using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory.Layout;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Cold-boot binary layout verifier for structs used by memcpy, save paging, AUP, and native telemetry lanes.
    /// </summary>
    public static class BinaryLayoutManifest
    {
        private static int s_x001BinaryLayoutManifestSignalPushDropCount;
        public static readonly bool IsLittleEndian = BitConverter.IsLittleEndian;

        private const uint LayoutRuleHash = 0x424C5954u; // BLYT
        private const uint LayoutSystemHash = 0x424C534Eu; // BLSN
        private const uint EndiannessContextHash = 0x454E444Eu; // ENDN
        private const uint SizeContextHash = 0x53495A45u; // SIZE
        private const uint OffsetContextHash = 0x4F464653u; // OFFS
        private const uint BlittableContextHash = 0x424C4954u; // BLIT
        private const uint AttributeContextHash = 0x41545452u; // ATTR
        private const uint DumpMagic = 0x4838424Cu; // H8BL
        private const int DumpVersion = 1;
        private const int DumpHeaderBytes = 28;
        private const int DumpTypeNameMaxBytes = 160;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_BINARY_LAYOUT_SENTINEL.bin";
        private const string DumpPayloadLabel = "binaryLayoutFailureDumpPayload";
        private const string ConstructionLayoutNamespace = "Hecton8.Construction.";
        private const string SaveLayoutNamespace = "Hecton8.SaveSystem.";
        private const string WorldLayoutNamespace = "Hecton8.World.";
        private const string PhysicsLayoutNamespace = "Hecton8.Physics.";
        private const string AiLayoutNamespace = "Hecton8.AI.";
        private const string GameplayLayoutNamespace = "Hecton8.Gameplay.";
        private static bool _verified;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForSubsystemRegistration()
        {
            _verified = false;
        }

        /// <summary>
        /// Verifies all batch-owned binary layouts once during the bootstrap memory prewarm phase.
        /// </summary>
        public static void VerifyColdBoot()
        {
            if (_verified)
                return;

            if (!IsLittleEndian)
                Fail("ENDIANNESS", expected: 1, observed: 0, EndiannessContextHash);

            VerifyAupLayouts();
            VerifyWeatherContractLayouts();
            VerifySaveLayouts();
            VerifyPersistentWorldLayouts();
            VerifySignalLayouts();
            VerifyWorldScatterLayouts();
            VerifyRenderBlitLayouts();
            VerifyAmbientBiotaLayouts();
            VerifyGameplayLayouts();

            _verified = true;
        }

        private static void VerifyAupLayouts()
        {
            string aup = WorldLayoutNamespace + "AbsoluteUniversePosition";
            AssertSize(aup, 48);
            AssertOffset(aup, "GridX", 0);
            AssertOffset(aup, "GridY", 8);
            AssertOffset(aup, "GridZ", 16);
            AssertOffset(aup, "LocalX", 24);
            AssertOffset(aup, "LocalY", 28);
            AssertOffset(aup, "LocalZ", 32);

            string blit = WorldLayoutNamespace + "AbsoluteUniversePositionBlit";
            AssertSize(blit, 48);
            AssertOffset(blit, "GridX", 0);
            AssertOffset(blit, "Local", 24);
            AssertOffset(blit, "Reserved1", 40);

            string blit128 = WorldLayoutNamespace + "AbsoluteUniversePositionBlit128";
            AssertSize(blit128, 48);
            AssertOffset(blit128, "GridX", 0);
            AssertOffset(blit128, "Local", 24);
            AssertOffset(blit128, "Reserved", 40);
        }

        private static void VerifyWeatherContractLayouts()
        {
            AssertSize<CurrentMeta>(32);
            AssertOffset<CurrentMeta>(nameof(CurrentMeta.GlobalBaseVector), 0);
            AssertOffset<CurrentMeta>(nameof(CurrentMeta.GlobalScale), 12);
            AssertOffset<CurrentMeta>(nameof(CurrentMeta.ThermalIntensity), 16);
            AssertOffset<CurrentMeta>(nameof(CurrentMeta.TimeAccumulator), 20);

            AssertSize<WeatherRuntimeSnapshot>(192);
            AssertOffset<WeatherRuntimeSnapshot>(nameof(WeatherRuntimeSnapshot.StateMask), 0);
            AssertOffset<WeatherRuntimeSnapshot>(nameof(WeatherRuntimeSnapshot.WeatherIntensity), 4);
            AssertOffset<WeatherRuntimeSnapshot>(nameof(WeatherRuntimeSnapshot.GlobalCurrentVector), 8);
            AssertOffset<WeatherRuntimeSnapshot>(nameof(WeatherRuntimeSnapshot.GlobalWindVector), 20);
            AssertOffset<WeatherRuntimeSnapshot>(nameof(WeatherRuntimeSnapshot.CurrentMeta), 32);
            AssertOffset<WeatherRuntimeSnapshot>(nameof(WeatherRuntimeSnapshot.Wave0), 64);
            AssertOffset<WeatherRuntimeSnapshot>(nameof(WeatherRuntimeSnapshot.Wave1), 96);
            AssertOffset<WeatherRuntimeSnapshot>(nameof(WeatherRuntimeSnapshot.Wave2), 128);

            AssertSize<OceanGerstnerWaveBufferMeta>(16);
            AssertOffset<OceanGerstnerWaveBufferMeta>(nameof(OceanGerstnerWaveBufferMeta.ActiveWaveCount), 0);
            AssertOffset<OceanGerstnerWaveBufferMeta>(nameof(OceanGerstnerWaveBufferMeta.TimeSeconds), 4);
            AssertOffset<OceanGerstnerWaveBufferMeta>(nameof(OceanGerstnerWaveBufferMeta.SleepCount), 8);
            AssertOffset<OceanGerstnerWaveBufferMeta>(nameof(OceanGerstnerWaveBufferMeta.Version), 12);
        }

        private static void VerifyAmbientBiotaLayouts()
        {
            string faunaInteraction = AiLayoutNamespace + "FaunaInteractionResponse";
            AssertSize(faunaInteraction, 32);
            AssertOffset(faunaInteraction, "DamageMultiplier", 0);
            AssertOffset(faunaInteraction, "RetreatDurationSeconds", 4);
            AssertOffset(faunaInteraction, "FearImpulse01", 8);
            AssertOffset(faunaInteraction, "InteractionKind", 12);
            AssertOffset(faunaInteraction, "ForceRetreatFlag", 13);

            string playerNoiseSignal = AiLayoutNamespace + "NoiseSystem+PlayerNoiseSignal";
            AssertSize(playerNoiseSignal, 96);
            AssertOffset(playerNoiseSignal, "PositionAup", 0);
            AssertOffset(playerNoiseSignal, "Position", 48);
            AssertOffset(playerNoiseSignal, "MovementSpeedSqr", 60);
            AssertOffset(playerNoiseSignal, "TransportBoost01", 64);
            AssertOffset(playerNoiseSignal, "TransportSignature", 68);
            AssertOffset(playerNoiseSignal, "ToolUseNoise01", 72);
            AssertOffset(playerNoiseSignal, "AcousticTransmission01", 76);
            AssertOffset(playerNoiseSignal, "AcousticLowPassCutoffHz", 80);
            AssertOffset(playerNoiseSignal, "SignalRadiusMeters", 84);
            AssertOffset(playerNoiseSignal, "ReportedFrame", 88);
            AssertOffset(playerNoiseSignal, "FlashlightOnFlag", 92);
            AssertOffset(playerNoiseSignal, "IsActiveSonarPingFlag", 93);

            string creatureContext = AiLayoutNamespace + "CreatureUtilityContext";
            AssertSize(creatureContext, 256);
            AssertOffset(creatureContext, "SelfPosition", 0);
            AssertOffset(creatureContext, "PlayerPosition", 36);
            AssertOffset(creatureContext, "ScatterDirection", 156);
            AssertOffset(creatureContext, "HealthNormalized", 168);
            AssertOffset(creatureContext, "FoveatedImportanceScore", 224);
            AssertOffset(creatureContext, "FlockCount", 228);
            AssertOffset(creatureContext, "Flags", 232);

            string creatureEvaluation = AiLayoutNamespace + "CreatureUtilityEvaluation";
            AssertSize(creatureEvaluation, 80);
            AssertOffset(creatureEvaluation, "DesiredDirection", 0);
            AssertOffset(creatureEvaluation, "AcousticHeadLookTarget", 12);
            AssertOffset(creatureEvaluation, "HungerScore", 24);
            AssertOffset(creatureEvaluation, "AcousticHeadLookWeight", 48);
            AssertOffset(creatureEvaluation, "PackRoleCode", 52);
            AssertOffset(creatureEvaluation, "LegacyState", 56);
            AssertOffset(creatureEvaluation, "Flags", 60);
            AssertOffset(creatureEvaluation, "StateMask", 62);

            AssertSize<AmbientBiotaState>(32);
            AssertOffset<AmbientBiotaState>(nameof(AmbientBiotaState.StateFlags), 0);
            AssertOffset<AmbientBiotaState>(nameof(AmbientBiotaState.StableHash), 4);
            AssertOffset<AmbientBiotaState>(nameof(AmbientBiotaState.SpeciesId), 8);
            AssertOffset<AmbientBiotaState>(nameof(AmbientBiotaState.BucketId), 10);
            AssertOffset<AmbientBiotaState>(nameof(AmbientBiotaState.AgeSeconds), 12);
            AssertOffset<AmbientBiotaState>(nameof(AmbientBiotaState.Reserved), 28);

            AssertSize<AmbientBiotaTelemetryEntry>(64);
            AssertOffset<AmbientBiotaTelemetryEntry>(nameof(AmbientBiotaTelemetryEntry.CenterAup), 0);
            AssertOffset<AmbientBiotaTelemetryEntry>(nameof(AmbientBiotaTelemetryEntry.FrameIndex), 48);
            AssertOffset<AmbientBiotaTelemetryEntry>(nameof(AmbientBiotaTelemetryEntry.StateHash), 52);
            AssertOffset<AmbientBiotaTelemetryEntry>(nameof(AmbientBiotaTelemetryEntry.Flags), 62);
        }

        private static void VerifyGameplayLayouts()
        {
            string survivalDeath = GameplayLayoutNamespace + "SurvivalDeathRecord";
            AssertSize(survivalDeath, 64);
            AssertOffset(survivalDeath, "LifeDurationSeconds", 0);
            AssertOffset(survivalDeath, "PeakDepthMeters", 8);
            AssertOffset(survivalDeath, "Position", 16);
            AssertOffset(survivalDeath, "LowestOxygenNormalized", 28);
            AssertOffset(survivalDeath, "LowestEnergyNormalized", 32);
            AssertOffset(survivalDeath, "LowestIntegrityNormalized", 36);
            AssertOffset(survivalDeath, "Cause", 40);

            string survivalBlackbox = GameplayLayoutNamespace + "SurvivalBlackboxSnapshot";
            AssertSize(survivalBlackbox, 64);
            AssertOffset(survivalBlackbox, "SourceHash", 0);
            AssertOffset(survivalBlackbox, "FrameIndex", 4);
            AssertOffset(survivalBlackbox, "PlayerEntityHash", 8);
            AssertOffset(survivalBlackbox, "Oxygen01", 12);
            AssertOffset(survivalBlackbox, "PressureAtm", 24);
            AssertOffset(survivalBlackbox, "DecompressionRisk01", 48);
            AssertOffset(survivalBlackbox, "StatusMask", 56);
            AssertOffset(survivalBlackbox, "Flags", 60);
        }

        private static void VerifyWorldScatterLayouts()
        {
            string scatterLayerQuota = WorldLayoutNamespace + "ScatterSimulationLayerQuota";
            AssertExternalContractSize(scatterLayerQuota, 16);
            AssertOffset(scatterLayerQuota, "PlacementsPerCell", 0);
            AssertOffset(scatterLayerQuota, "CellStride", 4);
            AssertOffset(scatterLayerQuota, "FamilyIndex", 8);
            AssertOffset(scatterLayerQuota, "_pad0", 12);

            string scatterQuotaState = WorldLayoutNamespace + "ScatterSimulationQuotaState";
            AssertExternalContractSize(scatterQuotaState, 64);
            AssertOffset(scatterQuotaState, "Ground", 0);
            AssertOffset(scatterQuotaState, "Cluster", 16);
            AssertOffset(scatterQuotaState, "Structure", 32);
            AssertOffset(scatterQuotaState, "Spawn", 48);

            string scatterCellState = WorldLayoutNamespace + "ScatterSimulationCellState";
            AssertExternalContractSize(scatterCellState, 32);
            AssertOffset(scatterCellState, "CellKey", 0);
            AssertOffset(scatterCellState, "CellX", 8);
            AssertOffset(scatterCellState, "CellZ", 12);
            AssertOffset(scatterCellState, "Height", 16);
            AssertOffset(scatterCellState, "HeightSource", 20);
            AssertOffset(scatterCellState, "BiomeInfluencePacked", 24);
            AssertOffset(scatterCellState, "Eligibility", 28);
            AssertOffset(scatterCellState, "Suppression", 29);
            AssertOffset(scatterCellState, "DirtyFlags", 30);
            AssertOffset(scatterCellState, "_pad0", 31);

            string scatterParity = WorldLayoutNamespace + "ScatterSimulationParitySnapshot";
            AssertExternalContractSize(scatterParity, 64);
            AssertOffset(scatterParity, "CandidateChecksum", 0);
            AssertOffset(scatterParity, "CellChecksum", 8);
            AssertOffset(scatterParity, "CandidateCount", 16);
            AssertOffset(scatterParity, "GroundCount", 20);
            AssertOffset(scatterParity, "ClusterCount", 24);
            AssertOffset(scatterParity, "StructureCount", 28);
            AssertOffset(scatterParity, "SpawnCount", 32);
            AssertOffset(scatterParity, "EligibleGroundCells", 36);
            AssertOffset(scatterParity, "EligibleClusterCells", 40);
            AssertOffset(scatterParity, "EligibleStructureCells", 44);
            AssertOffset(scatterParity, "EligibleSpawnCells", 48);
            AssertOffset(scatterParity, "DirtyCellCount", 52);
            AssertOffset(scatterParity, "SuppressedCellCount", 56);
            AssertOffset(scatterParity, "EvaluationFlags", 60);

            string scatterConfig = WorldLayoutNamespace + "ScatterSimulationConfig";
            AssertExternalContractSize(scatterConfig, 128);
            AssertOffset(scatterConfig, "QuotaState", 0);
            AssertOffset(scatterConfig, "PlayerPosition", 64);
            AssertOffset(scatterConfig, "CellSize", 76);
            AssertOffset(scatterConfig, "SurfaceYOffset", 80);
            AssertOffset(scatterConfig, "Seed", 84);
            AssertOffset(scatterConfig, "RadiusCells", 88);
            AssertOffset(scatterConfig, "GroundPlacementsPerCell", 92);
            AssertOffset(scatterConfig, "ClusterPlacementsPerCell", 96);
            AssertOffset(scatterConfig, "StructureCellStride", 100);
            AssertOffset(scatterConfig, "SpawnCellStride", 104);
            AssertOffset(scatterConfig, "GroundFamilyIndex", 108);
            AssertOffset(scatterConfig, "ClusterFamilyIndex", 112);
            AssertOffset(scatterConfig, "StructureFamilyIndex", 116);
            AssertOffset(scatterConfig, "SpawnFamilyIndex", 120);
            AssertOffset(scatterConfig, "DefaultEligibility", 124);
            AssertOffset(scatterConfig, "DefaultSuppressionState", 125);
            AssertOffset(scatterConfig, "DirtyFlags", 126);
            AssertOffset(scatterConfig, "_pad0", 127);

            string scatterCandidate = WorldLayoutNamespace + "ScatterSimulationCandidate";
            AssertExternalContractSize(scatterCandidate, 64);
            AssertOffset(scatterCandidate, "CellKey", 0);
            AssertOffset(scatterCandidate, "Position", 8);
            AssertOffset(scatterCandidate, "Rotation", 20);
            AssertOffset(scatterCandidate, "Scale", 24);
            AssertOffset(scatterCandidate, "Score", 28);
            AssertOffset(scatterCandidate, "FamilyIndex", 32);
            AssertOffset(scatterCandidate, "LayerIndex", 36);
            AssertOffset(scatterCandidate, "HeightSource", 40);
            AssertOffset(scatterCandidate, "IsValid", 44);
            AssertOffset(scatterCandidate, "_pad0", 45);
            AssertOffset(scatterCandidate, "_pad1", 46);
            AssertOffset(scatterCandidate, "_pad2", 48);
            AssertOffset(scatterCandidate, "_pad3", 56);

            string scatterBackendParity = WorldLayoutNamespace + "ScatterBackendParityReference";
            AssertExternalContractSize(scatterBackendParity, 32);
            AssertOffset(scatterBackendParity, "CandidateChecksum", 0);
            AssertOffset(scatterBackendParity, "CandidateCount", 8);
            AssertOffset(scatterBackendParity, "GroundCount", 12);
            AssertOffset(scatterBackendParity, "ClusterCount", 16);
            AssertOffset(scatterBackendParity, "StructureCount", 20);
            AssertOffset(scatterBackendParity, "SpawnCount", 24);
            AssertOffset(scatterBackendParity, "_pad0", 28);

            string scatterBackendSchedule = WorldLayoutNamespace + "ScatterBackendScheduleRequest";
            AssertExternalContractSize(scatterBackendSchedule, 96);
            AssertOffset(scatterBackendSchedule, "ParityReference", 0);
            AssertOffset(scatterBackendSchedule, "ObserverPosition", 32);
            AssertOffset(scatterBackendSchedule, "CellSize", 44);
            AssertOffset(scatterBackendSchedule, "SurfaceYOffset", 48);
            AssertOffset(scatterBackendSchedule, "Seed", 52);
            AssertOffset(scatterBackendSchedule, "TotalCells", 56);
            AssertOffset(scatterBackendSchedule, "RadiusCells", 60);
            AssertOffset(scatterBackendSchedule, "GroundBudget", 64);
            AssertOffset(scatterBackendSchedule, "ClusterBudget", 68);
            AssertOffset(scatterBackendSchedule, "StructureStride", 72);
            AssertOffset(scatterBackendSchedule, "SpawnStride", 76);
            AssertOffset(scatterBackendSchedule, "EligibilityMask", 80);
            AssertOffset(scatterBackendSchedule, "DefaultSuppressionState", 81);
            AssertOffset(scatterBackendSchedule, "DirtyFlags", 82);
            AssertOffset(scatterBackendSchedule, "_pad0", 83);
            AssertOffset(scatterBackendSchedule, "_pad1", 84);
            AssertOffset(scatterBackendSchedule, "_pad2", 88);

            string scatterBackendShadow = WorldLayoutNamespace + "ScatterBackendShadowCompletion";
            AssertExternalContractSize(scatterBackendShadow, 128);
            AssertOffset(scatterBackendShadow, "BackendParity", 0);
            AssertOffset(scatterBackendShadow, "ClassicParity", 64);
            AssertOffset(scatterBackendShadow, "CandidateCount", 96);
            AssertOffset(scatterBackendShadow, "ClassicQueuedCandidateCount", 100);
            AssertOffset(scatterBackendShadow, "CandidateDelta", 104);
            AssertOffset(scatterBackendShadow, "GroundDelta", 108);
            AssertOffset(scatterBackendShadow, "ClusterDelta", 112);
            AssertOffset(scatterBackendShadow, "StructureDelta", 116);
            AssertOffset(scatterBackendShadow, "SpawnDelta", 120);
            AssertOffset(scatterBackendShadow, "CandidateChecksumMatchFlag", 124);
            AssertOffset(scatterBackendShadow, "HasParityMatchFlag", 125);
            AssertOffset(scatterBackendShadow, "IsJobActiveFlag", 126);
            AssertOffset(scatterBackendShadow, "ParityStatusCode", 127);
        }

        private static void VerifySaveLayouts()
        {
            VerifySaveVoxelLayouts();
            VerifySaveEntityLayouts();
            VerifySaveStateLayouts();
            VerifySaveMerkleLayouts();
            VerifySaveStorageLayouts();
        }

        private static void VerifySaveVoxelLayouts()
        {
            string save = SaveLayoutNamespace;
            string saveVoxelDeltaRun5 = save + "SaveVoxelDeltaRun5";
            AssertSize(saveVoxelDeltaRun5, 8);
            AssertOffset(saveVoxelDeltaRun5, "StartIndex", 0);
            AssertOffset(saveVoxelDeltaRun5, "RunLength", 2);
            AssertOffset(saveVoxelDeltaRun5, "SdfValue", 4);

            string saveVoxelDeltaRun8 = save + "SaveVoxelDeltaRun8";
            AssertSize(saveVoxelDeltaRun8, 8);
            AssertOffset(saveVoxelDeltaRun8, "StartIndex", 0);
            AssertOffset(saveVoxelDeltaRun8, "RunLength", 2);
            AssertOffset(saveVoxelDeltaRun8, "SdfValue", 4);
            AssertOffset(saveVoxelDeltaRun8, "MaterialId", 5);
            AssertOffset(saveVoxelDeltaRun8, "Flags", 6);

            string voxelDeltaCell = save + "VoxelDeltaCellDTO";
            AssertSize(voxelDeltaCell, 24);
            AssertOffset(voxelDeltaCell, "universeKey", 0);
            AssertOffset(voxelDeltaCell, "sdfValue", 8);
            AssertOffset(voxelDeltaCell, "materialId", 12);
            AssertOffset(voxelDeltaCell, "flags", 13);
            AssertOffset(voxelDeltaCell, "metadata", 14);
            AssertOffset(voxelDeltaCell, "reserved", 16);
            AssertOffset(voxelDeltaCell, "_pad0", 20);

            string voxelCarving = save + "VoxelCarvingOperationDTO";
            AssertSize(voxelCarving, 24);
            AssertOffset(voxelCarving, "localPosition", 0);
            AssertOffset(voxelCarving, "radius", 12);
            AssertOffset(voxelCarving, "operation", 16);
            AssertOffset(voxelCarving, "materialId", 17);
            AssertOffset(voxelCarving, "flags", 18);
            AssertOffset(voxelCarving, "sequence", 20);

            string voxelRun = save + "VoxelDeltaRleRunDTO";
            AssertSize(voxelRun, 8);
            AssertOffset(voxelRun, "StartIndex", 0);
            AssertOffset(voxelRun, "RunLength", 2);
            AssertOffset(voxelRun, "SdfValue", 4);
            AssertOffset(voxelRun, "MaterialId", 5);
            AssertOffset(voxelRun, "Flags", 6);

            string voxelHeader = save + "VoxelDeltaHeaderDTO";
            AssertSize(voxelHeader, 32);
            AssertOffset(voxelHeader, "SectorHash", 0);
            AssertOffset(voxelHeader, "XXHash3Checksum", 8);
            AssertOffset(voxelHeader, "CompressedSize", 16);
            AssertOffset(voxelHeader, "UncompressedSize", 20);
            AssertOffset(voxelHeader, "Flags", 24);
            AssertOffset(voxelHeader, "LayoutMarker", 28);

            string voxelCounter = save + "VoxelDeltaBlockCounter64";
            AssertSize(voxelCounter, 64);
            AssertOffset(voxelCounter, "SectorHash", 0);
            AssertOffset(voxelCounter, "RunCount", 8);
            AssertOffset(voxelCounter, "ModifiedCellCount", 12);
            AssertOffset(voxelCounter, "EncodedBytes", 16);
            AssertOffset(voxelCounter, "Flags", 20);

            string voxelTelemetry = save + "VoxelDeltaCompressionTelemetryEntry";
            AssertSize(voxelTelemetry, 64);
            AssertOffset(voxelTelemetry, "SectorHash", 0);
            AssertOffset(voxelTelemetry, "PayloadHash", 8);
            AssertOffset(voxelTelemetry, "GlobalQualityWeight", 40);

            string voxelDumpHeader = save + "VoxelDeltaTelemetryDumpHeaderDTO";
            AssertSize(voxelDumpHeader, 64);
            AssertOffset(voxelDumpHeader, "Magic", 0);
            AssertOffset(voxelDumpHeader, "FirstSectorHash", 8);
            AssertOffset(voxelDumpHeader, "LastSectorHash", 16);
            AssertOffset(voxelDumpHeader, "Version", 24);
            AssertOffset(voxelDumpHeader, "EntryStride", 32);
            AssertOffset(voxelDumpHeader, "ReasonFlags", 40);
            AssertOffset(voxelDumpHeader, "LastFrame", 56);

            string voxelTuning = save + "VoxelDeltaCompressionTuningDTO";
            AssertSize(voxelTuning, 64);
            AssertOffset(voxelTuning, "ProfileHash", 0);
            AssertOffset(voxelTuning, "PruneThreshold01", 16);
            AssertOffset(voxelTuning, "MaxBytesPerFrame", 48);
            AssertOffset(voxelTuning, "DepthMinMeters", 52);
            AssertOffset(voxelTuning, "DepthMaxMeters", 56);
            AssertOffset(voxelTuning, "_pad0", 60);

            string voxelStats = save + "VoxelDeltaSectorStatsDTO";
            AssertSize(voxelStats, 64);
            AssertOffset(voxelStats, "SectorHash", 0);
            AssertOffset(voxelStats, "ModifiedRatio01", 36);
            AssertOffset(voxelStats, "_pad1", 56);
            AssertOffset(voxelStats, "_pad2", 60);

            string voxelDearLie = save + "VoxelDeltaDearLieStateDTO";
            AssertSize(voxelDearLie, 32);
            AssertOffset(voxelDearLie, "SectorHash", 0);
            AssertOffset(voxelDearLie, "VisualFade01", 16);

            string voxelMock = save + "VoxelDeltaMockSchemaDTO";
            AssertSize(voxelMock, 64);
            AssertOffset(voxelMock, "Magic", 0);
            AssertOffset(voxelMock, "SchemaHash", 8);
            AssertOffset(voxelMock, "Seed", 16);
            AssertOffset(voxelMock, "Version", 24);
        }

        private static void VerifySaveEntityLayouts()
        {
            string save = SaveLayoutNamespace;

            string entityHeader = save + "EntityDeltaHeaderDTO";
            AssertSize(entityHeader, 32);
            AssertOffset(entityHeader, "SectorHash", 0);
            AssertOffset(entityHeader, "CompressedSize", 8);
            AssertOffset(entityHeader, "UncompressedSize", 12);
            AssertOffset(entityHeader, "XXHash3Checksum", 16);
            AssertOffset(entityHeader, "_pad0", 24);
            AssertOffset(entityHeader, "_pad1", 28);

            string entityStream = save + "EntityDeltaRleStreamHeaderDTO";
            AssertSize(entityStream, 16);
            AssertOffset(entityStream, "Magic", 0);
            AssertOffset(entityStream, "Flags", 4);
            AssertOffset(entityStream, "DenseBytes", 8);
            AssertOffset(entityStream, "StoredBytes", 12);

            string entityRecord = save + "EntityDeltaDataRecordDTO";
            AssertSize(entityRecord, 80);
            AssertOffset(entityRecord, "SectorX", 0);
            AssertOffset(entityRecord, "SectorY", 8);
            AssertOffset(entityRecord, "SectorZ", 16);
            AssertOffset(entityRecord, "LocalX", 24);
            AssertOffset(entityRecord, "EntityKindHash", 36);
            AssertOffset(entityRecord, "StableEntityHash", 40);
            AssertOffset(entityRecord, "InstanceUid", 56);
            AssertOffset(entityRecord, "Flags", 68);
            AssertOffset(entityRecord, "SimulationTick", 76);

            string entityCounter = save + "EntityDeltaBlockCounter64";
            AssertSize(entityCounter, 64);
            AssertOffset(entityCounter, "DeltaCount", 0);
            AssertOffset(entityCounter, "SectorHash", 16);
            AssertOffset(entityCounter, "HashXor", 32);

            string entityTelemetry = save + "EntityCompressionTelemetryEntry";
            AssertSize(entityTelemetry, 64);
            AssertOffset(entityTelemetry, "SectorHash", 0);
            AssertOffset(entityTelemetry, "PayloadHash", 8);
            AssertOffset(entityTelemetry, "GlobalQualityWeight", 48);

            string entityTuning = save + "EntityDeltaCompressionTuningDTO";
            AssertSize(entityTuning, 64);
            AssertOffset(entityTuning, "ProfileHash", 0);
            AssertOffset(entityTuning, "TombstoneMaxDays", 16);
            AssertOffset(entityTuning, "MaxBytesPerFrame", 44);
            AssertOffset(entityTuning, "_pad0", 60);

            string entityStats = save + "EntityDeltaSectorStatsDTO";
            AssertSize(entityStats, 64);
            AssertOffset(entityStats, "SectorHash", 0);
            AssertOffset(entityStats, "FullSnapshotBytes", 20);
            AssertOffset(entityStats, "CompressionRatio01", 44);
            AssertOffset(entityStats, "_pad0", 56);

            string entityProfile = save + "EntityCompressionProfileDTO";
            AssertSize(entityProfile, 32);
            AssertOffset(entityProfile, "ProfileHash", 0);
            AssertOffset(entityProfile, "EntityKindHash", 8);
            AssertOffset(entityProfile, "StateMask", 24);

            string entityMock = save + "EntityDeltaMockSchemaDTO";
            AssertSize(entityMock, 64);
            AssertOffset(entityMock, "Magic", 0);
            AssertOffset(entityMock, "Seed", 40);

            string entityDumpHeader = save + "EntityDeltaTelemetryDumpHeaderDTO";
            AssertSize(entityDumpHeader, 64);
            AssertOffset(entityDumpHeader, "Magic", 0);
            AssertOffset(entityDumpHeader, "EntryStride", 12);
            AssertOffset(entityDumpHeader, "FirstSectorHash", 32);

            AssertSize(save + "PackedEntityState32", 8);
            AssertSize(save + "PackedSuitUpgradeState64", 8);
        }

        private static void VerifySaveStateLayouts()
        {
            string save = SaveLayoutNamespace;

            string quantizedLocal = save + "QuantizedLocalHalf3";
            AssertSize(quantizedLocal, 8);
            AssertOffset(quantizedLocal, "X", 0);
            AssertOffset(quantizedLocal, "Y", 2);
            AssertOffset(quantizedLocal, "Z", 4);

            string quantizedAup = save + "QuantizedAupSectorHalf3";
            AssertSize(quantizedAup, 24);
            AssertOffset(quantizedAup, "SectorX", 0);
            AssertOffset(quantizedAup, "LocalOffset", 12);

            string saveAup = save + "SaveAupLocalOffset32";
            AssertSize(saveAup, 32);
            AssertOffset(saveAup, "SectorKey", 0);
            AssertOffset(saveAup, "ShiftFrameId", 4);
            AssertOffset(saveAup, "LocalOffsetX", 8);
            AssertOffset(saveAup, "LocalOffsetY", 12);
            AssertOffset(saveAup, "LocalOffsetZ", 16);
            AssertOffset(saveAup, "Flags", 20);
            AssertOffset(saveAup, "_pad0", 24);
            AssertOffset(saveAup, "_pad1", 28);

            string strictHeader = save + "StrictSaveFileHeader64";
            AssertSize(strictHeader, 64);
            AssertOffset(strictHeader, "Magic", 0);
            AssertOffset(strictHeader, "PlayTimeSeconds", 8);
            AssertOffset(strictHeader, "AupX", 16);
            AssertOffset(strictHeader, "Checksum", 40);
            AssertOffset(strictHeader, "Version", 48);

            string playerKinematic = save + "PlayerKinematicStateDTO";
            AssertSize(playerKinematic, 48);
            AssertOffset(playerKinematic, "posX", 0);
            AssertOffset(playerKinematic, "rotX", 12);
            AssertOffset(playerKinematic, "velX", 28);
            AssertOffset(playerKinematic, "flags", 40);

            string scavenger = save + "ExternalScavengerSiteDTO";
            AssertSize(scavenger, 32);
            AssertOffset(scavenger, "chunkX", 0);
            AssertOffset(scavenger, "offsetX", 12);
            AssertOffset(scavenger, "remainingTime", 16);
            AssertOffset(scavenger, "seed", 20);

            string inventory = save + "InventoryShadowDTO";
            AssertSize(inventory, 32);
            AssertOffset(inventory, "cellCount", 0);
            AssertOffset(inventory, "payloadHash", 8);
            AssertOffset(inventory, "totalWeight", 20);
            AssertOffset(inventory, "flags", 24);

            string fauna = save + "ProceduralFaunaStateDTO";
            AssertSize(fauna, 16);
            AssertOffset(fauna, "runtimeKey", 0);
            AssertOffset(fauna, "cooldownUntilPlayTime", 8);
            AssertOffset(fauna, "flags", 12);

            string hibernatedFauna = save + "HibernatedFaunaStateDTO";
            AssertSize(hibernatedFauna, 112);
            AssertOffset(hibernatedFauna, "position", 16);
            AssertOffset(hibernatedFauna, "rotationX", 64);
            AssertOffset(hibernatedFauna, "uniqueInstanceUid", 104);
            AssertOffset(hibernatedFauna, "flags", 108);

            string geologySeam = save + "ProceduralGeologySeamStateDTO";
            AssertSize(geologySeam, 64);
            AssertOffset(geologySeam, "runtimeKey", 0);
            AssertOffset(geologySeam, "chunkX", 8);
            AssertOffset(geologySeam, "absoluteTerrainHeight", 16);
            AssertOffset(geologySeam, "terrainBlendWeight", 28);
            AssertOffset(geologySeam, "absolutePositionX", 36);
            AssertOffset(geologySeam, "absoluteVoxelCenterX", 48);

            string caveEntrance = save + "ProceduralGeologyCaveEntranceDTO";
            AssertSize(caveEntrance, 48);
            AssertOffset(caveEntrance, "runtimeKey", 0);
            AssertOffset(caveEntrance, "surfacePositionX", 8);
            AssertOffset(caveEntrance, "inwardDirectionX", 20);
            AssertOffset(caveEntrance, "radius", 32);
            AssertOffset(caveEntrance, "innerRadius", 40);

            string masterHash = save + "SaveMasterHashV10Result";
            AssertSize(masterHash, 32);
            AssertOffset(masterHash, "PlainLo", 0);
            AssertOffset(masterHash, "PlainHi", 8);
            AssertOffset(masterHash, "StoredLo", 16);
            AssertOffset(masterHash, "StoredHi", 24);

            string saveHeader = save + "SaveFileHeaderV10";
            AssertSize(saveHeader, 72);
            AssertOffset(saveHeader, "MagicValue", 0);
            AssertOffset(saveHeader, "Version", 4);
            AssertOffset(saveHeader, "CompatMask", 6);
            AssertOffset(saveHeader, "Flags", 7);
            AssertOffset(saveHeader, "TimestampUnixMs", 8);
            AssertOffset(saveHeader, "Checksum", 16);
            AssertOffset(saveHeader, "DeltaCount", 20);
            AssertOffset(saveHeader, "EntityCount", 24);
            AssertOffset(saveHeader, "PlayerOffset", 28);
            AssertOffset(saveHeader, "DeltaOffset", 32);
            AssertOffset(saveHeader, "EntityOffset", 36);
            AssertOffset(saveHeader, "HashPayload64", 40);
            AssertOffset(saveHeader, "HashHeader64", 48);
            AssertOffset(saveHeader, "MasterStateHashLo", 56);
            AssertOffset(saveHeader, "MasterStateHashHi", 64);
        }

        private static void VerifySaveMerkleLayouts()
        {
            string save = SaveLayoutNamespace;

            string merkleNode = save + "MerkleNodeDTO";
            AssertSize(merkleNode, 32);
            AssertOffset(merkleNode, "HashLo", 0);
            AssertOffset(merkleNode, "HashHi", 8);
            AssertOffset(merkleNode, "SectorKey", 16);
            AssertOffset(merkleNode, "ChildMask", 20);
            AssertOffset(merkleNode, "_pad0", 24);

            string sectorEntry = save + "SectorEntryDTO";
            AssertSize(sectorEntry, 32);
            AssertOffset(sectorEntry, "SectorHash", 0);
            AssertOffset(sectorEntry, "ByteOffset", 8);
            AssertOffset(sectorEntry, "CompressedSize", 16);
            AssertOffset(sectorEntry, "DecompressedSize", 20);
            AssertOffset(sectorEntry, "Checksum", 24);
            AssertOffset(sectorEntry, "_pad0", 28);

            string stateDelta = save + "StateDeltaRecordDTO";
            AssertSize(stateDelta, 64);
            AssertOffset(stateDelta, "PreviousHashLo", 0);
            AssertOffset(stateDelta, "PreviousHashHi", 8);
            AssertOffset(stateDelta, "NewHashLo", 16);
            AssertOffset(stateDelta, "NewHashHi", 24);
            AssertOffset(stateDelta, "SourceOffsetBytes", 32);
            AssertOffset(stateDelta, "DataLength", 36);
            AssertOffset(stateDelta, "DeltaPayloadOffset", 40);
            AssertOffset(stateDelta, "CompressedOffset", 44);
            AssertOffset(stateDelta, "SectorKey", 48);
            AssertOffset(stateDelta, "Flags", 52);
            AssertOffset(stateDelta, "Crc32", 56);
            AssertOffset(stateDelta, "_pad0", 60);

            string stateLeaf = save + "StateLeafDescriptor";
            AssertSize(stateLeaf, 32);
            AssertOffset(stateLeaf, "SectorKey", 0);
            AssertOffset(stateLeaf, "SourceOffsetBytes", 8);
            AssertOffset(stateLeaf, "TombstoneAliveMask", 24);

            string lz4 = save + "Lz4SubBlockHeader";
            AssertSize(lz4, 32);
            AssertOffset(lz4, "Magic", 0);
            AssertOffset(lz4, "RawBytes", 4);
            AssertOffset(lz4, "StoredBytes", 8);
            AssertOffset(lz4, "SourceOffsetBytes", 12);
            AssertOffset(lz4, "Crc32", 16);
            AssertOffset(lz4, "Flags", 20);
            AssertOffset(lz4, "Version", 24);
            AssertOffset(lz4, "HeaderBytes", 26);
            AssertOffset(lz4, "_pad0", 28);

            string wal = save + "SaveMerkleWalAppendHeader";
            AssertSize(wal, 64);
            AssertOffset(wal, "LogicalOffset", 0);
            AssertOffset(wal, "TimestampTicks", 8);
            AssertOffset(wal, "RootHashLo", 16);
            AssertOffset(wal, "RootHashHi", 24);
            AssertOffset(wal, "RawBytes", 32);
            AssertOffset(wal, "StoredBytes", 36);
            AssertOffset(wal, "Magic", 40);
            AssertOffset(wal, "Flags", 44);
            AssertOffset(wal, "BlockCount", 48);
            AssertOffset(wal, "Frame", 52);
            AssertOffset(wal, "RecordCrc32", 56);
            AssertOffset(wal, "Version", 60);
            AssertOffset(wal, "HeaderBytes", 62);

            string merkleTelemetry = save + "SaveMerkleTelemetryEntry";
            AssertSize(merkleTelemetry, 64);
            AssertOffset(merkleTelemetry, "RootHashLo", 0);
            AssertOffset(merkleTelemetry, "RootHashHi", 8);
            AssertOffset(merkleTelemetry, "TotalBytesHashed", 16);
            AssertOffset(merkleTelemetry, "Frame", 28);
            AssertOffset(merkleTelemetry, "_pad1", 56);

            string merkleEditor = save + "SaveMerkleEditorSnapshot";
            AssertSize(merkleEditor, 80);
            AssertOffset(merkleEditor, "RootHashLo", 0);
            AssertOffset(merkleEditor, "ChangedBranchBits0", 16);
            AssertOffset(merkleEditor, "ChangedLeafCount", 48);
            AssertOffset(merkleEditor, "StoredBytes", 64);
            AssertOffset(merkleEditor, "_pad0", 76);

            string merkleConfig = save + "SaveMerkleRuntimeConfig";
            AssertSize(merkleConfig, 32);
            AssertOffset(merkleConfig, "SubBlockBytes", 0);
            AssertOffset(merkleConfig, "SchemaHash", 20);
            AssertOffset(merkleConfig, "_pad0", 28);

            string mockState = save + "MockStatePayload";
            AssertSize(mockState, 32);
            AssertOffset(mockState, "LocalAup", 0);

            string emergency = save + "SaveMerkleEmergencyHeader64";
            AssertSize(emergency, 64);
            AssertOffset(emergency, "TimestampTicks", 0);
            AssertOffset(emergency, "RootHashLo", 8);
            AssertOffset(emergency, "RootHashHi", 16);
            AssertOffset(emergency, "_pad0", 24);
            AssertOffset(emergency, "_pad1", 32);
            AssertOffset(emergency, "Magic", 40);
            AssertOffset(emergency, "SectorEntryBytes", 44);
            AssertOffset(emergency, "MerkleNodeBytes", 48);
            AssertOffset(emergency, "Flags", 52);
            AssertOffset(emergency, "Checksum", 56);
            AssertOffset(emergency, "Version", 60);
            AssertOffset(emergency, "HeaderBytes", 62);
        }

        private static void VerifySaveStorageLayouts()
        {
            string save = SaveLayoutNamespace;

            string flood = save + "HabitatFloodStateDTO";
            AssertSize(flood, 32);
            AssertOffset(flood, "moduleHashId", 0);
            AssertOffset(flood, "airReserveNormalized", 12);
            AssertOffset(flood, "floodedReefFloodSeconds", 20);
            AssertOffset(flood, "flags", 24);

            string module = save + "ModuleBlitDTO";
            AssertSize(module, 64);
            AssertOffset(module, "prefabHashId", 0);
            AssertOffset(module, "aupGridX", 8);
            AssertOffset(module, "aupLocalX", 32);
            AssertOffset(module, "rotX", 44);
            AssertOffset(module, "health", 60);

            string advisory = save + "PDAContextualAdvisoryDTO";
            AssertSize(advisory, 48);
            AssertOffset(advisory, "issuedFlags", 0);
            AssertOffset(advisory, "deepExposureSeconds", 32);

            string strain = save + "EnvironmentalStrainDTO";
            AssertSize(strain, 16);
            AssertOffset(strain, "microplasticStrain", 0);
            AssertOffset(strain, "recycledPlasticItemCount", 8);

            string graphEdge = save + "ModuleGraphEdgeDTO";
            AssertSize(graphEdge, 16);
            AssertOffset(graphEdge, "sourceNodeIndex", 0);
            AssertOffset(graphEdge, "destinationNodeIndex", 4);

            string chunk = save + "SaveChunkHeader32";
            AssertSize(chunk, 32);
            AssertOffset(chunk, "ChunkKey", 0);
            AssertOffset(chunk, "PayloadLength", 12);
            AssertOffset(chunk, "PayloadHash64", 16);

            string sectorPayload = save + "SectorPayloadDTO";
            AssertSize(sectorPayload, 264);
            AssertOffset(sectorPayload, "SectorHash", 0);
            AssertOffset(sectorPayload, "DataLength", 4);

            AssertExternalContractSize<H8WorldPageReadTicket>(32);
            AssertOffset<H8WorldPageReadTicket>(nameof(H8WorldPageReadTicket.SectorHash), 0);
            AssertOffset<H8WorldPageReadTicket>(nameof(H8WorldPageReadTicket.PayloadType), 8);
            AssertOffset<H8WorldPageReadTicket>(nameof(H8WorldPageReadTicket.RequestId), 12);
            AssertOffset<H8WorldPageReadTicket>(nameof(H8WorldPageReadTicket.Frame), 16);
            AssertOffset<H8WorldPageReadTicket>(nameof(H8WorldPageReadTicket.Status), 24);

            AssertExternalContractSize<H8WorldPagerTelemetrySnapshot>(64);
            AssertOffset<H8WorldPagerTelemetrySnapshot>(nameof(H8WorldPagerTelemetrySnapshot.PendingDiskWrites), 0);
            AssertOffset<H8WorldPagerTelemetrySnapshot>(nameof(H8WorldPagerTelemetrySnapshot.LastSectorHash), 48);
            AssertOffset<H8WorldPagerTelemetrySnapshot>(nameof(H8WorldPagerTelemetrySnapshot.LastPayloadType), 56);

            string binary = save + "SaveBinaryStorage+";
            string binarySector = binary + "SectorEntry";
            AssertSize(binarySector, 32);
            AssertOffset(binarySector, "SectorHash", 0);
            AssertOffset(binarySector, "ByteOffset", 8);
            AssertOffset(binarySector, "Checksum", 24);
            AssertOffset(binarySector, "Reserved0", 28);

            string binaryAupShort = binary + "QuantizedAupLocalOffsetShort3";
            AssertSize(binaryAupShort, 8);
            AssertOffset(binaryAupShort, "XMillimeters", 0);
            AssertOffset(binaryAupShort, "Reserved0", 6);

            string binaryDeltaCell = binary + "DeltaCell";
            AssertSize(binaryDeltaCell, 24);
            AssertOffset(binaryDeltaCell, "UniverseKey", 0);
            AssertOffset(binaryDeltaCell, "SdfValue", 8);
            AssertOffset(binaryDeltaCell, "Reserved1", 20);

            AssertSize(binary + "ThermalGridRleRun", 8);

            AssertSize(save + "AbsoluteUniversePositionV7", 36);
            AssertSize(save + "PayloadPrefixV7", 60);
            AssertSize(save + "PayloadPrefixV8", 72);

        }

        private static void VerifyPersistentWorldLayouts()
        {
            string world = WorldLayoutNamespace;
            string poolSlot = world + "PoolSlotData";
            AssertSize(poolSlot, 72);
            AssertOffset(poolSlot, "BoundGuid", 0);
            AssertOffset(poolSlot, "GridX", 8);
            AssertOffset(poolSlot, "GridY", 16);
            AssertOffset(poolSlot, "GridZ", 24);
            AssertOffset(poolSlot, "LocalOffset", 32);
            AssertOffset(poolSlot, "HydrationFrame", 44);
            AssertOffset(poolSlot, "RefCount", 46);
            AssertOffset(poolSlot, "StateFlags", 47);
            AssertOffset(poolSlot, "StableFrames", 48);
            AssertOffset(poolSlot, "LastVisibleFrame", 50);

            string entity = world + "EntityDataRecord";
            AssertSize(entity, 64);
            AssertOffset(entity, "Position", 0);
            AssertOffset(entity, "Quantity", 48);
            AssertOffset(entity, "Integrity01", 52);
            AssertOffset(entity, "InventoryHash", 56);
            AssertOffset(entity, "InstanceUid", 60);

            string tombstone = world + "ResourceNodeTombstoneRecord";
            AssertSize(tombstone, 80);
            AssertOffset(tombstone, "TombstoneId", 0);
            AssertOffset(tombstone, "Position", 8);
            AssertOffset(tombstone, "ChunkId", 56);
            AssertOffset(tombstone, "InstanceUid", 68);
            AssertOffset(tombstone, "Reserved0", 72);
            AssertOffset(tombstone, "Reserved1", 76);

            string item = world + "PersistentWorldItemRecord";
            AssertSize(item, 256);
            AssertOffset(item, "Position", 0);
            AssertOffset(item, "ItemPersistentIdHash", 48);
            AssertOffset(item, "ItemPersistentId", 56);
            AssertOffset(item, "ChunkId", 184);
            AssertOffset(item, "Quantity", 196);
            AssertOffset(item, "InstanceUid", 200);
            AssertOffset(item, "Flags", 204);

            string delta = world + "PersistentWorldDeltaRecord";
            AssertSize(delta, 64);
            AssertOffset(delta, "ItemPersistentIdHash", 0);
            AssertOffset(delta, "ChunkId", 8);
            AssertOffset(delta, "InstanceUid", 20);
            AssertOffset(delta, "PackedLocalPosition", 24);
            AssertOffset(delta, "Quantity", 28);
            AssertOffset(delta, "ItemFlags", 30);
            AssertOffset(delta, "Reserved", 31);

            string compact = world + "PersistentWorldCompactDeltaRecord";
            AssertSize(compact, 16);
            AssertOffset(compact, "PackedLocalPosition", 0);
            AssertOffset(compact, "InstanceUid", 4);
            AssertOffset(compact, "Quantity", 8);
            AssertOffset(compact, "ItemFlags", 10);
            AssertOffset(compact, "Reserved", 11);
            AssertOffset(compact, "ChunkIndex", 12);
            AssertOffset(compact, "ItemHashIndex", 14);
        }

        private static void VerifySignalLayouts()
        {
            AssertSize<ComplianceViolationSignal>(32);
            AssertOffset<ComplianceViolationSignal>(nameof(ComplianceViolationSignal.RuleHash), 0);
            AssertOffset<ComplianceViolationSignal>(nameof(ComplianceViolationSignal.SystemHash), 4);
            AssertOffset<ComplianceViolationSignal>(nameof(ComplianceViolationSignal.ContextHash), 8);
            AssertOffset<ComplianceViolationSignal>(nameof(ComplianceViolationSignal.Frame), 12);
            AssertOffset<ComplianceViolationSignal>(nameof(ComplianceViolationSignal.Severity), 16);

            string physicsImpact = "Hecton8.Core.Contracts.PhysicsImpactSignal";
            AssertExternalContractSize(physicsImpact, 128);
            AssertOffset(physicsImpact, "PrimaryBodyId", 0);
            AssertOffset(physicsImpact, "SecondaryBodyId", 8);
            AssertOffset(physicsImpact, "_pointAupMeters", 16);
            AssertOffset(physicsImpact, "Point", 64);
            AssertOffset(physicsImpact, "Normal", 76);
            AssertOffset(physicsImpact, "Force", 88);
            AssertOffset(physicsImpact, "Intensity", 92);
            AssertOffset(physicsImpact, "MassVelocity", 96);
            AssertOffset(physicsImpact, "WeightClass", 100);
            AssertOffset(physicsImpact, "PrimaryAudioMaterialId", 101);
            AssertOffset(physicsImpact, "SecondaryAudioMaterialId", 102);
            AssertOffset(physicsImpact, "_hasPointAup", 103);
        }

        private static void VerifyRenderBlitLayouts()
        {
            string construction = ConstructionLayoutNamespace;
            string ghost = construction + "BuilderGhostStateDTO";
            AssertSize(ghost, 128);
            AssertOffset(ghost, "LocalToWorld", 0);
            AssertOffset(ghost, "AUP_TargetPosition", 64);
            AssertOffset(ghost, "PrefabHashID", 88);
            AssertOffset(ghost, "ValidationFlags", 92);
            AssertOffset(ghost, "AnimationPhase", 96);
            AssertOffset(ghost, "ValidationStateHash", 100);

            AssertSize(construction + "BuilderGhostVisualDTO", 64);
            AssertSize(construction + "HolographyTelemetryEntry", 64);

            string ghostArgs = construction + "BuilderGhostIndirectArgsDTO";
            AssertSize(ghostArgs, 16);
            AssertOffset(ghostArgs, "VertexCountPerInstance", 0);
            AssertOffset(ghostArgs, "InstanceCount", 4);
            AssertOffset(ghostArgs, "StartVertex", 8);
            AssertOffset(ghostArgs, "StartInstance", 12);

            string cultivationSlot = construction + "CultivationManager+CultivationSlotState";
            AssertSize(cultivationSlot, 32);
            AssertOffset(cultivationSlot, "GeneticsMask", 0);
            AssertOffset(cultivationSlot, "SeedItemHashId", 8);
            AssertOffset(cultivationSlot, "Growth01", 12);
            AssertOffset(cultivationSlot, "Quality01", 16);
        }

        private static void AssertSize<T>(int expected) where T : unmanaged
        {
            AssertBinarySafe<T>();
            int observed = UnsafeUtility.SizeOf<T>();
            UnityEngine.Debug.Assert(observed == expected, ResolveTypeName<T>());
            if (observed != expected)
                Fail(ResolveTypeName<T>(), expected, observed, CombineHash(SizeContextHash, ResolveTypeHash<T>()));
        }

        private static void AssertExternalContractSize<T>(int expected) where T : unmanaged
        {
            AssertBlittable<T>();
            int observed = UnsafeUtility.SizeOf<T>();
            UnityEngine.Debug.Assert(observed == expected, ResolveTypeName<T>());
            if (observed != expected)
                Fail(ResolveTypeName<T>(), expected, observed, CombineHash(SizeContextHash, ResolveTypeHash<T>()));
        }

        private static void AssertSize(string typeName, int expected)
        {
            Type type = ResolveLayoutType(typeName);
            AssertBinarySafe(type);
            int observed = Marshal.SizeOf(type);
            UnityEngine.Debug.Assert(observed == expected, typeName);
            if (observed != expected)
                Fail(typeName, expected, observed, CombineHash(SizeContextHash, ResolveTypeHash(type)));
        }

        private static void AssertExternalContractSize(string typeName, int expected)
        {
            Type type = ResolveLayoutType(typeName);
            AssertBlittable(type);
            int observed = Marshal.SizeOf(type);
            UnityEngine.Debug.Assert(observed == expected, typeName);
            if (observed != expected)
                Fail(typeName, expected, observed, CombineHash(SizeContextHash, ResolveTypeHash(type)));
        }

        private static void AssertOffset<T>(string fieldName, int expected) where T : unmanaged
        {
            int observed = Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
            UnityEngine.Debug.Assert(observed == expected, ResolveTypeName<T>());
            if (observed != expected)
                Fail(ResolveTypeName<T>(), expected, observed, CombineHash(OffsetContextHash, ComputeFnv1A32(fieldName)));
        }

        private static void AssertOffset(string typeName, string fieldName, int expected)
        {
            Type type = ResolveLayoutType(typeName);
            int observed = Marshal.OffsetOf(type, fieldName).ToInt32();
            UnityEngine.Debug.Assert(observed == expected, typeName);
            if (observed != expected)
                Fail(typeName, expected, observed, CombineHash(OffsetContextHash, ComputeFnv1A32(fieldName)));
        }

        private static void AssertBinarySafe<T>() where T : unmanaged
        {
            AssertBlittable<T>();

            if (!MemoryInquisitor.PrewarmBinaryBlittableSafety<T>())
                Fail(ResolveTypeName<T>(), expected: 1, observed: 0, CombineHash(AttributeContextHash, ResolveTypeHash<T>()));
        }

        private static void AssertBinarySafe(Type type)
        {
            AssertBlittable(type);

            if (!type.IsDefined(typeof(BinaryBlittableSafeAttribute), false))
                Fail(ResolveTypeName(type), expected: 1, observed: 0, CombineHash(AttributeContextHash, ResolveTypeHash(type)));
        }

        private static void AssertBlittable<T>() where T : unmanaged
        {
            if (!UnsafeUtility.IsBlittable<T>())
                Fail(ResolveTypeName<T>(), expected: 1, observed: 0, CombineHash(BlittableContextHash, ResolveTypeHash<T>()));
        }

        private static void AssertBlittable(Type type)
        {
            if (!IsBlittableType(type, depth: 0))
                Fail(ResolveTypeName(type), expected: 1, observed: 0, CombineHash(BlittableContextHash, ResolveTypeHash(type)));
        }

        private static bool IsBlittableType(Type type, int depth)
        {
            if (type == null || depth > 16)
                return false;

            if (type.IsPointer || type.IsEnum)
                return true;

            if (type.IsPrimitive)
                return type != typeof(bool) && type != typeof(char);

            if (!type.IsValueType)
                return false;

            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
            {
                if (!IsBlittableType(fields[i].FieldType, depth + 1))
                    return false;
            }

            return true;
        }

        private static Type ResolveLayoutType(string typeName)
        {
            Type type = Type.GetType(typeName, throwOnError: false);
            if (type != null)
                return type;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(typeName, throwOnError: false);
                if (type != null)
                    return type;
            }

            Fail(typeName, expected: 1, observed: 0, CombineHash(AttributeContextHash, ComputeFnv1A32(typeName)));
            return typeof(void);
        }

        private static void Fail(string structName, int expected, int observed, uint contextHash)
        {
            PublishComplianceViolation(contextHash);
            DumpFailure(structName, expected, observed, contextHash);
            throw new CriticalBootException("[BinaryLayoutManifest] Binary layout validation failed: " + structName);
        }

        private static void PublishComplianceViolation(uint contextHash)
        {
            ComplianceViolationSignal signal = new ComplianceViolationSignal
            {
                RuleHash = LayoutRuleHash,
                SystemHash = LayoutSystemHash,
                ContextHash = contextHash,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Severity = 3,
                Flags = 1
            };
            SignalBus<ComplianceViolationSignal>.TryPushTracked(in signal, ref s_x001BinaryLayoutManifestSignalPushDropCount);
        }

        private static unsafe void DumpFailure(string structName, int expected, int observed, uint contextHash)
        {
            string safeName = structName ?? string.Empty;
            int nameBytes = safeName.Length;
            if (nameBytes > DumpTypeNameMaxBytes)
                nameBytes = DumpTypeNameMaxBytes;

            int byteCount = DumpHeaderBytes + nameBytes;
            NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                byteCount,
                nameof(BinaryLayoutManifest),
                DumpPayloadLabel,
                NativeArrayOptions.UninitializedMemory);

            try
            {
                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                WriteUInt32LittleEndian(target, 0, DumpMagic);
                WriteInt32LittleEndian(target, 4, DumpVersion);
                WriteUInt32LittleEndian(target, 8, contextHash);
                WriteInt32LittleEndian(target, 12, expected);
                WriteInt32LittleEndian(target, 16, observed);
                WriteInt32LittleEndian(target, 20, nameBytes);
                WriteUInt32LittleEndian(target, 24, ComputeFnv1A32(safeName));
                for (int i = 0; i < nameBytes; i++)
                {
                    char c = safeName[i];
                    target[DumpHeaderBytes + i] = c <= 0x7F ? (byte)c : (byte)'?';
                }

                NativeFaultDumpWriter.TryWriteAll(DumpRelativePath, payload, byteCount);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(ref payload, nameof(BinaryLayoutManifest), DumpPayloadLabel);
            }
        }

        private static unsafe void WriteInt32LittleEndian(byte* target, int offset, int value)
        {
            WriteUInt32LittleEndian(target, offset, unchecked((uint)value));
        }

        private static unsafe void WriteUInt32LittleEndian(byte* target, int offset, uint value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
        }

        private static string ResolveTypeName<T>() where T : unmanaged
        {
            return typeof(T).FullName ?? typeof(T).Name;
        }

        private static string ResolveTypeName(Type type)
        {
            return type.FullName ?? type.Name;
        }

        private static uint ResolveTypeHash<T>() where T : unmanaged
        {
            return ComputeFnv1A32(ResolveTypeName<T>());
        }

        private static uint ResolveTypeHash(Type type)
        {
            return ComputeFnv1A32(ResolveTypeName(type));
        }

        private static uint CombineHash(uint left, uint right)
        {
            return unchecked((left * 16777619u) ^ right);
        }

        private static uint ComputeFnv1A32(string value)
        {
            uint hash = 2166136261u;
            if (string.IsNullOrEmpty(value))
                return hash;

            for (int i = 0; i < value.Length; i++)
                hash = unchecked((hash ^ value[i]) * 16777619u);

            return hash;
        }
    }
}
