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
        public const uint ArtifactVersion = 7u;

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
                VoxelSeamMask = math.saturate(masks.Fault * 0.35f + curvature01 * 0.40f + slope01 * 0.22f + masks.Trench * 0.16f),
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

        private static float EvaluateHeightMeters(float absoluteX, float absoluteZ, in WorldMacroGeologyParams p, out MacroMasks masks)
        {
            float extent = math.max(MinimumWorldExtentMeters, p.WorldExtentMeters);
            float half = extent * 0.5f;
            float2 pos = new float2(absoluteX, absoluteZ);
            float2 norm = pos / extent;
            float lowWarp = (FractalNoise01(norm * 2.0f + new float2(11.7f, -3.9f), p.Seed ^ 0xB5297A4Du) * 2f - 1f) * 980f;
            float midWarp = (FractalNoise01(norm * 4.4f + new float2(-2.1f, 8.6f), p.Seed ^ 0x4CF5AD43u) * 2f - 1f) * 520f;
            float highWarp = (FractalNoise01(norm * 7.2f + new float2(-17.2f, 29.3f), p.Seed ^ 0x68E31DA4u) * 2f - 1f) * 240f;

            // DOMAIN WARPING: To break the "plastic" value noise look, we perturb the coordinates for high-frequency noise.
            float warpX = (FractalSimplexNoise01(norm * 12.0f, p.Seed ^ 0x8A1F3C4Du) * 2f - 1f) * 0.005f; 
            float warpZ = (FractalSimplexNoise01(norm * 12.0f, p.Seed ^ 0x3B8E1D2Fu) * 2f - 1f) * 0.005f;
            float2 warpedNorm = norm + new float2(warpX, warpZ);
            float2 warpedPos = warpedNorm * extent;

            // 1. CONTINENTAL SHELF / ABYSS BLEND
            float continentNoise = FractalSimplexNoise01(warpedNorm * 2.8f, p.Seed ^ 0x12345678u);
            float shelfMask = math.smoothstep(0.35f, 0.65f, continentNoise);
            
            // Steep, dramatic continental slope transition (ShelfBreak)
            float shelfBreakMask = 1f - math.saturate(math.abs(continentNoise - 0.5f) * 6.0f);
            shelfBreakMask = math.saturate(shelfBreakMask);
            
            // Canyon cuts on the shelf break
            float canyonNoise = FractalNoise01(warpedPos * 0.0004f, p.Seed ^ 0x0CA14405u);
            float canyonDepthProfile = math.pow(math.smoothstep(0.6f, 0.95f, canyonNoise), 3f);
            float canyonMask = canyonDepthProfile * math.smoothstep(0.1f, 0.9f, shelfBreakMask);
            
            // Base depth blend
            float depth = math.lerp(p.AbyssDepthMeters, p.ShelfDepthMeters, shelfMask);
            depth += canyonMask * 800f; // Deep canyon cuts

            // 2. MOUNTAIN RIDGES
            // Use Ridged Multifractal for sharp, peaked mountain ranges
            float ridgeNoise = RidgedMultifractal01(warpedNorm * 8.0f, p.Seed ^ 0x91E83B37u, 5);
            float ridgeMask = math.smoothstep(0.35f, 0.85f, ridgeNoise);
            depth -= ridgeMask * p.RidgeHeightMeters * (1f - shelfMask * 0.4f);

            // 3. DEEP OCEANIC TRENCHES / FAULTS
            float trenchNoise = RidgedMultifractal01(warpedNorm * 6.0f + new float2(0.4f, -0.6f), p.Seed ^ 0x4B3A2C1Du, 4);
            float trenchMask = math.smoothstep(0.55f, 0.95f, trenchNoise) * (1f - shelfMask);
            depth += trenchMask * p.TrenchDepthMeters;

            // 4. FAULT LINES
            float faultNoise = RidgedMultifractal01(warpedNorm * 12.0f, p.Seed ^ 0xCA97D1F3u, 3);
            float faultMask = math.smoothstep(0.45f, 0.85f, faultNoise) * (1f - shelfMask * 0.5f);
            depth += faultMask * 120f;

            // 5. ABYSSAL BASINS
            float basinMask = math.saturate((1f - shelfMask) * (1f - ridgeMask) * (1f - trenchMask));
            depth += basinMask * p.BasinDepthMeters;

            ApplySeamounts(ref depth, warpedPos, basinMask, p.Seed);

            // 3. INTERNAL PLATE FEATURES (Highlands & Warps)
            float provinceRelief = math.smoothstep(0.36f, 0.92f, FractalNoise01(warpedPos * 0.00006f, p.Seed ^ 0x21DA7F47u));
            depth += provinceRelief * 145f * math.saturate(shelfMask + basinMask);

            // Tectonic Network (Internal smaller faults)
            float internalNetwork = 1f - 2f * math.abs(FractalNoise01(warpedPos * 0.00015f, p.Seed ^ 0xCA97D1F3u) - 0.5f);
            internalNetwork = math.smoothstep(0.85f, 0.98f, internalNetwork); // Tighter faults
            float fractureMask = math.max(faultMask, internalNetwork * 0.5f);
            depth += internalNetwork * 80f; // Reduced from 150f

            float descent01 = 1f - shelfMask;
            // Relief gate controls where chaotic noise is allowed. Keep it near 0 on flat shelves and basins.
            float reliefGate = math.saturate(shelfBreakMask * 0.6f + ridgeMask * 0.8f + faultMask * 0.4f);

            // REALISTIC TECTONIC BREAKUP:
            // Macro uses RidgedMultifractal to create sharp, eroded-looking mountain peaks instead of rounded value noise hills.
            // Meso and Micro use Simplex for natural organic surface roughness without grid artifacts.
            float macroBreakup = RidgedMultifractal01(warpedNorm * 18.0f + new float2(7.7f, 41.3f), p.Seed ^ 0x91E83B37u, 5);
            float mesoBreakup = FractalSimplexNoise01(warpedNorm * 48.0f + new float2(-23.1f, 5.6f), p.Seed ^ 0x6C8E9CF5u) * 2f - 1f;
            float microBreakup = FractalSimplexNoise01(warpedNorm * 220.0f + new float2(33.1f, -14.6f), p.Seed ^ 0x1A2B3C4Du) * 2f - 1f;
            
            // Apply macro breakup as sharp peaks (subtracting depth) where relief is allowed
            depth -= macroBreakup * 350f * reliefGate; 
            depth += mesoBreakup * 140f * math.saturate(reliefGate + shelfBreakMask * 0.2f);
            float microBreakupWeight = math.saturate(ridgeMask * 0.6f + faultMask * 0.4f + reliefGate * 0.5f);
            depth += microBreakup * math.lerp(10f, 60f, microBreakupWeight);
            depth += fractureMask * 60f;

            // MESO/MICRO DETAIL PASS
            float rockDetailNoise = (FractalNoise01(warpedNorm * 150.0f + new float2(-44.2f, 88.1f), p.Seed ^ 0x7B9C1A2Fu) * 2f - 1f);
            float rockyRidgeDetail = 1f - 2f * math.abs(FractalNoise01(warpedNorm * 320.0f + new float2(11.4f, -99.3f), p.Seed ^ 0x5E8A9C1Du) - 0.5f);
            
            float hardRockExposure = math.saturate(ridgeMask * 0.6f + faultMask * 0.4f + math.saturate(descent01 * 1.5f) * 0.20f);
            float mesoDetailWeight = math.saturate(hardRockExposure * 0.8f + reliefGate * 0.3f);
            
            depth += rockDetailNoise * 35f * mesoDetailWeight;
            depth -= rockyRidgeDetail * 30f * mesoDetailWeight * math.saturate(descent01); 

            // SEDIMENT DUNE/RIPPLE DETAIL PASS
            float duneSample = FractalSimplexNoise01(warpedPos * 0.05f, p.Seed ^ 0xD11EBA5Eu);
            duneSample = 1f - math.abs(duneSample); // Create sharp ridges and wide valleys
            duneSample = math.pow(duneSample, 1.8f); // Pin the valleys flatter
            
            // Patch masking: dunes only appear in specific fields
            float duneFieldMask = FractalNoise01(warpedPos * 0.0015f, p.Seed ^ 0xA8B2C41Eu);
            duneFieldMask = math.smoothstep(0.4f, 0.6f, duneFieldMask); // Sharp transition into dune fields
            
            float sedimentDepth = math.saturate(1f - math.saturate(hardRockExposure * 1.5f));
            
            float duneAmplitude = math.lerp(4f, 1f, depth / 6000f);
            float addedHeight = duneSample * duneAmplitude * sedimentDepth * duneFieldMask;
            
            // CELLULAR PITS PASS (Craters / Pockmarks / Subsidence)
            float2 cellHash;
            float cellDist = CellularDistance01(warpedPos * 0.012f, p.Seed ^ 0xF131A21Eu, out cellHash);
            
            // We want deep pits at the center (cellDist near 0).
            float pitProfile = math.saturate(1f - cellDist * 3f); // Only the central 33% of the cell
            pitProfile = math.pow(pitProfile, 2.5f); // Make it a bowl shape
            
            // Pits appear in clusters
            float pitFieldMask = FractalNoise01(warpedPos * 0.0008f, p.Seed ^ 0x99BBE211u);
            pitFieldMask = math.smoothstep(0.5f, 0.7f, pitFieldMask);
            
            // Pits subtract from sediment depth. Max pit depth is 6m.
            float pitDepth = pitProfile * pitFieldMask * sedimentDepth * 6f;
            
            float craterMask;
            ApplyMeteorCraters(ref depth, out craterMask, warpedPos, p.Seed);
            
            depth -= (addedHeight - pitDepth);

            if (depth < -260f)
                depth = -260f + (depth + 260f) * 0.42f;
            depth = math.clamp(depth, -620f, p.HadalDepthMeters);

            ApplyTectonicTerracing(ref depth, shelfBreakMask, ridgeMask, faultMask, warpedNorm, pos, p.Seed);

            // TALUS / SCREE ACCUMULATION
            float rockBase  = math.saturate(ridgeMask * 0.7f + faultMask * 0.4f + math.saturate((1f - shelfMask) * 1.5f) * 0.3f);
            float slope01   = math.saturate(shelfBreakMask * 0.9f + ridgeMask * 0.8f + faultMask * 0.4f);
            float screeMask = math.smoothstep(0.05f, 0.30f, slope01) * (1.0f - math.smoothstep(0.40f, 0.65f, slope01));
            float screeC    = RidgedMultifractal01(warpedNorm * 140.0f, p.Seed ^ 0xE70D1A5Bu, 3);
            float screeF    = RidgedMultifractal01(warpedNorm * 480.0f,  p.Seed ^ 0xC3F19802u, 2);
            float screeRubble = ((screeC * 0.7f + screeF * 0.3f) * 2f - 1f) * 35.0f;
            depth += screeRubble * screeMask * rockBase;

            masks = new MacroMasks
            {
                Shelf = math.saturate(shelfMask),
                ShelfBreak = math.saturate(shelfBreakMask),
                Ridge = math.saturate(ridgeMask),
                Trench = math.saturate(trenchMask),
                Basin = math.saturate(basinMask),
                Fault = math.saturate(fractureMask),
                Crater = math.saturate(craterMask)
            };
            return p.WaterSurfaceY - depth;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplySeamounts(ref float depth, float2 warpedPos, float basinMask, uint seed)
        {
                // 6. ABYSSAL SEAMOUNTS / GUYOTS (warpedPos deforms perfect circular shapes)
                float2 seamountCell = math.floor(warpedPos * 0.0003f); // 3.3km grid
                float2 frac = warpedPos * 0.0003f - seamountCell;
                float minDist = 8.0f;
                float2 seamountHash = new float2(0, 0);
                float2 seamountCenterLocal = new float2(0, 0);

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbor = new float2(x, y);
                        float2 pointHash = Hash2((int)(seamountCell.x + neighbor.x), (int)(seamountCell.y + neighbor.y), seed ^ 0x5EA30447u);
                        float2 seamountDiff = neighbor + pointHash - frac;
                        float dist = math.length(seamountDiff);
                        if (dist < minDist)
                        {
                            minDist = dist;
                            seamountHash = pointHash;
                            seamountCenterLocal = seamountDiff; // vector from current warpedPos to seamount center
                        }
                    }
                }

                float seamountProfile = math.saturate(1f - minDist * 2f);
                if (seamountProfile > 0f)
                {
                    // Volcanic exponential profile
                    float volProfile = math.exp(-minDist * 6.0f);

                    float isGuyot = HashToUnitFloat(Hash(unchecked((int)(seamountHash.x * 1000f)), unchecked((int)(seamountHash.y * 1000f)), 0x123456)) > 0.5f ? 1f : 0f;

                    if (isGuyot > 0f)
                    {
                        // Guyot: Flat top
                        volProfile = math.min(volProfile, 0.4f);
                    }
                    else
                    {
                        // Caldera (Depression at the very center)
                        float calderaProfile = 1f - math.smoothstep(0f, 0.045f, minDist);
                        volProfile -= calderaProfile * 0.3f * seamountProfile;
                    }

                    // Radial Erosional Gullies (seam-free organic branching using normalized direction and warpedPos phase shift)
                    float2 dir = minDist > 0.0001f ? seamountCenterLocal / minDist : new float2(1f, 0f);
                    float gullyPattern = (FractalSimplexNoise01(dir * 3.8f + warpedPos * 0.0005f, seed ^ 0x901177Au) * 2f - 1f);
                    float gullyProfile = 1f - math.abs(gullyPattern);
                    gullyProfile = math.pow(gullyProfile, 3.0f); // Sharper cuts

                    // Gullies only form on the flanks
                    float flankMask = math.smoothstep(0.05f, 0.3f, minDist) * math.smoothstep(0.4f, 0.25f, minDist);
                    volProfile -= gullyProfile * flankMask * 0.15f;

                    depth -= math.saturate(volProfile) * basinMask * 2600f;
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyMeteorCraters(ref float depth, out float craterMask, float2 warpedPos, uint seed)
        {
                // METEOR CRATERS PASS (with rim-warping to prevent perfect mathematical circles)
                float craterDepthDelta = 0f;
                craterMask = 0f;

                float craterGridSize = 2000f;
                int2 craterCell = new int2((int)math.floor(warpedPos.x / craterGridSize), (int)math.floor(warpedPos.y / craterGridSize));

                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int2 craterNeighborCell = craterCell + new int2(dx, dz);
                        uint h = Hash(craterNeighborCell.x, craterNeighborCell.y, unchecked((int)(seed ^ 0x9B3A21EFu)));

                        // ~15% chance of a crater in this 2km cell
                        float probability = HashToUnitFloat(h ^ 0x12345678u);
                        if (probability > 0.15f) continue;

                        float cx = (craterNeighborCell.x + HashToUnitFloat(h ^ 0x87654321u)) * craterGridSize;
                        float cz = (craterNeighborCell.y + HashToUnitFloat(h ^ 0xA1B2C3D4u)) * craterGridSize;

                        // Radius between 120m and 600m
                        float radius = math.lerp(120f, 600f, math.pow(HashToUnitFloat(h ^ 0x1A2B3C4Du), 2.5f));

                        float dist = math.length(new float2(warpedPos.x - cx, warpedPos.y - cz));
                        if (dist > radius * 2.0f) continue;

                        // rimWarp: deforms the crater radius so it is NOT a perfect circle
                        float rimWarp = (FractalSimplexNoise01(warpedPos * 0.015f, h ^ 0xDEADBEEFu) * 2f - 1f) * 0.06f;
                        float normalizedDist = dist / radius + rimWarp;

                        // Crater Cavity
                        float bowl = 1f - math.smoothstep(0f, 1f, normalizedDist);
                        bowl = math.pow(bowl, 1.5f); // Flatten the center due to sedimentation

                        // Crater Rim
                        float rimProfile = math.max(0f, 1f - math.abs(normalizedDist - 1f) * 2.5f);
                        rimProfile = math.smoothstep(0f, 1f, rimProfile);

                        // Central Peak (only in large craters)
                        float peak = 0f;
                        if (radius > 1200f) {
                            float peakRadius = radius * 0.15f;
                            peak = 1f - math.smoothstep(0f, peakRadius, dist);
                            peak = math.smoothstep(0f, 1f, peak) * 0.4f;
                        }

                        // Rim Erosion Noise
                        float angle = math.atan2(warpedPos.y - cz, warpedPos.x - cx);
                        float rimErosion = FractalNoise01(new float2(angle * 4.0f, radius), h ^ 0xDEADBEEFu);
                        rimProfile *= (0.4f + rimErosion * 0.6f);

                        float maxDepth = radius * 0.18f;
                        float maxRimHeight = radius * 0.08f;

                        craterDepthDelta += bowl * maxDepth;     // Depress (add to depth)
                        craterDepthDelta -= peak * maxDepth;     // Raise peak (subtract from depth)
                        craterDepthDelta -= rimProfile * maxRimHeight; // Raise rim

                        craterMask = math.max(craterMask, bowl);
                    }
                }
                depth += craterDepthDelta;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ApplyTectonicTerracing(ref float depth, float shelfBreakMask, float ridgeMask, float faultMask, float2 warpedNorm, float2 pos, uint seed)
        {
                // TECTONIC TERRACING — Localized Geological Strata
                //
                // ROOT CAUSE OF MINECRAFT LOOK:
                //   Old step=18-55m on 400m mountain = 7-22 terraces covering 90% surface → Minecraft.
                //   Old patchMask = max(0.15, smoothstep(0.1,0.65,...)) → covers 85%+ of surface → Minecraft.
                //   Old blend = terraceStrength * patchMask ≈ 0.6-0.9 → full replacement → Minecraft.
                //
                // REAL GEOLOGY:
                //   3-5 wide benches (100-180m each) on specific slope aspects, 25-35% coverage, rest smooth.
                //
                float terraceStrength = math.saturate(shelfBreakMask * 0.8f + ridgeMask * 0.4f + faultMask * 0.5f);
                if (terraceStrength > 0.05f)
                {
                    // STEP 1: LARGE STEPS → only 3-5 terraces on a 400m mountain.
                    // 80-180m: wide geological platforms, not pixel-height Minecraft slabs.
                    float dynamicTerraceScale = math.lerp(80.0f, 180.0f,
                        FractalSimplexNoise01(warpedNorm * 3.0f, seed ^ 0x112233u));

                    // STEP 2: STRATA TILT via pos (meters). 50m per km = 1-2 step shifts across mountain.
                    float2 tiltDir = math.normalize(new float2(
                        FractalSimplexNoise01(warpedNorm * 1.8f, seed ^ 0xAB12CD34u) * 2f - 1f,
                        FractalSimplexNoise01(warpedNorm * 1.8f, seed ^ 0x56EF78ABu) * 2f - 1f
                    ));
                    float strataCoord = depth + math.dot(tiltDir, pos) * 0.05f;

                    // STEP 3: EROSION at mountain scale. ±60m+±25m on 80-180m steps = 0.33-0.75 step shift.
                    // Merges/kills whole terraces in patches rather than just wiggling edges.
                    float terraceErosionC = (FractalSimplexNoise01(warpedNorm * 80.0f,  seed ^ 0x99AA88BBu) * 2f - 1f) * 60.0f;
                    float terraceErosionF = (FractalSimplexNoise01(warpedNorm * 250.0f, seed ^ 0x77CC4411u) * 2f - 1f) * 25.0f;
                    float terraceErosion  = terraceErosionC + terraceErosionF;

                    // STEP 4: QUANTIZE with sharp cliff wall at top of step.
                    float hPhase = (strataCoord + terraceErosion) / dynamicTerraceScale;
                    float fStep  = math.frac(hPhase);
                    float sStep  = math.smoothstep(0.55f, 0.88f, fStep);

                    float terracedCoord = (math.floor(hPhase) + sStep) * dynamicTerraceScale - terraceErosion;
                    float terracedDepth = terracedCoord - math.dot(tiltDir, pos) * 0.05f;

                    // STEP 5: AGGRESSIVE PATCHINESS — only ~30% of mountain gets terracing.
                    // smoothstep(0.60, 0.92) with NO floor: passes only top 32% of noise distribution.
                    float terracePatchMask = math.smoothstep(0.60f, 0.92f,
                        FractalSimplexNoise01(warpedNorm * 4.5f, seed ^ 0x992211AAu));

                    // STEP 6: MAX BLEND 0.55 — macro shape always reads through.
                    depth = math.lerp(depth, terracedDepth, terraceStrength * terracePatchMask * 0.55f);
                }
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
            
            float minDist = 8.0f;
            cellHash = new float2(0, 0);

            for (int y = -1; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    float2 neighbor = new float2(x, y);
                    float2 pointHash = Hash2( (int)(cell.x + neighbor.x), (int)(cell.y + neighbor.y), seed);
                    float2 diff = neighbor + pointHash - frac;
                    float dist = math.length(diff);

                    if (dist < minDist)
                    {
                        minDist = dist;
                        cellHash = pointHash;
                    }
                }
            }

            return math.saturate(minDist);
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

        public static float FractalSimplexNoise01(float2 sample, uint seed)
        {
            float amplitude = 0.5f;
            float frequency = 1f;
            float total = 0f;
            float norm = 0f;
            for (int octave = 0; octave < 5; octave++)
            {
                total += SimplexNoise01(sample * frequency, seed + (uint)octave * 0x9E3779B9u) * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.02f;
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
            for (int octave = 0; octave < octaves; octave++)
            {
                float n = SimplexNoise01(sample * frequency, seed + (uint)octave * 0x9E3779B9u);
                // Ridged inversion: 1 - abs(noise * 2 - 1)
                n = 1f - math.abs(n * 2f - 1f);
                n = n * n; // sharpen ridges
                n *= weight;
                weight = math.saturate(n * 2f);
                
                total += n * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.0f;
            }

            return total / math.max(0.0001f, norm);
        }

        private static float SimplexNoise01(float2 sample, uint seed)
        {
            float2 p = math.floor(sample);
            float2 f = sample - p;
            float2 w = f * f * (3f - 2f * f);

            float a = math.dot(HashGradient(p, seed), f);
            float b = math.dot(HashGradient(p + new float2(1f, 0f), seed), f - new float2(1f, 0f));
            float c = math.dot(HashGradient(p + new float2(0f, 1f), seed), f - new float2(0f, 1f));
            float d = math.dot(HashGradient(p + new float2(1f, 1f), seed), f - new float2(1f, 1f));

            return math.lerp(math.lerp(a, b, w.x), math.lerp(c, d, w.x), w.y) * 0.5f + 0.5f;
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

        private struct MacroMasks
        {
            public float Shelf;
            public float ShelfBreak;
            public float Ridge;
            public float Trench;
            public float Basin;
            public float Fault;
            public float Crater;
        }
    }
}

