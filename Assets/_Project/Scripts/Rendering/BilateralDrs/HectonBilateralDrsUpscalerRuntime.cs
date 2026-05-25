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
    public sealed unsafe class HectonBilateralDrsUpscalerRuntime : MonoBehaviour, IDispatcherSystem, IGlobalRegistryHotSwapListener
    {
        private const SystemID OwnerSystemId = SystemID.GraphicsScalability;
        private const uint SimulationSystemHash = 0x4232534Du; // B2SM
        private const uint PostSimulationSystemHash = 0x4232504Fu; // B2PO
        private const uint VisualSyncSystemHash = 0x42325653u; // B2VS
        private const int QualityProfileCsvColumnCount = 8;
        private const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_236.bin";

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

            public void PreSimulationTick(in DispatcherTimingDTO timing)
            {
            }

            public JobHandle ScheduleSimulation(
                in DispatcherTimingDTO timing,
                in DispatcherJobContext context,
                JobHandle dependsOn)
            {
                return _owner != null
                    ? _owner.ScheduleOwnerSimulation(in timing, in context, dependsOn)
                    : dependsOn;
            }

            public void PostSimulationTick(in DispatcherTimingDTO timing)
            {
            }

            public void VisualSyncTick(in DispatcherTimingDTO timing)
            {
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

            public void PreSimulationTick(in DispatcherTimingDTO timing)
            {
            }

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

            public void VisualSyncTick(in DispatcherTimingDTO timing)
            {
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

            public void PreSimulationTick(in DispatcherTimingDTO timing)
            {
            }

            public JobHandle ScheduleSimulation(
                in DispatcherTimingDTO timing,
                in DispatcherJobContext context,
                JobHandle dependsOn)
            {
                return dependsOn;
            }

            public void PostSimulationTick(in DispatcherTimingDTO timing)
            {
            }

            public void VisualSyncTick(in DispatcherTimingDTO timing)
            {
                _owner?.RunOwnerVisualSync();
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
        private bool _dispatcherRouteReady;
        private bool _registeredHotSwapListener;
        private bool _coldDependenciesCached;
        private bool _isInitialized;
        private bool _vaultStateReady;
        private bool _tuningSeeded;
        private bool _telemetrySeeded;
        private bool _telemetryCursorSeeded;
        private bool _profilesSeeded;
        private bool _mockStateSeeded;
        private bool _simulationKernelScheduled;
        private bool _pendingGpuUpload;
        private bool _faultDumped;
        private uint _lastFaultFlags;
        private uint _presentationFrameIndex;
        private float _presentationTimeSeconds;
        private int _submittedLowWidth;
        private int _submittedLowHeight;
        private int _submittedFullWidth;
        private int _submittedFullHeight;
        private float _submittedJitterX;
        private float _submittedJitterY;
        private string _blackBoxDumpPath;
        private string _blackBoxDumpDirectory;

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

            GameObject runtimeRoot = new GameObject("[HectonBilateralDrsUpscalerRuntime]"); // COLD ALLOC: GameObject[1] - scene-local SHINOBU_236 render-owner bootstrap.
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

            if (!runtime.TryResolveVaultBuffer(in runtime._tuningHandle, 1, out NativeArray<UpscalerTuningDTO> tuningArray))
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

            runtime.EnsureVaultState();
            if (!runtime.TryResolveVaultBuffer(in runtime._tuningHandle, 1, out NativeArray<UpscalerTuningDTO> tuningArray))
                return false;

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
            return true;
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
            InitializeService();
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
            RunOwnerPreSimulation(timing.FrameDelta);
        }

        public JobHandle ScheduleSimulation(
            in DispatcherTimingDTO timing,
            in DispatcherJobContext context,
            JobHandle dependsOn)
        {
            return dependsOn;
        }

        public void PostSimulationTick(in DispatcherTimingDTO timing)
        {
        }

        public void VisualSyncTick(in DispatcherTimingDTO timing)
        {
        }

        private void RunOwnerPreSimulation(float deltaTime)
        {
            if (!_isInitialized)
            {
                InitializeService();
                if (!_isInitialized)
                    return;
            }

            float safeDeltaTime = math.select(deltaTime, 0f, !math.isfinite(deltaTime) || deltaTime < 0f);
            _presentationTimeSeconds += math.min(safeDeltaTime, 0.25f);
            _presentationFrameIndex++;

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

            if (!TryResolveVaultBuffer(in _parametersHandle, BilateralDrsUpscalerConstants.ParameterCapacity, out NativeArray<UpscalerParamsDTO> parameters))
            {
                FailClosedRuntimeRoute(BilateralDrsUpscalerConstants.FaultVaultUnavailable);
                return dependsOn;
            }

            if (!TryResolveVaultBuffer(in _tuningHandle, 1, out NativeArray<UpscalerTuningDTO> tuning) ||
                !TryResolveVaultBuffer(in _telemetryHandle, BilateralDrsUpscalerConstants.TelemetryCapacity, out NativeArray<UpscalerTelemetryEntry> telemetry) ||
                !TryResolveVaultBuffer(in _telemetryCursorHandle, 1, out NativeArray<int> telemetryCursor) ||
                !TryResolveVaultBuffer(in _profilesHandle, BilateralDrsUpscalerConstants.ProfileCapacity, out NativeArray<UpscalerProfileDTO> profiles))
            {
                FailClosedRuntimeRoute(BilateralDrsUpscalerConstants.FaultVaultUnavailable);
                return dependsOn;
            }

            ResolutionScaleState scaleState = default;
            bool hasScaleState = _resolutionScaler != null && _resolutionScaler.TryGetScaleState(out scaleState);
            NativeArray<DrsStateDTO> mockState = default;
            bool useMock = !hasScaleState && TryResolveVaultBuffer(in _mockStateHandle, 1, out mockState);

            JobHandle handle = dependsOn;
            if (useMock)
            {
                GenerateMockDrsStateJob mockJob;
                mockJob.MockState = mockState;
                mockJob.TimeSeconds = _presentationTimeSeconds;
                mockJob.FrameIndex = _presentationFrameIndex;
                handle = mockJob.Schedule(handle);
            }

            CalculateUpscalerParamsJob job;
            job.Parameters = parameters;
            job.Telemetry = telemetry;
            job.TelemetryCursor = telemetryCursor;
            job.Tuning = tuning;
            job.Profiles = profiles;
            job.MockState = mockState;
            job.ScaleStateSnapshot = scaleState;
            job.MockStateSnapshot = default;
            job.SubmittedLowWidth = _submittedLowWidth;
            job.SubmittedLowHeight = _submittedLowHeight;
            job.SubmittedFullWidth = _submittedFullWidth;
            job.SubmittedFullHeight = _submittedFullHeight;
            job.SubmittedJitterX = _submittedJitterX;
            job.SubmittedJitterY = _submittedJitterY;
            job.FallbackQuality01 = ResolveGlobalQualityWeight01();
            job.FrameIndex = _presentationFrameIndex != 0u ? _presentationFrameIndex : context.Frame;
            job.OutputIndex = BilateralDrsUpscalerConstants.PendingParameterIndex;
            job.HasScaleState = hasScaleState ? (byte)1 : (byte)0;
            job.UseMockState = useMock ? (byte)1 : (byte)0;
            handle = job.Schedule(handle);
            _simulationKernelScheduled = true;
            H8Memory.RegisterActiveJob(OwnerSystemId, handle);
            return handle;
        }

        private void RunOwnerPostSimulation()
        {
            if (!_simulationKernelScheduled)
                return;

            _simulationKernelScheduled = false;
            PublishPendingParameters();
        }

        private void RunOwnerVisualSync()
        {
            if (!_isInitialized)
            {
                InitializeService();
                if (!_isInitialized)
                    return;
            }

            if (_pendingGpuUpload && UploadParametersToGpu())
                _pendingGpuUpload = false;
        }

        private void InitializeService()
        {
            if (!Application.isPlaying)
                return;

            EnsureBlackBoxDumpPathCold();
            TryRegisterHotSwapListener();
            if (!_coldDependenciesCached)
            {
                _dataVault = GlobalRegistry.DataVault;
                _resolutionScaler = GlobalRegistry.ResolutionScaler;
                _coldDependenciesCached = true;
            }

            EnsureVaultState();
            bool layoutValid = UpscalerParamsLayoutValidator.Validate();
            if (!layoutValid)
            {
                _lastFaultFlags = BilateralDrsUpscalerConstants.FaultLayout;
                DumpBlackBox();
                return;
            }

            bool hasConstantBuffers = EnsureConstantBuffers();
            _isInitialized = _vaultStateReady && hasConstantBuffers;
            if (!_isInitialized)
                return;

            RegisterDispatcherRouteAllOrFail();
        }

        private void EnsureVaultState()
        {
            if (_dataVault == null)
                return;

            bool hasParameters = AcquireOrRefreshOwnedVaultBuffer(
                BufferID.Shinobu236BilateralDrsParams,
                BilateralDrsUpscalerConstants.ParameterCapacity,
                NativeArrayOptions.UninitializedMemory,
                ref _parametersHandle,
                out NativeArray<UpscalerParamsDTO> _);
            bool hasTuning = AcquireOrRefreshOwnedVaultBuffer(
                BufferID.Shinobu236BilateralDrsTuning,
                1,
                NativeArrayOptions.UninitializedMemory,
                ref _tuningHandle,
                out NativeArray<UpscalerTuningDTO> tuning);
            bool hasTelemetry = AcquireOrRefreshOwnedVaultBuffer(
                BufferID.Shinobu236BilateralDrsTelemetry,
                BilateralDrsUpscalerConstants.TelemetryCapacity,
                NativeArrayOptions.UninitializedMemory,
                ref _telemetryHandle,
                out NativeArray<UpscalerTelemetryEntry> telemetry);
            bool hasTelemetryCursor = AcquireOrRefreshOwnedVaultBuffer(
                BufferID.Shinobu236BilateralDrsTelemetryCursor,
                1,
                NativeArrayOptions.UninitializedMemory,
                ref _telemetryCursorHandle,
                out NativeArray<int> telemetryCursor);
            bool hasProfiles = AcquireOrRefreshOwnedVaultBuffer(
                BufferID.Shinobu236BilateralDrsProfiles,
                BilateralDrsUpscalerConstants.ProfileCapacity,
                NativeArrayOptions.UninitializedMemory,
                ref _profilesHandle,
                out NativeArray<UpscalerProfileDTO> profiles);
            bool hasMockState = AcquireOrRefreshOwnedVaultBuffer(
                BufferID.Shinobu236BilateralDrsMockState,
                1,
                NativeArrayOptions.UninitializedMemory,
                ref _mockStateHandle,
                out NativeArray<DrsStateDTO> mockState);

            SeedTuningIfNeeded(tuning);
            SeedTelemetryIfNeeded(telemetry);
            SeedTelemetryCursorIfNeeded(telemetryCursor);
            SeedProfilesIfNeeded(profiles);
            SeedMockStateIfNeeded(mockState);
            RefreshCachedDebugFlag(tuning);
            EnsureCsvScratch();

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

        private void SeedTuningIfNeeded(NativeArray<UpscalerTuningDTO> tuning)
        {
            if (_tuningSeeded || !tuning.IsCreated || tuning.Length < 1)
                return;

            tuning[0] = CalculateUpscalerParamsJob.DefaultTuning();
            s_edgeMaskDebugEnabled = false;
            _tuningSeeded = true;
        }

        private static void RefreshCachedDebugFlag(NativeArray<UpscalerTuningDTO> tuning)
        {
            if (!tuning.IsCreated || tuning.Length < 1)
                return;

            s_edgeMaskDebugEnabled = tuning[0].DebugAndFlags.x > 0.5f;
        }

        private void SeedTelemetryIfNeeded(NativeArray<UpscalerTelemetryEntry> telemetry)
        {
            if (_telemetrySeeded || !telemetry.IsCreated)
                return;

            for (int i = 0; i < telemetry.Length; i++)
                telemetry[i] = default;
            _telemetrySeeded = true;
        }

        private void SeedTelemetryCursorIfNeeded(NativeArray<int> telemetryCursor)
        {
            if (_telemetryCursorSeeded || !telemetryCursor.IsCreated || telemetryCursor.Length < 1)
                return;

            telemetryCursor[0] = 0;
            _telemetryCursorSeeded = true;
        }

        private void SeedProfilesIfNeeded(NativeArray<UpscalerProfileDTO> profiles)
        {
            if (_profilesSeeded || !profiles.IsCreated)
                return;

            for (int i = 0; i < profiles.Length; i++)
                profiles[i] = default;
            _profilesSeeded = true;
        }

        private void SeedMockStateIfNeeded(NativeArray<DrsStateDTO> mockState)
        {
            if (_mockStateSeeded || !mockState.IsCreated || mockState.Length < 1)
                return;

            DrsStateDTO state;
            state.CurrentRenderScale = 0.5f;
            state.TargetRenderScale = 0.5f;
            state.UpscalerTypeHash = BilateralDrsUpscalerConstants.UpscalerTypeHash;
            state._pad0 = 0u;
            mockState[0] = state;
            _mockStateSeeded = true;
        }

        private void EnsureCsvScratch()
        {
            if (TryResolveVaultBuffer(in _csvScratchHandle, BilateralDrsUpscalerConstants.CsvScratchBytes, out NativeArray<byte> _))
                return;

            AcquireOrRefreshOwnedVaultBuffer(
                BufferID.Shinobu236BilateralDrsCsvScratch,
                BilateralDrsUpscalerConstants.CsvScratchBytes,
                NativeArrayOptions.UninitializedMemory,
                ref _csvScratchHandle,
                out NativeArray<byte> _);
        }

#if UNITY_EDITOR
        private bool LoadQualityProfilesCsv(string projectRelativePath)
        {
            EnsureVaultState();
            EnsureCsvScratch();
            if (!TryResolveVaultBuffer(in _csvScratchHandle, BilateralDrsUpscalerConstants.CsvScratchBytes, out NativeArray<byte> csvScratch) ||
                !TryResolveVaultBuffer(in _profilesHandle, BilateralDrsUpscalerConstants.ProfileCapacity, out NativeArray<UpscalerProfileDTO> profiles))
            {
                return false;
            }

            ClearProfiles(profiles);
            _profilesSeeded = false;
            string fullPath = BuildProjectPath(projectRelativePath);
            int byteCount = LoadFileBytesIntoScratch(fullPath, csvScratch);
            if (byteCount <= 0)
                return false;

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(csvScratch);
            ReadOnlySpan<byte> csvBytes = new ReadOnlySpan<byte>(ptr, byteCount);
            int parsed = ParseQualityProfiles(csvBytes, profiles);
            if (parsed <= 0)
                return false;

            _profilesSeeded = true;
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

        private bool EnsureConstantBuffers()
        {
            if (!SystemInfo.supportsSetConstantBuffer)
            {
                _lastFaultFlags = BilateralDrsUpscalerConstants.FaultConstantBufferUnsupported;
                if (!_faultDumped)
                {
                    DumpBlackBox();
                    _faultDumped = true;
                }

                return false;
            }

            if (_constantBufferA == null || !_constantBufferA.IsValid())
            {
                _constantBufferA = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    BilateralDrsUpscalerConstants.CBufferBytes); // COLD ALLOC: GraphicsBuffer[32B] A - SHINOBU_236 CBuffer.
            }

            if (_constantBufferB == null || !_constantBufferB.IsValid())
            {
                _constantBufferB = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    BilateralDrsUpscalerConstants.CBufferBytes); // COLD ALLOC: GraphicsBuffer[32B] B - SHINOBU_236 CBuffer.
            }

            return _constantBufferA != null && _constantBufferA.IsValid() &&
                   _constantBufferB != null && _constantBufferB.IsValid();
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
            if (!TryResolveVaultBuffer(in _parametersHandle, BilateralDrsUpscalerConstants.ParameterCapacity, out NativeArray<UpscalerParamsDTO> parameters))
            {
                FailClosedRuntimeRoute(BilateralDrsUpscalerConstants.FaultVaultUnavailable);
                return false;
            }

            parameters[BilateralDrsUpscalerConstants.ActiveParameterIndex] = parameters[BilateralDrsUpscalerConstants.PendingParameterIndex];
            UpscalerParamsDTO active = parameters[BilateralDrsUpscalerConstants.ActiveParameterIndex];
            bool valid = CheckFaultsAndDump(in active);
            _pendingGpuUpload = valid;
            if (!valid)
                InvalidatePublishedParameters();

            return valid;
        }

        private bool UploadParametersToGpu()
        {
            if (!TryResolveVaultBuffer(in _parametersHandle, BilateralDrsUpscalerConstants.ParameterCapacity, out NativeArray<UpscalerParamsDTO> parameters) ||
                !HasConstantBuffers())
            {
                FailClosedRuntimeRoute(BilateralDrsUpscalerConstants.FaultVaultUnavailable);
                return false;
            }

            GraphicsBuffer target = _activeConstantBufferIndex == 0 ? _constantBufferA : _constantBufferB;
            _activeConstantBufferIndex ^= 1;
            NativeArray<UpscalerParamsDTO> mapped = target.LockBufferForWrite<UpscalerParamsDTO>(0, 1);
            try
            {
                void* dst = NativeArrayUnsafeUtility.GetUnsafePtr(mapped);
                void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(parameters);
                UnsafeUtility.MemCpy(dst, src, BilateralDrsUpscalerConstants.CBufferBytes);
            }
            finally
            {
                target.UnlockBufferAfterWrite<UpscalerParamsDTO>(1);
            }

            _activeConstantBuffer = target;
            s_publishedConstantBuffer = target;
            s_publishedConstantBufferFrameIndex = _presentationFrameIndex;
            s_lastPublishedParameters = parameters[BilateralDrsUpscalerConstants.ActiveParameterIndex];
            s_hasPublishedParameters = true;
            return true;
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

        private bool TryResolveVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   requiredLength > 0 &&
                   IsVaultHandleCreated(in handle) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool AcquireOrRefreshOwnedVaultBuffer<T>(
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            ref VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null || requiredLength <= 0)
            {
                handle = default;
                return false;
            }

            if (IsVaultHandleCreated(in handle) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (IsVaultHandleCreated(in handle))
                ReleaseVaultHandle(vault, ref handle);

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, OwnerSystemId, options);
            return IsVaultHandleCreated(in handle) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null && IsVaultHandleCreated(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void RegisterDispatcherRouteAllOrFail()
        {
            if (_dispatcherRouteReady)
                return;

            if (_simulationBridge == null)
                _simulationBridge = new SimulationKernelBridge(this); // COLD ALLOC: IDispatcherSystem[1] - SHINOBU_236 scheduled Burst kernel bridge.
            if (_postSimulationBridge == null)
                _postSimulationBridge = new PostSimulationPublishBridge(this); // COLD ALLOC: IDispatcherSystem[1] - SHINOBU_236 post-simulation DTO publisher.
            if (_visualSyncBridge == null)
                _visualSyncBridge = new VisualSyncUploadBridge(this); // COLD ALLOC: IDispatcherSystem[1] - SHINOBU_236 VisualSync upload bridge.

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
            _simulationKernelScheduled = false;
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
                    IDataVault previousVault = _dataVault ?? previousService as IDataVault;
                    ReleaseAllVaultHandles(previousVault);
                    ResetVaultSeedState();
                    _dataVault = currentService as IDataVault;
                    if (_dataVault != null)
                    {
                        EnsureVaultState();
                    }
                    break;
                case GlobalRegistryServiceSlot.ResolutionScalerService:
                    _resolutionScaler = currentService as IResolutionScalerService;
                    break;
            }
        }

        private void ReleaseAllVaultHandles(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref _parametersHandle);
            ReleaseVaultHandle(vault, ref _tuningHandle);
            ReleaseVaultHandle(vault, ref _telemetryHandle);
            ReleaseVaultHandle(vault, ref _telemetryCursorHandle);
            ReleaseVaultHandle(vault, ref _profilesHandle);
            ReleaseVaultHandle(vault, ref _csvScratchHandle);
            ReleaseVaultHandle(vault, ref _mockStateHandle);
        }

        private void ResetVaultSeedState()
        {
            _vaultStateReady = false;
            _tuningSeeded = false;
            _telemetrySeeded = false;
            _telemetryCursorSeeded = false;
            _profilesSeeded = false;
            _mockStateSeeded = false;
            _simulationKernelScheduled = false;
            _pendingGpuUpload = false;
            _dispatcherRouteReady = false;
            InvalidatePublishedParameters();
            s_edgeMaskDebugEnabled = false;
        }

        private void ShutdownServiceState()
        {
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
            _dataVault = null;
            _resolutionScaler = null;
        }

        private void EnsureBlackBoxDumpPathCold()
        {
            if (!string.IsNullOrEmpty(_blackBoxDumpPath))
                return;

            string projectRoot = Application.dataPath;
            DirectoryInfo directory = Directory.GetParent(projectRoot);
            string root = directory != null ? directory.FullName : projectRoot;
            _blackBoxDumpPath = Path.Combine(root, DumpPath);
            _blackBoxDumpDirectory = Path.GetDirectoryName(_blackBoxDumpPath);
            if (string.IsNullOrEmpty(_blackBoxDumpDirectory))
            {
                _blackBoxDumpPath = null;
                return;
            }

            try
            {
                Directory.CreateDirectory(_blackBoxDumpDirectory);
            }
            catch (Exception)
            {
                _blackBoxDumpPath = null;
                _blackBoxDumpDirectory = null;
            }
        }

#if UNITY_EDITOR
        private static int LoadFileBytesIntoScratch(string fullPath, NativeArray<byte> csvScratch)
        {
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath) || !csvScratch.IsCreated)
                return 0;

            byte* dst = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(csvScratch);
            int capacity = csvScratch.Length;
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
                        fixed (byte* src = block)
                        {
                            UnsafeUtility.MemCpy(dst + total, src, copy);
                        }

                        total += copy;
                        if (copy < read)
                            break;
                    }
                }
            }
            catch (Exception)
            {
                return 0;
            }

            return total;
        }

        private static int ParseQualityProfiles(ReadOnlySpan<byte> csvBytes, NativeArray<UpscalerProfileDTO> profiles)
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
            if (!TryResolveVaultBuffer(in _telemetryHandle, BilateralDrsUpscalerConstants.TelemetryCapacity, out NativeArray<UpscalerTelemetryEntry> telemetry))
                return;

            TryResolveVaultBuffer(in _telemetryCursorHandle, 1, out NativeArray<int> telemetryCursor);
            EnsureBlackBoxDumpPathCold();
            string path = _blackBoxDumpPath;
            string directory = _blackBoxDumpDirectory;
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return;

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                int entryCount = math.min(telemetry.Length, BilateralDrsUpscalerConstants.TelemetryCapacity);
                int telemetryWriteCursor = telemetryCursor.IsCreated && telemetryCursor.Length > 0 ? telemetryCursor[0] : 0;
                int wrappedCursor = 0;
                if (entryCount > 0)
                {
                    wrappedCursor = telemetryWriteCursor % entryCount;
                    if (wrappedCursor < 0)
                        wrappedCursor += entryCount;
                }

                writer.Write(0x42323336u); // B236
                writer.Write(entryCount);
                writer.Write(telemetryWriteCursor);
                writer.Write(_lastFaultFlags);
                writer.Write(UnsafeUtility.SizeOf<UpscalerTelemetryEntry>());
                for (int i = 0; i < entryCount; i++)
                {
                    int index = wrappedCursor + i;
                    if (index >= entryCount)
                        index -= entryCount;

                    UpscalerTelemetryEntry entry = telemetry[index];
                    writer.Write(entry.FrameIndex);
                    writer.Write(entry.Flags);
                    writer.Write(entry.CurrentRenderScale01);
                    writer.Write(entry.TargetRenderScale01);
                    writer.Write(entry.QualityScalar);
                    writer.Write(entry.BilateralRadiusPixels);
                    writer.Write(entry.DepthWeight);
                    writer.Write(entry.EstimatedGpuMicros);
                    writer.Write(entry.ResolutionParams.x);
                    writer.Write(entry.ResolutionParams.y);
                    writer.Write(entry.ResolutionParams.z);
                    writer.Write(entry.ResolutionParams.w);
                    writer.Write(entry.FilterParams.x);
                    writer.Write(entry.FilterParams.y);
                    writer.Write(entry.FilterParams.z);
                    writer.Write(entry.FilterParams.w);
                }
            }
        }
    }
}
