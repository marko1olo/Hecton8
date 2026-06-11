# Hecton8 Blackbox Findings (findings_direct.md)

## Critical Findings
### [DIRECT_WORLD_SCENE_START_DETECTED] 02_HECTON_WORLD is active but bootstrap did not complete
- **Category:** Bootstrap
- **Evidence:** activeScene=02_HECTON_WORLD, registryPhase=1
- **Measured Value:** `phase=1`
- **Why It Matters:** Direct world-scene start bypasses bootstrap. All registry slots will be null, causing cascading NullReferenceExceptions.
- **Confidence:** 95%
- **Next Check:** Check if Play was pressed while 02_HECTON_WORLD was the active scene in the editor.
- **Likely Fix:** Always start from 00_BOOTSTRAP. Use EditorBuildSettings to enforce scene 0.

### [NO_ACTIVE_CAMERA] No cameras found in the scene at all
- **Category:** Camera
- **Evidence:** cameras.Count=0
- **Measured Value:** `0 cameras`
- **Why It Matters:** Without any camera, nothing will render. The Game view will be blank.
- **Confidence:** 95%
- **Next Check:** Check if any camera GameObjects exist in loaded scenes.
- **Likely Fix:** Ensure a camera exists. It may be spawned during bootstrap.

## Error Findings
### [GLOBAL_REGISTRY_SLOT_NULL_INPUT] Registry slot 'Input' is null
- **Category:** Registry
- **Evidence:** slot=Input, isNull=true, registryPhase=1
- **Measured Value:** `null`
- **Why It Matters:** The Input service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the Input provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the Input provider is present and its registration runs during bootstrap.

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

### [BOOTSTRAP_NOT_COMPLETE] Bootstrapper found but registry phase is not Complete (2)
- **Category:** Bootstrap
- **Evidence:** bootstrapperFound=True, registryPhase=1, phaseName=Registering
- **Measured Value:** `phase=1 (Registering)`
- **Why It Matters:** Bootstrap started but did not finish. Some services may be partially initialized, causing unpredictable failures.
- **Confidence:** 90%
- **Next Check:** Check console for errors during bootstrap. Look for exceptions in GameBootstrapper or GlobalRegistry init.
- **Likely Fix:** Debug the bootstrap sequence. Check for missing dependencies or exceptions during initialization.

### [SKY_DEPENDS_ON_BOOTSTRAP] Atmosphere/Celestial missing and bootstrap not complete — likely bootstrap-order issue
- **Category:** Atmosphere
- **Evidence:** atmosphereFound=False, celestialFound=False, registryPhase=1
- **Measured Value:** `phase=1`
- **Why It Matters:** These managers are spawned or activated during bootstrap. Without bootstrap completion, they won't exist.
- **Confidence:** 90%
- **Next Check:** Run from 00_BOOTSTRAP and re-check after bootstrap completes.
- **Likely Fix:** Start from 00_BOOTSTRAP scene. These managers should appear after bootstrap phase 2.

### [GLOBAL_REGISTRY_SLOT_NULL_DISPATCHER] Registry slot 'Dispatcher' is null
- **Category:** Registry
- **Evidence:** slot=Dispatcher, isNull=true, registryPhase=1
- **Measured Value:** `null`
- **Why It Matters:** The Dispatcher service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the Dispatcher provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the Dispatcher provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_TICKMANAGER] Registry slot 'TickManager' is null
- **Category:** Registry
- **Evidence:** slot=TickManager, isNull=true, registryPhase=1
- **Measured Value:** `null`
- **Why It Matters:** The TickManager service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the TickManager provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the TickManager provider is present and its registration runs during bootstrap.

### [TICK_DISPATCHER_MISSING] TickManager or Dispatcher slot is null — game loop may not run
- **Category:** Registry
- **Evidence:** TickManager.isNull=True, Dispatcher.isNull=True
- **Measured Value:** `TickManager=True, Dispatcher=True`
- **Why It Matters:** Without the tick dispatcher, IUpdatable/ITickable systems will not receive updates and the game loop stalls.
- **Confidence:** 90%
- **Next Check:** Check if GameTickManager or equivalent dispatcher exists and registers in GlobalRegistry.
- **Likely Fix:** Ensure tick dispatcher is present in the bootstrap scene and registers during initialization.

