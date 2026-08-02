using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Rendering
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9209)]
    public sealed unsafe class AbyssalDeferredCausticsRuntime : MonoBehaviour, ICausticsService, ILateFrameTickable, IOriginShiftListener, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private const SystemID OwnerSystemId = SystemID.GraphicsScalability;
        private const string DumpPath = "Docs/AgentLogs/Dump_13KRA.bin";
        private const string BlackBoxDumpPayloadLabel = "abyssalCausticsBlackBoxDumpPayload";
        private const float CausticsMinimumWavelength = 0.25f;

        private static AbyssalDeferredCausticsRuntime s_runtimeInstance;
        private static AbyssalDeferredCausticsRuntime s_publishedRuntime;
        private static GraphicsBuffer s_publishedConstantBuffer;
        private static uint s_publishedConstantBufferFrameIndex;

        private IDataVault _dataVault;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IWeatherService _weatherService;
        private ICelestialLightReadabilityReadModel _celestialLightReadModel;
        private VaultGenerationHandle<CausticsParametersDTO> _parametersHandle;
        private VaultGenerationHandle<CausticsTuningDTO> _tuningHandle;
        private VaultGenerationHandle<CausticsTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<CausticsLightingProfileDTO> _profilesHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<float4> _surfaceSwellInputHandle;
        private GraphicsBuffer _constantBufferA;
        private GraphicsBuffer _constantBufferB;
        private GraphicsBuffer _activeConstantBuffer;
        private string _blackBoxDumpPath;
        private string _blackBoxDumpDirectory;
        private float _presentationTimeSeconds;
        private int _activeConstantBufferIndex;
        private int _tickCount;
        private int _telemetryWriteCursor;
        private uint _presentationFrameIndex;
        private uint _activeConstantBufferFrameIndex;
        private uint _lastFaultFlags;
        private bool _isInitialized;
        private bool _ownsRegistrySlot;
        private bool _registeredLateFrame;
        private bool _registeredOriginShift;
        private bool _registeredHotSwap;
        private bool _runtimeOwnerAborted;
        private bool _pendingGpuUpload;
        private bool _tuningSeeded;
        private bool _profilesSeeded;
        private bool _telemetrySeeded;
        private bool _telemetryCursorSeeded;
        private bool _vaultStateReady;
        private bool _faultDumped;

        public ServiceHeartbeatState HeartbeatState => _isInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;
        public bool IsServiceReady => _isInitialized;
        public int TickCount => _tickCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ShutdownRuntimeInstanceForEditorReload();
            s_runtimeInstance = null;
            s_publishedRuntime = null;
            s_publishedConstantBuffer = null;
            s_publishedConstantBufferFrameIndex = 0u;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorReloadHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ShutdownRuntimeInstanceForEditorReload;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ShutdownRuntimeInstanceForEditorReload;
            UnityEditor.EditorApplication.quitting -= ShutdownRuntimeInstanceForEditorReload;
            UnityEditor.EditorApplication.quitting += ShutdownRuntimeInstanceForEditorReload;
        }
#endif

        private static void ShutdownRuntimeInstanceForEditorReload()
        {
            AbyssalDeferredCausticsRuntime runtime = s_runtimeInstance;
            if (runtime != null)
                runtime.ShutdownServiceState();
        }

        public static AbyssalDeferredCausticsRuntime EnsureRuntimeInstance()
        {
            AbyssalDeferredCausticsRuntime runtime = ResolveUsableRuntime();
            if (runtime != null)
                return runtime;

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Screen-space caustics owner must exist when bootstrap reflection path is skipped.
            GameObject runtimeRoot = new GameObject("[AbyssalDeferredCausticsRuntime]"); // COLD ALLOC: GameObject[1] - bootstrap-owned screen-space caustics owner - owner: AbyssalDeferredCausticsRuntime
            return runtimeRoot.AddComponent<AbyssalDeferredCausticsRuntime>();
        }

        public void InitializeService()
        {
            if (!EnsureSingletonOwnership())
                return;

            EnsureBlackBoxDumpPathCold();
            CacheRegistryServicesCold(forceRefresh: true);
            TryRegisterHotSwap();
            EnsureVaultState();
            EnsureCsvScratch();

            bool layoutValid = CausticsParametersLayoutValidator.Validate();
            bool constantBuffersReady = layoutValid && EnsureConstantBuffers();
            _isInitialized = layoutValid && constantBuffersReady;
            if (!_isInitialized)
            {
                _lastFaultFlags = layoutValid
                    ? AbyssalCausticsConstants.FaultConstantBufferUnavailable
                    : AbyssalCausticsConstants.FaultLayout;
                TryUnregisterLateFrame();
                TryUnregisterOriginShift();
                DumpBlackBox();
                return;
            }

            TryRegisterLateFrame();
            TryRegisterOriginShift();
            RunMockLightingKernel();
        }

        private void AdvanceCausticsFrameState(float deltaTime)
        {
            _tickCount++;
            if (!_isInitialized || !_ownsRegistrySlot)
                return;

            float safeDeltaTime = math.select(deltaTime, 0f, !math.isfinite(deltaTime) || deltaTime < 0f);
            _presentationTimeSeconds += math.min(safeDeltaTime, 0.25f);
            _presentationFrameIndex++;

            if (!_vaultStateReady)
                return;

            CausticsParametersDTO telemetryParameters = default;
            bool recordTelemetry = false;
            double3 cameraAupLocal = ResolveCameraAupLocalOffset();
            float quality = ResolveGlobalQualityWeight01();
            NativeArray<float4> surfaceSwell = default;
            bool hasTuning = TryResolveVaultBuffer(
                in _tuningHandle,
                BufferID.ShinobuCausticsTuning,
                1,
                out NativeArray<CausticsTuningDTO> tuning);
            bool hasProfiles = TryResolveVaultBuffer(
                in _profilesHandle,
                BufferID.ShinobuCausticsProfiles,
                AbyssalCausticsConstants.ProfileCapacity,
                out NativeArray<CausticsLightingProfileDTO> profiles);
            if (!hasTuning || !hasProfiles)
            {
                _vaultStateReady = false;
                return;
            }

            bool hasWeatherSnapshot = TryResolveWeatherSnapshot(out WeatherRuntimeSnapshot weatherSnapshot);
            TryResolveExternalVaultBuffer(
                in _surfaceSwellInputHandle,
                BufferID.ShinobuOceanSurfaceSwell,
                1,
                out surfaceSwell);

            CausticsInputSnapshotDTO inputSnapshot = CaptureCausticsInputSnapshot(
                tuning,
                hasWeatherSnapshot,
                weatherSnapshot,
                surfaceSwell,
                profiles,
                ResolveCelestialLightReadability(),
                quality,
                _presentationTimeSeconds);

            CalculateCausticParametersJob job = default;
            job.Telemetry = null;
            job.TelemetryLength = 0;
            job.TelemetryCursor = null;
            job.TelemetryCursorLength = 0;
            job.InputSnapshot = inputSnapshot;
            job.CameraAupLocalOffset = cameraAupLocal;
            job.TimeSeconds = _presentationTimeSeconds;
            job.GlobalQualityWeight = inputSnapshot.WeatherStormWindPhaseQuality.w;
            job.FrameIndex = _presentationFrameIndex;
            if (!TryCalculatePendingCausticsParameters(job, out CausticsParametersDTO calculatedParameters))
            {
                _vaultStateReady = false;
                return;
            }

            bool parametersLocked = false;
            try
            {
                if (!TryAcquireVaultWriteBuffer(
                        in _parametersHandle,
                        BufferID.ShinobuCausticsParameters,
                        AbyssalCausticsConstants.ParameterCapacity,
                        out NativeArray<CausticsParametersDTO> parameters))
                {
                    _vaultStateReady = false;
                    return;
                }

                parametersLocked = true;

                if (PublishCalculatedCausticsParameters(in calculatedParameters, parameters))
                {
                    telemetryParameters = calculatedParameters;
                    recordTelemetry = true;
                }
            }
            finally
            {
                if (parametersLocked)
                    ReleaseVaultWriteBuffer(in _parametersHandle, BufferID.ShinobuCausticsParameters);
            }

            if (recordTelemetry)
                RecordCausticsTelemetryOneLock(in telemetryParameters, in inputSnapshot);
        }

        public void LateFrameTick()
        {
            AdvanceCausticsFrameState(SystemDispatcher.CurrentFrameDeltaTime);

            if (!_isInitialized || !_ownsRegistrySlot)
                return;

            if (!_pendingGpuUpload)
                return;

            if (UploadParametersToGpu())
                _pendingGpuUpload = false;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            // Deferred caustics reconstructs from camera depth and AUP-local shader payloads.
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    if (_dataVault == null && previousService is IDataVault previousVault)
                        ReleaseAllVaultHandles(previousVault);

                    BindDataVaultForLifecycle(currentService as IDataVault);
                    EnsureVaultState();
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.Weather:
                    _weatherService = currentService as IWeatherService;
                    break;
                case GlobalRegistryServiceSlot.CelestialEngineRuntime:
                    CacheCelestialLightReadModel(currentService as ICelestialLightReadabilityReadModel);
                    break;
                case GlobalRegistryServiceSlot.CausticsRuntime:
                    _ownsRegistrySlot = ReferenceEquals(currentService, this);
                    if (_ownsRegistrySlot)
                    {
                        s_publishedRuntime = this;
                        if (_activeConstantBuffer != null && _activeConstantBuffer.IsValid())
                        {
                            s_publishedConstantBuffer = _activeConstantBuffer;
                            s_publishedConstantBufferFrameIndex = _activeConstantBufferFrameIndex;
                        }
                    }
                    else if (ReferenceEquals(s_publishedRuntime, this))
                    {
                        s_publishedRuntime = null;
                        ClearPublishedConstantBufferIfOwnedByThis();
                    }

                    break;
            }
        }

        public static bool TryGetActiveConstantBuffer(out GraphicsBuffer constantBuffer, out uint frameIndex)
        {
            GraphicsBuffer buffer = s_publishedConstantBuffer;
            if (buffer != null && buffer.IsValid())
            {
                constantBuffer = buffer;
                frameIndex = s_publishedConstantBufferFrameIndex;
                return true;
            }

            constantBuffer = null;
            frameIndex = 0u;
            return false;
        }

        public static bool TryGetActiveParameters(out CausticsParametersDTO parameters)
        {
            AbyssalDeferredCausticsRuntime runtime = s_publishedRuntime;
            if (runtime != null &&
                runtime.TryReadOnlyVaultBuffer(
                    in runtime._parametersHandle,
                    BufferID.ShinobuCausticsParameters,
                    AbyssalCausticsConstants.ParameterCapacity,
                    out NativeArray<CausticsParametersDTO>.ReadOnly parametersArray))
            {
                parameters = parametersArray[AbyssalCausticsConstants.ActiveParameterIndex];
                return true;
            }

            parameters = default;
            return false;
        }

        public static bool TryGetTuning(out CausticsTuningDTO tuning)
        {
            AbyssalDeferredCausticsRuntime runtime = s_publishedRuntime;
            if (runtime != null &&
                runtime.TryReadOnlyVaultBuffer(
                    in runtime._tuningHandle,
                    BufferID.ShinobuCausticsTuning,
                    1,
                    out NativeArray<CausticsTuningDTO>.ReadOnly tuningArray))
            {
                tuning = tuningArray[0];
                return true;
            }

            tuning = GenerateMockCausticLightingJob.DefaultTuning();
            return false;
        }

        public static bool TrySetEditorTuning(float chromaticDispersion, float noiseScale, float flowSpeedMultiplier, float maxDepthMeters)
        {
            AbyssalDeferredCausticsRuntime runtime = s_publishedRuntime;
            if (runtime == null)
                return false;

            return runtime.TrySetTuningInternal(chromaticDispersion, noiseScale, flowSpeedMultiplier, maxDepthMeters);
        }

