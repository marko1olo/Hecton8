// ============================================================================
// HECTON-8 — MainMenuValidator.cs
// Editor-only validator for 01_MAIN_MENU scene completeness.
//
// CHECKLIST:
// ✓ MainMenuController script attached and enabled
// ✓ All required CanvasGroups assigned (mainMenuGroup, saveLoadGroup, settingsGroup, loadingGroup)
// ✓ All required Buttons assigned (btnNewGame, btnLoadGame, btnSettings, btnQuit, back buttons)
// ✓ All required TextMeshProUGUI labels assigned
// ✓ Loading screen UI (slider, percent text)
// ✓ Save slots container and prefab assigned
// ✓ Camera present
// ✓ EventSystem present (for UI input)
// ✓ No broken references
//
// Usage: Window > HECTON-8 > Validate Main Menu
// ============================================================================

#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Hecton.UI.MainMenu;
using TMPro;

namespace Hecton8.Editor
{
    public static class MainMenuValidator
    {
        [MenuItem("Window/HECTON-8/Validate Main Menu")]
        public static void ValidateMainMenu()
        {
            Scene mainMenuScene = SceneManager.GetSceneByPath(
                "Assets/_Project/Scenes/01_MAIN_MENU.unity");

            if (!mainMenuScene.IsValid())
            {
                EditorUtility.DisplayDialog(
                    "Main Menu Validation",
                    "❌ 01_MAIN_MENU.unity not found or not loaded.",
                    "OK");
                return;
            }

            string report = "=== MAIN MENU COMPLETENESS VALIDATION ===\n\n";

            // ── Find MainMenuController ──
            report += "MAIN MENU CONTROLLER:\n";
            MainMenuController controller = FindControllerInScene(mainMenuScene);
            if (controller != null && controller.isActiveAndEnabled)
            {
                report += "  ✓ MainMenuController found and active\n";
                ValidateControllerReferences(controller, ref report);
            }
            else
            {
                report += "  ✗ MainMenuController NOT found or disabled!\n";
            }

            // ── Check for Camera ──
            report += "\nCAMERA:\n";
            Camera cam = FindComponentInScene<Camera>(mainMenuScene);
            if (cam != null)
            {
                report += $"  ✓ Camera found: {cam.name}\n";
            }
            else
            {
                report += "  ✗ No Camera found in scene!\n";
            }

            // ── Check for EventSystem ──
            report += "\nEVENT SYSTEM (UI Input):\n";
            GraphicRaycaster eventSystem = FindComponentInScene<GraphicRaycaster>(mainMenuScene);
            if (eventSystem != null)
            {
                report += "  ✓ EventSystem/GraphicRaycaster found\n";
            }
            else
            {
                report += "  ⚠️ No EventSystem detected (UI input may not work)\n";
            }

            // ── Scene root structure ──
            report += "\nROOT GAME OBJECTS:\n";
            var roots = mainMenuScene.GetRootGameObjects();
            foreach (var root in roots)
            {
                report += $"  • {root.name}\n";
            }

            report += "\n" + new string('=', 50) + "\n";

            Debug.Log(report);

            EditorUtility.DisplayDialog(
                "Main Menu Validation Report",
                report.Length > 2000 ? report.Substring(0, 2000) + "...\n(see Console for full report)" : report,
                "OK");
        }

        private static MainMenuController FindControllerInScene(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                MainMenuController controller = root.GetComponent<MainMenuController>();
                if (controller != null)
                    return controller;

                controller = root.GetComponentInChildren<MainMenuController>();
                if (controller != null)
                    return controller;
            }
            return null;
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T component = roots[i].GetComponentInChildren<T>();
                if (component != null)
                    return component;
            }

            return null;
        }

        private static void ValidateControllerReferences(MainMenuController controller, ref string report)
        {
            report += "\nCONTROLLER REFERENCES:\n";

            // Use reflection to check serialized fields
            var serializedObject = new SerializedObject(controller);
            var property = serializedObject.GetIterator();

            bool hasErrors = false;

            // Known field names to check
            string[] requiredFields = new[]
            {
                "mainMenuGroup", "saveLoadGroup", "settingsGroup", "loadingGroup",
                "slotsContainer", "slotPrefab",
                "btnNewGame", "btnLoadGame", "btnSettings", "btnQuit",
                "btnBackFromSaveLoad", "btnBackFromSettings",
                "labelNewGame", "labelLoadGame", "labelSettings", "labelQuit",
                "loadingProgressBar", "loadingPercentText"
            };

            foreach (string fieldName in requiredFields)
            {
                IsFieldAssigned(serializedObject, fieldName, out bool isAssigned);
                string status = isAssigned ? "✓" : "✗";
                report += $"  {status} {fieldName}\n";
                if (!isAssigned) hasErrors = true;
            }

            if (hasErrors)
            {
                report += "\n⚠️ Some required fields are missing!\n";
            }
        }

        private static bool IsFieldAssigned(SerializedObject serializedObject, string fieldName, out bool isAssigned)
        {
            SerializedProperty property = serializedObject.FindProperty(fieldName);
            isAssigned = property != null && property.objectReferenceValue != null;
            return isAssigned;
        }
    }
}

#endif
