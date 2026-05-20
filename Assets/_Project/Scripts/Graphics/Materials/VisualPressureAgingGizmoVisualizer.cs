using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Graphics.Materials
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Rendering/Visual Pressure Aging Gizmo")]
    public sealed class VisualPressureAgingGizmoVisualizer : MonoBehaviour
    {
        [SerializeField, Range(1, 512)] private int maxRings = 128;

        private void OnDrawGizmos()
        {
            if (!VisualPressureAgingRuntime.TryAcquireAgingBufferRead(out NativeArray<VisualAgingParamsDTO> aging, out int activeCount))
                return;

            try
            {
                int count = math.min(math.max(1, maxRings), math.min(activeCount, aging.Length));
                for (int i = 0; i < count; i++)
                {
                    VisualAgingParamsDTO dto = aging[i];
                    float heat = math.saturate(math.max(dto.RustAndCorrosion.x, dto.StressAndMicroFractures.y));
                    Gizmos.color = Color.Lerp(new Color(0.18f, 0.72f, 0.44f, 0.38f), new Color(0.95f, 0.12f, 0.08f, 0.68f), heat);
                    Gizmos.DrawWireSphere(
                        new Vector3(dto.DepthAndPressure.x, dto.DepthAndPressure.y, dto.DepthAndPressure.z),
                        0.35f + dto.DepthAndPressure.w * 1.15f);
                }
            }
            finally
            {
                VisualPressureAgingRuntime.ReleaseAgingBufferRead();
            }
        }
    }
}
