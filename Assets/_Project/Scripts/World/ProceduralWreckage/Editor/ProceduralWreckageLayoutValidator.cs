using System.Reflection;
using Hecton8.Core.Memory;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.World.ProceduralWreckage.Editor
{
    [InitializeOnLoad]
    public static class ProceduralWreckageLayoutValidator
    {
        private const int WreckageNodeStrideBytes = 128;
        private const int PaddedCounterStrideBytes = 64;
        private const int RuleStrideBytes = 64;
        private const int GridCellStrideBytes = 16;
        private const int SectorTriggerStrideBytes = 64;
        private const int TelemetryStrideBytes = 64;
        private const int BoxColliderStrideBytes = 64;
        private const int LootSpawnRequestStrideBytes = 64;

        static ProceduralWreckageLayoutValidator()
        {
            ValidateLayouts(logSuccess: false);
        }

        [MenuItem("HECTON-8/Procedural Wreckage/Validate Layouts")]
        public static void ValidateLayoutsMenu()
        {
            ValidateLayouts(logSuccess: true);
        }

        public static bool ValidateLayouts(bool logSuccess)
        {
            bool ok = true;
            ok &= ValidateSize<WreckageNodeDTO>(WreckageNodeStrideBytes);
            ok &= ValidateOffset<WreckageNodeDTO>(nameof(WreckageNodeDTO.LocalMatrix), 0);
            ok &= ValidateOffset<WreckageNodeDTO>(nameof(WreckageNodeDTO.PrefabHash), 64);
            ok &= ValidateOffset<WreckageNodeDTO>(nameof(WreckageNodeDTO.StateFlags), 68);
            ok &= ValidateOffset<WreckageNodeDTO>(nameof(WreckageNodeDTO.SectorAUP), 72);
            ok &= ValidateOffset<WreckageNodeDTO>(nameof(WreckageNodeDTO.BoundsExtents), 96);
            ok &= ValidateOffset<WreckageNodeDTO>(nameof(WreckageNodeDTO.BoundsRadius), 108);
            ok &= ValidateOffset<WreckageNodeDTO>(nameof(WreckageNodeDTO.SectorHash), 112);
            ok &= ValidateOffset<WreckageNodeDTO>(nameof(WreckageNodeDTO.ModuleId), 116);
            ok &= ValidateOffset<WreckageNodeDTO>(nameof(WreckageNodeDTO.GraphDegree), 120);
            ok &= ValidateOffset<WreckageNodeDTO>(nameof(WreckageNodeDTO.StableId), 124);
            ok &= ValidateSize<WreckagePaddedCounterDTO>(PaddedCounterStrideBytes);
            ok &= ValidateSize<WreckageRuleDTO>(RuleStrideBytes);
            ok &= ValidateSize<WreckageGridCellDTO>(GridCellStrideBytes);
            ok &= ValidateSize<WreckageSectorTriggerDTO>(SectorTriggerStrideBytes);
            ok &= ValidateSize<WreckageGenerationTelemetryEntry>(TelemetryStrideBytes);
            ok &= ValidateSize<WreckageBoxColliderDTO>(BoxColliderStrideBytes);
            ok &= ValidateSize<LootSpawnRequestDTO>(LootSpawnRequestStrideBytes);

            if (ok && logSuccess)
                Debug.Log("[WRECKAGE_1717] Procedural wreckage DTO layout validated.");

            return ok;
        }

        private static bool ValidateSize<T>(int expected) where T : struct
        {
            int observed = UnsafeUtility.SizeOf<T>();
            if (observed == expected)
                return true;

            Debug.LogError("[WRECKAGE_1717] Layout size mismatch: " + typeof(T).Name + " expected " + expected + " observed " + observed);
            return false;
        }

        private static bool ValidateOffset<T>(string fieldName, int expected) where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            int observed = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            if (observed == expected)
                return true;

            Debug.LogError("[WRECKAGE_1717] Layout offset mismatch: " + typeof(T).Name + "." + fieldName + " expected " + expected + " observed " + observed);
            return false;
        }
    }
}
