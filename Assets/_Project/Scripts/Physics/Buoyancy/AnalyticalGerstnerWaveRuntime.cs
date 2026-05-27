using System;
#if UNITY_EDITOR
using System.IO;
#endif
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Physics
{
    [DisallowMultipleComponent]
    public unsafe sealed class AnalyticalGerstnerWaveRuntime : MonoBehaviour, IFixedTickable, IPostFixedTickable, IGlobalRegistryHotSwapListener, IOriginShiftListener
    {
        private const int LockSpectrum = 1 << 0;
        private const int LockRequests = 1 << 1;
        private const int LockResults = 1 << 2;
        private const int LockMacroGrid = 1 << 3;
        private const int LockCounters = 1 << 4;
        private const int LockTuning = 1 << 5;
        private const int LockTelemetryRing = 1 << 6;
        private const int LockTelemetryCursor = 1 << 7;
        private const uint GerstnerFaultEventHash = 0x47464654u; // GFFT
        private const uint GerstnerFaultDumpHash = 0x47464450u; // GFDP

        [Header("Vault Capacity")]
        [SerializeField, Range(1, AnalyticalGerstnerWaveConstants.SampleCapacity)]
        [Tooltip("Maximum analytical buoyancy sample requests processed by the SHINOBU_263 CPU wave solver.")]
        private int _sampleCapacity = AnalyticalGerstnerWaveConstants.SampleCapacity;

        [SerializeField, Range(2, AnalyticalGerstnerWaveConstants.MacroGridMaxResolution)]
        [Tooltip("Macro swell grid resolution used by coarse low-priority sample lanes.")]
        private int _macroGridResolution = 32;

        [Header("Cold Boot")]
        [Tooltip("Seeds a deterministic mock wave spectrum when no authored CSV profile is loaded.")]
        [SerializeField] private bool _seedMockSpectrumOnEnable = true;
        [Tooltip("Seeds deterministic mock requests only when no current-frame external producer owns the request buffer.")]
        [SerializeField] private bool _seedMockRequestsWhenEmpty = true;
#if UNITY_EDITOR
        [Tooltip("Loads the cold wave-spectrum CSV bridge into DataVault profile rows during boot.")]
        [SerializeField] private bool _loadCsvOnEnable = true;
        [Tooltip("Project-root relative CSV path for authored ocean wave spectrum profiles.")]
        [SerializeField] private string _csvRelativePath = AnalyticalGerstnerWaveConstants.CsvRelativePath;
#endif

        private IDataVault _dataVault;
        private VaultGenerationHandle<GerstnerWaveParamsDTO> _spectrumHandle;
        private VaultGenerationHandle<GerstnerWaveTuningDTO> _tuningHandle;
        private VaultGenerationHandle<OceanSampleRequestDTO> _requestsHandle;
        private VaultGenerationHandle<OceanSampleResultDTO> _resultsHandle;
        private VaultGenerationHandle<float> _macroGridHandle;
        private VaultGenerationHandle<WaveMathTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
#if UNITY_EDITOR
        private VaultGenerationHandle<byte> _csvScratchHandle;
#endif
        private VaultGenerationHandle<WaveSpectrumProfileDTO> _profilesHandle;
        private VaultGenerationHandle<WaveMathCounterLane> _countersHandle;

        private JobHandle _pendingHandle;
        private long _scheduleTimestamp;
        private uint _simulationFrame;
        private int _lockedBuffers;
        private int _scheduledSampleCount;
        private bool _jobScheduled;
        private bool _registeredFixed;
        private bool _registeredPostFixed;
        private bool _registeredHotSwap;
        private bool _coldBootCompleted;
        private bool _dumpedFault;
        private bool _coreBlackboxWarmed;
        private bool _mockRequestsSeeded;
        private bool _runtimeActive;
        private bool _registeredOriginShiftListener;
        private double3 _cachedOriginAUP;
        private uint _cachedOriginShiftSequence;
        private uint _cachedOriginShiftFlags;

#if UNITY_EDITOR
        private static AnalyticalGerstnerWaveRuntime _activeRuntimeInstance;

        public static bool TryGetActiveRuntimeInstance(out AnalyticalGerstnerWaveRuntime runtime)
        {
            runtime = _activeRuntimeInstance;
            return runtime != null;
        }

        public bool TryOpenEditorViews(
            out NativeArray<GerstnerWaveTuningDTO>.ReadOnly tuning,
            out NativeArray<WaveMathTelemetryEntry>.ReadOnly telemetry,
            out NativeArray<int>.ReadOnly cursor,
            out NativeArray<OceanSampleRequestDTO>.ReadOnly requests,
            out NativeArray<OceanSampleResultDTO>.ReadOnly results)
        {
            tuning = default;
            telemetry = default;
            cursor = default;
            requests = default;
            results = default;
            IDataVault vault = _dataVault;
            if (vault == null || !HandlesReady(vault))
                return false;

            NativeArray<GerstnerWaveTuningDTO> mutableTuning = ResolveVaultBuffer(vault, in _tuningHandle);
            NativeArray<WaveMathTelemetryEntry> mutableTelemetry = ResolveVaultBuffer(vault, in _telemetryHandle);
            NativeArray<int> mutableCursor = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
            NativeArray<OceanSampleRequestDTO> mutableRequests = ResolveVaultBuffer(vault, in _requestsHandle);
            NativeArray<OceanSampleResultDTO> mutableResults = ResolveVaultBuffer(vault, in _resultsHandle);
            if (!mutableTuning.IsCreated ||
                mutableTuning.Length <= 0 ||
                !mutableTelemetry.IsCreated ||
                mutableTelemetry.Length <= 0 ||
                !mutableCursor.IsCreated ||
                mutableCursor.Length <= 0 ||
                !mutableRequests.IsCreated ||
                !mutableResults.IsCreated)
            {
                return false;
            }

            tuning = mutableTuning.AsReadOnly();
            telemetry = mutableTelemetry.AsReadOnly();
            cursor = mutableCursor.AsReadOnly();
            requests = mutableRequests.AsReadOnly();
            results = mutableResults.AsReadOnly();
            return true;
        }
#endif

        private void Awake()
        {
            if (!Application.isPlaying)
                return;

#if UNITY_EDITOR
            _activeRuntimeInstance = this;
#endif
            _runtimeActive = true;
            RefreshCachedOriginSnapshot();
            RefreshColdDependencies();
            EnsureColdBooted();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

#if UNITY_EDITOR
            _activeRuntimeInstance = this;
#endif
            _runtimeActive = true;
            RefreshCachedOriginSnapshot();
            RefreshColdDependencies();
            EnsureColdBooted();
            WarmCoreBlackboxRoute();
            TryRegisterOriginShiftListener();
            TryRegister();
        }

        private void OnDisable()
        {
            _runtimeActive = false;
            _coreBlackboxWarmed = false;
            TryUnregisterOriginShiftListener();
            TryUnregister();
            CompletePendingForTeardown();
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            if (ReferenceEquals(_activeRuntimeInstance, this))
                _activeRuntimeInstance = null;
#endif
            _runtimeActive = false;
            _coreBlackboxWarmed = false;
            TryUnregisterOriginShiftListener();
            TryUnregister();
            CompletePendingForTeardown();
            ReleaseVaultHandles(_dataVault);
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (!_runtimeActive || _jobScheduled || !math.isfinite(fixedDeltaTime) || fixedDeltaTime <= 0f)
                return;

            if (!TryPrepareRuntimeVault(out IDataVault vault))
                return;

            if (!TryResolveRuntimeBuffers(
                    vault,
                    out NativeArray<GerstnerWaveParamsDTO> spectrum,
                    out NativeArray<GerstnerWaveTuningDTO> tuning,
                    out NativeArray<OceanSampleRequestDTO> requests,
                    out NativeArray<OceanSampleResultDTO> results,
                    out NativeArray<float> macroGrid,
                    out NativeArray<WaveMathCounterLane> counters))
            {
                return;
            }

            if (!TryLockJobBuffers(vault))
                return;

            GerstnerWaveTuningDTO tuningDto = PrepareTuning(tuning[0], requests.Length, fixedDeltaTime);
            tuning[0] = tuningDto;
            int sampleCount = math.clamp(tuningDto.ActiveRequestCount, 0, math.min(requests.Length, results.Length));
            if (sampleCount <= 0)
            {
                UnlockJobBuffers();
                return;
            }

            ClearCounterLanes(counters);
            JobHandle handle = default;

            if (_seedMockRequestsWhenEmpty && ConsumeMockRequestSeedGate(requests, sampleCount, _simulationFrame))
            {
                GenerateMockWaveRequestsJob requestJob = new GenerateMockWaveRequestsJob
                {
                    Requests = requests,
                    Count = sampleCount,
                    OriginAUP = tuningDto.LocalOriginAUP,
                    FrameIndex = _simulationFrame,
                    OriginShiftSequence = tuningDto.OriginShiftSequence
                };
                handle = requestJob.Schedule(sampleCount, 128, handle);
            }

            int gridResolution = math.clamp(tuningDto.MacroGridResolution, 2, AnalyticalGerstnerWaveConstants.MacroGridMaxResolution);
            int gridCells = math.min(gridResolution * gridResolution, macroGrid.Length);
            if (gridCells > 0)
            {
                BuildMacroSwellGridJob gridJob = new BuildMacroSwellGridJob
                {
                    Spectrum = spectrum,
                    MacroGrid = macroGrid,
                    Tuning = tuningDto
                };
                handle = gridJob.Schedule(gridCells, 64, handle);
            }

            EvaluateAnalyticalWavesJob evaluateJob = new EvaluateAnalyticalWavesJob
            {
                Requests = requests,
                Spectrum = spectrum,
                MacroGrid = macroGrid,
                Results = results,
                Counters = counters,
                Tuning = tuningDto,
                SampleCount = sampleCount
            };

            int groupCount = (sampleCount + 3) >> 2;
            _scheduleTimestamp = Stopwatch.GetTimestamp();
            _scheduledSampleCount = sampleCount;
            _pendingHandle = evaluateJob.Schedule(groupCount, 32, handle);
            H8Memory.RegisterActiveJob(SystemID.Physics, _pendingHandle);
            _jobScheduled = true;
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            if (!_jobScheduled)
                return;

            DispatcherJobFence.BeginPostFixedSwapWindow();
            bool finalized = DispatcherJobFence.TryFinalizeCompleted(ref _pendingHandle);
            DispatcherJobFence.EndPostFixedSwapWindow();
            if (!finalized)
                return;

            float elapsedMicros = ResolveElapsedMicros(_scheduleTimestamp);
            IDataVault vault = _dataVault;
            if (vault != null &&
                TryLockTelemetryBuffers(vault) &&
                TryResolveRuntimeBuffers(
                    vault,
                    out _,
                    out NativeArray<GerstnerWaveTuningDTO> tuning,
                    out _,
                    out NativeArray<OceanSampleResultDTO> results,
                    out _,
                    out NativeArray<WaveMathCounterLane> counters) &&
                ResolveTelemetryBuffers(
                    vault,
                    out NativeArray<WaveMathTelemetryEntry> telemetry,
                    out NativeArray<int> telemetryCursor))
            {
                GerstnerWaveTuningDTO tuningDto = tuning[0];
                var telemetryJob = new RecordWaveMathTelemetryJob
                {
                    Results = results,
                    Counters = counters,
                    TelemetryRing = telemetry,
                    TelemetryCursor = telemetryCursor,
                    Tuning = tuningDto,
                    SampleCount = _scheduledSampleCount,
                    BurstMicros = elapsedMicros
                };
                telemetryJob.Execute();

                float dumpThreshold = math.max(1f, math.select(AnalyticalGerstnerWaveConstants.DefaultDumpThresholdMicros, tuningDto.MaxSolverMicrosBeforeDump, math.isfinite(tuningDto.MaxSolverMicrosBeforeDump)));
                int nonFinite = counters.IsCreated && counters.Length > 2 ? counters[2].Value : 0;
                if (!_dumpedFault && (elapsedMicros > dumpThreshold || nonFinite > 0))
                {
                    DumpBlackBoxOnce(telemetry, telemetryCursor);
                    _dumpedFault = true;
                }
            }

            UnlockJobBuffers();
            _jobScheduled = false;
            _scheduledSampleCount = 0;
            _simulationFrame++;
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.FloatingOriginRuntime)
            {
                RefreshCachedOriginSnapshot();
                if (_runtimeActive)
                    RefreshOriginShiftListenerRegistration();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            CompletePendingForTeardown();
            ReleaseVaultHandles(previousService as IDataVault);
            _dataVault = currentService as IDataVault;
            _coldBootCompleted = false;
            _mockRequestsSeeded = false;
            _coreBlackboxWarmed = false;
            EnsureColdBooted();
            WarmCoreBlackboxRoute();
        }

        private GerstnerWaveTuningDTO PrepareTuning(GerstnerWaveTuningDTO tuning, int requestCapacity, float fixedDeltaTime)
        {
            if (tuning.Flags == 0u)
                tuning = GerstnerWaveTuningDTO.Default();

            float quality = ResolveGlobalQualityWeight();
            tuning.GlobalQualityWeight = quality;
            tuning.ActiveRequestCount = math.clamp(tuning.ActiveRequestCount <= 0 ? math.min(_sampleCapacity, requestCapacity) : tuning.ActiveRequestCount, 0, requestCapacity);
            tuning.MaxOctaveLimit = math.clamp(tuning.MaxOctaveLimit <= 0 ? AnalyticalGerstnerWaveConstants.MaxOctaves : tuning.MaxOctaveLimit, 1, AnalyticalGerstnerWaveConstants.MaxOctaves);
            tuning.TotalOctaves = math.clamp(tuning.TotalOctaves <= 0 ? AnalyticalGerstnerWaveConstants.MaxOctaves : tuning.TotalOctaves, 1, AnalyticalGerstnerWaveConstants.MaxOctaves);
            tuning.ActiveOctaves = AnalyticalGerstnerWaveMath.ResolveActiveOctaves(in tuning);
            tuning.MacroGridResolution = math.clamp(tuning.MacroGridResolution <= 0 ? _macroGridResolution : tuning.MacroGridResolution, 2, AnalyticalGerstnerWaveConstants.MacroGridMaxResolution);
            tuning.MacroGridCellSizeMeters = math.max(0.25f, math.select(AnalyticalGerstnerWaveConstants.DefaultMacroGridCellSizeMeters, tuning.MacroGridCellSizeMeters, math.isfinite(tuning.MacroGridCellSizeMeters)));
            tuning.LargestWavelengthMeters = math.max(1f, math.select(AnalyticalGerstnerWaveConstants.DefaultLargestWavelengthMeters, tuning.LargestWavelengthMeters, math.isfinite(tuning.LargestWavelengthMeters)));
            tuning.WaveAmplitudeMultiplier = math.max(0f, math.select(AnalyticalGerstnerWaveConstants.DefaultAmplitudeMultiplier, tuning.WaveAmplitudeMultiplier, math.isfinite(tuning.WaveAmplitudeMultiplier)));
            tuning.CoarsePriorityThreshold = math.clamp(math.select(64f, tuning.CoarsePriorityThreshold, math.isfinite(tuning.CoarsePriorityThreshold)), 0f, 255f);
            tuning.MaxSolverMicrosBeforeDump = math.max(1f, math.select(AnalyticalGerstnerWaveConstants.DefaultDumpThresholdMicros, tuning.MaxSolverMicrosBeforeDump, math.isfinite(tuning.MaxSolverMicrosBeforeDump)));
            float safeDelta = math.max(0f, math.select(0.02f, fixedDeltaTime, math.isfinite(fixedDeltaTime) & fixedDeltaTime > 0f));
            double phaseTime = AnalyticalGerstnerWaveMath.ResolvePhaseTimeSeconds(in tuning);
            phaseTime += safeDelta;
            tuning.PhaseTimeSeconds = math.select(0d, phaseTime, math.isfinite(phaseTime) && phaseTime >= 0d);
            tuning.TimeSeconds = (float)math.min(tuning.PhaseTimeSeconds, (double)float.MaxValue);
            tuning.FrameIndex = _simulationFrame;
            tuning.LocalOriginAUP = ResolveCachedOriginAUP();
            tuning.OriginShiftSequence = _cachedOriginShiftSequence;
            tuning.OriginShiftFlags = _cachedOriginShiftFlags;
            tuning.Flags |= AnalyticalGerstnerWaveConstants.FlagActive | AnalyticalGerstnerWaveConstants.FlagDearLie;
            return tuning;
        }

        private void EnsureColdBooted()
        {
            if (_coldBootCompleted)
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            if (!AnalyticalGerstnerWaveLayout.Validate())
                return;

            if (!EnsureVaultHandles(vault))
                return;

            NativeArray<GerstnerWaveTuningDTO> tuning = ResolveVaultBuffer(vault, in _tuningHandle);
            NativeArray<GerstnerWaveParamsDTO> spectrum = ResolveVaultBuffer(vault, in _spectrumHandle);
            NativeArray<WaveSpectrumProfileDTO> profiles = ResolveVaultBuffer(vault, in _profilesHandle);
#if UNITY_EDITOR
            NativeArray<byte> csvScratch = ResolveVaultBuffer(vault, in _csvScratchHandle);
#endif
            NativeArray<int> telemetryCursor = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
            NativeArray<WaveMathCounterLane> counters = ResolveVaultBuffer(vault, in _countersHandle);
            if (!tuning.IsCreated || tuning.Length <= 0 || !spectrum.IsCreated || spectrum.Length <= 0)
                return;

            if (tuning[0].Flags == 0u)
                tuning[0] = GerstnerWaveTuningDTO.Default();

            if (telemetryCursor.IsCreated && telemetryCursor.Length > 0)
                telemetryCursor[0] = 0;
            if (counters.IsCreated)
            {
                for (int i = 0; i < counters.Length; i++)
                    counters[i] = default;
            }

            int profileRows = 0;
#if UNITY_EDITOR
            if (_loadCsvOnEnable && profiles.IsCreated && csvScratch.IsCreated)
            {
                string csvPath = ResolveProjectPath(_csvRelativePath);
                int csvBytes = ReadFileIntoNativeScratch(csvPath, csvScratch);
                if (csvBytes > 0)
                    WaveSpectrumProfileCsvParser.TryApply(new ReadOnlySpan<byte>(csvScratch.GetUnsafeReadOnlyPtr(), csvBytes), profiles, out profileRows);
            }
#endif

            if (profileRows > 0)
                ApplyProfile(profiles[0], spectrum, tuning);
            else if (_seedMockSpectrumOnEnable)
            {
                var mockSpectrumJob = new GenerateMockWaveSpectrumJob
                {
                    Spectrum = spectrum,
                    Tuning = tuning,
                    WindDirectionRadians = tuning[0].WindDirectionRadians,
                    WindSpeedMetersPerSecond = tuning[0].WindSpeedMetersPerSecond,
                    GlobalQualityWeight = tuning[0].GlobalQualityWeight,
                    FrameIndex = _simulationFrame
                };
                for (int i = 0; i < spectrum.Length; i++)
                    mockSpectrumJob.Execute(i);
            }

            _coldBootCompleted = true;
        }

        private bool ConsumeMockRequestSeedGate(NativeArray<OceanSampleRequestDTO> requests, int sampleCount, uint frameIndex)
        {
            if (!requests.IsCreated || sampleCount <= 0)
                return false;

            OceanSampleRequestDTO first = requests[0];
            uint flags = first.Flags;
            bool active = (flags & AnalyticalGerstnerWaveConstants.FlagActive) != 0u;
            bool mock = (flags & AnalyticalGerstnerWaveConstants.FlagMock) != 0u;
            bool currentExternal = active &&
                                   !mock &&
                                   first.RequestFrame == frameIndex &&
                                   first.EntityHashID != 0u &&
                                   math.all(math.isfinite(first.SampleAUP));
            if (currentExternal)
            {
                _mockRequestsSeeded = true;
                return false;
            }

            if (!_mockRequestsSeeded)
            {
                _mockRequestsSeeded = true;
                return true;
            }

            return !active;
        }

        private bool EnsureVaultHandles(IDataVault vault)
        {
            if (vault == null || vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                return false;

            _spectrumHandle = EnsureHandle(vault, _spectrumHandle, AnalyticalGerstnerWaveBufferIds.Spectrum, AnalyticalGerstnerWaveConstants.SpectrumRows, NativeArrayOptions.UninitializedMemory);
            _tuningHandle = EnsureHandle(vault, _tuningHandle, AnalyticalGerstnerWaveBufferIds.Tuning, 1, NativeArrayOptions.ClearMemory);
            _requestsHandle = EnsureHandle(vault, _requestsHandle, AnalyticalGerstnerWaveBufferIds.Requests, _sampleCapacity, NativeArrayOptions.UninitializedMemory);
            _resultsHandle = EnsureHandle(vault, _resultsHandle, AnalyticalGerstnerWaveBufferIds.Results, _sampleCapacity, NativeArrayOptions.UninitializedMemory);
            _macroGridHandle = EnsureHandle(vault, _macroGridHandle, AnalyticalGerstnerWaveBufferIds.MacroGrid, AnalyticalGerstnerWaveConstants.MacroGridMaxCells, NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = EnsureHandle(vault, _telemetryHandle, AnalyticalGerstnerWaveBufferIds.TelemetryRing, AnalyticalGerstnerWaveConstants.TelemetryCapacity, NativeArrayOptions.ClearMemory);
            _telemetryCursorHandle = EnsureHandle(vault, _telemetryCursorHandle, AnalyticalGerstnerWaveBufferIds.TelemetryCursor, 1, NativeArrayOptions.ClearMemory);
#if UNITY_EDITOR
            _csvScratchHandle = EnsureHandle(vault, _csvScratchHandle, AnalyticalGerstnerWaveBufferIds.CsvScratch, AnalyticalGerstnerWaveConstants.CsvScratchBytes, NativeArrayOptions.UninitializedMemory);
#endif
            _profilesHandle = EnsureHandle(vault, _profilesHandle, AnalyticalGerstnerWaveBufferIds.Profiles, AnalyticalGerstnerWaveConstants.ProfileCapacity, NativeArrayOptions.ClearMemory);
            _countersHandle = EnsureHandle(vault, _countersHandle, AnalyticalGerstnerWaveBufferIds.Counters, AnalyticalGerstnerWaveConstants.CounterCapacity, NativeArrayOptions.ClearMemory);
            return HandlesReady(vault);
        }

        private static VaultGenerationHandle<T> EnsureHandle<T>(
            IDataVault vault,
            VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            if (handle.BufferID != 0u &&
                vault.TryResolveHandle(in handle, out NativeArray<T> existing) &&
                existing.IsCreated &&
                existing.Length >= requiredLength)
            {
                return handle;
            }

            return vault.EnsureGenerationHandle<T>(bufferId, requiredLength, SystemID.Physics, options);
        }

        private bool HandlesReady(IDataVault vault)
        {
            return HasHandle(in _spectrumHandle) &&
                   HasHandle(in _tuningHandle) &&
                   HasHandle(in _requestsHandle) &&
                   HasHandle(in _resultsHandle) &&
                   HasHandle(in _macroGridHandle) &&
                   HasHandle(in _telemetryHandle) &&
                   HasHandle(in _telemetryCursorHandle) &&
#if UNITY_EDITOR
                   HasHandle(in _csvScratchHandle) &&
#endif
                   HasHandle(in _profilesHandle) &&
                   HasHandle(in _countersHandle) &&
                   vault.TryResolveHandle(in _tuningHandle, out NativeArray<GerstnerWaveTuningDTO> tuning) &&
                   tuning.IsCreated && tuning.Length > 0;
        }

        private static bool HasHandle<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private bool TryPrepareRuntimeVault(out IDataVault vault)
        {
            vault = _dataVault;
            return vault != null && _coldBootCompleted && HandlesReady(vault);
        }

        private void RefreshColdDependencies()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!ReferenceEquals(_dataVault, vault))
            {
                CompletePendingForTeardown();
                ReleaseVaultHandles(_dataVault);
                _dataVault = vault;
                _coldBootCompleted = false;
            }
        }

        private bool TryResolveRuntimeBuffers(
            IDataVault vault,
            out NativeArray<GerstnerWaveParamsDTO> spectrum,
            out NativeArray<GerstnerWaveTuningDTO> tuning,
            out NativeArray<OceanSampleRequestDTO> requests,
            out NativeArray<OceanSampleResultDTO> results,
            out NativeArray<float> macroGrid,
            out NativeArray<WaveMathCounterLane> counters)
        {
            spectrum = ResolveVaultBuffer(vault, in _spectrumHandle);
            tuning = ResolveVaultBuffer(vault, in _tuningHandle);
            requests = ResolveVaultBuffer(vault, in _requestsHandle);
            results = ResolveVaultBuffer(vault, in _resultsHandle);
            macroGrid = ResolveVaultBuffer(vault, in _macroGridHandle);
            counters = ResolveVaultBuffer(vault, in _countersHandle);
            return spectrum.IsCreated && spectrum.Length >= AnalyticalGerstnerWaveConstants.SpectrumRows &&
                   tuning.IsCreated && tuning.Length > 0 &&
                   requests.IsCreated &&
                   results.IsCreated &&
                   macroGrid.IsCreated &&
                   counters.IsCreated;
        }

        private bool ResolveTelemetryBuffers(IDataVault vault, out NativeArray<WaveMathTelemetryEntry> telemetry, out NativeArray<int> cursor)
        {
            telemetry = ResolveVaultBuffer(vault, in _telemetryHandle);
            cursor = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
            return telemetry.IsCreated && telemetry.Length > 0 && cursor.IsCreated && cursor.Length > 0;
        }

        private static NativeArray<T> ResolveVaultBuffer<T>(IDataVault vault, in VaultGenerationHandle<T> handle) where T : struct
        {
            return vault != null && handle.BufferID != 0u && vault.TryResolveHandle(in handle, out NativeArray<T> buffer)
                ? buffer
                : default;
        }

        private bool TryLockJobBuffers(IDataVault vault)
        {
            _lockedBuffers = 0;
            return TryLock(vault, AnalyticalGerstnerWaveBufferIds.Spectrum, LockSpectrum) &&
                   TryLock(vault, AnalyticalGerstnerWaveBufferIds.Tuning, LockTuning) &&
                   TryLock(vault, AnalyticalGerstnerWaveBufferIds.Requests, LockRequests) &&
                   TryLock(vault, AnalyticalGerstnerWaveBufferIds.Results, LockResults) &&
                   TryLock(vault, AnalyticalGerstnerWaveBufferIds.MacroGrid, LockMacroGrid) &&
                   TryLock(vault, AnalyticalGerstnerWaveBufferIds.Counters, LockCounters);
        }

        private bool TryLockTelemetryBuffers(IDataVault vault)
        {
            return TryLock(vault, AnalyticalGerstnerWaveBufferIds.TelemetryRing, LockTelemetryRing) &&
                   TryLock(vault, AnalyticalGerstnerWaveBufferIds.TelemetryCursor, LockTelemetryCursor);
        }

        private bool TryLock(IDataVault vault, BufferID bufferId, int bit)
        {
            if (vault != null && vault.TryLockBuffer(bufferId, SystemID.Physics))
            {
                _lockedBuffers |= bit;
                return true;
            }

            UnlockJobBuffers();
            return false;
        }

        private static void ClearCounterLanes(NativeArray<WaveMathCounterLane> counters)
        {
            if (!counters.IsCreated)
                return;

            int count = math.min(counters.Length, AnalyticalGerstnerWaveConstants.CounterCapacity);
            for (int i = 0; i < count; i++)
                counters[i] = default;
        }

        private void UnlockJobBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null || _lockedBuffers == 0)
            {
                _lockedBuffers = 0;
                return;
            }

            Unlock(vault, AnalyticalGerstnerWaveBufferIds.TelemetryCursor, LockTelemetryCursor);
            Unlock(vault, AnalyticalGerstnerWaveBufferIds.TelemetryRing, LockTelemetryRing);
            Unlock(vault, AnalyticalGerstnerWaveBufferIds.Counters, LockCounters);
            Unlock(vault, AnalyticalGerstnerWaveBufferIds.MacroGrid, LockMacroGrid);
            Unlock(vault, AnalyticalGerstnerWaveBufferIds.Results, LockResults);
            Unlock(vault, AnalyticalGerstnerWaveBufferIds.Requests, LockRequests);
            Unlock(vault, AnalyticalGerstnerWaveBufferIds.Tuning, LockTuning);
            Unlock(vault, AnalyticalGerstnerWaveBufferIds.Spectrum, LockSpectrum);
            _lockedBuffers = 0;
        }

        private void Unlock(IDataVault vault, BufferID bufferId, int bit)
        {
            if ((_lockedBuffers & bit) == 0)
                return;

            vault.TryUnlockBuffer(bufferId, SystemID.Physics);
        }

        private void TryRegister()
        {
            if (_registeredFixed && _registeredPostFixed && _registeredHotSwap)
                return;

            if (!_registeredFixed)
                _registeredFixed = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            if (!_registeredPostFixed)
                _registeredPostFixed = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Environment);
            if (!_registeredHotSwap)
                _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregister()
        {
            if (_registeredFixed)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
                _registeredFixed = false;
            }

            if (_registeredPostFixed)
            {
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
                _registeredPostFixed = false;
            }

            if (_registeredHotSwap)
            {
                GlobalRegistry.UnregisterHotSwapListener(this);
                _registeredHotSwap = false;
            }
        }

        private void TryRegisterOriginShiftListener()
        {
            if (!_runtimeActive)
                return;

            RefreshCachedOriginSnapshot();
            RefreshOriginShiftListenerRegistration();
        }

        private void RefreshOriginShiftListenerRegistration()
        {
            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
            if (_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void TryUnregisterOriginShiftListener()
        {
            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
            if (!_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            ApplyOriginSnapshot(in shiftData);
        }

        private void CompletePendingForTeardown()
        {
            if (_jobScheduled)
            {
                DispatcherJobFence.BeginPostFixedSwapWindow();
                DispatcherJobFence.TryComplete(ref _pendingHandle, forceComplete: true);
                DispatcherJobFence.EndPostFixedSwapWindow();
                _jobScheduled = false;
            }

            UnlockJobBuffers();
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            ReleaseHandle(vault, ref _spectrumHandle);
            ReleaseHandle(vault, ref _tuningHandle);
            ReleaseHandle(vault, ref _requestsHandle);
            ReleaseHandle(vault, ref _resultsHandle);
            ReleaseHandle(vault, ref _macroGridHandle);
            ReleaseHandle(vault, ref _telemetryHandle);
            ReleaseHandle(vault, ref _telemetryCursorHandle);
#if UNITY_EDITOR
            ReleaseHandle(vault, ref _csvScratchHandle);
#endif
            ReleaseHandle(vault, ref _profilesHandle);
            ReleaseHandle(vault, ref _countersHandle);
            _mockRequestsSeeded = false;
        }

        private static void ReleaseHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null && handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);
            handle = default;
        }

        private static float ResolveGlobalQualityWeight()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f);

            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, weight, math.isfinite(weight)));
        }

        private double3 ResolveCachedOriginAUP()
        {
            double3 origin = _cachedOriginAUP;
            return math.select(double3.zero, origin, math.isfinite(origin));
        }

        private void RefreshCachedOriginSnapshot()
        {
            OriginShiftEventData shiftEvent = HectonFloatingOrigin.LastShiftEvent;
            ApplyOriginSnapshot(in shiftEvent);
        }

        private void ApplyOriginSnapshot(in OriginShiftEventData shiftEvent)
        {
            double3 origin = shiftEvent.NewTotalOffsetDouble;
            _cachedOriginAUP = math.select(double3.zero, origin, math.isfinite(origin));
            _cachedOriginShiftSequence = shiftEvent.Sequence;
            _cachedOriginShiftFlags = shiftEvent.IsSafeTeleport != 0 ? 1u : 0u;
        }

        private static void ApplyProfile(
            WaveSpectrumProfileDTO profile,
            NativeArray<GerstnerWaveParamsDTO> spectrum,
            NativeArray<GerstnerWaveTuningDTO> tuning)
        {
            if (!spectrum.IsCreated || spectrum.Length <= 0 || !tuning.IsCreated || tuning.Length <= 0)
                return;

            GerstnerWaveTuningDTO tuningDto = tuning[0].Flags == 0u ? GerstnerWaveTuningDTO.Default() : tuning[0];
            tuningDto.ProfileHash = profile.StateHash;
            tuningDto.WindDirectionRadians = profile.WindDirectionRadians;
            tuningDto.StormWeight01 = profile.StormWeight01;
            tuningDto.WaveAmplitudeMultiplier = math.lerp(profile.MinAmplitudeMultiplier, profile.MaxAmplitudeMultiplier, 0.5f);
            tuningDto.LargestWavelengthMeters = math.max(profile.MaxWavelength, tuningDto.LargestWavelengthMeters);
            tuningDto.Flags |= AnalyticalGerstnerWaveConstants.FlagActive;
            tuning[0] = tuningDto;

            for (int row = 0; row < spectrum.Length; row++)
            {
                GerstnerWaveParamsDTO packed = default;
                packed.Wave1 = BuildProfileWave(profile, row * 4 + 0);
                packed.Wave2 = BuildProfileWave(profile, row * 4 + 1);
                packed.Wave3 = BuildProfileWave(profile, row * 4 + 2);
                packed.Wave4 = BuildProfileWave(profile, row * 4 + 3);
                spectrum[row] = packed;
            }
        }

        private static float4 BuildProfileWave(WaveSpectrumProfileDTO profile, int octave)
        {
            float t = math.saturate(octave * (1f / math.max(1f, AnalyticalGerstnerWaveConstants.MaxOctaves - 1f)));
            float angle = profile.WindDirectionRadians + octave * 0.31f;
            float steepness = math.lerp(profile.MaxSteepness, profile.MinSteepness, t);
            float wavelength = math.lerp(profile.MaxWavelength, profile.MinWavelength, t);
            float speed = math.lerp(profile.MinSpeed, profile.MaxSpeed, t);
            return new float4(angle, steepness, wavelength, speed);
        }

