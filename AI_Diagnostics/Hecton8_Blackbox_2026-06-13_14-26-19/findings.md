# Hecton8 Blackbox Findings (findings.md)

## Critical Findings
### [BOOTSTRAP_NOT_STARTED] Bootstrap has not started — registry phase is 0 and this is not the bootstrap scene
- **Category:** Bootstrap
- **Evidence:** registryPhase=0, isBootstrapScene=False, activeScene=02_HECTON_WORLD
- **Measured Value:** `registryPhase=0, scene=02_HECTON_WORLD`
- **Why It Matters:** The game was likely started from a non-bootstrap scene. No services are initialized.
- **Confidence:** 95%
- **Next Check:** Verify scene load order in Build Settings. 00_BOOTSTRAP must be scene 0.
- **Likely Fix:** Start Play Mode from 00_BOOTSTRAP, or add an auto-bootstrap mechanism.

### [DIRECT_WORLD_SCENE_START_DETECTED] 02_HECTON_WORLD is active but bootstrap did not complete
- **Category:** Bootstrap
- **Evidence:** activeScene=02_HECTON_WORLD, registryPhase=0
- **Measured Value:** `phase=0`
- **Why It Matters:** Direct world-scene start bypasses bootstrap. All registry slots will be null, causing cascading NullReferenceExceptions.
- **Confidence:** 95%
- **Next Check:** Check if Play was pressed while 02_HECTON_WORLD was the active scene in the editor.
- **Likely Fix:** Always start from 00_BOOTSTRAP. Use EditorBuildSettings to enforce scene 0.

### [GLOBAL_REGISTRY_EMPTY_OR_UNREADY] GlobalRegistry phase is 0 and all service slots are null
- **Category:** Registry
- **Evidence:** registryPhase=0, slotCount=18, allNull=true
- **Measured Value:** `phase=0, all slots null`
- **Why It Matters:** The registry has not been populated at all. No subsystem can resolve its dependencies.
- **Confidence:** 95%
- **Next Check:** Verify bootstrap ran. Check if GlobalRegistry.Initialize() was called.
- **Likely Fix:** Start from 00_BOOTSTRAP scene or debug the bootstrap flow.

## Error Findings
### [CELESTIAL_ENGINE_MISSING] CelestialEngine not found in scene
- **Category:** Atmosphere
- **Evidence:** celestialEngineFound=False
- **Measured Value:** `not found`
- **Why It Matters:** No celestial engine means no sun/moon cycle, no dynamic lighting direction, and broken sky.
- **Confidence:** 90%
- **Next Check:** Check if CelestialEngine component exists in the world scene.
- **Likely Fix:** Ensure CelestialEngine is present and active in the world scene.

### [ATMOSPHERE_MANAGER_MISSING] AtmosphereManager not found in scene
- **Category:** Atmosphere
- **Evidence:** atmosphereManagerFound=False
- **Measured Value:** `not found`
- **Why It Matters:** No atmosphere manager means no dynamic sky, fog, or time-of-day transitions.
- **Confidence:** 90%
- **Next Check:** Check if AtmosphereManager component exists in the world scene.
- **Likely Fix:** Ensure AtmosphereManager is present and active in the world scene.

### [OCEAN_KINEMATICS_NOT_REGISTERED] OceanKinematics is not registered in GlobalRegistry
- **Category:** Crest
- **Evidence:** kinematicsRegistered=False
- **Measured Value:** `false`
- **Why It Matters:** Gameplay systems that query wave height, buoyancy, or water level cannot resolve the ocean service.
- **Confidence:** 85%
- **Next Check:** Check if the ocean kinematics provider registers in GlobalRegistry during bootstrap.
- **Likely Fix:** Ensure the Crest adapter or kinematics provider calls GlobalRegistry.Register during init.

### [OCEAN_PRIMARY_LIGHT_NULL] OceanRenderer._primaryLight is null
- **Category:** Crest
- **Evidence:** primaryLightAssigned=False
- **Measured Value:** `null`
- **Why It Matters:** Without a primary light, Crest's specular, caustics, and underwater lighting will fail or look wrong.
- **Confidence:** 85%
- **Next Check:** Check if a directional light is assigned or discoverable by Crest.
- **Likely Fix:** Ensure a Sun directional light exists and is assigned to OceanRenderer.

### [OCEAN_VIEW_CAMERA_NULL] OceanRenderer._viewCamera is null
- **Category:** Crest
- **Evidence:** viewCameraAssigned=False
- **Measured Value:** `null`
- **Why It Matters:** Crest cannot determine which camera to generate ocean LODs for. Ocean may not render correctly.
- **Confidence:** 85%
- **Next Check:** Check OceanRenderer's ViewCamera reference in Play Mode.
- **Likely Fix:** Ensure a main camera exists and Crest can discover it, or assign it explicitly.

