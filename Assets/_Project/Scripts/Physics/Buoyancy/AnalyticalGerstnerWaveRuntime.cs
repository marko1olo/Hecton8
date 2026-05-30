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
        private const uint GerstnerFaultEventHash = 0x47464654u; // GFFT
        private const uint GerstnerFaultDumpHash = 0x47464450u; // GFDP
        private const uint JobPinSpectrum = 1u << 0;
        private const uint JobPinTuning = 1u << 1;
        private const uint JobPinRequests = 1u << 2;
        private const uint JobPinResults = 1u << 3;
        private const uint JobPinMacroGrid = 1u << 4;
        private const uint JobPinCounters = 1u << 5;

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
        private VaultGenerationHandle<WaveSpectrumProfileDTO> _profilesHandle;
        private VaultGenerationHandle<WaveMathCounterLane> _countersHandle;

        private JobHandle _pendingHandle;
        private long _scheduleTimestamp;
        private uint _simulationFrame;
        private int _scheduledSampleCount;
        private IDataVault _jobPinVault;
        private uint _jobPinMask;
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

            if (!vault.TryReadOnlyHandle(in _tuningHandle, out tuning) ||
                tuning.Length <= 0 ||
                !vault.TryReadOnlyHandle(in _telemetryHandle, out telemetry) ||
                telemetry.Length <= 0 ||
                !vault.TryReadOnlyHandle(in _telemetryCursorHandle, out cursor) ||
                cursor.Length <= 0 ||
                !vault.TryReadOnlyHandle(in _requestsHandle, out requests) ||
                !vault.TryReadOnlyHandle(in _resultsHandle, out results))
            {
                return false;
            }

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

            if (!_coldBootCompleted)
                return;

            if (!TryPrepareRuntimeVault(out IDataVault vault))
                return;

            if (!TryPinJobBuffers(vault))
                return;

            bool scheduled = false;
            try
            {
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

                GerstnerWaveTuningDTO tuningDto = PrepareTuning(tuning[0], requests.Length, fixedDeltaTime);
                tuning[0] = tuningDto;
                int sampleCount = math.clamp(tuningDto.ActiveRequestCount, 0, math.min(requests.Length, results.Length));
                if (sampleCount <= 0)
                    return;

                ClearCounterLanes(counters);
                JobHandle handle = default;

                if (_seedMockRequestsWhenEmpty && ConsumeMockRequestSeedGate(requests, sampleCount, _simulationFrame))
                {
                    GenerateMockWaveRequestsJob requestJob = default;
                    requestJob.Requests = requests;
                    requestJob.Count = sampleCount;
                    requestJob.OriginAUP = tuningDto.LocalOriginAUP;
                    requestJob.FrameIndex = _simulationFrame;
                    requestJob.OriginShiftSequence = tuningDto.OriginShiftSequence;
                    handle = requestJob.Schedule(sampleCount, 128, handle);
                }

                int gridResolution = math.clamp(tuningDto.MacroGridResolution, 2, AnalyticalGerstnerWaveConstants.MacroGridMaxResolution);
                int gridCells = math.min(gridResolution * gridResolution, macroGrid.Length);
                if (gridCells > 0)
                {
                    BuildMacroSwellGridJob gridJob = default;
                    gridJob.Spectrum = spectrum;
                    gridJob.MacroGrid = macroGrid;
                    gridJob.Tuning = tuningDto;
                    handle = gridJob.Schedule(gridCells, 64, handle);
                }

                EvaluateAnalyticalWavesJob evaluateJob = default;
                evaluateJob.Requests = requests;
                evaluateJob.Spectrum = spectrum;
                evaluateJob.MacroGrid = macroGrid;
                evaluateJob.Results = results;
                evaluateJob.Counters = counters;
                evaluateJob.Tuning = tuningDto;
                evaluateJob.SampleCount = sampleCount;

                int groupCount = (sampleCount + 3) >> 2;
                _scheduleTimestamp = Stopwatch.GetTimestamp();
                _scheduledSampleCount = sampleCount;
                _pendingHandle = evaluateJob.Schedule(groupCount, 32, handle);
                _jobScheduled = true;
                scheduled = true;
                H8Memory.RegisterActiveJob(SystemID.Physics, _pendingHandle);
            }
            finally
            {
                if (!scheduled)
                    ReleaseJobBufferPins();
            }
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            if (!_jobScheduled)
                return;

            bool finalized;
            DispatcherJobFence.BeginPostFixedSwapWindow();
            try
            {
                finalized = DispatcherJobFence.TryFinalizeCompleted(ref _pendingHandle);
            }
            finally
            {
                DispatcherJobFence.EndPostFixedSwapWindow();
            }

            if (!finalized)
                return;

            try
            {
                float elapsedMicros = ResolveElapsedMicros(_scheduleTimestamp);
                ReleaseJobBufferPins();

                IDataVault vault = _dataVault;
                if (vault != null &&
                    TryBuildWaveTelemetryEntry(
                        vault,
                        elapsedMicros,
                        out WaveMathTelemetryEntry telemetryEntry,
                        out int telemetryCursorValue,
                        out int telemetryNextCursorValue,
                        out int telemetryRingLength,
                        out bool shouldDumpBlackbox) &&
                    TryCommitWaveTelemetry(vault, in telemetryEntry, telemetryCursorValue, telemetryNextCursorValue, telemetryRingLength))
                {
                    if (shouldDumpBlackbox)
                    {
                        PushBlackBoxEvent(in telemetryEntry);
                        _dumpedFault = true;
                    }
                }
            }
            finally
            {
                ReleaseJobBufferPins();
                _jobScheduled = false;
                _scheduledSampleCount = 0;
                _simulationFrame++;
            }
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

            if (!OpenOrAcquireVaultHandlesForOwnerRoute(vault))
                return;

