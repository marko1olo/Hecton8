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
        public int BenchmarkStage;

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
        public float PositiveCurvature01;
        public float NegativeCurvature01;
        public float ErosionFlow01;
        public float TerraceMask;
        public float SlumpScarMask;
        public float TributaryCanyonMask;
        public float NodulePlainMask;
        public float ReefEligibilityMask;
        public float HardRockExposureMask;
        public float VoxelSeamMask;
        public float CraterMask;
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
        public const uint ArtifactVersion = 10u;

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
            sanitized.DetailProbeMeters = math.max(1f, source.DetailProbeMeters);
            return math.isfinite(sanitized.WorldExtentMeters) &&
                   math.isfinite(sanitized.ChunkSizeMeters) &&
                   math.isfinite(sanitized.WaterSurfaceY);
        }

        public static WorldMacroGeologySample Evaluate(float absoluteX, float absoluteZ, in WorldMacroGeologyParams parameters)
        {
            if (!TrySanitizeParams(in parameters, out WorldMacroGeologyParams p))
                return default;

            DifferentialSample differential = EvaluateDifferentials(absoluteX, absoluteZ, in p, out MacroMasks masks);
            return BuildSample(
                absoluteX,
                absoluteZ,
                in p,
                differential.HeightMeters,
                differential.Slope01,
                differential.Curvature01,
                differential.PositiveCurvature01,
                differential.NegativeCurvature01,
                in masks);
        }

        public static WorldMacroGeologySample EvaluateWithCachedDifferentials(
            float absoluteX,
            float absoluteZ,
            in WorldMacroGeologyParams parameters,
            float heightMeters,
            float slope01,
            float curvature01,
            float positiveCurvature01,
            float negativeCurvature01)
        {
            if (!TrySanitizeParams(in parameters, out WorldMacroGeologyParams p))
                return default;

            float evaluatedHeightMeters = EvaluateHeightMeters(absoluteX, absoluteZ, in p, out MacroMasks masks);
            float resolvedHeightMeters = math.isfinite(heightMeters) ? heightMeters : evaluatedHeightMeters;
            return BuildSample(
                absoluteX,
                absoluteZ,
                in p,
                resolvedHeightMeters,
                math.saturate(slope01),
                math.saturate(curvature01),
                math.saturate(positiveCurvature01),
                math.saturate(negativeCurvature01),
                in masks);
        }

        /// <summary>
        /// Single-pass evaluation: height + 4 probe differentials + full sample construction.
        /// Keeps MacroMasks private. Use this from Burst jobs instead of calling private overloads directly.
        /// 5 calls to EvaluateHeightMeters: center (with masks) + 4 probes (masks discarded).
        /// </summary>
        public static WorldMacroGeologySample EvaluateSinglePass(
            float absoluteX,
            float absoluteZ,
            in WorldMacroGeologyParams parameters)
        {
            if (!TrySanitizeParams(in parameters, out WorldMacroGeologyParams p))
                return default;

            float probe = math.max(1f, p.DetailProbeMeters);
            float heightC = EvaluateHeightMeters(absoluteX, absoluteZ, in p, out MacroMasks masks);
            float west    = EvaluateHeightMeters(absoluteX - probe, absoluteZ, in p, out _);
            float east    = EvaluateHeightMeters(absoluteX + probe, absoluteZ, in p, out _);
            float south   = EvaluateHeightMeters(absoluteX, absoluteZ - probe, in p, out _);
            float north   = EvaluateHeightMeters(absoluteX, absoluteZ + probe, in p, out _);

            float safeProbe = math.max(0.001f, probe);
            float dx = (east - west) / (safeProbe * 2f);
            float dz = (north - south) / (safeProbe * 2f);
            float slope = FastSqrtPositive(dx * dx + dz * dz);
            float curvature = (west + east + south + north - heightC * 4f) / math.max(0.001f, safeProbe * safeProbe);

            return BuildSample(
                absoluteX,
                absoluteZ,
                in p,
                heightC,
                math.saturate(slope / 1.25f),
                math.saturate(math.abs(curvature) * 280f),
                math.saturate(math.max(0f, curvature) * 280f),
                math.saturate(math.max(0f, -curvature) * 280f),
                in masks);
        }

        public static WorldMacroGeologySample BuildSample(
            float absoluteX,
            float absoluteZ,
            in WorldMacroGeologyParams p,
            float heightMeters,
            float slope01,
            float curvature01,
            float positiveCurvature01,
            float negativeCurvature01,
            in MacroMasks masks)
        {
            float basinFlow = math.saturate(masks.Basin * 0.42f + masks.ShelfBreak * 0.24f + masks.Canyon * 0.36f + (1f - slope01) * 0.12f);
            float faultFlow = math.saturate(masks.Fault * 0.36f + masks.Trench * 0.32f + masks.PlateEdge * 0.24f);
            float erosionVeins = RidgedMultifractal01(new float2(absoluteX, absoluteZ) * 0.00042f + new float2(13.1f, -8.4f), p.Seed ^ 0xA511E9B3u, 4);
            float erosionFlow = math.saturate(basinFlow + faultFlow + erosionVeins * masks.Canyon * 0.22f);
            float sediment = math.saturate((1f - slope01) * 0.50f + negativeCurvature01 * 0.28f + masks.Basin * 0.36f + masks.Shelf * 0.14f + masks.Canyon * 0.18f - masks.Ridge * 0.30f - masks.HardRock * 0.34f - masks.Trench * 0.12f);
            float seep = math.saturate(masks.Fault * 0.46f + masks.PlateEdge * 0.22f + masks.Basin * 0.20f + masks.Trench * 0.20f);
            float deepPlain01 = math.saturate((p.WaterSurfaceY - heightMeters - 1600f) / 1800f);
            float shallowReefBand01 = math.saturate(1f - math.abs(p.WaterSurfaceY - heightMeters - 90f) / 520f);

            WorldMacroGeologySample sample = new WorldMacroGeologySample
            {
                HeightMeters = heightMeters,
                DepthMeters = math.max(0f, p.WaterSurfaceY - heightMeters),
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
                PositiveCurvature01 = positiveCurvature01,
                NegativeCurvature01 = negativeCurvature01,
                ErosionFlow01 = erosionFlow,
                TerraceMask = math.saturate(masks.Terrace * 0.52f + masks.ShelfBreak * 0.18f + positiveCurvature01 * 0.18f + erosionFlow * 0.10f - masks.Trench * 0.12f),
                SlumpScarMask = math.saturate(masks.Slump * 0.42f + masks.ShelfBreak * 0.22f + negativeCurvature01 * 0.36f + slope01 * 0.18f - masks.Ridge * 0.12f),
                TributaryCanyonMask = math.saturate(masks.Canyon * 0.58f + erosionFlow * 0.30f + masks.Fault * 0.18f + negativeCurvature01 * 0.22f),
                NodulePlainMask = math.saturate(sediment * 0.46f + (1f - slope01) * 0.26f + deepPlain01 * 0.28f - masks.Ridge * 0.34f - masks.Trench * 0.22f),
                ReefEligibilityMask = math.saturate(masks.Shelf * 0.52f + shallowReefBand01 * 0.34f + (1f - slope01) * 0.18f - masks.Trench * 0.25f - masks.HardRock * 0.12f),
                HardRockExposureMask = math.saturate(masks.HardRock * 0.56f + masks.Ridge * 0.30f + masks.Fault * 0.24f + slope01 * 0.22f + positiveCurvature01 * 0.14f - sediment * 0.22f),
                VoxelSeamMask = math.saturate(masks.Fault * 0.34f + masks.PlateEdge * 0.22f + curvature01 * 0.34f + slope01 * 0.20f + masks.Trench * 0.16f),
                CraterMask = masks.Crater
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

        public static float EvaluateHeightMeters(float absoluteX, float absoluteZ, in WorldMacroGeologyParams p, out MacroMasks masks)
        {
            float extent = math.max(MinimumWorldExtentMeters, p.WorldExtentMeters);
            float2 pos = new float2(absoluteX, absoluteZ);
            float2 norm = pos / extent;

            // TIER 1: low-frequency tectonic domain warp and F1/F2 cellular plate solve.
            float2 tectonicWarp = new float2(
                FractalSimplexNoise01(norm * 1.55f + new float2(11.7f, -3.9f), p.Seed ^ 0xB5297A4Du) * 2f - 1f,
                FractalSimplexNoise01(norm * 1.45f + new float2(-2.1f, 8.6f), p.Seed ^ 0x4CF5AD43u) * 2f - 1f) * 4500f;
            float2 mesoWarp = new float2(
                FractalSimplexNoise01(norm * 7.5f + new float2(-17.2f, 29.3f), p.Seed ^ 0x68E31DA4u) * 2f - 1f,
                FractalSimplexNoise01(norm * 8.1f + new float2(23.5f, -19.7f), p.Seed ^ 0x8A1F3C4Du) * 2f - 1f) * 520f;
            float2 warpedPos = pos + tectonicWarp + mesoWarp;
            float2 warpedNorm = warpedPos / extent;

            float2 plateSample = warpedPos / 12000f;
            int2 plateBase = (int2)math.floor(plateSample);
            float plateF1Sq = float.MaxValue;
            float plateF2Sq = float.MaxValue;
            int2 nearestPlateCell = plateBase;
            float2 nearestPlateHash = new float2(0.5f, 0.5f);

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int2 cell = plateBase + new int2(dx, dz);
                    float2 feature = new float2(cell.x, cell.y) + Hash2(cell.x, cell.y, p.Seed ^ 0x5EAF1D7Bu);
                    float distSq = math.lengthsq(plateSample - feature);
                    if (distSq < plateF1Sq)
                    {
                        plateF2Sq = plateF1Sq;
                        plateF1Sq = distSq;
                        nearestPlateCell = cell;
                        nearestPlateHash = feature - new float2(cell.x, cell.y);
                    }
                    else if (distSq < plateF2Sq)
                    {
                        plateF2Sq = distSq;
                    }
                }
            }
            
            float plateF1 = math.sqrt(plateF1Sq);
            float plateF2 = math.sqrt(plateF2Sq);

            float plateEdgeDelta = math.max(0f, plateF2 - plateF1);
            float plateEdgeMask = 1f - math.smoothstep(0.035f, 0.28f, plateEdgeDelta);
            float plateInterior = 1f - math.smoothstep(0.08f, 0.62f, plateF1);
            float boundaryPolarity = HashToUnitFloat(Hash(nearestPlateCell.x, nearestPlateCell.y, unchecked((int)(p.Seed ^ 0xA77D3F19u))));
            float ridgePolarity = math.smoothstep(0.36f, 0.70f, boundaryPolarity);
            float trenchPolarity = 1f - math.smoothstep(0.30f, 0.64f, boundaryPolarity);
            float jaggedBoundary = RidgedMultifractal01(warpedPos * 0.00018f + nearestPlateHash * 9.7f, p.Seed ^ 0xD1F123BBu, 5);
            float plateRidgeMask = plateEdgeMask * ridgePolarity * math.smoothstep(0.24f, 0.78f, jaggedBoundary);
            float plateTrenchMask = plateEdgeMask * trenchPolarity * math.smoothstep(0.18f, 0.72f, 1f - jaggedBoundary * 0.55f + plateEdgeMask * 0.45f);

            // TIER 2: shelf, abyss, ridged mountains, and sediment-filled dendritic canyon erosion.
            float continentNoise = FractalSimplexNoise01(warpedNorm * 2.65f + new float2(0.17f, -0.41f), p.Seed ^ 0x12345678u);
            float shelfMask = math.smoothstep(0.38f, 0.66f, continentNoise);
            float shelfBreakMask = (1f - math.saturate(math.abs(continentNoise - 0.51f) * 5.7f)) * (0.62f + plateEdgeMask * 0.38f);
            shelfBreakMask = math.saturate(shelfBreakMask);
            float abyssPlainMask = math.saturate((1f - shelfMask) * (1f - plateEdgeMask * 0.85f) * (0.42f + plateInterior * 0.58f));

            float shelfToe = math.saturate(math.smoothstep(0.16f, 0.72f, shelfBreakMask) * (1f - shelfMask * 0.25f));
            float depth = math.lerp(p.AbyssDepthMeters, p.ShelfDepthMeters, shelfMask);
            depth += abyssPlainMask * p.BasinDepthMeters * 0.35f;

            // DOMAIN WARPING GEOLOGY INJECTION
            float geologicalNoise = FractalSimplexNoise01(warpedPos * 0.00045f + new float2(4.2f, -1.8f), p.Seed ^ 0x5D4E3C2Bu, 6);
            depth += (geologicalNoise - 0.5f) * 180f * (1f - abyssPlainMask * 0.5f); // Abyssal plain is flatter

            float ridgeBelt = RidgedMultifractal01(warpedNorm * 7.2f + new float2(4.1f, -3.7f), p.Seed ^ 0x91E83B37u, 5);
            float ridgeMask = math.saturate(math.smoothstep(0.38f, 0.86f, ridgeBelt) * (1f - shelfMask * 0.42f) + plateRidgeMask * 0.95f);
            
            // RIDGED MULTIFRACTAL SHARPENING
            float sharpRidges = RidgedMultifractal01(warpedPos * 0.0022f + new float2(-1.2f, 8.4f), p.Seed ^ 0x3F2A1C9Bu, 6);
            depth -= sharpRidges * p.RidgeHeightMeters * 0.45f * ridgeMask;
            depth -= ridgeMask * p.RidgeHeightMeters * (0.58f + plateEdgeMask * 0.42f);

            float trenchBelt = RidgedMultifractal01(warpedNorm * 6.1f + new float2(0.4f, -0.6f), p.Seed ^ 0x4B3A2C1Du, 4);
            float trenchMask = math.saturate(math.smoothstep(0.56f, 0.95f, trenchBelt) * (1f - shelfMask * 0.80f) + plateTrenchMask * 1.15f);
            depth += trenchMask * p.TrenchDepthMeters * (0.78f + plateEdgeMask * 0.58f);

            float faultNoise = RidgedMultifractal01(warpedNorm * 12.0f + new float2(-1.9f, 7.1f), p.Seed ^ 0xCA97D1F3u, 3);
            float faultMask = math.saturate(math.smoothstep(0.48f, 0.88f, faultNoise) * (1f - shelfMask * 0.45f) + plateEdgeMask * 0.34f);
            depth += faultMask * 95f;

            float basinMask = math.saturate((1f - shelfMask) * (1f - ridgeMask * 0.78f) * (1f - trenchMask * 0.52f));
            depth += basinMask * p.BasinDepthMeters * (0.54f + abyssPlainMask * 0.46f);

            float canyonWarp = FractalSimplexNoise01(warpedPos * 0.00011f + new float2(31.4f, -9.2f), p.Seed ^ 0x0CA14405u) * 2f - 1f;
            float dendritic = RidgedMultifractal01(warpedPos * 0.00062f + new float2(canyonWarp * 1.7f, -canyonWarp * 1.1f), p.Seed ^ 0x6DCD4A37u, 6);
            float canyonRim = math.smoothstep(0.54f, 0.86f, dendritic);
            float canyonFloor = math.smoothstep(0.80f, 0.96f, dendritic);
            float canyonMask = math.saturate(canyonRim * (shelfToe * 1.5f + shelfMask * 0.6f + faultMask * 0.8f + 0.1f));
            float uShapedCut = math.lerp(canyonRim * canyonRim, 0.68f + canyonFloor * 0.12f, canyonFloor * 0.72f);
            
            // RIDGED MULTIFRACTAL V-SHAPED CANYON
            float canyonCut = RidgedMultifractal01(warpedPos * 0.0018f + new float2(9.5f, -3.1f), p.Seed ^ 0x8A4B2C1Du, 6);
            depth += canyonCut * 350f * canyonMask * canyonFloor;
            depth += canyonMask * uShapedCut * math.lerp(260f, 980f, shelfToe);

            if (p.BenchmarkStage == 2) { masks = default; return depth; }

            // TIER 2B: volcanic seamounts and guyots.
            float2 seamountSample = warpedPos * 0.00030f;
            int2 seamountCell = (int2)math.floor(seamountSample);
            float2 seamountFrac = seamountSample - math.floor(seamountSample);
            float seamountDist = 8f;
            float2 seamountVector = new float2(0f, 0f);
            int2 seamountId = seamountCell;

            for (int sy = -1; sy <= 1; sy++)
            {
                for (int sx = -1; sx <= 1; sx++)
                {
                    int2 cell = seamountCell + new int2(sx, sy);
                    float2 hash = Hash2(cell.x, cell.y, p.Seed ^ 0x5EA30447u);
                    float2 diff = new float2(sx, sy) + hash - seamountFrac;
                    float dist = math.length(diff);
                    if (dist < seamountDist)
                    {
                        seamountDist = dist;
                        seamountVector = diff;
                        seamountId = cell;
                    }
                }
            }

            float seamountProfile = math.saturate(1f - seamountDist * 1.85f);
            if (seamountProfile > 0f)
            {
                float cone = math.exp(-seamountDist * 5.4f);
                float guyotChance = HashToUnitFloat(Hash(seamountId.x, seamountId.y, unchecked((int)(p.Seed ^ 0xCA7715A3u))));
                float guyotCap = math.lerp(cone, math.min(cone, 0.43f), math.smoothstep(0.48f, 0.72f, guyotChance));
                float caldera = (1f - math.smoothstep(0.0f, 0.055f, seamountDist)) * (1f - math.smoothstep(0.72f, 0.88f, guyotChance));
                float2 radialDir = SafeNormalize(seamountVector, new float2(1f, 0f));
                float gully = 1f - math.abs(FractalSimplexNoise01(radialDir * 4.1f + warpedPos * 0.00052f, p.Seed ^ 0x0901177Au) * 2f - 1f);
                gully = math.pow(gully, 3.2f) * math.smoothstep(0.06f, 0.28f, seamountDist) * (1f - math.smoothstep(0.32f, 0.54f, seamountDist));
                float volcanicRelief = math.saturate(guyotCap - caldera * 0.26f - gully * 0.13f);
                depth -= volcanicRelief * basinMask * 2450f;
                ridgeMask = math.max(ridgeMask, volcanicRelief * 0.52f);
            }

            if (p.BenchmarkStage == 3) { masks = default; return depth; }

            // TIER 3: eroded strata with pre-quantization erosion injection.
            float slopeProxy = math.saturate(shelfBreakMask * 0.82f + ridgeMask * 0.72f + faultMask * 0.65f + canyonMask * 0.48f + plateEdgeMask * 0.40f);
            float moderateSlopeMask = math.smoothstep(0.08f, 0.45f, slopeProxy) * (1f - math.smoothstep(0.65f, 0.98f, slopeProxy));
            float terraceMask = math.saturate(moderateSlopeMask * (shelfBreakMask * 1.2f + ridgeMask * 0.60f + faultMask * 0.58f + 0.1f));
            if (terraceMask > 0.025f)
            {
                float dynamicTerraceScale = math.lerp(78f, 185f, FractalSimplexNoise01(warpedNorm * 3.0f, p.Seed ^ 0x00112233u));
                float2 tilt = SafeNormalize(new float2(
                    FractalSimplexNoise01(warpedNorm * 1.8f, p.Seed ^ 0xAB12CD34u) * 2f - 1f,
                    FractalSimplexNoise01(warpedNorm * 1.8f, p.Seed ^ 0x56EF78ABu) * 2f - 1f), new float2(0.7071f, 0.7071f));
                float strataTilt = math.dot(tilt, pos) * 0.045f;
                float terraceErosion = (FractalSimplexNoise01(warpedNorm * 350f + new float2(6.1f, -4.7f), p.Seed ^ 0x77CC4411u) * 2f - 1f) * 25f;
                float broadErosion = (FractalSimplexNoise01(warpedNorm * 72f + new float2(-9.4f, 2.8f), p.Seed ^ 0x99AA88BBu) * 2f - 1f) * 42f;
                float hPhase = (depth + strataTilt + terraceErosion + broadErosion) / dynamicTerraceScale;
                float fStep = math.frac(hPhase);
                float sStep = math.smoothstep(0.15f, 0.45f, fStep);
                float roundedCoord = (math.floor(hPhase) + sStep) * dynamicTerraceScale - terraceErosion - broadErosion - strataTilt;
                float patch = math.smoothstep(0.58f, 0.91f, FractalSimplexNoise01(warpedNorm * 4.6f, p.Seed ^ 0x992211AAu));
                depth = math.lerp(depth, roundedCoord, terraceMask * patch * 0.52f);
                terraceMask *= patch;
            }

            // TIER 4: concave toe talus.
            float concaveToe = math.saturate((basinMask * 1.5f + canyonMask * 1.2f + shelfToe * 0.84f + 0.1f) * (ridgeMask * 0.75f + faultMask * 0.62f + shelfBreakMask * 0.66f + 0.1f));
            float talusMask = math.saturate(math.smoothstep(0.10f, 0.62f, slopeProxy) * (1f - math.smoothstep(0.74f, 0.98f, slopeProxy)) * concaveToe);
            float talusC = RidgedMultifractal01(warpedPos * 0.020f + new float2(3.3f, -7.2f), p.Seed ^ 0xE70D1A5Bu, 3);
            float talusF = RidgedMultifractal01(warpedPos * 0.071f + new float2(-5.0f, 1.7f), p.Seed ^ 0xC3F19802u, 2);
            float talusRubble = ((talusC * 0.70f + talusF * 0.30f) * 2f - 1f) * math.lerp(5f, 15f, talusMask);
            depth += talusRubble * talusMask;

            // TIER 5: KCC collision grit vs sediment ripples.
            float hardRockMask = math.saturate(ridgeMask * 0.48f + faultMask * 0.30f + plateEdgeMask * 0.18f + slopeProxy * 0.28f - basinMask * 0.18f - canyonFloor * canyonMask * 0.12f);
            float gritGate = math.smoothstep(0.50f, 0.72f, hardRockMask);
            float gritA = RidgedMultifractal01(warpedPos * 0.42f, p.Seed ^ 0x2F6A11C9u, 3);
            float gritB = RidgedMultifractal01(warpedPos * 0.92f + new float2(12.1f, -4.2f), p.Seed ^ 0x7193B5EDu, 2);
            depth += ((gritA * 0.68f + gritB * 0.32f) * 2f - 1f) * math.lerp(1.1f, 3.2f, gritGate) * gritGate;
            float sedimentMask = math.saturate((1f - hardRockMask) * (basinMask * 0.38f + shelfMask * 0.28f + canyonMask * 0.24f + (1f - slopeProxy) * 0.20f));

            // Subordinate pockmarks and impact scars.
            if (sedimentMask > 0.001f)
            {
                float2 pitHash;
                float pitDist = CellularDistance01(warpedPos * 0.012f, p.Seed ^ 0xF131A21Eu, out pitHash);
                float pitDistRaw = math.saturate(1f - pitDist * 3f);
                float pitProfile = (pitDistRaw * pitDistRaw) * math.sqrt(pitDistRaw);
                if (pitProfile > 0.001f)
                {
                    float pitFieldMask = math.smoothstep(0.52f, 0.72f, FractalSimplexNoise01(warpedPos * 0.0008f, p.Seed ^ 0x99BBE211u));
                    depth += pitProfile * pitFieldMask * sedimentMask * 6f;
                }
            }

            float craterDepthDelta = 0f;
            float craterMask = 0f;
            float craterGridSize = 2000f;
            int2 craterCell = new int2((int)math.floor(warpedPos.x / craterGridSize), (int)math.floor(warpedPos.y / craterGridSize));
            for (int cdz = -1; cdz <= 1; cdz++)
            {
                for (int cdx = -1; cdx <= 1; cdx++)
                {
                    int2 neighbor = craterCell + new int2(cdx, cdz);
                    uint h = Hash(neighbor.x, neighbor.y, unchecked((int)(p.Seed ^ 0x9B3A21EFu)));
                    if (HashToUnitFloat(h ^ 0x12345678u) > 0.08f)
                        continue;

                    float cx = (neighbor.x + HashToUnitFloat(h ^ 0x87654321u)) * craterGridSize;
                    float cz = (neighbor.y + HashToUnitFloat(h ^ 0xA1B2C3D4u)) * craterGridSize;
                    float radius = math.lerp(120f, 900f, math.pow(HashToUnitFloat(h ^ 0x1A2B3C4Du), 2.3f));
                    float dist = math.length(new float2(warpedPos.x - cx, warpedPos.y - cz));
                    if (dist > radius * 2f)
                        continue;

                    float rimWarp = (FractalSimplexNoise01(warpedPos * 0.015f, h ^ 0xDEADBEEFu) * 2f - 1f) * 0.07f;
                    float normalizedDist = dist / math.max(1f, radius) + rimWarp;
                    float bowl = math.pow(1f - math.smoothstep(0f, 1f, normalizedDist), 1.55f);
                    float rim = math.smoothstep(0f, 1f, math.max(0f, 1f - math.abs(normalizedDist - 1f) * 2.6f));
                    float peakRadius = radius * 0.15f;
                    float peak = math.smoothstep(0f, 1f, 1f - math.smoothstep(0f, peakRadius, dist)) * math.smoothstep(520f, 860f, radius) * 0.34f;
                    if (rim > 0.001f)
                    {
                        float angle = math.atan2(warpedPos.y - cz, warpedPos.x - cx);
                        rim *= 0.42f + FractalSimplexNoise01(new float2(angle * 4f, radius), h ^ 0xDEADBEEFu) * 0.58f;
                    }
                    craterDepthDelta += bowl * radius * 0.16f;
                    craterDepthDelta -= peak * radius * 0.09f;
                    craterDepthDelta -= rim * radius * 0.07f;
                    craterMask = math.max(craterMask, bowl);
                }
            }
            depth += craterDepthDelta;

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
                Fault = math.saturate(faultMask),
                Crater = math.saturate(craterMask),
                Canyon = math.saturate(canyonMask),
                HardRock = math.saturate(hardRockMask),
                PlateEdge = math.saturate(plateEdgeMask),
                Terrace = math.saturate(terraceMask),
                Slump = math.saturate(talusMask + canyonFloor * canyonMask * 0.36f + shelfToe * 0.18f)
            };
            return p.WaterSurfaceY - depth;
        }

        private static DifferentialSample EvaluateDifferentials(float absoluteX, float absoluteZ, in WorldMacroGeologyParams p, out MacroMasks masks)
        {
            float height = EvaluateHeightMeters(absoluteX, absoluteZ, in p, out masks);
            float probe = math.max(1f, p.DetailProbeMeters);
            float west = EvaluateHeightMeters(absoluteX - probe, absoluteZ, in p, out _);
            float east = EvaluateHeightMeters(absoluteX + probe, absoluteZ, in p, out _);
            float south = EvaluateHeightMeters(absoluteX, absoluteZ - probe, in p, out _);
            float north = EvaluateHeightMeters(absoluteX, absoluteZ + probe, in p, out _);
            float safeProbe = math.max(0.001f, probe);
            float dx = (east - west) / (safeProbe * 2f);
            float dz = (north - south) / (safeProbe * 2f);
            float slope = FastSqrtPositive(dx * dx + dz * dz);
            float curvature = (west + east + south + north - height * 4f) / math.max(0.001f, safeProbe * safeProbe);
            return new DifferentialSample
            {
                HeightMeters = height,
                Slope = slope,
                Slope01 = math.saturate(slope / 1.25f),
                Curvature = curvature,
                Curvature01 = math.saturate(math.abs(curvature) * 280f),
                PositiveCurvature01 = math.saturate(math.max(0f, curvature) * 280f),
                NegativeCurvature01 = math.saturate(math.max(0f, -curvature) * 280f)
            };
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CellularDistance01(float2 pos, uint seed, out float2 cellHash)
        {
            float2 cell = math.floor(pos);
            float2 frac = pos - cell;
            
            float minDistSq = 64.0f; // 8 * 8
            cellHash = new float2(0, 0);

            int cx = frac.x < 0.5f ? -1 : 1;
            int cy = frac.y < 0.5f ? -1 : 1;

            for (int i = 0; i < 4; i++)
            {
                int2 neighbor = new int2((i & 1) * cx, (i >> 1) * cy);
                float2 pointHash = Hash2((int)(cell.x + neighbor.x), (int)(cell.y + neighbor.y), seed);
                float2 diff = new float2(neighbor.x, neighbor.y) + pointHash - frac;
                float distSq = math.lengthsq(diff);

                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    cellHash = pointHash;
                }
            }

            return math.saturate(math.sqrt(minDistSq));
        }

        private static float CellularEdge01(float2 sample, uint seed)
        {
            int2 baseCell = (int2)math.floor(sample);
            float firstSq = float.MaxValue;
            float secondSq = float.MaxValue;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int2 cell = baseCell + new int2(dx, dz);
                    float2 feature = new float2(cell.x, cell.y) + Hash2(cell.x, cell.y, seed);
                    float distSq = math.lengthsq(sample - feature);
                    if (distSq < firstSq)
                    {
                        secondSq = firstSq;
                        firstSq = distSq;
                    }
                    else if (distSq < secondSq)
                    {
                        secondSq = distSq;
                    }
                }
            }

            return 1f - math.smoothstep(0.04f, 0.42f, math.max(0f, math.sqrt(secondSq) - math.sqrt(firstSq)));
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

        public static float FractalSimplexNoise01(float2 sample, uint seed, int octaves = 5, float filterWidth = 0f, float domainScale = 1f)
        {
            float amplitude = 0.5f;
            float frequency = 1f;
            float total = 0f;
            float norm = 0f;
            float2x2 rot = new float2x2(-0.7373688f, -0.6754903f, 0.6754903f, -0.7373688f);
            float2 p = sample;
            for (int octave = 0; octave < octaves; octave++)
            {
                if (filterWidth > 0f && octave > 0 && (domainScale / frequency) < filterWidth)
                    break;
                total += SimplexNoise01(p * frequency, seed + (uint)octave * 0x9E3779B9u) * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.02f;
                p = math.mul(rot, p);
            }

            return total / math.max(0.0001f, norm);
        }

        public static float RidgedMultifractal01(float2 sample, uint seed, int octaves = 5)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float total = 0f;
            float norm = 0f;
            float weight = 1f; // weight successive octaves by previous
            float2x2 rot = new float2x2(-0.7373688f, -0.6754903f, 0.6754903f, -0.7373688f);
            float2 p = sample;
            for (int octave = 0; octave < octaves; octave++)
            {
                uint layerSeed = seed + (uint)octave * 0x9E3779B9u;
                float seedOffsetX = HashToUnitFloat(layerSeed ^ 0x9E3779B9u) * 200000f - 100000f;
                float seedOffsetY = HashToUnitFloat(layerSeed ^ 0x334EAA71u) * 200000f - 100000f;
                float snoiseVal = noise.snoise(p * frequency + new float2(seedOffsetX, seedOffsetY));
                
                // Ridged inversion: 1 - abs(snoiseVal)
                float n = 1f - math.abs(snoiseVal);
                n = n * n; // sharpen ridges
                n *= weight;
                weight = math.saturate(n * 2f);
                
                total += n * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.0f;
                p = math.mul(rot, p);
            }

            return total / math.max(0.0001f, norm);
        }

        private static float SimplexNoise01(float2 sample, uint seed)
        {
            // Offset is bounded to ±8.0 so float32 precision is preserved when sample
            // is in the normalised/frequency-scaled domain (typically 0..10).
            // 200000×±1 would destroy mantissa bits for large-world coordinates.
            float seedOffsetX = HashToUnitFloat(seed ^ 0x9E3779B9u) * 16f - 8f;
            float seedOffsetY = HashToUnitFloat(seed ^ 0x334EAA71u) * 16f - 8f;
            float n = noise.snoise(sample + new float2(seedOffsetX, seedOffsetY));
            return n * 0.5f + 0.5f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 HashGradient(float2 p, uint seed)
        {
            uint h = Hash((int)p.x, (int)p.y, (int)seed);
            float angle = HashToUnitFloat(h) * 6.283185f;
            return new float2(math.cos(angle), math.sin(angle));
        }

        private static float ValueNoise01(float2 sample, uint seed)
        {
            float seedOffsetX = HashToUnitFloat(seed ^ 0x61C88647u) * 16f - 8f;
            float seedOffsetY = HashToUnitFloat(seed ^ 0xC2B2AE35u) * 16f - 8f;
            float n = noise.snoise(sample + new float2(seedOffsetX, seedOffsetY));
            return n * 0.5f + 0.5f;
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
        public static float HashToUnitFloat(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) * (1f / 16777215f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Hash(int x, int y, int seed)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 SafeNormalize(float2 value, float2 fallback)
        {
            float lenSq = math.lengthsq(value);
            if (lenSq <= 0.000001f || !math.isfinite(lenSq))
                return fallback;

            return value * math.rsqrt(lenSq);
        }

        private struct DifferentialSample
        {
            public float HeightMeters;
            public float Slope;
            public float Slope01;
            public float Curvature;
            public float Curvature01;
            public float PositiveCurvature01;
            public float NegativeCurvature01;
        }

        public struct MacroMasks
        {
            public float Shelf;
            public float ShelfBreak;
            public float Ridge;
            public float Trench;
            public float Basin;
            public float Fault;
            public float Crater;
            public float Canyon;
            public float HardRock;
            public float PlateEdge;
            public float Terrace;
            public float Slump;
        }
    }
}

