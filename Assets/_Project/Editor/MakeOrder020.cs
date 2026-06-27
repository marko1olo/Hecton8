using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;

namespace Hecton8.EditorTools
{
    public static class MakeOrder020
    {
        public static void AnalyzeAndFix()
        {
            string path = "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity";
            Debug.Log($"[MakeOrder020] Opening {path}...");
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            
            Debug.Log($"[MakeOrder020] Root objects in 020:");
            foreach (var go in scene.GetRootGameObjects())
            {
                Debug.Log($" - {go.name} (Components: {go.GetComponents<Component>().Length})");
            }

            // Force the serialization mode to text to be absolutely sure
            EditorSettings.serializationMode = SerializationMode.ForceText;
            
            // Mark the scene dirty so it actually saves
            EditorSceneManager.MarkSceneDirty(scene);
            
            bool saved = EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[MakeOrder020] Saved {path} as Text. Success: {saved}");
            
            // Also force reserialize
            AssetDatabase.ForceReserializeAssets(new string[] { path }, ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata);
            Debug.Log($"[MakeOrder020] Force reserialized {path}.");

            EditorApplication.Exit(0);
        }
    }
}
