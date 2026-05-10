using System.Runtime.InteropServices;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Ecosystem;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.Systems.AI;
using Hecton8.UI;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

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
    /// Cold-path sector ecosystem table. Population is a deterministic cinematic roll per 1 km sector.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4037)]
    public sealed class EcosystemDirector : MonoBehaviour, ISlowTickable, ILateFrameTickable, IEcosystemDirectorService, IServiceHeartbeat, IServiceShutdown
    {
        internal static EcosystemDirector ActiveRuntimeInstance { get; private set; }

        private const float DefaultSlowTickIntervalSeconds = 0.5f;
        private const float DefaultFloraGrazingSearchRadiusMeters = 2.75f;
        private const float SectorEdgeLengthMeters = 1000f;
        private const float InvSectorEdgeLengthMeters = 1f / SectorEdgeLengthMeters;
        private const float InvStableSectorRandomMask = 1f / 16777215f;
        private const float DefaultLeviathanSectorSpawnChance = 0.15f;
        private const int DefaultGrazerPopulationPerSector = 10;
        private const int DefaultLeviathanPopulationPerSector = 1;
        private const int MinimumSectorCapacity = 16;
        private const float DefaultHostilityPeakHoldSeconds = 18f;
        private const float LogicalLodFullSimDistanceMeters = 50f;
        private const float LogicalLodDataOnlyDistanceMeters = 150f;
        private const float ThermalSpawnTemperatureThresholdCelsius = 40f;
        private const float ThermalSpawnDepthThresholdMeters = 2000f;
        private const float LightFalloffDepthMeters = 2500f;
        private const float PredatorDietValidationRadiusMeters = 500f;
        private const float CorpseSpawnInfluenceRadiusMeters = 500f;
        private const float MinimumCorpseDietInfluence01 = 0.001f;
        private const float CorpseSpawnSelectionScale = 2.6f;
        private const float WhaleFallScavengerSpawnMultiplier = 10f;
        private const float HighPlayerStressThreshold01 = 0.8f;
        private const float WhaleFallAcousticImpulseLifetimeSeconds = 600f;
        private const float WhaleFallAcousticImpulseEnergyJoules = 28000f;
        private const float WhaleFallAcousticImpulseVolume01 = 0.42f;
        private const float WhaleFallAcousticImpulsePitchScale = 0.52f;
        private const int PredatorSpawnValidationHitCapacity = 64;
        private const int ApexSpawnGateCommandCount = 1;
        private const int ApexSpawnGateMaxHits = 4;
        private const float ApexSpawnGateCapsuleRadiusMeters = 2.5f;
        private const float ApexSpawnGateCapsuleHalfHeightMeters = 3f;
        private const float ApexSpawnGateSweepDistanceMeters = 0.25f;
        private const float ApexSpawnGateCacheCellSizeMeters = 10f;
        private const int HibernationPopulationSyncsPerColdSolve = 8;
        private const int ApexTerritoryOverlapCandidateCapacity = 16;
        private const int ApexTerritoryOverlapHitCapacity = 64;
        private const float ApexTerritoryOverlapQueryRadiusMeters = 1400f;
        private const float ApexTerritoryOverlapRetreatThreshold01 = 0.30f;
        private const int FloraPredatorAupBufferCapacity = 32;
        private const int FloraPredatorAupHitCapacity = 64;
        private const float FloraPredatorAupQueryRadiusMeters = 700f;
        private const float FloraPredatorStealthRadiusMeters = 15f;
        private const float FloraPredatorStealthDimStrength = 0.82f;
        private const string NativeMemoryOwner = nameof(EcosystemDirector);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        private static readonly string[] ThermalSpawnTokens = { "lava", "thermal", "brine", "heat", "volcanic", "smoker" };
        private static readonly string[] SharkSpawnTokens = { "shark", "hunter", "stalker" };
        private static readonly string[] ScavengerSpawnTokens = { "scavenger", "crab", "eel", "carrion", "cleaner" };
        private static readonly uint _FloraPredatorAupSaturationWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("EcosystemDirector.FloraPredatorAupSaturation"));
        private static readonly uint _EcosystemDirectorContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute(nameof(EcosystemDirector)));
        // COLD ALLOC: SpatialQueryHit[64] — non-alloc predator diet validation scratch for spawn gating — owner: EcosystemDirector
        private static readonly SpatialQueryHit[] _predatorSpawnValidationHits = new SpatialQueryHit[PredatorSpawnValidationHitCapacity];
        // COLD ALLOC: SpatialQueryHit[64] - non-alloc Apex territory candidate query scratch - owner: EcosystemDirector
        private static readonly SpatialQueryHit[] _apexTerritoryOverlapHits = new SpatialQueryHit[ApexTerritoryOverlapHitCapacity];
        // COLD ALLOC: SpatialQueryHit[64] - non-alloc flora predator AUP upload query scratch - owner: EcosystemDirector
        private static readonly SpatialQueryHit[] _floraPredatorAupHits = new SpatialQueryHit[FloraPredatorAupHitCapacity];
        private static readonly int _PredatorAUPBufferId = Shader.PropertyToID("_PredatorAUPBuffer");
        private static readonly int _PredatorAUPCountId = Shader.PropertyToID("_PredatorAUPCount");
        private static readonly int _PredatorAUPParamsId = Shader.PropertyToID("_PredatorAUPParams");
        private static readonly int _GlobalOceanPanicId = Shader.PropertyToID("_GlobalOceanPanic");
        private static readonly int _ApexInSectorId = Shader.PropertyToID("_ApexInSector");
        private static readonly int _GlobalOceanPanicColorId = Shader.PropertyToID("_GlobalOceanPanicColor");
        private static readonly int _BiolumFlashBangAUPId = Shader.PropertyToID("_BiolumFlashBangAUP");
        private static readonly int _BiolumFlashBangParamsId = Shader.PropertyToID("_BiolumFlashBangParams");

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]
        private struct SectorPopulationState
        {
            public int2 SectorCoord;
            public float PreyPopulation;
            public float PredatorPopulation;
            public float HarvestPressure;
            public float Fitness;
            public float SpeedMultiplier;
            public float CamouflageIndex;
            public float FoodDensity01;
            public float TemperatureScore01;
            public float Oxygen01;
            public float AlgaeBloom01;
            public int PreyPopulationRounded;
            public int PredatorPopulationRounded;
            public int BiomeId;
            public byte ApexInSector;
        }

        private struct HeadlessEntitySoA
        {
            public NativeArray<float3> Positions;
            public NativeArray<byte> SpeciesID;
            public NativeArray<byte> Hunger;
            public NativeArray<int2> SectorCoord;
            public NativeArray<int> SectorID;

            public bool IsCreated =>
                Positions.IsCreated &&
                SpeciesID.IsCreated &&
                Hunger.IsCreated &&
                SectorCoord.IsCreated &&
                SectorID.IsCreated;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]
        private struct ApexTerritorySample
        {
            public AbsoluteUniversePositionBlit128 PositionAup;
            public float Radius;
            public float MassScore;
            public int BrainIndex;
            public int Padding;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 16)]
        private struct ApexTerritoryOverlapResult
        {
            public int RetreatBrainIndex;
            public int RivalBrainIndex;
            public float Overlap01;
            public float Padding;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ApexTerritoryOverlapJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<ApexTerritorySample> Samples;
            public NativeArray<ApexTerritoryOverlapResult> Results;
            public int Count;
            public float OverlapThreshold01;

            public void Execute(int index)
            {
                ApexTerritoryOverlapResult result = default;
                result.RetreatBrainIndex = -1;
                result.RivalBrainIndex = -1;
                if (index < 0 || index >= Count)
                {
                    Results[index] = result;
                    return;
                }

                ApexTerritorySample sample = Samples[index];
                if (sample.Radius <= 0.001f)
                {
                    Results[index] = result;
                    return;
                }

                float bestOverlap01 = 0f;
                int bestRivalIndex = -1;
                for (int otherIndex = 0; otherIndex < Count; otherIndex++)
                {
                    if (otherIndex == index)
                        continue;

                    ApexTerritorySample rival = Samples[otherIndex];
                    if (rival.Radius <= 0.001f)
                        continue;

                    bool sampleIsSmaller =
                        sample.MassScore < rival.MassScore ||
                        (math.abs(sample.MassScore - rival.MassScore) <= 0.001f &&
                         (sample.Radius < rival.Radius ||
                          (math.abs(sample.Radius - rival.Radius) <= 0.001f && sample.BrainIndex > rival.BrainIndex)));
                    if (!sampleIsSmaller)
                        continue;

                    float overlap01 = ComputeSmallerSphereOverlap01(in sample.PositionAup, sample.Radius, in rival.PositionAup, rival.Radius);
                    if (overlap01 <= OverlapThreshold01 || overlap01 <= bestOverlap01)
                        continue;

                    bestOverlap01 = overlap01;
                    bestRivalIndex = rival.BrainIndex;
                }

                if (bestRivalIndex >= 0)
                {
                    result.RetreatBrainIndex = sample.BrainIndex;
                    result.RivalBrainIndex = bestRivalIndex;
                    result.Overlap01 = bestOverlap01;
                }

                Results[index] = result;
            }

            private static float ComputeSmallerSphereOverlap01(
                in AbsoluteUniversePositionBlit128 positionA,
                float radiusA,
                in AbsoluteUniversePositionBlit128 positionB,
                float radiusB)
            {
                float safeRadiusA = math.max(0.001f, radiusA);
                float safeRadiusB = math.max(0.001f, radiusB);
                float centerDistanceSq = ResolveAupDistanceSqClamped(in positionA, in positionB);
                float sumRadius = safeRadiusA + safeRadiusB;
                float sumRadiusSq = sumRadius * sumRadius;
                if (centerDistanceSq >= sumRadiusSq)
                    return 0f;

                float radiusDelta = math.abs(safeRadiusA - safeRadiusB);
                if (centerDistanceSq <= radiusDelta * radiusDelta)
                    return 1f;

                float smallerRadius = math.min(safeRadiusA, safeRadiusB);
                float largerRadius = math.max(safeRadiusA, safeRadiusB);
                float containmentBias = smallerRadius / largerRadius;
                float overlapPressure01 = math.saturate(1f - centerDistanceSq / math.max(0.001f, sumRadiusSq));
                return math.saturate(overlapPressure01 * (1f + containmentBias));
            }

            private static float ResolveAupDistanceSqClamped(
                in AbsoluteUniversePositionBlit128 positionA,
                in AbsoluteUniversePositionBlit128 positionB)
            {
                const double cellSizeMeters = AbsoluteUniversePosition.CellSizeMeters;
                double dx = ((positionA.GridX - positionB.GridX) * cellSizeMeters) + ((double)positionA.Local.x - positionB.Local.x);
                double dy = ((positionA.GridY - positionB.GridY) * cellSizeMeters) + ((double)positionA.Local.y - positionB.Local.y);
                double dz = ((positionA.GridZ - positionB.GridZ) * cellSizeMeters) + ((double)positionA.Local.z - positionB.Local.z);
                double distanceSq = dx * dx + dy * dy + dz * dz;
                return distanceSq >= float.MaxValue ? float.MaxValue : (float)math.max(0d, distanceSq);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct LotkaVolterraPopulationJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SectorPopulationState> FrontStates;
            [ReadOnly] public NativeArray<int> PreyCounts;
            [ReadOnly] public NativeArray<int> PredatorCounts;
            public NativeArray<SectorPopulationState> BackStates;
            public NativeArray<int> PreyBackCounts;
            public NativeArray<int> PredatorBackCounts;
            public NativeArray<float3> HeadlessPositions;
            public NativeArray<byte> HeadlessSpeciesID;
            public NativeArray<byte> HeadlessHunger;
            public NativeArray<int2> HeadlessSectorCoord;
            public NativeArray<int> HeadlessSectorID;
            public float DeltaSeconds;
            public float PreyBirthRate;
            public float PredationRate;
            public float PredatorGrowthRate;
            public float PredatorDeathRate;
            public float ReproductionFoodThreshold01;
            public int ReproductionPredatorThreshold;
            public int MutationBitMask;
            public int PreyCapacity;
            public int MaxPreyPopulation;
            public int MaxPredatorPopulation;
            public float MaximumSpeedMultiplier;
            public float StarvationComfortPreyPerPredator;

            public void Execute(int index)
            {
                SectorPopulationState state = FrontStates[index];
                float prey = math.max(0f, PreyCounts[index]);
                float predator = math.max(0f, PredatorCounts[index]);
                float foodDensity01 = math.saturate(state.FoodDensity01 - (math.saturate(state.HarvestPressure) * 0.35f) - (math.saturate(state.AlgaeBloom01) * 0.45f));
                float temperatureScore01 = state.TemperatureScore01;
                float oxygen01 = state.Oxygen01 <= 0f ? 1f : math.saturate(state.Oxygen01);
                float bloom01 = math.saturate(state.AlgaeBloom01);
                float dt = math.max(0f, DeltaSeconds);

                float dxdt = (PreyBirthRate * foodDensity01 * oxygen01 * prey) - (PredationRate * prey * predator);
                float dydt = (PredatorGrowthRate * prey * predator) - (PredatorDeathRate * (1.1f - foodDensity01) * predator);
                prey = math.clamp(prey + (dxdt * dt), 0f, math.max(0, MaxPreyPopulation));
                predator = math.clamp(predator + (dydt * dt), 0f, math.max(0, MaxPredatorPopulation));

                if (foodDensity01 > ReproductionFoodThreshold01 && predator < ReproductionPredatorThreshold)
                {
                    prey = math.min(math.max(0, MaxPreyPopulation), prey + 1f);
                    uint mutation = (uint)((state.SectorCoord.x * 31) ^ (state.SectorCoord.y * 17) ^ MutationBitMask);
                    float mutationSign = (mutation & 1u) == 0u ? -1f : 1f;
                    float mutationStep = ((mutation & 0x3u) + 1u) * 0.005f;
                    state.SpeedMultiplier = math.clamp(state.SpeedMultiplier + mutationSign * mutationStep, 1f, math.max(1f, MaximumSpeedMultiplier));
                    state.CamouflageIndex = math.saturate(state.CamouflageIndex + (((mutation >> 2) & 1u) == 0u ? -mutationStep : mutationStep));
                    state.Fitness = math.saturate(state.Fitness + 0.01f);
                }

                if (PreyCapacity > 0 && prey > PreyCapacity)
                {
                    bloom01 = math.saturate(bloom01 + 0.2f);
                    oxygen01 = math.saturate(oxygen01 - (0.18f * bloom01));
                }
                else
                {
                    bloom01 = math.saturate(bloom01 - 0.05f);
                    oxygen01 = math.saturate(oxygen01 + 0.03f);
                }

                if (oxygen01 < 0.35f)
                {
                    float dieOffScale = math.saturate(oxygen01 / 0.35f);
                    prey *= math.lerp(0.55f, 1f, dieOffScale);
                    predator *= math.lerp(0.7f, 1f, dieOffScale);
                }

                int preyPopulation = math.clamp((int)math.round(prey), 0, math.max(0, MaxPreyPopulation));
                int predatorPopulation = math.clamp((int)math.round(predator), 0, math.max(0, MaxPredatorPopulation));
                state.PreyPopulation = preyPopulation;
                state.PredatorPopulation = predatorPopulation;
                state.HarvestPressure = math.saturate(state.HarvestPressure * 0.65f);
                state.FoodDensity01 = foodDensity01;
                state.TemperatureScore01 = temperatureScore01;
                state.Oxygen01 = oxygen01;
                state.AlgaeBloom01 = bloom01;
                state.PreyPopulationRounded = preyPopulation;
                state.PredatorPopulationRounded = predatorPopulation;
                state.ApexInSector = predatorPopulation > 0 ? (byte)1 : (byte)0;
                BackStates[index] = state;
                PreyBackCounts[index] = preyPopulation;
                PredatorBackCounts[index] = predatorPopulation;

                HeadlessPositions[index] = ResolveSectorCenterPosition(state.SectorCoord);
                HeadlessSpeciesID[index] = predatorPopulation > preyPopulation ? (byte)2 : (byte)1;
                float preyPerPredator = predatorPopulation > 0
                    ? preyPopulation / math.max(1f, predatorPopulation)
                    : StarvationComfortPreyPerPredator;
                HeadlessHunger[index] = (byte)math.round(math.saturate(1f - preyPerPredator / math.max(1f, StarvationComfortPreyPerPredator)) * 255f);
                HeadlessSectorCoord[index] = state.SectorCoord;
                HeadlessSectorID[index] = ResolveSectorId(state.SectorCoord);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct HeadlessThresholdMigrationJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SectorPopulationState> States;
            public NativeArray<float3> Positions;
            public NativeArray<int2> SectorCoord;
            public NativeArray<int> SectorID;
            public float MigrationFoodThreshold01;
            public int MigrationPredatorTolerance;

            public void Execute(int index)
            {
                SectorPopulationState state = States[index];
                int population = math.max(0, state.PreyPopulationRounded + state.PredatorPopulationRounded);
                int2 coord = SectorCoord[index];
                if (population <= 0)
                {
                    Positions[index] = ResolveSectorCenterPosition(coord);
                    SectorID[index] = ResolveSectorId(coord);
                    return;
                }

                bool forcedMove =
                    state.FoodDensity01 < math.saturate(MigrationFoodThreshold01) ||
                    state.PredatorPopulationRounded > math.max(0, MigrationPredatorTolerance);
                if (forcedMove)
                    coord = ResolveBestFoodNeighbor(coord);

                SectorCoord[index] = coord;
                SectorID[index] = ResolveSectorId(coord);
                Positions[index] = ResolveSectorCenterPosition(coord);
            }

            private static int2 ResolveBestFoodNeighbor(int2 sectorCoord)
            {
                int2 bestCoord = sectorCoord;
                float bestFoodScore = ResolveMigrationFoodScore(sectorCoord);
                EvaluateFoodCandidate(sectorCoord + new int2(1, 0), ref bestCoord, ref bestFoodScore);
                EvaluateFoodCandidate(sectorCoord + new int2(-1, 0), ref bestCoord, ref bestFoodScore);
                EvaluateFoodCandidate(sectorCoord + new int2(0, 1), ref bestCoord, ref bestFoodScore);
                EvaluateFoodCandidate(sectorCoord + new int2(0, -1), ref bestCoord, ref bestFoodScore);
                return bestCoord;
            }

            private static void EvaluateFoodCandidate(
                int2 candidateCoord,
                ref int2 bestCoord,
                ref float bestFoodScore)
            {
                float foodScore = ResolveMigrationFoodScore(candidateCoord);
                if (foodScore > bestFoodScore + 0.0001f ||
                    (math.abs(foodScore - bestFoodScore) <= 0.0001f && ResolveSectorId(candidateCoord) < ResolveSectorId(bestCoord)))
                {
                    bestCoord = candidateCoord;
                    bestFoodScore = foodScore;
                }
            }

            private static float ResolveMigrationFoodScore(int2 sectorCoord)
            {
                int biomeId = ResolveBiomeIdForSector(sectorCoord);
                return ResolveSectorFoodDensity01(sectorCoord, biomeId, 0f, 0f);
            }
        }

        [Header("Sector Runtime")]
        [Tooltip("Maximum number of active 1 km sectors tracked in the cold-path population model.")]
        [SerializeField, Min(MinimumSectorCapacity)] private int maxTrackedSectors = 128;
        [Tooltip("Seconds between headless ecosystem solves. 5 seconds = FrostTick.")]
        [SerializeField, Min(1f)] private float coldTickIntervalSeconds = 5f;

        [Header("Probability Table")]
        [Tooltip("Deterministic 1 km sector roll chance that the sector presents one leviathan instead of a grazer pod.")]
        [SerializeField, Range(0f, 1f)] private float leviathanSectorSpawnChance = DefaultLeviathanSectorSpawnChance;
        [Tooltip("Fixed grazer count presented when the sector roll is not leviathan.")]
        [SerializeField, Min(0)] private int grazerPopulationPerSector = DefaultGrazerPopulationPerSector;
        [Tooltip("Fixed leviathan count presented when the sector roll hits the apex bucket.")]
        [SerializeField, Min(0)] private int leviathanPopulationPerSector = DefaultLeviathanPopulationPerSector;
        [Tooltip("Upper clamp for prey population values exposed to hibernation and spawn reconciliation.")]
        [SerializeField, Min(1)] private int maxPreyPopulation = DefaultGrazerPopulationPerSector;
        [Tooltip("Upper clamp for predator population values exposed to hibernation and spawn reconciliation.")]
        [SerializeField, Min(1)] private int maxPredatorPopulation = DefaultLeviathanPopulationPerSector;
        [Tooltip("Baseline prey fitness retained only for serialized compatibility; the purge table does not evolve it over time.")]
        [SerializeField, Range(0f, 1f)] private float baselineFitness = 0.08f;
        [Tooltip("Legacy save decode clamp for pre-purge sector adaptation payloads.")]
        [SerializeField, Min(1f)] private float maximumSpeedMultiplier = 1.35f;

        [Header("Lotka-Volterra Solver")]
        [SerializeField, Min(0f)] private float preyBirthRatePerSecond = 0.012f;
        [SerializeField, Min(0f)] private float predationRatePerSecond = 0.00045f;
        [SerializeField, Min(0f)] private float predatorGrowthRatePerSecond = 0.00014f;
        [SerializeField, Min(0f)] private float predatorDeathRatePerSecond = 0.006f;
        [SerializeField, Range(0f, 1f)] private float reproductionFoodThreshold01 = 0.62f;
        [SerializeField, Min(0)] private int reproductionPredatorThreshold = 2;
        [SerializeField] private int generationMutationBitMask = 0x2D5A;
        [SerializeField, Min(1)] private int preyPopulationCapacity = DefaultGrazerPopulationPerSector * 2;

        [Header("Threshold Migration")]
        [SerializeField, Range(0f, 1f)] private float migrationFoodThreshold01 = 0.38f;
        [SerializeField, Min(0)] private int migrationPredatorTolerance = 1;

        [Header("Spawn Budget")]
        [SerializeField, Min(0f)] private float spawnCreditBudgetMax = 24f;
        [SerializeField, Min(0f)] private float spawnCreditRecoverPerSecond = 2.5f;
        [SerializeField, Min(0f)] private float ambientSpawnCreditCost = 1f;
        [SerializeField, Min(0f)] private float predatorSpawnCreditCost = 4f;
        [SerializeField, Min(0f)] private float apexSpawnCreditCost = 9f;
        [SerializeField] private float _debugSpawnCreditBudget;
        [SerializeField] private float _debugPlayerStress01;
        [SerializeField] private int _debugHeadlessSectorCount;

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

        [Header("Eclipse Predator Migration")]
        [SerializeField, Min(0f)] private float eclipsePredatorTier0DepthMaxMeters = 40f;
        [SerializeField, Range(0f, 40f)] private float eclipsePredatorTier0TargetDepthMeters = 24f;
        [SerializeField, Range(0f, 1f)] private float eclipsePredatorLightSuppression = 1f;
        [SerializeField, Range(0f, 1f)] private float eclipsePredatorHostilityBoost = 0.35f;
        [SerializeField, Min(1f)] private float eclipsePredatorMigrationRadiusMeters = 1800f;
        [SerializeField, Min(1f)] private float eclipsePredatorMigrationStepMeters = 320f;
        [SerializeField, Min(0.5f)] private float eclipsePredatorMigrationIntervalSeconds = 2f;
        [SerializeField, Range(1f, 6f)] private float eclipsePredatorTier0SelectionBoost = 3f;
        [SerializeField] private float _debugEclipsePredatorMigrationTimeRemaining;
        [SerializeField] private float _debugEclipsePredatorMigrationIntensity;
        [SerializeField] private int _debugEclipsePredatorMigratedCount;

        [Header("Fauna Ecology Chains")]
        [Tooltip("Species IDs treated as herbivores for flora grazing and migration redirection.")]
        [SerializeField] private int[] herbivoreSpeciesIds;
        [Tooltip("Species IDs treated as cleaners that shadow apex fauna and reduce fatigue.")]
        [SerializeField] private int[] cleanerSpeciesIds;
        [Tooltip("Optional apex species IDs that accept cleaner symbiosis. Empty means any leviathan host.")]
        [SerializeField] private int[] cleanerHostSpeciesIds;
        [Tooltip("Hunger threshold that allows herbivore flora seeking to override default wander logic.")]
        [SerializeField, Range(0f, 1f)] private float herbivoreGrazeHungerThreshold = 0.68f;
        [Tooltip("Search radius used when resolving nearby consumable flora for hungry herbivores.")]
        [SerializeField, Min(1f)] private float herbivoreGrazeSearchRadiusMeters = 500f;
        [Tooltip("Distance where herbivores consume the resolved flora instance.")]
        [SerializeField, Min(0.1f)] private float herbivoreConsumeDistanceMeters = 6f;
        [Tooltip("Search radius used by cleaner species when acquiring an apex host to orbit.")]
        [SerializeField, Min(1f)] private float cleanerHostSearchRadiusMeters = 96f;
        [Tooltip("Distance where cleaner fish begin to apply fatigue relief to the host.")]
        [SerializeField, Min(0.1f)] private float cleanerSymbiosisDistanceMeters = 10f;
        [Tooltip("Normalized fatigue relief applied per second while a cleaner remains in symbiotic range.")]
        [SerializeField, Min(0f)] private float cleanerFatigueReliefPerSecond = 0.09f;
        [Tooltip("Hunger threshold that allows scavenger corpse feeding to override default hunt logic.")]
        [SerializeField, Range(0f, 1f)] private float scavengerHungerThreshold = 0.55f;
        [Tooltip("Search radius used when resolving active corpse-resource nodes for scavengers.")]
        [SerializeField, Min(1f)] private float scavengerCorpseSearchRadiusMeters = 240f;
        [Tooltip("Distance where scavengers begin consuming the corpse-resource node.")]
        [SerializeField, Min(0.1f)] private float scavengerConsumeDistanceMeters = 6f;
        [Tooltip("Units per second removed from a corpse-resource node while a scavenger is feeding.")]
        [SerializeField, Min(0.01f)] private float scavengerConsumeUnitsPerSecond = 1.2f;
        [Tooltip("Distance where dropped organic bait locks fauna into a local feeding investigate/sated loop.")]
        [SerializeField, Min(0.1f)] private float baitFeedingDistanceMeters = 5f;

        private NativeArray<SectorPopulationState> _sectorFrontStates;
        private NativeArray<SectorPopulationState> _sectorBackStates;
        private NativeArray<int> _preyFrontCounts;
        private NativeArray<int> _preyBackCounts;
        private NativeArray<int> _predatorFrontCounts;
        private NativeArray<int> _predatorBackCounts;
        private HeadlessEntitySoA _headlessEntities;
        private NativeHashMap<long, int> _sectorIndexByKey;
        private NativeArray<ApexTerritorySample> _apexTerritorySamples;
        private NativeArray<ApexTerritoryOverlapResult> _apexTerritoryOverlapResults;
        private NativeArray<CapsulecastCommand> _apexSpawnGateCommands;
        private NativeArray<RaycastHit> _apexSpawnGateHits;
        private NativeArray<float4> _floraPredatorAupUpload;
        private NativeList<EcosystemSectorSaveRecord> _saveSnapshotSectors;
        private FaunaBrain[] _apexTerritoryBrains;
        private GraphicsBuffer _floraPredatorAupBuffer;
        private JobHandle _scheduledSolveHandle;
        private JobHandle _scheduledApexTerritoryOverlapHandle;
        private JobHandle _apexSpawnGateHandle;
        private int3 _apexSpawnGatePendingCell;
        private int3 _apexSpawnGateCachedCell;
        private bool _apexSpawnGateScheduled;
        private bool _apexSpawnGateHasCachedResult;
        private byte _apexSpawnGateCachedBlocked;
        private float _coldTickAccumulator;
        private int _activeSectorCount;
        private int _scheduledApexTerritoryOverlapCount;
        private bool _registeredService;
        private bool _registeredSlowTickable;
        private bool _registeredLateFrameTickable;
        private bool _solveScheduled;
        private bool _apexTerritoryOverlapScheduled;
        private bool _populationSolvePendingHibernationSync;
        private float _biomeHostility01;
        private float _starvationAggressionPressure01;
        private float _playerStress01;
        private float _spawnCreditBudget;
        private int _hostilityTier;
        private bool _floraPredatorAupSaturationTelemetryIssued;
        private float _lastPublishedGlobalOceanPanic01 = -1f;
        private byte _lastPublishedApexInSector = byte.MaxValue;
        private int _lastPublishedFloraPredatorAupCount = -1;
        private bool _floraPredatorAupGlobalsDirty = true;
        private int _nextHibernationPopulationSyncIndex;
        private HectonMapMagicVegetationBridge _cachedVegetationBridge;
        private PersistentWorldRegistry _cachedPersistentWorldRegistry;
        private float _eclipsePredatorMigrationTimer;
        private float _eclipsePredatorMigrationIntensity01;
        private float _eclipsePredatorMigrationAccumulator;
        private Vector3 _activeWhaleFallAcousticPosition;
        private float _activeWhaleFallAcousticUntilTime;
        private uint _activeWhaleFallAcousticUid;

        /// <summary>
        /// True once the runtime-native state is allocated and registered.
        /// </summary>
        public bool IsInitialized =>
            _sectorFrontStates.IsCreated &&
            _sectorBackStates.IsCreated &&
            _preyFrontCounts.IsCreated &&
            _preyBackCounts.IsCreated &&
            _predatorFrontCounts.IsCreated &&
            _predatorBackCounts.IsCreated &&
            _headlessEntities.IsCreated &&
            _apexSpawnGateCommands.IsCreated &&
            _apexSpawnGateHits.IsCreated &&
            _sectorIndexByKey.IsCreated;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => IsServiceReady ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _registeredService && IsInitialized && ReferenceEquals(GlobalRegistry.EcosystemDirector, this);

        /// <summary>
        /// Normalized biome hostility score exposed to UI and pacing systems.
        /// </summary>
        public float BiomeHostility01 => ResolveCombinedHostility01();

        internal FaunaLogicalLodTier ResolveLogicalLodTier(Vector3 observerPosition, Vector3 faunaPosition)
        {
            AbsoluteUniversePosition observerAup = AbsoluteUniversePosition.FromRuntimePosition(observerPosition);
            AbsoluteUniversePosition faunaAup = AbsoluteUniversePosition.FromRuntimePosition(faunaPosition);
            return ResolveLogicalLodTier(in observerAup, in faunaAup);
        }

        internal FaunaLogicalLodTier ResolveLogicalLodTier(
            in AbsoluteUniversePosition observerAup,
            in AbsoluteUniversePosition faunaAup)
        {
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in observerAup, in faunaAup);

            if (distanceSq < (LogicalLodFullSimDistanceMeters * LogicalLodFullSimDistanceMeters))
                return FaunaLogicalLodTier.FullSim;

            if (distanceSq <= (LogicalLodDataOnlyDistanceMeters * LogicalLodDataOnlyDistanceMeters))
                return FaunaLogicalLodTier.DataOnly;

            return FaunaLogicalLodTier.Hibernating;
        }

        internal bool TryBuildEnvelope(Vector3 worldPosition, out EcosystemEnvelope envelope)
        {
            float depthMeters = 0f;
            MapMagicBridge mapMagicBridge = GlobalRegistry.MapMagic;
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
            float eclipseSuppression01 = ResolveEclipsePredatorLightSuppression01(worldPosition);
            if (eclipseSuppression01 > 0f)
                lightExposure01 *= 1f - eclipseSuppression01;

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

            if (IsApexRole(archetype) && !PassesApexSpawnVoxelGate(worldPosition))
                return false;

            if (IsPredatorOrApex(archetype) &&
                !CanSupportPredatorSpawn(archetype, worldPosition, PredatorDietValidationRadiusMeters))
            {
                return false;
            }

            TryBuildEnvelope(worldPosition, out EcosystemEnvelope envelope);
            float playerStress01 = _playerStress01;
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
                if (TryResolveEclipsePredatorTier0SelectionBoost(worldPosition, out float sharkEclipseSelectionBoost))
                    selectionMultiplier *= sharkEclipseSelectionBoost;

                selectionMultiplier *= ResolvePlayerStressSpawnWeight(archetype, playerStress01);
                selectionMultiplier *= ResolveSpawnCreditSelectionWeight(archetype);
                return selectionMultiplier > 0f;
            }

            if (archetype.isAggressive || archetype.roleType == CreatureRoleType.Hunter || archetype.roleType == CreatureRoleType.Leviathan)
            {
                selectionMultiplier = math.lerp(0.9f, 1.35f, scentPressure01);
            }

            selectionMultiplier *= ResolvePlayerStressSpawnWeight(archetype, playerStress01);

            if (IsPredatorOrApex(archetype) &&
                TryResolveEclipsePredatorTier0SelectionBoost(worldPosition, out float eclipseSelectionBoost))
            {
                selectionMultiplier *= eclipseSelectionBoost;
            }

            if (RespondsToCorpseFalls(archetype))
            {
                float corpseInfluence01 = ResolveCombinedCorpseSpawnInfluence01(worldPosition, CorpseSpawnInfluenceRadiusMeters);
                if (corpseInfluence01 > 0f)
                    selectionMultiplier *= math.lerp(1f, math.max(CorpseSpawnSelectionScale, WhaleFallScavengerSpawnMultiplier), corpseInfluence01);
            }

            selectionMultiplier *= ResolveSpawnCreditSelectionWeight(archetype);
            return selectionMultiplier > 0f;
        }

        internal bool TryConsumeSpawnCredit(CreatureArchetypeData archetype, bool isLargeThreat, bool isPredator)
        {
            float cost = ResolveSpawnCreditCost(archetype, isLargeThreat, isPredator);
            if (cost <= 0f)
                return true;

            if (_spawnCreditBudget + 0.0001f < cost)
                return false;

            _spawnCreditBudget = math.max(0f, _spawnCreditBudget - cost);
            _debugSpawnCreditBudget = _spawnCreditBudget;
            return true;
        }

        internal void RefundSpawnCredit(CreatureArchetypeData archetype, bool isLargeThreat, bool isPredator)
        {
            float cost = ResolveSpawnCreditCost(archetype, isLargeThreat, isPredator);
            if (cost <= 0f)
                return;

            _spawnCreditBudget = math.min(spawnCreditBudgetMax, _spawnCreditBudget + cost);
            _debugSpawnCreditBudget = _spawnCreditBudget;
        }

        private void UpdateSpawnCreditBudget(float deltaSeconds)
        {
            float stress01 = TryResolveDirectorPlayerStress01(out float resolvedStress01) ? resolvedStress01 : 0f;
            _playerStress01 = stress01;
            _debugPlayerStress01 = stress01;
            float recoveryScale = math.lerp(1.15f, 0.55f, stress01);
            _spawnCreditBudget = math.min(
                spawnCreditBudgetMax,
                _spawnCreditBudget + (spawnCreditRecoverPerSecond * math.max(0f, deltaSeconds) * recoveryScale));
            _debugSpawnCreditBudget = _spawnCreditBudget;
        }

        private float ResolveSpawnCreditSelectionWeight(CreatureArchetypeData archetype)
        {
            float cost = ResolveSpawnCreditCost(archetype, IsApexRole(archetype), IsPredatorOrApex(archetype));
            if (cost <= 0f)
                return 1f;

            return _spawnCreditBudget + 0.0001f >= cost ? 1f : 0f;
        }

        private float ResolveSpawnCreditCost(CreatureArchetypeData archetype, bool isLargeThreat, bool isPredator)
        {
            if (isLargeThreat || IsApexRole(archetype))
                return apexSpawnCreditCost;

            if (isPredator || IsPredatorOrApex(archetype))
                return predatorSpawnCreditCost;

            return ambientSpawnCreditCost;
        }

        private static float ResolvePlayerStressSpawnWeight(CreatureArchetypeData archetype, float playerStress01)
        {
            float stress01 = math.saturate(playerStress01);
            if (stress01 <= HighPlayerStressThreshold01)
                return 1f;

            float t = math.saturate((stress01 - HighPlayerStressThreshold01) / math.max(0.0001f, 1f - HighPlayerStressThreshold01));
            if (IsApexRole(archetype))
                return math.lerp(1f, 0.08f, t);

            if (IsPredatorOrApex(archetype))
                return math.lerp(1f, 0.3f, t);

            if (archetype != null && archetype.roleType == CreatureRoleType.Ambient)
                return math.lerp(1f, 1.45f, t);

            return 1f;
        }

        private static bool TryResolveDirectorPlayerStress01(out float stress01)
        {
            stress01 = 0f;
            bool resolved = false;

            HectonDirectorAI director = HectonDirectorAI.ActiveRuntimeInstance;
            if (director != null)
            {
                stress01 = math.max(stress01, math.saturate(director.CurrentStress01));
                resolved = true;
            }

            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null)
            {
                PlayerSurvivalRuntimeState survivalState = runtimeContext.SurvivalState;
                PlayerMovementRuntimeState movementState = runtimeContext.MovementState;
                if ((survivalState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasSurvival) != 0u)
                {
                    stress01 = math.max(stress01, math.saturate(1f - survivalState.OxygenNormalized));
                    stress01 = math.max(stress01, math.saturate(1f - survivalState.IntegrityNormalized));
                    stress01 = math.max(stress01, math.saturate(survivalState.PressureExposureSeverity01));
                    stress01 = math.max(stress01, math.saturate(survivalState.ThermalStressSeverity01));
                    resolved = true;
                }

                if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasMovement) != 0u)
                {
                    stress01 = math.max(stress01, math.saturate(movementState.UnderwaterStressIntensity01));
                    resolved = true;
                }
            }

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null)
            {
                HectonSurvivalSystem survivalSystem = playerContext.SurvivalSystem;
                if (survivalSystem != null)
                {
                    stress01 = math.max(stress01, math.saturate(1f - survivalSystem.OxygenNormalized));
                    stress01 = math.max(stress01, math.saturate(1f - survivalSystem.IntegrityNormalized));
                    stress01 = math.max(stress01, math.saturate(survivalSystem.PressureExposureSeverity01));
                    resolved = true;
                }

                HectonPlayerMovement movement = playerContext.PlayerMovement;
                if (movement != null)
                {
                    stress01 = math.max(stress01, math.saturate(movement.CurrentUnderwaterStressIntensity01));
                    resolved = true;
                }
            }

            stress01 = math.saturate(stress01);
            return resolved;
        }

        private bool PassesApexSpawnVoxelGate(Vector3 worldPosition)
        {
            if (!_apexSpawnGateCommands.IsCreated || !_apexSpawnGateHits.IsCreated)
                return false;

            if (_apexSpawnGateScheduled)
                CompleteApexSpawnGate(forceComplete: false);

            int3 gateCell = QuantizeApexSpawnGateCell(worldPosition);
            if (_apexSpawnGateHasCachedResult && math.all(gateCell == _apexSpawnGateCachedCell))
                return _apexSpawnGateCachedBlocked == 0;

            if (_apexSpawnGateScheduled)
                return false;

            for (int i = 0; i < _apexSpawnGateHits.Length; i++)
                _apexSpawnGateHits[i] = default;

            Vector3 capsuleOffset = Vector3.up * ApexSpawnGateCapsuleHalfHeightMeters;
            Vector3 point1 = worldPosition - capsuleOffset;
            Vector3 point2 = worldPosition + capsuleOffset;
            int collisionMask = HectonLayerMasks.TerrainLayerMask | HectonLayerMasks.VoxelCaveLayerMask;
            QueryParameters queryParameters = new QueryParameters(collisionMask, false, QueryTriggerInteraction.Ignore);
            _apexSpawnGateCommands[0] = new CapsulecastCommand(
                point1,
                point2,
                ApexSpawnGateCapsuleRadiusMeters,
                Vector3.up,
                queryParameters,
                ApexSpawnGateSweepDistanceMeters);

            _apexSpawnGatePendingCell = gateCell;
            _apexSpawnGateHandle = CapsulecastCommand.ScheduleBatch(
                _apexSpawnGateCommands,
                _apexSpawnGateHits,
                ApexSpawnGateCommandCount,
                ApexSpawnGateMaxHits,
                default);
            _apexSpawnGateScheduled = true;
            return false;
        }

        private void CompleteApexSpawnGate(bool forceComplete)
        {
            if (!_apexSpawnGateScheduled)
                return;

            if (!forceComplete && !_apexSpawnGateHandle.IsCompleted)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _apexSpawnGateHandle, forceComplete))
                return;

            byte blocked = 0;
            for (int i = 0; i < _apexSpawnGateHits.Length; i++)
            {
                if (_apexSpawnGateHits[i].collider != null)
                {
                    blocked = 1;
                    break;
                }
            }

            _apexSpawnGateCachedCell = _apexSpawnGatePendingCell;
            _apexSpawnGateCachedBlocked = blocked;
            _apexSpawnGateHasCachedResult = true;
            _apexSpawnGateScheduled = false;
            _apexSpawnGateHandle = default;
        }

        private static int3 QuantizeApexSpawnGateCell(Vector3 worldPosition)
        {
            float invCellSize = 1f / math.max(0.001f, ApexSpawnGateCacheCellSizeMeters);
            return new int3(
                (int)math.floor(worldPosition.x * invCellSize),
                (int)math.floor(worldPosition.y * invCellSize),
                (int)math.floor(worldPosition.z * invCellSize));
        }

        internal bool CanSupportPredatorSpawn(CreatureArchetypeData archetype, Vector3 worldPosition, float searchRadiusMeters)
        {
            if (archetype == null || !IsPredatorOrApex(archetype))
                return true;

            FaunaDataTemplate faunaDataTemplate = archetype.faunaDataTemplate;
            uint dietMaskBits = faunaDataTemplate != null ? faunaDataTemplate.DietMaskBits : 0u;
            if (dietMaskBits == 0u)
                return false;

            if ((dietMaskBits & (uint)FaunaDietMask.Carcass) != 0u &&
                ResolveCombinedCorpseSpawnInfluence01(worldPosition, CorpseSpawnInfluenceRadiusMeters) > MinimumCorpseDietInfluence01)
            {
                return true;
            }

            int hitCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                worldPosition,
                searchRadiusMeters,
                SpatialTargetKind.Bioform,
                _predatorSpawnValidationHits);

            for (int i = 0; i < hitCount; i++)
            {
                SpatialQueryHit hit = _predatorSpawnValidationHits[i];
                FaunaBrain preyBrain = hit.Owner as FaunaBrain;
                if (preyBrain == null &&
                    (hit.Transform == null || !hit.Transform.TryGetComponent(out preyBrain)))
                {
                    continue;
                }

                FaunaDataTemplate preyDataTemplate = preyBrain.DataTemplate;
                uint preyMaskBits = preyDataTemplate != null ? preyDataTemplate.PreyMaskBits : 0u;
                if (preyMaskBits != 0u && (dietMaskBits & preyMaskBits) != 0u)
                    return true;
            }

            return false;
        }

        internal bool IsHerbivoreSpecies(int speciesId)
        {
            return ContainsSpeciesId(herbivoreSpeciesIds, speciesId);
        }

        internal bool IsCleanerSpecies(int speciesId)
        {
            return ContainsSpeciesId(cleanerSpeciesIds, speciesId);
        }

        internal bool IsCleanerHostSpecies(FaunaBrain hostBrain)
        {
            if (hostBrain == null)
                return false;

            int speciesId = hostBrain.SpeciesId;
            if (cleanerHostSpeciesIds != null && cleanerHostSpeciesIds.Length > 0)
                return ContainsSpeciesId(cleanerHostSpeciesIds, speciesId);

            return hostBrain.SpeciesProfile != null && hostBrain.SpeciesProfile.isLeviathan;
        }

        internal float HerbivoreGrazeHungerThreshold => herbivoreGrazeHungerThreshold;
        internal float HerbivoreGrazeSearchRadiusMeters => herbivoreGrazeSearchRadiusMeters;
        internal float HerbivoreConsumeDistanceMeters => herbivoreConsumeDistanceMeters;
        internal float CleanerHostSearchRadiusMeters => cleanerHostSearchRadiusMeters;
        internal float CleanerSymbiosisDistanceMeters => cleanerSymbiosisDistanceMeters;
        internal float CleanerFatigueReliefPerSecond => cleanerFatigueReliefPerSecond;
        internal float ScavengerHungerThreshold => scavengerHungerThreshold;
        internal float ScavengerConsumeDistanceMeters => scavengerConsumeDistanceMeters;
        internal float ScavengerConsumeUnitsPerSecond => scavengerConsumeUnitsPerSecond;
        internal float BaitFeedingDistanceMeters => baitFeedingDistanceMeters;

        internal bool TryResolveHerbivoreGrazeTarget(Vector3 worldPosition, out Vector3 floraPosition, out uint floraInstanceUid)
        {
            floraPosition = default;
            floraInstanceUid = 0u;
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            return organicManager != null &&
                   organicManager.TryResolveNearestConsumableFlora(worldPosition, herbivoreGrazeSearchRadiusMeters, out floraPosition, out floraInstanceUid);
        }

        internal bool TryConsumeHerbivoreGrazeTarget(uint floraInstanceUid)
        {
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            return organicManager != null && organicManager.TryConsumeFlora(floraInstanceUid);
        }

        internal bool TryResolveMigrationTarget(int speciesId, Vector3 origin, out Vector3 target)
        {
            return MigrationDirector.TryResolveMigrationTarget(speciesId, origin, out target);
        }

        internal bool TryResolveNearestThermalVentAttractor(
            in AbsoluteUniversePosition queryAup,
            float searchRadiusMeters,
            out Vector3 target,
            out float heat01)
        {
            target = default;
            heat01 = 0f;
            AbyssalThermalManager thermalManager = GlobalRegistry.Thermodynamics;
            return thermalManager != null &&
                   thermalManager.TryResolveNearestActiveVentAttractor(in queryAup, searchRadiusMeters, out target, out heat01);
        }

        internal void RegisterCorpseResourceNode(Vector3 worldPosition, int speciesId, float capacityUnits)
        {
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            if (organicManager != null)
                organicManager.RegisterCorpseResourceNode(worldPosition, speciesId, capacityUnits);
        }

        internal void RegisterCorpseResourceNode(in AbsoluteUniversePosition positionAup, int speciesId, float capacityUnits)
        {
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            if (organicManager != null)
                organicManager.RegisterCorpseResourceNode(in positionAup, speciesId, capacityUnits);
        }

        internal bool TryResolveCorpseScavengeTarget(Vector3 worldPosition, out Vector3 corpsePosition, out uint corpseNodeId)
        {
            corpsePosition = default;
            corpseNodeId = 0u;
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            return organicManager != null &&
                   organicManager.TryResolveNearestCorpseResourceNode(worldPosition, scavengerCorpseSearchRadiusMeters, out corpsePosition, out corpseNodeId);
        }

        internal bool TryResolveCorpseScavengeTarget(in AbsoluteUniversePosition queryAup, out Vector3 corpsePosition, out uint corpseNodeId)
        {
            corpsePosition = default;
            corpseNodeId = 0u;
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            return organicManager != null &&
                   organicManager.TryResolveNearestCorpseResourceNode(in queryAup, scavengerCorpseSearchRadiusMeters, out corpsePosition, out corpseNodeId);
        }

        internal bool TryConsumeCorpseScavengeTarget(uint corpseNodeId, float consumeUnits)
        {
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            return organicManager != null && organicManager.TryConsumeCorpseResourceNode(corpseNodeId, consumeUnits);
        }

        internal bool TryResolveCorpseDiseaseExposure(
            in AbsoluteUniversePosition queryAup,
            float currentTimeSeconds,
            out float severity01,
            out Vector3 sourcePosition)
        {
            severity01 = 0f;
            sourcePosition = default;
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            return organicManager != null &&
                   organicManager.TryResolveCorpseDiseaseExposure(in queryAup, currentTimeSeconds, out severity01, out sourcePosition);
        }

        internal bool TryResolveNearestOrganicMass(Vector3 worldPosition, out Vector3 organicPosition)
        {
            AbsoluteUniversePosition queryAup = AbsoluteUniversePosition.FromRuntimePosition(worldPosition);
            return TryResolveNearestOrganicMass(in queryAup, out organicPosition);
        }

        internal bool TryResolveNearestOrganicMass(in AbsoluteUniversePosition queryAup, out Vector3 organicPosition)
        {
            organicPosition = default;
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            if (organicManager == null)
                return false;

            Vector3 worldPosition = queryAup.ToRuntimeFloat3();
            float searchRadius = math.max(scavengerCorpseSearchRadiusMeters, herbivoreGrazeSearchRadiusMeters);
            bool found = false;
            double bestDistanceSq = double.MaxValue;
            if (organicManager.TryResolveNearestCorpseResourceNode(in queryAup, searchRadius, out Vector3 corpsePosition, out _))
            {
                organicPosition = corpsePosition;
                bestDistanceSq = ResolveRuntimeAupDistanceSq(in queryAup, corpsePosition);
                found = true;
            }

            if (organicManager.TryResolveNearestConsumableFlora(worldPosition, searchRadius, out Vector3 floraPosition, out _))
            {
                double floraDistanceSq = ResolveRuntimeAupDistanceSq(in queryAup, floraPosition);
                if (floraDistanceSq < bestDistanceSq)
                {
                    organicPosition = floraPosition;
                    bestDistanceSq = floraDistanceSq;
                    found = true;
                }
            }

            return found;
        }

        internal bool TryConsumeOrganicMassAtPosition(Vector3 worldPosition, float searchRadius)
        {
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            if (organicManager == null)
                return false;

            float safeSearchRadius = math.max(0.1f, searchRadius);
            if (organicManager.TryConsumeFloraAtPosition(worldPosition, safeSearchRadius, out _))
                return true;

            if (!organicManager.TryResolveNearestCorpseResourceNode(worldPosition, safeSearchRadius, out _, out uint corpseNodeId))
                return false;

            return organicManager.TryConsumeCorpseResourceNode(corpseNodeId, scavengerConsumeUnitsPerSecond);
        }

        internal void PublishBiolumFlashBang(in AbsoluteUniversePosition flashAup, float currentTimeSeconds, float radiusMeters = 42f)
        {
            Vector3 flashPosition = flashAup.ToRuntimeFloat3();
            Shader.SetGlobalVector(_BiolumFlashBangAUPId, new Vector4(flashPosition.x, flashPosition.y, flashPosition.z, math.max(0.1f, radiusMeters)));
            Shader.SetGlobalVector(_BiolumFlashBangParamsId, new Vector4(currentTimeSeconds, 0.1f, 4f, 0f));
        }

        internal bool DoesSpeciesRespondToBait(FaunaBrain faunaBrain)
        {
            if (faunaBrain == null || faunaBrain.SpeciesProfile == null)
                return false;

            int speciesId = faunaBrain.SpeciesId;
            return faunaBrain.SpeciesProfile.isScavenger ||
                   IsHerbivoreSpecies(speciesId) ||
                   (faunaBrain.isAggressive && !faunaBrain.SpeciesProfile.isLeviathan);
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
            if (_cachedPersistentWorldRegistry != null)
            {
                AbsoluteUniversePosition whaleFallAup = AbsoluteUniversePosition.FromRuntimePosition(worldPosition);
                _cachedPersistentWorldRegistry.TryCacheWhaleFallPoiState(uniqueInstanceUid, unchecked((int)(uniqueInstanceUid & 0x00FFFFFFu)), in whaleFallAup, Time.time);
            }

            MigrationDirector.RegisterPredatorKillPoi(uniqueInstanceUid, worldPosition, Time.time);
            _activeWhaleFallAcousticPosition = worldPosition;
            _activeWhaleFallAcousticUid = uniqueInstanceUid;
            _activeWhaleFallAcousticUntilTime = Time.time + WhaleFallAcousticImpulseLifetimeSeconds;
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
            SyncPendingHibernatedFaunaPopulationRecords();
            for (int sectorIndex = 0; sectorIndex < _activeSectorCount; sectorIndex++)
            {
                SectorPopulationState state = _sectorFrontStates[sectorIndex];
                _saveSnapshotSectors.AddNoResize(new EcosystemSectorSaveRecord
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

            ClearHeadlessRuntimeState();

            int recordCount = loadedRecords != null ? math.min(loadedRecords.Length, _sectorFrontStates.Length) : 0;
            _activeSectorCount = 0;
            for (int sectorIndex = 0; sectorIndex < recordCount; sectorIndex++)
            {
                EcosystemSectorSaveRecord saveRecord = loadedRecords[sectorIndex];
                int biomeId = ResolveBiomeIdForSector(saveRecord.SectorCoord);
                ResolveProbabilityTablePopulation(
                    saveRecord.SectorCoord,
                    biomeId,
                    leviathanSectorSpawnChance,
                    grazerPopulationPerSector,
                    leviathanPopulationPerSector,
                    out int preyPopulation,
                    out int predatorPopulation);
                SectorPopulationState restoredState = new SectorPopulationState
                {
                    SectorCoord = saveRecord.SectorCoord,
                    PreyPopulation = preyPopulation,
                    PredatorPopulation = predatorPopulation,
                    HarvestPressure = 0f,
                    Fitness = baselineFitness,
                    SpeedMultiplier = 1f,
                    CamouflageIndex = 0f,
                    FoodDensity01 = ResolveSectorFoodDensity01(saveRecord.SectorCoord, biomeId, 0f, 0f),
                    TemperatureScore01 = ResolveSectorTemperatureScore01(saveRecord.SectorCoord, biomeId),
                    Oxygen01 = 1f,
                    AlgaeBloom01 = 0f,
                    PreyPopulationRounded = preyPopulation,
                    PredatorPopulationRounded = predatorPopulation,
                    BiomeId = biomeId,
                    ApexInSector = predatorPopulation > 0 ? (byte)1 : (byte)0
                };

                _sectorFrontStates[sectorIndex] = restoredState;
                _sectorBackStates[sectorIndex] = restoredState;
                WriteHeadlessSlot(sectorIndex, in restoredState);
                _sectorIndexByKey.TryAdd(PackSectorKey(saveRecord.SectorCoord), sectorIndex);
                _activeSectorCount++;
            }
        }

        private void Awake()
        {
            ActiveRuntimeInstance = this;
            SanitizeSettings();
            AllocateRuntimeState();
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;
            SanitizeSettings();
            AllocateRuntimeState();
            TryRegisterService();
            TryRegisterSlowTickable();
            TryRegisterLateFrameTickable();
        }

        private void OnDisable()
        {
            ShutdownServiceState();
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            if (ActiveRuntimeInstance == this)
                ActiveRuntimeInstance = null;

            TryUnregisterSlowTickable();
            TryUnregisterLateFrameTickable();
            TryUnregisterService();
            DisposeRuntimeState();
        }

        /// <summary>
        /// Explicit bootstrap registration pass for headless/data-only simulation.
        /// </summary>
        internal void InitializeService()
        {
            ActiveRuntimeInstance = this;
            SanitizeSettings();
            AllocateRuntimeState();
            TryRegisterService();
            TryRegisterSlowTickable();
            TryRegisterLateFrameTickable();
        }

        /// <summary>
        /// Advances the sector population solve at 0.1 Hz using a Burst job.
        /// </summary>
        public void SlowTick()
        {
            if (!IsInitialized)
                return;

            DecayBiomeHostility();
            UpdateSpawnCreditBudget(DefaultSlowTickIntervalSeconds);
            SyncPendingHibernatedFaunaPopulationRecords();
            EnsurePlayerSectorRegistered();
            TickEclipsePredatorShallowMigration(DefaultSlowTickIntervalSeconds);
            if (TryResolvePlayerRuntimePosition(out Vector3 playerPosition))
            {
                PublishFloraPredatorAupBuffer(playerPosition);
                ScheduleApexTerritoryOverlap(playerPosition);
                EmitWhaleFallAcousticImpulseSlowTick();
            }
            else
            {
                PublishFloraPredatorAupGlobals(0);
                PublishApexPresenceFake(false);
            }

            _coldTickAccumulator += DefaultSlowTickIntervalSeconds;
            if (_coldTickAccumulator >= coldTickIntervalSeconds)
            {
                if (HasPendingSimulationJob())
                    return;

                _coldTickAccumulator -= coldTickIntervalSeconds;
                SyncPendingHibernatedFaunaPopulationRecords();
                ScheduleSectorSolve();
            }
        }

        private void EmitWhaleFallAcousticImpulseSlowTick()
        {
            if (_activeWhaleFallAcousticUid == 0u || Time.time > _activeWhaleFallAcousticUntilTime)
                return;

            AcousticImpulseEvent impulseEvent = new AcousticImpulseEvent(
                _activeWhaleFallAcousticPosition,
                Vector3.down,
                WhaleFallAcousticImpulseEnergyJoules,
                WhaleFallAcousticImpulseVolume01,
                WhaleFallAcousticImpulsePitchScale,
                math.max(CorpseSpawnInfluenceRadiusMeters, scavengerCorpseSearchRadiusMeters),
                unchecked((int)(_activeWhaleFallAcousticUid & 0x7FFFFFFFu)),
                0,
                AcousticImpulseFlags.Leviathan);
            PhysicsEventBus.NotifyAcousticImpulse(in impulseEvent);
        }

        public void LateFrameTick()
        {
            CompleteScheduledSimulation(forceComplete: false);
            CompleteScheduledApexTerritoryOverlap(forceComplete: false);
            CompleteApexSpawnGate(forceComplete: false);
            FaunaSpatialHashRegistry.RunDeferredCleanupFrame();
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
            sample.ApexInSector = IsApexInSectorState(in state);
            return true;
        }

        /// <summary>
        /// Returns the sector-level apex presence flag used by presentation and audio fakes.
        /// </summary>
        public bool IsApexInSector(Vector3 worldPosition)
        {
            if (!IsInitialized || HasPendingSimulationJob())
                return false;

            int2 sectorCoord = QuantizeSector(worldPosition);
            if (!_sectorIndexByKey.TryGetValue(PackSectorKey(sectorCoord), out int slotIndex) || slotIndex < 0 || slotIndex >= _activeSectorCount)
                return false;

            SectorPopulationState state = _sectorFrontStates[slotIndex];
            return IsApexInSectorState(in state);
        }

        /// <summary>
        /// Registers visible prey consumption for strain only. Sector population is fixed by the cinematic table.
        /// </summary>
        public void ReportPredation(Vector3 worldPosition, int preyConsumed)
        {
            if (!IsInitialized || preyConsumed <= 0)
                return;

            GlobalRegistry.EnvironmentalStrain?.AccumulatePredationStrain(worldPosition, preyConsumed);
            if (HasPendingSimulationJob())
                return;

            int slotIndex = ResolveOrCreateSectorSlot(QuantizeSector(worldPosition), seedWithBaseline: true);
            if (slotIndex < 0)
                return;

            SectorPopulationState state = _sectorFrontStates[slotIndex];
            state.PreyPopulationRounded = math.max(0, state.PreyPopulationRounded - preyConsumed);
            state.PreyPopulation = state.PreyPopulationRounded;
            state.HarvestPressure = math.saturate(state.HarvestPressure + preyConsumed / math.max(1f, maxPreyPopulation));
            _sectorFrontStates[slotIndex] = state;
            _sectorBackStates[slotIndex] = state;
            WriteHeadlessSlot(slotIndex, in state);
        }

        /// <summary>
        /// Applies one herbivore flora-consumption event at the supplied world position and mirrors it into the sector prey-pressure solve.
        /// </summary>
        internal bool TryReportFloraGrazing(Vector3 worldPosition, float searchRadiusMeters = DefaultFloraGrazingSearchRadiusMeters)
        {
            if (!IsInitialized)
                return false;

            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            if (organicManager == null ||
                !organicManager.TryConsumeFloraAtPosition(worldPosition, math.max(0.5f, searchRadiusMeters), out _))
            {
                return false;
            }

            ReportPredation(worldPosition, 1);
            return true;
        }

        private void ApplyApexKillPopulationShock(Vector3 worldPosition)
        {
            if (!IsInitialized || HasPendingSimulationJob())
                return;

            int slotIndex = ResolveOrCreateSectorSlot(QuantizeSector(worldPosition), seedWithBaseline: true);
            if (slotIndex < 0)
                return;

            SectorPopulationState state = _sectorFrontStates[slotIndex];
            state.PredatorPopulationRounded = math.max(0, state.PredatorPopulationRounded - 1);
            state.PredatorPopulation = state.PredatorPopulationRounded;
            state.ApexInSector = state.PredatorPopulationRounded > 0 ? (byte)1 : (byte)0;
            int preyBloom = math.max(1, maxPreyPopulation / 5);
            state.PreyPopulationRounded = math.min(maxPreyPopulation, state.PreyPopulationRounded + preyBloom);
            state.PreyPopulation = state.PreyPopulationRounded;
            state.HarvestPressure = math.saturate(state.HarvestPressure + 0.15f);
            _sectorFrontStates[slotIndex] = state;
            _sectorBackStates[slotIndex] = state;
            WriteHeadlessSlot(slotIndex, in state);
        }

        /// <summary>
        /// Registers one player-attributed apex predator kill and escalates biome hostility.
        /// </summary>
        public void ReportApexPredatorKilled(Vector3 worldPosition, float hostilityDelta)
        {
            if (!IsInitialized)
                return;

            float appliedDelta = math.max(hostilityPerApexKill, hostilityDelta);
            SetBiomeHostility(_biomeHostility01 + appliedDelta);
            ApplyApexKillPopulationShock(worldPosition);
            ApplyDirectorHostilityPressure();
        }

        /// <summary>
        /// Opens a cold-path eclipse migration window for large predators and clears it when intensity or hold time reaches zero.
        /// </summary>
        public void ApplyEclipsePredatorShallowMigration(float intensity01, float holdSeconds)
        {
            float clampedIntensity = math.saturate(intensity01);
            if (clampedIntensity <= 0f || holdSeconds <= 0f)
            {
                _eclipsePredatorMigrationTimer = 0f;
                _eclipsePredatorMigrationIntensity01 = 0f;
                _eclipsePredatorMigrationAccumulator = 0f;
                _debugEclipsePredatorMigrationTimeRemaining = 0f;
                _debugEclipsePredatorMigrationIntensity = 0f;
                return;
            }

            _eclipsePredatorMigrationTimer = math.max(_eclipsePredatorMigrationTimer, holdSeconds);
            _eclipsePredatorMigrationIntensity01 = math.max(_eclipsePredatorMigrationIntensity01, clampedIntensity);
            _eclipsePredatorMigrationAccumulator = math.max(
                _eclipsePredatorMigrationAccumulator,
                eclipsePredatorMigrationIntervalSeconds);
            _debugEclipsePredatorMigrationTimeRemaining = _eclipsePredatorMigrationTimer;
            _debugEclipsePredatorMigrationIntensity = _eclipsePredatorMigrationIntensity01;

            SetBiomeHostility(math.max(_biomeHostility01, clampedIntensity * eclipsePredatorHostilityBoost));
            ApplyDirectorHostilityPressure();
        }

        /// <summary>
        /// Suppresses predator light reaction in the upper forty meters during eclipse migration.
        /// </summary>
        public float ResolveEclipsePredatorLightSuppression01(Vector3 worldPosition)
        {
            if (_eclipsePredatorMigrationTimer <= 0f || _eclipsePredatorMigrationIntensity01 <= 0f)
                return 0f;

            float depthMeters = ResolveDepthMeters(worldPosition);
            if (depthMeters > eclipsePredatorTier0DepthMaxMeters)
                return 0f;

            return math.saturate(math.lerp(
                _eclipsePredatorMigrationIntensity01,
                1f,
                eclipsePredatorLightSuppression));
        }

        private void SanitizeSettings()
        {
            maxTrackedSectors = math.max(MinimumSectorCapacity, maxTrackedSectors);
            coldTickIntervalSeconds = math.max(1f, coldTickIntervalSeconds);
            leviathanSectorSpawnChance = math.saturate(leviathanSectorSpawnChance);
            grazerPopulationPerSector = math.max(0, grazerPopulationPerSector);
            leviathanPopulationPerSector = math.max(0, leviathanPopulationPerSector);
            maxPreyPopulation = math.max(math.max(1, grazerPopulationPerSector), maxPreyPopulation);
            maxPredatorPopulation = math.max(math.max(1, leviathanPopulationPerSector), maxPredatorPopulation);
            baselineFitness = math.clamp(baselineFitness, 0f, 1f);
            maximumSpeedMultiplier = math.max(1f, maximumSpeedMultiplier);
            preyBirthRatePerSecond = math.max(0f, preyBirthRatePerSecond);
            predationRatePerSecond = math.max(0f, predationRatePerSecond);
            predatorGrowthRatePerSecond = math.max(0f, predatorGrowthRatePerSecond);
            predatorDeathRatePerSecond = math.max(0f, predatorDeathRatePerSecond);
            reproductionFoodThreshold01 = math.clamp(reproductionFoodThreshold01, 0f, 1f);
            reproductionPredatorThreshold = math.max(0, reproductionPredatorThreshold);
            if (generationMutationBitMask == 0)
                generationMutationBitMask = 0x2D5A;
            preyPopulationCapacity = math.max(1, preyPopulationCapacity);
            migrationFoodThreshold01 = math.clamp(migrationFoodThreshold01, 0f, 1f);
            migrationPredatorTolerance = math.max(0, migrationPredatorTolerance);
            spawnCreditBudgetMax = math.max(0f, spawnCreditBudgetMax);
            spawnCreditRecoverPerSecond = math.max(0f, spawnCreditRecoverPerSecond);
            ambientSpawnCreditCost = math.max(0f, ambientSpawnCreditCost);
            predatorSpawnCreditCost = math.max(0f, predatorSpawnCreditCost);
            apexSpawnCreditCost = math.max(predatorSpawnCreditCost, apexSpawnCreditCost);
            hostilityPerApexKill = math.clamp(hostilityPerApexKill, 0.01f, 1f);
            hostilityDecayPerSlowTick = math.clamp(hostilityDecayPerSlowTick, 0.001f, 0.2f);
            hostilityPeakHoldSeconds = math.max(0f, hostilityPeakHoldSeconds);
            starvationComfortPreyPerPredator = math.max(1f, starvationComfortPreyPerPredator);
            starvationHarvestWeight = math.max(0f, starvationHarvestWeight);
            starvationHostilityWeight = math.clamp(starvationHostilityWeight, 0f, 1f);
            eclipsePredatorTier0DepthMaxMeters = math.max(0f, eclipsePredatorTier0DepthMaxMeters);
            eclipsePredatorTier0TargetDepthMeters = math.clamp(
                eclipsePredatorTier0TargetDepthMeters,
                0f,
                math.max(0f, eclipsePredatorTier0DepthMaxMeters));
            eclipsePredatorLightSuppression = math.clamp(eclipsePredatorLightSuppression, 0f, 1f);
            eclipsePredatorHostilityBoost = math.clamp(eclipsePredatorHostilityBoost, 0f, 1f);
            eclipsePredatorMigrationRadiusMeters = math.max(1f, eclipsePredatorMigrationRadiusMeters);
            eclipsePredatorMigrationStepMeters = math.max(1f, eclipsePredatorMigrationStepMeters);
            eclipsePredatorMigrationIntervalSeconds = math.max(0.5f, eclipsePredatorMigrationIntervalSeconds);
            eclipsePredatorTier0SelectionBoost = math.max(1f, eclipsePredatorTier0SelectionBoost);
            herbivoreGrazeHungerThreshold = math.clamp(herbivoreGrazeHungerThreshold, 0f, 1f);
            herbivoreGrazeSearchRadiusMeters = math.max(1f, herbivoreGrazeSearchRadiusMeters);
            herbivoreConsumeDistanceMeters = math.max(0.1f, herbivoreConsumeDistanceMeters);
            cleanerHostSearchRadiusMeters = math.max(1f, cleanerHostSearchRadiusMeters);
            cleanerSymbiosisDistanceMeters = math.max(0.1f, cleanerSymbiosisDistanceMeters);
            cleanerFatigueReliefPerSecond = math.max(0f, cleanerFatigueReliefPerSecond);
            scavengerHungerThreshold = math.clamp(scavengerHungerThreshold, 0f, 1f);
            scavengerCorpseSearchRadiusMeters = math.max(1f, scavengerCorpseSearchRadiusMeters);
            scavengerConsumeDistanceMeters = math.max(0.1f, scavengerConsumeDistanceMeters);
            scavengerConsumeUnitsPerSecond = math.max(0.01f, scavengerConsumeUnitsPerSecond);
            baitFeedingDistanceMeters = math.max(0.1f, baitFeedingDistanceMeters);
        }

        private void ResolveRuntimeReferences()
        {
            _cachedVegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (_cachedPersistentWorldRegistry == null)
                _cachedPersistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
        }

        private static bool RequiresThermalEnvelope(CreatureArchetypeData archetype)
        {
            return MatchesAnyToken(archetype.creatureId, ThermalSpawnTokens) ||
                   MatchesAnyToken(archetype.displayName, ThermalSpawnTokens) ||
                   MatchesAnyToken(archetype.gameplayPurpose, ThermalSpawnTokens) ||
                   MatchesAnyToken(archetype.behaviorTreeHint, ThermalSpawnTokens);
        }

        private static bool IsPredatorOrApex(CreatureArchetypeData archetype)
        {
            if (archetype == null)
                return false;

            return archetype.isAggressive ||
                   archetype.roleType == CreatureRoleType.Hunter ||
                   archetype.roleType == CreatureRoleType.Leviathan;
        }

        private static bool IsApexRole(CreatureArchetypeData archetype)
        {
            return archetype != null && archetype.roleType == CreatureRoleType.Leviathan;
        }

        private static bool IsSharkLikePredator(CreatureArchetypeData archetype)
        {
            if (archetype == null)
                return false;

            return MatchesAnyToken(archetype.creatureId, SharkSpawnTokens) ||
                   MatchesAnyToken(archetype.displayName, SharkSpawnTokens) ||
                   (archetype.isAggressive && archetype.roleType == CreatureRoleType.Hunter);
        }

        private static bool RespondsToCorpseFalls(CreatureArchetypeData archetype)
        {
            if (archetype == null)
                return false;

            FaunaDataTemplate faunaDataTemplate = archetype.faunaDataTemplate;
            if (faunaDataTemplate != null &&
                (faunaDataTemplate.DietMaskBits & (uint)FaunaDietMask.Carcass) != 0u)
            {
                return true;
            }

            return MatchesAnyToken(archetype.creatureId, ScavengerSpawnTokens) ||
                   MatchesAnyToken(archetype.displayName, ScavengerSpawnTokens) ||
                   MatchesAnyToken(archetype.gameplayPurpose, ScavengerSpawnTokens) ||
                   MatchesAnyToken(archetype.behaviorTreeHint, ScavengerSpawnTokens);
        }

        private static float ResolveCorpseSpawnInfluence01(Vector3 worldPosition, float radiusMeters)
        {
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            return organicManager != null
                ? organicManager.ResolveCorpseSpawnInfluence01(worldPosition, radiusMeters)
                : 0f;
        }

        private static float ResolveCombinedCorpseSpawnInfluence01(Vector3 worldPosition, float radiusMeters)
        {
            float liveCorpseInfluence01 = ResolveCorpseSpawnInfluence01(worldPosition, radiusMeters);
            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            float persistentWhaleFallInfluence01 = registry != null
                ? registry.ResolveWhaleFallSpawnInfluence01(worldPosition, Time.time, radiusMeters)
                : 0f;
            return math.max(liveCorpseInfluence01, persistentWhaleFallInfluence01);
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

        private static bool ContainsSpeciesId(int[] speciesIds, int speciesId)
        {
            if (speciesId == 0 || speciesIds == null)
                return false;

            for (int i = 0; i < speciesIds.Length; i++)
            {
                if (speciesIds[i] == speciesId)
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
            float pressure01 = 0f;
            int count = math.min(_activeSectorCount, _sectorFrontStates.IsCreated ? _sectorFrontStates.Length : 0);
            for (int i = 0; i < count; i++)
            {
                SectorPopulationState state = _sectorFrontStates[i];
                if (state.PredatorPopulationRounded <= 0)
                {
                    pressure01 = math.max(pressure01, state.AlgaeBloom01 * 0.35f);
                    continue;
                }

                float preyPerPredator = state.PreyPopulationRounded / math.max(1f, state.PredatorPopulationRounded);
                float starvation01 = math.saturate(1f - (preyPerPredator / math.max(1f, starvationComfortPreyPerPredator)));
                float harvest01 = math.saturate(state.HarvestPressure * starvationHarvestWeight);
                float oxygenCollapse01 = math.saturate(1f - state.Oxygen01);
                pressure01 = math.max(pressure01, math.saturate(starvation01 + harvest01 + (oxygenCollapse01 * 0.25f)));
            }

            _starvationAggressionPressure01 = math.saturate(pressure01 * starvationHostilityWeight);
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
            // COLD ALLOC: NativeArray<int>[maxTrackedSectors] - Lotka-Volterra prey input front buffer - owner: EcosystemDirector
            _preyFrontCounts = new NativeArray<int>(maxTrackedSectors, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<int>[maxTrackedSectors] - Lotka-Volterra prey output back buffer - owner: EcosystemDirector
            _preyBackCounts = new NativeArray<int>(maxTrackedSectors, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<int>[maxTrackedSectors] - Lotka-Volterra predator input front buffer - owner: EcosystemDirector
            _predatorFrontCounts = new NativeArray<int>(maxTrackedSectors, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<int>[maxTrackedSectors] - Lotka-Volterra predator output back buffer - owner: EcosystemDirector
            _predatorBackCounts = new NativeArray<int>(maxTrackedSectors, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _headlessEntities = new HeadlessEntitySoA
            {
                Positions = new NativeArray<float3>(maxTrackedSectors, Allocator.Persistent, NativeArrayOptions.ClearMemory), // COLD ALLOC: headless SOA positions - owner: EcosystemDirector
                SpeciesID = new NativeArray<byte>(maxTrackedSectors, Allocator.Persistent, NativeArrayOptions.ClearMemory), // COLD ALLOC: headless SOA species IDs - owner: EcosystemDirector
                Hunger = new NativeArray<byte>(maxTrackedSectors, Allocator.Persistent, NativeArrayOptions.ClearMemory), // COLD ALLOC: headless SOA hunger bytes - owner: EcosystemDirector
                SectorCoord = new NativeArray<int2>(maxTrackedSectors, Allocator.Persistent, NativeArrayOptions.ClearMemory), // COLD ALLOC: headless SOA threshold-migration sector coordinates - owner: EcosystemDirector
                SectorID = new NativeArray<int>(maxTrackedSectors, Allocator.Persistent, NativeArrayOptions.ClearMemory) // COLD ALLOC: headless SOA threshold-migration sector IDs - owner: EcosystemDirector
            };
            // COLD ALLOC: NativeHashMap<long,int>[maxTrackedSectors] - packed sector-key to slot lookup for O(1) cold-path classification - owner: EcosystemDirector
            _sectorIndexByKey = new NativeHashMap<long, int>(maxTrackedSectors, Allocator.Persistent);
            // COLD ALLOC: NativeArray<ApexTerritorySample>[16] - active Apex territory overlap job inputs - owner: EcosystemDirector
            _apexTerritorySamples = new NativeArray<ApexTerritorySample>(ApexTerritoryOverlapCandidateCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<ApexTerritoryOverlapResult>[16] - active Apex territory overlap job outputs - owner: EcosystemDirector
            _apexTerritoryOverlapResults = new NativeArray<ApexTerritoryOverlapResult>(ApexTerritoryOverlapCandidateCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<CapsulecastCommand>[1] - non-alloc Apex voxel-wall spawn gate command - owner: EcosystemDirector
            _apexSpawnGateCommands = new NativeArray<CapsulecastCommand>(ApexSpawnGateCommandCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<RaycastHit>[4] - non-alloc Apex voxel-wall spawn gate results - owner: EcosystemDirector
            _apexSpawnGateHits = new NativeArray<RaycastHit>(ApexSpawnGateMaxHits, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float4>[32] - global flora predator AUP upload staging buffer - owner: EcosystemDirector
            _floraPredatorAupUpload = new NativeArray<float4>(FloraPredatorAupBufferCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeList<EcosystemSectorSaveRecord>[maxTrackedSectors] - packed ecosystem persistence snapshot staging buffer - owner: EcosystemDirector
            _saveSnapshotSectors = new NativeList<EcosystemSectorSaveRecord>(maxTrackedSectors, Allocator.Persistent);
            RegisterNativeMemorySentinelAllocations();
            // COLD ALLOC: FaunaBrain[16] - managed Apex brain lookup paired with Burst overlap result indices - owner: EcosystemDirector
            _apexTerritoryBrains = new FaunaBrain[ApexTerritoryOverlapCandidateCapacity];
            _floraPredatorAupBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(FloraPredatorAupBufferCapacity); // COLD ALLOC: GraphicsBuffer[32] - global flora predator AUP StructuredBuffer - owner: EcosystemDirector
            _floraPredatorAupGlobalsDirty = true;
            PublishFloraPredatorAupGlobals(0);
            Shader.SetGlobalColor(_GlobalOceanPanicColorId, new Color(1f, 0.05f, 0.035f, 1f));
            Shader.SetGlobalFloat(_ApexInSectorId, 0f);
            PublishGlobalOceanPanic(0f);
            _activeSectorCount = 0;
            _scheduledApexTerritoryOverlapCount = 0;
            _coldTickAccumulator = 0f;
            _scheduledSolveHandle = default;
            _scheduledApexTerritoryOverlapHandle = default;
            _apexSpawnGateHandle = default;
            _apexSpawnGatePendingCell = int3.zero;
            _apexSpawnGateCachedCell = int3.zero;
            _apexSpawnGateScheduled = false;
            _apexSpawnGateHasCachedResult = false;
            _apexSpawnGateCachedBlocked = 0;
            _solveScheduled = false;
            _apexTerritoryOverlapScheduled = false;
            _populationSolvePendingHibernationSync = false;
            _biomeHostility01 = 0f;
            _starvationAggressionPressure01 = 0f;
            _playerStress01 = 0f;
            _spawnCreditBudget = spawnCreditBudgetMax;
            _debugSpawnCreditBudget = _spawnCreditBudget;
            _debugPlayerStress01 = 0f;
            _debugHeadlessSectorCount = 0;
            _eclipsePredatorMigrationTimer = 0f;
            _eclipsePredatorMigrationIntensity01 = 0f;
            _eclipsePredatorMigrationAccumulator = 0f;
            _debugEclipsePredatorMigrationTimeRemaining = 0f;
            _debugEclipsePredatorMigrationIntensity = 0f;
            _debugEclipsePredatorMigratedCount = 0;
            _hostilityTier = 0;
        }

        private void DisposeRuntimeState()
        {
            JobHandle disposeDependency = _solveScheduled ? _scheduledSolveHandle : default;
            if (_apexTerritoryOverlapScheduled)
                disposeDependency = JobHandle.CombineDependencies(disposeDependency, _scheduledApexTerritoryOverlapHandle);
            disposeDependency = JobHandle.CombineDependencies(disposeDependency, _apexSpawnGateHandle);

            UnregisterNativeMemorySentinelAllocations();

            if (_sectorFrontStates.IsCreated)
                _sectorFrontStates.Dispose(disposeDependency);
            if (_sectorBackStates.IsCreated)
                _sectorBackStates.Dispose(disposeDependency);
            if (_preyFrontCounts.IsCreated)
                _preyFrontCounts.Dispose(disposeDependency);
            if (_preyBackCounts.IsCreated)
                _preyBackCounts.Dispose(disposeDependency);
            if (_predatorFrontCounts.IsCreated)
                _predatorFrontCounts.Dispose(disposeDependency);
            if (_predatorBackCounts.IsCreated)
                _predatorBackCounts.Dispose(disposeDependency);
            if (_headlessEntities.Positions.IsCreated)
                _headlessEntities.Positions.Dispose(disposeDependency);
            if (_headlessEntities.SpeciesID.IsCreated)
                _headlessEntities.SpeciesID.Dispose(disposeDependency);
            if (_headlessEntities.Hunger.IsCreated)
                _headlessEntities.Hunger.Dispose(disposeDependency);
            if (_headlessEntities.SectorCoord.IsCreated)
                _headlessEntities.SectorCoord.Dispose(disposeDependency);
            if (_headlessEntities.SectorID.IsCreated)
                _headlessEntities.SectorID.Dispose(disposeDependency);
            if (_sectorIndexByKey.IsCreated)
                _sectorIndexByKey.Dispose(disposeDependency);
            if (_apexTerritorySamples.IsCreated)
                _apexTerritorySamples.Dispose(disposeDependency);
            if (_apexTerritoryOverlapResults.IsCreated)
                _apexTerritoryOverlapResults.Dispose(disposeDependency);
            if (_apexSpawnGateCommands.IsCreated)
                _apexSpawnGateCommands.Dispose(disposeDependency);
            if (_apexSpawnGateHits.IsCreated)
                _apexSpawnGateHits.Dispose(disposeDependency);
            if (_floraPredatorAupUpload.IsCreated)
                _floraPredatorAupUpload.Dispose(disposeDependency);
            if (_saveSnapshotSectors.IsCreated)
                _saveSnapshotSectors.Dispose(disposeDependency);
            ReleaseBuffer(ref _floraPredatorAupBuffer);
            Shader.SetGlobalInt(_PredatorAUPCountId, 0);
            _lastPublishedFloraPredatorAupCount = 0;
            _floraPredatorAupGlobalsDirty = true;
            PublishApexPresenceFake(false);

            _sectorFrontStates = default;
            _sectorBackStates = default;
            _preyFrontCounts = default;
            _preyBackCounts = default;
            _predatorFrontCounts = default;
            _predatorBackCounts = default;
            _headlessEntities = default;
            _sectorIndexByKey = default;
            _apexTerritorySamples = default;
            _apexTerritoryOverlapResults = default;
            _apexSpawnGateCommands = default;
            _apexSpawnGateHits = default;
            _floraPredatorAupUpload = default;
            _saveSnapshotSectors = default;
            _apexTerritoryBrains = null;
            _activeSectorCount = 0;
            _scheduledApexTerritoryOverlapCount = 0;
            _coldTickAccumulator = 0f;
            _scheduledSolveHandle = default;
            _scheduledApexTerritoryOverlapHandle = default;
            _apexSpawnGateHandle = default;
            _apexSpawnGatePendingCell = int3.zero;
            _apexSpawnGateCachedCell = int3.zero;
            _apexSpawnGateScheduled = false;
            _apexSpawnGateHasCachedResult = false;
            _apexSpawnGateCachedBlocked = 0;
            _solveScheduled = false;
            _apexTerritoryOverlapScheduled = false;
            _biomeHostility01 = 0f;
            _starvationAggressionPressure01 = 0f;
            _playerStress01 = 0f;
            _spawnCreditBudget = 0f;
            _debugSpawnCreditBudget = 0f;
            _debugPlayerStress01 = 0f;
            _debugHeadlessSectorCount = 0;
            _hostilityTier = 0;
            _floraPredatorAupSaturationTelemetryIssued = false;
        }

        private void RegisterNativeMemorySentinelAllocations()
        {
            NativeMemorySentinel.RegisterNativeArray(_sectorFrontStates, NativeMemoryOwner, nameof(_sectorFrontStates), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_sectorBackStates, NativeMemoryOwner, nameof(_sectorBackStates), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_preyFrontCounts, NativeMemoryOwner, nameof(_preyFrontCounts), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_preyBackCounts, NativeMemoryOwner, nameof(_preyBackCounts), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_predatorFrontCounts, NativeMemoryOwner, nameof(_predatorFrontCounts), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_predatorBackCounts, NativeMemoryOwner, nameof(_predatorBackCounts), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_headlessEntities.Positions, NativeMemoryOwner, nameof(_headlessEntities.Positions), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_headlessEntities.SpeciesID, NativeMemoryOwner, nameof(_headlessEntities.SpeciesID), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_headlessEntities.Hunger, NativeMemoryOwner, nameof(_headlessEntities.Hunger), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_headlessEntities.SectorCoord, NativeMemoryOwner, nameof(_headlessEntities.SectorCoord), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_headlessEntities.SectorID, NativeMemoryOwner, nameof(_headlessEntities.SectorID), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_sectorIndexByKey, NativeMemoryOwner, nameof(_sectorIndexByKey), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_apexTerritorySamples, NativeMemoryOwner, nameof(_apexTerritorySamples), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_apexTerritoryOverlapResults, NativeMemoryOwner, nameof(_apexTerritoryOverlapResults), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_apexSpawnGateCommands, NativeMemoryOwner, nameof(_apexSpawnGateCommands), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_apexSpawnGateHits, NativeMemoryOwner, nameof(_apexSpawnGateHits), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_floraPredatorAupUpload, NativeMemoryOwner, nameof(_floraPredatorAupUpload), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeList(_saveSnapshotSectors, NativeMemoryOwner, nameof(_saveSnapshotSectors), NativeMemoryLifetime);
        }

        private void UnregisterNativeMemorySentinelAllocations()
        {
            NativeMemorySentinel.UnregisterNativeArray(_sectorFrontStates);
            NativeMemorySentinel.UnregisterNativeArray(_sectorBackStates);
            NativeMemorySentinel.UnregisterNativeArray(_preyFrontCounts);
            NativeMemorySentinel.UnregisterNativeArray(_preyBackCounts);
            NativeMemorySentinel.UnregisterNativeArray(_predatorFrontCounts);
            NativeMemorySentinel.UnregisterNativeArray(_predatorBackCounts);
            NativeMemorySentinel.UnregisterNativeArray(_headlessEntities.Positions);
            NativeMemorySentinel.UnregisterNativeArray(_headlessEntities.SpeciesID);
            NativeMemorySentinel.UnregisterNativeArray(_headlessEntities.Hunger);
            NativeMemorySentinel.UnregisterNativeArray(_headlessEntities.SectorCoord);
            NativeMemorySentinel.UnregisterNativeArray(_headlessEntities.SectorID);
            NativeMemorySentinel.UnregisterNativeHashMap(NativeMemoryOwner, nameof(_sectorIndexByKey));
            NativeMemorySentinel.UnregisterNativeArray(_apexTerritorySamples);
            NativeMemorySentinel.UnregisterNativeArray(_apexTerritoryOverlapResults);
            NativeMemorySentinel.UnregisterNativeArray(_apexSpawnGateCommands);
            NativeMemorySentinel.UnregisterNativeArray(_apexSpawnGateHits);
            NativeMemorySentinel.UnregisterNativeArray(_floraPredatorAupUpload);
            NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, nameof(_saveSnapshotSectors));
        }

        private void TryRegisterService()
        {
            if (_registeredService)
                return;

            GlobalRegistry.RegisterEcosystemDirectorService(this);
            _registeredService = ReferenceEquals(GlobalRegistry.EcosystemDirector, this);
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

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredSlowTickable = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregisterSlowTickable()
        {
            if (!_registeredSlowTickable)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredSlowTickable = false;
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrameTickable || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrameTickable = SystemDispatcher.GetLateFrameLane(PriorityLayer.Environment).Contains(this);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_registeredLateFrameTickable)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrameTickable = false;
        }

        private void EnsurePlayerSectorRegistered()
        {
            if (HasPendingSimulationJob())
                return;

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return;

            ResolveOrCreateSectorSlot(QuantizeSector(in playerAup), seedWithBaseline: true);
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

        private void TickEclipsePredatorShallowMigration(float dt)
        {
            if (_eclipsePredatorMigrationTimer <= 0f || _eclipsePredatorMigrationIntensity01 <= 0f)
            {
                _debugEclipsePredatorMigrationTimeRemaining = 0f;
                _debugEclipsePredatorMigrationIntensity = 0f;
                return;
            }

            _eclipsePredatorMigrationTimer = math.max(0f, _eclipsePredatorMigrationTimer - math.max(0f, dt));
            _debugEclipsePredatorMigrationTimeRemaining = _eclipsePredatorMigrationTimer;
            _debugEclipsePredatorMigrationIntensity = _eclipsePredatorMigrationIntensity01;

            if (_eclipsePredatorMigrationTimer <= 0f)
            {
                _eclipsePredatorMigrationIntensity01 = 0f;
                _eclipsePredatorMigrationAccumulator = 0f;
                _debugEclipsePredatorMigrationIntensity = 0f;
                return;
            }

            _eclipsePredatorMigrationAccumulator += dt;
            if (_eclipsePredatorMigrationAccumulator < eclipsePredatorMigrationIntervalSeconds)
                return;

            _eclipsePredatorMigrationAccumulator = 0f;
            if (!TryResolveEclipseTier0Attractor(out Vector3 attractorPosition))
                return;

            PersistentWorldRegistry registry = _cachedPersistentWorldRegistry != null
                ? _cachedPersistentWorldRegistry
                : GlobalRegistry.PersistentWorldRegistry;
            if (registry == null)
                return;

            float stepMeters = eclipsePredatorMigrationStepMeters * math.max(0.1f, _eclipsePredatorMigrationIntensity01);
            int migratedCount = registry.MigrateApexFaunaHibernationStatesToward(
                attractorPosition,
                eclipsePredatorMigrationRadiusMeters,
                stepMeters);
            _debugEclipsePredatorMigratedCount += migratedCount;
        }

        private bool TryResolveEclipseTier0Attractor(out Vector3 attractorPosition)
        {
            attractorPosition = default;
            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return false;

            float3 playerRuntimePosition = playerAup.ToRuntimeFloat3();
            attractorPosition = ToVector3(playerRuntimePosition);
            float waterLevel = ResolveWaterSurfaceLevel(attractorPosition);
            attractorPosition.y = waterLevel - eclipsePredatorTier0TargetDepthMeters;
            return true;
        }

        private bool TryResolveEclipsePredatorTier0SelectionBoost(Vector3 worldPosition, out float selectionBoost)
        {
            selectionBoost = 1f;
            if (_eclipsePredatorMigrationTimer <= 0f || _eclipsePredatorMigrationIntensity01 <= 0f)
                return false;

            float depthMeters = ResolveDepthMeters(worldPosition);
            if (depthMeters > eclipsePredatorTier0DepthMaxMeters)
                return false;

            selectionBoost = math.lerp(1f, eclipsePredatorTier0SelectionBoost, _eclipsePredatorMigrationIntensity01);
            return selectionBoost > 1f;
        }

        private static float ResolveDepthMeters(Vector3 worldPosition)
        {
            return math.max(0f, ResolveWaterSurfaceLevel(worldPosition) - worldPosition.y);
        }

        private static float ResolveWaterSurfaceLevel(Vector3 worldPosition)
        {
            MapMagicBridge bridge = GlobalRegistry.MapMagic;
            if (bridge != null)
                return bridge.WaterSurfaceLevel;

            return worldPosition.y;
        }

        private void ScheduleSectorSolve()
        {
            if (_activeSectorCount <= 0 || _solveScheduled)
                return;

            var solveJob = new LotkaVolterraPopulationJob
            {
                FrontStates = _sectorFrontStates,
                PreyCounts = _preyFrontCounts,
                PredatorCounts = _predatorFrontCounts,
                BackStates = _sectorBackStates,
                PreyBackCounts = _preyBackCounts,
                PredatorBackCounts = _predatorBackCounts,
                HeadlessPositions = _headlessEntities.Positions,
                HeadlessSpeciesID = _headlessEntities.SpeciesID,
                HeadlessHunger = _headlessEntities.Hunger,
                HeadlessSectorCoord = _headlessEntities.SectorCoord,
                HeadlessSectorID = _headlessEntities.SectorID,
                DeltaSeconds = coldTickIntervalSeconds,
                PreyBirthRate = preyBirthRatePerSecond,
                PredationRate = predationRatePerSecond,
                PredatorGrowthRate = predatorGrowthRatePerSecond,
                PredatorDeathRate = predatorDeathRatePerSecond,
                ReproductionFoodThreshold01 = reproductionFoodThreshold01,
                ReproductionPredatorThreshold = reproductionPredatorThreshold,
                MutationBitMask = generationMutationBitMask,
                PreyCapacity = preyPopulationCapacity,
                MaxPreyPopulation = maxPreyPopulation,
                MaxPredatorPopulation = maxPredatorPopulation,
                MaximumSpeedMultiplier = maximumSpeedMultiplier,
                StarvationComfortPreyPerPredator = starvationComfortPreyPerPredator
            };

            _scheduledSolveHandle = solveJob.Schedule(_activeSectorCount, 16);
            var migrationJob = new HeadlessThresholdMigrationJob
            {
                States = _sectorBackStates,
                Positions = _headlessEntities.Positions,
                SectorCoord = _headlessEntities.SectorCoord,
                SectorID = _headlessEntities.SectorID,
                MigrationFoodThreshold01 = migrationFoodThreshold01,
                MigrationPredatorTolerance = migrationPredatorTolerance
            };
            _scheduledSolveHandle = migrationJob.Schedule(_activeSectorCount, 16, _scheduledSolveHandle);
            _solveScheduled = true;
        }

        private bool HasPendingSimulationJob()
        {
            return _solveScheduled;
        }

        private void CompleteScheduledSimulation(bool forceComplete)
        {
            CompleteScheduledSolve(forceComplete);
        }

        private void CompleteScheduledSolve(bool forceComplete)
        {
            if (!_solveScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _scheduledSolveHandle, forceComplete))
                return;

            NativeArray<SectorPopulationState> swap = _sectorFrontStates;
            _sectorFrontStates = _sectorBackStates;
            _sectorBackStates = swap;
            NativeArray<int> preySwap = _preyFrontCounts;
            _preyFrontCounts = _preyBackCounts;
            _preyBackCounts = preySwap;
            NativeArray<int> predatorSwap = _predatorFrontCounts;
            _predatorFrontCounts = _predatorBackCounts;
            _predatorBackCounts = predatorSwap;
            _solveScheduled = false;
            RefreshStarvationPressure();
            _populationSolvePendingHibernationSync = true;
            _debugHeadlessSectorCount = _activeSectorCount;
        }

        private void ScheduleApexTerritoryOverlap(Vector3 queryOrigin)
        {
            if (_apexTerritoryOverlapScheduled ||
                !_apexTerritorySamples.IsCreated ||
                !_apexTerritoryOverlapResults.IsCreated ||
                _apexTerritoryBrains == null)
            {
                return;
            }

            int hitCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                queryOrigin,
                ApexTerritoryOverlapQueryRadiusMeters,
                SpatialTargetKind.Bioform,
                _apexTerritoryOverlapHits);
            int sampleCount = 0;
            for (int hitIndex = 0; hitIndex < hitCount && sampleCount < ApexTerritoryOverlapCandidateCapacity; hitIndex++)
            {
                SpatialQueryHit hit = _apexTerritoryOverlapHits[hitIndex];
                FaunaBrain brain = hit.Owner as FaunaBrain;
                if (brain == null || brain.IsDead || !brain.IsApexPredatorRuntime)
                    continue;

                AbsoluteUniversePosition hitAup = hit.HasAbsolutePosition
                    ? hit.AbsolutePosition
                    : AbsoluteUniversePosition.FromRuntimePosition(hit.Position);
                _apexTerritoryBrains[sampleCount] = brain;
                _apexTerritorySamples[sampleCount] = new ApexTerritorySample
                {
                    PositionAup = hitAup.ToAlignedBlit(),
                    Radius = brain.ApexTerritoryRadiusMeters,
                    MassScore = brain.ApexTerritoryMassScore,
                    BrainIndex = sampleCount,
                    Padding = 0
                };
                _apexTerritoryOverlapResults[sampleCount] = default;
                sampleCount++;
            }

            if (sampleCount < 2)
            {
                for (int i = 0; i < sampleCount; i++)
                    _apexTerritoryBrains[i] = null;
                return;
            }

            var overlapJob = new ApexTerritoryOverlapJob
            {
                Samples = _apexTerritorySamples,
                Results = _apexTerritoryOverlapResults,
                Count = sampleCount,
                OverlapThreshold01 = ApexTerritoryOverlapRetreatThreshold01
            };

            _scheduledApexTerritoryOverlapHandle = overlapJob.Schedule(sampleCount, 4);
            _scheduledApexTerritoryOverlapCount = sampleCount;
            _apexTerritoryOverlapScheduled = true;
        }

        private void CompleteScheduledApexTerritoryOverlap(bool forceComplete)
        {
            if (!_apexTerritoryOverlapScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _scheduledApexTerritoryOverlapHandle, forceComplete))
                return;

            int count = math.min(_scheduledApexTerritoryOverlapCount, ApexTerritoryOverlapCandidateCapacity);
            for (int i = 0; i < count; i++)
            {
                ApexTerritoryOverlapResult result = _apexTerritoryOverlapResults[i];
                if (result.RetreatBrainIndex < 0 ||
                    result.RetreatBrainIndex >= count ||
                    result.RivalBrainIndex < 0 ||
                    result.RivalBrainIndex >= count ||
                    result.Overlap01 <= ApexTerritoryOverlapRetreatThreshold01)
                {
                    continue;
                }

                FaunaBrain retreatBrain = _apexTerritoryBrains[result.RetreatBrainIndex];
                if (retreatBrain == null || retreatBrain.IsDead)
                    continue;

                ApexTerritorySample rivalSample = _apexTerritorySamples[result.RivalBrainIndex];
                AbsoluteUniversePosition rivalAup = AbsoluteUniversePosition.FromAlignedBlit(in rivalSample.PositionAup);
                retreatBrain.ForceApexRetreat(ToVector3(rivalAup.ToRuntimeFloat3()));
            }

            for (int i = 0; i < count; i++)
            {
                _apexTerritoryBrains[i] = null;
                _apexTerritoryOverlapResults[i] = default;
            }

            _scheduledApexTerritoryOverlapHandle = default;
            _scheduledApexTerritoryOverlapCount = 0;
            _apexTerritoryOverlapScheduled = false;
        }

        private void PublishFloraPredatorAupBuffer(Vector3 queryOrigin)
        {
            if (!_floraPredatorAupUpload.IsCreated || _floraPredatorAupBuffer == null)
                return;

            int hitCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                queryOrigin,
                FloraPredatorAupQueryRadiusMeters,
                SpatialTargetKind.Bioform,
                _floraPredatorAupHits);
            int uploadCount = 0;
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                SpatialQueryHit hit = _floraPredatorAupHits[hitIndex];
                FaunaBrain brain = hit.Owner as FaunaBrain;
                if (brain == null || brain.IsDead || !brain.IsApexPredatorRuntime)
                    continue;

                if (uploadCount < FloraPredatorAupBufferCapacity)
                {
                    _floraPredatorAupUpload[uploadCount] = new float4(
                        hit.Position.x,
                        hit.Position.y,
                        hit.Position.z,
                        FloraPredatorStealthRadiusMeters);
                    uploadCount++;
                }
            }

            if (uploadCount > 0)
                GraphicsBufferUploadUtility.UploadNativeArray(_floraPredatorAupBuffer, _floraPredatorAupUpload, uploadCount);

            bool saturated = hitCount >= FloraPredatorAupHitCapacity || uploadCount >= FloraPredatorAupBufferCapacity;
            if (saturated && !_floraPredatorAupSaturationTelemetryIssued)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _FloraPredatorAupSaturationWarningHash,
                    _EcosystemDirectorContextHash,
                    math.max(hitCount, uploadCount));
                _floraPredatorAupSaturationTelemetryIssued = true;
            }
            else if (!saturated)
            {
                _floraPredatorAupSaturationTelemetryIssued = false;
            }

            PublishFloraPredatorAupGlobals(uploadCount);
            PublishApexPresenceFake(IsApexInSector(queryOrigin));
        }

        private void PublishFloraPredatorAupGlobals(int uploadCount)
        {
            int safeUploadCount = math.clamp(uploadCount, 0, FloraPredatorAupBufferCapacity);
            if (_floraPredatorAupGlobalsDirty && _floraPredatorAupBuffer != null)
            {
                Shader.SetGlobalBuffer(_PredatorAUPBufferId, _floraPredatorAupBuffer);
                Shader.SetGlobalVector(_PredatorAUPParamsId, new Vector4(FloraPredatorStealthRadiusMeters, FloraPredatorStealthDimStrength, 0f, 0f));
                _floraPredatorAupGlobalsDirty = false;
            }

            if (_lastPublishedFloraPredatorAupCount == safeUploadCount)
                return;

            _lastPublishedFloraPredatorAupCount = safeUploadCount;
            Shader.SetGlobalInt(_PredatorAUPCountId, safeUploadCount);
        }

        private void PublishGlobalOceanPanic(float panic01)
        {
            float resolvedPanic01 = math.saturate(panic01);
            if (math.abs(_lastPublishedGlobalOceanPanic01 - resolvedPanic01) < 0.001f)
                return;

            _lastPublishedGlobalOceanPanic01 = resolvedPanic01;
            Shader.SetGlobalFloat(_GlobalOceanPanicId, resolvedPanic01);
        }

        private void PublishApexPresenceFake(bool apexInSector)
        {
            byte flag = apexInSector ? (byte)1 : (byte)0;
            if (_lastPublishedApexInSector != flag)
            {
                _lastPublishedApexInSector = flag;
                Shader.SetGlobalFloat(_ApexInSectorId, flag);
            }

            PublishGlobalOceanPanic(flag);
        }

        private static bool TryResolvePlayerRuntimePosition(out Vector3 playerPosition)
        {
            playerPosition = default;
            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return false;

            float3 runtimePosition = playerAup.ToRuntimeFloat3();
            playerPosition = ToVector3(runtimePosition);
            return true;
        }

        private static bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null)
            {
                PlayerMovementRuntimeState movementState = runtimeContext.MovementState;
                if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
                {
                    playerAup = movementState.PredictedAup;
                    return true;
                }

                PlayerLookState lookState = runtimeContext.LookState;
                if ((lookState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
                {
                    playerAup = AbsoluteUniversePosition.FromRuntimePosition(ToVector3(lookState.EyePosition));
                    return true;
                }
            }

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
            if (playerMovement == null)
                return false;

            playerAup = playerMovement.CurrentAup;
            return true;
        }

        private void SyncPendingHibernatedFaunaPopulationRecords()
        {
            if (!_populationSolvePendingHibernationSync)
                return;

            _populationSolvePendingHibernationSync = false;
            SyncHibernatedFaunaPopulationRecords();
        }

        private void SyncHibernatedFaunaPopulationRecords()
        {
            if (_activeSectorCount <= 0)
                return;

            if (_cachedPersistentWorldRegistry == null)
                _cachedPersistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;

            if (_cachedPersistentWorldRegistry == null)
                return;

            int syncBudget = math.min(HibernationPopulationSyncsPerColdSolve, _activeSectorCount);
            for (int i = 0; i < syncBudget; i++)
            {
                if (_nextHibernationPopulationSyncIndex >= _activeSectorCount)
                    _nextHibernationPopulationSyncIndex = 0;

                SectorPopulationState state = _sectorFrontStates[_nextHibernationPopulationSyncIndex];
                _cachedPersistentWorldRegistry.ReconcileFaunaHibernationSectorPopulation(
                    state.SectorCoord,
                    state.PreyPopulationRounded,
                    state.PredatorPopulationRounded,
                    maxPreyPopulation,
                    maxPredatorPopulation);

                _nextHibernationPopulationSyncIndex++;
            }
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
            WriteHeadlessSlot(slotIndex, in seededState);
            return slotIndex;
        }

        private SectorPopulationState SeedSectorState(int2 sectorCoord, bool seedWithBaseline)
        {
            int biomeId = ResolveBiomeIdForSector(sectorCoord);
            ResolveProbabilityTablePopulation(
                sectorCoord,
                biomeId,
                leviathanSectorSpawnChance,
                grazerPopulationPerSector,
                leviathanPopulationPerSector,
                out int preyPopulation,
                out int predatorPopulation);

            SectorPopulationState state = default;
            state.SectorCoord = sectorCoord;
            state.PreyPopulation = preyPopulation;
            state.PredatorPopulation = predatorPopulation;
            state.HarvestPressure = 0f;
            state.Fitness = baselineFitness;
            state.SpeedMultiplier = 1f;
            state.CamouflageIndex = 0f;
            state.FoodDensity01 = ResolveSectorFoodDensity01(sectorCoord, biomeId, 0f, 0f);
            state.TemperatureScore01 = ResolveSectorTemperatureScore01(sectorCoord, biomeId);
            state.Oxygen01 = 1f;
            state.AlgaeBloom01 = 0f;
            state.PreyPopulationRounded = preyPopulation;
            state.PredatorPopulationRounded = predatorPopulation;
            state.BiomeId = biomeId;
            state.ApexInSector = predatorPopulation > 0 ? (byte)1 : (byte)0;
            return state;
        }

        private void ClearHeadlessRuntimeState()
        {
            int capacity = _sectorFrontStates.IsCreated ? _sectorFrontStates.Length : 0;
            for (int i = 0; i < capacity; i++)
            {
                if (_preyFrontCounts.IsCreated)
                    _preyFrontCounts[i] = 0;
                if (_preyBackCounts.IsCreated)
                    _preyBackCounts[i] = 0;
                if (_predatorFrontCounts.IsCreated)
                    _predatorFrontCounts[i] = 0;
                if (_predatorBackCounts.IsCreated)
                    _predatorBackCounts[i] = 0;
                if (_headlessEntities.Positions.IsCreated)
                    _headlessEntities.Positions[i] = float3.zero;
                if (_headlessEntities.SpeciesID.IsCreated)
                    _headlessEntities.SpeciesID[i] = 0;
                if (_headlessEntities.Hunger.IsCreated)
                    _headlessEntities.Hunger[i] = 0;
                if (_headlessEntities.SectorCoord.IsCreated)
                    _headlessEntities.SectorCoord[i] = int2.zero;
                if (_headlessEntities.SectorID.IsCreated)
                    _headlessEntities.SectorID[i] = 0;
            }
        }

        private void WriteHeadlessSlot(int slotIndex, in SectorPopulationState state)
        {
            if (slotIndex < 0 || slotIndex >= maxTrackedSectors)
                return;

            if (_preyFrontCounts.IsCreated)
                _preyFrontCounts[slotIndex] = state.PreyPopulationRounded;
            if (_preyBackCounts.IsCreated)
                _preyBackCounts[slotIndex] = state.PreyPopulationRounded;
            if (_predatorFrontCounts.IsCreated)
                _predatorFrontCounts[slotIndex] = state.PredatorPopulationRounded;
            if (_predatorBackCounts.IsCreated)
                _predatorBackCounts[slotIndex] = state.PredatorPopulationRounded;
            if (_headlessEntities.Positions.IsCreated)
                _headlessEntities.Positions[slotIndex] = ResolveSectorCenterPosition(state.SectorCoord);
            if (_headlessEntities.SpeciesID.IsCreated)
                _headlessEntities.SpeciesID[slotIndex] = state.PredatorPopulationRounded > state.PreyPopulationRounded ? (byte)2 : (byte)1;
            if (_headlessEntities.Hunger.IsCreated)
            {
                float preyPerPredator = state.PredatorPopulationRounded > 0
                    ? state.PreyPopulationRounded / math.max(1f, state.PredatorPopulationRounded)
                    : starvationComfortPreyPerPredator;
                _headlessEntities.Hunger[slotIndex] = (byte)math.round(math.saturate(1f - preyPerPredator / math.max(1f, starvationComfortPreyPerPredator)) * 255f);
            }
            if (_headlessEntities.SectorCoord.IsCreated)
                _headlessEntities.SectorCoord[slotIndex] = state.SectorCoord;
            if (_headlessEntities.SectorID.IsCreated)
                _headlessEntities.SectorID[slotIndex] = ResolveSectorId(state.SectorCoord);
        }

        private static void ResolveProbabilityTablePopulation(
            int2 sectorCoord,
            int biomeId,
            float leviathanChance01,
            int grazerPopulationPerSector,
            int leviathanPopulationPerSector,
            out int preyPopulation,
            out int predatorPopulation)
        {
            float roll01 = StableSectorRandom01(sectorCoord, biomeId);
            bool spawnLeviathan = roll01 < math.saturate(leviathanChance01);
            preyPopulation = spawnLeviathan ? 0 : math.max(0, grazerPopulationPerSector);
            predatorPopulation = spawnLeviathan ? math.max(0, leviathanPopulationPerSector) : 0;
        }

        private static float ResolveSectorFoodDensity01(int2 sectorCoord, int biomeId, float harvestPressure01, float algaeBloom01)
        {
            float roll01 = StableSectorRandom01(sectorCoord + new int2(17, -31), biomeId);
            float biomeBias = biomeId == 1 ? 0.12f : biomeId == 2 ? -0.04f : 0f;
            return math.saturate(0.42f + (roll01 * 0.46f) + biomeBias - (math.saturate(harvestPressure01) * 0.35f) - (math.saturate(algaeBloom01) * 0.45f));
        }

        private static float ResolveSectorTemperatureScore01(int2 sectorCoord, int biomeId)
        {
            float roll01 = StableSectorRandom01(sectorCoord + new int2(-19, 43), biomeId ^ 0x35A);
            float biomeBias = biomeId == 1 ? 0.22f : biomeId == 2 ? 0.08f : 0f;
            return math.saturate(0.28f + (roll01 * 0.48f) + biomeBias);
        }

        private static float3 ResolveSectorCenterPosition(int2 sectorCoord)
        {
            return new float3(
                (sectorCoord.x + 0.5f) * SectorEdgeLengthMeters,
                0f,
                (sectorCoord.y + 0.5f) * SectorEdgeLengthMeters);
        }

        private static int ResolveSectorId(int2 sectorCoord)
        {
            return (int)(MixSectorBits(sectorCoord.x, sectorCoord.y) & 0x7FFFFFFFu);
        }

        private static float StableSectorRandom01(int2 sectorCoord, int biomeId)
        {
            uint mix = MixSectorBits(sectorCoord.x, sectorCoord.y) ^ (uint)biomeId;
            return (mix & 0x00FFFFFFu) * InvStableSectorRandomMask;
        }

        private static uint MixSectorBits(int sectorX, int sectorZ)
        {
            unchecked
            {
                uint mix = ((uint)sectorX * 73856093u) ^ ((uint)sectorZ * 19349663u);
                return mix;
            }
        }

        private static bool IsApexInSectorState(in SectorPopulationState state)
        {
            return state.ApexInSector != 0;
        }

        private static int2 QuantizeSector(Vector3 worldPosition)
        {
            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(worldPosition);
            return QuantizeSector(in aup);
        }

        private static int2 QuantizeSector(in AbsoluteUniversePosition position)
        {
            double3 absolutePosition = position.ToAbsoluteDouble3();
            return new int2(
                (int)math.floor(absolutePosition.x * InvSectorEdgeLengthMeters),
                (int)math.floor(absolutePosition.z * InvSectorEdgeLengthMeters));
        }

        private static double ResolveRuntimeAupDistanceSq(in AbsoluteUniversePosition originAup, Vector3 runtimePosition)
        {
            AbsoluteUniversePosition runtimeAup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            return AbsoluteUniversePosition.DistanceSq(in originAup, in runtimeAup);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static long PackSectorKey(int2 sectorCoord)
        {
            return ((long)sectorCoord.x << 32) | (uint)sectorCoord.y;
        }

        private static int ResolveBiomeIdForSector(int2 sectorCoord)
        {
            uint mix = MixSectorBits(sectorCoord.x, sectorCoord.y);
            uint bucket = mix & 0x0Fu;
            if (bucket < 3u)
                return 1;
            if (bucket < 8u)
                return 2;

            return 0;
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

    }
}
