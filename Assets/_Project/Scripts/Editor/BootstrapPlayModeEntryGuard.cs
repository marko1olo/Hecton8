#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    /// <summary>
    /// Editor-only enforcement for the production bootstrap entry point.
    /// </summary>
    [InitializeOnLoad]
    internal static class BootstrapPlayModeEntryGuard
    {
        private const string BootstrapSceneName = "00_BOOTSTRAP";

        static BootstrapPlayModeEntryGuard()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            if (state != PlayModeStateChange.ExitingEditMode)
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
                return;

            if (string.Equals(activeScene.name, BootstrapSceneName, System.StringComparison.Ordinal))
                return;

            Debug.LogError(
                $"[BootstrapPlayModeEntryGuard] Play Mode blocked. Active scene '{activeScene.name}' violates the bootstrap contract. " +
                $"Open '{BootstrapSceneName}' and enter Play Mode through the production route.");

            EditorApplication.isPlaying = false;
        }
    }
}
#endif
