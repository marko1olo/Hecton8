# RUNTIME_OWNER_01_PLAYER_UI_MOVEMENT_VERTICAL_SLICE_PACKET

Status: IMPLEMENTATION PACKET / PENDING VERIFICATION
Evidence class: STATIC_DOC + STATIC_SOURCE_ANCHORS
Owner: RUNTIME_OWNER_01_PLAYER_UI_MOVEMENT_PACKET_WRITER
Write scope: packet only. No source, prefab, scene, Unity, build, Play Mode, profiler, or runtime proof produced by this file.

## Scope

Create the playable baseline for the first-20 route: walking/interior or shoreline movement, swimming, ascend/descend, camera feel, interaction affordance, HUD/visor essentials, PDA/pause/rebinding boundary, zero-GC HUD updates, black-box telemetry, and proof.

First 20 Minutes moment: `Swim`, `Tool`, `Hazard`, `Pause/save`, and `Load return`.

Route impact: removes the current blocker where the product-facing route cannot be accepted until the active player, movement owner, HUD, interaction prompt, input owner, and camera graph are proven in Unity.

Parked work rejected: decorative UI chrome, fake screenshot-only HUD, shell/controller movement without production owner proof, mouse-only UI navigation, and any runtime readiness claim from static docs.

## Mandates Followed

- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/ARCH_Signal_Lane_Segregation.txt`
- `.agents-skills/CTRL_Device_Abstraction_Haptics.txt`
- `.agents-skills/UI_Data_Streaming_ZeroGC_Optimization.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`

Root bibles read: `player.md`, `ui.md`, `input.md`, `gameplay.md`, `systems.md`, `performance.md`, `VISION_LOCKS.md`, `TASTE.md`.

## Static Candidate Anchors

These are static anchors only. Treat every activation claim as `PENDING VERIFICATION` until Play Mode readback proves the active scene graph.

- `Assets/_Project/Prefabs/Player.prefab` - CANDIDATE production player prefab. Prior static report says it binds `PlayerInteraction`, `HectonPlayerMovement`, PDA, swim presentation, `VisorHUDController`, and `SuitHUDPresentationController`.
- `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab` - CANDIDATE suit HUD prefab. Prior static report says it binds `SuitHUDV4CanvasOverlay` and `Hecton8.Interaction.InteractionUI`.
- `Assets/_Project/Prefabs/HUD_Internal.prefab` - CANDIDATE internal HUD compositor. Static report says `forceScreenSpaceOverlay: 1`; this blocks diegetic UI acceptance until runtime readback or replacement.
- `Assets/_Project/Scripts/HectonPlayerMovement.cs` - CANDIDATE production movement authority; implements `IUpdatable`, `IFixedTickable`, `IColdTickable`, `ILateFrameTickable`, `IPlayerMovementContracts`, and exposes `CurrentAup`, `CurrentDepth`.
- `Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs` - CANDIDATE authoritative kinematic application layer; owns Rigidbody/capsule writes through fixed/post-fixed and late-frame routes.
- `Assets/_Project/Scripts/Gameplay/PlayerSwimMotor.cs` - CANDIDATE swim locomotion math helper.
- `Assets/_Project/Scripts/Gameplay/HectonPlayerCameraRig.cs` - CANDIDATE sole camera transform/FOV application owner in late-frame presentation.
- `Assets/_Project/Scripts/Gameplay/PlayerSwimPresentationController.cs` - CANDIDATE swim presentation owner.
- `Assets/_Project/Scripts/Interaction/PlayerInteraction.cs` - CANDIDATE interaction owner; static source shows `ITickable`, `IUpdatable`, `ILateFrameTickable`, cached camera, interaction mask, and prompt path.
- `Assets/_Project/Scripts/Interaction/InteractionUI.cs` - CANDIDATE preferred interaction prompt UI; static source shows `SetCharArray` and `CanvasGroup`.
- `Assets/_Project/Scripts/UI/InteractionUI.cs` - CANDIDATE legacy/suspect duplicate. Must prove inactive or justify route.
- `Assets/_Project/Scripts/Core/PlayerRuntimeContextService.cs` - CANDIDATE bootstrap-owned player runtime context publisher; exposes movement, survival, look, interaction snapshots.
- `Assets/_Project/Scripts/Core/PlayerRuntimeContext.cs` - CANDIDATE DTO owner for `PlayerMovementRuntimeState`, `PlayerSurvivalRuntimeState`, `PlayerLookState`, and related snapshots.
- `Assets/_Project/Scripts/Core/PlayerInputState.cs` - CANDIDATE input snapshot DTO with action bitmask and explicit layout.
- `Assets/_Project/Scripts/Core/InputDispatcher.cs` - CANDIDATE input service owner.
- `Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs` - CANDIDATE haptic synthesis partial for input dispatcher.
- `Assets/_Project/Scripts/Core/Signals/PlayerMovementPresentationSignals.cs` - CANDIDATE typed presentation signals for movement, splash, exhale, sprint, pressure, and transport.
- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs` - CANDIDATE main suit HUD; static source shows `GlobalQualityWeight`, `CanvasGroup`, `SetCharArray`, render-mode paths.
- `Assets/_Project/Scripts/UI/SurvivalHUDController.cs` - CANDIDATE survival HUD presentation.
- `Assets/_Project/Scripts/UI/TMP_TextRegistry.cs`, `TmpTextNoAlloc.cs`, `CharBufferPool.cs` - CANDIDATE zero-GC text utility path.
- `Assets/_Project/Scripts/UI/DiegeticPDAController.cs`, `PDAControlsRebindUI.cs`, `PauseMenuController.cs`, `PauseMenuHost.cs` - CANDIDATE PDA, rebinding, and pause/menu surfaces.
- `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs` - CANDIDATE first-hour route read model; route authority remains unproven.
- `Assets/_Project/Scripts/World/HectonWorldShellController1428.cs` - CANDIDATE blocker. Prior static report says scene-authored tagged `Player` shell can win over production player and uses direct input paths. Runtime owner must prove inactive or replace safely.

