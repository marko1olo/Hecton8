# RUNTIME_OWNER_02_SHELL_HUD_BLOCKER_REPAIR_PACKET

Status: EXECUTION PACKET / PENDING VERIFICATION
Evidence class: STATIC_DOC + STATIC_AUDIT_INPUT
Owner: RUNTIME_OWNER_02_SHELL_HUD_BLOCKER_PACKET_WRITER
Write scope: packet only. No source, prefab, scene, Unity, build, Play Mode, profiler, screenshot, or runtime proof produced by this file.

## Scope

Repair packet for the two static runtime blockers found in `Docs/Reports/RuntimeSystem_20260605/PLAYER_UI_MOVEMENT_STATIC_ANCHOR_AUDIT_20260605.md`:

- `Assets/_Project/Scripts/World/HectonWorldShellController1428.cs` shell/controller risk.
- `Assets/_Project/Prefabs/HUD_Internal.prefab` `forceScreenSpaceOverlay: 1` HUD mode risk.

First-20 route moment improved: `world load -> swim/orient -> interact -> hazard warning -> pause/save -> load return`.

Route impact: removes false player/HUD authority from the proof lane before runtime owners claim movement, swim, interaction, HUD, input, camera, or save/load readiness.

Do not claim code is fixed. Do not claim runtime readiness. This packet exists to tell the next Unity/runtime owner exactly what to prove, isolate, or repair.

## Mandates Followed

- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/ARCH_Signal_Lane_Segregation.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/UI_Data_Streaming_ZeroGC_Optimization.txt`
- `.agents-skills/UI_Diegetic_Physical_Interfaces.txt`
- `.agents-skills/PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `.agents-skills/CORE_Global_State_Reset_NonReload_Transitions.txt`

Root bibles sampled: `player.md`, `input.md`, `ui.md`, `systems.md`, `performance.md`.

## Analysis Gate For Future Runtime Owner

[ANALYSIS]
Target: prove or repair the shell controller and HUD overlay blockers without corrupting scene/prefab state.
Affected systems: production player prefab, input owner, movement owner, motor owner, camera owner, interaction owner, HUD/visor owner, pause/PDA routes, telemetry.
Zero GC proof: static scan is insufficient; future runtime owner must produce Profiler/GCMonitor proof for input, movement, interaction, HUD text, PDA/pause navigation, and camera visual sync.
State check: prove no duplicate player owner, no duplicate prompt owner, no direct-input shell active in production route, no ScreenSpaceOverlay interactive gameplay HUD, no stale static owner across non-reload transition.
Rule quote: one fact has one owner, one route, and one proof artifact; `GlobalQualityWeight` scales cadence/presentation only, not gameplay truth, save identity, DTO layout, action ids, or route ownership.

## Static Blocker Anchors

Use these anchors before any mutation:

- Shell blocker source: `Assets/_Project/Scripts/World/HectonWorldShellController1428.cs`
  - report line 10: `public sealed class HectonWorldShellController1428 : MonoBehaviour, IUpdatable`
  - report line 47: `public void Tick(float deltaTime)`
  - report line 57: writes `_transform.rotation`
  - report lines 66-68: writes `_transform.position`
  - report line 115: `Keyboard.current`
  - report line 153: `Mouse.current`
  - report lines 132-142: `Input.GetKey`
  - report line 161: `Input.GetMouseButton`
  - report lines 164-165: `Input.GetAxisRaw`
- HUD blocker prefab: `Assets/_Project/Prefabs/HUD_Internal.prefab`
  - report line 46: `NASAPunk.Visor.SuitHUDScreenCompositor`
  - report line 53: `forceScreenSpaceOverlay: 1`
- Candidate production player: `Assets/_Project/Prefabs/Player.prefab`
- Candidate production HUD: `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab`
- Prior packet: `taskslocal/runtime_system_20260605/RUNTIME_OWNER_01_PLAYER_UI_MOVEMENT_VERTICAL_SLICE_PACKET.md`

## Process Guard

- No raw `.prefab`, `.unity`, or `.asset` YAML edits.
- No Unity mutation until a clean process gate is recorded: active Unity/import/compiler/shader/dotnet processes, CPU state, dirty worktree, active scene risk.
- Prefer Unity API/editor script mutation after clean process gate. If scene/prefab mutation is needed, mutate through Unity API and read back prefab asset plus scene instance values.
- Do not launch `dotnet build` when CPU is over 50 percent or any `dotnet`/`csc.exe`/Unity compiler/import/shader process is active.
- Do not overwrite other agents' edits. Do not revert unrelated changes.
- Do not add packages, change project settings, tags, layers, URP assets, quality assets, or public contracts without explicit approval.

## Execution Tasks

### Lane A - Shell Controller Blocker

