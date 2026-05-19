using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Power
{
    [ExecuteAlways]
    public sealed class SubmarineOsThermalGridGizmo : MonoBehaviour
    {
        [SerializeField] private bool drawHeatmap = true;
        [SerializeField] private float sphereRadius = 0.18f;
        [SerializeField] private float thermalRadiusScale = 0.65f;

        private void OnDrawGizmos()
        {
            if (!drawHeatmap)
                return;

            SubmarineOsThermalGridRuntime runtime = SubmarineOsThermalGridRuntime.Active;
            if (runtime == null ||
                !runtime.TryGetGridReadback(out NativeArray<GridNodeDTO> nodes, out NativeArray<ThermalGridAnchorDTO> anchors, out _, out int nodeCount))
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
            }
        }
    }
}
