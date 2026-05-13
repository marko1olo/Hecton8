using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World.Terrain
{
    public struct HybridTerrainSeamPlanNative
    {
        public float3 RuntimeContactPosition;
        public float3 RuntimeVoxelCenter;
        public float3 VoxelSize;
        public float SeamBlendRadius;
        public float TerrainBlendWeight;
        public float CaveBlendWeight;
        public float SuggestedTerrainRaise;
        public float SuggestedTerrainCut;
        public float TerrainDelta;
        public float RidgeSignal;
        public float CanyonSignal;
        public float CompositionPotential;
    }

    public static class HybridTerrainSeamMath
    {
        public const float CloseHeightBandMeters = 5f;
        public const float RaymarchHalfSpanMeters = 5f;
        public const int RaymarchStepCount = 16;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SmoothMinNoTranscendental(float a, float b, float k)
        {
            float safeK = math.max(k, 0.0001f);
            float h = math.saturate(0.5f + 0.5f * (b - a) * math.rcp(safeK));
            return math.lerp(b, a, h) - safeK * h * (1f - h);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LengthFromSq(float lengthSq)
        {
            return lengthSq * math.rsqrt(math.max(lengthSq, 0.0000001f));
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HybridSdfHeightmapProjectionJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> BaselineHeights01;
        [ReadOnly] public NativeArray<ushort> QuantizedHeightSamples;
        [ReadOnly] public NativeArray<HybridTerrainSeamPlanNative> Plans;

        public NativeArray<float> PatchHeights01;
        public NativeArray<byte> BlendMask;

        public int HeightmapResolution;
        public int PatchX;
        public int PatchZ;
        public int PatchWidth;
        public int PatchHeight;
        public float HeightmapInvMaxIndex;
        public float3 TerrainPosition;
        public float3 TerrainSize;
        public byte LowTierVisualOnly;

        public void Execute(int index)
        {
            if (PatchWidth <= 0 || PatchHeight <= 0 || HeightmapResolution <= 1)
                return;

            int localX = index % PatchWidth;
            int localZ = index / PatchWidth;
            int heightX = PatchX + localX;
            int heightZ = PatchZ + localZ;
            int terrainIndex = heightX + heightZ * HeightmapResolution;
            float height01 = ResolveHeight01(terrainIndex);
            float worldX = TerrainPosition.x + heightX * HeightmapInvMaxIndex * TerrainSize.x;
            float worldZ = TerrainPosition.z + heightZ * HeightmapInvMaxIndex * TerrainSize.z;
            float worldY = TerrainPosition.y + height01 * TerrainSize.y;
            float mask01 = 0f;

            for (int i = 0; i < Plans.Length; i++)
            {
                HybridTerrainSeamPlanNative plan = Plans[i];
                float2 delta = new float2(worldX - plan.RuntimeContactPosition.x, worldZ - plan.RuntimeContactPosition.z);
                float effectiveRadius = math.max(2f, plan.SeamBlendRadius + 2f);
                float distanceSq = math.lengthsq(delta);
                float effectiveRadiusSq = effectiveRadius * effectiveRadius;
                if (distanceSq > effectiveRadiusSq)
                    continue;

                float distance = HybridTerrainSeamMath.LengthFromSq(distanceSq);
                float radial = 1f - math.saturate(distance * math.rcp(effectiveRadius));
                float falloff = HybridTerrainSeamMath.SmoothStep01(radial);
                float planWeight = math.saturate(
                    plan.TerrainBlendWeight * 0.45f +
                    plan.RidgeSignal * 0.18f +
                    plan.CanyonSignal * 0.12f +
                    plan.CompositionPotential * 0.15f +
                    plan.CaveBlendWeight * 0.10f);
                float blendWeight = falloff * math.max(planWeight, math.saturate(plan.CaveBlendWeight));
                mask01 = math.max(mask01, blendWeight);

                if (LowTierVisualOnly != 0)
                    continue;

                float voxelSurfaceY = RaymarchDownToVoxelSurface(worldX, worldY, worldZ, plan);
                float heightDelta = math.abs(voxelSurfaceY - worldY);
                if (heightDelta > HybridTerrainSeamMath.CloseHeightBandMeters)
                    continue;

                float smoothTarget = HybridTerrainSeamMath.SmoothMinNoTranscendental(
                    worldY,
                    voxelSurfaceY,
                    HybridTerrainSeamMath.CloseHeightBandMeters);
                float raise = math.max(0f, plan.TerrainDelta) + plan.SuggestedTerrainRaise;
                float cut = math.max(0f, -plan.TerrainDelta) + plan.SuggestedTerrainCut * math.saturate(plan.CaveBlendWeight + 0.25f);
                float biasedTarget = smoothTarget + (raise - cut) * 0.12f * falloff;
                worldY = math.lerp(worldY, biasedTarget, blendWeight);
            }

            PatchHeights01[index] = math.saturate((worldY - TerrainPosition.y) * math.rcp(math.max(TerrainSize.y, 0.0001f)));
            BlendMask[index] = (byte)math.round(math.saturate(mask01) * 255f);
        }

        private float ResolveHeight01(int terrainIndex)
        {
            if (QuantizedHeightSamples.IsCreated && terrainIndex >= 0 && terrainIndex < QuantizedHeightSamples.Length)
                return QuantizedHeightSamples[terrainIndex] * (1f / 65535f);

            if (BaselineHeights01.IsCreated && terrainIndex >= 0 && terrainIndex < BaselineHeights01.Length)
                return BaselineHeights01[terrainIndex];

            return 0f;
        }

        private static float RaymarchDownToVoxelSurface(
            float worldX,
            float terrainY,
            float worldZ,
            in HybridTerrainSeamPlanNative plan)
        {
            float step = (HybridTerrainSeamMath.RaymarchHalfSpanMeters * 2f) * math.rcp(HybridTerrainSeamMath.RaymarchStepCount);
            float y = terrainY + HybridTerrainSeamMath.RaymarchHalfSpanMeters;
            float previousY = y;
            float previousSdf = SampleAnalyticSdf(new float3(worldX, y, worldZ), plan);

            for (int i = 1; i <= HybridTerrainSeamMath.RaymarchStepCount; i++)
            {
                y -= step;
                float sdf = SampleAnalyticSdf(new float3(worldX, y, worldZ), plan);
                if ((previousSdf >= 0f && sdf <= 0f) || (previousSdf <= 0f && sdf >= 0f))
                {
                    float denominator = previousSdf - sdf;
                    float safeDenominator = math.select(
                        math.select(-0.0001f, 0.0001f, denominator >= 0f),
                        denominator,
                        math.abs(denominator) > 0.0001f);
                    float t = math.saturate(previousSdf * math.rcp(safeDenominator));
                    return math.lerp(previousY, y, t);
                }

                previousY = y;
                previousSdf = sdf;
            }

            float contactBias = math.max(plan.SuggestedTerrainCut, plan.SeamBlendRadius * 0.08f);
            return plan.RuntimeContactPosition.y - contactBias;
        }

        private static float SampleAnalyticSdf(float3 position, in HybridTerrainSeamPlanNative plan)
        {
            float3 halfSize = math.max(plan.VoxelSize * 0.5f, new float3(0.25f, 0.25f, 0.25f));
            float3 normalized = (position - plan.RuntimeVoxelCenter) * math.rcp(halfSize);
            float dominantScale = math.cmin(halfSize);
            return (HybridTerrainSeamMath.LengthFromSq(math.lengthsq(normalized)) - 1f) * dominantScale;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HybridTerrainSeamNormalJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> PatchHeights01;
        public NativeArray<float3> Normals;

        public int PatchWidth;
        public int PatchHeight;
        public float CellSizeX;
        public float CellSizeZ;
        public float HeightScale;

        public void Execute(int index)
        {
            if (PatchWidth <= 0 || PatchHeight <= 0)
                return;

            int x = index % PatchWidth;
            int z = index / PatchWidth;
            float hL = SampleHeight(math.max(0, x - 1), z);
            float hR = SampleHeight(math.min(PatchWidth - 1, x + 1), z);
            float hD = SampleHeight(x, math.max(0, z - 1));
            float hU = SampleHeight(x, math.min(PatchHeight - 1, z + 1));
            float3 tangentX = new float3(math.max(CellSizeX * 2f, 0.0001f), (hR - hL) * HeightScale, 0f);
            float3 tangentZ = new float3(0f, (hU - hD) * HeightScale, math.max(CellSizeZ * 2f, 0.0001f));
            float3 cross = math.cross(tangentZ, tangentX);
            float invLength = math.rsqrt(math.max(math.lengthsq(cross), 0.0000001f));
            float3 normal = cross * invLength;
            Normals[index] = math.select(new float3(0f, 1f, 0f), normal, math.all(math.isfinite(normal)));
        }

        private float SampleHeight(int x, int z)
        {
            return PatchHeights01[x + z * PatchWidth];
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HybridTerrainSeamMaskDetailJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Normals;
        public NativeArray<byte> BlendMask;

        public byte EnableDetail;

        public void Execute(int index)
        {
            if (EnableDetail == 0)
                return;

            float3 normal = Normals[index];
            if (!math.all(math.isfinite(normal)))
                return;

            float slope01 = math.saturate(1f - math.saturate(normal.y));
            int slopeBoost = (int)math.round(slope01 * slope01 * 72f);
            BlendMask[index] = (byte)math.min(255, BlendMask[index] + slopeBoost);
        }
    }
}
