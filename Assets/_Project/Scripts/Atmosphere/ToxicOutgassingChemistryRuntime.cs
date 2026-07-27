using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Core.Scheduling;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Atmosphere
{
    [DisallowMultipleComponent]
    public unsafe sealed partial class ToxicOutgassingChemistryRuntime : MonoBehaviour, ISlowTickable, IColdTickable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private static int s_x001ToxicOutgassingChemistryRuntimeSignalPushDropCount;
        public const int HighResolution = 32;
        public const int LowResolution = 16;
        public const int MaxCellCount = HighResolution * HighResolution * HighResolution;
        public const int MaxSourceCount = 128;
        public const int MaxEntityCount = 128;
        public const int MaxSignalsPerFrame = 64;
        public const int TelemetryCapacity = 300;
#if UNITY_EDITOR
        public const int CsvBufferBytes = 4096;
#endif
        public const int BinaryProbeBytes = 64;
        public const float DefaultCellSizeMeters = 10f;

        public const uint PoisonGasHash = 0x504F4953u;
        public const uint AcidChemicalHash = 0x41434944u;
        public const uint PurifierKelpHash = 0x504B454Cu;
        public const uint ToxicStatusTypeBit = 1u << 5;
        public const uint AcidStatusTypeBit = 1u << 8;

        private const ushort RuntimeSourceId = 65;
        private const byte RuntimeChannel = 65;
        private const SystemID OwnerSystemId = SystemID.HabitatAtmosphere;
        private const byte ToxicityExposureSignalFlagsActive = ToxicityExposureSignal.FlagHasSourceAup;
        private const byte ToxicBioluminescenceSignalFlagsActive = ToxicBioluminescenceSignal.FlagActive;
        private const byte SignalFlagsCorrosion = 4;
        private const byte TelemetryFlagMockChemistry = 1;
        private const float ToxicCorrosionStatusDurationSeconds = 2.0f;
        private const byte TelemetryFlagBinaryProbeFailure = 32;
        private const byte TelemetryFlagDumpFailure = 64;
        private const byte TelemetryFlagNaN = 128;
        private const float DefaultQualityWeight = 1f;
        private const float NaNEpsilon = 0.0001f;
        private const float SlowTickDeltaSeconds = 0.1f;
        private const float RebaseHalfCellBias = 0.5f;
        private const uint ToxicBlackBoxMagic = 0x38584F54u; // TOX8
        private const uint ToxicBlackBoxVersion = 1u;
        private const int ToxicBlackBoxHeaderBytes = 32;
        private const int ToxicBlackBoxEntryBytes = 64;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_TOXIC_SURGEON.bin";
        private const string DumpPayloadLabel = "ToxicOutgassingTelemetryDumpPayload";
#if UNITY_EDITOR
        private const string CsvRelativePath = "Data/Tuning/chemical_properties.csv";
#endif
        private const string LegacyBinaryRelativePath = "Data/Precomputed/gas_toxicity_tables.h8bin";
        private const string DaltonBinaryRelativePath = "Data/Precomputed/dalton_gas_toxicity.bin";

        private static readonly BufferID DensityFrontBufferId = BufferID.ToxicOutgassingDensityFront;
        private static readonly BufferID DensityBackBufferId = BufferID.ToxicOutgassingDensityBack;
        private static readonly BufferID FlowFieldBufferId = BufferID.ToxicOutgassingFlowField;
        private static readonly BufferID WorldSamplerBufferId = BufferID.ToxicOutgassingWorldSampler;
        private static readonly BufferID SourceBufferId = BufferID.ToxicOutgassingSources;
        private static readonly BufferID SourceIdBufferId = BufferID.ToxicOutgassingSourceIds;
        private static readonly BufferID EntityAupBufferId = BufferID.ToxicOutgassingEntityAups;
        private static readonly BufferID EntityIdBufferId = BufferID.ToxicOutgassingEntityIds;
        private static readonly BufferID EntityCorrosionTimerBufferId = BufferID.ToxicOutgassingEntityCorrosionTimers;
        private static readonly BufferID EntityExposureAccumulatorBufferId = BufferID.ToxicOutgassingEntityExposureAccumulators;
        private static readonly BufferID ExposureSignalBufferId = BufferID.ToxicOutgassingExposureSignals;
        private static readonly BufferID StatusSignalBufferId = BufferID.ToxicOutgassingStatusSignals;
        private static readonly BufferID BiolumSignalBufferId = BufferID.ToxicOutgassingBiolumSignals;
        private static readonly BufferID SignalCounterBufferId = BufferID.ToxicOutgassingSignalCounters;
        private static readonly BufferID TelemetryRingBufferId = BufferID.ToxicOutgassingTelemetryRing;
        private static readonly BufferID TelemetryScratchBufferId = BufferID.ToxicOutgassingTelemetryScratch;
        private static readonly BufferID ConstantsBufferId = BufferID.ToxicOutgassingConstants;
#if UNITY_EDITOR
        private static readonly BufferID CsvByteBufferId = BufferID.ToxicOutgassingCsvBytes;
#endif
        private static readonly BufferID BinaryProbeByteBufferId = BufferID.ToxicOutgassingBinaryProbeBytes;
        private static readonly BufferID NanFlagBufferId = BufferID.ToxicOutgassingNanFlags;
        private static readonly BufferID DensityMirrorBufferId = BufferID.ToxicOutgassingDensityMirror;
        private static readonly BufferID GridHeaderBufferId = BufferID.ToxicOutgassingGridHeader;
        private static readonly BufferID CellStateFrontBufferId = BufferID.ToxicOutgassingCellStatesFront;
        private static readonly BufferID CellStateBackBufferId = BufferID.ToxicOutgassingCellStatesBack;

        private VaultGenerationHandle<float> _densityFront;
        private VaultGenerationHandle<float> _densityBack;
        private VaultGenerationHandle<float> _densityMirror;
        private VaultGenerationHandle<MockFlowField> _flowField;
        private VaultGenerationHandle<MockWorldSampler> _worldSampler;
        private VaultGenerationHandle<ToxicitySourceDTO> _sources;
        private VaultGenerationHandle<uint> _sourceIds;
        private VaultGenerationHandle<double3> _entityAups;
        private VaultGenerationHandle<uint> _entityIds;
        private VaultGenerationHandle<float> _entityCorrosionTimers;
        private VaultGenerationHandle<float> _entityExposureAccumulators;
        private VaultGenerationHandle<ToxicityExposureSignal> _exposureSignals;
        private VaultGenerationHandle<ToxicityStatusSignal> _statusSignals;
        private VaultGenerationHandle<ToxicBioluminescenceSignal> _biolumSignals;
        private VaultGenerationHandle<int> _signalCounters;
        private VaultGenerationHandle<ToxicityGridTelemetryEntry> _telemetryRing;
        private VaultGenerationHandle<ToxicityGridTelemetryEntry> _telemetryScratch;
        private VaultGenerationHandle<ToxicOutgassingConstants> _constants;
#if UNITY_EDITOR
        private VaultGenerationHandle<byte> _csvBytes;
#endif
        private VaultGenerationHandle<byte> _binaryProbeBytes;
        private VaultGenerationHandle<int> _nanFlags;
        private VaultGenerationHandle<ToxicOutgassingGridHeaderDTO> _gridHeader;
        private VaultGenerationHandle<ToxicityStateDTO> _cellStatesFront;
        private VaultGenerationHandle<ToxicityStateDTO> _cellStatesBack;

        private IDataVault _vault;
        private JobHandle _scheduledHandle;
        private bool _hasScheduledWork;
        private int _activeResolution;
        private int _activeCellCount;
        private int _sourceCount;
        private int _entityCount;
        private int _telemetryCursor;
        private int _telemetryRetainedCount;
        private int _densityVersion;
        private uint _simulationFrameCounter;
        private float _cellSizeMeters;
        private float _simulationAccumulator;
        private float _corrosionAccumulator;
        private float _lastQualityWeight;
        private float _lastCompleteMs;
        private byte _pendingFailureFlags;
        private long _scheduledStartTicks;
        private double3 _gridOriginAup;
        private int3 _pendingRebaseCells;
        private bool _hasPendingRebase;
        private bool _nativeReady;
        private bool _mockChemistry;
        private bool _hotSwapRegistered;
        private bool _slowTickRegistered;
        private bool _coldTickRegistered;
        private bool _lateFrameTickRegistered;

        public static ToxicOutgassingChemistryRuntime Instance;

        public int ActiveResolution => _activeResolution;
        public int SourceCount => _sourceCount;
        public int EntityCount => _entityCount;
        public int DensityVersion => _densityVersion;
        public float CellSizeMeters => _cellSizeMeters;
        public double3 GridOriginAup => _gridOriginAup;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticRuntimeState()
        {
            Instance = null;
            Volatile.Write(ref s_x001ToxicOutgassingChemistryRuntimeSignalPushDropCount, 0);
        }

        public bool TryReadConstants(out ToxicOutgassingConstants constants)
        {
            if (!_nativeReady)
            {
                constants = default;
                return false;
            }

            if (!TryOpenBuffer(in _constants, ConstantsBufferId, out NativeArray<ToxicOutgassingConstants> constantsArray) ||
                constantsArray.Length == 0)
            {
                constants = default;
                return false;
            }

            constants = constantsArray[0];
            return true;
        }

        public bool TryWriteConstants(in ToxicOutgassingConstants constants)
        {
            if (!_nativeReady)
            {
                return false;
            }

            if (!TryOpenBuffer(in _constants, ConstantsBufferId, out NativeArray<ToxicOutgassingConstants> constantsArray) ||
                constantsArray.Length == 0)
            {
                return false;
            }

            constantsArray[0] = SanitizeConstants(constants);
            return true;
        }

        private void Awake()
        {
            Instance = this;
            EnsureNativeState();
        }

        private void OnEnable()
        {
            Instance = this;
            TryRegisterHotSwapListener();
            EnsureNativeState();
            _slowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            _coldTickRegistered = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment);
            _lateFrameTickRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            HectonFloatingOrigin.RegisterListener(this);
        }

        private void OnDisable()
        {
            if (_hasScheduledWork)
            {
                CompleteScheduledWorkForTeardown();
            }
            HectonFloatingOrigin.UnregisterListener(this);
            if (_lateFrameTickRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameTickRegistered = false;
            }

            if (_coldTickRegistered)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
                _coldTickRegistered = false;
            }

            if (_slowTickRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = false;
            }

            TryUnregisterHotSwapListener();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            ReleaseNativeStateForLifecycle();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            if (ReferenceEquals(_vault, currentService))
                return;

            IDataVault nextVault = currentService is IDataVault dataVault ? dataVault : null;
            RebindDataVaultForLifecycle(nextVault);

            if (isActiveAndEnabled && _vault != null)
                EnsureNativeState();
        }

        public void SlowTick()
        {
            if (!_nativeReady)
            {
                return;
            }

            _simulationAccumulator += SlowTickDeltaSeconds;
            _corrosionAccumulator += SlowTickDeltaSeconds;

            TryFinalizeScheduledWorkNoWait();
            if (_hasScheduledWork)
            {
                return;
            }

            float qualityWeight = ResolveQualityWeight();
            int targetResolution = ResolveResolution(qualityWeight);
            if (targetResolution != _activeResolution)
            {
                if (!TryResizeActiveGrid(targetResolution))
                {
                    return;
                }
            }

            float targetInterval = ResolveTickInterval(qualityWeight);
            if (_simulationAccumulator + 0.00001f < targetInterval)
            {
                return;
            }

            float simulationDelta = math.min(_simulationAccumulator, 0.5f);
            _simulationAccumulator = 0f;
            ScheduleSimulation(simulationDelta, qualityWeight);
        }

        public void ColdTick()
        {
            EnsureNativeState();
        }

        public void LateFrameTick()
        {
            if (_hasScheduledWork && _scheduledHandle.IsCompleted)
            {
                TryFinalizeScheduledWorkNoWait();
            }

            PublishShaderScalar();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftVector = shiftData.ShiftOffset;
            float3 shift = new float3(shiftVector.x, shiftVector.y, shiftVector.z);
            float shiftSqrMagnitude = math.lengthsq(shift);
            if (!math.all(math.isfinite(shift)) ||
                !math.isfinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.000001f ||
                !math.all(math.isfinite(shiftData.NewTotalOffsetDouble)))
            {
                return;
            }

            float invCell = math.rcp(math.max(_cellSizeMeters, NaNEpsilon));
            float3 cells = shift * invCell;
            _pendingRebaseCells += new int3((int)math.round(cells.x), (int)math.round(cells.y), (int)math.round(cells.z));
            _gridOriginAup = shiftData.NewTotalOffsetDouble;
            _hasPendingRebase = math.any(_pendingRebaseCells != int3.zero);
        }

        public bool TryUpsertSource(uint sourceId, double3 aup, float emissionRate, float density, uint chemicalHash)
        {
            if (!_nativeReady)
            {
                return false;
            }

            if (!TryOpenMutationWindow())
            {
                return false;
            }

            if (!math.all(math.isfinite(aup)) || !math.isfinite(emissionRate) || !math.isfinite(density))
            {
                return false;
            }

            NativeArray<ToxicitySourceDTO> sources = OpenBuffer(in _sources, SourceBufferId);
            NativeArray<uint> sourceIds = OpenBuffer(in _sourceIds, SourceIdBufferId);
            int index = FindSourceIndex(sourceIds, sourceId, _sourceCount);
            if (index < 0)
            {
                if (_sourceCount >= MaxSourceCount)
                {
                    return false;
                }

                index = _sourceCount;
                _sourceCount++;
            }

            sourceIds[index] = sourceId;
            sources[index] = new ToxicitySourceDTO
            {
                AUP = aup,
                EmissionRate = math.max(0f, emissionRate),
                Density = math.clamp(density, 0f, 1f),
                ChemicalHash = chemicalHash == 0u ? PoisonGasHash : chemicalHash,
                _pad0 = 0u,
                _pad1 = 0ul
            };
            UpdateGridHeader();
            return true;
        }

        public bool TryRemoveSource(uint sourceId)
        {
            if (!_nativeReady)
            {
                return false;
            }

            if (!TryOpenMutationWindow())
            {
                return false;
            }

            NativeArray<ToxicitySourceDTO> sources = OpenBuffer(in _sources, SourceBufferId);
            NativeArray<uint> sourceIds = OpenBuffer(in _sourceIds, SourceIdBufferId);
            int index = FindSourceIndex(sourceIds, sourceId, _sourceCount);
            if (index < 0)
            {
                return false;
            }

            int last = _sourceCount - 1;
            if (index != last)
            {
                sources[index] = sources[last];
                sourceIds[index] = sourceIds[last];
            }

            sources[last] = default;
            sourceIds[last] = 0u;
            _sourceCount = math.max(0, _sourceCount - 1);
            UpdateGridHeader();
            return true;
        }

        public bool TryUpsertEntity(uint entityId, double3 aup)
        {
            if (!_nativeReady)
            {
                return false;
            }

            if (!TryOpenMutationWindow())
            {
                return false;
            }

            if (!math.all(math.isfinite(aup)))
            {
                return false;
            }

            NativeArray<double3> entityAups = OpenBuffer(in _entityAups, EntityAupBufferId);
            NativeArray<uint> entityIds = OpenBuffer(in _entityIds, EntityIdBufferId);
            int index = FindEntityIndex(entityIds, entityId, _entityCount);
            if (index < 0)
            {
                if (_entityCount >= MaxEntityCount)
                {
                    return false;
                }

                index = _entityCount;
                _entityCount++;
            }

            entityIds[index] = entityId;
            entityAups[index] = aup;
            UpdateGridHeader();
            return true;
        }

        public bool TryRemoveEntity(uint entityId)
        {
            if (!_nativeReady)
            {
                return false;
            }

            if (!TryOpenMutationWindow())
            {
                return false;
            }

            NativeArray<double3> entityAups = OpenBuffer(in _entityAups, EntityAupBufferId);
            NativeArray<uint> entityIds = OpenBuffer(in _entityIds, EntityIdBufferId);
            NativeArray<float> timers = OpenBuffer(in _entityCorrosionTimers, EntityCorrosionTimerBufferId);
            NativeArray<float> accumulators = OpenBuffer(in _entityExposureAccumulators, EntityExposureAccumulatorBufferId);
            int index = FindEntityIndex(entityIds, entityId, _entityCount);
            if (index < 0)
            {
                return false;
            }

            int last = _entityCount - 1;
            if (index != last)
            {
                entityAups[index] = entityAups[last];
                entityIds[index] = entityIds[last];
                timers[index] = timers[last];
                accumulators[index] = accumulators[last];
            }

            entityAups[last] = default;
            entityIds[last] = 0u;
            timers[last] = 0f;
            accumulators[last] = 0f;
            _entityCount = math.max(0, _entityCount - 1);
            UpdateGridHeader();
            return true;
        }

        public bool TrySampleDensity(double3 aup, out float density)
        {
            density = 0f;
            if (!_nativeReady || !math.all(math.isfinite(aup)))
            {
                return false;
            }

            NativeArray<float> front = OpenBuffer(in _densityFront, DensityFrontBufferId);
            float sampleBlend = Smooth01((ReadCachedQualityWeight() - 0.28f) * 1.6f);
            float nearest = SampleDensityNearest(front, _activeResolution, _gridOriginAup, _cellSizeMeters, aup);
            density = nearest;
            if (sampleBlend > 0.0001f)
            {
                density = math.lerp(nearest, SampleDensityTrilinear(front, _activeResolution, _gridOriginAup, _cellSizeMeters, aup), sampleBlend);
            }

            return math.isfinite(density);
        }

        public bool TryGetGridReadback(out NativeArray<float>.ReadOnly density, out int resolution, out double3 originAup, out float cellSize, out int version)
        {
            if (!_nativeReady)
            {
                density = default;
                resolution = 0;
                originAup = default;
                cellSize = 0f;
                version = 0;
                return false;
            }

            NativeArray<float> densityBuffer = OpenBuffer(in _densityFront, DensityFrontBufferId);
            density = densityBuffer.AsReadOnly();
            resolution = _activeResolution;
            originAup = _gridOriginAup;
            cellSize = _cellSizeMeters;
            version = _densityVersion;
            return densityBuffer.IsCreated;
        }

        public bool TryGetGridHeader(out ToxicOutgassingGridHeaderDTO header)
        {
            header = default;
            if (!_nativeReady || !IsOwnedVaultHandle(in _gridHeader, GridHeaderBufferId))
            {
                return false;
            }

            NativeArray<ToxicOutgassingGridHeaderDTO> headers = OpenBuffer(in _gridHeader, GridHeaderBufferId);
            if (!headers.IsCreated || headers.Length == 0)
            {
                return false;
            }

            header = headers[0];
            return true;
        }

        public bool TryGetCellStates(out NativeArray<ToxicityStateDTO>.ReadOnly states)
        {
            states = default;
            if (!_nativeReady || !IsOwnedVaultHandle(in _cellStatesFront, CellStateFrontBufferId))
            {
                return false;
            }

            NativeArray<ToxicityStateDTO> stateBuffer = OpenBuffer(in _cellStatesFront, CellStateFrontBufferId);
            states = stateBuffer.AsReadOnly();
            return stateBuffer.IsCreated;
        }

        public void GenerateEmergencyMockChemistry()
        {
            if (!EnsureNativeState())
            {
                return;
            }

            var constants = new ToxicOutgassingConstants
            {
                BaseDiffusionRate = 0.17f,
                CurrentAdvectionMultiplier = 0.85f,
                AcidCorrosionDamage = 0.045f,
                FloraAbsorptionRate = 0.11f,
                DensityDecayPerSecond = 0.032f,
                SourceRadiusMeters = 42f,
                ExposureToxemiaMultiplier = 0.075f,
                CausticDensityThreshold = 0.58f,
                BiolumDensityThreshold = 0.34f,
                MaxDensity = 1f,
                RadialFallbackRadiusScale = 1.65f,
                SdfWallLeakScale = 0f,
                ChemistryFlags = 1u,
                _pad0 = 0u
            };
            constants.GlobalQualityWeight = ResolveRuntimeQualityWeight01();
            constants.SimulationTickDelta = ResolveTickInterval(constants.GlobalQualityWeight);
            TryWriteConstants(in constants);
            _mockChemistry = true;
        }

