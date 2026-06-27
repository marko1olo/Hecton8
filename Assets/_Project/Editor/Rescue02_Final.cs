using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using Hecton8.Graphics;
using Hecton8.Celestial;

namespace Hecton8.EditorTools
{
    public static class Rescue02_Final
    {
        public static void FixWorld()
        {
            string path = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
            Debug.Log($"[Rescue02] Opening {path}");
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            
            // 1. Celestial Engine
            var celestial = Object.FindAnyObjectByType<HectonCelestialEngine>();
            if (celestial == null)
            {
                GameObject celGO = new GameObject("HectonCelestialEngine");
                celestial = celGO.AddComponent<HectonCelestialEngine>();
                Debug.Log("[Rescue02] Added missing HectonCelestialEngine.");
            }
            
            // 2. Orchestrator
            var orchestrator = Object.FindAnyObjectByType<HectonVisualsOrchestrator>();
            if (orchestrator == null)
            {
                GameObject orchGO = new GameObject("HectonVisualsOrchestrator");
                orchestrator = orchGO.AddComponent<HectonVisualsOrchestrator>();
                Debug.Log("[Rescue02] Added missing HectonVisualsOrchestrator.");
            }
            var orchSO = new SerializedObject(orchestrator);
            orchSO.FindProperty("_celestialEngine").objectReferenceValue = celestial;
            orchSO.FindProperty("_oceanMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Crest/Crest/Materials/Ocean.mat");
            orchSO.ApplyModifiedProperties();

            // 3. Illumination Doctrine (Directional Light)
            Light sunLight = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l.type == LightType.Directional)
                {
                    sunLight = l;
                    l.shadows = LightShadows.Soft;
                    Debug.Log($"[Rescue02] Fixed existing Directional Light '{l.name}' (Soft Shadows).");
                    break;
                }
            }

            if (sunLight == null)
            {
                GameObject sunGO = new GameObject("Directional Light (Moon/Sun)");
                sunLight = sunGO.AddComponent<Light>();
                sunLight.type = LightType.Directional;
                sunLight.shadows = LightShadows.Soft;
                sunLight.intensity = 0.5f;
                sunLight.color = new Color(0.6f, 0.75f, 1f);
                sunGO.transform.rotation = Quaternion.Euler(50, -30, 0);
                Debug.Log("[Rescue02] Created new Directional Light for illumination.");
            }

            // Assign sun to CelestialEngine
            var celSO = new SerializedObject(celestial);
            celSO.FindProperty("sunLight").objectReferenceValue = sunLight;
            celSO.FindProperty("aegirFallbackMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Sky/Hecton_AegirSky_Mat.mat");
            celSO.ApplyModifiedProperties();

            // 4. Player Spawner & Bootstrapper timeout
            var spawner = Object.FindAnyObjectByType<HectonPlayerSpawner>();
            if (spawner != null)
            {
                Rigidbody playerRb = null;
                var players = GameObject.FindGameObjectsWithTag("Player");
                if (players.Length > 0) playerRb = players[0].GetComponentInChildren<Rigidbody>();
                
                if (playerRb == null)
                {
                    var pGO = GameObject.Find("Player");
                    if (pGO == null) pGO = GameObject.Find("HectonPlayer");
                    if (pGO != null) playerRb = pGO.GetComponentInChildren<Rigidbody>();
                }
                
                if (playerRb != null)
                {
                    var so = new SerializedObject(spawner);
                    so.FindProperty("playerRigidbody").objectReferenceValue = playerRb;
                    so.ApplyModifiedProperties();
                    Debug.Log($"[Rescue02] Found Player Rigidbody '{playerRb.name}' and assigned to Spawner.");
                }
                else 
                {
                    Debug.LogWarning("[Rescue02] Could not find any Player Rigidbody in the scene! The spawner will instantiate the prefab.");
                }
            }
            else
            {
                Debug.LogWarning("[Rescue02] HectonPlayerSpawner not found in 02_HECTON_WORLD!");
            }

            // 5. Camera Check
            var cam = Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                GameObject camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
                cam = camGO.AddComponent<Camera>();
                camGO.AddComponent<UniversalAdditionalCameraData>();
                Debug.Log("[Rescue02] Created missing Main Camera.");
            }
            else
            {
                if (cam.GetComponent<UniversalAdditionalCameraData>() == null)
                {
                    cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                    Debug.Log($"[Rescue02] Added UniversalAdditionalCameraData to {cam.name}.");
                }
            }

            // ACES Tonemapping
            var volume = Object.FindAnyObjectByType<Volume>();
            if (volume == null)
            {
                GameObject volGO = new GameObject("Global Volume");
                volume = volGO.AddComponent<Volume>();
                volume.isGlobal = true;
                Debug.Log("[Rescue02] Created Global Volume.");
            }

            // Apply binary config!
            orchestrator.LoadAndApplyVisuals();

            // Make sure everything is dirty!
            EditorUtility.SetDirty(celestial);
            EditorUtility.SetDirty(orchestrator);
            if (spawner != null) EditorUtility.SetDirty(spawner);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[Rescue02] Scene saved: {saved}");
            
            // Render screenshot
            H8_ScreenshotTaker.TakeSceneScreenshot("02_RESCUED", path);
            
            EditorApplication.Exit(0);
        }
    }
}
