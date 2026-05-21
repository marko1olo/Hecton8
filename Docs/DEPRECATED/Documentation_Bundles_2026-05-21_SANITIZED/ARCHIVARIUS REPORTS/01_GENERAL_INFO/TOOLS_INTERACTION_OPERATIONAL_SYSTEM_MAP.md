# HECTON-8 TOOLS / INTERACTION / OPERATIONAL SYSTEM MAP

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

Scope: source-backed ownership map for player tools, operational interaction routing, scanner/cutter/repair/beacon branches, and adjacent save/event surfaces
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `UI_Data_Streaming_ZeroGC_Optimization.txt`, `STRM_Persistent_Object_Registry.txt`

2026-05-01 trust note:

- Read `Docs/Reports/2026-05-18_DOCUMENTATION_REPORT_VAULT_AND_NAVIGATION_R17_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_R15_NAVIGATION_SUPERSESSION_R16_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_ACTIVE_ENTRYPOINT_NAVIGATION_R15_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_BATCH008_BINARY_HYGIENE_R14_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_GENERIC_REPORT_BOUNDARIES_R13_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_ACTIVE_REMAINDER_R11_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_LONGTAIL_INTERIOR_R10_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_EVIDENCE_LANGUAGE_AND_COUNTERS_R9_LOCAL.md`, `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`, and `Docs/Reports/2026-05-15_DOCUMENTATION_HONEST_ANALYSIS.md` before any older counter, root-path, build-artifact, or pre-Batch008 binary-hygiene claim. Treat the May 4 / May 1 reports as historical/domain context only.
- This file remains a source-backed tool/interaction ownership map, not proof of live multi-tool traversal, save/load, or zero-GC interaction spam.
- Current project-wide risks still include broad physics masks, mixed runtime service ownership, and local job/readback barriers outside dispatcher-owned windows.

## Purpose

This file exists to answer one concrete question:

Who actually owns the current tool runtime, interaction routing, scanner/cutter/repair/beacon branches, and the save/event surfaces around them.

This is not runtime proof.
This is a source-backed ownership map.

## Proof Boundary

The evidence below was derived from:

- `Assets/_Project/Scripts/**/*.cs`
- `Assets/_Project/Prefabs/Player.prefab`
- bootstrap/runtime files that publish service owners into `GlobalRegistry`

It does not prove:

- integrated tool play-mode correctness
- zero-GC profiler correctness under live usage spam
- save/load correctness for every tool branch
- that every authored prefab reference is currently valid in the active scene

## Core Ownership Model

The current tool domain is not owned by one class.
It is split across five layers:

1. Authored player-prefab components.
2. Tool base contract and per-tool branches.
3. Runtime-instanced service owners for interaction and compiled tool state.
4. Save-participant side systems.
5. Operational support surfaces such as loadout provisioning, beacon networking, and scan log persistence.

That split is structurally real in code.
Without a map, it is easy to misread.

## Layer 1: Authored Player Root

Confirmed authored tool-facing components on `Assets/_Project/Prefabs/Player.prefab`:

| Prefab line | Component | Role |
|---|---|---|
| `985` | `PlayerToolManager` | active-hand tool selection and per-frame tool dispatch owner |
| `1102` | `PlayerFlashlight` | flashlight simulation and event-source owner |
| `1151` | `ToolLoadoutProvisioner` | startup provisioning helper for core loadout and construction materials |
| `1256` | `PlayerBuilder` | authored build-placement and construction-action owner |
| `1344` | `ScanLogSystem` | authored save participant for scan discovery ledger |
| `1464` | `BeaconNetworkSystem` | authored save participant and runtime marker network owner |

Current evidence does not show authored prefab ownership for:

- `EquipmentInteractionHandler`
- `ModularEquipmentEngine`
- `ToolDurabilitySystem`

Those surfaces are presently runtime-instanced or resolved elsewhere.

## Layer 2: Base Tool Contract

