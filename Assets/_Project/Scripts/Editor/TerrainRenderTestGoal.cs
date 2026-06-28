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

                // Disable draft terrains to avoid two different colors of rock and ensure all tiles generate at full resolution
                mm.draftsInEditor = false;
                mm.draftsInPlaymode = false;
                mm.EnableEditorDrafts(false);

                // Disable all node previews on the graph to prevent yellow/colored overlay lines in screenshots
                if (mm.graph != null)
                {
                    foreach (var gen in mm.graph.generators)
                    {
                        gen.guiPreview = false;
                    }
                }
                
                // Use reflection since PreviewManager is defined in MapMagic.Editor assembly
                var previewType = System.Type.GetType("MapMagic.Previews.PreviewManager, MapMagic.Editor");
                if (previewType == null)
                {
                    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        previewType = assembly.GetType("MapMagic.Previews.PreviewManager");
                        if (previewType != null) break;
                    }
                }
                if (previewType != null)
                {
                    var clearMethod = previewType.GetMethod("ClearAll", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (clearMethod != null) clearMethod.Invoke(null, null);

                    var removeMethod = previewType.GetMethod("RemoveAllFromTerrain", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (removeMethod != null) removeMethod.Invoke(null, null);
                }

                // Force full regeneration by clearing existing tiles and removing all scene terrains
                for (int x = -10; x <= 10; x++)
                {
                    for (int z = -10; z <= 10; z++)
                    {
                        mm.tiles.Unpin(new Coord(x, z));
                    }
                }
                var existingTerrains = Object.FindObjectsByType<UnityEngine.Terrain>(FindObjectsInactive.Include);
                foreach (var t in existingTerrains)
                {
                    Object.DestroyImmediate(t.gameObject);
                }

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

                // Clear selection to prevent editor gizmos and camera target lines from rendering
                Selection.activeGameObject = null;
                Selection.objects = new Object[0];

                // Sterilize the scene: destroy any non-essential GameObjects that might render lines, shapes, or UI guides
                var protectedObjects = new System.Collections.Generic.HashSet<GameObject>();
                
                void Protect(GameObject goObj)
                {
                    if (goObj == null) return;
                    Transform tr = goObj.transform;
                    while (tr != null)
                    {
                        if (tr.gameObject.name == "Main Terrain")
                        {
                            Debug.Log($"[TRT] Main Terrain protected by call to Protect({goObj.name})");
                        }
                        protectedObjects.Add(tr.gameObject);
                        tr = tr.parent;
                    }
                }

                Camera activeCam = Camera.main;
                if (activeCam != null) Protect(activeCam.gameObject);
                
                foreach (var t in UnityEngine.Terrain.activeTerrains)
                {
                    if (t == null) continue;
                    var tile = t.GetComponentInParent<MapMagic.Terrains.TerrainTile>();
                    if (tile != null)
                    {
                        Protect(t.gameObject);
                    }
                }
                
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
                {
                    if (l != null) Protect(l.gameObject);
                }
                
                foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include))
                {
                    if (v != null) Protect(v.gameObject);
                }
                
                if (mm != null) Protect(mm.gameObject);

                var allGo = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
                foreach (var go in allGo)
                {
                    if (go != null && go.name.Contains("TRT_")) Protect(go);
                }

                foreach (var go in allGo)
                {
                    if (go != null && !protectedObjects.Contains(go))
                    {
                        Debug.Log($"[TRT] Destroying scene clutter: '{go.name}'");
                        Object.DestroyImmediate(go);
                    }
                }

                Debug.Log($"[TRT] --- PROTECTED OBJECTS COMPONENT AUDIT ---");
                foreach (var goObj in protectedObjects)
                {
                    if (goObj == null) continue;
                    Debug.Log($"[TRT] Protected Go: '{goObj.name}'");
                    foreach (var c in goObj.GetComponents<Component>())
                    {
                        if (c != null) Debug.Log($"[TRT]   Component: {c.GetType().FullName}");
                    }
                }
                Debug.Log($"[TRT] -----------------------------------------");

                Debug.Log($"[TRT] --- ACTIVE TERRAINS MATERIAL AUDIT ---");
                foreach (var t in UnityEngine.Terrain.activeTerrains)
                {
                    if (t == null) continue;
                    var mat = t.materialTemplate;
                    if (mat == null)
                    {
                        Debug.Log($"[TRT] Terrain: '{t.name}' HAS NO materialTemplate!");
                        continue;
                    }
                    Debug.Log($"[TRT] Terrain: '{t.name}' Mat: '{mat.name}' Shader: '{mat.shader.name}'");
                    if (mat.HasProperty("_Control")) Debug.Log($"[TRT]   _Control: {mat.GetTexture("_Control")?.name ?? "null"}");
                    if (mat.HasProperty("_Control1")) Debug.Log($"[TRT]   _Control1: {mat.GetTexture("_Control1")?.name ?? "null"}");
                    if (mat.HasProperty("_Control2")) Debug.Log($"[TRT]   _Control2: {mat.GetTexture("_Control2")?.name ?? "null"}");
                    if (mat.HasProperty("_AlbedoArray")) Debug.Log($"[TRT]   _AlbedoArray: {mat.GetTexture("_AlbedoArray")?.name ?? "null"}");
                    if (mat.HasProperty("_NormalArray")) Debug.Log($"[TRT]   _NormalArray: {mat.GetTexture("_NormalArray")?.name ?? "null"}");
                }
                Debug.Log($"[TRT] -----------------------------------------");

                Debug.Log($"[TRT] --- ALL RENDERING COMPONENTS IN SCENE ---");
                var allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
                foreach (var r in allRenderers)
                {
                    if (r != null) Debug.Log($"[TRT] Renderer: '{r.name}' type: {r.GetType().FullName} pos: {r.transform.position} enabled: {r.enabled} goActive: {r.gameObject.activeInHierarchy}");
                }
                var allFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include);
                foreach (var f in allFilters)
                {
                    if (f != null) Debug.Log($"[TRT] MeshFilter: '{f.name}' pos: {f.transform.position} goActive: {f.gameObject.activeInHierarchy}");
                }
                Debug.Log($"[TRT] -----------------------------------------");

                CaptureScreenshots();
            }
            catch (System.Exception ex)
            {
                Fail("Exception: " + ex);
            }
        }

        private static void CaptureScreenshots()
        {
            bool originalShowGizmos = true;
            System.Reflection.PropertyInfo showGizmosProp = null;
            try
            {
                var annotationUtilityType = System.Type.GetType("UnityEditor.AnnotationUtility, UnityEditor");
                if (annotationUtilityType != null)
                {
                    showGizmosProp = annotationUtilityType.GetProperty("showGizmos", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                    if (showGizmosProp != null)
                    {
                        originalShowGizmos = (bool)showGizmosProp.GetValue(null);
                        showGizmosProp.SetValue(null, false);
                        Debug.Log("[TRT] AnnotationUtility.showGizmos temporarily set to false");
                    }
                }
            }
            catch (System.Exception ex) { Debug.LogWarning($"[TRT] Failed to toggle AnnotationUtility.showGizmos: {ex.Message}"); }

            try
            {
                // Clear editor selection to avoid drawing selection gizmos/outlines in screenshots
                UnityEditor.Selection.activeGameObject = null;
                UnityEditor.Selection.objects = new UnityEngine.Object[0];

                UnityEngine.Terrain[] terrains = UnityEngine.Terrain.activeTerrains;

                // Clean up any preview terrains or draft terrains left in the scene by MapMagic
                var allTerrains = Object.FindObjectsByType<UnityEngine.Terrain>(FindObjectsInactive.Include);
                foreach (var t in allTerrains)
                {
                    if (t != null)
                    {
                        if (t.gameObject.name == "Preview Terrain")
                        {
                            Debug.Log($"[TRT] Destroying MapMagic Preview Terrain: '{t.gameObject.name}'");
                            Object.DestroyImmediate(t.gameObject);
                        }
                        else if (t.gameObject.name.Contains("Draft"))
                        {
                            Debug.Log($"[TRT] Disabling MapMagic Draft Terrain: '{t.gameObject.name}'");
                            t.gameObject.SetActive(false);
                        }
                        else
                        {
                            t.drawHeightmap = true;
                        }
                    }
                }
                
                // Refresh active terrains list after cleanup
                terrains = UnityEngine.Terrain.activeTerrains;

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
                        t.basemapDistance = 100000.0f; // Prevent fallback to URP Lit Basemap

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
                            
                            // High frequency tiling for micro-geology (2.5m per tile)
                            inj.customTerrainMaterial.SetFloat("_HectonUVScale", 400.0f);
                            inj.customTerrainMaterial.SetFloat("_HectonTriplanarBlend", 8.0f);
                        }
                        inj.ForceUpdate();
                    }
                }
                
                // Generate procedural scatter for rendering tests
                Debug.Log("[TRT] Generating procedural scatter...");
                Hecton8.Editor.ProceduralScatterRenderer.GenerateAndLogScatter(terrains);
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
            Debug.Log($"[TRT] Camera.main: '{cam.name}'");

            // Disable all other cameras to prevent their editor icons/frustums from rendering
            foreach (var otherCam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                if (otherCam != null && otherCam != cam)
                {
                    otherCam.enabled = false;
                    otherCam.gameObject.SetActive(false);
                }
            }

            // Disable ALL custom MonoBehaviours — kills gizmo drawers, scatter directors, overlay systems.
            // HectonTerrainMaterialInjector is NOT protected here — we re-apply material directly below.
            // WorldProceduralScatterDirector must be DESTROYED, not just disabled, to prevent [DrawGizmo]
            // from being called at all (Unity only invokes [DrawGizmo] when the target component exists).
            foreach (var comp in Object.FindObjectsByType<Component>(FindObjectsInactive.Include))
            {
                if (comp == null || comp is Transform || comp is Camera || comp is Light) continue;

                var type = comp.GetType();
                string typeName = type.FullName ?? "";

                // Protect only the URP Volume and pure UnityEngine terrain internals
                if (type == typeof(UnityEngine.Rendering.Volume) ||
                    typeName.StartsWith("UnityEngine.Terrain"))
                {
                    continue;
                }

                // DESTROY scatter director — [DrawGizmo] only fires when the component exists in scene.
                // Disabling is not enough; Unity still iterates existing components for DrawGizmo.
                if (typeName.Contains("WorldProceduralScatterDirector") ||
                    typeName.Contains("ScatterDirector"))
                {
                    Debug.Log($"[TRT] Destroying scatter director: '{comp.name}' ({typeName})");
                    Object.DestroyImmediate(comp.gameObject);
                    continue;
                }

                // Destroy any MeshRenderer/SkinnedMeshRenderer that isn't terrain — kills vertical stick artifacts
                if (comp is MeshRenderer || comp is SkinnedMeshRenderer)
                {
                    Debug.Log($"[TRT] Destroying mesh renderer object: '{comp.name}' ({typeName})");
                    Object.DestroyImmediate(comp.gameObject);
                    continue;
                }

                // Disable everything else that is a Behaviour (MonoBehaviour, etc.)
                if (comp is Behaviour beh && beh.enabled)
                {
                    beh.enabled = false;
                }
            }

            // --- Re-apply terrain material directly after the sweep ---
            // The injector is now disabled, so we push materialTemplate manually.
            try
            {
                Material baseMat2 = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat");
                Texture2DArray albedo2 = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_AlbedoArray.asset");
                Texture2DArray normal2 = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_NormalArray.asset");
                Texture2DArray mask2   = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_MaskArray.asset");

                if (baseMat2 != null)
                {
                    Debug.Log($"[TRT] Shader on baseMat2: {baseMat2.shader?.name ?? "null"}");
                    foreach (var t in UnityEngine.Terrain.activeTerrains)
                    {
                        if (t == null) continue;
                        // Create a per-terrain instanced copy so _Control can differ per tile
                        Material inst = new Material(baseMat2);
                        inst.name = baseMat2.name + "_" + t.name;

                        if (albedo2 != null) inst.SetTexture("_AlbedoArray", albedo2);
                        if (normal2 != null) inst.SetTexture("_NormalArray", normal2);
                        if (mask2   != null) inst.SetTexture("_MaskArray",   mask2);
                        inst.SetFloat("_HectonUVScale", 400.0f);
                        inst.SetFloat("_HectonTriplanarBlend", 8.0f);
                        inst.EnableKeyword("_NORMALMAP");
                        inst.EnableKeyword("_MASKMAP");
                        inst.EnableKeyword("_TERRAIN_BLEND_HEIGHT");

                        // Assign splatmaps from terrainData
                        if (t.terrainData != null && t.terrainData.alphamapTextureCount > 0)
                        {
                            Texture2D[] alphamaps = t.terrainData.alphamapTextures;
                            if (alphamaps.Length > 0 && alphamaps[0] != null) inst.SetTexture("_Control",  alphamaps[0]);
                            if (alphamaps.Length > 1 && alphamaps[1] != null) inst.SetTexture("_Control1", alphamaps[1]);
                            if (alphamaps.Length > 2 && alphamaps[2] != null) inst.SetTexture("_Control2", alphamaps[2]);
                            inst.SetFloat("_NumLayersCount", t.terrainData.alphamapLayers);
                            inst.SetVector("_TerrainSize", new Vector4(t.terrainData.size.x, t.terrainData.size.y, t.terrainData.size.z, 0));
                        }

                        t.materialTemplate = inst;
                        t.basemapDistance = 100000.0f;
                        Debug.Log($"[TRT] Direct materialTemplate assigned to '{t.name}': shader={inst.shader?.name ?? "null"}");
                    }
                }
                else
                {
                    Debug.LogError("[TRT] baseMat2 is null — HectonTerrainMaterial.mat not found at expected path!");
                }
            }
            catch (System.Exception ex) { Debug.LogException(ex); }

            foreach (var c in cam.GetComponents<Component>())
            {
                if (c != null) Debug.Log($"[TRT] Camera component: {c.GetType().FullName}");
            }
            if (cam.transform.parent != null)
            {
                Debug.Log($"[TRT] Camera parent: '{cam.transform.parent.name}'");
                foreach (var c in cam.transform.parent.GetComponents<Component>())
                {
                    if (c != null) Debug.Log($"[TRT] Camera parent component: {c.GetType().FullName}");
                }
            }
            cam.backgroundColor = new Color(0.05f, 0.08f, 0.12f); // Dark sea, not pure black
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.farClipPlane = 60000f;

            var urpCam = cam.gameObject.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (urpCam == null) urpCam = cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            urpCam.renderShadows = false;     // Shadows need baked data - skip in batchmode
            urpCam.renderPostProcessing = true; // CRITICAL: enables ColorAdjustments/Exposure volume override

            // Kill all pre-existing scene lights — any tinted light destroys PBR albedo read
            foreach (var existingLight in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                Object.DestroyImmediate(existingLight.gameObject);

            // Neutral ambient — low enough not to tint, present enough to kill pure-black shadows
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.2f, 0.25f, 0.3f); // Clinical neutral-cool, safe for PBR albedo
            RenderSettings.ambientIntensity = 1.0f;

            // Key light: white, 2.5 intensity, low-angle to reveal topology
            GameObject lightGo = new GameObject("TRT_DirectionalLight_Main");
            lightGo.transform.position = new Vector3(0f, -99999f, 0f); // Underground — icon hidden behind terrain
            Light dirLight = lightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.intensity = 2.5f;
            dirLight.transform.rotation = Quaternion.Euler(40f, 60f, 0f); // Low grazing angle reveals micro-grit and terraces
            dirLight.color = Color.white; // Spectrally neutral — no tint
            dirLight.shadows = LightShadows.None;
            RenderSettings.sun = dirLight;

            // Fill light: dim, opposite axis — lifts shadows without tinting
            GameObject fillLightGo = new GameObject("TRT_DirectionalLight_Fill");
            fillLightGo.transform.position = new Vector3(0f, -99999f, 0f); // Underground
            Light fillLight = fillLightGo.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.8f;
            fillLight.transform.rotation = Quaternion.Euler(30f, 240f, 0f);
            fillLight.color = Color.white;
            fillLight.shadows = LightShadows.None;

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
            exposure.postExposure.Override(2.2f); // +2.2 EV — dark basalt: 0.03*4.6=0.14 midtone, 0.14*4.6=0.64 highlights, no clip
            exposure.contrast.Override(-15f);     // Gentle shadow lift — preserves topo readability without crushing darks

            // Extra fill for macro topography — ensure we see terrain detail
            GameObject pointGo = new GameObject("TRT_DirectionalLight");
            pointGo.transform.position = new Vector3(0f, -99999f, 0f); // Underground
            Light pLight = pointGo.AddComponent<Light>();
            pLight.type = LightType.Directional;
            pLight.intensity = 1.5f;
            pLight.color = new Color(0.9f, 0.95f, 1.0f);
            pLight.shadows = LightShadows.None; // Always off — no baked shadow maps in TRT
            pLight.transform.rotation = Quaternion.Euler(50f, 30f, 0f);

            RenderSettings.fog = false;

            // Find Center Terrain
            UnityEngine.Terrain centerTerrain = null;
            float minCenterDist = float.MaxValue;
            if (terrains.Length > 0)
            {
                foreach (var t in terrains) {
                    if (t.terrainData == null) continue;
                    float dist = Vector3.Distance(t.transform.position + t.terrainData.size*0.5f, boundsCenter);
                    if (dist < minCenterDist) { 
                        minCenterDist = dist; 
                        centerTerrain = t; 
                    }
                }
            }

            Vector3 rockFacePos = boundsCenter;
            Vector3 rockFaceNormal = Vector3.up;
            Vector3 plainPos = boundsCenter;
            Vector3 transitionPos = boundsCenter;
            Vector3 canyonRimPos = boundsCenter;
            Vector3 terracesPos = boundsCenter;

            if (centerTerrain != null && centerTerrain.terrainData != null)
            {
                var td = centerTerrain.terrainData;
                int res = td.heightmapResolution;
                int alphaRes = td.alphamapResolution;
                float[,,] alphas = td.GetAlphamaps(0, 0, alphaRes, alphaRes);
                
                float maxSlope = 0f;
                float minSlope = 90f;
                float bestRockScore = -1f;

                // Key light: Euler(40, 60, 0) — need forward-facing faces for lit micro shots
                // L = -forward of light transform = transform.forward negated in light convention
                Vector3 keyLightDir = new Vector3(
                    -Mathf.Sin(60f * Mathf.Deg2Rad) * Mathf.Cos(40f * Mathf.Deg2Rad),
                     Mathf.Sin(40f * Mathf.Deg2Rad),
                    -Mathf.Cos(60f * Mathf.Deg2Rad) * Mathf.Cos(40f * Mathf.Deg2Rad)
                ).normalized;

                for (int y = 32; y < res - 32; y += 8)
                {
                    for (int x = 32; x < res - 32; x += 8)
                    {
                        float nx = (float)x / (res - 1);
                        float ny = (float)y / (res - 1);
                        float steepness = td.GetSteepness(nx, ny);
                        Vector3 worldPos = new Vector3(
                            nx * td.size.x + centerTerrain.transform.position.x,
                            td.GetHeight(x, y) + centerTerrain.transform.position.y,
                            ny * td.size.z + centerTerrain.transform.position.z
                        );
                        Vector3 normal = td.GetInterpolatedNormal(nx, ny);

                        // Rock Face: steep AND front-lit by key light (avoids dark back-faces)
                        // Score combines steepness with lighting angle — pure max-steepness
                        // always picks back-lit canyon walls that appear black.
                        float litFactor = Mathf.Max(0f, Vector3.Dot(normal, keyLightDir));
                        float rockScore = steepness * (0.3f + litFactor * 0.7f); // 30% base, 70% lit
                        if (steepness > 25f && rockScore > bestRockScore)
                        {
                            bestRockScore = rockScore;
                            rockFacePos = worldPos;
                            rockFaceNormal = normal;
                        }

                        // Abyssal Plain (Flattest)
                        if (steepness < minSlope)
                        {
                            minSlope = steepness;
                            plainPos = worldPos;
                        }

                        // Biome Transition (roughly 50% rock, 50% sand)
                        int ax = Mathf.Clamp(Mathf.RoundToInt(nx * alphaRes), 0, alphaRes - 1);
                        int ay = Mathf.Clamp(Mathf.RoundToInt(ny * alphaRes), 0, alphaRes - 1);
                        if (alphas.GetLength(2) >= 4)
                        {
                            float sandWeight = alphas[ay, ax, 0];
                            float rockWeight = alphas[ay, ax, 3];
                            // Ideal transition is where both are around 0.5
                            if (sandWeight > 0.35f && rockWeight > 0.35f)
                            {
                                transitionPos = worldPos;
                            }
                        }

                        // Canyon Rim (High elevation, steep drop-off)
                        if (worldPos.y > centerTerrain.transform.position.y + 400f && steepness > 45f)
                        {
                            canyonRimPos = worldPos + Vector3.up * 10f; // Stand on the rim
                        }

                        // Terraces (Mid slope, distinct height steps)
                        if (steepness > 15f && steepness < 35f && worldPos.y < centerTerrain.transform.position.y + 300f)
                        {
                            terracesPos = worldPos;
                        }
                    }
                }
            }

            // Helper function for shots
            void TakeMatrixShot(Vector3 targetPos, Vector3 camOffset, Vector3 lookOffset, float fov, string filename, bool ortho = false, float orthoSize = 5000f)
            {
                cam.transform.position = targetPos + camOffset;
                cam.transform.LookAt(targetPos + lookOffset);
                cam.fieldOfView = fov;
                cam.orthographic = ortho;
                if (ortho) cam.orthographicSize = orthoSize;
                TakeScreenshot(cam, filename);
                cam.orthographic = false; // Reset
            }

            Vector3 centerPos = boundsCenter;

            // 1. Macro_Nadir_10km.png
            TakeMatrixShot(centerPos, Vector3.up * 8000f, Vector3.zero, 60f, ArtifactDir + "Macro_Nadir_10km.png", true, 5000f);
            
            // 2. Macro_Iso_10km.png
            cam.transform.position = centerPos + new Vector3(5000f, 5000f, -5000f);
            cam.transform.LookAt(centerPos);
            cam.orthographic = true;
            cam.orthographicSize = 4000f;
            TakeScreenshot(cam, ArtifactDir + "Macro_Iso_10km.png");
            cam.orthographic = false;

            // 3. Meso_Canyon_Top_2km.png
            TakeMatrixShot(canyonRimPos, Vector3.up * 2000f, Vector3.zero, 60f, ArtifactDir + "Meso_Canyon_Top_2km.png");

            // 4. Meso_Canyon_Angled_2km.png
            TakeMatrixShot(canyonRimPos, Vector3.up * 1000f - Vector3.forward * 1000f, Vector3.zero, 60f, ArtifactDir + "Meso_Canyon_Angled_2km.png");

            // 5. Meso_Mountain_Peak_1km.png
            TakeMatrixShot(canyonRimPos, Vector3.up * 500f + Vector3.right * 500f, Vector3.zero, 60f, ArtifactDir + "Meso_Mountain_Peak_1km.png"); // Using canyon rim as peak proxy

            // 6. Meso_Abyssal_Plain_1km.png
            TakeMatrixShot(plainPos, Vector3.up * 500f, Vector3.zero, 60f, ArtifactDir + "Meso_Abyssal_Plain_1km.png");

            // 7. Meso_Shelf_Dropoff_2km.png
            TakeMatrixShot(terracesPos, Vector3.up * 2000f - Vector3.forward * 500f, Vector3.zero, 60f, ArtifactDir + "Meso_Shelf_Dropoff_2km.png");

            // 8. Beauty_Fault_Line.png
            TakeMatrixShot(canyonRimPos, Vector3.up * 500f - Vector3.forward * 2000f, Vector3.zero, 45f, ArtifactDir + "Beauty_Fault_Line.png");

            // 9. Beauty_Sediment_Basin.png
            TakeMatrixShot(plainPos, Vector3.up * 300f - Vector3.forward * 1000f, Vector3.zero, 60f, ArtifactDir + "Beauty_Sediment_Basin.png");

            // Camera 20m back (was 10m — risk of being inside terrain at cliff base).
            // nearClipPlane 0.5f to avoid near-clip z-fighting on close faces.
            cam.nearClipPlane = 0.5f;
            Vector3 rockLookDir = new Vector3(rockFaceNormal.x, 0, rockFaceNormal.z).normalized;
            if (rockLookDir.sqrMagnitude < 0.1f) rockLookDir = Vector3.forward;
            // Point light co-located with camera — face is lit regardless of directional angle
            GameObject microLightGo = new GameObject("TRT_MicroLight");
            Light microLight = microLightGo.AddComponent<Light>();
            microLight.type = LightType.Point;
            microLight.intensity = 4.0f;
            microLight.range = 120f;
            microLight.color = Color.white;
            microLight.shadows = LightShadows.None;
            microLightGo.transform.position = rockFacePos + rockLookDir * 20f + Vector3.up * 5f;
            TakeMatrixShot(rockFacePos, rockLookDir * 20f + Vector3.up * 5f, Vector3.up * 2f, 50f, ArtifactDir + "Micro_Rock_1m.png");
            Object.DestroyImmediate(microLightGo);
            cam.nearClipPlane = 0.3f; // Restore

            // 10. Take screenshots of scattered objects (from PR 600)
            cam.backgroundColor = Color.black;
            RenderSettings.fog = true;
            TakeScatterScreenshot(cam, "kelp", ArtifactDir + "Scatter_Kelp.png", pLight);
            TakeScatterScreenshot(cam, "coral", ArtifactDir + "Scatter_Coral.png", pLight);

            Debug.Log("[TRT] All screenshots captured. Exporting Diagnostic Maps...");
            ExportDiagnosticMaps(terrains, canyonRimPos, rockFacePos, ArtifactDir);

            Debug.Log("[TRT] Done.");
            File.WriteAllText(ArtifactDir + "mcp_success.txt", $"DONE at {System.DateTime.UtcNow:O}\nTerrains={terrains.Length}");

            if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            finally
            {
                if (showGizmosProp != null)
                {
                    try
                    {
                        showGizmosProp.SetValue(null, originalShowGizmos);
                        Debug.Log("[TRT] AnnotationUtility.showGizmos restored");
                    }
                    catch (System.Exception ex) { Debug.LogWarning($"[TRT] Failed to restore AnnotationUtility.showGizmos: {ex.Message}"); }
                }
            }
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

        // Cached per-terrain instanced materials — set once per TRT run, referenced in TakeScreenshot
        private static readonly System.Collections.Generic.List<Material> _trtTerrainMaterials =
            new System.Collections.Generic.List<Material>();

        private static void TakeScreenshot(Camera cam, string filename)
        {
            // Re-push terrain material RIGHT before cam.Render() — defeats any late MapMagic override
            foreach (var t in UnityEngine.Terrain.activeTerrains)
            {
                if (t == null) continue;
                // Find or reuse our instanced material (check by name convention)
                bool found = false;
                foreach (var m in _trtTerrainMaterials)
                {
                    if (m != null && m.name.EndsWith("_" + t.name))
                    {
                        if (t.materialTemplate != m)
                            t.materialTemplate = m;
                        found = true;
                        break;
                    }
                }
                // If not cached yet, create now
                if (!found)
                {
                    Material baseMat3 = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Terrain/HectonTerrainMaterial.mat");
                    if (baseMat3 != null)
                    {
                        Material inst2 = new Material(baseMat3);
                        inst2.name = baseMat3.name + "_" + t.name;
                        Texture2DArray alb = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_AlbedoArray.asset");
                        Texture2DArray nor = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_NormalArray.asset");
                        Texture2DArray msk = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_MaskArray.asset");
                        if (alb != null) inst2.SetTexture("_AlbedoArray", alb);
                        if (nor != null) inst2.SetTexture("_NormalArray", nor);
                        if (msk != null) inst2.SetTexture("_MaskArray",   msk);
                        inst2.SetFloat("_HectonUVScale", 400.0f);
                        inst2.SetFloat("_HectonTriplanarBlend", 8.0f);
                        inst2.EnableKeyword("_NORMALMAP");
                        inst2.EnableKeyword("_MASKMAP");
                        inst2.EnableKeyword("_TERRAIN_BLEND_HEIGHT");
                        if (t.terrainData != null && t.terrainData.alphamapTextureCount > 0)
                        {
                            Texture2D[] amps = t.terrainData.alphamapTextures;
                            if (amps.Length > 0 && amps[0] != null) inst2.SetTexture("_Control",  amps[0]);
                            if (amps.Length > 1 && amps[1] != null) inst2.SetTexture("_Control1", amps[1]);
                            if (amps.Length > 2 && amps[2] != null) inst2.SetTexture("_Control2", amps[2]);
                            inst2.SetFloat("_NumLayersCount", t.terrainData.alphamapLayers);
                            inst2.SetVector("_TerrainSize", new Vector4(t.terrainData.size.x, t.terrainData.size.y, t.terrainData.size.z, 0));
                        }
                        _trtTerrainMaterials.Add(inst2);
                        t.materialTemplate = inst2;
                        t.basemapDistance  = 100000.0f;
                    }
                }
            }

            // Suppress ALL scatter gizmo rendering (isolines + vertical sticks) via direct static flag.
            // The flag check is the FIRST line in DrawScatterPreviewGizmos — guaranteed to fire before any Handles call.
            WorldProceduralScatterPreviewGizmoDrawer.TrtSuppressAll = true;

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

        private static void ExportDiagnosticMaps(UnityEngine.Terrain[] terrains, Vector3 canyonPos, Vector3 rockPos, string artifactDir)
        {
            if (terrains == null || terrains.Length == 0) return;

            // -----------------------------------------------------------------------
            // 3x3 COMPOSITE X-RAY STITCH
            // Sort all terrains by their XZ grid position, composite into 1536x1536.
            // Each tile contributes 512x512 pixels. Slope is encoded: black=flat, red=90deg.
            // This is the ONLY valid way to audit seam-less geological distribution.
            // -----------------------------------------------------------------------
            const int TilePixels  = 512;
            const int GridN       = 3;
            const int TotalPixels = TilePixels * GridN; // 1536

            // Find world AABB of all valid terrains
            float minX = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxZ = float.MinValue;
            foreach (var t in terrains)
            {
                if (t == null || t.terrainData == null) continue;
                float tx = t.transform.position.x;
                float tz = t.transform.position.z;
                if (tx < minX) minX = tx;
                if (tz < minZ) minZ = tz;
                if (tx > maxX) maxX = tx;
                if (tz > maxZ) maxZ = tz;
            }

            // Use first valid tile to get tile size
            UnityEngine.Terrain refTerrain = null;
            foreach (var t in terrains) { if (t != null && t.terrainData != null) { refTerrain = t; break; } }
            if (refTerrain == null) return;
            float tileW = refTerrain.terrainData.size.x;
            float tileH = refTerrain.terrainData.size.z;

            // Master composite texture
            Texture2D master = new Texture2D(TotalPixels, TotalPixels, TextureFormat.RGB24, false);
            // Fill with black — any missing tile shows as black gap (seam visible immediately)
            Color[] fill = new Color[TotalPixels * TotalPixels];
            for (int i = 0; i < fill.Length; i++) fill[i] = Color.black;
            master.SetPixels(fill);

            foreach (var t in terrains)
            {
                if (t == null || t.terrainData == null) continue;
                var td = t.terrainData;
                int heightRes = td.heightmapResolution;

                // Determine grid cell [0..2, 0..2]
                int gridX = Mathf.RoundToInt((t.transform.position.x - minX) / tileW);
                int gridZ = Mathf.RoundToInt((t.transform.position.z - minZ) / tileH);
                gridX = Mathf.Clamp(gridX, 0, GridN - 1);
                gridZ = Mathf.Clamp(gridZ, 0, GridN - 1);

                // Pixel offset in master
                int offX = gridX * TilePixels;
                int offY = gridZ * TilePixels;

                // Sample steepness at TilePixels x TilePixels resolution
                for (int py = 0; py < TilePixels; py++)
                {
                    for (int px = 0; px < TilePixels; px++)
                    {
                        float nx = (float)px / (TilePixels - 1);
                        float nz = (float)py / (TilePixels - 1);
                        float steepness = td.GetSteepness(nx, nz); // 0..90 degrees

                        // Encode: flat=dark blue, moderate=green, steep=red (geological tricolor)
                        float s01 = steepness / 90f;
                        Color c;
                        if (s01 < 0.33f)
                            c = Color.Lerp(new Color(0.05f, 0.1f, 0.25f), new Color(0.1f, 0.55f, 0.2f), s01 / 0.33f);
                        else if (s01 < 0.66f)
                            c = Color.Lerp(new Color(0.1f, 0.55f, 0.2f), new Color(0.9f, 0.5f, 0.05f), (s01 - 0.33f) / 0.33f);
                        else
                            c = Color.Lerp(new Color(0.9f, 0.5f, 0.05f), new Color(0.95f, 0.05f, 0.05f), (s01 - 0.66f) / 0.34f);

                        master.SetPixel(offX + px, offY + py, c);
                    }
                }
            }

            master.Apply();
            File.WriteAllBytes(artifactDir + "Debug_Slope_3x3.png", master.EncodeToPNG());
            File.WriteAllBytes(artifactDir + "Debug_Slope_10km.png", master.EncodeToPNG());
            Object.DestroyImmediate(master);
            Debug.Log($"[TRT] Debug_Slope_3x3.png and Debug_Slope_10km.png written ({TotalPixels}x{TotalPixels})");

            // -----------------------------------------------------------------------
            // 3x3 COMPOSITE HEIGHTMAP STITCH (10km scale)
            // -----------------------------------------------------------------------
            Texture2D masterH = new Texture2D(TotalPixels, TotalPixels, TextureFormat.RGB24, false);
            Color[] fillH = new Color[TotalPixels * TotalPixels];
            for (int i = 0; i < fillH.Length; i++) fillH[i] = Color.black;
            masterH.SetPixels(fillH);

            // Find global height min/max for consistent scale across 10km grid
            float globalMinH = float.MaxValue;
            float globalMaxH = float.MinValue;
            foreach (var t in terrains)
            {
                if (t == null || t.terrainData == null) continue;
                int res = t.terrainData.heightmapResolution;
                float[,] heights = t.terrainData.GetHeights(0, 0, res, res);
                for (int y = 0; y < res; y++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        float h = heights[y, x] * t.terrainData.size.y;
                        if (h < globalMinH) globalMinH = h;
                        if (h > globalMaxH) globalMaxH = h;
                    }
                }
            }
            float globalHRange = Mathf.Max(0.001f, globalMaxH - globalMinH);

            foreach (var t in terrains)
            {
                if (t == null || t.terrainData == null) continue;
                var td = t.terrainData;
                int heightRes = td.heightmapResolution;
                float[,] heights = td.GetHeights(0, 0, heightRes, heightRes);

                int gridX = Mathf.RoundToInt((t.transform.position.x - minX) / tileW);
                int gridZ = Mathf.RoundToInt((t.transform.position.z - minZ) / tileH);
                gridX = Mathf.Clamp(gridX, 0, GridN - 1);
                gridZ = Mathf.Clamp(gridZ, 0, GridN - 1);

                int offX = gridX * TilePixels;
                int offY = gridZ * TilePixels;

                for (int py = 0; py < TilePixels; py++)
                {
                    for (int px = 0; px < TilePixels; px++)
                    {
                        int hx = Mathf.Clamp(Mathf.RoundToInt((float)px / (TilePixels - 1) * (heightRes - 1)), 0, heightRes - 1);
                        int hy = Mathf.Clamp(Mathf.RoundToInt((float)py / (TilePixels - 1) * (heightRes - 1)), 0, heightRes - 1);
                        float h = heights[hy, hx] * td.size.y;
                        float h01 = (h - globalMinH) / globalHRange;
                        masterH.SetPixel(offX + px, offY + py, new Color(h01, h01, h01));
                    }
                }
            }
            masterH.Apply();
            File.WriteAllBytes(artifactDir + "Debug_Heightmap.png", masterH.EncodeToPNG());
            File.WriteAllBytes(artifactDir + "Debug_Heightmap_10km.png", masterH.EncodeToPNG());
            Object.DestroyImmediate(masterH);
            Debug.Log($"[TRT] Debug_Heightmap.png and Debug_Heightmap_10km.png written ({TotalPixels}x{TotalPixels})");

            // -----------------------------------------------------------------------
            // MESO 1km PATCH — Canyon rim area (slope-map and heightmap detail)
            // -----------------------------------------------------------------------
            ExportSinglePatch(terrains, canyonPos, 1000f, artifactDir + "Debug_Slope_1km.png");
            ExportSingleHeightmapPatch(terrains, canyonPos, 1000f, artifactDir + "Debug_Heightmap_1km.png");

            // -----------------------------------------------------------------------
            // MICRO 100m PATCH — Rock face area (slope-map and heightmap detail)
            // -----------------------------------------------------------------------
            ExportSinglePatch(terrains, rockPos, 100f, artifactDir + "Debug_Slope_100m.png");
            ExportSingleHeightmapPatch(terrains, rockPos, 100f, artifactDir + "Debug_Heightmap_100m.png");
        }

        private static UnityEngine.Terrain FindTerrainAt(UnityEngine.Terrain[] terrains, float x, float z)
        {
            foreach (var t in terrains)
            {
                if (t == null || t.terrainData == null) continue;
                Vector3 pos = t.transform.position;
                Vector3 size = t.terrainData.size;
                if (x >= pos.x && x <= pos.x + size.x &&
                    z >= pos.z && z <= pos.z + size.z)
                {
                    return t;
                }
            }
            return null;
        }

        private static float SampleSteepnessAtWorld(UnityEngine.Terrain[] terrains, float x, float z)
        {
            UnityEngine.Terrain t = FindTerrainAt(terrains, x, z);
            if (t == null)
            {
                float minDist = float.MaxValue;
                foreach (var candidate in terrains)
                {
                    if (candidate == null || candidate.terrainData == null) continue;
                    Vector3 center = candidate.transform.position + candidate.terrainData.size * 0.5f;
                    float d = Mathf.Max(Mathf.Abs(x - center.x), Mathf.Abs(z - center.z));
                    if (d < minDist) { minDist = d; t = candidate; }
                }
            }

            if (t != null && t.terrainData != null)
            {
                Vector3 pos = t.transform.position;
                Vector3 size = t.terrainData.size;
                float nx = Mathf.Clamp01((x - pos.x) / size.x);
                float nz = Mathf.Clamp01((z - pos.z) / size.z);
                return t.terrainData.GetSteepness(nx, nz);
            }
            return 0f;
        }

        private static float SampleHeightAtWorld(UnityEngine.Terrain[] terrains, float x, float z)
        {
            UnityEngine.Terrain t = FindTerrainAt(terrains, x, z);
            if (t == null)
            {
                float minDist = float.MaxValue;
                foreach (var candidate in terrains)
                {
                    if (candidate == null || candidate.terrainData == null) continue;
                    Vector3 center = candidate.transform.position + candidate.terrainData.size * 0.5f;
                    float d = Mathf.Max(Mathf.Abs(x - center.x), Mathf.Abs(z - center.z));
                    if (d < minDist) { minDist = d; t = candidate; }
                }
            }

            if (t != null && t.terrainData != null)
            {
                Vector3 pos = t.transform.position;
                Vector3 size = t.terrainData.size;
                float nx = Mathf.Clamp01((x - pos.x) / size.x);
                float nz = Mathf.Clamp01((z - pos.z) / size.z);
                return t.terrainData.GetInterpolatedHeight(nx, nz);
            }
            return 0f;
        }

        private static void ExportSinglePatch(UnityEngine.Terrain[] terrains, Vector3 worldPos, float patchMeters, string outPath)
        {
            int patchPx = 512;
            if (patchMeters < 200f) patchPx = 256;

            Texture2D tex = new Texture2D(patchPx, patchPx, TextureFormat.RGB24, false);
            float startX = worldPos.x - patchMeters * 0.5f;
            float startZ = worldPos.z - patchMeters * 0.5f;

            for (int py = 0; py < patchPx; py++)
            {
                for (int px = 0; px < patchPx; px++)
                {
                    float wx = startX + (float)px / (patchPx - 1) * patchMeters;
                    float wz = startZ + (float)py / (patchPx - 1) * patchMeters;
                    float steepness = SampleSteepnessAtWorld(terrains, wx, wz);
                    float s01 = steepness / 90f;
                    Color c;
                    if (s01 < 0.33f)
                        c = Color.Lerp(new Color(0.05f, 0.1f, 0.25f), new Color(0.1f, 0.55f, 0.2f), s01 / 0.33f);
                    else if (s01 < 0.66f)
                        c = Color.Lerp(new Color(0.1f, 0.55f, 0.2f), new Color(0.9f, 0.5f, 0.05f), (s01 - 0.33f) / 0.33f);
                    else
                        c = Color.Lerp(new Color(0.9f, 0.5f, 0.05f), new Color(0.95f, 0.05f, 0.05f), (s01 - 0.66f) / 0.34f);
                    tex.SetPixel(px, py, c);
                }
            }
            tex.Apply();
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[TRT] {System.IO.Path.GetFileName(outPath)} written ({patchPx}x{patchPx})");
        }

        private static void ExportSingleHeightmapPatch(UnityEngine.Terrain[] terrains, Vector3 worldPos, float patchMeters, string outPath)
        {
            int patchPx = 512;
            if (patchMeters < 200f) patchPx = 256;

            float startX = worldPos.x - patchMeters * 0.5f;
            float startZ = worldPos.z - patchMeters * 0.5f;

            float minH = float.MaxValue;
            float maxH = float.MinValue;
            float[] tempHeights = new float[patchPx * patchPx];

            for (int py = 0; py < patchPx; py++)
            {
                for (int px = 0; px < patchPx; px++)
                {
                    float wx = startX + (float)px / (patchPx - 1) * patchMeters;
                    float wz = startZ + (float)py / (patchPx - 1) * patchMeters;
                    float h = SampleHeightAtWorld(terrains, wx, wz);
                    tempHeights[py * patchPx + px] = h;
                    if (h < minH) minH = h;
                    if (h > maxH) maxH = h;
                }
            }
            float hRange = Mathf.Max(0.001f, maxH - minH);

            Texture2D tex = new Texture2D(patchPx, patchPx, TextureFormat.RGB24, false);
            for (int py = 0; py < patchPx; py++)
            {
                for (int px = 0; px < patchPx; px++)
                {
                    float h = tempHeights[py * patchPx + px];
                    float h01 = (h - minH) / hRange;
                    tex.SetPixel(px, py, new Color(h01, h01, h01));
                }
            }
            tex.Apply();
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[TRT] {System.IO.Path.GetFileName(outPath)} written ({patchPx}x{patchPx})");
        }

        private static void Fail(string msg)
        {
            Debug.LogError("[TRT] FAIL: " + msg);
            File.WriteAllText(ArtifactDir + "mcp_error.txt", msg);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
