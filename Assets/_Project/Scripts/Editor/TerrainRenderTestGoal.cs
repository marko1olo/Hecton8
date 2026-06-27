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
        private const string ArtifactDir = "C:/Users/Admin/.gemini/antigravity/brain/9412af70-ebf5-491e-80e6-e0b2fcde1017/";
        private const string ScenePath   = "Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity";
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

                var scatterDir = Object.FindAnyObjectByType<Hecton8.World.WorldProceduralScatterDirector>(FindObjectsInactive.Include);
                if (scatterDir != null)
                {
                    scatterDir.enabled = false;
                }
                
                // Disable Voxel Engine and Cave Directors
                var allMonos = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
                foreach(var mono in allMonos)
                {
                    string mName = mono.GetType().Name;
                    if (mName.Contains("VoxelEngine") || mName.Contains("CaveDirector") || mName.Contains("SdfCarve"))
                    {
                        mono.enabled = false;
                    }
                }

                MapMagicObject mm = Object.FindAnyObjectByType<MapMagicObject>(FindObjectsInactive.Include);
                if (mm == null)
                {
                    Fail("No MapMagicObject in scene.");
                    return;
                }
                mm.gameObject.SetActive(true); // Ensure it's active


                Debug.Log($"[TRT] MapMagicObject found: '{mm.gameObject.name}'");

                // Pin 3x3 grid — this internally triggers generation. Do NOT call StartGenerate() after this.
                mm.tiles.generateRange = 0; // Disable automatic viewer-based generation
                for (int x = -1; x <= 1; x++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        mm.tiles.Pin(new Coord(x, z), false, mm);
                    }
                }

                // Save scene
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                Debug.Log("[TRT] Pinned 3x3 chunks. MapMagic generation started internally.");

                // Give threads 200ms to start before we poll
                System.Threading.Thread.Sleep(200);

                int loops = 0;
                int stableCount = 0;
                while (loops < TimeoutLoops)
                {
                    mm.Update();
                    Den.Tools.Tasks.CoroutineManager.Update();
                    System.Threading.Thread.Sleep(50);
                    loops++;

                    int terrainCount = UnityEngine.Terrain.activeTerrains.Length;
                    bool allTerrainsReady = true;
                    foreach (var t in UnityEngine.Terrain.activeTerrains)
                    {
                        if (t == null || t.terrainData == null || t.terrainData.alphamapTextureCount == 0)
                        {
                            allTerrainsReady = false;
                            break;
                        }
                        var col = t.GetComponent<TerrainCollider>();
                        if (col == null || col.terrainData == null)
                        {
                            allTerrainsReady = false;
                            break;
                        }
                    }

                    bool isGenerating = mm.IsGenerating();

                    // Log every 10 seconds (200 loops) to show explicit progress
                    if (loops % 200 == 0)
                    {
                        float maxH = 0f;
                        foreach (var t in UnityEngine.Terrain.activeTerrains)
                            if (t.terrainData != null) maxH = Mathf.Max(maxH, t.terrainData.size.y);
                        Debug.Log($"[TRT] loop={loops}  terrains={terrainCount}  generating={isGenerating}  stable={stableCount}  maxTerrainHeight={maxH}");
                    }

                    // Strict barrier: 9 terrains, MapMagic idle, all terrains have collider and alphamaps
                    if (!isGenerating && terrainCount == ExpectedTerrains && allTerrainsReady)
                    {
                        stableCount++;
                    }
                    else
                    {
                        stableCount = 0; // Reset if MapMagic starts generating again
                    }

                    // Require 400 consecutive loops (20 seconds) of complete idle state to proceed safely
                    if (stableCount >= 400)
                    {
                        Debug.Log($"[TRT] Done! Terrains={terrainCount}  stable={stableCount}  generating={isGenerating}  allTerrainsReady={allTerrainsReady}");
                        break;
                    }
                }

                if (loops >= TimeoutLoops)
                {
                    Fail($"TIMEOUT after {TimeoutLoops * 50 / 1000}s. Terrains={UnityEngine.Terrain.activeTerrains.Length}  generating={mm.IsGenerating()}");
                    return;
                }

                // Force transform synchronization and canvas updates before rendering
                UnityEngine.Physics.SyncTransforms();
                Canvas.ForceUpdateCanvases();

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
                            
                            // Read UVScale from source material — no hardcoded values
                            float uvScale = inj.customTerrainMaterial.HasProperty("_HectonUVScale")
                                ? inj.customTerrainMaterial.GetFloat("_HectonUVScale")
                                : 4.0f; // fallback only if property missing
                            inj.customTerrainMaterial.SetFloat("_HectonUVScale", uvScale);
                            inj.customTerrainMaterial.SetFloat("_HectonTriplanarBlend", 4.0f);
                        }
                        inj.ForceUpdate();
                    }
                }
                
                // Ecosystem Sterilization: Scatter generation intentionally removed.
            }
            catch (System.Exception ex) { Debug.LogException(ex); }

            // Calculate center
            Vector3 boundsCenter = Vector3.zero;
            float boundsHalf = 0f;
            if (terrains.Length > 0)
            {
                Bounds b = new Bounds();
                bool initialized = false;
                foreach (var t in terrains)
                {
                    if (t.terrainData != null)
                    {
                        Bounds tb = new Bounds(t.transform.position + t.terrainData.size * 0.5f, t.terrainData.size);
                        if (!initialized)
                        {
                            b = tb;
                            initialized = true;
                        }
                        else
                        {
                            b.Encapsulate(tb);
                        }
                    }
                }
                boundsCenter = b.center;
                boundsHalf = Mathf.Max(b.size.x, b.size.z) * 0.5f;
            }

            // Find central terrain to sample height properly
            float groundY = 0f;
            foreach (var t in terrains)
            {
                if (t.terrainData == null) continue;
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
            cam.backgroundColor = new Color(0.05f, 0.08f, 0.12f); // Dark sea, not pure black
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.farClipPlane = 60000f;

            var urpCam = cam.gameObject.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (urpCam == null) urpCam = cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            urpCam.renderShadows = false;     // Shadows need baked data - skip in batchmode
            urpCam.renderPostProcessing = true; // CRITICAL: enables ColorAdjustments/Exposure volume override

            // FIX: Setup Directional Light with neutral-warm spectrum (anti-gloom)
            Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.50f, 0.55f); // Slightly desaturated to not tint textures green
            RenderSettings.ambientIntensity = 2.5f;

            GameObject lightGo = new GameObject("TRT_DirectionalLight_Main");
            Light dirLight = lightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.intensity = 2.5f;
            dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            dirLight.color = new Color(1.0f, 0.95f, 0.9f); 
            dirLight.shadows = LightShadows.Soft;

            GameObject fillLightGo = new GameObject("TRT_DirectionalLight_Fill");
            Light fillLight = fillLightGo.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 1.5f; // Fill light to kill black shadows
            fillLight.transform.rotation = Quaternion.Euler(30f, 150f, 0f); // Opposite direction
            fillLight.color = new Color(0.6f, 0.7f, 0.9f); // Sky-blue fill
            fillLight.shadows = LightShadows.None; // No shadows for fill light

            // RenderSettings setup for ambient
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.01f, 0.03f, 0.05f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.005f;

            // Create Global Volume for PostProcessing
            // CRITICAL: Remove ACES - it crushes blacks in batchmode.
            // Use None tonemapping + explicit Exposure to get laboratory brightness.
            GameObject volumeGo = new GameObject("TRT_GlobalVolume");
            volumeGo.layer = LayerMask.NameToLayer("Default");
            var volume = volumeGo.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            volume.priority = 100;
            
            var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            volume.profile = profile;
            
            var tonemapping = profile.Add<UnityEngine.Rendering.Universal.Tonemapping>();
            tonemapping.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.None);

            var exposure = profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>();
            exposure.postExposure.Override(2.0f); // +2 EV = 4x brighter
            exposure.contrast.Override(-20f);     // Lift shadows slightly

            // Add a point light to illuminate the specific shot areas
            GameObject pointGo = new GameObject("TRT_PointLight");
            Light pLight = pointGo.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.range = 400f;
            pLight.intensity = 12f;
            pLight.color = new Color(0.8f, 0.85f, 1.0f);
            pLight.shadows = LightShadows.None;

            RenderSettings.fog = false;
            // 1. Naked_Macro_10km: 10km (Orthographic top-down)
            cam.orthographic = true;
            cam.orthographicSize = boundsHalf;
            cam.transform.position = new Vector3(boundsCenter.x, 10000f, boundsCenter.z);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            pLight.enabled = false;
            TakeScreenshot(cam, ArtifactDir + "Naked_Macro_10km.png");
            pLight.enabled = true;

            cam.orthographic = false;
            cam.fieldOfView = 60f;

            // Find an interesting steep slope in the center terrain ONLY
            Vector3 canyonPos = boundsCenter;
            Vector3 biomePos = boundsCenter;
            
            if (terrains.Length > 0)
            {
                // Find central terrain
                UnityEngine.Terrain centerTerrain = null;
                float minCenterDist = float.MaxValue;
                foreach(var t in terrains) {
                    if (t.terrainData == null) continue;
                    float dist = Vector3.Distance(t.transform.position + t.terrainData.size*0.5f, boundsCenter);
                    if (dist < minCenterDist) { 
                        minCenterDist = dist; 
                        centerTerrain = t; 
                    }
                }

                float minHeight = float.MaxValue;
                float maxHeight = float.MinValue;
                Vector3 minPos = boundsCenter;
                Vector3 maxPos = boundsCenter;
                
                if (centerTerrain != null && centerTerrain.terrainData != null)
                {
                    int res = centerTerrain.terrainData.heightmapResolution;
                    int startLimit = Mathf.RoundToInt(res * 0.2f);
                    int endLimit = Mathf.RoundToInt(res * 0.8f);
                    
                    for (int y = startLimit; y < endLimit; y += 16)
                    {
                        for (int x = startLimit; x < endLimit; x += 16)
                        {
                            float h = centerTerrain.terrainData.GetHeight(x, y);
                            if (h < minHeight) { 
                                minHeight = h; 
                                minPos = new Vector3(x, h, y); 
                                minPos.x = minPos.x / res * centerTerrain.terrainData.size.x + centerTerrain.transform.position.x; 
                                minPos.y = minPos.y + centerTerrain.transform.position.y;
                                minPos.z = minPos.z / res * centerTerrain.terrainData.size.z + centerTerrain.transform.position.z; 
                            }
                            if (h > maxHeight) { 
                                maxHeight = h; 
                                maxPos = new Vector3(x, h, y); 
                                maxPos.x = maxPos.x / res * centerTerrain.terrainData.size.x + centerTerrain.transform.position.x; 
                                maxPos.y = maxPos.y + centerTerrain.transform.position.y;
                                maxPos.z = maxPos.z / res * centerTerrain.terrainData.size.z + centerTerrain.transform.position.z; 
                            }
                        }
                    }

                    // --- SEARCH FOR PERFECT BIOME TRANSITION (SAND AND ROCK > 0.4) ---
                    int alphaRes = centerTerrain.terrainData.alphamapResolution;
                    float[,,] alphas = centerTerrain.terrainData.GetAlphamaps(0, 0, alphaRes, alphaRes);
                    bool foundTransition = false;
                    for (int y = 0; y < alphaRes; y += 4)
                    {
                        for (int x = 0; x < alphaRes; x += 4)
                        {
                            // Assuming layer 0 is Sand, layer 3 is HardRock (from Control1 mapping)
                            if (alphas.GetLength(2) >= 4)
                            {
                                float sandWeight = alphas[y, x, 0];
                                float rockWeight = alphas[y, x, 3];
                                if (sandWeight > 0.35f && rockWeight > 0.35f)
                                {
                                    float worldX = (float)x / alphaRes * centerTerrain.terrainData.size.x + centerTerrain.transform.position.x;
                                    float worldZ = (float)y / alphaRes * centerTerrain.terrainData.size.z + centerTerrain.transform.position.z;
                                    float worldY = centerTerrain.SampleHeight(new Vector3(worldX, 0, worldZ)) + centerTerrain.transform.position.y;
                                    biomePos = new Vector3(worldX, worldY, worldZ);
                                    foundTransition = true;
                                    break;
                                }
                            }
                        }
                        if (foundTransition) break;
                    }
                    if (!foundTransition) biomePos = minPos;
                }
                canyonPos = Vector3.Lerp(minPos, maxPos, 0.5f); // Mid slope
            }
            // 2. Naked_Mountain_Details: Bird-eye close-up, camera 200m above mid-slope, looking DOWN 65deg
            RenderSettings.fog = false; // No fog - diagnostic render
            cam.transform.position = canyonPos + new Vector3(0f, 200f, 0f);
            cam.transform.rotation = Quaternion.Euler(65f, 0f, 0f); // Steep downward look - terrain fills frame
            pLight.enabled = true;
            pLight.transform.position = cam.transform.position + new Vector3(50f, 100f, -50f);
            pLight.range = 1500f;
            pLight.intensity = 15f;
            TakeScreenshot(cam, ArtifactDir + "Naked_Mountain_Details.png");

            // 3. Naked_Biome_Transition: camera 150m above found sand/rock blend point, looking DOWN 75deg
            // 30m was too close - camera clipped into the terrain mesh
            RenderSettings.fog = false;
            cam.transform.position = new Vector3(biomePos.x, biomePos.y + 150f, biomePos.z);
            cam.transform.rotation = Quaternion.Euler(75f, 0f, 0f); // Steep - fills frame with terrain
            pLight.enabled = true;
            pLight.transform.position = cam.transform.position + new Vector3(0f, 50f, 0f);
            pLight.range = 600f;
            pLight.intensity = 8f;
            TakeScreenshot(cam, ArtifactDir + "Naked_Biome_Transition.png");

            cam.backgroundColor = Color.black; // Ensure strict black background

            // 4. Naked_Cave_Interior: 
            Vector3 caveInteriorPos = boundsCenter;
            bool foundHole = false;
            foreach (var t in terrains)
            {
                if (foundHole) break;
                if (t.terrainData == null) continue;
                int res = t.terrainData.holesResolution;
                bool[,] holes = t.terrainData.GetHoles(0, 0, res, res);
                for (int x = 0; x < res; x += 4)
                {
                    for (int z = 0; z < res; z += 4)
                    {
                        if (!holes[z, x]) // false means hole
                        {
                            float nx = (float)x / res;
                            float nz = (float)z / res;
                            float h = t.terrainData.GetHeight(Mathf.RoundToInt(nx * t.terrainData.heightmapResolution), Mathf.RoundToInt(nz * t.terrainData.heightmapResolution));
                            caveInteriorPos = new Vector3(t.transform.position.x + nx * t.terrainData.size.x,
                                                  t.transform.position.y + h - 10f, // 10m below hole
                                                  t.transform.position.z + nz * t.terrainData.size.z);
                            foundHole = true;
                            break;
                        }
                    }
                    if (foundHole) break;
                }
            }
            if (!foundHole) caveInteriorPos = new Vector3(boundsCenter.x + 200f, groundY + 5f, boundsCenter.z + 200f);

            cam.transform.position = caveInteriorPos;
            cam.transform.rotation = Quaternion.Euler(-15, 45, 0); // Looking slightly up from below
            pLight.enabled = true;
            pLight.transform.position = cam.transform.position;
            TakeScreenshot(cam, ArtifactDir + "Naked_Cave_Interior.png");
            
            // Ecosystem Screenshots are physically ELIMINATED for sterile geology validation
            // TakeScatterScreenshot(cam, "family_kelp", ...);

            Debug.Log("[TRT] All screenshots captured. Exporting Diagnostic Maps...");
            ExportDiagnosticMaps(terrains, ArtifactDir);

            Debug.Log("[TRT] Done.");
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

        private static void ExportDiagnosticMaps(UnityEngine.Terrain[] terrains, string artifactDir)
        {
            if (terrains == null || terrains.Length == 0) return;
            
            UnityEngine.Terrain center = null;
            float minCenterDist = float.MaxValue;
            foreach (var t in terrains)
            {
                if (t.terrainData != null)
                {
                    float dist = Vector3.Distance(t.transform.position, Vector3.zero);
                    if (dist < minCenterDist)
                    {
                        minCenterDist = dist;
                        center = t;
                    }
                }
            }
            
            if (center == null || center.terrainData == null) return;
            
            var td = center.terrainData;
            int res = td.heightmapResolution;
            
            Texture2D hTex = new Texture2D(res, res, TextureFormat.RGB24, false);
            float[,] heights = td.GetHeights(0, 0, res, res);
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float worldH = heights[y, x] * td.size.y + center.transform.position.y;
                    Color c;
                    // Our world height is typically between -4000 and +1000
                    float t = Mathf.InverseLerp(-4000f, 1000f, worldH);
                    if (t >= 0.8f) c = Color.Lerp(Color.green, Color.white, (t - 0.8f) / 0.2f); // Above sea level (1000/5000 is 0.8)
                    else c = Color.Lerp(new Color(0f, 0f, 0.3f), Color.cyan, t / 0.8f); // Below sea level

                    hTex.SetPixel(x, y, c);
                }
            }
            hTex.Apply();
            File.WriteAllBytes(artifactDir + "Debug_Heightmap.png", hTex.EncodeToPNG());
            Object.DestroyImmediate(hTex);
            
            Texture2D sTex = new Texture2D(res, res, TextureFormat.RGB24, false);
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float nx = (float)x / (res - 1);
                    float ny = (float)y / (res - 1);
                    float steepness = td.GetSteepness(nx, ny);
                    sTex.SetPixel(x, y, Color.Lerp(Color.black, Color.red, steepness / 90f));
                }
            }
            sTex.Apply();
            File.WriteAllBytes(artifactDir + "Debug_Slope.png", sTex.EncodeToPNG());
            Object.DestroyImmediate(sTex);
            
            int aRes = td.alphamapResolution;
            if (aRes > 0 && td.alphamapTextureCount > 0)
            {
                Texture2D aTex = new Texture2D(aRes, aRes, TextureFormat.RGB24, false);
                float[,,] maps = td.GetAlphamaps(0, 0, aRes, aRes);
                int layers = td.alphamapLayers;
                for (int y = 0; y < aRes; y++)
                {
                    for (int x = 0; x < aRes; x++)
                    {
                        float r = layers > 3 ? maps[y, x, 3] : 0f; // HardRock
                        float g = layers > 0 ? maps[y, x, 0] : 0f; // Sand
                        float b = layers > 2 ? maps[y, x, 2] : 0f; // Silt
                        aTex.SetPixel(x, y, new Color(r, g, b));
                    }
                }
                aTex.Apply();
                File.WriteAllBytes(artifactDir + "Debug_Splatmap.png", aTex.EncodeToPNG());
                Object.DestroyImmediate(aTex);
            }
        }

        private static void Fail(string msg)
        {
            Debug.LogError("[TRT] FAIL: " + msg);
            File.WriteAllText(ArtifactDir + "mcp_error.txt", msg);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
