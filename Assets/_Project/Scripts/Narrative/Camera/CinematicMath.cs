using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Narrative.Camera
{
    public static class CinematicMath
    {
        private const float MinDirectionSq = 0.000001f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion FastNlerp(Quaternion current, Vector3 targetDirection, float blend01, Vector3 up)
        {
            Vector3 safeDirection = ResolveSafeDirection(targetDirection, current * Vector3.forward);
            Vector3 safeUp = up.sqrMagnitude > MinDirectionSq ? up : Vector3.up;
            Quaternion target = Quaternion.LookRotation(safeDirection, safeUp);
            return FastNlerp(current, target, blend01);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion FastNlerp(Quaternion from, Quaternion to, float blend01)
        {
            float t = math.saturate(blend01);
            if (t <= 0f)
                return NormalizeOrIdentity(from);

            float dot = (from.x * to.x) + (from.y * to.y) + (from.z * to.z) + (from.w * to.w);
            float sign = dot < 0f ? -1f : 1f;
            Quaternion blended = new Quaternion(
                from.x + ((to.x * sign) - from.x) * t,
                from.y + ((to.y * sign) - from.y) * t,
                from.z + ((to.z * sign) - from.z) * t,
                from.w + ((to.w * sign) - from.w) * t);
            return NormalizeOrIdentity(blended);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 ResolveSafeDirection(Vector3 direction, Vector3 fallback)
        {
            float3 d = new float3(direction.x, direction.y, direction.z);
            float lengthSq = math.lengthsq(d);
            if (!math.isfinite(lengthSq) || lengthSq <= MinDirectionSq)
            {
                float3 f = new float3(fallback.x, fallback.y, fallback.z);
                float fallbackSq = math.lengthsq(f);
                if (!math.isfinite(fallbackSq) || fallbackSq <= MinDirectionSq)
                    return Vector3.forward;

                float fallbackInv = math.rsqrt(fallbackSq);
                return new Vector3(f.x * fallbackInv, f.y * fallbackInv, f.z * fallbackInv);
            }

            float inv = math.rsqrt(lengthSq);
            return new Vector3(d.x * inv, d.y * inv, d.z * inv);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Quaternion NormalizeOrIdentity(Quaternion value)
        {
            float lengthSq =
                (value.x * value.x) +
                (value.y * value.y) +
                (value.z * value.z) +
                (value.w * value.w);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.00000001f)
                return Quaternion.identity;

            float inv = math.rsqrt(lengthSq);
            return new Quaternion(value.x * inv, value.y * inv, value.z * inv, value.w * inv);
        }
    }
}
