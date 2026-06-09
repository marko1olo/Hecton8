using System;
using System.Diagnostics;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Graphics.Culling
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-84)]
    public sealed unsafe class AbyssalShadowCullingRuntime : MonoBehaviour, IDisposable, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private const uint SystemHash = 0x53313434u; // S134
        private const uint TelemetryFlagExternalProducer = 1u << 22;
        private const uint TelemetryFlagExternalHzb = 1u << 23;
        private const uint TelemetryFlagJobBusy = 1u << 24;
        private const uint TelemetryFlagCsvLoaded = 1u << 25;
        private const uint TelemetryFlagDumped = 1u << 26;
        private const uint TelemetryFlagGpuUploaded = 1u << 27;
        private const uint TelemetryFlagNonFinite = AbyssalShadowCullFlags.NonFinite;
        private const int BatchSize = 64;
        private const string RuntimeName = "SHINOBU_134_AbyssalShadowCulling";
        private const string DumpPath = "Docs/AgentLogs/Dump_SHADOW_DIRECTOR.bin";
        private const string DefaultCsvPath = "Docs/Tasks/shadow_culling_profiles.csv";
        private const SystemID OwnerSystemId = SystemID.GraphicsScalability;
        private const uint TelemetryFlagVaultLockFailed = 1u << 21;

        private const uint JobPinInstances = 1u << 0;
        private const uint JobPinStates = 1u << 1;
        private const uint JobPinIlluminationScalars = 1u << 2;
        private const uint JobPinFrustumPlanes = 1u << 3;
        private const uint JobPinProfileRules = 1u << 4;
        private const uint JobPinCounters = 1u << 5;
        private const uint JobPinHzbDepthTiles = 1u << 6;
        private const uint JobPinIndirectArgs = 1u << 7;

        private static readonly int ShadowCullStatesShaderId = Shader.PropertyToID("_H8AbyssalShadowCullStates");
        private static readonly int ShadowCullIndirectArgsShaderId = Shader.PropertyToID("_H8AbyssalShadowIndirectArgs");
        private static readonly int ShadowCullCountShaderId = Shader.PropertyToID("_H8AbyssalShadowCullCount");
        private static readonly int ShadowCullQualityShaderId = Shader.PropertyToID("_H8AbyssalShadowQuality");
        private static AbyssalShadowCullingRuntime s_active;

        [SerializeField, Min(1)] private int _instanceCapacity = AbyssalShadowCullingConstants.DefaultInstanceCapacity;
        [SerializeField, Range(20f, 300f)] private float _baseShadowDistanceMeters = AbyssalShadowCullingConstants.DefaultMaximumShadowDistanceMeters;
        [SerializeField, Range(0.001f, 0.5f)] private float _ditherFadeBand01 = AbyssalShadowCullingConstants.DefaultDitherFadeBand01;
        [SerializeField, Range(0f, 1f)] private float _darknessThreshold = AbyssalShadowCullingConstants.DefaultDarknessThreshold;
        [SerializeField, Range(0.7f, 1f)] private float _pointLightUltraThreshold = AbyssalShadowCullingConstants.DefaultPointLightUltraThreshold;
        [SerializeField] private Vector3 _directionalLightDirection = new Vector3(-0.35f, -0.72f, -0.25f);
        [SerializeField] private Vector3 _hzbViewRight = Vector3.right;
        [SerializeField] private Vector3 _hzbViewUp = Vector3.up;
        [SerializeField] private Vector3 _hzbViewForward = Vector3.forward;
        [SerializeField] private string _profileCsvPath = DefaultCsvPath;
        [SerializeField] private bool _editorDrawGizmos;
        [SerializeField, Min(1)] private int _gizmoBoxLimit = 96;

        private IDataVault _dataVault;
        private ICelestialLightReadabilityReadModel _celestialLightReadModel;
        private VaultGenerationHandle<ShadowCullInstanceDTO> _instanceHandle;
        private VaultGenerationHandle<ShadowCullStateDTO> _stateHandle;
        private VaultGenerationHandle<float> _illuminationHandle;
        private VaultGenerationHandle<float4> _frustumHandle;
        private VaultGenerationHandle<ShadowCullCountersDTO> _counterHandle;
        private VaultGenerationHandle<CullingTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<AbyssalShadowRuntimeStateDTO> _runtimeHandle;
        private VaultGenerationHandle<ShadowCullProfileRuleDTO> _profileRuleHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<ShadowCullHzbTileDTO> _hzbTileHandle;
        private VaultGenerationHandle<ShadowCullIndirectArgsDTO> _indirectArgsHandle;

        private GraphicsBuffer _stateUploadBufferA;
        private GraphicsBuffer _stateUploadBufferB;
        private GraphicsBuffer _indirectArgsBufferA;
        private GraphicsBuffer _indirectArgsBufferB;
        private GraphicsBuffer _publishedStateBuffer;
        private GraphicsBuffer _publishedIndirectArgsBuffer;
        private SimulationPhaseSystem _simulationPhaseSystem;
        private VisualSyncPhaseSystem _visualSyncPhaseSystem;
        private JobHandle _cullingHandle;
        private JobHandle _registeredProducerDependency;
        private double3 _cameraAUP;
        private long _scheduleTimestamp;
        private int _scheduledInstanceCount;
        private int _externalActiveInstanceCount;
        private int _externalHzbTileCount;
        private uint _scheduledFrame;
        private uint _registeredProducerFlags;
        private uint _lastTelemetryExtraFlags;
        private bool _registeredSimulationPhase;
        private bool _registeredVisualSyncPhase;
        private bool _registeredSlowTick;
        private bool _registeredHotSwapListener;
        private bool _initialized;
        private bool _resourceRefreshRequested;
        private bool _jobPending;
        private bool _jobPinsHeld;
        private IDataVault _jobPinVault;
        private uint _jobPinMask;
        private bool _mockSeeded;
        private bool _hzbSeeded;
        private bool _runtimeDefaultsWritten;
        private bool _frustumDefaultsWritten;
        private bool _profileDefaultsWritten;
        private bool _requestMockRegenerate;
        private bool _uploadBufferFlip;
        private float _lastBurstWallTimeMs;
        private float _lastUploadMicroseconds;
        private float _lastMaxShadowDistanceMeters;
        private ShadowCullCountersDTO _lastCounters;

        public static bool IsActive => s_active != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_active = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallRuntime()
        {
            if (!Application.isPlaying || s_active != null)
                return;

            GameObject host = new GameObject(RuntimeName);
            host.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            DontDestroyOnLoad(host);
            s_active = host.AddComponent<AbyssalShadowCullingRuntime>();
        }

        private void OnEnable()
        {
            if (s_active != null && !ReferenceEquals(s_active, this))
            {
                enabled = false;
                return;
            }

            s_active = this;
            CacheCelestialLightReadModel(GlobalRegistry.CelestialLightReadabilityReadModel);
            RebindDataVaultForLifecycle(GlobalRegistry.DataVault, null);
            if (_dataVault != null)
                EnsureInitialized(_dataVault);
            _resourceRefreshRequested = _dataVault == null || !_initialized;
            TryRegisterHotSwapListener();
            TryRegisterSlowTick();

            if (_simulationPhaseSystem == null)
                _simulationPhaseSystem = new SimulationPhaseSystem(this);
            if (_visualSyncPhaseSystem == null)
                _visualSyncPhaseSystem = new VisualSyncPhaseSystem(this);

            TryRegisterDispatcherSystems();
        }

        private void OnDisable()
        {
            Dispose();
            if (ReferenceEquals(s_active, this))
                s_active = null;
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            IDataVault vault = ResolveVault();
            uint frame = vault != null ? ResolveDeterministicFrame(vault, 0u, false) : (_scheduledFrame == 0u ? 1u : _scheduledFrame);
            CompletePendingJobForBarrier(frame);
            TryUnregisterHotSwapListener();
            TryUnregisterSlowTick();
            TryUnregisterDispatcherSystems();

            ReleaseGpuBuffers();
            ReleaseVaultHandles(vault);
            _celestialLightReadModel = null;
            ResetVaultHandles();
            _dataVault = null;
            _initialized = false;
        }

        private JobHandle ScheduleSimulationPhase(
            in DispatcherTimingDTO timing,
            in DispatcherJobContext context,
            JobHandle dependsOn)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !_initialized || _jobPending)
            {
                if (vault == null || !_initialized)
                    _resourceRefreshRequested = true;

                return dependsOn;
            }

            uint frame = ResolveDeterministicFrame(vault, context.Frame, true);
            return ScheduleCullingPass(vault, frame, dependsOn);
        }

        private void CommitVisualSyncPhase(in DispatcherTimingDTO timing)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !_initialized)
            {
                _resourceRefreshRequested = true;
                return;
            }

            uint frame = ResolveTelemetryFrame(vault);
            if (!TryFinalizePendingJobNoWait(frame))
                RecordTelemetry(vault, frame, TelemetryFlagJobBusy, 0u, 0f);
        }

        public void SlowTick()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                _resourceRefreshRequested = true;
                return;
            }

            if (!_resourceRefreshRequested && _initialized)
                return;

            _initialized = HasInitializedResourcesReady(vault);
            _resourceRefreshRequested = !_initialized;
        }

        public void SetCameraAUP(double3 cameraAUP)
        {
            _cameraAUP = cameraAUP;
        }

        public void SetHzbViewBasis(float3 right, float3 up, float3 forward)
        {
            _hzbViewRight = ToFiniteVector3(math.normalizesafe(right, new float3(1f, 0f, 0f)));
            _hzbViewUp = ToFiniteVector3(math.normalizesafe(up, new float3(0f, 1f, 0f)));
            _hzbViewForward = ToFiniteVector3(math.normalizesafe(forward, new float3(0f, 0f, 1f)));
        }

        public void SetLocalizedFrustumPlanes(
            float4 plane0,
            float4 plane1,
            float4 plane2,
            float4 plane3,
            float4 plane4,
            float4 plane5)
        {
            IDataVault vault = ResolveVault();
            if (vault == null || _jobPending || !EnsureVaultBuffers(vault))
                return;

            if (!TryOpenVaultBuffer(vault, ref _frustumHandle, AbyssalShadowBufferIds.FrustumPlanes, AbyssalShadowCullingConstants.FrustumPlaneCount, out NativeArray<float4> planes))
                return;

            planes[0] = plane0;
            planes[1] = plane1;
            planes[2] = plane2;
            planes[3] = plane3;
            planes[4] = plane4;
            planes[5] = plane5;
            _frustumDefaultsWritten = true;
        }

        private bool TryResolveProducerBuffers(
            out NativeArray<ShadowCullInstanceDTO> instances,
            out NativeArray<float> illuminationScalars,
            out NativeArray<ShadowCullHzbTileDTO> hzbTiles,
            out NativeArray<float4> frustumPlanes)
        {
            instances = default;
            illuminationScalars = default;
            hzbTiles = default;
            frustumPlanes = default;
            IDataVault vault = ResolveVault();
            if (vault == null || _jobPending)
                return false;

            return TryOpenVaultBuffer(vault, ref _instanceHandle, AbyssalShadowBufferIds.Instances, math.max(1, _instanceCapacity), out instances) &&
                   TryOpenVaultBuffer(vault, ref _illuminationHandle, AbyssalShadowBufferIds.IlluminationScalars, math.max(1, _instanceCapacity), out illuminationScalars) &&
                   TryOpenVaultBuffer(vault, ref _hzbTileHandle, AbyssalShadowBufferIds.HzbDepthTiles, AbyssalShadowCullingConstants.HzbTileCapacity, out hzbTiles) &&
                   TryOpenVaultBuffer(vault, ref _frustumHandle, AbyssalShadowBufferIds.FrustumPlanes, AbyssalShadowCullingConstants.FrustumPlaneCount, out frustumPlanes);
        }

        public void RegisterExternalProducerDependency(
            JobHandle dependency,
            int activeInstanceCount,
            int hzbTileCount,
            bool instanceDataWritten,
            bool hzbDataWritten)
        {
            _registeredProducerDependency = JobHandle.CombineDependencies(_registeredProducerDependency, dependency);
            if (activeInstanceCount > 0)
                _externalActiveInstanceCount = activeInstanceCount;
            if (hzbTileCount > 0)
                _externalHzbTileCount = hzbTileCount;
            if (instanceDataWritten)
            {
                _mockSeeded = true;
                _registeredProducerFlags |= TelemetryFlagExternalProducer;
            }

            if (hzbDataWritten)
            {
                _hzbSeeded = true;
                _registeredProducerFlags |= TelemetryFlagExternalHzb;
            }
        }

        public bool RunMockCullingOnce()
        {
            IDataVault vault = ResolveVault();
            if (vault == null || !EnsureInitialized(vault))
                return false;

            uint frame = ResolveDeterministicFrame(vault, 0u, true);
            CompletePendingJobForBarrier(frame);
            _requestMockRegenerate = true;
            ScheduleCullingPass(vault, frame, default);
            if (!_jobPending)
                return false;

            return CompletePendingJobForBarrier(frame);
        }

        public void ApplyTunerSettings(float baseShadowDistanceMeters, float ditherFadeBand01, float darknessThreshold)
        {
            IDataVault vault = ResolveVault();
            if (vault == null || _jobPending || !EnsureVaultBuffers(vault))
                return;

            if (!TryOpenVaultBuffer(vault, ref _runtimeHandle, AbyssalShadowBufferIds.RuntimeState, 1, out NativeArray<AbyssalShadowRuntimeStateDTO> runtimeArray))
                return;

            AbyssalShadowRuntimeStateDTO runtime = runtimeArray[0];
            runtime.BaseShadowDistanceMeters = math.clamp(baseShadowDistanceMeters, 20f, 300f);
            runtime.DitherFadeBand01 = math.clamp(ditherFadeBand01, 0.001f, 0.5f);
            runtime.DarknessThreshold = math.saturate(darknessThreshold);
            runtimeArray[0] = runtime;
            _baseShadowDistanceMeters = runtime.BaseShadowDistanceMeters;
            _ditherFadeBand01 = runtime.DitherFadeBand01;
            _darknessThreshold = runtime.DarknessThreshold;
        }

        public void SetProfileCsvPath(string path)
        {
            _profileCsvPath = string.IsNullOrEmpty(path) ? DefaultCsvPath : path;
        }

        public string GetProfileCsvPath()
        {
            return string.IsNullOrEmpty(_profileCsvPath) ? DefaultCsvPath : _profileCsvPath;
        }

