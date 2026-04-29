# HECTON-8 TOOLS / INTERACTION / OPERATIONAL SYSTEM MAP

Date: 2026-04-30
Status: PENDING VERIFICATION
Scope: source-backed ownership map for player tools, operational interaction routing, scanner/cutter/repair/beacon branches, and adjacent save/event surfaces
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `UI_Data_Streaming_ZeroGC_Optimization.txt`, `STRM_Persistent_Object_Registry.txt`

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
