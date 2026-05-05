using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Block-level erosion result metrics for cold-path verification and editor smoke tests.
    /// </summary>
    public struct HydraulicErosionMetricBlock
    {
        /// <summary>Minimum finite height in the block.</summary>
        public float MinHeight;

        /// <summary>Maximum finite height in the block.</summary>
        public float MaxHeight;

        /// <summary>Finite height sum in the block.</summary>
        public float SumHeight;

        /// <summary>Finite sediment mask sum in the block.</summary>
        public float SumSediment;

        /// <summary>Finite wear mask sum in the block.</summary>
        public float SumWear;

        /// <summary>Maximum finite sediment value in the block.</summary>
        public float MaxSediment;

        /// <summary>Maximum finite wear value in the block.</summary>
        public float MaxWear;

        /// <summary>Number of sampled cells with non-finite height, sediment, or wear.</summary>
        public int NanCount;

        /// <summary>Number of finite sampled cells in the block.</summary>
        public int SampleCount;
    }

    /// <summary>
    /// Burst scan over erosion buffers. It replaces editor cold-path scalar metric loops with block-parallel work.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HydraulicErosionMetricsJob : IJobParallelFor
    {
        /// <summary>Eroded height buffer to scan.</summary>
        [ReadOnly] public NativeArray<float> Heightmap;

        /// <summary>Sediment mask buffer to scan.</summary>
        [ReadOnly] public NativeArray<float> SedimentMask;

        /// <summary>Wear mask buffer to scan.</summary>
        [ReadOnly] public NativeArray<float> WearMask;

        /// <summary>Block metric output buffer.</summary>
        [WriteOnly] public NativeArray<HydraulicErosionMetricBlock> Blocks;

        /// <summary>Total number of valid samples in the scanned buffers.</summary>
        public int SampleCount;

        /// <summary>Number of samples scanned per block.</summary>
        public int BlockSize;

        /// <inheritdoc />
        public void Execute(int blockIndex)
        {
            int safeBlockSize = math.max(1, BlockSize);
            int start = blockIndex * safeBlockSize;
            int end = math.min(math.max(0, SampleCount), start + safeBlockSize);

            var block = new HydraulicErosionMetricBlock
            {
                MinHeight = 1f,
                MaxHeight = 0f
            };

            for (int i = start; i < end; i++)
            {
                float height = Heightmap[i];
                float sediment = SedimentMask[i];
                float wear = WearMask[i];
                bool invalid =
                    !math.isfinite(height) ||
                    !math.isfinite(sediment) ||
                    !math.isfinite(wear);

                if (invalid)
                {
                    block.NanCount++;
                    continue;
                }

                block.MinHeight = math.min(block.MinHeight, height);
                block.MaxHeight = math.max(block.MaxHeight, height);
                block.SumHeight += height;
                block.SumSediment += sediment;
                block.SumWear += wear;
                block.MaxSediment = math.max(block.MaxSediment, sediment);
                block.MaxWear = math.max(block.MaxWear, wear);
                block.SampleCount++;
            }

            Blocks[blockIndex] = block;
        }
    }
}
