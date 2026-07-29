using System;
using System.IO;
using System.Runtime.CompilerServices;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Physiology
{
    /// <summary>
    /// Data-vault backed physiology runtime. The component schedules jobs; biological truth lives in unmanaged vault rows.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed unsafe partial class ShinobuPhysiologyRuntime : MonoBehaviour, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001ShinobuPhysiologyRuntimeSignalPushDropCount;
        private static ShinobuPhysiologyRuntime s_activeRuntime;

        private const SystemID OwnerSystem = SystemID.GameplayPlayer;
#if UNITY_EDITOR
        private const int CsvMaxBytes = 8192;
        private const int CsvOverrideCapacity = 32;
#endif
        private const float KilopascalsPerAtmosphere = 101.325f;
        private const float AuthoritativeQualityWeight = 1f;
        private const double DefaultSeaLevelAupY = 14.02d;
        private const float ToxicityExposureFallbackDeltaScalePerSecond = 0.08f;
        private const float AuthoritativeUpdateIntervalSeconds = 0.1f;
        // Oxygen-critical bridge. Re-publish only after a meaningful further drop so the 32-slot
        // OxygenCriticalSignal lane and the VocalWarningSystem OxygenLow queue are not spammed at tick rate.
        private const float OxygenCriticalRepublishEpsilon = 0.01f;
        // Below this drain rate a time-to-zero projection is meaningless; report the unknown ceiling instead
        // of dividing by ~0 and handing consumers a fake countdown.
        private const float OxygenCriticalMinDrainPerSecond = 0.0001f;
        private const float OxygenCriticalUnknownSecondsRemaining = 3600f;
#if UNITY_EDITOR
        private const string CsvRelativePath = "buhlmann_3tissue_profiles.csv";
        private const string GasCsvRelativePath = "physiological_gas_profiles.csv";
#endif
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_321.bin";
#if UNITY_EDITOR
        private const string LegacyMetabolismFile = "metabolism_rates.h8bin";
        private const string LegacyMValueFile = "haldane_m-values.bin";
#endif
        private const ulong DumpMagic = 0x5348494E4F425532UL; // SHINOBU2
        private const uint DumpVersion = 3u;

        private static readonly uint _BaseO2DrainHash = HashLowerAsciiString("base_o2_drain");
        private static readonly uint _NitrogenUptakeHash = HashLowerAsciiString("nitrogen_uptake_rate");
        private static readonly uint _AdrenalineDecayHash = HashLowerAsciiString("adrenaline_decay");
        private static readonly uint _HypothermiaCoolingHash = HashLowerAsciiString("hypothermia_cooling_rate");
        private static readonly uint _MValueStrictnessHash = HashLowerAsciiString("m_value_strictness");
        private static readonly uint _OffGassingMultiplierHash = HashLowerAsciiString("offgassing_multiplier");
        private static readonly uint _NarcosisThresholdHash = HashLowerAsciiString("narcosis_threshold");
        private static readonly uint _OxygenFractionHash = HashLowerAsciiString("oxygen_fraction");
        private static readonly uint _NitrogenFractionHash = HashLowerAsciiString("nitrogen_fraction");
        private static readonly uint _CarbonDioxideFractionHash = HashLowerAsciiString("carbon_dioxide_fraction");
        private static readonly uint _Fo2Hash = HashLowerAsciiString("fo2");
        private static readonly uint _Fn2Hash = HashLowerAsciiString("fn2");
        private static readonly uint _Fco2Hash = HashLowerAsciiString("fco2");
        private static readonly uint _CnsToxicityRateHash = HashLowerAsciiString("cns_toxicity_rate");
        private static readonly uint _CnsExtremeRateHash = HashLowerAsciiString("cns_extreme_rate");
        private static readonly uint _HypoxiaLimitHash = HashLowerAsciiString("hypoxia_limit");
        private static readonly uint _HypoxiaPpo2Hash = HashLowerAsciiString("hypoxia_ppo2");
        private static readonly uint _AnoxiaLimitHash = HashLowerAsciiString("anoxia_limit");
        private static readonly uint _AnoxiaPpo2Hash = HashLowerAsciiString("anoxia_ppo2");
        private static readonly uint _Co2ToxicityStartHash = HashLowerAsciiString("co2_toxicity_start");
        private static readonly uint _Co2ToxicityFullHash = HashLowerAsciiString("co2_toxicity_full");
        private static readonly uint _SurvivalVitalsQueueDropWarningHash = HashLowerAsciiString("shinobu_survival_vitals_queue_drop");
        private static readonly uint _SurvivalVitalsQueueContextHash = HashLowerAsciiString("shinobu_survival_vitals");
        private static readonly ulong JobMutationGuardMask =
            MutationGuardBit(BufferID.ShinobuPhysiologyVitals) |
            MutationGuardBit(BufferID.ShinobuDecompressionStates) |
            MutationGuardBit(BufferID.ShinobuTissueCompartments) |
            MutationGuardBit(BufferID.ShinobuHaldaneCoefficients) |
            MutationGuardBit(BufferID.ShinobuEnvironmentVitals) |
            MutationGuardBit(BufferID.ShinobuPhysiologyScalars) |
            MutationGuardBit(ShinobuPhysiologyConstants.StatusEffectStatesBuffer) |
            MutationGuardBit(ShinobuPhysiologyConstants.GasPhysiologyStatesBuffer) |
            MutationGuardBit(ShinobuPhysiologyConstants.BreathingGasFractionsBuffer) |
            MutationGuardBit(ShinobuPhysiologyConstants.GasPhysiologyTuningBuffer) |
            MutationGuardBit(BufferID.ShinobuVitalsExport) |
            MutationGuardBit(BufferID.ShinobuPhysiologyTelemetryRing) |
            MutationGuardBit(ShinobuPhysiologyConstants.DecompressionTelemetryRingBuffer) |
            MutationGuardBit(BufferID.ShinobuCardiacPulseStates) |
            MutationGuardBit(BufferID.ShinobuMockToxemiaSignals) |
            MutationGuardBit(BufferID.ShinobuMockPressureSignals) |
            MutationGuardBit(BufferID.ShinobuMockCombatDamageSignals) |
            MutationGuardBit(BufferID.ShinobuMockPredatorAggroSignals) |
            MutationGuardBit(BufferID.ShinobuMockMedicalItemSignals) |
            MutationGuardBit(BufferID.ShinobuPhysiologyTuning);
        private static readonly ulong DefaultTuningMutationGuardMask =
            MutationGuardBit(BufferID.ShinobuPhysiologyTuning) |
            MutationGuardBit(ShinobuPhysiologyConstants.GasPhysiologyTuningBuffer);
        private static readonly ulong EmergencyMetabolismMutationGuardMask =
            MutationGuardBit(BufferID.ShinobuHaldaneCoefficients) |
            MutationGuardBit(BufferID.ShinobuPhysiologyTuning);
        private static readonly ulong DefaultStateMutationGuardMask =
            MutationGuardBit(BufferID.ShinobuPhysiologyVitals) |
            MutationGuardBit(BufferID.ShinobuDecompressionStates) |
            MutationGuardBit(BufferID.ShinobuTissueCompartments) |
            MutationGuardBit(BufferID.ShinobuPhysiologyScalars) |
            MutationGuardBit(ShinobuPhysiologyConstants.StatusEffectStatesBuffer) |
            MutationGuardBit(ShinobuPhysiologyConstants.GasPhysiologyStatesBuffer) |
            MutationGuardBit(ShinobuPhysiologyConstants.BreathingGasFractionsBuffer) |
            MutationGuardBit(BufferID.ShinobuCardiacPulseStates);
#if UNITY_EDITOR
        private static readonly ulong BiologyCsvMutationGuardMask =
            MutationGuardBit(BufferID.ShinobuPhysiologyTuning) |
            MutationGuardBit(BufferID.ShinobuBiologyCsvOverrides) |
            MutationGuardBit(BufferID.ShinobuHaldaneCoefficients) |
            MutationGuardBit(BufferID.ShinobuTissueCompartments);
        private static readonly ulong GasCsvMutationGuardMask =
            MutationGuardBit(ShinobuPhysiologyConstants.GasPhysiologyTuningBuffer);

        // COLD ALLOC: editor CSV import scratch. Never used by Tick/SlowTick/LateFrameTick.
        private static readonly byte[] s_csvScratchCold = new byte[CsvMaxBytes];
        // COLD ALLOC: parsed editor biology override rows before DataVault commit.
        private static readonly BiologyConstantOverrideDTO[] s_csvOverrideScratchCold = new BiologyConstantOverrideDTO[CsvOverrideCapacity];
        // COLD ALLOC: parsed Haldane coefficients before DataVault commit.
        private static readonly HaldaneTissueCoefficientDTO[] s_coefficientScratchCold = new HaldaneTissueCoefficientDTO[ShinobuPhysiologyConstants.TissueCompartmentCount];
        // COLD ALLOC: tissue row deltas derived from coefficient CSV lines before DataVault commit.
        private static readonly TissueCsvOverrideScratch[] s_tissueOverrideScratchCold = new TissueCsvOverrideScratch[ShinobuPhysiologyConstants.TissueCompartmentCount];
        private static int s_csvScratchBusy;

        private struct TissueCsvOverrideScratch
        {
            public float Halftime;
            public float MValue;
            public byte HasOverride;
        }
#endif

        [Header("Runtime Capacity")]
        [Tooltip("Maximum player or humanoid rows simulated by the physiology jobs.")]
        [SerializeField, Min(1)] private int entityCapacity = ShinobuPhysiologyConstants.DefaultEntityCapacity;

        [Header("Vacuum Mock")]
        [Tooltip("Fallback pressure depth used when no player/world pressure data exists.")]
        [SerializeField, Min(0f)] private float mockDepthMeters = 100f;

        [Tooltip("Sea-level Y in AUP meters; depth is computed in double precision before conversion to float.")]
        [SerializeField] private double seaLevelAupY = DefaultSeaLevelAupY;

        [Tooltip("Fallback thermal environment used by the mock pressure lane.")]
        [SerializeField] private float mockAmbientTemperatureCelsius = 2f;

        private VaultGenerationHandle<PhysiologyDTO> _vitalsHandle;
        private VaultGenerationHandle<DecompressionStateDTO> _decompressionHandle;
        private VaultGenerationHandle<TissueCompartmentDTO> _tissueHandle;
        private VaultGenerationHandle<HaldaneTissueCoefficientDTO> _coefficientHandle;
        private VaultGenerationHandle<MockEnvironmentVitalsSignal> _environmentHandle;
        private VaultGenerationHandle<PhysiologyScalarsDTO> _scalarHandle;
        private VaultGenerationHandle<StatusEffectStateDTO> _statusEffectHandle;
        private VaultGenerationHandle<GasPhysiologyStateDTO> _gasStateHandle;
        private VaultGenerationHandle<BreathingGasFractionsDTO> _breathingGasHandle;
        private VaultGenerationHandle<GasPhysiologyTuningDTO> _gasTuningHandle;
        private VaultGenerationHandle<VitalsExportDTO> _exportHandle;
        private VaultGenerationHandle<PhysiologyTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<DecompressionTelemetryEntry> _decompressionTelemetryHandle;
        private VaultGenerationHandle<CardiacPulseStateDTO> _pulseHandle;
        private VaultGenerationHandle<MockToxemiaSignal> _toxemiaHandle;
        private VaultGenerationHandle<MockPressureSignal> _pressureHandle;
        private VaultGenerationHandle<MockCombatDamageSignal> _combatHandle;
        private VaultGenerationHandle<MockPredatorAggroSignal> _predatorHandle;
        private VaultGenerationHandle<MockMedicalItemUsedSignal> _medicalHandle;
        private VaultGenerationHandle<PhysiologyTuningDTO> _tuningHandle;
#if UNITY_EDITOR
        private VaultGenerationHandle<BiologyConstantOverrideDTO> _csvOverrideHandle;
#endif
        private VaultGenerationHandle<DiveProfileSampleDTO> _mockDiveProfileHandle;

        private IDataVault _dataVault;
        private IPlayerRuntimeContext _playerContext;
        private IHectonOceanKinematicsService _oceanKinematics;
        private IGasDynamicsSolver _gasDynamics;
        private JobHandle _activeJobHandle;
#if UNITY_EDITOR
        private string _csvPath;
        private string _gasCsvPath;
#endif
        private string _dumpPath;
        private int _telemetryCursor;
        private int _decompressionTelemetryCursor;
        private int _scheduledCount;
        private uint _simulationFrameCounter;
        private long _jobScheduleTimestamp;
#if UNITY_EDITOR
        private long _csvLastWriteTicks;
        private long _gasCsvLastWriteTicks;
#endif
        private float _simulationAccumulator;
        private float _smoothedGlobalQualityWeight = 1f;
        private bool _registeredSlow;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _jobScheduled;
        private bool _jobLocksHeld;
        private bool _defaultsInitialized;
        private bool _autopsyDumped;
        private bool _playerDepthValid;
        private bool _previousDepthValid;
        private bool _insideHabitat;
        private bool _breathingGasOverrideActive;
        private int _activeHabitatRoomId = -1;
        private int _lastToxicityExposureSnapshotGeneration;
        private float _previousDepthMeters;
        private uint _playerToxicityTargetHash;
        private BreathingGasFractionsDTO _breathingGasOverride;
        private bool _oxygenCriticalLatched;
        private float _lastPublishedOxygenCritical01 = -1f;

        public static bool TryGetActive(out ShinobuPhysiologyRuntime runtime)
        {
            runtime = s_activeRuntime;
            return runtime != null && runtime.isActiveAndEnabled;
        }

        private void Awake()
        {
            entityCapacity = math.max(1, entityCapacity);
#if UNITY_EDITOR
            _csvPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", CsvRelativePath));
            _gasCsvPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", GasCsvRelativePath));
#endif
            _dumpPath = DumpRelativePath;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            s_activeRuntime = this;
            SignalBus<CardiacPulseSignal>.Configure(16, maxFrameSignals: 64, lowTierFrameSignals: 16, laneHash: CardiacPulseSignal.LaneHash);
            SignalBus<CardiacPulseSignal>.EnsureInitialized();
            SignalBus<PhysiologyStateSignal>.Configure(
                PhysiologyStateSignal.ExpectedCapacity,
                maxFrameSignals: PhysiologyStateSignal.MaxFrameSignals,
                lowTierFrameSignals: PhysiologyStateSignal.LowTierFrameSignals,
                laneHash: PhysiologyStateSignal.LaneHash);
            SignalBus<PhysiologyStateSignal>.EnsureInitialized();
            SignalBus<CombatDamageSignal>.EnsureInitialized();
            SignalBus<ToxicityExposureSignal>.Configure(
                ToxicityExposureSignal.ExpectedCapacity,
                maxFrameSignals: ToxicityExposureSignal.MaxFrameSignals,
                lowTierFrameSignals: ToxicityExposureSignal.LowTierFrameSignals,
                laneHash: ToxicityExposureSignal.LaneHash);
            SignalBus<ToxicityExposureSignal>.EnsureInitialized();
            _lastToxicityExposureSnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;
            TryRegisterHotSwapListener();
            RebindColdServices();

            if (EnsureVaultState())
                TryRegisterTicks();
        }

        private void Start()
        {
            if (!Application.isPlaying)
                return;

            RebindColdServices();
            if (EnsureVaultState())
                TryRegisterTicks();
        }

        private void OnDisable()
        {
            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;

            CompleteFrameJobForTeardown();
            TryUnregisterTicks();
            TryUnregisterHotSwapListener();
            UnlockJobBuffers();
            ClearCachedHandles();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                CompleteFrameJobForTeardown();
                UnlockJobBuffers();
                _dataVault = currentService as IDataVault;
                ClearCachedHandles();
                _defaultsInitialized = false;
                _autopsyDumped = false;
                if (_dataVault != null && EnsureVaultState())
                    TryRegisterTicks();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
                _playerContext = currentService as IPlayerRuntimeContext;
            else if (serviceSlot == GlobalRegistryServiceSlot.OceanKinematics)
                _oceanKinematics = currentService as IHectonOceanKinematicsService;
            else if (serviceSlot == GlobalRegistryServiceSlot.GasDynamicsRuntime)
                _gasDynamics = currentService as IGasDynamicsSolver;
        }

        public void SlowTick()
        {
            SchedulePhysiologyTick(AuthoritativeUpdateIntervalSeconds);
        }

        private void SchedulePhysiologyTick(float deltaTime)
        {
            if (_jobScheduled)
                return;

            IDataVault vault = _dataVault;
            if (vault == null || !_defaultsInitialized || !HandlesReady())
                return;

            float sourceDt = math.clamp(deltaTime, 0.0001f, ShinobuPhysiologyConstants.MaxSimulationStepSeconds);
            float qualityWeight01 = UpdateSmoothedGlobalQualityWeight(sourceDt);

            _simulationAccumulator = math.min(
                _simulationAccumulator + sourceDt,
                ShinobuPhysiologyConstants.MaxSimulationStepSeconds);
            float updateInterval = ResolvePhysiologyUpdateIntervalSeconds(qualityWeight01);
            if (_simulationAccumulator < updateInterval)
                return;

            float dt = math.clamp(_simulationAccumulator, 0.0001f, ShinobuPhysiologyConstants.MaxSimulationStepSeconds);
            uint frame = ++_simulationFrameCounter;
            if (!TryLockJobBuffers(vault))
                return;

            bool keepJobGuard = false;
            try
            {
            WriteEnvironmentSeed(vault, dt, frame);

            if (!TryResolveBuffers(
                    vault,
                    out NativeArray<PhysiologyDTO> vitals,
                    out NativeArray<DecompressionStateDTO> decompression,
                    out NativeArray<TissueCompartmentDTO> tissues,
                    out NativeArray<HaldaneTissueCoefficientDTO> coefficients,
                    out NativeArray<MockEnvironmentVitalsSignal> environment,
                    out NativeArray<PhysiologyScalarsDTO> scalars,
                    out NativeArray<StatusEffectStateDTO> statusEffects,
                    out NativeArray<GasPhysiologyStateDTO> gasStates,
                    out NativeArray<BreathingGasFractionsDTO> breathingGas,
                    out NativeArray<GasPhysiologyTuningDTO> gasTuningArray,
                    out NativeArray<VitalsExportDTO> exports,
                    out NativeArray<PhysiologyTelemetryEntry> telemetry,
                    out NativeArray<DecompressionTelemetryEntry> decompressionTelemetry,
                    out NativeArray<CardiacPulseStateDTO> pulses,
                    out NativeArray<MockToxemiaSignal> toxemia,
                    out NativeArray<MockPressureSignal> pressure,
                    out NativeArray<MockCombatDamageSignal> combat,
                    out NativeArray<MockPredatorAggroSignal> predator,
                    out NativeArray<MockMedicalItemUsedSignal> medical,
                    out NativeArray<PhysiologyTuningDTO> tuningArray,
                    out NativeArray<DiveProfileSampleDTO> mockDiveProfile))
            {
                return;
            }

            _ = coefficients;
            _ = mockDiveProfile;

            int count = 1;
            count = math.min(count, entityCapacity);
            count = math.min(count, vitals.Length);
            count = math.min(count, decompression.Length);
            count = math.min(count, tissues.Length / ShinobuPhysiologyConstants.TissueCompartmentCount);
            count = math.min(count, environment.Length);
            count = math.min(count, scalars.Length);
            count = math.min(count, statusEffects.Length);
            count = math.min(count, gasStates.Length);
            count = math.min(count, breathingGas.Length);
            count = math.min(count, exports.Length);
            count = math.min(count, pulses.Length);
            if (count <= 0)
            {
                return;
            }

            PhysiologyTuningDTO tuning = ShinobuPhysiologyJobMath.SanitizeTuning(tuningArray[0]);
            tuningArray[0] = tuning;
            GasPhysiologyTuningDTO gasTuning = ShinobuPhysiologyJobMath.SanitizeGasTuning(gasTuningArray[0]);
            gasTuningArray[0] = gasTuning;

            long scheduleTimestamp = Stopwatch.GetTimestamp();
            uint playerTargetHash = RefreshPlayerToxicityTargetHash();
            int toxicityExposureSnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;
            if (toxicityExposureSnapshotGeneration != _lastToxicityExposureSnapshotGeneration)
            {
                IngestAtmosphereToxicitySignals(toxemia, playerTargetHash, frame, deltaTime);
                _lastToxicityExposureSnapshotGeneration = toxicityExposureSnapshotGeneration;
            }

            IngestRadiationDoseSignals(combat, frame);
            JobHandle handle = new MockEnvironmentDropJob
            {
                Environment = environment,
                PressureSignals = pressure,
                MockDepthMeters = math.max(0f, mockDepthMeters),
                SystemHealthIndex01 = ResolveSystemHealthIndex01(),
                Frame = frame,
                Count = count,
                UseMockDepth = _playerDepthValid ? (byte)0 : (byte)1
            }.Schedule(count, ShinobuPhysiologyConstants.FrameJobBatchSize);

            handle = new GenerateMockBreathingGasJob
            {
                Environment = environment,
                BreathingGas = breathingGas,
                OverrideGas = _breathingGasOverride,
                Count = count,
                UseOverrideGas = _breathingGasOverrideActive ? (byte)1 : (byte)0
            }.Schedule(count, ShinobuPhysiologyConstants.FrameJobBatchSize, handle);

            handle = new CalculatePartialPressuresJob
            {
                Environment = environment,
                BreathingGas = breathingGas,
                GasStates = gasStates,
                Count = count
            }.Schedule(count, ShinobuPhysiologyConstants.FrameJobBatchSize, handle);

            handle = new PhysiologySignalIngestJob
            {
                Vitals = vitals,
                Scalars = scalars,
                CombatSignals = combat,
                PredatorSignals = predator,
                ToxemiaSignals = toxemia,
                MedicalSignals = medical,
                Count = count
            }.Schedule(count, ShinobuPhysiologyConstants.FrameJobBatchSize, handle);

            handle = new IntegrateBloodGasTensionsJob
            {
                Vitals = vitals,
                TissueCompartments = tissues,
                DecompressionStates = decompression,
                TissueCoefficients = coefficients,
                Environment = environment,
                GasStates = gasStates,
                Scalars = scalars,
                PhysiologyWriter = SignalBus<PhysiologyStateSignal>.ParallelWriter,
                PhysiologyWriterBudget = SignalBus<PhysiologyStateSignal>.ParallelWriterBudget,
                DamageWriter = SignalBus<CombatDamageSignal>.ParallelWriter,
                DamageWriterBudget = SignalBus<CombatDamageSignal>.ParallelWriterBudget,
                Tuning = tuning,
                DeltaSeconds = dt,
                GlobalQualityWeight = _smoothedGlobalQualityWeight,
                Frame = frame,
                PlayerTargetHash = playerTargetHash,
                Count = count,
                EmitPhysiologySignal = 1
            }.Schedule(count, ShinobuPhysiologyConstants.FrameJobBatchSize, handle);

            handle = new CalculateCnsToxicityJob
            {
                GasStates = gasStates,
                Scalars = scalars,
                Environment = environment,
                PhysiologyWriter = SignalBus<PhysiologyStateSignal>.ParallelWriter,
                PhysiologyWriterBudget = SignalBus<PhysiologyStateSignal>.ParallelWriterBudget,
                GasTuning = gasTuning,
                DeltaSeconds = dt,
                GlobalQualityWeight = _smoothedGlobalQualityWeight,
                Frame = frame,
                Count = count,
                EmitSignals = 1
            }.Schedule(count, ShinobuPhysiologyConstants.FrameJobBatchSize, handle);

            handle = new OxygenConsumptionJob
            {
                Vitals = vitals,
                Scalars = scalars,
                StatusEffects = statusEffects,
                PulseStates = pulses,
                Environment = environment,
                GasStates = gasStates,
                VitalsExport = exports,
                Telemetry = telemetry,
                DecompressionStates = decompression,
                DecompressionTelemetry = decompressionTelemetry,
                CardiacPulseWriter = SignalBus<CardiacPulseSignal>.ParallelWriter,
                CardiacPulseWriterBudget = SignalBus<CardiacPulseSignal>.ParallelWriterBudget,
                Tuning = tuning,
                GasTuning = gasTuning,
                DeltaSeconds = dt,
                Frame = frame,
                TelemetryCursor = _telemetryCursor,
                DecompressionTelemetryCursor = _decompressionTelemetryCursor,
                GlobalQualityWeight = _smoothedGlobalQualityWeight,
                Count = count,
                EmitPulseSignals = 1
            }.Schedule(count, ShinobuPhysiologyConstants.FrameJobBatchSize, handle);

            _activeJobHandle = handle;
            _scheduledCount = count;
            _jobScheduleTimestamp = scheduleTimestamp;
            _simulationAccumulator = 0f;
            _jobScheduled = true;
            H8Memory.RegisterActiveJob(OwnerSystem, _activeJobHandle);
            keepJobGuard = true;
            }
            finally
            {
                if (!keepJobGuard)
                    UnlockJobBuffers();
            }
        }

        public void LateFrameTick()
        {
            TryFinalizeFrameJobNoWait();
        }

        /// <summary>
        /// Injects a mock pressure sample for isolated tests.
        /// </summary>
        public bool InjectMockPressure(int entityIndex, float depthMeters, float ascentRateMetersPerSecond)
        {
            if (_jobScheduled)
                return false;

            NativeArray<MockPressureSignal> pressure = OpenPhysiologyVaultArray(ref _pressureHandle, BufferID.ShinobuMockPressureSignals, entityCapacity);
            if (!pressure.IsCreated || (uint)entityIndex >= (uint)pressure.Length)
                return false;

            pressure[entityIndex] = new MockPressureSignal
            {
                DepthMeters = math.max(0f, depthMeters),
                AmbientPressureAtm = ShinobuPhysiologyJobMath.DepthToPressureAtm(depthMeters),
                AscentRateMetersPerSecond = ascentRateMetersPerSecond,
                Frame = _simulationFrameCounter,
                Flags = MockPressureSignal.ActiveFlag,
                AmbientTemperatureCelsius = mockAmbientTemperatureCelsius
            };
            return true;
        }

        /// <summary>
        /// Pushes a chamber/habitat pressure override; stateMask carries chamber treatment bits without a cure branch.
        /// </summary>
        public bool SetHyperbaricTreatmentState(int entityIndex, float ambientPressureAtm, uint stateMask)
        {
            if (_jobScheduled)
                return false;

            NativeArray<MockPressureSignal> pressure = OpenPhysiologyVaultArray(ref _pressureHandle, BufferID.ShinobuMockPressureSignals, entityCapacity);
            if (!pressure.IsCreated || (uint)entityIndex >= (uint)pressure.Length)
                return false;

            float safeAtm = math.max(1f, ShinobuPhysiologyJobMath.SanitizeFinite(ambientPressureAtm, 1f));
            float depthMeters = math.max(0f, (safeAtm - ShinobuPhysiologyConstants.AtmosphericPressureAtSurfaceAtm) * 10f);
            pressure[entityIndex] = new MockPressureSignal
            {
                DepthMeters = depthMeters,
                AmbientPressureAtm = safeAtm,
                AscentRateMetersPerSecond = 0f,
                Frame = _simulationFrameCounter,
                Flags = MockPressureSignal.ActiveFlag | MockPressureSignal.HabitatOverrideFlag | MockPressureSignal.HyperbaricTreatmentFlag | (stateMask & 0xFFF0u),
                AmbientTemperatureCelsius = mockAmbientTemperatureCelsius
            };
            return true;
        }

        /// <summary>
        /// Generates an accelerated synthetic crash-dive profile into DataVault for editor/runtime smoke tests.
        /// </summary>
        public bool GenerateMockDiveProfile()
        {
            if (_jobScheduled)
                return false;

            NativeArray<DiveProfileSampleDTO> samples = OpenPhysiologyVaultArray(ref _mockDiveProfileHandle, BufferID.ShinobuMockDiveProfile, ShinobuPhysiologyConstants.TelemetryFrameCount);
            if (!samples.IsCreated || samples.Length <= 0)
                return false;

            GenerateMockDiveProfileJob job = new GenerateMockDiveProfileJob
            {
                Samples = samples,
                SampleStepSeconds = 10f,
                Frame = _simulationFrameCounter,
                Count = samples.Length
            };
            // COLD SYNC JOB: explicit smoke-test profile generation, not part of the frame simulation chain.
            for (int i = 0; i < samples.Length; i++)
                job.Execute(i);

            return true;
        }

        /// <summary>
        /// Generates deterministic breathing-gas rows into DataVault for isolated gas physiology tests.
        /// </summary>
        public bool GenerateMockBreathingGas()
        {
            if (_jobScheduled)
                return false;

            NativeArray<MockEnvironmentVitalsSignal> environment = OpenPhysiologyVaultArray(ref _environmentHandle, BufferID.ShinobuEnvironmentVitals, entityCapacity);
            NativeArray<BreathingGasFractionsDTO> breathingGas = OpenPhysiologyVaultArray(ref _breathingGasHandle, ShinobuPhysiologyConstants.BreathingGasFractionsBuffer, entityCapacity);
            if (!environment.IsCreated || !breathingGas.IsCreated || breathingGas.Length <= 0)
                return false;

            GenerateMockBreathingGasJob job = new GenerateMockBreathingGasJob
            {
                Environment = environment,
                BreathingGas = breathingGas,
                OverrideGas = _breathingGasOverride,
                Count = math.min(environment.Length, breathingGas.Length),
                UseOverrideGas = _breathingGasOverrideActive ? (byte)1 : (byte)0
            };
            // COLD SYNC JOB: explicit gas-profile generation for test tooling, not a frame simulation dependency.
            for (int i = 0; i < job.Count; i++)
                job.Execute(i);

            return true;
        }

        /// <summary>
        /// Injects a mock trauma bit.
        /// </summary>
        public bool InjectMockCombatDamage(int entityIndex, int traumaType, float severity01)
        {
            if (_jobScheduled)
                return false;

            NativeArray<MockCombatDamageSignal> combat = OpenPhysiologyVaultArray(ref _combatHandle, BufferID.ShinobuMockCombatDamageSignals, entityCapacity);
            if (!combat.IsCreated || (uint)entityIndex >= (uint)combat.Length)
                return false;

            int clampedTraumaType = math.clamp(traumaType, 0, 7);
            combat[entityIndex] = new MockCombatDamageSignal
            {
                TraumaType = clampedTraumaType,
                Severity01 = math.saturate(severity01),
                Frame = _simulationFrameCounter,
                Flags = 1u,
                SourceHash = ShinobuPhysiologyConstants.SourceHash,
                CombatStatusMask = ResolveMockCombatStatusMask(clampedTraumaType)
            };
            return true;
        }

        private static uint ResolveMockCombatStatusMask(int traumaType)
        {
            uint mask = 0u;
            mask |= traumaType == 0 ? ShinobuCombatStatusBridgeBits.Bleeding : 0u;
            mask |= traumaType == 4 ? ShinobuCombatStatusBridgeBits.Poisoned : 0u;
            mask |= traumaType == 5 ? ShinobuCombatStatusBridgeBits.Stunned : 0u;
            mask |= traumaType == 6 ? ShinobuCombatStatusBridgeBits.Irradiated : 0u;
            mask |= traumaType == 7 ? ShinobuCombatStatusBridgeBits.Hypoxia : 0u;
            return mask;
        }

        /// <summary>
        /// Injects predator aggro for adrenaline testing.
        /// </summary>
        public bool InjectMockPredatorAggro(int entityIndex, float aggro01)
        {
            if (_jobScheduled)
                return false;

            NativeArray<MockPredatorAggroSignal> predator = OpenPhysiologyVaultArray(ref _predatorHandle, BufferID.ShinobuMockPredatorAggroSignals, entityCapacity);
            if (!predator.IsCreated || (uint)entityIndex >= (uint)predator.Length)
                return false;

            predator[entityIndex] = new MockPredatorAggroSignal
            {
                Aggro01 = math.saturate(aggro01),
                Frame = _simulationFrameCounter,
                Flags = 1u,
                SourceHash = ShinobuPhysiologyConstants.SourceHash
            };
            return true;
        }

        /// <summary>
        /// Injects toxemia, either as absolute value or delta.
        /// </summary>
        public bool InjectMockToxemia(int entityIndex, float value01, bool absolute)
        {
            if (_jobScheduled)
                return false;

            NativeArray<MockToxemiaSignal> toxemia = OpenPhysiologyVaultArray(ref _toxemiaHandle, BufferID.ShinobuMockToxemiaSignals, entityCapacity);
            if (!toxemia.IsCreated || (uint)entityIndex >= (uint)toxemia.Length)
                return false;

            toxemia[entityIndex] = new MockToxemiaSignal
            {
                Delta01 = absolute ? 0f : value01,
                Absolute01 = absolute ? math.saturate(value01) : 0f,
                Frame = _simulationFrameCounter,
                Flags = absolute ? 3u : 1u
            };
            return true;
        }

        /// <summary>
        /// Starts slow toxemia purge from a mock medical item.
        /// </summary>
        public bool InjectMockMedicalItem(int entityIndex, float purgeStrength01)
        {
            if (_jobScheduled)
                return false;

            NativeArray<MockMedicalItemUsedSignal> medical = OpenPhysiologyVaultArray(ref _medicalHandle, BufferID.ShinobuMockMedicalItemSignals, entityCapacity);
            if (!medical.IsCreated || (uint)entityIndex >= (uint)medical.Length)
                return false;

            medical[entityIndex] = new MockMedicalItemUsedSignal
            {
                PurgeStrength01 = math.saturate(purgeStrength01),
                ItemHash = 0x4D454449u,
                Frame = _simulationFrameCounter,
                Flags = 1u
            };
            return true;
        }

        /// <summary>
        /// Returns the current tuning row for editor tooling.
        /// </summary>
        public bool TryGetTuning(out PhysiologyTuningDTO tuning)
        {
            tuning = default;
            if (_jobScheduled)
                return false;

            if (!TryReadPhysiologyVaultArray(in _tuningHandle, BufferID.ShinobuPhysiologyTuning, 1, out NativeArray<PhysiologyTuningDTO> tuningArray))
                return false;

            tuning = tuningArray[0];
            return true;
        }

        /// <summary>
        /// Applies editor-authored tuning directly to vault memory.
        /// </summary>
        public bool SetEditorTuning(PhysiologyTuningDTO tuning)
        {
            if (_jobScheduled)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsPhysiologyVaultHandle(in _tuningHandle, BufferID.ShinobuPhysiologyTuning) ||
                !vault.TryAcquireWriteLock(in _tuningHandle, OwnerSystem, out NativeArray<PhysiologyTuningDTO> tuningArray))
            {
                return false;
            }

            try
            {
                if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                    return false;

                tuningArray[0] = ShinobuPhysiologyJobMath.SanitizeTuning(tuning);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _tuningHandle, OwnerSystem);
            }
        }

        /// <summary>
        /// Reads the vault-backed gas toxicity tuning row for editor tools.
        /// </summary>
        public bool TryGetGasTuning(out GasPhysiologyTuningDTO tuning)
        {
            tuning = default;
            if (_jobScheduled)
                return false;

            if (!TryReadPhysiologyVaultArray(in _gasTuningHandle, ShinobuPhysiologyConstants.GasPhysiologyTuningBuffer, 1, out NativeArray<GasPhysiologyTuningDTO> tuningArray))
                return false;

            tuning = ShinobuPhysiologyJobMath.SanitizeGasTuning(tuningArray[0]);
            return true;
        }

        /// <summary>
        /// Applies editor-authored gas toxicity tuning directly to vault memory.
        /// </summary>
        public bool SetEditorGasTuning(GasPhysiologyTuningDTO tuning)
        {
            if (_jobScheduled)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsPhysiologyVaultHandle(in _gasTuningHandle, ShinobuPhysiologyConstants.GasPhysiologyTuningBuffer) ||
                !vault.TryAcquireWriteLock(in _gasTuningHandle, OwnerSystem, out NativeArray<GasPhysiologyTuningDTO> tuningArray))
            {
                return false;
            }

            try
            {
                if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                    return false;

                tuningArray[0] = ShinobuPhysiologyJobMath.SanitizeGasTuning(tuning);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _gasTuningHandle, OwnerSystem);
            }
        }

        /// <summary>
        /// Reads one tissue tension and its current M-value limit for editor histograms.
        /// </summary>
        public bool TryGetTissueTension(int entityIndex, int tissueIndex, out float tension, out float mValue)
        {
            tension = 0f;
            mValue = 0f;
            if (_jobScheduled)
                return false;

            int compartmentIndex = entityIndex * ShinobuPhysiologyConstants.TissueCompartmentCount + tissueIndex;
            if ((uint)tissueIndex >= ShinobuPhysiologyConstants.TissueCompartmentCount ||
                !TryReadPhysiologyVaultArray(in _tissueHandle, BufferID.ShinobuTissueCompartments, entityCapacity * ShinobuPhysiologyConstants.TissueCompartmentCount, out NativeArray<TissueCompartmentDTO> tissues) ||
                (uint)compartmentIndex >= (uint)tissues.Length)
            {
                return false;
            }

            TissueCompartmentDTO tissue = tissues[compartmentIndex];
            tension = tissue.NitrogenTension;
            HaldaneTissueCoefficientDTO coefficient = TryReadPhysiologyVaultArray(in _coefficientHandle, BufferID.ShinobuHaldaneCoefficients, ShinobuPhysiologyConstants.TissueCompartmentCount, out NativeArray<HaldaneTissueCoefficientDTO> coefficients) &&
                                                      (uint)tissueIndex < (uint)coefficients.Length
                ? coefficients[tissueIndex]
                : default;
            float fallbackA = ShinobuPhysiologyJobMath.ResolveEmergencyBuhlmannA(tissueIndex);
            float fallbackB = ShinobuPhysiologyJobMath.ResolveEmergencyBuhlmannB(tissueIndex);
            float rawA = coefficient.BuhlmannA > 0f ? coefficient.BuhlmannA : fallbackA;
            float rawB = coefficient.BuhlmannB > 0.1f ? coefficient.BuhlmannB : fallbackB;
            float a = math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(rawA, fallbackA));
            float b = math.clamp(ShinobuPhysiologyJobMath.SanitizeFinite(rawB, fallbackB), 0.1f, 2f);
            mValue = ShinobuPhysiologyJobMath.ResolveBuhlmannAllowedAmbientPressure(tension, a, b);
            return true;
        }

        /// <summary>
        /// Reads the first diegetic vitals export row.
        /// </summary>
        public bool TryGetVitalsExport(out VitalsExportDTO export)
        {
            export = default;
            if (_jobScheduled)
                return false;

            if (!TryReadPhysiologyVaultArray(in _exportHandle, BufferID.ShinobuVitalsExport, entityCapacity, out NativeArray<VitalsExportDTO> exports) ||
                exports.Length <= 0)
                return false;

            export = exports[0];
            return true;
        }

        /// <summary>
        /// Reads the latest completed black-box telemetry row for editor diagnostics.
        /// </summary>
        public bool TryGetLatestTelemetry(out PhysiologyTelemetryEntry entry)
        {
            entry = default;
            if (_jobScheduled)
                return false;

            if (!TryReadPhysiologyVaultArray(in _telemetryHandle, BufferID.ShinobuPhysiologyTelemetryRing, ShinobuPhysiologyConstants.TelemetryFrameCount, out NativeArray<PhysiologyTelemetryEntry> telemetry) ||
                telemetry.Length <= 0)
                return false;

            int index = _telemetryCursor - 1;
            if (index < 0)
                index += telemetry.Length;
            entry = telemetry[index % telemetry.Length];
            return entry.Frame != 0u;
        }

        /// <summary>
        /// Reads one gas physiology row for editor and debug visualizers.
        /// </summary>
        public bool TryGetGasPhysiologyState(int entityIndex, out GasPhysiologyStateDTO state)
        {
            state = default;
            if (_jobScheduled)
                return false;

            if (!TryReadPhysiologyVaultArray(in _gasStateHandle, ShinobuPhysiologyConstants.GasPhysiologyStatesBuffer, entityCapacity, out NativeArray<GasPhysiologyStateDTO> gasStates) ||
                (uint)entityIndex >= (uint)gasStates.Length)
                return false;

            state = gasStates[entityIndex];
            return true;
        }

        /// <summary>
        /// Reads one unified status-effect row for editor diagnostics and route checks.
        /// </summary>
        public bool TryGetStatusEffectState(int entityIndex, out StatusEffectStateDTO state)
        {
            state = default;
            if (_jobScheduled)
                return false;

            if (!TryReadPhysiologyVaultArray(in _statusEffectHandle, ShinobuPhysiologyConstants.StatusEffectStatesBuffer, entityCapacity, out NativeArray<StatusEffectStateDTO> statusStates) ||
                (uint)entityIndex >= (uint)statusStates.Length)
                return false;

            state = statusStates[entityIndex];
            return true;
        }

        private void RebindColdServices()
        {
            _dataVault = GlobalRegistry.DataVault;
            _playerContext = GlobalRegistry.Player;
            _oceanKinematics = GlobalRegistry.OceanKinematics;
            _gasDynamics = GlobalRegistry.GasDynamics;
        }

        private bool EnsureVaultState()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            entityCapacity = math.max(1, entityCapacity);
            if (HandlesReady())
                return true;
            if (!ShinobuPhysiologyLayoutGuards.ValidatePhysiologyLayouts())
                return false;

            bool created =
                OpenOrAcquirePhysiologyVaultBuffer(ref _vitalsHandle, BufferID.ShinobuPhysiologyVitals, entityCapacity, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquirePhysiologyVaultBuffer(ref _decompressionHandle, BufferID.ShinobuDecompressionStates, entityCapacity, NativeArrayOptions.UninitializedMemory, out _) &&
                OpenOrAcquirePhysiologyVaultBuffer(ref _tissueHandle, BufferID.ShinobuTissueCompartments, entityCapacity * ShinobuPhysiologyConstants.TissueCompartmentCount, NativeArrayOptions.UninitializedMemory, out _) &&
                OpenOrAcquirePhysiologyVaultBuffer(ref _coefficientHandle, BufferID.ShinobuHaldaneCoefficients, ShinobuPhysiologyConstants.TissueCompartmentCount, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquirePhysiologyVaultBuffer(ref _environmentHandle, BufferID.ShinobuEnvironmentVitals, entityCapacity, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquirePhysiologyVaultBuffer(ref _scalarHandle, BufferID.ShinobuPhysiologyScalars, entityCapacity, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquirePhysiologyVaultBuffer(ref _statusEffectHandle, ShinobuPhysiologyConstants.StatusEffectStatesBuffer, entityCapacity, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquirePhysiologyVaultBuffer(ref _gasStateHandle, ShinobuPhysiologyConstants.GasPhysiologyStatesBuffer, entityCapacity, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquirePhysiologyVaultBuffer(ref _breathingGasHandle, ShinobuPhysiologyConstants.BreathingGasFractionsBuffer, entityCapacity, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquirePhysiologyVaultBuffer(ref _gasTuningHandle, ShinobuPhysiologyConstants.GasPhysiologyTuningBuffer, 1, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquirePhysiologyVaultBuffer(ref _exportHandle, BufferID.ShinobuVitalsExport, entityCapacity, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquirePhysiologyVaultBuffer(ref _telemetryHandle, BufferID.ShinobuPhysiologyTelemetryRing, ShinobuPhysiologyConstants.TelemetryFrameCount, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquirePhysiologyVaultBuffer(ref _decompressionTelemetryHandle, ShinobuPhysiologyConstants.DecompressionTelemetryRingBuffer, ShinobuPhysiologyConstants.TelemetryFrameCount, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquirePhysiologyVaultBuffer(ref _pulseHandle, BufferID.ShinobuCardiacPulseStates, entityCapacity, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquirePhysiologyVaultBuffer(ref _toxemiaHandle, BufferID.ShinobuMockToxemiaSignals, entityCapacity, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquirePhysiologyVaultBuffer(ref _pressureHandle, BufferID.ShinobuMockPressureSignals, entityCapacity, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquirePhysiologyVaultBuffer(ref _combatHandle, BufferID.ShinobuMockCombatDamageSignals, entityCapacity, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquirePhysiologyVaultBuffer(ref _predatorHandle, BufferID.ShinobuMockPredatorAggroSignals, entityCapacity, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquirePhysiologyVaultBuffer(ref _medicalHandle, BufferID.ShinobuMockMedicalItemSignals, entityCapacity, NativeArrayOptions.ClearMemory, out _) &&
                OpenOrAcquirePhysiologyVaultBuffer(ref _tuningHandle, BufferID.ShinobuPhysiologyTuning, 1, NativeArrayOptions.ClearMemory, out _) &&
#if UNITY_EDITOR
                OpenOrAcquirePhysiologyVaultBuffer(ref _csvOverrideHandle, BufferID.ShinobuBiologyCsvOverrides, CsvOverrideCapacity, NativeArrayOptions.ClearMemory, out _) &&
#endif
                OpenOrAcquirePhysiologyVaultBuffer(ref _mockDiveProfileHandle, BufferID.ShinobuMockDiveProfile, ShinobuPhysiologyConstants.TelemetryFrameCount, NativeArrayOptions.ClearMemory, out _)
                ;
            if (!created || !HandlesReady())
                return false;

            InitializeDefaults(vault);
            return true;
        }

        private bool HandlesReady()
        {
            return OpenPhysiologyVaultBuffer(ref _vitalsHandle, BufferID.ShinobuPhysiologyVitals, entityCapacity, out _) &&
                   OpenPhysiologyVaultBuffer(ref _decompressionHandle, BufferID.ShinobuDecompressionStates, entityCapacity, out _) &&
                   OpenPhysiologyVaultBuffer(ref _tissueHandle, BufferID.ShinobuTissueCompartments, entityCapacity * ShinobuPhysiologyConstants.TissueCompartmentCount, out _) &&
                   OpenPhysiologyVaultBuffer(ref _coefficientHandle, BufferID.ShinobuHaldaneCoefficients, ShinobuPhysiologyConstants.TissueCompartmentCount, out _) &&
                   OpenPhysiologyVaultBuffer(ref _environmentHandle, BufferID.ShinobuEnvironmentVitals, entityCapacity, out _) &&
                   OpenPhysiologyVaultBuffer(ref _scalarHandle, BufferID.ShinobuPhysiologyScalars, entityCapacity, out _) &&
                   OpenPhysiologyVaultBuffer(ref _statusEffectHandle, ShinobuPhysiologyConstants.StatusEffectStatesBuffer, entityCapacity, out _) &&
                   OpenPhysiologyVaultBuffer(ref _gasStateHandle, ShinobuPhysiologyConstants.GasPhysiologyStatesBuffer, entityCapacity, out _) &&
                   OpenPhysiologyVaultBuffer(ref _breathingGasHandle, ShinobuPhysiologyConstants.BreathingGasFractionsBuffer, entityCapacity, out _) &&
                   OpenPhysiologyVaultBuffer(ref _gasTuningHandle, ShinobuPhysiologyConstants.GasPhysiologyTuningBuffer, 1, out _) &&
                   OpenPhysiologyVaultBuffer(ref _exportHandle, BufferID.ShinobuVitalsExport, entityCapacity, out _) &&
                   OpenPhysiologyVaultBuffer(ref _telemetryHandle, BufferID.ShinobuPhysiologyTelemetryRing, ShinobuPhysiologyConstants.TelemetryFrameCount, out _) &&
                   OpenPhysiologyVaultBuffer(ref _decompressionTelemetryHandle, ShinobuPhysiologyConstants.DecompressionTelemetryRingBuffer, ShinobuPhysiologyConstants.TelemetryFrameCount, out _) &&
                   OpenPhysiologyVaultBuffer(ref _pulseHandle, BufferID.ShinobuCardiacPulseStates, entityCapacity, out _) &&
                   OpenPhysiologyVaultBuffer(ref _toxemiaHandle, BufferID.ShinobuMockToxemiaSignals, entityCapacity, out _) &&
                   OpenPhysiologyVaultBuffer(ref _pressureHandle, BufferID.ShinobuMockPressureSignals, entityCapacity, out _) &&
                   OpenPhysiologyVaultBuffer(ref _combatHandle, BufferID.ShinobuMockCombatDamageSignals, entityCapacity, out _) &&
                   OpenPhysiologyVaultBuffer(ref _predatorHandle, BufferID.ShinobuMockPredatorAggroSignals, entityCapacity, out _) &&
                   OpenPhysiologyVaultBuffer(ref _medicalHandle, BufferID.ShinobuMockMedicalItemSignals, entityCapacity, out _) &&
                   OpenPhysiologyVaultBuffer(ref _tuningHandle, BufferID.ShinobuPhysiologyTuning, 1, out _) &&
#if UNITY_EDITOR
                   OpenPhysiologyVaultBuffer(ref _csvOverrideHandle, BufferID.ShinobuBiologyCsvOverrides, CsvOverrideCapacity, out _) &&
#endif
                   OpenPhysiologyVaultBuffer(ref _mockDiveProfileHandle, BufferID.ShinobuMockDiveProfile, ShinobuPhysiologyConstants.TelemetryFrameCount, out _)
                   ;
        }

        private bool OpenOrAcquirePhysiologyVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            IDataVault vault = _dataVault;
            if (OpenPhysiologyVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            if (vault == null || requiredLength <= 0)
            {
                buffer = default;
                return false;
            }

            if (vault.IsAllocationLocked)
            {
                if (!vault.TryGetGenerationHandle(bufferId, out handle))
                {
                    buffer = default;
                    return false;
                }

                return OpenPhysiologyVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                OwnerSystem,
                options);
            return OpenPhysiologyVaultBuffer(vault, ref handle, bufferId, requiredLength, out buffer);
        }

        private NativeArray<T> OpenPhysiologyVaultArray<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            return OpenPhysiologyVaultBuffer(_dataVault, ref handle, bufferId, requiredLength, out NativeArray<T> buffer)
                ? buffer
                : default;
        }

        private bool TryReadPhysiologyVaultArray<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsPhysiologyVaultHandle(in handle, bufferId) ||
                !vault.TryReadHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private bool OpenPhysiologyVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            return OpenPhysiologyVaultBuffer(_dataVault, ref handle, bufferId, requiredLength, out buffer);
        }

        private static bool OpenPhysiologyVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsPhysiologyVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsPhysiologyVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)OwnerSystem &&
                   handle.Generation != 0u;
        }

        private void InitializeDefaults(IDataVault vault)
        {
            if (_defaultsInitialized)
                return;

            if (!SanitizeDefaultTuningRows(vault))
                return;

#if UNITY_EDITOR
            if (!TryLoadLegacyMetabolismTables(vault))
#endif
                GenerateEmergencyMockMetabolism(vault);
#if UNITY_EDITOR
            LoadCsvOverridesFromDisk(vault);
#endif

            if (!vault.TryAcquireMutationGuard(DefaultStateMutationGuardMask))
                return;

            try
            {
            NativeArray<PhysiologyDTO> vitals = OpenPhysiologyVaultArray(ref _vitalsHandle, BufferID.ShinobuPhysiologyVitals, entityCapacity);
            NativeArray<DecompressionStateDTO> states = OpenPhysiologyVaultArray(ref _decompressionHandle, BufferID.ShinobuDecompressionStates, entityCapacity);
            NativeArray<TissueCompartmentDTO> tissues = OpenPhysiologyVaultArray(ref _tissueHandle, BufferID.ShinobuTissueCompartments, entityCapacity * ShinobuPhysiologyConstants.TissueCompartmentCount);
            NativeArray<HaldaneTissueCoefficientDTO> coefficients = OpenPhysiologyVaultArray(ref _coefficientHandle, BufferID.ShinobuHaldaneCoefficients, ShinobuPhysiologyConstants.TissueCompartmentCount);
            NativeArray<PhysiologyScalarsDTO> scalars = OpenPhysiologyVaultArray(ref _scalarHandle, BufferID.ShinobuPhysiologyScalars, entityCapacity);
            NativeArray<StatusEffectStateDTO> statusEffects = OpenPhysiologyVaultArray(ref _statusEffectHandle, ShinobuPhysiologyConstants.StatusEffectStatesBuffer, entityCapacity);
            NativeArray<GasPhysiologyStateDTO> gasStates = OpenPhysiologyVaultArray(ref _gasStateHandle, ShinobuPhysiologyConstants.GasPhysiologyStatesBuffer, entityCapacity);
            NativeArray<BreathingGasFractionsDTO> breathingGas = OpenPhysiologyVaultArray(ref _breathingGasHandle, ShinobuPhysiologyConstants.BreathingGasFractionsBuffer, entityCapacity);
            NativeArray<CardiacPulseStateDTO> pulses = OpenPhysiologyVaultArray(ref _pulseHandle, BufferID.ShinobuCardiacPulseStates, entityCapacity);
            int count = math.min(entityCapacity, math.min(vitals.Length, states.Length));
            if (tissues.IsCreated)
                count = math.min(count, tissues.Length / ShinobuPhysiologyConstants.TissueCompartmentCount);
            if (gasStates.IsCreated)
                count = math.min(count, gasStates.Length);
            if (breathingGas.IsCreated)
                count = math.min(count, breathingGas.Length);
            if (statusEffects.IsCreated)
                count = math.min(count, statusEffects.Length);
            for (int i = 0; i < count; i++)
            {
                vitals[i] = new PhysiologyDTO
                {
                    BloodOxygen = 1f,
                    TissueNitrogen = ShinobuPhysiologyConstants.SurfaceNitrogenPartialPressureAtm,
                    CoreTemperature = 37f,
                    ActiveTraumaMask = 0u,
                    ActiveTraumaRefreshMask = 0u,
                    LastTraumaRefreshFrame = 0u,
                    HeartRate = 62f,
                    Adrenaline = 0f
                };

                DecompressionStateDTO state = default;
                state.CurrentAmbientPressure = 1f;
                state.GradientAdvantage = 1f;
                state.BubbleFlags = 0u;
                for (int tissue = 0; tissue < ShinobuPhysiologyConstants.TissueCompartmentCount; tissue++)
                    state.SetTissueTensionN2(tissue, ShinobuPhysiologyConstants.SurfaceNitrogenPartialPressureAtm);

                states[i] = state;
                if (scalars.IsCreated && i < scalars.Length)
                    scalars[i] = new PhysiologyScalarsDTO { FatigueMultiplier = 1f };
                if (statusEffects.IsCreated && i < statusEffects.Length)
                    statusEffects[i] = default;
                if (breathingGas.IsCreated && i < breathingGas.Length)
                {
                    breathingGas[i] = ShinobuPhysiologyJobMath.SanitizeBreathingGas(new BreathingGasFractionsDTO
                    {
                        OxygenFraction = ShinobuPhysiologyConstants.OxygenFraction,
                        NitrogenFraction = ShinobuPhysiologyConstants.NitrogenFraction,
                        CarbonDioxideFraction = ShinobuPhysiologyConstants.CarbonDioxideFraction,
                        GasHash = 0x41495231u
                    });
                }
                if (gasStates.IsCreated && i < gasStates.Length)
                {
                    gasStates[i] = new GasPhysiologyStateDTO
                    {
                        OxygenPartialPressure = ShinobuPhysiologyConstants.SurfaceOxygenPartialPressureAtm,
                        NitrogenPartialPressure = ShinobuPhysiologyConstants.SurfaceNitrogenPartialPressureAtm,
                        CarbonDioxidePartialPressure = ShinobuPhysiologyConstants.CarbonDioxideFraction,
                        StaminaDrainRate = 1f
                    };
                }
                if (pulses.IsCreated && i < pulses.Length)
                    pulses[i] = default;
            }

            if (tissues.IsCreated && tissues.Length > 0)
            {
                InitTissueCompartmentsJob initJob = new InitTissueCompartmentsJob
                {
                    TissueCompartments = tissues,
                    TissueCoefficients = coefficients,
                    EntityCapacity = count
                };
                // COLD SYNC JOB: boot-time Vault initialization fence, never a gameplay tick dependency.
                for (int i = 0; i < tissues.Length; i++)
                    initJob.Execute(i);
            }

            _defaultsInitialized = true;
            }
            finally
            {
                vault.ReleaseMutationGuard(DefaultStateMutationGuardMask);
            }
        }

        private bool SanitizeDefaultTuningRows(IDataVault vault)
        {
            if (vault == null)
                return false;

            NativeArray<PhysiologyTuningDTO> tuning = OpenPhysiologyVaultArray(ref _tuningHandle, BufferID.ShinobuPhysiologyTuning, 1);
            if (!tuning.IsCreated || tuning.Length <= 0)
                return false;

            NativeArray<GasPhysiologyTuningDTO> gasTuning = OpenPhysiologyVaultArray(ref _gasTuningHandle, ShinobuPhysiologyConstants.GasPhysiologyTuningBuffer, 1);
            if (!gasTuning.IsCreated || gasTuning.Length <= 0)
                return false;

            PhysiologyTuningDTO sanitizedTuning = ShinobuPhysiologyJobMath.SanitizeTuning(tuning[0]);
            GasPhysiologyTuningDTO sanitizedGasTuning = ShinobuPhysiologyJobMath.SanitizeGasTuning(gasTuning[0]);

            if (!vault.TryAcquireMutationGuard(DefaultTuningMutationGuardMask))
                return false;

            try
            {
                tuning[0] = sanitizedTuning;
                gasTuning[0] = sanitizedGasTuning;
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(DefaultTuningMutationGuardMask);
            }
        }

        private bool TryResolveBuffers(
            IDataVault vault,
            out NativeArray<PhysiologyDTO> vitals,
            out NativeArray<DecompressionStateDTO> decompression,
            out NativeArray<TissueCompartmentDTO> tissues,
            out NativeArray<HaldaneTissueCoefficientDTO> coefficients,
            out NativeArray<MockEnvironmentVitalsSignal> environment,
            out NativeArray<PhysiologyScalarsDTO> scalars,
            out NativeArray<StatusEffectStateDTO> statusEffects,
            out NativeArray<GasPhysiologyStateDTO> gasStates,
            out NativeArray<BreathingGasFractionsDTO> breathingGas,
            out NativeArray<GasPhysiologyTuningDTO> gasTuning,
            out NativeArray<VitalsExportDTO> exports,
            out NativeArray<PhysiologyTelemetryEntry> telemetry,
            out NativeArray<DecompressionTelemetryEntry> decompressionTelemetry,
            out NativeArray<CardiacPulseStateDTO> pulses,
            out NativeArray<MockToxemiaSignal> toxemia,
            out NativeArray<MockPressureSignal> pressure,
            out NativeArray<MockCombatDamageSignal> combat,
            out NativeArray<MockPredatorAggroSignal> predator,
            out NativeArray<MockMedicalItemUsedSignal> medical,
            out NativeArray<PhysiologyTuningDTO> tuning,
            out NativeArray<DiveProfileSampleDTO> mockDiveProfile)
        {
            vitals = OpenPhysiologyVaultArray(ref _vitalsHandle, BufferID.ShinobuPhysiologyVitals, entityCapacity);
            decompression = OpenPhysiologyVaultArray(ref _decompressionHandle, BufferID.ShinobuDecompressionStates, entityCapacity);
            tissues = OpenPhysiologyVaultArray(ref _tissueHandle, BufferID.ShinobuTissueCompartments, entityCapacity * ShinobuPhysiologyConstants.TissueCompartmentCount);
            coefficients = OpenPhysiologyVaultArray(ref _coefficientHandle, BufferID.ShinobuHaldaneCoefficients, ShinobuPhysiologyConstants.TissueCompartmentCount);
            environment = OpenPhysiologyVaultArray(ref _environmentHandle, BufferID.ShinobuEnvironmentVitals, entityCapacity);
            scalars = OpenPhysiologyVaultArray(ref _scalarHandle, BufferID.ShinobuPhysiologyScalars, entityCapacity);
            statusEffects = OpenPhysiologyVaultArray(ref _statusEffectHandle, ShinobuPhysiologyConstants.StatusEffectStatesBuffer, entityCapacity);
            gasStates = OpenPhysiologyVaultArray(ref _gasStateHandle, ShinobuPhysiologyConstants.GasPhysiologyStatesBuffer, entityCapacity);
            breathingGas = OpenPhysiologyVaultArray(ref _breathingGasHandle, ShinobuPhysiologyConstants.BreathingGasFractionsBuffer, entityCapacity);
            gasTuning = OpenPhysiologyVaultArray(ref _gasTuningHandle, ShinobuPhysiologyConstants.GasPhysiologyTuningBuffer, 1);
            exports = OpenPhysiologyVaultArray(ref _exportHandle, BufferID.ShinobuVitalsExport, entityCapacity);
            telemetry = OpenPhysiologyVaultArray(ref _telemetryHandle, BufferID.ShinobuPhysiologyTelemetryRing, ShinobuPhysiologyConstants.TelemetryFrameCount);
            decompressionTelemetry = OpenPhysiologyVaultArray(ref _decompressionTelemetryHandle, ShinobuPhysiologyConstants.DecompressionTelemetryRingBuffer, ShinobuPhysiologyConstants.TelemetryFrameCount);
            pulses = OpenPhysiologyVaultArray(ref _pulseHandle, BufferID.ShinobuCardiacPulseStates, entityCapacity);
            toxemia = OpenPhysiologyVaultArray(ref _toxemiaHandle, BufferID.ShinobuMockToxemiaSignals, entityCapacity);
            pressure = OpenPhysiologyVaultArray(ref _pressureHandle, BufferID.ShinobuMockPressureSignals, entityCapacity);
            combat = OpenPhysiologyVaultArray(ref _combatHandle, BufferID.ShinobuMockCombatDamageSignals, entityCapacity);
            predator = OpenPhysiologyVaultArray(ref _predatorHandle, BufferID.ShinobuMockPredatorAggroSignals, entityCapacity);
            medical = OpenPhysiologyVaultArray(ref _medicalHandle, BufferID.ShinobuMockMedicalItemSignals, entityCapacity);
            tuning = OpenPhysiologyVaultArray(ref _tuningHandle, BufferID.ShinobuPhysiologyTuning, 1);
            mockDiveProfile = OpenPhysiologyVaultArray(ref _mockDiveProfileHandle, BufferID.ShinobuMockDiveProfile, ShinobuPhysiologyConstants.TelemetryFrameCount);
            return vitals.IsCreated &&
                   decompression.IsCreated &&
                   tissues.IsCreated &&
                   coefficients.IsCreated &&
                   environment.IsCreated &&
                   scalars.IsCreated &&
                   statusEffects.IsCreated &&
                   gasStates.IsCreated &&
                   breathingGas.IsCreated &&
                   gasTuning.IsCreated &&
                   gasTuning.Length > 0 &&
                   exports.IsCreated &&
                   telemetry.IsCreated &&
                   decompressionTelemetry.IsCreated &&
                   pulses.IsCreated &&
                   toxemia.IsCreated &&
                   pressure.IsCreated &&
                   combat.IsCreated &&
                   predator.IsCreated &&
                   medical.IsCreated &&
                   tuning.IsCreated &&
                   tuning.Length > 0 &&
                   mockDiveProfile.IsCreated;
        }

#if UNITY_EDITOR
        private static bool TryAcquireCsvScratchCold()
        {
            return System.Threading.Interlocked.CompareExchange(ref s_csvScratchBusy, 1, 0) == 0;
        }

        private static void ReleaseCsvScratchCold()
        {
            System.Threading.Volatile.Write(ref s_csvScratchBusy, 0);
        }

        private static int ReadCsvBytesCold(string path, byte[] scratch)
        {
            if (string.IsNullOrEmpty(path) || scratch == null || scratch.Length <= 0)
                return 0;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                int byteCount = math.min((int)stream.Length, math.min(scratch.Length, CsvMaxBytes));
                return byteCount > 0 ? stream.Read(scratch, 0, byteCount) : 0;
            }
        }
#endif

        private void WriteEnvironmentSeed(IDataVault vault, float deltaTime, uint frame)
        {
            NativeArray<MockEnvironmentVitalsSignal> environment = OpenPhysiologyVaultArray(ref _environmentHandle, BufferID.ShinobuEnvironmentVitals, entityCapacity);
            if (!environment.IsCreated || environment.Length <= 0)
                return;

            _playerDepthValid = false;
            float depthMeters = math.max(0f, mockDepthMeters);
            IPlayerRuntimeContext player = _playerContext;
            if (player != null && player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                var snapshotAup = snapshot.Aup;
                if (math.isfinite(snapshotAup.LocalX) &&
                    math.isfinite(snapshotAup.LocalY) &&
                    math.isfinite(snapshotAup.LocalZ))
                {
                    double3 playerAup = snapshotAup.ToAbsoluteDouble3();
                    if (math.all(math.isfinite(playerAup)))
                    {
                        double resolvedSeaLevelAupY = ResolveRuntimeSeaLevelAupY();
                        double3 seaLevelAup = default;
                        seaLevelAup.x = playerAup.x;
                        seaLevelAup.y = resolvedSeaLevelAupY;
                        seaLevelAup.z = playerAup.z;
                        double depth = ResolveDepthMetersFromAup(playerAup, seaLevelAup);
                        if (math.isfinite(depth))
                        {
                            depthMeters = (float)math.clamp(depth, 0d, 12000d);
                            _playerDepthValid = true;
                        }
                    }
                }
            }

            float ascentRate = 0f;
            if (_previousDepthValid)
            {
                float inverseDt = math.rcp(math.max(0.0001f, deltaTime));
                ascentRate = math.max(0f, (_previousDepthMeters - depthMeters) * inverseDt);
            }

            _previousDepthMeters = depthMeters;
            _previousDepthValid = true;

            UpdateHabitatRoomState();
            float ambientPressureAtm = ShinobuPhysiologyJobMath.DepthToPressureAtm(depthMeters);
            uint flags = _playerDepthValid ? 2u : MockPressureSignal.ActiveFlag;
            if (TryResolveHabitatAmbientPressure(out float habitatPressureAtm))
            {
                ambientPressureAtm = habitatPressureAtm;
                flags |= MockPressureSignal.HabitatOverrideFlag;
                if (habitatPressureAtm > ShinobuPhysiologyConstants.AtmosphericPressureAtSurfaceAtm)
                    flags |= MockPressureSignal.HyperbaricTreatmentFlag;
            }

            environment[0] = new MockEnvironmentVitalsSignal
            {
                DepthMeters = depthMeters,
                AmbientPressureAtm = ambientPressureAtm,
                AmbientTemperatureCelsius = math.isfinite(mockAmbientTemperatureCelsius) ? mockAmbientTemperatureCelsius : 2f,
                SystemHealthIndex01 = ResolveSystemHealthIndex01(),
                InventoryMask = 0u,
                Frame = frame,
                Flags = flags,
                AscentRateMetersPerSecond = ascentRate
            };
        }

        private uint RefreshPlayerToxicityTargetHash()
        {
            IPlayerRuntimeContext player = _playerContext;
            GameObject playerObject = player != null ? player.PlayerObject : null;
            if (playerObject != null)
            {
                uint entityHash = unchecked((uint)EntityId.ToULong(playerObject.GetEntityId()));
                if (entityHash != 0u)
                    _playerToxicityTargetHash = entityHash;
            }

            return _playerToxicityTargetHash != 0u
                ? _playerToxicityTargetHash
                : ShinobuPhysiologyConstants.PlayerTargetHash;
        }

        private static void IngestAtmosphereToxicitySignals(
            NativeArray<MockToxemiaSignal> toxemia,
            uint playerTargetHash,
            uint frame,
            float deltaTime)
        {
            if (!toxemia.IsCreated || toxemia.Length <= 0)
                return;

            ReadOnlySpan<ToxicityExposureSignal> signals = SignalBus<ToxicityExposureSignal>.GetFrameSnapshot();
            if (signals.Length <= 0)
                return;

            float safeDeltaTime = math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(deltaTime, 0f));
            float toxemiaDelta = 0f;
            for (int i = 0; i < signals.Length; i++)
            {
                ToxicityExposureSignal signal = signals[i];
                uint entityId = signal.EntityId;
                if (entityId == 0u)
                    continue;
                if (entityId != playerTargetHash && entityId != ShinobuPhysiologyConstants.PlayerTargetHash)
                    continue;

                float exposure = ShinobuPhysiologyJobMath.SanitizeUnit(signal.Exposure01);
                float explicitDelta = math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(signal.ToxemiaDelta, 0f));
                float fallbackDelta = exposure * safeDeltaTime * ToxicityExposureFallbackDeltaScalePerSecond;
                float delta = math.saturate(explicitDelta > 0f ? explicitDelta : fallbackDelta);
                if (exposure <= 0.0001f && delta <= 0f)
                    continue;

                toxemiaDelta = math.saturate(toxemiaDelta + delta);
            }

            if (toxemiaDelta <= 0f)
                return;

            MockToxemiaSignal pending = toxemia[0];
            if ((pending.Flags & 2u) != 0u)
            {
                pending.Absolute01 = math.saturate(
                    ShinobuPhysiologyJobMath.SanitizeUnit(pending.Absolute01) + toxemiaDelta);
                pending.Flags |= 3u;
            }
            else
            {
                pending.Delta01 = math.saturate(
                    math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(pending.Delta01, 0f)) + toxemiaDelta);
                pending.Flags = 1u;
            }

            pending.Frame = frame;
            toxemia[0] = pending;
        }

        private static void IngestRadiationDoseSignals(
            NativeArray<MockCombatDamageSignal> combat,
            uint frame)
        {
            if (!combat.IsCreated || combat.Length <= 0)
                return;

            ReadOnlySpan<RadiationDoseSignal> signals = SignalBus<RadiationDoseSignal>.GetFrameSnapshot();
            if (signals.Length <= 0)
                return;

            float severity01 = 0f;
            for (int i = 0; i < signals.Length; i++)
            {
                RadiationDoseSignal signal = signals[i];
                float dose = math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(signal.Dose, 0f));
                float intensity = ShinobuPhysiologyJobMath.SanitizeUnit(signal.Intensity01);
                if (dose <= 0f && intensity <= 0.0001f)
                    continue;

                float doseSeverity01 = RadiationDoseSignal.DoseToUnit01(dose);
                severity01 = math.max(severity01, math.max(intensity, doseSeverity01));
            }

            if (severity01 <= 0.0001f)
                return;

            MockCombatDamageSignal pending = combat[0];
            if ((pending.Flags & 1u) == 0u)
                pending.TraumaType = 6;

            pending.Severity01 = math.max(ShinobuPhysiologyJobMath.SanitizeUnit(pending.Severity01), severity01);
            pending.Frame = frame;
            pending.Flags |= 1u;
            pending.SourceHash = PhysiologyStateSignal.SourceShinobuPhysiology;
            pending.CombatStatusMask |= ShinobuCombatStatusBridgeBits.Irradiated;
            combat[0] = pending;
        }

        private void UpdateHabitatRoomState()
        {
            ReadOnlySpan<PlayerBaseExitSignal> exits = SignalBus<PlayerBaseExitSignal>.GetFrameSnapshot();
            for (int i = 0; i < exits.Length; i++)
            {
                if (!_insideHabitat)
                    continue;
                if (exits[i].RoomId == _activeHabitatRoomId || exits[i].RoomId < 0)
                {
                    _insideHabitat = false;
                    _activeHabitatRoomId = -1;
                }
            }

            ReadOnlySpan<PlayerBaseEnterSignal> enters = SignalBus<PlayerBaseEnterSignal>.GetFrameSnapshot();
            for (int i = 0; i < enters.Length; i++)
            {
                if (enters[i].RoomId < 0)
                    continue;
                _insideHabitat = true;
                _activeHabitatRoomId = enters[i].RoomId;
            }
        }

        private bool TryResolveHabitatAmbientPressure(out float ambientPressureAtm)
        {
            ambientPressureAtm = 0f;
            IGasDynamicsSolver gas = _gasDynamics;
            if (!_insideHabitat || gas == null || !gas.IsInitialized || _activeHabitatRoomId < 0)
                return false;

            if (!gas.TryGetRoomSnapshot(_activeHabitatRoomId, out GasRoomSnapshot snapshot))
                return false;

            float pressureKPa = snapshot.PressureKPa;
            if (!math.isfinite(pressureKPa) || pressureKPa <= 0f)
                return false;

            ambientPressureAtm = math.max(0.5f, pressureKPa * math.rcp(KilopascalsPerAtmosphere));
            return true;
        }

        private static double ResolveDepthMetersFromAup(double3 playerAup, double3 seaLevelAup)
        {
            double3 delta = seaLevelAup - playerAup;
            return delta.y;
        }

        private double ResolveRuntimeSeaLevelAupY()
        {
            IHectonOceanKinematicsService oceanKinematicsService = _oceanKinematics;
            IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null && oceanKinematicsService.IsInitialized
                ? oceanKinematicsService.ActiveProvider
                : null;
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TryResolveSeaLevelAupY(oceanKinematics.SeaLevel, out double seaLevelAupY))
            {
                return seaLevelAupY;
            }

            return ResolveSeaLevelAupY(this.seaLevelAupY);
        }

        private static bool TryResolveSeaLevelAupY(float candidateSeaLevelY, out double seaLevelAupY)
        {
            if (math.isfinite(candidateSeaLevelY) &&
                math.abs(candidateSeaLevelY) <= Hecton8.World.WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                seaLevelAupY = candidateSeaLevelY;
                return true;
            }

            seaLevelAupY = DefaultSeaLevelAupY;
            return false;
        }

        private static double ResolveSeaLevelAupY(double candidateSeaLevelAupY)
        {
            return math.isfinite(candidateSeaLevelAupY) &&
                   math.abs(candidateSeaLevelAupY) > 0.0001d &&
                   math.abs(candidateSeaLevelAupY) <= 1000d
                ? candidateSeaLevelAupY
                : DefaultSeaLevelAupY;
        }

        private float ResolveGlobalQualityWeight()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, AuthoritativeQualityWeight);

            float weight = HomeostasisBrain.GlobalQualityWeight;
            return MathLodApproximation.SaturateFinite(weight, AuthoritativeQualityWeight);
        }

        private float UpdateSmoothedGlobalQualityWeight(float deltaTime)
        {
            float target = ResolveGlobalQualityWeight();
            float response = target < _smoothedGlobalQualityWeight ? 4f : 1f;
            _smoothedGlobalQualityWeight = math.lerp(
                _smoothedGlobalQualityWeight,
                target,
                math.saturate(deltaTime * response));
            return _smoothedGlobalQualityWeight;
        }

        private static float ResolvePhysiologyUpdateIntervalSeconds(float globalQualityWeight)
        {
            float q = MathLodApproximation.SmoothStep01(
                MathLodApproximation.SaturateFinite(globalQualityWeight, AuthoritativeQualityWeight));
            return math.lerp(
                ShinobuPhysiologyConstants.MaxSimulationStepSeconds,
                AuthoritativeUpdateIntervalSeconds,
                q);
        }

        private static float ResolveSystemHealthIndex01()
        {
            return math.saturate(HomeostasisBrain.SystemHealthIndex01);
        }

        private void TryFinalizeFrameJobNoWait()
        {
            if (!_jobScheduled)
                return;

            if (!_activeJobHandle.IsCompleted)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _activeJobHandle))
                return;

            FinishFrameJobCompletion();
        }

        private void CompleteFrameJobForTeardown()
        {
            if (!_jobScheduled)
                return;

            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                if (!DispatcherJobFence.TryComplete(ref _activeJobHandle, forceComplete: true))
                    return;
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }

            FinishFrameJobCompletion();
        }

        private void FinishFrameJobCompletion()
        {
            float elapsedMicroseconds = ResolveElapsedMicroseconds(_jobScheduleTimestamp, Stopwatch.GetTimestamp());

            try
            {
                IDataVault vault = _dataVault;
                if (vault != null)
                {
                    PatchLatestTelemetryExecutionTime(vault, elapsedMicroseconds);
                    PublishSurvivalVitals(vault);
                    PublishOxygenCriticalBridge(vault);
                    PublishVisualSyncScalars(vault);
                    TryDumpAutopsyIfFatal(vault);
                }
            }
            finally
            {
                UnlockJobBuffers();
                _telemetryCursor++;
                if (_telemetryCursor >= ShinobuPhysiologyConstants.TelemetryFrameCount)
                    _telemetryCursor %= ShinobuPhysiologyConstants.TelemetryFrameCount;
                _decompressionTelemetryCursor++;
                if (_decompressionTelemetryCursor >= ShinobuPhysiologyConstants.TelemetryFrameCount)
                    _decompressionTelemetryCursor %= ShinobuPhysiologyConstants.TelemetryFrameCount;
                _scheduledCount = 0;
                _jobScheduled = false;
            }
        }

        private static float ResolveElapsedMicroseconds(long startTimestamp, long endTimestamp)
        {
            long rawDelta = endTimestamp - startTimestamp;
            long delta = rawDelta > 0L ? rawDelta : 0L;
            double microseconds = delta * 1000000.0 / Stopwatch.Frequency;
            return math.isfinite(microseconds) ? (float)math.min(microseconds, float.MaxValue) : 0f;
        }

        private void PatchLatestTelemetryExecutionTime(IDataVault vault, float elapsedMicroseconds)
        {
            NativeArray<PhysiologyTelemetryEntry> telemetry = OpenPhysiologyVaultArray(ref _telemetryHandle, BufferID.ShinobuPhysiologyTelemetryRing, ShinobuPhysiologyConstants.TelemetryFrameCount);
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            int telemetryIndex = _telemetryCursor % telemetry.Length;
            PhysiologyTelemetryEntry entry = telemetry[telemetryIndex];
            entry.ExecutionMicroseconds = math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(elapsedMicroseconds, 0f));
            telemetry[telemetryIndex] = entry;

            NativeArray<DecompressionTelemetryEntry> decompressionTelemetry = OpenPhysiologyVaultArray(ref _decompressionTelemetryHandle, ShinobuPhysiologyConstants.DecompressionTelemetryRingBuffer, ShinobuPhysiologyConstants.TelemetryFrameCount);
            if (!decompressionTelemetry.IsCreated || decompressionTelemetry.Length <= 0)
                return;

            int decompressionIndex = _decompressionTelemetryCursor % decompressionTelemetry.Length;
            DecompressionTelemetryEntry decompressionEntry = decompressionTelemetry[decompressionIndex];
            decompressionEntry.ExecutionMicroseconds = entry.ExecutionMicroseconds;
            if (!math.isfinite(decompressionEntry.ExecutionMicroseconds) ||
                decompressionEntry.ExecutionMicroseconds >= ShinobuPhysiologyConstants.TelemetryDumpBudgetMicroseconds)
            {
                decompressionEntry.FatalFlags |= ShinobuPhysiologyFlags.InvalidMath;
            }

            decompressionTelemetry[decompressionIndex] = decompressionEntry;
        }

        private void PublishSurvivalVitals(IDataVault vault)
        {
            NativeArray<VitalsExportDTO> exports = OpenPhysiologyVaultArray(ref _exportHandle, BufferID.ShinobuVitalsExport, entityCapacity);
            if (!exports.IsCreated || exports.Length <= 0)
                return;

            VitalsExportDTO export = exports[0];
            uint flags = SurvivalVitalsChangedSignalFlags.Oxygen |
                         SurvivalVitalsChangedSignalFlags.Temperature |
                         SurvivalVitalsChangedSignalFlags.Depth |
                         SurvivalVitalsChangedSignalFlags.Pressure;
            if ((export.StatusMask & ShinobuPhysiologyFlags.OxygenCritical) != 0u)
                flags |= SurvivalVitalsChangedSignalFlags.OxygenCritical;
            if ((export.StatusMask & (ShinobuPhysiologyFlags.Bends |
                                      ShinobuPhysiologyFlags.Hypothermia |
                                      ShinobuPhysiologyFlags.CarbonDioxideToxicity |
                                      ShinobuPhysiologyFlags.CnsOxygenToxicity |
                                      ShinobuPhysiologyFlags.Hypoxia)) != 0u)
                flags |= SurvivalVitalsChangedSignalFlags.Injury | SurvivalVitalsChangedSignalFlags.Thermal;
            if ((export.StatusMask & (ShinobuPhysiologyFlags.FatalOxygen | ShinobuPhysiologyFlags.FatalGasToxicity)) != 0u)
                flags |= SurvivalVitalsChangedSignalFlags.Death;

            SurvivalVitalsChangedSignal signal = default;
            signal.SourceId = ShinobuPhysiologyConstants.SourceHash;
            signal.Frame = _simulationFrameCounter;
            signal.Sequence = unchecked((uint)_telemetryCursor);
            signal.Flags = flags;
            signal.Oxygen01 = math.saturate(export.BloodOxygen);
            signal.Energy01 = 1f;
            signal.Integrity01 = (export.StatusMask & ShinobuPhysiologyFlags.Bends) != 0u ? 0.65f : 1f;
            signal.DeathCause = (byte)(((export.StatusMask & (ShinobuPhysiologyFlags.FatalOxygen | ShinobuPhysiologyFlags.FatalGasToxicity)) != 0u) ? 1 : 0);
            if (!SurvivalSignalRoute.TryQueueVitals(in signal))
                ReportSurvivalVitalsSignalDrop();
        }

        /// <summary>
        /// Bridges this runtime's oxygen truth onto the one lane the suit-tank survival clock actually reads.
        ///
        /// WHY THIS EXISTS. OxygenConsumptionJob already drains PhysiologyDTO.BloodOxygen every tick, and
        /// PublishSurvivalVitals already reports it - but SurvivalVitalsChangedSignal is consumed only by
        /// VocalWarningSystem, AdaptiveStemAudioMixer and the death recorder in SignalBridgeState, and
        /// HypoxiaSignal has no gameplay consumer at all. Neither lane touches HectonSurvivalSystem's suit
        /// tank (Standard_Suit_V1.asset maxOxygen 139.24). The ONLY lane that does is OxygenCriticalSignal,
        /// min-folded in HectonSurvivalSystem.ConsumeOxygenCriticalSignals - and before this method the only
        /// producer in the project was a predator biting a bio-cable (BioCableIK). So falling blood oxygen
        /// reached the audio mixer and never reached the survival clock.
        ///
        /// WHY THE MIN-FOLD IS THE RIGHT TARGET. ConsumeOxygenCriticalSignals runs immediately AFTER
        /// UpdateOxygen in the same SlowTick, and folds with math.min then clamps with math.max(0f, ...).
        /// That ordering is what makes this survive the recorded surface-refill defect: when a stale movement
        /// handle pins depth at 0, UpdateOxygen takes the "not underwater" branch and refills at 15/s, but a
        /// later min-fold cannot be outrun by an earlier refill. Draining through any earlier branch could be.
        ///
        /// Edge-gated on purpose: pushed only while a critical/hypoxic/fatal oxygen bit is set, and then only
        /// when oxygen has fallen a further OxygenCriticalRepublishEpsilon, so neither the 32-slot lane nor
        /// the OxygenLow voice queue is spammed at tick rate.
        ///
        /// SourceId is this runtime's own hash, which also keeps FluidPipeGraphRuntime out of it - that
        /// consumer early-continues on any SourceId that is not SourceBioCablePredatorBite, so the
        /// life-support-cutoff branch there cannot be tripped by physiology.
        /// </summary>
        private void PublishOxygenCriticalBridge(IDataVault vault)
        {
            _ = vault;

            NativeArray<VitalsExportDTO> exports = OpenPhysiologyVaultArray(ref _exportHandle, BufferID.ShinobuVitalsExport, entityCapacity);
            if (!exports.IsCreated || exports.Length <= 0)
                return;

            VitalsExportDTO export = exports[0];
            uint statusMask = export.StatusMask;
            bool oxygenCritical = (statusMask & (ShinobuPhysiologyFlags.OxygenCritical |
                                                 ShinobuPhysiologyFlags.Hypoxia |
                                                 ShinobuPhysiologyFlags.FatalOxygen)) != 0u;
            if (!oxygenCritical)
            {
                // Recovered (or never critical): drop the latch so the next descent re-publishes from scratch.
                _oxygenCriticalLatched = false;
                _lastPublishedOxygenCritical01 = -1f;
                return;
            }

            float oxygen01 = math.saturate(ShinobuPhysiologyJobMath.SanitizeUnit(export.BloodOxygen));
            if (_oxygenCriticalLatched && oxygen01 >= _lastPublishedOxygenCritical01 - OxygenCriticalRepublishEpsilon)
                return;

            // Real countdown, not a placeholder. OxygenDrainPerSecond is the rate OxygenConsumptionJob already
            // resolved this tick from heart rate, adrenaline, trauma, toxemia, shiver, ambient pressure and
            // stamina drain, in the same 0-1 units as BloodOxygen - so the quotient is seconds.
            NativeArray<PhysiologyScalarsDTO> scalars = OpenPhysiologyVaultArray(ref _scalarHandle, BufferID.ShinobuPhysiologyScalars, entityCapacity);
            float drainPerSecond = scalars.IsCreated && scalars.Length > 0
                ? math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(scalars[0].OxygenDrainPerSecond, 0f))
                : 0f;
            float secondsRemaining = drainPerSecond > OxygenCriticalMinDrainPerSecond
                ? math.min(oxygen01 * math.rcp(drainPerSecond), OxygenCriticalUnknownSecondsRemaining)
                : OxygenCriticalUnknownSecondsRemaining;

            OxygenCriticalSignal signal = default;

            // Oxygen01 IS DELIBERATELY NOT BLOOD SATURATION. Publishing it as such was a units bug.
            //
            // The one consumer that moves suit oxygen does
            // `targetOxygen = math.min(targetOxygen, maxOxygen * oxygen01)` (HectonSurvivalSystem.cs:1222),
            // where maxOxygen is the suit TANK capacity - 139.24 from Standard_Suit_V1.asset. This host's
            // export.BloodOxygen is an SpO2-like blood SATURATION fraction. Those are different physical
            // quantities, and multiplying one by the other is not a conversion.
            //
            // What it would have done: OxygenConsumptionJob raises OxygenCritical at
            // `BloodOxygen <= 0.18f || hypoxia01 > 0f` (ShinobuPhysiologyJobs.cs:1338-1340). At
            // BloodOxygen = 0.18 the consumer would clamp a FULL tank from 139.24 to 25.06 in one step -
            // 82% of the tank deleted because blood saturation fell. The causality is also backwards: an
            // empty tank should drive SpO2 down, not the reverse. And the gate is looser than 0.18, since
            // the Hypoxia flag is set whenever hypoxia01 > 0f (:1144), so mild hypoxia with BloodOxygen
            // still near 0.99 opens the same path.
            //
            // So the TANK-CLAMP channel is neutralised with 1f, making the consumer's min-fold a no-op,
            // while the WARNING channel is preserved: VocalWarningSystem folds
            // math.max(1 - Oxygen01, Severity/255) at :2704 and Severity below still carries the real
            // blood-oxygen deficit. Physiology therefore reaches the lane it was previously absent from -
            // which was the point of the wiring - without corrupting a quantity it has no authority over.
            //
            // The correct long-term wiring runs the other way: tank contents should feed blood saturation.
            // That needs a tank reader this host does not have, so it is queued rather than guessed at.
            signal.Oxygen01 = 1f;
            signal.SecondsRemaining = secondsRemaining;
            signal.SourceId = ShinobuPhysiologyConstants.SourceHash;
            signal.Frame = _simulationFrameCounter;
            // 0-255 scale, matching this file's existing HypoxiaSignal push. VocalWarningSystem folds
            // Severity/255 against (1 - Oxygen01) with math.max, so the scale choice cannot lose information.
            signal.Severity = (byte)math.round(math.saturate(1f - oxygen01) * 255f);
            signal.Flags = (byte)(((statusMask & ShinobuPhysiologyFlags.FatalOxygen) != 0u)
                ? OxygenCriticalSignal.FlagLifeSupportCutoff
                : 0);

            if (!SignalBus<OxygenCriticalSignal>.TryPushTracked(in signal, ref s_x001ShinobuPhysiologyRuntimeSignalPushDropCount))
            {
                // TryPushTracked already counted the drop - calling ReportSurvivalVitalsSignalDrop here would
                // double-count it (that helper exists for SurvivalSignalRoute, which does not take the counter).
                // Returning without touching the latch is what makes a drop recoverable: _lastPublishedOxygenCritical01
                // stays stale, so the next completed tick re-evaluates and re-publishes the same edge.
                return;
            }

            _oxygenCriticalLatched = true;
            _lastPublishedOxygenCritical01 = oxygen01;
        }

        private static void ReportSurvivalVitalsSignalDrop()
        {
            int dropCount = System.Threading.Interlocked.Increment(ref s_x001ShinobuPhysiologyRuntimeSignalPushDropCount);
            GlobalTelemetryBus.PublishPerformanceWarning(
                _SurvivalVitalsQueueDropWarningHash,
                _SurvivalVitalsQueueContextHash,
                math.max(1, dropCount));
        }

        private void PublishVisualSyncScalars(IDataVault vault)
        {
            NativeArray<GasPhysiologyStateDTO> gasStates = OpenPhysiologyVaultArray(ref _gasStateHandle, ShinobuPhysiologyConstants.GasPhysiologyStatesBuffer, entityCapacity);
            if (!gasStates.IsCreated || gasStates.Length <= 0)
                return;

            GasPhysiologyStateDTO gas = gasStates[0];
            float hypoxia01 = ShinobuPhysiologyJobMath.ResolveHypoxiaTunnel01(gas.OxygenPartialPressure);
            if (hypoxia01 > 0f)
            {
                HypoxiaSignal signal = default;
                signal.Oxygen01 = ShinobuPhysiologyJobMath.ResolveOxygenAvailability01(gas.OxygenPartialPressure);
                signal.SecondsRemaining = math.rcp(math.max(0.001f, hypoxia01));
                signal.SourceId = ShinobuPhysiologyConstants.SourceHash;
                signal.Frame = _simulationFrameCounter;
                signal.Severity = (byte)math.round(math.saturate(hypoxia01) * 255f);
                signal.Flags = 1;
                SignalBus<HypoxiaSignal>.TryPushTracked(in signal, ref s_x001ShinobuPhysiologyRuntimeSignalPushDropCount);
            }
        }

        private void TryDumpAutopsyIfFatal(IDataVault vault)
        {
            if (_autopsyDumped)
                return;

            NativeArray<PhysiologyDTO> vitals = OpenPhysiologyVaultArray(ref _vitalsHandle, BufferID.ShinobuPhysiologyVitals, entityCapacity);
            NativeArray<PhysiologyTelemetryEntry> telemetry = OpenPhysiologyVaultArray(ref _telemetryHandle, BufferID.ShinobuPhysiologyTelemetryRing, ShinobuPhysiologyConstants.TelemetryFrameCount);
            NativeArray<DecompressionTelemetryEntry> decompressionTelemetry = OpenPhysiologyVaultArray(ref _decompressionTelemetryHandle, ShinobuPhysiologyConstants.DecompressionTelemetryRingBuffer, ShinobuPhysiologyConstants.TelemetryFrameCount);
            if (!vitals.IsCreated || vitals.Length <= 0 || !telemetry.IsCreated || telemetry.Length <= 0 || !decompressionTelemetry.IsCreated || decompressionTelemetry.Length <= 0)
                return;

            bool fatal = vitals[0].BloodOxygen <= ShinobuPhysiologyConstants.OxygenDeathThreshold;
            if (!fatal)
            {
                int latestIndex = _telemetryCursor % telemetry.Length;
                PhysiologyTelemetryEntry latest = telemetry[latestIndex];
                int decompressionIndex = _decompressionTelemetryCursor % decompressionTelemetry.Length;
                DecompressionTelemetryEntry decompressionLatest = decompressionTelemetry[decompressionIndex];
                fatal = (latest.FatalFlags &
                         (ShinobuPhysiologyFlags.FatalOxygen |
                          ShinobuPhysiologyFlags.InvalidMath |
                          ShinobuPhysiologyFlags.FatalBends |
                          ShinobuPhysiologyFlags.FatalGasToxicity)) != 0u ||
                        !math.isfinite(latest.ExecutionMicroseconds) ||
                        latest.ExecutionMicroseconds >= ShinobuPhysiologyConstants.TelemetryDumpBudgetMicroseconds ||
                        (decompressionLatest.FatalFlags &
                         (ShinobuPhysiologyFlags.InvalidMath |
                          ShinobuPhysiologyFlags.FatalBends)) != 0u ||
                        !math.isfinite(decompressionLatest.ExecutionMicroseconds) ||
                        !math.isfinite(decompressionLatest.LeadingTissueTensionAtm) ||
                        !math.isfinite(decompressionLatest.MValueGradientAtm) ||
                        decompressionLatest.ExecutionMicroseconds >= ShinobuPhysiologyConstants.TelemetryDumpBudgetMicroseconds;
            }

            if (!fatal)
                return;

            _autopsyDumped = DumpAutopsyReport(telemetry, decompressionTelemetry);
        }

        private bool DumpAutopsyReport(NativeArray<PhysiologyTelemetryEntry> telemetry, NativeArray<DecompressionTelemetryEntry> decompressionTelemetry)
        {
            if (!telemetry.IsCreated || !decompressionTelemetry.IsCreated)
                return false;

            try
            {
                int telemetryByteCount = telemetry.Length * UnsafeUtility.SizeOf<PhysiologyTelemetryEntry>();
                int decompressionByteCount = decompressionTelemetry.Length * UnsafeUtility.SizeOf<DecompressionTelemetryEntry>();
                int totalBytes = 48 + telemetryByteCount + decompressionByteCount;
                NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                    totalBytes,
                    nameof(ShinobuPhysiologyRuntime),
                    "shinobuPhysiologyAutopsyPayload");
                try
                {
                    byte* payloadPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                    Span<byte> header = new Span<byte>(payloadPtr, 48);
                    WriteUInt64LittleEndian(header.Slice(0, 8), DumpMagic);
                    WriteUInt32LittleEndian(header.Slice(8, 4), DumpVersion);
                    WriteUInt32LittleEndian(header.Slice(12, 4), (uint)telemetry.Length);
                    WriteUInt32LittleEndian(header.Slice(16, 4), (uint)UnsafeUtility.SizeOf<PhysiologyTelemetryEntry>());
                    WriteUInt32LittleEndian(header.Slice(20, 4), unchecked((uint)_telemetryCursor));
                    WriteUInt32LittleEndian(header.Slice(24, 4), ShinobuPhysiologyConstants.SourceHash);
                    WriteUInt32LittleEndian(header.Slice(28, 4), _simulationFrameCounter);
                    WriteUInt32LittleEndian(header.Slice(32, 4), (uint)decompressionTelemetry.Length);
                    WriteUInt32LittleEndian(header.Slice(36, 4), (uint)UnsafeUtility.SizeOf<DecompressionTelemetryEntry>());
                    WriteUInt32LittleEndian(header.Slice(40, 4), unchecked((uint)_decompressionTelemetryCursor));
                    WriteUInt32LittleEndian(header.Slice(44, 4), unchecked((uint)ShinobuPhysiologyConstants.DecompressionTelemetryRingBuffer));

                    void* telemetryPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                    UnsafeUtility.MemCpy(payloadPtr + 48, telemetryPtr, telemetryByteCount);
                    void* decompressionTelemetryPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(decompressionTelemetry);
                    UnsafeUtility.MemCpy(payloadPtr + 48 + telemetryByteCount, decompressionTelemetryPtr, decompressionByteCount);
                    return NativeFaultDumpWriter.TryWriteAll(_dumpPath, payload, totalBytes);
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref payload,
                        nameof(ShinobuPhysiologyRuntime),
                        "shinobuPhysiologyAutopsyPayload");
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

#if UNITY_EDITOR
        private bool TryLoadLegacyMetabolismTables(IDataVault vault)
        {
            NativeArray<HaldaneTissueCoefficientDTO> sourceCoefficients = OpenPhysiologyVaultArray(ref _coefficientHandle, BufferID.ShinobuHaldaneCoefficients, ShinobuPhysiologyConstants.TissueCompartmentCount);
            if (!sourceCoefficients.IsCreated || sourceCoefficients.Length < ShinobuPhysiologyConstants.TissueCompartmentCount)
                return false;

            string streamingAssets = Path.Combine(Application.dataPath, "StreamingAssets");
            string halfTimePath = Path.Combine(streamingAssets, LegacyMetabolismFile);
            string mValuePath = Path.Combine(streamingAssets, LegacyMValueFile);
            if (!File.Exists(halfTimePath) && !File.Exists(mValuePath))
                return false;

            if (!TryAcquireCsvScratchCold())
                return false;

            bool anyLoaded = false;
            try
            {
                Span<HaldaneTissueCoefficientDTO> coefficientScratch = s_coefficientScratchCold;
                for (int i = 0; i < ShinobuPhysiologyConstants.TissueCompartmentCount; i++)
                    coefficientScratch[i] = sourceCoefficients[i];

                if (File.Exists(halfTimePath))
                    anyLoaded |= TryReadLegacyFloatTable(halfTimePath, s_csvScratchCold, coefficientScratch, readHalfTimes: true);
                if (File.Exists(mValuePath))
                    anyLoaded |= TryReadLegacyFloatTable(mValuePath, s_csvScratchCold, coefficientScratch, readHalfTimes: false);

                return anyLoaded && CommitLegacyMetabolismTables(vault, coefficientScratch);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            finally
            {
                ReleaseCsvScratchCold();
            }
        }

        private bool TryReadLegacyFloatTable(
            string path,
            byte[] scratch,
            Span<HaldaneTissueCoefficientDTO> coefficients,
            bool readHalfTimes)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int byteCount = math.min((int)stream.Length, ShinobuPhysiologyConstants.TissueCompartmentCount * 4);
                if (byteCount < ShinobuPhysiologyConstants.TissueCompartmentCount * 4 || byteCount > scratch.Length)
                    return false;

                int read = stream.Read(scratch, 0, byteCount);
                if (read < ShinobuPhysiologyConstants.TissueCompartmentCount * 4)
                    return false;

                ReadOnlySpan<byte> buffer = new ReadOnlySpan<byte>(scratch, 0, read);
                bool bigEndian = ShouldReadLegacyTableAsBigEndian(buffer, readHalfTimes);
                for (int i = 0; i < ShinobuPhysiologyConstants.TissueCompartmentCount; i++)
                {
                    HaldaneTissueCoefficientDTO coefficient = coefficients[i];
                    float value = ReadFloatEndianAware(buffer, i * 4, bigEndian);
                    if (!IsPlausibleLegacyFloat(value, readHalfTimes))
                        return false;

                    if (readHalfTimes)
                    {
                        float seconds = math.max(1f, value);
                        coefficient.HalfTimeSeconds = seconds;
                        coefficient.K = 0.69314718056f * math.rcp(seconds);
                        if (coefficient.BuhlmannA <= 0f)
                            coefficient.BuhlmannA = ResolveEmergencyBuhlmannA(i);
                        if (coefficient.BuhlmannB <= 0f)
                            coefficient.BuhlmannB = ResolveEmergencyBuhlmannB(i);
                        coefficient.NitrogenFraction = ShinobuPhysiologyConstants.NitrogenFraction;
                        if (coefficient.MValueRatio <= 0f)
                            coefficient.MValueRatio = ResolveEmergencyMValueRatio(i);
                    }
                    else
                    {
                        coefficient.MValueRatio = math.max(1.01f, value);
                        if (coefficient.BuhlmannA <= 0f)
                            coefficient.BuhlmannA = ResolveEmergencyBuhlmannA(i);
                        if (coefficient.BuhlmannB <= 0f)
                            coefficient.BuhlmannB = ResolveEmergencyBuhlmannB(i);
                        if (coefficient.HalfTimeSeconds <= 0f)
                        {
                            float seconds = ResolveEmergencyHalfTimeSeconds(i);
                            coefficient.HalfTimeSeconds = seconds;
                            coefficient.K = 0.69314718056f * math.rcp(seconds);
                            coefficient.NitrogenFraction = ShinobuPhysiologyConstants.NitrogenFraction;
                        }
                    }

                    coefficients[i] = coefficient;
                }
            }

            return true;
        }

        private bool CommitLegacyMetabolismTables(IDataVault vault, ReadOnlySpan<HaldaneTissueCoefficientDTO> coefficients)
        {
            if (vault == null)
                return false;

            NativeArray<HaldaneTissueCoefficientDTO> coefficientArray = OpenPhysiologyVaultArray(ref _coefficientHandle, BufferID.ShinobuHaldaneCoefficients, ShinobuPhysiologyConstants.TissueCompartmentCount);
            if (!coefficientArray.IsCreated || coefficientArray.Length < ShinobuPhysiologyConstants.TissueCompartmentCount)
                return false;

            int count = math.min(coefficients.Length, coefficientArray.Length);
            NativeArray<PhysiologyTuningDTO> tuning = OpenPhysiologyVaultArray(ref _tuningHandle, BufferID.ShinobuPhysiologyTuning, 1);
            byte hasTuning = 0;
            PhysiologyTuningDTO tuningRow = default;
            if (tuning.IsCreated && tuning.Length > 0)
            {
                tuningRow = ShinobuPhysiologyJobMath.SanitizeTuning(tuning[0]);
                tuningRow.Flags |= ShinobuPhysiologyFlags.EmergencyMockCoefficients;
                hasTuning = 1;
            }

            if (!vault.TryAcquireMutationGuard(EmergencyMetabolismMutationGuardMask))
                return false;

            try
            {
                for (int i = 0; i < count; i++)
                    coefficientArray[i] = coefficients[i];

                if (hasTuning != 0)
                    tuning[0] = tuningRow;

                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(EmergencyMetabolismMutationGuardMask);
            }
        }
