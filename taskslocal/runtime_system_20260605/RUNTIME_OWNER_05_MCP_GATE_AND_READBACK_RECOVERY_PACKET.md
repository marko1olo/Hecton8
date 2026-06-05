# RUNTIME_OWNER_05_MCP_GATE_AND_READBACK_RECOVERY_PACKET

Status: EXECUTION PACKET / PENDING VERIFICATION
Evidence class: STATIC_DOC + STATIC_LOG_INPUT
Owner: future Unity/runtime readback owner
Packet writer constraint: no Unity mutation, no build, no Play Mode, no import, no asset mutation, no scene save, no prefab save, no material save, no Addressables mutation, no project-settings mutation, no raw YAML mutation.

## Objective

Recover the Unity/MCP readback lane without corrupting the project while the process gate is red. This packet is the preflight and no-mutation readback gate that must run before `RUNTIME_OWNER_04_PLAYER_UI_MOVEMENT_UNITY_READBACK_AND_REPAIR_PACKET.md` can repair active player, HUD, input, movement, camera, interaction, save/load, or h8_1475 proof blockers.

First-20 route blocker targeted for removal: no trusted Unity readback path exists while CPU/import/compiler processes are active, so active player/HUD evidence cannot be promoted past static scans.

This packet does not prove Unity state, runtime readiness, visual quality, movement, swimming, UI, save/load, profiler, GC, or h8_1475 capture readiness. All runtime claims remain `PENDING VERIFICATION`.

## Current Facts To Preserve

- Current orchestrator memory source: `Docs/Orchestration/ORCHESTRATOR_NIGHT_20260605.md`. The future owner must read the latest tail at execution time; cursor references inside this packet are static authoring-time evidence only.
- Process gate is red in current controller evidence: CPU sampled at `100` in Cursor 70 and `67` in this packet's local refresh, with active `mcp-for-unity`, `Unity`, `Unity.ILPP.Runner`, `UnityPackageManager`, and multiple `UnityShaderCompiler` processes.
- MCP HTTP log exists: `Docs/Reports/McpHttp_20260605_20260605_212328.out.log` and `Docs/Reports/McpHttp_20260605_20260605_212328.err.log`.
- MCP log shows `mcp-for-unity-server 3.4.1` at `http://127.0.0.1:8088/mcp`, plugin `Hecton8` registered, 30 tools registered for a Unity session, and tool visibility synced.
- MCP log also contains repeated stale or wrong-route `GET /mcp` and well-known OAuth 404s. Those are not proof of failure by themselves, but they are stale-route evidence if paired with no successful POST/streamable session and empty resources/tools.
- Tooling existence does not allow Unity readback while CPU/compiler/import/package gate is red.
- Runtime repair packet already exists: `taskslocal/runtime_system_20260605/RUNTIME_OWNER_04_PLAYER_UI_MOVEMENT_UNITY_READBACK_AND_REPAIR_PACKET.md`.

## Authority Docs And Mandates

Read before execution:

- `AGENTS.md`
- `systems.md`
- `performance.md`
- `quality.md`
- `testing.md`
- `player.md`
- `ui.md`
- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/UI_Data_Streaming_ZeroGC_Optimization.txt`

Read `HECTON8_ORCHESTRATOR.md` only if the future owner is explicitly assigned controller/orchestration work. An ordinary Unity/runtime readback owner must not read it.

Mandate consequences:

- Runtime readback is allowed only at a safe process gate.
- Readback evidence is not repair evidence.
- Static logs and MCP availability do not prove Play Mode, console health, profiler, GC, or visual quality.
- `GlobalQualityWeight` can scale proof matrix breadth and presentation cadence only. It cannot change player authority, HUD authority, save identity, route selection, DTO layout, or h8_1475 proof fields.
- h8_1475 proof fields include `h8_1475_visual_reference_comparison.md` created from `Docs/Reports/AssetSystem_20260605/H8_1475_VISUAL_REFERENCE_COMPARISON_TEMPLATE_20260605.md` when runtime captures are used for visual proof.

## Owned Scope

The future owner may inspect process state, MCP server logs, MCP tool/resource availability, Unity editor state, loaded scene object identity, active Player source, Player prefab/source conflict, cameras, HUD compositor/overlay state, input owner, GC/profiler availability, console state, dirty state, and proof artifact paths.

The future owner must not modify Unity, project files, scenes, prefabs, materials, assets, Addressables, project settings, tags/layers, packages, build settings, import settings, or raw YAML in this packet.

## Exact Process Preflight

Required safe gate before any Unity/MCP readback:

- Take 3 CPU samples at least 10 seconds apart.
- All 3 CPU samples must be under 50 percent.
- `Unity.ILPP.Runner`, `UnityShaderCompiler`, `UnityPackageManager`, `AssetImportWorker`, `dotnet`, `csc`, and `MSBuild` must be absent.
- `Unity` may exist only if it is idle: not compiling, not importing, not domain-reloading, not running Play Mode, and not saving.
- `mcp-for-unity` may exist only as an idle server process and must not be consuming meaningful CPU.
- If any blocker exists, stop with `BLOCKED BY PROCESS GATE`; do not try a "small" readback.

Required local command shape:

```powershell
$names='Unity','Unity.ILPP.Runner','UnityShaderCompiler','UnityPackageManager','AssetImportWorker','dotnet','csc','MSBuild','mcp-for-unity'
$cpu=(Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average
$procs=Get-Process | Where-Object { $names -contains $_.ProcessName } | Select-Object ProcessName,Id,CPU
[pscustomobject]@{CpuAverage=$cpu; BlockingProcessCount=($procs|Measure-Object).Count}
$procs | Format-Table -AutoSize
```

## MCP Preflight

Required checks after the process gate is safe:

- Confirm the MCP server endpoint from log or active process: `http://127.0.0.1:8088/mcp`.
- Confirm log paths exist and capture a fresh copy outside `Assets`.
- Confirm plugin registration line for `Hecton8`.
- Confirm 30 or current expected tools are registered, or record exact tool visibility.
- Confirm MCP tool/resource list is non-empty if the transport exposes resources.
- If resources are empty but tools are callable, classify as `TOOLS_ONLY_MCP` and continue only with read-only tools.
- If resources are empty and tools are not callable, classify as `MCP_ROUTE_STALE` and stop.
- If latest HTTP activity is only 404 `GET /mcp` or OAuth well-known probes with no successful `POST /mcp`, `GET /mcp` stream, or tool session after that, classify as `MCP_ROUTE_STALE` and stop.
- If Unity reports compiling/importing/domain reload through MCP editor state, stop even if the OS process gate looked clean.

## Read-Only Unity Sequence Once Safe

Sequence is read-only. Any mutation prompt, save dialog, import trigger, dirty-scene creation, or unexpected state change stops the packet.

- Read editor state: compiling, importing, domain reload, play mode, active scene, dirty scenes, unsaved prefab stage, selected build target, console error/warning counts.
- Read loaded scenes and active scene hierarchy with bounded pagination.
- Read active tagged `Player` objects and any objects named `Player`.
- For each candidate player, record hierarchy path, scene, active state, tag, layer, prefab source, scene-local status, enabled components, and instance id.
- Read `BootstrapState.CurrentPlayerObject` or equivalent runtime context if exposed; classify current player source as scene-local shell, production prefab instance, bootstrap-spawned production player, null, or stale.
- Read production `Player.prefab` source relationship and reconcile it with scene instances.
- Read cameras: Main Camera, player camera rig, HUD/render cameras, camera stack, Cinemachine brain if present, and any camera writing to HUD render textures.
- Read HUD: `HUD_Internal`, `Suit_HUD_Canvas`, `SuitHUDScreenCompositor`, `SuitHUDV4CanvasOverlay`, `InteractionUI`, `VisorHUDController`, `SuitHUDPresentationController`, render mode, `forceScreenSpaceOverlay`, enabled state, canvas carrier, and player binding.
- Read input owner: `InputDispatcher`, direct shell input, hot `Keyboard.current`/`Mouse.current`/`Gamepad.current` consumers, legacy `Input.GetKey`/`GetAxis` routes, device matrix availability.
- Read profiler/GCMonitor availability, black-box telemetry owners, and console route.
- Read dirty state again before exit. If the scene or prefab stage became dirty, stop and record `READBACK CAUSED DIRTY STATE`; do not save.

## Strict Stop Rules

Stop immediately on:

- CPU sample over 50 percent.
- Any active `Unity.ILPP.Runner`, `UnityShaderCompiler`, `UnityPackageManager`, `AssetImportWorker`, `dotnet`, `csc`, or `MSBuild`.
- Unity editor state says compiling, importing, reloading, saving, or Play Mode running.
- MCP resources empty and read-only tools unavailable.
- MCP route stale after successful server log is older than the current Unity session.
- Any prompt to save scene, apply prefab, import asset, refresh asset database, modify project settings, or mutate Addressables.
- Any dirty scene/prefab state caused by readback.
- Any need to fix by raw YAML edit.

## Required Evidence Before Any Repair

Before packet 04 can run repair, gather outside `Assets`:

- `process_gate.md` with three CPU samples and blocker process tables.
- Fresh copy of MCP `.out.log` and `.err.log`.
- Unity editor state readback.
- Unity console error/warning table.
- Loaded scene and active scene table.
- Active player/source/prefab reconciliation table.
- Camera stack table.
- HUD compositor/overlay mode table.
- Input owner/direct-input table.
- Profiler/GCMonitor/black-box availability table.
- Dirty state before/after table.
- h8_1475 visual-reference comparison template availability when HUD/player captures feed the h8 proof packet.
- Screenshots only if safe and read-only. If screenshot capture would dirty/import/save or run Play Mode while gate is unsafe, skip and record `SCREENSHOT BLOCKED`.

## Handoff To Packet 04

Run `RUNTIME_OWNER_04_PLAYER_UI_MOVEMENT_UNITY_READBACK_AND_REPAIR_PACKET.md` only after this packet proves:

- Process gate was green.
- MCP was current enough for read-only Unity evidence.
- No readback-caused dirty state exists.
- Active player source is known.
- Production prefab/source relationship is known.
- Shell player status is known.
- HUD overlay/compositor status is known.
- Input owner is known.
- Console/profiler/GC/black-box evidence routes are known.
- h8_1475 visual-reference comparison route is known when HUD/player captures are expected to feed the canonical proof packet.

If any field remains unknown, packet 04 starts at `BLOCKED BY READBACK`, not repair.

## Regression Model

- CPU: readback itself must not run during import/compile/package/shader work. Any Unity tool call during red gate risks extending stalls and corrupting evidence timing.
- GC: this packet makes no runtime GC claim. GC proof is required later through GCMonitor or profiler over active route stress windows.
- Memory: this packet must not import, load Addressables, enter Play Mode, or trigger asset residency changes. Any memory claim remains absent.
- Cadence: readback must be discrete and bounded. No polling loop that hammers MCP while Unity is busy.
- Correctness: evidence must identify actual active owners. Static scene YAML, old screenshots, or server availability cannot replace current Unity readback.
- Visual proof: none claimed. Existing surface screenshots remain rejected diagnostic evidence until new safe captures pass the visual floor.

## Low/Middle/High/Ultra Consequences

- Low: gate protects compact hardware from import/compiler/tooling overload and prevents false runtime proof. Readback must preserve movement/HUD route truth and avoid screenshot-only claims.
- Middle: full owner discovery is required before repair, including player, camera, HUD, input, save/load, profiler, and black-box routes.
- High: richer h8_1475 proof can be prepared only after active owners are known; high-tier capture cannot hide shell/HUD authority conflicts.
- Ultra: visual overkill proof is allowed only after process, readback, player route, HUD route, and telemetry routes are clean. Ultra adds capture density, not different gameplay truth.

## Numbered Tasks

### Phase 0 - Static Refresh And Process Gate

1. Read `Docs/Orchestration/ORCHESTRATOR_NIGHT_20260605.md` tail and confirm latest relevant cursor, current front, and last accepted/rejected evidence.

2. Read `RUNTIME_OWNER_04_PLAYER_UI_MOVEMENT_UNITY_READBACK_AND_REPAIR_PACKET.md` and this packet. Confirm packet 05 is a preflight for packet 04, not a replacement repair plan.

3. Read MCP logs `Docs/Reports/McpHttp_20260605_20260605_212328.out.log` and `.err.log`. Record server version, endpoint, plugin registration, tool count, successful MCP session activity, and stale-route 404 evidence.

4. Take process sample 1. If CPU is over 50 percent or blocker processes exist, record `BLOCKED BY PROCESS GATE` and stop before Unity/MCP readback.

5. Take process samples 2 and 3 at least 10 seconds apart only if sample 1 is safe. All samples must pass.

6. Checkpoint 0: produce `process_mcp_preflight.md` with process verdict, MCP log verdict, allowed next step, and explicit `NO UNITY MUTATION`.

### Phase 1 - MCP Health And Editor Readiness

7. Query MCP/tool availability only after the process gate passes. Record whether tools and resources are exposed. If resources are empty, classify `TOOLS_ONLY_MCP` or `MCP_ROUTE_STALE` using the rules above.

8. Read Unity editor state through MCP only if MCP is current and gate is safe. Record compiling/importing/domain reload/play mode/saving/dirty status.

9. Read console error/warning counts and copy the relevant Unity log path. Do not clear the console.

10. Read loaded scenes and active scene without loading, unloading, saving, or setting active scene.

11. Read dirty scene/prefab stage state before any hierarchy readback.

12. Checkpoint 1: produce `editor_readiness_readback.md` with editor state, console state, loaded scenes, dirty state, MCP state, and stop/continue verdict.

### Phase 2 - Player, HUD, Camera, Input Readback

13. Read all active tagged `Player` objects and named `Player` objects. Record hierarchy path, scene, prefab source, scene-local status, enabled components, tag, layer, active state, and instance id.

14. Read bootstrap/current player context. Reconcile it against active scene player candidates and production `Player.prefab`.

15. Read production `Player.prefab` relationship and classify conflict: `production_instance_active`, `scene_shell_active`, `duplicate_authority`, `null_current_player`, or `unknown`.

16. Read camera stack: Main Camera, player camera rig, HUD/render cameras, Cinemachine brain if present, render textures, camera owners, and whether any presentation camera writes gameplay truth.

17. Read HUD compositor/overlay stack: `HUD_Internal`, `Suit_HUD_Canvas`, `SuitHUDScreenCompositor`, `SuitHUDV4CanvasOverlay`, `InteractionUI`, `VisorHUDController`, `SuitHUDPresentationController`, render mode, `forceScreenSpaceOverlay`, enabled state, and active player binding.

18. Checkpoint 2: produce `active_player_hud_camera_readback.md` with active owner verdicts and all blocker classifications. If any active owner is unknown, packet 04 remains blocked.

### Phase 3 - Input, Profiler, Dirty-State Exit, And Handoff

19. Read input owner state: `InputDispatcher`, direct shell input consumers, device availability, direct `Keyboard.current`/`Mouse.current`/`Gamepad.current` hot consumers, and legacy `Input.GetKey`/`Input.GetAxis` routes if exposed.

20. Read profiler, GCMonitor, black-box telemetry, save/load proof-route availability, and h8_1475 proof-route field coverage. Do not start profiling or Play Mode.

21. Read dirty state after all readback. If dirty state changed, record failure and do not save.

22. Write handoff verdict for packet 04: `READY_FOR_REPAIR_READBACK`, `BLOCKED_BY_PROCESS_GATE`, `BLOCKED_BY_MCP_ROUTE`, `BLOCKED_BY_EDITOR_STATE`, `BLOCKED_BY_UNKNOWN_PLAYER_OWNER`, `BLOCKED_BY_UNKNOWN_HUD_OWNER`, or `BLOCKED_BY_DIRTY_STATE`.

23. Assemble required evidence files outside `Assets`; include log copies, readback tables, and screenshot paths only if screenshots were safe.

24. Checkpoint 3: final controller report. Required labels: process gate, MCP state, editor state, active player source, prefab conflict, HUD overlay, input owner, profiler/GC route, dirty state, packet 04 handoff, blockers, and `PENDING VERIFICATION` for all runtime behavior.

## Final Reporting Shape

```text
What was wrong:
- ...

What I did:
- ...

In-game result:
- UNITY-VERIFIED: none unless fresh readback artifacts exist.
- PENDING VERIFICATION: movement, swimming, UI, input, camera, HUD, save/load, profiler, GC, visuals.

What was verified:
- Process gate:
- MCP state:
- Editor state:
- Console:
- Loaded scenes:
- Active Player:
- Player prefab/source conflict:
- Camera:
- HUD:
- Input:
- Profiler/GC route:
- Dirty state:
- Handoff to packet 04:

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
