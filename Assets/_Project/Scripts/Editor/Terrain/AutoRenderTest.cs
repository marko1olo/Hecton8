using UnityEditor;
using UnityEngine;
using System.Collections;
using UnityEditor.SceneManagement;

namespace Hecton8.Editor.Terrain
{
    public static class AutoRenderTest
    {
        public static void Run()
        {
            Debug.Log("[AutoRenderTest] Starting automated render test...");
            
            // Create Material
            Shader shader = Shader.Find("Hecton8/URP/Terrain_TextureArray");
            if (shader == null)
            {
                Debug.LogError("[AutoRenderTest] Shader not found!");
                EditorApplication.Exit(1);
                return;
            }

            Material mat = new Material(shader);
            mat.name = "HectonTerrainMaterial";
            
            // Load Texture Arrays
            Texture2DArray albedoArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_AlbedoArray.asset");
            Texture2DArray normalArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_NormalArray.asset");
            Texture2DArray maskArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_MaskArray.asset");
            
            if (albedoArray) mat.SetTexture("_AlbedoArray", albedoArray);
            if (normalArray) mat.SetTexture("_NormalArray", normalArray);
            if (maskArray) mat.SetTexture("_MaskArray", maskArray);
            mat.SetFloat("_UVScale", 4.0f);
            mat.SetFloat("_TriplanarBlend", 4.0f);
            mat.SetFloat("_MinDepth", -4600f);
            mat.SetFloat("_MaxDepth", 500f);
            
            AssetDatabase.CreateAsset(mat, "Assets/_Project/Art/MATERIALS/Terrain/HectonTerrainMaterial.mat");
            AssetDatabase.SaveAssets();

            // Load Scene
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX.unity");
            
            // Find MapMagic and force generate
            var mmObject = Object.FindObjectOfType<MapMagic.Core.MapMagicObject>();
            if (mmObject != null)
            {
                mmObject.StartGenerate();
            }

            // Let's use Coroutine to wait for generation, but since we're in Editor, we can't easily wait.
            // MapMagic has synchronous generation if forced or we just wait using EditorApplication.update.
            EditorApplication.update += CheckGeneration;
        }

        static double startTime = 0;
        static void CheckGeneration()
        {
            if (startTime == 0) startTime = EditorApplication.timeSinceStartup;
            
            // MapMagic uses background threads. We wait until Terrain has alphamaps or timeout
            bool hasTerrain = false;
            foreach (var t in UnityEngine.Terrain.activeTerrains)
            {
                if (t.terrainData != null && t.terrainData.alphamapTextureCount > 0)
                {
                    hasTerrain = true;
                    break;
                }
            }

            if (hasTerrain && EditorApplication.timeSinceStartup - startTime > 5.0)
            {
                // Wait an extra 5 seconds after first terrain to let it finish
                Debug.Log("[AutoRenderTest] Terrain generated! Injecting material and taking screenshots.");
                
                Material customMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/MATERIALS/Terrain/HectonTerrainMaterial.mat");
                foreach (var t in UnityEngine.Terrain.activeTerrains)
                {
                    var inj = t.GetComponent<Hecton8.World.HectonTerrainMaterialInjector>();
                    if (inj == null)
                    {
                        inj = t.gameObject.AddComponent<Hecton8.World.HectonTerrainMaterialInjector>();
                        inj.customTerrainMaterial = customMat;
                    }
                    inj.ForceUpdate();
                }

                EditorApplication.update -= CheckGeneration;
                TakeScreenshots();
                EditorApplication.Exit(0);
            }
            else if (EditorApplication.timeSinceStartup - startTime > 45.0)
            {
                Debug.Log("[AutoRenderTest] Timeout waiting for Terrain.");
                EditorApplication.update -= CheckGeneration;
                TakeScreenshots();
                EditorApplication.Exit(1);
            }
        }
        
        static void TakeScreenshots()
        {
            Debug.Log("[AutoRenderTest] Taking screenshots...");

            var terrains = UnityEngine.Terrain.activeTerrains;
            Vector3 center = Vector3.zero;
            float maxExtents = 1000f;

            if (terrains != null && terrains.Length > 0)
            {
                Bounds b = new Bounds(terrains[0].transform.position, Vector3.zero);
                foreach (var t in terrains)
                {
                    b.Encapsulate(t.transform.position);
                    b.Encapsulate(t.transform.position + new Vector3(t.terrainData.size.x, t.terrainData.size.y, t.terrainData.size.z));
                }
                center = b.center;
                maxExtents = Mathf.Max(b.extents.x, b.extents.z);
                Debug.Log($"[AutoRenderTest] Found {terrains.Length} terrains. Center: {center}, Extents: {maxExtents}");
            }
            else
            {
                Debug.Log("[AutoRenderTest] No active terrains found!");
            }

            var camGo = new GameObject("RenderCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.backgroundColor = new Color(0.1f, 0.2f, 0.3f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.farClipPlane = 50000f;
            
            // Orbit
            cam.transform.position = center + new Vector3(maxExtents * 1.2f, maxExtents * 0.8f, -maxExtents * 1.2f);
            cam.transform.LookAt(center);
            RenderScreenshot("Orbit.png", cam);

            // Level
            cam.transform.position = center + new Vector3(0, 50, -maxExtents * 0.5f);
            cam.transform.LookAt(center);
            RenderScreenshot("Level.png", cam);

            // Rock
            cam.transform.position = center + new Vector3(maxExtents * 0.2f, 100, maxExtents * 0.2f);
            cam.transform.LookAt(center + new Vector3(maxExtents * 0.4f, 50, maxExtents * 0.4f));
            RenderScreenshot("Rock.png", cam);

            // Depth
            cam.transform.position = center + new Vector3(0, -2000, -maxExtents * 0.2f);
            cam.transform.LookAt(center + new Vector3(0, -2000, 0));
            RenderScreenshot("Depth.png", cam);

            // Shallow
            cam.transform.position = center + new Vector3(0, -50, -maxExtents * 0.8f);
            cam.transform.LookAt(center);
            RenderScreenshot("Shallow.png", cam);
        }

        static void RenderScreenshot(string name, Camera cam)
        {
            RenderTexture rt = new RenderTexture(1920, 1080, 24);
            cam.targetTexture = rt;
            Texture2D screenShot = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            cam.Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            byte[] bytes = screenShot.EncodeToPNG();
            System.IO.File.WriteAllBytes("C:/hades/Hecton8/Logs/" + name, bytes);
        }
    }
}
