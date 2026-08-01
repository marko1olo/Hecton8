using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Text;

namespace Hecton8.Editor
{
    /// <summary>
    /// Editor tool to identify and document temporary/trial/smoke objects in production scenes.
    /// Helps clean up 02_HECTON_WORLD for shipping-ready state.
    /// </summary>
    public static class WorldSceneCleanupValidator
    {
        private const string ProductionWorldScene = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";

        private static readonly string[] TempPrefixes = 
        {
            "__TEMP",
            "_TEMP",
            "TEMP_",
            "Temp_",
            "temp_",
            "Trial_",
            "TRIAL_",
            "_Trial",
            "Staging_",
            "STAGING_",
            "_Staging",
            "Smoke_",
            "SMOKE_",
            "_Smoke",
            "Test_",
            "TEST_",
            "_Test",
            "Debug_",
            "DEBUG_",
            "_Debug",
            "Prototype_",
            "PROTOTYPE_",
            "_Prototype"
        };

        private static readonly string[] TempKeywords =
        {
            "trial",
            "staging",
            "smoke",
            "temp",
            "test",
            "debug",
            "prototype",
            "preview",
            "wip"
        };

        [MenuItem("Hecton8/Validate World Scene Cleanup", priority = 100)]
        public static void ValidateWorldSceneCleanup()
        {
            // Compile-proof / CI path: -executeMethod must never open DisplayDialog
            // (batchmode aborts with "This should not be called in batch mode").
            bool batch = Application.isBatchMode;
            Scene currentScene = SceneManager.GetActiveScene();

            if (currentScene.path != ProductionWorldScene)
            {
                if (!batch)
                {
                    if (!EditorUtility.DisplayDialog(
                        "Load Production World Scene?",
                        $"Current scene: {currentScene.name}\n\nLoad {ProductionWorldScene} for validation?",
                        "Load",
                        "Cancel"))
                    {
                        return;
                    }

                    EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                }

                currentScene = EditorSceneManager.OpenScene(ProductionWorldScene, OpenSceneMode.Single);
            }

            if (!currentScene.IsValid() || !currentScene.isLoaded || currentScene.path != ProductionWorldScene)
            {
                string missing = "[WorldSceneCleanupValidator] FAIL: 02_HECTON_WORLD.unity not found or not loaded at " + ProductionWorldScene;
                Debug.LogError(missing);
                if (!batch)
                    EditorUtility.DisplayDialog("World Scene Cleanup Validation", missing, "OK");
                if (batch)
                    EditorApplication.Exit(1);
                return;
            }

            List<GameObject> tempObjects = new List<GameObject>(64); // COLD ALLOC: temp object collection for validation
            List<GameObject> suspiciousObjects = new List<GameObject>(64); // COLD ALLOC: suspicious object collection for validation

            GameObject[] rootObjects = currentScene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
            {
                GameObject root = rootObjects[rootIndex];
                if (root != null)
                    ScanHierarchy(root.transform, tempObjects, suspiciousObjects);
            }

            StringBuilder report = new StringBuilder(4096);
            report.AppendLine("═══════════════════════════════════════════════════════");
            report.AppendLine("HECTON-8 — World Scene Cleanup Validation Report");
            report.AppendLine("═══════════════════════════════════════════════════════");
            report.AppendLine();

            report.Append("Scene: ");
            report.AppendLine(currentScene.name);
            report.Append("Path: ");
            report.AppendLine(currentScene.path);
            report.AppendLine();

            bool passed = tempObjects.Count == 0 && suspiciousObjects.Count == 0;
            if (passed)
            {
                report.AppendLine("✓ NO TEMPORARY OBJECTS FOUND");
                report.AppendLine();
                report.AppendLine("Scene appears clean for production.");
            }
            else
            {
                if (tempObjects.Count > 0)
                {
                    report.Append("✗ TEMPORARY OBJECTS FOUND: ");
                    report.Append(tempObjects.Count);
                    report.AppendLine();
                    report.AppendLine();
                    report.AppendLine("These objects have temp/trial/staging/smoke naming:");
                    report.AppendLine();

                    for (int i = 0; i < tempObjects.Count; i++)
                    {
                        report.Append("  • ");
                        report.AppendLine(GetGameObjectPath(tempObjects[i]));
                    }

                    report.AppendLine();
                }

                if (suspiciousObjects.Count > 0)
                {
                    report.Append("⚠ SUSPICIOUS OBJECTS FOUND: ");
                    report.Append(suspiciousObjects.Count);
                    report.AppendLine();
                    report.AppendLine();
                    report.AppendLine("These objects contain temp/trial/staging keywords:");
                    report.AppendLine();

                    for (int i = 0; i < suspiciousObjects.Count; i++)
                    {
                        report.Append("  • ");
                        report.AppendLine(GetGameObjectPath(suspiciousObjects[i]));
                    }

                    report.AppendLine();
                }

                report.AppendLine("═══════════════════════════════════════════════════════");
                report.AppendLine("RECOMMENDED ACTIONS:");
                report.AppendLine("═══════════════════════════════════════════════════════");
                report.AppendLine();
                report.AppendLine("1. Review each object listed above");
                report.AppendLine("2. Delete objects that are no longer needed");
                report.AppendLine("3. Move debug/test objects to sandbox scene");
                report.AppendLine("4. Rename objects to remove temp/trial/staging prefixes");
                report.AppendLine("5. Add [DEBUG_ONLY] tag to objects needed for development");
                report.AppendLine();
                report.AppendLine("Production scenes should not contain temporary objects.");
            }

            report.AppendLine("═══════════════════════════════════════════════════════");
            report.Append("RESULT: ");
            report.AppendLine(passed ? "PASS" : "FAIL");
            string reportText = report.ToString();

            // Soft FAIL is LogError so CI greps can see it; batchmode still exits 0
            // (scene cleanliness is audit, not a compile gate).
            if (passed)
                Debug.Log(reportText);
            else
                Debug.LogError(reportText);

            if (!batch)
            {
                if (!passed)
                {
                    EditorUtility.DisplayDialog(
                        "World Scene Cleanup Validation",
                        $"Found {tempObjects.Count} temporary objects and {suspiciousObjects.Count} suspicious objects.\n\nSee Console for full report.",
                        "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "World Scene Cleanup Validation",
                        "✓ Scene is clean!\n\nNo temporary objects found.",
                        "OK");
                }
            }
            // batchmode: rely on -quit; do not EditorApplication.Exit(1) on soft FAIL.
        }

