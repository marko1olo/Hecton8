# HECTON-8 Underwater Visual Audit And Execution Plan

Status: `PENDING VERIFICATION`  
Date: `2026-04-14`

## Implementation Snapshot

In-progress code slice already started:

- `HectonUnderwaterVisuals` now contains a short submerge impulse layer that darkens and thickens fog only near the waterline crossing.
- `HectonUnderwaterVisuals` now re-drives Crest `_Caustics` and `_CausticsStrength` from shallow-depth / light-factor gating instead of leaving caustics globally disabled all the time.
- `LandingImpactVFX` now exposes dedicated `TriggerSubmergeImpulse()` and `TriggerSurfaceBreakImpulse()` hooks so the existing camera PP owner can handle water-crossing lens shock without introducing a second transient-PP system.
- `HectonUnderwaterVisuals` now owns a camera-local suspended particulate layer (`Underwater_SuspendedMotes`) and drives its emission from depth / turbidity / submerge impulse instead of leaving the camera volume visually empty underwater.
- `Assets/_Project/Prefabs/Player.prefab` is now the source of truth for the underwater particulate child under `Player/Main Camera`, instead of relying on scene-only dressing.
- `Assets/_Project/Prefabs/Player.prefab` now also contains an inactive `Underwater_ShallowSunBeam` child with `VLB.VolumetricLightBeamHD` + driver light, and `HectonUnderwaterVisuals` now gates it by shallow depth / light factor instead of leaving god rays entirely absent near camera.
- Separate asset-authoring ledger now exists here: [2026-04-15_UNDERWATER_ASSET_REQUIREMENTS.md](/abs/path/c:/hades/Hecton8/Docs/2026-04-15_UNDERWATER_ASSET_REQUIREMENTS.md:1)
- `wet visor / runoff` is now being implemented on the existing `Suit_Visor` shader + `VisorHUDController` path instead of adding a second fullscreen overlay stack.
- `HectonUnderwaterVisuals` now also boosts the existing near-camera suspended motes when the player moves close to the seafloor, using sampled seafloor distance + player body speed instead of spawning a second bottom-silt particle owner.
- `PlayerFlashlight` now auto-resolves a VLB owner on `DiveLamp_Light`, and `Player.prefab` now gives that light its own `VLB.VolumetricLightBeamHD` + `TrackRealtimeChangesOnLightHD` so the flashlight shaft can thicken underwater from depth instead of staying visually flat.
- `PFB_Support_Pocket_Hazard.prefab` is now the next canonical world-space vent target: localized bubble columns are being attached to its `LOD0/LOD1` children instead of inventing a separate global ambient-bubbles manager.
- `HectonUnderwaterVisuals` is now also the canonical owner for `player exhale bubbles`: it subscribes to `HectonPlayerMovement.OnExhale` and emits a burst on a dedicated `Player/Main Camera/Underwater_ExhaleBubbles` child instead of inventing a second breathing-VFX manager.
- module-local `LeakVfx` children are now being authored on `PFB_Module_Corridor` and `PFB_Module_Foundation` under `LOD0`, while `BaseModule` gained a cold-path fallback that resolves `LeakVfx` by name when the serialized `leakVfx` field is missing.
- composite ruin seep plumes are now being authored on `PFB_Ruin_ClusterMedium` and `PFB_Ruin_Megastructure` under `LOD0`, while `ConstructionBootstrapAuthoring` now stamps the same `RuinLeakPlume_*` children during future rebuilds instead of leaving composite ruins visually dry underwater.
- `ConstructionBootstrapAuthoring` now also syncs module leak and composite ruin seep `ParticleSystemRenderer` owners back into `LODGroup` renderer lists, and the current prefab assets were patched to include those nested renderer refs, so the new plume layers do not float outside intended LOD cadence.
- module-local `LeakWetSheen` quads are now being authored on `PFB_Module_Corridor` and `PFB_Module_Foundation` under `LOD0` with existing `GlassWet.mat`, and both authoring source plus current prefab assets now keep those transparent sheen renderers inside `LOD0` renderer lists instead of leaving wetness as scene-only dressing.
- composite ruin seep zones now also receive small `RuinSeepSheen_*` `Quad` layers with existing `GlassWet.mat` under `LOD0`, and both authoring source plus current prefab assets now keep those transparent seep-surface renderers inside `LOD0` renderer lists instead of leaving ruins as plume-only points.
- composite ruins now also receive authored `micro-life silhouettes` directly inside their `VisualPrimitiveSpec[]` LOD definitions, reusing existing support-creature materials on a handful of small `Capsule` / `Cylinder` forms under `LOD0/LOD1` instead of introducing a new runtime swarm manager or leaving large ruin silhouettes visually dead.
- `PFB_Support_Pocket_Hazard` now also receives small `VentSheen_*` refractive quads with existing `GlassWet.mat` under the same post-build vent owner path as `VentBubbleColumn_*`, and both source plus prefab asset keep those transparent vent-surface renderers inside `LOD0/LOD1` renderer lists instead of leaving hazard vents as particles-only points.
- support pockets now also receive tiny authored fauna silhouettes directly inside their `BuildHazardPocketLods` / `BuildSafePocketLods` definitions: `PFB_Support_Pocket_Hazard` gets small predator-perch forms, while `PFB_Support_Pocket_Safe` gets small passive visitors, all kept inside `LOD0/LOD1` renderer lists instead of inventing a runtime swarm layer.
- `PFB_Support_Pocket_Resource` now also receives small passive `forager` silhouettes inside `LOD0/LOD1`, using the same authored pocket pattern as `safe/hazard`, so the full support-pocket family no longer diverges in visual density.
- `PFB_Support_Zone_ReefApex` now also receives localized passive `drift visitors` inside `LOD0/LOD1`, reusing the same support-creature material path for near/mid-field life read instead of leaving large reef-support silhouettes as static canopy-only masses.
- `PFB_Support_Zone_LargeThreat` now also receives localized predator `sentry` silhouettes inside `LOD0/LOD1`, reusing the existing predator material path so apex threat zones read as inhabited danger volumes instead of abstract monolith-only shapes.
- `PFB_Support_Zone_AbyssApex` now also receives localized predator `watcher` silhouettes inside `LOD0/LOD1`, so deep apex monoliths stop reading as empty geometric set-pieces and instead carry a nearby threat presence.
- `PFB_Support_Zone_RuinApex` now also receives localized predator `perch / sentinel` silhouettes inside `LOD0/LOD1`, so ruin apex spaces inherit the same inhabited-danger read as the other apex zone families.
- all currently authored underwater fauna-hint primitives in support pockets, support zones, and ruin apex silhouettes are now configured as `no-shadow` renderers in both source-of-truth editor code and current prefab assets, instead of wasting shadow-map cost on tiny ambient forms.
- `PFB_Support_CreatureSpawn_Passive` and `PFB_Support_CreatureSpawn_Predator` now also receive localized `fry / scout` hints inside `LOD0/LOD1`, so the support-creature spawn family no longer diverges from the rest of the authored underwater habitat language.
- `PFB_Debris_ScrapCluster` and `PFB_Debris_WreckField` are now being upgraded from single-LOD dead debris masses into authored `LOD0/LOD1` underwater dressing with localized scavenger silhouettes, so the debris family stops diverging from ruins/support forms in both visual density and LOD discipline.
- `ConstructionBootstrapAuthoring` is now also the canonical owner for selective industrial underwater decals on construction ruins/modules: leak-adjacent stripe/scuff decals are being attached only on authored `LOD0` surfaces of `PFB_Module_Corridor`, `PFB_Module_Foundation`, `PFB_Ruin_ClusterMedium`, and `PFB_Ruin_Megastructure`, reusing existing `ScifiFacility` decal prefabs instead of introducing scene-global projectors or terrain-wide decal spam.
- `NASAPunk/SuitVisor` now also contains a procedural imperfection fallback for micro-scratches and smudge breakup when `Mat_Visor_Glass` lacks authored `_ScratchNormalMap` / `_FingerprintTex`, so the near-camera visor no longer depends on missing texture slots to avoid reading clinically clean during underwater runoff.
- `NASAPunk/SuitVisor` runoff response is now also being pushed further on the same shader path: active runoff boosts scratch/smudge visibility and adds a local wet haze / sheen term, so submerge and surface-break moments read less like a clean transparent overlay and more like water physically sitting on the visor.
- `AcousticZoneController` now also validates mixer authoring coverage in editor/cold path: it warns when `MasterMixer` still has only a token snapshot set or no acoustic processing beyond `Attenuation`, so the underwater audio layer stops silently accepting non-functional mixer content as if it were production-ready.

