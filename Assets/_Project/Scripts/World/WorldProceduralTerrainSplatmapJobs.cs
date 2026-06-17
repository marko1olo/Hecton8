using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
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
        [WriteOnly, NoAlias] public NativeArray<float4> Primary;
        [WriteOnly, NoAlias] public NativeArray<float4> Secondary;
        [WriteOnly, NoAlias] public NativeArray<float4> Control1;
        [WriteOnly, NoAlias] public NativeArray<float4> Control2;

        public int Width;
        public int Height;
        public float CellSizeMeters;
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
            int x = index % safeWidth;
            int z = index / safeWidth;
            float safeCellSize = math.max(0.001f, CellSizeMeters);
            float absoluteX = (float)(WorldOriginXZ.x + x * (double)safeCellSize);
            float absoluteZ = (float)(WorldOriginXZ.y + z * (double)safeCellSize);
            WorldMacroGeologySample sample = WorldMacroGeologyFields.Evaluate(absoluteX, absoluteZ, in MacroGeologyParams);
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
            if (math.abs(contrast - 1f) <= 0.0001f)
                return;

            weights.ShellSand = math.pow(math.saturate(weights.ShellSand), contrast);
            weights.LimestoneShelf = math.pow(math.saturate(weights.LimestoneShelf), contrast);
            weights.ClaySilt = math.pow(math.saturate(weights.ClaySilt), contrast);
            weights.HardRock = math.pow(math.saturate(weights.HardRock), contrast);
            weights.BrineSaltCrust = math.pow(math.saturate(weights.BrineSaltCrust), contrast);
            weights.ManganeseNodulePlain = math.pow(math.saturate(weights.ManganeseNodulePlain), contrast);
            weights.ReefRubble = math.pow(math.saturate(weights.ReefRubble), contrast);
            weights.SeepCrust = math.pow(math.saturate(weights.SeepCrust), contrast);

            float total =
                weights.ShellSand +
                weights.LimestoneShelf +
                weights.ClaySilt +
                weights.HardRock +
                weights.BrineSaltCrust +
                weights.ManganeseNodulePlain +
                weights.ReefRubble +
                weights.SeepCrust;

            if (total <= 0.0001f || !math.isfinite(total))
            {
                weights.ShellSand = 1f;
                weights.LimestoneShelf = 0f;
                weights.ClaySilt = 0f;
                weights.HardRock = 0f;
                weights.BrineSaltCrust = 0f;
                weights.ManganeseNodulePlain = 0f;
                weights.ReefRubble = 0f;
                weights.SeepCrust = 0f;
                return;
            }

            float invTotal = 1f / total;
            weights.ShellSand *= invTotal;
            weights.LimestoneShelf *= invTotal;
            weights.ClaySilt *= invTotal;
            weights.HardRock *= invTotal;
            weights.BrineSaltCrust *= invTotal;
            weights.ManganeseNodulePlain *= invTotal;
            weights.ReefRubble *= invTotal;
            weights.SeepCrust *= invTotal;
        }
    }
}
