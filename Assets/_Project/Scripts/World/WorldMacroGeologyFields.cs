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
                    return new ProvinceRecipe { Craters = 0.05f, Rivers = 0.00f, Lakes = 0.00f, Strata = 0.10f, Folds = 0.00f, Volcanic = 0.10f, Mesa = 0.00f, Dunes = 0.30f, Reefs = 0.80f, BaseRough = 0.15f };
                case 1: // CRATERED_HIGHLANDS
                    return new ProvinceRecipe { Craters = 1.00f, Rivers = 0.10f, Lakes = 0.00f, Strata = 0.30f, Folds = 0.00f, Volcanic = 0.10f, Mesa = 0.20f, Dunes = 0.00f, Reefs = 0.50f, BaseRough = 0.40f };
                case 2: // RIVER_LOWLANDS
                    return new ProvinceRecipe { Craters = 0.10f, Rivers = 1.00f, Lakes = 0.80f, Strata = 0.50f, Folds = 0.10f, Volcanic = 0.00f, Mesa = 0.10f, Dunes = 0.20f, Reefs = 0.40f, BaseRough = 0.30f };
                case 3: // FOLDED_MOUNTAINS
                    return new ProvinceRecipe { Craters = 0.10f, Rivers = 0.30f, Lakes = 0.00f, Strata = 0.70f, Folds = 1.00f, Volcanic = 0.20f, Mesa = 0.00f, Dunes = 0.00f, Reefs = 0.10f, BaseRough = 0.60f };
                case 4: // RIFT_VALLEY
                    return new ProvinceRecipe { Craters = 0.10f, Rivers = 0.40f, Lakes = 0.30f, Strata = 0.40f, Folds = 0.30f, Volcanic = 0.80f, Mesa = 0.00f, Dunes = 0.00f, Reefs = 0.20f, BaseRough = 0.50f };
                case 5: // VOLCANIC_FIELD
                    return new ProvinceRecipe { Craters = 0.40f, Rivers = 0.10f, Lakes = 0.00f, Strata = 0.20f, Folds = 0.00f, Volcanic = 1.00f, Mesa = 0.10f, Dunes = 0.10f, Reefs = 0.30f, BaseRough = 0.50f };
                case 6: // MESA_TABLELANDS
                    return new ProvinceRecipe { Craters = 0.50f, Rivers = 0.30f, Lakes = 0.20f, Strata = 1.00f, Folds = 0.10f, Volcanic = 0.00f, Mesa = 1.00f, Dunes = 0.10f, Reefs = 0.20f, BaseRough = 0.30f };
                case 7: // DUNE_SEA
                    return new ProvinceRecipe { Craters = 0.00f, Rivers = 0.00f, Lakes = 0.00f, Strata = 0.20f, Folds = 0.00f, Volcanic = 0.00f, Mesa = 0.00f, Dunes = 1.00f, Reefs = 0.60f, BaseRough = 0.20f };
                default:
                    return new ProvinceRecipe { Craters = 0.30f, Rivers = 0.10f, Lakes = 0.10f, Strata = 0.20f, Folds = 0.10f, Volcanic = 0.30f, Mesa = 0.10f, Dunes = 0.10f, Reefs = 0.50f, BaseRough = 0.30f };
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
        // R99: 11 -> 12. Terrain and cave GEOMETRY changed, so the artifact identity must change with it:
        //   R98 HydraulicErosionJob   — direction integration in world-slope units (dendritic branching),
        //                               bilinear deposit weights fixed (removed 20-60x mass amplification),
        //                               droplet seeding moved to the write window.
        //   R98 ThermalWeathering     — mass-conserving border guard (removed the 1-pixel perimeter trench).
        //   R99 AbyssalShelf          — one height function instead of two disagreeing ones; meso amplitude
        //                               no longer ramps by LOD or by position inside the chunk.
        //   R99 live cave SDF         — exactly wrap-periodic field, unfolded strata domain, constants
        //                               matched to the canonical carve job.
        //
        // MIGRATION STATUS — OPEN. SaveManager detects the mismatch (CheckProceduralTerrainMacroMismatch)
        // but currently only warns and continues loading: a pre-R99 save will load its player position,
        // structures and voxel carve deltas against terrain that no longer has that shape. A real
        // migrate-or-reject route is still required before shipping; see Docs\AgentTasks\.
        public const uint ArtifactVersion = 12u;

        // BUILD SENTINEL: proves which compiled version the atlas actually ran. If the atlas report
        // does NOT print this exact string, Unity executed a STALE assembly (cache/no reload), not
        // this source. Bump the suffix every edit round.
        public static string BuildSentinel => "SENTINEL_R94_2026-07-26_PURE_GPU_NO_CPU_FAKES";

        // Geology forensic switches (R8-R39). These are deliberately `const`, not `static readonly`:
        // Burst and IL2CPP fold a const away completely, so a disabled probe costs zero instructions
        // in the shipped build. A `static readonly` would survive as a real load-and-branch in the
        // inner terrain loop and is not reliably Burst-foldable. Toggle a probe by editing the value
        // here and recompiling.
        //
        // These previously existed as two byte-identical copies under `#if UNITY_EDITOR` / `#else`.
        // The split served no purpose (verified: all 17 declarations matched exactly) and was a live
        // divergence trap - editing one arm while investigating would silently change editor
        // behaviour only, so a probe could "prove" something in the editor that was never true in a
        // player build. Collapsed to a single definition so editor and player cannot disagree.
        //
        // Consequence, accepted knowingly: while a switch sits at its shipped value the opposite
        // branch is provably dead and the compiler reports CS0162. That is correct and expected. It
        // is suppressed narrowly at the three sites where the dead arm cannot be folded into an
        // expression, never file-wide, so a genuine unreachable-code bug elsewhere still surfaces.

        // R17 STAGE-LOCALIZED FIXES:
        public const bool DiagRidgedAsFbmMountain = true;
        public const bool DiagFoldNonPeriodic     = true;
        public const bool DiagStrataNonPeriodic   = false; // R39: Real elevation-based strata snapping
        public const bool DiagSoftMaskEdges       = true;

        public const bool DiagStrataContourOff = false; // R14: real terrain (was OFF R8-R13 for isolation)
        public const bool DiagPlateSeamOff = false;     // R21: PLATE CLEARED — not the primary source
        public const bool DiagShelfBreakOff = false;    // R22: SHELFBREAK CLEARED — not the primary source

        public const int DiagRawProbe = 0; // R14: OFF — probe done its job.

        public const bool DiagTrenchOff  = false; // R11: restored (exonerated in R9)
        public const bool DiagVolcanoOff = false; // R11: restored (exonerated in R9)
        public const bool DiagFaultOff   = false; // R11: restored (exonerated in R9)
        public const bool DiagMesoFractureOff = false; // R11: restored
        public const bool DiagTalusOff        = false; // R11: restored
        public const bool DiagGeoNoiseOff     = false; // R11: restored

        public const bool DiagNoiseBroadband = false;
        public const bool DiagRidgedAsFbm  = false;
        public const bool DiagFoldsDunesOff = false;
        // R13 RAW PRIMITIVE PROBE. Pattern across R8-R12: removing any FEATURE makes zebra/seam WORSE or
        // unchanged, and zebra appears even on FLAT tiles (P5 1km slope 0.1%, hatch 4.33). Conclusion: the
        // artifact is NOT any added feature — it is intrinsic to the FOUNDATION every term shares:
        // noise.snoise and/or the domain warp. We never tested the bare primitive in 5 rounds. These
        // early-return probes bypass ALL geology and output pure noise so we measure where striping is born:
        //   0 = off (normal pipeline)

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

        public static WorldMacroGeologySample Evaluate(double absoluteX, double absoluteZ, in WorldMacroGeologyParams parameters)
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
            double absoluteX,
            double absoluteZ,
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
            double absoluteX,
            double absoluteZ,
            in WorldMacroGeologyParams parameters)
        {
            if (!TrySanitizeParams(in parameters, out WorldMacroGeologyParams p))
                return default;

            double probe = math.max(1.0, (double)p.DetailProbeMeters);
            float heightC = EvaluateHeightMeters(absoluteX, absoluteZ, in p, out MacroMasks masks);

            float west    = EvaluateHeightMeters(absoluteX - probe, absoluteZ, in p, out _);
            float east    = EvaluateHeightMeters(absoluteX + probe, absoluteZ, in p, out _);
            float south   = EvaluateHeightMeters(absoluteX, absoluteZ - probe, in p, out _);
            float north   = EvaluateHeightMeters(absoluteX, absoluteZ + probe, in p, out _);

            float safeProbe = math.max(0.001f, (float)probe);
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
            double absoluteX,
            double absoluteZ,
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
            float erosionVeins = RidgedMultifractal01(new float2((float)absoluteX, (float)absoluteZ) * 0.00042f + new float2(13.1f, -8.4f), p.Seed ^ 0xA511E9B3u, 4);
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

        public static float EvaluateHeightMeters(double absoluteX, double absoluteZ, in WorldMacroGeologyParams parameters)
        {
            if (!TrySanitizeParams(in parameters, out WorldMacroGeologyParams p))
                return 0f;

            return EvaluateHeightMeters(absoluteX, absoluteZ, in p, out _);
        }

        public static int2 ResolveChunkCoord(double absoluteX, double absoluteZ, in WorldMacroGeologyParams parameters)
        {
            double chunkSize = math.max(128.0, (double)parameters.ChunkSizeMeters);
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
            MixHash(ref hash, math.asuint(math.round(chunkSizeMeters)));
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
                    // math.md section 7: rank in SQUARED space. sqrt is monotonic, so nearest and
                    // second-nearest are unchanged, and the two survivors are rooted once after the
                    // loop instead of nine times inside it. The 1.5 cull also moves ahead of the sqrt,
                    // so out-of-range cells now cost neither the root nor the exp.
                    float distSq = math.lengthsq(sampleP - center);
                    if (distSq < f1) { f2 = f1; f1 = distSq; bestCell = cell; }
                    else if (distSq < f2) { f2 = distSq; }

                    const float provinceCullRadius = 1.5f;
                    if (distSq > provinceCullRadius * provinceCullRadius) continue;

                    // SUPPRESSION (math.sqrt): owner WorldMacroGeologyFields, reason = the exp falloff
                    // and smoothstep taper both need true metric distance; tier = all; verified by the
                    // 160k-sample height checksum recorded in the commit that introduced this.
                    float dist = math.sqrt(distSq);
                    float w = math.exp(-provHardness * dist) * math.smoothstep(1.5f, 1.0f, dist);
                    ProvinceRecipe r = ProvinceRecipe.GetRecipe(SelectGeologicalType(cell, continentality, plateEdgeMask, seed));
                    aCr += r.Craters * w; aRi += r.Rivers * w; aLa += r.Lakes * w; aSt += r.Strata * w;
                    aFo += r.Folds * w; aVo += r.Volcanic * w; aMe += r.Mesa * w; aDu += r.Dunes * w; aBr += r.BaseRough * w;
                    wSum += w;
                }
            }

            float inv = 1f / math.max(1e-6f, wSum);
            primaryTypeIndex = SelectGeologicalType(bestCell, continentality, plateEdgeMask, seed); // atlas colour only
            // f1/f2 were ranked squared inside the loop; take the two roots here, once.
            f1 = f1 < float.MaxValue ? math.sqrt(f1) : f1;
            f2 = f2 < float.MaxValue ? math.sqrt(f2) : f2;
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

        public static float EvaluateHeightMeters(double absoluteX, double absoluteZ, in WorldMacroGeologyParams parameters, out MacroMasks masks)
        {
            return EvaluateHeightMeters(absoluteX, absoluteZ, in parameters, out masks, 0);
        }

        // R16 STAGE DUMP: return the height with depth accumulation stopped after stage N, so ONE build
        // renders the depth field after each pipeline stage and we SEE which stage introduces the zebra /
        // rings / hairline. 0 = full pipeline (normal). Non-zero early-returns raw (WaterSurfaceY - depth).
        //   1=base(shelf/abyss)  2=+continentRelief(mtn/foothill/plateau)  3=+ridges  4=+trench/fault/basin
        //   5=+fold  6=+volcano/crater/river/lake/mesa/dune  7=+strata  8=+mesoFracture/talus (=full)
        public static float EvaluateHeightMeters(double absoluteX, double absoluteZ, in WorldMacroGeologyParams parameters, out MacroMasks masks, int stageDump)
        {
            // Use double arithmetic for macro scale and bounds
            double extentD = math.max((double)MinimumWorldExtentMeters, parameters.WorldExtentMeters);
            double2 posD = new double2(absoluteX, absoluteZ);
            double2 normD = posD / extentD; 
            uint seed = parameters.Seed; 

            // Cast to float ONLY for the noise evaluation (which takes float2)
            float2 normF = (float2)normD;
            float2 pos = (float2)posD;
            float2 norm = normF;

            // TIER 1: tectonic warp
            float2 tectonicWarp = new float2(
                FractalSimplexNoise01(normF * 0.62f + new float2(11.7f, -3.9f), seed ^ 0xB5297A4Du) * 2f - 1f,
                FractalSimplexNoise01(normF * 0.58f + new float2(-2.1f, 8.6f), seed ^ 0x4CF5AD43u) * 2f - 1f) * 4500f;

            float2 mesoWarp = new float2(
                FractalSimplexNoise01(normF * 7.5f + new float2(-17.2f, 29.3f), seed ^ 0x68E31DA4u) * 2f - 1f,
                FractalSimplexNoise01(normF * 8.1f + new float2(23.5f, -19.7f), seed ^ 0x8A1F3C4Du) * 2f - 1f) * 120f;

            // ADD the warp to the pristine double-precision world position
            double2 warpedPosD = posD + (double2)tectonicWarp + (double2)mesoWarp;

            // R100 FIX (512 m terrain cliff): the per-chunk anchor that used to be subtracted here was
            // `floor(posD / ChunkSizeMeters) * ChunkSizeMeters`. Its stated intent was to preserve ULP
            // precision at large absolute coordinates, which is a real problem - but every consumer of
            // `warpedPos` below is NON-PERIODIC fBm on a global simplex lattice, so subtracting a
            // staircase translated the entire noise domain by one chunk at every 512 m boundary and
            // fully decorrelated the field across it. Measured on the shipped code at x = 776704
            // (= floor(777000/512)*512): a 34.46 m height step between samples 0.25 m apart, against
            // 0.07 m of legitimate variation mid-chunk - a 475x discontinuity, driven mainly by
            // mountainUplift (x950) and the hill terms.
            //
            // The anchor is removed rather than resized: no anchor value can be correct here, because
            // any non-zero staircase is a domain translation and the lattice has no matching period.
            // The precision the anchor was protecting is genuinely lost by this cast - at 777 km a
            // float2 quantises position to 0.0625 m, which after the frequency multiply is a sub-
            // centimetre height tread. Trading a 34.46 m cliff for that is unambiguously correct.
            // Recovering the last centimetre is a separate, larger change: route the absolute
            // `warpedPosD` through the double-precision noise entry points that already exist in this
            // file (RidgedMultifractal01/ErodedRidge01/BillowNoise01 all have double2 overloads;
            // FractalSimplexNoise01's equivalent is DoubleFractalSimplexNoise01). That touches ~29 call
            // sites and must be verified by the same numerical continuity probe, so it is deliberately
            // not bundled with this one-line correctness fix.
            float2 warpedPos = (float2)warpedPosD;
            float2 warpedNorm = (float2)(warpedPosD / extentD);

#if UNITY_EDITOR
            // R13 RAW PRIMITIVE PROBE: bypass all geology, emit pure noise to locate the striping source.
            // Dead while DiagRawProbe == 0 (its shipped value). The probe early-returns, so unlike the
            // DiagPlateSeamOff/DiagTrenchOff switches it cannot be folded into an expression.
#pragma warning disable CS0162 // Unreachable while the const probe is off - intentional, see declarations.
            if (DiagRawProbe != 0)
            {
                masks = default;
                float raw;
                if (DiagRawProbe == 1)
                    raw = noise.snoise(((float2)posD) * 0.0009f + new float2(7.3f, -4.1f));   // bare simplex, UNWARPED
                else if (DiagRawProbe == 2)
                    raw = noise.snoise(warpedPos * 0.0009f + new float2(7.3f, -4.1f));      // bare simplex, WARPED
                else
                    raw = FractalSimplexNoise01(((float2)posD) * 0.0009f, seed, 5) * 2f - 1f;  // 5-octave, UNWARPED
                return raw * 400f;
            }
#pragma warning restore CS0162
#endif

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
            // Folded, not branched: a const-false `if` body is unreachable code (CS0162), whereas a
            // conditional expression on the same const folds to the untouched value with no warning
            // and no emitted branch. Same codegen, no suppression needed.
            plateRidgeMask = DiagPlateSeamOff ? 0f : plateRidgeMask;
            plateTrenchMask = DiagPlateSeamOff ? 0f : plateTrenchMask;
            plateEdgeMask = DiagPlateSeamOff ? 0f : plateEdgeMask;

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

            // ShelfDepthMeters, not a hardcoded 120. The parameter existed and was ACCEPTED but never reached
            // this evaluator: ShelfDepthMeters (default 90f at :49) was read only at :257-258, inside
            // Sanitize, purely as a floor for AbyssDepthMeters. That is the "parameter accepted then ignored"
            // silent-degeneracy class this project's own rules name, and it meant the shallowest shelf the
            // generator could emit was 120 m no matter what anyone authored.
            //
            // 90 is the intended figure and this same file already says so: :369 computes its reef band as
            // abs(WaterSurfaceY - heightMeters - 90f). The height evaluator and the reef band disagreed by 30 m.
            //
            // Cannot invert: Sanitize at :258 guarantees AbyssDepthMeters >= ShelfDepthMeters + 500, so the
            // lerp endpoints stay ordered for any authored value.
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
            float mountainUplift = mountainField * mountainBelt * 950f * recipe.BaseRough;
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
            // Medium hills: isolated island clusters via Simplex clump mask (R43: 0.03 baseline prevents sterile plastic valleys)
            float clumpNoiseMed = FractalSimplexNoise01(warpedPos * 0.0018f, seed ^ 0xC1D2E3F4u, 3);
            float clumpMaskMed  = math.max(0.03f, math.smoothstep(0.4f, 0.7f, clumpNoiseMed)) * hillinessMask;

            // Small outcrops: isolated, rugged patches via Fractal Simplex (R43: 0.05 baseline prevents sterile plastic valleys)
            float clumpNoiseSmall = FractalSimplexNoise01(warpedPos * 0.004f + new float2(-15.3f, 8.8f), seed ^ 0xA4B5C6D7u, 4);
            float clumpMaskSmall  = math.max(0.05f, math.smoothstep(0.65f, 0.90f, clumpNoiseSmall)) * hillinessMask;

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
            trenchMask = DiagTrenchOff ? 0f : trenchMask; // folded const switch; see DiagPlateSeamOff note
            // R29 FIX: Oceanic trench depth offset (1800m) MUST be gated by (1 - continentality)
            // so oceanic trenches cannot carve 1.8km cliffs across continental landmasses!
            float oceanicTrenchGate = (1f - continentality);
            depth += trenchMask * parameters.TrenchDepthMeters * (0.78f + plateEdgeMask * 0.58f) * oceanicTrenchGate;

            // THREAT 2 (Auditor #7): Subduction Crease Asymmetry and Depth Reduction
            float creaseWarp = FractalSimplexNoise01(warpedPos * 0.005f, seed ^ 0x11223344u, 2) * 0.2f;
            // Left as math.pow deliberately. ((x*x)*(x*x)*(x*x)) is cheaper, but it rounds differently
            // from pow and shifted the 160k-sample height checksum by 1.5e-4 (~1 nm per sample). The
            // saving is once per sample and was not measurable above machine noise, so it is not worth
            // perturbing generated terrain. Revisit only with a profiler capture showing this matters.
            float trenchCrease = math.pow(math.saturate(trenchMask + creaseWarp), 6.0f);
            depth += trenchCrease * 250f * oceanicTrenchGate;

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
            // R17 A/B arm: the hard-edged original, kept for comparison against the feathered path
            // above. Dead while DiagSoftMaskEdges == true (its shipped value). Multi-statement with
            // local declarations, so it cannot be folded into an expression.
#pragma warning disable CS0162 // Unreachable while the const switch selects the other arm.
            else
            {
                faultMask = math.saturate(math.smoothstep(0.48f, 0.88f, faultNoise) * (1f - shelfMask * 0.45f) + plateEdgeMask * 0.34f);
                basinMask = math.saturate((1f - shelfMask) * (1f - ridgeMask * 0.78f) * (1f - trenchMask * 0.52f));
            }
#pragma warning restore CS0162

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
            float maskFeather = DoubleFractalSimplexNoise01(warpedPosD * 0.002, seed ^ 0xFEA78E12u, 2) * 0.08f - 0.04f;

            // Mute high-frequency noise on flat sediment areas, with a natural edge.
            float sedimentTranquilityMask = 1f - math.smoothstep(0.05f + maskFeather, 0.15f + maskFeather, slopeProxy);
            
            // R43 STEP 2: Continuous power curve instead of smoothstep threshold switch.
            // Rocks grow organically out of the slope as steepness increases without sharp activation lines.
            float steepRockMask = math.saturate(math.pow(slopeProxy * 1.8f, 1.5f));

            // =========================================================================
            // SYSTEM B: FEATURE GENERATORS (Burst-safe, deterministic, Budget-respecting)
            // =========================================================================

            // --- B5: FOLD BELTS (Corrugated parallel ridges) ---
            float foldMask = 0f;
            float foldFade = math.smoothstep(0.01f, 0.03f, recipe.Folds);
            if (foldFade > 0.001f)
            {
                float foldAngle = DoubleFractalSimplexNoise01((double2)warpedNorm * 1.2, seed ^ 0x3B1A2C4Du, 1) * 3.14159f;
                float2 foldAxis = new float2(math.cos(foldAngle), math.sin(foldAngle));

                if (DiagFoldNonPeriodic)
                {
                    // R40 FIX: Use C2-smooth cosine wave instead of linear foldPhase with smoothstep(0.2, 0.8) kinks
                    // which injected 1-pixel thin derivative curves into the slope/hillshade maps on Stage 5.
                    float foldPhase = DoubleFractalSimplexNoise01(
                        warpedPosD * 0.0012 + (double2)(foldAxis * 3.7f),
                        seed ^ 0xF01D5EEDu, 3);
                    float foldWave = math.cos(foldPhase * 6.2831853f) * 0.5f + 0.5f;
                    float foldAsymmetry = math.pow(foldWave, 1.6f) * (0.3f + recipe.Folds * 0.7f);
                    foldMask = foldAsymmetry * recipe.Folds * continentality * foldFade;
                    if (!DiagFoldsDunesOff)
                        depth -= (foldAsymmetry - 0.5f) * 240f * continentality * (1f - abyssPlainMask) * foldFade;
                }
                // R17/R40 A/B arm: the original linear-phase fold, kept for comparison against the
                // C2-smooth cosine path above. Dead while DiagFoldNonPeriodic == true (its shipped
                // value). Multi-statement with local declarations, so it cannot be folded.
#pragma warning disable CS0162 // Unreachable while the const switch selects the other arm.
                else
                {
                    float foldCoord = math.dot(warpedPos, foldAxis) * 0.0012f;
                    float foldPattern = math.sin(foldCoord + DoubleFractalSimplexNoise01(warpedPosD * 0.0003, seed ^ 0x91F2E3D4u, 1) * 2.5f);
                    float foldAsymmetry = math.pow(math.saturate(foldPattern * 0.5f + 0.5f), 1.6f);
                    foldMask = foldAsymmetry * recipe.Folds * continentality * foldFade;
                    if (!DiagFoldsDunesOff)
                        depth -= (foldAsymmetry - 0.5f) * 240f * continentality * foldFade;
                }
#pragma warning restore CS0162
            }

            // --- B6: VOLCANIC FIELDS (Cones, calderas, guyots) ---
            float volcanoMask = 0f;
            float volcFade = math.smoothstep(0.01f, 0.03f, recipe.Volcanic);
            if (volcFade > 0.001f)
            {
                double2 volcSampleD = warpedPosD * 0.00018;
                int2 volcCell = (int2)math.floor((float2)volcSampleD);
                float2 volcFrac = (float2)volcSampleD - volcCell;

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
                        float volcWarp = DoubleFractalSimplexNoise01(warpedPosD * 0.0008 + (double2)hash, seed ^ 0x6B1A2C3Du, 2) * 0.4f - 0.2f;
                        
                        // R46 STEP 3: Breached calderas (asymmetric crater walls allow submarine entry)
                        float breachNoise = DoubleFractalSimplexNoise01(warpedPosD * 0.002 + (double2)(hash * 100f), seed ^ 0xBEEF1234u, 2);
                        float breach = math.smoothstep(0.3f, 0.6f, breachNoise); // 0 on breached side, 1 on intact side
                        
                        // R65 FIX: Use lower-frequency C2-smooth Simplex noise for volcano gullies instead of sharp RidgedMultifractal creases
                        float gullyNoise = DoubleFractalSimplexNoise01(warpedPosD * 0.003 + (double2)(hash * 50f), seed ^ 0x99887766u, 3);
                        float gullyErosion = gullyNoise * math.smoothstep(0.0f, 0.4f, dist) * math.smoothstep(1.2f, 0.6f, dist);
                        
                        // SUM the exponents to create smooth, C-infinity metaball blending between adjacent volcanoes.
                        float cone = math.exp(-(dist + volcWarp) * 4.2f) * (0.65f + 0.35f * breach) * (1f - gullyErosion * 0.6f);
                        float caldera = (1f - math.smoothstep(0.0f, 0.08f, dist + volcWarp * 0.5f)) * 0.35f * breach;
                        
                        coneSum += cone;
                        calderaSum += caldera;
                    }
                }
                volcanoMask = math.saturate(coneSum - calderaSum) * recipe.Volcanic * volcFade;
                if (!DiagVolcanoOff)
                    depth -= (coneSum * 380f - calderaSum * 120f) * recipe.Volcanic * volcFade;
            }

            // --- B1: CRATERS (Impact + Collapse) ---
            float craterMask = 0f;
            float craterFade = math.smoothstep(0.01f, 0.03f, recipe.Craters);
            if (craterFade > 0.001f)
            {
                double craterGridSizeD = 2500.0;
                int2 craterCell = new int2((int)math.floor(warpedPosD.x / craterGridSizeD), (int)math.floor(warpedPosD.y / craterGridSizeD));
                float craterDepthDelta = 0f;

                for (int cdz = -1; cdz <= 1; cdz++)
                {
                    for (int cdx = -1; cdx <= 1; cdx++)
                    {
                        int2 neighbor = craterCell + new int2(cdx, cdz);
                        uint h = Hash(neighbor.x, neighbor.y, unchecked((int)(seed ^ 0x9B3A21EFu)));
                        if (HashToUnitFloat(h ^ 0x12345678u) > (0.12f + recipe.Craters * 0.45f))
                            continue;

                        double cx = (neighbor.x + HashToUnitFloat(h ^ 0x87654321u)) * craterGridSizeD;
                        double cz = (neighbor.y + HashToUnitFloat(h ^ 0xA1B2C3D4u)) * craterGridSizeD;
                        float radius = math.lerp(120f, 950f, math.pow(HashToUnitFloat(h ^ 0x1A2B3C4Du), 2.2f));
                        // math.md section 7: cull on SQUARED distance so the sqrt is paid only by craters
                        // that actually influence this sample. This 3x3 neighbourhood ran the sqrt before
                        // the radius test, so every rejected candidate paid for one.
                        // SUPPRESSION (math.sqrt below): owner WorldMacroGeologyFields, reason = the bowl,
                        // rim and peak terms all need true metric distance; tier = all; verified by the
                        // out-of-process height checksum recorded in the commit that introduced this.
                        double craterDx = warpedPosD.x - cx;
                        double craterDz = warpedPosD.y - cz;
                        double craterDistSq = craterDx * craterDx + craterDz * craterDz;
                        double craterCull = (double)radius * 1.8;
                        if (craterDistSq > craterCull * craterCull)
                            continue;

                        float dist = (float)math.sqrt(craterDistSq);

                        float normalizedDist = dist / math.max(1f, radius);
                        float bowl = math.pow(1f - math.smoothstep(0f, 1f, normalizedDist), 1.55f);
                        
                        // C2-continuous bell curve without periodic cosine ripple rings
                        float rimDist = math.saturate(math.abs(normalizedDist - 1f) * 2.5f);
                        float rimB = math.saturate(1f - rimDist);
                        float rim = (rimB * rimB * (3f - 2f * rimB)) * math.smoothstep(1.0f, 0.95f, rimDist);
                        float peak = math.smoothstep(0f, 1f, 1f - math.smoothstep(0f, radius * 0.16f, dist)) * math.smoothstep(450f, 850f, radius) * 0.35f;

                        craterDepthDelta += bowl * radius * 0.45f * recipe.Craters;
                        craterDepthDelta -= peak * radius * 0.10f * recipe.Craters;
                        craterDepthDelta -= rim * radius * 0.25f * recipe.Craters;
                        craterMask = math.max(craterMask, bowl * recipe.Craters);
                    }
                }

                // R46 STEP 4: Micro-craters (30m to 180m radius) for dense impact stratification
                double microGridD = 600.0;
                int2 mCell = new int2((int)math.floor(warpedPosD.x / microGridD), (int)math.floor(warpedPosD.y / microGridD));
                for (int cdz = -1; cdz <= 1; cdz++)
                {
                    for (int cdx = -1; cdx <= 1; cdx++)
                    {
                        int2 neighbor = mCell + new int2(cdx, cdz);
                        uint h = Hash(neighbor.x, neighbor.y, unchecked((int)(seed ^ 0x11223344u)));
                        if (HashToUnitFloat(h ^ 0x55667788u) > (0.15f + recipe.Craters * 0.5f)) continue;
                        
                        double cx = (neighbor.x + HashToUnitFloat(h ^ 0x99AABBCCu)) * microGridD;
                        double cz = (neighbor.y + HashToUnitFloat(h ^ 0xDDEEFF00u)) * microGridD;
                        // Squared bias curve as a multiply, not a transcendental. This one sits inside
                        // the 3x3 micro-crater neighbourhood, so it ran up to nine times per sample.
                        float microRadiusBias = HashToUnitFloat(h ^ 0x11335577u);
                        float radius = math.lerp(30f, 180f, microRadiusBias * microRadiusBias);
                        // Same squared-distance cull as the macro crater loop above; micro craters are
                        // denser (600 m grid), so proportionally more candidates are rejected here.
                        double microDx = warpedPosD.x - cx;
                        double microDz = warpedPosD.y - cz;
                        double microDistSq = microDx * microDx + microDz * microDz;
                        double microCull = (double)radius * 1.8;
                        if (microDistSq > microCull * microCull) continue;

                        float dist = (float)math.sqrt(microDistSq);
                        
                        float nDist = dist / math.max(1f, radius);
                        float bowl = math.pow(1f - math.smoothstep(0f, 1f, nDist), 1.5f);
                        float rDist = math.saturate(math.abs(nDist - 1f) * 3f);
                        float rB = math.saturate(1f - rDist);
                        float rim = (rB * rB * (3f - 2f * rB)) * math.smoothstep(1.0f, 0.95f, rDist);
                        
                        craterDepthDelta += bowl * radius * 0.45f * recipe.Craters;
                        craterDepthDelta -= rim * radius * 0.25f * recipe.Craters;
                        craterMask = math.max(craterMask, bowl * recipe.Craters);
                    }
                }

                craterMask *= craterFade;
                depth += craterDepthDelta * craterFade;
            }

            // --- B2: RIVERS & DENDRITIC CHANNELS (Asymmetric & Rugged Inland Canyons) ---
            float riverRegion = DoubleFractalSimplexNoise01(warpedPosD * 0.00015 + new double2(-19.3, 44.1), seed ^ 0x1A2B3C4Du, 3);
            float riverGate = math.smoothstep(0.55f, 0.78f, riverRegion) * continentality * recipe.Rivers;
            float riverFade = math.smoothstep(0.0f, 0.05f, riverGate);

            float riverMask = 0f;
            if (riverFade > 0.001f)
            {
                // Domain warp in WORLD SPACE (meters) calculated in 64-bit double precision!
                double2 canyonWarpD = new double2(
                    DoubleFractalSimplexNoise01(warpedPosD * 0.0005 + new double2(12.1, -5.5), seed ^ 0x7E1A2B3Cu, 1) * 2.0 - 1.0,
                    DoubleFractalSimplexNoise01(warpedPosD * 0.0005 + new double2(-9.2, 18.4), seed ^ 0x3C4D5E6Fu, 1) * 2.0 - 1.0) * 1200.0;
                
                double2 warpedRiverPosD = (warpedPosD + canyonWarpD) * 0.00025;
                
                // Base canyon channel in 64-bit double precision!
                float dendritic = DoubleRidgedMultifractal01(warpedRiverPosD, seed ^ 0x6DCD4A37u, 5);
                
                // Ragged outer rim
                float rimNoise = DoubleFractalSimplexNoise01(warpedPosD * 0.003, seed ^ 0xCAFE1234u, 3);
                float canyonRim = math.smoothstep(0.55f, 0.88f, dendritic) * (0.85f + rimNoise * 0.15f);
                
                // Asymmetric bank slope: 600m world-space offset for bank asymmetry in 64-bit precision!
                float dendriticOffset = DoubleRidgedMultifractal01((warpedPosD + canyonWarpD + new double2(600.0, -600.0)) * 0.00025, seed ^ 0x6DCD4A37u, 5);
                float bankAsymmetry = math.smoothstep(0.4f, 0.9f, dendriticOffset);
                
                // Gentle floor undulation (~500m features, no pixel carpet)
                float floorRoughness = DoubleBillowNoise01(warpedPosD * 0.002 + new double2(7.7, -3.3), seed ^ 0x8899AABBu, 2) * 12f;
                
                float canyonFloor = math.smoothstep(0.60f, 0.99f, dendritic);
                riverMask = canyonRim * riverGate;
                
                // Deep cut influenced by asymmetry, minus the floor roughness, multiplied by riverFade to prevent C0 cliff
                float cutDepth = 280f * riverMask * canyonFloor * (0.6f + bankAsymmetry * 0.4f);
                depth += (cutDepth - floorRoughness * canyonFloor * riverMask) * riverFade;
            }
            float canyonMask = riverMask; // Export for downstream

            // --- B3: LAKES & PLAYAS (Sediment-filled basins) ---
            float lakeRegion = DoubleFractalSimplexNoise01(warpedPosD * 0.0002 + new double2(44.4, 11.1), seed ^ 0x55443322u, 3);
            float lakeGate = math.smoothstep(0.5f, 0.8f, lakeRegion) * continentality * recipe.Lakes;
            float lakeFade = math.smoothstep(0.0f, 0.05f, lakeGate);

            float lakeMask = 0f;
            if (lakeFade > 0.001f)
            {
                // Find natural regional depressions
                float bowlNoise = DoubleFractalSimplexNoise01(warpedPosD * 0.0004 + new double2(-22.2, 33.3), seed ^ 0x99887766u, 4);
                lakeMask = math.smoothstep(0.55f, 0.85f, bowlNoise) * lakeGate;
                
                if (lakeMask > 0.001f)
                {
                    float shoreFeather = DoubleFractalSimplexNoise01(warpedPosD * 0.005, seed ^ 0xE4F5A6B7u, 3);
                    lakeMask *= (0.7f + shoreFeather * 0.3f);
                    
                    // Sediment level varies slightly across the continent but forms local flat planes
                    float localSedimentLevel = 450f + DoubleFractalSimplexNoise01(warpedPosD * 0.0001, seed ^ 0x5A5A5A5Au, 2) * 400f;
                    
                    // R44 FIX: Use smin with k=8 meters to create a smooth C1-continuous fillet at the shoreline!
                    float filledDepth = smin(depth, localSedimentLevel, 8f);
                    depth = math.lerp(depth, filledDepth, lakeMask * 0.85f * lakeFade);
                    
                    // Subtle dry mud cracks/texture on the flat playa bed (R45: Zero-Mean subtracted)
                    float playaCracks = DoubleRidgedMultifractal01(warpedPosD * 0.015, seed ^ 0x6E01091Cu, 3);
                    depth += (playaCracks - 0.15f) * 4f * lakeMask * lakeFade;
                }
            }

            // --- B7: MESA TABLELANDS (flat caps, NOT height-quantised) ---
            float mesaMask = 0f;
            if (recipe.Mesa > 0.01f)
            {
                float mesaContFade = math.smoothstep(0.30f, 0.35f, continentality);
                float mesaField = DoubleFractalSimplexNoise01((double2)warpedNorm * 1.9 + new double2(7.8, -14.2), seed ^ 0x8C1B3D2Eu, 1);
                mesaMask = math.smoothstep(0.58f, 0.74f, mesaField) * recipe.Mesa * continentality * mesaContFade;
                float mesaWeight = math.smoothstep(0.0f, 0.15f, mesaMask) * mesaMask * 0.7f;
                float mesaFade = math.smoothstep(0.0f, 0.02f, mesaWeight);
                
                if (mesaFade > 0.001f)
                {
                    // cap elevation varies per broad patch (a few discrete plateau levels), continuous in space
                    float capDatum = DoubleFractalSimplexNoise01((double2)warpedNorm * 0.8 + new double2(-5.5, 12.1), seed ^ 0x2D9C4B7Au, 1);
                    float capDepth = math.lerp(560f, 260f, capDatum); // flat-top depth for this patch
                    
                    // R44 FIX: smin with k=12 meters rounds the sharp table-top edge into a natural slope fillet
                    float cappedDepth = smin(depth, capDepth, 12f);
                    depth = math.lerp(depth, cappedDepth, mesaWeight * mesaFade);
                }
            }

            // --- B8: DUNES / SEDIMENT BEDFORMS ---
            float duneMask = 0f;
            float dunePatch = math.smoothstep(0.40f, 0.70f, DoubleFractalSimplexNoise01(warpedPosD * 0.0015, seed ^ 0xD11E2233u, 3));
            float duneGate = math.saturate(recipe.Dunes * 0.8f + shelfMask * 0.6f * dunePatch - slopeProxy * 0.7f);
            float duneFade = math.smoothstep(0.0f, 0.15f, duneGate);

            if (duneFade > 0.0001f)
            {
                float duneDir = DoubleFractalSimplexNoise01((double2)warpedNorm * 2.5, seed ^ 0x4D3C2B1Au, 2) * 3.14159f;
                float2 duneAxis = new float2(math.cos(duneDir), math.sin(duneDir));
                double2 duneWarpD = new double2(
                    DoubleFractalSimplexNoise01(warpedPosD * 0.0015 + new double2(13.4, -7.2), seed ^ 0x778899AAu, 2) * 2.0 - 1.0,
                    DoubleFractalSimplexNoise01(warpedPosD * 0.0015 + new double2(-5.1, 18.9), seed ^ 0xBBCCDDEEu, 2) * 2.0 - 1.0) * 180.0;
                float duneOrgWarp = DoubleFractalSimplexNoise01(warpedPosD * 0.008, seed ^ 0x778899AAu, 3) * 12.0f;
                float dunePhase = (float)math.dot((float2)(warpedPosD + duneWarpD), duneAxis) * 0.025f + duneOrgWarp;
                float duneWave = math.pow(0.5f - 0.5f * math.cos(dunePhase), 1.5f);
                
                duneMask = duneGate * duneFade;
                if (!DiagFoldsDunesOff)
                    // R45: Zero-Mean Normalization (duneWave mean ~0.35 subtracted so base elevation doesn't shift)
                    depth += (duneWave - 0.35f) * 8.5f * duneMask; 
            }

            // --- B10: CORAL REEFS (Organic mounds, NO rings, C1 continuous) ---
            float reefMask = 0f;
            float reefFade = math.smoothstep(0.01f, 0.03f, recipe.Reefs);
            
            // 1. C1-Continuous Depth Gate (R48: Grows from surface -10m breaching down to 4500m depth)
            float depthGate = math.smoothstep(4500f, 3500f, depth) * math.smoothstep(-10f, 20f, depth);
            
            if (depthGate > 0.001f && reefFade > 0.001f)
            {
                float reefNoise = DoubleFractalSimplexNoise01(warpedPosD * 0.0015 + new double2(-31.4, 88.2), seed ^ 0x9E8D7C6Fu, 3);
                float reefPatch = math.smoothstep(0.50f, 0.75f, reefNoise);
                
                float coralHeads = DoubleFractalSimplexNoise01(warpedPosD * 0.025, seed ^ 0xCC00AA11u, 3);
                coralHeads = coralHeads * coralHeads; // was math.pow(x, 2f) - a transcendental for a square
                
                reefMask = reefPatch * depthGate * recipe.Reefs * reefFade;
                depth -= (coralHeads - 0.33f) * 35f * reefMask;
            }

            // --- B4: STRATIFICATION (elevation benches strictly on steep continental rock walls) ---
            float strataMask = 0f;
            float hardRockMask = math.saturate(ridgeMask * 0.48f + faultMask * 0.30f + plateEdgeMask * 0.18f + slopeProxy * 0.28f - basinMask * 0.18f);
            float rockFade = math.smoothstep(0.10f, 0.20f, hardRockMask);
            float strataActive = math.max(recipe.Strata, rockFade);

            if (!DiagStrataContourOff && strataActive > 0.001f)
            {
                // R64 PURE MATHEMATICAL STRATA B4:
                // Strata are Y-elevation sedimentary benches, but MUST be strictly gated by slope steepness (slopeProxy > 0.40).
                // Gentle dome peaks and volcanic cones (slopeProxy < 0.40) get slopeGate = 0.0 -> ZERO CONCENTRIC RINGS!
                // Steep canyon and cliff walls (slopeProxy > 0.50) render razor-sharp horizontal ledges.
                float slopeGate = math.smoothstep(0.40f, 0.70f, slopeProxy);
                float oceanicStrataBlock = math.smoothstep(0.35f, 0.65f, continentality);
                float strataStrength = math.saturate((hardRockMask * 0.8f + recipe.Strata * 0.8f) * slopeGate - duneMask * 2.0f - reefMask * 2.0f) * oceanicStrataBlock;
                strataStrength *= math.smoothstep(0.0f, 0.05f, strataActive);

                if (strataStrength > 0.01f)
                {
                    float patchLarge = DoubleFractalSimplexNoise01(warpedPosD * 0.0011 + new double2(21.4, -6.8), seed ^ 0x51C0FFEEu, 1);
                    float patchFeather = DoubleFractalSimplexNoise01(warpedPosD * 0.0047 + new double2(-13.2, 9.5), seed ^ 0x1F33A7B9u, 1);
                    float dropout = DoubleFractalSimplexNoise01(warpedPosD * 0.0026 + new double2(3.1, 17.7), seed ^ 0x7C2E9D41u, 1);
                    float broken = math.smoothstep(0.40f, 0.62f, patchLarge * 0.55f + patchFeather * 0.45f)
                                 * math.smoothstep(0.34f, 0.50f, dropout);
                    strataMask = strataStrength * broken;

                    if (strataMask > 0.001f)
                    {
                        float tiltDir = DoubleFractalSimplexNoise01((double2)warpedNorm * 1.3, seed ^ 0x5B17E3A1u, 1) * 6.2831853f;
                        float2 tiltAxis = new float2(math.cos(tiltDir), math.sin(tiltDir));
                        float tilt = math.dot(tiltAxis, warpedPos) * 0.06f;
                        float layerScale = math.lerp(22f, 46f, DoubleFractalSimplexNoise01((double2)warpedNorm * 0.9 + new double2(4.4, 4.4), seed ^ 0x2E71C4B3u, 1));
                        float hPhase = (depth + tilt) / layerScale;
                        float f = math.frac(hPhase);
                        
                        float bench = f - math.sin(6.2831853f * f) * 0.15915494f;
                        float snapped = (math.floor(hPhase) + bench) * layerScale - tilt;
                        depth = math.lerp(depth, snapped, strataMask * 0.5f);
                    }
                }
            }

            // --- B9: FRACTURED WALLS (Steep slope blocky detail) ---
            float mesoFractureMask = math.saturate(hardRockMask * 0.8f + slopeProxy * 0.4f) * steepRockMask;
            double2 fractureWarpD = new double2(
                DoubleFractalSimplexNoise01(warpedPosD * 0.002 + new double2(9.1, -3.4), seed ^ 0x4B3A2C1Du, 2) * 2.0 - 1.0,
                DoubleFractalSimplexNoise01(warpedPosD * 0.002 + new double2(-7.2, 14.8), seed ^ 0x9E8D7C6Fu, 2) * 2.0 - 1.0) * 60.0;
            float intermediateErosionA = DoubleFractalSimplexNoise01((warpedPosD + fractureWarpD) * 0.0025 + new double2(-8.2, 15.4), seed ^ 0x6E1A2B3Cu, 4);
            float intermediateErosionB = DoubleFractalSimplexNoise01((warpedPosD + fractureWarpD) * 0.0055 + new double2(12.7, -3.1), seed ^ 0x8C3B1A4Du, 3);
            
            float aboveWaterWeathering = math.smoothstep(20f, -50f, depth); 
            float fractureAmp = math.lerp(45f, 130f, aboveWaterWeathering);
            float mesoFractureDelta = ((intermediateErosionA * 0.6f + intermediateErosionB * 0.4f) * 2f - 1f) * fractureAmp;
            if (!DiagMesoFractureOff)
                depth += mesoFractureDelta * mesoFractureMask * (1f - abyssPlainMask * 0.6f);

            // R68 FIX: Lower microGravel frequency from 0.04 (25m wavelength) to 0.004 (250m wavelength)
            // to maintain sub-millimeter float32 ULP precision at X = 777,000m without 2-pixel checkerboard grid aliasing on steep rock walls.
            double2 gravelWarpD = new double2(
                DoubleFractalSimplexNoise01(warpedPosD * 0.0015, seed ^ 0x11223344u, 2) * 2.0 - 1.0,
                DoubleFractalSimplexNoise01(warpedPosD * 0.0015 + new double2(5.5, 5.5), seed ^ 0x44332211u, 2) * 2.0 - 1.0) * 15.0;

            float microGravel = (DoubleFractalSimplexNoise01((warpedPosD + gravelWarpD) * 0.004, seed ^ 0x99AA88BBu, 2) * 2f - 1f) * 1.5f;
            depth += microGravel * math.lerp(0.2f, 1.5f, steepRockMask);

            // TIER 4: Talus & Slump
            float concaveToe = math.saturate((basinMask * 1.5f + canyonMask * 1.2f + shelfToe * 0.84f + 0.1f) * (ridgeMask * 0.75f + faultMask * 0.62f + shelfBreakMask * 0.66f + 0.1f));
            float talusMask = math.saturate(math.smoothstep(0.10f, 0.62f, slopeProxy) * (1f - math.smoothstep(0.74f, 0.98f, slopeProxy)) * concaveToe);
            float talusC = DoubleBillowNoise01(warpedPosD * 0.020 + new double2(3.3, -7.2), seed ^ 0xE70D1A5Bu, 3);
            float talusF = DoubleBillowNoise01(warpedPosD * 0.071 + new double2(-5.0, 1.7), seed ^ 0xC3F19802u, 2);
            if (!DiagTalusOff)
                depth += ((talusC * 0.70f + talusF * 0.30f) * 2f - 1f) * math.lerp(5f, 15f, talusMask) * talusMask;

            // --- B11: COASTAL EROSION & KARST SPIRES ---
            float waveExposure = DoubleFractalSimplexNoise01((double2)warpedNorm * 4.5 + new double2(44.0, -12.0), seed ^ 0x99AABBCCu, 3);
            float cliffAsymmetry = math.smoothstep(0.3f, 0.7f, waveExposure);
            float coastalFalloff = depth / 15f;
            float coastalInfluence = math.exp(-(coastalFalloff * coastalFalloff)); // was exp(-pow(t, 2f))
            depth = math.lerp(depth, 2f, coastalInfluence * cliffAsymmetry * 0.9f);

            float spireRegion = math.smoothstep(0.75f, 0.98f, DoubleFractalSimplexNoise01(warpedPosD * 0.0008 + new double2(-22.0, 55.0), seed ^ 0x11223344u, 3));
            if (spireRegion > 0.01f && depth < 100f)
            {
                // R69 FIX: Add 2D domain warp and lower frequency to Karst Spires (B11)
                // High-frequency RidgedMultifractal01 (0.012f) cubed created 83m needle pinnacles that rendered as a 2-pixel diagonal grid.
                double2 spireWarpD = new double2(
                    DoubleFractalSimplexNoise01(warpedPosD * 0.0015, seed ^ 0x33445566u, 2) * 2.0 - 1.0,
                    DoubleFractalSimplexNoise01(warpedPosD * 0.0015 + new double2(-8.0, 12.0), seed ^ 0x778899AAu, 2) * 2.0 - 1.0) * 40.0;

                float spireNoise = DoubleRidgedMultifractal01((warpedPosD + spireWarpD) * 0.003, seed ^ 0x55667788u, 3);
                float spires = math.pow(spireNoise, 2.2f) * 140f * spireRegion;
                depth -= spires * math.smoothstep(100f, 10f, depth);
            }

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

            // R52 GAMEPLAY & BIOME MASKS (Nervous system for voxels, loot, and hazard biomes)
            float ledgeMask = math.saturate(strataMask * math.smoothstep(0.35f, 0.05f, slopeProxy));
            float caveEntranceMask = math.saturate(faultMask * steepRockMask);
            // Left as math.pow for the same reason as trenchCrease above: (x*x)*(x*x) rounds differently
            // and perturbs the terrain checksum for a once-per-sample saving that machine noise swallowed.
            float brinePoolMask = math.pow(math.saturate(trenchMask), 4.0f) * (1f - continentality);

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
                Reef = math.saturate(reefMask),
                Ledge = math.saturate(ledgeMask),
                CaveEntrance = math.saturate(caveEntranceMask),
                BrinePool = math.saturate(brinePoolMask)
            };

            return parameters.WaterSurfaceY - depth;
        }

        private static DifferentialSample EvaluateDifferentials(double absoluteX, double absoluteZ, in WorldMacroGeologyParams p, out MacroMasks masks)
        {
            float height = EvaluateHeightMeters(absoluteX, absoluteZ, in p, out masks);
            // Probe distance set to 12.0m for optimal slope gradient balance without Nyquist red-static aliasing
            float probe = 12.0f;
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

            // R46 STEP 5: Full 3x3 loop (eliminates 83m square grid artifact D4)
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    float2 pointHash = Hash2((int)(cell.x + dx), (int)(cell.y + dy), seed);
                    float2 diff = new float2(dx, dy) + pointHash - frac;
                    float distSq = math.lengthsq(diff);

                    if (distSq < minDistSq)
                    {
                        minDistSq = distSq;
                        cellHash = pointHash;
                    }
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

        public static float RidgedMultifractal01(double2 sampleD, uint seed, int octaves = 5)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float total = 0f;
            float norm = 0f;
            float weight = 1f;
            double2 p = sampleD;
            for (int octave = 0; octave < octaves; octave++)
            {
                float ang = 0.5f + octave * 0.7548776662f + HashToUnitFloat(seed ^ (uint)(octave * 0x9E3779B9)) * 6.2831853f;
                float sa = math.sin(ang), ca = math.cos(ang);
                double2x2 rot = new double2x2((double)ca, (double)(-sa), (double)sa, (double)ca);
                p = math.mul(rot, p);

                uint layerSeed = seed + (uint)octave * 0x9E3779B9u;
                float seedOffsetX = HashToUnitFloat(layerSeed ^ 0x9E3779B9u) * 16f - 8f;
                float seedOffsetY = HashToUnitFloat(layerSeed ^ 0x334EAA71u) * 16f - 8f;
                double2 samplePos = p * (double)frequency + new double2((double)seedOffsetX, (double)seedOffsetY);
                float snoiseVal = DoubleSimplex2D(samplePos, 1.0f, layerSeed);

                float n = 1f - math.abs(snoiseVal);
                n = n * n;
                if (DiagRidgedAsFbm || DiagRidgedAsFbmMountain) n = snoiseVal * 0.5f + 0.5f;
                n *= weight;
                weight = (DiagNoiseBroadband || DiagRidgedAsFbm || DiagRidgedAsFbmMountain) ? 1f : math.saturate(n * 2f);

                total += n * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.0f;
            }

            return total / math.max(0.0001f, norm);
        }

        public static float RidgedMultifractal01(float2 sample, uint seed, int octaves = 5)
        {
            return RidgedMultifractal01((double2)sample, seed, octaves);
        }

        public static float ErodedRidge01(double2 sampleD, uint seed, int octaves = 5)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float total = 0f;
            float norm = 0f;
            float weight = 1f;
            double2 p = sampleD;
            for (int octave = 0; octave < octaves; octave++)
            {
                float ang = 0.5f + octave * 0.7548776662f + HashToUnitFloat(seed ^ (uint)(octave * 0x9E3779B9)) * 6.2831853f;
                float sa = math.sin(ang), ca = math.cos(ang);
                double2x2 rot = new double2x2((double)ca, (double)(-sa), (double)sa, (double)ca);
                p = math.mul(rot, p);

                uint layerSeed = seed + (uint)octave * 0x9E3779B9u;
                float seedOffsetX = HashToUnitFloat(layerSeed ^ 0x9E3779B9u) * 16f - 8f;
                float seedOffsetY = HashToUnitFloat(layerSeed ^ 0x334EAA71u) * 16f - 8f;
                double2 samplePos = p * (double)frequency + new double2((double)seedOffsetX, (double)seedOffsetY);
                float snoiseVal = DoubleSimplex2D(samplePos, 1.0f, layerSeed);

                float n = 1f - math.abs(snoiseVal);
                n = n * n * (3f - 2f * n);
                if (DiagRidgedAsFbm || DiagRidgedAsFbmMountain) n = snoiseVal * 0.5f + 0.5f;
                n *= weight;
                weight = (DiagNoiseBroadband || DiagRidgedAsFbm || DiagRidgedAsFbmMountain) ? 1f : math.saturate(0.35f + n * 0.9f);

                total += n * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.0f;
            }

            return total / math.max(0.0001f, norm);
        }

        public static float ErodedRidge01(float2 sample, uint seed, int octaves = 5)
        {
            return ErodedRidge01((double2)sample, seed, octaves);
        }

        public static float BillowNoise01(double2 sampleD, uint seed, int octaves = 5)
        {
            float amplitude = 1f;
            float frequency = 1f;
            float total = 0f;
            float norm = 0f;
            double2 p = sampleD;
            for (int octave = 0; octave < octaves; octave++)
            {
                float ang = 0.5f + octave * 0.7548776662f + HashToUnitFloat(seed ^ (uint)(octave * 0x9E3779B9)) * 6.2831853f;
                float sa = math.sin(ang), ca = math.cos(ang);
                double2x2 rot = new double2x2((double)ca, (double)(-sa), (double)sa, (double)ca);
                p = math.mul(rot, p);

                uint layerSeed = seed + (uint)octave * 0x9E3779B9u;
                float seedOffsetX = HashToUnitFloat(layerSeed ^ 0x9E3779B9u) * 16f - 8f;
                float seedOffsetY = HashToUnitFloat(layerSeed ^ 0x334EAA71u) * 16f - 8f;
                double2 samplePos = p * (double)frequency + new double2((double)seedOffsetX, (double)seedOffsetY);
                float snoiseVal = DoubleSimplex2D(samplePos, 1.0f, layerSeed);

                float n = math.abs(snoiseVal);

                total += n * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.0f;
            }

            return total / math.max(0.0001f, norm);
        }

        public static float BillowNoise01(float2 sample, uint seed, int octaves = 5)
        {
            return BillowNoise01((double2)sample, seed, octaves);
        }

        public static float DoubleBillowNoise01(double2 sampleD, uint seed, int octaves = 5)
        {
            return BillowNoise01(sampleD, seed, octaves);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 HashGradient2D(int cx, int cy, uint seed)
        {
            uint h = Hash(cx, cy, (int)seed);
            float angle = (h & 0x00FFFFFFu) * (6.28318530718f / 16777215f);
            return new float2(math.cos(angle), math.sin(angle));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EvaluateSimplexGrad(int cellX, int cellY, float2 x0, uint seed)
        {
            int i1, j1;
            if (x0.x > x0.y) { i1 = 1; j1 = 0; }
            else { i1 = 0; j1 = 1; }

            const float G2 = 0.2113248654051871f;
            float2 x1 = x0 - new float2(i1, j1) + G2;
            float2 x2 = x0 - 1.0f + 2.0f * G2;

            float t0 = 0.5f - math.lengthsq(x0);
            float t1 = 0.5f - math.lengthsq(x1);
            float t2 = 0.5f - math.lengthsq(x2);

            float n0 = 0f, n1 = 0f, n2 = 0f;

            if (t0 > 0f)
            {
                t0 *= t0;
                float2 g0 = HashGradient2D(cellX, cellY, seed);
                n0 = (t0 * t0) * math.dot(g0, x0);
            }
            if (t1 > 0f)
            {
                t1 *= t1;
                float2 g1 = HashGradient2D(cellX + i1, cellY + j1, seed);
                n1 = (t1 * t1) * math.dot(g1, x1);
            }
            if (t2 > 0f)
            {
                t2 *= t2;
                float2 g2 = HashGradient2D(cellX + 1, cellY + 1, seed);
                n2 = (t2 * t2) * math.dot(g2, x2);
            }

            return 70.0f * (n0 + n1 + n2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DoubleSimplex2D(double2 posD, float frequency, uint seed)
        {
            double2 p = posD * (double)frequency;
            const double F2 = 0.3660254037844386; // 0.5 * (sqrt(3) - 1)
            double s = (p.x + p.y) * F2;
            double2 skewedFloor = math.floor(p + s);

            int cellX = (int)skewedFloor.x;
            int cellY = (int)skewedFloor.y;

            const double G2 = 0.2113248654051871; // (3 - sqrt(3)) / 6
            double t = (double)(cellX + cellY) * G2;
            double2 cellOrigin = new double2((double)cellX - t, (double)cellY - t);
            float2 localOffset = (float2)(p - cellOrigin);

            return EvaluateSimplexGrad(cellX, cellY, localOffset, seed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DoubleSimplex2D01(double2 posD, float frequency, uint seed)
        {
            return DoubleSimplex2D(posD, frequency, seed) * 0.5f + 0.5f;
        }

        public static float DoubleFractalSimplexNoise01(double2 posD, float frequency, uint seed, int octaves = 5)
        {
            float amplitude = 0.5f;
            float currentFreq = frequency;
            float total = 0f;
            float norm = 0f;
            for (int octave = 0; octave < octaves; octave++)
            {
                float ang = 0.5f + octave * 0.7548776662f + HashToUnitFloat(seed ^ (uint)(octave * 0x9E3779B9)) * 6.2831853f;
                float sa = math.sin(ang), ca = math.cos(ang);

                double2x2 rot = new double2x2((double)ca, (double)(-sa), (double)sa, (double)ca);
                posD = math.mul(rot, posD);

                uint layerSeed = seed + (uint)octave * 0x9E3779B9u;
                float seedOffsetX = HashToUnitFloat(layerSeed ^ 0x9E3779B9u) * 16f - 8f;
                float seedOffsetY = HashToUnitFloat(layerSeed ^ 0x334EAA71u) * 16f - 8f;

                double2 samplePos = posD + new double2((double)seedOffsetX, (double)seedOffsetY);
                float n = DoubleSimplex2D(samplePos, currentFreq, layerSeed) * 0.5f + 0.5f;

                total += n * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
                currentFreq *= 2.02f;
            }

            return total / math.max(0.0001f, norm);
        }

        public static float DoubleRidgedMultifractal01(double2 posD, float frequency, uint seed, int octaves = 5)
        {
            float amplitude = 1f;
            float currentFreq = frequency;
            float total = 0f;
            float norm = 0f;
            float weight = 1f;
            for (int octave = 0; octave < octaves; octave++)
            {
                float ang = 0.5f + octave * 0.7548776662f + HashToUnitFloat(seed ^ (uint)(octave * 0x9E3779B9)) * 6.2831853f;
                float sa = math.sin(ang), ca = math.cos(ang);

                double2x2 rot = new double2x2((double)ca, (double)(-sa), (double)sa, (double)ca);
                posD = math.mul(rot, posD);

                uint layerSeed = seed + (uint)octave * 0x9E3779B9u;
                float seedOffsetX = HashToUnitFloat(layerSeed ^ 0x9E3779B9u) * 16f - 8f;
                float seedOffsetY = HashToUnitFloat(layerSeed ^ 0x334EAA71u) * 16f - 8f;

                double2 samplePos = posD + new double2((double)seedOffsetX, (double)seedOffsetY);
                float snoiseVal = DoubleSimplex2D(samplePos, currentFreq, layerSeed);

                float n = 1f - math.abs(snoiseVal);
                n = n * n;
                n *= weight;
                weight = math.saturate(n * 2f);

                total += n * amplitude;
                norm += amplitude;
                amplitude *= 0.5f;
                currentFreq *= 2.0f;
            }

            return total / math.max(0.0001f, norm);
        }

        private static float SimplexNoise01(float2 sample, uint seed)
        {
            return DoubleSimplex2D((double2)sample, 1.0f, seed) * 0.5f + 0.5f;
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
            return new float2(Hash01(x, y, seed), Hash01(x, y, seed * 0x9E3779B9u + 0xA511E9B3u));
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
        private static float smin(float a, float b, float k)
        {
            float h = math.saturate(0.5f + 0.5f * (b - a) / k);
            return math.lerp(b, a, h) - k * h * (1f - h);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float smax(float a, float b, float k)
        {
            float h = math.saturate(0.5f + 0.5f * (a - b) / k);
            return math.lerp(b, a, h) + k * h * (1f - h);
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
            public float Ledge;
            public float CaveEntrance;
            public float BrinePool;
        }
    }
}