#endif

        private void GenerateEmergencyMockMetabolism(IDataVault vault)
        {
            if (vault == null || !vault.TryAcquireMutationGuard(EmergencyMetabolismMutationGuardMask))
                return;

            try
            {
            NativeArray<HaldaneTissueCoefficientDTO> coefficients = OpenPhysiologyVaultArray(ref _coefficientHandle, BufferID.ShinobuHaldaneCoefficients, ShinobuPhysiologyConstants.TissueCompartmentCount);
            if (!coefficients.IsCreated)
                return;

            int count = math.min(ShinobuPhysiologyConstants.TissueCompartmentCount, coefficients.Length);
            for (int i = 0; i < count; i++)
            {
                float seconds = ResolveEmergencyHalfTimeSeconds(i);
                coefficients[i] = new HaldaneTissueCoefficientDTO
                {
                    HalfTimeSeconds = seconds,
                    K = 0.69314718056f * math.rcp(seconds),
                    BuhlmannA = ResolveEmergencyBuhlmannA(i),
                    BuhlmannB = ResolveEmergencyBuhlmannB(i),
                    MValueRatio = ResolveEmergencyMValueRatio(i),
                    NitrogenFraction = ShinobuPhysiologyConstants.NitrogenFraction,
                    CompartmentHash = unchecked((uint)(0x5A483136u + (uint)i)),
                    Flags = ShinobuPhysiologyFlags.EmergencyMockCoefficients
                };
            }

            NativeArray<PhysiologyTuningDTO> tuning = OpenPhysiologyVaultArray(ref _tuningHandle, BufferID.ShinobuPhysiologyTuning, 1);
            if (tuning.IsCreated && tuning.Length > 0)
            {
                PhysiologyTuningDTO row = ShinobuPhysiologyJobMath.SanitizeTuning(tuning[0]);
                row.Flags |= ShinobuPhysiologyFlags.EmergencyMockCoefficients;
                tuning[0] = row;
            }
            }
            finally
            {
                vault.ReleaseMutationGuard(EmergencyMetabolismMutationGuardMask);
            }
        }

        private static float ResolveEmergencyHalfTimeSeconds(int index)
        {
            return ShinobuPhysiologyJobMath.ResolveEmergencyHalfTimeSeconds(index);
        }

        private static float ResolveEmergencyBuhlmannA(int index)
        {
            return ShinobuPhysiologyJobMath.ResolveEmergencyBuhlmannA(index);
        }

        private static float ResolveEmergencyBuhlmannB(int index)
        {
            return ShinobuPhysiologyJobMath.ResolveEmergencyBuhlmannB(index);
        }

        private static float ResolveEmergencyMValueRatio(int index)
        {
            return ShinobuPhysiologyJobMath.ResolveEmergencyMValueRatio(index);
        }

