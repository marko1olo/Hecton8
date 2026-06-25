using System.IO;
using UnityEngine;
using UnityEditor;

namespace Hecton8.Editor
{
    public static class AegirSkyTest
    {
        private const string ArtifactDir = "C:/Users/danat/.gemini/antigravity/brain/9412af70-ebf5-491e-80e6-e0b2fcde1017/";

        [MenuItem("Hecton8/Tests/Aegir Sky Render")]
        public static void Execute()
        {
            try
            {
                Material skyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Sky/Hecton_AegirSky_Mat.mat");
                if (skyMat == null)
                {
                    // Create it if it doesn't exist
                    Shader shader = Shader.Find("HECTON/Sky/Hecton_AegirSky");
                    if (shader != null)
                    {
                        skyMat = new Material(shader);
                        Texture2D bandTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Art/Textures/Generated/GeminiBiomeMaterialIntake_20260607/AegirBands.png");
                        if (bandTex != null) skyMat.SetTexture("_AegirBandTex", bandTex);
                        AssetDatabase.CreateAsset(skyMat, "Assets/_Project/Art/Materials/Sky/Hecton_AegirSky_Mat.mat");
                    }
                }

                RenderSettings.skybox = skyMat;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = Color.white;

                // Setup Aegir Global properties
                Shader.SetGlobalVector("_H8AegirPlanetCenterRadius", new Vector4(0, 5000, 50000, 30000));
                Shader.SetGlobalVector("_H8AegirSunDirection", new Vector4(1, 0.5f, -0.5f, 0).normalized);
                Shader.SetGlobalVector("_H8AegirRingPlaneInner", new Vector4(0, 1, 0, 40000));
                Shader.SetGlobalVector("_H8AegirOrbitScalars", new Vector4(80000, 0.5f, 1.0f, 1.0f)); // quality = 1.0 (HIGH)
                Shader.SetGlobalFloat("_H8GlobalQualityWeight", 1.0f);

                GameObject go = new GameObject("SkyCam");
                Camera cam = go.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.fieldOfView = 60f;
                // Look at the planet
                cam.transform.position = Vector3.zero;
                cam.transform.LookAt(new Vector3(0, 5000, 50000));

                RenderTexture rt = new RenderTexture(1920, 1080, 24);
                cam.targetTexture = rt;
                Texture2D tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                tex.Apply();
                cam.targetTexture = null;
                RenderTexture.active = null;
                
                File.WriteAllBytes(ArtifactDir + "AegirSkyView.png", tex.EncodeToPNG());

                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(go);

                Debug.Log("[AegirSkyTest] Rendered sky successfully.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (System.Exception ex)
            {
                Debug.LogError(ex);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
