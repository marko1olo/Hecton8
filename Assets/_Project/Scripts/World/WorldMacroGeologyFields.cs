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
            
            float2 pos = new float2(absoluteX, absoluteZ);

            float probe = p.DetailProbeMeters;
            float west = EvaluateHeightMeters(absoluteX - probe, absoluteZ, in p, out _);
            float east = EvaluateHeightMeters(absoluteX + probe, absoluteZ, in p, out _);
            float south = EvaluateHeightMeters(absoluteX, absoluteZ - probe, in p, out _);
            float north = EvaluateHeightMeters(absoluteX, absoluteZ + probe, in p, out _);

            float dx = (east - west) / math.max(0.001f, probe * 2f);
            float dz = (north - south) / math.max(0.001f, probe * 2f);
            float slope = FastSqrtPositive(dx * dx + dz * dz);
            
            float curvature = (west + east + south + north - height * 4f) / math.max(0.001f, probe * probe);

            // slope is tan(theta). tan(45)=1.0, tan(60)=1.73
            // Scaling by 0.6 means saturate hits 1.0 at slope=1.66 (approx 59 degrees).
            float slope01 = math.saturate(slope * 0.6f);
            float curvature01 = math.saturate(math.abs(curvature) * 280f);
            float positiveCurvature01 = math.saturate(math.max(0f, curvature) * 280f);
            float negativeCurvature01 = math.saturate(math.max(0f, -curvature) * 280f);

            float basinFlow = math.saturate(masks.Basin * 0.48f + masks.ShelfBreak * 0.22f + (1f - slope01) * 0.18f);
            float faultFlow = math.saturate(masks.Fault * 0.35f + masks.Trench * 0.32f);
            float erosionFlow = math.saturate(basinFlow + faultFlow + FractalNoise01(pos * 0.00038f, p.Seed ^ 0xA511E9B3u) * 0.12f);
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
            // --- CONTINUOUS COORDINATE WRAPPING ---
            float period = math.max(MinimumWorldExtentMeters, p.WorldExtentMeters);
            float2 pos = new float2(absoluteX, absoluteZ);
            float2 norm = pos / period;
            
            // DOMAIN WARPING
            float warpX = (FractalSimplexNoise01(norm * 12.0f, p.Seed ^ 0x8A1F3C4Du) * 2f - 1f) * 0.005f; 
            float warpZ = (FractalSimplexNoise01(norm * 12.0f, p.Seed ^ 0x3B8E1D2Fu) * 2f - 1f) * 0.005f;
            float2 warpedNorm = norm + new float2(warpX, warpZ);

            // CONTINENTAL SHELF BLEND (Smooth descent from shallows to abyss)
            float continentMask = FractalSimplexNoise01(warpedNorm * 3.0f, p.Seed ^ 0x12345678u);
            continentMask = math.smoothstep(0.3f, 0.7f, continentMask);
            float depth = math.lerp(p.AbyssDepthMeters, p.ShelfDepthMeters, continentMask);

            // RIDGES
            float ridgeNoise = RidgedMultifractal01(warpedNorm * 18.0f, p.Seed ^ 0x91E83B37u, 5);
            float ridgeMask = math.smoothstep(0.4f, 1.0f, ridgeNoise);
            depth -= ridgeMask * p.RidgeHeightMeters * (1f - continentMask * 0.6f);

            // FAULTS / TRENCHES (Deep cuts)
            float faultNoise = RidgedMultifractal01(warpedNorm * 12.0f + new float2(0.3f, 0.7f), p.Seed ^ 0x4B3A2C1Du, 4);
            float trenchMask = math.smoothstep(0.6f, 1.0f, faultNoise);
            depth += trenchMask * p.TrenchDepthMeters;

            // HILLS
            float hillNoise = FractalSimplexNoise01(warpedNorm * 50.0f, p.Seed ^ 0x6C8E9CF5u) * 2f - 1f;
            depth += hillNoise * 80f;

            depth = math.clamp(depth, -620f, p.HadalDepthMeters);

            masks = new MacroMasks
            {
                Shelf = math.saturate(continentMask),
                ShelfBreak = math.saturate(1f - continentMask) * 0.5f,
                Ridge = math.saturate(ridgeMask),
                Trench = math.saturate(trenchMask),
                Basin = math.saturate(1f - ridgeNoise - trenchMask),
                Fault = math.saturate(math.smoothstep(0.4f, 0.6f, faultNoise)),
                Crater = 0f
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
                // Soft Ridged inversion: 1 - sqrt((noise*2-1)^2 + 0.01) to prevent C0 discontinuity
                float centered = n * 2f - 1f;
                n = 1f - math.sqrt(centered * centered + 0.01f);
                n = math.max(0f, n); // clamp to 0
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
