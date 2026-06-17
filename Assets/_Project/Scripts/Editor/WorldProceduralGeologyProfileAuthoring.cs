using System;
using System.Collections.Generic;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Creates and assigns explicit geology profiles for large-form geological families.
    /// </summary>
    public static class WorldProceduralGeologyProfileAuthoring
    {
        private const string ProceduralFamilyFolder = "Assets/_Project/Data/World/ProceduralFamilies";
        private const string GeologyProfileFolder = "Assets/_Project/Data/World/GenerativeGeologyProfiles";

        /// <summary>
        /// Creates or refreshes explicit geology profiles for key geological families.
        /// </summary>
        [MenuItem("Hecton8/Authoring/Ensure Procedural Geology Profiles", priority = 180)]
        public static void EnsureProfiles()
        {
            EnsureFolder("Assets/_Project/Data");
            EnsureFolder("Assets/_Project/Data/World");
            EnsureFolder(GeologyProfileFolder);

            Dictionary<string, WorldPrefabFamilyProfile> familiesById = LoadFamiliesById();
            GeologyProfileSpec[] specs = BuildSpecs();
            int updatedProfiles = 0;
            int assignedFamilies = 0;
            int missingFamilies = 0;

            for (int i = 0; i < specs.Length; i++)
            {
                GeologyProfileSpec spec = specs[i];
                WorldGenerativeGeologyProfile profile = LoadOrCreateProfile(spec, ref updatedProfiles);
                if (profile == null)
                    continue;

                if (!familiesById.TryGetValue(spec.FamilyId, out WorldPrefabFamilyProfile family) || family == null)
                {
                    missingFamilies++;
                    Debug.LogWarning($"[WorldProceduralGeologyProfileAuthoring] Missing family '{spec.FamilyId}' while assigning profile '{spec.ProfileId}'.");
                    continue;
                }

                if (family.generativeGeologyProfile != profile)
                {
                    family.generativeGeologyProfile = profile;
                    EditorUtility.SetDirty(family);
                    assignedFamilies++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[WorldProceduralGeologyProfileAuthoring] Ensured geology profiles. UpdatedProfiles={updatedProfiles}, AssignedFamilies={assignedFamilies}, MissingFamilies={missingFamilies}");
        }

        private static Dictionary<string, WorldPrefabFamilyProfile> LoadFamiliesById()
        {
            string[] familyGuids = AssetDatabase.FindAssets($"t:{nameof(WorldPrefabFamilyProfile)}", new[] { ProceduralFamilyFolder });
            Array.Sort(familyGuids, StringComparer.Ordinal);
            Dictionary<string, WorldPrefabFamilyProfile> familiesById = new Dictionary<string, WorldPrefabFamilyProfile>(16, StringComparer.Ordinal);

            for (int i = 0; i < familyGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(familyGuids[i]);
                WorldPrefabFamilyProfile family = AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>(assetPath);
                if (family == null || string.IsNullOrWhiteSpace(family.familyId))
                    continue;

                familiesById[family.familyId] = family;
            }

            return familiesById;
        }

        private static WorldGenerativeGeologyProfile LoadOrCreateProfile(GeologyProfileSpec spec, ref int updatedProfiles)
        {
            string assetPath = $"{GeologyProfileFolder}/{spec.AssetFileName}";
            WorldGenerativeGeologyProfile profile = AssetDatabase.LoadAssetAtPath<WorldGenerativeGeologyProfile>(assetPath);
            bool created = false;
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<WorldGenerativeGeologyProfile>();
                AssetDatabase.CreateAsset(profile, assetPath);
                created = true;
            }

            bool changed = ApplySpec(profile, spec);
            if (created || changed)
            {
                EditorUtility.SetDirty(profile);
                updatedProfiles++;
            }

            return profile;
        }

        private static bool ApplySpec(WorldGenerativeGeologyProfile profile, GeologyProfileSpec spec)
        {
            bool changed = false;
            changed |= SetIfDifferent(ref profile.profileId, spec.ProfileId);
            changed |= SetIfDifferent(ref profile.profileLabel, spec.ProfileLabel);
            changed |= SetIfDifferent(ref profile.generatorMode, spec.GeneratorMode);
            changed |= SetIfDifferent(ref profile.shapeArchetype, spec.ShapeArchetype);
            changed |= SetIfDifferent(ref profile.compositionMode, spec.CompositionMode);
            changed |= SetIfDifferent(ref profile.idealSlopeRange, spec.IdealSlopeRange);
            changed |= SetIfDifferent(ref profile.idealCurvatureRange, spec.IdealCurvatureRange);
            changed |= SetIfDifferent(ref profile.idealCaveProximityRange, spec.IdealCaveProximityRange);
            changed |= SetIfDifferent(ref profile.idealRidgeSignalRange, spec.IdealRidgeSignalRange);
            changed |= SetIfDifferent(ref profile.idealCanyonSignalRange, spec.IdealCanyonSignalRange);
            changed |= SetIfDifferent(ref profile.placementWeight, spec.PlacementWeight);
            changed |= SetIfDifferent(ref profile.compositionWeight, spec.CompositionWeight);
            changed |= SetIfDifferent(ref profile.contextPackThreshold, spec.ContextPackThreshold);
            changed |= SetIfDifferent(ref profile.terrainSeamMode, spec.TerrainSeamMode);
            changed |= SetIfDifferent(ref profile.caveBlendMode, spec.CaveBlendMode);
            changed |= SetIfDifferent(ref profile.seamBlendRadius, spec.SeamBlendRadius);
            changed |= SetIfDifferent(ref profile.terrainRaiseMeters, spec.TerrainRaiseMeters);
            changed |= SetIfDifferent(ref profile.terrainCutMeters, spec.TerrainCutMeters);
            changed |= SetIfDifferent(ref profile.debrisCountMin, spec.DebrisCountMin);
            changed |= SetIfDifferent(ref profile.debrisCountMax, spec.DebrisCountMax);
            changed |= SetIfDifferent(ref profile.lodCount, spec.LodCount);
            changed |= SetIfDifferent(ref profile.lodScreenHeights, spec.LodScreenHeights);
            changed |= SetIfDifferent(ref profile.futureModelId, spec.FutureModelId);
            changed |= SetIfDifferent(ref profile.neuralPromptHint, spec.NeuralPromptHint);
            return changed;
        }

        private static GeologyProfileSpec[] BuildSpecs()
        {
            return new[]
            {
                new GeologyProfileSpec(
                    "family.rock.small_floor",
                    "WorldGenerativeGeologyProfile_RockSmallFloor.asset",
                    "geology.rock.small_floor",
                    "Rock Small Floor",
                    WorldGenerativeGeologyProfile.GeneratorMode.HeuristicSdfFallback,
                    WorldGenerativeGeologyProfile.ShapeArchetype.ComplexRock,
                    WorldGenerativeGeologyProfile.CompositionMode.SingleFeature,
                    new Vector2(0f, 32f),
                    new Vector2(-0.22f, 0.28f),
                    new Vector2(0f, 0.35f),
                    new Vector2(0f, 0.52f),
                    new Vector2(0f, 0.46f),
                    0.92f,
                    0.98f,
                    0.5f,
                    WorldGenerativeGeologyProfile.TerrainSeamMode.HeightBlend,
                    WorldGenerativeGeologyProfile.CaveBlendMode.None,
                    5f,
                    0.3f,
                    0f,
                    0,
                    2,
                    3,
                    new Vector3(0.6f, 0.15f, 0.04f),
                    "geology.rock.small_floor.generated.v1",
                    "Generate low heavy floor stones with chipped silhouette, layered erosion, and seafloor-read shape without noisy micro-detail."),
                new GeologyProfileSpec(
                    "family.rock.cluster.medium",
                    "WorldGenerativeGeologyProfile_RockClusterMedium.asset",
                    "geology.rock.cluster.medium",
                    "Rock Cluster Medium",
                    WorldGenerativeGeologyProfile.GeneratorMode.HeuristicSdfFallback,
                    WorldGenerativeGeologyProfile.ShapeArchetype.ComplexRock,
                    WorldGenerativeGeologyProfile.CompositionMode.ContextPack,
                    new Vector2(0f, 40f),
                    new Vector2(-0.34f, 0.34f),
                    new Vector2(0f, 0.42f),
                    new Vector2(0.08f, 0.78f),
                    new Vector2(0f, 0.58f),
                    1.0f,
                    1.08f,
                    0.56f,
                    WorldGenerativeGeologyProfile.TerrainSeamMode.DebrisBridge,
                    WorldGenerativeGeologyProfile.CaveBlendMode.None,
                    8f,
                    0.8f,
                    0.5f,
                    1,
                    4,
                    3,
                    new Vector3(0.6f, 0.15f, 0.04f),
                    "geology.rock.cluster.medium.generated.v1",
                    "Generate medium rock clusters with 2-5 linked masses, strong cover silhouette, and erosion layers without turning into noise."),
                new GeologyProfileSpec(
                    "family.rock.shelf.large",
                    "WorldGenerativeGeologyProfile_RockShelfLarge.asset",
                    "geology.rock.shelf.large",
                    "Rock Shelf Large",
                    WorldGenerativeGeologyProfile.GeneratorMode.HeuristicSdfFallback,
                    WorldGenerativeGeologyProfile.ShapeArchetype.Canopy,
                    WorldGenerativeGeologyProfile.CompositionMode.PairedFeature,
                    new Vector2(8f, 52f),
                    new Vector2(-0.46f, 0.18f),
                    new Vector2(0f, 0.55f),
                    new Vector2(0.12f, 0.86f),
                    new Vector2(0.08f, 0.78f),
                    1.04f,
                    1.1f,
                    0.58f,
                    WorldGenerativeGeologyProfile.TerrainSeamMode.SdfBlend,
                    WorldGenerativeGeologyProfile.CaveBlendMode.ProbeOnly,
                    15f,
                    2.2f,
                    0.8f,
                    2,
                    6,
                    3,
                    new Vector3(0.6f, 0.15f, 0.04f),
                    "geology.rock.shelf.large.generated.v1",
                    "Generate broad cliff shelves with heavy side mass, layered ledges, overhangs, and readable route-support silhouette."),
                new GeologyProfileSpec(
                    "family.rock.arch.large",
                    "WorldGenerativeGeologyProfile_RockArchLarge.asset",
                    "geology.rock.arch.large",
                    "Rock Arch Large",
                    WorldGenerativeGeologyProfile.GeneratorMode.HeuristicSdfFallback,
                    WorldGenerativeGeologyProfile.ShapeArchetype.Arch,
                    WorldGenerativeGeologyProfile.CompositionMode.PairedFeature,
                    new Vector2(16f, 48f),
                    new Vector2(-0.42f, 0.24f),
                    new Vector2(0.1f, 0.72f),
                    new Vector2(0.42f, 1f),
                    new Vector2(0f, 0.52f),
                    1.05f,
                    1.12f,
                    0.6f,
                    WorldGenerativeGeologyProfile.TerrainSeamMode.SdfBlend,
                    WorldGenerativeGeologyProfile.CaveBlendMode.ProbeOnly,
                    14f,
                    2.5f,
                    2f,
                    3,
                    8,
                    3,
                    new Vector3(0.6f, 0.15f, 0.04f),
                    "geology.rock.arch.large.generated.v1",
                    "Generate an underwater eroded rock arch with layered strata, underside fracture detail, broad silhouette continuity, and seamless seabed integration."),
                new GeologyProfileSpec(
                    "family.cave.entrance",
                    "WorldGenerativeGeologyProfile_CaveEntrance.asset",
                    "geology.cave.entrance",
                    "Cave Entrance",
                    WorldGenerativeGeologyProfile.GeneratorMode.HeuristicSdfFallback,
                    WorldGenerativeGeologyProfile.ShapeArchetype.CaveBridge,
                    WorldGenerativeGeologyProfile.CompositionMode.SingleFeature,
                    new Vector2(14f, 54f),
                    new Vector2(-0.5f, 0.18f),
                    new Vector2(0.45f, 1f),
                    new Vector2(0.28f, 0.92f),
                    new Vector2(0f, 0.66f),
                    1.08f,
                    1.08f,
                    0.58f,
                    WorldGenerativeGeologyProfile.TerrainSeamMode.CarveAndDebris,
                    WorldGenerativeGeologyProfile.CaveBlendMode.CarvePortal,
                    16f,
                    2f,
                    3f,
                    4,
                    10,
                    3,
                    new Vector3(0.6f, 0.15f, 0.04f),
                    "geology.cave.entrance.generated.v1",
                    "Generate an underwater cave entrance with readable lip geology, sidewall anchoring, portal depth cues, fracture continuity, and natural debris seam breakup."),
                new GeologyProfileSpec(
                    "family.landmark.spire",
                    "WorldGenerativeGeologyProfile_LandmarkSpire.asset",
                    "geology.landmark.spire",
                    "Landmark Spire",
                    WorldGenerativeGeologyProfile.GeneratorMode.HeuristicSdfFallback,
                    WorldGenerativeGeologyProfile.ShapeArchetype.ComplexRock,
                    WorldGenerativeGeologyProfile.CompositionMode.ContextPack,
                    new Vector2(8f, 44f),
                    new Vector2(-0.28f, 0.42f),
                    new Vector2(0f, 0.48f),
                    new Vector2(0.22f, 1f),
                    new Vector2(0f, 0.54f),
                    1.02f,
                    1.16f,
                    0.62f,
                    WorldGenerativeGeologyProfile.TerrainSeamMode.DebrisBridge,
                    WorldGenerativeGeologyProfile.CaveBlendMode.None,
                    12f,
                    1.5f,
                    1.5f,
                    2,
                    6,
                    3,
                    new Vector3(0.6f, 0.15f, 0.04f),
                    "geology.landmark.spire.generated.v1",
                    "Generate a tall underwater geological spire with strong silhouette memory, layered erosion, base anchoring, and readable landmark-scale form from far distance.")
            };
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            int slashIndex = assetPath.LastIndexOf('/');
            if (slashIndex <= 0)
                return;

            string parent = assetPath.Substring(0, slashIndex);
            string name = assetPath.Substring(slashIndex + 1);
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(assetPath))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static bool SetIfDifferent<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            return true;
        }

        private struct GeologyProfileSpec
        {
            public GeologyProfileSpec(
                string familyId,
                string assetFileName,
                string profileId,
                string profileLabel,
                WorldGenerativeGeologyProfile.GeneratorMode generatorMode,
                WorldGenerativeGeologyProfile.ShapeArchetype shapeArchetype,
                WorldGenerativeGeologyProfile.CompositionMode compositionMode,
                Vector2 idealSlopeRange,
                Vector2 idealCurvatureRange,
                Vector2 idealCaveProximityRange,
                Vector2 idealRidgeSignalRange,
                Vector2 idealCanyonSignalRange,
                float placementWeight,
                float compositionWeight,
                float contextPackThreshold,
                WorldGenerativeGeologyProfile.TerrainSeamMode terrainSeamMode,
                WorldGenerativeGeologyProfile.CaveBlendMode caveBlendMode,
                float seamBlendRadius,
                float terrainRaiseMeters,
                float terrainCutMeters,
                int debrisCountMin,
                int debrisCountMax,
                int lodCount,
                Vector3 lodScreenHeights,
                string futureModelId,
                string neuralPromptHint)
            {
                FamilyId = familyId;
                AssetFileName = assetFileName;
                ProfileId = profileId;
                ProfileLabel = profileLabel;
                GeneratorMode = generatorMode;
                ShapeArchetype = shapeArchetype;
                CompositionMode = compositionMode;
                IdealSlopeRange = idealSlopeRange;
                IdealCurvatureRange = idealCurvatureRange;
                IdealCaveProximityRange = idealCaveProximityRange;
                IdealRidgeSignalRange = idealRidgeSignalRange;
                IdealCanyonSignalRange = idealCanyonSignalRange;
                PlacementWeight = placementWeight;
                CompositionWeight = compositionWeight;
                ContextPackThreshold = contextPackThreshold;
                TerrainSeamMode = terrainSeamMode;
                CaveBlendMode = caveBlendMode;
                SeamBlendRadius = seamBlendRadius;
                TerrainRaiseMeters = terrainRaiseMeters;
                TerrainCutMeters = terrainCutMeters;
                DebrisCountMin = debrisCountMin;
                DebrisCountMax = debrisCountMax;
                LodCount = lodCount;
                LodScreenHeights = lodScreenHeights;
                FutureModelId = futureModelId;
                NeuralPromptHint = neuralPromptHint;
            }

            public string FamilyId;
            public string AssetFileName;
            public string ProfileId;
            public string ProfileLabel;
            public WorldGenerativeGeologyProfile.GeneratorMode GeneratorMode;
            public WorldGenerativeGeologyProfile.ShapeArchetype ShapeArchetype;
            public WorldGenerativeGeologyProfile.CompositionMode CompositionMode;
            public Vector2 IdealSlopeRange;
            public Vector2 IdealCurvatureRange;
            public Vector2 IdealCaveProximityRange;
            public Vector2 IdealRidgeSignalRange;
            public Vector2 IdealCanyonSignalRange;
            public float PlacementWeight;
            public float CompositionWeight;
            public float ContextPackThreshold;
            public WorldGenerativeGeologyProfile.TerrainSeamMode TerrainSeamMode;
            public WorldGenerativeGeologyProfile.CaveBlendMode CaveBlendMode;
            public float SeamBlendRadius;
            public float TerrainRaiseMeters;
            public float TerrainCutMeters;
            public int DebrisCountMin;
            public int DebrisCountMax;
            public int LodCount;
            public Vector3 LodScreenHeights;
            public string FutureModelId;
            public string NeuralPromptHint;
        }
    }
}
