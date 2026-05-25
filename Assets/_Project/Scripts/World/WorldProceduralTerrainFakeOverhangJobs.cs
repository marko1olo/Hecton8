using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct WorldProceduralTerrainFakeOverhangOffsetJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float> Heights01;
        [WriteOnly, NoAlias] public NativeArray<float2> HorizontalOffsetsMeters;

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
            if (!HorizontalOffsetsMeters.IsCreated)
                return;

            if (Width <= 2 || Height <= 2 || !Heights01.IsCreated)
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
            float invDoubleCellSize = math.rcp(safeCellSize * 2f);
            float dx = (hEast - hWest) * invDoubleCellSize;
            float dz = (hNorth - hSouth) * invDoubleCellSize;
            float2 gradient = new float2(dx, dz);
            float gradientLength = FastMagnitudeApprox(gradient);
            float slopeDegrees = math.degrees(global::Hecton8.Core.MathLodApproximation.ApproxAtanFast(gradientLength));
            float cliff01 = math.saturate((slopeDegrees - SlopeThresholdDegrees) * math.rcp(math.max(1f, 89f - SlopeThresholdDegrees)));
            if (cliff01 <= 0.0001f)
            {
                HorizontalOffsetsMeters[index] = float2.zero;
                return;
            }

            if (gradientLength <= 0.0001f)
            {
                HorizontalOffsetsMeters[index] = float2.zero;
                return;
            }

            float2 pushDirection = -gradient * math.rcp(math.max(gradientLength, 0.0001f));
            float2 worldXZ = new float2(x, z) * safeCellSize;
            float noise01 = math.saturate((noise.snoise(worldXZ * math.max(0.0001f, NoiseFrequency) + (float)Seed * 0.00137f) * 0.5f) + 0.5f);
            float offset = math.max(0f, MaxOffsetMeters) * cliff01 * math.lerp(0.35f, 1f, noise01);
            HorizontalOffsetsMeters[index] = pushDirection * offset;
        }

        private static float FastMagnitudeApprox(float2 value)
        {
            float2 abs = math.abs(value);
            float max = math.max(abs.x, abs.y);
            float min = math.min(abs.x, abs.y);
            return max + (min * 0.41421356f);
        }
    }
}
