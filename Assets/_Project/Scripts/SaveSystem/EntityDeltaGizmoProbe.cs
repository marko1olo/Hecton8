#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.SaveSystem
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class EntityDeltaGizmoProbe : MonoBehaviour
    {
        public bool DrawHeatmap = true;
        [Range(0.05f, 1f)] public float Alpha = 0.72f;
        public uint RedlineCompressedBytes = 131072u;

        private void OnDrawGizmos()
        {
            if (!DrawHeatmap)
                return;

            DrawSectorHeatmap(math.max(1u, RedlineCompressedBytes), math.saturate(Alpha));
        }

        public static void DrawSectorHeatmap(uint redlineCompressedBytes, float alpha01)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetGenerationHandle(BufferID.SaveEntityDeltaSectorStats, out VaultGenerationHandle<EntityDeltaSectorStatsDTO> handle) ||
                !vault.TryResolveHandle(in handle, out NativeArray<EntityDeltaSectorStatsDTO> stats))
            {
                return;
            }

            if (!stats.IsCreated)
                return;

            float redline = math.max(1f, redlineCompressedBytes);
            float alpha = math.saturate(alpha01);
            for (int i = 0; i < stats.Length; i++)
            {
                EntityDeltaSectorStatsDTO stat = stats[i];
                if (stat.DeltaEntities == 0u)
                    continue;

                float heat = math.saturate(stat.CompressedBytes / redline);
                Gizmos.color = Color.Lerp(
                    new Color(0.05f, 0.85f, 0.35f, 0.22f * alpha),
                    new Color(1f, 0.08f, 0.02f, 0.78f * alpha),
                    heat);
                Vector3 center = new Vector3(
                    stat.SectorX * EntityDeltaCompressionArchitecture.DefaultSectorMeters,
                    stat.SectorY * EntityDeltaCompressionArchitecture.DefaultSectorMeters,
                    stat.SectorZ * EntityDeltaCompressionArchitecture.DefaultSectorMeters);
                Gizmos.DrawWireCube(center, Vector3.one * EntityDeltaCompressionArchitecture.DefaultSectorMeters);
            }
        }
    }
}
#endif
