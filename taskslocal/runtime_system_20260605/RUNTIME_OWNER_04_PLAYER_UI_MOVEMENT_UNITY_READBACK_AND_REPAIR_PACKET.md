# RUNTIME_OWNER_04_PLAYER_UI_MOVEMENT_UNITY_READBACK_AND_REPAIR_PACKET

Status: EXECUTION PACKET / PENDING VERIFICATION
Evidence class: STATIC_DOC + STATIC_REPORT_INPUT
Owner: future Unity/runtime owner
Packet writer constraint: no Unity, no build, no Play Mode, no import, no asset mutation, no scene save, no prefab save, no raw YAML mutation.

## Objective

Turn the current static active-player/HUD conflict into a safe no-mutation Unity readback, then execute an owner-correct repair sequence only after the active route is proven.

First-20 route blocker targeted for removal: ambiguous active player and HUD authority blocking `world load -> walk/swim/orient -> interact -> HUD warning/prompt -> PDA/pause/save -> load return`.

This packet does not prove runtime readiness. All runtime behavior remains `PENDING VERIFICATION` until Unity Console, Play Mode or player route, profiler/GC, save/load, and capture artifacts exist.

## Evidence Basis

- `Docs/Reports/RuntimeSystem_20260605/ACTIVE_PLAYER_SCENE_CONFLICT_MAP_20260605.md/.csv`
- `Docs/Reports/RuntimeSystem_20260605/SHELL_HUD_SCENE_BINDING_ESCALATION_20260605.md/.csv`
- `Docs/Reports/RuntimeSystem_20260605/PLAYER_UI_MOVEMENT_STATIC_ANCHOR_AUDIT_20260605.md/.csv`
- `taskslocal/runtime_system_20260605/RUNTIME_OWNER_02_SHELL_HUD_BLOCKER_REPAIR_PACKET.md`
- `taskslocal/runtime_system_20260605/README.md`

Static facts to preserve:

- `02_HECTON_WORLD.unity` statically contains an active scene-local `Player`, tagged `Player`, with enabled `HectonWorldShellController1428`.
- The scene-local shell binds `cameraRig` to the scene `Main Camera` transform.
- `Player.prefab` contains production-looking `HectonPlayerMovement`, `PlayerInteraction`, Rigidbody, prefab camera, visor, and HUD bindings, but its GUID was not found in the targeted scene scan.
- `HUD_Internal.prefab` contains disabled `SuitHUDScreenCompositor` and `forceScreenSpaceOverlay: 1`; it is latent until Unity readback proves active clone/binding status.
- Static evidence is not runtime proof.

## Authority Docs And Mandates

Read before execution:

- `AGENTS.md`
- `HECTON8_ORCHESTRATOR.md`
- `PROJECT_BIBLES.md`
- `TASTE.md`
- `player.md`
- `input.md`
- `ui.md`
- `UI_DIEGETIC_HUD_STANDARDS.md`
- `systems.md`
- `performance.md`
- `quality.md`
- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/ARCH_Signal_Lane_Segregation.txt`
- `.agents-skills/UI_Data_Streaming_ZeroGC_Optimization.txt`
- `.agents-skills/CTRL_Device_Abstraction_Haptics.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## Owned Scope

Future Unity owner may inspect runtime player, UI, input, camera, interaction, pause/PDA, save/load, dispatcher, registry, and telemetry routes. Mutation is allowed only after Phase 1 no-mutation readback proves the active owner route and Phase 2 names the approved Unity API/editor-script repair path.

Hard mutation boundaries:

- No raw `.unity`, `.prefab`, `.asset`, `.mat`, or project-settings YAML edits.
- No patching shell movement/input/camera logic to make it "less bad".
- If scene-local shell is active in production, remove or disable it only through approved production-player route and Unity API/editor script.
- No project setting, tag, layer, URP, material, package, or public API mutation without explicit dependency proof and approval.
- Do not mutate sibling-agent edits or revert unrelated work.

## Analysis Gate For Future Runtime Owner