### 2.1 `PlayerTool` Is The Real Common Spine

`Assets/_Project/Scripts/PlayerTool.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `27` | class is `PlayerTool : MonoBehaviour, IPoolable` |
| `87` | reads durability through `ToolDurabilitySystem.Instance` |
| `207-218` | subscribes and unsubscribes to `ToolDurabilitySystem.OnToolBroken` on equip/unequip |
| `613` | forces runtime registration through `ModularEquipmentEngine.EnsureRuntimeInstance()` |

`PlayerTool` is not a passive helper.
It owns the common operational contract for:

- equip / unequip lifecycle
- pooled spawn / despawn resets
- durability reads and drain path
- modular runtime registration
- battery and wireless drain path
- recoil queueing
- operational summary and directive text

This means many apparently separate tool classes are only thin surface branches over one shared runtime spine.

### 2.2 Important Consequence

Tool behavior is currently distributed across:

- the concrete tool class
- `PlayerTool`
- `ToolDurabilitySystem`
- `ModularEquipmentEngine`
- `EquipmentInteractionHandler`

That architecture is workable.
It is not cheap to reason about from a cold read.

## Layer 3: Player-Hand Runtime Owner

### 3.1 `PlayerToolManager`

`Assets/_Project/Scripts/PlayerToolManager.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `44` | class is `PlayerToolManager : MonoBehaviour, ITickable, IUpdatable` |
| `196` | registers to tick on enable path |
| `228` | publishes itself into dispatcher through `GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player)` |
| `243` | resolves input through `GlobalRegistry.Input` |
| `272` | owns the main per-frame loop for tool dispatch |

This is the direct hand/runtime coordinator.
It owns:

- active slot selection
- player input to tool invocation
- hand-level tool ticking
- equip/unequip transitions between active tools

It is not the deep interaction authority.
It is the front controller for the player's currently held tool.

## Layer 4: Runtime-Instanced Authorities

### 4.1 `EquipmentInteractionHandler`

`Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `16` | class is `EquipmentInteractionHandler : MonoBehaviour, IInteractionSignalService, IUpdatable` |
| `39` | owns `NativeQueue<InteractionSignal>` |
| `40-42` | owns scheduled and staging `NativeArray<RaycastCommand>` lanes |
| `62` | service can be created through `EnsureRuntimeInstance()` |
| `86` | publishes through `GlobalRegistry.RegisterInteractionSignalService(this)` |
| `89` | also registers to dispatcher as `PriorityLayer.Core` |
| `158-165` | cold-allocates queue and raycast lanes as persistent runtime state |
| `469` | schedules the queued raycast batch through `RaycastCommand.ScheduleBatch(...)` |

This is the authoritative interaction-routing owner.
It is not just a raycast helper.

It owns:

- deferred interaction signal queueing
- staged raycast batch scheduling
- primary hit resolution for queued tool requests
- fallback routing into `ICuttable`
- interaction dispatch toward `IInteractionSignalConsumer`

This is one of the clearest examples where the codebase is more structured than a naive read suggests.

### 4.2 `ModularEquipmentEngine`

`Assets/_Project/Scripts/ModularEquipmentEngine.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `20` | class is `ModularEquipmentEngine : MonoBehaviour, IModularEquipmentService, IUpdatable` |
| `40-42` | owns native tool-state, compiled stat, and id-index tables |
| `59` | service can be created through `EnsureRuntimeInstance()` |
| `68` | explicit `InitializeService()` exists |
| `139` | exposes `RegisterTool(PlayerTool tool)` |
| `180` | exposes `UnregisterTool(PlayerTool tool, uint toolId)` |
| `535` | publishes through `GlobalRegistry.RegisterModularEquipmentService(this)` |

This is the compiled runtime state owner for tools.
It is not a visual gadget.

It owns:

- per-tool runtime registration
- compiled stat lookup
- battery state
- durability mirror
- upgrade flags
- heat and cooldown surfaces

