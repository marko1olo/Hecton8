# Physics / Vehicles / Water Manual Review

Status: STATIC REVIEW - NO PHYSICS/PROFILER PROOF
Date: 2026-06-02

## Reviewed Files

- `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` from pass 1
- `Assets/_Project/Scripts/World/VegetationNavGridSynchronizer.cs`
- `Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs`
- `Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs`
- `Assets/_Project/Scripts/World/Outposts/MarauderOutpostGenerationService.cs`
- `Assets/_Project/Scripts/Physics/GasDynamicsSolver.cs`
- ocean/atmosphere async readback paths from pass 4 static search

## What Exists

- Collision-proxy law exists in bibles and editor collider fitting appears editor-only in the reviewed wreck generator block.
- Vegetation/radar jobs use dispatcher-style job fences instead of naked direct `.Complete()` in the reviewed snippets.
- Radar and vegetation systems store state in native buffers with explicit owner ids.

## What Is Missing / Not Proven

- `ProceduralWreckGenerator` needs proof that LOD0 visual mesh generation and collider baking do not happen in player runtime.
- Vegetation path native scratch allocations may hit runtime under path pressure.
- Radar pending-job native allocations may hit runtime under scanner spam.
- No PhysX collider proof confirms visual meshes are not used as runtime colliders in generated/wreck/rock/coral families.
- `GlobalPhysicsStateManager.Shinobu37PhysicsCulling` appears to use culling/sleep/cache routes, but collider cache and mesh-collider strip/restore need gameplay proof.
- Outpost visual shell fallback is a runtime cube mesh if no authored mesh is assigned; physics/interactable proxies need separate proof.
- `GasDynamicsSolver` appears owner-phased (`FixedTick`, `PostFixedTick`, late-frame completion), but its completion windows still need proof that they do not create same-frame stalls under gas/water stress.
- Ocean/atmosphere readback paths need cadence/stall proof before water/atmosphere presentation can be called compact-safe.

## Current Classification

- Wreck collider fitting: `LEGAL_EDITOR_OR_DEV_GUARDED`.
- Wreck mesh merge: `YELLOW_RUNTIME_MESH_GENERATION_PROOF_REQUIRED`.
- Vegetation/radar native scratch: `YELLOW_NATIVE_RUNTIME_ALLOCATION_REVIEW_REQUIRED`.
- Physics culling: `YELLOW_COLLIDER_CACHE_AND_STRIP_PROOF_REQUIRED`.
- Outpost shell fallback: `YELLOW_RELEASE_PREFAB_ASSIGNMENT_REQUIRED`.
- `GasDynamicsSolver.cs`: `YELLOW_JOB_COMPLETION_WINDOW_PROOF_REQUIRED`.
- Ocean/atmosphere readback paths: `YELLOW_ASYNC_READBACK_CADENCE_PROOF_REQUIRED`.

## Required Next Proof

- Collider hierarchy audit for generated prefabs.
- 300-frame stress with radar/path activity and native allocation telemetry.
- Profiler proof before any claim that these systems satisfy physics/runtime bible mandates.
- Layer/collider proof for generated prefabs: `LOD0` visual meshes must not be assigned to runtime physics colliders.
- 300-frame gas/water stress with job completion timing, native allocation counters, and no hidden consumer-side `.Complete()`.

## Pass 6 Addendum - Survival Overlay And Cold Parse Boundary

- `HectonSurvivalSystem.cs:3652` and `:3731` allocate native staging during survival database parse. Static context reads as cold data parse, not steady-state physiology, but release proof must show this cannot occur during gameplay hot ticks.
- `DcsAscentProfileOverlay.cs` exposes `OnGUI` outside `/Editor/` path. It needs dev/editor exclusion proof or a production UI route.
