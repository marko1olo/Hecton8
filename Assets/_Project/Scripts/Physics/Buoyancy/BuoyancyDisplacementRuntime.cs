using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
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
    public unsafe sealed class BuoyancyDisplacementRuntime : MonoBehaviour, IFixedTickable, IPostFixedTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener, IOriginShiftListener
    {
        private const int LockStates = 1 << 0;
        private const int LockFlowSamples = 1 << 1;
        private const int LockTuning = 1 << 2;
        private const int LockTelemetry = 1 << 3;
        private const int LockTelemetryCursor = 1 << 4;
        private const int LockDebugForces = 1 << 5;
        private const int LockCounters = 1 << 6;
        private const int LockForcePackets = 1 << 7;
        private const int LockSleepTelemetry = 1 << 8;
        private const int LockSleepTelemetryCursor = 1 << 9;
        private const int LockSleepSdfDensity = 1 << 10;
        private const int LockSleepSdfConfig = 1 << 11;
        private const int LockMaterialSettlingProfiles = 1 << 12;

        [Header("Vault Capacity")]
        [SerializeField, Range(1, BuoyancyDisplacementConstants.StateCapacity)]
        [Tooltip("Maximum buoyant object records processed by the SHINOBU_201 SIMD/buoyancy solver.")]
        private int _stateCapacity = BuoyancyDisplacementConstants.StateCapacity;

        [SerializeField, Range(0, BuoyancyDisplacementConstants.FlowSampleCapacity)]
        [Tooltip("Maximum abyssal flow sample records read from the Vault.")]
        private int _flowSampleCapacity = BuoyancyDisplacementConstants.FlowSampleCapacity;

        [Header("Cold Boot")]
        [SerializeField]
        [Tooltip("Seeds 1000 deterministic synthetic buoyant objects when no inventory drop stream is present.")]
        private bool _seedEmergencyMockObjects = true;

        [SerializeField]
        [Tooltip("Loads item_volume_specs.csv into the Vault-backed material volume table during cold startup.")]
        private bool _loadCsvOnEnable = true;

        [SerializeField]
        [Tooltip("Loads material_settling_profiles.csv into the Vault-backed sleep profile table during cold startup.")]
        private bool _loadMaterialSettlingProfilesOnEnable = true;

        [SerializeField]
        [Tooltip("Loads simd_math_tolerances.csv into the Vault-backed SIMD polynomial tolerance table during cold startup.")]
        private bool _loadSimdTolerancesOnEnable = true;

        [SerializeField]
        [Tooltip("Project-relative material volume CSV path.")]
        private string _csvRelativePath = BuoyancyDisplacementConstants.CsvRelativePath;

        [SerializeField]
        [Tooltip("Project-relative material settling CSV path.")]
        private string _materialSettlingProfilesCsvRelativePath = BuoyancyDisplacementConstants.MaterialSettlingProfilesCsvRelativePath;

        private IDataVault _dataVault;
        private VaultGenerationHandle<BuoyancyStateDTO> _statesHandle;
        private VaultGenerationHandle<BuoyancyForcePacketDTO> _forcePacketsHandle;
        private VaultGenerationHandle<BuoyancyFlowSampleDTO> _flowSamplesHandle;
        private VaultGenerationHandle<BuoyancyTuningDTO> _tuningHandle;
        private VaultGenerationHandle<BuoyancyTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<BuoyancyMaterialVolumeDTO> _materialVolumesHandle;
        private VaultGenerationHandle<BuoyancyMaterialSettlingProfileDTO> _materialSettlingProfilesHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<BuoyancyDebugForceDTO> _debugForcesHandle;
        private VaultGenerationHandle<BuoyancyCounterDTO> _countersHandle;
        private VaultGenerationHandle<BuoyancyBodyBindingDTO> _bodyBindingsHandle;
        private VaultGenerationHandle<sbyte> _sleepSdfDensityHandle;
        private VaultGenerationHandle<BuoyancySleepSdfConfigDTO> _sleepSdfConfigHandle;
        private VaultGenerationHandle<SleepStateTelemetryEntry> _sleepTelemetryRingHandle;
        private VaultGenerationHandle<int> _sleepTelemetryCursorHandle;
        private VaultGenerationHandle<SimdFloat3Padded> _simdLocalPositionsHandle;
        private VaultGenerationHandle<SimdFloat3Padded> _simdVelocitiesHandle;
        private VaultGenerationHandle<float> _simdDragCoefficientsHandle;
        private VaultGenerationHandle<SimdFloat3Padded> _simdOutputForcesHandle;
        private VaultGenerationHandle<SimdTelemetryEntry> _simdTelemetryRingHandle;
        private VaultGenerationHandle<int> _simdTelemetryCursorHandle;
        private VaultGenerationHandle<SimdMathToleranceDTO> _simdMathTolerancesHandle;
        private VaultGenerationHandle<int> _simdVisibleIndexMaskHandle;
        private VaultGenerationHandle<int> _simdVisibleIndicesHandle;
        private VaultGenerationHandle<int> _simdVisibleCountHandle;
        private VaultGenerationHandle<SimdHydrodynamicTuningDTO> _simdHydrodynamicTuningHandle;
        private JobHandle _pendingHandle;
        private long _scheduleTimestamp;
        private uint _simulationFrame;
        private int _activeStateCount;
        private int _lockedBuffers;
        private double3 _cachedSectorAup;
        private bool _jobScheduled;
        private bool _registeredFixed;
        private bool _registeredPostFixed;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _registeredOriginShiftListener;
        private bool _coldBuffersInitialized;
        private bool _coldBootCompleted;
        private bool _dumpedFault;
        private bool _forcePacketsReadyToDrain;

#if UNITY_EDITOR
        private static BuoyancyDisplacementRuntime _activeRuntimeInstance;

        public static bool TryGetActiveRuntimeInstance(out BuoyancyDisplacementRuntime runtime)
        {
            runtime = _activeRuntimeInstance;
            return runtime != null;
        }
#endif

#if UNITY_EDITOR
        /// <summary>Opens mutable editor views for buoyancy tuning, counters, telemetry, and cursor rows.</summary>
        /// <param name="tuning">Opened tuning row.</param>
        /// <param name="counters">Opened counter row.</param>
        /// <param name="telemetry">Opened buoyancy telemetry ring.</param>
        /// <param name="cursor">Opened buoyancy telemetry cursor.</param>
        /// <remarks>Editor-only cold surface; gameplay phases do not call this accessor.</remarks>
        public bool TryOpenEditorViews(
            out NativeArray<BuoyancyTuningDTO> tuning,
            out NativeArray<BuoyancyCounterDTO> counters,
            out NativeArray<BuoyancyTelemetryEntry> telemetry,
            out NativeArray<int> cursor)
        {
            NativeArray<SleepStateTelemetryEntry> unused;
            return TryOpenEditorViews(out tuning, out counters, out telemetry, out unused, out cursor);
        }

        /// <summary>Opens mutable editor views for buoyancy and sleep telemetry rows.</summary>
        /// <param name="tuning">Opened tuning row.</param>
        /// <param name="counters">Opened counter row.</param>
        /// <param name="telemetry">Opened buoyancy telemetry ring.</param>
        /// <param name="sleepTelemetry">Opened sleep telemetry ring.</param>
        /// <param name="cursor">Opened telemetry cursor.</param>
        /// <remarks>Editor-only cold surface; returned buffers are not a read-only gameplay API.</remarks>
        public bool TryOpenEditorViews(
            out NativeArray<BuoyancyTuningDTO> tuning,
            out NativeArray<BuoyancyCounterDTO> counters,
            out NativeArray<BuoyancyTelemetryEntry> telemetry,
            out NativeArray<SleepStateTelemetryEntry> sleepTelemetry,
            out NativeArray<int> cursor)
        {
            tuning = default;
            counters = default;
            telemetry = default;
            sleepTelemetry = default;
            cursor = default;
            IDataVault vault = _dataVault;
            if (vault == null || !HandlesReady(vault))
                return false;

            tuning = ResolveVaultBuffer(vault, in _tuningHandle);
            counters = ResolveVaultBuffer(vault, in _countersHandle);
            telemetry = ResolveVaultBuffer(vault, in _telemetryRingHandle);
            sleepTelemetry = ResolveVaultBuffer(vault, in _sleepTelemetryRingHandle);
            cursor = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
            return tuning.IsCreated && tuning.Length > 0 &&
                   counters.IsCreated && counters.Length > 0 &&
                   telemetry.IsCreated && telemetry.Length > 0 &&
                   sleepTelemetry.IsCreated && sleepTelemetry.Length > 0 &&
                   cursor.IsCreated && cursor.Length > 0;
        }

        /// <summary>Opens mutable editor views for SIMD benchmark telemetry and tolerance rows.</summary>
        /// <param name="telemetry">Opened SIMD telemetry ring.</param>
        /// <param name="cursor">Opened SIMD telemetry cursor.</param>
        /// <param name="tolerances">Opened SIMD tolerance rows.</param>
        /// <remarks>Editor-only cold surface used by Burst Vectorization X-Ray tooling.</remarks>
        public bool TryOpenSimdEditorViews(
            out NativeArray<SimdTelemetryEntry> telemetry,
            out NativeArray<int> cursor,
            out NativeArray<SimdMathToleranceDTO> tolerances)
        {
            telemetry = default;
            cursor = default;
            tolerances = default;
            IDataVault vault = _dataVault;
            if (vault == null || !HandlesReady(vault))
                return false;

            telemetry = ResolveVaultBuffer(vault, in _simdTelemetryRingHandle);
            cursor = ResolveVaultBuffer(vault, in _simdTelemetryCursorHandle);
            tolerances = ResolveVaultBuffer(vault, in _simdMathTolerancesHandle);
            return telemetry.IsCreated && telemetry.Length > 0 &&
                   cursor.IsCreated && cursor.Length > 0 &&
                   tolerances.IsCreated && tolerances.Length > 0;
        }

        /// <summary>Opens the editor-only SIMD hydrodynamic tuning row.</summary>
        /// <param name="tuning">Opened SIMD hydrodynamic tuning row.</param>
        /// <remarks>Editor-only cold surface; not used by runtime gameplay phases.</remarks>
        public bool TryOpenSimdTuningEditorView(out NativeArray<SimdHydrodynamicTuningDTO> tuning)
        {
            tuning = default;
            IDataVault vault = _dataVault;
            if (vault == null || !HandlesReady(vault))
                return false;

            tuning = ResolveVaultBuffer(vault, in _simdHydrodynamicTuningHandle);
            return tuning.IsCreated && tuning.Length > 0;
        }

        /// <summary>Opens mutable editor views for SHINOBU_249 sleep telemetry and SDF tuning.</summary>
        /// <param name="tuning">Opened buoyancy tuning row.</param>
        /// <param name="telemetry">Opened sleep telemetry ring.</param>
        /// <param name="cursor">Opened sleep telemetry cursor.</param>
        /// <param name="sdfConfig">Opened sleep SDF config row.</param>
        /// <remarks>Editor-only cold surface used by the Physics Sleep State X-Ray window.</remarks>
        public bool TryOpenSleepTelemetryEditorViews(
            out NativeArray<BuoyancyTuningDTO> tuning,
            out NativeArray<SleepStateTelemetryEntry> telemetry,
            out NativeArray<int> cursor,
            out NativeArray<BuoyancySleepSdfConfigDTO> sdfConfig)
        {
            tuning = default;
            telemetry = default;
            cursor = default;
            sdfConfig = default;
            IDataVault vault = _dataVault;
            if (vault == null || !HandlesReady(vault))
                return false;

            tuning = ResolveVaultBuffer(vault, in _tuningHandle);
            telemetry = ResolveVaultBuffer(vault, in _sleepTelemetryRingHandle);
            cursor = ResolveVaultBuffer(vault, in _sleepTelemetryCursorHandle);
            sdfConfig = ResolveVaultBuffer(vault, in _sleepSdfConfigHandle);
            return tuning.IsCreated && tuning.Length > 0 &&
                   telemetry.IsCreated && telemetry.Length > 0 &&
                   cursor.IsCreated && cursor.Length > 0 &&
                   sdfConfig.IsCreated && sdfConfig.Length > 0;
        }
#endif

        private void Awake()
        {
            if (!Application.isPlaying)
                return;

#if UNITY_EDITOR
            if (_activeRuntimeInstance == null)
                _activeRuntimeInstance = this;
#endif

            RefreshColdDependencies();
            EnsureColdBooted();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

#if UNITY_EDITOR
            if (_activeRuntimeInstance == null)
                _activeRuntimeInstance = this;
#endif

            CompletePendingSolverForTeardown();
            RefreshColdDependencies();
            EnsureColdBooted();
            TryRegister();
            TryRegisterOriginShiftListener();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            TryUnregister();
            TryUnregisterOriginShiftListener();
            CompletePendingSolverForTeardown();
            _forcePacketsReadyToDrain = false;
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            if (ReferenceEquals(_activeRuntimeInstance, this))
                _activeRuntimeInstance = null;
#endif
            TryUnregister();
            TryUnregisterOriginShiftListener();
            CompletePendingSolverForTeardown();
            ReleaseVaultHandles(_dataVault);
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (!Application.isPlaying || !math.isfinite(fixedDeltaTime) || fixedDeltaTime <= 0f || _jobScheduled || _forcePacketsReadyToDrain)
                return;

            float safeFixedDeltaTime = math.clamp(fixedDeltaTime, 0.0001f, 0.2f);
            if (!TryPrepareRuntimeVault(out IDataVault vault))
                return;

            if (!TryResolveRuntimeBuffers(
                    vault,
                    out NativeArray<BuoyancyStateDTO> states,
                    out NativeArray<BuoyancyForcePacketDTO> forcePackets,
                    out NativeArray<BuoyancyFlowSampleDTO> flowSamples,
                    out NativeArray<BuoyancyTuningDTO> tuning,
                    out NativeArray<BuoyancyTelemetryEntry> telemetry,
                    out NativeArray<int> telemetryCursor,
                    out NativeArray<SleepStateTelemetryEntry> sleepTelemetry,
                    out NativeArray<int> sleepTelemetryCursor,
                    out NativeArray<sbyte> sleepSdfDensity,
                    out NativeArray<BuoyancySleepSdfConfigDTO> sleepSdfConfig,
                    out NativeArray<BuoyancyMaterialSettlingProfileDTO> materialSettlingProfiles,
                    out NativeArray<BuoyancyDebugForceDTO> debugForces,
                    out NativeArray<BuoyancyCounterDTO> counters))
            {
                return;
            }

            if (!TryLockJobBuffers(vault))
                return;

            BuoyancyTuningDTO tuningDto = tuning[0];
            float quality = ResolveGlobalQualityWeight(ref tuningDto);
            tuningDto.SectorAUP = ResolveCachedSectorAUP();
            tuningDto.ResolvedQualityWeight = quality;
            tuningDto.SimulationTickDelta = safeFixedDeltaTime;
            tuningDto.FrameIndex = _simulationFrame;
            tuning[0] = tuningDto;

            int authoredActiveCount = math.select(_stateCapacity, tuningDto.ActiveStateCount, tuningDto.ActiveStateCount > 0);
            _activeStateCount = math.clamp(authoredActiveCount, 0, states.Length);
            if (_activeStateCount <= 0)
            {
                WriteCompletedSimdUtilizationTelemetry(0f);
                UnlockJobBuffers();
                return;
            }

            if (!PhysicsApplySystem.TryPrepareBuoyancyForcePackets(forcePackets, counters))
            {
                UnlockJobBuffers();
                return;
            }

            NativeArray<WakeRequestSignal>.ReadOnly wakeRequests = SignalBus<WakeRequestSignal>.GetFrameSnapshotArray();
            int wakeRequestCount = wakeRequests.IsCreated ? wakeRequests.Length : 0;
            JobHandle sleepPrepassHandle = default;
            if (wakeRequestCount > 0)
            {
                ProcessBuoyancyWakeTriggersJob wakeJob = new ProcessBuoyancyWakeTriggersJob
                {
                    States = states,
                    WakeRequests = wakeRequests,
                    StateCount = _activeStateCount,
                    WakeRequestCount = wakeRequestCount
                };
                sleepPrepassHandle = wakeJob.Schedule(_activeStateCount, 64);
            }

            int currentPollCadence = ResolveAmbientCurrentPollCadence(quality);
            if ((_simulationFrame % (uint)currentPollCadence) == 0u)
            {
                BuoyancySleepSdfConfigDTO sdfConfig = sleepSdfConfig.Length > 0
                    ? sleepSdfConfig[0]
                    : BuoyancySleepSdfConfigDTO.Default();
                PollAmbientCurrentsJob currentWakeJob = new PollAmbientCurrentsJob
                {
                    States = states,
                    FlowSamples = flowSamples,
                    StateCount = _activeStateCount,
                    FlowSampleCount = flowSamples.Length,
                    StirThresholdSq = sdfConfig.AmbientStirThresholdSq,
                    SimulationFrame = _simulationFrame
                };
                sleepPrepassHandle = currentWakeJob.Schedule(_activeStateCount, 64, sleepPrepassHandle);
            }

            int stride = ResolveEvaluationStride(quality);
            int evaluationOffset = math.select((int)(_simulationFrame % (uint)stride), 0, stride == 1);
            int scheduledEvaluationCount = ResolveScheduledEvaluationCount(_activeStateCount, stride, evaluationOffset);
            _scheduleTimestamp = Stopwatch.GetTimestamp();
            if (scheduledEvaluationCount <= 0)
            {
                ReduceBuoyancyTelemetryJob emptyReduceJob = new ReduceBuoyancyTelemetryJob
                {
                    DebugForces = debugForces,
                    Counters = counters,
                    TelemetryRing = telemetry,
                    TelemetryCursor = telemetryCursor,
                    SleepTelemetryRing = sleepTelemetry,
                    SleepTelemetryCursor = sleepTelemetryCursor,
                    ActiveStateCount = _activeStateCount,
                    WakeRequestCount = wakeRequestCount,
                    SimulationFrame = _simulationFrame,
                    GlobalQualityWeight = quality,
                    SleepEnergyThreshold = tuningDto.SleepSpeedSq,
                    ComputeMicros = 0f
                };
                _pendingHandle = emptyReduceJob.Schedule(sleepPrepassHandle);
                _jobScheduled = true;
                return;
            }

            BuoyancySleepSdfConfigDTO sleepConfig = sleepSdfConfig.Length > 0
                ? sleepSdfConfig[0]
                : BuoyancySleepSdfConfigDTO.Default();
            EvaluateBuoyancyJob evaluateJob = new EvaluateBuoyancyJob
            {
                States = states,
                StateCount = states.Length,
                FlowSamples = flowSamples,
                FlowSampleCount = flowSamples.Length,
                SleepSdfDensity = sleepSdfDensity,
                MaterialSettlingProfiles = materialSettlingProfiles,
                MaterialSettlingProfileCount = materialSettlingProfiles.Length,
                SleepSdfConfig = sleepConfig,
                Tuning = tuningDto,
                DebugForces = debugForces,
                DebugForceCount = debugForces.Length,
                ForcePackets = forcePackets,
                ForcePacketCount = forcePackets.Length,
                ForcePacketWriteEnabled = 1,
                ActiveStateCount = _activeStateCount,
                EvaluationStride = stride,
                EvaluationOffset = evaluationOffset,
                SimulationFrame = _simulationFrame,
                SimulationTickDelta = safeFixedDeltaTime,
                GlobalQualityWeight = BuoyancyDisplacementConstants.AuthoritativeQualityWeight
            };

            JobHandle evaluateHandle = evaluateJob.Schedule(scheduledEvaluationCount, 64, sleepPrepassHandle);
            CompactBuoyancyForcePacketsJob compactForcePacketsJob = new CompactBuoyancyForcePacketsJob
            {
                ForcePackets = forcePackets,
                Counters = counters,
                CandidateCount = scheduledEvaluationCount
            };
            JobHandle compactHandle = compactForcePacketsJob.Schedule(evaluateHandle);
            ReduceBuoyancyTelemetryJob reduceJob = new ReduceBuoyancyTelemetryJob
            {
                DebugForces = debugForces,
                Counters = counters,
                TelemetryRing = telemetry,
                TelemetryCursor = telemetryCursor,
                SleepTelemetryRing = sleepTelemetry,
                SleepTelemetryCursor = sleepTelemetryCursor,
                ActiveStateCount = _activeStateCount,
                WakeRequestCount = wakeRequestCount,
                SimulationFrame = _simulationFrame,
                GlobalQualityWeight = quality,
                SleepEnergyThreshold = tuningDto.SleepSpeedSq,
                ComputeMicros = 0f
            };
            _pendingHandle = reduceJob.Schedule(compactHandle);
            _jobScheduled = true;
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            if (!Application.isPlaying)
                return;

            TryFinalizePendingSolverNoWait();
            if (!_forcePacketsReadyToDrain)
                return;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !HasHandle(in _forcePacketsHandle) ||
                !HasHandle(in _countersHandle) ||
                !HasHandle(in _bodyBindingsHandle))
            {
                _forcePacketsReadyToDrain = false;
                return;
            }

            NativeArray<BuoyancyForcePacketDTO> forcePackets = ResolveVaultBuffer(vault, in _forcePacketsHandle);
            NativeArray<BuoyancyCounterDTO> counters = ResolveVaultBuffer(vault, in _countersHandle);
            NativeArray<BuoyancyBodyBindingDTO> bodyBindings = ResolveVaultBuffer(vault, in _bodyBindingsHandle);
            PhysicsApplySystem.DrainBuoyancyForcePackets(
                forcePackets,
                counters,
                bodyBindings,
                BuoyancyDisplacementConstants.ForceQueueSoftCapacity,
                out _,
                out _);
            _forcePacketsReadyToDrain = false;
        }

        public void LateFrameTick()
        {
            if (!_jobScheduled)
                return;

            TryFinalizePendingSolverNoWait();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.FloatingOriginRuntime)
            {
                RefreshCachedSectorAUP();
                RefreshOriginShiftListenerRegistration();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            CompletePendingSolverForTeardown();
            IDataVault previousVault = (previousService as IDataVault) ?? _dataVault;
            IDataVault currentVault = currentService as IDataVault;
            if (!ReferenceEquals(previousVault, currentVault))
                ReleaseVaultHandles(previousVault);
            _dataVault = currentVault;
            if (!HandlesReady() && currentVault != null && !currentVault.IsAllocationLocked)
                EnsureColdBooted();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _cachedSectorAup = math.select(double3.zero, shiftData.NewTotalOffsetDouble, math.isfinite(shiftData.NewTotalOffsetDouble));
        }

        public bool GenerateMockBuoyantObjects()
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsAllocationLocked || !EnsureVaultBuffers())
                return false;

            NativeArray<BuoyancyStateDTO> states = ResolveVaultBuffer(vault, in _statesHandle);
            NativeArray<BuoyancyDebugForceDTO> debugForces = ResolveVaultBuffer(vault, in _debugForcesHandle);
            NativeArray<BuoyancyTuningDTO> tuning = ResolveVaultBuffer(vault, in _tuningHandle);
            if (!states.IsCreated || !debugForces.IsCreated || !tuning.IsCreated || tuning.Length <= 0)
                return false;

            BuoyancyTuningDTO tuningDto = tuning[0];
            int authoredMockCount = math.select(
                BuoyancyDisplacementConstants.MockObjectCount,
                tuningDto.MockStateCount,
                tuningDto.MockStateCount > 0);
            int mockCount = math.clamp(authoredMockCount, 1, math.min(states.Length, BuoyancyDisplacementConstants.MockObjectCount));
            GenerateMockBuoyantObjectsJob job = new GenerateMockBuoyantObjectsJob
            {
                States = states,
                DebugForces = debugForces,
                StateCount = states.Length,
                DebugForceCount = debugForces.Length,
                ActiveMockCount = mockCount,
                SurfaceAUP = tuningDto.OceanSurfaceAUP,
                SimulationFrame = _simulationFrame
            };
            JobHandle handle = job.Schedule(states.Length, 64);
            // COLD/EDITOR BLOCKING SYNC: emergency mock seeding is a boot/tuner path, not a frame-loop solver fence.
            if (!DispatcherJobFence.TryComplete(ref handle, forceComplete: true))
                return false;

            tuningDto.ActiveStateCount = math.max(tuningDto.ActiveStateCount, mockCount);
            tuningDto.MockStateCount = mockCount;
            tuning[0] = tuningDto;
            _activeStateCount = mockCount;
            return true;
        }

