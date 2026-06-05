# UNITY_OWNER_00 - MCP Gate And Tooling Readiness Packet

ID: `UNITY_OWNER_00_MCP_GATE_AND_TOOLING_PACKET_WRITER`
Role: Unity/MCP process gate and tooling readiness packet writer.
Project: `C:\hades\Hecton8`
Status: `DISTRIBUTABLE_TASK_PACKET / UNITY_NOT_RUN_BY_PACKET_WRITER`
Evidence class: `STATIC_DOC / STATIC_REPORT_SYNTHESIS`

This packet assigns a future Unity/tooling owner to restore or prove Unity MCP readiness before `h8_1475` readback, visual proof, Play Mode, build, import, package, shader, or scene/prefab work.

No Unity readback, runtime proof, visual proof, profiler proof, GC proof, or asset/source acceptance is possible until both gates are clean:

- Process gate: Unity, dotnet, compiler, package, import, and shader workers are idle.
- MCP gate: Codex can see non-empty Unity MCP resources and callable Unity MCP tools for the active project.

Current parent evidence says the gate is red: Unity/dotnet/ILPP/PackageManager/ShaderCompiler were active, and MCP resources exposed to Codex returned empty despite an `mcp-for-unity` process. Treat that as blocker state until fresh evidence proves otherwise.

## Objective

Restore or prove a clean no-mutation Unity/MCP readiness lane so future `ASSET_OWNER_36` execution can safely run `h8_1475` no-mutation readback and proof capture.

First-20 route moment protected: bright first surface exit with production player/HUD, sky/Aegir, Crest ocean, shoreline, photic terrain, and shallow underwater route proof.

Route blocker removed only if this packet produces clean process-gate evidence plus exposed MCP resources/tools. Without both, `ASSET_OWNER_36` must remain blocked.

## Authority Docs

Read before execution:

- `AGENTS.md`
- `HECTON8_ORCHESTRATOR.md`
- `taskslocal/asset_system_20260605/ASSET_OWNER_36_H8_1475_PROOF_EXECUTION_PACKET.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_STATIC_VALIDATION_SUMMARY_20260605.md`
- `C:\Users\danat\.codex\skills\unity-mcp-skill\SKILL.md` if accessible

Mandates followed by this packet writer:

- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

Do not bulk-read unrelated archives or stale batch logs. Do not infer a new agent ID.

## Owned Scope For Future Unity/Tooling Owner

May write only proof artifacts under:

- `Docs/Reports/UnitySystem_20260605/UNITY_OWNER_00_MCP_GATE_*.md`
- `Docs/Reports/UnitySystem_20260605/UNITY_OWNER_00_MCP_GATE_*.txt`
- `Docs/Screenshots/HectonProofPackets/mcp_gate_<YYYYMMDD_HHMMSS>/` only if screenshots become necessary for a no-mutation tooling proof.

No writes are allowed under:

- `Assets/`
- `ProjectSettings/`
- `Packages/`
- `UserSettings/`
- scenes, prefabs, materials, shaders, importers, Addressables settings, package manifests, code files, generated assets, or shared indexes.

## No-Mutation Guard

Forbidden during this packet execution:

- No Unity scene save.
- No prefab apply/revert.
- No asset import fix.
- No package add/remove/resolve.
- No project settings change.
- No PlayerSettings, Quality, URP, Tags/Layers, Addressables, Crest, MapMagic, or Package Manager mutation.
- No Play Mode.
- No build.
- No script compile request by file edit.
- No Unity readback for `h8_1475`.
- No MCP tool that creates, modifies, deletes, refreshes, imports, compiles, saves, plays, pauses, stops, builds, or changes editor/project state.

Allowed only after process gate is clean:

- read-only MCP resource listing;
- read-only editor state/resource inspection;
- read-only Unity console query;
- read-only package/editor state query if exposed by MCP;
- screenshots only if they do not require scene mutation, Play Mode, or saved assets.

If a prompt, pop-up, dirty-scene warning, package repair dialog, import warning, or save request appears, stop and document it. Do not accept or dismiss destructive dialogs without controller/user confirmation.

## Process Samples

Use these as samples, not fabricated output:

```powershell
Get-Counter '\Processor(_Total)\% Processor Time'
Get-Process Unity,dotnet,csc,Unity.ILPP.Runner,UnityPackageManager,UnityShaderCompiler,ShaderCompiler,AssetImportWorker,MSBuild,VBCSCompiler,mcp-for-unity -ErrorAction SilentlyContinue |
  Select-Object ProcessName,Id,CPU,StartTime,Responding,Path
```

Optional read-only process details:

```powershell
Get-Process Unity -ErrorAction SilentlyContinue | Select-Object Id,MainWindowTitle,Responding,CPU,StartTime
Get-Process dotnet,csc,Unity.ILPP.Runner,UnityPackageManager,UnityShaderCompiler,ShaderCompiler,AssetImportWorker -ErrorAction SilentlyContinue |
  Sort-Object ProcessName,StartTime |
  Select-Object ProcessName,Id,CPU,StartTime,Path
```

