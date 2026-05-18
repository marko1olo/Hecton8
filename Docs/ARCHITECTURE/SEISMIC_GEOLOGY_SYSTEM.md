# Seismic Geology System
Date: 2026-05-07

Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Verification: PENDING VERIFICATION

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical data boundary snapshot: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Current actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json`.
- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Visual-realistic-fake doctrine snapshot: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`; re-check `.agents-skills` for newer mandates before implementation.
- May 14 DOC_AUDIT override: the cited May 11 compile artifact is absent from the current filesystem; treat that May 11 compile-success line as stale report text. R43 rechecked the current external root `Hecton8*.csproj` no-restore CLI compile surface at `0 Warning(s)` / `0 Error(s)` after restore assets and referenced `Temp\bin\Debug` DLLs exist; full restore graphs still carry vendor/package warnings, and shared `Temp\obj` locks can create transient evidence noise. Runtime, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, import, scene wiring, and visual quality remain `PENDING VERIFICATION`.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
Historical 2026-05-04 boundary:

- This is the readable geology/seismic reference replacing encoding-damaged geology production notes in active docs.
- It describes intended owner boundaries and data flow, not Play Mode proof.
- Runtime terrain/voxel mutation, MapMagic terrain interaction, and vent/sediment coupling still require source re-open plus Unity verification before surgery.
- For broader current system ownership, read `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md` and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.

## Owners
- `RandomEventSystem`: emits `SeismicShockwaveEvent`.
- `WorldGenerativeGeologyVoxelBridgeDirector`: converts the shockwave payload into a deterministic trench line in runtime AUP.
- `WorldGenerativeGeologyTerrainSeamApplier`: applies terrain deformation against the real MapMagic-owned terrain tiles through `TerrainData.SetHeightsDelayLOD`.
- `HectonVoxelVolume`: applies the matching trench cut through the existing crater/delta path.
- `AbyssalThermalManager`: amplifies registered runtime vents into eruption state when the seismic event fires.
- `SedimentAccumulationManager`: captures exposed surfaces from above and accumulates a global sediment mask for lit shaders.

## Runtime Trench Math
The seismic trench is defined as one AUP line segment:

`L = (AupStart, AupEnd)`

For any terrain sample or voxel column at world position `P`, the cut amount is:

`distanceToLine = distance(P.xz, segment(AupStart.xz, AupEnd.xz))`

`cutDepth = max(0, TrenchDepth - distanceToLine * TrenchSlope)`

This yields a V-profile because cut depth is maximal on the trench centerline and collapses linearly toward zero at the influence radius.

## Terrain Path
`WorldGenerativeGeologyTerrainSeamApplier` owns runtime terrain edits. During reconcile:
- Start from the cached terrain baseline.
- Union the dirty rect from seam plans and active seismic trenches.
- For each heightmap sample in that rect, convert sample XZ to runtime world space.
- Evaluate `cutDepth`.
- Convert meters to normalized terrain height.
- Apply `height -= cutDepthNormalized`.
- Commit through `SetHeightsDelayLOD` and `SyncHeightmap`.

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
