using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AI.Ecosystem
{
    /// <summary>
    /// Data-only ecology population governor. Storage is DataVault-owned; this component only schedules and publishes.
    /// </summary>
    public sealed class EcosystemPopulationBalancer : MonoBehaviour, IColdTickable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001EcosystemPopulationBalancerSignalPushDropCount;
        private const int TelemetryCapacity = HectonEcologyContract.PopulationTelemetryCapacity;
        private const int CounterCapacity = HectonEcologyContract.PopulationCounterCapacity;
        private const int CoefficientCapacity = HectonEcologyContract.PopulationCoefficientCapacity;
        private const int DefaultMaxEntities = HectonEcologyContract.DefaultMaxEntities;
        private const int DefaultMaxSectors = HectonEcologyContract.DefaultMaxSectors;
        private const int EntityDeathSignalLaneCapacity = HectonEcologyContract.EntityDeathSignalLaneCapacity;
        private const int DefaultCullEventCapacity = EntityDeathSignalLaneCapacity;
        private const int DefaultFreeRingCapacity = HectonEcologyContract.DefaultFreeRingCapacity;
        private const int MaxCoefficientJsonBytes = HectonEcologyContract.MaxCoefficientJsonBytes;
        private const int CoefficientFileReadBufferBytes = HectonEcologyContract.CoefficientFileReadBufferBytes;
        private const float ColdTickDeltaSeconds = HectonEcologyContract.ColdTickDeltaSeconds;
        private const float DefaultBiomassPerEntity = HectonEcologyContract.DefaultBiomassPerEntity;
        private const int DefaultMaxActivePreyPerSector = HectonEcologyContract.DefaultMaxActivePreyPerSector;
        private const uint EcologySourceHash = 0x45434F4Cu; // ECOL
        private const uint PostSimulationSystemHash = EcologySourceHash ^ 0x50534D45u; // PSME
        private const byte EcologyDeathSignalFlag = 1;
        private const uint TelemetryInvalidMathFlag = 1u << 0;
        private const uint TelemetryFallbackCoefficientsFlag = 1u << 1;
        private const uint TelemetryVaultMissingFlag = 1u << 2;
        private const uint TelemetryDirectorMissingFlag = 1u << 3;
        private const uint TelemetryCullEventOverflowFlag = 1u << 4;
        private const uint TelemetryFreeRingOverflowFlag = 1u << 5;
        private const uint TelemetryStaleFreeSlotFlag = 1u << 6;
        private const uint TelemetryEntityBuffersMissingFlag = 1u << 7;
        private const uint BlackBoxDumpIoFaultHash = 0x444D5046u; // DMPF
        private const uint BlackBoxMissingTelemetryHash = 0x444D504Du; // DMPM
        private const ulong DumpMagic = 0x504F504543544548UL;
        private const int DumpFormatVersion = 3;
        private const int DumpHeaderBytes = 32;
        private const string CoefficientsRelativePath = "Data/Precomputed/ecosystem_coefficients.json";
        private const string LegacyCoefficientsRelativePath = "Data/Precomputed/Ecosystem_Coefficients.json";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_ECOSYSTEM_POPULATION_BALANCER.bin";
        private const string DumpPayloadLabel = "ecosystemPopulationTelemetryDumpPayload";

        [SerializeField, Min(1)] private int maxEntities = DefaultMaxEntities;
        [SerializeField, Min(1)] private int maxSectors = DefaultMaxSectors;
        [SerializeField, Min(1)] private int cullEventCapacity = DefaultCullEventCapacity;
        [SerializeField, Min(1)] private int freeRingCapacity = DefaultFreeRingCapacity;
        [SerializeField, Min(1f)] private float biomassPerEntity = DefaultBiomassPerEntity;
        [SerializeField, Min(1)] private int maxActivePreyPerSector = DefaultMaxActivePreyPerSector;
        [SerializeField] private bool enableTier1FleeDown = true;

        private VaultGenerationHandle<EcosystemPopulationCoefficient> _coefficientHandle;
        private VaultGenerationHandle<EcosystemPopulationSectorState> _sectorStateHandle;
        private VaultGenerationHandle<EcosystemPopulationCullEvent> _cullEventHandle;
        private VaultGenerationHandle<EcosystemPopulationTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<EcosystemPopulationFreeSlot> _freeRingHandle;
        private VaultGenerationHandle<int> _counterHandle;
        private VaultGenerationHandle<AbsoluteUniversePosition> _entityAupHandle;
        private VaultGenerationHandle<uint> _entityFlagHandle;
        private IDataVault _dataVault;
        private IEcosystemDirectorService _ecosystemDirector;
        private PostSimulationPhaseSystem _postSimulationPhase;
        private JobHandle _balancerHandle;
        private int _sectorCount;
        private int _telemetryCursor;
        private uint _simulationFrameCounter;
        private bool _coefficientsLoaded;
        private bool _registeredColdTick;
        private bool _registeredPostSimulation;
        private bool _registeredHotSwap;
        private bool _jobScheduled;
        private bool _dumpedFault;
        private uint _runtimeFlags;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            TryRegisterHotSwapListener();
            RebindDataVaultForLifecycle(GlobalRegistry.DataVault);
            _ecosystemDirector = GlobalRegistry.EcosystemDirector;
            if (!EnsureVaultState())
                return;

            TryRegisterTicks();
        }

        private void OnDisable()
        {
            CompleteScheduledJobForTeardown();
            TryUnregisterTicks();
            TryUnregisterHotSwapListener();
            _jobScheduled = false;
            ClearCachedDependencies();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                RebindDataVaultForLifecycle(currentService is IDataVault currentVault ? currentVault : null);

                if (_dataVault == null)
                {
                    _runtimeFlags |= TelemetryVaultMissingFlag;
                    TryUnregisterTicks();
                    return;
                }

                _runtimeFlags &= ~TelemetryVaultMissingFlag;
                if (!EnsureVaultState())
                {
                    TryUnregisterTicks();
                    return;
                }

                TryRegisterTicks();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.EcosystemDirector)
            {
                _ecosystemDirector = currentService as IEcosystemDirectorService;
                if (_ecosystemDirector == null || !_ecosystemDirector.IsInitialized)
                    _runtimeFlags |= TelemetryDirectorMissingFlag;
                else
                    _runtimeFlags &= ~TelemetryDirectorMissingFlag;
            }
        }

        public void ColdTick()
        {
            if (_jobScheduled)
                return;

            if (!RefreshVaultStateReadinessNoGrow())
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            uint frame = AdvanceSimulationFrame();
            if (!TryBuildSectorState(vault, frame, out int entityCount, out int totalActiveEntities))
            {
                RecordEmptyTelemetry(vault, frame, totalActiveEntities);
                return;
            }

            if (_sectorCount <= 0 || entityCount <= 0)
            {
                RecordEmptyTelemetry(vault, frame, totalActiveEntities);
                return;
            }

            ScheduleBalancerJob(vault, frame, entityCount, totalActiveEntities);
        }

        private void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            if (!_jobScheduled)
                return;

            if (!DispatcherJobFence.TryComplete(ref _balancerHandle, forceComplete: false))
                return;

            _jobScheduled = false;
            PublishCompletedCullSignals();
        }

        private bool EnsureVaultState()
        {
            EcosystemPopulationLayoutManifest.VerifyColdBoot();

            IDataVault vault = EnsureDataVaultDependency();
            if (vault == null)
            {
                _runtimeFlags |= TelemetryVaultMissingFlag;
                return false;
            }
            _runtimeFlags &= ~TelemetryVaultMissingFlag;

            maxEntities = math.max(1, maxEntities);
            maxSectors = math.max(1, maxSectors);
            cullEventCapacity = math.clamp(cullEventCapacity, 1, EntityDeathSignalLaneCapacity);
            freeRingCapacity = math.max(1, freeRingCapacity);
            biomassPerEntity = math.max(1f, biomassPerEntity);
            maxActivePreyPerSector = math.max(1, maxActivePreyPerSector);
            bool hasEntityHandles = EnsureEntityHandles(vault);

            bool hasOwnedBuffers =
                TryResolveOrAcquire(
                    vault,
                    ref _coefficientHandle,
                    BufferID.EcosystemPopulationCoefficients,
                    CoefficientCapacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<EcosystemPopulationCoefficient> _) &&
                TryResolveOrAcquire(
                    vault,
                    ref _sectorStateHandle,
                    BufferID.EcosystemPopulationSectorState,
                    maxSectors,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<EcosystemPopulationSectorState> _) &&
                TryResolveOrAcquire(
                    vault,
                    ref _cullEventHandle,
                    BufferID.EcosystemPopulationCullEvents,
                    cullEventCapacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<EcosystemPopulationCullEvent> _) &&
                TryResolveOrAcquire(
                    vault,
                    ref _telemetryHandle,
                    BufferID.EcosystemPopulationTelemetryRing,
                    TelemetryCapacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<EcosystemPopulationTelemetryEntry> _) &&
                TryResolveOrAcquire(
                    vault,
                    ref _freeRingHandle,
                    BufferID.EcosystemPopulationFreeRing,
                    freeRingCapacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<EcosystemPopulationFreeSlot> _) &&
                TryResolveOrAcquire(
                    vault,
                    ref _counterHandle,
                    BufferID.EcosystemPopulationCounters,
                    CounterCapacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<int> _);

            if (!hasOwnedBuffers)
            {
                _runtimeFlags |= TelemetryVaultMissingFlag;
                return false;
            }

            if (hasEntityHandles)
            {
                _runtimeFlags &= ~TelemetryEntityBuffersMissingFlag;
            }
            else
            {
                _runtimeFlags |= TelemetryEntityBuffersMissingFlag;
            }

            if (!_coefficientsLoaded)
                LoadCoefficientsIntoVault(vault);

            EnsureDirectorDependency();
            return _coefficientsLoaded;
        }

        private bool RefreshVaultStateReadinessNoGrow()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                _runtimeFlags |= TelemetryVaultMissingFlag;
                return false;
            }

            bool ownedReady =
                TryOpenOwnedVaultView(vault, in _coefficientHandle, BufferID.EcosystemPopulationCoefficients, CoefficientCapacity, out NativeArray<EcosystemPopulationCoefficient> _) &&
                TryOpenOwnedVaultView(vault, in _sectorStateHandle, BufferID.EcosystemPopulationSectorState, math.max(1, maxSectors), out NativeArray<EcosystemPopulationSectorState> _) &&
                TryOpenOwnedVaultView(vault, in _cullEventHandle, BufferID.EcosystemPopulationCullEvents, math.clamp(cullEventCapacity, 1, EntityDeathSignalLaneCapacity), out NativeArray<EcosystemPopulationCullEvent> _) &&
                TryOpenOwnedVaultView(vault, in _telemetryHandle, BufferID.EcosystemPopulationTelemetryRing, TelemetryCapacity, out NativeArray<EcosystemPopulationTelemetryEntry> _) &&
                TryOpenOwnedVaultView(vault, in _freeRingHandle, BufferID.EcosystemPopulationFreeRing, math.max(1, freeRingCapacity), out NativeArray<EcosystemPopulationFreeSlot> _) &&
                TryOpenOwnedVaultView(vault, in _counterHandle, BufferID.EcosystemPopulationCounters, CounterCapacity, out NativeArray<int> _);

            if (!ownedReady || !_coefficientsLoaded)
            {
                _runtimeFlags |= TelemetryVaultMissingFlag;
                return false;
            }

            _runtimeFlags &= ~TelemetryVaultMissingFlag;

            bool entityReady =
                TryOpenExternalVaultView(vault, in _entityAupHandle, BufferID.EntityAUPs, 1, out NativeArray<AbsoluteUniversePosition> _) &&
                TryOpenExternalVaultView(vault, in _entityFlagHandle, BufferID.EntityFlags, 1, out NativeArray<uint> _);

            if (entityReady)
                _runtimeFlags &= ~TelemetryEntityBuffersMissingFlag;
            else
                _runtimeFlags |= TelemetryEntityBuffersMissingFlag;

            return entityReady;
        }

        private IDataVault EnsureDataVaultDependency()
        {
            return _dataVault;
        }

        private IEcosystemDirectorService EnsureDirectorDependency()
        {
            IEcosystemDirectorService director = _ecosystemDirector;
            if (director != null && director.IsInitialized)
                return director;

            return null;
        }

        private bool EnsureEntityHandles(IDataVault vault)
        {
            bool hasAupHandle = TryBorrowVaultView(
                vault,
                BufferID.EntityAUPs,
                ref _entityAupHandle,
                1,
                out NativeArray<AbsoluteUniversePosition> _);
            bool hasFlagHandle = TryBorrowVaultView(
                vault,
                BufferID.EntityFlags,
                ref _entityFlagHandle,
                1,
                out NativeArray<uint> _);

            if (!hasAupHandle)
                _entityAupHandle = default;
            if (!hasFlagHandle)
                _entityFlagHandle = default;

            return hasAupHandle && hasFlagHandle;
        }

        private void LoadCoefficientsIntoVault(IDataVault vault)
        {
            if (!TryOpenOwnedVaultView(vault, in _coefficientHandle, BufferID.EcosystemPopulationCoefficients, 1, out NativeArray<EcosystemPopulationCoefficient> coefficients))
                return;

            EcosystemPopulationCoefficient coefficient = EcosystemPopulationCoefficient.CreateDefault();
#if UNITY_EDITOR
            if (TryReadCoefficientJson(out EcosystemCoefficientJson json))
            {
                coefficient = EcosystemPopulationCoefficient.FromJson(in json);
                _runtimeFlags &= ~TelemetryFallbackCoefficientsFlag;
            }
            else
#endif
            {
                _runtimeFlags |= TelemetryFallbackCoefficientsFlag;
            }

            coefficient = EcosystemPopulationMath.SanitizeCoefficient(in coefficient);
            coefficients[0] = coefficient;
            _coefficientsLoaded = true;
        }

