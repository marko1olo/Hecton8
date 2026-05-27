# Seismic Geology System

Date: 2026-05-07

Status: PENDING VERIFICATION

Verification: PENDING VERIFICATION

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.

- Historical actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json` (historical snapshot only; do not use for current counts or proof).

- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.

- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.

- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.

Historical 2026-05-04 boundary:

- This is the readable geology/seismic reference replacing encoding-damaged geology production notes in active docs.

- It describes intended owner boundaries and data flow, not Play Mode proof.

- Runtime terrain/voxel mutation, MapMagic terrain interaction, and vent/sediment coupling still require source re-open plus Unity verification before surgery.

- For broader current ownership, read `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, `PROJECT_RUNTIME_TOPOLOGY.md`, and `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md`.
- Older dated report names are historical only and are not active contract anchors.

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not terrain deformation runtime, voxel mutation, MapMagic scene wiring, profiler, or Play Mode proof.

- `Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs`

- `Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBridgeDirector.cs`

- `Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs`

- `Assets/_Project/Scripts/HectonVoxelVolume.cs`

- `Assets/_Project/Scripts/World/AbyssalThermalManager.cs`

- `Assets/_Project/Scripts/World/SedimentAccumulationManager.cs`

## Owners

- `RandomEventSystem`: emits `SeismicShockwaveEvent`.

- `WorldGenerativeGeologyVoxelBridgeDirector`: converts the shockwave payload into a deterministic trench line in runtime AUP.

- `WorldGenerativeGeologyTerrainSeamApplier`: owns runtime terrain writeback through `TerrainData.SetHeightsDelayLOD`. MapMagic is an input/bridge adapter only and does not own runtime deformation authority.
- `HectonVoxelVolume`: applies the matching trench cut through the existing crater/delta path.

- `AbyssalThermalManager`: amplifies registered runtime vents into eruption state when the seismic event fires.

- `SedimentAccumulationManager`: captures exposed surfaces from above and accumulates a global sediment mask for lit shaders.

## Runtime Trench Math

The seismic trench is defined as one AUP line segment:

`L = (AupStart, AupEnd)`

For any terrain sample or voxel column, compute AUP point `Paup` and subtract the trench sector/origin in double precision before any float downcast. The cut amount is:

`localP = double2(Paup.x - AupOrigin.x, Paup.z - AupOrigin.z)`

`localA = double2(AupStart.x - AupOrigin.x, AupStart.z - AupOrigin.z)`

`localB = double2(AupEnd.x - AupOrigin.x, AupEnd.z - AupOrigin.z)`

`distanceToLine = distance(localP, segment(localA, localB))`

`cutDepth = max(0, TrenchDepth - distanceToLine * TrenchSlope)`

This yields a V-profile because cut depth is maximal on the trench centerline and collapses linearly toward zero at the influence radius.

## Terrain Path

`WorldGenerativeGeologyTerrainSeamApplier` owns runtime terrain edits. During reconcile:

- Start from the cached terrain baseline.

- Union the dirty rect from seam plans and active seismic trenches.

- For each heightmap sample in that rect, convert sample XZ to terrain-local/AUP-local coordinates.
- Evaluate `cutDepth`.
- Convert meters to normalized terrain height.
- Apply `height -= cutDepthNormalized`.
- Commit through `SetHeightsDelayLOD` and `SyncHeightmap`.

Unity world space is a final writeback target only. It is not an authority coordinate for trench math.

This keeps terrain trenching inside the existing seam-authority owner instead of patching MapMagic core.

## Voxel Path

`WorldGenerativeGeologyVoxelBridgeDirector` broadcasts the same trench line to active `HectonVoxelVolume` instances.

Each volume:

- Converts the AUP trench line into the current runtime frame.

- Samples along the line and laterally across the trench half-width.

- Resolves the first solid ceiling/floor anchor in the SDF column.

- Applies `CarveCrater(...)` subtractive stamps whose radius is derived from `cutDepth`.

The real mutation path remains:

`TryApplySeismicTrench -> CarveCrater -> VoxelDeltaProcessor -> mesh rebuild`

No parallel voxel mutation path is introduced.

## Vent Eruption Coupling

`AbyssalThermalManager` listens to `RandomEventEvents.OnSeismicShockwave`.

During the eruption window:

- Runtime vents multiply heat, updraft, smoke density, and plume height.

- Hazard radius and heat intensity scale with the same eruption blend.

- The same GPU vent upload path publishes the stronger plume state to the existing smoke/VFX buffers.

## Sediment Overlay

The sediment system is a separate runtime owner because no existing geology owner publishes a reusable top-down exposure mask.

Pipeline:

1. A hidden orthographic camera captures visible surface height and up-facing normal data into `__HectonSedimentCapture`.

2. `SedimentAccumulation.compute` accumulates a mask over time:

   - exposed, upward-facing surfaces gain sediment

   - occluded or changed surfaces lose sediment

   - large height shifts decay old sediment to avoid trench ghosts

3. `Hecton_CoreLit.hlsl` samples the global sediment mask by world XZ and blends procedural sand/silt tint plus dune normal into wreck, dry-zone, and voxel-rock shading.

The accumulation kernel stores:

- `R`: sediment amount

- `G`: last observed normalized height

- `B`: current exposure

Height stability term:

`heightMatch = 1 - saturate(abs(currentHeight - previousHeight) * invTolerance)`

Accumulation term:

`sedimentNext = saturate(sedimentPrev + exposed * deposition * heightMatch - erosion - geometryShiftPenalty)`

This keeps the mask stable during slow buildup and sheds it when trenches or cave-ins reconfigure the surface.
