# HECTON-8 PLAYER / GAMEPLAY CORE MAP

Date: 2026-05-07
Status: PENDING VERIFICATION
Scope: source-backed ownership map for the current player-facing gameplay core
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `UI_Data_Streaming_ZeroGC_Optimization.txt`

2026-05-01 trust note:

- Read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before using this map as current project truth.
- This file is still useful as a player-domain ownership map, but its source-line evidence was not fully revalidated in this pass.
- Current player-domain risks include oversized `HectonPlayerMovement`, presentation-dependent gameplay branches, service authority drift, and unmeasured hot-path GC/CPU.

## Purpose

This document exists to answer one narrow question:

Who currently owns the player-facing gameplay stack in first-party code, and how that stack is split between authored prefab components, bootstrap-owned runtime services, and feature-level managers.

This is not a play-mode proof.
This is a source-backed ownership map.

## Proof Boundary

The evidence below was derived from:

- `Assets/_Project/Scripts/**/*.cs`
- `Assets/_Project/Prefabs/Player.prefab`
- `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab`
- bootstrap/runtime files that publish services into `GlobalRegistry`

It does not prove:

- that every referenced component behaves correctly in play mode
- that every service survives scene transitions without regression
- that every player-facing path is allocation-free under live profiler capture

## Core Ownership Model

The current player stack is not owned by one monolith.
It is split into three layers:

1. Authored player prefab components.
2. Bootstrap-owned runtime context and service mirrors.
3. Specialized gameplay managers for interaction, equipment, UI, and quest/state surfaces.

That split is deliberate in code, but it increases dependency-reading cost.

## Layer 1: Authored Player Root

Confirmed authored components on `Assets/_Project/Prefabs/Player.prefab`:

| Prefab line | Component | Role |
|---|---|---|
| `879` | `HectonPlayerMovement` | primary movement and underwater locomotion authority |
| `985` | `PlayerToolManager` | hand/tool runtime coordination |
| `1025` | `PlayerInventory` | inventory state container |
| `1064` | `PlayerPDA` | player PDA owner |
| `1256` | `PlayerBuilder` | construction tool / build interaction owner |
| `2092` | `VisorHUDController` | visor-facing HUD presentation owner |

Confirmed authored UI service host:

| Prefab line | Component | Role |
|---|---|---|
| `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab:2440` | `SuitHUDV4CanvasOverlay` | direct `IUIService` implementor and suit HUD runtime surface |

## Layer 2: Bootstrap-Owned Runtime Service Shell

The player domain is mirrored into `GlobalRegistry` through narrow runtime services rather than direct scene lookups.

### 2.1 Runtime Context Anchor

`Assets/_Project/Scripts/Core/PlayerRuntimeContextService.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `19` | class is `PlayerRuntimeContextService : MonoBehaviour, IPlayerRuntimeContext, IUpdatable` |
| `191` | service can be created through `EnsureRuntimeInstance()` |
| `226` | explicit `InitializeService()` gates registry publishing |
| `598` | publishes itself through `GlobalRegistry.RegisterPlayerRuntimeContext(this)` |

This service is the current canonical player read model.
It is responsible for rebinding and exposing:

- player root
- transform
- `HectonPlayerMovement`
- `Rigidbody`
- `PlayerToolManager`
- `PlayerInventory`
- `PlayerPDA`
- `PlayerBuilder`
- `VisorHUDController`
- `PlayerFlashlight`
- `PlayerThrusterAudio`
- `HectonUnderwaterVisuals`
- `HUDNotification`

It is not the implementation owner of those systems.
It is the central access surface that republishes them coherently.

### 2.2 Inventory / Tooling Mirror

`Assets/_Project/Scripts/Core/PlayerInventoryManager.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `14` | class is `PlayerInventoryManager : MonoBehaviour, IPlayerInventoryService, IUpdatable` |
| `83` | service can be created through `EnsureRuntimeInstance()` |
| `173-176` | resolves `ToolManager`, `Inventory`, `PlayerBuilder`, `HandAnchor` from `GlobalRegistry.Player` |
| `205` | publishes through `GlobalRegistry.RegisterPlayerInventoryService(this)` |

This is not an independent gameplay owner.
It is a narrow service mirror over the player runtime context.

### 2.3 Sensory / Camera / HUD Mirror

`Assets/_Project/Scripts/Core/PlayerSensoryManager.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `16` | class is `PlayerSensoryManager : MonoBehaviour, IPlayerSensoryService, IUpdatable` |
| `192-197` | resolves camera, flashlight, thruster audio, underwater visuals, visor, and HUD notification from `GlobalRegistry.Player` |
| `226` | publishes through `GlobalRegistry.RegisterPlayerSensoryService(this)` |

This service is also a mirror, not a primary feature owner.

## Layer 3: Specialized Gameplay Authorities

### 3.1 Interaction Signal Owner

`Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `16` | class is `EquipmentInteractionHandler : MonoBehaviour, IInteractionSignalService, IUpdatable` |
| `39` | owns `NativeQueue<InteractionSignal>` |
| `86` | publishes through `GlobalRegistry.RegisterInteractionSignalService(this)` |
| `158-166` | owns persistent non-alloc interaction/raycast lanes |
| `469` | uses `RaycastCommand.ScheduleBatch(...)` for staged tool raycasts |

This is the authoritative interaction signal lane.
It is more than a simple raycast helper.
It owns:

