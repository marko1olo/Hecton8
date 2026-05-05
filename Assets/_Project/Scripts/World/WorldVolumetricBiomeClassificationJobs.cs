using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    public struct VolumetricBiomeClassificationInput
    {
        public float DepthMeters;
        public int PrimaryBiomeMatrixDataIndex;
        public int PreferredFamilyDataIndex;
        public int SecondaryBiomeMatrixDataIndex;
        public byte Blend255;
        public byte Flags;
    }

    public struct VolumetricBiomeClassificationResult
    {
        public int PrimaryBiomeMatrixDataIndex;
        public int SecondaryBiomeMatrixDataIndex;
        public WorldProceduralFieldSampler.BiomeInfluenceCell InfluenceCell;
    }

    public struct VolumetricBiomeStressAuditResult
    {
        public int FailureMask;
        public int PrimaryBiomeId;
        public int ExpectedBiomeId;
        public byte Flags;
        public uint PackedCell;
    }

    public struct VolumetricBiomeStressBlockSummary
    {
        public int FailureCount;
        public uint PackedChecksum;
    }

    public struct VolumetricBiomeStressSummaryResult
    {
        public int FailureCount;
        public uint PackedChecksum;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct VolumetricBiomeStressInputBuildJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<VolumetricBiomeClassificationInput> Inputs;
        [WriteOnly] public NativeArray<int> ExpectedBiomeIds;
        [WriteOnly] public NativeArray<byte> ExpectedFlagMasks;

        public int ShallowBiomeId;
        public int TwilightBiomeId;
        public int HadalBiomeId;
        public int PreferredFamilyDataIndex;
        public byte VolumetricDepthFlag;

        public void Execute(int index)
        {
            int lane = index % 3;
            float depthMeters;
            int expectedBiomeId;
            byte expectedFlags;

            if (lane == 0)
            {
                depthMeters = 32f + (index % 127);
                expectedBiomeId = ShallowBiomeId;
                expectedFlags = 0;
            }
            else if (lane == 1)
            {
                depthMeters = 650f + (index % 900);
                expectedBiomeId = TwilightBiomeId;
                expectedFlags = VolumetricDepthFlag;
            }
            else
            {
                depthMeters = 2200f + (index % 1800);
                expectedBiomeId = HadalBiomeId;
                expectedFlags = VolumetricDepthFlag;
            }

            Inputs[index] = new VolumetricBiomeClassificationInput
            {
                DepthMeters = depthMeters,
                PrimaryBiomeMatrixDataIndex = 0,
                PreferredFamilyDataIndex = PreferredFamilyDataIndex,
                SecondaryBiomeMatrixDataIndex = -1,
                Blend255 = 0,
                Flags = 0
            };
            ExpectedBiomeIds[index] = expectedBiomeId;
            ExpectedFlagMasks[index] = expectedFlags;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct VolumetricBiomeClassificationJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<VolumetricBiomeClassificationInput> Inputs;
        [ReadOnly] public NativeArray<WorldProceduralFieldSampler.BiomeMatrixData> BiomeMatrices;
        [WriteOnly] public NativeArray<VolumetricBiomeClassificationResult> Results;

        public int BiomeMatrixCount;

        public void Execute(int index)
        {
            VolumetricBiomeClassificationInput input = Inputs[index];
            byte flags = input.Flags;
            int primaryIndex = ResolveDepthMatchedBiome(
                input.DepthMeters,
                input.PrimaryBiomeMatrixDataIndex,
                input.PreferredFamilyDataIndex);

            if (primaryIndex != input.PrimaryBiomeMatrixDataIndex)
                flags |= (byte)WorldProceduralFieldSampler.BiomeInfluenceFlags.VolumetricDepth;

            byte primaryBiomeId = ResolveMatrixId(primaryIndex, ref flags);
            byte secondaryBiomeId = ResolveMatrixId(input.SecondaryBiomeMatrixDataIndex, ref flags);
            byte blend255 = secondaryBiomeId != 0 && secondaryBiomeId != primaryBiomeId ? input.Blend255 : (byte)0;

            if (blend255 > 0)
                flags |= (byte)WorldProceduralFieldSampler.BiomeInfluenceFlags.TransitionEdge;

            Results[index] = new VolumetricBiomeClassificationResult
            {
                PrimaryBiomeMatrixDataIndex = primaryIndex,
                SecondaryBiomeMatrixDataIndex = blend255 > 0 ? input.SecondaryBiomeMatrixDataIndex : -1,
                InfluenceCell = WorldProceduralFieldSampler.BiomeInfluenceCell.Create(
                    primaryBiomeId,
                    blend255 > 0 ? secondaryBiomeId : (byte)0,
                    blend255,
                    flags)
            };
        }

        private int ResolveDepthMatchedBiome(float depthMeters, int currentIndex, int preferredFamilyIndex)
        {
            if (!BiomeMatrices.IsCreated || BiomeMatrixCount <= 0)
                return currentIndex;

            if (IsDepthMatch(depthMeters, currentIndex))
                return currentIndex;

            int bestIndex = -1;
            int bestScore = int.MinValue;
            for (int i = 0; i < BiomeMatrixCount; i++)
            {
                WorldProceduralFieldSampler.BiomeMatrixData candidate = BiomeMatrices[i];
                if (!IsDepthWithinBand(depthMeters, candidate.MinDepthMeters, candidate.MaxDepthMeters))
                    continue;

                int score = candidate.FamilyDataIndex == preferredFamilyIndex ? 1000 : 0;
                if (candidate.IsPlaceholder == 0)
                    score += 100;

                float bandSize = math.max(0.001f, candidate.MaxDepthMeters - candidate.MinDepthMeters);
                score += (int)math.round(50f / math.min(50f, math.abs(bandSize)));

                if (score <= bestScore)
                    continue;

                bestIndex = i;
                bestScore = score;
            }

            return bestIndex >= 0 ? bestIndex : currentIndex;
        }

        private bool IsDepthMatch(float depthMeters, int matrixDataIndex)
        {
            if (matrixDataIndex < 0 || matrixDataIndex >= BiomeMatrixCount || !BiomeMatrices.IsCreated)
                return false;

            WorldProceduralFieldSampler.BiomeMatrixData data = BiomeMatrices[matrixDataIndex];
            return IsDepthWithinBand(depthMeters, data.MinDepthMeters, data.MaxDepthMeters);
        }

        private static bool IsDepthWithinBand(float depthMeters, float minDepthMeters, float maxDepthMeters)
        {
            float minDepth = math.min(minDepthMeters, maxDepthMeters);
            float maxDepth = math.max(minDepthMeters, maxDepthMeters);
            if (maxDepth <= 0f && minDepth <= 0f)
                return true;

            return depthMeters >= minDepth && depthMeters <= maxDepth;
        }

        private byte ResolveMatrixId(int matrixDataIndex, ref byte flags)
        {
            if (!BiomeMatrices.IsCreated || matrixDataIndex < 0 || matrixDataIndex >= BiomeMatrixCount)
                return 0;

            WorldProceduralFieldSampler.BiomeMatrixData data = BiomeMatrices[matrixDataIndex];
            if (data.IsPlaceholder != 0)
                flags |= (byte)WorldProceduralFieldSampler.BiomeInfluenceFlags.Placeholder;

            return data.MatrixIndex > 0 && data.MatrixIndex <= 255 ? (byte)data.MatrixIndex : (byte)0;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct VolumetricBiomeStressAuditJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<VolumetricBiomeClassificationResult> Results;
        [ReadOnly] public NativeArray<int> ExpectedBiomeIds;
        [ReadOnly] public NativeArray<byte> ExpectedFlagMasks;
        [WriteOnly] public NativeArray<VolumetricBiomeStressAuditResult> AuditResults;

        public void Execute(int index)
        {
            VolumetricBiomeClassificationResult result = Results[index];
            WorldProceduralFieldSampler.BiomeInfluenceCell cell = result.InfluenceCell;
            int expectedBiomeId = ExpectedBiomeIds[index];
            byte expectedFlags = ExpectedFlagMasks[index];
            int failureMask = 0;

            if (cell.PrimaryBiomeId != expectedBiomeId)
                failureMask |= 1;

            if ((cell.Flags & expectedFlags) != expectedFlags)
                failureMask |= 2;

            uint expectedPack = (uint)(
                cell.PrimaryBiomeId |
                (cell.SecondaryBiomeId << 8) |
                (cell.Blend255 << 16) |
                (cell.Flags << 24));

            if (cell.Packed != expectedPack)
                failureMask |= 4;

            AuditResults[index] = new VolumetricBiomeStressAuditResult
            {
                FailureMask = failureMask,
                PrimaryBiomeId = cell.PrimaryBiomeId,
                ExpectedBiomeId = expectedBiomeId,
                Flags = cell.Flags,
                PackedCell = cell.Packed
            };
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct VolumetricBiomeStressBlockReduceJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<VolumetricBiomeStressAuditResult> AuditResults;
        [WriteOnly] public NativeArray<VolumetricBiomeStressBlockSummary> BlockSummaries;

        public int SampleCount;
        public int SamplesPerBlock;

        public void Execute(int blockIndex)
        {
            int start = blockIndex * SamplesPerBlock;
            if ((uint)start >= (uint)SampleCount)
            {
                BlockSummaries[blockIndex] = default;
                return;
            }

            int end = math.min(start + SamplesPerBlock, SampleCount);
            int failureCount = 0;
            uint checksum = 2166136261u ^ (uint)blockIndex;

            for (int i = start; i < end; i++)
            {
                VolumetricBiomeStressAuditResult audit = AuditResults[i];
                if (audit.FailureMask != 0)
                    failureCount++;

                checksum = (checksum ^ audit.PackedCell) * 16777619u;
            }

            BlockSummaries[blockIndex] = new VolumetricBiomeStressBlockSummary
            {
                FailureCount = failureCount,
                PackedChecksum = checksum
            };
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct VolumetricBiomeStressFinalReduceJob : IJob
    {
        [ReadOnly] public NativeArray<VolumetricBiomeStressBlockSummary> BlockSummaries;
        [WriteOnly] public NativeArray<VolumetricBiomeStressSummaryResult> Summary;

        public int BlockCount;

        public void Execute()
        {
            int failureCount = 0;
            uint checksum = 2166136261u;

            for (int i = 0; i < BlockCount; i++)
            {
                VolumetricBiomeStressBlockSummary block = BlockSummaries[i];
                failureCount += block.FailureCount;
                checksum = (checksum ^ block.PackedChecksum) * 16777619u;
            }

            Summary[0] = new VolumetricBiomeStressSummaryResult
            {
                FailureCount = failureCount,
                PackedChecksum = checksum
            };
        }
    }
}
