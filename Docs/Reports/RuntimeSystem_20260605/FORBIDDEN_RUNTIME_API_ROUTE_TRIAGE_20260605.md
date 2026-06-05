# Forbidden Runtime API Route Triage - 2026-06-05

ID: `RUNTIME_CONTROLLER_06_FORBIDDEN_API_ROUTE_TRIAGE`
Status: `STATIC_ROUTE_TRIAGE_ONLY / PENDING UNITY AND PROFILER VERIFICATION`
Evidence class: `STATIC_SOURCE_READBACK`

No Unity run, Play Mode, build, profiler, GCMonitor, import, scene edit, prefab edit, material edit, or `Assets` mutation was performed.

CSV companion:

`Docs/Reports/RuntimeSystem_20260605/FORBIDDEN_RUNTIME_API_ROUTE_TRIAGE_20260605.csv`

## Authorities And Mandates

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `player.md`
- `input.md`
- `ui.md`
- `systems.md`
- `performance.md`
- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/UI_Data_Streaming_ZeroGC_Optimization.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`

## Result

The grep scan was useful but too broad. Source context separates hard route blockers from false positives:

- `HectonWorldShellController1428.cs` remains a P0 static route blocker. It reads `Keyboard.current`, `Mouse.current`, and legacy `Input.*` inside movement/look input methods. Static scene evidence already places the enabled shell on the active scene-local `Player`.
- `SuitHUDScreenCompositor.cs` remains a P0 Editor Play Mode proof blocker. `forceScreenSpaceOverlay` can force `RenderMode.ScreenSpaceOverlay` under `UNITY_EDITOR` without `Application.isPlaying` gating.
- `SuitHUDV4CanvasOverlay.cs` is more constrained than the raw grep implies. Its overlay branch is editor-only and redirects to projection while playing. It still requires Unity readback because proof can only pass if the active canvas is `WorldSpace` with the projection route during Play Mode.
- `ThermalDynamicResolutionAdapter.cs` is a P1 runtime repair defect. The coroutine is not a route owner, but it is still a runtime manager coroutine path. Its detailed source anchors and paired black-box dump route defect are tracked in `Docs/Reports/RuntimeSystem_20260605/THERMAL_DRS_STATIC_DEFECT_ANCHORS_20260605.md/.csv`.
- `ScavengingLootOracle.cs` scene search and `DcsAscentProfileOverlay.cs` `OnGUI` are static false positives for player runtime. Both are editor-only by source context.

## Required Next Runtime Owner Actions

1. Run no-mutation Unity readback only after CPU is below 50 percent and Unity/import/compiler/shader/package processes are idle.
2. Record the active scene player object path, source prefab/scene-local state, enabled movement scripts, input owner, camera owner, interaction owner, and dispatcher registration.
3. Record `HUD_Internal`, compositor active/enabled status, `forceScreenSpaceOverlay`, `SuitHUDV4CanvasOverlay.renderPath`, all gameplay HUD canvas render modes, world camera binding, and `GraphicRaycaster` state.
4. Reject any first-20 movement/HUD proof if the scene-local shell wins input/movement/camera authority or if interactive gameplay HUD is proven through `ScreenSpaceOverlay`.
5. Convert `ThermalDynamicResolutionAdapter` dispatcher repair coroutine through `RUNTIME_OWNER_06_THERMAL_DRS_COROUTINE_REPAIR_PACKET.md` only after the active process gate allows code edits and compile verification. Coordinate with `RUNTIME_OWNER_07_THERMAL_DRS_BLACKBOX_DUMP_ROUTE_PACKET.md` in the same source/compile/profiler pass.

## Low / Middle / High / Ultra Consequences

Low/compact:

- Direct shell input and overlay HUD are not allowed as cheap fallback routes. Compact still needs production input snapshots and readable diegetic HUD.

Middle:

- Player, camera, HUD, interaction, and pause/PDA ownership must be phase-proven before movement/UI proof is accepted.

High:

- Richer visor response, HUD material detail, and haptics may be added only after owner and GC proof.

Ultra:

- Visual-sync overkill cannot change player identity, input semantics, HUD truth, save identity, DTO layout, or route authority.

## Regression Model

- CPU: static source read only. No timing claim.
- GC: static source read only. No `0 B/frame` claim.
- Memory: no memory readiness claim.
- Cadence: shell input and compositor overlay are route/proof blockers; coroutine repair is a runtime cadence defect pending owner repair.
- Correctness: static false positives were reduced, but active player/HUD route remains blocked until Unity readback proves ownership.

Final status: `STATIC_ROUTE_TRIAGE_ONLY / PENDING UNITY AND PROFILER VERIFICATION`.
