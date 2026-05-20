using System;
using System.IO;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
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
    public unsafe sealed class TerminalOsRuntime : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener, IScalabilityChangedEventListener
    {
        private const int ActiveRuntimeCapacity = 4;
        private const int HighResolution = 512;
        private const int LowResolution = 256;
        private const float AttentionCullDistanceMeters = 20f;
        private const float AttentionCullDistanceSq = AttentionCullDistanceMeters * AttentionCullDistanceMeters;
        private const uint FaultLayoutMismatch = 1u << 0;
        private const uint FaultFormatBudget = 1u << 1;
        private const uint FaultNonFinite = 1u << 2;
        private const uint FaultVaultUnavailable = 1u << 3;
        private const string NativeOwner = nameof(TerminalOsRuntime);
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_137.bin";
        private const string DumpMirrorRelativePath = "Docs/AgentLogs/Dump_SHINOBU_137.h8dump";
        private const BufferID TerminalStatesBufferId = (BufferID)71360;
        private const BufferID ScreenCommandsBufferId = (BufferID)71361;
        private const BufferID GlyphUvsBufferId = (BufferID)71362;
        private const BufferID TerminalPositionsBufferId = (BufferID)71363;
        private const BufferID TerminalForwardsBufferId = (BufferID)71364;
        private const BufferID DirtyIndicesBufferId = (BufferID)71365;
        private const BufferID TelemetryRingBufferId = (BufferID)71366;
        private const BufferID MockPowerBufferId = (BufferID)71367;
        private const BufferID MockDamageBufferId = (BufferID)71368;
        private const BufferID MockPowerStatusBufferId = (BufferID)71369;
        private const BufferID ButtonAabbBufferId = (BufferID)71370;
        private const BufferID PanelInstancesBufferId = (BufferID)71371;
        private const BufferID TerminalClickScratchBufferId = (BufferID)71372;
        private const BufferID TerminalPlanesBufferId = (BufferID)71373;
        private const BufferID GazeRayBufferId = (BufferID)71374;
        private const BufferID TerminalInteractionsBufferId = (BufferID)71375;
        private const uint TerminalClickLaneHash = 0x54434C4Bu; // TCLK
        private const uint TerminalCommandLaneHash = 0x54434D44u; // TCMD
        private const string TerminalInstancedKeyword = "HECTON_TERMINAL_INSTANCED";

        private static TerminalOsRuntime s_activeRuntime0;
        private static TerminalOsRuntime s_activeRuntime1;
        private static TerminalOsRuntime s_activeRuntime2;
        private static TerminalOsRuntime s_activeRuntime3;
        private static int s_activeRuntimeCount;
        private static TerminalStateDTO s_invalidTerminalStateRef;
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
        private static readonly int HectonDiegeticGlitchQualityWeightId = Shader.PropertyToID("_HectonDiegeticGlitchQualityWeight");

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

        [Header("Interaction Solver")]
        [SerializeField, Range(0.5f, 30f)] private float interactionMaxDistanceMeters = 10f;
        [SerializeField, Range(-0.5f, 0.95f)] private float interactionViewConeCos = -0.05f;
        [SerializeField, Range(0f, 1f)] private float hologramDistortionIntensity = 0.35f;
        [SerializeField, Range(0f, 1f)] private float minimumQualityWeight;
        [SerializeField] private bool drawTerminalDebugGizmos;

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
        private VaultBufferHandle<ButtonAABBDTO> _buttonAabbHandle;
        private VaultBufferHandle<TerminalPanelInstanceDTO> _panelInstancesHandle;
        private VaultBufferHandle<TerminalClickSignal> _clickScratchHandle;
        private VaultBufferHandle<TerminalPlaneDTO> _terminalPlanesHandle;
        private VaultBufferHandle<GazeRayDTO> _gazeRayHandle;
        private VaultBufferHandle<TerminalInteractionDTO> _terminalInteractionsHandle;

        private GraphicsBuffer _stateBuffer0;
        private GraphicsBuffer _stateBuffer1;
        private GraphicsBuffer _screenCommandBuffer;
        private GraphicsBuffer _glyphUvBuffer;
        private GraphicsBuffer _dirtyIndexBuffer;
        private GraphicsBuffer _panelInstanceBuffer;
        private RenderTexture _terminalTextureArray;
        private Camera _attentionCameraCache;
        private Bounds _panelRenderBounds;

        private JobHandle _formatHandle;
        private JobHandle _clickResolveHandle;
        private JobHandle _terminalInteractionHandle;
        private bool _formatScheduled;
        private bool _clickResolveScheduled;
        private bool _terminalInteractionScheduled;
        private bool _registeredLateFrame;
        private bool _nativeResourcesReady;
        private bool _graphicsResourcesReady;
        private bool _layoutUploadDirty;
        private bool _glyphUploadDirty;
        private bool _bindingsDirty;
        private bool _panelInstanceUploadDirty;
        private bool _blackBoxDumped;
        private bool _inputPressedLastFrame;
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
        private int _nextQualityRefreshFrame;
        private int _nextCameraResolveFrame;
        private int _lastDirtyCount;
        private int _lastDispatchedCount;
        private int _framesBetweenUpdates = 1;
        private int _lastEvaluatedTerminalCount;
        private uint _lastHoveredTerminalHash;
        private uint _lastFaultFlags;
        private long _interactionScheduleTicks;
        private float _lastFormatMainThreadMilliseconds;
        private float _lastUploadMicroseconds;
        private float _lastDispatchMicroseconds;
        private float _lastIntersectionMicroseconds;
        private float _lastPower01;
        private float _lastDamage01;
        private float _lastDiegeticGlitchIntensity;
        private float _lastPanelInstanceQualityWeight = -1f;
        private float _lastPanelInstanceGlitchIntensity = -1f;
        private float _globalQualityWeight = 1f;
        private IInputService _input;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private bool _registeredHotSwapListener;
        private bool _registeredScalabilityListener;
        private string _csvFullPath;
        private string _dumpFullPath;
        private string _dumpMirrorFullPath;
        private DateTime _csvLastWriteUtc;

        public void LateFrameTick()
        {
            EnsureRuntimeReady();
            if (!_nativeResourcesReady)
                return;

            int frame = Time.frameCount;
            RefreshScalabilityPolicy();
            TryFinalizeClickResolveJob();
            TryFinalizeTerminalInteractionJob();

            bool visualPipelineBlocked = false;
            if (_formatScheduled)
            {
                if (!TryFinalizeCompletedJob(ref _formatHandle))
                {
                    visualPipelineBlocked = true;
                }
                else
                {
                    _formatScheduled = false;
                }
            }

            TryMonitorLayoutCsv(frame);

            int dirtyCount = 0;
            int dispatchedCount = 0;
            if (!visualPipelineBlocked)
            {
                dirtyCount = BuildDirtyList();
                if (dirtyCount > 0)
                {
                    bool uploaded = UploadDirtyPayloads(dirtyCount);
                    if (uploaded)
                        dispatchedCount = DispatchDirtyScreens(dirtyCount);
                    if (dispatchedCount == dirtyCount)
                        ClearDirtyFlags(dirtyCount);
                }
            }

            _lastDirtyCount = dirtyCount;
            _lastDispatchedCount = dispatchedCount;
            if (!visualPipelineBlocked)
                UpdatePanelInstancesIfNeeded();
            TryScheduleTerminalInteractionPipeline(frame, !visualPipelineBlocked);
            TryScheduleClickResolveJob();
            if (!visualPipelineBlocked)
                TryScheduleFormatJob(frame);
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

        public int GetFramesBetweenUpdates()
        {
            return _framesBetweenUpdates;
        }

        public int GetLastEvaluatedTerminalCount()
        {
            return _lastEvaluatedTerminalCount;
        }

        public uint GetLastHoveredTerminalHash()
        {
            return _lastHoveredTerminalHash;
        }

        public float GetGlobalQualityWeight()
        {
            return _globalQualityWeight;
        }

        public float GetLastIntersectionMicroseconds()
        {
            return _lastIntersectionMicroseconds;
        }

        public bool TryGetTerminalInteractionCopy(int index, out TerminalInteractionDTO interaction)
        {
            if (!TryResolveBuffer(ref _terminalInteractionsHandle, out NativeArray<TerminalInteractionDTO> interactions) ||
                index < 0 ||
                index >= _terminalCount)
            {
                interaction = default;
                return false;
            }

            interaction = interactions[index];
            return true;
        }

        public void ApplyEditorTuning(float maxDistanceMeters, float viewConeCos, float distortionIntensity, float minQuality)
        {
            interactionMaxDistanceMeters = math.clamp(math.isfinite(maxDistanceMeters) ? maxDistanceMeters : 10f, 0.5f, 30f);
            interactionViewConeCos = math.clamp(math.isfinite(viewConeCos) ? viewConeCos : -0.05f, -0.5f, 0.95f);
            hologramDistortionIntensity = math.saturate(math.isfinite(distortionIntensity) ? distortionIntensity : 0.35f);
            minimumQualityWeight = math.saturate(math.isfinite(minQuality) ? minQuality : 0f);
            _nextQualityRefreshFrame = 0;
            RefreshScalabilityPolicy();
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
            {
                _lastFaultFlags |= FaultVaultUnavailable;
                s_invalidTerminalStateRef = default;
                return ref s_invalidTerminalStateRef;
            }

            return ref GetTerminalStateRefUnchecked(index);
        }

        public bool TrySetTerminalMockState(int index, float value1, float value2)
        {
            if (!TryResolveBuffer(ref _terminalStatesHandle, out NativeArray<TerminalStateDTO> terminalStates) ||
                index < 0 ||
                index >= _terminalCount)
            {
                return false;
            }

            TerminalStateDTO state = terminalStates[index];
            state.Value1 = math.saturate(math.isfinite(value1) ? value1 : 0f);
            state.Value2 = math.saturate(math.isfinite(value2) ? value2 : 0f);
            state.IsDirty = 1;
            terminalStates[index] = state;
            ForceDirty(index);
            return true;
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

        public void SetTerminalAvailability(int index, float power01, float submerged01)
        {
            if (!TryResolveBuffer(ref _terminalPlanesHandle, out NativeArray<TerminalPlaneDTO> terminalPlanes) ||
                index < 0 ||
                index >= _terminalCount)
                return;

            TerminalPlaneDTO plane = terminalPlanes[index];
            float safePower = math.saturate(math.isfinite(power01) ? power01 : 0f);
            float safeSubmerged = math.saturate(math.isfinite(submerged01) ? submerged01 : 0f);
            uint flags = plane.Flags | TerminalOsConstants.PlaneFlagActive;
            if (safePower > 0.001f)
                flags |= TerminalOsConstants.PlaneFlagPowered;
            else
                flags &= ~TerminalOsConstants.PlaneFlagPowered;

            if (safeSubmerged > 0.5f)
                flags |= TerminalOsConstants.PlaneFlagSubmerged;
            else
                flags &= ~TerminalOsConstants.PlaneFlagSubmerged;

            plane.Power01 = safePower;
            plane.Submerged01 = safeSubmerged;
            plane.Flags = flags;
            terminalPlanes[index] = plane;
            if (TryResolveBuffer(ref _terminalStatesHandle, out NativeArray<TerminalStateDTO> terminalStates) &&
                index < terminalStates.Length)
            {
                TerminalStateDTO state = terminalStates[index];
                state.Value1 = safePower;
                state.BackgroundColor = safePower > 0.001f && safeSubmerged <= 0.5f
                    ? (state.BackgroundColor == 0u ? 0x00061418u : state.BackgroundColor)
                    : 0u;
                state.IsDirty = 1;
                terminalStates[index] = state;
            }
            ForceDirty(index);
        }

        internal bool TryWritePowerLevelTokenLine(
            ReadOnlySpan<byte> templateBytes,
            int terminalIndex,
            out CharBufferPool.Lease lease,
            out int length)
        {
            lease = default;
            length = 0;
            if (!TryGetTerminalStateCopy(terminalIndex, out TerminalStateDTO state) ||
                !CharBufferPool.TryAcquire(out lease))
            {
                return false;
            }

            Span<char> destination = lease.Buffer.AsSpan();
            int powerPercent = math.clamp((int)math.round(math.saturate(state.Value1) * 100f), 0, 100);
            for (int i = 0; i < templateBytes.Length && length < destination.Length; i++)
            {
                if (MatchesPowerLevelToken(templateBytes, i))
                {
                    if (!powerPercent.TryFormat(destination.Slice(length), out int written))
                    {
                        CharBufferPool.Release(in lease);
                        lease = default;
                        length = 0;
                        return false;
                    }

                    length += written;
                    if (length < destination.Length)
                        destination[length++] = '%';
                    i += 12;
                    continue;
                }

                byte value = templateBytes[i];
                if (value == 0)
                    break;
                if (value == (byte)'\r')
                    continue;
                destination[length++] = value < 128 ? (char)value : '?';
            }

            return true;
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
                TerminalOsRuntime runtime = GetActiveRuntimeSlot(i);
                if (runtime != null && runtime.isActiveAndEnabled)
                    runtime.ApplyDiegeticGlitchIntensity(safeIntensity);
            }
        }

        private void ApplyDiegeticGlitchIntensity(float intensity01)
        {
            if (_vault == null || !_terminalStatesHandle.IsCreated)
            {
                _lastDiegeticGlitchIntensity = intensity01;
                _panelInstanceUploadDirty = true;
                _bindingsDirty = true;
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
            _panelInstanceUploadDirty = true;
            _bindingsDirty = true;
        }

        private void RegisterActiveRuntime()
        {
            for (int i = 0; i < s_activeRuntimeCount; i++)
            {
                if (ReferenceEquals(GetActiveRuntimeSlot(i), this))
                    return;
            }

            if (s_activeRuntimeCount >= ActiveRuntimeCapacity)
                return;

            SetActiveRuntimeSlot(s_activeRuntimeCount++, this);
        }

        private void UnregisterActiveRuntime()
        {
            for (int i = 0; i < s_activeRuntimeCount; i++)
            {
                if (!ReferenceEquals(GetActiveRuntimeSlot(i), this))
                    continue;

                int last = s_activeRuntimeCount - 1;
                SetActiveRuntimeSlot(i, GetActiveRuntimeSlot(last));
                SetActiveRuntimeSlot(last, null);
                s_activeRuntimeCount = math.max(0, last);
                return;
            }
        }

        private static TerminalOsRuntime GetActiveRuntimeSlot(int index)
        {
            switch (index)
            {
                case 0: return s_activeRuntime0;
                case 1: return s_activeRuntime1;
                case 2: return s_activeRuntime2;
                case 3: return s_activeRuntime3;
                default: return null;
            }
        }

        private static void SetActiveRuntimeSlot(int index, TerminalOsRuntime runtime)
        {
            switch (index)
            {
                case 0:
                    s_activeRuntime0 = runtime;
                    break;
                case 1:
                    s_activeRuntime1 = runtime;
                    break;
                case 2:
                    s_activeRuntime2 = runtime;
                    break;
                case 3:
                    s_activeRuntime3 = runtime;
                    break;
            }
        }

        private void Awake()
        {
            EnsureColdPaths();
            CacheRegistryServicesCold();
            ValidateLayouts();
            EnsureRuntimeReady();
        }

        private void OnEnable()
        {
            EnsureColdPaths();
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegisterScalabilityListener();
            EnsureRuntimeReady();
            RegisterActiveRuntime();
            TryRegisterLateFrame();
        }

        private void OnDisable()
        {
            UnregisterActiveRuntime();
            TryUnregisterLateFrame();
            TryUnregisterScalabilityListener();
            TryUnregisterHotSwapListener();
            CompleteJobsForTeardown();
            DisposeGraphicsResources();
            DisposeNativeResources();
        }

        private void OnDestroy()
        {
            UnregisterActiveRuntime();
            TryUnregisterLateFrame();
            TryUnregisterScalabilityListener();
            TryUnregisterHotSwapListener();
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

        private void CacheRegistryServicesCold()
        {
            _input = GlobalRegistry.Input;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            _cachedPlayerContext = playerContext;

            if (attentionCameraOverride == null && playerContext != null && playerContext.PlayerCamera != null)
            {
                _attentionCameraCache = playerContext.PlayerCamera;
            }
        }

        private void ValidateLayouts()
        {
            _lastFaultFlags &= ~FaultLayoutMismatch;
            if (UnsafeUtility.SizeOf<TerminalStateDTO>() != TerminalOsConstants.TerminalStateStrideBytes ||
                UnsafeUtility.SizeOf<ScreenCommandDTO>() != TerminalOsConstants.ScreenCommandStrideBytes ||
                UnsafeUtility.SizeOf<TerminalInteractionDTO>() != TerminalOsConstants.TerminalInteractionStrideBytes ||
                UnsafeUtility.SizeOf<TerminalPlaneDTO>() != TerminalOsConstants.TerminalPlaneStrideBytes ||
                UnsafeUtility.SizeOf<GazeRayDTO>() != TerminalOsConstants.GazeRayStrideBytes ||
                UnsafeUtility.SizeOf<ButtonAABBDTO>() != TerminalOsConstants.ButtonAabbStrideBytes ||
                UnsafeUtility.SizeOf<TerminalPanelInstanceDTO>() != 80 ||
                UnsafeUtility.SizeOf<TerminalTelemetryEntry>() != 64 ||
                !TerminalOsSelfAudit.ValidateLayoutAndRayPlaneMath())
            {
                _lastFaultFlags |= FaultLayoutMismatch;
            }
        }

        private void RefreshScalabilityPolicy()
        {
            int frame = Time.frameCount;
            float quality = math.max(ResolveGlobalQualityWeight01(), math.saturate(minimumQualityWeight));
            float previousQuality = _globalQualityWeight;
            _globalQualityWeight = quality;
            _framesBetweenUpdates = math.clamp((int)math.round(math.lerp(1f, 15f, 1f - quality)), 1, 15);
            if (math.abs(previousQuality - quality) > 0.0005f)
            {
                _panelInstanceUploadDirty = true;
                _bindingsDirty = true;
            }

            if (_textureResolution > 0 && frame < _nextQualityRefreshFrame)
                return;

            _nextQualityRefreshFrame = frame + math.clamp((int)math.round(math.lerp(30f, 120f, 1f - quality)), 30, 120);
            float resolutionCurve = Smooth01(quality);
            int targetResolution = AlignResolution((int)math.round(math.lerp(LowResolution, HighResolution, resolutionCurve)));
            bool resolutionChanged = _textureResolution != targetResolution;
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

        private float ResolveGlobalQualityWeight01()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            if (math.isfinite(weight))
                return math.saturate(weight);

            return math.saturate(_globalQualityWeight);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Input:
                    _input = currentService as IInputService;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                    if (attentionCameraOverride == null)
                    {
                        _attentionCameraCache = _cachedPlayerContext != null
                            ? _cachedPlayerContext.PlayerCamera
                            : null;
                        _nextCameraResolveFrame = 0;
                    }
                    break;
            }
        }

        public void OnScalabilityChanged(in ScalabilityChangedEvent payload)
        {
            _nextQualityRefreshFrame = 0;
            RefreshScalabilityPolicy();
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

        private void TryRegisterScalabilityListener()
        {
            if (_registeredScalabilityListener || !Application.isPlaying)
                return;

            ScalabilityEvents.Register(this);
            _registeredScalabilityListener = true;
        }

        private void TryUnregisterScalabilityListener()
        {
            if (!_registeredScalabilityListener)
                return;

            ScalabilityEvents.Unregister(this);
            _registeredScalabilityListener = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int AlignResolution(int value)
        {
            int clamped = math.clamp(value, LowResolution, HighResolution);
            return (clamped + 7) & ~7;
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
            ResolveNativeBuffer(vault, ButtonAabbBufferId, TerminalOsConstants.ButtonAabbCapacity, NativeArrayOptions.UninitializedMemory, out _buttonAabbHandle);
            ResolveNativeBuffer(vault, PanelInstancesBufferId, _terminalCount, NativeArrayOptions.UninitializedMemory, out _panelInstancesHandle);
            ResolveNativeBuffer(vault, TerminalClickScratchBufferId, TerminalOsConstants.MaxQueuedClicks, NativeArrayOptions.UninitializedMemory, out _clickScratchHandle);
            ResolveNativeBuffer(vault, TerminalPlanesBufferId, _terminalCount, NativeArrayOptions.UninitializedMemory, out _terminalPlanesHandle);
            ResolveNativeBuffer(vault, GazeRayBufferId, 1, NativeArrayOptions.UninitializedMemory, out _gazeRayHandle);
            ResolveNativeBuffer(vault, TerminalInteractionsBufferId, _terminalCount, NativeArrayOptions.UninitializedMemory, out _terminalInteractionsHandle);
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
            SignalBus<InteractionUiSignal>.EnsureInitialized();
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
                   TryResolveBuffer(ref _buttonAabbHandle, out NativeArray<ButtonAABBDTO> _) &&
                   TryResolveBuffer(ref _panelInstancesHandle, out NativeArray<TerminalPanelInstanceDTO> _) &&
                   TryResolveBuffer(ref _clickScratchHandle, out NativeArray<TerminalClickSignal> _) &&
                   TryResolveBuffer(ref _terminalPlanesHandle, out NativeArray<TerminalPlaneDTO> _) &&
                   TryResolveBuffer(ref _gazeRayHandle, out NativeArray<GazeRayDTO> _) &&
                   TryResolveBuffer(ref _terminalInteractionsHandle, out NativeArray<TerminalInteractionDTO> _);
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
                !TryResolveBuffer(ref _buttonAabbHandle, out NativeArray<ButtonAABBDTO> buttonAabbs) ||
                !TryResolveBuffer(ref _terminalPlanesHandle, out NativeArray<TerminalPlaneDTO> terminalPlanes) ||
                !TryResolveBuffer(ref _gazeRayHandle, out NativeArray<GazeRayDTO> gazeRays) ||
                !TryResolveBuffer(ref _terminalInteractionsHandle, out NativeArray<TerminalInteractionDTO> interactions))
            {
                _lastFaultFlags |= FaultVaultUnavailable;
                return;
            }

            _buttonCount = 0;
            gazeRays[0] = default;
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
                float4x4 matrix = float4x4.TRS(terminalPositions[i].xyz, quaternion.identity, new float3(1.25f, 0.72f, 1f));
                panelInstances[i] = new TerminalPanelInstanceDTO
                {
                    LocalToWorld = matrix,
                    SliceFlags = new float4(i, 0f, 0f, 0f)
                };
                uint firstButton = (uint)_buttonCount;
                AddButtonAabb(buttonAabbs, terminalHash, TerminalOsConstants.CommandOpenDoor, new float4(0.08f, 0.08f, 0.34f, 0.18f));
                AddButtonAabb(buttonAabbs, terminalHash, TerminalOsConstants.CommandAcknowledge, new float4(0.66f, 0.08f, 0.92f, 0.18f));
                terminalPlanes[i] = BuildTerminalPlane(matrix, terminalHash, firstButton, 2u, 1f, 0f);
                interactions[i] = default;
            }

            RecalculatePanelRenderBounds();
        }

        private void AddButtonAabb(NativeArray<ButtonAABBDTO> buttons, uint terminalHash, uint commandHash, float4 rectUv)
        {
            if (_buttonCount >= buttons.Length)
                return;

            buttons[_buttonCount++] = new ButtonAABBDTO
            {
                RectUv = rectUv,
                TerminalHash = terminalHash,
                CommandHash = commandHash,
                Flags = TerminalOsConstants.ButtonFlagEnabled
            };
        }

        private static TerminalPlaneDTO BuildTerminalPlane(
            in float4x4 matrix,
            uint terminalHash,
            uint firstButton,
            uint buttonCount,
            float power01,
            float submerged01)
        {
            float3 rightAxis = matrix.c0.xyz;
            float3 upAxis = matrix.c1.xyz;
            float3 normalAxis = -matrix.c2.xyz;
            float width = math.max(0.001f, math.length(rightAxis));
            float height = math.max(0.001f, math.length(upAxis));
            float safePower = math.saturate(math.isfinite(power01) ? power01 : 0f);
            float safeSubmerged = math.saturate(math.isfinite(submerged01) ? submerged01 : 0f);
            uint flags = TerminalOsConstants.PlaneFlagActive;
            flags |= safePower > 0.001f ? TerminalOsConstants.PlaneFlagPowered : 0u;
            flags |= safeSubmerged > 0.5f ? TerminalOsConstants.PlaneFlagSubmerged : 0u;

            return new TerminalPlaneDTO
            {
                CenterAup = AbsoluteUniversePosition.FromRuntimePosition(ToVector3(matrix.c3.xyz)),
                Normal = math.normalizesafe(normalAxis, new float3(0f, 0f, -1f)),
                Up = math.normalizesafe(upAxis, new float3(0f, 1f, 0f)),
                Right = math.normalizesafe(rightAxis, new float3(1f, 0f, 0f)),
                Width = width,
                Height = height,
                TerminalHash = terminalHash,
                Flags = flags,
                LayoutFirstButton = firstButton,
                LayoutButtonCount = buttonCount,
                Power01 = safePower,
                Submerged01 = safeSubmerged
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
            if (_stateBuffer0 == null)
                _stateBuffer0 = CreateStructuredLockBuffer<TerminalStateDTO>(_terminalCount);
            if (_stateBuffer1 == null)
                _stateBuffer1 = CreateStructuredLockBuffer<TerminalStateDTO>(_terminalCount);
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
                                      _stateBuffer0 != null &&
                                      _stateBuffer1 != null &&
                                      _screenCommandBuffer != null &&
                                      _glyphUvBuffer != null &&
                                      _dirtyIndexBuffer != null &&
                                      _panelInstanceBuffer != null;
        }

        private void EnsureTextureArray()
        {
            int resolution = _textureResolution > 0
                ? _textureResolution
                : AlignResolution((int)math.round(math.lerp(LowResolution, HighResolution, Smooth01(_globalQualityWeight))));
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
                name = "H8_TerminalOS_Array",
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private GraphicsBuffer ResolveStateBuffer(int index)
        {
            return index == 0 ? _stateBuffer0 : _stateBuffer1;
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
            Camera camera = ResolveAttentionCamera(Time.frameCount);

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

        private Camera ResolveAttentionCamera(int frame)
        {
            if (attentionCameraOverride != null)
            {
                _attentionCameraCache = attentionCameraOverride;
                return attentionCameraOverride;
            }

            if (_attentionCameraCache != null)
                return _attentionCameraCache;

            if (frame < _nextCameraResolveFrame)
                return null;

            _nextCameraResolveFrame = frame + math.clamp((int)math.round(math.lerp(15f, 60f, 1f - _globalQualityWeight)), 15, 60);
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null && playerContext.PlayerCamera != null)
                _attentionCameraCache = playerContext.PlayerCamera;

            return _attentionCameraCache;
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

        private bool UploadDirtyPayloads(int dirtyCount)
        {
            if (!_graphicsResourcesReady || dirtyCount <= 0)
                return false;

            long start = Stopwatch.GetTimestamp();
            bool uploaded = UploadDirtyIndices(dirtyCount) &&
                UploadDirtyStates(dirtyCount, ResolveStateBuffer(_writeBufferIndex));
            _lastUploadMicroseconds = ElapsedMicroseconds(start);
            return uploaded;
        }

        private bool UploadDirtyIndices(int dirtyCount)
        {
            if (_dirtyIndexBuffer == null ||
                !TryResolveBuffer(ref _dirtyIndicesHandle, out NativeArray<int> dirtyIndices))
                return false;

            bool copied = false;
            NativeArray<int> mapped = _dirtyIndexBuffer.LockBufferForWrite<int>(0, dirtyCount);
            try
            {
                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(dirtyIndices);
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                long copyBytes = (long)UnsafeUtility.SizeOf<int>() * dirtyCount;
                long destinationBytes = (long)UnsafeUtility.SizeOf<int>() * mapped.Length;
                copied = UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes);
                if (!copied)
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(NativeOwner);
            }
            finally
            {
                _dirtyIndexBuffer.UnlockBufferAfterWrite<int>(dirtyCount);
            }

            return copied;
        }

        private bool UploadDirtyStates(int dirtyCount, GraphicsBuffer buffer)
        {
            if (buffer == null ||
                dirtyCount <= 0 ||
                !TryResolveBuffer(ref _dirtyIndicesHandle, out NativeArray<int> dirtyIndices))
                return false;

            bool uploaded = true;
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

                uploaded &= UploadStateRun(buffer, runStart, runEnd - runStart + 1);
                runStart = index;
                runEnd = index;
            }

            uploaded &= UploadStateRun(buffer, runStart, runEnd - runStart + 1);
            return uploaded;
        }

        private bool UploadStateRun(GraphicsBuffer buffer, int startIndex, int count)
        {
            if (buffer == null || count <= 0)
                return false;

            bool copied = false;
            NativeArray<TerminalStateDTO> mapped = buffer.LockBufferForWrite<TerminalStateDTO>(startIndex, count);
            try
            {
                byte* sourceBase = (byte*)ResolveTerminalStatePointer();
                if (sourceBase == null)
                {
                    _lastFaultFlags |= FaultVaultUnavailable;
                    return false;
                }

                void* sourcePtr = sourceBase + (startIndex * UnsafeUtility.SizeOf<TerminalStateDTO>());
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                long copyBytes = (long)UnsafeUtility.SizeOf<TerminalStateDTO>() * count;
                long destinationBytes = (long)UnsafeUtility.SizeOf<TerminalStateDTO>() * mapped.Length;
                copied = UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes);
                if (!copied)
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(NativeOwner);
            }
            finally
            {
                buffer.UnlockBufferAfterWrite<TerminalStateDTO>(count);
            }

            return copied;
        }

        private bool UploadScreenCommands()
        {
            if (_screenCommandBuffer == null ||
                !TryResolveBuffer(ref _screenCommandsHandle, out NativeArray<ScreenCommandDTO> screenCommands))
                return false;

            bool copied = false;
            NativeArray<ScreenCommandDTO> mapped = _screenCommandBuffer.LockBufferForWrite<ScreenCommandDTO>(0, _terminalCount);
            try
            {
                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(screenCommands);
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                long copyBytes = (long)UnsafeUtility.SizeOf<ScreenCommandDTO>() * _terminalCount;
                long destinationBytes = (long)UnsafeUtility.SizeOf<ScreenCommandDTO>() * mapped.Length;
                copied = UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes);
                if (!copied)
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(NativeOwner);
            }
            finally
            {
                _screenCommandBuffer.UnlockBufferAfterWrite<ScreenCommandDTO>(_terminalCount);
            }

            if (copied)
                _layoutUploadDirty = false;
            return copied;
        }

        private bool UploadGlyphUvs()
        {
            if (_glyphUvBuffer == null ||
                !TryResolveBuffer(ref _glyphUvsHandle, out NativeArray<float4> glyphUvs))
                return false;

            bool copied = false;
            NativeArray<float4> mapped = _glyphUvBuffer.LockBufferForWrite<float4>(0, TerminalOsConstants.GlyphCount);
            try
            {
                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(glyphUvs);
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                long copyBytes = (long)UnsafeUtility.SizeOf<float4>() * TerminalOsConstants.GlyphCount;
                long destinationBytes = (long)UnsafeUtility.SizeOf<float4>() * mapped.Length;
                copied = UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes);
                if (!copied)
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(NativeOwner);
            }
            finally
            {
                _glyphUvBuffer.UnlockBufferAfterWrite<float4>(TerminalOsConstants.GlyphCount);
            }

            if (copied)
                _glyphUploadDirty = false;
            return copied;
        }

        private bool UploadPanelInstances()
        {
            if (_panelInstanceBuffer == null ||
                !TryResolveBuffer(ref _panelInstancesHandle, out NativeArray<TerminalPanelInstanceDTO> panelInstances))
                return false;

            bool copied = false;
            NativeArray<TerminalPanelInstanceDTO> mapped = _panelInstanceBuffer.LockBufferForWrite<TerminalPanelInstanceDTO>(0, _terminalCount);
            try
            {
                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(panelInstances);
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                long copyBytes = (long)UnsafeUtility.SizeOf<TerminalPanelInstanceDTO>() * _terminalCount;
                long destinationBytes = (long)UnsafeUtility.SizeOf<TerminalPanelInstanceDTO>() * mapped.Length;
                copied = UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes);
                if (!copied)
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(NativeOwner);
            }
            finally
            {
                _panelInstanceBuffer.UnlockBufferAfterWrite<TerminalPanelInstanceDTO>(_terminalCount);
            }

            if (copied)
                _panelInstanceUploadDirty = false;
            return copied;
        }

        private int DispatchDirtyScreens(int dirtyCount)
        {
            if (terminalBlitCompute == null || _blitKernel < 0 || _terminalTextureArray == null || dirtyCount <= 0)
                return 0;

            long start = Stopwatch.GetTimestamp();
            GraphicsBuffer stateBuffer = ResolveStateBuffer(_writeBufferIndex);
            if (stateBuffer == null)
                return 0;
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
            terminalBlitCompute.SetFloat(HectonDiegeticGlitchQualityWeightId, _globalQualityWeight);
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

            if (frame % _framesBetweenUpdates != 0)
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
                !TryResolveBuffer(ref _buttonAabbHandle, out NativeArray<ButtonAABBDTO> buttons))
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
                Buttons = buttons,
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

        private void TryScheduleTerminalInteractionPipeline(int frame, bool refreshAvailabilityFromStates)
        {
            if (_terminalInteractionScheduled ||
                _terminalCount <= 0 ||
                !TryResolveBuffer(ref _terminalPlanesHandle, out NativeArray<TerminalPlaneDTO> terminalPlanes) ||
                !TryResolveBuffer(ref _gazeRayHandle, out NativeArray<GazeRayDTO> gazeRays) ||
                !TryResolveBuffer(ref _terminalInteractionsHandle, out NativeArray<TerminalInteractionDTO> interactions) ||
                !TryResolveBuffer(ref _buttonAabbHandle, out NativeArray<ButtonAABBDTO> buttons))
            {
                return;
            }

            if (refreshAvailabilityFromStates)
                RefreshTerminalPlaneAvailability(terminalPlanes);
            ResolveGazeInput(
                frame,
                out AbsoluteUniversePosition originAup,
                out float3 forward,
                out float2 scrollDelta,
                out uint interactionFlags);

            int batchSize = math.clamp((int)math.round(math.lerp(1f, 32f, _globalQualityWeight)), 1, 32);
            float maxDistance = math.max(0.5f, interactionMaxDistanceMeters);
            float viewCone = math.clamp(interactionViewConeCos, -0.5f, 0.95f);
            _interactionScheduleTicks = Stopwatch.GetTimestamp();

            JobHandle gazeHandle = new MockGazeRayJob
            {
                GazeRays = gazeRays,
                FallbackOriginAup = originAup,
                FallbackForward = forward,
                ScrollDelta = scrollDelta,
                InteractionFlags = interactionFlags,
                Frame = (uint)frame,
                MicroSwayRadians = math.lerp(0.0125f, 0.0005f, _globalQualityWeight) * math.saturate(hologramDistortionIntensity)
            }.Schedule();

            JobHandle cullHandle = new CullTerminalsJob
            {
                Planes = terminalPlanes,
                GazeRays = gazeRays,
                Interactions = interactions,
                TerminalCount = _terminalCount,
                MaxDistanceMeters = maxDistance,
                ViewConeCos = viewCone
            }.Schedule(_terminalCount, batchSize, gazeHandle);

            JobHandle intersectionHandle = new TerminalIntersectionJob
            {
                Planes = terminalPlanes,
                GazeRays = gazeRays,
                Interactions = interactions,
                TerminalCount = _terminalCount,
                MaxDistanceMeters = maxDistance
            }.Schedule(_terminalCount, batchSize, cullHandle);

            _terminalInteractionHandle = new EvaluateTerminalButtonsJob
            {
                Interactions = interactions,
                Planes = terminalPlanes,
                Buttons = buttons,
                TerminalCount = _terminalCount,
                ButtonCount = _buttonCount,
                Frame = (uint)frame,
                Commands = SignalBus<TerminalCommandSignal>.ParallelWriter,
                UiSignals = SignalBus<InteractionUiSignal>.ParallelWriter
            }.Schedule(_terminalCount, batchSize, intersectionHandle);
            _terminalInteractionScheduled = true;
        }

        private void TryFinalizeTerminalInteractionJob()
        {
            if (!_terminalInteractionScheduled)
                return;

            if (!TryFinalizeCompletedJob(ref _terminalInteractionHandle))
                return;

            _terminalInteractionScheduled = false;
            _lastIntersectionMicroseconds = _interactionScheduleTicks > 0
                ? ElapsedMicroseconds(_interactionScheduleTicks)
                : 0f;
            _interactionScheduleTicks = 0;
            AuditLatestInteractions();
        }

        private void AuditLatestInteractions()
        {
            _lastHoveredTerminalHash = 0u;
            _lastEvaluatedTerminalCount = 0;
            if (!TryResolveBuffer(ref _terminalInteractionsHandle, out NativeArray<TerminalInteractionDTO> interactions))
                return;

            float closest = float.MaxValue;
            for (int i = 0; i < _terminalCount; i++)
            {
                TerminalInteractionDTO interaction = interactions[i];
                if ((interaction.InteractionFlags & TerminalOsConstants.InteractionFlagNonFinite) != 0u)
                    _lastFaultFlags |= FaultNonFinite;

                if ((interaction.InteractionFlags & TerminalOsConstants.InteractionFlagHover) == 0u)
                    continue;

                _lastEvaluatedTerminalCount++;
                if (interaction.Distance < closest)
                {
                    closest = interaction.Distance;
                    _lastHoveredTerminalHash = interaction.TerminalHash;
                }
            }
        }

        private void RefreshTerminalPlaneAvailability(NativeArray<TerminalPlaneDTO> terminalPlanes)
        {
            if (!TryResolveBuffer(ref _terminalStatesHandle, out NativeArray<TerminalStateDTO> terminalStates))
                return;

            int count = math.min(_terminalCount, math.min(terminalPlanes.Length, terminalStates.Length));
            for (int i = 0; i < count; i++)
            {
                TerminalPlaneDTO plane = terminalPlanes[i];
                TerminalStateDTO state = terminalStates[i];
                float statePower01 = math.saturate(math.isfinite(state.Value1) ? state.Value1 : 0f);
                float routedPower01 = math.saturate(math.isfinite(plane.Power01) ? plane.Power01 : statePower01);
                float power01 = math.min(statePower01, routedPower01);
                uint flags = plane.Flags | TerminalOsConstants.PlaneFlagActive;
                if (power01 > 0.001f)
                    flags |= TerminalOsConstants.PlaneFlagPowered;
                else
                    flags &= ~TerminalOsConstants.PlaneFlagPowered;

                if (plane.Submerged01 > 0.5f)
                    flags |= TerminalOsConstants.PlaneFlagSubmerged;
                else
                    flags &= ~TerminalOsConstants.PlaneFlagSubmerged;

                plane.TerminalHash = state.TerminalHash != 0u ? state.TerminalHash : plane.TerminalHash;
                plane.Power01 = power01;
                plane.Flags = flags;
                terminalPlanes[i] = plane;
            }
        }

        private void ResolveGazeInput(
            int frame,
            out AbsoluteUniversePosition originAup,
            out float3 forward,
            out float2 scrollDelta,
            out uint interactionFlags)
        {
            ResolveGazePose(frame, out originAup, out forward);
            interactionFlags = 0u;
            scrollDelta = default;

            if (_input == null || !_input.IsInitialized)
                return;

            PlayerInputState state = _input.GetState();
            Vector2 scroll = state.ScrollDelta;
            scrollDelta = SanitizeVector2(scroll);
            bool pressed = state.HasAction(PlayerInputAction.Interact) || state.HasAction(PlayerInputAction.PrimaryFire);
            if (pressed && !_inputPressedLastFrame)
                interactionFlags |= TerminalOsConstants.InteractionFlagPress;
            if (pressed)
                interactionFlags |= TerminalOsConstants.InteractionFlagHold;
            if (!pressed && _inputPressedLastFrame)
                interactionFlags |= TerminalOsConstants.InteractionFlagRelease;
            if (math.lengthsq(scrollDelta) > 0.000001f)
                interactionFlags |= TerminalOsConstants.InteractionFlagScroll;
            _inputPressedLastFrame = pressed;
        }

        private void ResolveGazePose(int frame, out AbsoluteUniversePosition originAup, out float3 forward)
        {
            Camera camera = ResolveAttentionCamera(frame);
            if (camera != null)
            {
                Transform cameraTransform = camera.transform;
                Vector3 position = cameraTransform.position;
                Vector3 direction = cameraTransform.forward;
                if (VectorFinite(position) && VectorFinite(direction))
                {
                    originAup = AbsoluteUniversePosition.FromRuntimePosition(position);
                    forward = math.normalizesafe(ToFloat3(direction), new float3(0f, 0f, 1f));
                    return;
                }

                _lastFaultFlags |= FaultNonFinite;
            }

            Vector3 fallbackPosition = transform.position;
            Vector3 fallbackForward = transform.forward;
            originAup = AbsoluteUniversePosition.FromRuntimePosition(VectorFinite(fallbackPosition) ? fallbackPosition : Vector3.zero);
            forward = VectorFinite(fallbackForward)
                ? math.normalizesafe(ToFloat3(fallbackForward), new float3(0f, 0f, 1f))
                : new float3(0f, 0f, 1f);
        }

        private void TryMonitorLayoutCsv(int frame)
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return;
#else
            if (string.IsNullOrEmpty(_csvFullPath))
                return;

            if (frame < _csvProbeFrame)
                return;

            _csvProbeFrame = frame + math.clamp((int)math.round(math.lerp(30f, 120f, 1f - _globalQualityWeight)), 30, 120);
            if (!File.Exists(_csvFullPath))
                return;

            DateTime writeUtc = File.GetLastWriteTimeUtc(_csvFullPath);
            if (writeUtc <= _csvLastWriteUtc)
                return;

            _csvLastWriteUtc = writeUtc;
            Span<byte> csvScratch = stackalloc byte[8192];
            int bytesRead;
            using (FileStream stream = new FileStream(_csvFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                bytesRead = stream.Read(csvScratch);
            }

            if (bytesRead > 0 && ParseLayoutCsv(csvScratch.Slice(0, bytesRead)))
            {
                _layoutUploadDirty = true;
                ForceAllDirty();
            }
#endif
        }

        private bool ParseLayoutCsv(ReadOnlySpan<byte> bytes)
        {
            bool changed = false;
            int lineStart = 0;
            for (int i = 0; i <= bytes.Length; i++)
            {
                bool lineEnd = i == bytes.Length || bytes[i] == (byte)'\n' || bytes[i] == (byte)'\r';
                if (!lineEnd)
                    continue;

                if (i > lineStart)
                    changed |= TryParseLayoutLine(bytes, lineStart, i);
                lineStart = i + 1;
            }

            return changed;
        }

        private bool TryParseLayoutLine(ReadOnlySpan<byte> bytes, int start, int end)
        {
            int a = FindCsvComma(bytes, start, end);
            if (a <= start)
                return false;
            int b = FindCsvComma(bytes, a + 1, end);
            int c = FindCsvComma(bytes, b + 1, end);
            if (b <= a || c <= b)
                return false;

            int d = FindCsvComma(bytes, c + 1, end);
            int e = d > c ? FindCsvComma(bytes, d + 1, end) : -1;
            if (d > c && e > d)
                return TryParseButtonLayoutLine(bytes, start, a, b, c, d, e, end);

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

        private bool TryParseButtonLayoutLine(ReadOnlySpan<byte> bytes, int start, int terminalEnd, int xEnd, int yEnd, int widthEnd, int heightEnd, int end)
        {
            uint terminalHash = ParseHashOrName(bytes, start, terminalEnd);
            uint actionHash = ParseHashOrName(bytes, heightEnd + 1, end);
            if (!TryParseFloat(bytes, terminalEnd + 1, xEnd, out float x) ||
                !TryParseFloat(bytes, xEnd + 1, yEnd, out float y) ||
                !TryParseFloat(bytes, yEnd + 1, widthEnd, out float width) ||
                !TryParseFloat(bytes, widthEnd + 1, heightEnd, out float height) ||
                actionHash == 0u)
            {
                return false;
            }

            if (!TryResolveBuffer(ref _buttonAabbHandle, out NativeArray<ButtonAABBDTO> buttons))
                return false;

            float2 min = SanitizeUv01(new float2(x, y));
            float2 max = SanitizeUv01(new float2(x + math.max(0.001f, width), y + math.max(0.001f, height)));
            float4 rect = new float4(
                math.min(min.x, max.x),
                math.min(min.y, max.y),
                math.max(min.x, max.x),
                math.max(min.y, max.y));
            int fallbackIndex = -1;
            for (int i = 0; i < _buttonCount; i++)
            {
                ButtonAABBDTO button = buttons[i];
                if (button.TerminalHash != terminalHash)
                    continue;

                if (button.CommandHash == actionHash)
                {
                    button.RectUv = rect;
                    button.Flags |= TerminalOsConstants.ButtonFlagEnabled;
                    buttons[i] = button;
                    return true;
                }

                if (fallbackIndex < 0)
                    fallbackIndex = i;
            }

            if (fallbackIndex < 0)
                return false;

            ButtonAABBDTO fallback = buttons[fallbackIndex];
            fallback.RectUv = rect;
            fallback.CommandHash = actionHash;
            fallback.Flags |= TerminalOsConstants.ButtonFlagEnabled;
            buttons[fallbackIndex] = fallback;
            return true;
        }

        private static int FindCsvComma(ReadOnlySpan<byte> bytes, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                if (bytes[i] == (byte)',')
                    return i;
            }

            return -1;
        }

        private static uint ParseHashOrName(ReadOnlySpan<byte> bytes, int start, int end)
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

        private static bool TryParseFloat(ReadOnlySpan<byte> bytes, int start, int end, out float value)
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

        private static bool MatchesPowerLevelToken(ReadOnlySpan<byte> bytes, int offset)
        {
            return offset + 12 < bytes.Length &&
                   bytes[offset] == (byte)'^' &&
                   bytes[offset + 1] == (byte)'P' &&
                   bytes[offset + 2] == (byte)'O' &&
                   bytes[offset + 3] == (byte)'W' &&
                   bytes[offset + 4] == (byte)'E' &&
                   bytes[offset + 5] == (byte)'R' &&
                   bytes[offset + 6] == (byte)'_' &&
                   bytes[offset + 7] == (byte)'L' &&
                   bytes[offset + 8] == (byte)'E' &&
                   bytes[offset + 9] == (byte)'V' &&
                   bytes[offset + 10] == (byte)'E' &&
                   bytes[offset + 11] == (byte)'L' &&
                   bytes[offset + 12] == (byte)'^';
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

            bool hasPlanes = TryResolveBuffer(ref _terminalPlanesHandle, out NativeArray<TerminalPlaneDTO> terminalPlanes);
            bool hasStates = TryResolveBuffer(ref _terminalStatesHandle, out NativeArray<TerminalStateDTO> terminalStates);
            float quality = _globalQualityWeight;
            float glitch = _lastDiegeticGlitchIntensity;
            bool scalarChanged = math.abs(_lastPanelInstanceQualityWeight - quality) > 0.0005f ||
                                 math.abs(_lastPanelInstanceGlitchIntensity - glitch) > 0.0005f;
            bool forceRewrite = _panelInstanceUploadDirty || scalarChanged;
            bool changed = forceRewrite;
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

                if (MatrixEquals(panelInstances[i].LocalToWorld, matrix) && !forceRewrite)
                    continue;

                panelInstances[i] = new TerminalPanelInstanceDTO
                {
                    LocalToWorld = matrix,
                    SliceFlags = new float4(i, 1f - quality, quality, glitch)
                };
                if (hasPlanes && i < terminalPlanes.Length)
                {
                    TerminalPlaneDTO previous = terminalPlanes[i];
                    TerminalStateDTO state = hasStates && i < terminalStates.Length ? terminalStates[i] : default;
                    uint terminalHash = state.TerminalHash != 0u ? state.TerminalHash : TerminalOsHash.HashIndex(i);
                    float statePower01 = state.TerminalHash != 0u ? state.Value1 : previous.Power01;
                    float power01 = math.min(
                        math.saturate(math.isfinite(statePower01) ? statePower01 : 0f),
                        math.saturate(math.isfinite(previous.Power01) ? previous.Power01 : 1f));
                    terminalPlanes[i] = BuildTerminalPlane(
                        matrix,
                        terminalHash,
                        previous.LayoutFirstButton,
                        previous.LayoutButtonCount,
                        power01,
                        previous.Submerged01);
                }
                changed = true;
            }

            if (!changed)
                return;

            RecalculatePanelRenderBounds();
            if (UploadPanelInstances())
            {
                _lastPanelInstanceQualityWeight = quality;
                _lastPanelInstanceGlitchIntensity = glitch;
            }
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

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawTerminalDebugGizmos ||
                !TryResolveBuffer(ref _terminalPlanesHandle, out NativeArray<TerminalPlaneDTO> terminalPlanes) ||
                !TryResolveBuffer(ref _buttonAabbHandle, out NativeArray<ButtonAABBDTO> buttons))
            {
                return;
            }

            TryResolveBuffer(ref _terminalInteractionsHandle, out NativeArray<TerminalInteractionDTO> interactions);
            int count = math.min(_terminalCount, terminalPlanes.Length);
            for (int i = 0; i < count; i++)
            {
                TerminalInteractionDTO interaction = interactions.IsCreated && i < interactions.Length ? interactions[i] : default;
                DrawTerminalPlaneGizmo(in terminalPlanes[i], buttons, in interaction);
            }
        }

        private static void DrawTerminalPlaneGizmo(
            in TerminalPlaneDTO plane,
            NativeArray<ButtonAABBDTO> buttons,
            in TerminalInteractionDTO interaction)
        {
            float3 center = plane.CenterAup.ToRuntimeFloat3();
            float3 right = math.normalizesafe(plane.Right, new float3(1f, 0f, 0f));
            float3 up = math.normalizesafe(plane.Up, new float3(0f, 1f, 0f));
            float3 halfRight = right * (math.max(0.001f, plane.Width) * 0.5f);
            float3 halfUp = up * (math.max(0.001f, plane.Height) * 0.5f);

            Gizmos.color = (plane.Flags & TerminalOsConstants.PlaneFlagPowered) != 0u
                ? new Color(0f, 0.85f, 1f, 0.85f)
                : new Color(0.35f, 0.35f, 0.35f, 0.65f);
            DrawGizmoRect(center, halfRight, halfUp);

            uint first = plane.LayoutFirstButton;
            uint last = math.min(first + plane.LayoutButtonCount, (uint)buttons.Length);
            Gizmos.color = new Color(1f, 0.82f, 0.14f, 0.85f);
            for (uint i = first; i < last; i++)
            {
                ButtonAABBDTO button = buttons[(int)i];
                if ((button.Flags & TerminalOsConstants.ButtonFlagEnabled) == 0u)
                    continue;

                float4 rect = button.RectUv;
                float3 buttonCenter = center +
                    right * (((rect.x + rect.z) * 0.5f) - 0.5f) * plane.Width +
                    up * (((rect.y + rect.w) * 0.5f) - 0.5f) * plane.Height;
                float3 buttonHalfRight = right * (math.max(0.001f, rect.z - rect.x) * plane.Width * 0.5f);
                float3 buttonHalfUp = up * (math.max(0.001f, rect.w - rect.y) * plane.Height * 0.5f);
                DrawGizmoRect(buttonCenter, buttonHalfRight, buttonHalfUp);
            }

            if ((interaction.InteractionFlags & TerminalOsConstants.InteractionFlagHover) == 0u)
                return;

            Gizmos.color = Color.magenta;
            float2 uv = interaction.LocalHitUV;
            float3 hit = center + right * ((uv.x - 0.5f) * plane.Width) + up * ((uv.y - 0.5f) * plane.Height);
            Gizmos.DrawWireSphere(ToVector3(hit), 0.035f);
        }

        private static void DrawGizmoRect(float3 center, float3 halfRight, float3 halfUp)
        {
            Vector3 a = ToVector3(center - halfRight - halfUp);
            Vector3 b = ToVector3(center + halfRight - halfUp);
            Vector3 c = ToVector3(center + halfRight + halfUp);
            Vector3 d = ToVector3(center - halfRight + halfUp);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }
