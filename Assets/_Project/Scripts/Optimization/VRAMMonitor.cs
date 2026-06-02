using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;

namespace Hecton8.Optimization
{
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VramTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public long TextureBytes;
        [FieldOffset(16)] public long RenderTextureBytes;
        [FieldOffset(24)] public long TotalVramBytes;
        [FieldOffset(32)] public long GfxUsedBytes;
        [FieldOffset(40)] public long GraphicsBudgetBytes;
        [FieldOffset(48)] public float Pressure01;
        [FieldOffset(52)] public float GlobalQualityWeight01;
        [FieldOffset(56)] public byte PressureState;
        [FieldOffset(57)] public byte TextureMipLimit;
        [FieldOffset(58)] public ushort _pad0;
        [FieldOffset(60)] public uint _pad1;
    }

    /// <summary>
    /// Monitors VRAM consumption and enforces budget thresholds.
    /// Executes in ISlowTickable (~0.5s interval) to avoid per-frame overhead.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8000)]
    public sealed class VRAMMonitor : MonoBehaviour, ISlowTickable, IVramBudgetReadModel, IVramBudgetSampleSink, IGlobalRegistryHotSwapListener
    {
        /// <summary>
        /// High-level VRAM pressure state derived from budget utilization.
        /// </summary>
        public enum VRAMPressureState : byte
        {
            Stable = 0,
            Warning = 1,
            Critical = 2
        }

        // ── REGISTRY CACHE ─────────────────────────────────────────────────────────
        
        
        // ── INSPECTOR SETTINGS ─────────────────────────────────────────────────────
        
        private const int VramTelemetryCapacity = 300;
        private const BufferID VramTelemetryBufferId = (BufferID)71617;
        private const SystemID VramTelemetryOwner = SystemID.GraphicsScalability;
        private const uint TelemetryFlagTextureOverBudget = 1u << 0;
        private const uint TelemetryFlagRenderTextureOverBudget = 1u << 1;
        private const uint TelemetryFlagTotalOverBudget = 1u << 2;
        private const uint TelemetryFlagMissingGpuCounter = 1u << 3;

        [Header("── VRAM Budget Thresholds ──────────────────")]
        [SerializeField] private VRAMBudgetThresholds _budgetThresholds = VRAMBudgetThresholds.Default;
        [Tooltip("Budget utilization at which VRAM pressure moves from stable to warning state.")]
        [SerializeField, Range(0.5f, 1f)] private float _warningBudgetFraction = 0.85f;
        [Tooltip("Budget utilization at which VRAM pressure becomes critical even before the hard budget break.")]
        [SerializeField, Range(0.7f, 1.5f)] private float _criticalBudgetFraction = 0.95f;
        
        // ── PRIVATE STATE ──────────────────────────────────────────────────────────
        
        private bool _registeredSlowTick;
        private bool _registeredService;
        private bool _registeredHotSwapListener;
        private ProfilerRecorder _textureMemoryRecorder;
        private ProfilerRecorder _renderTextureMemoryRecorder;
        private ProfilerRecorder _gfxUsedMemoryRecorder;
        private IRenderTextureLifecycleService _cachedRenderTextureLifecycle;
        private IDataVault _dataVault;
        private VaultGenerationHandle<VramTelemetryEntry> _vramTelemetryHandle;
        private int _vramTelemetryCursor;
        private long _graphicsBudgetBytes;
        
        // COLD ALLOC: StringBuilder[1024] — zero-GC logging — owner: VRAMMonitor
        private readonly StringBuilder _reportBuilder = new StringBuilder(1024);
        // COLD ALLOC: List<ProfilerRecorderHandle>[128] — profiler counter discovery at startup only — owner: VRAMMonitor
        private readonly List<ProfilerRecorderHandle> _availableHandles = new List<ProfilerRecorderHandle>(128);

        private static readonly string[] _textureMemoryCandidates =
        {
            "Texture Memory",
            "Texture Used Memory"
        };

        private static readonly string[] _renderTextureMemoryCandidates =
        {
            "RenderTexture Memory",
            "Render Textures Bytes",
            "Render Textures Memory"
        };

        private static readonly string[] _gfxUsedMemoryCandidates =
        {
            "Gfx.UsedMemory",
            "Gfx Used Memory",
            "Gfx Used Memory Bytes"
        };
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static float _nextLogTime;
#endif
        
        // ── PUBLIC PROPERTIES ──────────────────────────────────────────────────────
        
        /// <summary>
        /// Current texture memory consumption in bytes.
        /// </summary>
        public long TextureMemoryBytes { get; private set; }
        
        /// <summary>
        /// Current RenderTexture memory consumption in bytes.
        /// </summary>
        public long RenderTextureMemoryBytes { get; private set; }
        
        /// <summary>
        /// Total VRAM consumption in bytes (textures + RenderTextures + meshes + shaders).
        /// </summary>
        public long TotalVRAMBytes { get; private set; }

        /// <summary>
        /// Raw graphics used-memory profiler counter in bytes when the platform exposes it.
        /// </summary>
        public long GfxUsedMemoryBytes { get; private set; }
        
        /// <summary>
        /// Returns whether texture memory exceeds 900 MB threshold.
        /// </summary>
        public bool IsTextureMemoryOverBudget => TextureMemoryBytes > _budgetThresholds.TextureMemoryBudgetBytes;
        
        /// <summary>
        /// Returns whether RenderTexture memory exceeds 500 MB threshold.
        /// </summary>
        public bool IsRenderTextureMemoryOverBudget => RenderTextureMemoryBytes > _budgetThresholds.RenderTextureMemoryBudgetBytes;
        
        /// <summary>
        /// Returns whether total VRAM exceeds the active runtime graphics budget.
        /// </summary>
        public bool IsTotalVRAMOverBudget => TotalVRAMBytes > _budgetThresholds.TotalVRAMBudgetBytes;
        
        /// <summary>
        /// Normalized RenderTexture budget utilization.
        /// </summary>
        public float RenderTextureBudgetUtilization { get; private set; }
        
        /// <summary>
        /// Normalized total VRAM budget utilization.
        /// </summary>
        public float TotalVRAMBudgetUtilization { get; private set; }

        /// <summary>
        /// Normalized texture budget utilization.
        /// </summary>
        public float TextureBudgetUtilization { get; private set; }
        
        /// <summary>
        /// Current high-level VRAM pressure state.
        /// </summary>
        public VRAMPressureState PressureState { get; private set; }

        public byte PressureStateCode => (byte)PressureState;
        
        // ── LIFECYCLE ──────────────────────────────────────────────────────────────
        
        private void Awake()
        {
            _budgetThresholds = VRAMBudgetThresholds.ResolveRuntimeBudget(_budgetThresholds);
            _graphicsBudgetBytes = _budgetThresholds.TotalVRAMBudgetBytes;
            StartRecorders();
        }
        
        private void OnEnable()
        {
            if (TryRegisterService())
            {
                CacheRegistryServicesCold();
                TryRegisterHotSwapListener();
                TryRegister();
            }
        }
        
        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
        }
        
        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            ReleaseVramTelemetryRing(_dataVault);
            _dataVault = null;
            _textureMemoryRecorder.Dispose();
            _renderTextureMemoryRecorder.Dispose();
            _gfxUsedMemoryRecorder.Dispose();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.RenderTextureLifecycleRuntime)
            {
                _cachedRenderTextureLifecycle = currentService as IRenderTextureLifecycleService;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                if (!ReferenceEquals(previousService, currentService))
                    ReleaseVramTelemetryRing(previousService as IDataVault);

                _dataVault = currentService as IDataVault;
                EnsureVramTelemetryRing();
            }
        }
        
        // ── ISLOWTICABLE ───────────────────────────────────────────────────────────
        
        /// <summary>
        /// ISlowTickable implementation. Measures VRAM every ~0.5s.
        /// Zero GC: pre-allocated buffers, no LINQ, no string concat.
        /// </summary>
        public void SlowTick()
        {
            MeasureVRAM();
            CheckThresholds();
        }
        
        // ── PUBLIC API ─────────────────────────────────────────────────────────────
        
        /// <summary>
        /// Queries current VRAM consumption breakdown.
        /// </summary>
        /// <param name="textureMemoryBytes">Texture memory in bytes.</param>
        /// <param name="renderTextureMemoryBytes">RenderTexture memory in bytes.</param>
        /// <param name="totalVRAMBytes">Total VRAM in bytes.</param>
        public void GetVRAMBreakdown(out long textureMemoryBytes, out long renderTextureMemoryBytes, out long totalVRAMBytes)
        {
            textureMemoryBytes = TextureMemoryBytes;
            renderTextureMemoryBytes = RenderTextureMemoryBytes;
            totalVRAMBytes = TotalVRAMBytes;
        }

        void IVramBudgetSampleSink.SampleVramCounters()
        {
            SlowTick();
        }
        
        // ── PRIVATE METHODS ────────────────────────────────────────────────────────
        
        private void StartRecorders()
        {
            _textureMemoryRecorder = TryStartMemoryRecorder(_textureMemoryCandidates);
            _renderTextureMemoryRecorder = TryStartMemoryRecorder(_renderTextureMemoryCandidates);
            _gfxUsedMemoryRecorder = TryStartMemoryRecorder(_gfxUsedMemoryCandidates);
        }

        private void TryRegister()
        {
            if (_registeredSlowTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;
            if (!ReferenceEquals(GlobalRegistry.VRAMMonitor, this))
                return;

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
        }

        private bool TryRegisterService()
        {
            if (_registeredService)
                return true;
            if (!Application.isPlaying)
                return false;

            VRAMMonitor registered = GlobalRegistry.VRAMMonitor;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return false;
            }

            GlobalRegistry.RegisterVRAMMonitorRuntime(this);
            _registeredService = ReferenceEquals(GlobalRegistry.VRAMMonitor, this);
            return _registeredService;
        }

        private void TryUnregister()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            _registeredSlowTick = false;
        }

        private void TryUnregisterService()
        {
            if (!_registeredService)
                return;

            GlobalRegistry.UnregisterVRAMMonitorRuntime(this);
            _registeredService = false;
        }

        private void CacheRegistryServicesCold()
        {
            _cachedRenderTextureLifecycle = GlobalRegistry.RenderTextureLifecycleService;
            _dataVault = GlobalRegistry.DataVault;
            EnsureVramTelemetryRing();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }
        
        private void MeasureVRAM()
        {
            TextureMemoryBytes = ReadRecorderValue(_textureMemoryRecorder);
            RenderTextureMemoryBytes = ReadRenderTextureMemoryBytes();
            GfxUsedMemoryBytes = ReadRecorderValue(_gfxUsedMemoryRecorder);
            TotalVRAMBytes = ReadTotalGraphicsMemoryBytes();
            if (TotalVRAMBytes < GfxUsedMemoryBytes)
                TotalVRAMBytes = GfxUsedMemoryBytes;
            if (TotalVRAMBytes < TextureMemoryBytes + RenderTextureMemoryBytes)
                TotalVRAMBytes = TextureMemoryBytes + RenderTextureMemoryBytes;

            TextureBudgetUtilization = CalculateBudgetUtilization(
                TextureMemoryBytes,
                _budgetThresholds.TextureMemoryBudgetBytes);
            RenderTextureBudgetUtilization = CalculateBudgetUtilization(
                RenderTextureMemoryBytes,
                _budgetThresholds.RenderTextureMemoryBudgetBytes);
            TotalVRAMBudgetUtilization = CalculateBudgetUtilization(
                TotalVRAMBytes,
                _budgetThresholds.TotalVRAMBudgetBytes);
            UpdatePressureState();
            WriteTelemetrySample();
        }
        
        private void CheckThresholds()
        {
            if (IsTextureMemoryOverBudget || IsRenderTextureMemoryOverBudget || IsTotalVRAMOverBudget)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
                if (now >= _nextLogTime)
                {
                    _nextLogTime = now + 5f; // Throttle to once per 5s
                    LogVRAMWarning();
                }
#endif
            }
        }

        private float CalculateBudgetUtilization(long usedBytes, long budgetBytes)
        {
            if (budgetBytes <= 0L)
                return 0f;

            return usedBytes / (float)budgetBytes;
        }

        private void UpdatePressureState()
        {
            float maxUtilization = TextureBudgetUtilization;
            if (RenderTextureBudgetUtilization > maxUtilization)
                maxUtilization = RenderTextureBudgetUtilization;
            if (TotalVRAMBudgetUtilization > maxUtilization)
                maxUtilization = TotalVRAMBudgetUtilization;

            if (IsTextureMemoryOverBudget || IsRenderTextureMemoryOverBudget || IsTotalVRAMOverBudget || maxUtilization >= _criticalBudgetFraction)
            {
                PressureState = VRAMPressureState.Critical;
                return;
            }

            PressureState = maxUtilization >= _warningBudgetFraction
                ? VRAMPressureState.Warning
                : VRAMPressureState.Stable;
        }

        private ProfilerRecorder TryStartMemoryRecorder(string[] candidates)
        {
            if (candidates == null || candidates.Length == 0)
                return default;

            _availableHandles.Clear();
            ProfilerRecorderHandle.GetAvailable(_availableHandles);
            for (int candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
            {
                string candidate = candidates[candidateIndex];
                for (int handleIndex = 0; handleIndex < _availableHandles.Count; handleIndex++)
                {
                    ProfilerRecorderDescription description = ProfilerRecorderHandle.GetDescription(_availableHandles[handleIndex]);
                    if (!string.Equals(description.Name, candidate, StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        return ProfilerRecorder.StartNew(
                            description.Category,
                            description.Name,
                            1,
                            ProfilerRecorderOptions.Default);
                    }
                    catch (ArgumentException)
                    {
                    }
                }
            }

            return default;
        }

        private static long ReadRecorderValue(ProfilerRecorder recorder)
        {
            if (!recorder.Valid)
                return 0L;

            long value = recorder.LastValue;
            return value > 0L ? value : 0L;
        }

        private long ReadRenderTextureMemoryBytes()
        {
            long recorderValue = ReadRecorderValue(_renderTextureMemoryRecorder);
            if (recorderValue > 0L)
                return recorderValue;

            IRenderTextureLifecycleService tracker = _cachedRenderTextureLifecycle;
            if (tracker != null)
                return tracker.TrackedRenderTextureMemoryBytes;

            return 0L;
        }

        private static long ReadTotalGraphicsMemoryBytes()
        {
            long graphicsDriverBytes = Profiler.GetAllocatedMemoryForGraphicsDriver();
            return graphicsDriverBytes > 0L ? graphicsDriverBytes : 0L;
        }

        private void EnsureVramTelemetryRing()
        {
            if (!Application.isPlaying || _vramTelemetryHandle.BufferID != 0u)
                return;

            IDataVault vault = _dataVault;
            if (vault == null || vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return;

            _vramTelemetryHandle = vault.EnsureGenerationHandle<VramTelemetryEntry>(
                VramTelemetryBufferId,
                VramTelemetryCapacity,
                VramTelemetryOwner,
                NativeArrayOptions.ClearMemory);
            _vramTelemetryCursor = 0;
        }

        private void ReleaseVramTelemetryRing(IDataVault vault)
        {
            if (vault != null && _vramTelemetryHandle.BufferID != 0u)
                vault.ReleaseBuffer(in _vramTelemetryHandle);

            _vramTelemetryHandle = default;
            _vramTelemetryCursor = 0;
        }

        private void WriteTelemetrySample()
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                _vramTelemetryHandle.BufferID == 0u ||
                !vault.TryAcquireWriteLock(in _vramTelemetryHandle, VramTelemetryOwner, out NativeArray<VramTelemetryEntry> ring))
            {
                return;
            }

            try
            {
                if (!ring.IsCreated || ring.Length == 0)
                    return;

                int capacity = math.min(VramTelemetryCapacity, ring.Length);
                int cursor = _vramTelemetryCursor;
                if ((uint)cursor >= (uint)capacity)
                    cursor = 0;

                ring[cursor] = new VramTelemetryEntry
                {
                    Frame = SystemDispatcher.CurrentFrameId,
                    Flags = ResolveTelemetryFlags(),
                    TextureBytes = TextureMemoryBytes,
                    RenderTextureBytes = RenderTextureMemoryBytes,
                    TotalVramBytes = TotalVRAMBytes,
                    GfxUsedBytes = GfxUsedMemoryBytes,
                    GraphicsBudgetBytes = _graphicsBudgetBytes,
                    Pressure01 = math.saturate(TotalVRAMBudgetUtilization),
                    GlobalQualityWeight01 = ResolveGlobalQualityWeight01(),
                    PressureState = (byte)PressureState,
                    TextureMipLimit = (byte)math.clamp(QualitySettings.globalTextureMipmapLimit, 0, 255)
                };

                cursor++;
                _vramTelemetryCursor = cursor < capacity ? cursor : 0;
            }
            finally
            {
                vault.ReleaseWriteLock(in _vramTelemetryHandle, VramTelemetryOwner);
            }
        }

        private uint ResolveTelemetryFlags()
        {
            uint flags = 0u;
            if (IsTextureMemoryOverBudget)
                flags |= TelemetryFlagTextureOverBudget;
            if (IsRenderTextureMemoryOverBudget)
                flags |= TelemetryFlagRenderTextureOverBudget;
            if (IsTotalVRAMOverBudget)
                flags |= TelemetryFlagTotalOverBudget;
            if (GfxUsedMemoryBytes <= 0L)
                flags |= TelemetryFlagMissingGpuCounter;
            return flags;
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, quality, math.isfinite(quality)));
        }
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void LogVRAMWarning()
        {
            _reportBuilder.Clear();
            _reportBuilder.Append("[VRAMMonitor] BUDGET EXCEEDED: ");
            _reportBuilder.Append("Texture=").Append((TextureMemoryBytes / (1024f * 1024f)).ToString("0.0", CultureInfo.InvariantCulture)).Append("MB ");
            _reportBuilder.Append("RT=").Append((RenderTextureMemoryBytes / (1024f * 1024f)).ToString("0.0", CultureInfo.InvariantCulture)).Append("MB ");
            _reportBuilder.Append("Total=").Append((TotalVRAMBytes / (1024f * 1024f)).ToString("0.0", CultureInfo.InvariantCulture)).Append("MB");
            
            Hecton8.Core.H8Debug.LogWarning(_reportBuilder.ToString(), this);
        }
#endif
    }
}
