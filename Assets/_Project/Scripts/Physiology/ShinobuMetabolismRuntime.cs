using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Atmosphere;
using Hecton8.Core.Contracts.Physiology;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Physiology
{
    /// <summary>
    /// Vault-backed survival metabolism runtime. SlowTick schedules pure Burst kernels; LateFrameTick reclaims the fence.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed unsafe partial class ShinobuMetabolismRuntime : MonoBehaviour, IColdTickable, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener, IDisposable
    {
        private static int s_x001ShinobuMetabolismRuntimeSignalPushDropCount;
        private const SystemID OwnerSystem = SystemID.GameplayPlayer;
        private const SystemID ChemicalOwnerSystem = SystemID.AISensory;
        private const uint MockSectorHash = 0x4D455441u; // META
        private const ulong DumpMagic = 0x4D45544153524745ul; // METASRGE
        private const uint DumpVersion = 2u;
#if UNITY_EDITOR
        private const string CsvRelativePath = "biological_metabolism_profiles.csv";
        private const string SuitCsvRelativePath = "suit_thermal_profiles.csv";
#endif
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_320.bin";
        private const uint ToxicityExposureLaneHash = 0x54584F58u; // TOX
        private const uint MetabolicToxicChemicalHash = 0x4D54584Eu; // MTXN

        private static readonly ProfilerMarker _ScheduleMarker = new ProfilerMarker("ShinobuMetabolism.Schedule");
        private static readonly ProfilerMarker _CompleteMarker = new ProfilerMarker("ShinobuMetabolism.Complete");
        private static readonly int _GlobalsBufferId = Shader.PropertyToID("_HectonMetabolismFrostGlobals");
        private static readonly int _FrostScalarId = Shader.PropertyToID("_HectonMetabolismFrostScalar");
        private static readonly ulong JobMutationGuardMask =
            ShinobuMetabolismVaultContract.MetabolismStateMutationGuardMask |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismStatesBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismEntityAupsBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismExertionBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismSpeciesRulesBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismRuleIndicesBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismTelemetryRingBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismTuningBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismToxinSamplesBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismPhysiologySignalsBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismExposureSignalsBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismDetailTelemetryRingBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismSuitThermalProfilesBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismSuitProfileIndicesBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.ChemicalPublishedGridReadbackBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.ChemicalOverlayGridReadbackBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.ChemicalTuningReadbackBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.ChemicalTelemetryReadbackBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.ChemicalTelemetryCursorReadbackBuffer) |
            MutationGuardBit(ShinobuSuitIntegrityConstants.StateBuffer);
        private static readonly ulong BootstrapMutationGuardMask =
            ShinobuMetabolismVaultContract.MetabolismStateMutationGuardMask |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismStatesBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismEntityAupsBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismExertionBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismSpeciesRulesBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismRuleIndicesBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismTuningBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismToxinSamplesBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismPhysiologySignalsBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismExposureSignalsBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismSuitThermalProfilesBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismSuitProfileIndicesBuffer);
        private static readonly ulong BiologicalProfileImportMutationGuardMask =
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismSpeciesRulesBuffer);
        private static readonly ulong SuitProfileImportMutationGuardMask =
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismSuitThermalProfilesBuffer);
        private static readonly ulong TuningMutationGuardMask =
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismTuningBuffer);
        private static readonly ulong SuitProfileIndexMutationGuardMask =
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismSuitProfileIndicesBuffer);
        private static readonly ulong SuitProfileSelectionMutationGuardMask =
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismSuitProfileIndicesBuffer) |
            MutationGuardBit(ShinobuMetabolismConstants.MetabolismSuitThermalProfilesBuffer);
#if UNITY_EDITOR
        private static readonly byte[] s_csvImportScratch = new byte[ShinobuMetabolismConstants.CsvMaxBytes];
        private static readonly MetabolicSpeciesRuleDTO[] s_speciesRuleImportScratch = new MetabolicSpeciesRuleDTO[ShinobuMetabolismConstants.MaxSpeciesRules];
        private static readonly MetabolicSuitThermalProfileDTO[] s_suitProfileImportScratch = new MetabolicSuitThermalProfileDTO[ShinobuMetabolismConstants.MaxSuitThermalProfiles];
        private static int s_csvImportScratchBusy;
#endif

        [Header("Runtime Capacity")]
        [Tooltip("Maximum living-entity metabolism rows owned by this runtime.")]
        [SerializeField, Min(1)] private int entityCapacity = ShinobuMetabolismConstants.DefaultEntityCapacity;

        [Header("Cold Bootstrap")]
        [Tooltip("Generate deterministic fallback entities when no creature owner has hydrated metabolism rows yet.")]
        [SerializeField] private bool generateMockEcosystemOnEnable = true;

#if UNITY_EDITOR
        [Tooltip("Load biological_metabolism_profiles.csv from the project root during cold bootstrap.")]
        [SerializeField] private bool loadCsvProfilesOnEnable = true;

        [Tooltip("Load suit_thermal_profiles.csv from the project root during cold bootstrap.")]
        [SerializeField] private bool loadSuitThermalProfilesOnEnable = true;
#endif

        [Header("Editor Debug")]
        [Tooltip("Editor-only temperature bars for the first vault rows. Runtime builds ignore this path.")]
        [SerializeField] private bool drawDebugGizmos = true;

        [Tooltip("Maximum editor-only metabolism temperature bars drawn by OnDrawGizmos.")]
        [SerializeField, Range(0, 64)] private int debugGizmoRows = 8;

        private VaultGenerationHandle<MetabolicStateDTO> _stateHandle;
        private VaultGenerationHandle<double3> _entityAupHandle;
        private VaultGenerationHandle<float> _exertionHandle;
        private VaultGenerationHandle<MetabolicSpeciesRuleDTO> _speciesRuleHandle;
        private VaultGenerationHandle<ushort> _ruleIndexHandle;
        private VaultGenerationHandle<MetabolicTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<MetabolismTuningDTO> _tuningHandle;
        private VaultGenerationHandle<float> _toxinSampleHandle;
        private VaultGenerationHandle<PhysiologyStateSignal> _physiologySignalHandle;
        private VaultGenerationHandle<MetabolicExposureSignalDTO> _exposureSignalHandle;
        private VaultGenerationHandle<MetabolicDetailTelemetryEntry> _detailTelemetryHandle;
        private VaultGenerationHandle<MetabolicSuitThermalProfileDTO> _suitProfileHandle;
        private VaultGenerationHandle<ushort> _suitProfileIndexHandle;
        private VaultGenerationHandle<SuitIntegrityDTO> _suitIntegrityStateReadHandle;

        private IDataVault _dataVault;
        private IThermodynamicsService _thermodynamicsService;
        private IThermodynamicsService _thermalGridReadbackService;
        private ITickDispatcher _tickDispatcher;
        private JobHandle _activeJobHandle;
        private GraphicsBuffer _shaderGlobalsBufferA;
        private GraphicsBuffer _shaderGlobalsBufferB;
        private GraphicsBuffer _activeShaderGlobalsBuffer;
        private MetabolismShaderGlobalsDTO _lastShaderGlobals;
        private MetabolicTelemetryEntry _latestTelemetry;
        private MetabolicDetailTelemetryEntry _latestDetailTelemetry;
        private MetabolicTelemetryEntry _pendingShaderGlobalsTelemetry;
#if UNITY_EDITOR
        private string _csvPath;
        private string _suitCsvPath;