#if UNITY_EDITOR
        // EDITOR/MANUAL BLOCKING SYNC: this benchmark is invoked by the X-Ray window only.
        // It intentionally completes jobs for measured microsecond samples and is never called from FixedTick.
        public bool GenerateMockSimdBenchmark()
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsAllocationLocked || !EnsureVaultBuffers())
                return false;

            NativeArray<SimdFloat3Padded> positions = ResolveVaultBuffer(vault, in _simdLocalPositionsHandle);
            NativeArray<SimdFloat3Padded> velocities = ResolveVaultBuffer(vault, in _simdVelocitiesHandle);
            NativeArray<float> dragCoefficients = ResolveVaultBuffer(vault, in _simdDragCoefficientsHandle);
            NativeArray<SimdFloat3Padded> outputForces = ResolveVaultBuffer(vault, in _simdOutputForcesHandle);
            NativeArray<SimdTelemetryEntry> telemetry = ResolveVaultBuffer(vault, in _simdTelemetryRingHandle);
            NativeArray<int> cursor = ResolveVaultBuffer(vault, in _simdTelemetryCursorHandle);
            NativeArray<SimdHydrodynamicTuningDTO> benchmarkTuning = ResolveVaultBuffer(vault, in _simdHydrodynamicTuningHandle);
            if (!positions.IsCreated ||
                !velocities.IsCreated ||
                !dragCoefficients.IsCreated ||
                !outputForces.IsCreated ||
                !telemetry.IsCreated ||
                !cursor.IsCreated ||
                !benchmarkTuning.IsCreated ||
                benchmarkTuning.Length <= 0)
            {
                return false;
            }

            int count = math.min(
                BuoyancyDisplacementConstants.SimdBenchmarkCapacity,
                math.min(positions.Length, math.min(velocities.Length, math.min(dragCoefficients.Length, outputForces.Length))));
            if (count <= 0)
                return false;
            int laneCount = (count + SimdVectorizationConstants.HydrodynamicsLaneWidth - 1) /
                            SimdVectorizationConstants.HydrodynamicsLaneWidth;

            SimdHydrodynamicTuningDTO tuningValue = PrepareBenchmarkSimdTuning(benchmarkTuning, _simulationFrame);
            float scalarMicros = 0f;
            float scalarProbeWeight = math.saturate(math.select(
                0f,
                tuningValue.ScalarFallbackWeight01,
                math.isfinite(tuningValue.ScalarFallbackWeight01)));

            GenerateMockSimdBenchmarkJob generateJob = new GenerateMockSimdBenchmarkJob
            {
                LocalPositions = positions,
                Velocities = velocities,
                DragCoefficients = dragCoefficients,
                Count = count,
                Seed = 0x2015A11Du,
                FrameIndex = _simulationFrame
            };
            JobHandle handle = generateJob.Schedule(count, 128);
            int scalarProbeCount = math.clamp((int)math.round(count * scalarProbeWeight), 0, count);
            if (scalarProbeCount > 0)
            {
                if (!DispatcherJobFence.TryComplete(ref handle, forceComplete: true))
                    return false;

                long scalarStart = Stopwatch.GetTimestamp();
                ScalarHydrodynamicsReferenceJob scalarJob = new ScalarHydrodynamicsReferenceJob
                {
                    LocalPositions = positions,
                    Velocities = velocities,
                    DragCoefficients = dragCoefficients,
                    OutputForces = outputForces,
                    Tuning = tuningValue,
                    Count = scalarProbeCount
                };
                JobHandle scalarHandle = scalarJob.Schedule();
                if (!DispatcherJobFence.TryComplete(ref scalarHandle, forceComplete: true))
                    return false;

                float scalarScale = count * math.rcp(math.max(1, scalarProbeCount));
                float rawScalarMicros = ResolveElapsedMicros(scalarStart) * scalarScale;
                scalarMicros = rawScalarMicros;

                generateJob.FrameIndex = _simulationFrame;
                handle = generateJob.Schedule(count, 128);
            }

            long start = Stopwatch.GetTimestamp();
            VectorizedHydrodynamicsLane4Job hydroJob = new VectorizedHydrodynamicsLane4Job
            {
                LocalPositions = positions,
                Velocities = velocities,
                DragCoefficients = dragCoefficients,
                OutputForces = outputForces,
                Tuning = tuningValue,
                Count = count
            };
            handle = hydroJob.Schedule(laneCount, 64, handle);
            if (!DispatcherJobFence.TryComplete(ref handle, forceComplete: true))
                return false;

            float vectorMicros = ResolveElapsedMicros(start);
            float effectiveMaxSpeed = math.max(0f, math.select(0f, tuningValue.MaxSpeed, math.isfinite(tuningValue.MaxSpeed)));
            float effectiveMaxSpeedSq = effectiveMaxSpeed * effectiveMaxSpeed;
            RecordSimdTelemetryJob telemetryJob = new RecordSimdTelemetryJob
            {
                TelemetryRing = telemetry,
                TelemetryCursor = cursor,
                FrameIndex = _simulationFrame,
                KernelHash = SimdVectorizationConstants.HydrodynamicsKernelHash,
                EntityCount = count,
                VectorMicros = vectorMicros,
                ScalarMicros = scalarMicros,
                GlobalQualityWeight = tuningValue.GlobalQualityWeight,
                StateHash = (uint)count ^ _simulationFrame ^ SimdVectorizationConstants.HydrodynamicsKernelHash,
                MaxApproximationError = tuningValue.MaxApproximationError,
                MaxSpeedSq = math.select(0f, effectiveMaxSpeedSq, math.isfinite(effectiveMaxSpeedSq))
            };
            JobHandle telemetryHandle = telemetryJob.Schedule();
            if (!DispatcherJobFence.TryComplete(ref telemetryHandle, forceComplete: true))
                return false;

            if (ResolveSimdThroughputDrop(vectorMicros, scalarMicros) > 0.5f ||
                !math.isfinite(vectorMicros) ||
                !math.isfinite(scalarMicros))
            {
                TryDumpSimdTelemetry(telemetry);
            }
            return true;
        }
