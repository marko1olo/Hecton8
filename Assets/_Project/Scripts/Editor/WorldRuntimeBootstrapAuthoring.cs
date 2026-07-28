using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.AI;
using Hecton8.UI;
using Hecton8.World;
using Hecton8.Dev;
using Hecton8.Environment;
using Hecton8.Biolum;
using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hecton8.Editor
{
    public static class WorldRuntimeBootstrapAuthoring
    {
        private const string RuntimePrefabFolder = "Assets/_Project/Prefabs/WorldRuntime";
        private const string ColliderProxyPrefabPath = RuntimePrefabFolder + "/PFB_ProximityColliderProxy.prefab";
        private const string WorldProfileFolder = "Assets/_Project/Data/World/ZoneProfiles";
        private const string WorldZonePlanFolder = "Assets/_Project/Data/World/ZonePlans";
        private const string WorldExpeditionLoopFolder = "Assets/_Project/Data/World/ExpeditionLoops";
        private const string WorldSandboxAttractionFolder = "Assets/_Project/Data/World/SandboxAttractions";
        private const string WorldMotivationFolder = "Assets/_Project/Data/World/Motivations";
        private const string WorldContentProfileFolder = "Assets/_Project/Data/World/ContentProfiles";
        private const string WorldPopulationRuleFolder = "Assets/_Project/Data/World/PopulationRules";
        private const string WorldFamilyProfileFolder = "Assets/_Project/Data/World/FamilyProfiles";
        private const string WorldProceduralFamilyFolder = "Assets/_Project/Data/World/ProceduralFamilies";
        private const string WorldProceduralRuleFolder = "Assets/_Project/Data/World/ProceduralPlacementRules";
        private const string WorldProceduralPatternCatalogPath = "Assets/_Project/Data/World/ProceduralPatternCatalog.asset";
        private const string WorldProceduralBiomeContextCatalogPath = "Assets/_Project/Data/World/ProceduralBiomeFamilyContextCatalog.asset";
        private const string WorldChunkStreamingProfilePath = "Assets/_Project/Data/World/Streaming/WorldChunkStreamingProfile.asset";
        private const string WorldGeneratedMeshFolder = "Assets/_Project/Art/Meshes/Generated";
        private const string SeamDitherMaterialPath = "Assets/_Project/Art/Materials/VFX/Mat_LeakPlume.mat";
        private const string SeamDitherQuadMeshPath = WorldGeneratedMeshFolder + "/MESH_SeamDitherQuad_1428.asset";
        private const string BiomeFamilyProfileFolder = "Assets/_Project/Data/Biomes/FamilyProfiles";
        private const string BiomeMatrixCatalogPath = "Assets/_Project/Data/Biomes/BiomeMatrixCatalog.asset";
        private const string BiomeBoundarySdfRuntimeTypeName = "Hecton8.World.Biomes.BiomeBoundarySdfRuntime, Hecton8.Core";
        private const string ManagersRootName = "[MANAGERS]";
        private const string NearHolderName = "__NearInteractive";
        private const string MidHolderName = "__MidVisual";
        private const string FarHolderName = "__FarSilhouette";
        private const string WorldRootName = "--- WORLD ---";
        private const string BiolumRootName = "Biolum_Deep";
        private const string StarterReefFieldName = "Starter_ReefField";
        private const string StarterReefFieldPath = "--- WORLD ---/" + StarterReefFieldName;
        private const string HudRootName = "HUD_V4_CanvasRoot";
        private const string RelayMarkerLayerName = "HUD_RouteMarkerLayer";
        private const string RelayRouteMarkerName = "RelayRouteMarker";
        private const float DefaultSurfaceWaterLevelY = 14.02f;

        [MenuItem("Hecton8/Authoring/Rebuild World Runtime Stack", priority = 177)]
        public static void RebuildWorldRuntimeStack()
        {
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder(RuntimePrefabFolder);
            EnsureFolder("Assets/_Project/Data");
            EnsureFolder("Assets/_Project/Data/World");
            EnsureFolder(WorldProfileFolder);
            EnsureFolder(WorldZonePlanFolder);
            EnsureFolder(WorldExpeditionLoopFolder);
            EnsureFolder(WorldSandboxAttractionFolder);
            EnsureFolder(WorldMotivationFolder);
            EnsureFolder(WorldContentProfileFolder);
            EnsureFolder(WorldPopulationRuleFolder);
            EnsureFolder(WorldFamilyProfileFolder);
            EnsureFolder(WorldProceduralFamilyFolder);
            EnsureFolder(WorldProceduralRuleFolder);

            GameObject colliderPrefab = CreateOrUpdateColliderProxyPrefab();
            if (colliderPrefab == null)
            {
                Debug.LogError("[WorldRuntimeBootstrap] Failed to create collider proxy prefab.");
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogError("[WorldRuntimeBootstrap] No active loaded scene.");
                return;
            }

            GameObject managersRoot = FindByPathIncludingInactive(ManagersRootName);
            if (managersRoot == null)
                managersRoot = new GameObject(ManagersRootName);

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
                player = FindByPathIncludingInactive("Player");

            Transform playerTransform = player != null ? player.transform : null;
            Rigidbody playerBody = null;
            if (player != null)
                playerBody = player.TryGetComponent(out Rigidbody resolvedPlayerBody) ? resolvedPlayerBody : null;

            EnsureWorldRouteSkeleton(playerTransform);

            MapMagicBridge bridge = FindSceneObjectIncludingInactive<MapMagicBridge>();
            MapMagic.Core.MapMagicObject mapMagicObject = FindSceneObjectIncludingInactive<MapMagic.Core.MapMagicObject>();
            ScavengePopulator scavengePopulator = FindSceneObjectIncludingInactive<ScavengePopulator>();
            if (scavengePopulator == null)
                scavengePopulator = GetOrAddComponent<ScavengePopulator>(managersRoot);
            FaunaDirector faunaDirector = FindSceneObjectIncludingInactive<FaunaDirector>();
            ObjectPoolManager objectPoolManager = FindSceneObjectIncludingInactive<ObjectPoolManager>();

            BiomeSamplerCache biomeCache = GetOrAddComponent<BiomeSamplerCache>(managersRoot);
            ScatterBudgetController scatterBudgetController = GetOrAddComponent<ScatterBudgetController>(managersRoot);
            WorldStreamingDirector streamingDirector = GetOrAddComponent<WorldStreamingDirector>(managersRoot);
            WorldSliceDirector sliceDirector = GetOrAddComponent<WorldSliceDirector>(managersRoot);
            WorldInterestDirector interestDirector = GetOrAddComponent<WorldInterestDirector>(managersRoot);
            WorldZoneDirector zoneDirector = GetOrAddComponent<WorldZoneDirector>(managersRoot);
            WorldContentDirector contentDirector = GetOrAddComponent<WorldContentDirector>(managersRoot);
            WorldPopulationDirector populationDirector = GetOrAddComponent<WorldPopulationDirector>(managersRoot);
            WorldProceduralFillDirector proceduralFillDirector = GetOrAddComponent<WorldProceduralFillDirector>(managersRoot);
            WorldProceduralFieldSampler proceduralFieldSampler = GetOrAddComponent<WorldProceduralFieldSampler>(managersRoot);
            WorldProceduralScatterDirector proceduralScatterDirector = GetOrAddComponent<WorldProceduralScatterDirector>(managersRoot);
            WorldGenerativeGeologyIntegrationDirector geologyIntegrationDirector = GetOrAddComponent<WorldGenerativeGeologyIntegrationDirector>(managersRoot);
            WorldGenerativeGeologySeamExecutionDirector geologySeamExecutionDirector = GetOrAddComponent<WorldGenerativeGeologySeamExecutionDirector>(managersRoot);
            WorldGenerativeGeologyTerrainSeamApplier geologyTerrainSeamApplier = GetOrAddComponent<WorldGenerativeGeologyTerrainSeamApplier>(managersRoot);
            WorldGenerativeGeologyVoxelBridgeDirector geologyVoxelBridgeDirector = GetOrAddComponent<WorldGenerativeGeologyVoxelBridgeDirector>(managersRoot);
            SedimentAccumulationManager sedimentAccumulationManager = GetOrAddComponent<SedimentAccumulationManager>(managersRoot);
            SeamRegistry seamRegistry = GetOrAddComponent<SeamRegistry>(managersRoot);
            SeamGapDitherRenderer seamGapDitherRenderer = GetOrAddComponent<SeamGapDitherRenderer>(managersRoot);
            seamGapDitherRenderer.SetSeamRegistry(seamRegistry);
            WorldFaunaSpawnRegistry faunaSpawnRegistry = GetOrAddComponent<WorldFaunaSpawnRegistry>(managersRoot);
            WorldProceduralStateRegistry proceduralStateRegistry = GetOrAddComponent<WorldProceduralStateRegistry>(managersRoot);
            BiomeMatrixDirector biomeMatrixDirector = GetOrAddComponent<BiomeMatrixDirector>(managersRoot);
            Component biomeBoundarySdfRuntime = GetOrAddOptionalComponent(managersRoot, BiomeBoundarySdfRuntimeTypeName);
            WorldReadabilityDirector readabilityDirector = GetOrAddComponent<WorldReadabilityDirector>(managersRoot);
            GetOrAddComponent<EmergencyServiceRelayDirector>(managersRoot);
            WorldCaveDirector caveDirector = GetOrAddComponent<WorldCaveDirector>(managersRoot);
            ProximityColliderSystem proximityColliderSystem = GetOrAddComponent<ProximityColliderSystem>(managersRoot);
            HectonBiolumManager biolumManager = GetOrAddComponent<HectonBiolumManager>(managersRoot);
            HectonBiomeMatrixCatalog biomeMatrixCatalog = AssetDatabase.LoadAssetAtPath<HectonBiomeMatrixCatalog>(BiomeMatrixCatalogPath);
            WorldChunkStreamingProfile chunkStreamingProfile = AssetDatabase.LoadAssetAtPath<WorldChunkStreamingProfile>(WorldChunkStreamingProfilePath);

            ConfigureMapMagicObject(mapMagicObject);
            ConfigureMapMagicBridge(bridge, mapMagicObject, playerTransform);
            ConfigureBiomeSamplerCache(biomeCache, bridge, playerTransform);
            ConfigureScavengePopulator(scavengePopulator, chunkStreamingProfile);
            ConfigureWorldFaunaSpawnRegistry(faunaSpawnRegistry, proceduralStateRegistry);
            ConfigureFaunaDirector(faunaDirector, chunkStreamingProfile, faunaSpawnRegistry, proceduralStateRegistry);
            ConfigureProximityColliderSystem(proximityColliderSystem, playerTransform, colliderPrefab);
            ConfigureScatterBudgetController(
                scatterBudgetController,
                playerTransform,
                bridge,
                scavengePopulator,
                proximityColliderSystem,
                biomeCache,
                chunkStreamingProfile);
            ConfigureWorldStreamingDirector(
                streamingDirector,
                playerTransform,
                playerBody,
                bridge,
                biomeCache,
                scatterBudgetController,
                sliceDirector,
                chunkStreamingProfile);
            ConfigureWorldSliceDirector(sliceDirector, playerTransform, chunkStreamingProfile);
            ConfigureWorldInterestDirector(interestDirector, playerTransform, scatterBudgetController);
            ConfigureWorldZoneDirector(zoneDirector, playerTransform);
            ConfigureWorldContentDirector(contentDirector, playerTransform, zoneDirector);
            ConfigureWorldPopulationDirector(populationDirector, playerTransform, zoneDirector, contentDirector);
            ConfigureWorldProceduralFillDirector(proceduralFillDirector, playerTransform, zoneDirector, contentDirector, biomeMatrixDirector);
            ConfigureWorldProceduralFieldSampler(proceduralFieldSampler, playerTransform, bridge, zoneDirector, biomeMatrixDirector);
            ConfigureWorldProceduralScatterDirector(
                proceduralScatterDirector,
                playerTransform,
                proceduralFieldSampler,
                proceduralFillDirector,
                chunkStreamingProfile,
                faunaSpawnRegistry,
                proceduralStateRegistry);
            ConfigureWorldGenerativeGeologyIntegrationDirector(
                geologyIntegrationDirector,
                playerTransform,
                bridge,
                FindSceneObjectIncludingInactive<HectonVoxelEngine>(),
                chunkStreamingProfile);
            ConfigureWorldGenerativeGeologySeamExecutionDirector(
                geologySeamExecutionDirector,
                geologyIntegrationDirector,
                playerTransform);
            ConfigureSeamGapDitherRenderer(
                seamGapDitherRenderer,
                seamRegistry,
                geologyIntegrationDirector,
                playerTransform);
            ConfigureWorldGenerativeGeologyTerrainSeamApplier(
                geologyTerrainSeamApplier,
                geologyIntegrationDirector);
            ConfigureWorldGenerativeGeologyVoxelBridgeDirector(
                geologyVoxelBridgeDirector,
                geologySeamExecutionDirector,
                FindSceneObjectIncludingInactive<HectonVoxelEngine>());
            ConfigureSedimentAccumulationManager(sedimentAccumulationManager);
            ConfigureBiomeMatrixDirector(biomeMatrixDirector, playerTransform, biomeMatrixCatalog);
            ConfigureBiomeBoundarySdfRuntime(biomeBoundarySdfRuntime, playerTransform);
            ConfigureWorldReadabilityDirector(readabilityDirector, zoneDirector, biomeMatrixDirector);
            EnsureRelayHudMarker();
            ConfigureWorldCaveDirector(
                caveDirector,
                playerTransform,
                biomeMatrixDirector,
                zoneDirector,
                bridge,
                FindSceneObjectIncludingInactive<HectonVoxelEngine>(),
                chunkStreamingProfile);
            ConfigureBiolumManager(biolumManager);
            EnsureStarterReefFieldRoot(playerTransform);
            ConfigureSceneSlices();
            ConfigureSceneInterestAnchors();
            ConfigureSceneZones();
            ConfigureSceneContentSockets();
            ConfigureSceneBiolumZones(playerTransform);
            ConfigurePopulationRules(populationDirector);
            ConfigureProceduralFill(proceduralFillDirector);
            ConstructionBootstrapAuthoring.RebuildStarterConstructionKit();
            WorldProceduralSupportFinalAuthoring.RebuildWorldSupportFinals();
            WorldProceduralOrganicMiscFinalAuthoring.RebuildOrganicMiscFinals();
            WorldProceduralGeologyProfileAuthoring.EnsureProfiles();
            WorldProceduralGeologyFinalAuthoring.RebuildGeologyFinals();
            WorldProceduralFinalVariantAuthoring.ApplyFirstWave();
            WorldProceduralFloraTextureAuthoring.Apply();
            WorldProceduralFloraMaterialAuthoring.Apply();
            WorldProceduralFloraBakedStarterGenerator.Generate();
            WorldProceduralFloraFinalVariantAuthoring.ApplyBakedFloraFinals();
            HectonRockRuntimeBootstrapAuthoring.RebuildRockRuntimeStack();
            ConfigureWorldProceduralScatterDirector(
                proceduralScatterDirector,
                playerTransform,
                proceduralFieldSampler,
                proceduralFillDirector,
                chunkStreamingProfile,
                faunaSpawnRegistry,
                proceduralStateRegistry);
            WorldProceduralPlaceholderAuthoring.RebuildPlaceholderProxyVariants();

            if (objectPoolManager != null)
                EnsureWarmupPreset(objectPoolManager, colliderPrefab, 192);
            else
                Debug.LogWarning("[WorldRuntimeBootstrap] ObjectPoolManager not found. Collider proxy warmup was skipped.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log("[WorldRuntimeBootstrap] World runtime stack rebuilt.");
        }

        private static void ConfigureBiomeSamplerCache(
            BiomeSamplerCache biomeCache,
            MapMagicBridge bridge,
            Transform playerTransform)
        {
            SerializedObject so = new SerializedObject(biomeCache);
            so.FindProperty("mapMagicBridge").objectReferenceValue = bridge;
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(biomeCache);
        }

        private static void ConfigureSedimentAccumulationManager(SedimentAccumulationManager sedimentAccumulationManager)
        {
            if (sedimentAccumulationManager == null)
                return;

            EditorUtility.SetDirty(sedimentAccumulationManager);
        }

        private static void ConfigureMapMagicBridge(
            MapMagicBridge bridge,
            MapMagic.Core.MapMagicObject mapMagicObject,
            Transform playerTransform)
        {
            if (bridge == null)
                return;

            SerializedObject so = new SerializedObject(bridge);
            SerializedProperty mapMagicProperty = so.FindProperty("mapMagicObject");
            SerializedProperty playerProperty = so.FindProperty("playerTransform");

            if (mapMagicProperty != null)
                mapMagicProperty.objectReferenceValue = mapMagicObject;

            if (playerProperty != null)
                playerProperty.objectReferenceValue = playerTransform;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bridge);

            if (mapMagicObject != null)
                bridge.SetMapMagicObject(mapMagicObject);

            if (playerTransform != null)
                bridge.SetPlayerTransform(playerTransform);
        }

        private static void ConfigureMapMagicObject(MapMagic.Core.MapMagicObject mapMagicObject)
        {
            if (mapMagicObject == null)
                return;

            SerializedObject so = new SerializedObject(mapMagicObject);
            SerializedProperty draftsInPlaymode = so.FindProperty("draftsInPlaymode");
            SerializedProperty draftResolution = so.FindProperty("draftResolution");
            SerializedProperty tileResolution = so.FindProperty("tileResolution");
            SerializedProperty instantGenerate = so.FindProperty("instantGenerate");
            SerializedProperty hideFarTerrains = so.FindProperty("hideFarTerrains");
            SerializedProperty mainRange = so.FindProperty("mainRange");

            if (instantGenerate != null)
                instantGenerate.boolValue = false;
            if (hideFarTerrains != null)
                hideFarTerrains.boolValue = true;
            if (mainRange != null)
                mainRange.intValue = Mathf.Clamp(mainRange.intValue, 1, 2);
            if (draftsInPlaymode != null)
                draftsInPlaymode.boolValue = true;
            if (draftResolution != null && tileResolution != null)
                draftResolution.enumValueIndex = tileResolution.enumValueIndex;
            so.ApplyModifiedPropertiesWithoutUndo();

            mapMagicObject.instantGenerate = false;
            mapMagicObject.hideFarTerrains = true;
            mapMagicObject.mainRange = Mathf.Clamp(mapMagicObject.mainRange, 1, 2);
            mapMagicObject.tiles.generateLimited = true;
            mapMagicObject.tiles.generateInfinite = true;
            mapMagicObject.tiles.generateRange = Mathf.Max(mapMagicObject.tiles.generateRange, mapMagicObject.mainRange);
            mapMagicObject.draftsInPlaymode = true;
            mapMagicObject.draftResolution = mapMagicObject.tileResolution;
            mapMagicObject.terrainSettings.drawInstanced = true;
            mapMagicObject.globals.objectsNumPerFrame = Mathf.Min(mapMagicObject.globals.objectsNumPerFrame, 128);

            EditorUtility.SetDirty(mapMagicObject);
        }

        private static void ConfigureProximityColliderSystem(
            ProximityColliderSystem proximityColliderSystem,
            Transform playerTransform,
            GameObject colliderPrefab)
        {
            SerializedObject so = new SerializedObject(proximityColliderSystem);
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            so.FindProperty("colliderPrefab").objectReferenceValue = colliderPrefab;
            so.FindProperty("activateRadius").floatValue = 42f;
            so.FindProperty("deactivateRadius").floatValue = 48f;
            so.FindProperty("maxOperationsPerTick").intValue = 64;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(proximityColliderSystem);
        }

        private static void ConfigureScavengePopulator(
            ScavengePopulator scavengePopulator,
            WorldChunkStreamingProfile chunkStreamingProfile)
        {
            if (scavengePopulator == null)
                return;

            SerializedObject so = new SerializedObject(scavengePopulator);
            SerializedProperty profileProperty = so.FindProperty("chunkStreamingProfile");
            if (profileProperty != null)
                profileProperty.objectReferenceValue = chunkStreamingProfile;
            so.ApplyModifiedPropertiesWithoutUndo();
            scavengePopulator.SetChunkStreamingProfile(chunkStreamingProfile);
            EditorUtility.SetDirty(scavengePopulator);
        }

        private static void ConfigureBiolumManager(HectonBiolumManager biolumManager)
        {
            if (biolumManager == null)
                return;

            SerializedObject so = new SerializedObject(biolumManager);
            SerializedProperty autoFindZones = so.FindProperty("_autoFindZones");
            SerializedProperty globalIntensityScale = so.FindProperty("_globalIntensityScale");
            SerializedProperty globalRangeScale = so.FindProperty("_globalRangeScale");

            if (autoFindZones != null)
                autoFindZones.boolValue = true;

            if (globalIntensityScale != null)
                globalIntensityScale.floatValue = 1f;

            if (globalRangeScale != null)
                globalRangeScale.floatValue = 1f;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(biolumManager);
        }

        private static void ConfigureFaunaDirector(
            FaunaDirector faunaDirector,
            WorldChunkStreamingProfile chunkStreamingProfile,
            WorldFaunaSpawnRegistry faunaSpawnRegistry,
            WorldProceduralStateRegistry proceduralStateRegistry)
        {
            if (faunaDirector == null)
                return;

            SerializedObject so = new SerializedObject(faunaDirector);
            SerializedProperty profileProperty = so.FindProperty("chunkStreamingProfile");
            if (profileProperty != null)
                profileProperty.objectReferenceValue = chunkStreamingProfile;
            SerializedProperty registryProperty = so.FindProperty("spawnRegistry");
            if (registryProperty != null)
                registryProperty.objectReferenceValue = faunaSpawnRegistry;
            SerializedProperty proceduralStateProperty = so.FindProperty("proceduralStateRegistry");
            if (proceduralStateProperty != null)
                proceduralStateProperty.objectReferenceValue = proceduralStateRegistry;
            so.ApplyModifiedPropertiesWithoutUndo();
            faunaDirector.SetChunkStreamingProfile(chunkStreamingProfile);
            faunaDirector.SetSpawnRegistry(faunaSpawnRegistry);
            faunaDirector.SetProceduralStateRegistry(proceduralStateRegistry);
            EditorUtility.SetDirty(faunaDirector);
        }

        private static void ConfigureWorldFaunaSpawnRegistry(
            WorldFaunaSpawnRegistry faunaSpawnRegistry,
            WorldProceduralStateRegistry proceduralStateRegistry)
        {
            if (faunaSpawnRegistry == null)
                return;

            SerializedObject so = new SerializedObject(faunaSpawnRegistry);
            SerializedProperty proceduralStateProperty = so.FindProperty("proceduralStateRegistry");
            if (proceduralStateProperty != null)
                proceduralStateProperty.objectReferenceValue = proceduralStateRegistry;
            so.ApplyModifiedPropertiesWithoutUndo();
            faunaSpawnRegistry.SetProceduralStateRegistry(proceduralStateRegistry);
            EditorUtility.SetDirty(faunaSpawnRegistry);
        }

        private static void ConfigureScatterBudgetController(
            ScatterBudgetController controller,
            Transform playerTransform,
            MapMagicBridge bridge,
            ScavengePopulator scavengePopulator,
            ProximityColliderSystem proximityColliderSystem,
            BiomeSamplerCache biomeCache,
            WorldChunkStreamingProfile chunkStreamingProfile)
        {
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            so.FindProperty("mapMagicBridge").objectReferenceValue = bridge;
            so.FindProperty("scavengePopulator").objectReferenceValue = scavengePopulator;
            so.FindProperty("proximityColliderSystem").objectReferenceValue = proximityColliderSystem;
            so.FindProperty("biomeSamplerCache").objectReferenceValue = biomeCache;
            SerializedProperty profileProperty = so.FindProperty("chunkStreamingProfile");
            if (profileProperty != null)
                profileProperty.objectReferenceValue = chunkStreamingProfile;
            so.ApplyModifiedPropertiesWithoutUndo();
            controller.SetChunkStreamingProfile(chunkStreamingProfile);
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureWorldStreamingDirector(
            WorldStreamingDirector director,
            Transform playerTransform,
            Rigidbody playerBody,
            MapMagicBridge bridge,
            BiomeSamplerCache biomeCache,
            ScatterBudgetController scatterBudgetController,
            WorldSliceDirector sliceDirector,
            WorldChunkStreamingProfile chunkStreamingProfile)
        {
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            so.FindProperty("playerRigidbody").objectReferenceValue = playerBody;
            so.FindProperty("mapMagicBridge").objectReferenceValue = bridge;
            so.FindProperty("biomeSamplerCache").objectReferenceValue = biomeCache;
            so.FindProperty("scatterBudgetController").objectReferenceValue = scatterBudgetController;
            so.FindProperty("worldSliceDirector").objectReferenceValue = sliceDirector;
            SerializedProperty profileProperty = so.FindProperty("chunkStreamingProfile");
            if (profileProperty != null)
                profileProperty.objectReferenceValue = chunkStreamingProfile;
            so.ApplyModifiedPropertiesWithoutUndo();
            director.SetChunkStreamingProfile(chunkStreamingProfile);
            EditorUtility.SetDirty(director);
        }

        private static void ConfigureWorldSliceDirector(
            WorldSliceDirector director,
            Transform playerTransform,
            WorldChunkStreamingProfile chunkStreamingProfile)
        {
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            SerializedProperty profileProperty = so.FindProperty("chunkStreamingProfile");
            if (profileProperty != null)
                profileProperty.objectReferenceValue = chunkStreamingProfile;
            so.ApplyModifiedPropertiesWithoutUndo();
            director.SetChunkStreamingProfile(chunkStreamingProfile);
            EditorUtility.SetDirty(director);
        }

        private static void ConfigureWorldInterestDirector(
            WorldInterestDirector director,
            Transform playerTransform,
            ScatterBudgetController scatterBudgetController)
        {
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            so.FindProperty("scatterBudgetController").objectReferenceValue = scatterBudgetController;
            so.FindProperty("worldSliceDirector").objectReferenceValue = GetOrAddComponent<WorldSliceDirector>(FindByPathIncludingInactive(ManagersRootName));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
        }

        private static void ConfigureWorldZoneDirector(
            WorldZoneDirector director,
            Transform playerTransform)
        {
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
        }

        private static void ConfigureWorldContentDirector(
            WorldContentDirector director,
            Transform playerTransform,
            WorldZoneDirector zoneDirector)
        {
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            so.FindProperty("worldZoneDirector").objectReferenceValue = zoneDirector;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
        }

        private static void ConfigureWorldPopulationDirector(
            WorldPopulationDirector director,
            Transform playerTransform,
            WorldZoneDirector zoneDirector,
            WorldContentDirector contentDirector)
        {
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            so.FindProperty("worldZoneDirector").objectReferenceValue = zoneDirector;
            so.FindProperty("worldContentDirector").objectReferenceValue = contentDirector;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
        }

        private static void ConfigureBiomeMatrixDirector(
            BiomeMatrixDirector director,
            Transform playerTransform,
            HectonBiomeMatrixCatalog catalog)
        {
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            so.FindProperty("matrixCatalog").objectReferenceValue = catalog;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
        }

        private static void ConfigureBiomeBoundarySdfRuntime(
            Component runtime,
            Transform playerTransform)
        {
            if (runtime == null)
                return;

            SerializedObject so = new SerializedObject(runtime);
            SerializedProperty playerProperty = so.FindProperty("playerTransform");
            if (playerProperty != null)
                playerProperty.objectReferenceValue = playerTransform;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(runtime);
        }

        private static void ConfigureWorldReadabilityDirector(
            WorldReadabilityDirector director,
            WorldZoneDirector zoneDirector,
            BiomeMatrixDirector biomeMatrixDirector)
        {
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("worldZoneDirector").objectReferenceValue = zoneDirector;
            so.FindProperty("biomeMatrixDirector").objectReferenceValue = biomeMatrixDirector;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
        }

        private static void ConfigureWorldCaveDirector(
            WorldCaveDirector director,
            Transform playerTransform,
            BiomeMatrixDirector biomeMatrixDirector,
            WorldZoneDirector zoneDirector,
            MapMagicBridge mapMagicBridge,
            HectonVoxelEngine voxelEngine,
            WorldChunkStreamingProfile chunkStreamingProfile)
        {
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            so.FindProperty("biomeMatrixDirector").objectReferenceValue = biomeMatrixDirector;
            so.FindProperty("worldZoneDirector").objectReferenceValue = zoneDirector;
            so.FindProperty("mapMagicBridge").objectReferenceValue = mapMagicBridge;
            so.FindProperty("voxelEngine").objectReferenceValue = voxelEngine;
            so.FindProperty("chunkStreamingProfile").objectReferenceValue = chunkStreamingProfile;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
        }

        private static void ConfigureWorldProceduralFillDirector(
            WorldProceduralFillDirector director,
            Transform playerTransform,
            WorldZoneDirector zoneDirector,
            WorldContentDirector contentDirector,
            BiomeMatrixDirector biomeMatrixDirector)
        {
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            so.FindProperty("worldZoneDirector").objectReferenceValue = zoneDirector;
            so.FindProperty("worldContentDirector").objectReferenceValue = contentDirector;
            so.FindProperty("biomeMatrixDirector").objectReferenceValue = biomeMatrixDirector;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
        }

        private static void ConfigureWorldProceduralFieldSampler(
            WorldProceduralFieldSampler sampler,
            Transform playerTransform,
            MapMagicBridge bridge,
            WorldZoneDirector zoneDirector,
            BiomeMatrixDirector biomeMatrixDirector)
        {
            HectonBiomeFamilyProfile littoralKarstFamily = FindBiomeFamilyProfile("biome.family.littoral_karst");
            HectonBiomeFamilyProfile fossilReefFamily = FindBiomeFamilyProfile("biome.family.fossil_reef");
            HectonBiomeFamilyProfile sedimentDriftFamily = FindBiomeFamilyProfile("biome.family.sediment_drift");
            HectonBiomeFamilyProfile abyssalSiltFamily = FindBiomeFamilyProfile("biome.family.abyssal_silt");
            HectonBiomeFamilyProfile graniteEscarpmentFamily = FindBiomeFamilyProfile("biome.family.granite_escarpment");
            HectonBiomeFamilyProfile tectonicSpineFamily = FindBiomeFamilyProfile("biome.family.tectonic_spine");
            HectonBiomeFamilyProfile riftSpineFamily = FindBiomeFamilyProfile("biome.family.rift_spine");
            HectonBiomeFamilyProfile riftVoidFamily = FindBiomeFamilyProfile("biome.family.rift_void");
            HectonBiomeFamilyProfile volcanicGlassFamily = FindBiomeFamilyProfile("biome.family.volcanic_glass");
            HectonBiomeFamilyProfile volcanicHadalFamily = FindBiomeFamilyProfile("biome.family.volcanic_hadal");
            HectonBiomeFamilyProfile metallicHadalFamily = FindBiomeFamilyProfile("biome.family.metallic_hadal");
            HectonBiomeFamilyProfile chemosyntheticBrineFamily = FindBiomeFamilyProfile("biome.family.chemosynthetic_brine");
            HectonBiomeFamilyProfile crystalGrowthFamily = FindBiomeFamilyProfile("biome.family.crystal_growth");

            SerializedObject so = new SerializedObject(sampler);
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            so.FindProperty("mapMagicBridge").objectReferenceValue = bridge;
            so.FindProperty("worldZoneDirector").objectReferenceValue = zoneDirector;
            so.FindProperty("biomeMatrixDirector").objectReferenceValue = biomeMatrixDirector;
            so.FindProperty("littoralKarstFamily").objectReferenceValue = littoralKarstFamily;
            so.FindProperty("fossilReefFamily").objectReferenceValue = fossilReefFamily;
            so.FindProperty("sedimentDriftFamily").objectReferenceValue = sedimentDriftFamily;
            so.FindProperty("abyssalSiltFamily").objectReferenceValue = abyssalSiltFamily;
            so.FindProperty("graniteEscarpmentFamily").objectReferenceValue = graniteEscarpmentFamily;
            so.FindProperty("tectonicSpineFamily").objectReferenceValue = tectonicSpineFamily;
            so.FindProperty("riftSpineFamily").objectReferenceValue = riftSpineFamily;
            so.FindProperty("riftVoidFamily").objectReferenceValue = riftVoidFamily;
            so.FindProperty("volcanicGlassFamily").objectReferenceValue = volcanicGlassFamily;
            so.FindProperty("volcanicHadalFamily").objectReferenceValue = volcanicHadalFamily;
            so.FindProperty("metallicHadalFamily").objectReferenceValue = metallicHadalFamily;
            so.FindProperty("chemosyntheticBrineFamily").objectReferenceValue = chemosyntheticBrineFamily;
            so.FindProperty("crystalGrowthFamily").objectReferenceValue = crystalGrowthFamily;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sampler);
        }

        private static HectonBiomeFamilyProfile FindBiomeFamilyProfile(string familyId)
        {
            string[] guids = AssetDatabase.FindAssets("t:HectonBiomeFamilyProfile", new[] { "Assets/_Project/Data/Biomes/FamilyProfiles" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                HectonBiomeFamilyProfile profile = AssetDatabase.LoadAssetAtPath<HectonBiomeFamilyProfile>(path);
                if (profile != null && string.Equals(profile.familyId, familyId, StringComparison.Ordinal))
                    return profile;
            }

            return null;
        }

        private static void ConfigureWorldProceduralScatterDirector(
            WorldProceduralScatterDirector director,
            Transform playerTransform,
            WorldProceduralFieldSampler fieldSampler,
            WorldProceduralFillDirector fillDirector,
            WorldChunkStreamingProfile chunkStreamingProfile,
            WorldFaunaSpawnRegistry faunaSpawnRegistry,
            WorldProceduralStateRegistry proceduralStateRegistry)
        {
            WorldProceduralPatternCatalog patternCatalog = AssetDatabase.LoadAssetAtPath<WorldProceduralPatternCatalog>(WorldProceduralPatternCatalogPath);
            WorldProceduralBiomeFamilyContextCatalog biomeContextCatalog = AssetDatabase.LoadAssetAtPath<WorldProceduralBiomeFamilyContextCatalog>(WorldProceduralBiomeContextCatalogPath);
            GPUInstancer.GPUInstancerPrefabManager floraGpuiManager = ResolveRockRuntimeGpuiManager();
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            so.FindProperty("fieldSampler").objectReferenceValue = fieldSampler;
            so.FindProperty("proceduralFillDirector").objectReferenceValue = fillDirector;
            so.FindProperty("patternCatalog").objectReferenceValue = patternCatalog;
            so.FindProperty("biomeContextCatalog").objectReferenceValue = biomeContextCatalog;
            SerializedProperty floraGpuiProperty = so.FindProperty("floraGpuiManager");
            if (floraGpuiProperty != null)
                floraGpuiProperty.objectReferenceValue = floraGpuiManager;
            SerializedProperty profileProperty = so.FindProperty("chunkStreamingProfile");
            if (profileProperty != null)
                profileProperty.objectReferenceValue = chunkStreamingProfile;
            SerializedProperty registryProperty = so.FindProperty("faunaSpawnRegistry");
            if (registryProperty != null)
                registryProperty.objectReferenceValue = faunaSpawnRegistry;
            SerializedProperty proceduralStateProperty = so.FindProperty("proceduralStateRegistry");
            if (proceduralStateProperty != null)
                proceduralStateProperty.objectReferenceValue = proceduralStateRegistry;
            so.FindProperty("cellSize").floatValue = 22f;
            so.FindProperty("radiusCells").intValue = 7;
            so.FindProperty("groundPlacementsPerCell").intValue = 2;
            so.FindProperty("clusterPlacementsPerCell").intValue = 1;
            so.FindProperty("structureCellStride").intValue = 2;
            so.FindProperty("structurePlacementsPerWindow").intValue = 1;
            so.FindProperty("spawnCellStride").intValue = 3;
            so.FindProperty("spawnPlacementsPerWindow").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            director.SetChunkStreamingProfile(chunkStreamingProfile);
            director.SetFaunaSpawnRegistry(faunaSpawnRegistry);
            director.SetProceduralStateRegistry(proceduralStateRegistry);
            EditorUtility.SetDirty(director);
        }

        private static GPUInstancer.GPUInstancerPrefabManager ResolveRockRuntimeGpuiManager()
        {
            HectonRockManager rockManager = FindSceneObjectIncludingInactive<HectonRockManager>();
            if (rockManager != null && rockManager.TryGetComponent(out GPUInstancer.GPUInstancerPrefabManager boundManager))
                return boundManager;

            const string RockRuntimeRootName = "Rock_Runtime";
            GameObject runtimeRoot = FindByPathIncludingInactive($"{ManagersRootName}/{RockRuntimeRootName}");
            if (runtimeRoot != null && runtimeRoot.TryGetComponent(out GPUInstancer.GPUInstancerPrefabManager runtimeManager))
                return runtimeManager;

            return FindSceneObjectIncludingInactive<GPUInstancer.GPUInstancerPrefabManager>();
        }

        private static void ConfigureWorldGenerativeGeologyIntegrationDirector(
            WorldGenerativeGeologyIntegrationDirector director,
            Transform playerTransform,
            MapMagicBridge bridge,
            HectonVoxelEngine voxelEngine,
            WorldChunkStreamingProfile chunkStreamingProfile)
        {
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            so.FindProperty("mapMagicBridge").objectReferenceValue = bridge;
            so.FindProperty("voxelEngine").objectReferenceValue = voxelEngine;
            SerializedProperty profileProperty = so.FindProperty("chunkStreamingProfile");
            if (profileProperty != null)
                profileProperty.objectReferenceValue = chunkStreamingProfile;
            so.ApplyModifiedPropertiesWithoutUndo();
            director.SetPlayerTransform(playerTransform);
            director.SetMapMagicBridge(bridge);
            director.SetVoxelEngine(voxelEngine);
            director.SetChunkStreamingProfile(chunkStreamingProfile);
            EditorUtility.SetDirty(director);
        }

        private static void ConfigureWorldGenerativeGeologySeamExecutionDirector(
            WorldGenerativeGeologySeamExecutionDirector director,
            WorldGenerativeGeologyIntegrationDirector integrationDirector,
            Transform playerTransform)
        {
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("integrationDirector").objectReferenceValue = integrationDirector;
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            SerializedProperty gapDitherMaterial = so.FindProperty("gapDitherMaterial");
            if (gapDitherMaterial != null)
                gapDitherMaterial.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(SeamDitherMaterialPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            director.SetIntegrationDirector(integrationDirector);
            director.SetPlayerTransform(playerTransform);
            EditorUtility.SetDirty(director);
        }

        private static void ConfigureSeamGapDitherRenderer(
            SeamGapDitherRenderer renderer,
            SeamRegistry seamRegistry,
            WorldGenerativeGeologyIntegrationDirector integrationDirector,
            Transform playerTransform)
        {
            if (renderer == null)
                return;

            SerializedObject so = new SerializedObject(renderer);
            SetObjectReference(so, "seamRegistry", seamRegistry);
            SetObjectReference(so, "playerTransform", playerTransform);
            SetObjectReference(so, "targetCamera", ResolveSceneCamera());
            SetObjectReference(so, "integrationDirector", integrationDirector);
            SetObjectReference(so, "seamDitherMaterial", AssetDatabase.LoadAssetAtPath<Material>(SeamDitherMaterialPath));
            SetObjectReference(so, "seamDitherQuadMesh", ResolveSeamDitherQuadMesh());
            so.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetSeamRegistry(seamRegistry);
            EditorUtility.SetDirty(renderer);
        }

        private static Camera ResolveSceneCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
                return mainCamera;

            return FindSceneObjectIncludingInactive<Camera>();
        }

        private static Mesh ResolveSeamDitherQuadMesh()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(SeamDitherQuadMeshPath);
            if (mesh != null)
                return mesh;

            EnsureFolder(WorldGeneratedMeshFolder);

            mesh = new Mesh
            {
                name = "MESH_SeamDitherQuad_1428",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(1f, 0f)
                },
                triangles = new[] { 0, 1, 2, 2, 3, 0 },
                normals = new[]
                {
                    Vector3.back,
                    Vector3.back,
                    Vector3.back,
                    Vector3.back
                },
                bounds = new Bounds(Vector3.zero, Vector3.one)
            };
            mesh.UploadMeshData(false);
            AssetDatabase.CreateAsset(mesh, SeamDitherQuadMeshPath);
            AssetDatabase.SaveAssets();
            return mesh;
        }

        private static void SetObjectReference(SerializedObject so, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
        }

        private static void ConfigureWorldGenerativeGeologyTerrainSeamApplier(
            WorldGenerativeGeologyTerrainSeamApplier director,
            WorldGenerativeGeologyIntegrationDirector integrationDirector)
        {
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("integrationDirector").objectReferenceValue = integrationDirector;
            so.ApplyModifiedPropertiesWithoutUndo();
            director.SetIntegrationDirector(integrationDirector);
            EditorUtility.SetDirty(director);
        }

        private static void ConfigureWorldGenerativeGeologyVoxelBridgeDirector(
            WorldGenerativeGeologyVoxelBridgeDirector director,
            WorldGenerativeGeologySeamExecutionDirector seamExecutionDirector,
            HectonVoxelEngine voxelEngine)
        {
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("seamExecutionDirector").objectReferenceValue = seamExecutionDirector;
            so.FindProperty("voxelEngine").objectReferenceValue = voxelEngine;
            so.ApplyModifiedPropertiesWithoutUndo();
            director.SetSeamExecutionDirector(seamExecutionDirector);
            director.SetVoxelEngine(voxelEngine);
            EditorUtility.SetDirty(director);
        }

        private static void ConfigureSceneSlices()
        {
            ConfigureResourceFieldSlice();
            ConfigureFabricationOutpostSlice();
            ConfigureFabricationTrialSlice();
            ConfigureStarterReefFieldSlice();
            ConfigureToolStagingSlice();
            ConfigureToolTrialLaneSlice("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_ServiceModules", 72f, 132f, 18f);
            ConfigureToolTrialLaneSlice("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_ConstructionOps", 68f, 128f, 18f);
            ConfigureToolTrialLaneSlice("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_PowerOps", 72f, 138f, 18f);
            ConfigureToolTrialLaneSlice("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_EndgameOps", 84f, 154f, 20f);
            ConfigureToolTrialLaneSlice("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_CombatContacts", 70f, 134f, 18f);
        }

        private static void ConfigureSceneInterestAnchors()
        {
            ConfigureInterestAnchor(
                "--- WORLD ---/Resource_FieldSources",
                WorldInterestAnchor.InterestKind.ResourceField,
                78f,
                190f,
                1.18f,
                1.16f,
                1.1f,
                1.08f,
                1.08f,
                1.16f);
            ConfigureInterestAnchor(
                "--- WORLD ---/Fabrication_Outpost",
                WorldInterestAnchor.InterestKind.Fabrication,
                72f,
                165f,
                1.08f,
                1.04f,
                1.16f,
                1.12f,
                1.04f,
                1.2f);
            ConfigureInterestAnchor(
                "--- WORLD ---/Tool_Staging/Tool_TrialRange",
                WorldInterestAnchor.InterestKind.ToolRange,
                95f,
                220f,
                1.24f,
                1.22f,
                1.18f,
                1.18f,
                1.12f,
                1.22f);
            ConfigureInterestAnchor(
                "--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_ConstructionOps",
                WorldInterestAnchor.InterestKind.Construction,
                56f,
                132f,
                1.1f,
                1.08f,
                1.12f,
                1.12f,
                1.08f,
                1.16f);
            ConfigureInterestAnchor(
                "--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_ServiceModules",
                WorldInterestAnchor.InterestKind.Service,
                58f,
                136f,
                1.08f,
                1.06f,
                1.12f,
                1.12f,
                1.08f,
                1.14f);
            ConfigureInterestAnchor(
                "--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_PowerOps",
                WorldInterestAnchor.InterestKind.Power,
                60f,
                140f,
                1.12f,
                1.1f,
                1.14f,
                1.12f,
                1.08f,
                1.18f);
            ConfigureInterestAnchor(
                "--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_EndgameOps",
                WorldInterestAnchor.InterestKind.ProgressionHub,
                72f,
                164f,
                1.18f,
                1.14f,
                1.14f,
                1.12f,
                1.06f,
                1.2f);
            ConfigureInterestAnchor(
                StarterReefFieldPath,
                WorldInterestAnchor.InterestKind.ResourceField,
                92f,
                228f,
                1.28f,
                1.36f,
                1.08f,
                1.06f,
                1.22f,
                1.26f);
        }

        private static void ConfigureSceneZones()
        {
            ConfigureZone("--- WORLD ---/Resource_FieldSources", "zone.resources.field", "Resource Field", WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneTier.Starter, 0, 105f, 160f, "Starter raw-resource pocket for scrap, ore, and basic organics.", false, EnsureZoneProfile("ZoneProfile_Resources_Starter.asset", "profile.resources.starter", "Resources Starter", 1.16f, 1.14f, 1.08f, 1.08f, 1.06f, 1.12f, "resources.pickups.near", "resources.clutter.mid", "resources.landmarks.far"), 5);
            ConfigureZone(StarterReefFieldPath, "zone.resources.starter_reef", "Starter Fossil Shelf Field", WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneTier.Starter, 1, 126f, 212f, "Starter readable fossil-shelf pocket for carbonate and kelp GPUI coverage near spawn.", false, EnsureZoneProfile("ZoneProfile_Resources_Starter.asset", "profile.resources.starter", "Resources Starter", 1.16f, 1.14f, 1.08f, 1.08f, 1.06f, 1.12f, "resources.pickups.near", "resources.clutter.mid", "resources.landmarks.far"), 5);
            ConfigureZone("--- WORLD ---/Fabrication_Outpost", "zone.fabrication.outpost", "Fabrication Outpost", WorldZoneAnchor.ZoneKind.Fabrication, WorldZoneAnchor.ZoneTier.Early, 4, 92f, 156f, "Controlled utility stop for crafting, route recovery, and logistic reset.", true, EnsureZoneProfile("ZoneProfile_Fabrication_Early.asset", "profile.fabrication.early", "Fabrication Early", 1.04f, 1.02f, 1.12f, 1.1f, 1.04f, 1.16f, "fabrication.usables.near", "fabrication.outpost.mid", "fabrication.outpost.far"), 6);
            ConfigureZone("--- WORLD ---/Tool_Staging/Tool_TrialRange", "zone.trial.range", "Tool Trial Range", WorldZoneAnchor.ZoneKind.Trial, WorldZoneAnchor.ZoneTier.Early, 1, 110f, 190f, "Compact authored proving ground for tools, flows, and future prefab replacement.", false, EnsureZoneProfile("ZoneProfile_Trial_Early.asset", "profile.trial.early", "Trial Early", 1.08f, 1.08f, 1.06f, 1.06f, 1.08f, 1.18f, "trial.interactive.near", "trial.structures.mid", "trial.readability.far"), 9);
            ConfigureZone("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_ConstructionOps", "zone.trial.construction", "Construction Ops", WorldZoneAnchor.ZoneKind.Construction, WorldZoneAnchor.ZoneTier.Mid, 2, 74f, 126f, "Construction socket, blocker, and placement-control lane.", false, EnsureZoneProfile("ZoneProfile_Construction_Mid.asset", "profile.construction.mid", "Construction Mid", 1.02f, 1.0f, 1.1f, 1.08f, 1.08f, 1.12f, "construction.sockets.near", "construction.frames.mid", "construction.spine.far"), 17);
            ConfigureZone("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_ServiceModules", "zone.trial.service", "Service Modules", WorldZoneAnchor.ZoneKind.Service, WorldZoneAnchor.ZoneTier.Mid, 2, 78f, 132f, "Repair, flooding, and maintenance lane for service gameplay.", false, EnsureZoneProfile("ZoneProfile_Service_Mid.asset", "profile.service.mid", "Service Mid", 1.04f, 1.02f, 1.1f, 1.1f, 1.06f, 1.14f, "service.targets.near", "service.frames.mid", "service.route.far"), 23);
            ConfigureZone("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_PowerOps", "zone.trial.power", "Power Ops", WorldZoneAnchor.ZoneKind.Power, WorldZoneAnchor.ZoneTier.Mid, 2, 80f, 132f, "Generator, relay, and powered service route lane.", false, EnsureZoneProfile("ZoneProfile_Power_Mid.asset", "profile.power.mid", "Power Mid", 1.03f, 1.02f, 1.12f, 1.1f, 1.08f, 1.14f, "power.devices.near", "power.network.mid", "power.route.far"), 27);
            ConfigureZone("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_EndgameOps", "zone.trial.endgame", "Endgame Ops", WorldZoneAnchor.ZoneKind.Progression, WorldZoneAnchor.ZoneTier.Endgame, 5, 96f, 154f, "Mixed late-route lane for recovery, service, hazard, and combat escalation.", true, EnsureZoneProfile("ZoneProfile_Progression_Endgame.asset", "profile.progression.endgame", "Progression Endgame", 1.1f, 1.08f, 1.12f, 1.1f, 1.08f, 1.18f, "progression.setpieces.near", "progression.route.mid", "progression.skyline.far"), 39);
            ConfigureZone("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_CombatContacts", "zone.trial.combat", "Combat Contacts", WorldZoneAnchor.ZoneKind.Combat, WorldZoneAnchor.ZoneTier.Mid, 3, 76f, 126f, "Control, stun, finish, and threat-assessment lane.", false, EnsureZoneProfile("ZoneProfile_Combat_Mid.asset", "profile.combat.mid", "Combat Mid", 0.98f, 0.96f, 1.08f, 1.08f, 1.02f, 1.1f, "combat.targets.near", "combat.readability.mid", "combat.silhouette.far"), 21);
            ConfigureZone("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_ChoiceHub", "zone.trial.choice", "Choice Hub", WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneTier.Mid, 4, 84f, 140f, "Branch-selection hub that previews recovery, construction, and defense routes.", true, EnsureZoneProfile("ZoneProfile_Navigation_Mid.asset", "profile.navigation.mid", "Navigation Mid", 1.02f, 1.02f, 1.06f, 1.04f, 1.04f, 1.16f, "navigation.markers.near", "navigation.route.mid", "navigation.silhouette.far"), 25);
        }

        private static void ConfigureSceneContentSockets()
        {
            ConfigureContentSocket("--- WORLD ---/Resource_FieldSources/Scrap_Field/Scrap_A", "socket.resources.scrap_a", "Scrap A", WorldContentSocket.ContentKind.ResourcePickup, WorldSliceAnchor.SliceState.Near, 4f, 2, "resource.scrap.titanium", "Starter loose scrap pickup.", EnsureContentProfile("ContentProfile_ResourcePickup.asset", "content.profile.resource_pickup", "Resource Pickup", WorldContentSocket.ContentKind.ResourcePickup, WorldZoneAnchor.ZoneKind.Resources, WorldSliceAnchor.SliceState.Near, "resource.pickup", "Loose collectible resource.", 2));
            ConfigureContentSocket("--- WORLD ---/Resource_FieldSources/Mineral_Pocket/Node_Copper_A", "socket.resources.copper_a", "Copper Node A", WorldContentSocket.ContentKind.ResourceNode, WorldSliceAnchor.SliceState.Near, 7f, 3, "resource.node.copper", "Starter copper extraction node.", EnsureContentProfile("ContentProfile_ResourceNode.asset", "content.profile.resource_node", "Resource Node", WorldContentSocket.ContentKind.ResourceNode, WorldZoneAnchor.ZoneKind.Resources, WorldSliceAnchor.SliceState.Near, "resource.node", "Breakable extractable resource node.", 3));
            ConfigureContentSocket("--- WORLD ---/Resource_FieldSources/Mineral_Pocket/Node_Silver_A", "socket.resources.silver_a", "Silver Node A", WorldContentSocket.ContentKind.ResourceNode, WorldSliceAnchor.SliceState.Near, 7f, 4, "resource.node.silver", "Higher-value starter electronics node.", EnsureContentProfile("ContentProfile_ResourceNode.asset", "content.profile.resource_node", "Resource Node", WorldContentSocket.ContentKind.ResourceNode, WorldZoneAnchor.ZoneKind.Resources, WorldSliceAnchor.SliceState.Near, "resource.node", "Breakable extractable resource node.", 3));
            ConfigureContentSocket("--- WORLD ---/Fabrication_Outpost/Forward_Fabricator", "socket.fabrication.forward", "Forward Fabricator", WorldContentSocket.ContentKind.FabricationStation, WorldSliceAnchor.SliceState.Mid, 8f, 5, "station.fabricator.forward", "Controlled fabrication station and recovery stop.", EnsureContentProfile("ContentProfile_FabricationStation.asset", "content.profile.fabrication_station", "Fabrication Station", WorldContentSocket.ContentKind.FabricationStation, WorldZoneAnchor.ZoneKind.Fabrication, WorldSliceAnchor.SliceState.Mid, "station.fabrication", "Crafting and recovery station.", 5));
            ConfigureContentSocket("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_ConstructionOps/Construct_SocketBase", "socket.construction.socket_base", "Construction Socket Base", WorldContentSocket.ContentKind.ConstructionPoint, WorldSliceAnchor.SliceState.Near, 8f, 3, "construction.socket.foundation", "Reliable snapped construction point.", EnsureContentProfile("ContentProfile_ConstructionPoint.asset", "content.profile.construction_point", "Construction Point", WorldContentSocket.ContentKind.ConstructionPoint, WorldZoneAnchor.ZoneKind.Construction, WorldSliceAnchor.SliceState.Near, "construction.point", "Socket or placement point for build flow.", 3));
            ConfigureContentSocket("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_PowerOps/Power_CurrentTurbine", "socket.power.generator", "Current Turbine Point", WorldContentSocket.ContentKind.PowerPoint, WorldSliceAnchor.SliceState.Mid, 9f, 4, "power.generator.current_turbine", "Generator socket for power lane support.", EnsureContentProfile("ContentProfile_PowerPoint.asset", "content.profile.power_point", "Power Point", WorldContentSocket.ContentKind.PowerPoint, WorldZoneAnchor.ZoneKind.Power, WorldSliceAnchor.SliceState.Mid, "power.point", "Generation, relay, or load power point.", 4));
            ConfigureContentSocket("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_PowerOps/Power_ServicePump", "socket.power.load", "Service Pump Load", WorldContentSocket.ContentKind.PowerPoint, WorldSliceAnchor.SliceState.Near, 8f, 4, "power.load.service_pump", "Powered service load target.", EnsureContentProfile("ContentProfile_PowerPoint.asset", "content.profile.power_point", "Power Point", WorldContentSocket.ContentKind.PowerPoint, WorldZoneAnchor.ZoneKind.Power, WorldSliceAnchor.SliceState.Mid, "power.point", "Generation, relay, or load power point.", 4));
            ConfigureContentSocket("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_ServiceModules/Trial_Module_Corridor_Flooded", "socket.service.flooded_corridor", "Flooded Service Corridor", WorldContentSocket.ContentKind.ServiceTarget, WorldSliceAnchor.SliceState.Near, 8f, 4, "service.module.flooded_corridor", "Flooded service target for repair and restoration.", EnsureContentProfile("ContentProfile_ServiceTarget.asset", "content.profile.service_target", "Service Target", WorldContentSocket.ContentKind.ServiceTarget, WorldZoneAnchor.ZoneKind.Service, WorldSliceAnchor.SliceState.Near, "service.target", "Repairable or recoverable service-side target.", 4));
            ConfigureContentSocket("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_BeaconRoute/Route_Anchor", "socket.nav.anchor", "Route Anchor", WorldContentSocket.ContentKind.NavigationMarker, WorldSliceAnchor.SliceState.Mid, 10f, 3, "nav.route.anchor", "Primary return-route marker.", EnsureContentProfile("ContentProfile_NavigationMarker.asset", "content.profile.navigation_marker", "Navigation Marker", WorldContentSocket.ContentKind.NavigationMarker, WorldZoneAnchor.ZoneKind.Navigation, WorldSliceAnchor.SliceState.Mid, "nav.marker", "Readable route or branch marker.", 3));
            ConfigureContentSocket("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_BeaconRoute/Route_Frontier", "socket.nav.frontier", "Route Frontier", WorldContentSocket.ContentKind.NavigationMarker, WorldSliceAnchor.SliceState.Mid, 10f, 5, "nav.route.frontier", "Deep route frontier marker.", EnsureContentProfile("ContentProfile_NavigationMarker.asset", "content.profile.navigation_marker", "Navigation Marker", WorldContentSocket.ContentKind.NavigationMarker, WorldZoneAnchor.ZoneKind.Navigation, WorldSliceAnchor.SliceState.Mid, "nav.marker", "Readable route or branch marker.", 3));
            ConfigureContentSocket("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_DarkRoute/DarkRoute_HazardProbe", "socket.hazard.dark_probe", "Dark Route Hazard Probe", WorldContentSocket.ContentKind.HazardPoint, WorldSliceAnchor.SliceState.Mid, 9f, 4, "hazard.dark_route.probe", "Low-light hazard probe for route reading.", EnsureContentProfile("ContentProfile_HazardPoint.asset", "content.profile.hazard_point", "Hazard Point", WorldContentSocket.ContentKind.HazardPoint, WorldZoneAnchor.ZoneKind.Progression, WorldSliceAnchor.SliceState.Mid, "hazard.point", "Hazard warning or probe target.", 4));
            ConfigureContentSocket("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_CombatContacts/Combat_Aggressive", "socket.combat.aggressive", "Aggressive Contact", WorldContentSocket.ContentKind.CombatPoint, WorldSliceAnchor.SliceState.Near, 8f, 5, "combat.bioform.aggressive", "Aggressive combat contact point.", EnsureContentProfile("ContentProfile_CombatPoint.asset", "content.profile.combat_point", "Combat Point", WorldContentSocket.ContentKind.CombatPoint, WorldZoneAnchor.ZoneKind.Combat, WorldSliceAnchor.SliceState.Near, "combat.point", "Combat-capable contact anchor.", 5));
            ConfigureContentSocket("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_EndgameOps/Ops_Hazard", "socket.progression.ops_hazard", "Operation Hazard", WorldContentSocket.ContentKind.HazardPoint, WorldSliceAnchor.SliceState.Mid, 9f, 5, "progression.ops.hazard", "Mixed-route late-game hazard checkpoint.", EnsureContentProfile("ContentProfile_HazardPoint.asset", "content.profile.hazard_point", "Hazard Point", WorldContentSocket.ContentKind.HazardPoint, WorldZoneAnchor.ZoneKind.Progression, WorldSliceAnchor.SliceState.Mid, "hazard.point", "Hazard warning or probe target.", 4));
            ConfigureContentSocket("--- WORLD ---/Tool_Staging/Tool_TrialRange/Lane_EndgameOps/Ops_Frontier", "socket.progression.frontier", "Ops Frontier", WorldContentSocket.ContentKind.Landmark, WorldSliceAnchor.SliceState.Mid, 10f, 6, "progression.ops.frontier", "Late-route frontier landmark.", EnsureContentProfile("ContentProfile_Landmark.asset", "content.profile.landmark", "Landmark", WorldContentSocket.ContentKind.Landmark, WorldZoneAnchor.ZoneKind.Progression, WorldSliceAnchor.SliceState.Mid, "landmark.point", "Readable distant landmark or late-route goal.", 5));
        }

        private static void ConfigureSceneBiolumZones(Transform playerTransform)
        {
            // Inactive-inclusive on purpose: GameObject.Find sees only active objects, so with the
            // authored world root disabled under DEPRECATED_STUFF this lane silently did nothing at all
            // rather than reporting a problem. See FindSceneRootIncludingInactive for the full history.
            GameObject worldRoot = FindSceneRootIncludingInactive(WorldRootName);
            if (worldRoot == null)
                return;

            GameObject biolumRoot = EnsureChild(worldRoot.transform, BiolumRootName);
            Vector3 playerPosition = playerTransform != null
                ? playerTransform.position
                : new Vector3(1097.3f, 4937.8f, 1349.1f);
            Vector3 center = new Vector3(playerPosition.x, 0f, playerPosition.z);

            ConfigureOceanBiolumZone(
                biolumRoot.transform,
                "Ocean_DeepVeil",
                center + new Vector3(140f, 4450f, -120f),
                "biolum.ocean.deep_veil",
                0.62f,
                5,
                16f,
                0.58f,
                0.22f,
                1.22f,
                15.5f,
                6,
                7,
                0.82f);
            ConfigureOceanBiolumZone(
                biolumRoot.transform,
                "Ocean_AbyssRibbon",
                center + new Vector3(-180f, 3920f, 160f),
                "biolum.ocean.abyss_ribbon",
                0.84f,
                4,
                18f,
                0.44f,
                0.36f,
                1.3f,
                13.5f,
                7,
                6,
                0.74f);
            ConfigureFloorBiolumZone(
                biolumRoot.transform,
                "Floor_BrineGarden",
                center + new Vector3(220f, 3840f, 140f),
                "biolum.floor.brine_garden",
                FloorClusterType.Garden,
                4,
                4.6f,
                0.24f,
                0.38f,
                0.66f,
                0.28f,
                1.28f,
                14.5f,
                6,
                8,
                0.78f);
            ConfigureFloorBiolumZone(
                biolumRoot.transform,
                "Floor_BrittleVents",
                center + new Vector3(-260f, 3560f, -180f),
                "biolum.floor.brittle_vents",
                FloorClusterType.Vent,
                3,
                5.2f,
                0.32f,
                0.52f,
                0.72f,
                0.34f,
                1.34f,
                13.2f,
                6,
                9,
                0.76f);
        }

        private static void EnsureStarterReefFieldRoot(Transform playerTransform)
        {
            // Inactive-inclusive on purpose: GameObject.Find sees only active objects, so with the
            // authored world root disabled under DEPRECATED_STUFF this lane silently did nothing at all
            // rather than reporting a problem. See FindSceneRootIncludingInactive for the full history.
            GameObject worldRoot = FindSceneRootIncludingInactive(WorldRootName);
            if (worldRoot == null)
                return;

            GameObject reefField = EnsureChild(worldRoot.transform, StarterReefFieldName);
            Vector3 anchorPosition = playerTransform != null
                ? playerTransform.position + new Vector3(24f, 0f, 36f)
                : new Vector3(-1567f, DefaultSurfaceWaterLevelY, 2600f);
            reefField.transform.position = anchorPosition;
            reefField.transform.rotation = Quaternion.identity;
            reefField.transform.localScale = Vector3.one;
        }

        /// <summary>
        /// Finds a scene root by name, INCLUDING inactive ones.
        ///
        /// <see cref="GameObject.Find"/> returns only active objects. This tool used it to decide
        /// whether the world root already existed, so once
        /// <c>Assets/_Project/Editor/H8_SceneCleaner.cs</c> reparented <c>--- WORLD ---</c> under
        /// <c>DEPRECATED_STUFF</c> and called <c>SetActive(false)</c> (:41-42, followed by
        /// <c>SaveScene</c> at :47), the reuse check could no longer see it - and every run of this tool
        /// silently created a SECOND, active <c>--- WORLD ---</c> beside the buried one, in a binary
        /// scene with no diff to reveal it. The duplicate carries only the bare Transforms
        /// <c>EnsureRoutePath</c> creates, so it also looks plausible while holding no components.
        ///
        /// Note the asymmetry this fixes: <see cref="Transform.Find"/> DOES see inactive children, which
        /// is why <c>EnsureChild</c> in this same file reuses children correctly. Only the root-level
        /// lookups were blind.
        /// </summary>
        private static GameObject FindSceneRootIncludingInactive(string rootName)
        {
            if (string.IsNullOrEmpty(rootName))
                return null;

            for (int sceneIndex = 0; sceneIndex < EditorSceneManager.sceneCount; sceneIndex++)
            {
                UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    if (string.Equals(roots[i].name, rootName, System.StringComparison.Ordinal))
                        return roots[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves a "/"-separated hierarchy path, INCLUDING inactive objects. Drop-in replacement for
        /// <see cref="GameObject.Find"/> in this tool.
        ///
        /// Every path lookup in this file that starts at <c>--- WORLD ---</c> was blind for the same
        /// reason described on <see cref="FindSceneRootIncludingInactive"/>: the authored world root is
        /// disabled, and <see cref="GameObject.Find"/> skips inactive objects. Only the FIRST segment
        /// needed fixing - <see cref="Transform.Find"/> already accepts a slash-separated path and
        /// already sees inactive children.
        /// </summary>
        private static GameObject FindByPathIncludingInactive(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            int firstSeparator = path.IndexOf('/');
            if (firstSeparator < 0)
                return FindSceneRootIncludingInactive(path);

            GameObject root = FindSceneRootIncludingInactive(path.Substring(0, firstSeparator));
            if (root == null)
                return null;

            Transform child = root.transform.Find(path.Substring(firstSeparator + 1));
            return child != null ? child.gameObject : null;
        }

        private static void EnsureWorldRouteSkeleton(Transform playerTransform)
        {
            GameObject worldRoot = FindSceneRootIncludingInactive(WorldRootName);
            if (worldRoot == null)
                worldRoot = new GameObject(WorldRootName);

            Vector3 anchorOrigin = playerTransform != null
                ? playerTransform.position
                : new Vector3(-1567f, DefaultSurfaceWaterLevelY, 2600f);

            worldRoot.transform.position = anchorOrigin;
            worldRoot.transform.rotation = Quaternion.identity;
            worldRoot.transform.localScale = Vector3.one;

            EnsureRoutePath(worldRoot.transform, "Resource_FieldSources/Scrap_Field/Scrap_A", new Vector3(18f, 0f, 28f));
            EnsureRoutePath(worldRoot.transform, "Resource_FieldSources/Mineral_Pocket/Node_Copper_A", new Vector3(34f, -4f, 42f));
            EnsureRoutePath(worldRoot.transform, "Resource_FieldSources/Mineral_Pocket/Node_Silver_A", new Vector3(46f, -5f, 58f));
            EnsureRoutePath(worldRoot.transform, "Fabrication_Outpost/Forward_Fabricator", new Vector3(-28f, 0f, 62f));
            EnsureRoutePath(worldRoot.transform, "Fabrication_Trial", new Vector3(-42f, 0f, 92f));
            EnsureRoutePath(worldRoot.transform, "Tool_Staging/Tool_TrialRange/Lane_ConstructionOps/Construct_SocketBase", new Vector3(68f, 0f, 112f));
            EnsureRoutePath(worldRoot.transform, "Tool_Staging/Tool_TrialRange/Lane_PowerOps/Power_CurrentTurbine", new Vector3(92f, -8f, 132f));
            EnsureRoutePath(worldRoot.transform, "Tool_Staging/Tool_TrialRange/Lane_PowerOps/Power_ServicePump", new Vector3(114f, -4f, 138f));
            EnsureRoutePath(worldRoot.transform, "Tool_Staging/Tool_TrialRange/Lane_ServiceModules/Trial_Module_Corridor_Flooded", new Vector3(46f, -2f, 146f));
            EnsureRoutePath(worldRoot.transform, "Tool_Staging/Tool_TrialRange/Lane_BeaconRoute/Route_Anchor", new Vector3(0f, 0f, 118f));
            EnsureRoutePath(worldRoot.transform, "Tool_Staging/Tool_TrialRange/Lane_BeaconRoute/Route_Frontier", new Vector3(0f, -10f, 210f));
            EnsureRoutePath(worldRoot.transform, "Tool_Staging/Tool_TrialRange/Lane_DarkRoute/DarkRoute_HazardProbe", new Vector3(-96f, -16f, 184f));
            EnsureRoutePath(worldRoot.transform, "Tool_Staging/Tool_TrialRange/Lane_CombatContacts/Combat_Aggressive", new Vector3(-72f, -6f, 126f));
            EnsureRoutePath(worldRoot.transform, "Tool_Staging/Tool_TrialRange/Lane_EndgameOps/Ops_Hazard", new Vector3(-122f, -18f, 218f));
            EnsureRoutePath(worldRoot.transform, "Tool_Staging/Tool_TrialRange/Lane_EndgameOps/Ops_Frontier", new Vector3(-154f, -26f, 282f));

            EditorUtility.SetDirty(worldRoot);
        }

        private static GameObject EnsureRoutePath(Transform root, string relativePath, Vector3 localPosition)
        {
            string[] segments = relativePath.Split('/');
            Transform current = root;
            for (int i = 0; i < segments.Length; i++)
            {
                GameObject child = EnsureChild(current, segments[i]);
                current = child.transform;
            }

            current.localPosition = localPosition;
            current.localRotation = Quaternion.identity;
            current.localScale = Vector3.one;
            EditorUtility.SetDirty(current.gameObject);
            return current.gameObject;
        }

        private static void ConfigurePopulationRules(WorldPopulationDirector director)
        {
            List<WorldPopulationRule> rules = new List<WorldPopulationRule>
            {
                EnsurePopulationRule("PopulationRule_Resources_Starter.asset", "population.rule.resources.starter", "Starter Resource Pocket", WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneTier.Starter, WorldZoneAnchor.ZoneTier.Early, WorldContentSocket.ContentKind.ResourcePickup, "resource.pickup.cluster", "Starter loose resource pocket.", "Best in readable starter geology with clear gathering loops and obvious return lines.", 1.2f, 3, 2, 6, "biome.family.littoral_karst", "biome.family.sediment_drift", "biome.family.fossil_reef"),
                EnsurePopulationRule("PopulationRule_ResourceNode_Starter.asset", "population.rule.resource_node.starter", "Starter Resource Node", WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneTier.Starter, WorldZoneAnchor.ZoneTier.Mid, WorldContentSocket.ContentKind.ResourceNode, "resource.node.cluster", "Starter extractable node cluster.", "Best where readable stone forms hide mineral pockets without heavy combat pressure.", 1.15f, 2, 1, 3, "biome.family.littoral_karst", "biome.family.granite_escarpment", "biome.family.crystal_growth", "biome.family.fossil_reef"),
                EnsurePopulationRule("PopulationRule_Fabrication_Outpost.asset", "population.rule.fabrication.outpost", "Fabrication Outpost Utility", WorldZoneAnchor.ZoneKind.Fabrication, WorldZoneAnchor.ZoneTier.Early, WorldZoneAnchor.ZoneTier.Mid, WorldContentSocket.ContentKind.FabricationStation, "station.fabrication.outpost", "Crafting/rest stop and support pocket.", "Fits readable transition spaces that are clear enough to regroup without implying comfort.", 0.8f, 1, 1, 1, "biome.family.sediment_drift", "biome.family.littoral_karst", "biome.family.crystal_growth"),
                EnsurePopulationRule("PopulationRule_Construction_Mid.asset", "population.rule.construction.mid", "Construction Support Route", WorldZoneAnchor.ZoneKind.Construction, WorldZoneAnchor.ZoneTier.Mid, WorldZoneAnchor.ZoneTier.Endgame, WorldContentSocket.ContentKind.ConstructionPoint, "construction.support.route", "Sockets and blockers around construction flow.", "Works best in strong structural geology where frames, ledges, and route anchors read clearly.", 1.0f, 2, 1, 3, "biome.family.tectonic_spine", "biome.family.granite_escarpment", "biome.family.rift_spine"),
                EnsurePopulationRule("PopulationRule_Power_Mid.asset", "population.rule.power.mid", "Power Support Chain", WorldZoneAnchor.ZoneKind.Power, WorldZoneAnchor.ZoneTier.Mid, WorldZoneAnchor.ZoneTier.Endgame, WorldContentSocket.ContentKind.PowerPoint, "power.support.chain", "Generation, relay, and service load chain.", "Best in hot, fractured, or chemical spaces where energy infrastructure feels necessary.", 1.0f, 2, 1, 3, "biome.family.volcanic_glass", "biome.family.chemosynthetic_brine", "biome.family.rift_spine", "biome.family.granite_escarpment"),
                EnsurePopulationRule("PopulationRule_Service_Mid.asset", "population.rule.service.mid", "Service Recovery Target", WorldZoneAnchor.ZoneKind.Service, WorldZoneAnchor.ZoneTier.Mid, WorldZoneAnchor.ZoneTier.Endgame, WorldContentSocket.ContentKind.ServiceTarget, "service.recovery.target", "Flooded or damaged service recovery target.", "Best where pressure, silt, or corrosion make maintenance feel like real survival work.", 0.95f, 2, 1, 2, "biome.family.abyssal_silt", "biome.family.chemosynthetic_brine", "biome.family.tectonic_spine", "biome.family.granite_escarpment"),
                EnsurePopulationRule("PopulationRule_Navigation_Mid.asset", "population.rule.navigation.mid", "Navigation Guide Chain", WorldZoneAnchor.ZoneKind.Generic, WorldZoneAnchor.ZoneTier.Early, WorldZoneAnchor.ZoneTier.Endgame, WorldContentSocket.ContentKind.NavigationMarker, "navigation.marker.chain", "Readable route markers and frontier guides.", "Best in spaces where the terrain itself teaches route memory and branch choice.", 0.9f, 3, 2, 4, "biome.family.granite_escarpment", "biome.family.tectonic_spine", "biome.family.sediment_drift"),
                EnsurePopulationRule("PopulationRule_Combat_Mid.asset", "population.rule.combat.mid", "Combat Pressure Node", WorldZoneAnchor.ZoneKind.Combat, WorldZoneAnchor.ZoneTier.Mid, WorldZoneAnchor.ZoneTier.Endgame, WorldContentSocket.ContentKind.CombatPoint, "combat.pressure.node", "Hostile or controlling combat contact.", "Best in fossil shelves, fractures, and hostile terrain that create short control fights instead of flat arenas.", 0.95f, 2, 1, 2, "biome.family.fossil_reef", "biome.family.rift_spine", "biome.family.volcanic_hadal", "biome.family.tectonic_spine"),
                EnsurePopulationRule("PopulationRule_Progression_Endgame.asset", "population.rule.progression.endgame", "Endgame Progression Route", WorldZoneAnchor.ZoneKind.Progression, WorldZoneAnchor.ZoneTier.Late, WorldZoneAnchor.ZoneTier.Endgame, WorldContentSocket.ContentKind.Landmark, "progression.route.landmark", "Late-game frontier landmark and route goal.", "Best in extreme late spaces where the landmark itself is a promise of major progress.", 0.85f, 1, 1, 2, "biome.family.volcanic_hadal", "biome.family.metallic_hadal", "biome.family.rift_void", "biome.family.abyssal_silt"),
                EnsurePopulationRule("PopulationRule_Hazard_Generic.asset", "population.rule.hazard.generic", "Hazard Probe Logic", WorldZoneAnchor.ZoneKind.Generic, WorldZoneAnchor.ZoneTier.Starter, WorldZoneAnchor.ZoneTier.Endgame, WorldContentSocket.ContentKind.HazardPoint, "hazard.probe", "Probe or warning anchor in risky routes.", "Best where the terrain itself carries localized danger and forces a read before commitment.", 0.9f, 2, 1, 3, "biome.family.volcanic_glass", "biome.family.chemosynthetic_brine", "biome.family.rift_void", "biome.family.tectonic_spine", "biome.family.abyssal_silt")
            };

            director.SetRules(rules);
            EditorUtility.SetDirty(director);
        }

        private static void ConfigureOceanBiolumZone(
            Transform parent,
            string objectName,
            Vector3 worldPosition,
            string zoneKey,
            float depthRatio,
            int lightCount,
            float scatterRadius,
            float moodLevel,
            float hazardLevel,
            float intensityMultiplier,
            float rangeMultiplier,
            int updateInterval,
            int maxLights,
            float lodDistanceScale)
        {
            GameObject root = EnsureChild(parent, objectName);
            root.transform.position = worldPosition;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            OceanBiolumZone zone = GetOrAddComponent<OceanBiolumZone>(root);
            SerializedObject so = new SerializedObject(zone);
            so.FindProperty("_zoneKey").stringValue = zoneKey;
            so.FindProperty("_moodLevel").floatValue = moodLevel;
            so.FindProperty("_hazardLevel").floatValue = hazardLevel;
            so.FindProperty("_intensityMultiplier").floatValue = intensityMultiplier;
            so.FindProperty("_rangeMultiplier").floatValue = rangeMultiplier;
            so.FindProperty("_updateInterval").intValue = updateInterval;
            so.FindProperty("_maxLights").intValue = maxLights;
            so.FindProperty("_lodDistanceScale").floatValue = lodDistanceScale;
            so.FindProperty("_depthRatio").floatValue = depthRatio;
            so.FindProperty("_lightCount").intValue = lightCount;
            so.FindProperty("_scatterRadius").floatValue = scatterRadius;
            so.FindProperty("_useNoiseVariation").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(zone);
        }

        private static void ConfigureFloorBiolumZone(
            Transform parent,
            string objectName,
            Vector3 worldPosition,
            string zoneKey,
            FloorClusterType clusterType,
            int clusterCount,
            float clusterSize,
            float pulseIntensity,
            float pulseFrequency,
            float moodLevel,
            float hazardLevel,
            float intensityMultiplier,
            float rangeMultiplier,
            int updateInterval,
            int maxLights,
            float lodDistanceScale)
        {
            GameObject root = EnsureChild(parent, objectName);
            root.transform.position = worldPosition;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            FloorBiolumZone zone = GetOrAddComponent<FloorBiolumZone>(root);
            SerializedObject so = new SerializedObject(zone);
            so.FindProperty("_zoneKey").stringValue = zoneKey;
            so.FindProperty("_moodLevel").floatValue = moodLevel;
            so.FindProperty("_hazardLevel").floatValue = hazardLevel;
            so.FindProperty("_intensityMultiplier").floatValue = intensityMultiplier;
            so.FindProperty("_rangeMultiplier").floatValue = rangeMultiplier;
            so.FindProperty("_updateInterval").intValue = updateInterval;
            so.FindProperty("_maxLights").intValue = maxLights;
            so.FindProperty("_lodDistanceScale").floatValue = lodDistanceScale;
            so.FindProperty("_clusterType").enumValueIndex = (int)clusterType;
            so.FindProperty("_clusterCount").intValue = clusterCount;
            so.FindProperty("_clusterSize").floatValue = clusterSize;
            so.FindProperty("_pulseIntensity").floatValue = pulseIntensity;
            so.FindProperty("_pulseFrequency").floatValue = pulseFrequency;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(zone);
        }

        private static void ConfigureProceduralFill(WorldProceduralFillDirector director)
        {
            List<WorldProceduralPlacementRule> rules = LoadAssets<WorldProceduralPlacementRule>(WorldProceduralRuleFolder);
            List<WorldPrefabFamilyProfile> families = LoadAssets<WorldPrefabFamilyProfile>(WorldProceduralFamilyFolder);
            director.SetRules(rules);
            director.SetFamilies(families);
            EditorUtility.SetDirty(director);
        }

        private static void ConfigureResourceFieldSlice()
        {
            GameObject root = FindByPathIncludingInactive("--- WORLD ---/Resource_FieldSources");
            if (root == null)
                return;

            ZoneFidelityHolders holders = EnsureZoneFidelityHolders(root.transform);

            WorldSliceAnchor anchor = GetOrAddComponent<WorldSliceAnchor>(root);
            SerializedObject so = new SerializedObject(anchor);
            so.FindProperty("nearDistance").floatValue = 180f;
            so.FindProperty("midDistance").floatValue = 320f;
            so.FindProperty("hysteresisPadding").floatValue = 28f;
            AssignContentChildrenToRoots(so.FindProperty("nearOnlyRoots"), root.transform);
            ClearObjectArray(so.FindProperty("midAndNearRoots"));
            AssignSingleRoot(so.FindProperty("midOnlyRoots"), holders.mid);
            AssignSingleRoot(so.FindProperty("farOnlyRoots"), holders.far);
            ClearBehaviourArray(so.FindProperty("nearOnlyBehaviours"));
            ClearBehaviourArray(so.FindProperty("midAndNearBehaviours"));
            ClearBehaviourArray(so.FindProperty("midOnlyBehaviours"));
            ClearBehaviourArray(so.FindProperty("farOnlyBehaviours"));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(anchor);
        }

        private static void ConfigureStarterReefFieldSlice()
        {
            GameObject root = FindByPathIncludingInactive(StarterReefFieldPath);
            if (root == null)
                return;

            ZoneFidelityHolders holders = EnsureZoneFidelityHolders(root.transform);

            WorldSliceAnchor anchor = GetOrAddComponent<WorldSliceAnchor>(root);
            SerializedObject so = new SerializedObject(anchor);
            so.FindProperty("nearDistance").floatValue = 168f;
            so.FindProperty("midDistance").floatValue = 308f;
            so.FindProperty("hysteresisPadding").floatValue = 26f;
            AssignContentChildrenToRoots(so.FindProperty("nearOnlyRoots"), root.transform);
            ClearObjectArray(so.FindProperty("midAndNearRoots"));
            AssignSingleRoot(so.FindProperty("midOnlyRoots"), holders.mid);
            AssignSingleRoot(so.FindProperty("farOnlyRoots"), holders.far);
            ClearBehaviourArray(so.FindProperty("nearOnlyBehaviours"));
            ClearBehaviourArray(so.FindProperty("midAndNearBehaviours"));
            ClearBehaviourArray(so.FindProperty("midOnlyBehaviours"));
            ClearBehaviourArray(so.FindProperty("farOnlyBehaviours"));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(anchor);
        }

        private static void ConfigureFabricationOutpostSlice()
        {
            GameObject root = FindByPathIncludingInactive("--- WORLD ---/Fabrication_Outpost");
            if (root == null)
                return;

            ZoneFidelityHolders holders = EnsureZoneFidelityHolders(root.transform);

            WorldSliceAnchor anchor = GetOrAddComponent<WorldSliceAnchor>(root);
            SerializedObject so = new SerializedObject(anchor);
            so.FindProperty("nearDistance").floatValue = 120f;
            so.FindProperty("midDistance").floatValue = 260f;
            so.FindProperty("hysteresisPadding").floatValue = 24f;
            ClearObjectArray(so.FindProperty("nearOnlyRoots"));
            AssignContentChildrenToRoots(so.FindProperty("midAndNearRoots"), root.transform);
            AssignSingleRoot(so.FindProperty("midOnlyRoots"), holders.mid);
            AssignSingleRoot(so.FindProperty("farOnlyRoots"), holders.far);
            ClearBehaviourArray(so.FindProperty("nearOnlyBehaviours"));
            ClearBehaviourArray(so.FindProperty("midAndNearBehaviours"));
            ClearBehaviourArray(so.FindProperty("midOnlyBehaviours"));
            ClearBehaviourArray(so.FindProperty("farOnlyBehaviours"));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(anchor);
        }

        private static void ConfigureFabricationTrialSlice()
        {
            GameObject root = FindByPathIncludingInactive("Fabrication_Trial");
            if (root == null)
                return;

            ZoneFidelityHolders holders = EnsureZoneFidelityHolders(root.transform);

            WorldSliceAnchor anchor = GetOrAddComponent<WorldSliceAnchor>(root);
            SerializedObject so = new SerializedObject(anchor);
            so.FindProperty("nearDistance").floatValue = 100f;
            so.FindProperty("midDistance").floatValue = 210f;
            so.FindProperty("hysteresisPadding").floatValue = 22f;
            AssignContentChildrenToRoots(so.FindProperty("nearOnlyRoots"), root.transform);
            ClearObjectArray(so.FindProperty("midAndNearRoots"));
            AssignSingleRoot(so.FindProperty("midOnlyRoots"), holders.mid);
            AssignSingleRoot(so.FindProperty("farOnlyRoots"), holders.far);
            ClearBehaviourArray(so.FindProperty("nearOnlyBehaviours"));
            ClearBehaviourArray(so.FindProperty("midAndNearBehaviours"));
            ClearBehaviourArray(so.FindProperty("midOnlyBehaviours"));
            ClearBehaviourArray(so.FindProperty("farOnlyBehaviours"));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(anchor);
        }

        private static void ConfigureToolStagingSlice()
        {
            GameObject root = FindByPathIncludingInactive("Tool_Staging");
            if (root == null)
                return;

            WorldSliceAnchor anchor = GetOrAddComponent<WorldSliceAnchor>(root);
            SerializedObject so = new SerializedObject(anchor);
            so.FindProperty("nearDistance").floatValue = 110f;
            so.FindProperty("midDistance").floatValue = 190f;
            so.FindProperty("hysteresisPadding").floatValue = 20f;
            AssignContentChildrenToRoots(so.FindProperty("nearOnlyRoots"), root.transform);
            ClearObjectArray(so.FindProperty("midAndNearRoots"));
            ClearObjectArray(so.FindProperty("midOnlyRoots"));
            ClearObjectArray(so.FindProperty("farOnlyRoots"));

            SerializedProperty nearBehaviours = so.FindProperty("nearOnlyBehaviours");
            nearBehaviours.arraySize = 1;
            nearBehaviours.GetArrayElementAtIndex(0).objectReferenceValue = root.GetComponent<ToolStagingSpawner>();

            ClearBehaviourArray(so.FindProperty("midAndNearBehaviours"));
            ClearBehaviourArray(so.FindProperty("midOnlyBehaviours"));
            ClearBehaviourArray(so.FindProperty("farOnlyBehaviours"));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(anchor);
        }

        private static void ConfigureToolTrialLaneSlice(
            string lanePath,
            float nearDistance,
            float midDistance,
            float hysteresisPadding)
        {
            GameObject root = FindByPathIncludingInactive(lanePath);
            if (root == null)
                return;

            ZoneFidelityHolders holders = EnsureZoneFidelityHolders(root.transform);

            WorldSliceAnchor anchor = GetOrAddComponent<WorldSliceAnchor>(root);
            SerializedObject so = new SerializedObject(anchor);
            so.FindProperty("nearDistance").floatValue = nearDistance;
            so.FindProperty("midDistance").floatValue = midDistance;
            so.FindProperty("hysteresisPadding").floatValue = hysteresisPadding;
            AssignContentChildrenToRoots(so.FindProperty("nearOnlyRoots"), root.transform);
            ClearObjectArray(so.FindProperty("midAndNearRoots"));
            AssignSingleRoot(so.FindProperty("midOnlyRoots"), holders.mid);
            AssignSingleRoot(so.FindProperty("farOnlyRoots"), holders.far);
            ClearBehaviourArray(so.FindProperty("nearOnlyBehaviours"));
            ClearBehaviourArray(so.FindProperty("midAndNearBehaviours"));
            ClearBehaviourArray(so.FindProperty("midOnlyBehaviours"));
            ClearBehaviourArray(so.FindProperty("farOnlyBehaviours"));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(anchor);
        }

        private static void ConfigureInterestAnchor(
            string objectPath,
            WorldInterestAnchor.InterestKind kind,
            float fullRadius,
            float falloffRadius,
            float scavengeScale,
            float spawnScale,
            float colliderRadiusScale,
            float colliderOpsScale,
            float sliceNearScale = 1.04f,
            float sliceMidScale = 1.08f)
        {
            GameObject root = FindByPathIncludingInactive(objectPath);
            if (root == null)
                return;

            WorldInterestAnchor anchor = GetOrAddComponent<WorldInterestAnchor>(root);
            SerializedObject so = new SerializedObject(anchor);
            so.FindProperty("interestKind").enumValueIndex = (int)kind;
            so.FindProperty("fullInfluenceRadius").floatValue = fullRadius;
            so.FindProperty("falloffRadius").floatValue = falloffRadius;
            so.FindProperty("scavengeRadiusScale").floatValue = scavengeScale;
            so.FindProperty("spawnScale").floatValue = spawnScale;
            so.FindProperty("colliderRadiusScale").floatValue = colliderRadiusScale;
            so.FindProperty("colliderOpsScale").floatValue = colliderOpsScale;
            so.FindProperty("sliceNearScale").floatValue = sliceNearScale;
            so.FindProperty("sliceMidScale").floatValue = sliceMidScale;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(anchor);
        }

        private static void ConfigureZone(
            string objectPath,
            string zoneId,
            string zoneLabel,
            WorldZoneAnchor.ZoneKind zoneKind,
            WorldZoneAnchor.ZoneTier zoneTier,
            int priority,
            float activationRadius,
            float holdRadius,
            string gameplayIntent,
            bool routeCritical,
            WorldZoneProfile zoneProfile,
            int dominantMatrixIndex)
        {
            GameObject root = FindByPathIncludingInactive(objectPath);
            if (root == null)
                return;

            HectonBiomeMatrixProfile dominantBiome = LoadBiomeMatrixProfile(dominantMatrixIndex);
            WorldZoneAnchor zone = GetOrAddComponent<WorldZoneAnchor>(root);
            SerializedObject so = new SerializedObject(zone);
            so.FindProperty("zoneId").stringValue = zoneId;
            so.FindProperty("zoneLabel").stringValue = zoneLabel;
            so.FindProperty("zoneKind").enumValueIndex = (int)zoneKind;
            so.FindProperty("zoneTier").enumValueIndex = (int)zoneTier;
            so.FindProperty("priority").intValue = priority;
            so.FindProperty("activationRadius").floatValue = activationRadius;
            so.FindProperty("holdRadius").floatValue = holdRadius;
            so.FindProperty("edgeBlendDistance").floatValue = InferZoneEdgeBlend(zoneKind, activationRadius);
            so.FindProperty("edgeNoiseScale").floatValue = InferZoneEdgeNoiseScale(zoneKind);
            so.FindProperty("edgeNoiseStrength").floatValue = InferZoneEdgeNoiseStrength(zoneKind, routeCritical);
            so.FindProperty("edgeNoiseOffset").vector2Value = InferZoneEdgeNoiseOffset(dominantMatrixIndex, priority, zoneKind);
            so.FindProperty("gameplayIntent").stringValue = gameplayIntent;
            so.FindProperty("routeCritical").boolValue = routeCritical;
            so.FindProperty("zoneProfile").objectReferenceValue = zoneProfile;
            so.FindProperty("dominantMatrixBiome").objectReferenceValue = dominantBiome;
            so.FindProperty("dominantBiomeFamily").objectReferenceValue = dominantBiome != null ? dominantBiome.familyProfile : null;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(zone);
        }

        private static float InferZoneEdgeBlend(WorldZoneAnchor.ZoneKind zoneKind, float activationRadius)
        {
            float baseBlend = zoneKind switch
            {
                WorldZoneAnchor.ZoneKind.Resources => 26f,
                WorldZoneAnchor.ZoneKind.Navigation => 30f,
                WorldZoneAnchor.ZoneKind.Progression => 22f,
                WorldZoneAnchor.ZoneKind.Combat => 18f,
                WorldZoneAnchor.ZoneKind.Fabrication => 16f,
                _ => 20f
            };

            return Mathf.Clamp(baseBlend, 6f, activationRadius * 0.4f);
        }

        private static float InferZoneEdgeNoiseScale(WorldZoneAnchor.ZoneKind zoneKind)
        {
            return zoneKind switch
            {
                WorldZoneAnchor.ZoneKind.Resources => 0.015f,
                WorldZoneAnchor.ZoneKind.Navigation => 0.013f,
                WorldZoneAnchor.ZoneKind.Progression => 0.02f,
                WorldZoneAnchor.ZoneKind.Combat => 0.024f,
                _ => 0.018f
            };
        }

        private static float InferZoneEdgeNoiseStrength(WorldZoneAnchor.ZoneKind zoneKind, bool routeCritical)
        {
            float strength = zoneKind switch
            {
                WorldZoneAnchor.ZoneKind.Resources => 0.2f,
                WorldZoneAnchor.ZoneKind.Navigation => 0.16f,
                WorldZoneAnchor.ZoneKind.Progression => 0.14f,
                WorldZoneAnchor.ZoneKind.Fabrication => 0.08f,
                _ => 0.12f
            };

            if (routeCritical)
                strength *= 0.82f;

            return Mathf.Clamp(strength, 0.04f, 0.28f);
        }

        private static Vector2 InferZoneEdgeNoiseOffset(int dominantMatrixIndex, int priority, WorldZoneAnchor.ZoneKind zoneKind)
        {
            float zoneBias = ((int)zoneKind + 1) * 17.37f;
            float x = dominantMatrixIndex * 3.11f + priority * 5.7f + zoneBias;
            float y = dominantMatrixIndex * 1.73f + priority * 9.1f + zoneBias * 0.5f;
            return new Vector2(x, y);
        }

        private static void ConfigureContentSocket(
            string objectPath,
            string socketId,
            string socketLabel,
            WorldContentSocket.ContentKind contentKind,
            WorldSliceAnchor.SliceState preferredFidelity,
            float interactionRadius,
            int weight,
            string futurePrefabKey,
            string contentIntent,
            WorldContentProfile contentProfile)
        {
            GameObject target = FindByPathIncludingInactive(objectPath);
            if (target == null)
                return;

            WorldContentSocket socket = GetOrAddComponent<WorldContentSocket>(target);
            SerializedObject so = new SerializedObject(socket);
            so.FindProperty("socketId").stringValue = socketId;
            so.FindProperty("socketLabel").stringValue = socketLabel;
            so.FindProperty("contentKind").enumValueIndex = (int)contentKind;
            so.FindProperty("preferredFidelity").enumValueIndex = (int)preferredFidelity;
            so.FindProperty("interactionRadius").floatValue = interactionRadius;
            so.FindProperty("weight").intValue = weight;
            so.FindProperty("futurePrefabKey").stringValue = futurePrefabKey;
            so.FindProperty("contentIntent").stringValue = contentIntent;
            so.FindProperty("contentProfile").objectReferenceValue = contentProfile;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(socket);
        }

        private static WorldZoneProfile EnsureZoneProfile(
            string fileName,
            string profileId,
            string profileLabel,
            float scavengeRadiusScale,
            float spawnScale,
            float colliderRadiusScale,
            float colliderOpsScale,
            float sliceNearScale,
            float sliceMidScale,
            string nearInteractiveFamily,
            string midVisualFamily,
            string farSilhouetteFamily)
        {
            string assetPath = $"{WorldProfileFolder}/{fileName}";
            WorldZoneProfile profile = AssetDatabase.LoadAssetAtPath<WorldZoneProfile>(assetPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<WorldZoneProfile>();
                AssetDatabase.CreateAsset(profile, assetPath);
            }

            profile.profileId = profileId;
            profile.profileLabel = profileLabel;
            profile.scavengeRadiusScale = scavengeRadiusScale;
            profile.spawnScale = spawnScale;
            profile.colliderRadiusScale = colliderRadiusScale;
            profile.colliderOpsScale = colliderOpsScale;
            profile.sliceNearScale = sliceNearScale;
            profile.sliceMidScale = sliceMidScale;
            profile.nearInteractiveFamily = nearInteractiveFamily;
            profile.midVisualFamily = midVisualFamily;
            profile.farSilhouetteFamily = farSilhouetteFamily;
            profile.nearInteractiveProfile = EnsurePrefabFamilyProfile(nearInteractiveFamily);
            profile.midVisualProfile = EnsurePrefabFamilyProfile(midVisualFamily);
            profile.farSilhouetteProfile = EnsurePrefabFamilyProfile(farSilhouetteFamily);
            profile.zonePlanProfile = EnsureZonePlanProfile(
                $"ZonePlan_{fileName}",
                $"plan.{profileId}",
                $"{profileLabel} Plan",
                profile.nearInteractiveProfile,
                InferSupportFamilyProfile(profile.profileId, WorldSliceAnchor.SliceState.Near),
                InferDensity(profile.profileId, WorldSliceAnchor.SliceState.Near),
                BuildSliceUsage(profile.profileId, WorldSliceAnchor.SliceState.Near),
                profile.midVisualProfile,
                InferSupportFamilyProfile(profile.profileId, WorldSliceAnchor.SliceState.Mid),
                InferDensity(profile.profileId, WorldSliceAnchor.SliceState.Mid),
                BuildSliceUsage(profile.profileId, WorldSliceAnchor.SliceState.Mid),
                profile.farSilhouetteProfile,
                InferSupportFamilyProfile(profile.profileId, WorldSliceAnchor.SliceState.Far),
                InferDensity(profile.profileId, WorldSliceAnchor.SliceState.Far),
                BuildSliceUsage(profile.profileId, WorldSliceAnchor.SliceState.Far),
                InferHeroFamilyProfile(profile.profileId),
                BuildZoneGameplaySummary(profile.profileId));
            profile.expeditionLoopProfile = EnsureExpeditionLoopProfile(
                $"Read_{fileName}",
                $"loop.{profileId}",
                $"{profileLabel} Read",
                profile.profileId);
            profile.sandboxAttractionProfile = EnsureSandboxAttractionProfile(
                $"Sandbox_{fileName}",
                $"sandbox.{profileId}",
                $"{profileLabel} Sandbox",
                profile.profileId);
            profile.motivationProfile = EnsureMotivationProfile(
                $"Motivation_{fileName}",
                $"motivation.{profileId}",
                $"{profileLabel} Motivation",
                profile.profileId);
            ApplySpatialRolePlans(profile.zonePlanProfile, profile.profileId);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static WorldMotivationProfile EnsureMotivationProfile(
            string fileName,
            string profileId,
            string profileLabel,
            string zoneProfileId)
        {
            string assetPath = $"{WorldMotivationFolder}/{fileName}";
            WorldMotivationProfile profile = AssetDatabase.LoadAssetAtPath<WorldMotivationProfile>(assetPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<WorldMotivationProfile>();
                AssetDatabase.CreateAsset(profile, assetPath);
            }

            profile.profileId = profileId;
            profile.profileLabel = profileLabel;
            ApplyMotivationTemplate(profile, zoneProfileId);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void ApplyMotivationTemplate(WorldMotivationProfile profile, string zoneProfileId)
        {
            switch (zoneProfileId)
            {
                case "profile.resources.starter":
                    profile.survivalNeedWeight = 1.35f;
                    profile.resourceNeedWeight = 1.45f;
                    profile.engineeringNeedWeight = 0.75f;
                    profile.curiosityPullWeight = 1.15f;
                    profile.storyPullWeight = 0.7f;
                    profile.rareValuePullWeight = 0.55f;
                    profile.survivalNeed = "This area keeps early runs stocked with oxygen-adjacent safety and common supplies.";
                    profile.resourceNeed = "This area is worth short repeated trips for starter materials.";
                    profile.engineeringNeed = "It lightly supports first tools and first construction costs.";
                    profile.curiosityPull = "Clear shapes and shallow pockets tempt the player to check just one more corner.";
                    profile.storyPull = "It hints that the wider world is bigger and stranger beyond the safe shelf.";
                    profile.rareValuePull = "Rare value is weak here; this water teaches confidence more than greed.";
                    break;
                case "profile.fabrication.early":
                    profile.survivalNeedWeight = 1.4f;
                    profile.resourceNeedWeight = 0.7f;
                    profile.engineeringNeedWeight = 1.2f;
                    profile.curiosityPullWeight = 0.8f;
                    profile.storyPullWeight = 0.9f;
                    profile.rareValuePullWeight = 0.6f;
                    profile.survivalNeed = "This area resets pressure by letting the player recover, craft, and stabilize.";
                    profile.resourceNeed = "Its value is logistical, not raw abundance.";
                    profile.engineeringNeed = "It supports planning tools, modules, and the next safer departure.";
                    profile.curiosityPull = "Nearby authored details should invite checking what else this stop can support.";
                    profile.storyPull = "A fabrication outpost hints at prior human activity and unfinished work.";
                    profile.rareValuePull = "Rare value is indirect here: preparation for stronger future dives.";
                    break;
                case "profile.construction.mid":
                    profile.survivalNeedWeight = 0.95f;
                    profile.resourceNeedWeight = 0.8f;
                    profile.engineeringNeedWeight = 1.45f;
                    profile.curiosityPullWeight = 1f;
                    profile.storyPullWeight = 0.75f;
                    profile.rareValuePullWeight = 0.85f;
                    profile.survivalNeed = "This area can become safer if the player chooses to invest in it.";
                    profile.resourceNeed = "Its value is tied to improved local access and utility rather than raw loot.";
                    profile.engineeringNeed = "This is a natural place to solve space, access, and support problems.";
                    profile.curiosityPull = "Sockets, blockers, and awkward geometry should provoke planning instincts.";
                    profile.storyPull = "It suggests unfinished infrastructure and player-made improvement.";
                    profile.rareValuePull = "Rare value is practical: a better foothold, better serviceability, and better local flow.";
                    break;
                case "profile.power.mid":
                    profile.survivalNeedWeight = 1.05f;
                    profile.resourceNeedWeight = 0.85f;
                    profile.engineeringNeedWeight = 1.5f;
                    profile.curiosityPullWeight = 1f;
                    profile.storyPullWeight = 0.95f;
                    profile.rareValuePullWeight = 1.05f;
                    profile.survivalNeed = "Working power can make nearby water safer and more controllable.";
                    profile.resourceNeed = "Its value comes through systems and recovery, not simple pickups.";
                    profile.engineeringNeed = "This area invites diagnosing lines, relays, loads, and failed links.";
                    profile.curiosityPull = "Visible energy chains should make the player want to see where they begin and end.";
                    profile.storyPull = "Power routes imply old use, current failure, and places that once mattered.";
                    profile.rareValuePull = "Rare value comes from restoring leverage over a difficult pocket of world.";
                    break;
                case "profile.progression.endgame":
                    profile.survivalNeedWeight = 1.1f;
                    profile.resourceNeedWeight = 1.15f;
                    profile.engineeringNeedWeight = 1.2f;
                    profile.curiosityPullWeight = 1.45f;
                    profile.storyPullWeight = 1.6f;
                    profile.rareValuePullWeight = 1.75f;
                    profile.survivalNeed = "This area tests whether the player can survive bad visibility, pressure, and long return distance.";
                    profile.resourceNeed = "The water should promise expensive late-game value, not casual farm loops.";
                    profile.engineeringNeed = "Better gear, stronger tools, and prior preparation should matter here.";
                    profile.curiosityPull = "Abyss edges, strange silhouettes, and severe gradients should keep pulling attention deeper.";
                    profile.storyPull = "This is where strong narrative pull should live: mystery, consequence, and deep-world answers.";
                    profile.rareValuePull = "The strongest lure here is rare value worth fear, planning, and a later return.";
                    break;
                case "profile.combat.mid":
                    profile.survivalNeedWeight = 1.25f;
                    profile.resourceNeedWeight = 0.65f;
                    profile.engineeringNeedWeight = 1.05f;
                    profile.curiosityPullWeight = 1f;
                    profile.storyPullWeight = 0.8f;
                    profile.rareValuePullWeight = 1f;
                    profile.survivalNeed = "This area pressures awareness, spacing, and escape options.";
                    profile.resourceNeed = "Loot matters less than learning how danger behaves in this water.";
                    profile.engineeringNeed = "Tools, control options, and support gear can change how threatening the space feels.";
                    profile.curiosityPull = "Threat behavior and pressure pockets should still provoke observation, not only avoidance.";
                    profile.storyPull = "Danger zones can hint at why the ecosystem behaves differently here.";
                    profile.rareValuePull = "Rare value exists, but it should feel stolen from danger, not handed out.";
                    break;
                case "profile.navigation.mid":
                    profile.survivalNeedWeight = 1f;
                    profile.resourceNeedWeight = 0.6f;
                    profile.engineeringNeedWeight = 0.95f;
                    profile.curiosityPullWeight = 1.2f;
                    profile.storyPullWeight = 0.85f;
                    profile.rareValuePullWeight = 0.55f;
                    profile.survivalNeed = "This area helps the player not get lost when runs grow longer and messier.";
                    profile.resourceNeed = "Its value is mostly navigational clarity with a little practical reward.";
                    profile.engineeringNeed = "It supports route planning, beacon logic, and better return discipline.";
                    profile.curiosityPull = "Branching space should invite comparison and mental mapping.";
                    profile.storyPull = "Landmarks and route breaks can quietly suggest other systems nearby.";
                    profile.rareValuePull = "Rare value is weak; the real payoff is mastery of movement and memory.";
                    break;
                default:
                    profile.survivalNeedWeight = 1f;
                    profile.resourceNeedWeight = 1f;
                    profile.engineeringNeedWeight = 1f;
                    profile.curiosityPullWeight = 1f;
                    profile.storyPullWeight = 1f;
                    profile.rareValuePullWeight = 1f;
                    profile.survivalNeed = "This area offers some practical survival value.";
                    profile.resourceNeed = "This area offers readable material value.";
                    profile.engineeringNeed = "This area can support tools, systems, or later improvements.";
                    profile.curiosityPull = "The player should want to inspect at least one more feature.";
                    profile.storyPull = "The area should imply there is more to understand later.";
                    profile.rareValuePull = "A stronger reward should exist somewhere beyond the first glance.";
                    break;
            }

            profile.optionalityRule = "These pulls are invitations, not requirements; the player decides which one matters today.";
            profile.returnRule = "The area should stay worth revisiting later with a different goal, toolset, or tolerance for risk.";
            profile.ownershipRule = "The player should feel they built their own reasons to come back here.";
        }

        private static WorldExpeditionLoopProfile EnsureExpeditionLoopProfile(
            string fileName,
            string profileId,
            string profileLabel,
            string zoneProfileId)
        {
            string assetPath = $"{WorldExpeditionLoopFolder}/{fileName}";
            WorldExpeditionLoopProfile profile = AssetDatabase.LoadAssetAtPath<WorldExpeditionLoopProfile>(assetPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<WorldExpeditionLoopProfile>();
                AssetDatabase.CreateAsset(profile, assetPath);
            }

            profile.profileId = profileId;
            profile.profileLabel = profileLabel;
            ApplyExpeditionLoopTemplate(profile, zoneProfileId);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static WorldSandboxAttractionProfile EnsureSandboxAttractionProfile(
            string fileName,
            string profileId,
            string profileLabel,
            string zoneProfileId)
        {
            string assetPath = $"{WorldSandboxAttractionFolder}/{fileName}";
            WorldSandboxAttractionProfile profile = AssetDatabase.LoadAssetAtPath<WorldSandboxAttractionProfile>(assetPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<WorldSandboxAttractionProfile>();
                AssetDatabase.CreateAsset(profile, assetPath);
            }

            profile.profileId = profileId;
            profile.profileLabel = profileLabel;
            ApplySandboxAttractionTemplate(profile, zoneProfileId);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static WorldContentProfile EnsureContentProfile(
            string fileName,
            string profileId,
            string profileLabel,
            WorldContentSocket.ContentKind contentKind,
            WorldZoneAnchor.ZoneKind preferredZoneKind,
            WorldSliceAnchor.SliceState preferredFidelity,
            string futurePrefabFamily,
            string gameplayPurpose,
            int defaultWeight)
        {
            string assetPath = $"{WorldContentProfileFolder}/{fileName}";
            WorldContentProfile profile = AssetDatabase.LoadAssetAtPath<WorldContentProfile>(assetPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<WorldContentProfile>();
                AssetDatabase.CreateAsset(profile, assetPath);
            }

            profile.profileId = profileId;
            profile.profileLabel = profileLabel;
            profile.contentKind = contentKind;
            profile.preferredZoneKind = preferredZoneKind;
            profile.preferredFidelity = preferredFidelity;
            profile.futurePrefabFamily = futurePrefabFamily;
            profile.gameplayPurpose = gameplayPurpose;
            profile.defaultWeight = Mathf.Clamp(defaultWeight, 1, 20);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static WorldPopulationRule EnsurePopulationRule(
            string fileName,
            string ruleId,
            string ruleLabel,
            WorldZoneAnchor.ZoneKind zoneKind,
            WorldZoneAnchor.ZoneTier minTier,
            WorldZoneAnchor.ZoneTier maxTier,
            WorldContentSocket.ContentKind contentKind,
            string prefabFamily,
            string gameplayPurpose,
            string biomeFitSummary,
            float densityWeight,
            int suggestedClusterCount,
            int suggestedMinCount,
            int suggestedMaxCount,
            params string[] preferredBiomeFamilyIds)
        {
            string assetPath = $"{WorldPopulationRuleFolder}/{fileName}";
            WorldPopulationRule rule = AssetDatabase.LoadAssetAtPath<WorldPopulationRule>(assetPath);
            if (rule == null)
            {
                rule = ScriptableObject.CreateInstance<WorldPopulationRule>();
                AssetDatabase.CreateAsset(rule, assetPath);
            }

            rule.ruleId = ruleId;
            rule.ruleLabel = ruleLabel;
            rule.zoneKind = zoneKind;
            rule.minTier = minTier;
            rule.maxTier = maxTier;
            rule.contentKind = contentKind;
            rule.prefabFamily = prefabFamily;
            rule.familyProfile = EnsurePrefabFamilyProfile(prefabFamily);
            rule.gameplayPurpose = gameplayPurpose;
            rule.biomeFitSummary = biomeFitSummary;
            rule.preferredBiomeFamilies = LoadBiomeFamilies(preferredBiomeFamilyIds);
            rule.densityWeight = densityWeight;
            rule.suggestedClusterCount = suggestedClusterCount;
            rule.suggestedMinCount = suggestedMinCount;
            rule.suggestedMaxCount = suggestedMaxCount;
            EditorUtility.SetDirty(rule);
            return rule;
        }

        private static HectonBiomeMatrixProfile LoadBiomeMatrixProfile(int matrixIndex)
        {
            if (matrixIndex <= 0)
                return null;

            HectonBiomeMatrixCatalog catalog = AssetDatabase.LoadAssetAtPath<HectonBiomeMatrixCatalog>(BiomeMatrixCatalogPath);
            if (catalog == null || catalog.Profiles == null)
                return null;

            for (int i = 0; i < catalog.Profiles.Length; i++)
            {
                HectonBiomeMatrixProfile profile = catalog.Profiles[i];
                if (profile != null && profile.matrixIndex == matrixIndex)
                    return profile;
            }

            return null;
        }

        private static HectonBiomeFamilyProfile[] LoadBiomeFamilies(params string[] familyIds)
        {
            if (familyIds == null || familyIds.Length == 0)
                return System.Array.Empty<HectonBiomeFamilyProfile>();

            List<HectonBiomeFamilyProfile> results = new List<HectonBiomeFamilyProfile>(familyIds.Length);
            for (int i = 0; i < familyIds.Length; i++)
            {
                HectonBiomeFamilyProfile profile = LoadBiomeFamilyProfile(familyIds[i]);
                if (profile != null && !results.Contains(profile))
                    results.Add(profile);
            }

            return results.ToArray();
        }

        private static HectonBiomeFamilyProfile LoadBiomeFamilyProfile(string familyId)
        {
            if (string.IsNullOrWhiteSpace(familyId))
                return null;

            string safeName = familyId.Replace('.', '_').Replace(':', '_').Replace('/', '_');
            string assetPath = $"{BiomeFamilyProfileFolder}/BiomeFamilyProfile_{safeName}.asset";
            return AssetDatabase.LoadAssetAtPath<HectonBiomeFamilyProfile>(assetPath);
        }

        private static WorldPrefabFamilyProfile EnsurePrefabFamilyProfile(string familyId)
        {
            if (string.IsNullOrWhiteSpace(familyId))
                return null;

            string safeName = familyId.Replace('.', '_');
            string assetPath = $"{WorldFamilyProfileFolder}/FamilyProfile_{safeName}.asset";
            WorldPrefabFamilyProfile profile = AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>(assetPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<WorldPrefabFamilyProfile>();
                AssetDatabase.CreateAsset(profile, assetPath);
            }

            profile.familyId = familyId;
            profile.familyLabel = BuildFamilyLabel(familyId);
            profile.defaultFidelity = InferFamilyFidelity(familyId);
            profile.budgetClass = InferFamilyBudget(familyId);
            profile.expectsInteraction = InferFamilyInteraction(familyId);
            profile.expectsCollision = InferFamilyCollision(familyId, profile.expectsInteraction);
            profile.futurePrefabRoot = familyId;
            profile.gameplayRole = $"Planned world family for '{familyId}'.";
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static WorldZonePlanProfile EnsureZonePlanProfile(
            string fileName,
            string planId,
            string planLabel,
            WorldPrefabFamilyProfile nearPrimary,
            WorldPrefabFamilyProfile nearSupport,
            int nearDensity,
            string nearUsage,
            WorldPrefabFamilyProfile midPrimary,
            WorldPrefabFamilyProfile midSupport,
            int midDensity,
            string midUsage,
            WorldPrefabFamilyProfile farPrimary,
            WorldPrefabFamilyProfile farSupport,
            int farDensity,
            string farUsage,
            WorldPrefabFamilyProfile heroFamily,
            string gameplaySummary)
        {
            string assetPath = $"{WorldZonePlanFolder}/{fileName}";
            WorldZonePlanProfile profile = AssetDatabase.LoadAssetAtPath<WorldZonePlanProfile>(assetPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<WorldZonePlanProfile>();
                AssetDatabase.CreateAsset(profile, assetPath);
            }

            profile.planId = planId;
            profile.planLabel = planLabel;
            profile.nearPlan.primaryFamily = nearPrimary;
            profile.nearPlan.supportFamily = nearSupport;
            profile.nearPlan.targetDensity = nearDensity;
            profile.nearPlan.usage = nearUsage;
            profile.midPlan.primaryFamily = midPrimary;
            profile.midPlan.supportFamily = midSupport;
            profile.midPlan.targetDensity = midDensity;
            profile.midPlan.usage = midUsage;
            profile.farPlan.primaryFamily = farPrimary;
            profile.farPlan.supportFamily = farSupport;
            profile.farPlan.targetDensity = farDensity;
            profile.farPlan.usage = farUsage;
            profile.heroFamily = heroFamily;
            profile.gameplaySummary = gameplaySummary;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void ApplySpatialRolePlans(WorldZonePlanProfile profile, string zoneProfileId)
        {
            if (profile == null)
                return;

            ApplyRolePlan(profile.resourcePocketPlan, zoneProfileId, "resource_pocket");
            ApplyRolePlan(profile.nodeClusterPlan, zoneProfileId, "node_cluster");
            ApplyRolePlan(profile.safePocketPlan, zoneProfileId, "safe_pocket");
            ApplyRolePlan(profile.buildSocketPlan, zoneProfileId, "build_socket");
            ApplyRolePlan(profile.powerSpinePlan, zoneProfileId, "power_spine");
            ApplyRolePlan(profile.serviceChokePlan, zoneProfileId, "service_choke");
            ApplyRolePlan(profile.routeAnchorPlan, zoneProfileId, "route_anchor");
            ApplyRolePlan(profile.hazardGatePlan, zoneProfileId, "hazard_gate");
            ApplyRolePlan(profile.rareObjectivePlan, zoneProfileId, "rare_objective");
            EditorUtility.SetDirty(profile);
        }

        private static void ApplyRolePlan(WorldZonePlanProfile.RolePlan plan, string zoneProfileId, string roleId)
        {
            if (plan == null)
                return;

            plan.family = InferSpatialRoleFamilyProfile(zoneProfileId, roleId);
            plan.relation = InferSpatialRoleRelation(zoneProfileId, roleId);
            plan.preferredSlice = InferSpatialRoleSlice(zoneProfileId, roleId);
            plan.targetCount = InferSpatialRoleCount(zoneProfileId, roleId);
            plan.usage = BuildSpatialRoleUsage(zoneProfileId, roleId);
        }

        private static WorldPrefabFamilyProfile InferSupportFamilyProfile(string zoneProfileId, WorldSliceAnchor.SliceState slice)
        {
            switch (zoneProfileId)
            {
                case "profile.resources.starter":
                    return EnsurePrefabFamilyProfile(slice == WorldSliceAnchor.SliceState.Near ? "resource.node.cluster"
                        : slice == WorldSliceAnchor.SliceState.Mid ? "resource.pickup.cluster"
                        : "resources.landmarks.far");

                case "profile.fabrication.early":
                    return EnsurePrefabFamilyProfile(slice == WorldSliceAnchor.SliceState.Near ? "station.fabrication.outpost"
                        : slice == WorldSliceAnchor.SliceState.Mid ? "navigation.marker.chain"
                        : "fabrication.outpost.far");

                case "profile.trial.early":
                    return EnsurePrefabFamilyProfile(slice == WorldSliceAnchor.SliceState.Near ? "trial.structures.mid"
                        : slice == WorldSliceAnchor.SliceState.Mid ? "trial.readability.far"
                        : "trial.readability.far");

                case "profile.construction.mid":
                    return EnsurePrefabFamilyProfile(slice == WorldSliceAnchor.SliceState.Near ? "construction.support.route"
                        : slice == WorldSliceAnchor.SliceState.Mid ? "construction.support.route"
                        : "construction.spine.far");

                case "profile.power.mid":
                    return EnsurePrefabFamilyProfile(slice == WorldSliceAnchor.SliceState.Near ? "power.support.chain"
                        : slice == WorldSliceAnchor.SliceState.Mid ? "power.route.far"
                        : "power.route.far");

                case "profile.progression.endgame":
                    return EnsurePrefabFamilyProfile(slice == WorldSliceAnchor.SliceState.Near ? "service.recovery.target"
                        : slice == WorldSliceAnchor.SliceState.Mid ? "progression.route.landmark"
                        : "progression.route.landmark");

                case "profile.combat.mid":
                    return EnsurePrefabFamilyProfile(slice == WorldSliceAnchor.SliceState.Near ? "combat.pressure.node"
                        : slice == WorldSliceAnchor.SliceState.Mid ? "combat.pressure.node"
                        : "combat.silhouette.far");

                case "profile.navigation.mid":
                    return EnsurePrefabFamilyProfile(slice == WorldSliceAnchor.SliceState.Near ? "navigation.marker.chain"
                        : slice == WorldSliceAnchor.SliceState.Mid ? "navigation.marker.chain"
                        : "navigation.silhouette.far");
            }

            return null;
        }

        private static WorldPrefabFamilyProfile InferHeroFamilyProfile(string zoneProfileId)
        {
            switch (zoneProfileId)
            {
                case "profile.resources.starter":
                    return EnsurePrefabFamilyProfile("resources.landmarks.far");
                case "profile.fabrication.early":
                    return EnsurePrefabFamilyProfile("station.fabrication.outpost");
                case "profile.trial.early":
                    return EnsurePrefabFamilyProfile("trial.structures.mid");
                case "profile.construction.mid":
                    return EnsurePrefabFamilyProfile("construction.socket.foundation");
                case "profile.power.mid":
                    return EnsurePrefabFamilyProfile("power.generator.current_turbine");
                case "profile.progression.endgame":
                    return EnsurePrefabFamilyProfile("progression.route.landmark");
                case "profile.combat.mid":
                    return EnsurePrefabFamilyProfile("combat.bioform.aggressive");
                case "profile.navigation.mid":
                    return EnsurePrefabFamilyProfile("nav.route.frontier");
            }

            return null;
        }

        private static int InferDensity(string zoneProfileId, WorldSliceAnchor.SliceState slice)
        {
            switch (zoneProfileId)
            {
                case "profile.resources.starter":
                    return slice == WorldSliceAnchor.SliceState.Near ? 18 : slice == WorldSliceAnchor.SliceState.Mid ? 10 : 4;
                case "profile.fabrication.early":
                    return slice == WorldSliceAnchor.SliceState.Near ? 6 : slice == WorldSliceAnchor.SliceState.Mid ? 4 : 2;
                case "profile.trial.early":
                    return slice == WorldSliceAnchor.SliceState.Near ? 10 : slice == WorldSliceAnchor.SliceState.Mid ? 6 : 3;
                case "profile.construction.mid":
                    return slice == WorldSliceAnchor.SliceState.Near ? 8 : slice == WorldSliceAnchor.SliceState.Mid ? 5 : 2;
                case "profile.power.mid":
                    return slice == WorldSliceAnchor.SliceState.Near ? 7 : slice == WorldSliceAnchor.SliceState.Mid ? 5 : 2;
                case "profile.progression.endgame":
                    return slice == WorldSliceAnchor.SliceState.Near ? 9 : slice == WorldSliceAnchor.SliceState.Mid ? 6 : 3;
                case "profile.combat.mid":
                    return slice == WorldSliceAnchor.SliceState.Near ? 5 : slice == WorldSliceAnchor.SliceState.Mid ? 4 : 2;
                case "profile.navigation.mid":
                    return slice == WorldSliceAnchor.SliceState.Near ? 6 : slice == WorldSliceAnchor.SliceState.Mid ? 5 : 3;
            }

            return slice == WorldSliceAnchor.SliceState.Near ? 6 : slice == WorldSliceAnchor.SliceState.Mid ? 4 : 2;
        }

        private static string BuildSliceUsage(string zoneProfileId, WorldSliceAnchor.SliceState slice)
        {
            string sliceLabel = slice.ToString().ToLowerInvariant();

            switch (zoneProfileId)
            {
                case "profile.resources.starter":
                    return $"{sliceLabel}: readable harvesting pocket with scrap, nodes, and mineral shape memory.";
                case "profile.fabrication.early":
                    return $"{sliceLabel}: safe stop with fabrication readability, route rest, and utility silhouette.";
                case "profile.trial.early":
                    return $"{sliceLabel}: authored proving lane with clear tool readability.";
                case "profile.construction.mid":
                    return $"{sliceLabel}: placement guidance, sockets, blockers, and support frames.";
                case "profile.power.mid":
                    return $"{sliceLabel}: generator, relay, and service-load chain readability.";
                case "profile.progression.endgame":
                    return $"{sliceLabel}: late-route escalation with hazard, service, and landmark pull.";
                case "profile.combat.mid":
                    return $"{sliceLabel}: threat readability and control windows.";
                case "profile.navigation.mid":
                    return $"{sliceLabel}: branching route legibility and recovery path memory.";
            }

            return $"{sliceLabel}: generic world composition.";
        }

        private static string BuildZoneGameplaySummary(string zoneProfileId)
        {
            switch (zoneProfileId)
            {
                case "profile.resources.starter":
                    return "Starter harvesting pocket with clear pickups, extractable nodes, and distant landmark memory.";
                case "profile.fabrication.early":
                    return "Safe logistics stop for crafting, regrouping, and optional reset before another dive.";
                case "profile.trial.early":
                    return "Dense authored space for tool practice, regression checks, and future prefab replacement.";
                case "profile.construction.mid":
                    return "Construction pocket with obvious sockets, blockers, and support structure.";
                case "profile.power.mid":
                    return "Power pocket built around generator, relay, and serviced load readability.";
                case "profile.progression.endgame":
                    return "Late-game water that mixes hazard, recovery, combat pressure, and memorable landmarks.";
                case "profile.combat.mid":
                    return "Combat pocket focused on threat readability and control timing.";
                case "profile.navigation.mid":
                    return "Navigation pocket that helps the player read branch choice and return flow.";
            }

            return "Generic world zone plan.";
        }

        private static void ApplyExpeditionLoopTemplate(WorldExpeditionLoopProfile profile, string zoneProfileId)
        {
            switch (zoneProfileId)
            {
                case "profile.resources.starter":
                    profile.entryBeat = "A readable shelf, arch, or scrap silhouette makes the water easy to read at first glance.";
                    profile.routineBeat = "Common materials are visible quickly without forcing one exact sweep.";
                    profile.reliefBeat = "Short reorientation pockets let the player reassess and decide whether to keep wandering.";
                    profile.pressureBeat = "Risk rises naturally as visibility drops and easy materials thin out.";
                    profile.payoffBeat = "A stronger node or denser material patch exists deeper in for players who feel like pushing.";
                    profile.exitBeat = "Leaving stays readable by memory of shape and light, not by a prescribed line.";
                    profile.playerFreedomRule = "The player can drift, circle, detour, or leave at any time; this water only suggests confidence, not obedience.";
                    profile.softProgressionPull = "The place gently teaches that stronger finds usually sit a little beyond the most obvious shelf.";
                    profile.optionalDetourRule = "Side pockets should still teach the terrain and pay out a little value.";
                    profile.returnLogic = "Returning later should feel faster because the player remembers the shapes and material rhythm.";
                    profile.masteryLogic = "Mastery means building a personal quick-harvest loop, not following a designer path.";
                    profile.playerPromise = "Readable early water with recoverable value, mild risk, and room to improvise.";
                    profile.routeMemoryRule = "Memory should come from one strong silhouette and one reliable reorientation pocket.";
                    profile.failureMode = "If shapes, pressure, and value blur together, the area turns into mush.";
                    return;
                case "profile.fabrication.early":
                    profile.entryBeat = "A visible outpost or fabrication stop reads as a reliable human foothold.";
                    profile.routineBeat = "The player quickly understands this is a place to reset, craft, and reassess.";
                    profile.reliefBeat = "This area acts as a dependable pressure break before or after riskier water.";
                    profile.pressureBeat = "Pressure lives mostly outside the stop, not inside it.";
                    profile.payoffBeat = "The reward is readiness for the next self-chosen trip, not a scripted loot moment.";
                    profile.exitBeat = "Departure should feel deliberate and clean, not funnelled.";
                    profile.playerFreedomRule = "The player decides whether this stop matters now, later, or not at all.";
                    profile.softProgressionPull = "The place quietly advertises preparation and optional regrouping.";
                    profile.optionalDetourRule = "Even a brief glance should make the outpost memorable as a future reset point.";
                    profile.returnLogic = "Repeat visits should feel smart and self-directed, never mandatory.";
                    profile.masteryLogic = "Experienced players use the stop as a flexible logistics anchor between different plans.";
                    profile.playerPromise = "A stable pocket of control inside a wider uncertain world.";
                    profile.routeMemoryRule = "Memory should come from the outpost silhouette and its relation to nearby risk water.";
                    profile.failureMode = "If the stop is slow, noisy, or unclear, it stops working as a relief space.";
                    return;
                case "profile.construction.mid":
                    profile.entryBeat = "Sockets, frames, and awkward geometry immediately suggest possible improvement.";
                    profile.routineBeat = "The player reads what is easy to place, what is blocked, and what could become useful later.";
                    profile.reliefBeat = "A clean work pocket offers a moment of clarity in otherwise messy space.";
                    profile.pressureBeat = "Pressure comes from obstruction, poor angles, and the cost of investing here.";
                    profile.payoffBeat = "The reward is a better place: easier travel, better support, or a smarter foothold.";
                    profile.exitBeat = "The player can leave after learning enough, even if they build nothing yet.";
                    profile.playerFreedomRule = "Construction is optional leverage, not a required progression step.";
                    profile.softProgressionPull = "The zone suggests that building here could improve later runs.";
                    profile.optionalDetourRule = "Even without building, the player should understand why this spot matters.";
                    profile.returnLogic = "The place should stay memorable as a future improvement opportunity.";
                    profile.masteryLogic = "Mastery means spotting where investment pays off and where it does not.";
                    profile.playerPromise = "A place where player agency can reshape local quality of life.";
                    profile.routeMemoryRule = "Memory should hold onto the best socket, the main blocker, and the clearest working angle.";
                    profile.failureMode = "If building here changes nothing meaningful, the area feels fake.";
                    return;
                case "profile.power.mid":
                    profile.entryBeat = "Generator, relay, or load silhouettes make the local system legible from a distance.";
                    profile.routineBeat = "The player can quickly read source, transfer, and demand without being forced into one route.";
                    profile.reliefBeat = "A small stable pocket near the system gives room to inspect and plan.";
                    profile.pressureBeat = "Pressure comes from broken links, exposure, and the cost of stabilizing the chain.";
                    profile.payoffBeat = "The reward is regained leverage: safer nearby water, stronger utility, or a working support line.";
                    profile.exitBeat = "Leaving should still feel readable through system memory, not through a fixed track.";
                    profile.playerFreedomRule = "The player can observe, repair, ignore, or return later depending on their needs.";
                    profile.softProgressionPull = "Visible system logic naturally tempts the player to follow power relationships.";
                    profile.optionalDetourRule = "Off-angle looks should still reveal useful system clues or side value.";
                    profile.returnLogic = "Returning later should feel worthwhile because the player remembers how the line behaved.";
                    profile.masteryLogic = "Mastery means seeing the whole power picture fast and acting only where it matters.";
                    profile.playerPromise = "Readable infrastructure that rewards understanding more than obedience.";
                    profile.routeMemoryRule = "Memory should latch onto the strongest source, the most obvious relay, and the hungriest load.";
                    profile.failureMode = "If source, transfer, and demand blur together, the system loses meaning.";
                    return;
                case "profile.progression.endgame":
                    profile.entryBeat = "A strong silhouette or environmental shift warns that this water is serious.";
                    profile.routineBeat = "Routine value is sparse; the area tells the player this is not a lazy farm pocket.";
                    profile.reliefBeat = "Rare reorientation pockets matter because they create decision space before a deeper commitment.";
                    profile.pressureBeat = "Risk ramps through depth, exposure, and uncertainty rather than a forced gate.";
                    profile.payoffBeat = "A rare high-value lure exists deeper in for players willing to bring preparation and nerve.";
                    profile.exitBeat = "Getting out should reward memory, restraint, and choosing when enough is enough.";
                    profile.playerFreedomRule = "The player may sample the edge, turn back, or fully commit on their own terms.";
                    profile.softProgressionPull = "The area should tempt curiosity, need, and story hunger more than any scripted funnel.";
                    profile.optionalDetourRule = "Side observations should still teach something valuable even if the main lure stays out of reach.";
                    profile.returnLogic = "Retreat should preserve knowledge, landmarks, and unfinished intention for a later dive.";
                    profile.masteryLogic = "Mastery means assembling a personal deep-run plan from memory and judgment.";
                    profile.playerPromise = "Serious water with strong mystery, strong value, and honest consequences.";
                    profile.routeMemoryRule = "Memory should come from major shapes, rare shelter, and sharp pressure changes.";
                    profile.failureMode = "If this water becomes linear or obvious, it loses its awe and fear.";
                    return;
                case "profile.combat.mid":
                    profile.entryBeat = "Threat cues in movement, sound, or silhouette signal that awareness matters here.";
                    profile.routineBeat = "The player reads spacing, control options, and escape space before picking a response.";
                    profile.reliefBeat = "Cover or calmer side water provides a moment to recover and reassess.";
                    profile.pressureBeat = "Pressure comes from threat behavior and local geometry, not from a forced duel lane.";
                    profile.payoffBeat = "The payoff is surviving well, stealing value, or learning how this danger behaves.";
                    profile.exitBeat = "Leaving should still be a valid smart choice.";
                    profile.playerFreedomRule = "The player may engage, evade, control, or postpone the problem.";
                    profile.softProgressionPull = "Threat should make the water feel charged, not scripted.";
                    profile.optionalDetourRule = "Detours should offer flanking space, observation angles, or minor reward.";
                    profile.returnLogic = "A second visit should feel smarter because the player better understands local danger.";
                    profile.masteryLogic = "Mastery means deciding when the fight is not worth the cost.";
                    profile.playerPromise = "Tense water where control, timing, and escape matter more than brute force.";
                    profile.routeMemoryRule = "Memory should hold onto cover, escape angle, and the shape of the threat pocket.";
                    profile.failureMode = "If every encounter reads like a mandatory lane fight, the space stops feeling alive.";
                    return;
                case "profile.navigation.mid":
                    profile.entryBeat = "Distinct silhouettes and branching space make orientation the main value.";
                    profile.routineBeat = "The player quickly understands that mental mapping matters more than raw loot here.";
                    profile.reliefBeat = "Short calm ledges or readable pockets help reset direction.";
                    profile.pressureBeat = "Pressure comes from getting turned around, not from being shoved down one line.";
                    profile.payoffBeat = "The reward is better world memory, cleaner returns, and confidence in branch choice.";
                    profile.exitBeat = "Leaving should feel easier because the player now owns more of the map in their head.";
                    profile.playerFreedomRule = "The player is free to test branches, retreat, or cross-cut between landmarks.";
                    profile.softProgressionPull = "The area nudges exploration through readability and curiosity, not compulsion.";
                    profile.optionalDetourRule = "Wrong turns should still teach the space and occasionally pay out.";
                    profile.returnLogic = "Repeat visits should become smoother as landmark memory grows.";
                    profile.masteryLogic = "Mastery means building your own internal map and faster cross-links.";
                    profile.playerPromise = "Readable branching water that turns confusion into ownership over time.";
                    profile.routeMemoryRule = "Memory should come from silhouettes, branch logic, and safe reorientation pockets.";
                    profile.failureMode = "If every branch feels the same, the zone loses identity.";
                    return;
                default:
                    profile.entryBeat = "A readable landmark or contrast in the water invites first contact.";
                    profile.routineBeat = "Common value can be found without implying one correct sweep.";
                    profile.reliefBeat = "At least one reorientation pocket gives the player space to think.";
                    profile.pressureBeat = "Risk grows naturally where depth, threat, or visibility turn harsher.";
                    profile.payoffBeat = "A stronger lure exists deeper in for players who choose to keep pushing.";
                    profile.exitBeat = "Returning stays readable through remembered shapes, not a prescribed line.";
                    profile.playerFreedomRule = "This profile is a reading aid, not a route script.";
                    profile.softProgressionPull = "The area can tempt attention without claiming ownership over the player's path.";
                    profile.optionalDetourRule = "Detours should still teach the place and offer some value.";
                    profile.returnLogic = "Leaving early should still produce useful memory for later.";
                    profile.masteryLogic = "Mastery means inventing a personal line through risk and opportunity.";
                    profile.playerPromise = "Readable value, readable danger, and plenty of room for player choice.";
                    profile.routeMemoryRule = "The player should remember the place through shape, shelter, and pressure shifts.";
                    profile.failureMode = "If readability collapses, the space becomes noise instead of a sandbox.";
                    return;
            }
        }
#if false
            switch (zoneProfileId)
            {
                case "profile.resources.starter":
                    profile.entryBeat = "Vhod cherez prostoy orientir: svetlaya kromka, arka, oblomok ili ponyatnyy route anchor.";
                    profile.routineBeat = "Soberi bystrye nearby pockets i ne teryay glavnyy orientir.";
                    profile.reliefBeat = "Peredyshka korotkaya: karman za formoy relefa, gde mozhno bystro sveritsya s napravleniem.";
                    profile.pressureBeat = "Dalshe nachinaetsya pervyy risk: glubzhe, temnee, menshe legkoy dobychi.";
                    profile.payoffBeat = "Glavnaya nagrada - pervyy horoshiy uzel ili zametnaya gruppa materialov.";
                    profile.exitBeat = "Vyhod dolzhen chitatsya po tem zhe formam, po kotorym ty voshel.";
                    profile.playerFreedomRule = "Igrok mozhet svobodno kruzhit po startovoy vode; zona lish myagko podskazyvaet vygodnyy krug.";
                    profile.softProgressionPull = "Silnee vsego tyanet tuda, gde prostoy orientir perehodit v pervyy zametnyy uzel.";
                    profile.optionalDetourRule = "Esli igrok ushel v storonu, on vse ravno dolzhen vzyat chto-to poleznoe i ponyat relef.";
                    profile.returnLogic = "Dazhe korotkiy vyhod iz zony dolzhen zapominatsya kak ponyatnyy sborochnyy karman.";
                    profile.masteryLogic = "Opytnyy igrok mozhet rezat startovye krugi ochen bystro, pochti bez ostanovok.";
                    profile.playerPromise = "Ponyatnyy startovyy krug: bystro nashel, bystro ponyal, vernulsya po chitaemomu marshrutu.";
                    profile.routeMemoryRule = "Igrok dolzhen pomnit odin silnyy vhodnoy orientir i odin karman peredyshki.";
                    profile.failureMode = "Esli uyti slishkom gluboko bez yakorya marshruta, startovaya zona perestaet byt prostoy.";
                    break;
                case "profile.fabrication.early":
                    profile.entryBeat = "Vhod cherez uznavaemyy forpost ili teh-ostanovku.";
                    profile.routineBeat = "Blizhayshiy krug — proverit kraft, logistiku i vosstanovit nabor.";
                    profile.reliefBeat = "Peredyshka tut glavnaya: igrok dolzhen chuvstvovat kontrol i reset.";
                    profile.pressureBeat = "Sleduyuschee davlenie nachinaetsya uzhe posle vyhoda iz korotkogo kontura peredyshki.";
                    profile.payoffBeat = "Glavnaya nagrada — ne lut, a podgotovka k sleduyuschemu zahodu.";
                    profile.exitBeat = "Vyhod dolzhen byt korotkim i ochevidnym.";
                    profile.playerFreedomRule = "Igrok volen ispolzovat forpost kogda hochet; sistema ne trebuet poseschat ego po taymeru.";
                    profile.softProgressionPull = "Mesto myagko tyanet k sebe pered novym riskom, no ne zapiraet na odnom marshrute.";
                    profile.optionalDetourRule = "Dazhe esli igrok prosto proskochil mimo, on dolzhen zapomnit tochku kak vozmozhnyy reset.";
                    profile.returnLogic = "Vozvrat syuda dolzhen oschuschatsya kak razumnoe reshenie, a ne obyazatelnaya ostanovka.";
                    profile.masteryLogic = "Opytnyy igrok ispolzuet forpost kak bystruyu logisticheskuyu zasechku, a ne kak hab po raspisaniyu.";
                    profile.playerPromise = "Nadezhnaya ostanovka mezhdu riskovannymi kuskami marshruta.";
                    profile.routeMemoryRule = "Igrok dolzhen pomnit, gde eto mesto otnositelno sleduyuschey opasnoy zony.";
                    profile.failureMode = "Esli outpost ne chitaetsya bystro, on perestaet byt tochkoy otdyha.";
                    break;
                case "profile.construction.mid":
                    profile.entryBeat = "Vhod cherez ponyatnuyu ploschadku, soket ili chistyy stroitelnyy koridor.";
                    profile.routineBeat = "Snachala igrok otsenivaet, chto tut stavitsya legko, a chto meshaet.";
                    profile.reliefBeat = "Peredyshka — eto horoshiy build pocket, gde vse chitaetsya bez suety.";
                    profile.pressureBeat = "Davlenie sozdayut blokery, neudachnye ugly i potrebnost v pravilnom module.";
                    profile.payoffBeat = "Glavnaya nagrada — rabochaya tochka, posle kotoroy put stanovitsya luchshe.";
                    profile.exitBeat = "Igrok uhodit, ostaviv mesto ponyatnee i poleznee, chem nashel.";
                    profile.playerFreedomRule = "Igrok sam reshaet, stroit tut seychas, pozzhe ili voobsche oboyti eto mesto.";
                    profile.softProgressionPull = "Zona myagko podskazyvaet, chto pravilnaya postroyka sdelaet dalneyshiy marshrut udobnee.";
                    profile.optionalDetourRule = "Dazhe esli igrok ne stroit, on dolzhen ponyat, zachem eto mesto mozhet prigoditsya.";
                    profile.returnLogic = "Stroitelnaya zona dolzhna ostavatsya v pamyati kak potentsialnaya tochka uluchsheniya marshruta.";
                    profile.masteryLogic = "Opytnyy igrok bystro ponimaet, chto i gde stroit, a chto mozhno ignorirovat.";
                    profile.playerPromise = "Stroitelstvo dolzhno uluchshat prostranstvo, a ne tolko tratit resursy.";
                    profile.routeMemoryRule = "Igrok dolzhen pomnit, gde horoshiy soket i gde byl glavnyy blocker.";
                    profile.failureMode = "Esli stroyka ne menyaet marshrut, zona oschuschaetsya pustoy.";
                    break;
                case "profile.power.mid":
                    profile.entryBeat = "Vhod cherez vidimyy generatornyy ili releynyy kontur.";
                    profile.routineBeat = "Igrok bystro chitaet liniyu: istochnik -> peredacha -> nagruzka.";
                    profile.reliefBeat = "Peredyshka korotkaya i tehnichnaya: mesto, gde liniya snova ponyatna.";
                    profile.pressureBeat = "Davlenie rastet tam, gde liniya rvetsya ili uhodit v plohuyu vodu.";
                    profile.payoffBeat = "Glavnaya nagrada — ozhivlennaya liniya, stabilnaya nagruzka ili vygodnaya power pocket.";
                    profile.exitBeat = "Vyhod idet po toy zhe energeticheskoy logike, ne po haosu.";
                    profile.playerFreedomRule = "Igrok ne obyazan chinit ili chitat vsyu liniyu za odin zahod.";
                    profile.softProgressionPull = "Kontur myagko vedet vzglyad ot istochnika k problemnoy tochke.";
                    profile.optionalDetourRule = "Dazhe otdelnyy kusok linii dolzhen chitatsya kak chast bolshey sistemy.";
                    profile.returnLogic = "Igrok mozhet vernutsya pozzhe s luchshim naborom i doreshat tot zhe power-kontur.";
                    profile.masteryLogic = "Opytnyy igrok budet videt liniyu tselikom i srezat put k glavnoy probleme.";
                    profile.playerPromise = "Silovaya zona dolzhna chitatsya kak sistema, a ne kak nabor obektov.";
                    profile.routeMemoryRule = "Igrok dolzhen pomnit istochnik, odin relay i problemnyy load point.";
                    profile.failureMode = "Esli silovaya liniya ne chitaetsya, igrok perestaet ponimat, zachem ona nuzhna.";
                    break;
                case "profile.progression.endgame":
                    profile.entryBeat = "Vhod cherez posledniy nadezhnyy orientir pered sereznym davleniem.";
                    profile.routineBeat = "Rutiny malo: igrok bystro ponimaet, chto eto uzhe ne zona dlya lenivogo farma.";
                    profile.reliefBeat = "Peredyshka redkaya i dorogaya — tolko korotkoe okno pered sleduyuschim push.";
                    profile.pressureBeat = "Osnovnaya chast zony — hazard gate, service choke, threat pressure i plohoy vozvrat.";
                    profile.payoffBeat = "Glavnaya nagrada — redkaya tsel ili late-game material, opravdyvayuschiy ves zahod.";
                    profile.exitBeat = "Vyhod dolzhen opiratsya na pamyat marshruta, a ne na improvizatsiyu.";
                    profile.playerFreedomRule = "Igrok mozhet voobsche otkazatsya ot glubokoy tseli i vernutsya, eto tozhe normalnyy ishod.";
                    profile.softProgressionPull = "Zona dolzhna soblaznyat redkoy tsennostyu, a ne nasilno zatalkivat vpered.";
                    profile.optionalDetourRule = "Bokovye karmany dolzhny davat signaly o glavnoy tseli, no ne lomat svobodu zahoda.";
                    profile.returnLogic = "Igrok, kotoryy otstupil, dolzhen sohranit pamyat o gate, relief i payoff.";
                    profile.masteryLogic = "Opytnyy igrok stroit sobstvennuyu glubokuyu liniyu, ispolzuya tolko klyuchevye anchor'y.";
                    profile.playerPromise = "Pozdniy zahod dolzhen oschuschatsya kak ekspeditsiya, a ne kak obychnyy krug.";
                    profile.routeMemoryRule = "Igrok dolzhen pomnit posledniy anchor, redkuyu peredyshku i glavnyy gate.";
                    profile.failureMode = "Esli pozdnyaya zona daet slishkom mnogo komforta, ona teryaet tsennost.";
                    break;
                case "profile.combat.mid":
                    profile.entryBeat = "Vhod cherez zametnoe izmenenie ugrozy ili povedeniya vody.";
                    profile.routineBeat = "Igrok otsenivaet kontakt, distantsiyu i variant kontrolya.";
                    profile.reliefBeat = "Peredyshka korotkaya i hrupkaya, tolko chtoby smenit temp.";
                    profile.pressureBeat = "Glavnyy ritm — okna kontrolya, a ne postoyannyy uron.";
                    profile.payoffBeat = "Glavnaya nagrada — proyti mimo ugrozy, vzyat poleznoe i sohranit temp.";
                    profile.exitBeat = "Vyhod luchshe rabotaet po pamyati puti, chem po gonke.";
                    profile.playerFreedomRule = "Igrok mozhet dratsya, obhodit ili prosto chitat ugrozu i uhodit.";
                    profile.softProgressionPull = "Zona dolzhna davat napryazhenie, no ne zapirat v obyazatelnoy drake.";
                    profile.optionalDetourRule = "Dazhe bokovoy obhod dolzhen chemu-to uchit pro ugrozu i prostranstvo.";
                    profile.returnLogic = "Igrok, ushedshiy bez boya, vse ravno dolzhen vynesti poleznuyu informatsiyu.";
                    profile.masteryLogic = "Opytnyy igrok ispolzuet okna kontrolya kak korotkie instrumenty, a ne kak glavnyy rezhim igry.";
                    profile.playerPromise = "Boevaya zona dolzhna proveryat kontrol i reshenie, a ne tupo sedat resurs.";
                    profile.routeMemoryRule = "Igrok dolzhen pomnit odin safe angle i odnu opasnuyu liniyu vhoda.";
                    profile.failureMode = "Esli v boevoy zone net chitaemyh okon, ona prevraschaetsya v shum.";
                    break;
                case "profile.navigation.mid":
                    profile.entryBeat = "Vhod cherez razvilku ili ponyatnyy marshrutnyy yakor.";
                    profile.routineBeat = "Igrok bystro reshaet, kakoy put vedet k chemu.";
                    profile.reliefBeat = "Peredyshka zdes — ne otdyh, a yasnost.";
                    profile.pressureBeat = "Davlenie idet ot riska oshibitsya vetkoy i poteryat pamyat marshruta.";
                    profile.payoffBeat = "Glavnaya nagrada — pravilnaya vetka i horoshaya route memory.";
                    profile.exitBeat = "Vyhod dolzhen byt legche vhoda, esli igrok chital orientiry pravilno.";
                    profile.playerFreedomRule = "Igrok volen brat lyubuyu vetku; zona tolko pomogaet potom ne poteryatsya.";
                    profile.softProgressionPull = "Silneyshiy put dolzhen chitatsya, no ne otmenyat drugie varianty.";
                    profile.optionalDetourRule = "Nevernaya vetka tozhe dolzhna byt osmyslennoy, a ne pustoy oshibkoy.";
                    profile.returnLogic = "Dazhe posle nevernogo vybora igrok dolzhen sumet vosstanovit kartinu marshruta.";
                    profile.masteryLogic = "Opytnyy igrok ispolzuet hab kak kartu v golove i stroit svoy sobstvennyy krug.";
                    profile.playerPromise = "Navigatsionnaya zona dolzhna delat marshrut ponyatnee, a ne zaputannee.";
                    profile.routeMemoryRule = "Igrok dolzhen pomnit vetku, anchor i napravlenie vozvrata.";
                    profile.failureMode = "Esli vetki ne razlichayutsya, navigatsionnaya zona ne vypolnyaet svoyu rabotu.";
                    break;
                case "profile.trial.early":
                default:
                    profile.entryBeat = "Vhod cherez ponyatnuyu startovuyu tochku.";
                    profile.routineBeat = "Bazovyy krug daet igroku glavnyy tip deystviya etoy zony.";
                    profile.reliefBeat = "Peredyshka korotkaya i nuzhna, chtoby schitat sleduyuschiy shag.";
                    profile.pressureBeat = "Dalshe zona nachinaet prosit bolee tochnoe reshenie.";
                    profile.payoffBeat = "Glavnaya nagrada — ponyatnaya polza, a ne sluchaynaya vydacha.";
                    profile.exitBeat = "Vyhod derzhitsya na chitaemom marshrute.";
                    profile.playerFreedomRule = "Igrok svoboden idti kak hochet; loop — eto myagkaya forma, a ne stsenariy.";
                    profile.softProgressionPull = "Mesto dolzhno namekat na luchshiy zahod, ne otbiraya svobodu.";
                    profile.optionalDetourRule = "Dazhe neidealnyy put dolzhen byt soderzhatelnym.";
                    profile.returnLogic = "Korotkiy i dlinnyy zahod oba dolzhny ostavlyat chitaemuyu pamyat o zone.";
                    profile.masteryLogic = "Opytnyy igrok chitaet formu bystree i sam reshaet, skolko riska brat.";
                    profile.playerPromise = "Zona daet ponyatnyy ekspeditsionnyy tsikl.";
                    profile.routeMemoryRule = "Igrok dolzhen pomnit vhod, relief i payoff.";
                    profile.failureMode = "Esli tsikl ne chitaetsya, zona stanovitsya shumom.";
                    break;
            }
        }

#endif
        private static void ApplySandboxAttractionTemplate(WorldSandboxAttractionProfile profile, string zoneProfileId)
        {
            switch (zoneProfileId)
            {
                case "profile.resources.starter":
                    profile.entryRead = "Silnyy orientir prosto govorit: etu vodu stoit proverit.";
                    profile.ambientValue = "Bazovaya tsennost lezhit shiroko i podderzhivaet svobodnye korotkie krugi.";
                    profile.detourValue = "Pobochnye pockets i uzly sidyat chut v storone i nagrazhdayut lyubopytstvo.";
                    profile.shelterRead = "Spokoynyy karman daet vydoh i pomogaet zanovo prochitat prostranstvo.";
                    profile.pressureRead = "Glubzhe voda nachinaet chestno davit, no ne zapiraet igroka.";
                    profile.deepLure = "Chut dalshe rutiny lezhit bolee redkaya prichina risknut.";
                    profile.storyLure = "Sreda namekaet, chto glubzhe nachinaetsya chto-to bolee vazhnoe, chem prostoy farm.";
                    profile.returnValue = "Posle pervogo znakomstva syuda vygodno vozvraschatsya uzhe umnee.";
                    profile.freedomRule = "Igrok volen kruzhit, brat malye krugi, uhodit ranshe ili nyryat glubzhe po svoemu zhelaniyu.";
                    profile.curiosityRule = "Lyuboy bokovoy zahod dolzhen davat malenkiy, no vnyatnyy smysl.";
                    profile.crosslinkRule = "Sosednie pockets dolzhny peresekatsya tak, chtoby igrok stroil marshrut v golove sam.";
                    profile.reentryRule = "Posle pervogo znakomstva povtornyy zahod dolzhen oschuschatsya prosche i bystree.";
                    profile.masteryRule = "Masterstvo zdes — znat korotkie resursoemkie krugi bez lishnego bluzhdaniya.";
                    profile.playerPromise = "Ponyatnaya startovaya voda s chestnym farmom i myagkim namekom na glubinu.";
                    profile.memoryRule = "Pamyat derzhitsya na arkah, stupenyah, pyatnah sveta i odnom horoshem orientire.";
                    profile.dangerRule = "Opasnost rastet po mere udaleniya ot chitaemoy vody i route anchor.";
                    break;

                case "profile.power.mid":
                    profile.entryRead = "Vidimyy istochnik ili relay-liniya srazu tseplyayut vzglyad.";
                    profile.ambientValue = "Obychnaya tsennost sidit na chitaemoy svyazke istochnik -> peredacha -> nagruzka.";
                    profile.detourValue = "Bokovye power i service pockets pomogayut ponyat sistemu glubzhe.";
                    profile.shelterRead = "Peredyshka tut — eto yasnost shemy, a ne polnaya bezopasnost.";
                    profile.pressureRead = "Razryv linii i plohaya voda otmechayut uchastok s bolee vysokim riskom.";
                    profile.deepLure = "Dalshe manit vosstanovlenie vazhnoy linii ili dostup k bolee silnoy tochke pitaniya.";
                    profile.storyLure = "Energeticheskiy kontur namekaet, kak zhila ili lomalas eta infrastruktura.";
                    profile.returnValue = "Povtornyy zahod stanovitsya vygodnee, kogda igrok uzhe vidit shemu tselikom.";
                    profile.freedomRule = "Igrok ne obyazan chinit ves power-kontur za odin zahod.";
                    profile.curiosityRule = "Chasti linii dolzhny byt polezny dazhe po otdelnosti.";
                    profile.crosslinkRule = "Releynye tochki dolzhny svyazyvat sosednie pockets i marshruty.";
                    profile.reentryRule = "Povtornyy zahod dolzhen byt koroche blagodarya ponimaniyu vsey linii.";
                    profile.masteryRule = "Masterstvo — videt shemu tselikom i rezat put k nuzhnoy probleme.";
                    profile.playerPromise = "Tehnicheskaya zona, gde sistema chitaetsya kak sistema, a ne kak shum iz obektov.";
                    profile.memoryRule = "Pamyat derzhitsya na istochnike, relay-tsepochke i odnoy problemnoy tochke.";
                    profile.dangerRule = "Davlenie rastet tam, gde liniya uhodit v tyazheluyu vodu ili nachinaet lomatsya.";
                    break;

                case "profile.progression.endgame":
                    profile.entryRead = "Posledniy nadezhnyy orientir govorit: dalshe voda uzhe sereznee.";
                    profile.ambientValue = "Obychnoy tsennosti malo, i dazhe melkie nahodki namekayut na dorogoy glubokiy smysl.";
                    profile.detourValue = "Bokovye karmany dayut signaly o glubokoy tseli, no ne vedut igroka za ruku.";
                    profile.shelterRead = "Redkiy spokoynyy karman nuzhen, chtoby reshitsya na novyy push, a ne rasslabitsya.";
                    profile.pressureRead = "Risk narastaet stupenyami i zaranee chitaetsya po srede.";
                    profile.deepLure = "Glubzhe manit dorogaya late-game nahodka, radi kotoroy i zatevaetsya zahod.";
                    profile.storyLure = "Sreda obeschaet istoriyu, taynu i oschuschenie po-nastoyaschemu vazhnogo mesta.";
                    profile.returnValue = "Dazhe othod ostavlyaet tsennost: pamyat, orientiry, redkuyu dobychu i novyy plan.";
                    profile.freedomRule = "Igrok mozhet ne brat late-game tsel srazu i vernutsya pozzhe s drugim planom.";
                    profile.curiosityRule = "Pobochnye zahody dolzhny podkarmlivat lyubopytstvo, a ne lomat svobodu.";
                    profile.crosslinkRule = "Sosednie pockets i anchors dolzhny pozvolyat stroit svoy glubokiy marshrut.";
                    profile.reentryRule = "Dazhe otstuplenie dolzhno ostavlyat polzu: pamyat, orientiry, redkuyu dobychu.";
                    profile.masteryRule = "Masterstvo — sobirat svoy derzkiy marshrut iz neskolkih klyuchevyh anchor'ov.";
                    profile.playerPromise = "Glubokaya ekspeditsiya s redkoy tsennostyu, silnoy pamyatyu mesta i chestnym riskom.";
                    profile.memoryRule = "Pamyat derzhitsya na poslednem anchor, redkoy peredyshke i silnom preduprezhdenii o riske.";
                    profile.dangerRule = "Risk narastaet stupenyami: redkaya peredyshka, rezkiy rost davleniya, zatem dorogoy push.";
                    break;

                default:
                    profile.entryRead = "Vhodnoy orientir dolzhen myagko priglashat vnutr zony.";
                    profile.ambientValue = "Obychnaya tsennost dolzhna sidet ryadom s chitaemym prostranstvom.";
                    profile.detourValue = "Pobochnaya tsennost dolzhna pooschryat svobodnye otkloneniya.";
                    profile.shelterRead = "Peredyshka nuzhna, chtoby igrok ne teryal kartinu zony.";
                    profile.pressureRead = "Davlenie dolzhno chitatsya kak perehod k bolee sereznoy vode.";
                    profile.deepLure = "Bolee redkaya tsennost dolzhna lezhat glubzhe rutiny.";
                    profile.storyLure = "Zona dolzhna obeschat ne tolko resursy, no i smysl mesta.";
                    profile.returnValue = "Nazad igrok dolzhen idti po pamyati formy mira.";
                    profile.freedomRule = "Igrok svoboden narushat idealnyy marshrut i stroit svoy.";
                    profile.curiosityRule = "Lyuboy detour dolzhen nesti malenkiy, no chestnyy smysl.";
                    profile.crosslinkRule = "Sosednie pockets i anchors dolzhny obrazovyvat set, a ne trubu.";
                    profile.reentryRule = "Povtornyy zahod dolzhen oschuschatsya bystree i uverennee.";
                    profile.masteryRule = "Masterstvo — stroit svoy marshrut po pamyati i risku.";
                    profile.playerPromise = "Svobodnaya podvodnaya ekspeditsiya s chitaemym prostranstvom.";
                    profile.memoryRule = "Igrok dolzhen pomnit prostranstvo po formam i orientiru, a ne po UI.";
                    profile.dangerRule = "Risk dolzhen rasti myagko i chitaemo.";
                    break;
            }
        }

        private static WorldPrefabFamilyProfile InferSpatialRoleFamilyProfile(string zoneProfileId, string roleId)
        {
            switch (zoneProfileId)
            {
                case "profile.resources.starter":
                    return EnsurePrefabFamilyProfile(roleId switch
                    {
                        "resource_pocket" => "resource.pocket.readable",
                        "node_cluster" => "resource.node.cluster",
                        "safe_pocket" => "safe.pocket.reef",
                        "route_anchor" => "navigation.anchor.reef",
                        "rare_objective" => "resource.rare.pocket",
                        _ => "resources.landmarks.far"
                    });

                case "profile.fabrication.early":
                    return EnsurePrefabFamilyProfile(roleId switch
                    {
                        "safe_pocket" => "safe.outpost.support",
                        "route_anchor" => "navigation.anchor.outpost",
                        "rare_objective" => "fabrication.landmark.utility",
                        _ => "fabrication.outpost.mid"
                    });

                case "profile.trial.early":
                    return EnsurePrefabFamilyProfile(roleId switch
                    {
                        "resource_pocket" => "trial.pocket.readable",
                        "node_cluster" => "trial.node.cluster",
                        "safe_pocket" => "trial.safe.pocket",
                        "build_socket" => "trial.build.socket",
                        "power_spine" => "trial.power.spine",
                        "service_choke" => "trial.service.choke",
                        "route_anchor" => "trial.route.anchor",
                        "hazard_gate" => "trial.hazard.gate",
                        "rare_objective" => "trial.rare.objective",
                        _ => "trial.readability.far"
                    });

                case "profile.construction.mid":
                    return EnsurePrefabFamilyProfile(roleId switch
                    {
                        "build_socket" => "construction.socket.support",
                        "safe_pocket" => "construction.safe.ledge",
                        "route_anchor" => "construction.route.frame",
                        "rare_objective" => "construction.landmark.spine",
                        _ => "construction.spine.far"
                    });

                case "profile.power.mid":
                    return EnsurePrefabFamilyProfile(roleId switch
                    {
                        "power_spine" => "power.spine.chain",
                        "service_choke" => "power.service.junction",
                        "route_anchor" => "power.route.anchor",
                        "rare_objective" => "power.landmark.core",
                        _ => "power.route.far"
                    });

                case "profile.progression.endgame":
                    return EnsurePrefabFamilyProfile(roleId switch
                    {
                        "safe_pocket" => "progression.safe.pocket",
                        "service_choke" => "progression.service.choke",
                        "route_anchor" => "progression.route.anchor",
                        "hazard_gate" => "progression.hazard.gate",
                        "rare_objective" => "progression.rare.objective",
                        _ => "progression.route.landmark"
                    });

                case "profile.combat.mid":
                    return EnsurePrefabFamilyProfile(roleId switch
                    {
                        "safe_pocket" => "combat.safe.cover",
                        "route_anchor" => "combat.route.anchor",
                        "hazard_gate" => "combat.threat.gate",
                        "rare_objective" => "combat.landmark.threat",
                        _ => "combat.silhouette.far"
                    });

                case "profile.navigation.mid":
                    return EnsurePrefabFamilyProfile(roleId switch
                    {
                        "safe_pocket" => "navigation.safe.ledge",
                        "route_anchor" => "navigation.anchor.readable",
                        "rare_objective" => "navigation.frontier.landmark",
                        _ => "navigation.silhouette.far"
                    });
            }

            return EnsurePrefabFamilyProfile("world.generic.role");
        }

        private static WorldZonePlanProfile.SpatialRelation InferSpatialRoleRelation(string zoneProfileId, string roleId)
        {
            return zoneProfileId switch
            {
                "profile.resources.starter" => roleId switch
                {
                    "resource_pocket" => WorldZonePlanProfile.SpatialRelation.NearRouteAnchor,
                    "node_cluster" => WorldZonePlanProfile.SpatialRelation.OffMainRoute,
                    "safe_pocket" => WorldZonePlanProfile.SpatialRelation.BehindCover,
                    "route_anchor" => WorldZonePlanProfile.SpatialRelation.AlongMainRoute,
                    "rare_objective" => WorldZonePlanProfile.SpatialRelation.AtRouteTerminus,
                    _ => WorldZonePlanProfile.SpatialRelation.OffMainRoute
                },
                "profile.fabrication.early" => roleId switch
                {
                    "safe_pocket" => WorldZonePlanProfile.SpatialRelation.AroundHeroObject,
                    "route_anchor" => WorldZonePlanProfile.SpatialRelation.NearRouteAnchor,
                    "rare_objective" => WorldZonePlanProfile.SpatialRelation.AtRouteTerminus,
                    _ => WorldZonePlanProfile.SpatialRelation.AroundHeroObject
                },
                "profile.trial.early" => roleId switch
                {
                    "build_socket" => WorldZonePlanProfile.SpatialRelation.AtBranchPoint,
                    "power_spine" => WorldZonePlanProfile.SpatialRelation.AlongMainRoute,
                    "service_choke" => WorldZonePlanProfile.SpatialRelation.BehindHazardGate,
                    "route_anchor" => WorldZonePlanProfile.SpatialRelation.NearRouteAnchor,
                    "hazard_gate" => WorldZonePlanProfile.SpatialRelation.AtBranchPoint,
                    "rare_objective" => WorldZonePlanProfile.SpatialRelation.AtRouteTerminus,
                    _ => WorldZonePlanProfile.SpatialRelation.OffMainRoute
                },
                "profile.construction.mid" => roleId switch
                {
                    "build_socket" => WorldZonePlanProfile.SpatialRelation.AtBranchPoint,
                    "safe_pocket" => WorldZonePlanProfile.SpatialRelation.BehindCover,
                    "route_anchor" => WorldZonePlanProfile.SpatialRelation.AlongMainRoute,
                    "rare_objective" => WorldZonePlanProfile.SpatialRelation.AtRouteTerminus,
                    _ => WorldZonePlanProfile.SpatialRelation.OffMainRoute
                },
                "profile.power.mid" => roleId switch
                {
                    "power_spine" => WorldZonePlanProfile.SpatialRelation.AlongMainRoute,
                    "service_choke" => WorldZonePlanProfile.SpatialRelation.AtBranchPoint,
                    "route_anchor" => WorldZonePlanProfile.SpatialRelation.NearRouteAnchor,
                    "rare_objective" => WorldZonePlanProfile.SpatialRelation.AtRouteTerminus,
                    _ => WorldZonePlanProfile.SpatialRelation.OffMainRoute
                },
                "profile.progression.endgame" => roleId switch
                {
                    "safe_pocket" => WorldZonePlanProfile.SpatialRelation.BehindCover,
                    "service_choke" => WorldZonePlanProfile.SpatialRelation.AtBranchPoint,
                    "route_anchor" => WorldZonePlanProfile.SpatialRelation.AlongMainRoute,
                    "hazard_gate" => WorldZonePlanProfile.SpatialRelation.BehindHazardGate,
                    "rare_objective" => WorldZonePlanProfile.SpatialRelation.AtRouteTerminus,
                    _ => WorldZonePlanProfile.SpatialRelation.OffMainRoute
                },
                "profile.combat.mid" => roleId switch
                {
                    "safe_pocket" => WorldZonePlanProfile.SpatialRelation.BehindCover,
                    "route_anchor" => WorldZonePlanProfile.SpatialRelation.NearRouteAnchor,
                    "hazard_gate" => WorldZonePlanProfile.SpatialRelation.BehindHazardGate,
                    "rare_objective" => WorldZonePlanProfile.SpatialRelation.AtRouteTerminus,
                    _ => WorldZonePlanProfile.SpatialRelation.OffMainRoute
                },
                "profile.navigation.mid" => roleId switch
                {
                    "safe_pocket" => WorldZonePlanProfile.SpatialRelation.BehindCover,
                    "route_anchor" => WorldZonePlanProfile.SpatialRelation.AlongMainRoute,
                    "rare_objective" => WorldZonePlanProfile.SpatialRelation.AtRouteTerminus,
                    _ => WorldZonePlanProfile.SpatialRelation.OffMainRoute
                },
                _ => WorldZonePlanProfile.SpatialRelation.OffMainRoute
            };
        }

        private static WorldSliceAnchor.SliceState InferSpatialRoleSlice(string zoneProfileId, string roleId)
        {
            return roleId switch
            {
                "resource_pocket" => WorldSliceAnchor.SliceState.Near,
                "node_cluster" => WorldSliceAnchor.SliceState.Near,
                "safe_pocket" => WorldSliceAnchor.SliceState.Near,
                "build_socket" => WorldSliceAnchor.SliceState.Near,
                "power_spine" => WorldSliceAnchor.SliceState.Mid,
                "service_choke" => WorldSliceAnchor.SliceState.Near,
                "route_anchor" => WorldSliceAnchor.SliceState.Mid,
                "hazard_gate" => zoneProfileId == "profile.progression.endgame" || zoneProfileId == "profile.combat.mid"
                    ? WorldSliceAnchor.SliceState.Mid
                    : WorldSliceAnchor.SliceState.Near,
                "rare_objective" => WorldSliceAnchor.SliceState.Mid,
                _ => WorldSliceAnchor.SliceState.Mid
            };
        }

        private static int InferSpatialRoleCount(string zoneProfileId, string roleId)
        {
            return zoneProfileId switch
            {
                "profile.resources.starter" => roleId switch
                {
                    "resource_pocket" => 3,
                    "node_cluster" => 2,
                    "safe_pocket" => 2,
                    "route_anchor" => 2,
                    "rare_objective" => 1,
                    _ => 0
                },
                "profile.fabrication.early" => roleId switch
                {
                    "safe_pocket" => 1,
                    "route_anchor" => 1,
                    "rare_objective" => 1,
                    _ => 0
                },
                "profile.trial.early" => roleId switch
                {
                    "resource_pocket" => 1,
                    "node_cluster" => 1,
                    "safe_pocket" => 1,
                    "build_socket" => 1,
                    "power_spine" => 1,
                    "service_choke" => 1,
                    "route_anchor" => 2,
                    "hazard_gate" => 1,
                    "rare_objective" => 1,
                    _ => 0
                },
                "profile.construction.mid" => roleId switch
                {
                    "build_socket" => 2,
                    "safe_pocket" => 1,
                    "route_anchor" => 2,
                    "rare_objective" => 1,
                    _ => 0
                },
                "profile.power.mid" => roleId switch
                {
                    "power_spine" => 2,
                    "service_choke" => 1,
                    "route_anchor" => 2,
                    "rare_objective" => 1,
                    _ => 0
                },
                "profile.progression.endgame" => roleId switch
                {
                    "safe_pocket" => 1,
                    "service_choke" => 1,
                    "route_anchor" => 2,
                    "hazard_gate" => 1,
                    "rare_objective" => 1,
                    _ => 0
                },
                "profile.combat.mid" => roleId switch
                {
                    "safe_pocket" => 1,
                    "route_anchor" => 1,
                    "hazard_gate" => 1,
                    "rare_objective" => 1,
                    _ => 0
                },
                "profile.navigation.mid" => roleId switch
                {
                    "safe_pocket" => 1,
                    "route_anchor" => 3,
                    "rare_objective" => 1,
                    _ => 0
                },
                _ => 0
            };
        }

        private static string BuildSpatialRoleUsage(string zoneProfileId, string roleId)
        {
            return zoneProfileId switch
            {
                "profile.resources.starter" => roleId switch
                {
                    "resource_pocket" => "Small readable loose-resource pocket close to a stable route line.",
                    "node_cluster" => "A slightly deeper mineral cluster that asks for a small detour.",
                    "safe_pocket" => "Short recovery nook behind stone cover or fossil folds.",
                    "route_anchor" => "A strong readable form that keeps beginner routes stable.",
                    "rare_objective" => "The best find of the pocket, one layer deeper than routine scrap.",
                    _ => "Not a major role for this zone."
                },
                "profile.fabrication.early" => roleId switch
                {
                    "safe_pocket" => "Controlled regroup and craft stop around the outpost.",
                    "route_anchor" => "Approach marker that brings the player back to route clarity.",
                    "rare_objective" => "The memorable utility landmark that makes the stop worth revisiting.",
                    _ => "Not a major role for this zone."
                },
                "profile.trial.early" => roleId switch
                {
                    "resource_pocket" => "Simple readable reward near a practice route.",
                    "node_cluster" => "Compact extractable cluster for tool testing.",
                    "safe_pocket" => "Brief reset space between lanes.",
                    "build_socket" => "Obvious construction test point.",
                    "power_spine" => "Linear power-support read across a lane.",
                    "service_choke" => "A service problem that intentionally blocks smooth forward flow.",
                    "route_anchor" => "Clear lane anchor for route memory.",
                    "hazard_gate" => "A gate that tells the player risk begins here.",
                    "rare_objective" => "The endpoint that justifies finishing a lane.",
                    _ => "Not a major role for this zone."
                },
                "profile.construction.mid" => roleId switch
                {
                    "build_socket" => "Main place where the route wants construction to happen.",
                    "safe_pocket" => "Small calm space to read placement before committing.",
                    "route_anchor" => "Frame or support shape that keeps the build route legible.",
                    "rare_objective" => "The distant structural payoff that makes the route memorable.",
                    _ => "Not a major role for this zone."
                },
                "profile.power.mid" => roleId switch
                {
                    "power_spine" => "Main energy line through the zone.",
                    "service_choke" => "A junction where power and maintenance pressure meet.",
                    "route_anchor" => "A readable relay point that chains the route.",
                    "rare_objective" => "The major powered landmark at the end of the line.",
                    _ => "Not a major role for this zone."
                },
                "profile.progression.endgame" => roleId switch
                {
                    "safe_pocket" => "A rare breathing point before another hard push.",
                    "service_choke" => "A maintenance problem that reinforces route pressure.",
                    "route_anchor" => "The last trustworthy anchor before escalation.",
                    "hazard_gate" => "The clear threshold into expensive late-game risk.",
                    "rare_objective" => "The major pull that makes the dangerous route worth taking.",
                    _ => "Not a major role for this zone."
                },
                "profile.combat.mid" => roleId switch
                {
                    "safe_pocket" => "A small break in sightlines where the player can recover.",
                    "route_anchor" => "A stable combat-read form that prevents total chaos.",
                    "hazard_gate" => "The point where control space ends and danger starts.",
                    "rare_objective" => "The focal point that makes the threat pocket memorable.",
                    _ => "Not a major role for this zone."
                },
                "profile.navigation.mid" => roleId switch
                {
                    "safe_pocket" => "A brief recovery ledge near a branch.",
                    "route_anchor" => "A major route-memory form for branch choice and return flow.",
                    "rare_objective" => "The frontier landmark that rewards pushing one branch further.",
                    _ => "Not a major role for this zone."
                },
                _ => "Generic role plan."
            };
        }

        private static string BuildFamilyLabel(string familyId)
        {
            string[] parts = familyId.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length <= 0)
                    continue;

                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            }

            return string.Join(" ", parts);
        }

        private static WorldSliceAnchor.SliceState InferFamilyFidelity(string familyId)
        {
            if (familyId.Contains(".far"))
                return WorldSliceAnchor.SliceState.Far;

            if (familyId.Contains(".mid"))
                return WorldSliceAnchor.SliceState.Mid;

            return WorldSliceAnchor.SliceState.Near;
        }

        private static WorldPrefabFamilyProfile.BudgetClass InferFamilyBudget(string familyId)
        {
            if (familyId.Contains("setpieces") || familyId.Contains("landmark") || familyId.Contains("outpost"))
                return WorldPrefabFamilyProfile.BudgetClass.Heavy;

            if (familyId.Contains("silhouette") || familyId.Contains("markers") || familyId.Contains("clutter"))
                return WorldPrefabFamilyProfile.BudgetClass.Light;

            return WorldPrefabFamilyProfile.BudgetClass.Medium;
        }

        private static bool InferFamilyInteraction(string familyId)
        {
            return familyId.Contains(".near")
                || familyId.Contains("pickup")
                || familyId.Contains("usable")
                || familyId.Contains("socket")
                || familyId.Contains("device")
                || familyId.Contains("target");
        }

        private static bool InferFamilyCollision(string familyId, bool expectsInteraction)
        {
            if (expectsInteraction)
                return true;

            return familyId.Contains("route")
                || familyId.Contains("network")
                || familyId.Contains("frames")
                || familyId.Contains("outpost");
        }

        private static void AssignContentChildrenToRoots(SerializedProperty arrayProperty, Transform parent)
        {
            if (arrayProperty == null)
                return;

            List<GameObject> contentChildren = new List<GameObject>(parent.childCount);
            for (int i = 0; i < parent.childCount; i++)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (child.name == NearHolderName || child.name == MidHolderName || child.name == FarHolderName)
                    continue;

                contentChildren.Add(child);
            }

            arrayProperty.arraySize = contentChildren.Count;
            for (int i = 0; i < contentChildren.Count; i++)
                arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue = contentChildren[i];
        }

        private static void AssignSingleRoot(SerializedProperty arrayProperty, GameObject root)
        {
            if (arrayProperty == null)
                return;

            arrayProperty.arraySize = root != null ? 1 : 0;
            if (root != null)
                arrayProperty.GetArrayElementAtIndex(0).objectReferenceValue = root;
        }

        private static void ClearObjectArray(SerializedProperty arrayProperty)
        {
            if (arrayProperty != null)
                arrayProperty.arraySize = 0;
        }

        private static void ClearBehaviourArray(SerializedProperty arrayProperty)
        {
            if (arrayProperty != null)
                arrayProperty.arraySize = 0;
        }

        private readonly struct ZoneFidelityHolders
        {
            public readonly GameObject near;
            public readonly GameObject mid;
            public readonly GameObject far;

            public ZoneFidelityHolders(GameObject near, GameObject mid, GameObject far)
            {
                this.near = near;
                this.mid = mid;
                this.far = far;
            }
        }

        private static ZoneFidelityHolders EnsureZoneFidelityHolders(Transform root)
        {
            GameObject near = EnsureChild(root, NearHolderName);
            GameObject mid = EnsureChild(root, MidHolderName);
            GameObject far = EnsureChild(root, FarHolderName);
            ConfigureHolderFidelity(near, WorldSliceAnchor.SliceState.Near, WorldSliceAnchor.SliceState.Near, WorldSliceAnchor.SliceState.Near, WorldSliceAnchor.SliceState.Near, WorldSliceAnchor.SliceState.Near);
            ConfigureHolderFidelity(mid, WorldSliceAnchor.SliceState.Mid, WorldSliceAnchor.SliceState.Near, WorldSliceAnchor.SliceState.Near, WorldSliceAnchor.SliceState.Near, WorldSliceAnchor.SliceState.Mid);
            ConfigureHolderFidelity(far, WorldSliceAnchor.SliceState.Far, WorldSliceAnchor.SliceState.Near, WorldSliceAnchor.SliceState.Near, WorldSliceAnchor.SliceState.Near, WorldSliceAnchor.SliceState.Mid);
            return new ZoneFidelityHolders(near, mid, far);
        }

        private static GameObject EnsureChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
                return existing.gameObject;

            GameObject child = new GameObject(childName);
            child.transform.SetParent(parent);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child;
        }

        private static void ConfigureHolderFidelity(
            GameObject root,
            WorldSliceAnchor.SliceState visibleFromState,
            WorldSliceAnchor.SliceState collidersFromState,
            WorldSliceAnchor.SliceState behavioursFromState,
            WorldSliceAnchor.SliceState physicsFromState,
            WorldSliceAnchor.SliceState fullShadowsFromState)
        {
            if (root == null)
                return;

            WorldFidelityRoot fidelityRoot = GetOrAddComponent<WorldFidelityRoot>(root);
            SerializedObject so = new SerializedObject(fidelityRoot);
            so.FindProperty("visibleFromState").enumValueIndex = (int)visibleFromState;
            so.FindProperty("collidersFromState").enumValueIndex = (int)collidersFromState;
            so.FindProperty("behavioursFromState").enumValueIndex = (int)behavioursFromState;
            so.FindProperty("physicsFromState").enumValueIndex = (int)physicsFromState;
            so.FindProperty("fullShadowsFromState").enumValueIndex = (int)fullShadowsFromState;
            so.FindProperty("autoCollectChildren").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fidelityRoot);
        }

        private static void EnsureWarmupPreset(ObjectPoolManager objectPoolManager, GameObject prefab, int count)
        {
            SerializedObject so = new SerializedObject(objectPoolManager);
            SerializedProperty presets = so.FindProperty("warmupPresets");
            if (presets == null)
                return;

            for (int i = 0; i < presets.arraySize; i++)
            {
                SerializedProperty entry = presets.GetArrayElementAtIndex(i);
                SerializedProperty prefabProp = entry.FindPropertyRelative("prefab");
                SerializedProperty countProp = entry.FindPropertyRelative("count");
                if (prefabProp == null || countProp == null)
                    continue;

                if (prefabProp.objectReferenceValue == prefab)
                {
                    countProp.intValue = Mathf.Max(countProp.intValue, count);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(objectPoolManager);
                    return;
                }
            }

            int newIndex = presets.arraySize;
            presets.InsertArrayElementAtIndex(newIndex);
            SerializedProperty newEntry = presets.GetArrayElementAtIndex(newIndex);
            newEntry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            newEntry.FindPropertyRelative("count").intValue = count;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(objectPoolManager);
        }

        private static void EnsureRelayHudMarker()
        {
            SuitHUDV4CanvasOverlay overlay = FindSceneObjectIncludingInactive<SuitHUDV4CanvasOverlay>();
            if (overlay == null)
                return;

            RectTransform parent = ResolveRelayMarkerParent(overlay.transform);
            RelayHUDElement marker = overlay.GetComponentInChildren<RelayHUDElement>(true);
            if (marker != null && marker.transform.parent != parent)
                marker.transform.SetParent(parent, false);

            if (marker != null)
                return;

            if (parent == null)
                return;

            CreateRelayMarker(parent);
            EditorUtility.SetDirty(overlay.gameObject);
        }

        private static RelayHUDElement CreateRelayMarker(RectTransform parent)
        {
            GameObject markerRoot = new GameObject(
                RelayRouteMarkerName,
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image),
                typeof(RelayHUDElement));
            markerRoot.transform.SetParent(parent, false);

            RectTransform rootRect = markerRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(260f, 72f);

            CanvasGroup canvasGroup = markerRoot.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            Image background = markerRoot.GetComponent<Image>();
            background.color = new Color(0.02f, 0.08f, 0.12f, 0.18f);
            background.raycastTarget = false;

            Image markerIcon = CreateRelayMarkerIcon(markerRoot.transform);
            TMP_Text labelText = CreateRelayText(markerRoot.transform, "Label", new Vector2(200f, 28f), new Vector2(16f, 12f), 20f, new Color(0.72f, 0.92f, 1f, 0.96f));
            TMP_Text distanceText = CreateRelayText(markerRoot.transform, "Distance", new Vector2(160f, 24f), new Vector2(16f, -14f), 16f, new Color(0.52f, 0.82f, 0.96f, 0.9f));

            labelText.text = "EMERGENCY SERVICE RELAY";
            distanceText.text = "0M";

            RelayHUDElement marker = markerRoot.GetComponent<RelayHUDElement>();
            marker.ConfigureRuntimeBindings(markerIcon, distanceText, labelText);
            return marker;
        }

        private static RectTransform ResolveRelayMarkerParent(Transform overlayTransform)
        {
            if (overlayTransform == null)
                return null;

            RectTransform markerLayer = overlayTransform.Find(RelayMarkerLayerName) as RectTransform;
            if (markerLayer != null)
                return markerLayer;

            RectTransform overlayRect = overlayTransform as RectTransform;
            if (overlayRect == null)
                return null;

            markerLayer = CreateRelayMarkerLayer(overlayRect);

            RectTransform legacyRoot = overlayTransform.Find(HudRootName) as RectTransform;
            if (legacyRoot != null)
            {
                RelayHUDElement legacyMarker = legacyRoot.GetComponentInChildren<RelayHUDElement>(true);
                if (legacyMarker != null)
                    legacyMarker.transform.SetParent(markerLayer, false);
            }

            return markerLayer;
        }

        private static RectTransform CreateRelayMarkerLayer(RectTransform overlayRect)
        {
            GameObject markerLayerObject = new GameObject(RelayMarkerLayerName, typeof(RectTransform));
            markerLayerObject.transform.SetParent(overlayRect, false);

            RectTransform markerLayer = markerLayerObject.GetComponent<RectTransform>();
            markerLayer.anchorMin = Vector2.zero;
            markerLayer.anchorMax = Vector2.one;
            markerLayer.offsetMin = Vector2.zero;
            markerLayer.offsetMax = Vector2.zero;
            markerLayer.anchoredPosition = Vector2.zero;
            markerLayer.localScale = Vector3.one;
            markerLayer.SetAsLastSibling();
            return markerLayer;
        }

        private static Image CreateRelayMarkerIcon(Transform parent)
        {
            GameObject iconObject = CreateRelayChild(parent, "MarkerIcon", new Vector2(18f, 18f), new Vector2(-96f, 0f));
            iconObject.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image image = iconObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.86f, 1f, 0.95f);
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateRelayText(
            Transform parent,
            string name,
            Vector2 size,
            Vector2 anchoredPosition,
            float fontSize,
            Color color)
        {
            GameObject textObject = CreateRelayChild(parent, name, size, anchoredPosition);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = ResolveRelayFont(parent);
            text.fontSize = fontSize;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            return text;
        }

        private static GameObject CreateRelayChild(Transform parent, string name, Vector2 size, Vector2 anchoredPosition)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);

            RectTransform rect = child.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            return child;
        }

        private static TMP_FontAsset ResolveRelayFont(Transform parent)
        {
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font != null)
                return font;

            TextMeshProUGUI existingText = parent.GetComponentInChildren<TextMeshProUGUI>(true);
            return existingText != null ? existingText.font : null;
        }

        private static GameObject CreateOrUpdateColliderProxyPrefab()
        {
            GameObject root = new GameObject("PFB_ProximityColliderProxy");
            root.layer = 0;
            root.tag = "Untagged";

            BoxCollider boxCollider = root.AddComponent<BoxCollider>();
            boxCollider.center = new Vector3(0f, 0.15f, 0f);
            boxCollider.size = new Vector3(2.8f, 2.4f, 2.8f);
            boxCollider.isTrigger = false;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ColliderProxyPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            if (!gameObject.TryGetComponent(out T component))
                component = gameObject.AddComponent<T>();

            return component;
        }

        private static Component GetOrAddOptionalComponent(GameObject gameObject, string componentTypeName)
        {
            Type componentType = Type.GetType(componentTypeName, false);
            if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
                return null;

            Component component = gameObject.GetComponent(componentType);
            if (component == null)
                component = gameObject.AddComponent(componentType);

            return component;
        }

        private static T FindSceneObjectIncludingInactive<T>() where T : Component
        {
            T[] candidates = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < candidates.Length; i++)
            {
                T candidate = candidates[i];
                if (candidate == null)
                    continue;

                GameObject go = candidate.gameObject;
                if (go == null || !go.scene.IsValid())
                    continue;

                return candidate;
            }

            return null;
        }

        private static List<T> LoadAssets<T>(string folderPath) where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folderPath });
            List<T> assets = new List<T>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (asset != null)
                    assets.Add(asset);
            }

            return assets;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] split = folderPath.Split('/');
            string current = split[0];
            for (int i = 1; i < split.Length; i++)
            {
                string next = current + "/" + split[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, split[i]);

                current = next;
            }
        }
    }
}
