using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World.Terrain
{
    public struct HybridTerrainSeamPlanNative
    {
        // Terrain-local meters. The applier subtracts the terrain AUP before casting to float to avoid 100km jitter.
        public float3 TerrainLocalContactPosition;
        public float3 TerrainLocalVoxelCenter;
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
        public const float RaymarchHalfSpanMeters = 15f;  // Increased from 5f to catch overhangs up to 30m above terrain
        public const int RaymarchStepCount = 48;          // Scaled 3x with span to preserve ~0.625m/step at max quality
        public const float ExpensiveSamplingStartWeight = 0.30f;

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeQualityWeight(float globalQualityWeight)
        {
            return math.saturate(math.select(1f, globalQualityWeight, math.isfinite(globalQualityWeight)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveExpensiveSamplingWeight(float globalQualityWeight)
        {
            float q = SanitizeQualityWeight(globalQualityWeight);
            float curve = SmoothStep01(math.saturate((q - ExpensiveSamplingStartWeight) * math.rcp(math.max(1f - ExpensiveSamplingStartWeight, 0.0001f))));
            return curve;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveMaskDetailWeight(float globalQualityWeight)
        {
            float q = SanitizeQualityWeight(globalQualityWeight);
            return SmoothStep01(math.saturate((q - 0.70f) * math.rcp(0.30f)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveRaymarchStepCount(float globalQualityWeight)
        {
            float expensiveWeight = ResolveExpensiveSamplingWeight(globalQualityWeight);
            return math.max(1, (int)math.round(math.lerp(1f, RaymarchStepCount, expensiveWeight)));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HybridSdfHeightmapProjectionJob : IJobParallelFor
    {
        [NoAlias, ReadOnly] public NativeArray<float> BaselineHeights01;
        [NoAlias, ReadOnly] public NativeArray<ushort> QuantizedHeightSamples;
        [NoAlias, ReadOnly] public NativeArray<HybridTerrainSeamPlanNative> Plans;

        [NoAlias] public NativeArray<float> PatchHeights01;
        [NoAlias] public NativeArray<byte> BlendMask;

        public int HeightmapResolution;
        public int PatchX;
        public int PatchZ;
        public int PatchWidth;
        public int PatchHeight;
        public float HeightmapInvMaxIndex;
        // Legacy ABI field retained for stale generated csproj callers; projection math is terrain-local and expects zero.
        public float3 TerrainPosition;
        public float3 TerrainSize;
        public float GlobalQualityWeight;
        public byte GlobalQualityWeightValid;
        public byte VisualSamplingSuppressed;

        public void Execute(int index)
        {
            if (PatchWidth <= 0 || PatchHeight <= 0 || HeightmapResolution <= 1)
                return;

#if UNITY_EDITOR
            UnityEngine.Debug.Assert(math.all(TerrainPosition == float3.zero), "TerrainPosition ABI must be zero.");
#endif

            float qualityWeight = ResolveJobQualityWeight();
            float expensiveWeight = HybridTerrainSeamMath.ResolveExpensiveSamplingWeight(qualityWeight);
            int raymarchSteps = HybridTerrainSeamMath.ResolveRaymarchStepCount(qualityWeight);

            int localX = index % PatchWidth;
            int localZ = index / PatchWidth;
            int heightX = PatchX + localX;
            int heightZ = PatchZ + localZ;
            int terrainIndex = heightX + heightZ * HeightmapResolution;
            float height01 = ResolveHeight01(terrainIndex);
            float localTerrainX = heightX * HeightmapInvMaxIndex * TerrainSize.x;
            float localTerrainZ = heightZ * HeightmapInvMaxIndex * TerrainSize.z;
            float localTerrainY = height01 * TerrainSize.y;
            float mask01 = 0f;

            for (int i = 0; i < Plans.Length; i++)
            {
                HybridTerrainSeamPlanNative plan = Plans[i];
                float2 delta = new float2(localTerrainX - plan.TerrainLocalContactPosition.x, localTerrainZ - plan.TerrainLocalContactPosition.z);
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
                float blendWeight = falloff * math.saturate(planWeight);
                mask01 = math.max(mask01, blendWeight);

                if (expensiveWeight <= 0.0001f)
                    continue;

                float voxelSurfaceY = RaymarchDownToVoxelSurface(localTerrainX, localTerrainY, localTerrainZ, plan, raymarchSteps);
                float heightDelta = math.abs(voxelSurfaceY - localTerrainY);
                if (heightDelta > HybridTerrainSeamMath.CloseHeightBandMeters)
                    continue;

                float smoothTarget = HybridTerrainSeamMath.SmoothMinNoTranscendental(
                    localTerrainY,
                    voxelSurfaceY,
                    HybridTerrainSeamMath.CloseHeightBandMeters);
                float raise = math.max(0f, plan.TerrainDelta) + plan.SuggestedTerrainRaise;
                float cut = math.max(0f, -plan.TerrainDelta) + plan.SuggestedTerrainCut * math.saturate(plan.CaveBlendWeight + 0.25f);
                float biasedTarget = smoothTarget + (raise - cut) * 0.12f * falloff;
                localTerrainY = math.lerp(localTerrainY, biasedTarget, blendWeight * expensiveWeight);
            }

            PatchHeights01[index] = math.saturate(localTerrainY * math.rcp(math.max(TerrainSize.y, 0.0001f)));
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolveJobQualityWeight()
        {
            return GlobalQualityWeightValid != 0 && math.isfinite(GlobalQualityWeight)
                ? math.saturate(GlobalQualityWeight)
                : 1f;
        }

        private static float RaymarchDownToVoxelSurface(
            float localTerrainX,
            float terrainLocalY,
            float localTerrainZ,
            in HybridTerrainSeamPlanNative plan,
            int raymarchSteps)
        {
            raymarchSteps = math.max(raymarchSteps, 1);
            float step = (HybridTerrainSeamMath.RaymarchHalfSpanMeters * 2f) * math.rcp(raymarchSteps);
            float y = terrainLocalY + HybridTerrainSeamMath.RaymarchHalfSpanMeters;
            float previousY = y;
            float previousSdf = SampleAnalyticSdf(new float3(localTerrainX, y, localTerrainZ), plan);

            for (int i = 1; i <= raymarchSteps; i++)
            {
                y -= step;
                float sdf = SampleAnalyticSdf(new float3(localTerrainX, y, localTerrainZ), plan);
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
            return plan.TerrainLocalContactPosition.y - contactBias;
        }

        private static float SampleAnalyticSdf(float3 position, in HybridTerrainSeamPlanNative plan)
        {
            float3 halfSize = math.max(plan.VoxelSize * 0.5f, new float3(0.25f, 0.25f, 0.25f));
            float3 d = math.abs(position - plan.TerrainLocalVoxelCenter) - halfSize;
            float externalDist = math.length(math.max(d, 0.0f));
            float internalDist = math.min(math.cmax(d), 0.0f);
            return externalDist + internalDist;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HybridTerrainSeamNormalJob : IJobParallelFor
    {
        [NoAlias, ReadOnly] public NativeArray<float> PatchHeights01;
        [NoAlias] public NativeArray<float3> Normals;

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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HybridTerrainSeamMaskDetailJob : IJobParallelFor
    {
        [NoAlias, ReadOnly] public NativeArray<float3> Normals;
        [NoAlias] public NativeArray<byte> BlendMask;

        public float GlobalQualityWeight;
        public byte GlobalQualityWeightValid;
        public byte EnableDetail;

        public void Execute(int index)
        {
            float qualityWeight = GlobalQualityWeightValid != 0 && math.isfinite(GlobalQualityWeight)
                ? math.saturate(GlobalQualityWeight)
                : 1f;
            float detailWeight = HybridTerrainSeamMath.ResolveMaskDetailWeight(qualityWeight);
            if (EnableDetail == 0 || detailWeight <= 0.0001f)
                return;

            float3 normal = Normals[index];
            if (!math.all(math.isfinite(normal)))
                return;

            float slope01 = math.saturate(1f - math.saturate(normal.y));
            int slopeBoost = (int)math.round(slope01 * slope01 * 72f * detailWeight);
            BlendMask[index] = (byte)math.min(255, BlendMask[index] + slopeBoost);
        }
    }
}
