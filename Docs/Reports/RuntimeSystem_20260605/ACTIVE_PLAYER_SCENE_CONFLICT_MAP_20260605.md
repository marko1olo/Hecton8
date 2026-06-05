# Active Player Scene Conflict Map - 2026-06-05

Status: STATIC_CONFLICT_MAP / RUNTIME_PROOF_PENDING
Evidence class: STATIC_SCENE_YAML_SCAN / STATIC_PREFAB_YAML_SCAN / STATIC_SOURCE_SCAN

Mandates followed: ARCH_Project_Bootstrap_Sequence_Init_Safety; ARCH_Execution_Phases; ARCH_Global_Registry_ServiceLocator_DI_Init; UI_Diegetic_Physical_Interfaces; UI_Data_Streaming_ZeroGC_Optimization; QA_Evidence_Text_Filter_Audit.

Authority docs read: `player.md`, `ui.md`, `bootstrap.md`, `systems.md`, `quality.md`.

No Unity run, Play Mode, player build, profiler, GCMonitor, screenshot, scene save, prefab edit, source edit, or YAML mutation was performed.

## What Was Wrong

The scene has a scene-local active tagged `Player` object with enabled `HectonWorldShellController1428`. The production `Player.prefab` exists and has `HectonPlayerMovement`, but the production prefab GUID is not serialized into `02_HECTON_WORLD.unity` by the static search performed here. Static evidence therefore says the route is conflicted: production player code exists, but the active scene binding points at a shell.

`HUD_Internal.prefab` is not proven active in the scene, but it contains a disabled `SuitHUDScreenCompositor` with `forceScreenSpaceOverlay: 1`. That is a latent HUD blocker because if any runtime route enables or clones it as gameplay HUD, it violates the diegetic/visor UI acceptance path.

## Static Conflict Map

| Conflict | Static evidence | Severity | Runtime claim allowed |
|---|---|---|---|
| Active scene shell player | `02_HECTON_WORLD.unity` lines 70227-70232: active tagged `Player`; lines 70248-70260: enabled `HectonWorldShellController1428`; scene object/component `m_PrefabAsset: {fileID: 0}`. | ACTIVE_SCENE_BINDING_STATIC | none |
| Shell movement/input authority | `HectonWorldShellController1428.cs` line 10 implements `IUpdatable`; line 47 `Tick`; lines 57/66/68 write transform; lines 115/153 use `Keyboard.current`/`Mouse.current`; lines 132-165 use legacy `Input.*` fallbacks. | ACTIVE_ROUTE_RISK_STATIC | none |
| Production player candidate not scene-bound by GUID | `Player.prefab.meta` GUID `1c4db7a430141e5408e01b6ce4ed19d7`; static scene search found no GUID hit in `02_HECTON_WORLD.unity`. `Player.prefab` lines 915-916 are active tagged `Player`; line 1019 has enabled `HectonPlayerMovement`. | CANDIDATE_PRODUCTION_ROUTE_STATIC | none |
| Suit HUD canvas candidate not scene-bound by GUID | `Suit_HUD_Canvas.prefab.meta` GUID `e286dd44e529d8b4498750dd0abbbfd8`; static scene search found no GUID hit in `02_HECTON_WORLD.unity`. Prefab line 2416 has `Hecton8.UI.SuitHUDV4CanvasOverlay`. | CANDIDATE_HUD_ROUTE_STATIC | none |
| HUD internal latent overlay | `HUD_Internal.prefab.meta` GUID `949b94e6d99fdd44ea13e320d0784005`; static scene search found no GUID hit in `02_HECTON_WORLD.unity`. Prefab line 42 has compositor disabled, line 53 has `forceScreenSpaceOverlay: 1`. | LATENT_PREFAB_BLOCKER_STATIC | none |
| Compositor overlay path if enabled | `SuitHUDScreenCompositor.cs` line 14 implements `ILateFrameTickable`; lines 288-291 force `RenderMode.ScreenSpaceOverlay`; line 329 can create `HUD_RT_Compositor` overlay object; lines 434-436 keep overlay noninteractive by `CanvasGroup`. | CONDITIONAL_HUD_BLOCKER_STATIC | none |

## Required Unity Readback

Next runtime/Unity owner must run no-mutation readback only after the process gate is clean:

1. Active tagged `Player` object path, fileID, prefab source, and enabled state.
2. Whether `BootstrapState.CurrentPlayerObject` points to the scene-local shell or production prefab route.
3. Enabled components on active player, including `HectonWorldShellController1428`, `HectonPlayerMovement`, interaction, input, camera, HUD, PDA, pause, and survival routes.
4. Dispatcher lane registration for shell and production movement/camera owners.
5. Whether `Player.prefab`, `Suit_HUD_Canvas.prefab`, or `HUD_Internal.prefab` are instantiated, cloned, or absent at runtime.
6. Active HUD canvas render modes, compositor enabled state, interactivity, visor/world-space carrier, and text update route.
7. Console, profiler/GC, and screenshot proof before any movement/HUD acceptance claim.

## Rejection Gates

- Reject movement/HUD acceptance if the active route uses scene-local shell movement/camera/input as production authority.
- Reject patching input inside `HectonWorldShellController1428`; if active, it must be classified as temporary shell, replaced, or disabled through a Unity-safe owner pass.
- Reject gameplay HUD acceptance if any active interactive HUD is forced to `ScreenSpaceOverlay` without an approved temporary bridge classification.
- Reject raw `.unity` or `.prefab` YAML edits. Scene/prefab changes must go through Unity API after clean gate and readback.
- Reject `0 B/frame`, profiler, screenshot, save/load, or runtime owner claims from this static map.

## Regression Model

CPU: static scan only. If active, the shell route performs direct input polling, transform writes, and camera sync in `Tick`, creating duplicate movement/camera authority risk.

GC: static scan only. No GCMonitor or Profiler data exists.

Memory: static scan only. HUD overlay/compositor texture route remains pending runtime readback.

Cadence: shell uses dispatcher `Environment` lane while production movement route is a separate candidate. Active phase order is unproven.

Correctness: first-20 movement/HUD proof is blocked until the active scene player, input owner, movement owner, camera owner, and HUD owner are read back in Unity.

## Low / Middle / High / Ultra Consequence

Low/Compact: active shell must not own production movement; HUD must remain readable without overlay-only gameplay shortcuts.

Middle: production input, movement, camera, interaction, and HUD ownership must be one route with dispatcher phase proof.

High: richer visor/camera/haptic presentation is allowed only after authority conflict is removed or proven non-production.

Ultra: visual-sync density only. Quality weight must not change player authority, action IDs, save identity, DTO layout, or HUD fact ownership.

Final status: PENDING VERIFICATION.
