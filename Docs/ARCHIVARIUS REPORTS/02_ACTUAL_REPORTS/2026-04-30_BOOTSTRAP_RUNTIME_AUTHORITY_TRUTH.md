# BOOTSTRAP RUNTIME AUTHORITY TRUTH

Date: 2026-04-30
Status: PENDING VERIFICATION
Scope: current source-backed truth for bootstrap ownership, init authority, and scene-start handoff
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `STRM_Persistent_Object_Registry.txt`

## Purpose

The active docset already had bootstrap context, but not one current-source document that says the uncomfortable part directly:

HECTON-8 does not have one bootstrap owner.

It has a split bootstrap surface across three different runtime authorities:

- `BootstrapController`
- `GameBootstrapper`
- `SceneBootstrap`

This file exists to state that split honestly and describe what each class actually owns now.

## Proof Boundary

This report is based on current first-party source under `Assets/_Project/Scripts`.

It proves:

- declared bootstrap owners
- current init sequencing responsibilities
- current registry fallback behavior
- current scene-start handoff split

It does not prove:

- that every branch was exercised in live play mode
- that all bootstrap paths are bug-free
- that no duplicate bootstrap owner can arise in a broken scene setup
- that the current split is architecturally desirable

## Executive Verdict

Current bootstrap authority is split.

### What is true now

- `BootstrapController` owns early `00_BOOTSTRAP` scene guard and legacy global-system shell handoff.
- `GameBootstrapper` owns ordered `GlobalRegistry`-facing service initialization and entry-vector recovery.
- `SceneBootstrap` owns async world startup, player activation timing, and game-ready/bootstrap-failed event dispatch for gameplay scenes.

### What is false now

- "Bootstrap is fully centralized in one class."
- "GameBootstrapper owns the whole startup path."
- "SceneBootstrap is the only runtime startup authority."
- "UI layer is initialized by `GameBootstrapper`."

The last claim is directly contradicted by current code: `InitializeUILayer()` is still empty while scene-authored UI ownership remains elsewhere.

## 1. `BootstrapController` Authority

### 1.1 What It Owns

`BootstrapController` is still the earliest bootstrap shell owner in `00_BOOTSTRAP`.

Evidence:

- class declaration and execution order: `Assets/_Project/Scripts/Bootstrap/BootstrapController.cs:48`
- scene guard in `Awake()`: `Assets/_Project/Scripts/Bootstrap/BootstrapController.cs:105-113`
- singleton guard: `Assets/_Project/Scripts/Bootstrap/BootstrapController.cs:116-124`
- `DontDestroyOnLoad` shell persistence: `Assets/_Project/Scripts/Bootstrap/BootstrapController.cs:131`
- automatic handoff to `01_MAIN_MENU`: `Assets/_Project/Scripts/Bootstrap/BootstrapController.cs:210-218`

### 1.2 What It Still Initializes

The class still frames itself as the owner that ensures:

- build-settings validity
- system dispatcher
- tick/save/input/pool globals
- bootstrap audio listener
- scene handoff to main menu

It is not a dead historical leftover.

### 1.3 Why It Matters

This class means the repo still carries a bootstrap shell that predates or sits parallel to later registry-oriented runtime architecture.

That is important because any doc claiming "bootstrap is now entirely `GameBootstrapper`" is incomplete.

## 2. `GameBootstrapper` Authority

### 2.1 What It Owns

`GameBootstrapper` is the deterministic service-initialization owner for the `GlobalRegistry` shell.

Evidence:

- class declaration and execution order: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:23-28`
- ordered bootstrap steps: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:197-206`
- core layer init: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:238-251`
- environment layer init: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:254-267`
- player layer init: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:269-304`
- UI layer reality: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:306-309`

### 2.2 What It Explicitly Initializes

Current layers and owners:

| Layer | Current concrete owners initialized by `GameBootstrapper` |
|---|---|
| Core | `SystemDispatcher`, `GameTickManager`, `SaveManager`, `ObjectPoolManager`, `RenderDispatcher`, `SceneRuntimeService`, `EquipmentInteractionHandler` |
| Environment | `GlobalPhysicsStateManager`, `PhysicsApplySystem`, `DebrisManager`, `EnvironmentRuntimeContextService`, `OceanKinematicsRuntimeService`, `SpatialAudioManager` |
| Player | `InputDispatcher`, `PlayerRuntimeContextService`, `PlayerInventoryManager`, `PlayerSensoryManager`, `ContextualPhysicalIkRuntime` |
| UI | no direct registry adapter initialization yet |

This is the current code truth, not theory.

### 2.3 The UI Layer Is Still Empty

`InitializeUILayer()` currently contains only comments:

- no direct `IUIService` bootstrap init
- no direct menu/HUD owner init
- explicit admission that existing UI ownership remains on scene-authored controllers

That makes old or broad bootstrap docs easy to overstate if they describe fully layered service parity.

Current truth:

- core/environment/player are bootstrap-initialized
- UI is still scene-authored and later-bound

### 2.4 Entry Recovery Is Here, Not In `SceneBootstrap`

`GameBootstrapper` also owns entry-vector recovery behavior:

- validates active scene against `00_BOOTSTRAP`
- raises BIOS-style overlay on bad entry
- can forcibly `LoadScene("00_BOOTSTRAP")`

Evidence:

