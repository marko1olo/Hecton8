# ECS/DOTS Adoption Plan - FaunaSimulationEngine and FluidMathCore

Date: 2026-05-07
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-20 DOC_GLOBAL R46 Root/Architecture Boundary Note

R51 root/architecture encoding/boundary/read-order/route-card/source-counter correction (`Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md`) keeps this file as a static architecture/source contract, not runtime proof. Current DOC_GLOBAL boundary is `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md`; R50 remains prior at `Docs/Reports/2026-05-21_DOCUMENTATION_R50_ROOT_ARCHITECTURE_ATLAS_REGEN_R48_INTERIOR_DUMPTARGET_AND_COUNTER_DRIFT_LOCAL.md`; R49 remains prior at `Docs/Reports/2026-05-21_DOCUMENTATION_R49_ROOT_ARCHITECTURE_ATLASCHECK_BOUNDARY_ROUTE_FIELDS_AND_COUNTER_DRIFT_LOCAL.md`; R48 remains prior at `Docs/Reports/2026-05-21_DOCUMENTATION_R48_ROOT_ARCHITECTURE_DATE_ROLLOVER_ATLASCHECK_AND_COUNTER_REFRESH_LOCAL.md`; R47 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md`; R46/R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current static gates: `Tools/AtlasCheck.py` remains red on `ATLAS_CHECK_FAIL references=6881 missing=60` (one Dynamic Decals vendor asset ref, RealtimeCSG vendor icon/readme image refs, and missing HectonMaskChannelPacker and HectonMaterialChannelPackValidator source refs in the current atlas); `Docs/Modding/Validate_Mod_API_Static.ps1` passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only. Runtime proof remains absent.

Scope: roadmap only. No `com.unity.entities` production migration was performed in this pass.

2026-05-01 trust note:

- Read `Docs/README.md`, `Docs/Reports/README.md`, and the dated reports below before using this plan as current ECS/DOTS execution guidance.
- DOTS/Entities remains prototype/experimental architecture, not the production backbone.
- The removal criteria below are future gates, not current proof.
- Do not enable live ECS ownership for save, UI, player, bootstrap, active fauna, pooled physics, or scatter placement without profiler parity and runtime validation.

## Mandates Followed

- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`

## Current Baseline

The project already uses DOD under the MonoBehaviour/service surface:

- `FaunaSimulationEngine` schedules Burst jobs over `NativeArray<PoolSlotData>`, `NativeArray<float3>`, and `NativeArray<byte>`.
- `FaunaSimulationMemory` owns the resident native memory pack and is the natural conversion boundary for ECS chunk data.
- `FluidMathCore` is a Burst-safe stateless math service. It is not an ECS system yet; it should remain a static math kernel when moved.
- `com.unity.entities` is not confirmed as a production package dependency in the scanned project context. Any migration must start behind an assembly define and not contaminate runtime builds.

## Migration Rule

Do not migrate presentation, authored prefab wiring, or registry ownership first.

First ECS candidate must be data-only, job-owned simulation with no direct `Transform`, `GameObject`, `Collider`, `UnityEngine.Object`, managed interface, LINQ, coroutine, or string dependency.

## Phase 0 - Package Isolation

Create a disabled-by-default ECS assembly lane:

- Assembly: `Hecton8.Dots`
- Define constraint: `HECTON8_ENTITIES_EXPERIMENTAL`
- Package dependency: `com.unity.entities`, exact version pinned in `Packages/manifest.json`
- Runtime bridge: explicit adapter from existing native buffers to ECS worlds
- Rule: no existing gameplay assembly may require Entities until perf and memory proof exists

Exit criteria:

- Editor compiles with define disabled.
- Editor compiles with define enabled.
- No first-party runtime assembly gains an unconditional Entities dependency.

## Phase 1 - Fauna Data Components

Primary target: `FaunaSimulationEngine`.

Candidate `IComponentData` structs:

- `FaunaAupPosition`: `int3 Cell`, `float3 LocalOffset`
- `FaunaLinearVelocity`: `float3 Value`
- `FaunaSimulationFlags`: `byte Value`
- `FaunaHealthState`: `float Health`, `float Hunger01`, `byte StateFlags`
- `FaunaHibernationState`: `float SleepStartTimeSeconds`, `float LastCatchUpSeconds`
- `FaunaLodState`: `byte Residency`, `float DistanceSq`
- `FaunaSpeciesId`: `uint StableId`
- `FaunaPoolSlotIndex`: `int Value` for bridge compatibility during transition

Current native source mapping:

- `PoolSlotData` -> `FaunaAupPosition`, `FaunaPoolSlotIndex`, and species/runtime identity components.
- `NativeArray<float3> LinearVelocities` -> `FaunaLinearVelocity`.
- `NativeArray<byte> SimulationFlags` -> `FaunaSimulationFlags`.
- `FaunaHibernationCatchUpInput` and `FaunaHibernationCatchUpResult` -> `FaunaHealthState` and `FaunaHibernationState`.

