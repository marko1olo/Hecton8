# Hecton8 World Generative Geology Pipeline

This document describes the current implementation of the generative geology layer for large rock arches, canopies, complex cliffs, and future AI-authored geological formations.

The goal of this layer is not just to spawn another prefab family. It is meant to become the bridge between:

- procedural world placement
- cave and SDF logic
- future neural/SDF geology generators
- hybrid world streaming and LOD

Right now the system is implemented as a production-ready fallback pipeline with contextual placement, generated placeholder geometry, seam hints, and LOD support. A real neural backend can later replace only the shape provider without requiring another rewrite of world-fill.

## Current Scope

The system currently covers:

- contextual scoring for geological placement candidates
- per-family geology profiles
- fallback generated geometry for arches, canopies, cave bridges, and complex rock packs
- automatic `LODGroup` creation for generated formations
- runtime metadata on spawned world instances
- seam planning hints for future terrain and voxel blending
- active runtime seam plan assembly for terrain and voxel integration
- runtime smoke testing for generated geology lifecycle and suppression/restore
- emergency geology profiles for important domains even when authored profiles are missing

The system does not yet perform live terrain carving, heightmap deformation, or voxel seam blending. It now does, however, build and maintain explicit seam plans for those future operations.

## Main Files

### Profiles and Contracts

- `Assets/_Project/Scripts/WorldGenerativeGeologyProfile.cs`
- `Assets/_Project/Scripts/WorldPrefabFamilyProfile.cs`

`WorldGenerativeGeologyProfile` is the main authored contract for geological generation. It defines:

- generator preference: disabled, neural-preferred, heuristic SDF fallback
- archetype: arch, canopy, complex rock, reef pack, cave bridge
- composition mode: single, paired, context pack
- terrain seam mode and cave blend mode
- placement fitness ranges for slope, curvature, cave proximity, ridge signal, canyon signal
- LOD intent and future model metadata

`WorldPrefabFamilyProfile` now optionally references a geology profile. It also contains a fallback rule so important domains like rock arches, shelves, cave entrances, and landmarks can automatically opt into generated geology even before assets are fully authored.

### Field Context Sampling

- `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs`

The field sampler was extended beyond simple height/depth/slope. It now computes:

- `curvature`
- `ridgeSignal`
- `canyonSignal`
- `caveProximity`
- `compositionPotential`

These values are synthetic but deterministic and derived from:

- local slope probes
- second-order height response
- noise fields
- biome and zone biases
- hazard / landmark / ruggedness context

This is important because an AI geology system should not receive only a raw world position. It needs a local geological context vector that says whether a place behaves more like:

- a ridge
- a canyon edge
- a cave-adjacent seam
- a composition-friendly landmark pocket

### Scatter Integration

- `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`

Scatter now includes a geology-specific placement bonus:

- `GetGenerativeGeologyContextBonus(...)`

This bonus is applied on top of existing heat, family affinity, pattern affinity, biome matrix, and layer logic.

Every selected placement now carries geology data in `ScatterPlacement`, including:

- geology profile
- curvature
- cave proximity
- ridge signal
- canyon signal
- composition potential

During reconciliation, scatter calls:

- `ApplyGeneratedGeology(...)`

That means generated geology is now part of the normal runtime lifecycle for world-fill:

- desired placement selection
- rebuild / degrade / near-final switching
- metadata application
- streaming persistence

### Runtime Generated Geometry

- `Assets/_Project/Scripts/WorldGenerativeGeologyService.cs`

This service is the current shape provider. It is intentionally structured so a future neural generator can replace only the geometry synthesis part.

Right now it:

- creates generated child geometry under `__GENERATED_GEOLOGY`
- selects a composition plan from the geology profile and local context
- builds fallback forms:
  - arch
  - canopy
  - cave bridge
  - complex rock pack
- adds seam debris when required
- creates `LODGroup` data automatically
- writes a `WorldGenerativeGeologyBinding` component with runtime seam hints

`WorldGenerativeGeologyBinding` is the important runtime record for future integration. It stores:

- profile and archetype
- chosen composition mode
- terrain seam mode
- cave blend mode
- seam radius
- suggested terrain raise / cut
- suggested debris count
- local geological context values

### Runtime Instance Metadata

- `Assets/_Project/Scripts/WorldProceduralProxyInstance.cs`

World instances now persist geology-related metadata such as:

- curvature
- cave proximity
- ridge signal
- canyon signal
- composition potential
- whether generated geology is active
- geology profile id
- geology archetype

This keeps generated geological features compatible with:

- streaming
- debug inspection
- future save-state extensions
- eventual terrain seam reconstruction

### Runtime Seam Planning

- `Assets/_Project/Scripts/WorldGenerativeGeologySeamPlan.cs`
- `Assets/_Project/Scripts/WorldGenerativeGeologyIntegrationDirector.cs`
- `Assets/_Project/Scripts/WorldGenerativeGeologySeamExecutionDirector.cs`
- `Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBlendRequest.cs`
- `Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs`
- `Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBridgeDirector.cs`

