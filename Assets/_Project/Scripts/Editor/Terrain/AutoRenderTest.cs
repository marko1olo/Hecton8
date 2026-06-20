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
            
            // First, load or create the master material and inject the arrays PERMANENTLY
            Material customMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/MATERIALS/Terrain/HectonTerrainMaterial.mat");
            if (customMat == null)
            {
                Shader s = Shader.Find("Hecton8/URP/Terrain_TextureArray");
                customMat = new Material(s);
                customMat.name = "HectonTerrainMaterial";
                AssetDatabase.CreateAsset(customMat, "Assets/_Project/Art/MATERIALS/Terrain/HectonTerrainMaterial.mat");
            }

            Texture2DArray albedo = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_AlbedoArray.asset");
            Texture2DArray normal = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_NormalArray.asset");
            Texture2DArray mask = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_MaskArray.asset");

            if (albedo != null) customMat.SetTexture("_AlbedoArray", albedo);
            if (normal != null) customMat.SetTexture("_NormalArray", normal);
            if (mask != null) customMat.SetTexture("_MaskArray", mask);
            customMat.SetFloat("_UVScale", 4.0f);
            customMat.SetFloat("_TriplanarBlend", 4.0f);
            customMat.SetFloat("_MinDepth", -4600f);
            customMat.SetFloat("_MaxDepth", 500f);
            
            EditorUtility.SetDirty(customMat);
            AssetDatabase.SaveAssets();

            // Load Scene
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX.unity");
            
            // Find MapMagic and force generate
            var mmObject = Object.FindObjectOfType<MapMagic.Core.MapMagicObject>();
            if (mmObject != null)
            {
                // Change seed to force a completely fresh evaluation
                foreach (var gen in mmObject.graph.generators)
                {
                    if (gen is MapMagic.Nodes.MatrixGenerators.HectonSandboxAbyssalShelfMapMagicNode node)
                    {
                        node.seed += 1;
                    }
                }
                
                mmObject.Refresh(true);
            }

            // Let's use Coroutine to wait for generation, but since we're in Editor, we can't easily wait.
            // MapMagic has synchronous generation if forced or we just wait using EditorApplication.update.
            EditorApplication.update += CheckGeneration;
        }

        static double startTime = 0;
        static Texture2DArray s_albedoArray;
        static Texture2DArray s_normalArray;
        static Texture2DArray s_maskArray;

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

            if (hasTerrain && EditorApplication.timeSinceStartup - startTime > 100.0)
            {
                // Wait an extra 100 seconds after first terrain to let it finish
                Debug.Log("[AutoRenderTest] Terrain generated! Injecting material and taking screenshots.");
                
                Material customMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/MATERIALS/Terrain/HectonTerrainMaterial.mat");
                
                if (s_albedoArray != null) customMat.SetTexture("_AlbedoArray", s_albedoArray);
                if (s_normalArray != null) customMat.SetTexture("_NormalArray", s_normalArray);
                if (s_maskArray != null) customMat.SetTexture("_MaskArray", s_maskArray);

                foreach (var t in UnityEngine.Terrain.activeTerrains)
                {
                    var inj = t.GetComponent<Hecton8.World.HectonTerrainMaterialInjector>();
                    if (inj == null)
                    {
                        inj = t.gameObject.AddComponent<Hecton8.World.HectonTerrainMaterialInjector>();
                        inj.customTerrainMaterial = customMat;
                    }
                    else
                    {
                        inj.customTerrainMaterial = customMat;
                    }
                    
                    // Force the injector to re-initialize its instanced material with the new customMat
                    var instancedMatField = typeof(Hecton8.World.HectonTerrainMaterialInjector).GetField("_instancedMaterial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (instancedMatField != null)
                    {
                        var oldMat = instancedMatField.GetValue(inj) as Material;
                        if (oldMat != null) Object.DestroyImmediate(oldMat);
                        instancedMatField.SetValue(inj, null);
                    }
                    
                    inj.ForceUpdate();
                }
                
                foreach (var t in UnityEngine.Terrain.activeTerrains)
                {
                    var terrainMat = t.materialTemplate;
                    if (terrainMat != null)
                    {
                        if (s_albedoArray) terrainMat.SetTexture("_AlbedoArray", s_albedoArray);
                        if (s_normalArray) terrainMat.SetTexture("_NormalArray", s_normalArray);
                        if (s_maskArray) terrainMat.SetTexture("_MaskArray", s_maskArray);
                    }
                }

                var terrains = UnityEngine.Terrain.activeTerrains;
                if (terrains.Length > 0 && terrains[0].terrainData.alphamapTextures.Length > 0)
                {
                    Texture2D control = terrains[0].terrainData.alphamapTextures[0];
                    RenderTexture tmp = RenderTexture.GetTemporary(control.width, control.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                    UnityEngine.Graphics.Blit(control, tmp);
                    Texture2D readable = new Texture2D(control.width, control.height, TextureFormat.RGBA32, false, true);
                    RenderTexture.active = tmp;
                    readable.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
                    readable.Apply();
                    RenderTexture.active = null;
                    RenderTexture.ReleaseTemporary(tmp);
                    byte[] bytes = readable.EncodeToPNG();
                    System.IO.File.WriteAllBytes("C:/hades/Hecton8/Logs/Control_Dump.png", bytes);
                    Debug.Log($"[AutoRenderTest] Dumped _Control to Logs/Control_Dump.png ({control.width}x{control.height})");
                }

                EditorApplication.update -= CheckGeneration;
                TakeScreenshots();
                EditorApplication.Exit(0);
            }
            else if (EditorApplication.timeSinceStartup - startTime > 120.0)
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
                // DEBUG: EXPORT SPLATMAP AND LOG MATERIAL STATUS OF FIRST TERRAIN
                var t0 = terrains[0];
                var mat = t0.materialTemplate;
                Debug.Log($"[AutoRenderTest] Terrain0 Material: {(mat != null ? mat.name : "NULL")}");
                if (mat != null) {
                    Debug.Log($"[AutoRenderTest] _Control bound: {mat.GetTexture("_Control") != null}");
                    Debug.Log($"[AutoRenderTest] _AlbedoArray bound: {mat.GetTexture("_AlbedoArray") != null}");
                }
                
                if (t0.terrainData != null && t0.terrainData.alphamapTextureCount > 0) {
                    var tex = t0.terrainData.alphamapTextures[0];
                    if (tex != null) {
                        var t2d = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                        RenderTexture currentRT = RenderTexture.active;
                        RenderTexture renderTexture = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
                        UnityEngine.Graphics.Blit(tex, renderTexture);
                        RenderTexture.active = renderTexture;
                        t2d.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                        t2d.Apply();
                        RenderTexture.active = currentRT;
                        RenderTexture.ReleaseTemporary(renderTexture);
                        System.IO.File.WriteAllBytes("C:/hades/Hecton8/Logs/Splatmap_0.png", t2d.EncodeToPNG());
                        Debug.Log("[AutoRenderTest] Exported Splatmap_0.png");
                    }
                }
                
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
            
            float centerHeight = terrains != null && terrains.Length > 0 ? terrains[0].SampleHeight(center) + terrains[0].transform.position.y : 0;
            Vector3 surfaceCenter = new Vector3(center.x, centerHeight, center.z);
            
            // Orbit
            cam.transform.position = surfaceCenter + new Vector3(maxExtents * 0.8f, maxExtents * 0.4f, -maxExtents * 0.8f);
            cam.transform.LookAt(surfaceCenter);
            RenderScreenshot("Orbit.png", cam);

            // Level
            cam.transform.position = surfaceCenter + new Vector3(0, 5, -maxExtents * 0.2f);
            cam.transform.LookAt(surfaceCenter + new Vector3(0, 5, 0));
            RenderScreenshot("Level.png", cam);

            // Rock
            cam.transform.position = surfaceCenter + new Vector3(maxExtents * 0.2f, 10, maxExtents * 0.2f);
            cam.transform.LookAt(surfaceCenter + new Vector3(maxExtents * 0.4f, 2, maxExtents * 0.4f));
            RenderScreenshot("Rock.png", cam);

            // Depth
            cam.transform.position = surfaceCenter + new Vector3(0, -500, -maxExtents * 0.2f);
            cam.transform.LookAt(surfaceCenter + new Vector3(0, -500, 0));
            RenderScreenshot("Depth.png", cam);

            // Shallow
            cam.transform.position = surfaceCenter + new Vector3(0, -10, -maxExtents * 0.8f);
            cam.transform.LookAt(surfaceCenter);
            RenderScreenshot("Shallow.png", cam);

            // Macro (Close up to see textures)
            cam.transform.position = surfaceCenter + new Vector3(0, 10, 0);
            cam.transform.LookAt(surfaceCenter + new Vector3(10, 0, 10));
            RenderScreenshot("Macro.png", cam);
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