### [TICK_DISPATCHER_MISSING] TickManager or Dispatcher slot is null — game loop may not run
- **Category:** Registry
- **Evidence:** TickManager.isNull=True, Dispatcher.isNull=True
- **Measured Value:** `TickManager=True, Dispatcher=True`
- **Why It Matters:** Without the tick dispatcher, IUpdatable/ITickable systems will not receive updates and the game loop stalls.
- **Confidence:** 90%
- **Next Check:** Check if GameTickManager or equivalent dispatcher exists and registers in GlobalRegistry.
- **Likely Fix:** Ensure tick dispatcher is present in the bootstrap scene and registers during initialization.

### [GLOBAL_REGISTRY_SLOT_NULL_DISPATCHER] Registry slot 'Dispatcher' is null
- **Category:** Registry
- **Evidence:** slot=Dispatcher, isNull=true, registryPhase=0
- **Measured Value:** `null`
- **Why It Matters:** The Dispatcher service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the Dispatcher provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the Dispatcher provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_TICKMANAGER] Registry slot 'TickManager' is null
- **Category:** Registry
- **Evidence:** slot=TickManager, isNull=true, registryPhase=0
- **Measured Value:** `null`
- **Why It Matters:** The TickManager service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the TickManager provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the TickManager provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_TERRAINPROVIDERRUNTIME] Registry slot 'TerrainProviderRuntime' is null
- **Category:** Registry
- **Evidence:** slot=TerrainProviderRuntime, isNull=true, registryPhase=0
- **Measured Value:** `null`
- **Why It Matters:** The TerrainProviderRuntime service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the TerrainProviderRuntime provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the TerrainProviderRuntime provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_CELESTIALENGINERUNTIME] Registry slot 'CelestialEngineRuntime' is null
- **Category:** Registry
- **Evidence:** slot=CelestialEngineRuntime, isNull=true, registryPhase=0
- **Measured Value:** `null`
- **Why It Matters:** The CelestialEngineRuntime service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the CelestialEngineRuntime provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the CelestialEngineRuntime provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_MAPMAGICRUNTIME] Registry slot 'MapMagicRuntime' is null
- **Category:** Registry
- **Evidence:** slot=MapMagicRuntime, isNull=true, registryPhase=0
- **Measured Value:** `null`
- **Why It Matters:** The MapMagicRuntime service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the MapMagicRuntime provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the MapMagicRuntime provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_OCEANKINEMATICS] Registry slot 'OceanKinematics' is null
- **Category:** Registry
- **Evidence:** slot=OceanKinematics, isNull=true, registryPhase=0
- **Measured Value:** `null`
- **Why It Matters:** The OceanKinematics service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the OceanKinematics provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the OceanKinematics provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_PLAYER] Registry slot 'Player' is null
- **Category:** Registry
- **Evidence:** slot=Player, isNull=true, registryPhase=0
- **Measured Value:** `null`
- **Why It Matters:** The Player service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the Player provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the Player provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_UI] Registry slot 'UI' is null
- **Category:** Registry
- **Evidence:** slot=UI, isNull=true, registryPhase=0
- **Measured Value:** `null`
- **Why It Matters:** The UI service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the UI provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the UI provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_SAVE] Registry slot 'Save' is null
- **Category:** Registry
- **Evidence:** slot=Save, isNull=true, registryPhase=0
- **Measured Value:** `null`
- **Why It Matters:** The Save service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the Save provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the Save provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_SCENE] Registry slot 'Scene' is null
- **Category:** Registry
- **Evidence:** slot=Scene, isNull=true, registryPhase=0
- **Measured Value:** `null`
- **Why It Matters:** The Scene service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the Scene provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the Scene provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_AUDIO] Registry slot 'Audio' is null
- **Category:** Registry
- **Evidence:** slot=Audio, isNull=true, registryPhase=0
- **Measured Value:** `null`
- **Why It Matters:** The Audio service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the Audio provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the Audio provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_PHYSICS] Registry slot 'Physics' is null
- **Category:** Registry
- **Evidence:** slot=Physics, isNull=true, registryPhase=0
- **Measured Value:** `null`
- **Why It Matters:** The Physics service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the Physics provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the Physics provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_INPUT] Registry slot 'Input' is null
- **Category:** Registry
- **Evidence:** slot=Input, isNull=true, registryPhase=0
- **Measured Value:** `null`
- **Why It Matters:** The Input service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the Input provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the Input provider is present and its registration runs during bootstrap.