[ANALYSIS]
Target: prove active player/HUD/input/camera ownership and repair only the proven blocker.
Affected systems: active `Player`, `BootstrapState`, production `Player.prefab`, shell controller, movement, motor, camera, interaction, HUD/visor, PDA, pause, save/load, input devices, dispatcher, GlobalRegistry, SignalBus lanes, black-box telemetry.
Zero GC proof: static scans are insufficient. Required runtime proof is 0 B/frame for input poll, movement/control hot paths, interaction probe, HUD text updates, PDA/pause navigation, camera visual sync, and telemetry write over the requested stress windows.
State check: verify no duplicate active player identity, no duplicate movement owner, no direct-input shell in production route, no interactive gameplay `ScreenSpaceOverlay` HUD unless approved bridge, no stale `BootstrapState.CurrentPlayerObject`, no save/load identity mismatch after route return.
Rule quote: one fact has one owner, one route, one proof artifact. `GlobalQualityWeight` scales cadence and presentation only; it cannot change player authority, HUD authority, action semantics, DTO layout, save identity, or route selection.

## Numbered Tasks

### Phase 0 - Process Gate And No-Mutation Readback Prerequisites

1. Check the process gate before opening Unity: active Unity/import/compiler/shader/package/dotnet/csc processes, CPU load, dirty worktree, active scene risk, and current proof folder. If CPU is over 50 percent or another build/import/compiler path is active, stop and report `BLOCKED BY PROCESS GATE`.

2. Read the authority docs and mandates listed in this packet. Record the route bibles and mandates followed in the proof packet. Do not bulk-read unrelated archives or old logs.

3. Reconfirm the static blocker evidence with targeted reads only: active scene-local `Player` with enabled shell, no targeted `Player.prefab` GUID hit in `02_HECTON_WORLD`, and `HUD_Internal.forceScreenSpaceOverlay: 1` latent overlay risk.

4. Prepare a no-mutation Unity readback plan. It must read active object paths, prefab sources, enabled components, dispatcher lanes, `BootstrapState.CurrentPlayerObject`, render modes, camera stack, input owner, save owner, and black-box ring status without scene/prefab save.

5. Prepare proof output outside `Assets`: manifest, Unity log copy, readback tables, profiler/GC artifacts, save/load artifact, black-box dump manifest, screenshots, and clip paths. No screenshot/log fluff goes under `Assets`.

6. Checkpoint 0: write the process/readback prerequisite verdict. Required labels: process gate `GREEN/BLOCKED`, mutation state `NONE`, static evidence `RECONFIRMED/PENDING`, readback plan `READY/BLOCKED`, and runtime status `PENDING VERIFICATION`.

### Phase 1 - Active Player, Bootstrap, Dispatcher, And Component Readback

7. Read active tagged `Player` objects in the loaded route without mutation. Record hierarchy path, scene, active state, prefab source, scene-local status, instance id, tag, layer, parent, and whether the object matches `BootstrapState.CurrentPlayerObject`.

8. Read `BootstrapState.CurrentPlayerObject` and current player runtime context. Record whether the current player points to the scene-local shell object, a production prefab instance, a bootstrap-spawned object, or null/stale state.

9. Read enabled components on the active player route: shell, production movement, motor, Rigidbody/capsule, camera rig, interaction, survival/context, HUD/visor references, save owner, PDA/pause links, input consumers, and telemetry owners.

10. Read dispatcher and phase registration for shell, `InputDispatcher`, `HectonPlayerMovement`, `HectonPlayerMotor`, `HectonPlayerCameraRig`, `PlayerInteraction`, HUD, PDA, pause, save/load, and telemetry. Required fields: phase, lane, enabled state, owner object, registration count, and duplicate/coexistence risk.

11. Read active input, camera, interaction, and HUD stack. Prove whether `InputDispatcher` is the only gameplay input polling boundary; whether shell direct input is active; which camera writes presentation; which HUD route is active; and whether `HUD_Internal`, `Suit_HUD_Canvas`, `SuitHUDV4CanvasOverlay`, `InteractionUI`, `VisorHUDController`, or `SuitHUDPresentationController` are enabled.