#if UNITY_EDITOR
        public bool TryReloadCsvOverrides()
        {
            if (!EnsureNativeState())
            {
                return false;
            }

            try
            {
                string path = Path.Combine(ProjectRootPath(), CsvRelativePath);
                if (!File.Exists(path))
                {
                    return false;
                }

                NativeArray<byte> csvBytes = OpenBuffer(in _csvBytes, CsvByteBufferId);
                int length = FillByteBufferFromFile(path, csvBytes);
                if (length <= 0)
                {
                    return false;
                }

                if (!TryReadConstants(out ToxicOutgassingConstants constants))
                {
                    return false;
                }

                ParseChemicalCsv(csvBytes, length, ref constants);
                return TryWriteConstants(in constants);
            }
            catch (IOException)
            {
                MarkFailure(TelemetryFlagBinaryProbeFailure);
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                MarkFailure(TelemetryFlagBinaryProbeFailure);
                return false;
            }
            catch (ArgumentException)
            {
                MarkFailure(TelemetryFlagBinaryProbeFailure);
                return false;
            }
        }
#endif

        private bool EnsureNativeState()
        {
            IDataVault currentVault = GlobalRegistry.DataVault;
            if (!ReferenceEquals(_vault, currentVault))
                RebindDataVaultForLifecycle(currentVault);

            if (_nativeReady)
            {
                return true;
            }

            _vault = EnsureVault();
            _densityFront = AcquireBuffer<float>(DensityFrontBufferId, MaxCellCount);
            _densityBack = AcquireBuffer<float>(DensityBackBufferId, MaxCellCount);
            _densityMirror = AcquireBuffer<float>(DensityMirrorBufferId, MaxCellCount);
            _flowField = AcquireBuffer<MockFlowField>(FlowFieldBufferId, MaxCellCount);
            _worldSampler = AcquireBuffer<MockWorldSampler>(WorldSamplerBufferId, MaxCellCount);
            _sources = AcquireBuffer<ToxicitySourceDTO>(SourceBufferId, MaxSourceCount);
            _sourceIds = AcquireBuffer<uint>(SourceIdBufferId, MaxSourceCount);
            _entityAups = AcquireBuffer<double3>(EntityAupBufferId, MaxEntityCount);
            _entityIds = AcquireBuffer<uint>(EntityIdBufferId, MaxEntityCount);
            _entityCorrosionTimers = AcquireBuffer<float>(EntityCorrosionTimerBufferId, MaxEntityCount);
            _entityExposureAccumulators = AcquireBuffer<float>(EntityExposureAccumulatorBufferId, MaxEntityCount);
            _exposureSignals = AcquireBuffer<ToxicityExposureSignal>(ExposureSignalBufferId, MaxSignalsPerFrame);
            _statusSignals = AcquireBuffer<ToxicityStatusSignal>(StatusSignalBufferId, MaxSignalsPerFrame);
            _biolumSignals = AcquireBuffer<ToxicBioluminescenceSignal>(BiolumSignalBufferId, MaxSignalsPerFrame);
            _signalCounters = AcquireBuffer<int>(SignalCounterBufferId, 4);
            _telemetryRing = AcquireBuffer<ToxicityGridTelemetryEntry>(TelemetryRingBufferId, TelemetryCapacity);
            _telemetryScratch = AcquireBuffer<ToxicityGridTelemetryEntry>(TelemetryScratchBufferId, 1);
            _constants = AcquireBuffer<ToxicOutgassingConstants>(ConstantsBufferId, 1);
#if UNITY_EDITOR
            _csvBytes = AcquireBuffer<byte>(CsvByteBufferId, CsvBufferBytes);
#endif
            _binaryProbeBytes = AcquireBuffer<byte>(BinaryProbeByteBufferId, BinaryProbeBytes);
            _nanFlags = AcquireBuffer<int>(NanFlagBufferId, MaxCellCount);
            _gridHeader = AcquireBuffer<ToxicOutgassingGridHeaderDTO>(GridHeaderBufferId, 1);
            _cellStatesFront = AcquireBuffer<ToxicityStateDTO>(CellStateFrontBufferId, MaxCellCount);
            _cellStatesBack = AcquireBuffer<ToxicityStateDTO>(CellStateBackBufferId, MaxCellCount);

            if (!AreNativeHandlesReady())
            {
                _nativeReady = false;
                return false;
            }

            ClearAllNativeBuffersWithMemClear();
            _activeResolution = ResolveResolution(ResolveRuntimeQualityWeight01());
            _activeCellCount = _activeResolution * _activeResolution * _activeResolution;
            _cellSizeMeters = DefaultCellSizeMeters;
            _gridOriginAup = ResolveCurrentRuntimeOriginDouble3();
            _lastQualityWeight = ResolveRuntimeQualityWeight01();
            _nativeReady = true;
            PrewarmSignalLanes();
            GenerateEmergencyMockChemistry();
#if UNITY_EDITOR
            TryReloadCsvOverrides();
#endif
            ProbeColdBinaryPayloads();
            UpdateGridHeader();
            return true;
        }

        private bool AreNativeHandlesReady()
        {
            return IsOwnedVaultHandle(in _densityFront, DensityFrontBufferId) &&
                   IsOwnedVaultHandle(in _densityBack, DensityBackBufferId) &&
                   IsOwnedVaultHandle(in _densityMirror, DensityMirrorBufferId) &&
                   IsOwnedVaultHandle(in _flowField, FlowFieldBufferId) &&
                   IsOwnedVaultHandle(in _worldSampler, WorldSamplerBufferId) &&
                   IsOwnedVaultHandle(in _sources, SourceBufferId) &&
                   IsOwnedVaultHandle(in _sourceIds, SourceIdBufferId) &&
                   IsOwnedVaultHandle(in _entityAups, EntityAupBufferId) &&
                   IsOwnedVaultHandle(in _entityIds, EntityIdBufferId) &&
                   IsOwnedVaultHandle(in _entityCorrosionTimers, EntityCorrosionTimerBufferId) &&
                   IsOwnedVaultHandle(in _entityExposureAccumulators, EntityExposureAccumulatorBufferId) &&
                   IsOwnedVaultHandle(in _exposureSignals, ExposureSignalBufferId) &&
                   IsOwnedVaultHandle(in _statusSignals, StatusSignalBufferId) &&
                   IsOwnedVaultHandle(in _biolumSignals, BiolumSignalBufferId) &&
                   IsOwnedVaultHandle(in _signalCounters, SignalCounterBufferId) &&
                   IsOwnedVaultHandle(in _telemetryRing, TelemetryRingBufferId) &&
                   IsOwnedVaultHandle(in _telemetryScratch, TelemetryScratchBufferId) &&
                   IsOwnedVaultHandle(in _constants, ConstantsBufferId) &&
#if UNITY_EDITOR
                   IsOwnedVaultHandle(in _csvBytes, CsvByteBufferId) &&
#endif
                   IsOwnedVaultHandle(in _binaryProbeBytes, BinaryProbeByteBufferId) &&
                   IsOwnedVaultHandle(in _nanFlags, NanFlagBufferId) &&
                   IsOwnedVaultHandle(in _gridHeader, GridHeaderBufferId) &&
                   IsOwnedVaultHandle(in _cellStatesFront, CellStateFrontBufferId) &&
                   IsOwnedVaultHandle(in _cellStatesBack, CellStateBackBufferId);
        }

        private static double3 ResolveCurrentRuntimeOriginDouble3()
        {
            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            return originAup.IsFinite()
                ? originAup.ToAbsoluteDouble3()
                : double3.zero;
        }

        private IDataVault EnsureVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!ReferenceEquals(_vault, vault))
                RebindDataVaultForLifecycle(vault);

            return _vault;
        }

        private VaultGenerationHandle<T> AcquireBuffer<T>(BufferID id, int length) where T : struct
        {
            IDataVault vault = EnsureVault();
            if (vault == null || vault.IsCompactionFenceActive)
            {
                return default;
            }

            if (vault.IsAllocationLocked)
            {
                return vault.TryGetGenerationHandle(id, out VaultGenerationHandle<T> existing) &&
                       IsOwnedVaultHandle(in existing, id)
                    ? existing
                    : default;
            }

            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(id, length, OwnerSystemId, NativeArrayOptions.UninitializedMemory);
            return IsOwnedVaultHandle(in handle, id) ? handle : default;
        }

        private static bool IsOwnedVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == (uint)expectedBufferId &&
                   handle.SystemID == (uint)OwnerSystemId &&
                   handle.Generation != 0u;
        }

        private void RebindDataVaultForLifecycle(IDataVault nextVault)
        {
            if (ReferenceEquals(_vault, nextVault))
                return;

            ReleaseNativeStateForLifecycle();
            _vault = nextVault;
        }

        private void ReleaseNativeStateForLifecycle()
        {
            if (_hasScheduledWork)
                CompleteScheduledWorkForTeardown();

            ReleaseNativeHandleDescriptors(_vault);
            ResetNativeRuntimeState();
            _vault = null;
            _nativeReady = false;
        }

        private bool TryOpenBuffer<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _vault;
            return IsOwnedVaultHandle(in handle, expectedBufferId) &&
                   vault != null &&
                   !vault.IsCompactionFenceActive &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private NativeArray<T> OpenBuffer<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return TryOpenBuffer(in handle, expectedBufferId, out NativeArray<T> buffer)
                ? buffer
                : default;
        }

        private void ReleaseNativeHandleDescriptors(IDataVault vault)
        {
            ReleaseNativeHandle(vault, ref _densityFront, DensityFrontBufferId);
            ReleaseNativeHandle(vault, ref _densityBack, DensityBackBufferId);
            ReleaseNativeHandle(vault, ref _densityMirror, DensityMirrorBufferId);
            ReleaseNativeHandle(vault, ref _flowField, FlowFieldBufferId);
            ReleaseNativeHandle(vault, ref _worldSampler, WorldSamplerBufferId);
            ReleaseNativeHandle(vault, ref _sources, SourceBufferId);
            ReleaseNativeHandle(vault, ref _sourceIds, SourceIdBufferId);
            ReleaseNativeHandle(vault, ref _entityAups, EntityAupBufferId);
            ReleaseNativeHandle(vault, ref _entityIds, EntityIdBufferId);
            ReleaseNativeHandle(vault, ref _entityCorrosionTimers, EntityCorrosionTimerBufferId);
            ReleaseNativeHandle(vault, ref _entityExposureAccumulators, EntityExposureAccumulatorBufferId);
            ReleaseNativeHandle(vault, ref _exposureSignals, ExposureSignalBufferId);
            ReleaseNativeHandle(vault, ref _statusSignals, StatusSignalBufferId);
            ReleaseNativeHandle(vault, ref _biolumSignals, BiolumSignalBufferId);
            ReleaseNativeHandle(vault, ref _signalCounters, SignalCounterBufferId);
            ReleaseNativeHandle(vault, ref _telemetryRing, TelemetryRingBufferId);
            ReleaseNativeHandle(vault, ref _telemetryScratch, TelemetryScratchBufferId);
            ReleaseNativeHandle(vault, ref _constants, ConstantsBufferId);
#if UNITY_EDITOR
            ReleaseNativeHandle(vault, ref _csvBytes, CsvByteBufferId);
#endif
            ReleaseNativeHandle(vault, ref _binaryProbeBytes, BinaryProbeByteBufferId);
            ReleaseNativeHandle(vault, ref _nanFlags, NanFlagBufferId);
            ReleaseNativeHandle(vault, ref _gridHeader, GridHeaderBufferId);
            ReleaseNativeHandle(vault, ref _cellStatesFront, CellStateFrontBufferId);
            ReleaseNativeHandle(vault, ref _cellStatesBack, CellStateBackBufferId);
        }

        private static void ReleaseNativeHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle, BufferID expectedBufferId)
            where T : struct
        {
            if (vault != null &&
                IsOwnedVaultHandle(in handle, expectedBufferId))
            {
                vault.ReleaseBuffer(in handle);
            }

            handle = default;
        }

        private void ClearNativeHandleDescriptors()
        {
            _densityFront = default;
            _densityBack = default;
            _densityMirror = default;
            _flowField = default;
            _worldSampler = default;
            _sources = default;
            _sourceIds = default;
            _entityAups = default;
            _entityIds = default;
            _entityCorrosionTimers = default;
            _entityExposureAccumulators = default;
            _exposureSignals = default;
            _statusSignals = default;
            _biolumSignals = default;
            _signalCounters = default;
            _telemetryRing = default;
            _telemetryScratch = default;
            _constants = default;
#if UNITY_EDITOR
            _csvBytes = default;
#endif
            _binaryProbeBytes = default;
            _nanFlags = default;
            _gridHeader = default;
            _cellStatesFront = default;
            _cellStatesBack = default;
        }

        private void ResetNativeRuntimeState()
        {
            ClearNativeHandleDescriptors();
            _activeResolution = 0;
            _activeCellCount = 0;
            _sourceCount = 0;
            _entityCount = 0;
            _telemetryCursor = 0;
            _telemetryRetainedCount = 0;
            _densityVersion = 0;
            _simulationFrameCounter = 0u;
            _cellSizeMeters = 0f;
            _simulationAccumulator = 0f;
            _corrosionAccumulator = 0f;
            _lastQualityWeight = 0f;
            _lastCompleteMs = 0f;
            _pendingFailureFlags = 0;
            _scheduledStartTicks = 0L;
            _gridOriginAup = double3.zero;
            _pendingRebaseCells = int3.zero;
            _hasPendingRebase = false;
            _mockChemistry = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void ClearAllNativeBuffersWithMemClear()
        {
            MemClearArray(OpenBuffer(in _densityFront, DensityFrontBufferId));
            MemClearArray(OpenBuffer(in _densityBack, DensityBackBufferId));
            MemClearArray(OpenBuffer(in _densityMirror, DensityMirrorBufferId));
            MemClearArray(OpenBuffer(in _flowField, FlowFieldBufferId));
            MemClearArray(OpenBuffer(in _worldSampler, WorldSamplerBufferId));
            MemClearArray(OpenBuffer(in _sources, SourceBufferId));
            MemClearArray(OpenBuffer(in _sourceIds, SourceIdBufferId));
            MemClearArray(OpenBuffer(in _entityAups, EntityAupBufferId));
            MemClearArray(OpenBuffer(in _entityIds, EntityIdBufferId));
            MemClearArray(OpenBuffer(in _entityCorrosionTimers, EntityCorrosionTimerBufferId));
            MemClearArray(OpenBuffer(in _entityExposureAccumulators, EntityExposureAccumulatorBufferId));
            MemClearArray(OpenBuffer(in _exposureSignals, ExposureSignalBufferId));
            MemClearArray(OpenBuffer(in _statusSignals, StatusSignalBufferId));
            MemClearArray(OpenBuffer(in _biolumSignals, BiolumSignalBufferId));
            MemClearArray(OpenBuffer(in _signalCounters, SignalCounterBufferId));
            MemClearArray(OpenBuffer(in _telemetryRing, TelemetryRingBufferId));
            MemClearArray(OpenBuffer(in _telemetryScratch, TelemetryScratchBufferId));
            MemClearArray(OpenBuffer(in _constants, ConstantsBufferId));
#if UNITY_EDITOR
            MemClearArray(OpenBuffer(in _csvBytes, CsvByteBufferId));
#endif
            MemClearArray(OpenBuffer(in _binaryProbeBytes, BinaryProbeByteBufferId));
            MemClearArray(OpenBuffer(in _nanFlags, NanFlagBufferId));
            MemClearArray(OpenBuffer(in _gridHeader, GridHeaderBufferId));
            MemClearArray(OpenBuffer(in _cellStatesFront, CellStateFrontBufferId));
            MemClearArray(OpenBuffer(in _cellStatesBack, CellStateBackBufferId));
            _telemetryCursor = 0;
            _telemetryRetainedCount = 0;
        }

        private static void MemClearArray<T>(NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated || array.Length <= 0)
            {
                return;
            }

            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(array);
            UnsafeUtility.MemClear(ptr, (long)array.Length * UnsafeUtility.SizeOf<T>());
        }

        private bool TryResizeActiveGrid(int targetResolution)
        {
            TryFinalizeScheduledWorkNoWait();
            if (_hasScheduledWork)
            {
                return false;
            }

            _activeResolution = targetResolution;
            _activeCellCount = targetResolution * targetResolution * targetResolution;
            NativeArray<float> front = OpenBuffer(in _densityFront, DensityFrontBufferId);
            NativeArray<float> back = OpenBuffer(in _densityBack, DensityBackBufferId);
            NativeArray<float> mirror = OpenBuffer(in _densityMirror, DensityMirrorBufferId);
            NativeArray<MockFlowField> flow = OpenBuffer(in _flowField, FlowFieldBufferId);
            NativeArray<MockWorldSampler> world = OpenBuffer(in _worldSampler, WorldSamplerBufferId);
            NativeArray<ToxicityStateDTO> stateFront = OpenBuffer(in _cellStatesFront, CellStateFrontBufferId);
            NativeArray<ToxicityStateDTO> stateBack = OpenBuffer(in _cellStatesBack, CellStateBackBufferId);
            NativeArray<int> nan = OpenBuffer(in _nanFlags, NanFlagBufferId);

            MemClearArray(front);
            MemClearArray(back);
            MemClearArray(mirror);
            MemClearArray(flow);
            MemClearArray(world);
            MemClearArray(stateFront);
            MemClearArray(stateBack);
            MemClearArray(nan);
            _densityVersion++;
            UpdateGridHeader();
            return true;
        }

        private void ScheduleSimulation(float simulationDelta, float qualityWeight)
        {
            _scheduledStartTicks = Stopwatch.GetTimestamp();
            NativeArray<float> front = OpenBuffer(in _densityFront, DensityFrontBufferId);
            NativeArray<float> back = OpenBuffer(in _densityBack, DensityBackBufferId);
            NativeArray<float> mirror = OpenBuffer(in _densityMirror, DensityMirrorBufferId);
            NativeArray<MockFlowField> flow = OpenBuffer(in _flowField, FlowFieldBufferId);
            NativeArray<MockWorldSampler> world = OpenBuffer(in _worldSampler, WorldSamplerBufferId);
            NativeArray<ToxicitySourceDTO> sources = OpenBuffer(in _sources, SourceBufferId);
            NativeArray<uint> sourceIds = OpenBuffer(in _sourceIds, SourceIdBufferId);
            NativeArray<double3> entityAups = OpenBuffer(in _entityAups, EntityAupBufferId);
            NativeArray<uint> entityIds = OpenBuffer(in _entityIds, EntityIdBufferId);
            NativeArray<float> corrosionTimers = OpenBuffer(in _entityCorrosionTimers, EntityCorrosionTimerBufferId);
            NativeArray<float> exposureAccumulators = OpenBuffer(in _entityExposureAccumulators, EntityExposureAccumulatorBufferId);
            NativeArray<ToxicityExposureSignal> exposureSignals = OpenBuffer(in _exposureSignals, ExposureSignalBufferId);
            NativeArray<ToxicityStatusSignal> statusSignals = OpenBuffer(in _statusSignals, StatusSignalBufferId);
            NativeArray<ToxicBioluminescenceSignal> biolumSignals = OpenBuffer(in _biolumSignals, BiolumSignalBufferId);
            NativeArray<int> signalCounters = OpenBuffer(in _signalCounters, SignalCounterBufferId);
            NativeArray<int> nanFlags = OpenBuffer(in _nanFlags, NanFlagBufferId);
            NativeArray<ToxicityStateDTO> cellStatesBack = OpenBuffer(in _cellStatesBack, CellStateBackBufferId);
            NativeArray<ToxicityGridTelemetryEntry> scratch = OpenBuffer(in _telemetryScratch, TelemetryScratchBufferId);

            MemClearArray(signalCounters);
            MemClearArray(nanFlags);

            ToxicOutgassingConstants constants = TryReadConstants(out ToxicOutgassingConstants currentConstants)
                ? currentConstants
                : default;
            constants.GlobalQualityWeight = qualityWeight;
            constants.SimulationTickDelta = simulationDelta;
            constants = SanitizeConstants(constants);
            TryWriteConstants(in constants);

            int sourceBudget = ResolveSourceBudget(qualityWeight, _sourceCount);
            int3 pendingRebase = _pendingRebaseCells;
            bool runRebase = _hasPendingRebase && math.any(pendingRebase != int3.zero);
            _pendingRebaseCells = int3.zero;
            _hasPendingRebase = false;
            uint simulationFrame = ++_simulationFrameCounter;

            JobHandle dependency = default;
            NativeArray<float> diffusionRead = front;
            if (runRebase)
            {
                var rebaseJob = new RebaseGridJob
                {
                    Front = front,
                    Back = mirror,
                    RebaseCells = pendingRebase,
                    Resolution = _activeResolution
                };
                dependency = rebaseJob.Schedule(_activeCellCount, 128);
                diffusionRead = mirror;
            }

            var flowJob = new MockFlowFieldJob
            {
                FlowField = flow,
                GridOriginAup = _gridOriginAup,
                CellSizeMeters = _cellSizeMeters,
                Resolution = _activeResolution,
                GlobalQualityWeight = qualityWeight,
                Frame = simulationFrame
            };
            JobHandle samplingDependency = dependency;
            JobHandle flowHandle = flowJob.Schedule(_activeCellCount, 128, samplingDependency);

            var worldJob = new MockWorldSamplerJob
            {
                WorldSamples = world,
                GridOriginAup = _gridOriginAup,
                CellSizeMeters = _cellSizeMeters,
                Resolution = _activeResolution,
                GlobalQualityWeight = qualityWeight,
                PurifierKelpHashValue = PurifierKelpHash
            };
            JobHandle worldHandle = worldJob.Schedule(_activeCellCount, 128, samplingDependency);
            dependency = JobHandle.CombineDependencies(flowHandle, worldHandle);

            var diffusionJob = new ToxicDiffusionJob
            {
                Front = diffusionRead,
                Back = back,
                FlowField = flow,
                WorldSamples = world,
                Sources = sources,
                States = cellStatesBack,
                NanFlags = nanFlags,
                Constants = constants,
                GridOriginAup = _gridOriginAup,
                CellSizeMeters = _cellSizeMeters,
                Resolution = _activeResolution,
                SourceCount = _sourceCount,
                SourceBudget = sourceBudget,
                DeltaTime = simulationDelta,
                GlobalQualityWeight = qualityWeight,
                ChemicalHash = PoisonGasHash,
                Frame = simulationFrame
            };
            dependency = diffusionJob.Schedule(_activeCellCount, 128, dependency);

            var exposureJob = new EntityExposureJob
            {
                Density = back,
                EntityAups = entityAups,
                EntityIds = entityIds,
                CorrosionTimers = corrosionTimers,
                ExposureAccumulators = exposureAccumulators,
                ExposureSignals = exposureSignals,
                StatusSignals = statusSignals,
                SignalCounters = signalCounters,
                Constants = constants,
                GridOriginAup = _gridOriginAup,
                CellSizeMeters = _cellSizeMeters,
                Resolution = _activeResolution,
                EntityCount = _entityCount,
                DeltaTime = simulationDelta,
                CorrosionDelta = _corrosionAccumulator,
                GlobalQualityWeight = qualityWeight,
                Frame = simulationFrame,
                ToxicStatusType = ToxicStatusTypeBit | AcidStatusTypeBit,
                AcidChemicalHashValue = AcidChemicalHash,
                RuntimeSourceId = RuntimeSourceId,
                RuntimeChannel = RuntimeChannel
            };
            dependency = exposureJob.Schedule(dependency);
            _corrosionAccumulator = 0f;

            var biolumJob = new SignalHarvestJob
            {
                Density = back,
                WorldSamples = world,
                BiolumSignals = biolumSignals,
                SignalCounters = signalCounters,
                Constants = constants,
                GridOriginAup = _gridOriginAup,
                CellSizeMeters = _cellSizeMeters,
                Resolution = _activeResolution,
                GlobalQualityWeight = qualityWeight,
                Frame = simulationFrame,
                ChemicalHash = PoisonGasHash
            };
            dependency = biolumJob.Schedule(dependency);

            var telemetryJob = new ScanTelemetryJob
            {
                Density = back,
                NanFlags = nanFlags,
                Scratch = scratch,
                GridOriginAup = _gridOriginAup,
                CellSizeMeters = _cellSizeMeters,
                Resolution = _activeResolution,
                CellCount = _activeCellCount,
                SourceCount = _sourceCount,
                EntityCount = _entityCount,
                GlobalQualityWeight = qualityWeight,
                Frame = simulationFrame,
                Flags = (byte)(_mockChemistry ? TelemetryFlagMockChemistry : 0)
            };
            dependency = telemetryJob.Schedule(dependency);

            _scheduledHandle = dependency;
            _hasScheduledWork = true;
            H8Memory.RegisterActiveJob(OwnerSystemId, dependency);
        }

        private bool TryFinalizeScheduledWorkNoWait()
        {
            if (!_hasScheduledWork)
            {
                return true;
            }

            if (!_scheduledHandle.IsCompleted)
            {
                return false;
            }

            long completeStart = Stopwatch.GetTimestamp();
            if (!DispatcherJobFence.TryFinalizeCompleted(ref _scheduledHandle))
            {
                return false;
            }

            FinishScheduledWork(completeStart);
            return true;
        }

        private void CompleteScheduledWorkForTeardown()
        {
            if (!_hasScheduledWork)
                return;

            long completeStart = Stopwatch.GetTimestamp();
            if (!DispatcherJobFence.TryComplete(ref _scheduledHandle, forceComplete: true))
                return;

            FinishScheduledWork(completeStart);
        }

        private void FinishScheduledWork(long completeStart)
        {
            long completeEnd = Stopwatch.GetTimestamp();
            long scheduleStart = _scheduledStartTicks != 0L ? _scheduledStartTicks : completeStart;
            _lastCompleteMs = (float)((completeEnd - scheduleStart) * 1000.0 / Stopwatch.Frequency);
            _scheduledStartTicks = 0L;
            _hasScheduledWork = false;

            NativeArray<ToxicityExposureSignal> exposureSignals = OpenBuffer(in _exposureSignals, ExposureSignalBufferId);
            NativeArray<ToxicityStatusSignal> statusSignals = OpenBuffer(in _statusSignals, StatusSignalBufferId);
            NativeArray<ToxicBioluminescenceSignal> biolumSignals = OpenBuffer(in _biolumSignals, BiolumSignalBufferId);
            NativeArray<int> signalCounters = OpenBuffer(in _signalCounters, SignalCounterBufferId);
            NativeArray<ToxicityGridTelemetryEntry> scratch = OpenBuffer(in _telemetryScratch, TelemetryScratchBufferId);

            SwapDensityBuffers();
            _densityVersion++;
            UpdateGridHeader();

            PublishSignals(exposureSignals, statusSignals, biolumSignals, signalCounters);
            CommitTelemetryScratch(scratch);
        }

        private bool TryOpenMutationWindow()
        {
            if (!_hasScheduledWork)
            {
                return true;
            }

            if (!_scheduledHandle.IsCompleted)
            {
                return false;
            }

            return TryFinalizeScheduledWorkNoWait();
        }

        private void PublishSignals(NativeArray<ToxicityExposureSignal> exposures, NativeArray<ToxicityStatusSignal> statuses, NativeArray<ToxicBioluminescenceSignal> biolums, NativeArray<int> counters)
        {
            int exposureCount = math.clamp(counters[0], 0, MaxSignalsPerFrame);
            for (int i = 0; i < exposureCount; i++)
            {
                ToxicityExposureSignal exposure = exposures[i];
                if (!TryPrepareToxicityExposureSignalForPublish(ref exposure))
                {
                    IncrementToxicOutgassingSignalDropCount();
                    continue;
                }

                SignalBus<ToxicityExposureSignal>.TryPushTracked(in exposure, ref s_x001ToxicOutgassingChemistryRuntimeSignalPushDropCount);
            }

            int statusCount = math.clamp(counters[1], 0, MaxSignalsPerFrame);
            for (int i = 0; i < statusCount; i++)
            {
                ToxicityStatusSignal staged = statuses[i];
                float3 local = AupPrecisionMath.LocalDeltaFloat3(staged.AUP, _gridOriginAup, float3.zero);
                if (!math.all(math.isfinite(local)) || !math.isfinite(staged.Magnitude))
                {
                    continue;
                }

                int targetId = staged.TargetHash <= (uint)int.MaxValue
                    ? (int)staged.TargetHash
                    : 0;
                if (targetId == 0 && staged.TargetId != 0)
                    targetId = staged.TargetId;

                if (targetId == 0)
                    continue;

                CombatDamageRuntime.TryQueueStatusEffect(
                    targetId,
                    CombatStatusBits.Poisoned64,
                    ToxicCorrosionStatusDurationSeconds,
                    staged.SourceId != 0 ? staged.SourceId : RuntimeSourceId,
                    math.saturate(staged.Magnitude));
            }

            int biolumCount = math.clamp(counters[2], 0, MaxSignalsPerFrame);
            for (int i = 0; i < biolumCount; i++)
            {
                ToxicBioluminescenceSignal signal = biolums[i];
                if (!TryPrepareToxicBioluminescenceSignalForPublish(ref signal))
                {
                    IncrementToxicOutgassingSignalDropCount();
                    continue;
                }

                SignalBus<ToxicBioluminescenceSignal>.TryPushTracked(in signal, ref s_x001ToxicOutgassingChemistryRuntimeSignalPushDropCount);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryPrepareToxicityExposureSignalForPublish(ref ToxicityExposureSignal exposure)
        {
            if (exposure.EntityId == 0u)
                return false;

            if (!IsPublishableToxicitySourceAup(exposure.AUP))
                return false;

            if (!math.isfinite(exposure.Exposure01))
                return false;

            exposure.Exposure01 = math.saturate(exposure.Exposure01);
            if (exposure.Exposure01 <= 0.0001f)
                return false;

            if (!math.isfinite(exposure.ToxemiaDelta))
                return false;

            exposure.ToxemiaDelta = math.saturate(math.max(0f, exposure.ToxemiaDelta));
            exposure.Flags = ToxicityExposureSignalFlagsActive;

            exposure._pad0 = 0;
            exposure._pad1 = 0;
            exposure._pad2 = 0ul;
            exposure._pad3 = 0ul;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryPrepareToxicBioluminescenceSignalForPublish(ref ToxicBioluminescenceSignal signal)
        {
            if (!IsPublishableToxicitySourceAup(signal.AUP))
                return false;

            if (!math.isfinite(signal.Intensity01))
                return false;

            signal.Intensity01 = math.saturate(signal.Intensity01);
            if (signal.Intensity01 <= 0.0001f)
                return false;

            if (!math.isfinite(signal.ToxicDensity))
                return false;

            signal.ToxicDensity = math.max(0f, signal.ToxicDensity);
            if (signal.ToxicDensity <= 0.0001f)
                return false;

            signal.LocalNormal = math.all(math.isfinite(signal.LocalNormal))
                ? signal.LocalNormal
                : float3.zero;
            signal.Flags = ToxicBioluminescenceSignalFlagsActive;
            signal._pad0 = 0;
            signal._pad1 = 0ul;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPublishableToxicitySourceAup(double3 aup)
        {
            return math.all(math.isfinite(aup)) &&
                   math.lengthsq(aup) > 0.000001d &&
                   math.abs(aup.x) <= ToxicityExposureSignal.MaxSourceAupExtentMeters &&
                   math.abs(aup.y) <= ToxicityExposureSignal.MaxSourceAupExtentMeters &&
                   math.abs(aup.z) <= ToxicityExposureSignal.MaxSourceAupExtentMeters;
        }

        private static void IncrementToxicOutgassingSignalDropCount()
        {
            int current = Volatile.Read(ref s_x001ToxicOutgassingChemistryRuntimeSignalPushDropCount);
            if (current < int.MaxValue)
                Interlocked.Increment(ref s_x001ToxicOutgassingChemistryRuntimeSignalPushDropCount);
        }

        private void SwapDensityBuffers()
        {
            VaultGenerationHandle<float> previousFront = _densityFront;
            _densityFront = _densityBack;
            _densityBack = previousFront;

            VaultGenerationHandle<ToxicityStateDTO> previousStateFront = _cellStatesFront;
            _cellStatesFront = _cellStatesBack;
            _cellStatesBack = previousStateFront;
        }

        private void UpdateGridHeader()
        {
            if (!_nativeReady || !TryOpenBuffer(in _gridHeader, GridHeaderBufferId, out NativeArray<ToxicOutgassingGridHeaderDTO> headers) ||
                headers.Length == 0)
            {
                return;
            }

            ToxicOutgassingGridHeaderDTO header = headers[0];
            header.GridOriginAUP = _gridOriginAup;
            header.CellSizeMeters = _cellSizeMeters;
            header.GlobalQualityWeight = _lastQualityWeight;
            header.ActiveDensityBufferId = HandleBufferIdToUInt(in _densityFront);
            header.BackDensityBufferId = HandleBufferIdToUInt(in _densityBack);
            header.StateBufferId = HandleBufferIdToUInt(in _cellStatesFront);
            header.DensityVersion = unchecked((uint)math.max(0, _densityVersion));
            header.Resolution = (ushort)math.clamp(_activeResolution, 0, ushort.MaxValue);
            header.ActiveSources = (ushort)math.clamp(_sourceCount, 0, ushort.MaxValue);
            header.ActiveEntities = (ushort)math.clamp(_entityCount, 0, ushort.MaxValue);
            header.Flags = (byte)(_mockChemistry ? TelemetryFlagMockChemistry : 0);
            header._pad0 = 0;
            header._pad1 = 0ul;
            headers[0] = header;
        }

        private static uint HandleBufferIdToUInt<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID;
        }

        private void CommitTelemetryScratch(NativeArray<ToxicityGridTelemetryEntry> scratch)
        {
            if (!scratch.IsCreated || scratch.Length == 0)
            {
                return;
            }

            NativeArray<ToxicityGridTelemetryEntry> ring = OpenBuffer(in _telemetryRing, TelemetryRingBufferId);
            if (!ring.IsCreated || ring.Length == 0)
            {
                return;
            }

            int capacity = math.min(ring.Length, TelemetryCapacity);
            if (capacity <= 0)
            {
                return;
            }

            ToxicityGridTelemetryEntry entry = scratch[0];
            entry.DiffusionCompleteMs = _lastCompleteMs;
            entry.Flags = (byte)(entry.Flags | _pendingFailureFlags);
            _pendingFailureFlags = 0;
            int writeIndex = _telemetryCursor;
            if (writeIndex < 0 || writeIndex >= capacity)
            {
                writeIndex = 0;
            }

            ring[writeIndex] = entry;
            _telemetryCursor = (writeIndex + 1) % capacity;
            _telemetryRetainedCount = math.min(_telemetryRetainedCount + 1, capacity);
            if (entry.NanDetected != 0)
            {
                DumpBlackBox();
            }
        }

        private void PublishShaderScalar()
        {
            if (!_nativeReady)
            {
                return;
            }

            NativeArray<ToxicityGridTelemetryEntry> ring = OpenBuffer(in _telemetryRing, TelemetryRingBufferId);
            if (!ring.IsCreated || ring.Length == 0)
            {
                return;
            }

            int index = _telemetryCursor - 1;
            if (index < 0)
            {
                index = TelemetryCapacity - 1;
            }

            ToxicityGridTelemetryEntry entry = ring[index];
            if (!math.isfinite(entry.MaxDensity))
            {
                return;
            }

            if (!TryReadConstants(out ToxicOutgassingConstants constants))
            {
                return;
            }

            float threshold = math.max(constants.CausticDensityThreshold, NaNEpsilon);
            float acidCaustic01 = math.saturate(entry.MaxDensity / threshold);
            float visualOverkill = Smooth01(_lastQualityWeight);
            HectonShaderGlobalDataVaultBridge.PublishUberNoirRuntime(new Vector4(acidCaustic01, entry.TotalPlumeVolume, entry.GlobalQualityWeight, visualOverkill), acidCaustic01);
        }

        private void DumpBlackBox()
        {
            NativeArray<byte> payload = default;
            try
            {
                CompleteScheduledWorkForTeardown();
                NativeArray<ToxicityGridTelemetryEntry> ring = OpenBuffer(in _telemetryRing, TelemetryRingBufferId);
                if (!ring.IsCreated || ring.Length == 0)
                    return;

                int entrySize = UnsafeUtility.SizeOf<ToxicityGridTelemetryEntry>();
                int capacity = math.min(ring.Length, TelemetryCapacity);
                int retainedCount = math.clamp(_telemetryRetainedCount, 0, capacity);
                if (entrySize != ToxicBlackBoxEntryBytes || capacity <= 0 || retainedCount <= 0)
                    return;

                int cursor = _telemetryCursor;
                if (cursor < 0 || cursor >= capacity)
                    cursor = 0;

                int start = retainedCount >= capacity ? cursor : 0;
                int payloadBytes = retainedCount * entrySize;
                int byteCount = ToxicBlackBoxHeaderBytes + payloadBytes;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(ToxicOutgassingChemistryRuntime),
                    DumpPayloadLabel,
                    NativeArrayOptions.ClearMemory);

                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                uint* header = (uint*)destination;
                header[0] = ToxicBlackBoxMagic;
                header[1] = ToxicBlackBoxVersion;
                header[2] = ToxicBlackBoxHeaderBytes;
                header[3] = unchecked((uint)entrySize);
                header[4] = unchecked((uint)capacity);
                header[5] = unchecked((uint)retainedCount);
                header[6] = unchecked((uint)cursor);
                header[7] = unchecked((uint)payloadBytes);

                byte* rowDestination = destination + ToxicBlackBoxHeaderBytes;
                for (int i = 0; i < retainedCount; i++)
                {
                    int index = start + i;
                    if (index >= capacity)
                        index -= capacity;

                    ToxicityGridTelemetryEntry entry = ring[index];
                    UnsafeUtility.MemCpy(rowDestination + i * entrySize, UnsafeUtility.AddressOf(ref entry), entrySize);
                }

                if (!NativeFaultDumpWriter.TryWriteAll(DumpRelativePath, payload, byteCount))
                    MarkFailure(TelemetryFlagDumpFailure);
            }
            catch (IOException)
            {
                MarkFailure(TelemetryFlagDumpFailure);
            }
            catch (UnauthorizedAccessException)
            {
                MarkFailure(TelemetryFlagDumpFailure);
            }
            catch (ArgumentException)
            {
                MarkFailure(TelemetryFlagDumpFailure);
            }
            catch (NotSupportedException)
            {
                MarkFailure(TelemetryFlagDumpFailure);
            }
            catch (ObjectDisposedException)
            {
                MarkFailure(TelemetryFlagDumpFailure);
            }
            catch (InvalidOperationException)
            {
                MarkFailure(TelemetryFlagDumpFailure);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(ToxicOutgassingChemistryRuntime),
                    DumpPayloadLabel);
            }
        }

        private void ProbeColdBinaryPayloads()
        {
            try
            {
                string root = ProjectRootPath();
                string legacyPath = Path.Combine(root, LegacyBinaryRelativePath);
                if (File.Exists(legacyPath))
                {
                    ReadBinaryProbe(legacyPath);
                    return;
                }

                string daltonPath = Path.Combine(root, DaltonBinaryRelativePath);
                if (File.Exists(daltonPath))
                {
                    ReadBinaryProbe(daltonPath);
                }
            }
            catch (IOException)
            {
                MarkFailure(TelemetryFlagBinaryProbeFailure);
            }
            catch (UnauthorizedAccessException)
            {
                MarkFailure(TelemetryFlagBinaryProbeFailure);
            }
            catch (ArgumentException)
            {
                MarkFailure(TelemetryFlagBinaryProbeFailure);
            }
        }

        private void MarkFailure(byte failureFlag)
        {
            _pendingFailureFlags = (byte)(_pendingFailureFlags | failureFlag);
        }

        private void ReadBinaryProbe(string path)
        {
            NativeArray<byte> bytes = OpenBuffer(in _binaryProbeBytes, BinaryProbeByteBufferId);
            int length = FillByteBufferFromFile(path, bytes);
            if (length >= 4)
            {
                uint magic = (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));
                uint reverseMagic = ReverseBytes(magic);
                if (IsKnownGasMagic(magic) || IsKnownGasMagic(reverseMagic))
                {
                    _mockChemistry = true;
                }
            }
        }

        private static bool IsKnownGasMagic(uint magic)
        {
            return magic == 0x54473848u || magic == 0x4C473848u || magic == 0x58473848u;
        }

        private static uint ReverseBytes(uint value)
        {
            return ((value & 0x000000FFu) << 24) |
                   ((value & 0x0000FF00u) << 8) |
                   ((value & 0x00FF0000u) >> 8) |
                   ((value & 0xFF000000u) >> 24);
        }

        private static void PrewarmSignalLanes()
        {
            SignalBus<ToxicityExposureSignal>.Configure(
                ToxicityExposureSignal.ExpectedCapacity,
                ToxicityExposureSignal.MaxFrameSignals,
                ToxicityExposureSignal.LowTierFrameSignals,
                ToxicityExposureSignal.LaneHash);
            SignalBus<ToxicityExposureSignal>.EnsureInitialized();

            SignalBus<ToxicBioluminescenceSignal>.Configure(
                ToxicBioluminescenceSignal.ExpectedCapacity,
                ToxicBioluminescenceSignal.MaxFrameSignals,
                ToxicBioluminescenceSignal.LowTierFrameSignals,
                ToxicBioluminescenceSignal.LaneHash);
            SignalBus<ToxicBioluminescenceSignal>.EnsureInitialized();
        }

        // Runtime helper, deliberately OUTSIDE the editor CSV block below.
        // ReadBinaryProbe() -> ProbeColdBinaryPayloads() runs unguarded from EnsureNativeState(), and
        // the buffer it fills (_binaryProbeBytes) is acquired unguarded as well, so the player build
        // needs this reader. It is a bounded FileStream.Read into a preallocated NativeArray - not
        // File.ReadAllBytes - and its payload is the sanctioned Data/Precomputed .h8bin artifact.
        private static int FillByteBufferFromFile(string path, NativeArray<byte> buffer)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                int length = (int)math.min(buffer.Length, stream.Length);
                if (length <= 0)
                    return 0;

                unsafe
                {
                    Span<byte> span = new Span<byte>(buffer.GetUnsafePtr(), length);
                    return stream.Read(span);
                }
            }
        }

