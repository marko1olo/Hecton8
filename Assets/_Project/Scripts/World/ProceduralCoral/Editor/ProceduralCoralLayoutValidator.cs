using System.Reflection;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.World.ProceduralCoral.Editor
{
    [InitializeOnLoad]
    public static class ProceduralCoralLayoutValidator
    {
        static ProceduralCoralLayoutValidator()
        {
            ValidateLayouts(logSuccess: false);
        }

        [MenuItem("HECTON-8/Procedural Coral/Validate Layouts")]
        public static void ValidateLayoutsMenu()
        {
            ValidateLayouts(logSuccess: true);
        }

        public static bool ValidateLayouts(bool logSuccess)
        {
            bool ok = true;
            ok &= ValidateSize<CoralBranchDTO>(128);
            ok &= ValidateOffset<CoralBranchDTO>(nameof(CoralBranchDTO.LocalMatrix), 0);
            ok &= ValidateOffset<CoralBranchDTO>(nameof(CoralBranchDTO.PrefabHash), 64);
            ok &= ValidateOffset<CoralBranchDTO>(nameof(CoralBranchDTO.GenerationDepth), 68);
            ok &= ValidateOffset<CoralBranchDTO>(nameof(CoralBranchDTO.SectorAUP), 72);
            ok &= ValidateOffset<CoralBranchDTO>(nameof(CoralBranchDTO.Stiffness), 96);
            ok &= ValidateOffset<CoralBranchDTO>(nameof(CoralBranchDTO.Radius), 100);
            ok &= ValidateOffset<CoralBranchDTO>(nameof(CoralBranchDTO.StateFlags), 104);
            ok &= ValidateOffset<CoralBranchDTO>(nameof(CoralBranchDTO.ParentIndex), 108);
            ok &= ValidateOffset<CoralBranchDTO>(nameof(CoralBranchDTO.StableId), 112);
            ok &= ValidateOffset<CoralBranchDTO>(nameof(CoralBranchDTO.SectorHash), 116);
            ok &= ValidateSize<CoralLSystemRuleDTO>(64);
            ok &= ValidateSize<CoralSectorTriggerDTO>(64);
            ok &= ValidateSize<CoralSectorSaveDTO>(16);
            ok &= ValidateSize<CoralTuningDTO>(64);
            ok &= ValidateSize<CoralTurtleStateDTO>(64);
            ok &= ValidateSize<CoralSpatialCellDTO>(32);
            ok &= ValidateSize<CapsuleColliderDTO>(64);
            ok &= ValidateSize<SyncPulseDTO>(32);
            ok &= ValidateSize<CoralGenerationTelemetryEntry>(64);
            ok &= ValidateSize<CoralDebugSegmentDTO>(64);
            ok &= ValidateSize<CoralPaddedCounterDTO>(64);
            ok &= ValidateSize<CoralGpuSwayDTO>(64);
            ok &= ValidateSize<CoralSelfAuditResultDTO>(64);

            if (ok && logSuccess)
                Debug.Log("[SHINOBU_139] Procedural coral DTO layout validated.");

            return ok;
        }

        private static bool ValidateSize<T>(int expected) where T : struct
        {
            int observed = UnsafeUtility.SizeOf<T>();
            if (observed == expected)
                return true;

            Debug.LogError("[SHINOBU_139] Layout size mismatch: " + typeof(T).Name + " expected " + expected + " observed " + observed);
            return false;
        }

        private static bool ValidateOffset<T>(string fieldName, int expected) where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            int observed = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            if (observed == expected)
                return true;

            Debug.LogError("[SHINOBU_139] Layout offset mismatch: " + typeof(T).Name + "." + fieldName + " expected " + expected + " observed " + observed);
            return false;
        }
    }
}