### [BOOTSTRAP_NOT_COMPLETE] Bootstrapper found but registry phase is not Complete (2)
- **Category:** Bootstrap
- **Evidence:** bootstrapperFound=True, registryPhase=0, phaseName=Uninitialized
- **Measured Value:** `phase=0 (Uninitialized)`
- **Why It Matters:** Bootstrap started but did not finish. Some services may be partially initialized, causing unpredictable failures.
- **Confidence:** 90%
- **Next Check:** Check console for errors during bootstrap. Look for exceptions in GameBootstrapper or GlobalRegistry init.
- **Likely Fix:** Debug the bootstrap sequence. Check for missing dependencies or exceptions during initialization.

### [GLOBAL_REGISTRY_SLOT_NULL_ATMOSPHERERUNTIME] Registry slot 'AtmosphereRuntime' is null
- **Category:** Registry
- **Evidence:** slot=AtmosphereRuntime, isNull=true, registryPhase=0
- **Measured Value:** `null`
- **Why It Matters:** The AtmosphereRuntime service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the AtmosphereRuntime provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the AtmosphereRuntime provider is present and its registration runs during bootstrap.

### [SKY_DEPENDS_ON_BOOTSTRAP] Atmosphere/Celestial missing and bootstrap not complete — likely bootstrap-order issue
- **Category:** Atmosphere
- **Evidence:** atmosphereFound=False, celestialFound=False, registryPhase=0
- **Measured Value:** `phase=0`
- **Why It Matters:** These managers are spawned or activated during bootstrap. Without bootstrap completion, they won't exist.
- **Confidence:** 90%
- **Next Check:** Run from 00_BOOTSTRAP and re-check after bootstrap completes.
- **Likely Fix:** Start from 00_BOOTSTRAP scene. These managers should appear after bootstrap phase 2.

## Warning Findings
### [SUN_NULL] No sun light assigned in RenderSettings
- **Category:** Atmosphere
- **Evidence:** sunAssigned=False, sunName=null
- **Measured Value:** `null`
- **Why It Matters:** Without a sun reference, ambient lighting, shadow direction, and specular may be wrong.
- **Confidence:** 80%
- **Next Check:** Check RenderSettings.sun in the Lighting window.
- **Likely Fix:** Assign the primary directional light as the sun in RenderSettings or via CelestialEngine.

### [MAPMAGIC_NOT_REGISTERED] MapMagic is not registered in GlobalRegistry
- **Category:** MapMagic
- **Evidence:** registeredInGlobalRegistry=False
- **Measured Value:** `false`
- **Why It Matters:** Other systems cannot discover MapMagic through the registry. Terrain queries may fail.
- **Confidence:** 80%
- **Next Check:** Check if the MapMagic bridge registers itself during bootstrap.
- **Likely Fix:** Ensure the bridge component calls GlobalRegistry registration during initialization.

### [OCEAN_VIEWPOINT_NULL] OceanRenderer._viewpoint is null
- **Category:** Crest
- **Evidence:** viewpointAssigned=False
- **Measured Value:** `null`
- **Why It Matters:** No explicit viewpoint assigned. Crest will fall back to the view camera, which usually works but may cause LOD jitter.
- **Confidence:** 80%
- **Next Check:** Check OceanRenderer viewpoint field. This may be expected if relying on camera fallback.
- **Likely Fix:** Optionally assign a viewpoint transform to OceanRenderer for stable LOD transitions.

### [ATMOSPHERE_NOT_REGISTERED] AtmosphereRuntime is not registered in GlobalRegistry
- **Category:** Atmosphere
- **Evidence:** atmosphereRegistered=False
- **Measured Value:** `false`
- **Why It Matters:** Other systems cannot query atmosphere state (fog, sky color, weather) through the registry.
- **Confidence:** 80%
- **Next Check:** Check if AtmosphereManager registers in GlobalRegistry during bootstrap.
- **Likely Fix:** Ensure AtmosphereManager calls GlobalRegistry registration during initialization.

### [CELESTIAL_NOT_REGISTERED] CelestialEngineRuntime is not registered in GlobalRegistry
- **Category:** Atmosphere
- **Evidence:** celestialRegistered=False
- **Measured Value:** `false`
- **Why It Matters:** Other systems cannot query sun position, time of day, or moon phase through the registry.
- **Confidence:** 80%
- **Next Check:** Check if CelestialEngine registers in GlobalRegistry during bootstrap.
- **Likely Fix:** Ensure CelestialEngine calls GlobalRegistry registration during initialization.

## Info Findings
_None._

