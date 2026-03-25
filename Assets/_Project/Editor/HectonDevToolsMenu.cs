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
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        [MenuItem(MenuRoot + "Reveal Editor.log In OS", false, 12)]
        public static void RevealEditorLog()
        {
            string path = Application.consoleLogPath;
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[Hecton Dev] Application.consoleLogPath is empty.");
                return;
            }

            if (!File.Exists(path))
            {
                Debug.LogWarning("[Hecton Dev] Editor.log not found yet:\n" + path);
            }

            EditorUtility.RevealInFinder(path);
        }

        [MenuItem(MenuRoot + "Copy Editor.log Path", false, 13)]
        public static void CopyEditorLogPath()
        {
            string path = Application.consoleLogPath;
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[Hecton Dev] Application.consoleLogPath is empty.");
                return;
            }

            EditorGUIUtility.systemCopyBuffer = path;
            Debug.Log("[Hecton Dev] Copied Editor.log path:\n" + path);
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

        // ── Project Settings (Unity 6 SettingsService paths) ──────────────

        [MenuItem(MenuRoot + "Project Settings/Audio", false, 100)]
        public static void OpenPsAudio()
        {
            SettingsService.OpenProjectSettings("Project/Audio");
        }

        [MenuItem(MenuRoot + "Project Settings/Player", false, 101)]
        public static void OpenPsPlayer()
        {
            SettingsService.OpenProjectSettings("Project/Player");
        }

        [MenuItem(MenuRoot + "Project Settings/Quality", false, 102)]
        public static void OpenPsQuality()
        {
            SettingsService.OpenProjectSettings("Project/Quality");
        }

        [MenuItem(MenuRoot + "Project Settings/Graphics (URP)", false, 103)]
        public static void OpenPsGraphics()
        {
            SettingsService.OpenProjectSettings("Project/Graphics");
        }

        [MenuItem(MenuRoot + "Project Settings/Time", false, 104)]
        public static void OpenPsTime()
        {
            SettingsService.OpenProjectSettings("Project/Time");
        }

        [MenuItem(MenuRoot + "Project Settings/Physics", false, 105)]
        public static void OpenPsPhysics()
        {
            SettingsService.OpenProjectSettings("Project/Physics");
        }

        [MenuItem(MenuRoot + "Project Settings/Input System Package", false, 106)]
        public static void OpenPsInputSystem()
        {
            SettingsService.OpenProjectSettings("Project/Input System Package");
        }

        [MenuItem(MenuRoot + "Project Settings/Tags and Layers", false, 107)]
        public static void OpenPsTagsAndLayers()
        {
            SettingsService.OpenProjectSettings("Project/Tags and Layers");
        }

        [MenuItem(MenuRoot + "Open Package Manager Window", false, 110)]
        public static void OpenPackageManager()
        {
            EditorApplication.ExecuteMenuItem("Window/Package Manager");
        }

        [MenuItem(MenuRoot + "Open Console Window", false, 111)]
        public static void OpenConsole()
        {
            EditorApplication.ExecuteMenuItem("Window/General/Console");
        }

        // ── Scene & assets ───────────────────────────────────────────────

        [MenuItem(MenuRoot + "Scene/Copy Active Scene Path", false, 120)]
        public static void CopyActiveScenePath()
        {
            Scene s = EditorSceneManager.GetActiveScene();
            string path = s.path;
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[Hecton Dev] Active scene is not saved (no path).");
                return;
            }

            EditorGUIUtility.systemCopyBuffer = path;
            Debug.Log("[Hecton Dev] Copied scene path: " + path);
        }

        [MenuItem(MenuRoot + "Scene/Save Open Scenes + Assets", false, 121)]
        public static void SaveOpenScenesAndAssets()
        {
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[Hecton Dev] Saved assets and open scenes.");
        }

        [MenuItem(MenuRoot + "Scene/Validate Loaded Scenes (log)", false, 122)]
        public static void ValidateLoadedScenes()
        {
            Scene[] scenes = GetLoadedScenesSnapshot();
            if (scenes.Length == 0)
            {
                Debug.Log("[Hecton Dev] No loaded scenes.");
                return;
            }

            var sb = new StringBuilder(256);
            sb.AppendLine("── Hecton Dev — Loaded scenes ──");
            for (int i = 0; i < scenes.Length; i++)
            {
                Scene s = scenes[i];
                sb.Append("  • ").Append(s.name);
                if (!string.IsNullOrEmpty(s.path))
                {
                    sb.Append("  (").Append(s.path).Append(')');
                }

                sb.AppendLine();
            }

            SpatialAudioManager[] sams = UnityEngine.Object.FindObjectsByType<SpatialAudioManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            sb.Append("SpatialAudioManager count: ").AppendLine(sams.Length.ToString());
            if (sams.Length > 1)
            {
                Debug.LogWarning("[Hecton Dev] Несколько SpatialAudioManager — допустимо только при смене сцен; проверь DontDestroyOnLoad.");
            }
            else if (sams.Length == 0)
            {
                Debug.LogWarning("[Hecton Dev] SpatialAudioManager не найден в загруженных сценах (может быть ок в чистом sandbox).");
            }

            int missingTotal = LogMissingScriptsInLoadedScenes();
            sb.Append("Missing script components: ").Append(missingTotal).AppendLine();

            Debug.Log(sb.ToString());
        }

        private static Scene[] GetLoadedScenesSnapshot()
        {
            int count = SceneManager.sceneCount;
            var list = new Scene[count];
            for (int i = 0; i < count; i++)
            {
                list[i] = SceneManager.GetSceneAt(i);
            }

            return list;
        }

        /// <summary>Предупреждения с ping объектов; возвращает число missing *components*.</summary>
        private static int LogMissingScriptsInLoadedScenes()
        {
            GameObject[] gos = UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            int missing = 0;
            for (int i = 0; i < gos.Length; i++)
            {
                GameObject go = gos[i];
                if (go == null)
                {
                    continue;
                }

                Scene sc = go.scene;
                if (!sc.IsValid() || !sc.isLoaded)
                {
                    continue;
                }

                int miss = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (miss <= 0)
                {
                    continue;
                }

                missing += miss;
                Debug.LogWarning(
    "[Hecton Dev] Missing script (" + miss + "): " + BuildTransformPath(go.transform),
    go);
            }

            return missing;
        }

        private static string BuildTransformPath(Transform t)
        {
            if (t == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(128);
            while (t != null)
            {
                if (sb.Length > 0)
                {
                    sb.Insert(0, '/');
                }

                sb.Insert(0, t.name);
                t = t.parent;
            }

            return sb.ToString();
        }
    }
}