First ECS system candidates:

- `FaunaDataOnlyLodSystem`: replaces `DataOnlyFaunaLodJob`.
- `FaunaHibernationCatchUpSystem`: replaces `HibernationCatchUpJob`.
- `FaunaResidencyExportSystem`: writes back to existing render/proxy buffers during hybrid transition.

Hard blockers:

- Existing AUP math must remain deterministic and must not collapse to `Transform.position`.
- Render proxies and pooled GameObjects must remain outside ECS until data parity is proven.
- Save/load needs stable entity identity. Entity indices are not stable save IDs.

## Phase 2 - Fluid Data Components

Primary target: `FluidMathCore` as a math kernel, with state owned by submarine fluid runtime systems.

Candidate `IComponentData` structs:

- `FluidCompartmentState`: `float CurrentVolume`, `float MaxVolume`, `float3 LocalCenter`
- `FluidCompartmentMass`: `float MassKg`, `float3 CenterOfMass`
- `FluidBreachState`: `float AreaSquareMeters`, `float DepthMeters`, `byte Active`
- `FluidBulkheadState`: `Entity Source`, `Entity Destination`, `float DoorAreaSquareMeters`, `byte Open`
- `FluidTransferSettings`: `float FlowCoefficient`, `float MaxTransferPerTick`, `float DischargeCoefficient`
- `FluidIngressSettings`: `float DischargeCoefficient`, `float MaxIngressPerSecondNormalized`
- `FluidConstants`: `float WaterDensityKgPerCubicMeter`, `float GravityMetersPerSecondSquared`, `float Epsilon`

Current source mapping:

- `FluidMathCore.ResolveIngressVelocity` and `ResolveIngressVolume` -> stateless Burst functions called from `IJobEntity` or `ISystem`.
- `FluidMathCore.ResolveBulkheadTransferDelta` -> bulkhead transfer system over linked compartment entities.
- `FluidMathCore.ResolveCenterOfMassStep` -> mass aggregation and presentation export system.

First ECS system candidates:

- `FluidIngressSystem`: applies breach ingress per fixed simulation step.
- `FluidBulkheadTransferSystem`: processes transfer pairs.
- `FluidCenterOfMassSystem`: aggregates compartment mass and exports submarine center of mass.

Hard blockers:

- Entity references for bulkheads must be baked from authored submarine topology. No runtime `Find*` path.
- Floating origin/AUP compatibility must be explicit if any fluid world-space position is introduced.
- Physics handoff must remain queue-based. Direct `Rigidbody` mutation from ECS simulation is rejected.

## Phase 3 - Hybrid Bridge

Bridge rules:

- Existing `GlobalRegistry` remains the runtime authority until a full bootstrap replacement exists.
- ECS world startup must be owned by the Sovereign Bootstrap sequence, not by scene object self-instantiation.
- NativeArray owners may mirror ECS data during migration, but there must be one write owner per frame.
- Export to GameObject proxies happens after simulation and before presentation.

Required bridge systems:

- `FaunaEcsImportSystem`: one-time or dirty-only import from existing fauna pools.
- `FaunaProxyExportSystem`: writes presentation buffer data for current renderer/proxy path.
- `FluidRuntimeImportSystem`: imports authored compartment graph.
- `FluidPhysicsExportSystem`: emits force/mass packets to the existing physics queue.

## Phase 4 - Removal Criteria

Remove the old NativeArray service path only after all criteria are met and freshly verified:

- CPU frame time is equal or lower on MX350 target hardware.
- GC remains `0 B/frame` in hot paths.
- Save/load stable IDs survive scene reload and version migration.
- Native memory lifetime is owned by ECS worlds and disposed on world teardown.
- Fresh console queries return `0` errors and `0` warnings in both Entities-enabled and Entities-disabled configurations.

## Rejected Moves

- Replacing `GlobalRegistry` with ECS service lookup in one pass.
- Migrating visual GameObject proxies before data-only simulation.
- Storing managed references in ECS components.
- Converting `FluidMathCore` into a stateful ECS singleton. It should remain stateless math.
- Using Entities as a reason to weaken AUP, save identity, or zero-GC rules.

## Regression Model

- CPU: chunk iteration may improve cache behavior, but hybrid import/export can erase gains.
- GC: Entities is not automatically zero-GC if managed components, structural changes, or conversion-time allocations leak into gameplay cadence.
- Memory: duplicate NativeArray plus ECS chunk storage during transition increases resident memory until the old path is removed.
- Cadence: fixed simulation, render export, save snapshots, and floating-origin shifts must have explicit ordering.
- Correctness: entity identity, AUP conversion, hibernation catch-up, and bulkhead graph topology are the primary failure modes.

## Verdict

ECS/DOTS adoption is viable only as an isolated, experimental data-only lane. `FaunaSimulationEngine` is the first valid target. `FluidMathCore` should not become stateful; it should supply Burst-safe math to ECS systems after submarine fluid state has been split into explicit component data.

STATUS: PENDING VERIFICATION