Runtime verification is still blocked by the compile/runtime issues listed below.

## Scope

Audit the current underwater presentation in `02_HECTON_WORLD`, identify what already exists, what is disabled, what is missing, what can be reused immediately, and what should be implemented next without blowing the MX350 budget.

This document is facts first. No “looks good”. No “probably works”.

---

## 1. Current Truth

### 1.1 Runtime owners already present

- Underwater visual owner exists: [Assets/_Project/Scripts/HectonUnderwaterVisuals.cs](/abs/path/c:/hades/Hecton8/Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:69)
- Surface weather owner exists and already writes back into underwater visuals: [Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs](/abs/path/c:/hades/Hecton8/Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:248)
- Underwater audio transition owner exists: [Assets/_Project/Scripts/AcousticZoneController.cs](/abs/path/c:/hades/Hecton8/Assets/_Project/Scripts/AcousticZoneController.cs:131)
- Camera impact post-process controller already exists and can be repurposed for submerge impulse: [Assets/_Project/Scripts/LandingImpactVFX.cs](/abs/path/c:/hades/Hecton8/Assets/_Project/Scripts/LandingImpactVFX.cs:26)
- Flashlight already has optional Volumetric Light Beam integration hook: [Assets/_Project/Scripts/PlayerFlashlight.cs](/abs/path/c:/hades/Hecton8/Assets/_Project/Scripts/PlayerFlashlight.cs:172)

