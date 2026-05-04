using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    public struct BiomeTransitionSample
    {
        public byte FromBiomeId;
        public byte ToBiomeId;
        public byte Blend255;
        public byte Flags;
    }

    public struct BiomeTransitionFogSource
    {
        public float4 FogColor;
        public float Density;
        public float Turbidity;
        public float Absorption;
    }

    public struct BiomeTransitionFogResult
    {
        public BiomeTransitionSample Sample;
        public float4 FogColor;
        public float Density;
        public float Turbidity;
        public float Absorption;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct BiomeTransitionFogBlendJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<BiomeTransitionSample> Samples;
        [ReadOnly] public NativeArray<BiomeTransitionFogSource> FogSourcesByBiomeId;
        [ReadOnly] public NativeArray<AbsoluteUniversePositionBlit128> FromAup;
        [ReadOnly] public NativeArray<AbsoluteUniversePositionBlit128> ToAup;
        [ReadOnly] public NativeArray<AbsoluteUniversePositionBlit128> PlayerAup;
        [WriteOnly] public NativeArray<BiomeTransitionFogResult> Results;
        public float TransitionLengthMeters;

        public void Execute(int index)
        {
            BiomeTransitionSample sample = Samples[index];
            BiomeTransitionFogSource from = ResolveSource(sample.FromBiomeId);
            BiomeTransitionFogSource to = ResolveSource(sample.ToBiomeId);
            float blend = sample.Blend255 * (1f / 255f);
            blend = ResolveAupBlend(index, blend);
            float smoothBlend = blend * blend * (3f - 2f * blend);

            Results[index] = new BiomeTransitionFogResult
            {
                Sample = new BiomeTransitionSample
                {
                    FromBiomeId = sample.FromBiomeId,
                    ToBiomeId = sample.ToBiomeId,
                    Blend255 = (byte)math.round(math.saturate(smoothBlend) * 255f),
                    Flags = sample.Flags
                },
                FogColor = math.lerp(from.FogColor, to.FogColor, smoothBlend),
                Density = math.lerp(from.Density, to.Density, smoothBlend),
                Turbidity = math.lerp(from.Turbidity, to.Turbidity, smoothBlend),
                Absorption = math.lerp(from.Absorption, to.Absorption, smoothBlend)
            };
        }

        private BiomeTransitionFogSource ResolveSource(byte biomeId)
        {
            if (!FogSourcesByBiomeId.IsCreated || FogSourcesByBiomeId.Length == 0)
                return default;

            int index = math.clamp((int)biomeId, 0, FogSourcesByBiomeId.Length - 1);
            return FogSourcesByBiomeId[index];
        }

        private float ResolveAupBlend(int index, float fallbackBlend)
        {
            if (!FromAup.IsCreated ||
                !ToAup.IsCreated ||
                !PlayerAup.IsCreated ||
                index >= FromAup.Length ||
                index >= ToAup.Length ||
                index >= PlayerAup.Length)
            {
                return math.saturate(fallbackBlend);
            }

            double3 from = ToAbsoluteDouble3(FromAup[index]);
            double3 to = ToAbsoluteDouble3(ToAup[index]);
            double3 player = ToAbsoluteDouble3(PlayerAup[index]);
            double3 segment = to - from;
            double lengthSq = math.max(1e-6d, math.dot(segment, segment));
            double projected = math.dot(player - from, segment) / lengthSq;
            float segmentBlend = math.saturate((float)projected);

            float transitionLength = math.max(0.001f, TransitionLengthMeters);
            float segmentLength = (float)math.sqrt(lengthSq);
            if (segmentLength > transitionLength)
            {
                float halfWindow = math.saturate(transitionLength / segmentLength) * 0.5f;
                float lower = math.max(0f, 0.5f - halfWindow);
                float upper = math.min(1f, 0.5f + halfWindow);
                segmentBlend = math.saturate((segmentBlend - lower) / math.max(0.001f, upper - lower));
            }

            return segmentBlend;
        }

        private static double3 ToAbsoluteDouble3(AbsoluteUniversePositionBlit128 position)
        {
            const double CellSizeMeters = 5000d;
            return new double3(
                position.GridX * CellSizeMeters + position.Local.x,
                position.GridY * CellSizeMeters + position.Local.y,
                position.GridZ * CellSizeMeters + position.Local.z);
        }
    }
}
