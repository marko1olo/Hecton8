using System.Collections.Generic;
using Hecton8.Environment;
using Hecton8.Items;
using Hecton8.Tools;
using Hecton8.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools
{
    public static class BiomeMatrixBootstrapAuthoring
    {
        private const string BiomeProfileFolder = "Assets/_Project/Data/Biomes/MatrixProfiles";
        private const string BiomeFamilyProfileFolder = "Assets/_Project/Data/Biomes/FamilyProfiles";
        private const string BiomeAtmosphereProfileFolder = "Assets/_Project/Data/Biomes/AtmosphereProfiles";
        private const string BiomeFaunaFamilyProfileFolder = "Assets/_Project/Data/Biomes/FaunaFamilies";
        private const string BiomePlayProfileFolder = "Assets/_Project/Data/Biomes/PlayProfiles";
        private const string BiomeResourcePlanProfileFolder = "Assets/_Project/Data/Biomes/ResourcePlans";
        private const string BiomeCatalogPath = "Assets/_Project/Data/Biomes/BiomeMatrixCatalog.asset";
        private const string WorldFamilyProfileFolder = "Assets/_Project/Data/World/FamilyProfiles";
        private const string WorldZonePlanFolder = "Assets/_Project/Data/World/ZonePlans";
        private const string ManagersRootName = "[MANAGERS]";

        [MenuItem("Hecton/Authoring/Rebuild 108 Biome Matrix", priority = 178)]
        public static void Rebuild108BiomeMatrix()
        {
            EnsureFolder("Assets/_Project/Data");
            EnsureFolder("Assets/_Project/Data/Biomes");
            EnsureFolder(BiomeProfileFolder);
            EnsureFolder(BiomeFamilyProfileFolder);
            EnsureFolder(BiomeAtmosphereProfileFolder);
            EnsureFolder(BiomeFaunaFamilyProfileFolder);
            EnsureFolder(BiomePlayProfileFolder);
            EnsureFolder(BiomeResourcePlanProfileFolder);

            HectonBiomeMatrixCatalog catalog = AssetDatabase.LoadAssetAtPath<HectonBiomeMatrixCatalog>(BiomeCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<HectonBiomeMatrixCatalog>();
                AssetDatabase.CreateAsset(catalog, BiomeCatalogPath);
            }

            HectonBiomeMatrixProfile[] profiles = new HectonBiomeMatrixProfile[108];

            for (int tier = 1; tier <= 27; tier++)
            {
                for (int regionIndex = 0; regionIndex < 4; regionIndex++)
                {
                    int matrixIndex = ((tier - 1) * 4) + regionIndex + 1;
                    HectonBiomeMatrixProfile.CardinalRegion region = (HectonBiomeMatrixProfile.CardinalRegion)regionIndex;
                    BiomeSeed seed = GetSeed(matrixIndex, tier, region);

                    string assetPath = $"{BiomeProfileFolder}/Biome_{matrixIndex:000}_{region}_{tier:00}.asset";
                    HectonBiomeMatrixProfile profile = AssetDatabase.LoadAssetAtPath<HectonBiomeMatrixProfile>(assetPath);
                    if (profile == null)
                    {
                        profile = ScriptableObject.CreateInstance<HectonBiomeMatrixProfile>();
                        AssetDatabase.CreateAsset(profile, assetPath);
                    }

                    profile.matrixIndex = matrixIndex;
                    profile.depthTier = tier;
                    profile.region = region;
                    profile.biomeName = seed.name;
                    profile.minDepthMeters = seed.minDepth;
                    profile.maxDepthMeters = seed.maxDepth;
                    profile.shortDescription = seed.description;
                    profile.isPlaceholder = seed.isPlaceholder;
                    profile.familyId = InferFamilyId(seed, tier, region);
                    profile.familyProfile = EnsureBiomeFamilyProfile(profile.familyId);
                    profile.suggestedZoneFamily = seed.suggestedZoneFamily;
                    profile.progressionRole = seed.progressionRole;
                    ApplyMatrixPlayerFraming(profile, tier, region);
                    EditorUtility.SetDirty(profile);

                    profiles[matrixIndex - 1] = profile;
                }
            }

            SerializedObject catalogSo = new SerializedObject(catalog);
            SerializedProperty array = catalogSo.FindProperty("profiles");
            array.arraySize = profiles.Length;
            for (int i = 0; i < profiles.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];
            catalogSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded)
            {
                GameObject managersRoot = GameObject.Find(ManagersRootName);
                if (managersRoot == null)
                    managersRoot = new GameObject(ManagersRootName);

                BiomeMatrixDirector director = managersRoot.GetComponent<BiomeMatrixDirector>();
                if (director == null)
                    director = managersRoot.AddComponent<BiomeMatrixDirector>();

                SerializedObject directorSo = new SerializedObject(director);
                directorSo.FindProperty("matrixCatalog").objectReferenceValue = catalog;
                directorSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(director);
                EditorSceneManager.MarkSceneDirty(activeScene);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BiomeMatrixBootstrap] 108-biome matrix rebuilt.");
        }

        [MenuItem("Hecton/Validation/Validate 108 Biome Matrix", priority = 237)]
        public static void Validate108BiomeMatrix()
        {
            int errorCount = 0;
            int warningCount = 0;

            HectonBiomeMatrixCatalog catalog = AssetDatabase.LoadAssetAtPath<HectonBiomeMatrixCatalog>(BiomeCatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[BiomeMatrixValidation] Missing BiomeMatrixCatalog asset.");
                return;
            }

            if (!catalog.Validate(out string error))
            {
                Debug.LogError($"[BiomeMatrixValidation] {error}", catalog);
                return;
            }

            int placeholderCount = 0;
            HashSet<string> familyIds = new HashSet<string>();
            for (int i = 0; i < catalog.Profiles.Length; i++)
            {
                HectonBiomeMatrixProfile profile = catalog.Profiles[i];
                if (profile == null)
                {
                    errorCount++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(profile.biomeName))
                {
                    Debug.LogError($"[BiomeMatrixValidation] Profile slot {i} has empty biomeName.", profile);
                    errorCount++;
                }

                if (profile.maxDepthMeters < profile.minDepthMeters)
                {
                    Debug.LogError($"[BiomeMatrixValidation] '{profile.biomeName}' has invalid depth range.", profile);
                    errorCount++;
                }

                if (string.IsNullOrWhiteSpace(profile.familyId))
                {
                    Debug.LogError($"[BiomeMatrixValidation] '{profile.biomeName}' is missing familyId.", profile);
                    errorCount++;
                }

                if (string.IsNullOrWhiteSpace(profile.visitPurpose))
                {
                    Debug.LogWarning($"[BiomeMatrixValidation] '{profile.biomeName}' is missing visitPurpose.", profile);
                    warningCount++;
                }

                if (string.IsNullOrWhiteSpace(profile.landmarkIdentity))
                {
                    Debug.LogWarning($"[BiomeMatrixValidation] '{profile.biomeName}' is missing landmarkIdentity.", profile);
                    warningCount++;
                }

                if (string.IsNullOrWhiteSpace(profile.riskSummary))
                {
                    Debug.LogWarning($"[BiomeMatrixValidation] '{profile.biomeName}' is missing riskSummary.", profile);
                    warningCount++;
                }

                if (profile.familyProfile == null)
                {
                    Debug.LogError($"[BiomeMatrixValidation] '{profile.biomeName}' is missing familyProfile.", profile);
                    errorCount++;
                }
                else
                {
                    familyIds.Add(profile.familyProfile.familyId);

                    if (profile.familyProfile.primaryResource == null)
                    {
                        Debug.LogWarning($"[BiomeMatrixValidation] Biome family '{profile.familyProfile.familyLabel}' has no primary resource.", profile.familyProfile);
                        warningCount++;
                    }

                    if (profile.familyProfile.atmosphereProfile == null)
                    {
                        Debug.LogWarning($"[BiomeMatrixValidation] Biome family '{profile.familyProfile.familyLabel}' has no atmosphere profile.", profile.familyProfile);
                        warningCount++;
                    }

                    if (profile.familyProfile.faunaFamilyProfile == null)
                    {
                        Debug.LogWarning($"[BiomeMatrixValidation] Biome family '{profile.familyProfile.familyLabel}' has no fauna family profile.", profile.familyProfile);
                        warningCount++;
                    }

                    if (profile.familyProfile.recommendedLoadoutPreset == null)
                    {
                        Debug.LogWarning($"[BiomeMatrixValidation] Biome family '{profile.familyProfile.familyLabel}' has no recommended loadout preset.", profile.familyProfile);
                        warningCount++;
                    }

                    if (profile.familyProfile.playProfile == null)
                    {
                        Debug.LogWarning($"[BiomeMatrixValidation] Biome family '{profile.familyProfile.familyLabel}' has no play profile.", profile.familyProfile);
                        warningCount++;
                    }

                    if (profile.familyProfile.resourcePlanProfile == null)
                    {
                        Debug.LogWarning($"[BiomeMatrixValidation] Biome family '{profile.familyProfile.familyLabel}' has no resource plan profile.", profile.familyProfile);
                        warningCount++;
                    }
                }

                if (profile.isPlaceholder)
                    placeholderCount++;
            }

            BiomeMatrixDirector director = Object.FindAnyObjectByType<BiomeMatrixDirector>(FindObjectsInactive.Include);
            if (director == null)
            {
                Debug.LogWarning("[BiomeMatrixValidation] Scene is missing BiomeMatrixDirector.");
                warningCount++;
            }

            if (errorCount == 0)
            {
                Debug.Log($"[BiomeMatrixValidation] PASS placeholders={placeholderCount} families={familyIds.Count} warnings={warningCount}");
                return;
            }

            Debug.LogWarning($"[BiomeMatrixValidation] COMPLETE errors={errorCount} warnings={warningCount} placeholders={placeholderCount}");
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            string[] parts = assetPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void ApplyMatrixPlayerFraming(
            HectonBiomeMatrixProfile profile,
            int tier,
            HectonBiomeMatrixProfile.CardinalRegion region)
        {
            HectonBiomeFamilyProfile family = profile.familyProfile;
            HectonBiomePlayProfile play = family != null ? family.playProfile : null;
            HectonFaunaFamilyProfile fauna = family != null ? family.faunaFamilyProfile : null;

            profile.visitPurpose = BuildVisitPurpose(family, tier, region);
            profile.commonRewardHook = BuildCommonRewardHook(family, tier);
            profile.rareRewardHook = BuildRareRewardHook(family, tier);
            profile.landmarkIdentity = BuildLandmarkIdentity(profile, tier, region);
            profile.safePocketIdentity = BuildSafePocketIdentity(family, tier, region);
            profile.riskSummary = BuildRiskSummary(family, fauna, tier, region);

            int baseRoutePressure = 3;
            int baseLandmarkStrength = 3;
            int baseRewardPull = 3;
            int baseSurvivalPressure = 3;

            if (play != null)
            {
                baseRoutePressure = Mathf.Clamp(6 - play.routeClarity, 1, 5);
                baseLandmarkStrength = Mathf.Clamp(play.landmarkStrength, 1, 5);
                baseRewardPull = Mathf.Clamp(Mathf.Max(play.commonResourceDensity, play.rareRewardPull), 1, 5);
                baseSurvivalPressure = Mathf.Clamp(Mathf.Max(play.encounterPressure, play.hazardPressure), 1, 5);
            }

            int depthPressureOffset = tier <= 4 ? -1 : tier <= 9 ? 0 : tier <= 14 ? 1 : 2;
            int depthRewardOffset = tier <= 4 ? -1 : tier <= 9 ? 0 : tier <= 20 ? 1 : 2;

            if (region == HectonBiomeMatrixProfile.CardinalRegion.East)
                baseLandmarkStrength++;
            if (region == HectonBiomeMatrixProfile.CardinalRegion.West)
                baseRoutePressure++;
            if (region == HectonBiomeMatrixProfile.CardinalRegion.South)
                baseSurvivalPressure++;

            profile.routePressure = Mathf.Clamp(baseRoutePressure + depthPressureOffset, 1, 5);
            profile.landmarkStrength = Mathf.Clamp(baseLandmarkStrength, 1, 5);
            profile.rewardPull = Mathf.Clamp(baseRewardPull + depthRewardOffset, 1, 5);
            profile.survivalPressure = Mathf.Clamp(baseSurvivalPressure + depthPressureOffset, 1, 5);
        }

        private static string BuildVisitPurpose(
            HectonBiomeFamilyProfile family,
            int tier,
            HectonBiomeMatrixProfile.CardinalRegion region)
        {
            string regionHook = region switch
            {
                HectonBiomeMatrixProfile.CardinalRegion.North => "follow strong natural shapes and search obvious pockets",
                HectonBiomeMatrixProfile.CardinalRegion.South => "work around bowls, pools, and local hotspots",
                HectonBiomeMatrixProfile.CardinalRegion.East => "read walls, fissures, and major vertical routes",
                _ => "keep orientation in soft terrain and hunt hidden value"
            };

            if (tier <= 4)
                return $"Early exploration biome. Come here to learn routes, gather basics, and {regionHook}.";
            if (tier <= 9)
                return $"Transition biome. Come here to push deeper, find better materials, and {regionHook}.";
            if (tier <= 14)
                return $"Abyss biome. Come here for specialized runs, rarer materials, and {regionHook}.";
            if (tier <= 20)
                return $"Deep-pressure biome. Come here with purpose, stronger gear, and a plan to {regionHook}.";

            return $"Late-game pressure biome. Come here only for major progress, rare extraction, and to {regionHook}.";
        }

        private static string BuildCommonRewardHook(HectonBiomeFamilyProfile family, int tier)
        {
            string primary = GetItemLabel(family != null ? family.primaryResource : null, "general salvage");
            string secondary = GetItemLabel(family != null ? family.secondaryResource : null, "support material");
            string prefix = tier <= 4 ? "Common runs pay out in" : tier <= 14 ? "Reliable value comes from" : "Even routine value here comes from";
            return $"{prefix} {primary} and {secondary}.";
        }

        private static string BuildRareRewardHook(HectonBiomeFamilyProfile family, int tier)
        {
            string tertiary = GetItemLabel(family != null ? family.tertiaryResource : null, "deep material");
            string signature = GetItemLabel(family != null ? family.signatureComponent : null, "advanced component");
            string prefix = tier <= 4 ? "Rare pull is modest" : tier <= 14 ? "The rare pull is real" : "The reason to risk the trip is";
            return $"{prefix}: {tertiary} and {signature}.";
        }

        private static string BuildLandmarkIdentity(
            HectonBiomeMatrixProfile profile,
            int tier,
            HectonBiomeMatrixProfile.CardinalRegion region)
        {
            string depthHook = tier <= 4 ? "bright and readable" : tier <= 9 ? "large and route-defining" : tier <= 20 ? "scarce but memorable" : "singular and intimidating";
            string regionHook = region switch
            {
                HectonBiomeMatrixProfile.CardinalRegion.North => "spires, steps, and big natural crowns",
                HectonBiomeMatrixProfile.CardinalRegion.South => "bowls, pools, domes, and hotspot basins",
                HectonBiomeMatrixProfile.CardinalRegion.East => "walls, fins, fissures, and giant vertical breaks",
                _ => "dunes, plains, void cuts, and shape changes in the floor"
            };

            return $"{profile.biomeName} reads through {regionHook}. Landmarks should feel {depthHook}.";
        }

        private static string BuildSafePocketIdentity(
            HectonBiomeFamilyProfile family,
            int tier,
            HectonBiomeMatrixProfile.CardinalRegion region)
        {
            if (tier <= 4)
                return "Safe pockets are frequent. The player should often see a clear reset point nearby.";
            if (tier <= 9)
                return region == HectonBiomeMatrixProfile.CardinalRegion.East
                    ? "Safe pockets live behind slab cover and under wall breaks."
                    : "Safe pockets exist, but they must be earned by reading the terrain.";
            if (tier <= 14)
                return "Safe pockets are scarce. Relief should come from one clear nook, not constant cover.";
            if (tier <= 20)
                return "Safe pockets are short-lived and mostly procedural: a lee side, a bowl edge, a dead current seam.";

            return family != null && family.familyId == "biome.family.rift_void"
                ? "Safe pockets are almost psychological only. The player survives by discipline, not comfort."
                : "Safe pockets are rare and temporary. Retreat routes matter more than comfort.";
        }

        private static string BuildRiskSummary(
            HectonBiomeFamilyProfile family,
            HectonFaunaFamilyProfile fauna,
            int tier,
            HectonBiomeMatrixProfile.CardinalRegion region)
        {
            string faunaRisk = fauna != null ? fauna.threatStyle : "mixed pressure";
            string regionRisk = region switch
            {
                HectonBiomeMatrixProfile.CardinalRegion.North => "bad drops and exposed lines",
                HectonBiomeMatrixProfile.CardinalRegion.South => "localized danger pockets and hostile basins",
                HectonBiomeMatrixProfile.CardinalRegion.East => "vertical trap geometry and hard commitment",
                _ => "orientation loss and long empty recovery swims"
            };

            if (tier <= 4)
                return $"Main failure mode: {regionRisk}. Creature pressure stays {faunaRisk}.";
            if (tier <= 14)
                return $"Main failure mode: {regionRisk}, then overstaying for loot. Creature pressure stays {faunaRisk}.";

            return $"Main failure mode: {regionRisk}, depth stress, and expensive hesitation. Creature pressure stays {faunaRisk}.";
        }

        private static string GetItemLabel(ItemData item, string fallback)
        {
            if (item == null)
                return fallback;

            return string.IsNullOrWhiteSpace(item.itemName) ? item.name : item.itemName;
        }

        private static BiomeSeed GetSeed(int matrixIndex, int tier, HectonBiomeMatrixProfile.CardinalRegion region)
        {
            if (ExplicitSeeds.TryGetValue(matrixIndex, out BiomeSeed explicitSeed))
                return explicitSeed;

            GetTierDepthRange(tier, out float minDepth, out float maxDepth);
            string regionName = region.ToString();
            return new BiomeSeed(
                $"Tier {tier:00} {regionName} Reserve",
                minDepth,
                maxDepth,
                $"Reserved biome slot for Tier {tier} / {regionName}. The 108-biome matrix expects a bespoke authored identity here; final lore and geology are pending expansion from the master vision.",
                true,
                InferSuggestedZoneFamily(region, tier),
                InferProgressionRole(tier));
        }

        private static string InferSuggestedZoneFamily(HectonBiomeMatrixProfile.CardinalRegion region, int tier)
        {
            if (tier <= 4)
                return region == HectonBiomeMatrixProfile.CardinalRegion.West ? "resources.pickups.near" : "resources.clutter.mid";
            if (tier <= 9)
                return region == HectonBiomeMatrixProfile.CardinalRegion.East ? "navigation.route.mid" : "resources.landmarks.far";
            if (tier <= 14)
                return region == HectonBiomeMatrixProfile.CardinalRegion.South ? "power.route.far" : "progression.route.mid";
            if (tier <= 20)
                return region == HectonBiomeMatrixProfile.CardinalRegion.North ? "progression.setpieces.near" : "combat.readability.mid";

            return region == HectonBiomeMatrixProfile.CardinalRegion.East ? "progression.skyline.far" : "combat.silhouette.far";
        }

        private static string InferProgressionRole(int tier)
        {
            if (tier <= 4)
                return "starter_surface";
            if (tier <= 9)
                return "slope_descent";
            if (tier <= 14)
                return "abyss_entry";
            if (tier <= 20)
                return "deep_pressure";

            return "final_hadal";
        }

        private static string InferFamilyId(
            BiomeSeed seed,
            int tier,
            HectonBiomeMatrixProfile.CardinalRegion region)
        {
            string text = $"{seed.name} {seed.description}".ToLowerInvariant();

            if (ContainsAny(text, "brine", "methane", "hydrothermal"))
                return "biome.family.chemosynthetic_brine";

            if (ContainsAny(text, "lava", "magma", "obsidian", "cinder", "ash", "pillow-lava", "glass"))
                return tier >= 18 ? "biome.family.volcanic_hadal" : "biome.family.volcanic_glass";

            if (ContainsAny(text, "iron", "metal", "pressure", "static matrix"))
                return "biome.family.metallic_hadal";

            if (ContainsAny(text, "crystal", "crystalline", "mineral"))
                return "biome.family.crystal_growth";

            if (ContainsAny(text, "coral", "reef", "fossil", "alabaster", "gallows"))
                return "biome.family.fossil_reef";

            if (ContainsAny(text, "tectonic", "spine", "ridge", "fracture", "shards", "steps", "slab", "prism"))
                return tier >= 16 ? "biome.family.rift_spine" : "biome.family.tectonic_spine";

            if (ContainsAny(text, "wall", "granite", "cliff", "mesa", "plateau", "sea-stack", "archipelago"))
                return tier <= 4 ? "biome.family.littoral_karst" : "biome.family.granite_escarpment";

            if (ContainsAny(text, "rift", "maw", "void", "catacombs", "shadow", "chute"))
                return "biome.family.rift_void";

            if (ContainsAny(text, "silt", "dune", "sediment", "pothole", "meander", "basin", "fan", "drain", "domes", "plain"))
                return tier >= 10 ? "biome.family.abyssal_silt" : "biome.family.sediment_drift";

            if (tier <= 3)
                return region == HectonBiomeMatrixProfile.CardinalRegion.West ? "biome.family.sediment_drift" : "biome.family.littoral_karst";
            if (tier <= 9)
                return "biome.family.tectonic_spine";
            if (tier <= 17)
                return "biome.family.abyssal_silt";
            if (tier <= 22)
                return "biome.family.rift_spine";

            return "biome.family.rift_void";
        }

        private static bool ContainsAny(string text, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
            {
                if (text.Contains(needles[i]))
                    return true;
            }

            return false;
        }

        private static HectonBiomeFamilyProfile EnsureBiomeFamilyProfile(string familyId)
        {
            if (string.IsNullOrWhiteSpace(familyId))
                familyId = "biome.family.generic";

            string assetPath = $"{BiomeFamilyProfileFolder}/BiomeFamilyProfile_{SanitizeId(familyId)}.asset";
            HectonBiomeFamilyProfile profile = AssetDatabase.LoadAssetAtPath<HectonBiomeFamilyProfile>(assetPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<HectonBiomeFamilyProfile>();
                AssetDatabase.CreateAsset(profile, assetPath);
            }

            ApplyFamilyTemplate(profile, familyId);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void ApplyFamilyTemplate(HectonBiomeFamilyProfile profile, string familyId)
        {
            profile.familyId = familyId;

            switch (familyId)
            {
                case "biome.family.littoral_karst":
                    profile.familyLabel = "Littoral Karst";
                    profile.debugColor = new Color(0.77f, 0.9f, 0.96f, 1f);
                    profile.geologicalIdentity = "Ð˜Ð·Ð²ÐµÑÑ‚Ð½ÑÐºÐ¾Ð²Ñ‹Ðµ ÑÑ‚Ð¾Ð»Ð±Ñ‹, ÐºÐ°Ñ€ÑÑ‚Ð¾Ð²Ñ‹Ðµ Ð°Ñ€ÐºÐ¸, ÑÐ²ÐµÑ‚Ð»Ñ‹Ðµ ÐºÑ€Ð¾Ð¼ÐºÐ¸ Ð¸ ÑÐ¸Ð»ÑŒÐ½Ð¾ Ñ‡Ð¸Ñ‚Ð°ÐµÐ¼Ñ‹Ðµ Ð¿Ñ€Ð¸Ð±Ñ€ÐµÐ¶Ð½Ñ‹Ðµ Ñ„Ð¾Ñ€Ð¼Ñ‹.";
                    profile.gameplayIdentity = "Ð‘ÐµÐ·Ð¾Ð¿Ð°ÑÐ½ÐµÐµ Ð´Ð»Ñ ÑÑ‚Ð°Ñ€Ñ‚Ð°, Ñ…Ð¾Ñ€Ð¾ÑˆÐ¾ Ð·Ð°Ð¿Ð¾Ð¼Ð¸Ð½Ð°ÐµÑ‚ÑÑ Ð¿Ð¾ ÑÐ¸Ð»ÑƒÑÑ‚Ñƒ, Ð²ÐµÐ´Ñ‘Ñ‚ Ð¸Ð³Ñ€Ð¾ÐºÐ° Ð¾Ñ‚ Ð¿Ð¾Ð²ÐµÑ€Ñ…Ð½Ð¾ÑÑ‚Ð¸ Ðº Ð¿ÐµÑ€Ð²Ñ‹Ð¼ ÑÐ±Ð¾Ñ€Ð½Ñ‹Ð¼ Ð¼Ð°Ñ€ÑˆÑ€ÑƒÑ‚Ð°Ð¼.";
                    profile.atmosphereMood = "bright_exposed";
                    profile.navigationStyle = "landmark_horizon";
                    profile.hazardStyle = "dropoffs_and_surf";
                    profile.landmarkStyle = "arches_and_stacks";
                    profile.primaryResourceTheme = "starter_scrap_and_shell_minerals";
                    profile.secondaryResourceTheme = "surface_organics";
                    profile.suggestedZoneFamily = "resources.landmarks.far";
                    profile.progressionFeeling = "surface_to_shelf";
                    profile.primaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_TitaniumScrap.asset");
                    profile.secondaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_SilicaShards.asset");
                    profile.tertiaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_FiberKelp.asset");
                    profile.signatureComponent = LoadItemData("Assets/_Project/Data/Items/Resources/Components/Comp_GlassPanel.asset");
                    profile.nearInteractiveFamily = LoadWorldFamilyProfile("FamilyProfile_resources_pickups_near.asset");
                    profile.midVisualFamily = LoadWorldFamilyProfile("FamilyProfile_resources_clutter_mid.asset");
                    profile.farSilhouetteFamily = LoadWorldFamilyProfile("FamilyProfile_navigation_silhouette_far.asset");
                    profile.preferredZonePlan = LoadZonePlanProfile("ZonePlan_ZoneProfile_Resources_Starter.asset");
                    break;

                case "biome.family.sediment_drift":
                    profile.familyLabel = "Sediment Drift";
                    profile.debugColor = new Color(0.87f, 0.81f, 0.67f, 1f);
                    profile.geologicalIdentity = "ÐÐ°Ð½Ð¾ÑÐ½Ñ‹Ðµ Ð¿Ð¾Ð»Ñ, Ð¼ÑÐ³ÐºÐ¸Ðµ Ð´ÑŽÐ½Ñ‹, Ð²Ð¾Ñ€Ð¾Ð½ÐºÐ¸ Ð¸ ÐºÐ°Ñ€Ð¼Ð°Ð½Ñ‹ Ñ€Ñ‹Ñ…Ð»Ð¾Ð³Ð¾ Ð¼Ð°Ñ‚ÐµÑ€Ð¸Ð°Ð»Ð°.";
                    profile.gameplayIdentity = "Ð¥Ð¾Ñ€Ð¾ÑˆÐ¾ Ñ€Ð°Ð±Ð¾Ñ‚Ð°ÐµÑ‚ ÐºÐ°Ðº ÑÐ¿Ð¾ÐºÐ¾Ð¹Ð½Ð°Ñ Ð·Ð¾Ð½Ð° ÑÐ±Ð¾Ñ€Ð° Ð¸ ÐºÐ°Ðº Ð¿ÐµÑ€ÐµÑ…Ð¾Ð´Ð½Ñ‹Ð¹ Ñ„Ð¾Ð½ Ð¼ÐµÐ¶Ð´Ñƒ Ð±Ð¾Ð»ÐµÐµ Ð²Ñ‹Ñ€Ð°Ð·Ð¸Ñ‚ÐµÐ»ÑŒÐ½Ñ‹Ð¼Ð¸ Ð¼ÐµÑÑ‚Ð°Ð¼Ð¸.";
                    profile.atmosphereMood = "soft_hazy";
                    profile.navigationStyle = "micro_relief";
                    profile.hazardStyle = "poor_visibility";
                    profile.landmarkStyle = "dunes_and_bowls";
                    profile.primaryResourceTheme = "silica_salts_and_loose_ore";
                    profile.secondaryResourceTheme = "light_organics";
                    profile.suggestedZoneFamily = "resources.clutter.mid";
                    profile.progressionFeeling = "calm_gathering";
                    profile.primaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_SilicaShards.asset");
                    profile.secondaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset");
                    profile.tertiaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_ElectrolyteSalts.asset");
                    profile.signatureComponent = LoadItemData("Assets/_Project/Data/Items/Resources/Components/Comp_SealantPack.asset");
                    profile.nearInteractiveFamily = LoadWorldFamilyProfile("FamilyProfile_resources_pickups_near.asset");
                    profile.midVisualFamily = LoadWorldFamilyProfile("FamilyProfile_resources_clutter_mid.asset");
                    profile.farSilhouetteFamily = LoadWorldFamilyProfile("FamilyProfile_resources_landmarks_far.asset");
                    profile.preferredZonePlan = LoadZonePlanProfile("ZonePlan_ZoneProfile_Resources_Starter.asset");
                    break;

                case "biome.family.granite_escarpment":
                    profile.familyLabel = "Granite Escarpment";
                    profile.debugColor = new Color(0.56f, 0.6f, 0.66f, 1f);
                    profile.geologicalIdentity = "Ð¡Ñ‚ÐµÐ½Ñ‹, ÑƒÑÑ‚ÑƒÐ¿Ñ‹, ÑÑƒÑ…Ð¸Ðµ Ð¿Ð»Ð°Ñ‚Ð¾ Ð¸ Ð±Ð¾Ð»ÑŒÑˆÐ¸Ðµ ÐºÐ°Ð¼ÐµÐ½Ð½Ñ‹Ðµ Ð³Ñ€Ð°Ð½Ð¸Ñ†Ñ‹ Ð¼Ð°Ñ€ÑˆÑ€ÑƒÑ‚Ð°.";
                    profile.gameplayIdentity = "Ð Ð°Ð±Ð¾Ñ‚Ð°ÐµÑ‚ ÐºÐ°Ðº Ð±Ð¾Ð»ÑŒÑˆÐ¾Ð¹ Ð¾Ñ€Ð¸ÐµÐ½Ñ‚Ð¸Ñ€ Ð¸ ÐºÑ€Ð°Ð¹ ÐºÐ°Ñ€Ñ‚Ñ‹ Ñ‚ÐµÐºÑƒÑ‰ÐµÐ³Ð¾ ÑÑ‚Ð°Ð¿Ð°, Ð¿Ð¾Ð´Ð´ÐµÑ€Ð¶Ð¸Ð²Ð°ÐµÑ‚ Ð½Ð°Ð²Ð¸Ð³Ð°Ñ†Ð¸ÑŽ Ð¿Ð¾ ÐºÐ¾Ð½Ñ‚ÑƒÑ€Ð°Ð¼.";
                    profile.atmosphereMood = "cold_clear";
                    profile.navigationStyle = "wall_following";
                    profile.hazardStyle = "sheer_drop";
                    profile.landmarkStyle = "walls_and_terraces";
                    profile.primaryResourceTheme = "structural_stone_and_metal_veins";
                    profile.secondaryResourceTheme = "salvage_caches";
                    profile.suggestedZoneFamily = "navigation.silhouette.far";
                    profile.progressionFeeling = "edge_descent";
                    profile.primaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_IronComposite.asset");
                    profile.secondaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset");
                    profile.tertiaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_TitaniumScrap.asset");
                    profile.signatureComponent = LoadItemData("Assets/_Project/Data/Items/Resources/Components/Comp_StructuralBracket.asset");
                    profile.nearInteractiveFamily = LoadWorldFamilyProfile("FamilyProfile_navigation_markers_near.asset");
                    profile.midVisualFamily = LoadWorldFamilyProfile("FamilyProfile_navigation_route_mid.asset");
                    profile.farSilhouetteFamily = LoadWorldFamilyProfile("FamilyProfile_navigation_silhouette_far.asset");
                    profile.preferredZonePlan = LoadZonePlanProfile("ZonePlan_ZoneProfile_Navigation_Mid.asset");
                    break;

                case "biome.family.fossil_reef":
                    profile.familyLabel = "Fossil Reef";
                    profile.debugColor = new Color(0.72f, 0.82f, 0.74f, 1f);
                    profile.geologicalIdentity = "ÐŸÐ¾Ñ€Ð¸ÑÑ‚Ñ‹Ðµ Ñ€Ð¸Ñ„Ñ‹, ÐºÐ¾Ñ€Ð°Ð»Ð»Ð¾Ð²Ñ‹Ðµ ÑÑ‚ÐµÐ½Ñ‹ Ð¸ Ð¾Ñ€Ð³Ð°Ð½Ð¸Ñ‡ÐµÑÐºÐ¸Ðµ ÐºÐ°Ñ€Ð¼Ð°Ð½Ñ‹ Ð² ÐºÐ°Ð¼Ð½Ðµ.";
                    profile.gameplayIdentity = "Ð”Ð°Ñ‘Ñ‚ Ð¾Ñ€Ð³Ð°Ð½Ð¸ÐºÑƒ, ÑƒÐºÑ€Ñ‹Ñ‚Ð¸Ñ, Ð½Ð¾Ñ€Ñ‹ Ð¸ Ñ…Ð¾Ñ€Ð¾ÑˆÑƒÑŽ Ð¿Ð°Ð¼ÑÑ‚ÑŒ Ð¼ÐµÑÑ‚Ð° Ð±ÐµÐ· Ð¿Ñ€ÑÐ¼Ð¾Ð¹ Ð°Ð³Ñ€ÐµÑÑÐ¸Ð¸ Ð¼Ð¸Ñ€Ð°.";
                    profile.atmosphereMood = "organic_murmur";
                    profile.navigationStyle = "honeycomb_paths";
                    profile.hazardStyle = "ambush_and_maze";
                    profile.landmarkStyle = "reef_walls";
                    profile.primaryResourceTheme = "fiber_membrane_and_biolum";
                    profile.secondaryResourceTheme = "fine_minerals";
                    profile.suggestedZoneFamily = "resources.pickups.near";
                    profile.progressionFeeling = "curious_exploration";
                    profile.primaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_FiberKelp.asset");
                    profile.secondaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_MembraneTissue.asset");
                    profile.tertiaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_BiolumPaste.asset");
                    profile.signatureComponent = LoadItemData("Assets/_Project/Data/Items/Resources/Components/Comp_FiberMesh.asset");
                    profile.nearInteractiveFamily = LoadWorldFamilyProfile("FamilyProfile_resources_pickups_near.asset");
                    profile.midVisualFamily = LoadWorldFamilyProfile("FamilyProfile_resources_landmarks_far.asset");
                    profile.farSilhouetteFamily = LoadWorldFamilyProfile("FamilyProfile_progression_skyline_far.asset");
                    profile.preferredZonePlan = LoadZonePlanProfile("ZonePlan_ZoneProfile_Resources_Starter.asset");
                    break;

                case "biome.family.tectonic_spine":
                    profile.familyLabel = "Tectonic Spine";
                    profile.debugColor = new Color(0.59f, 0.52f, 0.72f, 1f);
                    profile.geologicalIdentity = "Ð Ð°Ð·Ð»Ð¾Ð¼Ð½Ñ‹Ðµ Ð³Ñ€ÐµÐ±Ð½Ð¸, ÐºÐ°Ð¼ÐµÐ½Ð½Ñ‹Ðµ Ñ€Ñ‘Ð±Ñ€Ð°, ÑÑ‚ÑƒÐ¿ÐµÐ½Ð¸ Ð¸ Ñ€ÐµÐ·ÐºÐ¸Ðµ Ð¿Ð¾Ð²Ð¾Ñ€Ð¾Ñ‚Ñ‹ Ð¼Ð°Ñ€ÑˆÑ€ÑƒÑ‚Ð°.";
                    profile.gameplayIdentity = "Ð¥Ð¾Ñ€Ð¾ÑˆÐ¾ Ð²ÐµÐ´Ñ‘Ñ‚ Ð¸Ð³Ñ€Ð¾ÐºÐ° Ð²Ð½Ð¸Ð·, Ñ€Ð°Ð·Ð´ÐµÐ»ÑÐµÑ‚ Ð±ÐµÐ·Ð¾Ð¿Ð°ÑÐ½Ñ‹Ðµ Ð¸ Ð¾Ð¿Ð°ÑÐ½Ñ‹Ðµ ÑÑ‚Ð¾Ñ€Ð¾Ð½Ñ‹ Ð¸ Ð´ÐµÐ»Ð°ÐµÑ‚ Ð¿ÑƒÑ‚ÑŒ Ð·Ð°Ð¿Ð¾Ð¼Ð¸Ð½Ð°ÑŽÑ‰Ð¸Ð¼ÑÑ.";
                    profile.atmosphereMood = "tense_structural";
                    profile.navigationStyle = "ridge_tracking";
                    profile.hazardStyle = "pinch_points";
                    profile.landmarkStyle = "spines_and_steps";
                    profile.primaryResourceTheme = "copper_cobalt_and_support_minerals";
                    profile.secondaryResourceTheme = "route_salvage";
                    profile.suggestedZoneFamily = "progression.route.mid";
                    profile.progressionFeeling = "controlled_descent";
                    profile.primaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset");
                    profile.secondaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_CobaltAlloy.asset");
                    profile.tertiaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_IronComposite.asset");
                    profile.signatureComponent = LoadItemData("Assets/_Project/Data/Items/Resources/Components/Comp_CopperWire.asset");
                    profile.nearInteractiveFamily = LoadWorldFamilyProfile("FamilyProfile_navigation_markers_near.asset");
                    profile.midVisualFamily = LoadWorldFamilyProfile("FamilyProfile_progression_route_mid.asset");
                    profile.farSilhouetteFamily = LoadWorldFamilyProfile("FamilyProfile_progression_skyline_far.asset");
                    profile.preferredZonePlan = LoadZonePlanProfile("ZonePlan_ZoneProfile_Navigation_Mid.asset");
                    break;

                case "biome.family.crystal_growth":
                    profile.familyLabel = "Crystal Growth";
                    profile.debugColor = new Color(0.52f, 0.85f, 0.94f, 1f);
                    profile.geologicalIdentity = "ÐšÑ€Ð¸ÑÑ‚Ð°Ð»Ð»Ð¸Ñ‡ÐµÑÐºÐ¸Ðµ Ð²Ñ‹ÑÑ‚ÑƒÐ¿Ñ‹, Ð¸Ð³Ð»Ñ‹ Ð¸ Ð¼Ð¸Ð½ÐµÑ€Ð°Ð»ÑŒÐ½Ñ‹Ðµ Ð³Ñ€Ð¾Ð·Ð´Ð¸ Ñ ÑÐ¸Ð»ÑŒÐ½Ð¾Ð¹ Ð²Ð¸Ð·ÑƒÐ°Ð»ÑŒÐ½Ð¾Ð¹ Ð¸Ð½Ð´Ð¸Ð²Ð¸Ð´ÑƒÐ°Ð»ÑŒÐ½Ð¾ÑÑ‚ÑŒÑŽ.";
                    profile.gameplayIdentity = "Ð”Ð°Ñ‘Ñ‚ Ñ€ÐµÐ´ÐºÐ¸Ðµ Ð¼Ð°Ñ‚ÐµÑ€Ð¸Ð°Ð»Ñ‹ Ð¸ Ð¿Ð¾Ð¾Ñ‰Ñ€ÑÐµÑ‚ Ñ€Ð¸ÑÐºÐ¾Ð²Ð°Ð½Ð½Ñ‹Ðµ Ð·Ð°Ñ…Ð¾Ð´Ñ‹ Ñ€Ð°Ð´Ð¸ Ð²Ñ‹ÑÐ¾ÐºÐ¾Ñ†ÐµÐ½Ð½Ð¾Ð¹ Ð´Ð¾Ð±Ñ‹Ñ‡Ð¸.";
                    profile.atmosphereMood = "resonant_clear";
                    profile.navigationStyle = "needle_gardens";
                    profile.hazardStyle = "contact_damage";
                    profile.landmarkStyle = "crystal_spires";
                    profile.primaryResourceTheme = "silver_gold_lithium_rare_earth";
                    profile.secondaryResourceTheme = "precision_optics_materials";
                    profile.suggestedZoneFamily = "resources.landmarks.far";
                    profile.progressionFeeling = "rewarding_risk";
                    profile.primaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_SilverOre.asset");
                    profile.secondaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_GoldOre.asset");
                    profile.tertiaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_LithiumCrystal.asset");
                    profile.signatureComponent = LoadItemData("Assets/_Project/Data/Items/Resources/Components/Comp_PrecisionLens.asset");
                    profile.nearInteractiveFamily = LoadWorldFamilyProfile("FamilyProfile_resources_pickups_near.asset");
                    profile.midVisualFamily = LoadWorldFamilyProfile("FamilyProfile_resources_landmarks_far.asset");
                    profile.farSilhouetteFamily = LoadWorldFamilyProfile("FamilyProfile_progression_skyline_far.asset");
                    profile.preferredZonePlan = LoadZonePlanProfile("ZonePlan_ZoneProfile_Progression_Endgame.asset");
                    break;

                case "biome.family.abyssal_silt":
                    profile.familyLabel = "Abyssal Silt";
                    profile.debugColor = new Color(0.36f, 0.42f, 0.48f, 1f);
                    profile.geologicalIdentity = "Ð¢Ð¸Ñ…Ð¸Ðµ Ð³Ð»ÑƒÐ±Ð¾ÐºÐ¸Ðµ Ð¿Ð¾Ð»Ñ, Ð´Ð»Ð¸Ð½Ð½Ñ‹Ðµ Ð²Ð¾Ð»Ð½Ñ‹ Ð½Ð°Ð½Ð¾ÑÐ¾Ð² Ð¸ Ð¿Ñ€Ð¾ÑÑ‚Ñ€Ð°Ð½ÑÑ‚Ð²Ð¾, Ð³Ð´Ðµ Ñ„Ð¾Ñ€Ð¼Ð° Ñ‡Ð¸Ñ‚Ð°ÐµÑ‚ÑÑ ÑÐ»Ð°Ð±Ð¾.";
                    profile.gameplayIdentity = "Ð¢Ñ€ÐµÐ±ÑƒÐµÑ‚ ÑÐ²ÐµÑ‚Ð°, ÑÐºÐ°Ð½ÐµÑ€Ð° Ð¸ Ð¾Ð¿Ð¾Ñ€Ñ‹ Ð½Ð° Ð¾Ñ€Ð¸ÐµÐ½Ñ‚Ð¸Ñ€Ñ‹; Ñ…Ð¾Ñ€Ð¾ÑˆÐ¾ Ñ€Ð°Ð±Ð¾Ñ‚Ð°ÐµÑ‚ ÐºÐ°Ðº Ð½Ð°Ð¿Ñ€ÑÐ¶Ñ‘Ð½Ð½Ñ‹Ð¹ Ñ‚Ñ€Ð°Ð½Ð·Ð¸Ñ‚ Ð¸ Ð¿Ð¾Ð¸ÑÐº.";
                    profile.atmosphereMood = "mute_pressure";
                    profile.navigationStyle = "instrument_led";
                    profile.hazardStyle = "concealed_sinks";
                    profile.landmarkStyle = "negative_space";
                    profile.primaryResourceTheme = "bulk_metals_and_salts";
                    profile.secondaryResourceTheme = "salvage_drifts";
                    profile.suggestedZoneFamily = "navigation.route.mid";
                    profile.progressionFeeling = "isolation_and_scale";
                    profile.primaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_IronComposite.asset");
                    profile.secondaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_ElectrolyteSalts.asset");
                    profile.tertiaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_RareEarthDust.asset");
                    profile.signatureComponent = LoadItemData("Assets/_Project/Data/Items/Resources/Components/Comp_StabilizerCoil.asset");
                    profile.nearInteractiveFamily = LoadWorldFamilyProfile("FamilyProfile_navigation_markers_near.asset");
                    profile.midVisualFamily = LoadWorldFamilyProfile("FamilyProfile_navigation_route_mid.asset");
                    profile.farSilhouetteFamily = LoadWorldFamilyProfile("FamilyProfile_combat_silhouette_far.asset");
                    profile.preferredZonePlan = LoadZonePlanProfile("ZonePlan_ZoneProfile_Navigation_Mid.asset");
                    break;

                case "biome.family.volcanic_glass":
                    profile.familyLabel = "Volcanic Glass";
                    profile.debugColor = new Color(0.78f, 0.28f, 0.18f, 1f);
                    profile.geologicalIdentity = "ÐžÐ±ÑÐ¸Ð´Ð¸Ð°Ð½Ð¾Ð²Ñ‹Ðµ Ð¿Ð¾Ñ‚Ð¾ÐºÐ¸, ÑˆÐ»Ð°ÐºÐ¾Ð²Ñ‹Ðµ Ð¿Ð¾Ð»Ñ Ð¸ ÑÑ‚ÐµÐºÐ»ÑÐ½Ð½Ñ‹Ðµ ÐºÐ¾Ñ€ÐºÐ¸ Ñ Ð¶Ñ‘ÑÑ‚ÐºÐ¾Ð¹ Ð³ÐµÐ¾Ð¼ÐµÑ‚Ñ€Ð¸ÐµÐ¹.";
                    profile.gameplayIdentity = "Ð Ð°Ð±Ð¾Ñ‚Ð°ÐµÑ‚ ÐºÐ°Ðº Ð¾Ð¿Ð°ÑÐ½Ð°Ñ Ð³Ð¾Ñ€ÑÑ‡Ð°Ñ ÑÑ€ÐµÐ´Ð° Ñ ÑÐ¸Ð»ÑŒÐ½Ñ‹Ð¼Ð¸ ÑÐ¸Ð»ÑƒÑÑ‚Ð°Ð¼Ð¸ Ð¸ Ñ…Ð¾Ñ€Ð¾ÑˆÐ¸Ð¼Ð¸ power-Ð¼Ð°Ñ‚ÐµÑ€Ð¸Ð°Ð»Ð°Ð¼Ð¸.";
                    profile.atmosphereMood = "hot_menace";
                    profile.navigationStyle = "thermal_veins";
                    profile.hazardStyle = "heat_and_rupture";
                    profile.landmarkStyle = "black_flows";
                    profile.primaryResourceTheme = "sulfur_thermal_gel_tungsten";
                    profile.secondaryResourceTheme = "power_materials";
                    profile.suggestedZoneFamily = "hazard.probe";
                    profile.progressionFeeling = "volatile_pressure";
                    profile.primaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_SulfurClumps.asset");
                    profile.secondaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_ThermalGel.asset");
                    profile.tertiaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_TungstenChunk.asset");
                    profile.signatureComponent = LoadItemData("Assets/_Project/Data/Items/Resources/Components/Comp_CoolingCartridge.asset");
                    profile.nearInteractiveFamily = LoadWorldFamilyProfile("FamilyProfile_hazard_probe.asset");
                    profile.midVisualFamily = LoadWorldFamilyProfile("FamilyProfile_power_network_mid.asset");
                    profile.farSilhouetteFamily = LoadWorldFamilyProfile("FamilyProfile_progression_skyline_far.asset");
                    profile.preferredZonePlan = LoadZonePlanProfile("ZonePlan_ZoneProfile_Power_Mid.asset");
                    break;

                case "biome.family.volcanic_hadal":
                    profile.familyLabel = "Volcanic Hadal";
                    profile.debugColor = new Color(0.9f, 0.18f, 0.12f, 1f);
                    profile.geologicalIdentity = "ÐšÑ€Ð°Ð¹Ð½Ðµ Ð³Ð»ÑƒÐ±Ð¾ÐºÐ¸Ðµ Ð¼Ð°Ð³Ð¼Ð°Ñ‚Ð¸Ñ‡ÐµÑÐºÐ¸Ðµ Ð¸ Ð»Ð°Ð²Ð¾Ð²Ñ‹Ðµ Ð·Ð¾Ð½Ñ‹ Ñ ÐºÑ€Ð°ÑÐ½Ñ‹Ð¼ ÑÐ²ÐµÑ‚Ð¾Ð¼ Ð¸ ÑÐ¸Ð»ÑŒÐ½Ñ‹Ð¼ Ñ€Ð¸ÑÐºÐ¾Ð¼.";
                    profile.gameplayIdentity = "Ð­Ñ‚Ð¾ ÑƒÐ¶Ðµ Ð¿Ð¾Ñ€Ð¾Ð³ Ð¿Ð¾Ð·Ð´Ð½ÐµÐ¹ Ð¸Ð³Ñ€Ñ‹: Ð´Ð¾Ñ€Ð¾Ð³Ð¾, Ð¾Ð¿Ð°ÑÐ½Ð¾, Ð½Ð¾ Ð±Ð¾Ð³Ð°Ñ‚Ð¾ Ð½Ð° ÐºÑ€Ð¸Ñ‚Ð¸Ñ‡Ð½Ñ‹Ðµ Ð¼Ð°Ñ‚ÐµÑ€Ð¸Ð°Ð»Ñ‹.";
                    profile.atmosphereMood = "red_pressure";
                    profile.navigationStyle = "vent_lines";
                    profile.hazardStyle = "catastrophic_heat";
                    profile.landmarkStyle = "lava_seams";
                    profile.primaryResourceTheme = "endgame_heat_materials";
                    profile.secondaryResourceTheme = "power_core_materials";
                    profile.suggestedZoneFamily = "hazard.probe";
                    profile.progressionFeeling = "threshold_to_endgame";
                    profile.primaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_AbyssalCrystal.asset");
                    profile.secondaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_ThermalGel.asset");
                    profile.tertiaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_TungstenChunk.asset");
                    profile.signatureComponent = LoadItemData("Assets/_Project/Data/Items/Resources/Components/Comp_AbyssPressureShell.asset");
                    profile.nearInteractiveFamily = LoadWorldFamilyProfile("FamilyProfile_hazard_probe.asset");
                    profile.midVisualFamily = LoadWorldFamilyProfile("FamilyProfile_power_network_mid.asset");
                    profile.farSilhouetteFamily = LoadWorldFamilyProfile("FamilyProfile_progression_skyline_far.asset");
                    profile.preferredZonePlan = LoadZonePlanProfile("ZonePlan_ZoneProfile_Progression_Endgame.asset");
                    break;

                case "biome.family.chemosynthetic_brine":
                    profile.familyLabel = "Chemosynthetic Brine";
                    profile.debugColor = new Color(0.34f, 0.78f, 0.64f, 1f);
                    profile.geologicalIdentity = "Ð‘Ñ€Ð°Ð¹Ð½Ð¾Ð²Ñ‹Ðµ ÐºÐ°Ñ€Ð¼Ð°Ð½Ñ‹, Ð¼ÐµÑ‚Ð°Ð½Ð¾Ð²Ñ‹Ðµ ÐºÑƒÐ¿Ð¾Ð»Ð° Ð¸ Ñ…Ð¸Ð¼Ð¸Ñ‡ÐµÑÐºÐ¸Ðµ ÑÑ‚Ð¾Ð»Ð±Ñ‹, Ð³Ð´Ðµ ÑÐ°Ð¼Ð° ÑÑ€ÐµÐ´Ð° Ñ„Ð¾Ñ€Ð¼Ð¸Ñ€ÑƒÐµÑ‚ gameplay.";
                    profile.gameplayIdentity = "Ð—Ð¾Ð½Ð° Ð½Ð°ÑƒÑ‡Ð½Ð¾Ð³Ð¾ Ñ€Ð¸ÑÐºÐ°: Ð¼Ð½Ð¾Ð³Ð¾ ÑƒÐ³Ñ€Ð¾Ð· ÑÑ€ÐµÐ´Ñ‹, Ð½Ð¾ Ð¸ Ð¼Ð½Ð¾Ð³Ð¾ Ñ†ÐµÐ½Ð½Ñ‹Ñ… Ñ…Ð¸Ð¼Ð¸Ñ‡ÐµÑÐºÐ¸Ñ… Ð¸ power-Ð¼Ð°Ñ‚ÐµÑ€Ð¸Ð°Ð»Ð¾Ð².";
                    profile.atmosphereMood = "alien_chemical";
                    profile.navigationStyle = "hazard_channeling";
                    profile.hazardStyle = "chemical_pools";
                    profile.landmarkStyle = "vents_and_pools";
                    profile.primaryResourceTheme = "salts_resin_sulfur_nickel";
                    profile.secondaryResourceTheme = "enzyme_and_thermal_materials";
                    profile.suggestedZoneFamily = "power.route.far";
                    profile.progressionFeeling = "science_and_risk";
                    profile.primaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_ElectrolyteSalts.asset");
                    profile.secondaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_HydrocarbonResin.asset");
                    profile.tertiaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_SulfurClumps.asset");
                    profile.signatureComponent = LoadItemData("Assets/_Project/Data/Items/Resources/Components/Comp_PumpRotor.asset");
                    profile.nearInteractiveFamily = LoadWorldFamilyProfile("FamilyProfile_hazard_probe.asset");
                    profile.midVisualFamily = LoadWorldFamilyProfile("FamilyProfile_power_devices_near.asset");
                    profile.farSilhouetteFamily = LoadWorldFamilyProfile("FamilyProfile_power_route_far.asset");
                    profile.preferredZonePlan = LoadZonePlanProfile("ZonePlan_ZoneProfile_Power_Mid.asset");
                    break;

                case "biome.family.metallic_hadal":
                    profile.familyLabel = "Metallic Hadal";
                    profile.debugColor = new Color(0.74f, 0.52f, 0.34f, 1f);
                    profile.geologicalIdentity = "Ð–ÐµÐ»ÐµÐ·Ð½Ñ‹Ðµ Ð¿Ð¸ÐºÐ¸, Ð¼ÐµÑ‚Ð°Ð»Ð»Ð¸Ñ‡ÐµÑÐºÐ¸Ðµ Ð¿Ð»Ð°ÑÑ‚Ð¸Ð½Ñ‹ Ð¸ ÑÐ²ÐµÑ€Ñ…Ð´Ð°Ð²Ð»ÐµÐ½Ñ‡ÐµÑÐºÐ¸Ðµ Ð¶Ñ‘ÑÑ‚ÐºÐ¸Ðµ Ñ„Ð¾Ñ€Ð¼Ñ‹ Ð¿Ð¾Ð·Ð´Ð½ÐµÐ¹ Ð³Ð»ÑƒÐ±Ð¸Ð½Ñ‹.";
                    profile.gameplayIdentity = "ÐŸÐ¾Ð·Ð´Ð½Ð¸Ð¹ Ð¼Ð°Ñ‚ÐµÑ€Ð¸Ð°Ð»-Ñ…Ð°Ð½Ñ‚ Ð¸ Ñ‚ÐµÑ…Ð½Ð¸Ñ‡Ð½Ñ‹Ðµ Ð¼Ð°Ñ€ÑˆÑ€ÑƒÑ‚Ñ‹, Ð³Ð´Ðµ ÑƒÐ¶Ðµ Ð½ÑƒÐ¶Ð½Ð° ÑÐµÑ€ÑŒÑ‘Ð·Ð½Ð°Ñ ÑÐºÐ¸Ð¿Ð¸Ñ€Ð¾Ð²ÐºÐ°.";
                    profile.atmosphereMood = "industrial_hadal";
                    profile.navigationStyle = "spike_and_plate";
                    profile.hazardStyle = "abrasive_contact";
                    profile.landmarkStyle = "metal_needles";
                    profile.primaryResourceTheme = "nickel_tungsten_cobalt_abyssal_crystal";
                    profile.secondaryResourceTheme = "high_capacity_power_parts";
                    profile.suggestedZoneFamily = "progression.setpieces.near";
                    profile.progressionFeeling = "endgame_material_hunt";
                    profile.primaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_NickelOre.asset");
                    profile.secondaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_CobaltAlloy.asset");
                    profile.tertiaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_AbyssalCrystal.asset");
                    profile.signatureComponent = LoadItemData("Assets/_Project/Data/Items/Resources/Components/Comp_HighCapacityCell.asset");
                    profile.nearInteractiveFamily = LoadWorldFamilyProfile("FamilyProfile_progression_setpieces_near.asset");
                    profile.midVisualFamily = LoadWorldFamilyProfile("FamilyProfile_power_network_mid.asset");
                    profile.farSilhouetteFamily = LoadWorldFamilyProfile("FamilyProfile_progression_skyline_far.asset");
                    profile.preferredZonePlan = LoadZonePlanProfile("ZonePlan_ZoneProfile_Progression_Endgame.asset");
                    break;

                case "biome.family.rift_spine":
                    profile.familyLabel = "Rift Spine";
                    profile.debugColor = new Color(0.54f, 0.34f, 0.7f, 1f);
                    profile.geologicalIdentity = "ÐŸÐ¾Ð·Ð´Ð½ÐµÐ³Ð»ÑƒÐ±Ð¸Ð½Ð½Ñ‹Ðµ Ñ€Ð°Ð·Ð»Ð¾Ð¼Ð½Ñ‹Ðµ Ñ€Ñ‘Ð±Ñ€Ð° Ð¸ Ñ‰ÐµÐ»Ð¸, ÐºÐ¾Ñ‚Ð¾Ñ€Ñ‹Ðµ Ð½Ð°Ð¿Ñ€Ð°Ð²Ð»ÑÑŽÑ‚ Ð¸ ÑÐ¶Ð¸Ð¼Ð°ÑŽÑ‚ Ð¼Ð°Ñ€ÑˆÑ€ÑƒÑ‚.";
                    profile.gameplayIdentity = "Ð­Ñ‚Ð¾ Ð±Ð¸Ð¾Ð¼ Ð´Ð»Ñ Ð¿Ð¾Ð·Ð´Ð½ÐµÐ³Ð¾ Ð¿Ñ€Ð¾Ð´Ð²Ð¸Ð¶ÐµÐ½Ð¸Ñ: Ð¿ÑƒÑ‚ÑŒ Ñ‡Ð¸Ñ‚Ð°ÐµÑ‚ÑÑ Ð¿Ð¾ Ð±Ð¾Ð»ÑŒÑˆÐ¸Ð¼ Ñ„Ð¾Ñ€Ð¼Ð°Ð¼, Ð° Ð¾ÑˆÐ¸Ð±ÐºÐ° Ð´Ð¾Ñ€Ð¾Ð³Ð¾ ÑÑ‚Ð¾Ð¸Ñ‚.";
                    profile.atmosphereMood = "severe_directional";
                    profile.navigationStyle = "rift_edge_following";
                    profile.hazardStyle = "collapse_and_fall";
                    profile.landmarkStyle = "rift_fins";
                    profile.primaryResourceTheme = "deep_power_and_structural_ores";
                    profile.secondaryResourceTheme = "combat_salvage";
                    profile.suggestedZoneFamily = "progression.route.landmark";
                    profile.progressionFeeling = "late_route_commitment";
                    profile.primaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_TungstenChunk.asset");
                    profile.secondaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_RareEarthDust.asset");
                    profile.tertiaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_LithiumCrystal.asset");
                    profile.signatureComponent = LoadItemData("Assets/_Project/Data/Items/Resources/Components/Comp_PowerCoupler.asset");
                    profile.nearInteractiveFamily = LoadWorldFamilyProfile("FamilyProfile_progression_setpieces_near.asset");
                    profile.midVisualFamily = LoadWorldFamilyProfile("FamilyProfile_progression_route_mid.asset");
                    profile.farSilhouetteFamily = LoadWorldFamilyProfile("FamilyProfile_progression_skyline_far.asset");
                    profile.preferredZonePlan = LoadZonePlanProfile("ZonePlan_ZoneProfile_Progression_Endgame.asset");
                    break;

                case "biome.family.rift_void":
                default:
                    profile.familyLabel = "Rift Void";
                    profile.debugColor = new Color(0.24f, 0.24f, 0.34f, 1f);
                    profile.geologicalIdentity = "ÐŸÐ°ÑÑ‚Ð¸, ÑˆÐ°Ñ…Ñ‚Ñ‹, Ð¿Ñ€Ð¾Ð²Ð°Ð»Ñ‹ Ð¸ Ð¿ÑƒÑÑ‚Ð¾Ñ‚Ð½Ñ‹Ðµ ÐºÐ°Ñ€Ð¼Ð°Ð½Ñ‹, Ð³Ð´Ðµ ÑÐ°Ð¼ Ð¼Ð°ÑÑˆÑ‚Ð°Ð± Ð¿Ñ€Ð¾ÑÑ‚Ñ€Ð°Ð½ÑÑ‚Ð²Ð° ÑÑ‚Ð°Ð½Ð¾Ð²Ð¸Ñ‚ÑÑ ÑƒÐ³Ñ€Ð¾Ð·Ð¾Ð¹.";
                    profile.gameplayIdentity = "Ð­Ñ‚Ð¾ ÑƒÐ¶Ðµ Ð¿Ð¾Ð·Ð´Ð½ÑÑ Ð½Ð°Ð²Ð¸Ð³Ð°Ñ†Ð¸Ñ Ð½Ð° ÑÑ‚Ñ€Ð°Ñ…Ðµ, Ð³Ð»ÑƒÐ±Ð¸Ð½Ðµ Ð¸ Ñ€ÐµÐ´ÐºÐ¸Ñ… Ð¾Ñ€Ð¸ÐµÐ½Ñ‚Ð¸Ñ€Ð°Ñ…, Ð¿Ð¾Ñ‡Ñ‚Ð¸ Ð±ÐµÐ· Ð»Ð¸ÑˆÐ½ÐµÐ³Ð¾ ÑˆÑƒÐ¼Ð°.";
                    profile.atmosphereMood = "void_awe";
                    profile.navigationStyle = "anchor_to_anchor";
                    profile.hazardStyle = "falloff_and_ambush";
                    profile.landmarkStyle = "negative_depth_landmarks";
                    profile.primaryResourceTheme = "rare_salvage_and_abyssal_crystal";
                    profile.secondaryResourceTheme = "late_pressure_components";
                    profile.suggestedZoneFamily = "progression.route.landmark";
                    profile.progressionFeeling = "final_descent";
                    profile.primaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_AbyssalCrystal.asset");
                    profile.secondaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_RareEarthDust.asset");
                    profile.tertiaryResource = LoadItemData("Assets/_Project/Data/Items/Resources/Raw/Data_GoldOre.asset");
                    profile.signatureComponent = LoadItemData("Assets/_Project/Data/Items/Resources/Components/Comp_AbyssPressureShell.asset");
                    profile.nearInteractiveFamily = LoadWorldFamilyProfile("FamilyProfile_progression_setpieces_near.asset");
                    profile.midVisualFamily = LoadWorldFamilyProfile("FamilyProfile_progression_route_mid.asset");
                    profile.farSilhouetteFamily = LoadWorldFamilyProfile("FamilyProfile_progression_skyline_far.asset");
                    profile.preferredZonePlan = LoadZonePlanProfile("ZonePlan_ZoneProfile_Progression_Endgame.asset");
                    break;
            }

            ApplyFamilyEnvironment(profile, familyId);
            ApplyFamilyLoadout(profile, familyId);
            ApplyFamilyPlay(profile, familyId);
            ApplyFamilyResourcePlan(profile, familyId);
        }

        private static void ApplyFamilyEnvironment(HectonBiomeFamilyProfile profile, string familyId)
        {
            switch (familyId)
            {
                case "biome.family.littoral_karst":
                    profile.atmosphereProfile = EnsureAtmosphereProfile("Atmos_LittoralKarst.asset", new Color(0.62f, 0.76f, 0.84f, 1f), 0.006f, 1.35f, new Color(0.52f, 0.55f, 0.6f, 1f), 1.55f);
                    profile.faunaFamilyProfile = EnsureFaunaFamilyProfile("fauna.family.littoral_passive", "Littoral Passive", "shoals_and_grazers", "low", "small_surface_hunters", "curious_to_calm", "Ð¡Ð¿Ð¾ÐºÐ¾Ð¹Ð½Ð°Ñ ÑÑ‚Ð°Ñ€Ñ‚Ð¾Ð²Ð°Ñ Ñ„Ð°ÑƒÐ½Ð°. ÐœÐ¸Ñ€ Ð¶Ð¸Ð²Ð¾Ð¹, Ð½Ð¾ Ð½Ðµ Ð´ÑƒÑˆÐ¸Ñ‚ Ð¸Ð³Ñ€Ð¾ÐºÐ°.", "ÐœÐ½Ð¾Ð³Ð¾ Ð¼ÐµÐ»ÐºÐ¾Ð¹ Ð¶Ð¸Ð·Ð½Ð¸, Ñ€ÐµÐ´ÐºÐ¸Ðµ Ð½ÐµÐ±Ð¾Ð»ÑŒÑˆÐ¸Ðµ Ñ…Ð¸Ñ‰Ð½Ð¸ÐºÐ¸.");
                    break;

                case "biome.family.sediment_drift":
                    profile.atmosphereProfile = EnsureAtmosphereProfile("Atmos_SedimentDrift.asset", new Color(0.54f, 0.6f, 0.58f, 1f), 0.01f, 1.05f, new Color(0.42f, 0.42f, 0.46f, 1f), 1.1f);
                    profile.faunaFamilyProfile = EnsureFaunaFamilyProfile("fauna.family.sediment_scavengers", "Sediment Scavengers", "burrowers_and_sifters", "low_to_medium", "ambush_flatfish", "calm_with_spikes", "ÐœÑÐ³ÐºÐ°Ñ Ð·Ð¾Ð½Ð° Ñ Ñ€Ð¾ÑŽÑ‰ÐµÐ¹ÑÑ Ð´Ð¾Ð½Ð½Ð¾Ð¹ Ð¶Ð¸Ð·Ð½ÑŒÑŽ Ð¸ Ñ€ÐµÐ´ÐºÐ¸Ð¼Ð¸ Ð²ÑÐ¿Ð»ÐµÑÐºÐ°Ð¼Ð¸ Ð¾Ð¿Ð°ÑÐ½Ð¾ÑÑ‚Ð¸.", "ÐžÑÐ½Ð¾Ð²Ð½Ð°Ñ Ð¶Ð¸Ð·Ð½ÑŒ ÑÐºÑ€Ñ‹Ñ‚Ð° Ð² Ð½Ð°Ð½Ð¾ÑÐ°Ñ….");
                    break;

                case "biome.family.granite_escarpment":
                    profile.atmosphereProfile = EnsureAtmosphereProfile("Atmos_GraniteEscarpment.asset", new Color(0.36f, 0.44f, 0.54f, 1f), 0.008f, 0.92f, new Color(0.34f, 0.36f, 0.42f, 1f), 0.95f);
                    profile.faunaFamilyProfile = EnsureFaunaFamilyProfile("fauna.family.escarpment_watchers", "Escarpment Watchers", "cling_and_hover", "medium", "wall_stalker", "watchful", "Ð¤Ð°ÑƒÐ½Ð° Ð¿Ñ€Ð¸Ð²ÑÐ·Ð°Ð½Ð° Ðº ÑÑ‚ÐµÐ½Ð°Ð¼ Ð¸ ÑƒÑÑ‚ÑƒÐ¿Ð°Ð¼, Ð° Ð½Ðµ Ðº Ð¾Ñ‚ÐºÑ€Ñ‹Ñ‚Ð¾Ð¼Ñƒ Ð¿Ð¾Ð»ÑŽ.", "ÐžÐ¿Ð°ÑÐ½Ð¾ÑÑ‚ÑŒ ÑÐ¼Ð¾Ñ‚Ñ€Ð¸Ñ‚ Ð½Ð° Ð¼Ð°Ñ€ÑˆÑ€ÑƒÑ‚ ÑÐ²ÐµÑ€Ñ…Ñƒ Ð¸ ÑÐ±Ð¾ÐºÑƒ.");
                    break;

                case "biome.family.fossil_reef":
                    profile.atmosphereProfile = EnsureAtmosphereProfile("Atmos_FossilReef.asset", new Color(0.26f, 0.46f, 0.38f, 1f), 0.012f, 0.88f, new Color(0.24f, 0.34f, 0.3f, 1f), 0.82f);
                    profile.faunaFamilyProfile = EnsureFaunaFamilyProfile("fauna.family.reef_ambush", "Reef Ambush", "reef_filter_and_nest", "medium", "pocket_ambusher", "patchy_pressure", "ÐŸÐ¾Ñ€Ð¸ÑÑ‚Ð°Ñ ÑÑ€ÐµÐ´Ð° ÑÐ¾Ð·Ð´Ð°Ñ‘Ñ‚ ÑƒÐºÑ€Ñ‹Ñ‚Ð¸Ñ Ð¸ Ð»Ð¾ÐºÐ°Ð»ÑŒÐ½Ñ‹Ðµ Ð·Ð°ÑÐ°Ð´Ñ‹.", "ÐœÐ½Ð¾Ð³Ð¾ ÐºÐ°Ñ€Ð¼Ð°Ð½Ð¾Ð² Ð¶Ð¸Ð·Ð½Ð¸ Ð¸ Ñ€ÐµÐ·ÐºÐ¸Ðµ ÐºÐ¾Ñ€Ð¾Ñ‚ÐºÐ¸Ðµ ÑƒÐ³Ñ€Ð¾Ð·Ñ‹.");
                    break;

                case "biome.family.tectonic_spine":
                    profile.atmosphereProfile = EnsureAtmosphereProfile("Atmos_TectonicSpine.asset", new Color(0.22f, 0.28f, 0.46f, 1f), 0.014f, 0.8f, new Color(0.2f, 0.22f, 0.28f, 1f), 0.7f);
                    profile.faunaFamilyProfile = EnsureFaunaFamilyProfile("fauna.family.ridge_hunters", "Ridge Hunters", "gliders_and_edge_hunters", "medium", "ridge_hunter", "route_pressure", "Ð¤Ð°ÑƒÐ½Ð° Ñ€Ð°Ð±Ð¾Ñ‚Ð°ÐµÑ‚ Ð²Ð´Ð¾Ð»ÑŒ Ð³Ñ€ÐµÐ±Ð½ÐµÐ¹ Ð¸ Ð½Ð°Ð¿Ñ€Ð°Ð²Ð»ÑÐµÑ‚ Ð¿Ð¾Ð²ÐµÐ´ÐµÐ½Ð¸Ðµ Ð¸Ð³Ñ€Ð¾ÐºÐ° Ñ‡ÐµÑ€ÐµÐ· Ñ„Ð¾Ñ€Ð¼Ñƒ Ð¼Ð°Ñ€ÑˆÑ€ÑƒÑ‚Ð°.", "Ð¥Ð¸Ñ‰Ð½Ð¸ÐºÐ¸ Ð»ÑŽÐ±ÑÑ‚ ÑƒÐ·ÐºÐ¸Ðµ Ð»Ð¸Ð½Ð¸Ð¸ Ð¿ÑƒÑ‚Ð¸.");
                    break;

                case "biome.family.crystal_growth":
                    profile.atmosphereProfile = EnsureAtmosphereProfile("Atmos_CrystalGrowth.asset", new Color(0.18f, 0.42f, 0.55f, 1f), 0.009f, 1.2f, new Color(0.26f, 0.34f, 0.42f, 1f), 0.9f);
                    profile.faunaFamilyProfile = EnsureFaunaFamilyProfile("fauna.family.crystal_skittish", "Crystal Skittish", "small_spark_life", "medium", "needle_hunter", "nervous_and_sparse", "Ð ÐµÐ´ÐºÐ¸Ðµ Ñ€ÐµÑÑƒÑ€ÑÑ‹ Ð´ÐµÐ»Ð°ÑŽÑ‚ Ð´Ð°Ð¶Ðµ Ñ‚Ð¸Ñ…ÑƒÑŽ Ð¶Ð¸Ð·Ð½ÑŒ Ñ†ÐµÐ½Ð½Ð¾Ð¹ Ð´Ð»Ñ Ð½Ð°Ð±Ð»ÑŽÐ´ÐµÐ½Ð¸Ñ.", "ÐœÐµÐ»ÐºÐ°Ñ Ð¶Ð¸Ð·Ð½ÑŒ ÐºÑ€Ð°ÑÐ¸Ð²Ð¾ ÑÑ‡Ð¸Ñ‚Ñ‹Ð²Ð°ÐµÑ‚ Ð¼Ð¸Ð½ÐµÑ€Ð°Ð»Ñ‹, ÐºÑ€ÑƒÐ¿Ð½Ð°Ñ Ð²ÑÑ‚Ñ€ÐµÑ‡Ð°ÐµÑ‚ÑÑ Ñ€ÐµÐ´ÐºÐ¾.");
                    break;

                case "biome.family.abyssal_silt":
                    profile.atmosphereProfile = EnsureAtmosphereProfile("Atmos_AbyssalSilt.asset", new Color(0.08f, 0.11f, 0.16f, 1f), 0.022f, 0.55f, new Color(0.12f, 0.14f, 0.18f, 1f), 0.35f);
                    profile.faunaFamilyProfile = EnsureFaunaFamilyProfile("fauna.family.abyssal_sparse", "Abyssal Sparse", "rare_sifters", "medium_to_high", "shadow_interceptor", "long_silence_spikes", "ÐŸÐ¾Ñ‡Ñ‚Ð¸ Ð¿ÑƒÑÑ‚Ð¾Ñ‚Ð° Ð¼ÐµÐ¶Ð´Ñƒ Ð²ÑÑ‚Ñ€ÐµÑ‡Ð°Ð¼Ð¸. ÐÐ°Ð¿Ñ€ÑÐ¶ÐµÐ½Ð¸Ðµ Ð¸Ð´Ñ‘Ñ‚ Ð¾Ñ‚ Ñ€ÐµÐ´ÐºÐ¸Ñ… ÐºÐ¾Ð½Ñ‚Ð°ÐºÑ‚Ð¾Ð².", "Ð¢Ð¸ÑˆÐ¸Ð½Ð° â€” Ð³Ð»Ð°Ð²Ð½Ñ‹Ð¹ ÑÑ„Ñ„ÐµÐºÑ‚ ÑÑ‚Ð¾Ð³Ð¾ ÑÐµÐ¼ÐµÐ¹ÑÑ‚Ð²Ð°.");
                    break;

                case "biome.family.volcanic_glass":
                    profile.atmosphereProfile = EnsureAtmosphereProfile("Atmos_VolcanicGlass.asset", new Color(0.18f, 0.07f, 0.05f, 1f), 0.028f, 0.68f, new Color(0.22f, 0.11f, 0.08f, 1f), 0.42f);
                    profile.faunaFamilyProfile = EnsureFaunaFamilyProfile("fauna.family.thermal_hostile", "Thermal Hostile", "vent_scavengers", "high", "heat_lurker", "hostile_pulses", "Ð“Ð¾Ñ€ÑÑ‡Ð°Ñ ÑÑ€ÐµÐ´Ð° Ñ Ñ€ÐµÐ´ÐºÐ¾Ð¹, Ð½Ð¾ Ð·Ð»Ð¾Ð¹ Ð¶Ð¸Ð·Ð½ÑŒÑŽ.", "ÐšÐ¾Ð½Ñ‚Ð°ÐºÑ‚Ñ‹ Ð·Ð´ÐµÑÑŒ Ð¾Ð¿Ð°ÑÐ½ÐµÐµ, Ñ‡ÐµÐ¼ Ñ‡Ð°Ñ‰Ðµ.");
                    break;

                case "biome.family.volcanic_hadal":
                    profile.atmosphereProfile = EnsureAtmosphereProfile("Atmos_VolcanicHadal.asset", new Color(0.16f, 0.03f, 0.03f, 1f), 0.036f, 0.52f, new Color(0.18f, 0.06f, 0.05f, 1f), 0.28f);
                    profile.faunaFamilyProfile = EnsureFaunaFamilyProfile("fauna.family.hadal_apex", "Hadal Apex", "scarce_heatproof_life", "extreme", "apex_leviathan_presence", "rare_but_dreaded", "ÐŸÐ¾Ð·Ð´Ð½ÑÑ Ð³Ð»ÑƒÐ±Ð¸Ð½Ð° Ñ Ð¾Ñ‡ÐµÐ½ÑŒ Ñ€ÐµÐ´ÐºÐ¾Ð¹, Ð½Ð¾ Ð¿ÑƒÐ³Ð°ÑŽÑ‰ÐµÐ¹ Ð²ÐµÑ€Ñ…ÑƒÑˆÐµÑ‡Ð½Ð¾Ð¹ ÑƒÐ³Ñ€Ð¾Ð·Ð¾Ð¹.", "Ð˜Ð³Ñ€Ð¾Ðº Ñ‡Ð°Ñ‰Ðµ Ñ‡ÑƒÐ²ÑÑ‚Ð²ÑƒÐµÑ‚ ÑÑ‚Ñ€Ð°Ñ… Ð¾Ð¶Ð¸Ð´Ð°Ð½Ð¸Ñ, Ñ‡ÐµÐ¼ Ð±Ð¾Ð¹.");
                    break;

                case "biome.family.chemosynthetic_brine":
                    profile.atmosphereProfile = EnsureAtmosphereProfile("Atmos_ChemosyntheticBrine.asset", new Color(0.09f, 0.24f, 0.18f, 1f), 0.026f, 0.74f, new Color(0.16f, 0.24f, 0.2f, 1f), 0.46f);
                    profile.faunaFamilyProfile = EnsureFaunaFamilyProfile("fauna.family.chemical_specialists", "Chemical Specialists", "vent_feeders", "high", "brine_stalker", "patchy_hotspots", "Ð–Ð¸Ð·Ð½ÑŒ ÐºÐ¾Ð½Ñ†ÐµÐ½Ñ‚Ñ€Ð¸Ñ€ÑƒÐµÑ‚ÑÑ Ð²Ð¾ÐºÑ€ÑƒÐ³ Ñ…Ð¸Ð¼Ð¸Ñ‡ÐµÑÐºÐ¸Ñ… ÐºÐ°Ñ€Ð¼Ð°Ð½Ð¾Ð², Ð° Ð½Ðµ Ð¿Ð¾ Ð²ÑÐµÐ¼Ñƒ Ð±Ð¸Ð¾Ð¼Ñƒ.", "Ð§ÐµÐ¼ Ð°ÐºÑ‚Ð¸Ð²Ð½ÐµÐµ ÐºÐ°Ñ€Ð¼Ð°Ð½, Ñ‚ÐµÐ¼ Ð¾Ð¿Ð°ÑÐ½ÐµÐµ ÐµÐ³Ð¾ ÑÐºÐ¾Ð»Ð¾Ð³Ð¸Ñ.");
                    break;

                case "biome.family.metallic_hadal":
                    profile.atmosphereProfile = EnsureAtmosphereProfile("Atmos_MetallicHadal.asset", new Color(0.14f, 0.1f, 0.08f, 1f), 0.03f, 0.6f, new Color(0.18f, 0.16f, 0.14f, 1f), 0.34f);
                    profile.faunaFamilyProfile = EnsureFaunaFamilyProfile("fauna.family.metal_predators", "Metal Predators", "scarce_hard_shell_life", "high", "armor_breaker", "measured_aggression", "Ð–Ñ‘ÑÑ‚ÐºÐ°Ñ Ð¿Ð¾Ð·Ð´Ð½ÑÑ ÑÑ€ÐµÐ´Ð° Ñ Ñ‚ÑÐ¶Ñ‘Ð»Ð¾Ð¹, Ñ€ÐµÐ´ÐºÐ¾Ð¹ Ð¸ Ð¾Ð¿Ð°ÑÐ½Ð¾Ð¹ Ñ„Ð°ÑƒÐ½Ð¾Ð¹.", "Ð¢ÑƒÑ‚ Ð²Ð°Ð¶Ð½ÐµÐµ Ñ†ÐµÐ½Ð° ÐºÐ¾Ð½Ñ‚Ð°ÐºÑ‚Ð°, Ñ‡ÐµÐ¼ ÐµÐ³Ð¾ Ñ‡Ð°ÑÑ‚Ð¾Ñ‚Ð°.");
                    break;

                case "biome.family.rift_spine":
                    profile.atmosphereProfile = EnsureAtmosphereProfile("Atmos_RiftSpine.asset", new Color(0.12f, 0.08f, 0.18f, 1f), 0.028f, 0.62f, new Color(0.14f, 0.12f, 0.2f, 1f), 0.33f);
                    profile.faunaFamilyProfile = EnsureFaunaFamilyProfile("fauna.family.rift_stalkers", "Rift Stalkers", "cleft_hunters", "high", "rift_stalker", "route_control", "Ð Ð°Ð·Ð»Ð¾Ð¼Ñ‹ Ñ€Ð°Ð±Ð¾Ñ‚Ð°ÑŽÑ‚ ÐºÐ°Ðº ÐµÑÑ‚ÐµÑÑ‚Ð²ÐµÐ½Ð½Ñ‹Ðµ Ð»Ð¸Ð½Ð¸Ð¸ Ð·Ð°ÑÐ°Ð´Ñ‹ Ð¸ ÐºÐ¾Ð½Ñ‚Ñ€Ð¾Ð»Ñ Ð¿ÑƒÑ‚Ð¸.", "Ð˜Ð³Ñ€Ð¾Ðº Ñ‡ÑƒÐ²ÑÑ‚Ð²ÑƒÐµÑ‚, Ñ‡Ñ‚Ð¾ Ð·Ð° Ð½Ð¸Ð¼ ÑÐ»ÐµÐ´ÑÑ‚ Ð¸Ð· Ð³ÐµÐ¾Ð¼ÐµÑ‚Ñ€Ð¸Ð¸ Ð¼Ð¸Ñ€Ð°.");
                    break;

                case "biome.family.rift_void":
                default:
                    profile.atmosphereProfile = EnsureAtmosphereProfile("Atmos_RiftVoid.asset", new Color(0.04f, 0.04f, 0.08f, 1f), 0.04f, 0.4f, new Color(0.08f, 0.08f, 0.12f, 1f), 0.22f);
                    profile.faunaFamilyProfile = EnsureFaunaFamilyProfile("fauna.family.void_apex", "Void Apex", "almost_none", "extreme", "void_apex_presence", "silence_then_terror", "ÐŸÑƒÑÑ‚Ð¾Ñ‚Ð° Ð¸ Ñ€ÐµÐ´ÐºÐ°Ñ Ð¿Ð¾Ð·Ð´Ð½ÑÑ ÑƒÐ³Ñ€Ð¾Ð·Ð°. Ð¡Ñ‚Ñ€Ð°Ñ… Ð¸Ð´Ñ‘Ñ‚ Ð¾Ñ‚ Ð¾Ñ‚ÑÑƒÑ‚ÑÑ‚Ð²Ð¸Ñ Ð±ÐµÐ·Ð¾Ð¿Ð°ÑÐ½Ð¾Ð¹ Ñ€ÑƒÑ‚Ð¸Ð½Ñ‹.", "ÐœÐ¸Ð½Ð¸Ð¼ÑƒÐ¼ Ð¶Ð¸Ð·Ð½Ð¸, Ð¼Ð°ÐºÑÐ¸Ð¼ÑƒÐ¼ Ð¾Ñ‰ÑƒÑ‰ÐµÐ½Ð¸Ñ Ð³Ð»ÑƒÐ±Ð¸Ð½Ñ‹.");
                    break;
            }
        }

        private static string SanitizeId(string value)
        {
            return value.Replace('.', '_').Replace('/', '_').Replace('\\', '_').Replace(':', '_');
        }

        private static WorldPrefabFamilyProfile LoadWorldFamilyProfile(string assetFileName)
        {
            return AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>($"{WorldFamilyProfileFolder}/{assetFileName}");
        }

        private static WorldZonePlanProfile LoadZonePlanProfile(string assetFileName)
        {
            return AssetDatabase.LoadAssetAtPath<WorldZonePlanProfile>($"{WorldZonePlanFolder}/{assetFileName}");
        }

        private static ItemData LoadItemData(string assetPath)
        {
            return AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
        }

        private static AtmosphereProfile EnsureAtmosphereProfile(
            string fileName,
            Color fogColor,
            float fogDensity,
            float skyExposure,
            Color ambientColor,
            float sunIntensity)
        {
            string assetPath = $"{BiomeAtmosphereProfileFolder}/{fileName}";
            AtmosphereProfile profile = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(assetPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<AtmosphereProfile>();
                AssetDatabase.CreateAsset(profile, assetPath);
            }

            profile.fogColor = fogColor;
            profile.fogDensity = fogDensity;
            profile.skyExposure = skyExposure;
            profile.ambientColor = ambientColor;
            profile.sunIntensity = sunIntensity;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static HectonFaunaFamilyProfile EnsureFaunaFamilyProfile(
            string familyId,
            string familyLabel,
            string ambientLife,
            string threatStyle,
            string signaturePredator,
            string encounterRhythm,
            string gameplaySummary,
            string ambienceSummary)
        {
            string assetPath = $"{BiomeFaunaFamilyProfileFolder}/FaunaFamilyProfile_{SanitizeId(familyId)}.asset";
            HectonFaunaFamilyProfile profile = AssetDatabase.LoadAssetAtPath<HectonFaunaFamilyProfile>(assetPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<HectonFaunaFamilyProfile>();
                AssetDatabase.CreateAsset(profile, assetPath);
            }

            profile.familyId = familyId;
            profile.familyLabel = familyLabel;
            profile.ambientLife = ambientLife;
            profile.threatStyle = threatStyle;
            profile.signaturePredator = signaturePredator;
            profile.encounterRhythm = encounterRhythm;
            profile.gameplaySummary = gameplaySummary;
            profile.ambienceSummary = ambienceSummary;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void ApplyFamilyLoadout(HectonBiomeFamilyProfile profile, string familyId)
        {
            switch (familyId)
            {
                case "biome.family.littoral_karst":
                case "biome.family.sediment_drift":
                case "biome.family.granite_escarpment":
                case "biome.family.fossil_reef":
                case "biome.family.tectonic_spine":
                case "biome.family.crystal_growth":
                    profile.recommendedLoadoutPreset = LoadToolLoadoutPreset("Preset_Loadout_Exploration.asset");
                    break;

                case "biome.family.abyssal_silt":
                case "biome.family.chemosynthetic_brine":
                    profile.recommendedLoadoutPreset = LoadToolLoadoutPreset("Preset_Loadout_FieldRecovery.asset");
                    break;

                case "biome.family.volcanic_glass":
                case "biome.family.rift_spine":
                    profile.recommendedLoadoutPreset = LoadToolLoadoutPreset("Preset_Loadout_Construction.asset");
                    break;

                case "biome.family.volcanic_hadal":
                case "biome.family.metallic_hadal":
                case "biome.family.rift_void":
                default:
                    profile.recommendedLoadoutPreset = LoadToolLoadoutPreset("Preset_Loadout_Defense.asset");
                    break;
            }
        }

        private static ToolLoadoutPreset LoadToolLoadoutPreset(string assetFileName)
        {
            return AssetDatabase.LoadAssetAtPath<ToolLoadoutPreset>($"Assets/_Project/Data/Tools/Presets/{assetFileName}");
        }

        private static void ApplyFamilyPlay(HectonBiomeFamilyProfile profile, string familyId)
        {
            switch (familyId)
            {
                case "biome.family.littoral_karst":
                    profile.playProfile = EnsurePlayProfile("Play_LittoralKarst.asset", "biome.play.littoral_karst", "Littoral Karst Play", "Starter resources, readable routes, and fast landmark memory.", "Bright and readable space for short dives, first gathering, and route learning.", "Short dive loops from one strong landmark to another.", 5, 5, 5, 4, 2, 1, 1, 1, "The main risk is overconfidence, not creatures.");
                    break;
                case "biome.family.sediment_drift":
                    profile.playProfile = EnsurePlayProfile("Play_SedimentDrift.asset", "biome.play.sediment_drift", "Sediment Drift Play", "Calm gathering and route linking without harsh punishment.", "A soft utility biome for searching, collecting, and stitching paths together.", "Wide loops that return to a familiar terrain shape.", 4, 3, 4, 4, 2, 1, 2, 2, "The main risk is losing heading in repetitive terrain.");
                    break;
                case "biome.family.granite_escarpment":
                    profile.playProfile = EnsurePlayProfile("Play_GraniteEscarpment.asset", "biome.play.granite_escarpment", "Granite Escarpment Play", "Vertical orientation, technical gathering, and a strong edge-of-world feel.", "A wall-travel biome with crack pockets and careful side movement.", "Move from pocket to pocket along one giant landmark.", 4, 5, 3, 3, 2, 2, 3, 3, "The danger is a bad drop and an awkward climb back.");
                    break;
                case "biome.family.fossil_reef":
                    profile.playProfile = EnsurePlayProfile("Play_FossilReef.asset", "biome.play.fossil_reef", "Fossil Reef Play", "Dense life, organics, and beautiful but risky reef navigation.", "Good reward biome with constant chance to disturb something dangerous.", "Slow inspection of reef pockets with repeated greed-versus-safety choices.", 3, 4, 3, 4, 3, 3, 3, 3, "The reef pays well but punishes deep commitment into porous clusters.");
                    break;
                case "biome.family.tectonic_spine":
                    profile.playProfile = EnsurePlayProfile("Play_TectonicSpine.asset", "biome.play.tectonic_spine", "Tectonic Spine Play", "Readable travel along a spine, with depth pressure and hard geometry.", "A transit biome where the player reads the world by huge stone lines.", "Long moves between strong forms with only rare pauses in cover.", 3, 4, 2, 3, 3, 3, 4, 4, "The key mistake here is overcommitting along the edge without a return plan.");
                    break;
                case "biome.family.crystal_growth":
                    profile.playProfile = EnsurePlayProfile("Play_CrystalGrowth.asset", "biome.play.crystal_growth", "Crystal Growth Play", "You come here for beauty and rare material, but only with care.", "Very readable visually, but it steals time if explored lazily.", "Stop, read, collect, and move again in short careful bursts.", 3, 5, 2, 3, 2, 4, 2, 3, "The main trap is moving faster than the space actually allows.");
                    break;
                case "biome.family.abyssal_silt":
                    profile.playProfile = EnsurePlayProfile("Play_AbyssalSilt.asset", "biome.play.abyssal_silt", "Abyssal Silt Play", "Silence, depth, and a small number of valuable rewards.", "A patience biome with weak orientation, weak safety, and important deep-sea gathering.", "Long quiet stretches interrupted by one awkward contact.", 2, 2, 1, 2, 3, 2, 3, 4, "The biggest enemy here is trip length and weak orientation.");
                    break;
                case "biome.family.volcanic_glass":
                    profile.playProfile = EnsurePlayProfile("Play_VolcanicGlass.asset", "biome.play.volcanic_glass", "Volcanic Glass Play", "Heat, risk, and strong materials for technical growth.", "An aggressive biome where thermal danger must be read before commitment.", "Sharp pushes toward one valuable point, then retreat.", 2, 4, 1, 2, 2, 4, 4, 5, "Pressure ramps up faster than one quiet pocket suggests.");
                    break;
                case "biome.family.volcanic_hadal":
                    profile.playProfile = EnsurePlayProfile("Play_VolcanicHadal.asset", "biome.play.volcanic_hadal", "Volcanic Hadal Play", "A late-game push for very valuable rewards in a biome that does not forgive mistakes.", "This is not a casual roaming space. It asks for planning and survival margin.", "Short breakthrough, high tension, retreat, then re-evaluate.", 1, 5, 1, 2, 1, 5, 5, 5, "The key mistake is treating it like a normal mid-depth biome.");
                    break;
                case "biome.family.chemosynthetic_brine":
                    profile.playProfile = EnsurePlayProfile("Play_ChemosyntheticBrine.asset", "biome.play.chemosynthetic_brine", "Chemosynthetic Brine Play", "Pockets of valuable chemistry and dangerous local hotspots.", "The biome alternates empty travel with very valuable active pockets.", "Find pocket, do a short risky harvest, move on.", 2, 4, 2, 3, 3, 4, 4, 4, "Danger here is not constant; it is concentrated in hotspots.");
                    break;
                case "biome.family.metallic_hadal":
                    profile.playProfile = EnsurePlayProfile("Play_MetallicHadal.asset", "biome.play.metallic_hadal", "Metallic Hadal Play", "Rare heavy materials that should feel earned.", "A hard late biome where every contact is expensive but return value is high.", "Long approach, exact target, fast extraction.", 2, 4, 1, 3, 2, 5, 4, 5, "If you stay here without purpose, the biome starts winning by itself.");
                    break;
                case "biome.family.rift_spine":
                    profile.playProfile = EnsurePlayProfile("Play_RiftSpine.asset", "biome.play.rift_spine", "Rift Spine Play", "Route control through fractures, vertical travel, and geometry-led tension.", "Strong geometry guides the player but also traps them into bad choices.", "Anxious travel along a fracture where every stop matters.", 2, 5, 1, 2, 3, 4, 4, 5, "A bad line choice here quickly turns into a bad pocket.");
                    break;
                case "biome.family.rift_void":
                default:
                    profile.playProfile = EnsurePlayProfile("Play_RiftVoid.asset", "biome.play.rift_void", "Rift Void Play", "Void and final depth. You go here for major progress, not routine.", "A final-pressure space where reward or progression should feel like an event.", "Very little routine, full concentration, expensive decisions.", 1, 5, 1, 1, 1, 5, 5, 5, "The player should come here with a clear purpose, not for convenience.");
                    break;
            }
        }


        private static HectonBiomePlayProfile EnsurePlayProfile(
            string fileName,
            string profileId,
            string profileLabel,
            string whyPlayerComesHere,
            string playerPromise,
            string traversalRhythm,
            int routeClarity,
            int landmarkStrength,
            int safePocketFrequency,
            int commonResourceDensity,
            int salvageValue,
            int rareRewardPull,
            int encounterPressure,
            int hazardPressure,
            string cautionSummary)
        {
            string assetPath = $"{BiomePlayProfileFolder}/{fileName}";
            HectonBiomePlayProfile profile = AssetDatabase.LoadAssetAtPath<HectonBiomePlayProfile>(assetPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<HectonBiomePlayProfile>();
                AssetDatabase.CreateAsset(profile, assetPath);
            }

            profile.profileId = profileId;
            profile.profileLabel = profileLabel;
            profile.whyPlayerComesHere = whyPlayerComesHere;
            profile.playerPromise = playerPromise;
            profile.traversalRhythm = traversalRhythm;
            profile.routeClarity = routeClarity;
            profile.landmarkStrength = landmarkStrength;
            profile.safePocketFrequency = safePocketFrequency;
            profile.commonResourceDensity = commonResourceDensity;
            profile.salvageValue = salvageValue;
            profile.rareRewardPull = rareRewardPull;
            profile.encounterPressure = encounterPressure;
            profile.hazardPressure = hazardPressure;
            profile.expeditionCommitment = Mathf.Clamp(Mathf.Max(rareRewardPull, hazardPressure), 1, 5);
            profile.cautionSummary = cautionSummary;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void GetTierDepthRange(int tier, out float minDepth, out float maxDepth)
        {
            switch (tier)
            {
                case 1: minDepth = -200f; maxDepth = 0f; return;
                case 2: minDepth = 0f; maxDepth = 300f; return;
                case 3: minDepth = 300f; maxDepth = 600f; return;
                case 4: minDepth = 600f; maxDepth = 1000f; return;
                case 5: minDepth = 1000f; maxDepth = 1500f; return;
                case 6: minDepth = 1500f; maxDepth = 2000f; return;
                case 7: minDepth = 2000f; maxDepth = 2500f; return;
                case 8: minDepth = 2500f; maxDepth = 3000f; return;
                case 9: minDepth = 3000f; maxDepth = 3500f; return;
                case 27: minDepth = 14000f; maxDepth = 15000f; return;
            }

            float step = 525f;
            minDepth = 3500f + ((tier - 10) * step);
            maxDepth = minDepth + step;
        }

        private readonly struct BiomeSeed
        {
            public readonly string name;
            public readonly float minDepth;
            public readonly float maxDepth;
            public readonly string description;
            public readonly bool isPlaceholder;
            public readonly string suggestedZoneFamily;
            public readonly string progressionRole;

            public BiomeSeed(string name, float minDepth, float maxDepth, string description, bool isPlaceholder, string suggestedZoneFamily, string progressionRole)
            {
                this.name = name;
                this.minDepth = minDepth;
                this.maxDepth = maxDepth;
                this.description = description;
                this.isPlaceholder = isPlaceholder;
                this.suggestedZoneFamily = suggestedZoneFamily;
                this.progressionRole = progressionRole;
            }
        }

        private static readonly Dictionary<int, BiomeSeed> ExplicitSeeds = new Dictionary<int, BiomeSeed>
        {
            { 1, new BiomeSeed("Archipelago Needles", -200f, 0f, "Razor-sharp limestone spires rising from the surf. Wind-eroded bird-nesting pillars with salt-crusted peaks.", false, "navigation.silhouette.far", "starter_surface") },
            { 2, new BiomeSeed("Mesa Plateaus", -200f, 0f, "Flat-topped dry platforms dotted with giant blue holes full of stagnant nutrient-rich water.", false, "resources.landmarks.far", "starter_surface") },
            { 3, new BiomeSeed("The Granite Spine", -200f, 0f, "A continuous obsidian-smooth vertical wall marking the end of the sea.", false, "progression.skyline.far", "starter_surface") },
            { 4, new BiomeSeed("The Silt Tongue", -200f, 0f, "Undulating white sand dunes rippling under long surface swells.", false, "resources.clutter.mid", "starter_surface") },
            { 5, new BiomeSeed("Sea-Stack Forest", 0f, 300f, "A forest of isolated circular stone pillars over coral dust and shell fields.", false, "resources.landmarks.far", "starter_surface") },
            { 6, new BiomeSeed("White Alabaster Pools", 0f, 300f, "Terraced basins like frozen waterfalls, filled with milky mineral pools.", false, "resources.pickups.near", "starter_surface") },
            { 7, new BiomeSeed("The Tectonic Chute", 0f, 300f, "A narrow straight canyon cut into granite, funneling the first cold currents.", false, "navigation.route.mid", "starter_surface") },
            { 8, new BiomeSeed("Sand-Fan Deltas", 0f, 300f, "Fractal tidal sediment patterns that change after every storm.", false, "resources.clutter.mid", "starter_surface") },
            { 9, new BiomeSeed("Basalt Steps", 300f, 600f, "Gargantuan hexagonal pillars with deep biolum cracks.", false, "progression.route.mid", "starter_surface") },
            { 10, new BiomeSeed("Meander-Basins", 300f, 600f, "Serpentine channels full of ash drifts and the skeletons of surface life.", false, "resources.clutter.mid", "starter_surface") },
            { 11, new BiomeSeed("Sharp Finned Ridges", 300f, 600f, "Parallel tectonic slabs like the spine of a colossal beast.", false, "progression.skyline.far", "starter_surface") },
            { 12, new BiomeSeed("Coral-Porous Walls", 300f, 600f, "Ancient fossilized reef walls honeycombed with holes and micro-caverns.", false, "resources.pickups.near", "starter_surface") },
            { 13, new BiomeSeed("Silt Dunes", 600f, 1000f, "High-altitude heavy-grey dunes with razor crests.", false, "resources.clutter.mid", "starter_surface") },
            { 14, new BiomeSeed("Pothole Fields", 600f, 1000f, "A meteor-like field of circular seabed depressions.", false, "resources.pickups.near", "starter_surface") },
            { 15, new BiomeSeed("The Slab Wall", 600f, 1000f, "Tilted granite megablocks leaning into sunless overhangs.", false, "progression.skyline.far", "starter_surface") },
            { 16, new BiomeSeed("Crystalline Ridges", 600f, 1000f, "Geometric mineral peaks covered in translucent needles.", false, "resources.landmarks.far", "starter_surface") },
            { 17, new BiomeSeed("The Great Staircase", 1000f, 1500f, "Massive descending shelves leading the player into the abyss.", false, "progression.route.mid", "slope_descent") },
            { 18, new BiomeSeed("Dendritic Erosion Gully", 1000f, 1500f, "Tree-like branching canyons cut by turbidity currents.", false, "navigation.route.mid", "slope_descent") },
            { 19, new BiomeSeed("The Vertical Shadow Wall", 1000f, 1500f, "A 70-degree incline of smooth black stone.", false, "progression.skyline.far", "slope_descent") },
            { 20, new BiomeSeed("Table-Land Benches", 1000f, 1500f, "Wide plateaus ending in brutal vertical drops.", false, "navigation.silhouette.far", "slope_descent") },
            { 21, new BiomeSeed("Labyrinth Trenches", 1500f, 2000f, "Intersecting deep V-shaped canyons forming a predator maze.", false, "combat.readability.mid", "slope_descent") },
            { 22, new BiomeSeed("Bubble Mound Fields", 1500f, 2000f, "Bulbous smooth hills like giant bubbles frozen in time.", false, "resources.clutter.mid", "slope_descent") },
            { 23, new BiomeSeed("The Shattered Cliff-Base", 1500f, 2000f, "A debris valley of square megablocks fallen from above.", false, "construction.frames.mid", "slope_descent") },
            { 24, new BiomeSeed("The Silt Cascades", 1500f, 2000f, "Frozen rivers of sediment flowing down into darkness.", false, "navigation.route.mid", "slope_descent") },
            { 25, new BiomeSeed("Fracture Slabs", 2000f, 2500f, "Offset tectonic plates creating a jagged neon skyline.", false, "combat.silhouette.far", "slope_descent") },
            { 26, new BiomeSeed("The Eye of the Abyss", 2000f, 2500f, "A perfectly circular 1km depression anchoring the south.", false, "progression.route.landmark", "slope_descent") },
            { 27, new BiomeSeed("The Wall-Fissures", 2000f, 2500f, "Narrow vertical cracks emitting faint thermal light.", false, "hazard.probe", "slope_descent") },
            { 28, new BiomeSeed("Meandering Silt-Rivers", 2000f, 2500f, "Liquid-like sediment channels wandering through the plains.", false, "navigation.route.mid", "slope_descent") },
            { 29, new BiomeSeed("Basalt Prisms", 2500f, 3000f, "Horizontal pipe-organ formations buried in mud.", false, "resources.landmarks.far", "slope_descent") },
            { 30, new BiomeSeed("Soft Domes", 2500f, 3000f, "Perfect hemispherical mounds under marine snow.", false, "resources.clutter.mid", "slope_descent") },
            { 31, new BiomeSeed("Spine-Teeth", 2500f, 3000f, "Predatory ridge-fins like giant teeth in the dark.", false, "progression.skyline.far", "slope_descent") },
            { 32, new BiomeSeed("Dune-Drains", 2500f, 3000f, "Funnel sinkholes centered in every silt wave.", false, "hazard.probe", "slope_descent") },
            { 33, new BiomeSeed("Silt Catacombs", 3000f, 3500f, "Roofless tunnels left by extinct mega-worms.", false, "progression.route.mid", "slope_descent") },
            { 34, new BiomeSeed("Fossil Gallows", 3000f, 3500f, "Calcified branching bone-coral structures.", false, "resources.landmarks.far", "slope_descent") },
            { 35, new BiomeSeed("The Granite Maw", 3000f, 3500f, "Gaping canyons like a mouth opening in the dark.", false, "progression.route.landmark", "slope_descent") },
            { 36, new BiomeSeed("The Flat Margin", 3000f, 3500f, "An endless plain of grey monotonous silt.", false, "combat.silhouette.far", "slope_descent") },
            { 37, new BiomeSeed("Methane Mounds", 3500f, 4025f, "Spongy cones venting constant methane bubbles.", false, "hazard.probe", "abyss_entry") },
            { 38, new BiomeSeed("The Fluid Seam", 3500f, 4025f, "Stone ridges frozen while seemingly flowing.", false, "progression.route.mid", "abyss_entry") },
            { 39, new BiomeSeed("Block-City", 3500f, 4025f, "Monolithic cube geometry like ancient ruins.", false, "progression.route.landmark", "abyss_entry") },
            { 40, new BiomeSeed("Silt-Void", 3500f, 4025f, "Perfectly flat and dark ultimate silence.", false, "combat.silhouette.far", "abyss_entry") },
            { 41, new BiomeSeed("Cinder Fields", 4025f, 4550f, "Fractal ash-noise fields from deep volcanism.", false, "resources.clutter.mid", "abyss_entry") },
            { 42, new BiomeSeed("Obsidian Flows", 4025f, 4550f, "Glass-smooth black volcanic flows.", false, "progression.skyline.far", "abyss_entry") },
            { 43, new BiomeSeed("Tectonic Shards", 4025f, 4550f, "Knife-thin vertical shards jutting from the floor.", false, "combat.readability.mid", "abyss_entry") },
            { 44, new BiomeSeed("Fluid Hills", 4025f, 4550f, "Soft rolling hills like a liquid sea frozen mid-wave.", false, "navigation.route.mid", "abyss_entry") },
            { 57, new BiomeSeed("The Iron Plains", 6125f, 6650f, "Rust-colored metallic peaks over manganese nodules.", false, "resources.landmarks.far", "deep_pressure") },
            { 58, new BiomeSeed("Brine Rivers", 6125f, 6650f, "Hyper-saline underwater lakes in deep depressions.", false, "hazard.probe", "deep_pressure") },
            { 59, new BiomeSeed("The Black Spine", 6125f, 6650f, "Jagged black peaks humming with low-frequency pressure.", false, "progression.skyline.far", "deep_pressure") },
            { 60, new BiomeSeed("The Silt Shadows", 6125f, 6650f, "Flat dark plains that swallow almost all light.", false, "combat.silhouette.far", "deep_pressure") },
            { 77, new BiomeSeed("The Ash-Wastes", 8750f, 9275f, "Deep ash deposits that storm when disturbed.", false, "hazard.probe", "deep_pressure") },
            { 78, new BiomeSeed("Hydrothermal Spires", 8750f, 9275f, "300m black chimneys venting superheated water.", false, "power.generator.current_turbine", "deep_pressure") },
            { 79, new BiomeSeed("The Rift-Gates", 8750f, 9275f, "Vertical fissures that seem to descend into the core.", false, "progression.route.landmark", "deep_pressure") },
            { 80, new BiomeSeed("Pressure-Slabs", 8750f, 9275f, "Compressed mirror-flat basalt plates.", false, "construction.spine.far", "deep_pressure") },
            { 81, new BiomeSeed("Iron Shards", 9275f, 9800f, "Metallic peaks sharp enough to threaten hull plating.", false, "combat.targets.near", "final_hadal") },
            { 82, new BiomeSeed("Magma Pools", 9275f, 9800f, "Exposed lava flows casting a permanent red glow.", false, "hazard.probe", "final_hadal") },
            { 83, new BiomeSeed("The Shattered Spine", 9275f, 9800f, "The mountainâ€™s deepest pulverized gravel fields.", false, "progression.setpieces.near", "final_hadal") },
            { 84, new BiomeSeed("The Glass Plains", 9275f, 9800f, "Quenched obsidian sheets at trench depth.", false, "progression.skyline.far", "final_hadal") },
            { 97, new BiomeSeed("The Shivering Slabs", 11900f, 12425f, "Tectonic plates vibrating with planetary frequency.", false, "hazard.probe", "final_hadal") },
            { 98, new BiomeSeed("The Pillow-Lava Hives", 11900f, 12425f, "Bulbous clusters from ancient magmatic eruptions.", false, "resources.landmarks.far", "final_hadal") },
            { 99, new BiomeSeed("The Rift-Maw", 11900f, 12425f, "A jagged tear one kilometre deep in the crust.", false, "progression.route.landmark", "final_hadal") },
            { 100, new BiomeSeed("The Basalt Flux", 11900f, 12425f, "Warped basalt waves resembling eternal water.", false, "progression.skyline.far", "final_hadal") },
            { 105, new BiomeSeed("The Iron Peak", 14000f, 15000f, "The highest point of the deepest tier, a single rusted needle.", false, "progression.route.landmark", "final_hadal") },
            { 106, new BiomeSeed("The Lava Seam", 14000f, 15000f, "A continuous river of fire flowing through the abyss.", false, "hazard.probe", "final_hadal") },
            { 107, new BiomeSeed("The Heart of the Rift", 14000f, 15000f, "The absolute deepest point: a silent vertical shaft.", false, "progression.setpieces.near", "final_hadal") },
            { 108, new BiomeSeed("The Static Matrix", 14000f, 15000f, "Stone compressed until it resembles a digital grid.", false, "progression.skyline.far", "final_hadal") },
        };
    }
}
