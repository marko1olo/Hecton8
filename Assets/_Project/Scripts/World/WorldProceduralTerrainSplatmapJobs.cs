using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    [System.Obsolete("DO NOT USE. Legacy job leaking weights. Awaiting PILLAR 1 8-layer architecture.", true)]
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct WorldProceduralTerrainSlopeCavitySplatmapJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float> Heights01;
        [ReadOnly, NoAlias] public NativeArray<float> Sediment01;
        [WriteOnly, NoAlias] public NativeArray<float4> Weights;
        [WriteOnly, NoAlias] public NativeArray<float> SlopeWeights01;

        public int Width;
        public int Height;
        public float CellSizeMeters;
        public float HeightScaleMeters;
        public float RockSlopeThresholdDegrees;
        public float SlopeBlendWidthDegrees;
        public float CavityStrength;
        public float SedimentStrength;
        public uint UseMacroGeology;
        public WorldMacroGeologyParams MacroGeologyParams;
        public double2 WorldOriginXZ;

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
            float slopeDegrees = math.degrees(global::Hecton8.Core.MathLodApproximation.ApproxAtanFast(FastMagnitudeApprox(new float2(dx, dz))));

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

            if (UseMacroGeology != 0u)
                ApplyMacroGeologySurfaceWeights(x, z, safeCellSize, ref sand, ref rock, ref silt, ref cavity);

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

        private void ApplyMacroGeologySurfaceWeights(
            int x,
            int z,
            float cellSizeMeters,
            ref float sand,
            ref float rock,
            ref float silt,
            ref float cavity)
        {
            float absoluteX = (float)(WorldOriginXZ.x + x * (double)cellSizeMeters);
            float absoluteZ = (float)(WorldOriginXZ.y + z * (double)cellSizeMeters);
            WorldMacroGeologySample sample = WorldMacroGeologyFields.Evaluate(absoluteX, absoluteZ, in MacroGeologyParams);
            WorldTerrainSurfaceMaterialWeights materialWeights = WorldTerrainSurfaceMaterialResolver.Resolve(
                in sample,
                absoluteX,
                absoluteZ,
                MacroGeologyParams.Seed);
            WorldTerrainMesoDetailParams mesoParams = ResolveMesoDetailParams(cellSizeMeters);
            WorldTerrainMesoDetailSample meso = WorldTerrainMesoDetailFields.Evaluate(
                in sample,
                absoluteX,
                absoluteZ,
                in mesoParams);
            materialWeights = WorldTerrainSurfaceMaterialResolver.ApplyMesoDetailBias(materialWeights, in meso);
            WorldTerrainControlMapSplats splats = WorldTerrainSurfaceMaterialResolver.ResolveControlSplats(in materialWeights);
            float4 control1 = splats.Control1;
            float4 control2 = splats.Control2;

            const float geologyBlend = 0.78f;
            rock = math.saturate(math.lerp(rock, control1.w, geologyBlend));
            sand = math.saturate(math.lerp(sand, control1.x, geologyBlend));
            silt = math.saturate(math.lerp(silt, control1.z, geologyBlend));
            cavity = math.saturate(math.max(cavity, math.max(control2.y, meso.TributaryCanyonMask * 0.30f + meso.SlumpScarMask * 0.22f)));
        }

        private WorldTerrainMesoDetailParams ResolveMesoDetailParams(float cellSizeMeters)
        {
            WorldTerrainMesoDetailParams meso = WorldTerrainMesoDetailFields.CreateDefaultParams(MacroGeologyParams.Seed);
            meso.PreviewExtentMeters = math.max(
                WorldTerrainDetailContracts.MesoProofExtentMeters,
                cellSizeMeters * math.max(math.max(Width, Height), 1));
            return meso;
        }

        private static float FastMagnitudeApprox(float2 value)
        {
            float2 abs = math.abs(value);
            float max = math.max(abs.x, abs.y);
            float min = math.min(abs.x, abs.y);
            return max + (min * 0.41421356f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct WorldTerrainSurfaceMaterialMaskJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<float> HeightBufferMeters;
        [WriteOnly, NoAlias] public NativeArray<float4> Primary;
        [WriteOnly, NoAlias] public NativeArray<float4> Secondary;
        [WriteOnly, NoAlias] public NativeArray<float4> Control1;
        [WriteOnly, NoAlias] public NativeArray<float4> Control2;
        [WriteOnly, NoAlias] public NativeArray<float> Slope01;
        [WriteOnly, NoAlias] public NativeArray<float> Curvature01;
        [WriteOnly, NoAlias] public NativeArray<int> DominantMaterialIndex;

        public int Width;
        public int Height;
        public int HeightBufferResolution;
        public float CellSizeMeters;
        public float HeightCellSizeMeters;
        public double2 WorldOriginXZ;
        public WorldMacroGeologyParams MacroGeologyParams;
        public float MaskContrast;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Primary.Length ||
                (uint)index >= (uint)Secondary.Length ||
                (uint)index >= (uint)Control1.Length ||
                (uint)index >= (uint)Control2.Length)
            {
                return;
            }

            int safeWidth = math.max(1, Width);
            int safeHeight = math.max(1, Height);
            int x = index % safeWidth;
            int z = index / safeWidth;
            if (z >= safeHeight)
                return;

            float safeCellSize = math.max(0.001f, CellSizeMeters);
            float absoluteX = (float)(WorldOriginXZ.x + x * (double)safeCellSize);
            float absoluteZ = (float)(WorldOriginXZ.y + z * (double)safeCellSize);
            int requiredHeightSamples = math.max(0, HeightBufferResolution) * math.max(0, HeightBufferResolution);
            bool useCachedHeightBuffer = HeightBufferMeters.IsCreated && HeightBufferResolution > 1 && HeightBufferMeters.Length >= requiredHeightSamples;
            WorldMacroGeologySample sample;
            if (useCachedHeightBuffer)
            {
                float safeHeightCellSize = math.max(0.001f, HeightCellSizeMeters > 0f ? HeightCellSizeMeters : safeCellSize);
                float u = (float)x / (float)math.max(1, safeWidth - 1);
                float v = (float)z / (float)math.max(1, safeHeight - 1);
                float center = SampleHeightBilinear(u, v);

                float du = 1f / (float)math.max(1, safeWidth - 1);
                float dv = 1f / (float)math.max(1, safeHeight - 1);
                float west = SampleHeightBilinear(math.max(0f, u - du), v);
                float east = SampleHeightBilinear(math.min(1f, u + du), v);
                float south = SampleHeightBilinear(u, math.max(0f, v - dv));
                float north = SampleHeightBilinear(u, math.min(1f, v + dv));

                float dx = (east - west) / (safeHeightCellSize * 2f);
                float dz = (north - south) / (safeHeightCellSize * 2f);
                float slope = math.sqrt(math.max(0f, dx * dx + dz * dz));
                float slope01 = math.saturate(slope / 1.25f);
                float laplacian = (west + east + south + north - center * 4f) / math.max(0.001f, safeHeightCellSize * safeHeightCellSize);
                float positiveCurvature01 = math.saturate(math.max(0f, laplacian) * 280f);
                float negativeCurvature01 = math.saturate(math.max(0f, -laplacian) * 280f);
                float curvature01 = math.saturate(math.abs(laplacian) * 280f);
                sample = WorldMacroGeologyFields.EvaluateWithCachedDifferentials(
                    absoluteX,
                    absoluteZ,
                    in MacroGeologyParams,
                    center,
                    slope01,
                    curvature01,
                    positiveCurvature01,
                    negativeCurvature01);
            }
            else
            {
                sample = WorldMacroGeologyFields.Evaluate(absoluteX, absoluteZ, in MacroGeologyParams);
            }
            WorldTerrainSurfaceMaterialWeights weights = WorldTerrainSurfaceMaterialResolver.Resolve(
                in sample,
                absoluteX,
                absoluteZ,
                MacroGeologyParams.Seed);
            WorldTerrainMesoDetailParams mesoParams = ResolveMesoDetailParams(safeCellSize);
            WorldTerrainMesoDetailSample meso = WorldTerrainMesoDetailFields.Evaluate(
                in sample,
                absoluteX,
                absoluteZ,
                in mesoParams);
            weights = WorldTerrainSurfaceMaterialResolver.ApplyMesoDetailBias(weights, in meso);

            ApplyContrastAndNormalize(ref weights, math.max(0.05f, MaskContrast));

            Primary[index] = new float4(
                weights.ShellSand,
                weights.LimestoneShelf,
                weights.ClaySilt,
                weights.HardRock);

            Secondary[index] = new float4(
                weights.BrineSaltCrust,
                weights.ManganeseNodulePlain,
                weights.ReefRubble,
                weights.SeepCrust);

            WorldTerrainControlMapSplats splats = WorldTerrainSurfaceMaterialResolver.ResolveControlSplats(in weights);
            Control1[index] = splats.Control1;
            Control2[index] = splats.Control2;

            if (Slope01.IsCreated && (uint)index < (uint)Slope01.Length)
                Slope01[index] = sample.Slope01;
            if (Curvature01.IsCreated && (uint)index < (uint)Curvature01.Length)
                Curvature01[index] = sample.Curvature01;
            if (DominantMaterialIndex.IsCreated && (uint)index < (uint)DominantMaterialIndex.Length)
                DominantMaterialIndex[index] = ResolveDominantMaterialIndex(in weights);
        }

        private static int ResolveDominantMaterialIndex(in WorldTerrainSurfaceMaterialWeights weights)
        {
            // Hard materials evaluated first so they win ties against softer sediment.
            // Each SelectDominant only updates if strictly greater, so first-evaluated wins ties.
            int index = 3; // Default: HardRock
            float best = weights.HardRock;
            SelectDominant(weights.ReefRubble, 6, ref best, ref index);
            SelectDominant(weights.SeepCrust, 7, ref best, ref index);
            SelectDominant(weights.BrineSaltCrust, 4, ref best, ref index);
            SelectDominant(weights.LimestoneShelf, 1, ref best, ref index);
            SelectDominant(weights.ManganeseNodulePlain, 5, ref best, ref index);
            SelectDominant(weights.ClaySilt, 2, ref best, ref index);
            SelectDominant(weights.ShellSand, 0, ref best, ref index); // Softest evaluated last
            return index;
        }

        private static void SelectDominant(float value, int candidate, ref float best, ref int index)
        {
            if (value <= best)
                return;

            best = value;
            index = candidate;
        }

        private float SampleHeightBilinear(float u, float v)
        {
            int safeRes = math.max(2, HeightBufferResolution);
            float gx = math.clamp(u, 0f, 1f) * (float)(safeRes - 1);
            float gz = math.clamp(v, 0f, 1f) * (float)(safeRes - 1);

            int x0 = (int)math.floor(gx);
            int z0 = (int)math.floor(gz);
            int x1 = math.min(x0 + 1, safeRes - 1);
            int z1 = math.min(z0 + 1, safeRes - 1);

            float fx = gx - (float)x0;
            float fz = gz - (float)z0;

            float h00 = ReadHeightMetersDirect(x0, z0, safeRes);
            float h10 = ReadHeightMetersDirect(x1, z0, safeRes);
            float h01 = ReadHeightMetersDirect(x0, z1, safeRes);
            float h11 = ReadHeightMetersDirect(x1, z1, safeRes);

            float h0 = math.lerp(h00, h10, fx);
            float h1 = math.lerp(h01, h11, fx);
            return math.lerp(h0, h1, fz);
        }

        private float ReadHeightMetersDirect(int x, int z, int safeResolution)
        {
            int i = z * safeResolution + x;
            if (!HeightBufferMeters.IsCreated || (uint)i >= (uint)HeightBufferMeters.Length)
                return 0f;

            return HeightBufferMeters[i];
        }

        private float ReadHeightMeters(int x, int z)
        {
            return ReadHeightMetersDirect(x, z, math.max(1, HeightBufferResolution));
        }

        private WorldTerrainMesoDetailParams ResolveMesoDetailParams(float cellSizeMeters)
        {
            WorldTerrainMesoDetailParams meso = WorldTerrainMesoDetailFields.CreateDefaultParams(MacroGeologyParams.Seed);
            meso.PreviewExtentMeters = math.max(
                WorldTerrainDetailContracts.MesoProofExtentMeters,
                cellSizeMeters * math.max(math.max(Width, Height), 1));
            return meso;
        }

        private static void ApplyContrastAndNormalize(ref WorldTerrainSurfaceMaterialWeights weights, float contrast)
        {
            // Sum original weights first.
            float origTotal =
                weights.ShellSand + weights.LimestoneShelf + weights.ClaySilt + weights.HardRock +
                weights.BrineSaltCrust + weights.ManganeseNodulePlain + weights.ReefRubble + weights.SeepCrust;

            if (origTotal <= 0.0001f || !math.isfinite(origTotal))
                return; // Empty pixel — nothing to do.

            if (math.abs(contrast - 1f) <= 0.0001f)
            {
                // Fast path: no contrast, just normalize.
                float invOrig = 1f / origTotal;
                weights.ShellSand        *= invOrig; weights.LimestoneShelf    *= invOrig;
                weights.ClaySilt         *= invOrig; weights.HardRock          *= invOrig;
                weights.BrineSaltCrust   *= invOrig; weights.ManganeseNodulePlain *= invOrig;
                weights.ReefRubble       *= invOrig; weights.SeepCrust         *= invOrig;
                return;
            }

            // Save originals for safe fallback.
            WorldTerrainSurfaceMaterialWeights orig = weights;

            // Apply contrast ONLY to non-trivial weights — skips math.pow on zeros,
            // cutting ~70% of transcendental ops on typical terrain pixels.
            const float kTrivialThreshold = 0.001f;
            weights.ShellSand        = orig.ShellSand        > kTrivialThreshold ? math.pow(math.saturate(orig.ShellSand),        contrast) : 0f;
            weights.LimestoneShelf   = orig.LimestoneShelf   > kTrivialThreshold ? math.pow(math.saturate(orig.LimestoneShelf),   contrast) : 0f;
            weights.ClaySilt         = orig.ClaySilt         > kTrivialThreshold ? math.pow(math.saturate(orig.ClaySilt),         contrast) : 0f;
            weights.HardRock         = orig.HardRock         > kTrivialThreshold ? math.pow(math.saturate(orig.HardRock),         contrast) : 0f;
            weights.BrineSaltCrust   = orig.BrineSaltCrust   > kTrivialThreshold ? math.pow(math.saturate(orig.BrineSaltCrust),   contrast) : 0f;
            weights.ManganeseNodulePlain = orig.ManganeseNodulePlain > kTrivialThreshold ? math.pow(math.saturate(orig.ManganeseNodulePlain), contrast) : 0f;
            weights.ReefRubble       = orig.ReefRubble       > kTrivialThreshold ? math.pow(math.saturate(orig.ReefRubble),       contrast) : 0f;
            weights.SeepCrust        = orig.SeepCrust        > kTrivialThreshold ? math.pow(math.saturate(orig.SeepCrust),        contrast) : 0f;

            float newTotal =
                weights.ShellSand + weights.LimestoneShelf + weights.ClaySilt + weights.HardRock +
                weights.BrineSaltCrust + weights.ManganeseNodulePlain + weights.ReefRubble + weights.SeepCrust;

            // FIX: If contrast crushed all weights, restore originals — do NOT corrupt pixel with pure Sand.
            if (newTotal <= 0.0001f || !math.isfinite(newTotal))
            {
                weights = orig;
                newTotal = origTotal;
            }

            float invTotal = 1f / newTotal;
            weights.ShellSand        *= invTotal; weights.LimestoneShelf    *= invTotal;
            weights.ClaySilt         *= invTotal; weights.HardRock          *= invTotal;
            weights.BrineSaltCrust   *= invTotal; weights.ManganeseNodulePlain *= invTotal;
            weights.ReefRubble       *= invTotal; weights.SeepCrust         *= invTotal;
        }
    }
}