#if UNITY_EDITOR
        public bool LoadProfileCsv()
        {
            IDataVault vault = ResolveVault();
            if (vault == null || _jobPending || !EnsureVaultBuffers(vault))
                return false;

            string path = ResolveCsvPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            if (!TryOpenVaultBuffer(vault, ref _csvScratchHandle, AbyssalShadowBufferIds.CsvScratch, AbyssalShadowCullingConstants.CsvScratchCapacity, out NativeArray<byte> scratch) ||
                !TryOpenVaultBuffer(vault, ref _profileRuleHandle, AbyssalShadowBufferIds.ProfileRules, AbyssalShadowCullingConstants.ProfileRuleCapacity, out NativeArray<ShadowCullProfileRuleDTO> rules))
                return false;

            int bytesRead;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                int readLength = Math.Min(scratch.Length, (int)Math.Min(stream.Length, scratch.Length));
                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
                bytesRead = stream.Read(new Span<byte>(destination, readLength));
            }

            bool validated = AbyssalShadowProfileCsv.Validate(scratch, bytesRead, rules.Length, out ShadowCullCsvParseResultDTO validation);
            if (!validated || validation.ParsedRuleCount == 0u || validation.RejectedLineCount != 0u)
                return false;

            bool parsed = AbyssalShadowProfileCsv.Parse(scratch, bytesRead, rules, out ShadowCullCsvParseResultDTO result);
            if (!parsed || result.ParsedRuleCount == 0u)
                return false;

            int ruleSize = UnsafeUtility.SizeOf<ShadowCullProfileRuleDTO>();
            int parsedCount = math.min((int)result.ParsedRuleCount, rules.Length);
            int tailCount = rules.Length - parsedCount;
            if (tailCount > 0)
            {
                byte* tail = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rules) + parsedCount * ruleSize;
                UnsafeUtility.MemClear(tail, tailCount * ruleSize);
            }

            _lastTelemetryExtraFlags |= TelemetryFlagCsvLoaded;
            return true;
        }
