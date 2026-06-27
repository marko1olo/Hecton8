// H8SceneTextConverter.cs
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class H8SceneTextConverter
    {
        public static void ConvertSandboxToText()
        {
            Debug.Log("[H8SceneTextConverter] Starting ForceText conversion of 020 via Open+Save...");

            EditorSettings.serializationMode = SerializationMode.ForceText;
            string scenePath = "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity";

            try
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                bool saved = EditorSceneManager.SaveScene(scene, scenePath);
                
                if (saved)
                    Debug.Log("[H8SceneTextConverter] SUCCESS: 020_RENDER_SANDBOX saved as ForceText YAML.");
                else
                    Debug.LogError("[H8SceneTextConverter] FAILED to save scene.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[H8SceneTextConverter] EXCEPTION: {ex.Message}");
            }

            Debug.Log("[H8SceneTextConverter] Done.");
            EditorApplication.Exit(0);
        }
    }
}
