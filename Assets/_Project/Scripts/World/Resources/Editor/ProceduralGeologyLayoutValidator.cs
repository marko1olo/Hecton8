using System.Reflection;
using Hecton8.World;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.World.Editor
{
    [InitializeOnLoad]
    public static class ProceduralGeologyLayoutValidator
    {
        private const int ResourceNodeStrideBytes = 128;
        private const int TerrainSampleStrideBytes = 32;
        private const int DistributionRuleStrideBytes = 64;
        private const int TuningStrideBytes = 64;
        private const int PlayerEcosystemTelemetryStrideBytes = 32;
        private const int TelemetryStrideBytes = 64;
        private const int SelfAuditStrideBytes = 64;
        private const int IndirectArgsStrideBytes = 16;
        private const int HzbTileStrideBytes = 16;
        private const int HzbMetaStrideBytes = 128;

        static ProceduralGeologyLayoutValidator()
        {
            ValidateLayouts(logSuccess: false);
        }

        [MenuItem("Hecton8/World/Procedural Geology/Validate Layouts")]
        public static void ValidateLayoutsMenu()
        {
            ValidateLayouts(logSuccess: true);
        }

        public static bool ValidateLayouts(bool logSuccess)
        {
            bool ok = true;
            ok &= ValidateSize<ResourceNodeDTO>(ResourceNodeStrideBytes);
            ok &= ValidateOffset<ResourceNodeDTO>(nameof(ResourceNodeDTO.LocalMatrix), 0);
            ok &= ValidateOffset<ResourceNodeDTO>(nameof(ResourceNodeDTO.ResourceTypeHash), 64);
            ok &= ValidateOffset<ResourceNodeDTO>(nameof(ResourceNodeDTO.YieldRemaining), 68);
            ok &= ValidateOffset<ResourceNodeDTO>(nameof(ResourceNodeDTO.SectorAUP), 72);
            ok &= ValidateOffset<ResourceNodeDTO>("_pad0", 96);
            ok &= ValidateOffset<ResourceNodeDTO>("_pad1", 104);
            ok &= ValidateOffset<ResourceNodeDTO>("_pad2", 112);
            ok &= ValidateOffset<ResourceNodeDTO>("_pad3", 120);
            ok &= ValidateSize<GeologyTerrainSampleDTO>(TerrainSampleStrideBytes);
            ok &= ValidateSize<GeologyDistributionRuleDTO>(DistributionRuleStrideBytes);
            ok &= ValidateSize<GeologyTuningDTO>(TuningStrideBytes);
            ok &= ValidateSize<PlayerEcosystemTelemetryDTO>(PlayerEcosystemTelemetryStrideBytes);
            ok &= ValidateOffset<PlayerEcosystemTelemetryDTO>(nameof(PlayerEcosystemTelemetryDTO.EmptyScansStreak), 0);
            ok &= ValidateOffset<PlayerEcosystemTelemetryDTO>(nameof(PlayerEcosystemTelemetryDTO.TotalOresMined), 4);
            ok &= ValidateOffset<PlayerEcosystemTelemetryDTO>(nameof(PlayerEcosystemTelemetryDTO.DistanceSinceLastFind), 8);
            ok &= ValidateOffset<PlayerEcosystemTelemetryDTO>(nameof(PlayerEcosystemTelemetryDTO.PityTriggerActive), 12);
            ok &= ValidateOffset<PlayerEcosystemTelemetryDTO>(nameof(PlayerEcosystemTelemetryDTO.LastPityResourceType), 28);
            ok &= ValidateSize<GeologyGenerationTelemetryEntry>(TelemetryStrideBytes);
            ok &= ValidateOffset<GeologyGenerationTelemetryEntry>(nameof(GeologyGenerationTelemetryEntry.SectorHash), 0);
            ok &= ValidateOffset<GeologyGenerationTelemetryEntry>(nameof(GeologyGenerationTelemetryEntry.Frame), 8);
            ok &= ValidateOffset<GeologyGenerationTelemetryEntry>(nameof(GeologyGenerationTelemetryEntry.GenerationBudgetUs), 32);
            ok &= ValidateOffset<GeologyGenerationTelemetryEntry>(nameof(GeologyGenerationTelemetryEntry.StateHash), 56);
            ok &= ValidateSize<GeologySelfAuditResultDTO>(SelfAuditStrideBytes);
            ok &= ValidateOffset<GeologySelfAuditResultDTO>(nameof(GeologySelfAuditResultDTO.PlayerEcosystemTelemetrySize), 52);
            ok &= ValidateSize<GeologyIndirectArgsDTO>(IndirectArgsStrideBytes);
            ok &= ValidateOffset<GeologyIndirectArgsDTO>(nameof(GeologyIndirectArgsDTO.VertexCountPerInstance), 0);
            ok &= ValidateOffset<GeologyIndirectArgsDTO>(nameof(GeologyIndirectArgsDTO.InstanceCount), 4);
            ok &= ValidateOffset<GeologyIndirectArgsDTO>(nameof(GeologyIndirectArgsDTO.StartVertex), 8);
            ok &= ValidateOffset<GeologyIndirectArgsDTO>(nameof(GeologyIndirectArgsDTO.StartInstance), 12);
            ok &= ValidateSize<GeologyHzbTileDTO>(HzbTileStrideBytes);
            ok &= ValidateOffset<GeologyHzbTileDTO>(nameof(GeologyHzbTileDTO.Depth01), 0);
            ok &= ValidateOffset<GeologyHzbTileDTO>(nameof(GeologyHzbTileDTO.Flags), 12);
            ok &= ValidateSize<GeologyHzbMetaDTO>(HzbMetaStrideBytes);
            ok &= ValidateOffset<GeologyHzbMetaDTO>(nameof(GeologyHzbMetaDTO.CameraRelativeViewProjection), 0);
            ok &= ValidateOffset<GeologyHzbMetaDTO>(nameof(GeologyHzbMetaDTO.Width), 64);
            ok &= ValidateOffset<GeologyHzbMetaDTO>(nameof(GeologyHzbMetaDTO.Flags), 72);
            ok &= ValidateOffset<GeologyHzbMetaDTO>("_pad1", 96);

            if (ok && logSuccess)
                Debug.Log("[SHINOBU_153] Procedural geology DTO layouts validated.");

            return ok;
        }

        private static bool ValidateSize<T>(int expected) where T : struct
        {
            int observed = UnsafeUtility.SizeOf<T>();
            if (observed == expected)
                return true;

            Debug.LogError("[SHINOBU_153] Layout size mismatch: " + typeof(T).Name + " expected " + expected + " observed " + observed);
            return false;
        }

        private static bool ValidateOffset<T>(string fieldName, int expected) where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            int observed = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            if (observed == expected)
                return true;

            Debug.LogError("[SHINOBU_153] Layout offset mismatch: " + typeof(T).Name + "." + fieldName + " expected " + expected + " observed " + observed);
            return false;
        }
    }
}