#if UNITY_EDITOR
        private static bool TryReadCoefficientJson(out EcosystemCoefficientJson coefficient)
        {
            coefficient = default;
            try
            {
                string path = BuildProjectRelativePath(CoefficientsRelativePath);
                if (!File.Exists(path))
                    path = BuildProjectRelativePath(LegacyCoefficientsRelativePath);
                if (!File.Exists(path))
                    return false;

                FileInfo fileInfo = new FileInfo(path);
                if (fileInfo.Length <= 0L || fileInfo.Length > MaxCoefficientJsonBytes)
                    return false;

                string json;
                using (FileStream stream = new FileStream(
                           path,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.Read,
                           CoefficientFileReadBufferBytes,
                           FileOptions.SequentialScan))
                using (StreamReader reader = new StreamReader(stream))
                {
                    json = reader.ReadToEnd();
                }

                if (!HasNonWhiteSpace(json != null ? json.AsSpan() : ReadOnlySpan<char>.Empty))
                    return false;

                coefficient = JsonUtility.FromJson<EcosystemCoefficientJson>(json);
            }
            catch (IOException)
            {
                coefficient = default;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                coefficient = default;
                return false;
            }
            catch (ArgumentException)
            {
                coefficient = default;
                return false;
            }
            catch (NotSupportedException)
            {
                coefficient = default;
                return false;
            }
            catch (InvalidOperationException)
            {
                coefficient = default;
                return false;
            }

            return coefficient.PreyCarryingCapacity > 0f;
        }
