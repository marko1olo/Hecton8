using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    internal struct EcosystemSectorSaveRecord
    {
        public int2 SectorCoord;
        public uint PackedPopulations;
        public uint PackedAdaptation;
    }

    /// <summary>
    /// Cold-path sector ecosystem simulator that evolves predator/prey counts with a Lotka-Volterra solve.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4037)]
    public sealed class EcosystemDirector : MonoBehaviour, ISlowTickable, IEcosystemDirectorService
    {
        private const float DefaultSlowTickIntervalSeconds = 0.5f;
        private const float SectorEdgeLengthMeters = 1000f;
        private const int MinimumSectorCapacity = 16;
        private const int MinimumPredationEventCapacity = 32;

        [StructLayout(LayoutKind.Sequential)]
        private struct SectorPopulationState
        {
            public int2 SectorCoord;
            public float PreyPopulation;
            public float PredatorPopulation;
            public float HarvestPressure;
            public float Fitness;
            public float SpeedMultiplier;
            public float CamouflageIndex;
            public int PreyPopulationRounded;
            public int PredatorPopulationRounded;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PredationEvent
        {
            public long SectorKey;
            public int PreyConsumed;
        }

        [BurstCompile]
        private struct LotkaVolterraSolveJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SectorPopulationState> FrontStates;
            [ReadOnly] public NativeHashMap<long, int> SectorIndexByKey;
            public NativeArray<SectorPopulationState> BackStates;
            public float DeltaTimeSeconds;
            public float Alpha;
            public float Beta;
            public float Delta;
            public float Gamma;
            public float PredatorGainPerPrey;
            public float HarvestPressureDecay;
            public float HarvestCrashRate;
            public float PreyMigrationRate;
            public float PredatorMigrationRate;
            public float PreyOverflowThreshold;
            public float PredatorOverflowThreshold;
            public float DepletedSectorBias;
            public float OverflowRedistributionRate;
            public float BaselineFitness;
            public float FitnessAdaptationRate;
            public float FitnessRecoveryRate;
            public float FitnessHarvestScale;
            public float MaximumSpeedMultiplier;
            public float MaximumCamouflageIndex;
            public int MaxPreyPopulation;
            public int MaxPredatorPopulation;

            public void Execute(int index)
            {
                SectorPopulationState state = FrontStates[index];
                float prey = math.max(0f, state.PreyPopulation);
                float predator = math.max(0f, state.PredatorPopulation);
                float harvestPressure = math.max(0f, state.HarvestPressure) * math.exp(-HarvestPressureDecay * DeltaTimeSeconds);
                float growthSuppression = math.saturate(1f - harvestPressure);
                growthSuppression *= growthSuppression;
                float harvestPressureSq = harvestPressure * harvestPressure;
                float interaction = prey * predator;
                float preyDelta = (Alpha * growthSuppression * prey) - (Beta * interaction) - (HarvestCrashRate * (harvestPressure + harvestPressureSq) * prey);
                float predatorDelta = (Delta * interaction * PredatorGainPerPrey) - (Gamma * predator);
                float preyMigration = ComputeNeighborMigration(index, prey, true, PreyOverflowThreshold);
                float predatorMigration = ComputeNeighborMigration(index, predator, false, PredatorOverflowThreshold);

                prey = math.clamp(prey + (preyDelta * DeltaTimeSeconds), 0f, MaxPreyPopulation);
                predator = math.clamp(predator + (predatorDelta * DeltaTimeSeconds), 0f, MaxPredatorPopulation);
                prey = math.clamp(prey + (preyMigration * PreyMigrationRate * DeltaTimeSeconds), 0f, MaxPreyPopulation);
                predator = math.clamp(predator + (predatorMigration * PredatorMigrationRate * DeltaTimeSeconds), 0f, MaxPredatorPopulation);

                float currentFitness = math.saturate(state.Fitness);
                float fitnessTarget = math.clamp(
                    BaselineFitness + math.saturate(harvestPressure * FitnessHarvestScale) * (1f - BaselineFitness),
                    0f,
                    1f);
                float adaptationRate = math.select(FitnessRecoveryRate, FitnessAdaptationRate, fitnessTarget > currentFitness);
                float fitnessBlend = 1f - math.exp(-math.max(0f, adaptationRate) * DeltaTimeSeconds);
                float fitness = math.lerp(currentFitness, fitnessTarget, fitnessBlend);
                float speedMultiplier = math.lerp(1f, MaximumSpeedMultiplier, fitness);
                float camouflageIndex = math.saturate(math.lerp(0f, MaximumCamouflageIndex, math.sqrt(fitness)));

                state.PreyPopulation = prey;
                state.PredatorPopulation = predator;
                state.HarvestPressure = harvestPressure;
                state.Fitness = fitness;
                state.SpeedMultiplier = speedMultiplier;
                state.CamouflageIndex = camouflageIndex;
                state.PreyPopulationRounded = (int)math.round(prey);
                state.PredatorPopulationRounded = (int)math.round(predator);
                BackStates[index] = state;
            }

            private float ComputeNeighborMigration(int index, float sectorPopulation, bool samplePrey, float overflowThreshold)
            {
                int2 sectorCoord = FrontStates[index].SectorCoord;
                float gradient = 0f;
                int lowerNeighborCount = 0;
                gradient += SampleNeighborDelta(sectorCoord + new int2(1, 0), sectorPopulation, samplePrey, ref lowerNeighborCount);
                gradient += SampleNeighborDelta(sectorCoord + new int2(-1, 0), sectorPopulation, samplePrey, ref lowerNeighborCount);
                gradient += SampleNeighborDelta(sectorCoord + new int2(0, 1), sectorPopulation, samplePrey, ref lowerNeighborCount);
                gradient += SampleNeighborDelta(sectorCoord + new int2(0, -1), sectorPopulation, samplePrey, ref lowerNeighborCount);

                float safeOverflowThreshold = math.max(1f, overflowThreshold);
                float depletion01 = math.saturate(1f - (sectorPopulation / safeOverflowThreshold));
                gradient *= 1f + (depletion01 * DepletedSectorBias);

                float overflow = math.max(0f, sectorPopulation - safeOverflowThreshold);
                if (overflow > 0f && lowerNeighborCount > 0)
                {
                    gradient -= overflow * OverflowRedistributionRate * (lowerNeighborCount * 0.25f);
                }

                return gradient;
            }

            private float SampleNeighborDelta(int2 neighborCoord, float sectorPopulation, bool samplePrey, ref int lowerNeighborCount)
            {
                if (!SectorIndexByKey.TryGetValue(PackSectorKey(neighborCoord), out int neighborIndex))
                    return 0f;

                SectorPopulationState neighbor = FrontStates[neighborIndex];
                float neighborPopulation = samplePrey ? neighbor.PreyPopulation : neighbor.PredatorPopulation;
                if (neighborPopulation < sectorPopulation)
                    lowerNeighborCount++;

                return neighborPopulation - sectorPopulation;
            }

            private static long PackSectorKey(int2 sectorCoord)
            {
                return ((long)sectorCoord.x << 32) | (uint)sectorCoord.y;
            }
        }

        [Header("Sector Runtime")]
        [Tooltip("Maximum number of active 1 km sectors tracked in the cold-path population model.")]
        [SerializeField, Min(MinimumSectorCapacity)] private int maxTrackedSectors = 128;
        [Tooltip("Maximum predation events buffered between 10-second cold solves.")]
        [SerializeField, Min(MinimumPredationEventCapacity)] private int maxBufferedPredationEvents = 256;
        [Tooltip("Seconds between ecosystem solves. 10 seconds = 0.1 Hz.")]
        [SerializeField, Min(1f)] private float coldTickIntervalSeconds = 10f;

        [Header("Initial Populations")]
        [Tooltip("Minimum deterministic prey population seeded into a new 1 km sector.")]
        [SerializeField, Min(0)] private int initialPreyPopulationMin = 192;
        [Tooltip("Maximum deterministic prey population seeded into a new 1 km sector.")]
        [SerializeField, Min(1)] private int initialPreyPopulationMax = 768;
        [Tooltip("Minimum deterministic predator population seeded into a new 1 km sector.")]
        [SerializeField, Min(0)] private int initialPredatorPopulationMin = 8;
        [Tooltip("Maximum deterministic predator population seeded into a new 1 km sector.")]
        [SerializeField, Min(1)] private int initialPredatorPopulationMax = 48;

        [Header("Lotka-Volterra Coefficients")]
        [Tooltip("Prey natural growth coefficient alpha in dPrey/dt = alpha*Prey - beta*Prey*Predator.")]
        [SerializeField, Min(0f)] private float preyGrowthAlpha = 0.0035f;
        [Tooltip("Predation coefficient beta in dPrey/dt = alpha*Prey - beta*Prey*Predator.")]
        [SerializeField, Min(0f)] private float predationBeta = 0.00003f;
        [Tooltip("Predator reproduction coefficient delta in dPredator/dt = delta*Prey*Predator - gamma*Predator.")]
        [SerializeField, Min(0f)] private float predatorGrowthDelta = 0.000012f;
        [Tooltip("Predator decay coefficient gamma in dPredator/dt = delta*Prey*Predator - gamma*Predator.")]
        [SerializeField, Min(0f)] private float predatorDecayGamma = 0.004f;
        [Tooltip("Additional predator gain factor applied when prey are consumed by runtime attacks.")]
        [SerializeField, Min(0f)] private float predatorGainPerPrey = 0.2f;
        [Tooltip("Upper clamp for prey population values after each solve.")]
        [SerializeField, Min(1)] private int maxPreyPopulation = 1024;
        [Tooltip("Upper clamp for predator population values after each solve.")]
        [SerializeField, Min(1)] private int maxPredatorPopulation = 128;
        [Tooltip("Exponential decay applied to harvest pressure each cold solve.")]
        [SerializeField, Min(0f)] private float harvestPressureDecay = 0.18f;
        [Tooltip("Additional prey loss applied while harvest pressure remains elevated.")]
        [SerializeField, Min(0f)] private float harvestCrashRate = 0.012f;
        [Tooltip("Harvest pressure added per prey consumed by runtime attacks.")]
        [SerializeField, Min(0f)] private float harvestPressurePerPrey = 0.08f;
        [Tooltip("Sector-to-sector prey migration rate along local population gradients.")]
        [SerializeField, Min(0f)] private float preyMigrationRate = 0.00085f;
        [Tooltip("Sector-to-sector predator migration rate along local population gradients.")]
        [SerializeField, Min(0f)] private float predatorMigrationRate = 0.00018f;
        [Tooltip("Prey population above this threshold is treated as overflow and bleeds toward depleted adjacent sectors.")]
        [SerializeField, Min(1f)] private float preyOverflowThreshold = 384f;
        [Tooltip("Predator population above this threshold is treated as overflow and bleeds toward depleted adjacent sectors.")]
        [SerializeField, Min(1f)] private float predatorOverflowThreshold = 28f;
        [Tooltip("Extra pull applied to depleted sectors so empty neighbors refill instead of remaining mathematically dead.")]
        [SerializeField, Min(0f)] private float depletedSectorBias = 1.35f;
        [Tooltip("Fraction of local overflow converted into outward migration pressure when adjacent sectors are less populated.")]
        [SerializeField, Min(0f)] private float overflowRedistributionRate = 0.85f;

        [Header("Adaptive Genetics")]
        [Tooltip("Minimum prey fitness retained by calm sectors so adaptation never fully collapses to zero.")]
        [SerializeField, Range(0f, 1f)] private float baselineFitness = 0.08f;
        [Tooltip("Rate constant driving prey adaptation upward in highly hunted sectors.")]
        [SerializeField, Min(0f)] private float fitnessAdaptationRate = 0.32f;
        [Tooltip("Rate constant driving prey adaptation back toward baseline after pressure fades.")]
        [SerializeField, Min(0f)] private float fitnessRecoveryRate = 0.05f;
        [Tooltip("Multiplier mapping harvest pressure into prey fitness gain.")]
        [SerializeField, Min(0f)] private float fitnessHarvestScale = 0.85f;
        [Tooltip("Maximum boid speed multiplier produced by sector adaptation.")]
        [SerializeField, Min(1f)] private float maximumSpeedMultiplier = 1.35f;
        [Tooltip("Maximum camouflage bias produced by sector adaptation.")]
        [SerializeField, Range(0f, 1f)] private float maximumCamouflageIndex = 0.9f;

        private NativeArray<SectorPopulationState> _sectorFrontStates;
        private NativeArray<SectorPopulationState> _sectorBackStates;
        private NativeHashMap<long, int> _sectorIndexByKey;
        private NativeArray<PredationEvent> _pendingPredationEvents;
        private NativeList<EcosystemSectorSaveRecord> _saveSnapshotSectors;
        private JobHandle _scheduledSolveHandle;
        private float _coldTickAccumulator;
        private int _activeSectorCount;
        private int _pendingPredationEventCount;
        private bool _registeredService;
        private bool _registeredSlowTickable;
        private bool _solveScheduled;

        /// <summary>
        /// True once the runtime-native state is allocated and registered.
        /// </summary>
        public bool IsInitialized => _sectorFrontStates.IsCreated && _sectorBackStates.IsCreated && _sectorIndexByKey.IsCreated;

        internal void CaptureSaveSnapshot()
        {
            if (!_saveSnapshotSectors.IsCreated)
                return;

            _saveSnapshotSectors.Clear();
            if (!IsInitialized)
                return;

            CompleteScheduledSolve(forceComplete: true);
            for (int sectorIndex = 0; sectorIndex < _activeSectorCount; sectorIndex++)
            {
                SectorPopulationState state = _sectorFrontStates[sectorIndex];
                _saveSnapshotSectors.Add(new EcosystemSectorSaveRecord
                {
                    SectorCoord = state.SectorCoord,
                    PackedPopulations = PackPopulationCounts(state.PreyPopulationRounded, state.PredatorPopulationRounded),
                    PackedAdaptation = PackAdaptationTraits(state.Fitness, state.SpeedMultiplier, state.CamouflageIndex, maximumSpeedMultiplier)
                });
            }
        }

        internal NativeArray<EcosystemSectorSaveRecord> GetSaveSnapshotArray()
        {
            return _saveSnapshotSectors.IsCreated ? _saveSnapshotSectors.AsArray() : default;
        }

        internal unsafe void RestoreFromLoadedRecords(EcosystemSectorSaveRecord[] loadedRecords)
        {
            if (!IsInitialized)
                return;

            CompleteScheduledSolve(forceComplete: true);
            _sectorIndexByKey.Clear();
            _pendingPredationEventCount = 0;
            _coldTickAccumulator = 0f;
            _solveScheduled = false;
            _scheduledSolveHandle = default;

            if (_sectorFrontStates.IsCreated)
            {
                void* frontPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_sectorFrontStates);
                UnsafeUtility.MemClear(frontPtr, _sectorFrontStates.Length * UnsafeUtility.SizeOf<SectorPopulationState>());
            }

            if (_sectorBackStates.IsCreated)
            {
                void* backPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_sectorBackStates);
                UnsafeUtility.MemClear(backPtr, _sectorBackStates.Length * UnsafeUtility.SizeOf<SectorPopulationState>());
            }

            int recordCount = loadedRecords != null ? math.min(loadedRecords.Length, _sectorFrontStates.Length) : 0;
            _activeSectorCount = 0;
            for (int sectorIndex = 0; sectorIndex < recordCount; sectorIndex++)
            {
                EcosystemSectorSaveRecord saveRecord = loadedRecords[sectorIndex];
                UnpackPopulationCounts(saveRecord.PackedPopulations, out int preyPopulation, out int predatorPopulation);
                SectorPopulationState restoredState = new SectorPopulationState
                {
                    SectorCoord = saveRecord.SectorCoord,
                    PreyPopulation = preyPopulation,
                    PredatorPopulation = predatorPopulation,
                    HarvestPressure = 0f,
                    Fitness = baselineFitness,
                    SpeedMultiplier = 1f,
                    CamouflageIndex = 0f,
                    PreyPopulationRounded = preyPopulation,
                    PredatorPopulationRounded = predatorPopulation
                };

                UnpackAdaptationTraits(
                    saveRecord.PackedAdaptation,
                    maximumSpeedMultiplier,
                    out restoredState.Fitness,
                    out restoredState.SpeedMultiplier,
                    out restoredState.CamouflageIndex);

                _sectorFrontStates[sectorIndex] = restoredState;
                _sectorBackStates[sectorIndex] = restoredState;
                _sectorIndexByKey.TryAdd(PackSectorKey(saveRecord.SectorCoord), sectorIndex);
                _activeSectorCount++;
            }
        }

        private void Awake()
        {
            SanitizeSettings();
            AllocateRuntimeState();
        }

        private void OnEnable()
        {
            SanitizeSettings();
            AllocateRuntimeState();
            TryRegisterService();
            TryRegisterSlowTickable();
        }

        private void OnDisable()
        {
            TryUnregisterSlowTickable();
            TryUnregisterService();
            DisposeRuntimeState();
        }

        /// <summary>
        /// Advances the sector population solve at 0.1 Hz using a Burst job.
        /// </summary>
        public void SlowTick()
        {
            if (!IsInitialized)
                return;

            CompleteScheduledSolve(forceComplete: false);
            EnsurePlayerSectorRegistered();
            EnsureMigrationNeighborSectorsRegistered();

            _coldTickAccumulator += DefaultSlowTickIntervalSeconds;
            if (_coldTickAccumulator < coldTickIntervalSeconds)
                return;

            _coldTickAccumulator -= coldTickIntervalSeconds;
            CompleteScheduledSolve(forceComplete: true);
            ApplyPendingPredationEvents();
            ScheduleSectorSolve();
        }

        /// <summary>
        /// Resolves predator/prey counts for the 1 km sector containing the supplied world position.
        /// </summary>
        public bool TryGetSectorPopulation(Vector3 worldPosition, out EcosystemSectorPopulationSample sample)
        {
            sample = default;
            if (!IsInitialized)
                return false;

            if (_solveScheduled && !_scheduledSolveHandle.IsCompleted)
                return false;

            CompleteScheduledSolve(forceComplete: false);
            int2 sectorCoord = QuantizeSector(worldPosition);
            int slotIndex = ResolveOrCreateSectorSlot(sectorCoord, seedWithBaseline: true);
            if (slotIndex < 0)
                return false;

            SectorPopulationState state = _sectorFrontStates[slotIndex];
            sample.SectorX = state.SectorCoord.x;
            sample.SectorZ = state.SectorCoord.y;
            sample.PreyPopulation = state.PreyPopulationRounded;
            sample.PredatorPopulation = state.PredatorPopulationRounded;
            sample.Fitness = state.Fitness;
            sample.SpeedMultiplier = state.SpeedMultiplier;
            sample.CamouflageIndex = state.CamouflageIndex;
            return true;
        }

        /// <summary>
        /// Registers prey consumption so the next cold solve reflects predator feeding pressure.
        /// </summary>
        public void ReportPredation(Vector3 worldPosition, int preyConsumed)
        {
            if (!IsInitialized || preyConsumed <= 0)
                return;

            int2 sectorCoord = QuantizeSector(worldPosition);
            long packedSectorKey = PackSectorKey(sectorCoord);
            int slotIndex;
            if (_sectorIndexByKey.TryGetValue(packedSectorKey, out int existingSlotIndex))
            {
                slotIndex = existingSlotIndex;
            }
            else
            {
                if (_solveScheduled && !_scheduledSolveHandle.IsCompleted)
                    return;

                slotIndex = ResolveOrCreateSectorSlot(sectorCoord, seedWithBaseline: true);
            }

            if (slotIndex < 0 || _pendingPredationEventCount >= _pendingPredationEvents.Length)
                return;

            PredationEvent predationEvent = default;
            predationEvent.SectorKey = packedSectorKey;
            predationEvent.PreyConsumed = preyConsumed;
            _pendingPredationEvents[_pendingPredationEventCount] = predationEvent;
            _pendingPredationEventCount++;
        }

        private void SanitizeSettings()
        {
            maxTrackedSectors = math.max(MinimumSectorCapacity, maxTrackedSectors);
            maxBufferedPredationEvents = math.max(MinimumPredationEventCapacity, maxBufferedPredationEvents);
            coldTickIntervalSeconds = math.max(1f, coldTickIntervalSeconds);
            initialPreyPopulationMax = math.max(initialPreyPopulationMin, initialPreyPopulationMax);
            initialPredatorPopulationMax = math.max(initialPredatorPopulationMin, initialPredatorPopulationMax);
            maxPreyPopulation = math.max(initialPreyPopulationMax, maxPreyPopulation);
            maxPredatorPopulation = math.max(initialPredatorPopulationMax, maxPredatorPopulation);
            preyOverflowThreshold = math.clamp(preyOverflowThreshold, 1f, maxPreyPopulation);
            predatorOverflowThreshold = math.clamp(predatorOverflowThreshold, 1f, maxPredatorPopulation);
            baselineFitness = math.clamp(baselineFitness, 0f, 1f);
            maximumSpeedMultiplier = math.max(1f, maximumSpeedMultiplier);
            maximumCamouflageIndex = math.clamp(maximumCamouflageIndex, 0f, 1f);
        }

        private void AllocateRuntimeState()
        {
            if (_sectorFrontStates.IsCreated)
                return;

            // COLD ALLOC: NativeArray<SectorPopulationState>[maxTrackedSectors] - ecosystem sector front buffer for Burst readers - owner: EcosystemDirector
            _sectorFrontStates = new NativeArray<SectorPopulationState>(maxTrackedSectors, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<SectorPopulationState>[maxTrackedSectors] - ecosystem sector back buffer for Burst writers - owner: EcosystemDirector
            _sectorBackStates = new NativeArray<SectorPopulationState>(maxTrackedSectors, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeHashMap<long,int>[maxTrackedSectors] - packed sector-key to slot lookup for O(1) cold-path classification - owner: EcosystemDirector
            _sectorIndexByKey = new NativeHashMap<long, int>(maxTrackedSectors, Allocator.Persistent);
            // COLD ALLOC: NativeArray<PredationEvent>[maxBufferedPredationEvents] - predation event ring for next cold solve consumption - owner: EcosystemDirector
            _pendingPredationEvents = new NativeArray<PredationEvent>(maxBufferedPredationEvents, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeList<EcosystemSectorSaveRecord>[maxTrackedSectors] - packed ecosystem persistence snapshot staging buffer - owner: EcosystemDirector
            _saveSnapshotSectors = new NativeList<EcosystemSectorSaveRecord>(maxTrackedSectors, Allocator.Persistent);
            _activeSectorCount = 0;
            _pendingPredationEventCount = 0;
            _coldTickAccumulator = 0f;
            _scheduledSolveHandle = default;
            _solveScheduled = false;
        }

        private void DisposeRuntimeState()
        {
            JobHandle disposeDependency = _solveScheduled ? _scheduledSolveHandle : default;

            if (_sectorFrontStates.IsCreated)
                _sectorFrontStates.Dispose(disposeDependency);
            if (_sectorBackStates.IsCreated)
                _sectorBackStates.Dispose(disposeDependency);
            if (_sectorIndexByKey.IsCreated)
                _sectorIndexByKey.Dispose(disposeDependency);
            if (_pendingPredationEvents.IsCreated)
                _pendingPredationEvents.Dispose(disposeDependency);
            if (_saveSnapshotSectors.IsCreated)
                _saveSnapshotSectors.Dispose(disposeDependency);

            _sectorFrontStates = default;
            _sectorBackStates = default;
            _sectorIndexByKey = default;
            _pendingPredationEvents = default;
            _saveSnapshotSectors = default;
            _activeSectorCount = 0;
            _pendingPredationEventCount = 0;
            _coldTickAccumulator = 0f;
            _scheduledSolveHandle = default;
            _solveScheduled = false;
        }

        private void TryRegisterService()
        {
            if (_registeredService)
                return;

            GlobalRegistry.RegisterEcosystemDirectorService(this);
            _registeredService = true;
        }

        private void TryUnregisterService()
        {
            if (!_registeredService)
                return;

            GlobalRegistry.UnregisterEcosystemDirectorService(this);
            _registeredService = false;
        }

        private void TryRegisterSlowTickable()
        {
            if (_registeredSlowTickable)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.UI);
            _registeredSlowTickable = true;
        }

        private void TryUnregisterSlowTickable()
        {
            if (!_registeredSlowTickable)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
            _registeredSlowTickable = false;
        }

        private void EnsurePlayerSectorRegistered()
        {
            if (_solveScheduled && !_scheduledSolveHandle.IsCompleted)
                return;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext == null || !playerContext.IsInitialized || playerContext.PlayerTransform == null)
                return;

            ResolveOrCreateSectorSlot(QuantizeSector(playerContext.PlayerTransform.position), seedWithBaseline: true);
        }

        private void EnsureMigrationNeighborSectorsRegistered()
        {
            if (_solveScheduled && !_scheduledSolveHandle.IsCompleted)
                return;

            int seedSectorCount = _activeSectorCount;
            for (int sectorIndex = 0; sectorIndex < seedSectorCount && _activeSectorCount < _sectorFrontStates.Length; sectorIndex++)
            {
                SectorPopulationState sourceState = _sectorFrontStates[sectorIndex];
                if (sourceState.PreyPopulationRounded <= 0 && sourceState.PredatorPopulationRounded <= 0)
                    continue;

                int2 sectorCoord = sourceState.SectorCoord;
                ResolveOrCreateSectorSlot(sectorCoord + new int2(1, 0), seedWithBaseline: false);
                if (_activeSectorCount >= _sectorFrontStates.Length)
                    break;

                ResolveOrCreateSectorSlot(sectorCoord + new int2(-1, 0), seedWithBaseline: false);
                if (_activeSectorCount >= _sectorFrontStates.Length)
                    break;

                ResolveOrCreateSectorSlot(sectorCoord + new int2(0, 1), seedWithBaseline: false);
                if (_activeSectorCount >= _sectorFrontStates.Length)
                    break;

                ResolveOrCreateSectorSlot(sectorCoord + new int2(0, -1), seedWithBaseline: false);
            }
        }

        private void ApplyPendingPredationEvents()
        {
            if (_pendingPredationEventCount <= 0)
                return;

            for (int eventIndex = 0; eventIndex < _pendingPredationEventCount; eventIndex++)
            {
                PredationEvent predationEvent = _pendingPredationEvents[eventIndex];
                if (!_sectorIndexByKey.TryGetValue(predationEvent.SectorKey, out int slotIndex))
                    continue;

                SectorPopulationState state = _sectorFrontStates[slotIndex];
                float preyRemoved = math.max(0f, predationEvent.PreyConsumed);
                float originalPreyPopulation = math.max(1f, state.PreyPopulation);
                float mortality01 = math.saturate(preyRemoved / originalPreyPopulation);
                state.PreyPopulation = math.max(0f, state.PreyPopulation - preyRemoved);
                state.PredatorPopulation = math.min(maxPredatorPopulation, state.PredatorPopulation + (preyRemoved * predatorGainPerPrey));
                state.HarvestPressure = math.max(0f, state.HarvestPressure + (preyRemoved * harvestPressurePerPrey));
                state.Fitness = math.max(state.Fitness, math.clamp(baselineFitness + mortality01 * fitnessHarvestScale, 0f, 1f));
                state.SpeedMultiplier = math.lerp(1f, maximumSpeedMultiplier, state.Fitness);
                state.CamouflageIndex = math.saturate(math.lerp(0f, maximumCamouflageIndex, math.sqrt(state.Fitness)));
                state.PreyPopulationRounded = (int)math.round(state.PreyPopulation);
                state.PredatorPopulationRounded = (int)math.round(state.PredatorPopulation);
                _sectorFrontStates[slotIndex] = state;
                _sectorBackStates[slotIndex] = state;
            }

            _pendingPredationEventCount = 0;
        }

        private void ScheduleSectorSolve()
        {
            if (_activeSectorCount <= 0 || _solveScheduled)
                return;

            var solveJob = new LotkaVolterraSolveJob
            {
                FrontStates = _sectorFrontStates,
                SectorIndexByKey = _sectorIndexByKey,
                BackStates = _sectorBackStates,
                DeltaTimeSeconds = coldTickIntervalSeconds,
                Alpha = preyGrowthAlpha,
                Beta = predationBeta,
                Delta = predatorGrowthDelta,
                Gamma = predatorDecayGamma,
                PredatorGainPerPrey = predatorGainPerPrey,
                HarvestPressureDecay = harvestPressureDecay,
                HarvestCrashRate = harvestCrashRate,
                PreyMigrationRate = preyMigrationRate,
                PredatorMigrationRate = predatorMigrationRate,
                PreyOverflowThreshold = preyOverflowThreshold,
                PredatorOverflowThreshold = predatorOverflowThreshold,
                DepletedSectorBias = depletedSectorBias,
                OverflowRedistributionRate = overflowRedistributionRate,
                BaselineFitness = baselineFitness,
                FitnessAdaptationRate = fitnessAdaptationRate,
                FitnessRecoveryRate = fitnessRecoveryRate,
                FitnessHarvestScale = fitnessHarvestScale,
                MaximumSpeedMultiplier = maximumSpeedMultiplier,
                MaximumCamouflageIndex = maximumCamouflageIndex,
                MaxPreyPopulation = maxPreyPopulation,
                MaxPredatorPopulation = maxPredatorPopulation
            };

            _scheduledSolveHandle = solveJob.Schedule(_activeSectorCount, 16);
            _solveScheduled = true;
        }

        private void CompleteScheduledSolve(bool forceComplete)
        {
            if (!_solveScheduled)
                return;

            if (!forceComplete && !_scheduledSolveHandle.IsCompleted)
                return;

            _scheduledSolveHandle.Complete();
            NativeArray<SectorPopulationState> swap = _sectorFrontStates;
            _sectorFrontStates = _sectorBackStates;
            _sectorBackStates = swap;
            _scheduledSolveHandle = default;
            _solveScheduled = false;
        }

        private int ResolveOrCreateSectorSlot(int2 sectorCoord, bool seedWithBaseline = true)
        {
            long packedKey = PackSectorKey(sectorCoord);
            if (_sectorIndexByKey.TryGetValue(packedKey, out int existingSlot))
                return existingSlot;

            if (_activeSectorCount >= _sectorFrontStates.Length)
                return -1;

            int slotIndex = _activeSectorCount;
            _activeSectorCount++;
            _sectorIndexByKey.TryAdd(packedKey, slotIndex);

            SectorPopulationState seededState = SeedSectorState(sectorCoord, seedWithBaseline);
            _sectorFrontStates[slotIndex] = seededState;
            _sectorBackStates[slotIndex] = seededState;
            return slotIndex;
        }

        private SectorPopulationState SeedSectorState(int2 sectorCoord, bool seedWithBaseline)
        {
            int preyPopulation = 0;
            int predatorPopulation = 0;
            if (seedWithBaseline)
            {
                uint hash = math.hash(new uint2((uint)sectorCoord.x, (uint)sectorCoord.y));
                float prey01 = (hash & 0xFFFFu) / 65535f;
                float predator01 = ((hash >> 16) & 0xFFFFu) / 65535f;
                preyPopulation = (int)math.round(math.lerp(initialPreyPopulationMin, initialPreyPopulationMax, prey01));
                predatorPopulation = (int)math.round(math.lerp(initialPredatorPopulationMin, initialPredatorPopulationMax, predator01));
            }

            SectorPopulationState state = default;
            state.SectorCoord = sectorCoord;
            state.PreyPopulation = preyPopulation;
            state.PredatorPopulation = predatorPopulation;
            state.HarvestPressure = 0f;
            state.Fitness = baselineFitness;
            state.SpeedMultiplier = 1f;
            state.CamouflageIndex = 0f;
            state.PreyPopulationRounded = preyPopulation;
            state.PredatorPopulationRounded = predatorPopulation;
            return state;
        }

        private static int2 QuantizeSector(Vector3 worldPosition)
        {
            float2 scaled = new float2(worldPosition.x, worldPosition.z) / SectorEdgeLengthMeters;
            return (int2)math.floor(scaled);
        }

        private static long PackSectorKey(int2 sectorCoord)
        {
            return ((long)sectorCoord.x << 32) | (uint)sectorCoord.y;
        }

        private static uint PackPopulationCounts(int preyPopulation, int predatorPopulation)
        {
            uint packedPrey = (ushort)math.clamp(preyPopulation, 0, ushort.MaxValue);
            uint packedPredator = (ushort)math.clamp(predatorPopulation, 0, ushort.MaxValue);
            return packedPrey | (packedPredator << 16);
        }

        private static uint PackAdaptationTraits(float fitness, float speedMultiplier, float camouflageIndex, float maximumSpeedMultiplier)
        {
            uint packedFitness = (uint)math.round(math.saturate(fitness) * 255f);
            float safeMaximumSpeedMultiplier = math.max(1f, maximumSpeedMultiplier);
            float speed01 = math.saturate((speedMultiplier - 1f) / (safeMaximumSpeedMultiplier - 1f + 0.0001f));
            uint packedSpeed = (uint)math.round(speed01 * 255f);
            uint packedCamouflage = (uint)math.round(math.saturate(camouflageIndex) * 255f);
            return packedFitness | (packedSpeed << 8) | (packedCamouflage << 16);
        }

        private static void UnpackPopulationCounts(uint packedCounts, out int preyPopulation, out int predatorPopulation)
        {
            preyPopulation = (int)(packedCounts & 0xFFFFu);
            predatorPopulation = (int)((packedCounts >> 16) & 0xFFFFu);
        }

        private static void UnpackAdaptationTraits(uint packedTraits, float maximumSpeedMultiplier, out float fitness, out float speedMultiplier, out float camouflageIndex)
        {
            fitness = ((packedTraits >> 0) & 0xFFu) / 255f;
            float speed01 = ((packedTraits >> 8) & 0xFFu) / 255f;
            camouflageIndex = ((packedTraits >> 16) & 0xFFu) / 255f;
            speedMultiplier = math.lerp(1f, math.max(1f, maximumSpeedMultiplier), speed01);
        }
    }
}
