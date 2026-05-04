using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Biome transition lane sample packed for Burst fog blending.
    /// </summary>
    public struct BiomeTransitionSample
    {
        /// <summary>Biome id used as the transition source.</summary>
        public byte FromBiomeId;

        /// <summary>Biome id used as the transition target.</summary>
        public byte ToBiomeId;

        /// <summary>Fallback transition blend when AUP segment data is unavailable.</summary>
        public byte Blend255;

        /// <summary>Caller-owned bit flags carried through the result.</summary>
        public byte Flags;
    }

    /// <summary>
    /// Source fog parameters indexed by biome id.
    /// </summary>
    public struct BiomeTransitionFogSource
    {
        /// <summary>Linear fog color RGBA.</summary>
        public float4 FogColor;

        /// <summary>Fog density scalar or density scale, depending on caller integration.</summary>
        public float Density;

        /// <summary>Suspended matter turbidity multiplier.</summary>
        public float Turbidity;

        /// <summary>Medium absorption scalar.</summary>
        public float Absorption;

        /// <summary>Approximate clear-view attenuation distance in meters.</summary>
        public float FogAttenuationDistance;
    }

    /// <summary>
    /// Burst-computed fog blend result for one biome transition lane.
    /// </summary>
    public struct BiomeTransitionFogResult
    {
        /// <summary>Result sample with smoothed Blend255.</summary>
        public BiomeTransitionSample Sample;

        /// <summary>Blended fog color RGBA.</summary>
        public float4 FogColor;

        /// <summary>Blended fog density scalar.</summary>
        public float Density;

        /// <summary>Blended turbidity multiplier.</summary>
        public float Turbidity;

        /// <summary>Blended medium absorption scalar.</summary>
        public float Absorption;

        /// <summary>Blended clear-view attenuation distance in meters.</summary>
        public float FogAttenuationDistance;
    }

    /// <summary>
    /// Blends biome fog parameters using AUP-projected transition position with a packed Blend255 fallback.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct BiomeTransitionFogBlendJob : IJobParallelFor
    {
        /// <summary>Transition samples to evaluate.</summary>
        [ReadOnly] public NativeArray<BiomeTransitionSample> Samples;

        /// <summary>Fog sources addressed by biome id.</summary>
        [ReadOnly] public NativeArray<BiomeTransitionFogSource> FogSourcesByBiomeId;

        /// <summary>AUP transition source anchors.</summary>
        [ReadOnly] public NativeArray<AbsoluteUniversePositionBlit128> FromAup;

        /// <summary>AUP transition target anchors.</summary>
        [ReadOnly] public NativeArray<AbsoluteUniversePositionBlit128> ToAup;

        /// <summary>Current player AUP samples.</summary>
        [ReadOnly] public NativeArray<AbsoluteUniversePositionBlit128> PlayerAup;

        /// <summary>Job output lane.</summary>
        [WriteOnly] public NativeArray<BiomeTransitionFogResult> Results;

        /// <summary>World-space width of the transition band in meters.</summary>
        public float TransitionLengthMeters;

        /// <summary>
        /// Evaluates one biome transition fog lane.
        /// </summary>
        /// <param name="index">Transition lane index.</param>
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
                Absorption = math.lerp(from.Absorption, to.Absorption, smoothBlend),
                FogAttenuationDistance = math.max(
                    0.001f,
                    math.lerp(from.FogAttenuationDistance, to.FogAttenuationDistance, smoothBlend))
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
            double rawLengthSq = math.dot(segment, segment);
            if (rawLengthSq <= 1e-6d)
                return math.saturate(fallbackBlend);

            double lengthSq = math.max(1e-6d, rawLengthSq);
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

            return math.max(segmentBlend, math.saturate(fallbackBlend));
        }

        private static double3 ToAbsoluteDouble3(AbsoluteUniversePositionBlit128 position)
        {
            const double CellSizeMeters = AbsoluteUniversePosition.CellSizeMeters;
            return new double3(
                position.GridX * CellSizeMeters + position.Local.x,
                position.GridY * CellSizeMeters + position.Local.y,
                position.GridZ * CellSizeMeters + position.Local.z);
        }
    }
}
