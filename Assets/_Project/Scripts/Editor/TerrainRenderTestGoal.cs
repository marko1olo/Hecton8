using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
            UnityEngine.Terrain[] terrains = UnityEngine.Terrain.activeTerrains;

            try
            {
                Material baseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat");
                Texture2DArray albedo = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_AlbedoArray.asset");
                Texture2DArray normal = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_NormalArray.asset");
                Texture2DArray mask = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_MaskArray.asset");

                foreach (var t in terrains)
                {
                    if (t != null)
                    {
                        var inj = t.GetComponent<Hecton8.World.HectonTerrainMaterialInjector>();
                        if (inj == null) inj = t.gameObject.AddComponent<Hecton8.World.HectonTerrainMaterialInjector>();

                        inj.enabled = true;
                        if (inj.customTerrainMaterial == null && baseMat != null)
                            inj.customTerrainMaterial = baseMat;

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
            cam.backgroundColor = new Color(0.01f, 0.02f, 0.05f); // Darker deep sea
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
            dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            dirLight.intensity = 1.5f;
            dirLight.color = new Color(0.8f, 0.9f, 1.0f);
            dirLight.shadows = LightShadows.Soft;

            // RenderSettings setup for ambient
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.01f, 0.03f, 0.05f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.008f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.18f, 0.25f);

            // Create Global Volume for PostProcessing
            GameObject volumeGo = new GameObject("TRT_GlobalVolume");
            volumeGo.layer = LayerMask.NameToLayer("Default");
            var volume = volumeGo.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            volume.priority = 1;
            
            var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            volume.profile = profile;
            
            var tonemapping = profile.Add<UnityEngine.Rendering.Universal.Tonemapping>();
            tonemapping.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.ACES);
            
            var bloom = profile.Add<UnityEngine.Rendering.Universal.Bloom>();
            bloom.intensity.Override(0.4f);
            bloom.threshold.Override(1.1f);
            bloom.scatter.Override(0.5f);

            // Add a point light to illuminate the specific shot areas
            GameObject pointGo = new GameObject("TRT_PointLight");
            Light pLight = pointGo.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.range = 50f;
            pLight.intensity = 3f;
            pLight.color = new Color(0.2f, 0.6f, 1.0f);

            // 1. MacroView: 10km (Orthographic top-down)
            cam.orthographic = true;
            cam.orthographicSize = boundsHalf;
            cam.transform.position = new Vector3(boundsCenter.x, 15000f, boundsCenter.z);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            // Point light is useless at 14km range=50 — disable it for MacroView, dirLight covers everything
            pLight.enabled = false;
            TakeScreenshot(cam, ArtifactDir + "MacroView.png");
            pLight.enabled = true;

            cam.orthographic = false;
            cam.fieldOfView = 60f;

            // Find an interesting canyon / steep slope in the center terrain
            Vector3 canyonPos = boundsCenter;
            Vector3 cavePos = boundsCenter;
            if (terrains.Length > 0)
            {
                float minHeight = float.MaxValue;
                float maxHeight = float.MinValue;
                Vector3 minPos = boundsCenter;
                Vector3 maxPos = boundsCenter;
                
                foreach (var t in terrains)
                {
                    int res = t.terrainData.heightmapResolution;
                    for (int y = 0; y < res; y += 16)
                    {
                        for (int x = 0; x < res; x += 16)
                        {
                            float h = t.terrainData.GetHeight(x, y);
                            if (h < minHeight) { 
                                minHeight = h; 
                                minPos = new Vector3(x, h, y); 
                                minPos.x = minPos.x / res * t.terrainData.size.x + t.transform.position.x; 
                                minPos.y = minPos.y + t.transform.position.y;
                                minPos.z = minPos.z / res * t.terrainData.size.z + t.transform.position.z; 
                            }
                            if (h > maxHeight) { 
                                maxHeight = h; 
                                maxPos = new Vector3(x, h, y); 
                                maxPos.x = maxPos.x / res * t.terrainData.size.x + t.transform.position.x; 
                                maxPos.y = maxPos.y + t.transform.position.y;
                                maxPos.z = maxPos.z / res * t.terrainData.size.z + t.transform.position.z; 
                            }
                        }
                    }
                } // Close foreach (var t in terrains)
                canyonPos = Vector3.Lerp(minPos, maxPos, 0.5f); // Mid slope
                cavePos = minPos; // Lowest point, good chance for a cave entrance or deep rift
                
                // Adjust to proper world y height
                UnityEngine.RaycastHit hit;
                if (UnityEngine.Physics.Raycast(new Vector3(canyonPos.x, 10000f, canyonPos.z), Vector3.down, out hit, 20000f)) canyonPos.y = hit.point.y;
                if (UnityEngine.Physics.Raycast(new Vector3(cavePos.x, 10000f, cavePos.z), Vector3.down, out hit, 20000f)) cavePos.y = hit.point.y;
            }
            // 2. CanyonView: 2km
            RenderSettings.fog = false;
            cam.transform.position = canyonPos + new Vector3(0f, 1500f, -1500f);
            cam.transform.rotation = Quaternion.Euler(45f, 0f, 0f); // Pitch 45
            pLight.enabled = false;
            TakeScreenshot(cam, ArtifactDir + "CanyonView.png");
            RenderSettings.fog = true;

            // 3. CaveEntrance: camera hovers above cavePos looking down at the terrain surface
            // cavePos is the lowest terrain point — we look AT the surface FROM above-side, not into the pit
            cam.transform.position = cavePos + new Vector3(0f, 80f, -120f);
            cam.transform.LookAt(cavePos + Vector3.up * 20f);
            pLight.enabled = true;
            pLight.transform.position = cam.transform.position;
            pLight.range = 300f;
            pLight.intensity = 8f;
            TakeScreenshot(cam, ArtifactDir + "CaveEntrance.png");

            cam.backgroundColor = Color.black; // Ensure strict black background

            // 4. CaveInterior: closer, looking into the low terrain rift
            cam.transform.position = cavePos + new Vector3(0f, 20f, -40f);
            cam.transform.LookAt(cavePos + Vector3.up * 5f);
            pLight.enabled = true;
            pLight.transform.position = cam.transform.position;
            pLight.range = 100f;
            pLight.intensity = 5f;
            TakeScreenshot(cam, ArtifactDir + "CaveInterior.png");

            // Ecosystem Screenshots with Collision Avoidance
            TakeScatterScreenshot(cam, "family_kelp", ArtifactDir + "Forest_Kelp.png", pLight);
            TakeScatterScreenshot(cam, "family_coral", ArtifactDir + "Cliff_Corals.png", pLight);
            TakeScatterScreenshot(cam, "CaveAnomaly", ArtifactDir + "Cave_Anomalies.png", pLight);

            Debug.Log("[TRT] All screenshots captured.");
            File.WriteAllText(ArtifactDir + "mcp_success.txt", $"DONE at {System.DateTime.UtcNow:O}\nTerrains={terrains.Length}");

            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void TakeScatterScreenshot(Camera cam, string targetNameMatch, string filename, Light pLight)
        {
            Vector3 objPos = Vector3.zero;
            bool found = false;

            foreach (var kvp in Hecton8.Editor.ProceduralScatterRenderer.RepresentativeInstancesByPrefab)
            {
                if (kvp.Key.Contains(targetNameMatch))
                {
                    objPos = kvp.Value;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Debug.LogWarning($"[TRT] Could not find any instance matching '{targetNameMatch}' to screenshot.");
                return;
            }

            // Place camera 8 meters back and 3 meters up
            Vector3 camOffset = new Vector3(-8f, 3f, -8f);
            
            int attempts = 0;
            while (attempts < 8)
            {
                Vector3 proposedPos = objPos + camOffset;
                cam.transform.position = proposedPos;
                cam.transform.LookAt(objPos + Vector3.up * 1f);
                
                // Simplified collision avoidance
                Vector3 dir = (objPos - proposedPos).normalized;
                float dist = Vector3.Distance(proposedPos, objPos);
                
                if (!UnityEngine.Physics.Raycast(proposedPos, dir, dist - 1f))
                {
                    break; // Good position found!
                }
                
                camOffset = Quaternion.Euler(0, 45f, 0) * camOffset;
                attempts++;
            }

            // Bring light close to the camera to illuminate the object
            pLight.transform.position = cam.transform.position;
            pLight.color = Color.white;
            pLight.intensity = 2f;
            pLight.range = 50f;

            TakeScreenshot(cam, filename);
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
