using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Physiology;
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
using UnityEngine.Serialization;
using Stopwatch = System.Diagnostics.Stopwatch;
using VoxelSdfReadModel = Hecton8.Core.Contracts.IVoxelSonarSdfReadModel;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Radiation Hazard Grid")]
    public sealed unsafe class RadiationHazardGrid : MonoBehaviour, ISlowTickable, IOriginShiftListener, ISaveable, IGlobalRegistryHotSwapListener
    {
        private static int _signalPushDropCount;
        public const int GridResolution = SaveData.RadiationGridResolution;
        public const int GridCellCount = SaveData.RadiationGridCellCount;
        public const int MaxSourceCount = 64;
        public const int TelemetryCapacity = 300;
        public const int RlePacketSizeBytes = SaveData.RadiationGridRlePacketSizeBytes;
        public const int MaxRlePayloadBytes = SaveData.RadiationGridRleMaxBytes;

        private const string NativeMemoryOwner = nameof(RadiationHazardGrid);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        private const float DoseDecayPerSimulationStep = 0.999f;
        private const float RadiationSlowTickIntervalSeconds = 0.1f;
        private const float DefaultCellSizeMeters = SaveData.RadiationGridDefaultCellSizeMeters;
        private const float MinCellSizeMeters = SaveData.RadiationGridMinCellSizeMeters;
        private const float MaxCellSizeMeters = SaveData.RadiationGridMaxCellSizeMeters;
        private const float DefaultSourceRadiusMeters = 18f;
        private const float DefaultSeaLevelY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
        private const float MaxSourceRadiusMeters = SaveData.RadiationGridMaxCellSizeMeters * GridResolution;
        private const float StaticVfxThreshold = 0.5f;
        private const float IodineDoseReduction = 50f;
        private const uint GeigerSourceId = 0x52414447u;
        private const byte GeigerAcousticChannel = 9;
        private const byte RadiationDoseGridKind = 1;
        private const byte RadiationDoseAtmosphereKind = 2;
        private const int EmergencyMockSourceId = unchecked((int)0x53483237u);
        private const int RadiationEntitySlotCount = 1;
        private const int RadiationProfileCapacity = 16;
        private const float RadiationCriticalDegradation01 = 0.5f;
        private const float RadiationStatusMagnitudeScale = 0.08f;
        private const float RadiationCriticalStatusDurationSeconds = 2.0f;
        private const uint RadiationStateFlagIrradiated = 1u << 0;
        private const uint RadiationStateFlagMutated = 1u << 1;
        private const uint RadiationStateFlagCritical = 1u << 2;
        private const uint RadiationStateFlagShielded = 1u << 3;
        private const uint RadiationStateFlagSdfShielded = 1u << 4;
        private const uint RadiationStateFlagBulkheadShielded = 1u << 5;
        private const uint RadiationStateFlagNonFinite = 1u << 31;
        private const uint RadiationTelemetryFlagSkippedEvaluation = 1u << 0;
        private const uint RadiationTelemetryFlagOriginShift = 1u << 1;
        private const uint RadiationTelemetryFlagSourceOverflow = 1u << 2;
        private const uint RadiationTelemetryFlagSourceOverflowReplaced = 1u << 3;
        private const uint RadiationTelemetryFlagSignalDrops = 1u << 4;
        private const uint RadiationTelemetryFlagJobActive = 1u << 5;
        private const uint RadiationSystemHash = 0x53483237u;
        private const ushort RadiationCombatSourceId = 274;
        private const BufferID RadiationStatusSignalBuffer = BufferID.Shinobu274RadiationDamageSignal;
        private const BufferID RadiationSdfSnapshotBuffer = BufferID.RadiationHazardGrid_RadiationSdfSnapshotBuffer;
        private const SystemID OwnerSystemId = SystemID.GameplayRadiation;
        private const string RadiationDumpFileName = "Dump_SHINOBU_274.bin";
        private static readonly ulong RadiationSdfSnapshotMutationGuardMask = RadiationMutationGuardBit(RadiationSdfSnapshotBuffer);

        private static readonly uint _iodineItemHash = H8DataHash.ComputeFnv1A32("iodine");
        private static readonly uint _iodineCapsItemHash = H8DataHash.ComputeFnv1A32("Iodine");
        private static readonly int _HazardRadiationLevelId = Shader.PropertyToID("_HazardRadiationLevel");
        private static readonly int _HectonVisualStaticGlitchId = Shader.PropertyToID("_HectonVisualStaticGlitch");
        private static readonly int _HectonVisualStaticGlitchSeedId = Shader.PropertyToID("_HectonVisualStaticGlitchSeed");
        private static readonly int _HectonHandRadiationDoseId = Shader.PropertyToID("_HectonHandRadiationDose");
        private static readonly int _HectonHandRadiationMutationId = Shader.PropertyToID("_HectonHandRadiationMutation01");
        private static readonly int _HectonHandRadiationTintId = Shader.PropertyToID("_HectonHandRadiationTint");
        internal static RadiationHazardGrid ActiveRuntimeInstance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticRuntimeState()
        {
            ActiveRuntimeInstance = null;
            Volatile.Write(ref _signalPushDropCount, 0);
        }

        private struct VaultBufferView<T> where T : struct
        {
            private IDataVault _vault;
            private VaultGenerationHandle<T> _handle;

            public static VaultBufferView<T> Create(IDataVault vault, VaultGenerationHandle<T> handle)
            {
                if (vault == null || handle.BufferID == 0u)
                    handle = default;

                return new VaultBufferView<T>
                {
                    _vault = vault,
                    _handle = handle
                };
            }

            public bool IsCreated => TryResolve(out _);

            public int Length => TryResolve(out NativeArray<T> buffer) ? buffer.Length : 0;

            public T this[int index]
            {
                get
                {
                    NativeArray<T> buffer = Resolve();
                    return buffer[index];
                }
                set
                {
                    NativeArray<T> buffer = Resolve();
                    buffer[index] = value;
                }
            }

            public NativeArray<T> Resolve()
            {
                return TryResolve(out NativeArray<T> buffer) ? buffer : default;
            }

            private bool TryResolve(out NativeArray<T> buffer)
            {
                if (_vault != null &&
                    _handle.BufferID != 0u &&
                    _handle.Generation != 0u &&
                    _vault.TryResolveHandle(in _handle, out buffer) &&
                    buffer.IsCreated)
                {
                    return true;
                }

                buffer = default;
                return false;
            }

            public static implicit operator NativeArray<T>(VaultBufferView<T> view)
            {
                return view.Resolve();
            }
        }

        [SerializeField, Min(MinCellSizeMeters)] private float cellSizeMeters = DefaultCellSizeMeters;
        [FormerlySerializedAs("doseScalePerFrostTick")]
        [SerializeField, Min(0f)] private float doseScalePerSimulationSecond = 1f;
        [SerializeField] private TextAsset radiationProfilesCsv;
        [SerializeField] private bool enableEmergencyMockSource;
        [SerializeField, Min(0f)] private float emergencyMockIntensity = 80f;
        [SerializeField] private Vector3 emergencyMockOffsetMeters = new Vector3(8f, 0f, 0f);

        private VaultBufferView<float> _gridRead;
        private VaultBufferView<float> _gridWrite;
        private VaultBufferView<float> _gridSource;
        private VaultBufferView<RadiationStateDTO> _radiationStates;
        private VaultBufferView<RadiationSource> _sources;
        private VaultBufferView<RadiationTelemetryEntry> _telemetryRing;
        private VaultBufferView<int> _sourceCountLane;
        private VaultBufferView<uint> _telemetryCursorLane;
        private VaultBufferView<RadiationProfileDTO> _profiles;
        private VaultBufferView<RadiationTuningDTO> _tuningLane;
        private VaultBufferView<RadiationStatusSignal> _statusSignalLane;
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
        private VaultGenerationHandle<RadiationTuningDTO> _tuningHandle;
        private VaultGenerationHandle<RadiationStatusSignal> _statusSignalHandle;
        private VaultGenerationHandle<byte> _radiationSdfSnapshotHandle;
        private VaultGenerationHandle<BulkheadStateDTO> _bulkheadStatesReadHandle;
        private VaultGenerationHandle<BulkheadPlaneDTO> _bulkheadPlanesReadHandle;
        private AbsoluteUniversePosition _gridOriginAup;
        private IDataVault _dataVault;
        private VoxelSdfReadModel _voxelSdfReadModel;
        private IVoxelSonarSdfReadLeaseModel _voxelSdfReadLeaseModel;
        private IHectonOceanKinematicsService _oceanKinematicsService;
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
        private bool _radiationSdfSnapshotLocked;
        private bool _radiationEvaluatedThisFrame;
        private bool _gridBuffersSwapped;
        private bool _registeredSimulationPhase;
        private bool _registeredPostSimulationPhase;
        private bool _registeredVisualSyncPhase;
        private bool _registeredSlowTick;
        private bool _registeredOriginShift;
        private bool _registeredSave;
        private bool _registeredHotSwapListener;
        private bool _pendingLoadDataValid;
        private bool _pendingDataVaultSwap;
        private bool _blackBoxDumpAttempted;
        private ISaveService _saveService;
        private ISaveService _registeredSaveService;

        public int SavePriority => 54;
        public int LoadPriority => 54;

        public static void RegisterSource(int sourceId, Vector3 runtimePosition, float intensity, float radiusMeters)
        {
            if (!Application.isPlaying || sourceId == 0)
                return;

            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition sourceAup))
            {
                UnregisterSource(sourceId);
                return;
            }

            RegisterSource(sourceId, in sourceAup, intensity, radiusMeters);
        }

        public static void RegisterSource(int sourceId, in AbsoluteUniversePosition sourceAup, float intensity, float radiusMeters)
        {
            if (!Application.isPlaying || sourceId == 0)
                return;

            if (!AbsoluteUniversePosition.IsFinite(in sourceAup))
            {
                UnregisterSource(sourceId);
                return;
            }

            float safeIntensity = NormalizeSourceIntensity(intensity);
            float safeRadius = NormalizeSourceRadius(radiusMeters);
            if (safeIntensity <= 0f || safeRadius <= 0f)
            {
                UnregisterSource(sourceId);
                return;
            }

            RadiationSourceSignal signal = new RadiationSourceSignal
            {
                PositionAup = sourceAup,
                Intensity = safeIntensity,
                RadiusMeters = safeRadius,
                SourceId = sourceId,
                Operation = RadiationSourceSignal.OperationUpsert,
                Flags = 0
            };
            SignalBus<RadiationSourceSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
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
            SignalBus<RadiationSourceSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
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

            float safeIntensity = Sanitize01(intensity01);
            RadiationDoseSignal signal = new RadiationDoseSignal
            {
                PositionAup = positionAup,
                Dose = dose,
                Intensity01 = safeIntensity,
                SourceId = 0u,
                DoseKind = RadiationDoseAtmosphereKind,
                Flags = 0
            };
            SignalBus<RadiationDoseSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
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
            float sampleIntensity = grid._radiationSimulationJobActive
                ? gridIntensity
                : math.max(grid.SampleInverseSquare(in sampleAup), gridIntensity);
            intensity01 = SanitizeNonNegative(sampleIntensity);
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
            CompleteRadiationJobsForTeardownRelease();
            _oceanKinematicsService = null;
        }

        private void OnDestroy()
        {
            if (ActiveRuntimeInstance == this)
                ActiveRuntimeInstance = null;

            TryUnregisterRuntimeLanes();
            TryUnregisterHotSwapListener();
            _oceanKinematicsService = null;
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
                PreserveRadiationSourceSignalsForNextSimulation();
                DrainExternalDoseSignals();
                DrainItemAcquiredSignalsDeferred();
                _radiationEvaluatedThisFrame = false;
                return dependsOn;
            }

            if (_radiationSimulationJobActive)
            {
                PreserveRadiationSourceSignalsForNextSimulation();
                DrainExternalDoseSignals();
                DrainItemAcquiredSignalsDeferred();
                return dependsOn;
            }

            if (HasDeferredStructuralOperations() && !TryApplyDeferredStructuralOperations())
            {
                PreserveRadiationSourceSignalsForNextSimulation();
                DrainExternalDoseSignals();
                DrainItemAcquiredSignalsDeferred();
                _radiationEvaluatedThisFrame = false;
                return dependsOn;
            }

            _radiationEvaluatedThisFrame = false;
            PlayerRuntimeContext playerContext = ResolveMutablePlayerRuntimeContext();
            IPlayerRuntimeContext playerReadContext = ResolveActivePlayerRuntimeContext();
            DrainRadiationSourceSignals();
            DrainExternalDoseSignals();
            DrainItemAcquiredSignals(playerContext);

            bool hasPlayerAup = TryResolvePlayerAup(playerReadContext, out AbsoluteUniversePosition playerAup);
            _lastSimulationPlayerContext = playerContext;
            _lastSimulationPlayerAup = hasPlayerAup ? playerAup : AbsoluteUniversePosition.Invalid();
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

            if (hasPlayerAup)
                dependency = ScheduleEmergencyMockSourceIfNeeded(in playerAup, dependency);

            JobHandle radiationHandle = ScheduleRadiationExposureKernel(
                playerReadContext,
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
                {
                    RecordTelemetry(_lastSimulationPlayerAup, _lastGridIntensity01, _accumulatedRadiationDose, RadiationTelemetryFlagJobActive);
                    return;
                }

                _radiationSimulationJobActive = false;
                ReleaseRadiationSdfSnapshotLock();
                _lastBurstExecutionMicroseconds = TicksToMicroseconds(Stopwatch.GetTimestamp() - _radiationSimulationStartTicks);
            }

            if (HasDeferredStructuralOperations() && !HasActiveRadiationJobs())
                TryApplyDeferredStructuralOperations();

            if (!_radiationEvaluatedThisFrame)
            {
                RecordTelemetry(_lastSimulationPlayerAup, _lastGridIntensity01, _accumulatedRadiationDose, RadiationTelemetryFlagSkippedEvaluation);
                return;
            }

            CaptureSanitizedRadiationStateFromRuntimeBuffer();

            PlayerRuntimeContext playerContext = _lastSimulationPlayerContext != null
                ? _lastSimulationPlayerContext
                : ResolveMutablePlayerRuntimeContext();
            bool hasPlayerAup = AbsoluteUniversePosition.IsFinite(in _lastSimulationPlayerAup);
            AbsoluteUniversePosition playerAup = hasPlayerAup
                ? _lastSimulationPlayerAup
                : AbsoluteUniversePosition.Invalid();
            if (!hasPlayerAup)
                hasPlayerAup = TryResolvePlayerAup(ResolveActivePlayerRuntimeContext(), out playerAup);

            float safeCompletedDelta = SanitizeNonNegative(_lastCompletedIntegrationDeltaSeconds);
            float doseAdd = _radiationEvaluatedThisFrame
                ? SanitizeNonNegative(_lastGridIntensity01 * safeCompletedDelta)
                : 0f;
            ApplyDoseToPlayerContext(playerContext, _accumulatedRadiationDose, _lastGridIntensity01);
            PublishPendingRadiationStatusSignal();
            if (hasPlayerAup)
            {
                PublishDoseSignal(in playerAup, doseAdd, _lastGridIntensity01, RadiationDoseGridKind);
                EmitGeigerIfNeeded(in playerAup, _lastGridIntensity01);
            }

            RecordTelemetry(
                playerAup,
                _lastGridIntensity01,
                _accumulatedRadiationDose,
                hasPlayerAup ? 0u : RadiationTelemetryFlagSkippedEvaluation);
            _lastExternalIntensity01 *= 0.5f;
        }

        private void VisualSyncRadiation(in DispatcherTimingDTO timing)
        {
            _currentSimulationFrame = timing.FrameId != 0u ? timing.FrameId : _currentSimulationFrame;
            PushVisualGlobals(_accumulatedRadiationDose, _lastGridIntensity01);
        }

        public void SlowTick()
        {
            _radiationCadenceAccumulatorSeconds = math.min(
                SanitizeNonNegative(_radiationCadenceAccumulatorSeconds) + RadiationSlowTickIntervalSeconds,
                RadiationSlowTickIntervalSeconds * 4f);
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!math.all(math.isfinite(new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z))) ||
                !math.isfinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.000001f)
            {
                return;
            }

            _lastShiftSequence = shiftData.Sequence;
            RecordTelemetry(_gridOriginAup, _lastGridIntensity01, _accumulatedRadiationDose, RadiationTelemetryFlagOriginShift);
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            EnsureNativeBuffers();
            CompleteRadiationJobsForSaveSnapshot();
            data.radiationDose = math.max(0f, math.isfinite(_accumulatedRadiationDose) ? _accumulatedRadiationDose : 0f);
            data.radiationGridCellSizeMeters = SanitizeRange(cellSizeMeters, DefaultCellSizeMeters, MinCellSizeMeters, MaxCellSizeMeters);
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
            RepairRadiationSourceCountFromBuffer();
            _hasGridOrigin = false;
            _lastRadiationState = default;
            _lastGridIntensity01 = 0f;
            _lastExternalIntensity01 = 0f;
            _pendingExternalDoseRad = 0f;
            _pendingIodineDoseReductionRad = 0f;
            _lastShieldingFactor01 = 0f;
            _lastCellularDegradation01 = 0f;
            _geigerPhase = 0f;
            _lastItemSignalDrainFrame = -1;
            _lastItemSignalDeferFrame = -1;
            _lastSourceSignalDrainFrame = -1;
            _lastSourceSignalPreserveFrame = -1;
            _lastExternalDoseSignalDrainFrame = -1;

            if (data == null)
            {
                StoreRestoredRadiationState(0f, 0f, 0f);
                RestoreGridOriginFromActiveSourceOrDefault();
                return;
            }

            float restoredRadiationDose = math.max(0f, math.isfinite(data.radiationDose) ? data.radiationDose : 0f);
            StoreRestoredRadiationState(restoredRadiationDose, 0f, 0f);
            cellSizeMeters = SanitizeRange(data.radiationGridCellSizeMeters, DefaultCellSizeMeters, MinCellSizeMeters, MaxCellSizeMeters);
            int persistedRleLength = ClampPersistedRadiationRleLength(data.radiationGridRle, data.radiationGridRleLength);
            if (persistedRleLength >= RlePacketSizeBytes &&
                math.isfinite(data.radiationGridOriginX) &&
                math.isfinite(data.radiationGridOriginY) &&
                math.isfinite(data.radiationGridOriginZ))
            {
                _gridOriginAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(
                    data.radiationGridOriginX,
                    data.radiationGridOriginY,
                    data.radiationGridOriginZ));
                _hasGridOrigin = true;
            }
            else
            {
                RestoreGridOriginFromActiveSourceOrDefault();
            }

            if (_hasGridOrigin && persistedRleLength >= RlePacketSizeBytes)
                DecodeSparseRle(data.radiationGridRle, persistedRleLength);

            if (applyPlayerContext)
                ApplyDoseToPlayerContext(ResolveMutablePlayerRuntimeContext(), _accumulatedRadiationDose, _lastGridIntensity01);
        }

        private void RegisterSourceInternal(int sourceId, in AbsoluteUniversePosition sourceAup, float intensity, float radiusMeters)
        {
            if (!HasRequiredRuntimeBuffers())
                return;

            float sourceIntensity01 = NormalizeSourceIntensity(intensity);
            if (!AbsoluteUniversePosition.IsFinite(in sourceAup) || sourceIntensity01 <= 0f)
            {
                UnregisterSourceInternal(sourceId);
                return;
            }

            float sourceRadiusMeters = NormalizeSourceRadius(radiusMeters);
            if (sourceRadiusMeters <= 0f)
            {
                UnregisterSourceInternal(sourceId);
                return;
            }

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
            {
                if (TryReplaceWeakestRadiationSource(sourceId, in sourceAup, sourceIntensity01, sourceRadiusMeters))
                {
                    RecordTelemetry(sourceAup, _lastGridIntensity01, _accumulatedRadiationDose, RadiationTelemetryFlagSourceOverflowReplaced);
                    return;
                }

                RecordTelemetry(sourceAup, _lastGridIntensity01, _accumulatedRadiationDose, RadiationTelemetryFlagSourceOverflow);
                return;
            }

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

        private bool TryReplaceWeakestRadiationSource(
            int sourceId,
            in AbsoluteUniversePosition sourceAup,
            float sourceIntensity01,
            float sourceRadiusMeters)
        {
            if (!_sources.IsCreated)
                return false;

            int weakestIndex = -1;
            float weakestIntensity = float.MaxValue;
            for (int i = 0; i < MaxSourceCount; i++)
            {
                RadiationSource source = _sources[i];
                if (source.Active == 0)
                    continue;

                float candidateIntensity = SanitizeNonNegative(source.Intensity01);
                if (candidateIntensity >= weakestIntensity)
                    continue;

                weakestIntensity = candidateIntensity;
                weakestIndex = i;
            }

            if (weakestIndex < 0 || sourceIntensity01 <= weakestIntensity)
                return false;

            _sources[weakestIndex] = new RadiationSource
            {
                PositionAup = sourceAup.ToAbsoluteDouble3(),
                Intensity01 = sourceIntensity01,
                RadiusMeters = sourceRadiusMeters,
                SourceId = sourceId,
                Active = 1
            };
            _sourceVersion++;
            return true;
        }

        private void RepairRadiationSourceCountFromBuffer()
        {
            int activeCount = 0;
            bool sourceSlotsChanged = false;
            if (_sources.IsCreated)
            {
                int capacity = math.clamp(MaxSourceCount, 0, _sources.Length);
                for (int i = 0; i < capacity; i++)
                {
                    RadiationSource source = _sources[i];
                    if (source.Active == 0)
                        continue;

                    float safeIntensity01 = NormalizeSourceIntensity(source.Intensity01);
                    float safeRadiusMeters = NormalizeSourceRadius(source.RadiusMeters);
                    if (source.SourceId == 0 ||
                        safeIntensity01 <= 0f ||
                        safeRadiusMeters <= 0f ||
                        !math.all(math.isfinite(source.PositionAup)))
                    {
                        _sources[i] = default;
                        sourceSlotsChanged = true;
                        continue;
                    }

                    if (source.Intensity01 != safeIntensity01 || source.RadiusMeters != safeRadiusMeters)
                    {
                        source.Intensity01 = safeIntensity01;
                        source.RadiusMeters = safeRadiusMeters;
                        _sources[i] = source;
                        sourceSlotsChanged = true;
                    }

                    activeCount++;
                }
            }

            bool sourceCountChanged = _activeSourceCount != activeCount;
            if (!sourceCountChanged)
            {
                if (_sourceCountLane.IsCreated && _sourceCountLane.Length > 0 && _sourceCountLane[0] != activeCount)
                    _sourceCountLane[0] = activeCount;
                if (sourceSlotsChanged)
                    _sourceVersion++;
                return;
            }

            _activeSourceCount = activeCount;
            if (_sourceCountLane.IsCreated && _sourceCountLane.Length > 0)
                _sourceCountLane[0] = activeCount;
            _sourceVersion++;
        }

        private void RestoreGridOriginFromActiveSourceOrDefault()
        {
            if (TryResolveFirstActiveRadiationSourceOrigin(out AbsoluteUniversePosition sourceAup))
            {
                _gridOriginAup = sourceAup;
                _hasGridOrigin = true;
                return;
            }

            _gridOriginAup = default;
            _hasGridOrigin = false;
        }

        private bool TryResolveFirstActiveRadiationSourceOrigin(out AbsoluteUniversePosition sourceAup)
        {
            sourceAup = default;
            if (!_sources.IsCreated)
                return false;

            int capacity = math.clamp(MaxSourceCount, 0, _sources.Length);
            for (int i = 0; i < capacity; i++)
            {
                RadiationSource source = _sources[i];
                if (source.Active == 0 || source.Intensity01 <= 0f)
                    continue;

                if (!math.all(math.isfinite(source.PositionAup)))
                    continue;

                sourceAup = AbsoluteUniversePosition.FromAbsolutePosition(source.PositionAup);
                return AbsoluteUniversePosition.IsFinite(in sourceAup);
            }

            return false;
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
                   _gridRead.IsCreated &&
                   _gridWrite.IsCreated &&
                   _gridSource.IsCreated &&
                   _radiationStates.IsCreated &&
                   _sources.IsCreated &&
                   _sourceCountLane.IsCreated &&
                   _telemetryRing.IsCreated &&
                   _telemetryCursorLane.IsCreated &&
                   _statusSignalLane.IsCreated &&
                   _radiationStates.Length >= RadiationEntitySlotCount &&
                   _sources.Length >= MaxSourceCount &&
                   _telemetryRing.Length >= TelemetryCapacity &&
                   _statusSignalLane.Length > 0;
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
                    Sources = (RadiationSource*)NativeArrayUnsafeUtility.GetUnsafePtr<RadiationSource>(_sources),
                    SourceCount = (int*)NativeArrayUnsafeUtility.GetUnsafePtr<int>(_sourceCountLane),
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
                _gridReadHandle = vault.EnsureGenerationHandle<float>(BufferID.Shinobu274RadiationGridRead, GridCellCount, OwnerSystemId);
                _gridWriteHandle = vault.EnsureGenerationHandle<float>(BufferID.Shinobu274RadiationGridWrite, GridCellCount, OwnerSystemId);
                _gridSourceHandle = vault.EnsureGenerationHandle<float>(BufferID.Shinobu274RadiationGridSource, GridCellCount, OwnerSystemId);
                _stateHandle = vault.EnsureGenerationHandle<RadiationStateDTO>(BufferID.Shinobu274RadiationStates, RadiationEntitySlotCount, OwnerSystemId);
                _sourcesHandle = vault.EnsureGenerationHandle<RadiationSource>(BufferID.Shinobu274RadiationSources, MaxSourceCount, OwnerSystemId);
                _sourceCountHandle = vault.EnsureGenerationHandle<int>(BufferID.Shinobu274RadiationSourceCount, 1, OwnerSystemId);
                _telemetryHandle = vault.EnsureGenerationHandle<RadiationTelemetryEntry>(BufferID.Shinobu274RadiationTelemetryRing, TelemetryCapacity, OwnerSystemId);
                _telemetryCursorHandle = vault.EnsureGenerationHandle<uint>(BufferID.Shinobu274RadiationTelemetryCursor, 1, OwnerSystemId);
                _profilesHandle = vault.EnsureGenerationHandle<RadiationProfileDTO>(BufferID.Shinobu274RadiationProfiles, RadiationProfileCapacity, OwnerSystemId);
                _tuningHandle = vault.EnsureGenerationHandle<RadiationTuningDTO>(BufferID.Shinobu274RadiationTuning, 1, OwnerSystemId);
                _statusSignalHandle = vault.EnsureGenerationHandle<RadiationStatusSignal>(RadiationStatusSignalBuffer, 1, OwnerSystemId);
                _radiationSdfSnapshotHandle = vault.EnsureGenerationHandle<byte>(RadiationSdfSnapshotBuffer, 1, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
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
                !vault.TryResolveHandle(in _tuningHandle, out NativeArray<RadiationTuningDTO> tuning) ||
                !vault.TryResolveHandle(in _statusSignalHandle, out NativeArray<RadiationStatusSignal> statusSignal))
            {
                return false;
            }

            _gridRead = VaultBufferView<float>.Create(vault, _gridBuffersSwapped ? _gridWriteHandle : _gridReadHandle);
            _gridWrite = VaultBufferView<float>.Create(vault, _gridBuffersSwapped ? _gridReadHandle : _gridWriteHandle);
            _gridSource = VaultBufferView<float>.Create(vault, _gridSourceHandle);
            _radiationStates = VaultBufferView<RadiationStateDTO>.Create(vault, _stateHandle);
            _sources = VaultBufferView<RadiationSource>.Create(vault, _sourcesHandle);
            _sourceCountLane = VaultBufferView<int>.Create(vault, _sourceCountHandle);
            _telemetryRing = VaultBufferView<RadiationTelemetryEntry>.Create(vault, _telemetryHandle);
            _telemetryCursorLane = VaultBufferView<uint>.Create(vault, _telemetryCursorHandle);
            _profiles = VaultBufferView<RadiationProfileDTO>.Create(vault, _profilesHandle);
            _tuningLane = VaultBufferView<RadiationTuningDTO>.Create(vault, _tuningHandle);
            _statusSignalLane = VaultBufferView<RadiationStatusSignal>.Create(vault, _statusSignalHandle);

            if (_sourceCountLane.IsCreated && _sourceCountLane.Length > 0)
                _sourceCountLane[0] = _activeSourceCount;
            if (_telemetryWriteIndex == 0 &&
                vault.TryReadOnlyHandle(in _telemetryCursorHandle, out NativeArray<uint>.ReadOnly telemetryCursorRead) &&
                telemetryCursorRead.IsCreated &&
                telemetryCursorRead.Length > 0)
            {
                _telemetryWriteIndex = WrapTelemetryCursor(telemetryCursorRead[0], TelemetryCapacity);
            }

            EnsureDefaultRadiationTuning();
#if UNITY_EDITOR
            TryLoadRadiationProfilesCsv();
#endif
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
                tuning.DamagePerTickScale = RadiationStatusMagnitudeScale;
                tuning.LeadShieldingEffectiveness = 1f;
                tuning.MaxSdfSamples = 12;
                tuning.Flags = 1u;
                _tuningLane[0] = tuning;
            }
        }

