using System.Reflection;
using Hecton8.World.OfflineWreckageBaker;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.World.OfflineWreckageBaker.Editor
{
    [InitializeOnLoad]
    public static class OfflineWreckageLayoutValidator
    {
        static OfflineWreckageLayoutValidator()
        {
            Validate(logSuccess: false);
        }

        [MenuItem("HECTON-8/Wreckage Forge/Validate Offline Wreckage Layouts")]
        public static void ValidateMenu()
        {
            Validate(logSuccess: true);
        }

        public static bool Validate(bool logSuccess)
        {
            bool ok = true;
            ok &= ValidateSize<MeshDamageStateMappingDTO>(32);
            ok &= ValidateOffset<MeshDamageStateMappingDTO>(nameof(MeshDamageStateMappingDTO.PristineMeshHash), 0);
            ok &= ValidateOffset<MeshDamageStateMappingDTO>(nameof(MeshDamageStateMappingDTO.StressedMeshHash), 4);
            ok &= ValidateOffset<MeshDamageStateMappingDTO>(nameof(MeshDamageStateMappingDTO.RupturedMeshHash), 8);
            ok &= ValidateOffset<MeshDamageStateMappingDTO>(nameof(MeshDamageStateMappingDTO.CollapsedMeshHash), 12);
            ok &= ValidateOffset<MeshDamageStateMappingDTO>(nameof(MeshDamageStateMappingDTO._pad0), 16);
            ok &= ValidateOffset<MeshDamageStateMappingDTO>(nameof(MeshDamageStateMappingDTO._pad1), 24);
            ok &= ValidateSize<OfflineWreckageBakeVertexDTO>(64);
            ok &= ValidateOffset<OfflineWreckageBakeVertexDTO>(nameof(OfflineWreckageBakeVertexDTO.Position), 0);
            ok &= ValidateOffset<OfflineWreckageBakeVertexDTO>(nameof(OfflineWreckageBakeVertexDTO.Normal), 12);
            ok &= ValidateOffset<OfflineWreckageBakeVertexDTO>(nameof(OfflineWreckageBakeVertexDTO.Tangent), 24);
            ok &= ValidateOffset<OfflineWreckageBakeVertexDTO>(nameof(OfflineWreckageBakeVertexDTO.Uv0), 40);
            ok &= ValidateOffset<OfflineWreckageBakeVertexDTO>(nameof(OfflineWreckageBakeVertexDTO.PackedColor), 48);
            ok &= ValidateOffset<OfflineWreckageBakeVertexDTO>(nameof(OfflineWreckageBakeVertexDTO.Uv3AupLocal), 52);
            ok &= ValidateSize<WreckageDeformationProfileDTO>(64);
            ok &= ValidateSize<OfflineWreckageBakeCounters64>(64);
            ok &= ValidateOffset<OfflineWreckageBakeCounters64>(nameof(OfflineWreckageBakeCounters64.ActiveVertexCount), 0);
            ok &= ValidateOffset<OfflineWreckageBakeCounters64>(nameof(OfflineWreckageBakeCounters64.TornVertexCount), 4);
            ok &= ValidateOffset<OfflineWreckageBakeCounters64>(nameof(OfflineWreckageBakeCounters64.DegenerateTriangleCount), 8);
            ok &= ValidateOffset<OfflineWreckageBakeCounters64>(nameof(OfflineWreckageBakeCounters64.HullVertexCount), 12);
            ok &= ValidateOffset<OfflineWreckageBakeCounters64>(nameof(OfflineWreckageBakeCounters64.WarningFlags), 16);
            ok &= ValidateOffset<OfflineWreckageBakeCounters64>(nameof(OfflineWreckageBakeCounters64._pad0), 20);
            ok &= ValidateOffset<OfflineWreckageBakeCounters64>(nameof(OfflineWreckageBakeCounters64._pad1), 24);
            ok &= ValidateOffset<OfflineWreckageBakeCounters64>(nameof(OfflineWreckageBakeCounters64._pad2), 32);
            ok &= ValidateOffset<OfflineWreckageBakeCounters64>(nameof(OfflineWreckageBakeCounters64._pad3), 40);
            ok &= ValidateOffset<OfflineWreckageBakeCounters64>(nameof(OfflineWreckageBakeCounters64._pad4), 48);
            ok &= ValidateOffset<OfflineWreckageBakeCounters64>(nameof(OfflineWreckageBakeCounters64._pad5), 56);
            ok &= ValidateSize<OfflineWreckageTelemetryEntry>(64);
            ok &= ValidateSize<OfflineWreckageSubMeshIndexRangeDTO>(16);
            ok &= ValidateOffset<OfflineWreckageSubMeshIndexRangeDTO>(nameof(OfflineWreckageSubMeshIndexRangeDTO.SourceIndexStart), 0);
            ok &= ValidateOffset<OfflineWreckageSubMeshIndexRangeDTO>(nameof(OfflineWreckageSubMeshIndexRangeDTO.IndexCount), 4);
            ok &= ValidateOffset<OfflineWreckageSubMeshIndexRangeDTO>(nameof(OfflineWreckageSubMeshIndexRangeDTO.DestinationIndexStart), 8);
            ok &= ValidateOffset<OfflineWreckageSubMeshIndexRangeDTO>(nameof(OfflineWreckageSubMeshIndexRangeDTO.BaseVertex), 12);

            if (ok && logSuccess)
                Debug.Log("[SHINOBU_209] Offline wreckage DTO layout validated.");

            return ok;
        }

        private static bool ValidateSize<T>(int expected) where T : struct
        {
            int observed = UnsafeUtility.SizeOf<T>();
            if (observed == expected)
                return true;

            Debug.LogError("[SHINOBU_209] Layout size mismatch: " + typeof(T).Name + " expected " + expected + " observed " + observed);
            return false;
        }

        private static bool ValidateOffset<T>(string fieldName, int expected) where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            int observed = field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
            if (observed == expected)
                return true;

            Debug.LogError("[SHINOBU_209] Layout offset mismatch: " + typeof(T).Name + "." + fieldName + " expected " + expected + " observed " + observed);
            return false;
        }
    }
}
