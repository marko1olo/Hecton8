// ============================================================================
// HECTON-8 - BaseAtmosphereLogisticsGizmo.cs
// Scene gizmo hook for live gas cells.
// ============================================================================

using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Atmosphere
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Atmosphere/Base Atmosphere Logistics Gizmo")]
    public sealed class BaseAtmosphereLogisticsGizmo : MonoBehaviour
    {
        [SerializeField, Range(1, 128)] private int stride = 24;
        [SerializeField, Range(0.1f, 4f)] private float cubeSizeMeters = 0.55f;
        [SerializeField] private bool drawOnlyWhenSelected;

        private void OnDrawGizmos()
        {
            if (!drawOnlyWhenSelected)
                DrawCells();
        }

        private void OnDrawGizmosSelected()
        {
            if (drawOnlyWhenSelected)
                DrawCells();
        }

        private void DrawCells()
        {
            int safeStride = math.max(1, stride);
            for (int i = 0; i < AtmosphereLogisticsConstants.MaxMockNodes; i += safeStride)
            {
                if (!BaseAtmosphereLogisticsRuntime.TryGetGizmoCell(i, out AtmosphereNodeDTO node, out AtmosphereCellDTO cell, out int nodeCount))
                    return;

                if (i >= nodeCount)
                    return;

                float hazard = math.saturate(cell.CarbonDioxide01 * 16f + cell.Toxin01 * 8f);
                Gizmos.color = Color.Lerp(new Color(0.1f, 0.75f, 0.35f, 0.55f), new Color(1f, 0.15f, 0.05f, 0.75f), hazard);
                Vector3 runtime = HectonFloatingOrigin.ToRuntimePosition(node.Aup);
                Gizmos.DrawWireCube(runtime, Vector3.one * cubeSizeMeters);
            }
        }
    }
}