1. Run static confirmation with `rg` against `HectonWorldShellController1428.cs` for `IUpdatable`, `Tick`, `Keyboard.current`, `Mouse.current`, `Input.Get`, `transform.position`, and `transform.rotation`. Record exact line numbers in the proof packet.

2. Read Unity scene binding without mutation after the process gate: active tagged `Player`, `BootstrapState.CurrentPlayerObject`, root prefab source, enabled components, and whether `HectonWorldShellController1428` exists enabled in `02_HECTON_WORLD`.

3. Read dispatcher registration without mutation: prove whether the shell is registered in any active dispatcher lane. Required fields: lane, phase, enabled state, GameObject path, scene name, and whether production `HectonPlayerMovement` is also registered.

4. If the shell is inactive and not scene-bound, mark it `STATIC RISK ONLY` and do not edit it. Still add a future cleanup note to quarantine or delete only after ownership review proves no demo/debug dependency.

5. If the shell is active, do not patch movement inside the shell. Classify it as `ACTIVE ROUTE BLOCKER` and plan replacement through the production player route: `Player.prefab`, `InputDispatcher`, `HectonPlayerMovement`, `HectonPlayerMotor`, and `HectonPlayerCameraRig`.

6. Checkpoint A: produce shell readback with `PASS/FAIL/PENDING` for active state, dispatcher registration, input polling, transform writes, production player coexistence, and safe replacement route. No gameplay claim yet.

### Lane B - Player Movement Proof

7. Read production movement ownership: `HectonPlayerMovement` owns body state, AUP, movement mode, depth/submerged state, and interaction lock; `HectonPlayerMotor` owns Rigidbody/capsule application; `HectonPlayerCameraRig` owns late-frame camera presentation only.

8. Prove input owner boundary: `InputDispatcher` is the only gameplay input polling owner. Consumers read immutable input snapshots or typed signals. Static scan must reject active path hits for `Input.GetKey`, `Input.GetAxis`, `Input.GetButton`, hot `Keyboard.current`, hot `Mouse.current`, hot `Gamepad.current`, and runtime string action lookup.

9. Prove dispatcher phase order: input snapshot in `PRE_SIMULATION`, movement truth in `SIMULATION`, motor/job completion/buffer swap in `POST_SIMULATION`, camera/HUD/haptics in `VISUAL_SYNC`. No presentation owner may mutate movement truth.

### Lane C - Swim / Walk / Control Proof

10. Prove walk/interior or shoreline control: forward/back/strafe, turn, stop, interaction lock, tool-use movement penalty if present, and no camera/UI script writing gameplay position.

11. Prove swim control: forward/back/strafe, ascend, descend, drag/inertia, surface/dive transition, depth readout response, oxygen/hazard warning response, and return-path control. `PlayerSwimMotor` is math helper only, not route authority.

12. Checkpoint B: produce player/control readback. Required columns: active owner, dispatcher phase, input source, body state source, motor apply source, camera visual-sync source, walk state, swim state, interaction lock state, runtime proof status.

### Lane D - HUD Mode / Binding Proof

13. Read active HUD instances without mutation: `HUD_Internal`, `Suit_HUD_Canvas`, `SuitHUDV4CanvasOverlay`, `SurvivalHUDController`, `InteractionUI` namespace variant, `VisorHUDController`, and `SuitHUDPresentationController`. Record GameObject path, prefab source, enabled state, canvas render mode, camera binding, world/visor anchor, and interactivity.

14. Classify `HUD_Internal.forceScreenSpaceOverlay: 1`. Accepted outcomes only:
    - proven inactive/non-production;
    - proven noninteractive debug/loading/legal/accessibility bridge;
    - repaired through Unity API/editor script to an approved diegetic/visor/world-space route after clean process gate and readback.

15. Reject any active interactive first-party gameplay HUD using `RenderMode.ScreenSpaceOverlay`. UI bible allows flat screen-space only for noninteractive debug, loading, legal/accessibility, or explicitly approved frontend bridge screens.

16. Verify HUD critical readout ownership: oxygen, depth, pressure/stress/integrity, energy/power, tool state, interaction prompt, save notification, route/compass/signal cue, and warning/failure state. HUD presents owner-published facts; HUD does not invent telemetry.

### Lane E - Zero-GC UI Proof

17. Static scan active HUD/UI route for forbidden text updates: `.text =`, `SetText(string)`, interpolation, `string.Format`, numeric `.ToString()`, enum `.ToString()`, runtime hierarchy hashing, and string-key localization in HUD/update paths. Verify active UI text route uses preallocated buffers, `TryFormat`, `TMP_Text.SetCharArray`, `TmpTextNoAlloc`, `CharBufferPool`, or equivalent fixed-buffer path.

