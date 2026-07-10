using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Hecton8.Tools;

namespace Hecton8.EditorTools
{
    public static class H8_ScreenshotTaker_PlayMode
    {
        public static void TakeScreenshotAndExit()
        {
            string scenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
            
            Debug.Log($"[H8ScreenshotPlayMode] Opening scene: {scenePath}");
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            FixCelestialAndLight();

            // Inject the PlayMode Screenshotter in the unsaved editor scene state only.
            var go = new GameObject("H8_PlayModeScreenshotter");
            go.AddComponent<H8_PlayModeScreenshotter>();

            Debug.Log("[H8ScreenshotPlayMode] Entering Play Mode...");
            EditorApplication.isPlaying = true;
        }

        private static void FixCelestialAndLight()
        {
            var celestial = Object.FindAnyObjectByType<Hecton8.Celestial.HectonCelestialEngine>();
            if (celestial == null)
            {
                var go = new GameObject("HectonCelestialEngine");
                celestial = go.AddComponent<Hecton8.Celestial.HectonCelestialEngine>();
                Debug.Log("[H8ScreenshotPlayMode] Added missing CelestialEngine.");
            }

            var orch = Object.FindAnyObjectByType<Hecton8.Graphics.HectonVisualsOrchestrator>();
            if (orch == null)
            {
                var go = new GameObject("HectonVisualsOrchestrator");
                orch = go.AddComponent<Hecton8.Graphics.HectonVisualsOrchestrator>();
            }

            var orchSO = new SerializedObject(orch);
            orchSO.FindProperty("_celestialEngine").objectReferenceValue = celestial;
            orchSO.FindProperty("_oceanMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Crest/Crest/Materials/Ocean.mat");
            orchSO.ApplyModifiedProperties();

            Light sunLight = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l.type == LightType.Directional)
                {
                    sunLight = l;
                    l.shadows = LightShadows.Soft;
                    break;
                }
            }

            if (sunLight == null)
            {
                var sunGO = new GameObject("Directional Light (Sun)");
                sunLight = sunGO.AddComponent<Light>();
                sunLight.type = LightType.Directional;
            }
            sunLight.shadows = LightShadows.Soft;
            sunLight.intensity = 1.5f;
            sunLight.color = Color.white;
            sunLight.transform.rotation = Quaternion.Euler(45, -30, 0);

            var celSO = new SerializedObject(celestial);
            celSO.FindProperty("sunLight").objectReferenceValue = sunLight;
            celSO.FindProperty("aegirFallbackMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Sky/Hecton_AegirSky_Mat.mat");
            celSO.ApplyModifiedProperties();
            
        }
    }
}
