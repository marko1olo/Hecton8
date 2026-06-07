using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Atmosphere
{
    [DefaultExecutionOrder(-2512)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Atmosphere/Shinobu Storm Propagation Runtime")]
    public sealed unsafe class ShinobuStormPropagationRuntime : MonoBehaviour,
        IUpdatable,
        ISlowTickable,
        ILateFrameTickable,
        IGlobalRegistryHotSwapListener,
        IGlobalRegistryHotSwapRefListener,
        IOriginShiftListener,
        IDisposable
    {
        private const SystemID OwnerSystem = SystemID.HabitatAtmosphere;
        private const float SimulationTickDeltaSeconds = 1f / 60f;
        private const float MinimumScheduleIntervalSeconds = 1f / 60f;
        private const float MaximumScheduleIntervalSeconds = 1f / 5f;
        private const uint HotSwapReasonHash = 0x53504853u; // SPHS
#if UNITY_EDITOR
        private const string ImpactCsvRelativePath = "Assets/_SourceData/Atmosphere/storm_depth_impact_profiles.csv";
#endif
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_234.bin";
        private static int s_runtimeClaimed;

        [SerializeField] private bool autoGenerateEmergencyMockHurricane;
        [SerializeField, Range(0f, 1f)] private float editorPreviewQualityWeight = 1f;
        [SerializeField] private double seaLevelAupY;

        private IDataVault _vault;
        private ITickDispatcher _tickDispatcher;
        private VaultGenerationHandle<WeatherStateDTO> _weatherHandle;
        private VaultGenerationHandle<StormPropagationDTO> _publishedStateHandle;
        private VaultGenerationHandle<StormPropagationTuningDTO> _tuningHandle;
        private VaultGenerationHandle<StormPropagationTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<MockHurricaneStateDTO> _mockWeatherHandle;
        private VaultGenerationHandle<StormDepthImpactProfileDTO> _profilesHandle;
#if UNITY_EDITOR
        private byte[] _impactCsvManagedScratch;
#endif
        private VaultGenerationHandle<float4> _flowScalarHandle;
        private VaultGenerationHandle<float4> _audioScalarHandle;
        private VaultGenerationHandle<float4> _biolumScalarHandle;
        private VaultGenerationHandle<float4> _fogScalarHandle;
        private StormPropagationJobStagingBuffers _jobStagingBuffers;
        private ref NativeArray<WeatherStateDTO> _jobWeatherSnapshot => ref _jobStagingBuffers.WeatherSnapshot;
        private ref NativeArray<StormPropagationTuningDTO> _jobTuningSnapshot => ref _jobStagingBuffers.TuningSnapshot;
        private ref NativeArray<StormDepthImpactProfileDTO> _jobProfileSnapshot => ref _jobStagingBuffers.ProfileSnapshot;
        private ref NativeArray<MockHurricaneStateDTO> _jobMockWeather => ref _jobStagingBuffers.MockWeather;
        private ref NativeArray<StormPropagationWriteSnapshotDTO> _jobWriteSnapshot => ref _jobStagingBuffers.WriteSnapshot;
        private ref NativeArray<StormPropagationTelemetryEntry> _jobTelemetry => ref _jobStagingBuffers.Telemetry;
        private ref NativeArray<int> _jobTelemetryCursor => ref _jobStagingBuffers.TelemetryCursor;
        private JobHandle _attenuationJobHandle;
        private JobHandle _mockHurricaneJobHandle;
        private double3 _lastOriginFallbackAup;
        private double3 _lastSeaLevelAup;
        private double3 _cachedOriginFallbackAup;
        private float _scheduleAccumulatorSeconds;
        private float _previousSurfaceIntensity01;
        private float _lastKnownQualityWeight = 1f;
        private float _cachedPublicationCadenceHz = ShinobuStormPropagationConstants.DefaultPublicationCadenceHz;
        private float _lastScheduleToPublishMicroseconds;
        private uint _lastTelemetryReasonFlags;
        private uint _lastTelemetryStateHash;
        private uint _frame;
        private int _runtimeClaimHeld;
        private int _registeredUpdate;
        private int _registeredSlow;
        private int _registeredLate;
        private int _registeredHotSwap;
        private int _registeredOriginShift;
        private byte[] _dumpManagedScratch;
        private bool _vaultReady;
        private bool _attenuationScheduled;
        private bool _mockScheduled;
        private bool _publishedFaultDump;
        private bool _disposed;
        private bool _impactProfilesLoaded;
        private long _jobScheduleTimestamp;
        private uint _pendingFaultDumpReasonFlags;
        private uint _pendingFaultDumpStateHash;

        private struct StormPropagationJobStagingBuffers
        {
            public NativeArray<WeatherStateDTO> WeatherSnapshot;
            public NativeArray<StormPropagationTuningDTO> TuningSnapshot;
            public NativeArray<StormDepthImpactProfileDTO> ProfileSnapshot;
            public NativeArray<MockHurricaneStateDTO> MockWeather;
            public NativeArray<StormPropagationWriteSnapshotDTO> WriteSnapshot;
            public NativeArray<StormPropagationTelemetryEntry> Telemetry;
            public NativeArray<int> TelemetryCursor;

            public bool IsReady =>
                WeatherSnapshot.IsCreated && WeatherSnapshot.Length > 0 &&
                TuningSnapshot.IsCreated && TuningSnapshot.Length > 0 &&
                ProfileSnapshot.IsCreated && ProfileSnapshot.Length >= ShinobuStormPropagationConstants.ImpactProfileCapacity &&
                MockWeather.IsCreated && MockWeather.Length > 0 &&
                WriteSnapshot.IsCreated && WriteSnapshot.Length > 0 &&
                Telemetry.IsCreated && Telemetry.Length > 0 &&
                TelemetryCursor.IsCreated && TelemetryCursor.Length > 0;

            public void Ensure()
            {
                EnsureNativeJobArray(ref WeatherSnapshot, 1, nameof(WeatherSnapshot));
                EnsureNativeJobArray(ref TuningSnapshot, 1, nameof(TuningSnapshot));
                EnsureNativeJobArray(ref ProfileSnapshot, ShinobuStormPropagationConstants.ImpactProfileCapacity, nameof(ProfileSnapshot));
                EnsureNativeJobArray(ref MockWeather, 1, nameof(MockWeather));
                EnsureNativeJobArray(ref WriteSnapshot, 1, nameof(WriteSnapshot));
                EnsureNativeJobArray(ref Telemetry, 1, nameof(Telemetry));
                EnsureNativeJobArray(ref TelemetryCursor, 1, nameof(TelemetryCursor));
            }

            public void Dispose()
            {
                DisposeNativeJobArray(ref TelemetryCursor);
                DisposeNativeJobArray(ref Telemetry);
                DisposeNativeJobArray(ref WriteSnapshot);
                DisposeNativeJobArray(ref MockWeather);
                DisposeNativeJobArray(ref ProfileSnapshot);
                DisposeNativeJobArray(ref TuningSnapshot);
                DisposeNativeJobArray(ref WeatherSnapshot);
            }

            private static void EnsureNativeJobArray<T>(ref NativeArray<T> array, int length, string label)
                where T : struct
            {
                if (array.IsCreated && array.Length == length)
                    return;

                DisposeNativeJobArray(ref array);
                array = H8Memory.Allocate<T>(length, OwnerSystem, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                if (!array.IsCreated)
                    throw new InvalidOperationException($"{nameof(ShinobuStormPropagationRuntime)} native allocation failed for {label}.");
            }

            private static void DisposeNativeJobArray<T>(ref NativeArray<T> array)
                where T : struct
            {
                if (!array.IsCreated)
                    return;

                H8Memory.Release(ref array, OwnerSystem);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeClaim()
        {
            Volatile.Write(ref s_runtimeClaimed, 0);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureSceneRuntime()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!Application.isPlaying)
                return;

            if (Volatile.Read(ref s_runtimeClaimed) != 0)
                return;

            GameObject host = new GameObject("H8_ShinobuStormPropagationRuntime"); // COLD ALLOC: GameObject[1] - scene-local storm propagation runtime root - owner: ShinobuStormPropagationRuntime
            host.hideFlags = HideFlags.DontSave;
            host.AddComponent<ShinobuStormPropagationRuntime>(); // COLD ALLOC: ShinobuStormPropagationRuntime[1] - auto-bootstrap fallback component - owner: ShinobuStormPropagationRuntime
#endif
        }

        private void OnEnable()
        {
            _disposed = false;
            if (!Application.isPlaying)
                return;

            if (!TryClaimRuntime())
            {
                enabled = false;
                return;
            }

            if (!ShinobuStormPropagationNative.ValidateLayouts())
            {
                ReleaseRuntimeClaim();
                enabled = false;
                return;
            }

            TryRegisterHotSwapListener();
            RefreshCachedOriginFallbackAupCold();
            TryRegisterOriginShiftListener();
            RefreshCachedRegistryServices();
            EnsureVaultBuffersCold();
            LoadImpactProfilesCold();
            TryRegisterUpdate();
            TryRegisterSlow();
            TryRegisterLate();
        }

        private void OnDisable()
        {
            CompleteScheduledJobsForShutdown();
            TryUnregisterUpdate();
            TryUnregisterSlow();
            TryUnregisterLate();
            TryUnregisterOriginShiftListener();
            TryUnregisterHotSwapListener();
            ReleaseRuntimeClaim();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            CompleteScheduledJobsForShutdown();
            DisposeJobStagingCold();
            ReleaseVaultStateForLifecycle(_vault);
            _vault = null;
        }

        public void Tick(float deltaTime)
        {
            if (!_vaultReady)
                return;

            _ = deltaTime;
            float dt = SimulationTickDeltaSeconds;
            _frame++;
            if (_frame == 0u)
                _frame = 1u;

            if (_attenuationScheduled)
                return;

            _scheduleAccumulatorSeconds += dt;
            float quality = SampleGlobalQualityWeightForTick();
            float cadenceHz = SamplePublicationCadenceHzForTick(quality);
            float interval = math.clamp(math.rcp(math.max(0.001f, cadenceHz)), MinimumScheduleIntervalSeconds, MaximumScheduleIntervalSeconds);
            if (_scheduleAccumulatorSeconds < interval)
                return;

            _scheduleAccumulatorSeconds = 0f;
            SchedulePropagationJobs(dt, quality);
        }

        public void SlowTick()
        {
            if (!_vaultReady)
                return;

            if (_attenuationScheduled)
                return;

            if (!IsHabitatAtmosphereHandle(in _weatherHandle, BufferID.ShinobuOceanWeatherState))
                TryRefreshWeatherHandleCold();

            RefreshCachedTuningSnapshotCold();

            if (!_publishedFaultDump && _pendingFaultDumpReasonFlags != 0u)
            {
                _publishedFaultDump = TryDumpTelemetryToDisk(_pendingFaultDumpReasonFlags, _pendingFaultDumpStateHash);
                if (_publishedFaultDump)
                {
                    _pendingFaultDumpReasonFlags = 0u;
                    _pendingFaultDumpStateHash = 0u;
                }

                return;
            }

            if (!_impactProfilesLoaded)
                LoadImpactProfilesCold();
        }

        public void LateFrameTick()
        {
            CompleteFinishedAttenuationJob();

            if (!_attenuationScheduled &&
                !_publishedFaultDump &&
                (_lastTelemetryReasonFlags & ShinobuStormPropagationConstants.TelemetryFlagNonFinite) != 0u)
            {
                _pendingFaultDumpReasonFlags = _lastTelemetryReasonFlags;
                _pendingFaultDumpStateHash = _lastTelemetryStateHash;
                _lastTelemetryReasonFlags = 0u;
                _lastTelemetryStateHash = 0u;
            }
        }

        void IGlobalRegistryHotSwapRefListener.OnGlobalRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            _ = previousService;
            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _cachedOriginFallbackAup = SanitizeAup(shiftData.NewTotalOffsetDouble);
        }

        private bool TryClaimRuntime()
        {
            if (_runtimeClaimHeld != 0)
                return true;

            if (Interlocked.CompareExchange(ref s_runtimeClaimed, 1, 0) != 0)
                return false;

            _runtimeClaimHeld = 1;
            return true;
        }

        private void ReleaseRuntimeClaim()
        {
            if (_runtimeClaimHeld == 0)
                return;

            _runtimeClaimHeld = 0;
            Volatile.Write(ref s_runtimeClaimed, 0);
        }

        private void TryRegisterUpdate()
        {
            if (_registeredUpdate != 0 || _tickDispatcher == null)
                return;

            _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment) ? 1 : 0;
        }

        private void TryRegisterSlow()
        {
            if (_registeredSlow != 0 || _tickDispatcher == null)
                return;

            _registeredSlow = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment) ? 1 : 0;
        }

        private void TryRegisterLate()
        {
            if (_registeredLate != 0 || _tickDispatcher == null)
                return;

            _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment) ? 1 : 0;
        }

        private void TryUnregisterUpdate()
        {
            if (_registeredUpdate == 0)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registeredUpdate = 0;
        }

        private void TryUnregisterSlow()
        {
            if (_registeredSlow == 0)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredSlow = 0;
        }

        private void TryUnregisterLate()
        {
            if (_registeredLate == 0)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLate = 0;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap != 0)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this) ? 1 : 0;
        }

        private void TryUnregisterHotSwapListener()
        {
            if (_registeredHotSwap == 0)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = 0;
        }

        private void TryRegisterOriginShiftListener()
        {
            if (_registeredOriginShift != 0)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShift = 1;
        }

        private void TryUnregisterOriginShiftListener()
        {
            if (_registeredOriginShift == 0)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _registeredOriginShift = 0;
        }

        private void RefreshCachedRegistryServices()
        {
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.Dispatcher, GlobalRegistry.TickDispatcher);
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.DataVault, GlobalRegistry.DataVault);
        }

        private void ApplyRegistryServiceRebind(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    if (!ReferenceEquals(_tickDispatcher, currentService))
                    {
                        TryUnregisterUpdate();
                        TryUnregisterSlow();
                        TryUnregisterLate();
                        _tickDispatcher = currentService as ITickDispatcher;
                        TryRegisterUpdate();
                        TryRegisterSlow();
                        TryRegisterLate();
                    }
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    IDataVault nextVault = currentService is IDataVault dataVault ? dataVault : null;
                    RebindDataVaultForLifecycle(nextVault);
                    break;
                case GlobalRegistryServiceSlot.FloatingOriginRuntime:
                    RefreshCachedOriginFallbackAupCold();
                    TryRegisterOriginShiftListener();
                    break;
            }
        }

        private void RebindDataVaultForLifecycle(IDataVault vault)
        {
            if (ReferenceEquals(_vault, vault))
                return;

            CompleteScheduledJobsForShutdown();
            ReleaseVaultStateForLifecycle(_vault);
            _vault = vault;
            ResetRuntimeStateForVaultRebind();
            EnsureVaultBuffersCold();
        }

        private bool EnsureVaultBuffersCold()
        {
            IDataVault vault = _vault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            TryRefreshWeatherHandleCold();
            if (!IsHabitatAtmosphereHandle(in _publishedStateHandle, BufferID.ShinobuStormPropagationState))
                _publishedStateHandle = vault.EnsureGenerationHandle<StormPropagationDTO>(BufferID.ShinobuStormPropagationState, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            if (!IsHabitatAtmosphereHandle(in _tuningHandle, BufferID.ShinobuStormPropagationTuning))
                _tuningHandle = vault.EnsureGenerationHandle<StormPropagationTuningDTO>(BufferID.ShinobuStormPropagationTuning, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            if (!IsHabitatAtmosphereHandle(in _telemetryHandle, BufferID.ShinobuStormPropagationTelemetryRing))
                _telemetryHandle = vault.EnsureGenerationHandle<StormPropagationTelemetryEntry>(BufferID.ShinobuStormPropagationTelemetryRing, ShinobuStormPropagationConstants.TelemetryFrameCount, OwnerSystem, NativeArrayOptions.ClearMemory);
            if (!IsHabitatAtmosphereHandle(in _telemetryCursorHandle, BufferID.ShinobuStormPropagationTelemetryCursor))
                _telemetryCursorHandle = vault.EnsureGenerationHandle<int>(BufferID.ShinobuStormPropagationTelemetryCursor, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            if (!IsHabitatAtmosphereHandle(in _mockWeatherHandle, BufferID.ShinobuStormPropagationMockWeather))
                _mockWeatherHandle = vault.EnsureGenerationHandle<MockHurricaneStateDTO>(BufferID.ShinobuStormPropagationMockWeather, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            if (!IsHabitatAtmosphereHandle(in _profilesHandle, BufferID.ShinobuStormPropagationImpactProfiles))
                _profilesHandle = vault.EnsureGenerationHandle<StormDepthImpactProfileDTO>(BufferID.ShinobuStormPropagationImpactProfiles, ShinobuStormPropagationConstants.ImpactProfileCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            EnsureDumpManagedScratchCold();
            EnsureJobStagingCold();
            if (!IsHabitatAtmosphereHandle(in _flowScalarHandle, BufferID.ShinobuStormPropagationFlowScalar))
                _flowScalarHandle = vault.EnsureGenerationHandle<float4>(BufferID.ShinobuStormPropagationFlowScalar, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            if (!IsHabitatAtmosphereHandle(in _audioScalarHandle, BufferID.ShinobuStormPropagationAudioScalar))
                _audioScalarHandle = vault.EnsureGenerationHandle<float4>(BufferID.ShinobuStormPropagationAudioScalar, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            if (!IsHabitatAtmosphereHandle(in _biolumScalarHandle, BufferID.ShinobuStormPropagationBiolumScalar))
                _biolumScalarHandle = vault.EnsureGenerationHandle<float4>(BufferID.ShinobuStormPropagationBiolumScalar, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            if (!IsHabitatAtmosphereHandle(in _fogScalarHandle, BufferID.ShinobuStormPropagationFogScalar))
                _fogScalarHandle = vault.EnsureGenerationHandle<float4>(BufferID.ShinobuStormPropagationFogScalar, 1, OwnerSystem, NativeArrayOptions.ClearMemory);

            _vaultReady =
                Resolve(in _publishedStateHandle, BufferID.ShinobuStormPropagationState, out NativeArray<StormPropagationDTO> published) && published.Length > 0 &&
                Resolve(in _tuningHandle, BufferID.ShinobuStormPropagationTuning, out NativeArray<StormPropagationTuningDTO> tuning) && tuning.Length > 0 &&
                Resolve(in _telemetryHandle, BufferID.ShinobuStormPropagationTelemetryRing, out NativeArray<StormPropagationTelemetryEntry> telemetry) && telemetry.Length >= ShinobuStormPropagationConstants.TelemetryFrameCount &&
                Resolve(in _telemetryCursorHandle, BufferID.ShinobuStormPropagationTelemetryCursor, out NativeArray<int> telemetryCursor) && telemetryCursor.Length > 0 &&
                Resolve(in _mockWeatherHandle, BufferID.ShinobuStormPropagationMockWeather, out NativeArray<MockHurricaneStateDTO> mock) && mock.Length > 0 &&
                Resolve(in _profilesHandle, BufferID.ShinobuStormPropagationImpactProfiles, out NativeArray<StormDepthImpactProfileDTO> profiles) && profiles.Length > 0 &&
                Resolve(in _flowScalarHandle, BufferID.ShinobuStormPropagationFlowScalar, out NativeArray<float4> flowScalar) && flowScalar.Length > 0 &&
                Resolve(in _audioScalarHandle, BufferID.ShinobuStormPropagationAudioScalar, out NativeArray<float4> audioScalar) && audioScalar.Length > 0 &&
                Resolve(in _biolumScalarHandle, BufferID.ShinobuStormPropagationBiolumScalar, out NativeArray<float4> biolumScalar) && biolumScalar.Length > 0 &&
                Resolve(in _fogScalarHandle, BufferID.ShinobuStormPropagationFogScalar, out NativeArray<float4> fogScalar) && fogScalar.Length > 0 &&
                HasJobStagingReady();

            if (_vaultReady)
                EnsureDefaultRowsCold();

            return _vaultReady;
        }

        private bool TryRefreshWeatherHandleCold()
        {
            if (_vault == null || _vault.IsCompactionFenceActive)
                return false;

            if (IsHabitatAtmosphereHandle(in _weatherHandle, BufferID.ShinobuOceanWeatherState))
                return true;

            if (_vault.TryGetGenerationHandle(BufferID.ShinobuOceanWeatherState, out _weatherHandle) &&
                IsHabitatAtmosphereHandle(in _weatherHandle, BufferID.ShinobuOceanWeatherState))
                return true;

            _weatherHandle = default;
            return false;
        }

        private void ClearVaultHandlesCold()
        {
            _weatherHandle = default;
            _publishedStateHandle = default;
            _tuningHandle = default;
            _telemetryHandle = default;
            _telemetryCursorHandle = default;
            _mockWeatherHandle = default;
            _profilesHandle = default;
            _flowScalarHandle = default;
            _audioScalarHandle = default;
            _biolumScalarHandle = default;
            _fogScalarHandle = default;
            _vaultReady = false;
            _impactProfilesLoaded = false;
        }

        private void ReleaseVaultStateForLifecycle(IDataVault vault)
        {
            _weatherHandle = default;
            ReleaseOwnedVaultHandle(vault, ref _publishedStateHandle, BufferID.ShinobuStormPropagationState);
            ReleaseOwnedVaultHandle(vault, ref _tuningHandle, BufferID.ShinobuStormPropagationTuning);
            ReleaseOwnedVaultHandle(vault, ref _telemetryHandle, BufferID.ShinobuStormPropagationTelemetryRing);
            ReleaseOwnedVaultHandle(vault, ref _telemetryCursorHandle, BufferID.ShinobuStormPropagationTelemetryCursor);
            ReleaseOwnedVaultHandle(vault, ref _mockWeatherHandle, BufferID.ShinobuStormPropagationMockWeather);
            ReleaseOwnedVaultHandle(vault, ref _profilesHandle, BufferID.ShinobuStormPropagationImpactProfiles);
            ReleaseOwnedVaultHandle(vault, ref _flowScalarHandle, BufferID.ShinobuStormPropagationFlowScalar);
            ReleaseOwnedVaultHandle(vault, ref _audioScalarHandle, BufferID.ShinobuStormPropagationAudioScalar);
            ReleaseOwnedVaultHandle(vault, ref _biolumScalarHandle, BufferID.ShinobuStormPropagationBiolumScalar);
            ReleaseOwnedVaultHandle(vault, ref _fogScalarHandle, BufferID.ShinobuStormPropagationFogScalar);
            _vaultReady = false;
            _impactProfilesLoaded = false;
        }

        private void EnsureDumpManagedScratchCold()
        {
            if (_dumpManagedScratch == null ||
                _dumpManagedScratch.Length < ShinobuStormPropagationConstants.DumpScratchBytes)
            {
                _dumpManagedScratch = new byte[ShinobuStormPropagationConstants.DumpScratchBytes]; // COLD ALLOC: byte[19232] - post-lock storm telemetry dump staging - owner: ShinobuStormPropagationRuntime
            }
        }

        private void EnsureJobStagingCold()
        {
            _jobStagingBuffers.Ensure();
        }

        private bool HasJobStagingReady()
        {
            return _jobStagingBuffers.IsReady;
        }

        private void DisposeJobStagingCold()
        {
            _jobStagingBuffers.Dispose();
        }

        private static void ReleaseOwnedVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId)
            where T : unmanaged
        {
            if (vault != null && IsOwnedStormPropagationHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsOwnedStormPropagationHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId)
            where T : unmanaged
        {
            return IsHabitatAtmosphereHandle(in handle, bufferId);
        }

        private static bool IsHabitatAtmosphereHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId)
            where T : unmanaged
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.Generation != 0u &&
                   handle.SystemID == (uint)OwnerSystem;
        }

        private static ulong StormPropagationMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private bool TryAcquireStormBufferGuard(BufferID bufferId)
        {
            IDataVault vault = _vault;
            return vault != null && vault.TryAcquireMutationGuard(StormPropagationMutationGuardBit(bufferId));
        }

        private void ReleaseStormBufferGuard(BufferID bufferId)
        {
            IDataVault vault = _vault;
            if (vault != null)
                vault.ReleaseMutationGuard(StormPropagationMutationGuardBit(bufferId));
        }

        private void ResetRuntimeStateForVaultRebind()
        {
            _scheduleAccumulatorSeconds = 0f;
            _previousSurfaceIntensity01 = 0f;
            _cachedPublicationCadenceHz = ShinobuStormPropagationConstants.DefaultPublicationCadenceHz;
            _lastScheduleToPublishMicroseconds = 0f;
            _lastTelemetryReasonFlags = 0u;
            _lastTelemetryStateHash = 0u;
            _publishedFaultDump = false;
            _pendingFaultDumpReasonFlags = 0u;
            _pendingFaultDumpStateHash = 0u;
            _jobScheduleTimestamp = 0L;
        }

        private void MarkVaultHandlesStaleAfterResolveFailure()
        {
            if (_attenuationScheduled)
                return;

            ClearVaultHandlesCold();
        }

        private void EnsureDefaultRowsCold()
        {
            if (_vault == null || _vault.IsCompactionFenceActive)
                return;

            StormPropagationTuningDTO tuningRow = default;
            bool hasTuningRow = false;
            bool tuningLocked = false;
            try
            {
                tuningLocked = TryAcquireStormBufferGuard(BufferID.ShinobuStormPropagationTuning);
                if (!tuningLocked)
                    return;

                if (Resolve(in _tuningHandle, BufferID.ShinobuStormPropagationTuning, out NativeArray<StormPropagationTuningDTO> tuning) && tuning.Length > 0)
                {
                    tuningRow = ShinobuStormPropagationNative.ReadElement(tuning, 0);
                    hasTuningRow = true;
                }
            }
            finally
            {
                if (tuningLocked) ReleaseStormBufferGuard(BufferID.ShinobuStormPropagationTuning);
            }

            if (hasTuningRow)
            {
                tuningRow = ShinobuStormPropagationNative.SanitizeTuning(tuningRow, SampleGlobalQualityWeightForTick());
                tuningLocked = false;
                try
                {
                    tuningLocked = TryAcquireStormBufferGuard(BufferID.ShinobuStormPropagationTuning);
                    if (!tuningLocked)
                        return;

                    if (Resolve(in _tuningHandle, BufferID.ShinobuStormPropagationTuning, out NativeArray<StormPropagationTuningDTO> tuning) && tuning.Length > 0)
                        ShinobuStormPropagationNative.ElementAt(tuning, 0) = tuningRow;
                }
                finally
                {
                    if (tuningLocked) ReleaseStormBufferGuard(BufferID.ShinobuStormPropagationTuning);
                }

                if (math.isfinite(tuningRow.PublicationCadenceHz) && tuningRow.PublicationCadenceHz > 0.001f)
                    _cachedPublicationCadenceHz = tuningRow.PublicationCadenceHz;
            }

            StormDepthImpactProfileDTO profileRow = default;
            bool hasProfileRow = false;
            bool profilesLocked = false;
            try
            {
                profilesLocked = TryAcquireStormBufferGuard(BufferID.ShinobuStormPropagationImpactProfiles);
                if (!profilesLocked)
                    return;

                if (Resolve(in _profilesHandle, BufferID.ShinobuStormPropagationImpactProfiles, out NativeArray<StormDepthImpactProfileDTO> profiles) && profiles.Length > 0)
                {
                    profileRow = ShinobuStormPropagationNative.ReadElement(profiles, 0);
                    hasProfileRow = true;
                }
            }
            finally
            {
                if (profilesLocked) ReleaseStormBufferGuard(BufferID.ShinobuStormPropagationImpactProfiles);
            }

            if (hasProfileRow && profileRow.ProfileHash == 0u)
            {
                profileRow = ShinobuStormPropagationNative.CreateFallbackProfile();
                profilesLocked = false;
                try
                {
                    profilesLocked = TryAcquireStormBufferGuard(BufferID.ShinobuStormPropagationImpactProfiles);
                    if (!profilesLocked)
                        return;

                    if (Resolve(in _profilesHandle, BufferID.ShinobuStormPropagationImpactProfiles, out NativeArray<StormDepthImpactProfileDTO> profiles) && profiles.Length > 0)
                        ShinobuStormPropagationNative.ElementAt(profiles, 0) = profileRow;
                }
                finally
                {
                    if (profilesLocked) ReleaseStormBufferGuard(BufferID.ShinobuStormPropagationImpactProfiles);
                }
            }
        }

        private void LoadImpactProfilesCold()
        {
#if !UNITY_EDITOR
            _impactProfilesLoaded = true;
            return;
#else
            if (_impactProfilesLoaded || _vault == null || _vault.IsCompactionFenceActive)
                return;

            string root = BuildProjectRootPathCold();
            if (string.IsNullOrEmpty(root))
            {
                _impactProfilesLoaded = true;
                return;
            }

            string path = Path.Combine(root, ImpactCsvRelativePath);
            if (!File.Exists(path))
            {
                _impactProfilesLoaded = true;
                return;
            }

            if (!EnsureImpactCsvManagedScratchCold())
            {
                return;
            }

            int byteCount = CopyFileIntoScratchCold(path, _impactCsvManagedScratch);
            if (byteCount <= 0)
            {
                _impactProfilesLoaded = true;
                return;
            }

            ReadOnlySpan<byte> csvBytes = _impactCsvManagedScratch;
            Span<StormDepthImpactProfileDTO> parsedProfiles = stackalloc StormDepthImpactProfileDTO[ShinobuStormPropagationConstants.ImpactProfileCapacity];
            if (!StormDepthImpactCsvParser.TryParse(csvBytes.Slice(0, byteCount), parsedProfiles, out int parsedCount, out _) || parsedCount <= 0)
            {
                _impactProfilesLoaded = true;
                return;
            }

            bool profilesLocked = false;
            try
            {
                profilesLocked = TryAcquireStormBufferGuard(BufferID.ShinobuStormPropagationImpactProfiles);
                if (!profilesLocked)
                    return;

                if (!Resolve(in _profilesHandle, BufferID.ShinobuStormPropagationImpactProfiles, out NativeArray<StormDepthImpactProfileDTO> profiles) || profiles.Length <= 0)
                {
                    return;
                }

                int copyCount = math.min(parsedCount, profiles.Length);
                for (int i = 0; i < copyCount; i++)
                    ShinobuStormPropagationNative.ElementAt(profiles, i) = parsedProfiles[i];

                for (int i = copyCount; i < profiles.Length; i++)
                    ShinobuStormPropagationNative.ElementAt(profiles, i) = default;
            }
            finally
            {
                if (profilesLocked) ReleaseStormBufferGuard(BufferID.ShinobuStormPropagationImpactProfiles);
            }

            bool tuningLocked = false;
            try
            {
                tuningLocked = TryAcquireStormBufferGuard(BufferID.ShinobuStormPropagationTuning);
                if (!tuningLocked)
                    return;

                if (Resolve(in _tuningHandle, BufferID.ShinobuStormPropagationTuning, out NativeArray<StormPropagationTuningDTO> tuning) && tuning.Length > 0)
                {
                    ref StormPropagationTuningDTO row = ref ShinobuStormPropagationNative.ElementAt(tuning, 0);
                    if (row.ProfileHash == 0u)
                        row.ProfileHash = ShinobuStormPropagationConstants.ProfileFallbackHash;
                }
            }
            finally
            {
                if (tuningLocked) ReleaseStormBufferGuard(BufferID.ShinobuStormPropagationTuning);
            }

            _impactProfilesLoaded = true;
#endif
        }

        private void SchedulePropagationJobs(float deltaTime, float quality)
        {
            if (_vault == null || _vault.IsCompactionFenceActive)
                return;

            if (!TrySnapshotPropagationJobInputs(out bool weatherAvailable))
            {
                MarkVaultHandlesStaleAfterResolveFailure();
                return;
            }

            _lastOriginFallbackAup = ResolveOriginFallbackAupDouble();
            WeatherStateDTO weatherRow = ShinobuStormPropagationNative.ReadElement(_jobWeatherSnapshot, 0);
            _lastSeaLevelAup = ResolveSeaLevelAupDouble(_lastOriginFallbackAup, in weatherRow, weatherAvailable);
            float time = ResolveTimeSeconds();
            JobHandle dependency = default;
            bool useMockHurricane = ShouldGenerateEmergencyMockHurricane() &&
                                    (!weatherAvailable || IsWeatherSourceInvalid(in weatherRow));

            if (useMockHurricane)
            {
                GenerateMockHurricaneJob mockJob = new GenerateMockHurricaneJob
                {
                    MockState = _jobMockWeather,
                    TimeSeconds = time,
                    GlobalQualityWeight = quality,
                    Seed = ShinobuStormPropagationConstants.SourceHash
                };
                _mockHurricaneJobHandle = mockJob.Schedule();
                dependency = _mockHurricaneJobHandle;
                _mockScheduled = true;
            }

            CalculateStormAttenuationJob attenuationJob = new CalculateStormAttenuationJob
            {
                WeatherState = _jobWeatherSnapshot,
                Tuning = _jobTuningSnapshot,
                Profiles = _jobProfileSnapshot,
                MockWeather = _jobMockWeather,
                WriteSnapshot = _jobWriteSnapshot,
                Telemetry = _jobTelemetry,
                TelemetryCursor = _jobTelemetryCursor,
                SampleAup = _lastOriginFallbackAup,
                SeaLevelAup = _lastSeaLevelAup,
                PreviousSurfaceIntensity01 = _previousSurfaceIntensity01,
                DeltaTime = deltaTime,
                TimeSeconds = time,
                GlobalQualityWeight = quality,
                Frame = _frame,
                ForceFlags = 0u,
                UseMockWeather = useMockHurricane ? 1 : 0
            };

            _jobScheduleTimestamp = Stopwatch.GetTimestamp();
            _attenuationJobHandle = attenuationJob.Schedule(dependency);
            _attenuationScheduled = true;
        }

        private void CompleteFinishedAttenuationJob()
        {
            if (!_attenuationScheduled || !DispatcherJobFence.TryFinalizeCompleted(ref _attenuationJobHandle))
                return;

            if (_mockScheduled)
            {
                DispatcherJobFence.TryFinalizeCompleted(ref _mockHurricaneJobHandle);
                _mockScheduled = false;
                TryPublishMockWeatherSnapshot();
            }

            long completeTimestamp = Stopwatch.GetTimestamp();
            _lastScheduleToPublishMicroseconds = _jobScheduleTimestamp > 0L
                ? (float)((completeTimestamp - _jobScheduleTimestamp) * 1000000.0 / Stopwatch.Frequency)
                : 0f;
            _attenuationScheduled = false;
            uint publicationFlags = PublishCompletedState();
            StampScheduleToPublishTelemetry(publicationFlags);
        }

        private bool ShouldGenerateEmergencyMockHurricane()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return autoGenerateEmergencyMockHurricane;
#else
            return false;
#endif
        }

        private void CompleteScheduledJobsForShutdown()
        {
            if (_attenuationScheduled)
            {
                DispatcherJobFence.BeginPostSimulationSwapWindow();
                try
                {
                    DispatcherJobFence.TryComplete(ref _attenuationJobHandle, forceComplete: true);
                }
                finally
                {
                    DispatcherJobFence.EndPostSimulationSwapWindow();
                }

                _attenuationScheduled = false;
            }

            if (_mockScheduled)
            {
                DispatcherJobFence.BeginPostSimulationSwapWindow();
                try
                {
                    DispatcherJobFence.TryComplete(ref _mockHurricaneJobHandle, forceComplete: true);
                }
                finally
                {
                    DispatcherJobFence.EndPostSimulationSwapWindow();
                }

                _mockScheduled = false;
            }
        }

        private uint PublishCompletedState()
        {
            uint publishedFlags = 0u;
            if (!_jobWriteSnapshot.IsCreated || _jobWriteSnapshot.Length <= 0)
                return 0u;

            StormPropagationWriteSnapshotDTO snapshot = ShinobuStormPropagationNative.ReadElement(_jobWriteSnapshot, 0);
            if (TryPublishCompletedStateRow(in snapshot.State))
            {
                if (TryPublishScalarRow(BufferID.ShinobuStormPropagationFlowScalar, in _flowScalarHandle, snapshot.FlowScalar))
                    publishedFlags |= ShinobuStormPropagationConstants.TelemetryFlagFlowPublished;
                if (TryPublishScalarRow(BufferID.ShinobuStormPropagationAudioScalar, in _audioScalarHandle, snapshot.AudioScalar))
                    publishedFlags |= ShinobuStormPropagationConstants.TelemetryFlagAudioPublished;
                if (TryPublishScalarRow(BufferID.ShinobuStormPropagationBiolumScalar, in _biolumScalarHandle, snapshot.BiolumScalar))
                    publishedFlags |= ShinobuStormPropagationConstants.TelemetryFlagBiolumPublished;
                if (TryPublishScalarRow(BufferID.ShinobuStormPropagationFogScalar, in _fogScalarHandle, snapshot.FogScalar))
                    publishedFlags |= ShinobuStormPropagationConstants.TelemetryFlagFogPublished;
            }

            return publishedFlags;
        }

        private bool TryPublishCompletedStateRow(in StormPropagationDTO state)
        {
            if (_vault == null || !TryAcquireStormBufferGuard(BufferID.ShinobuStormPropagationState))
                return false;

            try
            {
                if (!Resolve(in _publishedStateHandle, BufferID.ShinobuStormPropagationState, out NativeArray<StormPropagationDTO> readState) ||
                    readState.Length <= 0)
                {
                    return false;
                }

                StormPropagationDTO localState = state;
                void* src = UnsafeUtility.AddressOf(ref localState);
                void* dst = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(readState);
                UnsafeUtility.MemCpy(dst, src, ShinobuStormPropagationConstants.StormPropagationStrideBytes);
                return true;
            }
            finally
            {
                ReleaseStormBufferGuard(BufferID.ShinobuStormPropagationState);
            }
        }

        private bool TryPublishScalarRow(BufferID bufferId, in VaultGenerationHandle<float4> handle, in float4 value)
        {
            if (_vault == null || !TryAcquireStormBufferGuard(bufferId))
                return false;

            try
            {
                if (!Resolve(in handle, bufferId, out NativeArray<float4> row) || row.Length <= 0)
                    return false;

                ShinobuStormPropagationNative.ElementAt(row, 0) = value;
                return true;
            }
            finally
            {
                ReleaseStormBufferGuard(bufferId);
            }
        }

        private bool TrySnapshotPropagationJobInputs(out bool weatherAvailable)
        {
            weatherAvailable = false;
            if (_vault == null || _vault.IsCompactionFenceActive)
                return false;

            if (!HasJobStagingReady())
                return false;

            _jobWeatherSnapshot[0] = default;
            _jobMockWeather[0] = default;
            _jobWriteSnapshot[0] = default;
            _jobTelemetry[0] = default;
            _jobTelemetryCursor[0] = 0;

            if (IsHabitatAtmosphereHandle(in _weatherHandle, BufferID.ShinobuOceanWeatherState))
            {
                if (!TryReadSingleVaultRow(BufferID.ShinobuOceanWeatherState, in _weatherHandle, out WeatherStateDTO weatherRow))
                {
                    _weatherHandle = default;
                    return false;
                }

                _jobWeatherSnapshot[0] = weatherRow;
                weatherAvailable = true;
            }

            if (!TryReadSingleVaultRow(BufferID.ShinobuStormPropagationTuning, in _tuningHandle, out StormPropagationTuningDTO tuningRow))
                return false;
            _jobTuningSnapshot[0] = tuningRow;

            if (!TryReadSingleVaultRow(BufferID.ShinobuStormPropagationMockWeather, in _mockWeatherHandle, out MockHurricaneStateDTO mockRow))
                return false;
            _jobMockWeather[0] = mockRow;

            return TryCopyImpactProfilesToJobSnapshot();
        }

        private bool TryReadSingleVaultRow<T>(BufferID bufferId, in VaultGenerationHandle<T> handle, out T value)
            where T : unmanaged
        {
            value = default;
            if (_vault == null || !TryAcquireStormBufferGuard(bufferId))
                return false;

            try
            {
                if (!Resolve(in handle, bufferId, out NativeArray<T> source) || source.Length <= 0)
                    return false;

                value = ShinobuStormPropagationNative.ReadElement(source, 0);
                return true;
            }
            finally
            {
                ReleaseStormBufferGuard(bufferId);
            }
        }

        private bool TryWriteSingleVaultRow<T>(BufferID bufferId, in VaultGenerationHandle<T> handle, in T value)
            where T : unmanaged
        {
            if (_vault == null || !TryAcquireStormBufferGuard(bufferId))
                return false;

            try
            {
                if (!Resolve(in handle, bufferId, out NativeArray<T> destination) || destination.Length <= 0)
                    return false;

                ShinobuStormPropagationNative.ElementAt(destination, 0) = value;
                return true;
            }
            finally
            {
                ReleaseStormBufferGuard(bufferId);
            }
        }

        private bool TryCopyImpactProfilesToJobSnapshot()
        {
            if (_vault == null || !TryAcquireStormBufferGuard(BufferID.ShinobuStormPropagationImpactProfiles))
                return false;

            try
            {
                if (!Resolve(in _profilesHandle, BufferID.ShinobuStormPropagationImpactProfiles, out NativeArray<StormDepthImpactProfileDTO> profiles) ||
                    profiles.Length <= 0 ||
                    !_jobProfileSnapshot.IsCreated)
                {
                    return false;
                }

                int count = math.min(profiles.Length, _jobProfileSnapshot.Length);
                for (int i = 0; i < count; i++)
                    _jobProfileSnapshot[i] = ShinobuStormPropagationNative.ReadElement(profiles, i);
                for (int i = count; i < _jobProfileSnapshot.Length; i++)
                    _jobProfileSnapshot[i] = default;
                return true;
            }
            finally
            {
                ReleaseStormBufferGuard(BufferID.ShinobuStormPropagationImpactProfiles);
            }
        }

        private bool TryPublishMockWeatherSnapshot()
        {
            if (!_jobMockWeather.IsCreated || _jobMockWeather.Length <= 0)
                return false;

            MockHurricaneStateDTO mock = ShinobuStormPropagationNative.ReadElement(_jobMockWeather, 0);
            return TryWriteSingleVaultRow(BufferID.ShinobuStormPropagationMockWeather, in _mockWeatherHandle, in mock);
        }

        private void StampScheduleToPublishTelemetry(uint publicationFlags)
        {
            if (!_jobTelemetry.IsCreated || _jobTelemetry.Length <= 0)
                return;

            StormPropagationTelemetryEntry entry = ShinobuStormPropagationNative.ReadElement(_jobTelemetry, 0);
            if (entry.Frame == 0u)
                return;

            entry.Flags |= publicationFlags;
            entry.ScheduleToPublishMicroseconds = _lastScheduleToPublishMicroseconds;
            _previousSurfaceIntensity01 = ShinobuStormPropagationMath.Sanitize01(entry.SurfaceIntensity01);
            _lastTelemetryReasonFlags = entry.Flags;
            _lastTelemetryStateHash = entry.StateHash;
            TryPublishTelemetryEntry(in entry);
        }

        private bool TryPublishTelemetryEntry(in StormPropagationTelemetryEntry entry)
        {
            if (_vault == null || _vault.IsCompactionFenceActive)
                return false;

            if (!TryReadSingleVaultRow(BufferID.ShinobuStormPropagationTelemetryCursor, in _telemetryCursorHandle, out int cursor))
                return false;

            int writeIndex = ShinobuStormPropagationMath.WrapRingIndex(cursor, ShinobuStormPropagationConstants.TelemetryFrameCount);
            if (!TryWriteTelemetryEntryAt(writeIndex, in entry))
                return false;

            int nextCursor = ShinobuStormPropagationMath.AdvanceRingCursor(cursor, ShinobuStormPropagationConstants.TelemetryFrameCount);
            return TryWriteSingleVaultRow(BufferID.ShinobuStormPropagationTelemetryCursor, in _telemetryCursorHandle, in nextCursor);
        }

        private bool TryWriteTelemetryEntryAt(int index, in StormPropagationTelemetryEntry entry)
        {
            if (_vault == null || !TryAcquireStormBufferGuard(BufferID.ShinobuStormPropagationTelemetryRing))
                return false;

            try
            {
                if (!Resolve(in _telemetryHandle, BufferID.ShinobuStormPropagationTelemetryRing, out NativeArray<StormPropagationTelemetryEntry> telemetry) ||
                    telemetry.Length <= 0)
                {
                    return false;
                }

                int safeIndex = ShinobuStormPropagationMath.WrapRingIndex(index, telemetry.Length);
                ShinobuStormPropagationNative.ElementAt(telemetry, safeIndex) = entry;
                return true;
            }
            finally
            {
                ReleaseStormBufferGuard(BufferID.ShinobuStormPropagationTelemetryRing);
            }
        }

        private bool TryDumpTelemetryToDisk(uint reasonFlags, uint stateHash)
        {
            if (_vault == null || _vault.IsCompactionFenceActive)
            {
                return false;
            }

            byte[] scratch = _dumpManagedScratch;
            if (scratch == null ||
                scratch.Length < ShinobuStormPropagationConstants.DumpScratchBytes)
            {
                return false;
            }

            if (!TryCopyTelemetryDumpSnapshot(reasonFlags, stateHash, scratch, out int byteCount))
                return false;

            return TryWriteTelemetryDumpSnapshotCold(scratch, byteCount);
        }

        private bool TryCopyTelemetryDumpSnapshot(uint reasonFlags, uint stateHash, byte[] scratch, out int byteCount)
        {
            byteCount = 0;
            IDataVault vault = _vault;
            if (vault == null ||
                scratch == null ||
                scratch.Length < ShinobuStormPropagationConstants.DumpScratchBytes ||
                !vault.TryReadOnlyHandle(in _telemetryCursorHandle, out NativeArray<int>.ReadOnly cursor) ||
                cursor.Length <= 0 ||
                !vault.TryReadOnlyHandle(in _telemetryHandle, out NativeArray<StormPropagationTelemetryEntry>.ReadOnly telemetry) ||
                telemetry.Length <= 0)
            {
                return false;
            }

            StormPropagationDumpHeader header = default;
            header.Magic = ShinobuStormPropagationConstants.DumpMagic;
            header.ReasonFlags = reasonFlags;
            header.WriteCursor = cursor[0];
            header.EntryCount = math.min(telemetry.Length, ShinobuStormPropagationConstants.TelemetryFrameCount);
            header.EntryStrideBytes = ShinobuStormPropagationConstants.TelemetryEntryStrideBytes;
            header.SourceHash = ShinobuStormPropagationConstants.SourceHash;
            header.StateHash = stateHash;
            header.Reserved = 0u;

            if (header.EntryCount <= 0)
                return false;

            fixed (byte* scratchPtr = scratch)
            {
                void* headerPtr = UnsafeUtility.AddressOf(ref header);
                UnsafeUtility.MemCpy(scratchPtr, headerPtr, UnsafeUtility.SizeOf<StormPropagationDumpHeader>());
                byte* telemetryPtr = (byte*)telemetry.GetUnsafeReadOnlyPtr();
                int safeWriteCursor = ((header.WriteCursor % header.EntryCount) + header.EntryCount) % header.EntryCount;
                StormPropagationTelemetryEntry newestCandidate = telemetry[safeWriteCursor];
                int oldestIndex = newestCandidate.Frame != 0u ? safeWriteCursor : 0;
                byte* dumpEntryPtr = scratchPtr + UnsafeUtility.SizeOf<StormPropagationDumpHeader>();
                for (int i = 0; i < header.EntryCount; i++)
                {
                    int sourceIndex = (oldestIndex + i) % header.EntryCount;
                    UnsafeUtility.MemCpy(
                        dumpEntryPtr + (i * ShinobuStormPropagationConstants.TelemetryEntryStrideBytes),
                        telemetryPtr + (sourceIndex * ShinobuStormPropagationConstants.TelemetryEntryStrideBytes),
                        ShinobuStormPropagationConstants.TelemetryEntryStrideBytes);
                }
            }

            byteCount = UnsafeUtility.SizeOf<StormPropagationDumpHeader>() + header.EntryCount * ShinobuStormPropagationConstants.TelemetryEntryStrideBytes;
            return true;
        }

        private static bool TryWriteTelemetryDumpSnapshotCold(byte[] scratch, int byteCount)
        {
            return scratch != null &&
                   byteCount > 0 &&
                   byteCount <= scratch.Length &&
                   NativeFaultDumpWriter.TryWriteAll(
                       DumpRelativePath,
                       new ReadOnlySpan<byte>(scratch, 0, byteCount),
                       byteCount);
        }

        private float SampleGlobalQualityWeightForTick()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            if (!math.isfinite(weight))
                weight = editorPreviewQualityWeight;

            _lastKnownQualityWeight = ShinobuStormPropagationMath.Sanitize01(weight);
            return _lastKnownQualityWeight;
        }

        private float SamplePublicationCadenceHzForTick(float quality)
        {
            float defaultCadence = math.lerp(ShinobuStormPropagationConstants.MinimumPublicationCadenceHz, ShinobuStormPropagationConstants.DefaultPublicationCadenceHz, ShinobuStormPropagationMath.Smooth01(quality));
            float cachedCadence = _cachedPublicationCadenceHz;
            if (!math.isfinite(cachedCadence) || cachedCadence <= 0.001f)
                return defaultCadence;

            cachedCadence = math.max(ShinobuStormPropagationConstants.MinimumPublicationCadenceHz, cachedCadence);
            return math.lerp(ShinobuStormPropagationConstants.MinimumPublicationCadenceHz, cachedCadence, ShinobuStormPropagationMath.Smooth01(quality));
        }

        private void RefreshCachedTuningSnapshotCold()
        {
            if (_vault == null || _vault.IsCompactionFenceActive)
                return;

            if (!TryAcquireStormBufferGuard(BufferID.ShinobuStormPropagationTuning))
                return;

            try
            {
                if (Resolve(in _tuningHandle, BufferID.ShinobuStormPropagationTuning, out NativeArray<StormPropagationTuningDTO> tuning) && tuning.Length > 0)
                {
                    StormPropagationTuningDTO row = ShinobuStormPropagationNative.ReadElement(tuning, 0);
                    if (math.isfinite(row.PublicationCadenceHz) && row.PublicationCadenceHz > 0.001f)
                        _cachedPublicationCadenceHz = row.PublicationCadenceHz;
                }
            }
            finally
            {
                ReleaseStormBufferGuard(BufferID.ShinobuStormPropagationTuning);
            }
        }

        private float ResolveTimeSeconds()
        {
            double time = _frame * (double)SimulationTickDeltaSeconds;
            return (float)math.fmod(math.max(0d, time), 86400d);
        }

        private double3 ResolveOriginFallbackAupDouble()
        {
            return SanitizeAup(_cachedOriginFallbackAup);
        }

        private double3 ResolveSeaLevelAupDouble(double3 sampleAup, in WeatherStateDTO weather, bool weatherAvailable)
        {
            float seaLevelLocal = (!double.IsNaN(seaLevelAupY) && !double.IsInfinity(seaLevelAupY)) ? (float)seaLevelAupY : 0f;
            if (weatherAvailable)
            {
                if (math.isfinite(weather.SurfaceScalars.x))
                    seaLevelLocal = weather.SurfaceScalars.x;
            }

            return new double3(sampleAup.x, sampleAup.y + seaLevelLocal, sampleAup.z);
        }

        private void RefreshCachedOriginFallbackAupCold()
        {
            _cachedOriginFallbackAup = SanitizeAup(HectonFloatingOrigin.CurrentTotalOffsetDouble);
        }

        private static double3 SanitizeAup(double3 value)
        {
            return math.select(double3.zero, value, math.isfinite(value));
        }

        private static bool IsWeatherSourceInvalid(in WeatherStateDTO weather)
        {
            return !math.all(math.isfinite(weather.WindDirectionSpeedStorm)) ||
                   !math.all(math.isfinite(weather.SurfaceScalars)) ||
                   !math.all(math.isfinite(weather.SkyTintAndSurge)) ||
                   !math.isfinite(weather.GlobalQualityWeight) ||
                   !math.isfinite(weather.MaxWaveAmplitude);
        }

        private bool Resolve<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId, out NativeArray<T> buffer)
            where T : unmanaged
        {
            buffer = default;
            return _vault != null &&
                   IsHabitatAtmosphereHandle(in handle, expectedBufferId) &&
                   _vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private static string BuildProjectRootPathCold()
        {
            string dataPath = Application.dataPath;
            if (string.IsNullOrEmpty(dataPath))
                return null;

            DirectoryInfo parent = Directory.GetParent(dataPath);
            return parent != null ? parent.FullName : null;
        }

#if UNITY_EDITOR
        private bool EnsureImpactCsvManagedScratchCold()
        {
            if (_impactCsvManagedScratch == null ||
                _impactCsvManagedScratch.Length < ShinobuStormPropagationConstants.CsvScratchBytes)
            {
                _impactCsvManagedScratch = new byte[ShinobuStormPropagationConstants.CsvScratchBytes]; // COLD ALLOC: editor CSV import staging; never inside vault lock.
            }

            return _impactCsvManagedScratch.Length >= ShinobuStormPropagationConstants.CsvScratchBytes;
        }

        private static int CopyFileIntoScratchCold(string path, byte[] scratch)
        {
            try
            {
                if (scratch == null)
                    return 0;

                using (FileStream stream = File.OpenRead(path))
                {
                    if (stream.Length <= 0)
                        return 0;
                    if (stream.Length > scratch.Length)
                        return -1;

                    int length = (int)stream.Length;
                    int totalRead = 0;
                    while (totalRead < length)
                    {
                        int read = stream.Read(scratch, totalRead, length - totalRead);
                        if (read <= 0)
                            break;

                        totalRead += read;
                    }

                    return totalRead == length ? totalRead : -1;
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
        }
#endif
    }
}
