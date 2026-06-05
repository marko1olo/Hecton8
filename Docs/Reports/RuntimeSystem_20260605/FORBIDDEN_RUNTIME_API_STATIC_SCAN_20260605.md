# Forbidden Runtime API Static Scan - 2026-06-05

ID: `RUNTIME_CONTROLLER_05_FORBIDDEN_API_STATIC_SCAN`
Status: `STATIC_TRIAGE_ONLY / PENDING UNITY AND PROFILER VERIFICATION`
Evidence class: `STATIC_SOURCE_GREP + STATIC_CONTROLLER_TRIAGE`

No Unity run, Play Mode, build, profiler, GCMonitor, import, scene edit, prefab edit, material edit, or `Assets` mutation was performed.

## Purpose

This scan separates current production-blocking source hits from tooling noise. It does not repair code and does not claim runtime readiness.

CSV companion:

`Docs/Reports/RuntimeSystem_20260605/FORBIDDEN_RUNTIME_API_STATIC_SCAN_20260605.csv`

## Authorities And Mandates

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `TASTE.md`
- `player.md`
- `input.md`
- `ui.md`
- `UI_DIEGETIC_HUD_STANDARDS.md`
- `systems.md`
- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/ARCH_Signal_Lane_Segregation.txt`
- `.agents-skills/UI_Data_Streaming_ZeroGC_Optimization.txt`
- `.agents-skills/CTRL_Device_Abstraction_Haptics.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

## Scoped Grep

Root:

`Assets/_Project/Scripts`

Patterns:

- direct input: `Input.Get*`, `Keyboard.current`, `Mouse.current`, `Gamepad.current`
- screen-space gameplay HUD risk: `RenderMode.ScreenSpaceOverlay`, `ScreenSpaceOverlay`
- UI string mutation: `.text =`, `SetText(`
- visibility churn: `SetActive(`
- scene search: `Camera.main`, `FindObjectOfType`, `FindObjectsOfType`, `GameObject.Find`
- sync physics casts: `Physics.Raycast`, `SphereCast`, `CapsuleCast`, `OverlapSphere`
- coroutine scheduler: `StartCoroutine(`
- IMGUI: `OnGUI(`
- material risks: `new MaterialPropertyBlock(`, `.material`, `.materials`
- message bus abuse: `SendMessage(`, `BroadcastMessage(`

Editor/tooling filters were applied for summary counts only. Filtered noise still needs owner review if an editor-looking file is included in runtime builds.

## Findings

### P0 Active Player/Input Blocker

`Assets/_Project/Scripts/World/HectonWorldShellController1428.cs` is still the hard runtime blocker:

- `Keyboard.current`
- `Mouse.current`
- `Input.GetKey`
- `Input.GetMouseButton`
- `Input.GetAxisRaw`

Static scene evidence already shows this shell component enabled on the active scene-local `Player` in `02_HECTON_WORLD.unity`. If Unity readback proves it is the active player route, full walking/swimming/UI readiness is rejected. Do not patch movement inside this shell. The next owner must first prove active player identity, dispatcher registration, and production `Player.prefab` route status.

### P0 HUD Overlay Blocker

`Assets/_Project/Scripts/Visor/SuitHUDScreenCompositor.cs` and `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs` contain `ScreenSpaceOverlay` paths. Static prefab evidence already shows `HUD_Internal.prefab` has `forceScreenSpaceOverlay: 1`.

If Unity readback proves this route is active for interactive first-party gameplay HUD, HUD acceptance is rejected. It may only survive as explicit noninteractive/debug/loading/legal bridge proof.

### P1 Static Hits Need Owner Triage

The scan found runtime-looking hits for `.text =`, `SetActive(`, `new MaterialPropertyBlock(`, `.material`, and `.materials`. Static grep cannot classify cadence. Some are cold bootstrap, editor-facade, debug, or one-time setup; some may be real hot-path defects. The next owner must triage by active route, dispatcher phase, and profiler/GC proof.

`Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs` line 1560 is a P1 runtime repair defect after source-context triage: remove the coroutine repair route through `RUNTIME_OWNER_06_THERMAL_DRS_COROUTINE_REPAIR_PACKET.md` and coordinate the same source pass with `RUNTIME_OWNER_07_THERMAL_DRS_BLACKBOX_DUMP_ROUTE_PACKET.md`.

### Pattern Clear In This Scope

The scoped scan found zero non-editor hits for `Physics.Raycast`, `Physics.SphereCast`, `Physics.CapsuleCast`, and `Physics.OverlapSphere` with the current filters.

This is not physics acceptance. It only means this exact grep did not find those patterns outside excluded editor/tooling paths.

## First-20 Route Impact

This scan removes one false-acceptance route blocker: the project cannot claim first-20 walk, swim, interact, HUD, PDA, pause, save/load, or player proof until:

- active player identity is production-owned;
- shell direct input is absent or proven non-owner;
- interactive HUD is not accepted through a generic `ScreenSpaceOverlay`;
- HUD text and visibility paths are profiled, not merely grep-favorable;
- no hot path allocates or mutates presentation as gameplay truth.

## Low / Middle / High / Ultra Consequences

Low/compact:

- Must keep production input snapshots, movement, HUD readouts, and route cues readable with zero hot-path allocation.
- Cannot accept shell direct input or overlay-only HUD as cheap mode.

Middle:

- Must prove expected player lane: production movement, interaction, HUD, PDA/pause, input owner, and camera owner with dispatcher phase order.

High:

- May add richer visor, camera, haptic, and UI material response after owner and GC proof.

Ultra:

- Visual-sync overkill only. It cannot change player identity, action semantics, HUD truth, save identity, DTO layout, or input ownership.

## Regression Model

- CPU: static scan only. No runtime timing claim.
- GC: static scan only. No `0 B/frame` claim.
- Memory: static scan only. Material/MPB hits need owner triage before leak or clone claims.
- Cadence: direct input/screen overlay blockers are route-level risks; other categories need phase/cadence proof.
- Correctness: active shell player and active gameplay overlay HUD remain blockers until Unity readback classifies them.

Final status: `STATIC_TRIAGE_ONLY / PENDING UNITY AND PROFILER VERIFICATION`.
