using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.World
{
    public enum WorldMacroGeologyZone : byte
    {
        Unknown = 0,
        PhoticShelf = 1,
        ShelfBreak = 2,
        FaultRidge = 3,
        BrineTrench = 4,
        AbyssalPlain = 5,
        SedimentFan = 6,
        ColdSeepField = 7,
        HadalBasin = 8
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct WorldMacroGeologyParams
    {
        public uint Seed;
        public float WorldExtentMeters;
        public float ChunkSizeMeters;
        public float WaterSurfaceY;
        public float ShelfDepthMeters;
        public float AbyssDepthMeters;
        public float HadalDepthMeters;
        public float ShelfBreakWidthMeters;
        public float RidgeHeightMeters;
        public float RidgeWidthMeters;
        public float TrenchDepthMeters;
        public float TrenchWidthMeters;
        public float BasinDepthMeters;
        public float DetailProbeMeters;

        public static WorldMacroGeologyParams CreateDefault(uint seed)
        {
            return new WorldMacroGeologyParams
            {
                Seed = seed,
                WorldExtentMeters = WorldMacroGeologyFields.MinimumWorldExtentMeters,
                ChunkSizeMeters = WorldMacroGeologyFields.DefaultChunkSizeMeters,
                WaterSurfaceY = 0f,
                ShelfDepthMeters = 90f,
                AbyssDepthMeters = 2950f,
                HadalDepthMeters = 4600f,
                ShelfBreakWidthMeters = 5200f,
                RidgeHeightMeters = 1550f,
                RidgeWidthMeters = 2350f,
                TrenchDepthMeters = 900f,
                TrenchWidthMeters = 2200f,
                BasinDepthMeters = 620f,
                DetailProbeMeters = 120f
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WorldMacroGeologySample
    {
        public float HeightMeters;
        public float DepthMeters;
        public float ShelfMask;
        public float ShelfBreakMask;
        public float RidgeMask;
        public float TrenchMask;
        public float BasinMask;
        public float FaultMask;
        public float SedimentMask;
        public float SeepMask;
        public float Slope01;
        public float Curvature01;
        public float ErosionFlow01;
        public float TerraceMask;
        public float SlumpScarMask;
        public float TributaryCanyonMask;
        public float NodulePlainMask;
        public float ReefEligibilityMask;
        public float HardRockExposureMask;
        public float VoxelSeamMask;
        public WorldMacroGeologyZone PrimaryZone;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WorldMacroGeologyChunkKey
    {
        public int X;
        public int Z;
        public uint Seed;
        public uint ArtifactVersion;
        public uint ChunkSizeMeters;
    }

    /// <summary>
    /// Seeded macro geology fields for the playable seafloor. This is the source-level shape model;
    /// runtime chunk meshes, MapMagic layers, voxel seam masks, scatter gates, and PDA maps should
    /// derive from these stable fields instead of inventing local terrain truth. The configured extent
    /// is the minimum authored preview window; sampling remains procedural beyond that window in AUP XZ.
    /// </summary>
    public static class WorldMacroGeologyFields
    {
        public const int DefaultAuthoringSeed = 880031;
        public const float MinimumWorldExtentMeters = 30000f;
        public const float DefaultChunkSizeMeters = 512f;
        public const uint ArtifactVersion = 6u;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint CombineWorldSeed(uint authoringSeed, int runtimeWorldSeed)
        {
            return Hash((int)authoringSeed, runtimeWorldSeed, unchecked((int)0x6D2B79F5u));
        }

        public static bool TrySanitizeParams(in WorldMacroGeologyParams source, out WorldMacroGeologyParams sanitized)
        {
            sanitized = source;
            sanitized.WorldExtentMeters = math.max(MinimumWorldExtentMeters, source.WorldExtentMeters);
            sanitized.ChunkSizeMeters = math.max(128f, source.ChunkSizeMeters);
            sanitized.ShelfDepthMeters = math.max(10f, source.ShelfDepthMeters);
            sanitized.AbyssDepthMeters = math.max(sanitized.ShelfDepthMeters + 500f, source.AbyssDepthMeters);
            sanitized.HadalDepthMeters = math.max(sanitized.AbyssDepthMeters + 1000f, source.HadalDepthMeters);
            sanitized.ShelfBreakWidthMeters = math.max(500f, source.ShelfBreakWidthMeters);
            sanitized.RidgeHeightMeters = math.max(0f, source.RidgeHeightMeters);
            sanitized.RidgeWidthMeters = math.max(250f, source.RidgeWidthMeters);
            sanitized.TrenchDepthMeters = math.max(0f, source.TrenchDepthMeters);
            sanitized.TrenchWidthMeters = math.max(250f, source.TrenchWidthMeters);
            sanitized.BasinDepthMeters = math.max(0f, source.BasinDepthMeters);
            sanitized.DetailProbeMeters = math.max(8f, source.DetailProbeMeters);
            return math.isfinite(sanitized.WorldExtentMeters) &&
                   math.isfinite(sanitized.ChunkSizeMeters) &&
                   math.isfinite(sanitized.WaterSurfaceY);
        }

        public static WorldMacroGeologySample Evaluate(float absoluteX, float absoluteZ, in WorldMacroGeologyParams parameters)
        {
            if (!TrySanitizeParams(in parameters, out WorldMacroGeologyParams p))
                return default;

            float height = EvaluateHeightMeters(absoluteX, absoluteZ, in p, out MacroMasks masks);
            float probe = p.DetailProbeMeters;
            float west = EvaluateHeightMeters(absoluteX - probe, absoluteZ, in p, out _);
            float east = EvaluateHeightMeters(absoluteX + probe, absoluteZ, in p, out _);
            float south = EvaluateHeightMeters(absoluteX, absoluteZ - probe, in p, out _);
            float north = EvaluateHeightMeters(absoluteX, absoluteZ + probe, in p, out _);

            float dx = (east - west) / math.max(0.001f, probe * 2f);
            float dz = (north - south) / math.max(0.001f, probe * 2f);
            float slope = FastSqrtPositive(dx * dx + dz * dz);
            float curvature = (west + east + south + north - height * 4f) /
                math.max(0.001f, probe * probe);
            float slope01 = math.saturate(slope / 1.25f);
            float curvature01 = math.saturate(math.abs(curvature) * 280f);
            float positiveCurvature01 = math.saturate(math.max(0f, curvature) * 280f);
            float negativeCurvature01 = math.saturate(math.max(0f, -curvature) * 280f);
            float basinFlow = math.saturate(masks.Basin * 0.48f + masks.ShelfBreak * 0.22f + (1f - slope01) * 0.18f);
            float faultFlow = math.saturate(masks.Fault * 0.35f + masks.Trench * 0.32f);
            float erosionFlow = math.saturate(basinFlow + faultFlow + FractalNoise01(new float2(absoluteX, absoluteZ) * 0.00038f, p.Seed ^ 0xA511E9B3u) * 0.12f);
            float sediment = math.saturate((1f - slope01) * 0.58f + masks.Basin * 0.42f + masks.Shelf * 0.16f - masks.Ridge * 0.28f - masks.Trench * 0.16f);
            float seep = math.saturate(masks.Fault * 0.45f + masks.Basin * 0.25f + masks.Trench * 0.18f);
            float deepPlain01 = math.saturate((p.WaterSurfaceY - height - 1600f) / 1800f);
            float shallowReefBand01 = math.saturate(1f - math.abs(p.WaterSurfaceY - height - 90f) / 520f);

            WorldMacroGeologySample sample = new WorldMacroGeologySample
            {
                HeightMeters = height,
                DepthMeters = math.max(0f, p.WaterSurfaceY - height),
                ShelfMask = masks.Shelf,
                ShelfBreakMask = masks.ShelfBreak,
                RidgeMask = masks.Ridge,
                TrenchMask = masks.Trench,
                BasinMask = masks.Basin,
                FaultMask = masks.Fault,
                SedimentMask = sediment,
                SeepMask = seep,
                Slope01 = slope01,
                Curvature01 = curvature01,
                ErosionFlow01 = erosionFlow,
                TerraceMask = math.saturate(masks.ShelfBreak * 0.36f + masks.Shelf * 0.14f + positiveCurvature01 * 0.20f + erosionFlow * 0.16f - masks.Trench * 0.16f),
                SlumpScarMask = math.saturate(masks.ShelfBreak * 0.30f + negativeCurvature01 * 0.42f + slope01 * 0.28f - masks.Ridge * 0.18f),
                TributaryCanyonMask = math.saturate(erosionFlow * 0.48f + masks.Fault * 0.24f + masks.ShelfBreak * 0.20f + negativeCurvature01 * 0.26f),
                NodulePlainMask = math.saturate(sediment * 0.52f + (1f - slope01) * 0.28f + deepPlain01 * 0.20f - masks.Ridge * 0.35f - masks.Trench * 0.20f),
                ReefEligibilityMask = math.saturate(masks.Shelf * 0.52f + shallowReefBand01 * 0.34f + (1f - slope01) * 0.18f - masks.Trench * 0.25f),
                HardRockExposureMask = math.saturate(masks.Ridge * 0.45f + masks.Fault * 0.28f + slope01 * 0.30f - sediment * 0.24f),
                VoxelSeamMask = math.saturate(masks.Fault * 0.35f + curvature01 * 0.40f + slope01 * 0.22f + masks.Trench * 0.16f)
            };
            sample.PrimaryZone = ResolveZone(in sample);
            return sample;
        }

        public static float EvaluateHeightMeters(float absoluteX, float absoluteZ, in WorldMacroGeologyParams parameters)
        {
            if (!TrySanitizeParams(in parameters, out WorldMacroGeologyParams p))
                return 0f;

            return EvaluateHeightMeters(absoluteX, absoluteZ, in p, out _);
        }

        public static int2 ResolveChunkCoord(float absoluteX, float absoluteZ, in WorldMacroGeologyParams parameters)
        {
            float chunkSize = math.max(128f, parameters.ChunkSizeMeters);
            return new int2((int)math.floor(absoluteX / chunkSize), (int)math.floor(absoluteZ / chunkSize));
        }

        public static WorldMacroGeologyChunkKey BuildChunkKey(int2 chunkCoord, in WorldMacroGeologyParams parameters)
        {
            return new WorldMacroGeologyChunkKey
            {
                X = chunkCoord.x,
                Z = chunkCoord.y,
                Seed = parameters.Seed,
                ArtifactVersion = ArtifactVersion,
                ChunkSizeMeters = (uint)math.max(128f, math.round(parameters.ChunkSizeMeters))
            };
        }

        public static ulong BuildChunkArtifactId(in WorldMacroGeologyChunkKey key)
        {
            ulong value = ((ulong)key.Seed << 32) ^ ((ulong)(key.ArtifactVersion & 0xFFFFu) << 16);
            value ^= ((ulong)key.ChunkSizeMeters & 0xFFFFul) * 0xD6E8FEB86659FD93ul;
            value ^= ZigZag32(key.X) * 0x9E3779B97F4A7C15ul;
            value ^= ZigZag32(key.Z) * 0xC2B2AE3D27D4EB4Ful;
            return Mix64(value);
        }

        public static void ResolveMinimumChunkRange(
            float chunkSizeMeters,
            out int minX,
            out int minZ,
            out int maxX,
            out int maxZ)
        {
            float safeChunkSize = math.max(1f, chunkSizeMeters);
            float halfExtentMeters = MinimumWorldExtentMeters * 0.5f;
            minX = (int)math.floor(-halfExtentMeters / safeChunkSize);
            minZ = minX;
            maxX = (int)math.floor((halfExtentMeters - 0.001f) / safeChunkSize);
            maxZ = maxX;
        }

        public static uint BuildChunkArtifactRangeHash(
            uint authoringSeed,
            int runtimeSeed,
            int worldGenerationVersionId,
            uint macroArtifactVersion,
            float chunkSizeMeters,
            int chunkMinX,
            int chunkMinZ,
            int chunkMaxX,
            int chunkMaxZ)
        {
            uint hash = 2166136261u;
            MixHash(ref hash, authoringSeed);
            MixHash(ref hash, unchecked((uint)runtimeSeed));
            MixHash(ref hash, unchecked((uint)worldGenerationVersionId));
            MixHash(ref hash, macroArtifactVersion);
            MixHash(ref hash, math.asuint(chunkSizeMeters));
            MixHash(ref hash, unchecked((uint)chunkMinX));
            MixHash(ref hash, unchecked((uint)chunkMinZ));
            MixHash(ref hash, unchecked((uint)chunkMaxX));
            MixHash(ref hash, unchecked((uint)chunkMaxZ));
            return hash != 0u ? hash : 1u;
        }

        public static WorldMacroGeologyZone ResolveZone(in WorldMacroGeologySample sample)
        {
            if ((sample.TrenchMask > 0.92f && sample.DepthMeters > 500f) || sample.DepthMeters > 4450f)
                return WorldMacroGeologyZone.BrineTrench;

            if (sample.ShelfMask > 0.68f && sample.DepthMeters < 260f)
                return WorldMacroGeologyZone.PhoticShelf;

            if (sample.ShelfBreakMask > 0.35f &&
                sample.DepthMeters > 150f &&
                sample.DepthMeters < 2400f)
            {
                return WorldMacroGeologyZone.ShelfBreak;
            }

            if (sample.SeepMask > 0.40f &&
                sample.DepthMeters > 500f &&
                sample.DepthMeters < 4300f &&
                sample.TrenchMask < 0.62f &&
                sample.ShelfMask < 0.82f &&
                sample.RidgeMask < 0.88f &&
                (sample.FaultMask > 0.28f || sample.BasinMask > 0.38f))
            {
                return WorldMacroGeologyZone.ColdSeepField;
            }

            if (sample.BasinMask > 0.54f && sample.SedimentMask > 0.50f && sample.Slope01 < 0.48f)
                return WorldMacroGeologyZone.SedimentFan;

            if (sample.RidgeMask > 0.72f || (sample.FaultMask > 0.82f && sample.Slope01 > 0.48f))
                return WorldMacroGeologyZone.FaultRidge;

            if (sample.DepthMeters > 3900f && sample.TrenchMask < 0.76f)
                return WorldMacroGeologyZone.HadalBasin;

            return WorldMacroGeologyZone.AbyssalPlain;
        }

        private static float EvaluateHeightMeters(float absoluteX, float absoluteZ, in WorldMacroGeologyParams p, out MacroMasks masks)
        {
            float extent = math.max(MinimumWorldExtentMeters, p.WorldExtentMeters);
            float half = extent * 0.5f;
            float2 pos = new float2(absoluteX, absoluteZ);
            float2 norm = pos / extent;
            float lowWarp = (FractalNoise01(norm * 2.0f + new float2(11.7f, -3.9f), p.Seed ^ 0xB5297A4Du) * 2f - 1f) * 980f;
            float midWarp = (FractalNoise01(norm * 4.4f + new float2(-2.1f, 8.6f), p.Seed ^ 0x4CF5AD43u) * 2f - 1f) * 520f;
            float highWarp = (FractalNoise01(norm * 7.2f + new float2(-17.2f, 29.3f), p.Seed ^ 0x68E31DA4u) * 2f - 1f) * 240f;

            float shelfCurve = -half * 0.30f +
                math.sin(norm.x * 6.0f + HashToUnitFloat(p.Seed) * 6.283185f) * 1150f +
                math.sin(norm.x * 13.5f + HashToUnitFloat(p.Seed ^ 0xBADC0FFEu) * 6.283185f) * 420f +
                lowWarp * 0.62f +
                midWarp * 0.18f;
            float shelfDistance = absoluteZ - shelfCurve;
            float shelfMask = 1f - math.smoothstep(-p.ShelfBreakWidthMeters * 0.45f, p.ShelfBreakWidthMeters * 1.70f, shelfDistance);
            float shelfBreakMask = 1f - math.smoothstep(0f, p.ShelfBreakWidthMeters * 0.85f, math.abs(shelfDistance));
            float descent01 = math.smoothstep(-p.ShelfBreakWidthMeters * 0.65f, p.ShelfBreakWidthMeters * 2.45f, shelfDistance);

            float depth = math.lerp(p.ShelfDepthMeters, p.AbyssDepthMeters, descent01);
            depth += math.saturate((absoluteZ - shelfCurve) / math.max(1f, half)) * 820f;

            float terraceScale = 260f;
            float terraceLocal = depth / terraceScale;
            float terraceBase = math.floor(terraceLocal);
            float terraceFrac = terraceLocal - terraceBase;
            // Organic soft-stepping instead of primitive math.round
            float terraceSoft = terraceBase + math.smoothstep(0.2f, 0.8f, terraceFrac);
            float terraceTarget = terraceSoft * terraceScale;
            
            float terraceNoise = (FractalNoise01(norm * 16.5f + new float2(-4f, 9f), p.Seed ^ 0xD1B54A32u) * 2f - 1f) * 58f;
            float terraceWeight = math.saturate(shelfMask * 0.08f + shelfBreakMask * 0.22f);
            depth = math.lerp(depth, terraceTarget + terraceNoise, terraceWeight);

            float canyon = 0f;
            for (int i = 0; i < 5; i++)
            {
                uint h = Hash((int)p.Seed, i, unchecked((int)0xBA5EBA11u));
                float cx = math.lerp(-half * 0.88f, half * 0.88f, HashToUnitFloat(h));
                float phase = HashToUnitFloat(h ^ 0x9E3779B9u) * 6.283185f;
                float width = math.lerp(620f, 1850f, HashToUnitFloat(h ^ 0x27D4EB2Fu));
                
                // Add high frequency fractal noise to the canyon path so it's not a perfect sine wave
                float canyonWarp = (FractalNoise01(pos * 0.00015f + new float2(13.4f, -7.2f), h ^ 0x12345678u) * 2f - 1f) * 650f;
                float centerX = cx + math.sin(absoluteZ * 0.00034f + phase) * 860f + lowWarp * 0.08f + midWarp * 0.04f + canyonWarp;
                
                float gate = math.smoothstep(-p.ShelfBreakWidthMeters * 0.95f, p.ShelfBreakWidthMeters * 0.18f, shelfDistance);
                gate *= 1f - math.smoothstep(p.ShelfBreakWidthMeters * 2.50f, p.ShelfBreakWidthMeters * 5.40f, shelfDistance);
                
                // Warp the shape of the canyon so it's not a perfect V-cut
                float depthWarp = FractalNoise01(pos * 0.00028f + new float2(-9.1f, 22.3f), h ^ 0x87654321u);
                float cut = 1f - math.smoothstep(width * 0.34f, width * 1.95f, math.abs(absoluteX - centerX));
                cut *= (0.6f + depthWarp * 0.4f);
                
                canyon = math.max(canyon, cut * gate);
            }

            depth += canyon * math.lerp(150f, 560f, descent01);
            shelfBreakMask = math.max(shelfBreakMask, canyon * 0.36f);

            float rollingHills = FractalNoise01(norm * 9.0f + new float2(31.1f, -12.2f), p.Seed ^ 0xA53F9E21u) * 2f - 1f;
            depth += rollingHills * 175f * math.saturate(shelfMask * 0.45f + shelfBreakMask * 0.40f + (1f - descent01) * 0.18f);

            float2 faultNormal = math.normalize(new float2(0.72f, -0.69f));
            float2 faultTangent = new float2(-faultNormal.y, faultNormal.x);
            float alongFault = math.dot(pos, faultTangent);
            float acrossFault = math.dot(pos, faultNormal);
            float faultWander = math.sin(alongFault * 0.00031f + HashToUnitFloat(p.Seed ^ 0x9E3779B9u) * 6.283185f) * 980f +
                (FractalNoise01(new float2(alongFault * 0.00014f, 3.71f), p.Seed ^ 0xC2B2AE35u) * 2f - 1f) * 1420f;
            float trenchWarp = FractalNoise01(pos * 0.00018f, p.Seed ^ 0x12345678u);
            float trenchDistance = math.abs(acrossFault - faultWander + 1700f + highWarp * 0.42f) * (0.6f + trenchWarp * 0.4f);
            float trenchMask = 1f - math.smoothstep(p.TrenchWidthMeters * 0.62f, p.TrenchWidthMeters * 2.05f, trenchDistance);
            float faultMask = 1f - math.smoothstep(p.TrenchWidthMeters * 0.95f, p.TrenchWidthMeters * 4.20f, trenchDistance);

            float2 secondaryFaultNormal = math.normalize(new float2(-0.38f, -0.925f));
            float2 secondaryFaultTangent = new float2(-secondaryFaultNormal.y, secondaryFaultNormal.x);
            float alongSecondary = math.dot(pos, secondaryFaultTangent);
            float acrossSecondary = math.dot(pos, secondaryFaultNormal);
            float secondaryWander = math.sin(alongSecondary * 0.00026f + HashToUnitFloat(p.Seed ^ 0x51ED270Bu) * 6.283185f) * 1450f +
                (FractalNoise01(new float2(alongSecondary * 0.00011f, -9.37f), p.Seed ^ 0xA24BAED5u) * 2f - 1f) * 1180f;
            float secondaryWarp = FractalNoise01(pos * 0.00018f, p.Seed ^ 0x87654321u);
            float secondaryDistance = math.abs(acrossSecondary - secondaryWander - 900f + midWarp * 0.35f) * (0.6f + secondaryWarp * 0.4f);
            float secondaryFaultMask = 1f - math.smoothstep(p.TrenchWidthMeters * 1.10f, p.TrenchWidthMeters * 4.60f, secondaryDistance);
            float secondaryDepressionWarp = (FractalNoise01(pos * 0.00016f + new float2(11.2f, 44.9f), p.Seed ^ 0xDECADEEFu) * 2f - 1f) * 850f;
            float secondaryDepression = 1f - math.smoothstep(p.TrenchWidthMeters * 0.55f, p.TrenchWidthMeters * 2.70f, math.abs(acrossSecondary - secondaryWander + 3100f + secondaryDepressionWarp));

            float ridgeWarp = FractalNoise01(pos * 0.0002f, p.Seed ^ 0xABCDEF12u);
            float ridgeDistanceA = math.abs(acrossFault - faultWander - 4300f) * (0.5f + ridgeWarp * 0.5f);
            float ridgeDistanceB = math.abs(acrossFault - faultWander + 6900f) * (0.5f + ridgeWarp * 0.5f);
            float ridgeDistanceC = math.abs(acrossSecondary - secondaryWander - 2600f) * (0.5f + ridgeWarp * 0.5f);
            float ridgeA = 1f - math.smoothstep(p.RidgeWidthMeters * 0.34f, p.RidgeWidthMeters * 2.35f, ridgeDistanceA);
            float ridgeB = 1f - math.smoothstep(p.RidgeWidthMeters * 0.38f, p.RidgeWidthMeters * 2.45f, ridgeDistanceB);
            float ridgeC = 1f - math.smoothstep(p.RidgeWidthMeters * 0.45f, p.RidgeWidthMeters * 2.80f, ridgeDistanceC);
            float ridgeMask = math.saturate(math.max(ridgeA, math.max(ridgeB, ridgeC)) + faultMask * 0.04f + secondaryFaultMask * 0.05f);

            float basinA = EllipseMask(pos, new float2(-7400f, 6500f), new float2(6900f, 4300f), 0.34f);
            float basinB = EllipseMask(pos, new float2(8400f, -5600f), new float2(5600f, 8200f), -0.58f);
            float basinC = EllipseMask(pos, new float2(1400f, 9800f), new float2(8500f, 4800f), -0.18f);
            float basinMask = math.max(basinA, math.max(basinB, basinC));

            float ridgeChainHills = 0f;
            for (int i = 0; i < 4; i++)
            {
                uint h = Hash((int)p.Seed, i, unchecked((int)0x7FEB352Du));
                float angle = math.lerp(-1.05f, 0.85f, HashToUnitFloat(h ^ 0x846CA68Bu));
                float2 chainNormal = new float2(math.cos(angle), math.sin(angle));
                float2 chainTangent = new float2(-chainNormal.y, chainNormal.x);
                float chainAlong = math.dot(pos, chainTangent);
                float chainAcross = math.dot(pos, chainNormal);
                float center = math.lerp(-half * 0.62f, half * 0.62f, HashToUnitFloat(h ^ 0x27D4EB2Fu));
                float width = math.lerp(760f, 2100f, HashToUnitFloat(h ^ 0xC2B2AE35u));
                float wander = math.sin(chainAlong * 0.00031f + HashToUnitFloat(h ^ 0xB5297A4Du) * 6.283185f) *
                    math.lerp(520f, 1450f, HashToUnitFloat(h ^ 0xA53F9E21u));
                float band = 1f - math.smoothstep(width * 0.34f, width * 1.86f, math.abs(chainAcross - center - wander));
                float bead = FractalNoise01(new float2(chainAlong * 0.00042f + 17f, chainAcross * 0.00020f - 6f), h ^ 0x735A2D97u);
                float intermittent = math.smoothstep(0.42f, 0.86f, bead);
                ridgeChainHills = math.max(ridgeChainHills, band * intermittent);
            }

            // TECTONIC NETWORK RESTORED: Using Ridge Noise (1 - 2*abs(noise - 0.5)) instead of glitchy Voronoi CellularEdge.
            float provinceEdge = 1f - 2f * math.abs(FractalNoise01(pos * 0.000075f + new float2(-42.6f, 18.4f), p.Seed ^ 0x8B4C2F91u) - 0.5f);
            float provinceTexture = FractalNoise01(pos * 0.000061f + new float2(-3.4f, 15.1f), p.Seed ^ 0x21DA7F47u);
            float provinceRelief = math.smoothstep(0.36f, 0.92f, provinceEdge * 0.72f + provinceTexture * 0.28f);
            float regionalWarpA = (FractalNoise01(pos * 0.000050f + new float2(8.1f, -5.3f), p.Seed ^ 0xD8E4C2A7u) * 2f - 1f) * 3500f;
            float regionalWarpB = (FractalNoise01(pos * 0.000033f + new float2(-14.4f, 22.8f), p.Seed ^ 0xA1B35F19u) * 2f - 1f) * 4500f;
            float networkWarpX = (FractalNoise01(pos * 0.000085f + new float2(27.2f, -11.8f), p.Seed ^ 0xB77A91C3u) * 2f - 1f) * 2500f;
            float networkWarpZ = (FractalNoise01(pos * 0.000095f + new float2(-9.6f, 33.1f), p.Seed ^ 0x69D4F2A5u) * 2f - 1f) * 2500f;
            float networkX = absoluteX + regionalWarpA * 0.48f + networkWarpX;
            float networkZ = absoluteZ + regionalWarpB * 0.38f + networkWarpZ;
            float networkEdgeA = 1f - 2f * math.abs(FractalNoise01(new float2(networkX, networkZ) * 0.000052f + new float2(3.2f, -8.7f), p.Seed ^ 0xC35F19ABu) - 0.5f);
            float networkEdgeB = 1f - 2f * math.abs(FractalNoise01(
                new float2(networkX * 0.72f + networkZ * 0.20f, networkZ * 0.88f - networkX * 0.12f) * 0.000044f + new float2(-16.4f, 5.9f),
                p.Seed ^ 0x7E2C4D55u) - 0.5f);
            float networkActivity = math.smoothstep(0.26f, 0.78f, FractalNoise01(new float2(networkX * 0.000082f + 4.4f, networkZ * 0.000082f - 19.1f), p.Seed ^ 0x92E4B77Du));
            float braidedNetwork = math.max(
                math.smoothstep(0.28f, 0.86f, networkEdgeA),
                math.smoothstep(0.34f, 0.90f, networkEdgeB) * 0.74f);
            float networkTexture = FractalNoise01(new float2(networkX * 0.000115f - 2.7f, networkZ * 0.000115f + 8.9f), p.Seed ^ 0xCA97D1F3u);
            float tectonicNetwork = math.saturate(
                braidedNetwork *
                (0.44f + networkActivity * 0.50f) *
                (0.78f + networkTexture * 0.22f) +
                provinceRelief * 0.06f);
            float networkNode = math.smoothstep(0.72f, 0.96f, networkEdgeA) * math.smoothstep(0.52f, 0.90f, networkEdgeB) * networkActivity;
            
            float highlandNoise = FractalNoise01(pos * 0.000047f + new float2(-31.6f, 7.3f), p.Seed ^ 0xE35A9217u);
            // Retain slope inside the plateaus so they aren't perfectly flat islands
            float highlandPlate = math.smoothstep(0.56f, 0.84f, highlandNoise) * (0.8f + highlandNoise * 0.2f);
            
            float shallowNoise = FractalNoise01(
                    new float2(
                        absoluteX * 0.000030f + regionalWarpA * 0.000010f + 44.0f,
                        absoluteZ * 0.000030f + regionalWarpB * 0.000010f - 28.0f),
                    p.Seed ^ 0x4B6D13F7u);
            float shallowProvince = math.smoothstep(0.50f, 0.80f, shallowNoise) * (0.75f + shallowNoise * 0.25f);
            float riseWaveA = 0.5f + 0.5f * math.sin((absoluteX * 0.34f + absoluteZ * 0.94f + regionalWarpA) * 0.00019f + HashToUnitFloat(p.Seed ^ 0x6E624EB7u) * 6.283185f);
            float riseWaveB = 0.5f + 0.5f * math.sin((absoluteX * -0.87f + absoluteZ * 0.49f + regionalWarpB) * 0.000145f + HashToUnitFloat(p.Seed ^ 0xC7425A31u) * 6.283185f);
            float directedUplift = math.max(math.smoothstep(0.64f, 0.96f, riseWaveA) * 0.38f, math.smoothstep(0.68f, 0.97f, riseWaveB) * 0.30f);
            float distributedHighsNoise = FractalNoise01(
                new float2(
                    absoluteX * 0.000033f + regionalWarpA * 0.000006f - 71.0f,
                    absoluteZ * 0.000033f + regionalWarpB * 0.000006f + 39.0f),
                p.Seed ^ 0x18F3A9C5u);
            float distributedHighsWave = riseWaveA * 0.58f + riseWaveB * 0.42f;
            float distributedHighs = math.max(
                math.smoothstep(0.52f, 0.82f, distributedHighsNoise),
                math.smoothstep(0.70f, 0.96f, distributedHighsWave) * 0.72f) *
                (0.54f + networkActivity * 0.32f + provinceRelief * 0.18f);
            float upliftNetwork = math.saturate(math.max(math.max(math.max(math.max(tectonicNetwork * 0.62f, highlandPlate * 0.48f), shallowProvince * 0.58f), directedUplift), distributedHighs * 0.58f) + provinceRelief * 0.04f + ridgeChainHills * 0.12f + networkNode * 0.12f);
            float basinWarp = FractalNoise01(pos * 0.0001f, p.Seed ^ 0x8F2C3A1Du) * 0.6f;
            float basinWave = 0.5f + 0.5f * math.sin((absoluteX * 0.68f - absoluteZ * 0.74f + regionalWarpB * 0.72f) * 0.00012f + basinWarp + HashToUnitFloat(p.Seed ^ 0x53C9E2B1u) * 6.283185f);
            float recurringBasin = math.saturate(math.smoothstep(0.72f, 0.98f, basinWave) * (1f - upliftNetwork * 0.62f));
            float fractureBase = 1f - 2f * math.abs(FractalNoise01(pos * 0.00018f + new float2(19.3f, -7.1f), p.Seed ^ 0x51633E2Du) - 0.5f);
            float fractureNoise = math.saturate(math.pow(fractureBase, 2.5f));
            float fractureMask = math.saturate(fractureNoise * 0.09f + tectonicNetwork * 0.16f + networkNode * 0.10f + provinceRelief * 0.06f + faultMask * 0.48f + secondaryFaultMask * 0.24f + canyon * 0.04f);
            depth += trenchMask * p.TrenchDepthMeters * math.smoothstep(0.24f, 1f, descent01);
            depth += secondaryDepression * 520f * math.smoothstep(0.18f, 0.95f, descent01);
            basinMask = math.max(basinMask, recurringBasin * 0.34f);
            ridgeMask = math.max(ridgeMask, math.max(upliftNetwork * 0.38f, tectonicNetwork * 0.48f));
            depth += basinMask * p.BasinDepthMeters;
            depth -= ridgeMask * p.RidgeHeightMeters * math.smoothstep(0.16f, 0.90f, descent01);
            depth -= ridgeChainHills * math.lerp(350f, 1800f, math.saturate(shelfMask * 0.18f + shelfBreakMask * 0.24f + ridgeMask * 0.58f));
            float regionalBreakup = FractalNoise01(norm * 5.5f + new float2(-4.2f, 12.9f), p.Seed ^ 0x41C64E6Du) * 2f - 1f;
            float swellWarp = FractalNoise01(pos * 0.0001f, p.Seed ^ 0x3B8C1D2Eu) * 1.5f;
            float regionalSwell = math.sin(norm.x * 11.3f + norm.y * 5.7f + swellWarp + HashToUnitFloat(p.Seed ^ 0xDEADBEEFu) * 6.283185f);
            depth += regionalBreakup * 320f * math.saturate(0.25f + descent01 * 0.65f + shelfBreakMask * 0.12f);
            depth += regionalSwell * 170f * math.saturate(0.20f + shelfMask * 0.35f + descent01 * 0.45f);
            depth += provinceRelief * 145f * math.saturate(0.18f + descent01 * 0.50f + shelfBreakMask * 0.18f + ridgeMask * 0.18f);
            depth -= upliftNetwork * math.lerp(260f, 1480f, math.saturate(descent01 * 0.82f + ridgeMask * 0.16f + shelfBreakMask * 0.10f));
            depth -= highlandPlate * math.lerp(120f, 720f, math.saturate(descent01 * 0.75f + shelfBreakMask * 0.12f));
            depth -= shallowProvince * math.lerp(220f, 1850f, math.saturate(descent01 * 0.88f + shelfBreakMask * 0.10f));
            depth -= distributedHighs * math.lerp(180f, 1150f, math.saturate(descent01 * 0.90f + shelfBreakMask * 0.08f + ridgeMask * 0.08f));
            depth += recurringBasin * 430f * math.saturate(0.24f + descent01 * 0.56f);
            float reliefGate = math.saturate(0.22f + shelfBreakMask * 0.24f + ridgeMask * 0.28f + faultMask * 0.24f + canyon * 0.18f + basinMask * 0.08f);
            
            // DOMAIN WARPING: To break the "plastic" value noise look, we perturb the coordinates for high-frequency noise.
            float warpX = (FractalNoise01(norm * 35.0f, p.Seed ^ 0x8A1F3C4Du) * 2f - 1f) * 0.015f;
            float warpZ = (FractalNoise01(norm * 35.0f, p.Seed ^ 0x3B8E1D2Fu) * 2f - 1f) * 0.015f;
            float2 warpedNorm = norm + new float2(warpX, warpZ);

            float macroBreakup = FractalNoise01(warpedNorm * 18.0f + new float2(7.7f, 41.3f), p.Seed ^ 0x91E83B37u) * 2f - 1f;
            float mesoBreakup = FractalNoise01(warpedNorm * 48.0f + new float2(-23.1f, 5.6f), p.Seed ^ 0x6C8E9CF5u) * 2f - 1f;
            float microBreakup = FractalNoise01(warpedNorm * 220.0f + new float2(33.1f, -14.6f), p.Seed ^ 0x1A2B3C4Du) * 2f - 1f;
            
            depth += macroBreakup * 350f * reliefGate;
            depth += mesoBreakup * 180f * math.saturate(reliefGate + shelfMask * 0.12f + shelfBreakMask * 0.16f);
            float microBreakupWeight = math.saturate(ridgeMask * 0.8f + faultMask * 0.5f + reliefGate * 0.3f);
            depth += microBreakup * math.lerp(15f, 95f, microBreakupWeight);
            depth += fractureMask * 120f;

            // MESO/MICRO DETAIL PASS
            // Add sharp, high-frequency noise (scales ~60m down to ~4m) specifically to hard rock to break up "plastic" look.
            // Frequency 150.0/extent = 150/30000 = 0.005 (200m base scale). High octaves will reach ~12m.
            float rockDetailNoise = (FractalNoise01(warpedNorm * 150.0f + new float2(-44.2f, 88.1f), p.Seed ^ 0x7B9C1A2Fu) * 2f - 1f);
            // Add a sharper ridge-like noise for rocky outcrops
            float rockyRidgeDetail = 1f - 2f * math.abs(FractalNoise01(warpedNorm * 320.0f + new float2(11.4f, -99.3f), p.Seed ^ 0x5E8A9C1Du) - 0.5f);
            
            float hardRockExposure = math.saturate(ridgeMask * 0.45f + faultMask * 0.28f + math.saturate(descent01 * 1.5f) * 0.30f);
            float mesoDetailWeight = math.saturate(hardRockExposure * 0.8f + reliefGate * 0.3f);
            
            depth += rockDetailNoise * 70f * mesoDetailWeight;
            depth -= rockyRidgeDetail * 60f * mesoDetailWeight * math.saturate(descent01); // Sharp extrusions on ridges

            if (depth < -260f)
                depth = -260f + (depth + 260f) * 0.42f;
            depth = math.clamp(depth, -620f, p.HadalDepthMeters);

            masks = new MacroMasks
            {
                Shelf = math.saturate(shelfMask),
                ShelfBreak = math.saturate(shelfBreakMask),
                Ridge = math.saturate(ridgeMask),
                Trench = math.saturate(trenchMask),
                Basin = math.saturate(basinMask),
                Fault = math.saturate(fractureMask)
            };
            return p.WaterSurfaceY - depth;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float EllipseMask(float2 position, float2 center, float2 radii, float rotationRadians)
        {
            float s = math.sin(rotationRadians);
            float c = math.cos(rotationRadians);
            float2 d = position - center;
            float2 r = new float2(d.x * c - d.y * s, d.x * s + d.y * c) / math.max(new float2(1f, 1f), radii);
            float distance = math.length(r);
            return 1f - math.smoothstep(0.28f, 1f, distance);
        }

        private static float CellularEdge01(float2 sample, uint seed)
        {
            int2 baseCell = (int2)math.floor(sample);
            float first = float.MaxValue;
            float second = float.MaxValue;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int2 cell = baseCell + new int2(dx, dz);
                    float2 feature = new float2(cell.x, cell.y) + Hash2(cell.x, cell.y, seed);
                    float dist = math.length(sample - feature);
                    if (dist < first)
                    {
                        second = first;
                        first = dist;
                    }
                    else if (dist < second)
                    {
                        second = dist;
                    }
                }
            }

            return 1f - math.smoothstep(0.04f, 0.42f, math.max(0f, second - first));
        }

        private static float FractalNoise01(float2 sample, uint seed)
        {
            float amplitude = 0.5f;
            float frequency = 1f;
            float total = 0f;
            float norm = 0f;
            for (int octave = 0; octave < 5; octave++)
            {
                total += ValueNoise01(sample * frequency, seed + (uint)octave * 0x9E3779B9u) * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.02f;
            }

            return total / math.max(0.0001f, norm);
        }

        private static float ValueNoise01(float2 sample, uint seed)
        {
            float2 floorSample = math.floor(sample);
            int2 cell = (int2)floorSample;
            float2 local = sample - floorSample;
            float2 smooth = local * local * (3f - 2f * local);
            float a = Hash01(cell.x, cell.y, seed);
            float b = Hash01(cell.x + 1, cell.y, seed);
            float c = Hash01(cell.x, cell.y + 1, seed);
            float d = Hash01(cell.x + 1, cell.y + 1, seed);
            return math.lerp(math.lerp(a, b, smooth.x), math.lerp(c, d, smooth.x), smooth.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ZigZag32(int value)
        {
            return (uint)((value << 1) ^ (value >> 31));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Mix64(ulong value)
        {
            value ^= value >> 30;
            value *= 0xBF58476D1CE4E5B9ul;
            value ^= value >> 27;
            value *= 0x94D049BB133111EBul;
            value ^= value >> 31;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MixHash(ref uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 Hash2(int x, int y, uint seed)
        {
            return new float2(Hash01(x, y, seed), Hash01(x, y, seed ^ 0xA511E9B3u));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Hash01(int x, int y, uint seed)
        {
            return (Hash(x, y, (int)seed) & 0x00FFFFFFu) * (1f / 16777215f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float HashToUnitFloat(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) * (1f / 16777215f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash(int x, int y, int seed)
        {
            uint hash = (uint)x * 0x8DA6B343u;
            hash ^= (uint)y * 0xD8163841u;
            hash ^= (uint)seed + 0x9E3779B9u + (hash << 6) + (hash >> 2);
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FastSqrtPositive(float value)
        {
            return math.sqrt(math.max(0f, value));
        }

        private struct MacroMasks
        {
            public float Shelf;
            public float ShelfBreak;
            public float Ridge;
            public float Trench;
            public float Basin;
            public float Fault;
        }
    }
}
