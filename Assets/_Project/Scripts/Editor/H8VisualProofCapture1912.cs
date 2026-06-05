#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
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
