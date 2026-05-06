Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# Buoyancy / Current Plan

## Goal

Make floating and sinking objects look better without adding a second physics system and without burning CPU on full-fidelity updates for every rigidbody.

## Constraints

- Reuse existing:
  - `BuoyancyObject`
  - `HectonFluidEngine`
  - `CurrentManager`
- No per-object `Update` loops for buoyancy simulation
- Keep Burst/job path alive
- Prefer main-thread cheap decisions + job-side math over expensive scene queries

## Implemented Direction

### 1. Distance-based LOD in `HectonFluidEngine`

- Near objects:
  - full update cadence
  - full partial-submersion math
  - strongest current fidelity
  - strongest surface-stability torque

- Medium objects:
  - reduced update cadence
  - slightly reduced current/stability weighting

- Far objects:
  - lower update cadence
  - simplified submersion
  - cheaper, weaker motion

- Culled / very far objects:
  - sleeping bodies can be zeroed
  - moving bodies reuse cached force result on skipped ticks

### 2. Phantom current field

- Keep existing global `currentVector`
- Add low-cost noise-based current using `CurrentManager.SampleCurrent(...)`
- Blend phantom current by object response and LOD tier
- Result:
  - less dead water
  - more believable drift
  - still cheap because it stays inside Burst/job path

### 3. Surface restoring torque

- Add stabilizing torque near the surface
- Goal:
  - floating objects stop looking like random tumbling debris
  - nicer upright recovery
  - better visual readability for crates / props / tools

## Object-Level Controls

In `BuoyancyObject`:
- `currentResponse`
- `surfaceStability`
- `lodBias`
- `allowDistanceLod`

These let important props stay higher quality without globally raising cost.

## Implemented Follow-Up

### 4. Authored current volumes

- Added `CurrentVolume.cs`
- Supports:
  - box volume
  - sphere volume
  - directional flow
  - soft edge falloff
- Used by:
  - `HectonFluidEngine`
  - `HectonPlayerMovement`
  - `AmbientWaterMotionManager`

### 5. Cheap ambient motion for decorative props

- Added:
  - `AmbientWaterMotion.cs`
  - `AmbientWaterMotionManager.cs`
- Direction:
  - no rigidbody
  - no per-prop `Update`
  - one manager tick
  - distance LOD cadence reduction
- Intended use:
  - floating junk
  - cables / lightweight dressing
  - small scene props that should feel water-driven without full physics

### 6. Unified player current path

- `HectonPlayerMovement` no longer uses isolated hand-written sin/cos drift
- Ambient current now comes from:
  - phantom field
  - authored current volumes

### 7. Data-driven authoring presets

- Added:
  - `BuoyancyProfile`
  - `AmbientWaterMotionProfile`
- Why:
  - project rules already push data-driven balancing instead of hardcoding random inspector values into every prefab
- Result:
  - buoyant props and decorative water motion can now be standardized by profile instead of manual copy/paste

### 8. World-item buoyancy wiring

- `ItemData` now carries optional `worldBuoyancyProfile`
- `HectonItem` now applies that profile to its `BuoyancyObject` automatically
- Result:
  - dropped inventory items
  - tool pickups
  - pooled world items
  can all inherit standardized float/sink behavior from data instead of prefab-only inspector setup

### 9. Authored current modulation

- `CurrentVolume` now supports:
  - `pulseAmplitude`
  - `pulseFrequency`
  - `phaseOffset`
  - `turbulenceStrength`
  - `turbulenceScale`
  - `turbulenceTimeScale`
- Direction:
  - keep local volumes cheap
  - avoid dead uniform flow
  - add opt-in liveliness without a second simulation layer

## Known Limits

- This is still a cheap stylized physical layer, not CFD or wave-body coupling
- It does not simulate per-triangle hull buoyancy
- MCP was unreliable for exact transform placement of sample current-volume objects
- Authoring positions for sample current volumes should be corrected manually in the inspector if they are kept

## Next Logical Steps

1. Tune live defaults on `[MANAGERS]/HectonFluidEngine`
2. Place current volumes intentionally in gameplay spaces
3. Assign water profiles to real props / pickups / debris prefabs
4. Add sample authored current presets or scene zones for gameplay spaces