#endif

        public bool TryGetTunerSnapshot(out AbyssalShadowTunerSnapshot snapshot)
        {
            snapshot = default;
            IDataVault vault = ResolveVault();
            if (vault == null)
                return false;

            if (!TryOpenVaultBuffer(vault, ref _runtimeHandle, AbyssalShadowBufferIds.RuntimeState, 1, out NativeArray<AbyssalShadowRuntimeStateDTO> runtimeArray))
                return false;

            AbyssalShadowRuntimeStateDTO runtime = runtimeArray[0];
            snapshot.EvaluatedCount = _lastCounters.EvaluatedCount;
            snapshot.MainCulledCount = _lastCounters.MainCulledCount;
            snapshot.ShadowCulledCount = _lastCounters.ShadowCulledCount;
            snapshot.DarknessCulledCount = _lastCounters.DarknessCulledCount;
            snapshot.PointLightCulledCount = _lastCounters.PointLightCulledCount;
            snapshot.ShadowOnlyCount = _lastCounters.ShadowOnlyCount;
            snapshot.DitheredCount = _lastCounters.DitheredCount;
            snapshot.Flags = _lastCounters.Flags;
            snapshot.GlobalQualityWeight = runtime.GlobalQualityWeight;
            snapshot.BaseShadowDistanceMeters = runtime.BaseShadowDistanceMeters;
            snapshot.DitherFadeBand01 = runtime.DitherFadeBand01;
            snapshot.DarknessThreshold = runtime.DarknessThreshold;
            snapshot.LastBurstWallTimeMs = _lastBurstWallTimeMs;
            snapshot.LastUploadMicroseconds = _lastUploadMicroseconds;
            snapshot.MaxShadowDistanceMeters = _lastMaxShadowDistanceMeters;
            snapshot.LastUploadCount = runtime.LastUploadCount;
            return true;
        }

        public bool TryGetPublishedGpuBuffers(
            out GraphicsBuffer stateBuffer,
            out GraphicsBuffer indirectArgsBuffer,
            out uint uploadedCount)
        {
            stateBuffer = _publishedStateBuffer;
            indirectArgsBuffer = _publishedIndirectArgsBuffer;
            uploadedCount = _lastCounters.VisibleShadowCount;
            return stateBuffer != null && indirectArgsBuffer != null;
        }

        public static bool TryGetActiveSnapshot(out AbyssalShadowTunerSnapshot snapshot)
        {
            snapshot = default;
            return s_active != null && s_active.TryGetTunerSnapshot(out snapshot);
        }

        private static bool TryResolveActiveProducerBuffers(
            out NativeArray<ShadowCullInstanceDTO> instances,
            out NativeArray<float> illuminationScalars,
            out NativeArray<ShadowCullHzbTileDTO> hzbTiles,
            out NativeArray<float4> frustumPlanes)
        {
            instances = default;
            illuminationScalars = default;
            hzbTiles = default;
            frustumPlanes = default;
            return s_active != null && s_active.TryResolveProducerBuffers(out instances, out illuminationScalars, out hzbTiles, out frustumPlanes);
        }

        public static void RegisterActiveExternalProducerDependency(
            JobHandle dependency,
            int activeInstanceCount,
            int hzbTileCount,
            bool instanceDataWritten,
            bool hzbDataWritten)
        {
            if (s_active != null)
            {
                s_active.RegisterExternalProducerDependency(
                    dependency,
                    activeInstanceCount,
                    hzbTileCount,
                    instanceDataWritten,
                    hzbDataWritten);
            }
        }

        public static bool TryGetActivePublishedGpuBuffers(
            out GraphicsBuffer stateBuffer,
            out GraphicsBuffer indirectArgsBuffer,
            out uint uploadedCount)
        {
            stateBuffer = null;
            indirectArgsBuffer = null;
            uploadedCount = 0u;
            return s_active != null && s_active.TryGetPublishedGpuBuffers(out stateBuffer, out indirectArgsBuffer, out uploadedCount);
        }

        public static void ApplyActiveTunerSettings(float baseShadowDistanceMeters, float ditherFadeBand01, float darknessThreshold)
        {
            if (s_active != null)
                s_active.ApplyTunerSettings(baseShadowDistanceMeters, ditherFadeBand01, darknessThreshold);
        }

        public static bool RunActiveMockOnce()
        {
            return s_active != null && s_active.RunMockCullingOnce();
        }

#if UNITY_EDITOR
        public static bool LoadActiveProfileCsv()
        {
            return s_active != null && s_active.LoadProfileCsv();
        }
