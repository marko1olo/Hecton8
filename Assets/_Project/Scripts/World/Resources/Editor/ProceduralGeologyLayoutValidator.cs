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
        static ProceduralGeologyLayoutValidator()
        {
            ValidateLayouts(logSuccess: false);
        }

        [MenuItem("HECTON-8/World/Procedural Geology/Validate Layouts")]
        public static void ValidateLayoutsMenu()
        {
            ValidateLayouts(logSuccess: true);
        }

        public static bool ValidateLayouts(bool logSuccess)
        {
            bool ok = true;
            ok &= ValidateSize<ResourceNodeDTO>(128);
            ok &= ValidateOffset<ResourceNodeDTO>(nameof(ResourceNodeDTO.LocalMatrix), 0);
            ok &= ValidateOffset<ResourceNodeDTO>(nameof(ResourceNodeDTO.ResourceTypeHash), 64);
            ok &= ValidateOffset<ResourceNodeDTO>(nameof(ResourceNodeDTO.YieldRemaining), 68);
            ok &= ValidateOffset<ResourceNodeDTO>(nameof(ResourceNodeDTO.SectorAUP), 72);
            ok &= ValidateOffset<ResourceNodeDTO>("_pad0", 96);
            ok &= ValidateOffset<ResourceNodeDTO>("_pad1", 104);
            ok &= ValidateOffset<ResourceNodeDTO>("_pad2", 112);
            ok &= ValidateOffset<ResourceNodeDTO>("_pad3", 120);
            ok &= ValidateSize<GeologyTerrainSampleDTO>(32);
            ok &= ValidateSize<GeologyDistributionRuleDTO>(64);
            ok &= ValidateSize<GeologyTuningDTO>(64);
            ok &= ValidateSize<GeologyGenerationTelemetryEntry>(64);
            ok &= ValidateOffset<GeologyGenerationTelemetryEntry>(nameof(GeologyGenerationTelemetryEntry.SectorHash), 0);
            ok &= ValidateOffset<GeologyGenerationTelemetryEntry>(nameof(GeologyGenerationTelemetryEntry.Frame), 8);
            ok &= ValidateOffset<GeologyGenerationTelemetryEntry>(nameof(GeologyGenerationTelemetryEntry.GenerationBudgetUs), 32);
            ok &= ValidateOffset<GeologyGenerationTelemetryEntry>(nameof(GeologyGenerationTelemetryEntry.StateHash), 56);
            ok &= ValidateSize<GeologySelfAuditResultDTO>(64);
            ok &= ValidateSize<GeologyIndirectArgsDTO>(16);
            ok &= ValidateOffset<GeologyIndirectArgsDTO>(nameof(GeologyIndirectArgsDTO.VertexCountPerInstance), 0);
            ok &= ValidateOffset<GeologyIndirectArgsDTO>(nameof(GeologyIndirectArgsDTO.InstanceCount), 4);
            ok &= ValidateOffset<GeologyIndirectArgsDTO>(nameof(GeologyIndirectArgsDTO.StartVertex), 8);
            ok &= ValidateOffset<GeologyIndirectArgsDTO>(nameof(GeologyIndirectArgsDTO.StartInstance), 12);
            ok &= ValidateSize<GeologyHzbTileDTO>(16);
            ok &= ValidateOffset<GeologyHzbTileDTO>(nameof(GeologyHzbTileDTO.Depth01), 0);
            ok &= ValidateOffset<GeologyHzbTileDTO>(nameof(GeologyHzbTileDTO.Flags), 12);
            ok &= ValidateSize<GeologyHzbMetaDTO>(128);
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
