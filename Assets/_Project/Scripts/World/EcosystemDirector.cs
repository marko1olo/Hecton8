using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.AI;
using Hecton8.AI.Ecology.Migration;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Environment.Fluids;
using Hecton8.Gameplay;
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
using BrineLayerSample = Hecton8.Core.Contracts.BrineLayerSample;
using MacroSwarm = Hecton8.Core.Contracts.MacroSwarm;
using MacroSwarmArrival = Hecton8.Core.Contracts.MacroSwarmArrival;

namespace Hecton8.World
{
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct EcosystemSectorSaveRecord
    {
        [FieldOffset(0)] public int2 SectorCoord;
        [FieldOffset(8)] public uint PackedPopulations;
        [FieldOffset(12)] public uint PackedAdaptation;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct EcosystemBiomassSaveRun
    {
        [FieldOffset(0)] public int2 StartMacroCell;
        [FieldOffset(8)] public sbyte PreyBiomassQ;
        [FieldOffset(9)] public sbyte PredatorBiomassQ;
        [FieldOffset(10)] public sbyte CarryingCapacityQ;
        [FieldOffset(11)] public byte RunLength;
        [FieldOffset(12)] public uint Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct EcosystemIndexEntry
    {
        [FieldOffset(0)] public long Key;
        [FieldOffset(8)] public int Slot;
        [FieldOffset(12)] public int Occupied;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct MacroSwarmTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public int ActiveMacroSwarms;
        [FieldOffset(12)] public int ArrivalCount;
        [FieldOffset(16)] public float BiomassSum;
        [FieldOffset(20)] public int Flags;
        [FieldOffset(24)] public uint Reserved0;
        [FieldOffset(28)] public uint Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    internal struct FaunaMutationTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public int TotalMutatedEntities;
        [FieldOffset(12)] public int HeadlessMutatedCount;
        [FieldOffset(16)] public int MacroSwarmMutatedCount;
        [FieldOffset(20)] public uint LastMutationFlags;
        [FieldOffset(24)] public float LastRadiationRads;
        [FieldOffset(28)] public float LastToxicity01;
        [FieldOffset(32)] public float LastBrineDepth01;
        [FieldOffset(36)] public uint Reserved0;
        [FieldOffset(40)] public uint Reserved1;
        [FieldOffset(44)] public uint Reserved2;
    }

    /// <summary>
    /// Cold-path sector ecosystem table. Population is a deterministic cinematic roll per 1 km sector.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4037)]
    public sealed class EcosystemDirector : MonoBehaviour, ISlowTickable, IFrostTickable, ILateFrameTickable, IEcosystemDirectorService, IServiceHeartbeat, IServiceShutdown
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
        private const int MacroSwarmCapacity = 256;
        private const int MacroSwarmArrivalCapacity = 64;
        private const int MacroSwarmSignalScratchCapacity = 64;
        private const int MacroSwarmBlackBoxCapacity = 300;
        private const int MacroSwarmVisualBoidsPerBiomassUnit = 64;
        private const int FaunaMutationBlackBoxCapacity = 300;
        private const int MacroSwarmLowTierCap = 32;
        private const int MacroSwarmMiddleTierCap = 64;
        private const int MacroSwarmHighTierCap = 128;
        private const byte BiomassImpactKindDeath = 1;
        private const byte BiomassImpactKindFishing = 2;
        private const byte BiomassImpactKindPredation = 3;
        private const byte BiomassImpactKindApexKill = 4;
        private const byte BiomassImpactKindCampaignToxicity = 5;
        private const float MacroSwarmDiffusionLowThreshold01 = 0.1f;
        private const float MacroSwarmDiffusionHighThreshold01 = 0.9f;
        private const float MacroSwarmTransferFraction01 = 0.15f;
        private const float MacroSwarmDehydrationTransferFraction01 = 0.25f;
        private const float MacroSwarmPredatorHighThreshold01 = 0.65f;
        private const float MacroSwarmPredatorBiteFraction01 = 0.1f;
        private const float MacroSwarmDefaultSpeedCellsPerSecond = 0.2f;
        private const int MacroSwarmBlackBoxFlagInvalid = 1;
        private const int MacroSwarmBlackBoxFlagDatabaseHydrated = 2;
        private const int MacroSwarmBlackBoxFlagActiveHydrated = 4;
        private const int MacroSwarmBlackBoxFlagCapacityOverflow = 8;
        private const int MacroSwarmBlackBoxFlagActiveDehydrated = 16;
        private const int MacroSwarmBlackBoxFlagHeartbeat = 32;
        private const byte ItemAcquiredSourceUnknown = 0;
        private const byte ItemAcquiredSourceResourceNode = 1;
        private const byte BiomassCellFlagSectorClearedPublished = 1 << 0;
        private const byte BiomassCellFlagPredatorSeen = 1 << 1;
        private const uint BiomassSaveRecordMarker = 0x80000000u;
        private const uint BiomassSaveAdaptationMarker = 0x42494F4Du;
        private const uint MacroSwarmSaveHeaderMarker = 0x4D535748u;
        private const uint MacroSwarmSaveDetailMarker = 0x4D535744u;
        private const uint FaunaGenomeSaveHeaderMarker = 0x46474E48u;
        private const uint FaunaGenomeSaveDetailMarker = 0x46474E44u;
        private const uint BiomassSaveRunLengthMask = 0x000000FFu;
        private const int BiomassSavePreyShift = 8;
        private const int BiomassSavePredatorShift = 16;
        private const int BiomassSaveCapacityShift = 24;
        private const float MaxLotkaVolterraRatePerSecond = 0.2f;
        private const float DefaultHostilityPeakHoldSeconds = 18f;
        private const float CampaignToxicityDrainIntervalSeconds = 2f;
        private const float CampaignToxicitySafeDepthMeters = 120f;
        private const float CampaignToxicityPreyDrainPerPulse01 = 0.025f;
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
        private const float WhaleFallAcousticImpulseVolume01 = 0.42f;
        private const float BiomeGradientAmbientSpawnGain = 0.18f;
        private const float BiomeGradientPredatorSpawnGain = 0.08f;
        private const float BiomeGradientCapacityGain = 0.04f;
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
        private const ulong MacroSwarmTelemetryDumpMagic = 0x004D57534F434548UL;
        private const ulong FaunaMutationTelemetryDumpMagic = 0x004D55474F434548UL;
        private const string BiomassTelemetryDumpRelativePath = "Docs/AgentLogs/Dump_ECOLOGICAL_BIOMASS_ENGINE.bin";
        private const string MacroSwarmTelemetryDumpRelativePath = "Docs/AgentLogs/Dump_ECOSYSTEM_MIGRATION_LINK.bin";
        private const string FaunaMutationTelemetryDumpRelativePath = "Docs/AgentLogs/Dump_ECOLOGY_MUTATION_DIRECTOR.bin";
        private static readonly string[] ThermalSpawnTokens = { "lava", "thermal", "brine", "heat", "volcanic", "smoker" };
        private static readonly string[] SharkSpawnTokens = { "shark", "hunter", "stalker" };
        private static readonly string[] ScavengerSpawnTokens = { "scavenger", "crab", "eel", "carrion", "cleaner" };
        private static readonly uint _FloraPredatorAupSaturationWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("EcosystemDirector.FloraPredatorAupSaturation"));
        private static readonly uint _BiomassTelemetryHash = unchecked((uint)Hecton.Localization.LocHash.Compute("EcosystemDirector.GlobalBiomassSum"));
        private static readonly uint _FaunaMutationTelemetryHash = unchecked((uint)Hecton.Localization.LocHash.Compute("EcosystemDirector.TotalMutatedEntities"));
        private static readonly uint _EcologicalCollapseWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Warning: Ecological Collapse"));
        private static readonly uint _SectorClearedEventHash = unchecked((uint)Hecton.Localization.LocHash.Compute("EcosystemDirector.SectorCleared"));
        private static readonly uint _ItemCuredFishNameHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ITEM_CURED_FISH_NAME"));
        private static readonly uint _ItemRawFishNameHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ITEM_RAW_FISH_NAME"));
        private static readonly uint _ItemCookedFishNameHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ITEM_COOKED_FISH_NAME"));
        private static readonly uint _ItemCuredFishStableHash = unchecked((uint)Hecton.Localization.LocHash.Compute("cured_fish"));
        private static readonly uint _ItemRawFishStableHash = unchecked((uint)Hecton.Localization.LocHash.Compute("raw_fish"));
        private static readonly uint _ItemCookedFishStableHash = unchecked((uint)Hecton.Localization.LocHash.Compute("cooked_fish"));
        private static readonly uint _ItemFishStableHash = unchecked((uint)Hecton.Localization.LocHash.Compute("fish"));
        private static readonly uint _ItemCuredFishDisplayHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Cured Fish"));
        private static readonly uint _ItemRawFishDisplayHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Raw Fish"));
        private static readonly uint _ItemCookedFishDisplayHash = unchecked((uint)Hecton.Localization.LocHash.Compute("Cooked Fish"));
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

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct SectorPopulationState
        {
            [FieldOffset(0)] public int2 SectorCoord;
            [FieldOffset(8)] public float PreyPopulation;
            [FieldOffset(12)] public float PredatorPopulation;
            [FieldOffset(16)] public float HarvestPressure;
            [FieldOffset(20)] public float Fitness;
            [FieldOffset(24)] public float SpeedMultiplier;
            [FieldOffset(28)] public float CamouflageIndex;
            [FieldOffset(32)] public float FoodDensity01;
            [FieldOffset(36)] public float TemperatureScore01;
            [FieldOffset(40)] public float Oxygen01;
            [FieldOffset(44)] public float AlgaeBloom01;
            [FieldOffset(48)] public int PreyPopulationRounded;
            [FieldOffset(52)] public int PredatorPopulationRounded;
            [FieldOffset(56)] public int BiomeId;
            [FieldOffset(60)] public byte ApexInSector;
        }

        private struct VaultNativeArray<T> where T : struct
        {
            private IDataVault _vault;
            private VaultBufferHandle<T> _handle;

            public static VaultNativeArray<T> Create(IDataVault vault, VaultBufferHandle<T> handle)
            {
                return new VaultNativeArray<T>
                {
                    _vault = vault,
                    _handle = handle
                };
            }

            public bool IsCreated => _handle.IsCreated;

            public int Length => Resolve().Length;

            public T this[int index]
            {
                get
                {
                    NativeArray<T> array = Resolve();
                    return array[index];
                }
                set
                {
                    NativeArray<T> array = Resolve();
                    array[index] = value;
                }
            }

            public NativeArray<T> GetSubArray(int start, int length)
            {
                NativeArray<T> array = Resolve();
                return array.IsCreated ? array.GetSubArray(start, length) : default;
            }

            public NativeArray<T> Resolve()
            {
                return _handle.Resolve(_vault);
            }

            public static implicit operator NativeArray<T>(VaultNativeArray<T> view)
            {
                return view.Resolve();
            }
        }

        private struct HeadlessEntitySoA
        {
            public VaultNativeArray<float3> Positions;
            public VaultNativeArray<byte> SpeciesID;
            public VaultNativeArray<byte> Hunger;
            public VaultNativeArray<int2> SectorCoord;
            public VaultNativeArray<int> SectorID;
            public VaultNativeArray<ulong> FaunaGenomes;
            public VaultNativeArray<float> MutationRadiation;
            public VaultNativeArray<float> MutationToxicity;
            public VaultNativeArray<float> MutationBrine;
            public VaultNativeArray<uint> MutationStableHashes;
            public VaultNativeArray<byte> MutationResults;

            public bool IsCreated =>
                Positions.IsCreated &&
                SpeciesID.IsCreated &&
                Hunger.IsCreated &&
                SectorCoord.IsCreated &&
                SectorID.IsCreated &&
                FaunaGenomes.IsCreated &&
                MutationRadiation.IsCreated &&
                MutationToxicity.IsCreated &&
                MutationBrine.IsCreated &&
                MutationStableHashes.IsCreated &&
                MutationResults.IsCreated;
        }

        private static int ResolveVaultIndexCapacity(int requestedCapacity)
        {
            int target = math.max(4, requestedCapacity * 2);
            int capacity = 1;
            while (capacity < target && capacity < (1 << 30))
                capacity <<= 1;

            return capacity;
        }

        private static void ClearIndexEntries(NativeArray<EcosystemIndexEntry> entries)
        {
            if (!entries.IsCreated)
                return;

            for (int i = 0; i < entries.Length; i++)
                entries[i] = default;
        }

        private static bool TryFindIndexEntry(
            NativeArray<EcosystemIndexEntry> entries,
            long key,
            out int slot)
        {
            slot = -1;
            if (!entries.IsCreated || entries.Length <= 0)
                return false;

            int capacity = entries.Length;
            int start = ResolveIndexProbeStart(key, capacity);
            for (int probe = 0; probe < capacity; probe++)
            {
                int index = start + probe;
                if (index >= capacity)
                    index -= capacity;

                EcosystemIndexEntry entry = entries[index];
                if (entry.Occupied == 0)
                    return false;

                if (entry.Key == key)
                {
                    slot = entry.Slot;
                    return true;
                }
            }

            return false;
        }

        private static bool TryUpsertIndexEntry(
            NativeArray<EcosystemIndexEntry> entries,
            long key,
            int slot)
        {
            if (!entries.IsCreated || entries.Length <= 0 || slot < 0)
                return false;

            int capacity = entries.Length;
            int start = ResolveIndexProbeStart(key, capacity);
            for (int probe = 0; probe < capacity; probe++)
            {
                int index = start + probe;
                if (index >= capacity)
                    index -= capacity;

                EcosystemIndexEntry entry = entries[index];
                if (entry.Occupied == 0 || entry.Key == key)
                {
                    entries[index] = new EcosystemIndexEntry
                    {
                        Key = key,
                        Slot = slot,
                        Occupied = 1
                    };
                    return true;
                }
            }

            return false;
        }

        private static int ResolveIndexProbeStart(long key, int capacity)
        {
            ulong hash = (ulong)key;
            hash ^= hash >> 33;
            hash *= 0xff51afd7ed558ccdUL;
            hash ^= hash >> 33;
            hash *= 0xc4ceb9fe1a85ec53UL;
            hash ^= hash >> 33;
            return (int)(hash % (uint)math.max(1, capacity));
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct BiomassImpactEvent
        {
            [FieldOffset(0)] public int2 MacroCellCoord;
            [FieldOffset(8)] public float Amount;
            [FieldOffset(12)] public byte Kind;
            [FieldOffset(13)] public byte Padding0;
            [FieldOffset(14)] public ushort Padding1;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct BiomassTelemetryEntry
        {
            [FieldOffset(0)] public uint FrameIndex;
            [FieldOffset(4)] public uint StateHash;
            [FieldOffset(8)] public int ActiveCellCount;
            [FieldOffset(12)] public int Flags;
            [FieldOffset(16)] public float GlobalBiomassSum;
            [FieldOffset(20)] public float PreyBiomassSum;
            [FieldOffset(24)] public float PredatorBiomassSum;
            [FieldOffset(28)] public float FloraOvergrowth01;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct ApexTerritorySample
        {
            [FieldOffset(0)] public AbsoluteUniversePositionBlit128 PositionAup;
            [FieldOffset(48)] public float Radius;
            [FieldOffset(52)] public float MassScore;
            [FieldOffset(56)] public int BrainIndex;
            [FieldOffset(60)] public int Padding;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct ApexTerritoryOverlapResult
        {
            [FieldOffset(0)] public int RetreatBrainIndex;
            [FieldOffset(4)] public int RivalBrainIndex;
            [FieldOffset(8)] public float Overlap01;
            [FieldOffset(12)] public float Padding;
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
            [ReadOnly] public NativeArray<EcosystemIndexEntry> CellIndexEntries;
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
                if (!TryFindIndexEntry(CellIndexEntries, PackBiomassCellKey(coord), out int neighborIndex) ||
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
        [SerializeField, Min(0f)] private float preyBirthRatePerSecond = HectonEcologyContract.WorldPreyBirthRatePerSecond;
        [SerializeField, Min(0f)] private float predationRatePerSecond = HectonEcologyContract.WorldPredationRatePerSecond;
        [SerializeField, Min(0f)] private float predatorGrowthRatePerSecond = HectonEcologyContract.WorldPredatorGrowthRatePerSecond;
        [SerializeField, Min(0f)] private float predatorDeathRatePerSecond = HectonEcologyContract.WorldPredatorDeathRatePerSecond;
        [SerializeField, Range(0f, 1f)] private float reproductionFoodThreshold01 = HectonEcologyContract.WorldReproductionFoodThreshold01;
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

        private VaultNativeArray<SectorPopulationState> _sectorFrontStates;
        private VaultNativeArray<SectorPopulationState> _sectorBackStates;
        private VaultNativeArray<int> _preyFrontCounts;
        private VaultNativeArray<int> _preyBackCounts;
        private VaultNativeArray<int> _predatorFrontCounts;
        private VaultNativeArray<int> _predatorBackCounts;
        private VaultNativeArray<float> _preyBiomassFront;
        private VaultNativeArray<float> _preyBiomassBack;
        private VaultNativeArray<float> _predatorBiomassFront;
        private VaultNativeArray<float> _predatorBiomassBack;
        private VaultNativeArray<float> _biomassCarryingCapacity;
        private VaultNativeArray<float> _biomassSumScratch;
        private VaultNativeArray<int2> _biomassMacroCellCoords;
        private VaultNativeArray<byte> _biomassCellFlags;
        private VaultNativeArray<BiomassImpactEvent> _pendingBiomassImpacts;
        private VaultNativeArray<BiomassTelemetryEntry> _biomassBlackBox;
        private VaultNativeArray<MacroSwarm> _macroSwarms;
        private VaultNativeArray<MacroSwarmArrival> _macroSwarmArrivals;
        private VaultNativeArray<int> _macroSwarmCounters;
        private VaultNativeArray<MacroSwarmTelemetryEntry> _macroSwarmBlackBox;
        private VaultNativeArray<float> _macroSwarmMutationRadiation;
        private VaultNativeArray<float> _macroSwarmMutationToxicity;
        private VaultNativeArray<float> _macroSwarmMutationBrine;
        private VaultNativeArray<byte> _macroSwarmMutationResults;
        private VaultNativeArray<FaunaMutationTelemetryEntry> _faunaMutationBlackBox;
        private VaultNativeArray<MacroSwarm> _macroHydrationScratch;
        private VaultNativeArray<MacroSwarm> _macroDehydrationScratch;
        private VaultNativeArray<EcosystemBiomassSaveRun> _saveSnapshotBiomassRuns;
        private VaultNativeArray<EcosystemIndexEntry> _biomassIndexEntries;
        private HeadlessEntitySoA _headlessEntities;
        private VaultNativeArray<byte> _sectorFoodHeatmapR8;
        private VaultNativeArray<EcosystemIndexEntry> _sectorIndexEntries;
        private VaultNativeArray<ApexTerritorySample> _apexTerritorySamples;
        private VaultNativeArray<ApexTerritoryOverlapResult> _apexTerritoryOverlapResults;
        private VaultNativeArray<CapsulecastCommand> _apexSpawnGateCommands;
        private VaultNativeArray<RaycastHit> _apexSpawnGateHits;
        private VaultNativeArray<float4> _floraPredatorAupUpload;
        private VaultNativeArray<EcosystemSectorSaveRecord> _saveSnapshotSectors;
        private int _saveSnapshotSectorCount;
        private int _saveSnapshotBiomassRunCount;
        private FaunaBrain[] _apexTerritoryBrains;
        private GraphicsBuffer _floraPredatorAupBuffer;
        private JobHandle _scheduledSolveHandle;
        private JobHandle _scheduledGenomeMutationHandle;
        private JobHandle _macroSwarmTravelHandle;
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
        private int _macroSwarmBlackBoxCursor;
        private int _faunaMutationBlackBoxCursor;
        private int _macroHydrationScratchCount;
        private int _macroDehydrationScratchCount;
        private int _lastFaunaMutationInvalidScalarFrame = -1;
        private int _totalMutatedEntities;
        private int _lastHeadlessMutationCount;
        private int _lastMacroSwarmMutationCount;
        private int _scheduledHeadlessMutationCount;
        private int _scheduledMacroSwarmMutationCount;
        private uint _faunaGenomeMutationEpoch;
        private uint _lastMutationFlags;
        private float _lastMutationRadiationRads;
        private float _lastMutationToxicity01;
        private float _lastMutationBrineDepth01;
        private int _activeMacroSwarmCount;
        private int _macroSwarmActiveCap = MacroSwarmLowTierCap;
        private byte _macroSwarmQualityTierProfileByte;
        private float _macroSwarmSpeedCellsPerSecond = MacroSwarmDefaultSpeedCellsPerSecond * 0.5f;
        private int _lastMacroSwarmArrivalCount;
        private int _lastMacroSwarmsHydrated;
        private int _lastMacroHydratedBoidEstimate;
        private float _lastMacroSwarmBiomassSum;
        private uint _lastMacroSwarmStateHash;
        private int _lastSectorResidencySignalDrainFrame;
        private int _scheduledApexTerritoryOverlapCount;
        private IDataVault _dataVault;
        private VaultBufferHandle<MacroEcosystemSectorVaultRecord> _macroSectorSnapshotHandle;
        private VaultBufferHandle<MacroEcosystemSectorIndexRecord> _macroSectorIndexHandle;
        private VaultBufferHandle<MacroEcosystemTuningVaultRecord> _macroTuningHandle;
        private bool _registeredService;
        private bool _registeredSlowTickable;
        private bool _registeredFrostTickable;
        private bool _registeredLateFrameTickable;
        private bool _solveScheduled;
        private bool _genomeMutationScheduled;
        private bool _macroSwarmTravelScheduled;
        private bool _apexTerritoryOverlapScheduled;
        private bool _populationSolvePendingHibernationSync;
        private float _biomeHostility01;
        private float _biomeGradientBlend01;
        private byte _biomeGradientA;
        private byte _biomeGradientB;
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
        private float _campaignToxicity01;
        private uint _campaignToxicityStageHash;
        private float _campaignToxicityAccumulator;
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
            _biomassIndexEntries.IsCreated &&
            _macroSwarms.IsCreated &&
            _macroSwarmArrivals.IsCreated &&
            _macroSwarmCounters.IsCreated &&
            _macroHydrationScratch.IsCreated &&
            _macroDehydrationScratch.IsCreated &&
            _headlessEntities.IsCreated &&
            _apexSpawnGateCommands.IsCreated &&
            _apexSpawnGateHits.IsCreated &&
            _sectorIndexEntries.IsCreated;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => IsServiceReady ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _registeredService && IsInitialized && ReferenceEquals(GlobalRegistry.EcosystemDirector, this);

        /// <summary>
        /// Normalized biome hostility score exposed to UI and pacing systems.
        /// </summary>
        public float BiomeHostility01 => ResolveCombinedHostility01();

        /// <inheritdoc />
        public int ActiveMacroSwarmCount => _activeMacroSwarmCount;

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
            if (requiredLength <= 0L ||
                requiredLength > int.MaxValue ||
                heatmapR8.Length < requiredLength)
            {
                _sectorFoodHeatmapR8 = default;
                _sectorFoodHeatmapSize = default;
                return;
            }

            IDataVault vault = ResolveDataVault();
            if (vault == null)
            {
                _sectorFoodHeatmapR8 = default;
                _sectorFoodHeatmapSize = default;
                return;
            }

            NativeArray<byte> vaultHeatmap = vault.GetBuffer<byte>(
                BufferID.EcosystemSectorFoodHeatmapR8,
                (int)requiredLength,
                SystemID.AIEcology,
                NativeArrayOptions.UninitializedMemory);
            if (!vaultHeatmap.IsCreated || vaultHeatmap.Length < requiredLength)
            {
                _sectorFoodHeatmapR8 = default;
                _sectorFoodHeatmapSize = default;
                return;
            }

            for (int i = 0; i < requiredLength; i++)
                vaultHeatmap[i] = heatmapR8[i];

            vault.TryGetBufferHandle(BufferID.EcosystemSectorFoodHeatmapR8, out VaultBufferHandle<byte> heatmapHandle);
            _sectorFoodHeatmapR8 = VaultNativeArray<byte>.Create(vault, heatmapHandle);
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
                selectionMultiplier *= ResolveBiomeGradientSpawnWeight(archetype);
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
            selectionMultiplier *= ResolveBiomeGradientSpawnWeight(archetype);
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
            float biomeGradientScale = 1f + (_biomeGradientBlend01 * BiomeGradientAmbientSpawnGain);
            _spawnCreditBudget = math.min(
                spawnCreditBudgetMax,
                _spawnCreditBudget + (spawnCreditRecoverPerSecond * math.max(0f, deltaSeconds) * recoveryScale * biomeGradientScale));
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

        private float ResolveBiomeGradientSpawnWeight(CreatureArchetypeData archetype)
        {
            float blend01 = math.saturate(_biomeGradientBlend01);
            if (blend01 <= 0.001f)
                return 1f;

            return IsPredatorOrApex(archetype)
                ? 1f + (blend01 * BiomeGradientPredatorSpawnGain)
                : 1f + (blend01 * BiomeGradientAmbientSpawnGain);
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
            RegisterCorpseResourceNode(worldPosition, speciesId, capacityUnits, 0u);
        }

        internal void RegisterCorpseResourceNode(Vector3 worldPosition, int speciesId, float capacityUnits, uint contaminatedItemHash)
        {
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            if (organicManager != null)
                organicManager.RegisterCorpseResourceNode(worldPosition, speciesId, capacityUnits, contaminatedItemHash);
        }

        internal void RegisterCorpseResourceNode(in AbsoluteUniversePosition positionAup, int speciesId, float capacityUnits)
        {
            RegisterCorpseResourceNode(in positionAup, speciesId, capacityUnits, 0u);
        }

        internal void RegisterCorpseResourceNode(in AbsoluteUniversePosition positionAup, int speciesId, float capacityUnits, uint contaminatedItemHash)
        {
            DestructibleOrganicManager organicManager = DestructibleOrganicManager.ActiveRuntimeInstance;
            if (organicManager != null)
                organicManager.RegisterCorpseResourceNode(in positionAup, speciesId, capacityUnits, contaminatedItemHash);
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

            _saveSnapshotSectorCount = 0;
            _saveSnapshotBiomassRunCount = 0;
            if (!IsInitialized)
                return;

            CompleteScheduledSimulation(forceComplete: true);
            CompleteScheduledMacroSwarmTravel(forceComplete: true);
            ApplyPendingBiomassImpacts();
            SyncPendingHibernatedFaunaPopulationRecords();
            for (int sectorIndex = 0; sectorIndex < _activeSectorCount; sectorIndex++)
            {
                SectorPopulationState state = _sectorFrontStates[sectorIndex];
                TryAppendSaveSnapshotSector(new EcosystemSectorSaveRecord
                {
                    SectorCoord = state.SectorCoord,
                    PackedPopulations = PackPopulationCounts(state.PreyPopulationRounded, state.PredatorPopulationRounded),
                    PackedAdaptation = PackAdaptationTraits(state.Fitness, state.SpeedMultiplier, state.CamouflageIndex, maximumSpeedMultiplier)
                });
            }

            CaptureBiomassSaveRuns();
            if (_saveSnapshotBiomassRuns.IsCreated)
            {
                int runCount = math.min(_saveSnapshotBiomassRunCount, _saveSnapshotBiomassRuns.Length);
                for (int runIndex = 0; runIndex < runCount && HasSaveSnapshotSectorCapacity(1); runIndex++)
                    TryAppendSaveSnapshotSector(PackBiomassRunAsSectorRecord(_saveSnapshotBiomassRuns[runIndex]));
            }

            CaptureMacroSwarmSaveRecords();
            CaptureFaunaGenomeSaveRecords();
        }

        internal NativeArray<EcosystemSectorSaveRecord> GetSaveSnapshotArray()
        {
            if (!_saveSnapshotSectors.IsCreated || _saveSnapshotSectorCount <= 0)
                return default;

            return _saveSnapshotSectors.GetSubArray(0, math.min(_saveSnapshotSectorCount, _saveSnapshotSectors.Length));
        }

        internal NativeArray<EcosystemBiomassSaveRun> GetBiomassSaveSnapshotArray()
        {
            if (!_saveSnapshotBiomassRuns.IsCreated || _saveSnapshotBiomassRunCount <= 0)
                return default;

            return _saveSnapshotBiomassRuns.GetSubArray(0, math.min(_saveSnapshotBiomassRunCount, _saveSnapshotBiomassRuns.Length));
        }

        internal unsafe void RestoreFromLoadedRecords(EcosystemSectorSaveRecord[] loadedRecords)
        {
            if (!IsInitialized)
                return;

            CompleteScheduledSimulation(forceComplete: true);
            ClearIndexEntries(_sectorIndexEntries);
            ClearIndexEntries(_biomassIndexEntries);
            _coldTickAccumulator = 0f;
            _solveScheduled = false;
            _scheduledSolveHandle = default;

            if (_sectorFrontStates.IsCreated)
            {
                void* frontPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks<SectorPopulationState>(_sectorFrontStates.Resolve());
                UnsafeUtility.MemClear(frontPtr, _sectorFrontStates.Length * UnsafeUtility.SizeOf<SectorPopulationState>());
            }

            if (_sectorBackStates.IsCreated)
            {
                void* backPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks<SectorPopulationState>(_sectorBackStates.Resolve());
                UnsafeUtility.MemClear(backPtr, _sectorBackStates.Length * UnsafeUtility.SizeOf<SectorPopulationState>());
            }

            ClearHeadlessRuntimeState();
            ClearBiomassRuntimeState();
            ClearMacroSwarmRuntimeState();

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

                if (IsMacroSwarmSaveHeader(in saveRecord))
                {
                    if (recordIndex + 1 < recordCount)
                        RestoreMacroSwarmSaveRecords(in saveRecord, in loadedRecords[++recordIndex]);
                    continue;
                }

                if (IsMacroSwarmSaveDetail(in saveRecord))
                    continue;

                if (IsFaunaGenomeSaveHeader(in saveRecord))
                {
                    if (recordIndex + 1 < recordCount)
                        RestoreFaunaGenomeSaveRecords(in saveRecord, in loadedRecords[++recordIndex]);
                    continue;
                }

                if (IsFaunaGenomeSaveDetail(in saveRecord))
                    continue;

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
                TryUpsertIndexEntry(_sectorIndexEntries, PackSectorKey(saveRecord.SectorCoord), sectorIndex);
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
            RefreshMacroSwarmScalabilityCache();
            TryRegisterService();
            TryRegisterSlowTickable();
            TryRegisterFrostTickable();
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
            TryUnregisterFrostTickable();
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
            RefreshMacroSwarmScalabilityCache();
            TryRegisterService();
            TryRegisterSlowTickable();
            TryRegisterFrostTickable();
            TryRegisterLateFrameTickable();
        }

        /// <summary>
        /// Advances the sector population solve at 0.1 Hz using a Burst job.
        /// </summary>
        public void SlowTick()
        {
            if (!IsInitialized)
                return;

            RefreshMacroSwarmScalabilityCache();
            DrainBiomeGradientSignal();
            DecayBiomeHostility();
            UpdateSpawnCreditBudget(DefaultSlowTickIntervalSeconds);
            SyncPendingHibernatedFaunaPopulationRecords();
            EnsurePlayerSectorRegistered();
            TickEclipsePredatorShallowMigration(DefaultSlowTickIntervalSeconds);
            TickCampaignToxicityPressure(DefaultSlowTickIntervalSeconds);
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

        /// <summary>
        /// Advances abstract ecology migration on the 5 s post-simulation maintenance lane.
        /// </summary>
        public void FrostTick()
        {
            if (!IsInitialized || _macroSwarmTravelScheduled || HasPendingSimulationJob())
                return;

            RefreshMacroSwarmScalabilityCache();
            DrainSectorResidencySignalSnapshots();
            ApplyMacroSwarmPredatorAttraction();
            SpawnMacroSwarmDiffusionGradient();
            PushMacroSwarmBlackBox(0);
            JobHandle mutationDependency = ScheduleFaunaGenomeMutation();
            ScheduleMacroSwarmTravel(mutationDependency);
        }

        private void EmitWhaleFallAcousticImpulseSlowTick()
        {
            if (_activeWhaleFallAcousticUid == 0u || Time.time > _activeWhaleFallAcousticUntilTime)
                return;

            AcousticPingSignal signal = default;
            signal.PositionAup = AbsoluteUniversePosition.FromRuntimePosition(_activeWhaleFallAcousticPosition);
            signal.RadiusMeters = math.max(CorpseSpawnInfluenceRadiusMeters, scavengerCorpseSearchRadiusMeters);
            signal.Intensity01 = WhaleFallAcousticImpulseVolume01;
            signal.SourceId = _activeWhaleFallAcousticUid;
            signal.Channel = AcousticPingSignal.ChannelLeviathanRoar;
            signal.Flags = AcousticPingSignal.FlagLeviathanRoar;
            GlobalSignals.Publish(in signal);
        }

        private void DrainBiomeGradientSignal()
        {
            ReadOnlySpan<BiomeGradientSignal> signals = SignalBus<BiomeGradientSignal>.GetFrameSnapshot();
            if (signals.Length == 0)
                return;

            BiomeGradientSignal signal = signals[signals.Length - 1];
            _biomeGradientBlend01 = math.saturate(signal.BlendFactor01);
            _biomeGradientA = signal.BiomeA;
            _biomeGradientB = signal.BiomeB;
        }

        public void LateFrameTick()
        {
            CompleteScheduledSimulation(forceComplete: false);
            CompleteScheduledMacroSwarmTravel(forceComplete: false);
            if (IsInitialized)
                PushMacroSwarmHeartbeatBlackBox();
            DrainSectorResidencySignalSnapshots();
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
            if (TryGetMacroVaultBiomassAvailability(worldPosition, out preyBiomass01, out predatorBiomass01, out carryingCapacity01))
                return true;

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

        private bool TryGetMacroVaultBiomassAvailability(
            Vector3 worldPosition,
            out float preyBiomass01,
            out float predatorBiomass01,
            out float carryingCapacity01)
        {
            preyBiomass01 = 0f;
            predatorBiomass01 = 0f;
            carryingCapacity01 = 0f;

            float3 finiteProbe = new float3(worldPosition.x, worldPosition.y, worldPosition.z);
            if (!math.all(math.isfinite(finiteProbe)))
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!TryResolveMacroEcosystemVaultSnapshot(
                    vault,
                    out NativeArray<MacroEcosystemSectorVaultRecord> sectors,
                    out NativeArray<MacroEcosystemSectorIndexRecord> entries,
                    out NativeArray<MacroEcosystemTuningVaultRecord> tuning))
                return false;

            long sectorX = (long)math.floor((double)worldPosition.x / MacroEcosystemVaultContract.SectorSizeMeters);
            long sectorZ = (long)math.floor((double)worldPosition.z / MacroEcosystemVaultContract.SectorSizeMeters);
            ulong hash = MacroEcosystemVaultContract.ComputeSectorHash(sectorX, 0L, sectorZ);
            if (!MacroEcosystemVaultContract.TryResolveSectorIndex(entries, hash, out int index) ||
                (uint)index >= (uint)sectors.Length)
            {
                return false;
            }

            MacroEcosystemTuningVaultRecord tune = tuning[0];
            if ((tune.Flags & MacroEcosystemVaultContract.TuningFlagSnapshotWriteInFlight) != 0u)
                return false;

            MacroEcosystemSectorVaultRecord sector = sectors[index];
            float preyCapacity = math.max(1f, math.select(
                MacroEcosystemVaultContract.DefaultCarryingCapacityPrey,
                tune.CarryingCapacityPrey,
                math.isfinite(tune.CarryingCapacityPrey) & tune.CarryingCapacityPrey > 0f));
            float predatorCapacity = math.max(1f, math.select(
                MacroEcosystemVaultContract.DefaultCarryingCapacityPredator,
                tune.CarryingCapacityPredator,
                math.isfinite(tune.CarryingCapacityPredator) & tune.CarryingCapacityPredator > 0f));

            preyBiomass01 = math.saturate(sector.PreyBiomass * math.rcp(preyCapacity));
            predatorBiomass01 = math.saturate(sector.PredatorBiomass * math.rcp(predatorCapacity));
            float defaultCapacity = MacroEcosystemVaultContract.DefaultCarryingCapacityPrey + MacroEcosystemVaultContract.DefaultCarryingCapacityPredator;
            carryingCapacity01 = math.saturate((preyCapacity + predatorCapacity) * math.rcp(math.max(1f, defaultCapacity)));
            return true;
        }

        private bool TryResolveMacroEcosystemVaultSnapshot(
            IDataVault vault,
            out NativeArray<MacroEcosystemSectorVaultRecord> sectors,
            out NativeArray<MacroEcosystemSectorIndexRecord> entries,
            out NativeArray<MacroEcosystemTuningVaultRecord> tuning)
        {
            sectors = default;
            entries = default;
            tuning = default;

            if (vault == null)
                return false;

            if (!_macroSectorSnapshotHandle.IsCreated &&
                !vault.TryGetBufferHandle(BufferID.ShinobuMacroEcosystemSectorFront, out _macroSectorSnapshotHandle))
            {
                return false;
            }

            if (!_macroSectorIndexHandle.IsCreated &&
                !vault.TryGetBufferHandle(BufferID.ShinobuMacroEcosystemIndexEntries, out _macroSectorIndexHandle))
            {
                return false;
            }

            if (!_macroTuningHandle.IsCreated &&
                !vault.TryGetBufferHandle(BufferID.ShinobuMacroEcosystemTuning, out _macroTuningHandle))
            {
                return false;
            }

            sectors = _macroSectorSnapshotHandle.Resolve(vault);
            entries = _macroSectorIndexHandle.Resolve(vault);
            tuning = _macroTuningHandle.Resolve(vault);
            return sectors.IsCreated && entries.IsCreated && tuning.IsCreated && tuning.Length > 0;
        }

        /// <inheritdoc />
        public bool TryMutateFaunaGenome(ref FaunaGenomeMutationRequest request)
        {
            if (!IsInitialized)
                return false;

            if (!IsFiniteRuntimePosition(request.RuntimePosition))
            {
                request.ResultFlags = 0;
                RecordInvalidMutationScalarSample(request.RadiationRads, request.Toxicity01, request.BrineDepth01);
                return false;
            }

            if ((request.Flags & FaunaGenomeMutationRequestFlags.MacroSwarm) != 0 &&
                _macroSwarmQualityTierProfileByte == 0)
            {
                request.ResultFlags = FaunaGenomeMutationRequestFlags.LowTierMacroSkipped;
                return false;
            }

            SampleMutationScalars(request.RuntimePosition, out float radiationRads, out float toxicity01, out float brineDepth01);
            request.RadiationRads = math.max(SanitizeMutationScalar01(request.RadiationRads), radiationRads);
            request.Toxicity01 = math.max(SanitizeMutationScalar01(request.Toxicity01), toxicity01);
            request.BrineDepth01 = math.max(SanitizeMutationScalar01(request.BrineDepth01), brineDepth01);
            uint stableHash = request.StableEntityHash != 0u
                ? request.StableEntityHash
                : ResolveGenomeMutationStableHash(in request);
            uint rollIndex = request.RollIndex != 0u
                ? request.RollIndex
                : unchecked(_faunaGenomeMutationEpoch + request.Slot + 1u);
            if (rollIndex == 0u)
                rollIndex = 1u;
            ulong originalGenome = request.Genome;
            request.Genome = FaunaGenome64.MutateGenome(
                request.Genome,
                stableHash,
                request.RadiationRads,
                request.Toxicity01,
                request.BrineDepth01,
                rollIndex,
                out byte resultFlags);
            request.StableEntityHash = stableHash;
            request.RollIndex = rollIndex;
            request.ResultFlags = resultFlags;
            if (resultFlags == 0 || request.Genome == originalGenome)
                return false;

            RecordGenomeMutation(
                request.ResultFlags,
                request.RadiationRads,
                request.Toxicity01,
                request.BrineDepth01,
                0,
                0);
            PublishFaunaMutatedSignal(in request);
            return true;
        }

        /// <inheritdoc />
        public void ApplyCampaignToxicityPressure(float toxicity01, uint stageHash, uint frame)
        {
            float clamped = math.saturate(toxicity01);
            _campaignToxicity01 = clamped;
            _campaignToxicityStageHash = stageHash;
            if (clamped <= 0f)
            {
                _campaignToxicityAccumulator = 0f;
                return;
            }

            SetBiomeHostility(math.max(_biomeHostility01, clamped * 0.25f));
            ApplyDirectorHostilityPressure();
        }

        private static uint ResolveGenomeMutationStableHash(in FaunaGenomeMutationRequest request)
        {
            int3 cell = new int3(
                (int)math.floor(request.RuntimePosition.x * InvBiomassMacroCellSizeMeters),
                (int)math.floor(request.RuntimePosition.y * InvBiomassMacroCellSizeMeters),
                (int)math.floor(request.RuntimePosition.z * InvBiomassMacroCellSizeMeters));
            uint hash = math.hash(new int4(cell, request.SpeciesId));
            hash ^= (uint)request.Slot * 747796405u;
            return hash == 0u ? 1u : hash;
        }

        private static bool IsFiniteRuntimePosition(Vector3 runtimePosition)
        {
            return math.all(math.isfinite(new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z)));
        }

        private static Vector3 ResolveMacroSwarmRuntimePosition(in MacroSwarm swarm)
        {
            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(
                ((double)swarm.CurrentSectorAup.x + 0.5d) * BiomassMacroCellSizeMeters,
                0d,
                ((double)swarm.CurrentSectorAup.y + 0.5d) * BiomassMacroCellSizeMeters));
            float3 runtimePosition = positionAup.ToRuntimeFloat3();
            return new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
        }

        private static int ResolveMacroSwarmMutationBatchSize(int count)
        {
            return count <= 32 ? 16 : 32;
        }

        private void SampleMutationScalars(Vector3 runtimePosition, out float radiationRads, out float toxicity01, out float brineDepth01)
        {
            radiationRads = 0f;
            toxicity01 = 0f;
            brineDepth01 = 0f;

            if (!IsFiniteRuntimePosition(runtimePosition))
                return;

            bool invalidScalar = false;
            if (RadiationHazardGrid.TrySampleRadiationIntensity01(runtimePosition, out float radiation01))
            {
                invalidScalar |= !math.isfinite(radiation01);
                radiationRads = SanitizeMutationScalar01(radiation01);
            }

            HazardZoneManager hazardZoneManager = GlobalRegistry.HazardZones;
            if (hazardZoneManager != null)
            {
                float hazardToxicity01 = hazardZoneManager.GetHazardIntensity(runtimePosition, HazardType.Toxicity);
                invalidScalar |= !math.isfinite(hazardToxicity01);
                toxicity01 = math.max(toxicity01, SanitizeMutationScalar01(hazardToxicity01));
            }

            ResourceDistributionDirector resourceDistribution = GlobalRegistry.ResourceDistribution;
            if (resourceDistribution == null || !resourceDistribution.TrySampleBrineLayer(runtimePosition, out BrineLayerSample brineSample))
            {
                if (invalidScalar)
                    RecordInvalidMutationScalarSample(radiationRads, toxicity01, brineDepth01);

                return;
            }

            double shiftOffsetY = HectonFloatingOrigin.CurrentTotalOffsetDouble.y;
            double brineRuntimeHeightY = math.isfinite(brineSample.AbsoluteHeightY) && math.isfinite(shiftOffsetY)
                ? brineSample.AbsoluteHeightY - shiftOffsetY
                : double.NegativeInfinity;
            if (!math.isfinite(runtimePosition.y) || runtimePosition.y >= brineRuntimeHeightY)
            {
                invalidScalar |= !math.isfinite(brineSample.Toxicity01);
                toxicity01 = math.max(toxicity01, SanitizeMutationScalar01(brineSample.Toxicity01));
                if (invalidScalar)
                    RecordInvalidMutationScalarSample(radiationRads, toxicity01, brineDepth01);

                return;
            }

            float resolvedBrineDepth01 = (float)((brineRuntimeHeightY - runtimePosition.y) * 0.1d);
            invalidScalar |= !math.isfinite(resolvedBrineDepth01) || !math.isfinite(brineSample.Toxicity01);
            brineDepth01 = SanitizeMutationScalar01(resolvedBrineDepth01);
            toxicity01 = math.max(toxicity01, SanitizeMutationScalar01(brineSample.Toxicity01));
            if (invalidScalar)
                RecordInvalidMutationScalarSample(radiationRads, toxicity01, brineDepth01);
        }

        private static float SanitizeMutationScalar01(float value)
        {
            return FaunaGenome64.SanitizeScalar01(value);
        }

        private void RecordInvalidMutationScalarSample(float radiationRads, float toxicity01, float brineDepth01)
        {
            int frame = Time.frameCount;
            if (_lastFaunaMutationInvalidScalarFrame == frame)
                return;

            _lastFaunaMutationInvalidScalarFrame = frame;
            _lastMutationRadiationRads = SanitizeMutationScalar01(radiationRads);
            _lastMutationToxicity01 = SanitizeMutationScalar01(toxicity01);
            _lastMutationBrineDepth01 = SanitizeMutationScalar01(brineDepth01);
            PushFaunaMutationBlackBox(1);
        }

        private void RecordGenomeMutation(byte resultFlags, float radiationRads, float toxicity01, float brineDepth01, int headlessMutated, int macroSwarmMutated)
        {
            int mutationCount = math.max(1, headlessMutated + macroSwarmMutated);
            _totalMutatedEntities = math.max(0, _totalMutatedEntities + mutationCount);
            _lastHeadlessMutationCount = headlessMutated;
            _lastMacroSwarmMutationCount = macroSwarmMutated;
            _lastMutationFlags = resultFlags;
            _lastMutationRadiationRads = SanitizeMutationScalar01(radiationRads);
            _lastMutationToxicity01 = SanitizeMutationScalar01(toxicity01);
            _lastMutationBrineDepth01 = SanitizeMutationScalar01(brineDepth01);
            PushFaunaMutationBlackBox(0);
            GlobalTelemetryBus.PublishPerformanceWarning(_FaunaMutationTelemetryHash, _EcosystemDirectorContextHash, _totalMutatedEntities);
        }

        private void PublishFaunaMutatedSignal(in FaunaGenomeMutationRequest request)
        {
            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromRuntimePosition(request.RuntimePosition);
            GlobalSignals.Publish(new FaunaStateChangedSignal
            {
                PositionAup = positionAup,
                SpeciesHash = request.StableEntityHash ^ (uint)request.SpeciesId,
                StateFlags = request.ResultFlags,
                Frame = unchecked((uint)Time.frameCount),
                Slot = request.Slot,
                StateKind = FaunaStateChangedSignalKinds.Mutated,
                Flags = FaunaStateChangedSignalFlags.StateActive
            });
        }

        private void PublishBatchFaunaMutatedSignal(byte resultFlags)
        {
            Vector3 runtimePosition = Vector3.zero;
            if (_scheduledMacroSwarmMutationCount > 0 && _macroSwarms.IsCreated && _activeMacroSwarmCount > 0)
            {
                MacroSwarm firstSwarm = _macroSwarms[0];
                runtimePosition = ResolveMacroSwarmRuntimePosition(in firstSwarm);
            }
            else if (_headlessEntities.Positions.IsCreated && _scheduledHeadlessMutationCount > 0)
            {
                float3 position = _headlessEntities.Positions[0];
                runtimePosition = new Vector3(position.x, position.y, position.z);
            }

            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            GlobalSignals.Publish(new FaunaStateChangedSignal
            {
                PositionAup = positionAup,
                SpeciesHash = FaunaGenomeSaveHeaderMarker,
                StateFlags = resultFlags,
                Frame = unchecked((uint)Time.frameCount),
                Slot = 0,
                StateKind = FaunaStateChangedSignalKinds.Mutated,
                Flags = FaunaStateChangedSignalFlags.StateActive
            });
        }

        /// <inheritdoc />
        public bool TryGetGlobalBiomassAudit(out EcosystemBiomassAuditSample sample)
        {
            sample = default;
            if (!IsInitialized || HasPendingSimulationJob() || !_preyBiomassFront.IsCreated || !_predatorBiomassFront.IsCreated)
                return false;

            int count = math.min(_activeBiomassCellCount, math.min(_preyBiomassFront.Length, _predatorBiomassFront.Length));
            if (count <= 0)
                return false;

            float preySum = 0f;
            float predatorSum = 0f;
            float capacitySum = 0f;
            uint flags = 0u;
            for (int i = 0; i < count; i++)
            {
                float prey = _preyBiomassFront[i];
                float predator = _predatorBiomassFront[i];
                float capacity = _biomassCarryingCapacity.IsCreated && i < _biomassCarryingCapacity.Length
                    ? _biomassCarryingCapacity[i]
                    : 0f;
                bool finite = math.isfinite(prey) && math.isfinite(predator) && math.isfinite(capacity);
                if (!finite || prey < 0f || predator < 0f || capacity < 0f)
                {
                    flags |= 1u;
                    continue;
                }

                preySum += prey;
                predatorSum += predator;
                capacitySum += capacity;
            }

            sample = new EcosystemBiomassAuditSample
            {
                PreyBiomassSum = preySum,
                PredatorBiomassSum = predatorSum,
                CarryingCapacitySum = capacitySum,
                ActiveCellCount = count,
                Sequence = unchecked((uint)Time.frameCount),
                Flags = flags
            };
            return sample.IsFinite;
        }

        /// <inheritdoc />
        public bool TryCopyMacroSwarms(NativeArray<MacroSwarm> destination, out int copiedCount)
        {
            copiedCount = 0;
            if (!destination.IsCreated || !_macroSwarms.IsCreated || _activeMacroSwarmCount <= 0 || _macroSwarmTravelScheduled)
                return false;

            copiedCount = math.min(destination.Length, _activeMacroSwarmCount);
            for (int i = 0; i < copiedCount; i++)
                destination[i] = _macroSwarms[i];
            return copiedCount > 0;
        }

        /// <inheritdoc />
        public bool TryCopyMacroSwarmRadarPings(NativeArray<float4> destination, float3 probeOrigin, float radiusMeters, out int copiedCount)
        {
            copiedCount = 0;
            if (!destination.IsCreated || !_macroSwarms.IsCreated || _activeMacroSwarmCount <= 0 || _macroSwarmTravelScheduled)
                return false;

            float safeRadius = math.select(0f, math.max(0f, radiusMeters), math.isfinite(radiusMeters));
            float radiusSq = safeRadius * safeRadius;
            for (int i = 0; i < _activeMacroSwarmCount && copiedCount < destination.Length; i++)
            {
                MacroSwarm swarm = _macroSwarms[i];
                if (swarm.BiomassValue <= 0.0001f || !math.isfinite(swarm.BiomassValue))
                    continue;

                float3 runtimePosition = ResolveBiomassMacroCellCenterAup(swarm.SectorAup).ToRuntimeFloat3();
                runtimePosition.y = probeOrigin.y;
                float distanceSq = math.lengthsq(runtimePosition - probeOrigin);
                if (!math.isfinite(distanceSq) || distanceSq > radiusSq)
                    continue;

                float signalStrength = math.saturate(0.35f + swarm.BiomassValue * 0.65f);
                destination[copiedCount++] = new float4(runtimePosition, signalStrength);
            }

            return copiedCount > 0;
        }

        /// <inheritdoc />
        public unsafe bool TryImportMacroSwarmsFromVault(ulong sectorHash, out int importedCount)
        {
            importedCount = 0;
            if (sectorHash == 0UL || !_macroSwarms.IsCreated || _macroSwarmTravelScheduled)
                return false;

            IDataVault vault = ResolveDataVault();
            if (vault == null || !vault.TryGetMacroDatabasePayload(sectorHash, out MacroDatabasePayloadHandle handle))
                return false;

            int stride = UnsafeUtility.SizeOf<MacroSwarm>();
            if (handle.Pointer == IntPtr.Zero || handle.ByteLength < stride)
            {
                PushMacroSwarmBlackBox(MacroSwarmBlackBoxFlagInvalid);
                return false;
            }

            int available = handle.ByteLength / stride;
            int cap = ResolveMacroSwarmActiveCap();
            int invalid = 0;
            int overflow = 0;
            void* payload = handle.Pointer.ToPointer();
            for (int i = 0; i < available; i++)
            {
                if (_activeMacroSwarmCount >= cap)
                {
                    overflow++;
                    break;
                }

                MacroSwarm swarm = UnsafeUtility.ReadArrayElement<MacroSwarm>(payload, i);
                if (!TryNormalizeImportedMacroSwarm(ref swarm, sectorHash))
                {
                    invalid++;
                    continue;
                }

                if (HasActiveMacroSwarmRoute(swarm.SectorAup, swarm.TargetSectorAup))
                    continue;

                _macroSwarms[_activeMacroSwarmCount++] = swarm;
                importedCount++;
            }

            int flags = importedCount > 0 ? MacroSwarmBlackBoxFlagDatabaseHydrated : 0;
            if (invalid > 0)
                flags |= MacroSwarmBlackBoxFlagInvalid;
            if (overflow > 0)
                flags |= MacroSwarmBlackBoxFlagCapacityOverflow;
            if (flags != 0)
                PushMacroSwarmBlackBox(flags);

            return importedCount > 0;
        }

        /// <inheritdoc />
        public bool TryClaimMacroSwarmsForHydration(
            in AbsoluteUniversePosition centerAup,
            ushort radiusMetersQ,
            NativeArray<MacroSwarm> destination,
            out int claimedCount,
            out float claimedBiomass01)
        {
            claimedCount = 0;
            claimedBiomass01 = 0f;
            if (!destination.IsCreated ||
                destination.Length <= 0 ||
                !_macroSwarms.IsCreated ||
                _activeMacroSwarmCount <= 0 ||
                _macroSwarmTravelScheduled)
            {
                return false;
            }

            int2 centerCell = QuantizeBiomassMacroCell(in centerAup);
            int radiusCells = math.max(1, (int)math.ceil(math.max(1, radiusMetersQ) * InvBiomassMacroCellSizeMeters));
            int i = 0;
            while (i < _activeMacroSwarmCount && claimedCount < destination.Length)
            {
                MacroSwarm swarm = _macroSwarms[i];
                int2 delta = swarm.SectorAup - centerCell;
                if (math.max(math.abs(delta.x), math.abs(delta.y)) > radiusCells)
                {
                    i++;
                    continue;
                }

                destination[claimedCount++] = swarm;
                claimedBiomass01 = math.saturate(claimedBiomass01 + math.max(0f, swarm.BiomassValue));
                RemoveMacroSwarmSwapBack(i);
            }

            if (claimedCount <= 0)
                return false;

            _lastMacroSwarmsHydrated = claimedCount;
            _lastMacroHydratedBoidEstimate = math.max(0, (int)math.round(claimedBiomass01 * MacroSwarmVisualBoidsPerBiomassUnit));
            PushMacroSwarmBlackBox(MacroSwarmBlackBoxFlagActiveHydrated);
            return true;
        }

        /// <inheritdoc />
        public bool TryRepackHydratedBiotaToMacroSwarm(
            in AbsoluteUniversePosition centerAup,
            ushort radiusMetersQ,
            long chunkId,
            int releasedBoidCount,
            ushort flags,
            out float biomassValue)
        {
            biomassValue = 0f;
            if (!_macroSwarms.IsCreated ||
                _macroSwarmTravelScheduled ||
                releasedBoidCount <= 0 ||
                _activeMacroSwarmCount >= ResolveMacroSwarmActiveCap())
            {
                return false;
            }

            int2 sourceCell = QuantizeBiomassMacroCell(in centerAup);
            int radiusCells = math.max(1, (int)math.ceil(math.max(1, radiusMetersQ) * InvBiomassMacroCellSizeMeters));
            int2 targetCell = ResolveLowestNeighborMacroCell(sourceCell);
            if (math.all(targetCell == sourceCell))
                targetCell += ResolveDeterministicMigrationDirection(sourceCell + radiusCells);

            biomassValue = math.saturate(releasedBoidCount * math.rcp((float)MacroSwarmVisualBoidsPerBiomassUnit));
            bool appended = TryAppendMacroSwarm(
                sourceCell,
                targetCell,
                biomassValue,
                ResolveMacroSwarmSpeedCellsPerSecond(),
                HashMacroSwarm(sourceCell, targetCell, chunkId),
                flags);

            if (!appended)
            {
                biomassValue = 0f;
                return false;
            }

            PushMacroSwarmBlackBox(MacroSwarmBlackBoxFlagActiveDehydrated);
            return true;
        }

        private IDataVault ResolveDataVault()
        {
            IDataVault vault = _dataVault;
            if (vault != null)
                return vault;

            vault = GlobalRegistry.DataVault;
            _dataVault = vault;
            return vault;
        }

        private static bool TryNormalizeImportedMacroSwarm(ref MacroSwarm swarm, ulong sectorHash)
        {
            if (!math.isfinite(swarm.BiomassValue) ||
                !math.isfinite(swarm.Speed) ||
                math.all(swarm.SectorAup == swarm.TargetSectorAup))
            {
                return false;
            }

            if (!math.all(math.isfinite(swarm.CurrentSectorAup)))
                swarm.CurrentSectorAup = new float2(swarm.SectorAup.x, swarm.SectorAup.y);

            swarm.BiomassValue = math.saturate(swarm.BiomassValue);
            swarm.Speed = math.max(0.001f, swarm.Speed);
            if (swarm.BiomassValue <= 0.0001f)
                return false;

            if (swarm.HashId == 0u)
                swarm.HashId = HashMacroSwarm(swarm.SectorAup, swarm.TargetSectorAup, unchecked((long)sectorHash));
            if (swarm.Genome == 0UL)
                swarm.Genome = FaunaGenome64.BuildGenome(swarm.HashId, 1f, 1f + math.saturate(swarm.Speed) * 0.2f);

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
            if (!TryFindIndexEntry(_sectorIndexEntries, PackSectorKey(sectorCoord), out int slotIndex) || slotIndex < 0 || slotIndex >= _activeSectorCount)
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
            preyBirthRatePerSecond = ClampLotkaVolterraRate(preyBirthRatePerSecond);
            predationRatePerSecond = ClampLotkaVolterraRate(predationRatePerSecond);
            predatorGrowthRatePerSecond = ClampLotkaVolterraRate(predatorGrowthRatePerSecond);
            predatorDeathRatePerSecond = ClampLotkaVolterraRate(predatorDeathRatePerSecond);
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

            IDataVault vault = ResolveDataVault();
            if (vault == null)
                return;

            _sectorFrontStates = VaultNativeArray<SectorPopulationState>.Create(vault, vault.GetBufferHandle<SectorPopulationState>(BufferID.EcosystemSectorFrontStates, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _sectorBackStates = VaultNativeArray<SectorPopulationState>.Create(vault, vault.GetBufferHandle<SectorPopulationState>(BufferID.EcosystemSectorBackStates, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _preyFrontCounts = VaultNativeArray<int>.Create(vault, vault.GetBufferHandle<int>(BufferID.EcosystemPreyFrontCounts, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _preyBackCounts = VaultNativeArray<int>.Create(vault, vault.GetBufferHandle<int>(BufferID.EcosystemPreyBackCounts, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _predatorFrontCounts = VaultNativeArray<int>.Create(vault, vault.GetBufferHandle<int>(BufferID.EcosystemPredatorFrontCounts, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _predatorBackCounts = VaultNativeArray<int>.Create(vault, vault.GetBufferHandle<int>(BufferID.EcosystemPredatorBackCounts, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _preyBiomassFront = VaultNativeArray<float>.Create(vault, vault.GetBufferHandle<float>(BufferID.EcosystemPreyBiomassFront, maxTrackedBiomassCells, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _preyBiomassBack = VaultNativeArray<float>.Create(vault, vault.GetBufferHandle<float>(BufferID.EcosystemPreyBiomassBack, maxTrackedBiomassCells, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _predatorBiomassFront = VaultNativeArray<float>.Create(vault, vault.GetBufferHandle<float>(BufferID.EcosystemPredatorBiomassFront, maxTrackedBiomassCells, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _predatorBiomassBack = VaultNativeArray<float>.Create(vault, vault.GetBufferHandle<float>(BufferID.EcosystemPredatorBiomassBack, maxTrackedBiomassCells, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _biomassCarryingCapacity = VaultNativeArray<float>.Create(vault, vault.GetBufferHandle<float>(BufferID.EcosystemBiomassCarryingCapacity, maxTrackedBiomassCells, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _biomassSumScratch = VaultNativeArray<float>.Create(vault, vault.GetBufferHandle<float>(BufferID.EcosystemBiomassSumScratch, maxTrackedBiomassCells, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _biomassMacroCellCoords = VaultNativeArray<int2>.Create(vault, vault.GetBufferHandle<int2>(BufferID.EcosystemBiomassMacroCellCoords, maxTrackedBiomassCells, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _biomassCellFlags = VaultNativeArray<byte>.Create(vault, vault.GetBufferHandle<byte>(BufferID.EcosystemBiomassCellFlags, maxTrackedBiomassCells, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _biomassIndexEntries = VaultNativeArray<EcosystemIndexEntry>.Create(vault, vault.GetBufferHandle<EcosystemIndexEntry>(BufferID.EcosystemBiomassIndexEntries, ResolveVaultIndexCapacity(maxTrackedBiomassCells), SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _pendingBiomassImpacts = VaultNativeArray<BiomassImpactEvent>.Create(vault, vault.GetBufferHandle<BiomassImpactEvent>(BufferID.EcosystemPendingBiomassImpacts, BiomassImpactQueueCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _biomassBlackBox = VaultNativeArray<BiomassTelemetryEntry>.Create(vault, vault.GetBufferHandle<BiomassTelemetryEntry>(BufferID.EcosystemBiomassBlackBox, BiomassBlackBoxCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _macroSwarms = VaultNativeArray<MacroSwarm>.Create(vault, vault.GetBufferHandle<MacroSwarm>(BufferID.EcosystemMacroSwarms, MacroSwarmCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _macroSwarmArrivals = VaultNativeArray<MacroSwarmArrival>.Create(vault, vault.GetBufferHandle<MacroSwarmArrival>(BufferID.EcosystemMacroSwarmArrivals, MacroSwarmArrivalCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _macroSwarmCounters = VaultNativeArray<int>.Create(vault, vault.GetBufferHandle<int>(BufferID.EcosystemMacroSwarmCounters, 4, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _macroSwarmBlackBox = VaultNativeArray<MacroSwarmTelemetryEntry>.Create(vault, vault.GetBufferHandle<MacroSwarmTelemetryEntry>(BufferID.EcosystemMacroSwarmBlackBox, MacroSwarmBlackBoxCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _macroSwarmMutationRadiation = VaultNativeArray<float>.Create(vault, vault.GetBufferHandle<float>(BufferID.EcosystemMacroSwarmMutationRadiation, MacroSwarmCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _macroSwarmMutationToxicity = VaultNativeArray<float>.Create(vault, vault.GetBufferHandle<float>(BufferID.EcosystemMacroSwarmMutationToxicity, MacroSwarmCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _macroSwarmMutationBrine = VaultNativeArray<float>.Create(vault, vault.GetBufferHandle<float>(BufferID.EcosystemMacroSwarmMutationBrine, MacroSwarmCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _macroSwarmMutationResults = VaultNativeArray<byte>.Create(vault, vault.GetBufferHandle<byte>(BufferID.EcosystemMacroSwarmMutationResults, MacroSwarmCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _faunaMutationBlackBox = VaultNativeArray<FaunaMutationTelemetryEntry>.Create(vault, vault.GetBufferHandle<FaunaMutationTelemetryEntry>(BufferID.EcosystemFaunaMutationBlackBox, FaunaMutationBlackBoxCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _macroHydrationScratch = VaultNativeArray<MacroSwarm>.Create(vault, vault.GetBufferHandle<MacroSwarm>(BufferID.EcosystemMacroHydrationScratch, MacroSwarmSignalScratchCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _macroDehydrationScratch = VaultNativeArray<MacroSwarm>.Create(vault, vault.GetBufferHandle<MacroSwarm>(BufferID.EcosystemMacroDehydrationScratch, MacroSwarmSignalScratchCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _headlessEntities = new HeadlessEntitySoA
            {
                Positions = VaultNativeArray<float3>.Create(vault, vault.GetBufferHandle<float3>(BufferID.EcosystemHeadlessPositions, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory)),
                SpeciesID = VaultNativeArray<byte>.Create(vault, vault.GetBufferHandle<byte>(BufferID.EcosystemHeadlessSpeciesId, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory)),
                Hunger = VaultNativeArray<byte>.Create(vault, vault.GetBufferHandle<byte>(BufferID.EcosystemHeadlessHunger, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory)),
                SectorCoord = VaultNativeArray<int2>.Create(vault, vault.GetBufferHandle<int2>(BufferID.EcosystemHeadlessSectorCoord, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory)),
                SectorID = VaultNativeArray<int>.Create(vault, vault.GetBufferHandle<int>(BufferID.EcosystemHeadlessSectorId, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory)),
                FaunaGenomes = VaultNativeArray<ulong>.Create(vault, vault.GetBufferHandle<ulong>(BufferID.EcosystemHeadlessFaunaGenomes, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory)),
                MutationRadiation = VaultNativeArray<float>.Create(vault, vault.GetBufferHandle<float>(BufferID.EcosystemHeadlessMutationRadiation, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory)),
                MutationToxicity = VaultNativeArray<float>.Create(vault, vault.GetBufferHandle<float>(BufferID.EcosystemHeadlessMutationToxicity, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory)),
                MutationBrine = VaultNativeArray<float>.Create(vault, vault.GetBufferHandle<float>(BufferID.EcosystemHeadlessMutationBrine, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory)),
                MutationStableHashes = VaultNativeArray<uint>.Create(vault, vault.GetBufferHandle<uint>(BufferID.EcosystemHeadlessMutationStableHashes, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory)),
                MutationResults = VaultNativeArray<byte>.Create(vault, vault.GetBufferHandle<byte>(BufferID.EcosystemHeadlessMutationResults, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory))
            };
            _sectorIndexEntries = VaultNativeArray<EcosystemIndexEntry>.Create(vault, vault.GetBufferHandle<EcosystemIndexEntry>(BufferID.EcosystemSectorIndexEntries, ResolveVaultIndexCapacity(maxTrackedSectors), SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _apexTerritorySamples = VaultNativeArray<ApexTerritorySample>.Create(vault, vault.GetBufferHandle<ApexTerritorySample>(BufferID.EcosystemApexTerritorySamples, ApexTerritoryOverlapCandidateCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _apexTerritoryOverlapResults = VaultNativeArray<ApexTerritoryOverlapResult>.Create(vault, vault.GetBufferHandle<ApexTerritoryOverlapResult>(BufferID.EcosystemApexTerritoryOverlapResults, ApexTerritoryOverlapCandidateCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _apexSpawnGateCommands = VaultNativeArray<CapsulecastCommand>.Create(vault, vault.GetBufferHandle<CapsulecastCommand>(BufferID.EcosystemApexSpawnGateCommands, ApexSpawnGateCommandCount, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _apexSpawnGateHits = VaultNativeArray<RaycastHit>.Create(vault, vault.GetBufferHandle<RaycastHit>(BufferID.EcosystemApexSpawnGateHits, ApexSpawnGateMaxHits, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _floraPredatorAupUpload = VaultNativeArray<float4>.Create(vault, vault.GetBufferHandle<float4>(BufferID.EcosystemFloraPredatorAupUpload, FloraPredatorAupBufferCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _saveSnapshotSectors = VaultNativeArray<EcosystemSectorSaveRecord>.Create(vault, vault.GetBufferHandle<EcosystemSectorSaveRecord>(
                BufferID.EcosystemSaveSnapshotSectors,
                maxTrackedSectors + maxTrackedBiomassCells + (maxTrackedSectors * 2) + (MacroSwarmCapacity * 4),
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory));
            _saveSnapshotBiomassRuns = VaultNativeArray<EcosystemBiomassSaveRun>.Create(vault, vault.GetBufferHandle<EcosystemBiomassSaveRun>(
                BufferID.EcosystemSaveSnapshotBiomassRuns,
                maxTrackedBiomassCells,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory));
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
            _macroSwarmBlackBoxCursor = 0;
            _faunaMutationBlackBoxCursor = 0;
            _macroHydrationScratchCount = 0;
            _macroDehydrationScratchCount = 0;
            _totalMutatedEntities = 0;
            _lastHeadlessMutationCount = 0;
            _lastMacroSwarmMutationCount = 0;
            _scheduledHeadlessMutationCount = 0;
            _scheduledMacroSwarmMutationCount = 0;
            _faunaGenomeMutationEpoch = 0u;
            _lastFaunaMutationInvalidScalarFrame = -1;
            _lastMutationFlags = 0u;
            _lastMutationRadiationRads = 0f;
            _lastMutationToxicity01 = 0f;
            _lastMutationBrineDepth01 = 0f;
            _activeMacroSwarmCount = 0;
            _lastMacroSwarmArrivalCount = 0;
            _lastMacroSwarmsHydrated = 0;
            _lastMacroHydratedBoidEstimate = 0;
            _lastMacroSwarmBiomassSum = 0f;
            _lastMacroSwarmStateHash = 0u;
            _lastSectorResidencySignalDrainFrame = -1;
            _saveSnapshotSectorCount = 0;
            _saveSnapshotBiomassRunCount = 0;
            _scheduledApexTerritoryOverlapCount = 0;
            _coldTickAccumulator = 0f;
            _scheduledSolveHandle = default;
            _scheduledGenomeMutationHandle = default;
            _macroSwarmTravelHandle = default;
            _scheduledApexTerritoryOverlapHandle = default;
            _apexSpawnGateHandle = default;
            _apexSpawnGatePendingCell = int3.zero;
            _apexSpawnGateCachedCell = int3.zero;
            _apexSpawnGateScheduled = false;
            _apexSpawnGateHasCachedResult = false;
            _apexSpawnGateCachedBlocked = 0;
            _solveScheduled = false;
            _genomeMutationScheduled = false;
            _macroSwarmTravelScheduled = false;
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
            if (_genomeMutationScheduled)
                disposeDependency = JobHandle.CombineDependencies(disposeDependency, _scheduledGenomeMutationHandle);
            if (_macroSwarmTravelScheduled)
                disposeDependency = JobHandle.CombineDependencies(disposeDependency, _macroSwarmTravelHandle);
            if (_apexTerritoryOverlapScheduled)
                disposeDependency = JobHandle.CombineDependencies(disposeDependency, _scheduledApexTerritoryOverlapHandle);
            disposeDependency = JobHandle.CombineDependencies(disposeDependency, _apexSpawnGateHandle);

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
            _biomassIndexEntries = default;
            _pendingBiomassImpacts = default;
            _biomassBlackBox = default;
            _macroSwarms = default;
            _macroSwarmArrivals = default;
            _macroSwarmCounters = default;
            _macroSwarmBlackBox = default;
            _macroSwarmMutationRadiation = default;
            _macroSwarmMutationToxicity = default;
            _macroSwarmMutationBrine = default;
            _macroSwarmMutationResults = default;
            _faunaMutationBlackBox = default;
            _macroHydrationScratch = default;
            _macroDehydrationScratch = default;
            _macroHydrationScratchCount = 0;
            _macroDehydrationScratchCount = 0;
            _headlessEntities = default;
            _sectorFoodHeatmapR8 = default;
            _sectorFoodHeatmapSize = default;
            _sectorIndexEntries = default;
            _apexTerritorySamples = default;
            _apexTerritoryOverlapResults = default;
            _apexSpawnGateCommands = default;
            _apexSpawnGateHits = default;
            _floraPredatorAupUpload = default;
            _saveSnapshotSectors = default;
            _saveSnapshotBiomassRuns = default;
            _saveSnapshotSectorCount = 0;
            _saveSnapshotBiomassRunCount = 0;
            _apexTerritoryBrains = null;
            _dataVault = null;
            _macroSectorSnapshotHandle = default;
            _macroSectorIndexHandle = default;
            _macroTuningHandle = default;
            _activeSectorCount = 0;
            _activeBiomassCellCount = 0;
            _pendingBiomassImpactCount = 0;
            _lastBiomassSignalDrainFrame = -1;
            _lastScannerWarningFrame = -1024;
            _biomassBlackBoxCursor = 0;
            _macroSwarmBlackBoxCursor = 0;
            _faunaMutationBlackBoxCursor = 0;
            _totalMutatedEntities = 0;
            _lastHeadlessMutationCount = 0;
            _lastMacroSwarmMutationCount = 0;
            _scheduledHeadlessMutationCount = 0;
            _scheduledMacroSwarmMutationCount = 0;
            _faunaGenomeMutationEpoch = 0u;
            _lastFaunaMutationInvalidScalarFrame = -1;
            _lastMutationFlags = 0u;
            _lastMutationRadiationRads = 0f;
            _lastMutationToxicity01 = 0f;
            _lastMutationBrineDepth01 = 0f;
            _activeMacroSwarmCount = 0;
            _lastMacroSwarmArrivalCount = 0;
            _lastMacroSwarmsHydrated = 0;
            _lastMacroHydratedBoidEstimate = 0;
            _lastMacroSwarmBiomassSum = 0f;
            _lastMacroSwarmStateHash = 0u;
            _lastSectorResidencySignalDrainFrame = -1;
            _scheduledApexTerritoryOverlapCount = 0;
            _coldTickAccumulator = 0f;
            _scheduledSolveHandle = default;
            _scheduledGenomeMutationHandle = default;
            _macroSwarmTravelHandle = default;
            _scheduledApexTerritoryOverlapHandle = default;
            _apexSpawnGateHandle = default;
            _apexSpawnGatePendingCell = int3.zero;
            _apexSpawnGateCachedCell = int3.zero;
            _apexSpawnGateScheduled = false;
            _apexSpawnGateHasCachedResult = false;
            _apexSpawnGateCachedBlocked = 0;
            _solveScheduled = false;
            _genomeMutationScheduled = false;
            _macroSwarmTravelScheduled = false;
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

        private void TryRegisterFrostTickable()
        {
            if (_registeredFrostTickable || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterFrostTickable(this, PriorityLayer.Environment);
            _registeredFrostTickable = SystemDispatcher.GetFrostLane(PriorityLayer.Environment).Contains(this);
        }

        private void TryUnregisterFrostTickable()
        {
            if (!_registeredFrostTickable)
                return;

            GlobalRegistry.UnregisterFrostTickable(this, PriorityLayer.Environment);
            _registeredFrostTickable = false;
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

        private void TickCampaignToxicityPressure(float dt)
        {
            if (_campaignToxicity01 <= 0f)
                return;

            _campaignToxicityAccumulator += math.max(0f, dt);
            if (_campaignToxicityAccumulator < CampaignToxicityDrainIntervalSeconds)
                return;

            _campaignToxicityAccumulator = 0f;
            if (!TryResolvePlayerRuntimePosition(out Vector3 playerPosition))
                return;

            if (ResolveDepthMeters(playerPosition) > CampaignToxicitySafeDepthMeters)
                return;

            float amount = CampaignToxicityPreyDrainPerPulse01 * math.max(0.1f, _campaignToxicity01);
            QueueOrApplyBiomassImpact(playerPosition, BiomassImpactKindCampaignToxicity, amount);
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
                    CellIndexEntries = _biomassIndexEntries,
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
            return _solveScheduled || _genomeMutationScheduled;
        }

        private void CompleteScheduledSimulation(bool forceComplete)
        {
            CompleteScheduledGenomeMutation(forceComplete);
            CompleteScheduledSolve(forceComplete);
        }

        private void CompleteScheduledSolve(bool forceComplete)
        {
            if (!_solveScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _scheduledSolveHandle, forceComplete))
                return;

            VaultNativeArray<SectorPopulationState> stateSwap = _sectorFrontStates;
            _sectorFrontStates = _sectorBackStates;
            _sectorBackStates = stateSwap;
            VaultNativeArray<int> preySwap = _preyFrontCounts;
            _preyFrontCounts = _preyBackCounts;
            _preyBackCounts = preySwap;
            VaultNativeArray<int> predatorSwap = _predatorFrontCounts;
            _predatorFrontCounts = _predatorBackCounts;
            _predatorBackCounts = predatorSwap;
            if (_activeBiomassCellCount > 0)
            {
                VaultNativeArray<float> preyBiomassSwap = _preyBiomassFront;
                _preyBiomassFront = _preyBiomassBack;
                _preyBiomassBack = preyBiomassSwap;
                VaultNativeArray<float> predatorBiomassSwap = _predatorBiomassFront;
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

        private void DrainSectorResidencySignalSnapshots()
        {
            int frame = Time.frameCount;
            if (_lastSectorResidencySignalDrainFrame == frame || HasPendingSimulationJob() || _macroSwarmTravelScheduled)
                return;

            _lastSectorResidencySignalDrainFrame = frame;

            ReadOnlySpan<SectorDehydratedSignal> dehydratedSignals = SignalBus<SectorDehydratedSignal>.GetFrameSnapshot();
            if (_macroDehydrationScratch.IsCreated)
                _macroDehydrationScratchCount = 0;
            for (int i = 0; i < dehydratedSignals.Length; i++)
                StageDehydratedSectorSwarm(in dehydratedSignals[i]);

            if (_macroDehydrationScratch.IsCreated)
            {
                int count = math.min(_macroDehydrationScratchCount, _macroDehydrationScratch.Length);
                for (int i = 0; i < count; i++)
                {
                    MacroSwarm staged = _macroDehydrationScratch[i];
                    TryAppendMacroSwarm(staged.SectorAup, staged.TargetSectorAup, staged.BiomassValue, staged.Speed, staged.HashId, staged.Flags);
                }
            }

            ReadOnlySpan<MacroDatabaseSectorHydrationSignal> macroDatabaseHydratedSignals = SignalBus<MacroDatabaseSectorHydrationSignal>.GetFrameSnapshot();
            for (int i = 0; i < macroDatabaseHydratedSignals.Length; i++)
            {
                if (TryImportMacroSwarmsFromVault(macroDatabaseHydratedSignals[i].SectorHash, out _))
                    continue;

                PushMacroSwarmBlackBox(MacroSwarmBlackBoxFlagDatabaseHydrated);
            }

            ReadOnlySpan<SectorResidencyHydratedSignal> hydratedSignals = SignalBus<SectorResidencyHydratedSignal>.GetFrameSnapshot();
            for (int i = 0; i < hydratedSignals.Length; i++)
                HydrateSectorMacroSwarms(in hydratedSignals[i]);
        }

        private void StageDehydratedSectorSwarm(in SectorDehydratedSignal signal)
        {
            if (!_macroDehydrationScratch.IsCreated ||
                _macroDehydrationScratchCount >= _macroDehydrationScratch.Length ||
                _activeMacroSwarmCount >= ResolveMacroSwarmActiveCap())
            {
                PushMacroSwarmBlackBox(MacroSwarmBlackBoxFlagCapacityOverflow);
                return;
            }

            if (TryStageActiveBiotaDehydration(in signal))
                return;

            int2 sourceCell = QuantizeBiomassMacroCell(in signal.CenterAup);
            int sourceSlot = ResolveOrCreateBiomassCellSlot(sourceCell, seedWithBaseline: true);
            if (sourceSlot < 0)
                return;

            float capacity = math.max(0.0001f, _biomassCarryingCapacity[sourceSlot]);
            float prey = math.clamp(_preyBiomassFront[sourceSlot], 0f, capacity);
            float drained = math.min(prey, capacity * MacroSwarmDehydrationTransferFraction01);
            if (drained <= 0.0001f)
                return;

            int2 targetCell = ResolveLowestNeighborMacroCell(sourceCell);
            if (math.all(targetCell == sourceCell))
                targetCell += ResolveDeterministicMigrationDirection(sourceCell);

            _preyBiomassFront[sourceSlot] = prey - drained;
            _preyBiomassBack[sourceSlot] = _preyBiomassFront[sourceSlot];
            _biomassSumScratch[sourceSlot] = _preyBiomassFront[sourceSlot] + _predatorBiomassFront[sourceSlot];

            MacroSwarm swarm = CreateMacroSwarm(
                sourceCell,
                targetCell,
                drained * math.rcp(capacity),
                ResolveMacroSwarmSpeedCellsPerSecond(),
                HashMacroSwarm(sourceCell, targetCell, signal.ChunkId),
                signal.Flags);
            _macroDehydrationScratch[_macroDehydrationScratchCount++] = swarm;
        }

        private bool TryStageActiveBiotaDehydration(in SectorDehydratedSignal signal)
        {
            IAmbientBiotaService activeBiota = GlobalRegistry.AmbientBiota;
            if (activeBiota == null ||
                !activeBiota.IsInitialized ||
                !activeBiota.TryPackMacroHydratedBiota(
                    in signal.CenterAup,
                    signal.RadiusMetersQ,
                    out int releasedBoidCount,
                    out float biomassValue) ||
                releasedBoidCount <= 0 ||
                biomassValue <= 0.0001f)
            {
                return false;
            }

            int2 sourceCell = QuantizeBiomassMacroCell(in signal.CenterAup);
            int2 targetCell = ResolveLowestNeighborMacroCell(sourceCell);
            if (math.all(targetCell == sourceCell))
                targetCell += ResolveDeterministicMigrationDirection(sourceCell);

            MacroSwarm swarm = CreateMacroSwarm(
                sourceCell,
                targetCell,
                biomassValue,
                ResolveMacroSwarmSpeedCellsPerSecond(),
                HashMacroSwarm(sourceCell, targetCell, signal.ChunkId),
                signal.Flags);
            _macroDehydrationScratch[_macroDehydrationScratchCount++] = swarm;
            _lastMacroHydratedBoidEstimate = releasedBoidCount;
            PushMacroSwarmBlackBox(MacroSwarmBlackBoxFlagActiveDehydrated);
            return true;
        }

        private void HydrateSectorMacroSwarms(in SectorResidencyHydratedSignal signal)
        {
            if (!_macroSwarms.IsCreated || _activeMacroSwarmCount <= 0)
                return;

            int2 centerCell = QuantizeBiomassMacroCell(in signal.CenterAup);
            int radiusCells = math.max(1, (int)math.ceil(math.max(1, signal.RadiusMetersQ) * InvBiomassMacroCellSizeMeters));
            if (_macroHydrationScratch.IsCreated)
                _macroHydrationScratchCount = 0;

            int i = 0;
            bool scratchOverflow = false;
            while (i < _activeMacroSwarmCount)
            {
                MacroSwarm swarm = _macroSwarms[i];
                int2 delta = swarm.SectorAup - centerCell;
                if (math.max(math.abs(delta.x), math.abs(delta.y)) > radiusCells)
                {
                    i++;
                    continue;
                }

                if (_macroHydrationScratch.IsCreated && _macroHydrationScratchCount < _macroHydrationScratch.Length)
                    _macroHydrationScratch[_macroHydrationScratchCount++] = swarm;
                else
                    scratchOverflow = true;
                RemoveMacroSwarmSwapBack(i);
            }

            if (!_macroHydrationScratch.IsCreated || _macroHydrationScratchCount <= 0)
                return;

            int spawnedBoids = 0;
            bool activeHydrated = TryHydrateActiveBiotaFromScratch(in signal, out spawnedBoids);
            if (!activeHydrated)
            {
                int count = math.min(_macroHydrationScratchCount, _macroHydrationScratch.Length);
                for (int scratchIndex = 0; scratchIndex < count; scratchIndex++)
                    AddPreyBiomassToCell(centerCell, _macroHydrationScratch[scratchIndex].BiomassValue);
            }

            _lastMacroSwarmsHydrated = _macroHydrationScratchCount;
            _lastMacroHydratedBoidEstimate = spawnedBoids;
            PushMacroSwarmBlackBox(MacroSwarmBlackBoxFlagActiveHydrated | (scratchOverflow ? MacroSwarmBlackBoxFlagCapacityOverflow : 0));
            PublishHydratedMacroSwarmBurst(centerCell, signal.RadiusMetersQ, spawnedBoids);
        }

        private bool TryHydrateActiveBiotaFromScratch(in SectorResidencyHydratedSignal signal, out int spawnedBoids)
        {
            spawnedBoids = 0;
            if (!_macroHydrationScratch.IsCreated || _macroHydrationScratchCount <= 0)
                return false;

            IAmbientBiotaService activeBiota = GlobalRegistry.AmbientBiota;
            if (activeBiota == null || !activeBiota.IsInitialized)
                return false;

            return activeBiota.TryHydrateMacroSwarms(
                in signal.CenterAup,
                signal.RadiusMetersQ,
                _macroHydrationScratch,
                math.min(_macroHydrationScratchCount, _macroHydrationScratch.Length),
                _macroSwarmQualityTierProfileByte,
                SignalBusRegistry.SystemStress01,
                out spawnedBoids);
        }

        private void SpawnMacroSwarmDiffusionGradient()
        {
            if (!_macroSwarms.IsCreated || _activeBiomassCellCount <= 0)
                return;

            int cap = ResolveMacroSwarmActiveCap();
            for (int targetIndex = 0; targetIndex < _activeBiomassCellCount && _activeMacroSwarmCount < cap; targetIndex++)
            {
                float targetCapacity = math.max(0.0001f, _biomassCarryingCapacity[targetIndex]);
                float targetPrey01 = math.saturate(_preyBiomassFront[targetIndex] * math.rcp(targetCapacity));
                if (targetPrey01 > MacroSwarmDiffusionLowThreshold01)
                    continue;

                int2 targetCell = _biomassMacroCellCoords[targetIndex];
                if (TrySpawnDiffusionFromNeighbor(targetCell + new int2(1, 0), targetCell, targetIndex) ||
                    TrySpawnDiffusionFromNeighbor(targetCell + new int2(-1, 0), targetCell, targetIndex) ||
                    TrySpawnDiffusionFromNeighbor(targetCell + new int2(0, 1), targetCell, targetIndex) ||
                    TrySpawnDiffusionFromNeighbor(targetCell + new int2(0, -1), targetCell, targetIndex))
                {
                    continue;
                }
            }
        }

        private bool TrySpawnDiffusionFromNeighbor(int2 sourceCell, int2 targetCell, int targetSlot)
        {
            if (_activeMacroSwarmCount >= ResolveMacroSwarmActiveCap() || HasActiveMacroSwarmRoute(sourceCell, targetCell))
                return false;

            if (!TryFindIndexEntry(_biomassIndexEntries, PackBiomassCellKey(sourceCell), out int sourceSlot) ||
                sourceSlot < 0 ||
                sourceSlot >= _activeBiomassCellCount)
            {
                return false;
            }

            float sourceCapacity = math.max(0.0001f, _biomassCarryingCapacity[sourceSlot]);
            float sourcePrey = math.clamp(_preyBiomassFront[sourceSlot], 0f, sourceCapacity);
            float sourcePrey01 = math.saturate(sourcePrey * math.rcp(sourceCapacity));
            if (sourcePrey01 < MacroSwarmDiffusionHighThreshold01)
                return false;

            float targetCapacity = math.max(0.0001f, _biomassCarryingCapacity[targetSlot]);
            float transfer = math.min(sourcePrey - (sourceCapacity * 0.5f), sourceCapacity * MacroSwarmTransferFraction01);
            transfer = math.min(transfer, math.max(0f, targetCapacity - _preyBiomassFront[targetSlot]));
            if (transfer <= 0.0001f)
                return false;

            _preyBiomassFront[sourceSlot] = sourcePrey - transfer;
            _preyBiomassBack[sourceSlot] = _preyBiomassFront[sourceSlot];
            _biomassSumScratch[sourceSlot] = _preyBiomassFront[sourceSlot] + _predatorBiomassFront[sourceSlot];
            return TryAppendMacroSwarm(
                sourceCell,
                targetCell,
                transfer * math.rcp(sourceCapacity),
                ResolveMacroSwarmSpeedCellsPerSecond(),
                HashMacroSwarm(sourceCell, targetCell, 0L),
                0);
        }

        private void ApplyMacroSwarmPredatorAttraction()
        {
            for (int i = 0; i < _activeMacroSwarmCount; i++)
            {
                MacroSwarm swarm = _macroSwarms[i];
                if (!TryFindIndexEntry(_biomassIndexEntries, PackBiomassCellKey(swarm.SectorAup), out int slotIndex) ||
                    slotIndex < 0 ||
                    slotIndex >= _activeBiomassCellCount)
                {
                    continue;
                }

                float capacity = math.max(0.0001f, _biomassCarryingCapacity[slotIndex]);
                float predator01 = math.saturate(_predatorBiomassFront[slotIndex] * math.rcp(capacity));
                if (predator01 < MacroSwarmPredatorHighThreshold01)
                    continue;

                swarm.BiomassValue = math.saturate(swarm.BiomassValue * (1f - MacroSwarmPredatorBiteFraction01));
                swarm.Flags |= 1;
                _macroSwarms[i] = swarm;
            }
        }

        private JobHandle ScheduleFaunaGenomeMutation()
        {
            _scheduledHeadlessMutationCount = 0;
            _scheduledMacroSwarmMutationCount = 0;
            if (_genomeMutationScheduled || _macroSwarmQualityTierProfileByte == 0)
                return default;

            JobHandle dependency = default;
            bool scheduled = false;
            uint rollIndex = unchecked(++_faunaGenomeMutationEpoch);
            if (rollIndex == 0u)
                rollIndex = unchecked(++_faunaGenomeMutationEpoch);
            int headlessCount = PrepareHeadlessGenomeMutationInputs();
            if (headlessCount > 0)
            {
                var job = new FaunaGenomeMutationJob
                {
                    Genomes = _headlessEntities.FaunaGenomes,
                    Radiation = _headlessEntities.MutationRadiation,
                    Toxicity = _headlessEntities.MutationToxicity,
                    Brine = _headlessEntities.MutationBrine,
                    StableHashes = _headlessEntities.MutationStableHashes,
                    MutationResults = _headlessEntities.MutationResults,
                    RollIndex = rollIndex,
                    Count = headlessCount
                };
                dependency = job.Schedule(headlessCount, ResolveSectorJobBatchSize(headlessCount));
                _scheduledHeadlessMutationCount = headlessCount;
                scheduled = true;
            }

            int macroSwarmCount = PrepareMacroSwarmMutationInputs();
            if (macroSwarmCount > 0)
            {
                var macroJob = new MacroSwarmGenomeMutationJob
                {
                    Swarms = _macroSwarms,
                    Radiation = _macroSwarmMutationRadiation,
                    Toxicity = _macroSwarmMutationToxicity,
                    Brine = _macroSwarmMutationBrine,
                    MutationResults = _macroSwarmMutationResults,
                    RollIndex = rollIndex,
                    Count = macroSwarmCount
                };
                dependency = macroJob.Schedule(macroSwarmCount, ResolveMacroSwarmMutationBatchSize(macroSwarmCount), dependency);
                _scheduledMacroSwarmMutationCount = macroSwarmCount;
                scheduled = true;
            }

            if (!scheduled)
                return default;

            _scheduledGenomeMutationHandle = dependency;
            _genomeMutationScheduled = true;
            return dependency;
        }

        private int PrepareHeadlessGenomeMutationInputs()
        {
            if (!_headlessEntities.IsCreated || _activeSectorCount <= 0)
                return 0;

            NativeArray<float3> positions = _headlessEntities.Positions;
            NativeArray<int2> sectorCoords = _headlessEntities.SectorCoord;
            NativeArray<ulong> faunaGenomes = _headlessEntities.FaunaGenomes;
            NativeArray<float> mutationRadiation = _headlessEntities.MutationRadiation;
            NativeArray<float> mutationToxicity = _headlessEntities.MutationToxicity;
            NativeArray<float> mutationBrine = _headlessEntities.MutationBrine;
            NativeArray<uint> mutationStableHashes = _headlessEntities.MutationStableHashes;
            NativeArray<byte> mutationResults = _headlessEntities.MutationResults;
            int count = math.min(
                _activeSectorCount,
                math.min(faunaGenomes.Length, mutationResults.Length));
            for (int i = 0; i < count; i++)
            {
                float3 position = positions[i];
                if (!math.all(math.isfinite(position)))
                    position = ResolveSectorCenterPosition(sectorCoords[i]);

                Vector3 runtimePosition = new Vector3(position.x, position.y, position.z);
                SampleMutationScalars(runtimePosition, out float radiationRads, out float toxicity01, out float brineDepth01);
                mutationRadiation[i] = radiationRads;
                mutationToxicity[i] = toxicity01;
                mutationBrine[i] = brineDepth01;
                uint stableHash = mutationStableHashes[i] != 0u
                    ? mutationStableHashes[i]
                    : MixSectorBits(sectorCoords[i].x, sectorCoords[i].y);
                mutationStableHashes[i] = stableHash != 0u ? stableHash : (uint)(i + 1);
                if (faunaGenomes[i] == 0UL)
                {
                    float speed = i < _sectorFrontStates.Length ? _sectorFrontStates[i].SpeedMultiplier : 1f;
                    faunaGenomes[i] = FaunaGenome64.BuildGenome(mutationStableHashes[i], 1f, speed);
                }

                mutationResults[i] = 0;
            }

            return count;
        }

        private int PrepareMacroSwarmMutationInputs()
        {
            if (!_macroSwarms.IsCreated ||
                !_macroSwarmMutationRadiation.IsCreated ||
                _activeMacroSwarmCount <= 0)
            {
                return 0;
            }

            int count = math.min(_activeMacroSwarmCount, _macroSwarmMutationRadiation.Length);
            for (int i = 0; i < count; i++)
            {
                MacroSwarm swarm = _macroSwarms[i];
                if (swarm.HashId == 0u)
                {
                    _macroSwarmMutationRadiation[i] = 0f;
                    _macroSwarmMutationToxicity[i] = 0f;
                    _macroSwarmMutationBrine[i] = 0f;
                    _macroSwarmMutationResults[i] = 0;
                    continue;
                }

                if (swarm.Genome == 0UL)
                {
                    swarm.Genome = FaunaGenome64.BuildGenome(swarm.HashId, 1f, 1f + math.saturate(swarm.Speed) * 0.2f);
                    _macroSwarms[i] = swarm;
                }

                Vector3 runtimePosition = ResolveMacroSwarmRuntimePosition(in swarm);
                SampleMutationScalars(runtimePosition, out float radiationRads, out float toxicity01, out float brineDepth01);
                _macroSwarmMutationRadiation[i] = radiationRads;
                _macroSwarmMutationToxicity[i] = toxicity01;
                _macroSwarmMutationBrine[i] = brineDepth01;
                _macroSwarmMutationResults[i] = 0;
            }

            return count;
        }

        private void CompleteScheduledGenomeMutation(bool forceComplete)
        {
            if (!_genomeMutationScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _scheduledGenomeMutationHandle, forceComplete))
                return;

            _genomeMutationScheduled = false;
            int headlessMutated = 0;
            int macroMutated = 0;
            uint flags = 0u;
            float maxRadiation = 0f;
            float maxToxicity = 0f;
            float maxBrine = 0f;
            int headlessCount = math.min(_scheduledHeadlessMutationCount, _headlessEntities.MutationResults.IsCreated ? _headlessEntities.MutationResults.Length : 0);
            for (int i = 0; i < headlessCount; i++)
            {
                byte result = _headlessEntities.MutationResults[i];
                if (result == 0)
                    continue;

                headlessMutated++;
                flags |= result;
                maxRadiation = math.max(maxRadiation, _headlessEntities.MutationRadiation[i]);
                maxToxicity = math.max(maxToxicity, _headlessEntities.MutationToxicity[i]);
                maxBrine = math.max(maxBrine, _headlessEntities.MutationBrine[i]);
            }

            int macroCount = math.min(_scheduledMacroSwarmMutationCount, _macroSwarmMutationResults.IsCreated ? _macroSwarmMutationResults.Length : 0);
            for (int i = 0; i < macroCount; i++)
            {
                byte result = _macroSwarmMutationResults[i];
                if (result == 0)
                    continue;

                macroMutated++;
                flags |= result;
                maxRadiation = math.max(maxRadiation, _macroSwarmMutationRadiation[i]);
                maxToxicity = math.max(maxToxicity, _macroSwarmMutationToxicity[i]);
                maxBrine = math.max(maxBrine, _macroSwarmMutationBrine[i]);
            }

            _lastHeadlessMutationCount = headlessMutated;
            _lastMacroSwarmMutationCount = macroMutated;
            if (headlessMutated + macroMutated > 0)
            {
                RecordGenomeMutation((byte)(flags & 0xFFu), maxRadiation, maxToxicity, maxBrine, headlessMutated, macroMutated);
                PublishBatchFaunaMutatedSignal((byte)(flags & 0xFFu));
            }

            _scheduledHeadlessMutationCount = 0;
            _scheduledMacroSwarmMutationCount = 0;
        }

        private void ScheduleMacroSwarmTravel(JobHandle dependency = default)
        {
            if (_macroSwarmTravelScheduled ||
                _activeMacroSwarmCount <= 0 ||
                !_macroSwarms.IsCreated ||
                !_macroSwarmArrivals.IsCreated ||
                !_macroSwarmCounters.IsCreated)
            {
                return;
            }

            _macroSwarmCounters[0] = math.clamp(_activeMacroSwarmCount, 0, _macroSwarms.Length);
            _macroSwarmCounters[1] = 0;
            var job = new MacroSwarmTravelJob
            {
                Swarms = _macroSwarms,
                Arrivals = _macroSwarmArrivals,
                Counters = _macroSwarmCounters,
                DeltaSeconds = FrostTickIntervalSeconds
            };
            _macroSwarmTravelHandle = job.Schedule(dependency);
            _macroSwarmTravelScheduled = true;
        }

        private void CompleteScheduledMacroSwarmTravel(bool forceComplete)
        {
            if (!_macroSwarmTravelScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _macroSwarmTravelHandle, forceComplete))
                return;

            _macroSwarmTravelScheduled = false;
            _activeMacroSwarmCount = _macroSwarmCounters.IsCreated && _macroSwarmCounters.Length > 0
                ? math.clamp(_macroSwarmCounters[0], 0, _macroSwarms.Length)
                : 0;
            int arrivalCount = _macroSwarmCounters.IsCreated && _macroSwarmCounters.Length > 1 && _macroSwarmArrivals.IsCreated
                ? math.clamp(_macroSwarmCounters[1], 0, _macroSwarmArrivals.Length)
                : 0;
            _lastMacroSwarmArrivalCount = arrivalCount;
            for (int i = 0; i < arrivalCount; i++)
            {
                MacroSwarmArrival arrival = _macroSwarmArrivals[i];
                AddPreyBiomassToCell(arrival.TargetSectorAup, arrival.BiomassValue);
                _macroSwarmArrivals[i] = default;
            }

            PushMacroSwarmBlackBox(0);
        }

        private bool TryAppendMacroSwarm(int2 sourceCell, int2 targetCell, float biomassValue, float speed, uint hashId, ushort flags)
        {
            if (!_macroSwarms.IsCreated ||
                _macroSwarmTravelScheduled ||
                math.all(sourceCell == targetCell))
            {
                return false;
            }

            if (_activeMacroSwarmCount >= ResolveMacroSwarmActiveCap())
            {
                PushMacroSwarmBlackBox(MacroSwarmBlackBoxFlagCapacityOverflow);
                return false;
            }

            MacroSwarm swarm = CreateMacroSwarm(sourceCell, targetCell, biomassValue, speed, hashId, flags);
            if (swarm.HashId == 0u || swarm.BiomassValue <= 0.0001f)
                return false;

            _macroSwarms[_activeMacroSwarmCount++] = swarm;
            return true;
        }

        private static MacroSwarm CreateMacroSwarm(int2 sourceCell, int2 targetCell, float biomassValue, float speed, uint hashId, ushort flags)
        {
            return new MacroSwarm
            {
                HashId = hashId == 0u ? 1u : hashId,
                SectorAup = sourceCell,
                TargetSectorAup = targetCell,
                CurrentSectorAup = new float2(sourceCell.x, sourceCell.y),
                BiomassValue = math.saturate(math.select(0f, biomassValue, math.isfinite(biomassValue))),
                Speed = math.max(0.001f, math.select(0f, speed, math.isfinite(speed))),
                Flags = flags,
                Genome = FaunaGenome64.BuildGenome(hashId == 0u ? 1u : hashId, 1f, 1f + math.saturate(speed) * 0.2f)
            };
        }

        private void RemoveMacroSwarmSwapBack(int index)
        {
            if ((uint)index >= (uint)_activeMacroSwarmCount)
                return;

            _activeMacroSwarmCount--;
            _macroSwarms[index] = index < _activeMacroSwarmCount ? _macroSwarms[_activeMacroSwarmCount] : default;
            _macroSwarms[_activeMacroSwarmCount] = default;
        }

        private void AddPreyBiomassToCell(int2 macroCell, float amount01)
        {
            if (!math.isfinite(amount01) || amount01 <= 0f)
                return;

            int slotIndex = ResolveOrCreateBiomassCellSlot(macroCell, seedWithBaseline: true);
            if (slotIndex < 0)
                return;

            float capacity = math.max(0.0001f, _biomassCarryingCapacity[slotIndex]);
            float addition = math.saturate(amount01) * capacity;
            float prey = math.min(capacity, math.max(0f, _preyBiomassFront[slotIndex]) + addition);
            _preyBiomassFront[slotIndex] = prey;
            _preyBiomassBack[slotIndex] = prey;
            _biomassSumScratch[slotIndex] = prey + _predatorBiomassFront[slotIndex];
        }

        private int2 ResolveLowestNeighborMacroCell(int2 centerCell)
        {
            int2 bestCell = centerCell;
            float bestPrey01 = float.MaxValue;
            ResolveLowestNeighborCandidate(centerCell + new int2(1, 0), ref bestCell, ref bestPrey01);
            ResolveLowestNeighborCandidate(centerCell + new int2(-1, 0), ref bestCell, ref bestPrey01);
            ResolveLowestNeighborCandidate(centerCell + new int2(0, 1), ref bestCell, ref bestPrey01);
            ResolveLowestNeighborCandidate(centerCell + new int2(0, -1), ref bestCell, ref bestPrey01);
            return bestCell;
        }

        private void ResolveLowestNeighborCandidate(int2 cell, ref int2 bestCell, ref float bestPrey01)
        {
            int slotIndex = ResolveOrCreateBiomassCellSlot(cell, seedWithBaseline: false);
            if (slotIndex < 0)
                return;

            float capacity = math.max(0.0001f, _biomassCarryingCapacity[slotIndex]);
            float prey01 = math.saturate(_preyBiomassFront[slotIndex] * math.rcp(capacity));
            if (prey01 < bestPrey01)
            {
                bestPrey01 = prey01;
                bestCell = cell;
            }
        }

        private bool HasActiveMacroSwarmRoute(int2 sourceCell, int2 targetCell)
        {
            for (int i = 0; i < _activeMacroSwarmCount; i++)
            {
                MacroSwarm swarm = _macroSwarms[i];
                if (math.all(swarm.SectorAup == sourceCell) && math.all(swarm.TargetSectorAup == targetCell))
                    return true;
            }

            return false;
        }

        private int ResolveMacroSwarmActiveCap()
        {
            return math.min(_macroSwarmActiveCap, _macroSwarms.IsCreated ? _macroSwarms.Length : 0);
        }

        private float ResolveMacroSwarmSpeedCellsPerSecond()
        {
            return _macroSwarmSpeedCellsPerSecond;
        }

        private void RefreshMacroSwarmScalabilityCache()
        {
            byte tier = GlobalRegistry.ScalabilityTierProfileByte;
            _macroSwarmQualityTierProfileByte = tier;
            _macroSwarmActiveCap = tier switch
            {
                0 => MacroSwarmLowTierCap,
                1 => MacroSwarmMiddleTierCap,
                2 => MacroSwarmHighTierCap,
                _ => MacroSwarmCapacity
            };
            _macroSwarmSpeedCellsPerSecond = tier == 0
                ? MacroSwarmDefaultSpeedCellsPerSecond * 0.5f
                : MacroSwarmDefaultSpeedCellsPerSecond;
        }

        private static int2 ResolveDeterministicMigrationDirection(int2 sourceCell)
        {
            uint hash = MixSectorBits(sourceCell.x, sourceCell.y);
            switch (hash & 3u)
            {
                case 0u: return new int2(1, 0);
                case 1u: return new int2(-1, 0);
                case 2u: return new int2(0, 1);
                default: return new int2(0, -1);
            }
        }

        private static uint HashMacroSwarm(int2 sourceCell, int2 targetCell, long salt)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)sourceCell.x) * 16777619u;
                hash = (hash ^ (uint)sourceCell.y) * 16777619u;
                hash = (hash ^ (uint)targetCell.x) * 16777619u;
                hash = (hash ^ (uint)targetCell.y) * 16777619u;
                hash = (hash ^ (uint)salt) * 16777619u;
                hash = (hash ^ (uint)(salt >> 32)) * 16777619u;
                return hash == 0u ? 1u : hash;
            }
        }

        private void PublishHydratedMacroSwarmBurst(int2 macroCell, ushort radiusMetersQ, int spawnedBoids)
        {
            if (!_macroHydrationScratch.IsCreated || _macroHydrationScratchCount <= 0)
                return;

            AbsoluteUniversePosition centerAup = ResolveBiomassMacroCellCenterAup(macroCell);
            GlobalSignals.Publish(new SwarmDispersedSignal
            {
                PositionAup = centerAup,
                RadiusMeters = math.max(BiomassMacroCellSizeMeters, radiusMetersQ),
                Intensity01 = math.saturate(_macroHydrationScratchCount * math.rcp((float)MacroSwarmSignalScratchCapacity)),
                SourceId = MacroSwarmSaveHeaderMarker,
                EstimatedBoidCount = (ushort)math.clamp(
                    spawnedBoids > 0 ? spawnedBoids : _macroHydrationScratchCount * MacroSwarmVisualBoidsPerBiomassUnit,
                    0,
                    ushort.MaxValue),
                Flags = 1,
                QualityTier = _macroSwarmQualityTierProfileByte
            });
        }

        private void PushMacroSwarmBlackBox(int flags)
        {
            if (!_macroSwarmBlackBox.IsCreated || _macroSwarmBlackBox.Length <= 0)
                return;

            float biomassSum = 0f;
            int invalidFlag = flags;
            for (int i = 0; i < _activeMacroSwarmCount; i++)
            {
                MacroSwarm swarm = _macroSwarms[i];
                if (!math.isfinite(swarm.BiomassValue) || !math.isfinite(swarm.Speed) || !math.all(math.isfinite(swarm.CurrentSectorAup)))
                    invalidFlag |= MacroSwarmBlackBoxFlagInvalid;
                else
                    biomassSum += swarm.BiomassValue;
            }

            uint stateHash = MixMacroSwarmStateHash(biomassSum, _activeMacroSwarmCount);
            _lastMacroSwarmBiomassSum = biomassSum;
            _lastMacroSwarmStateHash = stateHash;

            int index = _macroSwarmBlackBoxCursor % _macroSwarmBlackBox.Length;
            _macroSwarmBlackBox[index] = new MacroSwarmTelemetryEntry
            {
                FrameIndex = unchecked((uint)Time.frameCount),
                StateHash = stateHash,
                ActiveMacroSwarms = _activeMacroSwarmCount,
                ArrivalCount = _lastMacroSwarmArrivalCount,
                BiomassSum = biomassSum,
                Flags = invalidFlag,
                Reserved0 = unchecked((uint)math.max(0, _lastMacroSwarmsHydrated)),
                Reserved1 = unchecked((uint)math.max(0, _lastMacroHydratedBoidEstimate))
            };
            _macroSwarmBlackBoxCursor++;
            if (invalidFlag != 0)
                DumpMacroSwarmBlackBox();
        }

        private void PushMacroSwarmHeartbeatBlackBox()
        {
            if (!_macroSwarmBlackBox.IsCreated || _macroSwarmBlackBox.Length <= 0)
                return;

            uint stateHash = _lastMacroSwarmStateHash != 0u
                ? _lastMacroSwarmStateHash
                : MixMacroSwarmStateHash(_lastMacroSwarmBiomassSum, _activeMacroSwarmCount);
            int index = _macroSwarmBlackBoxCursor % _macroSwarmBlackBox.Length;
            _macroSwarmBlackBox[index] = new MacroSwarmTelemetryEntry
            {
                FrameIndex = unchecked((uint)Time.frameCount),
                StateHash = stateHash,
                ActiveMacroSwarms = _activeMacroSwarmCount,
                ArrivalCount = _lastMacroSwarmArrivalCount,
                BiomassSum = _lastMacroSwarmBiomassSum,
                Flags = MacroSwarmBlackBoxFlagHeartbeat,
                Reserved0 = unchecked((uint)math.max(0, _lastMacroSwarmsHydrated)),
                Reserved1 = unchecked((uint)math.max(0, _lastMacroHydratedBoidEstimate))
            };
            _macroSwarmBlackBoxCursor++;
        }

        private unsafe void DumpMacroSwarmBlackBox()
        {
            if (!_macroSwarmBlackBox.IsCreated || _macroSwarmBlackBox.Length <= 0)
                return;

            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string dumpPath = Path.Combine(projectRoot, MacroSwarmTelemetryDumpRelativePath);
                string directory = Path.GetDirectoryName(dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    int entrySize = UnsafeUtility.SizeOf<MacroSwarmTelemetryEntry>();
                    int entryCapacity = _macroSwarmBlackBox.Length;
                    int entryCount = math.min(math.max(0, _macroSwarmBlackBoxCursor), entryCapacity);
                    int oldestIndex = entryCount == entryCapacity ? _macroSwarmBlackBoxCursor % entryCapacity : 0;
                    const int headerBytes = sizeof(ulong) + (sizeof(int) * 4);
                    byte* headerPtr = stackalloc byte[headerBytes];
                    UnsafeUtility.WriteArrayElement<ulong>(headerPtr, 0, MacroSwarmTelemetryDumpMagic);
                    UnsafeUtility.WriteArrayElement<int>(headerPtr + sizeof(ulong), 0, entryCount);
                    UnsafeUtility.WriteArrayElement<int>(headerPtr + sizeof(ulong) + sizeof(int), 0, entrySize);
                    UnsafeUtility.WriteArrayElement<int>(headerPtr + sizeof(ulong) + (sizeof(int) * 2), 0, oldestIndex);
                    UnsafeUtility.WriteArrayElement<int>(headerPtr + sizeof(ulong) + (sizeof(int) * 3), 0, entryCapacity);
                    stream.Write(new ReadOnlySpan<byte>(headerPtr, headerBytes));
                    if (entryCount <= 0)
                        return;

                    byte* dataPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr<MacroSwarmTelemetryEntry>(_macroSwarmBlackBox.Resolve());
                    int firstCount = math.min(entryCount, entryCapacity - oldestIndex);
                    stream.Write(new ReadOnlySpan<byte>(dataPtr + oldestIndex * entrySize, firstCount * entrySize));
                    int secondCount = entryCount - firstCount;
                    if (secondCount > 0)
                        stream.Write(new ReadOnlySpan<byte>(dataPtr, secondCount * entrySize));
                }
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)MacroSwarmSaveHeaderMarker));
            }
        }

        private void PushFaunaMutationBlackBox(int flags)
        {
            if (!_faunaMutationBlackBox.IsCreated || _faunaMutationBlackBox.Length <= 0)
                return;

            int invalidFlag = flags;
            if (!math.isfinite(_lastMutationRadiationRads) ||
                !math.isfinite(_lastMutationToxicity01) ||
                !math.isfinite(_lastMutationBrineDepth01))
            {
                invalidFlag |= 1;
            }

            int index = _faunaMutationBlackBoxCursor % _faunaMutationBlackBox.Length;
            _faunaMutationBlackBox[index] = new FaunaMutationTelemetryEntry
            {
                FrameIndex = unchecked((uint)Time.frameCount),
                StateHash = MixFaunaMutationStateHash(_totalMutatedEntities, _lastMutationFlags, _lastMutationRadiationRads, _lastMutationToxicity01),
                TotalMutatedEntities = _totalMutatedEntities,
                HeadlessMutatedCount = _lastHeadlessMutationCount,
                MacroSwarmMutatedCount = _lastMacroSwarmMutationCount,
                LastMutationFlags = _lastMutationFlags,
                LastRadiationRads = _lastMutationRadiationRads,
                LastToxicity01 = _lastMutationToxicity01,
                LastBrineDepth01 = _lastMutationBrineDepth01
            };
            _faunaMutationBlackBoxCursor++;
            if (invalidFlag != 0)
                DumpFaunaMutationBlackBox();
        }

        private unsafe void DumpFaunaMutationBlackBox()
        {
            if (!_faunaMutationBlackBox.IsCreated || _faunaMutationBlackBox.Length <= 0)
                return;

            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string dumpPath = Path.Combine(projectRoot, FaunaMutationTelemetryDumpRelativePath);
                string directory = Path.GetDirectoryName(dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    int entrySize = UnsafeUtility.SizeOf<FaunaMutationTelemetryEntry>();
                    int entryCapacity = _faunaMutationBlackBox.Length;
                    int entryCount = math.min(math.max(0, _faunaMutationBlackBoxCursor), entryCapacity);
                    int oldestIndex = entryCount == entryCapacity ? _faunaMutationBlackBoxCursor % entryCapacity : 0;
                    const int headerBytes = sizeof(ulong) + (sizeof(int) * 4);
                    byte* headerPtr = stackalloc byte[headerBytes];
                    UnsafeUtility.WriteArrayElement<ulong>(headerPtr, 0, FaunaMutationTelemetryDumpMagic);
                    UnsafeUtility.WriteArrayElement<int>(headerPtr + sizeof(ulong), 0, entryCount);
                    UnsafeUtility.WriteArrayElement<int>(headerPtr + sizeof(ulong) + sizeof(int), 0, entrySize);
                    UnsafeUtility.WriteArrayElement<int>(headerPtr + sizeof(ulong) + (sizeof(int) * 2), 0, oldestIndex);
                    UnsafeUtility.WriteArrayElement<int>(headerPtr + sizeof(ulong) + (sizeof(int) * 3), 0, entryCapacity);
                    stream.Write(new ReadOnlySpan<byte>(headerPtr, headerBytes));
                    if (entryCount <= 0)
                        return;

                    byte* dataPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr<FaunaMutationTelemetryEntry>(_faunaMutationBlackBox.Resolve());
                    int firstCount = math.min(entryCount, entryCapacity - oldestIndex);
                    stream.Write(new ReadOnlySpan<byte>(dataPtr + oldestIndex * entrySize, firstCount * entrySize));
                    int secondCount = entryCount - firstCount;
                    if (secondCount > 0)
                        stream.Write(new ReadOnlySpan<byte>(dataPtr, secondCount * entrySize));
                }
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)_FaunaMutationTelemetryHash));
            }
        }

        private static uint MixMacroSwarmStateHash(float biomassSum, int activeSwarmCount)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)math.asint(biomassSum)) * 16777619u;
                hash = (hash ^ (uint)activeSwarmCount) * 16777619u;
                return hash;
            }
        }

        private static uint MixFaunaMutationStateHash(int totalMutatedEntities, uint flags, float radiationRads, float toxicity01)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)totalMutatedEntities) * 16777619u;
                hash = (hash ^ flags) * 16777619u;
                hash = (hash ^ (uint)math.asint(radiationRads)) * 16777619u;
                hash = (hash ^ (uint)math.asint(toxicity01)) * 16777619u;
                return hash == 0u ? 1u : hash;
            }
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
                GraphicsBufferUploadUtility.UploadNativeArray<float4>(_floraPredatorAupBuffer, _floraPredatorAupUpload.Resolve(), uploadCount);

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
            if (TryFindIndexEntry(_sectorIndexEntries, packedKey, out int existingSlot))
                return existingSlot;

            if (_activeSectorCount >= _sectorFrontStates.Length)
                return -1;

            int slotIndex = _activeSectorCount;
            _activeSectorCount++;
            if (!TryUpsertIndexEntry(_sectorIndexEntries, packedKey, slotIndex))
            {
                _activeSectorCount--;
                return -1;
            }

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
            if (TryFindIndexEntry(_biomassIndexEntries, packedKey, out int existingSlot))
                return existingSlot;

            if (_activeBiomassCellCount >= _preyBiomassFront.Length)
                return -1;

            int slotIndex = _activeBiomassCellCount;
            _activeBiomassCellCount++;
            if (!TryUpsertIndexEntry(_biomassIndexEntries, packedKey, slotIndex))
            {
                _activeBiomassCellCount--;
                return -1;
            }
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
            _biomassCellFlags[slotIndex] = predator > 0.0001f ? BiomassCellFlagPredatorSeen : (byte)0;
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
            float gradientCapacityBias = math.saturate(_biomeGradientBlend01) * BiomeGradientCapacityGain;
            return math.clamp(food01 + biomeCapacityBias + gradientCapacityBias, 0.1f, 1f);
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
                case BiomassImpactKindCampaignToxicity:
                    prey = math.max(0f, prey - impact);
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
                if (!IsFishingBiomassSource(signal.SourceKind) || !IsFishItemHash(signal.ItemHash))
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
                bool predatorPresent = predator > 0.0001f;
                if (predatorPresent)
                    flags |= BiomassCellFlagPredatorSeen;

                bool predatorCleared = !predatorPresent;
                bool predatorWasSeen = (flags & BiomassCellFlagPredatorSeen) != 0;
                if (predatorCleared && predatorWasSeen && (flags & BiomassCellFlagSectorClearedPublished) == 0)
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
            if (!_biomassBlackBox.IsCreated || _biomassBlackBox.Length <= 0)
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
                    int entrySize = UnsafeUtility.SizeOf<BiomassTelemetryEntry>();
                    int entryCapacity = _biomassBlackBox.Length;
                    int entryCount = math.min(math.max(0, _biomassBlackBoxCursor), entryCapacity);
                    int oldestIndex = entryCount == entryCapacity ? _biomassBlackBoxCursor % entryCapacity : 0;
                    const int headerBytes = sizeof(ulong) + (sizeof(int) * 4);
                    byte* headerPtr = stackalloc byte[headerBytes];
                    UnsafeUtility.WriteArrayElement<ulong>(headerPtr, 0, BiomassTelemetryDumpMagic);
                    UnsafeUtility.WriteArrayElement<int>(headerPtr + sizeof(ulong), 0, entryCount);
                    UnsafeUtility.WriteArrayElement<int>(headerPtr + sizeof(ulong) + sizeof(int), 0, entrySize);
                    UnsafeUtility.WriteArrayElement<int>(headerPtr + sizeof(ulong) + (sizeof(int) * 2), 0, oldestIndex);
                    UnsafeUtility.WriteArrayElement<int>(headerPtr + sizeof(ulong) + (sizeof(int) * 3), 0, entryCapacity);
                    stream.Write(new ReadOnlySpan<byte>(headerPtr, headerBytes));
                    if (entryCount <= 0)
                        return;

                    byte* dataPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr<BiomassTelemetryEntry>(_biomassBlackBox.Resolve());
                    int firstCount = math.min(entryCount, entryCapacity - oldestIndex);
                    stream.Write(new ReadOnlySpan<byte>(dataPtr + oldestIndex * entrySize, firstCount * entrySize));
                    int secondCount = entryCount - firstCount;
                    if (secondCount > 0)
                        stream.Write(new ReadOnlySpan<byte>(dataPtr, secondCount * entrySize));
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
                   itemHash == _ItemCookedFishNameHash ||
                   itemHash == _ItemCuredFishStableHash ||
                   itemHash == _ItemRawFishStableHash ||
                   itemHash == _ItemCookedFishStableHash ||
                   itemHash == _ItemFishStableHash ||
                   itemHash == _ItemCuredFishDisplayHash ||
                   itemHash == _ItemRawFishDisplayHash ||
                   itemHash == _ItemCookedFishDisplayHash;
        }

        private static bool IsFishingBiomassSource(byte sourceKind)
        {
            return sourceKind == ItemAcquiredSourceUnknown ||
                   sourceKind == ItemAcquiredSourceResourceNode;
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

            _saveSnapshotBiomassRunCount = 0;
            int emittedCount = 0;
            bool hasLastSortedCoord = false;
            int2 lastSortedCoord = int2.zero;
            int runLength = 0;
            int2 runStart = int2.zero;
            int2 previousCoord = int2.zero;
            sbyte runPrey = 0;
            sbyte runPredator = 0;
            sbyte runCapacity = 0;
            while (emittedCount < _activeBiomassCellCount)
            {
                int bestIndex = -1;
                int2 bestCoord = int2.zero;
                for (int i = 0; i < _activeBiomassCellCount; i++)
                {
                    int2 candidateCoord = _biomassMacroCellCoords[i];
                    if (!IsBiomassCoordAfter(candidateCoord, lastSortedCoord, hasLastSortedCoord))
                        continue;

                    if (bestIndex < 0 || IsBiomassCoordBefore(candidateCoord, bestCoord))
                    {
                        bestIndex = i;
                        bestCoord = candidateCoord;
                    }
                }

                if (bestIndex < 0)
                    break;

                int2 coord = bestCoord;
                sbyte preyQ = QuantizeBiomass01(_preyBiomassFront[bestIndex]);
                sbyte predatorQ = QuantizeBiomass01(_predatorBiomassFront[bestIndex]);
                sbyte capacityQ = QuantizeBiomass01(_biomassCarryingCapacity[bestIndex]);
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
                lastSortedCoord = coord;
                hasLastSortedCoord = true;
                emittedCount++;
            }

            FlushBiomassSaveRun(runStart, runPrey, runPredator, runCapacity, runLength);
        }

        private void CaptureMacroSwarmSaveRecords()
        {
            if (!_saveSnapshotSectors.IsCreated || !_macroSwarms.IsCreated)
                return;

            int count = math.min(_activeMacroSwarmCount, _macroSwarms.Length);
            for (int i = 0; i < count && HasSaveSnapshotSectorCapacity(2); i++)
            {
                MacroSwarm swarm = _macroSwarms[i];
                if (swarm.HashId == 0u || swarm.BiomassValue <= 0.0001f)
                    continue;

                TryAppendSaveSnapshotSector(PackMacroSwarmHeaderRecord(in swarm));
                TryAppendSaveSnapshotSector(PackMacroSwarmDetailRecord(in swarm));
            }
        }

        private void CaptureFaunaGenomeSaveRecords()
        {
            if (!_saveSnapshotSectors.IsCreated)
                return;

            if (_headlessEntities.FaunaGenomes.IsCreated && _headlessEntities.SectorCoord.IsCreated)
            {
                int count = math.min(_activeSectorCount, _headlessEntities.FaunaGenomes.Length);
                for (int i = 0; i < count && HasSaveSnapshotSectorCapacity(2); i++)
                {
                    ulong genome = _headlessEntities.FaunaGenomes[i];
                    if (!FaunaGenome64.HasContaminatedYield(genome))
                        continue;

                    uint stableHash = _headlessEntities.MutationStableHashes.IsCreated
                        ? _headlessEntities.MutationStableHashes[i]
                        : MixSectorBits(_headlessEntities.SectorCoord[i].x, _headlessEntities.SectorCoord[i].y);
                    TryAppendSaveSnapshotSector(PackFaunaGenomeHeaderRecord(_headlessEntities.SectorCoord[i], stableHash, false));
                    TryAppendSaveSnapshotSector(PackFaunaGenomeDetailRecord(genome, false));
                }
            }

            if (!_macroSwarms.IsCreated)
                return;

            int macroCount = math.min(_activeMacroSwarmCount, _macroSwarms.Length);
            for (int i = 0; i < macroCount && HasSaveSnapshotSectorCapacity(2); i++)
            {
                MacroSwarm swarm = _macroSwarms[i];
                if (!FaunaGenome64.HasContaminatedYield(swarm.Genome))
                    continue;

                TryAppendSaveSnapshotSector(PackFaunaGenomeHeaderRecord(swarm.SectorAup, swarm.HashId, true));
                TryAppendSaveSnapshotSector(PackFaunaGenomeDetailRecord(swarm.Genome, true));
            }
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
                _saveSnapshotBiomassRunCount >= _saveSnapshotBiomassRuns.Length)
            {
                return;
            }

            _saveSnapshotBiomassRuns[_saveSnapshotBiomassRunCount++] = new EcosystemBiomassSaveRun
            {
                StartMacroCell = start,
                PreyBiomassQ = preyQ,
                PredatorBiomassQ = predatorQ,
                CarryingCapacityQ = capacityQ,
                RunLength = (byte)math.clamp(runLength, 1, byte.MaxValue)
            };
        }

        private bool HasSaveSnapshotSectorCapacity(int recordCount)
        {
            return _saveSnapshotSectors.IsCreated &&
                   recordCount >= 0 &&
                   _saveSnapshotSectorCount <= _saveSnapshotSectors.Length - recordCount;
        }

        private bool TryAppendSaveSnapshotSector(EcosystemSectorSaveRecord record)
        {
            if (!HasSaveSnapshotSectorCapacity(1))
                return false;

            _saveSnapshotSectors[_saveSnapshotSectorCount++] = record;
            return true;
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
                _biomassCellFlags[slotIndex] = predator > 0.0001f ? BiomassCellFlagPredatorSeen : (byte)0;
            }
        }

        private void ClearBiomassRuntimeState()
        {
            ClearIndexEntries(_biomassIndexEntries);

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

        private void ClearMacroSwarmRuntimeState()
        {
            if (_macroSwarmTravelScheduled)
            {
                _macroSwarmTravelHandle.Complete();
                _macroSwarmTravelScheduled = false;
                _macroSwarmTravelHandle = default;
            }

            if (_macroSwarms.IsCreated)
            {
                for (int i = 0; i < _macroSwarms.Length; i++)
                    _macroSwarms[i] = default;
            }

            if (_macroSwarmArrivals.IsCreated)
            {
                for (int i = 0; i < _macroSwarmArrivals.Length; i++)
                    _macroSwarmArrivals[i] = default;
            }

            if (_macroSwarmCounters.IsCreated)
            {
                for (int i = 0; i < _macroSwarmCounters.Length; i++)
                    _macroSwarmCounters[i] = 0;
            }

            if (_macroSwarmMutationRadiation.IsCreated)
            {
                for (int i = 0; i < _macroSwarmMutationRadiation.Length; i++)
                    _macroSwarmMutationRadiation[i] = 0f;
            }

            if (_macroSwarmMutationToxicity.IsCreated)
            {
                for (int i = 0; i < _macroSwarmMutationToxicity.Length; i++)
                    _macroSwarmMutationToxicity[i] = 0f;
            }

            if (_macroSwarmMutationBrine.IsCreated)
            {
                for (int i = 0; i < _macroSwarmMutationBrine.Length; i++)
                    _macroSwarmMutationBrine[i] = 0f;
            }

            if (_macroSwarmMutationResults.IsCreated)
            {
                for (int i = 0; i < _macroSwarmMutationResults.Length; i++)
                    _macroSwarmMutationResults[i] = 0;
            }

            if (_macroHydrationScratch.IsCreated)
                _macroHydrationScratchCount = 0;
            if (_macroDehydrationScratch.IsCreated)
                _macroDehydrationScratchCount = 0;

            _activeMacroSwarmCount = 0;
            _lastMacroSwarmArrivalCount = 0;
            _lastMacroSwarmsHydrated = 0;
            _lastMacroHydratedBoidEstimate = 0;
            _lastSectorResidencySignalDrainFrame = -1;
        }

        private void ClearHeadlessRuntimeState()
        {
            int capacity = _sectorFrontStates.IsCreated ? _sectorFrontStates.Length : 0;
            NativeArray<float3> positions = _headlessEntities.Positions;
            NativeArray<byte> speciesIds = _headlessEntities.SpeciesID;
            NativeArray<byte> hunger = _headlessEntities.Hunger;
            NativeArray<int2> sectorCoords = _headlessEntities.SectorCoord;
            NativeArray<int> sectorIds = _headlessEntities.SectorID;
            NativeArray<ulong> faunaGenomes = _headlessEntities.FaunaGenomes;
            NativeArray<float> mutationRadiation = _headlessEntities.MutationRadiation;
            NativeArray<float> mutationToxicity = _headlessEntities.MutationToxicity;
            NativeArray<float> mutationBrine = _headlessEntities.MutationBrine;
            NativeArray<uint> mutationStableHashes = _headlessEntities.MutationStableHashes;
            NativeArray<byte> mutationResults = _headlessEntities.MutationResults;
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
                if (positions.IsCreated)
                    positions[i] = float3.zero;
                if (speciesIds.IsCreated)
                    speciesIds[i] = 0;
                if (hunger.IsCreated)
                    hunger[i] = 0;
                if (sectorCoords.IsCreated)
                    sectorCoords[i] = int2.zero;
                if (sectorIds.IsCreated)
                    sectorIds[i] = 0;
                if (faunaGenomes.IsCreated)
                    faunaGenomes[i] = 0UL;
                if (mutationRadiation.IsCreated)
                    mutationRadiation[i] = 0f;
                if (mutationToxicity.IsCreated)
                    mutationToxicity[i] = 0f;
                if (mutationBrine.IsCreated)
                    mutationBrine[i] = 0f;
                if (mutationStableHashes.IsCreated)
                    mutationStableHashes[i] = 0u;
                if (mutationResults.IsCreated)
                    mutationResults[i] = 0;
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
            NativeArray<float3> positions = _headlessEntities.Positions;
            NativeArray<byte> speciesIds = _headlessEntities.SpeciesID;
            NativeArray<byte> hunger = _headlessEntities.Hunger;
            NativeArray<int2> sectorCoords = _headlessEntities.SectorCoord;
            NativeArray<int> sectorIds = _headlessEntities.SectorID;
            NativeArray<uint> mutationStableHashes = _headlessEntities.MutationStableHashes;
            NativeArray<ulong> faunaGenomes = _headlessEntities.FaunaGenomes;
            if (positions.IsCreated)
                positions[slotIndex] = ResolveSectorCenterPosition(state.SectorCoord);
            if (speciesIds.IsCreated)
                speciesIds[slotIndex] = (byte)math.select(1, 2, state.PredatorPopulationRounded > state.PreyPopulationRounded);
            if (hunger.IsCreated)
            {
                float preyPerPredator = math.select(
                    starvationComfortPreyPerPredator,
                    state.PreyPopulationRounded * math.rcp(math.max(1f, state.PredatorPopulationRounded)),
                    state.PredatorPopulationRounded > 0);
                float invStarvationComfort = math.rcp(math.max(1f, starvationComfortPreyPerPredator));
                hunger[slotIndex] = PackUnitByte(1f - preyPerPredator * invStarvationComfort);
            }
            if (sectorCoords.IsCreated)
                sectorCoords[slotIndex] = state.SectorCoord;
            if (sectorIds.IsCreated)
                sectorIds[slotIndex] = ResolveSectorId(state.SectorCoord);
            if (mutationStableHashes.IsCreated)
                mutationStableHashes[slotIndex] = MixSectorBits(state.SectorCoord.x, state.SectorCoord.y);
            if (faunaGenomes.IsCreated && faunaGenomes[slotIndex] == 0UL)
            {
                uint stableHash = MixSectorBits(state.SectorCoord.x, state.SectorCoord.y);
                faunaGenomes[slotIndex] = FaunaGenome64.BuildGenome(
                    stableHash,
                    state.SpeedMultiplier,
                    state.SpeedMultiplier);
            }
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

        private static void ClearIndexEntries(VaultNativeArray<EcosystemIndexEntry> indexEntries)
        {
            if (!indexEntries.IsCreated)
                return;

            NativeArray<EcosystemIndexEntry> entries = indexEntries.Resolve();
            if (!entries.IsCreated)
                return;

            for (int i = 0; i < entries.Length; i++)
                entries[i] = default;
        }

        private static bool TryFindIndexEntry(
            VaultNativeArray<EcosystemIndexEntry> indexEntries,
            long key,
            out int slot)
        {
            return TryFindIndexEntry(indexEntries.Resolve(), key, out slot);
        }

        private static bool TryUpsertIndexEntry(
            VaultNativeArray<EcosystemIndexEntry> indexEntries,
            long key,
            int slot)
        {
            NativeArray<EcosystemIndexEntry> entries = indexEntries.Resolve();
            if (!entries.IsCreated || entries.Length <= 0 || slot < 0)
                return false;

            int startIndex = ResolveIndexBucket(key, entries.Length);
            for (int probe = 0; probe < entries.Length; probe++)
            {
                int index = ResolveIndexProbe(startIndex, probe, entries.Length);
                EcosystemIndexEntry entry = entries[index];
                if (entry.Occupied != 0 && entry.Key != key)
                    continue;

                entries[index] = new EcosystemIndexEntry
                {
                    Key = key,
                    Slot = slot,
                    Occupied = 1
                };
                return true;
            }

            return false;
        }

        private static int ResolveIndexBucket(long key, int capacity)
        {
            if (capacity <= 0)
                return 0;

            uint hash = MixIndexKey(key);
            return IsPowerOfTwo(capacity)
                ? (int)(hash & (uint)(capacity - 1))
                : (int)(hash % (uint)capacity);
        }

        private static int ResolveIndexProbe(int startIndex, int probe, int capacity)
        {
            int index = startIndex + probe;
            return IsPowerOfTwo(capacity)
                ? index & (capacity - 1)
                : index % capacity;
        }

        private static uint MixIndexKey(long key)
        {
            unchecked
            {
                ulong value = (ulong)key;
                value ^= value >> 33;
                value *= 0xff51afd7ed558ccdUL;
                value ^= value >> 33;
                value *= 0xc4ceb9fe1a85ec53UL;
                value ^= value >> 33;
                return (uint)value ^ (uint)(value >> 32);
            }
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
            uint runLength = (uint)math.clamp((int)run.RunLength, 1, byte.MaxValue);
            uint preyQ = (uint)math.clamp((int)run.PreyBiomassQ, 0, 100);
            uint predatorQ = (uint)math.clamp((int)run.PredatorBiomassQ, 0, 100);
            uint capacityQ = (uint)math.clamp((int)run.CarryingCapacityQ, 0, 100);
            return new EcosystemSectorSaveRecord
            {
                SectorCoord = run.StartMacroCell,
                PackedPopulations = BiomassSaveRecordMarker |
                                    (runLength & BiomassSaveRunLengthMask) |
                                    (preyQ << BiomassSavePreyShift) |
                                    (predatorQ << BiomassSavePredatorShift) |
                                    (capacityQ << BiomassSaveCapacityShift),
                PackedAdaptation = BiomassSaveAdaptationMarker
            };
        }

        private static EcosystemSectorSaveRecord PackMacroSwarmHeaderRecord(in MacroSwarm swarm)
        {
            return new EcosystemSectorSaveRecord
            {
                SectorCoord = swarm.SectorAup,
                PackedPopulations = MacroSwarmSaveHeaderMarker,
                PackedAdaptation = swarm.HashId
            };
        }

        private static EcosystemSectorSaveRecord PackMacroSwarmDetailRecord(in MacroSwarm swarm)
        {
            uint biomassQ = (uint)math.clamp(RoundPositiveToInt(math.saturate(swarm.BiomassValue) * ushort.MaxValue), 0, ushort.MaxValue);
            uint speedQ = (uint)math.clamp(RoundPositiveToInt(math.max(0f, swarm.Speed) * 1000f), 0, ushort.MaxValue);
            return new EcosystemSectorSaveRecord
            {
                SectorCoord = swarm.TargetSectorAup,
                PackedPopulations = biomassQ | (speedQ << 16),
                PackedAdaptation = MacroSwarmSaveDetailMarker
            };
        }

        private static EcosystemSectorSaveRecord PackFaunaGenomeHeaderRecord(int2 keyCoord, uint stableHash, bool macroSwarm)
        {
            return new EcosystemSectorSaveRecord
            {
                SectorCoord = keyCoord,
                PackedPopulations = FaunaGenomeSaveHeaderMarker | (macroSwarm ? 1u : 0u),
                PackedAdaptation = stableHash != 0u ? stableHash : 1u
            };
        }

        private static EcosystemSectorSaveRecord PackFaunaGenomeDetailRecord(ulong genome, bool macroSwarm)
        {
            return new EcosystemSectorSaveRecord
            {
                SectorCoord = new int2(unchecked((int)(uint)genome), unchecked((int)(uint)(genome >> 32))),
                PackedPopulations = macroSwarm ? 1u : 0u,
                PackedAdaptation = FaunaGenomeSaveDetailMarker
            };
        }

        private bool RestoreMacroSwarmSaveRecords(in EcosystemSectorSaveRecord header, in EcosystemSectorSaveRecord detail)
        {
            if (!IsMacroSwarmSaveHeader(in header) || !IsMacroSwarmSaveDetail(in detail))
                return false;

            float biomass = (detail.PackedPopulations & 0x0000FFFFu) * math.rcp((float)ushort.MaxValue);
            float speed = ((detail.PackedPopulations >> 16) & 0x0000FFFFu) * 0.001f;
            return TryAppendMacroSwarm(
                header.SectorCoord,
                detail.SectorCoord,
                biomass,
                math.max(0.001f, speed),
                header.PackedAdaptation,
                0);
        }

        private bool RestoreFaunaGenomeSaveRecords(in EcosystemSectorSaveRecord header, in EcosystemSectorSaveRecord detail)
        {
            if (!IsFaunaGenomeSaveHeader(in header) || !IsFaunaGenomeSaveDetail(in detail))
                return false;

            ulong genome = (uint)detail.SectorCoord.x | ((ulong)(uint)detail.SectorCoord.y << 32);
            if (genome == 0UL)
                return false;

            bool macroSwarm = (header.PackedPopulations & 1u) != 0u || detail.PackedPopulations != 0u;
            if (macroSwarm)
            {
                for (int i = 0; i < _activeMacroSwarmCount; i++)
                {
                    MacroSwarm swarm = _macroSwarms[i];
                    if (swarm.HashId != header.PackedAdaptation)
                        continue;

                    swarm.Genome = genome;
                    swarm.Flags |= 1;
                    _macroSwarms[i] = swarm;
                    return true;
                }

                return false;
            }

            int slotIndex = ResolveOrCreateSectorSlot(header.SectorCoord, seedWithBaseline: true);
            NativeArray<ulong> faunaGenomes = _headlessEntities.FaunaGenomes;
            if (slotIndex < 0 || !faunaGenomes.IsCreated)
                return false;

            faunaGenomes[slotIndex] = genome;
            NativeArray<uint> mutationStableHashes = _headlessEntities.MutationStableHashes;
            if (mutationStableHashes.IsCreated)
                mutationStableHashes[slotIndex] = header.PackedAdaptation != 0u ? header.PackedAdaptation : MixSectorBits(header.SectorCoord.x, header.SectorCoord.y);
            return true;
        }

        private static bool IsBiomassSaveRecord(in EcosystemSectorSaveRecord saveRecord)
        {
            return (saveRecord.PackedPopulations & BiomassSaveRecordMarker) != 0u &&
                   saveRecord.PackedAdaptation == BiomassSaveAdaptationMarker;
        }

        private static bool IsMacroSwarmSaveHeader(in EcosystemSectorSaveRecord saveRecord)
        {
            return saveRecord.PackedPopulations == MacroSwarmSaveHeaderMarker &&
                   saveRecord.PackedAdaptation != 0u;
        }

        private static bool IsMacroSwarmSaveDetail(in EcosystemSectorSaveRecord saveRecord)
        {
            return saveRecord.PackedAdaptation == MacroSwarmSaveDetailMarker;
        }

        private static bool IsFaunaGenomeSaveHeader(in EcosystemSectorSaveRecord saveRecord)
        {
            return (saveRecord.PackedPopulations & 0xFFFFFFFEu) == FaunaGenomeSaveHeaderMarker &&
                   saveRecord.PackedAdaptation != 0u;
        }

        private static bool IsFaunaGenomeSaveDetail(in EcosystemSectorSaveRecord saveRecord)
        {
            return saveRecord.PackedAdaptation == FaunaGenomeSaveDetailMarker;
        }

        private static bool UnpackBiomassRun(in EcosystemSectorSaveRecord saveRecord, out EcosystemBiomassSaveRun run)
        {
            run = default;
            if (!IsBiomassSaveRecord(in saveRecord))
                return false;

            uint packed = saveRecord.PackedPopulations;
            run.StartMacroCell = saveRecord.SectorCoord;
            run.RunLength = (byte)math.max(1, (int)(packed & BiomassSaveRunLengthMask));
            run.PreyBiomassQ = (sbyte)math.clamp((int)((packed >> BiomassSavePreyShift) & 0xFFu), 0, 100);
            run.PredatorBiomassQ = (sbyte)math.clamp((int)((packed >> BiomassSavePredatorShift) & 0xFFu), 0, 100);
            run.CarryingCapacityQ = (sbyte)math.clamp((int)((packed >> BiomassSaveCapacityShift) & 0x7Fu), 0, 100);
            return true;
        }

        private static sbyte QuantizeBiomass01(float value)
        {
            return (sbyte)math.clamp(RoundPositiveToInt(math.saturate(value) * 100f), 0, 100);
        }

        private static float ClampLotkaVolterraRate(float value)
        {
            return math.select(0f, math.clamp(value, 0f, MaxLotkaVolterraRatePerSecond), math.isfinite(value));
        }

        private static bool IsBiomassCoordAfter(int2 coord, int2 lastCoord, bool hasLastCoord)
        {
            return !hasLastCoord ||
                   coord.y > lastCoord.y ||
                   (coord.y == lastCoord.y && coord.x > lastCoord.x);
        }

        private static bool IsBiomassCoordBefore(int2 coord, int2 other)
        {
            return coord.y < other.y ||
                   (coord.y == other.y && coord.x < other.x);
        }

        private static float DequantizeBiomassQ(sbyte value)
        {
            return math.clamp((int)value, 0, 100) * 0.01f;
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
