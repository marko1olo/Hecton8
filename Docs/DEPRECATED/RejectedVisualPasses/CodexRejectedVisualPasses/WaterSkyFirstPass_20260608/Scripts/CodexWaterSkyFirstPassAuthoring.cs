#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools
{
    [InitializeOnLoad]
    public static class CodexWaterSkyFirstPassAuthoring
    {
        private const string ScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
        private const string RootName = "H8_CODEX_WATER_SKY_FIRST_PASS_20260608";
        private const string RequestRelativePath = "Temp/CodexWaterSkyFirstPass.request";
        private const string ReportRelativePath = "Docs/AgentLogs/CODEX_WATER_SKY_FIRST_PASS_20260608.txt";
        private const string ScreenshotRelativeRoot = "Docs/Screenshots/CodexWaterSkyFirstPass";
        private const string MaterialRoot = "Assets/_Project/Art/Materials/Codex/WaterSkyFirstPass";
        private const string MeshRoot = "Assets/_Project/Art/Meshes/Codex/WaterSkyFirstPass";
        private const string TextureRoot = "Assets/_Project/Art/Textures/Codex/WaterSkyFirstPass";
        private const string Batch21SeabedSource = "Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604_Gemini_155742.png";

        private static readonly int ProofProbeBufferId = Shader.PropertyToID("_H8CustomLightProbeGrid");
        private static readonly int ProofShBufferId = Shader.PropertyToID("_HectonGIRelaySHBuffer");
        private static readonly int ProofGIParamsId = Shader.PropertyToID("_H8InteriorGIProbeParams");
        private static readonly int ProofGIOriginId = Shader.PropertyToID("_H8InteriorGIProbeOrigin");
        private static readonly int ProofGIRootAupId = Shader.PropertyToID("_H8InteriorGIProbeRootAup");
        private static readonly int ProofGIStateId = Shader.PropertyToID("_H8CustomLightProbeGridState");
        private static readonly int ProofEnvAmbientId = Shader.PropertyToID("_H8EnvironmentAmbientColor");
        private static readonly int ProofEnvFogId = Shader.PropertyToID("_H8EnvironmentFogColor");
        private static readonly int ProofEnvDirectionalId = Shader.PropertyToID("_H8EnvironmentDirectionalLightColor");
        private static readonly int ProofEnvScalarId = Shader.PropertyToID("_H8EnvironmentScalarParams");
        private static readonly int ProofEnvDebugBlocksId = Shader.PropertyToID("_H8EnvironmentDebugBlocks");

        private static readonly string[] DeprecatedSceneRoots =
        {
            "Ocean_DeepVeil",
            "Ocean_AbyssRibbon",
            "H8_WORLD_SURFACE_START_1428",
            "H8_WORLD_VISUAL_COMPOSITION_1428",
            "H8_WORLD_NOIR_STAGING_1428",
            "H8_WORLD_VISUAL_POLISH_1428",
            "H8_WATER_SURFACE_REEF_PASS_1447",
            "H8_VISIBLE_WATER_POLISH_PASS_1438",
            "H8_SURFACE_FOAM_REBUILD_PASS_1432",
            "H8_SURFACE_FOAM_TOPONLY_PASS_1458",
            "H8_SURFACE_FOAM_PATCHCLOUD_1459",
            "H8_SURFACE_OCEAN_READ_1428",
            "H8_SURFACE_SHORE_FOAM_1428",
            "H8_WORLD_SHORELINE_FOAM_ONLY_1428",
            "H8_WATER_SURFACE_CAUSTIC_PASS_1443",
            "H8_SURFACE_SKY_CARD_1428",
            "H8_SURFACE_CLOUD_PANORAMA_1428",
            "H8_ATMOSPHERE_CELESTIAL_OWNERS_1428",
            "H8_AEGIR_SKY_BACKDROP_1428",
            "H8_AEGIR_ATMOSPHERE_VEIL_1428",
            "H8_SURFACE_GAS_GIANT_DISC_1428",
            "H8_SURFACE_MOON_KHEPRI_REAL_1428",
            "H8_SURFACE_MOON_THALOS_REAL_1428",
            "H8_SURFACE_CLOUD_DECK_LOW_1428",
            "H8_SURFACE_CLOUD_DECK_HIGH_1428",
            "H8_SURFACE_CLOUD_DECK_HORIZON_1428",
            "H8_ATMOSPHERIC_CLOUD_DECK_1428",
            "Sky_System",
            "H8_PHOTIC_REEF_DETAIL_PASS_1464",
            "H8_SURFACE_LITTORAL_REBUILD_PASS_1430",
            "H8_WATER_TERRAIN_MATERIAL_PASS_1453",
            "H8_SURFACE_COASTAL_ISLAND_1428",
            "H8_WATER_FLORA_TERRAIN_PASS_1446",
            "H8_ORGANIC_SHORELINE_FOAM_FINE_1469",
            "H8_ORGANIC_SHORELINE_BREAKUP_1469",
            "H8_SURFACE_COAST_GEOLOGY_1428",
        };

        static CodexWaterSkyFirstPassAuthoring()
        {
            EditorApplication.delayCall += ApplyIfRequested;
        }

        [MenuItem("HECTON-8/Codex/Apply Water Sky First Pass")]
        public static void ApplyFromMenu()
        {
            ApplyInternal(exitAfterApply: false);
        }

        public static void ApplyAndExit()
        {
            int exitCode = 0;
            try
            {
                ApplyInternal(exitAfterApply: true);
            }
            catch (Exception exception)
            {
                exitCode = 1;
                WriteFailureReport(exception);
                Debug.LogException(exception);
            }

            EditorApplication.Exit(exitCode);
        }

        private static void ApplyIfRequested()
        {
            string requestPath = AbsoluteProjectPath(RequestRelativePath);
            if (!File.Exists(requestPath))
                return;

            try
            {
                ApplyInternal(exitAfterApply: false);
                File.Delete(requestPath);
            }
            catch (Exception exception)
            {
                WriteFailureReport(exception);
                Debug.LogException(exception);
            }
        }

        private static void ApplyInternal(bool exitAfterApply)
        {
            Directory.CreateDirectory(AbsoluteProjectPath(ScreenshotRelativeRoot));
            EnsureAssetDirectory(MaterialRoot);
            EnsureAssetDirectory(MeshRoot);
            EnsureAssetDirectory(TextureRoot);

            Scene scene = EnsureTargetSceneLoaded();
            RemoveExistingRoot(scene);

            GameObject root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            int deprecatedCount = DeprecateCompetingWaterSkyObjects(scene);
            Camera sourceCamera = ResolveSourceCamera();
            Vector3 focus = new Vector3(2600f, -2.5f, 2600f);
            Vector3 cameraPosition = focus + new Vector3(-96f, 16f, 118f);
            Vector3 cameraForward = (focus + Vector3.up * 5.2f - cameraPosition).normalized;

            Mesh waterSurfaceMesh = UpsertMesh(MeshRoot + "/MSH_Codex_IrregularOceanSurface_20260608.asset", BuildIrregularOceanSurfaceMesh(76, 160, "MSH_Codex_IrregularOceanSurface_20260608"));
            Mesh seabedGrid = UpsertMesh(MeshRoot + "/MSH_Codex_ShallowSeabedGrid_20260608.asset", BuildSeabedGridMesh(112, 1f, "MSH_Codex_ShallowSeabedGrid_20260608"));
            Mesh limestonePatch = UpsertMesh(MeshRoot + "/MSH_Codex_LimestoneShelfPatch_20260608.asset", BuildLimestonePatchMesh("MSH_Codex_LimestoneShelfPatch_20260608"));
            Mesh limestoneRidge = UpsertMesh(MeshRoot + "/MSH_Codex_LimestoneShelfRidge_20260608.asset", BuildLimestoneRidgeMesh("MSH_Codex_LimestoneShelfRidge_20260608"));
            Mesh unitQuad = UpsertMesh(MeshRoot + "/MSH_Codex_UnitSkyQuad_20260608.asset", BuildUnitQuadMesh("MSH_Codex_UnitSkyQuad_20260608"));
            Mesh foamRibbon = UpsertMesh(MeshRoot + "/MSH_Codex_FoamRibbon_20260608.asset", BuildFoamRibbonMesh("MSH_Codex_FoamRibbon_20260608"));
            Mesh curtainRibbon = UpsertMesh(MeshRoot + "/MSH_Codex_WaterCurtain_20260608.asset", BuildCurtainMesh("MSH_Codex_WaterCurtain_20260608"));
            Mesh skyDome = UpsertMesh(MeshRoot + "/MSH_Codex_SkyDome_20260608.asset", BuildSkyDomeMesh("MSH_Codex_SkyDome_20260608"));

            Texture2D aegirTexture = UpsertPngTexture(TextureRoot + "/TEX_Codex_AegirDisc_20260608.png", BuildAegirTexture(1024));
            Texture2D moonTexture = UpsertPngTexture(TextureRoot + "/TEX_Codex_MoonDisc_20260608.png", BuildMoonTexture(512));
            Texture2D skyTexture = UpsertPngTexture(TextureRoot + "/TEX_Codex_CyanNoirSkyGradient_20260608.png", BuildSkyGradientTexture(1024, 512));
            Texture2D seabedTexture = UpsertPngTexture(TextureRoot + "/TEX_Codex_Batch21ReadableSeabed_20260608.png", LoadPngTextureOrFallback(AbsoluteProjectPath(Batch21SeabedSource), BuildSeabedFallbackTexture(1024)), TextureWrapMode.Repeat);
            Texture2D waterFlowTexture = UpsertPngTexture(TextureRoot + "/TEX_Codex_WaterFlowSurface_20260608.png", BuildWaterFlowTexture(1024), TextureWrapMode.Repeat);
            Texture2D softCausticTexture = UpsertPngTexture(TextureRoot + "/TEX_Codex_SoftCausticNet_20260608.png", BuildSoftCausticTexture(1024), TextureWrapMode.Repeat);

            Material seabed = UpsertMaterial(MaterialRoot + "/MAT_Codex_Batch21ReadableSeabed_20260608.mat", new Color(0.60f, 0.78f, 0.72f, 1f), false, seabedTexture, 0f);
            Material limestone = UpsertMaterial(MaterialRoot + "/MAT_Codex_PaleLimestoneShelf_20260608.mat", new Color(0.58f, 0.72f, 0.65f, 1f), false, seabedTexture, 0f);
            Material shoalLimestone = UpsertMaterial(MaterialRoot + "/MAT_Codex_WetLimestoneShoalCaps_20260608.mat", new Color(0.72f, 0.84f, 0.74f, 1f), false, seabedTexture, 0.025f);
            Material waterSurface = UpsertWaterSurfaceMaterial(
                MaterialRoot + "/MAT_Codex_ReadableOceanSurface_20260608.mat",
                new Color(0.05f, 0.42f, 0.52f, 0.86f),
                waterFlowTexture,
                AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Crest/Crest/Textures/WaveNormals/WaveNormals.png"));
            Material deepMass = UpsertMaterial(MaterialRoot + "/MAT_Codex_DeepWaterMass_20260608.mat", new Color(0.02f, 0.16f, 0.22f, 0.24f), true, null, 0.04f);
            Material foam = UpsertMaterial(MaterialRoot + "/MAT_Codex_MineralFoamLace_20260608.mat", new Color(0.78f, 0.94f, 0.91f, 0.22f), true, AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Crest/Crest/Textures/Foam2.png"), 0.03f);
            Material caustic = UpsertMaterial(MaterialRoot + "/MAT_Codex_FloorCausticRibbons_20260608.mat", new Color(0.55f, 0.95f, 1f, 0.075f), true, softCausticTexture, 0.04f);
            Material particulate = UpsertMaterial(MaterialRoot + "/MAT_Codex_SuspendedParticulate_20260608.mat", new Color(0.80f, 0.92f, 0.88f, 0.15f), true, null, 0.04f);
            Material curtain = UpsertMaterial(MaterialRoot + "/MAT_Codex_WaterColumnCurtain_20260608.mat", new Color(0.02f, 0.30f, 0.40f, 0.022f), true, null, 0.01f);
            Material sky = UpsertMaterial(MaterialRoot + "/MAT_Codex_SkyGradient_20260608.mat", Color.white, false, skyTexture, 0.8f);
            Material aegir = UpsertMaterial(MaterialRoot + "/MAT_Codex_Aegir_20260608.mat", Color.white, true, aegirTexture, 0.9f);
            Material moon = UpsertMaterial(MaterialRoot + "/MAT_Codex_Moons_20260608.mat", Color.white, true, moonTexture, 0.8f);
            SetMaterialTextureScale(seabed, new Vector2(34f, 34f));
            SetMaterialTextureScale(limestone, new Vector2(4f, 3f));
            SetMaterialTextureScale(shoalLimestone, new Vector2(6f, 5f));
            SetMaterialTextureScale(waterSurface, new Vector2(20f, 18f));
            SetMaterialTextureScale(foam, new Vector2(2f, 1f));
            SetMaterialTextureScale(caustic, new Vector2(4f, 2f));

            TuneMainCamera(sourceCamera, cameraPosition, cameraForward);
            // Legacy ocean prefabs currently require project runtime buffers in edit-mode proof renders.
            BuildWaterStack(root.transform, focus, waterSurfaceMesh, seabedGrid, limestonePatch, limestoneRidge, unitQuad, foamRibbon, curtainRibbon, seabed, limestone, shoalLimestone, waterSurface, deepMass, foam, caustic, particulate, curtain);
            BuildSkyAndAegir(root.transform, sourceCamera, cameraPosition, cameraForward, unitQuad, skyDome, sky, aegir, moon);
            TuneLighting(focus);

            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            GraphicsBuffer proofProbeBuffer = null;
            GraphicsBuffer proofShBuffer = null;
            List<string> proofPaths;
            try
            {
                BindEditorProofLightingFallback(focus, out proofProbeBuffer, out proofShBuffer);
                proofPaths = CaptureProofs(sourceCamera, focus);
            }
            finally
            {
                ReleaseProofBuffer(ref proofProbeBuffer);
                ReleaseProofBuffer(ref proofShBuffer);
            }

            WriteSuccessReport(deprecatedCount, proofPaths, exitAfterApply);
            Debug.Log("[CodexWaterSkyFirstPassAuthoring] Applied " + RootName + ", deprecated=" + deprecatedCount);
        }

        private static Scene EnsureTargetSceneLoaded()
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid() && string.Equals(activeScene.path, ScenePath, StringComparison.OrdinalIgnoreCase))
                return activeScene;

            if (activeScene.IsValid() && activeScene.isDirty)
                EditorSceneManager.SaveOpenScenes();

            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static void RemoveExistingRoot(Scene scene)
        {
            Transform existing = FindSceneTransform(scene, RootName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        private static int DeprecateCompetingWaterSkyObjects(Scene scene)
        {
            int count = 0;
            for (int i = 0; i < DeprecatedSceneRoots.Length; i++)
            {
                Transform transform = FindSceneTransform(scene, DeprecatedSceneRoots[i]);
                if (transform == null)
                    continue;

                GameObject gameObject = transform.gameObject;
                if (!gameObject.name.StartsWith("DEPRECATED_", StringComparison.Ordinal))
                    gameObject.name = "DEPRECATED_WATER_SKY_20260608__" + gameObject.name;

                gameObject.SetActive(false);
                EditorUtility.SetDirty(gameObject);
                count++;
            }

            return count;
        }

        private static void TryInstantiateOceanPrefabs(Transform parent, Vector3 focus)
        {
            TryInstantiatePrefab(
                "Assets/_Project/Prefabs/Hecton Ocean.prefab",
                "H8_CODEX_HECTON_OCEAN_PREFAB_SOURCE_20260608",
                parent,
                new Vector3(focus.x, 0f, focus.z),
                Quaternion.identity,
                new Vector3(42f, 1f, 42f),
                faceCamera: false,
                sourceCamera: null,
                fallbackCameraPosition: default);

            TryInstantiatePrefab(
                "Assets/_Project/Prefabs/Ocean_Crest.prefab",
                "H8_CODEX_CREST_OCEAN_RUNTIME_SOURCE_20260608",
                parent,
                new Vector3(focus.x, 0f, focus.z),
                Quaternion.identity,
                Vector3.one,
                faceCamera: false,
                sourceCamera: null,
                fallbackCameraPosition: default);
        }

        private static GameObject TryInstantiatePrefab(
            string prefabPath,
            string instanceName,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            bool faceCamera,
            Camera sourceCamera,
            Vector3 fallbackCameraPosition)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return null;

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                return null;

            instance.name = instanceName;
            instance.transform.SetParent(parent, false);
            instance.transform.position = position;
            instance.transform.rotation = rotation;
            instance.transform.localScale = scale;
            if (faceCamera)
                FaceCamera(instance.transform, sourceCamera, fallbackCameraPosition);
            instance.SetActive(true);
            EditorUtility.SetDirty(instance);
            return instance;
        }

        private static void BuildWaterStack(
            Transform root,
            Vector3 focus,
            Mesh waterSurfaceMesh,
            Mesh seabedGrid,
            Mesh limestonePatch,
            Mesh limestoneRidge,
            Mesh unitQuad,
            Mesh foamRibbon,
            Mesh curtainRibbon,
            Material seabed,
            Material limestone,
            Material shoalLimestone,
            Material waterSurface,
            Material deepMass,
            Material foam,
            Material caustic,
            Material particulate,
            Material curtain)
        {
            GameObject floor = CreateMeshObject("H8_CODEX_PHOTIC_SEABED_READABLE_FLOOR_20260608", root, seabedGrid, seabed);
            floor.transform.position = new Vector3(focus.x, -19.5f, focus.z - 18f);
            floor.transform.rotation = Quaternion.Euler(0f, 7f, 0f);
            floor.transform.localScale = new Vector3(11.5f, 1f, 11.5f);

            GameObject originFloor = CreateMeshObject("H8_CODEX_ORIGIN_SANITY_SEABED_PATCH_20260608", root, seabedGrid, seabed);
            originFloor.transform.position = new Vector3(0f, -19.8f, 88f);
            originFloor.transform.rotation = Quaternion.Euler(0f, -11f, 0f);
            originFloor.transform.localScale = new Vector3(4.0f, 1f, 4.0f);

            for (int i = 0; i < 6; i++)
            {
                float angle = i * 53.5f + Mathf.Sin(i * 0.71f) * 9f;
                float radius = 22f + Mathf.Sin(i * 1.17f) * 7f;
                Vector3 position = focus + new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * radius, -2.35f - (i % 3) * 0.38f, Mathf.Cos(angle * Mathf.Deg2Rad) * radius);
                GameObject shelf = CreateMeshObject("H8_CODEX_LIMESTONE_WATERLINE_SHELF_" + i.ToString("00", CultureInfo.InvariantCulture) + "_20260608", root, limestonePatch, shoalLimestone);
                shelf.transform.position = position;
                shelf.transform.rotation = Quaternion.Euler(0f, angle + 15f, 0f);
                shelf.transform.localScale = new Vector3(12f + (i % 4) * 4.2f, 2.1f, 6.2f + (i % 3) * 1.8f);
            }

            for (int i = 0; i < 8; i++)
            {
                float lane = i - 3.5f;
                float x = lane * 22f + Mathf.Sin(i * 1.27f) * 11f;
                float z = 42f - i * 22f + Mathf.Sin(i * 0.77f) * 10f;
                GameObject cap = CreateMeshObject("H8_CODEX_PHOTIC_ROUTE_SHELF_CAP_" + i.ToString("00", CultureInfo.InvariantCulture) + "_20260608", root, limestonePatch, limestone);
                cap.transform.position = new Vector3(focus.x + x, -5.2f - (i % 3) * 0.45f, focus.z + z);
                cap.transform.rotation = Quaternion.Euler(0f, -28f + i * 18.5f, 0f);
                cap.transform.localScale = new Vector3(24f + (i % 4) * 6f, 3.0f + (i % 3) * 0.45f, 10f + (i % 5) * 2.6f);

                GameObject capFoam = CreateMeshObject("H8_CODEX_ROUTE_SHELF_CONTACT_FOAM_" + i.ToString("00", CultureInfo.InvariantCulture) + "_20260608", root, foamRibbon, foam);
                capFoam.transform.position = new Vector3(focus.x + x + Mathf.Sin(i * 0.91f) * 3f, -0.18f, focus.z + z + Mathf.Cos(i * 0.68f) * 4f);
                capFoam.transform.rotation = Quaternion.Euler(90f, -8f + i * 19.0f, 0f);
                capFoam.transform.localScale = new Vector3(24f + (i % 4) * 6f, 0.86f + (i % 3) * 0.22f, 1f);
            }

            for (int i = 0; i < 5; i++)
            {
                float x = -58f + i * 28f + Mathf.Sin(i * 1.4f) * 8f;
                float z = 68f - i * 17f + Mathf.Cos(i * 0.9f) * 7f;
                GameObject shoal = CreateMeshObject("H8_CODEX_FOREGROUND_LIMESTONE_SHOAL_" + i.ToString("00", CultureInfo.InvariantCulture) + "_20260608", root, limestonePatch, limestone);
                shoal.transform.position = new Vector3(focus.x + x, -6.0f - (i % 2) * 0.40f, focus.z + z);
                shoal.transform.rotation = Quaternion.Euler(0f, 18f + i * 27f, 0f);
                shoal.transform.localScale = new Vector3(28f + (i % 3) * 7f, 3.0f + (i % 2) * 0.5f, 10f + (i % 4) * 2.7f);

                GameObject shoalFoam = CreateMeshObject("H8_CODEX_FOREGROUND_SHOAL_FOAM_" + i.ToString("00", CultureInfo.InvariantCulture) + "_20260608", root, foamRibbon, foam);
                shoalFoam.transform.position = new Vector3(focus.x + x + 4f, -0.18f, focus.z + z - 2f);
                shoalFoam.transform.rotation = Quaternion.Euler(90f, 28f + i * 24f, 0f);
                shoalFoam.transform.localScale = new Vector3(24f + (i % 3) * 5f, 0.70f + (i % 2) * 0.22f, 1f);
            }

            for (int i = 0; i < 19; i++)
            {
                float u = Halton(i + 23, 2) - 0.5f;
                float v = Halton(i + 31, 3) - 0.5f;
                float angle = -34f + i * 17f + Mathf.Sin(i * 1.13f) * 12f;
                float xSpread = i < 8 ? 95f : 205f;
                float zSpread = i < 8 ? 92f : 178f;
                float baseY = i < 8 ? -13.6f - (i % 3) * 1.1f : -10.2f - (i % 5) * 1.35f;
                GameObject ridge = CreateMeshObject("H8_CODEX_PHOTIC_LIMESTONE_RELIEF_RIDGE_" + i.ToString("00", CultureInfo.InvariantCulture) + "_20260608", root, limestoneRidge, limestone);
                ridge.transform.position = new Vector3(focus.x + u * xSpread, baseY, focus.z + v * zSpread);
                ridge.transform.rotation = Quaternion.Euler(0f, angle, 0f);
                ridge.transform.localScale = new Vector3(18f + (i % 5) * 5.5f, 4.2f + (i % 4) * 0.65f, 7.2f + (i % 4) * 2.1f);
            }

            for (int i = 0; i < 18; i++)
            {
                float u = Halton(i + 41, 2) - 0.5f;
                float v = Halton(i + 47, 3) - 0.5f;
                GameObject mound = CreateMeshObject("H8_CODEX_PHOTIC_LIMESTONE_RUBBLE_MOUND_" + i.ToString("00", CultureInfo.InvariantCulture) + "_20260608", root, limestonePatch, limestone);
                mound.transform.position = new Vector3(focus.x + u * 132f, -17.8f - (i % 4) * 0.75f, focus.z + v * 108f);
                mound.transform.rotation = Quaternion.Euler(0f, 11f + i * 31.7f, 0f);
                mound.transform.localScale = new Vector3(3.8f + (i % 5) * 1.45f, 4.4f + (i % 3) * 0.85f, 2.8f + (i % 4) * 1.05f);
            }

            for (int i = 0; i < 10; i++)
            {
                GameObject distantRidge = CreateMeshObject("H8_CODEX_DISTANT_SUBMERGED_LOW_RIDGE_" + i.ToString("00", CultureInfo.InvariantCulture) + "_20260608", root, limestoneRidge, limestone);
                distantRidge.transform.position = new Vector3(focus.x + (i - 4.5f) * 58f + Mathf.Sin(i * 1.3f) * 18f, -8.2f - (i % 4) * 1.35f, focus.z - 148f - i * 16f);
                distantRidge.transform.rotation = Quaternion.Euler(0f, -24f + i * 7.8f, 0f);
                distantRidge.transform.localScale = new Vector3(34f + (i % 4) * 8f, 3.8f + (i % 3) * 0.9f, 9f + (i % 3) * 2.8f);
            }

            for (int i = 0; i < 14; i++)
            {
                bool usePatch = i % 3 == 0;
                Mesh mesh = usePatch ? limestonePatch : limestoneRidge;
                GameObject horizonShelf = CreateMeshObject("H8_CODEX_BROKEN_HORIZON_PHOTIC_SHELF_" + i.ToString("00", CultureInfo.InvariantCulture) + "_20260608", root, mesh, shoalLimestone);
                float x = (i - 6.5f) * 38f + Mathf.Sin(i * 0.83f) * 16f;
                float z = -104f - Mathf.Sin(i * 1.17f) * 28f - (i % 5) * 12f;
                horizonShelf.transform.position = new Vector3(focus.x + x, -6.4f - (i % 4) * 0.62f, focus.z + z);
                horizonShelf.transform.rotation = Quaternion.Euler(0f, -18f + i * 11.6f, 0f);
                horizonShelf.transform.localScale = usePatch
                    ? new Vector3(18f + (i % 5) * 5.5f, 4.4f + (i % 3) * 0.7f, 7f + (i % 4) * 2.1f)
                    : new Vector3(38f + (i % 5) * 7f, 5.6f + (i % 3) * 0.8f, 8f + (i % 4) * 2.4f);
            }

            GameObject surface = CreateMeshObject("H8_CODEX_READABLE_OCEAN_SURFACE_20260608", root, waterSurfaceMesh, waterSurface);
            surface.transform.position = new Vector3(focus.x, 0.02f, focus.z);
            surface.transform.localScale = new Vector3(5600f, 1f, 5200f);

            for (int i = 0; i < 6; i++)
            {
                float u = Halton(i + 3, 2) - 0.5f;
                float v = Halton(i + 5, 3) - 0.5f;
                GameObject glint = CreateMeshObject("H8_CODEX_SURFACE_GLINT_RIPPLE_" + i.ToString("00", CultureInfo.InvariantCulture) + "_20260608", root, foamRibbon, foam);
                glint.transform.position = new Vector3(focus.x + u * 430f, 0.31f + (i % 5) * 0.01f, focus.z + v * 320f);
                glint.transform.rotation = Quaternion.Euler(0f, 18f + i * 17.3f, 0f);
                glint.transform.localScale = new Vector3(7f + (i % 4) * 2.2f, 0.12f + (i % 3) * 0.04f, 1f);
            }

            for (int i = 0; i < 12; i++)
            {
                float angle = i * 31f + Mathf.Sin(i * 0.91f) * 8f;
                float radius = 27f + Mathf.Sin(i * 1.41f) * 9f;
                Vector3 position = focus + new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * radius, 0.14f, Mathf.Cos(angle * Mathf.Deg2Rad) * radius);
                GameObject ribbon = CreateMeshObject("H8_CODEX_CONTACT_FOAM_LACE_" + i.ToString("00", CultureInfo.InvariantCulture) + "_20260608", root, foamRibbon, foam);
                ribbon.transform.position = position;
                ribbon.transform.rotation = Quaternion.Euler(90f, angle + 90f, 0f);
                ribbon.transform.localScale = new Vector3(9f + (i % 5) * 2.1f, 1.15f + (i % 3) * 0.35f, 1f);
            }

            for (int i = 0; i < 7; i++)
            {
                GameObject shear = CreateMeshObject("H8_CODEX_DISTANT_SURFACE_SHEAR_LACE_" + i.ToString("00", CultureInfo.InvariantCulture) + "_20260608", root, foamRibbon, foam);
                shear.transform.position = new Vector3(focus.x + (i - 3f) * 84f, 0.18f, focus.z - 254f - i * 17f);
                shear.transform.rotation = Quaternion.Euler(90f, 92f + Mathf.Sin(i * 1.4f) * 8f, 0f);
                shear.transform.localScale = new Vector3(56f + (i % 3) * 18f, 0.72f + (i % 2) * 0.24f, 1f);
            }

            for (int i = 0; i < 14; i++)
            {
                float x = (Halton(i + 13, 2) - 0.5f) * 150f;
                float z = (Halton(i + 17, 3) - 0.5f) * 110f;
                GameObject causticRibbon = CreateMeshObject("H8_CODEX_FLOOR_CAUSTIC_RIB_" + i.ToString("00", CultureInfo.InvariantCulture) + "_20260608", root, foamRibbon, caustic);
                causticRibbon.transform.position = new Vector3(focus.x + x, -10.1f - (i % 3) * 1.1f, focus.z + z);
                causticRibbon.transform.rotation = Quaternion.Euler(90f, 28f + i * 23f, 0f);
                causticRibbon.transform.localScale = new Vector3(14f + (i % 4) * 4f, 0.82f + (i % 3) * 0.22f, 1f);
            }

            for (int i = 0; i < 7; i++)
            {
                GameObject band = CreateMeshObject("H8_CODEX_WATER_COLUMN_DEPTH_BAND_" + i.ToString("00", CultureInfo.InvariantCulture) + "_20260608", root, curtainRibbon, curtain);
                float side = i % 2 == 0 ? -1f : 1f;
                band.transform.position = new Vector3(focus.x + side * (72f + i * 9f), -17f - i * 1.9f, focus.z - 122f - i * 26f);
                band.transform.rotation = Quaternion.Euler(0f, 168f + side * (8f + i * 1.8f), 0f);
                band.transform.localScale = new Vector3(22f + (i % 3) * 5f, 18f + (i % 4) * 4f, 1f);
            }

            for (int i = 0; i < 90; i++)
            {
                float u = Hash01(i * 17 + 3);
                float v = Hash01(i * 29 + 11);
                float w = Hash01(i * 43 + 19);
                GameObject mote = CreateMeshObject("H8_CODEX_SUSPENDED_PARTICULATE_" + i.ToString("000", CultureInfo.InvariantCulture) + "_20260608", root, unitQuad, particulate);
                mote.transform.position = new Vector3(focus.x + (u - 0.5f) * 135f, -1.2f - v * 30f, focus.z + (w - 0.5f) * 122f);
                mote.transform.rotation = Quaternion.Euler(0f, 180f + u * 60f, 0f);
                float size = 0.08f + Hash01(i * 61 + 7) * 0.32f;
                mote.transform.localScale = new Vector3(size, size, 1f);
            }
        }

        private static void BuildSkyAndAegir(
            Transform root,
            Camera sourceCamera,
            Vector3 cameraPosition,
            Vector3 cameraForward,
            Mesh unitQuad,
            Mesh skyDomeMesh,
            Material sky,
            Material aegir,
            Material moon)
        {
            Vector3 forward = cameraForward.sqrMagnitude > 0.001f ? cameraForward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.right;
            right.Normalize();

            Vector3 up = Vector3.Cross(forward, right).normalized;
            Vector3 skyCenter = cameraPosition + forward * 1800f + up * 220f;

            GameObject skyDome = CreateMeshObject("H8_CODEX_CYAN_NOIR_SKY_DOME_20260608", root, skyDomeMesh, sky);
            skyDome.transform.position = cameraPosition;
            skyDome.transform.localScale = new Vector3(5200f, 5200f, 5200f);

            GameObject aegirCard = CreateMeshObject("H8_CODEX_AEGIR_GAS_GIANT_REAL_DISC_20260608", root, unitQuad, aegir);
            aegirCard.transform.position = skyCenter - right * 520f + up * 245f - forward * 8f;
            FaceCamera(aegirCard.transform, sourceCamera, cameraPosition);
            aegirCard.transform.localScale = new Vector3(430f, 430f, 1f);

            GameObject moonA = CreateMeshObject("H8_CODEX_MOON_KHEPRI_WARM_DISC_20260608", root, unitQuad, moon);
            moonA.transform.position = skyCenter + right * 330f + up * 320f - forward * 6f;
            FaceCamera(moonA.transform, sourceCamera, cameraPosition);
            moonA.transform.localScale = new Vector3(52f, 52f, 1f);

            GameObject moonB = CreateMeshObject("H8_CODEX_MOON_THALOS_PALE_DISC_20260608", root, unitQuad, moon);
            moonB.transform.position = skyCenter + right * 520f + up * 160f - forward * 5f;
            FaceCamera(moonB.transform, sourceCamera, cameraPosition);
            moonB.transform.localScale = new Vector3(34f, 34f, 1f);
        }

        private static void TuneMainCamera(Camera sourceCamera, Vector3 cameraPosition, Vector3 cameraForward)
        {
            if (sourceCamera == null)
                return;

            sourceCamera.transform.position = cameraPosition;
            sourceCamera.transform.rotation = Quaternion.LookRotation(cameraForward, Vector3.up);
            sourceCamera.cullingMask = ~0;
            sourceCamera.useOcclusionCulling = false;
            sourceCamera.clearFlags = CameraClearFlags.SolidColor;
            sourceCamera.backgroundColor = new Color(0.035f, 0.13f, 0.17f, 1f);
            sourceCamera.nearClipPlane = 0.05f;
            sourceCamera.farClipPlane = 120000f;
            sourceCamera.fieldOfView = 58f;
            EditorUtility.SetDirty(sourceCamera);
        }

        private static void TuneLighting(Vector3 focus)
        {
            Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            bool hasKey = false;
            for (int i = 0; i < lights.Length; i++)
            {
                if (!IsSceneObject(lights[i].gameObject))
                    continue;

                if (lights[i].type == LightType.Directional)
                {
                    lights[i].color = new Color(0.55f, 0.86f, 0.95f, 1f);
                    lights[i].intensity = 1.15f;
                    lights[i].transform.rotation = Quaternion.Euler(41f, -26f, 0f);
                    EditorUtility.SetDirty(lights[i]);
                    hasKey = true;
                    break;
                }
            }

            if (hasKey)
                return;

            GameObject key = new GameObject("H8_CODEX_SURFACE_CYAN_SUN_KEY_20260608");
            key.transform.position = focus + new Vector3(-20f, 80f, 20f);
            key.transform.rotation = Quaternion.Euler(41f, -26f, 0f);
            Light light = key.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.55f, 0.86f, 0.95f, 1f);
            light.intensity = 1.15f;
        }

        private static List<string> CaptureProofs(Camera sourceCamera, Vector3 focus)
        {
            List<string> paths = new List<string>();
            Vector3 basePosition = sourceCamera != null ? sourceCamera.transform.position : focus + new Vector3(-96f, 16f, 118f);
            Vector3 forward = (focus + Vector3.up * 5.2f - basePosition).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.right;
            right.Normalize();

            Vector3 up = Vector3.Cross(forward, right).normalized;
            Vector3 skyCenter = basePosition + forward * 1800f + up * 220f;
            Vector3 aegirTarget = skyCenter - right * 520f + up * 245f - forward * 8f;

            paths.Add(CaptureProof("water_sky_main", basePosition, focus + Vector3.up * 6f, 1280, 720, sourceCamera));
            paths.Add(CaptureProof("water_surface_low", focus + new Vector3(-82f, 4.4f, 74f), focus + new Vector3(14f, 0.4f, -42f), 1280, 720, sourceCamera));
            paths.Add(CaptureProof("underwater_caustics", focus + new Vector3(-76f, -9.2f, 76f), focus + new Vector3(22f, -15f, -58f), 1280, 720, sourceCamera));
            paths.Add(CaptureProof("terrain_relief_oblique", focus + new Vector3(94f, -3.4f, 38f), focus + new Vector3(-42f, -15.0f, -62f), 1280, 720, sourceCamera));
            paths.Add(CaptureProof("aegir_sky", basePosition + up * 16f - right * 18f, aegirTarget, 1280, 720, sourceCamera));
            paths.Add(CaptureProof("legacy_origin_photic_surface", new Vector3(-82f, 22f, 142f), new Vector3(6f, 9f, 76f), 1280, 720, sourceCamera));
            paths.Add(CaptureProof("legacy_origin_photic_underwater", new Vector3(-42f, 6.8f, 118f), new Vector3(12f, 7.2f, 72f), 1280, 720, sourceCamera));

            return paths;
        }

        private static string CaptureProof(string name, Vector3 position, Vector3 target, int width, int height, Camera sourceCamera)
        {
            string absoluteRoot = AbsoluteProjectPath(ScreenshotRelativeRoot);
            Directory.CreateDirectory(absoluteRoot);
            string absolutePath = Path.Combine(absoluteRoot, name + "_20260608.png");

            GameObject cameraObject = new GameObject("H8_CODEX_TEMP_PROOF_CAMERA");
            Camera camera = cameraObject.AddComponent<Camera>();
            if (sourceCamera != null)
                camera.CopyFrom(sourceCamera);

            camera.enabled = false;
            camera.cameraType = CameraType.Game;
            camera.cullingMask = ~0;
            camera.useOcclusionCulling = false;
            camera.forceIntoRenderTexture = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.13f, 0.17f, 1f);
            camera.fieldOfView = 58f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 120000f;
            camera.transform.position = position;
            camera.transform.LookAt(target, Vector3.up);

            RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;

            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                GL.Clear(true, true, camera.backgroundColor);
                camera.Render();
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }

            return absolutePath;
        }

        private static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return gameObject;
        }

        private static void FaceCamera(Transform transform, Camera sourceCamera, Vector3 fallbackCameraPosition)
        {
            Vector3 target = sourceCamera != null ? sourceCamera.transform.position : fallbackCameraPosition;
            transform.LookAt(target, Vector3.up);
        }

        private static Camera ResolveSourceCamera()
        {
            Camera main = Camera.main;
            if (main != null)
                return main;

            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (IsSceneObject(cameras[i].gameObject))
                    return cameras[i];
            }

            return null;
        }

        private static Transform FindSceneTransform(Scene scene, string name)
        {
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform == null || transform.gameObject == null)
                    continue;

                if (transform.gameObject.scene != scene || EditorUtility.IsPersistent(transform.gameObject))
                    continue;

                if (string.Equals(transform.name, name, StringComparison.Ordinal))
                    return transform;
            }

            return null;
        }

        private static bool IsSceneObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid() && !EditorUtility.IsPersistent(gameObject);
        }

        private static void BindEditorProofLightingFallback(Vector3 focus, out GraphicsBuffer probeBuffer, out GraphicsBuffer shBuffer)
        {
            probeBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, 128);
            shBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 27, sizeof(float));

            Shader.SetGlobalBuffer(ProofProbeBufferId, probeBuffer);
            Shader.SetGlobalBuffer(ProofShBufferId, shBuffer);
            Shader.SetGlobalVector(ProofGIParamsId, Vector4.zero);
            Shader.SetGlobalVector(ProofGIOriginId, new Vector4(focus.x, focus.y, focus.z, 0f));
            Shader.SetGlobalVector(ProofGIRootAupId, Vector4.zero);
            Shader.SetGlobalVector(ProofGIStateId, new Vector4(0f, 0f, 1f, 0f));
            Shader.SetGlobalVector(ProofEnvAmbientId, new Vector4(0.09f, 0.22f, 0.25f, 0.35f));
            Shader.SetGlobalVector(ProofEnvFogId, new Vector4(0.02f, 0.12f, 0.16f, 0.08f));
            Shader.SetGlobalVector(ProofEnvDirectionalId, new Vector4(0.25f, 0.42f, 0.45f, 0.25f));
            Shader.SetGlobalVector(ProofEnvScalarId, new Vector4(0.35f, 0.25f, 0f, 0.35f));
            Shader.SetGlobalFloat(ProofEnvDebugBlocksId, 0f);
        }

        private static void ReleaseProofBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static float Halton(int index, int radix)
        {
            float result = 0f;
            float fraction = 1f / radix;
            int value = Mathf.Max(index, 1);
            while (value > 0)
            {
                result += (value % radix) * fraction;
                value /= radix;
                fraction /= radix;
            }

            return result;
        }

        private static Mesh UpsertMesh(string assetPath, Mesh source)
        {
            EnsureAssetDirectory(assetPath);
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(source, existing);
                UnityEngine.Object.DestroyImmediate(source);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(source, assetPath);
            return source;
        }

        private static Material UpsertMaterial(string assetPath, Color color, bool transparent, Texture texture, float emissionBoost)
        {
            EnsureAssetDirectory(assetPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
                shader = Shader.Find("Standard");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, assetPath);
            }
            else if (shader != null)
            {
                material.shader = shader;
            }

            SetMaterialColor(material, color);
            SetMaterialTexture(material, texture);
            if (material.HasProperty("_Cull"))
                material.SetFloat("_Cull", 0f);
            ConfigureTransparency(material, transparent);
            if (emissionBoost > 0f)
                SetMaterialEmission(material, color, emissionBoost);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material UpsertWaterSurfaceMaterial(string assetPath, Color color, Texture texture, Texture normalMap)
        {
            EnsureAssetDirectory(assetPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                return UpsertMaterial(assetPath, color, true, texture, 0.08f);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, assetPath);
            }
            else
            {
                material.shader = shader;
            }

            SetMaterialColor(material, color);
            SetMaterialTexture(material, texture);
            if (normalMap != null)
            {
                if (material.HasProperty("_BumpMap"))
                {
                    material.SetTexture("_BumpMap", normalMap);
                    material.SetTextureScale("_BumpMap", new Vector2(18f, 16f));
                }
                if (material.HasProperty("_BumpScale"))
                    material.SetFloat("_BumpScale", 0.16f);
                material.EnableKeyword("_NORMALMAP");
            }

            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.92f);
            if (material.HasProperty("_SpecColor"))
                material.SetColor("_SpecColor", new Color(0.60f, 0.88f, 0.92f, 1f));
            if (material.HasProperty("_Cull"))
                material.SetFloat("_Cull", 0f);

            ConfigureTransparency(material, true);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
        }

        private static void SetMaterialTexture(Material material, Texture texture)
        {
            if (texture == null)
                return;

            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
        }

        private static void SetMaterialTextureScale(Material material, Vector2 scale)
        {
            if (material.HasProperty("_BaseMap"))
                material.SetTextureScale("_BaseMap", scale);
            if (material.HasProperty("_MainTex"))
                material.SetTextureScale("_MainTex", scale);
        }

        private static void ConfigureTransparency(Material material, bool transparent)
        {
            if (!transparent)
            {
                if (material.HasProperty("_Surface"))
                    material.SetFloat("_Surface", 0f);
                if (material.HasProperty("_Blend"))
                    material.SetFloat("_Blend", 0f);
                if (material.HasProperty("_ZWrite"))
                    material.SetFloat("_ZWrite", 1f);

                material.SetInt("_SrcBlend", (int)BlendMode.One);
                material.SetInt("_DstBlend", (int)BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.renderQueue = (int)RenderQueue.Geometry;
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                return;
            }

            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_Cull"))
                material.SetFloat("_Cull", 0f);
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);

            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        private static void SetMaterialEmission(Material material, Color color, float boost)
        {
            Color emission = new Color(color.r * boost, color.g * boost, color.b * boost, 1f);
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", emission);
            material.EnableKeyword("_EMISSION");
        }

        private static Texture2D UpsertPngTexture(string assetPath, Texture2D texture)
        {
            return UpsertPngTexture(assetPath, texture, TextureWrapMode.Clamp);
        }

        private static Texture2D UpsertPngTexture(string assetPath, Texture2D texture, TextureWrapMode wrapMode)
        {
            string absolutePath = AbsoluteProjectPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = wrapMode;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static Texture2D LoadPngTextureOrFallback(string absolutePath, Texture2D fallback)
        {
            if (!File.Exists(absolutePath))
                return fallback;

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            byte[] bytes = File.ReadAllBytes(absolutePath);
            if (ImageConversion.LoadImage(texture, bytes, false))
            {
                UnityEngine.Object.DestroyImmediate(fallback);
                return texture;
            }

            UnityEngine.Object.DestroyImmediate(texture);
            return fallback;
        }

        private static Mesh BuildGridMesh(int resolution, float spacing, string meshName)
        {
            int side = resolution + 1;
            Vector3[] vertices = new Vector3[side * side];
            Vector2[] uvs = new Vector2[vertices.Length];
            Vector3[] normals = new Vector3[vertices.Length];
            int[] triangles = new int[resolution * resolution * 6];
            float half = resolution * spacing * 0.5f;

            for (int z = 0; z < side; z++)
            {
                for (int x = 0; x < side; x++)
                {
                    int index = x + z * side;
                    float localX = x * spacing - half;
                    float localZ = z * spacing - half;
                    float wave =
                        Mathf.Sin(localX * 0.145f + localZ * 0.037f) * 0.34f +
                        Mathf.Sin(localX * 0.041f - localZ * 0.173f + 1.7f) * 0.21f +
                        Mathf.Sin((localX + localZ) * 0.088f + 3.1f) * 0.12f;
                    vertices[index] = new Vector3(localX, wave, localZ);
                    uvs[index] = new Vector2((float)x / resolution, (float)z / resolution);
                    normals[index] = Vector3.up;
                }
            }

            int triangle = 0;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int index = x + z * side;
                    triangles[triangle++] = index;
                    triangles[triangle++] = index + side;
                    triangles[triangle++] = index + 1;
                    triangles[triangle++] = index + 1;
                    triangles[triangle++] = index + side;
                    triangles[triangle++] = index + side + 1;
                }
            }

            Mesh mesh = new Mesh { name = meshName, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildIrregularOceanSurfaceMesh(int rings, int segments, string meshName)
        {
            int vertexCount = 1 + rings * segments;
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[segments * 3 + (rings - 1) * segments * 6];

            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int ring = 1; ring <= rings; ring++)
            {
                float normalizedRing = (float)ring / rings;
                float edgeWeight = normalizedRing * normalizedRing;
                for (int segment = 0; segment < segments; segment++)
                {
                    float t = (float)segment / segments;
                    float angle = t * Mathf.PI * 2f;
                    float brokenRadius = 1f +
                        Mathf.Sin(angle * 3.0f + 0.45f) * 0.035f * edgeWeight +
                        Mathf.Sin(angle * 7.0f + 1.70f) * 0.024f * edgeWeight +
                        Mathf.Sin(angle * 13.0f + 0.20f) * 0.012f * edgeWeight;
                    float radius = normalizedRing * brokenRadius;
                    float ellipse = 0.88f + Mathf.Sin(angle * 2.0f + 0.8f) * 0.035f;
                    float localX = Mathf.Cos(angle) * radius;
                    float localZ = Mathf.Sin(angle) * radius * ellipse;
                    float wave =
                        Mathf.Sin(localX * 9.4f + localZ * 3.1f) * 0.22f +
                        Mathf.Sin(localX * 2.7f - localZ * 11.6f + 1.7f) * 0.14f +
                        Mathf.Sin((localX + localZ) * 7.2f + 3.1f) * 0.08f;
                    int index = 1 + (ring - 1) * segments + segment;
                    vertices[index] = new Vector3(localX, wave, localZ);
                    uvs[index] = new Vector2(localX * 0.5f + 0.5f, localZ * 0.5f + 0.5f);
                }
            }

            int triangle = 0;
            for (int segment = 0; segment < segments; segment++)
            {
                int next = segment == segments - 1 ? 0 : segment + 1;
                triangles[triangle++] = 0;
                triangles[triangle++] = 1 + segment;
                triangles[triangle++] = 1 + next;
            }

            for (int ring = 2; ring <= rings; ring++)
            {
                int innerStart = 1 + (ring - 2) * segments;
                int outerStart = 1 + (ring - 1) * segments;
                for (int segment = 0; segment < segments; segment++)
                {
                    int next = segment == segments - 1 ? 0 : segment + 1;
                    int inner = innerStart + segment;
                    int innerNext = innerStart + next;
                    int outer = outerStart + segment;
                    int outerNext = outerStart + next;

                    triangles[triangle++] = inner;
                    triangles[triangle++] = outer;
                    triangles[triangle++] = innerNext;
                    triangles[triangle++] = innerNext;
                    triangles[triangle++] = outer;
                    triangles[triangle++] = outerNext;
                }
            }

            Mesh mesh = new Mesh { name = meshName, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildSeabedGridMesh(int resolution, float spacing, string meshName)
        {
            int rings = Mathf.Max(24, resolution);
            int segments = Mathf.Max(96, resolution + 48);
            int vertexCount = 1 + rings * segments;
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 3 + (rings - 1) * segments * 6];
            float half = resolution * spacing * 0.5f;

            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int ring = 1; ring <= rings; ring++)
            {
                float normalizedRing = (float)ring / rings;
                float edgeWeight = Mathf.SmoothStep(0.18f, 1f, normalizedRing);
                for (int segment = 0; segment < segments; segment++)
                {
                    float t = (float)segment / segments;
                    float angle = t * Mathf.PI * 2f;
                    float brokenRadius = 1f +
                        Mathf.Sin(angle * 2.0f + 0.34f) * 0.10f * edgeWeight +
                        Mathf.Sin(angle * 5.0f + 1.80f) * 0.065f * edgeWeight +
                        Mathf.Sin(angle * 11.0f + 0.20f) * 0.032f * edgeWeight;
                    float radial = normalizedRing * half * brokenRadius;
                    float localX = Mathf.Cos(angle) * radial * 1.18f;
                    float localZ = Mathf.Sin(angle) * radial * 0.92f;
                    float broadShelf =
                        Mathf.Sin(localX * 0.032f + localZ * 0.020f) * 3.1f +
                        Mathf.Sin(localX * 0.066f - localZ * 0.047f + 2.1f) * 1.7f;
                    float routeSaddle = -Mathf.Exp(-(localX * localX) / 2600f) * 2.7f;
                    float smallRelief = (Mathf.PerlinNoise(localX * 0.060f + 17.3f, localZ * 0.058f + 31.1f) - 0.5f) * 2.8f;
                    float ledgeNoise = Mathf.PerlinNoise(localX * 0.028f + 3.8f, localZ * 0.042f + 9.4f);
                    float limestoneTerraces =
                        Mathf.Sin(localZ * 0.18f + Mathf.Sin(localX * 0.050f) * 2.35f) *
                        Mathf.SmoothStep(0.18f, 0.82f, ledgeNoise) * 2.4f;
                    float rubbleMounds =
                        Mathf.Pow(Mathf.SmoothStep(0.55f, 0.92f, Mathf.PerlinNoise(localX * 0.092f + 41.2f, localZ * 0.084f + 7.5f)), 1.55f) * 3.1f;
                    float edgeDrop = Mathf.SmoothStep(0.66f, 1f, normalizedRing) * -7.0f;
                    float contourLift = Mathf.Exp(-((localZ + 18f) * (localZ + 18f)) / 1600f) * 2.8f;
                    int index = 1 + (ring - 1) * segments + segment;
                    vertices[index] = new Vector3(localX, broadShelf + routeSaddle + smallRelief + limestoneTerraces + rubbleMounds + edgeDrop + contourLift, localZ);
                    uvs[index] = new Vector2(localX / (half * 2.6f) + 0.5f, localZ / (half * 2.6f) + 0.5f);
                }
            }

            int triangle = 0;
            for (int segment = 0; segment < segments; segment++)
            {
                int next = segment == segments - 1 ? 0 : segment + 1;
                triangles[triangle++] = 0;
                triangles[triangle++] = 1 + segment;
                triangles[triangle++] = 1 + next;
            }

            for (int ring = 2; ring <= rings; ring++)
            {
                int innerStart = 1 + (ring - 2) * segments;
                int outerStart = 1 + (ring - 1) * segments;
                for (int segment = 0; segment < segments; segment++)
                {
                    int next = segment == segments - 1 ? 0 : segment + 1;
                    int inner = innerStart + segment;
                    int innerNext = innerStart + next;
                    int outer = outerStart + segment;
                    int outerNext = outerStart + next;
                    triangles[triangle++] = inner;
                    triangles[triangle++] = outer;
                    triangles[triangle++] = innerNext;
                    triangles[triangle++] = innerNext;
                    triangles[triangle++] = outer;
                    triangles[triangle++] = outerNext;
                }
            }

            Mesh mesh = new Mesh { name = meshName, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildHorizonShelfBandMesh(string meshName)
        {
            const int segments = 88;
            const int rows = 4;
            Vector3[] vertices = new Vector3[(segments + 1) * rows];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[segments * (rows - 1) * 6];

            for (int segment = 0; segment <= segments; segment++)
            {
                float t = (float)segment / segments;
                float x = (t - 0.5f) * 2f;
                float top =
                    0.10f +
                    Mathf.Sin(t * Mathf.PI * 5.5f + 0.3f) * 0.105f +
                    Mathf.Sin(t * Mathf.PI * 15.0f + 1.9f) * 0.045f;
                float bottom =
                    -0.92f +
                    Mathf.Sin(t * Mathf.PI * 4.0f + 1.1f) * 0.05f;

                for (int row = 0; row < rows; row++)
                {
                    float v = (float)row / (rows - 1);
                    int index = segment * rows + row;
                    float y = Mathf.Lerp(bottom, top, v);
                    float z = Mathf.Sin(t * Mathf.PI * 7.0f + v * 1.6f) * 0.018f;
                    vertices[index] = new Vector3(x, y, z);
                    uvs[index] = new Vector2(t, v);
                }
            }

            int triangle = 0;
            for (int segment = 0; segment < segments; segment++)
            {
                for (int row = 0; row < rows - 1; row++)
                {
                    int index = segment * rows + row;
                    triangles[triangle++] = index;
                    triangles[triangle++] = index + rows;
                    triangles[triangle++] = index + 1;
                    triangles[triangle++] = index + 1;
                    triangles[triangle++] = index + rows;
                    triangles[triangle++] = index + rows + 1;
                }
            }

            Mesh mesh = new Mesh { name = meshName, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildLimestonePatchMesh(string meshName)
        {
            const int rings = 10;
            const int segments = 72;
            Vector3[] vertices = new Vector3[1 + rings * segments];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 3 + (rings - 1) * segments * 6];

            vertices[0] = new Vector3(0f, 0.18f, 0f);
            uvs[0] = new Vector2(0.5f, 0.5f);
            for (int ring = 1; ring <= rings; ring++)
            {
                float normalizedRing = (float)ring / rings;
                float edgeWeight = Mathf.SmoothStep(0.2f, 1f, normalizedRing);
                for (int segment = 0; segment < segments; segment++)
                {
                    float t = (float)segment / segments;
                    float angle = t * Mathf.PI * 2f;
                    float radius =
                        normalizedRing *
                        (0.82f +
                        Mathf.Sin(t * Mathf.PI * 10f) * 0.11f * edgeWeight +
                        Mathf.Sin(t * Mathf.PI * 22f + 0.7f) * 0.045f * edgeWeight);
                    float x = Mathf.Cos(angle) * radius;
                    float z = Mathf.Sin(angle) * radius * (0.62f + Mathf.Sin(angle * 2f) * 0.08f * edgeWeight);
                    float crown = Mathf.Pow(1f - normalizedRing, 1.6f) * 0.18f;
                    float y =
                        crown +
                        Mathf.Sin(angle * 3f + 0.4f) * 0.060f * edgeWeight +
                        Mathf.Sin(angle * 7f + 1.3f) * 0.030f * edgeWeight -
                        Mathf.SmoothStep(0.78f, 1f, normalizedRing) * 0.055f;
                    int index = 1 + (ring - 1) * segments + segment;
                    vertices[index] = new Vector3(x, y, z);
                    uvs[index] = new Vector2(x * 0.5f + 0.5f, z * 0.5f + 0.5f);
                }
            }

            int triangle = 0;
            for (int segment = 0; segment < segments; segment++)
            {
                int next = segment == segments - 1 ? 0 : segment + 1;
                triangles[triangle++] = 0;
                triangles[triangle++] = 1 + segment;
                triangles[triangle++] = 1 + next;
            }

            for (int ring = 2; ring <= rings; ring++)
            {
                int innerStart = 1 + (ring - 2) * segments;
                int outerStart = 1 + (ring - 1) * segments;
                for (int segment = 0; segment < segments; segment++)
                {
                    int next = segment == segments - 1 ? 0 : segment + 1;
                    int inner = innerStart + segment;
                    int innerNext = innerStart + next;
                    int outer = outerStart + segment;
                    int outerNext = outerStart + next;
                    triangles[triangle++] = inner;
                    triangles[triangle++] = outer;
                    triangles[triangle++] = innerNext;
                    triangles[triangle++] = innerNext;
                    triangles[triangle++] = outer;
                    triangles[triangle++] = outerNext;
                }
            }

            Mesh mesh = new Mesh { name = meshName };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildLimestoneRidgeMesh(string meshName)
        {
            const int lengthSegments = 24;
            const int widthSegments = 8;
            int vertexCount = (lengthSegments + 1) * (widthSegments + 1);
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[lengthSegments * widthSegments * 6];

            for (int z = 0; z <= lengthSegments; z++)
            {
                float v = (float)z / lengthSegments;
                float localZ = (v - 0.5f) * 2f;
                float endFalloff = Mathf.SmoothStep(0f, 0.18f, v) * Mathf.SmoothStep(0f, 0.18f, 1f - v);
                float centerline = Mathf.Sin(v * Mathf.PI * 3.2f + 0.4f) * 0.10f + Mathf.Sin(v * Mathf.PI * 8.4f) * 0.035f;
                float widthEnvelope = (0.50f + Mathf.Sin(v * Mathf.PI * 5.6f + 1.2f) * 0.08f + Mathf.Sin(v * Mathf.PI * 13.0f) * 0.035f) * endFalloff;
                for (int x = 0; x <= widthSegments; x++)
                {
                    float u = (float)x / widthSegments;
                    float localX = (u - 0.5f) * 2f;
                    float widthFalloff = 1f - Mathf.Clamp01(Mathf.Abs(localX));
                    float crest = Mathf.Pow(widthFalloff, 1.85f) * endFalloff;
                    float brokenEdge = Mathf.Sin(v * Mathf.PI * 7f + localX * 2.1f) * 0.035f +
                        Mathf.Sin(v * Mathf.PI * 17f + localX * 5.4f) * 0.018f;
                    int index = x + z * (widthSegments + 1);
                    vertices[index] = new Vector3(
                        centerline + localX * widthEnvelope + brokenEdge * endFalloff,
                        crest * 0.42f + Mathf.Sin(v * Mathf.PI * 5f) * 0.035f,
                        localZ);
                    uvs[index] = new Vector2(u, v);
                }
            }

            int triangle = 0;
            for (int z = 0; z < lengthSegments; z++)
            {
                for (int x = 0; x < widthSegments; x++)
                {
                    int index = x + z * (widthSegments + 1);
                    triangles[triangle++] = index;
                    triangles[triangle++] = index + widthSegments + 1;
                    triangles[triangle++] = index + 1;
                    triangles[triangle++] = index + 1;
                    triangles[triangle++] = index + widthSegments + 1;
                    triangles[triangle++] = index + widthSegments + 2;
                }
            }

            Mesh mesh = new Mesh { name = meshName };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildShelfBreakMesh(string meshName)
        {
            const int lengthSegments = 68;
            const int widthSegments = 10;
            int vertexCount = (lengthSegments + 1) * (widthSegments + 1);
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[lengthSegments * widthSegments * 6];

            for (int z = 0; z <= lengthSegments; z++)
            {
                float v = (float)z / lengthSegments;
                float localZ = (v - 0.5f) * 2f;
                float endFalloff = Mathf.SmoothStep(0f, 0.12f, v) * Mathf.SmoothStep(0f, 0.12f, 1f - v);
                float centerBend = Mathf.Sin(v * Mathf.PI * 2.2f + 0.5f) * 0.10f + Mathf.Sin(v * Mathf.PI * 6.0f) * 0.035f;
                float widthEnvelope = (0.72f + Mathf.Sin(v * Mathf.PI * 5.0f + 1.3f) * 0.11f + Mathf.Sin(v * Mathf.PI * 13.0f) * 0.035f) * endFalloff;

                for (int x = 0; x <= widthSegments; x++)
                {
                    float u = (float)x / widthSegments;
                    float cross = (u - 0.5f) * 2f;
                    float lowSide = Mathf.SmoothStep(0.08f, 0.98f, u);
                    float topStep = Mathf.SmoothStep(0f, 0.18f, u);
                    float ledgeRelief =
                        Mathf.Sin(v * Mathf.PI * 8.0f + u * 2.4f) * 0.34f +
                        Mathf.Sin(v * Mathf.PI * 19.0f + u * 5.1f) * 0.13f;
                    float brokenEdge = Mathf.Sin(v * Mathf.PI * 17.0f + cross * 2.8f) * 0.035f * endFalloff;
                    float y = Mathf.Lerp(-1.15f, -14.8f, lowSide) + ledgeRelief * (0.35f + topStep * 0.65f);
                    int index = x + z * (widthSegments + 1);
                    vertices[index] = new Vector3(centerBend + cross * widthEnvelope + brokenEdge, y, localZ);
                    uvs[index] = new Vector2(u, v);
                }
            }

            int triangle = 0;
            for (int z = 0; z < lengthSegments; z++)
            {
                for (int x = 0; x < widthSegments; x++)
                {
                    int index = x + z * (widthSegments + 1);
                    triangles[triangle++] = index;
                    triangles[triangle++] = index + widthSegments + 1;
                    triangles[triangle++] = index + 1;
                    triangles[triangle++] = index + 1;
                    triangles[triangle++] = index + widthSegments + 1;
                    triangles[triangle++] = index + widthSegments + 2;
                }
            }

            Mesh mesh = new Mesh { name = meshName, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildUnitQuadMesh(string meshName)
        {
            Mesh mesh = new Mesh { name = meshName };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildFoamRibbonMesh(string meshName)
        {
            const int segments = 28;
            Vector3[] vertices = new Vector3[(segments + 1) * 2];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float x = t - 0.5f;
                float top = 0.12f + Mathf.Sin(t * 20.7f) * 0.025f + Mathf.Sin(t * 51.1f) * 0.012f;
                float bottom = -0.12f + Mathf.Sin(t * 18.3f + 1.7f) * 0.022f;
                vertices[i * 2] = new Vector3(x, bottom, 0f);
                vertices[i * 2 + 1] = new Vector3(x, top, 0f);
                uvs[i * 2] = new Vector2(t, 0f);
                uvs[i * 2 + 1] = new Vector2(t, 1f);
            }

            int triangle = 0;
            for (int i = 0; i < segments; i++)
            {
                int index = i * 2;
                triangles[triangle++] = index;
                triangles[triangle++] = index + 1;
                triangles[triangle++] = index + 2;
                triangles[triangle++] = index + 1;
                triangles[triangle++] = index + 3;
                triangles[triangle++] = index + 2;
            }

            Mesh mesh = new Mesh { name = meshName };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildCurtainMesh(string meshName)
        {
            const int segments = 36;
            Vector3[] vertices = new Vector3[(segments + 1) * 2];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float x = t - 0.5f;
                float top = 0.48f + Mathf.Sin(t * Mathf.PI * 7f + 0.2f) * 0.035f + Mathf.Sin(t * Mathf.PI * 19f) * 0.018f;
                float bottom = -0.50f + Mathf.Sin(t * Mathf.PI * 5f + 1.1f) * 0.045f;
                float zOffset = Mathf.Sin(t * Mathf.PI * 6f + 0.6f) * 0.018f;
                vertices[i * 2] = new Vector3(x, bottom, zOffset);
                vertices[i * 2 + 1] = new Vector3(x, top, -zOffset);
                uvs[i * 2] = new Vector2(t, 0f);
                uvs[i * 2 + 1] = new Vector2(t, 1f);
            }

            int triangle = 0;
            for (int i = 0; i < segments; i++)
            {
                int index = i * 2;
                triangles[triangle++] = index;
                triangles[triangle++] = index + 2;
                triangles[triangle++] = index + 1;
                triangles[triangle++] = index + 1;
                triangles[triangle++] = index + 2;
                triangles[triangle++] = index + 3;
            }

            Mesh mesh = new Mesh { name = meshName };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildSkyDomeMesh(string meshName)
        {
            const int latitudeSegments = 24;
            const int longitudeSegments = 48;
            Vector3[] vertices = new Vector3[(latitudeSegments + 1) * (longitudeSegments + 1)];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[latitudeSegments * longitudeSegments * 6];

            for (int lat = 0; lat <= latitudeSegments; lat++)
            {
                float v = (float)lat / latitudeSegments;
                float phi = v * Mathf.PI;
                float sinPhi = Mathf.Sin(phi);
                float cosPhi = Mathf.Cos(phi);
                for (int lon = 0; lon <= longitudeSegments; lon++)
                {
                    float u = (float)lon / longitudeSegments;
                    float theta = u * Mathf.PI * 2f;
                    int index = lat * (longitudeSegments + 1) + lon;
                    vertices[index] = new Vector3(
                        Mathf.Cos(theta) * sinPhi,
                        cosPhi,
                        Mathf.Sin(theta) * sinPhi);
                    uvs[index] = new Vector2(u, 1f - v);
                }
            }

            int triangle = 0;
            for (int lat = 0; lat < latitudeSegments; lat++)
            {
                for (int lon = 0; lon < longitudeSegments; lon++)
                {
                    int current = lat * (longitudeSegments + 1) + lon;
                    int next = current + longitudeSegments + 1;
                    triangles[triangle++] = current;
                    triangles[triangle++] = current + 1;
                    triangles[triangle++] = next;
                    triangles[triangle++] = current + 1;
                    triangles[triangle++] = next + 1;
                    triangles[triangle++] = next;
                }
            }

            Mesh mesh = new Mesh { name = meshName, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Texture2D BuildAegirTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.46f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 delta = new Vector2(x, y) - center;
                    float distance = delta.magnitude / radius;
                    int index = x + y * size;
                    if (distance > 1f)
                    {
                        pixels[index] = Color.clear;
                        continue;
                    }

                    float nx = delta.x / radius;
                    float lat = delta.y / radius;
                    float sphereZ = Mathf.Sqrt(Mathf.Clamp01(1f - distance * distance));
                    float turbulenceA = Mathf.PerlinNoise(nx * 2.2f + 4.8f, lat * 10.5f + 19.2f);
                    float turbulenceB = Mathf.PerlinNoise(nx * 7.5f + 21.4f, lat * 24.0f + 3.7f);
                    float warpedLat =
                        lat +
                        Mathf.Sin(nx * 5.3f + turbulenceA * 2.8f) * 0.026f +
                        (turbulenceB - 0.5f) * 0.045f;
                    float broadBand = Mathf.Sin((warpedLat + 0.05f) * 24f) * 0.5f + 0.5f;
                    float fineBand = Mathf.Sin((warpedLat * 74f) + Mathf.Sin(nx * 8.5f) * 1.7f + turbulenceB * 2.0f) * 0.5f + 0.5f;
                    float jetDark = Mathf.SmoothStep(0.62f, 0.96f, Mathf.Abs(Mathf.Sin(warpedLat * 46f + turbulenceA * 1.4f)));
                    float limb = Mathf.Pow(1f - distance, 0.28f);
                    float light = Mathf.Clamp01(nx * -0.26f + lat * 0.08f + sphereZ * 0.92f);
                    float terminator = Mathf.SmoothStep(0.05f, 0.85f, light);
                    float stormA = Mathf.Exp(-((nx + 0.28f) * (nx + 0.28f) / 0.042f + (lat - 0.04f) * (lat - 0.04f) / 0.010f));
                    float stormB = Mathf.Exp(-((nx - 0.18f) * (nx - 0.18f) / 0.026f + (lat + 0.22f) * (lat + 0.22f) / 0.007f));

                    Color cold = new Color(0.22f, 0.52f, 0.63f, 1f);
                    Color warm = new Color(0.78f, 0.66f, 0.46f, 1f);
                    Color cream = new Color(0.86f, 0.82f, 0.65f, 1f);
                    Color dark = new Color(0.08f, 0.22f, 0.30f, 1f);
                    Color color = Color.Lerp(cold, warm, broadBand * 0.48f + fineBand * 0.12f);
                    color = Color.Lerp(color, cream, Mathf.SmoothStep(0.78f, 0.98f, fineBand) * 0.22f);
                    color = Color.Lerp(color, dark, jetDark * 0.18f + Mathf.Abs(lat) * 0.20f);
                    color = Color.Lerp(color, new Color(0.95f, 0.78f, 0.48f, 1f), stormA * 0.55f);
                    color = Color.Lerp(color, new Color(0.40f, 0.66f, 0.70f, 1f), stormB * 0.35f);
                    color.a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - distance) * 14f));
                    float shade = 0.28f + terminator * 0.74f;
                    color.r *= shade * (0.62f + limb * 0.56f);
                    color.g *= shade * (0.62f + limb * 0.58f);
                    color.b *= shade * (0.68f + limb * 0.62f);
                    pixels[index] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D BuildMoonTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.44f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 delta = new Vector2(x, y) - center;
                    float distance = delta.magnitude / radius;
                    int index = x + y * size;
                    if (distance > 1f)
                    {
                        pixels[index] = Color.clear;
                        continue;
                    }

                    float crater = Mathf.Sin(delta.x * 0.055f) * Mathf.Sin(delta.y * 0.071f) * 0.5f + 0.5f;
                    float limb = Mathf.Pow(1f - distance, 0.45f);
                    Color color = Color.Lerp(new Color(0.48f, 0.64f, 0.63f, 1f), new Color(0.86f, 0.82f, 0.68f, 1f), crater * 0.35f + limb * 0.45f);
                    color.a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - distance) * 12f));
                    pixels[index] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D BuildSkyGradientTexture(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (float)y / (height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / (width - 1);
                    float cloud = Mathf.Sin(u * 18f + Mathf.Sin(v * 8f) * 1.5f) * Mathf.Sin(v * 11f) * 0.5f + 0.5f;
                    float horizonHaze = Mathf.SmoothStep(0.05f, 0.30f, v) * (1f - Mathf.SmoothStep(0.34f, 0.62f, v));
                    float highCloud = Mathf.PerlinNoise(u * 5.5f + 13.2f, v * 8.0f + 4.4f);
                    float thinBand = Mathf.SmoothStep(0.68f, 0.96f, cloud * 0.72f + highCloud * 0.28f);
                    float starHash = Hash01(x * 1973 + y * 9277 + 61);
                    float star = v > 0.54f && starHash > 0.9975f ? Mathf.SmoothStep(0.9975f, 1f, starHash) : 0f;
                    Color low = new Color(0.025f, 0.12f, 0.16f, 0.92f);
                    Color high = new Color(0.12f, 0.34f, 0.42f, 0.90f);
                    Color color = Color.Lerp(low, high, v);
                    color = Color.Lerp(color, new Color(0.45f, 0.76f, 0.82f, 0.88f), Mathf.Clamp01((cloud - 0.64f) * 1.8f) * 0.12f);
                    color = Color.Lerp(color, new Color(0.34f, 0.58f, 0.62f, 0.88f), horizonHaze * thinBand * 0.18f);
                    color = Color.Lerp(color, new Color(0.88f, 0.95f, 0.90f, 0.95f), star * 0.50f);
                    color.a = 0.92f;
                    pixels[x + y * width] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D BuildWaterFlowTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = (float)y / (size - 1);
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / (size - 1);
                    float noiseA = Mathf.PerlinNoise(u * 7.5f + 11.2f, v * 7.5f + 4.7f);
                    float noiseB = Mathf.PerlinNoise(u * 18.0f + 2.5f, v * 18.0f + 19.1f);
                    float noiseC = Mathf.PerlinNoise(u * 39.0f + 5.3f, v * 33.0f + 27.7f);
                    float flow =
                        Mathf.Sin((u * 11.0f + v * 4.5f + noiseA * 3.4f) * Mathf.PI) * 0.5f + 0.5f;
                    float cross =
                        Mathf.Sin((u * -5.5f + v * 9.0f + noiseB * 2.2f) * Mathf.PI) * 0.5f + 0.5f;
                    float diagonal =
                        Mathf.Sin(((u + v) * 7.0f + noiseC * 2.8f) * Mathf.PI) * 0.5f + 0.5f;
                    float lace = Mathf.SmoothStep(0.84f, 0.985f, flow) * 0.075f +
                        Mathf.SmoothStep(0.86f, 0.992f, cross) * 0.055f +
                        Mathf.SmoothStep(0.89f, 0.997f, diagonal) * 0.040f;
                    Color deep = new Color(0.015f, 0.22f, 0.30f, 0.78f);
                    Color shallow = new Color(0.12f, 0.58f, 0.66f, 0.72f);
                    Color color = Color.Lerp(deep, shallow, 0.34f + noiseA * 0.24f + noiseC * 0.10f);
                    color = Color.Lerp(color, new Color(0.26f, 0.66f, 0.70f, 0.76f), lace * 0.70f);
                    color.a = 0.66f + lace * 0.08f;
                    pixels[x + y * size] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D BuildSoftCausticTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = (float)y / (size - 1);
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / (size - 1);
                    float noise = Mathf.PerlinNoise(u * 5.2f + 42f, v * 5.2f + 9f);
                    float cellsA = Mathf.Sin((u * 10.5f + noise * 1.7f) * Mathf.PI) * Mathf.Sin((v * 8.5f - noise * 1.1f) * Mathf.PI);
                    float cellsB = Mathf.Sin(((u + v) * 9.0f + noise) * Mathf.PI);
                    float line = Mathf.SmoothStep(0.72f, 0.98f, Mathf.Abs(cellsA) * 0.62f + Mathf.Abs(cellsB) * 0.28f);
                    float alpha = line * 0.30f;
                    pixels[x + y * size] = new Color(0.58f, 0.93f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D BuildSeabedFallbackTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = (float)y / (size - 1);
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / (size - 1);
                    float ripple = Mathf.Sin((u * 13f + v * 2.7f) * Mathf.PI) * 0.5f + 0.5f;
                    float grain = Mathf.PerlinNoise(u * 64f + 3f, v * 64f + 8f);
                    float algae = Mathf.PerlinNoise(u * 18f + 73f, v * 18f + 27f);
                    Color sand = new Color(0.64f, 0.64f, 0.52f, 1f);
                    Color silt = new Color(0.38f, 0.50f, 0.48f, 1f);
                    Color color = Color.Lerp(sand, silt, ripple * 0.24f + grain * 0.14f);
                    color = Color.Lerp(color, new Color(0.30f, 0.42f, 0.24f, 1f), Mathf.SmoothStep(0.64f, 0.92f, algae) * 0.18f);
                    pixels[x + y * size] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static float Hash01(int value)
        {
            unchecked
            {
                uint x = (uint)value;
                x ^= x >> 16;
                x *= 0x7feb352du;
                x ^= x >> 15;
                x *= 0x846ca68bu;
                x ^= x >> 16;
                return (x & 0x00ffffffu) / 16777215f;
            }
        }

        private static void EnsureAssetDirectory(string assetPath)
        {
            string directory = assetPath.EndsWith("/", StringComparison.Ordinal) ? assetPath : Path.GetDirectoryName(assetPath).Replace('\\', '/');
            if (string.IsNullOrEmpty(directory))
                return;

            string[] parts = directory.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string AbsoluteProjectPath(string relativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                throw new InvalidOperationException("Project root could not be resolved.");

            return Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void WriteSuccessReport(int deprecatedCount, List<string> proofPaths, bool exitAfterApply)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("status=APPLIED");
            builder.AppendLine("date=2026-06-08");
            builder.AppendLine("scene=" + ScenePath);
            builder.AppendLine("root=" + RootName);
            builder.AppendLine("exitAfterApply=" + exitAfterApply);
            builder.AppendLine("deprecatedWaterSkyObjects=" + deprecatedCount.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("proofLightingFallback=bound_empty_custom_probe_grid_for_editmode_terrainmaster");
            builder.AppendLine("requiredElements=urp_lit_water_surface,crest_wave_normals,batch21_readable_seabed,limestone_relief_ridges,irregular_ocean_mesh,distant_submerged_ridges,contact_foam,soft_caustics,suspended_particulates,depth_curtains,sky_dome,aegir_disc,moons");
            for (int i = 0; i < proofPaths.Count; i++)
                builder.AppendLine("proof" + i.ToString(CultureInfo.InvariantCulture) + "=" + proofPaths[i]);

            string reportPath = AbsoluteProjectPath(ReportRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, builder.ToString(), Encoding.UTF8);
        }

        private static void WriteFailureReport(Exception exception)
        {
            string reportPath = AbsoluteProjectPath(ReportRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(
                reportPath,
                "status=FAILED\n" +
                "date=2026-06-08\n" +
                "root=" + RootName + "\n" +
                "exception=" + exception + "\n",
                Encoding.UTF8);
        }
    }
}
#endif