#if UNITY_EDITOR
        private void TryLoadRadiationProfilesCsv()
        {
            if (_profilesCsvLoaded || radiationProfilesCsv == null)
                return;

            NativeArray<byte> bytes = radiationProfilesCsv.GetData<byte>();
            if (!bytes.IsCreated || bytes.Length == 0)
            {
                _profilesCsvLoaded = true;
                return;
            }

            IDataVault vault = _dataVault;
            if (vault == null || _profilesHandle.BufferID == 0u)
                return;

            if (!vault.TryAcquireWriteLock(in _profilesHandle, OwnerSystemId, out NativeArray<RadiationProfileDTO> profiles))
                return;

            try
            {
                int loadedProfiles = IngestRadiationProfilesCsv(bytes, profiles);
                ClearRadiationProfilesTail(profiles, loadedProfiles);
                _profilesCsvLoaded = true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _profilesHandle, OwnerSystemId);
            }
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

        private static void ClearRadiationProfilesTail(NativeArray<RadiationProfileDTO> profiles, int startIndex)
        {
            if (!profiles.IsCreated)
                return;

            int index = math.max(0, startIndex);
            while (index < profiles.Length)
            {
                profiles[index] = default;
                index++;
            }
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
            float profileRadiusMeters = NormalizeSourceRadius(profile.RadiusMeters);
            profile.RadiusMeters = profileRadiusMeters > 0f ? profileRadiusMeters : DefaultSourceRadiusMeters;
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

        private static byte ToLowerAscii(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z'
                ? (byte)(value + 32)
                : value;
        }

#endif

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
                ReleaseVaultHandle(vault, ref _tuningHandle);
                ReleaseVaultHandle(vault, ref _statusSignalHandle);
                ReleaseRadiationSdfSnapshotLock();
                ReleaseVaultHandle(vault, ref _radiationSdfSnapshotHandle);
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
            _tuningHandle = default;
            _statusSignalHandle = default;
            _radiationSdfSnapshotHandle = default;
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
            _tuningLane = default;
            _statusSignalLane = default;
            _gridBuffersSwapped = false;
            _blackBoxDumpAttempted = false;
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

            if (!_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);

            if (!_registeredOriginShift)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _registeredOriginShift = true;
            }

            if (_registeredSave &&
                (!ReferenceEquals(_registeredSaveService, GlobalRegistry.Save) || !IsSaveServiceUsable(_registeredSaveService)))
            {
                TryUnregisterSaveParticipant();
            }

            TryRegisterSaveParticipant();
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

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }

            if (_registeredOriginShift)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShift = false;
            }

            TryUnregisterSaveParticipant();
        }

        private void TryUnregisterDispatcherLanes()
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

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        private void TryRegisterSaveParticipant()
        {
            if (_registeredSave)
                return;

            ISaveService saveService = _saveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _saveService = saveService;
            }

            if (!IsSaveServiceUsable(saveService))
                return;

            saveService.Register(this);
            _registeredSaveService = saveService;
            _saveService = saveService;
            _registeredSave = true;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (!_registeredSave && _registeredSaveService == null)
                return;

            ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _registeredSaveService = null;
            _registeredSave = false;
            _saveService = null;
        }

        private void RefreshColdRegistryReferences()
        {
            if (!_registeredSave)
                _saveService = GlobalRegistry.Save;

            _dataVault = GlobalRegistry.DataVault;
            _voxelSdfReadModel = GlobalRegistry.VoxelSonarSdf;
            _voxelSdfReadLeaseModel = _voxelSdfReadModel as IVoxelSonarSdfReadLeaseModel;
            _oceanKinematicsService = GlobalRegistry.OceanKinematics;
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
                    TryUnregisterDispatcherLanes();
                    if (currentService != null)
                        TryRegisterRuntimeLanes();
                    break;
                case GlobalRegistryServiceSlot.Save:
                    TryUnregisterSaveParticipant();
                    _saveService = currentService as ISaveService;
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
                    _voxelSdfReadLeaseModel = currentService as IVoxelSonarSdfReadLeaseModel;
                    break;
                case GlobalRegistryServiceSlot.OceanKinematics:
                    _oceanKinematicsService = currentService as IHectonOceanKinematicsService;
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
            float safeCellSize = SanitizeRange(cellSizeMeters, DefaultCellSizeMeters, MinCellSizeMeters, MaxCellSizeMeters);
            int half = GridResolution >> 1;

            for (int sourceIndex = 0; sourceIndex < MaxSourceCount; sourceIndex++)
            {
                RadiationSource source = _sources[sourceIndex];
                if (source.Active == 0 || source.Intensity01 <= 0f)
                    continue;

                double3 sourceAbsolute = source.PositionAup;
                double3 sourceOffset = sourceAbsolute - origin;
                if (!math.all(math.isfinite(sourceAbsolute)) || !math.all(math.isfinite(sourceOffset)))
                    continue;

                float safeRadius = NormalizeSourceRadius(source.RadiusMeters);
                if (safeRadius <= 0f)
                    continue;

                int centerX = (int)math.floor(sourceOffset.x / safeCellSize) + half;
                int centerY = (int)math.floor(sourceOffset.y / safeCellSize) + half;
                int centerZ = (int)math.floor(sourceOffset.z / safeCellSize) + half;
                int radiusCells = math.max(1, (int)math.ceil(safeRadius / safeCellSize));
                int minX = math.max(0, centerX - radiusCells);
                int maxX = math.min(GridResolution - 1, centerX + radiusCells);
                int minY = math.max(0, centerY - radiusCells);
                int maxY = math.min(GridResolution - 1, centerY + radiusCells);
                int minZ = math.max(0, centerZ - radiusCells);
                int maxZ = math.min(GridResolution - 1, centerZ + radiusCells);
                float radiusSq = math.max(0.25f, safeRadius * safeRadius);

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
            VaultBufferView<float> previousRead = _gridRead;
            _gridRead = _gridWrite;
            _gridWrite = previousRead;
            _gridBuffersSwapped = !_gridBuffersSwapped;
            _gridVersion++;
        }

        private void CompleteDiffusionJobForForcedSwapWindow()
        {
            if (!_diffusionJobActive)
                return;

            DispatcherJobFence.TryComplete(ref _diffusionJobHandle, forceComplete: true);
            _diffusionJobActive = false;
            VaultBufferView<float> previousRead = _gridRead;
            _gridRead = _gridWrite;
            _gridWrite = previousRead;
            _gridBuffersSwapped = !_gridBuffersSwapped;
            _gridVersion++;
        }

        private bool HasActiveRadiationJobs()
        {
            return _radiationSimulationJobActive || _diffusionJobActive;
        }

        private bool HasDeferredStructuralOperations()
        {
            return _pendingDataVaultSwap || _pendingLoadDataValid;
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
            float preservedAccumulatedDose = SanitizeNonNegative(_accumulatedRadiationDose);
            ReleaseVaultHandles();
            _dataVault = nextVault;
            EnsureNativeBuffers();
            RestoreRadiationRuntimeStateFromVaultAfterSwap(preservedAccumulatedDose);
        }

        private void RestoreRadiationRuntimeStateFromVaultAfterSwap(float preservedAccumulatedDose)
        {
            _lastExternalIntensity01 = 0f;
            _pendingExternalDoseRad = 0f;
            _pendingIodineDoseReductionRad = 0f;
            _radiationEvaluatedThisFrame = false;
            _lastSimulationPlayerAup = default;
            _lastSimulationPlayerContext = null;
            _lastCompletedIntegrationDeltaSeconds = 0f;
            _lastBurstExecutionMicroseconds = 0f;
            _lastItemSignalDrainFrame = -1;
            _lastItemSignalDeferFrame = -1;
            _lastSourceSignalDrainFrame = -1;
            _lastSourceSignalPreserveFrame = -1;
            _lastExternalDoseSignalDrainFrame = -1;
            _geigerPhase = 0f;
            _hasGridOrigin = false;

            ClearGrid(_gridSource);
            RepairRadiationSourceCountFromBuffer();
            RestoreGridOriginFromActiveSourceOrDefault();

            RadiationStateDTO state = default;
            float safePreservedDose = SanitizeNonNegative(preservedAccumulatedDose);
            if (_radiationStates.IsCreated && _radiationStates.Length > 0)
            {
                state = _radiationStates[0];
                if (!IsRadiationStateFinite(in state))
                    state = default;

                float vaultDose = SanitizeNonNegative(state.CumulativeDoseRad);
                state.CumulativeDoseRad = vaultDose > 0f ? vaultDose : safePreservedDose;
                state.CurrentExposureRate = 0f;
                state.ShieldingFactor01 = Sanitize01(state.ShieldingFactor01);
                state.CellularDegradation01 = Sanitize01(state.CellularDegradation01);
                state.EntityHashID = RadiationSystemHash;
                state.Flags = ResolveRestoredRadiationStateFlags(state.CellularDegradation01);
                _radiationStates[0] = state;
            }
            else
            {
                state.CumulativeDoseRad = safePreservedDose;
                state.EntityHashID = RadiationSystemHash;
                state.Flags = ResolveRestoredRadiationStateFlags(state.CellularDegradation01);
            }

            _accumulatedRadiationDose = state.CumulativeDoseRad;
            _lastRadiationState = state;
            _lastGridIntensity01 = 0f;
            _lastShieldingFactor01 = state.ShieldingFactor01;
            _lastCellularDegradation01 = state.CellularDegradation01;

            if (_statusSignalLane.IsCreated && _statusSignalLane.Length > 0)
                _statusSignalLane[0] = default;

            _telemetryWriteIndex = 0;
            if (_telemetryCursorLane.IsCreated && _telemetryCursorLane.Length > 0)
                _telemetryCursorLane[0] = 0u;
        }

        private static uint ResolveRestoredRadiationStateFlags(float cellularDegradation01)
        {
            float safeDegradation = Sanitize01(cellularDegradation01);
            uint flags = safeDegradation >= 0.01f ? RadiationStateFlagMutated : 0u;
            if (safeDegradation >= RadiationCriticalDegradation01)
                flags |= RadiationStateFlagCritical;

            return flags;
        }

        private void StoreRestoredRadiationState(float cumulativeDoseRad, float exposureRate, float cellularDegradation01)
        {
            RadiationStateDTO state = new RadiationStateDTO
            {
                CumulativeDoseRad = SanitizeNonNegative(cumulativeDoseRad),
                CurrentExposureRate = SanitizeNonNegative(exposureRate),
                ShieldingFactor01 = 0f,
                CellularDegradation01 = Sanitize01(cellularDegradation01),
                EntityHashID = RadiationSystemHash,
                Flags = ResolveRestoredRadiationStateFlags(cellularDegradation01)
            };

            if (_radiationStates.IsCreated && _radiationStates.Length > 0)
                _radiationStates[0] = state;

            _accumulatedRadiationDose = state.CumulativeDoseRad;
            _lastRadiationState = state;
            _lastGridIntensity01 = state.CurrentExposureRate;
            _lastShieldingFactor01 = state.ShieldingFactor01;
            _lastCellularDegradation01 = state.CellularDegradation01;
        }

        private void CompleteRadiationJobsForSaveSnapshot()
        {
            if (HasActiveRadiationJobs() || _radiationSdfSnapshotLocked)
            {
                DispatcherJobFence.BeginPostSimulationSwapWindow();
                try
                {
                    CompleteRadiationSimulationJobForForcedSwapWindow();
                    CompleteDiffusionJobForForcedSwapWindow();
                    ReleaseRadiationSdfSnapshotLock();
                }
                finally
                {
                    DispatcherJobFence.EndPostSimulationSwapWindow();
                }
            }

            CaptureSanitizedRadiationStateFromRuntimeBuffer();

            if (HasDeferredStructuralOperations() && !HasActiveRadiationJobs())
            {
                TryApplyDeferredStructuralOperations();
                CaptureSanitizedRadiationStateFromRuntimeBuffer();
            }
        }

        private void CaptureSanitizedRadiationStateFromRuntimeBuffer()
        {
            RadiationStateDTO state = _radiationStates.IsCreated && _radiationStates.Length > 0
                ? _radiationStates[0]
                : _lastRadiationState;
            if (!IsRadiationStateFinite(in state))
            {
                DumpBlackBox();
                state = default;
            }

            state.CumulativeDoseRad = SanitizeNonNegative(state.CumulativeDoseRad);
            state.CurrentExposureRate = SanitizeNonNegative(state.CurrentExposureRate);
            state.ShieldingFactor01 = Sanitize01(state.ShieldingFactor01);
            state.CellularDegradation01 = Sanitize01(state.CellularDegradation01);
            if (_radiationStates.IsCreated && _radiationStates.Length > 0)
                _radiationStates[0] = state;

            _lastRadiationState = state;
            _lastGridIntensity01 = state.CurrentExposureRate;
            _accumulatedRadiationDose = state.CumulativeDoseRad;
            _lastShieldingFactor01 = state.ShieldingFactor01;
            _lastCellularDegradation01 = state.CellularDegradation01;
        }

        private void CompleteRadiationJobsForTeardownRelease()
        {
            if (!HasActiveRadiationJobs() && !_radiationSdfSnapshotLocked)
                return;

            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                CompleteRadiationSimulationJobForForcedSwapWindow();
                CompleteDiffusionJobForForcedSwapWindow();
                ReleaseRadiationSdfSnapshotLock();
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        private void CompleteRadiationSimulationJobForForcedSwapWindow()
        {
            if (!_radiationSimulationJobActive)
                return;

            DispatcherJobFence.TryComplete(ref _radiationSimulationJobHandle, forceComplete: true);
            _radiationSimulationJobActive = false;
            ReleaseRadiationSdfSnapshotLock();
            _lastBurstExecutionMicroseconds = TicksToMicroseconds(Stopwatch.GetTimestamp() - _radiationSimulationStartTicks);
        }

        private void ReleaseRadiationSdfSnapshotLock()
        {
            if (!_radiationSdfSnapshotLocked)
                return;

            IDataVault vault = _dataVault;
            if (vault != null)
                vault.ReleaseMutationGuard(RadiationSdfSnapshotMutationGuardMask);

            _radiationSdfSnapshotLocked = false;
        }

        private float SampleGridNearest(in AbsoluteUniversePosition sampleAup)
        {
            if (!_gridRead.IsCreated || !_hasGridOrigin)
                return 0f;

            if (!TryResolveGridCell(in sampleAup, out int x, out int y, out int z))
                return 0f;

            float value = _gridRead[Flatten(x, y, z)];
            return math.isfinite(value) ? math.saturate(value) : 0f;
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

                if (!math.all(math.isfinite(source.PositionAup)) ||
                    !math.isfinite(source.Intensity01) ||
                    !math.isfinite(source.RadiusMeters))
                {
                    continue;
                }

                double3 delta = sampleAbsolute - source.PositionAup;
                double distanceSq = math.lengthsq(delta);
                if (!math.isfinite(distanceSq))
                    continue;

                float radius = NormalizeSourceRadius(source.RadiusMeters);
                if (radius <= 0f)
                    continue;

                float radiusSq = radius * radius;
                float inverseSq = radiusSq * math.rcp(math.max((float)distanceSq, 0.0001f));
                total += source.Intensity01 * math.saturate(inverseSq);
            }

            return math.saturate(total);
        }

        private JobHandle ScheduleRadiationExposureKernel(
            IPlayerRuntimeContext playerContext,
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
                !_statusSignalLane.IsCreated ||
                _radiationStates.Length == 0 ||
                _statusSignalLane.Length == 0)
            {
                return dependsOn;
            }

            NativeArray<byte>.ReadOnly encodedSdf = default;
            int3 sdfDimensions = default;
            float3 sdfVolumeOrigin = default;
            float3 sdfCellSize = default;
            float sdfRange = 0f;
            bool hasPlayerAup = AbsoluteUniversePosition.IsFinite(in playerAup);
            float3 playerRuntime = default;
            double3 playerAbsolute = default;
            if (hasPlayerAup)
            {
                playerRuntime = playerAup.ToRuntimeFloat3();
                playerAbsolute = playerAup.ToAbsoluteDouble3();
                hasPlayerAup = math.all(math.isfinite(playerRuntime)) && math.all(math.isfinite(playerAbsolute));
            }

            IVoxelSonarSdfReadLeaseModel sdfReadLeaseModel = _voxelSdfReadLeaseModel;
            VoxelSonarSdfReadLease sdfReadLease = default;
            bool sdfReadLeaseLocked = false;
            bool sdfSnapshotLocked = false;
            if (hasPlayerAup && sdfReadLeaseModel != null)
            {
                sdfReadLeaseLocked = sdfReadLeaseModel.TryAcquireNearestSonarSdfReadLease(
                    playerRuntime,
                    out NativeArray<byte>.ReadOnly sourceSdf,
                    out sdfDimensions,
                    out sdfVolumeOrigin,
                    out sdfCellSize,
                    out sdfRange,
                    out sdfReadLease);
                if (sdfReadLeaseLocked)
                {
                    if (!TryCopyRadiationSdfLeaseToSnapshot(sourceSdf, sdfDimensions, out encodedSdf, out sdfSnapshotLocked))
                    {
                        sdfReadLeaseModel.ReleaseNearestSonarSdfReadLease(in sdfReadLease);
                        sdfReadLeaseLocked = false;
                        sdfDimensions = default;
                        sdfVolumeOrigin = default;
                        sdfCellSize = default;
                        sdfRange = 0f;
                    }
                }
            }

            if (sdfReadLeaseLocked && sdfReadLeaseModel != null)
            {
                sdfReadLeaseModel.ReleaseNearestSonarSdfReadLease(in sdfReadLease);
                sdfReadLeaseLocked = false;
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
            RadiationStateDTO seedState = _radiationStates[0];
            if (_accumulatedRadiationDose > seedState.CumulativeDoseRad && math.isfinite(_accumulatedRadiationDose))
            {
                seedState.CumulativeDoseRad = _accumulatedRadiationDose;
                _radiationStates[0] = seedState;
            }
            _statusSignalLane[0] = default;

            CalculateRadiationExposureJob job = new CalculateRadiationExposureJob
            {
                States = (RadiationStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr<RadiationStateDTO>(_radiationStates),
                Sources = (RadiationSource*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr<RadiationSource>(_sources),
                StatusSignal = (RadiationStatusSignal*)NativeArrayUnsafeUtility.GetUnsafePtr<RadiationStatusSignal>(_statusSignalLane),
                EncodedSdf = encodedSdf,
                BulkheadStates = bulkheadStates.IsCreated ? (BulkheadStateDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bulkheadStates) : null,
                BulkheadPlanes = bulkheadPlanes.IsCreated ? (BulkheadPlaneDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bulkheadPlanes) : null,
                SourceCapacity = _sources.Length,
                ActiveSourceCount = activeSources,
                BulkheadCount = maxBulkheadSamples,
                PlayerAup = playerAbsolute,
                PlayerRuntime = playerRuntime,
                HasPlayerAup = hasPlayerAup ? 1u : 0u,
                SimulationTickDelta = SanitizeNonNegative(simulationTickDelta),
                ExternalExposureRate = Sanitize01(externalExposureRate),
                ExternalDoseDelta = math.max(0f, math.isfinite(externalDoseDelta) ? externalDoseDelta : 0f),
                DoseDecayPerTick = SanitizeRange(tuning.DecayPerTick, DoseDecayPerSimulationStep, 0f, 1f),
                DoseToDegradationScale = SanitizeRange(tuning.DoseToDegradationScale, 0.01f, 0.0001f, 1f),
                DamagePerTickScale = SanitizeRange(tuning.DamagePerTickScale, RadiationStatusMagnitudeScale, 0f, 100f),
                LeadShieldingEffectiveness = SanitizeRange(tuning.LeadShieldingEffectiveness, 1f, 0f, 1f),
                PlayerTargetId = playerTargetId,
                Frame = frame,
                SdfDimensions = sdfDimensions,
                SdfVolumeOrigin = sdfVolumeOrigin,
                SdfCellSize = SanitizeCellSize(sdfCellSize),
                SdfRange = SanitizeRange(sdfRange, 0.001f, 0.001f, 100000f),
                SdfSampleCount = sdfSampleCount
            };
            bool snapshotClaimed = false;
            try
            {
                JobHandle handle = job.Schedule(dependsOn);
                if (sdfSnapshotLocked)
                {
                    _radiationSdfSnapshotLocked = true;
                    snapshotClaimed = true;
                }

                return handle;
            }
            finally
            {
                if (sdfReadLeaseLocked && sdfReadLeaseModel != null)
                    sdfReadLeaseModel.ReleaseNearestSonarSdfReadLease(in sdfReadLease);
                if (!snapshotClaimed)
                    UnlockRadiationSdfSnapshot(ref sdfSnapshotLocked);
            }
        }

        private bool TryCopyRadiationSdfLeaseToSnapshot(
            NativeArray<byte>.ReadOnly sourceSdf,
            int3 dimensions,
            out NativeArray<byte>.ReadOnly snapshotSdf,
            out bool snapshotLocked)
        {
            snapshotSdf = default;
            snapshotLocked = false;
            long expectedLong = (long)dimensions.x * dimensions.y * dimensions.z;
            if (expectedLong <= 0L ||
                expectedLong > int.MaxValue ||
                !sourceSdf.IsCreated ||
                sourceSdf.Length < expectedLong)
            {
                return false;
            }

            int requiredLength = (int)expectedLong;
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                return false;

            if (!IsRadiationSdfSnapshotReady(vault, requiredLength, out NativeArray<byte> snapshot))
            {
                if (vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                    return false;

                _radiationSdfSnapshotHandle = vault.EnsureGenerationHandle<byte>(
                    RadiationSdfSnapshotBuffer,
                    requiredLength,
                    OwnerSystemId,
                    NativeArrayOptions.UninitializedMemory);
            }

            if (vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(RadiationSdfSnapshotMutationGuardMask))
            {
                return false;
            }

            snapshotLocked = true;
            bool handoffToScheduledJob = false;
            try
            {
                if (vault.IsCompactionFenceActive)
                    return false;

                if (!IsRadiationSdfSnapshotReady(vault, requiredLength, out snapshot))
                    return false;

                for (int i = 0; i < requiredLength; i++)
                    snapshot[i] = sourceSdf[i];

                snapshotSdf = snapshot.AsReadOnly();
                handoffToScheduledJob = true;
                return true;
            }
            finally
            {
                if (!handoffToScheduledJob)
                    UnlockRadiationSdfSnapshot(ref snapshotLocked);
            }
        }

        private bool IsRadiationSdfSnapshotReady(IDataVault vault, int requiredLength, out NativeArray<byte> snapshot)
        {
            snapshot = default;
            return vault != null &&
                   requiredLength > 0 &&
                   !vault.IsCompactionFenceActive &&
                   IsRadiationSdfSnapshotHandle(in _radiationSdfSnapshotHandle) &&
                   vault.TryResolveHandle(in _radiationSdfSnapshotHandle, out snapshot) &&
                   !vault.IsCompactionFenceActive &&
                   snapshot.IsCreated &&
                   snapshot.Length >= requiredLength;
        }

        private void UnlockRadiationSdfSnapshot(ref bool locked)
        {
            if (!locked)
                return;

            IDataVault vault = _dataVault;
            if (vault != null)
                vault.ReleaseMutationGuard(RadiationSdfSnapshotMutationGuardMask);

            locked = false;
        }

        private static bool IsRadiationSdfSnapshotHandle(in VaultGenerationHandle<byte> handle)
        {
            return handle.BufferID == unchecked((uint)(int)RadiationSdfSnapshotBuffer) &&
                   handle.SystemID == (uint)OwnerSystemId &&
                   handle.Generation != 0u;
        }

        private static ulong RadiationMutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 63);
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

        private static uint ResolvePlayerCombatTargetId(IPlayerRuntimeContext playerContext)
        {
            if (playerContext == null || playerContext.PlayerObject == null)
                return 0u;

            int target = CombatDamageRuntime.ResolveTargetId(playerContext.PlayerObject);
            return target == 0 ? 0u : unchecked((uint)target);
        }

        private void PublishPendingRadiationStatusSignal()
        {
            if (!_statusSignalLane.IsCreated || _statusSignalLane.Length == 0)
                return;

            RadiationStatusSignal signal = _statusSignalLane[0];
            if (!math.isfinite(signal.Magnitude01) ||
                signal.Magnitude01 <= 0f ||
                signal.TargetId == 0u)
            {
                _statusSignalLane[0] = default;
                return;
            }

            int targetId = signal.TargetId <= (uint)int.MaxValue
                ? (int)signal.TargetId
                : 0;
            int sourceId = signal.SourceId <= (uint)int.MaxValue
                ? (int)signal.SourceId
                : RadiationCombatSourceId;

            if (targetId != 0)
            {
                CombatDamageRuntime.TryQueueStatusEffect(
                    targetId,
                    CombatStatusBits.Irradiated64,
                    RadiationCriticalStatusDurationSeconds,
                    sourceId != 0 ? sourceId : RadiationCombatSourceId,
                    math.saturate(signal.Magnitude01));
            }

            _statusSignalLane[0] = default;
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

        private static float SanitizeNonNegative(float value)
        {
            return math.isfinite(value) && value > 0f ? value : 0f;
        }

        private static float Sanitize01(float value)
        {
            return math.saturate(math.isfinite(value) ? value : 0f);
        }

        private static float SanitizeSignalQuantity(float value)
        {
            return math.isfinite(value) && value > 0f ? math.min(value, 1000000f) : 1f;
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
            float tickInterval = RadiationSlowTickIntervalSeconds;
            bool forced = _lastRadiationState.EntityHashID == 0u;
            bool evaluate = forced || _radiationCadenceAccumulatorSeconds >= tickInterval;
            float accumulatedSeconds = forced && _radiationCadenceAccumulatorSeconds < tickInterval
                ? tickInterval
                : _radiationCadenceAccumulatorSeconds;
            integrationDelta = math.max(0f, math.isfinite(doseScalePerSimulationSecond) ? doseScalePerSimulationSecond : 0f) * accumulatedSeconds;
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
            x = 0;
            y = 0;
            z = 0;
            if (!AbsoluteUniversePosition.IsFinite(in sampleAup) || !AbsoluteUniversePosition.IsFinite(in _gridOriginAup))
                return false;

            double3 origin = _gridOriginAup.ToAbsoluteDouble3();
            double3 sample = sampleAup.ToAbsoluteDouble3();
            double3 offset = sample - origin;
            if (!math.all(math.isfinite(offset)))
                return false;

            float safeCellSize = SanitizeRange(cellSizeMeters, DefaultCellSizeMeters, MinCellSizeMeters, MaxCellSizeMeters);
            double maxOffsetMeters = (double)safeCellSize * GridResolution;
            if (math.abs(offset.x) > maxOffsetMeters ||
                math.abs(offset.y) > maxOffsetMeters ||
                math.abs(offset.z) > maxOffsetMeters)
            {
                return false;
            }

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

                float quantity = SanitizeSignalQuantity(signal.Quantity);
                ApplyIodineDoseReduction(playerContext, IodineDoseReduction * quantity);
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

                float quantity = SanitizeSignalQuantity(signal.Quantity);
                float pending = SanitizeNonNegative(_pendingIodineDoseReductionRad);
                float addition = SanitizeNonNegative(IodineDoseReduction * quantity);
                float nextPending = pending + addition;
                _pendingIodineDoseReductionRad = math.isfinite(nextPending) ? nextPending : pending;
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
            ApplyIodineDoseReduction(playerContext, pendingReduction);
        }

        private void ApplyIodineDoseReduction(PlayerRuntimeContext playerContext, float doseReductionRad)
        {
            if (!(doseReductionRad > 0f) || !math.isfinite(doseReductionRad))
                return;

            float doseReduction = doseReductionRad;
            float pendingDose = math.max(0f, math.isfinite(_pendingExternalDoseRad) ? _pendingExternalDoseRad : 0f);
            float accumulatedDose = math.max(0f, math.isfinite(_accumulatedRadiationDose) ? _accumulatedRadiationDose : 0f);
            float pendingReduction = math.min(pendingDose, doseReduction);
            _pendingExternalDoseRad = math.max(0f, pendingDose - pendingReduction);
            doseReduction = math.max(0f, doseReduction - pendingReduction);
            _accumulatedRadiationDose = math.max(0f, accumulatedDose - doseReduction);
            if (_radiationStates.IsCreated && _radiationStates.Length > 0)
            {
                RadiationStateDTO state = _radiationStates[0];
                state.CumulativeDoseRad = _accumulatedRadiationDose;
                _radiationStates[0] = state;
            }

            ApplyDoseToPlayerContext(playerContext, _accumulatedRadiationDose, _lastGridIntensity01);
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

                SignalBus<RadiationSourceSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
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

                float pendingDose = SanitizeNonNegative(_pendingExternalDoseRad);
                float signalDose = SanitizeNonNegative(signal.Dose);
                float nextDose = pendingDose + signalDose;
                _pendingExternalDoseRad = math.isfinite(nextDose) ? nextDose : pendingDose;
                _lastExternalIntensity01 = math.max(Sanitize01(_lastExternalIntensity01), Sanitize01(signal.Intensity01));
            }
        }

        private PlayerRuntimeContext ResolveMutablePlayerRuntimeContext()
        {
            return PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext)
                ? runtimeContext
                : null;
        }

        private static IPlayerRuntimeContext ResolveActivePlayerRuntimeContext()
        {
            return PlayerRuntimeContextService.ActiveRuntimeContext;
        }

        private static bool TryResolvePlayerAup(IPlayerRuntimeContext playerContext, out AbsoluteUniversePosition playerAup)
        {
            playerAup = AbsoluteUniversePosition.Invalid();
            if (playerContext != null)
            {
                if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                    (snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
                {
                    AbsoluteUniversePosition snapshotAup = snapshot.Aup;
                    if (snapshotAup.IsFinite())
                    {
                        playerAup = snapshotAup;
                        return true;
                    }
                }

                if (playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                    (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
                {
                    AbsoluteUniversePosition predictedAup = movementState.PredictedAup;
                    if (predictedAup.IsFinite())
                    {
                        playerAup = predictedAup;
                        return true;
                    }
                }

                return false;
            }

            if (TryResolveAupFromRuntimeOrigin(Vector3.zero, out playerAup))
                return true;

            playerAup = AbsoluteUniversePosition.Invalid();
            return false;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = AbsoluteUniversePosition.Invalid();
            if (!math.isfinite(runtimePosition.x) || !math.isfinite(runtimePosition.y) || !math.isfinite(runtimePosition.z))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
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

            float safeDose = math.max(0f, math.isfinite(dose) ? dose : 0f);
            float safeIntensity = Sanitize01(intensity01);
            float penalty01 = math.saturate(1f - HectonPlayerHealth.ResolveRadiationFatigueScale(safeDose));
            playerContext.RadiationDose = safeDose;
            playerContext.RadiationIntensity01 = safeIntensity;
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
            if (!AbsoluteUniversePosition.IsFinite(in positionAup))
            {
                DumpBlackBox();
                return;
            }

            float safeDose = SanitizeNonNegative(dose);
            float safeIntensity = Sanitize01(intensity01);
            RadiationDoseSignal signal = new RadiationDoseSignal
            {
                PositionAup = positionAup,
                Dose = safeDose,
                Intensity01 = safeIntensity,
                SourceId = GeigerSourceId,
                DoseKind = doseKind,
                Flags = UsesSparseRadiationCadence(ResolveGlobalQualityWeight(), _currentSimulationFrame) ? (byte)1 : (byte)0
            };
            SignalBus<RadiationDoseSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
        }

        private void EmitGeigerIfNeeded(in AbsoluteUniversePosition playerAup, float intensity01)
        {
            float safeIntensity = Sanitize01(intensity01);
            if (safeIntensity <= 0.001f)
            {
                _geigerPhase = 0f;
                return;
            }

            if (!AbsoluteUniversePosition.IsFinite(in playerAup))
            {
                _geigerPhase = 0f;
                DumpBlackBox();
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
            SignalBus<AcousticPingSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
        }

        private void PushVisualGlobals(float dose, float intensity01)
        {
            float safeDose = math.max(0f, math.isfinite(dose) ? dose : 0f);
            float exposureRate = math.max(0f, math.isfinite(intensity01) ? intensity01 : 0f);
            float safeIntensity = math.saturate(exposureRate);
            float safeDegradation = Sanitize01(_lastCellularDegradation01);
            float mutation01 = math.saturate(math.max(safeDegradation, safeDose * 0.01f));
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

        public static bool HasPersistedRadiationGridPayload(byte[] payload, int byteLength)
        {
            return ClampPersistedRadiationRleLength(payload, byteLength) >= RlePacketSizeBytes;
        }

        private static int ClampPersistedRadiationRleLength(byte[] payload, int byteLength)
        {
            if (payload == null)
                return 0;

            int payloadCapacity = math.min(payload.Length, MaxRlePayloadBytes);
            return math.clamp(byteLength, 0, payloadCapacity);
        }

        private static byte QuantizeCell(float value)
        {
            if (!math.isfinite(value) || value <= 0f)
                return 0;

            return (byte)math.clamp((int)math.round(math.saturate(value) * 127f), 0, 127);
        }

        private static int WrapTelemetryIndex(int value, int capacity)
        {
            if (capacity <= 0)
                return 0;

            int wrapped = value % capacity;
            return wrapped < 0 ? wrapped + capacity : wrapped;
        }

        private static int WrapTelemetryCursor(uint value, int capacity)
        {
            return capacity > 0 ? (int)(value % (uint)capacity) : 0;
        }

        private float ResolvePlayerDepthMeters(double3 playerAbsolute)
        {
            if (!math.isfinite(playerAbsolute.y))
                return 0f;

            double depthMeters = ResolveTelemetrySeaLevelY() - playerAbsolute.y;
            return (float)math.min(1000000d, math.max(0d, depthMeters));
        }

        private double ResolveTelemetrySeaLevelY()
        {
            IHectonOceanKinematicsService oceanKinematicsService = _oceanKinematicsService;
            IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null && oceanKinematicsService.IsInitialized
                ? oceanKinematicsService.ActiveProvider
                : null;
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TryResolveSeaLevelY(oceanKinematics.SeaLevel, out float seaLevelY))
            {
                return seaLevelY;
            }

            return DefaultSeaLevelY;
        }

        private static bool TryResolveSeaLevelY(float candidateSeaLevelY, out float seaLevelY)
        {
            if (math.isfinite(candidateSeaLevelY) &&
                math.abs(candidateSeaLevelY) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                seaLevelY = candidateSeaLevelY;
                return true;
            }

            seaLevelY = DefaultSeaLevelY;
            return false;
        }

        private void RecordTelemetry(in AbsoluteUniversePosition playerAup, float intensity01, float accumulatedRads, uint flags)
        {
            IDataVault vault = _dataVault;
            if (vault == null || _telemetryHandle.BufferID == 0u)
                return;

            bool hasPlayerAbsolute = AbsoluteUniversePosition.IsFinite(in playerAup);
            double3 playerAbsolute = hasPlayerAbsolute ? playerAup.ToAbsoluteDouble3() : double3.zero;
            float safeIntensity = SanitizeNonNegative(intensity01);
            float safeDose = SanitizeNonNegative(accumulatedRads);
            float safeShielding = Sanitize01(_lastShieldingFactor01);
            float safeDegradation = Sanitize01(_lastCellularDegradation01);
            RadiationTelemetryEntry entry = new RadiationTelemetryEntry
            {
                PlayerAup = playerAbsolute,
                PlayerDepthMeters = hasPlayerAbsolute ? ResolvePlayerDepthMeters(playerAbsolute) : 0f,
                CurrentExposureRate = safeIntensity,
                CumulativeDoseRad = safeDose,
                ShieldingFactor01 = safeShielding,
                CellularDegradation01 = safeDegradation,
                BurstExecutionMicroseconds = SanitizeNonNegative(_lastBurstExecutionMicroseconds),
                SourceCount = (ushort)math.clamp(_activeSourceCount, 0, ushort.MaxValue),
                SourceVersion = (ushort)math.clamp(_sourceVersion, 0, ushort.MaxValue),
                Frame = _currentSimulationFrame,
                ShiftSequence = _lastShiftSequence,
                Flags = flags
            };

            int nextWriteIndex = _telemetryWriteIndex;
            bool wrote = false;
            if (!vault.TryAcquireWriteLock(in _telemetryHandle, OwnerSystemId, out NativeArray<RadiationTelemetryEntry> telemetryRing))
                return;

            try
            {
                if (!telemetryRing.IsCreated)
                    return;

                int telemetryCapacity = math.min(telemetryRing.Length, TelemetryCapacity);
                if (telemetryCapacity <= 0)
                    return;

                int writeIndex = WrapTelemetryIndex(_telemetryWriteIndex, telemetryCapacity);
                nextWriteIndex = writeIndex + 1 >= telemetryCapacity ? 0 : writeIndex + 1;
                entry.Flags = ConsumeSignalDropFlags(flags);
                telemetryRing[writeIndex] = entry;
                wrote = true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _telemetryHandle, OwnerSystemId);
            }

            if (!wrote)
                return;

            _telemetryWriteIndex = nextWriteIndex;
            TryWriteTelemetryCursor(unchecked((uint)nextWriteIndex));
        }

        private static uint ConsumeSignalDropFlags(uint flags)
        {
            return Interlocked.Exchange(ref _signalPushDropCount, 0) > 0
                ? flags | RadiationTelemetryFlagSignalDrops
                : flags;
        }

        private void TryWriteTelemetryCursor(uint nextWriteIndex)
        {
            IDataVault vault = _dataVault;
            if (vault == null || _telemetryCursorHandle.BufferID == 0u)
                return;

            if (!vault.TryAcquireWriteLock(in _telemetryCursorHandle, OwnerSystemId, out NativeArray<uint> telemetryCursor))
                return;

            try
            {
                if (telemetryCursor.IsCreated && telemetryCursor.Length > 0)
                    telemetryCursor[0] = nextWriteIndex;
            }
            finally
            {
                vault.ReleaseWriteLock(in _telemetryCursorHandle, OwnerSystemId);
            }
        }

        private void DumpBlackBox()
        {
            if (_blackBoxDumpAttempted)
                return;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !vault.TryReadOnlyHandle(in _telemetryHandle, out NativeArray<RadiationTelemetryEntry>.ReadOnly telemetry) ||
                !telemetry.IsCreated ||
                telemetry.Length < TelemetryCapacity)
            {
                return;
            }

            _blackBoxDumpAttempted = true;
            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", RadiationDumpFileName));
                int headerBytes = 8;
                int stride = UnsafeUtility.SizeOf<RadiationTelemetryEntry>();
                int entryBytes = TelemetryCapacity * stride;
                int totalBytes = headerBytes + entryBytes;
                const string dumpPayloadLabel = "RadiationHazardGrid.BlackBoxDumpPayload";
                NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                    totalBytes,
                    nameof(RadiationHazardGrid),
                    dumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                try
                {
                    WriteUInt32LittleEndian(payload, 0, unchecked((uint)_telemetryWriteIndex));
                    WriteUInt32LittleEndian(payload, 4, (uint)TelemetryCapacity);
                    byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                    byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                    UnsafeUtility.MemCpy(target + headerBytes, source, entryBytes);
                    NativeFaultDumpWriter.TryWriteAll(path, payload, totalBytes);
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref payload,
                        nameof(RadiationHazardGrid),
                        dumpPayloadLabel);
                }
            }
            catch (Exception)
            {
            }
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> destination, int offset, uint value)
        {
            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !_sources.IsCreated)
                return;

            if (!TryResolvePlayerAup(ResolveActivePlayerRuntimeContext(), out AbsoluteUniversePosition playerAup))
                return;

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
                float radius = NormalizeSourceRadius(source.RadiusMeters);
                if (radius <= 0f)
                    continue;

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
            AbsoluteUniversePosition origin = RuntimeOriginRoute.CurrentRuntimeOriginAup();
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

        private static float NormalizeSourceRadius(float radiusMeters)
        {
            if (!math.isfinite(radiusMeters) || radiusMeters <= 0f)
                return 0f;

            return math.clamp(radiusMeters, 0.5f, MaxSourceRadiusMeters);
        }

        private static int Flatten(int x, int y, int z)
        {
            return x + y * GridResolution + z * GridResolution * GridResolution;
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
                DamagePerTickScale = RadiationStatusMagnitudeScale,
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

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct RadiationStatusSignal
        {
            [FieldOffset(0)] public uint TargetId;
            [FieldOffset(4)] public uint SourceId;
            [FieldOffset(8)] public float Magnitude01;
            [FieldOffset(12)] public uint Frame;
            [FieldOffset(16)] public ulong _pad0;
            [FieldOffset(24)] public ulong _pad1;
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
            public const int StatusSignalSizeBytes = 32;
            public const int TelemetrySizeBytes = 64;

            public static bool ValidateLayout()
            {
                bool sizesValid = UnsafeUtility.SizeOf<RadiationStateDTO>() == StateSizeBytes &&
                                  UnsafeUtility.SizeOf<RadiationStatusSignal>() == StatusSignalSizeBytes &&
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
                       GetOffset<RadiationStateDTO>(nameof(RadiationStateDTO._pad7)) == 31 &&
                       GetOffset<RadiationStatusSignal>(nameof(RadiationStatusSignal.TargetId)) == 0 &&
                       GetOffset<RadiationStatusSignal>(nameof(RadiationStatusSignal.SourceId)) == 4 &&
                       GetOffset<RadiationStatusSignal>(nameof(RadiationStatusSignal.Magnitude01)) == 8 &&
                       GetOffset<RadiationStatusSignal>(nameof(RadiationStatusSignal.Frame)) == 12 &&
                       GetOffset<RadiationTelemetryEntry>(nameof(RadiationTelemetryEntry.PlayerAup)) == 0 &&
                       GetOffset<RadiationTelemetryEntry>(nameof(RadiationTelemetryEntry.PlayerDepthMeters)) == 24 &&
                       GetOffset<RadiationTelemetryEntry>(nameof(RadiationTelemetryEntry.CurrentExposureRate)) == 28 &&
                       GetOffset<RadiationTelemetryEntry>(nameof(RadiationTelemetryEntry.CumulativeDoseRad)) == 32 &&
                       GetOffset<RadiationTelemetryEntry>(nameof(RadiationTelemetryEntry.ShieldingFactor01)) == 36 &&
                       GetOffset<RadiationTelemetryEntry>(nameof(RadiationTelemetryEntry.CellularDegradation01)) == 40 &&
                       GetOffset<RadiationTelemetryEntry>(nameof(RadiationTelemetryEntry.BurstExecutionMicroseconds)) == 44 &&
                       GetOffset<RadiationTelemetryEntry>(nameof(RadiationTelemetryEntry.Frame)) == 48 &&
                       GetOffset<RadiationTelemetryEntry>(nameof(RadiationTelemetryEntry.ShiftSequence)) == 52 &&
                       GetOffset<RadiationTelemetryEntry>(nameof(RadiationTelemetryEntry.SourceCount)) == 56 &&
                       GetOffset<RadiationTelemetryEntry>(nameof(RadiationTelemetryEntry.SourceVersion)) == 58 &&
                       GetOffset<RadiationTelemetryEntry>(nameof(RadiationTelemetryEntry.Flags)) == 60;
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

                if (!math.all(math.isfinite(PlayerAup)) || !math.all(math.isfinite(OffsetMeters)))
                    return;

                float safeIntensity = NormalizeSourceIntensity(Intensity01);
                if (safeIntensity <= 0f)
                    return;

                float safeRadius = NormalizeSourceRadius(RadiusMeters);
                if (safeRadius <= 0f)
                    safeRadius = DefaultSourceRadiusMeters;

                Sources[0] = new RadiationSource
                {
                    PositionAup = PlayerAup + (double3)OffsetMeters,
                    Intensity01 = safeIntensity,
                    RadiusMeters = safeRadius,
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
            [NativeDisableUnsafePtrRestriction, NoAlias] public RadiationStatusSignal* StatusSignal;
            [ReadOnly, NoAlias] public NativeArray<byte>.ReadOnly EncodedSdf;
            [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadStateDTO* BulkheadStates;
            [NativeDisableUnsafePtrRestriction, NoAlias] public BulkheadPlaneDTO* BulkheadPlanes;
            public int SourceCapacity;
            public int ActiveSourceCount;
            public int BulkheadCount;
            public double3 PlayerAup;
            public float3 PlayerRuntime;
            public uint HasPlayerAup;
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
                if (States == null || Sources == null || StatusSignal == null)
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
                bool hasPlayerAup =
                    HasPlayerAup != 0u &&
                    math.all(math.isfinite(PlayerAup)) &&
                    math.all(math.isfinite(PlayerRuntime));
                if (hasPlayerAup)
                {
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
                    StatusSignal[0] = new RadiationStatusSignal
                    {
                        TargetId = PlayerTargetId,
                        SourceId = RadiationCombatSourceId,
                        Magnitude01 = math.max(0f, (degradation - RadiationCriticalDegradation01) * safeDamageScale),
                        Frame = Frame
                    };
                }
                else
                {
                    StatusSignal[0] = default;
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
