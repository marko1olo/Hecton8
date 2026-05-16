using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
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
    public sealed class EcosystemPopulationBalancer : MonoBehaviour, IColdTickable, ILateFrameTickable
    {
        private const int TelemetryCapacity = 300;
        private const int CounterCapacity = 16;
        private const int CoefficientCapacity = 1;
        private const int DefaultMaxEntities = 8192;
        private const int DefaultMaxSectors = 256;
        private const int DefaultCullEventCapacity = 256;
        private const int DefaultFreeRingCapacity = 1024;
        private const float ColdTickDeltaSeconds = 1f;
        private const float DefaultBiomassPerEntity = 128f;
        private const int DefaultMaxActivePreyPerSector = 64;
        private const float StressCullThreshold01 = 0.8f;
        private const float DefaultStressCullFraction01 = 0.25f;
        private const uint EcologySourceHash = 0x45434F4Cu; // ECOL
        private const byte EcologyDeathSignalFlag = 1;
        private const uint TelemetryInvalidMathFlag = 1u << 0;
        private const uint TelemetryFallbackCoefficientsFlag = 1u << 1;
        private const uint TelemetryVaultMissingFlag = 1u << 2;
        private const uint TelemetryDirectorMissingFlag = 1u << 3;
        private const ulong DumpMagic = 0x504F504543544548UL;
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
        private JobHandle _balancerHandle;
        private int _sectorCount;
        private int _telemetryCursor;
        private bool _coefficientsLoaded;
        private bool _registeredColdTick;
        private bool _registeredLateFrame;
        private bool _jobScheduled;
        private bool _dumpedFault;
        private uint _runtimeFlags;

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (!EnsureVaultState())
                return;

            TryRegisterTicks();
        }

        private void OnDisable()
        {
            CompleteScheduledJob(forceComplete: true);
            TryUnregisterTicks();
            _jobScheduled = false;
        }

        public void ColdTick()
        {
            if (_jobScheduled)
                return;

            if (!EnsureVaultState())
                return;

            IDataVault vault = GlobalRegistry.DataVault;
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
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                _runtimeFlags |= TelemetryVaultMissingFlag;
                return false;
            }

            maxEntities = math.max(1, maxEntities);
            maxSectors = math.max(1, maxSectors);
            cullEventCapacity = math.max(1, cullEventCapacity);
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

            return _coefficientsLoaded;
        }

        private void EnsureEntityHandles(IDataVault vault)
        {
            bool hasAups = vault.TryGetBuffer<AbsoluteUniversePosition>(BufferID.EntityAUPs, out NativeArray<AbsoluteUniversePosition> entityAups) &&
                           entityAups.IsCreated &&
                           entityAups.Length > 0;
            bool hasFlags = vault.TryGetBuffer<uint>(BufferID.EntityFlags, out NativeArray<uint> entityFlags) &&
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
            NativeArray<EcosystemPopulationCoefficient> coefficients = _coefficientHandle.Resolve(vault);
            if (!coefficients.IsCreated || coefficients.Length <= 0)
                return;

            EcosystemPopulationCoefficient coefficient = EcosystemPopulationCoefficient.Default;
            if (TryReadCoefficientJson(out EcosystemCoefficientJson json))
            {
                coefficient = EcosystemPopulationCoefficient.FromJson(in json);
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
            string path = ResolveProjectRelativePath(CoefficientsRelativePath);
            if (!File.Exists(path))
                path = ResolveProjectRelativePath(LegacyCoefficientsRelativePath);
            if (!File.Exists(path))
                return false;

            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return false;

            coefficient = JsonUtility.FromJson<EcosystemCoefficientJson>(json);
            return coefficient.PreyCarryingCapacity > 0f;
        }

        private bool TryBuildSectorState(IDataVault vault, out int entityCount, out int totalActiveEntities)
        {
            entityCount = 0;
            totalActiveEntities = 0;
            NativeArray<AbsoluteUniversePosition> entityAups = _entityAupHandle.Resolve(vault);
            NativeArray<uint> entityFlags = _entityFlagHandle.Resolve(vault);
            NativeArray<EcosystemPopulationSectorState> sectorStates = _sectorStateHandle.Resolve(vault);
            NativeArray<EcosystemPopulationCoefficient> coefficients = _coefficientHandle.Resolve(vault);
            NativeArray<int> counters = _counterHandle.Resolve(vault);
            if (!entityAups.IsCreated ||
                !entityFlags.IsCreated ||
                !sectorStates.IsCreated ||
                !coefficients.IsCreated ||
                !counters.IsCreated)
            {
                return false;
            }

            int sectorCapacity = math.min(maxSectors, sectorStates.Length);
            for (int i = 0; i < sectorCapacity; i++)
                sectorStates[i] = default;

            int counterCount = math.min(CounterCapacity, counters.Length);
            int retainedFreeCursor = counterCount > EcosystemPopulationCounters.FreeRingWriteCursor
                ? counters[EcosystemPopulationCounters.FreeRingWriteCursor]
                : 0;
            int retainedFreeCount = counterCount > EcosystemPopulationCounters.FreeRingCount
                ? counters[EcosystemPopulationCounters.FreeRingCount]
                : 0;
            for (int i = 0; i < counterCount; i++)
                counters[i] = 0;
            if (counterCount > EcosystemPopulationCounters.FreeRingWriteCursor)
                counters[EcosystemPopulationCounters.FreeRingWriteCursor] = math.max(0, retainedFreeCursor);
            if (counterCount > EcosystemPopulationCounters.FreeRingCount)
                counters[EcosystemPopulationCounters.FreeRingCount] = math.max(0, retainedFreeCount);

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
                        state.ActivePreyCount++;
                    else if ((flags & EcosystemPopulationFlags.Flag_FreeList) != 0u)
                        state.FreePreyCount++;
                }

                if ((flags & EcosystemPopulationFlags.Flag_IsPredator) != 0u &&
                    (flags & EcosystemPopulationFlags.Flag_IsActive) != 0u)
                {
                    state.ActivePredatorCount++;
                }

                sectorStates[sectorIndex] = state;
            }

            EcosystemPopulationCoefficient coefficient = EcosystemPopulationMath.SanitizeCoefficient(coefficients[0]);
            IEcosystemDirectorService director = GlobalRegistry.EcosystemDirector;
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

            return true;
        }

        private void ScheduleBalancerJob(IDataVault vault, int entityCount, int totalActiveEntities)
        {
            NativeArray<EcosystemPopulationCoefficient> coefficients = _coefficientHandle.Resolve(vault);
            NativeArray<EcosystemPopulationSectorState> sectorStates = _sectorStateHandle.Resolve(vault);
            NativeArray<EcosystemPopulationCullEvent> cullEvents = _cullEventHandle.Resolve(vault);
            NativeArray<EcosystemPopulationTelemetryEntry> telemetry = _telemetryHandle.Resolve(vault);
            NativeArray<EcosystemPopulationFreeSlot> freeRing = _freeRingHandle.Resolve(vault);
            NativeArray<int> counters = _counterHandle.Resolve(vault);
            NativeArray<AbsoluteUniversePosition> entityAups = _entityAupHandle.Resolve(vault);
            NativeArray<uint> entityFlags = _entityFlagHandle.Resolve(vault);
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
            NativeArray<EcosystemPopulationTelemetryEntry> telemetry = _telemetryHandle.Resolve(vault);
            NativeArray<int> counters = _counterHandle.Resolve(vault);
            if (!telemetry.IsCreated || telemetry.Length <= 0 || !counters.IsCreated)
                return;

            int telemetryIndex = _telemetryCursor % telemetry.Length;
            _telemetryCursor++;
            telemetry[telemetryIndex] = new EcosystemPopulationTelemetryEntry
            {
                Frame = unchecked((uint)Time.frameCount),
                TotalActiveEntities = totalActiveEntities,
                SectorCount = 0,
                Flags = _runtimeFlags,
                StateHash = EcosystemPopulationMath.MixTelemetryHash(totalActiveEntities, 0, 0, 0)
            };

            if (counters.Length > EcosystemPopulationCounters.TotalActiveEntities)
                counters[EcosystemPopulationCounters.TotalActiveEntities] = totalActiveEntities;
        }

        private void PublishCompletedCullSignals()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            NativeArray<EcosystemPopulationCullEvent> cullEvents = _cullEventHandle.Resolve(vault);
            NativeArray<int> counters = _counterHandle.Resolve(vault);
            NativeArray<EcosystemPopulationTelemetryEntry> telemetry = _telemetryHandle.Resolve(vault);
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
                DumpBlackBox(telemetry);
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

        private static void DumpBlackBox(NativeArray<EcosystemPopulationTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated)
                return;

            string path = ResolveProjectRelativePath(DumpRelativePath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(DumpMagic);
                writer.Write(telemetry.Length);
                for (int i = 0; i < telemetry.Length; i++)
                {
                    EcosystemPopulationTelemetryEntry entry = telemetry[i];
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

            GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)EcologySourceHash));
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
                int freeWriteCursor = Counters.Length > EcosystemPopulationCounters.FreeRingWriteCursor
                    ? math.max(0, Counters[EcosystemPopulationCounters.FreeRingWriteCursor])
                    : 0;
                int freeCount = Counters.Length > EcosystemPopulationCounters.FreeRingCount
                    ? math.clamp(Counters[EcosystemPopulationCounters.FreeRingCount], 0, FreeRing.Length)
                    : 0;

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
                        culledTotal: ref culled);
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
                            culledTotal: ref culled)
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
                    state.Flags = invalidMath != 0 ? TelemetryInvalidMathFlag : 0u;
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

                uint flags = RuntimeFlags | (invalidMath != 0 ? TelemetryInvalidMathFlag : 0u);
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
                ref int culledTotal)
            {
                if (cullNeeded <= 0)
                    return 0;

                int culled = 0;
                int eventCount = Counters.Length > EcosystemPopulationCounters.CullEventCount
                    ? math.clamp(Counters[EcosystemPopulationCounters.CullEventCount], 0, CullEvents.Length)
                    : 0;
                for (int entityIndex = 0; entityIndex < entityCount && culled < cullNeeded; entityIndex++)
                {
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
                    if (eventCount < CullEvents.Length)
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
                        int ringIndex = freeWriteCursor % FreeRing.Length;
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
                        freeWriteCursor++;
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
            BirthRate = 0.03f,
            DeathRate = 0.018f,
            DeltaTimeSeconds = 0.02f,
            FeedRate = 0.000006f,
            PredatorConversion = 0.35f,
            PreyCarryingCapacity = 10000f,
            StablePredatorBiomass = 714.2857f,
            StablePreyBiomass = 8571.4287f,
            ObservedPredatorMax = 714.2857f,
            ObservedPreyMax = 9020.165f,
            IntegrationSteps = 1000000
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
            int sectorX = (int)math.floor(absoluteX / SectorSizeMeters);
            int sectorZ = (int)math.floor(absoluteZ / SectorSizeMeters);
            ulong packed = ((ulong)(uint)sectorX << 32) | (uint)sectorZ;
            return unchecked((long)packed);
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