## Non-Negotiable Acceptance Rules

- Status remains `PENDING VERIFICATION` until fresh Unity proof exists.
- Static grep, source review, prefab YAML, reports, and this packet are not runtime proof.
- Do not claim player movement, HUD, interaction, input, camera, or first-20 readiness without: Unity Console, Play Mode/player run, Profiler/GC, screenshot/clip, and save/load evidence.
- Do not launch `dotnet build` if Unity/import/compiler/shader processes are active or CPU is over 50 percent.
- Do not raw-edit `.unity`, `.prefab`, or `.asset` YAML unless the task explicitly approves it and Unity API mutation is impossible.
- Do not change public interfaces in `Hecton8.Core.Contracts` without dependency proof and explicit approval.
- Do not add a new global registry slot or signal lane unless owner-local/interface routes are proven insufficient and the route card can get `GREEN`.
- Runtime hot paths: 0 B GC, no `Update/LateUpdate/FixedUpdate` gameplay truth except allowed presentation exceptions, no scene search, no hot `GlobalRegistry`, no runtime string formatting, no hot material clones.

## Implementation Tasks

1. Run a no-mutation process gate. Record active Unity, compiler, shader, dotnet, and CPU state. If gate is red, do static-only work.
2. Read active scene/player binding in Unity with no mutation: active tagged `Player`, `BootstrapState.CurrentPlayerObject`, prefab source, enabled components, and whether production `Player.prefab` or `HectonWorldShellController1428` owns the route.
3. If production player is not active, write a narrow Unity-owner plan to route bootstrap to `Player.prefab`; do not patch scene blindly. If shell is active, classify it as temporary shell or blocker.
4. Verify `PlayerRuntimeContextService` binding flags for movement, camera, survival, tool manager, inventory, PDA, visor, flashlight, hand anchor, collider, and HUD notification.
5. Verify dispatcher registration for `HectonPlayerMovement`, `HectonPlayerMotor`, `HectonPlayerCameraRig`, `PlayerInteraction`, `SuitHUDV4CanvasOverlay`, PDA, pause, and rebinding owners.
6. Checkpoint A: produce `player_ui_binding_readback.md` with active owner table and `PASS/FAIL/PENDING` per owner. No gameplay claim yet.

7. Audit input ownership. Prove `InputDispatcher` is the only gameplay input polling boundary for movement, interact, primary/secondary tool, ascend/descend, sprint/boost, PDA, inventory, pause, cancel, tab next/previous, and flashlight.
8. Remove or isolate direct-input shell paths only after Step 2 proves they are active blockers. Do not touch if inactive.
9. Verify `PlayerInputState` action bit layout and buffered action window. Add missing action ids only by interface expansion, not mutation, if existing ids cannot cover required verbs.
10. Implement or wire rebinding through `PDAControlsRebindUI`, `InputBindingContracts`, `ControlRemapper`, and `RebindingManager` candidates. Rebinding is menu-safe only; hot paths read cached action ids/bitmasks.
11. Add device matrix proof path: keyboard/mouse, gamepad, Steam Deck if claimed. Do not claim Steam Deck without device/profile proof.
12. Checkpoint B: static scan for forbidden input APIs in active runtime path: `Input.GetKey`, `Input.GetAxis`, `Input.GetButton`, hot `Gamepad.current`, hot string action lookup, gameplay lambda callbacks.

