using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Hecton8.Rendering
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9210)]
    public sealed unsafe class HectonBilateralDrsUpscalerRuntime : MonoBehaviour, IDispatcherSystem, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private const SystemID OwnerSystemId = SystemID.GraphicsScalability;
        private const uint SimulationSystemHash = 0x4232534Du; // B2SM
        private const uint PostSimulationSystemHash = 0x4232504Fu; // B2PO
        private const uint VisualSyncSystemHash = 0x42325653u; // B2VS
        private const string BlackBoxDumpPath = "Docs/AgentLogs/Dump_BILATERAL_DRS_UPSCALER.bin";
        private const string BlackBoxDumpPayloadLabel = "bilateralDrsBlackBoxDumpPayload";
        private const uint BlackBoxDumpMagic = 0x42324438u; // 8D2B
        private const uint BlackBoxDumpVersion = 1u;
        private const int BlackBoxDumpHeaderBytes = 32;
        private const int QualityProfileCsvColumnCount = 8;
        private sealed class SimulationKernelBridge : IDispatcherSystem, IDispatcherFenceDomainProvider
        {
            private readonly HectonBilateralDrsUpscalerRuntime _owner;

            public SimulationKernelBridge(HectonBilateralDrsUpscalerRuntime owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => SimulationSystemHash;

            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.Simulation;

            public byte GetBucketId() => byte.MaxValue;

            public int GetDependencyCount() => 0;

            public uint GetDependencyHash(int dependencyIndex) => 0u;

            public DispatcherFenceDomain GetFenceDomain() => DispatcherFenceDomain.Simulation;

            public JobHandle ScheduleSimulation(
                in DispatcherTimingDTO timing,
                in DispatcherJobContext context,
                JobHandle dependsOn)
            {
                return _owner != null
                    ? _owner.ScheduleOwnerSimulation(in timing, in context, dependsOn)
                    : dependsOn;
            }

        }

        private sealed class PostSimulationPublishBridge : IDispatcherSystem
        {
            private readonly HectonBilateralDrsUpscalerRuntime _owner;

            public PostSimulationPublishBridge(HectonBilateralDrsUpscalerRuntime owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => PostSimulationSystemHash;

            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.PostSimulation;

            public byte GetBucketId() => byte.MaxValue;

            public int GetDependencyCount() => 0;

            public uint GetDependencyHash(int dependencyIndex) => 0u;

            public JobHandle ScheduleSimulation(
                in DispatcherTimingDTO timing,
                in DispatcherJobContext context,
                JobHandle dependsOn)
            {
                return dependsOn;
            }

            public void PostSimulationTick(in DispatcherTimingDTO timing)
            {
                _owner?.RunOwnerPostSimulation();
            }

        }

        private sealed class VisualSyncUploadBridge : IDispatcherSystem
        {
            private readonly HectonBilateralDrsUpscalerRuntime _owner;

            public VisualSyncUploadBridge(HectonBilateralDrsUpscalerRuntime owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => VisualSyncSystemHash;

            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.VisualSync;

            public byte GetBucketId() => byte.MaxValue;

            public int GetDependencyCount() => 0;

            public uint GetDependencyHash(int dependencyIndex) => 0u;

            public JobHandle ScheduleSimulation(
                in DispatcherTimingDTO timing,
                in DispatcherJobContext context,
                JobHandle dependsOn)
            {
                return dependsOn;
            }

            public void VisualSyncTick(in DispatcherTimingDTO timing)
            {
                _owner?.RunOwnerVisualSync(in timing);
            }
        }

        private static HectonBilateralDrsUpscalerRuntime s_runtimeInstance;
        private static GraphicsBuffer s_publishedConstantBuffer;
        private static uint s_publishedConstantBufferFrameIndex;
        private static UpscalerParamsDTO s_lastPublishedParameters;
        private static bool s_hasPublishedParameters;
        private static bool s_edgeMaskDebugEnabled;

        private IDataVault _dataVault;
        private IResolutionScalerService _resolutionScaler;
        private VaultGenerationHandle<UpscalerParamsDTO> _parametersHandle;
        private VaultGenerationHandle<UpscalerTuningDTO> _tuningHandle;
        private VaultGenerationHandle<UpscalerTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<UpscalerProfileDTO> _profilesHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<DrsStateDTO> _mockStateHandle;
        private GraphicsBuffer _constantBufferA;
        private GraphicsBuffer _constantBufferB;
        private GraphicsBuffer _activeConstantBuffer;
        private SimulationKernelBridge _simulationBridge;
        private PostSimulationPublishBridge _postSimulationBridge;
        private VisualSyncUploadBridge _visualSyncBridge;
        private int _activeConstantBufferIndex;
        private bool _registeredPreSimulationDispatcher;
        private bool _registeredSimulationDispatcher;
        private bool _registeredPostSimulationDispatcher;
        private bool _registeredVisualSyncDispatcher;
        private bool _registeredSlowTick;
        private bool _dispatcherRouteReady;
        private bool _registeredHotSwapListener;
        private bool _coldDependenciesCached;
        private bool _isInitialized;
        private bool _resourceRefreshRequested;
        private bool _vaultStateReady;
        private bool _coldSupportsSetConstantBuffer;
        private bool _tuningSeeded;
        private bool _telemetrySeeded;
        private bool _telemetryCursorSeeded;
        private bool _profilesSeeded;
        private bool _mockStateSeeded;
        private bool _simulationPendingPublish;
        private bool _pendingTelemetryEntryValid;
        private bool _pendingGpuUpload;
        private bool _faultDumped;
        private int _telemetryWriteCursor;
        private uint _lastFaultFlags;
        private uint _presentationFrameIndex;
        private float _presentationTimeSeconds;
        private UpscalerTelemetryEntry _pendingTelemetryEntry;
        private int _submittedLowWidth;
        private int _submittedLowHeight;
        private int _submittedFullWidth;
        private int _submittedFullHeight;
        private float _submittedJitterX;
        private float _submittedJitterY;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            s_runtimeInstance = null;
            s_publishedConstantBuffer = null;
            s_publishedConstantBufferFrameIndex = 0u;
            s_lastPublishedParameters = default;
            s_hasPublishedParameters = false;
            s_edgeMaskDebugEnabled = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapAfterSceneLoad()
        {
            if (!Application.isPlaying)
                return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (s_runtimeInstance != null)
                return;

            EnsureRuntimeInstance();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            if (!Application.isPlaying || s_runtimeInstance != null)
                return;

            EnsureRuntimeInstance();
        }

        public static HectonBilateralDrsUpscalerRuntime EnsureRuntimeInstance()
        {
            if (!Application.isPlaying)
                return null;

            if (s_runtimeInstance != null)
                return s_runtimeInstance;

            // Player-build construction path: no authored/bootstrap instance reachable.
            // 13KRA bilateral DRS upscaler must self-construct when scene load path races.
            GameObject runtimeRoot = new GameObject("[HectonBilateralDrsUpscalerRuntime]"); // COLD ALLOC: GameObject[1] - scene-local 13KRA render-owner bootstrap.
            return runtimeRoot.AddComponent<HectonBilateralDrsUpscalerRuntime>();
        }

        public static bool TryGetRuntimeInstance(out HectonBilateralDrsUpscalerRuntime runtime)
        {
            runtime = s_runtimeInstance;
            return runtime != null;
        }

        public static bool TryGetActiveConstantBufferForDimensions(
            int lowWidth,
            int lowHeight,
            int fullWidth,
            int fullHeight,
            out GraphicsBuffer constantBuffer,
            out uint frameIndex)
        {
            constantBuffer = s_publishedConstantBuffer;
            frameIndex = s_publishedConstantBufferFrameIndex;
            if (constantBuffer == null || !constantBuffer.IsValid() || !s_hasPublishedParameters)
                return false;

            float4 resolution = s_lastPublishedParameters.ResolutionParams;
            return MatchesDimension(resolution.x, lowWidth) &&
                   MatchesDimension(resolution.y, lowHeight) &&
                   MatchesDimension(resolution.z, fullWidth) &&
                   MatchesDimension(resolution.w, fullHeight);
        }

        public static void SubmitRenderDimensions(
            int lowWidth,
            int lowHeight,
            int fullWidth,
            int fullHeight,
            float jitterX,
            float jitterY)
        {
            HectonBilateralDrsUpscalerRuntime runtime = s_runtimeInstance;
            if (runtime == null)
                return;

            runtime.SetSubmittedRenderDimensions(lowWidth, lowHeight, fullWidth, fullHeight, jitterX, jitterY);
        }

        public static bool TryGetActiveConstantBuffer(out GraphicsBuffer constantBuffer, out uint frameIndex)
        {
            constantBuffer = s_publishedConstantBuffer;
            frameIndex = s_publishedConstantBufferFrameIndex;
            return constantBuffer != null && constantBuffer.IsValid() && s_hasPublishedParameters;
        }

        public static bool TryReadActiveParameters(out UpscalerParamsDTO parameters)
        {
            parameters = s_lastPublishedParameters;
            return s_hasPublishedParameters;
        }

        private static bool MatchesDimension(float actual, int expected)
        {
            return math.isfinite(actual) && math.abs(actual - math.max(1, expected)) <= 0.5f;
        }

        private static void InvalidatePublishedParameters()
        {
            s_publishedConstantBuffer = null;
            s_publishedConstantBufferFrameIndex = 0u;
            s_lastPublishedParameters = default;
            s_hasPublishedParameters = false;
        }

        public static bool TryReadEditorTuning(out UpscalerTuningDTO tuning)
        {
            tuning = default;
            HectonBilateralDrsUpscalerRuntime runtime = s_runtimeInstance;
            if (runtime == null || !runtime._vaultStateReady)
                return false;

            if (!runtime.TryReadVaultBuffer(
                    in runtime._tuningHandle,
                    BufferID.Shinobu236BilateralDrsTuning,
                    1,
                    out NativeArray<UpscalerTuningDTO>.ReadOnly tuningArray))
                return false;

            tuning = tuningArray[0];
            return true;
        }

        public static bool TrySetEditorTuning(
            float depthWeight,
            float colorWeight,
            float minRadius,
            float maxRadius,
            float forcedScale01,
            float forcedQuality01,
            float qualityBias01,
            bool debugEdgeMask)
        {
            HectonBilateralDrsUpscalerRuntime runtime = EnsureRuntimeInstance();
            if (runtime == null)
                return false;

            runtime.EnsureVaultState(allowAllocation: true);
            if (!runtime.TryAcquireVaultWriteBuffer(
                    in runtime._tuningHandle,
                    BufferID.Shinobu236BilateralDrsTuning,
                    1,
                    out NativeArray<UpscalerTuningDTO> tuningArray,
                    out IDataVault tuningVault))
            {
                return false;
            }

            try
            {
                UpscalerTuningDTO tuning = tuningArray[0];
                tuning.DepthColorRadiusSharpness.x = math.max(1f, depthWeight);
                tuning.DepthColorRadiusSharpness.y = math.max(0.001f, colorWeight);
                tuning.DepthColorRadiusSharpness.z = math.max(0.25f, minRadius);
                tuning.DepthColorRadiusSharpness.w = math.max(tuning.DepthColorRadiusSharpness.z, maxRadius);
                tuning.ScaleQualityOverride.x = math.clamp(forcedScale01, 0f, 1f);
                tuning.ScaleQualityOverride.y = forcedQuality01 >= 0f ? math.saturate(forcedQuality01) : -1f;
                tuning.ScaleQualityOverride.z = math.clamp(qualityBias01, -1f, 1f);
                tuning.DebugAndFlags.x = debugEdgeMask ? 1f : 0f;
                tuningArray[0] = tuning;
                s_edgeMaskDebugEnabled = debugEdgeMask;
                runtime._tuningSeeded = true;
                return true;
            }
            finally
            {
                tuningVault?.ReleaseWriteLock(in runtime._tuningHandle, OwnerSystemId);
            }
        }

#if UNITY_EDITOR
        public static bool TryLoadQualityProfilesCsv(string projectRelativePath)
        {
            HectonBilateralDrsUpscalerRuntime runtime = EnsureRuntimeInstance();
            return runtime != null && runtime.LoadQualityProfilesCsv(projectRelativePath);
        }
#endif

        public static bool IsEdgeMaskDebugEnabled()
        {
            return s_edgeMaskDebugEnabled;
        }

        private void OnEnable()
        {
            if (s_runtimeInstance != null && !ReferenceEquals(s_runtimeInstance, this))
            {
                enabled = false;
                return;
            }

            s_runtimeInstance = this;
            CacheGraphicsCapabilitiesCold();
            InitializeServiceForVisualSync(allowAllocation: true);
            TryRegisterSlowTick();
        }

        private void OnDisable()
        {
            ShutdownServiceState();
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
        }

        public uint GetSystemIdHash() => BilateralDrsUpscalerConstants.StateHash;

        public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.PreSimulation;

        public byte GetBucketId() => byte.MaxValue;

        public int GetDependencyCount() => 0;

        public uint GetDependencyHash(int dependencyIndex) => 0u;

        public void PreSimulationTick(in DispatcherTimingDTO timing)
        {
            RunOwnerPreSimulation();
        }

        public JobHandle ScheduleSimulation(
            in DispatcherTimingDTO timing,
            in DispatcherJobContext context,
            JobHandle dependsOn)
        {
            return dependsOn;
        }

        public void SlowTick()
        {
            if (!_resourceRefreshRequested && _isInitialized && _dispatcherRouteReady)
                return;

            InitializeServiceForVisualSync(allowAllocation: true);
            _resourceRefreshRequested = !_isInitialized || !_dispatcherRouteReady;
        }

        private void RunOwnerPreSimulation()
        {
            if (!_isInitialized)
            {
                if (!TryUsePreparedServiceStateHot(requireConstantBuffers: false))
                    return;
            }

            if (!_vaultStateReady)
                return;
        }

        private JobHandle ScheduleOwnerSimulation(
            in DispatcherTimingDTO timing,
            in DispatcherJobContext context,
            JobHandle dependsOn)
        {
            if (!_isInitialized || !_vaultStateReady)
                return dependsOn;

            if (!TryReadVaultBuffer(
                    in _tuningHandle,
                    BufferID.Shinobu236BilateralDrsTuning,
                    1,
                    out NativeArray<UpscalerTuningDTO>.ReadOnly tuning))
            {
                FailClosedRuntimeRoute(BilateralDrsUpscalerConstants.FaultVaultUnavailable);
                return dependsOn;
            }

            if (!TryReadVaultBuffer(
                    in _profilesHandle,
                    BufferID.Shinobu236BilateralDrsProfiles,
                    BilateralDrsUpscalerConstants.ProfileCapacity,
                    out NativeArray<UpscalerProfileDTO>.ReadOnly profiles))
            {
                FailClosedRuntimeRoute(BilateralDrsUpscalerConstants.FaultVaultUnavailable);
                return dependsOn;
            }

            ResolutionScaleState scaleState = default;
            bool hasScaleState = _resolutionScaler != null && _resolutionScaler.TryGetScaleState(out scaleState);
            DrsStateDTO mockStateSnapshot = default;
            bool useMock = !hasScaleState;
            if (useMock)
                mockStateSnapshot = BuildMockDrsStateSnapshot(context.Frame);

            CalculateUpscalerParamsJob job;
            job.Parameters = default;
            job.Telemetry = default;
            job.TelemetryCursor = default;
            job.Tuning = tuning;
            job.Profiles = profiles;
            job.ScaleStateSnapshot = scaleState;
            job.MockStateSnapshot = mockStateSnapshot;
            job.SubmittedLowWidth = _submittedLowWidth;
            job.SubmittedLowHeight = _submittedLowHeight;
            job.SubmittedFullWidth = _submittedFullWidth;
            job.SubmittedFullHeight = _submittedFullHeight;
            job.SubmittedJitterX = _submittedJitterX;
            job.SubmittedJitterY = _submittedJitterY;
            job.FallbackQuality01 = ResolveGlobalQualityWeight01();
            job.FrameIndex = context.Frame;
            job.OutputIndex = BilateralDrsUpscalerConstants.PendingParameterIndex;
            job.HasScaleState = hasScaleState ? (byte)1 : (byte)0;
            job.UseMockState = useMock ? (byte)1 : (byte)0;
            job.LastTelemetry = default;
            job.HasLastTelemetry = 0;
            job.LastParameters = default;
            job.HasLastParameters = 0;
            job.Execute();
            if (job.HasLastTelemetry == 0 || job.HasLastParameters == 0)
            {
                FailClosedRuntimeRoute(BilateralDrsUpscalerConstants.FaultVaultUnavailable);
                return dependsOn;
            }

            UpscalerParamsDTO pendingParameters = job.LastParameters;
            if (!TryAcquireVaultWriteBuffer(
                    in _parametersHandle,
                    BufferID.Shinobu236BilateralDrsParams,
                    BilateralDrsUpscalerConstants.ParameterCapacity,
                    out NativeArray<UpscalerParamsDTO> parameters,
                    out IDataVault parametersVault))
            {
                FailClosedRuntimeRoute(BilateralDrsUpscalerConstants.FaultVaultUnavailable);
                return dependsOn;
            }

            try
            {
                parameters[BilateralDrsUpscalerConstants.PendingParameterIndex] = pendingParameters;
            }
            finally
            {
                parametersVault?.ReleaseWriteLock(in _parametersHandle, OwnerSystemId);
            }

            _pendingTelemetryEntry = job.LastTelemetry;
            _pendingTelemetryEntryValid = true;
            _simulationPendingPublish = true;
            return dependsOn;
        }

        private void RecordUpscalerTelemetryOneLock(in UpscalerTelemetryEntry entry)
        {
            if (!TryAcquireVaultWriteBuffer(
                    in _telemetryHandle,
                    BufferID.Shinobu236BilateralDrsTelemetry,
                    BilateralDrsUpscalerConstants.TelemetryCapacity,
                    out NativeArray<UpscalerTelemetryEntry> telemetry,
                    out IDataVault telemetryVault))
            {
                FailClosedRuntimeRoute(BilateralDrsUpscalerConstants.FaultVaultUnavailable);
                return;
            }

            int nextCursor = _telemetryWriteCursor;
            bool wroteTelemetry = false;
            bool faultAfterRelease = false;
            try
            {
                if (!telemetry.IsCreated || telemetry.Length <= 0)
                {
                    faultAfterRelease = true;
                }
                else
                {
                    int cursor = WrapTelemetryCursor(_telemetryWriteCursor, telemetry.Length);
                    telemetry[cursor] = entry;
                    nextCursor = cursor + 1;
                    if (nextCursor >= telemetry.Length)
                        nextCursor = 0;
                    _telemetryWriteCursor = nextCursor;
                    wroteTelemetry = true;
                }
            }
            finally
            {
                telemetryVault?.ReleaseWriteLock(in _telemetryHandle, OwnerSystemId);
            }

            if (faultAfterRelease)
                FailClosedRuntimeRoute(BilateralDrsUpscalerConstants.FaultVaultUnavailable);

            if (wroteTelemetry)
                WriteTelemetryCursorOneLock(nextCursor);
        }

        private void WriteTelemetryCursorOneLock(int nextCursor)
        {
            if (!TryAcquireVaultWriteBuffer(
                    in _telemetryCursorHandle,
                    BufferID.Shinobu236BilateralDrsTelemetryCursor,
                    1,
                    out NativeArray<int> telemetryCursor,
                    out IDataVault telemetryCursorVault))
            {
                FailClosedRuntimeRoute(BilateralDrsUpscalerConstants.FaultVaultUnavailable);
                return;
            }

            bool faultAfterRelease = false;
            try
            {
                if (!telemetryCursor.IsCreated || telemetryCursor.Length <= 0)
                {
                    faultAfterRelease = true;
                }
                else
                {
                    telemetryCursor[0] = WrapTelemetryCursor(nextCursor, BilateralDrsUpscalerConstants.TelemetryCapacity);
                    _telemetryCursorSeeded = true;
                }
            }
            finally
            {
                telemetryCursorVault?.ReleaseWriteLock(in _telemetryCursorHandle, OwnerSystemId);
            }

            if (faultAfterRelease)
                FailClosedRuntimeRoute(BilateralDrsUpscalerConstants.FaultVaultUnavailable);
        }

        private static int WrapTelemetryCursor(int cursor, int capacity)
        {
            if (capacity <= 0)
                return 0;

            int wrapped = cursor % capacity;
            return wrapped < 0 ? wrapped + capacity : wrapped;
        }

        private static DrsStateDTO BuildMockDrsStateSnapshot(uint simulationFrame)
        {
            float phase = (simulationFrame & 1023u) * 0.013f;
            float wave = math.saturate(0.5f + 0.5f * MathLodApproximation.ApproxSinBhaskara(phase));
            float scale = math.lerp(0.4f, 0.72f, wave);

            DrsStateDTO state = default;
            state.CurrentRenderScale = scale;
            state.TargetRenderScale = math.max(0.38f, scale - 0.035f);
            state.UpscalerTypeHash = BilateralDrsUpscalerConstants.UpscalerTypeHash;
            return state;
        }

        private void RunOwnerPostSimulation()
        {
            if (!_simulationPendingPublish && !_pendingTelemetryEntryValid)
                return;

            if (_pendingTelemetryEntryValid)
            {
                UpscalerTelemetryEntry telemetryEntry = _pendingTelemetryEntry;
                _pendingTelemetryEntry = default;
                _pendingTelemetryEntryValid = false;
                RecordUpscalerTelemetryOneLock(in telemetryEntry);
            }

            if (_simulationPendingPublish)
            {
                _simulationPendingPublish = false;
                PublishPendingParameters();
            }
        }

        private void RunOwnerVisualSync(in DispatcherTimingDTO timing)
        {
            if (!_isInitialized)
            {
                if (!TryUsePreparedServiceStateHot(requireConstantBuffers: true))
                    return;
            }

            float safeDeltaTime = math.select(timing.FrameDelta, 0f, !math.isfinite(timing.FrameDelta) || timing.FrameDelta < 0f);
            _presentationTimeSeconds += math.min(safeDeltaTime, 0.25f);
            _presentationFrameIndex++;

            if (_pendingGpuUpload && UploadParametersToGpu())
                _pendingGpuUpload = false;
        }

        private void InitializeServiceForSimulation(bool allowAllocation)
        {
            if (!PrepareServiceState(allowAllocation))
                return;

            bool hasConstantBuffers = HasConstantBuffers();
            _isInitialized = _vaultStateReady && hasConstantBuffers;
            if (!_isInitialized)
                return;

            if (allowAllocation)
                RegisterDispatcherRouteAllOrFail();
        }

        private void InitializeServiceForVisualSync(bool allowAllocation)
        {
            if (!PrepareServiceState(allowAllocation))
                return;

            bool hasConstantBuffers = EnsureConstantBuffers(allowAllocation);
            _isInitialized = _vaultStateReady && hasConstantBuffers;
            if (!_isInitialized)
                return;

            if (allowAllocation)
            {
                RegisterDispatcherRouteAllOrFail();
            }
            else if (!_dispatcherRouteReady)
            {
                _isInitialized = false;
            }
        }

        private bool TryUsePreparedServiceStateHot(bool requireConstantBuffers)
        {
            bool ready =
                _coldDependenciesCached &&
                _vaultStateReady &&
                _dispatcherRouteReady &&
                (!requireConstantBuffers || HasConstantBuffers()) &&
                UpscalerParamsLayoutValidator.Validate();

            _isInitialized = ready;
            if (!ready)
                _resourceRefreshRequested = true;

            return ready;
        }

        private bool PrepareServiceState(bool allowAllocation)
        {
            if (!Application.isPlaying)
                return false;

            if (allowAllocation)
            {
                TryRegisterHotSwapListener();
            }

            if (!_coldDependenciesCached)
            {
                if (!allowAllocation)
                    return false;

                BindDataVaultForLifecycle(GlobalRegistry.DataVault);
                _resolutionScaler = GlobalRegistry.ResolutionScaler;
                _coldDependenciesCached = true;
            }

            EnsureVaultState(allowAllocation);
            bool layoutValid = UpscalerParamsLayoutValidator.Validate();
            if (!layoutValid)
            {
                _lastFaultFlags = BilateralDrsUpscalerConstants.FaultLayout;
                DumpBlackBox();
                return false;
            }

            return true;
        }

        private void EnsureVaultState(bool allowAllocation)
        {
            if (_dataVault == null)
                return;

            bool hasParameters = AcquireOrRefreshOwnedVaultBuffer(
                BufferID.Shinobu236BilateralDrsParams,
                BilateralDrsUpscalerConstants.ParameterCapacity,
                NativeArrayOptions.UninitializedMemory,
                ref _parametersHandle,
                allowAllocation,
                out NativeArray<UpscalerParamsDTO>.ReadOnly _);
            bool hasTuning = AcquireOrRefreshOwnedVaultBuffer(
                BufferID.Shinobu236BilateralDrsTuning,
                1,
                NativeArrayOptions.UninitializedMemory,
                ref _tuningHandle,
                allowAllocation,
                out NativeArray<UpscalerTuningDTO>.ReadOnly _);
            bool hasTelemetry = AcquireOrRefreshOwnedVaultBuffer(
                BufferID.Shinobu236BilateralDrsTelemetry,
                BilateralDrsUpscalerConstants.TelemetryCapacity,
                NativeArrayOptions.UninitializedMemory,
                ref _telemetryHandle,
                allowAllocation,
                out NativeArray<UpscalerTelemetryEntry>.ReadOnly _);
            bool hasTelemetryCursor = AcquireOrRefreshOwnedVaultBuffer(
                BufferID.Shinobu236BilateralDrsTelemetryCursor,
                1,
                NativeArrayOptions.UninitializedMemory,
                ref _telemetryCursorHandle,
                allowAllocation,
                out NativeArray<int>.ReadOnly _);
            bool hasProfiles = AcquireOrRefreshOwnedVaultBuffer(
                BufferID.Shinobu236BilateralDrsProfiles,
                BilateralDrsUpscalerConstants.ProfileCapacity,
                NativeArrayOptions.UninitializedMemory,
                ref _profilesHandle,
                allowAllocation,
                out NativeArray<UpscalerProfileDTO>.ReadOnly _);
            bool hasMockState = AcquireOrRefreshOwnedVaultBuffer(
                BufferID.Shinobu236BilateralDrsMockState,
                1,
                NativeArrayOptions.UninitializedMemory,
                ref _mockStateHandle,
                allowAllocation,
                out NativeArray<DrsStateDTO>.ReadOnly _);

            if (allowAllocation)
            {
                SeedTuningIfNeeded();
                SeedTelemetryIfNeeded();
                SeedTelemetryCursorIfNeeded();
                SeedProfilesIfNeeded();
                SeedMockStateIfNeeded();
            }

            RefreshCachedDebugFlag();
            EnsureCsvScratch(allowAllocation);

            _vaultStateReady = hasParameters &&
                               hasTuning &&
                               hasTelemetry &&
                               hasTelemetryCursor &&
                               hasProfiles &&
                               hasMockState &&
                               _tuningSeeded &&
                               _telemetrySeeded &&
                               _telemetryCursorSeeded &&
                               _profilesSeeded &&
                               _mockStateSeeded;
        }

        private void SeedTuningIfNeeded()
        {
            if (_tuningSeeded)
                return;

            if (!TryAcquireVaultWriteBuffer(
                    in _tuningHandle,
                    BufferID.Shinobu236BilateralDrsTuning,
                    1,
                    out NativeArray<UpscalerTuningDTO> tuning,
                    out IDataVault tuningVault))
            {
                return;
            }

            try
            {
                tuning[0] = CalculateUpscalerParamsJob.DefaultTuning();
                s_edgeMaskDebugEnabled = false;
                _tuningSeeded = true;
            }
            finally
            {
                tuningVault?.ReleaseWriteLock(in _tuningHandle, OwnerSystemId);
            }
        }

        private void RefreshCachedDebugFlag()
        {
            if (!TryReadVaultBuffer(
                    in _tuningHandle,
                    BufferID.Shinobu236BilateralDrsTuning,
                    1,
                    out NativeArray<UpscalerTuningDTO>.ReadOnly tuning))
                return;

            s_edgeMaskDebugEnabled = tuning[0].DebugAndFlags.x > 0.5f;
        }

        private void SeedTelemetryIfNeeded()
        {
            if (_telemetrySeeded)
                return;

            if (!TryAcquireVaultWriteBuffer(
                    in _telemetryHandle,
                    BufferID.Shinobu236BilateralDrsTelemetry,
                    BilateralDrsUpscalerConstants.TelemetryCapacity,
                    out NativeArray<UpscalerTelemetryEntry> telemetry,
                    out IDataVault telemetryVault))
            {
                return;
            }

            try
            {
                for (int i = 0; i < telemetry.Length; i++)
                    telemetry[i] = default;
                _telemetrySeeded = true;
            }
            finally
            {
                telemetryVault?.ReleaseWriteLock(in _telemetryHandle, OwnerSystemId);
            }
        }

        private void SeedTelemetryCursorIfNeeded()
        {
            if (_telemetryCursorSeeded)
            {
                if (TryReadVaultBuffer(
                        in _telemetryCursorHandle,
                        BufferID.Shinobu236BilateralDrsTelemetryCursor,
                        1,
                        out NativeArray<int>.ReadOnly telemetryCursorSnapshot))
                    _telemetryWriteCursor = WrapTelemetryCursor(telemetryCursorSnapshot[0], BilateralDrsUpscalerConstants.TelemetryCapacity);
                return;
            }

            if (!TryAcquireVaultWriteBuffer(
                    in _telemetryCursorHandle,
                    BufferID.Shinobu236BilateralDrsTelemetryCursor,
                    1,
                    out NativeArray<int> telemetryCursor,
                    out IDataVault telemetryCursorVault))
            {
                return;
            }

            try
            {
                telemetryCursor[0] = 0;
                _telemetryWriteCursor = 0;
                _telemetryCursorSeeded = true;
            }
            finally
            {
                telemetryCursorVault?.ReleaseWriteLock(in _telemetryCursorHandle, OwnerSystemId);
            }
        }

        private void SeedProfilesIfNeeded()
        {
            if (_profilesSeeded)
                return;

            if (!TryAcquireVaultWriteBuffer(
                    in _profilesHandle,
                    BufferID.Shinobu236BilateralDrsProfiles,
                    BilateralDrsUpscalerConstants.ProfileCapacity,
                    out NativeArray<UpscalerProfileDTO> profiles,
                    out IDataVault profilesVault))
            {
                return;
            }

            try
            {
                for (int i = 0; i < profiles.Length; i++)
                    profiles[i] = default;
                _profilesSeeded = true;
            }
            finally
            {
                profilesVault?.ReleaseWriteLock(in _profilesHandle, OwnerSystemId);
            }
        }

        private void SeedMockStateIfNeeded()
        {
            if (_mockStateSeeded)
                return;

            if (!TryAcquireVaultWriteBuffer(
                    in _mockStateHandle,
                    BufferID.Shinobu236BilateralDrsMockState,
                    1,
                    out NativeArray<DrsStateDTO> mockState,
                    out IDataVault mockStateVault))
            {
                return;
            }

            try
            {
                DrsStateDTO state = default;
                state.CurrentRenderScale = 0.5f;
                state.TargetRenderScale = 0.5f;
                state.UpscalerTypeHash = BilateralDrsUpscalerConstants.UpscalerTypeHash;
                mockState[0] = state;
                _mockStateSeeded = true;
            }
            finally
            {
                mockStateVault?.ReleaseWriteLock(in _mockStateHandle, OwnerSystemId);
            }
        }

        private bool EnsureCsvScratch(bool allowAllocation)
        {
            if (TryReadVaultBuffer(
                    in _csvScratchHandle,
                    BufferID.Shinobu236BilateralDrsCsvScratch,
                    BilateralDrsUpscalerConstants.CsvScratchBytes,
                    out NativeArray<byte>.ReadOnly _))
                return true;

            if (!allowAllocation)
                return false;

            return AcquireOrRefreshOwnedVaultBuffer(
                BufferID.Shinobu236BilateralDrsCsvScratch,
                BilateralDrsUpscalerConstants.CsvScratchBytes,
                NativeArrayOptions.UninitializedMemory,
                ref _csvScratchHandle,
                allowAllocation,
                out NativeArray<byte>.ReadOnly _);
        }

#if UNITY_EDITOR
        private bool LoadQualityProfilesCsv(string projectRelativePath)
        {
            EnsureVaultState(allowAllocation: true);
            EnsureCsvScratch(allowAllocation: true);
            string fullPath = BuildProjectPath(projectRelativePath);
            Span<byte> csvBytes = stackalloc byte[BilateralDrsUpscalerConstants.CsvScratchBytes];
            int byteCount = LoadFileBytesIntoSpan(fullPath, csvBytes);
            if (byteCount <= 0)
                return false;

            Span<UpscalerProfileDTO> parsedProfiles = stackalloc UpscalerProfileDTO[BilateralDrsUpscalerConstants.ProfileCapacity];
            int parsed = ParseQualityProfiles(csvBytes.Slice(0, byteCount), parsedProfiles);
            if (parsed <= 0)
                return false;

            if (!TryWriteParsedProfiles(parsedProfiles.Slice(0, parsed)))
                return false;

            TryMirrorCsvScratch(csvBytes.Slice(0, byteCount));
            return true;
        }
#endif

        private static void ClearProfiles(NativeArray<UpscalerProfileDTO> profiles)
        {
            if (!profiles.IsCreated)
                return;

            for (int i = 0; i < profiles.Length; i++)
                profiles[i] = default;
        }

#if UNITY_EDITOR
        private static void ClearProfiles(Span<UpscalerProfileDTO> profiles)
        {
            for (int i = 0; i < profiles.Length; i++)
                profiles[i] = default;
        }

        private bool TryWriteParsedProfiles(ReadOnlySpan<UpscalerProfileDTO> parsedProfiles)
        {
            if (parsedProfiles.Length <= 0 || parsedProfiles.Length > BilateralDrsUpscalerConstants.ProfileCapacity)
                return false;

            if (!TryAcquireVaultWriteBuffer(
                    in _profilesHandle,
                    BufferID.Shinobu236BilateralDrsProfiles,
                    BilateralDrsUpscalerConstants.ProfileCapacity,
                    out NativeArray<UpscalerProfileDTO> profiles,
                    out IDataVault profilesVault))
            {
                return false;
            }

            try
            {
                ClearProfiles(profiles);
                for (int i = 0; i < parsedProfiles.Length; i++)
                    profiles[i] = parsedProfiles[i];
                _profilesSeeded = true;
                return true;
            }
            finally
            {
                profilesVault?.ReleaseWriteLock(in _profilesHandle, OwnerSystemId);
            }
        }

        private void TryMirrorCsvScratch(ReadOnlySpan<byte> csvBytes)
        {
            if (!TryAcquireVaultWriteBuffer(
                    in _csvScratchHandle,
                    BufferID.Shinobu236BilateralDrsCsvScratch,
                    BilateralDrsUpscalerConstants.CsvScratchBytes,
                    out NativeArray<byte> csvScratch,
                    out IDataVault csvScratchVault))
            {
                return;
            }

            try
            {
                int length = math.min(csvBytes.Length, csvScratch.Length);
                for (int i = 0; i < length; i++)
                    csvScratch[i] = csvBytes[i];
                for (int i = length; i < csvScratch.Length; i++)
                    csvScratch[i] = 0;
            }
            finally
            {
                csvScratchVault?.ReleaseWriteLock(in _csvScratchHandle, OwnerSystemId);
            }
        }
#endif

        private bool EnsureConstantBuffers(bool allowAllocation)
        {
            if (!_coldSupportsSetConstantBuffer)
            {
                _lastFaultFlags = BilateralDrsUpscalerConstants.FaultConstantBufferUnsupported;
                if (allowAllocation && !_faultDumped)
                {
                    DumpBlackBox();
                    _faultDumped = true;
                }

                return false;
            }

            bool missingBuffer = _constantBufferA == null || !_constantBufferA.IsValid() ||
                                 _constantBufferB == null || !_constantBufferB.IsValid();
            if (missingBuffer && !allowAllocation)
                return false;

            if (_constantBufferA == null || !_constantBufferA.IsValid())
            {
                _constantBufferA = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    BilateralDrsUpscalerConstants.CBufferBytes); // COLD ALLOC: GraphicsBuffer[32B] A - 13KRA CBuffer.
            }

            if (_constantBufferB == null || !_constantBufferB.IsValid())
            {
                _constantBufferB = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    BilateralDrsUpscalerConstants.CBufferBytes); // COLD ALLOC: GraphicsBuffer[32B] B - 13KRA CBuffer.
            }

            return _constantBufferA != null && _constantBufferA.IsValid() &&
                   _constantBufferB != null && _constantBufferB.IsValid();
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _coldSupportsSetConstantBuffer = SystemInfo.supportsSetConstantBuffer;
        }

        private bool HasConstantBuffers()
        {
            return _constantBufferA != null && _constantBufferA.IsValid() &&
                   _constantBufferB != null && _constantBufferB.IsValid();
        }

        private void SetSubmittedRenderDimensions(
            int lowWidth,
            int lowHeight,
            int fullWidth,
            int fullHeight,
            float jitterX,
            float jitterY)
        {
            _submittedLowWidth = lowWidth > 0 ? lowWidth : 0;
            _submittedLowHeight = lowHeight > 0 ? lowHeight : 0;
            _submittedFullWidth = math.max(1, fullWidth);
            _submittedFullHeight = math.max(1, fullHeight);
            _submittedJitterX = math.clamp(jitterX, -1f, 1f);
            _submittedJitterY = math.clamp(jitterY, -1f, 1f);
        }

        private bool PublishPendingParameters()
        {
            UpscalerParamsDTO active = default;
            bool hasActive = false;
            if (!TryAcquireVaultWriteBuffer(
                    in _parametersHandle,
                    BufferID.Shinobu236BilateralDrsParams,
                    BilateralDrsUpscalerConstants.ParameterCapacity,
                    out NativeArray<UpscalerParamsDTO> parameters,
                    out IDataVault parametersVault))
            {
                FailClosedRuntimeRoute(BilateralDrsUpscalerConstants.FaultVaultUnavailable);
                return false;
            }

            try
            {
                parameters[BilateralDrsUpscalerConstants.ActiveParameterIndex] = parameters[BilateralDrsUpscalerConstants.PendingParameterIndex];
                active = parameters[BilateralDrsUpscalerConstants.ActiveParameterIndex];
                hasActive = true;
            }
            finally
            {
                parametersVault?.ReleaseWriteLock(in _parametersHandle, OwnerSystemId);
            }

            if (!hasActive)
            {
                FailClosedRuntimeRoute(BilateralDrsUpscalerConstants.FaultVaultUnavailable);
                return false;
            }

            bool valid = CheckFaultsAndDump(in active);
            _pendingGpuUpload = valid;
            if (!valid)
                InvalidatePublishedParameters();

            return valid;
        }

        private bool UploadParametersToGpu()
        {
            if (!TryReadVaultBuffer(
                    in _parametersHandle,
                    BufferID.Shinobu236BilateralDrsParams,
                    BilateralDrsUpscalerConstants.ParameterCapacity,
                    out NativeArray<UpscalerParamsDTO>.ReadOnly parameters) ||
                !HasConstantBuffers())
            {
                FailClosedRuntimeRoute(BilateralDrsUpscalerConstants.FaultVaultUnavailable);
                return false;
            }

            UpscalerParamsDTO activeParameters = parameters[BilateralDrsUpscalerConstants.ActiveParameterIndex];
            GraphicsBuffer target = _activeConstantBufferIndex == 0 ? _constantBufferA : _constantBufferB;
            _activeConstantBufferIndex ^= 1;
            NativeArray<UpscalerParamsDTO> mapped = default;
            bool locked = false;
            bool uploaded = false;
            try
            {
                mapped = target.LockBufferForWrite<UpscalerParamsDTO>(0, 1);
                locked = mapped.IsCreated && mapped.Length > 0;
                if (locked)
                {
                    mapped[0] = activeParameters;
                    uploaded = true;
                }
            }
            catch (ObjectDisposedException)
            {
                FailClosedRuntimeRoute(BilateralDrsUpscalerConstants.FaultConstantBufferUnsupported);
                return false;
            }
            catch (InvalidOperationException)
            {
                FailClosedRuntimeRoute(BilateralDrsUpscalerConstants.FaultConstantBufferUnsupported);
                return false;
            }
            catch (ArgumentException)
            {
                FailClosedRuntimeRoute(BilateralDrsUpscalerConstants.FaultConstantBufferUnsupported);
                return false;
            }
            catch (NotSupportedException)
            {
                FailClosedRuntimeRoute(BilateralDrsUpscalerConstants.FaultConstantBufferUnsupported);
                return false;
            }
            catch (UnityException)
            {
                FailClosedRuntimeRoute(BilateralDrsUpscalerConstants.FaultConstantBufferUnsupported);
                return false;
            }
            finally
            {
                if (locked && !TryUnlockConstantBuffer(target))
                    uploaded = false;
            }

            if (!uploaded)
            {
                FailClosedRuntimeRoute(BilateralDrsUpscalerConstants.FaultConstantBufferUnsupported);
                return false;
            }

            _activeConstantBuffer = target;
            s_publishedConstantBuffer = target;
            s_publishedConstantBufferFrameIndex = _presentationFrameIndex;
            s_lastPublishedParameters = activeParameters;
            s_hasPublishedParameters = true;
            return true;
        }

        private static bool TryUnlockConstantBuffer(GraphicsBuffer target)
        {
            try
            {
                target.UnlockBufferAfterWrite<UpscalerParamsDTO>(1);
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (UnityException)
            {
                return false;
            }
        }

        private bool CheckFaultsAndDump(in UpscalerParamsDTO parameters)
        {
            bool finite = math.all(math.isfinite(parameters.ResolutionParams)) &&
                          math.all(math.isfinite(parameters.FilterParams));
            if (finite)
            {
                _faultDumped = false;
                _lastFaultFlags = 0u;
                return true;
            }

            _lastFaultFlags = BilateralDrsUpscalerConstants.FaultNonFinite;
            if (!_faultDumped)
            {
                DumpBlackBox();
                _faultDumped = true;
            }

            return false;
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(quality, 1f, !math.isfinite(quality)));
        }

        private bool TryReadVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   requiredLength > 0 &&
                   IsOwnedVaultHandle(in handle, expectedBufferId) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   !vault.IsCompactionFenceActive &&
                   buffer.Length >= requiredLength;
        }

        private bool TryAcquireVaultWriteBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T> buffer,
            out IDataVault writeVault) where T : struct
        {
            buffer = default;
            writeVault = null;
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                requiredLength <= 0 ||
                !IsOwnedVaultHandle(in handle, expectedBufferId) ||
                !vault.TryAcquireWriteLock(in handle, OwnerSystemId, out buffer))
            {
                return false;
            }

            bool releaseOnExit = true;
            try
            {
                if (vault.IsCompactionFenceActive ||
                    !buffer.IsCreated ||
                    buffer.Length < requiredLength)
                {
                    buffer = default;
                    return false;
                }

                writeVault = vault;
                releaseOnExit = false;
                return true;
            }
            finally
            {
                if (releaseOnExit)
                    vault.ReleaseWriteLock(in handle, OwnerSystemId);
            }
        }

        private bool AcquireOrRefreshOwnedVaultBuffer<T>(
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            ref VaultGenerationHandle<T> handle,
            bool allowAllocation,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
            {
                handle = default;
                return false;
            }

            if (IsOwnedVaultHandle(in handle, bufferId) &&
                vault.TryReadOnlyHandle(in handle, out buffer) &&
                !vault.IsCompactionFenceActive &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (!allowAllocation || vault.IsAllocationLocked)
                return false;

            if (IsOwnedVaultHandle(in handle, bufferId))
                ReleaseVaultHandle(vault, ref handle, bufferId);
            else
                handle = default;

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, OwnerSystemId, options);
            return IsOwnedVaultHandle(in handle, bufferId) &&
                   !vault.IsCompactionFenceActive &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   !vault.IsCompactionFenceActive &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsOwnedVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   handle.SystemID == (uint)OwnerSystemId &&
                   handle.Generation != 0u;
        }

        private static void ReleaseVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID expectedBufferId) where T : struct
        {
            if (vault != null && IsOwnedVaultHandle(in handle, expectedBufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void RegisterDispatcherRouteAllOrFail()
        {
            if (_dispatcherRouteReady)
                return;

            if (_registeredPreSimulationDispatcher &&
                _registeredSimulationDispatcher &&
                _registeredPostSimulationDispatcher &&
                _registeredVisualSyncDispatcher)
            {
                _dispatcherRouteReady = true;
                return;
            }

            if (_simulationBridge == null)
                _simulationBridge = new SimulationKernelBridge(this); // COLD ALLOC: IDispatcherSystem[1] - 13KRA scheduled Burst kernel bridge.
            if (_postSimulationBridge == null)
                _postSimulationBridge = new PostSimulationPublishBridge(this); // COLD ALLOC: IDispatcherSystem[1] - 13KRA post-simulation DTO publisher.
            if (_visualSyncBridge == null)
                _visualSyncBridge = new VisualSyncUploadBridge(this); // COLD ALLOC: IDispatcherSystem[1] - 13KRA VisualSync upload bridge.

            bool preRegistered = GlobalRegistry.TryRegisterDispatcherSystem(this);
            _registeredPreSimulationDispatcher = preRegistered;
            if (!preRegistered)
            {
                FailClosedRuntimeRoute();
                return;
            }

            bool simulationRegistered = GlobalRegistry.TryRegisterDispatcherSystem(_simulationBridge);
            _registeredSimulationDispatcher = simulationRegistered;
            if (!simulationRegistered)
            {
                UnregisterPartialDispatcherRoute();
                FailClosedRuntimeRoute();
                return;
            }

            bool postSimulationRegistered = GlobalRegistry.TryRegisterDispatcherSystem(_postSimulationBridge);
            _registeredPostSimulationDispatcher = postSimulationRegistered;
            if (!postSimulationRegistered)
            {
                UnregisterPartialDispatcherRoute();
                FailClosedRuntimeRoute();
                return;
            }

            _registeredVisualSyncDispatcher = GlobalRegistry.TryRegisterDispatcherSystem(_visualSyncBridge);
            if (!_registeredVisualSyncDispatcher)
            {
                UnregisterPartialDispatcherRoute();
                FailClosedRuntimeRoute();
                return;
            }

            _dispatcherRouteReady = true;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener)
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

        private void TryRegisterSlowTick()
        {
            if (_registeredSlowTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
        }

        private void TryUnregisterSlowTick()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            _registeredSlowTick = false;
        }

        private void TryUnregisterPreSimulationDispatcher()
        {
            if (!_registeredPreSimulationDispatcher)
                return;

            GlobalRegistry.UnregisterDispatcherSystem(this);
            _registeredPreSimulationDispatcher = false;
            _dispatcherRouteReady = false;
        }

        private void TryUnregisterSimulationDispatcher()
        {
            if (!_registeredSimulationDispatcher)
                return;

            GlobalRegistry.UnregisterDispatcherSystem(_simulationBridge);
            _registeredSimulationDispatcher = false;
            _dispatcherRouteReady = false;
        }

        private void TryUnregisterPostSimulationDispatcher()
        {
            if (!_registeredPostSimulationDispatcher)
                return;

            GlobalRegistry.UnregisterDispatcherSystem(_postSimulationBridge);
            _registeredPostSimulationDispatcher = false;
            _dispatcherRouteReady = false;
        }

        private void TryUnregisterVisualSyncDispatcher()
        {
            if (!_registeredVisualSyncDispatcher)
                return;

            GlobalRegistry.UnregisterDispatcherSystem(_visualSyncBridge);
            _registeredVisualSyncDispatcher = false;
            _dispatcherRouteReady = false;
        }

        private void UnregisterPartialDispatcherRoute()
        {
            TryUnregisterVisualSyncDispatcher();
            TryUnregisterPostSimulationDispatcher();
            TryUnregisterSimulationDispatcher();
            TryUnregisterPreSimulationDispatcher();
        }

        private void FailClosedRuntimeRoute(uint faultFlags = 0u)
        {
            if (faultFlags != 0u)
                RequestFaultDump(faultFlags);

            _isInitialized = false;
            _vaultStateReady = false;
            _simulationPendingPublish = false;
            _pendingTelemetryEntryValid = false;
            _pendingTelemetryEntry = default;
            _pendingGpuUpload = false;
            _dispatcherRouteReady = false;
            InvalidatePublishedParameters();
        }

        private void RequestFaultDump(uint faultFlags)
        {
            _lastFaultFlags = faultFlags;
            if (_faultDumped)
                return;

            DumpBlackBox();
            _faultDumped = true;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    if (_dataVault == null && previousService is IDataVault previousVault)
                        ReleaseAllVaultHandles(previousVault);

                    BindDataVaultForLifecycle(currentService as IDataVault);
                    if (_dataVault != null)
                    {
                        InitializeServiceForVisualSync(allowAllocation: true);
                    }
                    break;
                case GlobalRegistryServiceSlot.ResolutionScalerService:
                    _resolutionScaler = currentService as IResolutionScalerService;
                    break;
            }
        }

        private void ReleaseAllVaultHandles(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref _parametersHandle, BufferID.Shinobu236BilateralDrsParams);
            ReleaseVaultHandle(vault, ref _tuningHandle, BufferID.Shinobu236BilateralDrsTuning);
            ReleaseVaultHandle(vault, ref _telemetryHandle, BufferID.Shinobu236BilateralDrsTelemetry);
            ReleaseVaultHandle(vault, ref _telemetryCursorHandle, BufferID.Shinobu236BilateralDrsTelemetryCursor);
            ReleaseVaultHandle(vault, ref _profilesHandle, BufferID.Shinobu236BilateralDrsProfiles);
            ReleaseVaultHandle(vault, ref _csvScratchHandle, BufferID.Shinobu236BilateralDrsCsvScratch);
            ReleaseVaultHandle(vault, ref _mockStateHandle, BufferID.Shinobu236BilateralDrsMockState);
        }

        private void BindDataVaultForLifecycle(IDataVault nextVault)
        {
            if (ReferenceEquals(_dataVault, nextVault))
                return;

            IDataVault previousVault = _dataVault;
            if (previousVault != null)
                ReleaseAllVaultHandles(previousVault);

            _dataVault = nextVault;
            ResetVaultSeedState();
        }

        private void ResetVaultSeedState()
        {
            _vaultStateReady = false;
            _tuningSeeded = false;
            _telemetrySeeded = false;
            _telemetryCursorSeeded = false;
            _profilesSeeded = false;
            _mockStateSeeded = false;
            _simulationPendingPublish = false;
            _pendingTelemetryEntryValid = false;
            _pendingTelemetryEntry = default;
            _pendingGpuUpload = false;
            _telemetryWriteCursor = 0;
            _dispatcherRouteReady = false;
            _faultDumped = false;
            _lastFaultFlags = 0u;
            InvalidatePublishedParameters();
            s_edgeMaskDebugEnabled = false;
        }

        private void ShutdownServiceState()
        {
            TryUnregisterSlowTick();
            TryUnregisterPreSimulationDispatcher();
            TryUnregisterSimulationDispatcher();
            TryUnregisterPostSimulationDispatcher();
            TryUnregisterVisualSyncDispatcher();
            TryUnregisterHotSwapListener();
            if (ReferenceEquals(s_runtimeInstance, this))
                s_runtimeInstance = null;
            if (ReferenceEquals(s_publishedConstantBuffer, _constantBufferA) ||
                ReferenceEquals(s_publishedConstantBuffer, _constantBufferB))
            {
                InvalidatePublishedParameters();
            }

            _constantBufferA?.Release();
            _constantBufferB?.Release();
            _constantBufferA = null;
            _constantBufferB = null;
            _activeConstantBuffer = null;
            _simulationBridge = null;
            _postSimulationBridge = null;
            _visualSyncBridge = null;
            _activeConstantBufferIndex = 0;

            ReleaseAllVaultHandles(_dataVault);

            ResetVaultSeedState();
            _coldDependenciesCached = false;
            _isInitialized = false;
            _resourceRefreshRequested = false;
            _dataVault = null;
            _resolutionScaler = null;
        }

#if UNITY_EDITOR
        private static int LoadFileBytesIntoSpan(string fullPath, Span<byte> csvBytes)
        {
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath) || csvBytes.Length <= 0)
                return 0;

            int capacity = csvBytes.Length;
            int total = 0;
            Span<byte> block = stackalloc byte[256];
            try
            {
                using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    while (total < capacity)
                    {
                        int read = stream.Read(block);
                        if (read <= 0)
                            break;

                        int copy = math.min(read, capacity - total);
                        block.Slice(0, copy).CopyTo(csvBytes.Slice(total, copy));
                        total += copy;
                        if (copy < read)
                            break;
                    }
                }
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
            catch (ObjectDisposedException)
            {
                return 0;
            }
            catch (InvalidOperationException)
            {
                return 0;
            }
            catch (ArgumentException)
            {
                return 0;
            }
            catch (NotSupportedException)
            {
                return 0;
            }

            return total;
        }

        private static int ParseQualityProfiles(ReadOnlySpan<byte> csvBytes, Span<UpscalerProfileDTO> profiles)
        {
            int rowStart = 0;
            int write = 0;
            for (int i = 0; i <= csvBytes.Length; i++)
            {
                bool end = i == csvBytes.Length;
                if (!end && csvBytes[i] != (byte)'\n')
                    continue;

                int rowEnd = i;
                if (rowEnd > rowStart && csvBytes[rowEnd - 1] == (byte)'\r')
                    rowEnd--;
                ReadOnlySpan<byte> row = csvBytes.Slice(rowStart, rowEnd - rowStart);
                if (TryParseProfileRow(row, out UpscalerProfileDTO profile))
                {
                    if (write >= profiles.Length)
                    {
                        ClearProfiles(profiles);
                        return 0;
                    }

                    profiles[write] = profile;
                    write++;
                }
                else if (!IsSkippableProfileRow(row))
                {
                    ClearProfiles(profiles);
                    return 0;
                }

                rowStart = i + 1;
            }

            if (write <= 0)
                return 0;

            for (int i = write; i < profiles.Length; i++)
                profiles[i] = default;
            return write;
        }

        private static bool IsSkippableProfileRow(ReadOnlySpan<byte> row)
        {
            ReadOnlySpan<byte> trimmed = TrimAscii(row);
            if (trimmed.Length <= 0 || trimmed[0] == (byte)'#')
                return true;

            int comma = trimmed.IndexOf((byte)',');
            ReadOnlySpan<byte> firstToken = comma >= 0 ? trimmed.Slice(0, comma) : trimmed;
            return IsHeaderToken(TrimAscii(firstToken));
        }

        private static bool TryParseProfileRow(ReadOnlySpan<byte> row, out UpscalerProfileDTO profile)
        {
            profile = default;
            if (row.Length <= 0 || row[0] == (byte)'#')
                return false;

            int tokenStart = 0;
            int tokenIndex = 0;
            uint hash = 0u;
            float minScale = 0f;
            float maxScale = 1f;
            float depthWeight = BilateralDrsUpscalerConstants.DefaultDepthWeight;
            float colorWeight = BilateralDrsUpscalerConstants.DefaultColorWeight;
            float minRadius = BilateralDrsUpscalerConstants.DefaultMinRadiusPixels;
            float maxRadius = BilateralDrsUpscalerConstants.DefaultMaxRadiusPixels;
            float qualityBias = 0f;
            for (int i = 0; i <= row.Length; i++)
            {
                bool end = i == row.Length;
                if (!end && row[i] != (byte)',')
                    continue;

                ReadOnlySpan<byte> token = TrimAscii(row.Slice(tokenStart, i - tokenStart));
                if (tokenIndex >= QualityProfileCsvColumnCount)
                    return false;

                if (tokenIndex == 0)
                {
                    if (IsHeaderToken(token))
                        return false;
                    hash = Fnv1aLower(token);
                }
                else
                {
                    if (!TryParseFloat(token, out float value))
                        return false;
                    switch (tokenIndex)
                    {
                        case 1:
                            minScale = value;
                            break;
                        case 2:
                            maxScale = value;
                            break;
                        case 3:
                            depthWeight = value;
                            break;
                        case 4:
                            colorWeight = value;
                            break;
                        case 5:
                            minRadius = value;
                            break;
                        case 6:
                            maxRadius = value;
                            break;
                        case 7:
                            qualityBias = value;
                            break;
                    }
                }

                tokenIndex++;
                tokenStart = i + 1;
            }

            if (hash == 0u || tokenIndex != QualityProfileCsvColumnCount)
                return false;

            profile.ProfileHash = hash;
            profile.MinScale01 = math.saturate(minScale);
            profile.MaxScale01 = math.max(profile.MinScale01, math.saturate(maxScale));
            profile.QualityBias01 = math.clamp(qualityBias, -1f, 1f);
            profile.FilterParams = new float4(
                math.max(1f, depthWeight),
                math.max(0.001f, colorWeight),
                math.max(0.25f, minRadius),
                math.max(math.max(0.25f, minRadius), maxRadius));
            return true;
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> token)
        {
            int start = 0;
            int end = token.Length;
            while (start < end && token[start] <= 32)
                start++;
            if (end - start >= 3 &&
                token[start] == 0xEF &&
                token[start + 1] == 0xBB &&
                token[start + 2] == 0xBF)
            {
                start += 3;
                while (start < end && token[start] <= 32)
                    start++;
            }

            while (end > start && token[end - 1] <= 32)
                end--;
            return token.Slice(start, end - start);
        }

        private static uint Fnv1aLower(ReadOnlySpan<byte> token)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < token.Length; i++)
                {
                    byte value = ToLower(token[i]);
                    hash = (hash ^ value) * 16777619u;
                }

                return hash;
            }
        }

        private static bool IsHeaderToken(ReadOnlySpan<byte> token)
        {
            if (token.Length == 4)
            {
                return ToLower(token[0]) == (byte)'n' &&
                       ToLower(token[1]) == (byte)'a' &&
                       ToLower(token[2]) == (byte)'m' &&
                       ToLower(token[3]) == (byte)'e';
            }

            if (token.Length == 7)
            {
                return ToLower(token[0]) == (byte)'p' &&
                       ToLower(token[1]) == (byte)'r' &&
                       ToLower(token[2]) == (byte)'o' &&
                       ToLower(token[3]) == (byte)'f' &&
                       ToLower(token[4]) == (byte)'i' &&
                       ToLower(token[5]) == (byte)'l' &&
                       ToLower(token[6]) == (byte)'e';
            }

            return false;
        }

        private static byte ToLower(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> token, out float value)
        {
            value = 0f;
            if (token.Length <= 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (token[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }

            float integer = 0f;
            bool anyDigit = false;
            while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
            {
                anyDigit = true;
                integer = integer * 10f + (token[index] - (byte)'0');
                index++;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (index < token.Length && token[index] == (byte)'.')
            {
                index++;
                while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
                {
                    anyDigit = true;
                    fraction = fraction * 10f + (token[index] - (byte)'0');
                    divisor *= 10f;
                    index++;
                }
            }

            if (!anyDigit || index != token.Length)
                return false;

            value = sign * (integer + fraction / divisor);
            return math.isfinite(value);
        }

        private static string BuildProjectPath(string projectRelativePath)
        {
            if (string.IsNullOrEmpty(projectRelativePath) ||
                Path.IsPathRooted(projectRelativePath) ||
                projectRelativePath.IndexOf("..", StringComparison.Ordinal) >= 0)
            {
                return null;
            }

            string root = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(root))
                return null;

            return Path.Combine(root, projectRelativePath);
        }
#endif

        private void DumpBlackBox()
        {
            if (!TryReadTelemetryDumpShape(out int entryCount, out int telemetryWriteCursor))
                return;

            if (!TryReadVaultBuffer(
                    in _telemetryHandle,
                    BufferID.Shinobu236BilateralDrsTelemetry,
                    BilateralDrsUpscalerConstants.TelemetryCapacity,
                    out NativeArray<UpscalerTelemetryEntry>.ReadOnly telemetry))
            {
                return;
            }

            WriteBlackBoxDump(entryCount, telemetryWriteCursor, _lastFaultFlags, telemetry);
        }

        private static unsafe void WriteBlackBoxDump(
            int entryCount,
            int telemetryWriteCursor,
            uint faultFlags,
            NativeArray<UpscalerTelemetryEntry>.ReadOnly telemetry)
        {
            int entrySize = UnsafeUtility.SizeOf<UpscalerTelemetryEntry>();
            if (entrySize != BilateralDrsUpscalerConstants.TelemetryBytes ||
                !telemetry.IsCreated ||
                telemetry.Length <= 0)
            {
                return;
            }

            int count = math.min(math.min(entryCount, telemetry.Length), BilateralDrsUpscalerConstants.TelemetryCapacity);
            if (count <= 0)
                return;

            int byteCount = BlackBoxDumpHeaderBytes + count * entrySize;
            NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                byteCount,
                nameof(HectonBilateralDrsUpscalerRuntime),
                BlackBoxDumpPayloadLabel,
                NativeArrayOptions.UninitializedMemory);
            try
            {
                payload = H8Memory.Allocate<byte>(
                    byteCount,
                    OwnerSystemId,
                    Allocator.Temp,
                    NativeArrayOptions.UninitializedMemory);
                if (!payload.IsCreated)
                    return;

                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                WriteUInt32LittleEndian(target, 0, BlackBoxDumpMagic);
                WriteUInt32LittleEndian(target, 4, BlackBoxDumpVersion);
                WriteUInt32LittleEndian(target, 8, faultFlags);
                WriteInt32LittleEndian(target, 12, telemetryWriteCursor);
                WriteInt32LittleEndian(target, 16, count);
                WriteInt32LittleEndian(target, 20, entrySize);
                WriteUInt32LittleEndian(target, 24, BilateralDrsUpscalerConstants.StateHash);
                WriteUInt32LittleEndian(target, 28, 0u);

                int start = telemetryWriteCursor - count;
                while (start < 0)
                    start += telemetry.Length;
                if (start >= telemetry.Length)
                    start %= telemetry.Length;

                int offset = BlackBoxDumpHeaderBytes;
                for (int i = 0; i < count; i++)
                {
                    int slot = start + i;
                    if (slot >= telemetry.Length)
                        slot -= telemetry.Length;

                    UpscalerTelemetryEntry entry = telemetry[slot];
                    UnsafeUtility.MemCpy(target + offset, &entry, entrySize);
                    offset += entrySize;
                }

                NativeFaultDumpWriter.TryWriteAll(BlackBoxDumpPath, payload, byteCount);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(HectonBilateralDrsUpscalerRuntime),
                    BlackBoxDumpPayloadLabel);
            }
        }

        private static unsafe void WriteInt32LittleEndian(byte* destination, int offset, int value)
        {
            WriteUInt32LittleEndian(destination, offset, unchecked((uint)value));
        }

        private static unsafe void WriteUInt32LittleEndian(byte* destination, int offset, uint value)
        {
            destination[offset] = unchecked((byte)value);
            destination[offset + 1] = unchecked((byte)(value >> 8));
            destination[offset + 2] = unchecked((byte)(value >> 16));
            destination[offset + 3] = unchecked((byte)(value >> 24));
        }

        private bool TryReadTelemetryDumpShape(out int entryCount, out int telemetryWriteCursor)
        {
            entryCount = 0;
            telemetryWriteCursor = 0;
            if (!TryReadVaultBuffer(
                    in _telemetryHandle,
                    BufferID.Shinobu236BilateralDrsTelemetry,
                    BilateralDrsUpscalerConstants.TelemetryCapacity,
                    out NativeArray<UpscalerTelemetryEntry>.ReadOnly telemetry))
            {
                return false;
            }

            entryCount = math.min(telemetry.Length, BilateralDrsUpscalerConstants.TelemetryCapacity);
            if (TryReadVaultBuffer(
                    in _telemetryCursorHandle,
                    BufferID.Shinobu236BilateralDrsTelemetryCursor,
                    1,
                    out NativeArray<int>.ReadOnly telemetryCursor))
                telemetryWriteCursor = telemetryCursor[0];
            return entryCount > 0;
        }

    }
}
