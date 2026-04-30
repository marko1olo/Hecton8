using System.Collections.Generic;
using Hecton8.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools
{
    public static class ScanIntelValidator
    {
        [MenuItem("Hecton/Validation/Validate Scan Intel", priority = 132)]
        public static void Validate()
        {
            List<string> issues = new List<string>(16);
            Scene scene = SceneManager.GetActiveScene();

            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[ScanIntelValidation] FAIL active scene is not loaded.");
                return;
            }

            ValidatePlayerScanLog(issues);
            ValidateSceneScannables(issues);
            ValidateStarterTitaniumPoi(issues);

            if (issues.Count > 0)
            {
                Debug.LogError($"[ScanIntelValidation] FAIL {issues.Count} issue(s):\n - {string.Join("\n - ", issues)}");
                return;
            }

            Debug.Log("[ScanIntelValidation] PASS no issues found.");
        }

        private static void ValidatePlayerScanLog(List<string> issues)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                issues.Add("Scene player tagged 'Player' was not found.");
                return;
            }

            if (player.GetComponent<ScanLogSystem>() == null)
                issues.Add("Player is missing ScanLogSystem.");
        }

        private static void ValidateSceneScannables(List<string> issues)
        {
            ScannableTarget[] targets = Object.FindObjectsByType<ScannableTarget>(FindObjectsInactive.Exclude);
            if (targets == null || targets.Length == 0)
            {
                issues.Add("No ScannableTarget components found in the active scene.");
                return;
            }

            HashSet<string> uniqueIds = new HashSet<string>();
            for (int i = 0; i < targets.Length; i++)
            {
                ScannableTarget target = targets[i];
                if (target == null)
                    continue;

                string entryId = target.EntryId;
                if (string.IsNullOrWhiteSpace(entryId))
                {
                    issues.Add($"ScannableTarget on '{target.gameObject.name}' has an empty entryId.");
                    continue;
                }

                if (!uniqueIds.Add(entryId))
                    issues.Add($"Duplicate scan entry id '{entryId}' detected in scene.");

                if (string.IsNullOrWhiteSpace(target.EntryTitle))
                    issues.Add($"ScannableTarget '{entryId}' has an empty title.");

                if (string.IsNullOrWhiteSpace(target.EntryCategory))
                    issues.Add($"ScannableTarget '{entryId}' has an empty category.");

                if (string.IsNullOrWhiteSpace(target.EntrySummary))
                    issues.Add($"ScannableTarget '{entryId}' has an empty summary.");
            }
        }

        private static void ValidateStarterTitaniumPoi(List<string> issues)
        {
            GameObject starterTitanium = GameObject.Find("--- GAMEPLAY ---/Item_Titanium");
            if (starterTitanium == null)
                return;

            if (starterTitanium.GetComponent<ScannableTarget>() == null)
                issues.Add("Starter Item_Titanium is missing authored ScannableTarget.");
        }
    }
}
