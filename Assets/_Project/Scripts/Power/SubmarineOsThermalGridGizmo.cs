using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Power
{
    [ExecuteAlways]
    public sealed class SubmarineOsThermalGridGizmo : MonoBehaviour
    {
        [SerializeField] private bool drawHeatmap = true;
        [SerializeField] private bool drawDivergence = true;
        [SerializeField] private float sphereRadius = 0.18f;
        [SerializeField] private float thermalRadiusScale = 0.65f;
        [SerializeField] private float divergenceRadiusScale = 1.8f;

        private void OnDrawGizmos()
        {
            if (!drawHeatmap)
                return;

            SubmarineOsThermalGridRuntime runtime = SubmarineOsThermalGridRuntime.Active;
            if (runtime == null ||
                !runtime.TryGetGridReadback(
                    out NativeArray<GridNodeDTO>.ReadOnly nodes,
                    out NativeArray<ThermalGridAnchorDTO>.ReadOnly anchors,
                    out _,
                    out int nodeCount))
            {
                return;
            }

            int count = math.min(nodeCount, math.min(nodes.Length, anchors.Length));
            for (int i = 0; i < count; i++)
            {
                GridNodeDTO node = nodes[i];
                float voltage = math.saturate(node.Potential);
                float thermal = math.saturate(node.ThermalLoad);
                Vector3 position = transform.TransformPoint(anchors[i].LocalOffset);
                Gizmos.color = Color.Lerp(Color.red, Color.green, voltage);
                Gizmos.DrawSphere(position, math.max(0.01f, sphereRadius));
                Gizmos.color = new Color(1f, 0.35f, 0.05f, math.lerp(0.08f, 0.9f, thermal));
                Gizmos.DrawWireSphere(position, math.max(sphereRadius, sphereRadius + thermal * thermalRadiusScale));
                if (drawDivergence && (node.Flags & SubmarineThermalGridStatusFlags.FaultDivergent) != 0u)
                {
                    float pulse = 0.5f + 0.5f * math.sin((float)Time.realtimeSinceStartup * 8f);
                    Gizmos.color = new Color(1f, 0f, 0f, 0.55f + pulse * 0.45f);
                    Gizmos.DrawWireSphere(position, math.max(sphereRadius, sphereRadius * divergenceRadiusScale * (1f + pulse * 0.25f)));
                }
            }
        }
    }
}