- `TryRecoverEntryVector(...)`: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:558-585`

This makes it more than a simple service initializer.
It also acts as a scene-entry guardrail.

## 3. `GameBootstrapper` Extended Registry Fallbacks

One of the most important current-source truths is the fallback registry coverage block.

After the main bootstrap phases, `GameBootstrapper` runs:

- `TryEnsureThermodynamicsRegistryCoverage()`
- `TryEnsureLogisticsRegistryCoverage()`
- `TryEnsureWorldGenRegistryCoverage()`
- `TryEnsureEncounterDirectorRegistryCoverage()`
- `TryEnsureQuestRegistryCoverage()`

Evidence:

- coverage entry point: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:587-593`
- thermodynamics fallback: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:595-603`
- logistics fallback: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:605-613`
- worldgen fallback: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:615-623`
- encounter fallback: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:625-633`
- quest fallback: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:635-643`

Current interpretation:

- bootstrap does not trust those service slots to be filled deterministically through one pure path
- it performs post-pass recovery with `FindAnyObjectByType`
- this is pragmatic coverage repair, not clean single-path DI purity

That is a real architecture signal.

## 4. `SceneBootstrap` Authority

### 4.1 What It Owns

`SceneBootstrap` is not the same layer as `GameBootstrapper`.

It owns:

- async gameplay-scene initialization flow
- player spawn/activation timing
- world-ready and ground-ready waiting
- game-ready / bootstrap-failed event publication
- runtime bootstrap state such as current player object and transform

Evidence:

- class declaration and execution order: `Assets/_Project/Scripts/SceneBootstrap.cs:82`
- static readiness/public state: `Assets/_Project/Scripts/SceneBootstrap.cs:120-143`
- queue-backed listener registry: `Assets/_Project/Scripts/SceneBootstrap.cs:85-118`, `145-178`

### 4.2 It Uses Its Own Queue-Backed Event Lane

`SceneBootstrap` is not just a MonoBehaviour with callbacks.

It maintains:

- `RegistryBucket<ISceneBootstrapEventListener>`
- `NativeQueue<SceneBootstrapEventPayload>`
- explicit `Register`, `Unregister`, and `FlushPendingEvents`

This means bootstrap readiness for scene consumers is already on a separate queue-backed event model.

That directly affects:

- HUD readiness
- music/tutorial unlock timing
- any listener that should wait for final player/world readiness

### 4.3 Why It Is Not Redundant

`SceneBootstrap` handles a different phase from `BootstrapController` and `GameBootstrapper`.

Current phase split:

- `BootstrapController` -> bootstrap-scene shell and main-menu handoff
- `GameBootstrapper` -> registry-facing runtime service shell and entry recovery
- `SceneBootstrap` -> gameplay-scene world and player startup

So treating `SceneBootstrap` as redundant is false.

## 5. Current Sequence Model

The current source-backed handoff model is best read like this:

1. `BootstrapController` guards and initializes the `00_BOOTSTRAP` shell.
2. `BootstrapController.Start()` routes to `01_MAIN_MENU`.
3. `GameBootstrapper` owns ordered service-layer registration and bad-entry recovery for the bootstrap shell.
4. gameplay scene startup later relies on `SceneBootstrap` for world-ready and player-ready handoff.
5. consumers such as `SuitHUDV4CanvasOverlay` listen to `SceneBootstrap` events instead of relying only on raw scene load timing.

This is not a small distinction.
It means startup responsibility is phased, not unified.

## 6. Current Contradictions And Debt

### 6.1 Split Authority Is Real

Current bootstrap surface is split across:

- one legacy/scene shell bootstrapper
- one registry bootstrapper
- one gameplay-scene startup coordinator

That is workable.
It is not simple.

### 6.2 `Build Dependency Graph` Is Partially Stale

The older `BUILD_DEPENDENCY_GRAPH.md` correctly captured much of `GameBootstrapper`, but it is now too flat if read as the whole bootstrap truth:

- it does not stand alone as the full bootstrap authority map
- it under-describes the three-owner split
- its "empty UI layer" note is true for `GameBootstrapper` but not sufficient to describe real scene-authored UI startup

### 6.3 Registry Fallbacks Show Incomplete Determinism

`FindAnyObjectByType` fallback registration for thermodynamics/logistics/worldgen/encounter/quest shows that bootstrap still needs recovery branches for services outside the stricter core shell.

That means:

- initialization is not fully authored in one uniform explicit graph
- some services are still discovered late and registered opportunistically

### 6.4 Input Registration Has Multiple Entry Paths

`InputDispatcher` registers into `GlobalRegistry` during `InitializeService()` and again in `OnEnable()` when initialized.

Evidence:

- `Assets/_Project/Scripts/Core/InputDispatcher.cs:106-128`
- `Assets/_Project/Scripts/Core/InputDispatcher.cs:137-146`

This is not proven broken here.
It is a real authority-path duplication worth keeping visible in docs.

## 7. Current Truths That Should Replace Older Simplifications

| Simplified claim | Current source-backed truth |
|---|---|
| Bootstrap is owned by one class | False. It is split across `BootstrapController`, `GameBootstrapper`, and `SceneBootstrap`. |
| `GameBootstrapper` fully owns UI startup | False. Its UI layer is empty; scene-authored UI remains elsewhere. |
| Registry initialization is fully deterministic everywhere | False. several service slots still have fallback discovery coverage. |
| `SceneBootstrap` is just a helper | False. It owns gameplay-scene startup and queue-backed readiness signaling. |

## 8. Recommended Read Order

For bootstrap/init-order work:

1. `2026-04-30_BOOTSTRAP_RUNTIME_AUTHORITY_TRUTH.md`
2. `BUILD_DEPENDENCY_GRAPH.md`
3. `2026-04-29_GLOBALREGISTRY_RUNTIME_AUTHORITY_MATRIX.md`
4. `2026-04-29_SCENE_PREFAB_SERVICE_OWNER_TRUTH.md`

For UI startup work:

1. `2026-04-30_BOOTSTRAP_RUNTIME_AUTHORITY_TRUTH.md`
2. `UI_AUDIO_PRESENTATION_SYSTEM_MAP.md`
3. `EVENT_FLOW_MAP.md`

## Regression Model

| Dimension | Impact |
|---|---|
| CPU | None. Documentation-only pass. |
| GC | None. Documentation-only pass. |
| Memory | None. Documentation-only pass. |
| Cadence | None. Runtime code unchanged. |
| Correctness | Improves bootstrap truth by replacing single-owner simplification with current phased authority model. |

## Verdict

The honest bootstrap model in current source is phased and split.

`BootstrapController`, `GameBootstrapper`, and `SceneBootstrap` each still own a different part of startup.

That split is the current truth layer.
Any documentation that collapses it into one owner is under-reporting the actual architecture.

STATUS: PENDING VERIFICATION
