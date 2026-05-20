using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Construction;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Cold-boot binary layout verifier for structs used by memcpy, save paging, AUP, and native telemetry lanes.
    /// </summary>
    public static class BinaryLayoutManifest
    {
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
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_BINARY_LAYOUT_SENTINEL.bin";
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
            VerifyRenderBlitLayouts();
            VerifyAmbientBiotaLayouts();

            _verified = true;
        }

        private static void VerifyAupLayouts()
        {
            AssertSize<AbsoluteUniversePosition>(48);
            AssertOffset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.GridX), 0);
            AssertOffset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.GridY), 8);
            AssertOffset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.GridZ), 16);
            AssertOffset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.LocalX), 24);
            AssertOffset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.LocalY), 28);
            AssertOffset<AbsoluteUniversePosition>(nameof(AbsoluteUniversePosition.LocalZ), 32);

            AssertSize<AbsoluteUniversePositionBlit>(48);
            AssertOffset<AbsoluteUniversePositionBlit>(nameof(AbsoluteUniversePositionBlit.GridX), 0);
            AssertOffset<AbsoluteUniversePositionBlit>(nameof(AbsoluteUniversePositionBlit.Local), 24);
            AssertOffset<AbsoluteUniversePositionBlit>(nameof(AbsoluteUniversePositionBlit.Reserved1), 40);

            AssertSize<AbsoluteUniversePositionBlit128>(48);
            AssertOffset<AbsoluteUniversePositionBlit128>(nameof(AbsoluteUniversePositionBlit128.GridX), 0);
            AssertOffset<AbsoluteUniversePositionBlit128>(nameof(AbsoluteUniversePositionBlit128.Local), 24);
            AssertOffset<AbsoluteUniversePositionBlit128>(nameof(AbsoluteUniversePositionBlit128.Reserved), 40);
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

        private static void VerifySaveLayouts()
        {
            AssertSize<SaveVoxelDeltaRun5>(8);
            AssertOffset<SaveVoxelDeltaRun5>(nameof(SaveVoxelDeltaRun5.StartIndex), 0);
            AssertOffset<SaveVoxelDeltaRun5>(nameof(SaveVoxelDeltaRun5.RunLength), 2);
            AssertOffset<SaveVoxelDeltaRun5>(nameof(SaveVoxelDeltaRun5.SdfValue), 4);

            AssertSize<SaveVoxelDeltaRun8>(8);
            AssertOffset<SaveVoxelDeltaRun8>(nameof(SaveVoxelDeltaRun8.StartIndex), 0);
            AssertOffset<SaveVoxelDeltaRun8>(nameof(SaveVoxelDeltaRun8.RunLength), 2);
            AssertOffset<SaveVoxelDeltaRun8>(nameof(SaveVoxelDeltaRun8.SdfValue), 4);
            AssertOffset<SaveVoxelDeltaRun8>(nameof(SaveVoxelDeltaRun8.MaterialId), 5);
            AssertOffset<SaveVoxelDeltaRun8>(nameof(SaveVoxelDeltaRun8.Flags), 6);

            AssertSize<VoxelDeltaCellDTO>(24);
            AssertOffset<VoxelDeltaCellDTO>(nameof(VoxelDeltaCellDTO.universeKey), 0);
            AssertOffset<VoxelDeltaCellDTO>(nameof(VoxelDeltaCellDTO.sdfValue), 8);
            AssertOffset<VoxelDeltaCellDTO>(nameof(VoxelDeltaCellDTO.materialId), 12);
            AssertOffset<VoxelDeltaCellDTO>(nameof(VoxelDeltaCellDTO.flags), 13);
            AssertOffset<VoxelDeltaCellDTO>(nameof(VoxelDeltaCellDTO.metadata), 14);
            AssertOffset<VoxelDeltaCellDTO>(nameof(VoxelDeltaCellDTO.reserved), 16);
            AssertOffset<VoxelDeltaCellDTO>(nameof(VoxelDeltaCellDTO._pad0), 20);

            AssertSize<VoxelCarvingOperationDTO>(24);
            AssertOffset<VoxelCarvingOperationDTO>(nameof(VoxelCarvingOperationDTO.localPosition), 0);
            AssertOffset<VoxelCarvingOperationDTO>(nameof(VoxelCarvingOperationDTO.radius), 12);
            AssertOffset<VoxelCarvingOperationDTO>(nameof(VoxelCarvingOperationDTO.operation), 16);
            AssertOffset<VoxelCarvingOperationDTO>(nameof(VoxelCarvingOperationDTO.materialId), 17);
            AssertOffset<VoxelCarvingOperationDTO>(nameof(VoxelCarvingOperationDTO.flags), 18);
            AssertOffset<VoxelCarvingOperationDTO>(nameof(VoxelCarvingOperationDTO.sequence), 20);

            AssertSize<VoxelDeltaRleRunDTO>(8);
            AssertOffset<VoxelDeltaRleRunDTO>(nameof(VoxelDeltaRleRunDTO.StartIndex), 0);
            AssertOffset<VoxelDeltaRleRunDTO>(nameof(VoxelDeltaRleRunDTO.RunLength), 2);
            AssertOffset<VoxelDeltaRleRunDTO>(nameof(VoxelDeltaRleRunDTO.SdfValue), 4);
            AssertOffset<VoxelDeltaRleRunDTO>(nameof(VoxelDeltaRleRunDTO.MaterialId), 5);
            AssertOffset<VoxelDeltaRleRunDTO>(nameof(VoxelDeltaRleRunDTO.Flags), 6);

            AssertSize<VoxelDeltaHeaderDTO>(32);
            AssertOffset<VoxelDeltaHeaderDTO>(nameof(VoxelDeltaHeaderDTO.SectorHash), 0);
            AssertOffset<VoxelDeltaHeaderDTO>(nameof(VoxelDeltaHeaderDTO.CompressedSize), 8);
            AssertOffset<VoxelDeltaHeaderDTO>(nameof(VoxelDeltaHeaderDTO.UncompressedSize), 12);
            AssertOffset<VoxelDeltaHeaderDTO>(nameof(VoxelDeltaHeaderDTO.XXHash3Checksum), 16);
            AssertOffset<VoxelDeltaHeaderDTO>(nameof(VoxelDeltaHeaderDTO._pad0), 24);
            AssertOffset<VoxelDeltaHeaderDTO>(nameof(VoxelDeltaHeaderDTO._pad1), 28);

            AssertSize<VoxelDeltaBlockCounter64>(64);
            AssertOffset<VoxelDeltaBlockCounter64>(nameof(VoxelDeltaBlockCounter64.RunCount), 0);
            AssertOffset<VoxelDeltaBlockCounter64>(nameof(VoxelDeltaBlockCounter64.SectorHash), 16);

            AssertSize<VoxelDeltaCompressionTelemetryEntry>(64);
            AssertOffset<VoxelDeltaCompressionTelemetryEntry>(nameof(VoxelDeltaCompressionTelemetryEntry.SectorHash), 0);
            AssertOffset<VoxelDeltaCompressionTelemetryEntry>(nameof(VoxelDeltaCompressionTelemetryEntry.PayloadHash), 8);
            AssertOffset<VoxelDeltaCompressionTelemetryEntry>(nameof(VoxelDeltaCompressionTelemetryEntry.GlobalQualityWeight), 40);

            AssertSize<VoxelDeltaTelemetryDumpHeaderDTO>(64);
            AssertOffset<VoxelDeltaTelemetryDumpHeaderDTO>(nameof(VoxelDeltaTelemetryDumpHeaderDTO.Magic), 0);
            AssertOffset<VoxelDeltaTelemetryDumpHeaderDTO>(nameof(VoxelDeltaTelemetryDumpHeaderDTO.EntryStride), 12);
            AssertOffset<VoxelDeltaTelemetryDumpHeaderDTO>(nameof(VoxelDeltaTelemetryDumpHeaderDTO.FirstSectorHash), 32);
            AssertOffset<VoxelDeltaTelemetryDumpHeaderDTO>(nameof(VoxelDeltaTelemetryDumpHeaderDTO.LastFrame), 52);

            AssertSize<VoxelDeltaCompressionTuningDTO>(64);
            AssertOffset<VoxelDeltaCompressionTuningDTO>(nameof(VoxelDeltaCompressionTuningDTO.ProfileHash), 0);
            AssertOffset<VoxelDeltaCompressionTuningDTO>(nameof(VoxelDeltaCompressionTuningDTO.PruneThreshold01), 16);
            AssertOffset<VoxelDeltaCompressionTuningDTO>(nameof(VoxelDeltaCompressionTuningDTO.MaxBytesPerFrame), 48);
            AssertOffset<VoxelDeltaCompressionTuningDTO>(nameof(VoxelDeltaCompressionTuningDTO.DepthMinMeters), 52);
            AssertOffset<VoxelDeltaCompressionTuningDTO>(nameof(VoxelDeltaCompressionTuningDTO.DepthMaxMeters), 56);
            AssertOffset<VoxelDeltaCompressionTuningDTO>(nameof(VoxelDeltaCompressionTuningDTO._pad0), 60);

            AssertSize<VoxelDeltaSectorStatsDTO>(64);
            AssertOffset<VoxelDeltaSectorStatsDTO>(nameof(VoxelDeltaSectorStatsDTO.SectorHash), 0);
            AssertOffset<VoxelDeltaSectorStatsDTO>(nameof(VoxelDeltaSectorStatsDTO.ModifiedRatio01), 36);
            AssertOffset<VoxelDeltaSectorStatsDTO>(nameof(VoxelDeltaSectorStatsDTO._pad1), 56);

            AssertSize<VoxelDeltaDearLieStateDTO>(32);
            AssertOffset<VoxelDeltaDearLieStateDTO>(nameof(VoxelDeltaDearLieStateDTO.SectorHash), 0);
            AssertOffset<VoxelDeltaDearLieStateDTO>(nameof(VoxelDeltaDearLieStateDTO.VisualFade01), 16);

            AssertSize<VoxelDeltaMockSchemaDTO>(64);
            AssertOffset<VoxelDeltaMockSchemaDTO>(nameof(VoxelDeltaMockSchemaDTO.Magic), 0);
            AssertOffset<VoxelDeltaMockSchemaDTO>(nameof(VoxelDeltaMockSchemaDTO.Seed), 40);

            AssertSize<EntityDeltaHeaderDTO>(32);
            AssertOffset<EntityDeltaHeaderDTO>(nameof(EntityDeltaHeaderDTO.SectorHash), 0);
            AssertOffset<EntityDeltaHeaderDTO>(nameof(EntityDeltaHeaderDTO.CompressedSize), 8);
            AssertOffset<EntityDeltaHeaderDTO>(nameof(EntityDeltaHeaderDTO.UncompressedSize), 12);
            AssertOffset<EntityDeltaHeaderDTO>(nameof(EntityDeltaHeaderDTO.XXHash3Checksum), 16);
            AssertOffset<EntityDeltaHeaderDTO>(nameof(EntityDeltaHeaderDTO._pad0), 24);
            AssertOffset<EntityDeltaHeaderDTO>(nameof(EntityDeltaHeaderDTO._pad1), 28);

            AssertSize<EntityDeltaRleStreamHeaderDTO>(16);
            AssertOffset<EntityDeltaRleStreamHeaderDTO>(nameof(EntityDeltaRleStreamHeaderDTO.Magic), 0);
            AssertOffset<EntityDeltaRleStreamHeaderDTO>(nameof(EntityDeltaRleStreamHeaderDTO.Flags), 4);
            AssertOffset<EntityDeltaRleStreamHeaderDTO>(nameof(EntityDeltaRleStreamHeaderDTO.DenseBytes), 8);
            AssertOffset<EntityDeltaRleStreamHeaderDTO>(nameof(EntityDeltaRleStreamHeaderDTO.StoredBytes), 12);

            AssertSize<EntityDeltaDataRecordDTO>(80);
            AssertOffset<EntityDeltaDataRecordDTO>(nameof(EntityDeltaDataRecordDTO.SectorX), 0);
            AssertOffset<EntityDeltaDataRecordDTO>(nameof(EntityDeltaDataRecordDTO.SectorY), 8);
            AssertOffset<EntityDeltaDataRecordDTO>(nameof(EntityDeltaDataRecordDTO.SectorZ), 16);
            AssertOffset<EntityDeltaDataRecordDTO>(nameof(EntityDeltaDataRecordDTO.LocalX), 24);
            AssertOffset<EntityDeltaDataRecordDTO>(nameof(EntityDeltaDataRecordDTO.EntityKindHash), 36);
            AssertOffset<EntityDeltaDataRecordDTO>(nameof(EntityDeltaDataRecordDTO.StableEntityHash), 40);
            AssertOffset<EntityDeltaDataRecordDTO>(nameof(EntityDeltaDataRecordDTO.InstanceUid), 56);
            AssertOffset<EntityDeltaDataRecordDTO>(nameof(EntityDeltaDataRecordDTO.Flags), 68);
            AssertOffset<EntityDeltaDataRecordDTO>(nameof(EntityDeltaDataRecordDTO.SimulationTick), 76);

            AssertSize<EntityDeltaBlockCounter64>(64);
            AssertOffset<EntityDeltaBlockCounter64>(nameof(EntityDeltaBlockCounter64.DeltaCount), 0);
            AssertOffset<EntityDeltaBlockCounter64>(nameof(EntityDeltaBlockCounter64.SectorHash), 16);
            AssertOffset<EntityDeltaBlockCounter64>(nameof(EntityDeltaBlockCounter64.HashXor), 32);

            AssertSize<EntityCompressionTelemetryEntry>(64);
            AssertOffset<EntityCompressionTelemetryEntry>(nameof(EntityCompressionTelemetryEntry.SectorHash), 0);
            AssertOffset<EntityCompressionTelemetryEntry>(nameof(EntityCompressionTelemetryEntry.PayloadHash), 8);
            AssertOffset<EntityCompressionTelemetryEntry>(nameof(EntityCompressionTelemetryEntry.GlobalQualityWeight), 48);

            AssertSize<EntityDeltaCompressionTuningDTO>(64);
            AssertOffset<EntityDeltaCompressionTuningDTO>(nameof(EntityDeltaCompressionTuningDTO.ProfileHash), 0);
            AssertOffset<EntityDeltaCompressionTuningDTO>(nameof(EntityDeltaCompressionTuningDTO.TombstoneMaxDays), 16);
            AssertOffset<EntityDeltaCompressionTuningDTO>(nameof(EntityDeltaCompressionTuningDTO.MaxBytesPerFrame), 44);
            AssertOffset<EntityDeltaCompressionTuningDTO>(nameof(EntityDeltaCompressionTuningDTO._pad0), 60);

            AssertSize<EntityDeltaSectorStatsDTO>(64);
            AssertOffset<EntityDeltaSectorStatsDTO>(nameof(EntityDeltaSectorStatsDTO.SectorHash), 0);
            AssertOffset<EntityDeltaSectorStatsDTO>(nameof(EntityDeltaSectorStatsDTO.FullSnapshotBytes), 20);
            AssertOffset<EntityDeltaSectorStatsDTO>(nameof(EntityDeltaSectorStatsDTO.CompressionRatio01), 44);
            AssertOffset<EntityDeltaSectorStatsDTO>(nameof(EntityDeltaSectorStatsDTO._pad0), 56);

            AssertSize<EntityCompressionProfileDTO>(32);
            AssertOffset<EntityCompressionProfileDTO>(nameof(EntityCompressionProfileDTO.ProfileHash), 0);
            AssertOffset<EntityCompressionProfileDTO>(nameof(EntityCompressionProfileDTO.EntityKindHash), 8);
            AssertOffset<EntityCompressionProfileDTO>(nameof(EntityCompressionProfileDTO.StateMask), 24);

            AssertSize<EntityDeltaMockSchemaDTO>(64);
            AssertOffset<EntityDeltaMockSchemaDTO>(nameof(EntityDeltaMockSchemaDTO.Magic), 0);
            AssertOffset<EntityDeltaMockSchemaDTO>(nameof(EntityDeltaMockSchemaDTO.Seed), 40);

            AssertSize<EntityDeltaTelemetryDumpHeaderDTO>(64);
            AssertOffset<EntityDeltaTelemetryDumpHeaderDTO>(nameof(EntityDeltaTelemetryDumpHeaderDTO.Magic), 0);
            AssertOffset<EntityDeltaTelemetryDumpHeaderDTO>(nameof(EntityDeltaTelemetryDumpHeaderDTO.EntryStride), 12);
            AssertOffset<EntityDeltaTelemetryDumpHeaderDTO>(nameof(EntityDeltaTelemetryDumpHeaderDTO.FirstSectorHash), 32);

            AssertSize<PackedEntityState32>(8);
            AssertSize<PackedSuitUpgradeState64>(8);
            AssertSize<QuantizedLocalHalf3>(8);
            AssertOffset<QuantizedLocalHalf3>(nameof(QuantizedLocalHalf3.X), 0);
            AssertOffset<QuantizedLocalHalf3>(nameof(QuantizedLocalHalf3.Y), 2);
            AssertOffset<QuantizedLocalHalf3>(nameof(QuantizedLocalHalf3.Z), 4);

            AssertSize<QuantizedAupSectorHalf3>(24);
            AssertOffset<QuantizedAupSectorHalf3>(nameof(QuantizedAupSectorHalf3.SectorX), 0);
            AssertOffset<QuantizedAupSectorHalf3>(nameof(QuantizedAupSectorHalf3.LocalOffset), 12);

            AssertSize<SaveAupLocalOffset32>(32);
            AssertOffset<SaveAupLocalOffset32>(nameof(SaveAupLocalOffset32.SectorKey), 0);
            AssertOffset<SaveAupLocalOffset32>(nameof(SaveAupLocalOffset32.ShiftFrameId), 4);
            AssertOffset<SaveAupLocalOffset32>(nameof(SaveAupLocalOffset32.LocalOffsetX), 8);
            AssertOffset<SaveAupLocalOffset32>(nameof(SaveAupLocalOffset32.LocalOffsetY), 12);
            AssertOffset<SaveAupLocalOffset32>(nameof(SaveAupLocalOffset32.LocalOffsetZ), 16);
            AssertOffset<SaveAupLocalOffset32>(nameof(SaveAupLocalOffset32.Flags), 20);
            AssertOffset<SaveAupLocalOffset32>(nameof(SaveAupLocalOffset32._pad0), 24);
            AssertOffset<SaveAupLocalOffset32>(nameof(SaveAupLocalOffset32._pad1), 28);

            AssertSize<StrictSaveFileHeader64>(64);
            AssertOffset<StrictSaveFileHeader64>(nameof(StrictSaveFileHeader64.Magic), 0);
            AssertOffset<StrictSaveFileHeader64>(nameof(StrictSaveFileHeader64.PlayTimeSeconds), 8);
            AssertOffset<StrictSaveFileHeader64>(nameof(StrictSaveFileHeader64.AupX), 16);
            AssertOffset<StrictSaveFileHeader64>(nameof(StrictSaveFileHeader64.Checksum), 40);
            AssertOffset<StrictSaveFileHeader64>(nameof(StrictSaveFileHeader64.Version), 48);

            AssertSize<PlayerKinematicStateDTO>(48);
            AssertOffset<PlayerKinematicStateDTO>(nameof(PlayerKinematicStateDTO.posX), 0);
            AssertOffset<PlayerKinematicStateDTO>(nameof(PlayerKinematicStateDTO.rotX), 12);
            AssertOffset<PlayerKinematicStateDTO>(nameof(PlayerKinematicStateDTO.velX), 28);
            AssertOffset<PlayerKinematicStateDTO>(nameof(PlayerKinematicStateDTO.flags), 40);

            AssertSize<ExternalScavengerSiteDTO>(32);
            AssertOffset<ExternalScavengerSiteDTO>(nameof(ExternalScavengerSiteDTO.chunkX), 0);
            AssertOffset<ExternalScavengerSiteDTO>(nameof(ExternalScavengerSiteDTO.offsetX), 12);
            AssertOffset<ExternalScavengerSiteDTO>(nameof(ExternalScavengerSiteDTO.remainingTime), 16);
            AssertOffset<ExternalScavengerSiteDTO>(nameof(ExternalScavengerSiteDTO.seed), 20);

            AssertSize<InventoryShadowDTO>(32);
            AssertOffset<InventoryShadowDTO>(nameof(InventoryShadowDTO.cellCount), 0);
            AssertOffset<InventoryShadowDTO>(nameof(InventoryShadowDTO.payloadHash), 8);
            AssertOffset<InventoryShadowDTO>(nameof(InventoryShadowDTO.totalWeight), 20);
            AssertOffset<InventoryShadowDTO>(nameof(InventoryShadowDTO.flags), 24);

            AssertSize<ProceduralFaunaStateDTO>(16);
            AssertOffset<ProceduralFaunaStateDTO>(nameof(ProceduralFaunaStateDTO.runtimeKey), 0);
            AssertOffset<ProceduralFaunaStateDTO>(nameof(ProceduralFaunaStateDTO.cooldownUntilPlayTime), 8);
            AssertOffset<ProceduralFaunaStateDTO>(nameof(ProceduralFaunaStateDTO.flags), 12);

            AssertSize<HibernatedFaunaStateDTO>(112);
            AssertOffset<HibernatedFaunaStateDTO>(nameof(HibernatedFaunaStateDTO.position), 16);
            AssertOffset<HibernatedFaunaStateDTO>(nameof(HibernatedFaunaStateDTO.rotationX), 64);
            AssertOffset<HibernatedFaunaStateDTO>(nameof(HibernatedFaunaStateDTO.uniqueInstanceUid), 104);
            AssertOffset<HibernatedFaunaStateDTO>(nameof(HibernatedFaunaStateDTO.flags), 108);

            AssertSize<ProceduralGeologySeamStateDTO>(64);
            AssertOffset<ProceduralGeologySeamStateDTO>(nameof(ProceduralGeologySeamStateDTO.runtimeKey), 0);
            AssertOffset<ProceduralGeologySeamStateDTO>(nameof(ProceduralGeologySeamStateDTO.chunkX), 8);
            AssertOffset<ProceduralGeologySeamStateDTO>(nameof(ProceduralGeologySeamStateDTO.absoluteTerrainHeight), 16);
            AssertOffset<ProceduralGeologySeamStateDTO>(nameof(ProceduralGeologySeamStateDTO.terrainBlendWeight), 28);
            AssertOffset<ProceduralGeologySeamStateDTO>(nameof(ProceduralGeologySeamStateDTO.absolutePositionX), 36);
            AssertOffset<ProceduralGeologySeamStateDTO>(nameof(ProceduralGeologySeamStateDTO.absoluteVoxelCenterX), 48);

            AssertSize<ProceduralGeologyCaveEntranceDTO>(48);
            AssertOffset<ProceduralGeologyCaveEntranceDTO>(nameof(ProceduralGeologyCaveEntranceDTO.runtimeKey), 0);
            AssertOffset<ProceduralGeologyCaveEntranceDTO>(nameof(ProceduralGeologyCaveEntranceDTO.surfacePositionX), 8);
            AssertOffset<ProceduralGeologyCaveEntranceDTO>(nameof(ProceduralGeologyCaveEntranceDTO.inwardDirectionX), 20);
            AssertOffset<ProceduralGeologyCaveEntranceDTO>(nameof(ProceduralGeologyCaveEntranceDTO.radius), 32);
            AssertOffset<ProceduralGeologyCaveEntranceDTO>(nameof(ProceduralGeologyCaveEntranceDTO.innerRadius), 40);

            AssertSize<SaveMasterHashV10Result>(32);
            AssertOffset<SaveMasterHashV10Result>(nameof(SaveMasterHashV10Result.PlainLo), 0);
            AssertOffset<SaveMasterHashV10Result>(nameof(SaveMasterHashV10Result.PlainHi), 8);
            AssertOffset<SaveMasterHashV10Result>(nameof(SaveMasterHashV10Result.StoredLo), 16);
            AssertOffset<SaveMasterHashV10Result>(nameof(SaveMasterHashV10Result.StoredHi), 24);

            AssertSize<SaveFileHeaderV10>(72);
            AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.MagicValue), 0);
            AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.Version), 4);
            AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.CompatMask), 6);
            AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.Flags), 7);
            AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.TimestampUnixMs), 8);
            AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.Checksum), 16);
            AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.DeltaCount), 20);
            AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.EntityCount), 24);
            AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.PlayerOffset), 28);
            AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.DeltaOffset), 32);
            AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.EntityOffset), 36);
            AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.HashPayload64), 40);
            AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.HashHeader64), 48);
            AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.MasterStateHashLo), SaveMasterHashV10.MasterStateHashLoOffset);
            AssertOffset<SaveFileHeaderV10>(nameof(SaveFileHeaderV10.MasterStateHashHi), SaveMasterHashV10.MasterStateHashHiOffset);

            AssertSize<MerkleNodeDTO>(32);
            AssertOffset<MerkleNodeDTO>(nameof(MerkleNodeDTO.HashLo), 0);
            AssertOffset<MerkleNodeDTO>(nameof(MerkleNodeDTO.HashHi), 8);
            AssertOffset<MerkleNodeDTO>(nameof(MerkleNodeDTO.SectorKey), 16);
            AssertOffset<MerkleNodeDTO>(nameof(MerkleNodeDTO.ChildMask), 20);
            AssertOffset<MerkleNodeDTO>(nameof(MerkleNodeDTO._pad0), 24);

            AssertSize<SectorEntryDTO>(32);
            AssertOffset<SectorEntryDTO>(nameof(SectorEntryDTO.SectorHash), 0);
            AssertOffset<SectorEntryDTO>(nameof(SectorEntryDTO.ByteOffset), 8);
            AssertOffset<SectorEntryDTO>(nameof(SectorEntryDTO.CompressedSize), 16);
            AssertOffset<SectorEntryDTO>(nameof(SectorEntryDTO.DecompressedSize), 20);
            AssertOffset<SectorEntryDTO>(nameof(SectorEntryDTO.Checksum), 24);
            AssertOffset<SectorEntryDTO>(nameof(SectorEntryDTO._pad0), 28);

            AssertSize<StateDeltaRecordDTO>(64);
            AssertOffset<StateDeltaRecordDTO>(nameof(StateDeltaRecordDTO.PreviousHashLo), 0);
            AssertOffset<StateDeltaRecordDTO>(nameof(StateDeltaRecordDTO.PreviousHashHi), 8);
            AssertOffset<StateDeltaRecordDTO>(nameof(StateDeltaRecordDTO.NewHashLo), 16);
            AssertOffset<StateDeltaRecordDTO>(nameof(StateDeltaRecordDTO.NewHashHi), 24);
            AssertOffset<StateDeltaRecordDTO>(nameof(StateDeltaRecordDTO.SourceOffsetBytes), 32);
            AssertOffset<StateDeltaRecordDTO>(nameof(StateDeltaRecordDTO.DataLength), 36);
            AssertOffset<StateDeltaRecordDTO>(nameof(StateDeltaRecordDTO.DeltaPayloadOffset), 40);
            AssertOffset<StateDeltaRecordDTO>(nameof(StateDeltaRecordDTO.CompressedOffset), 44);
            AssertOffset<StateDeltaRecordDTO>(nameof(StateDeltaRecordDTO.SectorKey), 48);
            AssertOffset<StateDeltaRecordDTO>(nameof(StateDeltaRecordDTO.Flags), 52);
            AssertOffset<StateDeltaRecordDTO>(nameof(StateDeltaRecordDTO.Crc32), 56);
            AssertOffset<StateDeltaRecordDTO>(nameof(StateDeltaRecordDTO._pad0), 60);

            AssertSize<StateLeafDescriptor>(32);
            AssertOffset<StateLeafDescriptor>(nameof(StateLeafDescriptor.SectorKey), 0);
            AssertOffset<StateLeafDescriptor>(nameof(StateLeafDescriptor.SourceOffsetBytes), 8);
            AssertOffset<StateLeafDescriptor>(nameof(StateLeafDescriptor.TombstoneAliveMask), 24);

            AssertSize<Lz4SubBlockHeader>(32);
            AssertOffset<Lz4SubBlockHeader>(nameof(Lz4SubBlockHeader.Magic), 0);
            AssertOffset<Lz4SubBlockHeader>(nameof(Lz4SubBlockHeader.RawBytes), 4);
            AssertOffset<Lz4SubBlockHeader>(nameof(Lz4SubBlockHeader.StoredBytes), 8);
            AssertOffset<Lz4SubBlockHeader>(nameof(Lz4SubBlockHeader.SourceOffsetBytes), 12);
            AssertOffset<Lz4SubBlockHeader>(nameof(Lz4SubBlockHeader.Crc32), 16);
            AssertOffset<Lz4SubBlockHeader>(nameof(Lz4SubBlockHeader.Flags), 20);
            AssertOffset<Lz4SubBlockHeader>(nameof(Lz4SubBlockHeader.Version), 24);
            AssertOffset<Lz4SubBlockHeader>(nameof(Lz4SubBlockHeader.HeaderBytes), 26);
            AssertOffset<Lz4SubBlockHeader>(nameof(Lz4SubBlockHeader._pad0), 28);

            AssertSize<SaveMerkleWalAppendHeader>(64);
            AssertOffset<SaveMerkleWalAppendHeader>(nameof(SaveMerkleWalAppendHeader.LogicalOffset), 0);
            AssertOffset<SaveMerkleWalAppendHeader>(nameof(SaveMerkleWalAppendHeader.TimestampTicks), 8);
            AssertOffset<SaveMerkleWalAppendHeader>(nameof(SaveMerkleWalAppendHeader.RootHashLo), 16);
            AssertOffset<SaveMerkleWalAppendHeader>(nameof(SaveMerkleWalAppendHeader.RootHashHi), 24);
            AssertOffset<SaveMerkleWalAppendHeader>(nameof(SaveMerkleWalAppendHeader.RawBytes), 32);
            AssertOffset<SaveMerkleWalAppendHeader>(nameof(SaveMerkleWalAppendHeader.StoredBytes), 36);
            AssertOffset<SaveMerkleWalAppendHeader>(nameof(SaveMerkleWalAppendHeader.Magic), 40);
            AssertOffset<SaveMerkleWalAppendHeader>(nameof(SaveMerkleWalAppendHeader.Flags), 44);
            AssertOffset<SaveMerkleWalAppendHeader>(nameof(SaveMerkleWalAppendHeader.BlockCount), 48);
            AssertOffset<SaveMerkleWalAppendHeader>(nameof(SaveMerkleWalAppendHeader.Frame), 52);
            AssertOffset<SaveMerkleWalAppendHeader>(nameof(SaveMerkleWalAppendHeader.RecordCrc32), 56);
            AssertOffset<SaveMerkleWalAppendHeader>(nameof(SaveMerkleWalAppendHeader.Version), 60);
            AssertOffset<SaveMerkleWalAppendHeader>(nameof(SaveMerkleWalAppendHeader.HeaderBytes), 62);

            AssertSize<SaveMerkleTelemetryEntry>(64);
            AssertOffset<SaveMerkleTelemetryEntry>(nameof(SaveMerkleTelemetryEntry.RootHashLo), 0);
            AssertOffset<SaveMerkleTelemetryEntry>(nameof(SaveMerkleTelemetryEntry.RootHashHi), 8);
            AssertOffset<SaveMerkleTelemetryEntry>(nameof(SaveMerkleTelemetryEntry.TotalBytesHashed), 16);
            AssertOffset<SaveMerkleTelemetryEntry>(nameof(SaveMerkleTelemetryEntry.Frame), 28);
            AssertOffset<SaveMerkleTelemetryEntry>(nameof(SaveMerkleTelemetryEntry._pad1), 56);

            AssertSize<SaveMerkleEditorSnapshot>(80);
            AssertOffset<SaveMerkleEditorSnapshot>(nameof(SaveMerkleEditorSnapshot.RootHashLo), 0);
            AssertOffset<SaveMerkleEditorSnapshot>(nameof(SaveMerkleEditorSnapshot.ChangedBranchBits0), 16);
            AssertOffset<SaveMerkleEditorSnapshot>(nameof(SaveMerkleEditorSnapshot.ChangedLeafCount), 48);
            AssertOffset<SaveMerkleEditorSnapshot>(nameof(SaveMerkleEditorSnapshot.StoredBytes), 64);
            AssertOffset<SaveMerkleEditorSnapshot>(nameof(SaveMerkleEditorSnapshot._pad0), 76);

            AssertSize<SaveMerkleRuntimeConfig>(32);
            AssertOffset<SaveMerkleRuntimeConfig>(nameof(SaveMerkleRuntimeConfig.SubBlockBytes), 0);
            AssertOffset<SaveMerkleRuntimeConfig>(nameof(SaveMerkleRuntimeConfig.SchemaHash), 20);
            AssertOffset<SaveMerkleRuntimeConfig>(nameof(SaveMerkleRuntimeConfig._pad0), 28);

            AssertSize<MockStatePayload>(32);
            AssertOffset<MockStatePayload>(nameof(MockStatePayload.LocalAup), 0);

            AssertSize<SaveMerkleEmergencyHeader64>(64);
            AssertOffset<SaveMerkleEmergencyHeader64>(nameof(SaveMerkleEmergencyHeader64.TimestampTicks), 0);
            AssertOffset<SaveMerkleEmergencyHeader64>(nameof(SaveMerkleEmergencyHeader64.RootHashLo), 8);
            AssertOffset<SaveMerkleEmergencyHeader64>(nameof(SaveMerkleEmergencyHeader64.RootHashHi), 16);
            AssertOffset<SaveMerkleEmergencyHeader64>(nameof(SaveMerkleEmergencyHeader64._pad0), 24);
            AssertOffset<SaveMerkleEmergencyHeader64>(nameof(SaveMerkleEmergencyHeader64._pad1), 32);
            AssertOffset<SaveMerkleEmergencyHeader64>(nameof(SaveMerkleEmergencyHeader64.Magic), 40);
            AssertOffset<SaveMerkleEmergencyHeader64>(nameof(SaveMerkleEmergencyHeader64.SectorEntryBytes), 44);
            AssertOffset<SaveMerkleEmergencyHeader64>(nameof(SaveMerkleEmergencyHeader64.MerkleNodeBytes), 48);
            AssertOffset<SaveMerkleEmergencyHeader64>(nameof(SaveMerkleEmergencyHeader64.Flags), 52);
            AssertOffset<SaveMerkleEmergencyHeader64>(nameof(SaveMerkleEmergencyHeader64.Checksum), 56);
            AssertOffset<SaveMerkleEmergencyHeader64>(nameof(SaveMerkleEmergencyHeader64.Version), 60);
            AssertOffset<SaveMerkleEmergencyHeader64>(nameof(SaveMerkleEmergencyHeader64.HeaderBytes), 62);

            AssertSize<HabitatFloodStateDTO>(32);
            AssertOffset<HabitatFloodStateDTO>(nameof(HabitatFloodStateDTO.moduleHashId), 0);
            AssertOffset<HabitatFloodStateDTO>(nameof(HabitatFloodStateDTO.airReserveNormalized), 12);
            AssertOffset<HabitatFloodStateDTO>(nameof(HabitatFloodStateDTO.floodedReefFloodSeconds), 20);
            AssertOffset<HabitatFloodStateDTO>(nameof(HabitatFloodStateDTO.flags), 24);

            AssertSize<ModuleBlitDTO>(64);
            AssertOffset<ModuleBlitDTO>(nameof(ModuleBlitDTO.prefabHashId), 0);
            AssertOffset<ModuleBlitDTO>(nameof(ModuleBlitDTO.aupGridX), 8);
            AssertOffset<ModuleBlitDTO>(nameof(ModuleBlitDTO.aupLocalX), 32);
            AssertOffset<ModuleBlitDTO>(nameof(ModuleBlitDTO.rotX), 44);
            AssertOffset<ModuleBlitDTO>(nameof(ModuleBlitDTO.health), 60);

            AssertSize<PDAContextualAdvisoryDTO>(48);
            AssertOffset<PDAContextualAdvisoryDTO>(nameof(PDAContextualAdvisoryDTO.issuedFlags), 0);
            AssertOffset<PDAContextualAdvisoryDTO>(nameof(PDAContextualAdvisoryDTO.deepExposureSeconds), 32);

            AssertSize<EnvironmentalStrainDTO>(16);
            AssertOffset<EnvironmentalStrainDTO>(nameof(EnvironmentalStrainDTO.microplasticStrain), 0);
            AssertOffset<EnvironmentalStrainDTO>(nameof(EnvironmentalStrainDTO.recycledPlasticItemCount), 8);

            AssertSize<ModuleGraphEdgeDTO>(16);
            AssertOffset<ModuleGraphEdgeDTO>(nameof(ModuleGraphEdgeDTO.sourceNodeIndex), 0);
            AssertOffset<ModuleGraphEdgeDTO>(nameof(ModuleGraphEdgeDTO.destinationNodeIndex), 4);

            AssertSize<SaveChunkHeader32>(32);
            AssertOffset<SaveChunkHeader32>(nameof(SaveChunkHeader32.ChunkKey), 0);
            AssertOffset<SaveChunkHeader32>(nameof(SaveChunkHeader32.PayloadLength), 12);
            AssertOffset<SaveChunkHeader32>(nameof(SaveChunkHeader32.PayloadHash64), 16);

            AssertSize<SectorPayloadDTO>(264);
            AssertOffset<SectorPayloadDTO>(nameof(SectorPayloadDTO.SectorHash), 0);
            AssertOffset<SectorPayloadDTO>(nameof(SectorPayloadDTO.DataLength), 4);

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

            AssertSize<SaveBinaryStorage.SectorEntry>(32);
            AssertOffset<SaveBinaryStorage.SectorEntry>(nameof(SaveBinaryStorage.SectorEntry.SectorHash), 0);
            AssertOffset<SaveBinaryStorage.SectorEntry>(nameof(SaveBinaryStorage.SectorEntry.ByteOffset), 8);
            AssertOffset<SaveBinaryStorage.SectorEntry>(nameof(SaveBinaryStorage.SectorEntry.Checksum), 24);
            AssertOffset<SaveBinaryStorage.SectorEntry>(nameof(SaveBinaryStorage.SectorEntry.Reserved0), 28);

            AssertSize<SaveBinaryStorage.QuantizedAupLocalOffsetShort3>(8);
            AssertOffset<SaveBinaryStorage.QuantizedAupLocalOffsetShort3>(nameof(SaveBinaryStorage.QuantizedAupLocalOffsetShort3.XMillimeters), 0);
            AssertOffset<SaveBinaryStorage.QuantizedAupLocalOffsetShort3>(nameof(SaveBinaryStorage.QuantizedAupLocalOffsetShort3.Reserved0), 6);

            AssertSize<SaveBinaryStorage.DeltaCell>(24);
            AssertOffset<SaveBinaryStorage.DeltaCell>(nameof(SaveBinaryStorage.DeltaCell.UniverseKey), 0);
            AssertOffset<SaveBinaryStorage.DeltaCell>(nameof(SaveBinaryStorage.DeltaCell.SdfValue), 8);
            AssertOffset<SaveBinaryStorage.DeltaCell>(nameof(SaveBinaryStorage.DeltaCell.Reserved1), 20);

            AssertSize<SaveBinaryStorage.ThermalGridRleRun>(8);

            AssertSize<AbsoluteUniversePositionV7>(36);
            AssertSize<PayloadPrefixV7>(60);
            AssertSize<PayloadPrefixV8>(72);
        }

        private static void VerifyPersistentWorldLayouts()
        {
            AssertSize<PoolSlotData>(40);
            AssertOffset<PoolSlotData>(nameof(PoolSlotData.BoundGuid), 0);
            AssertOffset<PoolSlotData>(nameof(PoolSlotData.AupCell), 8);
            AssertOffset<PoolSlotData>(nameof(PoolSlotData.LocalOffset), 20);
            AssertOffset<PoolSlotData>(nameof(PoolSlotData.HydrationFrame), 32);

            AssertSize<EntityDataRecord>(64);
            AssertOffset<EntityDataRecord>(nameof(EntityDataRecord.Position), 0);
            AssertOffset<EntityDataRecord>(nameof(EntityDataRecord.Quantity), 48);
            AssertOffset<EntityDataRecord>(nameof(EntityDataRecord.Integrity01), 52);
            AssertOffset<EntityDataRecord>(nameof(EntityDataRecord.InventoryHash), 56);
            AssertOffset<EntityDataRecord>(nameof(EntityDataRecord.InstanceUid), 60);

            AssertSize<ResourceNodeTombstoneRecord>(80);
            AssertOffset<ResourceNodeTombstoneRecord>(nameof(ResourceNodeTombstoneRecord.TombstoneId), 0);
            AssertOffset<ResourceNodeTombstoneRecord>(nameof(ResourceNodeTombstoneRecord.Position), 16);
            AssertOffset<ResourceNodeTombstoneRecord>(nameof(ResourceNodeTombstoneRecord.ChunkId), 64);

            AssertSize<PersistentWorldItemRecord>(204);
            AssertOffset<PersistentWorldItemRecord>(nameof(PersistentWorldItemRecord.Position), 0);
            AssertOffset<PersistentWorldItemRecord>(nameof(PersistentWorldItemRecord.ChunkId), 48);
            AssertOffset<PersistentWorldItemRecord>(nameof(PersistentWorldItemRecord.ItemPersistentIdHash), 60);
            AssertOffset<PersistentWorldItemRecord>(nameof(PersistentWorldItemRecord.ItemPersistentId), 68);
            AssertOffset<PersistentWorldItemRecord>(nameof(PersistentWorldItemRecord.InstanceUid), 200);

            AssertSize<PersistentWorldCompactDeltaRecord>(16);
            AssertOffset<PersistentWorldCompactDeltaRecord>(nameof(PersistentWorldCompactDeltaRecord.PackedLocalPosition), 0);
            AssertOffset<PersistentWorldCompactDeltaRecord>(nameof(PersistentWorldCompactDeltaRecord.Quantity), 8);
            AssertOffset<PersistentWorldCompactDeltaRecord>(nameof(PersistentWorldCompactDeltaRecord.ChunkIndex), 12);
        }

        private static void VerifySignalLayouts()
        {
            AssertSize<ComplianceViolationSignal>(32);
            AssertOffset<ComplianceViolationSignal>(nameof(ComplianceViolationSignal.RuleHash), 0);
            AssertOffset<ComplianceViolationSignal>(nameof(ComplianceViolationSignal.SystemHash), 4);
            AssertOffset<ComplianceViolationSignal>(nameof(ComplianceViolationSignal.ContextHash), 8);
            AssertOffset<ComplianceViolationSignal>(nameof(ComplianceViolationSignal.Frame), 12);
            AssertOffset<ComplianceViolationSignal>(nameof(ComplianceViolationSignal.Severity), 16);
        }

        private static void VerifyRenderBlitLayouts()
        {
            AssertSize<HectonBlueprintPreviewBatch.BlueprintPreviewInstance>(64);
            AssertOffset<HectonBlueprintPreviewBatch.BlueprintPreviewInstance>(
                nameof(HectonBlueprintPreviewBatch.BlueprintPreviewInstance.Position),
                0);
            AssertOffset<HectonBlueprintPreviewBatch.BlueprintPreviewInstance>(
                nameof(HectonBlueprintPreviewBatch.BlueprintPreviewInstance.Rotation),
                12);
            AssertOffset<HectonBlueprintPreviewBatch.BlueprintPreviewInstance>(
                nameof(HectonBlueprintPreviewBatch.BlueprintPreviewInstance.RequirementMask),
                40);
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

        private static void AssertOffset<T>(string fieldName, int expected) where T : unmanaged
        {
            int observed = Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
            UnityEngine.Debug.Assert(observed == expected, ResolveTypeName<T>());
            if (observed != expected)
                Fail(ResolveTypeName<T>(), expected, observed, CombineHash(OffsetContextHash, ComputeFnv1A32(fieldName)));
        }

        private static void AssertBinarySafe<T>() where T : unmanaged
        {
            AssertBlittable<T>();

            if (!MemoryInquisitor.PrewarmBinaryBlittableSafety<T>())
                Fail(ResolveTypeName<T>(), expected: 1, observed: 0, CombineHash(AttributeContextHash, ResolveTypeHash<T>()));
        }

        private static void AssertBlittable<T>() where T : unmanaged
        {
            if (!UnsafeUtility.IsBlittable<T>())
                Fail(ResolveTypeName<T>(), expected: 1, observed: 0, CombineHash(BlittableContextHash, ResolveTypeHash<T>()));
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
                Frame = (uint)Mathf.Max(0, Time.frameCount),
                Severity = 3,
                Flags = 1
            };
            GlobalSignals.Publish(in signal);
        }

        private static void DumpFailure(string structName, int expected, int observed, uint contextHash)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", DumpRelativePath));
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(DumpMagic);
                writer.Write(DumpVersion);
                writer.Write(contextHash);
                writer.Write(expected);
                writer.Write(observed);
                writer.Write(structName ?? string.Empty);
            }
        }

        private static string ResolveTypeName<T>() where T : unmanaged
        {
            return typeof(T).FullName ?? typeof(T).Name;
        }

        private static uint ResolveTypeHash<T>() where T : unmanaged
        {
            return ComputeFnv1A32(ResolveTypeName<T>());
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
