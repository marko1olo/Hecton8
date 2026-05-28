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
        private const float ShaderQualityEpsilon = 0.0005f;
        private const float InvTwoPi = 0.15915494309f;
        private const float HalfPi = 1.57079632679f;
        private const string MathLodModeProperty = "_HectonMathLodMode";
        private const string MathLodWeightProperty = "_HectonMathLodWeight";
        private const string MathLodDistanceSqProperty = "_HectonMathLodDistanceSq";
        private static readonly int _mathLodModePropertyId = Shader.PropertyToID(MathLodModeProperty);
        private static readonly int _mathLodWeightPropertyId = Shader.PropertyToID(MathLodWeightProperty);
        private static readonly int _mathLodDistanceSqPropertyId = Shader.PropertyToID(MathLodDistanceSqProperty);
        private static MathLodMode _lastPushedShaderMode;
        private static float _lastPushedShaderWeight;
        private static bool _hasPushedShaderMode;
        private static MathLodMode _pendingShaderMode;
        private static float _pendingShaderWeight;
        private static bool _hasPendingShaderState;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetShaderCache()
        {
            _lastPushedShaderMode = MathLodMode.Low;
            _lastPushedShaderWeight = -1f;
            _hasPushedShaderMode = false;
            _pendingShaderMode = MathLodMode.Low;
            _pendingShaderWeight = -1f;
            _hasPendingShaderState = false;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsHighQuality(float distanceSq)
        {
            return math.isfinite(distanceSq) && distanceSq < HighQualityDistanceSq;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsHighQuality(float distanceSq, HectonQualityTier scalabilityTier)
        {
            return IsHighQualityTier(scalabilityTier) && IsHighQuality(distanceSq);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsHighQualityTier(HectonQualityTier scalabilityTier)
        {
            uint tierOffset = (uint)((int)scalabilityTier - (int)HectonQualityTier.High);
            return tierOffset <= (uint)((int)HectonQualityTier.Ultra - (int)HectonQualityTier.High);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MathLodMode ResolveMathLodMode(HectonQualityTier scalabilityTier)
        {
            return ResolveMathLodMode(ResolveTierQualityWeight01(scalabilityTier));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MathLodMode ResolveMathLodMode(float globalQualityWeight)
        {
            return SanitizeQualityWeight01(globalQualityWeight) >= 0.5f ? MathLodMode.High : MathLodMode.Low;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveTierQualityWeight01(HectonQualityTier scalabilityTier)
        {
            switch (scalabilityTier)
            {
                case HectonQualityTier.Low:
                    return 0.2f;
                case HectonQualityTier.Mx350:
                    return 0.35f;
                case HectonQualityTier.Mid:
                    return 0.62f;
                case HectonQualityTier.High:
                    return 0.84f;
                case HectonQualityTier.Ultra:
                    return 1f;
                default:
                    return 0.5f;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveDistanceQualityWeight01(float distanceSq, float globalQualityWeight)
        {
            float safeDistanceSq = math.select(HighQualityDistanceSq, math.max(0f, distanceSq), math.isfinite(distanceSq));
            float distanceFade = 1f - DistanceBlend01(safeDistanceSq, HighQualityDistanceSq, HighQualityDistanceSq * 16f);
            return Smooth01(SanitizeQualityWeight01(globalQualityWeight)) * distanceFade;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeQualityWeight01(float globalQualityWeight)
        {
            return math.saturate(math.select(1f, globalQualityWeight, math.isfinite(globalQualityWeight)));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Normalize(float3 value, float distanceSq)
        {
            return Normalize(value, distanceSq, new float3(0f, 0f, 1f));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Normalize(float3 value, float distanceSq, HectonQualityTier scalabilityTier)
        {
            return Normalize(value, distanceSq, scalabilityTier, new float3(0f, 0f, 1f));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Normalize(float3 value, float distanceSq, float3 fallback)
        {
            return Normalize(value, distanceSq, HectonQualityTier.Ultra, fallback);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Normalize(float3 value, float distanceSq, float globalQualityWeight, float3 fallback)
        {
            if (!math.all(math.isfinite(value)))
                return fallback;

            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= MinimumVectorLengthSq)
                return fallback;

            float3 exact = value * math.rsqrt(lengthSq);
            float3 cheap = DominantAxisOrDefault(value, fallback);
            float quality = ResolveDistanceQualityWeight01(distanceSq, globalQualityWeight);
            return math.select(fallback, math.lerp(cheap, exact, quality), math.isfinite(quality));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sin(float radians, float distanceSq)
        {
            return Sin(radians, distanceSq, HectonQualityTier.Ultra);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sin(float radians, float distanceSq, HectonQualityTier scalabilityTier)
        {
            return IsHighQuality(distanceSq, scalabilityTier)
                ? CinematicMath.FastSin(radians)
                : TriangleSin(radians);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sin(float radians, float distanceSq, float globalQualityWeight)
        {
            float cheap = TriangleSin(radians);
            float exact = CinematicMath.FastSin(radians);
            return math.lerp(cheap, exact, ResolveDistanceQualityWeight01(distanceSq, globalQualityWeight));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Cos(float radians, float distanceSq)
        {
            return Sin(radians + HalfPi, distanceSq);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Cos(float radians, float distanceSq, HectonQualityTier scalabilityTier)
        {
            return Sin(radians + HalfPi, distanceSq, scalabilityTier);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Cos(float radians, float distanceSq, float globalQualityWeight)
        {
            return Sin(radians + HalfPi, distanceSq, globalQualityWeight);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceBlend01(float distanceSq, float nearDistanceSq, float farDistanceSq)
        {
            float spanSq = math.max(MinimumDistanceSq, farDistanceSq - nearDistanceSq);
            return math.saturate((distanceSq - nearDistanceSq) * math.rcp(spanSq));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DistanceBlendMeters01(float distanceSq, float nearDistanceMeters, float farDistanceMeters)
        {
            float safeDistanceSq = math.max(distanceSq, MinimumDistanceSq);
            float distanceMeters = safeDistanceSq * math.rsqrt(safeDistanceSq);
            float spanMeters = math.max(0.0001f, farDistanceMeters - nearDistanceMeters);
            return math.saturate((distanceMeters - nearDistanceMeters) * math.rcp(spanMeters));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LerpByDistanceSq(float nearValue, float farValue, float distanceSq, float nearDistanceSq, float farDistanceSq)
        {
            return math.lerp(nearValue, farValue, DistanceBlend01(distanceSq, nearDistanceSq, farDistanceSq));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 LerpByDistanceSq(float3 nearValue, float3 farValue, float distanceSq, float nearDistanceSq, float farDistanceSq)
        {
            return math.lerp(nearValue, farValue, DistanceBlend01(distanceSq, nearDistanceSq, farDistanceSq));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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
            PushShaderMathLod(mode == MathLodMode.High ? 1f : 0f, mode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PushShaderMathLod(float frameTimeMilliseconds, float highQualityBudgetMilliseconds)
        {
            float safeBudget = math.max(0.0001f, highQualityBudgetMilliseconds);
            float safeFrame = math.select(safeBudget, math.max(0f, frameTimeMilliseconds), math.isfinite(frameTimeMilliseconds));
            float pressure = math.saturate((safeFrame - safeBudget) * math.rcp(safeBudget));
            PushShaderMathLod(1f - pressure);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PushShaderMathLod(HectonQualityTier scalabilityTier)
        {
            PushShaderMathLod(ResolveTierQualityWeight01(scalabilityTier));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PushShaderMathLod(float globalQualityWeight)
        {
            float quality = SanitizeQualityWeight01(globalQualityWeight);
            PushShaderMathLod(quality, ResolveMathLodMode(quality));
        }

        public static void FlushVisualSyncShaderState()
        {
            if (!_hasPendingShaderState)
                return;

            MathLodMode mode = _pendingShaderMode;
            float quality = _pendingShaderWeight;
            _hasPendingShaderState = false;

            if (_hasPushedShaderMode &&
                _lastPushedShaderMode == mode &&
                math.abs(_lastPushedShaderWeight - quality) < ShaderQualityEpsilon)
            {
                return;
            }

            Shader.SetGlobalFloat(_mathLodModePropertyId, quality);
            Shader.SetGlobalFloat(_mathLodWeightPropertyId, quality);
            Shader.SetGlobalFloat(_mathLodDistanceSqPropertyId, HighQualityDistanceSq);
            _lastPushedShaderMode = mode;
            _lastPushedShaderWeight = quality;
            _hasPushedShaderMode = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float TriangleSin(float radians)
        {
            float phase = radians * InvTwoPi;
            float wrapped = phase - math.floor(phase);
            float magnitude = CinematicMath.FastTriangleWave01(wrapped * 2f);
            return math.select(-magnitude, magnitude, wrapped < 0.5f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PushShaderMathLod(float globalQualityWeight, MathLodMode legacyMode)
        {
            float quality = SanitizeQualityWeight01(globalQualityWeight);
            if (_hasPendingShaderState &&
                _pendingShaderMode == legacyMode &&
                math.abs(_pendingShaderWeight - quality) < ShaderQualityEpsilon)
            {
                return;
            }

            if (!_hasPendingShaderState &&
                _hasPushedShaderMode &&
                _lastPushedShaderMode == legacyMode &&
                math.abs(_lastPushedShaderWeight - quality) < ShaderQualityEpsilon)
            {
                return;
            }

            _pendingShaderMode = legacyMode;
            _pendingShaderWeight = quality;
            _hasPendingShaderState = true;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }
    }
}