Conclusion: there is no need to invent a second underwater owner. The existing owner stack is incomplete, not absent.

### 1.2 Scene-level state in `02_HECTON_WORLD`

- `Ocean_Crest` is present in scene and uses `Assets/Crest/Crest/Materials/Ocean-Underwater.mat`.
- Ocean runtime is configured with:
  - sea floor depth sim = on
  - shadow sim = on
  - foam sim = off
  - clip surface sim = off
  - LOD resolution `256`
  - geometry downsample `2`
  - lod count `6`
  - source: [Assets/_Project/Prefabs/Ocean_Crest.prefab](/abs/path/c:/hades/Hecton8/Assets/_Project/Prefabs/Ocean_Crest.prefab:50)
- Global world post stack exists in `SampleSceneProfile`:
  - Tonemapping
  - Bloom
  - Vignette
  - ColorAdjustments
  - WhiteBalance
  - ShadowsMidtonesHighlights
  - MotionBlur disabled
  - source: [Assets/_Project/Data/SampleSceneProfile.asset](/abs/path/c:/hades/Hecton8/Assets/_Project/Data/SampleSceneProfile.asset:13)
- Camera-local post stack exists on player camera:
  - Chromatic Aberration
  - Vignette
  - source: [Assets/_Project/Scenes/02_HECTON_WORLD/Main Camera Profile.asset](/abs/path/c:/hades/Hecton8/Assets/_Project/Scenes/02_HECTON_WORLD/Main Camera Profile.asset:13)

### 1.3 What is missing in live world right now

Direct scene inspection found:

