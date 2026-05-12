using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    public enum MathLodMode : int
    {
        Low = 0,
        High = 1
    }

    /// <summary>
    /// Distance-gated math LOD. High-tier close lanes keep high-fidelity math; lower tiers use deterministic cheap math.
    /// </summary>
    public static class DistanceMath
    {
        public const float HighQualityDistanceMeters = 15f;
        public const float HighQualityDistanceSq = HighQualityDistanceMeters * HighQualityDistanceMeters;

        private const float MinimumVectorLengthSq = 0.000001f;
        private const float MinimumDistanceSq = 0.000001f;
        private const float InvTwoPi = 0.15915494309f;
        private const float HalfPi = 1.57079632679f;
        private const string MathLodModeProperty = "_HectonMathLodMode";
        private const string MathLodDistanceSqProperty = "_HectonMathLodDistanceSq";
        private const string MathLodHighKeyword = "_MATH_LOD_HIGH";
        private const string MathLodLowKeyword = "_MATH_LOD_LOW";
        private static readonly int _mathLodModePropertyId = Shader.PropertyToID(MathLodModeProperty);
        private static readonly int _mathLodDistanceSqPropertyId = Shader.PropertyToID(MathLodDistanceSqProperty);
        private static MathLodMode _lastPushedShaderMode;
        private static bool _hasPushedShaderMode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetShaderCache()
        {
            _lastPushedShaderMode = MathLodMode.Low;
            _hasPushedShaderMode = false;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsHighQuality(float distanceSq)
        {
            return math.isfinite(distanceSq) && distanceSq < HighQualityDistanceSq;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsHighQuality(float distanceSq, HectonQualityTier scalabilityTier)
        {
            return IsHighQualityTier(scalabilityTier) && IsHighQuality(distanceSq);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsHighQualityTier(HectonQualityTier scalabilityTier)
        {
            uint tierOffset = (uint)((int)scalabilityTier - (int)HectonQualityTier.High);
            return tierOffset <= (uint)((int)HectonQualityTier.Ultra - (int)HectonQualityTier.High);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MathLodMode ResolveMathLodMode(HectonQualityTier scalabilityTier)
        {
            return IsHighQualityTier(scalabilityTier) ? MathLodMode.High : MathLodMode.Low;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Normalize(float3 value, float distanceSq)
        {
            return Normalize(value, distanceSq, new float3(0f, 0f, 1f));
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Normalize(float3 value, float distanceSq, HectonQualityTier scalabilityTier)
        {
            return Normalize(value, distanceSq, scalabilityTier, new float3(0f, 0f, 1f));
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Normalize(float3 value, float distanceSq, float3 fallback)
        {
            return Normalize(value, distanceSq, HectonQualityTier.Ultra, fallback);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Normalize(float3 value, float distanceSq, HectonQualityTier scalabilityTier, float3 fallback)
        {
            if (!math.all(math.isfinite(value)))
                return fallback;

            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= MinimumVectorLengthSq)
                return fallback;

            return IsHighQuality(distanceSq, scalabilityTier)
                ? value * math.rsqrt(lengthSq)
                : DominantAxisOrDefault(value, fallback);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sin(float radians, float distanceSq)
        {
            return Sin(radians, distanceSq, HectonQualityTier.Ultra);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sin(float radians, float distanceSq, HectonQualityTier scalabilityTier)
        {
            return IsHighQuality(distanceSq, scalabilityTier)
                ? CinematicMath.FastSin(radians)
                : TriangleSin(radians);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Cos(float radians, float distanceSq)
        {
            return Sin(radians + HalfPi, distanceSq);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Cos(float radians, float distanceSq, HectonQualityTier scalabilityTier)
        {
            return Sin(radians + HalfPi, distanceSq, scalabilityTier);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceBlend01(float distanceSq, float nearDistanceSq, float farDistanceSq)
        {
            float spanSq = math.max(MinimumDistanceSq, farDistanceSq - nearDistanceSq);
            return math.saturate((distanceSq - nearDistanceSq) * math.rcp(spanSq));
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceBlendMeters01(float distanceSq, float nearDistanceMeters, float farDistanceMeters)
        {
            float safeDistanceSq = math.max(distanceSq, MinimumDistanceSq);
            float distanceMeters = safeDistanceSq * math.rsqrt(safeDistanceSq);
            float spanMeters = math.max(0.0001f, farDistanceMeters - nearDistanceMeters);
            return math.saturate((distanceMeters - nearDistanceMeters) * math.rcp(spanMeters));
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LerpByDistanceSq(float nearValue, float farValue, float distanceSq, float nearDistanceSq, float farDistanceSq)
        {
            return math.lerp(nearValue, farValue, DistanceBlend01(distanceSq, nearDistanceSq, farDistanceSq));
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 LerpByDistanceSq(float3 nearValue, float3 farValue, float distanceSq, float nearDistanceSq, float farDistanceSq)
        {
            return math.lerp(nearValue, farValue, DistanceBlend01(distanceSq, nearDistanceSq, farDistanceSq));
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 DominantAxisOrDefault(float3 value, float3 fallback)
        {
            if (!math.all(math.isfinite(value)))
                return fallback;

            float3 absValue = math.abs(value);
            float maxAxis = math.cmax(absValue);
            float3 xAxis = new float3(math.select(1f, -1f, value.x < 0f), 0f, 0f);
            float3 yAxis = new float3(0f, math.select(1f, -1f, value.y < 0f), 0f);
            float3 zAxis = new float3(0f, 0f, math.select(1f, -1f, value.z < 0f));
            float3 yzAxis = math.select(zAxis, yAxis, absValue.y >= absValue.z);
            float3 dominantAxis = math.select(yzAxis, xAxis, absValue.x >= absValue.y & absValue.x >= absValue.z);
            return math.select(fallback, dominantAxis, maxAxis > MinimumVectorLengthSq);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PushShaderMathLod(MathLodMode mode)
        {
            if (_hasPushedShaderMode && _lastPushedShaderMode == mode)
                return;

            bool high = mode == MathLodMode.High;
            Shader.SetGlobalFloat(_mathLodModePropertyId, high ? 1f : 0f);
            Shader.SetGlobalFloat(_mathLodDistanceSqPropertyId, HighQualityDistanceSq);
            _lastPushedShaderMode = mode;
            _hasPushedShaderMode = true;

            if (high)
            {
                Shader.EnableKeyword(MathLodHighKeyword);
                Shader.DisableKeyword(MathLodLowKeyword);
                return;
            }

            Shader.DisableKeyword(MathLodHighKeyword);
            Shader.EnableKeyword(MathLodLowKeyword);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PushShaderMathLod(float frameTimeMilliseconds, float highQualityBudgetMilliseconds)
        {
            PushShaderMathLod(frameTimeMilliseconds <= highQualityBudgetMilliseconds ? MathLodMode.High : MathLodMode.Low);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PushShaderMathLod(HectonQualityTier scalabilityTier)
        {
            PushShaderMathLod(ResolveMathLodMode(scalabilityTier));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float TriangleSin(float radians)
        {
            float phase = radians * InvTwoPi;
            float wrapped = phase - math.floor(phase);
            float magnitude = CinematicMath.FastTriangleWave01(wrapped * 2f);
            return math.select(-magnitude, magnitude, wrapped < 0.5f);
        }
    }
}
