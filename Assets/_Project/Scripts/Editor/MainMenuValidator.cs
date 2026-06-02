// ============================================================================
// HECTON-8 - MainMenuValidator.cs
// Editor-only validator for the 01_MAIN_MENU diegetic menu scene.
// ============================================================================

#if UNITY_EDITOR

using Hecton.UI.MainMenu;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hecton8.Editor
{
    public static class MainMenuValidator
    {
        private const string MainMenuScenePath = "Assets/_Project/Scenes/01_MAIN_MENU.unity";

        [MenuItem("Window/HECTON-8/Validate Main Menu")]
        public static void ValidateMainMenu()
        {
            Scene mainMenuScene = SceneManager.GetSceneByPath(MainMenuScenePath);
            if (!mainMenuScene.IsValid())
            {
                EditorUtility.DisplayDialog(
                    "Main Menu Validation",
                    "01_MAIN_MENU.unity not found or not loaded.",
                    "OK");
                return;
            }

            string report = "=== MAIN MENU DIEGETIC VALIDATION ===\n\n";

            report += "MAIN MENU CONTROLLER:\n";
            MainMenuController controller = FindControllerInScene(mainMenuScene);
            if (controller != null && controller.isActiveAndEnabled)
            {
                report += "  OK MainMenuController found and active\n";
                ValidateControllerReferences(controller, ref report);
            }
            else
            {
                report += "  FAIL MainMenuController not found or disabled\n";
            }

            report += "\nCAMERA:\n";
            Camera camera = FindComponentInScene<Camera>(mainMenuScene);
            report += camera != null
                ? $"  OK Camera found: {camera.name}\n"
                : "  FAIL No Camera found in scene\n";

            report += "\nDIEGETIC INPUT OWNERSHIP:\n";
            EventSystem eventSystem = FindComponentInScene<EventSystem>(mainMenuScene);
            report += eventSystem != null
                ? $"  OK EventSystem found: {eventSystem.name}\n"
                : "  WARN No EventSystem detected; UI input module may be missing\n";

            Canvas canvas = FindComponentInScene<Canvas>(mainMenuScene);
            if (canvas == null)
                report += "  FAIL No Canvas found in scene\n";
            else if (canvas.renderMode == RenderMode.WorldSpace)
                report += $"  OK Canvas is World Space: {canvas.name}\n";
            else
                report += $"  FAIL Canvas is not World Space: {canvas.name} ({canvas.renderMode})\n";

            GraphicRaycaster graphicRaycaster = FindComponentInScene<GraphicRaycaster>(mainMenuScene);
            if (graphicRaycaster == null)
                report += "  OK No GraphicRaycaster present; physical panel raycaster owns hits\n";
            else if (!graphicRaycaster.enabled)
                report += $"  OK GraphicRaycaster disabled: {graphicRaycaster.name}\n";
            else
                report += $"  FAIL GraphicRaycaster enabled: {graphicRaycaster.name}\n";

            report += "\nROOT GAME OBJECTS:\n";
            GameObject[] roots = mainMenuScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                report += $"  - {roots[i].name}\n";

            report += "\n" + new string('=', 50) + "\n";
            Debug.Log(report);

            EditorUtility.DisplayDialog(
                "Main Menu Validation Report",
                report.Length > 2000 ? report.Substring(0, 2000) + "...\n(see Console for full report)" : report,
                "OK");
        }

        private static MainMenuController FindControllerInScene(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root.TryGetComponent(out MainMenuController controller))
                    return controller;

                controller = root.GetComponentInChildren<MainMenuController>(includeInactive: true);
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
                T component = roots[i].GetComponentInChildren<T>(includeInactive: true);
                if (component != null)
                    return component;
            }

            return null;
        }

        private static void ValidateControllerReferences(MainMenuController controller, ref string report)
        {
            report += "\nCONTROLLER REFERENCES:\n";

            SerializedObject serializedObject = new SerializedObject(controller);
            string[] requiredFields =
            {
                "mainMenuGroup",
                "saveLoadGroup",
                "settingsGroup",
                "loadingGroup",
                "slotsContainer",
                "slotPrefab",
                "btnNewGame",
                "btnLoadGame",
                "btnSettings",
                "btnQuit",
                "btnBackFromSaveLoad",
                "btnBackFromSettings",
                "labelNewGame",
                "labelLoadGame",
                "labelSettings",
                "labelQuit",
                "loadingProgressBar",
                "loadingPercentText"
            };

            bool hasErrors = false;
            for (int i = 0; i < requiredFields.Length; i++)
            {
                string fieldName = requiredFields[i];
                bool isAssigned = IsFieldAssigned(serializedObject, fieldName);
                report += isAssigned ? $"  OK {fieldName}\n" : $"  FAIL {fieldName}\n";
                hasErrors |= !isAssigned;
            }

            if (hasErrors)
                report += "\nWARN Some required fields are missing.\n";
        }

        private static bool IsFieldAssigned(SerializedObject serializedObject, string fieldName)
        {
            SerializedProperty property = serializedObject.FindProperty(fieldName);
            return property != null && property.objectReferenceValue != null;
        }
    }
}

#endif