#endif

        public bool TryLoadMaterialVolumesCsv()
        {
            // COLD TUNING PATH: explicit designer-triggered hydration into Vault scratch; never called by FixedTick.
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsAllocationLocked || !EnsureVaultBuffers())
                return false;

            NativeArray<byte> scratch = ResolveVaultBuffer(vault, in _csvScratchHandle);
            NativeArray<BuoyancyMaterialVolumeDTO> table = ResolveVaultBuffer(vault, in _materialVolumesHandle);
            if (!scratch.IsCreated || scratch.Length <= 0 || !table.IsCreated || table.Length <= 0)
                return false;

            string path = ResolveProjectPath(_csvRelativePath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            int bytesRead = ReadFileIntoNativeScratch(path, scratch);
            if (bytesRead <= 0)
                return false;

            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(
                NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch),
                math.min(bytesRead, scratch.Length));
            return BuoyancyMaterialVolumeCsvParser.TryApply(span, table, out _);
        }

        public bool TryLoadMaterialSettlingProfilesCsv()
        {
            // COLD TUNING PATH: explicit designer-triggered sleep profile hydration; hot jobs read unmanaged rows only.
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsAllocationLocked || !EnsureVaultBuffers())
                return false;

            NativeArray<byte> scratch = ResolveVaultBuffer(vault, in _csvScratchHandle);
            NativeArray<BuoyancyMaterialSettlingProfileDTO> table = ResolveVaultBuffer(vault, in _materialSettlingProfilesHandle);
            if (!scratch.IsCreated || scratch.Length <= 0 || !table.IsCreated || table.Length <= 0)
                return false;

            string path = ResolveProjectPath(_materialSettlingProfilesCsvRelativePath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            int bytesRead = ReadFileIntoNativeScratch(path, scratch);
            if (bytesRead <= 0)
                return false;

            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(
                NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch),
                math.min(bytesRead, scratch.Length));
            return BuoyancyMaterialSettlingProfileCsvParser.TryApply(span, table, out _);
        }

        public bool TryLoadSimdMathTolerancesCsv()
        {
            // COLD TUNING PATH: editor/manual SIMD tolerance hydration; gameplay jobs consume the parsed Vault rows.
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsAllocationLocked || !EnsureVaultBuffers())
                return false;

            NativeArray<byte> scratch = ResolveVaultBuffer(vault, in _csvScratchHandle);
            NativeArray<SimdMathToleranceDTO> tolerances = ResolveVaultBuffer(vault, in _simdMathTolerancesHandle);
            if (!scratch.IsCreated || scratch.Length <= 0 || !tolerances.IsCreated || tolerances.Length <= 0)
                return false;

            string path = ResolveProjectPath(BuoyancyDisplacementConstants.SimdToleranceCsvRelativePath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            int bytesRead = ReadFileIntoNativeScratch(path, scratch);
            if (bytesRead <= 0)
                return false;

            ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(
                NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch),
                math.min(bytesRead, scratch.Length));
            bool parsed = SimdToleranceCsvParser.TryApply(span, tolerances, out int toleranceRows);
            if (parsed)
            {
                NativeArray<SimdHydrodynamicTuningDTO> simdTuning = ResolveVaultBuffer(vault, in _simdHydrodynamicTuningHandle);
                ApplySimdToleranceTuning(tolerances, toleranceRows, simdTuning);
            }

            return parsed;
        }

        private void RefreshColdDependencies()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
        }

        private bool EnsureColdBooted()
        {
            if (_coldBootCompleted)
                return true;

            RefreshColdDependencies();
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsAllocationLocked)
                return false;

            if (!EnsureVaultBuffers())
                return false;

            SeedDefaultTuningIfNeeded();
            InitializeColdBuffersIfNeeded();
            if (_loadCsvOnEnable)
                TryLoadMaterialVolumesCsv();
            if (_loadMaterialSettlingProfilesOnEnable)
                TryLoadMaterialSettlingProfilesCsv();
            if (_loadSimdTolerancesOnEnable)
                TryLoadSimdMathTolerancesCsv();
            if (_seedEmergencyMockObjects && ShouldSeedEmergencyMock())
                GenerateMockBuoyantObjects();

            _coldBootCompleted = true;
            return true;
        }

        private bool ShouldSeedEmergencyMock()
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HasHandle(in _tuningHandle))
                return false;

            NativeArray<BuoyancyTuningDTO> tuning = ResolveVaultBuffer(vault, in _tuningHandle);
            if (!tuning.IsCreated || tuning.Length <= 0)
                return false;

            BuoyancyTuningDTO tuningDto = tuning[0];
            return tuningDto.ActiveStateCount <= 0;
        }

        private bool EnsureVaultBuffers()
        {
            if (_dataVault == null)
                RefreshColdDependencies();
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            int stateCapacity = math.clamp(_stateCapacity, 1, BuoyancyDisplacementConstants.StateCapacity);
            int flowCapacity = math.clamp(_flowSampleCapacity, 0, BuoyancyDisplacementConstants.FlowSampleCapacity);
            return EnsureVaultDescriptor(vault, ref _statesHandle, BuoyancyDisplacementBufferIds.States, stateCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _forcePacketsHandle, BuoyancyDisplacementBufferIds.ForcePackets, BuoyancyDisplacementConstants.ForceQueueSoftCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _flowSamplesHandle, BuoyancyDisplacementBufferIds.FlowSamples, math.max(1, flowCapacity), NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _tuningHandle, BuoyancyDisplacementBufferIds.Tuning, BuoyancyDisplacementConstants.TuningCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _telemetryRingHandle, BuoyancyDisplacementBufferIds.TelemetryRing, BuoyancyDisplacementConstants.TelemetryCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _telemetryCursorHandle, BuoyancyDisplacementBufferIds.TelemetryCursor, 1, NativeArrayOptions.ClearMemory) &&
                   EnsureVaultDescriptor(vault, ref _sleepTelemetryRingHandle, BuoyancyDisplacementBufferIds.SleepTelemetryRing, BuoyancyDisplacementConstants.TelemetryCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _sleepTelemetryCursorHandle, BuoyancyDisplacementBufferIds.SleepTelemetryCursor, 1, NativeArrayOptions.ClearMemory) &&
                   EnsureVaultDescriptor(vault, ref _sleepSdfDensityHandle, BuoyancyDisplacementBufferIds.SleepSdfDensity, BuoyancyDisplacementConstants.SleepSdfCellCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _sleepSdfConfigHandle, BuoyancyDisplacementBufferIds.SleepSdfConfig, 1, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _materialVolumesHandle, BuoyancyDisplacementBufferIds.MaterialVolumes, BuoyancyDisplacementConstants.MaterialVolumeCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _materialSettlingProfilesHandle, BuoyancyDisplacementBufferIds.MaterialSettlingProfiles, BuoyancyDisplacementConstants.MaterialSettlingProfileCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _csvScratchHandle, BuoyancyDisplacementBufferIds.CsvScratch, BuoyancyDisplacementConstants.CsvScratchBytes, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _debugForcesHandle, BuoyancyDisplacementBufferIds.DebugForces, stateCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _countersHandle, BuoyancyDisplacementBufferIds.Counters, BuoyancyDisplacementConstants.CounterCapacity, NativeArrayOptions.ClearMemory) &&
                   EnsureVaultDescriptor(vault, ref _bodyBindingsHandle, BuoyancyDisplacementBufferIds.BodyBindings, stateCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _simdLocalPositionsHandle, BuoyancyDisplacementBufferIds.SimdLocalPositions, BuoyancyDisplacementConstants.SimdBenchmarkCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _simdVelocitiesHandle, BuoyancyDisplacementBufferIds.SimdVelocities, BuoyancyDisplacementConstants.SimdBenchmarkCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _simdDragCoefficientsHandle, BuoyancyDisplacementBufferIds.SimdDragCoefficients, BuoyancyDisplacementConstants.SimdBenchmarkCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _simdOutputForcesHandle, BuoyancyDisplacementBufferIds.SimdOutputForces, BuoyancyDisplacementConstants.SimdBenchmarkCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _simdTelemetryRingHandle, BuoyancyDisplacementBufferIds.SimdTelemetryRing, BuoyancyDisplacementConstants.SimdTelemetryCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _simdTelemetryCursorHandle, BuoyancyDisplacementBufferIds.SimdTelemetryCursor, 1, NativeArrayOptions.ClearMemory) &&
                   EnsureVaultDescriptor(vault, ref _simdMathTolerancesHandle, BuoyancyDisplacementBufferIds.SimdMathTolerances, BuoyancyDisplacementConstants.SimdToleranceCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _simdVisibleIndexMaskHandle, BuoyancyDisplacementBufferIds.SimdVisibleIndexMask, BuoyancyDisplacementConstants.SimdBenchmarkCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _simdVisibleIndicesHandle, BuoyancyDisplacementBufferIds.SimdVisibleIndices, BuoyancyDisplacementConstants.SimdBenchmarkCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _simdVisibleCountHandle, BuoyancyDisplacementBufferIds.SimdVisibleCount, 1, NativeArrayOptions.ClearMemory) &&
                   EnsureVaultDescriptor(vault, ref _simdHydrodynamicTuningHandle, BuoyancyDisplacementBufferIds.SimdHydrodynamicTuning, BuoyancyDisplacementConstants.SimdHydrodynamicTuningCapacity, NativeArrayOptions.ClearMemory) &&
                   HandlesReady(vault);
        }

        private bool HandlesReady()
        {
            IDataVault vault = _dataVault;
            return vault != null && HandlesReady(vault);
        }

        private bool HandlesReady(IDataVault vault)
        {
            return vault != null &&
                   HasHandle(in _statesHandle) &&
                   HasHandle(in _forcePacketsHandle) &&
                   HasHandle(in _flowSamplesHandle) &&
                   HasHandle(in _tuningHandle) &&
                   HasHandle(in _telemetryRingHandle) &&
                   HasHandle(in _telemetryCursorHandle) &&
                   HasHandle(in _sleepTelemetryRingHandle) &&
                   HasHandle(in _sleepTelemetryCursorHandle) &&
                   HasHandle(in _sleepSdfDensityHandle) &&
                   HasHandle(in _sleepSdfConfigHandle) &&
                   HasHandle(in _materialVolumesHandle) &&
                   HasHandle(in _materialSettlingProfilesHandle) &&
                   HasHandle(in _csvScratchHandle) &&
                   HasHandle(in _debugForcesHandle) &&
                   HasHandle(in _countersHandle) &&
                   HasHandle(in _bodyBindingsHandle) &&
                   HasHandle(in _simdLocalPositionsHandle) &&
                   HasHandle(in _simdVelocitiesHandle) &&
                   HasHandle(in _simdDragCoefficientsHandle) &&
                   HasHandle(in _simdOutputForcesHandle) &&
                   HasHandle(in _simdTelemetryRingHandle) &&
                   HasHandle(in _simdTelemetryCursorHandle) &&
                   HasHandle(in _simdMathTolerancesHandle) &&
                   HasHandle(in _simdVisibleIndexMaskHandle) &&
                   HasHandle(in _simdVisibleIndicesHandle) &&
                   HasHandle(in _simdVisibleCountHandle) &&
                   HasHandle(in _simdHydrodynamicTuningHandle) &&
                   BuoyancyDisplacementLayout.Validate() &&
                   SimdVectorizationLayout.Validate();
        }

        private bool TryPrepareRuntimeVault(out IDataVault vault)
        {
            vault = _dataVault;
            if (!_coldBootCompleted || vault == null)
                return false;

            return HandlesReady(vault);
        }

        private static bool EnsureVaultDescriptor<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            if (vault == null || requiredLength <= 0)
                return false;

            if (HasHandle(in handle) &&
                vault.TryResolveHandle(in handle, out NativeArray<T> existing) &&
                existing.IsCreated &&
                existing.Length >= requiredLength)
            {
                return true;
            }

            if (TryAdoptExistingVaultDescriptor(vault, bufferId, requiredLength, ref handle))
                return true;

            if (vault.IsAllocationLocked)
                return false;

            handle = vault.GetGenerationHandle<T>(bufferId, requiredLength, SystemID.Physics, options);
            return HasHandle(in handle) &&
                   vault.TryResolveHandle(in handle, out NativeArray<T> resolved) &&
                   resolved.IsCreated &&
                   resolved.Length >= requiredLength;
        }

        private static bool TryAdoptExistingVaultDescriptor<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault == null || requiredLength <= 0)
                return false;

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existingHandle) &&
                HasHandle(in existingHandle) &&
                vault.TryResolveHandle(in existingHandle, out NativeArray<T> existingBuffer) &&
                existingBuffer.IsCreated &&
                existingBuffer.Length >= requiredLength)
            {
                handle = existingHandle;
                return true;
            }

            return false;
        }

        private static NativeArray<T> ResolveVaultBuffer<T>(IDataVault vault, in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return vault != null &&
                   HasHandle(in handle) &&
                   vault.TryResolveHandle(in handle, out NativeArray<T> buffer)
                ? buffer
                : default;
        }

        private static bool HasHandle<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private void SeedDefaultTuningIfNeeded()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            NativeArray<BuoyancyTuningDTO> tuning = ResolveVaultBuffer(vault, in _tuningHandle);
            if (!tuning.IsCreated || tuning.Length <= 0)
                return;

            BuoyancyTuningDTO value = tuning[0];
            if (!math.isfinite(value.WaterDensityKgPerM3) ||
                !math.isfinite(value.GravityMetersPerSecondSq) ||
                !math.isfinite(value.LinearDragCoefficient) ||
                !math.isfinite(value.QuadraticDragCoefficient) ||
                !math.isfinite(value.SurfaceDampening) ||
                !math.isfinite(value.GlobalQualityWeight) ||
                !math.isfinite(value.ResolvedQualityWeight) ||
                !math.isfinite(value.SimulationTickDelta) ||
                value.WaterDensityKgPerM3 < 900f ||
                value.WaterDensityKgPerM3 > 1160f ||
                value.GravityMetersPerSecondSq <= BuoyancyDisplacementConstants.Epsilon ||
                value.GravityMetersPerSecondSq > 40f ||
                value.LinearDragCoefficient < 0f ||
                value.QuadraticDragCoefficient < 0f ||
                value.SurfaceDampening < 0f ||
                value.SurfaceDampening > 1f ||
                value.GlobalQualityWeight < 0f ||
                value.GlobalQualityWeight > 1f ||
                value.ResolvedQualityWeight < 0f ||
                value.ResolvedQualityWeight > 1f ||
                value.SimulationTickDelta <= 0f ||
                value.SimulationTickDelta > 0.2f ||
                value.ActiveStateCount < 0 ||
                value.ActiveStateCount > BuoyancyDisplacementConstants.StateCapacity ||
                value.MockStateCount < 0 ||
                value.MockStateCount > BuoyancyDisplacementConstants.MockObjectCount ||
                value.MinFluidDensityKgPerM3 <= 0f ||
                value.MaxFluidDensityKgPerM3 <= value.MinFluidDensityKgPerM3)
            {
                tuning[0] = BuoyancyTuningDTO.Default();
            }
        }

        private void InitializeColdBuffersIfNeeded()
        {
            if (_coldBuffersInitialized)
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            NativeArray<BuoyancyFlowSampleDTO> flowSamples = ResolveVaultBuffer(vault, in _flowSamplesHandle);
            NativeArray<BuoyancyTelemetryEntry> telemetry = ResolveVaultBuffer(vault, in _telemetryRingHandle);
            NativeArray<int> telemetryCursor = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
            NativeArray<SleepStateTelemetryEntry> sleepTelemetry = ResolveVaultBuffer(vault, in _sleepTelemetryRingHandle);
            NativeArray<int> sleepTelemetryCursor = ResolveVaultBuffer(vault, in _sleepTelemetryCursorHandle);
            NativeArray<sbyte> sleepSdfDensity = ResolveVaultBuffer(vault, in _sleepSdfDensityHandle);
            NativeArray<BuoyancySleepSdfConfigDTO> sleepSdfConfig = ResolveVaultBuffer(vault, in _sleepSdfConfigHandle);
            NativeArray<BuoyancyMaterialVolumeDTO> materials = ResolveVaultBuffer(vault, in _materialVolumesHandle);
            NativeArray<BuoyancyMaterialSettlingProfileDTO> settlingProfiles = ResolveVaultBuffer(vault, in _materialSettlingProfilesHandle);
            NativeArray<BuoyancyDebugForceDTO> debug = ResolveVaultBuffer(vault, in _debugForcesHandle);
            NativeArray<BuoyancyCounterDTO> counters = ResolveVaultBuffer(vault, in _countersHandle);
            NativeArray<BuoyancyBodyBindingDTO> bindings = ResolveVaultBuffer(vault, in _bodyBindingsHandle);
            if (!flowSamples.IsCreated ||
                !telemetry.IsCreated ||
                !telemetryCursor.IsCreated ||
                !sleepTelemetry.IsCreated ||
                !sleepTelemetryCursor.IsCreated ||
                !sleepSdfDensity.IsCreated ||
                !sleepSdfConfig.IsCreated ||
                !materials.IsCreated ||
                !settlingProfiles.IsCreated ||
                !debug.IsCreated ||
                !counters.IsCreated ||
                !bindings.IsCreated)
            {
                return;
            }

            InitializeBuoyancyColdBuffersJob job = new InitializeBuoyancyColdBuffersJob
            {
                FlowSamples = flowSamples,
                TelemetryRing = telemetry,
                TelemetryCursor = telemetryCursor,
                SleepTelemetryRing = sleepTelemetry,
                SleepTelemetryCursor = sleepTelemetryCursor,
                SleepSdfDensity = sleepSdfDensity,
                SleepSdfConfig = sleepSdfConfig,
                MaterialVolumes = materials,
                MaterialSettlingProfiles = settlingProfiles,
                DebugForces = debug,
                Counters = counters,
                BodyBindings = bindings
            };
            JobHandle handle = job.Schedule();
            // COLD BOOT BLOCKING SYNC: clears Vault-owned buffers once before steady-state scheduling.
            if (!DispatcherJobFence.TryComplete(ref handle, forceComplete: true))
                return;

            _coldBuffersInitialized = true;
        }

        private bool TryResolveRuntimeBuffers(
            IDataVault vault,
            out NativeArray<BuoyancyStateDTO> states,
            out NativeArray<BuoyancyForcePacketDTO> forcePackets,
            out NativeArray<BuoyancyFlowSampleDTO> flowSamples,
            out NativeArray<BuoyancyTuningDTO> tuning,
            out NativeArray<BuoyancyTelemetryEntry> telemetry,
            out NativeArray<int> telemetryCursor,
            out NativeArray<SleepStateTelemetryEntry> sleepTelemetry,
            out NativeArray<int> sleepTelemetryCursor,
            out NativeArray<sbyte> sleepSdfDensity,
            out NativeArray<BuoyancySleepSdfConfigDTO> sleepSdfConfig,
            out NativeArray<BuoyancyMaterialSettlingProfileDTO> materialSettlingProfiles,
            out NativeArray<BuoyancyDebugForceDTO> debugForces,
            out NativeArray<BuoyancyCounterDTO> counters)
        {
            states = default;
            forcePackets = default;
            flowSamples = default;
            tuning = default;
            telemetry = default;
            telemetryCursor = default;
            sleepTelemetry = default;
            sleepTelemetryCursor = default;
            sleepSdfDensity = default;
            sleepSdfConfig = default;
            materialSettlingProfiles = default;
            debugForces = default;
            counters = default;
            if (vault == null)
                return false;

            states = ResolveVaultBuffer(vault, in _statesHandle);
            forcePackets = ResolveVaultBuffer(vault, in _forcePacketsHandle);
            flowSamples = ResolveVaultBuffer(vault, in _flowSamplesHandle);
            tuning = ResolveVaultBuffer(vault, in _tuningHandle);
            telemetry = ResolveVaultBuffer(vault, in _telemetryRingHandle);
            telemetryCursor = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
            sleepTelemetry = ResolveVaultBuffer(vault, in _sleepTelemetryRingHandle);
            sleepTelemetryCursor = ResolveVaultBuffer(vault, in _sleepTelemetryCursorHandle);
            sleepSdfDensity = ResolveVaultBuffer(vault, in _sleepSdfDensityHandle);
            sleepSdfConfig = ResolveVaultBuffer(vault, in _sleepSdfConfigHandle);
            materialSettlingProfiles = ResolveVaultBuffer(vault, in _materialSettlingProfilesHandle);
            debugForces = ResolveVaultBuffer(vault, in _debugForcesHandle);
            counters = ResolveVaultBuffer(vault, in _countersHandle);
            return states.IsCreated &&
                   forcePackets.IsCreated &&
                   flowSamples.IsCreated &&
                   tuning.IsCreated &&
                   telemetry.IsCreated &&
                   telemetryCursor.IsCreated &&
                   sleepTelemetry.IsCreated &&
                   sleepTelemetryCursor.IsCreated &&
                   sleepSdfDensity.IsCreated &&
                   sleepSdfConfig.IsCreated &&
                   materialSettlingProfiles.IsCreated &&
                   debugForces.IsCreated &&
                   counters.IsCreated &&
                   tuning.Length >= 1 &&
                   telemetry.Length >= BuoyancyDisplacementConstants.TelemetryCapacity &&
                   telemetryCursor.Length >= 1 &&
                   sleepTelemetry.Length >= BuoyancyDisplacementConstants.TelemetryCapacity &&
                   sleepTelemetryCursor.Length >= 1 &&
                   sleepSdfConfig.Length >= 1 &&
                   counters.Length >= 1;
        }

        private bool TryFinalizePendingSolverNoWait()
        {
            if (!_jobScheduled)
                return true;

            if (!_pendingHandle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _pendingHandle))
                return false;

            return FinishPendingSolverCompletion();
        }

        private bool CompletePendingSolverForTeardown()
        {
            if (!_jobScheduled)
                return true;

            if (!DispatcherJobFence.TryComplete(ref _pendingHandle, forceComplete: true))
                return false;

            return FinishPendingSolverCompletion();
        }

        private bool FinishPendingSolverCompletion()
        {
            _jobScheduled = false;
            float micros = ResolveElapsedMicros(_scheduleTimestamp);
            WriteCompletedComputeMicros(micros);
            WriteCompletedSimdUtilizationTelemetry(micros);
            bool shouldDumpFault = !_dumpedFault && TryLatestCounterHasFault();
            UnlockJobBuffers();
            if (shouldDumpFault)
            {
                DumpBlackBoxOnce();
                _dumpedFault = true;
            }

            _simulationFrame++;
            _forcePacketsReadyToDrain = true;
            return true;
        }

        private void WriteCompletedComputeMicros(float micros)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HasHandle(in _countersHandle))
                return;

            float safeMicros = math.max(0f, math.select(0f, micros, math.isfinite(micros)));
            NativeArray<BuoyancyCounterDTO> counters = ResolveVaultBuffer(vault, in _countersHandle);
            if (!counters.IsCreated || counters.Length <= 0)
                return;

            BuoyancyCounterDTO counter = counters[0];
            counter.ComputeMicros = safeMicros;
            counters[0] = counter;

            NativeArray<BuoyancyTelemetryEntry> telemetry = ResolveVaultBuffer(vault, in _telemetryRingHandle);
            NativeArray<int> cursor = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
            if (!telemetry.IsCreated || telemetry.Length <= 0 || !cursor.IsCreated || cursor.Length <= 0)
                return;

            int currentCursor = math.clamp(cursor[0], 0, telemetry.Length - 1);
            int slot = (currentCursor + telemetry.Length - 1) % telemetry.Length;
            BuoyancyTelemetryEntry entry = telemetry[slot];
            entry.ComputeMicros = safeMicros;
            telemetry[slot] = entry;

            NativeArray<SleepStateTelemetryEntry> sleepTelemetry = ResolveVaultBuffer(vault, in _sleepTelemetryRingHandle);
            NativeArray<int> sleepCursor = ResolveVaultBuffer(vault, in _sleepTelemetryCursorHandle);
            if (!sleepTelemetry.IsCreated || sleepTelemetry.Length <= 0 || !sleepCursor.IsCreated || sleepCursor.Length <= 0)
                return;

            int sleepCurrentCursor = math.clamp(sleepCursor[0], 0, sleepTelemetry.Length - 1);
            int sleepSlot = (sleepCurrentCursor + sleepTelemetry.Length - 1) % sleepTelemetry.Length;
            SleepStateTelemetryEntry sleepEntry = sleepTelemetry[sleepSlot];
            sleepEntry.ComputeMicros = safeMicros;
            sleepTelemetry[sleepSlot] = sleepEntry;
        }

        private void WriteCompletedSimdUtilizationTelemetry(float micros)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !HasHandle(in _simdTelemetryRingHandle) ||
                !HasHandle(in _simdTelemetryCursorHandle))
            {
                return;
            }

            if (!vault.TryLockBuffer(BuoyancyDisplacementBufferIds.SimdTelemetryRing, SystemID.Physics))
                return;

            bool cursorLocked = vault.TryLockBuffer(BuoyancyDisplacementBufferIds.SimdTelemetryCursor, SystemID.Physics);
            if (!cursorLocked)
            {
                vault.TryUnlockBuffer(BuoyancyDisplacementBufferIds.SimdTelemetryRing, SystemID.Physics);
                return;
            }

            try
            {
                NativeArray<SimdTelemetryEntry> telemetry = ResolveVaultBuffer(vault, in _simdTelemetryRingHandle);
                NativeArray<int> cursor = ResolveVaultBuffer(vault, in _simdTelemetryCursorHandle);
                if (!telemetry.IsCreated || telemetry.Length <= 0 || !cursor.IsCreated || cursor.Length <= 0)
                    return;

                NativeArray<SimdHydrodynamicTuningDTO> tuning = ResolveVaultBuffer(vault, in _simdHydrodynamicTuningHandle);
                SimdHydrodynamicTuningDTO tuningValue = default;
                if (tuning.IsCreated && tuning.Length > 0)
                    tuningValue = tuning[0];

                float safeMicros = math.max(0f, math.select(0f, micros, math.isfinite(micros)));
                float vectorMs = math.max(0.0001f, safeMicros * 0.001f);
                float throughput = math.max(0, _activeStateCount) * math.rcp(vectorMs);
                float quality = ResolveGlobalQualityWeightFromHomeostasis();
                float maxError = math.max(0f, math.select(0.01f, tuningValue.MaxApproximationError, math.isfinite(tuningValue.MaxApproximationError)));
                float maxSpeed = math.max(0f, math.select(0f, tuningValue.MaxSpeed, math.isfinite(tuningValue.MaxSpeed)));
                float maxSpeedSq = maxSpeed * maxSpeed;
                int currentCursor = math.max(0, cursor[0]);
                int previousSlot = (currentCursor + telemetry.Length - 1) % telemetry.Length;
                SimdTelemetryEntry previousEntry = telemetry[previousSlot];
                bool previousSameKernel = previousEntry.KernelHash == SimdVectorizationConstants.HydrodynamicsKernelHash;
                float previousScalarMicros = math.max(
                    0f,
                    math.select(0f, previousEntry.ScalarMicros, previousSameKernel & math.isfinite(previousEntry.ScalarMicros)));
                float throughputDrop = ResolveSimdThroughputDrop(safeMicros, previousScalarMicros);
                bool nonFinite = !math.isfinite(micros) |
                                 !math.isfinite(throughput) |
                                 !math.isfinite(maxError) |
                                 !math.isfinite(maxSpeedSq) |
                                 !math.isfinite(throughputDrop);

                int slot = currentCursor % telemetry.Length;
                SimdTelemetryEntry entry = default;
                entry.FrameIndex = _simulationFrame;
                entry.KernelHash = SimdVectorizationConstants.HydrodynamicsKernelHash;
                entry.EntityCount = math.max(0, _activeStateCount);
                entry.VectorMicros = safeMicros;
                entry.ScalarMicros = previousScalarMicros;
                entry.EntitiesPerMillisecond = math.select(0f, throughput, math.isfinite(throughput));
                entry.ThroughputDrop01 = throughputDrop;
                entry.GlobalQualityWeight = quality;
                entry.Flags = math.select(0u, SimdVectorizationConstants.FlagNonFinite, nonFinite);
                entry.LastStateHash = (_simulationFrame * 747796405u) ^
                                      (uint)math.max(0, _activeStateCount) ^
                                      SimdVectorizationConstants.HydrodynamicsKernelHash;
                entry.MaxError = maxError;
                entry.MaxSpeedSq = math.select(0f, maxSpeedSq, math.isfinite(maxSpeedSq));
                telemetry[slot] = entry;
                int nextCursor = slot + 1;
                cursor[0] = math.select(nextCursor, 0, nextCursor >= telemetry.Length);

                if ((entry.Flags & SimdVectorizationConstants.FlagNonFinite) != 0u || throughputDrop > 0.5f)
                    TryDumpSimdTelemetry(telemetry);
            }
            finally
            {
                vault.TryUnlockBuffer(BuoyancyDisplacementBufferIds.SimdTelemetryCursor, SystemID.Physics);
                vault.TryUnlockBuffer(BuoyancyDisplacementBufferIds.SimdTelemetryRing, SystemID.Physics);
            }
        }

        private bool TryLatestCounterHasFault()
        {
            IDataVault vault = _dataVault;
            if (vault == null || !HasHandle(in _countersHandle))
                return false;

            NativeArray<BuoyancyCounterDTO> counters = ResolveVaultBuffer(vault, in _countersHandle);
            if (!counters.IsCreated || counters.Length <= 0)
                return false;

            return (counters[0].Flags & BuoyancyDisplacementConstants.FlagNonFinite) != 0u ||
                   counters[0].NonFiniteCount > 0;
        }

        private bool TryLockJobBuffers(IDataVault vault)
        {
            _lockedBuffers = 0;
            return TryLock(vault, BuoyancyDisplacementBufferIds.States, LockStates) &&
                   TryLock(vault, BuoyancyDisplacementBufferIds.ForcePackets, LockForcePackets) &&
                   TryLock(vault, BuoyancyDisplacementBufferIds.FlowSamples, LockFlowSamples) &&
                   TryLock(vault, BuoyancyDisplacementBufferIds.Tuning, LockTuning) &&
                   TryLock(vault, BuoyancyDisplacementBufferIds.TelemetryRing, LockTelemetry) &&
                   TryLock(vault, BuoyancyDisplacementBufferIds.TelemetryCursor, LockTelemetryCursor) &&
                   TryLock(vault, BuoyancyDisplacementBufferIds.SleepTelemetryRing, LockSleepTelemetry) &&
                   TryLock(vault, BuoyancyDisplacementBufferIds.SleepTelemetryCursor, LockSleepTelemetryCursor) &&
                   TryLock(vault, BuoyancyDisplacementBufferIds.SleepSdfDensity, LockSleepSdfDensity) &&
                   TryLock(vault, BuoyancyDisplacementBufferIds.SleepSdfConfig, LockSleepSdfConfig) &&
                   TryLock(vault, BuoyancyDisplacementBufferIds.MaterialSettlingProfiles, LockMaterialSettlingProfiles) &&
                   TryLock(vault, BuoyancyDisplacementBufferIds.DebugForces, LockDebugForces) &&
                   TryLock(vault, BuoyancyDisplacementBufferIds.Counters, LockCounters);
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

        private void UnlockJobBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null || _lockedBuffers == 0)
            {
                _lockedBuffers = 0;
                return;
            }

            Unlock(vault, BuoyancyDisplacementBufferIds.States, LockStates);
            Unlock(vault, BuoyancyDisplacementBufferIds.ForcePackets, LockForcePackets);
            Unlock(vault, BuoyancyDisplacementBufferIds.FlowSamples, LockFlowSamples);
            Unlock(vault, BuoyancyDisplacementBufferIds.Tuning, LockTuning);
            Unlock(vault, BuoyancyDisplacementBufferIds.TelemetryRing, LockTelemetry);
            Unlock(vault, BuoyancyDisplacementBufferIds.TelemetryCursor, LockTelemetryCursor);
            Unlock(vault, BuoyancyDisplacementBufferIds.SleepTelemetryRing, LockSleepTelemetry);
            Unlock(vault, BuoyancyDisplacementBufferIds.SleepTelemetryCursor, LockSleepTelemetryCursor);
            Unlock(vault, BuoyancyDisplacementBufferIds.SleepSdfDensity, LockSleepSdfDensity);
            Unlock(vault, BuoyancyDisplacementBufferIds.SleepSdfConfig, LockSleepSdfConfig);
            Unlock(vault, BuoyancyDisplacementBufferIds.MaterialSettlingProfiles, LockMaterialSettlingProfiles);
            Unlock(vault, BuoyancyDisplacementBufferIds.DebugForces, LockDebugForces);
            Unlock(vault, BuoyancyDisplacementBufferIds.Counters, LockCounters);
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
            if (!Application.isPlaying)
                return;

            if (!_registeredHotSwap)
            {
                GlobalRegistry.RegisterHotSwapListener(this);
                _registeredHotSwap = true;
            }

            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredFixed)
                _registeredFixed = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            if (!_registeredPostFixed)
                _registeredPostFixed = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Environment);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryRegisterOriginShiftListener()
        {
            if (!Application.isPlaying)
                return;

            RefreshCachedSectorAUP();
            RefreshOriginShiftListenerRegistration();
        }

        private void RefreshOriginShiftListenerRegistration()
        {
            if (!Application.isPlaying)
                return;

            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
            if (_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void TryUnregister()
        {
            if (_registeredPostFixed)
            {
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
                _registeredPostFixed = false;
            }

            if (_registeredFixed)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
                _registeredFixed = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            if (_registeredHotSwap)
            {
                GlobalRegistry.UnregisterHotSwapListener(this);
                _registeredHotSwap = false;
            }
        }

        private void TryUnregisterOriginShiftListener()
        {
            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
            if (!_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            if (vault != null)
            {
                ReleaseVaultHandle(vault, ref _statesHandle);
                ReleaseVaultHandle(vault, ref _forcePacketsHandle);
                ReleaseVaultHandle(vault, ref _flowSamplesHandle);
                ReleaseVaultHandle(vault, ref _tuningHandle);
                ReleaseVaultHandle(vault, ref _telemetryRingHandle);
                ReleaseVaultHandle(vault, ref _telemetryCursorHandle);
                ReleaseVaultHandle(vault, ref _sleepTelemetryRingHandle);
                ReleaseVaultHandle(vault, ref _sleepTelemetryCursorHandle);
                ReleaseVaultHandle(vault, ref _sleepSdfDensityHandle);
                ReleaseVaultHandle(vault, ref _sleepSdfConfigHandle);
                ReleaseVaultHandle(vault, ref _materialVolumesHandle);
                ReleaseVaultHandle(vault, ref _materialSettlingProfilesHandle);
                ReleaseVaultHandle(vault, ref _csvScratchHandle);
                ReleaseVaultHandle(vault, ref _debugForcesHandle);
                ReleaseVaultHandle(vault, ref _countersHandle);
                ReleaseVaultHandle(vault, ref _bodyBindingsHandle);
                ReleaseVaultHandle(vault, ref _simdLocalPositionsHandle);
                ReleaseVaultHandle(vault, ref _simdVelocitiesHandle);
                ReleaseVaultHandle(vault, ref _simdDragCoefficientsHandle);
                ReleaseVaultHandle(vault, ref _simdOutputForcesHandle);
                ReleaseVaultHandle(vault, ref _simdTelemetryRingHandle);
                ReleaseVaultHandle(vault, ref _simdTelemetryCursorHandle);
                ReleaseVaultHandle(vault, ref _simdMathTolerancesHandle);
                ReleaseVaultHandle(vault, ref _simdVisibleIndexMaskHandle);
                ReleaseVaultHandle(vault, ref _simdVisibleIndicesHandle);
                ReleaseVaultHandle(vault, ref _simdVisibleCountHandle);
                ReleaseVaultHandle(vault, ref _simdHydrodynamicTuningHandle);
            }

            ClearHandles();
            if (ReferenceEquals(vault, _dataVault))
                _dataVault = null;
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null && HasHandle(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void ClearHandles()
        {
            _statesHandle = default;
            _forcePacketsHandle = default;
            _flowSamplesHandle = default;
            _tuningHandle = default;
            _telemetryRingHandle = default;
            _telemetryCursorHandle = default;
            _sleepTelemetryRingHandle = default;
            _sleepTelemetryCursorHandle = default;
            _sleepSdfDensityHandle = default;
            _sleepSdfConfigHandle = default;
            _materialVolumesHandle = default;
            _materialSettlingProfilesHandle = default;
            _csvScratchHandle = default;
            _debugForcesHandle = default;
            _countersHandle = default;
            _bodyBindingsHandle = default;
            _simdLocalPositionsHandle = default;
            _simdVelocitiesHandle = default;
            _simdDragCoefficientsHandle = default;
            _simdOutputForcesHandle = default;
            _simdTelemetryRingHandle = default;
            _simdTelemetryCursorHandle = default;
            _simdMathTolerancesHandle = default;
            _simdVisibleIndexMaskHandle = default;
            _simdVisibleIndicesHandle = default;
            _simdVisibleCountHandle = default;
            _simdHydrodynamicTuningHandle = default;
            _lockedBuffers = 0;
            _coldBuffersInitialized = false;
            _coldBootCompleted = false;
            _forcePacketsReadyToDrain = false;
        }

        private static int ResolveEvaluationStride(float quality)
        {
            return 1;
        }

        private static int ResolveAmbientCurrentPollCadence(float quality)
        {
            return 8;
        }

        private static int ResolveScheduledEvaluationCount(int activeCount, int stride, int offset)
        {
            int safeActive = math.max(0, activeCount);
            int safeStride = math.max(1, stride);
            int safeOffset = math.clamp(offset, 0, safeStride - 1);
            int numerator = math.max(0, safeActive - safeOffset);
            return (numerator + safeStride - 1) / safeStride;
        }

        private static float ResolveGlobalQualityWeight(ref BuoyancyTuningDTO tuning)
        {
            float homeostasis = ResolveGlobalQualityWeightFromHomeostasis();
            float tuningQuality = math.select(1f, tuning.GlobalQualityWeight, math.isfinite(tuning.GlobalQualityWeight));
            return math.saturate(math.min(homeostasis, math.saturate(tuningQuality)));
        }

        private static float ResolveGlobalQualityWeightFromHomeostasis()
        {
            float homeostasis = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, homeostasis, math.isfinite(homeostasis)));
        }

        private static SimdHydrodynamicTuningDTO PrepareBenchmarkSimdTuning(
            NativeArray<SimdHydrodynamicTuningDTO> tuning,
            uint frameIndex)
        {
            SimdHydrodynamicTuningDTO value = tuning[0];
            float scalarWeight = math.saturate(math.select(0f, value.ScalarFallbackWeight01, math.isfinite(value.ScalarFallbackWeight01)));
            value.DeltaTime = math.select(1f / 60f, value.DeltaTime, math.isfinite(value.DeltaTime) & value.DeltaTime > 0f);
            value.GlobalQualityWeight = ResolveGlobalQualityWeightFromHomeostasis();
            value.BaseLinearDrag = math.select(0.02f, value.BaseLinearDrag, math.isfinite(value.BaseLinearDrag) & value.BaseLinearDrag >= 0f);
            value.BuoyancyAccelerationY = math.select(0.15f, value.BuoyancyAccelerationY, math.isfinite(value.BuoyancyAccelerationY));
            value.BaseFlowVelocity = math.select(new float3(0.04f, 0f, -0.03f), value.BaseFlowVelocity, math.isfinite(value.BaseFlowVelocity));
            value.TurbulenceAmplitude = math.select(0.35f, value.TurbulenceAmplitude, math.isfinite(value.TurbulenceAmplitude) & value.TurbulenceAmplitude >= 0f);
            value.MaxSpeed = math.select(12f, value.MaxSpeed, math.isfinite(value.MaxSpeed) & value.MaxSpeed > 0f);
            value.FrameIndex = frameIndex;
            value.Flags = SimdVectorizationConstants.FlagActive;
            value.ScalarFallbackWeight01 = scalarWeight;
            bool hasApproximationWeight = math.isfinite(value.ApproximationQualityWeight) &
                                          value.ApproximationQualityWeight > BuoyancyDisplacementConstants.Epsilon;
            value.ApproximationQualityWeight = math.saturate(math.select(value.GlobalQualityWeight, value.ApproximationQualityWeight, hasApproximationWeight));
            value.MaxApproximationError = math.select(0.01f, value.MaxApproximationError, math.isfinite(value.MaxApproximationError) & value.MaxApproximationError >= 0f);
            value.SinPolynomialDegree = math.select(7, math.clamp(value.SinPolynomialDegree, 3, 7), value.SinPolynomialDegree > 0);
            tuning[0] = value;
            return value;
        }

        private static void ApplySimdToleranceTuning(
            NativeArray<SimdMathToleranceDTO> tolerances,
            int toleranceRows,
            NativeArray<SimdHydrodynamicTuningDTO> tuning)
        {
            if (!tolerances.IsCreated || tolerances.Length <= 0 || toleranceRows <= 0 || !tuning.IsCreated || tuning.Length <= 0)
                return;

            SimdHydrodynamicTuningDTO value = tuning[0];
            int degree = math.select(7, math.clamp(value.SinPolynomialDegree, 3, 7), value.SinPolynomialDegree > 0);
            float maxError = math.select(0.01f, value.MaxApproximationError, math.isfinite(value.MaxApproximationError) & value.MaxApproximationError >= 0f);
            int rows = math.min(toleranceRows, tolerances.Length);
            for (int i = 0; i < rows; i++)
            {
                SimdMathToleranceDTO row = tolerances[i];
                bool appliesToSine = row.FormulaHash == SimdVectorizationConstants.SinPolynomialFormulaHash ||
                                     row.FormulaHash == SimdVectorizationConstants.HydrodynamicTurbulenceFormulaHash;
                bool rowErrorFinite = math.isfinite(row.MaxError);
                float rowMaxError = math.max(0f, math.select(0f, row.MaxError, rowErrorFinite));
                bool applyRow = ((row.Flags & SimdVectorizationConstants.FlagActive) != 0u) &
                                appliesToSine &
                                rowErrorFinite;
                degree = math.select(degree, math.clamp(row.PolynomialDegree, 3, 7), applyRow);
                maxError = math.select(maxError, rowMaxError, applyRow);
            }

            value.SinPolynomialDegree = degree;
            value.MaxApproximationError = maxError;
            bool hasApproximationWeight = math.isfinite(value.ApproximationQualityWeight) &
                                          value.ApproximationQualityWeight > BuoyancyDisplacementConstants.Epsilon;
            value.ApproximationQualityWeight = math.saturate(math.select(value.GlobalQualityWeight, value.ApproximationQualityWeight, hasApproximationWeight));
            value.Flags = SimdVectorizationConstants.FlagActive;
            tuning[0] = value;
        }

        private double3 ResolveCachedSectorAUP()
        {
            return math.select(double3.zero, _cachedSectorAup, math.isfinite(_cachedSectorAup));
        }

        private void RefreshCachedSectorAUP()
        {
            _cachedSectorAup = ResolveSectorAUPFromOrigin();
        }

        private static double3 ResolveSectorAUPFromOrigin()
        {
            double3 sectorAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            return math.select(double3.zero, sectorAup, math.isfinite(sectorAup));
        }

        private static float ResolveElapsedMicros(long scheduleTimestamp)
        {
            if (scheduleTimestamp <= 0L)
                return 0f;

            long elapsed = Stopwatch.GetTimestamp() - scheduleTimestamp;
            if (elapsed <= 0L)
                return 0f;

            long frequency = Stopwatch.Frequency;
            if (frequency <= 0L)
                return 0f;

            double seconds = elapsed / (double)frequency;
            double micros = Math.Min(seconds * 1000000.0, float.MaxValue);
            float value = (float)micros;
            return math.max(0f, math.select(0f, value, math.isfinite(value)));
        }

        private static string ResolveProjectPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return null;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, relativePath));
        }

        private static int ReadFileIntoNativeScratch(string path, NativeArray<byte> scratch)
        {
            // COLD IO ONLY: caller-owned Vault scratch receives bytes for zero-GC Span parsers after the stream closes.
            if (!scratch.IsCreated || scratch.Length <= 0)
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

        private void DumpBlackBoxOnce()
        {
            // FAULT PATH ONLY: writes postmortem telemetry after non-finite/counter fault detection.
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            NativeArray<BuoyancyTelemetryEntry> telemetry = ResolveVaultBuffer(vault, in _telemetryRingHandle);
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            TryWriteTelemetryDump(projectRoot, BuoyancyDisplacementConstants.DumpRelativePath, telemetry);
            TryWriteTelemetryDump(projectRoot, BuoyancyDisplacementConstants.AgentDumpRelativePath, telemetry);

            NativeArray<SleepStateTelemetryEntry> sleepTelemetry = ResolveVaultBuffer(vault, in _sleepTelemetryRingHandle);
            TryWriteSleepTelemetryDump(projectRoot, BuoyancyDisplacementConstants.SleepStateDumpRelativePath, sleepTelemetry);
        }

        private static void TryWriteTelemetryDump(string projectRoot, string relativePath, NativeArray<BuoyancyTelemetryEntry> telemetry)
        {
            if (string.IsNullOrEmpty(projectRoot) || string.IsNullOrEmpty(relativePath) || !telemetry.IsCreated || telemetry.Length <= 0)
                return;

            string dumpPath = Path.Combine(projectRoot, relativePath);
            string directory = Path.GetDirectoryName(dumpPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            try
            {
                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    int bytes = telemetry.Length * UnsafeUtility.SizeOf<BuoyancyTelemetryEntry>();
                    byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                    ReadOnlySpan<byte> source = new ReadOnlySpan<byte>(ptr, bytes);
                    stream.Write(source);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void TryWriteSleepTelemetryDump(string projectRoot, string relativePath, NativeArray<SleepStateTelemetryEntry> telemetry)
        {
            if (string.IsNullOrEmpty(projectRoot) || string.IsNullOrEmpty(relativePath) || !telemetry.IsCreated || telemetry.Length <= 0)
                return;

            string dumpPath = Path.Combine(projectRoot, relativePath);
            string directory = Path.GetDirectoryName(dumpPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            try
            {
                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    int bytes = telemetry.Length * UnsafeUtility.SizeOf<SleepStateTelemetryEntry>();
                    byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                    ReadOnlySpan<byte> source = new ReadOnlySpan<byte>(ptr, bytes);
                    stream.Write(source);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static float ResolveSimdThroughputDrop(float vectorMicros, float scalarMicros)
        {
            float safeVectorMicros = math.max(0.0001f, math.select(0.0001f, vectorMicros, math.isfinite(vectorMicros)));
            float safeScalarMicros = math.max(0f, math.select(0f, scalarMicros, math.isfinite(scalarMicros)));
            float drop = math.saturate(1f - (safeScalarMicros * math.rcp(safeVectorMicros)));
            return math.select(0f, drop, (safeScalarMicros > 0.0001f) & math.isfinite(drop));
        }

        private static unsafe void TryDumpSimdTelemetry(NativeArray<SimdTelemetryEntry> telemetry)
        {
            // FAULT/BENCHMARK PATH ONLY: SIMD X-Ray dump is outside the steady-state solver cadence.
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dumpPath = Path.Combine(projectRoot, SimdVectorizationConstants.SimdAgentDumpRelativePath);
            string directory = Path.GetDirectoryName(dumpPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            try
            {
                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    int bytes = telemetry.Length * UnsafeUtility.SizeOf<SimdTelemetryEntry>();
                    byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                    ReadOnlySpan<byte> source = new ReadOnlySpan<byte>(ptr, bytes);
                    stream.Write(source);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

#if UNITY_EDITOR
        private const string SimdLocalPositionsName = "SHINOBU SIMD local-pos";
        private const string SimdVelocitiesName = "SHINOBU SIMD velocity";
        private const string SimdOutputForcesName = "SHINOBU SIMD force-out";
        private const string SimdDragCoefficientsName = "SHINOBU SIMD drag";

        private readonly char[] _simdGizmoLabelBuffer = new char[160];
        private readonly UnityEngine.GUIContent _simdLocalPositionsLabel =
            new UnityEngine.GUIContent(SimdLocalPositionsName);
        private readonly UnityEngine.GUIContent _simdVelocitiesLabel =
            new UnityEngine.GUIContent(SimdVelocitiesName);
        private readonly UnityEngine.GUIContent _simdOutputForcesLabel =
            new UnityEngine.GUIContent(SimdOutputForcesName);
        private readonly UnityEngine.GUIContent _simdDragCoefficientsLabel =
            new UnityEngine.GUIContent(SimdDragCoefficientsName);
        private int _simdLocalPositionsLabelHash = int.MinValue;
        private int _simdVelocitiesLabelHash = int.MinValue;
        private int _simdOutputForcesLabelHash = int.MinValue;
        private int _simdDragCoefficientsLabelHash = int.MinValue;

        private static readonly UnityEngine.GUIContent SimdAlignmentFaultLabel =
            new UnityEngine.GUIContent("SHINOBU SIMD ALIGNMENT FAULT - ARM64 NEON unsafe");

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || _dataVault == null)
                return;

            DrawSimdAlignmentGizmos();
            DrawSleepStateGizmos();
            if (!HasHandle(in _debugForcesHandle))
                return;

            NativeArray<BuoyancyDebugForceDTO> debugForces = ResolveVaultBuffer(_dataVault, in _debugForcesHandle);
            if (!debugForces.IsCreated)
                return;

            int count = math.min(math.max(0, _activeStateCount), debugForces.Length);
            double3 committedOffset = ResolveCachedSectorAUP();
            for (int i = 0; i < count; i++)
            {
                BuoyancyDebugForceDTO debug = debugForces[i];
                if ((debug.Flags & BuoyancyDisplacementConstants.FlagActive) == 0u || debug.EntityHashID == 0u)
                    continue;

                Vector3 origin = HectonFloatingOrigin.ToRuntimePosition(debug.CurrentAUP, committedOffset);
                DrawVector(origin, debug.BuoyantForce, Color.blue, 0.0025f);
                DrawVector(origin, debug.GravityForce, Color.red, 0.0025f);
                DrawVector(origin, debug.DragForce, Color.green, 0.01f);
            }
        }

        private void DrawSleepStateGizmos()
        {
            if (!HasHandle(in _statesHandle))
                return;

            NativeArray<BuoyancyStateDTO> states = ResolveVaultBuffer(_dataVault, in _statesHandle);
            if (!states.IsCreated)
                return;

            int count = math.min(math.max(0, _activeStateCount), states.Length);
            double3 committedOffset = ResolveCachedSectorAUP();
            for (int i = 0; i < count; i++)
            {
                BuoyancyStateDTO state = states[i];
                if ((state.Flags & BuoyancyDisplacementConstants.FlagActive) == 0u || state.EntityHashID == 0u)
                    continue;

                bool sleeping = (state.Flags & BuoyancyDisplacementConstants.FlagSleeping) != 0u;
                Gizmos.color = sleeping ? new Color(0.02f, 0.08f, 0.32f, 0.92f) : new Color(0.05f, 0.9f, 0.25f, 0.85f);
                Vector3 origin = HectonFloatingOrigin.ToRuntimePosition(state.CurrentAUP, committedOffset);
                float size = math.clamp(EstimateGizmoSize(state.VolumeCubicMeters), 0.12f, 1.25f);
                Gizmos.DrawWireCube(origin, new Vector3(size, size, size));
            }
        }

        private static float EstimateGizmoSize(float volume)
        {
            float safeVolume = math.max(BuoyancyDisplacementConstants.Epsilon, math.select(BuoyancyDisplacementConstants.Epsilon, volume, math.isfinite(volume)));
            float size = safeVolume * math.rsqrt(math.max(safeVolume, BuoyancyDisplacementConstants.Epsilon));
            return math.max(0.12f, size);
        }

        private static void DrawVector(Vector3 origin, float3 vector, Color color, float scale)
        {
            if (!math.all(math.isfinite(vector)))
                return;

            Gizmos.color = color;
            Vector3 delta = new Vector3(vector.x, vector.y, vector.z) * scale;
            Gizmos.DrawLine(origin, origin + delta);
        }

        private void DrawSimdAlignmentGizmos()
        {
            if (!HasHandle(in _simdLocalPositionsHandle) ||
                !HasHandle(in _simdVelocitiesHandle) ||
                !HasHandle(in _simdOutputForcesHandle) ||
                !HasHandle(in _simdDragCoefficientsHandle))
            {
                return;
            }

            Vector3 origin = transform.position + Vector3.up * 1.25f;
            bool localOk = DrawSimdLaneBar(
                ResolveVaultBuffer(_dataVault, in _simdLocalPositionsHandle),
                origin + Vector3.right * -0.75f,
                0.16f,
                _simdLocalPositionsLabel,
                SimdLocalPositionsName,
                ref _simdLocalPositionsLabelHash);
            bool velocityOk = DrawSimdLaneBar(
                ResolveVaultBuffer(_dataVault, in _simdVelocitiesHandle),
                origin + Vector3.right * -0.25f,
                0.16f,
                _simdVelocitiesLabel,
                SimdVelocitiesName,
                ref _simdVelocitiesLabelHash);
            bool forceOk = DrawSimdLaneBar(
                ResolveVaultBuffer(_dataVault, in _simdOutputForcesHandle),
                origin + Vector3.right * 0.25f,
                0.16f,
                _simdOutputForcesLabel,
                SimdOutputForcesName,
                ref _simdOutputForcesLabelHash);
            bool dragOk = DrawSimdLaneBar(
                ResolveVaultBuffer(_dataVault, in _simdDragCoefficientsHandle),
                origin + Vector3.right * 0.75f,
                0.16f,
                _simdDragCoefficientsLabel,
                SimdDragCoefficientsName,
                ref _simdDragCoefficientsLabelHash);

            if (!(localOk & velocityOk & forceOk & dragOk))
                DrawSimdAlignmentFault(origin);
        }

        private unsafe bool DrawSimdLaneBar<T>(
            NativeArray<T> array,
            Vector3 origin,
            float scale,
            UnityEngine.GUIContent label,
            string labelName,
            ref int labelHash) where T : struct
        {
            if (!array.IsCreated || array.Length <= 0)
                return true;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            bool pointerAligned = (((ulong)ptr) & 15UL) == 0UL;
            int stride = UnsafeUtility.SizeOf<T>();
            bool strideVectorSafe = stride == 4 || (stride & 15) == 0;
            bool ok = pointerAligned && strideVectorSafe;
            UpdateSimdLaneLabel(label, labelName, array.Length, stride, pointerAligned, strideVectorSafe, ok, ref labelHash);
            Gizmos.color = ok ? new Color(0.05f, 0.85f, 0.9f, 0.85f) : new Color(1f, 0.05f, 0.02f, 1f);
            float height = math.saturate(array.Length * (1f / SimdVectorizationConstants.BenchmarkEntityCount)) * 1.5f + 0.1f;
            Gizmos.DrawWireCube(origin + Vector3.up * (height * 0.5f), new Vector3(scale, height, scale));
            UnityEditor.Handles.color = ok ? Color.cyan : Color.red;
            UnityEditor.Handles.Label(origin + Vector3.up * (height + 0.08f), label);
            return ok;
        }

        private void UpdateSimdLaneLabel(
            UnityEngine.GUIContent label,
            string labelName,
            int capacity,
            int stride,
            bool pointerAligned,
            bool strideVectorSafe,
            bool ok,
            ref int labelHash)
        {
            int hash = capacity ^
                       (stride << 9) ^
                       (pointerAligned ? 0x10000 : 0x20000) ^
                       (strideVectorSafe ? 0x40000 : 0x80000) ^
                       (ok ? 0x100000 : 0x200000);
            if (hash == labelHash)
                return;

            int write = 0;
            AppendEditorLiteral(_simdGizmoLabelBuffer, ref write, labelName);
            AppendEditorLiteral(_simdGizmoLabelBuffer, ref write, " | stride ");
            AppendEditorInt(_simdGizmoLabelBuffer, ref write, stride);
            AppendEditorLiteral(_simdGizmoLabelBuffer, ref write, " | cap ");
            AppendEditorInt(_simdGizmoLabelBuffer, ref write, capacity);
            AppendEditorLiteral(_simdGizmoLabelBuffer, ref write, " | ptr16 ");
            AppendEditorLiteral(_simdGizmoLabelBuffer, ref write, pointerAligned ? "OK" : "FAIL");
            AppendEditorLiteral(_simdGizmoLabelBuffer, ref write, " | lane ");
            AppendEditorLiteral(_simdGizmoLabelBuffer, ref write, strideVectorSafe ? "OK" : "FAIL");
            label.text = new string(_simdGizmoLabelBuffer, 0, write);
            labelHash = hash;
        }

        private static void AppendEditorLiteral(char[] buffer, ref int offset, string value)
        {
            for (int i = 0; i < value.Length && offset < buffer.Length; i++)
                buffer[offset++] = value[i];
        }

        private static void AppendEditorInt(char[] buffer, ref int offset, int value)
        {
            if (offset >= buffer.Length)
                return;

            if (value == 0)
            {
                buffer[offset++] = '0';
                return;
            }

            int remaining = math.abs(value);
            if (value < 0 && offset < buffer.Length)
                buffer[offset++] = '-';

            int start = offset;
            while (remaining > 0 && offset < buffer.Length)
            {
                buffer[offset++] = (char)('0' + remaining % 10);
                remaining /= 10;
            }

            int end = offset - 1;
            while (start < end)
            {
                char swap = buffer[start];
                buffer[start] = buffer[end];
                buffer[end] = swap;
                start++;
                end--;
            }
        }

        private static void DrawSimdAlignmentFault(Vector3 origin)
        {
            float phase = math.frac((float)UnityEditor.EditorApplication.timeSinceStartup * 4f);
            float flash = math.step(0.5f, phase);
            Gizmos.color = new Color(1f, 0f, 0f, 0.25f + 0.55f * flash);
            Gizmos.DrawWireCube(origin + Vector3.up * 0.9f, new Vector3(2.6f, 2.2f, 2.6f));
            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.Label(origin + Vector3.up * 2.15f, SimdAlignmentFaultLabel);
        }
#endif
    }
}
