using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton8.Environment;
using Hecton8.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    public static class WorldProceduralMatrixBiomeMemoryReport
    {
        private const string ReportFileName = "PROCEDURAL_MATRIX_BIOME_MEMORY_REPORT.md";
        private static ISet<string> _activeRepresentativeSignatures = new HashSet<string>(16, StringComparer.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _activeRepresentativeSignatures = new HashSet<string>(16, StringComparer.OrdinalIgnoreCase);
        }

        [MenuItem("Hecton8/Validation/Generate Procedural Matrix Biome Memory Report", priority = 238)]
        public static void GenerateReport()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogError("[WorldProceduralMatrixBiomeMemoryReport] No active loaded scene.");
                return;
            }

            WorldProceduralFieldSampler sampler = UnityEngine.Object.FindAnyObjectByType<WorldProceduralFieldSampler>(FindObjectsInactive.Include);
            WorldProceduralScatterDirector scatterDirector = UnityEngine.Object.FindAnyObjectByType<WorldProceduralScatterDirector>(FindObjectsInactive.Include);
            BiomeMatrixDirector biomeMatrixDirector = UnityEngine.Object.FindAnyObjectByType<BiomeMatrixDirector>(FindObjectsInactive.Include);
            if (sampler == null || scatterDirector == null || biomeMatrixDirector == null || biomeMatrixDirector.MatrixCatalog == null)
            {
                Debug.LogError("[WorldProceduralMatrixBiomeMemoryReport] Required world managers were not found in scene.");
                return;
            }

            SerializedObject samplerObject = new SerializedObject(sampler);
            bool originalForcePatternOverride = GetBool(samplerObject, "forcePatternPreviewOverride");
            bool originalLimitPatternOverride = GetBool(samplerObject, "limitPatternOverrideToFallback");
            WorldProceduralPattern originalPattern = GetEnum<WorldProceduralPattern>(samplerObject, "previewPatternOverride");
            bool originalForceMatrixOverride = GetBool(samplerObject, "forceMatrixBiomePreviewOverride");
            bool originalLimitMatrixOverride = GetBool(samplerObject, "limitMatrixBiomeOverrideToFallback");
            UnityEngine.Object originalMatrixOverride = GetObject(samplerObject, "previewMatrixBiomeOverride");

            List<PatternMemorySection> sections = new List<PatternMemorySection>(9);

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
                    PatternMemorySection section = new PatternMemorySection
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
            Debug.Log($"[WorldProceduralMatrixBiomeMemoryReport] Wrote report to {reportPath}");
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
                primaryClusterFocus = profile.primaryClusterFocus.ToString(),
                secondaryClusterFocus = profile.secondaryClusterFocus.ToString(),
                primaryStructureFocus = profile.primaryStructureFocus.ToString(),
                secondaryStructureFocus = profile.secondaryStructureFocus.ToString(),
                faunaMood = profile.faunaMood.ToString(),
                resolvedPattern = GetString(samplerObject, "_debugLastPattern"),
                resolvedZone = GetString(samplerObject, "_debugLastZone"),
                resolvedBiomeContext = GetString(scatterObject, "_debugResolvedBiomeContextProfile"),
                topGroundFamily = GetString(scatterObject, "_debugGroundTopFamily"),
                topClusterFamily = GetString(scatterObject, "_debugClusterTopFamily"),
                topStructureFamily = GetString(scatterObject, "_debugStructureTopFamily"),
                topSpawnFamily = GetString(scatterObject, "_debugSpawnTopFamily"),
                dominantGroundFamily = GetString(scatterObject, "_debugGroundDominantFamily"),
                dominantClusterFamily = GetString(scatterObject, "_debugClusterDominantFamily"),
                dominantStructureFamily = GetString(scatterObject, "_debugStructureDominantFamily"),
                dominantSpawnFamily = GetString(scatterObject, "_debugSpawnDominantFamily"),
                dominantClusterAccent = GetString(scatterObject, "_debugClusterDominantAccentRole"),
                dominantStructureAccent = GetString(scatterObject, "_debugStructureDominantAccentRole"),
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
                memoryLine = BuildMemoryLine(profile)
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

            HashSet<int> usedMatrixIndices = new HashSet<int>(candidates.Count);
            HashSet<string> usedFamilyIds = new HashSet<string>(candidates.Count, StringComparer.OrdinalIgnoreCase);
            HashSet<string> usedMemorySignatures = new HashSet<string>(candidates.Count, StringComparer.OrdinalIgnoreCase);
            _activeRepresentativeSignatures = usedMemorySignatures;
            return new[]
            {
                new RepresentativeBiome("Samyy resursnyy", SelectBestRepresentative(candidates, usedMatrixIndices, usedFamilyIds, pattern, GetResourceRepresentativeScore)),
                new RepresentativeBiome("Samyy tehnogennyy", SelectBestRepresentative(candidates, usedMatrixIndices, usedFamilyIds, pattern, GetTechRepresentativeScore)),
                new RepresentativeBiome("Samyy opasnyy", SelectBestRepresentative(candidates, usedMatrixIndices, usedFamilyIds, pattern, GetDangerRepresentativeScore))
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

            return $"{profile.primaryClusterFocus}|{profile.primaryStructureFocus}|{profile.faunaMood}";
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
            if (profile.secondaryClusterFocus == WorldProceduralClusterFocus.ShelterPocket)
                score += 25;

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
            if (profile.primaryClusterFocus == WorldProceduralClusterFocus.DebrisField)
                score += 45;

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

        private static string BuildMarkdownReport(string scenePath, IReadOnlyList<PatternMemorySection> sections)
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("# Procedural Matrix Biome Memory Report");
            builder.AppendLine();
            builder.AppendLine($"- Scene: `{scenePath}`");
            builder.AppendLine($"- Generated: `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`");
            builder.AppendLine($"- Mode: `Forced preview pattern + forced matrix biome override (fallback-compatible)`");
            builder.AppendLine();

            for (int i = 0; i < sections.Count; i++)
            {
                PatternMemorySection section = sections[i];
                builder.AppendLine($"## {section.requestedPattern}");
                builder.AppendLine();

                for (int repIndex = 0; repIndex < section.representatives.Count; repIndex++)
                {
                    RepresentativeSnapshot snapshot = section.representatives[repIndex];
                    builder.AppendLine($"### {snapshot.kindLabel} — {snapshot.biomeName}");
                    builder.AppendLine();
                    builder.AppendLine($"- Family: `{snapshot.familyLabel}`");
                    builder.AppendLine($"- Resolved: pattern `{snapshot.resolvedPattern}` | zone `{snapshot.resolvedZone}` | biome context `{snapshot.resolvedBiomeContext}`");
                    builder.AppendLine($"- Cluster focus: `{snapshot.primaryClusterFocus}` -> `{snapshot.secondaryClusterFocus}`");
                    builder.AppendLine($"- Structure focus: `{snapshot.primaryStructureFocus}` -> `{snapshot.secondaryStructureFocus}`");
                    builder.AppendLine($"- Fauna mood: `{snapshot.faunaMood}`");
                    builder.AppendLine($"- Counts: ground `{snapshot.totalGroundCount}` | cluster `{snapshot.totalClusterCount}` | structure `{snapshot.totalStructureCount}` | spawn `{snapshot.totalSpawnCount}`");
                    builder.AppendLine($"- Top families: ground `{snapshot.topGroundFamily}` | cluster `{snapshot.topClusterFamily}` | structure `{snapshot.topStructureFamily}` | spawn `{snapshot.topSpawnFamily}`");
                    builder.AppendLine($"- Dominant families: ground `{snapshot.dominantGroundFamily}` | cluster `{snapshot.dominantClusterFamily}` | structure `{snapshot.dominantStructureFamily}` | spawn `{snapshot.dominantSpawnFamily}`");
                    builder.AppendLine($"- Accent mix: cluster `{snapshot.dominantClusterAccent}` | structure `{snapshot.dominantStructureAccent}`");
                    builder.AppendLine($"- Structure roles: natural `{snapshot.structureNaturalLandmarkCount}` | tech `{snapshot.structureTechFragmentCount}` | cave `{snapshot.structureCaveReadCount}` | bio `{snapshot.structureBiologicalSilhouetteCount}`");
                    builder.AppendLine($"- Spawn mix: passive `{snapshot.spawnPassiveCount}` | predator `{snapshot.spawnPredatorCount}`");
                    builder.AppendLine($"- Chem zapominaetsya: {snapshot.memoryLine}");
                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }

        private static string BuildMemoryLine(HectonBiomeMatrixProfile profile)
        {
            if (profile == null)
                return "Biom ne nayden.";

            string landmark = CleanSentence(profile.landmarkIdentity);
            string extraction = CleanSentence(profile.extractionFocus);
            string risk = CleanSentence(profile.riskSummary);
            string safePocket = CleanSentence(profile.safePocketIdentity);
            return $"orientir: {landmark}; poleznyy motiv: {extraction}; tihaya tochka: {safePocket}; risk: {risk}";
        }

        private static string CleanSentence(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "ne opisan" : value.Trim();
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

        private sealed class PatternMemorySection
        {
            public string requestedPattern;
            public List<RepresentativeSnapshot> representatives;
        }

        private sealed class RepresentativeSnapshot
        {
            public string kindLabel;
            public string biomeName;
            public string familyLabel;
            public string primaryClusterFocus;
            public string secondaryClusterFocus;
            public string primaryStructureFocus;
            public string secondaryStructureFocus;
            public string faunaMood;
            public string resolvedPattern;
            public string resolvedZone;
            public string resolvedBiomeContext;
            public string topGroundFamily;
            public string topClusterFamily;
            public string topStructureFamily;
            public string topSpawnFamily;
            public string dominantGroundFamily;
            public string dominantClusterFamily;
            public string dominantStructureFamily;
            public string dominantSpawnFamily;
            public string dominantClusterAccent;
            public string dominantStructureAccent;
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
            public string memoryLine;
        }
    }
}
