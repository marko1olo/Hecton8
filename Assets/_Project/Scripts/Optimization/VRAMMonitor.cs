using System;
using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;
using Hecton8.Core;

namespace Hecton8.Optimization
{
    /// <summary>
    /// Monitors VRAM consumption and enforces budget thresholds.
    /// Executes in ISlowTickable (~0.5s interval) to avoid per-frame overhead.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8000)]
    public sealed class VRAMMonitor : MonoBehaviour, ISlowTickable, IGlobalRegistryHotSwapListener
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
        private RenderTextureLifecycleTracker _cachedRenderTextureLifecycle;
        
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
        /// Returns whether total VRAM exceeds the 1.8 GB MX350 ceiling.
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
        
        // ── LIFECYCLE ──────────────────────────────────────────────────────────────
        
        private void Awake()
        {
            _budgetThresholds = VRAMBudgetThresholds.ResolveRuntimeBudget(_budgetThresholds);
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
                _cachedRenderTextureLifecycle = currentService as RenderTextureLifecycleTracker;
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

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Core);
            _registeredSlowTick = GlobalRegistry.SlowTickables.Contains(this);
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
            _cachedRenderTextureLifecycle = GlobalRegistry.RenderTextureLifecycle;
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
        }
        
        private void CheckThresholds()
        {
            if (IsTextureMemoryOverBudget || IsRenderTextureMemoryOverBudget || IsTotalVRAMOverBudget)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (Time.time >= _nextLogTime)
                {
                    _nextLogTime = Time.time + 5f; // Throttle to once per 5s
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

            RenderTextureLifecycleTracker tracker = _cachedRenderTextureLifecycle;
            if (tracker != null)
                return tracker.TrackedRenderTextureMemoryBytes;

            return 0L;
        }

        private static long ReadTotalGraphicsMemoryBytes()
        {
            long graphicsDriverBytes = Profiler.GetAllocatedMemoryForGraphicsDriver();
            return graphicsDriverBytes > 0L ? graphicsDriverBytes : 0L;
        }
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void LogVRAMWarning()
        {
            _reportBuilder.Clear();
            _reportBuilder.Append("[VRAMMonitor] BUDGET EXCEEDED: ");
            _reportBuilder.Append("Texture=").Append((TextureMemoryBytes / (1024f * 1024f)).ToString("0.0")).Append("MB ");
            _reportBuilder.Append("RT=").Append((RenderTextureMemoryBytes / (1024f * 1024f)).ToString("0.0")).Append("MB ");
            _reportBuilder.Append("Total=").Append((TotalVRAMBytes / (1024f * 1024f)).ToString("0.0")).Append("MB");
            
            Debug.LogWarning(_reportBuilder.ToString(), this);
        }
#endif
    }
}
