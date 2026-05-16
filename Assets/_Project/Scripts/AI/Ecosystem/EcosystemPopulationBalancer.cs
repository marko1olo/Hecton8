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
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AI.Ecosystem
{
    /// <summary>
    /// Data-only ecology population governor. Storage is DataVault-owned; this component only schedules and publishes.
    /// </summary>
    public sealed class EcosystemPopulationBalancer : MonoBehaviour, IColdTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
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
        private const float StressCullThreshold01 = HectonEcologyContract.StressCullThreshold01;
        private const float DefaultStressCullFraction01 = HectonEcologyContract.DefaultStressCullFraction01;
        private const uint EcologySourceHash = 0x45434F4Cu; // ECOL
        private const byte EcologyDeathSignalFlag = 1;
        private const uint TelemetryInvalidMathFlag = 1u << 0;
        private const uint TelemetryFallbackCoefficientsFlag = 1u << 1;
        private const uint TelemetryVaultMissingFlag = 1u << 2;
        private const uint TelemetryDirectorMissingFlag = 1u << 3;
        private const uint TelemetryCullEventOverflowFlag = 1u << 4;
        private const uint TelemetryFreeRingOverflowFlag = 1u << 5;
        private const uint BlackBoxDumpIoFaultHash = 0x444D5046u; // DMPF
        private const uint BlackBoxMissingTelemetryHash = 0x444D504Du; // DMPM
        private const ulong DumpMagic = 0x504F504543544548UL;
        private const int DumpFormatVersion = 2;
        private const string CoefficientsRelativePath = "Data/Precomputed/ecosystem_coefficients.json";
        private const string LegacyCoefficientsRelativePath = "Data/Precomputed/Ecosystem_Coefficients.json";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_ECOSYSTEM_POPULATION_BALANCER.bin";

        [SerializeField, Min(1)] private int maxEntities = DefaultMaxEntities;
        [SerializeField, Min(1)] private int maxSectors = DefaultMaxSectors;
        [SerializeField, Min(1)] private int cullEventCapacity = DefaultCullEventCapacity;
        [SerializeField, Min(1)] private int freeRingCapacity = DefaultFreeRingCapacity;
        [SerializeField, Min(1f)] private float biomassPerEntity = DefaultBiomassPerEntity;
        [SerializeField, Min(1)] private int maxActivePreyPerSector = DefaultMaxActivePreyPerSector;
        [SerializeField, Range(0f, 1f)] private float stressCullFraction01 = DefaultStressCullFraction01;
        [SerializeField] private bool enableTier1FleeDown = true;

        private VaultBufferHandle<EcosystemPopulationCoefficient> _coefficientHandle;
        private VaultBufferHandle<EcosystemPopulationSectorState> _sectorStateHandle;
        private VaultBufferHandle<EcosystemPopulationCullEvent> _cullEventHandle;
        private VaultBufferHandle<EcosystemPopulationTelemetryEntry> _telemetryHandle;
        private VaultBufferHandle<EcosystemPopulationFreeSlot> _freeRingHandle;
        private VaultBufferHandle<int> _counterHandle;
        private VaultBufferHandle<AbsoluteUniversePosition> _entityAupHandle;
        private VaultBufferHandle<uint> _entityFlagHandle;
        private IDataVault _dataVault;
        private IEcosystemDirectorService _ecosystemDirector;
        private JobHandle _balancerHandle;
        private int _sectorCount;
        private int _telemetryCursor;
        private bool _coefficientsLoaded;
        private bool _registeredColdTick;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _jobScheduled;
        private bool _dumpedFault;
        private uint _runtimeFlags;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            TryRegisterHotSwapListener();
            if (!EnsureVaultState())
                return;

            TryRegisterTicks();
        }

        private void OnDisable()
        {
            CompleteScheduledJob(forceComplete: true);
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
                CompleteScheduledJob(forceComplete: true);
                _dataVault = currentService as IDataVault;
                ResetVaultHandles();
                _coefficientsLoaded = false;
                _sectorCount = 0;
                _telemetryCursor = 0;
                _dumpedFault = false;

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

            if (!EnsureVaultState())
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            if (!TryBuildSectorState(vault, out int entityCount, out int totalActiveEntities))
                return;

            if (_sectorCount <= 0 || entityCount <= 0)
            {
                RecordEmptyTelemetry(vault, totalActiveEntities);
                return;
            }

            ScheduleBalancerJob(vault, entityCount, totalActiveEntities);
        }

        public void LateFrameTick()
        {
            if (!_jobScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _balancerHandle, forceComplete: false))
                return;

            _jobScheduled = false;
            PublishCompletedCullSignals();
        }

        private bool EnsureVaultState()
        {
            IDataVault vault = ResolveDataVaultDependency();
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
            stressCullFraction01 = math.saturate(stressCullFraction01);

            EnsureEntityHandles(vault);

            _coefficientHandle = vault.GetBufferHandle<EcosystemPopulationCoefficient>(
                BufferID.EcosystemPopulationCoefficients,
                CoefficientCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);
            _sectorStateHandle = vault.GetBufferHandle<EcosystemPopulationSectorState>(
                BufferID.EcosystemPopulationSectorState,
                maxSectors,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);
            _cullEventHandle = vault.GetBufferHandle<EcosystemPopulationCullEvent>(
                BufferID.EcosystemPopulationCullEvents,
                cullEventCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);
            _telemetryHandle = vault.GetBufferHandle<EcosystemPopulationTelemetryEntry>(
                BufferID.EcosystemPopulationTelemetryRing,
                TelemetryCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);
            _freeRingHandle = vault.GetBufferHandle<EcosystemPopulationFreeSlot>(
                BufferID.EcosystemPopulationFreeRing,
                freeRingCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);
            _counterHandle = vault.GetBufferHandle<int>(
                BufferID.EcosystemPopulationCounters,
                CounterCapacity,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory);

            if (!_coefficientHandle.IsCreated ||
                !_sectorStateHandle.IsCreated ||
                !_cullEventHandle.IsCreated ||
                !_telemetryHandle.IsCreated ||
                !_freeRingHandle.IsCreated ||
                !_counterHandle.IsCreated ||
                !_entityAupHandle.IsCreated ||
                !_entityFlagHandle.IsCreated)
            {
                _runtimeFlags |= TelemetryVaultMissingFlag;
                return false;
            }

            if (!_coefficientsLoaded)
                LoadCoefficientsIntoVault(vault);

            ResolveDirectorDependency();
            return _coefficientsLoaded;
        }

        private IDataVault ResolveDataVaultDependency()
        {
            IDataVault vault = _dataVault;
            if (vault != null)
                return vault;

            vault = GlobalRegistry.DataVault;
            _dataVault = vault;
            return vault;
        }

        private IEcosystemDirectorService ResolveDirectorDependency()
        {
            IEcosystemDirectorService director = _ecosystemDirector;
            if (director != null && director.IsInitialized)
                return director;

            director = GlobalRegistry.EcosystemDirector;
            _ecosystemDirector = director;
            return director;
        }

        private void EnsureEntityHandles(IDataVault vault)
        {
            bool hasAups = vault.TryGetBuffer<AbsoluteUniversePosition>(BufferID.EntityAUPs, out var entityAups) &&
                           entityAups.IsCreated &&
                           entityAups.Length > 0;
            bool hasFlags = vault.TryGetBuffer<uint>(BufferID.EntityFlags, out var entityFlags) &&
                            entityFlags.IsCreated &&
                            entityFlags.Length > 0;

            int requestedEntityCount = maxEntities;
            if (hasAups)
                requestedEntityCount = math.min(requestedEntityCount, entityAups.Length);
            if (hasFlags)
                requestedEntityCount = math.min(requestedEntityCount, entityFlags.Length);
            requestedEntityCount = math.max(1, requestedEntityCount);

            if (!hasAups)
            {
                entityAups = vault.GetBuffer<AbsoluteUniversePosition>(
                    BufferID.EntityAUPs,
                    requestedEntityCount,
                    SystemID.AIEcology,
                    NativeArrayOptions.ClearMemory);
            }

            if (!hasFlags)
            {
                entityFlags = vault.GetBuffer<uint>(
                    BufferID.EntityFlags,
                    requestedEntityCount,
                    SystemID.AIEcology,
                    NativeArrayOptions.ClearMemory);
            }

            vault.TryGetBufferHandle(BufferID.EntityAUPs, out _entityAupHandle);
            vault.TryGetBufferHandle(BufferID.EntityFlags, out _entityFlagHandle);
        }

        private void LoadCoefficientsIntoVault(IDataVault vault)
        {
            var coefficients = _coefficientHandle.Resolve(vault);
            if (!coefficients.IsCreated || coefficients.Length <= 0)
                return;

            EcosystemPopulationCoefficient coefficient = EcosystemPopulationCoefficient.Default;
            if (TryReadCoefficientJson(out EcosystemCoefficientJson json))
            {
                coefficient = EcosystemPopulationCoefficient.FromJson(in json);
                _runtimeFlags &= ~TelemetryFallbackCoefficientsFlag;
            }
            else
            {
                _runtimeFlags |= TelemetryFallbackCoefficientsFlag;
            }

            coefficient = EcosystemPopulationMath.SanitizeCoefficient(in coefficient);
            coefficients[0] = coefficient;
            _coefficientsLoaded = true;
        }

        private static bool TryReadCoefficientJson(out EcosystemCoefficientJson coefficient)
        {
            coefficient = default;
#if UNITY_EDITOR
            try
            {
                string path = ResolveProjectRelativePath(CoefficientsRelativePath);
                if (!File.Exists(path))
                    path = ResolveProjectRelativePath(LegacyCoefficientsRelativePath);
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

                if (string.IsNullOrWhiteSpace(json))
                    return false;

                coefficient = JsonUtility.FromJson<EcosystemCoefficientJson>(json);
            }
            catch (Exception)
            {
                coefficient = default;
                return false;
            }

            return coefficient.PreyCarryingCapacity > 0f;
#else
            return false;
#endif
        }

        private bool TryBuildSectorState(IDataVault vault, out int entityCount, out int totalActiveEntities)
        {
            entityCount = 0;
            totalActiveEntities = 0;
            var entityAups = _entityAupHandle.Resolve(vault);
            var entityFlags = _entityFlagHandle.Resolve(vault);
            var sectorStates = _sectorStateHandle.Resolve(vault);
            var coefficients = _coefficientHandle.Resolve(vault);
            var freeRing = _freeRingHandle.Resolve(vault);
            var counters = _counterHandle.Resolve(vault);
            if (!entityAups.IsCreated ||
                !entityFlags.IsCreated ||
                !sectorStates.IsCreated ||
                !coefficients.IsCreated ||
                !freeRing.IsCreated ||
                !counters.IsCreated)
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
            uint frame = unchecked((uint)Time.frameCount);
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
                int sectorIndex = ResolveOrCreateSectorSlot(sectorStates, sectorCapacity, ref sectorCount, sectorHash, in aup);
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

        private void ScheduleBalancerJob(IDataVault vault, int entityCount, int totalActiveEntities)
        {
            var coefficients = _coefficientHandle.Resolve(vault);
            var sectorStates = _sectorStateHandle.Resolve(vault);
            var cullEvents = _cullEventHandle.Resolve(vault);
            var telemetry = _telemetryHandle.Resolve(vault);
            var freeRing = _freeRingHandle.Resolve(vault);
            var counters = _counterHandle.Resolve(vault);
            var entityAups = _entityAupHandle.Resolve(vault);
            var entityFlags = _entityFlagHandle.Resolve(vault);
            if (!coefficients.IsCreated ||
                !sectorStates.IsCreated ||
                !cullEvents.IsCreated ||
                !telemetry.IsCreated ||
                !freeRing.IsCreated ||
                !counters.IsCreated ||
                !entityAups.IsCreated ||
                !entityFlags.IsCreated)
            {
                return;
            }

            int telemetryIndex = _telemetryCursor % math.max(1, telemetry.Length);
            _telemetryCursor++;
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
                Frame = unchecked((uint)Time.frameCount),
                DeltaSeconds = ColdTickDeltaSeconds,
                SystemStress01 = SignalBusRegistry.SystemStress01,
                BiomassPerEntity = math.max(1f, biomassPerEntity),
                MaxActivePreyPerSector = math.max(1, maxActivePreyPerSector),
                StressCullFraction01 = math.saturate(stressCullFraction01),
                EnableTier1FleeDown = enableTier1FleeDown ? 1 : 0,
                RuntimeFlags = _runtimeFlags
            };

            _balancerHandle = job.Schedule();
            _jobScheduled = true;
        }

        private void RecordEmptyTelemetry(IDataVault vault, int totalActiveEntities)
        {
            var telemetry = _telemetryHandle.Resolve(vault);
            var counters = _counterHandle.Resolve(vault);
            if (!telemetry.IsCreated || telemetry.Length <= 0 || !counters.IsCreated)
                return;

            int telemetryIndex = _telemetryCursor % telemetry.Length;
            _telemetryCursor++;
            int freeRingCount = counters.Length > EcosystemPopulationCounters.FreeRingCount
                ? math.max(0, counters[EcosystemPopulationCounters.FreeRingCount])
                : 0;
            float systemStress01 = math.saturate(SignalBusRegistry.SystemStress01);
            telemetry[telemetryIndex] = new EcosystemPopulationTelemetryEntry
            {
                Frame = unchecked((uint)Time.frameCount),
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

        private void PublishCompletedCullSignals()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            var cullEvents = _cullEventHandle.Resolve(vault);
            var counters = _counterHandle.Resolve(vault);
            var telemetry = _telemetryHandle.Resolve(vault);
            if (!cullEvents.IsCreated || !counters.IsCreated)
                return;

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
                SignalBus<EntityDeathSignal>.Push(in signal);
            }

            bool invalidMath = counters.Length > EcosystemPopulationCounters.InvalidMathRecovered &&
                               counters[EcosystemPopulationCounters.InvalidMathRecovered] != 0;
            if (invalidMath && !_dumpedFault)
            {
                _dumpedFault = true;
                DumpBlackBox(telemetry, _telemetryCursor);
            }
        }

        private void CompleteScheduledJob(bool forceComplete)
        {
            if (!_jobScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _balancerHandle, forceComplete))
                return;

            _jobScheduled = false;
            if (forceComplete)
                PublishCompletedCullSignals();
        }

        private void TryRegisterTicks()
        {
            if (!_registeredColdTick)
                _registeredColdTick = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            if (!_registeredColdTick || !_registeredLateFrame)
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

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void ClearCachedDependencies()
        {
            _dataVault = null;
            _ecosystemDirector = null;
            _coefficientsLoaded = false;
            _sectorCount = 0;
            _telemetryCursor = 0;
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

        private static int ResolveOrCreateSectorSlot(
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
            float3 runtime = aup.ToRuntimeFloat3();
            bool finite = math.all(math.isfinite(runtime));
            runtimePosition = finite ? new Vector3(runtime.x, runtime.y, runtime.z) : default;
            return finite;
        }

        private static string ResolveProjectRelativePath(string relativePath)
        {
            string assetsPath = Application.dataPath;
            string root = Directory.GetParent(assetsPath) != null
                ? Directory.GetParent(assetsPath).FullName
                : assetsPath;
            return Path.Combine(root, relativePath);
        }

        private static void DumpBlackBox(NativeArray<EcosystemPopulationTelemetryEntry> telemetry, int telemetryCursor)
        {
            if (!telemetry.IsCreated)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(BlackBoxMissingTelemetryHash, EcologySourceHash, 0f);
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)EcologySourceHash));
                return;
            }

            try
            {
                string path = ResolveProjectRelativePath(DumpRelativePath);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    int capacity = telemetry.Length;
                    int writtenCount = math.max(0, telemetryCursor);
                    int dumpCount = math.min(writtenCount, capacity);
                    int startIndex = writtenCount < capacity ? 0 : PositiveModulo(telemetryCursor, capacity);
                    writer.Write(DumpMagic);
                    writer.Write(DumpFormatVersion);
                    writer.Write(capacity);
                    writer.Write(dumpCount);
                    writer.Write(telemetryCursor);
                    writer.Write(startIndex);
                    for (int offset = 0; offset < dumpCount; offset++)
                    {
                        int index = startIndex + offset;
                        if (index >= capacity)
                            index -= capacity;
                        EcosystemPopulationTelemetryEntry entry = telemetry[index];
                        writer.Write(entry.Frame);
                        writer.Write(entry.StateHash);
                        writer.Write(entry.TotalActiveEntities);
                        writer.Write(entry.CulledByEcology);
                        writer.Write(entry.SpawnedByEcology);
                        writer.Write(entry.FleeDownRequests);
                        writer.Write(entry.SectorCount);
                        writer.Write(entry.FreeRingCount);
                        writer.Write(entry.SystemStress01);
                        writer.Write(entry.Flags);
                    }
                }
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(BlackBoxDumpIoFaultHash, EcologySourceHash, telemetry.Length);
            }

            GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)EcologySourceHash));
        }

        private static int PositiveModulo(int value, int length)
        {
            if (length <= 0)
                return 0;

            int result = value % length;
            return result < 0 ? result + length : result;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct EcosystemBalancerJob : IJob
        {
            [ReadOnly] public NativeArray<EcosystemPopulationCoefficient> Coefficients;
            public NativeArray<EcosystemPopulationSectorState> SectorStates;
            [ReadOnly] public NativeArray<AbsoluteUniversePosition> EntityAups;
            public NativeArray<uint> EntityFlags;
            public NativeArray<EcosystemPopulationCullEvent> CullEvents;
            public NativeArray<EcosystemPopulationFreeSlot> FreeRing;
            public NativeArray<EcosystemPopulationTelemetryEntry> TelemetryRing;
            public NativeArray<int> Counters;
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
            public float StressCullFraction01;
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
                    : EcosystemPopulationCoefficient.Default;

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
                    bool stressActive = SystemStress01 > StressCullThreshold01;
                    int stressCullTarget = stressActive && currentPrey + state.ActivePredatorCount > 0
                        ? math.max(1, (int)math.ceil((currentPrey + state.ActivePredatorCount) * math.saturate(StressCullFraction01)))
                        : 0;

                    int preyCull = CullTier2EntitiesInSector(
                        state.SectorHash,
                        cullNeeded,
                        entityCount,
                        EcosystemPopulationFlags.Flag_IsPrey,
                        requireAnyEcologyKind: false,
                        allowFreeListForNonPrey: false,
                        freeWriteCursor: ref freeWriteCursor,
                        freeCount: ref freeCount,
                        culledTotal: ref culled,
                        eventLimit: eventLimit,
                        eventOverflow: ref eventOverflow);
                    int stressCull = stressCullTarget > preyCull
                        ? CullTier2EntitiesInSector(
                            state.SectorHash,
                            stressCullTarget - preyCull,
                            entityCount,
                            requiredKindMask: 0u,
                            requireAnyEcologyKind: true,
                            allowFreeListForNonPrey: true,
                            freeWriteCursor: ref freeWriteCursor,
                            freeCount: ref freeCount,
                            culledTotal: ref culled,
                            eventLimit: eventLimit,
                            eventOverflow: ref eventOverflow)
                        : 0;
                    int sectorCull = preyCull + stressCull;
                    int remainingCull = cullNeeded - preyCull;
                    int sectorFleeDown = remainingCull > 0 && EnableTier1FleeDown != 0
                        ? RequestTier1FleeDownInSector(state.SectorHash, remainingCull, entityCount)
                        : 0;
                    fleeDown += sectorFleeDown;

                    int spawnNeeded = math.max(0, desiredPrey - currentPrey);
                    int sectorSpawned = spawnNeeded > 0
                        ? ReactivateFreePreyInSector(state.SectorHash, spawnNeeded, entityCount, ref freeCount)
                        : 0;
                    spawned += sectorSpawned;

                    state.PreyBiomass = nextPrey;
                    state.PredatorBiomass = nextPredator;
                    state.DesiredPreyCount = desiredPrey;
                    state.LastCulled = sectorCull;
                    state.LastSpawned = sectorSpawned;
                    state.LastFleeDown = sectorFleeDown;
                    state.Flags = (invalidMath != 0 ? TelemetryInvalidMathFlag : 0u) |
                                  (eventOverflow != 0 ? TelemetryCullEventOverflowFlag : 0u);
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
                             (eventOverflow != 0 ? TelemetryCullEventOverflowFlag : 0u);
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
                bool allowFreeListForNonPrey,
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

                    EntityFlags[entityIndex] = (flags & ~EcosystemPopulationFlags.Flag_IsActive) |
                                               EcosystemPopulationFlags.Flag_CulledByEcology |
                                               EcosystemPopulationFlags.Flag_FreeList;
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

                    bool canEnterFreeRing = (flags & EcosystemPopulationFlags.Flag_IsPrey) != 0u || allowFreeListForNonPrey;
                    if (FreeRing.Length > 0 && canEnterFreeRing)
                    {
                        int ringIndex = freeWriteCursor;
                        if (ringIndex < 0 || ringIndex >= FreeRing.Length)
                            ringIndex = 0;
                        uint freeSlotFlags = EcosystemPopulationFreeSlotFlags.Valid;
                        if ((flags & EcosystemPopulationFlags.Flag_IsPrey) != 0u)
                            freeSlotFlags |= EcosystemPopulationFreeSlotFlags.Prey;
                        FreeRing[ringIndex] = new EcosystemPopulationFreeSlot
                        {
                            SectorHash = sectorHash,
                            EntityIndex = entityIndex,
                            Frame = Frame,
                            Flags = freeSlotFlags
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

            private int ReactivateFreePreyInSector(long sectorHash, int spawnNeeded, int entityCount, ref int freeCount)
            {
                int spawned = 0;
                for (int ringIndex = 0; ringIndex < FreeRing.Length && spawned < spawnNeeded; ringIndex++)
                {
                    EcosystemPopulationFreeSlot slot = FreeRing[ringIndex];
                    if ((slot.Flags & EcosystemPopulationFreeSlotFlags.Valid) == 0u ||
                        (slot.Flags & EcosystemPopulationFreeSlotFlags.Prey) == 0u ||
                        slot.SectorHash != sectorHash ||
                        slot.EntityIndex < 0 ||
                        slot.EntityIndex >= entityCount)
                    {
                        continue;
                    }

                    uint flags = EntityFlags[slot.EntityIndex];
                    if ((flags & EcosystemPopulationFlags.Flag_IsActive) != 0u)
                    {
                        FreeRing[ringIndex] = default;
                        freeCount = math.max(0, freeCount - 1);
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

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 52)]
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

        public static EcosystemPopulationCoefficient Default => new EcosystemPopulationCoefficient
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

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 112)]
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

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 88)]
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
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 24)]
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
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]
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
                output.BirthRate = EcosystemPopulationCoefficient.Default.BirthRate;
            if (!math.isfinite(output.DeathRate) || output.DeathRate < 0f)
                output.DeathRate = EcosystemPopulationCoefficient.Default.DeathRate;
            if (!math.isfinite(output.DeltaTimeSeconds) || output.DeltaTimeSeconds <= 0f)
                output.DeltaTimeSeconds = EcosystemPopulationCoefficient.Default.DeltaTimeSeconds;
            if (!math.isfinite(output.FeedRate) || output.FeedRate < 0f)
                output.FeedRate = EcosystemPopulationCoefficient.Default.FeedRate;
            if (!math.isfinite(output.PredatorConversion) || output.PredatorConversion < 0f)
                output.PredatorConversion = EcosystemPopulationCoefficient.Default.PredatorConversion;
            if (!math.isfinite(output.PreyCarryingCapacity) || output.PreyCarryingCapacity <= 0f)
                output.PreyCarryingCapacity = EcosystemPopulationCoefficient.Default.PreyCarryingCapacity;

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