#if UNITY_EDITOR
        public static bool TryLoadLightingProfilesCsv(string projectRelativePath)
        {
            AbyssalDeferredCausticsRuntime runtime = s_publishedRuntime;
            return runtime != null && runtime.LoadLightingProfilesCsv(projectRelativePath);
        }

        public bool LoadLightingProfilesCsv(string projectRelativePath)
        {
            EnsureVaultState();
            EnsureCsvScratch();

            string fullPath = BuildProjectPath(projectRelativePath);
            if (!TryLoadLightingProfilesCsvIntoScratch(fullPath, out int byteCount))
                return false;

            return TryParseLightingProfilesFromScratch(byteCount);
        }

        private bool TryLoadLightingProfilesCsvIntoScratch(string fullPath, out int byteCount)
        {
            byteCount = 0;
            if (!TryAcquireVaultWriteBuffer(
                    in _csvScratchHandle,
                    BufferID.ShinobuCausticsCsvScratch,
                    AbyssalCausticsConstants.CsvScratchBytes,
                    out NativeArray<byte> csvScratch))
            {
                return false;
            }

            try
            {
                byteCount = LoadFileBytesIntoScratch(fullPath, csvScratch);
                return byteCount > 0;
            }
            finally
            {
                ReleaseVaultWriteBuffer(in _csvScratchHandle, BufferID.ShinobuCausticsCsvScratch);
            }
        }

        private bool TryParseLightingProfilesFromScratch(int byteCount)
        {
            if (byteCount <= 0 ||
                !TryResolveVaultBuffer(
                    in _csvScratchHandle,
                    BufferID.ShinobuCausticsCsvScratch,
                    AbyssalCausticsConstants.CsvScratchBytes,
                    out NativeArray<byte> csvScratch))
            {
                return false;
            }

            int safeByteCount = math.min(byteCount, csvScratch.Length);
            if (!TryAcquireVaultWriteBuffer(
                    in _profilesHandle,
                    BufferID.ShinobuCausticsProfiles,
                    AbyssalCausticsConstants.ProfileCapacity,
                    out NativeArray<CausticsLightingProfileDTO> profiles))
                return false;

            try
            {
                byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(csvScratch);
                ReadOnlySpan<byte> csvBytes = new ReadOnlySpan<byte>(ptr, safeByteCount);
                int parsed = ParseLightingProfiles(csvBytes, profiles);
                if (parsed <= 0)
                    return false;

                _profilesSeeded = true;
                return true;
            }
            finally
            {
                ReleaseVaultWriteBuffer(in _profilesHandle, BufferID.ShinobuCausticsProfiles);
            }
        }
#endif

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            s_runtimeInstance = this;
            EnsureBlackBoxDumpPathCold();
            CacheRegistryServicesCold(forceRefresh: true);
            TryRegisterHotSwap();
            if (EnsureSingletonOwnership() && CausticsParametersLayoutValidator.Validate())
                EnsureConstantBuffers();
        }

        private void OnEnable()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            s_runtimeInstance = this;
            EnsureBlackBoxDumpPathCold();
            CacheRegistryServicesCold(forceRefresh: false);
            TryRegisterHotSwap();
            if (EnsureSingletonOwnership() && CausticsParametersLayoutValidator.Validate())
                EnsureConstantBuffers();
            if (_isInitialized)
            {
                TryRegisterLateFrame();
                TryRegisterOriginShift();
            }
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            ShutdownServiceState();
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
                return;

            ShutdownServiceState();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!TryGetActiveParameters(out CausticsParametersDTO parameters))
                return;

            Camera camera = Camera.current;
            if (camera == null)
            {
                UnityEditor.SceneView sceneView = UnityEditor.SceneView.lastActiveSceneView;
                camera = sceneView != null ? sceneView.camera : null;
            }

            if (camera == null)
                return;

            Vector3 anchor = camera.cameraToWorldMatrix.GetColumn(3);
            Vector3 direction = new Vector3(parameters.ProjectionVectorAndScale.x, parameters.ProjectionVectorAndScale.y, parameters.ProjectionVectorAndScale.z);
            if (direction.sqrMagnitude < 0.0001f)
                return;

            direction.Normalize();
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(anchor, direction * 8f);
            Gizmos.DrawWireSphere(anchor + direction * 8f, 0.35f);

            float maxDepth = Mathf.Max(1f, parameters.IntensityAndDepthFalloff.z);
            Vector3 planeCenter = new Vector3(anchor.x, anchor.y - maxDepth, anchor.z);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(planeCenter, new Vector3(28f, 0.02f, 28f));
        }
