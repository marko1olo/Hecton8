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
        private const uint JobLockWeather = 1u << 0;
        private const uint JobLockTuning = 1u << 1;
        private const uint JobLockProfiles = 1u << 2;
        private const uint JobLockMockWeather = 1u << 3;
        private const uint JobLockWriteState = 1u << 4;
        private const uint JobLockTelemetry = 1u << 5;
        private const uint JobLockTelemetryCursor = 1u << 6;
        private static int s_runtimeClaimed;

        [SerializeField] private bool autoGenerateEmergencyMockHurricane;
        [SerializeField, Range(0f, 1f)] private float editorPreviewQualityWeight = 1f;
        [SerializeField] private double seaLevelAupY;

        private IDataVault _vault;
        private ITickDispatcher _tickDispatcher;
        private VaultGenerationHandle<WeatherStateDTO> _weatherHandle;
        private VaultGenerationHandle<StormPropagationDTO> _publishedStateHandle;
        private VaultGenerationHandle<StormPropagationWriteSnapshotDTO> _writeStateHandle;
        private VaultGenerationHandle<StormPropagationTuningDTO> _tuningHandle;
        private VaultGenerationHandle<StormPropagationTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<MockHurricaneStateDTO> _mockWeatherHandle;
        private VaultGenerationHandle<StormDepthImpactProfileDTO> _profilesHandle;
