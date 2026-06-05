#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using Den.Tools;
using Den.Tools.Matrices;
using MapMagic.Nodes;
using MapMagic.Nodes.MatrixGenerators;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    public static class H8VisualProofCapture1912
    {
        private const string WorldScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const string CaptureRoot = "C:/hades/Hecton8/Docs/Screenshots/MCP";
        private const string SurfaceHorizonHazeShaderPath = "Assets/_Project/Art/Shaders/H8_SurfaceHorizonHaze_1428.shader";
        private const string ActualTerrainGraphPath = "Assets/MapMagic/Map_Graph/New Gen/ACTUAL TERRAIN.asset";
        private const int CaptureWidth = 1280;
        private const int CaptureHeight = 720;

        public static void CaptureSurfaceAndExit()
        {
            CaptureSurfaceAndExit("h8_1912_surface_edit_main");
        }

        public static void CaptureSurfaceAfterQuarantineAndExit()
        {
            CaptureSurfaceAndExit("h8_1912_surface_after_quarantine_b");
        }

        public static void CaptureSurfacePatchAAndExit()
        {
            CaptureSurfaceAndExit("h8_1913_surface_patch_a");
        }

        public static void CaptureSurfaceCrestRecoveryProbeAndExit()
        {
            CaptureSurfaceCrestRecoveryProbeAndExit("h8_1914_surface_crest_recovery_probe");
        }

        public static void CaptureShallowUnderwaterPatchAAndExit()
        {
            CaptureWithPoseAndExit(
                "h8_1913_underwater_0_5m_patch_a",
                new Vector3(20f, 11.2f, 92f),
                new Vector3(24f, 9.6f, 72f),
                "temp_editor_underwater_0_5m");
        }

        public static void CaptureRouteUnderwaterPatchAAndExit()
        {
            CaptureWithPoseAndExit(
                "h8_1913_underwater_20_50m_patch_a",
                new Vector3(18f, -18f, 92f),
                new Vector3(28f, -22f, 54f),
                "temp_editor_underwater_20_50m");
        }

        private static void CaptureSurfaceAndExit(string captureName)
        {
            int exitCode = 0;
            try
            {
                Directory.CreateDirectory(CaptureRoot);
                Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                    throw new InvalidOperationException("Failed to open " + WorldScenePath);

                Camera mainCamera = Camera.main;
                if (mainCamera == null)
                    mainCamera = UnityEngine.Object.FindAnyObjectByType<Camera>(FindObjectsInactive.Exclude);
                if (mainCamera == null)
                    throw new InvalidOperationException("No camera found in " + WorldScenePath);

                RenderCamera(mainCamera, Path.Combine(CaptureRoot, captureName + ".png"));
                WriteMetadata(mainCamera, Path.Combine(CaptureRoot, captureName + ".txt"), "main_camera_edit_render");
            }
            catch (Exception ex)
            {
                exitCode = 1;
                Directory.CreateDirectory(CaptureRoot);
                File.WriteAllText(
                    Path.Combine(CaptureRoot, captureName + "_error.txt"),
                    ex.ToString(),
                    Encoding.UTF8);
                Debug.LogException(ex);
            }
            finally
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private static void CaptureSurfaceCrestRecoveryProbeAndExit(string captureName)
        {
            int exitCode = 0;
            try
            {
                Directory.CreateDirectory(CaptureRoot);
                Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                    throw new InvalidOperationException("Failed to open " + WorldScenePath);

                Camera mainCamera = Camera.main;
                if (mainCamera == null)
                    mainCamera = UnityEngine.Object.FindAnyObjectByType<Camera>(FindObjectsInactive.Exclude);
                if (mainCamera == null)
                    throw new InvalidOperationException("No camera found in " + WorldScenePath);

                ApplySurfaceCrestRecoveryProbe(mainCamera);
                RenderCamera(mainCamera, Path.Combine(CaptureRoot, captureName + ".png"));
                WriteMetadata(mainCamera, Path.Combine(CaptureRoot, captureName + ".txt"), "surface_actual_terrain_crest_recovery_probe_editor_only_unsaved");
            }
            catch (Exception ex)
            {
                exitCode = 1;
                Directory.CreateDirectory(CaptureRoot);
                File.WriteAllText(
                    Path.Combine(CaptureRoot, captureName + "_error.txt"),
                    ex.ToString(),
                    Encoding.UTF8);
                Debug.LogException(ex);
            }
            finally
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private static void CaptureWithPoseAndExit(string captureName, Vector3 position, Vector3 target, string captureTruth)
        {
            int exitCode = 0;
            try
            {
                Directory.CreateDirectory(CaptureRoot);
                Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                    throw new InvalidOperationException("Failed to open " + WorldScenePath);

                Camera mainCamera = Camera.main;
                if (mainCamera == null)
                    mainCamera = UnityEngine.Object.FindAnyObjectByType<Camera>(FindObjectsInactive.Exclude);
                if (mainCamera == null)
                    throw new InvalidOperationException("No camera found in " + WorldScenePath);

                Vector3 previousPosition = mainCamera.transform.position;
                Quaternion previousRotation = mainCamera.transform.rotation;
                float previousNear = mainCamera.nearClipPlane;
                float previousFar = mainCamera.farClipPlane;

                try
                {
                    mainCamera.transform.position = position;
                    mainCamera.transform.rotation = Quaternion.LookRotation((target - position).normalized, Vector3.up);
                    mainCamera.nearClipPlane = 0.03f;
                    mainCamera.farClipPlane = Mathf.Max(mainCamera.farClipPlane, 100000f);
                    RenderCamera(mainCamera, Path.Combine(CaptureRoot, captureName + ".png"));
                    WriteMetadata(mainCamera, Path.Combine(CaptureRoot, captureName + ".txt"), captureTruth);
                }
                finally
                {
                    mainCamera.transform.position = previousPosition;
                    mainCamera.transform.rotation = previousRotation;
                    mainCamera.nearClipPlane = previousNear;
                    mainCamera.farClipPlane = previousFar;
                }
            }
            catch (Exception ex)
            {
                exitCode = 1;
                Directory.CreateDirectory(CaptureRoot);
                File.WriteAllText(
                    Path.Combine(CaptureRoot, captureName + "_error.txt"),
                    ex.ToString(),
                    Encoding.UTF8);
                Debug.LogException(ex);
            }
            finally
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private static void ApplySurfaceCrestRecoveryProbe(Camera camera)
        {
            SetSceneObjectActive("H8_LeftPhoticCanyonWall_1446", false);
            ConfigureSurfaceHorizonHazeProbe();
            ConfigureActualTerrainMapMagicProbe(camera);

            GameObject ocean = FindSceneGameObject("H8_WORLD_CREST_OCEAN_RUNTIME_1428");
            if (ocean == null)
                return;

            Component oceanRenderer = ResolveComponentByFullName(ocean, "Crest.OceanRenderer");
            if (oceanRenderer == null)
                return;

            Material surfaceMaterial = BuildSurfaceCrestProbeMaterial();
            SerializedObject serialized = new SerializedObject(oceanRenderer);
            SetSerializedObjectReference(serialized, "_material", surfaceMaterial);
            SetSerializedBool(serialized, "_waterBodyCulling", false);
            SetSerializedFloat(serialized, "_extentsSizeMultiplier", 1800f);
            SetSerializedFloat(serialized, "_minScale", 8f);
            SetSerializedFloat(serialized, "_maxScale", 4096f);
            SetSerializedInt(serialized, "_lodDataResolution", 256);
            SetSerializedInt(serialized, "_geometryDownSampleFactor", 1);
            SetSerializedInt(serialized, "_lodCount", 8);
            SetSerializedBool(serialized, "_createSeaFloorDepthData", true);
            SetSerializedBool(serialized, "_createFoamSim", true);
            SetSerializedBool(serialized, "_createShadowData", true);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material BuildSurfaceCrestProbeMaterial()
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat");
            if (source == null)
                source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Crest/Crest/Materials/Ocean.mat");
            if (source == null)
                return null;

            Material material = new Material(source)
            {
                name = "H8_TEMP_SurfaceCrestOceanProbe_Blue_1428",
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetColor("_Diffuse", new Color(0.025f, 0.155f, 0.300f, 1.0f));
            material.SetColor("_DiffuseGrazing", new Color(0.360f, 0.720f, 0.960f, 1.0f));
            material.SetColor("_DiffuseShadow", new Color(0.020f, 0.085f, 0.160f, 1.0f));
            material.SetColor("_SubSurface", new Color(0.140f, 0.600f, 0.900f, 1.0f));
            material.SetColor("_SubSurfaceColour", new Color(0.060f, 0.240f, 0.440f, 1.0f));
            material.SetColor("_SubSurfaceShallowCol", new Color(0.500f, 0.850f, 1.000f, 1.0f));
            material.SetColor("_SubSurfaceShallowColShadow", new Color(0.080f, 0.240f, 0.360f, 1.0f));
            material.SetColor("_FoamWhiteColor", new Color(0.980f, 1.000f, 1.000f, 1.0f));
            material.SetColor("_FoamBubbleColor", new Color(0.860f, 0.980f, 1.000f, 1.0f));
            material.SetFloat("_Specular", 0.58f);
            material.SetFloat("_FresnelPower", 5.4f);
            material.SetFloat("_SubSurfaceSun", 1.18f);
            material.SetFloat("_CausticsStrength", 1.25f);
            material.SetFloat("_FoamScale", 0.026f);
            return material;
        }

        private static void ConfigureActualTerrainMapMagicProbe(Camera camera)
        {
            MapMagic.Core.MapMagicObject mapMagicObject =
                UnityEngine.Object.FindAnyObjectByType<MapMagic.Core.MapMagicObject>(FindObjectsInactive.Include);
            if (mapMagicObject == null)
                return;

            MapMagic.Nodes.Graph graph = AssetDatabase.LoadAssetAtPath<MapMagic.Nodes.Graph>(ActualTerrainGraphPath);
            if (graph == null)
                throw new InvalidOperationException("Missing MapMagic graph " + ActualTerrainGraphPath);

            SerializedObject serialized = new SerializedObject(mapMagicObject);
            SetSerializedObjectReference(serialized, "graph", graph);
            SetSerializedBool(serialized, "instantGenerate", true);
            SetSerializedBool(serialized, "draftsInEditor", true);
            SetSerializedBool(serialized, "draftsInPlaymode", true);
            SetSerializedBool(serialized, "hideFarTerrains", true);
            SetSerializedInt(serialized, "mainRange", 1);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            mapMagicObject.enabled = true;
            mapMagicObject.graph = graph;
            mapMagicObject.instantGenerate = true;
            mapMagicObject.draftsInEditor = true;
            mapMagicObject.draftsInPlaymode = true;
            mapMagicObject.hideFarTerrains = true;
            mapMagicObject.mainRange = 1;
            mapMagicObject.tiles.generateLimited = true;
            mapMagicObject.tiles.generateInfinite = true;
            mapMagicObject.tiles.generateRange = 1;
            Den.Tools.Coord coord = Den.Tools.Coord.Floor(
                camera.transform.position.x / mapMagicObject.tileSize.x,
                camera.transform.position.z / mapMagicObject.tileSize.z);
            mapMagicObject.tiles.Pin(coord, asDraft: false, holder: mapMagicObject);
            Den.Tools.Coord[] coords = { coord };
            mapMagicObject.tiles.ChangeDists(coords);
            mapMagicObject.Refresh(clearAll: true);
            mapMagicObject.StartGenerate(main: true, draft: true);
            PumpMapMagicGeneration(mapMagicObject, 90.0f);
        }

        private static void PumpMapMagicGeneration(MapMagic.Core.MapMagicObject mapMagicObject, double timeoutSeconds)
        {
            double started = EditorApplication.timeSinceStartup;
            float previousTimePerFrame = Den.Tools.Tasks.CoroutineManager.timePerFrame;
            Den.Tools.Tasks.CoroutineManager.timePerFrame = 1000f;
            try
            {
                while (mapMagicObject != null &&
                       (mapMagicObject.IsGenerating() ||
                        Den.Tools.Tasks.ThreadManager.IsWorking ||
                        Den.Tools.Tasks.CoroutineManager.IsWorking) &&
                       EditorApplication.timeSinceStartup - started < timeoutSeconds)
                {
                    mapMagicObject.Update();
                    Den.Tools.Tasks.CoroutineManager.Update();
                    EditorApplication.QueuePlayerLoopUpdate();
                    System.Threading.Thread.Sleep(50);
                }
            }
            finally
            {
                Den.Tools.Tasks.CoroutineManager.timePerFrame = previousTimePerFrame;
            }
        }

        private static void ConfigureSurfaceHorizonHazeProbe()
        {
            GameObject haze = FindSceneGameObject("SURFACE_HORIZON_SALT_HAZE_1428");
            if (haze == null)
                return;

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(SurfaceHorizonHazeShaderPath);
            if (shader == null)
                return;

            Material material = new Material(shader)
            {
                name = "H8_TEMP_SurfaceHorizonHazeProbe_1428",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = 3040
            };
            material.SetColor("_LowerTint", new Color(0.58f, 0.82f, 0.96f, 0.20f));
            material.SetColor("_UpperTint", new Color(0.96f, 0.985f, 1.00f, 0.10f));
            material.SetFloat("_Alpha", 0.18f);
            material.SetFloat("_LowerFade", 0.03f);
            material.SetFloat("_UpperFade", 0.46f);
            material.SetFloat("_Softness", 0.24f);
            material.SetFloat("_EdgeFade", 0.05f);
            material.SetFloat("_NoiseScale", 31f);
            material.SetFloat("_NoiseStrength", 0.18f);
            material.SetFloat("_GlobalQualityWeight", 0.78f);

            haze.SetActive(true);
            haze.transform.position = new Vector3(16f, 12.1f, 39f);
            haze.transform.rotation = Quaternion.Euler(1.4f, 178f, 0f);
            haze.transform.localScale = new Vector3(1.22f, 0.21f, 1.04f);

            MeshRenderer renderer = haze.GetComponent<MeshRenderer>();
            if (renderer == null)
                return;

            renderer.enabled = true;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 34;
        }

        private static void SetSceneObjectActive(string objectName, bool active)
        {
            GameObject go = FindSceneGameObject(objectName);
            if (go != null)
                go.SetActive(active);
        }

        private static Component ResolveComponentByFullName(GameObject go, string componentFullName)
        {
            if (go == null || string.IsNullOrEmpty(componentFullName))
                return null;

            Component[] components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                    continue;

                Type type = component.GetType();
                if (type != null && string.Equals(type.FullName, componentFullName, StringComparison.Ordinal))
                    return component;
            }

            return null;
        }

        private static void SetSerializedObjectReference(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
        }

        private static void SetSerializedFloat(SerializedObject serialized, string propertyName, float value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.floatValue = value;
        }

        private static void SetSerializedInt(SerializedObject serialized, string propertyName, int value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.intValue = value;
        }

        private static void SetSerializedBool(SerializedObject serialized, string propertyName, bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
                property.boolValue = value;
        }

        public static void QuarantineSurfaceRejectsAndExit()
        {
            int exitCode = 0;
            try
            {
                Directory.CreateDirectory(CaptureRoot);
                Scene scene = EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                    throw new InvalidOperationException("Failed to open " + WorldScenePath);

                StringBuilder sb = new StringBuilder(16384);
                sb.AppendLine("scene=" + scene.path);
                sb.AppendLine("action=quarantine_renderers_only");

                int disabled = 0;
                Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null)
                        continue;

                    GameObject go = renderer.gameObject;
                    string name = go.name;
                    string reason = ResolveRejectReason(name);
                    if (string.IsNullOrEmpty(reason))
                        continue;

                    if (renderer.enabled)
                    {
                        renderer.enabled = false;
                        disabled++;
                    }

                    sb.Append(name)
                        .Append(" reason=").Append(reason)
                        .Append(" activeSelf=").Append(go.activeSelf)
                        .Append(" activeHierarchy=").Append(go.activeInHierarchy)
                        .Append(" rendererEnabledNow=").Append(renderer.enabled)
                        .Append(" material=").Append(renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "NULL")
                        .AppendLine();
                }

                sb.AppendLine("disabledCount=" + disabled.ToString(CultureInfo.InvariantCulture));
                File.WriteAllText(Path.Combine(CaptureRoot, "h8_1912_surface_quarantine.txt"), sb.ToString(), Encoding.UTF8);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("SaveScene failed for " + scene.path);

                Debug.Log("[H8VisualProofCapture1912] Quarantined renderers count=" + disabled.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                exitCode = 1;
                Directory.CreateDirectory(CaptureRoot);
                File.WriteAllText(
                    Path.Combine(CaptureRoot, "h8_1912_surface_quarantine_error.txt"),
                    ex.ToString(),
                    Encoding.UTF8);
                Debug.LogException(ex);
            }
            finally
            {
                EditorApplication.Exit(exitCode);
            }
        }

        private static void RenderCamera(Camera camera, string path)
        {
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture target = null;
            Texture2D readback = null;

            try
            {
                target = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32)
                {
                    name = "H8VisualProofCapture1912_RT",
                    antiAliasing = 1,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                target.Create();

                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;

                readback = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGBA32, false, false)
                {
                    name = "H8VisualProofCapture1912_Readback",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                readback.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
                readback.Apply(false, false);

                byte[] png = readback.EncodeToPNG();
                if (png == null || png.Length == 0)
                    throw new InvalidOperationException("PNG encode failed for " + path);

                File.WriteAllBytes(path, png);
                Debug.Log("[H8VisualProofCapture1912] Wrote " + path + " bytes=" + png.Length.ToString(CultureInfo.InvariantCulture));
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (readback != null)
                    UnityEngine.Object.DestroyImmediate(readback);
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
        }

        private static void WriteMetadata(Camera camera, string path, string captureTruth)
        {
            StringBuilder sb = new StringBuilder(32768);
            sb.AppendLine("captureTruth=" + captureTruth);
            sb.AppendLine("scene=" + SceneManager.GetActiveScene().path);
            sb.AppendLine("camera=" + camera.name);
            sb.AppendLine("cameraPosition=" + camera.transform.position.ToString("F3"));
            sb.AppendLine("cameraRotation=" + camera.transform.eulerAngles.ToString("F3"));
            sb.AppendLine("cameraNearFar=" + camera.nearClipPlane.ToString("F3", CultureInfo.InvariantCulture) + "/" + camera.farClipPlane.ToString("F1", CultureInfo.InvariantCulture));
            sb.AppendLine("skybox=" + (RenderSettings.skybox != null ? RenderSettings.skybox.name : "NULL"));
            sb.AppendLine("sun=" + (RenderSettings.sun != null ? RenderSettings.sun.name : "NULL"));
            sb.AppendLine();
            AppendNamedObjectState(sb, "H8_ORGANIC_SHORELINE_FOAM_FINE_1469");
            AppendNamedObjectState(sb, "H8_WORLD_CREST_OCEAN_RUNTIME_1428");
            AppendNamedObjectState(sb, "SURFACE_HORIZON_SALT_HAZE_1428");
            AppendNamedObjectState(sb, "H8_FloorCausticSoft_1443");
            AppendNamedObjectState(sb, "H8_UnderwaterSurfaceSheet_1455");
            AppendNamedObjectState(sb, "H8_UnderwaterHazeCurtain_1454");
            AppendNamedObjectState(sb, "H8_DEPTH_LOW_SHELF_1428");
            AppendNamedObjectState(sb, "H8_WORLD_LOW_WATER_OCCLUSION_00_1428");
            AppendNamedObjectState(sb, "H8_WORLD_LOW_WATER_OCCLUSION_01_1428");
            AppendNamedObjectState(sb, "H8_WORLD_LOW_WATER_OCCLUSION_02_1428");
            AppendNamedObjectState(sb, "H8_WORLD_LOW_WATER_OCCLUSION_03_1428");
            AppendNamedObjectState(sb, "H8_DEPTH_CEILING_OCCLUSION_1428");
            AppendNamedObjectState(sb, "NOIR_UPPER_PRESSURE_LID");
            AppendNamedObjectState(sb, "H8_PhoticRouteTerrain_1464");
            AppendNamedObjectState(sb, "H8_WORLD_TERRAIN_SHELL_1428");
            sb.AppendLine();
            AppendMapMagicSummary(sb);
            sb.AppendLine();
            AppendActualTerrainGraphSummary(sb);
            sb.AppendLine();
            AppendTerrainSummary(sb, camera);
            sb.AppendLine();
            AppendMaterialSnapshot(sb, "Assets/Crest/Crest/Materials/Ocean.mat");
            AppendMaterialSnapshot(sb, "Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat");
            AppendMaterialSnapshot(sb, "Assets/_Project/Art/Materials/World/MAT_H8TerrainLit_BasaltSediment_1428.mat");
            AppendMaterialSnapshot(sb, "Assets/_Project/Art/Materials/World/Photic1464/MAT_H8_PhoticRouteTerrain_1464.mat");
            AppendMaterialSnapshot(sb, "Assets/_Project/Art/Materials/World/Photic1469/MAT_H8_ShorelineFoamFine_1469.mat");
            sb.AppendLine();
            AppendVisibleRendererSummary(sb, camera);
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            Debug.Log("[H8VisualProofCapture1912] Wrote " + path);
        }

        private static void AppendNamedObjectState(StringBuilder sb, string objectName)
        {
            GameObject go = FindSceneGameObject(objectName);
            sb.Append(objectName).Append('=');
            if (go == null)
            {
                sb.AppendLine("MISSING");
                return;
            }

            Renderer renderer = go.GetComponent<Renderer>();
            Terrain terrain = go.GetComponent<Terrain>();
            sb.Append("activeSelf=").Append(go.activeSelf)
              .Append(" activeHierarchy=").Append(go.activeInHierarchy)
              .Append(" layer=").Append(LayerMask.LayerToName(go.layer));

            if (renderer != null)
            {
                sb.Append(" rendererEnabled=").Append(renderer.enabled)
                  .Append(" bounds=").Append(renderer.bounds.size.ToString("F3"))
                  .Append(" material=").Append(renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "NULL");
            }

            if (terrain != null)
            {
                TerrainData data = terrain.terrainData;
                sb.Append(" terrainEnabled=").Append(terrain.enabled)
                  .Append(" drawHeightmap=").Append(terrain.drawHeightmap)
                  .Append(" drawInstanced=").Append(terrain.drawInstanced)
                  .Append(" heightmapPixelError=").Append(terrain.heightmapPixelError.ToString("F2", CultureInfo.InvariantCulture))
                  .Append(" basemapDistance=").Append(terrain.basemapDistance.ToString("F1", CultureInfo.InvariantCulture))
                  .Append(" terrainMaterial=").Append(terrain.materialTemplate != null ? terrain.materialTemplate.name : "NULL");
                if (data != null)
                    sb.Append(" terrainSize=").Append(data.size.ToString("F2"));
            }

            sb.AppendLine();
        }

        private static GameObject FindSceneGameObject(string objectName)
        {
            GameObject[] all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            foreach (GameObject go in all)
            {
                if (go != null && string.Equals(go.name, objectName, StringComparison.Ordinal))
                    return go;
            }

            return null;
        }

        private static void AppendVisibleRendererSummary(StringBuilder sb, Camera camera)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            sb.AppendLine("visibleRendererSummary=");
            int count = 0;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !GeometryUtility.TestPlanesAABB(planes, renderer.bounds))
                    continue;

                string name = renderer.gameObject.name;
                string materialName = renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "NULL";
                sb.Append("  ")
                    .Append(name)
                    .Append(" material=").Append(materialName)
                    .Append(" shader=").Append(renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null ? renderer.sharedMaterial.shader.name : "NULL")
                    .Append(" center=").Append(renderer.bounds.center.ToString("F2"))
                    .Append(" size=").Append(renderer.bounds.size.ToString("F2"))
                    .AppendLine();
                count++;
                if (count >= 300)
                    break;
            }
        }

        private static void AppendTerrainSummary(StringBuilder sb, Camera camera)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            Terrain[] terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
            sb.AppendLine("terrainSummary=");
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null)
                    continue;

                TerrainData data = terrain.terrainData;
                Vector3 size = data != null ? data.size : Vector3.zero;
                Bounds bounds = new Bounds(terrain.transform.position + size * 0.5f, size);
                bool inFrustum = data != null && GeometryUtility.TestPlanesAABB(planes, bounds);
                sb.Append("  ")
                    .Append(terrain.gameObject.name)
                    .Append(" activeSelf=").Append(terrain.gameObject.activeSelf)
                    .Append(" activeHierarchy=").Append(terrain.gameObject.activeInHierarchy)
                    .Append(" enabled=").Append(terrain.enabled)
                    .Append(" inFrustum=").Append(inFrustum)
                    .Append(" position=").Append(terrain.transform.position.ToString("F2"))
                    .Append(" size=").Append(size.ToString("F2"))
                    .Append(" pixelError=").Append(terrain.heightmapPixelError.ToString("F2", CultureInfo.InvariantCulture))
                    .Append(" basemapDistance=").Append(terrain.basemapDistance.ToString("F1", CultureInfo.InvariantCulture))
                    .Append(" drawInstanced=").Append(terrain.drawInstanced)
                    .Append(" material=").Append(terrain.materialTemplate != null ? terrain.materialTemplate.name : "NULL")
                    .AppendLine();
            }
        }

        private static void AppendMapMagicSummary(StringBuilder sb)
        {
            MapMagic.Core.MapMagicObject mapMagicObject =
                UnityEngine.Object.FindAnyObjectByType<MapMagic.Core.MapMagicObject>(FindObjectsInactive.Include);
            sb.AppendLine("mapMagicSummary=");
            if (mapMagicObject == null)
            {
                sb.AppendLine("  MISSING");
                return;
            }

            string graphPath = mapMagicObject.graph != null ? AssetDatabase.GetAssetPath(mapMagicObject.graph) : "NULL";
            int gridCount = mapMagicObject.tiles != null && mapMagicObject.tiles.grid != null ? mapMagicObject.tiles.grid.Count : -1;
            int pinnedCount = mapMagicObject.tiles != null && mapMagicObject.tiles.pinned != null ? mapMagicObject.tiles.pinned.Count : -1;
            int generateRange = mapMagicObject.tiles != null ? mapMagicObject.tiles.generateRange : -1;

            sb.Append("  enabled=").Append(mapMagicObject.enabled)
                .Append(" activeSelf=").Append(mapMagicObject.gameObject.activeSelf)
                .Append(" activeHierarchy=").Append(mapMagicObject.gameObject.activeInHierarchy)
                .Append(" graph=").Append(graphPath)
                .Append(" gridCount=").Append(gridCount.ToString(CultureInfo.InvariantCulture))
                .Append(" pinnedCount=").Append(pinnedCount.ToString(CultureInfo.InvariantCulture))
                .Append(" progress=").Append(mapMagicObject.GetProgress().ToString("F3", CultureInfo.InvariantCulture))
                .Append(" isGenerating=").Append(mapMagicObject.IsGenerating())
                .Append(" generateRange=").Append(generateRange.ToString(CultureInfo.InvariantCulture))
                .Append(" mainRange=").Append(mapMagicObject.mainRange.ToString(CultureInfo.InvariantCulture))
                .AppendLine();

            if (mapMagicObject.tiles == null)
                return;

            int count = 0;
            foreach (MapMagic.Terrains.TerrainTile tile in mapMagicObject.tiles.All())
            {
                if (tile == null)
                    continue;

                Terrain activeTerrain = tile.ActiveTerrain;
                sb.Append("  tile[").Append(count.ToString(CultureInfo.InvariantCulture)).Append("]")
                    .Append(" name=").Append(tile.name)
                    .Append(" coord=").Append(tile.coord.ToString())
                    .Append(" distance=").Append(tile.distance.ToString("F2", CultureInfo.InvariantCulture))
                    .Append(" activeTerrain=").Append(activeTerrain != null ? activeTerrain.name : "NULL")
                    .Append(" mainReady=").Append(tile.main != null && tile.main.applyReady)
                    .Append(" draftReady=").Append(tile.draft != null && tile.draft.applyReady);

                if (tile.main != null && tile.main.data != null)
                {
                    sb.Append(" mainGenerateReady=").Append(tile.main.generateReady)
                        .Append(" mainApplyReady=").Append(tile.main.applyReady)
                        .Append(" mainFinalizeMarks=").Append(tile.main.data.FinalizeMarksCount.ToString(CultureInfo.InvariantCulture))
                        .Append(" mainApplyMarks=").Append(tile.main.data.ApplyMarksCount.ToString(CultureInfo.InvariantCulture))
                        .Append(" mainProducts=").Append(tile.main.data.ProductsCount.ToString(CultureInfo.InvariantCulture))
                        .Append(" mainHeightOutputs=").Append(tile.main.data.OutputsCount(typeof(HeightOutput200), true).ToString(CultureInfo.InvariantCulture))
                        .Append(" mainAllOutputsReady=").Append(mapMagicObject.graph != null && tile.main.data.AllOutputsReady(mapMagicObject.graph, OutputLevel.Draft | OutputLevel.Main, true));
                }

                if (tile.draft != null && tile.draft.data != null)
                {
                    sb.Append(" draftGenerateReady=").Append(tile.draft.generateReady)
                        .Append(" draftApplyReady=").Append(tile.draft.applyReady)
                        .Append(" draftFinalizeMarks=").Append(tile.draft.data.FinalizeMarksCount.ToString(CultureInfo.InvariantCulture))
                        .Append(" draftApplyMarks=").Append(tile.draft.data.ApplyMarksCount.ToString(CultureInfo.InvariantCulture))
                        .Append(" draftProducts=").Append(tile.draft.data.ProductsCount.ToString(CultureInfo.InvariantCulture))
                        .Append(" draftHeightOutputs=").Append(tile.draft.data.OutputsCount(typeof(HeightOutput200), true).ToString(CultureInfo.InvariantCulture))
                        .Append(" draftAllOutputsReady=").Append(mapMagicObject.graph != null && tile.draft.data.AllOutputsReady(mapMagicObject.graph, OutputLevel.Draft, true));
                }

                sb.AppendLine();

                count++;
                if (count >= 12)
                    break;
            }
        }

        private static void AppendActualTerrainGraphSummary(StringBuilder sb)
        {
            Graph graph = AssetDatabase.LoadAssetAtPath<Graph>(ActualTerrainGraphPath);
            sb.AppendLine("actualTerrainGraphSummary=");
            if (graph == null)
            {
                sb.AppendLine("  MISSING " + ActualTerrainGraphPath);
                return;
            }

            sb.Append("  graphPath=").Append(AssetDatabase.GetAssetPath(graph))
                .Append(" generatorCount=").Append(graph.generators != null ? graph.generators.Length.ToString(CultureInfo.InvariantCulture) : "NULL")
                .Append(" linkCount=").Append(graph.links != null ? graph.links.Count.ToString(CultureInfo.InvariantCulture) : "NULL")
                .AppendLine();

            HeightOutput200 heightOutput = FindFirstGenerator<HeightOutput200>(graph);
            HectonBiomeMatrixMapMagicPostProcessNode tectonic = FindFirstGenerator<HectonBiomeMatrixMapMagicPostProcessNode>(graph);
            HectonHydraulicErosionMapMagicNode erosion = FindFirstGenerator<HectonHydraulicErosionMapMagicNode>(graph);
            HectonTerrainSplatmapMapMagicNode splat = FindFirstGenerator<HectonTerrainSplatmapMapMagicNode>(graph);
            HectonAnomalyMapMagicNode anomaly = FindFirstGenerator<HectonAnomalyMapMagicNode>(graph);

            AppendGeneratorSummary(sb, "heightOutput", heightOutput);
            AppendLinkSummary(sb, "heightOutput.in", graph, heightOutput);
            AppendGeneratorSummary(sb, "tectonic", tectonic);
            AppendLinkSummary(sb, "tectonic.in", graph, tectonic);
            AppendGeneratorSummary(sb, "erosion", erosion);
            AppendLinkSummary(sb, "erosion.heightIn", graph, erosion != null ? erosion.heightIn : null);
            AppendGeneratorSummary(sb, "splat", splat);
            AppendLinkSummary(sb, "splat.heightIn", graph, splat != null ? splat.heightIn : null);
            AppendLinkSummary(sb, "splat.sedimentIn", graph, splat != null ? splat.sedimentIn : null);
            AppendGeneratorSummary(sb, "anomaly", anomaly);
            AppendLinkSummary(sb, "anomaly.heightIn", graph, anomaly != null ? anomaly.heightIn : null);

            int importIndex = 0;
            if (graph.generators == null)
                return;

            for (int i = 0; i < graph.generators.Length; i++)
            {
                if (!(graph.generators[i] is Import200 import))
                    continue;

                sb.Append("  import[").Append(importIndex.ToString(CultureInfo.InvariantCulture)).Append("]")
                    .Append(" enabled=").Append(import.enabled)
                    .Append(" id=").Append(import.id.ToString(CultureInfo.InvariantCulture))
                    .Append(" version=").Append(import.version.ToString(CultureInfo.InvariantCulture))
                    .Append(" scale=").Append(import.scale.ToString("F3", CultureInfo.InvariantCulture))
                    .Append(" offset=").Append(import.offset.ToString("F3"));

                MatrixAsset matrixAsset = import.matrixAsset;
                sb.Append(" matrixAsset=").Append(matrixAsset != null ? AssetDatabase.GetAssetPath(matrixAsset) : "NULL");
                AppendMatrixAssetStats(sb, matrixAsset);
                sb.AppendLine();
                importIndex++;
            }
        }

        private static T FindFirstGenerator<T>(Graph graph)
            where T : Generator
        {
            if (graph == null || graph.generators == null)
                return null;

            for (int i = 0; i < graph.generators.Length; i++)
            {
                if (graph.generators[i] is T generator)
                    return generator;
            }

            return null;
        }

        private static void AppendGeneratorSummary(StringBuilder sb, string label, Generator generator)
        {
            sb.Append("  ").Append(label).Append('=');
            if (generator == null)
            {
                sb.AppendLine("MISSING");
                return;
            }

            sb.Append("type=").Append(generator.GetType().FullName)
                .Append(" enabled=").Append(generator.enabled)
                .Append(" id=").Append(generator.id.ToString(CultureInfo.InvariantCulture))
                .Append(" version=").Append(generator.version.ToString(CultureInfo.InvariantCulture))
                .AppendLine();
        }

        private static void AppendLinkSummary(StringBuilder sb, string label, Graph graph, IInlet<object> inlet)
        {
            sb.Append("  link ").Append(label).Append('=');
            if (graph == null || inlet == null)
            {
                sb.AppendLine("MISSING_INLET");
                return;
            }

            IOutlet<object> outlet = graph.GetLink(inlet);
            if (outlet == null)
            {
                sb.AppendLine("UNLINKED linkedOutletId=" + inlet.LinkedOutletId.ToString(CultureInfo.InvariantCulture));
                return;
            }

            Generator source = outlet.Gen;
            sb.Append("sourceType=").Append(source != null ? source.GetType().FullName : "NULL")
                .Append(" sourceId=").Append(source != null ? source.id.ToString(CultureInfo.InvariantCulture) : "NULL")
                .Append(" sourceVersion=").Append(source != null ? source.version.ToString(CultureInfo.InvariantCulture) : "NULL")
                .Append(" outletId=").Append(outlet.Id.ToString(CultureInfo.InvariantCulture))
                .Append(" storedLinkedOutletId=").Append(inlet.LinkedOutletId.ToString(CultureInfo.InvariantCulture))
                .AppendLine();
        }

        private static void AppendMatrixAssetStats(StringBuilder sb, MatrixAsset asset)
        {
            if (asset == null || asset.matrix == null || asset.matrix.arr == null)
            {
                sb.Append(" matrixStats=NULL");
                return;
            }

            float[] values = asset.matrix.arr;
            int count = values.Length;
            int finiteCount = 0;
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            double sum = 0.0;
            for (int i = 0; i < count; i++)
            {
                float value = values[i];
                if (float.IsNaN(value) || float.IsInfinity(value))
                    continue;

                if (value < min)
                    min = value;
                if (value > max)
                    max = value;
                sum += value;
                finiteCount++;
            }

            sb.Append(" matrixRect=").Append(asset.matrix.rect.ToString())
                .Append(" matrixCount=").Append(count.ToString(CultureInfo.InvariantCulture))
                .Append(" finiteCount=").Append(finiteCount.ToString(CultureInfo.InvariantCulture));

            if (finiteCount > 0)
            {
                sb.Append(" min=").Append(min.ToString("F5", CultureInfo.InvariantCulture))
                    .Append(" max=").Append(max.ToString("F5", CultureInfo.InvariantCulture))
                    .Append(" avg=").Append((sum / finiteCount).ToString("F5", CultureInfo.InvariantCulture));
            }
        }

        private static void AppendMaterialSnapshot(StringBuilder sb, string assetPath)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            sb.Append("materialSnapshot ").Append(assetPath).Append('=');
            if (material == null)
            {
                sb.AppendLine("MISSING");
                return;
            }

            sb.Append("name=").Append(material.name)
                .Append(" shader=").Append(material.shader != null ? material.shader.name : "NULL");
            AppendColorIfPresent(sb, material, "_BaseColor");
            AppendColorIfPresent(sb, material, "_Tint");
            AppendColorIfPresent(sb, material, "_ShadowTint");
            AppendColorIfPresent(sb, material, "_Diffuse");
            AppendColorIfPresent(sb, material, "_DiffuseGrazing");
            AppendColorIfPresent(sb, material, "_SubSurfaceShallowCol");
            AppendFloatIfPresent(sb, material, "_ClipSurface");
            AppendFloatIfPresent(sb, material, "_ClipUnderTerrain");
            AppendFloatIfPresent(sb, material, "_Foam");
            AppendFloatIfPresent(sb, material, "_WaveFoamStrength");
            AppendFloatIfPresent(sb, material, "_CausticsStrength");
            sb.AppendLine();
        }

        private static void AppendColorIfPresent(StringBuilder sb, Material material, string propertyName)
        {
            if (!material.HasProperty(propertyName))
                return;

            sb.Append(' ').Append(propertyName).Append('=').Append(material.GetColor(propertyName).ToString("F3"));
        }

        private static void AppendFloatIfPresent(StringBuilder sb, Material material, string propertyName)
        {
            if (!material.HasProperty(propertyName))
                return;

            sb.Append(' ').Append(propertyName).Append('=').Append(material.GetFloat(propertyName).ToString("F3", CultureInfo.InvariantCulture));
        }

        private static bool LooksRelevantForSurfaceAudit(string name, string materialName)
        {
            return ContainsAny(name, "Foam", "Water", "Ocean", "Boulder", "Rock", "NOIR", "Slab", "Curtain", "Band", "Cyan", "Caustic", "Aegir")
                || ContainsAny(materialName, "Foam", "Water", "Ocean", "Boulder", "Rock", "NOIR", "Caustic", "Aegir");
        }

        private static string ResolveRejectReason(string name)
        {
            if (ContainsAny(name, "MESH_SurfaceFoamRibbon_1428_", "H8_SurfaceFoamTopOnly_1458_", "H8_BrokenShoreFoam", "H8_BrokenReadableFoam", "H8_VisibleBrokenFoam_1435"))
                return "broken_debug_foam_sheet";

            if (ContainsAny(name, "H8_HeroWetBasaltBoulder_1453_"))
                return "black_primitive_foreground_boulder";

            if (ContainsAny(name, "NOIR_LEFT_VIGNETTE_SLAB", "NOIR_RIGHT_VIGNETTE_SLAB", "NOIR_UPPER_PRESSURE_LID", "NOIR_FAR_WATER_CURTAIN", "NOIR_MIDWATER_VEIL", "SURFACE_SKY_NOIR_BACKDROP_1428", "SURFACE_SKY_DOME_NOIR_1428"))
                return "surface_noir_slab_or_curtain";

            if (ContainsAny(name, "WaterColumnBand_", "Water_Mass_Mid_1428", "Water_Mass_Far_1428", "H8_WORLD_CYAN_DEPTH_LANE_", "NOIR_CYAN_INSTRUMENT_TICK_"))
                return "debug_depth_band_or_cyan_lane";

            if (ContainsAny(name, "H8_PHOTIC_ROCK_GARDEN_1469"))
                return "black_rejected_photic_rock_garden";

            if (ContainsAny(name, "H8_FloorCausticSoft_1443"))
                return "yellow_surface_visible_caustic_sheet";

            if (ContainsAny(name, "H8_PHOTIC_SOFT_WATER_HAZE_1430"))
                return "flat_green_surface_haze_sheet";

            return null;
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            for (int i = 0; i < tokens.Length; i++)
            {
                if (value.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }
}
#endif