#if UNITY_EDITOR
        private static void ParseChemicalCsv(NativeArray<byte> bytes, int length, ref ToxicOutgassingConstants constants)
        {
            int cursor = 0;
            while (cursor < length)
            {
                uint keyHash = 2166136261u;
                while (cursor < length)
                {
                    byte c = bytes[cursor++];
                    if (c == (byte)',' || c == (byte)'=' || c == (byte)'\t')
                    {
                        break;
                    }

                    if (c == (byte)'\n' || c == (byte)'\r')
                    {
                        keyHash = 0u;
                        break;
                    }

                    if (c >= (byte)'A' && c <= (byte)'Z')
                    {
                        c = (byte)(c + 32);
                    }

                    keyHash ^= c;
                    keyHash *= 16777619u;
                }

                float value = 0f;
                bool negative = false;
                bool hasDigit = false;
                float decimalScale = 0f;
                while (cursor < length)
                {
                    byte c = bytes[cursor++];
                    if (c == (byte)'-' && !hasDigit)
                    {
                        negative = true;
                        continue;
                    }

                    if (c == (byte)'.')
                    {
                        decimalScale = 0.1f;
                        continue;
                    }

                    if (c >= (byte)'0' && c <= (byte)'9')
                    {
                        hasDigit = true;
                        int digit = c - (byte)'0';
                        if (decimalScale > 0f)
                        {
                            value += digit * decimalScale;
                            decimalScale *= 0.1f;
                        }
                        else
                        {
                            value = value * 10f + digit;
                        }

                        continue;
                    }

                    if (c == (byte)'\n' || c == (byte)'\r')
                    {
                        break;
                    }
                }

                if (negative)
                {
                    value = -value;
                }

                if (hasDigit)
                {
                    ApplyCsvValue(keyHash, value, ref constants);
                }
            }
        }

        private static void ApplyCsvValue(uint hash, float value, ref ToxicOutgassingConstants constants)
        {
            switch (hash)
            {
                case 0x1DA6B953u:
                    constants.BaseDiffusionRate = value;
                    break;
                case 0x46C59124u:
                    constants.CurrentAdvectionMultiplier = value;
                    break;
                case 0xE92996B7u:
                    constants.AcidCorrosionDamage = value;
                    break;
                case 0x10749B7Au:
                    constants.FloraAbsorptionRate = value;
                    break;
                case 0x7EA5B0FBu:
                    constants.DensityDecayPerSecond = value;
                    break;
                case 0x50F1217Au:
                    constants.SourceRadiusMeters = value;
                    break;
                case 0x5254F708u:
                    constants.CausticDensityThreshold = value;
                    break;
                case 0x8E9B09BEu:
                    constants.BiolumDensityThreshold = value;
                    break;
                case 0x9837EEF6u:
                    constants.MaxDensity = value;
                    break;
                case 0x7D09E97Eu:
                    constants.ExposureToxemiaMultiplier = value;
                    break;
            }
        }
