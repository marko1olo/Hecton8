// Hecton02Fixer.cs
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hecton8.Editor.SaveSystem
{
    public static class Hecton02Fixer
    {
        public static void FixWorldScene()
        {
            Debug.Log("[Hecton02Fixer] Starting fix for 020_RENDER_SANDBOX...");

            string scenePath = "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity";

            try
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                
                // Fix pickup stable IDs
                WorldPickupStableIdScanResult scanResult = WorldPickupStateAuthoringValidator.ScanOpenScenePickupStableIds(repair: true, scenePath);
                Debug.Log($"[Hecton02Fixer] Repaired {scanResult.RepairedCount} pickup stable IDs.");

                bool saved = EditorSceneManager.SaveScene(scene, scenePath);
                
                if (saved)
                    Debug.Log("[Hecton02Fixer] SUCCESS: Scene saved.");
                else
                    Debug.LogError("[Hecton02Fixer] FAILED to save scene.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Hecton02Fixer] EXCEPTION: {ex.Message}");
            }

            Debug.Log("[Hecton02Fixer] Done.");
            EditorApplication.Exit(0);
        }
    }
}