#endif

        private bool EnsureSingletonOwnership()
        {
            if (TryAbortForUsableExistingRuntime())
                return false;

            ICausticsService registered = GlobalRegistry.Caustics;
            if (!ReferenceEquals(registered, null) && !ReferenceEquals(registered, this))
            {
                _ownsRegistrySlot = false;
                if (ReferenceEquals(s_publishedRuntime, this))
                    s_publishedRuntime = null;
                ClearPublishedConstantBufferIfOwnedByThis();

                return false;
            }

            if (!ReferenceEquals(registered, this))
                GlobalRegistry.RegisterCausticsService(this);
            _ownsRegistrySlot = true;
            s_publishedRuntime = this;
            _runtimeOwnerAborted = false;
            return true;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            if (!Application.isPlaying)
                return false;

            ICausticsService registeredService = GlobalRegistry.Caustics;
            if (!ReferenceEquals(registeredService, null) && !ReferenceEquals(registeredService, this))
            {
                AbyssalDeferredCausticsRuntime registeredRuntime = registeredService as AbyssalDeferredCausticsRuntime;
                if (ReferenceEquals(registeredRuntime, null))
                {
                    _runtimeOwnerAborted = true;
                    Destroy(gameObject);
                    return true;
                }

                if (IsCausticsRuntimeUsable(registeredRuntime))
                {
                    s_runtimeInstance = registeredRuntime;
                    s_publishedRuntime = registeredRuntime;
                    _runtimeOwnerAborted = true;
                    Destroy(gameObject);
                    return true;
                }

                registeredRuntime._ownsRegistrySlot = false;
                registeredRuntime.ClearPublishedConstantBufferIfOwnedByThis();
                GlobalRegistry.UnregisterCausticsService(registeredService);
                if (ReferenceEquals(s_runtimeInstance, registeredRuntime))
                    s_runtimeInstance = null;
                if (ReferenceEquals(s_publishedRuntime, registeredRuntime))
                    s_publishedRuntime = null;
            }

            AbyssalDeferredCausticsRuntime active = ResolveUsableRuntime();
            if (ReferenceEquals(active, null) || ReferenceEquals(active, this))
                return false;

            GlobalRegistry.RegisterCausticsService(active);
            active._ownsRegistrySlot = ReferenceEquals(GlobalRegistry.Caustics, active);
            s_runtimeInstance = active;
            s_publishedRuntime = active;
            _runtimeOwnerAborted = true;
            Destroy(gameObject);
            return true;
        }

        private static AbyssalDeferredCausticsRuntime ResolveUsableRuntime()
        {
            if (GlobalRegistry.Caustics is AbyssalDeferredCausticsRuntime registeredRuntime)
            {
                if (IsCausticsRuntimeUsable(registeredRuntime))
                {
                    s_runtimeInstance = registeredRuntime;
                    s_publishedRuntime = registeredRuntime;
                    return registeredRuntime;
                }

                registeredRuntime._ownsRegistrySlot = false;
                registeredRuntime.ClearPublishedConstantBufferIfOwnedByThis();
                GlobalRegistry.UnregisterCausticsService(registeredRuntime);
                if (ReferenceEquals(s_runtimeInstance, registeredRuntime))
                    s_runtimeInstance = null;
                if (ReferenceEquals(s_publishedRuntime, registeredRuntime))
                    s_publishedRuntime = null;
            }

            if (IsCausticsRuntimeUsable(s_publishedRuntime))
                return s_publishedRuntime;
            s_publishedRuntime = null;

            if (IsCausticsRuntimeUsable(s_runtimeInstance))
                return s_runtimeInstance;
            s_runtimeInstance = null;

            return null;
        }

        private static bool IsCausticsRuntimeUsable(AbyssalDeferredCausticsRuntime runtime)
        {
            return !ReferenceEquals(runtime, null) &&
                   runtime != null &&
                   runtime._ownsRegistrySlot &&
                   runtime.isActiveAndEnabled &&
                   !runtime._runtimeOwnerAborted;
        }

        private void EnsureVaultState()
        {
            if (_dataVault == null)
                return;

            if (_vaultStateReady && AreOwnedVaultHandlesCreated())
                return;

            bool hasParameters = EnsureParametersVaultReady();
            bool hasTuning = EnsureTuningVaultReady();
            bool hasTelemetry = EnsureTelemetryVaultReady();
            bool hasTelemetryCursor = EnsureTelemetryCursorVaultReady();
            bool hasProfiles = EnsureProfilesVaultReady();

            RefreshExternalInputHandles();
            _vaultStateReady = hasParameters &&
                               hasTuning &&
                               hasTelemetry &&
                               hasTelemetryCursor &&
                               hasProfiles &&
                               _telemetryCursorSeeded &&
                               _telemetrySeeded &&
                               _tuningSeeded &&
                               _profilesSeeded;
        }

        private bool AreOwnedVaultHandlesCreated()
        {
            return IsOwnedVaultHandle(in _parametersHandle, BufferID.ShinobuCausticsParameters) &&
                   IsOwnedVaultHandle(in _tuningHandle, BufferID.ShinobuCausticsTuning) &&
                   IsOwnedVaultHandle(in _telemetryHandle, BufferID.ShinobuCausticsTelemetryRing) &&
                   IsOwnedVaultHandle(in _telemetryCursorHandle, BufferID.ShinobuCausticsTelemetryCursor) &&
                   IsOwnedVaultHandle(in _profilesHandle, BufferID.ShinobuCausticsProfiles);
        }

        private bool EnsureParametersVaultReady()
        {
            return EnsureOwnedVaultHandleCold(
                BufferID.ShinobuCausticsParameters,
                AbyssalCausticsConstants.ParameterCapacity,
                NativeArrayOptions.UninitializedMemory,
                ref _parametersHandle);
        }

        private bool EnsureTuningVaultReady()
        {
            if (!EnsureOwnedVaultHandleCold(
                    BufferID.ShinobuCausticsTuning,
                    1,
                    NativeArrayOptions.UninitializedMemory,
                    ref _tuningHandle))
                return false;

            if (_tuningSeeded)
                return true;

            if (!TryAcquireVaultWriteBuffer(
                    in _tuningHandle,
                    BufferID.ShinobuCausticsTuning,
                    1,
                    out NativeArray<CausticsTuningDTO> tuning))
                return false;

            try
            {
                SeedTuningIfNeeded(tuning);
                return _tuningSeeded;
            }
            finally
            {
                ReleaseVaultWriteBuffer(in _tuningHandle, BufferID.ShinobuCausticsTuning);
            }
        }

        private bool EnsureTelemetryVaultReady()
        {
            if (!EnsureOwnedVaultHandleCold(
                    BufferID.ShinobuCausticsTelemetryRing,
                    AbyssalCausticsConstants.TelemetryCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    ref _telemetryHandle))
                return false;

            if (_telemetrySeeded)
                return true;

            if (!TryAcquireVaultWriteBuffer(
                    in _telemetryHandle,
                    BufferID.ShinobuCausticsTelemetryRing,
                    AbyssalCausticsConstants.TelemetryCapacity,
                    out NativeArray<CausticsTelemetryEntry> telemetry))
                return false;

            try
            {
                SeedTelemetryIfNeeded(telemetry);
                return _telemetrySeeded;
            }
            finally
            {
                ReleaseVaultWriteBuffer(in _telemetryHandle, BufferID.ShinobuCausticsTelemetryRing);
            }
        }

        private bool EnsureTelemetryCursorVaultReady()
        {
            if (!EnsureOwnedVaultHandleCold(
                    BufferID.ShinobuCausticsTelemetryCursor,
                    1,
                    NativeArrayOptions.UninitializedMemory,
                    ref _telemetryCursorHandle))
                return false;

            if (_telemetryCursorSeeded)
            {
                if (TryResolveVaultBuffer(
                        in _telemetryCursorHandle,
                        BufferID.ShinobuCausticsTelemetryCursor,
                        1,
                        out NativeArray<int> telemetryCursor))
                    _telemetryWriteCursor = WrapTelemetryCursor(telemetryCursor[0], AbyssalCausticsConstants.TelemetryCapacity);
                return true;
            }

            if (!TryAcquireVaultWriteBuffer(
                    in _telemetryCursorHandle,
                    BufferID.ShinobuCausticsTelemetryCursor,
                    1,
                    out NativeArray<int> writableCursor))
                return false;

            try
            {
                _telemetryWriteCursor = 0;
                writableCursor[0] = 0;
                _telemetryCursorSeeded = true;
                return true;
            }
            finally
            {
                ReleaseVaultWriteBuffer(in _telemetryCursorHandle, BufferID.ShinobuCausticsTelemetryCursor);
            }
        }

        private bool EnsureProfilesVaultReady()
        {
            if (!EnsureOwnedVaultHandleCold(
                    BufferID.ShinobuCausticsProfiles,
                    AbyssalCausticsConstants.ProfileCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    ref _profilesHandle))
                return false;

            if (_profilesSeeded)
                return true;

            if (!TryAcquireVaultWriteBuffer(
                    in _profilesHandle,
                    BufferID.ShinobuCausticsProfiles,
                    AbyssalCausticsConstants.ProfileCapacity,
                    out NativeArray<CausticsLightingProfileDTO> profiles))
                return false;

            try
            {
                SeedProfilesIfNeeded(profiles);
                return _profilesSeeded;
            }
            finally
            {
                ReleaseVaultWriteBuffer(in _profilesHandle, BufferID.ShinobuCausticsProfiles);
            }
        }

        private void SeedTuningIfNeeded(NativeArray<CausticsTuningDTO> tuning)
        {
            if (_tuningSeeded || !tuning.IsCreated || tuning.Length < 1)
                return;

            tuning[0] = GenerateMockCausticLightingJob.DefaultTuning();
            _tuningSeeded = true;
        }

        private void SeedTelemetryIfNeeded(NativeArray<CausticsTelemetryEntry> telemetry)
        {
            if (_telemetrySeeded || !telemetry.IsCreated)
                return;

            for (int i = 0; i < telemetry.Length; i++)
                telemetry[i] = default;
            _telemetrySeeded = true;
        }

        private void SeedProfilesIfNeeded(NativeArray<CausticsLightingProfileDTO> profiles)
        {
            if (_profilesSeeded || !profiles.IsCreated)
                return;

            for (int i = 0; i < profiles.Length; i++)
                profiles[i] = default;
            _profilesSeeded = true;
        }

        private void EnsureCsvScratch()
        {
            if (TryResolveVaultBuffer(
                    in _csvScratchHandle,
                    BufferID.ShinobuCausticsCsvScratch,
                    AbyssalCausticsConstants.CsvScratchBytes,
                    out NativeArray<byte> _))
                return;

            EnsureOwnedVaultHandleCold(
                BufferID.ShinobuCausticsCsvScratch,
                AbyssalCausticsConstants.CsvScratchBytes,
                NativeArrayOptions.UninitializedMemory,
                ref _csvScratchHandle);
        }

        private bool EnsureConstantBuffers()
        {
            if (!SystemInfo.supportsSetConstantBuffer)
                return false;

            if (_constantBufferA == null || !_constantBufferA.IsValid())
            {
                _constantBufferA = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    AbyssalCausticsConstants.CBufferBytes); // COLD ALLOC: GraphicsBuffer[64B] - deferred caustics constant buffer A - owner: AbyssalDeferredCausticsRuntime
            }

            if (_constantBufferB == null || !_constantBufferB.IsValid())
            {
                _constantBufferB = new GraphicsBuffer(
                    GraphicsBuffer.Target.Constant,
                    GraphicsBuffer.UsageFlags.LockBufferForWrite,
                    1,
                    AbyssalCausticsConstants.CBufferBytes); // COLD ALLOC: GraphicsBuffer[64B] - deferred caustics constant buffer B - owner: AbyssalDeferredCausticsRuntime
            }

            return _constantBufferA != null && _constantBufferA.IsValid() &&
                   _constantBufferB != null && _constantBufferB.IsValid();
        }

        private bool HasConstantBuffers()
        {
            return _constantBufferA != null && _constantBufferA.IsValid() &&
                   _constantBufferB != null && _constantBufferB.IsValid();
        }

        private bool UploadParametersToGpu()
        {
            if (!TryResolveVaultBuffer(
                    in _parametersHandle,
                    BufferID.ShinobuCausticsParameters,
                    AbyssalCausticsConstants.ParameterCapacity,
                    out NativeArray<CausticsParametersDTO> parameters) ||
                !HasConstantBuffers())
                return false;

            GraphicsBuffer target = _activeConstantBufferIndex == 0 ? _constantBufferA : _constantBufferB;
            _activeConstantBufferIndex ^= 1;
            NativeArray<CausticsParametersDTO> mapped = target.LockBufferForWrite<CausticsParametersDTO>(0, 1);
            try
            {
                void* dst = NativeArrayUnsafeUtility.GetUnsafePtr(mapped);
                void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(parameters);
                UnsafeUtility.MemCpy(dst, src, AbyssalCausticsConstants.CBufferBytes);
            }
            finally
            {
                target.UnlockBufferAfterWrite<CausticsParametersDTO>(1);
            }

            _activeConstantBuffer = target;
            _activeConstantBufferFrameIndex = _presentationFrameIndex;
            if (_ownsRegistrySlot)
            {
                s_publishedConstantBuffer = target;
                s_publishedConstantBufferFrameIndex = _activeConstantBufferFrameIndex;
            }

            return true;
        }

        private void ClearPublishedConstantBufferIfOwnedByThis()
        {
            if (ReferenceEquals(s_publishedConstantBuffer, _constantBufferA) ||
                ReferenceEquals(s_publishedConstantBuffer, _constantBufferB))
            {
                s_publishedConstantBuffer = null;
                s_publishedConstantBufferFrameIndex = 0u;
            }
        }

        private void RunMockLightingKernel()
        {
            TryResolveVaultBuffer(
                in _tuningHandle,
                BufferID.ShinobuCausticsTuning,
                1,
                out NativeArray<CausticsTuningDTO> tuning);
            NativeArray<float4> emptySurfaceSwell = default;
            NativeArray<CausticsLightingProfileDTO> emptyProfiles = default;
            CausticsInputSnapshotDTO inputSnapshot = CaptureCausticsInputSnapshot(
                tuning,
                false,
                default,
                emptySurfaceSwell,
                emptyProfiles,
                ResolveCelestialLightReadability(),
                ResolveGlobalQualityWeight01(),
                _presentationTimeSeconds);
            double3 cameraAupLocal = ResolveCameraAupLocalOffset();

            GenerateMockCausticLightingJob job = default;
            job.InputSnapshot = inputSnapshot;
            job.CameraAupLocalOffset = cameraAupLocal;
            job.TimeSeconds = _presentationTimeSeconds;
            job.GlobalQualityWeight = inputSnapshot.WeatherStormWindPhaseQuality.w;
            job.FrameIndex = _presentationFrameIndex;
            if (!TryCalculatePendingCausticsParameters(job, out CausticsParametersDTO calculatedParameters))
                return;

            if (!TryAcquireVaultWriteBuffer(
                    in _parametersHandle,
                    BufferID.ShinobuCausticsParameters,
                    AbyssalCausticsConstants.ParameterCapacity,
                    out NativeArray<CausticsParametersDTO> parameters))
                return;

            try
            {
                PublishCalculatedCausticsParameters(in calculatedParameters, parameters);
            }
            finally
            {
                ReleaseVaultWriteBuffer(in _parametersHandle, BufferID.ShinobuCausticsParameters);
            }
        }

        private bool TrySetTuningInternal(float chromaticDispersion, float noiseScale, float flowSpeedMultiplier, float maxDepthMeters)
        {
            EnsureVaultState();
            float safeNoiseScale = math.max(0.005f, noiseScale);
            float safeFlowSpeedMultiplier = math.max(0f, flowSpeedMultiplier);
            float safeMaxDepthMeters = math.max(1f, maxDepthMeters);
            float safeChromaticDispersion = math.saturate(chromaticDispersion);
            if (!TryAcquireVaultWriteBuffer(
                    in _tuningHandle,
                    BufferID.ShinobuCausticsTuning,
                    1,
                    out NativeArray<CausticsTuningDTO> tuningArray))
                return false;

            try
            {
                CausticsTuningDTO tuning = tuningArray[0];
                tuning.ScaleFlowDepthIntensity.x = safeNoiseScale;
                tuning.ScaleFlowDepthIntensity.y = safeFlowSpeedMultiplier;
                tuning.ScaleFlowDepthIntensity.z = safeMaxDepthMeters;
                tuning.DispersionSdfTileProfile.x = safeChromaticDispersion;
                tuningArray[0] = tuning;
                _pendingGpuUpload = false;
            }
            finally
            {
                ReleaseVaultWriteBuffer(in _tuningHandle, BufferID.ShinobuCausticsTuning);
            }

            RunMockLightingKernel();
            return true;
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(quality, 1f, !math.isfinite(quality)));
        }

        private bool TryResolveWeatherSnapshot(out WeatherRuntimeSnapshot snapshot)
        {
            snapshot = default;
            IWeatherService weather = _weatherService;
            if (weather == null || !weather.IsInitialized)
                return false;

            snapshot = weather.GetRuntimeSnapshot();
            return true;
        }

        private double3 ResolveCameraAupLocalOffset()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null &&
                playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                snapshot.Aup.IsFinite())
            {
                AbsoluteUniversePosition aup = snapshot.Aup;
                return new double3(aup.LocalX, aup.LocalY, aup.LocalZ);
            }

            return default;
        }

        private void CheckFaultsAndDump(in CausticsParametersDTO parameters)
        {
            bool finite = math.all(math.isfinite(parameters.ProjectionVectorAndScale)) &&
                          math.all(math.isfinite(parameters.NoiseAnimationSpeed)) &&
                          math.all(math.isfinite(parameters.IntensityAndDepthFalloff)) &&
                          math.all(math.isfinite(parameters.QualityAndColor));
            if (finite)
            {
                _faultDumped = false;
                _lastFaultFlags = 0u;
                return;
            }

            _lastFaultFlags = AbyssalCausticsConstants.FaultNonFinite;
            if (!_faultDumped)
            {
                DumpBlackBox();
                _faultDumped = true;
            }
        }

        private bool PublishCalculatedCausticsParameters(
            in CausticsParametersDTO calculatedParameters,
            NativeArray<CausticsParametersDTO> parameters)
        {
            if (!parameters.IsCreated || parameters.Length < AbyssalCausticsConstants.ParameterCapacity)
                return false;

            parameters[AbyssalCausticsConstants.PendingParameterIndex] = calculatedParameters;
            parameters[AbyssalCausticsConstants.ActiveParameterIndex] = calculatedParameters;
            _pendingGpuUpload = true;
            return true;
        }

        private bool TryCalculatePendingCausticsParameters(
            GenerateMockCausticLightingJob job,
            out CausticsParametersDTO parameters)
        {
            parameters = default;
            // Local scratch keeps caustic math outside DataVault write locks; callers copy one DTO under lock.
            CausticsParametersDTO* scratch = stackalloc CausticsParametersDTO[AbyssalCausticsConstants.ParameterCapacity];
            job.Parameters = scratch;
            job.ParameterLength = AbyssalCausticsConstants.ParameterCapacity;
            job.OutputIndex = AbyssalCausticsConstants.PendingParameterIndex;
            job.Execute();
            parameters = scratch[AbyssalCausticsConstants.PendingParameterIndex];
            CheckFaultsAndDump(in parameters);
            return true;
        }

        private bool TryCalculatePendingCausticsParameters(
            CalculateCausticParametersJob job,
            out CausticsParametersDTO parameters)
        {
            parameters = default;
            // Local scratch keeps caustic math outside DataVault write locks; callers copy one DTO under lock.
            CausticsParametersDTO* scratch = stackalloc CausticsParametersDTO[AbyssalCausticsConstants.ParameterCapacity];
            job.Parameters = scratch;
            job.ParameterLength = AbyssalCausticsConstants.ParameterCapacity;
            job.OutputIndex = AbyssalCausticsConstants.PendingParameterIndex;
            job.Execute();
            parameters = scratch[AbyssalCausticsConstants.PendingParameterIndex];
            CheckFaultsAndDump(in parameters);
            return true;
        }

        private void RecordCausticsTelemetryOneLock(
            in CausticsParametersDTO parameters,
            in CausticsInputSnapshotDTO inputSnapshot)
        {
            CausticsTelemetryEntry entry = BuildCausticsTelemetryEntry(in parameters, in inputSnapshot);
            if (!TryAcquireVaultWriteBuffer(
                    in _telemetryHandle,
                    BufferID.ShinobuCausticsTelemetryRing,
                    AbyssalCausticsConstants.TelemetryCapacity,
                    out NativeArray<CausticsTelemetryEntry> telemetry))
            {
                _vaultStateReady = false;
                return;
            }

            int nextCursor = _telemetryWriteCursor;
            bool wroteTelemetry = false;
            try
            {
                if (!telemetry.IsCreated || telemetry.Length <= 0)
                {
                    _vaultStateReady = false;
                    return;
                }

                int cursor = WrapTelemetryCursor(_telemetryWriteCursor, telemetry.Length);
                telemetry[cursor] = entry;
                nextCursor = cursor + 1;
                if (nextCursor >= telemetry.Length)
                    nextCursor = 0;
                _telemetryWriteCursor = nextCursor;
                wroteTelemetry = true;
            }
            finally
            {
                ReleaseVaultWriteBuffer(in _telemetryHandle, BufferID.ShinobuCausticsTelemetryRing);
            }

            if (wroteTelemetry)
                WriteTelemetryCursorOneLock(nextCursor);
        }

        private void WriteTelemetryCursorOneLock(int nextCursor)
        {
            if (!TryAcquireVaultWriteBuffer(
                    in _telemetryCursorHandle,
                    BufferID.ShinobuCausticsTelemetryCursor,
                    1,
                    out NativeArray<int> telemetryCursor))
            {
                _vaultStateReady = false;
                return;
            }

            try
            {
                if (!telemetryCursor.IsCreated || telemetryCursor.Length <= 0)
                {
                    _vaultStateReady = false;
                    return;
                }

                telemetryCursor[0] = WrapTelemetryCursor(nextCursor, AbyssalCausticsConstants.TelemetryCapacity);
                _telemetryCursorSeeded = true;
            }
            finally
            {
                ReleaseVaultWriteBuffer(in _telemetryCursorHandle, BufferID.ShinobuCausticsTelemetryCursor);
            }
        }

        private CausticsTelemetryEntry BuildCausticsTelemetryEntry(
            in CausticsParametersDTO parameters,
            in CausticsInputSnapshotDTO inputSnapshot)
        {
            float quality = math.saturate(math.select(
                parameters.QualityAndColor.x,
                ResolveGlobalQualityWeight01(),
                !math.isfinite(parameters.QualityAndColor.x)));
            float activeOctaves = GenerateMockCausticLightingJob.ResolveActiveOctaves(quality);
            float maxDepth = ResolveTelemetryMaxDepth(in inputSnapshot, quality);
            uint flags = ResolveTelemetryFlags(in parameters, in inputSnapshot);

            CausticsTelemetryEntry entry;
            entry.FrameIndex = _presentationFrameIndex;
            entry.StateHash = CalculateCausticParametersJob.ResolveTelemetryStateHash(in parameters, flags);
            entry.Flags = flags;
            entry.ActiveNoiseOctavesX1000 = (uint)math.round(activeOctaves * 1000f);
            entry.SunIntensity = parameters.IntensityAndDepthFalloff.x;
            entry.ActiveNoiseOctaves = activeOctaves;
            entry.MaxDepthMeters = maxDepth;
            entry.EstimatedGpuMicros = CalculateCausticParametersJob.EstimateTelemetryGpuMicros(quality, activeOctaves, maxDepth);
            entry.ProjectionVectorAndScale = parameters.ProjectionVectorAndScale;
            entry.NoiseAnimationSpeed = parameters.NoiseAnimationSpeed;
            return entry;
        }

        private static uint ResolveTelemetryFlags(
            in CausticsParametersDTO parameters,
            in CausticsInputSnapshotDTO inputSnapshot)
        {
            uint flags = inputSnapshot.Flags & ~AbyssalCausticsConstants.FlagInputSnapshot;
            bool finite = math.all(math.isfinite(parameters.ProjectionVectorAndScale)) &&
                          math.all(math.isfinite(parameters.NoiseAnimationSpeed)) &&
                          math.all(math.isfinite(parameters.IntensityAndDepthFalloff)) &&
                          math.all(math.isfinite(parameters.QualityAndColor));
            return flags | (finite ? 0u : AbyssalCausticsConstants.FaultNonFinite);
        }

        private static float ResolveTelemetryMaxDepth(
            in CausticsInputSnapshotDTO inputSnapshot,
            float quality)
        {
            float profileDepth = math.max(0f, inputSnapshot.ProfileIntensityScaleDepthFlow.z);
            float requestedDepth = profileDepth > 0.001f
                ? profileDepth
                : inputSnapshot.Tuning.ScaleFlowDepthIntensity.z;
            return GenerateMockCausticLightingJob.ResolveMaxDepth(requestedDepth, quality);
        }

        private static int WrapTelemetryCursor(int cursor, int capacity)
        {
            if (capacity <= 0)
                return 0;

            int wrapped = cursor % capacity;
            return wrapped < 0 ? wrapped + capacity : wrapped;
        }

        private void MarkBurstKernelUnavailable()
        {
            _pendingGpuUpload = false;
            _lastFaultFlags = AbyssalCausticsConstants.FaultBurstKernelUnavailable;
            if (!_faultDumped)
            {
                DumpBlackBox();
                _faultDumped = true;
            }
        }

        private static CausticsInputSnapshotDTO CaptureCausticsInputSnapshot(
            NativeArray<CausticsTuningDTO> tuningArray,
            bool hasWeatherSnapshot,
            WeatherRuntimeSnapshot weatherSnapshot,
            NativeArray<float4> surfaceSwellArray,
            NativeArray<CausticsLightingProfileDTO> profilesArray,
            CelestialLightReadabilitySnapshot celestialLight,
            float fallbackQuality,
            float timeSeconds)
        {
            CausticsTuningDTO tuning = GenerateMockCausticLightingJob.DefaultTuning();
            if (tuningArray.IsCreated && tuningArray.Length > 0)
                tuning = SanitizeTuning(tuningArray[0]);

            float quality = Sanitize01(fallbackQuality, 1f);
            float storm = 0f;
            float windSpeed = 0f;
            float waveHeight = 0.25f;
            float waveFrequency = 0.12f;
            float wavePhase = timeSeconds * 0.17f;
            uint weatherStateMask = 0u;
            uint flags = AbyssalCausticsConstants.FlagInputSnapshot;

            if (hasWeatherSnapshot)
            {
                storm = Sanitize01(weatherSnapshot.WeatherIntensity, 0f);
                windSpeed = math.length(math.select(float3.zero, weatherSnapshot.GlobalWindVector, math.isfinite(weatherSnapshot.GlobalWindVector)));
                weatherStateMask = (uint)weatherSnapshot.StateMask;
                float waveTime = math.select(timeSeconds, weatherSnapshot.CurrentMeta.TimeAccumulator, math.isfinite(weatherSnapshot.CurrentMeta.TimeAccumulator));
                ApplyGerstnerCausticWave(weatherSnapshot.Wave0, waveTime, ref waveHeight, ref waveFrequency, ref wavePhase);
                ApplyGerstnerCausticWave(weatherSnapshot.Wave1, waveTime, ref waveHeight, ref waveFrequency, ref wavePhase);
                ApplyGerstnerCausticWave(weatherSnapshot.Wave2, waveTime, ref waveHeight, ref waveFrequency, ref wavePhase);
                flags |= AbyssalCausticsConstants.FlagWeatherSnapshotBound;
                flags |= AbyssalCausticsConstants.FlagWaveInputBound;
            }

            if (surfaceSwellArray.IsCreated && surfaceSwellArray.Length > 0)
            {
                float4 swell = math.select(float4.zero, surfaceSwellArray[0], math.isfinite(surfaceSwellArray[0]));
                waveHeight = math.max(waveHeight, math.abs(swell.x));
                waveFrequency = math.max(waveFrequency, math.abs(swell.y));
                wavePhase = math.select(wavePhase, swell.z, math.isfinite(swell.z));
                flags |= AbyssalCausticsConstants.FlagWaveInputBound;
            }

            float profileIntensity = 1f;
            float profileScale = 1f;
            float profileDepth = 0f;
            float profileFlow = 1f;
            float profileChromatic = math.saturate(tuning.DispersionSdfTileProfile.x);
            float profileSdf = math.saturate(tuning.DispersionSdfTileProfile.y);
            uint resolvedWeatherKey = CalculateCausticParametersJob.ResolveProfileWeatherKey(weatherStateMask, storm);
            if (profilesArray.IsCreated)
            {
                for (int i = 0; i < profilesArray.Length; i++)
                {
                    CausticsLightingProfileDTO profile = profilesArray[i];
                    if (!CalculateCausticParametersJob.ProfileMatches(profile.StateHash, resolvedWeatherKey, weatherStateMask))
                        continue;

                    profileIntensity = SanitizeNonNegative(profile.Intensity, profileIntensity);
                    profileScale = math.max(0.01f, SanitizeNonNegative(profile.NoiseScale, profileScale));
                    profileDepth = SanitizeNonNegative(profile.MaxDepthMeters, profileDepth);
                    profileFlow = SanitizeNonNegative(profile.FlowSpeed, profileFlow);
                    if (math.isfinite(profile.ChromaticDispersion) && profile.ChromaticDispersion >= 0f)
                        profileChromatic = math.saturate(profile.ChromaticDispersion);
                    if (math.isfinite(profile.SdfShadowStrength) && profile.SdfShadowStrength >= 0f)
                        profileSdf = math.saturate(profile.SdfShadowStrength);
                    flags |= AbyssalCausticsConstants.FlagProfileBound;
                    break;
                }
            }

            if ((celestialLight.Flags & (uint)CelestialLightReadabilityFlags.Valid) != 0u)
            {
                float causticMultiplier = CelestialLightReadabilityUtility.ResolveCausticsIntensityMultiplier(in celestialLight);
                profileIntensity *= causticMultiplier;
                profileDepth = CelestialLightReadabilityUtility.ResolveCausticsMaxDepthMeters(in celestialLight, profileDepth > 0.001f ? profileDepth : tuning.ScaleFlowDepthIntensity.z);
                flags |= AbyssalCausticsConstants.FlagCelestialLightBound;
            }

            CausticsInputSnapshotDTO snapshot;
            snapshot.Tuning = tuning;
            snapshot.WeatherStormWindPhaseQuality = new float4(storm, windSpeed, wavePhase, quality);
            snapshot.WaveHeightFrequencyReserved = new float4(
                math.max(0.01f, waveHeight),
                math.max(0.02f, waveFrequency),
                math.saturate(celestialLight.CausticWeight01),
                math.saturate(celestialLight.DirectSun01));
            snapshot.ProfileIntensityScaleDepthFlow = new float4(profileIntensity, profileScale, profileDepth, profileFlow);
            snapshot.ProfileChromaticSdf = new float2(profileChromatic, profileSdf);
            snapshot.Flags = flags;
            snapshot.Reserved = 0u;
            return snapshot;
        }

        private static void ApplyGerstnerCausticWave(
            GerstnerWaveComponent wave,
            float timeSeconds,
            ref float waveHeight,
            ref float waveFrequency,
            ref float wavePhase)
        {
            float amplitude = SanitizeNonNegative(wave.Amplitude, 0f);
            float wavelength = math.max(CausticsMinimumWavelength, SanitizeNonNegative(wave.Wavelength, 24f));
            float speed = math.select(wave.SpeedMultiplier, 0f, !math.isfinite(wave.SpeedMultiplier));
            float phaseOffset = math.select(wave.PhaseOffset, 0f, !math.isfinite(wave.PhaseOffset));
            waveHeight = math.max(waveHeight, amplitude);
            waveFrequency = math.max(waveFrequency, math.rcp(math.max(wavelength, 0.0001f)));
            wavePhase += (speed * timeSeconds + phaseOffset) * math.saturate(amplitude);
        }

        private static CausticsTuningDTO SanitizeTuning(CausticsTuningDTO tuning)
        {
            CausticsTuningDTO fallback = GenerateMockCausticLightingJob.DefaultTuning();
            tuning.ScaleFlowDepthIntensity = math.select(fallback.ScaleFlowDepthIntensity, tuning.ScaleFlowDepthIntensity, math.isfinite(tuning.ScaleFlowDepthIntensity));
            tuning.DispersionSdfTileProfile = math.select(fallback.DispersionSdfTileProfile, tuning.DispersionSdfTileProfile, math.isfinite(tuning.DispersionSdfTileProfile));
            tuning.ColorRgbWeatherPenalty = math.select(fallback.ColorRgbWeatherPenalty, tuning.ColorRgbWeatherPenalty, math.isfinite(tuning.ColorRgbWeatherPenalty));
            tuning.ScaleFlowDepthIntensity.x = math.max(0.005f, tuning.ScaleFlowDepthIntensity.x);
            tuning.ScaleFlowDepthIntensity.y = math.max(0f, tuning.ScaleFlowDepthIntensity.y);
            tuning.ScaleFlowDepthIntensity.z = math.max(1f, tuning.ScaleFlowDepthIntensity.z);
            tuning.ScaleFlowDepthIntensity.w = math.max(0f, tuning.ScaleFlowDepthIntensity.w);
            tuning.DispersionSdfTileProfile.x = math.saturate(tuning.DispersionSdfTileProfile.x);
            tuning.DispersionSdfTileProfile.y = math.saturate(tuning.DispersionSdfTileProfile.y);
            tuning.DispersionSdfTileProfile.z = math.max(8f, tuning.DispersionSdfTileProfile.z);
            tuning.ColorRgbWeatherPenalty.w = math.saturate(tuning.ColorRgbWeatherPenalty.w);
            tuning.Reserved = default;
            return tuning;
        }

        private static float Sanitize01(float value, float fallback)
        {
            return math.saturate(math.select(value, fallback, !math.isfinite(value)));
        }

        private static float SanitizeNonNegative(float value, float fallback)
        {
            return math.max(0f, math.select(value, fallback, !math.isfinite(value)));
        }