18. Checkpoint C: produce HUD/zero-GC report. Required verdicts: `HUD_Internal` production status, active canvas render modes, physical carrier/anchor, critical readout source owner, text update path, input navigation path, screenshot status, UI GC proof status, and whether ScreenSpaceOverlay remains a blocker.

### Lane F - Black-Box / Telemetry Proof

19. Verify player/input/UI black-box fields for last 300 frames: AUP/world position, velocity, movement mode, depth, underwater flag, oxygen, pressure/stress, input bitmask, action owner frame id, active tool, interaction target hash, UI focus state, HUD mode, and error flags.

20. Verify dump paths:
    - input: `Docs/AgentLogs/Dump_INPUT_DETERMINISM.bin`
    - player kinematics: `Docs/AgentLogs/Dump_PLAYER_KINEMATICS.bin`
    - combined route if added by future owner: `Docs/AgentLogs/Dump_PLAYER_UI_MOVEMENT_{timestamp}.bin`
  Do not use `Dump_[ID].bin` unless an explicit agent ID is active at runtime.

21. Verify telemetry write path is fixed-size and non-allocating. Persistent native state belongs to DataVault or an explicitly owned route. No new local persistent `NativeArray` fields in MonoBehaviour runtime managers.

22. Verify profiler marker coverage for input poll, movement simulation, motor apply, camera visual sync, interaction probe, HUD text flush, PDA/pause update, haptic dispatch, and telemetry write. Marker names and budgets are estimates until profiler artifact exists.

23. Verify non-reload reset path: generation token, stop ingress, drain signal/queue lanes, complete/cancel jobs only at owner window, dispose/release buffers, clear registry references, reset replay counters, and ignore stale async callbacks.

24. Checkpoint D: final blocker disposition. Required output: shell `inactive/proven blocker/repaired by production route`, HUD overlay `inactive/noninteractive bridge/repaired/still blocker`, GC `0 B verified/PENDING`, telemetry `300-frame ring verified/PENDING`, route result `PENDING VERIFICATION` unless Unity proof exists.

## Required Proof Packet For Future Runtime Owner

Do not write screenshots/log fluff under `Assets`.

Required files under the current accepted proof-packet path:

- `manifest.json`
- `manifest.sha256`
- copied Unity editor/player log
- `shell_controller_readback.md`
- `player_owner_phase_table.md`
- `input_static_forbidden_api_scan.md`
- `hud_mode_binding_readback.md`
- `zero_gc_ui_scan_and_profiler.md`
- `route_control_readback.md`
- `black_box_dump_manifest.md`
- screenshots for 720p, 16:9, 16:10, 21:9, 4:3
- short clip showing movement, swim, interaction, HUD prompt, PDA/pause, and return path

## GlobalQualityWeight Consequences

`GlobalQualityWeight` is continuous. Low/Middle/High/Ultra are review labels, not branches.

- Low around `0.0`: shell path must not own production movement. HUD remains readable, physical/visor anchored or explicitly noninteractive, zero-GC, with oxygen/depth/warning/prompt clarity. No flat overlay-only gameplay HUD acceptance.
- Middle around `0.35`: production movement, swim, interaction, input, HUD, PDA, and pause routes are complete enough for first-20 proof. Presentation can add restrained glass/instrument feedback without changing truth.
- High around `0.70`: saved budget buys smoother camera visual sync, richer haptics, stronger HUD material response, better warning degradation, and denser prompt/route presentation after compact proof exists.
- Ultra around `1.0`: visual overkill only in `VISUAL_SYNC`: visor contamination, richer screen material, secondary tool/camera micro-motion, diagnostics, and haptics. Gameplay truth, action ids, DTO layout, save identity, and owner routes stay unchanged.

Continuous scaling notes:

- Scale cadence, smoothing, UI material richness, optional diagnostics, haptic layers, and presentation density.
- Do not scale movement authority, action semantics, oxygen/depth truth, interaction result, save layout, black-box field identity, or HUD source ownership.
- Use hysteresis: 2-3 seconds for UI/presentation cadence changes and 3-5 meters for distance/panel quality changes.

## Rejection Gates

- Shell controller is active in production route and writes movement/camera truth.
- Gameplay movement polls Unity input directly.
- Production player and shell both claim player movement authority.
- `HUD_Internal.forceScreenSpaceOverlay` remains active interactive gameplay HUD.
- HUD presents fake telemetry or duplicates survival/interaction truth.
- UI text allocates or clips at 720p.
- PDA/pause/rebinding requires mouse only.
- Camera writes gameplay truth.
- No 300-frame black-box ring for player/input/UI critical state.
- Report claims `READY`, `PASS`, `DONE`, `SOLVED`, or runtime readiness without Unity Console, Play Mode/player run, Profiler/GC, screenshot/clip, and save/load evidence.

## Final Reporting Template For Future Runtime Agent

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