### [GLOBAL_REGISTRY_SLOT_NULL_ATMOSPHERERUNTIME] Registry slot 'AtmosphereRuntime' is null
- **Category:** Registry
- **Evidence:** slot=AtmosphereRuntime, isNull=true, registryPhase=1
- **Measured Value:** `null`
- **Why It Matters:** The AtmosphereRuntime service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the AtmosphereRuntime provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the AtmosphereRuntime provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_PHYSICS] Registry slot 'Physics' is null
- **Category:** Registry
- **Evidence:** slot=Physics, isNull=true, registryPhase=1
- **Measured Value:** `null`
- **Why It Matters:** The Physics service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the Physics provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the Physics provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_AUDIO] Registry slot 'Audio' is null
- **Category:** Registry
- **Evidence:** slot=Audio, isNull=true, registryPhase=1
- **Measured Value:** `null`
- **Why It Matters:** The Audio service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the Audio provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the Audio provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_CELESTIALENGINERUNTIME] Registry slot 'CelestialEngineRuntime' is null
- **Category:** Registry
- **Evidence:** slot=CelestialEngineRuntime, isNull=true, registryPhase=1
- **Measured Value:** `null`
- **Why It Matters:** The CelestialEngineRuntime service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the CelestialEngineRuntime provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the CelestialEngineRuntime provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_SAVE] Registry slot 'Save' is null
- **Category:** Registry
- **Evidence:** slot=Save, isNull=true, registryPhase=1
- **Measured Value:** `null`
- **Why It Matters:** The Save service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the Save provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the Save provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_SCENE] Registry slot 'Scene' is null
- **Category:** Registry
- **Evidence:** slot=Scene, isNull=true, registryPhase=1
- **Measured Value:** `null`
- **Why It Matters:** The Scene service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the Scene provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the Scene provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_PLAYER] Registry slot 'Player' is null
- **Category:** Registry
- **Evidence:** slot=Player, isNull=true, registryPhase=1
- **Measured Value:** `null`
- **Why It Matters:** The Player service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the Player provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the Player provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_OCEANKINEMATICS] Registry slot 'OceanKinematics' is null
- **Category:** Registry
- **Evidence:** slot=OceanKinematics, isNull=true, registryPhase=1
- **Measured Value:** `null`
- **Why It Matters:** The OceanKinematics service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the OceanKinematics provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the OceanKinematics provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_UI] Registry slot 'UI' is null
- **Category:** Registry
- **Evidence:** slot=UI, isNull=true, registryPhase=1
- **Measured Value:** `null`
- **Why It Matters:** The UI service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the UI provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the UI provider is present and its registration runs during bootstrap.

## Warning Findings
### [OCEAN_VIEWPOINT_NULL] OceanRenderer._viewpoint is null
- **Category:** Crest
- **Evidence:** viewpointAssigned=False
- **Measured Value:** `null`
- **Why It Matters:** No explicit viewpoint assigned. Crest will fall back to the view camera, which usually works but may cause LOD jitter.
- **Confidence:** 80%
- **Next Check:** Check OceanRenderer viewpoint field. This may be expected if relying on camera fallback.
- **Likely Fix:** Optionally assign a viewpoint transform to OceanRenderer for stable LOD transitions.

### [MAPMAGIC_NOT_REGISTERED] MapMagic is not registered in GlobalRegistry
- **Category:** MapMagic
- **Evidence:** registeredInGlobalRegistry=False
- **Measured Value:** `false`
- **Why It Matters:** Other systems cannot discover MapMagic through the registry. Terrain queries may fail.
- **Confidence:** 80%
- **Next Check:** Check if the MapMagic bridge registers itself during bootstrap.
- **Likely Fix:** Ensure the bridge component calls GlobalRegistry registration during initialization.

### [MAPMAGIC_NO_TERRAINS_GENERATED] No active terrains found in scene
- **Category:** MapMagic
- **Evidence:** activeTerrainCount=0, allTerrainCount=0
- **Measured Value:** `active=0, total=0`
- **Why It Matters:** Terrain generation may not have run yet, or all terrains were destroyed/disabled.
- **Confidence:** 75%
- **Next Check:** Enter Play Mode from 00_BOOTSTRAP and wait for terrain generation. Check MapMagic logs.
- **Likely Fix:** Ensure MapMagic generates at least one terrain tile in Play Mode.

### [UNDERWATER_RENDERER_MISSING] UnderwaterRenderer not found
- **Category:** Crest
- **Evidence:** underwaterRendererFound=False
- **Measured Value:** `not found`
- **Why It Matters:** Without UnderwaterRenderer, the camera will not apply underwater fog, caustics, or meniscus effects.
- **Confidence:** 75%
- **Next Check:** Check if UnderwaterRenderer is attached to the main camera or Ocean_Crest hierarchy.
- **Likely Fix:** Add UnderwaterRenderer component to the main camera GameObject.

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

### [SUN_NULL] No sun light assigned in RenderSettings
- **Category:** Atmosphere
- **Evidence:** sunAssigned=False, sunName=null
- **Measured Value:** `null`
- **Why It Matters:** Without a sun reference, ambient lighting, shadow direction, and specular may be wrong.
- **Confidence:** 80%
- **Next Check:** Check RenderSettings.sun in the Lighting window.
- **Likely Fix:** Assign the primary directional light as the sun in RenderSettings or via CelestialEngine.

### [MAPMAGIC_BRIDGE_MISSING] MapMagic runtime bridge component not found
- **Category:** MapMagic
- **Evidence:** runtimeBridgeFound=False
- **Measured Value:** `not found`
- **Why It Matters:** The Hecton8 bridge to MapMagic is missing. Terrain events and integration may not work.
- **Confidence:** 80%
- **Next Check:** Check for the MapMagic bridge/adapter component in the scene.
- **Likely Fix:** Add the MapMagic runtime bridge component to the MapMagic GameObject.

## Info Findings
_None._

