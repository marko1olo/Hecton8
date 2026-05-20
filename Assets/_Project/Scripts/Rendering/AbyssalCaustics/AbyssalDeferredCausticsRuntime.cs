using System;
using System.IO;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Rendering
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9209)]
    public sealed unsafe class AbyssalDeferredCausticsRuntime : MonoBehaviour, ICausticsService, IUpdatable, ILateFrameTickable, IOriginShiftListener, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private const SystemID OwnerSystemId = SystemID.GraphicsScalability;
        private const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_232.bin";

        private static AbyssalDeferredCausticsRuntime s_runtimeInstance;

        private IDataVault _dataVault;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private VaultGenerationHandle<CausticsParametersDTO> _parametersHandle;
        private VaultGenerationHandle<CausticsTuningDTO> _tuningHandle;
        private VaultGenerationHandle<CausticsTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<CausticsLightingProfileDTO> _profilesHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<WeatherStateDTO> _weatherInputHandle;
        private VaultGenerationHandle<WaveParametersDTO> _waveInputHandle;
        private VaultGenerationHandle<float4> _surfaceSwellInputHandle;
        private GraphicsBuffer _constantBufferA;
        private GraphicsBuffer _constantBufferB;
        private GraphicsBuffer _activeConstantBuffer;
        private Vector4 _legacyCausticsAup;
        private float _presentationTimeSeconds;
        private int _activeConstantBufferIndex;
        private int _tickCount;
        private uint _presentationFrameIndex;
        private uint _lastFaultFlags;
        private bool _isInitialized;
        private bool _ownsRegistrySlot;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _registeredOriginShift;
        private bool _registeredHotSwap;
        private bool _pendingGpuUpload;
        private bool _tuningSeeded;
        private bool _profilesSeeded;
        private bool _telemetryCursorSeeded;
        private bool _vaultStateReady;
        private bool _faultDumped;

        public bool IsComputeActive => _activeConstantBuffer != null && _activeConstantBuffer.IsValid();
        public RenderTexture CausticsMap => null;
        public Vector4 CausticsAup => _legacyCausticsAup;
        public ServiceHeartbeatState HeartbeatState => _isInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;
        public bool IsServiceReady => _isInitialized;
        public int TickCount => _tickCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_runtimeInstance = null;
        }

        public static AbyssalDeferredCausticsRuntime EnsureRuntimeInstance()
        {
            if (GlobalRegistry.Caustics is AbyssalDeferredCausticsRuntime runtime)
                return runtime;

            if (s_runtimeInstance != null)
                return s_runtimeInstance;

            GameObject runtimeRoot = new GameObject("[AbyssalDeferredCausticsRuntime]"); // COLD ALLOC: GameObject[1] - bootstrap-owned screen-space caustics owner - owner: AbyssalDeferredCausticsRuntime
            return runtimeRoot.AddComponent<AbyssalDeferredCausticsRuntime>();
        }

        public void InitializeService()
        {
            if (!EnsureSingletonOwnership())
                return;

            CacheRegistryServicesCold(forceRefresh: true);
            TryRegisterHotSwap();
            EnsureVaultState();
            EnsureCsvScratch();
            TryRegisterUpdate();
            TryRegisterLateFrame();
            TryRegisterOriginShift();
            _isInitialized = CausticsParametersLayoutValidator.Validate();
            if (!_isInitialized)
            {
                _lastFaultFlags = AbyssalCausticsConstants.FaultLayout;
                DumpBlackBox();
                return;
            }

            RunMockLightingKernel();
        }

        public void Tick(float deltaTime)
        {
            _tickCount++;
            if (!_isInitialized)
            {
                InitializeService();
                if (!_isInitialized || !_ownsRegistrySlot)
                    return;
            }

            float safeDeltaTime = math.select(deltaTime, 0f, !math.isfinite(deltaTime) || deltaTime < 0f);
            _presentationTimeSeconds += math.min(safeDeltaTime, 0.25f);
            _presentationFrameIndex++;

            if (!_vaultStateReady)
                return;
            if (!TryResolveVaultBuffer(in _parametersHandle, AbyssalCausticsConstants.ParameterCapacity, out NativeArray<CausticsParametersDTO> parameters))
            {
                _vaultStateReady = false;
                return;
            }

            double3 cameraAupLocal = ResolveCameraAupLocalOffset();
            float quality = ResolveGlobalQualityWeight01();
            NativeArray<WeatherStateDTO> weather = default;
            NativeArray<WaveParametersDTO> waveParameters = default;
            NativeArray<float4> surfaceSwell = default;
            bool hasTuning = TryResolveVaultBuffer(in _tuningHandle, 1, out NativeArray<CausticsTuningDTO> tuning);
            bool hasTelemetry = TryResolveVaultBuffer(in _telemetryHandle, AbyssalCausticsConstants.TelemetryCapacity, out NativeArray<CausticsTelemetryEntry> telemetry);
            bool hasTelemetryCursor = TryResolveVaultBuffer(in _telemetryCursorHandle, 1, out NativeArray<int> telemetryCursor);
            bool hasProfiles = TryResolveVaultBuffer(in _profilesHandle, AbyssalCausticsConstants.ProfileCapacity, out NativeArray<CausticsLightingProfileDTO> profiles);
            if (!hasTuning || !hasTelemetry || !hasTelemetryCursor || !hasProfiles)
                _vaultStateReady = false;
            TryResolveVaultBuffer(in _weatherInputHandle, 1, out weather);
            TryResolveVaultBuffer(in _waveInputHandle, 1, out waveParameters);
            TryResolveVaultBuffer(in _surfaceSwellInputHandle, 1, out surfaceSwell);

            CausticsInputSnapshotDTO inputSnapshot = CaptureCausticsInputSnapshot(
                tuning,
                weather,
                waveParameters,
                surfaceSwell,
                profiles,
                quality,
                _presentationTimeSeconds);

            CalculateCausticParametersJob job = default;
            job.Parameters = parameters;
            job.Telemetry = telemetry;
            job.TelemetryCursor = telemetryCursor;
            job.InputSnapshot = inputSnapshot;
            job.CameraAupLocalOffset = cameraAupLocal;
            job.TimeSeconds = _presentationTimeSeconds;
            job.GlobalQualityWeight = inputSnapshot.WeatherStormWindPhaseQuality.w;
            job.FrameIndex = _presentationFrameIndex;
            job.OutputIndex = AbyssalCausticsConstants.PendingParameterIndex;
            job.Run();
            PublishPendingCausticsParameters();
        }

        public void LateFrameTick()
        {
            if (!_isInitialized)
            {
                InitializeService();
                if (!_isInitialized || !_ownsRegistrySlot)
                    return;
            }

            if (!_pendingGpuUpload)
                return;

            if (UploadParametersToGpu())
                _pendingGpuUpload = false;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _legacyCausticsAup.x -= shiftData.ShiftOffset.x;
            _legacyCausticsAup.y -= shiftData.ShiftOffset.z;
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
                    ReleaseAllVaultHandles(previousService as IDataVault ?? _dataVault);
                    ClearExternalInputHandles();
                    _dataVault = currentService as IDataVault;
                    _tuningSeeded = false;
                    _profilesSeeded = false;
                    _telemetryCursorSeeded = false;
                    _vaultStateReady = false;
                    EnsureVaultState();
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.CausticsRuntime:
                    _ownsRegistrySlot = ReferenceEquals(currentService, this);
                    break;
            }
        }

        public static bool TryGetActiveConstantBuffer(out GraphicsBuffer constantBuffer)
        {
            AbyssalDeferredCausticsRuntime runtime = s_runtimeInstance;
            if (runtime != null && runtime._activeConstantBuffer != null && runtime._activeConstantBuffer.IsValid())
            {
                constantBuffer = runtime._activeConstantBuffer;
                return true;
            }

            constantBuffer = null;
            return false;
        }

        public static bool TryGetActiveParameters(out CausticsParametersDTO parameters)
        {
            AbyssalDeferredCausticsRuntime runtime = s_runtimeInstance;
            if (runtime != null &&
                runtime.TryResolveVaultBuffer(in runtime._parametersHandle, AbyssalCausticsConstants.ParameterCapacity, out NativeArray<CausticsParametersDTO> parametersArray))
            {
                parameters = parametersArray[AbyssalCausticsConstants.ActiveParameterIndex];
                return true;
            }

            parameters = default;
            return false;
        }

        public static bool TryGetTuning(out CausticsTuningDTO tuning)
        {
            AbyssalDeferredCausticsRuntime runtime = s_runtimeInstance;
            if (runtime != null &&
                runtime.TryResolveVaultBuffer(in runtime._tuningHandle, 1, out NativeArray<CausticsTuningDTO> tuningArray))
            {
                tuning = tuningArray[0];
                return true;
            }

            tuning = GenerateMockCausticLightingJob.DefaultTuning();
            return false;
        }

        public static bool TrySetEditorTuning(float chromaticDispersion, float noiseScale, float flowSpeedMultiplier, float maxDepthMeters)
        {
            AbyssalDeferredCausticsRuntime runtime = s_runtimeInstance;
            if (runtime == null)
                return false;

            return runtime.TrySetTuningInternal(chromaticDispersion, noiseScale, flowSpeedMultiplier, maxDepthMeters);
        }

        public static bool TryLoadLightingProfilesCsv(string projectRelativePath)
        {
            AbyssalDeferredCausticsRuntime runtime = s_runtimeInstance;
            return runtime != null && runtime.LoadLightingProfilesCsv(projectRelativePath);
        }

        public bool LoadLightingProfilesCsv(string projectRelativePath)
        {
            EnsureVaultState();
            EnsureCsvScratch();
            if (!TryResolveVaultBuffer(in _csvScratchHandle, AbyssalCausticsConstants.CsvScratchBytes, out NativeArray<byte> csvScratch) ||
                !TryResolveVaultBuffer(in _profilesHandle, AbyssalCausticsConstants.ProfileCapacity, out NativeArray<CausticsLightingProfileDTO> profiles))
            {
                return false;
            }

            string fullPath = BuildProjectPath(projectRelativePath);
            int byteCount = LoadFileBytesIntoScratch(fullPath, csvScratch);
            if (byteCount <= 0)
                return false;

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(csvScratch);
            ReadOnlySpan<byte> csvBytes = new ReadOnlySpan<byte>(ptr, byteCount);
            int parsed = ParseLightingProfiles(csvBytes, profiles);
            if (parsed <= 0)
                return false;

            _profilesSeeded = true;
            return true;
        }

        private void Awake()
        {
            if (s_runtimeInstance != null && !ReferenceEquals(s_runtimeInstance, this))
            {
                Destroy(gameObject);
                return;
            }

            s_runtimeInstance = this;
            CacheRegistryServicesCold(forceRefresh: true);
            TryRegisterHotSwap();
            EnsureSingletonOwnership();
        }

        private void OnEnable()
        {
            if (s_runtimeInstance != null && !ReferenceEquals(s_runtimeInstance, this))
            {
                Destroy(gameObject);
                return;
            }

            s_runtimeInstance = this;
            CacheRegistryServicesCold(forceRefresh: false);
            TryRegisterHotSwap();
            EnsureSingletonOwnership();
            if (_isInitialized)
            {
                TryRegisterUpdate();
                TryRegisterLateFrame();
                TryRegisterOriginShift();
            }
        }

        private void OnDisable()
        {
            TryUnregisterUpdate();
            TryUnregisterLateFrame();
            TryUnregisterOriginShift();
            TryUnregisterHotSwap();
            if (ReferenceEquals(GlobalRegistry.Caustics, this))
                GlobalRegistry.UnregisterCausticsService(this);
            _ownsRegistrySlot = false;
            if (ReferenceEquals(s_runtimeInstance, this))
                s_runtimeInstance = null;
            _activeConstantBuffer = null;
        }

        private void OnDestroy()
        {
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
            ICausticsService registered = GlobalRegistry.Caustics;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                _ownsRegistrySlot = false;
                return false;
            }

            if (!ReferenceEquals(registered, this))
                GlobalRegistry.RegisterCausticsService(this);
            _ownsRegistrySlot = true;
            return true;
        }

        private void EnsureVaultState()
        {
            if (_dataVault == null)
                return;

            if (_vaultStateReady && AreOwnedVaultHandlesCreated())
                return;

            bool hasParameters = AcquireOrRefreshOwnedVaultBuffer(
                BufferID.ShinobuCausticsParameters,
                AbyssalCausticsConstants.ParameterCapacity,
                NativeArrayOptions.UninitializedMemory,
                ref _parametersHandle,
                out NativeArray<CausticsParametersDTO> _);
            bool hasTuning = AcquireOrRefreshOwnedVaultBuffer(
                BufferID.ShinobuCausticsTuning,
                1,
                NativeArrayOptions.UninitializedMemory,
                ref _tuningHandle,
                out NativeArray<CausticsTuningDTO> tuning);
            bool hasTelemetry = AcquireOrRefreshOwnedVaultBuffer(
                BufferID.ShinobuCausticsTelemetryRing,
                AbyssalCausticsConstants.TelemetryCapacity,
                NativeArrayOptions.UninitializedMemory,
                ref _telemetryHandle,
                out NativeArray<CausticsTelemetryEntry> _);
            bool hasTelemetryCursor = AcquireOrRefreshOwnedVaultBuffer(
                BufferID.ShinobuCausticsTelemetryCursor,
                1,
                NativeArrayOptions.UninitializedMemory,
                ref _telemetryCursorHandle,
                out NativeArray<int> telemetryCursor);
            bool hasProfiles = AcquireOrRefreshOwnedVaultBuffer(
                BufferID.ShinobuCausticsProfiles,
                AbyssalCausticsConstants.ProfileCapacity,
                NativeArrayOptions.UninitializedMemory,
                ref _profilesHandle,
                out NativeArray<CausticsLightingProfileDTO> profiles);

            if (!_telemetryCursorSeeded && telemetryCursor.IsCreated && telemetryCursor.Length > 0)
            {
                telemetryCursor[0] = 0;
                _telemetryCursorSeeded = true;
            }

            SeedTuningIfNeeded(tuning);
            SeedProfilesIfNeeded(profiles);
            RefreshExternalInputHandles();
            _vaultStateReady = hasParameters &&
                               hasTuning &&
                               hasTelemetry &&
                               hasTelemetryCursor &&
                               hasProfiles &&
                               _telemetryCursorSeeded &&
                               _tuningSeeded &&
                               _profilesSeeded;
        }

        private bool AreOwnedVaultHandlesCreated()
        {
            return IsVaultHandleCreated(in _parametersHandle) &&
                   IsVaultHandleCreated(in _tuningHandle) &&
                   IsVaultHandleCreated(in _telemetryHandle) &&
                   IsVaultHandleCreated(in _telemetryCursorHandle) &&
                   IsVaultHandleCreated(in _profilesHandle);
        }

        private void SeedTuningIfNeeded(NativeArray<CausticsTuningDTO> tuning)
        {
            if (_tuningSeeded || !tuning.IsCreated || tuning.Length < 1)
                return;

            tuning[0] = GenerateMockCausticLightingJob.DefaultTuning();
            _tuningSeeded = true;
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
            if (TryResolveVaultBuffer(in _csvScratchHandle, AbyssalCausticsConstants.CsvScratchBytes, out NativeArray<byte> _))
                return;

            AcquireOrRefreshOwnedVaultBuffer(
                BufferID.ShinobuCausticsCsvScratch,
                AbyssalCausticsConstants.CsvScratchBytes,
                NativeArrayOptions.UninitializedMemory,
                ref _csvScratchHandle,
                out NativeArray<byte> _);
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

        private bool UploadParametersToGpu()
        {
            if (!TryResolveVaultBuffer(in _parametersHandle, AbyssalCausticsConstants.ParameterCapacity, out NativeArray<CausticsParametersDTO> parameters) || !EnsureConstantBuffers())
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
            return true;
        }

        private void RunMockLightingKernel()
        {
            if (!TryResolveVaultBuffer(in _parametersHandle, AbyssalCausticsConstants.ParameterCapacity, out NativeArray<CausticsParametersDTO> parameters))
                return;

            TryResolveVaultBuffer(in _tuningHandle, 1, out NativeArray<CausticsTuningDTO> tuning);
            NativeArray<WeatherStateDTO> emptyWeather = default;
            NativeArray<WaveParametersDTO> emptyWaveParameters = default;
            NativeArray<float4> emptySurfaceSwell = default;
            NativeArray<CausticsLightingProfileDTO> emptyProfiles = default;
            CausticsInputSnapshotDTO inputSnapshot = CaptureCausticsInputSnapshot(
                tuning,
                emptyWeather,
                emptyWaveParameters,
                emptySurfaceSwell,
                emptyProfiles,
                ResolveGlobalQualityWeight01(),
                _presentationTimeSeconds);

            GenerateMockCausticLightingJob job = default;
            job.Parameters = parameters;
            job.InputSnapshot = inputSnapshot;
            job.CameraAupLocalOffset = ResolveCameraAupLocalOffset();
            job.TimeSeconds = _presentationTimeSeconds;
            job.GlobalQualityWeight = inputSnapshot.WeatherStormWindPhaseQuality.w;
            job.FrameIndex = _presentationFrameIndex;
            job.OutputIndex = AbyssalCausticsConstants.PendingParameterIndex;
            job.Run();
            PublishPendingCausticsParameters();
        }

        private bool TrySetTuningInternal(float chromaticDispersion, float noiseScale, float flowSpeedMultiplier, float maxDepthMeters)
        {
            EnsureVaultState();
            if (!TryResolveVaultBuffer(in _tuningHandle, 1, out NativeArray<CausticsTuningDTO> tuningArray))
                return false;

            CausticsTuningDTO tuning = tuningArray[0];
            tuning.ScaleFlowDepthIntensity.x = math.max(0.005f, noiseScale);
            tuning.ScaleFlowDepthIntensity.y = math.max(0f, flowSpeedMultiplier);
            tuning.ScaleFlowDepthIntensity.z = math.max(1f, maxDepthMeters);
            tuning.DispersionSdfTileProfile.x = math.saturate(chromaticDispersion);
            tuningArray[0] = tuning;
            _pendingGpuUpload = false;
            ScheduleMockLightingJob();
            return true;
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(quality, 1f, !math.isfinite(quality)));
        }

        private double3 ResolveCameraAupLocalOffset()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null &&
                playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                MathGuard.IsFinite(in snapshot.Aup))
            {
                AbsoluteUniversePosition aup = snapshot.Aup;
                return new double3(aup.LocalX, aup.LocalY, aup.LocalZ);
            }

            HectonPlayerMovement movement = playerContext != null ? playerContext.PlayerMovement : null;
            if (movement != null)
            {
                AbsoluteUniversePosition aup = movement.CurrentAup;
                return new double3(aup.LocalX, aup.LocalY, aup.LocalZ);
            }

            return default;
        }

        private void UpdateLegacyCausticsAup(in CausticsParametersDTO parameters)
        {
            _legacyCausticsAup = new Vector4(
                parameters.NoiseAnimationSpeed.x,
                parameters.NoiseAnimationSpeed.y,
                parameters.ProjectionVectorAndScale.w,
                IsComputeActive ? 1f : 0f);
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

        private bool PublishPendingCausticsParameters()
        {
            if (!TryResolveVaultBuffer(in _parametersHandle, AbyssalCausticsConstants.ParameterCapacity, out NativeArray<CausticsParametersDTO> parameters))
                return false;

            parameters[AbyssalCausticsConstants.ActiveParameterIndex] = parameters[AbyssalCausticsConstants.PendingParameterIndex];
            CausticsParametersDTO activeParameters = parameters[AbyssalCausticsConstants.ActiveParameterIndex];
            UpdateLegacyCausticsAup(in activeParameters);
            CheckFaultsAndDump(in activeParameters);
            _pendingGpuUpload = true;
            return true;
        }

        private static CausticsInputSnapshotDTO CaptureCausticsInputSnapshot(
            NativeArray<CausticsTuningDTO> tuningArray,
            NativeArray<WeatherStateDTO> weatherArray,
            NativeArray<WaveParametersDTO> waveParametersArray,
            NativeArray<float4> surfaceSwellArray,
            NativeArray<CausticsLightingProfileDTO> profilesArray,
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

            if (weatherArray.IsCreated && weatherArray.Length > 0)
            {
                WeatherStateDTO weather = weatherArray[0];
                storm = Sanitize01(weather.WindDirectionSpeedStorm.w, 0f);
                windSpeed = SanitizeNonNegative(weather.WindDirectionSpeedStorm.z, 0f);
                if (math.isfinite(weather.GlobalQualityWeight))
                    quality = math.min(quality, math.saturate(weather.GlobalQualityWeight));
                if (math.isfinite(weather.MaxWaveAmplitude))
                    waveHeight = math.max(waveHeight, math.max(0f, weather.MaxWaveAmplitude));
                weatherStateMask = weather.StateMask;
                flags |= AbyssalCausticsConstants.FlagWeatherVaultBound;
            }

            if (waveParametersArray.IsCreated && waveParametersArray.Length > 0)
            {
                WaveParametersDTO waveParameters = HectonOceanSurfaceMath.SanitizeWave(waveParametersArray[0]);
                float4 lane = waveParameters.Wave1;
                waveHeight = math.max(waveHeight, HectonOceanSurfaceMath.WaveLaneAmplitude(lane));
                float wavelength = HectonOceanSurfaceMath.WaveLaneWavelength(lane);
                waveFrequency = math.max(waveFrequency, math.rcp(math.max(wavelength, 0.0001f)));
                wavePhase += HectonOceanSurfaceMath.WaveLaneSpeed(lane) * timeSeconds;
                flags |= AbyssalCausticsConstants.FlagWaveVaultBound;
            }

            if (surfaceSwellArray.IsCreated && surfaceSwellArray.Length > 0)
            {
                float4 swell = math.select(float4.zero, surfaceSwellArray[0], math.isfinite(surfaceSwellArray[0]));
                waveHeight = math.max(waveHeight, math.abs(swell.x));
                waveFrequency = math.max(waveFrequency, math.abs(swell.y));
                wavePhase = math.select(wavePhase, swell.z, math.isfinite(swell.z));
                flags |= AbyssalCausticsConstants.FlagWaveVaultBound;
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

            CausticsInputSnapshotDTO snapshot;
            snapshot.Tuning = tuning;
            snapshot.WeatherStormWindPhaseQuality = new float4(storm, windSpeed, wavePhase, quality);
            snapshot.WaveHeightFrequencyReserved = new float4(
                math.max(0.01f, waveHeight),
                math.max(0.02f, waveFrequency),
                0f,
                0f);
            snapshot.ProfileIntensityScaleDepthFlow = new float4(profileIntensity, profileScale, profileDepth, profileFlow);
            snapshot.ProfileChromaticSdf = new float2(profileChromatic, profileSdf);
            snapshot.Flags = flags;
            snapshot.Reserved = 0u;
            return snapshot;
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

        private static int LoadFileBytesIntoScratch(string fullPath, NativeArray<byte> csvScratch)
        {
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath) || !csvScratch.IsCreated)
                return 0;

            byte* dst = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(csvScratch);
            int capacity = csvScratch.Length;
            int total = 0;
            Span<byte> block = stackalloc byte[512];
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

        private void TryRegisterUpdate()
        {
            if (_registeredUpdate || !Application.isPlaying)
                return;

            _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterUpdate()
        {
            if (!_registeredUpdate)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registeredUpdate = false;
        }

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
                _dataVault = GlobalRegistry.DataVault;
            if (forceRefresh || _playerRuntimeContext == null)
                _playerRuntimeContext = GlobalRegistry.Player;
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

        private void RefreshExternalInputHandles()
        {
            RefreshExternalInputHandle(BufferID.ShinobuOceanWeatherState, 1, ref _weatherInputHandle);
            RefreshExternalInputHandle(BufferID.ShinobuOceanWaveParameters, 1, ref _waveInputHandle);
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

            if (IsVaultHandleCreated(in handle) &&
                vault.TryResolveHandle(in handle, out NativeArray<T> buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            handle = default;
            if (!vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existingHandle) ||
                !IsVaultHandleCreated(in existingHandle))
            {
                return false;
            }

            handle = existingHandle;
            if (vault.TryResolveHandle(in handle, out NativeArray<T> refreshedBuffer) &&
                refreshedBuffer.IsCreated &&
                refreshedBuffer.Length >= requiredLength)
            {
                return true;
            }

            handle = default;
            return false;
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

            handle = vault.GetGenerationHandle<T>(bufferId, requiredLength, OwnerSystemId, options);
            return IsVaultHandleCreated(in handle) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private void ShutdownServiceState()
        {
            TryUnregisterUpdate();
            TryUnregisterLateFrame();
            TryUnregisterOriginShift();
            TryUnregisterHotSwap();
            if (ReferenceEquals(GlobalRegistry.Caustics, this))
                GlobalRegistry.UnregisterCausticsService(this);
            _ownsRegistrySlot = false;
            if (ReferenceEquals(s_runtimeInstance, this))
                s_runtimeInstance = null;

            _constantBufferA?.Release();
            _constantBufferB?.Release();
            _constantBufferA = null;
            _constantBufferB = null;
            _activeConstantBuffer = null;

            ReleaseAllVaultHandles(_dataVault);

            _isInitialized = false;
            _pendingGpuUpload = false;
            _tuningSeeded = false;
            _profilesSeeded = false;
            _telemetryCursorSeeded = false;
            _vaultStateReady = false;
            ClearExternalInputHandles();
        }

        private void ReleaseAllVaultHandles(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref _parametersHandle);
            ReleaseVaultHandle(vault, ref _tuningHandle);
            ReleaseVaultHandle(vault, ref _telemetryHandle);
            ReleaseVaultHandle(vault, ref _telemetryCursorHandle);
            ReleaseVaultHandle(vault, ref _profilesHandle);
            ReleaseVaultHandle(vault, ref _csvScratchHandle);
            _vaultStateReady = false;
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null && IsVaultHandleCreated(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void ClearExternalInputHandles()
        {
            _weatherInputHandle = default;
            _waveInputHandle = default;
            _surfaceSwellInputHandle = default;
        }

        private void DumpBlackBox()
        {
            if (!TryResolveVaultBuffer(in _telemetryHandle, AbyssalCausticsConstants.TelemetryCapacity, out NativeArray<CausticsTelemetryEntry> telemetry))
                return;

            TryResolveVaultBuffer(in _telemetryCursorHandle, 1, out NativeArray<int> telemetryCursor);
            string root = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(root))
                return;

            string path = Path.Combine(root, DumpPath);
            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
                return;

            Directory.CreateDirectory(directory);
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(0x32334353u); // SC32
                writer.Write(AbyssalCausticsConstants.TelemetryCapacity);
                writer.Write(telemetryCursor.IsCreated && telemetryCursor.Length > 0 ? telemetryCursor[0] : 0);
                writer.Write(_lastFaultFlags);
                writer.Write(UnsafeUtility.SizeOf<CausticsTelemetryEntry>());
                for (int i = 0; i < telemetry.Length; i++)
                {
                    CausticsTelemetryEntry entry = telemetry[i];
                    writer.Write(entry.FrameIndex);
                    writer.Write(entry.StateHash);
                    writer.Write(entry.Flags);
                    writer.Write(entry.ActiveNoiseOctavesX1000);
                    writer.Write(entry.SunIntensity);
                    writer.Write(entry.ActiveNoiseOctaves);
                    writer.Write(entry.MaxDepthMeters);
                    writer.Write(entry.EstimatedGpuMicros);
                    writer.Write(entry.ProjectionVectorAndScale.x);
                    writer.Write(entry.ProjectionVectorAndScale.y);
                    writer.Write(entry.ProjectionVectorAndScale.z);
                    writer.Write(entry.ProjectionVectorAndScale.w);
                    writer.Write(entry.NoiseAnimationSpeed.x);
                    writer.Write(entry.NoiseAnimationSpeed.y);
                    writer.Write(entry.NoiseAnimationSpeed.z);
                    writer.Write(entry.NoiseAnimationSpeed.w);
                }
            }
        }
    }

    public static class AbyssalCausticsShaderIds
    {
        public static readonly int ConstantBufferId = Shader.PropertyToID("HectonAbyssalCaustics");
        public static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        public static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
    }
}