This layer turns generated geology hints into actual runtime seam plans.

`WorldGenerativeGeologyIntegrationDirector` now:

- finds active generated geology bindings near the player
- samples terrain height at each generated formation
- computes terrain blend weight, voxel blend weight, and seam debris weight
- builds stable `WorldGenerativeGeologySeamPlan` records
- keeps chunk and macro-zone ownership in the plan
- exposes plan lookup by `runtimeKey`
- prepares voxel blend bounds for future `HectonVoxelEngine` integration

`WorldGenerativeGeologySeamPlan` stores:

- world position and orientation
- terrain contact sample and terrain delta
- terrain seam mode and cave blend mode
- seam radius, terrain raise, terrain cut, debris count
- chunk and macro-zone ownership
- plan strength and per-channel weights
- suggested voxel integration bounds

### Runtime Seam Execution

The seam executor is the first real execution step after planning.

`WorldGenerativeGeologySeamExecutionDirector` now:

- consumes active seam plans from the integration director
- creates runtime seam visuals under `__GEOLOGY_SEAM`
- builds terrain skirts for terrain blend plans
- builds voxel collars for cave / SDF bridge plans
- builds debris bands for seam breakup
- publishes stable voxel blend requests for future `HectonVoxelEngine` consumption

This is intentionally a non-destructive execution layer. It improves visual and architectural continuity now, while keeping actual terrain carving and voxel edits as the next safe step.

### Runtime Terrain Application

`WorldGenerativeGeologyTerrainSeamApplier` is the first true terrain-affecting layer.

It now:

- consumes active terrain seam plans from the integration director
- finds the target Unity terrain tile
- snapshots baseline heights the first time a terrain is touched
- applies localized heightmap patches derived from seam radius and plan weight
- restores old patches when plans disappear
- restores touched terrain when the system is disabled

The implementation is intentionally conservative:

- it only affects local patch rectangles
- it always rebuilds from baseline data instead of accumulating drift
- it does not yet mutate voxel volumes directly

### Runtime Voxel Bridge

`WorldGenerativeGeologyVoxelBridgeDirector` is the first live connection into `HectonVoxelEngine`.

It now:

- consumes `WorldGenerativeGeologyVoxelBlendRequest`
- converts each request into deterministic `CaveStructure` arrays
- runs the voxel engine in `structure-only` mode
- spawns local voxel volumes for geology seam masses
- refreshes and despawns those volumes deterministically by `runtimeKey`
- aligns arches, bridges, and canopies to the generated formation rotation instead of world-axis fallback
- prioritizes requests by plan strength, blend weight, archetype importance, and player distance
- caps expensive rebuilds per tick so the bridge does not thrash on large seam bursts
- rejects stale async results if the request changed or disappeared before voxel generation completed

### Runtime Smoke Testing

- `Assets/_Project/Scripts/WorldGenerativeGeologyRuntimeSmokeTester.cs`

This dev-only smoke tester validates the real lifecycle of generated geology in a live scene.

It currently checks:

- active generated geology can be discovered after scatter/integration warmup
- seam execution creates `__GEOLOGY_SEAM`
- voxel-enabled plans produce a live `WorldGenerativeGeologyVoxelRuntime`
- suppressing a placement through `WorldProceduralStateRegistry` tears down:
  - the generated geology binding
  - the seam plan
  - the voxel runtime
- restoring the placement rebuilds the same runtime key back into the world

This is important because the geology stack is no longer just a visual generator. It now participates in:

- state-driven world streaming
- deterministic rebuilds
- save-ready suppression and restoration
- terrain/voxel seam ownership

## Emergency Fallback Profiles

`WorldProceduralScatterDirector` creates emergency in-memory geology profiles for key domains when authored profiles are missing:

- rock arch
- rock shelf / canopy
- cave entrance / cave bridge
- general landmark rock pack

This ensures the system is usable immediately and does not depend on hand-authoring every profile before runtime testing.

These profiles are not meant to be final art direction. They exist so the runtime architecture is never blocked by data completeness.

## Runtime Flow

The pipeline currently works like this:

1. `WorldProceduralFieldSampler` samples a point and produces geological context.
2. `WorldProceduralScatterDirector` evaluates normal world-fill rules.
3. Families with geology enabled receive an extra contextual placement bonus.
4. The final selected placement stores its geology profile and local terrain context.
5. During reconciliation, scatter spawns or updates the world instance.
6. `WorldGenerativeGeologyService` generates contextual placeholder geometry and LODs.
7. `WorldGenerativeGeologyBinding` records seam hints for future terrain/voxel integration.
8. `WorldGenerativeGeologyIntegrationDirector` converts those hints into stable seam plans near the player.
9. `WorldGenerativeGeologySeamExecutionDirector` turns those plans into runtime seam geometry and voxel blend requests.
10. `WorldGenerativeGeologyTerrainSeamApplier` applies the terrain-side part of those plans to local terrain patches.
11. `WorldGenerativeGeologyVoxelBridgeDirector` turns voxel requests into structure-only voxel volumes.
12. `WorldGenerativeGeologyRuntimeSmokeTester` can validate the full generated-geology lifecycle end-to-end in play mode.

