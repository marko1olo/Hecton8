# HECTON-8 SCENE / PREFAB SERVICE-OWNER TRUTH

Date: 2026-04-29
Status: PENDING VERIFICATION
Scope: current authored-vs-runtime truth for key service owners in active first-party scenes and prefabs
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `UI_Data_Streaming_ZeroGC_Optimization.txt`

## Purpose

Class-level ownership does not prove authored scene or prefab truth.
This file exists to separate:

- what is definitely authored into scene/prefab assets
- what is definitely ensured at runtime by bootstrap
- what was not found in the scanned authored assets and therefore should be treated as runtime-instanced or unproven

## Scan Boundary

This pass scanned:

- `Assets/_Project/Scenes/*.unity`
- `Assets/_Project/Prefabs/**/*.prefab`
- script ownership files that publish runtime services

This pass did not claim coverage for:

- third-party assets
- inactive archive scenes outside the scanned surface
- dynamic object creation after play-mode startup

Negative evidence in this document is bounded by that scan surface.

## Authored Scene Truth: 00_BOOTSTRAP

Confirmed authored service objects in `Assets/_Project/Scenes/00_BOOTSTRAP.unity`:

| Scene line | Evidence | Conclusion |
|---|---|---|
| `189`, `206` | `SaveManager` object + class identifier | `SaveManager` is scene-authored in bootstrap scene |
| `271`, `288` | `GameTickManager` object + class identifier | `GameTickManager` is scene-authored in bootstrap scene |
| `327`, `344` | `ObjectPoolManager` object + class identifier | `ObjectPoolManager` is scene-authored in bootstrap scene |
| `571` + `Assets/_Project/Scripts/Bootstrap/BootstrapController.cs.meta` guid `37290befeffd3d94796e62b9097c7db9` | scene contains the `BootstrapController` script reference | `BootstrapController` is scene-authored in bootstrap scene |

This is the strongest authored bootstrap proof available from current scan.

## Authored Prefab Truth: Player Stack

Confirmed authored player-facing components on `Assets/_Project/Prefabs/Player.prefab`:

| Prefab line | Component |
|---|---|
| `879` | `HectonPlayerMovement` |
| `985` | `PlayerToolManager` |
| `1025` | `PlayerInventory` |
| `1064` | `PlayerPDA` |
| `1256` | `PlayerBuilder` |
| `2092` | `VisorHUDController` |

Confirmed authored HUD service host:

| Prefab line | Component |
|---|---|
| `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab:2440` | `SuitHUDV4CanvasOverlay` |

Current meaning:

- raw player movement/tool/inventory/PDA/build/visor components are authored on the player prefab
- direct `IUIService` host is authored on the HUD prefab

## Runtime-Ensured Services

`Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` proves that many important services are not expected to be authored in player prefabs or normal scenes.

Confirmed bootstrap steps:

| Source line | Fact |
|---|---|
| `197` | explicit Core step |
| `200` | explicit Environment step |
| `203` | explicit Player step |
| `238` | `InitializeCoreLayer()` exists |
| `254` | `InitializeEnvironmentLayer()` exists |
| `269` | `InitializePlayerLayer()` exists |

Confirmed runtime-ensured services:

| Source line | Runtime owner |
|---|---|
| `248` | `SceneRuntimeService.EnsureRuntimeInstance()` |
| `249` | `EquipmentInteractionHandler.EnsureRuntimeInstance()` |
| `257` | `PhysicsApplySystem.EnsureRuntimeInstance()` |
| `258` | `DebrisManager.EnsureRuntimeInstance()` |
| `259` | `EnvironmentRuntimeContextService.EnsureRuntimeInstance()` |
| `260` | `OceanKinematicsRuntimeService.EnsureRuntimeInstance()` |
| `294` | `InputDispatcher.EnsureRuntimeInstance()` |
| `295` | `PlayerRuntimeContextService.EnsureRuntimeInstance()` |
| `296` | `PlayerInventoryManager.EnsureRuntimeInstance()` |
| `297` | `PlayerSensoryManager.EnsureRuntimeInstance()` |
| `298` | `ContextualPhysicalIkRuntime.EnsureRuntimeInstance()` |

This is strong evidence that several core services are intentionally bootstrap-instanced rather than prefab-authored.

## Current Direct Registry Publishers

Confirmed direct publishers:

