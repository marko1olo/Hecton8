// ============================================================================
// HECTON-8 - MainMenuValidator.cs
// Editor-only validator for the 01_MAIN_MENU diegetic menu scene.
// ============================================================================

#if UNITY_EDITOR

using System.Collections.Generic;
using System.Text;
using Hecton.UI.MainMenu;
using Hecton8.UI;
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
        private const string DiegeticCanvasRootName = "Canvas";
        private const string ReadableOverlayRootName = "H8_MENU_READABLE_OVERLAY_1428";
        private const string InterfaceGlowLightName = "Menu_AAA_Interface_Glow";
        private const string CoolFillLightName = "Menu_AAA_CoolFill_Key";
        private const string WarmRimLightName = "Menu_AAA_Warm_Rim";
        private const int ExpectedMenuCanvasCount = 2;

        [MenuItem("Window/HECTON-8/Validate Main Menu")]
        public static void ValidateMainMenu()
        {
            // Compile-proof / CI path: -executeMethod must never open DisplayDialog
            // (batchmode aborts with "This should not be called in batch mode").
            bool batch = Application.isBatchMode;
            Scene mainMenuScene = SceneManager.GetSceneByPath(MainMenuScenePath);

            if (!mainMenuScene.IsValid() || !mainMenuScene.isLoaded)
            {
                // OpenScene is additive-safe for a one-shot audit; batchmode never has the scene
                // already open, so the previous IsValid-only gate always failed there.
                mainMenuScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    MainMenuScenePath,
                    UnityEditor.SceneManagement.OpenSceneMode.Single);
            }

            if (!mainMenuScene.IsValid() || !mainMenuScene.isLoaded)
            {
                string missing = "[MainMenuValidator] FAIL: 01_MAIN_MENU.unity not found or not loaded at " + MainMenuScenePath;
                Debug.LogError(missing);
                if (!batch)
                    EditorUtility.DisplayDialog("Main Menu Validation", missing, "OK");
                if (batch)
                    EditorApplication.Exit(1);
                return;
            }

            StringBuilder report = new StringBuilder(4096);
            report.AppendLine("=== MAIN MENU DIEGETIC VALIDATION ===");
            report.AppendLine();

            ValidateController(mainMenuScene, report);
            ValidateCamera(mainMenuScene, report);
            ValidateDiegeticInput(mainMenuScene, report);
            ValidateCanvasInventory(mainMenuScene, report);
            ValidateLighting(mainMenuScene, report);
            AppendRootGameObjects(mainMenuScene, report);

            report.AppendLine();
            report.AppendLine(new string('=', 50));
            string reportText = report.ToString();
            bool passed = reportText.IndexOf("FAIL ", System.StringComparison.Ordinal) < 0;
            report.Append("RESULT: ");
            report.AppendLine(passed ? "PASS" : "FAIL");
            reportText = report.ToString();

            // Always emit the report. Soft FAIL is LogError so CI greps can see it,
            // but batchmode still exits 0: this method is a compile/scene-audit entry.
            if (passed)
                Debug.Log(reportText);
            else
                Debug.LogError(reportText);

            if (!batch)
            {
                EditorUtility.DisplayDialog(
                    "Main Menu Validation Report",
                    reportText.Length > 2000 ? reportText.Substring(0, 2000) + "...\n(see Console for full report)" : reportText,
                    "OK");
            }
            // batchmode: rely on -quit; do not EditorApplication.Exit(1) on soft FAIL.
        }


        private static void ValidateController(Scene scene, StringBuilder report)
        {
            report.AppendLine("MAIN MENU CONTROLLER:");
            MainMenuController controller = FindComponentInScene<MainMenuController>(scene);
            if (controller != null && controller.isActiveAndEnabled)
            {
                report.AppendLine("  OK MainMenuController found and active");
                ValidateControllerReferences(controller, report);
            }
            else
            {
                report.AppendLine("  FAIL MainMenuController not found or disabled");
            }

            report.AppendLine();
        }

        private static void ValidateCamera(Scene scene, StringBuilder report)
        {
            report.AppendLine("CAMERA:");
            Camera camera = FindComponentInScene<Camera>(scene);
            if (camera == null)
            {
                report.AppendLine("  FAIL No Camera found in scene");
                report.AppendLine();
                return;
            }

            report.Append("  OK Camera found: ");
            report.AppendLine(camera.name);
            report.Append(camera.clearFlags == CameraClearFlags.SolidColor
                ? "  OK Clear flags are SolidColor"
                : "  FAIL Clear flags are not SolidColor: ");
            if (camera.clearFlags != CameraClearFlags.SolidColor)
                report.Append(camera.clearFlags);
            report.AppendLine();
            report.Append("  Background: ");
            report.Append(camera.backgroundColor);
            report.AppendLine();

            bool hasMenuCameraController = camera.TryGetComponent(out MenuCameraController menuCameraController) &&
                                           menuCameraController != null &&
                                           menuCameraController.isActiveAndEnabled;
            report.AppendLine(hasMenuCameraController
                ? "  OK MenuCameraController serialized on Main Camera"
                : "  FAIL MenuCameraController missing or disabled on Main Camera");

            bool hasAtmosphereController = camera.TryGetComponent(out MainMenuAtmosphereController atmosphereController) &&
                                           atmosphereController != null &&
                                           atmosphereController.isActiveAndEnabled;
            report.AppendLine(hasAtmosphereController
                ? "  OK MainMenuAtmosphereController serialized on Main Camera"
                : "  FAIL MainMenuAtmosphereController missing or disabled on Main Camera");
            report.AppendLine();
        }

        private static void ValidateDiegeticInput(Scene scene, StringBuilder report)
        {
            report.AppendLine("DIEGETIC INPUT OWNERSHIP:");

            EventSystem eventSystem = FindComponentInScene<EventSystem>(scene);
            report.AppendLine(eventSystem != null
                ? "  OK EventSystem found: " + eventSystem.name
                : "  WARN No EventSystem detected; UI input module may be missing");

            DiegeticPanelController panelController = FindComponentInScene<DiegeticPanelController>(scene);
            report.AppendLine(panelController != null
                ? "  OK DiegeticPanelController found: " + panelController.name
                : "  FAIL DiegeticPanelController missing");

            if (panelController != null)
            {
                bool hasPanelCollider = panelController.TryGetComponent(out BoxCollider panelCollider) &&
                                        panelCollider != null &&
                                        panelCollider.enabled;
                report.AppendLine(hasPanelCollider
                    ? "  OK Physical panel BoxCollider serialized"
                    : "  FAIL Physical panel BoxCollider missing or disabled");

                if (hasPanelCollider)
                {
                    report.AppendLine(panelCollider.isTrigger
                        ? "  OK Physical panel BoxCollider is trigger-only"
                        : "  FAIL Physical panel BoxCollider is not trigger-only");

                    Vector3 size = panelCollider.size;
                    bool validPanelVolume = size.x >= 1900f && size.y >= 1000f && size.z >= 0.01f;
                    report.AppendLine(validPanelVolume
                        ? "  OK Physical panel BoxCollider covers diegetic canvas"
                        : "  FAIL Physical panel BoxCollider size below canvas bounds");
                }
            }

            DiegeticMenuRaycastReceiver receiver = FindComponentInScene<DiegeticMenuRaycastReceiver>(scene);
            report.AppendLine(receiver != null
                ? "  OK DiegeticMenuRaycastReceiver found: " + receiver.name
                : "  FAIL DiegeticMenuRaycastReceiver missing");

            report.AppendLine();
        }

        private static void ValidateCanvasInventory(Scene scene, StringBuilder report)
        {
            report.AppendLine("CANVAS INVENTORY:");

            List<Canvas> canvases = new List<Canvas>(8);
            CollectComponentsInScene(scene, canvases);
            if (canvases.Count == 0)
            {
                report.AppendLine("  FAIL No Canvas found; authored diegetic TMP panel missing");
                report.AppendLine();
                return;
            }

            int activeWorldSpace = 0;
            int serializedWorldSpace = 0;
            int screenSpaceCamera = 0;
            int screenSpaceOverlay = 0;
            int unknownCanvases = 0;
            bool hasDiegeticCanvas = false;
            bool hasReadableOverlayCanvas = false;
            for (int i = 0; i < canvases.Count; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null)
                    continue;

                bool active = canvas.gameObject.activeInHierarchy && canvas.enabled;
                bool allowedCanvas = IsAllowedMenuCanvasRoot(canvas.name);
                if (active && canvas.renderMode == RenderMode.WorldSpace)
                    activeWorldSpace++;
                if (canvas.renderMode == RenderMode.WorldSpace)
                    serializedWorldSpace++;
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                    screenSpaceCamera++;
                else if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    screenSpaceOverlay++;
                if (!allowedCanvas)
                    unknownCanvases++;
                hasDiegeticCanvas |= canvas.name == DiegeticCanvasRootName;
                hasReadableOverlayCanvas |= canvas.name == ReadableOverlayRootName;

                report.Append("  ");
                report.Append(IsForbiddenScreenSpaceCanvas(canvas) || !allowedCanvas ? "FAIL " : "INFO ");
                report.Append(canvas.name);
                report.Append(" renderMode=");
                report.Append(canvas.renderMode);
                report.Append(" enabled=");
                report.Append(canvas.enabled);
                report.Append(" activeInHierarchy=");
                report.Append(active);
                report.AppendLine();
            }

            report.AppendLine(activeWorldSpace > 0
                ? "  OK Active WorldSpace Canvas count: " + activeWorldSpace
                : "  FAIL No active WorldSpace Canvas found");
            report.AppendLine(serializedWorldSpace == ExpectedMenuCanvasCount
                ? "  OK Serialized WorldSpace Canvas count: " + serializedWorldSpace
                : "  FAIL Serialized WorldSpace Canvas count: " + serializedWorldSpace);
            report.AppendLine(unknownCanvases == 0
                ? "  OK Unknown Canvas count: 0"
                : "  FAIL Unknown Canvas count: " + unknownCanvases);
            report.AppendLine(hasDiegeticCanvas
                ? "  OK Authored diegetic Canvas present"
                : "  FAIL Authored diegetic Canvas missing");
            report.AppendLine(hasReadableOverlayCanvas
                ? "  OK Readable overlay Canvas present"
                : "  FAIL Readable overlay Canvas missing");
            report.AppendLine(screenSpaceOverlay == 0
                ? "  OK Serialized ScreenSpaceOverlay Canvas count: 0"
                : "  FAIL Serialized ScreenSpaceOverlay Canvas count: " + screenSpaceOverlay);
            report.AppendLine(screenSpaceCamera == 0
                ? "  OK Serialized ScreenSpaceCamera Canvas count: 0"
                : "  FAIL Serialized ScreenSpaceCamera Canvas count: " + screenSpaceCamera);

            ValidateGraphicRaycasters(scene, report);
            report.AppendLine();
        }

        private static void ValidateGraphicRaycasters(Scene scene, StringBuilder report)
        {
            List<GraphicRaycaster> raycasters = new List<GraphicRaycaster>(8);
            CollectComponentsInScene(scene, raycasters);

            int activeEnabled = 0;
            int enabledTotal = 0;
            int unknownRaycasters = 0;
            for (int i = 0; i < raycasters.Count; i++)
            {
                GraphicRaycaster raycaster = raycasters[i];
                if (raycaster == null)
                    continue;

                bool active = raycaster.gameObject.activeInHierarchy && raycaster.enabled;
                bool allowedRoot = IsAllowedMenuCanvasRoot(raycaster.name);
                if (active)
                    activeEnabled++;
                if (raycaster.enabled)
                    enabledTotal++;
                if (!allowedRoot)
                    unknownRaycasters++;

                report.Append("  ");
                report.Append(raycaster.enabled || !allowedRoot ? "FAIL " : "INFO ");
                report.Append("GraphicRaycaster ");
                report.Append(raycaster.name);
                report.Append(" enabled=");
                report.Append(raycaster.enabled);
                report.Append(" activeInHierarchy=");
                report.Append(active);
                report.AppendLine();
            }

            report.AppendLine(activeEnabled == 0
                ? "  OK Active enabled GraphicRaycaster count: 0"
                : "  FAIL Active enabled GraphicRaycaster count: " + activeEnabled);
            report.AppendLine(enabledTotal == 0
                ? "  OK Serialized enabled GraphicRaycaster count: 0"
                : "  FAIL Serialized enabled GraphicRaycaster count: " + enabledTotal);
            report.AppendLine(unknownRaycasters == 0
                ? "  OK Unknown GraphicRaycaster count: 0"
                : "  FAIL Unknown GraphicRaycaster count: " + unknownRaycasters);
        }

        private static bool IsForbiddenScreenSpaceCanvas(Canvas canvas)
        {
            return canvas != null &&
                   (canvas.renderMode == RenderMode.ScreenSpaceOverlay ||
                    canvas.renderMode == RenderMode.ScreenSpaceCamera);
        }

        private static bool IsAllowedMenuCanvasRoot(string rootName)
        {
            return rootName == DiegeticCanvasRootName ||
                   rootName == ReadableOverlayRootName;
        }



        private static void ValidateLighting(Scene scene, StringBuilder report)
        {
            report.AppendLine("LIGHTING:");
            List<Light> lights = new List<Light>(16);
            CollectComponentsInScene(scene, lights);

            int activeLights = 0;
            int realtimeShadowCasters = 0;
            int practicalLights = 0;
            int activePracticalLights = 0;
            bool hasInterfaceGlow = false;
            bool hasCoolFill = false;
            bool hasWarmRim = false;
            for (int i = 0; i < lights.Count; i++)
            {
                Light light = lights[i];
                if (light == null)
                    continue;

                bool isPractical = IsMainMenuPracticalLight(light.name);
                if (isPractical)
                {
                    practicalLights++;
                    hasInterfaceGlow |= light.name == InterfaceGlowLightName;
                    hasCoolFill |= light.name == CoolFillLightName;
                    hasWarmRim |= light.name == WarmRimLightName;

                    report.Append("  ");
                    report.Append(light.shadows == LightShadows.None ? "OK " : "FAIL ");
                    report.Append(light.name);
                    report.Append(" shadows=");
                    report.Append(light.shadows);
                    report.Append(" active=");
                    report.Append(light.gameObject.activeInHierarchy);
                    report.Append(" enabled=");
                    report.Append(light.enabled);
                    report.Append(" intensity=");
                    report.Append(light.intensity);
                    report.Append(" range=");
                    report.Append(light.range);
                    report.AppendLine();

                    if (light.gameObject.activeInHierarchy && light.enabled)
                        activePracticalLights++;
                }

                if (light.gameObject.activeInHierarchy && light.enabled)
                {
                    activeLights++;
                    if (light.shadows != LightShadows.None)
                        realtimeShadowCasters++;
                }
            }

            report.AppendLine(activeLights > 0
                ? "  OK Active light count: " + activeLights
                : "  FAIL Active light count: 0");
            report.AppendLine(practicalLights == 3 && hasInterfaceGlow && hasCoolFill && hasWarmRim
                ? "  OK Authored practical light set complete"
                : "  FAIL Authored practical light set incomplete");
            report.AppendLine(activePracticalLights > 0
                ? "  OK Active practical light count: " + activePracticalLights
                : "  FAIL Active practical light count: 0");
            report.AppendLine(realtimeShadowCasters == 0
                ? "  OK Active realtime shadow-casting light count: 0"
                : "  WARN Active realtime shadow-casting light count: " + realtimeShadowCasters);
            report.AppendLine();
        }

        private static bool IsMainMenuPracticalLight(string lightName)
        {
            return lightName == InterfaceGlowLightName ||
                   lightName == CoolFillLightName ||
                   lightName == WarmRimLightName;
        }

        private static void AppendRootGameObjects(Scene scene, StringBuilder report)
        {
            report.AppendLine("ROOT GAME OBJECTS:");
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                report.Append("  - ");
                report.AppendLine(roots[i].name);
            }
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            List<T> results = new List<T>(4);
            CollectComponentsInScene(scene, results);
            return results.Count > 0 ? results[0] : null;
        }

        private static void CollectComponentsInScene<T>(Scene scene, List<T> results) where T : Component
        {
            results.Clear();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null)
                    continue;

                root.GetComponentsInChildren(true, results);
            }
        }

        private static void ValidateControllerReferences(MainMenuController controller, StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("CONTROLLER REFERENCES:");

            SerializedObject serializedObject = new SerializedObject(controller);
            string[] requiredFields =
            {
                "mainMenuGroup",
                "saveLoadGroup",
                "settingsGroup",
                "loadingGroup",
                "slotsContainer",
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
                report.AppendLine(isAssigned ? "  OK " + fieldName : "  FAIL " + fieldName);
                hasErrors |= !isAssigned;
            }

            if (hasErrors)
                report.AppendLine("WARN Some required fields are missing.");
        }

        private static bool IsFieldAssigned(SerializedObject serializedObject, string fieldName)
        {
            SerializedProperty property = serializedObject.FindProperty(fieldName);
            return property != null && property.objectReferenceValue != null;
        }
    }
}

#endif
