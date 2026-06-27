using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class YamlConvert
    {
        public static void Convert020ToYaml()
        {
            string path = "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity";
            Debug.Log($"[YamlConvert] Opening {path} for YAML conversion...");
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            
            // EditorSettings is set to ForceText in project
            bool saved = EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[YamlConvert] Saved {path}. Success: {saved}");
            
            EditorApplication.Exit(0);
        }
    }
}