| System | Evidence |
|---|---|
| `PlayerRuntimeContextService` | `Assets/_Project/Scripts/Core/PlayerRuntimeContextService.cs:598` |
| `PlayerInventoryManager` | `Assets/_Project/Scripts/Core/PlayerInventoryManager.cs:205` |
| `PlayerSensoryManager` | `Assets/_Project/Scripts/Core/PlayerSensoryManager.cs:226` |
| `EquipmentInteractionHandler` | `Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs:86` |
| `ModularEquipmentEngine` | `Assets/_Project/Scripts/ModularEquipmentEngine.cs:535` |
| `QuestManager` | `Assets/_Project/Scripts/Quest/QuestManager.cs:77` |
| `ConstructionManager` | `Assets/_Project/Scripts/ConstructionManager.cs:742` |
| `PowerGridManager` | `Assets/_Project/Scripts/PowerGridManager.cs:383` |

These registry publications are real.
What scene/prefab scan adds is the distinction between authored presence and runtime creation.

## Negative Evidence: Not Found In Scanned Authored Assets

The following systems were not found in the scanned first-party scene/prefab surface during this pass:

- `GameBootstrapper`
- `SceneRuntimeService`
- `PlayerRuntimeContextService`
- `PlayerInventoryManager`
- `PlayerSensoryManager`
- `EquipmentInteractionHandler`
- `ModularEquipmentEngine`
- `QuestManager`
- `ConstructionManager`
- `PowerGridManager`

Interpretation:

- this does not prove they never exist as authored objects anywhere
- it does prove they were not found in the scanned active first-party scene/prefab surface
- current best explanation is runtime instancing and/or bootstrap-owned persistence for many of them

## Bootstrap Authority Is Still Split

Current bootstrap authority is distributed across:

- `BootstrapController`
- `GameBootstrapper`
- `SceneBootstrap`

Confirmed signals:

| File | Evidence |
|---|---|
| `Assets/_Project/Scripts/Bootstrap/BootstrapController.cs:48` | `BootstrapController` exists as its own bootstrap owner |
| `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:29` | `GameBootstrapper` exists as separate orchestrator |
| `Assets/_Project/Scripts/SceneBootstrap.cs:82` | `SceneBootstrap` exists as separate scene-ready/event bridge |
| `Assets/_Project/Scripts/SceneBootstrap.cs:84-88` | owns listener bucket and pending event queue |
| `Assets/_Project/Scripts/SceneBootstrap.cs:108-127` | publishes `IsGameReady`, `HasActiveInstance`, current player object/transform |

This is the main architectural warning in this area.
Authored/runtime truth is clearer now, but bootstrap authority is still not consolidated into one obvious owner.

## What Looks Good

- bootstrap scene definitely owns critical persistent managers
- player prefab definitely owns the raw player stack
- HUD prefab definitely owns the direct `IUIService` host
- runtime-only services are explicitly created through `EnsureRuntimeInstance()` rather than hidden scene magic

## What Looks Merely Acceptable

- scene/prefab truth is clearer for bootstrap and player layers than for mid-gameplay service owners
- registry publication is explicit, but authored placement for many services remains intentionally indirect

## What Looks Weak

- many service owners remain invisible at authored-asset level and only visible through bootstrap code
- bootstrap authority is split across three classes
- absence-from-scan evidence is bounded and must not be overstated

## Failure Modes To Watch

- service can publish correctly to `GlobalRegistry` while authored prefab root has regressed
- bootstrap can instantiate services correctly while scene/prefab assumptions drift
- split bootstrap authority can create duplication or timing confusion during scene-transition regressions

## Regression Model

| Dimension | Impact |
|---|---|
| CPU | None. Documentation-only pass. |
| GC | None. Documentation-only pass. |
| Memory | None. Documentation-only pass. |
| Cadence | None. Runtime code unchanged. |
| Correctness | Improves distinction between authored asset truth and bootstrap/runtime-instanced truth. |

## Verdict

Current first-party truth is now much clearer:

- `00_BOOTSTRAP` definitely authors `SaveManager`, `GameTickManager`, `ObjectPoolManager`, and `BootstrapController`
- `Player.prefab` definitely authors the raw player gameplay stack
- `Suit_HUD_Canvas.prefab` definitely authors the direct UI service host
- many important gameplay services are intentionally runtime-instanced by `GameBootstrapper`
- bootstrap authority is still split across `BootstrapController`, `GameBootstrapper`, and `SceneBootstrap`

That is a better, more honest model than treating all service owners as if they lived directly in scenes or prefabs.

STATUS: PENDING VERIFICATION
