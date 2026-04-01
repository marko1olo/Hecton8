using System.Collections.Generic;
using Hecton8.Environment;
using Hecton8.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.AI.Editor
{
    public static class FaunaBiomeBootstrapAuthoring
    {
        private const string BiomeCatalogPath = "Assets/_Project/Data/Biomes/BiomeMatrixCatalog.asset";
        private const string RootFolder = "Assets/_Project/Data/AI/FaunaBiomes";
        private const string WorldChunkStreamingProfilePath = "Assets/_Project/Data/World/Streaming/WorldChunkStreamingProfile.asset";

        [MenuItem("Hecton/Authoring/Build Fauna Biome Datasets", priority = 183)]
        public static void BuildFaunaBiomeDatasets()
        {
            CreatureArchetypeAuthoring.BuildCreatureArchetypes();

            HectonBiomeMatrixCatalog catalog = AssetDatabase.LoadAssetAtPath<HectonBiomeMatrixCatalog>(BiomeCatalogPath);
            if (catalog == null || catalog.Profiles == null || catalog.Profiles.Length == 0)
            {
                Debug.LogError("[FaunaBiomeBootstrap] Missing BiomeMatrixCatalog. Rebuild 108 Biome Matrix first.");
                return;
            }

            EnsureFolder("Assets/_Project/Data");
            EnsureFolder("Assets/_Project/Data/AI");
            EnsureFolder(RootFolder);

            List<CreatureArchetypeData> archetypes = LoadArchetypes();
            WorldChunkStreamingProfile chunkProfile =
                AssetDatabase.LoadAssetAtPath<WorldChunkStreamingProfile>(WorldChunkStreamingProfilePath);
            List<FaunaBiomeData> builtDatasets = new List<FaunaBiomeData>(catalog.Profiles.Length);

            for (int i = 0; i < catalog.Profiles.Length; i++)
            {
                HectonBiomeMatrixProfile profile = catalog.Profiles[i];
                if (profile == null)
                    continue;

                string assetPath = $"{RootFolder}/FaunaBiome_{profile.matrixIndex:000}_{ToAssetToken(profile.biomeName)}.asset";
                FaunaBiomeData dataset = AssetDatabase.LoadAssetAtPath<FaunaBiomeData>(assetPath);
                if (dataset == null)
                {
                    dataset = ScriptableObject.CreateInstance<FaunaBiomeData>();
                    AssetDatabase.CreateAsset(dataset, assetPath);
                }

                ConfigureDataset(dataset, profile, archetypes, chunkProfile);
                EditorUtility.SetDirty(dataset);
                builtDatasets.Add(dataset);
            }

            AssignDatasetsToSceneDirector(builtDatasets, chunkProfile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FaunaBiomeBootstrap] Rebuilt {builtDatasets.Count} fauna biome datasets.");
        }

        private static void ConfigureDataset(
            FaunaBiomeData dataset,
            HectonBiomeMatrixProfile profile,
            List<CreatureArchetypeData> archetypes,
            WorldChunkStreamingProfile chunkProfile)
        {
            dataset.biomeIndex = profile.matrixIndex;
            dataset.biomeName = profile.biomeName;
            dataset.biomeMaxCreatures = ResolveBiomeMaxCreatures(profile);
            dataset.spawnHeightAboveBottom = ResolveSpawnHeightMin(profile);
            dataset.spawnHeightMax = ResolveSpawnHeightMax(profile);
            dataset.possibleCreatures = BuildEntries(profile, archetypes);
            ConfigureLargeThreatZone(dataset, profile, chunkProfile);
        }

        private static void ConfigureLargeThreatZone(
            FaunaBiomeData dataset,
            HectonBiomeMatrixProfile profile,
            WorldChunkStreamingProfile chunkProfile)
        {
            dataset.useLargeThreatMacroZone = false;
            dataset.largeThreatZoneLabel = string.Empty;
            dataset.largeThreatArchetype = null;
            dataset.largeThreatEncounterType = LeviathanEncounterType.PresenceCircle;
            dataset.preferHeavyHunterInsteadOfLeviathan = false;

            CreatureArchetypeData largeThreat = ResolveLargeThreatArchetype(profile, dataset.possibleCreatures, out bool preferHeavyHunter);
            if (largeThreat == null)
                return;

            float baseRadius = chunkProfile != null ? chunkProfile.macroZoneSizeMeters : 768f;

            dataset.useLargeThreatMacroZone = true;
            dataset.largeThreatArchetype = largeThreat;
            dataset.preferHeavyHunterInsteadOfLeviathan = preferHeavyHunter;
            dataset.largeThreatEncounterType = ResolveLargeThreatEncounterType(profile, largeThreat, preferHeavyHunter);
            dataset.largeThreatZoneRadius = ResolveLargeThreatZoneRadius(profile, baseRadius, preferHeavyHunter);
            dataset.largeThreatZoneLabel = ResolveLargeThreatZoneLabel(profile, largeThreat, preferHeavyHunter);
        }

        private static List<FaunaEntry> BuildEntries(HectonBiomeMatrixProfile profile, List<CreatureArchetypeData> archetypes)
        {
            string faunaFamilyId = profile.familyProfile != null && profile.familyProfile.faunaFamilyProfile != null
                ? profile.familyProfile.faunaFamilyProfile.familyId
                : string.Empty;

            List<CreatureArchetypeData> ambient = RankArchetypes(archetypes, CreatureRoleType.Ambient, profile, faunaFamilyId);
            List<CreatureArchetypeData> territorial = RankArchetypes(archetypes, CreatureRoleType.Territorial, profile, faunaFamilyId);
            List<CreatureArchetypeData> hunters = RankArchetypes(archetypes, CreatureRoleType.Hunter, profile, faunaFamilyId);
            List<CreatureArchetypeData> leviathans = RankArchetypes(archetypes, CreatureRoleType.Leviathan, profile, faunaFamilyId);

            var result = new List<FaunaEntry>(6);

            for (int i = 0; i < ResolveAmbientTarget(profile); i++)
                AddEntry(result, ambient, i);

            if (ShouldAddTerritorial(profile))
                AddEntry(result, territorial, 0);

            for (int i = 0; i < ResolveHunterTarget(profile); i++)
                AddEntry(result, hunters, i);

            if (ShouldIncludeLeviathan(profile))
                AddEntry(result, leviathans, 0);

            EnsurePassivePresence(result, profile, ambient, territorial);
            EnsureThreatPresence(result, profile, territorial, hunters, leviathans);
            return result;
        }

        private static int ResolveBiomeMaxCreatures(HectonBiomeMatrixProfile profile)
        {
            if (IsMassiveSurfaceSetpiece(profile))
                return profile.faunaMood == WorldProceduralFaunaMood.Lively ? 15 : 13;
            if (IsCalmFamily(profile.familyId))
                return profile.faunaMood == WorldProceduralFaunaMood.Lively ? 14 : 12;
            if (IsResourceFriendlyFamily(profile.familyId))
                return profile.faunaMood == WorldProceduralFaunaMood.Lively ? 11 : 10;
            if (IsServiceHeavyFamily(profile.familyId))
                return 7;
            if (IsRiftFamily(profile.familyId))
                return 6;
            if (IsLateSparseFamily(profile.familyId))
                return 5;
            return 8;
        }

        private static float ResolveSpawnHeightMin(HectonBiomeMatrixProfile profile)
        {
            if (IsCalmFamily(profile.familyId))
                return 1.5f;
            if (IsServiceHeavyFamily(profile.familyId))
                return 1f;
            return 2f;
        }

        private static float ResolveSpawnHeightMax(HectonBiomeMatrixProfile profile)
        {
            if (IsCalmFamily(profile.familyId))
                return 18f;
            if (IsServiceHeavyFamily(profile.familyId))
                return 10f;
            if (IsLateSparseFamily(profile.familyId))
                return 8f;
            return 12f;
        }

        private static int ResolveAmbientTarget(HectonBiomeMatrixProfile profile)
        {
            if (IsMassiveSurfaceSetpiece(profile))
                return profile.faunaMood == WorldProceduralFaunaMood.Lively ? 4 : 3;
            if (IsCalmFamily(profile.familyId))
                return 3;
            if (IsResourceFriendlyFamily(profile.familyId) || profile.faunaMood == WorldProceduralFaunaMood.Lively)
                return 2;
            if (profile.faunaMood == WorldProceduralFaunaMood.Calm && !IsServiceHeavyFamily(profile.familyId))
                return 2;
            return 1;
        }

        private static bool ShouldAddTerritorial(HectonBiomeMatrixProfile profile)
        {
            if (IsMassiveSurfaceSetpiece(profile))
                return true;
            if (profile.primaryClusterFocus == WorldProceduralClusterFocus.BiologicalNest)
                return true;
            if (profile.primaryStructureFocus == WorldProceduralStructureFocus.NaturalLandmark)
                return true;
            if (IsCalmFamily(profile.familyId))
                return true;
            return profile.survivalPressure >= 4 && !IsLateSparseFamily(profile.familyId);
        }

        private static int ResolveHunterTarget(HectonBiomeMatrixProfile profile)
        {
            if (IsHeavyHunterSetpiece(profile))
                return 2;
            if (IsMassiveSurfaceSetpiece(profile))
                return profile.survivalPressure >= 4 ? 2 : 1;
            if (IsRiftFamily(profile.familyId) || IsServiceHeavyFamily(profile.familyId))
                return 2;
            if (IsCalmFamily(profile.familyId))
                return profile.survivalPressure >= 4 ? 1 : 0;
            return profile.survivalPressure >= 4 ? 2 : 1;
        }

        private static bool ShouldIncludeLeviathan(HectonBiomeMatrixProfile profile)
        {
            if (profile == null)
                return false;

            if (IsHeavyHunterSetpiece(profile))
                return false;

            if (IsSurfaceLeviathanCandidate(profile))
                return true;

            bool lateDepth = profile.maxDepthMeters >= 8750f || profile.depthTier >= 18;
            if (!lateDepth)
                return false;

            if (IsGenericReserveBiomeName(profile.biomeName))
                return false;

            bool heavyFamily = IsRiftFamily(profile.familyId) ||
                               string.Equals(profile.familyId, "biome.family.metallic_hadal", System.StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(profile.familyId, "biome.family.abyssal_silt", System.StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(profile.familyId, "biome.family.volcanic_hadal", System.StringComparison.OrdinalIgnoreCase);
            if (!heavyFamily)
                return false;

            bool lateRole = ContainsToken(profile.progressionRole, "deep_pressure", "final_hadal");
            bool setpieceRoute = ContainsToken(profile.suggestedZoneFamily, "progression.route.landmark", "progression.setpieces.near", "hazard.probe");
            bool extremeDanger = profile.survivalPressure >= 5;
            return lateRole || setpieceRoute || extremeDanger;
        }

        private static void EnsurePassivePresence(
            List<FaunaEntry> result,
            HectonBiomeMatrixProfile profile,
            List<CreatureArchetypeData> ambient,
            List<CreatureArchetypeData> territorial)
        {
            int ambientCount = CountRole(result, CreatureRoleType.Ambient);
            int targetAmbientCount = ResolveAmbientTarget(profile);

            if (ambientCount == 0)
            {
                if (!AddEntry(result, ambient, 0))
                    AddEntry(result, territorial, 0);
                ambientCount = CountRole(result, CreatureRoleType.Ambient);
            }

            if (targetAmbientCount >= 2 && ambientCount < 2)
                AddEntry(result, ambient, 1);

            if (targetAmbientCount >= 3 && CountRole(result, CreatureRoleType.Ambient) < 3)
                AddEntry(result, ambient, 2);
        }

        private static void EnsureThreatPresence(
            List<FaunaEntry> result,
            HectonBiomeMatrixProfile profile,
            List<CreatureArchetypeData> territorial,
            List<CreatureArchetypeData> hunters,
            List<CreatureArchetypeData> leviathans)
        {
            if (ContainsAnyThreat(result))
                return;

            if (IsCalmFamily(profile.familyId))
            {
                AddEntry(result, territorial, 0);
                return;
            }

            if (!AddEntry(result, hunters, 0))
                AddEntry(result, territorial, 0);

            if (!ContainsAnyThreat(result) && ShouldIncludeLeviathan(profile))
                AddEntry(result, leviathans, 0);
        }

        private static bool AddEntry(List<FaunaEntry> result, List<CreatureArchetypeData> rankedCandidates, int preferredIndex)
        {
            if (rankedCandidates == null || rankedCandidates.Count == 0)
                return false;

            for (int i = preferredIndex; i < rankedCandidates.Count; i++)
            {
                CreatureArchetypeData archetype = rankedCandidates[i];
                if (archetype == null || archetype.prefab == null)
                    continue;
                if (ContainsArchetype(result, archetype))
                    continue;

                result.Add(new FaunaEntry
                {
                    archetype = archetype,
                    prefab = archetype.prefab,
                    spawnWeight = archetype.spawnWeight,
                    maxAlive = archetype.maxAlivePerBiome
                });
                return true;
            }

            return false;
        }

        private static bool ContainsArchetype(List<FaunaEntry> result, CreatureArchetypeData archetype)
        {
            for (int i = 0; i < result.Count; i++)
            {
                if (result[i].archetype == archetype)
                    return true;
            }

            return false;
        }

        private static bool ContainsRole(List<FaunaEntry> result, CreatureRoleType roleType)
        {
            for (int i = 0; i < result.Count; i++)
            {
                CreatureArchetypeData archetype = result[i].archetype;
                if (archetype != null && archetype.roleType == roleType)
                    return true;
            }

            return false;
        }

        private static int CountRole(List<FaunaEntry> result, CreatureRoleType roleType)
        {
            int count = 0;
            for (int i = 0; i < result.Count; i++)
            {
                CreatureArchetypeData archetype = result[i].archetype;
                if (archetype != null && archetype.roleType == roleType)
                    count++;
            }

            return count;
        }

        private static bool ContainsAnyThreat(List<FaunaEntry> result)
        {
            for (int i = 0; i < result.Count; i++)
            {
                CreatureArchetypeData archetype = result[i].archetype;
                if (archetype == null)
                    continue;

                if (archetype.roleType == CreatureRoleType.Territorial ||
                    archetype.roleType == CreatureRoleType.Hunter ||
                    archetype.roleType == CreatureRoleType.Leviathan)
                    return true;
            }

            return false;
        }

        private static List<CreatureArchetypeData> RankArchetypes(
            List<CreatureArchetypeData> archetypes,
            CreatureRoleType roleType,
            HectonBiomeMatrixProfile profile,
            string faunaFamilyId)
        {
            var scored = new List<ArchetypeScore>(archetypes.Count);

            for (int i = 0; i < archetypes.Count; i++)
            {
                CreatureArchetypeData archetype = archetypes[i];
                if (archetype == null || archetype.roleType != roleType)
                    continue;

                int score = ScoreArchetype(archetype, profile, faunaFamilyId);
                if (score < -40)
                    continue;

                scored.Add(new ArchetypeScore(archetype, score));
            }

            scored.Sort((a, b) =>
            {
                int scoreCompare = b.score.CompareTo(a.score);
                if (scoreCompare != 0)
                    return scoreCompare;
                return string.CompareOrdinal(a.archetype.displayName, b.archetype.displayName);
            });

            var ranked = new List<CreatureArchetypeData>(scored.Count);
            for (int i = 0; i < scored.Count; i++)
                ranked.Add(scored[i].archetype);
            return ranked;
        }

        private static int ScoreArchetype(CreatureArchetypeData archetype, HectonBiomeMatrixProfile profile, string faunaFamilyId)
        {
            int score = 0;

            if (Contains(archetype.recommendedBiomeFamilyIds, profile.familyId))
                score += 10;
            if (Contains(archetype.recommendedFaunaFamilyIds, faunaFamilyId))
                score += 8;

            switch (archetype.roleType)
            {
                case CreatureRoleType.Ambient:
                    if (profile.faunaMood == WorldProceduralFaunaMood.Calm)
                        score += 4;
                    else if (profile.faunaMood == WorldProceduralFaunaMood.Lively)
                        score += 6;
                    else if (profile.faunaMood == WorldProceduralFaunaMood.Hostile)
                        score -= 3;
                    break;
                case CreatureRoleType.Territorial:
                    if (profile.primaryClusterFocus == WorldProceduralClusterFocus.BiologicalNest)
                        score += 5;
                    if (profile.primaryStructureFocus == WorldProceduralStructureFocus.NaturalLandmark ||
                        profile.primaryStructureFocus == WorldProceduralStructureFocus.CaveRead)
                        score += 3;
                    break;
                case CreatureRoleType.Hunter:
                    score += profile.survivalPressure;
                    if (profile.faunaMood == WorldProceduralFaunaMood.Hostile)
                        score += 6;
                    else if (profile.faunaMood == WorldProceduralFaunaMood.Mixed)
                        score += 4;
                    else if (profile.faunaMood == WorldProceduralFaunaMood.Calm)
                        score -= 4;
                    score += ScoreHeavyHunterSetpieceAffinity(archetype, profile);
                    break;
                case CreatureRoleType.Leviathan:
                    if (!ShouldIncludeLeviathan(profile))
                        return -100;
                    score += 12;
                    if (ContainsToken(profile.progressionRole, "deep_pressure", "final_hadal"))
                        score += 5;
                    score += ScoreLeviathanSetpieceAffinity(archetype, profile);
                    break;
            }

            score += ScoreBiomeNameAffinity(archetype, profile);
            return score;
        }

        private static int ScoreBiomeNameAffinity(CreatureArchetypeData archetype, HectonBiomeMatrixProfile profile)
        {
            string biomeName = profile.biomeName != null ? profile.biomeName.ToLowerInvariant() : string.Empty;
            string creatureId = archetype.creatureId != null ? archetype.creatureId.ToLowerInvariant() : string.Empty;

            if (biomeName.Contains("silt") && creatureId.Contains("silt"))
                return 4;
            if ((biomeName.Contains("brine") || biomeName.Contains("hydrothermal")) && creatureId.Contains("brine"))
                return 4;
            if ((biomeName.Contains("rift") || biomeName.Contains("void")) && (creatureId.Contains("rift") || creatureId.Contains("void")))
                return 4;
            if ((biomeName.Contains("lava") || biomeName.Contains("magma") || biomeName.Contains("thermal")) && (creatureId.Contains("heat") || creatureId.Contains("furnace")))
                return 4;
            if ((biomeName.Contains("arch") || biomeName.Contains("gallows") || biomeName.Contains("needle")) && creatureId.Contains("archway"))
                return 3;
            return 0;
        }

        private static int ScoreLeviathanSetpieceAffinity(CreatureArchetypeData archetype, HectonBiomeMatrixProfile profile)
        {
            string biomeName = profile.biomeName != null ? profile.biomeName.ToLowerInvariant() : string.Empty;
            string creatureId = archetype.creatureId != null ? archetype.creatureId.ToLowerInvariant() : string.Empty;

            switch (NormalizeBiomeName(profile.biomeName))
            {
                case "sea-stack forest":
                    if (creatureId.Contains("halo_crown"))
                        return 18;
                    if (creatureId.Contains("gate_warden"))
                        return 6;
                    break;
                case "the granite spine":
                    if (creatureId.Contains("gate_warden"))
                        return 18;
                    if (creatureId.Contains("halo_crown"))
                        return 5;
                    break;
                case "the ash-wastes":
                    if (creatureId.Contains("black_choir"))
                        return 18;
                    if (creatureId.Contains("void_ribbon"))
                        return 6;
                    break;
                case "the rift-gates":
                    if (creatureId.Contains("gate_warden"))
                        return 18;
                    if (creatureId.Contains("rift_lancer"))
                        return 8;
                    break;
                case "magma pools":
                    if (creatureId.Contains("furnace_maw"))
                        return 18;
                    break;
                case "the shattered spine":
                    if (creatureId.Contains("rift_lancer"))
                        return 18;
                    if (creatureId.Contains("gate_warden"))
                        return 4;
                    break;
                case "the glass plains":
                    if (creatureId.Contains("halo_crown"))
                        return 14;
                    if (creatureId.Contains("black_choir"))
                        return 10;
                    break;
                case "the shivering slabs":
                    if (creatureId.Contains("black_choir"))
                        return 16;
                    if (creatureId.Contains("halo_crown"))
                        return 6;
                    break;
                case "the pillow-lava hives":
                    if (creatureId.Contains("furnace_maw"))
                        return 18;
                    break;
                case "the rift-maw":
                    if (creatureId.Contains("rift_lancer"))
                        return 18;
                    if (creatureId.Contains("gate_warden"))
                        return 6;
                    break;
                case "the basalt flux":
                    if (creatureId.Contains("furnace_maw"))
                        return 16;
                    if (creatureId.Contains("gate_warden"))
                        return 4;
                    break;
                case "the lava seam":
                    if (creatureId.Contains("furnace_maw"))
                        return 18;
                    break;
                case "the heart of the rift":
                    if (creatureId.Contains("black_choir"))
                        return 16;
                    if (creatureId.Contains("rift_lancer"))
                        return 8;
                    break;
                case "the static matrix":
                    if (creatureId.Contains("void_ribbon"))
                        return 18;
                    if (creatureId.Contains("black_choir"))
                        return 6;
                    break;
            }

            if (ContainsToken(biomeName, "archipelago", "sea-stack", "coral porous"))
            {
                if (creatureId.Contains("halo_crown"))
                    return 10;
                if (creatureId.Contains("gate_warden"))
                    return 4;
            }

            if (ContainsToken(biomeName, "granite spine", "gates", "maw", "spine", "walls"))
            {
                if (creatureId.Contains("gate_warden"))
                    return 10;
                if (creatureId.Contains("rift_lancer"))
                    return 5;
            }

            if (ContainsToken(biomeName, "rift", "void"))
            {
                if (creatureId.Contains("rift_lancer"))
                    return 9;
                if (creatureId.Contains("void_ribbon"))
                    return 7;
            }

            if (ContainsToken(biomeName, "ash", "shiver", "glass", "static"))
            {
                if (creatureId.Contains("black_choir"))
                    return 9;
                if (creatureId.Contains("halo_crown"))
                    return 4;
            }

            if (ContainsToken(biomeName, "lava", "magma", "basalt", "pillow"))
            {
                if (creatureId.Contains("furnace_maw"))
                    return 10;
                if (creatureId.Contains("gate_warden"))
                    return 3;
            }

            return 0;
        }

        private static int ScoreHeavyHunterSetpieceAffinity(CreatureArchetypeData archetype, HectonBiomeMatrixProfile profile)
        {
            string creatureId = archetype.creatureId != null ? archetype.creatureId.ToLowerInvariant() : string.Empty;

            switch (NormalizeBiomeName(profile.biomeName))
            {
                case "pressure-slabs":
                    if (creatureId.Contains("armor_breaker"))
                        return 16;
                    if (creatureId.Contains("shadow_interceptor"))
                        return 8;
                    break;
                case "iron shards":
                    if (creatureId.Contains("armor_breaker"))
                        return 18;
                    break;
                case "the iron peak":
                    if (creatureId.Contains("armor_breaker"))
                        return 18;
                    if (creatureId.Contains("shadow_interceptor"))
                        return 5;
                    break;
            }

            return 0;
        }

        private static void AssignDatasetsToSceneDirector(List<FaunaBiomeData> datasets, WorldChunkStreamingProfile chunkProfile)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
                return;

            FaunaDirector director = Object.FindAnyObjectByType<FaunaDirector>(FindObjectsInactive.Include);
            if (director == null)
                return;

            SerializedObject directorSo = new SerializedObject(director);
            SerializedProperty array = directorSo.FindProperty("biomeDatasets");
            array.arraySize = datasets.Count;
            for (int i = 0; i < datasets.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = datasets[i];

            SerializedProperty chunkProfileProperty = directorSo.FindProperty("chunkStreamingProfile");
            if (chunkProfileProperty != null)
                chunkProfileProperty.objectReferenceValue = chunkProfile;

            directorSo.ApplyModifiedPropertiesWithoutUndo();
            director.SetChunkStreamingProfile(chunkProfile);
            EditorUtility.SetDirty(director);
            EditorSceneManager.MarkSceneDirty(activeScene);
        }

        private static List<CreatureArchetypeData> LoadArchetypes()
        {
            string[] guids = AssetDatabase.FindAssets("t:CreatureArchetypeData");
            var result = new List<CreatureArchetypeData>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                CreatureArchetypeData asset = AssetDatabase.LoadAssetAtPath<CreatureArchetypeData>(path);
                if (asset != null)
                    result.Add(asset);
            }

            return result;
        }

        private static bool IsCalmFamily(string familyId)
        {
            return string.Equals(familyId, "biome.family.littoral_karst", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(familyId, "biome.family.fossil_reef", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(familyId, "biome.family.crystal_growth", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsServiceHeavyFamily(string familyId)
        {
            return string.Equals(familyId, "biome.family.tectonic_spine", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(familyId, "biome.family.chemosynthetic_brine", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(familyId, "biome.family.metallic_hadal", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRiftFamily(string familyId)
        {
            return string.Equals(familyId, "biome.family.rift_spine", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(familyId, "biome.family.rift_void", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(familyId, "biome.family.volcanic_glass", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(familyId, "biome.family.volcanic_hadal", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLateSparseFamily(string familyId)
        {
            return string.Equals(familyId, "biome.family.abyssal_silt", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(familyId, "biome.family.metallic_hadal", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(familyId, "biome.family.rift_void", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(familyId, "biome.family.volcanic_hadal", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsResourceFriendlyFamily(string familyId)
        {
            return string.Equals(familyId, "biome.family.sediment_drift", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(familyId, "biome.family.granite_escarpment", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMassiveSurfaceSetpiece(HectonBiomeMatrixProfile profile)
        {
            if (profile == null || profile.maxDepthMeters > 3000f)
                return false;

            string biomeName = profile.biomeName != null ? profile.biomeName.ToLowerInvariant() : string.Empty;
            return biomeName.Contains("archipelago needles") ||
                   biomeName.Contains("sea-stack forest") ||
                   biomeName.Contains("coral porous walls") ||
                   biomeName.Contains("granite spine");
        }

        private static bool IsSurfaceLeviathanCandidate(HectonBiomeMatrixProfile profile)
        {
            if (!IsMassiveSurfaceSetpiece(profile))
                return false;

            return profile.landmarkStrength >= 4 &&
                   (profile.survivalPressure >= 3 ||
                    ContainsToken(profile.suggestedZoneFamily, "progression.route.landmark", "progression.skyline.far", "resources.landmarks.far"));
        }

        private static bool IsHeavyHunterSetpiece(HectonBiomeMatrixProfile profile)
        {
            switch (NormalizeBiomeName(profile != null ? profile.biomeName : null))
            {
                case "pressure-slabs":
                case "iron shards":
                case "the iron peak":
                    return true;
                default:
                    return false;
            }
        }

        private static CreatureArchetypeData ResolveLargeThreatArchetype(
            HectonBiomeMatrixProfile profile,
            List<FaunaEntry> entries,
            out bool preferHeavyHunter)
        {
            preferHeavyHunter = IsHeavyHunterSetpiece(profile);
            if (entries == null || entries.Count == 0)
                return null;

            if (preferHeavyHunter)
                return FindFirstRole(entries, CreatureRoleType.Hunter);

            if (ShouldIncludeLeviathan(profile))
                return FindFirstRole(entries, CreatureRoleType.Leviathan);

            if (IsMassiveSurfaceSetpiece(profile) && profile.survivalPressure >= 4)
            {
                preferHeavyHunter = true;
                return FindFirstRole(entries, CreatureRoleType.Hunter);
            }

            return null;
        }

        private static CreatureArchetypeData FindFirstRole(List<FaunaEntry> entries, CreatureRoleType roleType)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                CreatureArchetypeData archetype = entries[i].archetype;
                if (archetype != null && archetype.roleType == roleType)
                    return archetype;
            }

            return null;
        }

        private static LeviathanEncounterType ResolveLargeThreatEncounterType(
            HectonBiomeMatrixProfile profile,
            CreatureArchetypeData largeThreat,
            bool preferHeavyHunter)
        {
            if (largeThreat == null)
                return LeviathanEncounterType.PresenceCircle;

            if (largeThreat.roleType == CreatureRoleType.Leviathan)
                return largeThreat.leviathanEncounterType;

            string creatureId = largeThreat.creatureId != null ? largeThreat.creatureId.ToLowerInvariant() : string.Empty;
            if (creatureId.Contains("armor_breaker"))
                return LeviathanEncounterType.SentinelPressure;
            if (creatureId.Contains("shadow_interceptor") || creatureId.Contains("ambusher"))
                return LeviathanEncounterType.AmbushBurst;
            if (preferHeavyHunter && ContainsToken(profile.biomeName, "iron", "pressure", "peak", "gate", "spine"))
                return LeviathanEncounterType.SentinelPressure;
            if (ContainsToken(profile.biomeName, "rift", "void"))
                return LeviathanEncounterType.AmbushBurst;

            return LeviathanEncounterType.PresenceCircle;
        }

        private static float ResolveLargeThreatZoneRadius(
            HectonBiomeMatrixProfile profile,
            float baseRadius,
            bool preferHeavyHunter)
        {
            float radius = Mathf.Max(256f, baseRadius);

            if (preferHeavyHunter)
                return radius * 0.9f;

            if (IsMassiveSurfaceSetpiece(profile))
                return radius * 1.3f;

            if (ContainsToken(profile.biomeName, "void", "rift-maw", "heart of the rift", "static matrix"))
                return radius * 1.2f;

            return radius * 1.05f;
        }

        private static string ResolveLargeThreatZoneLabel(
            HectonBiomeMatrixProfile profile,
            CreatureArchetypeData largeThreat,
            bool preferHeavyHunter)
        {
            string biomeName = string.IsNullOrWhiteSpace(profile.biomeName) ? "Unnamed Biome" : profile.biomeName.Trim();
            string encounterLabel = DescribeEncounterType(ResolveLargeThreatEncounterType(profile, largeThreat, preferHeavyHunter));
            if (preferHeavyHunter)
                return $"{biomeName} / heavy hunter / {encounterLabel}";

            return $"{biomeName} / leviathan / {encounterLabel}";
        }

        private static string DescribeEncounterType(LeviathanEncounterType encounterType)
        {
            switch (encounterType)
            {
                case LeviathanEncounterType.AmbushBurst:
                    return "ambush burst";
                case LeviathanEncounterType.SentinelPressure:
                    return "sentinel pressure";
                default:
                    return "presence circle";
            }
        }

        private static string NormalizeBiomeName(string biomeName)
        {
            return string.IsNullOrWhiteSpace(biomeName)
                ? string.Empty
                : biomeName.Trim().ToLowerInvariant();
        }

        private static bool IsGenericReserveBiomeName(string biomeName)
        {
            return !string.IsNullOrWhiteSpace(biomeName) &&
                   biomeName.StartsWith("Tier ", System.StringComparison.OrdinalIgnoreCase) &&
                   biomeName.Contains("Reserve", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool Contains(string[] values, string target)
        {
            if (values == null || string.IsNullOrWhiteSpace(target))
                return false;

            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], target, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool ContainsToken(string source, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(source) || tokens == null)
                return false;

            string lower = source.ToLowerInvariant();
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrWhiteSpace(token) && lower.Contains(token.ToLowerInvariant()))
                    return true;
            }

            return false;
        }

        private static string ToAssetToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "UnnamedBiome";

            return value.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("'", string.Empty);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slashIndex = path.LastIndexOf('/');
            if (slashIndex <= 0)
                return;

            string parent = path.Substring(0, slashIndex);
            string folderName = path.Substring(slashIndex + 1);
            EnsureFolder(parent);

            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, folderName);
        }

        private readonly struct ArchetypeScore
        {
            public readonly CreatureArchetypeData archetype;
            public readonly int score;

            public ArchetypeScore(CreatureArchetypeData archetype, int score)
            {
                this.archetype = archetype;
                this.score = score;
            }
        }
    }
}
