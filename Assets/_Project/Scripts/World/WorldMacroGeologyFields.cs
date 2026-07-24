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

    public enum ProvinceType : int
    {
        AbyssalPlain = 0,
        CrateredHighlands = 1,
        RiverLowlands = 2,
        FoldedMountains = 3,
        RiftValley = 4,
        VolcanicField = 5,
        MesaTablelands = 6,
        DuneSea = 7
    }

    public struct ProvinceRecipe
    {
        public float Craters;   // B1
        public float Rivers;    // B2
        public float Lakes;     // B3
        public float Strata;    // B4
        public float Folds;     // B5
        public float Volcanic;  // B6
        public float Mesa;      // B7
        public float Dunes;     // B8
        public float Reefs;     // B10
        public float BaseRough; // TIER 2 base roughness multiplier

        public static ProvinceRecipe GetRecipe(int type)
        {
            switch (type)
            {
                case 0: // ABYSSAL_PLAIN
                    return new ProvinceRecipe { Craters = 0.05f, Rivers = 0.00f, Lakes = 0.00f, Strata = 0.10f, Folds = 0.00f, Volcanic = 0.10f, Mesa = 0.00f, Dunes = 0.30f, Reefs = 0.50f, BaseRough = 0.15f };
                case 1: // CRATERED_HIGHLANDS
                    return new ProvinceRecipe { Craters = 1.00f, Rivers = 0.10f, Lakes = 0.00f, Strata = 0.30f, Folds = 0.00f, Volcanic = 0.10f, Mesa = 0.20f, Dunes = 0.00f, Reefs = 0.20f, BaseRough = 0.40f };
                case 2: // RIVER_LOWLANDS
                    return new ProvinceRecipe { Craters = 0.10f, Rivers = 1.00f, Lakes = 0.80f, Strata = 0.50f, Folds = 0.10f, Volcanic = 0.00f, Mesa = 0.10f, Dunes = 0.20f, Reefs = 0.40f, BaseRough = 0.30f };
                case 3: // FOLDED_MOUNTAINS
                    return new ProvinceRecipe { Craters = 0.10f, Rivers = 0.30f, Lakes = 0.00f, Strata = 0.70f, Folds = 1.00f, Volcanic = 0.20f, Mesa = 0.00f, Dunes = 0.00f, Reefs = 0.10f, BaseRough = 0.60f };
                case 4: // RIFT_VALLEY
                    return new ProvinceRecipe { Craters = 0.10f, Rivers = 0.40f, Lakes = 0.30f, Strata = 0.40f, Folds = 0.30f, Volcanic = 0.60f, Mesa = 0.00f, Dunes = 0.00f, Reefs = 0.20f, BaseRough = 0.50f };
                case 5: // VOLCANIC_FIELD
                    return new ProvinceRecipe { Craters = 0.20f, Rivers = 0.10f, Lakes = 0.00f, Strata = 0.20f, Folds = 0.00f, Volcanic = 1.00f, Mesa = 0.10f, Dunes = 0.10f, Reefs = 0.30f, BaseRough = 0.50f };
                case 6: // MESA_TABLELANDS
                    return new ProvinceRecipe { Craters = 0.10f, Rivers = 0.30f, Lakes = 0.20f, Strata = 1.00f, Folds = 0.10f, Volcanic = 0.00f, Mesa = 1.00f, Dunes = 0.10f, Reefs = 0.20f, BaseRough = 0.30f };
                case 7: // DUNE_SEA
                    return new ProvinceRecipe { Craters = 0.00f, Rivers = 0.00f, Lakes = 0.00f, Strata = 0.20f, Folds = 0.00f, Volcanic = 0.00f, Mesa = 0.00f, Dunes = 1.00f, Reefs = 0.60f, BaseRough = 0.20f };
                default:
                    return new ProvinceRecipe { Craters = 0.10f, Rivers = 0.10f, Lakes = 0.10f, Strata = 0.20f, Folds = 0.10f, Volcanic = 0.10f, Mesa = 0.10f, Dunes = 0.10f, Reefs = 0.30f, BaseRough = 0.30f };
            }
        }

        public static ProvinceRecipe Lerp(ProvinceRecipe a, ProvinceRecipe b, float t)
        {
            t = math.saturate(t);
            return new ProvinceRecipe
            {
                Craters   = math.lerp(a.Craters, b.Craters, t),
                Rivers    = math.lerp(a.Rivers, b.Rivers, t),
                Lakes     = math.lerp(a.Lakes, b.Lakes, t),
                Strata    = math.lerp(a.Strata, b.Strata, t),
                Folds     = math.lerp(a.Folds, b.Folds, t),
                Volcanic  = math.lerp(a.Volcanic, b.Volcanic, t),
                Mesa      = math.lerp(a.Mesa, b.Mesa, t),
                Dunes     = math.lerp(a.Dunes, b.Dunes, t),
                Reefs     = math.lerp(a.Reefs, b.Reefs, t),
                BaseRough = math.lerp(a.BaseRough, b.BaseRough, t)
            };
        }
    }

    /// <summary>
    /// Seeded macro geology fields for the playable seafloor.
    /// </summary>
    public static class WorldMacroGeologyFields
    {
        public const int DefaultAuthoringSeed = 880031;
        public const float MinimumWorldExtentMeters = 30000f;
        public const float DefaultChunkSizeMeters = 512f;
        public const uint ArtifactVersion = 11u;

        // BUILD SENTINEL: proves which compiled version the atlas actually ran. If the atlas report
        // does NOT print this exact string, Unity executed a STALE assembly (cache/no reload), not
        // this source. Bump the suffix every edit round.
        public static string BuildSentinel => "SENTINEL_R42_2026-07-24_METABALL_VOLCANOES_AND_FEATHERS";

        // R17 STAGE-LOCALIZED FIXES:
        public const bool DiagRidgedAsFbmMountain = true;
        public const bool DiagFoldNonPeriodic     = true;
        public const bool DiagStrataNonPeriodic   = false; // R39: Real elevation-based strata snapping
        public const bool DiagSoftMaskEdges       = true;

        // R14: ALL diagnostic isolation OFF. R13 raw-probe proved the hatching METRIC is degenerate at
        // sub-period scales (bare-noise SMOOTH-RAMP tile at 200m scored hatch 8.18 with ZERO visible
        // stripes — a uniform gradient reads as max anisotropy). That invalidates the 1km/200m numeric
        // FAILs we chased for 13 rounds, including the "flat-tile zebra" that drove R13. So R14 abandons
        // the metric and evaluates the TRUE FULL SHIPPING TERRAIN by EYE only. Every isolation flag below
        // is false → the atlas renders exactly what the Director sees in-game.
        public const bool DiagStrataContourOff = false; // R14: real terrain (was OFF R8-R13 for isolation)
        public const bool DiagPlateSeamOff = false;     // R21: PLATE CLEARED — not the primary source
        public const bool DiagShelfBreakOff = false;    // R22: SHELFBREAK CLEARED — not the primary source
        // R9 ISOLATION: R8 proved strata+plate are NOT the P2-P5 dactyloscopy source (rings remain with
        // both OFF; hatching P2 200m=2.43, P3 200m=4.00, P4 200m=2.31, P5 200m=1.91 — all >1.8 visible).
        // Per-point dominant mask splits the suspects spatially: P2=Trench49%, P3/P5=Volcano, P4=Fault18%.
        // RidgedMultifractal01 still uses the sharp n=1-abs(snoise);n=n*n crest = C1 corner = crease grain;
        // volcano cone=exp(-dist*4.2) is RADIAL = concentric rings. These flags zero each height write so
        // one atlas run isolates all three at once. If a point's hatching drops <1.8 → that term is its root.
        public const bool DiagTrenchOff  = false; // R11: restored (exonerated in R9)
        public const bool DiagVolcanoOff = false; // R11: restored (exonerated in R9)
        public const bool DiagFaultOff   = false; // R11: restored (exonerated in R9)
        public const bool DiagMesoFractureOff = false; // R11: restored (exonerated in R10 — its removal made zebra WORSE)
        public const bool DiagTalusOff        = false; // R11: restored
        public const bool DiagGeoNoiseOff     = false; // R11: restored
        // R11 DECISIVE TEST: the ridged/eroded noise weight-feedback (weight=saturate(n*2)) kills higher
        // octaves wherever octave-0 is weak → the field COLLAPSES to single-frequency octave-0 = REGULAR
        // parallel ridges at octave-0's FIXED per-seed angle = the fingerprint + the long crest "seam"
        // lines. R10 proved removing HF dither made it WORSE (naked octave-0 showed through). This flag
        // makes RidgedMultifractal01 + ErodedRidge01 BROADBAND (weight stays 1, all octaves contribute).
        // TWO-PASS PROTOCOL (RULE 1, one variable): build once with false (R11_BASE), flip to true
        // (R11_BROAD), rebuild. Director compares hillshade+height between the two image sets ONLY.
        public const bool DiagNoiseBroadband = false; // R11 EXONERATED weight-feedback: weight=1 removed ZERO seam/zebra pixels.
        // R12 TWO ORTHOGONAL TESTS (each isolates one bug definitively, one atlas run):
        //  A) DiagRidgedAsFbm: in RidgedMultifractal01 + ErodedRidge01, replace the RIDGED transform
        //     n=1-|snoise| (which makes a SHARP CREST LINE ~1px on hillshade = the seam) with plain fBm
        //     n=snoise*0.5+0.5 (NO crest). If the 1px hairline seams VANISH → ridged crest is the seam.
        //  B) DiagFoldsDunesOff: zero the B5 fold sin() corrugation (line ~708) and B8 dune sin() (~841).
        //     sin(dot(pos,axis)) = REGULAR PARALLEL world-locked waves = the zebra. If zebra VANISHES on
        //     continental tiles (P2/P3) → fold corrugation is the dactyloscopy.
        public const bool DiagRidgedAsFbm  = false; // R16: OFF — stage-dump shows the REAL pipeline per stage.
        public const bool DiagFoldsDunesOff = false; // R16: OFF — real pipeline.
        // R13 RAW PRIMITIVE PROBE. Pattern across R8-R12: removing any FEATURE makes zebra/seam WORSE or
        // unchanged, and zebra appears even on FLAT tiles (P5 1km slope 0.1%, hatch 4.33). Conclusion: the
        // artifact is NOT any added feature — it is intrinsic to the FOUNDATION every term shares:
        // noise.snoise and/or the domain warp. We never tested the bare primitive in 5 rounds. These
        // early-return probes bypass ALL geology and output pure noise so we measure where striping is born:
        //   0 = off (normal pipeline)
        public const int DiagRawProbe = 0; // R14: OFF — probe done its job (localized the metric bug, not the primitive).

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
            float erosionFlow = math.saturate(basinFlow + faultFlow + erosionVeins * masks.Canyon * 0.65f);
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
                TributaryCanyonMask = math.saturate(masks.Canyon * 0.85f + erosionFlow * 0.50f + masks.Fault * 0.18f + negativeCurvature01 * 0.35f),
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

        // System A: Resolve Organic Geological Province Recipe
        private static ProvinceRecipe ResolveProvince(
            float2 pos,
            float2 norm,
            float continentality,
            float plateEdgeMask,
            uint seed,
            out int primaryTypeIndex,
            out float provinceBlend)
        {
            float sizeNoise = FractalSimplexNoise01(norm * 0.45f + new float2(12.5f, -8.3f), seed ^ 0x7A9B3C1Du);
            float baseCellSize = math.lerp(55000f, 95000f, sizeNoise);

            float2 provWarp = new float2(
                FractalSimplexNoise01(norm * 0.35f + new float2(14.2f, -8.7f), seed ^ 0x3E5A7B11u) * 2f - 1f,
                FractalSimplexNoise01(norm * 0.35f + new float2(-9.1f, 18.4f), seed ^ 0x8C1D4F22u) * 2f - 1f) * 32000f;

            float2 sampleP = (pos + provWarp) / baseCellSize;
            int2 cellBase = (int2)math.floor(sampleP);

            // SMOOTH province blend: weight each of the 3x3 cells by exp(-hardness*dist). For a FIXED
            // cell, dist is a C-infinity function of position, so the normalized blend is smooth
            // EVERYWHERE — no Voronoi F2-F1 edge, no gradient crease, therefore NO 1px seam line along
            // province borders (the previous Lerp(r1,r2,0.5*(1-blend)) was value-continuous but its
            // gradient still kinked at the F2-F1 edge -> the thin curved lines the Director kept seeing).
            const float provHardness = 5.5f;
            float wSum = 0f, f1 = float.MaxValue, f2 = float.MaxValue;
            int2 bestCell = cellBase;
            float aCr = 0f, aRi = 0f, aLa = 0f, aSt = 0f, aFo = 0f, aVo = 0f, aMe = 0f, aDu = 0f, aBr = 0f;

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int2 cell = cellBase + new int2(dx, dz);
                    float2 hashP = Hash2(cell.x, cell.y, seed ^ 0x6E2D9A15u);
                    float2 center = new float2(cell.x, cell.y) + hashP;
                    float dist = math.length(sampleP - center);
                    if (dist < f1) { f2 = f1; f1 = dist; bestCell = cell; }
                    else if (dist < f2) { f2 = dist; }

                    float w = math.exp(-provHardness * dist);
                    ProvinceRecipe r = ProvinceRecipe.GetRecipe(SelectGeologicalType(cell, continentality, plateEdgeMask, seed));
                    aCr += r.Craters * w; aRi += r.Rivers * w; aLa += r.Lakes * w; aSt += r.Strata * w;
                    aFo += r.Folds * w; aVo += r.Volcanic * w; aMe += r.Mesa * w; aDu += r.Dunes * w; aBr += r.BaseRough * w;
                    wSum += w;
                }
            }

            float inv = 1f / math.max(1e-6f, wSum);
            primaryTypeIndex = SelectGeologicalType(bestCell, continentality, plateEdgeMask, seed); // atlas colour only
            provinceBlend = math.saturate((f2 - f1) * 2.5f); // atlas display only; NOT fed into height
            return new ProvinceRecipe
            {
                Craters = aCr * inv, Rivers = aRi * inv, Lakes = aLa * inv, Strata = aSt * inv,
                Folds = aFo * inv, Volcanic = aVo * inv, Mesa = aMe * inv, Dunes = aDu * inv, BaseRough = aBr * inv
            };
        }

        private static int SelectGeologicalType(int2 cell, float continentality, float plateEdgeMask, uint seed)
        {
            uint h = Hash(cell.x, cell.y, (int)(seed ^ 0x6E2D9A15u));
            float rawVal = HashToUnitFloat(h);

            // FIX B (R25): province TYPE from cell hash ONLY. No continentality/plateEdgeMask
            // threshold here — those are SMOOTH fields and any hard cutoff on them injects a C0
            // step in recipe.BaseRough along the cutoff isoline (the 10km 1px lines). Land/ocean
            // is applied later as a SMOOTH height gate (continentalRelief * continentality), so
            // an ocean-typed cell on land (or vice versa) blends with zero discontinuity.
            if (rawVal < 0.16f) return 0; // ABYSSAL_PLAIN
            if (rawVal < 0.28f) return 7; // DUNE_SEA
            if (rawVal < 0.42f) return 1; // CRATERED_HIGHLANDS
            if (rawVal < 0.56f) return 2; // RIVER_LOWLANDS
            if (rawVal < 0.70f) return 3; // FOLDED_MOUNTAINS
            if (rawVal < 0.82f) return 6; // MESA_TABLELANDS
            if (rawVal < 0.92f) return 5; // VOLCANIC_FIELD
            return 4;                     // RIFT_VALLEY
        }

        public static float EvaluateHeightMeters(float absoluteX, float absoluteZ, in WorldMacroGeologyParams parameters, out MacroMasks masks)
        {
            return EvaluateHeightMeters(absoluteX, absoluteZ, in parameters, out masks, 0);
        }

        // R16 STAGE DUMP: return the height with depth accumulation stopped after stage N, so ONE build
        // renders the depth field after each pipeline stage and we SEE which stage introduces the zebra /
        // rings / hairline. 0 = full pipeline (normal). Non-zero early-returns raw (WaterSurfaceY - depth).
        //   1=base(shelf/abyss)  2=+continentRelief(mtn/foothill/plateau)  3=+ridges  4=+trench/fault/basin
        //   5=+fold  6=+volcano/crater/river/lake/mesa/dune  7=+strata  8=+mesoFracture/talus (=full)
        public static float EvaluateHeightMeters(float absoluteX, float absoluteZ, in WorldMacroGeologyParams parameters, out MacroMasks masks, int stageDump)
        {
            float extent = math.max(MinimumWorldExtentMeters, parameters.WorldExtentMeters);
            float2 pos = new float2(absoluteX, absoluteZ);
            float2 norm = pos / extent;
            uint seed = parameters.Seed;

            // TIER 1: low-frequency tectonic domain warp and F1/F2 cellular plate solve.
            float2 tectonicWarp = new float2(
                FractalSimplexNoise01(norm * 0.62f + new float2(11.7f, -3.9f), seed ^ 0xB5297A4Du) * 2f - 1f,
                FractalSimplexNoise01(norm * 0.58f + new float2(-2.1f, 8.6f), seed ^ 0x4CF5AD43u) * 2f - 1f) * 4500f;
            float2 mesoWarp = new float2(
                FractalSimplexNoise01(norm * 7.5f + new float2(-17.2f, 29.3f), seed ^ 0x68E31DA4u) * 2f - 1f,
                FractalSimplexNoise01(norm * 8.1f + new float2(23.5f, -19.7f), seed ^ 0x8A1F3C4Du) * 2f - 1f) * 120f;
            float2 warpedPos = pos + tectonicWarp + mesoWarp;
            float2 warpedNorm = warpedPos / extent;

            // R13 RAW PRIMITIVE PROBE: bypass all geology, emit pure noise to locate the striping source.
            if (DiagRawProbe != 0)
            {
                masks = default;
                float raw;
                if (DiagRawProbe == 1)
                    raw = noise.snoise(pos * 0.0009f + new float2(7.3f, -4.1f));           // bare simplex, UNWARPED
                else if (DiagRawProbe == 2)
                    raw = noise.snoise(warpedPos * 0.0009f + new float2(7.3f, -4.1f));      // bare simplex, WARPED
                else
                    raw = FractalSimplexNoise01(pos * 0.0009f, seed, 5) * 2f - 1f;          // 5-octave, UNWARPED
                return raw * 400f;
            }

            float2 plateSample = warpedPos / 12000f;
            int2 plateBase = (int2)math.floor(plateSample);
            float plateF1Sq = float.MaxValue;
            float plateF2Sq = float.MaxValue;
            int2 nearestPlateCell = plateBase;

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int2 cell = plateBase + new int2(dx, dz);
                    float2 feature = new float2(cell.x, cell.y) + Hash2(cell.x, cell.y, seed ^ 0x5EAF1D7Bu);
                    float distSq = math.lengthsq(plateSample - feature);
                    if (distSq < plateF1Sq)
                    {
                        plateF2Sq = plateF1Sq;
                        plateF1Sq = distSq;
                        nearestPlateCell = cell;
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

            // R40 FIX: Warp plateInterior distance field so 12km Voronoi cell centers do not inject
            // perfect smooth concentric circles into Stage 1 depth (the P1 200m "soapy oval" artifact).
            float plateInteriorWarp = FractalSimplexNoise01(warpedNorm * 2.8f + new float2(15.1f, -9.4f), seed ^ 0x3E5A7B11u) * 0.25f - 0.125f;
            float plateInterior = 1f - math.smoothstep(0.08f, 0.62f, plateF1 + plateInteriorWarp);

            float boundaryPolarity = FractalSimplexNoise01(warpedNorm * 0.85f + new float2(41.3f, -22.7f), seed ^ 0xA77D3F19u, 3);
            float ridgePolarity = math.smoothstep(0.36f, 0.70f, boundaryPolarity);
            float trenchPolarity = 1f - math.smoothstep(0.30f, 0.64f, boundaryPolarity);
            float jaggedBoundary = RidgedMultifractal01(warpedPos * 0.00018f + new float2(13.6f, -8.1f), seed ^ 0xD1F123BBu, 5);
            float plateRidgeMask = plateEdgeMask * ridgePolarity * math.smoothstep(0.24f, 0.78f, jaggedBoundary);
            float plateTrenchMask = plateEdgeMask * trenchPolarity * math.smoothstep(0.18f, 0.72f, 1f - jaggedBoundary * 0.55f + plateEdgeMask * 0.45f);
            if (DiagPlateSeamOff) { plateRidgeMask = 0f; plateTrenchMask = 0f; plateEdgeMask = 0f; }

            // TIER 2: continent & ocean base fields (unified continentality + C2 smooth shelf break)
            float continentField = FractalSimplexNoise01(warpedNorm * 1.35f + new float2(19.2f, -7.3f), seed ^ 0x1C0A7E5Fu, 5);
            float continentality = math.smoothstep(0.40f, 0.66f, continentField);

            // FIX: Evaluate shelfMask directly from the raw field, NOT from continentality.
            // This prevents nested-smoothstep flat terracing (the "overlapping transparent PNGs" look).
            float shelfMask = math.smoothstep(0.30f, 0.60f, continentField);

            // R42: Warp continentality input for shelfBreakMask so the shelf edge is an organic, ragged coastline,
            // NOT a smooth geometric ellipse (which created the 2 smooth oval shapes in top-left P1 200m).
            float shelfBreakWarp = FractalSimplexNoise01(warpedPos * 0.0008f + new float2(7.1f, -11.3f), seed ^ 0x6E1A2B3Cu, 3) * 0.12f - 0.06f;
            float breakDelta = math.saturate(math.abs(continentality + shelfBreakWarp - 0.42f) * 4.5f);
            float smoothBreak = math.cos(breakDelta * 1.5707963f) * math.smoothstep(1.0f, 0.90f, breakDelta);
            float shelfBreakMask = math.saturate(smoothBreak * (0.62f + plateEdgeMask * 0.38f));

            float abyssPlainMask = math.saturate((1f - shelfMask) * (1f - plateEdgeMask * 0.85f) * (0.42f + plateInterior * 0.58f));
            float shelfToe = math.saturate(math.smoothstep(0.16f, 0.72f, shelfBreakMask) * (1f - shelfMask * 0.25f));

            float depth = math.lerp(parameters.AbyssDepthMeters, parameters.ShelfDepthMeters, shelfMask);
            depth += abyssPlainMask * parameters.BasinDepthMeters * 0.35f;

            // R41: Add subtle organic micro-terrain noise to Stage 1 shelf so base shelf is not a smooth plastic lens
            float shelfRoughness = (FractalSimplexNoise01(warpedPos * 0.0006f + new float2(14.2f, -7.8f), seed ^ 0x51A2B3C4u, 4) * 2f - 1f) * 28f;
            depth += shelfRoughness * shelfMask;

            // =========================================================================
            // SYSTEM A: PROVINCE RESOLVE (Organic, non-grid, blended recipes)
            // =========================================================================
            int primaryProvinceTypeIndex;
            float provinceBlend;
            ProvinceRecipe recipe = ResolveProvince(pos, norm, continentality, plateEdgeMask, seed, out primaryProvinceTypeIndex, out provinceBlend);

            // TIER 2B: Base Tectonic Relief (scaled by recipe.BaseRough)
            float2 mtnWarp = new float2(
                FractalSimplexNoise01(warpedPos * 0.00006f + new float2(5.1f, 2.4f), seed ^ 0x2B9F4C11u) * 2f - 1f,
                FractalSimplexNoise01(warpedPos * 0.00006f + new float2(-3.7f, 9.8f), seed ^ 0x77C2A5E3u) * 2f - 1f) * 2600f;

            // R27 STEP 1: Finite-difference analytical mountain gradient.
            // 3 taps of ErodedRidge01 at +10m offsets → true slope vector → trueMountainSlope.
            // This is the only C1-safe way to make fractures and talus causally aware of mountain steepness.
            float mBase = ErodedRidge01(warpedPos * 0.00013f + mtnWarp * 0.00013f, seed ^ 0x51B7D9A2u, 6);
            float mDx = ErodedRidge01((warpedPos + new float2(10f, 0f)) * 0.00013f + mtnWarp * 0.00013f, seed ^ 0x51B7D9A2u, 6);
            float mDz = ErodedRidge01((warpedPos + new float2(0f, 10f)) * 0.00013f + mtnWarp * 0.00013f, seed ^ 0x51B7D9A2u, 6);
            float mSlopeMag = math.length(new float2(mDx - mBase, mDz - mBase)) / 10f; // true gradient

            float mountainField = mBase;
            float mountainBelt = math.smoothstep(0.30f, 0.72f, FractalSimplexNoise01(warpedNorm * 2.1f + new float2(-8.4f, 3.1f), seed ^ 0x93A11E77u));
            float mountainUplift = mountainField * mountainBelt * 650f * recipe.BaseRough;
            float trueMountainSlope = math.saturate(mSlopeMag * 1500f * mountainBelt); // boosted to 0..1

            float hillinessField = FractalSimplexNoise01(warpedNorm * 0.9f + new float2(33.1f, -12.7f), seed ^ 0xD4E5F601u, 4);
            float hillinessMask = math.smoothstep(0.28f, 0.68f, hillinessField);

            float2 hillWarp = new float2(
                FractalSimplexNoise01(warpedPos * 0.00035f + new float2(7.3f, -4.1f), seed ^ 0xF1A2B3C4u) * 2f - 1f,
                FractalSimplexNoise01(warpedPos * 0.00035f + new float2(-2.9f, 8.6f), seed ^ 0xE5D4C3B2u) * 2f - 1f) * 900f;

            // R28 FIX 1: Non-periodic fractal directional skew for cuestas.
            // REPLACED periodic sin(math.dot(pos, dir)) which injected 13.9km periodic harmonics (the Stage 2 dactyloscopy zebra).
            // Now using non-periodic Simplex noise along hillStrikeDir to stretch hills into cuestas ZERO zebra rings.
            float hillStrikeAngle = FractalSimplexNoise01(warpedNorm * 1.8f, seed ^ 0x11223344u) * 3.14159f;
            float2 hillStrikeDir = new float2(math.cos(hillStrikeAngle), math.sin(hillStrikeAngle));
            float hillSkewA = FractalSimplexNoise01(warpedPos * 0.00035f + new float2(12.4f, -8.1f), seed ^ 0x55667788u) * 2f - 1f;
            float hillSkewB = FractalSimplexNoise01(warpedPos * 0.00075f + new float2(-4.2f, 15.3f), seed ^ 0x99AABBCCu) * 2f - 1f;
            float2 skewedPosLarge = warpedPos + hillWarp + hillStrikeDir * (hillSkewA * 1500f);
            float2 skewedPosMed   = warpedPos + hillWarp * 0.5f + hillStrikeDir * (hillSkewB * 600f);

            // ──── R30 COMPOUND MASKING: break uniform pimple carpet into localized patchy outcrops ────
            // Medium hills: isolated island clusters via Simplex clump mask
            float clumpNoiseMed = FractalSimplexNoise01(warpedPos * 0.0018f, seed ^ 0xC1D2E3F4u, 3);
            float clumpMaskMed  = math.smoothstep(0.4f, 0.7f, clumpNoiseMed) * hillinessMask;

            // Small outcrops: isolated, rugged patches via Fractal Simplex (not cellular Ridged web)
            float clumpNoiseSmall = FractalSimplexNoise01(warpedPos * 0.004f + new float2(-15.3f, 8.8f), seed ^ 0xA4B5C6D7u, 4);
            float clumpMaskSmall  = math.smoothstep(0.65f, 0.90f, clumpNoiseSmall) * hillinessMask;

            // Domain warp for small hills: independent noise samples for X/Y (no sin/cos iso-contour trap)
            float2 warpSmall = new float2(
                FractalSimplexNoise01(warpedPos * 0.0025f + new float2(19.7f, -6.3f), seed ^ 0xD8E9F0A1u) * 2f - 1f,
                FractalSimplexNoise01(warpedPos * 0.0025f + new float2(-11.2f, 14.8f), seed ^ 0xB2C3D4E5u) * 2f - 1f) * 150f;

            // Noise generators: largeHills & medHills keep R28 cuesta-skewed coords
            float largeHills = BillowNoise01(skewedPosLarge * 0.00045f, seed ^ 0xA1B2C3D4u, 4);
            float medHills   = BillowNoise01(skewedPosMed * 0.0014f + new float2(5.5f, -3.3f), seed ^ 0x9E8D7C6Bu, 3);
            float smallHills = BillowNoise01((warpedPos + warpSmall) * 0.0042f + new float2(-8.1f, 2.7f), seed ^ 0x7F6E5D4Cu, 3);

            // Assembly: large on broad mask, med/small on clumpy masks
            float foothills  = (largeHills * 140f) * hillinessMask
                             + (medHills * 55f) * clumpMaskMed
                             + (smallHills * 20f) * clumpMaskSmall;
            foothills *= recipe.BaseRough;

            float plateauField  = math.smoothstep(0.52f, 0.80f, FractalSimplexNoise01(warpedNorm * 1.7f + new float2(11.9f, 4.3f), seed ^ 0xB3C7159Du));
            float plateauUplift = plateauField * 180f * (0.3f + recipe.Mesa * 0.7f);

            float continentalRelief = mountainUplift + foothills + plateauUplift;
            float landBaseDepth = math.lerp(1500f, 380f, shelfMask);
            depth = math.lerp(depth, landBaseDepth, continentality);
            depth -= continentalRelief * continentality;

            // Broad geology noise injection
            float geologicalNoise = FractalSimplexNoise01(warpedPos * 0.00045f + new float2(4.2f, -1.8f), seed ^ 0x5D4E3C2Bu, 6);
            depth += (DiagGeoNoiseOff ? 0f : (geologicalNoise - 0.5f) * 160f * (1f - abyssPlainMask * 0.5f));
            if (stageDump == 2) { masks = default; return parameters.WaterSurfaceY - depth; } // STAGE 2: +continent relief (mtn/foothill/plateau/geoNoise)

            float ridgeBelt = ErodedRidge01(warpedNorm * 2.88f + new float2(4.1f, -3.7f), seed ^ 0x91E83B37u, 5);
            float ridgeMask = math.saturate(math.smoothstep(0.38f, 0.86f, ridgeBelt) * (1f - shelfMask * 0.42f) + plateRidgeMask * 0.95f);
            float oceanicRidgeGate = 1f - continentality;
            float billowMountains = ErodedRidge01(warpedPos * 0.00088f + new float2(-1.2f, 8.4f), seed ^ 0x3F2A1C9Bu, 5);
            depth -= billowMountains * parameters.RidgeHeightMeters * 0.65f * ridgeMask * oceanicRidgeGate;
            depth -= ridgeMask * parameters.RidgeHeightMeters * (0.58f + plateEdgeMask * 0.42f) * oceanicRidgeGate;
            if (stageDump == 3) { masks = default; return parameters.WaterSurfaceY - depth; } // STAGE 3: +ridges (ErodedRidge crest)

            float trenchBelt = RidgedMultifractal01(warpedNorm * 2.44f + new float2(0.4f, -0.6f), seed ^ 0x4B3A2C1Du, 4);
            float trenchMask = math.saturate(math.smoothstep(0.56f, 0.95f, trenchBelt) * (1f - shelfMask * 0.80f) + plateTrenchMask * 1.15f);
            if (DiagTrenchOff) trenchMask = 0f;
            // R29 FIX: Oceanic trench depth offset (1800m) MUST be gated by (1 - continentality)
            // so oceanic trenches cannot carve 1.8km cliffs across continental landmasses!
            float oceanicTrenchGate = (1f - continentality);
            depth += trenchMask * parameters.TrenchDepthMeters * (0.78f + plateEdgeMask * 0.58f) * oceanicTrenchGate;

            float faultNoise = RidgedMultifractal01(warpedNorm * 12.0f + new float2(-1.9f, 7.1f), seed ^ 0xCA97D1F3u, 3);
            float faultMask;
            float basinMask;

            if (DiagSoftMaskEdges)
            {
                float faultMaskRaw = math.smoothstep(0.35f, 0.95f, faultNoise);
                float faultEdgeNoise = FractalSimplexNoise01(
                    warpedPos * 0.0031f + new float2(-5.5f, 12.3f), seed ^ 0xCAFEBABEu, 2);
                faultMask = math.saturate(faultMaskRaw * (0.7f + faultEdgeNoise * 0.3f)
                    * (1f - shelfMask * 0.45f) + plateEdgeMask * 0.34f);

                float shelfEdgeNoise = FractalSimplexNoise01(
                    warpedPos * 0.0018f + new float2(8.8f, -3.1f), seed ^ 0xDEADBEEFu, 2);
                float shelfMaskFeathered = math.saturate(shelfMask + (shelfEdgeNoise - 0.5f) * 0.15f);
                basinMask = math.saturate((1f - shelfMaskFeathered)
                    * (1f - ridgeMask * 0.78f) * (1f - trenchMask * 0.52f));
            }
            else
            {
                faultMask = math.saturate(math.smoothstep(0.48f, 0.88f, faultNoise) * (1f - shelfMask * 0.45f) + plateEdgeMask * 0.34f);
                basinMask = math.saturate((1f - shelfMask) * (1f - ridgeMask * 0.78f) * (1f - trenchMask * 0.52f));
            }

            depth += (DiagFaultOff ? 0f : faultNoise * 95f);
            // R28 FIX 2: Oceanic basin depth offset MUST be gated by (1 - continentality)
            // so abyssal basin depth does not cut into continental landmasses as a 1.2km cliff overlay mask!
            float oceanicBasinGate = (1f - continentality);
            depth += basinMask * parameters.BasinDepthMeters * (0.54f + abyssPlainMask * 0.46f) * oceanicBasinGate;
            if (stageDump == 4) { masks = default; return parameters.WaterSurfaceY - depth; } // STAGE 4: +trench/fault/basin

            // Slope proxy declaration (used by feature generators below).
            // R27: trueMountainSlope from finite-difference gradient replaces blind mountainBelt —
            // fractures and talus now activate only where mountains are actually steep.
            float slopeProxy = math.saturate(shelfBreakMask * 0.82f + ridgeMask * 0.72f + faultMask * 0.65f + plateEdgeMask * 0.40f + trueMountainSlope);

            // FIX: Add low-frequency spatial noise to feather the transition edges.
            float maskFeather = FractalSimplexNoise01(warpedPos * 0.002f, seed ^ 0xFEA78E12u, 2) * 0.08f - 0.04f;

            // Mute all high-frequency noise on flat sediment areas, with a jagged, natural edge.
            float sedimentTranquilityMask = 1f - math.smoothstep(0.05f + maskFeather, 0.15f + maskFeather, slopeProxy);
            float steepRockMask = math.smoothstep(0.20f + maskFeather, 0.35f + maskFeather, slopeProxy);

            // =========================================================================
            // SYSTEM B: FEATURE GENERATORS (Burst-safe, deterministic, Budget-respecting)
            // =========================================================================

            // --- B5: FOLD BELTS (Corrugated parallel ridges) ---
            float foldMask = 0f;
            if (recipe.Folds > 0.01f)
            {
                float foldAngle = FractalSimplexNoise01(warpedNorm * 1.2f, seed ^ 0x3B1A2C4Du) * 3.14159f;
                float2 foldAxis = new float2(math.cos(foldAngle), math.sin(foldAngle));

                if (DiagFoldNonPeriodic)
                {
                    // R40 FIX: Use C2-smooth cosine wave instead of linear foldPhase with smoothstep(0.2, 0.8) kinks
                    // which injected 1-pixel thin derivative curves into the slope/hillshade maps on Stage 5.
                    float foldPhase = FractalSimplexNoise01(
                        warpedPos * 0.0012f + foldAxis * 3.7f,
                        seed ^ 0xF01D5EEDu, 3);
                    float foldWave = math.cos(foldPhase * 6.2831853f) * 0.5f + 0.5f;
                    float foldAsymmetry = math.pow(foldWave, 1.6f) * (0.3f + recipe.Folds * 0.7f);
                    foldMask = foldAsymmetry * recipe.Folds * continentality;
                    if (!DiagFoldsDunesOff)
                        depth -= (foldAsymmetry - 0.5f) * 240f * continentality * (1f - abyssPlainMask);
                }
                else
                {
                    float foldCoord = math.dot(warpedPos, foldAxis) * 0.0012f;
                    float foldPattern = math.sin(foldCoord + FractalSimplexNoise01(warpedPos * 0.0003f, seed ^ 0x91F2E3D4u) * 2.5f);
                    float foldAsymmetry = math.pow(math.saturate(foldPattern * 0.5f + 0.5f), 1.6f);
                    foldMask = foldAsymmetry * recipe.Folds * continentality;
                    if (!DiagFoldsDunesOff)
                        depth -= foldAsymmetry * 240f * recipe.Folds * continentality;
                }
            }

            // --- B6: VOLCANIC FIELDS (Cones, calderas, guyots) ---
            float volcanoMask = 0f;
            if (recipe.Volcanic > 0.01f)
            {
                float2 volcSample = warpedPos * 0.00018f;
                int2 volcCell = (int2)math.floor(volcSample);
                float2 volcFrac = volcSample - volcCell;

                float coneSum = 0f;
                float calderaSum = 0f;

                for (int vy = -1; vy <= 1; vy++)
                {
                    for (int vx = -1; vx <= 1; vx++)
                    {
                        int2 cell = volcCell + new int2(vx, vy);
                        float2 hash = Hash2(cell.x, cell.y, seed ^ 0x7E1A2B3Cu);
                        float dist = math.length(new float2(vx, vy) + hash - volcFrac);
                        
                        // Organic radial domain warp
                        float volcWarp = FractalSimplexNoise01(warpedPos * 0.0008f + hash, seed ^ 0x6B1A2C3Du, 2) * 0.4f - 0.2f;
                        
                        // SUM the exponents to create smooth, C-infinity metaball blending between adjacent volcanoes.
                        // This mathematically eliminates the 1-pixel Voronoi boundary cell crease!
                        float cone = math.exp(-(dist + volcWarp) * 4.2f);
                        float caldera = (1f - math.smoothstep(0.0f, 0.08f, dist + volcWarp * 0.5f)) * 0.35f;
                        
                        coneSum += cone;
                        calderaSum += caldera;
                    }
                }
                volcanoMask = math.saturate(coneSum - calderaSum) * recipe.Volcanic;
                if (!DiagVolcanoOff)
                    depth -= (coneSum * 380f - calderaSum * 120f) * recipe.Volcanic;
            }

            // --- B1: CRATERS (Impact + Collapse) ---
            float craterMask = 0f;
            if (recipe.Craters > 0.01f)
            {
                float craterGridSize = 2500f;
                int2 craterCell = new int2((int)math.floor(warpedPos.x / craterGridSize), (int)math.floor(warpedPos.y / craterGridSize));
                float craterDepthDelta = 0f;

                for (int cdz = -1; cdz <= 1; cdz++)
                {
                    for (int cdx = -1; cdx <= 1; cdx++)
                    {
                        int2 neighbor = craterCell + new int2(cdx, cdz);
                        uint h = Hash(neighbor.x, neighbor.y, unchecked((int)(seed ^ 0x9B3A21EFu)));
                        if (HashToUnitFloat(h ^ 0x12345678u) > (0.12f + recipe.Craters * 0.45f))
                            continue;

                        float cx = (neighbor.x + HashToUnitFloat(h ^ 0x87654321u)) * craterGridSize;
                        float cz = (neighbor.y + HashToUnitFloat(h ^ 0xA1B2C3D4u)) * craterGridSize;
                        float radius = math.lerp(120f, 950f, math.pow(HashToUnitFloat(h ^ 0x1A2B3C4Du), 2.2f));
                        float dist = math.length(new float2(warpedPos.x - cx, warpedPos.y - cz));
                        if (dist > radius * 1.8f)
                            continue;

                        float normalizedDist = dist / math.max(1f, radius);
                        float bowl = math.pow(1f - math.smoothstep(0f, 1f, normalizedDist), 1.55f);
                        
                        // C2-continuous bell curve using cosine to eliminate C1-discontinuity red slope arc
                        float rimDist = math.saturate(math.abs(normalizedDist - 1f) * 2.5f);
                        float rim = math.saturate(0.5f + 0.5f * math.cos(rimDist * 3.14159f)) * math.smoothstep(1.0f, 0.95f, rimDist);
                        float peak = math.smoothstep(0f, 1f, 1f - math.smoothstep(0f, radius * 0.16f, dist)) * math.smoothstep(450f, 850f, radius) * 0.35f;

                        craterDepthDelta += bowl * radius * 0.18f * recipe.Craters;
                        craterDepthDelta -= peak * radius * 0.10f * recipe.Craters;
                        craterDepthDelta -= rim * radius * 0.08f * recipe.Craters;
                        craterMask = math.max(craterMask, bowl * recipe.Craters);
                    }
                }
                depth += craterDepthDelta;
            }

            // --- B2: RIVERS & DENDRITIC CHANNELS (Asymmetric & Rugged Inland Canyons) ---
            float riverRegion = FractalSimplexNoise01(warpedPos * 0.00015f + new float2(-19.3f, 44.1f), seed ^ 0x1A2B3C4Du, 3);
            float riverGate = math.smoothstep(0.55f, 0.78f, riverRegion) * continentality * recipe.Rivers;

            float riverMask = 0f;
            if (riverGate > 0.01f)
            {
                // Domain warp in WORLD SPACE (meters), then scale together with base frequency (NO ALIASING!)
                float2 canyonWarp = new float2(
                    FractalSimplexNoise01(warpedPos * 0.0005f + new float2(12.1f, -5.5f), seed ^ 0x7E1A2B3Cu) * 2f - 1f,
                    FractalSimplexNoise01(warpedPos * 0.0005f + new float2(-9.2f, 18.4f), seed ^ 0x3C4D5E6Fu) * 2f - 1f) * 1200f;
                
                float2 warpedRiverPos = (warpedPos + canyonWarp) * 0.00025f;
                
                // Base canyon channel
                float dendritic = RidgedMultifractal01(warpedRiverPos, seed ^ 0x6DCD4A37u, 5);
                
                // Ragged outer rim
                float rimNoise = FractalSimplexNoise01(warpedPos * 0.003f, seed ^ 0xCAFE1234u, 3);
                float canyonRim = math.smoothstep(0.55f, 0.88f, dendritic) * (0.85f + rimNoise * 0.15f);
                
                // Asymmetric bank slope: 600m world-space offset for bank asymmetry
                float dendriticOffset = RidgedMultifractal01((warpedPos + canyonWarp + new float2(600f, -600f)) * 0.00025f, seed ^ 0x6DCD4A37u, 5);
                float bankAsymmetry = math.smoothstep(0.4f, 0.9f, dendriticOffset);
                
                // Gentle floor undulation (~500m features, no pixel carpet)
                float floorRoughness = BillowNoise01(warpedPos * 0.002f + new float2(7.7f, -3.3f), seed ^ 0x8899AABBu, 2) * 12f;
                
                float canyonFloor = math.smoothstep(0.60f, 0.99f, dendritic);
                riverMask = canyonRim * riverGate;
                
                // Deep cut influenced by asymmetry, minus the floor roughness
                float cutDepth = 280f * riverMask * canyonFloor * (0.6f + bankAsymmetry * 0.4f);
                depth += cutDepth - floorRoughness * canyonFloor * riverMask;
            }
            float canyonMask = riverMask; // Export for downstream

            // --- B3: LAKES & PLAYAS (Sediment-filled basins) ---
            float lakeRegion = FractalSimplexNoise01(warpedPos * 0.0002f + new float2(44.4f, 11.1f), seed ^ 0x55443322u, 3);
            float lakeGate = math.smoothstep(0.5f, 0.8f, lakeRegion) * continentality * recipe.Lakes;

            float lakeMask = 0f;
            if (lakeGate > 0.01f)
            {
                // Find natural regional depressions
                float bowlNoise = FractalSimplexNoise01(warpedPos * 0.0004f + new float2(-22.2f, 33.3f), seed ^ 0x99887766u, 4);
                lakeMask = math.smoothstep(0.55f, 0.85f, bowlNoise) * lakeGate;
                
                if (lakeMask > 0.01f)
                {
                    // Ragged, irregular shorelines
                    float shoreFeather = FractalSimplexNoise01(warpedPos * 0.005f, seed ^ 0xE4F5A6B7u, 3);
                    lakeMask *= (0.7f + shoreFeather * 0.3f);
                    
                    // Sediment level varies slightly across the continent but forms local flat planes
                    float localSedimentLevel = 450f + FractalSimplexNoise01(warpedPos * 0.0001f, seed ^ 0x5A5A5A5Au, 2) * 400f;
                    
                    // FILL valleys with sediment. math.min(depth, level) pulls deep valleys UP to the sediment level
                    depth = math.lerp(depth, math.min(depth, localSedimentLevel), lakeMask * 0.85f);
                    
                    // Subtle dry mud cracks/texture on the flat playa bed
                    float playaCracks = RidgedMultifractal01(warpedPos * 0.015f, seed ^ 0x6E01091Cu, 3);
                    depth += playaCracks * 4f * lakeMask;
                }
            }

            // --- B7: MESA TABLELANDS (flat caps, NOT height-quantised) ---
            float mesaMask = 0f;
            if (recipe.Mesa > 0.01f && continentality > 0.30f)
            {
                float mesaField = FractalSimplexNoise01(warpedNorm * 1.9f + new float2(7.8f, -14.2f), seed ^ 0x8C1B3D2Eu);
                mesaMask = math.smoothstep(0.58f, 0.74f, mesaField) * recipe.Mesa * continentality;
                float mesaWeight = math.smoothstep(0.0f, 0.15f, mesaMask) * mesaMask * 0.7f;
                if (mesaWeight > 0.0001f)
                {
                    // cap elevation varies per broad patch (a few discrete plateau levels), continuous in space
                    float capDatum = FractalSimplexNoise01(warpedNorm * 0.8f + new float2(-5.5f, 12.1f), seed ^ 0x2D9C4B7Au);
                    float capDepth = math.lerp(560f, 260f, capDatum); // flat-top depth for this patch
                    depth = math.lerp(depth, math.min(depth, capDepth), mesaWeight);
                }
            }

            // --- B8: DUNES / SEDIMENT BEDFORMS ---
            float duneMask = 0f;
            float dunePatch = math.smoothstep(0.40f, 0.70f, FractalSimplexNoise01(warpedPos * 0.0015f, seed ^ 0xD11E2233u, 3));
            float duneGate = math.saturate(recipe.Dunes * 0.8f + shelfMask * 0.6f * dunePatch - slopeProxy * 0.7f);
            float duneFade = math.smoothstep(0.0f, 0.15f, duneGate);

            if (duneFade > 0.0001f)
            {
                float duneDir = FractalSimplexNoise01(warpedNorm * 2.5f, seed ^ 0x4D3C2B1Au, 2) * 3.14159f;
                float2 duneAxis = new float2(math.cos(duneDir), math.sin(duneDir));
                float dunePhase = math.dot(warpedPos, duneAxis) * 0.025f + FractalSimplexNoise01(warpedPos * 0.004f, seed ^ 0x9A8B7C6Du, 2) * 1.5f;
                float duneWave = math.pow(0.5f - 0.5f * math.cos(dunePhase), 1.5f);
                
                duneMask = duneGate * duneFade;
                if (!DiagFoldsDunesOff)
                    depth += duneWave * 8.5f * duneMask; 
            }

            // --- B10: CORAL REEFS (Organic mounds, NO rings, C1 continuous) ---
            float reefMask = 0f;
            
            // 1. C1-Continuous Depth Gate (NO HARD IF-STATEMENTS ON DEPTH)
            // Grows smoothly between 15m and 380m depth. Fades out smoothly at the edges.
            float depthGate = math.smoothstep(380f, 300f, depth) * math.smoothstep(15f, 45f, depth);
            
            if (depthGate > 0.001f && recipe.Reefs > 0.01f)
            {
                // 2. Patchy clusters of reefs
                float reefNoise = FractalSimplexNoise01(warpedPos * 0.0015f + new float2(-31.4f, 88.2f), seed ^ 0x9E8D7C6Fu, 3);
                float reefPatch = math.smoothstep(0.50f, 0.75f, reefNoise);
                
                // 3. Organic coral mounds (NO SINE WAVES / NO RINGS)
                // High-frequency Simplex creates bubbly coral heads, pow(2) isolates them into distinct mounds
                float coralHeads = FractalSimplexNoise01(warpedPos * 0.025f, seed ^ 0xCC00AA11u, 3);
                coralHeads = math.pow(coralHeads, 2f); 
                
                reefMask = reefPatch * depthGate * recipe.Reefs;
                
                // 4. Add organic coral volume (up to 15m tall) smoothly gated by the mask
                depth -= coralHeads * 15f * reefMask;
            }

            // --- B4: STRATIFICATION (elevation benches strictly on steep rock walls) ---
            float strataMask = 0f;
            float hardRockMask = math.saturate(ridgeMask * 0.48f + faultMask * 0.30f + plateEdgeMask * 0.18f + slopeProxy * 0.28f - basinMask * 0.18f);
            if (!DiagStrataContourOff && (recipe.Strata > 0.01f || hardRockMask > 0.10f))
            {
                // Strict slope-gating (slopeProxy > 0.45): eliminates flat-area concentric rings on domes,
                // while producing real elevation benches (depth) on steep canyon and mountain walls.
                float slopeGate = math.smoothstep(0.35f, 0.65f, slopeProxy);
                float strataStrength = math.saturate((hardRockMask * 0.8f + recipe.Strata * 0.8f) * slopeGate - volcanoMask * 1.2f - trenchMask * 0.9f - (1f - continentality) * 1.0f);
                if (strataStrength > 0.03f)
                {
                    float patchLarge = FractalSimplexNoise01(warpedPos * 0.0011f + new float2(21.4f, -6.8f), seed ^ 0x51C0FFEEu);
                    float patchFeather = FractalSimplexNoise01(warpedPos * 0.0047f + new float2(-13.2f, 9.5f), seed ^ 0x1F33A7B9u);
                    float dropout = FractalSimplexNoise01(warpedPos * 0.0026f + new float2(3.1f, 17.7f), seed ^ 0x7C2E9D41u);
                    float broken = math.smoothstep(0.40f, 0.62f, patchLarge * 0.55f + patchFeather * 0.45f)
                                 * math.smoothstep(0.34f, 0.50f, dropout);
                    strataMask = strataStrength * broken;

                    if (strataMask > 0.04f)
                    {
                        // Real elevation-based strata (snaps depth to horizontal step-and-riser benches)
                        float tiltDir = FractalSimplexNoise01(warpedNorm * 1.3f, seed ^ 0x5B17E3A1u) * 6.2831853f;
                        float2 tiltAxis = new float2(math.cos(tiltDir), math.sin(tiltDir));
                        float tilt = math.dot(tiltAxis, warpedPos) * 0.06f;
                        float layerScale = math.lerp(22f, 46f, FractalSimplexNoise01(warpedNorm * 0.9f + new float2(4.4f, 4.4f), seed ^ 0x2E71C4B3u));
                        float hPhase = (depth + tilt) / layerScale;
                        float f = math.frac(hPhase);
                        float bench = math.smoothstep(0.30f, 0.70f, f);
                        float snapped = (math.floor(hPhase) + bench) * layerScale - tilt;
                        depth = math.lerp(depth, snapped, strataMask * 0.5f);
                    }
                }
            }

            // --- B9: FRACTURED WALLS (Steep slope blocky detail) ---
            float mesoFractureMask = math.saturate(hardRockMask * 0.8f + slopeProxy * 0.4f) * steepRockMask;
            float intermediateErosionA = FractalSimplexNoise01(warpedPos * 0.006f + new float2(-8.2f, 15.4f), seed ^ 0x6E1A2B3Cu, 4);
            float intermediateErosionB = FractalSimplexNoise01(warpedPos * 0.018f + new float2(12.7f, -3.1f), seed ^ 0x8C3B1A4Du, 3);
            float mesoFractureDelta = ((intermediateErosionA * 0.6f + intermediateErosionB * 0.4f) * 2f - 1f) * 45f;
            if (!DiagMesoFractureOff)
                depth += mesoFractureDelta * mesoFractureMask * (1f - abyssPlainMask * 0.6f);

            // TIER 4: Talus & Slump
            float concaveToe = math.saturate((basinMask * 1.5f + canyonMask * 1.2f + shelfToe * 0.84f + 0.1f) * (ridgeMask * 0.75f + faultMask * 0.62f + shelfBreakMask * 0.66f + 0.1f));
            float talusMask = math.saturate(math.smoothstep(0.10f, 0.62f, slopeProxy) * (1f - math.smoothstep(0.74f, 0.98f, slopeProxy)) * concaveToe);
            float talusC = BillowNoise01(warpedPos * 0.020f + new float2(3.3f, -7.2f), seed ^ 0xE70D1A5Bu, 3);
            float talusF = BillowNoise01(warpedPos * 0.071f + new float2(-5.0f, 1.7f), seed ^ 0xC3F19802u, 2);
            if (!DiagTalusOff)
                depth += ((talusC * 0.70f + talusF * 0.30f) * 2f - 1f) * math.lerp(5f, 15f, talusMask) * talusMask;

            // =========================================================================
            // SOFT CEILING: Applied AFTER all features, compressing peaks smoothly
            // =========================================================================
            if (depth < -260f)
            {
                float over = -260f - depth;
                float compressed = 340f * (1f - math.exp(-over / 340f));
                depth = -260f - compressed;
            }
            depth = math.clamp(depth, -620f, parameters.HadalDepthMeters);

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
                Terrace = math.saturate(mesaMask * 0.6f + strataMask * 0.4f),
                Slump = math.saturate(talusMask + shelfToe * 0.18f),
                ProvinceType = (float)primaryProvinceTypeIndex / 7.0f,
                ProvinceBlend = provinceBlend,
                River = math.saturate(riverMask),
                Lake = math.saturate(lakeMask),
                Strata = math.saturate(strataMask),
                Fold = math.saturate(foldMask),
                Volcano = math.saturate(volcanoMask),
                Mesa = math.saturate(mesaMask),
                Dune = math.saturate(duneMask),
                Continentality = math.saturate(continentality),
                Reef = math.saturate(reefMask)
            };

            return parameters.WaterSurfaceY - depth;
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
            float minDistSq = 64.0f;
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
            float2 p = sample;
            for (int octave = 0; octave < octaves; octave++)
            {
                float ang = 0.5f + octave * 0.7548776662f + HashToUnitFloat(seed ^ (uint)(octave * 0x9E3779B9)) * 6.2831853f;
                float sa = math.sin(ang), ca = math.cos(ang);
                p = math.mul(new float2x2(ca, -sa, sa, ca), p);

                if (filterWidth > 0f && octave > 0 && (domainScale / frequency) < filterWidth)
                    break;
                total += SimplexNoise01(p * frequency, seed + (uint)octave * 0x9E3779B9u) * amplitude;
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
            float weight = 1f;
            float2 p = sample;
            for (int octave = 0; octave < octaves; octave++)
            {
                float ang = 0.5f + octave * 0.7548776662f + HashToUnitFloat(seed ^ (uint)(octave * 0x9E3779B9)) * 6.2831853f;
                float sa = math.sin(ang), ca = math.cos(ang);
                p = math.mul(new float2x2(ca, -sa, sa, ca), p);

                uint layerSeed = seed + (uint)octave * 0x9E3779B9u;
                float seedOffsetX = HashToUnitFloat(layerSeed ^ 0x9E3779B9u) * 16f - 8f;
                float seedOffsetY = HashToUnitFloat(layerSeed ^ 0x334EAA71u) * 16f - 8f;
                float snoiseVal = noise.snoise(p * frequency + new float2(seedOffsetX, seedOffsetY));
                
                float n = 1f - math.abs(snoiseVal);
                n = n * n;
                if (DiagRidgedAsFbm || DiagRidgedAsFbmMountain) n = snoiseVal * 0.5f + 0.5f; // R12-A / R17-A: plain fBm, NO crest line
                n *= weight;
                weight = (DiagNoiseBroadband || DiagRidgedAsFbm || DiagRidgedAsFbmMountain) ? 1f : math.saturate(n * 2f);

                total += n * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.0f;
            }

            return total / math.max(0.0001f, norm);
        }

        public static float ErodedRidge01(float2 sample, uint seed, int octaves = 5)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float total = 0f;
            float norm = 0f;
            float weight = 1f;
            float2 p = sample;
            for (int octave = 0; octave < octaves; octave++)
            {
                float ang = 0.5f + octave * 0.7548776662f + HashToUnitFloat(seed ^ (uint)(octave * 0x9E3779B9)) * 6.2831853f;
                float sa = math.sin(ang), ca = math.cos(ang);
                p = math.mul(new float2x2(ca, -sa, sa, ca), p);

                uint layerSeed = seed + (uint)octave * 0x9E3779B9u;
                float seedOffsetX = HashToUnitFloat(layerSeed ^ 0x9E3779B9u) * 16f - 8f;
                float seedOffsetY = HashToUnitFloat(layerSeed ^ 0x334EAA71u) * 16f - 8f;
                float snoiseVal = noise.snoise(p * frequency + new float2(seedOffsetX, seedOffsetY));

                float n = 1f - math.abs(snoiseVal);
                n = n * n * (3f - 2f * n);
                if (DiagRidgedAsFbm || DiagRidgedAsFbmMountain) n = snoiseVal * 0.5f + 0.5f; // R12-A / R17-A: plain fBm, NO crest line
                n *= weight;
                weight = (DiagNoiseBroadband || DiagRidgedAsFbm || DiagRidgedAsFbmMountain) ? 1f : math.saturate(0.35f + n * 0.9f);

                total += n * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.0f;
            }

            return total / math.max(0.0001f, norm);
        }

        public static float BillowNoise01(float2 sample, uint seed, int octaves = 5)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float total = 0f;
            float norm = 0f;
            float2 p = sample;
            for (int octave = 0; octave < octaves; octave++)
            {
                float ang = 0.5f + octave * 0.7548776662f + HashToUnitFloat(seed ^ (uint)(octave * 0x9E3779B9)) * 6.2831853f;
                float sa = math.sin(ang), ca = math.cos(ang);
                p = math.mul(new float2x2(ca, -sa, sa, ca), p);

                uint layerSeed = seed + (uint)octave * 0x9E3779B9u;
                float seedOffsetX = HashToUnitFloat(layerSeed ^ 0x9E3779B9u) * 16f - 8f;
                float seedOffsetY = HashToUnitFloat(layerSeed ^ 0x334EAA71u) * 16f - 8f;
                float snoiseVal = noise.snoise(p * frequency + new float2(seedOffsetX, seedOffsetY));
                
                float n = math.abs(snoiseVal);
                
                total += n * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.0f;
            }

            return total / math.max(0.0001f, norm);
        }

        private static float SimplexNoise01(float2 sample, uint seed)
        {
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
            public float ProvinceType;
            public float ProvinceBlend;
            public float River;
            public float Lake;
            public float Strata;
            public float Fold;
            public float Volcano;
            public float Mesa;
            public float Dune;
            public float Continentality;
            public float Reef;
        }
    }
}
