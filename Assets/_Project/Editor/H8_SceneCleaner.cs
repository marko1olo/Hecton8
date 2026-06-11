using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

namespace Hecton8.EditorTools
{
    public static class H8_SceneCleaner
    {
        public static void CleanWorldScene()
        {
            string scenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
            Debug.Log($"[SceneCleaner] Opening scene: {scenePath}");
            Scene scene = EditorSceneManager.OpenScene(scenePath);
            
            GameObject deprecatedParent = GameObject.Find("DEPRECATED_STUFF");
            if (deprecatedParent == null)
            {
                deprecatedParent = new GameObject("DEPRECATED_STUFF");
                deprecatedParent.SetActive(false);
            }
            
            GameObject[] rootObjects = scene.GetRootGameObjects();
            int movedCount = 0;
            foreach (var go in rootObjects)
            {
                if (go == deprecatedParent) continue;
                
                string name = go.name.ToUpperInvariant();
                
                // Keep Terrain
                if (name.Contains("TERRAIN")) continue;
                // Keep Camera / Player / Light / Ocean for visual proof
                if (name.Contains("CAMERA") || name.Contains("PLAYER") || name.Contains("LIGHT") || name.Contains("OCEAN") || name.Contains("WATER") || name.Contains("SUN") || name.Contains("SKY") || name.Contains("ATMOSPHERE")) continue;
                // Keep core systems so the game doesn't crash on load
                if (name.Contains("SYSTEM") || name.Contains("MANAGER") || name.Contains("DIRECTOR") || name.Contains("REGISTRY") || name.Contains("BOOTSTRAP")) continue;

                Debug.Log($"[SceneCleaner] Moving {go.name} to DEPRECATED_STUFF and disabling.");
                go.transform.SetParent(deprecatedParent.transform);
                go.SetActive(false);
                movedCount++;
            }
            
            Debug.Log($"[SceneCleaner] Moved {movedCount} root objects to DEPRECATED_STUFF.");
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SceneCleaner] Scene saved.");
            EditorApplication.Exit(0);
        }
    }
}
