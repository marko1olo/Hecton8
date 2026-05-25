#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hecton8.Core;

namespace Hecton8.Editor
{
    /// <summary>
    /// Editor-only enforcement for the production bootstrap entry point.
    /// </summary>
    [InitializeOnLoad]
    internal static class BootstrapPlayModeEntryGuard
    {
        private const string BootstrapSceneName = "00_BOOTSTRAP";
        private const string BootstrapScenePath = "Assets/_Project/Scenes/00_BOOTSTRAP.unity";

        static BootstrapPlayModeEntryGuard()
        {
            if (Application.isBatchMode)
                return;

            EnsurePlayModeStartSceneConfigured();

            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (Application.isBatchMode)
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            if (state != PlayModeStateChange.ExitingEditMode)
                return;

            EnsurePlayModeStartSceneConfigured();

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
                return;

            if (string.Equals(activeScene.name, BootstrapSceneName, System.StringComparison.Ordinal))
                return;

            if (EditorSceneManager.playModeStartScene != null)
                return;

            H8Debug.LogError(
                $"[BootstrapPlayModeEntryGuard] Play Mode blocked. Active scene '{activeScene.name}' violates the bootstrap contract and '{BootstrapScenePath}' could not be resolved.");

            EditorApplication.isPlaying = false;
        }

        private static void EnsurePlayModeStartSceneConfigured()
        {
            SceneAsset bootstrapScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath);
            if (bootstrapScene == null)
                return;

            if (EditorSceneManager.playModeStartScene != bootstrapScene)
                EditorSceneManager.playModeStartScene = bootstrapScene;
        }
    }
}
#endif
