using System.Runtime.InteropServices;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Systems.AI;
using Hecton8.UI;
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
        private const float DefaultDiffusionTickIntervalSeconds = 5f;
        private const float SectorEdgeLengthMeters = 1000f;
        private const int MinimumSectorCapacity = 16;
        private const int MinimumPredationEventCapacity = 32;
        private const float DefaultHostilityPeakHoldSeconds = 18f;
        private const float LogicalLodFullSimDistanceMeters = 40f;
        private const float LogicalLodDataOnlyDistanceMeters = 150f;
        private const float ThermalSpawnTemperatureThresholdCelsius = 40f;
        private const float ThermalSpawnDepthThresholdMeters = 2000f;
        private const float LightFalloffDepthMeters = 2500f;
        private static readonly string[] ThermalSpawnTokens = { "lava", "thermal", "brine", "heat", "volcanic", "smoker" };
        private static readonly string[] SharkSpawnTokens = { "shark", "hunter", "stalker" };

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
            public float DiagonalMigrationWeight;
            public float BorderBleedEqualizationRate;
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
                float totalWeight = 0f;
                int lowerNeighborCount = 0;
                gradient += SampleNeighborDelta(sectorCoord + new int2(1, 0), sectorPopulation, samplePrey, 1f, ref totalWeight, ref lowerNeighborCount);
                gradient += SampleNeighborDelta(sectorCoord + new int2(-1, 0), sectorPopulation, samplePrey, 1f, ref totalWeight, ref lowerNeighborCount);
                gradient += SampleNeighborDelta(sectorCoord + new int2(0, 1), sectorPopulation, samplePrey, 1f, ref totalWeight, ref lowerNeighborCount);
                gradient += SampleNeighborDelta(sectorCoord + new int2(0, -1), sectorPopulation, samplePrey, 1f, ref totalWeight, ref lowerNeighborCount);
                gradient += SampleNeighborDelta(sectorCoord + new int2(1, 1), sectorPopulation, samplePrey, DiagonalMigrationWeight, ref totalWeight, ref lowerNeighborCount);
                gradient += SampleNeighborDelta(sectorCoord + new int2(1, -1), sectorPopulation, samplePrey, DiagonalMigrationWeight, ref totalWeight, ref lowerNeighborCount);
                gradient += SampleNeighborDelta(sectorCoord + new int2(-1, 1), sectorPopulation, samplePrey, DiagonalMigrationWeight, ref totalWeight, ref lowerNeighborCount);
                gradient += SampleNeighborDelta(sectorCoord + new int2(-1, -1), sectorPopulation, samplePrey, DiagonalMigrationWeight, ref totalWeight, ref lowerNeighborCount);

                float safeOverflowThreshold = math.max(1f, overflowThreshold);
                float depletion01 = math.saturate(1f - (sectorPopulation / safeOverflowThreshold));
                float equalization = totalWeight > 0f
                    ? (gradient / totalWeight) * math.max(0f, BorderBleedEqualizationRate)
                    : 0f;
                equalization *= 1f + (depletion01 * DepletedSectorBias);

                float overflow = math.max(0f, sectorPopulation - safeOverflowThreshold);
                if (overflow > 0f && lowerNeighborCount > 0)
                {
                    equalization -= overflow * OverflowRedistributionRate * (lowerNeighborCount * 0.125f);
                }

                return equalization;
            }

            private float SampleNeighborDelta(
                int2 neighborCoord,
                float sectorPopulation,
                bool samplePrey,
                float weight,
                ref float totalWeight,
                ref int lowerNeighborCount)
            {
                if (!SectorIndexByKey.TryGetValue(PackSectorKey(neighborCoord), out int neighborIndex))
                    return 0f;

                SectorPopulationState neighbor = FrontStates[neighborIndex];
                float neighborPopulation = samplePrey ? neighbor.PreyPopulation : neighbor.PredatorPopulation;
                if (neighborPopulation < sectorPopulation)
                    lowerNeighborCount++;

                totalWeight += weight;
                return (neighborPopulation - sectorPopulation) * weight;
            }

            private static long PackSectorKey(int2 sectorCoord)
            {
                return ((long)sectorCoord.x << 32) | (uint)sectorCoord.y;
            }
        }

        [BurstCompile]
        private struct PopulationDiffusionJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SectorPopulationState> FrontStates;
            [ReadOnly] public NativeHashMap<long, int> SectorIndexByKey;
            public NativeArray<SectorPopulationState> BackStates;
            public float PreyDiffusionFraction;
            public float PredatorFollowPreyDiffusionFraction;
            public float PredatorPerPreyRatio;
            public int MaxPreyPopulation;
            public int MaxPredatorPopulation;

            public void Execute(int index)
            {
                SectorPopulationState state = FrontStates[index];
                float prey = math.max(0f, state.PreyPopulation);
                float predator = math.max(0f, state.PredatorPopulation);
                float preyNet = 0f;
                float predatorNet = 0f;
                int2 sectorCoord = state.SectorCoord;

                AccumulateNeighborDiffusion(sectorCoord + new int2(1, 0), prey, ref preyNet, ref predatorNet);
                AccumulateNeighborDiffusion(sectorCoord + new int2(-1, 0), prey, ref preyNet, ref predatorNet);
                AccumulateNeighborDiffusion(sectorCoord + new int2(0, 1), prey, ref preyNet, ref predatorNet);
                AccumulateNeighborDiffusion(sectorCoord + new int2(0, -1), prey, ref preyNet, ref predatorNet);

                prey = math.clamp(prey + preyNet, 0f, MaxPreyPopulation);
                predator = math.clamp(predator + predatorNet, 0f, MaxPredatorPopulation);
                state.PreyPopulation = prey;
                state.PredatorPopulation = predator;
                state.PreyPopulationRounded = (int)math.round(prey);
                state.PredatorPopulationRounded = (int)math.round(predator);
                BackStates[index] = state;
            }

            private void AccumulateNeighborDiffusion(
                int2 neighborCoord,
                float localPrey,
                ref float preyNet,
                ref float predatorNet)
            {
                if (!SectorIndexByKey.TryGetValue(PackSectorKey(neighborCoord), out int neighborIndex))
                    return;

                SectorPopulationState neighbor = FrontStates[neighborIndex];
                float preyDelta = neighbor.PreyPopulation - localPrey;
                preyNet += preyDelta * PreyDiffusionFraction;
                predatorNet += preyDelta * PredatorPerPreyRatio * PredatorFollowPreyDiffusionFraction;
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
        [Tooltip("Seconds between explicit diffusion passes that bleed prey and predators across adjacent 1 km sector borders.")]
        [SerializeField, Min(1f)] private float diffusionTickIntervalSeconds = DefaultDiffusionTickIntervalSeconds;
        [Tooltip("Fraction of prey density differential moved across each adjacent sector edge on a diffusion tick.")]
        [SerializeField, Range(0f, 1f)] private float preyDiffusionFraction = 0.05f;
        [Tooltip("Fraction of prey density differential converted into predator movement pressure on a diffusion tick.")]
        [SerializeField, Range(0f, 1f)] private float predatorFollowPreyDiffusionFraction = 0.05f;
        [Tooltip("Relative weight applied to diagonal 1 km sector bleed so cross-border diffusion is not restricted to four cardinal neighbors.")]
        [SerializeField, Min(0f)] private float diagonalMigrationWeight = 0.7071f;
        [Tooltip("Additional equalization scalar applied to the weighted neighbor differential before migration rates are integrated.")]
        [SerializeField, Min(0f)] private float borderBleedEqualizationRate = 1f;
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

        [Header("Biome Hostility")]
        [Tooltip("Normalized hostility applied when the player kills one standard apex predator.")]
        [SerializeField, Range(0.01f, 1f)] private float hostilityPerApexKill = 0.22f;
        [Tooltip("Normalized hostility removed per SlowTick while the biome is calming down.")]
        [SerializeField, Range(0.001f, 0.2f)] private float hostilityDecayPerSlowTick = 0.015f;
        [Tooltip("Minimum director peak-hold duration injected when hostility is elevated.")]
        [SerializeField, Min(0f)] private float hostilityPeakHoldSeconds = DefaultHostilityPeakHoldSeconds;

        [Header("Predator Starvation")]
        [Tooltip("Comfort prey-per-predator ratio. Values below this drive desperation pressure upward.")]
        [SerializeField, Min(1f)] private float starvationComfortPreyPerPredator = 12f;
        [Tooltip("Additional starvation pressure sourced from elevated sector harvest pressure.")]
        [SerializeField, Min(0f)] private float starvationHarvestWeight = 0.65f;
        [Tooltip("Scale applied when converting starvation pressure into director hostility.")]
        [SerializeField, Range(0f, 1f)] private float starvationHostilityWeight = 0.85f;

        private NativeArray<SectorPopulationState> _sectorFrontStates;
        private NativeArray<SectorPopulationState> _sectorBackStates;
        private NativeHashMap<long, int> _sectorIndexByKey;
        private NativeArray<PredationEvent> _pendingPredationEvents;
        private NativeList<EcosystemSectorSaveRecord> _saveSnapshotSectors;
        private JobHandle _scheduledDiffusionHandle;
        private JobHandle _scheduledSolveHandle;
        private float _coldTickAccumulator;
        private float _diffusionTickAccumulator;
        private int _activeSectorCount;
        private int _pendingPredationEventCount;
        private bool _registeredService;
        private bool _registeredSlowTickable;
        private bool _diffusionScheduled;
        private bool _solveScheduled;
        private float _biomeHostility01;
        private float _starvationAggressionPressure01;
        private int _hostilityTier;
        private HectonMapMagicVegetationBridge _cachedVegetationBridge;
        private PersistentWorldRegistry _cachedPersistentWorldRegistry;

        /// <summary>
        /// True once the runtime-native state is allocated and registered.
        /// </summary>
        public bool IsInitialized => _sectorFrontStates.IsCreated && _sectorBackStates.IsCreated && _sectorIndexByKey.IsCreated;

        /// <summary>
        /// Normalized biome hostility score exposed to UI and pacing systems.
        /// </summary>
        public float BiomeHostility01 => ResolveCombinedHostility01();

        internal FaunaLogicalLodTier ResolveLogicalLodTier(Vector3 observerPosition, Vector3 faunaPosition)
        {
            AbsoluteUniversePosition observerAup = AbsoluteUniversePosition.FromRuntimePosition(observerPosition);
            AbsoluteUniversePosition faunaAup = AbsoluteUniversePosition.FromRuntimePosition(faunaPosition);
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in observerAup, in faunaAup);

            if (distanceSq <= (LogicalLodFullSimDistanceMeters * LogicalLodFullSimDistanceMeters))
                return FaunaLogicalLodTier.FullSim;

            if (distanceSq <= (LogicalLodDataOnlyDistanceMeters * LogicalLodDataOnlyDistanceMeters))
                return FaunaLogicalLodTier.DataOnly;

            return FaunaLogicalLodTier.Hibernating;
        }

        internal bool TryBuildEnvelope(Vector3 worldPosition, out EcosystemEnvelope envelope)
        {
            float depthMeters = 0f;
            MapMagicBridge mapMagicBridge = MapMagicBridge.Instance;
            if (mapMagicBridge != null)
                depthMeters = math.max(0f, mapMagicBridge.WaterSurfaceLevel - worldPosition.y);

            ResolveRuntimeReferences();

            float temperatureCelsius = _cachedVegetationBridge != null
                ? _cachedVegetationBridge.GetWaterTemperature(worldPosition)
                : 15f;

            float4 normalizedChannels;
            if (!ChemicalInfluenceGrid.TrySampleNormalizedChannels(worldPosition, out normalizedChannels))
                normalizedChannels = float4.zero;

            float lightExposure01 = math.saturate(1f - (depthMeters / math.max(1f, LightFalloffDepthMeters)));
            envelope = new EcosystemEnvelope(
                temperatureCelsius,
                depthMeters,
                lightExposure01,
                normalizedChannels.x,
                normalizedChannels.y,
                normalizedChannels.z,
                ResolveCombinedHostility01());
            return true;
        }

        internal bool TryResolveSpawnWeightMultiplier(CreatureArchetypeData archetype, Vector3 worldPosition, out float selectionMultiplier)
        {
            selectionMultiplier = 1f;
            if (archetype == null)
                return true;

            TryBuildEnvelope(worldPosition, out EcosystemEnvelope envelope);
            if (RequiresThermalEnvelope(archetype) &&
                (envelope.TemperatureCelsius < ThermalSpawnTemperatureThresholdCelsius ||
                 envelope.DepthMeters < ThermalSpawnDepthThresholdMeters))
            {
                return false;
            }

            float scentPressure01 = math.saturate(math.max(envelope.BloodScent01, envelope.FearScent01));
            if (IsSharkLikePredator(archetype))
            {
                selectionMultiplier = math.lerp(0.65f, 1.85f, scentPressure01);
                return selectionMultiplier > 0f;
            }

            if (archetype.isAggressive || archetype.roleType == CreatureRoleType.Hunter || archetype.roleType == CreatureRoleType.Leviathan)
            {
                selectionMultiplier = math.lerp(0.9f, 1.35f, scentPressure01);
            }

            return selectionMultiplier > 0f;
        }

        internal bool IsApexTombstoned(uint uniqueInstanceUid)
        {
            ResolveRuntimeReferences();
            return _cachedPersistentWorldRegistry != null && _cachedPersistentWorldRegistry.IsTombstoned(uniqueInstanceUid);
        }

        internal void RegisterApexPredatorKill(uint uniqueInstanceUid, Vector3 worldPosition, float hostilityDelta)
        {
            ResolveRuntimeReferences();
            _cachedPersistentWorldRegistry?.TryRegisterFaunaTombstone(uniqueInstanceUid);
            ReportApexPredatorKilled(worldPosition, hostilityDelta);
        }

        internal void CaptureSaveSnapshot()
        {
            if (!_saveSnapshotSectors.IsCreated)
                return;

            _saveSnapshotSectors.Clear();
            if (!IsInitialized)
                return;

            CompleteScheduledSimulation(forceComplete: true);
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

            CompleteScheduledSimulation(forceComplete: true);
            _sectorIndexByKey.Clear();
            _pendingPredationEventCount = 0;
            _coldTickAccumulator = 0f;
            _diffusionTickAccumulator = 0f;
            _diffusionScheduled = false;
            _scheduledDiffusionHandle = default;
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

            DecayBiomeHostility();
            CompleteScheduledSimulation(forceComplete: false);
            EnsurePlayerSectorRegistered();
            EnsureMigrationNeighborSectorsRegistered();

            _coldTickAccumulator += DefaultSlowTickIntervalSeconds;
            _diffusionTickAccumulator += DefaultSlowTickIntervalSeconds;
            if (_coldTickAccumulator >= coldTickIntervalSeconds)
            {
                _coldTickAccumulator -= coldTickIntervalSeconds;
                _diffusionTickAccumulator = 0f;
                CompleteScheduledSimulation(forceComplete: true);
                ApplyPendingPredationEvents();
                ScheduleSectorSolve();
                return;
            }

            if (_diffusionTickAccumulator < diffusionTickIntervalSeconds)
                return;

            _diffusionTickAccumulator -= diffusionTickIntervalSeconds;
            CompleteScheduledSimulation(forceComplete: true);
            ApplyPendingPredationEvents();
            SchedulePopulationDiffusion();
        }

        /// <summary>
        /// Resolves predator/prey counts for the 1 km sector containing the supplied world position.
        /// </summary>
        public bool TryGetSectorPopulation(Vector3 worldPosition, out EcosystemSectorPopulationSample sample)
        {
            sample = default;
            if (!IsInitialized)
                return false;

            if (HasPendingSimulationJob())
                return false;

            CompleteScheduledSimulation(forceComplete: false);
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

            EnvironmentalStrainManager.Instance?.AccumulatePredationStrain(worldPosition, preyConsumed);

            int2 sectorCoord = QuantizeSector(worldPosition);
            long packedSectorKey = PackSectorKey(sectorCoord);
            int slotIndex;
            if (_sectorIndexByKey.TryGetValue(packedSectorKey, out int existingSlotIndex))
            {
                slotIndex = existingSlotIndex;
            }
            else
            {
                if (HasPendingSimulationJob())
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

        /// <summary>
        /// Registers one player-attributed apex predator kill and escalates biome hostility.
        /// </summary>
        public void ReportApexPredatorKilled(Vector3 worldPosition, float hostilityDelta)
        {
            if (!IsInitialized)
                return;

            float appliedDelta = math.max(hostilityPerApexKill, hostilityDelta);
            int2 sectorCoord = QuantizeSector(worldPosition);
            if (!HasPendingSimulationJob())
            {
                int slotIndex = ResolveOrCreateSectorSlot(sectorCoord, seedWithBaseline: true);
                if (slotIndex >= 0)
                {
                    SectorPopulationState state = _sectorFrontStates[slotIndex];
                    state.PredatorPopulation = math.max(0f, state.PredatorPopulation - 1f);
                    state.PredatorPopulationRounded = (int)math.round(state.PredatorPopulation);
                    _sectorFrontStates[slotIndex] = state;
                    _sectorBackStates[slotIndex] = state;
                }
            }

            SetBiomeHostility(_biomeHostility01 + appliedDelta);
            ApplyDirectorHostilityPressure();
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
            diffusionTickIntervalSeconds = math.max(1f, diffusionTickIntervalSeconds);
            preyDiffusionFraction = math.clamp(preyDiffusionFraction, 0f, 1f);
            predatorFollowPreyDiffusionFraction = math.clamp(predatorFollowPreyDiffusionFraction, 0f, 1f);
            diagonalMigrationWeight = math.max(0f, diagonalMigrationWeight);
            borderBleedEqualizationRate = math.max(0f, borderBleedEqualizationRate);
            preyOverflowThreshold = math.clamp(preyOverflowThreshold, 1f, maxPreyPopulation);
            predatorOverflowThreshold = math.clamp(predatorOverflowThreshold, 1f, maxPredatorPopulation);
            baselineFitness = math.clamp(baselineFitness, 0f, 1f);
            maximumSpeedMultiplier = math.max(1f, maximumSpeedMultiplier);
            maximumCamouflageIndex = math.clamp(maximumCamouflageIndex, 0f, 1f);
            hostilityPerApexKill = math.clamp(hostilityPerApexKill, 0.01f, 1f);
            hostilityDecayPerSlowTick = math.clamp(hostilityDecayPerSlowTick, 0.001f, 0.2f);
            hostilityPeakHoldSeconds = math.max(0f, hostilityPeakHoldSeconds);
            starvationComfortPreyPerPredator = math.max(1f, starvationComfortPreyPerPredator);
            starvationHarvestWeight = math.max(0f, starvationHarvestWeight);
            starvationHostilityWeight = math.clamp(starvationHostilityWeight, 0f, 1f);
        }

        private void ResolveRuntimeReferences()
        {
            _cachedVegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (_cachedPersistentWorldRegistry == null)
                _cachedPersistentWorldRegistry = PersistentWorldRegistry.Instance;
        }

        private static bool RequiresThermalEnvelope(CreatureArchetypeData archetype)
        {
            return MatchesAnyToken(archetype.creatureId, ThermalSpawnTokens) ||
                   MatchesAnyToken(archetype.displayName, ThermalSpawnTokens) ||
                   MatchesAnyToken(archetype.gameplayPurpose, ThermalSpawnTokens) ||
                   MatchesAnyToken(archetype.behaviorTreeHint, ThermalSpawnTokens);
        }

        private static bool IsSharkLikePredator(CreatureArchetypeData archetype)
        {
            if (archetype == null)
                return false;

            return MatchesAnyToken(archetype.creatureId, SharkSpawnTokens) ||
                   MatchesAnyToken(archetype.displayName, SharkSpawnTokens) ||
                   (archetype.isAggressive && archetype.roleType == CreatureRoleType.Hunter);
        }

        private static bool MatchesAnyToken(string value, string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(value) || tokens == null || tokens.Length == 0)
                return false;

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrEmpty(token) && value.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private void DecayBiomeHostility()
        {
            if (_biomeHostility01 <= 0f)
                return;

            SetBiomeHostility(math.max(0f, _biomeHostility01 - hostilityDecayPerSlowTick));
            if (_biomeHostility01 > 0f)
                ApplyDirectorHostilityPressure();
        }

        private void ApplyDirectorHostilityPressure()
        {
            HectonDirectorAI director = HectonDirectorAI.ActiveRuntimeInstance;
            float combinedHostility01 = ResolveCombinedHostility01();
            if (director == null || combinedHostility01 <= 0f)
                return;

            float holdSeconds = hostilityPeakHoldSeconds * math.saturate(combinedHostility01 + (_starvationAggressionPressure01 * 0.35f));
            director.ApplyExternalPeakPressure(combinedHostility01, holdSeconds);
        }

        private void SetBiomeHostility(float hostility01)
        {
            float clamped = math.saturate(hostility01);
            if (math.abs(clamped - _biomeHostility01) <= 0.0001f)
                return;

            _biomeHostility01 = clamped;
            RefreshHostilityTier();
        }

        private void RefreshStarvationPressure()
        {
            if (!IsInitialized || _activeSectorCount <= 0)
            {
                _starvationAggressionPressure01 = 0f;
                RefreshHostilityTier();
                return;
            }

            float weightedPressureSum = 0f;
            float totalPredatorWeight = 0f;
            float safeComfortRatio = math.max(1f, starvationComfortPreyPerPredator);
            float safeHarvestNormalizer = math.max(1f, preyOverflowThreshold * harvestPressurePerPrey);
            for (int sectorIndex = 0; sectorIndex < _activeSectorCount; sectorIndex++)
            {
                SectorPopulationState state = _sectorFrontStates[sectorIndex];
                float predatorPopulation = math.max(0f, state.PredatorPopulation);
                if (predatorPopulation <= 0f)
                    continue;

                float preyPopulation = math.max(0f, state.PreyPopulation);
                float preyPerPredator = preyPopulation / math.max(1f, predatorPopulation);
                float scarcity01 = math.saturate(1f - (preyPerPredator / safeComfortRatio));
                float harvest01 = math.saturate(state.HarvestPressure / safeHarvestNormalizer);
                float sectorPressure01 = math.saturate(scarcity01 + (harvest01 * starvationHarvestWeight));
                weightedPressureSum += sectorPressure01 * predatorPopulation;
                totalPredatorWeight += predatorPopulation;
            }

            _starvationAggressionPressure01 = totalPredatorWeight > 0f
                ? math.saturate((weightedPressureSum / totalPredatorWeight) * starvationHostilityWeight)
                : 0f;
            RefreshHostilityTier();
        }

        private void RefreshHostilityTier()
        {
            int tier = ResolveHostilityTier(ResolveCombinedHostility01());
            if (tier == _hostilityTier)
                return;

            _hostilityTier = tier;
            switch (tier)
            {
                case 3:
                    NotificationEvents.PushCritical("BIOME HOSTILITY: EXTREME. THE ABYSS HATES YOU.");
                    break;

                case 2:
                    NotificationEvents.PushWarning("BIOME HOSTILITY: ELEVATED. PREDATOR PEAK EXTENDED.");
                    break;

                case 1:
                    NotificationEvents.PushInfo("BIOME HOSTILITY: RISING.");
                    break;
            }
        }

        private float ResolveCombinedHostility01()
        {
            return math.saturate(math.max(_biomeHostility01, _starvationAggressionPressure01));
        }

        private static int ResolveHostilityTier(float hostility01)
        {
            if (hostility01 >= 0.75f)
                return 3;

            if (hostility01 >= 0.4f)
                return 2;

            return hostility01 >= 0.15f ? 1 : 0;
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
            _diffusionTickAccumulator = 0f;
            _scheduledDiffusionHandle = default;
            _scheduledSolveHandle = default;
            _diffusionScheduled = false;
            _solveScheduled = false;
            _biomeHostility01 = 0f;
            _starvationAggressionPressure01 = 0f;
            _hostilityTier = 0;
        }

        private void DisposeRuntimeState()
        {
            JobHandle disposeDependency = default;
            if (_solveScheduled && _diffusionScheduled)
                disposeDependency = JobHandle.CombineDependencies(_scheduledSolveHandle, _scheduledDiffusionHandle);
            else if (_solveScheduled)
                disposeDependency = _scheduledSolveHandle;
            else if (_diffusionScheduled)
                disposeDependency = _scheduledDiffusionHandle;

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
            _diffusionTickAccumulator = 0f;
            _scheduledDiffusionHandle = default;
            _scheduledSolveHandle = default;
            _diffusionScheduled = false;
            _solveScheduled = false;
            _biomeHostility01 = 0f;
            _starvationAggressionPressure01 = 0f;
            _hostilityTier = 0;
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
            if (_registeredSlowTickable || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
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
            if (HasPendingSimulationJob())
                return;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext == null || !playerContext.IsInitialized || playerContext.PlayerTransform == null)
                return;

            ResolveOrCreateSectorSlot(QuantizeSector(playerContext.PlayerTransform.position), seedWithBaseline: true);
        }

        private void EnsureMigrationNeighborSectorsRegistered()
        {
            if (HasPendingSimulationJob())
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
            RefreshStarvationPressure();
        }

        private void ScheduleSectorSolve()
        {
            if (_activeSectorCount <= 0 || _solveScheduled || _diffusionScheduled)
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
                DiagonalMigrationWeight = diagonalMigrationWeight,
                BorderBleedEqualizationRate = borderBleedEqualizationRate,
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

        private void SchedulePopulationDiffusion()
        {
            if (_activeSectorCount <= 0 || _solveScheduled || _diffusionScheduled)
                return;

            float predatorPerPreyRatio = maxPreyPopulation > 0
                ? (float)maxPredatorPopulation / math.max(1f, maxPreyPopulation)
                : 0f;

            var diffusionJob = new PopulationDiffusionJob
            {
                FrontStates = _sectorFrontStates,
                SectorIndexByKey = _sectorIndexByKey,
                BackStates = _sectorBackStates,
                PreyDiffusionFraction = preyDiffusionFraction,
                PredatorFollowPreyDiffusionFraction = predatorFollowPreyDiffusionFraction,
                PredatorPerPreyRatio = predatorPerPreyRatio,
                MaxPreyPopulation = maxPreyPopulation,
                MaxPredatorPopulation = maxPredatorPopulation
            };

            _scheduledDiffusionHandle = diffusionJob.Schedule(_activeSectorCount, 16);
            _diffusionScheduled = true;
        }

        private bool HasPendingSimulationJob()
        {
            if (_solveScheduled && !_scheduledSolveHandle.IsCompleted)
                return true;

            return _diffusionScheduled && !_scheduledDiffusionHandle.IsCompleted;
        }

        private void CompleteScheduledSimulation(bool forceComplete)
        {
            CompleteScheduledDiffusion(forceComplete);
            CompleteScheduledSolve(forceComplete);
        }

        private void CompleteScheduledDiffusion(bool forceComplete)
        {
            if (!_diffusionScheduled)
                return;

            if (!forceComplete && !_scheduledDiffusionHandle.IsCompleted)
                return;

            _scheduledDiffusionHandle.Complete();
            NativeArray<SectorPopulationState> swap = _sectorFrontStates;
            _sectorFrontStates = _sectorBackStates;
            _sectorBackStates = swap;
            _scheduledDiffusionHandle = default;
            _diffusionScheduled = false;
            RefreshStarvationPressure();
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
            RefreshStarvationPressure();
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
