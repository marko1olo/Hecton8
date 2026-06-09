# Physics / Vehicles / Pressure / Water Truth Line-Level Runtime Classification

Status: LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING
Date: 2026-06-02
Evidence class: `STATIC_SOURCE` + `STATIC_DOC`

This file classifies all 281 static suspect lines from:

- `Docs/BibleMandateAudits/1700/05_physics_vehicles_water/RUNTIME_TRIAGE.md`
- `Docs/BibleMandateAudits/1700/05_physics_vehicles_water/RUNTIME_PRECLASSIFICATION.md`
- `Docs/BibleMandateAudits/1700/_scans/05_physics_vehicles_water_runtime_risks.txt`

This is not PhysX proof, fixed-step proof, player-build proof, profiler proof, GC proof, native allocation proof, collision hierarchy proof, async readback proof, or MX350 device proof. Physics/water remains yellow until the required runtime artifacts exist.

## Classification Summary

| Class | Count | Meaning |
|---|---:|---|
| `LEGAL_EDITOR_OR_DEV_GUARDED` | 129 | The line is editor-only, development-only, inside `H8Debug`, a tuner/window/self-test, or an editor-only collider-fitting/telemetry route. |
| `LEGAL_COLD_PATH` | 142 | The line is setup, bootstrap, owner-lifetime native storage, black-box/fault dump, smoke-test, scene registration, readback target initialization, physics culling policy, or proof-gated collider route. |
| `FALSE_POSITIVE` | 3 | Static search matched comments/tooltips/allocator comparison rather than an executable risky path. |
| `RUNTIME_VIOLATION` | 7 registered | All 7 are already registered under `RB-001` or `RB-015`; no new blocker row was required. |

## Existing Blockers Still Binding This Group

- `RB-001`: wreck runtime visual/proxy mesh generation must be moved to offline authoring or proven release-unreachable.
- `RB-015`: world shell prototype `Camera.main`, `Update()`, and `LateUpdate()` routes are not accepted as release camera/world presentation.
- `RB-102`: voxel dynamic nav/grid collider snapshot proof.
- `RB-106`: GPU scatter, microfauna, vegetation, and readback cadence proof.
- `RB-107`: water/gas solver completion and readback cadence proof.
- `RB-115`, `RB-116`, `RB-117`: scatter, vegetation, radar, path, flow, threat, thermal, and chunk native growth/upload proof.
- `RB-120`: H8Memory/DataVault growth proof where physics/world systems allocate through shared native owners.
- `RB-122`: physics culling collider cache, sleep/restore, mesh-collider strip command, and PhysX stability proof.
- `RB-130`: vehicle/tether/buoyancy/storm/thermodynamics runtime proof gates, including the current `RuntimePhysicsBaker1609` prebound offline collider route.

## Line Classification

