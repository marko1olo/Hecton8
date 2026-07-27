using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Ecosystem;
using Hecton8.Gameplay;
using Hecton8.Systems.AI;
using Hecton8.UI;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using BrineLayerSample = Hecton8.Core.Contracts.BrineLayerSample;
using EcosystemSectorDTO = Hecton8.Core.Contracts.EcosystemSectorDTO;
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

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
    internal struct MacroSwarmTelemetryEntry
    {
        [System.Runtime.InteropServices.FieldOffset(0)]
        public uint FrameIndex;
        [System.Runtime.InteropServices.FieldOffset(4)]
        public uint StateHash;
        [System.Runtime.InteropServices.FieldOffset(8)]
        public int ActiveMacroSwarms;
        [System.Runtime.InteropServices.FieldOffset(12)]
        public int ArrivalCount;
        [System.Runtime.InteropServices.FieldOffset(16)]
        public float BiomassSum;
        [System.Runtime.InteropServices.FieldOffset(20)]
        public int Flags;
        [System.Runtime.InteropServices.FieldOffset(24)]
        public uint Reserved0;
        [System.Runtime.InteropServices.FieldOffset(28)]
        public uint Reserved1;
        [System.Runtime.InteropServices.FieldOffset(32)]
        private byte _pad0;
        [System.Runtime.InteropServices.FieldOffset(33)]
        private byte _pad1;
        [System.Runtime.InteropServices.FieldOffset(34)]
        private byte _pad2;
        [System.Runtime.InteropServices.FieldOffset(35)]
        private byte _pad3;
        [System.Runtime.InteropServices.FieldOffset(36)]
        private byte _pad4;
        [System.Runtime.InteropServices.FieldOffset(37)]
        private byte _pad5;
        [System.Runtime.InteropServices.FieldOffset(38)]
        private byte _pad6;
        [System.Runtime.InteropServices.FieldOffset(39)]
        private byte _pad7;
        [System.Runtime.InteropServices.FieldOffset(40)]
        private byte _pad8;
        [System.Runtime.InteropServices.FieldOffset(41)]
        private byte _pad9;
        [System.Runtime.InteropServices.FieldOffset(42)]
        private byte _pad10;
        [System.Runtime.InteropServices.FieldOffset(43)]
        private byte _pad11;
        [System.Runtime.InteropServices.FieldOffset(44)]
        private byte _pad12;
        [System.Runtime.InteropServices.FieldOffset(45)]
        private byte _pad13;
        [System.Runtime.InteropServices.FieldOffset(46)]
        private byte _pad14;
        [System.Runtime.InteropServices.FieldOffset(47)]
        private byte _pad15;
        [System.Runtime.InteropServices.FieldOffset(48)]
        private byte _pad16;
        [System.Runtime.InteropServices.FieldOffset(49)]
        private byte _pad17;
        [System.Runtime.InteropServices.FieldOffset(50)]
        private byte _pad18;
        [System.Runtime.InteropServices.FieldOffset(51)]
        private byte _pad19;
        [System.Runtime.InteropServices.FieldOffset(52)]
        private byte _pad20;
        [System.Runtime.InteropServices.FieldOffset(53)]
        private byte _pad21;
        [System.Runtime.InteropServices.FieldOffset(54)]
        private byte _pad22;
        [System.Runtime.InteropServices.FieldOffset(55)]
        private byte _pad23;
        [System.Runtime.InteropServices.FieldOffset(56)]
        private byte _pad24;
        [System.Runtime.InteropServices.FieldOffset(57)]
        private byte _pad25;
        [System.Runtime.InteropServices.FieldOffset(58)]
        private byte _pad26;
        [System.Runtime.InteropServices.FieldOffset(59)]
        private byte _pad27;
        [System.Runtime.InteropServices.FieldOffset(60)]
        private byte _pad28;
        [System.Runtime.InteropServices.FieldOffset(61)]
        private byte _pad29;
        [System.Runtime.InteropServices.FieldOffset(62)]
        private byte _pad30;
        [System.Runtime.InteropServices.FieldOffset(63)]
        private byte _pad31;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
    internal struct FaunaMutationTelemetryEntry
    {
        [System.Runtime.InteropServices.FieldOffset(0)]
        public uint FrameIndex;
        [System.Runtime.InteropServices.FieldOffset(4)]
        public uint StateHash;
        [System.Runtime.InteropServices.FieldOffset(8)]
        public int TotalMutatedEntities;
        [System.Runtime.InteropServices.FieldOffset(12)]
        public int HeadlessMutatedCount;
        [System.Runtime.InteropServices.FieldOffset(16)]
        public int MacroSwarmMutatedCount;
        [System.Runtime.InteropServices.FieldOffset(20)]
        public uint LastMutationFlags;
        [System.Runtime.InteropServices.FieldOffset(24)]
        public float LastRadiationRads;
        [System.Runtime.InteropServices.FieldOffset(28)]
        public float LastToxicity01;
        [System.Runtime.InteropServices.FieldOffset(32)]
        public float LastBrineDepth01;
        [System.Runtime.InteropServices.FieldOffset(36)]
        public uint Reserved0;
        [System.Runtime.InteropServices.FieldOffset(40)]
        public uint Reserved1;
        [System.Runtime.InteropServices.FieldOffset(44)]
        public uint Reserved2;
        [System.Runtime.InteropServices.FieldOffset(48)]
        private byte _pad0;
        [System.Runtime.InteropServices.FieldOffset(49)]
        private byte _pad1;
        [System.Runtime.InteropServices.FieldOffset(50)]
        private byte _pad2;
        [System.Runtime.InteropServices.FieldOffset(51)]
        private byte _pad3;
        [System.Runtime.InteropServices.FieldOffset(52)]
        private byte _pad4;
        [System.Runtime.InteropServices.FieldOffset(53)]
        private byte _pad5;
        [System.Runtime.InteropServices.FieldOffset(54)]
        private byte _pad6;
        [System.Runtime.InteropServices.FieldOffset(55)]
        private byte _pad7;
        [System.Runtime.InteropServices.FieldOffset(56)]
        private byte _pad8;
        [System.Runtime.InteropServices.FieldOffset(57)]
        private byte _pad9;
        [System.Runtime.InteropServices.FieldOffset(58)]
        private byte _pad10;
        [System.Runtime.InteropServices.FieldOffset(59)]
        private byte _pad11;
        [System.Runtime.InteropServices.FieldOffset(60)]
        private byte _pad12;
        [System.Runtime.InteropServices.FieldOffset(61)]
        private byte _pad13;
        [System.Runtime.InteropServices.FieldOffset(62)]
        private byte _pad14;
        [System.Runtime.InteropServices.FieldOffset(63)]
        private byte _pad15;
    }

    /// <summary>
    /// Cold-path sector ecosystem table. Population is a deterministic cinematic roll per 1 km sector.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4037)]
    public sealed class EcosystemDirector : MonoBehaviour, ISlowTickable, IFrostTickable, ILateFrameTickable, IEcosystemDirectorService, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private int _signalPushDropCount;
        internal static EcosystemDirector ActiveRuntimeInstance { get; private set; }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorReloadHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= DisposeActiveRuntimeForEditorReload;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += DisposeActiveRuntimeForEditorReload;
        }

        private static void DisposeActiveRuntimeForEditorReload()
        {
            EcosystemDirector runtime = ActiveRuntimeInstance;
            if (runtime != null)
                runtime.ShutdownServiceState();
        }
#endif

        private const float DefaultSlowTickIntervalSeconds = 0.5f;
        private const float FrostTickIntervalSeconds = 5f;
        private const float DefaultWaterSurfaceLevelY = 14.02f;
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
        private const int FaunaGeneticsTelemetryCapacity = 300;
        private const int FaunaGeneticsProfileCapacity = 64;
        private const int FaunaGeneticsCsvScratchBytes = 8192;
        private const int FaunaGeneticsTelemetryBudgetMicroseconds = 500;
        private const int MacroSwarmLowTierCap = 32;
        private const float MacroSwarmMinimumMutationCadenceWeight01 = 0.00390625f;
        private const float MacroSwarmDiffusionQualityStart01 = 0.3f;
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
        private const int GeologyBiomeCacheCapacity = 256;
        private const uint GeologyBiomeCacheIndexMask = (uint)GeologyBiomeCacheCapacity - 1u;
        private const int PredatorSpawnValidationHitCapacity = 64;
        private const float ApexSpawnGateCapsuleRadiusMeters = 2.5f;
        private const float ApexSpawnGateCapsuleHalfHeightMeters = 3f;
        private const float ApexSpawnGateCacheCellSizeMeters = 10f;
        private const float InvApexSpawnGateCacheCellSizeMeters = 1f / ApexSpawnGateCacheCellSizeMeters;
        private const float ApexSpawnGateTerrainMarginMeters = 0.1f;
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
        private const ulong FaunaGeneticsTelemetryDumpMagic = 0x00474E474F434548UL;
        private const string BiomassTelemetryDumpRelativePath = "Docs/AgentLogs/Dump_ECOLOGICAL_BIOMASS_ENGINE.bin";
        private const string MacroSwarmTelemetryDumpRelativePath = "Docs/AgentLogs/Dump_ECOSYSTEM_MIGRATION_LINK.bin";
        private const string FaunaMutationTelemetryDumpRelativePath = "Docs/AgentLogs/Dump_ECOLOGY_MUTATION_DIRECTOR.bin";
        private const string FaunaGeneticsTelemetryDumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_306.bin";
        private const string FaunaGeneticsProfilesCsvPrimaryRelativePath = "Assets/_SourceData/Biota/fauna_genetic_profiles.csv";
        private const string FaunaGeneticsProfilesCsvFallbackRelativePath = "Data/Precomputed/fauna_genetic_profiles.csv";
        private static readonly ulong SectorSolveMutationGuardMask =
            EcosystemVaultMutationGuardBit(BufferID.EcosystemSectorFrontStates) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemPreyFrontCounts) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemPredatorFrontCounts) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemSectorFoodHeatmapR8) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemSectorBackStates) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemPreyBackCounts) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemPredatorBackCounts) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemSectorIndexEntries) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemHeadlessPositions) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemHeadlessSpeciesId) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemHeadlessHunger) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemHeadlessSectorCoord) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemHeadlessSectorId) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemHeadlessFaunaGenomes) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemHeadlessMutationStableHashes) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemPreyBiomassFront) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemPredatorBiomassFront) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemBiomassCarryingCapacity) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemBiomassMacroCellCoords) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemBiomassIndexEntries) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemPreyBiomassBack) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemPredatorBiomassBack) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemBiomassSumScratch) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemBiomassCellFlags) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemBiomassBlackBox);
        private static readonly ulong GenomeMutationGuardMask =
            EcosystemVaultMutationGuardBit(BufferID.EcosystemHeadlessFaunaGenomes) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemHeadlessMutationRadiation) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemHeadlessMutationToxicity) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemHeadlessMutationBrine) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemHeadlessMutationStableHashes) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemHeadlessMutationResults) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemMacroSwarms) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemMacroSwarmMutationRadiation) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemMacroSwarmMutationToxicity) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemMacroSwarmMutationBrine) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemMacroSwarmMutationResults);
        private static readonly ulong MacroSwarmTravelMutationGuardMask =
            EcosystemVaultMutationGuardBit(BufferID.EcosystemMacroSwarms) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemMacroSwarmArrivals) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemMacroSwarmCounters) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemMacroSwarmBlackBox) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemMacroHydrationScratch) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemMacroDehydrationScratch) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemPreyBiomassFront) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemPreyBiomassBack) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemPredatorBiomassFront) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemPredatorBiomassBack) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemBiomassCarryingCapacity) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemBiomassSumScratch) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemBiomassMacroCellCoords) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemBiomassIndexEntries) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemBiomassCellFlags);
        private static readonly ulong BiomassImpactDrainMutationGuardMask =
            MacroSwarmTravelMutationGuardMask |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemPendingBiomassImpacts);
        private static readonly ulong ApexTerritoryOverlapMutationGuardMask =
            EcosystemVaultMutationGuardBit(BufferID.EcosystemApexTerritorySamples) |
            EcosystemVaultMutationGuardBit(BufferID.EcosystemApexTerritoryOverlapResults);
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
#if UNITY_EDITOR
        // COLD ALLOC: CSV import scratch keeps editor disk IO outside DataVault write ownership.
        private static readonly byte[] _faunaGeneticsCsvImportScratch = new byte[FaunaGeneticsCsvScratchBytes];
        private static int _faunaGeneticsCsvImportScratchBusy;
