using System.Collections.Generic;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Validation
{
    public static class WorldPopulationValidator
    {
        [MenuItem("Hecton8/Validation/Validate World Population", priority = 242)]
        public static void Validate()
        {
            List<string> issues = new List<string>(128);
            List<string> warnings = new List<string>(128);

            WorldPopulationDirector populationDirector = FindSceneObjectIncludingInactive<WorldPopulationDirector>();
            WorldContentDirector contentDirector = FindSceneObjectIncludingInactive<WorldContentDirector>();
            WorldZoneDirector zoneDirector = FindSceneObjectIncludingInactive<WorldZoneDirector>();
            WorldContentSocket[] sockets = FindSceneObjectsIncludingInactive<WorldContentSocket>();

            Dictionary<WorldContentSocket.ContentKind, int> socketCounts = new Dictionary<WorldContentSocket.ContentKind, int>(8);
            Dictionary<WorldContentSocket.ContentKind, int> uncoveredCounts = new Dictionary<WorldContentSocket.ContentKind, int>(8);

            if (populationDirector == null)
                issues.Add("Active scene is missing WorldPopulationDirector.");

            if (contentDirector == null)
                issues.Add("Active scene is missing WorldContentDirector.");

            if (zoneDirector == null)
                warnings.Add("Active scene is missing WorldZoneDirector. Population diagnostics cannot track the live zone owner.");

            ValidateDirectorReferences(populationDirector, warnings);

            List<WorldPopulationRule> rules = ExtractRules(populationDirector, issues);
            ValidateRules(rules, warnings);

            int uncoveredSockets = 0;
            int weakSpatialSockets = 0;
            int socketsOutsideZones = 0;
            int rulesWithoutCoverage = 0;

            Dictionary<WorldPopulationRule, int> coverageByRule = new Dictionary<WorldPopulationRule, int>(rules.Count);
            for (int i = 0; i < rules.Count; i++)
                coverageByRule[rules[i]] = 0;

            for (int i = 0; i < sockets.Length; i++)
            {
                WorldContentSocket socket = sockets[i];
                if (socket == null)
                    continue;

                CountSocket(socketCounts, socket.Kind);

                WorldZoneAnchor zone = socket.GetZoneAnchor();
                if (zone == null)
                {
                    socketsOutsideZones++;
                    issues.Add($"Socket '{socket.name}' is not parented under any WorldZoneAnchor.");
                    continue;
                }

                WorldPopulationRule strongestRule = FindStrongestMatchingRule(rules, zone, socket);
                if (strongestRule == null)
                {
                    uncoveredSockets++;
                    CountSocket(uncoveredCounts, socket.Kind);
                    issues.Add(
                        $"Socket '{socket.name}' in zone '{zone.ZoneLabel}' kind='{socket.Kind}' has no matching WorldPopulationRule.");
                    continue;
                }

                coverageByRule[strongestRule] = coverageByRule[strongestRule] + 1;

                if (IsWeakSpatialCoverage(strongestRule, zone, socket))
                {
                    weakSpatialSockets++;
                    warnings.Add(
                        $"Socket '{socket.name}' matched rule '{strongestRule.ruleLabel}' but still has weak spatial guidance.");
                }
            }

            for (int i = 0; i < rules.Count; i++)
            {
                WorldPopulationRule rule = rules[i];
                if (rule == null)
                    continue;

                if (coverageByRule.TryGetValue(rule, out int coverage) && coverage > 0)
                    continue;

                rulesWithoutCoverage++;
                warnings.Add(
                    $"Rule '{rule.ruleLabel}' currently covers no sockets in the active scene.");
            }

            LogIssues("[WorldPopulationValidation]", issues, true);
            LogIssues("[WorldPopulationValidation]", warnings, false);

            string coverageSummary = BuildKindSummary(socketCounts, uncoveredCounts);
            if (issues.Count == 0)
            {
                Debug.Log(
                    $"[WorldPopulationValidation] PASS sockets={sockets.Length} uncovered={uncoveredSockets} weakSpatial={weakSpatialSockets} rules={rules.Count} rulesWithoutCoverage={rulesWithoutCoverage} socketsOutsideZones={socketsOutsideZones} coverage={coverageSummary}");
                return;
            }

            Debug.LogWarning(
                $"[WorldPopulationValidation] FAIL issues={issues.Count} warnings={warnings.Count} sockets={sockets.Length} uncovered={uncoveredSockets} weakSpatial={weakSpatialSockets} rules={rules.Count} rulesWithoutCoverage={rulesWithoutCoverage} socketsOutsideZones={socketsOutsideZones} coverage={coverageSummary}");
        }

        private static void ValidateDirectorReferences(WorldPopulationDirector populationDirector, List<string> warnings)
        {
            if (populationDirector == null)
                return;

            SerializedObject serializedObject = new SerializedObject(populationDirector);
            SerializedProperty playerTransform = serializedObject.FindProperty("playerTransform");
            SerializedProperty worldZoneDirector = serializedObject.FindProperty("worldZoneDirector");
            SerializedProperty worldContentDirector = serializedObject.FindProperty("worldContentDirector");

            if (playerTransform == null || playerTransform.objectReferenceValue == null)
                warnings.Add("WorldPopulationDirector is relying on runtime auto-resolve for playerTransform.");

            if (worldZoneDirector == null || worldZoneDirector.objectReferenceValue == null)
                warnings.Add("WorldPopulationDirector is relying on runtime auto-resolve for worldZoneDirector.");

            if (worldContentDirector == null || worldContentDirector.objectReferenceValue == null)
                warnings.Add("WorldPopulationDirector is relying on runtime auto-resolve for worldContentDirector.");
        }

        private static List<WorldPopulationRule> ExtractRules(WorldPopulationDirector populationDirector, List<string> issues)
        {
            List<WorldPopulationRule> rules = new List<WorldPopulationRule>(32);
            if (populationDirector == null)
                return rules;

            SerializedObject serializedObject = new SerializedObject(populationDirector);
            SerializedProperty rulesProperty = serializedObject.FindProperty("rules");
            if (rulesProperty == null || rulesProperty.arraySize <= 0)
            {
                issues.Add("WorldPopulationDirector has no rules assigned.");
                return rules;
            }

            for (int i = 0; i < rulesProperty.arraySize; i++)
            {
                SerializedProperty entry = rulesProperty.GetArrayElementAtIndex(i);
                WorldPopulationRule rule = entry != null ? entry.objectReferenceValue as WorldPopulationRule : null;
                if (rule == null)
                {
                    issues.Add($"WorldPopulationDirector rules[{i}] is null.");
                    continue;
                }

                rules.Add(rule);
            }

            return rules;
        }

        private static void ValidateRules(List<WorldPopulationRule> rules, List<string> warnings)
        {
            for (int i = 0; i < rules.Count; i++)
            {
                WorldPopulationRule rule = rules[i];
                if (rule == null)
                    continue;

                if (string.IsNullOrWhiteSpace(rule.ruleId))
                    warnings.Add($"Rule '{rule.name}' has an empty ruleId.");

                if (string.IsNullOrWhiteSpace(rule.ruleLabel))
                    warnings.Add($"Rule asset '{rule.name}' has an empty ruleLabel.");

                if (string.IsNullOrWhiteSpace(rule.prefabFamily))
                    warnings.Add($"Rule '{rule.ruleLabel}' has no prefabFamily. Future population family is still undefined.");

                if (!string.IsNullOrWhiteSpace(rule.prefabFamily) && rule.familyProfile == null)
                    warnings.Add($"Rule '{rule.ruleLabel}' has prefabFamily but no familyProfile asset.");

                if (string.IsNullOrWhiteSpace(rule.gameplayPurpose))
                    warnings.Add($"Rule '{rule.ruleLabel}' has an empty gameplayPurpose.");

                if (rule.suggestedClusterCount <= 0)
                    warnings.Add($"Rule '{rule.ruleLabel}' has non-positive suggestedClusterCount.");

                if (rule.suggestedMinCount <= 0)
                    warnings.Add($"Rule '{rule.ruleLabel}' has non-positive suggestedMinCount.");

                if (rule.suggestedMaxCount < rule.suggestedMinCount)
                    warnings.Add($"Rule '{rule.ruleLabel}' has suggestedMaxCount below suggestedMinCount.");

                if (rule.preferredBiomeFamilies == null)
                    continue;

                for (int familyIndex = 0; familyIndex < rule.preferredBiomeFamilies.Length; familyIndex++)
                {
                    if (rule.preferredBiomeFamilies[familyIndex] != null)
                        continue;

                    warnings.Add($"Rule '{rule.ruleLabel}' has an empty preferredBiomeFamilies slot at index {familyIndex}.");
                }
            }
        }

        private static WorldPopulationRule FindStrongestMatchingRule(
            List<WorldPopulationRule> rules,
            WorldZoneAnchor zone,
            WorldContentSocket socket)
        {
            WorldPopulationRule bestRule = null;
            float bestWeight = float.MinValue;

            for (int i = 0; i < rules.Count; i++)
            {
                WorldPopulationRule rule = rules[i];
                if (rule == null || !rule.Matches(zone, socket))
                    continue;

                float weight = rule.GetEffectiveDensityWeight(zone, socket);
                if (bestRule != null && weight <= bestWeight)
                    continue;

                bestRule = rule;
                bestWeight = weight;
            }

            return bestRule;
        }

        private static bool IsWeakSpatialCoverage(
            WorldPopulationRule rule,
            WorldZoneAnchor zone,
            WorldContentSocket socket)
        {
            if (rule == null || socket == null)
                return false;

            string spatialRole = rule.BuildSpatialRole(zone, socket);
            string spatialReason = rule.BuildSpatialRoleReason(zone, socket);

            return string.IsNullOrWhiteSpace(spatialRole)
                || string.Equals(spatialRole, "Generic Point", System.StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(spatialReason)
                || string.Equals(
                    spatialReason,
                    "Socket follows the biome's default spatial rhythm.",
                    System.StringComparison.OrdinalIgnoreCase);
        }

        private static void LogIssues(string prefix, List<string> issues, bool asError)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (asError)
                    Debug.LogError($"{prefix} {issues[i]}");
                else
                    Debug.LogWarning($"{prefix} {issues[i]}");
            }
        }

        private static void CountSocket(
            Dictionary<WorldContentSocket.ContentKind, int> counts,
            WorldContentSocket.ContentKind kind)
        {
            if (counts.TryGetValue(kind, out int current))
                counts[kind] = current + 1;
            else
                counts.Add(kind, 1);
        }

        private static string BuildKindSummary(
            Dictionary<WorldContentSocket.ContentKind, int> socketCounts,
            Dictionary<WorldContentSocket.ContentKind, int> uncoveredCounts)
        {
            List<string> parts = new List<string>(socketCounts.Count);
            foreach (KeyValuePair<WorldContentSocket.ContentKind, int> entry in socketCounts)
            {
                uncoveredCounts.TryGetValue(entry.Key, out int uncovered);
                int covered = entry.Value - uncovered;
                parts.Add($"{entry.Key}:{covered}/{entry.Value}");
            }

            return parts.Count > 0 ? string.Join(", ", parts) : "none";
        }

        private static T FindSceneObjectIncludingInactive<T>() where T : Component
        {
            T[] candidates = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < candidates.Length; i++)
            {
                T candidate = candidates[i];
                if (candidate == null || candidate.gameObject == null || !candidate.gameObject.scene.IsValid())
                    continue;

                return candidate;
            }

            return null;
        }

        private static T[] FindSceneObjectsIncludingInactive<T>() where T : Component
        {
            T[] candidates = Resources.FindObjectsOfTypeAll<T>();
            List<T> results = new List<T>(candidates.Length);

            for (int i = 0; i < candidates.Length; i++)
            {
                T candidate = candidates[i];
                if (candidate == null || candidate.gameObject == null || !candidate.gameObject.scene.IsValid())
                    continue;

                results.Add(candidate);
            }

            return results.ToArray();
        }
    }
}
