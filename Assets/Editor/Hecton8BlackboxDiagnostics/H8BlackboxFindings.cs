// H8BlackboxFindings.cs — Findings engine for Hecton8 Blackbox Diagnostics
// Analyzes an H8DiagnosticSnapshot and produces prioritized H8Finding lists.
// READ-ONLY diagnostic tool. Does not modify scenes, settings, or project assets.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.BlackboxDiagnostics
{
    public static class H8FindingsEngine
    {
        // ── Public Entry Point ───────────────────────────────────────────────

        /// <summary>
        /// Run all domain analyzers against a snapshot and return findings sorted
        /// by severity (Critical > Error > Warning > Info).
        /// </summary>
        public static List<H8Finding> Analyze(H8DiagnosticSnapshot snapshot)
        {
            if (snapshot == null)
            {
                Debug.LogWarning("[H8Blackbox] Analyze called with null snapshot.");
                return new List<H8Finding>();
            }

            var all = new List<H8Finding>();
            try { all.AddRange(AnalyzeBootstrap(snapshot)); }   catch (Exception e) { Debug.LogWarning($"[H8Blackbox] AnalyzeBootstrap failed: {e.Message}"); }
            try { all.AddRange(AnalyzeRegistry(snapshot)); }    catch (Exception e) { Debug.LogWarning($"[H8Blackbox] AnalyzeRegistry failed: {e.Message}"); }
            try { all.AddRange(AnalyzeMapMagic(snapshot)); }    catch (Exception e) { Debug.LogWarning($"[H8Blackbox] AnalyzeMapMagic failed: {e.Message}"); }
            try { all.AddRange(AnalyzeCrest(snapshot)); }       catch (Exception e) { Debug.LogWarning($"[H8Blackbox] AnalyzeCrest failed: {e.Message}"); }
            try { all.AddRange(AnalyzeAtmosphere(snapshot)); }  catch (Exception e) { Debug.LogWarning($"[H8Blackbox] AnalyzeAtmosphere failed: {e.Message}"); }
            try { all.AddRange(AnalyzeUrp(snapshot)); }         catch (Exception e) { Debug.LogWarning($"[H8Blackbox] AnalyzeUrp failed: {e.Message}"); }
            try { all.AddRange(AnalyzeCameras(snapshot)); }     catch (Exception e) { Debug.LogWarning($"[H8Blackbox] AnalyzeCameras failed: {e.Message}"); }
            try { all.AddRange(AnalyzeConsole(snapshot)); }     catch (Exception e) { Debug.LogWarning($"[H8Blackbox] AnalyzeConsole failed: {e.Message}"); }

            all.Sort((a, b) => SeverityRank(b.severity).CompareTo(SeverityRank(a.severity)));
            return all;
        }

        private static int SeverityRank(string severity)
        {
            switch (severity)
            {
                case "Critical": return 4;
                case "Error":    return 3;
                case "Warning":  return 2;
                case "Info":     return 1;
                default:         return 0;
            }
        }

        // ── 1. Bootstrap ─────────────────────────────────────────────────────

        private static List<H8Finding> AnalyzeBootstrap(H8DiagnosticSnapshot s)
        {
            var f = new List<H8Finding>();
            var boot = s.bootstrap;
            var reg  = s.registry;

            if (!boot.bootstrapperFound)
            {
                f.Add(H8Finding.Create(
                    "BOOTSTRAP_NOT_FOUND", H8Severity.Critical, H8FindingCategory.Bootstrap,
                    "GameBootstrapper not found in scene",
                    $"bootstrapperFound={boot.bootstrapperFound}, activeScene={boot.activeSceneName}",
                    "false",
                    "Without the bootstrapper, GlobalRegistry never initializes and all subsystems remain null. Nothing works.",
                    95,
                    "Check if 00_BOOTSTRAP scene is loaded or if bootstrapper prefab exists.",
                    "Load 00_BOOTSTRAP scene first, or ensure GameBootstrapper is present in the active scene."));
            }

            if (reg.registryPhase == 0 && !boot.isBootstrapScene)
            {
                f.Add(H8Finding.Create(
                    "BOOTSTRAP_NOT_STARTED", H8Severity.Critical, H8FindingCategory.Bootstrap,
                    "Bootstrap has not started — registry phase is 0 and this is not the bootstrap scene",
                    $"registryPhase={reg.registryPhase}, isBootstrapScene={boot.isBootstrapScene}, activeScene={boot.activeSceneName}",
                    $"registryPhase=0, scene={boot.activeSceneName}",
                    "The game was likely started from a non-bootstrap scene. No services are initialized.",
                    95,
                    "Verify scene load order in Build Settings. 00_BOOTSTRAP must be scene 0.",
                    "Start Play Mode from 00_BOOTSTRAP, or add an auto-bootstrap mechanism."));
            }

            bool isWorldScene = !string.IsNullOrEmpty(boot.activeSceneName) &&
                                boot.activeSceneName.Contains("02_HECTON_WORLD");
            if (isWorldScene && reg.registryPhase != 2)
            {
                f.Add(H8Finding.Create(
                    "DIRECT_WORLD_SCENE_START_DETECTED", H8Severity.Critical, H8FindingCategory.Bootstrap,
                    "02_HECTON_WORLD is active but bootstrap did not complete",
                    $"activeScene={boot.activeSceneName}, registryPhase={reg.registryPhase}",
                    $"phase={reg.registryPhase}",
                    "Direct world-scene start bypasses bootstrap. All registry slots will be null, causing cascading NullReferenceExceptions.",
                    95,
                    "Check if Play was pressed while 02_HECTON_WORLD was the active scene in the editor.",
                    "Always start from 00_BOOTSTRAP. Use EditorBuildSettings to enforce scene 0."));
            }

            if (boot.bootstrapperFound && reg.registryPhase != 2)
            {
                f.Add(H8Finding.Create(
                    "BOOTSTRAP_NOT_COMPLETE", H8Severity.Error, H8FindingCategory.Bootstrap,
                    "Bootstrapper found but registry phase is not Complete (2)",
                    $"bootstrapperFound={boot.bootstrapperFound}, registryPhase={reg.registryPhase}, phaseName={reg.registryPhaseName}",
                    $"phase={reg.registryPhase} ({reg.registryPhaseName})",
                    "Bootstrap started but did not finish. Some services may be partially initialized, causing unpredictable failures.",
                    90,
                    "Check console for errors during bootstrap. Look for exceptions in GameBootstrapper or GlobalRegistry init.",
                    "Debug the bootstrap sequence. Check for missing dependencies or exceptions during initialization."));
            }

            return f;
        }

        // ── 2. Registry ──────────────────────────────────────────────────────

        private static List<H8Finding> AnalyzeRegistry(H8DiagnosticSnapshot s)
        {
            var f   = new List<H8Finding>();
            var reg = s.registry;

            if (!reg.typeFound)
            {
                f.Add(H8Finding.Create(
                    "GLOBAL_REGISTRY_TYPE_NOT_FOUND", H8Severity.Critical, H8FindingCategory.Registry,
                    "GlobalRegistry type not found via reflection",
                    $"typeFound={reg.typeFound}, typeName={reg.typeName}",
                    "type not found",
                    "The GlobalRegistry class could not be located. This may indicate a compile error or missing assembly.",
                    100,
                    "Check for compile errors in the Console. Verify GlobalRegistry.cs exists and compiles.",
                    "Fix compile errors. Ensure the assembly containing GlobalRegistry is loaded."));
                return f;
            }

            if (reg.registryPhase == 0)
            {
                bool allNull = true;
                foreach (var slot in reg.slots)
                {
                    if (!slot.isNull) { allNull = false; break; }
                }
                if (allNull)
                {
                    f.Add(H8Finding.Create(
                        "GLOBAL_REGISTRY_EMPTY_OR_UNREADY", H8Severity.Critical, H8FindingCategory.Registry,
                        "GlobalRegistry phase is 0 and all service slots are null",
                        $"registryPhase={reg.registryPhase}, slotCount={reg.slots.Count}, allNull=true",
                        "phase=0, all slots null",
                        "The registry has not been populated at all. No subsystem can resolve its dependencies.",
                        95,
                        "Verify bootstrap ran. Check if GlobalRegistry.Initialize() was called.",
                        "Start from 00_BOOTSTRAP scene or debug the bootstrap flow."));
                }
            }

            // Critical service slots
            var criticalSlots = new string[]
            {
                "Input", "Physics", "Audio", "Scene", "Save", "UI", "Player",
                "OceanKinematics", "AtmosphereRuntime", "CelestialEngineRuntime",
                "MapMagicRuntime", "TerrainProviderRuntime", "TickManager", "Dispatcher"
            };

            foreach (var slotName in criticalSlots)
            {
                var slot = FindSlot(reg.slots, slotName);
                if (slot != null)
                {
                    if (!slot.memberFound)
                    {
                        f.Add(H8Finding.Create(
                            $"GLOBAL_REGISTRY_SLOT_MEMBER_NOT_FOUND_{slotName.ToUpperInvariant()}",
                            H8Severity.Warning, H8FindingCategory.Registry,
                            $"Registry slot field for '{slotName}' was not found in the class",
                            $"slot={slotName}, memberFound=false",
                            "not found",
                            $"The field for {slotName} does not exist in GlobalRegistry. It might have been renamed or removed.",
                            70,
                            $"Check GlobalRegistry source code for the {slotName} field.",
                            $"Update the diagnostic tool or fix GlobalRegistry field names."));
                    }
                    else if (slot.isNull)
                    {
                        f.Add(H8Finding.Create(
                            $"GLOBAL_REGISTRY_SLOT_NULL_{slotName.ToUpperInvariant()}",
                            H8Severity.Error, H8FindingCategory.Registry,
                            $"Registry slot '{slotName}' is null",
                            $"slot={slotName}, isNull=true, registryPhase={reg.registryPhase}",
                            "null",
                            $"The {slotName} service is not registered. Systems depending on it will fail or degrade.",
                            90,
                            $"Check if the {slotName} provider component exists in the scene and registers during bootstrap.",
                            $"Ensure the {slotName} provider is present and its registration runs during bootstrap."));
                    }
                }
            }

            // Specific tick dispatcher check
            var tickSlot       = FindSlot(reg.slots, "TickManager");
            var dispatcherSlot = FindSlot(reg.slots, "Dispatcher");
            if ((tickSlot != null && tickSlot.isNull) || (dispatcherSlot != null && dispatcherSlot.isNull))
            {
                f.Add(H8Finding.Create(
                    "TICK_DISPATCHER_MISSING", H8Severity.Error, H8FindingCategory.Registry,
                    "TickManager or Dispatcher slot is null — game loop may not run",
                    $"TickManager.isNull={tickSlot?.isNull}, Dispatcher.isNull={dispatcherSlot?.isNull}",
                    $"TickManager={tickSlot?.isNull}, Dispatcher={dispatcherSlot?.isNull}",
                    "Without the tick dispatcher, IUpdatable/ITickable systems will not receive updates and the game loop stalls.",
                    90,
                    "Check if GameTickManager or equivalent dispatcher exists and registers in GlobalRegistry.",
                    "Ensure tick dispatcher is present in the bootstrap scene and registers during initialization."));
            }

            return f;
        }

        private static H8RegistrySlotInfo FindSlot(List<H8RegistrySlotInfo> slots, string name)
        {
            if (slots == null) return null;
            for (int i = 0; i < slots.Count; i++)
            {
                if (string.Equals(slots[i].slotName, name, StringComparison.OrdinalIgnoreCase))
                    return slots[i];
            }
            return null;
        }

        // ── 3. MapMagic ──────────────────────────────────────────────────────

        private static List<H8Finding> AnalyzeMapMagic(H8DiagnosticSnapshot s)
        {
            var f  = new List<H8Finding>();
            var mm = s.mapMagic;

            if (!mm.mapMagicObjectFound)
            {
                f.Add(H8Finding.Create(
                    "MAPMAGIC_OBJECT_MISSING", H8Severity.Error, H8FindingCategory.MapMagic,
                    "MapMagicObject not found in scene",
                    $"mapMagicObjectFound={mm.mapMagicObjectFound}",
                    "not found",
                    "Without MapMagic, terrain generation will not work and the world will have no terrain.",
                    90,
                    "Check if a GameObject with MapMagicObject component exists in the world scene.",
                    "Ensure the MapMagic terrain generator is present in 02_HECTON_WORLD."));
                return f;
            }

            if (!mm.mapMagicObjectActiveInHierarchy)
            {
                f.Add(H8Finding.Create(
                    "MAPMAGIC_OBJECT_INACTIVE", H8Severity.Warning, H8FindingCategory.MapMagic,
                    "MapMagicObject found but inactive in hierarchy",
                    $"activeSelf={mm.mapMagicObjectActive}, activeInHierarchy={mm.mapMagicObjectActiveInHierarchy}",
                    $"activeInHierarchy={mm.mapMagicObjectActiveInHierarchy}",
                    "MapMagic exists but is disabled. Terrain will not generate until activated.",
                    85,
                    "Check if the MapMagic GameObject or a parent is disabled.",
                    "Enable the MapMagic GameObject and all parents in the hierarchy."));
            }

            if (!mm.runtimeBridgeFound)
            {
                f.Add(H8Finding.Create(
                    "MAPMAGIC_BRIDGE_MISSING", H8Severity.Warning, H8FindingCategory.MapMagic,
                    "MapMagic runtime bridge component not found",
                    $"runtimeBridgeFound={mm.runtimeBridgeFound}",
                    "not found",
                    "The Hecton8 bridge to MapMagic is missing. Terrain events and integration may not work.",
                    80,
                    "Check for the MapMagic bridge/adapter component in the scene.",
                    "Add the MapMagic runtime bridge component to the MapMagic GameObject."));
            }
            else if (!mm.runtimeBridgeEnabled)
            {
                f.Add(H8Finding.Create(
                    "MAPMAGIC_BRIDGE_DISABLED", H8Severity.Warning, H8FindingCategory.MapMagic,
                    "MapMagic runtime bridge found but disabled",
                    $"runtimeBridgeFound={mm.runtimeBridgeFound}, runtimeBridgeEnabled={mm.runtimeBridgeEnabled}",
                    $"enabled={mm.runtimeBridgeEnabled}",
                    "The bridge exists but is not enabled. Terrain integration callbacks will not fire.",
                    80,
                    "Check the enabled state of the bridge component in the Inspector.",
                    "Enable the MapMagic runtime bridge component."));
            }

            if (!mm.graphAssigned)
            {
                f.Add(H8Finding.Create(
                    "MAPMAGIC_GRAPH_NULL", H8Severity.Error, H8FindingCategory.MapMagic,
                    "MapMagic graph asset is not assigned",
                    $"graphAssigned={mm.graphAssigned}, graphAssetName={mm.graphAssetName}",
                    "null",
                    "Without a graph, MapMagic has no generation recipe. No terrain tiles will be created.",
                    90,
                    "Check the MapMagicObject Inspector for the Graph field.",
                    "Assign a valid MapMagic graph asset to the MapMagicObject."));
            }

            if (mm.activeTerrainCount == 0)
            {
                f.Add(H8Finding.Create(
                    "MAPMAGIC_NO_TERRAINS_GENERATED", H8Severity.Warning, H8FindingCategory.MapMagic,
                    "No active terrains found in scene",
                    $"activeTerrainCount={mm.activeTerrainCount}, allTerrainCount={mm.allTerrainCount}",
                    $"active={mm.activeTerrainCount}, total={mm.allTerrainCount}",
                    "Terrain generation may not have run yet, or all terrains were destroyed/disabled.",
                    75,
                    "Enter Play Mode from 00_BOOTSTRAP and wait for terrain generation. Check MapMagic logs.",
                    "Ensure MapMagic generates at least one terrain tile in Play Mode."));
            }

            if (!mm.registeredInGlobalRegistry)
            {
                f.Add(H8Finding.Create(
                    "MAPMAGIC_NOT_REGISTERED", H8Severity.Warning, H8FindingCategory.MapMagic,
                    "MapMagic is not registered in GlobalRegistry",
                    $"registeredInGlobalRegistry={mm.registeredInGlobalRegistry}",
                    "false",
                    "Other systems cannot discover MapMagic through the registry. Terrain queries may fail.",
                    80,
                    "Check if the MapMagic bridge registers itself during bootstrap.",
                    "Ensure the bridge component calls GlobalRegistry registration during initialization."));
            }

            return f;
        }

        // ── 4. Crest ─────────────────────────────────────────────────────────

        private static List<H8Finding> AnalyzeCrest(H8DiagnosticSnapshot s)
        {
            var f = new List<H8Finding>();
            var c = s.crest;

            if (!c.oceanCrestObjectFound)
            {
                f.Add(H8Finding.Create(
                    "OCEAN_CREST_MISSING", H8Severity.Error, H8FindingCategory.Crest,
                    "Ocean_Crest GameObject not found in scene",
                    $"oceanCrestObjectFound={c.oceanCrestObjectFound}",
                    "not found",
                    "The ocean root object is missing. No ocean surface will render.",
                    95,
                    "Check if Ocean_Crest exists in the world scene hierarchy.",
                    "Ensure Ocean_Crest GameObject is present in 02_HECTON_WORLD."));
                return f;
            }

            if (!c.oceanCrestActive)
            {
                f.Add(H8Finding.Create(
                    "OCEAN_CREST_INACTIVE", H8Severity.Critical, H8FindingCategory.Crest,
                    "Ocean_Crest GameObject is inactive",
                    $"oceanCrestActive={c.oceanCrestActive}, activeInHierarchy={c.oceanCrestActiveInHierarchy}",
                    $"active={c.oceanCrestActive}",
                    "The ocean root is disabled. No ocean surface, underwater, or wave simulation will run.",
                    95,
                    "Check if Ocean_Crest or a parent is disabled in the hierarchy.",
                    "Enable Ocean_Crest and all parent GameObjects."));
            }

            if (!c.oceanRendererFound)
            {
                f.Add(H8Finding.Create(
                    "OCEAN_RENDERER_MISSING", H8Severity.Error, H8FindingCategory.Crest,
                    "OceanRenderer component not found",
                    $"oceanRendererFound={c.oceanRendererFound}",
                    "not found",
                    "Crest's core OceanRenderer component is missing. Ocean simulation cannot start.",
                    95,
                    "Check if OceanRenderer is attached to the Ocean_Crest GameObject.",
                    "Add OceanRenderer component to Ocean_Crest."));
            }
            else
            {
                if (!c.oceanRendererActive)
                {
                    f.Add(H8Finding.Create(
                        "OCEAN_RENDERER_INACTIVE", H8Severity.Critical, H8FindingCategory.Crest,
                        "OceanRenderer found but its GameObject is inactive",
                        $"oceanRendererActive={c.oceanRendererActive}",
                        $"active={c.oceanRendererActive}",
                        "OceanRenderer exists but its host GameObject is off. Ocean will not render.",
                        95,
                        "Check the OceanRenderer's host GameObject active state.",
                        "Enable the OceanRenderer's host GameObject."));
                }

                if (!c.oceanRendererEnabled)
                {
                    f.Add(H8Finding.Create(
                        "OCEAN_RENDERER_DISABLED", H8Severity.Error, H8FindingCategory.Crest,
                        "OceanRenderer component is disabled",
                        $"oceanRendererEnabled={c.oceanRendererEnabled}",
                        $"enabled={c.oceanRendererEnabled}",
                        "OceanRenderer is present but the component is turned off. Ocean will not simulate or render.",
                        90,
                        "Check the OceanRenderer component checkbox in the Inspector.",
                        "Enable the OceanRenderer component."));
                }
            }

            if (!c.viewCameraAssigned)
            {
                f.Add(H8Finding.Create(
                    "OCEAN_VIEW_CAMERA_NULL", H8Severity.Error, H8FindingCategory.Crest,
                    "OceanRenderer._viewCamera is null",
                    $"viewCameraAssigned={c.viewCameraAssigned}",
                    "null",
                    "Crest cannot determine which camera to generate ocean LODs for. Ocean may not render correctly.",
                    85,
                    "Check OceanRenderer's ViewCamera reference in Play Mode.",
                    "Ensure a main camera exists and Crest can discover it, or assign it explicitly."));
            }

            if (!c.viewpointAssigned)
            {
                f.Add(H8Finding.Create(
                    "OCEAN_VIEWPOINT_NULL", H8Severity.Warning, H8FindingCategory.Crest,
                    "OceanRenderer._viewpoint is null",
                    $"viewpointAssigned={c.viewpointAssigned}",
                    "null",
                    "No explicit viewpoint assigned. Crest will fall back to the view camera, which usually works but may cause LOD jitter.",
                    80,
                    "Check OceanRenderer viewpoint field. This may be expected if relying on camera fallback.",
                    "Optionally assign a viewpoint transform to OceanRenderer for stable LOD transitions."));
            }

            if (!c.primaryLightAssigned)
            {
                f.Add(H8Finding.Create(
                    "OCEAN_PRIMARY_LIGHT_NULL", H8Severity.Error, H8FindingCategory.Crest,
                    "OceanRenderer._primaryLight is null",
                    $"primaryLightAssigned={c.primaryLightAssigned}",
                    "null",
                    "Without a primary light, Crest's specular, caustics, and underwater lighting will fail or look wrong.",
                    85,
                    "Check if a directional light is assigned or discoverable by Crest.",
                    "Ensure a Sun directional light exists and is assigned to OceanRenderer."));
            }

            if (c.adapterFound && (!c.adapterActive || !c.adapterEnabled))
            {
                f.Add(H8Finding.Create(
                    "CREST_ADAPTER_INACTIVE", H8Severity.Warning, H8FindingCategory.Crest,
                    "Crest adapter/bridge found but not active or not enabled",
                    $"adapterFound={c.adapterFound}, adapterActive={c.adapterActive}, adapterEnabled={c.adapterEnabled}",
                    $"active={c.adapterActive}, enabled={c.adapterEnabled}",
                    "The Hecton8-Crest adapter exists but is off. Ocean integration with gameplay systems will not work.",
                    80,
                    "Check the adapter component's enabled state and its GameObject's active state.",
                    "Enable the Crest adapter component and its host GameObject."));
            }

            if (!c.kinematicsRegistered)
            {
                f.Add(H8Finding.Create(
                    "OCEAN_KINEMATICS_NOT_REGISTERED", H8Severity.Error, H8FindingCategory.Crest,
                    "OceanKinematics is not registered in GlobalRegistry",
                    $"kinematicsRegistered={c.kinematicsRegistered}",
                    "false",
                    "Gameplay systems that query wave height, buoyancy, or water level cannot resolve the ocean service.",
                    85,
                    "Check if the ocean kinematics provider registers in GlobalRegistry during bootstrap.",
                    "Ensure the Crest adapter or kinematics provider calls GlobalRegistry.Register during init."));
            }

            if (!c.underwaterRendererFound)
            {
                f.Add(H8Finding.Create(
                    "UNDERWATER_RENDERER_MISSING", H8Severity.Warning, H8FindingCategory.Crest,
                    "UnderwaterRenderer not found",
                    $"underwaterRendererFound={c.underwaterRendererFound}",
                    "not found",
                    "Without UnderwaterRenderer, the camera will not apply underwater fog, caustics, or meniscus effects.",
                    75,
                    "Check if UnderwaterRenderer is attached to the main camera or Ocean_Crest hierarchy.",
                    "Add UnderwaterRenderer component to the main camera GameObject."));
            }
            else if (!c.underwaterRendererEnabled)
            {
                f.Add(H8Finding.Create(
                    "UNDERWATER_RENDERER_DISABLED", H8Severity.Warning, H8FindingCategory.Crest,
                    "UnderwaterRenderer found but disabled",
                    $"underwaterRendererFound={c.underwaterRendererFound}, underwaterRendererEnabled={c.underwaterRendererEnabled}",
                    $"enabled={c.underwaterRendererEnabled}",
                    "Underwater visual effects exist but are turned off. Submerging will look wrong.",
                    70,
                    "Check UnderwaterRenderer component enabled state.",
                    "Enable the UnderwaterRenderer component."));
            }

            return f;
        }

        // ── 5. Atmosphere ────────────────────────────────────────────────────

        private static List<H8Finding> AnalyzeAtmosphere(H8DiagnosticSnapshot s)
        {
            var f   = new List<H8Finding>();
            var atm = s.atmosphere;
            var reg = s.registry;

            if (!atm.atmosphereManagerFound)
            {
                f.Add(H8Finding.Create(
                    "ATMOSPHERE_MANAGER_MISSING", H8Severity.Error, H8FindingCategory.Atmosphere,
                    "AtmosphereManager not found in scene",
                    $"atmosphereManagerFound={atm.atmosphereManagerFound}",
                    "not found",
                    "No atmosphere manager means no dynamic sky, fog, or time-of-day transitions.",
                    90,
                    "Check if AtmosphereManager component exists in the world scene.",
                    "Ensure AtmosphereManager is present and active in the world scene."));
            }

            if (!atm.celestialEngineFound)
            {
                f.Add(H8Finding.Create(
                    "CELESTIAL_ENGINE_MISSING", H8Severity.Error, H8FindingCategory.Atmosphere,
                    "CelestialEngine not found in scene",
                    $"celestialEngineFound={atm.celestialEngineFound}",
                    "not found",
                    "No celestial engine means no sun/moon cycle, no dynamic lighting direction, and broken sky.",
                    90,
                    "Check if CelestialEngine component exists in the world scene.",
                    "Ensure CelestialEngine is present and active in the world scene."));
            }

            if (!atm.atmosphereRegistered)
            {
                f.Add(H8Finding.Create(
                    "ATMOSPHERE_NOT_REGISTERED", H8Severity.Warning, H8FindingCategory.Atmosphere,
                    "AtmosphereRuntime is not registered in GlobalRegistry",
                    $"atmosphereRegistered={atm.atmosphereRegistered}",
                    "false",
                    "Other systems cannot query atmosphere state (fog, sky color, weather) through the registry.",
                    80,
                    "Check if AtmosphereManager registers in GlobalRegistry during bootstrap.",
                    "Ensure AtmosphereManager calls GlobalRegistry registration during initialization."));
            }

            if (!atm.celestialRegistered)
            {
                f.Add(H8Finding.Create(
                    "CELESTIAL_NOT_REGISTERED", H8Severity.Warning, H8FindingCategory.Atmosphere,
                    "CelestialEngineRuntime is not registered in GlobalRegistry",
                    $"celestialRegistered={atm.celestialRegistered}",
                    "false",
                    "Other systems cannot query sun position, time of day, or moon phase through the registry.",
                    80,
                    "Check if CelestialEngine registers in GlobalRegistry during bootstrap.",
                    "Ensure CelestialEngine calls GlobalRegistry registration during initialization."));
            }

            if (!atm.skyboxAssigned)
            {
                f.Add(H8Finding.Create(
                    "SKYBOX_NULL", H8Severity.Warning, H8FindingCategory.Atmosphere,
                    "No skybox material assigned in RenderSettings",
                    $"skyboxAssigned={atm.skyboxAssigned}, skyboxMaterialName={atm.skyboxMaterialName}",
                    "null",
                    "Without a skybox material, the sky background will be a solid color or black.",
                    75,
                    "Check RenderSettings.skybox in the Lighting window.",
                    "Assign a skybox material in RenderSettings or via AtmosphereManager."));
            }

            if (!atm.sunAssigned)
            {
                f.Add(H8Finding.Create(
                    "SUN_NULL", H8Severity.Warning, H8FindingCategory.Atmosphere,
                    "No sun light assigned in RenderSettings",
                    $"sunAssigned={atm.sunAssigned}, sunName={atm.sunName}",
                    "null",
                    "Without a sun reference, ambient lighting, shadow direction, and specular may be wrong.",
                    80,
                    "Check RenderSettings.sun in the Lighting window.",
                    "Assign the primary directional light as the sun in RenderSettings or via CelestialEngine."));
            }

            if (atm.directionalLightCount == 0)
            {
                f.Add(H8Finding.Create(
                    "NO_ACTIVE_DIRECTIONAL_LIGHT", H8Severity.Warning, H8FindingCategory.Atmosphere,
                    "No active directional lights found in scene",
                    $"directionalLightCount={atm.directionalLightCount}",
                    "0",
                    "Without any directional light, the scene will have no direct lighting, no shadows, and Crest specular breaks.",
                    75,
                    "Check if any directional lights exist and are enabled in the scene.",
                    "Add or enable a directional light for the sun."));
            }

            if ((!atm.atmosphereManagerFound || !atm.celestialEngineFound) && reg.registryPhase != 2)
            {
                f.Add(H8Finding.Create(
                    "SKY_DEPENDS_ON_BOOTSTRAP", H8Severity.Error, H8FindingCategory.Atmosphere,
                    "Atmosphere/Celestial missing and bootstrap not complete — likely bootstrap-order issue",
                    $"atmosphereFound={atm.atmosphereManagerFound}, celestialFound={atm.celestialEngineFound}, registryPhase={reg.registryPhase}",
                    $"phase={reg.registryPhase}",
                    "These managers are spawned or activated during bootstrap. Without bootstrap completion, they won't exist.",
                    90,
                    "Run from 00_BOOTSTRAP and re-check after bootstrap completes.",
                    "Start from 00_BOOTSTRAP scene. These managers should appear after bootstrap phase 2."));
            }

            return f;
        }

        // ── 6. URP ───────────────────────────────────────────────────────────

        private static List<H8Finding> AnalyzeUrp(H8DiagnosticSnapshot s)
        {
            var f   = new List<H8Finding>();
            var urp = s.urp;

            if (string.IsNullOrEmpty(urp.activeUrpAssetName))
            {
                f.Add(H8Finding.Create(
                    "URP_PIPELINE_NULL", H8Severity.Critical, H8FindingCategory.Urp,
                    "No active URP pipeline asset — rendering will use built-in or fail",
                    $"activeUrpAssetName='{urp.activeUrpAssetName}', defaultPipeline='{urp.defaultRenderPipelineAsset}', qualityPipeline='{urp.qualityRenderPipelineAsset}'",
                    "null",
                    "Without URP, all URP shaders, render features, and post-processing are broken. The game cannot render correctly.",
                    95,
                    "Check Graphics Settings and Quality Settings for the render pipeline asset.",
                    "Assign the correct URP pipeline asset in Quality Settings and/or Graphics Settings."));
            }

            if (!string.IsNullOrEmpty(urp.qualityRenderPipelineAsset) &&
                !string.IsNullOrEmpty(urp.defaultRenderPipelineAsset) &&
                urp.qualityRenderPipelineAsset != urp.defaultRenderPipelineAsset)
            {
                f.Add(H8Finding.Create(
                    "URP_QUALITY_PIPELINE_MISMATCH", H8Severity.Warning, H8FindingCategory.Urp,
                    "Quality-level URP asset differs from default Graphics pipeline asset",
                    $"quality='{urp.qualityRenderPipelineAsset}', default='{urp.defaultRenderPipelineAsset}'",
                    $"quality={urp.qualityRenderPipelineAsset} vs default={urp.defaultRenderPipelineAsset}",
                    "Pipeline mismatch can cause unexpected rendering differences between quality levels. May be intentional for scalability.",
                    60,
                    "Verify this is intentional in Project Settings > Quality > Rendering.",
                    "If unintentional, align the quality-level pipeline asset with the default."));
            }

            if (string.IsNullOrEmpty(urp.activeRendererDataName))
            {
                f.Add(H8Finding.Create(
                    "URP_RENDERER_DATA_NOT_FOUND", H8Severity.Error, H8FindingCategory.Urp,
                    "No active URP Renderer Data found",
                    $"activeRendererDataName='{urp.activeRendererDataName}'",
                    "empty",
                    "Without renderer data, URP cannot configure render passes, features, or post-processing.",
                    85,
                    "Check the URP pipeline asset's Renderer List in the Inspector.",
                    "Assign a valid Universal Renderer Data asset to the URP pipeline asset."));
            }

            // Check Hecton8-specific renderer features
            var hectonFeatures = new string[] {
                "HectonSinglePassOceanFeature", "HectonDeferredCausticsFeature",
                "HectonVolumetricParticulateFogFeature", "HectonNoirDepthFogFeature",
                "HectonFluidAdvectionRenderFeature"
            };

            foreach (var featureName in hectonFeatures)
            {
                H8UrpFeatureInfo found = null;
                for (int i = 0; i < urp.rendererFeatures.Count; i++)
                {
                    if (urp.rendererFeatures[i].name.IndexOf(featureName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        urp.rendererFeatures[i].typeName.IndexOf(featureName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        found = urp.rendererFeatures[i];
                        break;
                    }
                }

                if (found != null && !found.isActive)
                {
                    f.Add(H8Finding.Create(
                        $"URP_FEATURE_DISABLED_{featureName.ToUpperInvariant()}",
                        H8Severity.Warning, H8FindingCategory.Urp,
                        $"URP renderer feature '{found.name}' is disabled",
                        $"feature={found.name}, typeName={found.typeName}, isActive={found.isActive}",
                        $"active={found.isActive}",
                        $"The {featureName} render feature exists but is turned off. Its visual effect will not appear.",
                        75,
                        "Check the URP Renderer Data asset and enable the feature.",
                        $"Enable {found.name} in the Universal Renderer Data asset."));
                }
                else if (found == null)
                {
                    f.Add(H8Finding.Create(
                        $"URP_FEATURE_MISSING_{featureName.ToUpperInvariant()}",
                        H8Severity.Info, H8FindingCategory.Urp,
                        $"URP renderer feature '{featureName}' not found in renderer data",
                        $"searchedFor={featureName}, totalFeatures={urp.rendererFeatures.Count}",
                        "not found",
                        $"{featureName} may not be implemented yet, or may be named differently.",
                        50,
                        "Check if this feature is expected in the current project state.",
                        $"If needed, add {featureName} as a renderer feature in the URP Renderer Data."));
                }
            }

            return f;
        }

        // ── 7. Cameras ───────────────────────────────────────────────────────

        private static List<H8Finding> AnalyzeCameras(H8DiagnosticSnapshot s)
        {
            var f    = new List<H8Finding>();
            var cams = s.cameras;

            if (cams == null || cams.Count == 0)
            {
                f.Add(H8Finding.Create(
                    "NO_ACTIVE_CAMERA", H8Severity.Critical, H8FindingCategory.Camera,
                    "No cameras found in the scene at all",
                    "cameras.Count=0",
                    "0 cameras",
                    "Without any camera, nothing will render. The Game view will be blank.",
                    95,
                    "Check if any camera GameObjects exist in loaded scenes.",
                    "Ensure a camera exists. It may be spawned during bootstrap."));
                return f;
            }

            // Count active cameras and main cameras
            bool hasActiveCamera    = false;
            int mainCameraCount     = 0;
            H8CameraInfo mainCamera = null;

            for (int i = 0; i < cams.Count; i++)
            {
                if (cams[i].activeInHierarchy && cams[i].enabled)
                    hasActiveCamera = true;
                if (cams[i].isMainCamera)
                {
                    mainCameraCount++;
                    if (mainCamera == null) mainCamera = cams[i];
                }
            }

            if (!hasActiveCamera)
            {
                f.Add(H8Finding.Create(
                    "NO_ACTIVE_CAMERA", H8Severity.Critical, H8FindingCategory.Camera,
                    "No active and enabled camera found",
                    $"totalCameras={cams.Count}, activeAndEnabled=0",
                    "0 active",
                    "All cameras are either disabled or on inactive GameObjects. Nothing will render.",
                    95,
                    "Check camera GameObjects' active state and Camera component enabled state.",
                    "Enable at least one camera and its host GameObject."));
            }

            if (mainCameraCount == 0)
            {
                f.Add(H8Finding.Create(
                    "NO_MAIN_CAMERA", H8Severity.Error, H8FindingCategory.Camera,
                    "No camera tagged MainCamera found",
                    $"totalCameras={cams.Count}, mainCameraCount=0",
                    "0 main cameras",
                    "Many systems use Camera.main. Without a MainCamera tag, they will get null and fail.",
                    85,
                    "Check camera tags in the Inspector.",
                    "Tag the primary gameplay camera as MainCamera."));
            }

            if (mainCameraCount > 1)
            {
                f.Add(H8Finding.Create(
                    "MULTIPLE_MAIN_CAMERAS", H8Severity.Warning, H8FindingCategory.Camera,
                    $"Multiple cameras tagged MainCamera found ({mainCameraCount})",
                    $"mainCameraCount={mainCameraCount}",
                    $"{mainCameraCount} main cameras",
                    "Camera.main returns one arbitrary MainCamera-tagged camera. Multiple tags cause unpredictable behavior.",
                    70,
                    "Search for all GameObjects tagged MainCamera.",
                    "Remove the MainCamera tag from all but the primary gameplay camera."));
            }

            if (mainCamera != null && (!mainCamera.activeInHierarchy || !mainCamera.enabled))
            {
                f.Add(H8Finding.Create(
                    "MAIN_CAMERA_DISABLED", H8Severity.Error, H8FindingCategory.Camera,
                    "Main camera is tagged MainCamera but is disabled or inactive",
                    $"name={mainCamera.name}, activeInHierarchy={mainCamera.activeInHierarchy}, enabled={mainCamera.enabled}",
                    $"active={mainCamera.activeInHierarchy}, enabled={mainCamera.enabled}",
                    "Camera.main may return this camera, but it won't render because it's off.",
                    90,
                    "Check main camera's active and enabled state.",
                    "Enable the main camera component and activate its GameObject."));
            }

            if (mainCamera != null && mainCamera.hasTargetTexture)
            {
                f.Add(H8Finding.Create(
                    "CAMERA_TARGET_TEXTURE_SET", H8Severity.Warning, H8FindingCategory.Camera,
                    $"Main camera '{mainCamera.name}' has a target texture assigned ({mainCamera.targetTextureName})",
                    $"hasTargetTexture={mainCamera.hasTargetTexture}, targetTexture={mainCamera.targetTextureName}",
                    $"targetTexture={mainCamera.targetTextureName}",
                    "A target texture means the camera renders to an RT instead of the screen. The Game view may be blank.",
                    70,
                    "Check if the target texture is intentional (e.g. for VR, picture-in-picture, or post-processing).",
                    "Remove the target texture if the camera should render directly to screen."));
            }

            // Check layer culling on main camera
            if (mainCamera != null)
            {
                int terrainLayer = LayerMask.NameToLayer("Terrain");
                if (terrainLayer >= 0 && (mainCamera.cullingMask & (1 << terrainLayer)) == 0)
                {
                    f.Add(H8Finding.Create(
                        "CAMERA_CULLS_TERRAIN_LAYER", H8Severity.Warning, H8FindingCategory.Camera,
                        "Main camera culling mask excludes the Terrain layer",
                        $"cullingMask={mainCamera.cullingMask}, terrainLayer={terrainLayer}, culledLayers=[{string.Join(", ", mainCamera.culledLayerNames)}]",
                        $"Terrain layer ({terrainLayer}) excluded",
                        "The camera will not render any terrain. The world ground will be invisible.",
                        75,
                        "Check the camera's Culling Mask in the Inspector.",
                        "Add the Terrain layer to the main camera's culling mask."));
                }

                int envLayer = LayerMask.NameToLayer("Environment");
                if (envLayer >= 0 && (mainCamera.cullingMask & (1 << envLayer)) == 0)
                {
                    f.Add(H8Finding.Create(
                        "CAMERA_CULLS_ENVIRONMENT_LAYER", H8Severity.Warning, H8FindingCategory.Camera,
                        "Main camera culling mask excludes the Environment layer",
                        $"cullingMask={mainCamera.cullingMask}, environmentLayer={envLayer}, culledLayers=[{string.Join(", ", mainCamera.culledLayerNames)}]",
                        $"Environment layer ({envLayer}) excluded",
                        "The camera will not render environment objects like rocks, props, and structures.",
                        70,
                        "Check the camera's Culling Mask in the Inspector.",
                        "Add the Environment layer to the main camera's culling mask."));
                }

                if (mainCamera.farClip < 100f)
                {
                    f.Add(H8Finding.Create(
                        "CAMERA_FAR_CLIP_TOO_SMALL", H8Severity.Warning, H8FindingCategory.Camera,
                        $"Main camera far clip plane is very small ({mainCamera.farClip})",
                        $"farClip={mainCamera.farClip}",
                        $"{mainCamera.farClip}",
                        "A far clip under 100 will clip terrain, ocean, and distant objects. Open-world needs 1000+.",
                        60,
                        "Check camera far clip in Inspector. May be intentional for a special camera.",
                        "Increase the far clip plane to at least 1000 for the main gameplay camera."));
                }
            }

            return f;
        }

        // ── 8. Console ───────────────────────────────────────────────────────

        private static List<H8Finding> AnalyzeConsole(H8DiagnosticSnapshot s)
        {
            var f       = new List<H8Finding>();
            var console = s.console;

            if (console.totalErrors > 0)
            {
                H8Severity sev;
                if (console.totalErrors >= 20) sev = H8Severity.Critical;
                else if (console.totalErrors >= 5) sev = H8Severity.Error;
                else sev = H8Severity.Warning;

                f.Add(H8Finding.Create(
                    "CONSOLE_ERRORS_PRESENT", sev, H8FindingCategory.Console,
                    $"Console contains {console.totalErrors} error(s)",
                    $"totalErrors={console.totalErrors}, totalWarnings={console.totalWarnings}, totalLogs={console.totalLogs}",
                    $"{console.totalErrors} errors",
                    "Console errors indicate runtime failures. High counts suggest systemic issues like missing bootstrap or broken references.",
                    sev == H8Severity.Critical ? 90 : (sev == H8Severity.Error ? 85 : 75),
                    "Read the console errors to identify the root cause. First error is often the trigger.",
                    "Fix the earliest console error first — later errors are often cascading consequences."));
            }

            // Pattern-match known error categories
            var patterns = new (string keyword, string id, string title, string why)[]
            {
                ("NullReferenceException", "CONSOLE_NULLREF",
                    "NullReferenceException(s) in console",
                    "A null reference usually means a missing service, uninitialized field, or destroyed object access."),
                ("Bootstrap", "CONSOLE_BOOTSTRAP_ERROR",
                    "Bootstrap-related error in console",
                    "Bootstrap errors can prevent all subsystems from initializing."),
                ("GlobalRegistry", "CONSOLE_REGISTRY_ERROR",
                    "GlobalRegistry-related error in console",
                    "Registry errors mean services failed to register or resolve."),
                ("MapMagic", "CONSOLE_MAPMAGIC_ERROR",
                    "MapMagic-related error in console",
                    "MapMagic errors can prevent terrain generation."),
                ("Crest", "CONSOLE_CREST_ERROR",
                    "Crest/Ocean-related error in console",
                    "Crest errors affect ocean rendering and wave simulation."),
                ("URP", "CONSOLE_URP_ERROR",
                    "URP pipeline error in console",
                    "URP errors affect all rendering — shaders, features, and post-processing."),
                ("Shader", "CONSOLE_SHADER_ERROR",
                    "Shader error in console",
                    "Shader errors cause pink/magenta materials and visual corruption."),
                ("Addressable", "CONSOLE_ADDRESSABLES_ERROR",
                    "Addressables error in console",
                    "Addressables errors mean assets failed to load from bundles or catalogs."),
            };

            foreach (var (keyword, id, title, why) in patterns)
            {
                int matchCount = 0;
                string firstMessage = "";
                for (int i = 0; i < console.entries.Count; i++)
                {
                    var entry = console.entries[i];
                    if (entry.type != "Error") continue;
                    if (entry.message.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        entry.category.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchCount++;
                        if (matchCount == 1) firstMessage = H8Utils.Truncate(entry.message, 200);
                    }
                }

                if (matchCount > 0)
                {
                    f.Add(H8Finding.Create(
                        id, H8Severity.Error, H8FindingCategory.Console,
                        $"{title} ({matchCount} occurrence{(matchCount > 1 ? "s" : "")})",
                        $"keyword={keyword}, matches={matchCount}, first=\"{firstMessage}\"",
                        $"{matchCount} {keyword} errors",
                        why,
                        80,
                        $"Search console for '{keyword}' and inspect the first occurrence's full stack trace.",
                        $"Fix the root cause of the {keyword} error. The first occurrence is often the trigger."));
                }
            }

            return f;
        }
    }
}
