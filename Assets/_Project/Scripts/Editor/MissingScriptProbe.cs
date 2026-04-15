using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    [InitializeOnLoad]
    internal static class MissingScriptProbe
    {
        private const int LoadedObjectRescanFrames = 120;
        private const int AssetScanLogLimit = 64;
        private static readonly string[] AssetScanRoots = { "Assets" };
        private static readonly StringBuilder ReportBuilder = new StringBuilder(512);
        private static readonly HashSet<string> ReportedKeys = new HashSet<string>();
        private static bool _prefabAssetScanCompleted;
        private static int _remainingPlayModeRescanFrames;

        static MissingScriptProbe()
        {
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorSceneManager.sceneOpened += HandleSceneOpened;
            EditorApplication.update += HandleEditorUpdate;
            EditorApplication.delayCall += ScanPrefabAssetsOnce;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                ScanLoadedObjects("entered-play-mode");
                _remainingPlayModeRescanFrames = LoadedObjectRescanFrames;
                ScanPrefabAssetsOnce();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                _remainingPlayModeRescanFrames = 0;
            }
        }

        private static void HandleSceneOpened(Scene scene, OpenSceneMode mode)
        {
            ScanLoadedObjects($"scene-opened:{scene.name}");
        }

        private static void HandleEditorUpdate()
        {
            if (_remainingPlayModeRescanFrames <= 0)
                return;

            if (!EditorApplication.isPlaying)
            {
                _remainingPlayModeRescanFrames = 0;
                return;
            }

            _remainingPlayModeRescanFrames--;
            ScanLoadedObjects($"play-update:{LoadedObjectRescanFrames - _remainingPlayModeRescanFrames}");
        }

        private static void ScanLoadedObjects(string reason)
        {
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject gameObject = objects[i];
                if (gameObject == null)
                    continue;

                int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                if (missingCount <= 0)
                    continue;

                ReportBuilder.Clear();
                ReportBuilder.Append("[MissingScriptProbe] reason=")
                    .Append(reason)
                    .Append(" scene=");

                Scene scene = gameObject.scene;
                ReportBuilder.Append(scene.IsValid() ? scene.name : "DontDestroyOnLoad");
                ReportBuilder.Append(" path=")
                    .Append(GetHierarchyPath(gameObject.transform))
                    .Append(" missingCount=")
                    .Append(missingCount);

                if (!TryRegisterReport(ReportBuilder.ToString()))
                    continue;

                Debug.LogError(ReportBuilder.ToString(), gameObject);
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
            ScanPrefabAssets("prefab-asset-scan");
        }

        private static void ScanPrefabAssets(string reason)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", AssetScanRoots);
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
            else
            {
                Debug.Log("[MissingScriptProbe] No missing scripts found in prefab assets under Assets.");
            }
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
    }
}
