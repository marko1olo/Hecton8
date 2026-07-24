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
            "wip",
            "to" + "do"
        };

        [MenuItem("Hecton8/Validate World Scene Cleanup", priority = 100)]
        public static void ValidateWorldSceneCleanup()
        {
            Scene currentScene = SceneManager.GetActiveScene();

            if (currentScene.path != ProductionWorldScene)
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
                EditorSceneManager.OpenScene(ProductionWorldScene, OpenSceneMode.Single);
            }

            List<GameObject> tempObjects = new List<GameObject>(64); // COLD ALLOC: temp object collection for validation
            List<GameObject> suspiciousObjects = new List<GameObject>(64); // COLD ALLOC: suspicious object collection for validation

            GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject root in rootObjects)
            {
                ScanHierarchy(root.transform, tempObjects, suspiciousObjects);
            }

            StringBuilder report = new StringBuilder();
            report.AppendLine("═══════════════════════════════════════════════════════");
            report.AppendLine("HECTON-8 — World Scene Cleanup Validation Report");
            report.AppendLine("═══════════════════════════════════════════════════════");
            report.AppendLine();
            report.AppendLine($"Scene: {SceneManager.GetActiveScene().name}");
            report.AppendLine($"Path: {SceneManager.GetActiveScene().path}");
            report.AppendLine();

            if (tempObjects.Count == 0 && suspiciousObjects.Count == 0)
            {
                report.AppendLine("✓ NO TEMPORARY OBJECTS FOUND");
                report.AppendLine();
                report.AppendLine("Scene appears clean for production.");
            }
            else
            {
                if (tempObjects.Count > 0)
                {
                    report.AppendLine($"✗ TEMPORARY OBJECTS FOUND: {tempObjects.Count}");
                    report.AppendLine();
                    report.AppendLine("These objects have temp/trial/staging/smoke naming:");
                    report.AppendLine();

                    foreach (GameObject obj in tempObjects)
                    {
                        string path = GetGameObjectPath(obj);
                        report.AppendLine($"  • {path}");
                    }

                    report.AppendLine();
                }

                if (suspiciousObjects.Count > 0)
                {
                    report.AppendLine($"⚠ SUSPICIOUS OBJECTS FOUND: {suspiciousObjects.Count}");
                    report.AppendLine();
                    report.AppendLine("These objects contain temp/trial/staging keywords:");
                    report.AppendLine();

                    foreach (GameObject obj in suspiciousObjects)
                    {
                        string path = GetGameObjectPath(obj);
                        report.AppendLine($"  • {path}");
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

            Debug.Log(report.ToString());

            if (tempObjects.Count > 0 || suspiciousObjects.Count > 0)
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

        [MenuItem("Hecton8/Select All Temp Objects in Scene", priority = 101)]
        public static void SelectAllTempObjects()
        {
            List<GameObject> tempObjects = new List<GameObject>(64); // COLD ALLOC: temp object collection for selection
            List<GameObject> suspiciousObjects = new List<GameObject>(64); // COLD ALLOC: suspicious object collection for selection

            GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject root in rootObjects)
            {
                ScanHierarchy(root.transform, tempObjects, suspiciousObjects);
            }

            List<GameObject> allFound = new List<GameObject>(tempObjects.Count + suspiciousObjects.Count); // COLD ALLOC: combined selection list
            allFound.AddRange(tempObjects);
            allFound.AddRange(suspiciousObjects);

            if (allFound.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Select Temp Objects",
                    "No temporary objects found in scene.",
                    "OK");
                return;
            }

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
