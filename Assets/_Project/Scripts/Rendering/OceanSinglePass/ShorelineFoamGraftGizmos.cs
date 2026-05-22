using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Rendering.OceanSinglePass
{
    [DisallowMultipleComponent]
    public sealed class ShorelineFoamGraftGizmos : MonoBehaviour
    {
        [SerializeField] private float debugExtentMeters = 8f;
        [SerializeField] private float debugSpacingMeters = 1.35f;

        private void OnDrawGizmos()
        {
            if (!ShorelineFoamGraftRuntime.TryReadDebugFoam(out NativeArray<ShorelineFoamParamsDTO>.ReadOnly foamParams, out int count))
                return;

            int safeCount = math.min(count, foamParams.Length);
            Vector3 origin = transform.position;
            for (int i = 0; i < safeCount; i++)
            {
                ShorelineFoamParamsDTO dto = foamParams[i];
                float opacity = math.saturate(dto.FoamIntensityAndFalloff.w * dto.QualityAndLimits.w);
                if (opacity <= 0.0001f)
                    continue;

                float lane = i - safeCount * 0.5f;
                float extent = math.max(0.1f, debugExtentMeters + dto.FoamIntensityAndFalloff.y * 0.15f);
                Vector3 center = origin + new Vector3(lane * debugSpacingMeters, dto.FoamIntensityAndFalloff.z, 0f);
                Vector3 size = new Vector3(extent, 0.12f + opacity * 0.35f, extent * 0.28f);
                Gizmos.color = new Color(0.55f, 0.92f, 1f, opacity);
                Gizmos.DrawWireCube(center, size);
            }
        }
    }
}
