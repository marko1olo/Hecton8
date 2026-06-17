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
        private const int DamageStateMappingStrideBytes = 32;
        private const int BakeVertexStrideBytes = 64;
        private const int DeformationProfileStrideBytes = 64;
        private const int BakeCountersStrideBytes = 64;
        private const int TelemetryStrideBytes = 64;
        private const int SubMeshIndexRangeStrideBytes = 16;

        static OfflineWreckageLayoutValidator()
        {
            Validate(logSuccess: false);
        }

        [MenuItem("Hecton8/Wreckage Forge/Validate Offline Wreckage Layouts")]
        public static void ValidateMenu()
        {
            Validate(logSuccess: true);
        }

        public static bool Validate(bool logSuccess)
        {
            bool ok = true;
            ok &= ValidateSize<MeshDamageStateMappingDTO>(DamageStateMappingStrideBytes);
            ok &= ValidateOffset<MeshDamageStateMappingDTO>(nameof(MeshDamageStateMappingDTO.PristineMeshHash), 0);
            ok &= ValidateOffset<MeshDamageStateMappingDTO>(nameof(MeshDamageStateMappingDTO.StressedMeshHash), 4);
            ok &= ValidateOffset<MeshDamageStateMappingDTO>(nameof(MeshDamageStateMappingDTO.RupturedMeshHash), 8);
            ok &= ValidateOffset<MeshDamageStateMappingDTO>(nameof(MeshDamageStateMappingDTO.CollapsedMeshHash), 12);
            ok &= ValidateOffset<MeshDamageStateMappingDTO>(nameof(MeshDamageStateMappingDTO.MappingVersion), 16);
            ok &= ValidateOffset<MeshDamageStateMappingDTO>(nameof(MeshDamageStateMappingDTO.ArtifactVersion), 20);
            ok &= ValidateOffset<MeshDamageStateMappingDTO>(nameof(MeshDamageStateMappingDTO._pad0), 24);
            ok &= ValidateSize<OfflineWreckageBakeVertexDTO>(BakeVertexStrideBytes);
            ok &= ValidateOffset<OfflineWreckageBakeVertexDTO>(nameof(OfflineWreckageBakeVertexDTO.Position), 0);
            ok &= ValidateOffset<OfflineWreckageBakeVertexDTO>(nameof(OfflineWreckageBakeVertexDTO.Normal), 12);
            ok &= ValidateOffset<OfflineWreckageBakeVertexDTO>(nameof(OfflineWreckageBakeVertexDTO.Tangent), 24);
            ok &= ValidateOffset<OfflineWreckageBakeVertexDTO>(nameof(OfflineWreckageBakeVertexDTO.Uv0), 40);
            ok &= ValidateOffset<OfflineWreckageBakeVertexDTO>(nameof(OfflineWreckageBakeVertexDTO.PackedColor), 48);
            ok &= ValidateOffset<OfflineWreckageBakeVertexDTO>(nameof(OfflineWreckageBakeVertexDTO.Uv3AupLocal), 52);
            ok &= ValidateSize<WreckageDeformationProfileDTO>(DeformationProfileStrideBytes);
            ok &= ValidateSize<OfflineWreckageBakeCounters64>(BakeCountersStrideBytes);
            ok &= ValidateOffset<OfflineWreckageBakeCounters64>(nameof(OfflineWreckageBakeCounters64.ActiveVertexCount), 0);
            ok &= ValidateOffset<OfflineWreckageBakeCounters64>(nameof(OfflineWreckageBakeCounters64.TornVertexCount), 4);
            ok &= ValidateOffset<OfflineWreckageBakeCounters64>(nameof(OfflineWreckageBakeCounters64.DegenerateTriangleCount), 8);
            ok &= ValidateOffset<OfflineWreckageBakeCounters64>(nameof(OfflineWreckageBakeCounters64.HullVertexCount), 12);
            ok &= ValidateOffset<OfflineWreckageBakeCounters64>(nameof(OfflineWreckageBakeCounters64.WarningFlags), 16);
            ok &= ValidateOffset<OfflineWreckageBakeCounters64>(nameof(OfflineWreckageBakeCounters64.ActiveIndexCount), 20);
            ok &= ValidateOffset<OfflineWreckageBakeCounters64>(nameof(OfflineWreckageBakeCounters64.FractureHoleTriangleCount), 24);
            ok &= ValidateOffset<OfflineWreckageBakeCounters64>(nameof(OfflineWreckageBakeCounters64._pad0), 28);
            ok &= ValidateOffset<OfflineWreckageBakeCounters64>(nameof(OfflineWreckageBakeCounters64._pad2), 32);
            ok &= ValidateOffset<OfflineWreckageBakeCounters64>(nameof(OfflineWreckageBakeCounters64._pad3), 40);
            ok &= ValidateOffset<OfflineWreckageBakeCounters64>(nameof(OfflineWreckageBakeCounters64._pad4), 48);
            ok &= ValidateOffset<OfflineWreckageBakeCounters64>(nameof(OfflineWreckageBakeCounters64._pad5), 56);
            ok &= ValidateSize<OfflineWreckageTelemetryEntry>(TelemetryStrideBytes);
            ok &= ValidateSize<OfflineWreckageSubMeshIndexRangeDTO>(SubMeshIndexRangeStrideBytes);
            ok &= ValidateOffset<OfflineWreckageSubMeshIndexRangeDTO>(nameof(OfflineWreckageSubMeshIndexRangeDTO.SourceIndexStart), 0);
            ok &= ValidateOffset<OfflineWreckageSubMeshIndexRangeDTO>(nameof(OfflineWreckageSubMeshIndexRangeDTO.IndexCount), 4);
            ok &= ValidateOffset<OfflineWreckageSubMeshIndexRangeDTO>(nameof(OfflineWreckageSubMeshIndexRangeDTO.DestinationIndexStart), 8);
            ok &= ValidateOffset<OfflineWreckageSubMeshIndexRangeDTO>(nameof(OfflineWreckageSubMeshIndexRangeDTO.BaseVertex), 12);

            if (ok && logSuccess)
                Debug.Log("[WRECKAGE_1717] Offline wreckage DTO layout validated.");

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
