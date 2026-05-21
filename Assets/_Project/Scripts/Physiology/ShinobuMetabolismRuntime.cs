using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts.Physiology;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Physiology
{
    /// <summary>
    /// Vault-backed survival metabolism runtime. SlowTick schedules pure Burst kernels; LateFrameTick reclaims the fence.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed unsafe class ShinobuMetabolismRuntime : MonoBehaviour, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener, IDisposable
    {
        private const SystemID OwnerSystem = SystemID.GameplayPlayer;
        private const int LockBufferCount = 9;
        private const uint MockSectorHash = 0x4D455441u; // META
        private const ulong DumpMagic = 0x4D45544153524745ul; // METASRGE
        private const uint DumpVersion = 1u;
        private const string CsvRelativePath = "biological_metabolism_profiles.csv";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_METABOLISM_SURGEON.bin";

        private static readonly ProfilerMarker _ScheduleMarker = new ProfilerMarker("ShinobuMetabolism.Schedule");
        private static readonly ProfilerMarker _CompleteMarker = new ProfilerMarker("ShinobuMetabolism.Complete");
        private static readonly int _GlobalsBufferId = Shader.PropertyToID("_HectonMetabolismFrostGlobals");
        private static readonly int _FrostScalarId = Shader.PropertyToID("_HectonMetabolismFrostScalar");

        [Header("Runtime Capacity")]
        [Tooltip("Maximum living-entity metabolism rows owned by this runtime.")]
        [SerializeField, Min(1)] private int entityCapacity = ShinobuMetabolismConstants.DefaultEntityCapacity;

        [Header("Cold Bootstrap")]
        [Tooltip("Generate deterministic fallback entities when no creature owner has hydrated metabolism rows yet.")]
        [SerializeField] private bool generateMockEcosystemOnEnable = true;

        [Tooltip("Load biological_metabolism_profiles.csv from the project root during cold bootstrap.")]
        [SerializeField] private bool loadCsvProfilesOnEnable = true;

        [Header("Editor Debug")]
        [Tooltip("Editor-only labels for the first vault rows. Runtime builds ignore this path.")]
        [SerializeField] private bool drawDebugGizmos = true;

        [Tooltip("Maximum editor-only metabolism labels drawn by OnDrawGizmos.")]
        [SerializeField, Range(0, 64)] private int debugGizmoRows = 8;

        private VaultBufferHandle<MetabolicStateDTO> _stateHandle;
        private VaultBufferHandle<double3> _entityAupHandle;
        private VaultBufferHandle<float> _exertionHandle;
        private VaultBufferHandle<MetabolicSpeciesRuleDTO> _speciesRuleHandle;
        private VaultBufferHandle<ushort> _ruleIndexHandle;
        private VaultBufferHandle<MetabolicTelemetryEntry> _telemetryHandle;
        private VaultBufferHandle<MetabolismTuningDTO> _tuningHandle;
        private VaultBufferHandle<float> _toxinSampleHandle;
        private VaultBufferHandle<byte> _csvScratchHandle;
        private VaultBufferHandle<PhysiologyStateSignal> _physiologySignalHandle;
        private VaultBufferHandle<CombatDamageSignal> _combatSignalHandle;

        private IDataVault _dataVault;
        private IThermodynamicsService _thermodynamicsService;
        private ITickDispatcher _tickDispatcher;
        private JobHandle _activeJobHandle;
        private GraphicsBuffer _shaderGlobalsBufferA;
        private GraphicsBuffer _shaderGlobalsBufferB;
        private GraphicsBuffer _activeShaderGlobalsBuffer;
        private MetabolismShaderGlobalsDTO _lastShaderGlobals;
        private MetabolicTelemetryEntry _latestTelemetry;
        private string _csvPath;
        private string _dumpPath;
        private double _lastDispatcherTimeSeconds = -1d;
        private float _simulationAccumulator;
        private long _jobScheduleTimestamp;
        private int _telemetryCursor;
        private int _pendingTelemetryIndex = -1;
        private int _scheduledCount;
        private int _chemicalReadbackLockedCount;
        private uint _simulationFrameCounter;
        private bool _registeredSlowTick;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _jobScheduled;
        private bool _jobLocksHeld;
        private bool _chemicalReadbackLocksHeld;
        private bool _defaultsInitialized;
        private bool _latestTelemetryValid;
        private bool _autopsyDumped;
        private bool _shaderGlobalsInitialized;
        private int _shaderWriteIndex;

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

            _ = GlobalSignals.DamageSignalWriter;
            SignalBus<PhysiologyStateSignal>.EnsureInitialized();
            SignalBus<CombatDamageSignal>.EnsureInitialized();
            SignalBus<KccVelocitySignal>.EnsureInitialized();
            TryRegisterHotSwapListener();
            RebindColdServices();
            EnsureShaderGlobalsBuffers();

            if (EnsureVaultState())
            {
                InitializeDefaultVaultContents();
                if (loadCsvProfilesOnEnable)
                    TryLoadBiologicalProfilesCsv();
                TryRegisterTicks();
            }
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
            CompleteFrameJobForTeardown();
            TryUnregisterTicks();
            TryUnregisterHotSwapListener();
            UnlockChemicalReadbackBuffers();
            UnlockJobBuffers();
            ReleaseShaderGlobalsBuffers();
            ClearCachedHandles();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            CompleteFrameJobForTeardown();
            UnlockChemicalReadbackBuffers();
            UnlockJobBuffers();
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
                CompleteFrameJobForTeardown();
                UnlockChemicalReadbackBuffers();
                UnlockJobBuffers();
                _dataVault = currentService as IDataVault;
                ClearCachedHandles();
                _defaultsInitialized = false;
                _autopsyDumped = false;
                if (_dataVault != null && EnsureVaultState())
                {
                    InitializeDefaultVaultContents();
                    TryRegisterTicks();
                }

                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.ThermodynamicsService)
                _thermodynamicsService = currentService as IThermodynamicsService;
            else if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
                _tickDispatcher = currentService as ITickDispatcher;
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            if (_jobScheduled)
                return;

            IDataVault vault = _dataVault;
            if (vault == null || !EnsureVaultState())
                return;

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
                        out NativeArray<CombatDamageSignal> combatSignals))
                {
                    return;
                }

                int count = ResolveEntityCount(states, entityAups, exertion, ruleIndices, toxinSamples);
                if (count <= 0 ||
                    rules.Length <= 0 ||
                    telemetry.Length <= 0 ||
                    tuningArray.Length <= 0 ||
                    physiologySignals.Length < ResolvePhysiologySignalCapacity(count) ||
                    combatSignals.Length < count)
                {
                    return;
                }

                TryApplyLatestKccSignal(states, entityAups, exertion);

                MetabolismTuningDTO tuning = ShinobuMetabolismJobMath.SanitizeTuning(tuningArray[0]);
                tuning.GlobalQualityWeight = quality;
                tuningArray[0] = tuning;

                TryResolveThermalGrid(
                    out NativeArray<float> thermalGrid,
                    out int3 thermalResolution,
                    out double3 thermalRootAup,
                    out float thermalCellSizeMeters,
                    out byte hasThermalGrid);

                if (!TryLockJobBuffers(vault))
                    return;

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
                uint frame = ++_simulationFrameCounter;
                int telemetryIndex = _telemetryCursor % telemetry.Length;
                if (telemetryIndex < 0)
                    telemetryIndex += telemetry.Length;

                float* thermalPtr = hasThermalGrid != 0 && thermalGrid.IsCreated
                    ? (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(thermalGrid)
                    : null;

                MetabolicIntegrationJob integrationJob = default;
                integrationJob.States = (MetabolicStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states);
                integrationJob.EntityAups = (double3*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(entityAups);
                integrationJob.ExertionSpeedSq = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(exertion);
                integrationJob.ToxinSamples = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(toxinSamples);
                integrationJob.RuleIndices = (ushort*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(ruleIndices);
                integrationJob.Rules = (MetabolicSpeciesRuleDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rules);
                integrationJob.ThermalCelsiusGrid = thermalPtr;
                integrationJob.ChemicalPublishedGrid = chemicalPublishedPtr;
                integrationJob.ChemicalOverlayGrid = chemicalOverlayPtr;
                integrationJob.PhysiologySignals = (PhysiologyStateSignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(physiologySignals);
                integrationJob.CombatSignals = (CombatDamageSignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(combatSignals);
                integrationJob.Tuning = tuning;
                integrationJob.ThermalGridRootAup = thermalRootAup;
                integrationJob.ChemicalGridRootAup = chemicalRootAup;
                integrationJob.ThermalGridResolution = thermalResolution;
                integrationJob.ChemicalGridResolution = chemicalResolution;
                integrationJob.ThermalGridLength = hasThermalGrid != 0 && thermalGrid.IsCreated ? thermalGrid.Length : 0;
                integrationJob.ChemicalGridLength = chemicalGridLength;
                integrationJob.PhysiologySignalLength = physiologySignals.Length;
                integrationJob.CombatSignalLength = combatSignals.Length;
                integrationJob.ThermalCellSizeMeters = thermalCellSizeMeters;
                integrationJob.ChemicalCellSizeMeters = chemicalCellSizeMeters;
                integrationJob.DeltaSeconds = dt;
                integrationJob.GlobalQualityWeight = 1f;
                integrationJob.Frame = frame;
                integrationJob.Count = count;
                integrationJob.RuleCount = rules.Length;
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
                _simulationAccumulator = 0f;
                _jobScheduled = true;
                H8Memory.RegisterActiveJob(OwnerSystem, _activeJobHandle);
            }
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            TryFinalizeFrameJobNoWait();
        }

        /// <summary>
        /// Generates deterministic 5000-row fallback metabolism data in Vault memory.
        /// </summary>
        public bool GenerateMockEcosystemMetabolism()
        {
            CompleteFrameJobForTeardown();
            if (!EnsureVaultState() || !TryResolveBuffers(
                    _dataVault,
                    out NativeArray<MetabolicStateDTO> states,
                    out NativeArray<double3> entityAups,
                    out NativeArray<float> exertion,
                    out NativeArray<MetabolicSpeciesRuleDTO> rules,
                    out NativeArray<ushort> ruleIndices,
                    out _,
                    out NativeArray<MetabolismTuningDTO> tuningArray,
                    out NativeArray<float> toxinSamples,
                    out NativeArray<PhysiologyStateSignal> physiologySignals,
                    out NativeArray<CombatDamageSignal> combatSignals))
            {
                return false;
            }

            int count = ResolveEntityCount(states, entityAups, exertion, ruleIndices, toxinSamples);
            if (count <= 0 || rules.Length <= 0 || tuningArray.Length <= 0)
                return false;

            InitMetabolismRulesJob initRulesJob = default;
            initRulesJob.Rules = (MetabolicSpeciesRuleDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rules);
            initRulesJob.Tuning = (MetabolismTuningDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(tuningArray);
            initRulesJob.RuleCount = rules.Length;
            JobHandle initRules = initRulesJob.Schedule();

            InitInactiveMetabolismJob initInactiveJob = default;
            initInactiveJob.States = (MetabolicStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states);
            initInactiveJob.EntityAups = (double3*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(entityAups);
            initInactiveJob.ExertionSpeedSq = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(exertion);
            initInactiveJob.ToxinSamples = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(toxinSamples);
            initInactiveJob.RuleIndices = (ushort*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(ruleIndices);
            initInactiveJob.PhysiologySignals = (PhysiologyStateSignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(physiologySignals);
            initInactiveJob.CombatSignals = (CombatDamageSignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(combatSignals);
            initInactiveJob.Count = count;
            initInactiveJob.PhysiologySignalLength = physiologySignals.Length;
            initInactiveJob.CombatSignalLength = combatSignals.Length;
            JobHandle initInactive = initInactiveJob.Schedule(count, ShinobuMetabolismConstants.FrameJobBatchSize, initRules);

            int mockCount = math.min(count, ShinobuMetabolismConstants.DefaultEntityCapacity);
            InitMockMetabolismJob initMockJob = default;
            initMockJob.States = (MetabolicStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states);
            initMockJob.EntityAups = (double3*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(entityAups);
            initMockJob.ExertionSpeedSq = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(exertion);
            initMockJob.ToxinSamples = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(toxinSamples);
            initMockJob.RuleIndices = (ushort*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(ruleIndices);
            initMockJob.Count = mockCount;
            initMockJob.Seed = MockSectorHash;
            initMockJob.Frame = _simulationFrameCounter;
            JobHandle initMock = initMockJob.Schedule(mockCount, ShinobuMetabolismConstants.FrameJobBatchSize, initInactive);

            // COLD SYNC JOB: editor/bootstrap fallback hydration, not part of gameplay SlowTick.
            DispatcherJobFence.TryComplete(ref initMock, forceComplete: true);
            _defaultsInitialized = true;
            return true;
        }

        /// <summary>
        /// Reloads designer-authored biological metabolism profile rows from the project-root CSV.
        /// </summary>
        public bool TryLoadBiologicalProfilesCsv()
        {
            IDataVault vault = _dataVault;
            if (vault == null || !EnsureVaultState())
                return false;

            NativeArray<byte> scratch = _csvScratchHandle.Resolve(vault);
            NativeArray<MetabolicSpeciesRuleDTO> rules = _speciesRuleHandle.Resolve(vault);
            if (!scratch.IsCreated || !rules.IsCreated || scratch.Length <= 0 || rules.Length <= 0)
                return false;

            try
            {
                if (!File.Exists(_csvPath))
                    return false;

                int maxBytes = math.min(scratch.Length, ShinobuMetabolismConstants.CsvMaxBytes);
                if (maxBytes <= 0)
                    return false;

                byte* scratchPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(scratch);
                Span<byte> buffer = new Span<byte>(scratchPtr, maxBytes);
                using (FileStream stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long boundedBytes = stream.Length < maxBytes ? stream.Length : maxBytes;
                    int byteCount = boundedBytes > int.MaxValue ? int.MaxValue : (int)boundedBytes;
                    if (byteCount <= 0)
                        return false;

                    int read = stream.Read(buffer.Slice(0, byteCount));
                    if (read <= 0)
                        return false;

                    ParseBiologicalProfilesCsv(buffer.Slice(0, read), rules);
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
        }

        /// <summary>
        /// Returns a mutable reference to a metabolism state row without CS1612 copies.
        /// </summary>
        public ref MetabolicStateDTO GetStateRef(int entityIndex)
        {
            IDataVault vault = _dataVault;
            if (vault == null || (uint)entityIndex >= (uint)_stateHandle.Length)
                FatalMemoryException.ThrowStaleVaultHandle();

            return ref _stateHandle.GetElementAsRef(vault, entityIndex);
        }

        /// <summary>
        /// Reads one metabolism state row for editor or diagnostics.
        /// </summary>
        public bool TryGetState(int entityIndex, out MetabolicStateDTO state)
        {
            state = default;
            IDataVault vault = _dataVault;
            NativeArray<MetabolicStateDTO> states = vault != null ? _stateHandle.Resolve(vault) : default;
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
            IDataVault vault = _dataVault;
            NativeArray<double3> entityAups = vault != null ? _entityAupHandle.Resolve(vault) : default;
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
            NativeArray<MetabolismTuningDTO> tuningArray = vault != null ? _tuningHandle.Resolve(vault) : default;
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
            IDataVault vault = _dataVault;
            NativeArray<MetabolismTuningDTO> tuningArray = vault != null ? _tuningHandle.Resolve(vault) : default;
            if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                return false;

            tuningArray[0] = ShinobuMetabolismJobMath.SanitizeTuning(tuning);
            return true;
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
        /// Forces the 300-frame metabolism telemetry ring to disk.
        /// </summary>
        public bool TryDumpBlackBoxForEditor()
        {
            IDataVault vault = _dataVault;
            NativeArray<MetabolicTelemetryEntry> telemetry = vault != null ? _telemetryHandle.Resolve(vault) : default;
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
                if (!DispatcherJobFence.TryComplete(ref _activeJobHandle, forceComplete: true))
                    return;

                FinishFrameJobCompletion();
            }
        }

        private void FinishFrameJobCompletion()
        {
            float executionMicroseconds = ResolveJobExecutionMicroseconds();
            IDataVault vault = _dataVault;
            NativeArray<MetabolicTelemetryEntry> telemetry = vault != null ? _telemetryHandle.Resolve(vault) : default;
            if (telemetry.IsCreated && (uint)_pendingTelemetryIndex < (uint)telemetry.Length)
            {
                MetabolicTelemetryEntry entry = telemetry[_pendingTelemetryIndex];
                entry.ExecutionMicroseconds = executionMicroseconds;
                telemetry[_pendingTelemetryIndex] = entry;
                _latestTelemetry = entry;
                _latestTelemetryValid = true;
                _telemetryCursor = (_pendingTelemetryIndex + 1) % telemetry.Length;
                PublishShaderGlobals(in entry);
                if (!_autopsyDumped && (entry.Flags & ShinobuMetabolismFlags.NanDetected) != 0u)
                {
                    DumpBlackBox(telemetry);
                    _autopsyDumped = true;
                }
            }

            PublishStagedSignals(vault, _scheduledCount);
            _jobScheduled = false;
            _pendingTelemetryIndex = -1;
            _scheduledCount = 0;
            UnlockChemicalReadbackBuffers();
            UnlockJobBuffers();
        }

        private void PublishStagedSignals(IDataVault vault, int scheduledCount)
        {
            if (vault == null || scheduledCount <= 0)
                return;

            NativeArray<PhysiologyStateSignal> physiologySignals = _physiologySignalHandle.Resolve(vault);
            NativeArray<CombatDamageSignal> combatSignals = _combatSignalHandle.Resolve(vault);
            int physiologyLimit = physiologySignals.IsCreated
                ? math.min(physiologySignals.Length, ResolvePhysiologySignalCapacity(scheduledCount))
                : 0;
            int combatLimit = combatSignals.IsCreated ? math.min(combatSignals.Length, scheduledCount) : 0;

            for (int i = 0; i < physiologyLimit; i++)
            {
                PhysiologyStateSignal signal = physiologySignals[i];
                if (signal.SourceHash == 0u || signal.Frame == 0u)
                    continue;

                SignalBus<PhysiologyStateSignal>.TryPush(in signal);
            }

            for (int i = 0; i < combatLimit; i++)
            {
                CombatDamageSignal signal = combatSignals[i];
                if (signal.TargetHash == 0u || signal.Frame == 0u || signal.Magnitude <= 0f)
                    continue;

                SignalBus<CombatDamageSignal>.TryPush(in signal);
            }
        }

        private bool EnsureVaultState()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            int physiologySignalCapacity = ResolvePhysiologySignalCapacity(entityCapacity);
            _stateHandle = vault.GetBufferHandle<MetabolicStateDTO>(
                ShinobuMetabolismConstants.MetabolismStatesBuffer,
                entityCapacity,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _entityAupHandle = vault.GetBufferHandle<double3>(
                ShinobuMetabolismConstants.MetabolismEntityAupsBuffer,
                entityCapacity,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _exertionHandle = vault.GetBufferHandle<float>(
                ShinobuMetabolismConstants.MetabolismExertionBuffer,
                entityCapacity,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _speciesRuleHandle = vault.GetBufferHandle<MetabolicSpeciesRuleDTO>(
                ShinobuMetabolismConstants.MetabolismSpeciesRulesBuffer,
                ShinobuMetabolismConstants.MaxSpeciesRules,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _ruleIndexHandle = vault.GetBufferHandle<ushort>(
                ShinobuMetabolismConstants.MetabolismRuleIndicesBuffer,
                entityCapacity,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = vault.GetBufferHandle<MetabolicTelemetryEntry>(
                ShinobuMetabolismConstants.MetabolismTelemetryRingBuffer,
                ShinobuMetabolismConstants.TelemetryFrameCount,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _tuningHandle = vault.GetBufferHandle<MetabolismTuningDTO>(
                ShinobuMetabolismConstants.MetabolismTuningBuffer,
                1,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _toxinSampleHandle = vault.GetBufferHandle<float>(
                ShinobuMetabolismConstants.MetabolismToxinSamplesBuffer,
                entityCapacity,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _csvScratchHandle = vault.GetBufferHandle<byte>(
                ShinobuMetabolismConstants.MetabolismCsvScratchBuffer,
                ShinobuMetabolismConstants.CsvMaxBytes,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _physiologySignalHandle = vault.GetBufferHandle<PhysiologyStateSignal>(
                ShinobuMetabolismConstants.MetabolismPhysiologySignalsBuffer,
                physiologySignalCapacity,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            _combatSignalHandle = vault.GetBufferHandle<CombatDamageSignal>(
                ShinobuMetabolismConstants.MetabolismCombatSignalsBuffer,
                entityCapacity,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);

            return _stateHandle.IsCreated &&
                   _entityAupHandle.IsCreated &&
                   _exertionHandle.IsCreated &&
                   _speciesRuleHandle.IsCreated &&
                   _ruleIndexHandle.IsCreated &&
                   _telemetryHandle.IsCreated &&
                   _tuningHandle.IsCreated &&
                   _toxinSampleHandle.IsCreated &&
                   _csvScratchHandle.IsCreated &&
                   _physiologySignalHandle.IsCreated &&
                   _combatSignalHandle.IsCreated;
        }

        private static int ResolvePhysiologySignalCapacity(int entityCount)
        {
            int count = math.max(1, entityCount);
            int maxSafe = int.MaxValue / ShinobuMetabolismConstants.PhysiologySignalsPerEntity;
            count = math.min(count, maxSafe);
            return count * ShinobuMetabolismConstants.PhysiologySignalsPerEntity;
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
                    out NativeArray<CombatDamageSignal> combatSignals))
            {
                return;
            }

            int count = ResolveEntityCount(states, entityAups, exertion, ruleIndices, toxinSamples);
            if (count <= 0 || rules.Length <= 0 || tuningArray.Length <= 0)
                return;

            InitMetabolismRulesJob initRulesJob = default;
            initRulesJob.Rules = (MetabolicSpeciesRuleDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rules);
            initRulesJob.Tuning = (MetabolismTuningDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(tuningArray);
            initRulesJob.RuleCount = rules.Length;
            JobHandle initRules = initRulesJob.Schedule();

            InitInactiveMetabolismJob initInactiveJob = default;
            initInactiveJob.States = (MetabolicStateDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states);
            initInactiveJob.EntityAups = (double3*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(entityAups);
            initInactiveJob.ExertionSpeedSq = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(exertion);
            initInactiveJob.ToxinSamples = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(toxinSamples);
            initInactiveJob.RuleIndices = (ushort*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(ruleIndices);
            initInactiveJob.PhysiologySignals = (PhysiologyStateSignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(physiologySignals);
            initInactiveJob.CombatSignals = (CombatDamageSignal*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(combatSignals);
            initInactiveJob.Count = count;
            initInactiveJob.PhysiologySignalLength = physiologySignals.Length;
            initInactiveJob.CombatSignalLength = combatSignals.Length;
            JobHandle initInactive = initInactiveJob.Schedule(count, ShinobuMetabolismConstants.FrameJobBatchSize, initRules);

            // COLD SYNC JOB: default metabolism rule and inactive-row hydration, not part of gameplay SlowTick.
            DispatcherJobFence.TryComplete(ref initInactive, forceComplete: true);
            _defaultsInitialized = true;
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
            out NativeArray<CombatDamageSignal> combatSignals)
        {
            states = vault != null ? _stateHandle.Resolve(vault) : default;
            entityAups = vault != null ? _entityAupHandle.Resolve(vault) : default;
            exertion = vault != null ? _exertionHandle.Resolve(vault) : default;
            rules = vault != null ? _speciesRuleHandle.Resolve(vault) : default;
            ruleIndices = vault != null ? _ruleIndexHandle.Resolve(vault) : default;
            telemetry = vault != null ? _telemetryHandle.Resolve(vault) : default;
            tuningArray = vault != null ? _tuningHandle.Resolve(vault) : default;
            toxinSamples = vault != null ? _toxinSampleHandle.Resolve(vault) : default;
            physiologySignals = vault != null ? _physiologySignalHandle.Resolve(vault) : default;
            combatSignals = vault != null ? _combatSignalHandle.Resolve(vault) : default;
            return states.IsCreated &&
                   entityAups.IsCreated &&
                   exertion.IsCreated &&
                   rules.IsCreated &&
                   ruleIndices.IsCreated &&
                   telemetry.IsCreated &&
                   tuningArray.IsCreated &&
                   toxinSamples.IsCreated &&
                   physiologySignals.IsCreated &&
                   combatSignals.IsCreated;
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
            out NativeArray<float> thermalGrid,
            out int3 thermalResolution,
            out double3 thermalRootAup,
            out float thermalCellSizeMeters,
            out byte hasThermalGrid)
        {
            thermalGrid = default;
            thermalResolution = default;
            thermalRootAup = double3.zero;
            thermalCellSizeMeters = 1f;
            hasThermalGrid = 0;

            IThermodynamicsService service = _thermodynamicsService;
            if (service == null || !service.IsInitialized)
                return;

            if (!service.TryGetThermalGridReadback(
                    out thermalGrid,
                    out int width,
                    out int height,
                    out int depth,
                    out Vector3 originWS,
                    out float cellSizeMeters,
                    out _))
            {
                return;
            }

            if (!thermalGrid.IsCreated || thermalGrid.Length <= 0 || width <= 0 || height <= 0 || depth <= 0)
            {
                thermalGrid = default;
                return;
            }

            thermalResolution.x = width;
            thermalResolution.y = height;
            thermalResolution.z = depth;
            thermalCellSizeMeters = math.max(0.001f, cellSizeMeters);
            hasThermalGrid = TryResolveAupDoubleFromRuntimeOrigin(originWS, out thermalRootAup) ? (byte)1 : (byte)0;
        }

        private static bool TryResolveAupDoubleFromRuntimeOrigin(Vector3 runtimePosition, out double3 absoluteAup)
        {
            absoluteAup = default;
            if (!float.IsFinite(runtimePosition.x) ||
                !float.IsFinite(runtimePosition.y) ||
                !float.IsFinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            if (!positionAup.IsFinite())
                return false;

            absoluteAup = positionAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absoluteAup));
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

            if (vault == null || !TryLockChemicalReadbackBuffers(vault))
                return;

            if (!vault.TryGetBufferHandle<float4>(ShinobuMetabolismConstants.ChemicalPublishedGridReadbackBuffer, out VaultBufferHandle<float4> publishedHandle) ||
                !vault.TryGetBufferHandle<MetabolismChemicalTelemetryMirrorDTO>(ShinobuMetabolismConstants.ChemicalTelemetryReadbackBuffer, out VaultBufferHandle<MetabolismChemicalTelemetryMirrorDTO> telemetryHandle) ||
                !vault.TryGetBufferHandle<int>(ShinobuMetabolismConstants.ChemicalTelemetryCursorReadbackBuffer, out VaultBufferHandle<int> cursorHandle) ||
                !vault.TryGetBufferHandle<MetabolismChemicalTuningMirrorDTO>(ShinobuMetabolismConstants.ChemicalTuningReadbackBuffer, out VaultBufferHandle<MetabolismChemicalTuningMirrorDTO> tuningHandle))
            {
                UnlockChemicalReadbackBuffers();
                return;
            }

            if (publishedHandle.ptr == null ||
                telemetryHandle.ptr == null ||
                cursorHandle.ptr == null ||
                tuningHandle.ptr == null ||
                publishedHandle.Length < ShinobuMetabolismConstants.ChemicalGridCellCount ||
                telemetryHandle.Length <= 0 ||
                cursorHandle.Length <= 0 ||
                tuningHandle.Length <= 0)
            {
                UnlockChemicalReadbackBuffers();
                return;
            }

            int cursor = ((int*)cursorHandle.ptr)[0];
            int telemetryIndex = cursor - 1;
            if (telemetryIndex < 0)
                telemetryIndex += telemetryHandle.Length;
            if ((uint)telemetryIndex >= (uint)telemetryHandle.Length)
                telemetryIndex = 0;

            MetabolismChemicalTelemetryMirrorDTO telemetry = ((MetabolismChemicalTelemetryMirrorDTO*)telemetryHandle.ptr)[telemetryIndex];
            if (telemetry.Frame == 0u || !math.all(math.isfinite(telemetry.GridOriginAup)))
            {
                UnlockChemicalReadbackBuffers();
                return;
            }

            MetabolismChemicalTuningMirrorDTO tuning = ((MetabolismChemicalTuningMirrorDTO*)tuningHandle.ptr)[0];
            float cellSize = math.isfinite(tuning.CellSizeMeters) && tuning.CellSizeMeters > 0.001f
                ? tuning.CellSizeMeters
                : ShinobuMetabolismConstants.ChemicalDefaultCellSizeMeters;

            float4* overlayPtr = null;
            if (_chemicalReadbackLockedCount >= 5 &&
                vault.TryGetBufferHandle<float4>(ShinobuMetabolismConstants.ChemicalOverlayGridReadbackBuffer, out VaultBufferHandle<float4> overlayHandle) &&
                overlayHandle.ptr != null &&
                overlayHandle.Length >= ShinobuMetabolismConstants.ChemicalGridCellCount)
            {
                overlayPtr = (float4*)overlayHandle.ptr;
            }

            publishedGrid = (float4*)publishedHandle.ptr;
            overlayGrid = overlayPtr;
            chemicalResolution.x = ShinobuMetabolismConstants.ChemicalGridAxisX;
            chemicalResolution.y = ShinobuMetabolismConstants.ChemicalGridAxisY;
            chemicalResolution.z = ShinobuMetabolismConstants.ChemicalGridAxisZ;
            chemicalGridLength = ShinobuMetabolismConstants.ChemicalGridCellCount;
            chemicalRootAup = telemetry.GridOriginAup;
            chemicalCellSizeMeters = cellSize;
            hasChemicalGrid = 1;
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
            if (!_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);

            if (!_registeredSlowTick || !_registeredLateFrame)
                TryUnregisterTicks();
        }

        private void TryUnregisterTicks()
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

            int locked = 0;
            if (!vault.TryLockBuffer(ShinobuMetabolismConstants.MetabolismStatesBuffer, OwnerSystem)) return false;
            locked++;
            if (!vault.TryLockBuffer(ShinobuMetabolismConstants.MetabolismEntityAupsBuffer, OwnerSystem)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(ShinobuMetabolismConstants.MetabolismExertionBuffer, OwnerSystem)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(ShinobuMetabolismConstants.MetabolismSpeciesRulesBuffer, OwnerSystem)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(ShinobuMetabolismConstants.MetabolismRuleIndicesBuffer, OwnerSystem)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(ShinobuMetabolismConstants.MetabolismTelemetryRingBuffer, OwnerSystem)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(ShinobuMetabolismConstants.MetabolismToxinSamplesBuffer, OwnerSystem)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(ShinobuMetabolismConstants.MetabolismPhysiologySignalsBuffer, OwnerSystem)) { UnlockLockedJobBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(ShinobuMetabolismConstants.MetabolismCombatSignalsBuffer, OwnerSystem)) { UnlockLockedJobBuffers(vault, locked); return false; }

            _jobLocksHeld = true;
            return true;
        }

        private bool TryLockChemicalReadbackBuffers(IDataVault vault)
        {
            if (vault == null || _chemicalReadbackLocksHeld)
                return false;

            int locked = 0;
            if (!vault.TryLockBuffer(ShinobuMetabolismConstants.ChemicalPublishedGridReadbackBuffer, OwnerSystem)) return false;
            locked++;
            if (!vault.TryLockBuffer(ShinobuMetabolismConstants.ChemicalTelemetryReadbackBuffer, OwnerSystem)) { UnlockLockedChemicalReadbackBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(ShinobuMetabolismConstants.ChemicalTelemetryCursorReadbackBuffer, OwnerSystem)) { UnlockLockedChemicalReadbackBuffers(vault, locked); return false; }
            locked++;
            if (!vault.TryLockBuffer(ShinobuMetabolismConstants.ChemicalTuningReadbackBuffer, OwnerSystem)) { UnlockLockedChemicalReadbackBuffers(vault, locked); return false; }
            locked++;
            if (vault.TryLockBuffer(ShinobuMetabolismConstants.ChemicalOverlayGridReadbackBuffer, OwnerSystem))
                locked++;

            _chemicalReadbackLockedCount = locked;
            _chemicalReadbackLocksHeld = true;
            return true;
        }

        private void UnlockChemicalReadbackBuffers()
        {
            if (!_chemicalReadbackLocksHeld)
                return;

            IDataVault vault = _dataVault;
            if (vault != null)
                UnlockLockedChemicalReadbackBuffers(vault, _chemicalReadbackLockedCount);
            _chemicalReadbackLockedCount = 0;
            _chemicalReadbackLocksHeld = false;
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
            if (lockedCount >= 9) vault.TryUnlockBuffer(ShinobuMetabolismConstants.MetabolismCombatSignalsBuffer, OwnerSystem);
            if (lockedCount >= 8) vault.TryUnlockBuffer(ShinobuMetabolismConstants.MetabolismPhysiologySignalsBuffer, OwnerSystem);
            if (lockedCount >= 7) vault.TryUnlockBuffer(ShinobuMetabolismConstants.MetabolismToxinSamplesBuffer, OwnerSystem);
            if (lockedCount >= 6) vault.TryUnlockBuffer(ShinobuMetabolismConstants.MetabolismTelemetryRingBuffer, OwnerSystem);
            if (lockedCount >= 5) vault.TryUnlockBuffer(ShinobuMetabolismConstants.MetabolismRuleIndicesBuffer, OwnerSystem);
            if (lockedCount >= 4) vault.TryUnlockBuffer(ShinobuMetabolismConstants.MetabolismSpeciesRulesBuffer, OwnerSystem);
            if (lockedCount >= 3) vault.TryUnlockBuffer(ShinobuMetabolismConstants.MetabolismExertionBuffer, OwnerSystem);
            if (lockedCount >= 2) vault.TryUnlockBuffer(ShinobuMetabolismConstants.MetabolismEntityAupsBuffer, OwnerSystem);
            if (lockedCount >= 1) vault.TryUnlockBuffer(ShinobuMetabolismConstants.MetabolismStatesBuffer, OwnerSystem);
        }

        private static void UnlockLockedChemicalReadbackBuffers(IDataVault vault, int lockedCount)
        {
            if (lockedCount >= 5) vault.TryUnlockBuffer(ShinobuMetabolismConstants.ChemicalOverlayGridReadbackBuffer, OwnerSystem);
            if (lockedCount >= 4) vault.TryUnlockBuffer(ShinobuMetabolismConstants.ChemicalTuningReadbackBuffer, OwnerSystem);
            if (lockedCount >= 3) vault.TryUnlockBuffer(ShinobuMetabolismConstants.ChemicalTelemetryCursorReadbackBuffer, OwnerSystem);
            if (lockedCount >= 2) vault.TryUnlockBuffer(ShinobuMetabolismConstants.ChemicalTelemetryReadbackBuffer, OwnerSystem);
            if (lockedCount >= 1) vault.TryUnlockBuffer(ShinobuMetabolismConstants.ChemicalPublishedGridReadbackBuffer, OwnerSystem);
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
            if (!SystemInfo.supportsSetConstantBuffer)
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
            mapped[0] = globals;
            writeBuffer.UnlockBufferAfterWrite<MetabolismShaderGlobalsDTO>(1);
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

        private void ParseBiologicalProfilesCsv(ReadOnlySpan<byte> bytes, NativeArray<MetabolicSpeciesRuleDTO> rules)
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

        private void DumpBlackBox(NativeArray<MetabolicTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            if (!TryEnsureBlackBoxDirectory(_dumpPath))
                return;

            string tempPath = _dumpPath + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
                {
                    Span<byte> header = stackalloc byte[32];
                    WriteUInt64LittleEndian(header.Slice(0, 8), DumpMagic);
                    WriteUInt32LittleEndian(header.Slice(8, 4), DumpVersion);
                    WriteUInt32LittleEndian(header.Slice(12, 4), (uint)telemetry.Length);
                    WriteUInt32LittleEndian(header.Slice(16, 4), (uint)UnsafeUtility.SizeOf<MetabolicTelemetryEntry>());
                    WriteUInt32LittleEndian(header.Slice(20, 4), _simulationFrameCounter);
                    WriteUInt32LittleEndian(header.Slice(24, 4), (uint)math.max(0, _pendingTelemetryIndex));
                    WriteUInt32LittleEndian(header.Slice(28, 4), 0u);
                    stream.Write(header);

                    byte* telemetryPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                    int byteLength = telemetry.Length * UnsafeUtility.SizeOf<MetabolicTelemetryEntry>();
                    stream.Write(new ReadOnlySpan<byte>(telemetryPtr, byteLength));
                }

                ReplaceBlackBoxDump(tempPath, _dumpPath);
            }
            catch (IOException)
            {
                TryDeleteBlackBoxDumpPath(tempPath);
            }
            catch (UnauthorizedAccessException)
            {
                TryDeleteBlackBoxDumpPath(tempPath);
            }
            catch (PlatformNotSupportedException)
            {
                TryDeleteBlackBoxDumpPath(tempPath);
            }
        }

        private static bool TryEnsureBlackBoxDirectory(string path)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                return true;
            }
            catch (Exception)
            {
                return false;
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
            _csvScratchHandle = default;
            _physiologySignalHandle = default;
            _combatSignalHandle = default;
            _simulationAccumulator = 0f;
            _lastDispatcherTimeSeconds = -1d;
            _latestTelemetryValid = false;
            _jobScheduled = false;
            _jobLocksHeld = false;
            _chemicalReadbackLocksHeld = false;
            _chemicalReadbackLockedCount = 0;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawDebugGizmos || debugGizmoRows <= 0 || !Application.isPlaying)
                return;

            IDataVault vault = _dataVault != null ? _dataVault : GlobalRegistry.DataVault;
            NativeArray<MetabolicStateDTO> states = vault != null ? _stateHandle.Resolve(vault) : default;
            NativeArray<double3> aups = vault != null ? _entityAupHandle.Resolve(vault) : default;
            if (!states.IsCreated || !aups.IsCreated)
                return;

            int count = math.min(math.min(states.Length, aups.Length), debugGizmoRows);
            for (int i = 0; i < count; i++)
            {
                MetabolicStateDTO state = states[i];
                double3 aup = aups[i];
                if (!math.all(math.isfinite(aup)))
                    continue;

                Vector3 runtimePosition = HectonFloatingOrigin.ToRuntimePosition(aup);
                Handles.Label(
                    runtimePosition,
                    "Cal " + state.Calories.ToString("0.0") + "\nCore " + state.CoreTemperature.ToString("0.0"));
            }
        }
#endif
    }
}