#if UNITY_EDITOR
        private static int LoadFileBytesIntoScratch(string fullPath, NativeArray<byte> csvScratch)
        {
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath) || !csvScratch.IsCreated)
                return 0;

            byte* dst = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(csvScratch);
            int capacity = csvScratch.Length;
            int total = 0;
            Span<byte> block = stackalloc byte[512];
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
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }

            return total;
        }

        private static int ParseLightingProfiles(ReadOnlySpan<byte> csvBytes, NativeArray<CausticsLightingProfileDTO> profiles)
        {
            int rowStart = 0;
            int write = 0;
            for (int i = 0; i <= csvBytes.Length && write < profiles.Length; i++)
            {
                bool end = i == csvBytes.Length;
                if (!end && csvBytes[i] != (byte)'\n')
                    continue;

                int rowEnd = i;
                if (rowEnd > rowStart && csvBytes[rowEnd - 1] == (byte)'\r')
                    rowEnd--;
                if (TryParseProfileRow(csvBytes.Slice(rowStart, rowEnd - rowStart), out CausticsLightingProfileDTO profile))
                {
                    profiles[write] = profile;
                    write++;
                }

                rowStart = i + 1;
            }

            if (write <= 0)
                return 0;

            for (int i = write; i < profiles.Length; i++)
                profiles[i] = default;
            return write;
        }

        private static bool TryParseProfileRow(ReadOnlySpan<byte> row, out CausticsLightingProfileDTO profile)
        {
            profile = default;
            if (row.Length <= 0 || row[0] == (byte)'#')
                return false;

            int tokenStart = 0;
            int tokenIndex = 0;
            uint hash = 0u;
            float noiseScale = 0f;
            float intensity = 0f;
            float maxDepth = 0f;
            float flow = 1f;
            float chroma = -1f;
            float sdf = -1f;
            for (int i = 0; i <= row.Length; i++)
            {
                bool end = i == row.Length;
                if (!end && row[i] != (byte)',')
                    continue;

                ReadOnlySpan<byte> token = TrimAscii(row.Slice(tokenStart, i - tokenStart));
                if (tokenIndex == 0)
                {
                    hash = ResolveProfileStateKey(token);
                    if (IsHeaderToken(token))
                        return false;
                }
                else
                {
                    if (!TryParseFloat(token, out float value))
                        return false;
                    switch (tokenIndex)
                    {
                        case 1:
                            noiseScale = value;
                            break;
                        case 2:
                            intensity = value;
                            break;
                        case 3:
                            maxDepth = value;
                            break;
                        case 4:
                            flow = value;
                            break;
                        case 5:
                            chroma = value;
                            break;
                        case 6:
                            sdf = value;
                            break;
                    }
                }

                tokenIndex++;
                tokenStart = i + 1;
            }

            if (hash == 0u || tokenIndex < 4)
                return false;

            profile.StateHash = hash;
            profile.NoiseScale = math.max(0.005f, noiseScale);
            profile.Intensity = math.max(0f, intensity);
            profile.MaxDepthMeters = math.max(1f, maxDepth);
            profile.FlowSpeed = math.max(0f, flow);
            profile.ChromaticDispersion = chroma >= 0f ? math.saturate(chroma) : -1f;
            profile.SdfShadowStrength = sdf >= 0f ? math.saturate(sdf) : -1f;
            profile.Reserved = 0f;
            return true;
        }

        private static uint ResolveProfileStateKey(ReadOnlySpan<byte> token)
        {
            ReadOnlySpan<byte> trimmed = TrimAscii(token);
            uint fallbackHash = Fnv1aLower(trimmed);
            if (EqualsAsciiIgnoreCase(trimmed, "calm"))
                return (uint)WeatherState.Calm;
            if (EqualsAsciiIgnoreCase(trimmed, "storm") ||
                EqualsAsciiIgnoreCase(trimmed, "hurricane") ||
                EqualsAsciiIgnoreCase(trimmed, "squall") ||
                EqualsAsciiIgnoreCase(trimmed, "tempest"))
            {
                return (uint)WeatherState.Storm;
            }

            if (EqualsAsciiIgnoreCase(trimmed, "thermocline"))
                return (uint)WeatherState.ThermoclineActive;
            if (EqualsAsciiIgnoreCase(trimmed, "halocline"))
                return (uint)WeatherState.HaloclineActive;
            if (EqualsAsciiIgnoreCase(trimmed, "biolume") ||
                EqualsAsciiIgnoreCase(trimmed, "bioluminescence"))
            {
                return (uint)WeatherState.BiolumeSurge;
            }

            return fallbackHash;
        }

        private static bool EqualsAsciiIgnoreCase(ReadOnlySpan<byte> token, string literal)
        {
            if (token.Length != literal.Length)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                if (ToLower(token[i]) != (byte)literal[i])
                    return false;
            }

            return true;
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> token)
        {
            int start = 0;
            int end = token.Length;
            while (start < end && token[start] <= 32)
                start++;
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
                    byte value = token[i];
                    if (value >= (byte)'A' && value <= (byte)'Z')
                        value = (byte)(value + 32);
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

            if (token.Length == 5)
            {
                return ToLower(token[0]) == (byte)'s' &&
                       ToLower(token[1]) == (byte)'t' &&
                       ToLower(token[2]) == (byte)'a' &&
                       ToLower(token[3]) == (byte)'t' &&
                       ToLower(token[4]) == (byte)'e';
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
            string root = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(root))
                return projectRelativePath;

            return Path.Combine(root, projectRelativePath);
        }
