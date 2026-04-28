# THIRD-PARTY POISON — CREST ANTI-CORRUPTION STATUS

**Audit Date:** 2026-04-28  
**Scope:** `HectonPlayerMovement.cs`, `HectonSurfaceWeatherDirector.cs`, related ocean abstraction ownership  
**Question:** Has Crest been abstracted away from gameplay/runtime owners?

---

## Executive Verdict

**Status:** Partial only.  
`HectonPlayerMovement` has been migrated onto the anti-corruption layer. `HectonSurfaceWeatherDirector` has not.

The statement **"Crest has been fully abstracted away from gameplay scripts"** is false.

---

## Confirmed Anti-Corruption Layer

The abstraction exists:

- `[IHectonOceanKinematics](</c:/hades/Hecton8/Assets/_Project/Scripts/IHectonOceanKinematics.cs:1>)`
- `[Crest4KinematicsAdapter](</c:/hades/Hecton8/Assets/_Project/Scripts/Crest4KinematicsAdapter.cs:1>)`

What that means:

- Crest sampling ownership can be centralized behind an interface.
- Gameplay callers can consume water height / flow / wave normals without importing Crest directly.

---

## HectonPlayerMovement Status

**Result:** Migrated to the interface. Direct Crest coupling removed from this controller.

Evidence:

- No `using Crest;` in `[HectonPlayerMovement.cs](</c:/hades/Hecton8/Assets/_Project/Scripts/HectonPlayerMovement.cs:1>)`
- Provider field is abstracted as `MonoBehaviour oceanKinematicsProvider` at [234](</c:/hades/Hecton8/Assets/_Project/Scripts/HectonPlayerMovement.cs:234>)
- Runtime owner is `IHectonOceanKinematics _oceanKinematics` at [924](</c:/hades/Hecton8/Assets/_Project/Scripts/HectonPlayerMovement.cs:924>)
- Sampling path resolves through `ResolveOceanKinematics()` at [4331](</c:/hades/Hecton8/Assets/_Project/Scripts/HectonPlayerMovement.cs:4331>)
- Height / flow / normal sampling uses the interface in `InitOceanKinematics()`, `UpdateOceanWaterHeight()`, `UpdateOceanFlowSampling()` at [4053](</c:/hades/Hecton8/Assets/_Project/Scripts/HectonPlayerMovement.cs:4053>), [4083](</c:/hades/Hecton8/Assets/_Project/Scripts/HectonPlayerMovement.cs:4083>), [4168](</c:/hades/Hecton8/Assets/_Project/Scripts/HectonPlayerMovement.cs:4168>)

Residual contamination:

- Field names and comments still say `Crest`, but that is naming debt, not runtime coupling.

Verdict:

- `HectonPlayerMovement`: **ACL implemented successfully**

---

## HectonSurfaceWeatherDirector Status

**Result:** Not migrated. Still directly coupled to Crest runtime API.

Evidence:

- Direct namespace import: `[using Crest;](</c:/hades/Hecton8/Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:14>)`
- Direct concrete field: `OceanRenderer _oceanRenderer` at [293](</c:/hades/Hecton8/Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:293>)
- Direct singleton resolution: `OceanRenderer.Instance` at [476](</c:/hades/Hecton8/Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:476>), [578](</c:/hades/Hecton8/Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:578>), [1460](</c:/hades/Hecton8/Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:1460>), [1515](</c:/hades/Hecton8/Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:1515>)
- Direct Crest state mutation in `ApplyOceanState(...)`:
  - `_oceanRenderer._globalWindSpeed = targetWindSpeed` at [1469](</c:/hades/Hecton8/Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:1469>)
  - `oceanMaterial.SetFloat(...)` foam properties at [1485](</c:/hades/Hecton8/Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:1485>), [1494](</c:/hades/Hecton8/Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:1494>), [1503](</c:/hades/Hecton8/Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:1503>)
- Direct Crest reset in `RestoreOceanDefaults(...)` at [1509](</c:/hades/Hecton8/Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:1509>)

Verdict:

- `HectonSurfaceWeatherDirector`: **ACL not implemented**

---

## Broader Runtime Crest Exposure

Even beyond the two target files, Crest is still present in first-party runtime code:

- `[HectonUnderwaterVisuals.cs](</c:/hades/Hecton8/Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:63>)`
- `[HectonCelestialEngine.cs](</c:/hades/Hecton8/Assets/_Project/Scripts/HectonCelestialEngine.cs:68>)`
- `[HectonUrpTextureRequirementsGuard.cs](</c:/hades/Hecton8/Assets/_Project/Scripts/Core/HectonUrpTextureRequirementsGuard.cs:2>)`
- `[HectonCrestOceanDepthCacheBootstrap.cs](</c:/hades/Hecton8/Assets/_Project/Scripts/World/HectonCrestOceanDepthCacheBootstrap.cs:2>)`
- `[HectonCrestOceanDepthCacheRuntimeBridge.cs](</c:/hades/Hecton8/Assets/_Project/Scripts/World/HectonCrestOceanDepthCacheRuntimeBridge.cs:3>)`

This means the anti-corruption layer is not project-wide yet.

---

## Player Prefab Decomposition Monitor

`Player.prefab` decomposition is in progress, but still incomplete.

Evidence found:

- Runtime service exists: `[PlayerRuntimeContextService.cs](</c:/hades/Hecton8/Assets/_Project/Scripts/Core/PlayerRuntimeContextService.cs:18>)`
- Shared contract exists: `[IPlayerRuntimeContext](</c:/hades/Hecton8/Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:465>)`
- Many systems already consume `GlobalRegistry.Player`

What is missing:

- No dedicated `PlayerRuntimeContext` data struct was found
- No extracted immutable player data record was found
- No new state carrier class was found that materially siphons logic out of the main player MonoBehaviour

Current service behavior is still service-locator hydration, not decomposition:

- `Tick()` calls `SyncPlayerContext()` at [290](</c:/hades/Hecton8/Assets/_Project/Scripts/Core/PlayerRuntimeContextService.cs:290>)
- `SyncPlayerContext()` still performs component and hierarchy resolution, including `_playerTransform.Find("Suit_Visor")` at [423](</c:/hades/Hecton8/Assets/_Project/Scripts/Core/PlayerRuntimeContextService.cs:423>)

Verdict:

- Progress exists
- Real decomposition into data carriers/structs is **not** visible yet

---

## Audio Runtime Check

`AudioSource.PlayOneShot` audit result:

- No first-party runtime hits were found in `Assets/_Project/Scripts`
- Repository hits exist only under `Assets/Plugins/Editor/DarkTonic/MasterAudio/...`

Verdict:

- First-party runtime audio remains clear of `PlayOneShot`
- Editor-only plugin previewers still use it, which is outside runtime scope

---

## Mandates Followed

- `OPT_Zero_GC_Policy_AllocFree_Mandate`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety`
- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC`
- `AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation`
