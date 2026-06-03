# Gameplay / Tools / Construction / Inventory / Combat / Economy Line-Level Runtime Classification

Status: LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING
Date: 2026-06-02
Evidence class: `STATIC_SOURCE` + `STATIC_DOC`

This file classifies all 193 static suspect lines from:

- `Docs/BibleMandateAudits/1700/07_gameplay_construction_tools_inventory_combat/RUNTIME_TRIAGE.md`
- `Docs/BibleMandateAudits/1700/07_gameplay_construction_tools_inventory_combat/RUNTIME_PRECLASSIFICATION.md`
- `Docs/BibleMandateAudits/1700/_scans/07_gameplay_construction_tools_inventory_combat_runtime_risks.txt`

This is not gameplay profiler proof, Play Mode proof, Unity import proof, player-build proof, GC proof, Memory Profiler proof, physics proof, inventory/economy route proof, combat proof, construction graph proof, or device proof. The system remains yellow until runtime artifacts prove the static classifications.

## Classification Summary

| Class | Count | Meaning |
|---|---:|---|
| `LEGAL_EDITOR_OR_DEV_GUARDED` | 124 | The line is editor-only, editor/test-only, an editor verification window/scanner, or a compile-stripped `H8Debug` diagnostic route. |
| `LEGAL_COLD_PATH` | 55 | The line is boot/setup cache repair, owner-lifetime native storage, black-box/fault dump payload, registry invalidation, or cold fail-safe installer work. |
| `FALSE_POSITIVE` | 14 | Static search matched comments, allocator constants, or warning text that is not the risky operation. |
| `RUNTIME_VIOLATION` | 0 new | No new direct runtime violation was found in this group during this line pass. Existing release blockers still bind the group. |

## Existing Blockers Still Binding This Group

- `RB-008`: dynamic runtime roots/add-components, now also binding `EconomyRuntimeInstaller` as a recovery-only composition route until authored scene/prefab proof exists.
- `RB-011`: foundation mock SDF truth route.
- `RB-012`: drone mock truth and procedural material route.
- `RB-121`: scavenging oracle runtime host and orphan scan.
- `RB-126`: player kinematics and autonomous extraction owner-phase stress.
- `RB-128`: construction preview, ambient biota, beacon, and diagnostic GPU/material fallback lifecycle.
- `RB-130`: vehicle, tether, buoyancy, storm, and thermodynamics runtime proof gates, now also binding `SubmarineCoreDirector` legacy PhysX auto-level component auto-install proof.

## Line Classification