Do not kill processes blindly. If a destructive stop, Unity restart, package-manager reset, or process termination appears necessary, write the exact process list, reason, and risk, then request user/controller confirmation. Safe retry path comes first: wait, resample, and verify idle state.

## Numbered Tasks

1. Read only the authority docs named above. Acceptance proof: list exact files read in `UNITY_OWNER_00_MCP_GATE_READINESS_<timestamp>.md`. Fallback: if a named file is missing, record `MISSING_STATIC_INPUT`; do not invent the missing rule.

2. Create a readiness report folder only under `Docs/Reports/UnitySystem_20260605/`. Acceptance proof: folder path exists. Fallback: if folder creation fails, write the final report in chat only and do not touch other directories.

3. Record the inherited gate boundary: current static evidence says CPU was low but blocked processes included `dotnet`, `mcp-for-unity`, `Unity`, `Unity.ILPP.Runner`, `UnityPackageManager`, and `UnityShaderCompiler`. Acceptance proof: quote this as static parent evidence, not fresh process truth.

4. Run the first fresh process sample with `Get-Counter` and `Get-Process`. Acceptance proof: paste concise output into the readiness report with timestamp. Fallback: if command fails, record `PROCESS_SAMPLE_FAILED` and stop before MCP or Unity access.

5. Interpret CPU gate. Acceptance proof: CPU <= 50 percent or `BLOCKED_CPU_BUSY`. Fallback: if CPU > 50 percent, wait 60 seconds, resample once, and if still high, abort without Unity/MCP readback.

6. Interpret compiler/import/package/shader gate. Acceptance proof: no active busy `dotnet`, `csc`, `Unity.ILPP.Runner`, `UnityPackageManager`, `UnityShaderCompiler`, `ShaderCompiler`, `AssetImportWorker`, `MSBuild`, or `VBCSCompiler` process related to the project. Fallback: if active, wait 60 seconds and resample up to 3 times; do not kill.

7. Interpret Unity editor gate. Acceptance proof: Unity is either closed or responsive and idle. Fallback: if Unity is non-responsive, compiling, importing, shader-compiling, package-resolving, or showing prompts, abort and request controller/user decision before restart or termination.

8. Checkpoint 1 - write `process_gate.md` or a process section in the readiness report. Acceptance proof: status is exactly one of `CLEAN_PROCESS_GATE`, `BLOCKED_PROCESS_BUSY`, `BLOCKED_CPU_BUSY`, `BLOCKED_UNITY_UNRESPONSIVE`, or `BLOCKED_UNKNOWN_PROCESS_STATE`.

9. If process gate is not clean, stop here. Acceptance proof: report says no Unity readback/proof is possible, `ASSET_OWNER_36` remains blocked, and no MCP tool mutation was attempted. Fallback: none.

10. If process gate is clean, query MCP resources exposed to Codex with the available MCP resource listing path. Acceptance proof: resource list includes Unity MCP resources such as editor state, project info, scenes, instances, or equivalent. Fallback: if resources are empty, record `BLOCKED_MCP_RESOURCES_EMPTY`.

11. Query MCP tool availability without executing mutating tools. Acceptance proof: callable Unity MCP tools are visible in the active session, especially read-oriented tools such as `read_console`, `find_gameobjects`, `manage_scene` read actions, `manage_asset` search/info actions, and `manage_camera` screenshot. Fallback: if tools are absent, record `BLOCKED_MCP_TOOLS_UNAVAILABLE`.

12. Check MCP active instance state if available. Acceptance proof: active Unity instance matches `C:\hades\Hecton8` or project identity is unambiguous. Fallback: if multiple Unity instances are exposed or project identity is ambiguous, do not select by guess; request controller/user direction.

13. Read MCP editor state resource if exposed. Acceptance proof: `ready_for_tools=true` or equivalent, `is_compiling=false`, `is_domain_reload_pending=false`, and no blocking reasons. Fallback: if editor state resource is missing or reports blocked, record exact state and abort before h8_1475 readback.

14. Read MCP project/info resource if exposed. Acceptance proof: Unity version, project path, package state, and read-only capability state are recorded. Fallback: if unavailable, record `MCP_PROJECT_INFO_UNAVAILABLE`; do not substitute process list as proof of MCP readiness.

15. Query Unity Console read-only if tools are available and editor state is ready. Acceptance proof: latest errors/warnings/logs are exported to `console_gate.txt` or report section. Fallback: if console read fails, record `MCP_CONSOLE_READ_FAILED`; no h8_1475 handoff.

16. Check package/import/shader idle state from Unity/MCP if available. Acceptance proof: no package resolution, asset import, shader compile, script compile, or domain reload. Fallback: if not exposed by MCP, keep process sample as lower evidence and label Unity internal state `PENDING_MCP_STATE_PROOF`.

17. Check dirty-state risk only through read-only editor state if exposed. Acceptance proof: no dirty scene/project/prefab/material warning is reported. Fallback: if dirty-state cannot be read without mutation, do not run readback; record `DIRTY_STATE_PROOF_ABSENT`.

