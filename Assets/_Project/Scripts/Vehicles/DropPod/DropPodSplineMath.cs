namespace Hecton8.Vehicles.DropPod
{
    using Unity.Mathematics;
    using UnityEngine;

    public static class DropPodSplineMath
    {
        public const float MinimumDurationSeconds = 0.05f;
        private const float TinyLengthSq = 0.000001f;
        private const float MaxAuthoringEulerDegrees = 360f;

        public static float SanitizeDuration(float seconds)
        {
            return math.isfinite(seconds) ? math.max(MinimumDurationSeconds, seconds) : 1f;
        }

        public static float SmoothStep01(float t)
        {
            float x = SanitizeUnit01(t);
            return x * x * (3f - (2f * x));
        }

        public static float SanitizeUnit01(float value)
        {
            return math.saturate(math.isfinite(value) ? value : 0f);
        }

        public static float SanitizeRange(float value, float min, float max, float fallback)
        {
            float low = math.min(min, max);
            float high = math.max(min, max);
            float safe = math.isfinite(value) ? value : fallback;
            if (!math.isfinite(safe))
                safe = low;

            return math.clamp(safe, low, high);
        }

        public static float ResolveTransitT(float elapsedSeconds, float durationSeconds)
        {
            float duration = SanitizeDuration(durationSeconds);
            float raw = SanitizeUnit01((math.isfinite(elapsedSeconds) ? elapsedSeconds : 0f) / duration);
            return SmoothStep01(raw);
        }

        public static Vector3 ResolveBezierPosition(Vector3 start, Vector3 controlA, Vector3 controlB, Vector3 end, float t)
        {
            float x = SanitizeUnit01(t);
            float omt = 1f - x;
            float omt2 = omt * omt;
            float t2 = x * x;
            return (start * (omt2 * omt)) +
                   (controlA * (3f * omt2 * x)) +
                   (controlB * (3f * omt * t2)) +
                   (end * (t2 * x));
        }

        public static Quaternion ResolveNlerp(Quaternion from, Quaternion to, float t)
        {
            return ResolveSlerp(from, to, t);
        }

        public static Quaternion ResolveSlerp(Quaternion from, Quaternion to, float t)
        {
            if (!IsFinite(from))
                from = Quaternion.identity;
            if (!IsFinite(to))
                to = Quaternion.identity;

            float x = SanitizeUnit01(t);
            float4 a = new float4(from.x, from.y, from.z, from.w);
            float4 b = new float4(to.x, to.y, to.z, to.w);
            float lengthA = math.dot(a, a);
            float lengthB = math.dot(b, b);
            if (lengthA <= TinyLengthSq || lengthB <= TinyLengthSq || !math.isfinite(lengthA) || !math.isfinite(lengthB))
                return Quaternion.identity;

            a *= math.rsqrt(lengthA);
            b *= math.rsqrt(lengthB);
            b = math.select(b, -b, math.dot(a, b) < 0f);
            float4 value = math.slerp(new quaternion(a), new quaternion(b), x).value;
            float lengthSq = math.dot(value, value);
            if (lengthSq <= TinyLengthSq || !math.isfinite(lengthSq))
                return Quaternion.identity;

            value *= math.rsqrt(lengthSq);
            return new Quaternion(value.x, value.y, value.z, value.w);
        }

        public static Quaternion ResolveLocalEulerNoAlloc(Vector3 degrees)
        {
            if (!IsFinite(degrees))
                return Quaternion.identity;

            Vector3 safeDegrees = new Vector3(
                SanitizeAuthoringEulerDegrees(degrees.x),
                SanitizeAuthoringEulerDegrees(degrees.y),
                SanitizeAuthoringEulerDegrees(degrees.z));
            return Quaternion.Euler(safeDegrees);
        }

        private static float SanitizeAuthoringEulerDegrees(float degrees)
        {
            return SanitizeRange(degrees, -MaxAuthoringEulerDegrees, MaxAuthoringEulerDegrees, 0f);
        }

        public static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        public static bool IsFinite(Quaternion value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z) &&
                   float.IsFinite(value.w);
        }

        public static float ApproxSinBhaskara(float radians)
        {
            const float pi = 3.14159274f;
            const float twoPi = 6.28318548f;
            float x = radians - (twoPi * math.floor((radians + pi) / twoPi));
            float sign = x < 0f ? -1f : 1f;
            float ax = math.abs(x);
            float numerator = 16f * ax * (pi - ax);
            float denominator = (5f * pi * pi) - (4f * ax * (pi - ax));
            float value = denominator > TinyLengthSq ? numerator / denominator : 0f;
            return sign * value;
        }
    }
}
