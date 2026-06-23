using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using MapMagic.Core;
using Den.Tools;

namespace Hecton8.Editor
{
    public static class TerrainRenderTestGoal
    {
        private const string ArtifactDir = "C:/Users/danat/.gemini/antigravity/brain/9412af70-ebf5-491e-80e6-e0b2fcde1017/";
        private const string ScenePath   = "Assets/_Project/Scenes/020_RENDER_SANDBOX.unity";
        private const int    ExpectedTerrains = 9;        // 3x3 grid
        private const int    TimeoutLoops     = 72000;    // 72000 * 50ms = 60 min hard cap
        private const int    LogEveryN        = 600;      // log every 30s

        [MenuItem("Hecton8/Tests/Terrain Render Test")]
        public static void Execute()
        {
            string errorPath   = ArtifactDir + "mcp_error.txt";
            string successPath = ArtifactDir + "mcp_success.txt";

            if (File.Exists(errorPath))   File.Delete(errorPath);
            if (File.Exists(successPath)) File.Delete(successPath);

            try
            {
                Debug.Log("[TRT] Opening scene: " + ScenePath);
                EditorSceneManager.OpenScene(ScenePath);

                MapMagicObject mm = Object.FindAnyObjectByType<MapMagicObject>();
                if (mm == null)
                {
                    Fail("No MapMagicObject in scene.");
                    return;
                }

                Debug.Log($"[TRT] MapMagicObject found: '{mm.gameObject.name}'");

                // Pin 3x3 grid
                mm.tiles.generateRange = 0; // Disable automatic viewer-based generation
                for (int x = -1; x <= 1; x++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        mm.tiles.Pin(new Coord(x, z), false, mm);
                    }
                }

                // Save scene as instructed
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                Debug.Log("[TRT] Pinned 3x3 chunks and saved scene.");

                // Force generation
                mm.StartGenerate();
                
                int loops = 0;
                int stableCount = 0;
                int lastTerrainCount = 0;
                while (loops < TimeoutLoops)
                {
                    mm.Update();
                    Den.Tools.Tasks.CoroutineManager.Update();
                    System.Threading.Thread.Sleep(50);
                    loops++;

                    int terrainCount = UnityEngine.Terrain.activeTerrains.Length;

                    if (loops % LogEveryN == 0)
                        Debug.Log($"[TRT] Waiting... loop={loops}  terrains={terrainCount}  generating={mm.IsGenerating()}  stable={stableCount}");

                    // Track stability: count how many consecutive checks we have >= expected
                    if (terrainCount >= ExpectedTerrains)
                    {
                        if (terrainCount == lastTerrainCount)
                            stableCount++;
                        else
                        {
                            stableCount = 1;
                            lastTerrainCount = terrainCount;
                        }
                    }
                    else
                    {
                        stableCount = 0;
                        lastTerrainCount = terrainCount;
                    }

                    // Done when not generating AND stable, OR when we've had 100 stable loops (5s) at >= 9 terrains
                    bool generationDone = (!mm.IsGenerating() && terrainCount >= ExpectedTerrains) || stableCount >= 100;
                    if (generationDone)
                    {
                        Debug.Log($"[TRT] Proceeding. Terrains={terrainCount}  stable={stableCount}  generating={mm.IsGenerating()}");
                        break;
                    }
                }

                if (loops >= TimeoutLoops && stableCount < 100)
                {
                    Fail($"TIMEOUT after {TimeoutLoops * 50 / 1000}s. Terrains={UnityEngine.Terrain.activeTerrains.Length}  generating={mm.IsGenerating()}");
                    return;
                }

                CaptureScreenshots();
            }
            catch (System.Exception ex)
            {
                Fail("Exception: " + ex);
            }
        }

