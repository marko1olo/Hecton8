using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using Hecton8.Graphics;

namespace Hecton8.EditorTools
{
    public static class Rescue02
    {
        public static void FixWorld()
        {
            string path = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
            Debug.Log($"[Rescue02] Opening {path}");
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            
            HectonVisualsConfigurator.ConfigureVisuals02();
            
            var orchestrator = Object.FindAnyObjectByType<HectonVisualsOrchestrator>();
            if (orchestrator == null)
            {
                GameObject orchGO = new GameObject("HectonVisualsOrchestrator");
                orchestrator = orchGO.AddComponent<HectonVisualsOrchestrator>();
                Debug.Log("[Rescue02] Added missing HectonVisualsOrchestrator.");
            }

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

            bool hasDirectional = false;
            var allLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            foreach (var l in allLights)
            {
                if (l.type == LightType.Directional)
                {
                    hasDirectional = true;
                    l.shadows = LightShadows.Soft;
                    Debug.Log($"[Rescue02] Fixed existing Directional Light '{l.name}' (Soft Shadows).");
                }
            }

            if (!hasDirectional)
            {
                GameObject sunGO = new GameObject("Directional Light (Moon/Sun)");
                var sun = sunGO.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
                sun.intensity = 0.5f;
                sun.color = new Color(0.6f, 0.75f, 1f);
                sunGO.transform.rotation = Quaternion.Euler(50, -30, 0);
                Debug.Log("[Rescue02] Created new Directional Light for illumination.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[Rescue02] Scene saved: {saved}");
            
            EditorApplication.Exit(0);
        }
    }
}
