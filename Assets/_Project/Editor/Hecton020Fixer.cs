// Hecton020Fixer.cs
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class Hecton020Fixer
    {
        public static void FixSandboxScene()
        {
            Debug.Log("[Hecton020Fixer] Starting fix for 020_RENDER_SANDBOX...");

            string scenePath = "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity";

            try
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                
                // Find Player Spawner
                var spawnerGO = GameObject.Find("Player Spawner");
                if (spawnerGO != null)
                {
                    var spawner = spawnerGO.GetComponent("HectonPlayerSpawner"); // Use reflection or direct if accessible
                    if (spawner != null)
                    {
                        var propPrefab = new SerializedObject(spawner).FindProperty("productionPlayerPrefab");
                        
                        if (propPrefab != null && propPrefab.objectReferenceValue == null)
                        {
                            string prefabPath = "Assets/_Project/Prefabs/Player/Production Player.prefab";
                            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                            if (prefab != null)
                            {
                                propPrefab.objectReferenceValue = prefab;
                                propPrefab.serializedObject.ApplyModifiedProperties();
                                Debug.Log($"[Hecton020Fixer] Assigned productionPlayerPrefab!");
                                EditorUtility.SetDirty(spawner);
                            }
                            else
                            {
                                Debug.LogError($"[Hecton020Fixer] Could not find prefab at {prefabPath}");
                            }
                        }
                        else
                        {
                            Debug.Log($"[Hecton020Fixer] prefab already assigned: {propPrefab?.objectReferenceValue}");
                        }
                    }
                }
                else
                {
                    Debug.LogError("[Hecton020Fixer] Player Spawner not found!");
                }

                // Add AegirSky if missing
                var celestialGO = GameObject.Find("CelestialEngine");
                if (celestialGO == null)
                {
                    celestialGO = new GameObject("CelestialEngine");
                    celestialGO.AddComponent<Hecton8.Celestial.HectonCelestialEngine>();
                    Debug.Log("[Hecton020Fixer] Added CelestialEngine to scene.");
                    EditorSceneManager.MarkSceneDirty(scene);
                }

                bool saved = EditorSceneManager.SaveScene(scene, scenePath);
                
                if (saved)
                    Debug.Log("[Hecton020Fixer] SUCCESS: Scene saved.");
                else
                    Debug.LogError("[Hecton020Fixer] FAILED to save scene.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Hecton020Fixer] EXCEPTION: {ex.Message}");
            }

            Debug.Log("[Hecton020Fixer] Done.");
            EditorApplication.Exit(0);
        }
    }
}
