#if UNITY_EDITOR
namespace Hecton8.Tools
{
    using Hecton8.Core;
    using Hecton8.Core.Contracts;
    using Unity.Mathematics;
    using UnityEngine;

    public sealed class LaserCutterDodDebugGizmo : MonoBehaviour
    {
        [SerializeField, Range(1, LaserCutterDodConstants.MaxRequests)] private int requestCount = 16;
        [SerializeField] private Color beamColor = new Color(1f, 0.18f, 0.02f, 0.9f);
        [SerializeField] private Color originColor = new Color(0.05f, 0.8f, 1f, 0.9f);
        [SerializeField, Min(0.01f)] private float originRadius = 0.035f;

        private void OnDrawGizmos()
        {
            double3 presentationOrigin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            int safeCount = math.clamp(requestCount, 1, LaserCutterDodConstants.MaxRequests);
            for (int i = 0; i < safeCount; i++)
            {
                if (!LaserCutterDodRuntime.TryGetRequestForGizmo(i, out LaserCutRequestDTO request))
                    break;

                float3 localOrigin = AupPrecisionMath.LocalDeltaFloat3(request.RayOriginAUP, presentationOrigin, float3.zero);
                float3 direction = SafeNormalize(request.RayDirection, new float3(0f, 0f, 1f));
                Vector3 origin = new Vector3(localOrigin.x, localOrigin.y, localOrigin.z);
                Vector3 end = origin + new Vector3(direction.x, direction.y, direction.z) * math.max(0.01f, request.MaximumDistance);

                Gizmos.color = originColor;
                Gizmos.DrawSphere(origin, originRadius);
                Gizmos.color = beamColor;
                Gizmos.DrawLine(origin, end);
            }
        }

        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return fallback;

            return value * math.rsqrt(lengthSq);
        }
    }
}
#endif