#if UNITY_EDITOR
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private byte[] _impactCsvManagedScratch;
#endif
        private VaultGenerationHandle<float4> _flowScalarHandle;
        private VaultGenerationHandle<float4> _audioScalarHandle;
        private VaultGenerationHandle<float4> _biolumScalarHandle;
        private VaultGenerationHandle<float4> _fogScalarHandle;
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
        private uint _jobLockMask;
        private uint _pendingFaultDumpReasonFlags;
        private uint _pendingFaultDumpStateHash;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeClaim()
        {
            Volatile.Write(ref s_runtimeClaimed, 0);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureSceneRuntime()
        {
            if (!Application.isPlaying)
                return;

            if (Volatile.Read(ref s_runtimeClaimed) != 0)
                return;

            GameObject host = new GameObject("H8_ShinobuStormPropagationRuntime"); // COLD ALLOC: GameObject[1] - scene-local storm propagation runtime root - owner: ShinobuStormPropagationRuntime
            host.hideFlags = HideFlags.DontSave;
            host.AddComponent<ShinobuStormPropagationRuntime>(); // COLD ALLOC: ShinobuStormPropagationRuntime[1] - auto-bootstrap fallback component - owner: ShinobuStormPropagationRuntime
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
            {
                EnsureVaultBuffersCold();
                return;
            }

            if (_attenuationScheduled)
                return;

            if (_weatherHandle.BufferID == 0u)
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
                    RebindDataVaultForLifecycle(currentService as IDataVault);
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
            if (_publishedStateHandle.BufferID == 0u)
                _publishedStateHandle = vault.EnsureGenerationHandle<StormPropagationDTO>(BufferID.ShinobuStormPropagationState, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            if (_writeStateHandle.BufferID == 0u)
                _writeStateHandle = vault.EnsureGenerationHandle<StormPropagationWriteSnapshotDTO>(BufferID.ShinobuStormPropagationWriteState, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            if (_tuningHandle.BufferID == 0u)
                _tuningHandle = vault.EnsureGenerationHandle<StormPropagationTuningDTO>(BufferID.ShinobuStormPropagationTuning, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            if (_telemetryHandle.BufferID == 0u)
                _telemetryHandle = vault.EnsureGenerationHandle<StormPropagationTelemetryEntry>(BufferID.ShinobuStormPropagationTelemetryRing, ShinobuStormPropagationConstants.TelemetryFrameCount, OwnerSystem, NativeArrayOptions.ClearMemory);
            if (_telemetryCursorHandle.BufferID == 0u)
                _telemetryCursorHandle = vault.EnsureGenerationHandle<int>(BufferID.ShinobuStormPropagationTelemetryCursor, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            if (_mockWeatherHandle.BufferID == 0u)
                _mockWeatherHandle = vault.EnsureGenerationHandle<MockHurricaneStateDTO>(BufferID.ShinobuStormPropagationMockWeather, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            if (_profilesHandle.BufferID == 0u)
                _profilesHandle = vault.EnsureGenerationHandle<StormDepthImpactProfileDTO>(BufferID.ShinobuStormPropagationImpactProfiles, ShinobuStormPropagationConstants.ImpactProfileCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
#if UNITY_EDITOR
            if (_csvScratchHandle.BufferID == 0u)
                _csvScratchHandle = vault.EnsureGenerationHandle<byte>(BufferID.ShinobuStormPropagationCsvScratch, ShinobuStormPropagationConstants.CsvScratchBytes, OwnerSystem, NativeArrayOptions.ClearMemory);
#endif
            EnsureDumpManagedScratchCold();
            if (_flowScalarHandle.BufferID == 0u)
                _flowScalarHandle = vault.EnsureGenerationHandle<float4>(BufferID.ShinobuStormPropagationFlowScalar, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            if (_audioScalarHandle.BufferID == 0u)
                _audioScalarHandle = vault.EnsureGenerationHandle<float4>(BufferID.ShinobuStormPropagationAudioScalar, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            if (_biolumScalarHandle.BufferID == 0u)
                _biolumScalarHandle = vault.EnsureGenerationHandle<float4>(BufferID.ShinobuStormPropagationBiolumScalar, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            if (_fogScalarHandle.BufferID == 0u)
                _fogScalarHandle = vault.EnsureGenerationHandle<float4>(BufferID.ShinobuStormPropagationFogScalar, 1, OwnerSystem, NativeArrayOptions.ClearMemory);

            _vaultReady =
                Resolve(in _publishedStateHandle, out NativeArray<StormPropagationDTO> published) && published.Length > 0 &&
                Resolve(in _writeStateHandle, out NativeArray<StormPropagationWriteSnapshotDTO> write) && write.Length > 0 &&
                Resolve(in _tuningHandle, out NativeArray<StormPropagationTuningDTO> tuning) && tuning.Length > 0 &&
                Resolve(in _telemetryHandle, out NativeArray<StormPropagationTelemetryEntry> telemetry) && telemetry.Length >= ShinobuStormPropagationConstants.TelemetryFrameCount &&
                Resolve(in _telemetryCursorHandle, out NativeArray<int> telemetryCursor) && telemetryCursor.Length > 0 &&
                Resolve(in _mockWeatherHandle, out NativeArray<MockHurricaneStateDTO> mock) && mock.Length > 0 &&
                Resolve(in _profilesHandle, out NativeArray<StormDepthImpactProfileDTO> profiles) && profiles.Length > 0 &&
                Resolve(in _flowScalarHandle, out NativeArray<float4> flowScalar) && flowScalar.Length > 0 &&
                Resolve(in _audioScalarHandle, out NativeArray<float4> audioScalar) && audioScalar.Length > 0 &&
                Resolve(in _biolumScalarHandle, out NativeArray<float4> biolumScalar) && biolumScalar.Length > 0 &&
                Resolve(in _fogScalarHandle, out NativeArray<float4> fogScalar) && fogScalar.Length > 0;

            if (_vaultReady)
                EnsureDefaultRowsCold();

            return _vaultReady;
        }

        private bool TryRefreshWeatherHandleCold()
        {
            if (_vault == null || _vault.IsCompactionFenceActive)
                return false;

            if (_weatherHandle.BufferID != 0u)
                return true;

            if (_vault.TryGetGenerationHandle(BufferID.ShinobuOceanWeatherState, out _weatherHandle))
                return true;

            _weatherHandle = default;
            return false;
        }

        private void ClearVaultHandlesCold()
        {
            _weatherHandle = default;
            _publishedStateHandle = default;
            _writeStateHandle = default;
            _tuningHandle = default;
            _telemetryHandle = default;
            _telemetryCursorHandle = default;
            _mockWeatherHandle = default;
            _profilesHandle = default;
#if UNITY_EDITOR
            _csvScratchHandle = default;
#endif
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
            ReleaseOwnedVaultHandle(vault, ref _writeStateHandle, BufferID.ShinobuStormPropagationWriteState);
            ReleaseOwnedVaultHandle(vault, ref _tuningHandle, BufferID.ShinobuStormPropagationTuning);
            ReleaseOwnedVaultHandle(vault, ref _telemetryHandle, BufferID.ShinobuStormPropagationTelemetryRing);
            ReleaseOwnedVaultHandle(vault, ref _telemetryCursorHandle, BufferID.ShinobuStormPropagationTelemetryCursor);
            ReleaseOwnedVaultHandle(vault, ref _mockWeatherHandle, BufferID.ShinobuStormPropagationMockWeather);
            ReleaseOwnedVaultHandle(vault, ref _profilesHandle, BufferID.ShinobuStormPropagationImpactProfiles);
#if UNITY_EDITOR
            ReleaseOwnedVaultHandle(vault, ref _csvScratchHandle, BufferID.ShinobuStormPropagationCsvScratch);
#endif
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
            return handle.BufferID == (uint)bufferId &&
                   handle.Generation != 0u &&
                   handle.SystemID == (uint)OwnerSystem;
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

            bool tuningLocked = false;
            bool profilesLocked = false;
            if (!_vault.TryLockBuffer(BufferID.ShinobuStormPropagationTuning, OwnerSystem))
                return;

            tuningLocked = true;
            try
            {
                if (!_vault.TryLockBuffer(BufferID.ShinobuStormPropagationImpactProfiles, OwnerSystem))
                    return;

                profilesLocked = true;
                if (Resolve(in _tuningHandle, out NativeArray<StormPropagationTuningDTO> tuning) && tuning.Length > 0)
                {
                    ref StormPropagationTuningDTO row = ref ShinobuStormPropagationNative.ElementAt(tuning, 0);
                    row = ShinobuStormPropagationNative.SanitizeTuning(row, SampleGlobalQualityWeightForTick());

                    if (math.isfinite(row.PublicationCadenceHz) && row.PublicationCadenceHz > 0.001f)
                        _cachedPublicationCadenceHz = row.PublicationCadenceHz;
                }

                if (Resolve(in _profilesHandle, out NativeArray<StormDepthImpactProfileDTO> profiles) && profiles.Length > 0)
                {
                    ref StormDepthImpactProfileDTO row = ref ShinobuStormPropagationNative.ElementAt(profiles, 0);
                    if (row.ProfileHash == 0u)
                        row = ShinobuStormPropagationNative.CreateFallbackProfile();
                }
            }
            finally
            {
                if (profilesLocked) _vault.TryUnlockBuffer(BufferID.ShinobuStormPropagationImpactProfiles, OwnerSystem);
                if (tuningLocked) _vault.TryUnlockBuffer(BufferID.ShinobuStormPropagationTuning, OwnerSystem);
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
                profilesLocked = _vault.TryLockBuffer(BufferID.ShinobuStormPropagationImpactProfiles, OwnerSystem);
                if (!profilesLocked)
                    return;

                if (!Resolve(in _profilesHandle, out NativeArray<StormDepthImpactProfileDTO> profiles) || profiles.Length <= 0)
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
                if (profilesLocked) _vault.TryUnlockBuffer(BufferID.ShinobuStormPropagationImpactProfiles, OwnerSystem);
            }

            bool tuningLocked = false;
            try
            {
                tuningLocked = _vault.TryLockBuffer(BufferID.ShinobuStormPropagationTuning, OwnerSystem);
                if (!tuningLocked)
                    return;

                if (Resolve(in _tuningHandle, out NativeArray<StormPropagationTuningDTO> tuning) && tuning.Length > 0)
                {
                    ref StormPropagationTuningDTO row = ref ShinobuStormPropagationNative.ElementAt(tuning, 0);
                    if (row.ProfileHash == 0u)
                        row.ProfileHash = ShinobuStormPropagationConstants.ProfileFallbackHash;
                }
            }
            finally
            {
                if (tuningLocked) _vault.TryUnlockBuffer(BufferID.ShinobuStormPropagationTuning, OwnerSystem);
            }

            _impactProfilesLoaded = true;
#endif
        }

        private void SchedulePropagationJobs(float deltaTime, float quality)
        {
            if (_vault == null || _vault.IsCompactionFenceActive)
                return;

            if (!TryLockOwnedJobBuffers())
            {
                MarkVaultHandlesStaleAfterResolveFailure();
                return;
            }

            NativeArray<WeatherStateDTO> weather = default;
            bool weatherAvailable = Resolve(in _weatherHandle, out weather) && weather.Length > 0;
            if (_weatherHandle.BufferID != 0u && !weatherAvailable)
                _weatherHandle = default;

            if (!Resolve(in _tuningHandle, out NativeArray<StormPropagationTuningDTO> tuning) ||
                !Resolve(in _profilesHandle, out NativeArray<StormDepthImpactProfileDTO> profiles) ||
                !Resolve(in _mockWeatherHandle, out NativeArray<MockHurricaneStateDTO> mockWeather) ||
                !Resolve(in _writeStateHandle, out NativeArray<StormPropagationWriteSnapshotDTO> writeSnapshot) ||
                !Resolve(in _telemetryHandle, out NativeArray<StormPropagationTelemetryEntry> telemetry) ||
                !Resolve(in _telemetryCursorHandle, out NativeArray<int> telemetryCursor))
            {
                UnlockOwnedJobBuffers();
                MarkVaultHandlesStaleAfterResolveFailure();
                return;
            }

            _lastOriginFallbackAup = ResolveOriginFallbackAupDouble();
            _lastSeaLevelAup = ResolveSeaLevelAupDouble(_lastOriginFallbackAup);
            float time = ResolveTimeSeconds();
            JobHandle dependency = default;
            bool useMockHurricane = autoGenerateEmergencyMockHurricane &&
                                    (!weatherAvailable || IsWeatherSourceInvalid(ShinobuStormPropagationNative.ReadElement(weather, 0)));

            if (useMockHurricane)
            {
                GenerateMockHurricaneJob mockJob = new GenerateMockHurricaneJob
                {
                    MockState = mockWeather,
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
                WeatherState = weather,
                Tuning = tuning,
                Profiles = profiles,
                MockWeather = mockWeather,
                WriteSnapshot = writeSnapshot,
                Telemetry = telemetry,
                TelemetryCursor = telemetryCursor,
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
            }

            long completeTimestamp = Stopwatch.GetTimestamp();
            _lastScheduleToPublishMicroseconds = _jobScheduleTimestamp > 0L
                ? (float)((completeTimestamp - _jobScheduleTimestamp) * 1000000.0 / Stopwatch.Frequency)
                : 0f;
            _attenuationScheduled = false;
            try
            {
                PublishCompletedState();
            }
            finally
            {
                UnlockOwnedJobBuffers();
            }
        }

        private void CompleteScheduledJobsForShutdown()
        {
            if (_attenuationScheduled)
            {
                DispatcherJobFence.TryComplete(ref _attenuationJobHandle, forceComplete: true);
                _attenuationScheduled = false;
            }

            if (_mockScheduled)
            {
                DispatcherJobFence.TryComplete(ref _mockHurricaneJobHandle, forceComplete: true);
                _mockScheduled = false;
            }

            UnlockOwnedJobBuffers();
        }

        private void PublishCompletedState()
        {
            uint publishedFlags = 0u;
            try
            {
                if (_vault == null ||
                    _vault.IsCompactionFenceActive ||
                    !Resolve(in _writeStateHandle, out NativeArray<StormPropagationWriteSnapshotDTO> writeSnapshot) ||
                    writeSnapshot.Length <= 0)
                {
                    return;
                }

                StormPropagationWriteSnapshotDTO snapshot = ShinobuStormPropagationNative.ReadElement(writeSnapshot, 0);
                if (!TryPublishCompletedStateRow(in snapshot.State))
                    return;

                if (TryPublishScalarRow(BufferID.ShinobuStormPropagationFlowScalar, in _flowScalarHandle, snapshot.FlowScalar))
                    publishedFlags |= ShinobuStormPropagationConstants.TelemetryFlagFlowPublished;
                if (TryPublishScalarRow(BufferID.ShinobuStormPropagationAudioScalar, in _audioScalarHandle, snapshot.AudioScalar))
                    publishedFlags |= ShinobuStormPropagationConstants.TelemetryFlagAudioPublished;
                if (TryPublishScalarRow(BufferID.ShinobuStormPropagationBiolumScalar, in _biolumScalarHandle, snapshot.BiolumScalar))
                    publishedFlags |= ShinobuStormPropagationConstants.TelemetryFlagBiolumPublished;
                if (TryPublishScalarRow(BufferID.ShinobuStormPropagationFogScalar, in _fogScalarHandle, snapshot.FogScalar))
                    publishedFlags |= ShinobuStormPropagationConstants.TelemetryFlagFogPublished;
            }
            finally
            {
                StampScheduleToPublishTelemetry(publishedFlags);
            }
        }

        private bool TryPublishCompletedStateRow(in StormPropagationDTO state)
        {
            if (_vault == null || !_vault.TryLockBuffer(BufferID.ShinobuStormPropagationState, OwnerSystem))
                return false;

            try
            {
                if (!Resolve(in _publishedStateHandle, out NativeArray<StormPropagationDTO> readState) ||
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
                _vault.TryUnlockBuffer(BufferID.ShinobuStormPropagationState, OwnerSystem);
            }
        }

        private bool TryPublishScalarRow(BufferID bufferId, in VaultGenerationHandle<float4> handle, in float4 value)
        {
            if (_vault == null || !_vault.TryLockBuffer(bufferId, OwnerSystem))
                return false;

            try
            {
                if (!Resolve(in handle, out NativeArray<float4> row) || row.Length <= 0)
                    return false;

                ShinobuStormPropagationNative.ElementAt(row, 0) = value;
                return true;
            }
            finally
            {
                _vault.TryUnlockBuffer(bufferId, OwnerSystem);
            }
        }

        private bool TryLockOwnedJobBuffers()
        {
            if (_vault == null)
                return false;

            _jobLockMask = 0u;
            return TryLockOptionalWeatherJobBuffer() &&
                   TryLockJobBuffer(BufferID.ShinobuStormPropagationTuning, JobLockTuning) &&
                   TryLockJobBuffer(BufferID.ShinobuStormPropagationImpactProfiles, JobLockProfiles) &&
                   TryLockJobBuffer(BufferID.ShinobuStormPropagationMockWeather, JobLockMockWeather) &&
                   TryLockJobBuffer(BufferID.ShinobuStormPropagationWriteState, JobLockWriteState) &&
                   TryLockJobBuffer(BufferID.ShinobuStormPropagationTelemetryRing, JobLockTelemetry) &&
                   TryLockJobBuffer(BufferID.ShinobuStormPropagationTelemetryCursor, JobLockTelemetryCursor);
        }

        private bool TryLockOptionalWeatherJobBuffer()
        {
            if (_weatherHandle.BufferID == 0u)
                return true;

            return TryLockJobBuffer(BufferID.ShinobuOceanWeatherState, JobLockWeather);
        }

        private bool TryLockJobBuffer(BufferID bufferId, uint bit)
        {
            if (_vault != null && _vault.TryLockBuffer(bufferId, OwnerSystem))
            {
                _jobLockMask |= bit;
                return true;
            }

            UnlockOwnedJobBuffers();
            return false;
        }

        private void UnlockOwnedJobBuffers()
        {
            if (_vault == null)
            {
                _jobLockMask = 0u;
                return;
            }

            uint mask = _jobLockMask;
            if ((mask & JobLockTelemetryCursor) != 0u) _vault.TryUnlockBuffer(BufferID.ShinobuStormPropagationTelemetryCursor, OwnerSystem);
            if ((mask & JobLockTelemetry) != 0u) _vault.TryUnlockBuffer(BufferID.ShinobuStormPropagationTelemetryRing, OwnerSystem);
            if ((mask & JobLockWriteState) != 0u) _vault.TryUnlockBuffer(BufferID.ShinobuStormPropagationWriteState, OwnerSystem);
            if ((mask & JobLockMockWeather) != 0u) _vault.TryUnlockBuffer(BufferID.ShinobuStormPropagationMockWeather, OwnerSystem);
            if ((mask & JobLockProfiles) != 0u) _vault.TryUnlockBuffer(BufferID.ShinobuStormPropagationImpactProfiles, OwnerSystem);
            if ((mask & JobLockTuning) != 0u) _vault.TryUnlockBuffer(BufferID.ShinobuStormPropagationTuning, OwnerSystem);
            if ((mask & JobLockWeather) != 0u) _vault.TryUnlockBuffer(BufferID.ShinobuOceanWeatherState, OwnerSystem);
            _jobLockMask = 0u;
        }

        private void StampScheduleToPublishTelemetry(uint publicationFlags)
        {
            if (_vault == null || _vault.IsCompactionFenceActive)
                return;

            if (!Resolve(in _telemetryHandle, out NativeArray<StormPropagationTelemetryEntry> telemetry) ||
                !Resolve(in _telemetryCursorHandle, out NativeArray<int> cursorArray) ||
                telemetry.Length <= 0 ||
                cursorArray.Length <= 0)
            {
                return;
            }

            int index = ShinobuStormPropagationMath.PreviousRingIndex(ShinobuStormPropagationNative.ReadElement(cursorArray, 0), telemetry.Length);
            ref StormPropagationTelemetryEntry entry = ref ShinobuStormPropagationNative.ElementAt(telemetry, index);
            entry.Flags |= publicationFlags;
            entry.ScheduleToPublishMicroseconds = _lastScheduleToPublishMicroseconds;
            _previousSurfaceIntensity01 = ShinobuStormPropagationMath.Sanitize01(entry.SurfaceIntensity01);
            _lastTelemetryReasonFlags = entry.Flags;
            _lastTelemetryStateHash = entry.StateHash;
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
            bool cursorLocked = false;
            try
            {
                if (!_vault.TryLockBuffer(BufferID.ShinobuStormPropagationTelemetryCursor, OwnerSystem))
                    return false;

                cursorLocked = true;
                if (!Resolve(in _telemetryCursorHandle, out NativeArray<int> cursor) ||
                    cursor.Length <= 0 ||
                    scratch.Length < ShinobuStormPropagationConstants.DumpScratchBytes)
                {
                    return false;
                }

                int writeCursor = ShinobuStormPropagationNative.ReadElement(cursor, 0);
                cursorLocked = false;
                _vault.TryUnlockBuffer(BufferID.ShinobuStormPropagationTelemetryCursor, OwnerSystem);

                bool telemetryLocked = false;
                try
                {
                    if (!_vault.TryLockBuffer(BufferID.ShinobuStormPropagationTelemetryRing, OwnerSystem))
                        return false;

                    telemetryLocked = true;
                    if (!Resolve(in _telemetryHandle, out NativeArray<StormPropagationTelemetryEntry> telemetry) ||
                        telemetry.Length <= 0 ||
                        scratch.Length < ShinobuStormPropagationConstants.DumpScratchBytes)
                    {
                        return false;
                    }

                    StormPropagationDumpHeader header = default;
                    header.Magic = ShinobuStormPropagationConstants.DumpMagic;
                    header.ReasonFlags = reasonFlags;
                    header.WriteCursor = writeCursor;
                    header.EntryCount = math.min(telemetry.Length, ShinobuStormPropagationConstants.TelemetryFrameCount);
                    header.EntryStrideBytes = ShinobuStormPropagationConstants.TelemetryEntryStrideBytes;
                    header.SourceHash = ShinobuStormPropagationConstants.SourceHash;
                    header.StateHash = stateHash;
                    header.Reserved = 0u;

                    fixed (byte* scratchPtr = scratch)
                    {
                        void* headerPtr = UnsafeUtility.AddressOf(ref header);
                        UnsafeUtility.MemCpy(scratchPtr, headerPtr, UnsafeUtility.SizeOf<StormPropagationDumpHeader>());
                        void* telemetryPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                        int safeWriteCursor = ((header.WriteCursor % header.EntryCount) + header.EntryCount) % header.EntryCount;
                        StormPropagationTelemetryEntry newestCandidate = ShinobuStormPropagationNative.ReadElement(telemetry, safeWriteCursor);
                        int oldestIndex = newestCandidate.Frame != 0u ? safeWriteCursor : 0;
                        byte* dumpEntryPtr = scratchPtr + UnsafeUtility.SizeOf<StormPropagationDumpHeader>();
                        for (int i = 0; i < header.EntryCount; i++)
                        {
                            int sourceIndex = (oldestIndex + i) % header.EntryCount;
                            UnsafeUtility.MemCpy(
                                dumpEntryPtr + (i * ShinobuStormPropagationConstants.TelemetryEntryStrideBytes),
                                (byte*)telemetryPtr + (sourceIndex * ShinobuStormPropagationConstants.TelemetryEntryStrideBytes),
                                ShinobuStormPropagationConstants.TelemetryEntryStrideBytes);
                        }
                    }

                    byteCount = UnsafeUtility.SizeOf<StormPropagationDumpHeader>() + header.EntryCount * ShinobuStormPropagationConstants.TelemetryEntryStrideBytes;
                    return true;
                }
                finally
                {
                    if (telemetryLocked)
                        _vault.TryUnlockBuffer(BufferID.ShinobuStormPropagationTelemetryRing, OwnerSystem);
                }
            }
            finally
            {
                if (cursorLocked) _vault.TryUnlockBuffer(BufferID.ShinobuStormPropagationTelemetryCursor, OwnerSystem);
            }
        }

        private static bool TryWriteTelemetryDumpSnapshotCold(byte[] scratch, int byteCount)
        {
            try
            {
                string root = BuildProjectRootPathCold();
                if (string.IsNullOrEmpty(root))
                    return false;

                string path = Path.Combine(root, DumpRelativePath);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                string tempPath = path + ".tmp";
                string backupPath = path + ".bak";
                using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    stream.Write(scratch, 0, byteCount);
                }

                if (new FileInfo(tempPath).Length != byteCount)
                {
                    File.Delete(tempPath);
                    return false;
                }

                if (File.Exists(path))
                    File.Replace(tempPath, path, backupPath, true);
                else
                    File.Move(tempPath, path);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            return true;
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

            if (!_vault.TryLockBuffer(BufferID.ShinobuStormPropagationTuning, OwnerSystem))
                return;

            try
            {
                if (Resolve(in _tuningHandle, out NativeArray<StormPropagationTuningDTO> tuning) && tuning.Length > 0)
                {
                    StormPropagationTuningDTO row = ShinobuStormPropagationNative.ReadElement(tuning, 0);
                    if (math.isfinite(row.PublicationCadenceHz) && row.PublicationCadenceHz > 0.001f)
                        _cachedPublicationCadenceHz = row.PublicationCadenceHz;
                }
            }
            finally
            {
                _vault.TryUnlockBuffer(BufferID.ShinobuStormPropagationTuning, OwnerSystem);
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

        private double3 ResolveSeaLevelAupDouble(double3 sampleAup)
        {
            float seaLevelLocal = (!double.IsNaN(seaLevelAupY) && !double.IsInfinity(seaLevelAupY)) ? (float)seaLevelAupY : 0f;
            if (Resolve(in _weatherHandle, out NativeArray<WeatherStateDTO> weather) &&
                weather.Length > 0)
            {
                WeatherStateDTO row = ShinobuStormPropagationNative.ReadElement(weather, 0);
                if (math.isfinite(row.SurfaceScalars.x))
                    seaLevelLocal = row.SurfaceScalars.x;
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

        private bool Resolve<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer)
            where T : unmanaged
        {
            buffer = default;
            return _vault != null && handle.BufferID != 0u && _vault.TryResolveHandle(in handle, out buffer) && buffer.IsCreated;
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
