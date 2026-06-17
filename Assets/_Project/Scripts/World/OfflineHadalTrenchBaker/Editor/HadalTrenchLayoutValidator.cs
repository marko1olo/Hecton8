using System.Reflection;
using Hecton8.World.OfflineHadalTrenchBaker;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.World.OfflineHadalTrenchBaker.Editor
{
    [InitializeOnLoad]
    public static class HadalTrenchLayoutValidator
    {
        private const int FaultLineStrideBytes = 64;
        private const int ThermalVentSpawnStrideBytes = 64;
        private const int BakeConfigStrideBytes = 160;
        private const int ChunkHeaderStrideBytes = 160;
        private const int RleRunStrideBytes = 16;
        private const int AdaptiveBlockStrideBytes = 32;
        private const int RiftProfileStrideBytes = 128;
        private const int TelemetryStrideBytes = 64;
        private const int RollbackExclusionStrideBytes = 32;

        static HadalTrenchLayoutValidator()
        {
            Validate(logSuccess: false);
        }

        [MenuItem("Hecton8/Hadal Trench Forge/Validate DTO Layouts")]
        public static void ValidateMenu()
        {
            Validate(logSuccess: true);
        }

        public static bool Validate(bool logSuccess)
        {
            bool ok = true;
            ok &= ValidateSize<FaultLineParamsDTO>(FaultLineStrideBytes);
            ok &= ValidateOffset<FaultLineParamsDTO>(nameof(FaultLineParamsDTO.StartAUP), 0);
            ok &= ValidateOffset<FaultLineParamsDTO>(nameof(FaultLineParamsDTO.EndAUP), 24);
            ok &= ValidateOffset<FaultLineParamsDTO>(nameof(FaultLineParamsDTO.Depth), 48);
            ok &= ValidateOffset<FaultLineParamsDTO>(nameof(FaultLineParamsDTO.Width), 52);
            ok &= ValidateOffset<FaultLineParamsDTO>(nameof(FaultLineParamsDTO.NoiseIntensity), 56);
            ok &= ValidateOffset<FaultLineParamsDTO>(nameof(FaultLineParamsDTO._pad0), 60);
            ok &= ValidateSize<ThermalVentSpawnDTO>(ThermalVentSpawnStrideBytes);
            ok &= ValidateOffset<ThermalVentSpawnDTO>(nameof(ThermalVentSpawnDTO.PositionAUP), 0);
            ok &= ValidateOffset<ThermalVentSpawnDTO>(nameof(ThermalVentSpawnDTO.RadiusMeters), 24);
            ok &= ValidateOffset<ThermalVentSpawnDTO>(nameof(ThermalVentSpawnDTO.HeatCelsius), 28);
            ok &= ValidateOffset<ThermalVentSpawnDTO>(nameof(ThermalVentSpawnDTO.PressureKPa), 32);
            ok &= ValidateOffset<ThermalVentSpawnDTO>(nameof(ThermalVentSpawnDTO.LootAffinity01), 36);
            ok &= ValidateOffset<ThermalVentSpawnDTO>(nameof(ThermalVentSpawnDTO.FaultHash), 40);
            ok &= ValidateOffset<ThermalVentSpawnDTO>(nameof(ThermalVentSpawnDTO.Flags), 44);
            ok &= ValidateOffset<ThermalVentSpawnDTO>(nameof(ThermalVentSpawnDTO._pad0), 48);
            ok &= ValidateOffset<ThermalVentSpawnDTO>(nameof(ThermalVentSpawnDTO._pad1), 56);
            ok &= ValidateSize<HadalTrenchBakeConfigDTO>(BakeConfigStrideBytes);
            ok &= ValidateOffset<HadalTrenchBakeConfigDTO>(nameof(HadalTrenchBakeConfigDTO.SectorOriginAUP), 0);
            ok &= ValidateOffset<HadalTrenchBakeConfigDTO>(nameof(HadalTrenchBakeConfigDTO.WorldMinAUP), 24);
            ok &= ValidateOffset<HadalTrenchBakeConfigDTO>(nameof(HadalTrenchBakeConfigDTO.WorldMaxAUP), 48);
            ok &= ValidateOffset<HadalTrenchBakeConfigDTO>(nameof(HadalTrenchBakeConfigDTO.SeaFloorAUPY), 72);
            ok &= ValidateOffset<HadalTrenchBakeConfigDTO>(nameof(HadalTrenchBakeConfigDTO.Resolution), 80);
            ok &= ValidateOffset<HadalTrenchBakeConfigDTO>(nameof(HadalTrenchBakeConfigDTO.VoxelSizeMeters), 92);
            ok &= ValidateOffset<HadalTrenchBakeConfigDTO>(nameof(HadalTrenchBakeConfigDTO.VoronoiCellSizeMeters), 96);
            ok &= ValidateOffset<HadalTrenchBakeConfigDTO>(nameof(HadalTrenchBakeConfigDTO.DefaultDepthMeters), 100);
            ok &= ValidateOffset<HadalTrenchBakeConfigDTO>(nameof(HadalTrenchBakeConfigDTO.DefaultWidthMeters), 104);
            ok &= ValidateOffset<HadalTrenchBakeConfigDTO>(nameof(HadalTrenchBakeConfigDTO.NoiseIntensity), 108);
            ok &= ValidateOffset<HadalTrenchBakeConfigDTO>(nameof(HadalTrenchBakeConfigDTO.NoiseFrequency), 112);
            ok &= ValidateOffset<HadalTrenchBakeConfigDTO>(nameof(HadalTrenchBakeConfigDTO.GlobalQualityWeight), 116);
            ok &= ValidateOffset<HadalTrenchBakeConfigDTO>(nameof(HadalTrenchBakeConfigDTO.Seed), 120);
            ok &= ValidateOffset<HadalTrenchBakeConfigDTO>(nameof(HadalTrenchBakeConfigDTO.FaultGridX), 124);
            ok &= ValidateOffset<HadalTrenchBakeConfigDTO>(nameof(HadalTrenchBakeConfigDTO.FaultGridZ), 128);
            ok &= ValidateOffset<HadalTrenchBakeConfigDTO>(nameof(HadalTrenchBakeConfigDTO.FaultCount), 132);
            ok &= ValidateOffset<HadalTrenchBakeConfigDTO>(nameof(HadalTrenchBakeConfigDTO.MaxVentCount), 136);
            ok &= ValidateOffset<HadalTrenchBakeConfigDTO>(nameof(HadalTrenchBakeConfigDTO.Flags), 140);
            ok &= ValidateOffset<HadalTrenchBakeConfigDTO>(nameof(HadalTrenchBakeConfigDTO._pad0), 144);
            ok &= ValidateOffset<HadalTrenchBakeConfigDTO>(nameof(HadalTrenchBakeConfigDTO._pad1), 152);
            ok &= ValidateSize<HadalTrenchChunkHeaderDTO>(ChunkHeaderStrideBytes);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.Magic), 0);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.Version), 4);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.Flags), 8);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.Resolution), 12);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.SectorOriginAUP), 24);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.VoxelSizeMeters), 48);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.CompressionMode), 52);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.CompressedBytes), 56);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.RleRunCount), 60);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.VentCount), 64);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.AdaptiveBlockCount), 68);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.MaxDepthMeters), 72);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.ExcavatedCubicMeters), 80);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.DensityPayloadOffset), 88);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.VentPayloadOffset), 96);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.AdaptivePayloadOffset), 104);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.PayloadHash), 112);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.HeaderBytes), 120);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.EndianMarker), 124);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.UncompressedBytes), 128);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.DensityPreludeBytes), 132);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.TotalFileBytes), 136);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.SectionAlignmentBytes), 144);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.ChecksumType), 148);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO.SchemaHash), 152);
            ok &= ValidateOffset<HadalTrenchChunkHeaderDTO>(nameof(HadalTrenchChunkHeaderDTO._pad0), 156);
            ok &= ValidateSize<HadalTrenchRleRunDTO>(RleRunStrideBytes);
            ok &= ValidateOffset<HadalTrenchRleRunDTO>(nameof(HadalTrenchRleRunDTO.StartVoxel), 0);
            ok &= ValidateOffset<HadalTrenchRleRunDTO>(nameof(HadalTrenchRleRunDTO.RunLength), 4);
            ok &= ValidateOffset<HadalTrenchRleRunDTO>(nameof(HadalTrenchRleRunDTO.Density), 8);
            ok &= ValidateOffset<HadalTrenchRleRunDTO>(nameof(HadalTrenchRleRunDTO.MaterialId), 9);
            ok &= ValidateOffset<HadalTrenchRleRunDTO>(nameof(HadalTrenchRleRunDTO.Flags), 10);
            ok &= ValidateOffset<HadalTrenchRleRunDTO>(nameof(HadalTrenchRleRunDTO._pad0), 12);
            ok &= ValidateSize<HadalTrenchAdaptiveBlockDTO>(AdaptiveBlockStrideBytes);
            ok &= ValidateOffset<HadalTrenchAdaptiveBlockDTO>(nameof(HadalTrenchAdaptiveBlockDTO.MinVoxel), 0);
            ok &= ValidateOffset<HadalTrenchAdaptiveBlockDTO>(nameof(HadalTrenchAdaptiveBlockDTO.BlockSizeVoxels), 12);
            ok &= ValidateOffset<HadalTrenchAdaptiveBlockDTO>(nameof(HadalTrenchAdaptiveBlockDTO.MinDensity), 13);
            ok &= ValidateOffset<HadalTrenchAdaptiveBlockDTO>(nameof(HadalTrenchAdaptiveBlockDTO.MaxDensity), 14);
            ok &= ValidateOffset<HadalTrenchAdaptiveBlockDTO>(nameof(HadalTrenchAdaptiveBlockDTO.Flags), 15);
            ok &= ValidateOffset<HadalTrenchAdaptiveBlockDTO>(nameof(HadalTrenchAdaptiveBlockDTO.VoxelCount), 16);
            ok &= ValidateOffset<HadalTrenchAdaptiveBlockDTO>(nameof(HadalTrenchAdaptiveBlockDTO.ErrorMeters), 20);
            ok &= ValidateOffset<HadalTrenchAdaptiveBlockDTO>(nameof(HadalTrenchAdaptiveBlockDTO.StateHash), 24);
            ok &= ValidateOffset<HadalTrenchAdaptiveBlockDTO>(nameof(HadalTrenchAdaptiveBlockDTO._pad0), 28);
            ok &= ValidateSize<TectonicRiftProfileDTO>(RiftProfileStrideBytes);
            ok &= ValidateOffset<TectonicRiftProfileDTO>(nameof(TectonicRiftProfileDTO.SectorOriginAUP), 0);
            ok &= ValidateOffset<TectonicRiftProfileDTO>(nameof(TectonicRiftProfileDTO.Name), 24);
            ok &= ValidateOffset<TectonicRiftProfileDTO>(nameof(TectonicRiftProfileDTO.Seed), 88);
            ok &= ValidateOffset<TectonicRiftProfileDTO>(nameof(TectonicRiftProfileDTO.VoronoiCellSizeMeters), 92);
            ok &= ValidateOffset<TectonicRiftProfileDTO>(nameof(TectonicRiftProfileDTO.TrenchWidthMeters), 96);
            ok &= ValidateOffset<TectonicRiftProfileDTO>(nameof(TectonicRiftProfileDTO.TrenchDepthMeters), 100);
            ok &= ValidateOffset<TectonicRiftProfileDTO>(nameof(TectonicRiftProfileDTO.NoiseIntensity), 104);
            ok &= ValidateOffset<TectonicRiftProfileDTO>(nameof(TectonicRiftProfileDTO.NoiseFrequency), 108);
            ok &= ValidateOffset<TectonicRiftProfileDTO>(nameof(TectonicRiftProfileDTO.GlobalQualityWeight), 112);
            ok &= ValidateOffset<TectonicRiftProfileDTO>(nameof(TectonicRiftProfileDTO._pad0), 116);
            ok &= ValidateOffset<TectonicRiftProfileDTO>(nameof(TectonicRiftProfileDTO._pad1), 120);
            ok &= ValidateSize<HadalTrenchBakeTelemetryEntry>(TelemetryStrideBytes);
            ok &= ValidateOffset<HadalTrenchBakeTelemetryEntry>(nameof(HadalTrenchBakeTelemetryEntry.SectorOriginAUP), 0);
            ok &= ValidateOffset<HadalTrenchBakeTelemetryEntry>(nameof(HadalTrenchBakeTelemetryEntry.Frame), 24);
            ok &= ValidateOffset<HadalTrenchBakeTelemetryEntry>(nameof(HadalTrenchBakeTelemetryEntry.FaultCount), 28);
            ok &= ValidateOffset<HadalTrenchBakeTelemetryEntry>(nameof(HadalTrenchBakeTelemetryEntry.VoxelCount), 32);
            ok &= ValidateOffset<HadalTrenchBakeTelemetryEntry>(nameof(HadalTrenchBakeTelemetryEntry.RleRunCount), 36);
            ok &= ValidateOffset<HadalTrenchBakeTelemetryEntry>(nameof(HadalTrenchBakeTelemetryEntry.CarvingMilliseconds), 40);
            ok &= ValidateOffset<HadalTrenchBakeTelemetryEntry>(nameof(HadalTrenchBakeTelemetryEntry.SerializationMilliseconds), 44);
            ok &= ValidateOffset<HadalTrenchBakeTelemetryEntry>(nameof(HadalTrenchBakeTelemetryEntry.WarningFlags), 48);
            ok &= ValidateOffset<HadalTrenchBakeTelemetryEntry>(nameof(HadalTrenchBakeTelemetryEntry.StateHash), 52);
            ok &= ValidateOffset<HadalTrenchBakeTelemetryEntry>(nameof(HadalTrenchBakeTelemetryEntry.DumpReason), 56);
            ok &= ValidateOffset<HadalTrenchBakeTelemetryEntry>(nameof(HadalTrenchBakeTelemetryEntry.Stage), 60);
            ok &= ValidateSize<HadalTrenchRollbackExclusionDTO>(RollbackExclusionStrideBytes);
            ok &= ValidateOffset<HadalTrenchRollbackExclusionDTO>(nameof(HadalTrenchRollbackExclusionDTO.StaticVoxelHash), 0);
            ok &= ValidateOffset<HadalTrenchRollbackExclusionDTO>(nameof(HadalTrenchRollbackExclusionDTO.Flags), 4);
            ok &= ValidateOffset<HadalTrenchRollbackExclusionDTO>(nameof(HadalTrenchRollbackExclusionDTO.FileGuidLow), 8);
            ok &= ValidateOffset<HadalTrenchRollbackExclusionDTO>(nameof(HadalTrenchRollbackExclusionDTO.FileGuidHigh), 16);
            ok &= ValidateOffset<HadalTrenchRollbackExclusionDTO>(nameof(HadalTrenchRollbackExclusionDTO._pad0), 24);
            if (ok && logSuccess)
                Debug.Log("[SHINOBU_241] Hadal trench DTO layouts validated.");
            return ok;
        }

        private static bool ValidateSize<T>(int expected) where T : struct
        {
            int observed = UnsafeUtility.SizeOf<T>();
            if (observed == expected)
                return true;

            Debug.LogError("[SHINOBU_241] Layout size mismatch: " + typeof(T).Name + " expected " + expected + " observed " + observed);
            return false;
        }

        private static bool ValidateOffset<T>(string fieldName, int expected) where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            int observed = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            if (observed == expected)
                return true;

            Debug.LogError("[SHINOBU_241] Layout offset mismatch: " + typeof(T).Name + "." + fieldName + " expected " + expected + " observed " + observed);
            return false;
        }
    }
}