        [MenuItem("Hecton8/Select All Temp Objects in Scene", priority = 101)]
        public static void SelectAllTempObjects()
        {
            // Interactive Selection-only tool; still must not abort batchmode if invoked.
            bool batch = Application.isBatchMode;
            List<GameObject> tempObjects = new List<GameObject>(64); // COLD ALLOC: temp object collection for selection
            List<GameObject> suspiciousObjects = new List<GameObject>(64); // COLD ALLOC: suspicious object collection for selection

            GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
            {
                GameObject root = rootObjects[rootIndex];
                if (root != null)
                    ScanHierarchy(root.transform, tempObjects, suspiciousObjects);
            }

            List<GameObject> allFound = new List<GameObject>(tempObjects.Count + suspiciousObjects.Count); // COLD ALLOC: combined selection list
            allFound.AddRange(tempObjects);
            allFound.AddRange(suspiciousObjects);

            if (allFound.Count == 0)
            {
                Debug.Log("[WorldSceneCleanupValidator] No temporary objects found in scene.");
                if (!batch)
                {
                    EditorUtility.DisplayDialog(
                        "Select Temp Objects",
                        "No temporary objects found in scene.",
                        "OK");
                }
                return;
            }

            if (!batch)
                Selection.objects = allFound.ToArray();
            Debug.Log($"Selected {allFound.Count} temporary/suspicious objects in scene.");
        }


        private static void ScanHierarchy(Transform root, List<GameObject> tempObjects, List<GameObject> suspiciousObjects)
        {
            if (root == null)
                return;

            string name = root.name;
            bool isTemp = false;
            bool isSuspicious = false;

            // Check for temp prefixes
            foreach (string prefix in TempPrefixes)
            {
                if (name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    isTemp = true;
                    break;
                }
            }

            // Check for temp keywords (case-insensitive)
            if (!isTemp)
            {
                string lowerName = name.ToLowerInvariant();
                foreach (string keyword in TempKeywords)
                {
                    if (lowerName.Contains(keyword))
                    {
                        isSuspicious = true;
                        break;
                    }
                }
            }

            if (isTemp)
                tempObjects.Add(root.gameObject);
            else if (isSuspicious)
                suspiciousObjects.Add(root.gameObject);

            // Recurse to children
            for (int i = 0; i < root.childCount; i++)
            {
                ScanHierarchy(root.GetChild(i), tempObjects, suspiciousObjects);
            }
        }

        private static string GetGameObjectPath(GameObject obj)
        {
            if (obj == null)
                return string.Empty;

            StringBuilder path = new StringBuilder();
            Transform current = obj.transform;

            while (current != null)
            {
                if (path.Length > 0)
                    path.Insert(0, "/");

                path.Insert(0, current.name);
                current = current.parent;
            }

            return path.ToString();
        }
    }
}