#endif

        private static ToxicOutgassingConstants SanitizeConstants(ToxicOutgassingConstants constants)
        {
            constants.BaseDiffusionRate = ClampFinite(constants.BaseDiffusionRate, 0f, 2f, 0.17f);
            constants.CurrentAdvectionMultiplier = ClampFinite(constants.CurrentAdvectionMultiplier, 0f, 4f, 0.85f);
            constants.AcidCorrosionDamage = ClampFinite(constants.AcidCorrosionDamage, 0f, 1f, 0.045f);
            constants.FloraAbsorptionRate = ClampFinite(constants.FloraAbsorptionRate, 0f, 1f, 0.11f);
            constants.DensityDecayPerSecond = ClampFinite(constants.DensityDecayPerSecond, 0f, 1f, 0.032f);
            constants.SourceRadiusMeters = ClampFinite(constants.SourceRadiusMeters, 1f, 200f, 42f);
            constants.ExposureToxemiaMultiplier = ClampFinite(constants.ExposureToxemiaMultiplier, 0f, 1f, 0.075f);
            constants.CausticDensityThreshold = ClampFinite(constants.CausticDensityThreshold, 0.01f, 1f, 0.58f);
            constants.BiolumDensityThreshold = ClampFinite(constants.BiolumDensityThreshold, 0.01f, 1f, 0.34f);
            constants.MaxDensity = ClampFinite(constants.MaxDensity, 0.1f, 8f, 1f);
            constants.RadialFallbackRadiusScale = ClampFinite(constants.RadialFallbackRadiusScale, 0.25f, 8f, 1.65f);
            constants.SdfWallLeakScale = ClampFinite(constants.SdfWallLeakScale, 0f, 0.25f, 0f);
            constants.GlobalQualityWeight = math.saturate(constants.GlobalQualityWeight);
            constants.SimulationTickDelta = ClampFinite(constants.SimulationTickDelta, 0.001f, 0.5f, 0.2f);
            return constants;
        }

        private static float ClampFinite(float value, float min, float max, float fallback)
        {
            return math.isfinite(value) ? math.clamp(value, min, max) : fallback;
        }

        private float ResolveQualityWeight()
        {
            _lastQualityWeight = ResolveRuntimeQualityWeight01();
            return _lastQualityWeight;
        }

        private float ReadCachedQualityWeight()
        {
            return math.saturate(math.isfinite(_lastQualityWeight) ? _lastQualityWeight : ResolveRuntimeQualityWeight01());
        }

        private static int ResolveResolution(float qualityWeight)
        {
            float quality = Smooth01(qualityWeight);
            return math.clamp((int)math.round(math.lerp(LowResolution, HighResolution, quality)), LowResolution, HighResolution);
        }

        private static float ResolveTickInterval(float qualityWeight)
        {
            return math.lerp(0.2f, 0.08333334f, Smooth01(qualityWeight));
        }

        private static int ResolveSourceBudget(float qualityWeight, int sourceCount)
        {
            return math.clamp(sourceCount, 0, MaxSourceCount);
        }

        private static float ResolveRuntimeQualityWeight01()
        {
            float signalWeight = SignalBusRegistry.GlobalQualityWeight01;
            if (math.isfinite(signalWeight) && signalWeight > 0f)
                return math.saturate(signalWeight);

            float brainWeight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(brainWeight) ? brainWeight : DefaultQualityWeight);
        }

        private static float Smooth01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FastLengthFromSq(float lengthSq)
        {
            float sanitized = math.isfinite(lengthSq) ? math.max(0f, lengthSq) : 0f;
            return sanitized * math.rsqrt(math.max(sanitized, NaNEpsilon));
        }

        private static int FindSourceIndex(NativeArray<uint> sourceIds, uint sourceId, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (sourceIds[i] == sourceId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindEntityIndex(NativeArray<uint> entityIds, uint entityId, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (entityIds[i] == entityId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static float SampleDensityTrilinear(NativeArray<float> density, int resolution, double3 gridOriginAup, float cellSizeMeters, double3 aup)
        {
            float3 local = AupPrecisionMath.LocalDeltaFloat3(aup, gridOriginAup, float3.zero);
            float invCell = math.rcp(math.max(cellSizeMeters, NaNEpsilon));
            float3 cell = local * invCell + (resolution * 0.5f);
            float3 baseCell = math.floor(cell);
            int3 c0 = new int3((int)baseCell.x, (int)baseCell.y, (int)baseCell.z);
            float3 f = math.saturate(cell - baseCell);
            int3 c1 = math.min(c0 + 1, new int3(resolution - 1));
            c0 = math.clamp(c0, int3.zero, new int3(resolution - 1));

            float c000 = density[Flatten(c0.x, c0.y, c0.z, resolution)];
            float c100 = density[Flatten(c1.x, c0.y, c0.z, resolution)];
            float c010 = density[Flatten(c0.x, c1.y, c0.z, resolution)];
            float c110 = density[Flatten(c1.x, c1.y, c0.z, resolution)];
            float c001 = density[Flatten(c0.x, c0.y, c1.z, resolution)];
            float c101 = density[Flatten(c1.x, c0.y, c1.z, resolution)];
            float c011 = density[Flatten(c0.x, c1.y, c1.z, resolution)];
            float c111 = density[Flatten(c1.x, c1.y, c1.z, resolution)];

            float cx00 = math.lerp(c000, c100, f.x);
            float cx10 = math.lerp(c010, c110, f.x);
            float cx01 = math.lerp(c001, c101, f.x);
            float cx11 = math.lerp(c011, c111, f.x);
            float cy0 = math.lerp(cx00, cx10, f.y);
            float cy1 = math.lerp(cx01, cx11, f.y);
            return math.lerp(cy0, cy1, f.z);
        }

        private static float SampleDensityNearest(NativeArray<float> density, int resolution, double3 gridOriginAup, float cellSizeMeters, double3 aup)
        {
            float3 local = AupPrecisionMath.LocalDeltaFloat3(aup, gridOriginAup, float3.zero);
            float invCell = math.rcp(math.max(cellSizeMeters, NaNEpsilon));
            int3 c = (int3)math.round(local * invCell + resolution * 0.5f);
            c = math.clamp(c, int3.zero, new int3(resolution - 1));
            return density[Flatten(c.x, c.y, c.z, resolution)];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Flatten(int x, int y, int z, int resolution)
        {
            return x + resolution * (y + resolution * z);
        }

        private static string ProjectRootPath()
        {
            string dataPath = Application.dataPath;
            DirectoryInfo directory = Directory.GetParent(dataPath);
            return directory != null ? directory.FullName : dataPath;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct RebaseGridJob : IJobParallelFor
        {
            [NoAlias, ReadOnly] public NativeArray<float> Front;
            [NoAlias] public NativeArray<float> Back;
            public int3 RebaseCells;
            public int Resolution;

            public void Execute(int index)
            {
                int z = index / (Resolution * Resolution);
                int rem = index - z * Resolution * Resolution;
                int y = rem / Resolution;
                int x = rem - y * Resolution;
                int3 source = new int3(x, y, z) + RebaseCells;
                if (math.any(source < int3.zero) || math.any(source >= new int3(Resolution)))
                {
                    Back[index] = 0f;
                    return;
                }

                Back[index] = Front[Flatten(source.x, source.y, source.z, Resolution)];
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct MockFlowFieldJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<MockFlowField> FlowField;
            public double3 GridOriginAup;
            public float CellSizeMeters;
            public int Resolution;
            public float GlobalQualityWeight;
            public uint Frame;

            public void Execute(int index)
            {
                int z = index / (Resolution * Resolution);
                int rem = index - z * Resolution * Resolution;
                int y = rem / Resolution;
                int x = rem - y * Resolution;
                float3 local = (new float3(x, y, z) - (Resolution * 0.5f)) * CellSizeMeters;
                float quality = math.saturate(GlobalQualityWeight);
                float detailBlend = Smooth01((quality - 0.18f) * 1.45f);
                float3 direction = new float3(0.8944272f, 0f, 0.4472136f);
                float turbulence = 0f;
                float3 curl = float3.zero;
                if (detailBlend > 0.0001f)
                {
                    double3 phaseAup = (GridOriginAup * 0.001d) + (new double3(local.x, local.y, local.z) * 0.013d);
                    float3 p = AupPrecisionMath.DowncastProceduralPhase(phaseAup, local * 0.013f);
                    float timePhase = Frame * math.lerp(0.001f, 0.004f, quality);
                    float s0 = MathLodApproximation.ApproxSinBhaskara(p.x + p.z * 0.37f + timePhase);
                    float s1 = MathLodApproximation.ApproxCosBhaskara(p.y * 0.61f - p.x * 0.23f + timePhase * 0.7f);
                    float3 raw = new float3(s1, s0 * 0.15f, s0 - s1 * 0.35f);
                    float lenSq = math.max(math.lengthsq(raw), NaNEpsilon);
                    direction = raw * math.rsqrt(lenSq);
                    turbulence = Smooth01((quality - 0.2f) * 1.25f);
                    curl = new float3(s0, s1, s0 * s1) * turbulence;
                }

                FlowField[index] = new MockFlowField
                {
                    Direction = direction,
                    Speed = math.lerp(0.15f, 1.65f, quality),
                    Curl = curl,
                    Turbulence = turbulence
                };
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct MockWorldSamplerJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<MockWorldSampler> WorldSamples;
            public double3 GridOriginAup;
            public float CellSizeMeters;
            public int Resolution;
            public float GlobalQualityWeight;
            public uint PurifierKelpHashValue;

            public void Execute(int index)
            {
                int z = index / (Resolution * Resolution);
                int rem = index - z * Resolution * Resolution;
                int y = rem / Resolution;
                int x = rem - y * Resolution;
                float3 local = (new float3(x, y, z) - (Resolution * 0.5f)) * CellSizeMeters;
                float quality = math.saturate(GlobalQualityWeight);
                float detailBlend = Smooth01((quality - 0.18f) * 1.45f);
                float caveShell = FastLengthFromSq((local.x * local.x) + (local.z * local.z)) - (Resolution * CellSizeMeters * 0.42f);
                float ceiling = math.abs(local.y) - (Resolution * CellSizeMeters * 0.18f);
                float rib = 0f;
                float flora = 0f;
                if (detailBlend > 0.0001f)
                {
                    double3 phase = (GridOriginAup * 0.0007d) + (new double3(local.x, local.y, local.z) * 0.017d);
                    float3 p = AupPrecisionMath.DowncastProceduralPhase(phase, local * 0.017f);
                    rib = MathLodApproximation.ApproxSinBhaskara(p.x * 1.7f + p.z * 0.9f) * math.lerp(2f, 8f, quality) * detailBlend;
                    float kelpWave = MathLodApproximation.ApproxSinBhaskara(p.x * 2.1f) * MathLodApproximation.ApproxCosBhaskara(p.z * 1.3f);
                    flora = math.saturate((kelpWave - 0.35f) * 2.2f) * math.saturate((local.y + CellSizeMeters * 8f) * math.rcp(math.max(CellSizeMeters * 16f, NaNEpsilon))) * detailBlend;
                }

                float sdf = math.max(caveShell + rib, ceiling - rib * 0.25f);
                float3 gradient = new float3(local.x, 0f, local.z);
                float gradientLenSq = math.max(math.lengthsq(gradient), NaNEpsilon);
                WorldSamples[index] = new MockWorldSampler
                {
                    SdfDistance = sdf,
                    FloraAbsorption01 = flora,
                    SdfGradient = gradient * math.rsqrt(gradientLenSq),
                    Flags = sdf < 0f ? 1u : 0u,
                    PurifierKelpHash = flora > 0.05f ? PurifierKelpHashValue : 0u,
                    _pad0 = 0u
                };
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct ToxicDiffusionJob : IJobParallelFor
        {
            [NoAlias, ReadOnly] public NativeArray<float> Front;
            [NoAlias] public NativeArray<float> Back;
            [NoAlias, ReadOnly] public NativeArray<MockFlowField> FlowField;
            [NoAlias, ReadOnly] public NativeArray<MockWorldSampler> WorldSamples;
            [NoAlias, ReadOnly] public NativeArray<ToxicitySourceDTO> Sources;
            [NoAlias] public NativeArray<ToxicityStateDTO> States;
            [NoAlias] public NativeArray<int> NanFlags;
            public ToxicOutgassingConstants Constants;
            public double3 GridOriginAup;
            public float CellSizeMeters;
            public int Resolution;
            public int SourceCount;
            public int SourceBudget;
            public float DeltaTime;
            public float GlobalQualityWeight;
            public uint ChemicalHash;
            public uint Frame;

            public void Execute(int index)
            {
                MockWorldSampler world = WorldSamples[index];
                if (world.SdfDistance < 0f)
                {
                    Back[index] = 0f;
                    States[index] = new ToxicityStateDTO
                    {
                        Density = 0f,
                        PreviousDensity = Front[index],
                        FlowBias = 0f,
                        SdfDistance = world.SdfDistance,
                        ChemicalHash = ChemicalHash,
                        CellHash = unchecked((uint)index),
                        Frame = Frame,
                        _pad0 = 0u
                    };
                    NanFlags[index] = 0;
                    return;
                }

                int z = index / (Resolution * Resolution);
                int rem = index - z * Resolution * Resolution;
                int y = rem / Resolution;
                int x = rem - y * Resolution;

                float quality = math.saturate(GlobalQualityWeight);
                float dt = math.clamp(DeltaTime, 0.001f, 0.5f);
                float current = math.max(0f, Front[index]);
                float decay = math.saturate(Constants.DensityDecayPerSecond * dt);
                float source = SourceContribution(x, y, z, quality, dt);
                float radialOnly = current * (1f - decay) + source;
                float diffusionBlend = Smooth01((quality - 0.22f) * 1.55f);
                float next = radialOnly;
                float flowBias = 0f;

                if (diffusionBlend > 0.0001f)
                {
                    float center = current;
                    float xp = Neighbor(x + 1, y, z, center);
                    float xm = Neighbor(x - 1, y, z, center);
                    float yp = Neighbor(x, y + 1, z, center);
                    float ym = Neighbor(x, y - 1, z, center);
                    float zp = Neighbor(x, y, z + 1, center);
                    float zm = Neighbor(x, y, z - 1, center);
                    float laplacian = (xp + xm + yp + ym + zp + zm - center * 6f);
                    float diffusion = laplacian * Constants.BaseDiffusionRate * dt;

                    MockFlowField flow = FlowField[index];
                    float3 absDirection = math.abs(flow.Direction);
                    float advected = center;
                    if (absDirection.x >= absDirection.y && absDirection.x >= absDirection.z)
                    {
                        advected = flow.Direction.x >= 0f ? Neighbor(x - 1, y, z, center) : Neighbor(x + 1, y, z, center);
                    }
                    else if (absDirection.y >= absDirection.z)
                    {
                        advected = flow.Direction.y >= 0f ? Neighbor(x, y - 1, z, center) : Neighbor(x, y + 1, z, center);
                    }
                    else
                    {
                        advected = flow.Direction.z >= 0f ? Neighbor(x, y, z - 1, center) : Neighbor(x, y, z + 1, center);
                    }

                    float advection01 = math.saturate(flow.Speed * Constants.CurrentAdvectionMultiplier * dt * math.rcp(math.max(CellSizeMeters, NaNEpsilon)));
                    flowBias = advection01;
                    float diffusionCandidate = center + diffusion + source - center * decay;
                    diffusionCandidate = math.lerp(diffusionCandidate, advected + source - advected * decay, advection01);
                    next = math.lerp(radialOnly, diffusionCandidate, diffusionBlend);
                }

                float floraSink = world.FloraAbsorption01 * Constants.FloraAbsorptionRate * dt * math.lerp(0.35f, 1f, quality);
                next = math.max(0f, next - floraSink);
                next = math.min(next, math.max(Constants.MaxDensity, NaNEpsilon));

                if (!math.isfinite(next))
                {
                    Back[index] = 0f;
                    States[index] = new ToxicityStateDTO
                    {
                        Density = 0f,
                        PreviousDensity = current,
                        FlowBias = flowBias,
                        SdfDistance = world.SdfDistance,
                        ChemicalHash = ChemicalHash,
                        CellHash = CellHash(x, y, z),
                        Frame = Frame,
                        _pad0 = 0u
                    };
                    NanFlags[index] = 1;
                    return;
                }

                Back[index] = next;
                States[index] = new ToxicityStateDTO
                {
                    Density = next,
                    PreviousDensity = current,
                    FlowBias = flowBias,
                    SdfDistance = world.SdfDistance,
                    ChemicalHash = ChemicalHash,
                    CellHash = CellHash(x, y, z),
                    Frame = Frame,
                    _pad0 = 0u
                };
                NanFlags[index] = 0;
            }

            private static uint CellHash(int x, int y, int z)
            {
                return math.hash(new uint3((uint)x, (uint)y, (uint)z));
            }

            private float SourceContribution(int x, int y, int z, float quality, float dt)
            {
                float3 cellLocal = (new float3(x, y, z) - (Resolution * 0.5f + RebaseHalfCellBias)) * CellSizeMeters;
                float sourceRadius = math.max(Constants.SourceRadiusMeters * math.lerp(Constants.RadialFallbackRadiusScale, 1f, quality), CellSizeMeters);
                float radiusSq = math.max(sourceRadius * sourceRadius, NaNEpsilon);
                float sum = 0f;
                int budget = math.min(SourceBudget, SourceCount);
                for (int i = 0; i < budget; i++)
                {
                    ToxicitySourceDTO source = Sources[i];
                    double3 aupDelta = AupPrecisionMath.LocalDeltaDouble(source.AUP, GridOriginAup);
                    if (!math.all(math.isfinite(aupDelta)))
                    {
                        continue;
                    }

                    float3 sourceLocal = AupPrecisionMath.DowncastLocalDeltaClamped(
                        aupDelta,
                        AupPrecisionMath.DefaultMaxLocalCastMeters,
                        float3.zero);
                    float distSq = math.lengthsq(cellLocal - sourceLocal);
                    float falloff = math.saturate(1f - distSq / radiusSq);
                    falloff *= falloff * (3f - 2f * falloff);
                    float emission = math.max(0f, source.EmissionRate) * math.max(0f, source.Density);
                    sum += emission * falloff * dt;
                }

                return sum;
            }

            private float Neighbor(int x, int y, int z, float fallback)
            {
                if (x < 0 || y < 0 || z < 0 || x >= Resolution || y >= Resolution || z >= Resolution)
                {
                    return fallback;
                }

                int index = Flatten(x, y, z, Resolution);
                if (WorldSamples[index].SdfDistance < 0f)
                {
                    return fallback * Constants.SdfWallLeakScale;
                }

                return Front[index];
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct EntityExposureJob : IJob
        {
            [NoAlias, ReadOnly] public NativeArray<float> Density;
            [NoAlias, ReadOnly] public NativeArray<double3> EntityAups;
            [NoAlias, ReadOnly] public NativeArray<uint> EntityIds;
            [NoAlias] public NativeArray<float> CorrosionTimers;
            [NoAlias] public NativeArray<float> ExposureAccumulators;
            [NoAlias] public NativeArray<ToxicityExposureSignal> ExposureSignals;
            [NoAlias] public NativeArray<ToxicityStatusSignal> StatusSignals;
            [NoAlias] public NativeArray<int> SignalCounters;
            public ToxicOutgassingConstants Constants;
            public double3 GridOriginAup;
            public float CellSizeMeters;
            public int Resolution;
            public int EntityCount;
            public float DeltaTime;
            public float CorrosionDelta;
            public float GlobalQualityWeight;
            public uint Frame;
            public uint ToxicStatusType;
            public uint AcidChemicalHashValue;
            public ushort RuntimeSourceId;
            public byte RuntimeChannel;

            public void Execute()
            {
                float sampleBlend = Smooth01((math.saturate(GlobalQualityWeight) - 0.28f) * 1.6f);
                float dt = math.clamp(DeltaTime, 0.001f, 0.5f);
                float corrosionDelta = math.max(CorrosionDelta, dt);
                int count = math.min(EntityCount, EntityAups.Length);
                for (int i = 0; i < count; i++)
                {
                    uint entityId = EntityIds[i];
                    if (entityId == 0u)
                    {
                        continue;
                    }

                    double3 aup = EntityAups[i];
                    if (!math.all(math.isfinite(aup)))
                    {
                        continue;
                    }

                    float nearest = SampleNearest(Density, Resolution, GridOriginAup, CellSizeMeters, aup);
                    float sample = nearest;
                    if (sampleBlend > 0.0001f)
                    {
                        float trilinear = SampleDensityTrilinear(Density, Resolution, GridOriginAup, CellSizeMeters, aup);
                        sample = math.lerp(nearest, trilinear, sampleBlend);
                    }

                    float exposure = math.saturate(sample * math.rcp(math.max(Constants.MaxDensity, NaNEpsilon)));
                    if (!math.isfinite(exposure))
                    {
                        continue;
                    }

                    if (exposure > 0.0001f)
                    {
                        int exposureIndex = SignalCounters[0];
                        if (exposureIndex < ExposureSignals.Length)
                        {
                            SignalCounters[0] = exposureIndex + 1;
                            ExposureSignals[exposureIndex] = new ToxicityExposureSignal
                            {
                                AUP = aup,
                                Exposure01 = exposure,
                                ToxemiaDelta = exposure * Constants.ExposureToxemiaMultiplier * dt,
                                EntityId = entityId,
                                ChemicalHash = PoisonGasHash,
                                Frame = Frame,
                                Flags = ToxicityExposureSignalFlagsActive,
                                _pad0 = 0,
                                _pad1 = 0,
                                _pad2 = 0ul,
                                _pad3 = 0ul
                            };
                        }
                    }

                    CorrosionTimers[i] += corrosionDelta;
                    ExposureAccumulators[i] += math.max(0f, exposure - 0.12f) * Constants.AcidCorrosionDamage * dt;
                    if (CorrosionTimers[i] >= 2f && ExposureAccumulators[i] > 0.0001f)
                    {
                        int statusIndex = SignalCounters[1];
                        if (statusIndex < StatusSignals.Length)
                        {
                            SignalCounters[1] = statusIndex + 1;
                            StatusSignals[statusIndex] = new ToxicityStatusSignal
                            {
                                AUP = aup,
                                Magnitude = math.saturate(ExposureAccumulators[i]),
                                TargetHash = entityId,
                                SourceHash = AcidChemicalHashValue,
                                StatusType = ToxicStatusType,
                                Frame = Frame,
                                SourceId = RuntimeSourceId,
                                TargetId = (ushort)math.min(entityId & 0xFFFFu, 0xFFFFu),
                                Channel = RuntimeChannel,
                                Flags = SignalFlagsCorrosion,
                                _pad0 = 0,
                                _pad1 = 0u,
                                _pad2 = 0ul
                            };
                        }

                        CorrosionTimers[i] = 0f;
                        ExposureAccumulators[i] = 0f;
                    }
                }
            }

            private static float SampleNearest(NativeArray<float> density, int resolution, double3 gridOriginAup, float cellSizeMeters, double3 aup)
            {
                float3 local = AupPrecisionMath.LocalDeltaFloat3(aup, gridOriginAup, float3.zero);
                float invCell = math.rcp(math.max(cellSizeMeters, NaNEpsilon));
                int3 c = (int3)math.round(local * invCell + resolution * 0.5f);
                c = math.clamp(c, int3.zero, new int3(resolution - 1));
                return density[Flatten(c.x, c.y, c.z, resolution)];
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct SignalHarvestJob : IJob
        {
            [NoAlias, ReadOnly] public NativeArray<float> Density;
            [NoAlias, ReadOnly] public NativeArray<MockWorldSampler> WorldSamples;
            [NoAlias] public NativeArray<ToxicBioluminescenceSignal> BiolumSignals;
            [NoAlias] public NativeArray<int> SignalCounters;
            public ToxicOutgassingConstants Constants;
            public double3 GridOriginAup;
            public float CellSizeMeters;
            public int Resolution;
            public float GlobalQualityWeight;
            public uint Frame;
            public uint ChemicalHash;

            public void Execute()
            {
                float quality = math.saturate(GlobalQualityWeight);
                int stride = (int)math.max(1, math.round(math.lerp(8f, 2f, quality)));
                int cellCount = Resolution * Resolution * Resolution;
                for (int index = 0; index < cellCount; index += stride)
                {
                    if (SignalCounters[2] >= BiolumSignals.Length)
                    {
                        return;
                    }

                    float density = Density[index];
                    MockWorldSampler world = WorldSamples[index];
                    if (density < Constants.BiolumDensityThreshold || world.FloraAbsorption01 <= 0.05f)
                    {
                        continue;
                    }

                    int z = index / (Resolution * Resolution);
                    int rem = index - z * Resolution * Resolution;
                    int y = rem / Resolution;
                    int x = rem - y * Resolution;
                    float3 local = (new float3(x, y, z) - (Resolution * 0.5f)) * CellSizeMeters;
                    int signalIndex = SignalCounters[2];
                    SignalCounters[2] = signalIndex + 1;
                    BiolumSignals[signalIndex] = new ToxicBioluminescenceSignal
                    {
                        AUP = GridOriginAup + new double3(local.x, local.y, local.z),
                        Intensity01 = math.saturate(density * math.rcp(math.max(Constants.MaxDensity, NaNEpsilon))),
                        ToxicDensity = density,
                        LocalNormal = world.SdfGradient,
                        ChemicalHash = ChemicalHash,
                        Frame = Frame,
                        CellIndex = (ushort)math.min(index, 0xFFFF),
                        Flags = ToxicBioluminescenceSignalFlagsActive,
                        _pad0 = 0,
                        _pad1 = 0ul
                    };
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct ScanTelemetryJob : IJob
        {
            [NoAlias, ReadOnly] public NativeArray<float> Density;
            [NoAlias, ReadOnly] public NativeArray<int> NanFlags;
            [NoAlias] public NativeArray<ToxicityGridTelemetryEntry> Scratch;
            public double3 GridOriginAup;
            public float CellSizeMeters;
            public int Resolution;
            public int CellCount;
            public int SourceCount;
            public int EntityCount;
            public float GlobalQualityWeight;
            public uint Frame;
            public byte Flags;

            public void Execute()
            {
                float maxDensity = 0f;
                float volume = 0f;
                uint hash = 2166136261u;
                byte nanDetected = 0;
                float cellVolume = CellSizeMeters * CellSizeMeters * CellSizeMeters;
                int count = math.min(CellCount, Density.Length);
                for (int i = 0; i < count; i++)
                {
                    float value = Density[i];
                    if (!math.isfinite(value) || NanFlags[i] != 0)
                    {
                        nanDetected = 1;
                        value = 0f;
                    }

                    maxDensity = math.max(maxDensity, value);
                    volume += value * cellVolume;
                    hash ^= math.asuint(value + i * 0.000001f);
                    hash *= 16777619u;
                }

                Scratch[0] = new ToxicityGridTelemetryEntry
                {
                    GridOriginAUP = GridOriginAup,
                    MaxDensity = maxDensity,
                    TotalPlumeVolume = volume,
                    GlobalQualityWeight = GlobalQualityWeight,
                    DiffusionCompleteMs = 0f,
                    StateHash = hash,
                    Frame = Frame,
                    ActiveResolution = (ushort)Resolution,
                    ActiveSources = (ushort)math.min(SourceCount, 0xFFFF),
                    ActiveEntities = (ushort)math.min(EntityCount, 0xFFFF),
                    Flags = (byte)(Flags | (nanDetected != 0 ? TelemetryFlagNaN : 0)),
                    NanDetected = nanDetected,
                    _pad0 = 0ul
                };
            }
        }
    }
}
