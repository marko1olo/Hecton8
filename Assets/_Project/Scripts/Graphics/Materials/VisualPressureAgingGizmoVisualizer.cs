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

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            bool hasAging = VisualPressureAgingRuntime.TryOpenAgingBufferSnapshotLease(
                out NativeArray<VisualAgingParamsDTO>.ReadOnly aging,
                out int agingActiveCount);
            bool hasDegradation = VisualPressureAgingRuntime.TryOpenDegradationBufferSnapshotLease(
                out NativeArray<InstanceDegradationDTO>.ReadOnly degradation,
                out int degradationActiveCount);

            if (!hasAging && !hasDegradation)
                return;

            try
            {
                int agingCount = hasAging ? math.min(agingActiveCount, aging.Length) : 0;
                int degradationCount = hasDegradation ? math.min(degradationActiveCount, degradation.Length) : 0;
                int count = math.min(math.max(1, maxRings), math.max(agingCount, degradationCount));
                for (int i = 0; i < count; i++)
                {
                    bool hasAgingRow = i < agingCount;
                    bool hasDegradationRow = i < degradationCount;
                    if (!hasAgingRow && !hasDegradationRow)
                        continue;

                    VisualAgingParamsDTO agingDto = hasAgingRow ? aging[i] : default;
                    InstanceDegradationDTO degradationDto = hasDegradationRow ? degradation[i] : default;
                    float rust = hasDegradationRow ? degradationDto.RustAmount : agingDto.RustAndCorrosion.x;
                    float scorch = hasDegradationRow ? degradationDto.ScorchAmount : agingDto.StressAndMicroFractures.y;
                    float bio = hasDegradationRow ? degradationDto.BioFouling : agingDto.SaltAndBiomass.y;
                    float stress = hasDegradationRow ? degradationDto.StructuralStress : agingDto.StressAndMicroFractures.x;
                    if (!math.isfinite(rust) || !math.isfinite(scorch) || !math.isfinite(bio) || !math.isfinite(stress))
                        continue;
                    if (hasAgingRow && !math.all(math.isfinite(agingDto.DepthAndPressure)))
                        continue;

                    float heat = math.saturate(math.max(scorch, math.max(rust, stress)));
                    float fouling = math.saturate(bio);
                    float pressure = hasAgingRow ? math.saturate(agingDto.DepthAndPressure.w) : math.saturate(stress);
                    Vector3 position = hasAgingRow
                        ? new Vector3(agingDto.DepthAndPressure.x, agingDto.DepthAndPressure.y, agingDto.DepthAndPressure.z)
                        : transform.position + new Vector3((i & 15) * 0.45f, 0.0f, (i >> 4) * 0.45f);
                    Color cold = Color.Lerp(new Color(0.18f, 0.72f, 0.44f, 0.38f), new Color(0.12f, 0.48f, 0.30f, 0.52f), fouling);
                    Gizmos.color = Color.Lerp(cold, new Color(0.95f, 0.12f, 0.08f, 0.68f), heat);
                    Gizmos.DrawWireSphere(position, 0.35f + pressure * 1.15f);
                }
            }
            finally
            {
                if (hasDegradation)
                    VisualPressureAgingRuntime.CloseDegradationBufferSnapshotLease();
                if (hasAging)
                    VisualPressureAgingRuntime.CloseAgingBufferSnapshotLease();
            }
        }
#endif
    }
}
