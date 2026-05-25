using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton8.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools
{
    public static class WorldProceduralPatternBalanceReport
    {
        private const string ReportFileName = "PROCEDURAL_WATER_PATTERN_REPORT.md";
        private static readonly List<GameObject> s_sceneRoots = new List<GameObject>(8);
        private static readonly List<WorldProceduralFieldSampler> s_fieldSamplers = new List<WorldProceduralFieldSampler>(2);
        private static readonly List<WorldProceduralScatterDirector> s_scatterDirectors = new List<WorldProceduralScatterDirector>(2);

        [MenuItem("Hecton/Validation/Generate Procedural Water Pattern Report", priority = 237)]
        public static void GenerateReport()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogError("[WorldProceduralPatternBalanceReport] No active loaded scene.");
                return;
            }

            WorldProceduralFieldSampler sampler = FindInScene(activeScene, s_fieldSamplers);
            WorldProceduralScatterDirector scatterDirector = FindInScene(activeScene, s_scatterDirectors);
            if (sampler == null || scatterDirector == null)
            {
                Debug.LogError("[WorldProceduralPatternBalanceReport] Required procedural managers were not found in scene.");
                return;
            }

            SerializedObject samplerObject = new SerializedObject(sampler);
            bool originalForceOverride = GetBool(samplerObject, "forcePatternPreviewOverride");
            bool originalLimitToFallback = GetBool(samplerObject, "limitPatternOverrideToFallback");
            WorldProceduralPattern originalPattern = GetEnum<WorldProceduralPattern>(samplerObject, "previewPatternOverride");
            bool originalForceMatrixOverride = GetBool(samplerObject, "forceMatrixBiomePreviewOverride");
            bool originalLimitMatrixOverride = GetBool(samplerObject, "limitMatrixBiomeOverrideToFallback");
            UnityEngine.Object originalMatrixOverride = GetObject(samplerObject, "previewMatrixBiomeOverride");

            List<PatternSnapshot> snapshots = new List<PatternSnapshot>(9);

            try
            {
                SetBool(samplerObject, "forcePatternPreviewOverride", true);
                SetBool(samplerObject, "limitPatternOverrideToFallback", true);
                SetBool(samplerObject, "forceMatrixBiomePreviewOverride", false);
                SetBool(samplerObject, "limitMatrixBiomeOverrideToFallback", true);
                SetObject(samplerObject, "previewMatrixBiomeOverride", null);

                WorldProceduralPattern[] patterns = (WorldProceduralPattern[])Enum.GetValues(typeof(WorldProceduralPattern));
                for (int i = 0; i < patterns.Length; i++)
                {
                    WorldProceduralPattern pattern = patterns[i];
                    SetEnum(samplerObject, "previewPatternOverride", pattern);
                    samplerObject.ApplyModifiedPropertiesWithoutUndo();

                    WorldProceduralScatterPreviewBuilder.RebuildProceduralScatterPreview();
                    snapshots.Add(CaptureSnapshot(pattern, sampler, scatterDirector));
                }
            }
            finally
            {
                SetBool(samplerObject, "forcePatternPreviewOverride", originalForceOverride);
                SetBool(samplerObject, "limitPatternOverrideToFallback", originalLimitToFallback);
                SetEnum(samplerObject, "previewPatternOverride", originalPattern);
                SetBool(samplerObject, "forceMatrixBiomePreviewOverride", originalForceMatrixOverride);
                SetBool(samplerObject, "limitMatrixBiomeOverrideToFallback", originalLimitMatrixOverride);
                SetObject(samplerObject, "previewMatrixBiomeOverride", originalMatrixOverride);
                samplerObject.ApplyModifiedPropertiesWithoutUndo();
                WorldProceduralScatterPreviewBuilder.RebuildProceduralScatterPreview();
            }

            string reportPath = Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath, ReportFileName);
            File.WriteAllText(reportPath, BuildMarkdownReport(activeScene.path, snapshots), Encoding.UTF8);
            AssetDatabase.Refresh();

            Debug.Log($"[WorldProceduralPatternBalanceReport] Wrote report to {reportPath}");
        }

        private static T FindInScene<T>(Scene scene, List<T> scratch) where T : Component
        {
            scratch.Clear();
            s_sceneRoots.Clear();
            if (s_sceneRoots.Capacity < scene.rootCount)
                s_sceneRoots.Capacity = scene.rootCount;

            scene.GetRootGameObjects(s_sceneRoots);

            for (int i = 0; i < s_sceneRoots.Count; i++)
            {
                GameObject root = s_sceneRoots[i];
                if (root == null)
                    continue;

                root.GetComponentsInChildren<T>(true, scratch);
                if (scratch.Count <= 0)
                    continue;

                T result = scratch[0];
                scratch.Clear();
                s_sceneRoots.Clear();
                return result;
            }

            scratch.Clear();
            s_sceneRoots.Clear();
            return null;
        }

        private static PatternSnapshot CaptureSnapshot(
            WorldProceduralPattern requestedPattern,
            WorldProceduralFieldSampler sampler,
            WorldProceduralScatterDirector scatterDirector)
        {
            SerializedObject samplerObject = new SerializedObject(sampler);
            SerializedObject scatterObject = new SerializedObject(scatterDirector);

            return new PatternSnapshot
            {
                requestedPattern = requestedPattern.ToString(),
                resolvedPattern = GetString(samplerObject, "_debugLastPattern"),
                resolvedBiomeProfile = GetString(scatterObject, "_debugBiomeMatrixProfile"),
                dominantMatrixBiome = GetString(scatterObject, "_debugSampleDominantMatrixBiome"),
                resolvedBiomeFamily = GetString(samplerObject, "_debugLastBiomeFamily"),
                resolvedZone = GetString(samplerObject, "_debugLastZone"),
                heightSource = GetString(samplerObject, "_debugLastHeightSource"),
                profileLabel = GetString(scatterObject, "_debugResolvedPatternProfile"),
                usedFallbackProfile = GetBool(scatterObject, "_debugUsedFallbackPatternProfile"),
                biomeContextLabel = GetString(scatterObject, "_debugResolvedBiomeContextProfile"),
                usedFallbackBiomeContext = GetBool(scatterObject, "_debugUsedFallbackBiomeContextProfile"),
                totalPlacements = scatterDirector.ActivePlacementCount,
                groundCount = GetInt(scatterObject, "_debugGroundPlacements"),
                clusterCount = GetInt(scatterObject, "_debugClusterPlacements"),
                structureCount = GetInt(scatterObject, "_debugStructurePlacements"),
                spawnCount = GetInt(scatterObject, "_debugSpawnPlacements"),
                targetGroundMin = GetInt(scatterObject, "_debugTargetGroundMin"),
                targetGroundMax = GetInt(scatterObject, "_debugTargetGroundMax"),
                targetClusterMin = GetInt(scatterObject, "_debugTargetClusterMin"),
                targetClusterMax = GetInt(scatterObject, "_debugTargetClusterMax"),
                targetStructureMin = GetInt(scatterObject, "_debugTargetStructureMin"),
                targetStructureMax = GetInt(scatterObject, "_debugTargetStructureMax"),
                targetSpawnMin = GetInt(scatterObject, "_debugTargetSpawnMin"),
                targetSpawnMax = GetInt(scatterObject, "_debugTargetSpawnMax"),
                groundTopFamily = GetString(scatterObject, "_debugGroundTopFamily"),
                clusterTopFamily = GetString(scatterObject, "_debugClusterTopFamily"),
                structureTopFamily = GetString(scatterObject, "_debugStructureTopFamily"),
                spawnTopFamily = GetString(scatterObject, "_debugSpawnTopFamily"),
                groundDominantFamily = GetString(scatterObject, "_debugGroundDominantFamily"),
                clusterDominantFamily = GetString(scatterObject, "_debugClusterDominantFamily"),
                structureDominantFamily = GetString(scatterObject, "_debugStructureDominantFamily"),
                spawnDominantFamily = GetString(scatterObject, "_debugSpawnDominantFamily"),
                dominantClusterAccent = GetString(scatterObject, "_debugClusterDominantAccentRole"),
                dominantStructureAccent = GetString(scatterObject, "_debugStructureDominantAccentRole"),
                clusterResourcePocketCount = GetInt(scatterObject, "_debugClusterResourcePocketCount"),
                clusterShelterPocketCount = GetInt(scatterObject, "_debugClusterShelterPocketCount"),
                clusterHazardPocketCount = GetInt(scatterObject, "_debugClusterHazardPocketCount"),
                clusterDebrisFieldCount = GetInt(scatterObject, "_debugClusterDebrisFieldCount"),
                clusterRockCoverCount = GetInt(scatterObject, "_debugClusterRockCoverCount"),
                structureNaturalLandmarkCount = GetInt(scatterObject, "_debugStructureNaturalLandmarkCount"),
                structureTechFragmentCount = GetInt(scatterObject, "_debugStructureTechFragmentCount"),
                structureCaveReadCount = GetInt(scatterObject, "_debugStructureCaveReadCount"),
                structureBiologicalSilhouetteCount = GetInt(scatterObject, "_debugStructureBiologicalSilhouetteCount"),
                spawnPassiveCount = GetInt(scatterObject, "_debugSpawnPassiveCount"),
                spawnPredatorCount = GetInt(scatterObject, "_debugSpawnPredatorCount")
            };
        }

        private static string BuildMarkdownReport(string scenePath, IReadOnlyList<PatternSnapshot> snapshots)
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("# Procedural Water Pattern Report");
            builder.AppendLine();
            builder.AppendLine($"- Scene: `{scenePath}`");
            builder.AppendLine($"- Generated: `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`");
            builder.AppendLine($"- Mode: `Forced preview pattern override (fallback-compatible)`");
            builder.AppendLine();

            for (int i = 0; i < snapshots.Count; i++)
            {
                PatternSnapshot snapshot = snapshots[i];
                string status = EvaluateStatus(snapshot, out string statusReason);
                builder.AppendLine($"## {snapshot.requestedPattern}");
                builder.AppendLine();
                builder.AppendLine($"- Resolved: `{snapshot.resolvedPattern}`");
                builder.AppendLine($"- Profile: `{snapshot.profileLabel}` | fallback used `{snapshot.usedFallbackProfile}`");
                builder.AppendLine($"- Matrix Biome: `{snapshot.resolvedBiomeProfile}`");
                builder.AppendLine($"- Sample Dominant Matrix Biome: `{snapshot.dominantMatrixBiome}`");
                builder.AppendLine($"- Biome: `{snapshot.resolvedBiomeFamily}`");
                builder.AppendLine($"- Biome Context: `{snapshot.biomeContextLabel}` | fallback used `{snapshot.usedFallbackBiomeContext}`");
                builder.AppendLine($"- Zone: `{snapshot.resolvedZone}`");
                builder.AppendLine($"- Height Source: `{snapshot.heightSource}`");
                builder.AppendLine($"- Status: `{status}` | {statusReason}");
                builder.AppendLine($"- Targets: ground `{snapshot.targetGroundMin}-{snapshot.targetGroundMax}` | cluster `{snapshot.targetClusterMin}-{snapshot.targetClusterMax}` | structure `{snapshot.targetStructureMin}-{snapshot.targetStructureMax}` | spawn `{snapshot.targetSpawnMin}-{snapshot.targetSpawnMax}`");
                builder.AppendLine($"- Placements: total `{snapshot.totalPlacements}` | ground `{snapshot.groundCount}` | cluster `{snapshot.clusterCount}` | structure `{snapshot.structureCount}` | spawn `{snapshot.spawnCount}`");
                builder.AppendLine($"- Top families: ground `{snapshot.groundTopFamily}` | cluster `{snapshot.clusterTopFamily}` | structure `{snapshot.structureTopFamily}` | spawn `{snapshot.spawnTopFamily}`");
                builder.AppendLine($"- Dominant families: ground `{snapshot.groundDominantFamily}` | cluster `{snapshot.clusterDominantFamily}` | structure `{snapshot.structureDominantFamily}` | spawn `{snapshot.spawnDominantFamily}`");
                builder.AppendLine($"- Accent roles: cluster `{snapshot.dominantClusterAccent}` | structure `{snapshot.dominantStructureAccent}`");
                builder.AppendLine($"- Cluster mix: resource `{snapshot.clusterResourcePocketCount}` | shelter `{snapshot.clusterShelterPocketCount}` | hazard `{snapshot.clusterHazardPocketCount}` | debris `{snapshot.clusterDebrisFieldCount}` | rock cover `{snapshot.clusterRockCoverCount}`");
                builder.AppendLine($"- Structure mix: natural `{snapshot.structureNaturalLandmarkCount}` | tech `{snapshot.structureTechFragmentCount}` | cave `{snapshot.structureCaveReadCount}` | bio `{snapshot.structureBiologicalSilhouetteCount}`");
                builder.AppendLine($"- Spawn mix: passive `{snapshot.spawnPassiveCount}` | predator `{snapshot.spawnPredatorCount}`");
                builder.AppendLine();
            }

            return builder.ToString();
        }

        private static string EvaluateStatus(PatternSnapshot snapshot, out string reason)
        {
            List<string> warnings = new List<string>(6);

            if (snapshot.usedFallbackProfile)
                warnings.Add("pattern profile fallback");

            if (snapshot.usedFallbackBiomeContext)
                warnings.Add("biome context fallback");

            ValidateRange(snapshot.groundCount, snapshot.targetGroundMin, snapshot.targetGroundMax, "ground", warnings);
            ValidateRange(snapshot.clusterCount, snapshot.targetClusterMin, snapshot.targetClusterMax, "cluster", warnings);
            ValidateRange(snapshot.structureCount, snapshot.targetStructureMin, snapshot.targetStructureMax, "structure", warnings);
            ValidateRange(snapshot.spawnCount, snapshot.targetSpawnMin, snapshot.targetSpawnMax, "spawn", warnings);

            if (snapshot.spawnCount < snapshot.targetSpawnMin)
                warnings.Add("spawn below target");

            if (HasContradictingDominantFamily(snapshot))
                warnings.Add("dominant family fights pattern intent");

            if (snapshot.structureCount > 0 && snapshot.structureDominantCountLikeSingular())
                warnings.Add("structure layer leans too hard on one object");

            reason = warnings.Count == 0 ? "targets look healthy" : string.Join(", ", warnings);
            return warnings.Count == 0 ? "PASS" : "WARN";
        }

        private static void ValidateRange(int value, int min, int max, string label, ICollection<string> warnings)
        {
            if (max <= 0)
                return;

            if (value < min || value > max)
                warnings.Add($"{label} outside target");
        }

        private static bool HasContradictingDominantFamily(PatternSnapshot snapshot)
        {
            string structure = snapshot.structureDominantFamily ?? string.Empty;
            string cluster = snapshot.clusterDominantFamily ?? string.Empty;

            switch (snapshot.requestedPattern)
            {
                case nameof(WorldProceduralPattern.FertileShallows):
                    return ContainsAny(structure, "Service Scar", "Route Power", "Ruin") ||
                           ContainsAny(cluster, "Debris");
                case nameof(WorldProceduralPattern.ReefNavigation):
                    return ContainsAny(structure, "Route Power", "Service Scar", "Ruin Megastructure") ||
                           ContainsAny(cluster, "Debris");
                case nameof(WorldProceduralPattern.SedimentResources):
                    return ContainsAny(structure, "Plant Giant");
                case nameof(WorldProceduralPattern.IndustrialService):
                    return !ContainsAny(structure, "Route Power", "Service Scar", "Ruin", "Cave Entrance");
                case nameof(WorldProceduralPattern.BrineToxic):
                    bool caveDrivenButStillTechRead =
                        ContainsAny(structure, "Cave Entrance") &&
                        snapshot.structureTechFragmentCount >= 3 &&
                        snapshot.structureTechFragmentCount >= snapshot.structureCaveReadCount;
                    return !(ContainsAny(structure, "Service Scar", "Route Power", "Ruin") || caveDrivenButStillTechRead);
                case nameof(WorldProceduralPattern.VolcanicPressure):
                    return !ContainsAny(structure, "Cave Entrance", "Rock Arch", "Landmark Spire");
                case nameof(WorldProceduralPattern.RiftHazard):
                    return snapshot.spawnPredatorCount < snapshot.spawnPassiveCount;
                case nameof(WorldProceduralPattern.LandmarkCorridor):
                    return !ContainsAny(structure, "Rock Arch", "Landmark Spire", "Cave Entrance");
                default:
                    return false;
            }
        }

        private static bool ContainsAny(string value, params string[] fragments)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            for (int i = 0; i < fragments.Length; i++)
            {
                if (value.IndexOf(fragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static bool GetBool(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null && property.boolValue;
        }

        private static int GetInt(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null ? property.intValue : 0;
        }

        private static string GetString(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null ? property.stringValue : "None";
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

        private static UnityEngine.Object GetObject(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null ? property.objectReferenceValue : null;
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

        private sealed class PatternSnapshot
        {
            public string requestedPattern;
            public string resolvedPattern;
            public string resolvedBiomeProfile;
            public string dominantMatrixBiome;
            public string resolvedBiomeFamily;
            public string resolvedZone;
            public string heightSource;
            public string profileLabel;
            public bool usedFallbackProfile;
            public string biomeContextLabel;
            public bool usedFallbackBiomeContext;
            public int totalPlacements;
            public int groundCount;
            public int clusterCount;
            public int structureCount;
            public int spawnCount;
            public int targetGroundMin;
            public int targetGroundMax;
            public int targetClusterMin;
            public int targetClusterMax;
            public int targetStructureMin;
            public int targetStructureMax;
            public int targetSpawnMin;
            public int targetSpawnMax;
            public string groundTopFamily;
            public string clusterTopFamily;
            public string structureTopFamily;
            public string spawnTopFamily;
            public string groundDominantFamily;
            public string clusterDominantFamily;
            public string structureDominantFamily;
            public string spawnDominantFamily;
            public string dominantClusterAccent;
            public string dominantStructureAccent;
            public int clusterResourcePocketCount;
            public int clusterShelterPocketCount;
            public int clusterHazardPocketCount;
            public int clusterDebrisFieldCount;
            public int clusterRockCoverCount;
            public int structureNaturalLandmarkCount;
            public int structureTechFragmentCount;
            public int structureCaveReadCount;
            public int structureBiologicalSilhouetteCount;
            public int spawnPassiveCount;
            public int spawnPredatorCount;

            public bool structureDominantCountLikeSingular()
            {
                return structureCount > 0 &&
                       (structureNaturalLandmarkCount >= structureCount - 1 ||
                        structureTechFragmentCount >= structureCount - 1 ||
                        structureCaveReadCount >= structureCount - 1 ||
                        structureBiologicalSilhouetteCount >= structureCount - 1);
            }
        }
    }
}
