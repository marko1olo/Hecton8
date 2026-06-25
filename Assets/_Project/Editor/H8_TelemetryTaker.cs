using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class H8_TelemetryTaker
    {
        public static void RunAndExit()
        {
            Debug.Log("[TelemetryTaker] Opening 02_HECTON_WORLD in Editor mode...");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/02_HECTON_WORLD.unity");
            
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.Exit(0);
            }
        }
    }
}
