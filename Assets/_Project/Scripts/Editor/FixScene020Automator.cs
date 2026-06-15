#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    public static class FixScene020Automator
    {
        [MenuItem("Hecton8/Prepare Sandbox Scene 020")]
        public static void PrepareSandboxScene()
        {
            string scenePath = "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity";
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Debug.Log($"[FixScene020Automator] Opened scene: {scenePath}");

            // 3. Directional Light
            GameObject sunGo = GameObject.Find("Sun_DirectionalLight");
            if (sunGo == null)
            {
                sunGo = new GameObject("Sun_DirectionalLight");
                Light light = sunGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 2.0f;
                sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                Debug.Log("[FixScene020Automator] Created Sun_DirectionalLight");
            }
            Light sunLight = sunGo.GetComponent<Light>();
            RenderSettings.sun = sunLight;

            // 4. Camera
            GameObject camGo = GameObject.Find("Main Camera");
            if (camGo == null)
            {
                camGo = new GameObject("Main Camera");
                camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
                Debug.Log("[FixScene020Automator] Created Main Camera");
            }
            if (camGo.transform.position.magnitude < 10f)
            {
                camGo.transform.position = new Vector3(5000f, 1000f, 5000f);
                Debug.Log("[FixScene020Automator] Moved Camera to (5000, 1000, 5000)");
            }
            Camera cam = camGo.GetComponent<Camera>();
            if (cam.farClipPlane < 10000f)
            {
                cam.farClipPlane = 10000f;
                Debug.Log("[FixScene020Automator] Set Camera far clip to 10000");
            }

            // 5. RenderSettings
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            Material skyboxMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Sky/Mat_HectonSky.mat");
            if (skyboxMat != null)
            {
                RenderSettings.skybox = skyboxMat;
                Debug.Log("[FixScene020Automator] Assigned Mat_HectonSky to skybox");
            }

            // 6. Shader globals
            Shader.SetGlobalVector("_SunDirection", new Vector4(0.5f, 0.8f, 0.3f, 0f).normalized);
            Shader.SetGlobalFloat("_HectonTimeOfDay01", 0.5f);
            Debug.Log("[FixScene020Automator] Set Shader globals");

            // 7. Crest OceanRenderer
            GameObject oceanGo = GameObject.Find("OceanRenderer");
            if (oceanGo != null)
            {
                var oceanType = System.Type.GetType("Crest.OceanRenderer, Crest");
                if (oceanType != null)
                {
                    var ocean = oceanGo.GetComponent(oceanType);
                    if (ocean != null)
                    {
                        var propCam = oceanType.GetProperty("ViewCamera", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        if (propCam != null) propCam.SetValue(ocean, cam);
                        
                        var propMat = oceanType.GetField("_material", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        if (propMat != null)
                        {
                            Material oceanMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Crest/Crest/Shaders/Ocean.mat");
                            if (oceanMat != null) propMat.SetValue(ocean, oceanMat);
                        }
                        Debug.Log("[FixScene020Automator] Assigned Crest OceanRenderer properties");
                    }
                }
                else
                {
                    Debug.LogWarning("[FixScene020Automator] Could not find Crest.OceanRenderer type via reflection!");
                }
            }
            else
            {
                Debug.LogWarning("[FixScene020Automator] Could not find OceanRenderer GameObject!");
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[FixScene020Automator] Saved scene: {scenePath}");
        }
    }
}
#endif