#if UNITY_EDITOR
        private static string ResolveProjectPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return null;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, relativePath));
        }

        private static int ReadFileIntoNativeScratch(string path, NativeArray<byte> scratch)
        {
            if (string.IsNullOrEmpty(path) || !scratch.IsCreated || scratch.Length <= 0)
                return 0;

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int limit = (int)math.min(stream.Length, scratch.Length);
                    if (limit <= 0)
                        return 0;

                    void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
                    Span<byte> destination = new Span<byte>(ptr, limit);
                    return stream.Read(destination);
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

        private static float ResolveElapsedMicros(long scheduleTimestamp)
        {
            if (scheduleTimestamp <= 0L)
                return 0f;

            long elapsed = Stopwatch.GetTimestamp() - scheduleTimestamp;
            if (elapsed <= 0L || Stopwatch.Frequency <= 0L)
                return 0f;

            double micros = elapsed * 1000000.0d / Stopwatch.Frequency;
            float value = (float)math.min(micros, float.MaxValue);
            return math.max(0f, math.select(0f, value, math.isfinite(value)));
        }

        private void DumpBlackBoxOnce(NativeArray<WaveMathTelemetryEntry> telemetry, NativeArray<int> telemetryCursor)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0 || !_coreBlackboxWarmed || GlobalTelemetryBus.BlackboxActiveFrameCount <= 0)
                return;

            int cursorValue = telemetryCursor.IsCreated && telemetryCursor.Length > 0 ? math.max(0, telemetryCursor[0]) : 0;
            int latestIndex = cursorValue > 0 ? (cursorValue - 1) % telemetry.Length : 0;
            WaveMathTelemetryEntry latest = telemetry[latestIndex];
            float scalar = math.max(latest.BurstMicros, latest.MaxAbsHeight);
            GlobalTelemetryBus.PushEvent(GerstnerFaultEventHash, scalar, latest.LastEntityHashID);
            _ = GlobalTelemetryBus.TryDumpBlackboxNow(GerstnerFaultDumpHash);
        }

        private void WarmCoreBlackboxRoute()
        {
            if (_coreBlackboxWarmed)
                return;

            GlobalTelemetryBus.Initialize();
            _coreBlackboxWarmed = GlobalTelemetryBus.BlackboxActiveFrameCount > 0;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || _dataVault == null || !HandlesReady(_dataVault))
                return;

            NativeArray<OceanSampleRequestDTO> requests = ResolveVaultBuffer(_dataVault, in _requestsHandle);
            NativeArray<OceanSampleResultDTO> results = ResolveVaultBuffer(_dataVault, in _resultsHandle);
            if (!requests.IsCreated || !results.IsCreated)
                return;

            int count = math.min(_scheduledSampleCount > 0 ? _scheduledSampleCount : _sampleCapacity, math.min(requests.Length, results.Length));
            int stride = math.max(1, count / 256);
            double3 origin = ResolveCachedOriginAUP();
            for (int i = 0; i < count; i += stride)
            {
                OceanSampleResultDTO result = results[i];
                if ((result.Flags & AnalyticalGerstnerWaveConstants.FlagActive) == 0u)
                    continue;

                double3 surfaceAup = result.SampleAUP;
                surfaceAup.y = result.WaterHeight;
                Vector3 runtime = HectonFloatingOrigin.ToRuntimePosition(surfaceAup, origin);
                Gizmos.color = (result.Flags & AnalyticalGerstnerWaveConstants.FlagCoarseGrid) != 0u
                    ? new Color(0.1f, 0.45f, 1f, 0.85f)
                    : new Color(0.1f, 0.95f, 0.85f, 0.9f);
                Gizmos.DrawWireSphere(runtime, 0.12f);
                Gizmos.color = Color.yellow;
                Vector3 normal = new Vector3(result.SurfaceNormal.x, result.SurfaceNormal.y, result.SurfaceNormal.z);
                Gizmos.DrawLine(runtime, runtime + normal * 0.75f);
            }
        }
#endif
    }
}