- `DecalProjector` count in loaded scenes: `0`
- `ParticleSystem` count in loaded scenes for underwater dressing: `0`
- `VolumetricLightBeamHD` count in loaded scenes: `0`

This means the current live underwater world has no authored runtime layer for:

- seabed light decals
- suspended particulate matter
- bubble fields / vents / exhale visuals
- local god rays / shafts
- wet camera drip layer
- underwater micro-life dressing

The packages exist. The runtime layer does not.

### 1.4 Caustics are already in project but disabled

`Ocean-Underwater.mat` already contains the Crest caustics texture and parameters, but the feature flag is off:

- `_CausticsTexture` assigned
- `_Caustics: 0`
- `_CausticsStrength: 3.2`
- `_CausticsTextureScale: 5`
- `_CausticsDistortionScale: 25`
- source: [Assets/Crest/Crest/Materials/Ocean-Underwater.mat](/abs/path/c:/hades/Hecton8/Assets/Crest/Crest/Materials/Ocean-Underwater.mat:38)

Conclusion: the seabed light pattern is not missing from the project. It is present in Crest and explicitly disabled.

### 1.5 Decals exist, but not as a live underwater system

Project inventory:

- `Assets/Dynamic Decals` package exists.
- `Assets/ScifiFacility/Prefabs/decals/*.prefab` exists.
- Example sci-fi “decal” prefab is a mesh + mesh renderer, not URP `DecalProjector`:
  - [Assets/ScifiFacility/Prefabs/decals/decal_01.prefab](/abs/path/c:/hades/Hecton8/Assets/ScifiFacility/Prefabs/decals/decal_01.prefab:1)

Conclusion:

- For world grime, panel labels, hazard stripes, wet leaks on structures: mesh decals are already available.
- For underwater light pattern on terrain floor: do not default to mesh decals. Use Crest caustics first.
- For local hero splashes or temporary wetness: projector/decal system can be used selectively, not globally.

### 1.6 Bubble content exists, but not in the form needed

Found:

- bubble audio set: `Assets/_Project/Audio/SFX/bubble sound (1-4).wav`
- proxy prefab `PFB_family_pocket_safe__bubble`, but this is static sphere geometry, not VFX:
  - [Assets/_Project/Prefabs/WorldProceduralProxy/PFB_family_pocket_safe__bubble.prefab](/abs/path/c:/hades/Hecton8/Assets/_Project/Prefabs/WorldProceduralProxy/PFB_family_pocket_safe__bubble.prefab:1)

Conclusion:

- “bubble” content exists only as sound and static proxy shape.
- There is no reusable underwater bubble emitter prefab in first-party runtime content.

### 1.7 Volumetric light beam support exists, but player does not use it

Code path exists:

- `PlayerFlashlight` has serialized `volumetricBeam` field and runtime update path.
- source: [Assets/_Project/Scripts/PlayerFlashlight.cs](/abs/path/c:/hades/Hecton8/Assets/_Project/Scripts/PlayerFlashlight.cs:172)

Live player state:

- `volumetricBeam = null`

Conclusion:

- VLB can be used immediately on player flashlight or selected shallow-water sun proxy lights.
- It is not currently wired.

---

## 2. Verification Blockers

### 2.1 Runtime verification is currently blocked by existing compile errors

Console during play attempt reported:

- `Assets/_Project/Scripts/UI/UITooltip.cs(230,32): error CS0234`
- `Assets/_Project/Scripts/UI/UIAudioFeedback.cs(272,51): error CS0120`
- `Assets/_Project/Scripts/UI/SettingsLivePreview.cs(173,25): error CS0311`
- `Assets/_Project/Scripts/UI/SettingsLivePreview.cs(175,22): error CS1061`

As long as this is red, any “verified underwater fix” claim is invalid.

### 2.2 Editor-side underwater snapshot is currently misleading

Static scene inspection showed `HectonUnderwaterVisuals` reading editor camera state instead of a valid player runtime state:

- `mainCamera = SceneCamera`
- `_debugEditorDriven = true`
- `CurrentDepth = 0`
- `IsUnderwater = false`

This explains why editor captures do not represent actual underwater presentation.

### 2.3 World bootstrap is not stable enough for trustworthy visual capture

Play attempt fell back into `01_MAIN_MENU`, and world-side managers lost critical runtime references:

- `playerTransform = null` in multiple world systems
- `mainCamera = null` / `playerCamera = null` inside `HectonUnderwaterVisuals`
- `MapMagicBridge.IsAvailable = false`

This is not a visual polish issue. This is a runtime verification blocker.

---

## 3. Visual Assessment Of Current Underwater Layer

### 3.1 What currently exists

- Depth fog and underwater color grading via `HectonUnderwaterVisuals`
- Crest underwater fullscreen pass ownership
- Basic underwater ambient color
- Audio snapshot transition hook
- Camera-local vignette/chromatic profile
- Existing shallow/deep biome palette infrastructure

### 3.2 What currently does not read as premium underwater image

- No clear authored “submerge moment”
- No water-on-visor / runoff / droplets pass
- No shallow-water god ray layer
- No reliable seabed light pattern in runtime because Crest caustics are disabled
- No suspended particulate field
- No bubble emitters or bubble vent silhouettes
- No local structure wetness decals in active world
- No underwater micro-dressing that gives motion between player and background

### 3.3 What this means for the image

Current underwater presentation is mostly fog + ocean shader + base post. That is enough for baseline readability. It is not enough for a premium NASA-punk / deep sea noir frame.

The missing piece is not “more fog”. The missing piece is layered light transport and medium detail:

- transition response
- suspended medium
- shallow light breakup
- floor interaction
- local moving detail near camera

---

## 4. Resource Inventory

### 4.1 Safe immediate reuse

- Crest underwater pass
- Crest caustics texture and shader path
- Existing player camera volume
- `LandingImpactVFX` logic pattern for temporary lens response
- `AcousticZoneController` underwater snapshot path
- `PlayerFlashlight` VLB hook
- Dynamic Decals package
- SciFiFacility mesh decals
- bubble audio clips

### 4.2 Existing assets that are not the right answer for seabed light

- SciFiFacility decal meshes
- static bubble proxy prefab

These are usable for structure dressing or set dressing, not for global underwater light transport.

### 4.3 Likely assets that need to be added

- one first-party pooled bubble vent emitter prefab
- one first-party pooled suspended silt / plankton particle prefab
- one first-party visor-drip / runoff overlay texture or mesh card solution
- one shallow-water light shaft prefab or controlled VLB rig
- optionally one localized “wet leak” decal set for structures

---

## 5. Recommended Execution Order

## Phase 0: Restore Trustworthy Runtime Verification

Do first.

- Clear the existing compile errors in UI scripts.
- Verify `02_HECTON_WORLD` can enter play mode directly.
- Verify `HectonUnderwaterVisuals` binds to player camera, not `SceneCamera`.
- Verify player depth and underwater state are real at runtime.
- Capture screenshots at:
  - waterline
  - 2-5 m below surface
  - shallow seabed
  - mid-depth travel lane

Without this, all later visual tuning is blind.

## Phase 1: Submerge Transition Layer

Implement first because it is cheap and immediately visible.

Target behavior:

- brief darkening on crossing the surface
- mild lens distortion / vignette surge
- water run-off / visor drip pass
- underwater audio snapshot blend

Recommended owner model:

- keep transition authority in `HectonUnderwaterVisuals`
- reuse `LandingImpactVFX` style cached volume handling, but trigger on waterline crossing instead of landing
- drive audio through existing `AcousticZoneController`

Do not:

- spawn ad-hoc particles on every crossing
- use coroutine spam
- bolt a separate “UnderwaterTransitionManager” into scene

