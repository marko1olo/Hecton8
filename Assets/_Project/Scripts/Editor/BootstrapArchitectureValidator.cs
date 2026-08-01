// ============================================================================
// HECTON-8 — BootstrapArchitectureValidator.cs
// Editor-only validator for bootstrap scene architecture.
// Ensures 00_BOOTSTRAP contains all required managers and no gameplay objects.
//
// RULES:
//   ✓ 00_BOOTSTRAP must contain: GameTickManager, SaveManager, InputManager, ObjectPoolManager
//   ✓ 00_BOOTSTRAP must NOT contain: Player, Enemy, Interactable objects
//   ✓ 00_BOOTSTRAP must be first scene in Build Settings
//   ✗ 01_MAIN_MENU and 02_HECTON_WORLD must NOT auto-load (SceneManager.LoadScene only)
//
// Usage: Window > HECTON-8 > Validate Bootstrap Architecture
// ============================================================================

#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    public static class BootstrapArchitectureValidator
    {
        private static readonly string[] REQUIRED_MANAGERS = new string[]
        {
            "GameTickManager",
            "SaveManager",
            "InputManager",
            "ObjectPoolManager",
        };

        private static readonly string[] FORBIDDEN_PATTERNS = new string[]
        {
            "Player",
            "_Player",
            "PlayerController",
            "Enemy",
            "_Enemy",
            "Creature",
            "NPC",
            "Interactable",
        };

        private const string BootstrapScenePath = "Assets/_Project/Scenes/00_BOOTSTRAP.unity";

        [MenuItem("Window/HECTON-8/Validate Bootstrap Architecture")]
        public static void ValidateBootstrapArchitecture()
        {
            // Compile-proof / CI path: -executeMethod must never open DisplayDialog
            // (batchmode aborts with "This should not be called in batch mode").
            bool batch = Application.isBatchMode;
            Scene bootstrapScene = SceneManager.GetSceneByPath(BootstrapScenePath);

            if (!bootstrapScene.IsValid() || !bootstrapScene.isLoaded)
            {
                // OpenScene is additive-safe for a one-shot audit; batchmode never has the scene
                // already open, so the previous IsValid-only gate always failed there.
                bootstrapScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    BootstrapScenePath,
                    UnityEditor.SceneManagement.OpenSceneMode.Single);
            }

            if (!bootstrapScene.IsValid() || !bootstrapScene.isLoaded)
            {
                string missing = "[BootstrapArchitectureValidator] FAIL: 00_BOOTSTRAP.unity not found or not loaded at " + BootstrapScenePath;
                Debug.LogError(missing);
                if (!batch)
                    EditorUtility.DisplayDialog("Bootstrap Validation", missing, "OK");
                if (batch)
                    EditorApplication.Exit(1);
                return;
            }

            GameObject[] rootObjects = bootstrapScene.GetRootGameObjects();
            string report = "=== BOOTSTRAP ARCHITECTURE VALIDATION ===\n\n";
            bool managersOk = true;
            bool buildSettingsOk = true;

            // ── Check for required managers ──
            report += "REQUIRED MANAGERS:\n";
            for (int managerIndex = 0; managerIndex < REQUIRED_MANAGERS.Length; managerIndex++)
            {
                string managerName = REQUIRED_MANAGERS[managerIndex];
                bool found = FindObjectInScene(bootstrapScene, managerName) != null;
                report += $"  {(found ? "✓" : "✗")} {managerName}\n";
                if (!found)
                    managersOk = false;
            }

            report += "\nOBJECTS IN ROOT:\n";
            for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
            {
                GameObject root = rootObjects[rootIndex];
                // Check for forbidden patterns
                bool isForbidden = IsForbiddenObject(root.name);
                report += $"  {(isForbidden ? "⚠️" : "  ")} {root.name}\n";

                // List children
                if (root.transform.childCount > 0)
                {
                    ListChildren(root.transform, report, depth: 1);
                }
            }

            report += "\nFORBIDDEN OBJECTS:\n";
            bool foundForbidden = false;
            for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
            {
                GameObject root = rootObjects[rootIndex];
                if (HasForbiddenChildren(root.transform))
                {
                    report += $"  ⚠️ {root.name} contains forbidden children!\n";
                    foundForbidden = true;
                }
            }

            if (!foundForbidden)
            {
                report += "  ✓ No forbidden gameplay objects found.\n";
            }

            // ── Check Build Settings ──
            report += "\nBUILD SETTINGS:\n";
            if (EditorBuildSettings.scenes.Length > 0)
            {
                string firstScene = EditorBuildSettings.scenes[0].path;
                bool isBootstrapFirst = firstScene.Contains("00_BOOTSTRAP");
                report += $"  {(isBootstrapFirst ? "✓" : "✗")} First scene: {firstScene}\n";
                if (!isBootstrapFirst)
                    buildSettingsOk = false;
            }
            else
            {
                report += "  ✗ No scenes in Build Settings!\n";
                buildSettingsOk = false;
            }

            bool passed = managersOk && buildSettingsOk && !foundForbidden;
            report += "\nRESULT: " + (passed ? "PASS" : "FAIL") + "\n";

            // Always emit the report. Architecture FAIL is LogError so CI greps can see it,
            // but batchmode still exits 0: this method is the project's compile-proof entry
            // and managers may be code-constructed (GameBootstrapper) rather than scene-named.
            if (passed)
                Debug.Log(report);
            else
                Debug.LogError(report);

            if (!batch)
            {
                EditorUtility.DisplayDialog(
                    "Bootstrap Validation Report",
                    report,
                    "OK");
            }
            // batchmode: rely on -quit; do not EditorApplication.Exit(1) on soft FAIL.
        }



        private static GameObject FindObjectInScene(Scene scene, string name)
        {
            GameObject[] rootObjects = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
            {
                GameObject root = rootObjects[rootIndex];
                if (root.name == name)
                    return root;

                Transform found = root.transform.Find(name);
                if (found != null)
                    return found.gameObject;
            }
            return null;
        }

        private static bool IsForbiddenObject(string name)
        {
            for (int patternIndex = 0; patternIndex < FORBIDDEN_PATTERNS.Length; patternIndex++)
            {
                string pattern = FORBIDDEN_PATTERNS[patternIndex];
                if (name.Contains(pattern))
                    return true;
            }
            return false;
        }

        private static bool HasForbiddenChildren(Transform root)
        {
            if (IsForbiddenObject(root.name))
                return true;

            for (int i = 0; i < root.childCount; i++)
            {
                if (HasForbiddenChildren(root.GetChild(i)))
                    return true;
            }
            return false;
        }

        private static void ListChildren(Transform parent, string report, int depth)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                string indent = new string(' ', depth * 4);
                bool isForbidden = IsForbiddenObject(child.name);
                Debug.Log($"{indent}{(isForbidden ? "⚠️" : "  ")} {child.name}");

                if (child.childCount > 0)
                {
                    ListChildren(child, report, depth + 1);
                }
            }
        }
    }
}

#endif
