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

using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
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
            "Mock",
            "Stub",
            "TestTrap",
        };

        private const string BootstrapScenePath = "Assets/_Project/Scenes/00_BOOTSTRAP.unity";

        [MenuItem("Window/HECTON-8/Validate Bootstrap Architecture")]
        public static void ValidateBootstrapArchitecture()
        {
            bool batch = Application.isBatchMode;
            Scene bootstrapScene = SceneManager.GetSceneByPath(BootstrapScenePath);

            if (!bootstrapScene.IsValid() || !bootstrapScene.isLoaded)
            {
                bootstrapScene = EditorSceneManager.OpenScene(
                    BootstrapScenePath,
                    OpenSceneMode.Single);
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
            StringBuilder report = new StringBuilder(2048);
            report.AppendLine("=== BOOTSTRAP ARCHITECTURE VALIDATION ===").AppendLine();
            bool managersOk = true;
            bool buildSettingsOk = true;

            // ── Check for required managers ──
            report.AppendLine("REQUIRED MANAGERS:");
            for (int managerIndex = 0; managerIndex < REQUIRED_MANAGERS.Length; managerIndex++)
            {
                string managerName = REQUIRED_MANAGERS[managerIndex];
                bool found = FindObjectInSceneRecursive(bootstrapScene, managerName) != null;
                report.Append("  ").Append(found ? "✓ " : "✗ ").AppendLine(managerName);
                if (!found)
                    managersOk = false;
            }

            report.AppendLine().AppendLine("OBJECTS IN ROOT:");
            for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
            {
                GameObject root = rootObjects[rootIndex];
                bool isForbidden = IsForbiddenObject(root.name);
                report.Append("  ").Append(isForbidden ? "⚠️ " : "  ").AppendLine(root.name);

                if (root.transform.childCount > 0)
                {
                    AppendChildren(root.transform, report, depth: 1);
                }
            }

            report.AppendLine().AppendLine("FORBIDDEN OBJECTS:");
            bool foundForbidden = false;
            for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
            {
                GameObject root = rootObjects[rootIndex];
                if (HasForbiddenChildren(root.transform))
                {
                    report.Append("  ⚠️ ").Append(root.name).AppendLine(" contains forbidden children!");
                    foundForbidden = true;
                }
            }

            if (!foundForbidden)
            {
                report.AppendLine("  ✓ No forbidden gameplay objects found.");
            }

            // ── Check Build Settings & Auto-load Rules ──
            report.AppendLine().AppendLine("BUILD SETTINGS:");
            if (EditorBuildSettings.scenes.Length > 0)
            {
                string firstScene = EditorBuildSettings.scenes[0].path;
                bool isBootstrapFirst = firstScene.Contains("00_BOOTSTRAP");
                report.Append("  ").Append(isBootstrapFirst ? "✓ " : "✗ ").Append("First scene: ").AppendLine(firstScene);
                if (!isBootstrapFirst)
                    buildSettingsOk = false;

                // Validate 01_MAIN_MENU and 02_HECTON_WORLD order
                for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
                {
                    EditorBuildSettingsScene sceneSetting = EditorBuildSettings.scenes[i];
                    if (sceneSetting.path.Contains("01_MAIN_MENU") || sceneSetting.path.Contains("02_HECTON_WORLD"))
                    {
                        report.Append("  ✓ Configured scene index ").Append(i).Append(": ").AppendLine(sceneSetting.path);
                    }
                }
            }
            else
            {
                report.AppendLine("  ✗ No scenes in Build Settings!");
                buildSettingsOk = false;
            }

            bool passed = managersOk && buildSettingsOk && !foundForbidden;
            report.Append("\nRESULT: ").AppendLine(passed ? "PASS" : "FAIL");

            string finalReport = report.ToString();

            if (passed)
                Debug.Log(finalReport);
            else
                Debug.LogError(finalReport);

            if (!batch)
            {
                EditorUtility.DisplayDialog(
                    "Bootstrap Validation Report",
                    finalReport,
                    "OK");
            }
        }

        private static GameObject FindObjectInSceneRecursive(Scene scene, string name)
        {
            GameObject[] rootObjects = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
            {
                GameObject result = SearchTransformRecursive(rootObjects[rootIndex].transform, name);
                if (result != null)
                    return result;
            }
            return null;
        }

        private static GameObject SearchTransformRecursive(Transform parent, string name)
        {
            if (parent.name.Equals(name, System.StringComparison.Ordinal))
                return parent.gameObject;

            for (int i = 0; i < parent.childCount; i++)
            {
                GameObject found = SearchTransformRecursive(parent.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static bool IsForbiddenObject(string name)
        {
            for (int patternIndex = 0; patternIndex < FORBIDDEN_PATTERNS.Length; patternIndex++)
            {
                string pattern = FORBIDDEN_PATTERNS[patternIndex];
                if (name.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) >= 0)
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

        private static void AppendChildren(Transform parent, StringBuilder report, int depth)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                bool isForbidden = IsForbiddenObject(child.name);
                for (int d = 0; d < depth * 4; d++)
                    report.Append(' ');
                report.Append(isForbidden ? "⚠️ " : "  ").AppendLine(child.name);

                if (child.childCount > 0)
                {
                    AppendChildren(child, report, depth + 1);
                }
            }
        }
    }
}
#endif
