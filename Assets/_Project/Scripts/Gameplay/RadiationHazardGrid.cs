using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Data;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
using VoxelSdfReadModel = Hecton8.Core.Contracts.IVoxelSonarSdfReadModel;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Radiation Hazard Grid")]
    public sealed unsafe class RadiationHazardGrid : MonoBehaviour, IOriginShiftListener, ISaveable, IGlobalRegistryHotSwapListener
    {
        public const int GridResolution = 32;
        public const int GridCellCount = GridResolution * GridResolution * GridResolution;
        public const int MaxSourceCount = 64;
        public const int TelemetryCapacity = 300;
        public const int RlePacketSizeBytes = 5;
        public const int MaxRlePayloadBytes = 81920;

        private const string NativeMemoryOwner = nameof(RadiationHazardGrid);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        private const float DoseDecayPerSimulationStep = 0.999f;
        private const float DefaultCellSizeMeters = 4f;
        private const float DefaultSourceRadiusMeters = 18f;
        private const float StaticVfxThreshold = 0.5f;
        private const float IodineDoseReduction = 50f;
        private const uint GeigerSourceId = 0x52414447u;
        private const byte GeigerAcousticChannel = 9;
        private const byte RadiationDoseGridKind = 1;
        private const byte RadiationDoseAtmosphereKind = 2;
        private const int EmergencyMockSourceId = unchecked((int)0x53483237u);
        private const int RadiationEntitySlotCount = 1;
        private const int RadiationProfileCapacity = 16;
        private const int RadiationCsvScratchBytes = 8192;
        private const float RadiationCriticalDegradation01 = 0.5f;
        private const float RadiationDamagePerTickScale = 0.08f;
        private const uint RadiationStateFlagIrradiated = 1u << 0;
        private const uint RadiationStateFlagMutated = 1u << 1;
        private const uint RadiationStateFlagCritical = 1u << 2;
        private const uint RadiationStateFlagShielded = 1u << 3;
        private const uint RadiationStateFlagSdfShielded = 1u << 4;
        private const uint RadiationStateFlagBulkheadShielded = 1u << 5;
        private const uint RadiationStateFlagNonFinite = 1u << 31;
        private const uint RadiationSystemHash = 0x53483237u;
        private const ushort RadiationCombatSourceId = 274;
        private const SystemID OwnerSystemId = SystemID.GameplayRadiation;
        private const string RadiationDumpFileName = "Dump_SHINOBU_274.bin";

        private static readonly uint _iodineItemHash = H8DataHash.ComputeFnv1A32("iodine");
        private static readonly uint _iodineCapsItemHash = H8DataHash.ComputeFnv1A32("Iodine");
        private static readonly int _HazardRadiationLevelId = Shader.PropertyToID("_HazardRadiationLevel");
        private static readonly int _HectonVisualStaticGlitchId = Shader.PropertyToID("_HectonVisualStaticGlitch");
        private static readonly int _HectonVisualStaticGlitchSeedId = Shader.PropertyToID("_HectonVisualStaticGlitchSeed");
        private static readonly int _HectonHandRadiationDoseId = Shader.PropertyToID("_HectonHandRadiationDose");
        private static readonly int _HectonHandRadiationMutationId = Shader.PropertyToID("_HectonHandRadiationMutation01");
        private static readonly int _HectonHandRadiationTintId = Shader.PropertyToID("_HectonHandRadiationTint");
        internal static RadiationHazardGrid ActiveRuntimeInstance { get; private set; }

        [SerializeField, Min(0.5f)] private float cellSizeMeters = DefaultCellSizeMeters;
        [SerializeField, Min(0f)] private float doseScalePerFrostTick = 1f;
        [SerializeField] private TextAsset radiationProfilesCsv;
        [SerializeField] private bool enableEmergencyMockSource;
        [SerializeField, Min(0f)] private float emergencyMockIntensity = 80f;
        [SerializeField] private Vector3 emergencyMockOffsetMeters = new Vector3(8f, 0f, 0f);

        private NativeArray<float> _gridRead;
        private NativeArray<float> _gridWrite;
        private NativeArray<float> _gridSource;
        private NativeArray<RadiationStateDTO> _radiationStates;
        private NativeArray<RadiationSource> _sources;
        private NativeArray<RadiationTelemetryEntry> _telemetryRing;
        private NativeArray<int> _sourceCountLane;
        private NativeArray<uint> _telemetryCursorLane;
        private NativeArray<RadiationProfileDTO> _profiles;
        private NativeArray<byte> _csvScratch;
        private NativeArray<RadiationTuningDTO> _tuningLane;
        private NativeArray<CombatDamageSignal> _damageSignalLane;
        private JobHandle _diffusionJobHandle;
        private JobHandle _radiationSimulationJobHandle;
        private VaultGenerationHandle<float> _gridReadHandle;
        private VaultGenerationHandle<float> _gridWriteHandle;
        private VaultGenerationHandle<float> _gridSourceHandle;
        private VaultGenerationHandle<RadiationStateDTO> _stateHandle;
        private VaultGenerationHandle<RadiationSource> _sourcesHandle;
        private VaultGenerationHandle<int> _sourceCountHandle;
        private VaultGenerationHandle<RadiationTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<uint> _telemetryCursorHandle;
        private VaultGenerationHandle<RadiationProfileDTO> _profilesHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<RadiationTuningDTO> _tuningHandle;
        private VaultGenerationHandle<CombatDamageSignal> _damageSignalHandle;
        private VaultGenerationHandle<BulkheadStateDTO> _bulkheadStatesReadHandle;
        private VaultGenerationHandle<BulkheadPlaneDTO> _bulkheadPlanesReadHandle;
        private AbsoluteUniversePosition _gridOriginAup;
        private IDataVault _dataVault;
        private VoxelSdfReadModel _voxelSdfReadModel;
        private SimulationPhaseSystem _simulationPhase;
        private PostSimulationPhaseSystem _postSimulationPhase;
        private VisualSyncPhaseSystem _visualSyncPhase;
        private int _activeSourceCount;
        private int _telemetryWriteIndex;
        private int _sourceVersion;
        private int _gridVersion;
        private float _lastShieldingFactor01;
        private float _lastCellularDegradation01;
        private float _lastBurstExecutionMicroseconds;
        private float _radiationCadenceAccumulatorSeconds;
        private float _lastCompletedIntegrationDeltaSeconds;
        private long _radiationSimulationStartTicks;
        private RadiationStateDTO _lastRadiationState;
        private AbsoluteUniversePosition _lastSimulationPlayerAup;
        private PlayerRuntimeContext _lastSimulationPlayerContext;
        private SaveData _pendingLoadData;
        private IDataVault _pendingDataVault;
        private bool _vaultInitialized;
        private bool _layoutChecked;
        private bool _layoutValid;
        private bool _profilesCsvLoaded;
        private uint _geigerLcg = 0xA21F3B5Du;
        private uint _lastShiftSequence;
        private uint _currentSimulationFrame;
        private float _accumulatedRadiationDose;
        private float _lastGridIntensity01;
        private float _lastExternalIntensity01;
        private float _pendingExternalDoseRad;
        private float _pendingIodineDoseReductionRad;
        private float _geigerPhase;
        private int _lastItemSignalDrainFrame = -1;
        private int _lastItemSignalDeferFrame = -1;
        private int _lastSourceSignalDrainFrame = -1;
        private int _lastSourceSignalPreserveFrame = -1;
        private int _lastExternalDoseSignalDrainFrame = -1;
        private bool _hasGridOrigin;
        private bool _diffusionJobActive;
        private bool _radiationSimulationJobActive;
        private bool _radiationEvaluatedThisFrame;
        private bool _gridBuffersSwapped;
        private bool _registeredSimulationPhase;
        private bool _registeredPostSimulationPhase;
        private bool _registeredVisualSyncPhase;
        private bool _registeredOriginShift;
        private bool _registeredSave;
        private bool _registeredHotSwapListener;
        private bool _pendingLoadDataValid;
        private bool _pendingDataVaultSwap;
        private ISaveService _saveService;

        public int SavePriority => 54;
        public int LoadPriority => 54;

        public static void RegisterSource(int sourceId, Vector3 runtimePosition, float intensity, float radiusMeters)
        {
            if (!Application.isPlaying || sourceId == 0)
                return;

            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition sourceAup))
                return;

            RegisterSource(sourceId, in sourceAup, intensity, radiusMeters);
        }

        public static void RegisterSource(int sourceId, in AbsoluteUniversePosition sourceAup, float intensity, float radiusMeters)
        {
            if (!Application.isPlaying || sourceId == 0 || !AbsoluteUniversePosition.IsFinite(in sourceAup))
                return;

            RadiationSourceSignal signal = new RadiationSourceSignal
            {
                PositionAup = sourceAup,
                Intensity = math.max(0f, intensity),
                RadiusMeters = math.max(0.5f, radiusMeters),
                SourceId = sourceId,
                Operation = RadiationSourceSignal.OperationUpsert,
                Flags = 0
            };
            SignalBus<RadiationSourceSignal>.Push(in signal);
        }

        public static void UnregisterSource(int sourceId)
        {
            if (!Application.isPlaying || sourceId == 0)
                return;

            RadiationSourceSignal signal = new RadiationSourceSignal
            {
                SourceId = sourceId,
                Operation = RadiationSourceSignal.OperationRemove
            };
            SignalBus<RadiationSourceSignal>.Push(in signal);
        }

        public static void ReportExternalDose(float dose, float intensity01, Vector3 runtimePosition)
        {
            if (!Application.isPlaying || !(dose > 0f) || !math.isfinite(dose))
                return;

            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition positionAup))
                return;

            ReportExternalDose(dose, intensity01, in positionAup);
        }

        public static void ReportExternalDose(float dose, float intensity01, in AbsoluteUniversePosition positionAup)
        {
            if (!Application.isPlaying ||
                !(dose > 0f) ||
                !math.isfinite(dose) ||
                !AbsoluteUniversePosition.IsFinite(in positionAup))
            {
                return;
            }

            RadiationDoseSignal signal = new RadiationDoseSignal
            {
                PositionAup = positionAup,
                Dose = dose,
                Intensity01 = math.saturate(intensity01),
                SourceId = 0u,
                DoseKind = RadiationDoseAtmosphereKind,
                Flags = 0
            };
            SignalBus<RadiationDoseSignal>.Push(in signal);
        }

        internal static bool TrySampleRadiationIntensity01(Vector3 runtimePosition, out float intensity01)
        {
            intensity01 = 0f;
            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition sampleAup))
                return false;

            return TrySampleRadiationIntensity01(in sampleAup, out intensity01);
        }

        internal static bool TrySampleRadiationIntensity01(in AbsoluteUniversePosition sampleAup, out float intensity01)
        {
            intensity01 = 0f;
            RadiationHazardGrid grid = ActiveRuntimeInstance;
            if (grid == null || !AbsoluteUniversePosition.IsFinite(in sampleAup))
                return false;

            float gridIntensity = grid.SampleGridNearest(in sampleAup);
            intensity01 = grid._radiationSimulationJobActive
                ? gridIntensity
                : math.max(grid.SampleInverseSquare(in sampleAup), gridIntensity);
            return intensity01 > 0f;
        }

        private void Awake()
        {
            // COLD ALLOC: dispatcher phase adapters[3] - SystemDispatcher route owners - owner: RadiationHazardGrid
            _simulationPhase = new SimulationPhaseSystem(this);
            _postSimulationPhase = new PostSimulationPhaseSystem(this);
            _visualSyncPhase = new VisualSyncPhaseSystem(this);
            RefreshColdRegistryReferences();
            EnsureNativeBuffers();
        }

        private void Start()
        {
            RefreshColdRegistryReferences();
            TryRegisterHotSwapListener();
            TryRegisterRuntimeLanes();
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;
            RefreshColdRegistryReferences();
            TryRegisterHotSwapListener();
            EnsureNativeBuffers();
            TryRegisterRuntimeLanes();
        }

        private void OnDisable()
        {
            if (ActiveRuntimeInstance == this)
                ActiveRuntimeInstance = null;

            TryUnregisterRuntimeLanes();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            if (ActiveRuntimeInstance == this)
                ActiveRuntimeInstance = null;

            TryUnregisterRuntimeLanes();
            TryUnregisterHotSwapListener();
            DisposeNativeBuffers();
        }

        private JobHandle ScheduleRadiationSimulation(
            in DispatcherTimingDTO timing,
            in DispatcherJobContext context,
            JobHandle dependsOn)
        {
            _currentSimulationFrame = context.Frame != 0u ? context.Frame : timing.FrameId;
            if (!HasRequiredRuntimeBuffers())
            {
                _radiationEvaluatedThisFrame = false;
                return dependsOn;
            }

            CompleteDiffusionJobIfReady();
            if (_radiationSimulationJobActive)
            {
                PreserveRadiationSourceSignalsForNextSimulation();
                DrainExternalDoseSignals();
                DrainItemAcquiredSignalsDeferred();
                return dependsOn;
            }

            _radiationEvaluatedThisFrame = false;
            PlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();
            DrainRadiationSourceSignals();
            DrainExternalDoseSignals();
            DrainItemAcquiredSignals(playerContext);

            AbsoluteUniversePosition playerAup = ResolvePlayerAup(playerContext);
            _lastSimulationPlayerContext = playerContext;
            _lastSimulationPlayerAup = playerAup;
            JobHandle dependency = dependsOn;

            float qualityWeight = ResolveGlobalQualityWeight();
            float simulationDelta = ResolveSimulationDeltaSeconds(in timing);
            float pendingExternalDose = _pendingExternalDoseRad;
            bool evaluateThisTick = ShouldEvaluateRadiationThisTick(
                qualityWeight,
                _lastExternalIntensity01,
                pendingExternalDose,
                simulationDelta,
                out float integrationDelta);
            if (!evaluateThisTick)
                return dependency;

            if (!_diffusionJobActive)
            {
                RebuildSourceGrid();
                dependency = ScheduleDiffusionJobIfIdle(dependency);
            }

            dependency = ScheduleEmergencyMockSourceIfNeeded(in playerAup, dependency);
            JobHandle radiationHandle = ScheduleRadiationExposureKernel(
                playerContext,
                in playerAup,
                qualityWeight,
                _lastExternalIntensity01,
                pendingExternalDose,
                integrationDelta,
                _currentSimulationFrame,
                dependency);

            _pendingExternalDoseRad = 0f;
            _radiationSimulationStartTicks = Stopwatch.GetTimestamp();
            _lastCompletedIntegrationDeltaSeconds = integrationDelta;
            _radiationSimulationJobHandle = radiationHandle;
            _radiationSimulationJobActive = true;
            _radiationEvaluatedThisFrame = true;
            H8Memory.RegisterActiveJob(OwnerSystemId, radiationHandle);
            return radiationHandle;
        }

        private void PostSimulationRadiation(in DispatcherTimingDTO timing)
        {
            _currentSimulationFrame = timing.FrameId != 0u ? timing.FrameId : _currentSimulationFrame;
            CompleteDiffusionJobIfReady();
            if (_radiationSimulationJobActive)
            {
                if (!DispatcherJobFence.TryFinalizeCompleted(ref _radiationSimulationJobHandle))
                    return;

                _radiationSimulationJobActive = false;
                _lastBurstExecutionMicroseconds = TicksToMicroseconds(Stopwatch.GetTimestamp() - _radiationSimulationStartTicks);
            }

            if (!TryApplyDeferredStructuralOperations())
                return;

            RadiationStateDTO state = _radiationStates.IsCreated && _radiationStates.Length > 0
                ? _radiationStates[0]
                : _lastRadiationState;
            if (!IsRadiationStateFinite(in state))
            {
                DumpBlackBox();
                state = default;
                if (_radiationStates.IsCreated && _radiationStates.Length > 0)
                    _radiationStates[0] = state;
            }

            _lastRadiationState = state;
            _lastGridIntensity01 = state.CurrentExposureRate;
            _accumulatedRadiationDose = state.CumulativeDoseRad;
            _lastShieldingFactor01 = state.ShieldingFactor01;
            _lastCellularDegradation01 = state.CellularDegradation01;

            PlayerRuntimeContext playerContext = _lastSimulationPlayerContext != null
                ? _lastSimulationPlayerContext
                : ResolvePlayerRuntimeContext();
            AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.IsFinite(in _lastSimulationPlayerAup)
                ? _lastSimulationPlayerAup
                : ResolvePlayerAup(playerContext);
            float doseAdd = _radiationEvaluatedThisFrame
                ? math.max(0f, _lastGridIntensity01 * math.max(0f, _lastCompletedIntegrationDeltaSeconds))
                : 0f;
            ApplyDoseToPlayerContext(playerContext, _accumulatedRadiationDose, _lastGridIntensity01);
            PublishPendingRadiationDamageSignal();
            PublishDoseSignal(in playerAup, doseAdd, _lastGridIntensity01, RadiationDoseGridKind);
            EmitGeigerIfNeeded(in playerAup, _lastGridIntensity01);
            RecordTelemetry(playerAup, _lastGridIntensity01, _accumulatedRadiationDose, _radiationEvaluatedThisFrame ? 0u : 1u);
            _lastExternalIntensity01 *= 0.5f;
        }

        private void VisualSyncRadiation(in DispatcherTimingDTO timing)
        {
            _currentSimulationFrame = timing.FrameId != 0u ? timing.FrameId : _currentSimulationFrame;
            PushVisualGlobals(_accumulatedRadiationDose, _lastGridIntensity01);
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _lastShiftSequence = shiftData.Sequence;
            RecordTelemetry(_gridOriginAup, _lastGridIntensity01, _accumulatedRadiationDose, 1u << 1);
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            EnsureNativeBuffers();
            CompleteDiffusionJobIfReady();
            data.radiationDose = _accumulatedRadiationDose;
            data.radiationGridCellSizeMeters = math.max(0.5f, cellSizeMeters);
            double3 origin = _hasGridOrigin ? _gridOriginAup.ToAbsoluteDouble3() : double3.zero;
            data.radiationGridOriginX = origin.x;
            data.radiationGridOriginY = origin.y;
            data.radiationGridOriginZ = origin.z;
            EnsureRleSaveBuffer(data);
            data.radiationGridRleLength = EncodeSparseRle(data.radiationGridRle);
        }

        public void LoadFromSaveData(SaveData data)
        {
            EnsureNativeBuffers();
            if (HasActiveRadiationJobs())
            {
                _pendingLoadData = data;
                _pendingLoadDataValid = true;
                return;
            }

            ApplySaveDataImmediate(data, applyPlayerContext: true);
        }

        private void ApplySaveDataImmediate(SaveData data, bool applyPlayerContext)
        {
            ClearGrid(_gridRead);
            ClearGrid(_gridWrite);
            ClearGrid(_gridSource);

            if (data == null)
            {
                _accumulatedRadiationDose = 0f;
                _lastRadiationState = default;
                if (_radiationStates.IsCreated && _radiationStates.Length > 0)
                    _radiationStates[0] = default;
                return;
            }

            _accumulatedRadiationDose = math.max(0f, data.radiationDose);
            if (_radiationStates.IsCreated && _radiationStates.Length > 0)
            {
                RadiationStateDTO state = _radiationStates[0];
                state.CumulativeDoseRad = _accumulatedRadiationDose;
                state.CurrentExposureRate = _lastGridIntensity01;
                _radiationStates[0] = state;
            }
            cellSizeMeters = math.max(0.5f, data.radiationGridCellSizeMeters);
            if (math.isfinite(data.radiationGridOriginX) &&
                math.isfinite(data.radiationGridOriginY) &&
                math.isfinite(data.radiationGridOriginZ))
            {
                _gridOriginAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(
                    data.radiationGridOriginX,
                    data.radiationGridOriginY,
                    data.radiationGridOriginZ));
                _hasGridOrigin = true;
            }

            DecodeSparseRle(data.radiationGridRle, data.radiationGridRleLength);
            if (applyPlayerContext)
                ApplyDoseToPlayerContext(ResolvePlayerRuntimeContext(), _accumulatedRadiationDose, _lastGridIntensity01);
        }

        private void RegisterSourceInternal(int sourceId, in AbsoluteUniversePosition sourceAup, float intensity, float radiusMeters)
        {
            if (!HasRequiredRuntimeBuffers())
                return;

            float sourceIntensity01 = NormalizeSourceIntensity(intensity);
            float sourceRadiusMeters = math.max(0.5f, radiusMeters > 0f ? radiusMeters : DefaultSourceRadiusMeters);
            if (!_hasGridOrigin)
            {
                _gridOriginAup = sourceAup;
                _hasGridOrigin = true;
            }

            int freeIndex = -1;
            for (int i = 0; i < MaxSourceCount; i++)
            {
                RadiationSource source = _sources[i];
                if (source.Active == 0)
                {
                    if (freeIndex < 0)
                        freeIndex = i;
                    continue;
                }

                if (source.SourceId != sourceId)
                    continue;

                source.PositionAup = sourceAup.ToAbsoluteDouble3();
                source.Intensity01 = sourceIntensity01;
                source.RadiusMeters = sourceRadiusMeters;
                _sources[i] = source;
                _sourceVersion++;
                return;
            }

            if (freeIndex < 0)
                return;

            _sources[freeIndex] = new RadiationSource
            {
                PositionAup = sourceAup.ToAbsoluteDouble3(),
                Intensity01 = sourceIntensity01,
                RadiusMeters = sourceRadiusMeters,
                SourceId = sourceId,
                Active = 1
            };
            _activeSourceCount++;
            if (_sourceCountLane.IsCreated && _sourceCountLane.Length > 0)
                _sourceCountLane[0] = _activeSourceCount;
            _sourceVersion++;
        }

        private void UnregisterSourceInternal(int sourceId)
        {
            if (!_sources.IsCreated)
                return;

            for (int i = 0; i < MaxSourceCount; i++)
            {
                RadiationSource source = _sources[i];
                if (source.Active == 0 || source.SourceId != sourceId)
                    continue;

                _sources[i] = default;
                _activeSourceCount = math.max(0, _activeSourceCount - 1);
                if (_sourceCountLane.IsCreated && _sourceCountLane.Length > 0)
                    _sourceCountLane[0] = _activeSourceCount;
                _sourceVersion++;
                return;
            }
        }

        private void EnsureNativeBuffers()
        {
            EnsureVaultState();
        }

        private bool HasRequiredRuntimeBuffers()
        {
            IDataVault vault = _dataVault;
            return _vaultInitialized &&
                   vault != null &&
                   RefreshVaultViews(vault) &&
                   _gridRead.IsCreated &&
                   _gridWrite.IsCreated &&
                   _gridSource.IsCreated &&
                   _radiationStates.IsCreated &&
                   _sources.IsCreated &&
                   _sourceCountLane.IsCreated &&
                   _telemetryRing.IsCreated &&
                   _telemetryCursorLane.IsCreated &&
                   _damageSignalLane.IsCreated &&
                   _radiationStates.Length >= RadiationEntitySlotCount &&
                   _sources.Length >= MaxSourceCount &&
                   _telemetryRing.Length >= TelemetryCapacity &&
                   _damageSignalLane.Length > 0;
        }

        private JobHandle ScheduleEmergencyMockSourceIfNeeded(in AbsoluteUniversePosition playerAup, JobHandle dependsOn)
        {
            if (!enableEmergencyMockSource || _activeSourceCount > 0 || !AbsoluteUniversePosition.IsFinite(in playerAup))
                return dependsOn;

            if (_sources.IsCreated &&
                _sourceCountLane.IsCreated &&
                _sourceCountLane.Length > 0)
            {
                _activeSourceCount = math.max(_activeSourceCount, 1);
                _sourceCountLane[0] = _activeSourceCount;
                GenerateMockRadiationSourceJob job = new GenerateMockRadiationSourceJob
                {
                    Sources = (RadiationSource*)NativeArrayUnsafeUtility.GetUnsafePtr(_sources),
                    SourceCount = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(_sourceCountLane),
                    Capacity = _sources.Length,
                    PlayerAup = playerAup.ToAbsoluteDouble3(),
                    OffsetMeters = new float3(emergencyMockOffsetMeters.x, emergencyMockOffsetMeters.y, emergencyMockOffsetMeters.z),
                    Intensity01 = NormalizeSourceIntensity(emergencyMockIntensity),
                    RadiusMeters = DefaultSourceRadiusMeters,
                    SourceId = EmergencyMockSourceId
                };
                _sourceVersion++;
                JobHandle handle = job.Schedule(dependsOn);
                H8Memory.RegisterActiveJob(OwnerSystemId, handle);
                return handle;
            }

            return dependsOn;
        }

        private bool EnsureVaultState()
        {
            if (!_layoutChecked)
            {
                _layoutValid = RadiationStateLayoutGuard.ValidateLayout();
                _layoutChecked = true;
            }

            if (!_layoutValid)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!_vaultInitialized || !ReferenceEquals(_dataVault, vault))
            {
                ReleaseVaultHandles();
                _dataVault = vault;
                _gridReadHandle = vault.GetGenerationHandle<float>(BufferID.Shinobu274RadiationGridRead, GridCellCount, OwnerSystemId);
                _gridWriteHandle = vault.GetGenerationHandle<float>(BufferID.Shinobu274RadiationGridWrite, GridCellCount, OwnerSystemId);
                _gridSourceHandle = vault.GetGenerationHandle<float>(BufferID.Shinobu274RadiationGridSource, GridCellCount, OwnerSystemId);
                _stateHandle = vault.GetGenerationHandle<RadiationStateDTO>(BufferID.Shinobu274RadiationStates, RadiationEntitySlotCount, OwnerSystemId);
                _sourcesHandle = vault.GetGenerationHandle<RadiationSource>(BufferID.Shinobu274RadiationSources, MaxSourceCount, OwnerSystemId);
                _sourceCountHandle = vault.GetGenerationHandle<int>(BufferID.Shinobu274RadiationSourceCount, 1, OwnerSystemId);
                _telemetryHandle = vault.GetGenerationHandle<RadiationTelemetryEntry>(BufferID.Shinobu274RadiationTelemetryRing, TelemetryCapacity, OwnerSystemId);
                _telemetryCursorHandle = vault.GetGenerationHandle<uint>(BufferID.Shinobu274RadiationTelemetryCursor, 1, OwnerSystemId);
                _profilesHandle = vault.GetGenerationHandle<RadiationProfileDTO>(BufferID.Shinobu274RadiationProfiles, RadiationProfileCapacity, OwnerSystemId);
                _csvScratchHandle = vault.GetGenerationHandle<byte>(BufferID.Shinobu274RadiationCsvScratch, RadiationCsvScratchBytes, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
                _tuningHandle = vault.GetGenerationHandle<RadiationTuningDTO>(BufferID.Shinobu274RadiationTuning, 1, OwnerSystemId);
                _damageSignalHandle = vault.GetGenerationHandle<CombatDamageSignal>(BufferID.Shinobu274RadiationDamageSignal, 1, OwnerSystemId);
                TryBindBulkheadReadHandles(vault);
                _vaultInitialized = true;
            }

            return RefreshVaultViews(vault);
        }

        private bool RefreshVaultViews(IDataVault vault)
        {
            if (vault == null)
                return false;

            if (!vault.TryResolveHandle(in _gridReadHandle, out NativeArray<float> gridRead) ||
                !vault.TryResolveHandle(in _gridWriteHandle, out NativeArray<float> gridWrite) ||
                !vault.TryResolveHandle(in _gridSourceHandle, out NativeArray<float> gridSource) ||
                !vault.TryResolveHandle(in _stateHandle, out NativeArray<RadiationStateDTO> states) ||
                !vault.TryResolveHandle(in _sourcesHandle, out NativeArray<RadiationSource> sources) ||
                !vault.TryResolveHandle(in _sourceCountHandle, out NativeArray<int> sourceCount) ||
                !vault.TryResolveHandle(in _telemetryHandle, out NativeArray<RadiationTelemetryEntry> telemetry) ||
                !vault.TryResolveHandle(in _telemetryCursorHandle, out NativeArray<uint> telemetryCursor) ||
                !vault.TryResolveHandle(in _profilesHandle, out NativeArray<RadiationProfileDTO> profiles) ||
                !vault.TryResolveHandle(in _csvScratchHandle, out NativeArray<byte> csvScratch) ||
                !vault.TryResolveHandle(in _tuningHandle, out NativeArray<RadiationTuningDTO> tuning) ||
                !vault.TryResolveHandle(in _damageSignalHandle, out NativeArray<CombatDamageSignal> damageSignal))
            {
                return false;
            }

            _gridRead = _gridBuffersSwapped ? gridWrite : gridRead;
            _gridWrite = _gridBuffersSwapped ? gridRead : gridWrite;
            _gridSource = gridSource;
            _radiationStates = states;
            _sources = sources;
            _sourceCountLane = sourceCount;
            _telemetryRing = telemetry;
            _telemetryCursorLane = telemetryCursor;
            _profiles = profiles;
            _csvScratch = csvScratch;
            _tuningLane = tuning;
            _damageSignalLane = damageSignal;

            if (_sourceCountLane.IsCreated && _sourceCountLane.Length > 0)
                _sourceCountLane[0] = _activeSourceCount;
            if (_telemetryCursorLane.IsCreated && _telemetryCursorLane.Length > 0 && _telemetryWriteIndex == 0)
                _telemetryWriteIndex = unchecked((int)_telemetryCursorLane[0]);

            EnsureDefaultRadiationTuning();
            TryLoadRadiationProfilesCsv();
            return gridRead.IsCreated && gridRead.Length >= GridCellCount &&
                   gridWrite.IsCreated && gridWrite.Length >= GridCellCount &&
                   gridSource.IsCreated && gridSource.Length >= GridCellCount &&
                   states.IsCreated && states.Length >= RadiationEntitySlotCount &&
                   sources.IsCreated && sources.Length >= MaxSourceCount &&
                   telemetry.IsCreated && telemetry.Length >= TelemetryCapacity;
        }

        private void EnsureDefaultRadiationTuning()
        {
            if (!_tuningLane.IsCreated || _tuningLane.Length == 0)
                return;

            RadiationTuningDTO tuning = _tuningLane[0];
            if (tuning.Flags == 0u)
            {
                tuning.DoseToDegradationScale = 0.01f;
                tuning.DecayPerTick = DoseDecayPerSimulationStep;
                tuning.DamagePerTickScale = RadiationDamagePerTickScale;
                tuning.LeadShieldingEffectiveness = 1f;
                tuning.MaxSdfSamples = 12;
                tuning.Flags = 1u;
                _tuningLane[0] = tuning;
            }
        }

        private void TryLoadRadiationProfilesCsv()
        {
            if (_profilesCsvLoaded || radiationProfilesCsv == null || !_profiles.IsCreated)
                return;

            NativeArray<byte> bytes = radiationProfilesCsv.GetData<byte>();
            if (!bytes.IsCreated || bytes.Length == 0)
            {
                _profilesCsvLoaded = true;
                return;
            }

            IngestRadiationProfilesCsv(bytes, _profiles);
            _profilesCsvLoaded = true;
        }

        private static int IngestRadiationProfilesCsv(NativeArray<byte> csv, NativeArray<RadiationProfileDTO> profiles)
        {
            if (!csv.IsCreated || csv.Length == 0 || !profiles.IsCreated)
                return 0;

            int lineStart = 0;
            int row = 0;
            bool headerSkipped = false;
            while (lineStart < csv.Length && row < profiles.Length)
            {
                int lineEnd = lineStart;
                while (lineEnd < csv.Length && csv[lineEnd] != (byte)'\n')
                    lineEnd++;

                int cleanEnd = lineEnd;
                if (cleanEnd > lineStart && csv[cleanEnd - 1] == (byte)'\r')
                    cleanEnd--;

                if (cleanEnd > lineStart)
                {
                    if (!headerSkipped && LooksLikeCsvHeader(csv, lineStart, cleanEnd))
                    {
                        headerSkipped = true;
                    }
                    else
                    {
                        RadiationProfileDTO profile = ParseRadiationProfileLine(csv, lineStart, cleanEnd);
                        if (profile.ProfileHash != 0u)
                            profiles[row++] = profile;
                    }
                }

                lineStart = lineEnd + 1;
            }

            return row;
        }

        private static int IngestRadiationProfilesCsv(ReadOnlySpan<byte> csv, NativeArray<RadiationProfileDTO> profiles)
        {
            if (csv.Length == 0 || !profiles.IsCreated)
                return 0;

            int lineStart = 0;
            int row = 0;
            bool headerSkipped = false;
            while (lineStart < csv.Length && row < profiles.Length)
            {
                int lineEnd = lineStart;
                while (lineEnd < csv.Length && csv[lineEnd] != (byte)'\n')
                    lineEnd++;

                int cleanEnd = lineEnd;
                if (cleanEnd > lineStart && csv[cleanEnd - 1] == (byte)'\r')
                    cleanEnd--;

                if (cleanEnd > lineStart)
                {
                    if (!headerSkipped && LooksLikeCsvHeader(csv.Slice(lineStart, cleanEnd - lineStart)))
                    {
                        headerSkipped = true;
                    }
                    else
                    {
                        RadiationProfileDTO profile = ParseRadiationProfileLine(csv, lineStart, cleanEnd);
                        if (profile.ProfileHash != 0u)
                            profiles[row++] = profile;
                    }
                }

                lineStart = lineEnd + 1;
            }

            return row;
        }

        private static RadiationProfileDTO ParseRadiationProfileLine(NativeArray<byte> csv, int start, int end)
        {
            RadiationProfileDTO profile = default;
            int cursor = start;
            for (int field = 0; field < 5 && cursor <= end; field++)
            {
                int fieldStart = cursor;
                while (cursor < end && csv[cursor] != (byte)',')
                    cursor++;

                TrimAscii(csv, fieldStart, cursor, out int tokenStart, out int tokenEnd);
                switch (field)
                {
                    case 0:
                        profile.ProfileHash = HashAscii(csv, tokenStart, tokenEnd);
                        break;
                    case 1:
                        profile.IntensityScale = ParseAsciiFloat(csv, tokenStart, tokenEnd, 1f);
                        break;
                    case 2:
                        profile.RadiusMeters = ParseAsciiFloat(csv, tokenStart, tokenEnd, DefaultSourceRadiusMeters);
                        break;
                    case 3:
                        profile.ShieldAttenuation01 = math.saturate(ParseAsciiFloat(csv, tokenStart, tokenEnd, 1f));
                        break;
                    case 4:
                        profile.MutationScale = math.max(0f, ParseAsciiFloat(csv, tokenStart, tokenEnd, 1f));
                        break;
                }

                cursor++;
            }

            profile.IntensityScale = math.max(0f, math.isfinite(profile.IntensityScale) ? profile.IntensityScale : 1f);
            profile.RadiusMeters = math.max(0.5f, math.isfinite(profile.RadiusMeters) ? profile.RadiusMeters : DefaultSourceRadiusMeters);
            profile.ShieldAttenuation01 = math.saturate(math.isfinite(profile.ShieldAttenuation01) ? profile.ShieldAttenuation01 : 1f);
            profile.MutationScale = math.max(0f, math.isfinite(profile.MutationScale) ? profile.MutationScale : 1f);
            profile.Flags = 1u;
            return profile;
        }

        private static void TrimAscii(NativeArray<byte> value, int start, int end, out int trimmedStart, out int trimmedEnd)
        {
            while (start < end && value[start] <= (byte)' ')
                start++;
            while (end > start && value[end - 1] <= (byte)' ')
                end--;

            trimmedStart = start;
            trimmedEnd = end;
        }

        private static bool LooksLikeCsvHeader(NativeArray<byte> value, int start, int end)
        {
            TrimAscii(value, start, end, out int trimmedStart, out int trimmedEnd);
            int length = trimmedEnd - trimmedStart;
            if (length < 4)
                return false;

            byte c0 = ToLowerAscii(value[trimmedStart]);
            byte c1 = ToLowerAscii(value[trimmedStart + 1]);
            byte c2 = ToLowerAscii(value[trimmedStart + 2]);
            byte c3 = ToLowerAscii(value[trimmedStart + 3]);
            if (c0 == (byte)'n' && c1 == (byte)'a' && c2 == (byte)'m' && c3 == (byte)'e')
                return true;
            return length >= 7 &&
                   c0 == (byte)'p' &&
                   c1 == (byte)'r' &&
                   c2 == (byte)'o' &&
                   c3 == (byte)'f' &&
                   ToLowerAscii(value[trimmedStart + 4]) == (byte)'i' &&
                   ToLowerAscii(value[trimmedStart + 5]) == (byte)'l' &&
                   ToLowerAscii(value[trimmedStart + 6]) == (byte)'e';
        }

        private static uint HashAscii(NativeArray<byte> value, int start, int end)
        {
            uint hash = 2166136261u;
            for (int i = start; i < end; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }

            return hash == 0u ? 1u : hash;
        }

        private static float ParseAsciiFloat(NativeArray<byte> value, int start, int end, float fallback)
        {
            if (start >= end)
                return fallback;

            int index = start;
            float sign = 1f;
            if (value[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }

            float result = 0f;
            bool any = false;
            while (index < end)
            {
                byte c = value[index];
                if (c < (byte)'0' || c > (byte)'9')
                    break;
                result = result * 10f + (c - (byte)'0');
                any = true;
                index++;
            }

            if (index < end && value[index] == (byte)'.')
            {
                index++;
                float scale = 0.1f;
                while (index < end)
                {
                    byte c = value[index];
                    if (c < (byte)'0' || c > (byte)'9')
                        break;
                    result += (c - (byte)'0') * scale;
                    scale *= 0.1f;
                    any = true;
                    index++;
                }
            }

            return any ? result * sign : fallback;
        }

        private static RadiationProfileDTO ParseRadiationProfileLine(ReadOnlySpan<byte> csv, int start, int end)
        {
            RadiationProfileDTO profile = default;
            int cursor = start;
            for (int field = 0; field < 5 && cursor <= end; field++)
            {
                int fieldStart = cursor;
                while (cursor < end && csv[cursor] != (byte)',')
                    cursor++;

                ReadOnlySpan<byte> token = TrimAscii(csv.Slice(fieldStart, cursor - fieldStart));
                switch (field)
                {
                    case 0:
                        profile.ProfileHash = HashAscii(token);
                        break;
                    case 1:
                        profile.IntensityScale = ParseAsciiFloat(token, 1f);
                        break;
                    case 2:
                        profile.RadiusMeters = ParseAsciiFloat(token, DefaultSourceRadiusMeters);
                        break;
                    case 3:
                        profile.ShieldAttenuation01 = math.saturate(ParseAsciiFloat(token, 1f));
                        break;
                    case 4:
                        profile.MutationScale = math.max(0f, ParseAsciiFloat(token, 1f));
                        break;
                }

                cursor++;
            }

            profile.IntensityScale = math.max(0f, math.isfinite(profile.IntensityScale) ? profile.IntensityScale : 1f);
            profile.RadiusMeters = math.max(0.5f, math.isfinite(profile.RadiusMeters) ? profile.RadiusMeters : DefaultSourceRadiusMeters);
            profile.ShieldAttenuation01 = math.saturate(math.isfinite(profile.ShieldAttenuation01) ? profile.ShieldAttenuation01 : 1f);
            profile.MutationScale = math.max(0f, math.isfinite(profile.MutationScale) ? profile.MutationScale : 1f);
            profile.Flags = 1u;
            return profile;
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length;
            while (start < end && value[start] <= (byte)' ')
                start++;
            while (end > start && value[end - 1] <= (byte)' ')
                end--;
            return value.Slice(start, end - start);
        }

        private static bool LooksLikeCsvHeader(ReadOnlySpan<byte> value)
        {
            ReadOnlySpan<byte> trimmed = TrimAscii(value);
            if (trimmed.Length < 4)
                return false;

            byte c0 = ToLowerAscii(trimmed[0]);
            byte c1 = ToLowerAscii(trimmed[1]);
            byte c2 = ToLowerAscii(trimmed[2]);
            byte c3 = ToLowerAscii(trimmed[3]);
            if (c0 == (byte)'n' && c1 == (byte)'a' && c2 == (byte)'m' && c3 == (byte)'e')
                return true;
            if (trimmed.Length >= 7 &&
                c0 == (byte)'p' &&
                c1 == (byte)'r' &&
                c2 == (byte)'o' &&
                c3 == (byte)'f' &&
                ToLowerAscii(trimmed[4]) == (byte)'i' &&
                ToLowerAscii(trimmed[5]) == (byte)'l' &&
                ToLowerAscii(trimmed[6]) == (byte)'e')
            {
                return true;
            }

            return false;
        }

        private static byte ToLowerAscii(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z'
                ? (byte)(value + 32)
                : value;
        }

        private static uint HashAscii(ReadOnlySpan<byte> value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }

            return hash == 0u ? 1u : hash;
        }

        private static float ParseAsciiFloat(ReadOnlySpan<byte> value, float fallback)
        {
            if (value.Length == 0)
                return fallback;

            int index = 0;
            float sign = 1f;
            if (value[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }

            float result = 0f;
            bool any = false;
            while (index < value.Length)
            {
                byte c = value[index];
                if (c < (byte)'0' || c > (byte)'9')
                    break;
                result = result * 10f + (c - (byte)'0');
                any = true;
                index++;
            }

            if (index < value.Length && value[index] == (byte)'.')
            {
                index++;
                float scale = 0.1f;
                while (index < value.Length)
                {
                    byte c = value[index];
                    if (c < (byte)'0' || c > (byte)'9')
                        break;
                    result += (c - (byte)'0') * scale;
                    scale *= 0.1f;
                    any = true;
                    index++;
                }
            }

            return any ? result * sign : fallback;
        }

        private void TryBindBulkheadReadHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            if (vault.TryGetGenerationHandle(BufferID.Shinobu220BulkheadStates, out VaultGenerationHandle<BulkheadStateDTO> statesHandle))
                _bulkheadStatesReadHandle = statesHandle;
            if (vault.TryGetGenerationHandle(BufferID.Shinobu220BulkheadPlanes, out VaultGenerationHandle<BulkheadPlaneDTO> planesHandle))
                _bulkheadPlanesReadHandle = planesHandle;
        }

        private void ReleaseVaultHandles()
        {
            IDataVault vault = _dataVault;
            if (vault != null)
            {
                ReleaseVaultHandle(vault, ref _gridReadHandle);
                ReleaseVaultHandle(vault, ref _gridWriteHandle);
                ReleaseVaultHandle(vault, ref _gridSourceHandle);
                ReleaseVaultHandle(vault, ref _stateHandle);
                ReleaseVaultHandle(vault, ref _sourcesHandle);
                ReleaseVaultHandle(vault, ref _sourceCountHandle);
                ReleaseVaultHandle(vault, ref _telemetryHandle);
                ReleaseVaultHandle(vault, ref _telemetryCursorHandle);
                ReleaseVaultHandle(vault, ref _profilesHandle);
                ReleaseVaultHandle(vault, ref _csvScratchHandle);
                ReleaseVaultHandle(vault, ref _tuningHandle);
                ReleaseVaultHandle(vault, ref _damageSignalHandle);
            }

            _gridReadHandle = default;
            _gridWriteHandle = default;
            _gridSourceHandle = default;
            _stateHandle = default;
            _sourcesHandle = default;
            _sourceCountHandle = default;
            _telemetryHandle = default;
            _telemetryCursorHandle = default;
            _profilesHandle = default;
            _csvScratchHandle = default;
            _tuningHandle = default;
            _damageSignalHandle = default;
            _bulkheadStatesReadHandle = default;
            _bulkheadPlanesReadHandle = default;
            _gridRead = default;
            _gridWrite = default;
            _gridSource = default;
            _radiationStates = default;
            _sources = default;
            _sourceCountLane = default;
            _telemetryRing = default;
            _telemetryCursorLane = default;
            _profiles = default;
            _csvScratch = default;
            _tuningLane = default;
            _damageSignalLane = default;
            _gridBuffersSwapped = false;
            _vaultInitialized = false;
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void DisposeNativeBuffers()
        {
            CompleteRadiationJobsForTeardownRelease();
            ReleaseVaultHandles();
        }

        private void TryRegisterRuntimeLanes()
        {
            if (!Application.isPlaying)
                return;

            if (!_registeredSimulationPhase && _simulationPhase != null)
                _registeredSimulationPhase = GlobalRegistry.TryRegisterDispatcherSystem(_simulationPhase);

            if (!_registeredPostSimulationPhase && _postSimulationPhase != null)
                _registeredPostSimulationPhase = GlobalRegistry.TryRegisterDispatcherSystem(_postSimulationPhase);

            if (!_registeredVisualSyncPhase && _visualSyncPhase != null)
                _registeredVisualSyncPhase = GlobalRegistry.TryRegisterDispatcherSystem(_visualSyncPhase);

            if (!_registeredOriginShift)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _registeredOriginShift = true;
            }

            ISaveService saveService = _saveService;
            if (!_registeredSave && saveService != null)
            {
                saveService.Register(this);
                _registeredSave = true;
            }
        }

        private void TryUnregisterRuntimeLanes()
        {
            if (_registeredSimulationPhase && _simulationPhase != null)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_simulationPhase);
                _registeredSimulationPhase = false;
            }

            if (_registeredPostSimulationPhase && _postSimulationPhase != null)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_postSimulationPhase);
                _registeredPostSimulationPhase = false;
            }

            if (_registeredVisualSyncPhase && _visualSyncPhase != null)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_visualSyncPhase);
                _registeredVisualSyncPhase = false;
            }

            if (_registeredOriginShift)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShift = false;
            }

            ISaveService saveService = _saveService;
            if (_registeredSave && saveService != null)
            {
                saveService.Unregister(this);
                _registeredSave = false;
            }
        }

        private void RefreshColdRegistryReferences()
        {
            _saveService = GlobalRegistry.Save;
            _dataVault = GlobalRegistry.DataVault;
            _voxelSdfReadModel = GlobalRegistry.VoxelSonarSdf;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    if (currentService != null)
                        TryRegisterRuntimeLanes();
                    break;
                case GlobalRegistryServiceSlot.Save:
                    if (_registeredSave && previousService is ISaveService previousSave)
                        previousSave.Unregister(this);
                    _registeredSave = false;
                    _saveService = currentService as ISaveService;
                    if (_saveService != null)
                        TryRegisterRuntimeLanes();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    IDataVault nextVault = currentService as IDataVault;
                    if (HasActiveRadiationJobs())
                    {
                        _pendingDataVault = nextVault;
                        _pendingDataVaultSwap = true;
                        return;
                    }

                    ApplyDataVaultSwap(nextVault);
                    break;
                case GlobalRegistryServiceSlot.VoxelEngineRuntime:
                    _voxelSdfReadModel = currentService as VoxelSdfReadModel;
                    break;
            }
        }

        private void RebuildSourceGrid()
        {
            if (!_gridSource.IsCreated || !_gridRead.IsCreated)
                return;

            ClearGrid(_gridSource);
            if (_activeSourceCount <= 0 || !_hasGridOrigin)
                return;

            double3 origin = _gridOriginAup.ToAbsoluteDouble3();
            float safeCellSize = math.max(0.5f, cellSizeMeters);
            int half = GridResolution >> 1;

            for (int sourceIndex = 0; sourceIndex < MaxSourceCount; sourceIndex++)
            {
                RadiationSource source = _sources[sourceIndex];
                if (source.Active == 0 || source.Intensity01 <= 0f)
                    continue;

                double3 sourceAbsolute = source.PositionAup;
                double3 sourceOffset = sourceAbsolute - origin;
                int centerX = (int)math.floor(sourceOffset.x / safeCellSize) + half;
                int centerY = (int)math.floor(sourceOffset.y / safeCellSize) + half;
                int centerZ = (int)math.floor(sourceOffset.z / safeCellSize) + half;
                int radiusCells = math.max(1, (int)math.ceil(source.RadiusMeters / safeCellSize));
                int minX = math.max(0, centerX - radiusCells);
                int maxX = math.min(GridResolution - 1, centerX + radiusCells);
                int minY = math.max(0, centerY - radiusCells);
                int maxY = math.min(GridResolution - 1, centerY + radiusCells);
                int minZ = math.max(0, centerZ - radiusCells);
                int maxZ = math.min(GridResolution - 1, centerZ + radiusCells);
                float radiusSq = math.max(0.25f, source.RadiusMeters * source.RadiusMeters);

                for (int z = minZ; z <= maxZ; z++)
                {
                    float dz = (z - centerZ) * safeCellSize;
                    for (int y = minY; y <= maxY; y++)
                    {
                        float dy = (y - centerY) * safeCellSize;
                        for (int x = minX; x <= maxX; x++)
                        {
                            float dx = (x - centerX) * safeCellSize;
                            float distanceSq = dx * dx + dy * dy + dz * dz;
                            if (distanceSq > radiusSq)
                                continue;

                            float falloff = 1f - math.saturate(distanceSq / radiusSq);
                            float value = source.Intensity01 * falloff;
                            int cellIndex = Flatten(x, y, z);
                            if (value > _gridSource[cellIndex])
                                _gridSource[cellIndex] = value;
                            if (value > _gridRead[cellIndex])
                                _gridRead[cellIndex] = value;
                        }
                    }
                }
            }
        }

        private JobHandle ScheduleDiffusionJobIfIdle(JobHandle dependsOn)
        {
            if (_diffusionJobActive || !_gridRead.IsCreated || !_gridWrite.IsCreated || !_gridSource.IsCreated)
                return dependsOn;

            RadiationJacobiDiffusionJob job = new RadiationJacobiDiffusionJob
            {
                Previous = _gridRead,
                Sources = _gridSource,
                Next = _gridWrite,
                Width = GridResolution,
                Height = GridResolution,
                Depth = GridResolution
            };
            _diffusionJobHandle = job.Schedule(GridCellCount, 64, dependsOn);
            _diffusionJobActive = true;
            H8Memory.RegisterActiveJob(OwnerSystemId, _diffusionJobHandle);
            return _diffusionJobHandle;
        }

        private void CompleteDiffusionJobIfReady()
        {
            if (!_diffusionJobActive || !_diffusionJobHandle.IsCompleted)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _diffusionJobHandle))
                return;

            _diffusionJobActive = false;
            NativeArray<float> previousRead = _gridRead;
            _gridRead = _gridWrite;
            _gridWrite = previousRead;
            _gridBuffersSwapped = !_gridBuffersSwapped;
            _gridVersion++;
        }

        private void CompleteDiffusionJobForTeardownRelease()
        {
            if (!_diffusionJobActive)
                return;

            DispatcherJobFence.TryComplete(ref _diffusionJobHandle, forceComplete: true);
            _diffusionJobActive = false;
            NativeArray<float> previousRead = _gridRead;
            _gridRead = _gridWrite;
            _gridWrite = previousRead;
            _gridBuffersSwapped = !_gridBuffersSwapped;
            _gridVersion++;
        }

        private bool HasActiveRadiationJobs()
        {
            return _radiationSimulationJobActive || _diffusionJobActive;
        }

        private bool TryApplyDeferredStructuralOperations()
        {
            if (HasActiveRadiationJobs())
                return false;

            if (_pendingDataVaultSwap)
            {
                ApplyDataVaultSwap(_pendingDataVault);
                _pendingDataVault = null;
                _pendingDataVaultSwap = false;
            }

            if (_pendingLoadDataValid)
            {
                SaveData loadData = _pendingLoadData;
                _pendingLoadData = null;
                _pendingLoadDataValid = false;
                ApplySaveDataImmediate(loadData, applyPlayerContext: false);
            }

            return true;
        }

        private void ApplyDataVaultSwap(IDataVault nextVault)
        {
            ReleaseVaultHandles();
            _dataVault = nextVault;
            EnsureNativeBuffers();
        }

        private void CompleteRadiationJobsForTeardownRelease()
        {
            if (_radiationSimulationJobActive)
            {
                DispatcherJobFence.TryComplete(ref _radiationSimulationJobHandle, forceComplete: true);
                _radiationSimulationJobActive = false;
            }

            CompleteDiffusionJobForTeardownRelease();
        }

        private float SampleGridNearest(in AbsoluteUniversePosition sampleAup)
        {
            if (!_gridRead.IsCreated || !_hasGridOrigin)
                return 0f;

            if (!TryResolveGridCell(in sampleAup, out int x, out int y, out int z))
                return 0f;

            return math.saturate(_gridRead[Flatten(x, y, z)]);
        }

        private float SampleInverseSquare(in AbsoluteUniversePosition sampleAup)
        {
            if (!_sources.IsCreated || _activeSourceCount <= 0)
                return 0f;

            float total = 0f;
            double3 sampleAbsolute = sampleAup.ToAbsoluteDouble3();
            for (int i = 0; i < MaxSourceCount; i++)
            {
                RadiationSource source = _sources[i];
                if (source.Active == 0 || source.Intensity01 <= 0f)
                    continue;

                double3 delta = sampleAbsolute - source.PositionAup;
                double distanceSq = math.lengthsq(delta);
                float radiusSq = source.RadiusMeters * source.RadiusMeters;
                float inverseSq = radiusSq * math.rcp((float)math.max(1d, distanceSq));
                total += source.Intensity01 * math.saturate(inverseSq);
            }

            return math.saturate(total);
        }

        private JobHandle ScheduleRadiationExposureKernel(
            PlayerRuntimeContext playerContext,
            in AbsoluteUniversePosition playerAup,
            float qualityWeight,
            float externalExposureRate,
            float externalDoseDelta,
            float simulationTickDelta,
            uint frame,
            JobHandle dependsOn)
        {
            if (!_radiationStates.IsCreated ||
                !_sources.IsCreated ||
                !_damageSignalLane.IsCreated ||
                _radiationStates.Length == 0 ||
                _damageSignalLane.Length == 0)
            {
                return dependsOn;
            }

            NativeArray<byte> encodedSdf = default;
            int3 sdfDimensions = default;
            float3 sdfVolumeOrigin = default;
            float3 sdfCellSize = default;
            float sdfRange = 0f;
            float3 playerRuntime = playerAup.ToRuntimeFloat3();
            VoxelSdfReadModel sdfReadModel = _voxelSdfReadModel;
            if (sdfReadModel != null)
            {
                sdfReadModel.TryReadNearestSonarSdf(
                    playerRuntime,
                    out encodedSdf,
                    out sdfDimensions,
                    out sdfVolumeOrigin,
                    out sdfCellSize,
                    out sdfRange);
            }

            NativeArray<BulkheadStateDTO> bulkheadStates = default;
            NativeArray<BulkheadPlaneDTO> bulkheadPlanes = default;
            ResolveBulkheadReadBuffers(ref bulkheadStates, ref bulkheadPlanes);

            RadiationTuningDTO tuning = _tuningLane.IsCreated && _tuningLane.Length > 0
                ? _tuningLane[0]
                : CreateDefaultRadiationTuning();
            float sanitizedQuality = math.saturate(math.isfinite(qualityWeight) ? qualityWeight : 0f);
            int activeSources = _sourceCountLane.IsCreated && _sourceCountLane.Length > 0
                ? math.clamp(_sourceCountLane[0], 0, MaxSourceCount)
                : math.clamp(_activeSourceCount, 0, MaxSourceCount);
            int maxBulkheadSamples = ResolveBulkheadSampleLimit(sanitizedQuality, bulkheadStates, bulkheadPlanes);
            int sdfSampleCount = ResolveSdfSampleCount(sanitizedQuality, tuning);
            uint playerTargetId = ResolvePlayerCombatTargetId(playerContext);
            double3 playerAbsolute = playerAup.ToAbsoluteDouble3();
            RadiationStateDTO seedState = _radiationStates[0];
            if (_accumulatedRadiationDose > seedState.CumulativeDoseRad && math.isfinite(_accumulatedRadiationDose))
            {
                seedState.CumulativeDoseRad = _accumulatedRadiationDose;
                _radiationStates[0] = seedState;
            }
            _damageSignalLane[0] = default;

            CalculateRadiationExposureJob job = new CalculateRadiationExposureJob
            {
                States = (RadiationStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(_radiationStates),
                Sources = (RadiationSource*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_sources),
                DamageSignal = (CombatDamageSignal*)NativeArrayUnsafeUtility.GetUnsafePtr(_damageSignalLane),
                EncodedSdf = encodedSdf,
                BulkheadStates = bulkheadStates.IsCreated ? (BulkheadStateDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bulkheadStates) : null,
                BulkheadPlanes = bulkheadPlanes.IsCreated ? (BulkheadPlaneDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bulkheadPlanes) : null,
                SourceCapacity = _sources.Length,
                ActiveSourceCount = activeSources,
                BulkheadCount = maxBulkheadSamples,
                PlayerAup = playerAbsolute,
                PlayerRuntime = playerRuntime,
                SimulationTickDelta = math.max(0f, simulationTickDelta),
                ExternalExposureRate = math.saturate(externalExposureRate),
                ExternalDoseDelta = math.max(0f, math.isfinite(externalDoseDelta) ? externalDoseDelta : 0f),
                DoseDecayPerTick = SanitizeRange(tuning.DecayPerTick, DoseDecayPerSimulationStep, 0f, 1f),
                DoseToDegradationScale = SanitizeRange(tuning.DoseToDegradationScale, 0.01f, 0.0001f, 1f),
                DamagePerTickScale = SanitizeRange(tuning.DamagePerTickScale, RadiationDamagePerTickScale, 0f, 100f),
                LeadShieldingEffectiveness = SanitizeRange(tuning.LeadShieldingEffectiveness, 1f, 0f, 1f),
                PlayerTargetId = playerTargetId,
                Frame = frame,
                SdfDimensions = sdfDimensions,
                SdfVolumeOrigin = sdfVolumeOrigin,
                SdfCellSize = SanitizeCellSize(sdfCellSize),
                SdfRange = math.max(0.001f, sdfRange),
                SdfSampleCount = sdfSampleCount
            };
            return job.Schedule(dependsOn);
        }

        private void ResolveBulkheadReadBuffers(
            ref NativeArray<BulkheadStateDTO> bulkheadStates,
            ref NativeArray<BulkheadPlaneDTO> bulkheadPlanes)
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            if (_bulkheadStatesReadHandle.BufferID == 0u || _bulkheadPlanesReadHandle.BufferID == 0u)
                TryBindBulkheadReadHandles(vault);

            if (_bulkheadStatesReadHandle.BufferID != 0u)
                vault.TryReadHandle(in _bulkheadStatesReadHandle, out bulkheadStates);
            if (_bulkheadPlanesReadHandle.BufferID != 0u)
                vault.TryReadHandle(in _bulkheadPlanesReadHandle, out bulkheadPlanes);
        }

        private static uint ResolvePlayerCombatTargetId(PlayerRuntimeContext playerContext)
        {
            if (playerContext == null || playerContext.PlayerObject == null)
                return 0u;

            int target = CombatDamageRuntime.ResolveTargetId(playerContext.PlayerObject);
            return target == 0 ? 0u : unchecked((uint)target);
        }

        private void PublishPendingRadiationDamageSignal()
        {
            if (!_damageSignalLane.IsCreated || _damageSignalLane.Length == 0)
                return;

            CombatDamageSignal signal = _damageSignalLane[0];
            if (signal.Magnitude <= 0f || signal.TargetHash == 0u && signal.TargetId == 0)
                return;

            SignalBus<CombatDamageSignal>.Push(in signal);
            _damageSignalLane[0] = default;
        }

        private static bool IsRadiationStateFinite(in RadiationStateDTO state)
        {
            return math.isfinite(state.CumulativeDoseRad) &&
                   math.isfinite(state.CurrentExposureRate) &&
                   math.isfinite(state.ShieldingFactor01) &&
                   math.isfinite(state.CellularDegradation01);
        }

        private static float3 SanitizeCellSize(float3 cellSize)
        {
            if (!math.all(math.isfinite(cellSize)))
                return new float3(1f, 1f, 1f);

            return math.max(cellSize, new float3(0.001f, 0.001f, 0.001f));
        }

        private static int ResolveSdfSampleCount(float qualityWeight, in RadiationTuningDTO tuning)
        {
            int maxSamples = math.clamp(tuning.MaxSdfSamples > 0 ? tuning.MaxSdfSamples : 12, 2, 24);
            return math.clamp((int)math.round(math.lerp(2f, maxSamples, math.saturate(qualityWeight))), 2, maxSamples);
        }

        private static int ResolveBulkheadSampleLimit(
            float qualityWeight,
            NativeArray<BulkheadStateDTO> states,
            NativeArray<BulkheadPlaneDTO> planes)
        {
            if (!states.IsCreated || !planes.IsCreated)
                return 0;

            int count = math.min(states.Length, planes.Length);
            int budget = math.clamp((int)math.round(math.lerp(32f, 256f, math.saturate(qualityWeight))), 32, 256);
            return math.min(count, budget);
        }

        private static float SanitizeRange(float value, float fallback, float min, float max)
        {
            return math.clamp(math.isfinite(value) ? value : fallback, min, max);
        }

        private static float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(weight) ? weight : 0f);
        }

        private static float ResolveSimulationDeltaSeconds(in DispatcherTimingDTO timing)
        {
            float dt = timing.FrameDelta;
            if (!math.isfinite(dt) || dt <= 0f)
                dt = 1f / 60f;

            return math.min(dt, 0.2f);
        }

        private bool ShouldEvaluateRadiationThisTick(
            float qualityWeight,
            float externalExposureRate,
            float externalDoseDelta,
            float simulationDeltaSeconds,
            out float integrationDelta)
        {
            float q = math.saturate(math.isfinite(qualityWeight) ? qualityWeight : 0f);
            float tickInterval = math.lerp(0.2f, 0.016f, q);
            _radiationCadenceAccumulatorSeconds += math.max(0f, simulationDeltaSeconds);
            bool forced = _lastRadiationState.EntityHashID == 0u ||
                          externalExposureRate > 0.0001f ||
                          math.abs(externalDoseDelta) > 0.0001f;
            bool evaluate = forced || _radiationCadenceAccumulatorSeconds >= tickInterval;
            float accumulatedSeconds = _radiationCadenceAccumulatorSeconds;
            integrationDelta = math.max(0f, doseScalePerFrostTick) * accumulatedSeconds;
            if (evaluate)
                _radiationCadenceAccumulatorSeconds = 0f;
            return evaluate;
        }

        private static bool UsesSparseRadiationCadence(float qualityWeight, uint frame)
        {
            float weight = math.saturate(math.isfinite(qualityWeight) ? qualityWeight : 0f);
            int frames = math.max(1, (int)math.round(math.lerp(4f, 1f, weight)));
            return frames > 1 && frame % (uint)frames != 0u;
        }

        private static float TicksToMicroseconds(long ticks)
        {
            return ticks <= 0L ? 0f : (float)(ticks * 1000000.0 / Stopwatch.Frequency);
        }

        private bool TryResolveGridCell(in AbsoluteUniversePosition sampleAup, out int x, out int y, out int z)
        {
            double3 origin = _gridOriginAup.ToAbsoluteDouble3();
            double3 sample = sampleAup.ToAbsoluteDouble3();
            double3 offset = sample - origin;
            float safeCellSize = math.max(0.5f, cellSizeMeters);
            int half = GridResolution >> 1;
            x = (int)math.floor(offset.x / safeCellSize) + half;
            y = (int)math.floor(offset.y / safeCellSize) + half;
            z = (int)math.floor(offset.z / safeCellSize) + half;
            return (uint)x < GridResolution && (uint)y < GridResolution && (uint)z < GridResolution;
        }

        private void DrainItemAcquiredSignals(PlayerRuntimeContext playerContext)
        {
            ApplyPendingIodineDoseReduction(playerContext);

            ReadOnlySpan<ItemAcquiredSignal> itemSignals = SignalBus<ItemAcquiredSignal>.GetFrameSnapshot();
            if (itemSignals.Length == 0)
                return;

            int frame = unchecked((int)_currentSimulationFrame);
            if (_lastItemSignalDrainFrame == frame)
                return;

            _lastItemSignalDrainFrame = frame;
            for (int i = 0; i < itemSignals.Length; i++)
            {
                ItemAcquiredSignal signal = itemSignals[i];
                if (signal.ItemHash != _iodineItemHash && signal.ItemHash != _iodineCapsItemHash)
                    continue;

                float quantity = signal.Quantity > 0 ? signal.Quantity : 1f;
                ApplyIodineDoseReduction(playerContext, IodineDoseReduction * quantity, in signal.PositionAup);
            }
        }

        private void DrainItemAcquiredSignalsDeferred()
        {
            ReadOnlySpan<ItemAcquiredSignal> itemSignals = SignalBus<ItemAcquiredSignal>.GetFrameSnapshot();
            if (itemSignals.Length == 0)
                return;

            int frame = unchecked((int)_currentSimulationFrame);
            if (_lastItemSignalDeferFrame == frame)
                return;

            _lastItemSignalDeferFrame = frame;
            _lastItemSignalDrainFrame = frame;
            for (int i = 0; i < itemSignals.Length; i++)
            {
                ItemAcquiredSignal signal = itemSignals[i];
                if (signal.ItemHash != _iodineItemHash && signal.ItemHash != _iodineCapsItemHash)
                    continue;

                float quantity = signal.Quantity > 0 ? signal.Quantity : 1f;
                _pendingIodineDoseReductionRad = math.max(0f, _pendingIodineDoseReductionRad + IodineDoseReduction * quantity);
            }
        }

        private void ApplyPendingIodineDoseReduction(PlayerRuntimeContext playerContext)
        {
            float pendingReduction = _pendingIodineDoseReductionRad;
            if (!(pendingReduction > 0f) || !math.isfinite(pendingReduction))
            {
                _pendingIodineDoseReductionRad = 0f;
                return;
            }

            _pendingIodineDoseReductionRad = 0f;
            AbsoluteUniversePosition doseAup = AbsoluteUniversePosition.IsFinite(in _lastSimulationPlayerAup)
                ? _lastSimulationPlayerAup
                : ResolvePlayerAup(playerContext);
            ApplyIodineDoseReduction(playerContext, pendingReduction, in doseAup);
        }

        private void ApplyIodineDoseReduction(PlayerRuntimeContext playerContext, float doseReductionRad, in AbsoluteUniversePosition doseAup)
        {
            if (!(doseReductionRad > 0f) || !math.isfinite(doseReductionRad))
                return;

            float doseReduction = doseReductionRad;
            float pendingReduction = math.min(_pendingExternalDoseRad, doseReduction);
            _pendingExternalDoseRad = math.max(0f, _pendingExternalDoseRad - pendingReduction);
            doseReduction = math.max(0f, doseReduction - pendingReduction);
            _accumulatedRadiationDose = math.max(0f, _accumulatedRadiationDose - doseReduction);
            if (_radiationStates.IsCreated && _radiationStates.Length > 0)
            {
                RadiationStateDTO state = _radiationStates[0];
                state.CumulativeDoseRad = _accumulatedRadiationDose;
                _radiationStates[0] = state;
            }

            ApplyDoseToPlayerContext(playerContext, _accumulatedRadiationDose, _lastGridIntensity01);
            if (AbsoluteUniversePosition.IsFinite(in doseAup))
                PublishDoseSignal(in doseAup, -doseReductionRad, _lastGridIntensity01, RadiationDoseAtmosphereKind);
        }

        private void DrainRadiationSourceSignals()
        {
            ReadOnlySpan<RadiationSourceSignal> sourceSignals = SignalBus<RadiationSourceSignal>.GetFrameSnapshot();
            if (sourceSignals.Length == 0)
                return;

            int frame = unchecked((int)_currentSimulationFrame);
            if (_lastSourceSignalDrainFrame == frame)
                return;

            _lastSourceSignalDrainFrame = frame;
            for (int i = 0; i < sourceSignals.Length; i++)
            {
                RadiationSourceSignal signal = sourceSignals[i];
                if (signal.SourceId == 0)
                    continue;

                if (signal.Operation == RadiationSourceSignal.OperationUpsert)
                    RegisterSourceInternal(signal.SourceId, in signal.PositionAup, signal.Intensity, signal.RadiusMeters);
                else
                    UnregisterSourceInternal(signal.SourceId);
            }
        }

        private void PreserveRadiationSourceSignalsForNextSimulation()
        {
            ReadOnlySpan<RadiationSourceSignal> sourceSignals = SignalBus<RadiationSourceSignal>.GetFrameSnapshot();
            if (sourceSignals.Length == 0)
                return;

            int frame = unchecked((int)_currentSimulationFrame);
            if (_lastSourceSignalPreserveFrame == frame)
                return;

            _lastSourceSignalPreserveFrame = frame;
            for (int i = 0; i < sourceSignals.Length; i++)
            {
                RadiationSourceSignal signal = sourceSignals[i];
                if (signal.SourceId == 0)
                    continue;

                SignalBus<RadiationSourceSignal>.Push(in signal);
            }
        }

        private void DrainExternalDoseSignals()
        {
            ReadOnlySpan<RadiationDoseSignal> doseSignals = SignalBus<RadiationDoseSignal>.GetFrameSnapshot();
            if (doseSignals.Length == 0)
                return;

            int frame = unchecked((int)_currentSimulationFrame);
            if (_lastExternalDoseSignalDrainFrame == frame)
                return;

            _lastExternalDoseSignalDrainFrame = frame;
            for (int i = 0; i < doseSignals.Length; i++)
            {
                RadiationDoseSignal signal = doseSignals[i];
                if (signal.SourceId != 0u || signal.DoseKind != RadiationDoseAtmosphereKind)
                    continue;

                if (!math.isfinite(signal.Dose) || !math.isfinite(signal.Intensity01))
                {
                    DumpBlackBox();
                    continue;
                }

                _pendingExternalDoseRad = math.max(0f, _pendingExternalDoseRad + math.max(0f, signal.Dose));
                _lastExternalIntensity01 = math.max(_lastExternalIntensity01, math.saturate(signal.Intensity01));
            }
        }

        private PlayerRuntimeContext ResolvePlayerRuntimeContext()
        {
            return PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext)
                ? runtimeContext
                : null;
        }

        private static AbsoluteUniversePosition ResolvePlayerAup(PlayerRuntimeContext playerContext)
        {
            if (playerContext != null)
            {
                var playerMovement = playerContext.PlayerMovement;
                if (playerMovement != null)
                {
                    AbsoluteUniversePosition currentAup = playerMovement.CurrentAup;
                    if (currentAup.IsFinite())
                        return currentAup;
                }

                AbsoluteUniversePosition predictedAup = playerContext.MovementState.PredictedAup;
                if (predictedAup.IsFinite())
                    return predictedAup;
            }

            return TryResolveAupFromRuntimeOrigin(Vector3.zero, out AbsoluteUniversePosition fallbackAup)
                ? fallbackAup
                : default;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!math.isfinite(runtimePosition.x) || !math.isfinite(runtimePosition.y) || !math.isfinite(runtimePosition.z))
                return false;

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            if (!AbsoluteUniversePosition.IsFinite(in originAup))
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return AbsoluteUniversePosition.IsFinite(in positionAup);
        }

        private void ApplyDoseToPlayerContext(PlayerRuntimeContext playerContext, float dose, float intensity01)
        {
            if (!math.isfinite(dose) || !math.isfinite(intensity01))
            {
                DumpBlackBox();
                dose = 0f;
                intensity01 = 0f;
            }

            if (playerContext == null)
                return;

            float safeDose = math.max(0f, dose);
            float penalty01 = math.saturate(1f - HectonPlayerHealth.ResolveRadiationFatigueScale(safeDose));
            playerContext.RadiationDose = safeDose;
            playerContext.RadiationIntensity01 = math.saturate(intensity01);
            playerContext.RadiationMaxHealthPenalty01 = penalty01;
            if (penalty01 > 0.0001f)
                playerContext.SurvivalState.StatusMask |= SurvivalStatusMasks.RadiationPenalty;
            else
                playerContext.SurvivalState.StatusMask &= ~SurvivalStatusMasks.RadiationPenalty;

            if (playerContext.PlayerHealth != null)
            {
                playerContext.PlayerHealth.SetRadiationExposure(safeDose);
                if (!playerContext.PlayerHealth.IsAlive && _lastCellularDegradation01 >= RadiationCriticalDegradation01)
                    DumpBlackBox();
            }
        }

        private void PublishDoseSignal(in AbsoluteUniversePosition positionAup, float dose, float intensity01, byte doseKind)
        {
            RadiationDoseSignal signal = new RadiationDoseSignal
            {
                PositionAup = positionAup,
                Dose = dose,
                Intensity01 = math.saturate(intensity01),
                SourceId = GeigerSourceId,
                DoseKind = doseKind,
                Flags = UsesSparseRadiationCadence(ResolveGlobalQualityWeight(), _currentSimulationFrame) ? (byte)1 : (byte)0
            };
            SignalBus<RadiationDoseSignal>.Push(in signal);
        }

        private void EmitGeigerIfNeeded(in AbsoluteUniversePosition playerAup, float intensity01)
        {
            float safeIntensity = math.saturate(intensity01);
            if (safeIntensity <= 0.001f)
            {
                _geigerPhase = 0f;
                return;
            }

            _geigerPhase += 0.2f + safeIntensity * 5f;
            if (_geigerPhase < 1f)
                return;

            _geigerPhase -= math.floor(_geigerPhase);
            _geigerLcg = unchecked(_geigerLcg * 1664525u + 1013904223u);
            float jitter01 = ((_geigerLcg >> 8) & 0x00FFFFFFu) * (1f / 16777215f);
            if (jitter01 > 0.35f + safeIntensity * 0.60f)
                return;

            AcousticPingSignal signal = new AcousticPingSignal
            {
                PositionAup = playerAup,
                RadiusMeters = 2f,
                Intensity01 = safeIntensity,
                SourceId = GeigerSourceId,
                Channel = GeigerAcousticChannel,
                Flags = 1
            };
            SignalBus<AcousticPingSignal>.Push(in signal);
        }

        private void PushVisualGlobals(float dose, float intensity01)
        {
            float safeDose = math.max(0f, dose);
            float exposureRate = math.max(0f, math.isfinite(intensity01) ? intensity01 : 0f);
            float safeIntensity = math.saturate(exposureRate);
            float mutation01 = math.saturate(math.max(_lastCellularDegradation01, safeDose * 0.01f));
            float static01 = exposureRate > 10f
                ? math.saturate(exposureRate * 0.01f)
                : (safeIntensity > StaticVfxThreshold ? safeIntensity : 0f);
            Shader.SetGlobalFloat(_HazardRadiationLevelId, safeIntensity);
            Shader.SetGlobalFloat(_HectonVisualStaticGlitchId, static01);
            Shader.SetGlobalFloat(_HectonVisualStaticGlitchSeedId, (_geigerLcg & 1023u) * (1f / 1023f));
            Shader.SetGlobalFloat(_HectonHandRadiationDoseId, safeDose);
            Shader.SetGlobalFloat(_HectonHandRadiationMutationId, mutation01);
            Shader.SetGlobalVector(_HectonHandRadiationTintId, new Vector4(0.65f, 1f, 0.42f, mutation01));
        }

        private int EncodeSparseRle(byte[] payload)
        {
            if (payload == null || payload.Length < RlePacketSizeBytes || !_gridRead.IsCreated)
                return 0;

            int cursor = 0;
            int cellIndex = 0;
            while (cellIndex < GridCellCount && cursor + RlePacketSizeBytes <= payload.Length)
            {
                byte value = QuantizeCell(_gridRead[cellIndex]);
                if (value == 0)
                {
                    cellIndex++;
                    continue;
                }

                int runStart = cellIndex;
                int runLength = 1;
                cellIndex++;
                while (cellIndex < GridCellCount && runLength < ushort.MaxValue)
                {
                    byte next = QuantizeCell(_gridRead[cellIndex]);
                    if (next != value)
                        break;

                    runLength++;
                    cellIndex++;
                }

                payload[cursor++] = (byte)(runStart & 0xFF);
                payload[cursor++] = (byte)((runStart >> 8) & 0xFF);
                payload[cursor++] = value;
                payload[cursor++] = (byte)(runLength & 0xFF);
                payload[cursor++] = (byte)((runLength >> 8) & 0xFF);
            }

            return cursor;
        }

        private void DecodeSparseRle(byte[] payload, int byteLength)
        {
            if (payload == null || !_gridRead.IsCreated)
                return;

            int safeLength = math.min(math.max(0, byteLength), payload.Length);
            int cursor = 0;
            while (cursor + RlePacketSizeBytes <= safeLength)
            {
                int runStart = payload[cursor] | (payload[cursor + 1] << 8);
                byte quantized = payload[cursor + 2];
                int runLength = payload[cursor + 3] | (payload[cursor + 4] << 8);
                cursor += RlePacketSizeBytes;
                if ((uint)runStart >= GridCellCount || runLength <= 0)
                    continue;

                float value = quantized * (1f / 127f);
                int end = math.min(GridCellCount, runStart + runLength);
                for (int i = runStart; i < end; i++)
                {
                    _gridRead[i] = value;
                    _gridWrite[i] = value;
                }
            }

            _gridVersion++;
        }

        private static void EnsureRleSaveBuffer(SaveData data)
        {
            if (data.radiationGridRle == null || data.radiationGridRle.Length < MaxRlePayloadBytes)
                data.radiationGridRle = new byte[MaxRlePayloadBytes];
        }

        private static byte QuantizeCell(float value)
        {
            if (!math.isfinite(value) || value <= 0f)
                return 0;

            return (byte)math.clamp((int)math.round(math.saturate(value) * 127f), 0, 127);
        }

        private void RecordTelemetry(in AbsoluteUniversePosition playerAup, float intensity01, float accumulatedRads, uint flags)
        {
            if (!_telemetryRing.IsCreated)
                return;

            double3 playerAbsolute = playerAup.ToAbsoluteDouble3();
            RadiationTelemetryEntry entry = new RadiationTelemetryEntry
            {
                PlayerAup = playerAbsolute,
                PlayerDepthMeters = (float)math.max(0d, -playerAbsolute.y),
                CurrentExposureRate = intensity01,
                CumulativeDoseRad = accumulatedRads,
                ShieldingFactor01 = _lastShieldingFactor01,
                CellularDegradation01 = _lastCellularDegradation01,
                BurstExecutionMicroseconds = _lastBurstExecutionMicroseconds,
                SourceCount = (ushort)math.clamp(_activeSourceCount, 0, ushort.MaxValue),
                SourceVersion = (ushort)math.clamp(_sourceVersion, 0, ushort.MaxValue),
                Frame = _currentSimulationFrame,
                ShiftSequence = _lastShiftSequence,
                Flags = flags
            };
            _telemetryRing[_telemetryWriteIndex % TelemetryCapacity] = entry;
            _telemetryWriteIndex++;
            if (_telemetryCursorLane.IsCreated && _telemetryCursorLane.Length > 0)
                _telemetryCursorLane[0] = unchecked((uint)_telemetryWriteIndex);
        }

        private void DumpBlackBox()
        {
            if (!_telemetryRing.IsCreated)
                return;

            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", RadiationDumpFileName));
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(_telemetryWriteIndex);
                    writer.Write(TelemetryCapacity);
                    for (int i = 0; i < TelemetryCapacity; i++)
                    {
                        RadiationTelemetryEntry entry = _telemetryRing[i];
                        writer.Write(entry.PlayerAup.x);
                        writer.Write(entry.PlayerAup.y);
                        writer.Write(entry.PlayerAup.z);
                        writer.Write(entry.PlayerDepthMeters);
                        writer.Write(entry.CurrentExposureRate);
                        writer.Write(entry.CumulativeDoseRad);
                        writer.Write(entry.ShieldingFactor01);
                        writer.Write(entry.CellularDegradation01);
                        writer.Write(entry.BurstExecutionMicroseconds);
                        writer.Write(entry.SourceCount);
                        writer.Write(entry.SourceVersion);
                        writer.Write(entry.Frame);
                        writer.Write(entry.ShiftSequence);
                        writer.Write(entry.Flags);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !_sources.IsCreated)
                return;

            AbsoluteUniversePosition playerAup = ResolvePlayerAup(ResolvePlayerRuntimeContext());
            float3 playerRuntime = playerAup.ToRuntimeFloat3();
            NativeArray<BulkheadStateDTO> bulkheadStates = default;
            NativeArray<BulkheadPlaneDTO> bulkheadPlanes = default;
            ResolveBulkheadReadBuffers(ref bulkheadStates, ref bulkheadPlanes);
            int bulkheadCount = bulkheadStates.IsCreated && bulkheadPlanes.IsCreated
                ? math.min(bulkheadStates.Length, bulkheadPlanes.Length)
                : 0;

            for (int i = 0; i < _sources.Length; i++)
            {
                RadiationSource source = _sources[i];
                if (source.Active == 0)
                    continue;

                float3 sourceRuntime = ResolveRuntimeFromAbsolute(source.PositionAup);
                float radius = math.max(0.5f, source.RadiusMeters);
                Gizmos.color = new Color(1f, 0.18f, 0.05f, 0.35f);
                Gizmos.DrawWireSphere(ToVector3(sourceRuntime), radius);

                bool shielded = DoesRayHitBulkheadForGizmo(
                    source.PositionAup,
                    playerAup.ToAbsoluteDouble3(),
                    bulkheadStates,
                    bulkheadPlanes,
                    bulkheadCount);
                Gizmos.color = shielded
                    ? new Color(0.15f, 1f, 0.35f, 0.9f)
                    : new Color(1f, 0.05f, 0.02f, 0.9f);
                Gizmos.DrawLine(ToVector3(sourceRuntime), ToVector3(playerRuntime));
            }
        }

        private static bool DoesRayHitBulkheadForGizmo(
            double3 sourceAup,
            double3 playerAup,
            NativeArray<BulkheadStateDTO> states,
            NativeArray<BulkheadPlaneDTO> planes,
            int count)
        {
            if (!states.IsCreated || !planes.IsCreated)
                return false;

            double3 segment = playerAup - sourceAup;
            int safeCount = math.min(count, math.min(states.Length, planes.Length));
            for (int i = 0; i < safeCount; i++)
            {
                BulkheadStateDTO state = states[i];
                if ((state.Flags & BulkheadStateFlags.Active) == 0u ||
                    (state.Flags & BulkheadStateFlags.Destroyed) != 0u ||
                    state.ClosureProgress <= 0.001f)
                {
                    continue;
                }

                BulkheadPlaneDTO plane = planes[i];
                float3 normal = BulkheadContainmentMath.SafeNormal(plane.Normal, new float3(0f, 0f, 1f));
                double denom = math.dot(segment, (double3)normal);
                if (math.abs((float)denom) < 0.0001f)
                    continue;

                double t = math.dot(plane.CenterAup - sourceAup, (double3)normal) / denom;
                if (t <= 0.0d || t >= 1.0d)
                    continue;

                double3 hitAup = sourceAup + segment * t;
                float3 local = (float3)(hitAup - plane.CenterAup);
                float3 tangentSeed = math.abs(normal.y) < 0.9f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);
                float3 tangent = BulkheadContainmentMath.SafeNormal(math.cross(tangentSeed, normal), new float3(1f, 0f, 0f));
                float3 bitangent = math.cross(normal, tangent);
                if (math.abs(math.dot(local, tangent)) <= math.max(0.05f, plane.WidthMeters * 0.5f) &&
                    math.abs(math.dot(local, bitangent)) <= math.max(0.05f, plane.HeightMeters * 0.5f))
                {
                    return true;
                }
            }

            return false;
        }

        private static float3 ResolveRuntimeFromAbsolute(double3 absolute)
        {
            AbsoluteUniversePosition origin = GlobalSignals.CurrentRuntimeOriginAup();
            double3 originAbsolute = AbsoluteUniversePosition.IsFinite(in origin) ? origin.ToAbsoluteDouble3() : double3.zero;
            return (float3)(absolute - originAbsolute);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static void ClearGrid(NativeArray<float> grid)
        {
            if (!grid.IsCreated)
                return;

            for (int i = 0; i < grid.Length; i++)
                grid[i] = 0f;
        }

        private static float NormalizeSourceIntensity(float intensity)
        {
            if (!math.isfinite(intensity) || intensity <= 0f)
                return 0f;

            return math.min(intensity, 1000000f);
        }

        private static int Flatten(int x, int y, int z)
        {
            return x + y * GridResolution + z * GridResolution * GridResolution;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        public struct RadiationStateDTO
        {
            [FieldOffset(0)] public float CumulativeDoseRad;
            [FieldOffset(4)] public float CurrentExposureRate;
            [FieldOffset(8)] public float ShieldingFactor01;
            [FieldOffset(12)] public float CellularDegradation01;
            [FieldOffset(16)] public uint EntityHashID;
            [FieldOffset(20)] public uint Flags;
            [FieldOffset(24)] public byte _pad0;
            [FieldOffset(25)] public byte _pad1;
            [FieldOffset(26)] public byte _pad2;
            [FieldOffset(27)] public byte _pad3;
            [FieldOffset(28)] public byte _pad4;
            [FieldOffset(29)] public byte _pad5;
            [FieldOffset(30)] public byte _pad6;
            [FieldOffset(31)] public byte _pad7;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        public struct RadiationTuningDTO
        {
            [FieldOffset(0)] public float DoseToDegradationScale;
            [FieldOffset(4)] public float DecayPerTick;
            [FieldOffset(8)] public float DamagePerTickScale;
            [FieldOffset(12)] public float LeadShieldingEffectiveness;
            [FieldOffset(16)] public int MaxSdfSamples;
            [FieldOffset(20)] public uint Flags;
            [FieldOffset(24)] public uint _pad0;
            [FieldOffset(28)] public uint _pad1;

        }

        private static RadiationTuningDTO CreateDefaultRadiationTuning()
        {
            return new RadiationTuningDTO
            {
                DoseToDegradationScale = 0.01f,
                DecayPerTick = DoseDecayPerSimulationStep,
                DamagePerTickScale = RadiationDamagePerTickScale,
                LeadShieldingEffectiveness = 1f,
                MaxSdfSamples = 12,
                Flags = 1u
            };
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct RadiationProfileDTO
        {
            [FieldOffset(0)] public uint ProfileHash;
            [FieldOffset(4)] public float IntensityScale;
            [FieldOffset(8)] public float RadiusMeters;
            [FieldOffset(12)] public float ShieldAttenuation01;
            [FieldOffset(16)] public float MutationScale;
            [FieldOffset(20)] public uint Flags;
            [FieldOffset(24)] public uint _pad0;
            [FieldOffset(28)] public uint _pad1;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct RadiationSource
        {
            [FieldOffset(0)] public double3 PositionAup;
            [FieldOffset(24)] public float Intensity01;
            [FieldOffset(28)] public float RadiusMeters;
            [FieldOffset(32)] public int SourceId;
            [FieldOffset(36)] public uint ProfileHash;
            [FieldOffset(40)] public uint Flags;
            [FieldOffset(44)] public byte Active;
            [FieldOffset(45)] public byte _pad0;
            [FieldOffset(46)] public ushort _pad1;
            [FieldOffset(48)] public ulong _pad2;
            [FieldOffset(56)] public ulong _pad3;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct RadiationTelemetryEntry
        {
            [FieldOffset(0)] public double3 PlayerAup;
            [FieldOffset(24)] public float PlayerDepthMeters;
            [FieldOffset(28)] public float CurrentExposureRate;
            [FieldOffset(32)] public float CumulativeDoseRad;
            [FieldOffset(36)] public float ShieldingFactor01;
            [FieldOffset(40)] public float CellularDegradation01;
            [FieldOffset(44)] public float BurstExecutionMicroseconds;
            [FieldOffset(48)] public uint Frame;
            [FieldOffset(52)] public uint ShiftSequence;
            [FieldOffset(56)] public ushort SourceCount;
            [FieldOffset(58)] public ushort SourceVersion;
            [FieldOffset(60)] public uint Flags;
        }

        public static class RadiationStateLayoutGuard
        {
            public const int StateSizeBytes = 32;
            public const int TelemetrySizeBytes = 64;

            public static bool ValidateLayout()
            {
                bool sizesValid = UnsafeUtility.SizeOf<RadiationStateDTO>() == StateSizeBytes &&
                                  UnsafeUtility.SizeOf<RadiationTelemetryEntry>() == TelemetrySizeBytes;
#if UNITY_EDITOR
                return sizesValid &&
                       GetOffset<RadiationStateDTO>(nameof(RadiationStateDTO.CumulativeDoseRad)) == 0 &&
                       GetOffset<RadiationStateDTO>(nameof(RadiationStateDTO.CurrentExposureRate)) == 4 &&
                       GetOffset<RadiationStateDTO>(nameof(RadiationStateDTO.ShieldingFactor01)) == 8 &&
                       GetOffset<RadiationStateDTO>(nameof(RadiationStateDTO.CellularDegradation01)) == 12 &&
                       GetOffset<RadiationStateDTO>(nameof(RadiationStateDTO.EntityHashID)) == 16 &&
                       GetOffset<RadiationStateDTO>(nameof(RadiationStateDTO.Flags)) == 20 &&
                       GetOffset<RadiationStateDTO>(nameof(RadiationStateDTO._pad0)) == 24 &&
                       GetOffset<RadiationStateDTO>(nameof(RadiationStateDTO._pad7)) == 31;
#else
                return sizesValid;
#endif
            }

#if UNITY_EDITOR
            private static int GetOffset<T>(string fieldName) where T : struct
            {
                return (int)UnsafeUtility.GetFieldOffset(typeof(T).GetField(fieldName));
            }
#endif
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct GenerateMockRadiationSourceJob : IJob
        {
            [NativeDisableUnsafePtrRestriction, NoAlias] public RadiationSource* Sources;
            [NativeDisableUnsafePtrRestriction, NoAlias] public int* SourceCount;
            public int Capacity;
            public double3 PlayerAup;
            public float3 OffsetMeters;
            public float Intensity01;
            public float RadiusMeters;
            public int SourceId;

            public void Execute()
            {
                if (Sources == null || SourceCount == null || Capacity <= 0)
                    return;

                Sources[0] = new RadiationSource
                {
                    PositionAup = PlayerAup + (double3)OffsetMeters,
                    Intensity01 = math.max(0f, Intensity01),
                    RadiusMeters = math.max(0.5f, RadiusMeters),
                    SourceId = SourceId,
                    Active = 1
                };
                SourceCount[0] = math.max(SourceCount[0], 1);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct CalculateRadiationExposureJob : IJob
        {
            [NativeDisableUnsafePtrRestriction, NoAlias] public RadiationStateDTO* States;
            [NativeDisableUnsafePtrRestriction, NoAlias] public RadiationSource* Sources;
            [NativeDisableUnsafePtrRestriction, NoAlias] public CombatDamageSignal* DamageSignal;
            [ReadOnly, NoAlias] public NativeArray<byte> EncodedSdf;
            [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadStateDTO* BulkheadStates;
            [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadPlaneDTO* BulkheadPlanes;
            public int SourceCapacity;
            public int ActiveSourceCount;
            public int BulkheadCount;
            public double3 PlayerAup;
            public float3 PlayerRuntime;
            public float SimulationTickDelta;
            public float ExternalExposureRate;
            public float ExternalDoseDelta;
            public float DoseDecayPerTick;
            public float DoseToDegradationScale;
            public float DamagePerTickScale;
            public float LeadShieldingEffectiveness;
            public uint PlayerTargetId;
            public uint Frame;
            public int3 SdfDimensions;
            public float3 SdfVolumeOrigin;
            public float3 SdfCellSize;
            public float SdfRange;
            public int SdfSampleCount;

            public void Execute()
            {
                if (States == null || Sources == null || DamageSignal == null)
                    return;

                RadiationStateDTO previous = States[0];
                float exposure = SanitizeNonNegative(ExternalExposureRate);
                float integratedExposure = 0f;
                float shielding = 0f;
                uint flags = exposure > 0.0001f ? RadiationStateFlagIrradiated : 0u;
                float safeDoseDecay = math.saturate(SanitizeNonNegative(DoseDecayPerTick));
                float safeDoseScale = math.max(0.0001f, SanitizeNonNegative(DoseToDegradationScale));
                float safeDamageScale = SanitizeNonNegative(DamagePerTickScale);
                float safeLeadEffect = math.saturate(SanitizeNonNegative(LeadShieldingEffectiveness));
                int capacity = math.clamp(SourceCapacity, 0, MaxSourceCount);
                int activeSeen = 0;
                for (int i = 0; i < capacity; i++)
                {
                    RadiationSource source = Sources[i];
                    if (source.Active == 0 || source.Intensity01 <= 0f)
                        continue;

                    if (!math.all(math.isfinite(source.PositionAup)) ||
                        !math.isfinite(source.Intensity01) ||
                        !math.isfinite(source.RadiusMeters))
                    {
                        flags |= RadiationStateFlagNonFinite;
                        continue;
                    }

                    activeSeen++;
                    double3 sourceToPlayer = PlayerAup - source.PositionAup;
                    if (!math.all(math.isfinite(sourceToPlayer)))
                    {
                        flags |= RadiationStateFlagNonFinite;
                        continue;
                    }

                    double distanceSqDouble = math.lengthsq(sourceToPlayer);
                    if (!math.isfinite(distanceSqDouble))
                    {
                        flags |= RadiationStateFlagNonFinite;
                        continue;
                    }

                    float distanceSq = (float)math.max(0.0001d, distanceSqDouble);
                    float sourceExposure = SanitizeNonNegative(source.Intensity01) * math.rcp(math.max(distanceSq, 0.0001f));
                    uint sourceShieldingFlags = 0u;
                    float sourceShielding = CalculateSourceShielding(source.PositionAup, PlayerAup, PlayerRuntime, ref sourceShieldingFlags) * safeLeadEffect;
                    flags |= sourceShieldingFlags;
                    shielding = math.max(shielding, sourceShielding);
                    float unshieldedSourceExposure = sourceExposure * (1f - sourceShielding);
                    exposure += unshieldedSourceExposure;
                    integratedExposure += unshieldedSourceExposure;
                    if (activeSeen >= ActiveSourceCount && ActiveSourceCount > 0)
                        break;
                }

                shielding = math.saturate(shielding);
                float stepSeconds = SanitizeNonNegative(SimulationTickDelta);
                float externalDose = SanitizeNonNegative(ExternalDoseDelta);
                float previousDose = SanitizeNonNegative(previous.CumulativeDoseRad);
                float decay = math.saturate(1f - (1f - safeDoseDecay) * stepSeconds * 60f);
                float cumulative = math.max(0f, (previousDose + integratedExposure * stepSeconds + externalDose) * decay);
                float degradation = math.saturate(cumulative * safeDoseScale);
                if (shielding > 0.0001f)
                    flags |= RadiationStateFlagShielded;
                if (degradation >= RadiationCriticalDegradation01)
                    flags |= RadiationStateFlagCritical;
                if (degradation >= 0.01f)
                    flags |= RadiationStateFlagMutated;
                if (!math.isfinite(exposure) || !math.isfinite(cumulative) || !math.isfinite(degradation))
                {
                    exposure = 0f;
                    cumulative = 0f;
                    degradation = 0f;
                    shielding = 0f;
                    flags |= RadiationStateFlagNonFinite;
                }

                States[0] = new RadiationStateDTO
                {
                    CumulativeDoseRad = cumulative,
                    CurrentExposureRate = math.max(0f, exposure),
                    ShieldingFactor01 = shielding,
                    CellularDegradation01 = degradation,
                    EntityHashID = RadiationSystemHash,
                    Flags = flags
                };

                if (degradation > RadiationCriticalDegradation01 && PlayerTargetId != 0u)
                {
                    DamageSignal[0] = new CombatDamageSignal
                    {
                        ImpactAup = PlayerAup,
                        Direction = new float3(0f, 1f, 0f),
                        Magnitude = math.max(0f, (degradation - RadiationCriticalDegradation01) * safeDamageScale),
                        DamageType = CombatDamageTypes.Radioactive,
                        TargetHash = PlayerTargetId,
                        SourceHash = RadiationSystemHash,
                        Frame = Frame,
                        SourceId = RadiationCombatSourceId,
                        TargetId = (ushort)math.min(PlayerTargetId, 65535u),
                        Channel = GeigerAcousticChannel,
                        Flags = CombatDamageSignal.DirectRuntimeFlag
                    };
                }
                else
                {
                    DamageSignal[0] = default;
                }
            }

            private static float SanitizeNonNegative(float value)
            {
                return math.isfinite(value) ? math.max(0f, value) : 0f;
            }

            private static float Sanitize01(float value)
            {
                return math.isfinite(value) ? math.saturate(value) : 0f;
            }

            private float CalculateSourceShielding(double3 sourceAup, double3 playerAup, float3 playerRuntime, ref uint flags)
            {
                float bulkheadShielding = CalculateBulkheadShielding(sourceAup, playerAup);
                if (bulkheadShielding > 0.0001f)
                    flags |= RadiationStateFlagBulkheadShielded;
                if (bulkheadShielding >= 0.999f)
                    return 1f;

                float sdfShielding = CalculateSdfShielding(sourceAup, playerAup, playerRuntime);
                if (sdfShielding > 0.0001f)
                    flags |= RadiationStateFlagSdfShielded;
                return math.max(bulkheadShielding, sdfShielding);
            }

            private float CalculateBulkheadShielding(double3 sourceAup, double3 playerAup)
            {
                if (BulkheadStates == null || BulkheadPlanes == null || BulkheadCount <= 0)
                    return 0f;

                double3 segment = playerAup - sourceAup;
                if (!math.all(math.isfinite(segment)))
                    return 0f;

                float shielding = 0f;
                int count = math.clamp(BulkheadCount, 0, 256);
                for (int i = 0; i < count; i++)
                {
                    BulkheadStateDTO state = BulkheadStates[i];
                    if ((state.Flags & BulkheadStateFlags.Active) == 0u ||
                        (state.Flags & BulkheadStateFlags.Destroyed) != 0u)
                    {
                        continue;
                    }

                    BulkheadPlaneDTO plane = BulkheadPlanes[i];
                    if (!math.all(math.isfinite(plane.CenterAup)) ||
                        !math.all(math.isfinite(plane.Normal)))
                    {
                        continue;
                    }

                    float3 normal = BulkheadContainmentMath.SafeNormal(plane.Normal, new float3(0f, 0f, 1f));
                    double denom = math.dot(segment, (double3)normal);
                    if (math.abs((float)denom) < 0.0001f)
                        continue;

                    double t = math.dot(plane.CenterAup - sourceAup, (double3)normal) / denom;
                    if (t <= 0.0d || t >= 1.0d)
                        continue;

                    double3 hitAup = sourceAup + segment * t;
                    float3 local = (float3)(hitAup - plane.CenterAup);
                    float3 tangentSeed = math.abs(normal.y) < 0.9f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);
                    float3 tangent = BulkheadContainmentMath.SafeNormal(math.cross(tangentSeed, normal), new float3(1f, 0f, 0f));
                    float3 bitangent = math.cross(normal, tangent);
                    float halfWidth = math.max(0.05f, SanitizeNonNegative(plane.WidthMeters) * 0.5f);
                    float halfHeight = math.max(0.05f, SanitizeNonNegative(plane.HeightMeters) * 0.5f);
                    if (math.abs(math.dot(local, tangent)) > halfWidth ||
                        math.abs(math.dot(local, bitangent)) > halfHeight)
                    {
                        continue;
                    }

                    float closure = Sanitize01(state.ClosureProgress);
                    if ((state.Flags & BulkheadStateFlags.Sealed) != 0u)
                        closure = 1f;
                    shielding = math.max(shielding, closure);
                }

                return math.saturate(shielding);
            }

            private float CalculateSdfShielding(double3 sourceAup, double3 playerAup, float3 playerRuntime)
            {
                if (!EncodedSdf.IsCreated ||
                    EncodedSdf.Length == 0 ||
                    SdfSampleCount <= 0 ||
                    SdfDimensions.x <= 0 ||
                    SdfDimensions.y <= 0 ||
                    SdfDimensions.z <= 0)
                {
                    return 0f;
                }

                double3 segment = playerAup - sourceAup;
                if (!math.all(math.isfinite(segment)) || !math.all(math.isfinite(playerRuntime)))
                    return 0f;

                float3 sourceRuntime = playerRuntime - (float3)segment;
                if (!math.all(math.isfinite(sourceRuntime)))
                    return 0f;

                int samples = math.clamp(SdfSampleCount, 2, 24);
                float shielding = 0f;
                for (int i = 1; i <= samples; i++)
                {
                    float t = i * math.rcp(samples + 1f);
                    float3 sample = math.lerp(sourceRuntime, playerRuntime, t);
                    float density = SampleSdfDensity(sample);
                    shielding = math.max(shielding, math.saturate((density - 0.45f) * 2.2f));
                    if (shielding >= 0.999f)
                        return 1f;
                }

                return shielding;
            }

            private float SampleSdfDensity(float3 runtimePosition)
            {
                if (!math.all(math.isfinite(runtimePosition)) ||
                    !math.all(math.isfinite(SdfVolumeOrigin)) ||
                    !math.all(math.isfinite(SdfCellSize)) ||
                    !math.isfinite(SdfRange))
                {
                    return 0f;
                }

                float3 local = runtimePosition - SdfVolumeOrigin;
                float3 safeCellSize = math.max(math.abs(SdfCellSize), new float3(0.001f));
                float3 grid = local * math.rcp(safeCellSize);
                if (!math.all(math.isfinite(grid)))
                    return 0f;

                int ix = (int)math.floor(grid.x);
                int iy = (int)math.floor(grid.y);
                int iz = (int)math.floor(grid.z);
                if ((uint)ix >= (uint)SdfDimensions.x ||
                    (uint)iy >= (uint)SdfDimensions.y ||
                    (uint)iz >= (uint)SdfDimensions.z)
                {
                    return 0f;
                }

                int index = ix + SdfDimensions.x * (iy + SdfDimensions.y * iz);
                if ((uint)index >= (uint)EncodedSdf.Length)
                    return 0f;

                float normalized = EncodedSdf[index] * math.rcp(255f);
                float signedDistance = (normalized * 2f - 1f) * math.max(0.001f, SdfRange);
                return math.saturate(0.5f - signedDistance * math.rcp(math.max(0.001f, SdfRange)));
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct RadiationJacobiDiffusionJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<float> Previous;
            [ReadOnly, NoAlias] public NativeArray<float> Sources;
            [WriteOnly, NoAlias] public NativeArray<float> Next;
            public int Width;
            public int Height;
            public int Depth;

            public void Execute(int index)
            {
                int plane = Width * Height;
                int z = index / plane;
                int rem = index - z * plane;
                int y = rem / Width;
                int x = rem - y * Width;

                float self = Previous[index];
                float left = Previous[FlattenLocal(math.max(0, x - 1), y, z)];
                float right = Previous[FlattenLocal(math.min(Width - 1, x + 1), y, z)];
                float down = Previous[FlattenLocal(x, math.max(0, y - 1), z)];
                float up = Previous[FlattenLocal(x, math.min(Height - 1, y + 1), z)];
                float back = Previous[FlattenLocal(x, y, math.max(0, z - 1))];
                float forward = Previous[FlattenLocal(x, y, math.min(Depth - 1, z + 1))];
                float next = (self + left + right + down + up + back + forward) * 0.16f;
                next = math.max(next, Sources[index]);
                Next[index] = math.isfinite(next) ? math.saturate(next) : 0f;
            }

            private int FlattenLocal(int x, int y, int z)
            {
                return x + y * Width + z * Width * Height;
            }
        }

        private sealed class SimulationPhaseSystem : IDispatcherSystem
        {
            private const uint SystemHash = 0x53483274u;
            private readonly RadiationHazardGrid _owner;

            public SimulationPhaseSystem(RadiationHazardGrid owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => SystemHash;
            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.Simulation;
            public byte GetBucketId() => byte.MaxValue;
            public int GetDependencyCount() => 0;
            public uint GetDependencyHash(int dependencyIndex) => 0u;
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn)
            {
                return _owner != null ? _owner.ScheduleRadiationSimulation(in timing, in context, dependsOn) : dependsOn;
            }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }

        private sealed class PostSimulationPhaseSystem : IDispatcherSystem
        {
            private const uint SystemHash = 0x53483275u;
            private readonly RadiationHazardGrid _owner;

            public PostSimulationPhaseSystem(RadiationHazardGrid owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => SystemHash;
            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.PostSimulation;
            public byte GetBucketId() => byte.MaxValue;
            public int GetDependencyCount() => 0;
            public uint GetDependencyHash(int dependencyIndex) => 0u;
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) => dependsOn;
            public void PostSimulationTick(in DispatcherTimingDTO timing)
            {
                if (_owner != null)
                    _owner.PostSimulationRadiation(in timing);
            }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }

        private sealed class VisualSyncPhaseSystem : IDispatcherSystem
        {
            private const uint SystemHash = 0x53483276u;
            private readonly RadiationHazardGrid _owner;

            public VisualSyncPhaseSystem(RadiationHazardGrid owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => SystemHash;
            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.VisualSync;
            public byte GetBucketId() => byte.MaxValue;
            public int GetDependencyCount() => 0;
            public uint GetDependencyHash(int dependencyIndex) => 0u;
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) => dependsOn;
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing)
            {
                if (_owner != null)
                    _owner.VisualSyncRadiation(in timing);
            }
        }
    }
}
