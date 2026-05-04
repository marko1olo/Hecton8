using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct WorldProceduralTerrainFakeOverhangOffsetJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> Heights01;
        [WriteOnly] public NativeArray<float2> HorizontalOffsetsMeters;

        public int Width;
        public int Height;
        public float CellSizeMeters;
        public float HeightScaleMeters;
        public float SlopeThresholdDegrees;
        public float MaxOffsetMeters;
        public float NoiseFrequency;
        public uint Seed;

        public void Execute(int index)
        {
            if (Width <= 2 || Height <= 2 || !Heights01.IsCreated || !HorizontalOffsetsMeters.IsCreated)
            {
                HorizontalOffsetsMeters[index] = float2.zero;
                return;
            }

            int x = index % Width;
            int z = index / Width;
            if (x <= 0 || z <= 0 || x >= Width - 1 || z >= Height - 1)
            {
                HorizontalOffsetsMeters[index] = float2.zero;
                return;
            }

            float hWest = Heights01[index - 1] * HeightScaleMeters;
            float hEast = Heights01[index + 1] * HeightScaleMeters;
            float hSouth = Heights01[index - Width] * HeightScaleMeters;
            float hNorth = Heights01[index + Width] * HeightScaleMeters;
            float safeCellSize = math.max(0.001f, CellSizeMeters);
            float dx = (hEast - hWest) / (safeCellSize * 2f);
            float dz = (hNorth - hSouth) / (safeCellSize * 2f);
            float2 gradient = new float2(dx, dz);
            float gradientLength = math.length(gradient);
            float slopeDegrees = math.degrees(math.atan(gradientLength));
            float cliff01 = math.saturate((slopeDegrees - SlopeThresholdDegrees) / math.max(1f, 89f - SlopeThresholdDegrees));
            if (cliff01 <= 0.0001f)
            {
                HorizontalOffsetsMeters[index] = float2.zero;
                return;
            }

            float2 pushDirection = math.normalizesafe(-gradient, float2.zero);
            if (math.lengthsq(pushDirection) <= 0.0001f)
            {
                HorizontalOffsetsMeters[index] = float2.zero;
                return;
            }

            float2 worldXZ = new float2(x, z) * safeCellSize;
            float noise01 = math.saturate((noise.snoise(worldXZ * math.max(0.0001f, NoiseFrequency) + (float)Seed * 0.00137f) * 0.5f) + 0.5f);
            float offset = math.max(0f, MaxOffsetMeters) * cliff01 * math.lerp(0.35f, 1f, noise01);
            HorizontalOffsetsMeters[index] = pushDirection * offset;
        }
    }
}