#endif

        private bool TryBuildSectorState(IDataVault vault, uint frame, out int entityCount, out int totalActiveEntities)
        {
            entityCount = 0;
            totalActiveEntities = 0;
            if (!TryOpenExternalVaultView(vault, in _entityAupHandle, BufferID.EntityAUPs, 1, out NativeArray<AbsoluteUniversePosition> entityAups) ||
                !TryOpenExternalVaultView(vault, in _entityFlagHandle, BufferID.EntityFlags, 1, out NativeArray<uint> entityFlags) ||
                !TryOpenOwnedVaultView(vault, in _sectorStateHandle, BufferID.EcosystemPopulationSectorState, 1, out NativeArray<EcosystemPopulationSectorState> sectorStates) ||
                !TryOpenOwnedVaultView(vault, in _coefficientHandle, BufferID.EcosystemPopulationCoefficients, 1, out NativeArray<EcosystemPopulationCoefficient> coefficients) ||
                !TryOpenOwnedVaultView(vault, in _freeRingHandle, BufferID.EcosystemPopulationFreeRing, 1, out NativeArray<EcosystemPopulationFreeSlot> freeRing) ||
                !TryOpenOwnedVaultView(vault, in _counterHandle, BufferID.EcosystemPopulationCounters, 1, out NativeArray<int> counters))
            {
                return false;
            }

            int sectorCapacity = math.min(maxSectors, sectorStates.Length);
            for (int i = 0; i < sectorCapacity; i++)
                sectorStates[i] = default;

            int counterCount = math.min(CounterCapacity, counters.Length);
            for (int i = 0; i < counterCount; i++)
                counters[i] = 0;

            int freeRingLength = freeRing.Length;
            for (int i = 0; i < freeRingLength; i++)
                freeRing[i] = default;
            int rebuiltFreeCount = 0;
            _runtimeFlags &= ~TelemetryFreeRingOverflowFlag;

            int scanCount = math.min(maxEntities, math.min(entityAups.Length, entityFlags.Length));
            int sectorCount = 0;
            for (int index = 0; index < scanCount; index++)
            {
                uint flags = entityFlags[index];
                if ((flags & EcosystemPopulationFlags.Flag_IsActive) != 0u)
                    totalActiveEntities++;

                if ((flags & EcosystemPopulationFlags.EcologyMask) == 0u)
                    continue;

                AbsoluteUniversePosition aup = entityAups[index];
                if (!EcosystemPopulationMath.IsFiniteAup(in aup))
                    continue;

                long sectorHash = EcosystemPopulationMath.ResolveSectorHash(in aup);
                int sectorIndex = EnsureSectorSlot(sectorStates, sectorCapacity, ref sectorCount, sectorHash, in aup);
                if (sectorIndex < 0)
                    continue;

                EcosystemPopulationSectorState state = sectorStates[sectorIndex];
                if ((flags & EcosystemPopulationFlags.Flag_IsPrey) != 0u)
                {
                    if ((flags & EcosystemPopulationFlags.Flag_IsActive) != 0u)
                    {
                        state.ActivePreyCount++;
                    }
                    else if ((flags & EcosystemPopulationFlags.Flag_FreeList) != 0u)
                    {
                        state.FreePreyCount++;
                        if (rebuiltFreeCount < freeRingLength)
                        {
                            freeRing[rebuiltFreeCount] = new EcosystemPopulationFreeSlot
                            {
                                SectorHash = sectorHash,
                                EntityIndex = index,
                                Frame = frame,
                                Flags = EcosystemPopulationFreeSlotFlags.Valid | EcosystemPopulationFreeSlotFlags.Prey
                            };
                            rebuiltFreeCount++;
                        }
                        else
                        {
                            _runtimeFlags |= TelemetryFreeRingOverflowFlag;
                        }
                    }
                }

                if ((flags & EcosystemPopulationFlags.Flag_IsPredator) != 0u &&
                    (flags & EcosystemPopulationFlags.Flag_IsActive) != 0u)
                {
                    state.ActivePredatorCount++;
                }

                sectorStates[sectorIndex] = state;
            }

            EcosystemPopulationCoefficient coefficient = EcosystemPopulationMath.SanitizeCoefficient(coefficients[0]);
            IEcosystemDirectorService director = _ecosystemDirector;
            if (director == null || !director.IsInitialized)
                _runtimeFlags |= TelemetryDirectorMissingFlag;
            else
                _runtimeFlags &= ~TelemetryDirectorMissingFlag;

            for (int i = 0; i < sectorCount; i++)
            {
                EcosystemPopulationSectorState state = sectorStates[i];
                if (director != null && director.IsInitialized && TryResolveRuntimePosition(in state.SampleAup, out Vector3 runtimePosition) &&
                    director.TryGetBiomassAvailability(runtimePosition, out float prey01, out float predator01, out float capacity01))
                {
                    float capacity = math.max(1f, coefficient.PreyCarryingCapacity * math.max(0.01f, math.saturate(capacity01)));
                    state.PreyBiomass = math.clamp(prey01, 0f, 1f) * capacity;
                    state.PredatorBiomass = math.clamp(predator01, 0f, 1f) * capacity;
                    state.MaxCapacity = capacity;
                }
                else
                {
                    state.PreyBiomass = math.max(0f, state.ActivePreyCount * biomassPerEntity);
                    state.PredatorBiomass = math.max(0f, state.ActivePredatorCount * biomassPerEntity);
                    state.MaxCapacity = coefficient.PreyCarryingCapacity;
                }

                sectorStates[i] = EcosystemPopulationMath.SanitizeSectorState(in state, coefficient.PreyCarryingCapacity);
            }

            _sectorCount = sectorCount;
            entityCount = scanCount;
            if (counterCount > EcosystemPopulationCounters.SectorCount)
                counters[EcosystemPopulationCounters.SectorCount] = sectorCount;
            if (counterCount > EcosystemPopulationCounters.TotalActiveEntities)
                counters[EcosystemPopulationCounters.TotalActiveEntities] = totalActiveEntities;
            if (counterCount > EcosystemPopulationCounters.FreeRingWriteCursor)
                counters[EcosystemPopulationCounters.FreeRingWriteCursor] = freeRingLength > 0 && rebuiltFreeCount < freeRingLength
                    ? rebuiltFreeCount
                    : 0;
            if (counterCount > EcosystemPopulationCounters.FreeRingCount)
                counters[EcosystemPopulationCounters.FreeRingCount] = rebuiltFreeCount;

            return true;
        }

        private void ScheduleBalancerJob(IDataVault vault, uint frame, int entityCount, int totalActiveEntities)
        {
            if (vault == null || vault.IsCompactionFenceActive)
                return;

            if (!TryOpenOwnedVaultView(vault, in _coefficientHandle, BufferID.EcosystemPopulationCoefficients, 1, out NativeArray<EcosystemPopulationCoefficient> coefficients) ||
                !TryOpenOwnedVaultView(vault, in _sectorStateHandle, BufferID.EcosystemPopulationSectorState, 1, out NativeArray<EcosystemPopulationSectorState> sectorStates) ||
                !TryOpenOwnedVaultView(vault, in _cullEventHandle, BufferID.EcosystemPopulationCullEvents, 1, out NativeArray<EcosystemPopulationCullEvent> cullEvents) ||
                !TryOpenOwnedVaultView(vault, in _telemetryHandle, BufferID.EcosystemPopulationTelemetryRing, 1, out NativeArray<EcosystemPopulationTelemetryEntry> telemetry) ||
                !TryOpenOwnedVaultView(vault, in _freeRingHandle, BufferID.EcosystemPopulationFreeRing, 1, out NativeArray<EcosystemPopulationFreeSlot> freeRing) ||
                !TryOpenOwnedVaultView(vault, in _counterHandle, BufferID.EcosystemPopulationCounters, 1, out NativeArray<int> counters) ||
                !TryOpenExternalVaultView(vault, in _entityAupHandle, BufferID.EntityAUPs, 1, out NativeArray<AbsoluteUniversePosition> entityAups) ||
                !TryOpenExternalVaultView(vault, in _entityFlagHandle, BufferID.EntityFlags, 1, out NativeArray<uint> entityFlags))
            {
                return;
            }

            if (vault.IsCompactionFenceActive)
                return;

            int telemetryIndex = ReserveTelemetryIndex(telemetry.Length);
            var job = new EcosystemBalancerJob
            {
                Coefficients = coefficients,
                SectorStates = sectorStates,
                EntityAups = entityAups,
                EntityFlags = entityFlags,
                CullEvents = cullEvents,
                FreeRing = freeRing,
                TelemetryRing = telemetry,
                Counters = counters,
                CullEventLimit = math.min(EntityDeathSignalLaneCapacity, cullEvents.Length),
                SectorCount = _sectorCount,
                EntityCount = entityCount,
                TotalActiveEntities = totalActiveEntities,
                TelemetryIndex = telemetryIndex,
                Frame = frame,
                DeltaSeconds = ColdTickDeltaSeconds,
                SystemStress01 = SignalBusRegistry.SystemStress01,
                BiomassPerEntity = math.max(1f, biomassPerEntity),
                MaxActivePreyPerSector = math.max(1, maxActivePreyPerSector),
                EnableTier1FleeDown = enableTier1FleeDown ? 1 : 0,
                RuntimeFlags = _runtimeFlags
            };

            try
            {
                _balancerHandle = job.Schedule();
            }
            catch (InvalidOperationException)
            {
                _balancerHandle = default;
                GlobalTelemetryBus.PublishPerformanceWarning(0x45504A53u, EcologySourceHash, 0f);
                return;
            }
            catch (ArgumentException)
            {
                _balancerHandle = default;
                GlobalTelemetryBus.PublishPerformanceWarning(0x45504A53u, EcologySourceHash, 0f);
                return;
            }

            H8Memory.RegisterActiveJob(SystemID.AIEcology, _balancerHandle);
            _jobScheduled = true;
        }

        private void RecordEmptyTelemetry(IDataVault vault, uint frame, int totalActiveEntities)
        {
            if (!TryOpenOwnedVaultView(vault, in _telemetryHandle, BufferID.EcosystemPopulationTelemetryRing, 1, out NativeArray<EcosystemPopulationTelemetryEntry> telemetry) ||
                !TryOpenOwnedVaultView(vault, in _counterHandle, BufferID.EcosystemPopulationCounters, 1, out NativeArray<int> counters))
                return;

            int telemetryIndex = ReserveTelemetryIndex(telemetry.Length);
            int freeRingCount = counters.Length > EcosystemPopulationCounters.FreeRingCount
                ? math.max(0, counters[EcosystemPopulationCounters.FreeRingCount])
                : 0;
            float systemStress01 = math.saturate(SignalBusRegistry.SystemStress01);
            telemetry[telemetryIndex] = new EcosystemPopulationTelemetryEntry
            {
                Frame = frame,
                TotalActiveEntities = totalActiveEntities,
                SectorCount = 0,
                FreeRingCount = freeRingCount,
                SystemStress01 = systemStress01,
                Flags = _runtimeFlags,
                StateHash = EcosystemPopulationMath.MixTelemetryHash(totalActiveEntities, 0, 0, 0)
            };

            if (counters.Length > EcosystemPopulationCounters.TotalActiveEntities)
                counters[EcosystemPopulationCounters.TotalActiveEntities] = totalActiveEntities;
        }

        private uint AdvanceSimulationFrame()
        {
            uint next = _simulationFrameCounter + 1u;
            if (next == 0u)
                next = 1u;

            _simulationFrameCounter = next;
            return next;
        }

        private int ReserveTelemetryIndex(int telemetryLength)
        {
            if (telemetryLength <= 0)
                return 0;

            int index = PositiveModulo(_telemetryCursor, telemetryLength);
            if (_telemetryCursor == int.MaxValue)
            {
                int nextIndex = index + 1;
                if (nextIndex >= telemetryLength)
                    nextIndex = 0;
                _telemetryCursor = telemetryLength + nextIndex;
            }
            else
            {
                _telemetryCursor++;
            }

            return index;
        }

        private void PublishCompletedCullSignals()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            if (!TryOpenOwnedVaultView(vault, in _cullEventHandle, BufferID.EcosystemPopulationCullEvents, 1, out NativeArray<EcosystemPopulationCullEvent> cullEvents) ||
                !TryOpenOwnedVaultView(vault, in _counterHandle, BufferID.EcosystemPopulationCounters, 1, out NativeArray<int> counters))
                return;

            TryOpenOwnedVaultView(vault, in _telemetryHandle, BufferID.EcosystemPopulationTelemetryRing, 1, out NativeArray<EcosystemPopulationTelemetryEntry> telemetry);

            int eventCount = counters.Length > EcosystemPopulationCounters.CullEventCount
                ? math.clamp(counters[EcosystemPopulationCounters.CullEventCount], 0, cullEvents.Length)
                : 0;

            for (int i = 0; i < eventCount; i++)
            {
                EcosystemPopulationCullEvent cullEvent = cullEvents[i];
                if (cullEvent.EntityIndex < 0)
                    continue;

                EntityDeathSignal signal = new EntityDeathSignal
                {
                    PositionAup = cullEvent.PositionAup,
                    EntityHash = cullEvent.EntityHash,
                    SourceHash = EcologySourceHash,
                    Intensity01 = math.saturate(cullEvent.Intensity01),
                    Flags = EcologyDeathSignalFlag
                };
                SignalBus<EntityDeathSignal>.TryPushTracked(in signal, ref s_x001EcosystemPopulationBalancerSignalPushDropCount);
            }

            bool invalidMath = counters.Length > EcosystemPopulationCounters.InvalidMathRecovered &&
                               counters[EcosystemPopulationCounters.InvalidMathRecovered] != 0;
            if (invalidMath && !_dumpedFault)
            {
                _dumpedFault = true;
                DumpBlackBox(telemetry, _telemetryCursor);
            }
        }

        private void CompleteScheduledJobForTeardown()
        {
            if (!_jobScheduled)
                return;

            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                if (!DispatcherJobFence.TryComplete(ref _balancerHandle, forceComplete: true))
                    return;
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }

            _jobScheduled = false;
            PublishCompletedCullSignals();
        }

        private void TryRegisterTicks()
        {
            if (!_registeredPostSimulation)
            {
                if (_postSimulationPhase == null)
                    _postSimulationPhase = new PostSimulationPhaseSystem(this);

                _registeredPostSimulation = GlobalRegistry.TryRegisterDispatcherSystem(_postSimulationPhase);
            }

            if (!_registeredPostSimulation)
                return;

            if (!_registeredColdTick)
                _registeredColdTick = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment);
            if (!_registeredColdTick)
                TryUnregisterTicks();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterTicks()
        {
            if (_registeredColdTick)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
                _registeredColdTick = false;
            }

            if (_registeredPostSimulation)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_postSimulationPhase);
                _registeredPostSimulation = false;
            }
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private sealed class PostSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly EcosystemPopulationBalancer _owner;

            public PostSimulationPhaseSystem(EcosystemPopulationBalancer owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => PostSimulationSystemHash;

            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.PostSimulation;

            public byte GetBucketId() => byte.MaxValue;

            public int GetDependencyCount() => 0;

            public uint GetDependencyHash(int dependencyIndex) => 0u;

            public void PreSimulationTick(in DispatcherTimingDTO timing)
            {
            }

            public JobHandle ScheduleSimulation(
                in DispatcherTimingDTO timing,
                in DispatcherJobContext context,
                JobHandle dependsOn)
            {
                return dependsOn;
            }

            public void PostSimulationTick(in DispatcherTimingDTO timing)
            {
                _owner?.PostSimulationTick(in timing);
            }

            public void VisualSyncTick(in DispatcherTimingDTO timing)
            {
            }
        }

        private void ClearCachedDependencies()
        {
            RebindDataVaultForLifecycle(null);
            _ecosystemDirector = null;
        }

        private void RebindDataVaultForLifecycle(IDataVault currentVault)
        {
            if (ReferenceEquals(_dataVault, currentVault))
            {
                if (currentVault == null)
                    ResetVaultRuntimeState();
                return;
            }

            CompleteScheduledJobForTeardown();
            ReleaseOwnedVaultHandles(_dataVault);
            _dataVault = currentVault;
            ResetVaultRuntimeState();
        }

        private void ResetVaultRuntimeState()
        {
            _coefficientsLoaded = false;
            _sectorCount = 0;
            _telemetryCursor = 0;
            _simulationFrameCounter = 0u;
            _dumpedFault = false;
            ResetVaultHandles();
        }

        private void ResetVaultHandles()
        {
            _coefficientHandle = default;
            _sectorStateHandle = default;
            _cullEventHandle = default;
            _telemetryHandle = default;
            _freeRingHandle = default;
            _counterHandle = default;
            _entityAupHandle = default;
            _entityFlagHandle = default;
        }

        private static bool TryResolveOrAcquire<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (IsOwnedVaultHandle(in handle, bufferId) &&
                TryOpenOwnedVaultView(vault, in handle, bufferId, requiredLength, out buffer))
            {
                return true;
            }

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existingHandle) &&
                IsOwnedVaultHandle(in existingHandle, bufferId) &&
                TryOpenOwnedVaultView(vault, in existingHandle, bufferId, requiredLength, out buffer))
            {
                handle = existingHandle;
                return true;
            }

            if (vault.IsAllocationLocked)
            {
                handle = default;
                buffer = default;
                return false;
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.AIEcology,
                options);

            if (TryOpenOwnedVaultView(vault, in handle, bufferId, requiredLength, out buffer))
                return true;

            ReleaseVaultHandle(vault, ref handle, bufferId);
            buffer = default;
            return false;
        }

        private static bool TryBorrowVaultView<T>(
            IDataVault vault,
            BufferID bufferId,
            ref VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (TryOpenExternalVaultView(vault, in handle, bufferId, requiredLength, out buffer))
                return true;

            if (!vault.TryGetGenerationHandle<T>(bufferId, out handle))
            {
                handle = default;
                return false;
            }

            if (TryOpenExternalVaultView(vault, in handle, bufferId, requiredLength, out buffer))
                return true;

            handle = default;
            buffer = default;
            return false;
        }

        private static bool TryOpenOwnedVaultView<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return IsOwnedVaultHandle(in handle, expectedBufferId) &&
                   TryOpenVaultView(vault, in handle, requiredLength, out buffer);
        }

        private static bool TryOpenExternalVaultView<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return IsVaultHandleForBuffer(in handle, expectedBufferId) &&
                   TryOpenVaultView(vault, in handle, requiredLength, out buffer);
        }

        private static bool TryOpenVaultView<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   requiredLength >= 0 &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private void ReleaseOwnedVaultHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            ReleaseVaultHandle(vault, ref _coefficientHandle, BufferID.EcosystemPopulationCoefficients);
            ReleaseVaultHandle(vault, ref _sectorStateHandle, BufferID.EcosystemPopulationSectorState);
            ReleaseVaultHandle(vault, ref _cullEventHandle, BufferID.EcosystemPopulationCullEvents);
            ReleaseVaultHandle(vault, ref _telemetryHandle, BufferID.EcosystemPopulationTelemetryRing);
            ReleaseVaultHandle(vault, ref _freeRingHandle, BufferID.EcosystemPopulationFreeRing);
            ReleaseVaultHandle(vault, ref _counterHandle, BufferID.EcosystemPopulationCounters);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle, BufferID expectedBufferId)
            where T : struct
        {
            if (IsOwnedVaultHandle(in handle, expectedBufferId))
            {
                vault.ReleaseBuffer(in handle);
            }

            handle = default;
        }

        private static bool IsVaultHandleForBuffer<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId)
            where T : struct
        {
            return handle.BufferID == (uint)expectedBufferId &&
                   handle.Generation != 0u;
        }

        private static bool IsOwnedVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId)
            where T : struct
        {
            return IsVaultHandleForBuffer(in handle, expectedBufferId) &&
                   handle.SystemID == (uint)SystemID.AIEcology;
        }

        private static int EnsureSectorSlot(
            NativeArray<EcosystemPopulationSectorState> states,
            int capacity,
            ref int count,
            long sectorHash,
            in AbsoluteUniversePosition sampleAup)
        {
            for (int i = 0; i < count; i++)
            {
                if (states[i].SectorHash == sectorHash)
                    return i;
            }

            if (count >= capacity)
                return -1;

            int slot = count++;
            states[slot] = new EcosystemPopulationSectorState
            {
                SectorHash = sectorHash,
                SampleAup = sampleAup
            };
            return slot;
        }

        private static bool TryResolveRuntimePosition(in AbsoluteUniversePosition aup, out Vector3 runtimePosition)
        {
            runtimePosition = default;
            if (!AbsoluteUniversePosition.IsFinite(in aup))
                return false;

            float3 runtime = aup.ToRuntimeFloat3();
            bool finite = math.all(math.isfinite(runtime));
            runtimePosition = finite ? new Vector3(runtime.x, runtime.y, runtime.z) : default;
            return finite;
        }

        private static string BuildProjectRelativePath(string relativePath)
        {
            string assetsPath = Application.dataPath;
            string root = Directory.GetParent(assetsPath) != null
                ? Directory.GetParent(assetsPath).FullName
                : assetsPath;
            return Path.Combine(root, relativePath);
        }

        private static bool HasNonWhiteSpace(ReadOnlySpan<char> value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (!IsAsciiWhiteSpace(value[i]))
                    return true;
            }

            return false;
        }

        private static bool IsAsciiWhiteSpace(char value)
        {
            return value == ' ' || (uint)(value - '\t') <= 4u;
        }

        private static unsafe void DumpBlackBox(NativeArray<EcosystemPopulationTelemetryEntry> telemetry, int telemetryCursor)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(BlackBoxMissingTelemetryHash, EcologySourceHash, 0f);
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)EcologySourceHash));
                return;
            }

            int entrySize = UnsafeUtility.SizeOf<EcosystemPopulationTelemetryEntry>();
            int count = math.min(telemetry.Length, TelemetryCapacity);
            int byteCount = DumpHeaderBytes + count * entrySize;
            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(EcosystemPopulationBalancer),
                    DumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                WriteUInt64LittleEndian(target, 0, DumpMagic);
                WriteInt32LittleEndian(target, 8, DumpFormatVersion);
                WriteInt32LittleEndian(target, 12, telemetryCursor);
                WriteInt32LittleEndian(target, 16, count);
                WriteInt32LittleEndian(target, 20, entrySize);
                WriteUInt32LittleEndian(target, 24, EcologySourceHash);
                WriteUInt32LittleEndian(target, 28, 0u);

                int start = telemetryCursor - count;
                while (start < 0)
                    start += telemetry.Length;
                if (start >= telemetry.Length)
                    start %= telemetry.Length;

                int cursor = DumpHeaderBytes;
                for (int i = 0; i < count; i++)
                {
                    int slot = start + i;
                    if (slot >= telemetry.Length)
                        slot -= telemetry.Length;

                    EcosystemPopulationTelemetryEntry entry = telemetry[slot];
                    UnsafeUtility.MemCpy(target + cursor, &entry, entrySize);
                    cursor += entrySize;
                }

                if (!NativeFaultDumpWriter.TryWriteAll(DumpRelativePath, payload, cursor))
                    GlobalTelemetryBus.PublishPerformanceWarning(BlackBoxDumpIoFaultHash, EcologySourceHash, 0f);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(EcosystemPopulationBalancer),
                    DumpPayloadLabel);
            }

            GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)EcologySourceHash));
        }

        private static unsafe void WriteInt32LittleEndian(byte* destination, int offset, int value)
        {
            WriteUInt32LittleEndian(destination, offset, unchecked((uint)value));
        }

        private static unsafe void WriteUInt32LittleEndian(byte* destination, int offset, uint value)
        {
            destination[offset] = unchecked((byte)value);
            destination[offset + 1] = unchecked((byte)(value >> 8));
            destination[offset + 2] = unchecked((byte)(value >> 16));
            destination[offset + 3] = unchecked((byte)(value >> 24));
        }

        private static unsafe void WriteUInt64LittleEndian(byte* destination, int offset, ulong value)
        {
            WriteUInt32LittleEndian(destination, offset, unchecked((uint)value));
            WriteUInt32LittleEndian(destination, offset + 4, unchecked((uint)(value >> 32)));
        }

        private static int PositiveModulo(int value, int length)
        {
            if (length <= 0)
                return 0;

            int result = value % length;
            return result < 0 ? result + length : result;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct EcosystemBalancerJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<EcosystemPopulationCoefficient> Coefficients;
            [NoAlias] public NativeArray<EcosystemPopulationSectorState> SectorStates;
            [ReadOnly, NoAlias] public NativeArray<AbsoluteUniversePosition> EntityAups;
            [NoAlias] public NativeArray<uint> EntityFlags;
            [NoAlias] public NativeArray<EcosystemPopulationCullEvent> CullEvents;
            [NoAlias] public NativeArray<EcosystemPopulationFreeSlot> FreeRing;
            [NoAlias] public NativeArray<EcosystemPopulationTelemetryEntry> TelemetryRing;
            [NoAlias] public NativeArray<int> Counters;
            public int CullEventLimit;
            public int SectorCount;
            public int EntityCount;
            public int TotalActiveEntities;
            public int TelemetryIndex;
            public uint Frame;
            public float DeltaSeconds;
            public float SystemStress01;
            public float BiomassPerEntity;
            public int MaxActivePreyPerSector;
            public int EnableTier1FleeDown;
            public uint RuntimeFlags;

            public void Execute()
            {
                int sectorCount = math.clamp(SectorCount, 0, SectorStates.Length);
                int entityCount = math.clamp(EntityCount, 0, math.min(EntityAups.Length, EntityFlags.Length));
                int culled = 0;
                int spawned = 0;
                int fleeDown = 0;
                int invalidMath = 0;
                int eventOverflow = 0;
                int staleFreeSlot = 0;
                int eventLimit = math.clamp(CullEventLimit, 0, CullEvents.Length);
                int freeWriteCursor = Counters.Length > EcosystemPopulationCounters.FreeRingWriteCursor
                    ? math.max(0, Counters[EcosystemPopulationCounters.FreeRingWriteCursor])
                    : 0;
                int freeCount = Counters.Length > EcosystemPopulationCounters.FreeRingCount
                    ? math.clamp(Counters[EcosystemPopulationCounters.FreeRingCount], 0, FreeRing.Length)
                    : 0;
                if (FreeRing.Length <= 0)
                    freeWriteCursor = 0;
                else if (freeWriteCursor >= FreeRing.Length)
                    freeWriteCursor %= FreeRing.Length;

                EcosystemPopulationCoefficient coefficient = Coefficients.Length > 0
                    ? EcosystemPopulationMath.SanitizeCoefficient(Coefficients[0])
                    : EcosystemPopulationCoefficient.CreateDefault();

                for (int sectorIndex = 0; sectorIndex < sectorCount; sectorIndex++)
                {
                    EcosystemPopulationSectorState rawState = SectorStates[sectorIndex];
                    EcosystemPopulationSectorState state = EcosystemPopulationMath.SanitizeSectorState(
                        in rawState,
                        coefficient.PreyCarryingCapacity);
                    float capacity = math.max(1f, state.MaxCapacity);
                    float prey = math.clamp(state.PreyBiomass, 0f, capacity);
                    float predator = math.clamp(state.PredatorBiomass, 0f, capacity);
                    float dt = math.max(0f, math.min(DeltaSeconds, 4f));
                    float invCapacity = math.rcp(math.max(1f, capacity));
                    float dPrey = (coefficient.BirthRate * prey * (1f - (prey * invCapacity))) -
                                  (coefficient.FeedRate * prey * predator);
                    float dPredator = (coefficient.PredatorConversion * coefficient.FeedRate * prey * predator) -
                                      (coefficient.DeathRate * predator);
                    float nextPrey = math.clamp(prey + (dPrey * dt), 0f, capacity);
                    float nextPredator = math.clamp(predator + (dPredator * dt), 0f, capacity);
                    if (!math.isfinite(nextPrey))
                    {
                        nextPrey = 0f;
                        invalidMath = 1;
                    }

                    if (!math.isfinite(nextPredator))
                    {
                        nextPredator = 0f;
                        invalidMath = 1;
                    }

                    int desiredPrey = math.clamp(
                        (int)math.round(nextPrey * math.rcp(math.max(1f, BiomassPerEntity))),
                        0,
                        math.max(1, MaxActivePreyPerSector));
                    int currentPrey = math.max(0, state.ActivePreyCount);
                    int cullNeeded = math.max(0, currentPrey - desiredPrey);

                    int preyCull = CullTier2EntitiesInSector(
                        state.SectorHash,
                        cullNeeded,
                        entityCount,
                        EcosystemPopulationFlags.Flag_IsPrey,
                        requireAnyEcologyKind: false,
                        freeWriteCursor: ref freeWriteCursor,
                        freeCount: ref freeCount,
                        culledTotal: ref culled,
                        eventLimit: eventLimit,
                        eventOverflow: ref eventOverflow);
                    int sectorCull = preyCull;
                    int remainingCull = cullNeeded - preyCull;
                    int sectorFleeDown = remainingCull > 0 && EnableTier1FleeDown != 0
                        ? RequestTier1FleeDownInSector(state.SectorHash, remainingCull, entityCount)
                        : 0;
                    fleeDown += sectorFleeDown;

                    int spawnNeeded = math.max(0, desiredPrey - currentPrey);
                    int sectorSpawned = spawnNeeded > 0
                        ? ReactivateFreePreyInSector(state.SectorHash, spawnNeeded, entityCount, ref freeCount, ref staleFreeSlot)
                        : 0;
                    spawned += sectorSpawned;

                    state.PreyBiomass = nextPrey;
                    state.PredatorBiomass = nextPredator;
                    state.DesiredPreyCount = desiredPrey;
                    state.LastCulled = sectorCull;
                    state.LastSpawned = sectorSpawned;
                    state.LastFleeDown = sectorFleeDown;
                    state.Flags = (invalidMath != 0 ? TelemetryInvalidMathFlag : 0u) |
                                  (eventOverflow != 0 ? TelemetryCullEventOverflowFlag : 0u) |
                                  (staleFreeSlot != 0 ? TelemetryStaleFreeSlotFlag : 0u);
                    SectorStates[sectorIndex] = state;
                }

                if (Counters.Length > EcosystemPopulationCounters.CulledByEcology)
                    Counters[EcosystemPopulationCounters.CulledByEcology] = culled;
                if (Counters.Length > EcosystemPopulationCounters.SpawnedByEcology)
                    Counters[EcosystemPopulationCounters.SpawnedByEcology] = spawned;
                if (Counters.Length > EcosystemPopulationCounters.FleeDownRequests)
                    Counters[EcosystemPopulationCounters.FleeDownRequests] = fleeDown;
                if (Counters.Length > EcosystemPopulationCounters.FreeRingWriteCursor)
                    Counters[EcosystemPopulationCounters.FreeRingWriteCursor] = freeWriteCursor;
                if (Counters.Length > EcosystemPopulationCounters.FreeRingCount)
                    Counters[EcosystemPopulationCounters.FreeRingCount] = freeCount;
                if (Counters.Length > EcosystemPopulationCounters.InvalidMathRecovered)
                    Counters[EcosystemPopulationCounters.InvalidMathRecovered] = invalidMath;

                uint flags = RuntimeFlags |
                             (invalidMath != 0 ? TelemetryInvalidMathFlag : 0u) |
                             (eventOverflow != 0 ? TelemetryCullEventOverflowFlag : 0u) |
                             (staleFreeSlot != 0 ? TelemetryStaleFreeSlotFlag : 0u);
                if (TelemetryRing.IsCreated && TelemetryRing.Length > 0)
                {
                    int telemetryIndex = math.clamp(TelemetryIndex, 0, TelemetryRing.Length - 1);
                    TelemetryRing[telemetryIndex] = new EcosystemPopulationTelemetryEntry
                    {
                        Frame = Frame,
                        StateHash = EcosystemPopulationMath.MixTelemetryHash(TotalActiveEntities, culled, spawned, sectorCount),
                        TotalActiveEntities = TotalActiveEntities,
                        CulledByEcology = culled,
                        SpawnedByEcology = spawned,
                        FleeDownRequests = fleeDown,
                        SectorCount = sectorCount,
                        FreeRingCount = freeCount,
                        SystemStress01 = math.saturate(SystemStress01),
                        Flags = flags
                    };
                }
            }

            private int CullTier2EntitiesInSector(
                long sectorHash,
                int cullNeeded,
                int entityCount,
                uint requiredKindMask,
                bool requireAnyEcologyKind,
                ref int freeWriteCursor,
                ref int freeCount,
                ref int culledTotal,
                int eventLimit,
                ref int eventOverflow)
            {
                if (cullNeeded <= 0)
                    return 0;

                int culled = 0;
                int eventCount = Counters.Length > EcosystemPopulationCounters.CullEventCount
                    ? math.clamp(Counters[EcosystemPopulationCounters.CullEventCount], 0, eventLimit)
                    : 0;
                for (int entityIndex = 0; entityIndex < entityCount && culled < cullNeeded; entityIndex++)
                {
                    if (eventCount >= eventLimit)
                    {
                        eventOverflow = 1;
                        break;
                    }

                    uint flags = EntityFlags[entityIndex];
                    uint required = EcosystemPopulationFlags.Flag_IsActive |
                                    EcosystemPopulationFlags.Flag_Tier2Frozen;
                    if ((flags & required) != required)
                        continue;
                    if (requiredKindMask != 0u && (flags & requiredKindMask) != requiredKindMask)
                        continue;
                    if (requireAnyEcologyKind && (flags & (EcosystemPopulationFlags.Flag_IsPrey | EcosystemPopulationFlags.Flag_IsPredator)) == 0u)
                        continue;

                    AbsoluteUniversePosition aup = EntityAups[entityIndex];
                    if (!EcosystemPopulationMath.IsFiniteAup(in aup) ||
                        EcosystemPopulationMath.ResolveSectorHash(in aup) != sectorHash)
                    {
                        continue;
                    }

                    bool canEnterFreeRing = (flags & EcosystemPopulationFlags.Flag_IsPrey) != 0u;
                    uint nextFlags = (flags & ~EcosystemPopulationFlags.Flag_IsActive) |
                                     EcosystemPopulationFlags.Flag_CulledByEcology;
                    EntityFlags[entityIndex] = canEnterFreeRing
                        ? nextFlags | EcosystemPopulationFlags.Flag_FreeList
                        : nextFlags & ~EcosystemPopulationFlags.Flag_FreeList;
                    if (eventCount < eventLimit)
                    {
                        CullEvents[eventCount] = new EcosystemPopulationCullEvent
                        {
                            PositionAup = aup,
                            EntityHash = EcosystemPopulationMath.ResolveEntityHash(entityIndex, sectorHash),
                            SectorHash = sectorHash,
                            EntityIndex = entityIndex,
                            Intensity01 = 1f,
                            Flags = EcosystemPopulationCullEventFlags.CulledByEcology
                        };
                        eventCount++;
                    }

                    if (FreeRing.Length > 0 && canEnterFreeRing)
                    {
                        int ringIndex = freeWriteCursor;
                        if (ringIndex < 0 || ringIndex >= FreeRing.Length)
                            ringIndex = 0;
                        FreeRing[ringIndex] = new EcosystemPopulationFreeSlot
                        {
                            SectorHash = sectorHash,
                            EntityIndex = entityIndex,
                            Frame = Frame,
                            Flags = EcosystemPopulationFreeSlotFlags.Valid | EcosystemPopulationFreeSlotFlags.Prey
                        };
                        freeWriteCursor = ringIndex + 1;
                        if (freeWriteCursor >= FreeRing.Length)
                            freeWriteCursor = 0;
                        freeCount = math.min(FreeRing.Length, freeCount + 1);
                    }

                    culled++;
                    culledTotal++;
                }

                if (Counters.Length > EcosystemPopulationCounters.CullEventCount)
                    Counters[EcosystemPopulationCounters.CullEventCount] = eventCount;
                return culled;
            }

            private int RequestTier1FleeDownInSector(long sectorHash, int countNeeded, int entityCount)
            {
                int requested = 0;
                for (int entityIndex = 0; entityIndex < entityCount && requested < countNeeded; entityIndex++)
                {
                    uint flags = EntityFlags[entityIndex];
                    uint required = EcosystemPopulationFlags.Flag_IsActive |
                                    EcosystemPopulationFlags.Flag_IsPrey |
                                    EcosystemPopulationFlags.Flag_Tier1Loaded;
                    if ((flags & required) != required ||
                        (flags & EcosystemPopulationFlags.Flag_EcologyFleeDown) != 0u)
                    {
                        continue;
                    }

                    AbsoluteUniversePosition aup = EntityAups[entityIndex];
                    if (!EcosystemPopulationMath.IsFiniteAup(in aup) ||
                        EcosystemPopulationMath.ResolveSectorHash(in aup) != sectorHash)
                    {
                        continue;
                    }

                    EntityFlags[entityIndex] = flags | EcosystemPopulationFlags.Flag_EcologyFleeDown;
                    requested++;
                }

                return requested;
            }

            private int ReactivateFreePreyInSector(
                long sectorHash,
                int spawnNeeded,
                int entityCount,
                ref int freeCount,
                ref int staleFreeSlot)
            {
                int spawned = 0;
                for (int ringIndex = 0; ringIndex < FreeRing.Length && spawned < spawnNeeded; ringIndex++)
                {
                    EcosystemPopulationFreeSlot slot = FreeRing[ringIndex];
                    if ((slot.Flags & EcosystemPopulationFreeSlotFlags.Valid) == 0u)
                    {
                        continue;
                    }

                    if (slot.EntityIndex < 0 || slot.EntityIndex >= entityCount)
                    {
                        ClearStaleFreeSlot(ringIndex, ref freeCount, ref staleFreeSlot);
                        continue;
                    }

                    if ((slot.Flags & EcosystemPopulationFreeSlotFlags.Prey) == 0u ||
                        slot.SectorHash != sectorHash)
                    {
                        continue;
                    }

                    uint flags = EntityFlags[slot.EntityIndex];
                    if ((flags & EcosystemPopulationFlags.Flag_IsActive) != 0u)
                    {
                        ClearStaleFreeSlot(ringIndex, ref freeCount, ref staleFreeSlot);
                        continue;
                    }

                    const uint requiredFlags = EcosystemPopulationFlags.Flag_IsPrey |
                                               EcosystemPopulationFlags.Flag_FreeList;
                    if ((flags & requiredFlags) != requiredFlags)
                    {
                        ClearStaleFreeSlot(ringIndex, ref freeCount, ref staleFreeSlot);
                        continue;
                    }

                    AbsoluteUniversePosition aup = EntityAups[slot.EntityIndex];
                    if (!EcosystemPopulationMath.IsFiniteAup(in aup) ||
                        EcosystemPopulationMath.ResolveSectorHash(in aup) != sectorHash)
                    {
                        ClearStaleFreeSlot(ringIndex, ref freeCount, ref staleFreeSlot);
                        continue;
                    }

                    EntityFlags[slot.EntityIndex] =
                        (flags | EcosystemPopulationFlags.Flag_IsActive | EcosystemPopulationFlags.Flag_IsPrey | EcosystemPopulationFlags.Flag_Tier2Frozen) &
                        ~(EcosystemPopulationFlags.Flag_FreeList |
                          EcosystemPopulationFlags.Flag_CulledByEcology |
                          EcosystemPopulationFlags.Flag_EcologyFleeDown);
                    FreeRing[ringIndex] = default;
                    freeCount = math.max(0, freeCount - 1);
                    spawned++;
                }

                return spawned;
            }

            private void ClearStaleFreeSlot(int ringIndex, ref int freeCount, ref int staleFreeSlot)
            {
                FreeRing[ringIndex] = default;
                freeCount = math.max(0, freeCount - 1);
                staleFreeSlot = 1;
            }
        }
    }

    [Serializable]
    internal struct EcosystemCoefficientJson
    {
        public float BirthRate;
        public float DeathRate;
        public float DeltaTimeSeconds;
        public float FeedRate;
        public float FinalPredatorBiomass;
        public float FinalPreyBiomass;
        public int IntegrationSteps;
        public float ObservedPredatorMax;
        public float ObservedPredatorMin;
        public float ObservedPreyMax;
        public float ObservedPreyMin;
        public float PredatorConversion;
        public float PreyCarryingCapacity;
        public float StablePredatorBiomass;
        public float StablePreyBiomass;
    }

    internal static class EcosystemPopulationFlags
    {
        public const uint Flag_IsActive = 1u << 0;
        public const uint Flag_IsPrey = 1u << 16;
        public const uint Flag_IsPredator = 1u << 17;
        public const uint Flag_Tier2Frozen = 1u << 18;
        public const uint Flag_Tier1Loaded = 1u << 19;
        public const uint Flag_EcologyFleeDown = 1u << 20;
        public const uint Flag_CulledByEcology = 1u << 21;
        public const uint Flag_FreeList = 1u << 22;
        public const uint EcologyMask = Flag_IsPrey | Flag_IsPredator | Flag_Tier2Frozen | Flag_Tier1Loaded | Flag_EcologyFleeDown | Flag_CulledByEcology | Flag_FreeList;
    }

    internal static class EcosystemPopulationCounters
    {
        public const int TotalActiveEntities = 0;
        public const int CulledByEcology = 1;
        public const int SpawnedByEcology = 2;
        public const int FleeDownRequests = 3;
        public const int CullEventCount = 4;
        public const int FreeRingWriteCursor = 5;
        public const int FreeRingCount = 6;
        public const int SectorCount = 7;
        public const int InvalidMathRecovered = 8;
    }

    internal static class EcosystemPopulationCullEventFlags
    {
        public const uint CulledByEcology = 1u << 0;
    }

    internal static class EcosystemPopulationFreeSlotFlags
    {
        public const uint Valid = 1u << 0;
        public const uint Prey = 1u << 1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct EcosystemPopulationCoefficient
    {
        [FieldOffset(0)]
        public float BirthRate;
        [FieldOffset(4)]
        public float DeathRate;
        [FieldOffset(8)]
        public float DeltaTimeSeconds;
        [FieldOffset(12)]
        public float FeedRate;
        [FieldOffset(16)]
        public float PredatorConversion;
        [FieldOffset(20)]
        public float PreyCarryingCapacity;
        [FieldOffset(24)]
        public float StablePredatorBiomass;
        [FieldOffset(28)]
        public float StablePreyBiomass;
        [FieldOffset(32)]
        public float ObservedPredatorMax;
        [FieldOffset(36)]
        public float ObservedPreyMax;
        [FieldOffset(40)]
        public int IntegrationSteps;
        [FieldOffset(44)]
        public uint Flags;
        [FieldOffset(48)]
        public uint Reserved;
        [FieldOffset(52)]
        public uint Reserved1;
        [FieldOffset(56)]
        public uint Reserved2;
        [FieldOffset(60)]
        public uint Reserved3;

        public static EcosystemPopulationCoefficient CreateDefault()
        {
            return new EcosystemPopulationCoefficient
            {
                BirthRate = HectonEcologyContract.LotkaBirthRate,
                DeathRate = HectonEcologyContract.LotkaDeathRate,
                DeltaTimeSeconds = HectonEcologyContract.LotkaDeltaTimeSeconds,
                FeedRate = HectonEcologyContract.LotkaFeedRate,
                PredatorConversion = HectonEcologyContract.LotkaPredatorConversion,
                PreyCarryingCapacity = HectonEcologyContract.LotkaPreyCarryingCapacity,
                StablePredatorBiomass = HectonEcologyContract.LotkaStablePredatorBiomass,
                StablePreyBiomass = HectonEcologyContract.LotkaStablePreyBiomass,
                ObservedPredatorMax = HectonEcologyContract.LotkaObservedPredatorMax,
                ObservedPreyMax = HectonEcologyContract.LotkaObservedPreyMax,
                IntegrationSteps = HectonEcologyContract.LotkaIntegrationSteps
            };
        }

        public static EcosystemPopulationCoefficient FromJson(in EcosystemCoefficientJson json)
        {
            return new EcosystemPopulationCoefficient
            {
                BirthRate = json.BirthRate,
                DeathRate = json.DeathRate,
                DeltaTimeSeconds = json.DeltaTimeSeconds,
                FeedRate = json.FeedRate,
                PredatorConversion = json.PredatorConversion,
                PreyCarryingCapacity = json.PreyCarryingCapacity,
                StablePredatorBiomass = json.StablePredatorBiomass,
                StablePreyBiomass = json.StablePreyBiomass,
                ObservedPredatorMax = json.ObservedPredatorMax,
                ObservedPreyMax = json.ObservedPreyMax,
                IntegrationSteps = json.IntegrationSteps
            };
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 112)]
    internal struct EcosystemPopulationSectorState
    {
        [FieldOffset(0)]
        public AbsoluteUniversePosition SampleAup;
        [FieldOffset(48)]
        public long SectorHash;
        [FieldOffset(56)]
        public float PreyBiomass;
        [FieldOffset(60)]
        public float PredatorBiomass;
        [FieldOffset(64)]
        public float MaxCapacity;
        [FieldOffset(68)]
        public int ActivePreyCount;
        [FieldOffset(72)]
        public int ActivePredatorCount;
        [FieldOffset(76)]
        public int FreePreyCount;
        [FieldOffset(80)]
        public int DesiredPreyCount;
        [FieldOffset(84)]
        public int LastCulled;
        [FieldOffset(88)]
        public int LastSpawned;
        [FieldOffset(92)]
        public int LastFleeDown;
        [FieldOffset(96)]
        public uint Flags;
        [FieldOffset(100)]
        public uint Reserved0;
        [FieldOffset(104)]
        public uint Reserved1;
        [FieldOffset(108)]
        public uint Reserved2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    internal struct EcosystemPopulationCullEvent
    {
        [FieldOffset(0)]
        public AbsoluteUniversePosition PositionAup;
        [FieldOffset(48)]
        public long SectorHash;
        [FieldOffset(56)]
        public uint EntityHash;
        [FieldOffset(60)]
        public int EntityIndex;
        [FieldOffset(64)]
        public float Intensity01;
        [FieldOffset(68)]
        public uint Flags;
        [FieldOffset(72)]
        public uint Reserved0;
        [FieldOffset(76)]
        public uint Reserved1;
        [FieldOffset(80)]
        public uint Reserved2;
        [FieldOffset(84)]
        public uint Reserved3;
        [FieldOffset(88)]
        public uint Reserved4;
        [FieldOffset(92)]
        public uint Reserved5;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct EcosystemPopulationFreeSlot
    {
        [FieldOffset(0)]
        public long SectorHash;
        [FieldOffset(8)]
        public int EntityIndex;
        [FieldOffset(12)]
        public uint Frame;
        [FieldOffset(16)]
        public uint Flags;
        [FieldOffset(20)]
        public uint Reserved;
        [FieldOffset(24)]
        public uint Reserved1;
        [FieldOffset(28)]
        public uint Reserved2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct EcosystemPopulationTelemetryEntry
    {
        [FieldOffset(0)]
        public uint Frame;
        [FieldOffset(4)]
        public uint StateHash;
        [FieldOffset(8)]
        public int TotalActiveEntities;
        [FieldOffset(12)]
        public int CulledByEcology;
        [FieldOffset(16)]
        public int SpawnedByEcology;
        [FieldOffset(20)]
        public int FleeDownRequests;
        [FieldOffset(24)]
        public int SectorCount;
        [FieldOffset(28)]
        public int FreeRingCount;
        [FieldOffset(32)]
        public float SystemStress01;
        [FieldOffset(36)]
        public uint Flags;
        [FieldOffset(40)]
        public uint Reserved0;
        [FieldOffset(44)]
        public uint Reserved1;
        [FieldOffset(48)]
        public uint Reserved2;
        [FieldOffset(52)]
        public uint Reserved3;
        [FieldOffset(56)]
        public uint Reserved4;
        [FieldOffset(60)]
        public uint Reserved5;
    }

    internal static class EcosystemPopulationLayoutManifest
    {
        private const string LayoutSizeMismatchMessage = "[EcosystemPopulationLayoutManifest] Size mismatch";
        private const string LayoutOffsetMismatchMessage = "[EcosystemPopulationLayoutManifest] Offset mismatch";

        private static bool _verified;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _verified = false;
        }

        public static void VerifyColdBoot()
        {
            if (_verified)
                return;

            AssertSize<EcosystemPopulationCoefficient>(64);
            AssertSize<EcosystemPopulationSectorState>(112);
            AssertSize<EcosystemPopulationCullEvent>(96);
            AssertSize<EcosystemPopulationFreeSlot>(32);
            AssertSize<EcosystemPopulationTelemetryEntry>(64);
            AssertOffset<EcosystemPopulationSectorState>(nameof(EcosystemPopulationSectorState.SampleAup), 0);
            AssertOffset<EcosystemPopulationSectorState>(nameof(EcosystemPopulationSectorState.SectorHash), 48);
            AssertOffset<EcosystemPopulationSectorState>(nameof(EcosystemPopulationSectorState.PreyBiomass), 56);
            AssertOffset<EcosystemPopulationSectorState>(nameof(EcosystemPopulationSectorState.Flags), 96);
            AssertOffset<EcosystemPopulationCullEvent>(nameof(EcosystemPopulationCullEvent.PositionAup), 0);
            AssertOffset<EcosystemPopulationCullEvent>(nameof(EcosystemPopulationCullEvent.SectorHash), 48);
            AssertOffset<EcosystemPopulationCullEvent>(nameof(EcosystemPopulationCullEvent.EntityHash), 56);
            AssertOffset<EcosystemPopulationCullEvent>(nameof(EcosystemPopulationCullEvent.Flags), 68);
            AssertOffset<EcosystemPopulationFreeSlot>(nameof(EcosystemPopulationFreeSlot.SectorHash), 0);
            AssertOffset<EcosystemPopulationFreeSlot>(nameof(EcosystemPopulationFreeSlot.EntityIndex), 8);
            AssertOffset<EcosystemPopulationFreeSlot>(nameof(EcosystemPopulationFreeSlot.Flags), 16);
            AssertOffset<EcosystemPopulationTelemetryEntry>(nameof(EcosystemPopulationTelemetryEntry.Frame), 0);
            AssertOffset<EcosystemPopulationTelemetryEntry>(nameof(EcosystemPopulationTelemetryEntry.Flags), 36);
            _verified = true;
        }

        private static void AssertSize<T>(int expected)
            where T : struct
        {
            int observed = UnsafeUtility.SizeOf<T>();
            if (observed != expected)
                throw new CriticalBootException(LayoutSizeMismatchMessage);
        }

        private static void AssertOffset<T>(string fieldName, int expected)
            where T : struct
        {
            int observed = (int)Marshal.OffsetOf<T>(fieldName);
            if (observed != expected)
                throw new CriticalBootException(LayoutOffsetMismatchMessage);
        }
    }

    internal static class EcosystemPopulationMath
    {
        private const double SectorSizeMeters = 1000d;
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private const ulong Fnv64Offset = 14695981039346656037UL;
        private const ulong Fnv64Prime = 1099511628211UL;
        private const double LongMinAsDouble = -9223372036854775808d;
        private const double LongMaxAsDouble = 9223372036854775807d;

        public static EcosystemPopulationCoefficient SanitizeCoefficient(in EcosystemPopulationCoefficient input)
        {
            EcosystemPopulationCoefficient output = input;
            if (!math.isfinite(output.BirthRate) || output.BirthRate < 0f)
                output.BirthRate = HectonEcologyContract.LotkaBirthRate;
            if (!math.isfinite(output.DeathRate) || output.DeathRate < 0f)
                output.DeathRate = HectonEcologyContract.LotkaDeathRate;
            if (!math.isfinite(output.DeltaTimeSeconds) || output.DeltaTimeSeconds <= 0f)
                output.DeltaTimeSeconds = HectonEcologyContract.LotkaDeltaTimeSeconds;
            if (!math.isfinite(output.FeedRate) || output.FeedRate < 0f)
                output.FeedRate = HectonEcologyContract.LotkaFeedRate;
            if (!math.isfinite(output.PredatorConversion) || output.PredatorConversion < 0f)
                output.PredatorConversion = HectonEcologyContract.LotkaPredatorConversion;
            if (!math.isfinite(output.PreyCarryingCapacity) || output.PreyCarryingCapacity <= 0f)
                output.PreyCarryingCapacity = HectonEcologyContract.LotkaPreyCarryingCapacity;

            output.BirthRate = math.clamp(output.BirthRate, 0f, 1f);
            output.DeathRate = math.clamp(output.DeathRate, 0f, 1f);
            output.FeedRate = math.clamp(output.FeedRate, 0f, 1f);
            output.PredatorConversion = math.clamp(output.PredatorConversion, 0f, 4f);
            output.PreyCarryingCapacity = math.clamp(output.PreyCarryingCapacity, 1f, 1000000f);
            return output;
        }

        public static EcosystemPopulationSectorState SanitizeSectorState(in EcosystemPopulationSectorState input, float defaultCapacity)
        {
            EcosystemPopulationSectorState output = input;
            float capacity = math.max(1f, math.isfinite(output.MaxCapacity) ? output.MaxCapacity : defaultCapacity);
            output.MaxCapacity = capacity;
            output.PreyBiomass = math.select(0f, math.clamp(output.PreyBiomass, 0f, capacity), math.isfinite(output.PreyBiomass));
            output.PredatorBiomass = math.select(0f, math.clamp(output.PredatorBiomass, 0f, capacity), math.isfinite(output.PredatorBiomass));
            output.ActivePreyCount = math.max(0, output.ActivePreyCount);
            output.ActivePredatorCount = math.max(0, output.ActivePredatorCount);
            output.FreePreyCount = math.max(0, output.FreePreyCount);
            return output;
        }

        public static bool IsFiniteAup(in AbsoluteUniversePosition aup)
        {
            return math.isfinite(aup.LocalX) &&
                   math.isfinite(aup.LocalY) &&
                   math.isfinite(aup.LocalZ) &&
                   aup.GridX > long.MinValue / 2 &&
                   aup.GridY > long.MinValue / 2 &&
                   aup.GridZ > long.MinValue / 2;
        }

        public static long ResolveSectorHash(in AbsoluteUniversePosition aup)
        {
            double absoluteX = (aup.GridX * (double)AbsoluteUniversePosition.CellSizeMeters) + aup.LocalX;
            double absoluteZ = (aup.GridZ * (double)AbsoluteUniversePosition.CellSizeMeters) + aup.LocalZ;
            long sectorX = FloorToLongSaturated(absoluteX / SectorSizeMeters);
            long sectorZ = FloorToLongSaturated(absoluteZ / SectorSizeMeters);
            ulong hash = Fnv64Offset;
            hash = HashUInt64(hash, unchecked((ulong)sectorX));
            hash = HashUInt64(hash, unchecked((ulong)sectorZ));
            return unchecked((long)(hash != 0UL ? hash : 1UL));
        }

        private static long FloorToLongSaturated(double value)
        {
            if (!math.isfinite(value))
                return 0L;

            double floored = math.floor(value);
            if (floored <= LongMinAsDouble)
                return long.MinValue;
            if (floored >= LongMaxAsDouble)
                return long.MaxValue;
            return (long)floored;
        }

        private static ulong HashUInt64(ulong hash, ulong value)
        {
            hash = (hash ^ (uint)value) * Fnv64Prime;
            hash = (hash ^ (uint)(value >> 32)) * Fnv64Prime;
            return hash;
        }

        public static uint ResolveEntityHash(int entityIndex, long sectorHash)
        {
            uint hash = FnvOffset;
            hash = (hash ^ (uint)entityIndex) * FnvPrime;
            hash = (hash ^ (uint)sectorHash) * FnvPrime;
            hash = (hash ^ (uint)((ulong)sectorHash >> 32)) * FnvPrime;
            return hash != 0u ? hash : 1u;
        }

        public static uint MixTelemetryHash(int active, int culled, int spawned, int sectorCount)
        {
            uint hash = FnvOffset;
            hash = (hash ^ (uint)active) * FnvPrime;
            hash = (hash ^ (uint)culled) * FnvPrime;
            hash = (hash ^ (uint)spawned) * FnvPrime;
            hash = (hash ^ (uint)sectorCount) * FnvPrime;
            return hash != 0u ? hash : 1u;
        }
    }
}