18. Checkpoint 2 - write MCP gate result. Acceptance proof: status is exactly one of `CLEAN_MCP_GATE`, `BLOCKED_MCP_RESOURCES_EMPTY`, `BLOCKED_MCP_TOOLS_UNAVAILABLE`, `BLOCKED_MCP_EDITOR_NOT_READY`, `BLOCKED_MCP_PROJECT_AMBIGUOUS`, or `BLOCKED_MCP_STATE_PROOF_ABSENT`.

19. If MCP gate is not clean, stop here. Acceptance proof: report explicitly states no Unity readback/proof is possible without clean process gate and exposed MCP resources/tools. Fallback: request user/controller confirmation only for any needed restart/termination; do not perform it.

20. If both gates are clean, run one no-mutation smoke read only: read editor state and console state again after a 10-second delay. Acceptance proof: both samples remain clean and stable. Fallback: if state changes, downgrade to `BLOCKED_UNSTABLE_TOOLING_GATE`.

21. Write `UNITY_OWNER_00_MCP_GATE_HANDOFF_<timestamp>.md`. Acceptance proof: include process samples, MCP resource list summary, tool availability summary, editor readiness state, console state, dirty-state proof status, abort flags, and exact next owner handoff state.

22. Create no screenshots unless tooling proof requires a no-mutation MCP screenshot and the editor state is clean. Acceptance proof if used: screenshot path is under `Docs/Screenshots/HectonProofPackets/mcp_gate_<timestamp>/`; no file under `Assets/`. Fallback: omit screenshots and state screenshot proof not needed for gate readiness.

23. Decide handoff. Acceptance proof: handoff to `ASSET_OWNER_36` is allowed only if process gate, MCP resource/tool gate, editor state, console read, package/import/shader idle state, and dirty-state risk are clean or explicitly read-only proven. Fallback: keep `ASSET_OWNER_36` blocked.

24. Final report in chat and report file must use evidence classes. Acceptance proof: every claim is labeled `STATIC_DOC`, `LOCAL_PROCESS`, `MCP_RESOURCE`, `MCP_TOOL`, `UNITY_CONSOLE`, or `PENDING_VERIFICATION`. Fallback: rewrite report before handoff if any claim says runtime/visual/profiler/GC proof without artifact.

25. Do not continue into `h8_1475`. Acceptance proof: final report says this packet proves only tooling readiness or blocker state. `h8_1475` execution remains a separate no-mutation proof packet owned by `ASSET_OWNER_36` or a controller-assigned replacement owner.

## Abort Rules

Abort immediately if any of the following occur:

- CPU remains above 50 percent after retry.
- Any compiler/import/package/shader process remains active after retry.
- Unity is non-responsive.
- Unity asks to save, repair, import, recompile, update packages, or apply settings.
- MCP resources are empty.
- MCP tools are unavailable.
- MCP active project/instance cannot be proven as `C:\hades\Hecton8`.
- MCP editor state says not ready for tools.
- Dirty-state proof is absent or dirty state is detected.
- Console read fails after MCP says ready.
- Any mutation would be required to make the gate look clean.

Abort report must state the last safe step, exact blocker, process/tool evidence, and required controller/user decision if destructive stop/restart is proposed.

## Proof Artifacts

Minimum successful readiness artifacts:

- `Docs/Reports/UnitySystem_20260605/UNITY_OWNER_00_MCP_GATE_READINESS_<timestamp>.md`
- process sample section or `process_gate.md`
- MCP resources summary
- MCP tools summary
- editor state summary
- console state export or summary
- dirty-state proof status
- handoff decision section

Minimum blocked readiness artifacts:

- `Docs/Reports/UnitySystem_20260605/UNITY_OWNER_00_MCP_GATE_BLOCKED_<timestamp>.md`
- exact blocker
- last safe step
- process samples if available
- MCP resource/tool state if reached
- user/controller confirmation needed if destructive restart/stop is requested

## Handoff To ASSET_OWNER_36

Only hand off when all are true:

- `CLEAN_PROCESS_GATE`
- `CLEAN_MCP_GATE`
- Unity editor state ready for tools
- no compile/import/shader/package activity
- console readable through MCP
- dirty-state risk read-only checked and clean
- active Unity instance/project path proven as `C:\hades\Hecton8`
- no mutation performed

Handoff text:

`ASSET_OWNER_36 may begin the h8_1475 no-mutation proof packet. Tooling gate is clean as of <timestamp>. This is not h8_1475 proof. It only proves process/MCP readiness. Re-run process gate before ASSET_OWNER_36 touches Unity.`

If any condition is missing:

`ASSET_OWNER_36 remains BLOCKED. No Unity readback/proof is possible without clean process gate and exposed MCP resources/tools.`

## Regression Model

- CPU: local process sampling only; no runtime CPU change.
- GC: no runtime code changed; no GC claim.
- Memory/VRAM: no Unity or player memory proof; no residency claim.
- Cadence: no dispatcher, simulation, visual sync, or tooling cadence changed.
- Correctness: packet prevents false `h8_1475` readback under red process/MCP gate.

Final packet status: `PENDING VERIFICATION` until a future Unity/tooling owner executes the gate and writes proof artifacts.