#if UNITY_EDITOR
            int stagedProfileRows = 0;
            Span<WaveSpectrumProfileDTO> stagedProfiles = stackalloc WaveSpectrumProfileDTO[AnalyticalGerstnerWaveConstants.ProfileCapacity];
            if (_loadCsvOnEnable)
                TryStageWaveProfilesCsv(stagedProfiles, out stagedProfileRows);
#endif

            if (!TryReadOrInitializeColdBootTuning(vault, out GerstnerWaveTuningDTO stagedTuning))
                return;

            bool hasSpectrum = false;
            Span<GerstnerWaveParamsDTO> stagedSpectrum = stackalloc GerstnerWaveParamsDTO[AnalyticalGerstnerWaveConstants.SpectrumRows];

#if UNITY_EDITOR
            if (stagedProfileRows > 0)
            {
                ApplyProfileToScratch(stagedProfiles[0], ref stagedTuning, stagedSpectrum);
                hasSpectrum = true;

                if (!TryCommitColdBootProfiles(vault, stagedProfiles))
                    return;
            }
#endif

            if (!hasSpectrum && _seedMockSpectrumOnEnable)
            {
                BuildMockWaveSpectrumScratch(ref stagedTuning, stagedSpectrum, _simulationFrame);
                hasSpectrum = true;
            }

            if (hasSpectrum && !TryCommitColdBootSpectrum(vault, stagedSpectrum))
                return;
            if (!TryCommitColdBootTuning(vault, in stagedTuning))
                return;
            if (!TryResetColdBootTelemetryCursor(vault))
                return;
            if (!TryClearColdBootCounters(vault))
                return;

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

        private bool OpenOrAcquireVaultHandlesForOwnerRoute(IDataVault vault)
        {
            if (vault == null || vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                return false;

            _spectrumHandle = OpenOrAcquireHandleForOwnerRoute(vault, _spectrumHandle, AnalyticalGerstnerWaveBufferIds.Spectrum, AnalyticalGerstnerWaveConstants.SpectrumRows, NativeArrayOptions.UninitializedMemory);
            _tuningHandle = OpenOrAcquireHandleForOwnerRoute(vault, _tuningHandle, AnalyticalGerstnerWaveBufferIds.Tuning, 1, NativeArrayOptions.ClearMemory);
            _requestsHandle = OpenOrAcquireHandleForOwnerRoute(vault, _requestsHandle, AnalyticalGerstnerWaveBufferIds.Requests, _sampleCapacity, NativeArrayOptions.UninitializedMemory);
            _resultsHandle = OpenOrAcquireHandleForOwnerRoute(vault, _resultsHandle, AnalyticalGerstnerWaveBufferIds.Results, _sampleCapacity, NativeArrayOptions.UninitializedMemory);
            _macroGridHandle = OpenOrAcquireHandleForOwnerRoute(vault, _macroGridHandle, AnalyticalGerstnerWaveBufferIds.MacroGrid, AnalyticalGerstnerWaveConstants.MacroGridMaxCells, NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = OpenOrAcquireHandleForOwnerRoute(vault, _telemetryHandle, AnalyticalGerstnerWaveBufferIds.TelemetryRing, AnalyticalGerstnerWaveConstants.TelemetryCapacity, NativeArrayOptions.ClearMemory);
            _telemetryCursorHandle = OpenOrAcquireHandleForOwnerRoute(vault, _telemetryCursorHandle, AnalyticalGerstnerWaveBufferIds.TelemetryCursor, 1, NativeArrayOptions.ClearMemory);
            _profilesHandle = OpenOrAcquireHandleForOwnerRoute(vault, _profilesHandle, AnalyticalGerstnerWaveBufferIds.Profiles, AnalyticalGerstnerWaveConstants.ProfileCapacity, NativeArrayOptions.ClearMemory);
            _countersHandle = OpenOrAcquireHandleForOwnerRoute(vault, _countersHandle, AnalyticalGerstnerWaveBufferIds.Counters, AnalyticalGerstnerWaveConstants.CounterCapacity, NativeArrayOptions.ClearMemory);
            return HandlesReady(vault);
        }

        private static VaultGenerationHandle<T> OpenOrAcquireHandleForOwnerRoute<T>(
            IDataVault vault,
            VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            if (handle.BufferID != 0u &&
                vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly existing) &&
                existing.IsCreated &&
                existing.Length >= requiredLength)
            {
                return handle;
            }

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return default;

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
                   HasHandle(in _profilesHandle) &&
                   HasHandle(in _countersHandle) &&
                   vault.TryReadOnlyHandle(in _tuningHandle, out NativeArray<GerstnerWaveTuningDTO>.ReadOnly tuning) &&
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

        private static NativeArray<T> ResolveVaultBuffer<T>(IDataVault vault, in VaultGenerationHandle<T> handle) where T : struct
        {
            return vault != null && handle.BufferID != 0u && vault.TryResolveHandle(in handle, out NativeArray<T> buffer)
                ? buffer
                : default;
        }

        private bool TryPinJobBuffers(IDataVault vault)
        {
            ReleaseJobBufferPins();
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            bool pinned = false;
            try
            {
                _jobPinVault = vault;
                if (!TryLockJobBuffer(vault, AnalyticalGerstnerWaveBufferIds.Spectrum, JobPinSpectrum) ||
                    !TryLockJobBuffer(vault, AnalyticalGerstnerWaveBufferIds.Tuning, JobPinTuning) ||
                    !TryLockJobBuffer(vault, AnalyticalGerstnerWaveBufferIds.Requests, JobPinRequests) ||
                    !TryLockJobBuffer(vault, AnalyticalGerstnerWaveBufferIds.Results, JobPinResults) ||
                    !TryLockJobBuffer(vault, AnalyticalGerstnerWaveBufferIds.MacroGrid, JobPinMacroGrid) ||
                    !TryLockJobBuffer(vault, AnalyticalGerstnerWaveBufferIds.Counters, JobPinCounters))
                {
                    return false;
                }

                if (!TryResolveRuntimeBuffers(
                        vault,
                        out _,
                        out _,
                        out _,
                        out _,
                        out _,
                        out _))
                {
                    return false;
                }

                pinned = true;
                return true;
            }
            finally
            {
                if (!pinned)
                    ReleaseJobBufferPins();
            }
        }

        private static void ClearCounterLanes(NativeArray<WaveMathCounterLane> counters)
        {
            if (!counters.IsCreated)
                return;

            int count = math.min(counters.Length, AnalyticalGerstnerWaveConstants.CounterCapacity);
            for (int i = 0; i < count; i++)
                counters[i] = default;
        }

        private void ReleaseJobBufferPins()
        {
            IDataVault vault = _jobPinVault;
            uint pinMask = _jobPinMask;
            _jobPinVault = null;
            _jobPinMask = 0u;
            if (vault == null || pinMask == 0u)
                return;

            TryUnlockJobBuffer(vault, pinMask, JobPinCounters, AnalyticalGerstnerWaveBufferIds.Counters);
            TryUnlockJobBuffer(vault, pinMask, JobPinMacroGrid, AnalyticalGerstnerWaveBufferIds.MacroGrid);
            TryUnlockJobBuffer(vault, pinMask, JobPinResults, AnalyticalGerstnerWaveBufferIds.Results);
            TryUnlockJobBuffer(vault, pinMask, JobPinRequests, AnalyticalGerstnerWaveBufferIds.Requests);
            TryUnlockJobBuffer(vault, pinMask, JobPinTuning, AnalyticalGerstnerWaveBufferIds.Tuning);
            TryUnlockJobBuffer(vault, pinMask, JobPinSpectrum, AnalyticalGerstnerWaveBufferIds.Spectrum);
        }

        private bool TryLockJobBuffer(IDataVault vault, BufferID bufferId, uint pinBit)
        {
            if ((_jobPinMask & pinBit) != 0u)
                return true;

            if (vault == null || !vault.TryLockBuffer(bufferId, SystemID.Physics))
                return false;

            _jobPinMask |= pinBit;
            return true;
        }

        private static void TryUnlockJobBuffer(IDataVault vault, uint pinMask, uint pinBit, BufferID bufferId)
        {
            if ((pinMask & pinBit) != 0u)
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
                try
                {
                    DispatcherJobFence.TryComplete(ref _pendingHandle, forceComplete: true);
                }
                finally
                {
                    DispatcherJobFence.EndPostFixedSwapWindow();
                }

                _jobScheduled = false;
            }

            ReleaseJobBufferPins();
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

        private bool TryBuildWaveTelemetryEntry(
            IDataVault vault,
            float elapsedMicros,
            out WaveMathTelemetryEntry entry,
            out int cursorValue,
            out int nextCursorValue,
            out int ringLength,
            out bool shouldDumpBlackbox)
        {
            entry = default;
            cursorValue = 0;
            nextCursorValue = 0;
            ringLength = 0;
            shouldDumpBlackbox = false;

            if (vault == null ||
                !vault.TryReadOnlyHandle(in _tuningHandle, out NativeArray<GerstnerWaveTuningDTO>.ReadOnly tuning) ||
                !vault.TryReadOnlyHandle(in _resultsHandle, out NativeArray<OceanSampleResultDTO>.ReadOnly results) ||
                !vault.TryReadOnlyHandle(in _countersHandle, out NativeArray<WaveMathCounterLane>.ReadOnly counters) ||
                !vault.TryReadOnlyHandle(in _telemetryHandle, out NativeArray<WaveMathTelemetryEntry>.ReadOnly telemetryRing) ||
                !vault.TryReadOnlyHandle(in _telemetryCursorHandle, out NativeArray<int>.ReadOnly telemetryCursor) ||
                tuning.Length <= 0 ||
                telemetryRing.Length <= 0 ||
                telemetryCursor.Length <= 0)
            {
                return false;
            }

            GerstnerWaveTuningDTO tuningDto = tuning[0];
            int count = math.min(math.max(0, _scheduledSampleCount), results.Length);
            uint lastHash = 0u;
            float maxAbsHeight = 0f;
            uint flags = 0u;
            int resultWindow = math.min(count, 1024);
            for (int i = 0; i < resultWindow; i++)
            {
                OceanSampleResultDTO result = results[i];
                lastHash = math.select(lastHash, result.EntityHashID, result.EntityHashID != 0u);
                maxAbsHeight = math.max(maxAbsHeight, math.abs(math.select(0f, result.WaterHeight, math.isfinite(result.WaterHeight))));
                flags |= result.Flags & AnalyticalGerstnerWaveConstants.FlagNonFinite;
            }

            int evaluated = counters.Length > 0 ? counters[0].Value : count;
            int coarse = counters.Length > 1 ? counters[1].Value : 0;
            int nonFinite = counters.Length > 2 ? counters[2].Value : 0;
            int staleOrigin = counters.Length > 3 ? counters[3].Value : 0;
            cursorValue = math.max(0, telemetryCursor[0]);
            ringLength = telemetryRing.Length;
            nextCursorValue = cursorValue >= int.MaxValue - 1 ? ringLength : cursorValue + 1;

            entry.FrameIndex = tuningDto.FrameIndex;
            entry.EvaluatedCoordinates = math.max(0, evaluated);
            entry.ActiveOctaves = AnalyticalGerstnerWaveMath.ResolveActiveOctaves(in tuningDto);
            entry.CoarseGridSamples = math.max(0, coarse);
            entry.BurstMicros = math.max(0f, math.select(0f, elapsedMicros, math.isfinite(elapsedMicros)));
            entry.GlobalQualityWeight = math.saturate(math.select(1f, tuningDto.GlobalQualityWeight, math.isfinite(tuningDto.GlobalQualityWeight)));
            entry.Flags = flags |
                          math.select(0u, AnalyticalGerstnerWaveConstants.FlagNonFinite, nonFinite > 0) |
                          math.select(0u, AnalyticalGerstnerWaveConstants.FlagStaleOrigin, staleOrigin > 0);
            entry.NonFiniteCount = math.max(0, nonFinite);
            entry.LastEntityHashID = lastHash;
            entry.MaxAbsHeight = maxAbsHeight;
            entry.MacroGridResolution = math.max(0, tuningDto.MacroGridResolution);
            entry.RequestCount = count;
            entry.KernelHash = AnalyticalGerstnerWaveConstants.KernelHash;
            entry.ProfileHash = tuningDto.ProfileHash;
            entry.OriginShiftSequence = tuningDto.OriginShiftSequence;

            float dumpThreshold = math.max(1f, math.select(AnalyticalGerstnerWaveConstants.DefaultDumpThresholdMicros, tuningDto.MaxSolverMicrosBeforeDump, math.isfinite(tuningDto.MaxSolverMicrosBeforeDump)));
            shouldDumpBlackbox = !_dumpedFault && (entry.BurstMicros > dumpThreshold || entry.NonFiniteCount > 0);
            return true;
        }

        private bool TryCommitWaveTelemetry(
            IDataVault vault,
            in WaveMathTelemetryEntry entry,
            int cursorValue,
            int nextCursorValue,
            int expectedRingLength)
        {
            if (!TryCommitWaveTelemetryEntry(vault, in entry, cursorValue, expectedRingLength))
                return false;

            return TryCommitWaveTelemetryCursor(vault, nextCursorValue);
        }

        private bool TryCommitWaveTelemetryEntry(
            IDataVault vault,
            in WaveMathTelemetryEntry entry,
            int cursorValue,
            int expectedRingLength)
        {
            bool lockAcquired = false;
            try
            {
                if (vault == null ||
                    !vault.TryAcquireWriteLock(in _telemetryHandle, SystemID.Physics, out NativeArray<WaveMathTelemetryEntry> telemetry))
                {
                    return false;
                }
                lockAcquired = true;
                if (!telemetry.IsCreated || telemetry.Length <= 0 || telemetry.Length != expectedRingLength)
                    return false;

                int slot = math.max(0, cursorValue) % telemetry.Length;
                telemetry[slot] = entry;
                return true;
            }
            finally
            {
                if (lockAcquired)
                    vault.ReleaseWriteLock(in _telemetryHandle, SystemID.Physics);
            }
        }

        private bool TryCommitWaveTelemetryCursor(IDataVault vault, int nextCursorValue)
        {
            bool lockAcquired = false;
            try
            {
                if (vault == null ||
                    !vault.TryAcquireWriteLock(in _telemetryCursorHandle, SystemID.Physics, out NativeArray<int> telemetryCursor))
                {
                    return false;
                }
                lockAcquired = true;
                if (!telemetryCursor.IsCreated || telemetryCursor.Length <= 0)
                    return false;

                telemetryCursor[0] = math.max(0, nextCursorValue);
                return true;
            }
            finally
            {
                if (lockAcquired)
                    vault.ReleaseWriteLock(in _telemetryCursorHandle, SystemID.Physics);
            }
        }

        private bool TryReadOrInitializeColdBootTuning(IDataVault vault, out GerstnerWaveTuningDTO tuningDto)
        {
            tuningDto = default;
            bool lockAcquired = false;
            try
            {
                if (vault == null ||
                    !vault.TryAcquireWriteLock(in _tuningHandle, SystemID.Physics, out NativeArray<GerstnerWaveTuningDTO> tuning))
                {
                    return false;
                }
                lockAcquired = true;
                if (!tuning.IsCreated || tuning.Length <= 0)
                    return false;

                tuningDto = tuning[0];
                if (tuningDto.Flags == 0u)
                {
                    tuningDto = GerstnerWaveTuningDTO.Default();
                    tuning[0] = tuningDto;
                }

                return true;
            }
            finally
            {
                if (lockAcquired)
                    vault.ReleaseWriteLock(in _tuningHandle, SystemID.Physics);
            }
        }

        private bool TryCommitColdBootTuning(IDataVault vault, in GerstnerWaveTuningDTO tuningDto)
        {
            bool lockAcquired = false;
            try
            {
                if (vault == null ||
                    !vault.TryAcquireWriteLock(in _tuningHandle, SystemID.Physics, out NativeArray<GerstnerWaveTuningDTO> tuning))
                {
                    return false;
                }
                lockAcquired = true;
                if (!tuning.IsCreated || tuning.Length <= 0)
                    return false;

                tuning[0] = tuningDto;
                return true;
            }
            finally
            {
                if (lockAcquired)
                    vault.ReleaseWriteLock(in _tuningHandle, SystemID.Physics);
            }
        }

        private bool TryCommitColdBootSpectrum(IDataVault vault, ReadOnlySpan<GerstnerWaveParamsDTO> staged)
        {
            if (staged.Length <= 0)
                return false;

            bool lockAcquired = false;
            try
            {
                if (vault == null ||
                    !vault.TryAcquireWriteLock(in _spectrumHandle, SystemID.Physics, out NativeArray<GerstnerWaveParamsDTO> spectrum))
                {
                    return false;
                }
                lockAcquired = true;
                if (!spectrum.IsCreated || spectrum.Length != staged.Length)
                    return false;

                fixed (GerstnerWaveParamsDTO* source = staged)
                {
                    void* target = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(spectrum);
                    long byteCount = (long)staged.Length * UnsafeUtility.SizeOf<GerstnerWaveParamsDTO>();
                    if (!UnsafeMemoryCopyGuard.SafeCopy(target, byteCount, source, byteCount))
                        return false;
                }

                return true;
            }
            finally
            {
                if (lockAcquired)
                    vault.ReleaseWriteLock(in _spectrumHandle, SystemID.Physics);
            }
        }

        private bool TryResetColdBootTelemetryCursor(IDataVault vault)
        {
            bool lockAcquired = false;
            try
            {
                if (vault == null ||
                    !vault.TryAcquireWriteLock(in _telemetryCursorHandle, SystemID.Physics, out NativeArray<int> telemetryCursor))
                {
                    return false;
                }
                lockAcquired = true;
                if (!telemetryCursor.IsCreated || telemetryCursor.Length <= 0)
                    return false;

                telemetryCursor[0] = 0;
                return true;
            }
            finally
            {
                if (lockAcquired)
                    vault.ReleaseWriteLock(in _telemetryCursorHandle, SystemID.Physics);
            }
        }

        private bool TryClearColdBootCounters(IDataVault vault)
        {
            bool lockAcquired = false;
            try
            {
                if (vault == null ||
                    !vault.TryAcquireWriteLock(in _countersHandle, SystemID.Physics, out NativeArray<WaveMathCounterLane> counters))
                {
                    return false;
                }
                lockAcquired = true;
                if (!counters.IsCreated || counters.Length <= 0)
                    return false;

                void* target = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(counters);
                UnsafeUtility.MemClear(target, (long)counters.Length * UnsafeUtility.SizeOf<WaveMathCounterLane>());
                return true;
            }
            finally
            {
                if (lockAcquired)
                    vault.ReleaseWriteLock(in _countersHandle, SystemID.Physics);
            }
        }

        private static void ApplyProfileToScratch(
            WaveSpectrumProfileDTO profile,
            ref GerstnerWaveTuningDTO tuning,
            Span<GerstnerWaveParamsDTO> spectrum)
        {
            tuning.ProfileHash = profile.StateHash;
            tuning.WindDirectionRadians = profile.WindDirectionRadians;
            tuning.StormWeight01 = profile.StormWeight01;
            tuning.WaveAmplitudeMultiplier = math.lerp(profile.MinAmplitudeMultiplier, profile.MaxAmplitudeMultiplier, 0.5f);
            tuning.LargestWavelengthMeters = math.max(profile.MaxWavelength, tuning.LargestWavelengthMeters);
            tuning.Flags |= AnalyticalGerstnerWaveConstants.FlagActive;

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

        private static void BuildMockWaveSpectrumScratch(
            ref GerstnerWaveTuningDTO tuning,
            Span<GerstnerWaveParamsDTO> spectrum,
            uint frameIndex)
        {
            float q = math.saturate(math.select(1f, tuning.GlobalQualityWeight, math.isfinite(tuning.GlobalQualityWeight)));
            float wind = math.max(0.01f, math.select(10f, tuning.WindSpeedMetersPerSecond, math.isfinite(tuning.WindSpeedMetersPerSecond)));
            float windDirection = math.select(0.35f, tuning.WindDirectionRadians, math.isfinite(tuning.WindDirectionRadians));

            for (int row = 0; row < spectrum.Length; row++)
            {
                GerstnerWaveParamsDTO packed = default;
                packed.Wave1 = BuildMockWave(row * 4 + 0, windDirection, wind, frameIndex);
                packed.Wave2 = BuildMockWave(row * 4 + 1, windDirection, wind, frameIndex);
                packed.Wave3 = BuildMockWave(row * 4 + 2, windDirection, wind, frameIndex);
                packed.Wave4 = BuildMockWave(row * 4 + 3, windDirection, wind, frameIndex);
                spectrum[row] = packed;
            }

            tuning.GlobalQualityWeight = q;
            tuning.WindDirectionRadians = windDirection;
            tuning.WindSpeedMetersPerSecond = wind;
            tuning.StormWeight01 = math.saturate((wind - 2f) * math.rcp(30f));
            tuning.TotalOctaves = AnalyticalGerstnerWaveConstants.MaxOctaves;
            tuning.MaxOctaveLimit = math.clamp(tuning.MaxOctaveLimit <= 0 ? AnalyticalGerstnerWaveConstants.MaxOctaves : tuning.MaxOctaveLimit, 1, AnalyticalGerstnerWaveConstants.MaxOctaves);
            tuning.LargestWavelengthMeters = math.max(16f, tuning.LargestWavelengthMeters);
            tuning.FrameIndex = frameIndex;
        }

        private static float4 BuildProfileWave(WaveSpectrumProfileDTO profile, int octave)
        {
            float t = math.saturate(octave * (1f / math.max(1f, AnalyticalGerstnerWaveConstants.MaxOctaves - 1f)));
            float angle = profile.WindDirectionRadians + octave * 0.31f;
            float steepness = math.lerp(profile.MaxSteepness, profile.MinSteepness, t);
            float wavelength = math.lerp(profile.MaxWavelength, profile.MinWavelength, t);
            float speed = math.lerp(profile.MinSpeed, profile.MaxSpeed, t);
            float4 wave;
            wave.x = angle;
            wave.y = steepness;
            wave.z = wavelength;
            wave.w = speed;
            return wave;
        }

        private static float4 BuildMockWave(int octave, float windDirection, float windSpeed, uint frame)
        {
            float octave01 = math.saturate(octave * (1f / math.max(1f, AnalyticalGerstnerWaveConstants.MaxOctaves - 1f)));
            float angleJitter = HashToSigned01((uint)(octave + 1) * 0x9E3779B9u + frame) * 0.28f;
            float wavelength = math.lerp(128f, 6f, octave01);
            float steepness = 0.11f * math.lerp(1f, 0.42f, octave01);
            float speed = math.lerp(0.18f, 1.45f, octave01) * math.lerp(0.65f, 1.55f, math.saturate(windSpeed * (1f / 28f)));
            float4 wave;
            wave.x = windDirection + angleJitter + octave * 0.37f;
            wave.y = steepness;
            wave.z = wavelength;
            wave.w = speed;
            return wave;
        }

        private static uint Hash32(uint value)
        {
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return math.select(1u, value, value != 0u);
        }

        private static float HashToSigned01(uint value)
        {
            return ((Hash32(value) & 0x00FFFFFFu) * (1f / 16777215f)) * 2f - 1f;
        }

#if UNITY_EDITOR
        private bool TryCommitColdBootProfiles(IDataVault vault, ReadOnlySpan<WaveSpectrumProfileDTO> staged)
        {
            if (staged.Length <= 0)
                return false;

            bool lockAcquired = false;
            try
            {
                if (vault == null ||
                    !vault.TryAcquireWriteLock(in _profilesHandle, SystemID.Physics, out NativeArray<WaveSpectrumProfileDTO> profiles))
                {
                    return false;
                }
                lockAcquired = true;
                if (!profiles.IsCreated || profiles.Length != staged.Length)
                    return false;

                return CopyWaveProfilesToVault(staged, profiles);
            }
            finally
            {
                if (lockAcquired)
                    vault.ReleaseWriteLock(in _profilesHandle, SystemID.Physics);
            }
        }

        private bool TryStageWaveProfilesCsv(Span<WaveSpectrumProfileDTO> profileScratch, out int profileRows)
        {
            profileRows = 0;
            if (profileScratch.Length <= 0)
                return false;

            string path = ResolveProjectPath(_csvRelativePath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            Span<byte> csvScratch = stackalloc byte[AnalyticalGerstnerWaveConstants.CsvImportByteCapacity];
            int bytesRead = ReadFileIntoColdScratch(path, csvScratch);
            if (bytesRead <= 0)
                return false;

            return WaveSpectrumProfileCsvParser.TryApply(csvScratch.Slice(0, bytesRead), profileScratch, out profileRows);
        }

        private static bool CopyWaveProfilesToVault(
            ReadOnlySpan<WaveSpectrumProfileDTO> staged,
            NativeArray<WaveSpectrumProfileDTO> profiles)
        {
            if (staged.Length <= 0 || !profiles.IsCreated || profiles.Length != staged.Length)
                return false;

            fixed (WaveSpectrumProfileDTO* source = staged)
            {
                void* target = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(profiles);
                long byteCount = (long)staged.Length * UnsafeUtility.SizeOf<WaveSpectrumProfileDTO>();
                return UnsafeMemoryCopyGuard.SafeCopy(target, byteCount, source, byteCount);
            }
        }

        private static string ResolveProjectPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return null;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, relativePath));
        }

        private static int ReadFileIntoColdScratch(string path, Span<byte> scratch)
        {
            if (string.IsNullOrEmpty(path) || scratch.Length <= 0)
                return 0;

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length <= 0L || stream.Length > scratch.Length)
                        return 0;

                    int expectedBytes = (int)stream.Length;
                    int read = 0;
                    while (read < expectedBytes)
                    {
                        int chunk = stream.Read(scratch.Slice(read, expectedBytes - read));
                        if (chunk <= 0)
                            return 0;
                        read += chunk;
                    }

                    return read == expectedBytes ? read : 0;
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

        private void PushBlackBoxEvent(in WaveMathTelemetryEntry latest)
        {
            if (!_coreBlackboxWarmed || GlobalTelemetryBus.BlackboxActiveFrameCount <= 0)
                return;

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

            _dataVault.TryReadOnlyHandle(in _requestsHandle, out NativeArray<OceanSampleRequestDTO>.ReadOnly requests);
            _dataVault.TryReadOnlyHandle(in _resultsHandle, out NativeArray<OceanSampleResultDTO>.ReadOnly results);
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
