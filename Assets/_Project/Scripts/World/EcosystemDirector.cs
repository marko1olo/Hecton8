using System;
using System.IO;
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
using UnityEngine.Serialization;

namespace Hecton8.World
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    internal struct EcosystemSectorSaveRecord
    {
        public int2 SectorCoord;
        public uint PackedPopulations;
        public uint PackedAdaptation;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 16)]
    internal struct EcosystemBiomassSaveRun
    {
        public int2 StartMacroCell;
        public sbyte PreyBiomassQ;
        public sbyte PredatorBiomassQ;
        public sbyte CarryingCapacityQ;
        public byte RunLength;
        public uint Reserved;
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
        private const float FrostTickIntervalSeconds = 5f;
        private const float DefaultFloraGrazingSearchRadiusMeters = 2.75f;
        private const float SectorEdgeLengthMeters = 1000f;
        private const float InvSectorEdgeLengthMeters = 1f / SectorEdgeLengthMeters;
        private const float BiomassMacroCellSizeMeters = 50f;
        private const float InvBiomassMacroCellSizeMeters = 1f / BiomassMacroCellSizeMeters;
        private const float InvStableSectorRandomMask = 1f / 16777215f;
        private const float InvFoodHeatmapByteMax = 1f / 255f;
        private const float OxygenDieOffThreshold01 = 0.35f;
        private const float InvOxygenDieOffThreshold01 = 1f / OxygenDieOffThreshold01;
        private const float InvAlgaeBloomPreyGrowthDivisor = 0.2f;
        private const int DefaultApexSectorBucketCutoff = 3;
        private const int ApexSectorBucketCount = 16;
        private const int DefaultGrazerPopulationPerSector = 10;
        private const int DefaultLeviathanPopulationPerSector = 1;
        private const int MinimumSectorCapacity = 16;
        private const int MinimumBiomassCellCapacity = 64;
        private const int BiomassImpactQueueCapacity = 128;
        private const int BiomassBlackBoxCapacity = 300;
        private const byte BiomassImpactKindDeath = 1;
        private const byte BiomassImpactKindFishing = 2;
        private const byte BiomassImpactKindPredation = 3;
        private const byte BiomassImpactKindApexKill = 4;
        private const byte BiomassCellFlagSectorClearedPublished = 1 << 0;
        private const uint BiomassSaveRecordMarker = 0x80000000u;
        private const uint BiomassSaveRunLengthMask = 0x000000FFu;
        private const int BiomassSavePreyShift = 8;
        private const int BiomassSavePredatorShift = 16;
        private const int BiomassSaveCapacityShift = 24;
        private const float DefaultHostilityPeakHoldSeconds = 18f;
        private const float LogicalLodFullSimDistanceMeters = 50f;
        private const float LogicalLodDataOnlyDistanceMeters = 150f;
        private const float ThermalSpawnTemperatureThresholdCelsius = 40f;
        private const float ThermalSpawnDepthThresholdMeters = 2000f;
        private const float LightFalloffDepthMeters = 2500f;
        private const float InvLightFalloffDepthMeters = 1f / LightFalloffDepthMeters;
        private const float PredatorDietValidationRadiusMeters = 500f;
        private const float CorpseSpawnInfluenceRadiusMeters = 500f;
        private const float MinimumCorpseDietInfluence01 = 0.001f;
        private const float CorpseSpawnSelectionScale = 2.6f;
        private const float WhaleFallScavengerSpawnMultiplier = 50f;
        private const float HighPlayerStressThreshold01 = 0.8f;
        private const float InvHighPlayerStressRange01 = 1f / (1f - HighPlayerStressThreshold01);
        private const float WhaleFallAcousticImpulseLifetimeSeconds = 7200f;
        private const float WhaleFallAcousticImpulseEnergyJoules = 28000f;
        private const float WhaleFallAcousticImpulseVolume01 = 0.42f;
        private const float WhaleFallAcousticImpulsePitchScale = 0.52f;
        private const int PredatorSpawnValidationHitCapacity = 64;
        private const int ApexSpawnGateCommandCount = 1;
        private const int ApexSpawnGateMaxHits = 1;
        private const float ApexSpawnGateCapsuleRadiusMeters = 2.5f;
        private const float ApexSpawnGateCapsuleHalfHeightMeters = 3f;
        private const float ApexSpawnGateSweepDistanceMeters = 0.25f;
        private const float ApexSpawnGateCacheCellSizeMeters = 10f;
        private const float InvApexSpawnGateCacheCellSizeMeters = 1f / ApexSpawnGateCacheCellSizeMeters;
        private const int HibernationPopulationSyncsPerColdSolve = 8;
        private const int ApexTerritoryOverlapCandidateCapacity = 16;
        private const int ApexTerritoryOverlapHitCapacity = 64;
        private const int ApexThreatProbeHitCapacity = 16;
        private const int MigrationTieNoNeighborBucket = 4;
        private const float ApexTerritoryOverlapQueryRadiusMeters = 1400f;
        private const float ApexTerritoryOverlapRetreatThreshold01 = 0.30f;
        private const int FloraPredatorAupBufferCapacity = 32;
        private const int FloraPredatorAupHitCapacity = 64;
        private const float FloraPredatorAupQueryRadiusMeters = 700f;
        private const float FloraPredatorStealthRadiusMeters = 15f;
        private const float FloraPredatorStealthDimStrength = 0.82f;
        private const string NativeMemoryOwner = nameof(EcosystemDirector);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        private const ulong BiomassTelemetryDumpMagic = 0x0038424D53434548UL;
        private const string BiomassTelemetryDumpRelativePath = "Docs/AgentLogs/Dump_ECOLOGICAL_BIOMASS_ENGINE.bin";
        private static readonly string[] ThermalSpawnTokens = { "lava", "thermal", "brine", "heat", "volcanic", "smoker" };
        private static readonly string[] SharkSpawnTokens = { "shark", "hunter", "stalker" };
        private static readonly string[] ScavengerSpawnTokens = { "scavenger", "crab", "eel", "carrion", "cleaner" };
        private static readonly uint _FloraPredatorAupSaturationWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("EcosystemDirector.FloraPredatorAupSaturation"));
        private static readonly uint _BiomassTelemetryHash = unchecked((uint)Hecton.Localization.LocHash.Compute("EcosystemDirector.GlobalBiomassSum"));
        private static readonly uint _EcologicalCollapseWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Ecological Collapse"));
        private static readonly uint _SectorClearedEventHash = unchecked((uint)Hecton.Localization.LocHash.Compute("EcosystemDirector.SectorCleared"));
        private static readonly uint _ItemCuredFishNameHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ITEM_CURED_FISH_NAME"));
        private static readonly uint _ItemRawFishNameHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ITEM_RAW_FISH_NAME"));
        private static readonly uint _ItemCookedFishNameHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ITEM_COOKED_FISH_NAME"));
        private static readonly uint _EcosystemDirectorContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute(nameof(EcosystemDirector)));
        // COLD ALLOC: SpatialQueryHit[64] - non-alloc predator diet validation scratch for spawn gating - owner: EcosystemDirector
        private static readonly SpatialQueryHit[] _predatorSpawnValidationHits = new SpatialQueryHit[PredatorSpawnValidationHitCapacity];
        // COLD ALLOC: SpatialQueryHit[64] - non-alloc Apex territory candidate query scratch - owner: EcosystemDirector
        private static readonly SpatialQueryHit[] _apexTerritoryOverlapHits = new SpatialQueryHit[ApexTerritoryOverlapHitCapacity];
        // COLD ALLOC: SpatialQueryHit[16] - non-alloc player physiology Apex threat probe scratch - owner: EcosystemDirector
        private static readonly SpatialQueryHit[] _apexThreatProbeHits = new SpatialQueryHit[ApexThreatProbeHitCapacity];
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
        private static readonly int _BiomassOvergrowthId = Shader.PropertyToID("_HectonBiomassOvergrowth");

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

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 16)]
        private struct BiomassImpactEvent
        {
            public int2 MacroCellCoord;
            public float Amount;
            public byte Kind;
            public byte Padding0;
            public ushort Padding1;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 32)]
        private struct BiomassTelemetryEntry
        {
            public uint FrameIndex;
            public uint StateHash;
            public int ActiveCellCount;
            public int Flags;
            public float GlobalBiomassSum;
            public float PreyBiomassSum;
            public float PredatorBiomassSum;
            public float FloraOvergrowth01;
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

                    float overlap01 = ComputeSquaredTerritoryPressure01(in sample.PositionAup, sample.Radius, in rival.PositionAup, rival.Radius);
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

            private static float ComputeSquaredTerritoryPressure01(
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
                return math.saturate(1f - centerDistanceSq * math.rcp(math.max(0.001f, sumRadiusSq)));
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
            [ReadOnly] public NativeArray<byte> FoodDensityHeatmapR8;
            public NativeArray<SectorPopulationState> BackStates;
            public NativeArray<int> PreyBackCounts;
            public NativeArray<int> PredatorBackCounts;
            public NativeArray<float3> HeadlessPositions;
            public NativeArray<byte> HeadlessSpeciesID;
            public NativeArray<byte> HeadlessHunger;
            public NativeArray<int2> HeadlessSectorCoord;
            public NativeArray<int> HeadlessSectorID;
            public int2 FoodDensityHeatmapSize;
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
                float foodDensity01 = ResolveSectorFoodDensity01(
                    state.SectorCoord,
                    state.BiomeId,
                    state.HarvestPressure,
                    state.AlgaeBloom01,
                    FoodDensityHeatmapR8,
                    FoodDensityHeatmapSize);
                float temperatureScore01 = state.TemperatureScore01;
                float oxygen01 = state.Oxygen01 <= 0f ? 1f : math.saturate(state.Oxygen01);
                float bloom01 = math.saturate(state.AlgaeBloom01);
                float dt = math.max(0f, DeltaSeconds);
                float invStarvationComfort = math.rcp(math.max(1f, StarvationComfortPreyPerPredator));

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

                if (oxygen01 < OxygenDieOffThreshold01)
                {
                    float dieOffScale = math.saturate(oxygen01 * InvOxygenDieOffThreshold01);
                    prey *= 0.55f + (0.45f * dieOffScale);
                    predator *= 0.7f + (0.3f * dieOffScale);
                }

                int preyPopulation = math.clamp(RoundPositiveToInt(prey), 0, math.max(0, MaxPreyPopulation));
                int predatorPopulation = math.clamp(RoundPositiveToInt(predator), 0, math.max(0, MaxPredatorPopulation));
                state.PreyPopulation = preyPopulation;
                state.PredatorPopulation = predatorPopulation;
                state.HarvestPressure = math.saturate(state.HarvestPressure * 0.65f);
                state.FoodDensity01 = foodDensity01;
                state.TemperatureScore01 = temperatureScore01;
                state.Oxygen01 = oxygen01;
                state.AlgaeBloom01 = bloom01;
                state.PreyPopulationRounded = preyPopulation;
                state.PredatorPopulationRounded = predatorPopulation;
                byte apexInSector = (byte)math.select(0, 1, predatorPopulation > 0);
                byte headlessSpeciesId = (byte)math.select(1, 2, predatorPopulation > preyPopulation);
                float preyPerPredator = math.select(
                    StarvationComfortPreyPerPredator,
                    preyPopulation * math.rcp(math.max(1f, predatorPopulation)),
                    predatorPopulation > 0);
                state.ApexInSector = apexInSector;
                BackStates[index] = state;
                PreyBackCounts[index] = preyPopulation;
                PredatorBackCounts[index] = predatorPopulation;

                HeadlessPositions[index] = ResolveSectorCenterPosition(state.SectorCoord);
                HeadlessSpeciesID[index] = headlessSpeciesId;
                HeadlessHunger[index] = PackUnitByte(1f - preyPerPredator * invStarvationComfort);
                HeadlessSectorCoord[index] = state.SectorCoord;
                HeadlessSectorID[index] = ResolveSectorId(state.SectorCoord);
            }

            private static int RoundPositiveToInt(float value)
            {
                return (int)(math.max(0f, value) + 0.5f);
            }

            private static byte PackUnitByte(float value)
            {
                return (byte)math.clamp(RoundPositiveToInt(math.saturate(value) * 255f), 0, 255);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BiomassLotkaVolterraJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> PreyFront;
            [ReadOnly] public NativeArray<float> PredatorFront;
            [ReadOnly] public NativeArray<float> CarryingCapacity;
            [ReadOnly] public NativeArray<int2> MacroCellCoords;
            [ReadOnly] public NativeHashMap<long, int> CellIndexByKey;
            public NativeArray<float> PreyBack;
            public NativeArray<float> PredatorBack;
            public NativeArray<float> BiomassSumScratch;
            public float DeltaSeconds;
            public float BirthRate;
            public float PredRate;
            public float FeedRate;
            public float DeathRate;
            public float DiffusionRate;
            public int EnableDiffusion;

            public void Execute(int index)
            {
                float capacity = math.saturate(CarryingCapacity[index]);
                float prey = math.clamp(PreyFront[index], 0f, capacity);
                float predator = math.clamp(PredatorFront[index], 0f, capacity);
                float dt = math.max(0f, DeltaSeconds);

                float dPrey = prey * (BirthRate - (PredRate * predator));
                float dPred = predator * ((FeedRate * prey) - DeathRate);
                float nextPrey = math.clamp(prey + (dPrey * dt), 0f, capacity);
                float nextPredator = math.clamp(predator + (dPred * dt), 0f, capacity);

                if (EnableDiffusion != 0 && DiffusionRate > 0f)
                {
                    int2 coord = MacroCellCoords[index];
                    float neighborPrey = 0f;
                    float neighborPredator = 0f;
                    int neighborCount = 0;
                    AccumulateNeighbor(coord + new int2(1, 0), ref neighborPrey, ref neighborPredator, ref neighborCount);
                    AccumulateNeighbor(coord + new int2(-1, 0), ref neighborPrey, ref neighborPredator, ref neighborCount);
                    AccumulateNeighbor(coord + new int2(0, 1), ref neighborPrey, ref neighborPredator, ref neighborCount);
                    AccumulateNeighbor(coord + new int2(0, -1), ref neighborPrey, ref neighborPredator, ref neighborCount);
                    if (neighborCount > 0)
                    {
                        float invCount = math.rcp(neighborCount);
                        float diffusion01 = math.saturate(DiffusionRate);
                        nextPrey = math.clamp(math.lerp(nextPrey, neighborPrey * invCount, diffusion01), 0f, capacity);
                        nextPredator = math.clamp(math.lerp(nextPredator, neighborPredator * invCount, diffusion01), 0f, capacity);
                    }
                }

                if (!math.isfinite(nextPrey))
                    nextPrey = 0f;
                if (!math.isfinite(nextPredator))
                    nextPredator = 0f;

                PreyBack[index] = nextPrey;
                PredatorBack[index] = nextPredator;
                BiomassSumScratch[index] = nextPrey + nextPredator;
            }

            private void AccumulateNeighbor(
                int2 coord,
                ref float prey,
                ref float predator,
                ref int count)
            {
                if (!CellIndexByKey.TryGetValue(PackBiomassCellKey(coord), out int neighborIndex) ||
                    neighborIndex < 0 ||
                    neighborIndex >= PreyFront.Length)
                {
                    return;
                }

                float capacity = math.saturate(CarryingCapacity[neighborIndex]);
                prey += math.clamp(PreyFront[neighborIndex], 0f, capacity);
                predator += math.clamp(PredatorFront[neighborIndex], 0f, capacity);
                count++;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct HeadlessThresholdMigrationJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SectorPopulationState> States;
            [ReadOnly] public NativeArray<byte> FoodDensityHeatmapR8;
            public NativeArray<float3> Positions;
            public NativeArray<int2> SectorCoord;
            public NativeArray<int> SectorID;
            public int2 FoodDensityHeatmapSize;
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
                    coord = ResolveBestFoodNeighbor(coord, FoodDensityHeatmapR8, FoodDensityHeatmapSize);

                SectorCoord[index] = coord;
                SectorID[index] = ResolveSectorId(coord);
                Positions[index] = ResolveSectorCenterPosition(coord);
            }

            private static int2 ResolveBestFoodNeighbor(
                int2 sectorCoord,
                NativeArray<byte> foodDensityHeatmapR8,
                int2 foodDensityHeatmapSize)
            {
                int2 bestCoord = sectorCoord;
                float bestFoodScore = ResolveMigrationFoodScore(sectorCoord, foodDensityHeatmapR8, foodDensityHeatmapSize);
                int bestTieBucket = MigrationTieNoNeighborBucket;
                EvaluateFoodCandidate(sectorCoord + new int2(1, 0), foodDensityHeatmapR8, foodDensityHeatmapSize, ref bestCoord, ref bestFoodScore, ref bestTieBucket);
                EvaluateFoodCandidate(sectorCoord + new int2(-1, 0), foodDensityHeatmapR8, foodDensityHeatmapSize, ref bestCoord, ref bestFoodScore, ref bestTieBucket);
                EvaluateFoodCandidate(sectorCoord + new int2(0, 1), foodDensityHeatmapR8, foodDensityHeatmapSize, ref bestCoord, ref bestFoodScore, ref bestTieBucket);
                EvaluateFoodCandidate(sectorCoord + new int2(0, -1), foodDensityHeatmapR8, foodDensityHeatmapSize, ref bestCoord, ref bestFoodScore, ref bestTieBucket);
                return bestCoord;
            }

            private static void EvaluateFoodCandidate(
                int2 candidateCoord,
                NativeArray<byte> foodDensityHeatmapR8,
                int2 foodDensityHeatmapSize,
                ref int2 bestCoord,
                ref float bestFoodScore,
                ref int bestTieBucket)
            {
                float foodScore = ResolveMigrationFoodScore(candidateCoord, foodDensityHeatmapR8, foodDensityHeatmapSize);
                int candidateTieBucket = ResolveAupMigrationTieBucket(candidateCoord);
                bool betterFood = foodScore > bestFoodScore + 0.0001f;
                bool equalFood = math.abs(foodScore - bestFoodScore) <= 0.0001f;
                bool betterTie = equalFood && candidateTieBucket < bestTieBucket;
                bool takeCandidate = betterFood || betterTie;
                bestCoord = math.select(bestCoord, candidateCoord, takeCandidate);
                bestFoodScore = math.select(bestFoodScore, foodScore, takeCandidate);
                bestTieBucket = math.select(bestTieBucket, candidateTieBucket, takeCandidate);
            }

            private static float ResolveMigrationFoodScore(
                int2 sectorCoord,
                NativeArray<byte> foodDensityHeatmapR8,
                int2 foodDensityHeatmapSize)
            {
                int biomeId = ResolveBiomeIdForSector(sectorCoord);
                return ResolveSectorFoodDensity01(sectorCoord, biomeId, 0f, 0f, foodDensityHeatmapR8, foodDensityHeatmapSize);
            }

            private static int ResolveAupMigrationTieBucket(int2 candidateCoord)
            {
                unchecked
                {
                    return ((candidateCoord.x * 73856) + (candidateCoord.y * 19349)) & 3;
                }
            }
        }

        [Header("Sector Runtime")]
        [Tooltip("Maximum number of active 1 km sectors tracked in the cold-path population model.")]
        [SerializeField, Min(MinimumSectorCapacity)] private int maxTrackedSectors = 128;
        [Tooltip("Seconds between headless ecosystem solves. 5 seconds = FrostTick.")]
        [SerializeField, Min(1f)] private float coldTickIntervalSeconds = 5f;

        [Header("Cinematic Sector Buckets")]
        [Tooltip("Low-nibble bucket cutoff for deterministic 1 km apex-sector assignment. 3 means buckets 0,1,2 out of 16.")]
        [FormerlySerializedAs("leviathanSectorSpawnChance")]
        [SerializeField, Range(0, ApexSectorBucketCount)] private int apexSectorBucketCutoff = DefaultApexSectorBucketCutoff;
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

        [Header("Biomass Macro Grid")]
        [Tooltip("Maximum number of 50 m biomass macro-cells tracked around active ecology pressure.")]
        [SerializeField, Min(MinimumBiomassCellCapacity)] private int maxTrackedBiomassCells = 512;
        [Tooltip("Initial normalized prey biomass when a macro-cell is first touched.")]
        [SerializeField, Range(0f, 1f)] private float defaultPreyBiomass01 = 0.65f;
        [Tooltip("Initial normalized predator biomass when a macro-cell is first touched.")]
        [SerializeField, Range(0f, 1f)] private float defaultPredatorBiomass01 = 0.35f;
        [Tooltip("Cold-tick neighbor diffusion rate. Disabled automatically on low scalability tier.")]
        [SerializeField, Range(0f, 1f)] private float biomassDiffusionRate = 0.06f;
        [Tooltip("Normalized biomass removed by one generic entity death when trophic class is unknown.")]
        [SerializeField, Range(0f, 1f)] private float entityDeathBiomassPenalty01 = 0.08f;
        [Tooltip("Normalized prey biomass removed by one fish acquisition event.")]
        [SerializeField, Range(0f, 1f)] private float fishAcquisitionPreyPenalty01 = 1f;
        [SerializeField] private float _debugGlobalBiomassSum;
        [SerializeField] private float _debugPreyBiomassSum;
        [SerializeField] private float _debugPredatorBiomassSum;
        [SerializeField] private float _debugFloraOvergrowth01;
        [SerializeField] private int _debugBiomassCellCount;

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
        private NativeArray<float> _preyBiomassFront;
        private NativeArray<float> _preyBiomassBack;
        private NativeArray<float> _predatorBiomassFront;
        private NativeArray<float> _predatorBiomassBack;
        private NativeArray<float> _biomassCarryingCapacity;
        private NativeArray<float> _biomassSumScratch;
        private NativeArray<int2> _biomassMacroCellCoords;
        private NativeArray<byte> _biomassCellFlags;
        private NativeArray<BiomassImpactEvent> _pendingBiomassImpacts;
        private NativeArray<BiomassTelemetryEntry> _biomassBlackBox;
        private NativeList<EcosystemBiomassSaveRun> _saveSnapshotBiomassRuns;
        private NativeHashMap<long, int> _biomassIndexByKey;
        private HeadlessEntitySoA _headlessEntities;
        private NativeArray<byte> _sectorFoodHeatmapR8;
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
        private int _activeBiomassCellCount;
        private int _pendingBiomassImpactCount;
        private int _lastBiomassSignalDrainFrame;
        private int _lastScannerWarningFrame;
        private int _biomassBlackBoxCursor;
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
        private int2 _sectorFoodHeatmapSize;

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
            _preyBiomassFront.IsCreated &&
            _preyBiomassBack.IsCreated &&
            _predatorBiomassFront.IsCreated &&
            _predatorBiomassBack.IsCreated &&
            _biomassCarryingCapacity.IsCreated &&
            _biomassIndexByKey.IsCreated &&
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

        /// <summary>
        /// Binds a non-owned R8 sector-food heatmap generated by the world geology pass.
        /// </summary>
        /// <param name="heatmapR8">One byte per sector sample, 0-255 normalized food capacity.</param>
        /// <param name="width">Power-of-two heatmap width.</param>
        /// <param name="height">Power-of-two heatmap height.</param>
        /// <remarks>
        /// Caller owns allocation and disposal. Power-of-two dimensions keep Burst sampling on bit masks instead of modulo.
        /// </remarks>
        public void BindSectorFoodDensityHeatmap(NativeArray<byte> heatmapR8, int width, int height)
        {
            if (!heatmapR8.IsCreated ||
                width <= 0 ||
                height <= 0 ||
                !IsPowerOfTwo(width) ||
                !IsPowerOfTwo(height))
            {
                _sectorFoodHeatmapR8 = default;
                _sectorFoodHeatmapSize = default;
                return;
            }

            long requiredLength = (long)width * height;
            if (heatmapR8.Length < requiredLength)
            {
                _sectorFoodHeatmapR8 = default;
                _sectorFoodHeatmapSize = default;
                return;
            }

            _sectorFoodHeatmapR8 = heatmapR8;
            _sectorFoodHeatmapSize = new int2(width, height);
        }

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

            float lightExposure01 = math.saturate(1f - (depthMeters * InvLightFalloffDepthMeters));
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
                selectionMultiplier = 0.65f + (1.2f * scentPressure01);
                if (TryResolveEclipsePredatorTier0SelectionBoost(worldPosition, out float sharkEclipseSelectionBoost))
                    selectionMultiplier *= sharkEclipseSelectionBoost;

                selectionMultiplier *= ResolvePlayerStressSpawnWeight(archetype, playerStress01);
                selectionMultiplier *= ResolveBiomassSpawnSelectionWeight(archetype, worldPosition);
                selectionMultiplier *= ResolveSpawnCreditSelectionWeight(archetype);
                return selectionMultiplier > 0f;
            }

            if (archetype.isAggressive || archetype.roleType == CreatureRoleType.Hunter || archetype.roleType == CreatureRoleType.Leviathan)
            {
                selectionMultiplier = 0.9f + (0.45f * scentPressure01);
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
                    selectionMultiplier *= 1f + ((math.max(CorpseSpawnSelectionScale, WhaleFallScavengerSpawnMultiplier) - 1f) * corpseInfluence01);
            }

            selectionMultiplier *= ResolveBiomassSpawnSelectionWeight(archetype, worldPosition);
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
            float recoveryScale = 1.15f - (0.6f * stress01);
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

        private float ResolveBiomassSpawnSelectionWeight(CreatureArchetypeData archetype, Vector3 worldPosition)
        {
            if (archetype == null ||
                !TryGetBiomassAvailability(worldPosition, out float preyBiomass01, out float predatorBiomass01, out _))
            {
                return 1f;
            }

            if (IsApexRole(archetype))
                return predatorBiomass01 < 0.1f ? 0.5f : math.max(0.05f, predatorBiomass01);

            if (IsPredatorOrApex(archetype))
                return math.max(0.05f, predatorBiomass01);

            float preyWeight = math.max(0.05f, preyBiomass01);
            return preyBiomass01 > 0.9f ? preyWeight * 2f : preyWeight;
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

            float t = math.saturate((stress01 - HighPlayerStressThreshold01) * InvHighPlayerStressRange01);
            if (IsApexRole(archetype))
                return 1f - (0.92f * t);

            if (IsPredatorOrApex(archetype))
                return 1f - (0.7f * t);

            if (archetype != null && archetype.roleType == CreatureRoleType.Ambient)
                return 1f + (0.45f * t);

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

            _apexSpawnGateHits[0] = default;

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

            byte blocked = PackBooleanByte(_apexSpawnGateHits[0].collider != null);

            _apexSpawnGateCachedCell = _apexSpawnGatePendingCell;
            _apexSpawnGateCachedBlocked = blocked;
            _apexSpawnGateHasCachedResult = true;
            _apexSpawnGateScheduled = false;
            _apexSpawnGateHandle = default;
        }

        private static int3 QuantizeApexSpawnGateCell(Vector3 worldPosition)
        {
            float invCellSize = InvApexSpawnGateCacheCellSizeMeters;
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
            SargassumMicroFaunaBoids microFaunaBoids = GlobalRegistry.SargassumMicroFauna;
            if (microFaunaBoids != null)
                microFaunaBoids.RegisterWhaleFallScavengerBurst(worldPosition, uniqueInstanceUid, Time.time);

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
            if (_saveSnapshotBiomassRuns.IsCreated)
                _saveSnapshotBiomassRuns.Clear();
            if (!IsInitialized)
                return;

            CompleteScheduledSimulation(forceComplete: true);
            ApplyPendingBiomassImpacts();
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

            CaptureBiomassSaveRuns();
            if (_saveSnapshotBiomassRuns.IsCreated)
            {
                for (int runIndex = 0; runIndex < _saveSnapshotBiomassRuns.Length && _saveSnapshotSectors.Length < _saveSnapshotSectors.Capacity; runIndex++)
                    _saveSnapshotSectors.AddNoResize(PackBiomassRunAsSectorRecord(_saveSnapshotBiomassRuns[runIndex]));
            }
        }

        internal NativeArray<EcosystemSectorSaveRecord> GetSaveSnapshotArray()
        {
            return _saveSnapshotSectors.IsCreated ? _saveSnapshotSectors.AsArray() : default;
        }

        internal NativeArray<EcosystemBiomassSaveRun> GetBiomassSaveSnapshotArray()
        {
            return _saveSnapshotBiomassRuns.IsCreated ? _saveSnapshotBiomassRuns.AsArray() : default;
        }

        internal unsafe void RestoreFromLoadedRecords(EcosystemSectorSaveRecord[] loadedRecords)
        {
            if (!IsInitialized)
                return;

            CompleteScheduledSimulation(forceComplete: true);
            _sectorIndexByKey.Clear();
            _biomassIndexByKey.Clear();
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
            ClearBiomassRuntimeState();

            int recordCount = loadedRecords != null ? loadedRecords.Length : 0;
            _activeSectorCount = 0;
            for (int recordIndex = 0; recordIndex < recordCount; recordIndex++)
            {
                EcosystemSectorSaveRecord saveRecord = loadedRecords[recordIndex];
                if (IsBiomassSaveRecord(in saveRecord))
                {
                    RestoreBiomassSaveRun(in saveRecord);
                    continue;
                }

                if (_activeSectorCount >= _sectorFrontStates.Length)
                    continue;

                int biomeId = ResolveBiomeIdForSector(saveRecord.SectorCoord);
                UnpackPopulationCounts(saveRecord.PackedPopulations, out int preyPopulation, out int predatorPopulation);
                UnpackAdaptationTraits(
                    saveRecord.PackedAdaptation,
                    maximumSpeedMultiplier,
                    out float fitness,
                    out float speedMultiplier,
                    out float camouflageIndex);
                SectorPopulationState restoredState = new SectorPopulationState
                {
                    SectorCoord = saveRecord.SectorCoord,
                    PreyPopulation = preyPopulation,
                    PredatorPopulation = predatorPopulation,
                    HarvestPressure = 0f,
                    Fitness = fitness,
                    SpeedMultiplier = speedMultiplier,
                    CamouflageIndex = camouflageIndex,
                    FoodDensity01 = ResolveRuntimeSectorFoodDensity01(saveRecord.SectorCoord, biomeId, 0f, 0f),
                    TemperatureScore01 = ResolveSectorTemperatureScore01(saveRecord.SectorCoord, biomeId),
                    Oxygen01 = 1f,
                    AlgaeBloom01 = 0f,
                    PreyPopulationRounded = preyPopulation,
                    PredatorPopulationRounded = predatorPopulation,
                    BiomeId = biomeId,
                    ApexInSector = PackBooleanByte(predatorPopulation > 0)
                };

                int sectorIndex = _activeSectorCount;
                _sectorFrontStates[sectorIndex] = restoredState;
                _sectorBackStates[sectorIndex] = restoredState;
                WriteHeadlessSlot(sectorIndex, in restoredState);
                _sectorIndexByKey.TryAdd(PackSectorKey(saveRecord.SectorCoord), sectorIndex);
                _activeSectorCount++;
            }

            PublishBiomassTelemetryAndEvents();
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
            DrainBiomassSignalSnapshots();
            PublishScannerEcologyWarningIfNeeded();
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
            if (TryGetBiomassAvailability(worldPosition, out float preyBiomass01, out float predatorBiomass01, out _))
            {
                sample.PreyBiomass01 = preyBiomass01;
                sample.PredatorBiomass01 = predatorBiomass01;
                sample.FloraOvergrowth01 = ResolveFloraOvergrowth01(preyBiomass01);
            }
            return true;
        }

        public bool TryGetBiomassAvailability(
            Vector3 worldPosition,
            out float preyBiomass01,
            out float predatorBiomass01,
            out float carryingCapacity01)
        {
            preyBiomass01 = 0f;
            predatorBiomass01 = 0f;
            carryingCapacity01 = 0f;
            if (!IsInitialized || HasPendingSimulationJob())
                return false;

            int slotIndex = ResolveOrCreateBiomassCellSlot(QuantizeBiomassMacroCell(worldPosition), seedWithBaseline: true);
            if (slotIndex < 0)
                return false;

            float capacity = math.max(0.0001f, _biomassCarryingCapacity[slotIndex]);
            preyBiomass01 = math.saturate(_preyBiomassFront[slotIndex] * math.rcp(capacity));
            predatorBiomass01 = math.saturate(_predatorBiomassFront[slotIndex] * math.rcp(capacity));
            carryingCapacity01 = math.saturate(capacity);
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

        public bool TryGetApexPredatorThreat(Vector3 worldPosition, float radiusMeters, out float proximity01)
        {
            proximity01 = 0f;
            if (!IsInitialized ||
                radiusMeters <= 0f ||
                !math.isfinite(radiusMeters) ||
                !math.all(math.isfinite(new float3(worldPosition.x, worldPosition.y, worldPosition.z))))
            {
                return false;
            }

            int hitCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                worldPosition,
                radiusMeters,
                SpatialTargetKind.Bioform,
                _apexThreatProbeHits);
            if (hitCount <= 0)
                return false;

            float radiusSq = math.max(0.0001f, radiusMeters * radiusMeters);
            float bestDistanceSq = radiusSq;
            bool found = false;
            int count = math.min(hitCount, ApexThreatProbeHitCapacity);
            for (int i = 0; i < count; i++)
            {
                SpatialQueryHit hit = _apexThreatProbeHits[i];
                FaunaBrain brain = hit.Owner as FaunaBrain;
                if (brain == null || brain.IsDead || !brain.IsApexPredatorRuntime)
                    continue;

                bestDistanceSq = math.min(bestDistanceSq, math.max(0f, hit.DistanceSqr));
                found = true;
            }

            if (!found)
                return false;

            proximity01 = math.saturate(1f - bestDistanceSq * math.rcp(radiusSq));
            return true;
        }

        /// <summary>
        /// Registers visible prey consumption for strain only. Sector population is fixed by the cinematic table.
        /// </summary>
        public void ReportPredation(Vector3 worldPosition, int preyConsumed)
        {
            if (!IsInitialized || preyConsumed <= 0)
                return;

            GlobalRegistry.EnvironmentalStrain?.AccumulatePredationStrain(worldPosition, preyConsumed);
            QueueOrApplyBiomassImpact(worldPosition, BiomassImpactKindPredation, preyConsumed * math.rcp(math.max(1f, maxPreyPopulation)));
            if (HasPendingSimulationJob())
                return;

            int slotIndex = ResolveOrCreateSectorSlot(QuantizeSector(worldPosition), seedWithBaseline: true);
            if (slotIndex < 0)
                return;

            SectorPopulationState state = _sectorFrontStates[slotIndex];
            state.PreyPopulationRounded = math.max(0, state.PreyPopulationRounded - preyConsumed);
            state.PreyPopulation = state.PreyPopulationRounded;
            state.HarvestPressure = math.saturate(state.HarvestPressure + preyConsumed * math.rcp(math.max(1f, maxPreyPopulation)));
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
            state.ApexInSector = PackBooleanByte(state.PredatorPopulationRounded > 0);
            int preyBloom = math.max(1, (int)(maxPreyPopulation * InvAlgaeBloomPreyGrowthDivisor));
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
            QueueOrApplyBiomassImpact(worldPosition, BiomassImpactKindApexKill, 1f);
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

            return math.saturate(_eclipsePredatorMigrationIntensity01 + ((1f - _eclipsePredatorMigrationIntensity01) * eclipsePredatorLightSuppression));
        }

        private void SanitizeSettings()
        {
            maxTrackedSectors = math.max(MinimumSectorCapacity, maxTrackedSectors);
            coldTickIntervalSeconds = FrostTickIntervalSeconds;
            apexSectorBucketCutoff = math.clamp(apexSectorBucketCutoff, 0, ApexSectorBucketCount);
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
            maxTrackedBiomassCells = math.max(MinimumBiomassCellCapacity, maxTrackedBiomassCells);
            defaultPreyBiomass01 = math.clamp(defaultPreyBiomass01, 0f, 1f);
            defaultPredatorBiomass01 = math.clamp(defaultPredatorBiomass01, 0f, 1f);
            biomassDiffusionRate = math.clamp(biomassDiffusionRate, 0f, 1f);
            entityDeathBiomassPenalty01 = math.clamp(entityDeathBiomassPenalty01, 0f, 1f);
            fishAcquisitionPreyPenalty01 = math.clamp(fishAcquisitionPreyPenalty01, 0f, 1f);
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
            float invStarvationComfort = math.rcp(math.max(1f, starvationComfortPreyPerPredator));
            for (int i = 0; i < count; i++)
            {
                SectorPopulationState state = _sectorFrontStates[i];
                if (state.PredatorPopulationRounded <= 0)
                {
                    pressure01 = math.max(pressure01, state.AlgaeBloom01 * 0.35f);
                    continue;
                }

                float preyPerPredator = state.PreyPopulationRounded * math.rcp(math.max(1f, state.PredatorPopulationRounded));
                float starvation01 = math.saturate(1f - (preyPerPredator * invStarvationComfort));
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
            // COLD ALLOC: NativeArray<float>[maxTrackedBiomassCells] - 50 m prey biomass front buffer - owner: EcosystemDirector
            _preyBiomassFront = new NativeArray<float>(maxTrackedBiomassCells, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[maxTrackedBiomassCells] - 50 m prey biomass back buffer - owner: EcosystemDirector
            _preyBiomassBack = new NativeArray<float>(maxTrackedBiomassCells, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[maxTrackedBiomassCells] - 50 m predator biomass front buffer - owner: EcosystemDirector
            _predatorBiomassFront = new NativeArray<float>(maxTrackedBiomassCells, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[maxTrackedBiomassCells] - 50 m predator biomass back buffer - owner: EcosystemDirector
            _predatorBiomassBack = new NativeArray<float>(maxTrackedBiomassCells, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[maxTrackedBiomassCells] - per-biome biomass carrying capacity - owner: EcosystemDirector
            _biomassCarryingCapacity = new NativeArray<float>(maxTrackedBiomassCells, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float>[maxTrackedBiomassCells] - per-cell biomass sum scratch for main-thread reduction - owner: EcosystemDirector
            _biomassSumScratch = new NativeArray<float>(maxTrackedBiomassCells, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<int2>[maxTrackedBiomassCells] - 50 m macro-cell coordinates - owner: EcosystemDirector
            _biomassMacroCellCoords = new NativeArray<int2>(maxTrackedBiomassCells, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[maxTrackedBiomassCells] - biomass event latch flags - owner: EcosystemDirector
            _biomassCellFlags = new NativeArray<byte>(maxTrackedBiomassCells, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<BiomassImpactEvent>[128] - deferred biomass impacts while Burst owns front buffers - owner: EcosystemDirector
            _pendingBiomassImpacts = new NativeArray<BiomassImpactEvent>(BiomassImpactQueueCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<BiomassTelemetryEntry>[300] - ecology blackbox circular buffer - owner: EcosystemDirector
            _biomassBlackBox = new NativeArray<BiomassTelemetryEntry>(BiomassBlackBoxCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeHashMap<long,int>[maxTrackedBiomassCells] - packed 50 m macro-cell key to slot lookup - owner: EcosystemDirector
            _biomassIndexByKey = new NativeHashMap<long, int>(maxTrackedBiomassCells, Allocator.Persistent);
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
            // COLD ALLOC: NativeArray<RaycastHit>[1] - non-alloc Apex voxel-wall spawn gate result - owner: EcosystemDirector
            _apexSpawnGateHits = new NativeArray<RaycastHit>(ApexSpawnGateMaxHits, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<float4>[32] - global flora predator AUP upload staging buffer - owner: EcosystemDirector
            _floraPredatorAupUpload = new NativeArray<float4>(FloraPredatorAupBufferCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeList<EcosystemSectorSaveRecord>[maxTrackedSectors] - packed ecosystem persistence snapshot staging buffer - owner: EcosystemDirector
            _saveSnapshotSectors = new NativeList<EcosystemSectorSaveRecord>(maxTrackedSectors + maxTrackedBiomassCells, Allocator.Persistent);
            // COLD ALLOC: NativeList<EcosystemBiomassSaveRun>[maxTrackedBiomassCells] - quantized RLE biomass save bridge - owner: EcosystemDirector
            _saveSnapshotBiomassRuns = new NativeList<EcosystemBiomassSaveRun>(maxTrackedBiomassCells, Allocator.Persistent);
            RegisterNativeMemorySentinelAllocations();
            // COLD ALLOC: FaunaBrain[16] - managed Apex brain lookup paired with Burst overlap result indices - owner: EcosystemDirector
            _apexTerritoryBrains = new FaunaBrain[ApexTerritoryOverlapCandidateCapacity];
            _floraPredatorAupBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(FloraPredatorAupBufferCapacity); // COLD ALLOC: GraphicsBuffer[32] - global flora predator AUP StructuredBuffer - owner: EcosystemDirector
            _floraPredatorAupGlobalsDirty = true;
            PublishFloraPredatorAupGlobals(0);
            Shader.SetGlobalColor(_GlobalOceanPanicColorId, new Color(1f, 0.05f, 0.035f, 1f));
            Shader.SetGlobalFloat(_ApexInSectorId, 0f);
            Shader.SetGlobalFloat(_BiomassOvergrowthId, 0f);
            PublishGlobalOceanPanic(0f);
            _activeSectorCount = 0;
            _activeBiomassCellCount = 0;
            _pendingBiomassImpactCount = 0;
            _lastBiomassSignalDrainFrame = -1;
            _lastScannerWarningFrame = -1024;
            _biomassBlackBoxCursor = 0;
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
            _debugBiomassCellCount = 0;
            _debugGlobalBiomassSum = 0f;
            _debugPreyBiomassSum = 0f;
            _debugPredatorBiomassSum = 0f;
            _debugFloraOvergrowth01 = 0f;
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
            if (_preyBiomassFront.IsCreated)
                _preyBiomassFront.Dispose(disposeDependency);
            if (_preyBiomassBack.IsCreated)
                _preyBiomassBack.Dispose(disposeDependency);
            if (_predatorBiomassFront.IsCreated)
                _predatorBiomassFront.Dispose(disposeDependency);
            if (_predatorBiomassBack.IsCreated)
                _predatorBiomassBack.Dispose(disposeDependency);
            if (_biomassCarryingCapacity.IsCreated)
                _biomassCarryingCapacity.Dispose(disposeDependency);
            if (_biomassSumScratch.IsCreated)
                _biomassSumScratch.Dispose(disposeDependency);
            if (_biomassMacroCellCoords.IsCreated)
                _biomassMacroCellCoords.Dispose(disposeDependency);
            if (_biomassCellFlags.IsCreated)
                _biomassCellFlags.Dispose(disposeDependency);
            if (_pendingBiomassImpacts.IsCreated)
                _pendingBiomassImpacts.Dispose(disposeDependency);
            if (_biomassBlackBox.IsCreated)
                _biomassBlackBox.Dispose(disposeDependency);
            if (_biomassIndexByKey.IsCreated)
                _biomassIndexByKey.Dispose(disposeDependency);
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
            if (_saveSnapshotBiomassRuns.IsCreated)
                _saveSnapshotBiomassRuns.Dispose(disposeDependency);
            ReleaseBuffer(ref _floraPredatorAupBuffer);
            Shader.SetGlobalInt(_PredatorAUPCountId, 0);
            Shader.SetGlobalFloat(_BiomassOvergrowthId, 0f);
            _lastPublishedFloraPredatorAupCount = 0;
            _floraPredatorAupGlobalsDirty = true;
            PublishApexPresenceFake(false);

            _sectorFrontStates = default;
            _sectorBackStates = default;
            _preyFrontCounts = default;
            _preyBackCounts = default;
            _predatorFrontCounts = default;
            _predatorBackCounts = default;
            _preyBiomassFront = default;
            _preyBiomassBack = default;
            _predatorBiomassFront = default;
            _predatorBiomassBack = default;
            _biomassCarryingCapacity = default;
            _biomassSumScratch = default;
            _biomassMacroCellCoords = default;
            _biomassCellFlags = default;
            _pendingBiomassImpacts = default;
            _biomassBlackBox = default;
            _biomassIndexByKey = default;
            _headlessEntities = default;
            _sectorFoodHeatmapR8 = default;
            _sectorFoodHeatmapSize = default;
            _sectorIndexByKey = default;
            _apexTerritorySamples = default;
            _apexTerritoryOverlapResults = default;
            _apexSpawnGateCommands = default;
            _apexSpawnGateHits = default;
            _floraPredatorAupUpload = default;
            _saveSnapshotSectors = default;
            _saveSnapshotBiomassRuns = default;
            _apexTerritoryBrains = null;
            _activeSectorCount = 0;
            _activeBiomassCellCount = 0;
            _pendingBiomassImpactCount = 0;
            _lastBiomassSignalDrainFrame = -1;
            _lastScannerWarningFrame = -1024;
            _biomassBlackBoxCursor = 0;
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
            _debugBiomassCellCount = 0;
            _debugGlobalBiomassSum = 0f;
            _debugPreyBiomassSum = 0f;
            _debugPredatorBiomassSum = 0f;
            _debugFloraOvergrowth01 = 0f;
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
            NativeMemorySentinel.RegisterNativeArray(_preyBiomassFront, NativeMemoryOwner, nameof(_preyBiomassFront), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_preyBiomassBack, NativeMemoryOwner, nameof(_preyBiomassBack), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_predatorBiomassFront, NativeMemoryOwner, nameof(_predatorBiomassFront), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_predatorBiomassBack, NativeMemoryOwner, nameof(_predatorBiomassBack), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_biomassCarryingCapacity, NativeMemoryOwner, nameof(_biomassCarryingCapacity), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_biomassSumScratch, NativeMemoryOwner, nameof(_biomassSumScratch), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_biomassMacroCellCoords, NativeMemoryOwner, nameof(_biomassMacroCellCoords), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_biomassCellFlags, NativeMemoryOwner, nameof(_biomassCellFlags), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_pendingBiomassImpacts, NativeMemoryOwner, nameof(_pendingBiomassImpacts), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_biomassBlackBox, NativeMemoryOwner, nameof(_biomassBlackBox), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeHashMap(_biomassIndexByKey, NativeMemoryOwner, nameof(_biomassIndexByKey), NativeMemoryLifetime);
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
            NativeMemorySentinel.RegisterNativeList(_saveSnapshotBiomassRuns, NativeMemoryOwner, nameof(_saveSnapshotBiomassRuns), NativeMemoryLifetime);
        }

        private void UnregisterNativeMemorySentinelAllocations()
        {
            NativeMemorySentinel.UnregisterNativeArray(_sectorFrontStates);
            NativeMemorySentinel.UnregisterNativeArray(_sectorBackStates);
            NativeMemorySentinel.UnregisterNativeArray(_preyFrontCounts);
            NativeMemorySentinel.UnregisterNativeArray(_preyBackCounts);
            NativeMemorySentinel.UnregisterNativeArray(_predatorFrontCounts);
            NativeMemorySentinel.UnregisterNativeArray(_predatorBackCounts);
            NativeMemorySentinel.UnregisterNativeArray(_preyBiomassFront);
            NativeMemorySentinel.UnregisterNativeArray(_preyBiomassBack);
            NativeMemorySentinel.UnregisterNativeArray(_predatorBiomassFront);
            NativeMemorySentinel.UnregisterNativeArray(_predatorBiomassBack);
            NativeMemorySentinel.UnregisterNativeArray(_biomassCarryingCapacity);
            NativeMemorySentinel.UnregisterNativeArray(_biomassSumScratch);
            NativeMemorySentinel.UnregisterNativeArray(_biomassMacroCellCoords);
            NativeMemorySentinel.UnregisterNativeArray(_biomassCellFlags);
            NativeMemorySentinel.UnregisterNativeArray(_pendingBiomassImpacts);
            NativeMemorySentinel.UnregisterNativeArray(_biomassBlackBox);
            NativeMemorySentinel.UnregisterNativeHashMap(NativeMemoryOwner, nameof(_biomassIndexByKey));
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
            NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, nameof(_saveSnapshotBiomassRuns));
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
            int2 macroCell = QuantizeBiomassMacroCell(in playerAup);
            ResolveOrCreateBiomassCellSlot(macroCell, seedWithBaseline: true);
            ResolveOrCreateBiomassCellSlot(macroCell + new int2(1, 0), seedWithBaseline: false);
            ResolveOrCreateBiomassCellSlot(macroCell + new int2(-1, 0), seedWithBaseline: false);
            ResolveOrCreateBiomassCellSlot(macroCell + new int2(0, 1), seedWithBaseline: false);
            ResolveOrCreateBiomassCellSlot(macroCell + new int2(0, -1), seedWithBaseline: false);
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

            selectionBoost = 1f + ((eclipsePredatorTier0SelectionBoost - 1f) * _eclipsePredatorMigrationIntensity01);
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
                FoodDensityHeatmapR8 = _sectorFoodHeatmapR8,
                BackStates = _sectorBackStates,
                PreyBackCounts = _preyBackCounts,
                PredatorBackCounts = _predatorBackCounts,
                HeadlessPositions = _headlessEntities.Positions,
                HeadlessSpeciesID = _headlessEntities.SpeciesID,
                HeadlessHunger = _headlessEntities.Hunger,
                HeadlessSectorCoord = _headlessEntities.SectorCoord,
                HeadlessSectorID = _headlessEntities.SectorID,
                FoodDensityHeatmapSize = _sectorFoodHeatmapSize,
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

            int sectorBatchSize = ResolveSectorJobBatchSize(_activeSectorCount);
            _scheduledSolveHandle = solveJob.Schedule(_activeSectorCount, sectorBatchSize);
            var migrationJob = new HeadlessThresholdMigrationJob
            {
                States = _sectorBackStates,
                FoodDensityHeatmapR8 = _sectorFoodHeatmapR8,
                Positions = _headlessEntities.Positions,
                SectorCoord = _headlessEntities.SectorCoord,
                SectorID = _headlessEntities.SectorID,
                FoodDensityHeatmapSize = _sectorFoodHeatmapSize,
                MigrationFoodThreshold01 = migrationFoodThreshold01,
                MigrationPredatorTolerance = migrationPredatorTolerance
            };
            _scheduledSolveHandle = migrationJob.Schedule(_activeSectorCount, sectorBatchSize, _scheduledSolveHandle);
            if (_activeBiomassCellCount > 0)
            {
                var biomassJob = new BiomassLotkaVolterraJob
                {
                    PreyFront = _preyBiomassFront,
                    PredatorFront = _predatorBiomassFront,
                    CarryingCapacity = _biomassCarryingCapacity,
                    MacroCellCoords = _biomassMacroCellCoords,
                    CellIndexByKey = _biomassIndexByKey,
                    PreyBack = _preyBiomassBack,
                    PredatorBack = _predatorBiomassBack,
                    BiomassSumScratch = _biomassSumScratch,
                    DeltaSeconds = coldTickIntervalSeconds,
                    BirthRate = preyBirthRatePerSecond,
                    PredRate = predationRatePerSecond,
                    FeedRate = predatorGrowthRatePerSecond,
                    DeathRate = predatorDeathRatePerSecond,
                    DiffusionRate = biomassDiffusionRate,
                    EnableDiffusion = ResolveBiomassDiffusionEnabled() ? 1 : 0
                };
                _scheduledSolveHandle = biomassJob.Schedule(_activeBiomassCellCount, ResolveBiomassJobBatchSize(_activeBiomassCellCount), _scheduledSolveHandle);
            }
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
            if (_activeBiomassCellCount > 0)
            {
                NativeArray<float> preyBiomassSwap = _preyBiomassFront;
                _preyBiomassFront = _preyBiomassBack;
                _preyBiomassBack = preyBiomassSwap;
                NativeArray<float> predatorBiomassSwap = _predatorBiomassFront;
                _predatorBiomassFront = _predatorBiomassBack;
                _predatorBiomassBack = predatorBiomassSwap;
            }
            _solveScheduled = false;
            ApplyPendingBiomassImpacts();
            PublishBiomassTelemetryAndEvents();
            RefreshStarvationPressure();
            _populationSolvePendingHibernationSync = true;
            _debugHeadlessSectorCount = _activeSectorCount;
            _debugBiomassCellCount = _activeBiomassCellCount;
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

            _scheduledApexTerritoryOverlapHandle = overlapJob.Schedule(sampleCount, ResolveApexTerritoryBatchSize(sampleCount));
            _scheduledApexTerritoryOverlapCount = sampleCount;
            _apexTerritoryOverlapScheduled = true;
        }

        private static int ResolveSectorJobBatchSize(int count)
        {
            if (count <= 16)
                return 1;

            if (count <= 64)
                return 8;

            return 64;
        }

        private static int ResolveBiomassJobBatchSize(int count)
        {
            if (count <= 32)
                return 4;

            if (count <= 128)
                return 16;

            return 64;
        }

        private static int ResolveApexTerritoryBatchSize(int count)
        {
            return count <= 4 ? 1 : 4;
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
            ResolveBucketTablePopulation(
                sectorCoord,
                apexSectorBucketCutoff,
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
            state.FoodDensity01 = ResolveRuntimeSectorFoodDensity01(sectorCoord, biomeId, 0f, 0f);
            state.TemperatureScore01 = ResolveSectorTemperatureScore01(sectorCoord, biomeId);
            state.Oxygen01 = 1f;
            state.AlgaeBloom01 = 0f;
            state.PreyPopulationRounded = preyPopulation;
            state.PredatorPopulationRounded = predatorPopulation;
            state.BiomeId = biomeId;
            state.ApexInSector = PackBooleanByte(predatorPopulation > 0);
            return state;
        }

        private int ResolveOrCreateBiomassCellSlot(int2 macroCellCoord, bool seedWithBaseline = true)
        {
            long packedKey = PackBiomassCellKey(macroCellCoord);
            if (_biomassIndexByKey.TryGetValue(packedKey, out int existingSlot))
                return existingSlot;

            if (_activeBiomassCellCount >= _preyBiomassFront.Length)
                return -1;

            int slotIndex = _activeBiomassCellCount;
            _activeBiomassCellCount++;
            _biomassIndexByKey.TryAdd(packedKey, slotIndex);
            _biomassMacroCellCoords[slotIndex] = macroCellCoord;

            float carryingCapacity01 = ResolveBiomassCarryingCapacity01(macroCellCoord);
            float seedScale = seedWithBaseline ? 1f : 0.5f;
            float prey = math.clamp(defaultPreyBiomass01 * carryingCapacity01 * seedScale, 0f, carryingCapacity01);
            float predator = math.clamp(defaultPredatorBiomass01 * carryingCapacity01 * seedScale, 0f, carryingCapacity01);
            _preyBiomassFront[slotIndex] = prey;
            _preyBiomassBack[slotIndex] = prey;
            _predatorBiomassFront[slotIndex] = predator;
            _predatorBiomassBack[slotIndex] = predator;
            _biomassCarryingCapacity[slotIndex] = carryingCapacity01;
            _biomassSumScratch[slotIndex] = prey + predator;
            _biomassCellFlags[slotIndex] = 0;
            return slotIndex;
        }

        private float ResolveBiomassCarryingCapacity01(int2 macroCellCoord)
        {
            int2 sectorCoord = new int2(
                FloorDiv(macroCellCoord.x, (int)(SectorEdgeLengthMeters * InvBiomassMacroCellSizeMeters)),
                FloorDiv(macroCellCoord.y, (int)(SectorEdgeLengthMeters * InvBiomassMacroCellSizeMeters)));
            int biomeId = ResolveBiomeIdForSector(sectorCoord);
            float food01 = ResolveRuntimeSectorFoodDensity01(sectorCoord, biomeId, 0f, 0f);
            float biomeCapacityBias = (math.select(0f, 0.08f, biomeId == 1)) - (math.select(0f, 0.05f, biomeId == 2));
            return math.clamp(food01 + biomeCapacityBias, 0.1f, 1f);
        }

        private static int FloorDiv(int value, int divisor)
        {
            divisor = math.max(1, divisor);
            int quotient = value / divisor;
            int remainder = value % divisor;
            return quotient - math.select(0, 1, remainder != 0 && ((remainder < 0) != (divisor < 0)));
        }

        private void QueueOrApplyBiomassImpact(Vector3 worldPosition, byte kind, float amount)
        {
            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(worldPosition);
            QueueOrApplyBiomassImpact(in aup, kind, amount);
        }

        private void QueueOrApplyBiomassImpact(in AbsoluteUniversePosition positionAup, byte kind, float amount)
        {
            if (!math.isfinite(amount) || amount <= 0f)
                return;

            QueueOrApplyBiomassImpact(QuantizeBiomassMacroCell(in positionAup), kind, amount);
        }

        private void QueueOrApplyBiomassImpact(int2 macroCellCoord, byte kind, float amount)
        {
            if (!IsInitialized || !math.isfinite(amount) || amount <= 0f)
                return;

            if (HasPendingSimulationJob())
            {
                if (_pendingBiomassImpactCount >= _pendingBiomassImpacts.Length)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(
                        _BiomassTelemetryHash,
                        _EcosystemDirectorContextHash,
                        _pendingBiomassImpactCount);
                    return;
                }

                _pendingBiomassImpacts[_pendingBiomassImpactCount++] = new BiomassImpactEvent
                {
                    MacroCellCoord = macroCellCoord,
                    Amount = math.saturate(amount),
                    Kind = kind
                };
                return;
            }

            ApplyBiomassImpact(macroCellCoord, kind, amount);
        }

        private void ApplyPendingBiomassImpacts()
        {
            int count = math.min(_pendingBiomassImpactCount, _pendingBiomassImpacts.IsCreated ? _pendingBiomassImpacts.Length : 0);
            _pendingBiomassImpactCount = 0;
            for (int i = 0; i < count; i++)
            {
                BiomassImpactEvent impact = _pendingBiomassImpacts[i];
                ApplyBiomassImpact(impact.MacroCellCoord, impact.Kind, impact.Amount);
                _pendingBiomassImpacts[i] = default;
            }
        }

        private void ApplyBiomassImpact(int2 macroCellCoord, byte kind, float amount)
        {
            int slotIndex = ResolveOrCreateBiomassCellSlot(macroCellCoord, seedWithBaseline: true);
            if (slotIndex < 0)
                return;

            float capacity = math.max(0.0001f, _biomassCarryingCapacity[slotIndex]);
            float prey = math.clamp(_preyBiomassFront[slotIndex], 0f, capacity);
            float predator = math.clamp(_predatorBiomassFront[slotIndex], 0f, capacity);
            float impact = math.saturate(amount) * capacity;

            switch (kind)
            {
                case BiomassImpactKindFishing:
                    prey = math.max(0f, prey - impact);
                    break;
                case BiomassImpactKindApexKill:
                    predator = math.max(0f, predator - math.max(impact, capacity));
                    break;
                case BiomassImpactKindPredation:
                    prey = math.max(0f, prey - impact);
                    predator = math.min(capacity, predator + (impact * 0.1f));
                    break;
                default:
                    if (predator > 0.001f)
                        predator = math.max(0f, predator - impact);
                    else
                        prey = math.max(0f, prey - (impact * 0.5f));
                    break;
            }

            _preyBiomassFront[slotIndex] = prey;
            _preyBiomassBack[slotIndex] = prey;
            _predatorBiomassFront[slotIndex] = predator;
            _predatorBiomassBack[slotIndex] = predator;
            _biomassSumScratch[slotIndex] = prey + predator;
        }

        private void DrainBiomassSignalSnapshots()
        {
            int frame = Time.frameCount;
            if (_lastBiomassSignalDrainFrame == frame)
                return;

            _lastBiomassSignalDrainFrame = frame;

            ReadOnlySpan<EntityDeathSignal> deathSignals = SignalBus<EntityDeathSignal>.GetFrameSnapshot();
            for (int i = 0; i < deathSignals.Length; i++)
            {
                EntityDeathSignal signal = deathSignals[i];
                if (signal.EntityHash == 0u)
                    continue;

                float amount = math.max(0.01f, entityDeathBiomassPenalty01 * math.max(0.1f, signal.Intensity01));
                QueueOrApplyBiomassImpact(in signal.PositionAup, BiomassImpactKindDeath, amount);
            }

            ReadOnlySpan<ItemAcquiredSignal> itemSignals = SignalBus<ItemAcquiredSignal>.GetFrameSnapshot();
            for (int i = 0; i < itemSignals.Length; i++)
            {
                ItemAcquiredSignal signal = itemSignals[i];
                if (!IsFishItemHash(signal.ItemHash))
                    continue;

                float quantity = math.max(1f, signal.Quantity);
                QueueOrApplyBiomassImpact(
                    in signal.PositionAup,
                    BiomassImpactKindFishing,
                    fishAcquisitionPreyPenalty01 * quantity);
            }
        }

        private void PublishScannerEcologyWarningIfNeeded()
        {
            if (Time.frameCount - _lastScannerWarningFrame < 120)
                return;

            if (!GlobalSignals.TryGetLatestScannerToolActiveSignal(out ScannerToolActiveSignal scannerSignal, out _) ||
                scannerSignal.Active == 0)
            {
                return;
            }

            if (!TryResolvePlayerRuntimePosition(out Vector3 playerPosition) ||
                !TryGetBiomassAvailability(playerPosition, out float preyBiomass01, out float predatorBiomass01, out _))
            {
                return;
            }

            if (preyBiomass01 > 0.05f || predatorBiomass01 > 0.05f)
                return;

            _lastScannerWarningFrame = Time.frameCount;
            GlobalSignals.Publish(new HUDNotificationSignal
            {
                MessageHash = _EcologicalCollapseWarningHash,
                ContextHash = _EcosystemDirectorContextHash,
                SourceId = _BiomassTelemetryHash,
                Frame = unchecked((uint)Time.frameCount),
                Severity = 3,
                Flags = 0
            });
        }

        private void PublishBiomassTelemetryAndEvents()
        {
            float preySum = 0f;
            float predatorSum = 0f;
            int invalidFlag = 0;
            for (int i = 0; i < _activeBiomassCellCount; i++)
            {
                float capacity = math.max(0.0001f, _biomassCarryingCapacity[i]);
                float prey = math.clamp(_preyBiomassFront[i], 0f, capacity);
                float predator = math.clamp(_predatorBiomassFront[i], 0f, capacity);
                if (!math.isfinite(prey) || !math.isfinite(predator))
                {
                    invalidFlag = 1;
                    prey = 0f;
                    predator = 0f;
                    _preyBiomassFront[i] = 0f;
                    _preyBiomassBack[i] = 0f;
                    _predatorBiomassFront[i] = 0f;
                    _predatorBiomassBack[i] = 0f;
                }

                preySum += prey;
                predatorSum += predator;
                byte flags = _biomassCellFlags[i];
                bool predatorCleared = predator <= 0.0001f;
                if (predatorCleared && (flags & BiomassCellFlagSectorClearedPublished) == 0)
                {
                    PublishPredatorClearedEvent(_biomassMacroCellCoords[i]);
                    flags |= BiomassCellFlagSectorClearedPublished;
                }
                else if (!predatorCleared && predator > 0.05f)
                {
                    flags = (byte)(flags & ~BiomassCellFlagSectorClearedPublished);
                }

                _biomassCellFlags[i] = flags;
            }

            float globalSum = preySum + predatorSum;
            float overgrowth01 = ResolveGlobalFloraOvergrowth01(preySum);
            _debugPreyBiomassSum = preySum;
            _debugPredatorBiomassSum = predatorSum;
            _debugGlobalBiomassSum = globalSum;
            _debugFloraOvergrowth01 = overgrowth01;
            _debugBiomassCellCount = _activeBiomassCellCount;
            Shader.SetGlobalFloat(_BiomassOvergrowthId, overgrowth01);
            PushBiomassBlackBox(globalSum, preySum, predatorSum, overgrowth01, invalidFlag);
            GlobalTelemetryBus.PublishPerformanceWarning(_BiomassTelemetryHash, _EcosystemDirectorContextHash, globalSum);
            if (invalidFlag != 0)
                DumpBiomassBlackBox();
        }

        private void PublishPredatorClearedEvent(int2 macroCellCoord)
        {
            AbsoluteUniversePosition centerAup = ResolveBiomassMacroCellCenterAup(macroCellCoord);
            GlobalSignals.Publish(new ProgressionEventSignal
            {
                PositionAup = centerAup,
                PoiHash = _SectorClearedEventHash,
                QuestHash = 0u,
                Frame = unchecked((uint)Time.frameCount),
                Source = 3,
                Flags = 0
            });
        }

        private void PushBiomassBlackBox(
            float globalSum,
            float preySum,
            float predatorSum,
            float overgrowth01,
            int flags)
        {
            if (!_biomassBlackBox.IsCreated || _biomassBlackBox.Length <= 0)
                return;

            int index = _biomassBlackBoxCursor % _biomassBlackBox.Length;
            _biomassBlackBox[index] = new BiomassTelemetryEntry
            {
                FrameIndex = unchecked((uint)Time.frameCount),
                StateHash = MixBiomassStateHash(globalSum, preySum, predatorSum, _activeBiomassCellCount),
                ActiveCellCount = _activeBiomassCellCount,
                Flags = flags,
                GlobalBiomassSum = globalSum,
                PreyBiomassSum = preySum,
                PredatorBiomassSum = predatorSum,
                FloraOvergrowth01 = overgrowth01
            };
            _biomassBlackBoxCursor++;
        }

        private unsafe void DumpBiomassBlackBox()
        {
            if (!_biomassBlackBox.IsCreated)
                return;

            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string dumpPath = Path.Combine(projectRoot, BiomassTelemetryDumpRelativePath);
                string directory = Path.GetDirectoryName(dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    byte* headerPtr = stackalloc byte[sizeof(ulong)];
                    UnsafeUtility.WriteArrayElement(headerPtr, 0, BiomassTelemetryDumpMagic);
                    stream.Write(new ReadOnlySpan<byte>(headerPtr, sizeof(ulong)));
                    byte* dataPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_biomassBlackBox);
                    int dataBytes = _biomassBlackBox.Length * UnsafeUtility.SizeOf<BiomassTelemetryEntry>();
                    stream.Write(new ReadOnlySpan<byte>(dataPtr, dataBytes));
                }
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)_BiomassTelemetryHash));
            }
        }

        private static uint MixBiomassStateHash(float globalSum, float preySum, float predatorSum, int activeCellCount)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)math.asint(globalSum)) * 16777619u;
                hash = (hash ^ (uint)math.asint(preySum)) * 16777619u;
                hash = (hash ^ (uint)math.asint(predatorSum)) * 16777619u;
                hash = (hash ^ (uint)activeCellCount) * 16777619u;
                return hash;
            }
        }

        private static bool IsFishItemHash(uint itemHash)
        {
            return itemHash == _ItemCuredFishNameHash ||
                   itemHash == _ItemRawFishNameHash ||
                   itemHash == _ItemCookedFishNameHash;
        }

        private static float ResolveFloraOvergrowth01(float preyBiomass01)
        {
            return math.saturate((0.35f - math.saturate(preyBiomass01)) * math.rcp(0.35f));
        }

        private float ResolveGlobalFloraOvergrowth01(float preySum)
        {
            if (_activeBiomassCellCount <= 0)
                return 0f;

            float avgPrey01 = preySum * math.rcp(math.max(1f, _activeBiomassCellCount));
            return ResolveFloraOvergrowth01(avgPrey01);
        }

        private static AbsoluteUniversePosition ResolveBiomassMacroCellCenterAup(int2 macroCellCoord)
        {
            double3 absolutePosition = new double3(
                ((double)macroCellCoord.x + 0.5d) * BiomassMacroCellSizeMeters,
                0d,
                ((double)macroCellCoord.y + 0.5d) * BiomassMacroCellSizeMeters);
            return AbsoluteUniversePosition.FromAbsolutePosition(absolutePosition);
        }

        private static int2 QuantizeBiomassMacroCell(Vector3 worldPosition)
        {
            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(worldPosition);
            return QuantizeBiomassMacroCell(in aup);
        }

        private static int2 QuantizeBiomassMacroCell(in AbsoluteUniversePosition position)
        {
            double3 absolutePosition = position.ToAbsoluteDouble3();
            return new int2(
                (int)math.floor(absolutePosition.x * InvBiomassMacroCellSizeMeters),
                (int)math.floor(absolutePosition.z * InvBiomassMacroCellSizeMeters));
        }

        private static bool ResolveBiomassDiffusionEnabled()
        {
            return GlobalRegistry.ScalabilityTierProfileByte > 0;
        }

        private void CaptureBiomassSaveRuns()
        {
            if (!_saveSnapshotBiomassRuns.IsCreated)
                return;

            _saveSnapshotBiomassRuns.Clear();
            int runLength = 0;
            int2 runStart = int2.zero;
            int2 previousCoord = int2.zero;
            sbyte runPrey = 0;
            sbyte runPredator = 0;
            sbyte runCapacity = 0;
            for (int i = 0; i < _activeBiomassCellCount; i++)
            {
                int2 coord = _biomassMacroCellCoords[i];
                sbyte preyQ = QuantizeBiomass01(_preyBiomassFront[i]);
                sbyte predatorQ = QuantizeBiomass01(_predatorBiomassFront[i]);
                sbyte capacityQ = QuantizeBiomass01(_biomassCarryingCapacity[i]);
                bool canExtend =
                    runLength > 0 &&
                    runLength < byte.MaxValue &&
                    coord.y == previousCoord.y &&
                    coord.x == previousCoord.x + 1 &&
                    preyQ == runPrey &&
                    predatorQ == runPredator &&
                    capacityQ == runCapacity;
                if (!canExtend)
                {
                    FlushBiomassSaveRun(runStart, runPrey, runPredator, runCapacity, runLength);
                    runStart = coord;
                    runLength = 1;
                    runPrey = preyQ;
                    runPredator = predatorQ;
                    runCapacity = capacityQ;
                }
                else
                {
                    runLength++;
                }

                previousCoord = coord;
            }

            FlushBiomassSaveRun(runStart, runPrey, runPredator, runCapacity, runLength);
        }

        private void FlushBiomassSaveRun(
            int2 start,
            sbyte preyQ,
            sbyte predatorQ,
            sbyte capacityQ,
            int runLength)
        {
            if (runLength <= 0 ||
                !_saveSnapshotBiomassRuns.IsCreated ||
                _saveSnapshotBiomassRuns.Length >= _saveSnapshotBiomassRuns.Capacity)
            {
                return;
            }

            _saveSnapshotBiomassRuns.AddNoResize(new EcosystemBiomassSaveRun
            {
                StartMacroCell = start,
                PreyBiomassQ = preyQ,
                PredatorBiomassQ = predatorQ,
                CarryingCapacityQ = capacityQ,
                RunLength = (byte)math.clamp(runLength, 1, byte.MaxValue)
            });
        }

        private void RestoreBiomassSaveRun(in EcosystemSectorSaveRecord saveRecord)
        {
            if (!UnpackBiomassRun(saveRecord, out EcosystemBiomassSaveRun run))
                return;

            int count = math.max(1, run.RunLength);
            float capacity = math.max(0.1f, DequantizeBiomassQ(run.CarryingCapacityQ));
            float prey = math.clamp(DequantizeBiomassQ(run.PreyBiomassQ), 0f, capacity);
            float predator = math.clamp(DequantizeBiomassQ(run.PredatorBiomassQ), 0f, capacity);
            for (int offset = 0; offset < count && _activeBiomassCellCount < _preyBiomassFront.Length; offset++)
            {
                int2 coord = run.StartMacroCell + new int2(offset, 0);
                int slotIndex = ResolveOrCreateBiomassCellSlot(coord, seedWithBaseline: false);
                if (slotIndex < 0)
                    break;

                _biomassCarryingCapacity[slotIndex] = capacity;
                _preyBiomassFront[slotIndex] = prey;
                _preyBiomassBack[slotIndex] = prey;
                _predatorBiomassFront[slotIndex] = predator;
                _predatorBiomassBack[slotIndex] = predator;
                _biomassSumScratch[slotIndex] = prey + predator;
            }
        }

        private void ClearBiomassRuntimeState()
        {
            if (_biomassIndexByKey.IsCreated)
                _biomassIndexByKey.Clear();

            int capacity = _preyBiomassFront.IsCreated ? _preyBiomassFront.Length : 0;
            for (int i = 0; i < capacity; i++)
            {
                if (_preyBiomassFront.IsCreated)
                    _preyBiomassFront[i] = 0f;
                if (_preyBiomassBack.IsCreated)
                    _preyBiomassBack[i] = 0f;
                if (_predatorBiomassFront.IsCreated)
                    _predatorBiomassFront[i] = 0f;
                if (_predatorBiomassBack.IsCreated)
                    _predatorBiomassBack[i] = 0f;
                if (_biomassCarryingCapacity.IsCreated)
                    _biomassCarryingCapacity[i] = 0f;
                if (_biomassSumScratch.IsCreated)
                    _biomassSumScratch[i] = 0f;
                if (_biomassMacroCellCoords.IsCreated)
                    _biomassMacroCellCoords[i] = int2.zero;
                if (_biomassCellFlags.IsCreated)
                    _biomassCellFlags[i] = 0;
            }

            if (_pendingBiomassImpacts.IsCreated)
            {
                for (int i = 0; i < _pendingBiomassImpacts.Length; i++)
                    _pendingBiomassImpacts[i] = default;
            }

            _activeBiomassCellCount = 0;
            _pendingBiomassImpactCount = 0;
            _debugBiomassCellCount = 0;
            _debugGlobalBiomassSum = 0f;
            _debugPreyBiomassSum = 0f;
            _debugPredatorBiomassSum = 0f;
            _debugFloraOvergrowth01 = 0f;
            Shader.SetGlobalFloat(_BiomassOvergrowthId, 0f);
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
                _headlessEntities.SpeciesID[slotIndex] = (byte)math.select(1, 2, state.PredatorPopulationRounded > state.PreyPopulationRounded);
            if (_headlessEntities.Hunger.IsCreated)
            {
                float preyPerPredator = math.select(
                    starvationComfortPreyPerPredator,
                    state.PreyPopulationRounded * math.rcp(math.max(1f, state.PredatorPopulationRounded)),
                    state.PredatorPopulationRounded > 0);
                float invStarvationComfort = math.rcp(math.max(1f, starvationComfortPreyPerPredator));
                _headlessEntities.Hunger[slotIndex] = PackUnitByte(1f - preyPerPredator * invStarvationComfort);
            }
            if (_headlessEntities.SectorCoord.IsCreated)
                _headlessEntities.SectorCoord[slotIndex] = state.SectorCoord;
            if (_headlessEntities.SectorID.IsCreated)
                _headlessEntities.SectorID[slotIndex] = ResolveSectorId(state.SectorCoord);
        }

        private static void ResolveBucketTablePopulation(
            int2 sectorCoord,
            int apexBucketCutoff,
            int grazerPopulationPerSector,
            int leviathanPopulationPerSector,
            out int preyPopulation,
            out int predatorPopulation)
        {
            uint apexBucket = (MixSectorBits(sectorCoord.x, sectorCoord.y) >> 4) & 0x0Fu;
            int spawnLeviathanMask = math.select(0, 1, apexBucket < (uint)math.clamp(apexBucketCutoff, 0, ApexSectorBucketCount));
            preyPopulation = math.max(0, grazerPopulationPerSector) * (1 - spawnLeviathanMask);
            predatorPopulation = math.max(0, leviathanPopulationPerSector) * spawnLeviathanMask;
        }

        private float ResolveRuntimeSectorFoodDensity01(int2 sectorCoord, int biomeId, float harvestPressure01, float algaeBloom01)
        {
            return ResolveSectorFoodDensity01(
                sectorCoord,
                biomeId,
                harvestPressure01,
                algaeBloom01,
                _sectorFoodHeatmapR8,
                _sectorFoodHeatmapSize);
        }

        private static float ResolveSectorFoodDensity01(int2 sectorCoord, int biomeId, float harvestPressure01, float algaeBloom01)
        {
            return ResolveSectorFoodDensity01(sectorCoord, biomeId, harvestPressure01, algaeBloom01, default, default);
        }

        private static float ResolveSectorFoodDensity01(
            int2 sectorCoord,
            int biomeId,
            float harvestPressure01,
            float algaeBloom01,
            NativeArray<byte> foodHeatmapR8,
            int2 foodHeatmapSize)
        {
            float baseFood01 = ResolveSectorBaseFoodCapacity01(sectorCoord, biomeId, foodHeatmapR8, foodHeatmapSize);
            float biomeBias = (math.select(0f, 0.12f, biomeId == 1)) - (math.select(0f, 0.04f, biomeId == 2));
            return math.saturate(baseFood01 + biomeBias - (math.saturate(harvestPressure01) * 0.35f) - (math.saturate(algaeBloom01) * 0.45f));
        }

        private static float ResolveSectorBaseFoodCapacity01(
            int2 sectorCoord,
            int biomeId,
            NativeArray<byte> foodHeatmapR8,
            int2 foodHeatmapSize)
        {
            if (foodHeatmapR8.IsCreated &&
                IsPowerOfTwo(foodHeatmapSize.x) &&
                IsPowerOfTwo(foodHeatmapSize.y))
            {
                int heatmapX = sectorCoord.x & (foodHeatmapSize.x - 1);
                int heatmapZ = sectorCoord.y & (foodHeatmapSize.y - 1);
                int heatmapIndex = heatmapZ * foodHeatmapSize.x + heatmapX;
                if ((uint)heatmapIndex < (uint)foodHeatmapR8.Length)
                    return foodHeatmapR8[heatmapIndex] * InvFoodHeatmapByteMax;
            }

            float roll01 = StableSectorRandom01(sectorCoord + new int2(17, -31), biomeId);
            return 0.42f + (roll01 * 0.46f);
        }

        private static float ResolveSectorTemperatureScore01(int2 sectorCoord, int biomeId)
        {
            float roll01 = StableSectorRandom01(sectorCoord + new int2(-19, 43), biomeId ^ 0x35A);
            float biomeBias = (math.select(0f, 0.22f, biomeId == 1)) + (math.select(0f, 0.08f, biomeId == 2));
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

        private static bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
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

        private static long PackBiomassCellKey(int2 macroCellCoord)
        {
            return ((long)macroCellCoord.x << 32) | (uint)macroCellCoord.y;
        }

        private static int ResolveBiomeIdForSector(int2 sectorCoord)
        {
            uint mix = MixSectorBits(sectorCoord.x, sectorCoord.y);
            uint bucket = mix & 0x0Fu;
            int belowEightMask = math.select(0, 1, bucket < 8u);
            int belowThreeMask = math.select(0, 1, bucket < 3u);
            return belowEightMask * (2 - belowThreeMask);
        }

        private static uint PackPopulationCounts(int preyPopulation, int predatorPopulation)
        {
            uint packedPrey = (ushort)math.clamp(preyPopulation, 0, ushort.MaxValue);
            uint packedPredator = (ushort)math.clamp(predatorPopulation, 0, ushort.MaxValue);
            return packedPrey | (packedPredator << 16);
        }

        private static void UnpackPopulationCounts(uint packed, out int preyPopulation, out int predatorPopulation)
        {
            preyPopulation = (ushort)(packed & 0x0000FFFFu);
            predatorPopulation = (ushort)((packed >> 16) & 0x0000FFFFu);
        }

        private static uint PackAdaptationTraits(float fitness, float speedMultiplier, float camouflageIndex, float maximumSpeedMultiplier)
        {
            uint packedFitness = PackUnitByte(fitness);
            float safeMaximumSpeedMultiplier = math.max(1f, maximumSpeedMultiplier);
            float speed01 = math.saturate((speedMultiplier - 1f) * math.rcp(safeMaximumSpeedMultiplier - 1f + 0.0001f));
            uint packedSpeed = PackUnitByte(speed01);
            uint packedCamouflage = PackUnitByte(camouflageIndex);
            return packedFitness | (packedSpeed << 8) | (packedCamouflage << 16);
        }

        private static void UnpackAdaptationTraits(
            uint packed,
            float maximumSpeedMultiplier,
            out float fitness,
            out float speedMultiplier,
            out float camouflageIndex)
        {
            fitness = ((packed >> 0) & 0xFFu) * math.rcp(255f);
            float speed01 = ((packed >> 8) & 0xFFu) * math.rcp(255f);
            camouflageIndex = ((packed >> 16) & 0xFFu) * math.rcp(255f);
            speedMultiplier = 1f + (math.saturate(speed01) * math.max(0f, maximumSpeedMultiplier - 1f));
        }

        private static EcosystemSectorSaveRecord PackBiomassRunAsSectorRecord(in EcosystemBiomassSaveRun run)
        {
            uint runLength = math.clamp(run.RunLength, (byte)1, byte.MaxValue);
            uint preyQ = (uint)(byte)math.clamp(run.PreyBiomassQ, (sbyte)0, (sbyte)100);
            uint predatorQ = (uint)(byte)math.clamp(run.PredatorBiomassQ, (sbyte)0, (sbyte)100);
            uint capacityQ = (uint)(byte)math.clamp(run.CarryingCapacityQ, (sbyte)0, (sbyte)100);
            return new EcosystemSectorSaveRecord
            {
                SectorCoord = run.StartMacroCell,
                PackedPopulations = BiomassSaveRecordMarker |
                                    (runLength & BiomassSaveRunLengthMask) |
                                    (preyQ << (int)BiomassSavePreyShift) |
                                    (predatorQ << (int)BiomassSavePredatorShift) |
                                    (capacityQ << (int)BiomassSaveCapacityShift),
                PackedAdaptation = 0u
            };
        }

        private static bool IsBiomassSaveRecord(in EcosystemSectorSaveRecord saveRecord)
        {
            return (saveRecord.PackedPopulations & BiomassSaveRecordMarker) != 0u;
        }

        private static bool UnpackBiomassRun(in EcosystemSectorSaveRecord saveRecord, out EcosystemBiomassSaveRun run)
        {
            run = default;
            if (!IsBiomassSaveRecord(in saveRecord))
                return false;

            uint packed = saveRecord.PackedPopulations;
            run.StartMacroCell = saveRecord.SectorCoord;
            run.RunLength = (byte)math.max(1u, packed & BiomassSaveRunLengthMask);
            run.PreyBiomassQ = (sbyte)math.clamp((int)((packed >> (int)BiomassSavePreyShift) & 0xFFu), 0, 100);
            run.PredatorBiomassQ = (sbyte)math.clamp((int)((packed >> (int)BiomassSavePredatorShift) & 0xFFu), 0, 100);
            run.CarryingCapacityQ = (sbyte)math.clamp((int)((packed >> (int)BiomassSaveCapacityShift) & 0x7Fu), 0, 100);
            return true;
        }

        private static sbyte QuantizeBiomass01(float value)
        {
            return (sbyte)math.clamp(RoundPositiveToInt(math.saturate(value) * 100f), 0, 100);
        }

        private static float DequantizeBiomassQ(sbyte value)
        {
            return math.clamp(value, (sbyte)0, (sbyte)100) * 0.01f;
        }

        private static int RoundPositiveToInt(float value)
        {
            return (int)(math.max(0f, value) + 0.5f);
        }

        private static byte PackUnitByte(float value)
        {
            return (byte)math.clamp(RoundPositiveToInt(math.saturate(value) * 255f), 0, 255);
        }

        private static byte PackBooleanByte(bool value)
        {
            return (byte)math.select(0, 1, value);
        }

    }
}
