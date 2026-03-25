// ============================================================================
// Hecton8 — HectonDevToolsMenu.cs
// Статические пункты меню для разработки (редактор, не рантайм билда).
// ============================================================================

using System;
using System.IO;
using System.Text;
using Hecton8.Audio;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Общие dev-утилиты: пути, PlayerPrefs, сцена, диагностика сборки.
    /// </summary>
    public static class HectonDevToolsMenu
    {
        private const string MenuRoot = "Tools/Hecton/Dev/";

        [MenuItem(MenuRoot + "Reveal Persistent Data Path %#&p", false, 10)]
        public static void RevealPersistentDataPath()
        {
            string path = Application.persistentDataPath;
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            EditorUtility.RevealInFinder(path);
        }

        [MenuItem(MenuRoot + "Copy Persistent Data Path", false, 11)]
        public static void CopyPersistentDataPath()
        {
            EditorGUIUtility.systemCopyBuffer = Application.persistentDataPath;
            Debug.Log("[Hecton Dev] Copied persistentDataPath:\n" + Application.persistentDataPath);
        }

        [MenuItem(MenuRoot + "Reveal Project Root In OS", false, 20)]
        public static void RevealProjectRoot()
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(root))
            {
                return;
            }

            EditorUtility.RevealInFinder(root);
        }

        [MenuItem(MenuRoot + "Clear All PlayerPrefs (this machine)…", false, 30)]
        public static void ClearPlayerPrefs()
        {
            if (!EditorUtility.DisplayDialog(
                    "Hecton Dev",
                    "Удалить все PlayerPrefs для этого пользователя и редактора Unity?\n\n" +
                    "Откатить нельзя.",
                    "Очистить",
                    "Отмена"))
            {
                return;
            }

            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[Hecton Dev] PlayerPrefs cleared.");
        }

        [MenuItem(MenuRoot + "Log Build & Scripting Info To Console", false, 40)]
        public static void LogBuildInfo()
        {
            var sb = new StringBuilder(512);
            sb.AppendLine("── Hecton Dev — Build info ──");
            sb.AppendLine("Unity: " + Application.unityVersion);
            sb.AppendLine("Product: " + Application.productName);
            sb.AppendLine("Active build target: " + EditorUserBuildSettings.activeBuildTarget);
            sb.AppendLine("Selected build target group: " + EditorUserBuildSettings.selectedBuildTargetGroup);

            NamedBuildTarget nbt = NamedBuildTarget.FromBuildTargetGroup(
                EditorUserBuildSettings.selectedBuildTargetGroup);
            string defLine = PlayerSettings.GetScriptingDefineSymbols(nbt);
            string[] defines = string.IsNullOrEmpty(defLine)
                ? Array.Empty<string>()
                : defLine.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            sb.Append("Scripting defines (").Append(defines.Length).AppendLine("):");
            for (int i = 0; i < defines.Length; i++)
            {
                sb.Append("  • ").AppendLine(defines[i]);
            }

            if (defines.Length == 0)
            {
                sb.AppendLine("  (none)");
            }

            Debug.Log(sb.ToString());
        }

        [MenuItem(MenuRoot + "Select SpatialAudioManager In Loaded Scenes", false, 50)]
        public static void SelectSpatialAudioManager()
        {
            SpatialAudioManager sam = UnityEngine.Object.FindFirstObjectByType<SpatialAudioManager>(
                FindObjectsInactive.Include);
            if (sam == null)
            {
                Debug.LogWarning("[Hecton Dev] SpatialAudioManager not found in loaded scenes.");
                return;
            }

            Selection.activeObject = sam.gameObject;
            EditorGUIUtility.PingObject(sam.gameObject);
        }

        [MenuItem(MenuRoot + "Select Main Camera In Loaded Scenes", false, 51)]
        public static void SelectMainCamera()
        {
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera c = cameras[i];
                if (c != null && c.CompareTag("MainCamera"))
                {
                    Selection.activeObject = c.gameObject;
                    EditorGUIUtility.PingObject(c.gameObject);
                    return;
                }
            }

            Debug.LogWarning("[Hecton Dev] No camera tagged MainCamera in loaded scenes.");
        }

        [MenuItem(MenuRoot + "Reset Time Scale To 1 (Play Mode)", false, 60)]
        public static void ResetTimeScale()
        {
            Time.timeScale = 1f;
            Debug.Log("[Hecton Dev] Time.timeScale = 1");
        }

        [MenuItem(MenuRoot + "Reset Time Scale To 1 (Play Mode)", true)]
        public static bool ResetTimeScaleValidate()
        {
            return EditorApplication.isPlaying;
        }

        private const string ScreenshotsFolder = "Assets/Screenshots";

        [MenuItem(MenuRoot + "Capture Screenshot → Assets/Screenshots (Play Mode)", false, 70)]
        public static void CaptureScreenshotToProject()
        {
            if (!AssetDatabase.IsValidFolder(ScreenshotsFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets"))
                {
                    Debug.LogError("[Hecton Dev] Assets folder missing.");
                    return;
                }

                AssetDatabase.CreateFolder("Assets", "Screenshots");
            }

            string fileName = $"screenshot-{DateTime.Now:yyyyMMdd-HHmmss}.png";
            string relativePath = Path.Combine(ScreenshotsFolder, fileName).Replace('\\', '/');
            ScreenCapture.CaptureScreenshot(relativePath);
            Debug.Log("[Hecton Dev] Capturing screenshot to: " + relativePath);
            EditorApplication.delayCall += () =>
            {
                AssetDatabase.Refresh();
                EditorApplication.delayCall += () =>
                {
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(relativePath);
                    if (tex != null)
                    {
                        Selection.activeObject = tex;
                        EditorGUIUtility.PingObject(tex);
                    }
                };
            };
        }

        [MenuItem(MenuRoot + "Capture Screenshot → Assets/Screenshots (Play Mode)", true)]
        public static bool CaptureScreenshotValidate()
        {
            return EditorApplication.isPlaying;
        }
    }
}