        private static void CaptureScreenshots()
        {
            UnityEngine.Terrain[] terrains = UnityEngine.Object.FindObjectsByType<UnityEngine.Terrain>(FindObjectsSortMode.None);

            // ── Apply custom material ──
            try
            {
                Material baseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat");
                Texture2DArray albedo = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_AlbedoArray.asset");
                Texture2DArray normal = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_NormalArray.asset");
                Texture2DArray mask = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_MaskArray.asset");

                foreach (var t in terrains)
                {
                    var inj = t.GetComponent<Hecton8.World.HectonTerrainMaterialInjector>();
                    if (inj != null)
                    {
                        inj.enabled = true;
                        if (inj.customTerrainMaterial != null)
                        {
                            inj.customTerrainMaterial.EnableKeyword("_NORMALMAP");
                            inj.customTerrainMaterial.EnableKeyword("_MASKMAP");
                            inj.customTerrainMaterial.EnableKeyword("_TERRAIN_BLEND_HEIGHT");

                            if (albedo != null) inj.customTerrainMaterial.SetTexture("_AlbedoArray", albedo);
                            if (normal != null) inj.customTerrainMaterial.SetTexture("_NormalArray", normal);
                            if (mask   != null) inj.customTerrainMaterial.SetTexture("_MaskArray",   mask);
                            
                            inj.customTerrainMaterial.SetFloat("_HectonUVScale", 4.0f);
                            inj.customTerrainMaterial.SetFloat("_HectonTriplanarBlend", 4.0f);
                        }
                        inj.ForceUpdate();
                    }
                }

                // FIX: Generate procedural scatter
                Debug.Log("[TRT] Generating procedural scatter...");
                Hecton8.Editor.ProceduralScatterRenderer.GenerateAndLogScatter(terrains);

            }
            catch (System.Exception ex) { Debug.LogException(ex); }

            // Calculate center
            Vector3 boundsCenter = Vector3.zero;
            float boundsHalf = 0f;
            if (terrains.Length > 0 && terrains[0].terrainData != null)
            {
                Bounds b = new Bounds(terrains[0].transform.position + terrains[0].terrainData.size * 0.5f, terrains[0].terrainData.size);
                foreach (var t in terrains)
                    b.Encapsulate(new Bounds(t.transform.position + t.terrainData.size * 0.5f, t.terrainData.size));
                boundsCenter = b.center;
                boundsHalf = Mathf.Max(b.size.x, b.size.z) * 0.5f;
            }

            // Find central terrain to sample height properly
            float groundY = 0f;
            foreach (var t in terrains)
            {
                Vector3 tPos = t.transform.position;
                Vector3 tSize = t.terrainData.size;
                if (boundsCenter.x >= tPos.x && boundsCenter.x <= tPos.x + tSize.x &&
                    boundsCenter.z >= tPos.z && boundsCenter.z <= tPos.z + tSize.z)
                {
                    groundY = tPos.y + t.SampleHeight(boundsCenter);
                    break;
                }
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("TRT_Camera");
                cam = go.AddComponent<Camera>();
            }
            cam.backgroundColor = new Color(0.02f, 0.05f, 0.1f); // Darker deep sea
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.farClipPlane = 60000f;

            // FIX: Setup Directional Light to ensure shaders respond properly
            Light dirLight = Object.FindAnyObjectByType<Light>();
            if (dirLight == null || dirLight.type != LightType.Directional)
            {
                GameObject lightGo = new GameObject("TRT_DirectionalLight");
                dirLight = lightGo.AddComponent<Light>();
                dirLight.type = LightType.Directional;
            }
            dirLight.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
            dirLight.intensity = 1.2f;
            dirLight.color = new Color(0.8f, 0.9f, 1.0f);
            dirLight.shadows = LightShadows.Soft;

            // RenderSettings setup for ambient
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientSkyColor = new Color(0.05f, 0.1f, 0.2f);

            // Add a point light to illuminate the specific shot areas
            GameObject pointGo = new GameObject("TRT_PointLight");
            Light pLight = pointGo.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.range = 50f;
            pLight.intensity = 3f;
            pLight.color = new Color(0.2f, 0.6f, 1.0f);

            // Take Macro Shot (Orthographic)
            cam.orthographic = true;
            cam.orthographicSize = boundsHalf;
            cam.transform.position = boundsCenter + new Vector3(0, 1000f, 0);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            pLight.transform.position = boundsCenter + new Vector3(0, 500f, 0);
            TakeScreenshot(cam, ArtifactDir + "Macro_Top.png");

            cam.orthographic = false;
            cam.fieldOfView = 60f;

            // Find scatter positions
            GameObject scatterRoot = GameObject.Find("TRT_ScatterRoot");
            Vector3 kelpPos = boundsCenter;
            Vector3 coralPos = boundsCenter;
            if (scatterRoot != null)
            {
                foreach (Transform child in scatterRoot.transform)
                {
                    if (child.name.Contains("kelp")) kelpPos = child.position;
                    if (child.name.Contains("coral")) coralPos = child.position;
                }
            }

            Vector3 anomalyPos = boundsCenter;
            var instancer = Object.FindAnyObjectByType<Hecton8.World.CaveAnomalyInstancedRenderer>();
            if (instancer != null) 
            {
                // Retrieve instance matrix from private field using reflection
                var field = instancer.GetType().GetField("_instances", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var instances = field.GetValue(instancer) as System.Collections.Generic.List<Matrix4x4>;
                    if (instances != null && instances.Count > 0)
                        anomalyPos = new Vector3(instances[0].m03, instances[0].m13, instances[0].m23);
                }
            }

            // Shot 1: Kelp Forest
            cam.transform.position = kelpPos + new Vector3(10f, 5f, 10f);
            cam.transform.LookAt(kelpPos + new Vector3(0, 5f, 0));
            pLight.transform.position = cam.transform.position;
            TakeScreenshot(cam, ArtifactDir + "Forest_Kelp.png");

            // Shot 2: Corals
            cam.transform.position = coralPos + new Vector3(-8f, 3f, -8f);
            cam.transform.LookAt(coralPos + new Vector3(0, 2f, 0));
            pLight.transform.position = cam.transform.position;
            TakeScreenshot(cam, ArtifactDir + "Cliff_Corals.png");

            // Shot 3: Cave Anomalies
            cam.transform.position = anomalyPos + new Vector3(0, -2f, 15f);
            cam.transform.LookAt(anomalyPos);
            pLight.transform.position = cam.transform.position;
            TakeScreenshot(cam, ArtifactDir + "Cave_Anomalies.png");

            Debug.Log("[TRT] All screenshots captured.");
            File.WriteAllText(ArtifactDir + "mcp_success.txt", $"DONE at {System.DateTime.UtcNow:O}\nTerrains={terrains.Length}");

            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void TakeScreenshot(Camera cam, string filename)
        {
            RenderTexture rt = new RenderTexture(1920, 1080, 24);
            cam.targetTexture = rt;
            Texture2D tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            File.WriteAllBytes(filename, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static void Fail(string msg)
        {
            Debug.LogError("[TRT] FAIL: " + msg);
            File.WriteAllText(ArtifactDir + "mcp_error.txt", msg);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
