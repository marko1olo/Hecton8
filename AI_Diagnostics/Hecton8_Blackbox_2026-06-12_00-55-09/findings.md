# Hecton8 Blackbox Findings (findings.md)

## Critical Findings
_None._

## Error Findings
### [BOOTSTRAP_NOT_COMPLETE] Bootstrapper found but registry phase is not Complete (2)
- **Category:** Bootstrap
- **Evidence:** bootstrapperFound=True, registryPhase=1, phaseName=Registering
- **Measured Value:** `phase=1 (Registering)`
- **Why It Matters:** Bootstrap started but did not finish. Some services may be partially initialized, causing unpredictable failures.
- **Confidence:** 90%
- **Next Check:** Check console for errors during bootstrap. Look for exceptions in GameBootstrapper or GlobalRegistry init.
- **Likely Fix:** Debug the bootstrap sequence. Check for missing dependencies or exceptions during initialization.

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

### [OCEAN_CREST_MISSING] Ocean_Crest GameObject not found in scene
- **Category:** Crest
- **Evidence:** oceanCrestObjectFound=False
- **Measured Value:** `not found`
- **Why It Matters:** The ocean root object is missing. No ocean surface will render.
- **Confidence:** 95%
- **Next Check:** Check if Ocean_Crest exists in the world scene hierarchy.
- **Likely Fix:** Ensure Ocean_Crest GameObject is present in 02_HECTON_WORLD.

### [MAPMAGIC_OBJECT_MISSING] MapMagicObject not found in scene
- **Category:** MapMagic
- **Evidence:** mapMagicObjectFound=False
- **Measured Value:** `not found`
- **Why It Matters:** Without MapMagic, terrain generation will not work and the world will have no terrain.
- **Confidence:** 90%
- **Next Check:** Check if a GameObject with MapMagicObject component exists in the world scene.
- **Likely Fix:** Ensure the MapMagic terrain generator is present in 02_HECTON_WORLD.

### [GLOBAL_REGISTRY_SLOT_NULL_TERRAINPROVIDERRUNTIME] Registry slot 'TerrainProviderRuntime' is null
- **Category:** Registry
- **Evidence:** slot=TerrainProviderRuntime, isNull=true, registryPhase=1
- **Measured Value:** `null`
- **Why It Matters:** The TerrainProviderRuntime service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the TerrainProviderRuntime provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the TerrainProviderRuntime provider is present and its registration runs during bootstrap.

### [SKY_DEPENDS_ON_BOOTSTRAP] Atmosphere/Celestial missing and bootstrap not complete — likely bootstrap-order issue
- **Category:** Atmosphere
- **Evidence:** atmosphereFound=False, celestialFound=False, registryPhase=1
- **Measured Value:** `phase=1`
- **Why It Matters:** These managers are spawned or activated during bootstrap. Without bootstrap completion, they won't exist.
- **Confidence:** 90%
- **Next Check:** Run from 00_BOOTSTRAP and re-check after bootstrap completes.
- **Likely Fix:** Start from 00_BOOTSTRAP scene. These managers should appear after bootstrap phase 2.

### [GLOBAL_REGISTRY_SLOT_NULL_CELESTIALENGINERUNTIME] Registry slot 'CelestialEngineRuntime' is null
- **Category:** Registry
- **Evidence:** slot=CelestialEngineRuntime, isNull=true, registryPhase=1
- **Measured Value:** `null`
- **Why It Matters:** The CelestialEngineRuntime service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the CelestialEngineRuntime provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the CelestialEngineRuntime provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_MAPMAGICRUNTIME] Registry slot 'MapMagicRuntime' is null
- **Category:** Registry
- **Evidence:** slot=MapMagicRuntime, isNull=true, registryPhase=1
- **Measured Value:** `null`
- **Why It Matters:** The MapMagicRuntime service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the MapMagicRuntime provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the MapMagicRuntime provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_OCEANKINEMATICS] Registry slot 'OceanKinematics' is null
- **Category:** Registry
- **Evidence:** slot=OceanKinematics, isNull=true, registryPhase=1
- **Measured Value:** `null`
- **Why It Matters:** The OceanKinematics service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the OceanKinematics provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the OceanKinematics provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_PLAYER] Registry slot 'Player' is null
- **Category:** Registry
- **Evidence:** slot=Player, isNull=true, registryPhase=1
- **Measured Value:** `null`
- **Why It Matters:** The Player service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the Player provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the Player provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_UI] Registry slot 'UI' is null
- **Category:** Registry
- **Evidence:** slot=UI, isNull=true, registryPhase=1
- **Measured Value:** `null`
- **Why It Matters:** The UI service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the UI provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the UI provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_AUDIO] Registry slot 'Audio' is null
- **Category:** Registry
- **Evidence:** slot=Audio, isNull=true, registryPhase=1
- **Measured Value:** `null`
- **Why It Matters:** The Audio service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the Audio provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the Audio provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_PHYSICS] Registry slot 'Physics' is null
- **Category:** Registry
- **Evidence:** slot=Physics, isNull=true, registryPhase=1
- **Measured Value:** `null`
- **Why It Matters:** The Physics service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the Physics provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the Physics provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_INPUT] Registry slot 'Input' is null
- **Category:** Registry
- **Evidence:** slot=Input, isNull=true, registryPhase=1
- **Measured Value:** `null`
- **Why It Matters:** The Input service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the Input provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the Input provider is present and its registration runs during bootstrap.

### [GLOBAL_REGISTRY_SLOT_NULL_ATMOSPHERERUNTIME] Registry slot 'AtmosphereRuntime' is null
- **Category:** Registry
- **Evidence:** slot=AtmosphereRuntime, isNull=true, registryPhase=1
- **Measured Value:** `null`
- **Why It Matters:** The AtmosphereRuntime service is not registered. Systems depending on it will fail or degrade.
- **Confidence:** 90%
- **Next Check:** Check if the AtmosphereRuntime provider component exists in the scene and registers during bootstrap.
- **Likely Fix:** Ensure the AtmosphereRuntime provider is present and its registration runs during bootstrap.

## Warning Findings
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

### [CAMERA_FAR_CLIP_TOO_SMALL] Main camera far clip plane is very small (80)
- **Category:** Camera
- **Evidence:** farClip=80
- **Measured Value:** `80`
- **Why It Matters:** A far clip under 100 will clip terrain, ocean, and distant objects. Open-world needs 1000+.
- **Confidence:** 60%
- **Next Check:** Check camera far clip in Inspector. May be intentional for a special camera.
- **Likely Fix:** Increase the far clip plane to at least 1000 for the main gameplay camera.

## Info Findings
_None._

