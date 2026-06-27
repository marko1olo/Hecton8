// HectonVisualsConfigurator.cs
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Hecton8.Celestial;
using System.Reflection;
using System.Linq;

namespace Hecton8.EditorTools
{
    public static class HectonVisualsConfigurator
    {
        public static void ConfigureVisuals020()
        {
            Debug.Log("[Visuals] Configuring 020_RENDER_SANDBOX...");
            ConfigureScene("Assets/_Project/Scenes/020_RENDER_SANDBOX.unity");
            Debug.Log("[Visuals] Done.");
        }

        public static void ConfigureVisuals02()
        {
            Debug.Log("[Visuals] Configuring 02_HECTON_WORLD...");
            ConfigureScene("Assets/_Project/Scenes/02_HECTON_WORLD.unity");
            Debug.Log("[Visuals] Done.");
        }

        private static void ConfigureScene(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 1. Ocean Material
            MonoBehaviour oceanRenderer = null;
            var allMonos = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mono in allMonos)
            {
                if (mono != null && mono.GetType().Name == "OceanRenderer")
                {
                    oceanRenderer = mono;
                    break;
                }
            }

            if (oceanRenderer != null)
            {
                string matPath = "Assets/Crest/Crest/Materials/Ocean.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat != null)
                {
                    var prop = oceanRenderer.GetType().GetProperty("Material");
                    if (prop != null)
                    {
                        prop.SetValue(oceanRenderer, mat);
                        EditorUtility.SetDirty(oceanRenderer);
                        Debug.Log("[Visuals] Set OceanRenderer material via reflection.");
                    }
                    
                    mat.SetColor("_ScatterColourBase", new Color(0.05f, 0.45f, 0.45f));
                    mat.SetColor("_ScatterColourShallow", new Color(0.15f, 0.75f, 0.7f));
                    mat.SetFloat("_ScatterColourShallowDepthMax", 10f);
                    EditorUtility.SetDirty(mat);
                }
                else
                {
                    Debug.LogError("[Visuals] Could not find Ocean.mat!");
                }
            }
            else
            {
                Debug.LogWarning("[Visuals] No OceanRenderer component found in the scene.");
            }

            // 2. Celestial Engine (Gas Giant + Sky)
            var celestial = Object.FindAnyObjectByType<HectonCelestialEngine>();
            if (celestial != null)
            {
                var so = new SerializedObject(celestial);
                
                var profileProp = so.FindProperty("aegirSkyProjection");
                if (profileProp != null && profileProp.objectReferenceValue != null)
                {
                    var pSo = new SerializedObject(profileProp.objectReferenceValue);
                    var radiusProp = pSo.FindProperty("planetCenterRadius");
                    if (radiusProp != null) radiusProp.floatValue = 15f;
                    pSo.ApplyModifiedProperties();
                }

                var sunProp = so.FindProperty("sunLight");
                if (sunProp != null && sunProp.objectReferenceValue != null)
                {
                    var sunLight = sunProp.objectReferenceValue as Light;
                    if (sunLight != null)
                    {
                        sunLight.intensity = 1.2f;
                        sunLight.color = new Color(1f, 0.95f, 0.9f);
                        EditorUtility.SetDirty(sunLight);
                    }
                }

                so.ApplyModifiedProperties();
                Debug.Log("[Visuals] Configured Celestial Engine.");
            }
            else
            {
                Debug.LogWarning("[Visuals] No CelestialEngine component found in the scene.");
            }

            bool saved = EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[Visuals] Scene save: {saved}");
        }
    }
}
