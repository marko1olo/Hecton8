using System;
using System.IO;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
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
    public sealed unsafe class ShinobuPhysiologyRuntime : MonoBehaviour, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const SystemID OwnerSystem = SystemID.GameplayPlayer;
        private const int CsvMaxBytes = 8192;
        private const int CsvOverrideCapacity = 32;
        private const int LockBufferCount = 14;
        private const float CsvPollIntervalSeconds = 1f;
        private const float KilopascalsPerAtmosphere = 101.325f;
        private const string CsvRelativePath = "tissue_halftime_profiles.csv";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_PHYSIOLOGY_SURGEON.bin";
        private const string LegacyMetabolismFile = "metabolism_rates.h8bin";
        private const string LegacyMValueFile = "haldane_m-values.bin";
        private const ulong DumpMagic = 0x5348494E4F425532UL; // SHINOBU2
        private const uint DumpVersion = 1u;

        private static readonly uint _BaseO2DrainHash = HashLowerAsciiString("base_o2_drain");
        private static readonly uint _NitrogenUptakeHash = HashLowerAsciiString("nitrogen_uptake_rate");
        private static readonly uint _AdrenalineDecayHash = HashLowerAsciiString("adrenaline_decay");
        private static readonly uint _HypothermiaCoolingHash = HashLowerAsciiString("hypothermia_cooling_rate");
        private static readonly uint _MValueStrictnessHash = HashLowerAsciiString("m_value_strictness");
        private static readonly uint _OffGassingMultiplierHash = HashLowerAsciiString("offgassing_multiplier");
        private static readonly uint _NarcosisThresholdHash = HashLowerAsciiString("narcosis_threshold");

        [Header("Runtime Capacity")]
        [Tooltip("Maximum player or humanoid rows simulated by the physiology jobs.")]
        [SerializeField, Min(1)] private int entityCapacity = ShinobuPhysiologyConstants.DefaultEntityCapacity;

        [Header("Vacuum Mock")]
        [Tooltip("Fallback pressure depth used when no player/world pressure data exists.")]
        [SerializeField, Min(0f)] private float mockDepthMeters = 100f;

        [Tooltip("Sea-level Y in AUP meters; depth is computed in double precision before conversion to float.")]
        [SerializeField] private double seaLevelAupY;

        [Tooltip("Fallback thermal environment used by the mock pressure lane.")]
        [SerializeField] private float mockAmbientTemperatureCelsius = 2f;

        private VaultBufferHandle<PhysiologyDTO> _vitalsHandle;
        private VaultBufferHandle<DecompressionStateDTO> _decompressionHandle;
        private VaultBufferHandle<TissueCompartmentDTO> _tissueHandle;
        private VaultBufferHandle<HaldaneTissueCoefficientDTO> _coefficientHandle;
        private VaultBufferHandle<MockEnvironmentVitalsSignal> _environmentHandle;
        private VaultBufferHandle<PhysiologyScalarsDTO> _scalarHandle;
        private VaultBufferHandle<VitalsExportDTO> _exportHandle;
        private VaultBufferHandle<PhysiologyTelemetryEntry> _telemetryHandle;
        private VaultBufferHandle<CardiacPulseStateDTO> _pulseHandle;
        private VaultBufferHandle<MockToxemiaSignal> _toxemiaHandle;
        private VaultBufferHandle<MockPressureSignal> _pressureHandle;
        private VaultBufferHandle<MockCombatDamageSignal> _combatHandle;
        private VaultBufferHandle<MockPredatorAggroSignal> _predatorHandle;
        private VaultBufferHandle<MockMedicalItemUsedSignal> _medicalHandle;
        private VaultBufferHandle<PhysiologyTuningDTO> _tuningHandle;
        private VaultBufferHandle<BiologyConstantOverrideDTO> _csvOverrideHandle;
        private VaultBufferHandle<DiveProfileSampleDTO> _mockDiveProfileHandle;
        private VaultBufferHandle<byte> _csvScratchHandle;

        private IDataVault _dataVault;
        private IPlayerRuntimeContext _playerContext;
        private IGasDynamicsSolver _gasDynamics;
        private JobHandle _activeJobHandle;
        private string _csvPath;
        private string _dumpPath;
        private int _telemetryCursor;
        private int _scheduledCount;
        private uint _simulationFrameCounter;
        private long _jobScheduleTimestamp;
        private long _csvLastWriteTicks;
        private float _csvPollTimer;
        private float _simulationAccumulator;
        private float _smoothedGlobalQualityWeight = 1f;
        private bool _registeredUpdate;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _jobScheduled;
        private bool _jobLocksHeld;
        private bool _defaultsInitialized;
        private bool _autopsyDumped;
        private bool _playerDepthValid;
        private bool _previousDepthValid;
        private bool _insideHabitat;
        private int _activeHabitatRoomId = -1;
        private float _previousDepthMeters;

        private void Awake()
        {
            entityCapacity = math.max(1, entityCapacity);
            _csvPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", CsvRelativePath));
            _dumpPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", DumpRelativePath));
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            SignalBus<CardiacPulseSignal>.Configure(16, maxFrameSignals: 64, lowTierFrameSignals: 16, laneHash: CardiacPulseSignal.LaneHash);
            SignalBus<CardiacPulseSignal>.EnsureInitialized();
            SignalBus<PhysiologyStateSignal>.Configure(64, maxFrameSignals: 64, lowTierFrameSignals: 32, laneHash: PhysiologyStateSignal.LaneHash);
            SignalBus<PhysiologyStateSignal>.EnsureInitialized();
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
            CompleteFrameJob(forceComplete: true);
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
                CompleteFrameJob(forceComplete: true);
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
            else if (serviceSlot == GlobalRegistryServiceSlot.GasDynamicsRuntime)
                _gasDynamics = currentService as IGasDynamicsSolver;
        }

        public void Tick(float deltaTime)
        {
            if (_jobScheduled)
                return;

            IDataVault vault = _dataVault;
            if (vault == null || !EnsureVaultState())
                return;

            float sourceDt = math.clamp(deltaTime, 0.0001f, ShinobuPhysiologyConstants.MaxSimulationStepSeconds);
            float globalQualityWeight = ResolveSmoothedGlobalQualityWeight(sourceDt);
            MonitorCsvOverrides(sourceDt, vault);

            _simulationAccumulator = math.min(
                _simulationAccumulator + sourceDt,
                ShinobuPhysiologyConstants.MaxSimulationStepSeconds);
            float updateInterval = ResolvePhysiologyUpdateIntervalSeconds(globalQualityWeight);
            if (_simulationAccumulator < updateInterval)
                return;

            float dt = math.clamp(_simulationAccumulator, 0.0001f, ShinobuPhysiologyConstants.MaxSimulationStepSeconds);
            uint frame = ++_simulationFrameCounter;
            WriteEnvironmentSeed(vault, dt, frame);

            if (!TryResolveBuffers(
                    vault,
                    out NativeArray<PhysiologyDTO> vitals,
                    out NativeArray<DecompressionStateDTO> decompression,
                    out NativeArray<TissueCompartmentDTO> tissues,
                    out NativeArray<HaldaneTissueCoefficientDTO> coefficients,
                    out NativeArray<MockEnvironmentVitalsSignal> environment,
                    out NativeArray<PhysiologyScalarsDTO> scalars,
                    out NativeArray<VitalsExportDTO> exports,
                    out NativeArray<PhysiologyTelemetryEntry> telemetry,
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

            int count = math.min(entityCapacity, vitals.Length);
            count = math.min(count, decompression.Length);
            count = math.min(count, tissues.Length / ShinobuPhysiologyConstants.TissueCompartmentCount);
            count = math.min(count, environment.Length);
            count = math.min(count, scalars.Length);
            count = math.min(count, exports.Length);
            count = math.min(count, pulses.Length);
            if (count <= 0)
                return;

            if (!TryLockJobBuffers(vault))
                return;

            PhysiologyTuningDTO tuning = ShinobuPhysiologyJobMath.SanitizeTuning(tuningArray[0]);
            tuningArray[0] = tuning;

            long scheduleTimestamp = Stopwatch.GetTimestamp();
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

            handle = new TissueSaturationJob
            {
                Vitals = vitals,
                TissueCompartments = tissues,
                DecompressionStates = decompression,
                Environment = environment,
                Scalars = scalars,
                PhysiologyWriter = SignalBus<PhysiologyStateSignal>.ParallelWriter,
                Tuning = tuning,
                DeltaSeconds = dt,
                GlobalQualityWeight = globalQualityWeight,
                Count = count,
                EmitPhysiologySignal = 1
            }.Schedule(count, ShinobuPhysiologyConstants.FrameJobBatchSize, handle);

            handle = new OxygenConsumptionJob
            {
                Vitals = vitals,
                Scalars = scalars,
                PulseStates = pulses,
                Environment = environment,
                VitalsExport = exports,
                Telemetry = telemetry,
                CardiacPulseWriter = SignalBus<CardiacPulseSignal>.ParallelWriter,
                Tuning = tuning,
                DeltaSeconds = dt,
                Frame = frame,
                TelemetryCursor = _telemetryCursor,
                Count = count,
                EmitPulseSignals = 1
            }.Schedule(count, ShinobuPhysiologyConstants.FrameJobBatchSize, handle);

            _activeJobHandle = handle;
            _scheduledCount = count;
            _jobScheduleTimestamp = scheduleTimestamp;
            _simulationAccumulator = 0f;
            _jobScheduled = true;
            H8Memory.RegisterActiveJob(OwnerSystem, _activeJobHandle);
        }

        public void LateFrameTick()
        {
            CompleteFrameJob(forceComplete: false);
        }

        /// <summary>
        /// Returns a mutable reference to a vault-owned vitals row without CS1612 struct copies.
        /// </summary>
        /// <param name="entityIndex">Entity row index.</param>
        /// <returns>Reference to the exact vault element.</returns>
        public ref PhysiologyDTO GetVitalsRef(int entityIndex)
        {
            IDataVault vault = _dataVault;
            NativeArray<PhysiologyDTO> vitals = vault != null ? _vitalsHandle.Resolve(vault) : default;
            if (!vitals.IsCreated || (uint)entityIndex >= (uint)vitals.Length)
                FatalMemoryException.ThrowStaleVaultHandle();

            void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(vitals);
            return ref UnsafeUtility.ArrayElementAsRef<PhysiologyDTO>(ptr, entityIndex);
        }

        /// <summary>
        /// Injects a mock pressure sample for isolated tests.
        /// </summary>
        public bool InjectMockPressure(int entityIndex, float depthMeters, float ascentRateMetersPerSecond)
        {
            IDataVault vault = _dataVault;
            NativeArray<MockPressureSignal> pressure = vault != null ? _pressureHandle.Resolve(vault) : default;
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
            IDataVault vault = _dataVault;
            NativeArray<MockPressureSignal> pressure = vault != null ? _pressureHandle.Resolve(vault) : default;
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
            IDataVault vault = _dataVault;
            NativeArray<DiveProfileSampleDTO> samples = vault != null ? _mockDiveProfileHandle.Resolve(vault) : default;
            if (!samples.IsCreated || samples.Length <= 0)
                return false;

            JobHandle handle = new MockDiveProfileJob
            {
                Samples = samples,
                SampleStepSeconds = 10f,
                Frame = _simulationFrameCounter,
                Count = samples.Length
            }.Schedule(samples.Length, ShinobuPhysiologyConstants.FrameJobBatchSize);
            // COLD SYNC JOB: explicit smoke-test profile generation, not part of the frame simulation chain.
            handle.Complete();
            return true;
        }

        /// <summary>
        /// Injects a mock trauma bit.
        /// </summary>
        public bool InjectMockCombatDamage(int entityIndex, int traumaType, float severity01)
        {
            IDataVault vault = _dataVault;
            NativeArray<MockCombatDamageSignal> combat = vault != null ? _combatHandle.Resolve(vault) : default;
            if (!combat.IsCreated || (uint)entityIndex >= (uint)combat.Length)
                return false;

            combat[entityIndex] = new MockCombatDamageSignal
            {
                TraumaType = math.clamp(traumaType, 0, 3),
                Severity01 = math.saturate(severity01),
                Frame = _simulationFrameCounter,
                Flags = 1u,
                SourceHash = ShinobuPhysiologyConstants.SourceHash
            };
            return true;
        }

        /// <summary>
        /// Injects predator aggro for adrenaline testing.
        /// </summary>
        public bool InjectMockPredatorAggro(int entityIndex, float aggro01)
        {
            IDataVault vault = _dataVault;
            NativeArray<MockPredatorAggroSignal> predator = vault != null ? _predatorHandle.Resolve(vault) : default;
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
            IDataVault vault = _dataVault;
            NativeArray<MockToxemiaSignal> toxemia = vault != null ? _toxemiaHandle.Resolve(vault) : default;
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
            IDataVault vault = _dataVault;
            NativeArray<MockMedicalItemUsedSignal> medical = vault != null ? _medicalHandle.Resolve(vault) : default;
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
            IDataVault vault = _dataVault;
            NativeArray<PhysiologyTuningDTO> tuningArray = vault != null ? _tuningHandle.Resolve(vault) : default;
            if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                return false;

            tuning = tuningArray[0];
            return true;
        }

        /// <summary>
        /// Applies editor-authored tuning directly to vault memory.
        /// </summary>
        public bool SetEditorTuning(PhysiologyTuningDTO tuning)
        {
            IDataVault vault = _dataVault;
            NativeArray<PhysiologyTuningDTO> tuningArray = vault != null ? _tuningHandle.Resolve(vault) : default;
            if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                return false;

            tuningArray[0] = ShinobuPhysiologyJobMath.SanitizeTuning(tuning);
            return true;
        }

        /// <summary>
        /// Reads one tissue tension and its current M-value limit for editor histograms.
        /// </summary>
        public bool TryGetTissueTension(int entityIndex, int tissueIndex, out float tension, out float mValue)
        {
            tension = 0f;
            mValue = 0f;
            IDataVault vault = _dataVault;
            NativeArray<TissueCompartmentDTO> tissues = vault != null ? _tissueHandle.Resolve(vault) : default;
            NativeArray<MockEnvironmentVitalsSignal> environment = vault != null ? _environmentHandle.Resolve(vault) : default;
            int compartmentIndex = entityIndex * ShinobuPhysiologyConstants.TissueCompartmentCount + tissueIndex;
            if (!tissues.IsCreated ||
                (uint)tissueIndex >= ShinobuPhysiologyConstants.TissueCompartmentCount ||
                (uint)compartmentIndex >= (uint)tissues.Length)
            {
                return false;
            }

            TissueCompartmentDTO tissue = tissues[compartmentIndex];
            tension = tissue.NitrogenTension;
            float ambient = environment.IsCreated && (uint)entityIndex < (uint)environment.Length
                ? math.max(0.5f, environment[entityIndex].AmbientPressureAtm)
                : ShinobuPhysiologyConstants.AtmosphericPressureAtSurfaceAtm;
            mValue = math.max(0.1f, ambient * math.max(1.01f, tissue.MValue));
            return true;
        }

        /// <summary>
        /// Reads the first diegetic vitals export row.
        /// </summary>
        public bool TryGetVitalsExport(out VitalsExportDTO export)
        {
            export = default;
            IDataVault vault = _dataVault;
            NativeArray<VitalsExportDTO> exports = vault != null ? _exportHandle.Resolve(vault) : default;
            if (!exports.IsCreated || exports.Length <= 0)
                return false;

            export = exports[0];
            return true;
        }

        private void RebindColdServices()
        {
            _dataVault = GlobalRegistry.DataVault;
            _playerContext = GlobalRegistry.Player;
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
            if (!ShinobuPhysiologyLayoutGuards.ValidateTissueCompartmentLayout())
                return false;

            _vitalsHandle = vault.GetBufferHandle<PhysiologyDTO>(BufferID.ShinobuPhysiologyVitals, entityCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _decompressionHandle = vault.GetBufferHandle<DecompressionStateDTO>(BufferID.ShinobuDecompressionStates, entityCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _tissueHandle = vault.GetBufferHandle<TissueCompartmentDTO>(BufferID.ShinobuTissueCompartments, entityCapacity * ShinobuPhysiologyConstants.TissueCompartmentCount, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _coefficientHandle = vault.GetBufferHandle<HaldaneTissueCoefficientDTO>(BufferID.ShinobuHaldaneCoefficients, ShinobuPhysiologyConstants.TissueCompartmentCount, OwnerSystem, NativeArrayOptions.ClearMemory);
            _environmentHandle = vault.GetBufferHandle<MockEnvironmentVitalsSignal>(BufferID.ShinobuEnvironmentVitals, entityCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _scalarHandle = vault.GetBufferHandle<PhysiologyScalarsDTO>(BufferID.ShinobuPhysiologyScalars, entityCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _exportHandle = vault.GetBufferHandle<VitalsExportDTO>(BufferID.ShinobuVitalsExport, entityCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _telemetryHandle = vault.GetBufferHandle<PhysiologyTelemetryEntry>(BufferID.ShinobuPhysiologyTelemetryRing, ShinobuPhysiologyConstants.TelemetryFrameCount, OwnerSystem, NativeArrayOptions.ClearMemory);
            _pulseHandle = vault.GetBufferHandle<CardiacPulseStateDTO>(BufferID.ShinobuCardiacPulseStates, entityCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _toxemiaHandle = vault.GetBufferHandle<MockToxemiaSignal>(BufferID.ShinobuMockToxemiaSignals, entityCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _pressureHandle = vault.GetBufferHandle<MockPressureSignal>(BufferID.ShinobuMockPressureSignals, entityCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _combatHandle = vault.GetBufferHandle<MockCombatDamageSignal>(BufferID.ShinobuMockCombatDamageSignals, entityCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _predatorHandle = vault.GetBufferHandle<MockPredatorAggroSignal>(BufferID.ShinobuMockPredatorAggroSignals, entityCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _medicalHandle = vault.GetBufferHandle<MockMedicalItemUsedSignal>(BufferID.ShinobuMockMedicalItemSignals, entityCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _tuningHandle = vault.GetBufferHandle<PhysiologyTuningDTO>(BufferID.ShinobuPhysiologyTuning, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            _csvOverrideHandle = vault.GetBufferHandle<BiologyConstantOverrideDTO>(BufferID.ShinobuBiologyCsvOverrides, CsvOverrideCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _mockDiveProfileHandle = vault.GetBufferHandle<DiveProfileSampleDTO>(BufferID.ShinobuMockDiveProfile, ShinobuPhysiologyConstants.TelemetryFrameCount, OwnerSystem, NativeArrayOptions.ClearMemory);
            _csvScratchHandle = vault.GetBufferHandle<byte>(BufferID.ShinobuTissueCsvScratch, CsvMaxBytes, OwnerSystem, NativeArrayOptions.UninitializedMemory);

            if (!HandlesReady())
                return false;

            InitializeDefaults(vault);
            return true;
        }

        private bool HandlesReady()
        {
            return _vitalsHandle.IsCreated &&
                   _decompressionHandle.IsCreated &&
                   _tissueHandle.IsCreated &&
                   _coefficientHandle.IsCreated &&
                   _environmentHandle.IsCreated &&
                   _scalarHandle.IsCreated &&
                   _exportHandle.IsCreated &&
                   _telemetryHandle.IsCreated &&
                   _pulseHandle.IsCreated &&
                   _toxemiaHandle.IsCreated &&
                   _pressureHandle.IsCreated &&
                   _combatHandle.IsCreated &&
                   _predatorHandle.IsCreated &&
                   _medicalHandle.IsCreated &&
                   _tuningHandle.IsCreated &&
                   _csvOverrideHandle.IsCreated &&
                   _mockDiveProfileHandle.IsCreated &&
                   _csvScratchHandle.IsCreated;
        }

        private void InitializeDefaults(IDataVault vault)
        {
            if (_defaultsInitialized)
                return;

            NativeArray<PhysiologyTuningDTO> tuning = _tuningHandle.Resolve(vault);
            if (tuning.IsCreated && tuning.Length > 0)
                tuning[0] = ShinobuPhysiologyJobMath.SanitizeTuning(tuning[0]);

            if (!TryLoadLegacyMetabolismTables(vault))
                GenerateEmergencyMockMetabolism(vault);

            NativeArray<PhysiologyDTO> vitals = _vitalsHandle.Resolve(vault);
            NativeArray<DecompressionStateDTO> states = _decompressionHandle.Resolve(vault);
            NativeArray<TissueCompartmentDTO> tissues = _tissueHandle.Resolve(vault);
            NativeArray<HaldaneTissueCoefficientDTO> coefficients = _coefficientHandle.Resolve(vault);
            NativeArray<PhysiologyScalarsDTO> scalars = _scalarHandle.Resolve(vault);
            NativeArray<CardiacPulseStateDTO> pulses = _pulseHandle.Resolve(vault);
            int count = math.min(entityCapacity, math.min(vitals.Length, states.Length));
            if (tissues.IsCreated)
                count = math.min(count, tissues.Length / ShinobuPhysiologyConstants.TissueCompartmentCount);
            for (int i = 0; i < count; i++)
            {
                vitals[i] = new PhysiologyDTO
                {
                    BloodOxygen = 1f,
                    TissueNitrogen = ShinobuPhysiologyConstants.AtmosphericPressureAtSurfaceAtm,
                    CoreTemperature = 37f,
                    ActiveTraumaMask = 0u,
                    HeartRate = 62f,
                    Adrenaline = 0f
                };

                DecompressionStateDTO state = default;
                state.AmbientPressure = 1f;
                float* stateTissues = state.TissueTensions;
                {
                    for (int tissue = 0; tissue < ShinobuPhysiologyConstants.TissueCompartmentCount; tissue++)
                        stateTissues[tissue] = ShinobuPhysiologyConstants.AtmosphericPressureAtSurfaceAtm;
                }

                states[i] = state;
                if (scalars.IsCreated && i < scalars.Length)
                    scalars[i] = new PhysiologyScalarsDTO { FatigueMultiplier = 1f };
                if (pulses.IsCreated && i < pulses.Length)
                    pulses[i] = default;
            }

            if (tissues.IsCreated && tissues.Length > 0)
            {
                JobHandle initHandle = new InitTissueCompartmentsJob
                {
                    TissueCompartments = tissues,
                    TissueCoefficients = coefficients,
                    EntityCapacity = count
                }.Schedule(tissues.Length, ShinobuPhysiologyConstants.FrameJobBatchSize);
                // COLD SYNC JOB: boot-time Vault initialization fence, never a gameplay tick dependency.
                initHandle.Complete();
            }

            _defaultsInitialized = true;
        }

        private bool TryResolveBuffers(
            IDataVault vault,
            out NativeArray<PhysiologyDTO> vitals,
            out NativeArray<DecompressionStateDTO> decompression,
            out NativeArray<TissueCompartmentDTO> tissues,
            out NativeArray<HaldaneTissueCoefficientDTO> coefficients,
            out NativeArray<MockEnvironmentVitalsSignal> environment,
            out NativeArray<PhysiologyScalarsDTO> scalars,
            out NativeArray<VitalsExportDTO> exports,
            out NativeArray<PhysiologyTelemetryEntry> telemetry,
            out NativeArray<CardiacPulseStateDTO> pulses,
            out NativeArray<MockToxemiaSignal> toxemia,
            out NativeArray<MockPressureSignal> pressure,
            out NativeArray<MockCombatDamageSignal> combat,
            out NativeArray<MockPredatorAggroSignal> predator,
            out NativeArray<MockMedicalItemUsedSignal> medical,
            out NativeArray<PhysiologyTuningDTO> tuning,
            out NativeArray<DiveProfileSampleDTO> mockDiveProfile)
        {
            vitals = _vitalsHandle.Resolve(vault);
            decompression = _decompressionHandle.Resolve(vault);
            tissues = _tissueHandle.Resolve(vault);
            coefficients = _coefficientHandle.Resolve(vault);
            environment = _environmentHandle.Resolve(vault);
            scalars = _scalarHandle.Resolve(vault);
            exports = _exportHandle.Resolve(vault);
            telemetry = _telemetryHandle.Resolve(vault);
            pulses = _pulseHandle.Resolve(vault);
            toxemia = _toxemiaHandle.Resolve(vault);
            pressure = _pressureHandle.Resolve(vault);
            combat = _combatHandle.Resolve(vault);
            predator = _predatorHandle.Resolve(vault);
            medical = _medicalHandle.Resolve(vault);
            tuning = _tuningHandle.Resolve(vault);
            mockDiveProfile = _mockDiveProfileHandle.Resolve(vault);
            return vitals.IsCreated &&
                   decompression.IsCreated &&
                   tissues.IsCreated &&
                   coefficients.IsCreated &&
                   environment.IsCreated &&
                   scalars.IsCreated &&
                   exports.IsCreated &&
                   telemetry.IsCreated &&
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

        private bool TryResolveCsvScratch(IDataVault vault, out NativeArray<byte> scratch)
        {
            scratch = _csvScratchHandle.Resolve(vault);
            return scratch.IsCreated && scratch.Length >= CsvMaxBytes;
        }

        private void WriteEnvironmentSeed(IDataVault vault, float deltaTime, uint frame)
        {
            NativeArray<MockEnvironmentVitalsSignal> environment = _environmentHandle.Resolve(vault);
            if (!environment.IsCreated || environment.Length <= 0)
                return;

            _playerDepthValid = false;
            float depthMeters = math.max(0f, mockDepthMeters);
            IPlayerRuntimeContext player = _playerContext;
            if (player != null && player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                double3 playerAup = ToDouble3(snapshot.Aup);
                double3 seaLevelAup = new double3(playerAup.x, seaLevelAupY, playerAup.z);
                double depth = ResolveDepthMetersFromAup(playerAup, seaLevelAup);
                if (math.isfinite(depth))
                {
                    depthMeters = (float)math.clamp(depth, 0d, 12000d);
                    _playerDepthValid = true;
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

            NativeArray<float>.ReadOnly roomPressure = gas.RoomPressure;
            if (!roomPressure.IsCreated || (uint)_activeHabitatRoomId >= (uint)roomPressure.Length)
                return false;

            float pressureKPa = roomPressure[_activeHabitatRoomId];
            if (!math.isfinite(pressureKPa) || pressureKPa <= 0f)
                return false;

            ambientPressureAtm = math.max(0.5f, pressureKPa * math.rcp(KilopascalsPerAtmosphere));
            return true;
        }

        private static double3 ToDouble3(in AbsoluteUniversePosition aup)
        {
            double cell = AbsoluteUniversePosition.CellSizeMeters;
            return new double3(
                (double)aup.GridX * cell + aup.LocalX,
                (double)aup.GridY * cell + aup.LocalY,
                (double)aup.GridZ * cell + aup.LocalZ);
        }

        private static double ResolveDepthMetersFromAup(double3 playerAup, double3 seaLevelAup)
        {
            double3 delta = seaLevelAup - playerAup;
            return delta.y;
        }

        private float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(weight) ? weight : 1f);
        }

        private float ResolveSmoothedGlobalQualityWeight(float deltaTime)
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
            float curve = math.smoothstep(0f, 1f, math.saturate(globalQualityWeight));
            return math.lerp(0.2f, 0.0001f, curve);
        }

        private static float ResolveSystemHealthIndex01()
        {
            return math.saturate(HomeostasisBrain.SystemHealthIndex01);
        }

        private void CompleteFrameJob(bool forceComplete)
        {
            if (!_jobScheduled)
                return;

            if (!forceComplete && !_activeJobHandle.IsCompleted)
                return;

            _activeJobHandle.Complete();
            float elapsedMicroseconds = ResolveElapsedMicroseconds(_jobScheduleTimestamp, Stopwatch.GetTimestamp());
            _activeJobHandle = default;
            _jobScheduled = false;
            UnlockJobBuffers();

            IDataVault vault = _dataVault;
            if (vault != null)
                PatchLatestTelemetryExecutionTime(vault, elapsedMicroseconds);

            _telemetryCursor++;
            if (_telemetryCursor >= ShinobuPhysiologyConstants.TelemetryFrameCount)
                _telemetryCursor %= ShinobuPhysiologyConstants.TelemetryFrameCount;
            _scheduledCount = 0;

            if (vault == null)
                return;

            PublishSurvivalVitals(vault);
            PublishVisualSyncScalars(vault);
            TryDumpAutopsyIfFatal(vault);
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
            NativeArray<PhysiologyTelemetryEntry> telemetry = _telemetryHandle.Resolve(vault);
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            int telemetryIndex = _telemetryCursor % telemetry.Length;
            PhysiologyTelemetryEntry entry = telemetry[telemetryIndex];
            entry.ExecutionMicroseconds = math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(elapsedMicroseconds, 0f));
            telemetry[telemetryIndex] = entry;
        }

        private void PublishSurvivalVitals(IDataVault vault)
        {
            NativeArray<VitalsExportDTO> exports = _exportHandle.Resolve(vault);
            if (!exports.IsCreated || exports.Length <= 0)
                return;

            VitalsExportDTO export = exports[0];
            uint flags = SurvivalVitalsChangedSignalFlags.Oxygen |
                         SurvivalVitalsChangedSignalFlags.Temperature |
                         SurvivalVitalsChangedSignalFlags.Depth |
                         SurvivalVitalsChangedSignalFlags.Pressure;
            if ((export.StatusMask & ShinobuPhysiologyFlags.OxygenCritical) != 0u)
                flags |= SurvivalVitalsChangedSignalFlags.OxygenCritical;
            if ((export.StatusMask & (ShinobuPhysiologyFlags.Bends | ShinobuPhysiologyFlags.Hypothermia)) != 0u)
                flags |= SurvivalVitalsChangedSignalFlags.Injury | SurvivalVitalsChangedSignalFlags.Thermal;
            if ((export.StatusMask & ShinobuPhysiologyFlags.FatalOxygen) != 0u)
                flags |= SurvivalVitalsChangedSignalFlags.Death;

            SurvivalVitalsChangedSignal signal = new SurvivalVitalsChangedSignal
            {
                SourceId = ShinobuPhysiologyConstants.SourceHash,
                Frame = _simulationFrameCounter,
                Sequence = unchecked((uint)_telemetryCursor),
                Flags = flags,
                Oxygen01 = math.saturate(export.BloodOxygen),
                Energy01 = 1f,
                Integrity01 = (export.StatusMask & ShinobuPhysiologyFlags.Bends) != 0u ? 0.65f : 1f,
                DeathCause = (byte)(((export.StatusMask & ShinobuPhysiologyFlags.FatalOxygen) != 0u) ? 1 : 0)
            };
            GlobalSignals.Publish(in signal);
        }

        private void PublishVisualSyncScalars(IDataVault vault)
        {
            NativeArray<PhysiologyScalarsDTO> scalars = _scalarHandle.Resolve(vault);
            NativeArray<MockEnvironmentVitalsSignal> environment = _environmentHandle.Resolve(vault);
            if (!scalars.IsCreated || scalars.Length <= 0)
                return;

            PhysiologyScalarsDTO scalar = scalars[0];
            float ambient = 1f;
            if (environment.IsCreated && environment.Length > 0)
                ambient = math.max(0f, environment[0].AmbientPressureAtm);

            HectonShaderGlobalDataVaultBridge.PublishPhysiologyDecompression(
                new Vector4(
                    math.saturate(scalar.BendsRisk),
                    math.saturate(scalar.NarcosisSeverity),
                    ambient,
                    ResolveGlobalQualityWeight()));
        }

        private void TryDumpAutopsyIfFatal(IDataVault vault)
        {
            if (_autopsyDumped)
                return;

            NativeArray<PhysiologyDTO> vitals = _vitalsHandle.Resolve(vault);
            NativeArray<PhysiologyTelemetryEntry> telemetry = _telemetryHandle.Resolve(vault);
            if (!vitals.IsCreated || vitals.Length <= 0 || !telemetry.IsCreated || telemetry.Length <= 0)
                return;

            bool fatal = vitals[0].BloodOxygen <= ShinobuPhysiologyConstants.OxygenDeathThreshold;
            if (!fatal)
            {
                int latestIndex = (_telemetryCursor + telemetry.Length - 1) % telemetry.Length;
                fatal = (telemetry[latestIndex].FatalFlags &
                         (ShinobuPhysiologyFlags.FatalOxygen | ShinobuPhysiologyFlags.InvalidMath | ShinobuPhysiologyFlags.FatalBends)) != 0u;
            }

            if (!fatal)
                return;

            _autopsyDumped = true;
            DumpAutopsyReport(telemetry);
        }

        private void DumpAutopsyReport(NativeArray<PhysiologyTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated)
                return;

            try
            {
                string directory = Path.GetDirectoryName(_dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    Span<byte> header = stackalloc byte[32];
                    WriteUInt64LittleEndian(header.Slice(0, 8), DumpMagic);
                    WriteUInt32LittleEndian(header.Slice(8, 4), DumpVersion);
                    WriteUInt32LittleEndian(header.Slice(12, 4), (uint)telemetry.Length);
                    WriteUInt32LittleEndian(header.Slice(16, 4), (uint)UnsafeUtility.SizeOf<PhysiologyTelemetryEntry>());
                    WriteUInt32LittleEndian(header.Slice(20, 4), unchecked((uint)_telemetryCursor));
                    WriteUInt32LittleEndian(header.Slice(24, 4), ShinobuPhysiologyConstants.SourceHash);
                    WriteUInt32LittleEndian(header.Slice(28, 4), _simulationFrameCounter);
                    stream.Write(header);

                    void* telemetryPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                    int byteCount = telemetry.Length * UnsafeUtility.SizeOf<PhysiologyTelemetryEntry>();
                    stream.Write(new ReadOnlySpan<byte>(telemetryPtr, byteCount));
                    stream.Flush();
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private bool TryLoadLegacyMetabolismTables(IDataVault vault)
        {
            NativeArray<HaldaneTissueCoefficientDTO> coefficients = _coefficientHandle.Resolve(vault);
            if (!coefficients.IsCreated || coefficients.Length < ShinobuPhysiologyConstants.TissueCompartmentCount)
                return false;

            string streamingAssets = Path.Combine(Application.dataPath, "StreamingAssets");
            string halfTimePath = Path.Combine(streamingAssets, LegacyMetabolismFile);
            string mValuePath = Path.Combine(streamingAssets, LegacyMValueFile);
            if (!File.Exists(halfTimePath) && !File.Exists(mValuePath))
                return false;

            bool anyLoaded = false;
            try
            {
                if (File.Exists(halfTimePath))
                    anyLoaded |= TryReadLegacyFloatTable(vault, halfTimePath, coefficients, readHalfTimes: true);
                if (File.Exists(mValuePath))
                    anyLoaded |= TryReadLegacyFloatTable(vault, mValuePath, coefficients, readHalfTimes: false);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            return anyLoaded;
        }

        private bool TryReadLegacyFloatTable(
            IDataVault vault,
            string path,
            NativeArray<HaldaneTissueCoefficientDTO> coefficients,
            bool readHalfTimes)
        {
            if (!TryResolveCsvScratch(vault, out NativeArray<byte> scratch))
                return false;

            byte* scratchPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
            Span<byte> buffer = new Span<byte>(scratchPtr, math.min(scratch.Length, CsvMaxBytes));
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int byteCount = math.min((int)stream.Length, ShinobuPhysiologyConstants.TissueCompartmentCount * 4);
                if (byteCount < ShinobuPhysiologyConstants.TissueCompartmentCount * 4 || byteCount > buffer.Length)
                    return false;

                int read = stream.Read(buffer.Slice(0, byteCount));
                if (read < ShinobuPhysiologyConstants.TissueCompartmentCount * 4)
                    return false;

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
                        coefficient.NitrogenFraction = ShinobuPhysiologyConstants.NitrogenFraction;
                        if (coefficient.MValueRatio <= 0f)
                            coefficient.MValueRatio = ResolveEmergencyMValueRatio(i);
                    }
                    else
                    {
                        coefficient.MValueRatio = math.max(1.01f, value);
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

        private void GenerateEmergencyMockMetabolism(IDataVault vault)
        {
            NativeArray<HaldaneTissueCoefficientDTO> coefficients = _coefficientHandle.Resolve(vault);
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
                    MValueRatio = ResolveEmergencyMValueRatio(i),
                    NitrogenFraction = ShinobuPhysiologyConstants.NitrogenFraction
                };
            }

            NativeArray<PhysiologyTuningDTO> tuning = _tuningHandle.Resolve(vault);
            if (tuning.IsCreated && tuning.Length > 0)
            {
                PhysiologyTuningDTO row = ShinobuPhysiologyJobMath.SanitizeTuning(tuning[0]);
                row.Flags |= ShinobuPhysiologyFlags.EmergencyMockCoefficients;
                tuning[0] = row;
            }
        }

        private static float ResolveEmergencyHalfTimeSeconds(int index)
        {
            switch (index)
            {
                case 0: return 5f * 60f;
                case 1: return 8f * 60f;
                case 2: return 12.5f * 60f;
                case 3: return 18.5f * 60f;
                case 4: return 27f * 60f;
                case 5: return 38.3f * 60f;
                case 6: return 54.3f * 60f;
                case 7: return 77f * 60f;
                case 8: return 109f * 60f;
                case 9: return 146f * 60f;
                case 10: return 187f * 60f;
                case 11: return 239f * 60f;
                case 12: return 305f * 60f;
                case 13: return 390f * 60f;
                case 14: return 498f * 60f;
                default: return 635f * 60f;
            }
        }

        private static float ResolveEmergencyMValueRatio(int index)
        {
            return math.max(1.08f, 1.58f - index * 0.028f);
        }

        private void MonitorCsvOverrides(float deltaTime, IDataVault vault)
        {
            _csvPollTimer -= deltaTime;
            if (_csvPollTimer > 0f)
                return;

            _csvPollTimer = CsvPollIntervalSeconds;
            if (string.IsNullOrEmpty(_csvPath) || !File.Exists(_csvPath))
                return;

            DateTime stamp = File.GetLastWriteTimeUtc(_csvPath);
            if (stamp.Ticks == 0L || stamp.Ticks == _csvLastWriteTicks)
                return;

            _csvLastWriteTicks = stamp.Ticks;
            try
            {
                if (!TryResolveCsvScratch(vault, out NativeArray<byte> scratch))
                    return;

                byte* scratchPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
                Span<byte> buffer = new Span<byte>(scratchPtr, math.min(scratch.Length, CsvMaxBytes));
                using (FileStream stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int byteCount = math.min((int)stream.Length, buffer.Length);
                    if (byteCount <= 0)
                        return;

                    int read = stream.Read(buffer.Slice(0, byteCount));
                    if (read > 0)
                        ParseBiologyConstantsCsv(vault, buffer.Slice(0, read));
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private void ParseBiologyConstantsCsv(IDataVault vault, ReadOnlySpan<byte> bytes)
        {
            NativeArray<PhysiologyTuningDTO> tuningArray = _tuningHandle.Resolve(vault);
            NativeArray<BiologyConstantOverrideDTO> overrides = _csvOverrideHandle.Resolve(vault);
            NativeArray<HaldaneTissueCoefficientDTO> coefficients = _coefficientHandle.Resolve(vault);
            NativeArray<TissueCompartmentDTO> tissues = _tissueHandle.Resolve(vault);
            if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                return;

            PhysiologyTuningDTO tuning = ShinobuPhysiologyJobMath.SanitizeTuning(tuningArray[0]);
            int overrideIndex = 0;
            int cursor = 0;
            while (cursor < bytes.Length)
            {
                int lineStart = cursor;
                while (cursor < bytes.Length && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                    cursor++;

                int lineEnd = cursor;
                while (cursor < bytes.Length && (bytes[cursor] == (byte)'\n' || bytes[cursor] == (byte)'\r'))
                    cursor++;

                ParseCsvLine(bytes.Slice(lineStart, lineEnd - lineStart), ref tuning, overrides, coefficients, tissues, ref overrideIndex);
            }

            tuning.Flags |= ShinobuPhysiologyFlags.CsvOverride;
            tuningArray[0] = ShinobuPhysiologyJobMath.SanitizeTuning(tuning);
        }

        private static void ParseCsvLine(
            ReadOnlySpan<byte> line,
            ref PhysiologyTuningDTO tuning,
            NativeArray<BiologyConstantOverrideDTO> overrides,
            NativeArray<HaldaneTissueCoefficientDTO> coefficients,
            NativeArray<TissueCompartmentDTO> tissues,
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
                if (TryParseAsciiFloat(line.Slice(secondStart), out float mValue))
                {
                    ApplyTissueCsvOverride(
                        line.Slice(keyStart, keyEnd - keyStart + 1),
                        keyHash,
                        value,
                        mValue,
                        coefficients,
                        tissues);
                    WriteCsvOverride(overrides, ref overrideIndex, keyHash, value);
                    return;
                }
            }

            ApplyCsvOverride(keyHash, value, ref tuning);
            WriteCsvOverride(overrides, ref overrideIndex, keyHash, value);
        }

        private static void WriteCsvOverride(
            NativeArray<BiologyConstantOverrideDTO> overrides,
            ref int overrideIndex,
            uint keyHash,
            float value)
        {
            if (overrides.IsCreated && overrideIndex < overrides.Length)
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
            float mValueRatio,
            NativeArray<HaldaneTissueCoefficientDTO> coefficients,
            NativeArray<TissueCompartmentDTO> tissues)
        {
            if (!coefficients.IsCreated || coefficients.Length <= 0)
                return;

            int index = ResolveCsvCompartmentIndex(key, keyHash);
            if ((uint)index >= (uint)coefficients.Length)
                return;

            float safeHalfTime = math.max(1f, ShinobuPhysiologyJobMath.SanitizeFinite(halfTimeSeconds, ResolveEmergencyHalfTimeSeconds(index)));
            float safeMValue = math.max(1.01f, ShinobuPhysiologyJobMath.SanitizeFinite(mValueRatio, ResolveEmergencyMValueRatio(index)));
            coefficients[index] = new HaldaneTissueCoefficientDTO
            {
                HalfTimeSeconds = safeHalfTime,
                K = 0.69314718056f * math.rcp(safeHalfTime),
                MValueRatio = safeMValue,
                NitrogenFraction = ShinobuPhysiologyConstants.NitrogenFraction
            };

            if (!tissues.IsCreated)
                return;

            for (int tissueIndex = index; tissueIndex < tissues.Length; tissueIndex += ShinobuPhysiologyConstants.TissueCompartmentCount)
            {
                TissueCompartmentDTO tissue = tissues[tissueIndex];
                tissue.Halftime = safeHalfTime;
                tissue.MValue = safeMValue;
                tissue.Flags |= ShinobuPhysiologyFlags.CsvOverride;
                tissues[tissueIndex] = tissue;
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
                return math.clamp(value, 0, ShinobuPhysiologyConstants.TissueCompartmentCount - 1);

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

        private bool TryLockJobBuffers(IDataVault vault)
        {
            if (vault == null || _jobLocksHeld)
                return false;

            int locked = 0;
            if (!vault.TryLockBuffer(BufferID.ShinobuPhysiologyVitals, OwnerSystem)) return false;
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuDecompressionStates, OwnerSystem)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuTissueCompartments, OwnerSystem)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuHaldaneCoefficients, OwnerSystem)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuEnvironmentVitals, OwnerSystem)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuPhysiologyScalars, OwnerSystem)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuVitalsExport, OwnerSystem)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuPhysiologyTelemetryRing, OwnerSystem)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuCardiacPulseStates, OwnerSystem)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuMockToxemiaSignals, OwnerSystem)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuMockPressureSignals, OwnerSystem)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuMockCombatDamageSignals, OwnerSystem)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuMockPredatorAggroSignals, OwnerSystem)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(BufferID.ShinobuMockMedicalItemSignals, OwnerSystem)) { UnlockLockedJobBuffers(vault, locked); return false; }

            _jobLocksHeld = true;
            return true;
        }

        private void UnlockJobBuffers()
        {
            if (!_jobLocksHeld)
                return;

            IDataVault vault = _dataVault;
            if (vault != null)
                UnlockLockedJobBuffers(vault, LockBufferCount);
            _jobLocksHeld = false;
        }

        private static void UnlockLockedJobBuffers(IDataVault vault, int lockedCount)
        {
            if (lockedCount >= 14) vault.TryUnlockBuffer(BufferID.ShinobuMockMedicalItemSignals, OwnerSystem);
            if (lockedCount >= 13) vault.TryUnlockBuffer(BufferID.ShinobuMockPredatorAggroSignals, OwnerSystem);
            if (lockedCount >= 12) vault.TryUnlockBuffer(BufferID.ShinobuMockCombatDamageSignals, OwnerSystem);
            if (lockedCount >= 11) vault.TryUnlockBuffer(BufferID.ShinobuMockPressureSignals, OwnerSystem);
            if (lockedCount >= 10) vault.TryUnlockBuffer(BufferID.ShinobuMockToxemiaSignals, OwnerSystem);
            if (lockedCount >= 9) vault.TryUnlockBuffer(BufferID.ShinobuCardiacPulseStates, OwnerSystem);
            if (lockedCount >= 8) vault.TryUnlockBuffer(BufferID.ShinobuPhysiologyTelemetryRing, OwnerSystem);
            if (lockedCount >= 7) vault.TryUnlockBuffer(BufferID.ShinobuVitalsExport, OwnerSystem);
            if (lockedCount >= 6) vault.TryUnlockBuffer(BufferID.ShinobuPhysiologyScalars, OwnerSystem);
            if (lockedCount >= 5) vault.TryUnlockBuffer(BufferID.ShinobuEnvironmentVitals, OwnerSystem);
            if (lockedCount >= 4) vault.TryUnlockBuffer(BufferID.ShinobuHaldaneCoefficients, OwnerSystem);
            if (lockedCount >= 3) vault.TryUnlockBuffer(BufferID.ShinobuTissueCompartments, OwnerSystem);
            if (lockedCount >= 2) vault.TryUnlockBuffer(BufferID.ShinobuDecompressionStates, OwnerSystem);
            if (lockedCount >= 1) vault.TryUnlockBuffer(BufferID.ShinobuPhysiologyVitals, OwnerSystem);
        }

        private void TryRegisterTicks()
        {
            if (!_registeredUpdate)
                _registeredUpdate = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);

            if (!_registeredUpdate || !_registeredLateFrame)
                TryUnregisterTicks();
        }

        private void TryUnregisterTicks()
        {
            if (_registeredUpdate)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
                _registeredUpdate = false;
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
            _exportHandle = default;
            _telemetryHandle = default;
            _pulseHandle = default;
            _toxemiaHandle = default;
            _pressureHandle = default;
            _combatHandle = default;
            _predatorHandle = default;
            _medicalHandle = default;
            _tuningHandle = default;
            _csvOverrideHandle = default;
            _mockDiveProfileHandle = default;
            _csvScratchHandle = default;
            _simulationAccumulator = 0f;
            _previousDepthValid = false;
            _insideHabitat = false;
            _activeHabitatRoomId = -1;
        }

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
    }
}
