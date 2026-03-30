using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.World;
using Hecton8.Dev;
using Hecton8.Environment;
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools
{
    public static class WorldRuntimeBootstrapAuthoring
    {
        private const string RuntimePrefabFolder = "Assets/_Project/Prefabs/WorldRuntime";
        private const string ColliderProxyPrefabPath = RuntimePrefabFolder + "/PFB_ProximityColliderProxy.prefab";
        private const string WorldProfileFolder = "Assets/_Project/Data/World/ZoneProfiles";
        private const string WorldZonePlanFolder = "Assets/_Project/Data/World/ZonePlans";
        private const string WorldContentProfileFolder = "Assets/_Project/Data/World/ContentProfiles";
        private const string WorldPopulationRuleFolder = "Assets/_Project/Data/World/PopulationRules";
        private const string WorldFamilyProfileFolder = "Assets/_Project/Data/World/FamilyProfiles";
        private const string BiomeFamilyProfileFolder = "Assets/_Project/Data/Biomes/FamilyProfiles";
        private const string BiomeMatrixCatalogPath = "Assets/_Project/Data/Biomes/BiomeMatrixCatalog.asset";
        private const string ManagersRootName = "[MANAGERS]";
        private const string NearHolderName = "__NearInteractive";
        private const string MidHolderName = "__MidVisual";
        private const string FarHolderName = "__FarSilhouette";

        [MenuItem("Hecton/Authoring/Rebuild World Runtime Stack", priority = 177)]
        public static void RebuildWorldRuntimeStack()
        {
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder(RuntimePrefabFolder);
            EnsureFolder("Assets/_Project/Data");
            EnsureFolder("Assets/_Project/Data/World");
            EnsureFolder(WorldProfileFolder);
            EnsureFolder(WorldZonePlanFolder);
            EnsureFolder(WorldContentProfileFolder);
            EnsureFolder(WorldPopulationRuleFolder);
            EnsureFolder(WorldFamilyProfileFolder);

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

            GameObject managersRoot = GameObject.Find(ManagersRootName);
            if (managersRoot == null)
                managersRoot = new GameObject(ManagersRootName);

            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
                player = GameObject.Find("Player");

            Transform playerTransform = player != null ? player.transform : null;
            Rigidbody playerBody = player != null ? player.GetComponent<Rigidbody>() : null;

            MapMagicBridge bridge = FindSceneObjectIncludingInactive<MapMagicBridge>();
            ScavengePopulator scavengePopulator = FindSceneObjectIncludingInactive<ScavengePopulator>();
            ObjectPoolManager objectPoolManager = FindSceneObjectIncludingInactive<ObjectPoolManager>();

            BiomeSamplerCache biomeCache = GetOrAddComponent<BiomeSamplerCache>(managersRoot);
            ScatterBudgetController scatterBudgetController = GetOrAddComponent<ScatterBudgetController>(managersRoot);
            WorldStreamingDirector streamingDirector = GetOrAddComponent<WorldStreamingDirector>(managersRoot);
            WorldSliceDirector sliceDirector = GetOrAddComponent<WorldSliceDirector>(managersRoot);
            WorldInterestDirector interestDirector = GetOrAddComponent<WorldInterestDirector>(managersRoot);
            WorldZoneDirector zoneDirector = GetOrAddComponent<WorldZoneDirector>(managersRoot);
            WorldContentDirector contentDirector = GetOrAddComponent<WorldContentDirector>(managersRoot);
            WorldPopulationDirector populationDirector = GetOrAddComponent<WorldPopulationDirector>(managersRoot);
            BiomeMatrixDirector biomeMatrixDirector = GetOrAddComponent<BiomeMatrixDirector>(managersRoot);
            ProximityColliderSystem proximityColliderSystem = GetOrAddComponent<ProximityColliderSystem>(managersRoot);
            HectonBiomeMatrixCatalog biomeMatrixCatalog = AssetDatabase.LoadAssetAtPath<HectonBiomeMatrixCatalog>(BiomeMatrixCatalogPath);

            ConfigureBiomeSamplerCache(biomeCache, bridge, playerTransform);
            ConfigureProximityColliderSystem(proximityColliderSystem, playerTransform, colliderPrefab);
            ConfigureScatterBudgetController(
                scatterBudgetController,
                playerTransform,
                bridge,
                scavengePopulator,
                proximityColliderSystem,
                biomeCache);
            ConfigureWorldStreamingDirector(
                streamingDirector,
                playerTransform,
                playerBody,
                bridge,
                biomeCache,
                scatterBudgetController,
                sliceDirector);
            ConfigureWorldSliceDirector(sliceDirector, playerTransform);
            ConfigureWorldInterestDirector(interestDirector, playerTransform, scatterBudgetController);
            ConfigureWorldZoneDirector(zoneDirector, playerTransform);
            ConfigureWorldContentDirector(contentDirector, playerTransform, zoneDirector);
            ConfigureWorldPopulationDirector(populationDirector, playerTransform, zoneDirector, contentDirector);
            ConfigureBiomeMatrixDirector(biomeMatrixDirector, playerTransform, biomeMatrixCatalog);
            ConfigureSceneSlices();
            ConfigureSceneInterestAnchors();
            ConfigureSceneZones();
            ConfigureSceneContentSockets();
            ConfigurePopulationRules(populationDirector);

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

        private static void ConfigureScatterBudgetController(
            ScatterBudgetController controller,
            Transform playerTransform,
            MapMagicBridge bridge,
            ScavengePopulator scavengePopulator,
            ProximityColliderSystem proximityColliderSystem,
            BiomeSamplerCache biomeCache)
        {
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            so.FindProperty("mapMagicBridge").objectReferenceValue = bridge;
            so.FindProperty("scavengePopulator").objectReferenceValue = scavengePopulator;
            so.FindProperty("proximityColliderSystem").objectReferenceValue = proximityColliderSystem;
            so.FindProperty("biomeSamplerCache").objectReferenceValue = biomeCache;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureWorldStreamingDirector(
            WorldStreamingDirector director,
            Transform playerTransform,
            Rigidbody playerBody,
            MapMagicBridge bridge,
            BiomeSamplerCache biomeCache,
            ScatterBudgetController scatterBudgetController,
            WorldSliceDirector sliceDirector)
        {
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            so.FindProperty("playerRigidbody").objectReferenceValue = playerBody;
            so.FindProperty("mapMagicBridge").objectReferenceValue = bridge;
            so.FindProperty("biomeSamplerCache").objectReferenceValue = biomeCache;
            so.FindProperty("scatterBudgetController").objectReferenceValue = scatterBudgetController;
            so.FindProperty("worldSliceDirector").objectReferenceValue = sliceDirector;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
        }

        private static void ConfigureWorldSliceDirector(
            WorldSliceDirector director,
            Transform playerTransform)
        {
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            so.ApplyModifiedPropertiesWithoutUndo();
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
            so.FindProperty("worldSliceDirector").objectReferenceValue = GetOrAddComponent<WorldSliceDirector>(GameObject.Find(ManagersRootName));
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

        private static void ConfigureSceneSlices()
        {
            ConfigureResourceFieldSlice();
            ConfigureFabricationOutpostSlice();
            ConfigureFabricationTrialSlice();
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
        }

        private static void ConfigureSceneZones()
        {
            ConfigureZone("--- WORLD ---/Resource_FieldSources", "zone.resources.field", "Resource Field", WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneTier.Starter, 0, 105f, 160f, "Starter raw-resource pocket for scrap, ore, and basic organics.", false, EnsureZoneProfile("ZoneProfile_Resources_Starter.asset", "profile.resources.starter", "Resources Starter", 1.16f, 1.14f, 1.08f, 1.08f, 1.06f, 1.12f, "resources.pickups.near", "resources.clutter.mid", "resources.landmarks.far"), 5);
            ConfigureZone("--- WORLD ---/Fabrication_Outpost", "zone.fabrication.outpost", "Fabrication Outpost", WorldZoneAnchor.ZoneKind.Fabrication, WorldZoneAnchor.ZoneTier.Early, 4, 92f, 156f, "Safe utility stop for crafting, route recovery, and logistic reset.", true, EnsureZoneProfile("ZoneProfile_Fabrication_Early.asset", "profile.fabrication.early", "Fabrication Early", 1.04f, 1.02f, 1.12f, 1.1f, 1.04f, 1.16f, "fabrication.usables.near", "fabrication.outpost.mid", "fabrication.outpost.far"), 6);
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
            ConfigureContentSocket("--- WORLD ---/Fabrication_Outpost/Forward_Fabricator", "socket.fabrication.forward", "Forward Fabricator", WorldContentSocket.ContentKind.FabricationStation, WorldSliceAnchor.SliceState.Mid, 8f, 5, "station.fabricator.forward", "Safe fabrication station and recovery stop.", EnsureContentProfile("ContentProfile_FabricationStation.asset", "content.profile.fabrication_station", "Fabrication Station", WorldContentSocket.ContentKind.FabricationStation, WorldZoneAnchor.ZoneKind.Fabrication, WorldSliceAnchor.SliceState.Mid, "station.fabrication", "Crafting and recovery station.", 5));
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

        private static void ConfigurePopulationRules(WorldPopulationDirector director)
        {
            List<WorldPopulationRule> rules = new List<WorldPopulationRule>
            {
                EnsurePopulationRule("PopulationRule_Resources_Starter.asset", "population.rule.resources.starter", "Starter Resource Pocket", WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneTier.Starter, WorldZoneAnchor.ZoneTier.Early, WorldContentSocket.ContentKind.ResourcePickup, "resource.pickup.cluster", "Starter loose resource pocket.", "Best in bright starter geology with clear gathering loops and obvious return lines.", 1.2f, 3, 2, 6, "biome.family.littoral_karst", "biome.family.sediment_drift"),
                EnsurePopulationRule("PopulationRule_ResourceNode_Starter.asset", "population.rule.resource_node.starter", "Starter Resource Node", WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneTier.Starter, WorldZoneAnchor.ZoneTier.Mid, WorldContentSocket.ContentKind.ResourceNode, "resource.node.cluster", "Starter extractable node cluster.", "Best where readable stone forms hide mineral pockets without heavy combat pressure.", 1.15f, 2, 1, 3, "biome.family.littoral_karst", "biome.family.granite_escarpment", "biome.family.crystal_growth"),
                EnsurePopulationRule("PopulationRule_Fabrication_Outpost.asset", "population.rule.fabrication.outpost", "Fabrication Outpost Utility", WorldZoneAnchor.ZoneKind.Fabrication, WorldZoneAnchor.ZoneTier.Early, WorldZoneAnchor.ZoneTier.Mid, WorldContentSocket.ContentKind.FabricationStation, "station.fabrication.outpost", "Crafting/rest stop and support pocket.", "Fits calm, readable transition spaces that feel safe enough to regroup.", 0.8f, 1, 1, 1, "biome.family.sediment_drift", "biome.family.littoral_karst"),
                EnsurePopulationRule("PopulationRule_Construction_Mid.asset", "population.rule.construction.mid", "Construction Support Route", WorldZoneAnchor.ZoneKind.Construction, WorldZoneAnchor.ZoneTier.Mid, WorldZoneAnchor.ZoneTier.Endgame, WorldContentSocket.ContentKind.ConstructionPoint, "construction.support.route", "Sockets and blockers around construction flow.", "Works best in strong structural geology where frames, ledges, and route anchors read clearly.", 1.0f, 2, 1, 3, "biome.family.tectonic_spine", "biome.family.granite_escarpment", "biome.family.rift_spine"),
                EnsurePopulationRule("PopulationRule_Power_Mid.asset", "population.rule.power.mid", "Power Support Chain", WorldZoneAnchor.ZoneKind.Power, WorldZoneAnchor.ZoneTier.Mid, WorldZoneAnchor.ZoneTier.Endgame, WorldContentSocket.ContentKind.PowerPoint, "power.support.chain", "Generation, relay, and service load chain.", "Best in hot, fractured, or chemical spaces where energy infrastructure feels necessary.", 1.0f, 2, 1, 3, "biome.family.volcanic_glass", "biome.family.chemosynthetic_brine", "biome.family.rift_spine"),
                EnsurePopulationRule("PopulationRule_Service_Mid.asset", "population.rule.service.mid", "Service Recovery Target", WorldZoneAnchor.ZoneKind.Service, WorldZoneAnchor.ZoneTier.Mid, WorldZoneAnchor.ZoneTier.Endgame, WorldContentSocket.ContentKind.ServiceTarget, "service.recovery.target", "Flooded or damaged service recovery target.", "Best where pressure, silt, or corrosion make maintenance feel like real survival work.", 0.95f, 2, 1, 2, "biome.family.abyssal_silt", "biome.family.chemosynthetic_brine", "biome.family.tectonic_spine"),
                EnsurePopulationRule("PopulationRule_Navigation_Mid.asset", "population.rule.navigation.mid", "Navigation Guide Chain", WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneTier.Mid, WorldZoneAnchor.ZoneTier.Endgame, WorldContentSocket.ContentKind.NavigationMarker, "navigation.marker.chain", "Readable route markers and frontier guides.", "Best in spaces where the terrain itself teaches route memory and branch choice.", 0.9f, 3, 2, 4, "biome.family.granite_escarpment", "biome.family.tectonic_spine", "biome.family.sediment_drift"),
                EnsurePopulationRule("PopulationRule_Combat_Mid.asset", "population.rule.combat.mid", "Combat Pressure Node", WorldZoneAnchor.ZoneKind.Combat, WorldZoneAnchor.ZoneTier.Mid, WorldZoneAnchor.ZoneTier.Endgame, WorldContentSocket.ContentKind.CombatPoint, "combat.pressure.node", "Hostile or controlling combat contact.", "Best in reefs, fractures, and hostile terrain that create short control fights instead of flat arenas.", 0.95f, 2, 1, 2, "biome.family.fossil_reef", "biome.family.rift_spine", "biome.family.volcanic_hadal"),
                EnsurePopulationRule("PopulationRule_Progression_Endgame.asset", "population.rule.progression.endgame", "Endgame Progression Route", WorldZoneAnchor.ZoneKind.Progression, WorldZoneAnchor.ZoneTier.Late, WorldZoneAnchor.ZoneTier.Endgame, WorldContentSocket.ContentKind.Landmark, "progression.route.landmark", "Late-game frontier landmark and route goal.", "Best in extreme late spaces where the landmark itself is a promise of major progress.", 0.85f, 1, 1, 2, "biome.family.volcanic_hadal", "biome.family.metallic_hadal", "biome.family.rift_void"),
                EnsurePopulationRule("PopulationRule_Hazard_Generic.asset", "population.rule.hazard.generic", "Hazard Probe Logic", WorldZoneAnchor.ZoneKind.Generic, WorldZoneAnchor.ZoneTier.Starter, WorldZoneAnchor.ZoneTier.Endgame, WorldContentSocket.ContentKind.HazardPoint, "hazard.probe", "Probe or warning anchor in risky routes.", "Best where the terrain itself carries localized danger and forces a read before commitment.", 0.9f, 2, 1, 3, "biome.family.volcanic_glass", "biome.family.chemosynthetic_brine", "biome.family.rift_void")
            };

            director.SetRules(rules);
            EditorUtility.SetDirty(director);
        }

        private static void ConfigureResourceFieldSlice()
        {
            GameObject root = GameObject.Find("--- WORLD ---/Resource_FieldSources");
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

        private static void ConfigureFabricationOutpostSlice()
        {
            GameObject root = GameObject.Find("--- WORLD ---/Fabrication_Outpost");
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
            GameObject root = GameObject.Find("Fabrication_Trial");
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
            GameObject root = GameObject.Find("Tool_Staging");
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
            GameObject root = GameObject.Find(lanePath);
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
            GameObject root = GameObject.Find(objectPath);
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
            GameObject root = GameObject.Find(objectPath);
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
            so.FindProperty("gameplayIntent").stringValue = gameplayIntent;
            so.FindProperty("routeCritical").boolValue = routeCritical;
            so.FindProperty("zoneProfile").objectReferenceValue = zoneProfile;
            so.FindProperty("dominantMatrixBiome").objectReferenceValue = dominantBiome;
            so.FindProperty("dominantBiomeFamily").objectReferenceValue = dominantBiome != null ? dominantBiome.familyProfile : null;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(zone);
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
            GameObject target = GameObject.Find(objectPath);
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
            ApplySpatialRolePlans(profile.zonePlanProfile, profile.profileId);
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
                    return "Safe logistics stop for crafting, route reset, and regrouping.";
                case "profile.trial.early":
                    return "Dense authored space for tool practice, regression checks, and future prefab replacement.";
                case "profile.construction.mid":
                    return "Construction route with obvious sockets, blockers, and support structure.";
                case "profile.power.mid":
                    return "Power route built around generator, relay, and serviced load readability.";
                case "profile.progression.endgame":
                    return "Late-game chain that mixes hazard, recovery, combat pressure, and route landmarks.";
                case "profile.combat.mid":
                    return "Combat pocket focused on threat readability and control timing.";
                case "profile.navigation.mid":
                    return "Navigation hub that helps the player read branch choice and return flow.";
            }

            return "Generic world zone plan.";
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
                    "resource_pocket" => "Small readable loose-resource pocket close to a safe route line.",
                    "node_cluster" => "A slightly deeper mineral cluster that asks for a small detour.",
                    "safe_pocket" => "Short recovery nook behind stone cover or reef folds.",
                    "route_anchor" => "A strong readable form that keeps beginner routes stable.",
                    "rare_objective" => "The best find of the pocket, one layer deeper than routine scrap.",
                    _ => "Not a major role for this zone."
                },
                "profile.fabrication.early" => roleId switch
                {
                    "safe_pocket" => "The trusted regroup and craft stop around the outpost.",
                    "route_anchor" => "Approach marker that brings the player back to safety.",
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
            T component = gameObject.GetComponent<T>();
            if (component == null)
                component = gameObject.AddComponent<T>();

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