| Source line(s) | Classification | Reason | Residual proof required |
|---|---|---|---|
| `DebrisManager.cs:1712`, `:1731` | `LEGAL_COLD_PATH` | `RebuildCache()` caches authored child mesh filters/colliders into reusable scratch lists during `Awake()`/`OnValidate()` style setup. It is not a normal gameplay tick query in the reviewed context. | Debris prefab authoring proof, no repeated cache rebuild during gameplay cadence, and zero-GC debris stress. |
| `HarvestableOutcrop.cs:146`, `:148` | `LEGAL_COLD_PATH` | Renderer/collider child caches are built during outcrop setup. The line is cold component cache repair, not interaction hot-path scanning. | Harvestable prefab cache proof and 300-frame harvest/interaction profiler with no repeated hierarchy scan. |
| `LifePodTactilePrologueController.cs:449` | `LEGAL_COLD_PATH` | Seat strap latch references are cached once for prologue setup. | Prologue boot proof and no repeated latch hierarchy scan during player control. |
| `MountablePlayerTransport.cs:1587` | `LEGAL_COLD_PATH` | Component lookup is a cold transport/submarine binding validation path. | Vehicle mount/unmount proof with no repeated rebind scan. |
| `PlayerKinematicsRuntime.cs:1067`, `:1068`, `:1575`, `:1733` | `LEGAL_COLD_PATH` | Movement/motor references are resolved during setup/rebind fallback, not from the fixed/post-fixed solver hot path. | `RB-126`: rebind-count telemetry, 300-frame movement stress, origin-shift/teardown completion proof, 0 B/frame after bootstrap. |
| `SubmarineCoreDirector.cs:397` | `LEGAL_COLD_PATH` | Raw scan points at the ballast controller lookup/legacy install branch in `CacheReferences()`. This is cold composition repair, but the legacy auto-add path is not release scene composition proof. | `RB-130`: authored ballast controller proof, legacy flag build policy, and no normal release scene composition through `AddComponent<SubmarineAutoLevelBallastController>()`. |
| `SubmarineCompoundColliderAuthoring.cs:316` | `LEGAL_COLD_PATH` | `RebuildRuntimeColliderCache()` scans the generated collider root during `Awake()`/`OnEnable()` to cache baked compound colliders. The component is runtime-aware, despite "Authoring" in the type name. | Authored `__CompoundColliders` proof, no enable/disable churn cache rebuilds during gameplay, collider LOD transition telemetry, and no LOD0 visual MeshCollider usage. |
| `EconomyRuntimeInstaller.cs:23`, `:26`, `:29`, `:32` | `LEGAL_COLD_PATH` | Cold installer checks/repairs economy owner components on `__HECTON_ECONOMY_RUNTIME`. This is legal only as boot recovery, not as normal release scene composition. | `RB-008`: authored economy runtime root/prefab proof, boot manifest proof, and counters showing installer repair did not execute as normal player-scene composition. |
| `InteractableRegistry.cs:228`, `:243` | `LEGAL_COLD_PATH` | `RegisterTree`/`InvalidateTree` use reusable collider scratch during interaction registration/unregistration, not every player query. | Interaction registration churn proof, fixed collider target cache proof, and no hot-path tree scan. |
| `RandomEventSystem.cs:1743` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Meteor splash prefab scan is inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD` validation. | None for release runtime; production meteor VFX proof remains separate. |
| `ArmorPenetrationEditorFacade.cs:757` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Prefab collider scan belongs to the editor facade/scanner route. | None for player runtime; keep facade editor/tool-only. |
| `ScavengingLootOracle.cs:1782`, `:1788` | `LEGAL_EDITOR_OR_DEV_GUARDED` | HideAndDontSave orphan cleanup uses `Resources.FindObjectsOfTypeAll` and component checks under editor/development reload cleanup. | `RB-121`: prove runtime host is authored/bootstrap-owned and orphan scan is not release gameplay cadence. |
| `ResourceNodeTemplate.cs:275` | `FALSE_POSITIVE` | The line is tooltip text saying `MeshCollider` is forbidden. Static search matched the word, not a runtime MeshCollider assignment. | None from this line. Resource node collider proxy proof remains separate. |
| `BaseAirlockEvents.cs:103`, `FirstHourDirector.cs:67`, `EndingSystem.cs:102`, `HectonSubmarineOS.cs:240`, `PlayerSignalEvents.cs:111`, `EclipseGameplaySystem.cs:75`, `PlayerExpressionManager.cs:56`, `RandomEventSystem.cs:221`, `VehicleCommandSignals.cs:55`, `SuitMeshUpdateEvents.cs:43`, `InteractionEvents.cs:69` | `FALSE_POSITIVE` | These are constant allocator selector definitions for DataVault-exempt signal lanes, not allocation callsites. | Signal-lane capacity proof belongs to the actual bus/vault owner storage, not these constants. |
| `PlayerInteraction.cs:38`, `:216` | `FALSE_POSITIVE` | These are comments describing intended debug behavior, not executable logging lines. | None from these comments. |
| `ContextualPhysicalIkRig.cs:3210`, `ContextualPhysicalIkRuntime.cs:1886`, `:2793` | `LEGAL_COLD_PATH` | Generic native-array helpers allocate owner storage or transient helper storage in IK setup/fault contexts. | IK owner lifetime/disposal proof, no same-frame schedule/readback loop, and no first-use allocation during interaction hot paths. |
| `SomaticKinematicsRuntime.cs:891`, `:896`, `:901`, `:906`, `:911`, `:916`, `:921`, `:926`, `:2488` | `LEGAL_COLD_PATH` | Fixed native player state, sphere, stroke history, tuning, drag LUT, signal scratch, black-box ring, cursor, and dump payload storage belong to the somatic kinematics owner. | `RB-126`: post-fixed completion, origin-shift/teardown force-complete boundaries, black-box dump artifact, native memory sentinel stability, and 0 B/frame after bootstrap. |
| `RadiationHazardGrid.cs:2367`, `SubmarineAutoLevelBallastController.cs:3074`, `:3136`, `VRSomaticProvider.Comfort.cs:1189`, `VRSomaticProvider.cs:3080`, `LaserCutterDodRuntime.cs:1077`, `WfcLaserCutRuntime.cs:623`, `ToolKinematicsRuntime.cs:957`, `Shinobu19EconomyLedger.cs:1585`, `:1624`, `InventoryRoutingNetwork.cs:1081`, `SubmarineOsThermalGridRuntime.cs:1654`, `ShinobuLogisticsRouter.cs:1747` | `LEGAL_COLD_PATH` | These `NativeArray<byte>(Allocator.Temp/TempJob)` hits are black-box dump, fault/export, or explicit snapshot payload routes, not healthy-frame gameplay work. | Fault-trigger proof, no normal-frame dump spam, dump artifact paths, and compact/high stress profiler proof for each owning system. |
| `AutonomousExtractorSystem.cs:140`, `:142`, `:144`, `:146`, `:148`, `:150` | `LEGAL_COLD_PATH` | Fixed-capacity slow-tick SOA arrays are allocated once by the extractor owner. | `RB-126`: 256-module extractor stress, registration/unregistration churn, dropped-item/power routing proof, and no post-bootstrap growth. |
| `ScavengingLootOracle.cs:1056`, `:1058`, `:1061`, `:1063`, `:1066`, `:1068` | `LEGAL_COLD_PATH` | Persistent request/result/telemetry arrays are owned by the scavenging oracle scratch, not allocated per harvest. | `RB-121`: authored/bootstrap host proof and 300-frame scavenging interaction proof. |
| `VRSomaticProvider.Comfort.cs:1626`, `:1630`, `:1634` | `LEGAL_EDITOR_OR_DEV_GUARDED` | These native buffers are inside `#if UNITY_EDITOR || UNITY_INCLUDE_TESTS` comfort fuzzer/test code. | None for release runtime. |
| `ScavengingLootOracle.cs:2494` | `LEGAL_EDITOR_OR_DEV_GUARDED` | CSV ingest scratch allocation belongs to the editor oracle/tuner window route. | None for player runtime; production loot tables need authored data proof. |
| `StatusEffectsEditorFacade.cs:314`, `:389` | `LEGAL_EDITOR_OR_DEV_GUARDED` | The coroutine strings are scanner text inside the editor facade, not runtime coroutine scheduling. | None for player runtime. |
| `LogisticsNetworkGraph.cs:301` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Direct `Debug.LogError` is inside an editor-only layout validation method. | None for release runtime; power-grid black-box proof remains separate. |
| All executable `H8Debug` / `Hecton8.Core.H8Debug` lines in the raw gameplay scan except the two `PlayerInteraction` comments above | `LEGAL_EDITOR_OR_DEV_GUARDED` | `H8Debug` methods are decorated with conditional editor/development attributes, so these diagnostic callsites are stripped from non-development player builds. This includes gameplay warnings/errors, combat verifier logs, construction/tool verifier logs, interaction warnings, scavenging/power dump logs, performance monitor logs, and economy/trade dump logs. | Build-symbol proof that release player is non-development, plus runtime proof for the underlying systems. The log lines are not release logging violations. |
| `SolarPanel.cs:527` | `LEGAL_EDITOR_OR_DEV_GUARDED` | The gizmo label assignment is inside `#if UNITY_EDITOR` and `OnDrawGizmosSelected()`. | None for player runtime; solar runtime power proof remains separate. |
| `BallisticsEditorFacade.cs:244`, `:255`, `ArmorPenetrationEditorFacade.cs:235`, `:239`, `StatusEffectsEditorFacade.cs:211`, `:215` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Telemetry labels belong to editor combat facades/windows. | None for player runtime; combat runtime proof remains separate. |
| `ScavengingLootOracle.cs:2439`, `:2455`, `:2467`, `:2475`, `:2490`, `:2513`, `:2517` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Audit/layout labels belong to the editor oracle/tuner window route. | None for player runtime. |
| `SolverConvergenceXRayWindow.cs:84`, `:88`, `:89` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Solver convergence status/telemetry labels are inside an editor X-ray window. | None for player runtime. |

## Current System Verdict

`YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING`

All 193 listed gameplay/tools/construction/inventory/combat/economy static suspect lines are now classified. This does not clear the group for release. The remaining work is concrete: close `RB-008`, `RB-011`, `RB-012`, `RB-121`, `RB-126`, `RB-128`, and `RB-130`; prove authored economy/submarine/construction/scavenging composition; prove encoded SDF/DataMonolith and production drone providers; prove interaction/tool/combat zero-GC routes; and collect first-20-minute, 300-frame, compact/high player-build profiler artifacts.