#endif
        private static readonly int _PredatorAUPBufferId = Shader.PropertyToID("_PredatorAUPBuffer");
        private static readonly int _PredatorAUPCountId = Shader.PropertyToID("_PredatorAUPCount");
        private static readonly int _PredatorAUPParamsId = Shader.PropertyToID("_PredatorAUPParams");
        private static readonly int _GlobalOceanPanicId = Shader.PropertyToID("_GlobalOceanPanic");
        private static readonly int _ApexInSectorId = Shader.PropertyToID("_ApexInSector");
        private static readonly int _GlobalOceanPanicColorId = Shader.PropertyToID("_GlobalOceanPanicColor");
        private static readonly int _BiolumFlashBangAUPId = Shader.PropertyToID("_BiolumFlashBangAUP");
        private static readonly int _BiolumFlashBangParamsId = Shader.PropertyToID("_BiolumFlashBangParams");
        private static readonly int _BiomassOvergrowthId = Shader.PropertyToID("_HectonBiomassOvergrowth");
        private static readonly uint _HostilityNotificationMissWarningHash =
            unchecked((uint)Hecton.Localization.LocHash.Compute("EcosystemDirector.HostilityNotificationMiss"));
        private static readonly uint _HostilityNotificationContextHash =
            unchecked((uint)Hecton.Localization.LocHash.Compute("EcosystemDirector.HostilityNotification"));

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

        private struct VaultBufferView<T> where T : struct
        {
            private IDataVault _vault;
            private IDataVault _writeLockVault;
            private VaultGenerationHandle<T> _handle;

            public static VaultBufferView<T> Create(IDataVault vault, VaultGenerationHandle<T> handle)
            {
                if (vault == null || handle.BufferID == 0u)
                {
                    handle = default;
                }

                return new VaultBufferView<T>
                {
                    _vault = vault,
                    _writeLockVault = null,
                    _handle = handle
                };
            }

            public bool IsCreated => TryResolve(out _);

            public int Length => TryResolve(out NativeArray<T> array) ? array.Length : 0;

            public bool IsOwnedHandle(SystemID systemID, BufferID bufferId)
            {
                return _vault != null &&
                       _handle.BufferID == (uint)bufferId &&
                       _handle.Generation != 0u &&
                       _handle.SystemID == (uint)systemID;
            }

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
                return TryResolve(out NativeArray<T> array)
                    ? array.GetSubArray(start, length)
                    : default;
            }

            public NativeArray<T> Resolve()
            {
                return TryResolve(out NativeArray<T> array) ? array : default;
            }

            public bool TryResolveReadOnly(out NativeArray<T>.ReadOnly array)
            {
                if (_vault != null &&
                    _handle.BufferID != 0u &&
                    _vault.TryReadOnlyHandle(in _handle, out array) &&
                    array.IsCreated)
                {
                    return true;
                }

                array = default;
                return false;
            }

            public bool TryAcquireWriteLock(SystemID systemID, out NativeArray<T> array)
            {
                IDataVault vault = _vault;
                if (vault == null ||
                    _writeLockVault != null ||
                    _handle.BufferID == 0u ||
                    _handle.Generation == 0u ||
                    _handle.SystemID != (uint)systemID)
                {
                    array = default;
                    return false;
                }

                if (!vault.TryAcquireWriteLock(in _handle, systemID, out array))
                {
                    array = default;
                    return false;
                }

                bool ownershipTransferred = false;
                try
                {
                    if (array.IsCreated)
                    {
                        _writeLockVault = vault;
                        ownershipTransferred = true;
                        return true;
                    }

                    array = default;
                    return false;
                }
                finally
                {
                    if (!ownershipTransferred)
                        vault.ReleaseWriteLock(in _handle, systemID);
                }
            }

            public bool ReleaseWriteLock(SystemID systemID)
            {
                IDataVault vault = _writeLockVault;
                if (vault == null)
                    return false;

                _writeLockVault = null;
                return vault != null &&
                       _handle.BufferID != 0u &&
                       _handle.Generation != 0u &&
                       _handle.SystemID == (uint)systemID &&
                       vault.ReleaseWriteLock(in _handle, systemID);
            }

            private bool TryResolve(out NativeArray<T> array)
            {
                if (_vault != null &&
                    _handle.BufferID != 0u &&
                    _vault.TryResolveHandle(in _handle, out array) &&
                    array.IsCreated)
                {
                    return true;
                }

                array = default;
                return false;
            }

            public static implicit operator NativeArray<T>(VaultBufferView<T> view)
            {
                return view.Resolve();
            }
        }

        private struct HeadlessEntitySoA
        {
            public VaultBufferView<float3> Positions;
            public VaultBufferView<byte> SpeciesID;
            public VaultBufferView<byte> Hunger;
            public VaultBufferView<int2> SectorCoord;
            public VaultBufferView<int> SectorID;
            public VaultBufferView<ulong> FaunaGenomes;
            public VaultBufferView<float> MutationRadiation;
            public VaultBufferView<float> MutationToxicity;
            public VaultBufferView<float> MutationBrine;
            public VaultBufferView<uint> MutationStableHashes;
            public VaultBufferView<byte> MutationResults;

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
            int start = ResolveIndexBucket(key, capacity);
            for (int probe = 0; probe < capacity; probe++)
            {
                int index = ResolveIndexProbe(start, probe, capacity);

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

        private static bool TryFindIndexEntry(
            NativeArray<EcosystemIndexEntry>.ReadOnly entries,
            long key,
            out int slot)
        {
            slot = -1;
            if (!entries.IsCreated || entries.Length <= 0)
                return false;

            int capacity = entries.Length;
            int start = ResolveIndexBucket(key, capacity);
            for (int probe = 0; probe < capacity; probe++)
            {
                int index = ResolveIndexProbe(start, probe, capacity);

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
            int start = ResolveIndexBucket(key, capacity);
            for (int probe = 0; probe < capacity; probe++)
            {
                int index = ResolveIndexProbe(start, probe, capacity);

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

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct BiomassImpactEvent
        {
            [FieldOffset(0)] public int2 MacroCellCoord;
            [FieldOffset(8)] public float Amount;
            [FieldOffset(12)] public byte Kind;
            [FieldOffset(13)] public byte Padding0;
            [FieldOffset(14)] public ushort Padding1;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
        private struct BiomassTelemetryEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public uint FrameIndex;
            [System.Runtime.InteropServices.FieldOffset(4)]
            public uint StateHash;
            [System.Runtime.InteropServices.FieldOffset(8)]
            public int ActiveCellCount;
            [System.Runtime.InteropServices.FieldOffset(12)]
            public int Flags;
            [System.Runtime.InteropServices.FieldOffset(16)]
            public float GlobalBiomassSum;
            [System.Runtime.InteropServices.FieldOffset(20)]
            public float PreyBiomassSum;
            [System.Runtime.InteropServices.FieldOffset(24)]
            public float PredatorBiomassSum;
            [System.Runtime.InteropServices.FieldOffset(28)]
            public float FloraOvergrowth01;
            [System.Runtime.InteropServices.FieldOffset(32)]
            private byte _pad0;
            [System.Runtime.InteropServices.FieldOffset(33)]
            private byte _pad1;
            [System.Runtime.InteropServices.FieldOffset(34)]
            private byte _pad2;
            [System.Runtime.InteropServices.FieldOffset(35)]
            private byte _pad3;
            [System.Runtime.InteropServices.FieldOffset(36)]
            private byte _pad4;
            [System.Runtime.InteropServices.FieldOffset(37)]
            private byte _pad5;
            [System.Runtime.InteropServices.FieldOffset(38)]
            private byte _pad6;
            [System.Runtime.InteropServices.FieldOffset(39)]
            private byte _pad7;
            [System.Runtime.InteropServices.FieldOffset(40)]
            private byte _pad8;
            [System.Runtime.InteropServices.FieldOffset(41)]
            private byte _pad9;
            [System.Runtime.InteropServices.FieldOffset(42)]
            private byte _pad10;
            [System.Runtime.InteropServices.FieldOffset(43)]
            private byte _pad11;
            [System.Runtime.InteropServices.FieldOffset(44)]
            private byte _pad12;
            [System.Runtime.InteropServices.FieldOffset(45)]
            private byte _pad13;
            [System.Runtime.InteropServices.FieldOffset(46)]
            private byte _pad14;
            [System.Runtime.InteropServices.FieldOffset(47)]
            private byte _pad15;
            [System.Runtime.InteropServices.FieldOffset(48)]
            private byte _pad16;
            [System.Runtime.InteropServices.FieldOffset(49)]
            private byte _pad17;
            [System.Runtime.InteropServices.FieldOffset(50)]
            private byte _pad18;
            [System.Runtime.InteropServices.FieldOffset(51)]
            private byte _pad19;
            [System.Runtime.InteropServices.FieldOffset(52)]
            private byte _pad20;
            [System.Runtime.InteropServices.FieldOffset(53)]
            private byte _pad21;
            [System.Runtime.InteropServices.FieldOffset(54)]
            private byte _pad22;
            [System.Runtime.InteropServices.FieldOffset(55)]
            private byte _pad23;
            [System.Runtime.InteropServices.FieldOffset(56)]
            private byte _pad24;
            [System.Runtime.InteropServices.FieldOffset(57)]
            private byte _pad25;
            [System.Runtime.InteropServices.FieldOffset(58)]
            private byte _pad26;
            [System.Runtime.InteropServices.FieldOffset(59)]
            private byte _pad27;
            [System.Runtime.InteropServices.FieldOffset(60)]
            private byte _pad28;
            [System.Runtime.InteropServices.FieldOffset(61)]
            private byte _pad29;
            [System.Runtime.InteropServices.FieldOffset(62)]
            private byte _pad30;
            [System.Runtime.InteropServices.FieldOffset(63)]
            private byte _pad31;
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct ApexTerritoryOverlapJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<ApexTerritorySample> Samples;
            [NoAlias] public NativeArray<ApexTerritoryOverlapResult> Results;
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct LotkaVolterraPopulationJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<SectorPopulationState> FrontStates;
            [ReadOnly, NoAlias] public NativeArray<int> PreyCounts;
            [ReadOnly, NoAlias] public NativeArray<int> PredatorCounts;
            [ReadOnly, NoAlias] public NativeArray<byte> FoodDensityHeatmapR8;
            [NoAlias] public NativeArray<SectorPopulationState> BackStates;
            [NoAlias] public NativeArray<int> PreyBackCounts;
            [NoAlias] public NativeArray<int> PredatorBackCounts;
            [NoAlias] public NativeArray<float3> HeadlessPositions;
            [NoAlias] public NativeArray<byte> HeadlessSpeciesID;
            [NoAlias] public NativeArray<byte> HeadlessHunger;
            [NoAlias] public NativeArray<int2> HeadlessSectorCoord;
            [NoAlias] public NativeArray<int> HeadlessSectorID;
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct BiomassLotkaVolterraJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<float> PreyFront;
            [ReadOnly, NoAlias] public NativeArray<float> PredatorFront;
            [ReadOnly, NoAlias] public NativeArray<float> CarryingCapacity;
            [ReadOnly, NoAlias] public NativeArray<int2> MacroCellCoords;
            [ReadOnly, NoAlias] public NativeArray<EcosystemIndexEntry> CellIndexEntries;
            [NoAlias] public NativeArray<float> PreyBack;
            [NoAlias] public NativeArray<float> PredatorBack;
            [NoAlias] public NativeArray<float> BiomassSumScratch;
            public float DeltaSeconds;
            public float BirthRate;
            public float PredRate;
            public float FeedRate;
            public float DeathRate;
            public float DiffusionRate;
            public float DiffusionWeight;

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

                float diffusionWeight01 = math.saturate(DiffusionWeight);
                if (diffusionWeight01 > 0f && DiffusionRate > 0f)
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
                        float diffusion01 = math.saturate(DiffusionRate) * diffusionWeight01;
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct HeadlessThresholdMigrationJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<SectorPopulationState> States;
            [ReadOnly, NoAlias] public NativeArray<byte> FoodDensityHeatmapR8;
            [NoAlias] public NativeArray<float3> Positions;
            [NoAlias] public NativeArray<int2> SectorCoord;
            [NoAlias] public NativeArray<int> SectorID;
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

        private VaultBufferView<SectorPopulationState> _sectorFrontStates;
        private VaultBufferView<SectorPopulationState> _sectorBackStates;
        private VaultBufferView<int> _preyFrontCounts;
        private VaultBufferView<int> _preyBackCounts;
        private VaultBufferView<int> _predatorFrontCounts;
        private VaultBufferView<int> _predatorBackCounts;
        private VaultBufferView<float> _preyBiomassFront;
        private VaultBufferView<float> _preyBiomassBack;
        private VaultBufferView<float> _predatorBiomassFront;
        private VaultBufferView<float> _predatorBiomassBack;
        private VaultBufferView<float> _biomassCarryingCapacity;
        private VaultBufferView<float> _biomassSumScratch;
        private VaultBufferView<int2> _biomassMacroCellCoords;
        private VaultBufferView<byte> _biomassCellFlags;
        private VaultBufferView<BiomassImpactEvent> _pendingBiomassImpacts;
        private VaultBufferView<BiomassTelemetryEntry> _biomassBlackBox;
        private VaultBufferView<MacroSwarm> _macroSwarms;
        private VaultBufferView<MacroSwarmArrival> _macroSwarmArrivals;
        private VaultBufferView<int> _macroSwarmCounters;
        private VaultBufferView<MacroSwarmTelemetryEntry> _macroSwarmBlackBox;
        private VaultBufferView<float> _macroSwarmMutationRadiation;
        private VaultBufferView<float> _macroSwarmMutationToxicity;
        private VaultBufferView<float> _macroSwarmMutationBrine;
        private VaultBufferView<byte> _macroSwarmMutationResults;
        private VaultBufferView<FaunaMutationTelemetryEntry> _faunaMutationBlackBox;
        private VaultBufferView<GeneticsTelemetryEntry> _faunaGeneticsTelemetry;
        private VaultBufferView<FaunaGeneticsTuningDTO> _faunaGeneticsTuning;
        private VaultBufferView<FaunaGeneticsProfileDTO> _faunaGeneticsProfiles;
        private VaultBufferView<byte> _faunaGeneticsCsvScratch;
        private VaultBufferView<MacroSwarm> _macroHydrationScratch;
        private VaultBufferView<MacroSwarm> _macroDehydrationScratch;
        private VaultBufferView<EcosystemBiomassSaveRun> _saveSnapshotBiomassRuns;
        private VaultBufferView<EcosystemIndexEntry> _biomassIndexEntries;
        private HeadlessEntitySoA _headlessEntities;
        private VaultBufferView<byte> _sectorFoodHeatmapR8;
        private VaultBufferView<EcosystemIndexEntry> _sectorIndexEntries;
        private VaultBufferView<ApexTerritorySample> _apexTerritorySamples;
        private VaultBufferView<ApexTerritoryOverlapResult> _apexTerritoryOverlapResults;
        private VaultBufferView<float4> _floraPredatorAupUpload;
        private VaultBufferView<EcosystemSectorSaveRecord> _saveSnapshotSectors;
        private int _saveSnapshotSectorCount;
        private int _saveSnapshotBiomassRunCount;
        private IFaunaPredationTarget[] _apexTerritoryTargets;
        private float4[] _floraPredatorAupUploadSnapshot;
        private GraphicsBuffer _floraPredatorAupBufferA;
        private GraphicsBuffer _floraPredatorAupBufferB;
        private GraphicsBuffer _activeFloraPredatorAupBuffer;
        private JobHandle _scheduledSolveHandle;
        private JobHandle _scheduledGenomeMutationHandle;
        private JobHandle _macroSwarmTravelHandle;
        private JobHandle _scheduledApexTerritoryOverlapHandle;
        private int3 _apexSpawnGateCachedCell;
        private bool _apexSpawnGateHasCachedResult;
        private int _floraPredatorAupUploadIndex;
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
        private int _faunaGeneticsTelemetryCursor;
        private int _lastFaunaGeneticsProfileCount;
        private int _lastFaunaGenomeCompiledCount;
        private int _lastFaunaGeneticsDumpFrame = -1;
        private uint _faunaGenomeMutationEpoch;
        private uint _lastMutationFlags;
        private float _lastMutationRadiationRads;
        private float _lastMutationToxicity01;
        private float _lastMutationBrineDepth01;
        private float _lastFaunaGenomeBurstMicroseconds;
        private long _faunaGenomeMutationScheduleTimestamp;
        private long _faunaGeneticsCsvLastWriteTicks;
        private int _activeMacroSwarmCount;
        private int _macroSwarmActiveCap = MacroSwarmLowTierCap;
        private byte _macroSwarmQualityTierProfileByte;
        private float _macroSwarmQualityWeight01;
        private float _macroSwarmSpeedCellsPerSecond = MacroSwarmDefaultSpeedCellsPerSecond * 0.5f;
        private int _lastMacroSwarmArrivalCount;
        private int _lastMacroSwarmsHydrated;
        private int _lastMacroHydratedBoidEstimate;
        private float _lastMacroSwarmBiomassSum;
        private uint _lastMacroSwarmStateHash;
        private int _lastSectorResidencySignalDrainFrame;
        private int _scheduledApexTerritoryOverlapCount;
        private IDataVault _dataVault;
        private VaultGenerationHandle<EcosystemSectorDTO> _macroSectorSnapshotHandle;
        private VaultGenerationHandle<MacroEcosystemSectorIndexRecord> _macroSectorIndexHandle;
        private VaultGenerationHandle<MacroEcosystemTuningVaultRecord> _macroTuningHandle;
        private bool _registeredService;
        private bool _registeredSlowTickable;
        private bool _registeredFrostTickable;
        private bool _registeredLateFrameTickable;
        private bool _registeredHotSwap;
        private bool _solveScheduled;
        private bool _genomeMutationScheduled;
        private bool _macroSwarmTravelScheduled;
        private bool _apexTerritoryOverlapScheduled;
        private bool _solveJobLocksHeld;
        private bool _genomeMutationJobLocksHeld;
        private bool _macroSwarmTravelJobLocksHeld;
        private bool _apexTerritoryOverlapJobLocksHeld;
        private IDataVault _solveJobGuardVault;
        private IDataVault _genomeMutationJobGuardVault;
        private IDataVault _macroSwarmTravelJobGuardVault;
        private IDataVault _apexTerritoryOverlapJobGuardVault;
        private bool _populationSolvePendingHibernationSync;
        private float _biomeHostility01;
        private float _biomeGradientBlend01;
        private byte _biomeGradientA;
        private byte _biomeGradientB;
        private float _starvationAggressionPressure01;
        private float _playerStress01;
        private float _spawnCreditBudget;
        private int _hostilityTier;
        private int _hostilityNotificationMissCount;
        private bool _floraPredatorAupSaturationTelemetryIssued;
        private float _lastPublishedGlobalOceanPanic01 = -1f;
        private byte _lastPublishedApexInSector = byte.MaxValue;
        private int _lastPublishedFloraPredatorAupCount = -1;
        private bool _floraPredatorAupGlobalsDirty = true;
        private bool _floraPredatorAupRefreshDirty;
        private bool _floraPredatorAupCountDirty;
        private bool _apexPresenceFakeDirty;
        private bool _globalOceanPanicDirty;
        private bool _biolumFlashBangDirty;
        private bool _biomassOvergrowthDirty;
        private Vector3 _pendingFloraPredatorAupQueryOrigin;
        private int _pendingFloraPredatorAupCount;
        private byte _pendingApexInSector;
        private float _pendingGlobalOceanPanic01;
        private Vector4 _pendingBiolumFlashBangAup;
        private Vector4 _pendingBiolumFlashBangParams;
        private float _pendingBiomassOvergrowth01;
        private float _lastPublishedBiomassOvergrowth01 = -1f;
        private int _nextHibernationPopulationSyncIndex;

        private struct GeologyBiomeCacheEntry
        {
            public int2 SectorCoord;
            public int BiomeId;
            public byte Occupied;
        }

        // COLD ALLOC: GeologyBiomeCacheEntry[256] - direct-mapped sector -> biome classification cache so
        // the macro geology stack runs once per 1 km sector instead of once per 50 m biomass macro cell
        // (400 macro cells per sector) - owner: EcosystemDirector
        private GeologyBiomeCacheEntry[] _geologyBiomeCache;
        private WorldMacroGeologyParams _geologyBiomeParams;
        private bool _geologyBiomeParamsResolved;
        private int _geologyBiomeParamsSeed;

        private MapMagicBridge _cachedMapMagicBridge;
        private HectonMapMagicVegetationBridge _cachedVegetationBridge;
        private PersistentWorldRegistry _cachedPersistentWorldRegistry;
        private SargassumMicroFaunaBoids _cachedSargassumMicroFauna;
        private IAmbientBiotaService _cachedAmbientBiota;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private IHazardZoneReadModel _cachedHazardZones;
        private ResourceDistributionDirector _cachedResourceDistribution;
        private IHectonOceanKinematicsService _cachedOceanKinematicsService;
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
            _sectorIndexEntries.IsCreated;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => IsServiceReady ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _registeredService && IsInitialized && ReferenceEquals(GlobalRegistry.EcosystemDirector, this);

        /// <summary>
        /// Normalized biome hostility score exposed to UI and pacing systems.
        /// </summary>
        public float BiomeHostility01 => ResolveCombinedHostility01();
        public int HostilityNotificationMissCount => _hostilityNotificationMissCount;

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

            IDataVault vault = ResolveDataVaultCold();
            if (vault == null)
            {
                _sectorFoodHeatmapR8 = default;
                _sectorFoodHeatmapSize = default;
                return;
            }

            VaultGenerationHandle<byte> heatmapHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.EcosystemSectorFoodHeatmapR8,
                (int)requiredLength,
                SystemID.AIEcology,
                NativeArrayOptions.UninitializedMemory);
            if (!vault.TryAcquireWriteLock(in heatmapHandle, SystemID.AIEcology, out NativeArray<byte> vaultHeatmap))
            {
                _sectorFoodHeatmapR8 = default;
                _sectorFoodHeatmapSize = default;
                return;
            }

            try
            {
                if (!vaultHeatmap.IsCreated || vaultHeatmap.Length < requiredLength)
                {
                    _sectorFoodHeatmapR8 = default;
                    _sectorFoodHeatmapSize = default;
                    return;
                }

                for (int i = 0; i < requiredLength; i++)
                    vaultHeatmap[i] = heatmapR8[i];

                _sectorFoodHeatmapR8 = VaultBufferView<byte>.Create(vault, heatmapHandle);
                _sectorFoodHeatmapSize = new int2(width, height);
            }
            finally
            {
                vault.ReleaseWriteLock(in heatmapHandle, SystemID.AIEcology);
            }
        }

        public FaunaLogicalLodTier ResolveLogicalLodTier(Vector3 observerPosition, Vector3 faunaPosition)
        {
            if (!TryResolveAupFromRuntimeOrigin(observerPosition, out AbsoluteUniversePosition observerAup) ||
                !TryResolveAupFromRuntimeOrigin(faunaPosition, out AbsoluteUniversePosition faunaAup))
            {
                return FaunaLogicalLodTier.Hibernating;
            }

            return ResolveLogicalLodTier(in observerAup, in faunaAup);
        }

        public FaunaLogicalLodTier ResolveLogicalLodTier(
            in AbsoluteUniversePosition observerAup,
            in AbsoluteUniversePosition faunaAup)
        {
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in observerAup, in faunaAup);
            float zone1RadiusSq = LogicalLodFullSimDistanceMeters * LogicalLodFullSimDistanceMeters;
            float zone2RadiusSq = LogicalLodDataOnlyDistanceMeters * LogicalLodDataOnlyDistanceMeters;
            float qualityWeight = ResolveGlobalQualityWeight01();

            int tierIndex = Hecton8.PureLogic.Ecosystem.EcosystemLogicalLodTieringCalculator.Compute(
                (float)distanceSq,
                zone1RadiusSq,
                zone2RadiusSq,
                qualityWeight);

            return (FaunaLogicalLodTier)tierIndex;
        }

        internal bool TryBuildEnvelope(Vector3 worldPosition, out EcosystemEnvelope envelope)
        {
            float depthMeters = math.max(0f, ResolveWaterSurfaceLevel() - worldPosition.y);

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

        public bool TryResolveSpawnWeightMultiplier(CreatureArchetypeData archetype, Vector3 worldPosition, out float selectionMultiplier)
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

        public bool TryConsumeSpawnCredit(CreatureArchetypeData archetype, bool isLargeThreat, bool isPredator)
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

        public void RefundSpawnCredit(CreatureArchetypeData archetype, bool isLargeThreat, bool isPredator)
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
            float effectiveRegenRate = spawnCreditRecoverPerSecond * recoveryScale * biomeGradientScale;

            _spawnCreditBudget = Hecton8.PureLogic.Ecosystem.EcosystemSpawnCreditBudgeting.Calculate(
                _spawnCreditBudget, spawnCreditBudgetMax, effectiveRegenRate, deltaSeconds);

            _debugSpawnCreditBudget = _spawnCreditBudget;
        }

        private float ResolveSpawnCreditSelectionWeight(CreatureArchetypeData archetype)
        {
            float cost = ResolveSpawnCreditCost(archetype, IsApexRole(archetype), IsPredatorOrApex(archetype));
            return Hecton8.PureLogic.Ecosystem.EcosystemSpeciesSelectionWeightCalculator.Compute(1f, cost, _spawnCreditBudget);
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

            HectonDirectorAI director = null;
            HectonDirectorAI.TryResolveActiveRuntime(ref director);
            if (director != null)
            {
                stress01 = math.max(stress01, math.saturate(director.CurrentStress01));
                resolved = true;
            }

            IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            if (runtimeContext != null)
            {
                if (runtimeContext.TryGetSurvivalRuntimeState(out PlayerSurvivalRuntimeState survivalState) &&
                    (survivalState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasSurvival) != 0u)
                {
                    stress01 = math.max(stress01, math.saturate(1f - survivalState.OxygenNormalized));
                    stress01 = math.max(stress01, math.saturate(1f - survivalState.IntegrityNormalized));
                    stress01 = math.max(stress01, math.saturate(survivalState.PressureExposureSeverity01));
                    stress01 = math.max(stress01, math.saturate(survivalState.ThermalStressSeverity01));
                    resolved = true;
                }

                if (runtimeContext.TryGetMovementStressRuntimeState(out PlayerMovementStressRuntimeState stressState) &&
                    (stressState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasMovement) != 0u &&
                    (stressState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                    math.isfinite(stressState.UnderwaterStressIntensity01))
                {
                    stress01 = math.max(stress01, math.saturate(stressState.UnderwaterStressIntensity01));
                    resolved = true;
                }

                stress01 = math.saturate(stress01);
                return resolved;
            }

            IPlayerRuntimeContext playerContext = ActiveRuntimeInstance != null
                ? ActiveRuntimeInstance._cachedPlayerContext
                : null;
            if (playerContext != null)
            {
                if (playerContext.TryGetSurvivalRuntimeState(out PlayerSurvivalRuntimeState survivalState) &&
                    (survivalState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasSurvival) != 0u)
                {
                    stress01 = math.max(stress01, math.saturate(1f - survivalState.OxygenNormalized));
                    stress01 = math.max(stress01, math.saturate(1f - survivalState.IntegrityNormalized));
                    stress01 = math.max(stress01, math.saturate(survivalState.PressureExposureSeverity01));
                    stress01 = math.max(stress01, math.saturate(survivalState.ThermalStressSeverity01));
                    resolved = true;
                }

                if (playerContext.TryGetMovementStressRuntimeState(out PlayerMovementStressRuntimeState stressState) &&
                    (stressState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasMovement) != 0u &&
                    (stressState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                    math.isfinite(stressState.UnderwaterStressIntensity01))
                {
                    stress01 = math.max(stress01, math.saturate(stressState.UnderwaterStressIntensity01));
                    resolved = true;
                }
            }

            stress01 = math.saturate(stress01);
            return resolved;
        }

        private bool PassesApexSpawnVoxelGate(Vector3 worldPosition)
        {
            int3 gateCell = QuantizeApexSpawnGateCell(worldPosition);
            if (_apexSpawnGateHasCachedResult && math.all(gateCell == _apexSpawnGateCachedCell))
                return _apexSpawnGateCachedBlocked == 0;

            byte blocked = PackBooleanByte(IsApexSpawnTerrainBlocked(worldPosition));
            _apexSpawnGateCachedCell = gateCell;
            _apexSpawnGateCachedBlocked = blocked;
            _apexSpawnGateHasCachedResult = true;
            return blocked == 0;
        }

        private bool IsApexSpawnTerrainBlocked(Vector3 worldPosition)
        {
            if (!math.isfinite(worldPosition.x) ||
                !math.isfinite(worldPosition.y) ||
                !math.isfinite(worldPosition.z))
            {
                return true;
            }

            MapMagicBridge bridge = _cachedMapMagicBridge;
            if (bridge == null ||
                !bridge.TryGetHeight(worldPosition.x, worldPosition.z, out float terrainHeight) ||
                !math.isfinite(terrainHeight))
            {
                return true;
            }

            float capsuleBottom = worldPosition.y - ApexSpawnGateCapsuleHalfHeightMeters - ApexSpawnGateCapsuleRadiusMeters;
            return capsuleBottom <= terrainHeight + ApexSpawnGateTerrainMarginMeters;
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
                IFaunaSpatialContact preyContact = hit.Owner as IFaunaSpatialContact;
                if (preyContact == null)
                {
                    preyContact = ComponentReferenceUtility.ResolveParentService<IFaunaSpatialContact>(hit.Transform);
                    if (preyContact == null)
                        continue;
                }

                uint preyMaskBits = preyContact.PreyMaskBits;
                if (preyMaskBits != 0u && (dietMaskBits & preyMaskBits) != 0u)
                    return true;
            }

            return false;
        }

        public bool IsHerbivoreSpecies(int speciesId)
        {
            return ContainsSpeciesId(herbivoreSpeciesIds, speciesId);
        }

        public bool IsCleanerSpecies(int speciesId)
        {
            return ContainsSpeciesId(cleanerSpeciesIds, speciesId);
        }

        internal bool IsCleanerHostSpecies(FaunaBrain hostBrain)
        {
            if (hostBrain == null)
                return false;

            int speciesId = hostBrain.SpeciesId;
            bool isLeviathan = hostBrain.SpeciesProfile != null && hostBrain.SpeciesProfile.isLeviathan;
            return IsCleanerHostSpecies(speciesId, isLeviathan);
        }

        public bool IsCleanerHostSpecies(int speciesId, bool isLeviathan)
        {
            if (cleanerHostSpeciesIds != null && cleanerHostSpeciesIds.Length > 0)
                return ContainsSpeciesId(cleanerHostSpeciesIds, speciesId);

            return isLeviathan;
        }

        public float HerbivoreGrazeHungerThreshold => herbivoreGrazeHungerThreshold;
        public float HerbivoreGrazeSearchRadiusMeters => herbivoreGrazeSearchRadiusMeters;
        public float HerbivoreConsumeDistanceMeters => herbivoreConsumeDistanceMeters;
        public float CleanerHostSearchRadiusMeters => cleanerHostSearchRadiusMeters;
        public float CleanerSymbiosisDistanceMeters => cleanerSymbiosisDistanceMeters;
        public float CleanerFatigueReliefPerSecond => cleanerFatigueReliefPerSecond;
        public float ScavengerHungerThreshold => scavengerHungerThreshold;
        public float ScavengerConsumeDistanceMeters => scavengerConsumeDistanceMeters;
        public float ScavengerConsumeUnitsPerSecond => scavengerConsumeUnitsPerSecond;
        public float BaitFeedingDistanceMeters => baitFeedingDistanceMeters;

        private static bool TryResolveDestructibleOrganicManager(out DestructibleOrganicManager organicManager)
        {
            organicManager = null;
            return WorldRuntimeReferenceUtility.TryResolveDestructibleOrganicManager(ref organicManager);
        }

        public bool TryResolveHerbivoreGrazeTarget(Vector3 worldPosition, out Vector3 floraPosition, out uint floraInstanceUid)
        {
            floraPosition = default;
            floraInstanceUid = 0u;
            TryResolveDestructibleOrganicManager(out DestructibleOrganicManager organicManager);
            return organicManager != null &&
                   organicManager.TryResolveNearestConsumableFlora(worldPosition, herbivoreGrazeSearchRadiusMeters, out floraPosition, out floraInstanceUid);
        }

        public bool TryConsumeHerbivoreGrazeTarget(uint floraInstanceUid)
        {
            TryResolveDestructibleOrganicManager(out DestructibleOrganicManager organicManager);
            return organicManager != null && organicManager.TryConsumeFlora(floraInstanceUid);
        }

        public bool TryResolveMigrationTarget(int speciesId, Vector3 origin, out Vector3 target)
        {
            return MigrationDirector.TryResolveMigrationTarget(speciesId, origin, out target);
        }

        public bool TryResolveNearestThermalVentAttractor(
            in AbsoluteUniversePosition queryAup,
            float searchRadiusMeters,
            out Vector3 target,
            out float heat01)
        {
            target = default;
            heat01 = 0f;
            AbyssalThermalManager thermalManager = null;
            WorldRuntimeReferenceUtility.TryResolveAbyssalThermalManager(ref thermalManager);
            return thermalManager != null &&
                   thermalManager.TryResolveNearestActiveVentAttractor(in queryAup, searchRadiusMeters, out target, out heat01);
        }

        internal void RegisterCorpseResourceNode(Vector3 worldPosition, int speciesId, float capacityUnits)
        {
            RegisterCorpseResourceNode(worldPosition, speciesId, capacityUnits, 0u);
        }

        internal void RegisterCorpseResourceNode(Vector3 worldPosition, int speciesId, float capacityUnits, uint contaminatedItemHash)
        {
            TryResolveDestructibleOrganicManager(out DestructibleOrganicManager organicManager);
            if (organicManager != null)
                organicManager.RegisterCorpseResourceNode(worldPosition, speciesId, capacityUnits, contaminatedItemHash);
        }

        internal void RegisterCorpseResourceNode(in AbsoluteUniversePosition positionAup, int speciesId, float capacityUnits)
        {
            RegisterCorpseResourceNode(in positionAup, speciesId, capacityUnits, 0u);
        }

        public void RegisterCorpseResourceNode(in AbsoluteUniversePosition positionAup, int speciesId, float capacityUnits, uint contaminatedItemHash)
        {
            TryResolveDestructibleOrganicManager(out DestructibleOrganicManager organicManager);
            if (organicManager != null)
                organicManager.RegisterCorpseResourceNode(in positionAup, speciesId, capacityUnits, contaminatedItemHash);
        }

        internal bool TryResolveCorpseScavengeTarget(Vector3 worldPosition, out Vector3 corpsePosition, out uint corpseNodeId)
        {
            corpsePosition = default;
            corpseNodeId = 0u;
            TryResolveDestructibleOrganicManager(out DestructibleOrganicManager organicManager);
            return organicManager != null &&
                   organicManager.TryResolveNearestCorpseResourceNode(worldPosition, scavengerCorpseSearchRadiusMeters, out corpsePosition, out corpseNodeId);
        }

        public bool TryResolveCorpseScavengeTarget(in AbsoluteUniversePosition queryAup, out Vector3 corpsePosition, out uint corpseNodeId)
        {
            corpsePosition = default;
            corpseNodeId = 0u;
            TryResolveDestructibleOrganicManager(out DestructibleOrganicManager organicManager);
            return organicManager != null &&
                   organicManager.TryResolveNearestCorpseResourceNode(in queryAup, scavengerCorpseSearchRadiusMeters, out corpsePosition, out corpseNodeId);
        }

        public bool TryConsumeCorpseScavengeTarget(uint corpseNodeId, float consumeUnits)
        {
            TryResolveDestructibleOrganicManager(out DestructibleOrganicManager organicManager);
            return organicManager != null && organicManager.TryConsumeCorpseResourceNode(corpseNodeId, consumeUnits);
        }

        public bool TryResolveCorpseDiseaseExposure(
            in AbsoluteUniversePosition queryAup,
            float currentTimeSeconds,
            out float severity01,
            out Vector3 sourcePosition)
        {
            severity01 = 0f;
            sourcePosition = default;
            TryResolveDestructibleOrganicManager(out DestructibleOrganicManager organicManager);
            return organicManager != null &&
                   organicManager.TryResolveCorpseDiseaseExposure(in queryAup, currentTimeSeconds, out severity01, out sourcePosition);
        }

        public bool TryResolveNearestOrganicMass(Vector3 worldPosition, out Vector3 organicPosition)
        {
            organicPosition = default;
            if (!TryResolveAupFromRuntimeOrigin(worldPosition, out AbsoluteUniversePosition queryAup))
                return false;

            return TryResolveNearestOrganicMass(in queryAup, out organicPosition);
        }

        internal bool TryResolveNearestOrganicMass(in AbsoluteUniversePosition queryAup, out Vector3 organicPosition)
        {
            organicPosition = default;
            TryResolveDestructibleOrganicManager(out DestructibleOrganicManager organicManager);
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

        public bool TryConsumeOrganicMassAtPosition(Vector3 worldPosition, float searchRadius)
        {
            TryResolveDestructibleOrganicManager(out DestructibleOrganicManager organicManager);
            if (organicManager == null)
                return false;

            float safeSearchRadius = math.max(0.1f, searchRadius);
            if (organicManager.TryConsumeFloraAtPosition(worldPosition, safeSearchRadius, out _))
                return true;

            if (!organicManager.TryResolveNearestCorpseResourceNode(worldPosition, safeSearchRadius, out _, out uint corpseNodeId))
                return false;

            return organicManager.TryConsumeCorpseResourceNode(corpseNodeId, scavengerConsumeUnitsPerSecond);
        }

        public void PublishBiolumFlashBang(in AbsoluteUniversePosition flashAup, float currentTimeSeconds, float radiusMeters = 42f)
        {
            Vector3 flashPosition = flashAup.ToRuntimeFloat3();
            _pendingBiolumFlashBangAup = new Vector4(flashPosition.x, flashPosition.y, flashPosition.z, math.max(0.1f, radiusMeters));
            _pendingBiolumFlashBangParams = new Vector4(currentTimeSeconds, 0.1f, 4f, 0f);
            _biolumFlashBangDirty = true;
        }

        internal bool DoesSpeciesRespondToBait(FaunaBrain faunaBrain)
        {
            if (faunaBrain == null || faunaBrain.SpeciesProfile == null)
                return false;

            int speciesId = faunaBrain.SpeciesId;
            return DoesSpeciesRespondToBait(
                speciesId,
                faunaBrain.SpeciesProfile.isScavenger,
                faunaBrain.isAggressive,
                faunaBrain.SpeciesProfile.isLeviathan);
        }

        public bool DoesSpeciesRespondToBait(int speciesId, bool isScavenger, bool isAggressive, bool isLeviathan)
        {
            return isScavenger ||
                   IsHerbivoreSpecies(speciesId) ||
                   (isAggressive && !isLeviathan);
        }

        public bool IsApexTombstoned(uint uniqueInstanceUid)
        {
            return _cachedPersistentWorldRegistry != null && _cachedPersistentWorldRegistry.IsTombstoned(uniqueInstanceUid);
        }

        public void RegisterApexPredatorKill(uint uniqueInstanceUid, Vector3 worldPosition, float hostilityDelta)
        {
            float nowSeconds = ReadDispatcherTimeSeconds();
            _cachedPersistentWorldRegistry?.TryRegisterFaunaTombstone(uniqueInstanceUid);
            if (_cachedPersistentWorldRegistry != null)
            {
                if (TryResolveAupFromRuntimeOrigin(worldPosition, out AbsoluteUniversePosition whaleFallAup))
                    _cachedPersistentWorldRegistry.TryCacheWhaleFallPoiState(uniqueInstanceUid, unchecked((int)(uniqueInstanceUid & 0x00FFFFFFu)), in whaleFallAup, nowSeconds);
            }

            MigrationDirector.RegisterPredatorKillPoi(uniqueInstanceUid, worldPosition, nowSeconds);
            SargassumMicroFaunaBoids microFaunaBoids = _cachedSargassumMicroFauna;
            if (microFaunaBoids != null)
                microFaunaBoids.RegisterWhaleFallScavengerBurst(worldPosition, uniqueInstanceUid, nowSeconds);

            _activeWhaleFallAcousticPosition = worldPosition;
            _activeWhaleFallAcousticUid = uniqueInstanceUid;
            _activeWhaleFallAcousticUntilTime = nowSeconds + WhaleFallAcousticImpulseLifetimeSeconds;
            ReportApexPredatorKilled(worldPosition, hostilityDelta);
        }

        internal void CaptureSaveSnapshot()
        {
            _saveSnapshotSectorCount = 0;
            _saveSnapshotBiomassRunCount = 0;
            if (!IsInitialized)
                return;

            CompleteScheduledSimulation(forceComplete: true);
            CompleteScheduledMacroSwarmTravel(forceComplete: true);
            ApplyPendingBiomassImpacts();
            SyncPendingHibernatedFaunaPopulationRecords();
            CaptureBiomassSaveRuns();

            if (!_saveSnapshotSectors.TryAcquireWriteLock(SystemID.AIEcology, out NativeArray<EcosystemSectorSaveRecord> saveSectors))
                return;

            try
            {
                if (_sectorFrontStates.TryResolveReadOnly(out NativeArray<SectorPopulationState>.ReadOnly sectorFrontStates))
                {
                    int sectorCount = math.min(_activeSectorCount, sectorFrontStates.Length);
                    for (int sectorIndex = 0; sectorIndex < sectorCount; sectorIndex++)
                    {
                        SectorPopulationState state = sectorFrontStates[sectorIndex];
                        TryAppendSaveSnapshotSector(saveSectors, new EcosystemSectorSaveRecord
                        {
                            SectorCoord = state.SectorCoord,
                            PackedPopulations = PackPopulationCounts(state.PreyPopulationRounded, state.PredatorPopulationRounded),
                            PackedAdaptation = PackAdaptationTraits(state.Fitness, state.SpeedMultiplier, state.CamouflageIndex, maximumSpeedMultiplier)
                        });
                    }
                }

                if (_saveSnapshotBiomassRuns.TryResolveReadOnly(out NativeArray<EcosystemBiomassSaveRun>.ReadOnly biomassRuns))
                {
                    int runCount = math.min(_saveSnapshotBiomassRunCount, biomassRuns.Length);
                    for (int runIndex = 0; runIndex < runCount && HasSaveSnapshotSectorCapacity(saveSectors, 1); runIndex++)
                        TryAppendSaveSnapshotSector(saveSectors, PackBiomassRunAsSectorRecord(biomassRuns[runIndex]));
                }

                CaptureMacroSwarmSaveRecords(saveSectors);
                CaptureFaunaGenomeSaveRecords(saveSectors);
            }
            finally
            {
                _saveSnapshotSectors.ReleaseWriteLock(SystemID.AIEcology);
            }
        }

        internal NativeArray<EcosystemSectorSaveRecord>.ReadOnly GetSaveSnapshotArray(out int recordCount)
        {
            recordCount = 0;
            if (_saveSnapshotSectorCount <= 0 ||
                !_saveSnapshotSectors.TryResolveReadOnly(out NativeArray<EcosystemSectorSaveRecord>.ReadOnly snapshot))
            {
                return default;
            }

            recordCount = math.min(_saveSnapshotSectorCount, snapshot.Length);
            return snapshot;
        }

        internal NativeArray<EcosystemBiomassSaveRun>.ReadOnly GetBiomassSaveSnapshotArray(out int recordCount)
        {
            recordCount = 0;
            if (_saveSnapshotBiomassRunCount <= 0 ||
                !_saveSnapshotBiomassRuns.TryResolveReadOnly(out NativeArray<EcosystemBiomassSaveRun>.ReadOnly snapshot))
            {
                return default;
            }

            recordCount = math.min(_saveSnapshotBiomassRunCount, snapshot.Length);
            return snapshot;
        }

        internal unsafe void RestoreFromLoadedRecords(EcosystemSectorSaveRecord[] loadedRecords)
        {
            if (!IsInitialized)
                return;

            CompleteScheduledSimulation(forceComplete: true);
            if (!TryLockSectorSolveJobBuffers())
                return;

            try
            {
                NativeArray<EcosystemIndexEntry> sectorIndexEntries = _sectorIndexEntries.Resolve();
                NativeArray<SectorPopulationState> sectorFrontStates = _sectorFrontStates.Resolve();
                NativeArray<SectorPopulationState> sectorBackStates = _sectorBackStates.Resolve();
                ClearIndexEntries(sectorIndexEntries);
                _coldTickAccumulator = 0f;
                _solveScheduled = false;
                _scheduledSolveHandle = default;

                if (sectorFrontStates.IsCreated)
                {
                    void* frontPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks<SectorPopulationState>(sectorFrontStates);
                    UnsafeUtility.MemClear(frontPtr, sectorFrontStates.Length * UnsafeUtility.SizeOf<SectorPopulationState>());
                }

                if (sectorBackStates.IsCreated)
                {
                    void* backPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks<SectorPopulationState>(sectorBackStates);
                    UnsafeUtility.MemClear(backPtr, sectorBackStates.Length * UnsafeUtility.SizeOf<SectorPopulationState>());
                }
            }
            finally
            {
                UnlockSectorSolveJobBuffers();
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

                int biomeId = ResolveGeologyBiomeIdForSector(saveRecord.SectorCoord);
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

                if (!TryLockSectorSolveJobBuffers())
                    continue;

                try
                {
                    NativeArray<EcosystemIndexEntry> sectorIndexEntries = _sectorIndexEntries.Resolve();
                    NativeArray<SectorPopulationState> sectorFrontStates = _sectorFrontStates.Resolve();
                    NativeArray<SectorPopulationState> sectorBackStates = _sectorBackStates.Resolve();
                    if (!sectorIndexEntries.IsCreated ||
                        !sectorFrontStates.IsCreated ||
                        !sectorBackStates.IsCreated ||
                        _activeSectorCount >= sectorFrontStates.Length ||
                        _activeSectorCount >= sectorBackStates.Length)
                    {
                        continue;
                    }

                    int sectorIndex = _activeSectorCount;
                    sectorFrontStates[sectorIndex] = restoredState;
                    sectorBackStates[sectorIndex] = restoredState;
                    WriteHeadlessSlot(sectorIndex, in restoredState);
                    TryUpsertIndexEntry(sectorIndexEntries, PackSectorKey(saveRecord.SectorCoord), sectorIndex);
                    _activeSectorCount++;
                }
                finally
                {
                    UnlockSectorSolveJobBuffers();
                }
            }

            if (TryLockSectorSolveJobBuffers())
            {
                try
                {
                    PublishBiomassTelemetryAndEvents();
                }
                finally
                {
                    UnlockSectorSolveJobBuffers();
                }
            }
        }

        private void Awake()
        {
            ActiveRuntimeInstance = this;
            SanitizeSettings();
            CacheColdRegistryReferences();
            AllocateRuntimeState();
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;
            SanitizeSettings();
            CacheColdRegistryReferences();
            AllocateRuntimeState();
            RefreshMacroSwarmScalabilityCache();
            TryRegisterService();
            TryRegisterSlowTickable();
            TryRegisterFrostTickable();
            TryRegisterLateFrameTickable();
            TryRegisterHotSwapListener();
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
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            _cachedAmbientBiota = null;
            _cachedPlayerContext = null;
            _cachedSargassumMicroFauna = null;
            _cachedOceanKinematicsService = null;
            ClearHostilityNotificationDiagnostics();
            DisposeRuntimeState();
        }

        /// <summary>
        /// Explicit bootstrap registration pass for headless/data-only simulation.
        /// </summary>
        internal void InitializeService()
        {
            ActiveRuntimeInstance = this;
            SanitizeSettings();
            CacheColdRegistryReferences();
            AllocateRuntimeState();
            RefreshMacroSwarmScalabilityCache();
            TryRegisterService();
            TryRegisterSlowTickable();
            TryRegisterFrostTickable();
            TryRegisterLateFrameTickable();
            TryRegisterHotSwapListener();
        }

        /// <summary>
        /// Advances the sector population solve at 0.1 Hz using a Burst job.
        /// </summary>
        public void SlowTick()
        {
            if (!IsInitialized)
                return;

            RefreshMacroSwarmScalabilityCache();
            RefreshMacroEcosystemVaultHandlesCold(ResolveDataVault());
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
            if (!TryLockMacroSwarmTravelJobBuffers())
                return;

            try
            {
                ApplyMacroSwarmPredatorAttraction();
                SpawnMacroSwarmDiffusionGradient();
                PushMacroSwarmBlackBox(0);
            }
            finally
            {
                UnlockMacroSwarmTravelJobBuffers();
            }

            JobHandle mutationDependency = ScheduleFaunaGenomeMutation();
            ScheduleMacroSwarmTravel(mutationDependency);
        }

        private void EmitWhaleFallAcousticImpulseSlowTick()
        {
            if (_activeWhaleFallAcousticUid == 0u || ReadDispatcherTimeSeconds() > _activeWhaleFallAcousticUntilTime)
                return;

            AcousticPingSignal signal = default;
            if (!TryResolveAupFromRuntimeOrigin(_activeWhaleFallAcousticPosition, out signal.PositionAup))
                return;

            signal.RadiusMeters = math.max(CorpseSpawnInfluenceRadiusMeters, scavengerCorpseSearchRadiusMeters);
            signal.Intensity01 = WhaleFallAcousticImpulseVolume01;
            signal.SourceId = _activeWhaleFallAcousticUid;
            signal.Channel = AcousticPingSignal.ChannelLeviathanRoar;
            signal.Flags = AcousticPingSignal.FlagLeviathanRoar;
            SignalBus<AcousticPingSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
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
            {
                PushMacroSwarmHeartbeatBlackBox();
                PushFaunaGeneticsTelemetryFrame();
            }
            DrainSectorResidencySignalSnapshots();
            DrainBiomassSignalSnapshots();
            PublishScannerEcologyWarningIfNeeded();
            CompleteScheduledApexTerritoryOverlap(forceComplete: false);
            FlushQueuedEcosystemVisuals();
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

            if (!TryQuantizeSector(worldPosition, out int2 sectorCoord))
                return false;

            if (!TryResolveSectorSlotReadOnly(sectorCoord, out int slotIndex))
                return false;

            if (!_sectorFrontStates.TryResolveReadOnly(out NativeArray<SectorPopulationState>.ReadOnly sectorFrontStates) ||
                (uint)slotIndex >= (uint)sectorFrontStates.Length)
            {
                return false;
            }

            SectorPopulationState state = sectorFrontStates[slotIndex];
            sample.SectorX = state.SectorCoord.x;
            sample.SectorZ = state.SectorCoord.y;
            sample.PreyPopulation = state.PreyPopulationRounded;
            sample.PredatorPopulation = state.PredatorPopulationRounded;
            sample.Fitness = state.Fitness;
            sample.SpeedMultiplier = state.SpeedMultiplier;
            sample.CamouflageIndex = state.CamouflageIndex;
            sample.ApexInSector = IsApexInSectorState(in state) ? (byte)1 : (byte)0;
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

            if (!TryQuantizeBiomassMacroCell(worldPosition, out int2 macroCellCoord))
                return false;

            if (!TryResolveBiomassCellSlotReadOnly(macroCellCoord, out int slotIndex))
                return false;

            if (!_preyBiomassFront.TryResolveReadOnly(out NativeArray<float>.ReadOnly preyFront) ||
                !_predatorBiomassFront.TryResolveReadOnly(out NativeArray<float>.ReadOnly predatorFront) ||
                !_biomassCarryingCapacity.TryResolveReadOnly(out NativeArray<float>.ReadOnly carryingCapacity) ||
                (uint)slotIndex >= (uint)preyFront.Length ||
                (uint)slotIndex >= (uint)predatorFront.Length ||
                (uint)slotIndex >= (uint)carryingCapacity.Length)
            {
                return false;
            }

            float capacity = math.max(0.0001f, carryingCapacity[slotIndex]);
            preyBiomass01 = math.saturate(preyFront[slotIndex] * math.rcp(capacity));
            predatorBiomass01 = math.saturate(predatorFront[slotIndex] * math.rcp(capacity));
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

            if (!TryResolveAupFromRuntimeOrigin(worldPosition, out AbsoluteUniversePosition positionAup))
                return false;

            double3 absolutePosition = positionAup.ToAbsoluteDouble3();
            if (!math.all(math.isfinite(absolutePosition)))
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!TryResolveMacroEcosystemVaultSnapshot(
                    vault,
                    out NativeArray<EcosystemSectorDTO>.ReadOnly sectors,
                    out NativeArray<MacroEcosystemSectorIndexRecord>.ReadOnly entries,
                    out NativeArray<MacroEcosystemTuningVaultRecord>.ReadOnly tuning))
                return false;

            MacroEcosystemTuningVaultRecord tune = tuning[0];
            if (!IsMacroEcosystemSnapshotReadable(tune))
                return false;

            double invSectorSize = 1.0 / MacroEcosystemVaultContract.SectorSizeMeters;
            long sectorX = (long)math.floor(absolutePosition.x * invSectorSize);
            long sectorZ = (long)math.floor(absolutePosition.z * invSectorSize);
            ulong hash = MacroEcosystemVaultContract.ComputeSectorHash(sectorX, 0L, sectorZ);
            if (!TryResolveMacroEcosystemSectorIndex(entries, hash, out int index) ||
                (uint)index >= (uint)sectors.Length)
            {
                return false;
            }

            EcosystemSectorDTO sector = sectors[index];
            MacroEcosystemTuningVaultRecord postReadTune = tuning[0];
            if (!IsMacroEcosystemSnapshotReadable(postReadTune) ||
                !MacroEcosystemTuningMatchesForBiomassRead(tune, postReadTune))
            {
                return false;
            }

            float preyCapacity = math.max(1f, math.select(
                MacroEcosystemVaultContract.DefaultCarryingCapacityPrey,
                tune.CarryingCapacityPrey,
                math.isfinite(tune.CarryingCapacityPrey) & tune.CarryingCapacityPrey > 0f));
            float predatorCapacity = math.max(1f, math.select(
                MacroEcosystemVaultContract.DefaultCarryingCapacityPredator,
                tune.CarryingCapacityPredator,
                math.isfinite(tune.CarryingCapacityPredator) & tune.CarryingCapacityPredator > 0f));

            float sectorCapacity = math.max(1f, math.select(preyCapacity + predatorCapacity, sector.CarryingCapacity, math.isfinite(sector.CarryingCapacity) & sector.CarryingCapacity > 0f));
            preyBiomass01 = math.saturate(sector.PreyBiomass * math.rcp(sectorCapacity));
            predatorBiomass01 = math.saturate(sector.PredatorBiomass * math.rcp(sectorCapacity));
            float defaultCapacity = MacroEcosystemVaultContract.DefaultCarryingCapacityPrey + MacroEcosystemVaultContract.DefaultCarryingCapacityPredator;
            carryingCapacity01 = math.saturate(sectorCapacity * math.rcp(math.max(1f, defaultCapacity)));
            return true;
        }

        private static bool IsMacroEcosystemSnapshotReadable(MacroEcosystemTuningVaultRecord tune)
        {
            return (tune.Flags & MacroEcosystemVaultContract.TuningFlagSnapshotWriteInFlight) == 0u;
        }

        private static bool MacroEcosystemTuningMatchesForBiomassRead(
            MacroEcosystemTuningVaultRecord before,
            MacroEcosystemTuningVaultRecord after)
        {
            return before.Flags == after.Flags &&
                   before.StateHash == after.StateHash &&
                   before.CarryingCapacityPrey == after.CarryingCapacityPrey &&
                   before.CarryingCapacityPredator == after.CarryingCapacityPredator;
        }

        private static bool TryResolveMacroEcosystemSectorIndex(
            NativeArray<MacroEcosystemSectorIndexRecord>.ReadOnly entries,
            ulong sectorHash,
            out int sectorIndex)
        {
            sectorIndex = -1;
            if (!entries.IsCreated || entries.Length <= 0 || sectorHash == 0UL)
                return false;

            int slot = MacroEcosystemVaultContract.ResolveOpenAddressSlot(sectorHash, entries.Length);
            for (int probe = 0; probe < entries.Length; probe++)
            {
                MacroEcosystemSectorIndexRecord entry = entries[slot];
                if (entry.Occupied == 0u)
                    return false;

                if (entry.SectorHash == sectorHash)
                {
                    sectorIndex = entry.Slot;
                    return sectorIndex >= 0 && sectorIndex < MacroEcosystemVaultContract.SectorCapacity;
                }

                slot++;
                if (slot >= entries.Length)
                    slot = 0;
            }

            return false;
        }

        private bool TryResolveMacroEcosystemVaultSnapshot(
            IDataVault vault,
            out NativeArray<EcosystemSectorDTO>.ReadOnly sectors,
            out NativeArray<MacroEcosystemSectorIndexRecord>.ReadOnly entries,
            out NativeArray<MacroEcosystemTuningVaultRecord>.ReadOnly tuning)
        {
            sectors = default;
            entries = default;
            tuning = default;

            if (vault == null)
                return false;

            if (!TryReadMacroEcosystemVaultBuffer(
                    vault,
                    in _macroSectorSnapshotHandle,
                    BufferID.ShinobuMacroEcosystemSectorFront,
                    0,
                    out sectors))
            {
                return false;
            }

            if (!TryReadMacroEcosystemVaultBuffer(
                    vault,
                    in _macroSectorIndexHandle,
                    BufferID.ShinobuMacroEcosystemIndexEntries,
                    0,
                    out entries))
            {
                return false;
            }

            if (!TryReadMacroEcosystemVaultBuffer(
                    vault,
                    in _macroTuningHandle,
                    BufferID.ShinobuMacroEcosystemTuning,
                    1,
                    out tuning))
            {
                return false;
            }

            return sectors.IsCreated && entries.IsCreated && tuning.IsCreated && tuning.Length > 0;
        }

        private void RefreshMacroEcosystemVaultHandlesCold(IDataVault vault)
        {
            if (vault == null || vault.IsCompactionFenceActive)
                return;

            RefreshMacroEcosystemVaultHandleCold(vault, BufferID.ShinobuMacroEcosystemSectorFront, ref _macroSectorSnapshotHandle);
            RefreshMacroEcosystemVaultHandleCold(vault, BufferID.ShinobuMacroEcosystemIndexEntries, ref _macroSectorIndexHandle);
            RefreshMacroEcosystemVaultHandleCold(vault, BufferID.ShinobuMacroEcosystemTuning, ref _macroTuningHandle);
        }

        private static bool RefreshMacroEcosystemVaultHandleCold<T>(
            IDataVault vault,
            BufferID bufferId,
            ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault == null ||
                bufferId == BufferID.Unknown ||
                vault.IsCompactionFenceActive)
            {
                return false;
            }

            if (IsMacroEcosystemVaultHandle(in handle, bufferId) &&
                vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly existing) &&
                existing.IsCreated)
            {
                return true;
            }

            if (!vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> refreshed))
                return false;

            handle = refreshed;
            return IsMacroEcosystemVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly buffer) &&
                   buffer.IsCreated;
        }

        private static bool TryReadMacroEcosystemVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int minimumLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                bufferId == BufferID.Unknown ||
                minimumLength < 0 ||
                vault.IsCompactionFenceActive ||
                !IsMacroEcosystemVaultHandle(in handle, bufferId) ||
                !vault.TryReadOnlyHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < minimumLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsMacroEcosystemVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)SystemID.AIEcology &&
                   handle.Generation != 0u;
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
            float safeX = request.RuntimePosition.x == 0f ? 0f : request.RuntimePosition.x;
            float safeY = request.RuntimePosition.y == 0f ? 0f : request.RuntimePosition.y;
            float safeZ = request.RuntimePosition.z == 0f ? 0f : request.RuntimePosition.z;
            int3 cell = new int3(
                (int)math.floor(safeX * InvBiomassMacroCellSizeMeters),
                (int)math.floor(safeY * InvBiomassMacroCellSizeMeters),
                (int)math.floor(safeZ * InvBiomassMacroCellSizeMeters));
            uint hash = math.hash(new int4(cell, request.SpeciesId));
            hash ^= (uint)request.Slot * 747796405u;
            return hash == 0u ? 1u : hash;
        }

        private static bool IsFiniteRuntimePosition(Vector3 runtimePosition)
        {
            return math.all(math.isfinite(new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z)));
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFiniteRuntimePosition(runtimePosition))
                return false;

            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (!math.all(math.isfinite(origin)))
                return false;

            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromAbsolutePosition(origin);
            double safeX = runtimePosition.x == 0f ? 0d : (double)runtimePosition.x;
            double safeY = runtimePosition.y == 0f ? 0d : (double)runtimePosition.y;
            double safeZ = runtimePosition.z == 0f ? 0d : (double)runtimePosition.z;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(safeX, safeY, safeZ));
            return positionAup.IsFinite();
        }

        private static float ReadDispatcherTimeSeconds()
        {
            SystemDispatcher dispatcher = SystemDispatcher.ActiveRuntimeInstance;
            double seconds = dispatcher != null ? dispatcher.DilatedTimeSeconds : 0d;
            if (!math.isfinite(seconds) || seconds <= 0d)
                return 0f;

            return seconds > float.MaxValue ? float.MaxValue : (float)seconds;
        }

        private static uint ReadDispatcherFrameId()
        {
            uint frame = TimeSliceScheduler.CurrentFrameId;
            return frame != 0u ? frame : 1u;
        }

        private static int ReadDispatcherFrameInt()
        {
            return unchecked((int)ReadDispatcherFrameId());
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

            IHazardZoneReadModel hazardZoneManager = _cachedHazardZones;
            if (hazardZoneManager != null)
            {
                AbsoluteUniversePosition runtimeAup = default;
                if (RuntimeOriginRoute.TryRuntimePositionToAup(runtimePosition, ref runtimeAup))
                {
                    float hazardToxicity01 = hazardZoneManager.GetHazardIntensity(in runtimeAup, HazardType.Toxicity);
                    invalidScalar |= !math.isfinite(hazardToxicity01);
                    toxicity01 = math.max(toxicity01, SanitizeMutationScalar01(hazardToxicity01));
                }
            }

            ResourceDistributionDirector resourceDistribution = _cachedResourceDistribution;
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
            int frame = ReadDispatcherFrameInt();
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
            if (!TryResolveAupFromRuntimeOrigin(request.RuntimePosition, out AbsoluteUniversePosition positionAup))
                return;

            FaunaStateChangedSignal signal = new FaunaStateChangedSignal
            {
                PositionAup = positionAup,
                SpeciesHash = request.StableEntityHash ^ (uint)request.SpeciesId,
                StateFlags = request.ResultFlags,
                Frame = ReadDispatcherFrameId(),
                Slot = request.Slot,
                StateKind = FaunaStateChangedSignalKinds.Mutated,
                Flags = FaunaStateChangedSignalFlags.StateActive
            };
            SignalBus<FaunaStateChangedSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
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

            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition positionAup))
                return;

            FaunaStateChangedSignal signal = new FaunaStateChangedSignal
            {
                PositionAup = positionAup,
                SpeciesHash = FaunaGenomeSaveHeaderMarker,
                StateFlags = resultFlags,
                Frame = ReadDispatcherFrameId(),
                Slot = 0,
                StateKind = FaunaStateChangedSignalKinds.Mutated,
                Flags = FaunaStateChangedSignalFlags.StateActive
            };
            SignalBus<FaunaStateChangedSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
        }

        /// <inheritdoc />
        public bool TryGetGlobalBiomassAudit(out EcosystemBiomassAuditSample sample)
        {
            sample = default;
            if (!IsInitialized ||
                HasPendingSimulationJob() ||
                !_preyBiomassFront.TryResolveReadOnly(out NativeArray<float>.ReadOnly preyFront) ||
                !_predatorBiomassFront.TryResolveReadOnly(out NativeArray<float>.ReadOnly predatorFront))
            {
                return false;
            }

            bool hasCapacity = _biomassCarryingCapacity.TryResolveReadOnly(out NativeArray<float>.ReadOnly carryingCapacity);
            int count = math.min(_activeBiomassCellCount, math.min(preyFront.Length, predatorFront.Length));
            if (count <= 0)
                return false;

            float preySum = 0f;
            float predatorSum = 0f;
            float capacitySum = 0f;
            uint flags = 0u;
            for (int i = 0; i < count; i++)
            {
                float prey = preyFront[i];
                float predator = predatorFront[i];
                float capacity = hasCapacity && i < carryingCapacity.Length
                    ? carryingCapacity[i]
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
                Sequence = ReadDispatcherFrameId(),
                Flags = flags
            };
            return sample.IsFinite();
        }

        /// <inheritdoc />
        public bool TryCopyMacroSwarms(NativeArray<MacroSwarm> destination, out int copiedCount)
        {
            copiedCount = 0;
            if (!destination.IsCreated ||
                !_macroSwarms.TryResolveReadOnly(out NativeArray<MacroSwarm>.ReadOnly macroSwarms) ||
                _activeMacroSwarmCount <= 0 ||
                _macroSwarmTravelScheduled)
            {
                return false;
            }

            copiedCount = math.min(destination.Length, math.min(_activeMacroSwarmCount, macroSwarms.Length));
            for (int i = 0; i < copiedCount; i++)
                destination[i] = macroSwarms[i];
            return copiedCount > 0;
        }

        /// <inheritdoc />
        public bool TryCopyMacroSwarmRadarPings(NativeArray<float4> destination, float3 probeOrigin, float radiusMeters, out int copiedCount)
        {
            copiedCount = 0;
            if (!destination.IsCreated ||
                !_macroSwarms.TryResolveReadOnly(out NativeArray<MacroSwarm>.ReadOnly macroSwarms) ||
                _activeMacroSwarmCount <= 0 ||
                _macroSwarmTravelScheduled)
            {
                return false;
            }

            float safeRadius = math.select(0f, math.max(0f, radiusMeters), math.isfinite(radiusMeters));
            float radiusSq = safeRadius * safeRadius;
            int swarmCount = math.min(_activeMacroSwarmCount, macroSwarms.Length);
            for (int i = 0; i < swarmCount && copiedCount < destination.Length; i++)
            {
                MacroSwarm swarm = macroSwarms[i];
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

            bool releaseMacroLock = false;
            if (!_macroSwarmTravelJobLocksHeld)
            {
                if (!TryLockMacroSwarmTravelJobBuffers())
                    return false;
                releaseMacroLock = true;
            }

            try
            {
                IDataVault vault = ResolveDataVault();
                if (vault == null || !vault.TryOpenMacroDatabasePayload(sectorHash, out MacroDatabasePayloadHandle handle))
                    return false;

                int stride = UnsafeUtility.SizeOf<MacroSwarm>();
                if (handle.ByteLength < stride || !_macroHydrationScratch.IsCreated)
                {
                    PushMacroSwarmBlackBox(MacroSwarmBlackBoxFlagInvalid);
                    return false;
                }

                NativeArray<MacroSwarm> scratch = _macroHydrationScratch.Resolve();
                if (!scratch.IsCreated || scratch.Length <= 0)
                {
                    PushMacroSwarmBlackBox(MacroSwarmBlackBoxFlagInvalid);
                    return false;
                }

                int cap = ResolveMacroSwarmActiveCap();
                int invalid = 0;
                int overflow = 0;
                int sourceOffset = 0;
                int scratchBytes = scratch.Length * stride;
                while (sourceOffset + stride <= handle.ByteLength)
                {
                    if (_activeMacroSwarmCount >= cap)
                    {
                        overflow++;
                        break;
                    }

                    int remainingBytes = handle.ByteLength - sourceOffset;
                    int copyCapacity = remainingBytes < scratchBytes ? remainingBytes : scratchBytes;
                    if (!vault.TryCopyMacroDatabasePayload(
                            sectorHash,
                            sourceOffset,
                            scratch,
                            copyCapacity,
                            out int bytesCopied,
                            out handle) ||
                        bytesCopied < stride)
                    {
                        invalid++;
                        break;
                    }

                    int copiedSwarmCount = bytesCopied / stride;
                    for (int i = 0; i < copiedSwarmCount; i++)
                    {
                        if (_activeMacroSwarmCount >= cap)
                        {
                            overflow++;
                            break;
                        }

                        MacroSwarm swarm = scratch[i];
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

                    sourceOffset += copiedSwarmCount * stride;
                    if (copiedSwarmCount == 0 || _activeMacroSwarmCount >= cap)
                        break;
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
            finally
            {
                if (releaseMacroLock)
                    UnlockMacroSwarmTravelJobBuffers();
            }
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

            if (!TryLockMacroSwarmTravelJobBuffers())
                return false;

            try
            {
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
            finally
            {
                UnlockMacroSwarmTravelJobBuffers();
            }
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

            if (!TryLockMacroSwarmTravelJobBuffers())
                return false;

            try
            {
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
            finally
            {
                UnlockMacroSwarmTravelJobBuffers();
            }
        }

        private IDataVault ResolveDataVault()
        {
            return _dataVault;
        }

        private IDataVault ResolveDataVaultCold()
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

            if (!TryQuantizeSector(worldPosition, out int2 sectorCoord))
                return false;

            if (!_sectorIndexEntries.TryResolveReadOnly(out NativeArray<EcosystemIndexEntry>.ReadOnly sectorIndexEntries) ||
                !_sectorFrontStates.TryResolveReadOnly(out NativeArray<SectorPopulationState>.ReadOnly sectorFrontStates) ||
                !TryFindIndexEntry(sectorIndexEntries, PackSectorKey(sectorCoord), out int slotIndex) ||
                slotIndex < 0 ||
                slotIndex >= _activeSectorCount ||
                (uint)slotIndex >= (uint)sectorFrontStates.Length)
            {
                return false;
            }

            SectorPopulationState state = sectorFrontStates[slotIndex];
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
                IFaunaSpatialContact faunaContact = hit.Owner as IFaunaSpatialContact;
                if (faunaContact == null || faunaContact.IsDead || !faunaContact.IsApexPredatorContact)
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

            EnvironmentalStrainManager.Instance?.AccumulatePredationStrain(worldPosition, preyConsumed);
            QueueOrApplyBiomassImpact(worldPosition, BiomassImpactKindPredation, preyConsumed * math.rcp(math.max(1f, maxPreyPopulation)));
            if (HasPendingSimulationJob())
                return;

            if (!TryQuantizeSector(worldPosition, out int2 sectorCoord))
                return;

            if (!TryLockSectorSolveJobBuffers())
                return;

            try
            {
                int slotIndex = ResolveOrCreateSectorSlot(sectorCoord, seedWithBaseline: true);
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
            finally
            {
                UnlockSectorSolveJobBuffers();
            }
        }

        /// <summary>
        /// Applies one herbivore flora-consumption event at the supplied world position and mirrors it into the sector prey-pressure solve.
        /// </summary>
        internal bool TryReportFloraGrazing(Vector3 worldPosition, float searchRadiusMeters = DefaultFloraGrazingSearchRadiusMeters)
        {
            if (!IsInitialized)
                return false;

            TryResolveDestructibleOrganicManager(out DestructibleOrganicManager organicManager);
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

            if (!TryQuantizeSector(worldPosition, out int2 sectorCoord))
                return;

            if (!TryLockSectorSolveJobBuffers())
                return;

            try
            {
                int slotIndex = ResolveOrCreateSectorSlot(sectorCoord, seedWithBaseline: true);
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
            finally
            {
                UnlockSectorSolveJobBuffers();
            }
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

        private void RefreshRuntimeReferences()
        {
            if (_cachedMapMagicBridge == null || !_cachedMapMagicBridge.isActiveAndEnabled)
            {
                _cachedMapMagicBridge = null;
                WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref _cachedMapMagicBridge);
            }
            WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _cachedVegetationBridge);
            if (_cachedPersistentWorldRegistry == null)
                _cachedPersistentWorldRegistry = PersistentWorldRegistry.Instance;
            WorldRuntimeReferenceUtility.TryResolveSargassumMicroFaunaBoids(ref _cachedSargassumMicroFauna);
        }

        private void CacheColdRegistryReferences()
        {
            _dataVault = GlobalRegistry.DataVault;
            RefreshRuntimeReferences();
            if (_cachedPlayerContext == null)
                _cachedPlayerContext = GlobalRegistry.Player;
            if (_cachedHazardZones == null)
                _cachedHazardZones = GlobalRegistry.HazardZoneReadModel;
            if (_cachedResourceDistribution == null)
                _cachedResourceDistribution = GlobalRegistry.ResourceDistribution;
            if (_cachedOceanKinematicsService == null)
                _cachedOceanKinematicsService = GlobalRegistry.OceanKinematics;
            IAmbientBiotaService ambientBiota = GlobalRegistry.AmbientBiota;
            if (ambientBiota != null)
                _cachedAmbientBiota = ambientBiota;
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
            TryResolveDestructibleOrganicManager(out DestructibleOrganicManager organicManager);
            return organicManager != null
                ? organicManager.ResolveCorpseSpawnInfluence01(worldPosition, radiusMeters)
                : 0f;
        }

        private float ResolveCombinedCorpseSpawnInfluence01(Vector3 worldPosition, float radiusMeters)
        {
            float liveCorpseInfluence01 = ResolveCorpseSpawnInfluence01(worldPosition, radiusMeters);
            PersistentWorldRegistry registry = _cachedPersistentWorldRegistry;
            float persistentWhaleFallInfluence01 = registry != null
                ? registry.UpdateWhaleFallSpawnInfluence01(worldPosition, ReadDispatcherTimeSeconds(), radiusMeters)
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
            HectonDirectorAI director = null;
            HectonDirectorAI.TryResolveActiveRuntime(ref director);
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
                    TryPushHostilityNotification("BIOME HOSTILITY: EXTREME. THE ABYSS HATES YOU.".AsSpan(), tier);
                    break;

                case 2:
                    TryPushHostilityNotification("BIOME HOSTILITY: ELEVATED. PREDATOR PEAK EXTENDED.".AsSpan(), tier);
                    break;

                case 1:
                    TryPushHostilityNotification("BIOME HOSTILITY: RISING.".AsSpan(), tier);
                    break;
            }
        }

        private void TryPushHostilityNotification(ReadOnlySpan<char> message, int tier)
        {
            bool pushed;
            switch (tier)
            {
                case 3:
                    pushed = NotificationEvents.TryPushCritical(message);
                    break;
                case 2:
                    pushed = NotificationEvents.TryPushWarning(message);
                    break;
                default:
                    pushed = NotificationEvents.TryPushInfo(message);
                    break;
            }

            if (pushed)
                return;

            ReportHostilityNotificationMiss(tier);
        }

        private void ReportHostilityNotificationMiss(int tier)
        {
            _hostilityNotificationMissCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _HostilityNotificationMissWarningHash,
                _EcosystemDirectorContextHash ^ _HostilityNotificationContextHash ^ unchecked((uint)tier),
                math.max(1, _hostilityNotificationMissCount));
        }

        private void ClearHostilityNotificationDiagnostics()
        {
            _hostilityNotificationMissCount = 0;
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

            IDataVault vault = ResolveDataVaultCold();
            if (vault == null)
                return;

            RefreshMacroEcosystemVaultHandlesCold(vault);

            _sectorFrontStates = VaultBufferView<SectorPopulationState>.Create(vault, vault.EnsureGenerationHandle<SectorPopulationState>(BufferID.EcosystemSectorFrontStates, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _sectorBackStates = VaultBufferView<SectorPopulationState>.Create(vault, vault.EnsureGenerationHandle<SectorPopulationState>(BufferID.EcosystemSectorBackStates, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _preyFrontCounts = VaultBufferView<int>.Create(vault, vault.EnsureGenerationHandle<int>(BufferID.EcosystemPreyFrontCounts, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _preyBackCounts = VaultBufferView<int>.Create(vault, vault.EnsureGenerationHandle<int>(BufferID.EcosystemPreyBackCounts, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _predatorFrontCounts = VaultBufferView<int>.Create(vault, vault.EnsureGenerationHandle<int>(BufferID.EcosystemPredatorFrontCounts, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _predatorBackCounts = VaultBufferView<int>.Create(vault, vault.EnsureGenerationHandle<int>(BufferID.EcosystemPredatorBackCounts, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _preyBiomassFront = VaultBufferView<float>.Create(vault, vault.EnsureGenerationHandle<float>(BufferID.EcosystemPreyBiomassFront, maxTrackedBiomassCells, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _preyBiomassBack = VaultBufferView<float>.Create(vault, vault.EnsureGenerationHandle<float>(BufferID.EcosystemPreyBiomassBack, maxTrackedBiomassCells, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _predatorBiomassFront = VaultBufferView<float>.Create(vault, vault.EnsureGenerationHandle<float>(BufferID.EcosystemPredatorBiomassFront, maxTrackedBiomassCells, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _predatorBiomassBack = VaultBufferView<float>.Create(vault, vault.EnsureGenerationHandle<float>(BufferID.EcosystemPredatorBiomassBack, maxTrackedBiomassCells, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _biomassCarryingCapacity = VaultBufferView<float>.Create(vault, vault.EnsureGenerationHandle<float>(BufferID.EcosystemBiomassCarryingCapacity, maxTrackedBiomassCells, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _biomassSumScratch = VaultBufferView<float>.Create(vault, vault.EnsureGenerationHandle<float>(BufferID.EcosystemBiomassSumScratch, maxTrackedBiomassCells, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _biomassMacroCellCoords = VaultBufferView<int2>.Create(vault, vault.EnsureGenerationHandle<int2>(BufferID.EcosystemBiomassMacroCellCoords, maxTrackedBiomassCells, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _biomassCellFlags = VaultBufferView<byte>.Create(vault, vault.EnsureGenerationHandle<byte>(BufferID.EcosystemBiomassCellFlags, maxTrackedBiomassCells, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _biomassIndexEntries = VaultBufferView<EcosystemIndexEntry>.Create(vault, vault.EnsureGenerationHandle<EcosystemIndexEntry>(BufferID.EcosystemBiomassIndexEntries, ResolveVaultIndexCapacity(maxTrackedBiomassCells), SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _pendingBiomassImpacts = VaultBufferView<BiomassImpactEvent>.Create(vault, vault.EnsureGenerationHandle<BiomassImpactEvent>(BufferID.EcosystemPendingBiomassImpacts, BiomassImpactQueueCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _biomassBlackBox = VaultBufferView<BiomassTelemetryEntry>.Create(vault, vault.EnsureGenerationHandle<BiomassTelemetryEntry>(BufferID.EcosystemBiomassBlackBox, BiomassBlackBoxCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _macroSwarms = VaultBufferView<MacroSwarm>.Create(vault, vault.EnsureGenerationHandle<MacroSwarm>(BufferID.EcosystemMacroSwarms, MacroSwarmCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _macroSwarmArrivals = VaultBufferView<MacroSwarmArrival>.Create(vault, vault.EnsureGenerationHandle<MacroSwarmArrival>(BufferID.EcosystemMacroSwarmArrivals, MacroSwarmArrivalCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _macroSwarmCounters = VaultBufferView<int>.Create(vault, vault.EnsureGenerationHandle<int>(BufferID.EcosystemMacroSwarmCounters, 4, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _macroSwarmBlackBox = VaultBufferView<MacroSwarmTelemetryEntry>.Create(vault, vault.EnsureGenerationHandle<MacroSwarmTelemetryEntry>(BufferID.EcosystemMacroSwarmBlackBox, MacroSwarmBlackBoxCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _macroSwarmMutationRadiation = VaultBufferView<float>.Create(vault, vault.EnsureGenerationHandle<float>(BufferID.EcosystemMacroSwarmMutationRadiation, MacroSwarmCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _macroSwarmMutationToxicity = VaultBufferView<float>.Create(vault, vault.EnsureGenerationHandle<float>(BufferID.EcosystemMacroSwarmMutationToxicity, MacroSwarmCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _macroSwarmMutationBrine = VaultBufferView<float>.Create(vault, vault.EnsureGenerationHandle<float>(BufferID.EcosystemMacroSwarmMutationBrine, MacroSwarmCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _macroSwarmMutationResults = VaultBufferView<byte>.Create(vault, vault.EnsureGenerationHandle<byte>(BufferID.EcosystemMacroSwarmMutationResults, MacroSwarmCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _faunaMutationBlackBox = VaultBufferView<FaunaMutationTelemetryEntry>.Create(vault, vault.EnsureGenerationHandle<FaunaMutationTelemetryEntry>(BufferID.EcosystemFaunaMutationBlackBox, FaunaMutationBlackBoxCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _faunaGeneticsTelemetry = VaultBufferView<GeneticsTelemetryEntry>.Create(vault, vault.EnsureGenerationHandle<GeneticsTelemetryEntry>(BufferID.EcosystemFaunaGeneticsTelemetry, FaunaGeneticsTelemetryCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _faunaGeneticsTuning = VaultBufferView<FaunaGeneticsTuningDTO>.Create(vault, vault.EnsureGenerationHandle<FaunaGeneticsTuningDTO>(BufferID.EcosystemFaunaGeneticsTuning, 1, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _faunaGeneticsProfiles = VaultBufferView<FaunaGeneticsProfileDTO>.Create(vault, vault.EnsureGenerationHandle<FaunaGeneticsProfileDTO>(BufferID.EcosystemFaunaGeneticsProfiles, FaunaGeneticsProfileCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _faunaGeneticsCsvScratch = VaultBufferView<byte>.Create(vault, vault.EnsureGenerationHandle<byte>(BufferID.EcosystemFaunaGeneticsCsvScratch, FaunaGeneticsCsvScratchBytes, SystemID.AIEcology, NativeArrayOptions.UninitializedMemory));
            _macroHydrationScratch = VaultBufferView<MacroSwarm>.Create(vault, vault.EnsureGenerationHandle<MacroSwarm>(BufferID.EcosystemMacroHydrationScratch, MacroSwarmSignalScratchCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _macroDehydrationScratch = VaultBufferView<MacroSwarm>.Create(vault, vault.EnsureGenerationHandle<MacroSwarm>(BufferID.EcosystemMacroDehydrationScratch, MacroSwarmSignalScratchCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _headlessEntities = new HeadlessEntitySoA
            {
                Positions = VaultBufferView<float3>.Create(vault, vault.EnsureGenerationHandle<float3>(BufferID.EcosystemHeadlessPositions, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory)),
                SpeciesID = VaultBufferView<byte>.Create(vault, vault.EnsureGenerationHandle<byte>(BufferID.EcosystemHeadlessSpeciesId, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory)),
                Hunger = VaultBufferView<byte>.Create(vault, vault.EnsureGenerationHandle<byte>(BufferID.EcosystemHeadlessHunger, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory)),
                SectorCoord = VaultBufferView<int2>.Create(vault, vault.EnsureGenerationHandle<int2>(BufferID.EcosystemHeadlessSectorCoord, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory)),
                SectorID = VaultBufferView<int>.Create(vault, vault.EnsureGenerationHandle<int>(BufferID.EcosystemHeadlessSectorId, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory)),
                FaunaGenomes = VaultBufferView<ulong>.Create(vault, vault.EnsureGenerationHandle<ulong>(BufferID.EcosystemHeadlessFaunaGenomes, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.UninitializedMemory)),
                MutationRadiation = VaultBufferView<float>.Create(vault, vault.EnsureGenerationHandle<float>(BufferID.EcosystemHeadlessMutationRadiation, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory)),
                MutationToxicity = VaultBufferView<float>.Create(vault, vault.EnsureGenerationHandle<float>(BufferID.EcosystemHeadlessMutationToxicity, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory)),
                MutationBrine = VaultBufferView<float>.Create(vault, vault.EnsureGenerationHandle<float>(BufferID.EcosystemHeadlessMutationBrine, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory)),
                MutationStableHashes = VaultBufferView<uint>.Create(vault, vault.EnsureGenerationHandle<uint>(BufferID.EcosystemHeadlessMutationStableHashes, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory)),
                MutationResults = VaultBufferView<byte>.Create(vault, vault.EnsureGenerationHandle<byte>(BufferID.EcosystemHeadlessMutationResults, maxTrackedSectors, SystemID.AIEcology, NativeArrayOptions.ClearMemory))
            };
            InitializeFaunaGeneticsVaultState();
            _sectorIndexEntries = VaultBufferView<EcosystemIndexEntry>.Create(vault, vault.EnsureGenerationHandle<EcosystemIndexEntry>(BufferID.EcosystemSectorIndexEntries, ResolveVaultIndexCapacity(maxTrackedSectors), SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _apexTerritorySamples = VaultBufferView<ApexTerritorySample>.Create(vault, vault.EnsureGenerationHandle<ApexTerritorySample>(BufferID.EcosystemApexTerritorySamples, ApexTerritoryOverlapCandidateCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _apexTerritoryOverlapResults = VaultBufferView<ApexTerritoryOverlapResult>.Create(vault, vault.EnsureGenerationHandle<ApexTerritoryOverlapResult>(BufferID.EcosystemApexTerritoryOverlapResults, ApexTerritoryOverlapCandidateCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _floraPredatorAupUpload = VaultBufferView<float4>.Create(vault, vault.EnsureGenerationHandle<float4>(BufferID.EcosystemFloraPredatorAupUpload, FloraPredatorAupBufferCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory));
            _saveSnapshotSectors = VaultBufferView<EcosystemSectorSaveRecord>.Create(vault, vault.EnsureGenerationHandle<EcosystemSectorSaveRecord>(
                BufferID.EcosystemSaveSnapshotSectors,
                maxTrackedSectors + maxTrackedBiomassCells + (maxTrackedSectors * 2) + (MacroSwarmCapacity * 4),
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory));
            _saveSnapshotBiomassRuns = VaultBufferView<EcosystemBiomassSaveRun>.Create(vault, vault.EnsureGenerationHandle<EcosystemBiomassSaveRun>(
                BufferID.EcosystemSaveSnapshotBiomassRuns,
                maxTrackedBiomassCells,
                SystemID.AIEcology,
                NativeArrayOptions.ClearMemory));
            // COLD ALLOC: IFaunaPredationTarget[16] - managed Apex retreat lookup paired with Burst overlap result indices - owner: EcosystemDirector
            _apexTerritoryTargets = new IFaunaPredationTarget[ApexTerritoryOverlapCandidateCapacity];
            // COLD ALLOC: float4[32] - flora predator AUP GPU upload snapshot copied under DataVault lock and consumed after release - owner: EcosystemDirector
            _floraPredatorAupUploadSnapshot = new float4[FloraPredatorAupBufferCapacity];
            ReleaseBuffer(ref _floraPredatorAupBufferA);
            ReleaseBuffer(ref _floraPredatorAupBufferB);
            _activeFloraPredatorAupBuffer = null;
            _floraPredatorAupBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(FloraPredatorAupBufferCapacity); // COLD ALLOC: GraphicsBuffer[32] - global flora predator AUP StructuredBuffer A - owner: EcosystemDirector
            _floraPredatorAupBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(FloraPredatorAupBufferCapacity); // COLD ALLOC: GraphicsBuffer[32] - global flora predator AUP StructuredBuffer B - owner: EcosystemDirector
            _activeFloraPredatorAupBuffer = _floraPredatorAupBufferA;
            _floraPredatorAupUploadIndex = 0;
            _floraPredatorAupGlobalsDirty = true;
            PublishFloraPredatorAupGlobalsImmediate(0);
            Shader.SetGlobalColor(_GlobalOceanPanicColorId, new Color(1f, 0.05f, 0.035f, 1f));
            Shader.SetGlobalFloat(_ApexInSectorId, 0f);
            Shader.SetGlobalFloat(_BiomassOvergrowthId, 0f);
            PublishGlobalOceanPanicImmediate(0f);
            _activeSectorCount = 0;
            _activeBiomassCellCount = 0;
            _pendingBiomassImpactCount = 0;
            _lastBiomassSignalDrainFrame = -1;
            _lastScannerWarningFrame = -1024;
            _biomassBlackBoxCursor = 0;
            _macroSwarmBlackBoxCursor = 0;
            _faunaMutationBlackBoxCursor = 0;
            _faunaGeneticsTelemetryCursor = 0;
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
            _lastFaunaGenomeBurstMicroseconds = 0f;
            _faunaGenomeMutationScheduleTimestamp = 0L;
            _lastFaunaGenomeCompiledCount = 0;
            _lastFaunaGeneticsDumpFrame = -1;
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
            _apexSpawnGateCachedCell = int3.zero;
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

        private void InitializeFaunaGeneticsVaultState()
        {
            if (_faunaGeneticsTuning.TryAcquireWriteLock(SystemID.AIEcology, out NativeArray<FaunaGeneticsTuningDTO> tuningRecords))
            {
                try
                {
                    if (tuningRecords.Length > 0)
                        tuningRecords[0] = FaunaGeneticsTuningDTO.CreateDefault();
                }
                finally
                {
                    _faunaGeneticsTuning.ReleaseWriteLock(SystemID.AIEcology);
                }
            }

            _lastFaunaGeneticsProfileCount = 0;
            _faunaGeneticsCsvLastWriteTicks = 0L;
#if UNITY_EDITOR
            TryReloadFaunaGeneticsProfilesFromCsv();
#endif
        }

#if UNITY_EDITOR
        private bool TryReloadFaunaGeneticsProfilesFromCsv()
        {
            string path = ResolveExistingProjectPath(
                FaunaGeneticsProfilesCsvPrimaryRelativePath,
                FaunaGeneticsProfilesCsvFallbackRelativePath);
            if (string.IsNullOrEmpty(path))
                return false;

            FileInfo fileInfo = new FileInfo(path);
            if (!fileInfo.Exists ||
                fileInfo.Length <= 0L)
            {
                return false;
            }

            long writeTicks = fileInfo.LastWriteTimeUtc.Ticks;
            if (writeTicks == _faunaGeneticsCsvLastWriteTicks)
                return false;

            if (!TryLoadFaunaGeneticsCsvScratchOneLock(path, fileInfo.Length, out int byteCount))
                return false;

            if (byteCount <= 0)
                return false;

            FaunaGeneticsTuningDTO tuning = FaunaGeneticsTuningDTO.CreateDefault();
            if (_faunaGeneticsTuning.TryResolveReadOnly(out NativeArray<FaunaGeneticsTuningDTO>.ReadOnly tuningRead) &&
                tuningRead.Length > 0 &&
                tuningRead[0].StateHash != 0u)
            {
                tuning = tuningRead[0];
            }

            if (!TryApplyFaunaGeneticsProfilesFromScratchOneLock(byteCount, ref tuning, out int updatedCount))
                return false;

            return TryCommitFaunaGeneticsTuningOneLock(tuning, updatedCount, writeTicks);
        }

        private bool TryLoadFaunaGeneticsCsvScratchOneLock(string path, long fileLength, out int byteCount)
        {
            byteCount = 0;
            if (fileLength <= 0L || fileLength > FaunaGeneticsCsvScratchBytes)
                return false;

            if (Interlocked.CompareExchange(ref _faunaGeneticsCsvImportScratchBusy, 1, 0) != 0)
                return false;

            try
            {
                int readLength = TryReadFileIntoScratch(path, _faunaGeneticsCsvImportScratch);
                if (readLength <= 0)
                    return false;

                if (!_faunaGeneticsCsvScratch.TryAcquireWriteLock(SystemID.AIEcology, out NativeArray<byte> csvScratch))
                    return false;

                try
                {
                    if (readLength > csvScratch.Length)
                        return false;

                    CopyFaunaGeneticsCsvScratchToVault(_faunaGeneticsCsvImportScratch, csvScratch, readLength);
                    byteCount = readLength;
                    return true;
                }
                finally
                {
                    _faunaGeneticsCsvScratch.ReleaseWriteLock(SystemID.AIEcology);
                }
            }
            finally
            {
                Volatile.Write(ref _faunaGeneticsCsvImportScratchBusy, 0);
            }
        }

        private bool TryApplyFaunaGeneticsProfilesFromScratchOneLock(
            int byteCount,
            ref FaunaGeneticsTuningDTO tuning,
            out int updatedCount)
        {
            updatedCount = 0;
            if (byteCount <= 0 ||
                !_faunaGeneticsCsvScratch.TryResolveReadOnly(out NativeArray<byte>.ReadOnly csvScratchRead) ||
                csvScratchRead.Length < byteCount)
            {
                return false;
            }

            bool profilesLocked = false;
            try
            {
                if (!_faunaGeneticsProfiles.TryAcquireWriteLock(SystemID.AIEcology, out NativeArray<FaunaGeneticsProfileDTO> profiles))
                    return false;

                profilesLocked = true;
                unsafe
                {
                    byte* bytes = (byte*)csvScratchRead.GetUnsafeReadOnlyPtr();
                    ReadOnlySpan<byte> csv = new ReadOnlySpan<byte>(bytes, byteCount);
                    if (!FaunaGeneticsProfileCsv.TryApplyProfiles(csv, profiles, ref tuning, out updatedCount))
                        return false;

                    return true;
                }
            }
            finally
            {
                if (profilesLocked)
                    _faunaGeneticsProfiles.ReleaseWriteLock(SystemID.AIEcology);
            }
        }

        private bool TryCommitFaunaGeneticsTuningOneLock(
            FaunaGeneticsTuningDTO tuning,
            int updatedCount,
            long writeTicks)
        {
            bool tuningLocked = false;
            try
            {
                if (!_faunaGeneticsTuning.TryAcquireWriteLock(SystemID.AIEcology, out NativeArray<FaunaGeneticsTuningDTO> tuningRecords))
                    return false;

                tuningLocked = true;
                if (tuningRecords.Length <= 0)
                    return false;

                tuningRecords[0] = tuning;
                _lastFaunaGeneticsProfileCount = updatedCount;
                _faunaGeneticsCsvLastWriteTicks = writeTicks;

                return true;
            }
            finally
            {
                if (tuningLocked)
                    _faunaGeneticsTuning.ReleaseWriteLock(SystemID.AIEcology);
            }
        }
#endif

        private static string ResolveExistingProjectPath(string primaryRelativePath, string fallbackRelativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string primary = Path.Combine(projectRoot, primaryRelativePath);
            if (File.Exists(primary))
                return primary;

            string fallback = Path.Combine(projectRoot, fallbackRelativePath);
            return File.Exists(fallback) ? fallback : string.Empty;
        }

        private static int TryReadFileIntoScratch(string path, byte[] bytes)
        {
            if (string.IsNullOrEmpty(path) || bytes == null || bytes.Length <= 0)
                return 0;

            try
            {
                using FileStream stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    bufferSize: 4096,
                    options: FileOptions.SequentialScan);

                if (stream.Length <= 0L || stream.Length > bytes.Length)
                    return 0;

                Span<byte> span = new Span<byte>(bytes, 0, (int)stream.Length);
                int totalRead = 0;
                while (totalRead < span.Length)
                {
                    int read = stream.Read(span.Slice(totalRead));
                    if (read <= 0)
                        break;

                    totalRead += read;
                }

                return totalRead == span.Length ? totalRead : 0;
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
        }

        private static unsafe void CopyFaunaGeneticsCsvScratchToVault(byte[] source, NativeArray<byte> target, int byteCount)
        {
            if (source == null || !target.IsCreated || byteCount <= 0 || source.Length < byteCount || target.Length < byteCount)
                return;

            fixed (byte* sourcePtr = source)
            {
                byte* targetPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr<byte>(target);
                UnsafeUtility.MemCpy(targetPtr, sourcePtr, byteCount);
            }
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
            ReleaseBuffer(ref _floraPredatorAupBufferA);
            ReleaseBuffer(ref _floraPredatorAupBufferB);
            _floraPredatorAupUploadSnapshot = null;
            _activeFloraPredatorAupBuffer = null;
            _floraPredatorAupUploadIndex = 0;
            Shader.SetGlobalInt(_PredatorAUPCountId, 0);
            Shader.SetGlobalFloat(_BiomassOvergrowthId, 0f);
            _lastPublishedFloraPredatorAupCount = 0;
            _floraPredatorAupGlobalsDirty = true;
            PublishApexPresenceFake(false);
            UnlockApexTerritoryOverlapJobBuffers();
            UnlockMacroSwarmTravelJobBuffers();
            UnlockGenomeMutationJobBuffers();
            UnlockSectorSolveJobBuffers();

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
            _faunaGeneticsTelemetry = default;
            _faunaGeneticsTuning = default;
            _faunaGeneticsProfiles = default;
            _faunaGeneticsCsvScratch = default;
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
            _floraPredatorAupUpload = default;
            _saveSnapshotSectors = default;
            _saveSnapshotBiomassRuns = default;
            _saveSnapshotSectorCount = 0;
            _saveSnapshotBiomassRunCount = 0;
            _apexTerritoryTargets = null;
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
            _faunaGeneticsTelemetryCursor = 0;
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
            _lastFaunaGenomeBurstMicroseconds = 0f;
            _faunaGenomeMutationScheduleTimestamp = 0L;
            _faunaGeneticsCsvLastWriteTicks = 0L;
            _lastFaunaGeneticsProfileCount = 0;
            _lastFaunaGenomeCompiledCount = 0;
            _lastFaunaGeneticsDumpFrame = -1;
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
            _apexSpawnGateCachedCell = int3.zero;
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
            ClearHostilityNotificationDiagnostics();
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

            _registeredSlowTickable = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
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

            _registeredFrostTickable = GlobalRegistry.TryRegisterFrostTickable(this, PriorityLayer.Environment);
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

            _registeredLateFrameTickable = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_registeredLateFrameTickable)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrameTickable = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.AmbientBiotaRuntime:
                    _cachedAmbientBiota = currentService as IAmbientBiotaService;
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    _dataVault = currentService as IDataVault;
                    break;
                case GlobalRegistryServiceSlot.MapMagicRuntime:
                case GlobalRegistryServiceSlot.TerrainProviderRuntime:
                    if (ReferenceEquals(_cachedMapMagicBridge, previousService))
                        _cachedMapMagicBridge = null;
                    _cachedMapMagicBridge = currentService as MapMagicBridge;
                    WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref _cachedMapMagicBridge);
                    break;
                case GlobalRegistryServiceSlot.MapMagicVegetationRuntime:
                    _cachedVegetationBridge = currentService as HectonMapMagicVegetationBridge;
                    WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref _cachedVegetationBridge);
                    break;
                case GlobalRegistryServiceSlot.PersistentWorldRegistry:
                    _cachedPersistentWorldRegistry = currentService as PersistentWorldRegistry;
                    break;
                case GlobalRegistryServiceSlot.SargassumMicroFaunaRuntime:
                    _cachedSargassumMicroFauna = currentService as SargassumMicroFaunaBoids;
                    WorldRuntimeReferenceUtility.TryResolveSargassumMicroFaunaBoids(ref _cachedSargassumMicroFauna);
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.HazardZoneRuntime:
                    _cachedHazardZones = currentService as IHazardZoneReadModel;
                    break;
                case GlobalRegistryServiceSlot.ResourceDistributionRuntime:
                    _cachedResourceDistribution = currentService as ResourceDistributionDirector;
                    break;
                case GlobalRegistryServiceSlot.OceanKinematics:
                    _cachedOceanKinematicsService = currentService as IHectonOceanKinematicsService;
                    break;
            }
        }

        private void EnsurePlayerSectorRegistered()
        {
            if (HasPendingSimulationJob())
                return;

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return;

            if (!TryLockSectorSolveJobBuffers())
                return;

            try
            {
                ResolveOrCreateSectorSlot(QuantizeSector(in playerAup), seedWithBaseline: true);
                int2 macroCell = QuantizeBiomassMacroCell(in playerAup);
                ResolveOrCreateBiomassCellSlot(macroCell, seedWithBaseline: true);
                ResolveOrCreateBiomassCellSlot(macroCell + new int2(1, 0), seedWithBaseline: false);
                ResolveOrCreateBiomassCellSlot(macroCell + new int2(-1, 0), seedWithBaseline: false);
                ResolveOrCreateBiomassCellSlot(macroCell + new int2(0, 1), seedWithBaseline: false);
                ResolveOrCreateBiomassCellSlot(macroCell + new int2(0, -1), seedWithBaseline: false);
            }
            finally
            {
                UnlockSectorSolveJobBuffers();
            }
        }

        private void EnsureMigrationNeighborSectorsRegistered()
        {
            if (HasPendingSimulationJob())
                return;

            int seedSectorCount = _activeSectorCount;
            if (!TryLockSectorSolveJobBuffers())
                return;

            try
            {
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
            finally
            {
                UnlockSectorSolveJobBuffers();
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

            PersistentWorldRegistry registry = _cachedPersistentWorldRegistry;
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
            float waterLevel = ResolveWaterSurfaceLevel();
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

        private float ResolveDepthMeters(Vector3 worldPosition)
        {
            return math.max(0f, ResolveWaterSurfaceLevel() - worldPosition.y);
        }

        private float ResolveWaterSurfaceLevel()
        {
            if (TryResolveOceanWaterSurfaceLevel(out float oceanWaterSurfaceLevel))
                return oceanWaterSurfaceLevel;

            MapMagicBridge bridge = _cachedMapMagicBridge;
            if (WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref bridge) &&
                TryResolveWaterSurfaceLevel(bridge.WaterSurfaceLevel, out float bridgeWaterSurfaceLevel))
            {
                _cachedMapMagicBridge = bridge;
                return bridgeWaterSurfaceLevel;
            }

            return DefaultWaterSurfaceLevelY;
        }

        private bool TryResolveOceanWaterSurfaceLevel(out float waterSurfaceLevel)
        {
            IHectonOceanKinematicsService oceanKinematicsService = _cachedOceanKinematicsService;
            IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null && oceanKinematicsService.IsInitialized
                ? oceanKinematicsService.ActiveProvider
                : null;
            if (oceanKinematics != null &&
                oceanKinematics.IsAvailable &&
                TryResolveOceanWaterSurfaceLevel(oceanKinematics.SeaLevel, out waterSurfaceLevel))
            {
                return true;
            }

            waterSurfaceLevel = DefaultWaterSurfaceLevelY;
            return false;
        }

        private static bool TryResolveOceanWaterSurfaceLevel(float candidateWaterSurfaceLevel, out float waterSurfaceLevel)
        {
            if (math.isfinite(candidateWaterSurfaceLevel) &&
                math.abs(candidateWaterSurfaceLevel) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                waterSurfaceLevel = candidateWaterSurfaceLevel;
                return true;
            }

            waterSurfaceLevel = DefaultWaterSurfaceLevelY;
            return false;
        }

        private static bool TryResolveWaterSurfaceLevel(float candidateWaterSurfaceLevel, out float waterSurfaceLevel)
        {
            if (math.isfinite(candidateWaterSurfaceLevel) &&
                math.abs(candidateWaterSurfaceLevel) > 0.0001f &&
                math.abs(candidateWaterSurfaceLevel) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                waterSurfaceLevel = candidateWaterSurfaceLevel;
                return true;
            }

            waterSurfaceLevel = DefaultWaterSurfaceLevelY;
            return false;
        }

        private void ScheduleSectorSolve()
        {
            if (_activeSectorCount <= 0 || _solveScheduled)
                return;

            if (!TryLockSectorSolveJobBuffers())
                return;

            bool keepLocksForScheduledJob = false;
            try
            {
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
                        DiffusionWeight = ResolveBiomassDiffusionWeight01()
                    };
                    _scheduledSolveHandle = biomassJob.Schedule(_activeBiomassCellCount, ResolveBiomassJobBatchSize(_activeBiomassCellCount), _scheduledSolveHandle);
                }
                _solveScheduled = true;
                keepLocksForScheduledJob = true;
            }
            finally
            {
                if (!keepLocksForScheduledJob)
                    UnlockSectorSolveJobBuffers();
            }
        }

        private bool HasPendingSimulationJob()
        {
            return _solveScheduled ||
                   _genomeMutationScheduled ||
                   _macroSwarmTravelScheduled ||
                   _apexTerritoryOverlapScheduled;
        }

        private bool TryLockSectorSolveJobBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null || _solveJobLocksHeld)
                return false;

            if (!vault.TryAcquireMutationGuard(SectorSolveMutationGuardMask))
                return false;

            bool keepGuard = false;
            try
            {
                if (!HasSectorSolveJobViews())
                    return false;

                _solveJobLocksHeld = true;
                _solveJobGuardVault = vault;
                keepGuard = true;
                return true;
            }
            finally
            {
                if (!keepGuard)
                    vault.ReleaseMutationGuard(SectorSolveMutationGuardMask);
            }
        }

        private void UnlockSectorSolveJobBuffers()
        {
            if (!_solveJobLocksHeld)
                return;

            IDataVault vault = _solveJobGuardVault;
            if (vault != null)
                vault.ReleaseMutationGuard(SectorSolveMutationGuardMask);
            _solveJobLocksHeld = false;
            _solveJobGuardVault = null;
        }

        private bool HasSectorSolveJobViews()
        {
            return IsAIEcologyBuffer(_sectorFrontStates, BufferID.EcosystemSectorFrontStates) &&
                   IsAIEcologyBuffer(_preyFrontCounts, BufferID.EcosystemPreyFrontCounts) &&
                   IsAIEcologyBuffer(_predatorFrontCounts, BufferID.EcosystemPredatorFrontCounts) &&
                   IsAIEcologyBuffer(_sectorFoodHeatmapR8, BufferID.EcosystemSectorFoodHeatmapR8) &&
                   IsAIEcologyBuffer(_sectorBackStates, BufferID.EcosystemSectorBackStates) &&
                   IsAIEcologyBuffer(_preyBackCounts, BufferID.EcosystemPreyBackCounts) &&
                   IsAIEcologyBuffer(_predatorBackCounts, BufferID.EcosystemPredatorBackCounts) &&
                   IsAIEcologyBuffer(_sectorIndexEntries, BufferID.EcosystemSectorIndexEntries) &&
                   IsAIEcologyBuffer(_headlessEntities.Positions, BufferID.EcosystemHeadlessPositions) &&
                   IsAIEcologyBuffer(_headlessEntities.SpeciesID, BufferID.EcosystemHeadlessSpeciesId) &&
                   IsAIEcologyBuffer(_headlessEntities.Hunger, BufferID.EcosystemHeadlessHunger) &&
                   IsAIEcologyBuffer(_headlessEntities.SectorCoord, BufferID.EcosystemHeadlessSectorCoord) &&
                   IsAIEcologyBuffer(_headlessEntities.SectorID, BufferID.EcosystemHeadlessSectorId) &&
                   IsAIEcologyBuffer(_headlessEntities.FaunaGenomes, BufferID.EcosystemHeadlessFaunaGenomes) &&
                   IsAIEcologyBuffer(_headlessEntities.MutationStableHashes, BufferID.EcosystemHeadlessMutationStableHashes) &&
                   IsAIEcologyBuffer(_preyBiomassFront, BufferID.EcosystemPreyBiomassFront) &&
                   IsAIEcologyBuffer(_predatorBiomassFront, BufferID.EcosystemPredatorBiomassFront) &&
                   IsAIEcologyBuffer(_biomassCarryingCapacity, BufferID.EcosystemBiomassCarryingCapacity) &&
                   IsAIEcologyBuffer(_biomassMacroCellCoords, BufferID.EcosystemBiomassMacroCellCoords) &&
                   IsAIEcologyBuffer(_biomassIndexEntries, BufferID.EcosystemBiomassIndexEntries) &&
                   IsAIEcologyBuffer(_preyBiomassBack, BufferID.EcosystemPreyBiomassBack) &&
                   IsAIEcologyBuffer(_predatorBiomassBack, BufferID.EcosystemPredatorBiomassBack) &&
                   IsAIEcologyBuffer(_biomassSumScratch, BufferID.EcosystemBiomassSumScratch) &&
                   IsAIEcologyBuffer(_biomassCellFlags, BufferID.EcosystemBiomassCellFlags) &&
                   IsAIEcologyBuffer(_biomassBlackBox, BufferID.EcosystemBiomassBlackBox);
        }

        private bool TryLockGenomeMutationJobBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null || _genomeMutationJobLocksHeld)
                return false;

            if (!vault.TryAcquireMutationGuard(GenomeMutationGuardMask))
                return false;

            bool keepGuard = false;
            try
            {
                if (!HasGenomeMutationJobViews())
                    return false;

                _genomeMutationJobLocksHeld = true;
                _genomeMutationJobGuardVault = vault;
                keepGuard = true;
                return true;
            }
            finally
            {
                if (!keepGuard)
                    vault.ReleaseMutationGuard(GenomeMutationGuardMask);
            }
        }

        private void UnlockGenomeMutationJobBuffers()
        {
            if (!_genomeMutationJobLocksHeld)
                return;

            IDataVault vault = _genomeMutationJobGuardVault;
            if (vault != null)
                vault.ReleaseMutationGuard(GenomeMutationGuardMask);
            _genomeMutationJobLocksHeld = false;
            _genomeMutationJobGuardVault = null;
        }

        private bool HasGenomeMutationJobViews()
        {
            return IsAIEcologyBuffer(_headlessEntities.FaunaGenomes, BufferID.EcosystemHeadlessFaunaGenomes) &&
                   IsAIEcologyBuffer(_headlessEntities.MutationRadiation, BufferID.EcosystemHeadlessMutationRadiation) &&
                   IsAIEcologyBuffer(_headlessEntities.MutationToxicity, BufferID.EcosystemHeadlessMutationToxicity) &&
                   IsAIEcologyBuffer(_headlessEntities.MutationBrine, BufferID.EcosystemHeadlessMutationBrine) &&
                   IsAIEcologyBuffer(_headlessEntities.MutationStableHashes, BufferID.EcosystemHeadlessMutationStableHashes) &&
                   IsAIEcologyBuffer(_headlessEntities.MutationResults, BufferID.EcosystemHeadlessMutationResults) &&
                   IsAIEcologyBuffer(_macroSwarms, BufferID.EcosystemMacroSwarms) &&
                   IsAIEcologyBuffer(_macroSwarmMutationRadiation, BufferID.EcosystemMacroSwarmMutationRadiation) &&
                   IsAIEcologyBuffer(_macroSwarmMutationToxicity, BufferID.EcosystemMacroSwarmMutationToxicity) &&
                   IsAIEcologyBuffer(_macroSwarmMutationBrine, BufferID.EcosystemMacroSwarmMutationBrine) &&
                   IsAIEcologyBuffer(_macroSwarmMutationResults, BufferID.EcosystemMacroSwarmMutationResults);
        }

        private bool TryLockMacroSwarmTravelJobBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null || _macroSwarmTravelJobLocksHeld)
                return false;

            if (!vault.TryAcquireMutationGuard(MacroSwarmTravelMutationGuardMask))
                return false;

            bool keepGuard = false;
            try
            {
                if (!HasMacroSwarmTravelJobViews())
                    return false;

                _macroSwarmTravelJobLocksHeld = true;
                _macroSwarmTravelJobGuardVault = vault;
                keepGuard = true;
                return true;
            }
            finally
            {
                if (!keepGuard)
                    vault.ReleaseMutationGuard(MacroSwarmTravelMutationGuardMask);
            }
        }

        private void UnlockMacroSwarmTravelJobBuffers()
        {
            if (!_macroSwarmTravelJobLocksHeld)
                return;

            IDataVault vault = _macroSwarmTravelJobGuardVault;
            if (vault != null)
                vault.ReleaseMutationGuard(MacroSwarmTravelMutationGuardMask);
            _macroSwarmTravelJobLocksHeld = false;
            _macroSwarmTravelJobGuardVault = null;
        }

        private bool TryAcquireBiomassImpactDrainGuard(out IDataVault guardVault)
        {
            guardVault = _dataVault;
            if (guardVault == null)
            {
                return false;
            }

            if (!guardVault.TryAcquireMutationGuard(BiomassImpactDrainMutationGuardMask))
            {
                guardVault = null;
                return false;
            }

            bool keepGuard = false;
            try
            {
                if (!HasMacroSwarmTravelJobViews() ||
                    !IsAIEcologyBuffer(_pendingBiomassImpacts, BufferID.EcosystemPendingBiomassImpacts))
                {
                    return false;
                }

                keepGuard = true;
                return true;
            }
            finally
            {
                if (!keepGuard)
                {
                    guardVault.ReleaseMutationGuard(BiomassImpactDrainMutationGuardMask);
                    guardVault = null;
                }
            }
        }

        private static void ReleaseBiomassImpactDrainGuard(IDataVault guardVault)
        {
            if (guardVault != null)
                guardVault.ReleaseMutationGuard(BiomassImpactDrainMutationGuardMask);
        }

        private bool HasMacroSwarmTravelJobViews()
        {
            return IsAIEcologyBuffer(_macroSwarms, BufferID.EcosystemMacroSwarms) &&
                   IsAIEcologyBuffer(_macroSwarmArrivals, BufferID.EcosystemMacroSwarmArrivals) &&
                   IsAIEcologyBuffer(_macroSwarmCounters, BufferID.EcosystemMacroSwarmCounters) &&
                   IsAIEcologyBuffer(_macroSwarmBlackBox, BufferID.EcosystemMacroSwarmBlackBox) &&
                   IsAIEcologyBuffer(_macroHydrationScratch, BufferID.EcosystemMacroHydrationScratch) &&
                   IsAIEcologyBuffer(_macroDehydrationScratch, BufferID.EcosystemMacroDehydrationScratch) &&
                   IsAIEcologyBuffer(_preyBiomassFront, BufferID.EcosystemPreyBiomassFront) &&
                   IsAIEcologyBuffer(_preyBiomassBack, BufferID.EcosystemPreyBiomassBack) &&
                   IsAIEcologyBuffer(_predatorBiomassFront, BufferID.EcosystemPredatorBiomassFront) &&
                   IsAIEcologyBuffer(_predatorBiomassBack, BufferID.EcosystemPredatorBiomassBack) &&
                   IsAIEcologyBuffer(_biomassCarryingCapacity, BufferID.EcosystemBiomassCarryingCapacity) &&
                   IsAIEcologyBuffer(_biomassSumScratch, BufferID.EcosystemBiomassSumScratch) &&
                   IsAIEcologyBuffer(_biomassMacroCellCoords, BufferID.EcosystemBiomassMacroCellCoords) &&
                   IsAIEcologyBuffer(_biomassIndexEntries, BufferID.EcosystemBiomassIndexEntries) &&
                   IsAIEcologyBuffer(_biomassCellFlags, BufferID.EcosystemBiomassCellFlags);
        }

        private bool TryLockApexTerritoryOverlapJobBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null || _apexTerritoryOverlapJobLocksHeld)
                return false;

            if (!vault.TryAcquireMutationGuard(ApexTerritoryOverlapMutationGuardMask))
                return false;

            bool keepGuard = false;
            try
            {
                if (!IsAIEcologyBuffer(_apexTerritorySamples, BufferID.EcosystemApexTerritorySamples) ||
                    !IsAIEcologyBuffer(_apexTerritoryOverlapResults, BufferID.EcosystemApexTerritoryOverlapResults))
                    return false;

                _apexTerritoryOverlapJobLocksHeld = true;
                _apexTerritoryOverlapJobGuardVault = vault;
                keepGuard = true;
                return true;
            }
            finally
            {
                if (!keepGuard)
                    vault.ReleaseMutationGuard(ApexTerritoryOverlapMutationGuardMask);
            }
        }

        private void UnlockApexTerritoryOverlapJobBuffers()
        {
            if (!_apexTerritoryOverlapJobLocksHeld)
                return;

            IDataVault vault = _apexTerritoryOverlapJobGuardVault;
            if (vault != null)
                vault.ReleaseMutationGuard(ApexTerritoryOverlapMutationGuardMask);
            _apexTerritoryOverlapJobLocksHeld = false;
            _apexTerritoryOverlapJobGuardVault = null;
        }

        private static ulong EcosystemVaultMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private static bool IsAIEcologyBuffer<T>(VaultBufferView<T> buffer, BufferID bufferId)
            where T : struct
        {
            return buffer.IsOwnedHandle(SystemID.AIEcology, bufferId) && buffer.IsCreated;
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

            bool applyPendingImpacts = false;
            try
            {
                VaultBufferView<SectorPopulationState> stateSwap = _sectorFrontStates;
                _sectorFrontStates = _sectorBackStates;
                _sectorBackStates = stateSwap;
                VaultBufferView<int> preySwap = _preyFrontCounts;
                _preyFrontCounts = _preyBackCounts;
                _preyBackCounts = preySwap;
                VaultBufferView<int> predatorSwap = _predatorFrontCounts;
                _predatorFrontCounts = _predatorBackCounts;
                _predatorBackCounts = predatorSwap;
                if (_activeBiomassCellCount > 0)
                {
                    VaultBufferView<float> preyBiomassSwap = _preyBiomassFront;
                    _preyBiomassFront = _preyBiomassBack;
                    _preyBiomassBack = preyBiomassSwap;
                    VaultBufferView<float> predatorBiomassSwap = _predatorBiomassFront;
                    _predatorBiomassFront = _predatorBiomassBack;
                    _predatorBiomassBack = predatorBiomassSwap;
                }
                _solveScheduled = false;
                applyPendingImpacts = true;
            }
            finally
            {
                UnlockSectorSolveJobBuffers();
            }

            if (!applyPendingImpacts)
                return;

            ApplyPendingBiomassImpacts();
            PublishBiomassTelemetryAndEvents();
            RefreshStarvationPressure();
            _populationSolvePendingHibernationSync = true;
            _debugHeadlessSectorCount = _activeSectorCount;
            _debugBiomassCellCount = _activeBiomassCellCount;
        }

        private void DrainSectorResidencySignalSnapshots()
        {
            int frame = ReadDispatcherFrameInt();
            if (_lastSectorResidencySignalDrainFrame == frame || HasPendingSimulationJob() || _macroSwarmTravelScheduled)
                return;

            if (!TryLockMacroSwarmTravelJobBuffers())
                return;

            _lastSectorResidencySignalDrainFrame = frame;

            try
            {
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
            finally
            {
                UnlockMacroSwarmTravelJobBuffers();
            }
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
            IAmbientBiotaService activeBiota = _cachedAmbientBiota;
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

            IAmbientBiotaService activeBiota = _cachedAmbientBiota;
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

            NativeArray<EcosystemIndexEntry> biomassIndexEntries = _biomassIndexEntries.Resolve();
            if (!TryFindIndexEntry(biomassIndexEntries, PackBiomassCellKey(sourceCell), out int sourceSlot) ||
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
            NativeArray<EcosystemIndexEntry> biomassIndexEntries = _biomassIndexEntries.Resolve();
            for (int i = 0; i < _activeMacroSwarmCount; i++)
            {
                MacroSwarm swarm = _macroSwarms[i];
                if (!TryFindIndexEntry(biomassIndexEntries, PackBiomassCellKey(swarm.SectorAup), out int slotIndex) ||
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
            if (_genomeMutationScheduled)
                return default;

            if (!TryLockGenomeMutationJobBuffers())
                return default;

            bool keepLocksForScheduledJob = false;
            try
            {
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

                _faunaGenomeMutationScheduleTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                _scheduledGenomeMutationHandle = dependency;
                _genomeMutationScheduled = true;
                keepLocksForScheduledJob = true;
                return dependency;
            }
            finally
            {
                if (!keepLocksForScheduledJob)
                    UnlockGenomeMutationJobBuffers();
            }
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
            if (count <= 0)
                return 0;

            float mutationWeight01 = math.max(MacroSwarmMinimumMutationCadenceWeight01, Smooth01(_macroSwarmQualityWeight01));
            count = math.clamp((int)math.ceil(count * mutationWeight01), 1, count);
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

            try
            {
                if (_faunaGenomeMutationScheduleTimestamp != 0L)
                {
                    long ticks = System.Diagnostics.Stopwatch.GetTimestamp() - _faunaGenomeMutationScheduleTimestamp;
                    if (ticks < 0L)
                        ticks = 0L;
                    double seconds = ticks / (double)System.Diagnostics.Stopwatch.Frequency;
                    _lastFaunaGenomeBurstMicroseconds = math.isfinite(seconds) ? (float)(seconds * 1000000d) : 0f;
                    _faunaGenomeMutationScheduleTimestamp = 0L;
                }
                else
                {
                    _lastFaunaGenomeBurstMicroseconds = 0f;
                }

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
                _lastFaunaGenomeCompiledCount = math.max(0, _scheduledHeadlessMutationCount + _scheduledMacroSwarmMutationCount);
                if (headlessMutated + macroMutated > 0)
                {
                    RecordGenomeMutation((byte)(flags & 0xFFu), maxRadiation, maxToxicity, maxBrine, headlessMutated, macroMutated);
                    PublishBatchFaunaMutatedSignal((byte)(flags & 0xFFu));
                }

                _scheduledHeadlessMutationCount = 0;
                _scheduledMacroSwarmMutationCount = 0;
            }
            finally
            {
                UnlockGenomeMutationJobBuffers();
            }
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

            if (!TryLockMacroSwarmTravelJobBuffers())
                return;

            bool keepLocksForScheduledJob = false;
            try
            {
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
                keepLocksForScheduledJob = true;
            }
            finally
            {
                if (!keepLocksForScheduledJob)
                    UnlockMacroSwarmTravelJobBuffers();
            }
        }

        private void CompleteScheduledMacroSwarmTravel(bool forceComplete)
        {
            if (!_macroSwarmTravelScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _macroSwarmTravelHandle, forceComplete))
                return;

            try
            {
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
            finally
            {
                UnlockMacroSwarmTravelJobBuffers();
            }
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
            float qualityWeight01 = ResolveGlobalQualityWeight01();
            float qualityCurve01 = Smooth01(qualityWeight01);
            _macroSwarmQualityWeight01 = qualityWeight01;
            _macroSwarmQualityTierProfileByte = EncodeMacroSwarmVisualQualityByte(qualityWeight01);
            _macroSwarmActiveCap = math.clamp(
                (int)math.round(math.lerp((float)MacroSwarmLowTierCap, (float)MacroSwarmCapacity, qualityCurve01)),
                MacroSwarmLowTierCap,
                MacroSwarmCapacity);
            _macroSwarmSpeedCellsPerSecond = MacroSwarmDefaultSpeedCellsPerSecond * math.lerp(0.5f, 1f, qualityCurve01);
        }

        private static byte EncodeMacroSwarmVisualQualityByte(float qualityWeight01)
        {
            return (byte)math.clamp((int)math.round(math.saturate(qualityWeight01) * 255f), 0, 255);
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(weight) ? weight : 1f);
        }

        private static float ResolveBiomassDiffusionWeight01()
        {
            return math.smoothstep(MacroSwarmDiffusionQualityStart01, 1f, ResolveGlobalQualityWeight01());
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
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
            SwarmDispersedSignal signal = new SwarmDispersedSignal
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
            };
            SignalBus<SwarmDispersedSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
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
                FrameIndex = ReadDispatcherFrameId(),
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
                FrameIndex = ReadDispatcherFrameId(),
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
            if (!_macroSwarmBlackBox.TryResolveReadOnly(out NativeArray<MacroSwarmTelemetryEntry>.ReadOnly macroSwarmBlackBox) ||
                macroSwarmBlackBox.Length <= 0)
                return;

            try
            {
                if (!TryWriteTelemetryRingDump(
                    MacroSwarmTelemetryDumpRelativePath,
                    MacroSwarmTelemetryDumpMagic,
                    macroSwarmBlackBox,
                    _macroSwarmBlackBoxCursor))
                {
                    GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)MacroSwarmSaveHeaderMarker));
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
                FrameIndex = ReadDispatcherFrameId(),
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

        private void PushFaunaGeneticsTelemetryFrame()
        {
            if (!_faunaGeneticsTelemetry.TryResolveReadOnly(out NativeArray<GeneticsTelemetryEntry>.ReadOnly telemetryReadOnly) ||
                telemetryReadOnly.Length <= 0)
            {
                return;
            }

            int invalidCount = 0;
            NativeArray<ulong>.ReadOnly headlessGenomes = default;
            NativeArray<MacroSwarm>.ReadOnly macroSwarms = default;
            bool headlessResolved = _headlessEntities.FaunaGenomes.TryResolveReadOnly(out headlessGenomes);
            bool macroResolved = _macroSwarms.TryResolveReadOnly(out macroSwarms);
            int headlessCount = headlessResolved ? math.min(_activeSectorCount, headlessGenomes.Length) : 0;
            int macroCount = macroResolved ? math.min(_activeMacroSwarmCount, macroSwarms.Length) : 0;
            int activeCount = 0;
            int extractionOps = 0;
            float hueSum = 0f;
            float sizeSum = 0f;
            float aggressionSum = 0f;
            float patternSum = 0f;
            uint stateHash = 2166136261u;
            uint patternLo = 0u;
            uint patternHi = 0u;

            for (int i = 0; i < headlessCount; i++)
                AccumulateGeneticsTelemetry(headlessGenomes[i], ref activeCount, ref invalidCount, ref extractionOps, ref hueSum, ref sizeSum, ref aggressionSum, ref patternSum, ref stateHash, ref patternLo, ref patternHi);

            for (int i = 0; i < macroCount; i++)
                AccumulateGeneticsTelemetry(macroSwarms[i].Genome, ref activeCount, ref invalidCount, ref extractionOps, ref hueSum, ref sizeSum, ref aggressionSum, ref patternSum, ref stateHash, ref patternLo, ref patternHi);

            float invCount = activeCount > 0 ? math.rcp((float)activeCount) : 0f;
            FaunaGeneticsTuningDTO tuning = default;
            NativeArray<FaunaGeneticsTuningDTO>.ReadOnly tuningReadOnly;
            if (_faunaGeneticsTuning.TryResolveReadOnly(out tuningReadOnly) &&
                tuningReadOnly.Length > 0)
            {
                tuning = tuningReadOnly[0];
            }

            int compiledGenomeCount = math.max(_lastFaunaGenomeCompiledCount, activeCount);
            uint frame = ReadDispatcherFrameId();
            GeneticsTelemetryEntry entry = new GeneticsTelemetryEntry
            {
                FrameIndex = frame,
                StateHash = stateHash == 0u ? 1u : stateHash,
                CompiledGenomeCount = compiledGenomeCount,
                ActiveGenomeCount = activeCount,
                ExtractionOperationCount = extractionOps,
                InvalidMaskCount = invalidCount,
                AverageHueShift01 = hueSum * invCount,
                AverageSize01 = sizeSum * invCount,
                AverageAggression01 = aggressionSum * invCount,
                AveragePattern01 = patternSum * invCount,
                BurstExecutionMicroseconds = _lastFaunaGenomeBurstMicroseconds,
                TuningStateHash = tuning.StateHash,
                PatternHistogramLo = patternLo,
                PatternHistogramHi = patternHi,
                Flags = (uint)math.select(0, 1, invalidCount > 0)
            };
            bool shouldDump = invalidCount > 0 ||
                              (compiledGenomeCount > 0 &&
                               _lastFaunaGenomeBurstMicroseconds > FaunaGeneticsTelemetryBudgetMicroseconds);

            if (!_faunaGeneticsTelemetry.TryAcquireWriteLock(SystemID.AIEcology, out NativeArray<GeneticsTelemetryEntry> geneticsTelemetry))
                return;

            try
            {
                if (geneticsTelemetry.Length <= 0)
                    return;

                int index = _faunaGeneticsTelemetryCursor % geneticsTelemetry.Length;
                geneticsTelemetry[index] = entry;
                _faunaGeneticsTelemetryCursor++;
            }
            finally
            {
                _faunaGeneticsTelemetry.ReleaseWriteLock(SystemID.AIEcology);
            }

            int frameInt = unchecked((int)frame);
            if (shouldDump && _lastFaunaGeneticsDumpFrame != frameInt)
            {
                _lastFaunaGeneticsDumpFrame = frameInt;
                DumpFaunaGeneticsTelemetry();
            }
        }

        private static void AccumulateGeneticsTelemetry(
            ulong mask,
            ref int activeCount,
            ref int invalidCount,
            ref int extractionOps,
            ref float hueSum,
            ref float sizeSum,
            ref float aggressionSum,
            ref float patternSum,
            ref uint stateHash,
            ref uint patternLo,
            ref uint patternHi)
        {
            if (mask == 0UL)
            {
                invalidCount++;
                return;
            }

            int size = FaunaGenome64.ExtractSizeByte(mask);
            int aggression = FaunaGenome64.ExtractAggressionByte(mask);
            int hue = FaunaGenome64.ExtractHueByte(mask);
            int pattern = FaunaGenome64.ExtractPatternIndex(mask);
            activeCount++;
            extractionOps += 4;
            hueSum += hue * (1f / 255f);
            sizeSum += size * (1f / 255f);
            aggressionSum += aggression * (1f / 255f);
            patternSum += pattern * (1f / 15f);
            stateHash = (stateHash ^ (uint)mask) * 16777619u;
            stateHash = (stateHash ^ (uint)(mask >> 32)) * 16777619u;
            AddPatternHistogram(pattern, ref patternLo, ref patternHi);
        }

        private static void AddPatternHistogram(int pattern, ref uint lo, ref uint hi)
        {
            pattern &= 15;
            if (pattern < 8)
            {
                int shift = pattern << 2;
                uint count = ((lo >> shift) & 0x0Fu) + 1u;
                if (count > 15u)
                    count = 15u;
                lo = (lo & ~(0x0Fu << shift)) | (count << shift);
                return;
            }

            int hiShift = (pattern - 8) << 2;
            uint hiCount = ((hi >> hiShift) & 0x0Fu) + 1u;
            if (hiCount > 15u)
                hiCount = 15u;
            hi = (hi & ~(0x0Fu << hiShift)) | (hiCount << hiShift);
        }

        private static unsafe bool TryWriteTelemetryRingDump<T>(
            string dumpPath,
            ulong magic,
            NativeArray<T>.ReadOnly entries,
            int cursor) where T : struct
        {
            if (string.IsNullOrEmpty(dumpPath) || !entries.IsCreated)
                return false;

            int entryCapacity = entries.Length;
            if (entryCapacity <= 0)
                return false;

            int entrySize = UnsafeUtility.SizeOf<T>();
            int entryCount = math.min(math.max(0, cursor), entryCapacity);
            int oldestIndex = entryCount == entryCapacity ? cursor % entryCapacity : 0;
            const int headerBytes = sizeof(ulong) + (sizeof(int) * 4);
            int payloadBytes = headerBytes + (entryCount * entrySize);
            NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                payloadBytes,
                nameof(EcosystemDirector),
                "EcosystemTelemetryRingDumpPayload");
            try
            {
                byte* payloadPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                UnsafeUtility.WriteArrayElement<ulong>(payloadPtr, 0, magic);
                UnsafeUtility.WriteArrayElement<int>(payloadPtr + sizeof(ulong), 0, entryCount);
                UnsafeUtility.WriteArrayElement<int>(payloadPtr + sizeof(ulong) + sizeof(int), 0, entrySize);
                UnsafeUtility.WriteArrayElement<int>(payloadPtr + sizeof(ulong) + (sizeof(int) * 2), 0, oldestIndex);
                UnsafeUtility.WriteArrayElement<int>(payloadPtr + sizeof(ulong) + (sizeof(int) * 3), 0, entryCapacity);

                CopyTelemetryRingEntries(payloadPtr + headerBytes, entries, entrySize, entryCount, oldestIndex);
                return NativeFaultDumpWriter.TryWriteAll(dumpPath, payload, payloadBytes);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(EcosystemDirector),
                    "EcosystemTelemetryRingDumpPayload");
            }
        }

        private static unsafe void CopyTelemetryRingEntries<T>(
            byte* destination,
            NativeArray<T>.ReadOnly entries,
            int entrySize,
            int entryCount,
            int oldestIndex) where T : struct
        {
            if (destination == null || !entries.IsCreated || entrySize <= 0 || entryCount <= 0)
                return;

            int entryCapacity = entries.Length;
            if (entryCapacity <= 0)
                return;

            for (int i = 0; i < entryCount; i++)
            {
                int entryIndex = oldestIndex + i;
                if (entryIndex >= entryCapacity)
                    entryIndex -= entryCapacity;

                T entry = entries[entryIndex];
                UnsafeUtility.CopyStructureToPtr(ref entry, destination + (i * entrySize));
            }
        }

        private unsafe void DumpFaunaGeneticsTelemetry()
        {
            if (!_faunaGeneticsTelemetry.TryResolveReadOnly(out NativeArray<GeneticsTelemetryEntry>.ReadOnly faunaGeneticsTelemetry) ||
                faunaGeneticsTelemetry.Length <= 0)
                return;

            try
            {
                if (!TryWriteTelemetryRingDump(
                    FaunaGeneticsTelemetryDumpRelativePath,
                    FaunaGeneticsTelemetryDumpMagic,
                    faunaGeneticsTelemetry,
                    _faunaGeneticsTelemetryCursor))
                {
                    GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)FaunaGenomeSaveHeaderMarker));
                }
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)FaunaGenomeSaveHeaderMarker));
            }
        }

        private unsafe void DumpFaunaMutationBlackBox()
        {
            if (!_faunaMutationBlackBox.TryResolveReadOnly(out NativeArray<FaunaMutationTelemetryEntry>.ReadOnly faunaMutationBlackBox) ||
                faunaMutationBlackBox.Length <= 0)
                return;

            try
            {
                if (!TryWriteTelemetryRingDump(
                    FaunaMutationTelemetryDumpRelativePath,
                    FaunaMutationTelemetryDumpMagic,
                    faunaMutationBlackBox,
                    _faunaMutationBlackBoxCursor))
                {
                    GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)_FaunaMutationTelemetryHash));
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
                _apexTerritoryTargets == null)
            {
                return;
            }

            int hitCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                queryOrigin,
                ApexTerritoryOverlapQueryRadiusMeters,
                SpatialTargetKind.Bioform,
                _apexTerritoryOverlapHits);

            if (hitCount <= 0)
                return;

            if (!TryLockApexTerritoryOverlapJobBuffers())
                return;

            int sampleCount = 0;
            bool keepLocksForScheduledJob = false;
            try
            {
                for (int hitIndex = 0; hitIndex < hitCount && sampleCount < ApexTerritoryOverlapCandidateCapacity; hitIndex++)
                {
                    SpatialQueryHit hit = _apexTerritoryOverlapHits[hitIndex];
                    IFaunaSpatialContact faunaContact = hit.Owner as IFaunaSpatialContact;
                    if (faunaContact == null || faunaContact.IsDead || !faunaContact.IsApexPredatorContact)
                        continue;

                    IFaunaPredationTarget retreatTarget = hit.Owner as IFaunaPredationTarget;
                    if (retreatTarget == null)
                        continue;

                    AbsoluteUniversePosition hitAup = hit.AbsolutePosition;
                    if (!hit.HasAbsolutePosition &&
                        !TryResolveAupFromRuntimeOrigin(hit.Position, out hitAup))
                    {
                        continue;
                    }

                    _apexTerritoryTargets[sampleCount] = retreatTarget;
                    _apexTerritorySamples[sampleCount] = new ApexTerritorySample
                    {
                        PositionAup = hitAup.ToAlignedBlit(),
                        Radius = faunaContact.ApexTerritoryRadiusMeters,
                        MassScore = faunaContact.ApexTerritoryMassScore,
                        BrainIndex = sampleCount,
                        Padding = 0
                    };
                    _apexTerritoryOverlapResults[sampleCount] = default;
                    sampleCount++;
                }

                if (sampleCount < 2)
                {
                    for (int i = 0; i < sampleCount; i++)
                        _apexTerritoryTargets[i] = null;
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
                keepLocksForScheduledJob = true;
            }
            finally
            {
                if (!keepLocksForScheduledJob)
                    UnlockApexTerritoryOverlapJobBuffers();
            }
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

            try
            {
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

                    IFaunaPredationTarget retreatTarget = _apexTerritoryTargets[result.RetreatBrainIndex];
                    if (retreatTarget == null || retreatTarget.IsDead)
                        continue;

                    ApexTerritorySample rivalSample = _apexTerritorySamples[result.RivalBrainIndex];
                    AbsoluteUniversePosition rivalAup = AbsoluteUniversePosition.FromAlignedBlit(in rivalSample.PositionAup);
                    retreatTarget.ForceApexRetreatFrom(ToVector3(rivalAup.ToRuntimeFloat3()));
                }

                for (int i = 0; i < count; i++)
                {
                    _apexTerritoryTargets[i] = null;
                    _apexTerritoryOverlapResults[i] = default;
                }

                _scheduledApexTerritoryOverlapHandle = default;
                _scheduledApexTerritoryOverlapCount = 0;
                _apexTerritoryOverlapScheduled = false;
            }
            finally
            {
                UnlockApexTerritoryOverlapJobBuffers();
            }
        }

        private void PublishFloraPredatorAupBuffer(Vector3 queryOrigin)
        {
            _pendingFloraPredatorAupQueryOrigin = queryOrigin;
            _floraPredatorAupRefreshDirty = true;
        }

        private void PublishFloraPredatorAupBufferImmediate(Vector3 queryOrigin)
        {
            if (_activeFloraPredatorAupBuffer == null)
                return;

            int hitCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                queryOrigin,
                FloraPredatorAupQueryRadiusMeters,
                SpatialTargetKind.Bioform,
                _floraPredatorAupHits);
            int uploadCount = 0;
            bool publishEmptyFallback = false;
            float4[] uploadSnapshot = _floraPredatorAupUploadSnapshot;
            if (uploadSnapshot == null || uploadSnapshot.Length < FloraPredatorAupBufferCapacity)
            {
                PublishFloraPredatorAupGlobalsImmediate(0);
                PublishApexPresenceFakeImmediate(IsApexInSector(queryOrigin));
                return;
            }

            int snapshotCapacity = math.min(FloraPredatorAupBufferCapacity, uploadSnapshot.Length);
            for (int hitIndex = 0; hitIndex < hitCount && uploadCount < snapshotCapacity; hitIndex++)
            {
                SpatialQueryHit hit = _floraPredatorAupHits[hitIndex];
                IFaunaSpatialContact faunaContact = hit.Owner as IFaunaSpatialContact;
                if (faunaContact == null || faunaContact.IsDead || !faunaContact.IsApexPredatorContact)
                    continue;

                uploadSnapshot[uploadCount++] = new float4(
                    hit.Position.x,
                    hit.Position.y,
                    hit.Position.z,
                    FloraPredatorStealthRadiusMeters);
            }

            if (uploadCount > 0)
            {
                if (!_floraPredatorAupUpload.TryAcquireWriteLock(SystemID.AIEcology, out NativeArray<float4> upload) ||
                    !upload.IsCreated)
                {
                    publishEmptyFallback = true;
                }
                else
                {
                    try
                    {
                        int copyCount = math.min(uploadCount, upload.Length);
                        for (int i = 0; i < copyCount; i++)
                            upload[i] = uploadSnapshot[i];

                        uploadCount = copyCount;
                    }
                    finally
                    {
                        _floraPredatorAupUpload.ReleaseWriteLock(SystemID.AIEcology);
                    }
                }
            }

            if (publishEmptyFallback)
            {
                PublishFloraPredatorAupGlobalsImmediate(0);
                PublishApexPresenceFakeImmediate(IsApexInSector(queryOrigin));
                return;
            }

            if (uploadCount > 0)
            {
                GraphicsBuffer writeBuffer = ResolveFloraPredatorAupWriteBuffer();
                if (writeBuffer == null)
                    return;

                GraphicsBufferUploadUtility.UploadArray<float4>(writeBuffer, uploadSnapshot, uploadCount);
                _activeFloraPredatorAupBuffer = writeBuffer;
                _floraPredatorAupUploadIndex ^= 1;
                _floraPredatorAupGlobalsDirty = true;
            }

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

            PublishFloraPredatorAupGlobalsImmediate(uploadCount);
            PublishApexPresenceFakeImmediate(IsApexInSector(queryOrigin));
        }

        private void PublishFloraPredatorAupGlobals(int uploadCount)
        {
            _pendingFloraPredatorAupCount = math.clamp(uploadCount, 0, FloraPredatorAupBufferCapacity);
            _floraPredatorAupCountDirty = true;
        }

        private GraphicsBuffer ResolveFloraPredatorAupWriteBuffer()
        {
            GraphicsBuffer preferred = (_floraPredatorAupUploadIndex & 1) == 0
                ? _floraPredatorAupBufferB
                : _floraPredatorAupBufferA;
            if (preferred != null && preferred.IsValid())
                return preferred;

            return _floraPredatorAupBufferA != null && _floraPredatorAupBufferA.IsValid()
                ? _floraPredatorAupBufferA
                : _floraPredatorAupBufferB;
        }

        private void PublishFloraPredatorAupGlobalsImmediate(int uploadCount)
        {
            int safeUploadCount = math.clamp(uploadCount, 0, FloraPredatorAupBufferCapacity);
            if (_floraPredatorAupGlobalsDirty && _activeFloraPredatorAupBuffer != null)
            {
                Shader.SetGlobalBuffer(_PredatorAUPBufferId, _activeFloraPredatorAupBuffer);
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
            _pendingGlobalOceanPanic01 = math.saturate(panic01);
            _globalOceanPanicDirty = true;
        }

        private void PublishGlobalOceanPanicImmediate(float panic01)
        {
            float resolvedPanic01 = math.saturate(panic01);
            if (math.abs(_lastPublishedGlobalOceanPanic01 - resolvedPanic01) < 0.001f)
                return;

            _lastPublishedGlobalOceanPanic01 = resolvedPanic01;
            Shader.SetGlobalFloat(_GlobalOceanPanicId, resolvedPanic01);
        }

        private void PublishBiolumFlashBangImmediate(in Vector4 flashAup, in Vector4 flashParams)
        {
            Shader.SetGlobalVector(_BiolumFlashBangAUPId, flashAup);
            Shader.SetGlobalVector(_BiolumFlashBangParamsId, flashParams);
        }

        private void PublishBiomassOvergrowth(float overgrowth01)
        {
            _pendingBiomassOvergrowth01 = math.saturate(overgrowth01);
            _biomassOvergrowthDirty = true;
        }

        private void PublishBiomassOvergrowthImmediate(float overgrowth01)
        {
            float safeOvergrowth01 = math.saturate(overgrowth01);
            if (math.abs(_lastPublishedBiomassOvergrowth01 - safeOvergrowth01) < 0.001f)
                return;

            _lastPublishedBiomassOvergrowth01 = safeOvergrowth01;
            Shader.SetGlobalFloat(_BiomassOvergrowthId, safeOvergrowth01);
        }

        private void PublishApexPresenceFake(bool apexInSector)
        {
            _pendingApexInSector = apexInSector ? (byte)1 : (byte)0;
            _apexPresenceFakeDirty = true;
            PublishGlobalOceanPanic(_pendingApexInSector);
        }

        private void PublishApexPresenceFakeImmediate(bool apexInSector)
        {
            byte flag = apexInSector ? (byte)1 : (byte)0;
            if (_lastPublishedApexInSector != flag)
            {
                _lastPublishedApexInSector = flag;
                Shader.SetGlobalFloat(_ApexInSectorId, flag);
            }

            PublishGlobalOceanPanicImmediate(flag);
        }

        private void FlushQueuedEcosystemVisuals()
        {
            if (_biolumFlashBangDirty)
            {
                _biolumFlashBangDirty = false;
                PublishBiolumFlashBangImmediate(in _pendingBiolumFlashBangAup, in _pendingBiolumFlashBangParams);
            }

            if (_floraPredatorAupRefreshDirty)
            {
                _floraPredatorAupRefreshDirty = false;
                PublishFloraPredatorAupBufferImmediate(_pendingFloraPredatorAupQueryOrigin);
            }

            if (_floraPredatorAupCountDirty)
            {
                _floraPredatorAupCountDirty = false;
                PublishFloraPredatorAupGlobalsImmediate(_pendingFloraPredatorAupCount);
            }

            if (_apexPresenceFakeDirty)
            {
                _apexPresenceFakeDirty = false;
                PublishApexPresenceFakeImmediate(_pendingApexInSector != 0);
            }

            if (_globalOceanPanicDirty)
            {
                _globalOceanPanicDirty = false;
                PublishGlobalOceanPanicImmediate(_pendingGlobalOceanPanic01);
            }

            if (_biomassOvergrowthDirty)
            {
                _biomassOvergrowthDirty = false;
                PublishBiomassOvergrowthImmediate(_pendingBiomassOvergrowth01);
            }
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
            IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;
            if (runtimeContext != null)
            {
                if (TryResolvePlayerAupFromRuntimeContext(runtimeContext, out playerAup))
                    return true;

                if (runtimeContext.TryGetLookRuntimeState(out PlayerLookState lookState) &&
                    (lookState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
                {
                    if (TryResolveAupFromRuntimeOrigin(ToVector3(lookState.EyePosition), out playerAup))
                        return true;
                }

                return false;
            }

            IPlayerRuntimeContext playerContext = ActiveRuntimeInstance != null
                ? ActiveRuntimeInstance._cachedPlayerContext
                : null;
            if (playerContext == null)
                return false;

            return TryResolvePlayerAupFromRuntimeContext(playerContext, out playerAup);
        }

        private static bool TryResolvePlayerAupFromRuntimeContext(
            IPlayerRuntimeContext playerContext,
            out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            if (playerContext == null)
                return false;

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

            if (playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState fallbackMovementState) &&
                (fallbackMovementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
            {
                AbsoluteUniversePosition fallbackPredictedAup = fallbackMovementState.PredictedAup;
                if (fallbackPredictedAup.IsFinite())
                {
                    playerAup = fallbackPredictedAup;
                    return true;
                }
            }

            return false;
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

            if (!_sectorFrontStates.TryResolveReadOnly(out NativeArray<SectorPopulationState>.ReadOnly sectorFrontStates))
                return;

            if (_cachedPersistentWorldRegistry == null)
                return;

            int syncBudget = math.min(HibernationPopulationSyncsPerColdSolve, math.min(_activeSectorCount, sectorFrontStates.Length));
            for (int i = 0; i < syncBudget; i++)
            {
                if (_nextHibernationPopulationSyncIndex >= _activeSectorCount ||
                    _nextHibernationPopulationSyncIndex >= sectorFrontStates.Length)
                {
                    _nextHibernationPopulationSyncIndex = 0;
                }

                SectorPopulationState state = sectorFrontStates[_nextHibernationPopulationSyncIndex];
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
            if (!_solveJobLocksHeld)
                return -1;

            NativeArray<EcosystemIndexEntry> sectorIndexEntries = _sectorIndexEntries.Resolve();
            NativeArray<SectorPopulationState> sectorFrontStates = _sectorFrontStates.Resolve();
            NativeArray<SectorPopulationState> sectorBackStates = _sectorBackStates.Resolve();
            if (!sectorIndexEntries.IsCreated ||
                !sectorFrontStates.IsCreated ||
                !sectorBackStates.IsCreated)
            {
                return -1;
            }

            long packedKey = PackSectorKey(sectorCoord);
            if (TryFindIndexEntry(sectorIndexEntries, packedKey, out int existingSlot))
                return existingSlot;

            if (_activeSectorCount >= sectorFrontStates.Length ||
                _activeSectorCount >= sectorBackStates.Length)
            {
                return -1;
            }

            int slotIndex = _activeSectorCount;
            _activeSectorCount++;
            if (!TryUpsertIndexEntry(sectorIndexEntries, packedKey, slotIndex))
            {
                _activeSectorCount--;
                return -1;
            }

            SectorPopulationState seededState = SeedSectorState(sectorCoord, seedWithBaseline);
            sectorFrontStates[slotIndex] = seededState;
            sectorBackStates[slotIndex] = seededState;
            WriteHeadlessSlot(slotIndex, in seededState);
            return slotIndex;
        }

        private SectorPopulationState SeedSectorState(int2 sectorCoord, bool seedWithBaseline)
        {
            int biomeId = ResolveGeologyBiomeIdForSector(sectorCoord);
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
            if (!_solveJobLocksHeld && !_macroSwarmTravelJobLocksHeld)
                return -1;

            NativeArray<EcosystemIndexEntry> biomassIndexEntries = _biomassIndexEntries.Resolve();
            NativeArray<int2> biomassMacroCellCoords = _biomassMacroCellCoords.Resolve();
            NativeArray<float> preyFront = _preyBiomassFront.Resolve();
            NativeArray<float> preyBack = _preyBiomassBack.Resolve();
            NativeArray<float> predatorFront = _predatorBiomassFront.Resolve();
            NativeArray<float> predatorBack = _predatorBiomassBack.Resolve();
            NativeArray<float> carryingCapacity = _biomassCarryingCapacity.Resolve();
            NativeArray<float> biomassSumScratch = _biomassSumScratch.Resolve();
            NativeArray<byte> biomassCellFlags = _biomassCellFlags.Resolve();
            if (!biomassIndexEntries.IsCreated ||
                !biomassMacroCellCoords.IsCreated ||
                !preyFront.IsCreated ||
                !preyBack.IsCreated ||
                !predatorFront.IsCreated ||
                !predatorBack.IsCreated ||
                !carryingCapacity.IsCreated ||
                !biomassSumScratch.IsCreated ||
                !biomassCellFlags.IsCreated)
            {
                return -1;
            }

            long packedKey = PackBiomassCellKey(macroCellCoord);
            if (TryFindIndexEntry(biomassIndexEntries, packedKey, out int existingSlot))
                return existingSlot;

            if (_activeBiomassCellCount >= preyFront.Length ||
                _activeBiomassCellCount >= preyBack.Length ||
                _activeBiomassCellCount >= predatorFront.Length ||
                _activeBiomassCellCount >= predatorBack.Length ||
                _activeBiomassCellCount >= carryingCapacity.Length ||
                _activeBiomassCellCount >= biomassMacroCellCoords.Length ||
                _activeBiomassCellCount >= biomassSumScratch.Length ||
                _activeBiomassCellCount >= biomassCellFlags.Length)
            {
                return -1;
            }

            int slotIndex = _activeBiomassCellCount;
            _activeBiomassCellCount++;
            if (!TryUpsertIndexEntry(biomassIndexEntries, packedKey, slotIndex))
            {
                _activeBiomassCellCount--;
                return -1;
            }
            biomassMacroCellCoords[slotIndex] = macroCellCoord;

            float carryingCapacity01 = ResolveBiomassCarryingCapacity01(macroCellCoord);
            float seedScale = seedWithBaseline ? 1f : 0.5f;
            float prey = math.clamp(defaultPreyBiomass01 * carryingCapacity01 * seedScale, 0f, carryingCapacity01);
            float predator = math.clamp(defaultPredatorBiomass01 * carryingCapacity01 * seedScale, 0f, carryingCapacity01);
            preyFront[slotIndex] = prey;
            preyBack[slotIndex] = prey;
            predatorFront[slotIndex] = predator;
            predatorBack[slotIndex] = predator;
            carryingCapacity[slotIndex] = carryingCapacity01;
            biomassSumScratch[slotIndex] = prey + predator;
            biomassCellFlags[slotIndex] = predator > 0.0001f ? BiomassCellFlagPredatorSeen : (byte)0;
            return slotIndex;
        }

        private bool TryResolveSectorSlotReadOnly(int2 sectorCoord, out int slotIndex)
        {
            slotIndex = -1;
            if (!_sectorIndexEntries.TryResolveReadOnly(out NativeArray<EcosystemIndexEntry>.ReadOnly sectorIndexEntries) ||
                !_sectorFrontStates.TryResolveReadOnly(out NativeArray<SectorPopulationState>.ReadOnly sectorFrontStates))
            {
                return false;
            }

            if (!TryFindIndexEntry(sectorIndexEntries, PackSectorKey(sectorCoord), out slotIndex))
                return false;

            return (uint)slotIndex < (uint)_activeSectorCount &&
                   (uint)slotIndex < (uint)sectorFrontStates.Length;
        }

        private bool TryResolveBiomassCellSlotReadOnly(int2 macroCellCoord, out int slotIndex)
        {
            slotIndex = -1;
            if (!_biomassIndexEntries.TryResolveReadOnly(out NativeArray<EcosystemIndexEntry>.ReadOnly biomassIndexEntries) ||
                !_preyBiomassFront.TryResolveReadOnly(out NativeArray<float>.ReadOnly preyFront) ||
                !_predatorBiomassFront.TryResolveReadOnly(out NativeArray<float>.ReadOnly predatorFront) ||
                !_biomassCarryingCapacity.TryResolveReadOnly(out NativeArray<float>.ReadOnly carryingCapacity))
            {
                return false;
            }

            if (!TryFindIndexEntry(biomassIndexEntries, PackBiomassCellKey(macroCellCoord), out slotIndex))
                return false;

            return (uint)slotIndex < (uint)_activeBiomassCellCount &&
                   (uint)slotIndex < (uint)preyFront.Length &&
                   (uint)slotIndex < (uint)predatorFront.Length &&
                   (uint)slotIndex < (uint)carryingCapacity.Length;
        }

        private float ResolveBiomassCarryingCapacity01(int2 macroCellCoord)
        {
            int2 sectorCoord = new int2(
                FloorDiv(macroCellCoord.x, (int)(SectorEdgeLengthMeters * InvBiomassMacroCellSizeMeters)),
                FloorDiv(macroCellCoord.y, (int)(SectorEdgeLengthMeters * InvBiomassMacroCellSizeMeters)));
            int biomeId = ResolveGeologyBiomeIdForSector(sectorCoord);
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
            if (!TryResolveAupFromRuntimeOrigin(worldPosition, out AbsoluteUniversePosition aup))
                return;

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
                TryQueueBiomassImpact(macroCellCoord, kind, amount);
                return;
            }

            if (!TryLockMacroSwarmTravelJobBuffers())
            {
                TryQueueBiomassImpact(macroCellCoord, kind, amount);
                return;
            }

            try
            {
                ApplyBiomassImpact(macroCellCoord, kind, amount);
            }
            finally
            {
                UnlockMacroSwarmTravelJobBuffers();
            }
        }

        private bool TryQueueBiomassImpact(int2 macroCellCoord, byte kind, float amount)
        {
            if (!_pendingBiomassImpacts.TryAcquireWriteLock(SystemID.AIEcology, out NativeArray<BiomassImpactEvent> pendingImpacts))
                return false;

            bool queued = false;
            bool publishOverflowWarning = false;
            int overflowCount = 0;
            try
            {
                if (_pendingBiomassImpactCount >= pendingImpacts.Length)
                {
                    publishOverflowWarning = true;
                    overflowCount = _pendingBiomassImpactCount;
                }
                else
                {
                    pendingImpacts[_pendingBiomassImpactCount++] = new BiomassImpactEvent
                    {
                        MacroCellCoord = macroCellCoord,
                        Amount = math.saturate(amount),
                        Kind = kind
                    };
                    queued = true;
                }
            }
            finally
            {
                _pendingBiomassImpacts.ReleaseWriteLock(SystemID.AIEcology);
            }

            if (publishOverflowWarning)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _BiomassTelemetryHash,
                    _EcosystemDirectorContextHash,
                    overflowCount);
            }

            return queued;
        }

        private void ApplyPendingBiomassImpacts()
        {
            if (!TryAcquireBiomassImpactDrainGuard(out IDataVault drainGuardVault))
                return;

            try
            {
                NativeArray<BiomassImpactEvent> pendingImpacts = _pendingBiomassImpacts.Resolve();
                if (!pendingImpacts.IsCreated)
                    return;

                int count = math.min(_pendingBiomassImpactCount, pendingImpacts.Length);
                _pendingBiomassImpactCount = 0;
                for (int i = 0; i < count; i++)
                {
                    BiomassImpactEvent impact = pendingImpacts[i];
                    ApplyBiomassImpact(impact.MacroCellCoord, impact.Kind, impact.Amount);
                    pendingImpacts[i] = default;
                }
            }
            finally
            {
                ReleaseBiomassImpactDrainGuard(drainGuardVault);
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
            int frame = ReadDispatcherFrameInt();
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
            int frame = ReadDispatcherFrameInt();
            if (frame - _lastScannerWarningFrame < 120)
                return;

            ReadOnlySpan<ScannerToolActiveSignal> scannerSignals = SignalBus<ScannerToolActiveSignal>.GetFrameSnapshot();
            if (scannerSignals.Length == 0 ||
                !TryReadLatestScannerToolActiveSignal(scannerSignals, out ScannerToolActiveSignal scannerSignal) ||
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

            _lastScannerWarningFrame = frame;
            HUDNotificationSignal notification = new HUDNotificationSignal
            {
                MessageHash = _EcologicalCollapseWarningHash,
                ContextHash = _EcosystemDirectorContextHash,
                SourceId = _BiomassTelemetryHash,
                Frame = ReadDispatcherFrameId(),
                Severity = 3,
                Flags = 0
            };
            SignalBus<HUDNotificationSignal>.TryPushTracked(in notification, ref _signalPushDropCount);
        }

        private static bool TryReadLatestScannerToolActiveSignal(
            ReadOnlySpan<ScannerToolActiveSignal> signals,
            out ScannerToolActiveSignal signal)
        {
            signal = default;
            if (signals.Length <= 0)
                return false;

            signal = signals[signals.Length - 1];
            return true;
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
            PublishBiomassOvergrowth(overgrowth01);
            PushBiomassBlackBox(globalSum, preySum, predatorSum, overgrowth01, invalidFlag);
            GlobalTelemetryBus.PublishPerformanceWarning(_BiomassTelemetryHash, _EcosystemDirectorContextHash, globalSum);
            if (invalidFlag != 0)
                DumpBiomassBlackBox();
        }

        private void PublishPredatorClearedEvent(int2 macroCellCoord)
        {
            AbsoluteUniversePosition centerAup = ResolveBiomassMacroCellCenterAup(macroCellCoord);
            ProgressionEventSignal signal = new ProgressionEventSignal
            {
                PositionAup = centerAup,
                PoiHash = _SectorClearedEventHash,
                QuestHash = 0u,
                Frame = ReadDispatcherFrameId(),
                Source = 3,
                Flags = 0
            };
            SignalBus<ProgressionEventSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
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
                FrameIndex = ReadDispatcherFrameId(),
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
            if (!_biomassBlackBox.TryResolveReadOnly(out NativeArray<BiomassTelemetryEntry>.ReadOnly biomassBlackBox) ||
                biomassBlackBox.Length <= 0)
                return;

            try
            {
                if (!TryWriteTelemetryRingDump(
                    BiomassTelemetryDumpRelativePath,
                    BiomassTelemetryDumpMagic,
                    biomassBlackBox,
                    _biomassBlackBoxCursor))
                {
                    GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)_BiomassTelemetryHash));
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
            return sourceKind == ItemAcquiredSignalSourceKinds.Unknown ||
                   sourceKind == ItemAcquiredSignalSourceKinds.ResourceNode ||
                   sourceKind == ItemAcquiredSignalSourceKinds.ManualPickup ||
                   sourceKind == ItemAcquiredSignalSourceKinds.LootMagnet ||
                   sourceKind == ItemAcquiredSignalSourceKinds.ScavengingLootOracle ||
                   sourceKind == ItemAcquiredSignalSourceKinds.HarvestableOutcrop;
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

        private static bool TryQuantizeBiomassMacroCell(Vector3 worldPosition, out int2 macroCellCoord)
        {
            macroCellCoord = default;
            if (!TryResolveAupFromRuntimeOrigin(worldPosition, out AbsoluteUniversePosition aup))
                return false;

            macroCellCoord = QuantizeBiomassMacroCell(in aup);
            return true;
        }

        private static int2 QuantizeBiomassMacroCell(in AbsoluteUniversePosition position)
        {
            double3 absolutePosition = position.ToAbsoluteDouble3();
            return new int2(
                (int)math.floor(absolutePosition.x * InvBiomassMacroCellSizeMeters),
                (int)math.floor(absolutePosition.z * InvBiomassMacroCellSizeMeters));
        }

        private void CaptureBiomassSaveRuns()
        {
            if (!_saveSnapshotBiomassRuns.TryAcquireWriteLock(SystemID.AIEcology, out NativeArray<EcosystemBiomassSaveRun> biomassRuns))
                return;

            try
            {
                _saveSnapshotBiomassRunCount = 0;
                if (!_biomassMacroCellCoords.TryResolveReadOnly(out NativeArray<int2>.ReadOnly macroCellCoords) ||
                    !_preyBiomassFront.TryResolveReadOnly(out NativeArray<float>.ReadOnly preyBiomassFront) ||
                    !_predatorBiomassFront.TryResolveReadOnly(out NativeArray<float>.ReadOnly predatorBiomassFront) ||
                    !_biomassCarryingCapacity.TryResolveReadOnly(out NativeArray<float>.ReadOnly biomassCarryingCapacity))
                {
                    return;
                }

                int cellCount = math.min(
                    _activeBiomassCellCount,
                    math.min(
                        macroCellCoords.Length,
                        math.min(preyBiomassFront.Length, math.min(predatorBiomassFront.Length, biomassCarryingCapacity.Length))));
                int emittedCount = 0;
                bool hasLastSortedCoord = false;
                int2 lastSortedCoord = int2.zero;
                int runLength = 0;
                int2 runStart = int2.zero;
                int2 previousCoord = int2.zero;
                sbyte runPrey = 0;
                sbyte runPredator = 0;
                sbyte runCapacity = 0;
                while (emittedCount < cellCount)
                {
                    int bestIndex = -1;
                    int2 bestCoord = int2.zero;
                    for (int i = 0; i < cellCount; i++)
                    {
                        int2 candidateCoord = macroCellCoords[i];
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
                    sbyte preyQ = QuantizeBiomass01(preyBiomassFront[bestIndex]);
                    sbyte predatorQ = QuantizeBiomass01(predatorBiomassFront[bestIndex]);
                    sbyte capacityQ = QuantizeBiomass01(biomassCarryingCapacity[bestIndex]);
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
                        FlushBiomassSaveRun(biomassRuns, runStart, runPrey, runPredator, runCapacity, runLength);
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

                FlushBiomassSaveRun(biomassRuns, runStart, runPrey, runPredator, runCapacity, runLength);
            }
            finally
            {
                _saveSnapshotBiomassRuns.ReleaseWriteLock(SystemID.AIEcology);
            }
        }

        private void CaptureMacroSwarmSaveRecords(NativeArray<EcosystemSectorSaveRecord> saveSectors)
        {
            if (!_macroSwarms.TryResolveReadOnly(out NativeArray<MacroSwarm>.ReadOnly macroSwarms))
                return;

            int count = math.min(_activeMacroSwarmCount, macroSwarms.Length);
            for (int i = 0; i < count && HasSaveSnapshotSectorCapacity(saveSectors, 2); i++)
            {
                MacroSwarm swarm = macroSwarms[i];
                if (swarm.HashId == 0u || swarm.BiomassValue <= 0.0001f)
                    continue;

                TryAppendSaveSnapshotSector(saveSectors, PackMacroSwarmHeaderRecord(in swarm));
                TryAppendSaveSnapshotSector(saveSectors, PackMacroSwarmDetailRecord(in swarm));
            }
        }

        private void CaptureFaunaGenomeSaveRecords(NativeArray<EcosystemSectorSaveRecord> saveSectors)
        {
            if (_headlessEntities.FaunaGenomes.TryResolveReadOnly(out NativeArray<ulong>.ReadOnly faunaGenomes) &&
                _headlessEntities.SectorCoord.TryResolveReadOnly(out NativeArray<int2>.ReadOnly sectorCoords))
            {
                bool hasStableHashes = _headlessEntities.MutationStableHashes.TryResolveReadOnly(out NativeArray<uint>.ReadOnly mutationStableHashes);
                int count = math.min(_activeSectorCount, math.min(faunaGenomes.Length, sectorCoords.Length));
                for (int i = 0; i < count && HasSaveSnapshotSectorCapacity(saveSectors, 2); i++)
                {
                    ulong genome = faunaGenomes[i];
                    if (!FaunaGenome64.HasContaminatedYield(genome))
                        continue;

                    int2 sectorCoord = sectorCoords[i];
                    uint stableHash = hasStableHashes && i < mutationStableHashes.Length
                        ? mutationStableHashes[i]
                        : MixSectorBits(sectorCoord.x, sectorCoord.y);
                    TryAppendSaveSnapshotSector(saveSectors, PackFaunaGenomeHeaderRecord(sectorCoord, stableHash, false));
                    TryAppendSaveSnapshotSector(saveSectors, PackFaunaGenomeDetailRecord(genome, false));
                }
            }

            if (!_macroSwarms.TryResolveReadOnly(out NativeArray<MacroSwarm>.ReadOnly macroSwarms))
                return;

            int macroCount = math.min(_activeMacroSwarmCount, macroSwarms.Length);
            for (int i = 0; i < macroCount && HasSaveSnapshotSectorCapacity(saveSectors, 2); i++)
            {
                MacroSwarm swarm = macroSwarms[i];
                if (!FaunaGenome64.HasContaminatedYield(swarm.Genome))
                    continue;

                TryAppendSaveSnapshotSector(saveSectors, PackFaunaGenomeHeaderRecord(swarm.SectorAup, swarm.HashId, true));
                TryAppendSaveSnapshotSector(saveSectors, PackFaunaGenomeDetailRecord(swarm.Genome, true));
            }
        }

        private void FlushBiomassSaveRun(
            NativeArray<EcosystemBiomassSaveRun> biomassRuns,
            int2 start,
            sbyte preyQ,
            sbyte predatorQ,
            sbyte capacityQ,
            int runLength)
        {
            if (runLength <= 0 ||
                !biomassRuns.IsCreated ||
                _saveSnapshotBiomassRunCount >= biomassRuns.Length)
            {
                return;
            }

            biomassRuns[_saveSnapshotBiomassRunCount++] = new EcosystemBiomassSaveRun
            {
                StartMacroCell = start,
                PreyBiomassQ = preyQ,
                PredatorBiomassQ = predatorQ,
                CarryingCapacityQ = capacityQ,
                RunLength = (byte)math.clamp(runLength, 1, byte.MaxValue)
            };
        }

        private bool HasSaveSnapshotSectorCapacity(NativeArray<EcosystemSectorSaveRecord> saveSectors, int recordCount)
        {
            return saveSectors.IsCreated &&
                   recordCount >= 0 &&
                   _saveSnapshotSectorCount <= saveSectors.Length - recordCount;
        }

        private bool TryAppendSaveSnapshotSector(NativeArray<EcosystemSectorSaveRecord> saveSectors, EcosystemSectorSaveRecord record)
        {
            if (!HasSaveSnapshotSectorCapacity(saveSectors, 1))
                return false;

            saveSectors[_saveSnapshotSectorCount++] = record;
            return true;
        }

        private void RestoreBiomassSaveRun(in EcosystemSectorSaveRecord saveRecord)
        {
            if (!UnpackBiomassRun(saveRecord, out EcosystemBiomassSaveRun run))
                return;

            if (!TryLockMacroSwarmTravelJobBuffers())
                return;

            try
            {
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
            finally
            {
                UnlockMacroSwarmTravelJobBuffers();
            }
        }

        private void ClearBiomassRuntimeState()
        {
            if (!TryAcquireBiomassImpactDrainGuard(out IDataVault drainGuardVault))
                return;

            NativeArray<BiomassImpactEvent> pendingImpacts = _pendingBiomassImpacts.Resolve();
            try
            {
                NativeArray<EcosystemIndexEntry> biomassIndexEntries = _biomassIndexEntries.Resolve();
                ClearIndexEntries(biomassIndexEntries);

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

                if (pendingImpacts.IsCreated)
                {
                    for (int i = 0; i < pendingImpacts.Length; i++)
                        pendingImpacts[i] = default;
                }
            }
            finally
            {
                ReleaseBiomassImpactDrainGuard(drainGuardVault);
            }

            _activeBiomassCellCount = 0;
            _pendingBiomassImpactCount = 0;
            _debugBiomassCellCount = 0;
            _debugGlobalBiomassSum = 0f;
            _debugPreyBiomassSum = 0f;
            _debugPredatorBiomassSum = 0f;
            _debugFloraOvergrowth01 = 0f;
            PublishBiomassOvergrowth(0f);
        }

        private void ClearMacroSwarmRuntimeState()
        {
            if (_macroSwarmTravelScheduled)
            {
                DispatcherJobFence.TryComplete(ref _macroSwarmTravelHandle, forceComplete: true);
                _macroSwarmTravelScheduled = false;
                UnlockMacroSwarmTravelJobBuffers();
            }

            if (!TryLockMacroSwarmTravelJobBuffers())
                return;

            try
            {
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

                ClearVaultFloatBuffer(_macroSwarmMutationRadiation, MacroSwarmCapacity);
                ClearVaultFloatBuffer(_macroSwarmMutationToxicity, MacroSwarmCapacity);
                ClearVaultFloatBuffer(_macroSwarmMutationBrine, MacroSwarmCapacity);
                ClearVaultByteBuffer(_macroSwarmMutationResults, MacroSwarmCapacity);

                if (_macroHydrationScratch.IsCreated)
                    _macroHydrationScratchCount = 0;
                if (_macroDehydrationScratch.IsCreated)
                    _macroDehydrationScratchCount = 0;
            }
            finally
            {
                UnlockMacroSwarmTravelJobBuffers();
            }

            _activeMacroSwarmCount = 0;
            _lastMacroSwarmArrivalCount = 0;
            _lastMacroSwarmsHydrated = 0;
            _lastMacroHydratedBoidEstimate = 0;
            _lastSectorResidencySignalDrainFrame = -1;
        }

        private static void ClearVaultFloatBuffer(VaultBufferView<float> buffer, int maxCount)
        {
            if (!buffer.IsCreated)
                return;

            bool locked = false;
            try
            {
                if (!buffer.TryAcquireWriteLock(SystemID.AIEcology, out NativeArray<float> values))
                    return;

                locked = true;
                int count = maxCount > 0 ? math.min(maxCount, values.Length) : values.Length;
                for (int i = 0; i < count; i++)
                    values[i] = 0f;
            }
            finally
            {
                if (locked)
                    buffer.ReleaseWriteLock(SystemID.AIEcology);
            }
        }

        private static void ClearVaultByteBuffer(VaultBufferView<byte> buffer, int maxCount)
        {
            if (!buffer.IsCreated)
                return;

            bool locked = false;
            try
            {
                if (!buffer.TryAcquireWriteLock(SystemID.AIEcology, out NativeArray<byte> values))
                    return;

                locked = true;
                int count = maxCount > 0 ? math.min(maxCount, values.Length) : values.Length;
                for (int i = 0; i < count; i++)
                    values[i] = 0;
            }
            finally
            {
                if (locked)
                    buffer.ReleaseWriteLock(SystemID.AIEcology);
            }
        }

        private void ClearHeadlessRuntimeState()
        {
            if (!TryLockSectorSolveJobBuffers())
                return;

            try
            {
                int capacity = _sectorFrontStates.IsCreated ? _sectorFrontStates.Length : 0;
                NativeArray<float3> positions = _headlessEntities.Positions;
                NativeArray<byte> speciesIds = _headlessEntities.SpeciesID;
                NativeArray<byte> hunger = _headlessEntities.Hunger;
                NativeArray<int2> sectorCoords = _headlessEntities.SectorCoord;
                NativeArray<int> sectorIds = _headlessEntities.SectorID;
                NativeArray<ulong> faunaGenomes = _headlessEntities.FaunaGenomes;
                NativeArray<uint> mutationStableHashes = _headlessEntities.MutationStableHashes;
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
                    if (mutationStableHashes.IsCreated)
                        mutationStableHashes[i] = 0u;
                }

                ClearVaultFloatBuffer(_headlessEntities.MutationRadiation, capacity);
                ClearVaultFloatBuffer(_headlessEntities.MutationToxicity, capacity);
                ClearVaultFloatBuffer(_headlessEntities.MutationBrine, capacity);
                ClearVaultByteBuffer(_headlessEntities.MutationResults, capacity);
            }
            finally
            {
                UnlockSectorSolveJobBuffers();
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
            if (faunaGenomes.IsCreated)
                faunaGenomes[slotIndex] = CompileHeadlessFaunaGenome(slotIndex, in state);
        }

        private ulong CompileHeadlessFaunaGenome(int slotIndex, in SectorPopulationState state)
        {
            uint stableHash = MixSectorBits(state.SectorCoord.x, state.SectorCoord.y);
            uint speciesHash = (uint)math.select(1, 2, state.PredatorPopulationRounded > state.PreyPopulationRounded);
            double3 sectorCenterAup = new double3(
                ((double)state.SectorCoord.x + 0.5d) * SectorEdgeLengthMeters,
                0d,
                ((double)state.SectorCoord.y + 0.5d) * SectorEdgeLengthMeters);
            uint seed = FaunaGenome64.BuildDoubleAupSeed(
                sectorCenterAup,
                stableHash,
                speciesHash,
                (uint)math.max(0, slotIndex));
            ulong mask = FaunaGenome64.CompileGeneticMaskFromSeed(seed);
            if (!_faunaGeneticsTuning.TryResolveReadOnly(out NativeArray<FaunaGeneticsTuningDTO>.ReadOnly tuningRecords) ||
                tuningRecords.Length <= 0)
                return mask;

            FaunaGeneticsTuningDTO tuning = tuningRecords[0];
            FaunaGeneticsProfileDTO profile = default;
            if (_faunaGeneticsProfiles.TryResolveReadOnly(out NativeArray<FaunaGeneticsProfileDTO>.ReadOnly profiles))
                profile = FaunaGenome64.ResolveProfile(profiles, speciesHash);

            return FaunaGenome64.ApplyTuningAndProfile(mask, in tuning, in profile);
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

        private static bool TryQuantizeSector(Vector3 worldPosition, out int2 sectorCoord)
        {
            sectorCoord = default;
            if (!TryResolveAupFromRuntimeOrigin(worldPosition, out AbsoluteUniversePosition aup))
                return false;

            sectorCoord = QuantizeSector(in aup);
            return true;
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
            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition runtimeAup))
                return double.MaxValue;

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

        /// <summary>
        /// Classifies a 1 km sector from the macro geology field that actually shapes the terrain,
        /// so fauna density follows the seafloor the player can see instead of a coordinate hash.
        /// Falls back to <see cref="ResolveBiomeIdForSector"/> until the runtime world seed exists.
        /// </summary>
        private int ResolveGeologyBiomeIdForSector(int2 sectorCoord)
        {
            if (!TryResolveGeologyBiomeParams(out WorldMacroGeologyParams parameters))
                return ResolveBiomeIdForSector(sectorCoord);

            GeologyBiomeCacheEntry[] cache = _geologyBiomeCache;
            if (cache == null)
            {
                cache = new GeologyBiomeCacheEntry[GeologyBiomeCacheCapacity];
                _geologyBiomeCache = cache;
            }

            int slot = (int)(MixSectorBits(sectorCoord.x, sectorCoord.y) & GeologyBiomeCacheIndexMask);
            if (cache[slot].Occupied != 0 && cache[slot].SectorCoord.Equals(sectorCoord))
                return cache[slot].BiomeId;

            // Sector centre in absolute world metres, kept in double: at the 777 km AUP range a
            // float32 centre loses whole-metre resolution, which would smear the trench/shelf
            // classification across sector borders.
            double centerX = ((double)sectorCoord.x + 0.5d) * SectorEdgeLengthMeters;
            double centerZ = ((double)sectorCoord.y + 0.5d) * SectorEdgeLengthMeters;
            WorldMacroGeologySample sample = WorldMacroGeologyFields.Evaluate(centerX, centerZ, in parameters);
            int biomeId = ResolveBiomeIdFromGeologySample(in sample);

            cache[slot] = new GeologyBiomeCacheEntry
            {
                SectorCoord = sectorCoord,
                BiomeId = biomeId,
                Occupied = 1
            };
            return biomeId;
        }

        /// <summary>
        /// Maps a macro geology sample onto the existing three-lane biome contract. The lane
        /// semantics are unchanged - only their derivation is. Lane 1 is the rich lane
        /// (+0.08 carrying capacity) and lane 2 the scarce lane (-0.05), per
        /// <see cref="ResolveBiomassCarryingCapacity01"/>.
        /// </summary>
        private static int ResolveBiomeIdFromGeologySample(in WorldMacroGeologySample sample)
        {
            // Delegated rather than inlined so the runtime path and the distribution audit in
            // EcosystemGeologyBiomeLanes cannot drift apart. A silently diverging audit would report
            // a healthy lane spread while the runtime collapsed to one lane.
            return EcosystemGeologyBiomeLanes.ClassifyLane(in sample);
        }

        private bool TryResolveGeologyBiomeParams(out WorldMacroGeologyParams parameters)
        {
            if (!global::HectonWorldGenerator.TryGetActiveRuntimeWorldSeed(out int runtimeWorldSeed))
            {
                parameters = default;
                return false;
            }

            // Validate the fast path on the seed alone. ResolveWaterSurfaceLevel() walks the ocean
            // service and can fall through to a MapMagic bridge resolve, which is far too expensive to
            // run per biomass macro cell - and the classification below reads only TrenchMask and
            // ShelfMask, neither of which is derived from WaterSurfaceY.
            if (_geologyBiomeParamsResolved && _geologyBiomeParamsSeed == runtimeWorldSeed)
            {
                parameters = _geologyBiomeParams;
                return true;
            }

            float waterSurfaceY = ResolveWaterSurfaceLevel();
            if (!math.isfinite(waterSurfaceY))
                waterSurfaceY = DefaultWaterSurfaceLevelY;

            parameters = WorldMacroGeologyParams.CreateDefault(
                WorldMacroGeologyFields.CombineWorldSeed(
                    unchecked((uint)WorldMacroGeologyFields.DefaultAuthoringSeed),
                    runtimeWorldSeed));
            parameters.WaterSurfaceY = waterSurfaceY;

            _geologyBiomeParams = parameters;
            _geologyBiomeParamsSeed = runtimeWorldSeed;
            _geologyBiomeParamsResolved = true;

            // A reseed invalidates every cached classification.
            if (_geologyBiomeCache != null)
                Array.Clear(_geologyBiomeCache, 0, _geologyBiomeCache.Length);

            return true;
        }

        /// <summary>
        /// Burst-safe coordinate-hash fallback. Retained deliberately:
        /// <c>HeadlessThresholdMigrationJob</c> scores neighbour sectors that are not tracked yet, and
        /// the macro geology stack is neither reachable from Burst through the managed world-seed
        /// lookup nor cheap enough to run per migration candidate. Managed seeding, save restore and
        /// carrying-capacity paths use <see cref="ResolveGeologyBiomeIdForSector"/> instead.
        /// </summary>
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

            if (!TryLockMacroSwarmTravelJobBuffers())
                return false;

            try
            {
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
            finally
            {
                UnlockMacroSwarmTravelJobBuffers();
            }
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
                if (!TryLockGenomeMutationJobBuffers())
                    return false;

                try
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
                }
                finally
                {
                    UnlockGenomeMutationJobBuffers();
                }

                return false;
            }

            if (!TryLockSectorSolveJobBuffers())
                return false;

            try
            {
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
            finally
            {
                UnlockSectorSolveJobBuffers();
            }
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

        #region JulesLink_BiomassResourceGradientWeightCalculator
        private static void JulesLink_BiomassResourceGradientWeightCalculator() { _ = typeof(Hecton8.PureLogic.Ecosystem.BiomassResourceGradientWeightCalculator); }
        #endregion

        #region JulesLink_BiomeDepthViabilityCurveCalculator
        private static void JulesLink_BiomeDepthViabilityCurveCalculator() { _ = typeof(Hecton8.PureLogic.Ecosystem.BiomeDepthViabilityCurveCalculator); }
        #endregion

        #region JulesLink__2dGridHeatmapDecayCalculator
        private static void JulesLink__2dGridHeatmapDecayCalculator() { _ = typeof(Hecton8.PureLogic.Ecosystem._2dGridHeatmapDecayCalculator); }
        #endregion
    }
}
