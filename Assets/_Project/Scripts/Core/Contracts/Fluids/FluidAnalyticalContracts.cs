using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts.Fluids
{
    public static class FluidAnalyticalContractConstants
    {
        public const int MaxAnalyticalThrusterCount = 4;
        public const int MaxAnalyticalWhirlpoolCount = 2;
        public const int MaxActiveMaelstromCount = MaxAnalyticalWhirlpoolCount;
        public const int MaxDynamicViscosityRegionCount = 4;
        public const float MaelstromMaxVelocityMetersPerSecond = 18f;
        public const float MaelstromMinimumMathDetailMaxVelocityMetersPerSecond = 10f;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ActiveThrusterFlow
    {
        [FieldOffset(0)] public float3 PositionWS;
        [FieldOffset(12)] public float3 DirectionWS;
        [FieldOffset(24)] public float Strength;
        [FieldOffset(28)] public float RadiusSq;
        [FieldOffset(32)] public float InvRadiusSq;
        [FieldOffset(36)] public float ConeCos;
        [FieldOffset(40)] public int Active;
        [FieldOffset(44)] public float Padding0;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct WhirlpoolFlow
    {
        [FieldOffset(0)] public float3 CenterWS;
        [FieldOffset(12)] public float RadiusSq;
        [FieldOffset(16)] public float InvRadiusSq;
        [FieldOffset(20)] public float TangentialStrength;
        [FieldOffset(24)] public float CentripetalStrength;
        [FieldOffset(28)] public float VerticalPull;
        [FieldOffset(32)] public int Active;
        [FieldOffset(36)] public float Padding0;
        [FieldOffset(40)] public float Padding1;
        [FieldOffset(44)] public float Padding2;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FluidViscosityRegion
    {
        [FieldOffset(0)] public float3 CenterWS;
        [FieldOffset(12)] public float InvRadiusSq;
        [FieldOffset(16)] public float ViscosityMultiplier;
        [FieldOffset(20)] public int Active;
        [FieldOffset(24)] public float Padding0;
        [FieldOffset(28)] public float Padding1;
    }

    public static class FluidAnalyticalContractMath
    {
        public static float3 SampleWhirlpoolVelocity(
            float3 samplePosition,
            NativeArray<WhirlpoolFlow>.ReadOnly whirlpools,
            int whirlpoolCount,
            byte simplifiedMathEnabled,
            float maxVelocityMetersPerSecond)
        {
            if (!whirlpools.IsCreated || whirlpoolCount <= 0)
                return float3.zero;

            float3 velocity = float3.zero;
            int count = math.min(math.max(0, whirlpoolCount), whirlpools.Length);
            for (int i = 0; i < count; i++)
            {
                velocity += SampleWhirlpoolVelocity(
                    samplePosition,
                    whirlpools[i],
                    simplifiedMathEnabled,
                    maxVelocityMetersPerSecond);
            }

            return ClampFiniteFloat3Magnitude(velocity, maxVelocityMetersPerSecond);
        }

        public static float3 SampleWhirlpoolVelocity(
            float3 samplePosition,
            WhirlpoolFlow whirlpool,
            byte simplifiedMathEnabled,
            float maxVelocityMetersPerSecond)
        {
            if (whirlpool.Active == 0 || whirlpool.RadiusSq <= 0f || whirlpool.InvRadiusSq <= 0f)
                return float3.zero;

            if (!math.all(math.isfinite(whirlpool.CenterWS)) ||
                !math.isfinite(whirlpool.TangentialStrength) ||
                !math.isfinite(whirlpool.CentripetalStrength) ||
                !math.isfinite(whirlpool.VerticalPull))
            {
                return float3.zero;
            }

            float3 toCenter = whirlpool.CenterWS - samplePosition;
            toCenter.y = 0f;
            float distanceSq = math.lengthsq(toCenter);
            float normalizedDistanceSq = distanceSq * whirlpool.InvRadiusSq;
            if (distanceSq <= 0.000001f || normalizedDistanceSq > 1f)
                return float3.zero;

            float invDistance = math.rsqrt(math.max(distanceSq, 0.000001f));
            float3 inward = toCenter * invDistance;
            float3 tangent = simplifiedMathEnabled != 0
                ? float3.zero
                : math.cross(new float3(0f, 1f, 0f), toCenter) * invDistance;
            float falloff = math.saturate(1f - normalizedDistanceSq);
            float inverseSqGain = math.min(8f, whirlpool.RadiusSq * math.rcp(math.max(1f, distanceSq)));
            float3 velocity =
                ((inward * whirlpool.CentripetalStrength) +
                 (tangent * whirlpool.TangentialStrength)) *
                (falloff * inverseSqGain);
            velocity.y -= whirlpool.VerticalPull * falloff;
            return ClampFiniteFloat3Magnitude(
                velocity,
                simplifiedMathEnabled != 0
                    ? math.min(maxVelocityMetersPerSecond, FluidAnalyticalContractConstants.MaelstromMinimumMathDetailMaxVelocityMetersPerSecond)
                    : maxVelocityMetersPerSecond);
        }

        public static float3 ClampFiniteFloat3Magnitude(float3 value, float maxMagnitude)
        {
            if (!math.all(math.isfinite(value)))
                return float3.zero;

            float maxSafe = math.max(0f, maxMagnitude);
            float lengthSq = math.lengthsq(value);
            if (lengthSq <= maxSafe * maxSafe || lengthSq <= 0.000001f)
                return value;

            return value * (maxSafe * math.rsqrt(lengthSq));
        }
    }
}