#if UNITY_EDITOR
        private void LoadCsvOverridesFromDisk(IDataVault vault)
        {
            LoadGasCsvOverridesFromDisk(vault);
            if (string.IsNullOrEmpty(_csvPath) || !File.Exists(_csvPath))
                return;

            DateTime stamp = File.GetLastWriteTimeUtc(_csvPath);
            if (stamp.Ticks == 0L || stamp.Ticks == _csvLastWriteTicks)
                return;

            if (!TryAcquireCsvScratchCold())
                return;

            try
            {
                int read = ReadCsvBytesCold(_csvPath, s_csvScratchCold);
                if (read <= 0)
                    return;

                ReadOnlySpan<byte> buffer = new ReadOnlySpan<byte>(s_csvScratchCold, 0, read);
                if (!TryParseBiologyConstantsCsv(
                    vault,
                    buffer,
                    out PhysiologyTuningDTO tuning,
                    s_csvOverrideScratchCold,
                    out int overrideCount,
                    s_coefficientScratchCold,
                    s_tissueOverrideScratchCold))
                {
                    return;
                }

                if (!CommitBiologyConstantsCsv(
                    vault,
                    in tuning,
                    s_csvOverrideScratchCold,
                    overrideCount,
                    s_coefficientScratchCold,
                    s_tissueOverrideScratchCold))
                {
                    return;
                }

                _csvLastWriteTicks = stamp.Ticks;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            finally
            {
                ReleaseCsvScratchCold();
            }
        }

        private void LoadGasCsvOverridesFromDisk(IDataVault vault)
        {
            if (string.IsNullOrEmpty(_gasCsvPath) || !File.Exists(_gasCsvPath))
                return;

            DateTime stamp = File.GetLastWriteTimeUtc(_gasCsvPath);
            if (stamp.Ticks == 0L || stamp.Ticks == _gasCsvLastWriteTicks)
                return;

            if (!TryAcquireCsvScratchCold())
                return;

            try
            {
                int read = ReadCsvBytesCold(_gasCsvPath, s_csvScratchCold);
                if (read <= 0)
                    return;

                ReadOnlySpan<byte> buffer = new ReadOnlySpan<byte>(s_csvScratchCold, 0, read);
                if (!TryParseGasProfilesCsv(vault, buffer, out BreathingGasFractionsDTO gas, out GasPhysiologyTuningDTO gasTuning))
                    return;

                if (!CommitGasProfilesCsv(vault, in gasTuning))
                    return;

                _breathingGasOverride = ShinobuPhysiologyJobMath.SanitizeBreathingGas(gas);
                _breathingGasOverrideActive = true;
                _gasCsvLastWriteTicks = stamp.Ticks;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            finally
            {
                ReleaseCsvScratchCold();
            }
        }

        private bool TryParseGasProfilesCsv(
            IDataVault vault,
            ReadOnlySpan<byte> bytes,
            out BreathingGasFractionsDTO gas,
            out GasPhysiologyTuningDTO gasTuning)
        {
            gas = _breathingGasOverrideActive
                ? _breathingGasOverride
                : new BreathingGasFractionsDTO
                {
                    OxygenFraction = ShinobuPhysiologyConstants.OxygenFraction,
                    NitrogenFraction = ShinobuPhysiologyConstants.NitrogenFraction,
                    CarbonDioxideFraction = ShinobuPhysiologyConstants.CarbonDioxideFraction,
                    GasHash = 0x43535631u,
                    Flags = ShinobuPhysiologyFlags.CsvOverride
                };
            if (!TryReadPhysiologyVaultArray(in _gasTuningHandle, ShinobuPhysiologyConstants.GasPhysiologyTuningBuffer, 1, out NativeArray<GasPhysiologyTuningDTO> tuningArray))
            {
                gasTuning = default;
                return false;
            }

            gasTuning = ShinobuPhysiologyJobMath.SanitizeGasTuning(tuningArray[0]);

            int cursor = 0;
            while (cursor < bytes.Length)
            {
                int lineStart = cursor;
                while (cursor < bytes.Length && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                    cursor++;

                int lineEnd = cursor;
                while (cursor < bytes.Length && (bytes[cursor] == (byte)'\n' || bytes[cursor] == (byte)'\r'))
                    cursor++;

                ParseGasCsvLine(bytes.Slice(lineStart, lineEnd - lineStart), ref gas, ref gasTuning);
            }

            gas.Flags |= ShinobuPhysiologyFlags.CsvOverride;
            gas = ShinobuPhysiologyJobMath.SanitizeBreathingGas(gas);
            gasTuning = ShinobuPhysiologyJobMath.SanitizeGasTuning(gasTuning);
            return true;
        }

        private bool CommitGasProfilesCsv(IDataVault vault, in GasPhysiologyTuningDTO gasTuning)
        {
            if (vault == null)
                return false;

            GasPhysiologyTuningDTO sanitizedGasTuning = ShinobuPhysiologyJobMath.SanitizeGasTuning(gasTuning);
            NativeArray<GasPhysiologyTuningDTO> tuningArray = OpenPhysiologyVaultArray(ref _gasTuningHandle, ShinobuPhysiologyConstants.GasPhysiologyTuningBuffer, 1);
            if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                return false;

            if (!vault.TryAcquireMutationGuard(GasCsvMutationGuardMask))
                return false;

            try
            {
                tuningArray[0] = sanitizedGasTuning;
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(GasCsvMutationGuardMask);
            }
        }

        private static void ParseGasCsvLine(ReadOnlySpan<byte> line, ref BreathingGasFractionsDTO gas, ref GasPhysiologyTuningDTO gasTuning)
        {
            int keyStart = 0;
            while (keyStart < line.Length && IsCsvSpace(line[keyStart]))
                keyStart++;
            if (keyStart >= line.Length || line[keyStart] == (byte)'#')
                return;

            int separator = keyStart;
            while (separator < line.Length && line[separator] != (byte)',' && line[separator] != (byte)'=')
                separator++;
            if (separator >= line.Length)
                return;

            int keyEnd = separator - 1;
            while (keyEnd >= keyStart && IsCsvSpace(line[keyEnd]))
                keyEnd--;
            if (keyEnd < keyStart)
                return;

            int valueStart = separator + 1;
            while (valueStart < line.Length && IsCsvSpace(line[valueStart]))
                valueStart++;

            int valueEnd = valueStart;
            while (valueEnd < line.Length && line[valueEnd] != (byte)',')
                valueEnd++;
            if (!TryParseAsciiFloat(line.Slice(valueStart, valueEnd - valueStart), out float value))
                return;

            uint keyHash = HashLowerAscii(line.Slice(keyStart, keyEnd - keyStart + 1));
            if (keyHash == _OxygenFractionHash || keyHash == _Fo2Hash)
                gas.OxygenFraction = value;
            else if (keyHash == _NitrogenFractionHash || keyHash == _Fn2Hash)
                gas.NitrogenFraction = value;
            else if (keyHash == _CarbonDioxideFractionHash || keyHash == _Fco2Hash)
                gas.CarbonDioxideFraction = value;
            else if (keyHash == _CnsToxicityRateHash)
                gasTuning.CnsAccumulationRate = value;
            else if (keyHash == _CnsExtremeRateHash)
                gasTuning.CnsExtremeRate = value;
            else if (keyHash == _NarcosisThresholdHash)
                gasTuning.NarcosisStartAtm = value;
            else if (keyHash == _HypoxiaLimitHash || keyHash == _HypoxiaPpo2Hash)
                gasTuning.HypoxiaPartialPressureAtm = value;
            else if (keyHash == _AnoxiaLimitHash || keyHash == _AnoxiaPpo2Hash)
                gasTuning.AnoxiaPartialPressureAtm = value;
            else if (keyHash == _Co2ToxicityStartHash)
                gasTuning.CarbonDioxideToxicityStartAtm = value;
            else if (keyHash == _Co2ToxicityFullHash)
                gasTuning.CarbonDioxideToxicityFullAtm = value;
        }

        private bool TryParseBiologyConstantsCsv(
            IDataVault vault,
            ReadOnlySpan<byte> bytes,
            out PhysiologyTuningDTO tuning,
            Span<BiologyConstantOverrideDTO> overrides,
            out int overrideIndex,
            Span<HaldaneTissueCoefficientDTO> coefficients,
            Span<TissueCsvOverrideScratch> tissueOverrides)
        {
            tuning = default;
            overrideIndex = 0;
            if (!TryReadPhysiologyVaultArray(in _tuningHandle, BufferID.ShinobuPhysiologyTuning, 1, out NativeArray<PhysiologyTuningDTO> tuningArray) ||
                !TryReadPhysiologyVaultArray(in _coefficientHandle, BufferID.ShinobuHaldaneCoefficients, ShinobuPhysiologyConstants.TissueCompartmentCount, out NativeArray<HaldaneTissueCoefficientDTO> sourceCoefficients))
            {
                return false;
            }

            tuning = ShinobuPhysiologyJobMath.SanitizeTuning(tuningArray[0]);
            int coefficientCount = math.min(ShinobuPhysiologyConstants.TissueCompartmentCount, coefficients.Length);
            for (int i = 0; i < coefficientCount; i++)
                coefficients[i] = sourceCoefficients[i];
            for (int i = 0; i < overrides.Length; i++)
                overrides[i] = default;
            for (int i = 0; i < tissueOverrides.Length; i++)
                tissueOverrides[i] = default;

            int cursor = 0;
            while (cursor < bytes.Length)
            {
                int lineStart = cursor;
                while (cursor < bytes.Length && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                    cursor++;

                int lineEnd = cursor;
                while (cursor < bytes.Length && (bytes[cursor] == (byte)'\n' || bytes[cursor] == (byte)'\r'))
                    cursor++;

                ParseCsvLine(bytes.Slice(lineStart, lineEnd - lineStart), ref tuning, overrides, coefficients, tissueOverrides, ref overrideIndex);
            }

            tuning.Flags |= ShinobuPhysiologyFlags.CsvOverride;
            tuning = ShinobuPhysiologyJobMath.SanitizeTuning(tuning);
            return true;
        }

        private bool CommitBiologyConstantsCsv(
            IDataVault vault,
            in PhysiologyTuningDTO tuning,
            ReadOnlySpan<BiologyConstantOverrideDTO> overrides,
            int overrideCount,
            ReadOnlySpan<HaldaneTissueCoefficientDTO> coefficients,
            ReadOnlySpan<TissueCsvOverrideScratch> tissueOverrides)
        {
            if (vault == null)
                return false;

            PhysiologyTuningDTO sanitizedTuning = ShinobuPhysiologyJobMath.SanitizeTuning(tuning);
            NativeArray<PhysiologyTuningDTO> tuningArray = OpenPhysiologyVaultArray(ref _tuningHandle, BufferID.ShinobuPhysiologyTuning, 1);
            NativeArray<BiologyConstantOverrideDTO> overrideArray = OpenPhysiologyVaultArray(ref _csvOverrideHandle, BufferID.ShinobuBiologyCsvOverrides, CsvOverrideCapacity);
            NativeArray<HaldaneTissueCoefficientDTO> coefficientArray = OpenPhysiologyVaultArray(ref _coefficientHandle, BufferID.ShinobuHaldaneCoefficients, ShinobuPhysiologyConstants.TissueCompartmentCount);
            NativeArray<TissueCompartmentDTO> tissueArray = OpenPhysiologyVaultArray(ref _tissueHandle, BufferID.ShinobuTissueCompartments, entityCapacity * ShinobuPhysiologyConstants.TissueCompartmentCount);
            if (!tuningArray.IsCreated || tuningArray.Length <= 0 ||
                !overrideArray.IsCreated ||
                !coefficientArray.IsCreated ||
                coefficientArray.Length <= 0)
            {
                return false;
            }

            int safeOverrideCount = math.min(math.max(0, overrideCount), math.min(overrides.Length, overrideArray.Length));
            int coefficientCount = math.min(coefficients.Length, coefficientArray.Length);

            if (!vault.TryAcquireMutationGuard(BiologyCsvMutationGuardMask))
                return false;

            try
            {
                tuningArray[0] = sanitizedTuning;

                for (int i = 0; i < safeOverrideCount; i++)
                    overrideArray[i] = overrides[i];
                for (int i = safeOverrideCount; i < overrideArray.Length; i++)
                    overrideArray[i] = default;

                for (int i = 0; i < coefficientCount; i++)
                    coefficientArray[i] = coefficients[i];

                if (tissueArray.IsCreated && tissueArray.Length > 0)
                    CommitTissueCsvOverrides(tissueArray, tissueOverrides);

                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(BiologyCsvMutationGuardMask);
            }
        }

        private static void CommitTissueCsvOverrides(
            NativeArray<TissueCompartmentDTO> tissues,
            ReadOnlySpan<TissueCsvOverrideScratch> tissueOverrides)
        {
            int tissueCount = math.min(ShinobuPhysiologyConstants.TissueCompartmentCount, tissueOverrides.Length);
            for (int index = 0; index < tissueCount; index++)
            {
                TissueCsvOverrideScratch overrideRow = tissueOverrides[index];
                if (overrideRow.HasOverride == 0)
                    continue;

                for (int tissueIndex = index; tissueIndex < tissues.Length; tissueIndex += ShinobuPhysiologyConstants.TissueCompartmentCount)
                {
                    TissueCompartmentDTO tissue = tissues[tissueIndex];
                    tissue.Halftime = overrideRow.Halftime;
                    tissue.MValue = overrideRow.MValue;
                    tissue.Flags |= ShinobuPhysiologyFlags.CsvOverride;
                    tissues[tissueIndex] = tissue;
                }
            }
        }

        private static void ParseCsvLine(
            ReadOnlySpan<byte> line,
            ref PhysiologyTuningDTO tuning,
            Span<BiologyConstantOverrideDTO> overrides,
            Span<HaldaneTissueCoefficientDTO> coefficients,
            Span<TissueCsvOverrideScratch> tissueOverrides,
            ref int overrideIndex)
        {
            int keyStart = 0;
            while (keyStart < line.Length && IsCsvSpace(line[keyStart]))
                keyStart++;
            if (keyStart >= line.Length || line[keyStart] == (byte)'#')
                return;

            int separator = keyStart;
            while (separator < line.Length && line[separator] != (byte)',' && line[separator] != (byte)'=')
                separator++;
            if (separator >= line.Length)
                return;

            int keyEnd = separator - 1;
            while (keyEnd >= keyStart && IsCsvSpace(line[keyEnd]))
                keyEnd--;
            if (keyEnd < keyStart)
                return;

            int valueStart = separator + 1;
            while (valueStart < line.Length && IsCsvSpace(line[valueStart]))
                valueStart++;

            int valueEnd = valueStart;
            while (valueEnd < line.Length && line[valueEnd] != (byte)',')
                valueEnd++;
            if (!TryParseAsciiFloat(line.Slice(valueStart, valueEnd - valueStart), out float value))
                return;

            uint keyHash = HashLowerAscii(line.Slice(keyStart, keyEnd - keyStart + 1));
            if (valueEnd < line.Length)
            {
                int secondStart = valueEnd + 1;
                while (secondStart < line.Length && IsCsvSpace(line[secondStart]))
                    secondStart++;
                int secondEnd = secondStart;
                while (secondEnd < line.Length && line[secondEnd] != (byte)',')
                    secondEnd++;
                if (TryParseAsciiFloat(line.Slice(secondStart, secondEnd - secondStart), out float secondValue))
                {
                    float thirdValue = 0f;
                    byte hasThirdValue = 0;
                    if (secondEnd < line.Length)
                    {
                        int thirdStart = secondEnd + 1;
                        while (thirdStart < line.Length && IsCsvSpace(line[thirdStart]))
                            thirdStart++;
                        hasThirdValue = TryParseAsciiFloat(line.Slice(thirdStart), out thirdValue) ? (byte)1 : (byte)0;
                    }

                    ApplyTissueCsvOverride(
                        line.Slice(keyStart, keyEnd - keyStart + 1),
                        keyHash,
                        value,
                        secondValue,
                        thirdValue,
                        hasThirdValue,
                        coefficients,
                        tissueOverrides);
                    WriteCsvOverride(overrides, ref overrideIndex, keyHash, value);
                    return;
                }
            }

            ApplyCsvOverride(keyHash, value, ref tuning);
            WriteCsvOverride(overrides, ref overrideIndex, keyHash, value);
        }

        private static void WriteCsvOverride(
            Span<BiologyConstantOverrideDTO> overrides,
            ref int overrideIndex,
            uint keyHash,
            float value)
        {
            if (overrideIndex < overrides.Length)
            {
                overrides[overrideIndex++] = new BiologyConstantOverrideDTO
                {
                    KeyHash = keyHash,
                    Value = value,
                    Flags = 1u
                };
            }
        }

        private static void ApplyTissueCsvOverride(
            ReadOnlySpan<byte> key,
            uint keyHash,
            float halfTimeSeconds,
            float secondValue,
            float thirdValue,
            byte hasBuhlmannColumns,
            Span<HaldaneTissueCoefficientDTO> coefficients,
            Span<TissueCsvOverrideScratch> tissueOverrides)
        {
            if (coefficients.Length <= 0)
                return;

            int index = ResolveCsvCompartmentIndex(key, keyHash);
            if ((uint)index >= (uint)coefficients.Length)
                return;

            float safeHalfTime = math.max(1f, ShinobuPhysiologyJobMath.SanitizeFinite(halfTimeSeconds, ResolveEmergencyHalfTimeSeconds(index)));
            float safeA = hasBuhlmannColumns != 0
                ? math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(secondValue, ResolveEmergencyBuhlmannA(index)))
                : ResolveEmergencyBuhlmannA(index);
            float safeB = hasBuhlmannColumns != 0
                ? math.clamp(ShinobuPhysiologyJobMath.SanitizeFinite(thirdValue, ResolveEmergencyBuhlmannB(index)), 0.1f, 2f)
                : ResolveEmergencyBuhlmannB(index);
            float safeMValue = hasBuhlmannColumns != 0
                ? ResolveEmergencyMValueRatio(index)
                : math.max(1.01f, ShinobuPhysiologyJobMath.SanitizeFinite(secondValue, ResolveEmergencyMValueRatio(index)));
            coefficients[index] = new HaldaneTissueCoefficientDTO
            {
                HalfTimeSeconds = safeHalfTime,
                K = 0.69314718056f * math.rcp(safeHalfTime),
                BuhlmannA = safeA,
                BuhlmannB = safeB,
                MValueRatio = safeMValue,
                NitrogenFraction = ShinobuPhysiologyConstants.NitrogenFraction,
                CompartmentHash = keyHash,
                Flags = ShinobuPhysiologyFlags.CsvOverride
            };

            if ((uint)index < (uint)tissueOverrides.Length)
            {
                tissueOverrides[index] = new TissueCsvOverrideScratch
                {
                    Halftime = safeHalfTime,
                    MValue = safeB,
                    HasOverride = 1
                };
            }
        }

        private static int ResolveCsvCompartmentIndex(ReadOnlySpan<byte> key, uint keyHash)
        {
            int multiplier = 1;
            int value = 0;
            bool foundDigit = false;
            for (int i = key.Length - 1; i >= 0; i--)
            {
                byte c = key[i];
                if (c < (byte)'0' || c > (byte)'9')
                    break;

                foundDigit = true;
                value += (c - (byte)'0') * multiplier;
                multiplier *= 10;
            }

            if (foundDigit)
                return value;

            return (int)(keyHash & 0x0Fu);
        }

        private static void ApplyCsvOverride(uint keyHash, float value, ref PhysiologyTuningDTO tuning)
        {
            if (keyHash == _BaseO2DrainHash)
                tuning.BaseO2DrainPerSecond = value;
            else if (keyHash == _NitrogenUptakeHash)
                tuning.NitrogenUptakeRate = value;
            else if (keyHash == _AdrenalineDecayHash)
                tuning.AdrenalineDecaySeconds = value;
            else if (keyHash == _HypothermiaCoolingHash)
                tuning.HypothermiaCoolingRate = value;
            else if (keyHash == _MValueStrictnessHash)
                tuning.BendsRiskScale = value;
            else if (keyHash == _OffGassingMultiplierHash)
                tuning.HaldaneTimeScale = value;
            else if (keyHash == _NarcosisThresholdHash)
                tuning.NarcosisStartAtm = value;
        }
#endif

        // Everything from here to ClearCachedHandles is runtime lifecycle - DataVault mutation
        // guards, dispatcher tick registration, hot-swap listener wiring and handle teardown - and
        // is called from unguarded code above. It sat inside the editor-only CSV override block, so
        // a player build had no job-buffer locking and no tick registration for physiology at all.
        // Keep this block outside the guard. CSV parsing stays inside it, per
        // TOOL_Designer_Facades_CSV_Binary_Bridge.txt.
        private bool TryLockJobBuffers(IDataVault vault)
        {
            if (vault == null || _jobLocksHeld)
                return false;

            if (!vault.TryAcquireMutationGuard(JobMutationGuardMask))
                return false;

            _jobLocksHeld = true;
            return true;
        }

        private void UnlockJobBuffers()
        {
            if (!_jobLocksHeld)
                return;

            _jobLocksHeld = false;
            IDataVault vault = _dataVault;
            if (vault != null)
                vault.ReleaseMutationGuard(JobMutationGuardMask);
        }

        private static ulong MutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private void TryRegisterTicks()
        {
            if (!_registeredSlow)
                _registeredSlow = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);

            if (!_registeredSlow || !_registeredLateFrame)
                TryUnregisterTicks();
        }

        private void TryUnregisterTicks()
        {
            if (_registeredSlow)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
                _registeredSlow = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrame = false;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void ClearCachedHandles()
        {
            _vitalsHandle = default;
            _decompressionHandle = default;
            _tissueHandle = default;
            _coefficientHandle = default;
            _environmentHandle = default;
            _scalarHandle = default;
            _statusEffectHandle = default;
            _gasStateHandle = default;
            _breathingGasHandle = default;
            _gasTuningHandle = default;
            _exportHandle = default;
            _telemetryHandle = default;
            _decompressionTelemetryHandle = default;
            _pulseHandle = default;
            _toxemiaHandle = default;
            _pressureHandle = default;
            _combatHandle = default;
            _predatorHandle = default;
            _medicalHandle = default;
            _tuningHandle = default;
#if UNITY_EDITOR
            _csvOverrideHandle = default;
#endif
            _mockDiveProfileHandle = default;
            _simulationAccumulator = 0f;
            _previousDepthValid = false;
            _insideHabitat = false;
            _activeHabitatRoomId = -1;
            _decompressionTelemetryCursor = 0;
            // A vault hot-swap or teardown invalidates the oxygen-critical edge history; keeping the latch
            // would suppress the first re-publish after the rebind and silently re-open the gap this bridge closes.
            _oxygenCriticalLatched = false;
            _lastPublishedOxygenCritical01 = -1f;
        }

#if UNITY_EDITOR
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsCsvSpace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }

        private static bool TryParseAsciiFloat(ReadOnlySpan<byte> bytes, out float value)
        {
            value = 0f;
            int cursor = 0;
            while (cursor < bytes.Length && IsCsvSpace(bytes[cursor]))
                cursor++;

            float sign = 1f;
            if (cursor < bytes.Length && bytes[cursor] == (byte)'-')
            {
                sign = -1f;
                cursor++;
            }
            else if (cursor < bytes.Length && bytes[cursor] == (byte)'+')
            {
                cursor++;
            }

            float whole = 0f;
            bool hasDigit = false;
            while (cursor < bytes.Length && bytes[cursor] >= (byte)'0' && bytes[cursor] <= (byte)'9')
            {
                hasDigit = true;
                whole = whole * 10f + (bytes[cursor] - (byte)'0');
                cursor++;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (cursor < bytes.Length && bytes[cursor] == (byte)'.')
            {
                cursor++;
                while (cursor < bytes.Length && bytes[cursor] >= (byte)'0' && bytes[cursor] <= (byte)'9')
                {
                    hasDigit = true;
                    fraction = fraction * 10f + (bytes[cursor] - (byte)'0');
                    divisor *= 10f;
                    cursor++;
                }
            }

            if (!hasDigit)
                return false;

            value = sign * (whole + fraction * math.rcp(math.max(1f, divisor)));
            return math.isfinite(value);
        }

        private static uint HashLowerAscii(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
            {
                byte value = bytes[i];
                if (value >= (byte)'A' && value <= (byte)'Z')
                    value = (byte)(value + 32);
                hash ^= value;
                hash *= 16777619u;
            }

            return hash;
        }
#endif

        private static uint HashLowerAsciiString(string value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);
                hash ^= c;
                hash *= 16777619u;
            }

            return hash;
        }

#if UNITY_EDITOR
        private static bool ShouldReadLegacyTableAsBigEndian(ReadOnlySpan<byte> bytes, bool readHalfTimes)
        {
            float little = ReadFloatEndianAware(bytes, 0, bigEndian: false);
            if (IsPlausibleLegacyFloat(little, readHalfTimes))
                return false;

            float big = ReadFloatEndianAware(bytes, 0, bigEndian: true);
            return IsPlausibleLegacyFloat(big, readHalfTimes);
        }

        private static bool IsPlausibleLegacyFloat(float value, bool readHalfTimes)
        {
            if (!math.isfinite(value) || value <= 0f)
                return false;

            return readHalfTimes
                ? value >= 1f && value <= 1000000f
                : value >= 1.01f && value <= 10f;
        }

        private static float ReadFloatEndianAware(ReadOnlySpan<byte> bytes, int offset, bool bigEndian)
        {
            uint raw = (uint)(bytes[offset] |
                              (bytes[offset + 1] << 8) |
                              (bytes[offset + 2] << 16) |
                              (bytes[offset + 3] << 24));
            if (bigEndian)
                raw = ReverseUInt32(raw);
            return math.asfloat(raw);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReverseUInt32(uint value)
        {
            return (value >> 24) |
                   ((value >> 8) & 0x0000FF00u) |
                   ((value << 8) & 0x00FF0000u) |
                   (value << 24);
        }
#endif

        private static void WriteUInt32LittleEndian(Span<byte> destination, uint value)
        {
            destination[0] = (byte)value;
            destination[1] = (byte)(value >> 8);
            destination[2] = (byte)(value >> 16);
            destination[3] = (byte)(value >> 24);
        }

        private static void WriteUInt64LittleEndian(Span<byte> destination, ulong value)
        {
            destination[0] = (byte)value;
            destination[1] = (byte)(value >> 8);
            destination[2] = (byte)(value >> 16);
            destination[3] = (byte)(value >> 24);
            destination[4] = (byte)(value >> 32);
            destination[5] = (byte)(value >> 40);
            destination[6] = (byte)(value >> 48);
            destination[7] = (byte)(value >> 56);
        }
    
        #region JulesLink_NitrogenNarcosisModel
        private static void JulesLink_NitrogenNarcosisModel() { _ = typeof(Hecton8.PureLogic.Systems.NitrogenNarcosisModel); }
        #endregion

        #region JulesLink_HeartRateExertionModel
        private static void JulesLink_HeartRateExertionModel() { _ = typeof(Hecton8.PureLogic.Systems.HeartRateExertionModel); }
        #endregion

        #region JulesLink_SuitO2ConsumptionModel
        private static void JulesLink_SuitO2ConsumptionModel() { _ = typeof(Hecton8.PureLogic.Systems.SuitO2ConsumptionModel); }
        #endregion

        #region JulesLink_Co2ScrubberEfficiencyModel
        private static void JulesLink_Co2ScrubberEfficiencyModel() { _ = typeof(Hecton8.PureLogic.Systems.Co2ScrubberEfficiencyModel); }
        #endregion
}
}