12. Checkpoint 1: produce `player_owner_phase_table.md` and `hud_mode_binding_readback.md`. Required verdicts: active player source, prefab source, bootstrap state, shell active/inactive, production movement active/inactive, input owner, camera owner, HUD owner, interaction owner, save owner, and all duplicate authority risks.

### Phase 2 - Owner-Correct Repair Sequence After Readback Only

13. Gate Phase 2. If Phase 1 did not prove the active owner route, mark implementation tasks `BLOCKED BY READBACK` and do not mutate anything. Discovery may invalidate all later repair tasks.

14. If the scene-local shell is inactive or non-production, leave it unchanged and document it as `STATIC RISK ONLY`. Do not delete or disable it without a separate cleanup proof path.

15. If the shell is active in production, classify it as `ACTIVE ROUTE BLOCKER`. Do not patch its direct input, transform writes, camera writes, speed values, or movement feel. Plan removal/disablement through Unity API/editor script after proving the production player route that will replace it.

16. Repair active player authority only through the approved route: production `Player.prefab` instance or bootstrap-spawned production player, `InputDispatcher` snapshot source, `HectonPlayerMovement` movement truth, `HectonPlayerMotor` body application, `HectonPlayerCameraRig` visual sync, `PlayerInteraction` interaction probe, and save/load owner. Use owner interfaces, cached GlobalRegistry dependency injection, DataVault snapshots, or typed SignalBus lanes; no concrete cross-domain shortcut.

17. Classify and repair HUD only after active stack readback. Reject any active interactive first-party gameplay HUD using `RenderMode.ScreenSpaceOverlay` unless it is explicitly approved as a noninteractive debug/loading/legal/accessibility/frontend bridge. If `HUD_Internal.forceScreenSpaceOverlay` is active for gameplay, reroute through diegetic/visor/world-space carrier using Unity API/editor script and read back the result.

18. Checkpoint 2: produce `repair_disposition.md`. Required verdicts: shell `inactive/static risk/active blocker/repaired/still blocker`, HUD overlay `inactive/noninteractive bridge/repaired/still blocker`, production route `proven/unproven`, raw YAML mutation `NO`, public API mutation `NO or approved`, and runtime status `PENDING VERIFICATION` unless proof exists.

### Phase 3 - Route Proof: Movement, UI, Input, Save, Telemetry, Captures

19. Prove walk and swim route operation: walk/shoreline movement, stop, turn, interaction lock, swim forward/strafe/ascend/descend, drag/inertia, surface/dive transition, depth/pressure response, camera readability, and no camera/UI script writing gameplay position.

20. Prove interaction and HUD route operation: target acquisition, prompt display, interact spam x20, oxygen/depth/pressure/tool/route/warning/source-owner readouts, stale/fault display behavior, HUD text updates, and prompt recovery after PDA/pause/menu close.

21. Prove PDA, pause, save/load, and input devices: PDA open/close x10, pause open/close x10, focus return, input lock, keyboard/mouse navigation, gamepad navigation, rebinding/conflict path if touched, save notification, save, load, restored player identity, restored position/state, and route return.

22. Prove performance and telemetry: 0 B/frame GC for input, movement, interaction probe, HUD text, PDA/pause navigation, camera visual sync, and telemetry write; profiler markers for each route; no direct `Input.GetKey`, `Input.GetAxis`, `Input.GetButton`, hot `Keyboard.current`, hot `Mouse.current`, hot `Gamepad.current`, `.text =`, `SetText(string)`, or runtime string formatting in active hot paths; 300-frame black-box rings and dump manifests for input, player kinematics, UI/HUD focus/state, and route error flags.

23. Capture compact and high route evidence: 720p, 16:9, 16:10, 21:9, and 4:3 screenshots; compact/low and high captures; short clip covering walk, swim, interact, HUD warning/prompt, PDA/pause, save/load return, and camera route readability. Captures must reject generic overlay HUD, unreadable text, route-hidden camera, clipped UI, and visual-floor downgrade.

