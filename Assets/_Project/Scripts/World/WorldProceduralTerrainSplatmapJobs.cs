using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct WorldProceduralTerrainSlopeCavitySplatmapJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> Heights01;
        [ReadOnly] public NativeArray<float> Sediment01;
        [WriteOnly] public NativeArray<float4> Weights;
        [WriteOnly] public NativeArray<float> SlopeWeights01;

        public int Width;
        public int Height;
        public float CellSizeMeters;
        public float HeightScaleMeters;
        public float RockSlopeThresholdDegrees;
        public float SlopeBlendWidthDegrees;
        public float CavityStrength;
        public float SedimentStrength;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Weights.Length)
                return;

            int safeWidth = math.max(1, Width);
            int safeHeight = math.max(1, Height);
            int x = index % safeWidth;
            int z = index / safeWidth;
            int maxX = safeWidth - 1;
            int maxZ = safeHeight - 1;

            float center = ReadHeight(math.clamp(x, 0, maxX), math.clamp(z, 0, maxZ), safeWidth);
            float west = ReadHeight(math.max(0, x - 1), z, safeWidth);
            float east = ReadHeight(math.min(maxX, x + 1), z, safeWidth);
            float south = ReadHeight(x, math.max(0, z - 1), safeWidth);
            float north = ReadHeight(x, math.min(maxZ, z + 1), safeWidth);

            float safeCellSize = math.max(0.001f, CellSizeMeters);
            float safeHeightScale = math.max(0.001f, HeightScaleMeters);
            float dx = (east - west) * safeHeightScale / (safeCellSize * 2f);
            float dz = (north - south) * safeHeightScale / (safeCellSize * 2f);
            float slopeDegrees = math.degrees(math.atan(math.sqrt(dx * dx + dz * dz)));

            float halfBlend = math.max(0.001f, SlopeBlendWidthDegrees);
            float rock = math.smoothstep(
                RockSlopeThresholdDegrees - halfBlend,
                RockSlopeThresholdDegrees + halfBlend,
                slopeDegrees);
            float slopeWeight = math.smoothstep(15f, math.max(15.001f, RockSlopeThresholdDegrees), slopeDegrees);

            float neighborAverage = (west + east + south + north) * 0.25f;
            float cavity = math.saturate((neighborAverage - center) * safeHeightScale * math.max(0f, CavityStrength));
            float sediment = math.saturate(ReadSediment(index) * math.max(0f, SedimentStrength));
            float channelBottom = math.smoothstep(0.02f, 0.22f, cavity);
            float silt = math.saturate(sediment * channelBottom * (1f - rock));
            float sand = math.saturate((1f - rock) * (1f - silt));

            float total = math.max(0.0001f, sand + rock + silt);
            Weights[index] = new float4(sand / total, rock / total, silt / total, cavity);

            if (SlopeWeights01.IsCreated && (uint)index < (uint)SlopeWeights01.Length)
                SlopeWeights01[index] = slopeWeight;
        }

        private float ReadHeight(int x, int z, int width)
        {
            int index = z * width + x;
            if ((uint)index >= (uint)Heights01.Length)
                return 0f;

            return math.saturate(Heights01[index]);
        }

        private float ReadSediment(int index)
        {
            if ((uint)index >= (uint)Sediment01.Length)
                return 0f;

            return math.saturate(Sediment01[index]);
        }
    }
}