#endif

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 SanitizeVector2(Vector2 value)
        {
            return math.all(math.isfinite(new float2(value.x, value.y)))
                ? new float2(value.x, value.y)
                : default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool VectorFinite(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private void BindTerminalRenderers()
        {
            if (_terminalTextureArray == null)
                return;

            Shader.SetGlobalTexture(TerminalTextureArrayId, _terminalTextureArray);
            if (terminalArrayMaterial != null)
            {
                terminalArrayMaterial.SetTexture(TerminalTextureArrayId, _terminalTextureArray);
                terminalArrayMaterial.SetFloat(HectonDiegeticGlitchQualityWeightId, _globalQualityWeight);
                if (drawPanelsInstanced && _panelInstanceBuffer != null)
                {
                    terminalArrayMaterial.SetBuffer(TerminalPanelInstancesId, _panelInstanceBuffer);
                    terminalArrayMaterial.EnableKeyword(TerminalInstancedKeyword);
                }
                else
                {
                    terminalArrayMaterial.DisableKeyword(TerminalInstancedKeyword);
                }
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
                HoveredTerminalHash = _lastHoveredTerminalHash,
                LastPower01 = _lastPower01,
                LastDamage01 = _lastDamage01,
                EvaluatedTerminals = _lastEvaluatedTerminalCount,
                FramesBetweenUpdates = _framesBetweenUpdates,
                IntersectionMicroseconds = _lastIntersectionMicroseconds,
                GlobalQualityWeight = _globalQualityWeight
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

            if (TryResolveBuffer(ref _buttonAabbHandle, out NativeArray<ButtonAABBDTO> buttons))
            {
                int buttonCount = math.min(_buttonCount, buttons.Length);
                for (int i = 0; i < buttonCount; i++)
                {
                    ButtonAABBDTO button = buttons[i];
                    hash = (hash ^ button.TerminalHash) * 16777619u;
                    hash = (hash ^ button.CommandHash) * 16777619u;
                    hash = (hash ^ math.asuint(button.RectUv.x)) * 16777619u;
                    hash = (hash ^ math.asuint(button.RectUv.y)) * 16777619u;
                    hash = (hash ^ math.asuint(button.RectUv.z)) * 16777619u;
                    hash = (hash ^ math.asuint(button.RectUv.w)) * 16777619u;
                }
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
                    writer.Write(entry.HoveredTerminalHash);
                    writer.Write(entry.LastPower01);
                    writer.Write(entry.LastDamage01);
                    writer.Write(entry.EvaluatedTerminals);
                    writer.Write(entry.FramesBetweenUpdates);
                    writer.Write(entry.IntersectionMicroseconds);
                    writer.Write(entry.GlobalQualityWeight);
                }
            }
        }

        private void CompleteJobsForTeardown()
        {
            if (_formatScheduled)
            {
                ForceCompleteJobForTeardown(ref _formatHandle);
                _formatScheduled = false;
            }

            if (_clickResolveScheduled)
            {
                ForceCompleteJobForTeardown(ref _clickResolveHandle);
                _clickResolveScheduled = false;
            }

            if (_terminalInteractionScheduled)
            {
                ForceCompleteJobForTeardown(ref _terminalInteractionHandle);
                _terminalInteractionScheduled = false;
            }
        }

        private static bool TryFinalizeCompletedJob(ref JobHandle handle)
        {
            if (!handle.IsCompleted)
                return false;

            return DispatcherJobFence.TryFinalizeCompleted(ref handle);
        }

        private static void ForceCompleteJobForTeardown(ref JobHandle handle)
        {
            DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
        }

        private void DisposeGraphicsResources()
        {
            ReleaseBuffer(ref _stateBuffer0);
            ReleaseBuffer(ref _stateBuffer1);
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
            _buttonAabbHandle = default;
            _panelInstancesHandle = default;
            _clickScratchHandle = default;
            _terminalPlanesHandle = default;
            _gazeRayHandle = default;
            _terminalInteractionsHandle = default;
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