`PlayerTool` depends on it directly.
That means the operational tool chain is not just `PlayerToolManager -> ToolClass`.
It is `PlayerToolManager -> PlayerTool -> ModularEquipmentEngine`.

## Layer 5: Specialized Operational Branches

### 5.1 Build Placement And Construction Action

`Assets/_Project/Scripts/PlayerBuilder.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `51` | class is `PlayerBuilder : PlayerTool` |
| `1209` | registers placed module data through a construction manager path |
| `1597` | resolves input through `GlobalRegistry.Input` |
| `1628-1645` | resolves player and environment through runtime context services |

`PlayerBuilder` is the authored player-side construction action owner.
It is not the whole construction system.
It is the player's placement and build-action bridge into it.

### 5.2 Scanner Branch

`Assets/_Project/Scripts/ScannerTool.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `28` | class is `ScannerTool : PlayerTool, IBatteryTool` |
| `365-366` | owns a persistent scientific cone `NativeArray<RaycastCommand>` batch lane |
| `568` | raises `ScanEvents.RaiseScanTriggered(...)` |
| `875` | raises entry discovery events |
| `920` | raises node-found events |
| `1318` | allocates persistent scientific ray lane |
| `1410` | schedules `RaycastCommand.ScheduleBatch(...)` |
| `2205` | file also contains `ScannerPulseDrawer : MonoBehaviour, ITickable, IUpdatable` |
| `2354` | pulse drawer registers itself as `PriorityLayer.UI` |

The scanner branch is split:

- `ScannerTool` owns scan action logic and batch probing
- `ScanEvents` is the event surface
- `ScanLogSystem` owns persistence of discoveries
- `ScannerPulseDrawer` owns one presentation branch

This is not a single-owner system.

### 5.3 Scan Persistence

`Assets/_Project/Scripts/ScanLogSystem.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `11` | class is `ScanLogSystem : MonoBehaviour, ISaveable, IScanEventListener` |
| `60-61` | `SavePriority => 35`, `LoadPriority => 35` |
| `86-87` | registers with `SaveManager.Instance` and `ScanEvents` |
| `97-98` | unregisters from both on disable |
| `223` | resolves event metadata through `ScanEvents.TryResolveEntryMetadata(...)` |

This is the actual persistence owner for discovered scan entries.
`ScannerTool` itself is not the save authority.

### 5.4 Durability Runtime

`Assets/_Project/Scripts/Tools/ToolDurabilitySystem.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `19` | class is `ToolDurabilitySystem : MonoBehaviour, ISaveable, ISlowTickable, IUpdatable, ILateFrameTickable` |
| `80` | owns `NativeQueue<BreakdownEvent>` |
| `96-97` | `SavePriority => 20`, `LoadPriority => 20` |
| `530-531` | resolves `PlayerToolManager` through `GlobalRegistry.Player.ToolManager` when available |
| `552` | cold-allocates breakdown queue |
| `775` | registers as `PriorityLayer.Player` slow tick |
| `795` | registers as `PriorityLayer.Player` update |
| `815` | registers as `PriorityLayer.Player` late-frame tick |
| `833` | registers with `GlobalRegistry.Save` |

This is the save owner and degradation runtime for tool condition.
It is more central than older player docs implied.

### 5.5 Cutter / Repair / Flashlight / Beacon Branches

Confirmed tool-class anchors:

| File | Current role |
|---|---|
| `LaserCutter.cs` | `PlayerTool + IToolModule`, publishes into `IInteractionSignalService`, reads `ICuttable`, uses `GlobalRegistry.Input` and audio |
| `RepairTool.cs` | `PlayerTool + IBatteryTool`, player-driven repair branch |
| `FlashlightTool.cs` | `PlayerTool + IBatteryTool`, tool wrapper around owned `PlayerFlashlight` runtime |
| `BeaconDeployerTool.cs` | `PlayerTool`, deploy/retract branch into beacon network surface |
| `KnifeTool.cs` | `PlayerTool`, simpler direct tool branch |
| `HarpoonLauncherTool.cs` | `PlayerTool`, tether/grapple branch |
| `PropulsionTool.cs` | `PlayerTool`, transport/manipulation branch |

