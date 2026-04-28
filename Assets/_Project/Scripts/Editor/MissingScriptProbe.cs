using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    internal static class MissingScriptProbe
    {
        private const string CleanupMenuPath = "Tools/HECTON-8/Maintenance/Cleanup Missing Scripts (_Project + Loaded Scenes)";
        private const string ScanMenuPath = "Tools/HECTON-8/Maintenance/Scan Missing Scripts (_Project + Loaded Scenes)";
        private const string ExhaustiveScanMenuPath = "Tools/HECTON-8/Maintenance/Scan Missing Scripts (All Assets + Hidden Runtime)";
        private const int LoadedObjectRescanPassCount = 6;
        private const double LoadedObjectRescanIntervalSeconds = 0.5d;
        private const int AssetScanLogLimit = 64;
        private static readonly string[] AssetScanRoots = { "Assets/_Project" };
        private static readonly string[] ExhaustiveAssetScanRoots = { "Assets" };
        private static readonly StringBuilder ReportBuilder = new StringBuilder(512);
        private static readonly HashSet<string> ReportedKeys = new HashSet<string>();
        private static bool _prefabAssetScanCompleted;
        private static int _remainingPlayModeRescanPasses;
        private static double _nextPlayModeRescanAt;

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                ScanLoadedObjects("entered-play-mode", includeHiddenObjects: true);
                _remainingPlayModeRescanPasses = LoadedObjectRescanPassCount;
                _nextPlayModeRescanAt = EditorApplication.timeSinceStartup + LoadedObjectRescanIntervalSeconds;
                ScanPrefabAssetsOnce();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                _remainingPlayModeRescanPasses = 0;
                _nextPlayModeRescanAt = 0d;
            }
        }

        private static void HandleSceneOpened(Scene scene, OpenSceneMode mode)
        {
            ScanLoadedObjects($"scene-opened:{scene.name}", includeHiddenObjects: false);
        }

        private static void HandleEditorUpdate()
        {
            if (_remainingPlayModeRescanPasses <= 0)
                return;

            if (!EditorApplication.isPlaying)
            {
                _remainingPlayModeRescanPasses = 0;
                _nextPlayModeRescanAt = 0d;
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < _nextPlayModeRescanAt)
                return;

            int completedPassIndex = (LoadedObjectRescanPassCount - _remainingPlayModeRescanPasses) + 1;
            _remainingPlayModeRescanPasses--;
            _nextPlayModeRescanAt = now + LoadedObjectRescanIntervalSeconds;
            ScanLoadedObjects($"play-rescan:{completedPassIndex}", includeHiddenObjects: true);
        }

        private static void ScanLoadedObjects(string reason, bool includeHiddenObjects)
        {
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            int loggedObjectCount = 0;
            int totalObjectCount = 0;
            int totalMissingScripts = 0;
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject gameObject = objects[i];
                if (gameObject == null)
                    continue;

                if (!includeHiddenObjects && gameObject.hideFlags != HideFlags.None)
                    continue;

                Scene scene = gameObject.scene;
                bool isSceneObject = scene.IsValid() && scene.isLoaded;
                bool isDontDestroyObject = scene.IsValid() && string.Equals(scene.name, "DontDestroyOnLoad", global::System.StringComparison.Ordinal);
                if (!isSceneObject && !isDontDestroyObject)
                    continue;

                int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                if (missingCount <= 0)
                    continue;

                totalObjectCount++;
                totalMissingScripts += missingCount;

                ReportBuilder.Clear();
                ReportBuilder.Append("[MissingScriptProbe] reason=")
                    .Append(reason)
                    .Append(" scene=");

                ReportBuilder.Append(scene.IsValid() ? scene.name : "DontDestroyOnLoad");
                ReportBuilder.Append(" path=")
                    .Append(GetHierarchyPath(gameObject.transform))
                    .Append(" missingCount=")
                    .Append(missingCount)
                    .Append(" hideFlags=")
                    .Append(gameObject.hideFlags)
                    .Append(" activeSelf=")
                    .Append(gameObject.activeSelf ? 1 : 0)
                    .Append(" activeInHierarchy=")
                    .Append(gameObject.activeInHierarchy ? 1 : 0)
                    .Append(" entityID=")
                    .Append(EntityId.ToULong(gameObject.GetEntityId()));

                if (!TryRegisterReport(ReportBuilder.ToString()))
                    continue;

                loggedObjectCount++;
                Debug.LogError(ReportBuilder.ToString(), gameObject);
            }

            if (totalMissingScripts > 0)
            {
                Debug.LogWarning(
                    "[MissingScriptProbe] Loaded-object scan found " +
                    totalMissingScripts +
                    " missing scripts across " +
                    totalObjectCount +
                    " objects. loggedObjects=" +
                    loggedObjectCount +
                    " includeHidden=" +
                    (includeHiddenObjects ? 1 : 0));
            }
        }

        private static void ScanPrefabAssetsOnce()
        {
            if (_prefabAssetScanCompleted)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ScanPrefabAssetsOnce;
                return;
            }

            _prefabAssetScanCompleted = true;
            ScanPrefabAssets("prefab-asset-scan", AssetScanRoots);
        }

        [MenuItem(CleanupMenuPath)]
        private static void CleanupMissingScripts()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogWarning("[MissingScriptProbe] Cleanup skipped because the editor is compiling or updating.");
                return;
            }

            CleanupSummary summary = default;
            summary.Reason = "manual-cleanup";
            ReportedKeys.Clear();

            try
            {
                CleanupPrefabAssets(ref summary);
                CleanupLoadedScenes(ref summary);

                if (summary.PrefabAssetsChanged > 0)
                    AssetDatabase.SaveAssets();

                if (summary.SceneObjectsChanged > 0)
                    EditorSceneManager.SaveOpenScenes();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log(
                "[MissingScriptProbe] Cleanup complete. " +
                "prefabAssetsChanged=" + summary.PrefabAssetsChanged +
                " prefabObjectsChanged=" + summary.PrefabObjectsChanged +
                " prefabScriptsRemoved=" + summary.PrefabScriptsRemoved +
                " sceneObjectsChanged=" + summary.SceneObjectsChanged +
                " sceneScriptsRemoved=" + summary.SceneScriptsRemoved);
        }

        [MenuItem(ScanMenuPath)]
        private static void ManualScanMissingScripts()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogWarning("[MissingScriptProbe] Scan skipped because the editor is compiling or updating.");
                return;
            }

            ReportedKeys.Clear();
            ScanPrefabAssets("manual-scan", AssetScanRoots);
            ScanLoadedObjects("manual-scan", includeHiddenObjects: false);
        }

        [MenuItem(ExhaustiveScanMenuPath)]
        private static void ManualExhaustiveScanMissingScripts()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Debug.LogWarning("[MissingScriptProbe] Exhaustive scan skipped because the editor is compiling or updating.");
                return;
            }

            ReportedKeys.Clear();
            ScanPrefabAssets("manual-exhaustive-scan", ExhaustiveAssetScanRoots);
            ScanLoadedObjects("manual-exhaustive-scan", includeHiddenObjects: true);
        }

        private static void ScanPrefabAssets(string reason, string[] assetRoots)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", assetRoots);
            int loggedObjectCount = 0;
            int totalObjectCount = 0;
            int totalMissingScripts = 0;

            for (int guidIndex = 0; guidIndex < prefabGuids.Length; guidIndex++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(prefabGuids[guidIndex]);
                GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefabRoot == null)
                    continue;

                Transform[] transforms = prefabRoot.GetComponentsInChildren<Transform>(true);
                for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                {
                    Transform currentTransform = transforms[transformIndex];
                    if (currentTransform == null)
                        continue;

                    int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(currentTransform.gameObject);
                    if (missingCount <= 0)
                        continue;

                    totalObjectCount++;
                    totalMissingScripts += missingCount;

                    if (loggedObjectCount >= AssetScanLogLimit)
                        continue;

                    loggedObjectCount++;
                    ReportBuilder.Clear();
                    ReportBuilder.Append("[MissingScriptProbe] reason=")
                        .Append(reason)
                        .Append(" asset=")
                        .Append(assetPath)
                        .Append(" path=")
                        .Append(GetHierarchyPath(currentTransform))
                        .Append(" missingCount=")
                        .Append(missingCount);

                    if (!TryRegisterReport(ReportBuilder.ToString()))
                        continue;

                    Debug.LogError(ReportBuilder.ToString(), prefabRoot);
                }
            }

            if (totalMissingScripts > 0)
            {
                Debug.LogWarning(
                    $"[MissingScriptProbe] Prefab scan found {totalMissingScripts} missing scripts across {totalObjectCount} objects. " +
                    $"Logged first {Mathf.Min(loggedObjectCount, AssetScanLogLimit)} objects.");
            }
        }

        private static void CleanupPrefabAssets(ref CleanupSummary summary)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", AssetScanRoots);
            for (int guidIndex = 0; guidIndex < prefabGuids.Length; guidIndex++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(prefabGuids[guidIndex]);
                if (string.IsNullOrEmpty(assetPath))
                    continue;

                EditorUtility.DisplayProgressBar(
                    "Missing Script Cleanup",
                    "Prefab: " + assetPath,
                    prefabGuids.Length > 0 ? (guidIndex + 1f) / prefabGuids.Length : 1f);

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
                if (prefabRoot == null)
                    continue;

                bool prefabChanged = false;
                try
                {
                    Transform[] transforms = prefabRoot.GetComponentsInChildren<Transform>(true);
                    for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                    {
                        Transform currentTransform = transforms[transformIndex];
                        if (currentTransform == null)
                            continue;

                        int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(currentTransform.gameObject);
                        if (removed <= 0)
                            continue;

                        prefabChanged = true;
                        summary.PrefabObjectsChanged++;
                        summary.PrefabScriptsRemoved += removed;
                        LogCleanupEntry("prefab-cleanup", assetPath, GetHierarchyPath(currentTransform), removed);
                    }

                    if (prefabChanged)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
                        summary.PrefabAssetsChanged++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        private static void CleanupLoadedScenes(ref CleanupSummary summary)
        {
            int loadedSceneCount = SceneManager.sceneCount;
            for (int sceneIndex = 0; sceneIndex < loadedSceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
                    continue;

                EditorUtility.DisplayProgressBar(
                    "Missing Script Cleanup",
                    "Scene: " + scene.path,
                    loadedSceneCount > 0 ? (sceneIndex + 1f) / loadedSceneCount : 1f);

                bool sceneChanged = false;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    GameObject root = roots[rootIndex];
                    if (root == null)
                        continue;

                    Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                    for (int transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                    {
                        Transform currentTransform = transforms[transformIndex];
                        if (currentTransform == null)
                            continue;

                        int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(currentTransform.gameObject);
                        if (removed <= 0)
                            continue;

                        sceneChanged = true;
                        summary.SceneObjectsChanged++;
                        summary.SceneScriptsRemoved += removed;
                        LogCleanupEntry("scene-cleanup", scene.path, GetHierarchyPath(currentTransform), removed);
                    }
                }

                if (sceneChanged)
                    EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private static void LogCleanupEntry(string reason, string assetOrScenePath, string hierarchyPath, int removed)
        {
            ReportBuilder.Clear();
            ReportBuilder.Append("[MissingScriptProbe] reason=")
                .Append(reason)
                .Append(" target=")
                .Append(assetOrScenePath)
                .Append(" path=")
                .Append(hierarchyPath)
                .Append(" removed=")
                .Append(removed);

            string report = ReportBuilder.ToString();
            if (TryRegisterReport(report))
                Debug.Log(report);
        }

        private static bool TryRegisterReport(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            return ReportedKeys.Add(key);
        }

        private static string GetHierarchyPath(Transform target)
        {
            if (target == null)
                return "<null>";

            StringBuilder pathBuilder = new StringBuilder(256);
            AppendHierarchyPath(target, pathBuilder);
            return pathBuilder.ToString();
        }

        private static void AppendHierarchyPath(Transform target, StringBuilder builder)
        {
            if (target.parent != null)
            {
                AppendHierarchyPath(target.parent, builder);
                builder.Append('/');
            }

            builder.Append(target.name);
        }

        private struct CleanupSummary
        {
            public string Reason;
            public int PrefabAssetsChanged;
            public int PrefabObjectsChanged;
            public int PrefabScriptsRemoved;
            public int SceneObjectsChanged;
            public int SceneScriptsRemoved;
        }
    }
}
