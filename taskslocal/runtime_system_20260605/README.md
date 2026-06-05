# Runtime System Task Index - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_DOC`.
Scope: runtime/player/UI movement task packets generated during the 2026-06-05 orchestration run.

This folder does not prove runtime behavior, Play Mode, player build, profiler, GC, save/load, UI readability, or input-device readiness.

## Current Packets

- `RUNTIME_OWNER_01_PLAYER_UI_MOVEMENT_VERTICAL_SLICE_PACKET.md` - future implementation packet for walking/interior or shoreline movement, swimming, ascend/descend, camera feel, interaction affordance, HUD/visor essentials, PDA/pause/rebinding, zero-GC HUD updates, black-box telemetry, save/load proof, and first-20 route acceptance.
- `RUNTIME_OWNER_02_SHELL_HUD_BLOCKER_REPAIR_PACKET.md` - future repair/proof packet for shell movement/input authority and HUD overlay blocker isolation.

## Current Reports

- `Docs/Reports/RuntimeSystem_20260605/PLAYER_UI_MOVEMENT_STATIC_ANCHOR_AUDIT_20260605.md/.csv` - static anchor audit for 28 player/UI/movement candidates. All rows are `PENDING VERIFICATION`. Hard blockers: `HectonWorldShellController1428` direct input/transform shell risk and `HUD_Internal.prefab` `forceScreenSpaceOverlay: 1`.
- `Docs/Reports/RuntimeSystem_20260605/SHELL_HUD_SCENE_BINDING_ESCALATION_20260605.md/.csv` - static escalation: `02_HECTON_WORLD.unity` contains active `Player` with enabled scene-local `HectonWorldShellController1428`; `HUD_Internal` compositor is disabled but keeps latent `forceScreenSpaceOverlay: 1`.

## Hard Boundaries

- Do not claim movement, swimming, UI, input, camera, HUD, PDA, pause, save/load, or first-20 route readiness from this packet alone.
- Do not run Unity, Play Mode, player build, profiler, or `dotnet build` unless CPU is below 50 percent and no Unity/import/compiler/shader/package process is active.
- Do not raw-edit `.unity`, `.prefab`, `.asset`, `.mat`, or project settings.
- Do not add packages or mutate public contracts without explicit dependency proof and approval.
- Runtime implementation must remain zero-GC in hot paths and use existing owner routes, typed signals, cold GlobalRegistry injection, and continuous `GlobalQualityWeight`.

## Next Runtime Owner

Start with `RUNTIME_OWNER_01_PLAYER_UI_MOVEMENT_VERTICAL_SLICE_PACKET.md`.

First required proof is no-mutation Unity readback of active player, movement owner, HUD owner, input owner, interaction prompt, camera graph, `HUD_Internal` production status, and whether the enabled scene-local `HectonWorldShellController1428` on `02_HECTON_WORLD` `Player` is still winning over production `Player.prefab`.

Final status remains `PENDING VERIFICATION`.
