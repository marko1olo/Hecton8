// ============================================================================
// Hecton8 - HectonDevToolsMenu.cs
// Static development menu entries for editor-only diagnostics.
// ============================================================================

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Hecton8.Audio;
using Hecton8.Dev;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    /// <summary>
    /// General editor utilities: paths, PlayerPrefs, scenes, and build diagnostics.
    /// </summary>
    public static class HectonDevToolsMenu
    {
        private const string MenuRoot = "Tools/Hecton/Dev/";

        // COLD ALLOC: List<Transform>[128] - prefab cleanup traversal scratch - owner: HectonDevToolsMenu
        private static readonly List<Transform> s_transformScratch = new List<Transform>(128);

        [MenuItem(MenuRoot + "Reveal Persistent Data Path", false, 10)]
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

        [MenuItem(MenuRoot + "Clear All PlayerPrefs (this machine)...", false, 30)]
        public static void ClearPlayerPrefs()
        {
            if (!EditorUtility.DisplayDialog(
                    "Hecton Dev",
                    "Delete all PlayerPrefs for this Unity user and editor?\n\n" +
                    "This cannot be undone.",
                    "Clear",
                    "Cancel"))
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
            sb.AppendLine("-- Hecton Dev - Build info --");
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
                sb.Append("  - ").AppendLine(defines[i]);
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
            SpatialAudioManager sam = UnityEngine.Object.FindAnyObjectByType<SpatialAudioManager>(
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
                FindObjectsInactive.Exclude);
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

        private const string ScreenshotsFolderName = "Docs/Screenshots";

        [MenuItem(MenuRoot + "Capture Screenshot -> Docs/Screenshots (Play Mode)", false, 70)]
        public static void CaptureScreenshotToProject()
        {
            string screenshotsFolder = ResolveScreenshotsFolderPath();
            Directory.CreateDirectory(screenshotsFolder);

            string fileName = $"screenshot-{DateTime.Now:yyyyMMdd-HHmmss}.png";
            string absolutePath = Path.Combine(screenshotsFolder, fileName);
            ScreenCapture.CaptureScreenshot(absolutePath);
            Debug.Log("[Hecton Dev] Capturing screenshot to: " + absolutePath.Replace('\\', '/'));
        }

        [MenuItem(MenuRoot + "Capture Screenshot -> Docs/Screenshots (Play Mode)", true)]
        public static bool CaptureScreenshotValidate()
        {
            return EditorApplication.isPlaying;
        }

        private static string ResolveScreenshotsFolderPath()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
                return ScreenshotsFolderName;

            return Path.Combine(projectRoot, ScreenshotsFolderName);
        }

        // Project Settings (Unity 6 SettingsService paths)

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

        // Scene & assets

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
            sb.AppendLine("-- Hecton Dev - Loaded scenes --");
            for (int i = 0; i < scenes.Length; i++)
            {
                Scene s = scenes[i];
                sb.Append("  - ").Append(s.name);
                if (!string.IsNullOrEmpty(s.path))
                {
                    sb.Append("  (").Append(s.path).Append(')');
                }

                sb.AppendLine();
            }

            SpatialAudioManager[] sams = UnityEngine.Object.FindObjectsByType<SpatialAudioManager>(
                FindObjectsInactive.Include);
            sb.Append("SpatialAudioManager count: ").Append(sams.Length).AppendLine();
            if (sams.Length > 1)
            {
                Debug.LogWarning("[Hecton Dev] Multiple SpatialAudioManager instances; allowed only during scene swaps. Check DontDestroyOnLoad.");
            }
            else if (sams.Length == 0)
            {
                Debug.LogWarning("[Hecton Dev] SpatialAudioManager not found in loaded scenes (may be OK in a clean sandbox).");
            }

            int missingTotal = LogMissingScriptsInLoadedScenes();
            sb.Append("Missing script components: ").Append(missingTotal).AppendLine();

            Debug.Log(sb.ToString());
        }

        [MenuItem(MenuRoot + "Scene/Remove Missing Scripts In Loaded Scenes", false, 123)]
        public static void RemoveMissingScriptsInLoadedScenes()
        {
            Scene[] scenes = GetLoadedScenesSnapshot();
            if (scenes.Length == 0)
            {
                Debug.Log("[Hecton Dev] No loaded scenes.");
                return;
            }

            int gameObjectsTouched = 0;
            int removedComponents = 0;
            GameObject[] gos = UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include);

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

                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (missing <= 0)
                {
                    continue;
                }

                Undo.RegisterCompleteObjectUndo(go, "Remove missing scripts");
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                gameObjectsTouched++;
                removedComponents += missing;
                EditorSceneManager.MarkSceneDirty(sc);
                Debug.Log(
                    "[Hecton Dev] Removed missing script (" + missing + "): " + BuildTransformPath(go.transform),
                    go);
            }

            AssetDatabase.SaveAssets();
            if (gameObjectsTouched > 0)
            {
                Debug.Log(
                    "[Hecton Dev] Removed " + removedComponents + " missing script components across " +
                    gameObjectsTouched + " GameObjects.");
            }
            else
            {
                Debug.Log("[Hecton Dev] No missing scripts found in loaded scenes.");
            }
        }

        [MenuItem(MenuRoot + "Scene/Remove Missing Scripts In _Project Prefabs", false, 124)]
        public static void RemoveMissingScriptsInProjectPrefabs()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project", "Assets/_Project/Prefabs" });
            if (prefabGuids == null || prefabGuids.Length == 0)
            {
                Debug.Log("[Hecton Dev] No prefabs found under Assets/_Project.");
                return;
            }

            int prefabCount = 0;
            int gameObjectsTouched = 0;
            int removedComponents = 0;

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                GameObject root = null;
                bool dirty = false;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    if (root == null)
                    {
                        continue;
                    }

                    List<Transform> transforms = s_transformScratch;
                    transforms.Clear();
                    CollectTransforms(root.transform, transforms);

                    for (int t = 0; t < transforms.Count; t++)
                    {
                        GameObject go = transforms[t] != null ? transforms[t].gameObject : null;
                        if (go == null)
                        {
                            continue;
                        }

                        int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                        if (missing <= 0)
                        {
                            continue;
                        }

                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                        gameObjectsTouched++;
                        removedComponents += missing;
                        dirty = true;
                        Debug.Log("[Hecton Dev] Removed missing script (" + missing + ") from prefab: " + path + " :: " + BuildTransformPath(go.transform));
                    }

                    if (dirty)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        prefabCount++;
                    }
                }
                finally
                {
                    s_transformScratch.Clear();
                    if (root != null)
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (removedComponents > 0)
            {
                Debug.Log(
                    "[Hecton Dev] Removed " + removedComponents + " missing script components from " +
                    gameObjectsTouched + " GameObjects across " + prefabCount + " prefabs.");
            }
            else
            {
                Debug.Log("[Hecton Dev] No missing scripts found in _Project prefabs.");
            }
        }

        [MenuItem(MenuRoot + "Scene/Run World Generative Geology Smoke (Play Mode)", false, 125)]
        public static void RunWorldGenerativeGeologySmoke()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Hecton Dev] Geology smoke can only run in play mode.");
                return;
            }

            WorldGenerativeGeologyRuntimeSmokeTester tester =
                UnityEngine.Object.FindAnyObjectByType<WorldGenerativeGeologyRuntimeSmokeTester>(
                    FindObjectsInactive.Include);

            if (tester == null)
            {
                Scene activeScene = SceneManager.GetActiveScene();
                if (!activeScene.IsValid() || !activeScene.isLoaded)
                {
                    Debug.LogWarning("[Hecton Dev] No active loaded scene for geology smoke.");
                    return;
                }

                GameObject parent = FindRootGameObject(activeScene, "--- SYSTEMS ---");
                GameObject host = new GameObject("__DEV_GeologySmoke");
                host.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                SceneManager.MoveGameObjectToScene(host, activeScene);
                if (parent != null)
                {
                    host.transform.SetParent(parent.transform, false);
                }

                tester = host.AddComponent<WorldGenerativeGeologyRuntimeSmokeTester>();
            }
            else if (tester.gameObject.name.StartsWith("__DEV_", StringComparison.Ordinal))
            {
                tester.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }

            tester.ConfigureForDevRun(
                enableVerboseLogging: true,
                enableSuppressionRestore: true,
                timeoutSeconds: 24f,
                startupDelaySeconds: 0.35f,
                settleDelaySeconds: 0.2f,
                preferVoxel: true,
                preferTerrain: true);

            if (!tester.TryRunImmediately())
            {
                Debug.LogWarning("[Hecton Dev] Geology smoke is already running. " + tester.DescribeStatus());
                return;
            }

            FocusDevRuntimeObject(tester.gameObject);
            Debug.Log("[Hecton Dev] Started world generative geology smoke pass.");
        }

        [MenuItem(MenuRoot + "Scene/Run World Generative Geology Smoke (Play Mode)", true)]
        public static bool RunWorldGenerativeGeologySmokeValidate()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem(MenuRoot + "Scene/Log World Generative Geology Smoke Status (Play Mode)", false, 126)]
        public static void LogWorldGenerativeGeologySmokeStatus()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Hecton Dev] Geology smoke status is only available in play mode.");
                return;
            }

            WorldGenerativeGeologyRuntimeSmokeTester tester =
                UnityEngine.Object.FindAnyObjectByType<WorldGenerativeGeologyRuntimeSmokeTester>(
                    FindObjectsInactive.Include);
            if (tester == null)
            {
                Debug.LogWarning("[Hecton Dev] No WorldGenerativeGeologyRuntimeSmokeTester found.");
                return;
            }

            Debug.Log("[Hecton Dev] Geology smoke status: " + tester.DescribeStatus(), tester.gameObject);
            FocusDevRuntimeObject(tester.gameObject);
        }

        [MenuItem(MenuRoot + "Scene/Log World Generative Geology Smoke Status (Play Mode)", true)]
        public static bool LogWorldGenerativeGeologySmokeStatusValidate()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem(MenuRoot + "Scene/Run Runtime Performance Profiler (Play Mode)", false, 127)]
        public static void RunRuntimePerformanceProfiler()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Hecton Dev] Runtime performance profiler can only run in play mode.");
                return;
            }

            PlayModePerformanceMonitor.Start(
                sampleWindowSeconds: 2f,
                logBudgetViolations: true,
                logEveryWindow: true);
            Debug.Log("[Hecton Dev] Started runtime performance profiler.");
        }

        [MenuItem(MenuRoot + "Scene/Arm Runtime Profiler Auto-Bootstrap (Next Play Mode)", false, 127)]
        public static void ArmRuntimePerformanceProfilerAutoBootstrap()
        {
            RuntimePerformanceProfiler.ArmAutoBootstrapForNextPlayMode();
            Debug.Log("[Hecton Dev] Armed runtime profiler auto-bootstrap for the next play mode entry.");
        }

        [MenuItem(MenuRoot + "Scene/Run Runtime Performance Profiler (Play Mode)", true)]
        public static bool RunRuntimePerformanceProfilerValidate()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem(MenuRoot + "Scene/Log Runtime Performance Profiler Status (Play Mode)", false, 128)]
        public static void LogRuntimePerformanceProfilerStatus()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Hecton Dev] Runtime performance profiler status is only available in play mode.");
                return;
            }

            if (!PlayModePerformanceMonitor.IsActive)
            {
                Debug.LogWarning("[Hecton Dev] Runtime performance profiler is not active.");
                return;
            }

            PlayModePerformanceMonitor.LogStatus();
        }

        [MenuItem(MenuRoot + "Scene/Log Runtime Performance Profiler Status (Play Mode)", true)]
        public static bool LogRuntimePerformanceProfilerStatusValidate()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem(MenuRoot + "Scene/Log Runtime Performance Profiler Counters (Play Mode)", false, 129)]
        public static void LogRuntimePerformanceProfilerCounters()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Hecton Dev] Runtime performance profiler counters are only available in play mode.");
                return;
            }

            PlayModePerformanceMonitor.LogAvailableCounters();
        }

        [MenuItem(MenuRoot + "Scene/Log Runtime Performance Profiler Counters (Play Mode)", true)]
        public static bool LogRuntimePerformanceProfilerCountersValidate()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem(MenuRoot + "Scene/Stop Runtime Performance Profiler (Play Mode)", false, 130)]
        public static void StopRuntimePerformanceProfiler()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Hecton Dev] Runtime performance profiler can only be stopped in play mode.");
                return;
            }

            if (!PlayModePerformanceMonitor.IsActive)
            {
                Debug.LogWarning("[Hecton Dev] Runtime performance profiler is not active.");
                return;
            }

            PlayModePerformanceMonitor.Stop();
            Debug.Log("[Hecton Dev] Stopped runtime performance profiler.");
        }

        [MenuItem(MenuRoot + "Scene/Stop Runtime Performance Profiler (Play Mode)", true)]
        public static bool StopRuntimePerformanceProfilerValidate()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem(MenuRoot + "Scene/Run Tool Runtime Smoke (Play Mode)", false, 131)]
        public static void RunToolRuntimeSmoke()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Hecton Dev] Tool smoke can only run in play mode.");
                return;
            }

            ToolRuntimeSmokeTester tester = FindOrCreateToolRuntimeSmokeTester();
            if (tester == null)
                return;

            tester.ConfigureForDevRun(
                enableVerboseLogging: true,
                restoreLoadoutAfterRun: true,
                startupDelaySeconds: 0.2f,
                equipTimeoutSeconds: 1.5f,
                settleDelaySeconds: 0.1f,
                betweenToolsDelaySeconds: 0.05f,
                simulatedDeltaSeconds: 0.1f);

            if (!tester.TryRunImmediately())
            {
                Debug.LogWarning("[Hecton Dev] Tool smoke is already running. " + tester.DescribeStatus());
                return;
            }

            FocusDevRuntimeObject(tester.gameObject);
            Debug.Log("[Hecton Dev] Started tool runtime smoke pass.");
        }

        [MenuItem(MenuRoot + "Scene/Run Tool Runtime Smoke (Play Mode)", true)]
        public static bool RunToolRuntimeSmokeValidate()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem(MenuRoot + "Scene/Log Tool Runtime Smoke Status (Play Mode)", false, 132)]
        public static void LogToolRuntimeSmokeStatus()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Hecton Dev] Tool smoke status is only available in play mode.");
                return;
            }

            ToolRuntimeSmokeTester tester =
                UnityEngine.Object.FindAnyObjectByType<ToolRuntimeSmokeTester>(FindObjectsInactive.Include);
            if (tester == null)
            {
                Debug.LogWarning("[Hecton Dev] No ToolRuntimeSmokeTester found.");
                return;
            }

            Debug.Log("[Hecton Dev] Tool smoke status: " + tester.DescribeStatus(), tester.gameObject);
            FocusDevRuntimeObject(tester.gameObject);
        }

        [MenuItem(MenuRoot + "Scene/Log Tool Runtime Smoke Status (Play Mode)", true)]
        public static bool LogToolRuntimeSmokeStatusValidate()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem(MenuRoot + "Scene/Start Optimization Audit (Play Mode)", false, 133)]
        public static void StartOptimizationAudit()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Hecton Dev] Optimization audit can only run in play mode.");
                return;
            }

            PlayModeOptimizationAudit.Start(
                startRuntimeProfiler: true,
                sampleWindowSeconds: 2f,
                logEveryProfilerWindow: true);
        }

        [MenuItem(MenuRoot + "Scene/Start Optimization Audit (Play Mode)", true)]
        public static bool StartOptimizationAuditValidate()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem(MenuRoot + "Scene/Log Optimization Audit Summary (Play Mode)", false, 134)]
        public static void LogOptimizationAuditSummary()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Hecton Dev] Optimization audit summary is only available in play mode.");
                return;
            }

            if (!PlayModeOptimizationAudit.IsActive)
            {
                Debug.LogWarning("[Hecton Dev] Optimization audit is not active.");
                return;
            }

            PlayModeOptimizationAudit.LogSummary();
        }

        [MenuItem(MenuRoot + "Scene/Log Optimization Audit Summary (Play Mode)", true)]
        public static bool LogOptimizationAuditSummaryValidate()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem(MenuRoot + "Scene/Stop Optimization Audit (Play Mode)", false, 135)]
        public static void StopOptimizationAudit()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Hecton Dev] Optimization audit can only be stopped in play mode.");
                return;
            }

            if (!PlayModeOptimizationAudit.IsActive)
            {
                Debug.LogWarning("[Hecton Dev] Optimization audit is not active.");
                return;
            }

            PlayModeOptimizationAudit.Stop(logSummary: true);
        }

        [MenuItem(MenuRoot + "Scene/Stop Optimization Audit (Play Mode)", true)]
        public static bool StopOptimizationAuditValidate()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem(MenuRoot + "Scene/Run Tool Smoke + Optimization Audit (Play Mode)", false, 136)]
        public static void RunToolSmokeWithOptimizationAudit()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Hecton Dev] Tool smoke audit can only run in play mode.");
                return;
            }

            PlayModeOptimizationAudit.Start(
                startRuntimeProfiler: true,
                sampleWindowSeconds: 2f,
                logEveryProfilerWindow: true);
            RunToolRuntimeSmoke();
        }

        [MenuItem(MenuRoot + "Scene/Run Tool Smoke + Optimization Audit (Play Mode)", true)]
        public static bool RunToolSmokeWithOptimizationAuditValidate()
        {
            return EditorApplication.isPlaying;
        }

        private static void FocusDevRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            // Runtime smoke helpers already appear as context objects in the console log.
            // Avoid forcing editor selection during play mode while automation is driving
            // the session, as this can destabilize MCP observation.
            if (EditorApplication.isPlaying)
                return;

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
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

        private static ToolRuntimeSmokeTester FindOrCreateToolRuntimeSmokeTester()
        {
            ToolRuntimeSmokeTester tester =
                UnityEngine.Object.FindAnyObjectByType<ToolRuntimeSmokeTester>(FindObjectsInactive.Include);
            if (tester != null)
            {
                if (tester.gameObject.name.StartsWith("__DEV_", StringComparison.Ordinal))
                    tester.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

                return tester;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogWarning("[Hecton Dev] No active loaded scene for tool smoke.");
                return null;
            }

            GameObject parent = FindRootGameObject(activeScene, "--- SYSTEMS ---");
            GameObject host = new GameObject("__DEV_ToolSmoke");
            host.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            SceneManager.MoveGameObjectToScene(host, activeScene);
            if (parent != null)
                host.transform.SetParent(parent.transform, false);

            return host.AddComponent<ToolRuntimeSmokeTester>();
        }

        /// <summary>Preduprezhdeniya s ping obektov; vozvraschaet chislo missing *components*.</summary>
        private static int LogMissingScriptsInLoadedScenes()
        {
            GameObject[] gos = UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include);

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

        private static void CollectTransforms(Transform root, List<Transform> destination)
        {
            if (root == null || destination == null)
            {
                return;
            }

            destination.Add(root);
            for (int i = 0; i < root.childCount; i++)
            {
                CollectTransforms(root.GetChild(i), destination);
            }
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

        private static GameObject FindRootGameObject(Scene scene, string name)
        {
            if (!scene.IsValid() || string.IsNullOrEmpty(name))
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root != null && string.Equals(root.name, name, StringComparison.Ordinal))
                {
                    return root;
                }
            }

            return null;
        }

    }
}
