using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Burst terrain seed job used by the editor erosion harness.
    /// </summary>
    [BurstCompile]
    public struct ErosionFractalHeightmapJob : IJobParallelFor
    {
        /// <summary>Unmodified source height output.</summary>
        [WriteOnly]
        public NativeArray<float> Before;

        /// <summary>Mutable erosion input height output.</summary>
        [WriteOnly]
        public NativeArray<float> Height;

        /// <summary>Square heightmap resolution.</summary>
        public int Resolution;

        /// <summary>Primary deterministic noise seed.</summary>
        public uint PrimarySeed;

        /// <summary>Ridge deterministic noise seed.</summary>
        public uint RidgeSeed;

        /// <inheritdoc />
        public void Execute(int index)
        {
            int safeResolution = math.max(1, Resolution);
            int x = index % safeResolution;
            int z = index / safeResolution;
            float2 uv = new float2(x, z) * (1f / safeResolution);
            float n = FractalValueNoise(uv * 7.5f, PrimarySeed);
            float ridge = 1f - math.abs(FractalValueNoise(uv * 3.25f + new float2(19.3f, -7.1f), RidgeSeed) * 2f - 1f);
            float h = math.saturate(math.smoothstep(0.2f, 0.95f, n) * 0.72f + math.pow(ridge, 3.2f) * 0.28f);
            Before[index] = h;
            Height[index] = h;
        }

        private static float FractalValueNoise(float2 sample, uint seed)
        {
            float amplitude = 0.5f;
            float frequency = 1f;
            float total = 0f;
            float normalization = 0f;

            for (int octave = 0; octave < 6; octave++)
            {
                total += ValueNoise(sample * frequency, seed + (uint)octave * 0x85EBCA6Bu) * amplitude;
                normalization += amplitude;
                amplitude *= 0.52f;
                frequency *= 2.03f;
            }

            return total / math.max(0.0001f, normalization);
        }

        private static float ValueNoise(float2 sample, uint seed)
        {
            float2 floorSample = math.floor(sample);
            int2 cell = (int2)floorSample;
            float2 local = sample - floorSample;
            float2 smooth = local * local * (3f - 2f * local);

            float a = Hash01(cell.x, cell.y, seed);
            float b = Hash01(cell.x + 1, cell.y, seed);
            float c = Hash01(cell.x, cell.y + 1, seed);
            float d = Hash01(cell.x + 1, cell.y + 1, seed);

            return math.lerp(
                math.lerp(a, b, smooth.x),
                math.lerp(c, d, smooth.x),
                smooth.y);
        }

        private static float Hash01(int x, int y, uint seed)
        {
            uint hash = (uint)x * 0x8DA6B343u;
            hash ^= (uint)y * 0xD8163841u;
            hash ^= seed + 0x9E3779B9u + (hash << 6) + (hash >> 2);
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) * (1f / 16777215f);
        }
    }

    /// <summary>
    /// Blittable erosion smoke-test metrics.
    /// </summary>
    public struct ErosionSmokeMetrics
    {
        /// <summary>Minimum source height.</summary>
        public float MinBefore;

        /// <summary>Maximum source height.</summary>
        public float MaxBefore;

        /// <summary>Minimum final height.</summary>
        public float MinAfter;

        /// <summary>Maximum final height.</summary>
        public float MaxAfter;

        /// <summary>Maximum normalized sediment accumulator before PNG normalization.</summary>
        public float MaxSediment;

        /// <summary>Maximum normalized wear accumulator before PNG normalization.</summary>
        public float MaxWear;

        /// <summary>Mean absolute height delta.</summary>
        public float MeanAbsoluteDelta;

        /// <summary>Cells with measurable height change.</summary>
        public int ChangedCellCount;

        /// <summary>Cells containing non-finite values in any sampled field.</summary>
        public int NonFiniteCellCount;
    }

    /// <summary>
    /// Burst reduction job for editor erosion smoke-test metrics.
    /// </summary>
    [BurstCompile]
    public struct ErosionSmokeMetricsJob : IJob
    {
        /// <summary>Original heightmap.</summary>
        [ReadOnly] public NativeArray<float> Before;

        /// <summary>Eroded heightmap.</summary>
        [ReadOnly] public NativeArray<float> After;

        /// <summary>Sediment mask accumulator.</summary>
        [ReadOnly] public NativeArray<float> Sediment;

        /// <summary>Wear mask accumulator.</summary>
        [ReadOnly] public NativeArray<float> Wear;

        /// <summary>Single output metrics slot.</summary>
        [WriteOnly]
        public NativeArray<ErosionSmokeMetrics> Metrics;

        /// <inheritdoc />
        public void Execute()
        {
            int count = math.min(math.min(Before.Length, After.Length), math.min(Sediment.Length, Wear.Length));
            ErosionSmokeMetrics metrics = default;
            metrics.MinBefore = float.MaxValue;
            metrics.MinAfter = float.MaxValue;
            metrics.MaxBefore = float.MinValue;
            metrics.MaxAfter = float.MinValue;

            float absoluteDeltaSum = 0f;
            for (int i = 0; i < count; i++)
            {
                float before = Before[i];
                float after = After[i];
                float sediment = Sediment[i];
                float wear = Wear[i];

                if (!math.isfinite(before) || !math.isfinite(after) || !math.isfinite(sediment) || !math.isfinite(wear))
                {
                    metrics.NonFiniteCellCount++;
                    continue;
                }

                metrics.MinBefore = math.min(metrics.MinBefore, before);
                metrics.MaxBefore = math.max(metrics.MaxBefore, before);
                metrics.MinAfter = math.min(metrics.MinAfter, after);
                metrics.MaxAfter = math.max(metrics.MaxAfter, after);
                metrics.MaxSediment = math.max(metrics.MaxSediment, sediment);
                metrics.MaxWear = math.max(metrics.MaxWear, wear);

                float delta = math.abs(after - before);
                absoluteDeltaSum += delta;
                if (delta > 0.00001f)
                    metrics.ChangedCellCount++;
            }

            float validCount = math.max(1, count - metrics.NonFiniteCellCount);
            metrics.MeanAbsoluteDelta = absoluteDeltaSum / validCount;
            if (count == metrics.NonFiniteCellCount)
            {
                metrics.MinBefore = 0f;
                metrics.MaxBefore = 0f;
                metrics.MinAfter = 0f;
                metrics.MaxAfter = 0f;
            }

            Metrics[0] = metrics;
        }
    }
}
