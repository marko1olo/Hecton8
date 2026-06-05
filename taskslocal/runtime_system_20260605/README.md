# Runtime System Task Index - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_DOC`.
Scope: runtime/player/UI movement task packets generated during the 2026-06-05 orchestration run.

This folder does not prove runtime behavior, Play Mode, player build, profiler, GC, save/load, UI readability, or input-device readiness.

## Shared Proof Inputs

- `Docs/Reports/Batch32/CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md` - mandatory visual reference digest for user-facing water, terrain, sky, flora, and UI captures.
- `Docs/Reports/AssetSystem_20260605/H8_1475_VISUAL_REFERENCE_COMPARISON_TEMPLATE_20260605.md` - required template for `h8_1475_visual_reference_comparison.md` when runtime player/HUD captures feed h8_1475 proof.

Ordinary runtime owners must not read `HECTON8_ORCHESTRATOR.md` unless explicitly assigned controller/orchestration work. They should use the packet evidence basis and current proof folders instead.

## Current Packets

- `RUNTIME_OWNER_01_PLAYER_UI_MOVEMENT_VERTICAL_SLICE_PACKET.md` - future implementation packet for walking/interior or shoreline movement, swimming, ascend/descend, camera feel, interaction affordance, HUD/visor essentials, PDA/pause/rebinding, zero-GC HUD updates, black-box telemetry, save/load proof, and first-20 route acceptance.
- `RUNTIME_OWNER_02_SHELL_HUD_BLOCKER_REPAIR_PACKET.md` - future repair/proof packet for shell movement/input authority and HUD overlay blocker isolation.
- `RUNTIME_OWNER_04_PLAYER_UI_MOVEMENT_UNITY_READBACK_AND_REPAIR_PACKET.md` - next future Unity owner packet for no-mutation active player/HUD readback followed by owner-correct repair and full movement/UI/input/camera/save proof.
- `RUNTIME_OWNER_05_MCP_GATE_AND_READBACK_RECOVERY_PACKET.md` - current next preflight packet for process-gated MCP health, no-mutation Unity editor readiness, active player/HUD/input readback, and packet 04 handoff control.
- `RUNTIME_OWNER_06_THERMAL_DRS_COROUTINE_REPAIR_PACKET.md` - future graphics scalability repair packet for removing the runtime coroutine dispatcher-repair path in `ThermalDynamicResolutionAdapter`; source still requires repair plus compile, Unity Console, Play Mode, GC, and profiler proof.
- `RUNTIME_OWNER_07_THERMAL_DRS_BLACKBOX_DUMP_ROUTE_PACKET.md` - future graphics scalability telemetry repair packet for replacing stale `Dump_13KRA.bin` and the current no-file-write black-box path with a deterministic owner/system binary dump route; source still requires repair plus binary artifact, GC, and profiler proof.
- `RUNTIME_OWNER_08_VFX_DATAVAULT_SOVEREIGNTY_REPAIR_PACKET.md` - future VFX memory-sovereignty repair packet for Biolum, MarineSnow, and PlasmaBeam runtime/editor NativeArray debt split; still pending source repair, compile, Play Mode, GC, profiler, and black-box dump proof.

## Current Reports

- `Docs/Reports/RuntimeSystem_20260605/PLAYER_UI_MOVEMENT_STATIC_ANCHOR_AUDIT_20260605.md/.csv` - static anchor audit for 28 player/UI/movement candidates. All rows are `PENDING VERIFICATION`. Hard blockers: `HectonWorldShellController1428` direct input/transform shell risk and `HUD_Internal.prefab` `forceScreenSpaceOverlay: 1`.
- `Docs/Reports/RuntimeSystem_20260605/SHELL_HUD_SCENE_BINDING_ESCALATION_20260605.md/.csv` - static escalation: `02_HECTON_WORLD.unity` contains active `Player` with enabled scene-local `HectonWorldShellController1428`; `HUD_Internal` compositor is disabled but keeps latent `forceScreenSpaceOverlay: 1`.
- `Docs/Reports/RuntimeSystem_20260605/ACTIVE_PLAYER_SCENE_CONFLICT_MAP_20260605.md/.csv` - consolidated static conflict map for active scene shell, production player prefab GUID absence in `02_HECTON_WORLD`, HUD prefab GUID absence, and compositor overlay risk.
- `Docs/Reports/RuntimeSystem_20260605/FORBIDDEN_RUNTIME_API_STATIC_SCAN_20260605.md/.csv` - scoped static grep triage for direct input, `ScreenSpaceOverlay`, UI string mutation, `SetActive`, material access, and other forbidden/suspicious API patterns. P0 rows remain shell direct input and gameplay HUD overlay route. No runtime/profiler proof.
- `Docs/Reports/RuntimeSystem_20260605/FORBIDDEN_RUNTIME_API_ROUTE_TRIAGE_20260605.md/.csv` - source-context triage for the grep hits. Confirms shell direct input and editor Play Mode overlay proof as blockers, classifies `ThermalDynamicResolutionAdapter` coroutine repair as P1, and excludes editor-only `OnGUI`/scavenging cleanup hits from player-runtime blockers.
- `Docs/Reports/RuntimeSystem_20260605/THERMAL_DRS_STATIC_DEFECT_ANCHORS_20260605.md/.csv` - static source anchors for `ThermalDynamicResolutionAdapter` coroutine repair contamination, stale `Dump_13KRA.bin`, null dump path, and missing binary file write route.
- `Docs/AssetAudit/VFX_DATAVAULT_SOVEREIGNTY_STATIC_REVIEW_20260605.md` and `Docs/AssetAudit/VFX_DATAVAULT_SOVEREIGNTY_AUDIT_20260605.json` - scoped VFX DataVault sovereignty audit. Current static split: 18 direct constructors, 12 editor-only transient constructors, 4 runtime-forbidden constructors, 2 editor/offline persistent constructors, and 6 forbidden declarations.

## Hard Boundaries

- Do not claim movement, swimming, UI, input, camera, HUD, PDA, pause, save/load, or first-20 route readiness from this packet alone.
- Do not run Unity, Play Mode, player build, profiler, or `dotnet build` unless CPU is below 50 percent and no Unity/import/compiler/shader/package process is active.
- Do not raw-edit `.unity`, `.prefab`, `.asset`, `.mat`, or project settings.
- Do not add packages or mutate public contracts without explicit dependency proof and approval.
- Runtime implementation must remain zero-GC in hot paths and use existing owner routes, typed signals, cold GlobalRegistry injection, and continuous `GlobalQualityWeight`.
- Runtime player/HUD screenshots used for h8_1475 proof must include `h8_1475_visual_reference_comparison.md` against the mandatory visual reference digest. Missing comparison keeps visual proof `PENDING VERIFICATION`.

## Next Runtime Owner

Start with `RUNTIME_OWNER_05_MCP_GATE_AND_READBACK_RECOVERY_PACKET.md`, then hand off to `RUNTIME_OWNER_04_PLAYER_UI_MOVEMENT_UNITY_READBACK_AND_REPAIR_PACKET.md` only after process, MCP, editor, active player, HUD, input, profiler/GC route, and dirty-state readback are proven.

First required proof is no-mutation Unity readback of active player, movement owner, HUD owner, input owner, interaction prompt, camera graph, `HUD_Internal` production status, and whether the enabled scene-local `HectonWorldShellController1428` on `02_HECTON_WORLD` `Player` is still winning over production `Player.prefab`.

Final status remains `PENDING VERIFICATION`.