#endif

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = false;
        }

        private void TryRegisterOriginShift()
        {
            if (_registeredOriginShift || !Application.isPlaying)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShift = true;
        }

        private void TryUnregisterOriginShift()
        {
            if (!_registeredOriginShift)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _registeredOriginShift = false;
        }

        private void TryRegisterHotSwap()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwap()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void CacheRegistryServicesCold(bool forceRefresh)
        {
            if (forceRefresh || _dataVault == null)
                BindDataVaultForLifecycle(GlobalRegistry.DataVault);
            if (forceRefresh || _playerRuntimeContext == null)
                _playerRuntimeContext = GlobalRegistry.Player;
            if (forceRefresh || _weatherService == null)
                _weatherService = GlobalRegistry.Weather;
            if (forceRefresh || _celestialLightReadModel == null)
                CacheCelestialLightReadModel(GlobalRegistry.CelestialLightReadabilityReadModel);
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

        private CelestialLightReadabilitySnapshot ResolveCelestialLightReadability()
        {
            ICelestialLightReadabilityReadModel readModel = _celestialLightReadModel;
            if (!IsCelestialLightReadModelUsable(readModel))
            {
                CacheCelestialLightReadModel(GlobalRegistry.CelestialLightReadabilityReadModel);
                readModel = _celestialLightReadModel;
                if (!IsCelestialLightReadModelUsable(readModel))
                    return default;
            }

            return readModel.LightReadabilitySnapshot;
        }

        private static bool IsCelestialLightReadModelUsable(ICelestialLightReadabilityReadModel readModel)
        {
            if (readModel == null)
                return false;

            if (readModel is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private bool TryResolveVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   requiredLength > 0 &&
                   IsOwnedVaultHandle(in handle, expectedBufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool EnsureOwnedVaultHandleCold<T>(
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (TryResolveVaultBuffer(in handle, bufferId, requiredLength, out NativeArray<T> _))
                return true;

            IDataVault vault = _dataVault;
            if (vault == null || requiredLength <= 0)
            {
                handle = default;
                return false;
            }

            if (IsOwnedVaultHandle(in handle, bufferId))
                ReleaseVaultHandle(vault, ref handle, bufferId);
            else
                handle = default;

            if (vault.IsAllocationLocked)
                return false;

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, OwnerSystemId, options);
            return TryResolveVaultBuffer(in handle, bufferId, requiredLength, out NativeArray<T> _);
        }

        private bool TryAcquireVaultWriteBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsOwnedVaultHandle(in handle, expectedBufferId) ||
                !vault.TryAcquireWriteLock(in handle, OwnerSystemId, out buffer))
            {
                return false;
            }

            bool releaseOnExit = true;
            try
            {
                if (!buffer.IsCreated || buffer.Length < requiredLength)
                {
                    buffer = default;
                    return false;
                }

                releaseOnExit = false;
                return true;
            }
            finally
            {
                if (releaseOnExit)
                    vault.ReleaseWriteLock(in handle, OwnerSystemId);
            }
        }

        private void ReleaseVaultWriteBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId) where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault != null && IsOwnedVaultHandle(in handle, expectedBufferId))
                vault.ReleaseWriteLock(in handle, OwnerSystemId);
        }

        private bool TryReadOnlyVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   requiredLength > 0 &&
                   IsOwnedVaultHandle(in handle, expectedBufferId) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool TryResolveExternalVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   requiredLength > 0 &&
                   IsVaultHandleForBuffer(in handle, expectedBufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private void RefreshExternalInputHandles()
        {
            RefreshExternalInputHandle(BufferID.ShinobuOceanSurfaceSwell, 1, ref _surfaceSwellInputHandle);
        }

        private bool RefreshExternalInputHandle<T>(
            BufferID bufferId,
            int requiredLength,
            ref VaultGenerationHandle<T> handle) where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault == null || requiredLength <= 0)
            {
                handle = default;
                return false;
            }

            if (IsVaultHandleForBuffer(in handle, bufferId) &&
                vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            handle = default;
            if (!vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existingHandle) ||
                !IsVaultHandleForBuffer(in existingHandle, bufferId))
            {
                return false;
            }

            handle = existingHandle;
            if (vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly refreshedBuffer) &&
                refreshedBuffer.IsCreated &&
                refreshedBuffer.Length >= requiredLength)
            {
                return true;
            }

            handle = default;
            return false;
        }

        private static bool IsVaultHandleForBuffer<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   handle.Generation != 0u;
        }

        private static bool IsOwnedVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   handle.SystemID == (uint)OwnerSystemId &&
                   handle.Generation != 0u;
        }

        private void ShutdownServiceState()
        {
            TryUnregisterLateFrame();
            TryUnregisterOriginShift();
            TryUnregisterHotSwap();
            if (ReferenceEquals(GlobalRegistry.Caustics, this))
                GlobalRegistry.UnregisterCausticsService(this);
            _ownsRegistrySlot = false;
            if (ReferenceEquals(s_runtimeInstance, this))
                s_runtimeInstance = null;
            if (ReferenceEquals(s_publishedRuntime, this))
                s_publishedRuntime = null;
            ClearPublishedConstantBufferIfOwnedByThis();

            _constantBufferA?.Release();
            _constantBufferB?.Release();
            _constantBufferA = null;
            _constantBufferB = null;
            _activeConstantBuffer = null;
            _activeConstantBufferFrameIndex = 0u;

            ReleaseAllVaultHandles(_dataVault);

            ResetVaultEpochState();
            _isInitialized = false;
            _dataVault = null;
            _playerRuntimeContext = null;
            _weatherService = null;
            _celestialLightReadModel = null;
        }

        private void ReleaseAllVaultHandles(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref _parametersHandle, BufferID.ShinobuCausticsParameters);
            ReleaseVaultHandle(vault, ref _tuningHandle, BufferID.ShinobuCausticsTuning);
            ReleaseVaultHandle(vault, ref _telemetryHandle, BufferID.ShinobuCausticsTelemetryRing);
            ReleaseVaultHandle(vault, ref _telemetryCursorHandle, BufferID.ShinobuCausticsTelemetryCursor);
            ReleaseVaultHandle(vault, ref _profilesHandle, BufferID.ShinobuCausticsProfiles);
            ReleaseVaultHandle(vault, ref _csvScratchHandle, BufferID.ShinobuCausticsCsvScratch);
        }

        private void BindDataVaultForLifecycle(IDataVault nextVault)
        {
            if (ReferenceEquals(_dataVault, nextVault))
                return;

            IDataVault previousVault = _dataVault;
            if (previousVault != null)
                ReleaseAllVaultHandles(previousVault);

            _dataVault = nextVault;
            ResetVaultEpochState();
        }

        private void ResetVaultEpochState()
        {
            _pendingGpuUpload = false;
            _tuningSeeded = false;
            _profilesSeeded = false;
            _telemetrySeeded = false;
            _telemetryCursorSeeded = false;
            _telemetryWriteCursor = 0;
            _vaultStateReady = false;
            _faultDumped = false;
            _lastFaultFlags = 0u;
            ClearExternalInputHandles();
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

        private void ClearExternalInputHandles()
        {
            _surfaceSwellInputHandle = default;
        }

        private void EnsureBlackBoxDumpPathCold()
        {
            if (!string.IsNullOrEmpty(_blackBoxDumpPath))
                return;

            string root = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(root))
                return;

            _blackBoxDumpPath = Path.Combine(root, DumpPath);
            _blackBoxDumpDirectory = Path.GetDirectoryName(_blackBoxDumpPath);
            if (string.IsNullOrEmpty(_blackBoxDumpDirectory))
            {
                _blackBoxDumpPath = null;
                return;
            }
        }

        private void DumpBlackBox()
        {
            if (!TryResolveVaultBuffer(
                    in _telemetryHandle,
                    BufferID.ShinobuCausticsTelemetryRing,
                    AbyssalCausticsConstants.TelemetryCapacity,
                    out NativeArray<CausticsTelemetryEntry> telemetry))
                return;

            TryResolveVaultBuffer(
                in _telemetryCursorHandle,
                BufferID.ShinobuCausticsTelemetryCursor,
                1,
                out NativeArray<int> telemetryCursor);
            string path = _blackBoxDumpPath;
            if (string.IsNullOrEmpty(path))
                return;

            int entryCount = math.min(telemetry.Length, AbyssalCausticsConstants.TelemetryCapacity);
            int telemetryWriteCursor = telemetryCursor.IsCreated && telemetryCursor.Length > 0 ? telemetryCursor[0] : 0;
            int wrappedCursor = 0;
            if (entryCount > 0)
            {
                wrappedCursor = telemetryWriteCursor % entryCount;
                if (wrappedCursor < 0)
                    wrappedCursor += entryCount;
            }

            const int HeaderBytes = 20;
            const int RowBytes = 64;
            int totalBytes = HeaderBytes + entryCount * RowBytes;
            NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                totalBytes,
                nameof(AbyssalDeferredCausticsRuntime),
                BlackBoxDumpPayloadLabel,
                NativeArrayOptions.UninitializedMemory);
            try
            {
                payload = H8Memory.Allocate<byte>(
                    totalBytes,
                    OwnerSystemId,
                    Allocator.Temp,
                    NativeArrayOptions.UninitializedMemory);
                if (!payload.IsCreated)
                    return;

                WriteUInt32LittleEndian(payload, 0, 0x32334353u);
                WriteInt32LittleEndian(payload, 4, entryCount);
                WriteInt32LittleEndian(payload, 8, telemetryWriteCursor);
                WriteUInt32LittleEndian(payload, 12, _lastFaultFlags);
                WriteInt32LittleEndian(payload, 16, UnsafeUtility.SizeOf<CausticsTelemetryEntry>());
                for (int i = 0; i < entryCount; i++)
                {
                    int index = wrappedCursor + i;
                    if (index >= entryCount)
                        index -= entryCount;

                    CausticsTelemetryEntry entry = telemetry[index];
                    int offset = HeaderBytes + i * RowBytes;
                    WriteUInt32LittleEndian(payload, offset, entry.FrameIndex);
                    WriteUInt32LittleEndian(payload, offset + 4, entry.StateHash);
                    WriteUInt32LittleEndian(payload, offset + 8, entry.Flags);
                    WriteUInt32LittleEndian(payload, offset + 12, entry.ActiveNoiseOctavesX1000);
                    WriteFloat32LittleEndian(payload, offset + 16, entry.SunIntensity);
                    WriteFloat32LittleEndian(payload, offset + 20, entry.ActiveNoiseOctaves);
                    WriteFloat32LittleEndian(payload, offset + 24, entry.MaxDepthMeters);
                    WriteFloat32LittleEndian(payload, offset + 28, entry.EstimatedGpuMicros);
                    WriteFloat4LittleEndian(payload, offset + 32, entry.ProjectionVectorAndScale);
                    WriteFloat4LittleEndian(payload, offset + 48, entry.NoiseAnimationSpeed);
                }

                if (!NativeFaultDumpWriter.TryWriteAll(path, payload, totalBytes))
                    _lastFaultFlags |= AbyssalCausticsConstants.FaultDumpIo;
            }
            catch (IOException)
            {
                _lastFaultFlags |= AbyssalCausticsConstants.FaultDumpIo;
            }
            catch (UnauthorizedAccessException)
            {
                _lastFaultFlags |= AbyssalCausticsConstants.FaultDumpIo;
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(AbyssalDeferredCausticsRuntime),
                    BlackBoxDumpPayloadLabel);
            }
        }

        private static void WriteFloat4LittleEndian(NativeArray<byte> payload, int offset, float4 value)
        {
            WriteFloat32LittleEndian(payload, offset, value.x);
            WriteFloat32LittleEndian(payload, offset + 4, value.y);
            WriteFloat32LittleEndian(payload, offset + 8, value.z);
            WriteFloat32LittleEndian(payload, offset + 12, value.w);
        }

        private static void WriteFloat32LittleEndian(NativeArray<byte> payload, int offset, float value)
        {
            WriteUInt32LittleEndian(payload, offset, math.asuint(value));
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> payload, int offset, int value)
        {
            WriteUInt32LittleEndian(payload, offset, unchecked((uint)value));
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> payload, int offset, uint value)
        {
            payload[offset] = (byte)value;
            payload[offset + 1] = (byte)(value >> 8);
            payload[offset + 2] = (byte)(value >> 16);
            payload[offset + 3] = (byte)(value >> 24);
        }
    }

    public static class AbyssalCausticsShaderIds
    {
        public static readonly int ConstantBufferId = Shader.PropertyToID("HectonAbyssalCaustics");
        public static readonly int SourceTextureId = Shader.PropertyToID("_HectonDeferredCausticsSource");
        public static readonly int DepthTextureId = Shader.PropertyToID("_HectonDeferredCausticsDepth");
        public static readonly int BakedAtlasTextureId = Shader.PropertyToID("_HectonBakedCausticAtlas");
        public static readonly int BakedAtlasParamsId = Shader.PropertyToID("_HectonBakedCausticAtlasParams");
        public static readonly int BakedAtlasTexelParamsId = Shader.PropertyToID("_HectonBakedCausticAtlasTexelParams");
        public static readonly int BakedWaterlineMaskId = Shader.PropertyToID("_HectonBakedCausticWaterlineMask");
        public static readonly int BakedWaterlineParamsId = Shader.PropertyToID("_HectonBakedCausticWaterlineParams");
    }
}