#endif

        public static void SetActiveProfileCsvPath(string path)
        {
            if (s_active != null)
                s_active.SetProfileCsvPath(path);
        }

        public static string GetActiveProfileCsvPath()
        {
            return s_active != null ? s_active.GetProfileCsvPath() : DefaultCsvPath;
        }

        public static void SetActiveGizmo(bool enabled)
        {
            if (s_active != null)
                s_active._editorDrawGizmos = enabled;
        }

        public static void SetActiveHzbViewBasis(float3 right, float3 up, float3 forward)
        {
            if (s_active != null)
                s_active.SetHzbViewBasis(right, up, forward);
        }

        private IDataVault ResolveVault()
        {
            return _dataVault;
        }

        private bool EnsureInitialized(IDataVault vault)
        {
            if (!EnsureVaultBuffers(vault))
                return false;

            EnsureGpuBuffersCold(math.max(1, _instanceCapacity));
            _initialized = true;
            return true;
        }

        private bool HasInitializedResourcesReady(IDataVault vault)
        {
            int instanceCount = math.max(1, _instanceCapacity);
            return vault != null &&
                   TryOpenVaultBuffer(vault, ref _instanceHandle, AbyssalShadowBufferIds.Instances, instanceCount, out NativeArray<ShadowCullInstanceDTO> _) &&
                   TryOpenVaultBuffer(vault, ref _stateHandle, AbyssalShadowBufferIds.States, instanceCount, out NativeArray<ShadowCullStateDTO> _) &&
                   TryOpenVaultBuffer(vault, ref _illuminationHandle, AbyssalShadowBufferIds.IlluminationScalars, instanceCount, out NativeArray<float> _) &&
                   TryOpenVaultBuffer(vault, ref _frustumHandle, AbyssalShadowBufferIds.FrustumPlanes, AbyssalShadowCullingConstants.FrustumPlaneCount, out NativeArray<float4> _) &&
                   TryOpenVaultBuffer(vault, ref _counterHandle, AbyssalShadowBufferIds.Counters, 1, out NativeArray<ShadowCullCountersDTO> _) &&
                   TryOpenVaultBuffer(vault, ref _telemetryHandle, AbyssalShadowBufferIds.TelemetryRing, AbyssalShadowCullingConstants.TelemetryCapacity, out NativeArray<CullingTelemetryEntry> _) &&
                   TryOpenVaultBuffer(vault, ref _runtimeHandle, AbyssalShadowBufferIds.RuntimeState, 1, out NativeArray<AbyssalShadowRuntimeStateDTO> _) &&
                   TryOpenVaultBuffer(vault, ref _profileRuleHandle, AbyssalShadowBufferIds.ProfileRules, AbyssalShadowCullingConstants.ProfileRuleCapacity, out NativeArray<ShadowCullProfileRuleDTO> _) &&
                   TryOpenVaultBuffer(vault, ref _csvScratchHandle, AbyssalShadowBufferIds.CsvScratch, AbyssalShadowCullingConstants.CsvScratchCapacity, out NativeArray<byte> _) &&
                   TryOpenVaultBuffer(vault, ref _hzbTileHandle, AbyssalShadowBufferIds.HzbDepthTiles, AbyssalShadowCullingConstants.HzbTileCapacity, out NativeArray<ShadowCullHzbTileDTO> _) &&
                   TryOpenVaultBuffer(vault, ref _indirectArgsHandle, AbyssalShadowBufferIds.IndirectArgs, 1, out NativeArray<ShadowCullIndirectArgsDTO> _) &&
                   HasGpuBuffersReady(instanceCount);
        }

        private bool EnsureVaultBuffers(IDataVault vault)
        {
            int instanceCount = math.max(1, _instanceCapacity);
            NativeArray<float4> planes = default;
            NativeArray<AbyssalShadowRuntimeStateDTO> runtimeArray = default;
            NativeArray<ShadowCullProfileRuleDTO> profileRules = default;
            bool buffersReady = OpenOrAcquireVaultBuffer(vault, ref _instanceHandle, AbyssalShadowBufferIds.Instances, instanceCount, NativeArrayOptions.UninitializedMemory, out _) &&
                                OpenOrAcquireVaultBuffer(vault, ref _stateHandle, AbyssalShadowBufferIds.States, instanceCount, NativeArrayOptions.UninitializedMemory, out _) &&
                                OpenOrAcquireVaultBuffer(vault, ref _illuminationHandle, AbyssalShadowBufferIds.IlluminationScalars, instanceCount, NativeArrayOptions.UninitializedMemory, out _) &&
                                OpenOrAcquireVaultBuffer(vault, ref _frustumHandle, AbyssalShadowBufferIds.FrustumPlanes, AbyssalShadowCullingConstants.FrustumPlaneCount, NativeArrayOptions.UninitializedMemory, out planes) &&
                                OpenOrAcquireVaultBuffer(vault, ref _counterHandle, AbyssalShadowBufferIds.Counters, 1, NativeArrayOptions.UninitializedMemory, out _) &&
                                OpenOrAcquireVaultBuffer(vault, ref _telemetryHandle, AbyssalShadowBufferIds.TelemetryRing, AbyssalShadowCullingConstants.TelemetryCapacity, NativeArrayOptions.UninitializedMemory, out _) &&
                                OpenOrAcquireVaultBuffer(vault, ref _runtimeHandle, AbyssalShadowBufferIds.RuntimeState, 1, NativeArrayOptions.UninitializedMemory, out runtimeArray) &&
                                OpenOrAcquireVaultBuffer(vault, ref _profileRuleHandle, AbyssalShadowBufferIds.ProfileRules, AbyssalShadowCullingConstants.ProfileRuleCapacity, NativeArrayOptions.UninitializedMemory, out profileRules) &&
                                OpenOrAcquireVaultBuffer(vault, ref _csvScratchHandle, AbyssalShadowBufferIds.CsvScratch, AbyssalShadowCullingConstants.CsvScratchCapacity, NativeArrayOptions.UninitializedMemory, out _) &&
                                OpenOrAcquireVaultBuffer(vault, ref _hzbTileHandle, AbyssalShadowBufferIds.HzbDepthTiles, AbyssalShadowCullingConstants.HzbTileCapacity, NativeArrayOptions.UninitializedMemory, out NativeArray<ShadowCullHzbTileDTO> hzbTiles) &&
                                OpenOrAcquireVaultBuffer(vault, ref _indirectArgsHandle, AbyssalShadowBufferIds.IndirectArgs, 1, NativeArrayOptions.UninitializedMemory, out NativeArray<ShadowCullIndirectArgsDTO> indirectArgs);
            if (!buffersReady)
                return false;

            if (!_runtimeDefaultsWritten)
            {
                AbyssalShadowRuntimeStateDTO runtime = default;
                runtime.BaseShadowDistanceMeters = math.clamp(_baseShadowDistanceMeters, 20f, 300f);
                runtime.DitherFadeBand01 = math.clamp(_ditherFadeBand01, 0.001f, 0.5f);
                runtime.DarknessThreshold = math.saturate(_darknessThreshold);
                runtime.ActiveInstanceCount = instanceCount;
                runtime.GlobalQualityWeightOverride = -1f;
                runtime.GlobalQualityWeight = 1f;
                runtime.DirectionalLightDirection = ResolveDirectionalLight();
                runtime.PointLightUltraThreshold = math.clamp(_pointLightUltraThreshold, 0.7f, 1f);
                runtime.MaxShadowDistanceMeters = runtime.BaseShadowDistanceMeters;
                runtime.MinCasterRadiusMeters = AbyssalShadowCullingConstants.DefaultShadowCasterRadiusUltra;
                runtimeArray[0] = runtime;
                _runtimeDefaultsWritten = true;
            }

            if (!_frustumDefaultsWritten)
            {
                AbyssalShadowFrustumMath.WriteDefaultCameraRelativePlanes(planes);
                _frustumDefaultsWritten = true;
            }

            if (!_profileDefaultsWritten)
            {
                UnsafeUtility.MemClear(
                    NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(profileRules),
                    profileRules.Length * UnsafeUtility.SizeOf<ShadowCullProfileRuleDTO>());
                _profileDefaultsWritten = true;
            }

            return true;
        }

        private bool OpenOrAcquireVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            if (TryOpenVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            if (vault == null || requiredLength <= 0)
            {
                buffer = default;
                return false;
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                OwnerSystemId,
                options);
            return TryOpenVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
        }

        private bool TryOpenVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsMatchingVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsMatchingVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)OwnerSystemId &&
                   handle.Generation != 0u;
        }

        private void ResetVaultHandles()
        {
            _instanceHandle = default;
            _stateHandle = default;
            _illuminationHandle = default;
            _frustumHandle = default;
            _counterHandle = default;
            _telemetryHandle = default;
            _runtimeHandle = default;
            _profileRuleHandle = default;
            _csvScratchHandle = default;
            _hzbTileHandle = default;
            _indirectArgsHandle = default;
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref _instanceHandle);
            ReleaseVaultHandle(vault, ref _stateHandle);
            ReleaseVaultHandle(vault, ref _illuminationHandle);
            ReleaseVaultHandle(vault, ref _frustumHandle);
            ReleaseVaultHandle(vault, ref _counterHandle);
            ReleaseVaultHandle(vault, ref _telemetryHandle);
            ReleaseVaultHandle(vault, ref _runtimeHandle);
            ReleaseVaultHandle(vault, ref _profileRuleHandle);
            ReleaseVaultHandle(vault, ref _csvScratchHandle);
            ReleaseVaultHandle(vault, ref _hzbTileHandle);
            ReleaseVaultHandle(vault, ref _indirectArgsHandle);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null &&
                handle.BufferID != 0u &&
                handle.Generation != 0u &&
                handle.SystemID == (uint)OwnerSystemId)
            {
                vault.ReleaseBuffer(in handle);
            }

            handle = default;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                RebindDataVaultForLifecycle(currentService as IDataVault, previousService as IDataVault);
                if (_dataVault != null && isActiveAndEnabled)
                {
                    _initialized = EnsureInitialized(_dataVault);
                    _resourceRefreshRequested = !_initialized;
                }
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterSlowTick();
                TryUnregisterDispatcherSystems();
                if (currentService != null && isActiveAndEnabled)
                {
                    TryRegisterSlowTick();
                    TryRegisterDispatcherSystems();
                }
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.CelestialEngineRuntime)
            {
                CacheCelestialLightReadModel(currentService as ICelestialLightReadabilityReadModel);
            }
        }

        private void RebindDataVaultForLifecycle(IDataVault nextVault, IDataVault releaseVaultFallback)
        {
            if (ReferenceEquals(_dataVault, nextVault))
                return;

            IDataVault releaseVault = _dataVault ?? releaseVaultFallback;
            uint frame = releaseVault != null ? ResolveDeterministicFrame(releaseVault, 0u, false) : (_scheduledFrame == 0u ? 1u : _scheduledFrame);
            CompletePendingJobForBarrier(frame);
            ReleaseVaultHandles(releaseVault);
            ResetVaultHandles();
            _dataVault = nextVault;
            _initialized = false;
            _jobPending = false;
            _mockSeeded = false;
            _hzbSeeded = false;
            _runtimeDefaultsWritten = false;
            _frustumDefaultsWritten = false;
            _profileDefaultsWritten = false;
            _requestMockRegenerate = true;
            _scheduledInstanceCount = 0;
            _externalActiveInstanceCount = 0;
            _externalHzbTileCount = 0;
            _registeredProducerDependency = default;
            _registeredProducerFlags = 0u;
            _lastTelemetryExtraFlags = 0u;
            _lastCounters = default;
            _resourceRefreshRequested = nextVault != null && isActiveAndEnabled;
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

        private void TryRegisterDispatcherSystems()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (_simulationPhaseSystem == null)
                _simulationPhaseSystem = new SimulationPhaseSystem(this);
            if (_visualSyncPhaseSystem == null)
                _visualSyncPhaseSystem = new VisualSyncPhaseSystem(this);

            if (!_registeredSimulationPhase && GlobalRegistry.TryRegisterDispatcherSystem(_simulationPhaseSystem))
                _registeredSimulationPhase = true;
            if (!_registeredVisualSyncPhase && GlobalRegistry.TryRegisterDispatcherSystem(_visualSyncPhaseSystem))
                _registeredVisualSyncPhase = true;
        }

        private void TryUnregisterDispatcherSystems()
        {
            if (_registeredVisualSyncPhase)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_visualSyncPhaseSystem);
                _registeredVisualSyncPhase = false;
            }

            if (_registeredSimulationPhase)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_simulationPhaseSystem);
                _registeredSimulationPhase = false;
            }
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

        private bool HasGpuBuffersReady(int capacity)
        {
            int stride = UnsafeUtility.SizeOf<ShadowCullStateDTO>();
            int argsStride = UnsafeUtility.SizeOf<ShadowCullIndirectArgsDTO>();
            return _stateUploadBufferA != null && _stateUploadBufferA.count >= capacity && _stateUploadBufferA.stride == stride &&
                   _stateUploadBufferB != null && _stateUploadBufferB.count >= capacity && _stateUploadBufferB.stride == stride &&
                   _indirectArgsBufferA != null && _indirectArgsBufferA.count >= 1 && _indirectArgsBufferA.stride == argsStride &&
                   _indirectArgsBufferB != null && _indirectArgsBufferB.count >= 1 && _indirectArgsBufferB.stride == argsStride;
        }

        private void EnsureGpuBuffersCold(int capacity)
        {
            if (HasGpuBuffersReady(capacity))
                return;

            int stride = UnsafeUtility.SizeOf<ShadowCullStateDTO>();
            int argsStride = UnsafeUtility.SizeOf<ShadowCullIndirectArgsDTO>();

            ReleaseGpuBuffers();
            _stateUploadBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, capacity, stride);
            _stateUploadBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, capacity, stride);
            _indirectArgsBufferA = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, argsStride);
            _indirectArgsBufferB = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, argsStride);
        }

        private void ReleaseGpuBuffers()
        {
            if (_stateUploadBufferA != null)
            {
                _stateUploadBufferA.Dispose();
                _stateUploadBufferA = null;
            }

            if (_stateUploadBufferB != null)
            {
                _stateUploadBufferB.Dispose();
                _stateUploadBufferB = null;
            }

            if (_indirectArgsBufferA != null)
            {
                _indirectArgsBufferA.Dispose();
                _indirectArgsBufferA = null;
            }

            if (_indirectArgsBufferB != null)
            {
                _indirectArgsBufferB.Dispose();
                _indirectArgsBufferB = null;
            }

            _publishedStateBuffer = null;
            _publishedIndirectArgsBuffer = null;
        }

        private JobHandle ScheduleCullingPass(IDataVault vault, uint frame, JobHandle dependsOn)
        {
            if (_jobPending)
                return dependsOn;

            if (!TryLockJobBuffers(vault))
            {
                RecordTelemetry(vault, frame, TelemetryFlagVaultLockFailed, 0u, 0f);
                return dependsOn;
            }

            bool keepJobPins = false;
            try
            {
            int instanceCapacity = math.max(1, _instanceCapacity);
            if (!TryOpenVaultBuffer(vault, ref _instanceHandle, AbyssalShadowBufferIds.Instances, instanceCapacity, out NativeArray<ShadowCullInstanceDTO> instances) ||
                !TryOpenVaultBuffer(vault, ref _stateHandle, AbyssalShadowBufferIds.States, instanceCapacity, out NativeArray<ShadowCullStateDTO> states) ||
                !TryOpenVaultBuffer(vault, ref _illuminationHandle, AbyssalShadowBufferIds.IlluminationScalars, instanceCapacity, out NativeArray<float> illumination) ||
                !TryOpenVaultBuffer(vault, ref _frustumHandle, AbyssalShadowBufferIds.FrustumPlanes, AbyssalShadowCullingConstants.FrustumPlaneCount, out NativeArray<float4> planes) ||
                !TryOpenVaultBuffer(vault, ref _counterHandle, AbyssalShadowBufferIds.Counters, 1, out NativeArray<ShadowCullCountersDTO> counters) ||
                !TryOpenVaultBuffer(vault, ref _runtimeHandle, AbyssalShadowBufferIds.RuntimeState, 1, out NativeArray<AbyssalShadowRuntimeStateDTO> runtimeArray) ||
                !TryOpenVaultBuffer(vault, ref _profileRuleHandle, AbyssalShadowBufferIds.ProfileRules, AbyssalShadowCullingConstants.ProfileRuleCapacity, out NativeArray<ShadowCullProfileRuleDTO> profileRules) ||
                !TryOpenVaultBuffer(vault, ref _hzbTileHandle, AbyssalShadowBufferIds.HzbDepthTiles, AbyssalShadowCullingConstants.HzbTileCapacity, out NativeArray<ShadowCullHzbTileDTO> hzbTiles) ||
                !TryOpenVaultBuffer(vault, ref _indirectArgsHandle, AbyssalShadowBufferIds.IndirectArgs, 1, out NativeArray<ShadowCullIndirectArgsDTO> indirectArgs))
            {
                _initialized = false;
                _resourceRefreshRequested = true;
                return dependsOn;
            }

            AbyssalShadowRuntimeStateDTO runtime = runtimeArray[0];
            int requestedCount = _externalActiveInstanceCount > 0
                ? _externalActiveInstanceCount
                : (runtime.ActiveInstanceCount > 0 ? runtime.ActiveInstanceCount : _instanceCapacity);
            int count = math.min(math.max(1, requestedCount), math.min(instances.Length, states.Length));
            int activeProfileRuleCount = CountActiveProfileRules(profileRules);
            bool regenerateMockData = !_mockSeeded || _requestMockRegenerate;
            bool regenerateHzbData = !_hzbSeeded || _requestMockRegenerate;
            float quality = ResolveGlobalQualityWeight(in runtime);
            int hzbResolution = ResolveHzbGridResolution(quality);
            int requestedHzbTileCount = _externalHzbTileCount > 0
                ? _externalHzbTileCount
                : hzbResolution * hzbResolution;
            int hzbTileCount = math.min(hzbTiles.Length, math.max(1, requestedHzbTileCount));
            runtime.BaseShadowDistanceMeters = math.clamp(runtime.BaseShadowDistanceMeters > 0f ? runtime.BaseShadowDistanceMeters : _baseShadowDistanceMeters, 20f, 300f);
            runtime.DitherFadeBand01 = math.clamp(runtime.DitherFadeBand01 > 0f ? runtime.DitherFadeBand01 : _ditherFadeBand01, 0.001f, 0.5f);
            runtime.DarknessThreshold = math.saturate(runtime.DarknessThreshold);
            runtime.ActiveInstanceCount = count;
            runtime.Frame = frame;
            runtime.GlobalQualityWeight = quality;
            runtime.DirectionalLightDirection = ResolveDirectionalLight();
            runtime.PointLightUltraThreshold = math.clamp(_pointLightUltraThreshold, 0.7f, 1f);
            runtime.MaxShadowDistanceMeters = math.lerp(AbyssalShadowCullingConstants.MinimumShadowDistanceMeters, runtime.BaseShadowDistanceMeters, quality);
            runtime.MinCasterRadiusMeters = math.lerp(
                AbyssalShadowCullingConstants.DefaultShadowCasterRadiusLow,
                AbyssalShadowCullingConstants.DefaultShadowCasterRadiusUltra,
                quality);
            runtimeArray[0] = runtime;
            _lastMaxShadowDistanceMeters = runtime.MaxShadowDistanceMeters;

            uint producerFlags = _registeredProducerFlags;
            JobHandle producerDependency = _registeredProducerDependency;
            _registeredProducerDependency = default;
            _registeredProducerFlags = 0u;
            _externalActiveInstanceCount = 0;
            _externalHzbTileCount = 0;
            JobHandle handle = JobHandle.CombineDependencies(dependsOn, producerDependency);
            if (regenerateMockData)
            {
                handle = new GenerateMockCullingDataJob
                {
                    Instances = instances,
                    States = states,
                    IlluminationScalars = illumination,
                    OriginAUP = _cameraAUP,
                    Count = count,
                    Seed = 0x134C011Du
                }.Schedule(count, BatchSize, handle);
                _mockSeeded = true;
                _requestMockRegenerate = false;
            }

            if (regenerateHzbData)
            {
                handle = new GenerateMockHzbTilesJob
                {
                    HzbTiles = hzbTiles,
                    NearDepthMeters = math.max(4f, runtime.MaxShadowDistanceMeters * 0.12f),
                    FarDepthMeters = math.max(24f, runtime.MaxShadowDistanceMeters * 1.1f),
                    GlobalQualityWeight = quality,
                    TileCount = hzbTileCount,
                    GridResolution = hzbResolution,
                    Seed = 0x1347A11Du
                }.Schedule(hzbTileCount, 32, handle);
                _hzbSeeded = true;
            }

            handle = new EvaluateShadowCullingJob
            {
                Instances = instances,
                IlluminationScalars = illumination,
                LocalFrustumPlanes = planes,
                ProfileRules = profileRules,
                HzbTiles = hzbTiles,
                States = states,
                CameraAUP = _cameraAUP,
                DirectionalLightDirection = runtime.DirectionalLightDirection,
                GlobalQualityWeight = quality,
                BaseShadowDistanceMeters = runtime.BaseShadowDistanceMeters,
                DarknessThreshold = runtime.DarknessThreshold,
                DitherFadeBand01 = runtime.DitherFadeBand01,
                MinCasterRadiusAtFullQuality = AbyssalShadowCullingConstants.DefaultShadowCasterRadiusUltra,
                MaxCasterRadiusAtMinQuality = AbyssalShadowCullingConstants.DefaultShadowCasterRadiusLow,
                DirectionalShadowReachMeters = AbyssalShadowCullingConstants.DefaultDirectionalShadowReachMeters,
                PointLightUltraThreshold = runtime.PointLightUltraThreshold,
                HzbWorldSpanMeters = math.max(1f, runtime.MaxShadowDistanceMeters * 2f),
                HzbViewRight = ResolveFiniteDirection(_hzbViewRight, new float3(1f, 0f, 0f)),
                HzbViewUp = ResolveFiniteDirection(_hzbViewUp, new float3(0f, 1f, 0f)),
                HzbViewForward = ResolveFiniteDirection(_hzbViewForward, new float3(0f, 0f, 1f)),
                InstanceCount = count,
                ProfileRuleCount = activeProfileRuleCount,
                HzbTileCount = hzbTileCount,
                HzbGridResolution = hzbResolution
            }.Schedule(count, BatchSize, handle);

            handle = new ReduceShadowCullTelemetryJob
            {
                States = states,
                Counters = counters,
                Count = count,
                ProfileRuleCount = (uint)activeProfileRuleCount
            }.Schedule(handle);

            handle = new BuildShadowIndirectArgsJob
            {
                Counters = counters,
                IndirectArgs = indirectArgs,
                VertexCountPerInstance = 1u,
                StartVertex = 0u,
                StartInstance = 0u,
                StartIndex = 0u
            }.Schedule(handle);

            _cullingHandle = handle;
            _scheduleTimestamp = Stopwatch.GetTimestamp();
            _scheduledFrame = frame;
            _scheduledInstanceCount = count;
            _lastTelemetryExtraFlags |= producerFlags;
            _jobPending = true;
            keepJobPins = true;
            return handle;
            }
            finally
            {
                if (!keepJobPins)
                    UnlockJobBuffers();
            }
        }

        private bool TryFinalizePendingJobNoWait(uint frame)
        {
            if (!_jobPending)
                return true;

            if (!_cullingHandle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _cullingHandle))
            {
                return false;
            }

            return CommitCompletedJob(frame);
        }

        private bool CompletePendingJobForBarrier(uint frame)
        {
            if (!_jobPending)
                return true;

            if (!ForceCompleteCullingJobInPostSimulationWindow())
                return false;

            return CommitCompletedJob(frame);
        }

        private bool ForceCompleteCullingJobInPostSimulationWindow()
        {
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                return DispatcherJobFence.TryComplete(ref _cullingHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        private bool CommitCompletedJob(uint frame)
        {
            _jobPending = false;
            IDataVault vault = _dataVault;
            UnlockJobBuffers();

            long elapsedTicks = Stopwatch.GetTimestamp() - _scheduleTimestamp;
            double tickMs = Stopwatch.Frequency > 0 ? elapsedTicks * 1000.0 / Stopwatch.Frequency : 0.0;
            _lastBurstWallTimeMs = (float)math.max(0.0, tickMs);
            if (vault == null || !_initialized)
            {
                _resourceRefreshRequested = true;
                return true;
            }

            uint uploaded = UploadCompletedState(vault, _scheduledInstanceCount);
            RecordTelemetry(vault, _scheduledFrame == 0u ? frame : _scheduledFrame, TelemetryFlagGpuUploaded, uploaded, _lastBurstWallTimeMs);
            return true;
        }

        private uint UploadCompletedState(IDataVault vault, int count)
        {
            if (!TryOpenVaultBuffer(vault, ref _stateHandle, AbyssalShadowBufferIds.States, math.max(1, _instanceCapacity), out NativeArray<ShadowCullStateDTO> states) ||
                !TryOpenVaultBuffer(vault, ref _indirectArgsHandle, AbyssalShadowBufferIds.IndirectArgs, 1, out NativeArray<ShadowCullIndirectArgsDTO> indirectArgs) ||
                !TryOpenVaultBuffer(vault, ref _runtimeHandle, AbyssalShadowBufferIds.RuntimeState, 1, out NativeArray<AbyssalShadowRuntimeStateDTO> runtimeArray) ||
                count <= 0)
                return 0u;

            if (!HasGpuBuffersReady(count))
            {
                _resourceRefreshRequested = true;
                return 0u;
            }

            bool writeBufferA = _uploadBufferFlip;
            GraphicsBuffer target = writeBufferA ? _stateUploadBufferA : _stateUploadBufferB;
            GraphicsBuffer indirectTarget = writeBufferA ? _indirectArgsBufferA : _indirectArgsBufferB;
            _uploadBufferFlip = !_uploadBufferFlip;
            if (target == null || indirectTarget == null)
                return 0u;

            int uploadCount = math.min(count, states.Length);
            long uploadStart = Stopwatch.GetTimestamp();
            NativeArray<ShadowCullStateDTO> mapped = target.LockBufferForWrite<ShadowCullStateDTO>(0, uploadCount);
            try
            {
                void* destination = mapped.GetUnsafePtr();
                void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(states);
                UnsafeUtility.MemCpy(destination, source, uploadCount * AbyssalShadowCullingConstants.ShadowCullStateStrideBytes);
            }
            finally
            {
                target.UnlockBufferAfterWrite<ShadowCullStateDTO>(uploadCount);
            }

            NativeArray<ShadowCullIndirectArgsDTO> mappedArgs = indirectTarget.LockBufferForWrite<ShadowCullIndirectArgsDTO>(0, 1);
            try
            {
                void* argsDestination = mappedArgs.GetUnsafePtr();
                void* argsSource = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(indirectArgs);
                UnsafeUtility.MemCpy(argsDestination, argsSource, UnsafeUtility.SizeOf<ShadowCullIndirectArgsDTO>());
            }
            finally
            {
                indirectTarget.UnlockBufferAfterWrite<ShadowCullIndirectArgsDTO>(1);
            }
            long uploadTicks = Stopwatch.GetTimestamp() - uploadStart;
            _lastUploadMicroseconds = Stopwatch.Frequency > 0 ? (float)(uploadTicks * 1000000.0 / Stopwatch.Frequency) : 0f;

            _publishedStateBuffer = target;
            _publishedIndirectArgsBuffer = indirectTarget;
            Shader.SetGlobalBuffer(ShadowCullStatesShaderId, _publishedStateBuffer);
            Shader.SetGlobalBuffer(ShadowCullIndirectArgsShaderId, _publishedIndirectArgsBuffer);
            Shader.SetGlobalInt(ShadowCullCountShaderId, uploadCount);

            AbyssalShadowRuntimeStateDTO runtime = runtimeArray[0];
            runtime.LastUploadCount = (uint)uploadCount;
            runtimeArray[0] = runtime;
            Shader.SetGlobalFloat(ShadowCullQualityShaderId, runtime.GlobalQualityWeight);
            return (uint)uploadCount;
        }

        private void RecordTelemetry(IDataVault vault, uint frame, uint extraFlags, uint uploadedCount, float burstWallTimeMs)
        {
            if (!TryOpenVaultBuffer(vault, ref _telemetryHandle, AbyssalShadowBufferIds.TelemetryRing, AbyssalShadowCullingConstants.TelemetryCapacity, out NativeArray<CullingTelemetryEntry> telemetry))
                return;

            bool hasCounters = TryOpenVaultBuffer(vault, ref _counterHandle, AbyssalShadowBufferIds.Counters, 1, out NativeArray<ShadowCullCountersDTO> countersArray);
            bool hasRuntime = TryOpenVaultBuffer(vault, ref _runtimeHandle, AbyssalShadowBufferIds.RuntimeState, 1, out NativeArray<AbyssalShadowRuntimeStateDTO> runtimeArray);
            ShadowCullCountersDTO counters = hasCounters ? countersArray[0] : default;
            AbyssalShadowRuntimeStateDTO runtime = hasRuntime ? runtimeArray[0] : default;
            _lastCounters = counters;
            uint stateHash = counters.StateHash;
            uint faultFlags = counters.Flags & TelemetryFlagNonFinite;
            uint telemetryIndex = frame % (uint)telemetry.Length;
            CullingTelemetryEntry entry = default;
            entry.Frame = frame;
            entry.EvaluatedCount = counters.EvaluatedCount;
            entry.MainCulledCount = counters.MainCulledCount;
            entry.ShadowCulledCount = counters.ShadowCulledCount;
            entry.DarknessCulledCount = counters.DarknessCulledCount;
            entry.PointLightCulledCount = counters.PointLightCulledCount;
            entry.UploadedCount = uploadedCount;
            entry.Flags = faultFlags | extraFlags | _lastTelemetryExtraFlags;
            entry.BurstWallTimeMs = math.max(0f, burstWallTimeMs);
            entry.UploadMicroseconds = math.max(0f, _lastUploadMicroseconds);
            entry.GlobalQualityWeight = runtime.GlobalQualityWeight;
            entry.MaxShadowDistanceMeters = runtime.MaxShadowDistanceMeters;
            entry.StateHash = stateHash == 0u ? 1u : stateHash;
            entry.NonFiniteHash = (faultFlags & TelemetryFlagNonFinite) != 0u ? entry.StateHash : 0u;
            entry.ShadowOnlyCount = counters.ShadowOnlyCount;
            entry.DitheredCount = counters.DitheredCount;
            telemetry[(int)telemetryIndex] = entry;
            _lastTelemetryExtraFlags = 0u;

            if ((faultFlags & TelemetryFlagNonFinite) != 0u)
            {
                AbyssalShadowDumpWriter.DumpTelemetry(DumpPath, telemetry);
                _lastTelemetryExtraFlags |= TelemetryFlagDumped;
            }
        }

        private bool TryLockJobBuffers(IDataVault vault)
        {
            if (_jobPinsHeld)
                return true;
            if (vault == null)
                return false;

            _jobPinVault = vault;
            try
            {
                if (!TryLockJobBuffer(vault, AbyssalShadowBufferIds.Instances, JobPinInstances) ||
                    !TryLockJobBuffer(vault, AbyssalShadowBufferIds.States, JobPinStates) ||
                    !TryLockJobBuffer(vault, AbyssalShadowBufferIds.IlluminationScalars, JobPinIlluminationScalars) ||
                    !TryLockJobBuffer(vault, AbyssalShadowBufferIds.FrustumPlanes, JobPinFrustumPlanes) ||
                    !TryLockJobBuffer(vault, AbyssalShadowBufferIds.ProfileRules, JobPinProfileRules) ||
                    !TryLockJobBuffer(vault, AbyssalShadowBufferIds.Counters, JobPinCounters) ||
                    !TryLockJobBuffer(vault, AbyssalShadowBufferIds.HzbDepthTiles, JobPinHzbDepthTiles) ||
                    !TryLockJobBuffer(vault, AbyssalShadowBufferIds.IndirectArgs, JobPinIndirectArgs))
                    return false;

                _jobPinsHeld = true;
                return true;
            }
            finally
            {
                if (!_jobPinsHeld)
                    UnlockJobBuffers();
            }
        }

        private void UnlockJobBuffers()
        {
            IDataVault vault = _jobPinVault;
            uint pinMask = _jobPinMask;
            _jobPinVault = null;
            _jobPinMask = 0u;
            _jobPinsHeld = false;
            if (vault == null || pinMask == 0u)
                return;

            TryUnlockJobBuffer(vault, pinMask, JobPinIndirectArgs, AbyssalShadowBufferIds.IndirectArgs);
            TryUnlockJobBuffer(vault, pinMask, JobPinHzbDepthTiles, AbyssalShadowBufferIds.HzbDepthTiles);
            TryUnlockJobBuffer(vault, pinMask, JobPinCounters, AbyssalShadowBufferIds.Counters);
            TryUnlockJobBuffer(vault, pinMask, JobPinProfileRules, AbyssalShadowBufferIds.ProfileRules);
            TryUnlockJobBuffer(vault, pinMask, JobPinFrustumPlanes, AbyssalShadowBufferIds.FrustumPlanes);
            TryUnlockJobBuffer(vault, pinMask, JobPinIlluminationScalars, AbyssalShadowBufferIds.IlluminationScalars);
            TryUnlockJobBuffer(vault, pinMask, JobPinStates, AbyssalShadowBufferIds.States);
            TryUnlockJobBuffer(vault, pinMask, JobPinInstances, AbyssalShadowBufferIds.Instances);
        }

        private bool TryLockJobBuffer(IDataVault vault, BufferID bufferId, uint pinBit)
        {
            if ((_jobPinMask & pinBit) != 0u)
                return true;

            if (vault == null || !vault.TryLockBuffer(bufferId, OwnerSystemId))
                return false;

            _jobPinMask |= pinBit;
            return true;
        }

        private static void TryUnlockJobBuffer(IDataVault vault, uint pinMask, uint pinBit, BufferID bufferId)
        {
            if ((pinMask & pinBit) != 0u)
                vault.TryUnlockBuffer(bufferId, OwnerSystemId);
        }

        private float ResolveGlobalQualityWeight(in AbyssalShadowRuntimeStateDTO runtime)
        {
            if (math.isfinite(runtime.GlobalQualityWeightOverride) && runtime.GlobalQualityWeightOverride >= 0f)
                return math.saturate(runtime.GlobalQualityWeightOverride);

            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(weight) ? weight : 1f);
        }

        private static int ResolveHzbGridResolution(float qualityWeight)
        {
            float q = math.saturate(math.isfinite(qualityWeight) ? qualityWeight : 1f);
            float smooth = q * q * (3f - 2f * q);
            int resolution = (int)math.round(math.lerp(8f, AbyssalShadowCullingConstants.HzbGridResolution, smooth));
            return math.clamp(resolution, 1, AbyssalShadowCullingConstants.HzbGridResolution);
        }

        private static int CountActiveProfileRules(NativeArray<ShadowCullProfileRuleDTO> profileRules)
        {
            if (!profileRules.IsCreated)
                return 0;

            int count = 0;
            for (int i = 0; i < profileRules.Length; i++)
            {
                if (profileRules[i].ProfileHash != 0u)
                    count = i + 1;
            }

            return count;
        }

        private uint ResolveDeterministicFrame(IDataVault vault, uint dispatcherFrame, bool advanceFallback)
        {
            if (dispatcherFrame != 0u)
                return dispatcherFrame;

            uint current = _scheduledFrame;
            if (vault != null &&
                TryOpenVaultBuffer(vault, ref _runtimeHandle, AbyssalShadowBufferIds.RuntimeState, 1, out NativeArray<AbyssalShadowRuntimeStateDTO> runtimeArray))
            {
                current = runtimeArray[0].Frame;
            }

            if (!advanceFallback && current != 0u)
                return current;

            uint next = current == uint.MaxValue ? 1u : current + 1u;
            return next == 0u ? 1u : next;
        }

        private uint ResolveTelemetryFrame(IDataVault vault)
        {
            if (_scheduledFrame != 0u)
                return _scheduledFrame;

            return ResolveDeterministicFrame(vault, 0u, false);
        }

        private float3 ResolveDirectionalLight()
        {
            ICelestialLightReadabilityReadModel readModel = _celestialLightReadModel;
            CelestialLightReadabilitySnapshot light = default;
            if (IsCelestialLightReadModelUsable(readModel))
            {
                light = readModel.LightReadabilitySnapshot;
            }
            else
            {
                CacheCelestialLightReadModel(GlobalRegistry.CelestialLightReadabilityReadModel);
                readModel = _celestialLightReadModel;
                if (IsCelestialLightReadModelUsable(readModel))
                    light = readModel.LightReadabilitySnapshot;
            }

            if ((light.Flags & (uint)CelestialLightReadabilityFlags.Valid) != 0u &&
                math.all(math.isfinite(light.SunDirection)) &&
                math.lengthsq(light.SunDirection) > 0.000001f)
            {
                return math.normalizesafe(-light.SunDirection, new float3(-0.35f, -0.72f, -0.25f));
            }

            float3 value = new float3(_directionalLightDirection.x, _directionalLightDirection.y, _directionalLightDirection.z);
            if (!math.all(math.isfinite(value)) || math.lengthsq(value) < 0.000001f)
                value = new float3(-0.35f, -0.72f, -0.25f);
            return math.normalizesafe(value, new float3(-0.35f, -0.72f, -0.25f));
        }

        private void CacheCelestialLightReadModel(ICelestialLightReadabilityReadModel readModel)
        {
            if (IsCelestialLightReadModelUsable(readModel))
            {
                _celestialLightReadModel = readModel;
                return;
            }

            ICelestialLightReadabilityReadModel fallback = GlobalRegistry.CelestialLightReadabilityReadModel;
            _celestialLightReadModel = IsCelestialLightReadModelUsable(fallback) ? fallback : null;
        }

        private static bool IsCelestialLightReadModelUsable(ICelestialLightReadabilityReadModel readModel)
        {
            if (readModel == null)
                return false;

            if (readModel is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private static float3 ResolveFiniteDirection(Vector3 value, float3 fallback)
        {
            float3 vector = new float3(value.x, value.y, value.z);
            if (!math.all(math.isfinite(vector)) || math.lengthsq(vector) < 0.000001f)
                return fallback;
            return math.normalizesafe(vector, fallback);
        }

        private static Vector3 ToFiniteVector3(float3 value)
        {
            if (!math.all(math.isfinite(value)))
                value = new float3(0f, 0f, 1f);
            return new Vector3(value.x, value.y, value.z);
        }

        private string ResolveCsvPath()
        {
            string configured = GetProfileCsvPath();
            if (Path.IsPathRooted(configured))
                return configured;

            string projectRoot = Directory.GetCurrentDirectory();
            return Path.Combine(projectRoot, configured);
        }

        private sealed class SimulationPhaseSystem : IDispatcherSystem
        {
            private readonly AbyssalShadowCullingRuntime _owner;

            public SimulationPhaseSystem(AbyssalShadowCullingRuntime owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => SystemHash ^ 0x51510000u;

            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.Simulation;

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
                return _owner != null
                    ? _owner.ScheduleSimulationPhase(in timing, in context, dependsOn)
                    : dependsOn;
            }

            public void PostSimulationTick(in DispatcherTimingDTO timing)
            {
            }

            public void VisualSyncTick(in DispatcherTimingDTO timing)
            {
            }
        }

        private sealed class VisualSyncPhaseSystem : IDispatcherSystem
        {
            private readonly AbyssalShadowCullingRuntime _owner;

            public VisualSyncPhaseSystem(AbyssalShadowCullingRuntime owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => SystemHash ^ 0xA11CE000u;

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
                if (_owner != null)
                    _owner.CommitVisualSyncPhase(in timing);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_editorDrawGizmos || !_initialized)
                return;

            IDataVault vault = ResolveVault();
            if (vault == null)
                return;

            if (!TryOpenVaultBuffer(vault, ref _instanceHandle, AbyssalShadowBufferIds.Instances, math.max(1, _instanceCapacity), out NativeArray<ShadowCullInstanceDTO> instances) ||
                !TryOpenVaultBuffer(vault, ref _stateHandle, AbyssalShadowBufferIds.States, math.max(1, _instanceCapacity), out NativeArray<ShadowCullStateDTO> states))
                return;

            int count = math.min(math.min(_gizmoBoxLimit, _scheduledInstanceCount), math.min(instances.Length, states.Length));
            for (int i = 0; i < count; i++)
            {
                ShadowCullStateDTO state = states[i];
                ShadowCullInstanceDTO instance = instances[i];
                if ((state.CullFlags & AbyssalShadowCullFlags.CastShadows) != 0u &&
                    (state.CullFlags & AbyssalShadowCullFlags.MainVisible) != 0u)
                {
                    Gizmos.color = Color.green;
                }
                else if ((state.CullFlags & AbyssalShadowCullFlags.ShadowOnly) != 0u)
                {
                    Gizmos.color = Color.yellow;
                }
                else
                {
                    Gizmos.color = Color.red;
                }

                float3 local = AupPrecisionMath.LocalDeltaFloat3(instance.CenterAUP, _cameraAUP, float3.zero);
                Vector3 center = new Vector3(local.x, local.y, local.z);
                Vector3 size = new Vector3(instance.Extents.x * 2f, instance.Extents.y * 2f, instance.Extents.z * 2f);
                Gizmos.DrawWireCube(center, size);
            }
        }
#endif
    }
}
