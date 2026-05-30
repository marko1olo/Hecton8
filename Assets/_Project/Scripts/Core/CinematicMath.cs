using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Visual-only math approximations for hot presentation paths.
    /// </summary>
    public static class CinematicMath
    {
        private const float TwoPi = 6.28318530718f;
        private const float InvTwoPi = 0.15915494309f;
        private const float HalfPi = 1.57079632679f;
        private const float FastSinA = 1.27323954474f;
        private const float FastSinB = 0.40528473456f;
        private const float MinimumQuaternionLengthSq = 0.000001f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproximateLength(float3 value)
        {
            if (!math.all(math.isfinite(value)))
                return 0f;

            float3 absolute = math.abs(value);
            float maxAxis = math.max(absolute.x, math.max(absolute.y, absolute.z));
            float minAxis = math.min(absolute.x, math.min(absolute.y, absolute.z));
            float midAxis = (absolute.x + absolute.y + absolute.z) - maxAxis - minAxis;
            return maxAxis + (midAxis * 0.375f) + (minAxis * 0.25f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FastTriangleWave01(float phase)
        {
            if (!math.isfinite(phase))
                return 0f;

            float wrapped = phase - math.floor(phase);
            return 1f - math.abs((wrapped * 2f) - 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FastTriangleWaveSigned(float phase)
        {
            return (FastTriangleWave01(phase) * 2f) - 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FastSin(float radians)
        {
            if (!math.isfinite(radians))
                return 0f;

            float wrapped = radians - (math.floor((radians + math.PI) * InvTwoPi) * TwoPi);
            return (FastSinA * wrapped) - (FastSinB * wrapped * math.abs(wrapped));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FastCos(float radians)
        {
            return FastSin(radians + HalfPi);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion FastYawQuaternion(float radians)
        {
            if (!math.isfinite(radians))
                return quaternion.identity;

            float half = radians * 0.5f;
            float y = FastSin(half);
            float w = FastCos(half);
            float lengthSq = (y * y) + (w * w);
            if (!math.isfinite(lengthSq) || lengthSq <= MinimumQuaternionLengthSq)
                return quaternion.identity;

            float invLength = math.rsqrt(lengthSq);
            return new quaternion(0f, y * invLength, 0f, w * invLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion NormalizeQuaternionOrIdentity(quaternion value)
        {
            float lengthSq = math.lengthsq(value.value);
            if (!math.isfinite(lengthSq) || lengthSq <= MinimumQuaternionLengthSq)
                return quaternion.identity;

            float4 normalized = value.value * math.rsqrt(math.max(lengthSq, MinimumQuaternionLengthSq));
            return new quaternion(normalized);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion FastNlerp(quaternion from, quaternion to, float t)
        {
            float4 fromValue = from.value;
            float4 toValue = to.value;
            toValue = math.select(-toValue, toValue, math.dot(fromValue, toValue) >= 0f);
            float safeT = math.isfinite(t) ? math.saturate(t) : 0f;
            float4 blended = math.lerp(fromValue, toValue, safeT);
            float lengthSq = math.lengthsq(blended);
            if (!math.isfinite(lengthSq) || lengthSq <= MinimumQuaternionLengthSq)
                return quaternion.identity;

            float4 normalized = blended * math.rsqrt(math.max(lengthSq, MinimumQuaternionLengthSq));
            return new quaternion(normalized);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion FastNlerp(Quaternion current, Vector3 targetDirection, float blend01, Vector3 up)
        {
            Vector3 safeDirection = ResolveSafeDirection(targetDirection, current * Vector3.forward);
            Vector3 safeUp = up.sqrMagnitude > MinimumQuaternionLengthSq ? up : Vector3.up;
            Quaternion target = Quaternion.LookRotation(safeDirection, safeUp);
            return FastNlerp(current, target, blend01);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion FastNlerp(Quaternion from, Quaternion to, float t)
        {
            float clampedT = math.isfinite(t) ? math.saturate(t) : 0f;
            if (clampedT <= 0f)
                return NormalizeQuaternionOrIdentity(from);

            float dot = (from.x * to.x) + (from.y * to.y) + (from.z * to.z) + (from.w * to.w);
            float sign = dot < 0f ? -1f : 1f;
            Quaternion blended = new Quaternion(
                from.x + ((to.x * sign) - from.x) * clampedT,
                from.y + ((to.y * sign) - from.y) * clampedT,
                from.z + ((to.z * sign) - from.z) * clampedT,
                from.w + ((to.w * sign) - from.w) * clampedT);
            return NormalizeQuaternionOrIdentity(blended);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 ResolveSafeDirection(Vector3 direction, Vector3 fallback)
        {
            float3 value = new float3(direction.x, direction.y, direction.z);
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= MinimumQuaternionLengthSq)
            {
                float3 fallbackValue = new float3(fallback.x, fallback.y, fallback.z);
                float fallbackSq = math.lengthsq(fallbackValue);
                if (!math.isfinite(fallbackSq) || fallbackSq <= MinimumQuaternionLengthSq)
                    return Vector3.forward;

                float fallbackInv = math.rsqrt(fallbackSq);
                return new Vector3(fallbackValue.x * fallbackInv, fallbackValue.y * fallbackInv, fallbackValue.z * fallbackInv);
            }

            float inv = math.rsqrt(lengthSq);
            return new Vector3(value.x * inv, value.y * inv, value.z * inv);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Quaternion NormalizeQuaternionOrIdentity(Quaternion value)
        {
            float lengthSq =
                (value.x * value.x) +
                (value.y * value.y) +
                (value.z * value.z) +
                (value.w * value.w);
            if (!math.isfinite(lengthSq) || lengthSq <= MinimumQuaternionLengthSq)
                return Quaternion.identity;

            float inv = math.rsqrt(lengthSq);
            return new Quaternion(value.x * inv, value.y * inv, value.z * inv, value.w * inv);
        }
    }
}