Notable line-backed facts:

- `LaserCutter.cs:609` and `1188` route through `GlobalRegistry.InteractionSignals`
- `LaserCutter.cs:1165` checks `ICuttable`
- `FlashlightTool.cs:358` resolves `PlayerFlashlight` from `GlobalRegistry.Player` when available
- `PlayerFlashlight.cs:72` is the actual flashlight runtime owner, not just a dummy presentation class
- `PlayerFlashlight.cs:443`, `454`, `810`, `828`, `875` raise `FlashlightEvents`

### 5.6 Beacon Network Branch

`Assets/_Project/Scripts/BeaconNetworkSystem.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `13` | class is `BeaconNetworkSystem : MonoBehaviour, ISaveable` |
| `52-53` | `SavePriority => 37`, `LoadPriority => 37` |
| `71` | registers with `SaveManager.Instance` |
| `81` | exposes `GetOrCreate()` |
| `96` | exposes deploy path |
| `119` | exposes retract-nearest path |
| `275` | writes operation records through `FieldOperationLogSystem.RecordOperation(...)` |

This is not just a marker list.
It is the save-participant owner for deployed beacons and the operational runtime network for field markers.

## Support Surface: Tool Loadout Provisioning

`Assets/_Project/Scripts/ToolLoadoutProvisioner.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `21` | class is `ToolLoadoutProvisioner : MonoBehaviour` |
| `26-27` | directly references `PlayerInventory` and `PlayerToolManager` |
| `71-74` | provisions tool kit and construction materials on startup depending on config |
| `143-146` | can provision and assign a core loadout |
| `170` | falls back to `GlobalRegistry.Player.ToolManager` when resolving runtime tool manager |

This is not part of the hot action loop.
It is still part of the current operational tool pipeline because it shapes the initial tool inventory and assignment surface.

## Interaction Contract Layer