| Source line(s) | Classification | Reason | Residual proof required |
|---|---|---|---|
| `ProceduralWreckGenerator.cs:5660`, `:5866`, `:5988` | `RUNTIME_VIOLATION` registered | Current source creates `new Mesh()` in runtime-facing wreck visual/proxy merge branches. This is the same violation already registered in generated-assets and world audits. | `RB-001`: authored wreck visual/proxy package proof, editor/offline mesh generation only, or release-unreachable proof plus profiler/player evidence. |
| `HectonWorldShellController1428.cs:25`, `:26`, `:33`; `HectonWorldShellVisualDriver1428.cs:28` | `RUNTIME_VIOLATION` registered | Prototype shell code uses `Camera.main`, private `Update()`, and private `LateUpdate()` outside the owned runtime architecture. This is the same violation already registered in generated-assets/world audits. | `RB-015`: remove/exclude prototype shell from release or rewrite behind owned camera/world presentation routes with profiler proof. |
| `RuntimePhysicsBaker1609.cs:23`, `:24`, `:33`, `:34`, `:35`, `:36`, `:47`, `:56`, `:74`, `:77`, `:102`, `:103` | `LEGAL_COLD_PATH` | Current source no longer exposes `Physics.BakeMesh(...)` or runtime `sharedMesh` reassignment. The component is a legacy authoring/prebound proxy verifier: it refreshes a cached entity key and `CommitBakedCollider(...)` only enables a collider whose `sharedMesh` already equals the serialized offline `collisionProxyMesh`. | `RB-130`: release scene/prefab proof that all target colliders are prebound to serialized `COL_*` proxies, no runtime baking/reassignment callsites are reintroduced, and component use is boot/authoring compatible. |
| `GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:1329`, `:1395`, `:1731`, `:2625`, `:2626`, `:2704`, `:2706`, `:2707`, `:2708`, `:2720`, `:2751` | `LEGAL_COLD_PATH` | Physics culling caches colliders through a bounded scratch list, tracks distance sleep/kinematic/mesh-strip state, and emits command bits. This is a compact-lane optimization shape, not a new violation by text alone. | `RB-122`: registration-only cache proof, collider toggle/strip/restore telemetry, PhysX stability proof, and confirmation this is not hiding illegal LOD0 visual MeshColliders. |
| `GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:2200`, `AbyssalThermodynamicsSolver.ReactorBridge.cs:960`, `ThermodynamicsHazardGridRuntime.cs:1322`, `AbyssalThermalManager.cs:2304`, `ChemicalInfluenceGrid.cs:2132`, `TerrainChunkPagerRuntime.cs:2346`, `VoxelSurfaceNetsVault.cs:1705`, and comparable Temp/TempJob byte payload lines | `LEGAL_COLD_PATH` | These lines are black-box dump, fault/export, terrain/sector payload, or diagnostic snapshot payloads. Static review did not show healthy-frame physics simulation use. | Fault-trigger proof, dump artifact proof, no normal-frame dump spam, and native allocation counters during physics/water/world stress. |
| `VehicleComponentDamageRuntime.cs:946`, `HarpoonTensionSolver328.cs:1409`, `TetherVerletJobs.cs:622`, `:624`, `BuoyancyDisplacementRuntime.cs:2462` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Current source places vehicle CSV staging, tether dump/validator logs, and buoyancy SIMD gizmo label allocation under `#if UNITY_EDITOR` or editor-only validation/gizmo routes. | Runtime damage/tether/buoyancy systems still require stress proof, but these specific lines do not enter non-development player hot paths. |
| `AsyncBuoyancyReadbackRuntime.cs:1555`, `:1557`; `ShinobuOceanSurfaceAtmosphereRuntime.cs:1789`, `:1791`; `GasDynamicsSolver.cs:1746`, `:1748`; `ShinobuStormPropagationRuntime.cs:144` | `LEGAL_COLD_PATH` | Persistent readback/telemetry/storm arrays are owner resource initialization. The architecture is acceptable only if these allocations happen during boot/enable/prewarm and not as first gameplay spikes. | `RB-107` / `RB-130`: compact/high water-gas-storm stress, async readback latency proof, no blocking wait in normal frames, no post-bootstrap native/GPU growth. |
| `AbyssalHeatTunerWindow.cs:*`, `SubmarineThermodynamicsTunerWindow.cs:*`, `ShinobuAtmosphereWaveTunerWindow.cs:*`, `AnalyticalWaveTunerWindow.Editor.cs:*`, `BaseAtmosphereLogisticsEditor.cs:146`, `:156`, `PcieBandwidthGuard1411SelfTest.cs:*` | `LEGAL_EDITOR_OR_DEV_GUARDED` | These are editor windows/self-tests wrapped by `#if UNITY_EDITOR`. UI text writes and TempJob allocations here are not player runtime systems. | Keep tuner/self-test code editor-only; do not use it as release gameplay proof. |
| All `H8Debug` / `Hecton8.Core.H8Debug` callsites in the raw physics scan | `LEGAL_EDITOR_OR_DEV_GUARDED` | The project `H8Debug` facade is compile-stripped from non-development player builds. Many of these lines are also explicit missing-asset/fault diagnostics. | Release build-symbol proof and separate runtime proof for the underlying systems. Missing authored asset diagnostics still indicate proof gaps even when the log call is stripped. |
| `ProceduralWreckGenerator.cs:6880`, `:6883`, `:6897`, `:6931` | `LEGAL_EDITOR_OR_DEV_GUARDED` | These lines are inside `HectonCompoundColliderAutoFitter`, wrapped by `#if UNITY_EDITOR`. The tool removes `MeshCollider` components and fits primitive colliders through editor undo APIs. | Generated wreck prefabs still need collider proxy reports and no-LOD0-MeshCollider proof. |
| `MarauderOutpostGenerationService.cs:188`, `:1973`, `:2085`; `SargassumGlobalDragManager.cs:1276`, `:1539`, `:4051` | `LEGAL_COLD_PATH` / `LEGAL_EDITOR_OR_DEV_GUARDED` | Current source no longer shows the old runtime outpost cube/material synthesis or Sargassum runtime mesh/material synthesis. The outpost line now faults on missing authored shell mesh/material; Sargassum lines are exception/duplicate/editor-validation diagnostics through `H8Debug`. | Authored outpost and Sargassum package proof remains required, but these lines are not active mesh/material generation violations. |
| `BiomeTransitionSmokeTester`, `HectonSandboxAbyssalShelfSmokeTester`, `PlanetaryCanvasSmokeTester`, `VolumetricBiomeSmokeTester`, `WorldGenRegistrySmokeTester`, and similar smoke/self-test native allocations | `LEGAL_COLD_PATH` | These are smoke-test or headless validation harnesses. Some can be attached to scenes, so they are not release proof by default, but their native allocations are not normal physics gameplay by static review. | Build/scene exclusion or dev-only route proof; if any smoke harness ships, prove it cannot run during normal gameplay cadence. |
| `SubmarineAutopilotSdfNavigator.cs:538` | `FALSE_POSITIVE` | Static search matched a comment explaining that a Unity `MeshCollider` path was rejected. | None for this line. Autopilot SDF proof remains separate. |
| `FloraDataTemplate.cs:186` | `FALSE_POSITIVE` | Static search matched a tooltip saying MeshColliders remain forbidden. | None for this line. Flora collider proxy proof remains in generated/world domains. |
| `TOOL_Procedural_Wreckage_Generator.cs:114` | `FALSE_POSITIVE` | Static search matched an allocator comparison inside `ResolveLifetime(...)`, not a new allocation or hot path. | Wreck generator storage proof remains under `RB-001` and generated asset gates. |
| Remaining native owner/storage lines in `GroundPenetratingRadarRuntime`, `VegetationNavGridSynchronizer`, `VegetationFlowFieldIntegrator`, `VegetationChunkResidencyDirector`, `GPUScatterDirector`, `HectonMapMagicVegetationBridge`, `HectonIndirectVegetationRenderer`, `SargassumMicroFaunaBoids`, `VoxelDynamicNavGridRuntime`, `ResourceDistributionDirector`, `ProceduralOreSpawner`, `WorldRegrowthSimulation`, `PersistentWorldRegistry`, and related world/physics-adjacent systems | `LEGAL_COLD_PATH` | These are already covered by world/rendering/runtime architecture manual passes as owner storage, readback targets, dump payloads, or capacity-growth proof gates. Static review did not add new physics-specific violations. | Keep existing blockers open: prewarm, capacity stability, no post-bootstrap growth, no blocking readbacks, no normal-frame dump/export allocation, and compact/high profiler/player proof. |

## Current System Verdict

`YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING`

All 281 listed physics/vehicles/water/pressure static suspect lines are now classified. This does not clear physics or water truth for release. The remaining work is hard evidence: authored collider proxy reports, no LOD0 visual MeshCollider proof, prebound `COL_*` collider proof for `RuntimePhysicsBaker1609`, vehicle/tether/buoyancy/storm/reactor/water stress captures, async readback latency proof, DataVault/H8Memory/native growth counters, physics culling transition telemetry, and black-box dump artifacts.
