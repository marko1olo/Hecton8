using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.World
{
    public enum WorldTerrainDetailTier : byte
    {
        NearPlayable = 0,
        MidTraversal = 1,
        FarSilhouette = 2,
        DistantHlod = 3
    }

    public readonly struct WorldTerrainDetailTierInfo
    {
        public readonly WorldTerrainDetailTier Tier;
        public readonly int HeightResolution;
        public readonly float SamplePitchMeters;
        public readonly float MaxDistanceMeters;
        public readonly uint EnabledControlMaps;

        public WorldTerrainDetailTierInfo(
            WorldTerrainDetailTier tier,
            int heightResolution,
            float samplePitchMeters,
            float maxDistanceMeters,
            uint enabledControlMaps)
        {
            Tier = tier;
            HeightResolution = heightResolution;
            SamplePitchMeters = samplePitchMeters;
            MaxDistanceMeters = maxDistanceMeters;
            EnabledControlMaps = enabledControlMaps;
        }
    }

    public static class WorldTerrainControlMapFlags
    {
        public const uint None = 0u;
        public const uint MacroHeight = 1u << 0;
        public const uint Slope = 1u << 1;
        public const uint Curvature = 1u << 2;
        public const uint ErosionFlow = 1u << 3;
        public const uint Terrace = 1u << 4;
        public const uint Slump = 1u << 5;
        public const uint Tributary = 1u << 6;
        public const uint Sediment = 1u << 7;
        public const uint HardRock = 1u << 8;
        public const uint Nodule = 1u << 9;
        public const uint ReefEligibility = 1u << 10;
        public const uint VoxelSeam = 1u << 11;
        public const uint MaterialRegion = 1u << 12;
        public const uint All =
            MacroHeight |
            Slope |
            Curvature |
            ErosionFlow |
            Terrace |
            Slump |
            Tributary |
            Sediment |
            HardRock |
            Nodule |
            ReefEligibility |
            VoxelSeam |
            MaterialRegion;
    }

    [System.Flags]
    public enum WorldTerrainDetailEligibilityFlags : uint
    {
        None = 0u,
        SandScatter = 1u << 0,
        RockScatter = 1u << 1,
        NoduleScatter = 1u << 2,
        ReefScatter = 1u << 3,
        BrineDeposit = 1u << 4,
        SeepDeposit = 1u << 5,
        TalusBoulder = 1u << 6,
        RubblePebble = 1u << 7,
        DecalOverlay = 1u << 8,
        DetailNormal = 1u << 9,
        VoxelAnchor = 1u << 10,
        CaveMouthCandidate = 1u << 11
    }

    public enum WorldTerrainSurfaceMaterialClass : byte
    {
        Unknown = 0,
        ShellSand = 1,
        LimestoneShelf = 2,
        ClaySilt = 3,
        HardRock = 4,
        BrineSaltCrust = 5,
        ManganeseNodulePlain = 6,
        ReefRubble = 7,
        SeepCrust = 8
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WorldTerrainSurfaceMaterialWeights
    {
        public float ShellSand;
        public float LimestoneShelf;
        public float ClaySilt;
        public float HardRock;
        public float BrineSaltCrust;
        public float ManganeseNodulePlain;
        public float ReefRubble;
        public float SeepCrust;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WorldTerrainControlMapSplats
    {
        public float4 Control1;
        public float4 Control2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WorldTerrainDetailRuntimeSample
    {
        public WorldMacroGeologySample Macro;
        public WorldTerrainMesoDetailSample Meso;
        public WorldTerrainSurfaceMaterialWeights MaterialWeights;
        public WorldTerrainSurfaceMaterialClass DominantMaterial;
        public WorldTerrainDetailEligibilityFlags EligibilityFlags;
        public float4 Control1;
        public float4 Control2;
        public uint MacroArtifactVersion;
        public uint SurfaceMaterialContractVersion;
        public uint MesoDetailContractVersion;
        public uint DetailEligibilityContractVersion;

        public bool IsValid =>
            MacroArtifactVersion == WorldMacroGeologyFields.ArtifactVersion &&
            SurfaceMaterialContractVersion == WorldTerrainSurfaceMaterialResolver.ContractVersion &&
            MesoDetailContractVersion == WorldTerrainMesoDetailFields.ContractVersion &&
            DetailEligibilityContractVersion == WorldTerrainDetailContracts.ContractVersion &&
            math.isfinite(Macro.HeightMeters);
    }

    public static class WorldTerrainSurfaceMaterialResolver
    {
        // 4 -> 5: steepSlope is synchronised to be the exact complement of angleOfRepose (:185), so
        // rock no longer reaches full strength three degrees before sediment has finished sliding
        // off, and a talus apron feeds ReefRubble on the 24-38 degree band at any depth (:191). Both
        // change material output on mid slopes; splatmaps baked at version 4 are stale. Terrain
        // GEOMETRY is still untouched - WorldMacroGeologyFields.ArtifactVersion does not move.
        public const uint ContractVersion = 5u;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WorldTerrainSurfaceMaterialWeights Resolve(
            in WorldMacroGeologySample sample,
            float absoluteX,
            float absoluteZ,
            uint seed)
        {
            float slope = math.saturate(sample.Slope01);
            float flat = 1f - slope;
            float positiveCurvature = math.saturate(sample.PositiveCurvature01);
            float negativeCurvature = math.saturate(sample.NegativeCurvature01);
            float signedCurvature = positiveCurvature - negativeCurvature;
            float shallow = 1f - math.saturate((sample.DepthMeters - 40f) / 420f);
            float upperWater = 1f - math.saturate((sample.DepthMeters - 500f) / 1250f);
            float abyss = math.saturate((sample.DepthMeters - 1200f) / 2300f);
            float trench = math.saturate(sample.TrenchMask);
            float basin = math.saturate(sample.BasinMask);
            float ridge = math.saturate(sample.RidgeMask);
            float shelf = math.saturate(sample.ShelfMask);
            float shelfBreak = math.saturate(sample.ShelfBreakMask);
            float sediment = math.saturate(sample.SedimentMask);
            float hardRock = math.saturate(sample.HardRockExposureMask);
            float reef = math.saturate(sample.ReefEligibilityMask);
            float nodule = math.saturate(sample.NodulePlainMask);
            float seep = math.saturate(sample.SeepMask);
            float erosion = math.saturate(sample.ErosionFlow01);
            float terrace = math.saturate(sample.TerraceMask);
            float slump = math.saturate(sample.SlumpScarMask);
            float tributary = math.saturate(sample.TributaryCanyonMask);
            float exposedRidge = math.saturate(ridge * 0.36f + sample.FaultMask * 0.30f + hardRock * 0.56f + math.smoothstep(0.56f, 0.84f, slope) * 0.42f);
            float flatFloor = math.smoothstep(0.54f, 0.90f, flat);
            // The angle of repose is the single physical statement about where sediment stops
            // resting, and it is now the ONLY one. steepSlope used to open at 0.34 and saturate at
            // 0.56 - 23.0 to 35.0 degrees - while angleOfRepose closes over 0.36..0.62, i.e. 24.2 to
            // 37.8. Two ramps in one method describing the same physics, disagreeing by 15 degrees,
            // with rock reaching full strength almost three degrees BEFORE sediment had finished
            // sliding off. Measured consequence at W3_typical, the world's median site: only 22.6%
            // of a 1 km window is past the repose angle and HardRock won 80.5% of it.
            //
            // Synchronised on the owner's ruling 2026-08-10: sediment lies where it can hold, rock
            // is exposed where it cannot. steepSlope is now exactly the complement of angleOfRepose,
            // so the two can no longer drift apart - there is one authored pair of bounds, used
            // twice, rather than two pairs that happen to be near each other.
            //
            // This does NOT flatten anything: geometry is untouched and slopes past 37.8 degrees are
            // still fully rock. It moves sand and gravel onto the 23-35 degree band that was being
            // called bare rock, which is the mid-slope apron a real submarine scarp carries.
            const float ReposeLowerSlope01 = 0.36f;
            const float ReposeUpperSlope01 = 0.62f;
            float angleOfRepose = 1f - math.smoothstep(ReposeLowerSlope01, ReposeUpperSlope01, slope);
            float steepSlope = 1f - angleOfRepose;
            float verySteep = math.smoothstep(0.56f, 0.84f, slope);
            float convexScrape = math.smoothstep(0.18f, 0.72f, positiveCurvature) * math.smoothstep(0.28f, 0.68f, slope);
            float finalRock = math.saturate(math.max(convexScrape * 1.35f, steepSlope * (0.56f + positiveCurvature * 0.48f + verySteep * 0.36f)) + exposedRidge - negativeCurvature * flatFloor * 0.32f - sediment * angleOfRepose * 0.18f);
            float curvatureNeutral = 1f - math.saturate(positiveCurvature + negativeCurvature);
            float shellShelfPool = math.smoothstep(0.74f, 0.96f, flat) * math.smoothstep(0.58f, 0.92f, curvatureNeutral) * shallow * math.saturate(shelf * 0.76f + terrace * 0.20f + upperWater * 0.12f) * (1f - math.smoothstep(0.18f, 0.50f, negativeCurvature));
            float concaveSiltDominance = math.smoothstep(0.36f, 0.72f, negativeCurvature) * (1f - math.smoothstep(0.16f, 0.28f, slope));
            // TALUS APRON. The band between "sediment lies flat" and "bare rock face" is where a real
            // submarine scarp carries its scree: loose cobble shed from above and caught on the slope
            // below. steepSlope * angleOfRepose is exactly that band - both factors are non-zero only
            // between 24.2 and 37.8 degrees - and it peaks at 0.25 in the middle, so x4 normalises it.
            //
            // The class it feeds already exists and was effectively dead. ReefRubble is mapped to
            // 2Rock.terrainlayer (Editor/Terrain/AutoBuildTextureArrays.cs:21), a plain cobble
            // texture, but the only route to it was reef * shallow * upperWater - shallow closes by
            // 460 m and upperWater by 1750 m, so the gravel texture could not appear anywhere in the
            // abyss whatever the ground looked like. A rubble material that cannot reach the places
            // rubble forms is an unused texture, not a design.
            //
            // Talus is rock debris, so unlike the sediments it is only lightly suppressed by
            // finalRock (0.25 rather than 0.65); being on rocky ground is a reason for scree to
            // exist, not a reason to erase it.
            float talusBand = math.saturate(steepSlope * angleOfRepose * 4f);
            float talusApron = talusBand * (0.55f + negativeCurvature * 0.45f) * (1f - trench * 0.40f);
            // The curvature half of this term is gated by slope. It was not, and that one missing
            // gate painted half the FLAT seafloor as rock.
            //
            // MEASURED 2026-08-10 by intervention on real in-world samples
            // (WorldTerrainRockAttributionTests.RockDominance_AttributedByIntervention): each sample
            // re-resolved through this resolver with exactly one input neutralised.
            //
            //   site        mean slope   rock wins   PositiveCurvature=0   Slope01 halved
            //   W1_flat        9.4 deg        47%                    0%              47%
            //   W2_gentle     18.0 deg        53%                   13%              48%
            //   W3_typical    31.3 deg        82%                   77%              52%
            //   W4_steep      42.1 deg        97%                   97%              83%
            //
            // At 9.4 degrees - gentle abyssal plain - rock won 47% of the window; removing curvature
            // took it to ZERO while halving slope changed nothing. That is curvature alone deciding
            // the material on ground that is flat. It inverts with steepness: by W4 curvature is
            // irrelevant and slope does the work, which is correct and is left untouched.
            //
            // WHY ~HALF THE WORLD. On a fractal surface about half the cells are convex, so an
            // ungated smoothstep on positive curvature saturates on about half of everything. The
            // 47% is that half - a property of the ruler, not of the terrain.
            //
            // THE GATE. Convex curvature on a plain is a gentle swell and holds sediment; on a steep
            // face it is an exposed edge that sheds it. Where sediment stops resting is already
            // stated in this method as angleOfRepose (closing between 24 and 38 degrees), so
            // (1 - angleOfRepose) is exactly the ramp wanted and reuses the resolver's own
            // definition rather than inventing a second threshold. convexScrape at :182 already
            // gates its curvature by slope; this term was the outlier.
            //
            // NOT A GEOMETRY CHANGE. This is the material resolver; EvaluateHeightMeters is
            // untouched, so every landform, cliff and shelf break is bit-identical. Only which
            // material is painted on gentle ground moves.
            float ridgeRockDominance = math.saturate(math.smoothstep(0.24f, 0.48f, positiveCurvature) + math.smoothstep(0.54f, 0.72f, slope));
            finalRock = math.saturate(finalRock + ridgeRockDominance * (0.78f - finalRock * 0.42f) - concaveSiltDominance * flatFloor * 0.20f - talusApron * 0.30f);

            // C1-Smooth Early-Exit Gate: skip 9 octaves of material noise on pure rock faces (finalRock >= 0.98)
            float jitterGate = math.smoothstep(0.98f, 0.85f, finalRock);
            float provinceJitter = 0.5f;
            float localPatch = 0.5f;
            float finePatch = 0.5f;
            if (jitterGate > 0.0001f)
            {
                provinceJitter = WorldMacroGeologyFields.DoubleFractalSimplexNoise01(new double2(absoluteX, absoluteZ) / 900.0, seed ^ 0x51A7E531u, 3);
                localPatch = WorldMacroGeologyFields.DoubleFractalSimplexNoise01(new double2(absoluteX, absoluteZ) / 240.0, seed ^ 0xB34ACE21u, 3);
                finePatch = WorldMacroGeologyFields.DoubleFractalSimplexNoise01(new double2(absoluteX, absoluteZ) / 72.0, seed ^ 0x6E9CF5A1u, 3);
            }

            float concaveFloor = math.smoothstep(0.14f, 0.66f, negativeCurvature) * flatFloor * angleOfRepose;
            float concavityDeposit = math.saturate(concaveFloor * 1.08f + slump * 0.34f + tributary * 0.34f + basin * 0.24f + sediment * 0.30f - convexScrape * 0.58f - finalRock * 0.34f);
            float patchContrast = math.smoothstep(0.28f, 0.72f, localPatch);
            float shellHash = math.smoothstep(0.18f, 0.82f, finePatch);
            float depthLerp = math.saturate((sample.DepthMeters - 180f) / 1900f);
            float baseSand = math.lerp(0.88f, 0.08f, depthLerp);
            float baseSilt = math.lerp(0.08f, 0.88f, depthLerp);
            float sedimentRoom = math.saturate(1f - finalRock);
            float shallowFlatSand = shallow * flatFloor * angleOfRepose * (1f - math.smoothstep(0.18f, 0.58f, negativeCurvature));
            float convexSandScour = math.saturate(convexScrape * 0.78f + positiveCurvature * steepSlope * 0.44f + signedCurvature * 0.18f);

            WorldTerrainSurfaceMaterialWeights weights = new WorldTerrainSurfaceMaterialWeights
            {
                ShellSand = math.saturate((baseSand * angleOfRepose * (0.50f + patchContrast * 0.30f) + shallowFlatSand * 0.42f + shelf * shallow * 0.24f + terrace * angleOfRepose * 0.10f + shellHash * shelf * 0.08f + shellShelfPool * 1.18f) * sedimentRoom * (1f - concavityDeposit * 0.55f) * (1f - convexSandScour * 0.72f) * (1f - concaveSiltDominance * 0.38f) * (1f - ridgeRockDominance * 0.54f)),
                LimestoneShelf = math.saturate((shelf * (0.24f + shelfBreak * 0.28f) + ridge * shallow * 0.12f + terrace * shelf * 0.16f) * sedimentRoom * (1f - trench * 0.48f) * (1f - concavityDeposit * 0.22f) * (1f - ridgeRockDominance * 0.18f)),
                ClaySilt = math.saturate((baseSilt * flatFloor * (0.46f + (1f - patchContrast) * 0.30f) + concavityDeposit * 0.86f + negativeCurvature * flatFloor * 0.42f + concaveSiltDominance * 1.24f + basin * abyss * 0.18f + tributary * 0.14f) * sedimentRoom * (1f - convexScrape * 0.70f) * (1f - ridgeRockDominance * 0.48f)),
                HardRock = finalRock,
                BrineSaltCrust = math.saturate(((trench * (0.50f + abyss * 0.38f)) + (math.smoothstep(2200f, 2800f, sample.DepthMeters) * basin * 0.12f)) * (1f - finalRock * 0.78f) * (1f - concavityDeposit * 0.18f)),
                ManganeseNodulePlain = math.saturate(nodule * abyss * flatFloor * (0.70f + provinceJitter * 0.26f) * (1f - trench * 0.58f) * sedimentRoom * (1f - convexScrape * 0.48f)),
                ReefRubble = math.saturate(reef * shallow * upperWater * (0.74f + localPatch * 0.36f) * (1f - trench * 0.72f) * (1f - finalRock * 0.65f) * (1f - concavityDeposit * 0.24f) + talusApron * (0.62f + finePatch * 0.30f) * (1f - finalRock * 0.25f)),
                SeepCrust = math.saturate(seep * (0.64f + tributary * 0.30f + erosion * 0.24f) * (1f - shelf * 0.24f) * (0.28f + sedimentRoom * 0.72f))
            };

            return NormalizeOrFallback(weights, in sample);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WorldTerrainSurfaceMaterialWeights ApplyMesoDetailBias(
            WorldTerrainSurfaceMaterialWeights weights,
            in WorldTerrainMesoDetailSample meso)
        {
            float talus = math.saturate(meso.TalusMask);
            float rubble = math.saturate(meso.RubbleMask);
            float reef = math.saturate(meso.ReefDetailMask);
            float sediment = math.saturate(meso.SedimentMask);
            float terrace = math.saturate(meso.TerraceMask);
            float slump = math.saturate(meso.SlumpScarMask);
            float tributary = math.saturate(meso.TributaryCanyonMask);
            float anchor = math.saturate(meso.VoxelAnchorMask);
            float roughness = math.saturate(meso.ScatterRoughnessMask);
            float concavity = math.saturate(slump * 0.52f + tributary * 0.36f + sediment * 0.28f);
            float mesoRockScrape = math.saturate(talus * 0.48f + anchor * 0.36f + roughness * 0.28f);
            float mesoSedimentPocket = math.saturate(concavity * 0.72f + sediment * 0.24f - mesoRockScrape * 0.30f);

            weights.HardRock = math.saturate(weights.HardRock + mesoRockScrape * 0.18f - mesoSedimentPocket * 0.08f);
            weights.LimestoneShelf = math.saturate(weights.LimestoneShelf + terrace * 0.08f + reef * 0.03f - mesoSedimentPocket * 0.03f);
            weights.ClaySilt = math.saturate(weights.ClaySilt + mesoSedimentPocket * 0.22f + sediment * 0.05f);
            weights.ShellSand = math.saturate(weights.ShellSand + rubble * 0.04f + (1f - sediment) * reef * 0.05f - concavity * 0.06f - mesoRockScrape * 0.07f);
            weights.ReefRubble = math.saturate(weights.ReefRubble + rubble * 0.16f + reef * 0.18f);
            weights.SeepCrust = math.saturate(weights.SeepCrust + tributary * meso.SeepEligibilityMask * 0.12f + anchor * meso.SeepEligibilityMask * 0.04f);

            return Normalize(weights);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WorldTerrainControlMapSplats ResolveControlSplats(in WorldTerrainSurfaceMaterialWeights weights)
        {
            float total = math.max(0.0001f, 
                weights.ShellSand + weights.LimestoneShelf + weights.ClaySilt + weights.HardRock + 
                weights.BrineSaltCrust + weights.ManganeseNodulePlain + weights.ReefRubble + weights.SeepCrust);
            
            float invTotal = 1f / total;
            return new WorldTerrainControlMapSplats
            {
                Control1 = new float4(
                    weights.ShellSand * invTotal,
                    weights.LimestoneShelf * invTotal,
                    weights.ClaySilt * invTotal,
                    weights.HardRock * invTotal),
                Control2 = new float4(
                    weights.BrineSaltCrust * invTotal,
                    weights.ManganeseNodulePlain * invTotal,
                    weights.ReefRubble * invTotal,
                    weights.SeepCrust * invTotal)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WorldTerrainSurfaceMaterialClass ResolveDominant(in WorldTerrainSurfaceMaterialWeights weights)
        {
            WorldTerrainSurfaceMaterialClass best = WorldTerrainSurfaceMaterialClass.ShellSand;
            float value = weights.ShellSand;
            SelectDominant(weights.LimestoneShelf, WorldTerrainSurfaceMaterialClass.LimestoneShelf, ref value, ref best);
            SelectDominant(weights.ClaySilt, WorldTerrainSurfaceMaterialClass.ClaySilt, ref value, ref best);
            SelectDominant(weights.HardRock, WorldTerrainSurfaceMaterialClass.HardRock, ref value, ref best);
            SelectDominant(weights.BrineSaltCrust, WorldTerrainSurfaceMaterialClass.BrineSaltCrust, ref value, ref best);
            SelectDominant(weights.ManganeseNodulePlain, WorldTerrainSurfaceMaterialClass.ManganeseNodulePlain, ref value, ref best);
            SelectDominant(weights.ReefRubble, WorldTerrainSurfaceMaterialClass.ReefRubble, ref value, ref best);
            SelectDominant(weights.SeepCrust, WorldTerrainSurfaceMaterialClass.SeepCrust, ref value, ref best);
            return best;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SelectDominant(
            float candidate,
            WorldTerrainSurfaceMaterialClass material,
            ref float bestValue,
            ref WorldTerrainSurfaceMaterialClass best)
        {
            if (candidate <= bestValue)
                return;

            bestValue = candidate;
            best = material;
        }

        private static WorldTerrainSurfaceMaterialWeights NormalizeOrFallback(
            WorldTerrainSurfaceMaterialWeights weights,
            in WorldMacroGeologySample sample)
        {
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
                float slopeDegrees = math.degrees(math.atan(math.max(0f, sample.Slope01) * 1.25f));
                return new WorldTerrainSurfaceMaterialWeights
                {
                    ShellSand = slopeDegrees > 35f ? 0f : 1f,
                    LimestoneShelf = 0f,
                    ClaySilt = 0f,
                    HardRock = slopeDegrees > 35f ? 1f : 0f,
                    BrineSaltCrust = 0f,
                    ManganeseNodulePlain = 0f,
                    ReefRubble = 0f,
                    SeepCrust = 0f
                };
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
            return weights;
        }

        private static WorldTerrainSurfaceMaterialWeights Normalize(WorldTerrainSurfaceMaterialWeights weights)
        {
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
                return weights;

            float invTotal = 1f / total;
            weights.ShellSand *= invTotal;
            weights.LimestoneShelf *= invTotal;
            weights.ClaySilt *= invTotal;
            weights.HardRock *= invTotal;
            weights.BrineSaltCrust *= invTotal;
            weights.ManganeseNodulePlain *= invTotal;
            weights.ReefRubble *= invTotal;
            weights.SeepCrust *= invTotal;
            return weights;
        }

        private static float CoarseValueNoise01(float absoluteX, float absoluteZ, uint seed, float cellSizeMeters)
        {
            float invCell = 1f / math.max(1f, cellSizeMeters);
            float sx = absoluteX * invCell;
            float sz = absoluteZ * invCell;
            float2 floorSample = math.floor(new float2(sx, sz));
            int2 cell = (int2)floorSample;
            float2 local = new float2(sx, sz) - floorSample;
            float2 smooth = local * local * (3f - 2f * local);
            float a = Hash01(cell.x, cell.y, seed);
            float b = Hash01(cell.x + 1, cell.y, seed);
            float c = Hash01(cell.x, cell.y + 1, seed);
            float d = Hash01(cell.x + 1, cell.y + 1, seed);
            return math.lerp(math.lerp(a, b, smooth.x), math.lerp(c, d, smooth.x), smooth.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Hash01(int x, int z, uint seed)
        {
            uint h = seed ^ 2166136261u;
            h = Mix(h ^ unchecked((uint)x));
            h = Mix(h ^ unchecked((uint)z));
            return (h & 0x00FFFFFFu) / 16777215f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WorldTerrainMesoDetailParams
    {
        public uint Seed;
        public float PreviewExtentMeters;
        public float TerraceStrengthMeters;
        public float SlumpStrengthMeters;
        public float TributaryStrengthMeters;
        public float TalusStrengthMeters;
        public float RubbleStrengthMeters;
        public float ReefStrengthMeters;
        public float MaxMesoDeltaMeters;

        public static WorldTerrainMesoDetailParams CreateDefault(uint seed)
        {
            return new WorldTerrainMesoDetailParams
            {
                Seed = seed,
                PreviewExtentMeters = WorldTerrainDetailContracts.MesoMesoProofExtentMeters,
                TerraceStrengthMeters = 0.10f,
                SlumpStrengthMeters = 1f,
                TributaryStrengthMeters = 1f,
                TalusStrengthMeters = 1f,
                RubbleStrengthMeters = 1f,
                ReefStrengthMeters = 1f,
                MaxMesoDeltaMeters = 70f
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WorldTerrainMesoDetailSample
    {
        public float BaseHeightMeters;
        public float DetailedHeightMeters;
        public float HeightDeltaMeters;
        public float TerraceMask;
        public float SlumpScarMask;
        public float TributaryCanyonMask;
        public float TalusMask;
        public float RubbleMask;
        public float ReefDetailMask;
        public float SedimentMask;
        public float ScatterRoughnessMask;
        public float MicroHeightEligibilityMask;
        public float VoxelAnchorMask;
        public float SeepEligibilityMask;
    }

    public static class WorldTerrainMesoDetailFields
    {
        public const uint ContractVersion = 1u;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WorldTerrainMesoDetailParams CreateDefaultParams(uint seed)
        {
            return WorldTerrainMesoDetailParams.CreateDefault(seed);
        }

        public static WorldTerrainMesoDetailSample Evaluate(
            in WorldMacroGeologySample macro,
            float absoluteX,
            float absoluteZ,
            in WorldTerrainMesoDetailParams parameters)
        {
            WorldTerrainMesoDetailParams p = Sanitize(parameters);
            float extent = math.max(50f, p.PreviewExtentMeters);
            float detailGate = math.saturate(math.pow(10000f / math.max(100f, extent), 0.45f));
            float shelfBreak = math.saturate(macro.ShelfBreakMask);
            float shelf = math.saturate(macro.ShelfMask);
            float fault = math.saturate(macro.FaultMask);
            float ridge = math.saturate(macro.RidgeMask);
            float basin = math.saturate(macro.BasinMask);
            float trench = math.saturate(macro.TrenchMask);
            float slope = math.saturate(macro.Slope01);
            float curvature = math.saturate(macro.Curvature01);
            float depth = math.max(0f, macro.DepthMeters);

            float sediment = math.saturate((1f - slope) * 0.54f + basin * 0.36f + shelf * 0.18f - ridge * 0.24f - trench * 0.18f);
            float terracePatch = WorldMacroGeologyFields.FractalSimplexNoise01(new float2(absoluteX, absoluteZ) / 588f, p.Seed ^ 0x334EAA71u, 3);
            float terrace = math.saturate(shelfBreak * 0.45f + shelf * 0.12f + curvature * 0.16f - trench * 0.18f);
            terrace *= 0.24f + math.smoothstep(0.22f, 0.82f, terracePatch) * 0.76f;

            float slump = math.saturate(shelfBreak * 0.35f + slope * 0.35f + basin * 0.10f - ridge * 0.15f);
            float tributary = math.saturate(fault * 0.22f + shelfBreak * 0.24f + curvature * 0.30f + sediment * 0.12f);
            float talus = math.saturate(slope * 0.42f + ridge * 0.24f + fault * 0.18f - basin * 0.12f);
            float rubble = math.saturate(shelf * 0.20f + shelfBreak * 0.22f + talus * 0.32f + curvature * 0.16f);
            float reefDetail = math.saturate(shelf * 0.45f + (1f - slope) * 0.24f - trench * 0.40f);

            float terraceStep = 18f + shelfBreak * 48f + ridge * 24f;
            float terraceWarp = (WorldMacroGeologyFields.FractalSimplexNoise01(new float2(absoluteX, absoluteZ) / 910f, p.Seed ^ 0x7D4B9143u, 3) - 0.5f) * terraceStep * 0.42f;
            float terraceHeight = macro.HeightMeters + terraceWarp;
            float terraceLocal = terraceHeight / math.max(1f, terraceStep);
            float terraceBase = math.floor(terraceLocal);
            float terraceFrac = terraceLocal - terraceBase;
            // Organic smooth-step instead of primitive rounding
            float terraceSoft = terraceBase + math.smoothstep(0.25f, 0.75f, terraceFrac);
            float terraceOffset = terraceSoft * terraceStep - terraceHeight;
            
            // Mask out the terrace in steep areas to prevent texture stretching
            float terraceMask = math.smoothstep(0.85f, 0.45f, slope);
            
            float terraceDelta = (terraceOffset - terraceWarp * 0.18f) *
                terrace *
                terraceMask *
                p.TerraceStrengthMeters *
                detailGate;

            float channelNoise = WorldMacroGeologyFields.FractalSimplexNoise01(new float2(absoluteX, absoluteZ) / 238f, p.Seed ^ 0x58B9D13Du, 3);
            float channelWeave = WorldMacroGeologyFields.FractalSimplexNoise01(new float2(absoluteX, absoluteZ) / 769f, p.Seed ^ 0x9C31B8EFu, 3);
            float channelLines = math.smoothstep(0.56f, 0.94f, channelNoise) *
                tributary *
                (0.30f + math.smoothstep(0.12f, 0.84f, channelWeave) * 0.46f);
            float channelDelta = -channelLines *
                (2.4f + 11.0f * detailGate) *
                p.TributaryStrengthMeters *
                math.saturate(0.32f + shelfBreak * 0.72f + fault * 0.36f);

            float slumpLobes = math.smoothstep(0.58f, 0.91f, WorldMacroGeologyFields.FractalSimplexNoise01(new float2(absoluteX, absoluteZ) / 476f, p.Seed ^ 0x711CE4A9u, 3)) * slump;
            float slumpDelta = -slumpLobes * (5f + 34f * detailGate) * p.SlumpStrengthMeters;

            float talusNoise = WorldMacroGeologyFields.FractalSimplexNoise01(new float2(absoluteX, absoluteZ) / 100f, p.Seed ^ 0xA9C3EF17u, 3);
            float talusDelta = (talusNoise - 0.5f) * (4f + 16f * detailGate) * talus * p.TalusStrengthMeters;

            float rubbleNoise = WorldMacroGeologyFields.FractalSimplexNoise01(new float2(absoluteX, absoluteZ) / 45f, p.Seed ^ 0xC361A27Fu, 3);
            float rubbleDelta = (rubbleNoise - 0.5f) * (1.6f + 6.5f * detailGate) * rubble * p.RubbleStrengthMeters;

            float reefNoise = WorldMacroGeologyFields.FractalSimplexNoise01(new float2(absoluteX, absoluteZ) / 29f, p.Seed ^ 0x91D4C0DEu, 3);
            float reefDelta = (reefNoise - 0.5f) *
                (0.8f + 3.8f * detailGate) *
                reefDetail *
                math.smoothstep(0f, 120f, 120f - depth) *
                p.ReefStrengthMeters;

            float maxDelta = math.max(0.5f, p.MaxMesoDeltaMeters);
            if (extent < 512f)
                maxDelta = math.min(maxDelta, 7f);
            else if (extent < 1000f)
                maxDelta = math.min(maxDelta, 24f);

            // [MICRO-GEOLOGY CALIBRATION] Add Ridged Noise for Hard Rock/Talus
            float rockNoise1 = WorldMacroGeologyFields.FractalSimplexNoise01(new float2(absoluteX, absoluteZ) / 15f, p.Seed ^ 0x1A2B3C4Du, 3);
            float rockNoise2 = WorldMacroGeologyFields.FractalSimplexNoise01(new float2(absoluteX, absoluteZ) / 6f, p.Seed ^ 0x4D3C2B1Au, 3);
            float ridged1 = 1f - math.abs(rockNoise1 * 2f - 1f);
            float ridged2 = 1f - math.abs(rockNoise2 * 2f - 1f);
            // Sharp, aggressive erosion that bites into slopes and talus regions
            float rockErosion = (ridged1 * 0.7f + ridged2 * 0.3f) * math.saturate(talus + (slope * 2f));
            float rockDelta = -rockErosion * (4f + 16f * detailGate);

            float delta = math.clamp(
                terraceDelta + channelDelta + slumpDelta + talusDelta + rubbleDelta + reefDelta + rockDelta,
                -maxDelta,
                maxDelta);

            float roughness = math.saturate(talus * 0.30f + rubble * 0.26f + reefDetail * 0.18f + curvature * 0.16f + slope * 0.10f);

            return new WorldTerrainMesoDetailSample
            {
                BaseHeightMeters = macro.HeightMeters,
                DetailedHeightMeters = macro.HeightMeters + delta,
                HeightDeltaMeters = delta,
                TerraceMask = terrace,
                SlumpScarMask = slump,
                TributaryCanyonMask = tributary,
                TalusMask = talus,
                RubbleMask = rubble,
                ReefDetailMask = reefDetail,
                SedimentMask = sediment,
                ScatterRoughnessMask = roughness,
                MicroHeightEligibilityMask = math.saturate(roughness * 0.55f + reefDetail * 0.24f + rubble * 0.21f),
                VoxelAnchorMask = math.saturate(macro.VoxelSeamMask * 0.45f + talus * 0.28f + ridge * 0.18f + curvature * 0.20f),
                SeepEligibilityMask = math.saturate(macro.SeepMask * 0.58f + fault * 0.22f + tributary * 0.18f)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WorldTerrainDetailEligibilityFlags ResolveEligibilityFlags(
            in WorldMacroGeologySample macro,
            in WorldTerrainMesoDetailSample meso,
            in WorldTerrainSurfaceMaterialWeights materialWeights)
        {
            WorldTerrainDetailEligibilityFlags flags = WorldTerrainDetailEligibilityFlags.None;
            if (materialWeights.ShellSand > 0.24f || meso.SedimentMask > 0.56f)
                flags |= WorldTerrainDetailEligibilityFlags.SandScatter;
            if (materialWeights.HardRock > 0.24f || materialWeights.LimestoneShelf > 0.28f || meso.TalusMask > 0.36f)
                flags |= WorldTerrainDetailEligibilityFlags.RockScatter;
            if (materialWeights.ManganeseNodulePlain > 0.18f && macro.DepthMeters > 850f && macro.Slope01 < 0.48f)
                flags |= WorldTerrainDetailEligibilityFlags.NoduleScatter;
            if ((materialWeights.ReefRubble > 0.16f || meso.ReefDetailMask > 0.34f) && macro.DepthMeters < 220f)
                flags |= WorldTerrainDetailEligibilityFlags.ReefScatter;
            if (materialWeights.BrineSaltCrust > 0.16f)
                flags |= WorldTerrainDetailEligibilityFlags.BrineDeposit;
            if (materialWeights.SeepCrust > 0.14f || meso.SeepEligibilityMask > 0.42f)
                flags |= WorldTerrainDetailEligibilityFlags.SeepDeposit;
            if (meso.TalusMask > 0.42f)
                flags |= WorldTerrainDetailEligibilityFlags.TalusBoulder;
            if (meso.RubbleMask > 0.36f || meso.ReefDetailMask > 0.42f)
                flags |= WorldTerrainDetailEligibilityFlags.RubblePebble;
            if (meso.MicroHeightEligibilityMask > 0.30f)
                flags |= WorldTerrainDetailEligibilityFlags.DetailNormal;
            if (meso.ScatterRoughnessMask > 0.34f || materialWeights.BrineSaltCrust > 0.12f || materialWeights.SeepCrust > 0.12f)
                flags |= WorldTerrainDetailEligibilityFlags.DecalOverlay;
            if (meso.VoxelAnchorMask > 0.38f)
                flags |= WorldTerrainDetailEligibilityFlags.VoxelAnchor;
            if (meso.VoxelAnchorMask > 0.52f && macro.Slope01 > 0.42f && macro.HardRockExposureMask > 0.24f)
                flags |= WorldTerrainDetailEligibilityFlags.CaveMouthCandidate;

            return flags;
        }

        private static WorldTerrainMesoDetailParams Sanitize(WorldTerrainMesoDetailParams source)
        {
            source.PreviewExtentMeters = math.max(50f, source.PreviewExtentMeters);
            source.TerraceStrengthMeters = math.max(0f, source.TerraceStrengthMeters);
            source.SlumpStrengthMeters = math.max(0f, source.SlumpStrengthMeters);
            source.TributaryStrengthMeters = math.max(0f, source.TributaryStrengthMeters);
            source.TalusStrengthMeters = math.max(0f, source.TalusStrengthMeters);
            source.RubbleStrengthMeters = math.max(0f, source.RubbleStrengthMeters);
            source.ReefStrengthMeters = math.max(0f, source.ReefStrengthMeters);
            source.MaxMesoDeltaMeters = math.max(0.5f, source.MaxMesoDeltaMeters);
            return source;
        }

        private static float ValueNoise01(float absoluteX, float absoluteZ, uint seed, float cellSizeMeters)
        {
            float invCell = 1f / math.max(1f, cellSizeMeters);
            float sx = absoluteX * invCell;
            float sz = absoluteZ * invCell;
            float2 floorSample = math.floor(new float2(sx, sz));
            int2 cell = (int2)floorSample;
            float2 local = new float2(sx, sz) - floorSample;
            float2 smooth = local * local * (3f - 2f * local);
            float a = Hash01(cell.x, cell.y, seed);
            float b = Hash01(cell.x + 1, cell.y, seed);
            float c = Hash01(cell.x, cell.y + 1, seed);
            float d = Hash01(cell.x + 1, cell.y + 1, seed);
            return math.lerp(math.lerp(a, b, smooth.x), math.lerp(c, d, smooth.x), smooth.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Hash01(int x, int z, uint seed)
        {
            uint h = seed ^ 2166136261u;
            h = Mix(h ^ unchecked((uint)x));
            h = Mix(h ^ unchecked((uint)z));
            return (h & 0x00FFFFFFu) / 16777215f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }

    public static class WorldTerrainDetailContracts
    {
        public const uint ContractVersion = 2u;
        public const float AuthoredProofExtentMeters = WorldMacroGeologyFields.MinimumWorldExtentMeters;
        public const float RuntimeChunkSizeMeters = WorldMacroGeologyFields.DefaultChunkSizeMeters;
        public const float SpawnProofExtentMeters = 10_000f;
        public const float MesoMesoProofExtentMeters = 1_000f;
        public const float MesoProofExtentMeters = 512f;
        public const float MicroProofExtentMeters = 100f;
        public const float PrimaryShallowDepthMeters = 50f;
        public const float TransitionShallowDepthMeters = 100f;
        public const float UpperPlayableDepthMeters = 500f;
        public const float TargetMaxSingleIslandSquareKilometers = 1f;
        public const float HardMaxSingleIslandSquareKilometers = 2f;

        public static WorldTerrainDetailTierInfo NearPlayable => new WorldTerrainDetailTierInfo(
            WorldTerrainDetailTier.NearPlayable,
            513,
            1f,
            768f,
            WorldTerrainControlMapFlags.All);

        public static WorldTerrainDetailTierInfo MidTraversal => new WorldTerrainDetailTierInfo(
            WorldTerrainDetailTier.MidTraversal,
            257,
            2f,
            2_048f,
            WorldTerrainControlMapFlags.MacroHeight |
            WorldTerrainControlMapFlags.Slope |
            WorldTerrainControlMapFlags.Curvature |
            WorldTerrainControlMapFlags.ErosionFlow |
            WorldTerrainControlMapFlags.Terrace |
            WorldTerrainControlMapFlags.Slump |
            WorldTerrainControlMapFlags.Tributary |
            WorldTerrainControlMapFlags.MaterialRegion |
            WorldTerrainControlMapFlags.VoxelSeam);

        public static WorldTerrainDetailTierInfo FarSilhouette => new WorldTerrainDetailTierInfo(
            WorldTerrainDetailTier.FarSilhouette,
            129,
            4f,
            6_144f,
            WorldTerrainControlMapFlags.MacroHeight |
            WorldTerrainControlMapFlags.Slope |
            WorldTerrainControlMapFlags.MaterialRegion |
            WorldTerrainControlMapFlags.VoxelSeam);

        public static WorldTerrainDetailTierInfo DistantHlod => new WorldTerrainDetailTierInfo(
            WorldTerrainDetailTier.DistantHlod,
            65,
            8f,
            24_576f,
            WorldTerrainControlMapFlags.MacroHeight |
            WorldTerrainControlMapFlags.MaterialRegion);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WorldTerrainDetailTier ResolveTier(float distanceMeters)
        {
            if (distanceMeters <= NearPlayable.MaxDistanceMeters)
                return WorldTerrainDetailTier.NearPlayable;
            if (distanceMeters <= MidTraversal.MaxDistanceMeters)
                return WorldTerrainDetailTier.MidTraversal;
            if (distanceMeters <= FarSilhouette.MaxDistanceMeters)
                return WorldTerrainDetailTier.FarSilhouette;
            return WorldTerrainDetailTier.DistantHlod;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WorldTerrainDetailTierInfo ResolveTierInfo(WorldTerrainDetailTier tier)
        {
            switch (tier)
            {
                case WorldTerrainDetailTier.NearPlayable:
                    return NearPlayable;
                case WorldTerrainDetailTier.MidTraversal:
                    return MidTraversal;
                case WorldTerrainDetailTier.FarSilhouette:
                    return FarSilhouette;
                default:
                    return DistantHlod;
            }
        }
    }
}