13. Prove walking/interior or shoreline locomotion. `HectonPlayerMovement` owns body state and AUP; `HectonPlayerMotor` owns Rigidbody/capsule writes. No UI/camera/tool script may own movement truth.
14. Prove swimming baseline: forward/back/strafe, ascend/descend, drag/inertia, surface/dive transition, depth readout, return-path movement, and current/slowdown response. `PlayerSwimMotor` is math helper only, not route proof.
15. Prove camera feel: `HectonPlayerCameraRig` applies transform/FOV in late-frame visual sync from finalized movement state; camera must not write gameplay truth.
16. Prove interaction affordance: `PlayerInteraction` target probe uses bounded layer/probe cadence and `InteractionUI` displays prompt through `SetCharArray`. Interaction must continue probe refresh after menu close.
17. Prove tool/use boundary: one first-route useful action must work through existing tool/interaction route: scan, repair, cut, weld, drill, or harvest. If no tool route is active, mark `BLOCKED_TOOL_ROUTE`.
18. Checkpoint C: run Play Mode movement route from safe anchor to water and back. Capture input device used, route distance, depth range, active owner, and any comfort/control failure.

19. Wire HUD essentials through production HUD only: oxygen, depth, pressure/stress or integrity, energy/power, tool state, interaction prompt, save notification, route/compass/signal cue, warning/failure state.
20. Reject `ScreenSpaceOverlay` gameplay HUD unless it is a documented temporary bridge. Preferred path is diegetic/world-space/visor projection. `HUD_Internal.forceScreenSpaceOverlay` remains blocker until readback proves non-production or is replaced safely.
21. Verify HUD text update route: `TMP_Text.SetCharArray`, `TmpTextNoAlloc`, `CharBufferPool`, or fixed buffers. No `TMP_Text.text`, interpolation, `ToString()`, `SetText(string)`, or runtime hierarchy hashing in hot update path.
22. Verify UI visibility route: `CanvasGroup.alpha`, `interactable`, `blocksRaycasts`; no hot `SetActive` toggling for active HUD/menu state.
23. Verify PDA/pause focus boundary: gameplay input lock, UI navigation action layer, default focus on open, cancel/close, save/load entry, rebinding entry, and return to gameplay without stale interaction locks.
24. Checkpoint D: 720p, 16:9, 16:10, 21:9, and 4:3 UI screenshots. Capture normal, warning, disabled, menu-open, and rebinding states. Text clipping or unreadable instrument hierarchy is rejection.

25. Add or verify black-box telemetry for player critical state: last 300 frames of AUP/world position, velocity, movement mode, depth, underwater flag, oxygen, pressure/stress, input bitmask, active tool, interaction target hash, UI focus state, error flags, and owner frame id.
26. Add or verify dump route on NaN/error. If no explicit agent ID is active at runtime, dump as `Docs/AgentLogs/Dump_PLAYER_UI_MOVEMENT_{timestamp}.bin` plus manifest. Do not allocate during hot telemetry writes.
27. Add profiler markers or verify existing markers for input poll, movement simulation, motor apply, camera visual sync, interaction probe, HUD text flush, PDA/pause update, haptic dispatch, and telemetry write.
28. Run 300-frame and 60-second GC validation after code changes: movement, swim, interaction prompt spam x20, PDA open/close x10, pause/rebind navigation, save notification, despawn/disable where relevant.
29. Run save/load proof: save from route, quit/reload, restore position/AUP, movement state, inventory/tool state, scanned/opened flags where route uses them, HUD state, and hazard state.
30. Checkpoint E: final proof packet. If any proof class is missing, final status is `PENDING VERIFICATION`, not partial acceptance.

## Proof Packet Required

Create proof under `Docs/Screenshots/HectonProofPackets/h8_1475_{session}/` or the current accepted proof-packet path if superseded. Do not write screenshots/log fluff under `Assets`.

Required files:

- `manifest.json`
- `manifest.sha256`
- copied Unity editor/player log
- `player_ui_binding_readback.md`
- `input_device_matrix.md`
- `gc_validation.md`
- `profiler_summary.md`
- `save_load_diff.md`
- `black_box_dump_manifest.md`
- screenshots:
  - `01_surface_coast_aegir_ui_on.png`
  - `02_underwater_0_5m_hud_prompt.png`
  - `03_underwater_20_50m_route_depth_oxygen.png`
  - `04_interaction_target_prompt.png`
  - `05_pda_controls_rebind.png`
  - `06_pause_save_state.png`
  - `07_low_quality_readability.png`
- clip: short route run showing movement, swim, interaction, HUD, PDA/pause, and return path.

Unity proof requirements:

- Console has no route-blocking errors after import and Play Mode.
- Play Mode or player run proves route operation.
- Profiler capture covers at least 60 seconds on the selected route.
- GCMonitor or Profiler GC column proves 0 B hot-path allocation for input, movement, interaction, HUD text, PDA/pause navigation, and camera visual sync.
- Memory/VRAM snapshot exists at world load and after save/load.
- Save directory diff proves `.tmp -> .sav` behavior or current persistence route, plus restored-state notes.
- Screenshot/clip shows actual first-route gameplay, not editor-only widgets.

## Process And Build Gate

- Before build: sample CPU and active processes. Do not build when CPU > 50 percent or any `dotnet`, `csc.exe`, Unity compiler/import/shader process is active.
- Before Unity mutation: check dirty worktree and active scene risk. Do not overwrite other agents' edits.
- If compile breaks after runtime edits: keep fixing manually. If the same dependency wall repeats three times and is caused by another owner, revert only the chunk just written and report `BLOCKED BY DEPENDENCY`.
- Use Unity API for prefab/scene mutation. Avoid raw YAML.
- Do not add packages.
- Do not change project settings, quality assets, tags, layers, or URP configuration unless explicitly assigned.

## GlobalQualityWeight Consequences

Use continuous `GlobalQualityWeight` only. Low/Middle/High/Ultra are review labels, not branches.

- Low around `0.0`: production truth unchanged. Movement keeps readable control, oxygen/depth warnings, interaction prompts, stable camera, basic haptics, static/low-motion HUD, bounded prompt cadence, and no ugly UI mode.
- Middle around `0.35`: expected player lane. Movement, swim, HUD, PDA, interaction, and route cues must feel complete; presentation adds richer glass/instrument detail and readable route warnings without extra truth.
- High around `0.70`: saved budget buys smoother camera response, richer haptic layers, stronger HUD degradation, denser prompt/compass/signal presentation, longer LOD residency, and better swim/contact presentation.
- Ultra around `1.0`: visual overkill only in `VISUAL_SYNC`: visor contamination, richer screen material, secondary camera/tool micro-motion, dense diagnostics, refined haptics, and richer warning presentation. Gameplay truth, save identity, DTO layout, action ids, and ownership stay unchanged.

Continuous scaling rules:

- Scale cadence, smoothing, presentation density, haptic layering, optional diagnostics, and UI material detail.
- Do not scale oxygen math, action semantics, movement authority, save layout, interaction results, resource truth, or route ownership.
- Add hysteresis for state/cadence changes: 2-3 seconds for UI/presentation cadence, 3-5 meters for distance-related presentation.

## Rejection Gates

- Active route uses `HectonWorldShellController1428` for production movement without accepted owner route.
- `Player.prefab` and `Suit_HUD_Canvas.prefab` are not active or not bound through `PlayerRuntimeContextService`, unless a newer production owner is proven.
- HUD is generic screen-space chrome with fake telemetry.
- Interaction prompt allocates or reads action names through runtime strings.
- Gameplay movement polls Unity input directly.
- Camera writes movement or gameplay truth.
- PDA/pause/rebinding is mouse-only.
- Text clips or fails 720p readability.
- Hot path allocates > 0 B.
- No black-box ring for critical movement/input/UI state.
- Save/load does not restore player route state.
- Any claim says `READY`, `PASS`, `DONE`, or `SOLVED` without the proof packet.

## Final Reporting Template For Future Runtime Agent

Use this exact structure:

```text
What was wrong:
- ...

What I did:
- ...

In-game result:
- UNITY-VERIFIED: ...
- PENDING VERIFICATION: ...

What was verified:
- Unity Console:
- Play Mode/player route:
- Profiler:
- GC:
- Memory/VRAM:
- Save/load:
- Screenshots/clip:

Regression model:
- CPU:
- GC:
- Memory:
- Cadence:
- Correctness:

Low/Middle/High/Ultra consequences:
- Low:
- Middle:
- High:
- Ultra:
```
