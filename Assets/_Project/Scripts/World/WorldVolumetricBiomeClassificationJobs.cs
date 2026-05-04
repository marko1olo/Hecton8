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
}
