using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton8.Environment;
using Hecton8.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools
{
    public static class WorldProceduralMatrixBiomeContentReport
    {
        private const string ReportFileName = "PROCEDURAL_MATRIX_BIOME_CONTENT_REPORT.md";
        private static ISet<string> _activeRepresentativeSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [MenuItem("Hecton/Validation/Generate Procedural Matrix Biome Content Report", priority = 239)]
        public static void GenerateReport()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogError("[WorldProceduralMatrixBiomeContentReport] No active loaded scene.");
                return;
            }

            WorldProceduralFieldSampler sampler = UnityEngine.Object.FindAnyObjectByType<WorldProceduralFieldSampler>(FindObjectsInactive.Include);
            WorldProceduralScatterDirector scatterDirector = UnityEngine.Object.FindAnyObjectByType<WorldProceduralScatterDirector>(FindObjectsInactive.Include);
            BiomeMatrixDirector biomeMatrixDirector = UnityEngine.Object.FindAnyObjectByType<BiomeMatrixDirector>(FindObjectsInactive.Include);
            if (sampler == null || scatterDirector == null || biomeMatrixDirector == null || biomeMatrixDirector.MatrixCatalog == null)
            {
                Debug.LogError("[WorldProceduralMatrixBiomeContentReport] Required world managers were not found in scene.");
                return;
            }

            SerializedObject samplerObject = new SerializedObject(sampler);
            bool originalForcePatternOverride = GetBool(samplerObject, "forcePatternPreviewOverride");
            bool originalLimitPatternOverride = GetBool(samplerObject, "limitPatternOverrideToFallback");
            WorldProceduralPattern originalPattern = GetEnum<WorldProceduralPattern>(samplerObject, "previewPatternOverride");
            bool originalForceMatrixOverride = GetBool(samplerObject, "forceMatrixBiomePreviewOverride");
            bool originalLimitMatrixOverride = GetBool(samplerObject, "limitMatrixBiomeOverrideToFallback");
            UnityEngine.Object originalMatrixOverride = GetObject(samplerObject, "previewMatrixBiomeOverride");

            List<PatternContentSection> sections = new List<PatternContentSection>(9);

            try
            {
                SetBool(samplerObject, "forcePatternPreviewOverride", true);
                SetBool(samplerObject, "limitPatternOverrideToFallback", true);
                SetBool(samplerObject, "forceMatrixBiomePreviewOverride", true);
                SetBool(samplerObject, "limitMatrixBiomeOverrideToFallback", true);

                WorldProceduralPattern[] patterns = (WorldProceduralPattern[])Enum.GetValues(typeof(WorldProceduralPattern));
                HectonBiomeMatrixProfile[] profiles = biomeMatrixDirector.MatrixCatalog.Profiles;
                for (int i = 0; i < patterns.Length; i++)
                {
                    WorldProceduralPattern pattern = patterns[i];
                    RepresentativeBiome[] representatives = SelectRepresentativeBiomes(pattern, profiles);
                    PatternContentSection section = new PatternContentSection
                    {
                        requestedPattern = pattern.ToString(),
                        representatives = new List<RepresentativeSnapshot>(representatives.Length)
                    };

                    for (int repIndex = 0; repIndex < representatives.Length; repIndex++)
                    {
                        RepresentativeBiome representative = representatives[repIndex];
                        if (representative.profile == null)
                            continue;

                        SetEnum(samplerObject, "previewPatternOverride", pattern);
                        SetObject(samplerObject, "previewMatrixBiomeOverride", representative.profile);
                        samplerObject.ApplyModifiedPropertiesWithoutUndo();

                        WorldProceduralScatterPreviewBuilder.RebuildProceduralScatterPreview();
                        section.representatives.Add(CaptureSnapshot(representative.kindLabel, representative.profile, sampler, scatterDirector));
                    }

                    sections.Add(section);
                }
            }
            finally
            {
                SetBool(samplerObject, "forcePatternPreviewOverride", originalForcePatternOverride);
                SetBool(samplerObject, "limitPatternOverrideToFallback", originalLimitPatternOverride);
                SetEnum(samplerObject, "previewPatternOverride", originalPattern);
                SetBool(samplerObject, "forceMatrixBiomePreviewOverride", originalForceMatrixOverride);
                SetBool(samplerObject, "limitMatrixBiomeOverrideToFallback", originalLimitMatrixOverride);
                SetObject(samplerObject, "previewMatrixBiomeOverride", originalMatrixOverride);
                samplerObject.ApplyModifiedPropertiesWithoutUndo();
                WorldProceduralScatterPreviewBuilder.RebuildProceduralScatterPreview();
            }

            string reportPath = Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath, ReportFileName);
            File.WriteAllText(reportPath, BuildMarkdownReport(activeScene.path, sections), Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log($"[WorldProceduralMatrixBiomeContentReport] Wrote report to {reportPath}");
        }

        private static RepresentativeSnapshot CaptureSnapshot(
            string kindLabel,
            HectonBiomeMatrixProfile profile,
            WorldProceduralFieldSampler sampler,
            WorldProceduralScatterDirector scatterDirector)
        {
            SerializedObject samplerObject = new SerializedObject(sampler);
            SerializedObject scatterObject = new SerializedObject(scatterDirector);

            return new RepresentativeSnapshot
            {
                kindLabel = kindLabel,
                biomeName = profile.biomeName,
                familyLabel = profile.familyProfile != null ? profile.familyProfile.familyLabel : profile.familyId,
                resolvedPattern = GetString(samplerObject, "_debugLastPattern"),
                resolvedZone = GetString(samplerObject, "_debugLastZone"),
                resolvedBiomeContext = GetString(scatterObject, "_debugResolvedBiomeContextProfile"),
                preferredGroundCategories = JoinFamilyLabels(profile.preferredGroundFamilies),
                preferredClusterCategories = JoinFamilyLabels(profile.preferredClusterFamilies),
                preferredStructureCategories = JoinFamilyLabels(profile.preferredStructureFamilies),
                preferredSpawnCategories = JoinFamilyLabels(profile.preferredSpawnFamilies),
                topGroundFamily = GetString(scatterObject, "_debugGroundTopFamily"),
                topClusterFamily = GetString(scatterObject, "_debugClusterTopFamily"),
                topStructureFamily = GetString(scatterObject, "_debugStructureTopFamily"),
                topSpawnFamily = GetString(scatterObject, "_debugSpawnTopFamily"),
                dominantGroundFamily = GetString(scatterObject, "_debugGroundDominantFamily"),
                dominantClusterFamily = GetString(scatterObject, "_debugClusterDominantFamily"),
                dominantStructureFamily = GetString(scatterObject, "_debugStructureDominantFamily"),
                dominantSpawnFamily = GetString(scatterObject, "_debugSpawnDominantFamily"),
                structureNaturalLandmarkCount = GetInt(scatterObject, "_debugStructureNaturalLandmarkCount"),
                structureTechFragmentCount = GetInt(scatterObject, "_debugStructureTechFragmentCount"),
                structureCaveReadCount = GetInt(scatterObject, "_debugStructureCaveReadCount"),
                structureBiologicalSilhouetteCount = GetInt(scatterObject, "_debugStructureBiologicalSilhouetteCount"),
                spawnPassiveCount = GetInt(scatterObject, "_debugSpawnPassiveCount"),
                spawnPredatorCount = GetInt(scatterObject, "_debugSpawnPredatorCount"),
                totalGroundCount = GetInt(scatterObject, "_debugGroundPlacements"),
                totalClusterCount = GetInt(scatterObject, "_debugClusterPlacements"),
                totalStructureCount = GetInt(scatterObject, "_debugStructurePlacements"),
                totalSpawnCount = GetInt(scatterObject, "_debugSpawnPlacements"),
                differenceLine = BuildDifferenceLine(profile)
            };
        }

        private static RepresentativeBiome[] SelectRepresentativeBiomes(
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile[] profiles)
        {
            List<HectonBiomeMatrixProfile> candidates = new List<HectonBiomeMatrixProfile>(32);
            if (profiles != null)
            {
                for (int i = 0; i < profiles.Length; i++)
                {
                    HectonBiomeMatrixProfile profile = profiles[i];
                    if (profile == null || profile.isPlaceholder)
                        continue;

                    if (GetPatternCompatibilityScore(pattern, profile) > 0)
                        candidates.Add(profile);
                }
            }

            if (candidates.Count == 0 && profiles != null)
            {
                for (int i = 0; i < profiles.Length; i++)
                {
                    HectonBiomeMatrixProfile profile = profiles[i];
                    if (profile != null && !profile.isPlaceholder)
                        candidates.Add(profile);
                }
            }

            HashSet<int> usedMatrixIndices = new HashSet<int>();
            HashSet<string> usedFamilyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> usedSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _activeRepresentativeSignatures = usedSignatures;
            return new[]
            {
                new RepresentativeBiome("Most resource-rich", SelectBestRepresentative(candidates, usedMatrixIndices, usedFamilyIds, pattern, GetResourceRepresentativeScore)),
                new RepresentativeBiome("Most tech-marked", SelectBestRepresentative(candidates, usedMatrixIndices, usedFamilyIds, pattern, GetTechRepresentativeScore)),
                new RepresentativeBiome("Most dangerous", SelectBestRepresentative(candidates, usedMatrixIndices, usedFamilyIds, pattern, GetDangerRepresentativeScore))
            };
        }

        private static HectonBiomeMatrixProfile SelectBestRepresentative(
            IReadOnlyList<HectonBiomeMatrixProfile> candidates,
            ISet<int> usedMatrixIndices,
            ISet<string> usedFamilyIds,
            WorldProceduralPattern pattern,
            Func<HectonBiomeMatrixProfile, int> scoreSelector)
        {
            HectonBiomeMatrixProfile best = TrySelectBestRepresentative(candidates, usedMatrixIndices, usedFamilyIds, pattern, scoreSelector, true, true);
            if (best == null)
                best = TrySelectBestRepresentative(candidates, usedMatrixIndices, usedFamilyIds, pattern, scoreSelector, false, true);
            if (best == null)
                best = TrySelectBestRepresentative(candidates, usedMatrixIndices, usedFamilyIds, pattern, scoreSelector, true, false);
            if (best == null)
                best = TrySelectBestRepresentative(candidates, usedMatrixIndices, usedFamilyIds, pattern, scoreSelector, false, false);

            if (best != null)
            {
                usedMatrixIndices.Add(best.matrixIndex);
                if (!string.IsNullOrWhiteSpace(best.familyId))
                    usedFamilyIds.Add(best.familyId);
                _activeRepresentativeSignatures.Add(GetRepresentativeSignature(best));
            }

            return best;
        }

        private static HectonBiomeMatrixProfile TrySelectBestRepresentative(
            IReadOnlyList<HectonBiomeMatrixProfile> candidates,
            ISet<int> usedMatrixIndices,
            ISet<string> usedFamilyIds,
            WorldProceduralPattern pattern,
            Func<HectonBiomeMatrixProfile, int> scoreSelector,
            bool requireFreshFamily,
            bool requireFreshSignature)
        {
            HectonBiomeMatrixProfile best = null;
            int bestScore = int.MinValue;
            if (candidates == null)
                return null;

            for (int i = 0; i < candidates.Count; i++)
            {
                HectonBiomeMatrixProfile profile = candidates[i];
                if (profile == null || usedMatrixIndices.Contains(profile.matrixIndex))
                    continue;

                if (requireFreshFamily && !string.IsNullOrWhiteSpace(profile.familyId) && usedFamilyIds.Contains(profile.familyId))
                    continue;

                if (requireFreshSignature && _activeRepresentativeSignatures.Contains(GetRepresentativeSignature(profile)))
                    continue;

                int totalScore = (GetPatternCompatibilityScore(pattern, profile) * 100) + scoreSelector(profile);
                if (totalScore <= bestScore)
                    continue;

                best = profile;
                bestScore = totalScore;
            }

            return best;
        }

        private static string GetRepresentativeSignature(HectonBiomeMatrixProfile profile)
        {
            if (profile == null)
                return "None";

            return $"{profile.primaryClusterFocus}|{profile.primaryStructureFocus}|{profile.faunaMood}|{JoinFamilyLabels(profile.preferredClusterFamilies)}|{JoinFamilyLabels(profile.preferredStructureFamilies)}";
        }

        private static int GetPatternCompatibilityScore(WorldProceduralPattern pattern, HectonBiomeMatrixProfile profile)
        {
            if (profile == null)
                return 0;

            int score = 0;
            string familyId = (profile.familyId ?? string.Empty).ToLowerInvariant();
            string zoneHint = (profile.suggestedZoneFamily ?? string.Empty).ToLowerInvariant();
            string progressionRole = (profile.progressionRole ?? string.Empty).ToLowerInvariant();

            switch (pattern)
            {
                case WorldProceduralPattern.FertileShallows:
                    score += ScoreFamilyTokens(familyId, ("littoral_karst", 120), ("crystal_growth", 110), ("fossil_reef", 70));
                    if (profile.primaryClusterFocus == WorldProceduralClusterFocus.FertileGrowth)
                        score += 35;
                    if (profile.primaryClusterFocus == WorldProceduralClusterFocus.BiologicalNest)
                        score += 28;
                    if (profile.primaryStructureFocus == WorldProceduralStructureFocus.BiologicalSilhouette)
                        score += 18;
                    if (profile.faunaMood == WorldProceduralFaunaMood.Calm || profile.faunaMood == WorldProceduralFaunaMood.Lively)
                        score += 20;
                    break;

                case WorldProceduralPattern.ReefNavigation:
                    score += ScoreFamilyTokens(familyId, ("fossil_reef", 120), ("crystal_growth", 100), ("littoral_karst", 85), ("granite_escarpment", 35));
                    if (profile.primaryStructureFocus == WorldProceduralStructureFocus.NaturalLandmark)
                        score += 30;
                    if (profile.secondaryStructureFocus == WorldProceduralStructureFocus.CaveRead)
                        score += 18;
                    if (profile.primaryClusterFocus == WorldProceduralClusterFocus.ShelterPocket || profile.secondaryClusterFocus == WorldProceduralClusterFocus.ShelterPocket)
                        score += 10;
                    break;

                case WorldProceduralPattern.SedimentResources:
                    score += ScoreFamilyTokens(familyId, ("sediment_drift", 130), ("granite_escarpment", 110), ("abyssal_silt", 55));
                    if (profile.primaryClusterFocus == WorldProceduralClusterFocus.ResourcePocket)
                        score += 32;
                    if (profile.primaryClusterFocus == WorldProceduralClusterFocus.RockCover)
                        score += 12;
                    if (profile.secondaryClusterFocus == WorldProceduralClusterFocus.ShelterPocket)
                        score += 16;
                    break;

                case WorldProceduralPattern.IndustrialService:
                    score += ScoreFamilyTokens(familyId, ("tectonic_spine", 130), ("metallic_hadal", 110), ("chemosynthetic_brine", 95));
                    if (profile.primaryStructureFocus == WorldProceduralStructureFocus.TechFragment)
                        score += 34;
                    break;

                case WorldProceduralPattern.BrineToxic:
                    score += ScoreFamilyTokens(familyId, ("chemosynthetic_brine", 140), ("metallic_hadal", 85), ("tectonic_spine", 50));
                    if (profile.primaryClusterFocus == WorldProceduralClusterFocus.HazardPocket)
                        score += 20;
                    break;

                case WorldProceduralPattern.VolcanicPressure:
                    score += ScoreFamilyTokens(familyId, ("volcanic_glass", 140), ("volcanic_hadal", 125), ("granite_escarpment", 30));
                    if (profile.primaryStructureFocus == WorldProceduralStructureFocus.CaveRead)
                        score += 30;
                    break;

                case WorldProceduralPattern.RiftHazard:
                    score += ScoreFamilyTokens(familyId, ("rift_void", 140), ("rift_spine", 130), ("volcanic_hadal", 45), ("metallic_hadal", 35));
                    if (profile.faunaMood == WorldProceduralFaunaMood.Hostile || profile.faunaMood == WorldProceduralFaunaMood.Mixed)
                        score += 22;
                    break;

                case WorldProceduralPattern.AbyssSparse:
                    score += ScoreFamilyTokens(familyId, ("abyssal_silt", 135), ("metallic_hadal", 75), ("granite_escarpment", 25));
                    if (profile.primaryClusterFocus == WorldProceduralClusterFocus.RockCover)
                        score += 20;
                    break;

                case WorldProceduralPattern.LandmarkCorridor:
                    score += ScoreFamilyTokens(familyId, ("granite_escarpment", 120), ("fossil_reef", 90), ("rift_spine", 70), ("littoral_karst", 55));
                    if (profile.primaryStructureFocus == WorldProceduralStructureFocus.NaturalLandmark || profile.primaryStructureFocus == WorldProceduralStructureFocus.CaveRead)
                        score += 28;
                    break;
            }

            if (zoneHint.Contains("navigation") && (pattern == WorldProceduralPattern.ReefNavigation || pattern == WorldProceduralPattern.LandmarkCorridor))
                score += 18;
            if (zoneHint.Contains("resources") && pattern == WorldProceduralPattern.SedimentResources)
                score += 18;
            if ((zoneHint.Contains("service") || zoneHint.Contains("power")) && (pattern == WorldProceduralPattern.IndustrialService || pattern == WorldProceduralPattern.BrineToxic))
                score += 18;
            if ((zoneHint.Contains("hazard") || progressionRole.Contains("final") || progressionRole.Contains("pressure")) && (pattern == WorldProceduralPattern.RiftHazard || pattern == WorldProceduralPattern.VolcanicPressure))
                score += 16;

            return score;
        }

        private static int GetResourceRepresentativeScore(HectonBiomeMatrixProfile profile)
        {
            if (profile == null)
                return 0;

            int score = (profile.rewardPull * 90)
                + (profile.commonResourceBias * 55)
                + (profile.uncommonResourceBias * 40)
                + (profile.rareResourceBias * 30);

            if (profile.primaryClusterFocus == WorldProceduralClusterFocus.ResourcePocket)
                score += 120;
            if (profile.secondaryClusterFocus == WorldProceduralClusterFocus.ResourcePocket)
                score += 60;
            if (JoinFamilyLabels(profile.preferredClusterFamilies).Contains("Pocket Resource", StringComparison.OrdinalIgnoreCase))
                score += 60;

            return score;
        }

        private static int GetTechRepresentativeScore(HectonBiomeMatrixProfile profile)
        {
            if (profile == null)
                return 0;

            int score = (profile.salvageBias * 100)
                + (profile.nodeExtractionBias * 50)
                + (profile.landmarkStrength * 25);

            if (profile.primaryStructureFocus == WorldProceduralStructureFocus.TechFragment)
                score += 120;
            if (profile.secondaryStructureFocus == WorldProceduralStructureFocus.TechFragment)
                score += 60;
            if (JoinFamilyLabels(profile.preferredStructureFamilies).Contains("Service Scar", StringComparison.OrdinalIgnoreCase))
                score += 60;

            return score;
        }

        private static int GetDangerRepresentativeScore(HectonBiomeMatrixProfile profile)
        {
            if (profile == null)
                return 0;

            int score = (profile.survivalPressure * 100)
                + (profile.routePressure * 45)
                + (profile.landmarkStrength * 20);

            if (profile.primaryClusterFocus == WorldProceduralClusterFocus.HazardPocket)
                score += 90;
            if (profile.primaryStructureFocus == WorldProceduralStructureFocus.CaveRead)
                score += 35;
            if (profile.faunaMood == WorldProceduralFaunaMood.Hostile)
                score += 100;
            else if (profile.faunaMood == WorldProceduralFaunaMood.Mixed)
                score += 45;

            if (JoinFamilyLabels(profile.preferredSpawnFamilies).Contains("Predator", StringComparison.OrdinalIgnoreCase))
                score += 40;

            return score;
        }

        private static int ScoreFamilyTokens(string familyId, params (string token, int score)[] tokens)
        {
            if (string.IsNullOrWhiteSpace(familyId) || tokens == null)
                return 0;

            int best = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                if (familyId.Contains(tokens[i].token) && tokens[i].score > best)
                    best = tokens[i].score;
            }

            return best;
        }

        private static string BuildMarkdownReport(string scenePath, IReadOnlyList<PatternContentSection> sections)
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("# Procedural Matrix Biome Content Report");
            builder.AppendLine();
            builder.AppendLine($"- Scene: `{scenePath}`");
            builder.AppendLine($"- Generated: `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`");
            builder.AppendLine($"- Mode: `Forced preview pattern + forced matrix biome override (fallback-compatible)`");
            builder.AppendLine();

            for (int i = 0; i < sections.Count; i++)
            {
                PatternContentSection section = sections[i];
                builder.AppendLine($"## {section.requestedPattern}");
                builder.AppendLine();

                for (int repIndex = 0; repIndex < section.representatives.Count; repIndex++)
                {
                    RepresentativeSnapshot snapshot = section.representatives[repIndex];
                    builder.AppendLine($"### {snapshot.kindLabel} - {snapshot.biomeName}");
                    builder.AppendLine();
                    builder.AppendLine($"- Biome family: `{snapshot.familyLabel}`");
                    builder.AppendLine($"- Resolved: pattern `{snapshot.resolvedPattern}` | zone `{snapshot.resolvedZone}` | biome context `{snapshot.resolvedBiomeContext}`");
                    builder.AppendLine($"- Preferred ground categories: `{snapshot.preferredGroundCategories}`");
                    builder.AppendLine($"- Preferred cluster categories: `{snapshot.preferredClusterCategories}`");
                    builder.AppendLine($"- Preferred structure categories: `{snapshot.preferredStructureCategories}`");
                    builder.AppendLine($"- Preferred spawn categories: `{snapshot.preferredSpawnCategories}`");
                    builder.AppendLine($"- Counts: ground `{snapshot.totalGroundCount}` | cluster `{snapshot.totalClusterCount}` | structure `{snapshot.totalStructureCount}` | spawn `{snapshot.totalSpawnCount}`");
                    builder.AppendLine($"- Top categories now: ground `{snapshot.topGroundFamily}` | cluster `{snapshot.topClusterFamily}` | structure `{snapshot.topStructureFamily}` | spawn `{snapshot.topSpawnFamily}`");
                    builder.AppendLine($"- Dominant categories now: ground `{snapshot.dominantGroundFamily}` | cluster `{snapshot.dominantClusterFamily}` | structure `{snapshot.dominantStructureFamily}` | spawn `{snapshot.dominantSpawnFamily}`");
                    builder.AppendLine($"- Structure role mix: natural `{snapshot.structureNaturalLandmarkCount}` | tech `{snapshot.structureTechFragmentCount}` | cave `{snapshot.structureCaveReadCount}` | bio `{snapshot.structureBiologicalSilhouetteCount}`");
                    builder.AppendLine($"- Spawn mix: passive `{snapshot.spawnPassiveCount}` | predator `{snapshot.spawnPredatorCount}`");
                    builder.AppendLine($"- What makes this place different: {snapshot.differenceLine}");
                    builder.AppendLine();
                }

                AppendDifferenceChecks(builder, section.representatives);
            }

            return builder.ToString();
        }

        private static void AppendDifferenceChecks(StringBuilder builder, IReadOnlyList<RepresentativeSnapshot> representatives)
        {
            if (representatives == null || representatives.Count < 2)
                return;

            builder.AppendLine("### Difference checks");
            builder.AppendLine();
            for (int i = 0; i < representatives.Count; i++)
            {
                for (int j = i + 1; j < representatives.Count; j++)
                    builder.AppendLine($"- {representatives[i].kindLabel} vs {representatives[j].kindLabel}: {BuildDifferenceSummary(representatives[i], representatives[j])}");
            }

            builder.AppendLine();
        }

        private static string BuildDifferenceSummary(RepresentativeSnapshot a, RepresentativeSnapshot b)
        {
            List<string> differences = new List<string>(4);
            if (!string.Equals(a.dominantClusterFamily, b.dominantClusterFamily, StringComparison.Ordinal))
                differences.Add("different dominant cluster category");
            if (!string.Equals(a.dominantStructureFamily, b.dominantStructureFamily, StringComparison.Ordinal))
                differences.Add("different dominant structure category");
            if (a.spawnPassiveCount != b.spawnPassiveCount || a.spawnPredatorCount != b.spawnPredatorCount)
                differences.Add("different spawn mix");
            if (!string.Equals(GetCombinedPreferredList(a), GetCombinedPreferredList(b), StringComparison.Ordinal))
                differences.Add("different preferred category list");

            if (differences.Count == 0)
                return "0/4 different";

            return $"{differences.Count}/4 different ({string.Join(", ", differences)})";
        }

        private static string GetCombinedPreferredList(RepresentativeSnapshot snapshot)
        {
            return $"{snapshot.preferredGroundCategories}|{snapshot.preferredClusterCategories}|{snapshot.preferredStructureCategories}|{snapshot.preferredSpawnCategories}";
        }

        private static string JoinFamilyLabels(WorldPrefabFamilyProfile[] families)
        {
            if (families == null || families.Length == 0)
                return "None";

            List<string> labels = new List<string>(families.Length);
            for (int i = 0; i < families.Length; i++)
            {
                WorldPrefabFamilyProfile family = families[i];
                if (family == null || string.IsNullOrWhiteSpace(family.familyLabel))
                    continue;

                labels.Add(family.familyLabel);
            }

            return labels.Count == 0 ? "None" : string.Join(" -> ", labels);
        }

        private static string BuildDifferenceLine(HectonBiomeMatrixProfile profile)
        {
            if (profile == null)
                return "Biome not found.";

            string cluster = FirstPreferredLabel(profile.preferredClusterFamilies, "mixed pockets");
            string structure = FirstPreferredLabel(profile.preferredStructureFamilies, "mixed landmarks");
            string spawn = FirstPreferredLabel(profile.preferredSpawnFamilies, "passive life");
            return $"This biome leans into `{cluster}`, remembers itself through `{structure}`, and tends toward `{spawn}`.";
        }

        private static string FirstPreferredLabel(WorldPrefabFamilyProfile[] families, string fallback)
        {
            if (families == null)
                return fallback;

            for (int i = 0; i < families.Length; i++)
            {
                if (families[i] != null && !string.IsNullOrWhiteSpace(families[i].familyLabel))
                    return families[i].familyLabel;
            }

            return fallback;
        }

        private static bool GetBool(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null && property.boolValue;
        }

        private static string GetString(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null ? property.stringValue : "None";
        }

        private static int GetInt(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null ? property.intValue : 0;
        }

        private static UnityEngine.Object GetObject(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null ? property.objectReferenceValue : null;
        }

        private static TEnum GetEnum<TEnum>(SerializedObject serializedObject, string propertyName) where TEnum : struct
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
                return default;

            return (TEnum)Enum.ToObject(typeof(TEnum), property.enumValueIndex);
        }

        private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.boolValue = value;
        }

        private static void SetObject(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
        }

        private static void SetEnum<TEnum>(SerializedObject serializedObject, string propertyName, TEnum value) where TEnum : struct
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.enumValueIndex = Convert.ToInt32(value);
        }

        private readonly struct RepresentativeBiome
        {
            public RepresentativeBiome(string kindLabel, HectonBiomeMatrixProfile profile)
            {
                this.kindLabel = kindLabel;
                this.profile = profile;
            }

            public string kindLabel { get; }
            public HectonBiomeMatrixProfile profile { get; }
        }

        private sealed class PatternContentSection
        {
            public string requestedPattern;
            public List<RepresentativeSnapshot> representatives;
        }

        private sealed class RepresentativeSnapshot
        {
            public string kindLabel;
            public string biomeName;
            public string familyLabel;
            public string resolvedPattern;
            public string resolvedZone;
            public string resolvedBiomeContext;
            public string preferredGroundCategories;
            public string preferredClusterCategories;
            public string preferredStructureCategories;
            public string preferredSpawnCategories;
            public string topGroundFamily;
            public string topClusterFamily;
            public string topStructureFamily;
            public string topSpawnFamily;
            public string dominantGroundFamily;
            public string dominantClusterFamily;
            public string dominantStructureFamily;
            public string dominantSpawnFamily;
            public int structureNaturalLandmarkCount;
            public int structureTechFragmentCount;
            public int structureCaveReadCount;
            public int structureBiologicalSilhouetteCount;
            public int spawnPassiveCount;
            public int spawnPredatorCount;
            public int totalGroundCount;
            public int totalClusterCount;
            public int totalStructureCount;
            public int totalSpawnCount;
            public string differenceLine;
        }
    }
}
