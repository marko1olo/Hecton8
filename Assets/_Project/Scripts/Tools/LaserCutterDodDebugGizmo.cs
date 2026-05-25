#if UNITY_EDITOR
namespace Hecton8.Tools
{
    using Hecton8.Core;
    using Hecton8.Core.Contracts;
    using Unity.Mathematics;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class LaserCutterDodDebugGizmo : MonoBehaviour
    {
        [SerializeField, Range(1, LaserCutterDodConstants.MaxRequests)] private int requestCount = 16;
        [SerializeField] private Color beamColor = new Color(1f, 0.18f, 0.02f, 0.9f);
        [SerializeField] private Color originColor = new Color(0.05f, 0.8f, 1f, 0.9f);
        [SerializeField] private Color hitColor = new Color(0.05f, 1f, 0.28f, 0.95f);
        [SerializeField] private Color normalColor = new Color(1f, 0.9f, 0.05f, 0.95f);
        [SerializeField, Min(0.01f)] private float originRadius = 0.035f;
        [SerializeField, Min(0.01f)] private float hitRadius = 0.08f;
        [SerializeField, Min(0.01f)] private float normalLength = 0.35f;

        private void OnDrawGizmos()
        {
            if (!LaserCutterDodRuntime.TryGetPresentationOriginForGizmo(out double3 presentationOrigin))
                return;

            int safeCount = math.clamp(requestCount, 1, LaserCutterDodConstants.MaxRequests);
            for (int i = 0; i < safeCount; i++)
            {
                if (!LaserCutterDodRuntime.TryGetRequestForGizmo(i, out LaserCutRequestDTO request, out LaserCutRequestMetaDTO meta))
                    break;
                if ((meta.Flags & LaserCutterDodConstants.RequestFlagSuppressedByCooldown) != 0u)
                    continue;

                float3 localOrigin = AupPrecisionMath.LocalDeltaFloat3(request.RayOriginAUP, presentationOrigin, float3.zero);
                float3 direction = SafeNormalize(request.RayDirection, new float3(0f, 0f, 1f));
                Vector3 origin = new Vector3(localOrigin.x, localOrigin.y, localOrigin.z);
                Vector3 end = origin + new Vector3(direction.x, direction.y, direction.z) * math.max(0.01f, request.MaximumDistance);

                Gizmos.color = originColor;
                Gizmos.DrawSphere(origin, originRadius);
                Gizmos.color = beamColor;
                Gizmos.DrawLine(origin, end);

                if (!LaserCutterDodRuntime.TryGetHitForGizmo(i, out LaserCutHitDTO hit))
                    continue;

                float3 localHit = AupPrecisionMath.LocalDeltaFloat3(hit.HitAUP, presentationOrigin, float3.zero);
                float3 hitNormal = SafeNormalize(hit.Normal, new float3(0f, 1f, 0f));
                Vector3 hitPoint = new Vector3(localHit.x, localHit.y, localHit.z);
                Vector3 normalEnd = hitPoint + new Vector3(hitNormal.x, hitNormal.y, hitNormal.z) * normalLength;

                Gizmos.color = hitColor;
                Gizmos.DrawWireSphere(hitPoint, hitRadius);
                Gizmos.color = normalColor;
                Gizmos.DrawLine(hitPoint, normalEnd);
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
