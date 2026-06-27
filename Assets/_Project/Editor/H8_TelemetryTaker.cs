
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class H8_TelemetryTaker
    {
        private const float TimeoutSeconds = 120f;
        private static float _timeoutStart;

        // Entry point for 02_HECTON_WORLD (legacy main world)
        public static void RunAndExit()
        {
            RunScene("Assets/_Project/Scenes/02_HECTON_WORLD.unity");
        }

        // Entry point for 020_RENDER_SANDBOX (newest clean world)
        public static void RunAndExitSandbox()
        {
            RunScene("Assets/_Project/Scenes/020_RENDER_SANDBOX.unity");
        }

        private static void RunScene(string scenePath)
        {
            Debug.Log($"[TelemetryTaker] Opening scene: {scenePath}");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.isPlaying = true;
            // Start timeout watchdog via update loop
            _timeoutStart = (float)EditorApplication.timeSinceStartup;
            EditorApplication.update += TimeoutWatchdog;
        }

        private static void TimeoutWatchdog()
        {
            float elapsed = (float)EditorApplication.timeSinceStartup - _timeoutStart;
            if (elapsed >= TimeoutSeconds)
            {
                EditorApplication.update -= TimeoutWatchdog;
                Debug.LogWarning($"[TelemetryTaker] Timeout after {TimeoutSeconds}s — forcing exit.");
                EditorApplication.Exit(1);
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= TimeoutWatchdog;
                EditorApplication.Exit(0);
            }
        }
    }
}