## Relationship To Voxel Caves

This layer is designed specifically to connect with `HectonVoxelEngine` and the SDF cave system.

Relevant existing cave-side strengths:

- deterministic cave graph generation
- multi-primitive SDF support
- entrance funnels
- smooth blending
- cave structure primitives
- marching cubes extraction

The new geology layer prepares the surface-side equivalent:

- contextual placement near ridges, canyon edges, and cave-adjacent terrain
- shape archetypes compatible with SDF thinking
- seam modes that tell future integration whether a feature wants:
  - height blend
  - SDF blend
  - carve-and-debris seam
  - cave portal style integration

## Planned Next Step

The seam integrator now exists, so the next major step is moving from planning to actual deformation/execution.

The next responsibilities are:

- consume `WorldGenerativeGeologySeamPlan`
- consume `WorldGenerativeGeologyVoxelBlendRequest`
- resolve whether a plan should affect:
  - heightmap only
  - voxel cave volume only
  - both
- execute local integration operations:
  - raise terrain more intelligently against real generated geometry silhouettes
  - cut terrain while coordinating with voxel requests
  - request voxel SDF union/subtract
  - spawn seam debris clusters

For `HectonVoxelEngine`, the correct long-term path is not to hardcode “arches” separately. The correct path is:

- let the geology provider expose a local SDF or signed density evaluator
- inject that evaluator as additional structure / union / subtraction data into the voxel density job

That would make arches, canopies, and complex geological bridges behave like first-class SDF citizens instead of only post-spawn visual meshes.

## AI Backend Replacement Strategy

The current fallback service is deliberately shaped so a future neural backend can slot in behind the same request contract.

Target replacement path:

1. keep `WorldGenerativeGeologyRequest`
2. replace primitive composition generation with one of:
   - learned SDF generator
   - neural implicit surface decoder
   - mesh generator producing watertight geology meshes
3. keep `WorldGenerativeGeologyBinding`
4. keep scatter scoring and world placement as-is
5. keep seam planning as-is

This keeps the expensive work localized to shape generation rather than re-architecting world-fill again.

## Honest Current Limitations

The system is intentionally not pretending to be “full AI geology” yet.

Current limitations:

- no trained neural model is integrated
- no offline geometry baking pipeline yet
- no live terrain deformation yet
- no live voxel seam blending yet
- current generated forms are structured placeholders, not production art

What is already real:

- the world context model
- the placement and selection logic
- the runtime orchestration layer
- the generated geometry lifecycle
- the LOD chain generation
- the seam metadata contract
- the runtime seam planning contract
- the runtime smoke pass covering `scatter -> seam -> voxel -> suppress -> restore`
- voxel request prioritization, orientation-aware structure generation, and capped rebuilds per tick
- proactive `VoxelChunk` pool warmup inside the geology voxel bridge to avoid on-demand pool expansion spikes

## Runtime Verification

The geology stack now has a stable play-mode entry point for verification.

Use:

- `Tools/Hecton/Dev/Scene/Run World Generative Geology Smoke (Play Mode)`
- `Tools/Hecton/Dev/Scene/Log World Generative Geology Smoke Status (Play Mode)`

That command will:

1. find or create a `__DEV_GeologySmoke` host in the active scene
2. configure `WorldGenerativeGeologyRuntimeSmokeTester`
3. run a full pass against the live world stack

Expected success log:

- `[GeologySmoke] PASS ...`

Useful status log:

- `[Hecton Dev] Geology smoke status: running=... phase=... runtimeKey=...`

The smoke covers:

- target selection from active generated geology bindings
- seam execution
- voxel runtime creation
- suppression teardown through `WorldProceduralStateRegistry`
- restore back to a consistent runtime

## Authoring Guidance

If you want a family to participate explicitly:

1. Open its `WorldPrefabFamilyProfile`.
2. Assign a `WorldGenerativeGeologyProfile`.
3. Choose an archetype and seam modes.
4. Tune ideal ranges for slope, curvature, and cave proximity.
5. Let scatter handle runtime placement from there.

If no authored profile is assigned but the family is in a geology-heavy domain, the emergency fallback profile will still activate.

## Why This Matters

This architecture avoids the trap of coupling world generation to one content source.

It means Hecton8 can evolve from:

- placeholder procedural geometry

to:

- authored hybrid geology

to:

- neural/SDF-generated geological formations

without redoing:

- scatter
- streaming
- world metadata
- save compatibility
- runtime LOD orchestration

That is the real value of this layer.