- deferred interaction signal queueing
- frame-latent tool raycast staging
- dispatch into `IInteractionSignalConsumer`
- fallback into `ICuttable`

### 3.2 Modular Equipment Runtime

`Assets/_Project/Scripts/ModularEquipmentEngine.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `20` | class is `ModularEquipmentEngine : MonoBehaviour, IModularEquipmentService, IUpdatable` |
| `59` | service can be created through `EnsureRuntimeInstance()` |
| `83-96` | owns persistent native state buffers for tool slots and tool-id lookup |
| `535` | publishes through `GlobalRegistry.RegisterModularEquipmentService(this)` |

This service is the actual runtime owner for compiled tool/module state.
It is not authored on the player prefab in the scanned assets.
Current evidence points to runtime instancing rather than scene-authored ownership.

### 3.3 Quest Runtime

`Assets/_Project/Scripts/Quest/QuestManager.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `17` | class is `QuestManager : MonoBehaviour, ISaveable, IQuestSystem` |
| `52` | `SavePriority => 7` |
| `54` | `LoadPriority => 7` |
| `77` | publishes through `GlobalRegistry.RegisterQuestRuntime(this)` |
| `78` | registers with `GlobalRegistry.Save?.Register(this)` |
| `314` | implements `PopulateSaveData(...)` |
| `341` | implements `LoadFromSaveData(...)` |

Quest runtime is currently both service owner and save participant.

## Bootstrap Wiring

The player domain is explicitly staged by `GameBootstrapper`, not left to incidental scene order.

`Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`

Confirmed facts:

| Source line | Fact |
|---|---|
| `197` | runs `InitializeCoreLayer` as an explicit bootstrap step |
| `200` | runs `InitializeEnvironmentLayer` as an explicit bootstrap step |
| `203` | runs `InitializePlayerLayer` as an explicit bootstrap step |
| `269` | player layer has its own dedicated init routine |
| `294` | ensures `InputDispatcher` runtime instance |
| `295` | ensures `PlayerRuntimeContextService` runtime instance |
| `296` | ensures `PlayerInventoryManager` runtime instance |
| `297` | ensures `PlayerSensoryManager` runtime instance |
| `298` | ensures `ContextualPhysicalIkRuntime` runtime instance |

This confirms the intended ownership split:

- authored player root carries raw gameplay components
- bootstrap-owned services expose stable registry surfaces
- later systems read via `GlobalRegistry` instead of scene search

## Current System Graph

Current player-facing dependency chain can be summarized as:

`Player.prefab components`
-> `PlayerRuntimeContextService`
-> `PlayerInventoryManager` and `PlayerSensoryManager`
-> `EquipmentInteractionHandler` / `ModularEquipmentEngine` / `SuitHUDV4CanvasOverlay`
-> save / quest / UI surfaces

That is cleaner than direct `Find*` coupling.
It is still not cheap to reason about because authority is distributed across multiple mirrors.

## What Looks Good

- Player root ownership is explicit on the prefab.
- Runtime access is centralized through `IPlayerRuntimeContext` instead of repeated scene search.
- Inventory and sensory mirrors are narrow and readable.
- Interaction runtime already uses a queue plus staged batched raycasts instead of ad-hoc per-tool physics calls.
- Modular equipment runtime uses native buffers and slot-index tables rather than dynamic collections in hot paths.

## What Looks Merely Acceptable

- The player domain is split across many managers, which improves local ownership but worsens overall discoverability.
- `PlayerInventoryManager` and `PlayerSensoryManager` are largely forwarding shells. This is not automatically wrong, but it means the real truth still lives one layer deeper in `PlayerRuntimeContextService`.
- Quest, PDA, HUD, tool runtime, and interaction authority are adjacent systems, not a single clearly indexed subgraph in older docs. That gap is the reason this file now exists.

## What Looks Weak

- There is still no single measured runtime truth showing that the whole player stack remains zero-GC under integrated play-mode traversal.
- `ModularEquipmentEngine` appears runtime-instanced but was not found in scanned player prefab or active scene assets. That is architecturally plausible, but it increases hidden bootstrap dependence.
- Split ownership between authored player root, runtime mirrors, and specialized authorities raises regression risk during future refactors.

## Failure Modes To Watch

- Player root changes on `Player.prefab` can silently desync from runtime mirror assumptions.
- Service mirrors can look healthy while underlying prefab references are broken.
- Bootstrap failure or ordering regression can leave narrow mirrors alive but unbound.
- Tool and interaction regressions can hide behind deferred queueing unless play-mode traversal is tested end-to-end.

## Regression Model

| Dimension | Impact |
|---|---|
| CPU | None. Documentation-only pass. |
| GC | None. Documentation-only pass. |
| Memory | None. Documentation-only pass. |
| Cadence | None. Runtime code unchanged. |
| Correctness | Improves codebase navigation and reduces false ownership assumptions in later audits. |

## Verdict

The current player/gameplay core is not chaotic, but it is layered and easy to misread without a map.

Current truth:

- authored player root owns the raw player components
- `PlayerRuntimeContextService` is the canonical read model
- `PlayerInventoryManager` and `PlayerSensoryManager` are registry-facing mirrors
- `EquipmentInteractionHandler` owns deferred interaction dispatch
- `ModularEquipmentEngine` owns compiled runtime tool state
- `QuestManager` is both gameplay authority and save participant

This is materially more structured than older documentation implied.
It still lacks integrated runtime proof.

STATUS: PENDING VERIFICATION