## Phase 2: Shallow-Water Light Transport

This is the real image upgrade.

Implementation target:

- re-enable Crest caustics only where they matter
- tie strength to shallow depth / sun visibility / biome clarity
- ensure seabed and large shallow props receive readable moving pattern

Constraints:

- do not turn on expensive full-scene volumetric fog blindly
- do not use a global decal projector carpet over terrain
- do not enable caustics at deep depth where they make no physical sense

Preferred path:

1. enable Crest caustics on `Ocean-Underwater.mat`
2. gate strength by depth and shallow-water visibility in `HectonUnderwaterVisuals`
3. validate MX350 cost before adding any extra local shaft rigs

## Phase 3: Local Shafts / God Rays

Not global. Localized.

Recommended approach:

- use a few controlled shafts in shallow landmarks, reef tops, broken structure zones
- re-use `VolumetricLightBeamHD` only where camera actually notices it
- bind the player flashlight beam first because the hook already exists and it is readable everywhere

Do not:

- fill the open ocean with dozens of volumetric beams
- enable volumetric fog package globally without budget proof

## Phase 4: Medium Detail Layer

Add the missing “water has matter in it” read.

Need:

- suspended silt / plankton motes
- occasional bubble vents / seep emitters
- localized leak bubbles near ruins / vents / thermal cracks
- player exhale bubbles near camera / visor edge
- optional drifting cards or sparse silhouette movers for mid-distance life

Implementation rule:

- pooled emitters only
- low particle counts
- depth/biome gating
- disable aggressively outside near field

## Phase 5: Decal Strategy

Split the problem correctly.

Use mesh decals from `ScifiFacility` for:

- structure labels
- warning stripes
- worn panel surfaces
- interior/exterior industrial storytelling

Use dynamic/projected decals selectively for:

- fresh wet leaks on modules
- impact residue
- local hero spots

Do not use decals for:

- broad seabed caustic pattern
- large continuous underwater floor lighting

That belongs to Crest caustics or a controlled light/material solution.

---

## 6. First Safe Implementation Slice

If implementation starts immediately after blockers are cleared, the first safe slice should be:

1. Fix runtime verification path.
2. Add submerge transition impulse.
3. Re-enable shallow-only Crest caustics.
4. Wire `VolumetricLightBeam` on player flashlight.
5. Add localized bubble vent columns on `PFB_Support_Pocket_Hazard` with prefab-local particles, not a scene-global manager.

Why this order:

- all four are immediately visible
- all four reuse existing owners or packages
- none require a new architecture branch
- none require heavy world authoring first

Do not start with:

- full underwater volumetric fog
- giant bubble ecosystem
- mass decal pass
- new global underwater weather system

That is architecture drift before the base layer is even trustworthy.

---

## 7. Regression Model

### CPU

- risk: local shaft beams and particle emitters add per-frame renderer cost
- guard: keep counts low and depth-gated

### GC

- risk: naive transition effects or particle spawning cause allocations
- guard: cached `Volume` refs, `ITickable` state, pooled emitters, no coroutines in gameplay path

### Memory

- risk: full-screen RT and volumetric stacks are already sensitive in this project
- guard: reuse existing profiles/materials first, avoid new always-on RT layers

### Correctness

- risk: wrong camera ownership makes underwater state false
- guard: verify player camera binding before any visual tuning

### Cadence

- risk: adding world dressing before runtime state is stable creates false positives
- guard: unblock play mode first

---

## 8. Immediate Findings Summary

- Underwater stack exists, but it is not layered enough.
- Crest caustics already exist and are disabled.
- Decal assets already exist, but there is no active underwater decal strategy in scene.
- Volumetric Light Beam support already exists in code, but player flashlight is not wired.
- Bubble content exists only as audio and static proxy mesh, not as runtime VFX.
- Current runtime verification is blocked by existing compile errors and broken play-mode binding.

Status remains `PENDING VERIFICATION`.