#endif
        private string _dumpPath;
        private double _lastDispatcherTimeSeconds = -1d;
        private float _simulationAccumulator;
        private long _jobScheduleTimestamp;
        private int _telemetryCursor;
        private int _pendingTelemetryIndex = -1;
        private int _scheduledCount;
        private uint _simulationFrameCounter;
        private bool _registeredColdTick;
        private bool _registeredSlowTick;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _jobScheduled;
        private bool _jobLocksHeld;
        private bool _defaultsInitialized;
        private bool _latestTelemetryValid;
        private bool _latestDetailTelemetryValid;
        private bool _autopsyDumped;
        private bool _shaderGlobalsInitialized;
        private bool _thermalGridReadbackHeld;
        private bool _supportsConstantBufferCold;
        private bool _vaultRepairRequested;
        private bool _pendingShaderGlobals;
        private int _shaderWriteIndex;

        private void Awake()
        {
            entityCapacity = math.max(1, entityCapacity);
            _supportsConstantBufferCold = SystemInfo.supportsSetConstantBuffer;
#if UNITY_EDITOR
            _csvPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", CsvRelativePath));
            _suitCsvPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", SuitCsvRelativePath));
#endif
            _dumpPath = DumpRelativePath;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            _supportsConstantBufferCold = SystemInfo.supportsSetConstantBuffer;
            SignalBus<PhysiologyStateSignal>.Configure(
                PhysiologyStateSignal.ExpectedCapacity,
                maxFrameSignals: PhysiologyStateSignal.MaxFrameSignals,
                lowTierFrameSignals: PhysiologyStateSignal.LowTierFrameSignals,
                laneHash: PhysiologyStateSignal.LaneHash);
            SignalBus<PhysiologyStateSignal>.EnsureInitialized();
            SignalBus<ToxicityExposureSignal>.Configure(
                ToxicityExposureSignal.ExpectedCapacity,
                maxFrameSignals: ToxicityExposureSignal.MaxFrameSignals,
                lowTierFrameSignals: ToxicityExposureSignal.LowTierFrameSignals,
                laneHash: ToxicityExposureSignal.LaneHash);
            SignalBus<ToxicityExposureSignal>.EnsureInitialized();
            SignalBus<KccVelocitySignal>.EnsureInitialized();
            TryRegisterHotSwapListener();
            RebindColdServices();
            EnsureShaderGlobalsBuffers();
            PrepareRuntimeStateCold(loadEditorProfiles: true);
            TryRegisterTicks();
        }

        private void Start()
        {
            if (!Application.isPlaying)
                return;

            RebindColdServices();
            PrepareRuntimeStateCold(loadEditorProfiles: false);
            TryRegisterTicks();
        }

        private void OnDisable()
        {
            CompleteFrameJobForTeardown();
            TryUnregisterTicks();
            TryUnregisterHotSwapListener();
            ReleaseThermalGridReadback();
            UnlockJobBuffers();
            ReleaseMetabolismVaultHandles(_dataVault);
            ReleaseShaderGlobalsBuffers();
            ClearCachedHandles();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            CompleteFrameJobForTeardown();
            ReleaseThermalGridReadback();
            UnlockJobBuffers();
            ReleaseMetabolismVaultHandles(_dataVault);
            ReleaseShaderGlobalsBuffers();
            ClearCachedHandles();
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                IDataVault previousVault = previousService as IDataVault ?? _dataVault;
                CompleteFrameJobForTeardown();
                ReleaseThermalGridReadback();
                UnlockJobBuffers();
                ReleaseMetabolismVaultHandles(previousVault);
                _dataVault = currentService as IDataVault;
                ClearCachedHandles();
                _defaultsInitialized = false;
                _autopsyDumped = false;
                _vaultRepairRequested = true;
                PrepareRuntimeStateCold(loadEditorProfiles: false);
                TryRegisterTicks();

                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.ThermodynamicsService)
            {
                CompleteFrameJobForTeardown();
                ReleaseThermalGridReadback();
                _thermodynamicsService = currentService as IThermodynamicsService;
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                _tickDispatcher = currentService as ITickDispatcher;
                TryUnregisterTicks();
                if (currentService != null && isActiveAndEnabled)
                    TryRegisterTicks();
            }
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            if (_jobScheduled)
                return;

            IDataVault vault = _dataVault;
            if (vault == null || !HasVaultStateReady())
            {
                _vaultRepairRequested = true;
                return;
            }

            float slowDeltaSeconds = ResolveSlowTickDeltaSeconds();
            if (slowDeltaSeconds <= 0f)
                return;

            float quality = ResolveGlobalQualityWeight();
            _simulationAccumulator = math.min(
                _simulationAccumulator + slowDeltaSeconds,
                ShinobuMetabolismConstants.MaxAccumulatedDeltaSeconds);
            float cadenceSeconds = ShinobuMetabolismJobMath.ResolveCadenceSeconds(quality);
            if (_simulationAccumulator < cadenceSeconds)
                return;

            using (_ScheduleMarker.Auto())
            {
                if (!TryResolveBuffers(
                        vault,
                        out NativeArray<MetabolicStateDTO> states,
                        out NativeArray<double3> entityAups,
                        out NativeArray<float> exertion,
                        out NativeArray<MetabolicSpeciesRuleDTO> rules,
                        out NativeArray<ushort> ruleIndices,
                        out NativeArray<MetabolicTelemetryEntry> telemetry,
                        out NativeArray<MetabolismTuningDTO> tuningArray,
                        out NativeArray<float> toxinSamples,
                        out NativeArray<PhysiologyStateSignal> physiologySignals,
                        out NativeArray<MetabolicExposureSignalDTO> exposureSignals,
                        out NativeArray<MetabolicDetailTelemetryEntry> detailTelemetry,
                        out NativeArray<MetabolicSuitThermalProfileDTO> suitProfiles,
                        out NativeArray<ushort> suitProfileIndices))
                {
                    return;
                }

                int count = ResolveEntityCount(states, entityAups, exertion, ruleIndices, toxinSamples);
                if (count <= 0 ||
                    rules.Length <= 0 ||
                    telemetry.Length <= 0 ||
                    detailTelemetry.Length <= 0 ||
                    suitProfiles.Length <= 0 ||
                    tuningArray.Length <= 0 ||
                    physiologySignals.Length < ResolvePhysiologySignalCapacity(count) ||
                    exposureSignals.Length < ResolveExposureSignalCapacity(count))
                {
                    return;
                }

                bool scheduled = false;
                try
                {
                    MetabolismTuningDTO tuning = ShinobuMetabolismJobMath.SanitizeTuning(tuningArray[0]);
                    tuning.GlobalQualityWeight = quality;

                    TryResolveThermalGrid(
                        out NativeArray<float>.ReadOnly thermalGrid,
                        out int3 thermalResolution,
                        out double3 thermalRootAup,
                        out float thermalCellSizeMeters,
                        out byte hasThermalGrid);

                    NativeArray<SuitIntegrityDTO> suitIntegrityStates = default;
                    byte hasSuitIntegrityStates = 0;
                    if (TryReadSuitIntegrityStates(vault, 1, out suitIntegrityStates))
                        hasSuitIntegrityStates = 1;

                    TryResolveChemicalGrid(
                        vault,
                        out float4* chemicalPublishedPtr,
                        out float4* chemicalOverlayPtr,
                        out int3 chemicalResolution,
                        out int chemicalGridLength,
                        out double3 chemicalRootAup,
                        out float chemicalCellSizeMeters,
                        out byte hasChemicalGrid);

                    float dt = math.clamp(_simulationAccumulator, 0.0001f, ShinobuMetabolismConstants.MaxAccumulatedDeltaSeconds);
                    uint frame = _simulationFrameCounter + 1u;
                    int telemetryIndex = _telemetryCursor % telemetry.Length;
                    if (telemetryIndex < 0)
                        telemetryIndex += telemetry.Length;

                    float* thermalPtr = hasThermalGrid != 0 && thermalGrid.IsCreated
                        ? (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(thermalGrid)
                        : null;

                    if (!TryLockJobBuffers(vault))
                        return;

                    TryApplyLatestKccSignal(states, entityAups, exertion);
                    tuningArray[0] = tuning;

                    MetabolicIntegrationJob integrationJob = default;
                    integrationJob.States = (MetabolicStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states);
                    integrationJob.EntityAups = (double3*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(entityAups);
                    integrationJob.ExertionSpeedSq = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(exertion);
                    integrationJob.ToxinSamples = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(toxinSamples);
                    integrationJob.RuleIndices = (ushort*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(ruleIndices);
                    integrationJob.Rules = (MetabolicSpeciesRuleDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rules);
                    integrationJob.SuitProfileIndices = (ushort*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(suitProfileIndices);
                    integrationJob.SuitProfiles = (MetabolicSuitThermalProfileDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(suitProfiles);
                    integrationJob.SuitIntegrityStates = hasSuitIntegrityStates != 0 && suitIntegrityStates.IsCreated
                        ? (SuitIntegrityDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(suitIntegrityStates)
                        : null;
                    integrationJob.ThermalCelsiusGrid = thermalPtr;
                    integrationJob.ChemicalPublishedGrid = chemicalPublishedPtr;
                    integrationJob.ChemicalOverlayGrid = chemicalOverlayPtr;
                    integrationJob.PhysiologySignals = (PhysiologyStateSignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(physiologySignals);
                    integrationJob.ExposureSignals = (MetabolicExposureSignalDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(exposureSignals);
                    integrationJob.DetailTelemetry = (MetabolicDetailTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(detailTelemetry);
                    integrationJob.Tuning = tuning;
                    integrationJob.ThermalGridRootAup = thermalRootAup;
                    integrationJob.ChemicalGridRootAup = chemicalRootAup;
                    integrationJob.ThermalGridResolution = thermalResolution;
                    integrationJob.ChemicalGridResolution = chemicalResolution;
                    integrationJob.ThermalGridLength = hasThermalGrid != 0 && thermalGrid.IsCreated ? thermalGrid.Length : 0;
                    integrationJob.ChemicalGridLength = chemicalGridLength;
                    integrationJob.PhysiologySignalLength = physiologySignals.Length;
                    integrationJob.ExposureSignalLength = exposureSignals.Length;
                    integrationJob.DetailTelemetryLength = detailTelemetry.Length;
                    integrationJob.DetailTelemetryCursor = telemetryIndex;
                    integrationJob.SuitIntegrityStateCount = hasSuitIntegrityStates != 0 && suitIntegrityStates.IsCreated ? suitIntegrityStates.Length : 0;
                    integrationJob.ThermalCellSizeMeters = thermalCellSizeMeters;
                    integrationJob.ChemicalCellSizeMeters = chemicalCellSizeMeters;
                    integrationJob.DeltaSeconds = dt;
                    integrationJob.GlobalQualityWeight = quality;
                    integrationJob.Frame = frame;
                    integrationJob.Count = count;
                    integrationJob.RuleCount = rules.Length;
                    integrationJob.SuitProfileCount = suitProfiles.Length;
                    integrationJob.HasThermalGrid = hasThermalGrid;
                    integrationJob.HasChemicalGrid = hasChemicalGrid;
                    integrationJob.EmitSignals = 1;
                    JobHandle integrationHandle = integrationJob.Schedule(count, ShinobuMetabolismConstants.FrameJobBatchSize);

                    MetabolismTelemetryJob telemetryJob = default;
                    telemetryJob.States = (MetabolicStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states);
                    telemetryJob.Telemetry = (MetabolicTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(telemetry);
                    telemetryJob.Count = count;
                    telemetryJob.TelemetryLength = telemetry.Length;
                    telemetryJob.TelemetryCursor = telemetryIndex;
                    telemetryJob.DeltaSeconds = dt;
                    telemetryJob.GlobalQualityWeight = quality;
                    telemetryJob.Frame = frame;
                    JobHandle telemetryHandle = telemetryJob.Schedule(integrationHandle);

                    _activeJobHandle = telemetryHandle;
                    _jobScheduleTimestamp = Stopwatch.GetTimestamp();
                    _pendingTelemetryIndex = telemetryIndex;
                    _scheduledCount = count;
                    _simulationFrameCounter = frame;
                    _simulationAccumulator = 0f;
                    H8Memory.RegisterActiveJob(OwnerSystem, _activeJobHandle);
                    _jobScheduled = true;
                    scheduled = true;
                }
                finally
                {
                    if (!scheduled)
                    {
                        ReleaseThermalGridReadback();
                        UnlockJobBuffers();
                    }
                }
            }
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            TryFinalizeFrameJobNoWait();
            FlushPendingShaderGlobalsVisualSync();
        }

        public void ColdTick()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
                return;

            if (!_vaultRepairRequested && HasVaultStateReady())
                return;

            PrepareRuntimeStateCold(loadEditorProfiles: false);
            TryRegisterTicks();
        }

        /// <summary>
        /// Generates deterministic 5000-row fallback metabolism data in Vault memory.
        /// </summary>
        public bool GenerateMockEcosystemMetabolism()
        {
            CompleteFrameJobForTeardown();
            IDataVault vault = _dataVault;
            if (vault == null || !EnsureVaultState() || !vault.TryAcquireMutationGuard(BootstrapMutationGuardMask))
            {
                return false;
            }

            try
            {
                if (!TryResolveBuffers(
                        vault,
                        out NativeArray<MetabolicStateDTO> states,
                        out NativeArray<double3> entityAups,
                        out NativeArray<float> exertion,
                        out NativeArray<MetabolicSpeciesRuleDTO> rules,
                        out NativeArray<ushort> ruleIndices,
                        out _,
                        out NativeArray<MetabolismTuningDTO> tuningArray,
                        out NativeArray<float> toxinSamples,
                        out NativeArray<PhysiologyStateSignal> physiologySignals,
                        out NativeArray<MetabolicExposureSignalDTO> exposureSignals,
                        out _,
                        out NativeArray<MetabolicSuitThermalProfileDTO> suitProfiles,
                        out NativeArray<ushort> suitProfileIndices))
                {
                    return false;
                }

                int count = ResolveEntityCount(states, entityAups, exertion, ruleIndices, toxinSamples);
                if (count <= 0 || rules.Length <= 0 || tuningArray.Length <= 0)
                    return false;

                InitMetabolismRulesJob initRulesJob = default;
                initRulesJob.Rules = (MetabolicSpeciesRuleDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rules);
                initRulesJob.SuitProfiles = (MetabolicSuitThermalProfileDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(suitProfiles);
                initRulesJob.Tuning = (MetabolismTuningDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(tuningArray);
                initRulesJob.RuleCount = rules.Length;
                initRulesJob.SuitProfileCount = suitProfiles.Length;
                JobHandle initRules = initRulesJob.Schedule();

                InitInactiveMetabolismJob initInactiveJob = default;
                initInactiveJob.States = (MetabolicStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states);
                initInactiveJob.EntityAups = (double3*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(entityAups);
                initInactiveJob.ExertionSpeedSq = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(exertion);
                initInactiveJob.ToxinSamples = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(toxinSamples);
                initInactiveJob.RuleIndices = (ushort*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(ruleIndices);
                initInactiveJob.SuitProfileIndices = (ushort*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(suitProfileIndices);
                initInactiveJob.PhysiologySignals = (PhysiologyStateSignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(physiologySignals);
                initInactiveJob.ExposureSignals = (MetabolicExposureSignalDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(exposureSignals);
                initInactiveJob.Count = count;
                initInactiveJob.PhysiologySignalLength = physiologySignals.Length;
                initInactiveJob.ExposureSignalLength = exposureSignals.Length;
                JobHandle initInactive = initInactiveJob.Schedule(count, ShinobuMetabolismConstants.FrameJobBatchSize, initRules);

                int mockCount = math.min(count, ShinobuMetabolismConstants.DefaultEntityCapacity);
                InitMockMetabolismJob initMockJob = default;
                initMockJob.States = (MetabolicStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states);
                initMockJob.EntityAups = (double3*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(entityAups);
                initMockJob.ExertionSpeedSq = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(exertion);
                initMockJob.ToxinSamples = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(toxinSamples);
                initMockJob.RuleIndices = (ushort*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(ruleIndices);
                initMockJob.SuitProfileIndices = (ushort*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(suitProfileIndices);
                initMockJob.Count = mockCount;
                initMockJob.Seed = MockSectorHash;
                initMockJob.Frame = _simulationFrameCounter;
                JobHandle initMock = initMockJob.Schedule(mockCount, ShinobuMetabolismConstants.FrameJobBatchSize, initInactive);

                // COLD SYNC JOB: editor/bootstrap fallback hydration, not part of gameplay SlowTick.
                if (!ForceCompleteInPostSimulationWindow(ref initMock))
                    return false;

                _defaultsInitialized = true;
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(BootstrapMutationGuardMask);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Reloads designer-authored biological metabolism profile rows from the project-root CSV.
        /// </summary>
        public bool TryLoadBiologicalProfilesCsv()
        {
            if (_jobScheduled)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null || !EnsureVaultState())
                return false;

            if (System.Threading.Interlocked.CompareExchange(ref s_csvImportScratchBusy, 1, 0) != 0)
                return false;

            try
            {
                if (!File.Exists(_csvPath))
                    return false;

                int bytesRead = ReadCsvBytesCold(_csvPath, s_csvImportScratch, ShinobuMetabolismConstants.CsvMaxBytes);
                if (bytesRead <= 0)
                    return false;

                int parsed = ParseBiologicalProfilesCsv(
                    s_csvImportScratch.AsSpan(0, bytesRead),
                    s_speciesRuleImportScratch);
                if (parsed <= 0)
                    return true;

                if (!TryOpenMetabolismVaultBuffer(
                        vault,
                        in _speciesRuleHandle,
                        ShinobuMetabolismConstants.MetabolismSpeciesRulesBuffer,
                        ShinobuMetabolismConstants.MaxSpeciesRules,
                        out NativeArray<MetabolicSpeciesRuleDTO> rules) ||
                    !rules.IsCreated ||
                    rules.Length <= 0)
                {
                    return false;
                }

                if (!vault.TryAcquireMutationGuard(BiologicalProfileImportMutationGuardMask))
                {
                    return false;
                }

                try
                {
                    int copyCount = math.min(parsed, rules.Length);
                    int byteCount = copyCount * UnsafeUtility.SizeOf<MetabolicSpeciesRuleDTO>();
                    void* destination = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rules);
                    fixed (MetabolicSpeciesRuleDTO* source = s_speciesRuleImportScratch)
                    {
                        UnsafeUtility.MemCpy(destination, source, byteCount);
                    }

                    if (rules.Length > copyCount)
                    {
                        int staleByteCount = (rules.Length - copyCount) * UnsafeUtility.SizeOf<MetabolicSpeciesRuleDTO>();
                        UnsafeUtility.MemClear((byte*)destination + byteCount, staleByteCount);
                    }
                }
                finally
                {
                    vault.ReleaseMutationGuard(BiologicalProfileImportMutationGuardMask);
                }

                return true;
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
                System.Threading.Volatile.Write(ref s_csvImportScratchBusy, 0);
            }
        }

        /// <summary>
        /// Reloads designer-authored suit thermal profiles from the project-root CSV.
        /// </summary>
        public bool TryLoadSuitThermalProfilesCsv()
        {
            if (_jobScheduled)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null || !EnsureVaultState())
                return false;

            if (System.Threading.Interlocked.CompareExchange(ref s_csvImportScratchBusy, 1, 0) != 0)
                return false;

            try
            {
                if (!File.Exists(_suitCsvPath))
                    return false;

                int bytesRead = ReadCsvBytesCold(_suitCsvPath, s_csvImportScratch, ShinobuMetabolismConstants.CsvMaxBytes);
                if (bytesRead <= 0)
                    return false;

                int parsed = ParseSuitThermalProfilesCsv(
                    s_csvImportScratch.AsSpan(0, bytesRead),
                    s_suitProfileImportScratch);
                if (parsed <= 0)
                    return false;

                if (!TryOpenMetabolismVaultBuffer(
                        vault,
                        in _suitProfileHandle,
                        ShinobuMetabolismConstants.MetabolismSuitThermalProfilesBuffer,
                        ShinobuMetabolismConstants.MaxSuitThermalProfiles,
                        out NativeArray<MetabolicSuitThermalProfileDTO> suitProfiles) ||
                    !suitProfiles.IsCreated ||
                    suitProfiles.Length <= 0)
                {
                    return false;
                }

                if (!vault.TryAcquireMutationGuard(SuitProfileImportMutationGuardMask))
                {
                    return false;
                }

                try
                {
                    int copyCount = math.min(parsed, suitProfiles.Length);
                    int byteCount = copyCount * UnsafeUtility.SizeOf<MetabolicSuitThermalProfileDTO>();
                    void* destination = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(suitProfiles);
                    fixed (MetabolicSuitThermalProfileDTO* source = s_suitProfileImportScratch)
                    {
                        UnsafeUtility.MemCpy(destination, source, byteCount);
                    }

                    if (suitProfiles.Length > copyCount)
                    {
                        int staleByteCount = (suitProfiles.Length - copyCount) * UnsafeUtility.SizeOf<MetabolicSuitThermalProfileDTO>();
                        UnsafeUtility.MemClear((byte*)destination + byteCount, staleByteCount);
                    }
                }
                finally
                {
                    vault.ReleaseMutationGuard(SuitProfileImportMutationGuardMask);
                }

                return true;
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
                System.Threading.Volatile.Write(ref s_csvImportScratchBusy, 0);
            }
        }

        private static int ReadCsvBytesCold(string path, byte[] scratch, int maxBytes)
        {
            if (scratch == null || scratch.Length <= 0 || maxBytes <= 0)
                return 0;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long boundedLength = stream.Length < maxBytes ? stream.Length : maxBytes;
                int byteCount = boundedLength > scratch.Length ? scratch.Length : (int)boundedLength;
                return byteCount > 0 ? stream.Read(scratch, 0, byteCount) : 0;
            }
        }
#endif

        /// <summary>
        /// Reads one metabolism state row for editor or diagnostics.
        /// </summary>
        public bool TryGetState(int entityIndex, out MetabolicStateDTO state)
        {
            state = default;
            if (_jobScheduled)
                return false;

            IDataVault vault = _dataVault;
            TryReadMetabolismVaultBuffer(
                vault,
                in _stateHandle,
                ShinobuMetabolismConstants.MetabolismStatesBuffer,
                1,
                out NativeArray<MetabolicStateDTO> states);
            if (!states.IsCreated || (uint)entityIndex >= (uint)states.Length)
                return false;

            state = states[entityIndex];
            return true;
        }

        /// <summary>
        /// Reads one metabolism AUP row for editor or diagnostics.
        /// </summary>
        public bool TryGetEntityAup(int entityIndex, out double3 aup)
        {
            aup = default;
            if (_jobScheduled)
                return false;

            IDataVault vault = _dataVault;
            TryReadMetabolismVaultBuffer(
                vault,
                in _entityAupHandle,
                ShinobuMetabolismConstants.MetabolismEntityAupsBuffer,
                1,
                out NativeArray<double3> entityAups);
            if (!entityAups.IsCreated || (uint)entityIndex >= (uint)entityAups.Length)
                return false;

            aup = entityAups[entityIndex];
            return math.all(math.isfinite(aup));
        }

        /// <summary>
        /// Reads the current metabolism tuning row.
        /// </summary>
        public bool TryGetTuning(out MetabolismTuningDTO tuning)
        {
            tuning = default;
            IDataVault vault = _dataVault;
            TryReadMetabolismVaultBuffer(
                vault,
                in _tuningHandle,
                ShinobuMetabolismConstants.MetabolismTuningBuffer,
                1,
                out NativeArray<MetabolismTuningDTO> tuningArray);
            if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                return false;

            tuning = ShinobuMetabolismJobMath.SanitizeTuning(tuningArray[0]);
            return true;
        }

        /// <summary>
        /// Writes the current metabolism tuning row from an editor facade.
        /// </summary>
        public bool TrySetTuning(in MetabolismTuningDTO tuning)
        {
            if (_jobScheduled)
                return false;

            MetabolismTuningDTO sanitizedTuning = ShinobuMetabolismJobMath.SanitizeTuning(tuning);
            IDataVault vault = _dataVault;
            if (vault == null ||
                !TryOpenMetabolismVaultBuffer(
                    vault,
                    in _tuningHandle,
                    ShinobuMetabolismConstants.MetabolismTuningBuffer,
                    1,
                    out NativeArray<MetabolismTuningDTO> tuningArray) ||
                !tuningArray.IsCreated ||
                tuningArray.Length <= 0)
            {
                return false;
            }

            if (!vault.TryAcquireMutationGuard(TuningMutationGuardMask))
                return false;

            try
            {
                ref MetabolismTuningDTO target = ref UnsafeUtility.AsRef<MetabolismTuningDTO>(
                    NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(tuningArray));
                target = sanitizedTuning;
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(TuningMutationGuardMask);
            }
        }

        /// <summary>
        /// Reads the most recent completed telemetry row.
        /// </summary>
        public bool TryGetLatestTelemetry(out MetabolicTelemetryEntry telemetry)
        {
            telemetry = _latestTelemetry;
            return _latestTelemetryValid;
        }

        /// <summary>
        /// Reads the most recent detailed player metabolism telemetry row.
        /// </summary>
        public bool TryGetLatestDetailTelemetry(out MetabolicDetailTelemetryEntry telemetry)
        {
            telemetry = _latestDetailTelemetry;
            return _latestDetailTelemetryValid;
        }

        /// <summary>
        /// Cold bridge for inventory/equipment owners to select a suit thermal profile row.
        /// </summary>
        public bool TrySetSuitProfileIndex(int entityIndex, ushort suitProfileIndex)
        {
            if (_jobScheduled)
                return false;

            int clamped = math.clamp((int)suitProfileIndex, 0, ShinobuMetabolismConstants.MaxSuitThermalProfiles - 1);
            IDataVault vault = _dataVault;
            if (vault == null ||
                !TryOpenMetabolismVaultBuffer(
                    vault,
                    in _suitProfileIndexHandle,
                    ShinobuMetabolismConstants.MetabolismSuitProfileIndicesBuffer,
                    entityCapacity,
                    out NativeArray<ushort> suitProfileIndices) ||
                !suitProfileIndices.IsCreated ||
                (uint)entityIndex >= (uint)suitProfileIndices.Length)
            {
                return false;
            }

            if (!vault.TryAcquireMutationGuard(SuitProfileIndexMutationGuardMask))
                return false;

            try
            {
                ushort* indices = (ushort*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(suitProfileIndices);
                ref ushort slot = ref UnsafeUtility.AsRef<ushort>((byte*)indices + entityIndex * sizeof(ushort));
                slot = (ushort)clamped;
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(SuitProfileIndexMutationGuardMask);
            }
        }

        /// <summary>
        /// Cold bridge for equipment owners that publish suit identity instead of profile index.
        /// </summary>
        public bool TrySetSuitProfileHash(int entityIndex, uint equippedSuitHash)
        {
            if (_jobScheduled || equippedSuitHash == 0u)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !TryOpenMetabolismVaultBuffer(
                    vault,
                    in _suitProfileIndexHandle,
                    ShinobuMetabolismConstants.MetabolismSuitProfileIndicesBuffer,
                    entityCapacity,
                    out NativeArray<ushort> suitProfileIndices) ||
                !TryReadMetabolismVaultBuffer(
                    vault,
                    in _suitProfileHandle,
                    ShinobuMetabolismConstants.MetabolismSuitThermalProfilesBuffer,
                    ShinobuMetabolismConstants.MaxSuitThermalProfiles,
                    out NativeArray<MetabolicSuitThermalProfileDTO> suitProfiles) ||
                (uint)entityIndex >= (uint)suitProfileIndices.Length)
            {
                return false;
            }

            if (!vault.TryAcquireMutationGuard(SuitProfileSelectionMutationGuardMask))
                return false;

            try
            {
                ushort resolvedIndex = ResolveSuitProfileIndexForHash(suitProfiles, equippedSuitHash, out bool matched);
                if (!matched)
                    return false;

                ushort* indices = (ushort*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(suitProfileIndices);
                ref ushort slot = ref UnsafeUtility.AsRef<ushort>((byte*)indices + entityIndex * sizeof(ushort));
                slot = resolvedIndex;
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(SuitProfileSelectionMutationGuardMask);
            }
        }

        /// <summary>
        /// Forces the 300-frame metabolism telemetry ring to disk.
        /// </summary>
        public bool DumpBlackBoxForEditor()
        {
            if (_jobScheduled)
                return false;

            IDataVault vault = _dataVault;
            TryReadMetabolismVaultBuffer(
                vault,
                in _telemetryHandle,
                ShinobuMetabolismConstants.MetabolismTelemetryRingBuffer,
                ShinobuMetabolismConstants.TelemetryFrameCount,
                out NativeArray<MetabolicTelemetryEntry> telemetry);
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return false;

            DumpBlackBox(telemetry);
            return true;
        }

        private void TryFinalizeFrameJobNoWait()
        {
            if (!_jobScheduled)
                return;

            if (!_activeJobHandle.IsCompleted)
                return;

            using (_CompleteMarker.Auto())
            {
                if (!DispatcherJobFence.TryFinalizeCompleted(ref _activeJobHandle))
                    return;

                FinishFrameJobCompletion();
            }
        }

        private void CompleteFrameJobForTeardown()
        {
            if (!_jobScheduled)
                return;

            using (_CompleteMarker.Auto())
            {
                if (!ForceCompleteInPostSimulationWindow(ref _activeJobHandle))
                    return;

                FinishFrameJobCompletion();
            }
        }

        private void FinishFrameJobCompletion()
        {
            float executionMicroseconds = ResolveJobExecutionMicroseconds();
            IDataVault vault = _dataVault;
            int stagedSignalCount = _scheduledCount;
            bool publishShaderGlobals = false;
            bool publishSignals = stagedSignalCount > 0;
            MetabolicTelemetryEntry shaderTelemetry = default;
            try
            {
                TryOpenMetabolismVaultBuffer(
                    vault,
                    in _telemetryHandle,
                    ShinobuMetabolismConstants.MetabolismTelemetryRingBuffer,
                    ShinobuMetabolismConstants.TelemetryFrameCount,
                    out NativeArray<MetabolicTelemetryEntry> telemetry);
                if (telemetry.IsCreated && (uint)_pendingTelemetryIndex < (uint)telemetry.Length)
                {
                    MetabolicTelemetryEntry entry = telemetry[_pendingTelemetryIndex];
                    if (executionMicroseconds > 200f)
                        entry.Flags |= ShinobuMetabolismFlags.ExecutionBudgetExceeded;
                    entry.ExecutionMicroseconds = executionMicroseconds;
                    telemetry[_pendingTelemetryIndex] = entry;
                    _latestTelemetry = entry;
                    _latestTelemetryValid = true;
                    TryReadMetabolismVaultBuffer(
                        vault,
                        in _detailTelemetryHandle,
                        ShinobuMetabolismConstants.MetabolismDetailTelemetryRingBuffer,
                        ShinobuMetabolismConstants.TelemetryFrameCount,
                        out NativeArray<MetabolicDetailTelemetryEntry> detailTelemetry);
                    if (detailTelemetry.IsCreated && (uint)_pendingTelemetryIndex < (uint)detailTelemetry.Length)
                    {
                        _latestDetailTelemetry = detailTelemetry[_pendingTelemetryIndex];
                        _latestDetailTelemetryValid = true;
                    }

                    _telemetryCursor = (_pendingTelemetryIndex + 1) % telemetry.Length;
                    shaderTelemetry = entry;
                    publishShaderGlobals = true;
                    if (!_autopsyDumped && (entry.Flags & (ShinobuMetabolismFlags.NanDetected | ShinobuMetabolismFlags.ExecutionBudgetExceeded)) != 0u)
                    {
                        DumpBlackBox(telemetry);
                        _autopsyDumped = true;
                    }
                }
            }
            finally
            {
                ReleaseThermalGridReadback();
                _jobScheduled = false;
                _pendingTelemetryIndex = -1;
                _scheduledCount = 0;
                UnlockJobBuffers();
            }

            if (publishShaderGlobals)
                QueueShaderGlobalsVisualSync(in shaderTelemetry);
            if (publishSignals)
                PublishStagedSignals(vault, stagedSignalCount);
        }

        private void QueueShaderGlobalsVisualSync(in MetabolicTelemetryEntry telemetry)
        {
            _pendingShaderGlobalsTelemetry = telemetry;
            _pendingShaderGlobals = true;
        }

        private void FlushPendingShaderGlobalsVisualSync()
        {
            if (!_pendingShaderGlobals)
                return;

            _pendingShaderGlobals = false;
            PublishShaderGlobals(in _pendingShaderGlobalsTelemetry);
        }

        private void PublishStagedSignals(IDataVault vault, int scheduledCount)
        {
            if (vault == null || scheduledCount <= 0)
                return;

            TryReadMetabolismVaultBuffer(
                vault,
                in _physiologySignalHandle,
                ShinobuMetabolismConstants.MetabolismPhysiologySignalsBuffer,
                ResolvePhysiologySignalCapacity(scheduledCount),
                out NativeArray<PhysiologyStateSignal> physiologySignals);
            TryReadMetabolismVaultBuffer(
                vault,
                in _exposureSignalHandle,
                ShinobuMetabolismConstants.MetabolismExposureSignalsBuffer,
                ResolveExposureSignalCapacity(scheduledCount),
                out NativeArray<MetabolicExposureSignalDTO> exposureSignals);
            int physiologyLimit = physiologySignals.IsCreated
                ? math.min(physiologySignals.Length, ResolvePhysiologySignalCapacity(scheduledCount))
                : 0;
            int exposureLimit = exposureSignals.IsCreated
                ? math.min(exposureSignals.Length, ResolveExposureSignalCapacity(scheduledCount))
                : 0;

            for (int i = 0; i < physiologyLimit; i++)
            {
                PhysiologyStateSignal signal = physiologySignals[i];
                if (signal.SourceHash == 0u || signal.Frame == 0u)
                    continue;

                SignalBus<PhysiologyStateSignal>.TryPushTracked(in signal, ref s_x001ShinobuMetabolismRuntimeSignalPushDropCount);
            }

            for (int i = 0; i < exposureLimit; i++)
            {
                MetabolicExposureSignalDTO signal = exposureSignals[i];
                if (signal.EntityHash == 0u || signal.Frame == 0u || signal.ToxemiaDelta <= 0f)
                    continue;

                int slot = i % ShinobuMetabolismConstants.MetabolicExposureSignalsPerEntity;
                if (slot == ShinobuMetabolismConstants.MetabolicExposureSignalSlotToxic)
                {
                    ToxicityExposureSignal exposure = default;
                    exposure.AUP = signal.AUP;
                    exposure.Exposure01 = math.saturate(signal.Exposure01);
                    exposure.ToxemiaDelta = math.saturate(signal.ToxemiaDelta);
                    exposure.EntityId = signal.EntityHash;
                    exposure.ChemicalHash = signal.ChemicalHash != 0u ? signal.ChemicalHash : MetabolicToxicChemicalHash;
                    exposure.Frame = signal.Frame;
                    exposure.Flags = 1;
                    if (exposure.EntityId != 0u)
                    {
                        SignalBus<ToxicityExposureSignal>.TryPushTracked(in exposure, ref s_x001ShinobuMetabolismRuntimeSignalPushDropCount);
                    }

                    continue;
                }
            }
        }

        private bool EnsureVaultState()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            int physiologySignalCapacity = ResolvePhysiologySignalCapacity(entityCapacity);
            int exposureSignalCapacity = ResolveExposureSignalCapacity(entityCapacity);
            return OpenOrAcquireMetabolismVaultBuffer(
                       vault,
                       ref _stateHandle,
                       ShinobuMetabolismConstants.MetabolismStatesBuffer,
                       entityCapacity,
                       NativeArrayOptions.UninitializedMemory,
                       out _) &&
                   OpenOrAcquireMetabolismVaultBuffer(
                       vault,
                       ref _entityAupHandle,
                       ShinobuMetabolismConstants.MetabolismEntityAupsBuffer,
                       entityCapacity,
                       NativeArrayOptions.UninitializedMemory,
                       out _) &&
                   OpenOrAcquireMetabolismVaultBuffer(
                       vault,
                       ref _exertionHandle,
                       ShinobuMetabolismConstants.MetabolismExertionBuffer,
                       entityCapacity,
                       NativeArrayOptions.UninitializedMemory,
                       out _) &&
                   OpenOrAcquireMetabolismVaultBuffer(
                       vault,
                       ref _speciesRuleHandle,
                       ShinobuMetabolismConstants.MetabolismSpeciesRulesBuffer,
                       ShinobuMetabolismConstants.MaxSpeciesRules,
                       NativeArrayOptions.UninitializedMemory,
                       out _) &&
                   OpenOrAcquireMetabolismVaultBuffer(
                       vault,
                       ref _ruleIndexHandle,
                       ShinobuMetabolismConstants.MetabolismRuleIndicesBuffer,
                       entityCapacity,
                       NativeArrayOptions.UninitializedMemory,
                       out _) &&
                   OpenOrAcquireMetabolismVaultBuffer(
                       vault,
                       ref _telemetryHandle,
                       ShinobuMetabolismConstants.MetabolismTelemetryRingBuffer,
                       ShinobuMetabolismConstants.TelemetryFrameCount,
                       NativeArrayOptions.UninitializedMemory,
                       out _) &&
                   OpenOrAcquireMetabolismVaultBuffer(
                       vault,
                       ref _tuningHandle,
                       ShinobuMetabolismConstants.MetabolismTuningBuffer,
                       1,
                       NativeArrayOptions.UninitializedMemory,
                       out _) &&
                   OpenOrAcquireMetabolismVaultBuffer(
                       vault,
                       ref _toxinSampleHandle,
                       ShinobuMetabolismConstants.MetabolismToxinSamplesBuffer,
                       entityCapacity,
                       NativeArrayOptions.UninitializedMemory,
                       out _) &&
                   OpenOrAcquireMetabolismVaultBuffer(
                       vault,
                       ref _physiologySignalHandle,
                       ShinobuMetabolismConstants.MetabolismPhysiologySignalsBuffer,
                       physiologySignalCapacity,
                       NativeArrayOptions.UninitializedMemory,
                       out _) &&
                   OpenOrAcquireMetabolismVaultBuffer(
                       vault,
                       ref _exposureSignalHandle,
                       ShinobuMetabolismConstants.MetabolismExposureSignalsBuffer,
                       exposureSignalCapacity,
                       NativeArrayOptions.UninitializedMemory,
                       out _) &&
                   OpenOrAcquireMetabolismVaultBuffer(
                       vault,
                       ref _detailTelemetryHandle,
                       ShinobuMetabolismConstants.MetabolismDetailTelemetryRingBuffer,
                       ShinobuMetabolismConstants.TelemetryFrameCount,
                       NativeArrayOptions.UninitializedMemory,
                       out _) &&
                   OpenOrAcquireMetabolismVaultBuffer(
                       vault,
                       ref _suitProfileHandle,
                       ShinobuMetabolismConstants.MetabolismSuitThermalProfilesBuffer,
                       ShinobuMetabolismConstants.MaxSuitThermalProfiles,
                       NativeArrayOptions.UninitializedMemory,
                       out _) &&
                   OpenOrAcquireMetabolismVaultBuffer(
                       vault,
                       ref _suitProfileIndexHandle,
                       ShinobuMetabolismConstants.MetabolismSuitProfileIndicesBuffer,
                       entityCapacity,
                       NativeArrayOptions.UninitializedMemory,
                       out _);
        }

        private void PrepareRuntimeStateCold(bool loadEditorProfiles)
        {
            if (_dataVault == null)
            {
                _vaultRepairRequested = true;
                return;
            }

            EnsureShaderGlobalsBuffers();
            if (!EnsureVaultState())
            {
                _vaultRepairRequested = true;
                return;
            }

            InitializeDefaultVaultContents();
#if UNITY_EDITOR
            if (loadEditorProfiles)
            {
                if (loadCsvProfilesOnEnable)
                    TryLoadBiologicalProfilesCsv();
                if (loadSuitThermalProfilesOnEnable)
                    TryLoadSuitThermalProfilesCsv();
            }
#endif
            _vaultRepairRequested = !HasVaultStateReady();
        }

        private bool HasVaultStateReady()
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !_defaultsInitialized)
            {
                return false;
            }

            return TryResolveBuffers(
                vault,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _);
        }

        private static int ResolvePhysiologySignalCapacity(int entityCount)
        {
            int count = math.max(1, entityCount);
            int maxSafe = int.MaxValue / ShinobuMetabolismConstants.PhysiologySignalsPerEntity;
            count = math.min(count, maxSafe);
            return count * ShinobuMetabolismConstants.PhysiologySignalsPerEntity;
        }

        private static int ResolveExposureSignalCapacity(int entityCount)
        {
            int count = math.max(1, entityCount);
            int maxSafe = int.MaxValue / ShinobuMetabolismConstants.MetabolicExposureSignalsPerEntity;
            count = math.min(count, maxSafe);
            return count * ShinobuMetabolismConstants.MetabolicExposureSignalsPerEntity;
        }

        private void InitializeDefaultVaultContents()
        {
            if (_defaultsInitialized)
                return;

            if (generateMockEcosystemOnEnable)
                GenerateMockEcosystemMetabolism();
            else
                InitializeRulesAndTuningOnly();
        }

        private void InitializeRulesAndTuningOnly()
        {
            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(BootstrapMutationGuardMask))
            {
                return;
            }

            try
            {
                if (!TryResolveBuffers(
                        vault,
                        out NativeArray<MetabolicStateDTO> states,
                        out NativeArray<double3> entityAups,
                        out NativeArray<float> exertion,
                        out NativeArray<MetabolicSpeciesRuleDTO> rules,
                        out NativeArray<ushort> ruleIndices,
                        out _,
                        out NativeArray<MetabolismTuningDTO> tuningArray,
                        out NativeArray<float> toxinSamples,
                        out NativeArray<PhysiologyStateSignal> physiologySignals,
                        out NativeArray<MetabolicExposureSignalDTO> exposureSignals,
                        out _,
                        out NativeArray<MetabolicSuitThermalProfileDTO> suitProfiles,
                        out NativeArray<ushort> suitProfileIndices))
                {
                    return;
                }

                int count = ResolveEntityCount(states, entityAups, exertion, ruleIndices, toxinSamples);
                if (count <= 0 || rules.Length <= 0 || tuningArray.Length <= 0)
                    return;

                InitMetabolismRulesJob initRulesJob = default;
                initRulesJob.Rules = (MetabolicSpeciesRuleDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rules);
                initRulesJob.SuitProfiles = (MetabolicSuitThermalProfileDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(suitProfiles);
                initRulesJob.Tuning = (MetabolismTuningDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(tuningArray);
                initRulesJob.RuleCount = rules.Length;
                initRulesJob.SuitProfileCount = suitProfiles.Length;
                JobHandle initRules = initRulesJob.Schedule();

                InitInactiveMetabolismJob initInactiveJob = default;
                initInactiveJob.States = (MetabolicStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states);
                initInactiveJob.EntityAups = (double3*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(entityAups);
                initInactiveJob.ExertionSpeedSq = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(exertion);
                initInactiveJob.ToxinSamples = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(toxinSamples);
                initInactiveJob.RuleIndices = (ushort*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(ruleIndices);
                initInactiveJob.SuitProfileIndices = (ushort*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(suitProfileIndices);
                initInactiveJob.PhysiologySignals = (PhysiologyStateSignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(physiologySignals);
                initInactiveJob.ExposureSignals = (MetabolicExposureSignalDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(exposureSignals);
                initInactiveJob.Count = count;
                initInactiveJob.PhysiologySignalLength = physiologySignals.Length;
                initInactiveJob.ExposureSignalLength = exposureSignals.Length;
                JobHandle initInactive = initInactiveJob.Schedule(count, ShinobuMetabolismConstants.FrameJobBatchSize, initRules);

                // COLD SYNC JOB: default metabolism rule and inactive-row hydration, not part of gameplay SlowTick.
                if (ForceCompleteInPostSimulationWindow(ref initInactive))
                    _defaultsInitialized = true;
            }
            finally
            {
                vault.ReleaseMutationGuard(BootstrapMutationGuardMask);
            }
        }

        private bool TryResolveBuffers(
            IDataVault vault,
            out NativeArray<MetabolicStateDTO> states,
            out NativeArray<double3> entityAups,
            out NativeArray<float> exertion,
            out NativeArray<MetabolicSpeciesRuleDTO> rules,
            out NativeArray<ushort> ruleIndices,
            out NativeArray<MetabolicTelemetryEntry> telemetry,
            out NativeArray<MetabolismTuningDTO> tuningArray,
            out NativeArray<float> toxinSamples,
            out NativeArray<PhysiologyStateSignal> physiologySignals,
            out NativeArray<MetabolicExposureSignalDTO> exposureSignals,
            out NativeArray<MetabolicDetailTelemetryEntry> detailTelemetry,
            out NativeArray<MetabolicSuitThermalProfileDTO> suitProfiles,
            out NativeArray<ushort> suitProfileIndices)
        {
            int physiologySignalCapacity = ResolvePhysiologySignalCapacity(entityCapacity);
            int exposureSignalCapacity = ResolveExposureSignalCapacity(entityCapacity);
            TryOpenMetabolismVaultBuffer(vault, in _stateHandle, ShinobuMetabolismConstants.MetabolismStatesBuffer, entityCapacity, out states);
            TryOpenMetabolismVaultBuffer(vault, in _entityAupHandle, ShinobuMetabolismConstants.MetabolismEntityAupsBuffer, entityCapacity, out entityAups);
            TryOpenMetabolismVaultBuffer(vault, in _exertionHandle, ShinobuMetabolismConstants.MetabolismExertionBuffer, entityCapacity, out exertion);
            TryOpenMetabolismVaultBuffer(vault, in _speciesRuleHandle, ShinobuMetabolismConstants.MetabolismSpeciesRulesBuffer, ShinobuMetabolismConstants.MaxSpeciesRules, out rules);
            TryOpenMetabolismVaultBuffer(vault, in _ruleIndexHandle, ShinobuMetabolismConstants.MetabolismRuleIndicesBuffer, entityCapacity, out ruleIndices);
            TryOpenMetabolismVaultBuffer(vault, in _telemetryHandle, ShinobuMetabolismConstants.MetabolismTelemetryRingBuffer, ShinobuMetabolismConstants.TelemetryFrameCount, out telemetry);
            TryOpenMetabolismVaultBuffer(vault, in _tuningHandle, ShinobuMetabolismConstants.MetabolismTuningBuffer, 1, out tuningArray);
            TryOpenMetabolismVaultBuffer(vault, in _toxinSampleHandle, ShinobuMetabolismConstants.MetabolismToxinSamplesBuffer, entityCapacity, out toxinSamples);
            TryOpenMetabolismVaultBuffer(vault, in _physiologySignalHandle, ShinobuMetabolismConstants.MetabolismPhysiologySignalsBuffer, physiologySignalCapacity, out physiologySignals);
            TryOpenMetabolismVaultBuffer(vault, in _exposureSignalHandle, ShinobuMetabolismConstants.MetabolismExposureSignalsBuffer, exposureSignalCapacity, out exposureSignals);
            TryOpenMetabolismVaultBuffer(vault, in _detailTelemetryHandle, ShinobuMetabolismConstants.MetabolismDetailTelemetryRingBuffer, ShinobuMetabolismConstants.TelemetryFrameCount, out detailTelemetry);
            TryOpenMetabolismVaultBuffer(vault, in _suitProfileHandle, ShinobuMetabolismConstants.MetabolismSuitThermalProfilesBuffer, ShinobuMetabolismConstants.MaxSuitThermalProfiles, out suitProfiles);
            TryOpenMetabolismVaultBuffer(vault, in _suitProfileIndexHandle, ShinobuMetabolismConstants.MetabolismSuitProfileIndicesBuffer, entityCapacity, out suitProfileIndices);
            return states.IsCreated &&
                   entityAups.IsCreated &&
                   exertion.IsCreated &&
                   rules.IsCreated &&
                   ruleIndices.IsCreated &&
                   telemetry.IsCreated &&
                   tuningArray.IsCreated &&
                   toxinSamples.IsCreated &&
                   physiologySignals.IsCreated &&
                   exposureSignals.IsCreated &&
                   detailTelemetry.IsCreated &&
                   suitProfiles.IsCreated &&
                   suitProfileIndices.IsCreated;
        }

        private static ushort ResolveSuitProfileIndexForHash(
            NativeArray<MetabolicSuitThermalProfileDTO> suitProfiles,
            uint equippedSuitHash,
            out bool matched)
        {
            matched = false;
            if (!suitProfiles.IsCreated || suitProfiles.Length <= 0 || equippedSuitHash == 0u)
                return 0;

            for (int i = 0; i < suitProfiles.Length && i <= ushort.MaxValue; i++)
            {
                MetabolicSuitThermalProfileDTO profile = ShinobuMetabolismJobMath.SanitizeSuitProfile(suitProfiles[i]);
                if (!ShinobuMetabolismJobMath.SuitProfileHashMatches(profile.ProfileHash, equippedSuitHash))
                    continue;

                matched = true;
                return (ushort)i;
            }

            return 0;
        }

        private bool TryReadSuitIntegrityStates(
            IDataVault vault,
            int requiredLength,
            out NativeArray<SuitIntegrityDTO> states)
        {
            if (TryReadExistingSuitIntegrityStates(vault, in _suitIntegrityStateReadHandle, requiredLength, out states))
                return true;

            states = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !vault.TryGetGenerationHandle<SuitIntegrityDTO>(
                    ShinobuSuitIntegrityConstants.StateBuffer,
                    out _suitIntegrityStateReadHandle))
            {
                return false;
            }

            return TryReadExistingSuitIntegrityStates(vault, in _suitIntegrityStateReadHandle, requiredLength, out states);
        }

        private static bool TryReadExistingSuitIntegrityStates(
            IDataVault vault,
            in VaultGenerationHandle<SuitIntegrityDTO> handle,
            int requiredLength,
            out NativeArray<SuitIntegrityDTO> states)
        {
            states = default;
            return vault != null &&
                   requiredLength > 0 &&
                   IsSuitIntegrityStateReadHandle(in handle) &&
                   vault.TryReadHandle(in handle, out states) &&
                   states.IsCreated &&
                   states.Length >= requiredLength;
        }

        private static bool IsSuitIntegrityStateReadHandle(in VaultGenerationHandle<SuitIntegrityDTO> handle)
        {
            return handle.BufferID == unchecked((uint)(int)ShinobuSuitIntegrityConstants.StateBuffer) &&
                   handle.SystemID == (uint)OwnerSystem &&
                   handle.Generation != 0u;
        }

        private static bool OpenOrAcquireMetabolismVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            if (TryOpenMetabolismVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer))
                return true;

            if (vault == null ||
                requiredLength <= 0 ||
                vault.IsAllocationLocked ||
                vault.IsCompactionFenceActive)
            {
                buffer = default;
                return false;
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                OwnerSystem,
                options);
            return TryOpenMetabolismVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryOpenMetabolismVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   requiredLength > 0 &&
                   IsMetabolismVaultHandle(in handle, bufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryReadMetabolismVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   requiredLength > 0 &&
                   IsMetabolismVaultHandle(in handle, bufferId) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsMetabolismVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)OwnerSystem &&
                   handle.Generation != 0u;
        }

        private static int ResolveEntityCount(
            NativeArray<MetabolicStateDTO> states,
            NativeArray<double3> entityAups,
            NativeArray<float> exertion,
            NativeArray<ushort> ruleIndices,
            NativeArray<float> toxinSamples)
        {
            int count = states.IsCreated ? states.Length : 0;
            count = math.min(count, entityAups.IsCreated ? entityAups.Length : 0);
            count = math.min(count, exertion.IsCreated ? exertion.Length : 0);
            count = math.min(count, ruleIndices.IsCreated ? ruleIndices.Length : 0);
            count = math.min(count, toxinSamples.IsCreated ? toxinSamples.Length : 0);
            return math.max(0, count);
        }

        private void TryApplyLatestKccSignal(
            NativeArray<MetabolicStateDTO> states,
            NativeArray<double3> entityAups,
            NativeArray<float> exertion)
        {
            if (!states.IsCreated || !entityAups.IsCreated || !exertion.IsCreated || states.Length == 0)
                return;

            ReadOnlySpan<KccVelocitySignal> signals = SignalBus<KccVelocitySignal>.GetFrameSnapshot();
            if (signals.Length == 0)
                return;

            KccVelocitySignal latest = default;
            bool found = false;
            for (int i = 0; i < signals.Length; i++)
            {
                KccVelocitySignal candidate = signals[i];
                if (!found || IsKccVelocitySignalNewer(in candidate, in latest))
                {
                    latest = candidate;
                    found = true;
                }
            }

            if (!found)
                return;

            double3 aup = latest.BodyAup.ToAbsoluteDouble3();
            if (math.all(math.isfinite(aup)))
                entityAups[0] = aup;

            float speedSq = math.max(0f, ShinobuMetabolismJobMath.SanitizeFinite(latest.PlanarSpeedSq, math.lengthsq(latest.Velocity.xz)));
            exertion[0] = speedSq;
            MetabolicStateDTO playerState = states[0];
            if (playerState.EntityHashID == 0u)
                playerState.EntityHashID = latest.SourceId != 0u ? latest.SourceId : ShinobuMetabolismConstants.SourceHash;
            states[0] = playerState;
        }

        private static bool IsKccVelocitySignalNewer(in KccVelocitySignal candidate, in KccVelocitySignal current)
        {
            if (candidate.Sequence != current.Sequence)
                return candidate.Sequence > current.Sequence;

            return candidate.Frame > current.Frame;
        }

        private void TryResolveThermalGrid(
            out NativeArray<float>.ReadOnly thermalGrid,
            out int3 thermalResolution,
            out double3 thermalRootAup,
            out float thermalCellSizeMeters,
            out byte hasThermalGrid)
        {
            ReleaseThermalGridReadback();
            thermalGrid = default;
            thermalResolution = default;
            thermalRootAup = double3.zero;
            thermalCellSizeMeters = 1f;
            hasThermalGrid = 0;

            IThermodynamicsService service = _thermodynamicsService;
            if (service == null || !service.IsInitialized)
                return;

            if (!service.TryAcquireThermalGridReadbackAup(
                    out thermalGrid,
                    out int width,
                    out int height,
                    out int depth,
                    out thermalRootAup,
                    out float cellSizeMeters,
                    out _))
            {
                return;
            }

            if (!thermalGrid.IsCreated || thermalGrid.Length <= 0 || width <= 0 || height <= 0 || depth <= 0)
            {
                service.ReleaseThermalGridReadback();
                thermalGrid = default;
                return;
            }

            thermalResolution.x = width;
            thermalResolution.y = height;
            thermalResolution.z = depth;
            thermalCellSizeMeters = math.max(0.001f, cellSizeMeters);
            if (!math.all(math.isfinite(thermalRootAup)))
            {
                service.ReleaseThermalGridReadback();
                thermalGrid = default;
                thermalRootAup = double3.zero;
                return;
            }

            _thermalGridReadbackService = service;
            _thermalGridReadbackHeld = true;
            hasThermalGrid = 1;
        }

        private void TryResolveChemicalGrid(
            IDataVault vault,
            out float4* publishedGrid,
            out float4* overlayGrid,
            out int3 chemicalResolution,
            out int chemicalGridLength,
            out double3 chemicalRootAup,
            out float chemicalCellSizeMeters,
            out byte hasChemicalGrid)
        {
            publishedGrid = null;
            overlayGrid = null;
            chemicalResolution = default;
            chemicalGridLength = 0;
            chemicalRootAup = double3.zero;
            chemicalCellSizeMeters = ShinobuMetabolismConstants.ChemicalDefaultCellSizeMeters;
            hasChemicalGrid = 0;

            if (vault == null || !_jobLocksHeld)
                return;

            if (!TryReadChemicalVaultBuffer(
                    vault,
                    ShinobuMetabolismConstants.ChemicalPublishedGridReadbackBuffer,
                    ShinobuMetabolismConstants.ChemicalGridCellCount,
                    out NativeArray<float4> published) ||
                !TryReadChemicalVaultBuffer(
                    vault,
                    ShinobuMetabolismConstants.ChemicalTelemetryReadbackBuffer,
                    ShinobuMetabolismConstants.TelemetryFrameCount,
                    out NativeArray<MetabolismChemicalTelemetryMirrorDTO> telemetryRing) ||
                !TryReadChemicalVaultBuffer(
                    vault,
                    ShinobuMetabolismConstants.ChemicalTelemetryCursorReadbackBuffer,
                    1,
                    out NativeArray<int> cursorBuffer) ||
                !TryReadChemicalVaultBuffer(
                    vault,
                    ShinobuMetabolismConstants.ChemicalTuningReadbackBuffer,
                    1,
                    out NativeArray<MetabolismChemicalTuningMirrorDTO> tuningBuffer))
            {
                return;
            }

            int cursor = cursorBuffer[0];
            int telemetryIndex = cursor - 1;
            if (telemetryIndex < 0)
                telemetryIndex += telemetryRing.Length;
            if ((uint)telemetryIndex >= (uint)telemetryRing.Length)
                telemetryIndex = 0;

            MetabolismChemicalTelemetryMirrorDTO telemetry = telemetryRing[telemetryIndex];
            if (telemetry.Frame == 0u || !math.all(math.isfinite(telemetry.GridOriginAup)))
            {
                return;
            }

            MetabolismChemicalTuningMirrorDTO tuning = tuningBuffer[0];
            float cellSize = math.isfinite(tuning.CellSizeMeters) && tuning.CellSizeMeters > 0.001f
                ? tuning.CellSizeMeters
                : ShinobuMetabolismConstants.ChemicalDefaultCellSizeMeters;

            float4* overlayPtr = null;
            if (TryReadChemicalVaultBuffer(
                    vault,
                    ShinobuMetabolismConstants.ChemicalOverlayGridReadbackBuffer,
                    ShinobuMetabolismConstants.ChemicalGridCellCount,
                    out NativeArray<float4> overlay))
            {
                overlayPtr = (float4*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(overlay);
            }

            publishedGrid = (float4*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(published);
            overlayGrid = overlayPtr;
            chemicalResolution.x = ShinobuMetabolismConstants.ChemicalGridAxisX;
            chemicalResolution.y = ShinobuMetabolismConstants.ChemicalGridAxisY;
            chemicalResolution.z = ShinobuMetabolismConstants.ChemicalGridAxisZ;
            chemicalGridLength = ShinobuMetabolismConstants.ChemicalGridCellCount;
            chemicalRootAup = telemetry.GridOriginAup;
            chemicalCellSizeMeters = cellSize;
            hasChemicalGrid = 1;
        }

        private static bool TryReadChemicalVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) ||
                !IsChemicalVaultHandle(in handle, bufferId) ||
                !vault.TryReadHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsChemicalVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)ChemicalOwnerSystem &&
                   handle.Generation != 0u;
        }

        private float ResolveSlowTickDeltaSeconds()
        {
            ITickDispatcher dispatcher = _tickDispatcher;
            if (dispatcher != null && dispatcher.SimulationPaused)
                return 0f;

            if (dispatcher != null)
            {
                H8TimeSnapshot snapshot = dispatcher.TimeSnapshot;
                if (double.IsFinite(snapshot.Time))
                {
                    double delta = _lastDispatcherTimeSeconds >= 0d
                        ? snapshot.Time - _lastDispatcherTimeSeconds
                        : ShinobuMetabolismConstants.DispatcherSlowTickSeconds;
                    _lastDispatcherTimeSeconds = snapshot.Time;
                    if (double.IsFinite(delta) && delta > 0d)
                        return math.clamp((float)delta, 0.0001f, ShinobuMetabolismConstants.MaxAccumulatedDeltaSeconds);
                }
            }

            return ShinobuMetabolismConstants.DispatcherSlowTickSeconds;
        }

        private static float ResolveGlobalQualityWeight()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f);

            float weight = HomeostasisBrain.GlobalQualityWeight;
            if (math.isfinite(weight))
                return math.saturate(weight);

            weight = SignalBusRegistry.GlobalQualityWeight01;
            return math.isfinite(weight) ? math.saturate(weight) : 1f;
        }

        private void RebindColdServices()
        {
            _dataVault = GlobalRegistry.DataVault;
            _thermodynamicsService = GlobalRegistry.ThermodynamicsService;
            _tickDispatcher = GlobalRegistry.TickDispatcher;
        }

        private void TryRegisterTicks()
        {
            if (!_registeredColdTick)
                _registeredColdTick = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Player);

            if (!HasVaultStateReady())
                return;

            if (!_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);

            if (!_registeredSlowTick || !_registeredLateFrame)
            {
                if (_registeredSlowTick)
                {
                    GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
                    _registeredSlowTick = false;
                }

                if (_registeredLateFrame)
                {
                    GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                    _registeredLateFrame = false;
                }
            }
        }

        private void TryUnregisterTicks()
        {
            if (_registeredColdTick)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Player);
                _registeredColdTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
                _registeredSlowTick = false;
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

        private bool TryLockJobBuffers(IDataVault vault)
        {
            if (vault == null || _jobLocksHeld)
                return false;

            if (!vault.TryAcquireMutationGuard(JobMutationGuardMask))
                return false;

            _jobLocksHeld = true;
            return true;
        }

        private void ReleaseThermalGridReadback()
        {
            if (!_thermalGridReadbackHeld)
                return;

            IThermodynamicsService service = _thermalGridReadbackService;
            if (service != null)
                service.ReleaseThermalGridReadback();
            _thermalGridReadbackService = null;
            _thermalGridReadbackHeld = false;
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

        private static bool ForceCompleteInPostSimulationWindow(ref JobHandle handle)
        {
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                return DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        private static ulong MutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private float ResolveJobExecutionMicroseconds()
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - _jobScheduleTimestamp;
            if (elapsedTicks <= 0L)
                return 0f;

            double microseconds = elapsedTicks * 1000000.0 / Stopwatch.Frequency;
            return double.IsFinite(microseconds) ? (float)math.min(microseconds, float.MaxValue) : 0f;
        }

        private bool EnsureShaderGlobalsBuffers()
        {
            if (!_supportsConstantBufferCold)
                return false;

            int stride = UnsafeUtility.SizeOf<MetabolismShaderGlobalsDTO>();
            if (_shaderGlobalsBufferA != null && _shaderGlobalsBufferA.IsValid() &&
                _shaderGlobalsBufferB != null && _shaderGlobalsBufferB.IsValid())
            {
                return true;
            }

            ReleaseShaderGlobalsBuffers();
            _shaderGlobalsBufferA = new GraphicsBuffer(
                GraphicsBuffer.Target.Constant,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                stride); // COLD ALLOC: GraphicsBuffer[1] - metabolism frost shader globals buffer A - owner: ShinobuMetabolismRuntime
            _shaderGlobalsBufferB = new GraphicsBuffer(
                GraphicsBuffer.Target.Constant,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                1,
                stride); // COLD ALLOC: GraphicsBuffer[1] - metabolism frost shader globals buffer B - owner: ShinobuMetabolismRuntime
            _shaderGlobalsInitialized = false;
            return _shaderGlobalsBufferA.IsValid() && _shaderGlobalsBufferB.IsValid();
        }

        private void ReleaseShaderGlobalsBuffers()
        {
            _shaderGlobalsBufferA?.Release();
            _shaderGlobalsBufferB?.Release();
            _shaderGlobalsBufferA = null;
            _shaderGlobalsBufferB = null;
            _activeShaderGlobalsBuffer = null;
            _shaderGlobalsInitialized = false;
        }

        private void PublishShaderGlobals(in MetabolicTelemetryEntry telemetry)
        {
            MetabolismTuningDTO tuning = default;
            TryGetTuning(out tuning);
            tuning = ShinobuMetabolismJobMath.SanitizeTuning(tuning);
            float entityCount = math.max(1f, telemetry.EntityCount);
            float frostDenominator = math.max(0.0001f, tuning.FrostStartTemperatureCelsius - tuning.FrostFullTemperatureCelsius);
            MetabolismShaderGlobalsDTO globals = default;
            globals.FrostScalar = math.saturate((tuning.FrostStartTemperatureCelsius - telemetry.MinimumCoreTemperature) * math.rcp(frostDenominator));
            globals.AverageCoreTemperature = ShinobuMetabolismJobMath.SanitizeFinite(telemetry.AverageCoreTemperature, 37f);
            globals.MinimumCoreTemperature = ShinobuMetabolismJobMath.SanitizeFinite(telemetry.MinimumCoreTemperature, 37f);
            globals.GlobalQualityWeight = math.saturate(telemetry.GlobalQualityWeight);
            globals.ToxicityScalar = math.saturate(telemetry.MaximumToxicity);
            globals.StarvationScalar = math.saturate(telemetry.StarvationCount * math.rcp(entityCount));
            globals.DehydrationScalar = math.saturate(telemetry.DehydrationCount * math.rcp(entityCount));
            globals.ReservedVisualOverkill.x = globals.FrostScalar * globals.GlobalQualityWeight;
            globals.ReservedVisualOverkill.y = globals.ToxicityScalar * globals.GlobalQualityWeight;
            globals.ReservedVisualOverkill.z = globals.StarvationScalar;
            globals.ReservedVisualOverkill.w = globals.DehydrationScalar;
            globals.Frame = telemetry.Frame;
            globals.Flags = telemetry.Flags;

            Shader.SetGlobalFloat(_FrostScalarId, globals.FrostScalar);

            if (!EnsureShaderGlobalsBuffers())
                return;

            if (_shaderGlobalsInitialized && ShaderGlobalsEqual(in _lastShaderGlobals, in globals))
            {
                if (_activeShaderGlobalsBuffer != null && _activeShaderGlobalsBuffer.IsValid())
                    Shader.SetGlobalConstantBuffer(_GlobalsBufferId, _activeShaderGlobalsBuffer, 0, ShinobuMetabolismConstants.ShaderGlobalsStrideBytes);
                return;
            }

            GraphicsBuffer writeBuffer = (_shaderWriteIndex++ & 1) == 0 ? _shaderGlobalsBufferA : _shaderGlobalsBufferB;
            if (writeBuffer == null || !writeBuffer.IsValid())
                return;

            NativeArray<MetabolismShaderGlobalsDTO> mapped = writeBuffer.LockBufferForWrite<MetabolismShaderGlobalsDTO>(0, 1);
            try
            {
                mapped[0] = globals;
            }
            finally
            {
                writeBuffer.UnlockBufferAfterWrite<MetabolismShaderGlobalsDTO>(1);
            }
            _lastShaderGlobals = globals;
            _shaderGlobalsInitialized = true;
            _activeShaderGlobalsBuffer = writeBuffer;
            Shader.SetGlobalConstantBuffer(_GlobalsBufferId, writeBuffer, 0, ShinobuMetabolismConstants.ShaderGlobalsStrideBytes);
        }

        private static bool ShaderGlobalsEqual(in MetabolismShaderGlobalsDTO left, in MetabolismShaderGlobalsDTO right)
        {
            return math.abs(left.FrostScalar - right.FrostScalar) <= 0.0001f &&
                   math.abs(left.AverageCoreTemperature - right.AverageCoreTemperature) <= 0.0001f &&
                   math.abs(left.MinimumCoreTemperature - right.MinimumCoreTemperature) <= 0.0001f &&
                   math.abs(left.ToxicityScalar - right.ToxicityScalar) <= 0.0001f &&
                   math.abs(left.StarvationScalar - right.StarvationScalar) <= 0.0001f &&
                   math.abs(left.DehydrationScalar - right.DehydrationScalar) <= 0.0001f &&
                   left.Frame == right.Frame &&
                   left.Flags == right.Flags;
        }

#if UNITY_EDITOR
        private static int ParseBiologicalProfilesCsv(ReadOnlySpan<byte> bytes, NativeArray<MetabolicSpeciesRuleDTO> rules)
        {
            if (!rules.IsCreated || rules.Length <= 0)
                return 0;

            return ParseBiologicalProfilesCsv(
                bytes,
                new Span<MetabolicSpeciesRuleDTO>(NativeArrayUnsafeUtility.GetUnsafePtr(rules), rules.Length));
        }

        private static int ParseBiologicalProfilesCsv(ReadOnlySpan<byte> bytes, Span<MetabolicSpeciesRuleDTO> rules)
        {
            int cursor = 0;
            int writeIndex = 0;
            while (cursor < bytes.Length && writeIndex < rules.Length)
            {
                int lineStart = cursor;
                while (cursor < bytes.Length && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                    cursor++;

                int lineEnd = cursor;
                while (cursor < bytes.Length && (bytes[cursor] == (byte)'\n' || bytes[cursor] == (byte)'\r'))
                    cursor++;

                if (TryParseSpeciesRuleLine(bytes.Slice(lineStart, lineEnd - lineStart), out MetabolicSpeciesRuleDTO rule))
                    rules[writeIndex++] = ShinobuMetabolismJobMath.SanitizeRule(rule);
            }

            return writeIndex;
        }

        private static int ParseSuitThermalProfilesCsv(ReadOnlySpan<byte> bytes, NativeArray<MetabolicSuitThermalProfileDTO> profiles)
        {
            if (!profiles.IsCreated || profiles.Length <= 0)
                return 0;

            return ParseSuitThermalProfilesCsv(
                bytes,
                new Span<MetabolicSuitThermalProfileDTO>(NativeArrayUnsafeUtility.GetUnsafePtr(profiles), profiles.Length));
        }

        private static int ParseSuitThermalProfilesCsv(ReadOnlySpan<byte> bytes, Span<MetabolicSuitThermalProfileDTO> profiles)
        {
            int cursor = 0;
            int writeIndex = 0;
            while (cursor < bytes.Length && writeIndex < profiles.Length)
            {
                int lineStart = cursor;
                while (cursor < bytes.Length && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                    cursor++;

                int lineEnd = cursor;
                while (cursor < bytes.Length && (bytes[cursor] == (byte)'\n' || bytes[cursor] == (byte)'\r'))
                    cursor++;

                if (TryParseSuitProfileLine(bytes.Slice(lineStart, lineEnd - lineStart), out MetabolicSuitThermalProfileDTO profile))
                    profiles[writeIndex++] = ShinobuMetabolismJobMath.SanitizeSuitProfile(profile);
            }

            if (writeIndex <= 0)
                return 0;

            for (int i = writeIndex; i < profiles.Length; i++)
                profiles[i] = default;

            return writeIndex;
        }
        private static bool TryParseSuitProfileLine(ReadOnlySpan<byte> line, out MetabolicSuitThermalProfileDTO profile)
        {
            profile = default;
            int cursor = 0;
            if (!TryReadColumn(line, ref cursor, out ReadOnlySpan<byte> key))
                return false;
            if (key.Length == 0 || key[0] == (byte)'#')
                return false;
            if (IsSuitProfileHeader(key))
                return false;

            uint keyHash = HashLowerAscii(key);
            profile = ShinobuMetabolismJobMath.BuildDefaultSuitProfile(keyHash);
            profile.Flags = ShinobuMetabolismSuitProfileFlags.CsvProfile;

            if (TryReadFloatColumn(line, ref cursor, out float value)) profile.ConductanceMultiplier = value;
            if (TryReadFloatColumn(line, ref cursor, out value)) profile.Insulation01 = value;
            if (TryReadFloatColumn(line, ref cursor, out value)) profile.ShiverMultiplier = value;
            if (TryReadFloatColumn(line, ref cursor, out value)) profile.HeatHydrationMultiplier = value;
            if (TryReadFloatColumn(line, ref cursor, out value)) profile.BatteryHeatingCelsiusPerSecond = value;
            return true;
        }

        private static bool TryParseSpeciesRuleLine(ReadOnlySpan<byte> line, out MetabolicSpeciesRuleDTO rule)
        {
            rule = default;
            int cursor = 0;
            if (!TryReadColumn(line, ref cursor, out ReadOnlySpan<byte> key))
                return false;
            if (key.Length == 0 || key[0] == (byte)'#')
                return false;
            if (IsSpeciesProfileHeader(key))
                return false;

            uint keyHash = HashLowerAscii(key);
            rule = ShinobuMetabolismJobMath.BuildDefaultRule(keyHash);
            rule.Flags |= ShinobuMetabolismFlags.CsvProfile;

            if (TryReadFloatColumn(line, ref cursor, out float value)) rule.MaxCalories = value;
            if (TryReadFloatColumn(line, ref cursor, out value)) rule.MaxHydration = value;
            if (TryReadFloatColumn(line, ref cursor, out value)) rule.BaseCalorieDrainPerSecond = value;
            if (TryReadFloatColumn(line, ref cursor, out value)) rule.BaseHydrationDrainPerSecond = value;
            if (TryReadFloatColumn(line, ref cursor, out value)) rule.ThermalConductance = value;
            if (TryReadFloatColumn(line, ref cursor, out value)) rule.ToxinSusceptibility = value;
            if (TryReadFloatColumn(line, ref cursor, out value)) rule.ShiverTemperatureCelsius = value;
            if (TryReadFloatColumn(line, ref cursor, out value)) rule.HypothermiaTemperatureCelsius = value;
            if (TryReadFloatColumn(line, ref cursor, out value)) rule.HeatHydrationLossScale = value;
            if (TryReadFloatColumn(line, ref cursor, out value)) rule.ToxicDamagePerSecond = value;
            if (TryReadFloatColumn(line, ref cursor, out value)) rule.RecoveryTemperatureCelsius = value;
            return true;
        }

        private static bool IsSuitProfileHeader(ReadOnlySpan<byte> key)
        {
            if (key.Length == 4)
            {
                bool isName = EqualsLower(key, 0, (byte)'n') &&
                              EqualsLower(key, 1, (byte)'a') &&
                              EqualsLower(key, 2, (byte)'m') &&
                              EqualsLower(key, 3, (byte)'e');
                bool isSuit = EqualsLower(key, 0, (byte)'s') &&
                              EqualsLower(key, 1, (byte)'u') &&
                              EqualsLower(key, 2, (byte)'i') &&
                              EqualsLower(key, 3, (byte)'t');
                return isName || isSuit;
            }

            if (key.Length == 7)
                return EqualsLower(key, 0, (byte)'p') &&
                       EqualsLower(key, 1, (byte)'r') &&
                       EqualsLower(key, 2, (byte)'o') &&
                       EqualsLower(key, 3, (byte)'f') &&
                       EqualsLower(key, 4, (byte)'i') &&
                       EqualsLower(key, 5, (byte)'l') &&
                       EqualsLower(key, 6, (byte)'e');

            if (key.Length == 9)
                return EqualsLower(key, 0, (byte)'s') &&
                       EqualsLower(key, 1, (byte)'u') &&
                       EqualsLower(key, 2, (byte)'i') &&
                       EqualsLower(key, 3, (byte)'t') &&
                       key[4] == (byte)'_' &&
                       EqualsLower(key, 5, (byte)'h') &&
                       EqualsLower(key, 6, (byte)'a') &&
                       EqualsLower(key, 7, (byte)'s') &&
                       EqualsLower(key, 8, (byte)'h');

            if (key.Length == 11)
                return EqualsLower(key, 0, (byte)'p') &&
                       EqualsLower(key, 1, (byte)'r') &&
                       EqualsLower(key, 2, (byte)'o') &&
                       EqualsLower(key, 3, (byte)'f') &&
                       EqualsLower(key, 4, (byte)'i') &&
                       EqualsLower(key, 5, (byte)'l') &&
                       EqualsLower(key, 6, (byte)'e') &&
                       key[7] == (byte)'_' &&
                       EqualsLower(key, 8, (byte)'i') &&
                       EqualsLower(key, 9, (byte)'d') &&
                       EqualsLower(key, 10, (byte)'x');

            if (key.Length == 12)
                return EqualsLower(key, 0, (byte)'p') &&
                       EqualsLower(key, 1, (byte)'r') &&
                       EqualsLower(key, 2, (byte)'o') &&
                       EqualsLower(key, 3, (byte)'f') &&
                       EqualsLower(key, 4, (byte)'i') &&
                       EqualsLower(key, 5, (byte)'l') &&
                       EqualsLower(key, 6, (byte)'e') &&
                       key[7] == (byte)'_' &&
                       EqualsLower(key, 8, (byte)'h') &&
                       EqualsLower(key, 9, (byte)'a') &&
                       EqualsLower(key, 10, (byte)'s') &&
                       EqualsLower(key, 11, (byte)'h');

            return false;
        }

        private static bool IsSpeciesProfileHeader(ReadOnlySpan<byte> key)
        {
            if (key.Length == 4)
                return EqualsLower(key, 0, (byte)'n') &&
                       EqualsLower(key, 1, (byte)'a') &&
                       EqualsLower(key, 2, (byte)'m') &&
                       EqualsLower(key, 3, (byte)'e');

            if (key.Length == 7)
                return EqualsLower(key, 0, (byte)'s') &&
                       EqualsLower(key, 1, (byte)'p') &&
                       EqualsLower(key, 2, (byte)'e') &&
                       EqualsLower(key, 3, (byte)'c') &&
                       EqualsLower(key, 4, (byte)'i') &&
                       EqualsLower(key, 5, (byte)'e') &&
                       EqualsLower(key, 6, (byte)'s');

            if (key.Length == 11)
                return EqualsLower(key, 0, (byte)'s') &&
                       EqualsLower(key, 1, (byte)'p') &&
                       EqualsLower(key, 2, (byte)'e') &&
                       EqualsLower(key, 3, (byte)'c') &&
                       EqualsLower(key, 4, (byte)'i') &&
                       EqualsLower(key, 5, (byte)'e') &&
                       EqualsLower(key, 6, (byte)'s') &&
                       EqualsLower(key, 7, (byte)'h') &&
                       EqualsLower(key, 8, (byte)'a') &&
                       EqualsLower(key, 9, (byte)'s') &&
                       EqualsLower(key, 10, (byte)'h');

            if (key.Length == 12)
                return EqualsLower(key, 0, (byte)'s') &&
                       EqualsLower(key, 1, (byte)'p') &&
                       EqualsLower(key, 2, (byte)'e') &&
                       EqualsLower(key, 3, (byte)'c') &&
                       EqualsLower(key, 4, (byte)'i') &&
                       EqualsLower(key, 5, (byte)'e') &&
                       EqualsLower(key, 6, (byte)'s') &&
                       key[7] == (byte)'_' &&
                       EqualsLower(key, 8, (byte)'h') &&
                       EqualsLower(key, 9, (byte)'a') &&
                       EqualsLower(key, 10, (byte)'s') &&
                       EqualsLower(key, 11, (byte)'h');

            return false;
        }

        private static bool EqualsLower(ReadOnlySpan<byte> bytes, int index, byte expectedLower)
        {
            byte value = bytes[index];
            if (value >= (byte)'A' && value <= (byte)'Z')
                value = (byte)(value + 32);

            return value == expectedLower;
        }

        private static bool TryReadFloatColumn(ReadOnlySpan<byte> line, ref int cursor, out float value)
        {
            value = 0f;
            return TryReadColumn(line, ref cursor, out ReadOnlySpan<byte> column) &&
                   TryParseAsciiFloat(column, out value);
        }

        private static bool TryReadColumn(ReadOnlySpan<byte> line, ref int cursor, out ReadOnlySpan<byte> column)
        {
            column = default;
            if (cursor > line.Length)
                return false;

            int start = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;

            int end = cursor;
            if (cursor < line.Length && line[cursor] == (byte)',')
                cursor++;

            while (start < end && IsCsvSpace(line[start]))
                start++;
            while (end > start && IsCsvSpace(line[end - 1]))
                end--;

            column = line.Slice(start, end - start);
            return true;
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

        private static bool IsCsvSpace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }
#endif

        private void DumpBlackBox(NativeArray<MetabolicTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            TryReadMetabolismVaultBuffer(
                _dataVault,
                in _detailTelemetryHandle,
                ShinobuMetabolismConstants.MetabolismDetailTelemetryRingBuffer,
                ShinobuMetabolismConstants.TelemetryFrameCount,
                out NativeArray<MetabolicDetailTelemetryEntry> detailTelemetry);
            uint detailStride = detailTelemetry.IsCreated && detailTelemetry.Length > 0
                ? (uint)UnsafeUtility.SizeOf<MetabolicDetailTelemetryEntry>()
                : 0u;

            NativeArray<byte> payload = default;
            try
            {
                int telemetryByteLength = telemetry.Length * UnsafeUtility.SizeOf<MetabolicTelemetryEntry>();
                int detailByteLength = detailStride > 0u
                    ? detailTelemetry.Length * UnsafeUtility.SizeOf<MetabolicDetailTelemetryEntry>()
                    : 0;
                int byteCount = 32 + telemetryByteLength + detailByteLength;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(ShinobuMetabolismRuntime),
                    "shinobuMetabolismBlackBoxPayload");
                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                Span<byte> header = new Span<byte>(destination, 32);
                WriteUInt64LittleEndian(header.Slice(0, 8), DumpMagic);
                WriteUInt32LittleEndian(header.Slice(8, 4), DumpVersion);
                WriteUInt32LittleEndian(header.Slice(12, 4), (uint)telemetry.Length);
                WriteUInt32LittleEndian(header.Slice(16, 4), (uint)UnsafeUtility.SizeOf<MetabolicTelemetryEntry>());
                WriteUInt32LittleEndian(header.Slice(20, 4), _simulationFrameCounter);
                WriteUInt32LittleEndian(header.Slice(24, 4), (uint)math.max(0, _pendingTelemetryIndex));
                WriteUInt32LittleEndian(header.Slice(28, 4), detailStride);

                byte* telemetryPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                UnsafeUtility.MemCpy(destination + 32, telemetryPtr, telemetryByteLength);
                if (detailByteLength > 0)
                {
                    byte* detailPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(detailTelemetry);
                    UnsafeUtility.MemCpy(destination + 32 + telemetryByteLength, detailPtr, detailByteLength);
                }

                NativeFaultDumpWriter.TryWriteAll(_dumpPath, payload, byteCount);
            }
            catch (Exception)
            {
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(ShinobuMetabolismRuntime),
                    "shinobuMetabolismBlackBoxPayload");
            }
        }

        private static void ReplaceBlackBoxDump(string tempPath, string path)
        {
            if (!File.Exists(path))
            {
                File.Move(tempPath, path);
                return;
            }

            try
            {
                File.Replace(tempPath, path, null, true);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceBlackBoxDumpByBackupMove(tempPath, path);
            }
            catch (IOException)
            {
                ReplaceBlackBoxDumpByBackupMove(tempPath, path);
            }
        }

        private static void ReplaceBlackBoxDumpByBackupMove(string tempPath, string path)
        {
            string backupPath = path + ".bak";
            if (File.Exists(backupPath))
                File.Delete(backupPath);

            File.Move(path, backupPath);
            try
            {
                File.Move(tempPath, path);
                TryDeleteBlackBoxDumpPath(backupPath);
            }
            catch (Exception)
            {
                TryRestoreBlackBoxDumpBackup(backupPath, path);
                throw;
            }
        }

        private static void TryRestoreBlackBoxDumpBackup(string backupPath, string path)
        {
            try
            {
                if (!File.Exists(path) && File.Exists(backupPath))
                    File.Move(backupPath, path);
            }
            catch (Exception)
            {
            }
        }

        private static void TryDeleteBlackBoxDumpPath(string targetPath)
        {
            try
            {
                if (File.Exists(targetPath))
                    File.Delete(targetPath);
            }
            catch (Exception)
            {
            }
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

        private void ReleaseMetabolismVaultHandles(IDataVault vault)
        {
            ReleaseMetabolismVaultHandle(vault, ref _stateHandle, ShinobuMetabolismConstants.MetabolismStatesBuffer);
            ReleaseMetabolismVaultHandle(vault, ref _entityAupHandle, ShinobuMetabolismConstants.MetabolismEntityAupsBuffer);
            ReleaseMetabolismVaultHandle(vault, ref _exertionHandle, ShinobuMetabolismConstants.MetabolismExertionBuffer);
            ReleaseMetabolismVaultHandle(vault, ref _speciesRuleHandle, ShinobuMetabolismConstants.MetabolismSpeciesRulesBuffer);
            ReleaseMetabolismVaultHandle(vault, ref _ruleIndexHandle, ShinobuMetabolismConstants.MetabolismRuleIndicesBuffer);
            ReleaseMetabolismVaultHandle(vault, ref _telemetryHandle, ShinobuMetabolismConstants.MetabolismTelemetryRingBuffer);
            ReleaseMetabolismVaultHandle(vault, ref _tuningHandle, ShinobuMetabolismConstants.MetabolismTuningBuffer);
            ReleaseMetabolismVaultHandle(vault, ref _toxinSampleHandle, ShinobuMetabolismConstants.MetabolismToxinSamplesBuffer);
            ReleaseMetabolismVaultHandle(vault, ref _physiologySignalHandle, ShinobuMetabolismConstants.MetabolismPhysiologySignalsBuffer);
            ReleaseMetabolismVaultHandle(vault, ref _exposureSignalHandle, ShinobuMetabolismConstants.MetabolismExposureSignalsBuffer);
            ReleaseMetabolismVaultHandle(vault, ref _detailTelemetryHandle, ShinobuMetabolismConstants.MetabolismDetailTelemetryRingBuffer);
            ReleaseMetabolismVaultHandle(vault, ref _suitProfileHandle, ShinobuMetabolismConstants.MetabolismSuitThermalProfilesBuffer);
            ReleaseMetabolismVaultHandle(vault, ref _suitProfileIndexHandle, ShinobuMetabolismConstants.MetabolismSuitProfileIndicesBuffer);
        }

        private static void ReleaseMetabolismVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            if (vault != null && IsMetabolismVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void ClearCachedHandles()
        {
            _stateHandle = default;
            _entityAupHandle = default;
            _exertionHandle = default;
            _speciesRuleHandle = default;
            _ruleIndexHandle = default;
            _telemetryHandle = default;
            _tuningHandle = default;
            _toxinSampleHandle = default;
            _physiologySignalHandle = default;
            _exposureSignalHandle = default;
            _detailTelemetryHandle = default;
            _suitProfileHandle = default;
            _suitProfileIndexHandle = default;
            _suitIntegrityStateReadHandle = default;
            _thermalGridReadbackService = null;
            _simulationAccumulator = 0f;
            _lastDispatcherTimeSeconds = -1d;
            _latestTelemetryValid = false;
            _latestDetailTelemetryValid = false;
            _jobScheduled = false;
            _jobLocksHeld = false;
            _thermalGridReadbackHeld = false;
        }

    }
}