`Assets/_Project/Scripts/Interaction/EquipmentInteractionContracts.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `10-18` | defines `ToolStateBits` |
| `23-29` | defines `ToolActionMode` |
| `34-42` | defines `InteractionEffectType` |
| `47-83` | defines stable capability masks |
| `89-117` | defines `InteractionPacket` |
| `124-155` | defines `InteractionSignal` |
| `160-184` | defines `IToolModule` |
| `189-199` | defines `IInteractionSignalConsumer` |

This is the formal protocol surface between:

- tool runtime
- interaction queue owner
- downstream cut/consume targets

It matters because the system is not held together only by ad-hoc per-tool method calls.
There is a real packet/signal contract underneath.

## Current Operational Chain

The current player-tool chain can be summarized as:

`Player.prefab authored components`
-> `PlayerToolManager`
-> concrete `PlayerTool` branch
-> `PlayerTool` shared operational spine
-> `ModularEquipmentEngine`
-> `EquipmentInteractionHandler`
-> target contracts (`ICuttable`, `IInteractionSignalConsumer`, scan/discovery listeners)
-> save participants (`ToolDurabilitySystem`, `ScanLogSystem`, `BeaconNetworkSystem`)

That is a deeper and more structured chain than older summaries implied.

## 2026-04-30 Operational Linkage Recheck

Prompt-targeted recheck:

| Link | Source evidence | Operational meaning |
|---|---|---|
| player intent ingress | `PlayerToolManager.cs:243`, `308` read `GlobalRegistry.Input`; `PlayerToolManager.cs:272+` owns hand-level dispatch | input enters the tool domain through the held-tool front controller |
| inventory/tool ownership | `PlayerToolManager.cs:55` serializes `PlayerInventory`; `PlayerToolManager.cs:136` exposes it internally | held tools are still inventory-gated, not purely ability-driven |
| compiled equipment state | `ModularEquipmentEngine.cs:40-42` owns `NativeArray<ToolState>`, `NativeArray<ToolRuntimeStats>`, and `NativeHashMap<uint,int>` | battery, heat, durability mirrors, and upgrade state have a central native runtime owner |
| tool registration | `ModularEquipmentEngine.cs:139` registers `PlayerTool`; `PlayerTool.cs:613` forces `ModularEquipmentEngine.EnsureRuntimeInstance()` | concrete tools become indexed runtime state records before downstream systems read their stats |
| queued interaction dispatch | `EquipmentInteractionHandler.cs:43-47` owns queue and raycast lanes; `EquipmentInteractionHandler.cs:514` schedules `RaycastCommand.ScheduleBatch` | tool hits are resolved by a deferred interaction service, not by every tool firing ad-hoc physics |
| world mutation handoff | `EquipmentInteractionHandler.cs:314-316` routes to `IInteractionSignalConsumer`; `EquipmentInteractionHandler.cs:311` contains base-module suppression guard | mutation authority is contract-routed and has construction-specific safety exceptions |
| build-cost read | `HabitatConstructionManager.cs:161` checks build resources through `PlayerInventory` | construction placement is inventory-backed |
| build-cost consume | `HabitatConstructionManager.cs:200-219` consumes required item hashes through `TryRemoveFirstMatchingItemByHash` | construction mutates inventory before committing build state |
| placement validation | `HabitatConstructionManager.cs:267` schedules validation; `276-279` consumes only after `IsCompleted` | construction has a Burst-backed validation lane with a guarded completion point |
| habitat graph publication | `HabitatGraphManager.cs:46-52` owns native CSR graph buffers; `1108-1119` allocates node/edge/reachability snapshots | placed modules feed a graph backend, not just spawned GameObjects |

Current layered flow:

`GlobalRegistry.Input`
-> `PlayerToolManager`
-> active `PlayerTool`
-> `ModularEquipmentEngine` native state
-> `EquipmentInteractionHandler` queue / batch raycast
-> target contract (`IInteractionSignalConsumer`, `ICuttable`, construction/base-module guards)
-> domain owner (`Voxel`, `BaseModule`, scanner log, beacon network, repair target)
-> persistence owner where applicable (`ToolDurabilitySystem`, `ScanLogSystem`, `BeaconNetworkSystem`, construction save participants)

Construction-specific flow:

`PlayerBuilder`
-> `ConstructionManager` / `HabitatConstructionManager`
-> `PlayerInventory` resource check and consume
-> Burst placement integrity validation
-> module spawn / placement commit
-> `HabitatGraphManager` CSR rebuild
-> downstream power, atmosphere, logistics, and module-integrity consumers

The important boundary:

- `PlayerToolManager` is the intent and active-hand owner.
- `ModularEquipmentEngine` is the tool runtime-state owner.
- `EquipmentInteractionHandler` is the interaction signal and batch-query owner.
- `HabitatConstructionManager` is the construction transaction and validation owner.
- `HabitatGraphManager` is the post-placement topology owner.
- `PlayerInventory` is not a passive bag in this chain; it is the resource ledger that construction consumes.

Risk:

- the chain is real and layered, but it is not flat; regressions can appear two owners away from the visible tool class.
- `EquipmentInteractionHandler` still uses a runtime-created `DontDestroyOnLoad` service root, which keeps it in mixed-authority territory.
- construction has its own native validation and graph truth, so tool-side build success is not the final authority.

## What Looks Good

- Tool runtime is not a random cluster of MonoBehaviours; there is a clear shared base spine in `PlayerTool`.
- Interaction routing is queue-backed and batch-raycast-backed rather than naive direct per-tool physics spam.
- Scanner and durability branches both use explicit save participants instead of burying persistence inside the hand tool.
- `ModularEquipmentEngine` centralizes compiled runtime stats and upgrade state instead of scattering them across each tool.
- The player prefab already carries the most important authored operational surfaces: hand manager, flashlight, builder, scan log, beacon network.

## What Looks Merely Acceptable

- The domain is heavily layered, which improves specialization but worsens discoverability.
- `ToolLoadoutProvisioner` is useful as a startup helper, but it also adds another piece of authored-player operational state that future audits must remember.
- Some save participants still register through `SaveManager.Instance`, while other runtime services use `GlobalRegistry.Save`. That is not automatically broken, but it is a consistency smell.

## What Looks Weak

- Tool authority is split across many files and runtime mirrors. Without documentation, ownership is easy to misread.
- `ToolDurabilitySystem`, `EquipmentInteractionHandler`, and `ModularEquipmentEngine` are important enough that hiding them outside the authored player prefab increases bootstrap dependence.
- `BeaconNetworkSystem` still uses a classic `Instance` pattern and direct `SaveManager.Instance` registration instead of the newer registry style. That is a real architecture mismatch with the stronger `GlobalRegistry` direction.
- No integrated measured runtime proof exists for multi-tool spam, scan spam, beacon spam, or cutter/repair interaction stress.

## Failure Modes To Watch

- Hand-tool behavior can look broken even when the actual failure is in `ModularEquipmentEngine` registration.
- Interaction failures can hide one layer deeper inside the queued interaction lane rather than in the visible tool class.
- Scan discovery can appear healthy while `ScanLogSystem` persistence is broken, or vice versa.
- Beacon deployment can succeed visually while save replay or trim behavior regresses.
- Bootstrap or player-runtime-context failures can leave authored tool components present but partially unbound.

## Regression Model

| Dimension | Impact |
|---|---|
| CPU | None. Documentation-only pass. |
| GC | None. Documentation-only pass. |
| Memory | None. Documentation-only pass. |
| Cadence | None. Runtime code unchanged. |
| Correctness | Improves owner visibility for one of the most layered gameplay domains and reduces future false assumptions about where tool behavior really lives. |

## 2026-04-30 Late Revalidation - Intent To World Mutation Chain

Static source scan was repeated against the current `Interaction`, `Tools`, `Construction`, and `Visor` folders after the broader forensic docs were read.
The relevant scan surface contained 134 class/interface declarations in those four folders.

### Operational chain structure

Current source-backed chain:

`Player input`
-> `PlayerToolManager.RefreshInputSubscriptions()` via `GlobalRegistry.Input` (`PlayerToolManager.cs:243-255`)
-> `PlayerToolManager.Tick()` active-tool dispatch (`PlayerToolManager.cs:279-320`)
-> concrete `PlayerTool` branch
-> `PlayerTool` common registration and interaction service calls (`PlayerTool.cs:190`, `PlayerTool.cs:613-619`)
-> `ModularEquipmentEngine` runtime state owner (`ModularEquipmentEngine.cs:139`, `ModularEquipmentEngine.cs:328-375`)
-> `EquipmentInteractionHandler` queued signal/raycast owner (`EquipmentInteractionHandler.cs:112`, `EquipmentInteractionHandler.cs:472-514`)
-> world-side consumer: `IInteractionSignalConsumer`, `ICuttable`, voxel cut, boil, repair, scan, beacon, or builder branch

Construction-specific chain:

`PlayerToolManager`
-> `BuilderTool.UsePrimary/UseSecondary/ToolTick` delegation (`BuilderTool.cs:276-312`)
-> `PlayerBuilder.TryPlaceModuleInternal()` transaction (`PlayerBuilder.cs:967-1038`)
-> `HabitatConstructionManager.HasBuildResources/ConsumeBuildResources()` (`HabitatConstructionManager.cs:161`, `HabitatConstructionManager.cs:200-219`)
-> `PlayerInventory` hash-based resource removal
-> `HabitatConstructionManager.ScheduleIntegrityValidation()` and Burst validation job (`HabitatConstructionManager.cs:233`, `HabitatConstructionManager.cs:599`, `HabitatConstructionManager.cs:711`)
-> placed module registration (`PlayerBuilder.cs:1247`)
-> `ConstructionManager.RegisterModule()` and graph refresh (`ConstructionManager.cs:254-271`, `ConstructionManager.cs:1012-1017`)
-> `HabitatGraphManager.Rebuild()` CSR topology publish (`HabitatGraphManager.cs:97-137`)
-> hydrodynamic stress, power, logistics, and downstream module-state consumers (`HabitatGraphManager.cs:140-183`)

### Layering facts

- `PlayerToolManager` owns hand intent and active slot dispatch, not deep interaction truth.
- `PlayerTool` is the common operational spine and forces modular runtime registration.
- `ModularEquipmentEngine` owns compiled tool state, heat, cooldown, battery, upgrade flags, and durability mirrors.
- `EquipmentInteractionHandler` owns the queued interaction lane and staged raycast batch.
- `PlayerBuilder` owns the player-side construction transaction, but construction validity is not final until `HabitatConstructionManager` and `HabitatGraphManager` accept it.
- `PlayerInventory` is a hard dependency in construction, not a passive container.

### Job/barrier honesty

- `EquipmentInteractionHandler.CompleteScheduledRaycasts()` currently checks `_scheduledRaycastHandle.IsCompleted` before completing (`EquipmentInteractionHandler.cs:472-477`). That is the right shape for avoiding a mid-frame forced wait in the interaction raycast lane.
- `HabitatConstructionManager` still owns explicit completion authority through `CompletePendingValidation()` (`HabitatConstructionManager.cs:599`). That is not automatically wrong, but it must remain outside input-spam cadence or be proven safe in profiler.
- `HabitatGraphManager.Rebuild()` is synchronous topology publication. It is expected after construction mutation, but it is a graph-wide authority and should not absorb unrelated construction policy.

### Surgery log - Interaction Linkage Map structure

Required structure for this map going forward:

1. Proof boundary and scan date.
2. Authored player surfaces.
3. Player intent owner: `PlayerToolManager`.
4. Common tool spine: `PlayerTool`.
5. Runtime state owner: `ModularEquipmentEngine`.
6. Interaction query/signal owner: `EquipmentInteractionHandler`.
7. Specialized tool branches: scanner, cutter, repair, beacon, builder.
8. Construction transaction path: `PlayerBuilder` -> `HabitatConstructionManager` -> `PlayerInventory` -> `ConstructionManager` -> `HabitatGraphManager`.
9. Job/barrier risks.
10. Save/event surfaces and regression model.

### Current open risks

- Runtime-created service roots (`EquipmentInteractionHandler`, `ModularEquipmentEngine`) remain mixed-authority surfaces because they are critical but not obviously authored in the player prefab.
- Construction success has several owners. A bug may appear as a tool failure while actually being resource hash, integrity validation, graph rebuild, or module registration.
- No live GC/CPU proof exists for input spam across tool swap, scan, cut, repair, and build placement in one session.

## Unified EquipmentInteractionHandler Execution Flow - 2026-04-30

### Mandates followed

- `CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`

### Exact path: input to raycast lane

`Player Input`
-> `PlayerToolManager.RefreshInputSubscriptions()` binds input actions through `GlobalRegistry.Input`
-> `PlayerToolManager.Tick()` dispatches the active tool state
-> concrete `PlayerTool` branch calls the shared interaction service
-> `GlobalRegistry.InteractionSignalService`
-> `EquipmentInteractionHandler.TryRaycastPrimary(...)` (`EquipmentInteractionHandler.cs:97`, `EquipmentInteractionHandler.cs:105`)
-> `QueuePrimaryRaycast(...)` stages one request per requester id (`EquipmentInteractionHandler.cs:113`, `EquipmentInteractionHandler.cs:512`)
-> `LateFrameTick()` calls `FlushSignals()` then `ScheduleStagedRaycasts()` (`EquipmentInteractionHandler.cs:205-208`)
-> `RaycastCommand.ScheduleBatch(...)` writes staged commands into the job lane (`EquipmentInteractionHandler.cs:602`)
-> the next `Tick()` calls `CompleteScheduledRaycasts()` only when `_scheduledRaycastHandle.IsCompleted` is true (`EquipmentInteractionHandler.cs:118-120`, `EquipmentInteractionHandler.cs:560`)
-> `_completedRequesterIds`, `_completedHasHit`, and `_completedHits` become the read side for the next service query.

### Exact path: NativeQueue to damage signal

`Tool effect`
-> `EquipmentInteractionHandler.Publish(in InteractionSignal, Collider)` enqueues into `_signalQueue` and writes the collider side-channel (`EquipmentInteractionHandler.cs:84`, `EquipmentInteractionHandler.cs:46`)
-> `LateFrameTick()` drains via `FlushSignals()` (`EquipmentInteractionHandler.cs:205`, `EquipmentInteractionHandler.cs:224`)
-> `DispatchSignal(...)` routes by `InteractionEffectType` (`EquipmentInteractionHandler.cs:282`)
-> `PlasmaCut` tries `HectonVoxelVolume.ApplyPlasmaCutDda(...)` first, then falls back to cut damage (`EquipmentInteractionHandler.cs:306`, `EquipmentInteractionHandler.cs:293`)
-> `Boil` calls `SubmarineFluidDynamics.InjectLocalizedWaterHeat(...)` through `GlobalRegistry.Submarine` (`EquipmentInteractionHandler.cs:327`)
-> default/cut flow calls `DispatchCutDamage(...)` (`EquipmentInteractionHandler.cs:339`)
-> `IInteractionSignalConsumer.ApplyInteractionSignal(in signal, runtimeHitPoint)` is preferred (`EquipmentInteractionHandler.cs:350`)
-> fallback is `ICuttable.ApplyCutDamage(signal.PowerDelivered, runtimeHitPoint)` (`EquipmentInteractionHandler.cs:355`).

### Barrier and allocation notes

- Raycast completion is non-blocking until the handle reports completion. This is the required shape for avoiding forced input-thread waits.
- Signal dispatch is main-thread because it resolves `Collider`, `GetComponent`, and managed interfaces. This is not Burst-side damage application.
- `_signalQueue` is `Allocator.Persistent` and owned by `EquipmentInteractionHandler`; it must remain paired with a disposal path and scene/service shutdown.
- The side-channel collider array is the managed bridge. It is not a Burst-safe event payload and must not be moved into a worker job without replacing object references.

### Regression model

- CPU risk: `DispatchCutDamage` still resolves components from colliders. Input-spam profiling is required before claiming 60 FPS safety.
- GC risk: service dispatch uses managed interfaces. No allocation was proven by profiler in this documentation pass.
- Memory risk: persistent queue/raycast buffers are safe only if disposal remains tied to service teardown.
- Correctness risk: completed raycast results are one frame delayed by design; tools must tolerate stale/no-hit returns.

## Verdict

The current tools / interaction / operational stack is not one owner and not one layer.

Current truth:

- `PlayerToolManager` owns hand-level active tool dispatch
- `PlayerTool` is the common operational spine
- `ModularEquipmentEngine` owns compiled runtime tool state
- `EquipmentInteractionHandler` owns deferred interaction routing
- `PlayerBuilder`, `ScannerTool`, `LaserCutter`, `RepairTool`, `FlashlightTool`, `BeaconDeployerTool`, and other branches are specialized action owners
- `ToolDurabilitySystem`, `ScanLogSystem`, and `BeaconNetworkSystem` are separate save-participant owners

This domain is structurally richer than older summaries suggested.
It still lacks live runtime proof.

STATUS: PENDING VERIFICATION
