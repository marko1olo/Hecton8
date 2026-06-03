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

- `ProceduralWreckGenerator` still has registered runtime visual/proxy mesh generation lines under `RB-001`; release proof must show those branches are unreachable, removed, or replaced by serialized authored visual/proxy assets. Collider fitting remains editor/offline only in the reviewed fitter path.
- Vegetation path native scratch allocations may hit runtime under path pressure.
- Radar pending-job native allocations may hit runtime under scanner spam.
- No PhysX collider proof confirms visual meshes are not used as runtime colliders in generated/wreck/rock/coral families.
- `GlobalPhysicsStateManager.Shinobu37PhysicsCulling` appears to use culling/sleep/cache routes, but collider cache and mesh-collider strip/restore need gameplay proof.
- `MarauderOutpostGenerationService` no longer shows the older runtime cube mesh/material synthesis in current-source scan evidence; it now faults on missing authored shell mesh/material. Physics/interactable proxies still need separate release proof.
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

## Pass 7 Addendum - Wreck Proxy And Native Physics Adjacent Paths

- `HectonCompoundColliderAutoFitter` is editor-only; its primitive Box/Capsule collider creation is consistent with the collision-proxy bible when used as offline prefab fitting.
- `ProceduralWreckGenerator.BuildProxyMesh()` can create a runtime proxy mesh if `wreckCollisionProxyMesh` is absent. Even if used for navigation rather than PhysX, release closure still requires serialized proxy assignment or editor bake proof.
- Vegetation path and radar scan staging are physics-adjacent because they affect navigation/scanner truth under player pressure; they need allocation and job completion proof before physics/water truth can be accepted.

## Pass 8 Addendum - Physics Culling Collider Transitions

- `GlobalPhysicsStateManager.Shinobu37PhysicsCulling` caches child colliders through a preallocated scratch list, stores them in fixed arrays, and toggles `collider.enabled` during distance sleep/restore. This is a plausible compact-lane cinematic cheat, but it must prove that cache collection is registration/transition-only and that collider toggles do not destabilize PhysX contacts.
- The physics culling job emits command bits for sleep, kinematic, and mesh-collider strip behavior. Release closure requires telemetry for transition counts, strip/restore counts, frame cost, and proof that this route is not compensating for illegal LOD0 MeshCollider authoring.

## Pass 11 Addendum - Ocean Readback And Gas Solver Detail

- `ShinobuOceanSurfaceAtmosphereRuntime` uses triple-buffered async wave-height readback with bounded sample capacity and persistent native readback arrays. It consumes only completed requests and defers disposal while requests are pending. This is structurally good, but water truth still needs GPU/readback latency proof on compact and high lanes.
- `GasDynamicsSolver` uses fixed/post-fixed owner phases, quality-scaled cadence, persistent telemetry scratch, and black-box dump on repeated write-lock failure. It remains `GREENISH_GAS_OWNER_PHASE_WITH_COMPLETION_STRESS_PROOF_REQUIRED`; no release acceptance without gas/flood/base stress profiler proof.

## Pass 16 Addendum - Vehicle, Tether, Buoyancy, Storm, And Thermal Runtime Gates

- `VehicleComponentDamageRuntime.TryLoadCsvLayout()` is inside `#if UNITY_EDITOR`; the `Allocator.Temp` CSV staging grid is not a player-runtime violation. The runtime damage path is still proof-gated because `FixedTick()` schedules damage jobs, `PostFixedTick()` finalizes them, and release needs combat stress showing no signal-lane lazy init, no DataVault/H8Memory growth, and no force-complete outside lifecycle windows.
- `HarpoonTensionSolver328.WriteTelemetryDump(...)` and `TetherMemorySovereigntyValidator1303` are editor guarded. The runtime tether solver still needs active-tether stress proof for force-event output, spline vertices, telemetry rings, fault flags, and downstream physics event budget.
- `AsyncBuoyancyReadbackRuntime` uses a triple async readback shape and consumes completed requests without waiting. It remains `GREENISH_TRIPLE_ASYNC_READBACK_WITH_BOOT_ALLOC_AND_GPU_PROOF_REQUIRED` because `EnsureGpuBuffers()` and `EnsureReadbackData()` create owner resources on enable/first dispatch, and because compute-unavailable mock readback cannot become silent production water truth.
- `BuoyancyDisplacementRuntime` SIMD label allocation is editor gizmo code under `#if UNITY_EDITOR`, not runtime UI allocation.
- Current `RuntimePhysicsBaker1609` source no longer exposes `Physics.BakeMesh(...)` or runtime `sharedMesh` reassignment. It is a legacy authoring/prebound proxy verifier: `CommitBakedCollider(...)` only enables a target collider whose `sharedMesh` already equals the serialized offline `collisionProxyMesh`. Release proof must show all uses are prebound to serialized `COL_*` proxies and no runtime cooking/reassignment route is reintroduced.
- `ShinobuStormPropagationRuntime` uses persistent job staging and `GlobalQualityWeight` cadence, but auto-creates `H8_ShinobuStormPropagationRuntime` at `AfterSceneLoad` if no authored instance exists. That is recovery behavior, not release scene composition proof.
- `AbyssalThermodynamicsSolver` and `ThermodynamicsHazardGridRuntime` are owner-phased, but their reactor/heat visual upload and fault dump routes need compact/high stress and black-box artifact proof before physics/water/pressure truth can be accepted.

## Pass 16 Required Proof

- Vehicle damage, tether, buoyancy, storm, reactor, and heat hazard 300-frame compact/high stress with GC Alloc, NativeMemorySentinel, H8Memory, DataVault, job completion, readback latency, and black-box counters visible.
- Collider report proving no LOD0 visual mesh is assigned to `MeshCollider`, proving all `RuntimePhysicsBaker1609` targets are prebound serialized `COL_*` proxies, and proving no normal-frame collider cooking or runtime collider mesh reassignment occurs during healthy gameplay.
- Authored release-scene component proof for storm/buoyancy/thermal owners so runtime fallback root creation does not become normal composition.

## Line-Level Classification Addendum

- All 281 physics/vehicles/water/pressure static suspect lines are now classified in `LINE_LEVEL_CLASSIFICATION.md`: 129 editor/dev guarded, 142 cold/setup/fault/owner-lifetime, 3 false positives, and 7 registered runtime violations.
- The 7 violations are not new physics blockers: they are cross-routed current-source violations under `RB-001` (`ProceduralWreckGenerator.cs:5660`, `:5866`, `:5988`) and `RB-015` (`HectonWorldShellController1428.cs:25`, `:26`, `:33`; `HectonWorldShellVisualDriver1428.cs:28`).
- Editor/dev guarded lines include vehicle CSV staging, tether memory validator logs, harpoon telemetry dump payload, buoyancy SIMD gizmo labels, and tuner/window UI text.
- Proof-gated cold lines include physics culling collider cache/strip transitions, async buoyancy readback ownership, storm/thermal owner storage, gas/ocean runtime storage, and the current prebound `RuntimePhysicsBaker1609` collider-proxy verifier.