24. Checkpoint 3: produce the final proof packet. Required labels: Unity Console, Play Mode/player route, profiler, GC, memory/VRAM if changed, save/load, black-box 300 frames, compact capture, high capture, input device matrix, rejection gates, files changed, and remaining `PENDING VERIFICATION`. Do not claim `READY`, `DONE`, `SOLVED`, or runtime readiness without current artifacts.

## Proof Packet Requirements

Write proof outside `Assets`, under the accepted runtime proof folder:

- `manifest.json`
- `manifest.sha256`
- copied Unity editor/player log
- `process_gate.md`
- `active_player_readback.md`
- `player_owner_phase_table.md`
- `bootstrap_state_readback.md`
- `dispatcher_registration_readback.md`
- `input_static_forbidden_api_scan.md`
- `hud_mode_binding_readback.md`
- `repair_disposition.md`
- `route_control_readback.md`
- `zero_gc_ui_input_movement_profiler.md`
- `save_load_return_proof.md`
- `black_box_dump_manifest.md`
- compact and high screenshots
- route proof clip

## Rejection Gates

- Process gate red but Unity/build/import/profiler was launched.
- Any raw scene, prefab, material, asset, or project-settings YAML mutation.
- Shell controller remains active in the production player route.
- Shell movement/input/camera logic is patched instead of removed/disabled through an approved owner route.
- Gameplay movement polls `Input.GetKey`, `Input.GetAxis`, `Input.GetButton`, hot `Keyboard.current`, hot `Mouse.current`, hot `Gamepad.current`, or runtime string action lookup.
- Production player and shell both claim movement, camera, input, interaction, or save identity authority.
- Active interactive first-party gameplay HUD uses `ScreenSpaceOverlay` without approved bridge classification.
- HUD presents fake telemetry or owns oxygen, depth, pressure, route, tool, interaction, survival, save, or movement truth.
- UI text allocates, clips at 720p, or requires mouse-only navigation for PDA/pause/save/load.
- Camera writes gameplay truth.
- `GlobalQualityWeight` changes player authority, HUD authority, action semantics, DTO layout, save identity, route selection, or black-box field identity.
- No 300-frame black-box route for critical input/player/UI state.
- Static scans are reported as runtime proof.

## GlobalQualityWeight Consequences

`GlobalQualityWeight` is continuous. Low/Middle/High/Ultra are review labels, not binary branches.

- Low around `0.0`: production player/HUD authority is unchanged. Compact keeps readable movement, swim, oxygen/depth/warning/prompt, input buffering, core HUD facts, and route camera. It may reduce optional diagnostics, motion, haptic layering, and panel material richness only.
- Middle around `0.35`: full owner chain must stay proven: input snapshot, movement truth, motor apply, camera visual sync, interaction, HUD, PDA/pause, save/load. Presentation adds restrained glass, warning cadence, and device hints without changing truth.
- High around `0.70`: saved budget buys smoother camera visual sync, richer visor material, stronger haptics, better prompt/warning degradation, denser route UI, and optional telemetry depth after compact proof exists.
- Ultra around `1.0`: visual overkill only in `VISUAL_SYNC`: visor contamination, layered screen artifacts, secondary tool/camera micro-motion, richer haptic texture, high-density diagnostics, and cinematic carrier detail around stable text.

Quality weight cannot change player/HUD authority, action ids, input semantics, DTO layout, save identity, interaction result, oxygen/depth truth, warning priority, route selection, or black-box field identity.

## Final Reporting Shape For Future Unity Owner

Use simple factual reporting:

```text
What was wrong:
- ...

What I did:
- ...

In-game result:
- UNITY-VERIFIED: ...
- PENDING VERIFICATION: ...

What was verified:
- Process gate:
- Active player:
- BootstrapState:
- Dispatcher:
- Input:
- Movement:
- Swim:
- Interaction:
- Camera:
- HUD:
- PDA/pause:
- Save/load:
- Profiler:
- GC:
- Black-box:
- Captures:

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
