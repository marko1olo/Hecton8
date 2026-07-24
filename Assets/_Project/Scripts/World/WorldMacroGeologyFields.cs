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

            // 1. TECTONIC PLATES (Voronoi)
            float plateScale = 14000f; // 14km avg plate size
            float2 cell = math.floor(pos / plateScale);
            
            float minDist1 = 10f; // normalized
            float minDist2 = 10f;
            float2 bestNode1 = 0;
            float2 bestNode2 = 0;
            uint bestHash1 = 0;
            uint bestHash2 = 0;

            for (int y = -2; y <= 2; y++) // Search radius 2 for voronoi
            {
                for (int x = -2; x <= 2; x++)
                {
                    float2 neighborCell = cell + new float2(x, y);
                    uint h = Hash((int)neighborCell.x, (int)neighborCell.y, unchecked((int)(p.Seed ^ 0x6A91B3E2u)));
                    float2 node = neighborCell + new float2(HashToUnitFloat(h ^ 0x11223344u), HashToUnitFloat(h ^ 0x55667788u));
                    
                    float dist = math.length((pos / plateScale) - node);
                    if (dist < minDist1)
                    {
                        minDist2 = minDist1;
                        bestNode2 = bestNode1;
                        bestHash2 = bestHash1;
                        minDist1 = dist;
                        bestNode1 = node;
                        bestHash1 = h;
                    }
                    else if (dist < minDist2)
                    {
                        minDist2 = dist;
                        bestNode2 = node;
                        bestHash2 = h;
                    }
                }
            }

            // Plate boundary (Distance to edge)
            // distanceToBoundary based on difference between 2 closest nodes guarantees C0 continuity!
            float distanceToBoundary = (minDist2 - minDist1) * plateScale;
            float edgeDistNorm = distanceToBoundary / plateScale; // For shelf generation blending
            
            // Domain Warping for the boundary so it's not perfectly straight
            float boundaryWarp = (FractalSimplexNoise01(pos * 0.0001f, p.Seed ^ 0x98765432u) * 2f - 1f) * 1200f;
            distanceToBoundary = math.max(0f, distanceToBoundary + boundaryWarp);

            // Determine Plate Types
            float plate1Type = HashToUnitFloat(bestHash1 ^ 0xDDCCBBAAu);
            float plate2Type = HashToUnitFloat(bestHash2 ^ 0xDDCCBBAAu);
            
            // Continent vs Abyss
            bool isContinent = plate1Type > 0.6f; // 40% of plates are continents
            float plateBaseDepth = isContinent ? p.ShelfDepthMeters : p.AbyssDepthMeters;
            
            // 2. BOUNDARY FEATURES (Ridges & Trenches)
            uint boundaryHash = (uint)math.min(bestHash1, bestHash2) ^ (uint)math.max(bestHash1, bestHash2) ^ 0x99AABBCCu;
            float boundaryType = HashToUnitFloat(boundaryHash);
            
            float shelfMask = 0f;
            float shelfBreakMask = 0f;
            float trenchMask = 0f;
            float ridgeMask = 0f;
            float faultMask = 0f;
            float basinMask = 0f;
            float depth = plateBaseDepth;
            
            bool isContinent2 = plate2Type > 0.6f;
            bool subduction = (isContinent && !isContinent2) || (!isContinent && isContinent2);
            
            if (subduction)
            {
                // Subduction creates a trench on the oceanic side, and mountains (ridge) on the continental side.
                float trenchProfile = 1f - math.smoothstep(p.TrenchWidthMeters * 0.1f, p.TrenchWidthMeters * 1.5f, distanceToBoundary);
                trenchMask = trenchProfile;
                faultMask = 1f - math.smoothstep(0f, p.TrenchWidthMeters * 3.0f, distanceToBoundary);
                
                // Continental Shelf Dropoff (ShelfBreak)
                shelfBreakMask = 1f - math.smoothstep(0f, p.ShelfBreakWidthMeters * 2.0f, distanceToBoundary);
                shelfMask = isContinent ? math.saturate(distanceToBoundary / (p.ShelfBreakWidthMeters * 2.0f)) : 0f;
                
                // Submarine Canyons (Deep cuts along the shelf break)
                if (isContinent)
                {
                    float canyonNoise = FractalNoise01(new float2(pos.x * 0.0004f, pos.y * 0.0004f), p.Seed ^ 0x0CA14405u);
                    float canyonDepthProfile = math.pow(math.smoothstep(0.6f, 0.95f, canyonNoise), 3f);
                    float canyonMask = canyonDepthProfile * math.smoothstep(0.1f, 0.9f, shelfBreakMask);
                    depth += canyonMask * 800f; // Cut deep into the shelf
                }
                
                // Transition depth across the boundary (smoothstep over 3km)
                // If we are on the continent side, edgeDistNorm is positive away from the ocean.
                // We fake the transition by blending based on distance.
                float blendDist = 2000f;
                float blendSign = isContinent ? 1f : -1f;
                float edgeVal = distanceToBoundary * blendSign;
                float blendWeight = math.smoothstep(-blendDist, blendDist, edgeVal);
                depth = math.lerp(p.AbyssDepthMeters, p.ShelfDepthMeters, blendWeight);
                
                // Add trench depth
                depth += trenchProfile * p.TrenchDepthMeters;
            }
            else if (!isContinent && !isContinent2)
            {
                // Divergent or Transform Ocean boundary
                if (boundaryType > 0.5f)
                {
                    // Mid-Ocean Ridge (Divergent)
                    float ridgeProfile = 1f - math.smoothstep(p.RidgeWidthMeters * 0.2f, p.RidgeWidthMeters * 1.8f, distanceToBoundary);
                    ridgeMask = ridgeProfile;
                    faultMask = 1f - math.smoothstep(0f, p.RidgeWidthMeters * 3.0f, distanceToBoundary);
                    
                    depth -= ridgeProfile * p.RidgeHeightMeters;
                    
                    // Rift Valley (Sharp central tear)
                    float riftWidth = p.RidgeWidthMeters * 0.15f;
                    float riftValleyProfile = 1f - math.smoothstep(0f, riftWidth, distanceToBoundary);
                    // Add chaotic noise to the rift floor
                    float riftNoise = FractalNoise01(pos * 0.001f, p.Seed ^ 0x12381F7Au) * 2f - 1f;
                    depth += riftValleyProfile * (p.RidgeHeightMeters * 0.8f + riftNoise * 80f);
                }
                else
                {
                    // Transform Fault (Oceanic)
                    faultMask = 1f - math.smoothstep(0f, p.RidgeWidthMeters * 1.5f, distanceToBoundary);
                    float faultDepression = 1f - math.smoothstep(0f, p.RidgeWidthMeters * 0.8f, distanceToBoundary);
                    depth += faultDepression * 400f; // Small trench
                }
                
                // Basin is the deep center of oceanic plates
                // minDist1 is distance to cell center in normalized space (0 to ~0.707)
                basinMask = math.smoothstep(0.2f, 0.45f, minDist1);
                depth += basinMask * p.BasinDepthMeters;
                
                // Abyssal Seamounts / Guyots (Advanced Volcanic Profiles with Calderas and Radial Gullies)
                float2 seamountCell = math.floor(pos * 0.0001f);
                float2 frac = pos * 0.0001f - seamountCell;
                float minDist = 8.0f;
                float2 seamountHash = new float2(0, 0);
                float2 seamountCenterLocal = new float2(0, 0);
                
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbor = new float2(x, y);
                        float2 pointHash = Hash2((int)(seamountCell.x + neighbor.x), (int)(seamountCell.y + neighbor.y), p.Seed ^ 0x5EA30447u);
                        float2 seamountDiff = neighbor + pointHash - frac;
                        float dist = math.length(seamountDiff);
                        if (dist < minDist)
                        {
                            minDist = dist;
                            seamountHash = pointHash;
                            seamountCenterLocal = seamountDiff; // vector from current pos to seamount center
                        }
                    }
                }
                
                float seamountProfile = math.saturate(1f - minDist * 8f);
                if (seamountProfile > 0f)
                {
                    // Volcanic exponential profile
                    float volProfile = math.exp(-minDist * 40f);
                    
                    float isGuyot = HashToUnitFloat(Hash(unchecked((int)(seamountHash.x * 1000f)), unchecked((int)(seamountHash.y * 1000f)), 0x123456)) > 0.5f ? 1f : 0f;
                    
                    if (isGuyot > 0f)
                    {
                        // Guyot: Flat top
                        volProfile = math.min(volProfile, 0.4f);
                    }
                    else
                    {
                        // Caldera (Depression at the very center)
                        float calderaProfile = 1f - math.smoothstep(0f, 0.015f, minDist);
                        volProfile -= calderaProfile * 0.3f;
                    }
                    
                    // Radial Erosional Gullies
                    // Angle from center
                    float angle = math.atan2(seamountCenterLocal.y, seamountCenterLocal.x);
                    // Add noise to the angle so gullies aren't perfectly straight lines
                    float angleNoise = FractalSimplexNoise01(pos * 0.001f, p.Seed ^ 0x901177Au) * 0.5f;
                    // Ridged noise along the angle
                    float gullyFreq = 24f; // Number of gullies
                    float gullyPattern = math.sin((angle + angleNoise) * gullyFreq);
                    // We want sharp V-shaped gullies:
                    float gullyProfile = 1f - math.abs(gullyPattern);
                    gullyProfile = math.pow(gullyProfile, 2f);
                    
                    // Gullies only form on the flanks, not on the flat guyot top or the very center caldera
                    float flankMask = math.smoothstep(0.02f, 0.1f, minDist) * math.smoothstep(0.12f, 0.08f, minDist);
                    
                    volProfile -= gullyProfile * flankMask * 0.15f;
                    
                    float seamountHeight = math.saturate(volProfile) * basinMask;
                    depth -= seamountHeight * 3500f;
                    ridgeMask = math.max(ridgeMask, seamountHeight * 0.8f); // Make seamounts rocky
                }
            }
            else // Continent vs Continent
            {
                // Continental Collision (Himalayas equivalent)
                float ridgeProfile = 1f - math.smoothstep(p.RidgeWidthMeters * 0.3f, p.RidgeWidthMeters * 2.5f, distanceToBoundary);
                ridgeMask = math.max(ridgeMask, ridgeProfile);
                faultMask = math.max(faultMask, 1f - math.smoothstep(0f, p.RidgeWidthMeters * 4.0f, distanceToBoundary));
                
                depth -= ridgeProfile * (p.RidgeHeightMeters * 1.5f); // Massive mountains
                shelfMask = 1f; // All shelf
            }

            // 3. INTERNAL PLATE FEATURES (Highlands & Warps)
            float provinceRelief = math.smoothstep(0.36f, 0.92f, FractalNoise01(pos * 0.00006f, p.Seed ^ 0x21DA7F47u));
            depth += provinceRelief * 145f * math.saturate(shelfMask + basinMask);

            // Tectonic Network (Internal smaller faults)
            float internalNetwork = 1f - 2f * math.abs(FractalNoise01(pos * 0.00015f, p.Seed ^ 0xCA97D1F3u) - 0.5f);
            internalNetwork = math.smoothstep(0.85f, 0.98f, internalNetwork); // Tighter faults
            float fractureMask = math.max(faultMask, internalNetwork * 0.5f);
            depth += internalNetwork * 80f; // Reduced from 150f

            float descent01 = 1f - shelfMask;
            // Relief gate controls where chaotic noise is allowed. Keep it near 0 on flat shelves and basins.
            float reliefGate = math.saturate(shelfBreakMask * 0.8f + ridgeMask * 1.0f + faultMask * 0.6f + 0.1f);

            // DOMAIN WARPING: To break the "plastic" value noise look, we perturb the coordinates for high-frequency noise.
            float warpX = (FractalSimplexNoise01(norm * 12.0f, p.Seed ^ 0x8A1F3C4Du) * 2f - 1f) * 0.005f; 
            float warpZ = (FractalSimplexNoise01(norm * 12.0f, p.Seed ^ 0x3B8E1D2Fu) * 2f - 1f) * 0.005f;
            float2 warpedNorm = norm + new float2(warpX, warpZ);

            // REALISTIC TECTONIC BREAKUP:
            float macroBreakup = RidgedMultifractal01(warpedNorm * 18.0f + new float2(7.7f, 41.3f), p.Seed ^ 0x91E83B37u, 5);
            float mesoBreakup = FractalSimplexNoise01(warpedNorm * 48.0f + new float2(-23.1f, 5.6f), p.Seed ^ 0x6C8E9CF5u) * 2f - 1f;
            float microBreakup = FractalSimplexNoise01(warpedNorm * 220.0f + new float2(33.1f, -14.6f), p.Seed ^ 0x1A2B3C4Du) * 2f - 1f;
            
            // Abyssal Plain / Basin rolling hills (gives depth and texture to the ocean floor)
            float basinHills = FractalSimplexNoise01(warpedNorm * 12.0f, p.Seed ^ 0x55AA1122u) * 2f - 1f;
            depth += basinHills * 120f * math.saturate(basinMask + shelfMask);

            // Apply macro breakup as sharp peaks (subtracting depth) where relief is allowed
            depth -= macroBreakup * 180f * reliefGate; 
            depth += mesoBreakup * 80f * math.saturate(reliefGate + shelfBreakMask * 0.4f + basinMask * 0.2f);
            float microBreakupWeight = math.saturate(ridgeMask * 0.6f + faultMask * 0.4f + reliefGate * 0.5f + basinMask * 0.3f);
            depth += microBreakup * math.lerp(10f, 65f, microBreakupWeight);
            depth += fractureMask * 60f;

            // MESO/MICRO DETAIL PASS
            float rockDetailNoise = (FractalNoise01(warpedNorm * 150.0f + new float2(-44.2f, 88.1f), p.Seed ^ 0x7B9C1A2Fu) * 2f - 1f);
            float rockyRidgeDetail = 1f - 2f * math.abs(FractalNoise01(warpedNorm * 320.0f + new float2(11.4f, -99.3f), p.Seed ^ 0x5E8A9C1Du) - 0.5f);
            
            // INCREASE HARD ROCK EXPOSURE IN BASINS! This prevents the "белый ебаный песок" (fucking white sand) problem!
            float hardRockExposure = math.saturate(ridgeMask * 0.8f + faultMask * 0.5f + math.saturate(descent01 * 1.5f) * 0.20f + basinMask * 0.4f);
            float mesoDetailWeight = math.saturate(hardRockExposure * 0.8f + reliefGate * 0.4f + basinMask * 0.3f);
            
            depth += rockDetailNoise * 45f * mesoDetailWeight;
            depth -= rockyRidgeDetail * 40f * mesoDetailWeight * math.saturate(descent01); 

            // Add ultra-high frequency micro noise (10m wavelength) to trigger MapMagic's Cavity node for texturing!
            float microRockDetail = SimplexNoise01(warpedNorm * 2000.0f, p.Seed ^ 0x12345678u) * 2f - 1f;
            depth += microRockDetail * 5f * mesoDetailWeight; // +/- 5m micro differential

            // SEDIMENT DUNE/RIPPLE DETAIL PASS
            // Dune frequency: 0.05 (20m wide dunes) for better mesh density compatibility
            float duneSample = FractalSimplexNoise01(pos * 0.05f, p.Seed ^ 0xD11EBA5Eu);
            duneSample = 1f - math.abs(duneSample); // Create sharp ridges and wide valleys
            duneSample = math.pow(duneSample, 1.8f); // Pin the valleys flatter
            
            // Patch masking: dunes only appear in specific fields
            float duneFieldMask = FractalNoise01(pos * 0.0015f, p.Seed ^ 0xA8B2C41Eu);
            duneFieldMask = math.smoothstep(0.4f, 0.6f, duneFieldMask); // Sharp transition into dune fields
            
            float sedimentDepth = math.saturate(1f - math.saturate(hardRockExposure * 1.5f));
            
            float duneAmplitude = math.lerp(4f, 1f, depth / 6000f);
            float addedHeight = duneSample * duneAmplitude * sedimentDepth * duneFieldMask;
            
            // CELLULAR PITS PASS (Craters / Pockmarks / Subsidence)
            // Frequency 0.012 -> ~80m cells. We extract the distance to the center.
            float2 cellHash;
            float cellDist = CellularDistance01(pos * 0.012f, p.Seed ^ 0xF131A21Eu, out cellHash);
            
            // We want deep pits at the center (cellDist near 0).
            // A pit is formed if cellDist is small. We invert it: 1 - cellDist.
            float pitProfile = math.saturate(1f - cellDist * 3f); // Only the central 33% of the cell
            pitProfile = math.pow(pitProfile, 2.5f); // Make it a bowl shape
            
            // Pits appear in clusters
            float pitFieldMask = FractalNoise01(pos * 0.0008f, p.Seed ^ 0x99BBE211u);
            pitFieldMask = math.smoothstep(0.5f, 0.7f, pitFieldMask);
            
            // Pits subtract from sediment depth. Max pit depth is 6m.
            float pitDepth = pitProfile * pitFieldMask * sedimentDepth * 6f;
            
            // METEOR CRATERS PASS
            float craterDepthDelta = 0f;
            float craterMask = 0f;
            
            float craterGridSize = 8000f;
            int2 craterCell = new int2((int)math.floor(absoluteX / craterGridSize), (int)math.floor(absoluteZ / craterGridSize));
            
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int2 craterNeighborCell = craterCell + new int2(dx, dz);
                    uint h = Hash(craterNeighborCell.x, craterNeighborCell.y, unchecked((int)(p.Seed ^ 0x9B3A21EFu)));
                    
                    // ~15% chance of a crater in this 8km cell
                    float probability = HashToUnitFloat(h ^ 0x12345678u);
                    if (probability > 0.15f) continue;
                    
                    float cx = (craterNeighborCell.x + HashToUnitFloat(h ^ 0x87654321u)) * craterGridSize;
                    float cz = (craterNeighborCell.y + HashToUnitFloat(h ^ 0xA1B2C3D4u)) * craterGridSize;
                    
                    // Radius between 400m and 2500m
                    float radius = math.lerp(400f, 2500f, math.pow(HashToUnitFloat(h ^ 0x1A2B3C4Du), 2.5f)); 
                    
                    float dist = math.length(new float2(absoluteX - cx, absoluteZ - cz));
                    if (dist > radius * 2.0f) continue;
                    
                    float normalizedDist = dist / radius;
                    
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
                    float angle = math.atan2(absoluteZ - cz, absoluteX - cx);
                    float rimErosion = FractalNoise01(new float2(angle * 4.0f, radius), h ^ 0xDEADBEEFu);
                    rimProfile *= (0.4f + rimErosion * 0.6f);
                    
                    float maxDepth = radius * 0.18f;
                    float maxRimHeight = radius * 0.08f;
                    
                    craterDepthDelta += bowl * maxDepth;     // Depress (add to depth)
                    craterDepthDelta -= peak * maxDepth;     // Raise peak (subtract from depth)
                    craterDepthDelta -= rimProfile * maxRimHeight; // Raise rim
                    
                    craterMask = math.max(craterMask, bowl);
                    ridgeMask = math.max(ridgeMask, rimProfile * 0.8f); // Make crater rims rocky!
                }
            }
            depth += craterDepthDelta;
            
            depth -= (addedHeight - pitDepth);

            if (depth < -260f)
                depth = -260f + (depth + 260f) * 0.42f;
            depth = math.clamp(depth, -620f, p.HadalDepthMeters);

            // Tectonic Terracing (Advanced Strata-based non-uniform terracing)
            float terraceStrength = math.saturate(shelfBreakMask + ridgeMask * 0.5f + faultMask * 0.6f);
            if (terraceStrength > 0.01f)
            {
                // We use a 1D noise based on depth to determine the "hardness" of the geological strata layer.
                // Hard strata = sharp cliff. Soft strata = wide sloped sediment terrace.
                float strataNoise = FractalNoise01(new float2(depth * 0.02f, 0f), p.Seed ^ 0x578A7A5u);

                float baseTerraceHeight = 40f; // Average layer thickness
                // Warp the depth: where noise is high, depth compresses (cliff). where noise is low, depth stretches (shelf).
                float warpedDepth = depth + strataNoise * 30f;

                float normalizedDepth = warpedDepth / baseTerraceHeight;
                float baseStep = math.floor(normalizedDepth);
                float frac = normalizedDepth - baseStep;

                // The easing sharpness depends on the hardness of the current step
                float stepHardness = FractalNoise01(new float2(baseStep * 0.5f, 0f), p.Seed ^ 0x4A8D4E5u);
                float edgeWidth = math.lerp(0.4f, 0.05f, stepHardness); // Soft = 0.4, Hard = 0.05

                float eased = math.smoothstep(0.5f - edgeWidth, 0.5f + edgeWidth, frac);
                float terracedDepth = (baseStep + eased) * baseTerraceHeight;
                
                depth = math.lerp(depth, terracedDepth, terraceStrength * 0.85f);
            }

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
