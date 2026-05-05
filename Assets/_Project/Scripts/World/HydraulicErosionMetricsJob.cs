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

        /// <summary>Maximum finite height delta in the audited boundary band.</summary>
        public float MaxBoundaryHeightDelta;

        /// <summary>Maximum finite sediment value in the audited boundary band.</summary>
        public float MaxBoundarySediment;

        /// <summary>Maximum finite wear value in the audited boundary band.</summary>
        public float MaxBoundaryWear;

        /// <summary>Number of sampled cells with non-finite height, sediment, or wear.</summary>
        public int NanCount;

        /// <summary>Number of finite sampled cells in the block.</summary>
        public int SampleCount;

        /// <summary>Number of finite sampled cells in the audited boundary band.</summary>
        public int BoundarySampleCount;

        /// <summary>Number of boundary-band cells with non-finite height, sediment, wear, or neighbor data.</summary>
        public int BoundaryNanCount;
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

        /// <summary>Heightmap width in cells.</summary>
        public int Width;

        /// <summary>Heightmap height in cells.</summary>
        public int Height;

        /// <summary>Boundary band width to audit in cells.</summary>
        public int BoundaryMargin;

        /// <inheritdoc />
        public void Execute(int blockIndex)
        {
            int safeBlockSize = math.max(1, BlockSize);
            int start = blockIndex * safeBlockSize;
            int end = math.min(math.max(0, SampleCount), start + safeBlockSize);
            int safeWidth = math.max(1, Width);
            int safeHeight = math.max(1, Height);
            int safeMargin = math.clamp(BoundaryMargin, 0, math.max(0, math.min(safeWidth, safeHeight) / 2));

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
                int x = i % safeWidth;
                int z = i / safeWidth;
                bool isBoundary =
                    safeMargin > 0 &&
                    z < safeHeight &&
                    (x < safeMargin ||
                     z < safeMargin ||
                     x >= safeWidth - safeMargin ||
                     z >= safeHeight - safeMargin);

                if (invalid)
                {
                    block.NanCount++;
                    if (isBoundary)
                        block.BoundaryNanCount++;

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

                if (isBoundary)
                {
                    block.BoundarySampleCount++;
                    block.MaxBoundarySediment = math.max(block.MaxBoundarySediment, sediment);
                    block.MaxBoundaryWear = math.max(block.MaxBoundaryWear, wear);

                    float boundaryDelta = MaxFiniteNeighborDelta(i, x, z, height, safeWidth, safeHeight);
                    if (boundaryDelta < 0f)
                        block.BoundaryNanCount++;
                    else
                        block.MaxBoundaryHeightDelta = math.max(block.MaxBoundaryHeightDelta, boundaryDelta);
                }
            }

            Blocks[blockIndex] = block;
        }

        private float MaxFiniteNeighborDelta(int index, int x, int z, float height, int width, int heightCount)
        {
            float maxDelta = 0f;
            bool invalidNeighbor = false;

            if (x > 0)
                AccumulateNeighborDelta(index - 1, height, ref maxDelta, ref invalidNeighbor);

            if (x + 1 < width)
                AccumulateNeighborDelta(index + 1, height, ref maxDelta, ref invalidNeighbor);

            if (z > 0)
                AccumulateNeighborDelta(index - width, height, ref maxDelta, ref invalidNeighbor);

            if (z + 1 < heightCount)
                AccumulateNeighborDelta(index + width, height, ref maxDelta, ref invalidNeighbor);

            return invalidNeighbor ? -1f : maxDelta;
        }

        private void AccumulateNeighborDelta(int neighborIndex, float height, ref float maxDelta, ref bool invalidNeighbor)
        {
            float neighborHeight = Heightmap[neighborIndex];
            if (!math.isfinite(neighborHeight))
            {
                invalidNeighbor = true;
                return;
            }

            maxDelta = math.max(maxDelta, math.abs(height - neighborHeight));
        }
    }

    /// <summary>
    /// Burst reduction over metric blocks. Keeps smoke-test aggregation off the managed main-thread loop.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HydraulicErosionMetricReductionJob : IJob
    {
        /// <summary>Metric blocks produced by <see cref="HydraulicErosionMetricsJob"/>.</summary>
        [ReadOnly] public NativeArray<HydraulicErosionMetricBlock> Blocks;

        /// <summary>Single-element summary output.</summary>
        [WriteOnly] public NativeArray<HydraulicErosionMetricBlock> Summary;

        /// <summary>Number of valid metric blocks to reduce.</summary>
        public int BlockCount;

        /// <inheritdoc />
        public void Execute()
        {
            var summary = new HydraulicErosionMetricBlock
            {
                MinHeight = 1f,
                MaxHeight = 0f
            };

            int count = math.min(math.max(0, BlockCount), Blocks.Length);
            for (int i = 0; i < count; i++)
            {
                HydraulicErosionMetricBlock block = Blocks[i];
                if (block.SampleCount > 0)
                {
                    summary.MinHeight = math.min(summary.MinHeight, block.MinHeight);
                    summary.MaxHeight = math.max(summary.MaxHeight, block.MaxHeight);
                    summary.SumHeight += block.SumHeight;
                    summary.SumSediment += block.SumSediment;
                    summary.SumWear += block.SumWear;
                    summary.MaxSediment = math.max(summary.MaxSediment, block.MaxSediment);
                    summary.MaxWear = math.max(summary.MaxWear, block.MaxWear);
                    summary.MaxBoundaryHeightDelta = math.max(summary.MaxBoundaryHeightDelta, block.MaxBoundaryHeightDelta);
                    summary.MaxBoundarySediment = math.max(summary.MaxBoundarySediment, block.MaxBoundarySediment);
                    summary.MaxBoundaryWear = math.max(summary.MaxBoundaryWear, block.MaxBoundaryWear);
                    summary.SampleCount += block.SampleCount;
                    summary.BoundarySampleCount += block.BoundarySampleCount;
                }

                summary.NanCount += block.NanCount;
                summary.BoundaryNanCount += block.BoundaryNanCount;
            }

            if (Summary.Length > 0)
                Summary[0] = summary;
        }
    }
}
