**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# Water Authoring Guide

## Goal

Author buoyancy, sinking, local currents, and decorative water motion without creating a second physics system and without hand-tuning every prefab from scratch.

## Runtime Systems

- `HectonFluidEngine`
  - authoritative buoyancy / drag / current pass
  - Burst + jobs
  - distance LOD
- `CurrentVolume`
  - authored local current zones
- `AmbientWaterMotionManager`
  - cheap decorative motion for non-physics props

## Data Profiles

### Buoyancy profiles

Path:
- `Assets/_Project/Data/Water/BuoyancyProfiles`

Created presets:
- `Profile_Float_LightTrash`
- `Profile_Float_WorkCrate`
- `Profile_Sink_HeavyMetal`

Use on:
- `BuoyancyObject.profile`

Recommended usage:
- `LightTrash`
  - cans, plastic, soft junk, thin panels
- `WorkCrate`
  - crates, cases, sealed industrial props
- `HeavyMetal`
  - anchors, dense scrap, machinery chunks

### Ambient motion profiles

Path:
- `Assets/_Project/Data/Water/AmbientMotionProfiles`

Created presets:
- `Profile_Ambient_CalmSurface`
- `Profile_Ambient_DeepDrift`

Use on:
- `AmbientWaterMotion.profile`

Recommended usage:
- `CalmSurface`
  - buoys, ropes, surface junk, dock clutter
- `DeepDrift`
  - hanging cables, deep props, slow suspended debris

## Component Usage

### 1. Real floating / sinking object

Add:
- `Rigidbody`
- `BuoyancyObject`

Then:
- assign a `BuoyancyProfile`
- keep `autoApplyProfile = true`

Notes:
- `density < 1000` tends to float
- `density > 1000` tends to sink
- `surfaceStability` controls how fast the object recovers upright
- `currentResponse` controls how strongly it follows currents

### 1b. Inventory/world item pickup

If the object uses `HectonItem` + `ItemData`:
- assign `ItemData.worldBuoyancyProfile`
- `HectonItem` now pushes that profile into `BuoyancyObject` automatically

Use this when:
- dropped inventory items
- tool pickups
- pooled generic world-item prefabs

Result:
- one prefab can stay generic
- the float/sink behavior comes from data, not prefab duplication

### 2. Cheap decorative prop

Add:
- `AmbientWaterMotion`

Then:
- assign an `AmbientWaterMotionProfile`
- keep `autoApplyProfile = true`

Use this for:
- visual clutter
- floating dressing
- props that should move but do not need physics

Do not use this on:
- active rigidbodies already driven by buoyancy

### 3. Local current zone

Add:
- `CurrentVolume`

Tune:
- `shape`
- `localDirection`
- `strength`
- `verticalFactor`
- `edgeSoftness`
- `pulseAmplitude`
- `pulseFrequency`
- `phaseOffset`
- `turbulenceStrength`
- `turbulenceScale`
- `turbulenceTimeScale`
- `boxSize` or `sphereRadius`

Use this for:
- cave throat pull
- corridor crossflow
- surface drift bands
- vent sidewash
- living but cheap rhythmic surge in authored spaces

## Optimization Rules

- Prefer `AmbientWaterMotion` for decorative clutter instead of adding `Rigidbody + BuoyancyObject` to everything.
- Use `BuoyancyObject` only on interactable or physically meaningful props.
- Keep authored `CurrentVolume` count low and intentional.
- Use `lodBias` only on important props; do not globally raise it.
- Do not disable engine LOD just to “make it look better” from far away.

## Debug

On `HectonFluidEngine`:
- `drawLodGizmos`
- `drawCurrentVectors`
- `_debugNearCount`
- `_debugMediumCount`
- `_debugFarCount`
- `_debugCulledCount`
- `_debugCurrentVolumeCount`

On `AmbientWaterMotionManager`:
- `_debugActiveObjects`
- `_debugNearCount`
- `_debugMediumCount`
- `_debugFarCount`
- `_debugCulledCount`

## Known Limitation

Scene-object placement through MCP was unreliable for sample current-volume objects.

If a sample volume needs to be used for real gameplay:
1. select it manually in Unity
2. move it in the inspector
3. save the scene or prefab normally
