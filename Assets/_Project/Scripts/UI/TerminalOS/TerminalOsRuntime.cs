using System;
using System.IO;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    public unsafe sealed class TerminalOsRuntime : MonoBehaviour, ILateFrameTickable
    {
        private const int ActiveRuntimeCapacity = 4;
        private const int HighResolution = 512;
        private const int LowResolution = 256;
        private const int LowTierFrameModulo = 6;
        private const float AttentionCullDistanceMeters = 20f;
        private const float AttentionCullDistanceSq = AttentionCullDistanceMeters * AttentionCullDistanceMeters;
        private const uint FaultLayoutMismatch = 1u << 0;
        private const uint FaultFormatBudget = 1u << 1;
        private const uint FaultNonFinite = 1u << 2;
        private const uint FaultVaultUnavailable = 1u << 3;
        private const string NativeOwner = nameof(TerminalOsRuntime);
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_TERMINAL_OS.bin";
        private const string DumpMirrorRelativePath = "Docs/AgentLogs/Dump_TERMINAL_OS.h8dump";
        private const BufferID TerminalStatesBufferId = (BufferID)70520;
        private const BufferID ScreenCommandsBufferId = (BufferID)70521;
        private const BufferID GlyphUvsBufferId = (BufferID)70522;
        private const BufferID TerminalPositionsBufferId = (BufferID)70523;
        private const BufferID TerminalForwardsBufferId = (BufferID)70524;
        private const BufferID DirtyIndicesBufferId = (BufferID)70525;
        private const BufferID TelemetryRingBufferId = (BufferID)70526;
        private const BufferID MockPowerBufferId = (BufferID)70527;
        private const BufferID MockDamageBufferId = (BufferID)70528;
        private const BufferID MockPowerStatusBufferId = (BufferID)70529;
        private const BufferID VirtualButtonsBufferId = (BufferID)70530;
        private const BufferID PanelInstancesBufferId = (BufferID)70531;
        private const BufferID TerminalClickScratchBufferId = (BufferID)70532;
        private const uint TerminalClickLaneHash = 0x54434C4Bu; // TCLK
        private const uint TerminalCommandLaneHash = 0x54434D44u; // TCMD
        private const string TerminalInstancedKeyword = "HECTON_TERMINAL_INSTANCED";

        // COLD ALLOC: TerminalOsRuntime[4] - active terminal runtime bridge for SHINOBU_49 diegetic glitch DTO writes - owner: TerminalOsRuntime
        private static readonly TerminalOsRuntime[] s_activeRuntimes = new TerminalOsRuntime[ActiveRuntimeCapacity];
        private static int s_activeRuntimeCount;
        private static readonly int TerminalTextureArrayId = Shader.PropertyToID("_TerminalTextureArray");
        private static readonly int TerminalPanelInstancesId = Shader.PropertyToID("_TerminalPanelInstances");
        private static readonly int TerminalStatesId = Shader.PropertyToID("_TerminalStates");
        private static readonly int ScreenCommandsId = Shader.PropertyToID("_ScreenCommands");
        private static readonly int DirtyTerminalIndicesId = Shader.PropertyToID("_DirtyTerminalIndices");
        private static readonly int GlyphUvsId = Shader.PropertyToID("_GlyphUvs");
        private static readonly int FontSdfAtlasId = Shader.PropertyToID("_FontSdfAtlas");
        private static readonly int FontAtlasReadyId = Shader.PropertyToID("_FontAtlasReady");
        private static readonly int TerminalResolutionXId = Shader.PropertyToID("_TerminalResolutionX");
        private static readonly int TerminalResolutionYId = Shader.PropertyToID("_TerminalResolutionY");
        private static readonly int DirtyTerminalCountId = Shader.PropertyToID("_DirtyTerminalCount");
        private static readonly int TimeSeedId = Shader.PropertyToID("_TimeSeed");

        [Header("GPU")]
        [SerializeField] private ComputeShader terminalBlitCompute;
        [SerializeField] private Texture2D fontSdfAtlas;
        [SerializeField] private Material terminalArrayMaterial;
        [SerializeField] private Mesh terminalPanelMesh;

        [Header("Scene Binding")]
        [SerializeField] private Camera attentionCameraOverride;
        [SerializeField] private Renderer[] terminalRenderers;
        [SerializeField] private Transform[] terminalTransforms;
        [SerializeField] private bool drawPanelsInstanced = true;

        [Header("Cold Data")]
        [SerializeField] private bool mockGeneratorEnabled = true;
        [SerializeField] private string layoutCsvRelativePath = "Assets/StreamingAssets/terminal_layouts.csv";

        private IDataVault _vault;
        private VaultBufferHandle<TerminalStateDTO> _terminalStatesHandle;
        private VaultBufferHandle<ScreenCommandDTO> _screenCommandsHandle;
        private VaultBufferHandle<float4> _glyphUvsHandle;
        private VaultBufferHandle<float4> _terminalPositionsHandle;
        private VaultBufferHandle<float4> _terminalForwardHandle;
        private VaultBufferHandle<int> _dirtyIndicesHandle;
        private VaultBufferHandle<TerminalTelemetryEntry> _telemetryRingHandle;
        private VaultBufferHandle<MockPowerStateSignal> _mockPowerSignalHandle;
        private VaultBufferHandle<MockDamageScalarSignal> _mockDamageSignalHandle;
        private VaultBufferHandle<MockPowerStatusSignal> _mockPowerStatusSignalHandle;
        private VaultBufferHandle<TerminalVirtualButtonDTO> _virtualButtonsHandle;
        private VaultBufferHandle<TerminalPanelInstanceDTO> _panelInstancesHandle;
        private VaultBufferHandle<TerminalClickSignal> _clickScratchHandle;

        private readonly GraphicsBuffer[] _stateBuffers = new GraphicsBuffer[2];
        private GraphicsBuffer _screenCommandBuffer;
        private GraphicsBuffer _glyphUvBuffer;
        private GraphicsBuffer _dirtyIndexBuffer;
        private GraphicsBuffer _panelInstanceBuffer;
        private RenderTexture _terminalTextureArray;
        private Camera _attentionCameraCache;
        private Bounds _panelRenderBounds;

        private JobHandle _formatHandle;
        private JobHandle _clickResolveHandle;
        private bool _formatScheduled;
        private bool _clickResolveScheduled;
        private bool _registeredLateFrame;
        private bool _nativeResourcesReady;
        private bool _graphicsResourcesReady;
        private bool _layoutUploadDirty;
        private bool _glyphUploadDirty;
        private bool _bindingsDirty;
        private bool _panelInstanceUploadDirty;
        private bool _blackBoxDumped;
        private bool _lowTier;
        private int _terminalCount;
        private int _buttonCount;
        private int _writeBufferIndex;
        private int _textureResolution;
        private int _blitKernel = -1;
        private int _groupsX;
        private int _groupsY;
        private int _threadsX = 8;
        private int _threadsY = 8;
        private int _telemetryCursor;
        private int _csvProbeFrame;
        private int _nextTierRefreshFrame;
        private int _nextCameraResolveFrame;
        private int _lastDirtyCount;
        private int _lastDispatchedCount;
        private uint _lastFaultFlags;
        private float _lastFormatMainThreadMilliseconds;
        private float _lastUploadMicroseconds;
        private float _lastDispatchMicroseconds;
        private float _lastPower01;
        private float _lastDamage01;
        private float _lastDiegeticGlitchIntensity;
        private HectonQualityTier _cachedTier = HectonQualityTier.Unknown;
        private string _csvFullPath;
        private string _dumpFullPath;
        private string _dumpMirrorFullPath;
        private byte[] _csvBuffer;
        private DateTime _csvLastWriteUtc;

        public void LateFrameTick()
        {
            EnsureRuntimeReady();
            if (!_nativeResourcesReady)
                return;

            int frame = Time.frameCount;
            RefreshScalabilityPolicy();
            TryFinalizeClickResolveJob();

            if (_formatScheduled)
            {
                if (!TryFinalizeCompletedJob(ref _formatHandle))
                {
                    RecordTelemetry(frame, 0, 0, _lastFaultFlags);
                    return;
                }

                _formatScheduled = false;
            }

            TryMonitorLayoutCsv(frame);

            int dirtyCount = BuildDirtyList();
            _lastDirtyCount = dirtyCount;
            int dispatchedCount = 0;
            if (dirtyCount > 0)
            {
                UploadDirtyPayloads(dirtyCount);
                dispatchedCount = DispatchDirtyScreens(dirtyCount);
                ClearDirtyFlags(dirtyCount);
            }

            _lastDispatchedCount = dispatchedCount;
            TryScheduleClickResolveJob();
            TryScheduleFormatJob(frame);
            UpdatePanelInstancesIfNeeded();
            RenderInstancedPanels();
            uint faultFlags = _lastFaultFlags;
            if (_terminalCount >= TerminalOsConstants.ActiveTargetTerminals && _lastFormatMainThreadMilliseconds > 0.5f)
                faultFlags |= FaultFormatBudget;
            if (faultFlags != 0u)
                TryDumpBlackBox(faultFlags);
            RecordTelemetry(frame, dirtyCount, dispatchedCount, faultFlags);
        }

        public bool QueueClick(in TerminalClickSignal signal)
        {
            EnsureRuntimeReady();
            if (!math.all(math.isfinite(signal.LocalUv)))
                return false;

            return SignalBus<TerminalClickSignal>.TryPush(in signal);
        }

        public void SetAttentionCamera(Camera camera)
        {
            attentionCameraOverride = camera;
            _attentionCameraCache = camera;
        }

        public bool TryDequeueCommand(out TerminalCommandSignal command)
        {
            command = default;
            if (_clickResolveScheduled)
                TryFinalizeClickResolveJob();

            return SignalBus<TerminalCommandSignal>.TryReadFrame(out command);
        }

        public RenderTexture GetTerminalTextureArray()
        {
            return _terminalTextureArray;
        }

        public int GetTerminalCount()
        {
            return _terminalCount;
        }

        public bool TryGetTerminalStateCopy(int index, out TerminalStateDTO state)
        {
            if (!TryResolveBuffer(ref _terminalStatesHandle, out NativeArray<TerminalStateDTO> terminalStates) ||
                index < 0 ||
                index >= _terminalCount)
            {
                state = default;
                return false;
            }

            state = terminalStates[index];
            return true;
        }

        public bool TryGetScreenCommandCopy(int index, out ScreenCommandDTO command)
        {
            if (!TryResolveBuffer(ref _screenCommandsHandle, out NativeArray<ScreenCommandDTO> screenCommands) ||
                index < 0 ||
                index >= _terminalCount)
            {
                command = default;
                return false;
            }

            command = screenCommands[index];
            return true;
        }

        public ref TerminalStateDTO GetTerminalStateRef(int index)
        {
            if (_vault == null || !_terminalStatesHandle.IsCreated || index < 0 || index >= _terminalCount)
                throw new ArgumentOutOfRangeException(nameof(index));

            return ref GetTerminalStateRefUnchecked(index);
        }

        public void SetScreenCommand(int index, float2 position, float scale)
        {
            if (!TryResolveBuffer(ref _screenCommandsHandle, out NativeArray<ScreenCommandDTO> screenCommands) ||
                index < 0 ||
                index >= _terminalCount)
                return;

            ScreenCommandDTO command = screenCommands[index];
            command.Position = SanitizeUv01(position);
            command.Scale = SanitizeScale(scale);
            screenCommands[index] = command;
            _layoutUploadDirty = true;
            ForceDirty(index);
        }

        public void ForceDirty(int index)
        {
            if (_vault == null || !_terminalStatesHandle.IsCreated || index < 0 || index >= _terminalCount)
                return;

            ref TerminalStateDTO state = ref GetTerminalStateRefUnchecked(index);
            state.IsDirty = 1;
        }

        public void ForceAllDirty()
        {
            if (_vault == null || !_terminalStatesHandle.IsCreated)
                return;

            for (int i = 0; i < _terminalCount; i++)
            {
                ref TerminalStateDTO state = ref GetTerminalStateRefUnchecked(i);
                state.IsDirty = 1;
            }
        }

        public static void ApplyDiegeticGlitchToActiveRuntimes(float intensity01)
        {
            float safeIntensity = math.saturate(math.isfinite(intensity01) ? intensity01 : 0f);
            int count = math.min(s_activeRuntimeCount, ActiveRuntimeCapacity);
            for (int i = 0; i < count; i++)
            {
                TerminalOsRuntime runtime = s_activeRuntimes[i];
                if (runtime != null && runtime.isActiveAndEnabled)
                    runtime.ApplyDiegeticGlitchIntensity(safeIntensity);
            }
        }

        private void ApplyDiegeticGlitchIntensity(float intensity01)
        {
            if (_vault == null || !_terminalStatesHandle.IsCreated)
            {
                _lastDiegeticGlitchIntensity = intensity01;
                return;
            }

            for (int i = 0; i < _terminalCount; i++)
            {
                ref TerminalStateDTO state = ref GetTerminalStateRefUnchecked(i);
                float current = math.isfinite(state.Value2) ? state.Value2 : 0f;
                float preservedExternal = current <= _lastDiegeticGlitchIntensity + 0.001f
                    ? 0f
                    : current;
                float next = math.saturate(math.max(preservedExternal, intensity01));
                if (math.abs(current - next) > 0.0005f || next > 0.001f)
                {
                    state.Value2 = next;
                    state.IsDirty = 1;
                }
            }

            _lastDiegeticGlitchIntensity = intensity01;
        }

        private void RegisterActiveRuntime()
        {
            for (int i = 0; i < s_activeRuntimeCount; i++)
            {
                if (ReferenceEquals(s_activeRuntimes[i], this))
                    return;
            }

            if (s_activeRuntimeCount >= ActiveRuntimeCapacity)
                return;

            s_activeRuntimes[s_activeRuntimeCount++] = this;
        }

        private void UnregisterActiveRuntime()
        {
            for (int i = 0; i < s_activeRuntimeCount; i++)
            {
                if (!ReferenceEquals(s_activeRuntimes[i], this))
                    continue;

                int last = s_activeRuntimeCount - 1;
                s_activeRuntimes[i] = s_activeRuntimes[last];
                s_activeRuntimes[last] = null;
                s_activeRuntimeCount = math.max(0, last);
                return;
            }
        }

        private void Awake()
        {
            EnsureColdPaths();
            ValidateLayouts();
            EnsureRuntimeReady();
        }

        private void OnEnable()
        {
            EnsureColdPaths();
            EnsureRuntimeReady();
            RegisterActiveRuntime();
            TryRegisterLateFrame();
        }

        private void OnDisable()
        {
            UnregisterActiveRuntime();
            TryUnregisterLateFrame();
            CompleteJobsForTeardown();
            DisposeGraphicsResources();
            DisposeNativeResources();
        }

        private void OnDestroy()
        {
            UnregisterActiveRuntime();
            TryUnregisterLateFrame();
            CompleteJobsForTeardown();
            DisposeGraphicsResources();
            DisposeNativeResources();
        }

        private void EnsureRuntimeReady()
        {
            EnsureColdPaths();
            RefreshScalabilityPolicy();
            EnsureNativeResources();
            EnsureGraphicsResources();
            TryRegisterLateFrame();
        }

        private void EnsureColdPaths()
        {
            if (_csvBuffer == null)
                _csvBuffer = new byte[8192];

            if (_attentionCameraCache == null && attentionCameraOverride != null)
                _attentionCameraCache = attentionCameraOverride;

            if (string.IsNullOrEmpty(_csvFullPath))
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                _csvFullPath = Path.GetFullPath(Path.Combine(projectRoot, layoutCsvRelativePath));
                _dumpFullPath = Path.GetFullPath(Path.Combine(projectRoot, DumpRelativePath));
                _dumpMirrorFullPath = Path.GetFullPath(Path.Combine(projectRoot, DumpMirrorRelativePath));
            }
        }

        private void ValidateLayouts()
        {
            _lastFaultFlags &= ~FaultLayoutMismatch;
            if (UnsafeUtility.SizeOf<TerminalStateDTO>() != TerminalOsConstants.TerminalStateStrideBytes ||
                UnsafeUtility.SizeOf<ScreenCommandDTO>() != TerminalOsConstants.ScreenCommandStrideBytes ||
                UnsafeUtility.SizeOf<TerminalPanelInstanceDTO>() != 80)
            {
                _lastFaultFlags |= FaultLayoutMismatch;
            }
        }

        private void RefreshScalabilityPolicy()
        {
            int frame = Time.frameCount;
            if (_textureResolution > 0 && frame < _nextTierRefreshFrame)
                return;

            _nextTierRefreshFrame = frame + (_lowTier ? 120 : 60);
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            if (tier == _cachedTier && _textureResolution > 0)
                return;

            bool lowTier = tier == HectonQualityTier.Unknown || tier == HectonQualityTier.Low || tier == HectonQualityTier.Mx350;
            int targetResolution = lowTier ? LowResolution : HighResolution;
            bool resolutionChanged = _textureResolution != targetResolution;
            _cachedTier = tier;
            _lowTier = lowTier;
            if (_terminalTextureArray != null && Application.isPlaying)
                return;

            _textureResolution = targetResolution;
            if (resolutionChanged)
            {
                ReleaseRenderTexture();
                _graphicsResourcesReady = false;
                _bindingsDirty = true;
                ForceAllDirty();
            }
        }

        private void EnsureNativeResources()
        {
            if (_nativeResourcesReady)
                return;

            _terminalCount = TerminalOsConstants.TerminalCapacity;
            bool vaultBacked = GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault);
            if (!vaultBacked)
            {
                _lastFaultFlags |= FaultVaultUnavailable;
                _terminalCount = 0;
                return;
            }

            _vault = vaultBacked ? vault : null;
            ResolveNativeBuffer(vault, TerminalStatesBufferId, _terminalCount, NativeArrayOptions.ClearMemory, out _terminalStatesHandle);
            ResolveNativeBuffer(vault, ScreenCommandsBufferId, _terminalCount, NativeArrayOptions.ClearMemory, out _screenCommandsHandle);
            ResolveNativeBuffer(vault, GlyphUvsBufferId, TerminalOsConstants.GlyphCount, NativeArrayOptions.UninitializedMemory, out _glyphUvsHandle);
            ResolveNativeBuffer(vault, TerminalPositionsBufferId, _terminalCount, NativeArrayOptions.UninitializedMemory, out _terminalPositionsHandle);
            ResolveNativeBuffer(vault, TerminalForwardsBufferId, _terminalCount, NativeArrayOptions.UninitializedMemory, out _terminalForwardHandle);
            ResolveNativeBuffer(vault, DirtyIndicesBufferId, _terminalCount, NativeArrayOptions.UninitializedMemory, out _dirtyIndicesHandle);
            ResolveNativeBuffer(vault, TelemetryRingBufferId, TerminalOsConstants.BlackBoxFrameCount, NativeArrayOptions.ClearMemory, out _telemetryRingHandle);
            ResolveNativeBuffer(vault, MockPowerBufferId, 1, NativeArrayOptions.ClearMemory, out _mockPowerSignalHandle);
            ResolveNativeBuffer(vault, MockDamageBufferId, 1, NativeArrayOptions.ClearMemory, out _mockDamageSignalHandle);
            ResolveNativeBuffer(vault, MockPowerStatusBufferId, 1, NativeArrayOptions.ClearMemory, out _mockPowerStatusSignalHandle);
            ResolveNativeBuffer(vault, VirtualButtonsBufferId, TerminalOsConstants.VirtualButtonCapacity, NativeArrayOptions.ClearMemory, out _virtualButtonsHandle);
            ResolveNativeBuffer(vault, PanelInstancesBufferId, _terminalCount, NativeArrayOptions.UninitializedMemory, out _panelInstancesHandle);
            ResolveNativeBuffer(vault, TerminalClickScratchBufferId, TerminalOsConstants.MaxQueuedClicks, NativeArrayOptions.UninitializedMemory, out _clickScratchHandle);
            ConfigureSignalLanes();

            if (!ValidateNativeBuffers())
            {
                _lastFaultFlags |= FaultVaultUnavailable;
                DisposeNativeResources();
                return;
            }

            InitializeTerminalState();
            GenerateEmergencyMockFont();
            _layoutUploadDirty = true;
            _glyphUploadDirty = true;
            _panelInstanceUploadDirty = true;
            _bindingsDirty = true;
            _nativeResourcesReady = true;
        }

        private static void ConfigureSignalLanes()
        {
            SignalBus<TerminalClickSignal>.Configure(
                TerminalOsConstants.MaxQueuedClicks,
                TerminalOsConstants.MaxQueuedClicks,
                16,
                TerminalClickLaneHash);
            SignalBus<TerminalClickSignal>.EnsureInitialized();
            SignalBus<TerminalCommandSignal>.Configure(
                TerminalOsConstants.MaxQueuedClicks,
                TerminalOsConstants.MaxQueuedClicks,
                16,
                TerminalCommandLaneHash);
            SignalBus<TerminalCommandSignal>.EnsureInitialized();
        }

        private static void ResolveNativeBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int length,
            NativeArrayOptions options,
            out VaultBufferHandle<T> handle) where T : struct
        {
            handle = default;
            if (vault == null)
                return;

            handle = vault.GetBufferHandle<T>(bufferId, length, SystemID.UI, options);
        }

        private bool ValidateNativeBuffers()
        {
            return TryResolveBuffer(ref _terminalStatesHandle, out NativeArray<TerminalStateDTO> _) &&
                   TryResolveBuffer(ref _screenCommandsHandle, out NativeArray<ScreenCommandDTO> _) &&
                   TryResolveBuffer(ref _glyphUvsHandle, out NativeArray<float4> _) &&
                   TryResolveBuffer(ref _terminalPositionsHandle, out NativeArray<float4> _) &&
                   TryResolveBuffer(ref _terminalForwardHandle, out NativeArray<float4> _) &&
                   TryResolveBuffer(ref _dirtyIndicesHandle, out NativeArray<int> _) &&
                   TryResolveBuffer(ref _telemetryRingHandle, out NativeArray<TerminalTelemetryEntry> _) &&
                   TryResolveBuffer(ref _mockPowerSignalHandle, out NativeArray<MockPowerStateSignal> _) &&
                   TryResolveBuffer(ref _mockDamageSignalHandle, out NativeArray<MockDamageScalarSignal> _) &&
                   TryResolveBuffer(ref _mockPowerStatusSignalHandle, out NativeArray<MockPowerStatusSignal> _) &&
                   TryResolveBuffer(ref _virtualButtonsHandle, out NativeArray<TerminalVirtualButtonDTO> _) &&
                   TryResolveBuffer(ref _panelInstancesHandle, out NativeArray<TerminalPanelInstanceDTO> _) &&
                   TryResolveBuffer(ref _clickScratchHandle, out NativeArray<TerminalClickSignal> _);
        }

        private bool TryResolveBuffer<T>(ref VaultBufferHandle<T> handle, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (_vault == null || !handle.IsCreated)
                return false;

            buffer = handle.Resolve(_vault);
            return buffer.IsCreated;
        }

        private void InitializeTerminalState()
        {
            if (!TryResolveBuffer(ref _terminalStatesHandle, out NativeArray<TerminalStateDTO> terminalStates) ||
                !TryResolveBuffer(ref _screenCommandsHandle, out NativeArray<ScreenCommandDTO> screenCommands) ||
                !TryResolveBuffer(ref _terminalPositionsHandle, out NativeArray<float4> terminalPositions) ||
                !TryResolveBuffer(ref _terminalForwardHandle, out NativeArray<float4> terminalForward) ||
                !TryResolveBuffer(ref _panelInstancesHandle, out NativeArray<TerminalPanelInstanceDTO> panelInstances) ||
                !TryResolveBuffer(ref _virtualButtonsHandle, out NativeArray<TerminalVirtualButtonDTO> virtualButtons))
            {
                _lastFaultFlags |= FaultVaultUnavailable;
                return;
            }

            _buttonCount = 0;
            for (int i = 0; i < _terminalCount; i++)
            {
                uint terminalHash = TerminalOsHash.HashIndex(i);
                TerminalStateDTO state = default;
                state.TerminalHash = terminalHash;
                state.BackgroundColor = 0x00061418u;
                state.Value1 = 0.75f;
                state.Value2 = 0f;
                TerminalAsciiFormatter.WritePowerLine(ref state.TextLine, 75, 0, true);
                state.IsDirty = 1;
                terminalStates[i] = state;

                int col = i & 7;
                int row = i >> 3;
                screenCommands[i] = new ScreenCommandDTO
                {
                    FontAtlasUV_Packed = 0u,
                    Position = new float2(0.055f, 0.63f - ((row & 1) * 0.015f)),
                    Scale = 0.075f
                };

                terminalPositions[i] = new float4((col - 3.5f) * 2.25f, 1.35f + ((row & 3) * 0.18f), 4.5f + row * 0.85f, 0f);
                terminalForward[i] = new float4(0f, 0f, -1f, 0f);
                panelInstances[i] = new TerminalPanelInstanceDTO
                {
                    LocalToWorld = float4x4.TRS(terminalPositions[i].xyz, quaternion.identity, new float3(1.25f, 0.72f, 1f)),
                    SliceFlags = new float4(i, 0f, 0f, 0f)
                };
                AddVirtualButton(virtualButtons, terminalHash, TerminalOsConstants.CommandOpenDoor, new float4(0.08f, 0.08f, 0.34f, 0.18f));
                AddVirtualButton(virtualButtons, terminalHash, TerminalOsConstants.CommandAcknowledge, new float4(0.66f, 0.08f, 0.92f, 0.18f));
            }

            RecalculatePanelRenderBounds();
        }

        private void AddVirtualButton(NativeArray<TerminalVirtualButtonDTO> virtualButtons, uint terminalHash, uint commandHash, float4 rectUv)
        {
            if (_buttonCount >= virtualButtons.Length)
                return;

            virtualButtons[_buttonCount++] = new TerminalVirtualButtonDTO
            {
                TerminalHash = terminalHash,
                CommandHash = commandHash,
                RectUv = rectUv
            };
        }

        private void GenerateEmergencyMockFont()
        {
            if (!TryResolveBuffer(ref _glyphUvsHandle, out NativeArray<float4> glyphUvs))
                return;

            const float invGrid = 1f / 16f;
            for (int i = 0; i < TerminalOsConstants.GlyphCount; i++)
            {
                int col = i & 15;
                int row = i >> 4;
                float2 uv0 = new float2(col * invGrid, row * invGrid);
                float2 uv1 = uv0 + new float2(invGrid, invGrid);
                glyphUvs[i] = new float4(uv0.x, uv0.y, uv1.x, uv1.y);
            }
        }

        private void EnsureGraphicsResources()
        {
            if (!TryResolveBuffer(ref _terminalStatesHandle, out NativeArray<TerminalStateDTO> _))
                return;

            EnsureTextureArray();
            if (_stateBuffers[0] == null)
                _stateBuffers[0] = CreateStructuredLockBuffer<TerminalStateDTO>(_terminalCount);
            if (_stateBuffers[1] == null)
                _stateBuffers[1] = CreateStructuredLockBuffer<TerminalStateDTO>(_terminalCount);
            if (_screenCommandBuffer == null)
                _screenCommandBuffer = CreateStructuredLockBuffer<ScreenCommandDTO>(_terminalCount);
            if (_glyphUvBuffer == null)
                _glyphUvBuffer = CreateStructuredLockBuffer<float4>(TerminalOsConstants.GlyphCount);
            if (_dirtyIndexBuffer == null)
                _dirtyIndexBuffer = CreateStructuredLockBuffer<int>(_terminalCount);
            if (_panelInstanceBuffer == null)
                _panelInstanceBuffer = CreateStructuredLockBuffer<TerminalPanelInstanceDTO>(_terminalCount);

            if (_layoutUploadDirty)
                UploadScreenCommands();
            if (_glyphUploadDirty)
                UploadGlyphUvs();
            if (_panelInstanceUploadDirty)
                UploadPanelInstances();
            if (_bindingsDirty)
                BindTerminalRenderers();

            ResolveComputeKernel();
            RefreshDispatchGroupCounts();
            _graphicsResourcesReady = _terminalTextureArray != null &&
                                      _stateBuffers[0] != null &&
                                      _stateBuffers[1] != null &&
                                      _screenCommandBuffer != null &&
                                      _glyphUvBuffer != null &&
                                      _dirtyIndexBuffer != null &&
                                      _panelInstanceBuffer != null;
        }

        private void EnsureTextureArray()
        {
            int resolution = _textureResolution > 0 ? _textureResolution : (_lowTier ? LowResolution : HighResolution);
            if (_terminalTextureArray != null &&
                _terminalTextureArray.width == resolution &&
                _terminalTextureArray.height == resolution &&
                _terminalTextureArray.volumeDepth == TerminalOsConstants.TerminalCapacity)
                return;

            ReleaseRenderTexture();
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(resolution, resolution, GraphicsFormat.R8G8B8A8_UNorm, 0)
            {
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = TerminalOsConstants.TerminalCapacity,
                enableRandomWrite = true,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear
            };

            _terminalTextureArray = new RenderTexture(descriptor)
            {
                name = _lowTier ? "H8_TerminalOS_Array_256x64" : "H8_TerminalOS_Array_512x64",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _terminalTextureArray.Create();
            Shader.SetGlobalTexture(TerminalTextureArrayId, _terminalTextureArray);
            if (terminalArrayMaterial != null)
                terminalArrayMaterial.SetTexture(TerminalTextureArrayId, _terminalTextureArray);
            _bindingsDirty = true;
        }

        private static GraphicsBuffer CreateStructuredLockBuffer<T>(int count) where T : struct
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                count,
                UnsafeUtility.SizeOf<T>());
        }

        private void ResolveComputeKernel()
        {
            if (_blitKernel >= 0 || terminalBlitCompute == null)
                return;

            _blitKernel = terminalBlitCompute.FindKernel("KTerminalBlit");
            terminalBlitCompute.GetKernelThreadGroupSizes(_blitKernel, out uint x, out uint y, out _);
            _threadsX = (int)math.max(1u, x);
            _threadsY = (int)math.max(1u, y);
        }

        private void RefreshDispatchGroupCounts()
        {
            int resolution = math.max(1, _textureResolution);
            _groupsX = (resolution + _threadsX - 1) / _threadsX;
            _groupsY = (resolution + _threadsY - 1) / _threadsY;
        }

        private int BuildDirtyList()
        {
            if (!TryResolveBuffer(ref _dirtyIndicesHandle, out NativeArray<int> dirtyIndices) ||
                _vault == null ||
                !_terminalStatesHandle.IsCreated)
                return 0;

            bool hasCamera = TryResolveCameraFrame(out float3 cameraPosition, out float3 cameraForward);
            int dirtyCount = 0;
            _lastFaultFlags &= ~FaultNonFinite;
            for (int i = 0; i < _terminalCount; i++)
            {
                ref TerminalStateDTO state = ref GetTerminalStateRefUnchecked(i);
                if (state.IsDirty == 0)
                    continue;

                if (hasCamera && !PassesAttentionCull(i, cameraPosition, cameraForward))
                {
                    state.IsDirty = 0;
                    continue;
                }

                if (!math.isfinite(state.Value1) || !math.isfinite(state.Value2))
                {
                    state.IsDirty = 0;
                    _lastFaultFlags |= FaultNonFinite;
                    continue;
                }

                dirtyIndices[dirtyCount++] = i;
            }

            return dirtyCount;
        }

        private bool TryResolveCameraFrame(out float3 cameraPosition, out float3 cameraForward)
        {
            Camera camera = attentionCameraOverride != null ? attentionCameraOverride : _attentionCameraCache;
            int frame = Time.frameCount;
            if (camera == null && frame >= _nextCameraResolveFrame)
            {
                _nextCameraResolveFrame = frame + 30;
                camera = _attentionCameraCache;
            }

            if (camera == null)
            {
                cameraPosition = default;
                cameraForward = default;
                return false;
            }

            Transform cameraTransform = camera.transform;
            Vector3 position = cameraTransform.position;
            Vector3 forward = cameraTransform.forward;
            cameraPosition = new float3(position.x, position.y, position.z);
            cameraForward = math.normalizesafe(new float3(forward.x, forward.y, forward.z), new float3(0f, 0f, 1f));
            bool finite = math.all(math.isfinite(cameraPosition)) && math.all(math.isfinite(cameraForward));
            if (!finite)
            {
                _lastFaultFlags |= FaultNonFinite;
                cameraPosition = default;
                cameraForward = default;
            }

            return finite;
        }

        private bool PassesAttentionCull(int index, float3 cameraPosition, float3 cameraForward)
        {
            float3 terminalPosition = ResolveTerminalPosition(index);
            if (!math.all(math.isfinite(terminalPosition)) ||
                !math.all(math.isfinite(cameraPosition)) ||
                !math.all(math.isfinite(cameraForward)))
            {
                _lastFaultFlags |= FaultNonFinite;
                return false;
            }

            float3 toTerminal = terminalPosition - cameraPosition;
            float distanceSq = math.lengthsq(toTerminal);
            if (!math.isfinite(distanceSq))
            {
                _lastFaultFlags |= FaultNonFinite;
                return false;
            }

            if (distanceSq > AttentionCullDistanceSq)
                return false;

            if (distanceSq <= 0.0001f)
                return true;

            float3 direction = toTerminal * math.rsqrt(distanceSq);
            return math.dot(cameraForward, direction) > 0f;
        }

        private float3 ResolveTerminalPosition(int index)
        {
            Transform terminal = ResolveTerminalTransform(index);
            if (terminal != null)
            {
                Vector3 position = terminal.position;
                return new float3(position.x, position.y, position.z);
            }

            return TryResolveBuffer(ref _terminalPositionsHandle, out NativeArray<float4> terminalPositions) && index < terminalPositions.Length
                ? terminalPositions[index].xyz
                : default;
        }

        private int ResolveBoundPanelCount()
        {
            int transformCount = terminalTransforms != null ? terminalTransforms.Length : 0;
            int rendererCount = terminalRenderers != null ? terminalRenderers.Length : 0;
            return math.max(transformCount, rendererCount);
        }

        private Transform ResolveTerminalTransform(int index)
        {
            if (terminalTransforms != null && index >= 0 && index < terminalTransforms.Length && terminalTransforms[index] != null)
                return terminalTransforms[index];

            if (terminalRenderers != null && index >= 0 && index < terminalRenderers.Length && terminalRenderers[index] != null)
                return terminalRenderers[index].transform;

            return null;
        }

        private void UploadDirtyPayloads(int dirtyCount)
        {
            if (!_graphicsResourcesReady || dirtyCount <= 0)
                return;

            long start = Stopwatch.GetTimestamp();
            UploadDirtyIndices(dirtyCount);
            UploadDirtyStates(dirtyCount, _stateBuffers[_writeBufferIndex]);
            _lastUploadMicroseconds = ElapsedMicroseconds(start);
        }

        private void UploadDirtyIndices(int dirtyCount)
        {
            if (!TryResolveBuffer(ref _dirtyIndicesHandle, out NativeArray<int> dirtyIndices))
                return;

            NativeArray<int> mapped = _dirtyIndexBuffer.LockBufferForWrite<int>(0, dirtyCount);
            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(dirtyIndices);
            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
            long copyBytes = (long)UnsafeUtility.SizeOf<int>() * dirtyCount;
            long destinationBytes = (long)UnsafeUtility.SizeOf<int>() * mapped.Length;
            if (!UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes))
                UnsafeMemoryCopyGuard.ReportRejectedCopy(NativeOwner);
            _dirtyIndexBuffer.UnlockBufferAfterWrite<int>(dirtyCount);
        }

        private void UploadDirtyStates(int dirtyCount, GraphicsBuffer buffer)
        {
            if (!TryResolveBuffer(ref _dirtyIndicesHandle, out NativeArray<int> dirtyIndices))
                return;

            int runStart = dirtyIndices[0];
            int runEnd = runStart;
            for (int i = 1; i < dirtyCount; i++)
            {
                int index = dirtyIndices[i];
                if (index == runEnd + 1)
                {
                    runEnd = index;
                    continue;
                }

                UploadStateRun(buffer, runStart, runEnd - runStart + 1);
                runStart = index;
                runEnd = index;
            }

            UploadStateRun(buffer, runStart, runEnd - runStart + 1);
        }

        private void UploadStateRun(GraphicsBuffer buffer, int startIndex, int count)
        {
            if (count <= 0)
                return;

            NativeArray<TerminalStateDTO> mapped = buffer.LockBufferForWrite<TerminalStateDTO>(startIndex, count);
            byte* sourceBase = (byte*)ResolveTerminalStatePointer();
            if (sourceBase == null)
            {
                _lastFaultFlags |= FaultVaultUnavailable;
                return;
            }

            void* sourcePtr = sourceBase + (startIndex * UnsafeUtility.SizeOf<TerminalStateDTO>());
            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
            long copyBytes = (long)UnsafeUtility.SizeOf<TerminalStateDTO>() * count;
            long destinationBytes = (long)UnsafeUtility.SizeOf<TerminalStateDTO>() * mapped.Length;
            if (!UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes))
                UnsafeMemoryCopyGuard.ReportRejectedCopy(NativeOwner);
            buffer.UnlockBufferAfterWrite<TerminalStateDTO>(count);
        }

        private void UploadScreenCommands()
        {
            if (_screenCommandBuffer == null ||
                !TryResolveBuffer(ref _screenCommandsHandle, out NativeArray<ScreenCommandDTO> screenCommands))
                return;

            NativeArray<ScreenCommandDTO> mapped = _screenCommandBuffer.LockBufferForWrite<ScreenCommandDTO>(0, _terminalCount);
            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(screenCommands);
            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
            long copyBytes = (long)UnsafeUtility.SizeOf<ScreenCommandDTO>() * _terminalCount;
            long destinationBytes = (long)UnsafeUtility.SizeOf<ScreenCommandDTO>() * mapped.Length;
            if (!UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes))
                UnsafeMemoryCopyGuard.ReportRejectedCopy(NativeOwner);
            _screenCommandBuffer.UnlockBufferAfterWrite<ScreenCommandDTO>(_terminalCount);
            _layoutUploadDirty = false;
        }

        private void UploadGlyphUvs()
        {
            if (_glyphUvBuffer == null ||
                !TryResolveBuffer(ref _glyphUvsHandle, out NativeArray<float4> glyphUvs))
                return;

            NativeArray<float4> mapped = _glyphUvBuffer.LockBufferForWrite<float4>(0, TerminalOsConstants.GlyphCount);
            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(glyphUvs);
            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
            long copyBytes = (long)UnsafeUtility.SizeOf<float4>() * TerminalOsConstants.GlyphCount;
            long destinationBytes = (long)UnsafeUtility.SizeOf<float4>() * mapped.Length;
            if (!UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes))
                UnsafeMemoryCopyGuard.ReportRejectedCopy(NativeOwner);
            _glyphUvBuffer.UnlockBufferAfterWrite<float4>(TerminalOsConstants.GlyphCount);
            _glyphUploadDirty = false;
        }

        private void UploadPanelInstances()
        {
            if (_panelInstanceBuffer == null ||
                !TryResolveBuffer(ref _panelInstancesHandle, out NativeArray<TerminalPanelInstanceDTO> panelInstances))
                return;

            NativeArray<TerminalPanelInstanceDTO> mapped = _panelInstanceBuffer.LockBufferForWrite<TerminalPanelInstanceDTO>(0, _terminalCount);
            void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(panelInstances);
            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
            long copyBytes = (long)UnsafeUtility.SizeOf<TerminalPanelInstanceDTO>() * _terminalCount;
            long destinationBytes = (long)UnsafeUtility.SizeOf<TerminalPanelInstanceDTO>() * mapped.Length;
            if (!UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes))
                UnsafeMemoryCopyGuard.ReportRejectedCopy(NativeOwner);
            _panelInstanceBuffer.UnlockBufferAfterWrite<TerminalPanelInstanceDTO>(_terminalCount);
            _panelInstanceUploadDirty = false;
        }

        private int DispatchDirtyScreens(int dirtyCount)
        {
            if (terminalBlitCompute == null || _blitKernel < 0 || _terminalTextureArray == null || dirtyCount <= 0)
                return 0;

            long start = Stopwatch.GetTimestamp();
            GraphicsBuffer stateBuffer = _stateBuffers[_writeBufferIndex];
            terminalBlitCompute.SetTexture(_blitKernel, TerminalTextureArrayId, _terminalTextureArray);
            terminalBlitCompute.SetBuffer(_blitKernel, TerminalStatesId, stateBuffer);
            terminalBlitCompute.SetBuffer(_blitKernel, ScreenCommandsId, _screenCommandBuffer);
            terminalBlitCompute.SetBuffer(_blitKernel, DirtyTerminalIndicesId, _dirtyIndexBuffer);
            terminalBlitCompute.SetBuffer(_blitKernel, GlyphUvsId, _glyphUvBuffer);
            if (fontSdfAtlas != null)
                terminalBlitCompute.SetTexture(_blitKernel, FontSdfAtlasId, fontSdfAtlas);
            terminalBlitCompute.SetInt(FontAtlasReadyId, fontSdfAtlas != null ? 1 : 0);
            terminalBlitCompute.SetInt(TerminalResolutionXId, _textureResolution);
            terminalBlitCompute.SetInt(TerminalResolutionYId, _textureResolution);
            terminalBlitCompute.SetInt(DirtyTerminalCountId, dirtyCount);
            terminalBlitCompute.SetFloat(TimeSeedId, Time.unscaledTime);
            terminalBlitCompute.Dispatch(_blitKernel, _groupsX, _groupsY, dirtyCount);
            _writeBufferIndex = 1 - _writeBufferIndex;
            _lastDispatchMicroseconds = ElapsedMicroseconds(start);
            return dirtyCount;
        }

        private void ClearDirtyFlags(int dirtyCount)
        {
            if (!TryResolveBuffer(ref _dirtyIndicesHandle, out NativeArray<int> dirtyIndices))
                return;

            for (int i = 0; i < dirtyCount; i++)
            {
                ref TerminalStateDTO state = ref GetTerminalStateRefUnchecked(dirtyIndices[i]);
                state.IsDirty = 0;
            }
        }

        private void TryScheduleFormatJob(int frame)
        {
            if (_formatScheduled ||
                !mockGeneratorEnabled ||
                _vault == null ||
                !_terminalStatesHandle.IsCreated)
                return;

            if (_lowTier && frame % LowTierFrameModulo != 0)
                return;

            UpdateMockSignals((uint)frame);
            if (!TryResolveBuffer(ref _mockPowerSignalHandle, out NativeArray<MockPowerStateSignal> mockPowerSignal) ||
                !TryResolveBuffer(ref _mockDamageSignalHandle, out NativeArray<MockDamageScalarSignal> mockDamageSignal) ||
                !TryResolveBuffer(ref _mockPowerStatusSignalHandle, out NativeArray<MockPowerStatusSignal> mockPowerStatusSignal))
            {
                _lastFaultFlags |= FaultVaultUnavailable;
                return;
            }

            long start = Stopwatch.GetTimestamp();
            TerminalStateDTO* statePtr = ResolveTerminalStatePointer();
            if (statePtr == null)
            {
                _lastFaultFlags |= FaultVaultUnavailable;
                return;
            }

            _formatHandle = new UpdateTerminalTextJob
            {
                States = statePtr,
                PowerSignals = mockPowerSignal,
                DamageSignals = mockDamageSignal,
                PowerStatusSignals = mockPowerStatusSignal,
                TerminalCount = _terminalCount,
                Frame = (uint)frame
            }.Schedule(_terminalCount, 16);
            _formatScheduled = true;
            _lastFormatMainThreadMilliseconds = ElapsedMilliseconds(start);
        }

        private void UpdateMockSignals(uint frame)
        {
            if (!TryResolveBuffer(ref _mockPowerSignalHandle, out NativeArray<MockPowerStateSignal> mockPowerSignal) ||
                !TryResolveBuffer(ref _mockDamageSignalHandle, out NativeArray<MockDamageScalarSignal> mockDamageSignal) ||
                !TryResolveBuffer(ref _mockPowerStatusSignalHandle, out NativeArray<MockPowerStatusSignal> mockPowerStatusSignal))
            {
                _lastFaultFlags |= FaultVaultUnavailable;
                return;
            }

            MockTerminalDataGenerator generator = default;
            float power01 = generator.ResolvePower01(frame);
            float damage01 = generator.ResolveDamage01(frame);
            mockPowerSignal[0] = new MockPowerStateSignal { Frame = frame, MockPowerLevel = power01 * 100f };
            mockDamageSignal[0] = new MockDamageScalarSignal { Frame = frame, Damage01 = damage01 };
            mockPowerStatusSignal[0] = generator.ResolvePowerStatus(frame, power01);
            _lastPower01 = power01;
            _lastDamage01 = damage01;
        }

        private void TryScheduleClickResolveJob()
        {
            if (_clickResolveScheduled ||
                !TryResolveBuffer(ref _clickScratchHandle, out NativeArray<TerminalClickSignal> clickScratch) ||
                !TryResolveBuffer(ref _virtualButtonsHandle, out NativeArray<TerminalVirtualButtonDTO> virtualButtons))
                return;

            int count = math.min(
                math.min(SignalBus<TerminalClickSignal>.SnapshotCount, TerminalOsConstants.MaxQueuedClicks),
                clickScratch.Length);
            if (count <= 0)
                return;

            NativeArray<TerminalClickSignal>.ReadOnly snapshot = SignalBus<TerminalClickSignal>.GetFrameSnapshotArray();
            for (int i = 0; i < count; i++)
                clickScratch[i] = snapshot[i];

            _clickResolveHandle = new TerminalClickResolveJob
            {
                Clicks = clickScratch.AsReadOnly(),
                ClickCount = count,
                Buttons = virtualButtons,
                ButtonCount = _buttonCount,
                Commands = SignalBus<TerminalCommandSignal>.ParallelWriter
            }.Schedule(count, 1);
            _clickResolveScheduled = true;
        }

        private void TryFinalizeClickResolveJob()
        {
            if (!_clickResolveScheduled)
                return;

            if (TryFinalizeCompletedJob(ref _clickResolveHandle))
                _clickResolveScheduled = false;
        }

        private void TryMonitorLayoutCsv(int frame)
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return;
#else
            if (_csvBuffer == null || string.IsNullOrEmpty(_csvFullPath))
                return;

            if (frame < _csvProbeFrame)
                return;

            _csvProbeFrame = frame + (_lowTier ? 120 : 30);
            if (!File.Exists(_csvFullPath))
                return;

            DateTime writeUtc = File.GetLastWriteTimeUtc(_csvFullPath);
            if (writeUtc <= _csvLastWriteUtc)
                return;

            _csvLastWriteUtc = writeUtc;
            int bytesRead = 0;
            using (FileStream stream = new FileStream(_csvFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                bytesRead = stream.Read(_csvBuffer, 0, _csvBuffer.Length);
            }

            if (bytesRead > 0 && ParseLayoutCsv(_csvBuffer, bytesRead))
            {
                _layoutUploadDirty = true;
                ForceAllDirty();
            }
#endif
        }

        private bool ParseLayoutCsv(byte[] bytes, int byteCount)
        {
            bool changed = false;
            int lineStart = 0;
            for (int i = 0; i <= byteCount; i++)
            {
                bool lineEnd = i == byteCount || bytes[i] == (byte)'\n' || bytes[i] == (byte)'\r';
                if (!lineEnd)
                    continue;

                if (i > lineStart)
                    changed |= TryParseLayoutLine(bytes, lineStart, i);
                lineStart = i + 1;
            }

            return changed;
        }

        private bool TryParseLayoutLine(byte[] bytes, int start, int end)
        {
            int a = FindCsvComma(bytes, start, end);
            if (a <= start)
                return false;
            int b = FindCsvComma(bytes, a + 1, end);
            int c = FindCsvComma(bytes, b + 1, end);
            if (b <= a || c <= b)
                return false;

            uint hash = ParseHashOrName(bytes, start, a);
            if (!TryParseFloat(bytes, a + 1, b, out float x) ||
                !TryParseFloat(bytes, b + 1, c, out float y) ||
                !TryParseFloat(bytes, c + 1, end, out float scale))
                return false;

            int index = FindTerminalIndex(hash);
            if (index < 0)
                return false;

            if (!TryResolveBuffer(ref _screenCommandsHandle, out NativeArray<ScreenCommandDTO> screenCommands))
                return false;

            ScreenCommandDTO command = screenCommands[index];
            command.Position = SanitizeUv01(new float2(x, y));
            command.Scale = SanitizeScale(scale);
            screenCommands[index] = command;
            return true;
        }

        private static int FindCsvComma(byte[] bytes, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                if (bytes[i] == (byte)',')
                    return i;
            }

            return -1;
        }

        private static uint ParseHashOrName(byte[] bytes, int start, int end)
        {
            uint numeric = 0u;
            bool numericOnly = end > start;
            for (int i = start; i < end; i++)
            {
                byte value = bytes[i];
                if (value < (byte)'0' || value > (byte)'9')
                {
                    numericOnly = false;
                    break;
                }

                numeric = numeric * 10u + (uint)(value - (byte)'0');
            }

            if (numericOnly)
                return numeric;

            uint hash = 2166136261u;
            for (int i = start; i < end; i++)
            {
                byte value = bytes[i];
                if (value > 32)
                    hash = TerminalOsHash.Fnv1A(value, hash);
            }

            return hash == 0u ? 1u : hash;
        }

        private static bool TryParseFloat(byte[] bytes, int start, int end, out float value)
        {
            value = 0f;
            if (end <= start)
                return false;

            int i = start;
            bool negative = false;
            if (bytes[i] == (byte)'-')
            {
                negative = true;
                i++;
            }

            float result = 0f;
            bool any = false;
            for (; i < end; i++)
            {
                byte c = bytes[i];
                if (c == (byte)'.')
                {
                    i++;
                    break;
                }

                if (c < (byte)'0' || c > (byte)'9')
                    return false;

                result = result * 10f + (c - (byte)'0');
                any = true;
            }

            float decimalScale = 0.1f;
            for (; i < end; i++)
            {
                byte c = bytes[i];
                if (c < (byte)'0' || c > (byte)'9')
                    return false;

                result += (c - (byte)'0') * decimalScale;
                decimalScale *= 0.1f;
                any = true;
            }

            if (!any)
                return false;

            value = negative ? -result : result;
            return math.isfinite(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 SanitizeUv01(float2 value)
        {
            return math.all(math.isfinite(value)) ? math.saturate(value) : new float2(0.055f, 0.63f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeScale(float value)
        {
            return math.isfinite(value) ? math.clamp(value, 0.025f, 0.25f) : 0.075f;
        }

        private int FindTerminalIndex(uint hash)
        {
            if (!TryResolveBuffer(ref _terminalStatesHandle, out NativeArray<TerminalStateDTO> terminalStates))
                return -1;

            for (int i = 0; i < _terminalCount; i++)
            {
                if (terminalStates[i].TerminalHash == hash)
                    return i;
            }

            return -1;
        }

        private void UpdatePanelInstancesIfNeeded()
        {
            if (!drawPanelsInstanced ||
                !TryResolveBuffer(ref _panelInstancesHandle, out NativeArray<TerminalPanelInstanceDTO> panelInstances))
                return;

            bool changed = _panelInstanceUploadDirty;
            int count = math.min(_terminalCount, ResolveBoundPanelCount());
            for (int i = 0; i < count; i++)
            {
                Transform terminal = ResolveTerminalTransform(i);
                if (terminal == null)
                    continue;

                float4x4 matrix = ToFloat4x4(terminal.localToWorldMatrix);
                if (!MatrixFinite(matrix))
                {
                    _lastFaultFlags |= FaultNonFinite;
                    continue;
                }

                if (MatrixEquals(panelInstances[i].LocalToWorld, matrix))
                    continue;

                panelInstances[i] = new TerminalPanelInstanceDTO
                {
                    LocalToWorld = matrix,
                    SliceFlags = new float4(i, _lowTier ? 1f : 0f, 0f, 0f)
                };
                changed = true;
            }

            if (!changed)
                return;

            RecalculatePanelRenderBounds();
            UploadPanelInstances();
        }

        private void RenderInstancedPanels()
        {
            if (!drawPanelsInstanced ||
                terminalArrayMaterial == null ||
                terminalPanelMesh == null ||
                _panelInstanceBuffer == null ||
                _terminalTextureArray == null ||
                _terminalCount <= 0)
            {
                return;
            }

            RenderParams renderParams = new RenderParams(terminalArrayMaterial)
            {
                worldBounds = _panelRenderBounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = gameObject.layer
            };
            UnityEngine.Graphics.RenderMeshPrimitives(renderParams, terminalPanelMesh, 0, _terminalCount);
        }

        private void RecalculatePanelRenderBounds()
        {
            if (!TryResolveBuffer(ref _panelInstancesHandle, out NativeArray<TerminalPanelInstanceDTO> panelInstances) ||
                _terminalCount <= 0)
            {
                _panelRenderBounds = new Bounds(transform.position, Vector3.one);
                return;
            }

            float3 minBounds = new float3(float.MaxValue);
            float3 maxBounds = new float3(float.MinValue);
            int validCount = 0;
            for (int i = 0; i < _terminalCount; i++)
            {
                float3 center = panelInstances[i].LocalToWorld.c3.xyz;
                if (!math.all(math.isfinite(center)))
                    continue;

                minBounds = math.min(minBounds, center);
                maxBounds = math.max(maxBounds, center);
                validCount++;
            }

            if (validCount == 0)
            {
                _lastFaultFlags |= FaultNonFinite;
                _panelRenderBounds = new Bounds(transform.position, Vector3.one);
                return;
            }

            float3 size = math.max(maxBounds - minBounds, new float3(1f, 1f, 1f)) + new float3(2f, 2f, 2f);
            float3 centerBounds = (minBounds + maxBounds) * 0.5f;
            _panelRenderBounds = new Bounds(
                new Vector3(centerBounds.x, centerBounds.y, centerBounds.z),
                new Vector3(size.x, size.y, size.z));
        }

        private static float4x4 ToFloat4x4(Matrix4x4 matrix)
        {
            return new float4x4(
                new float4(matrix.m00, matrix.m10, matrix.m20, matrix.m30),
                new float4(matrix.m01, matrix.m11, matrix.m21, matrix.m31),
                new float4(matrix.m02, matrix.m12, matrix.m22, matrix.m32),
                new float4(matrix.m03, matrix.m13, matrix.m23, matrix.m33));
        }

        private static bool MatrixEquals(in float4x4 lhs, in float4x4 rhs)
        {
            return math.all(lhs.c0 == rhs.c0) &&
                   math.all(lhs.c1 == rhs.c1) &&
                   math.all(lhs.c2 == rhs.c2) &&
                   math.all(lhs.c3 == rhs.c3);
        }

        private static bool MatrixFinite(in float4x4 matrix)
        {
            return math.all(math.isfinite(matrix.c0)) &&
                   math.all(math.isfinite(matrix.c1)) &&
                   math.all(math.isfinite(matrix.c2)) &&
                   math.all(math.isfinite(matrix.c3));
        }

        private void BindTerminalRenderers()
        {
            if (_terminalTextureArray == null)
                return;

            Shader.SetGlobalTexture(TerminalTextureArrayId, _terminalTextureArray);
            if (terminalArrayMaterial != null)
            {
                terminalArrayMaterial.SetTexture(TerminalTextureArrayId, _terminalTextureArray);
                terminalArrayMaterial.SetBuffer(TerminalPanelInstancesId, _panelInstanceBuffer);
                if (drawPanelsInstanced)
                    terminalArrayMaterial.EnableKeyword(TerminalInstancedKeyword);
            }

            _bindingsDirty = false;
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registeredLateFrame = false;
        }

        private void RecordTelemetry(int frame, int dirtyCount, int dispatchedCount, uint faultFlags)
        {
            if (!TryResolveBuffer(ref _telemetryRingHandle, out NativeArray<TerminalTelemetryEntry> telemetryRing))
                return;

            TerminalTelemetryEntry entry = new TerminalTelemetryEntry
            {
                Frame = frame,
                TerminalCount = _terminalCount,
                DirtyCount = dirtyCount,
                DispatchedCount = dispatchedCount,
                FormatMainThreadMilliseconds = _lastFormatMainThreadMilliseconds,
                UploadMicroseconds = _lastUploadMicroseconds,
                DispatchMicroseconds = _lastDispatchMicroseconds,
                FaultFlags = faultFlags,
                LayoutHash = ComputeLayoutHash(),
                LastPower01 = _lastPower01,
                LastDamage01 = _lastDamage01
            };
            telemetryRing[_telemetryCursor] = entry;
            _telemetryCursor = (_telemetryCursor + 1) % TerminalOsConstants.BlackBoxFrameCount;
        }

        private uint ComputeLayoutHash()
        {
            if (!TryResolveBuffer(ref _screenCommandsHandle, out NativeArray<ScreenCommandDTO> screenCommands))
                return 0u;

            uint hash = 2166136261u;
            for (int i = 0; i < _terminalCount; i++)
            {
                ScreenCommandDTO command = screenCommands[i];
                hash = (hash ^ math.asuint(command.Position.x)) * 16777619u;
                hash = (hash ^ math.asuint(command.Position.y)) * 16777619u;
                hash = (hash ^ math.asuint(command.Scale)) * 16777619u;
            }

            return hash;
        }

        private void TryDumpBlackBox(uint faultFlags)
        {
            if (_blackBoxDumped ||
                !TryResolveBuffer(ref _telemetryRingHandle, out NativeArray<TerminalTelemetryEntry> telemetryRing) ||
                string.IsNullOrEmpty(_dumpFullPath))
                return;

            _blackBoxDumped = true;
            try
            {
                WriteBlackBoxDump(_dumpFullPath, faultFlags, telemetryRing);
                if (!string.IsNullOrEmpty(_dumpMirrorFullPath))
                    WriteBlackBoxDump(_dumpMirrorFullPath, faultFlags, telemetryRing);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void WriteBlackBoxDump(string path, uint faultFlags, NativeArray<TerminalTelemetryEntry> telemetryRing)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(0x544F5348u); // HSOT
                writer.Write(1u);
                writer.Write(faultFlags);
                writer.Write(telemetryRing.Length);
                writer.Write(_telemetryCursor);
                for (int i = 0; i < telemetryRing.Length; i++)
                {
                    TerminalTelemetryEntry entry = telemetryRing[i];
                    writer.Write(entry.Frame);
                    writer.Write(entry.TerminalCount);
                    writer.Write(entry.DirtyCount);
                    writer.Write(entry.DispatchedCount);
                    writer.Write(entry.FormatMainThreadMilliseconds);
                    writer.Write(entry.UploadMicroseconds);
                    writer.Write(entry.DispatchMicroseconds);
                    writer.Write(entry.FaultFlags);
                    writer.Write(entry.LayoutHash);
                    writer.Write(entry.Reserved0);
                    writer.Write(entry.LastPower01);
                    writer.Write(entry.LastDamage01);
                }
            }
        }

        private void CompleteJobsForTeardown()
        {
            if (_formatScheduled)
            {
                ForceCompleteJob(ref _formatHandle);
                _formatScheduled = false;
            }

            if (_clickResolveScheduled)
            {
                ForceCompleteJob(ref _clickResolveHandle);
                _clickResolveScheduled = false;
            }
        }

        private static bool TryFinalizeCompletedJob(ref JobHandle handle)
        {
            if (!handle.IsCompleted)
                return false;

            handle.Complete();
            handle = default;
            return true;
        }

        private static void ForceCompleteJob(ref JobHandle handle)
        {
            handle.Complete();
            handle = default;
        }

        private void DisposeGraphicsResources()
        {
            ReleaseBuffer(ref _stateBuffers[0]);
            ReleaseBuffer(ref _stateBuffers[1]);
            ReleaseBuffer(ref _screenCommandBuffer);
            ReleaseBuffer(ref _glyphUvBuffer);
            ReleaseBuffer(ref _dirtyIndexBuffer);
            ReleaseBuffer(ref _panelInstanceBuffer);
            ReleaseRenderTexture();
            _graphicsResourcesReady = false;
            _blitKernel = -1;
        }

        private void ReleaseRenderTexture()
        {
            if (_terminalTextureArray == null)
                return;

            _terminalTextureArray.Release();
            Destroy(_terminalTextureArray);
            _terminalTextureArray = null;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private void DisposeNativeResources()
        {
            ClearVaultHandles();
            _nativeResourcesReady = false;
            _vault = null;
            _terminalCount = 0;
            _buttonCount = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref TerminalStateDTO GetTerminalStateRefUnchecked(int index)
        {
            void* basePtr = ResolveTerminalStatePointer();
            if (basePtr == null)
                FatalMemoryException.ThrowStaleVaultHandle();

            return ref UnsafeUtility.AsRef<TerminalStateDTO>((byte*)basePtr + index * UnsafeUtility.SizeOf<TerminalStateDTO>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TerminalStateDTO* ResolveTerminalStatePointer()
        {
            if (_vault != null && _terminalStatesHandle.IsCreated)
                return (TerminalStateDTO*)_terminalStatesHandle.ResolvePointer(_vault);

            return null;
        }

        private void ClearVaultHandles()
        {
            _terminalStatesHandle = default;
            _screenCommandsHandle = default;
            _glyphUvsHandle = default;
            _terminalPositionsHandle = default;
            _terminalForwardHandle = default;
            _dirtyIndicesHandle = default;
            _telemetryRingHandle = default;
            _mockPowerSignalHandle = default;
            _mockDamageSignalHandle = default;
            _mockPowerStatusSignalHandle = default;
            _virtualButtonsHandle = default;
            _panelInstancesHandle = default;
            _clickScratchHandle = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ElapsedMilliseconds(long startTicks)
        {
            return (float)((Stopwatch.GetTimestamp() - startTicks) * 1000.0 / Stopwatch.Frequency);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ElapsedMicroseconds(long startTicks)
        {
            return (float)((Stopwatch.GetTimestamp() - startTicks) * 1000000.0 / Stopwatch.Frequency);
        }
    }
}
