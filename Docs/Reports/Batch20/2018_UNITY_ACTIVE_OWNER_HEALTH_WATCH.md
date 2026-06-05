# Unity Active Owner Health Watch - ID 2018

Date: 2026-06-04 14:30:15 +04:00
Scope: read-only process/log inspection. No Unity MCP calls. No Unity commands. No build. No scene/asset/script edits.

## Evidence Sources

| Evidence | Artifact | State |
|---|---|---|
| Process command lines | `Win32_Process` query at 2026-06-04 14:29-14:30 +04:00 | STATIC_PROCESS |
| Main redirected Unity log | `Docs/AgentLogs/UnityEditor_visual_audit_restart.log`, last write 2026-06-04 14:24:23 | STATIC_LOG |
| Unity default editor log | `%LOCALAPPDATA%/Unity/Editor/Editor.log`, last write 2026-06-04 11:01:54 | STATIC_LOG, stale relative to active redirected log |
| Asset import worker logs | `Logs/AssetImportWorker0.log`, `Logs/AssetImportWorker1.log`, last write 2026-06-04 11:51:04 | STATIC_LOG |
| Project churn | recent file mtimes under `Assets`, `Docs`, `Tools`, excluding `Library/Temp/Logs` | STATIC_FILE |
| MCP run state | `Library/MCPForUnity/RunState/mcp_http_8088.pid`, last write 2026-06-04 11:34:09 | STATIC_FILE |

## Current Unity Process State

- Main Unity editor is running: PID 52264, command line `Unity.exe -projectPath C:\hades\Hecton8 -logFile C:\hades\Hecton8\Docs\AgentLogs\UnityEditor_visual_audit_restart.log`.
- Two AssetImportWorker Unity processes are running: PID 19376 `AssetImportWorker0`, PID 12164 `AssetImportWorker1`, both launched 2026-06-04 11:34:08 with project path `C:/hades/Hecton8`.
- Shader compiler helpers are still resident: eight `UnityShaderCompiler.exe` processes, started between 11:32:53 and 12:00:34.
- Eight `UnwrapCL.exe` helpers are still resident, started around 11:33:02-11:33:04.
- `Unity.ILPP.Runner.exe` is still resident, started 11:28:30.
- MCP server stack is running: `cmd.exe` terminal, `uvx/uv`, `mcp-for-unity.exe`, and two MCP python wrapper processes. I did not call MCP.
- No `dotnet.exe`, `csc.exe`, `MSBuild.exe`, or Bee backend process was present in the process list during inspection. The latest visible compile activity is log evidence, not a live process.

## Active Imports And Churn

Evidence does not prove a current hang. It proves heavy recent import/compile churn and one import-worker transport failure.

- Redirected Unity log contains 814 `Start importing` lines and 206 `Asset Pipeline Refresh` lines.
- The latest visible redirected-log import loop repeatedly touched `Assets/_Project/Scenes/02_HECTON_WORLD.unity` and `Assets/_Project/Art/Materials/World/Photic1428/*`.
- Recent file mtimes confirm visual/material work around Photic1428 and screenshots: `02_HECTON_WORLD.unity`, `MAT_H8_RubbleStrata_1448.mat`, `MAT_H8_SurfaceFoamBlob_1447.mat`, `H8_RubbleStrata_1448.shader`, generated Gemini wet-basalt QA/refinement outputs, and MCP screenshots.
- Default `Editor.log` shows repeated imports of `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` near its tail. This matters because Data Monolith import churn can retrigger postprocessors and compile/import loops.
- `Library/ArtifactDB` last write is 2026-06-04 14:11:17 and `Temp/FSTimeGet-*` last write is 13:30:32. This is recent filesystem activity, but not proof of a current import job.
- Asset worker logs last wrote at 11:51:04, while worker processes remain alive. This is suspicious only if the editor is expected to be idle; do not kill workers without a fresh editor-state check by the active owner.

## Repeated Warnings And Errors

Highest-risk findings:

1. `Unexpected transport error from import worker 0 (possible crash). code=10054`.
   - Artifact: `Docs/AgentLogs/UnityEditor_visual_audit_restart.log`, line 175399.
   - Evidence class: STATIC_LOG.
   - Interpretation: import worker 0 connection was forcibly closed after a script compilation cycle. This is a real warning. It is not proof the main editor is dead.

2. Script compilation churn around `Hecton8.Core.dll (+2 others)`.
   - Artifact: `Docs/AgentLogs/UnityEditor_visual_audit_restart.log`, lines 175376-175396.
   - Evidence class: STATIC_LOG.
   - Detail: `[ScriptCompilation] Requested script compilation...`, Bee build `ExitCode: 4 Duration: 1m:36s`, then `ExitCode: 0 Duration: 14s`; default `Editor.log` also contains repeated `ExitCode: 4` and `ExitCode: 0` entries.
   - Interpretation: compilation retried/re-ran. No live compiler process was present during inspection, so do not start another build blindly.

3. Repeated render texture lifecycle warning from MCP dynamic capture code.
   - Artifact: `Docs/AgentLogs/UnityEditor_visual_audit_restart.log`, lines 19977, 20001, 20034, 20058, 20136, 20160, 20228.
   - Evidence class: STATIC_LOG.
   - Detail: `Releasing render texture that is set as Camera.targetTexture!` with stack through `MCPDynamicCode` and `MCPForUnity.Editor.Tools.ExecuteCode`.
   - Interpretation: previous MCP screenshot/capture code destroyed a target texture before detaching it. This is owner-action debt for the MCP/capture agent, not a reason to interrupt asset import unless it repeats during new captures.

4. MCP session warning.
   - Artifact: `Docs/AgentLogs/UnityEditor_visual_audit_restart.log`, line 175362.
   - Evidence class: STATIC_LOG.
   - Detail: `MCP-FOR-UNITY: Server no longer running; ending orphaned session.`
   - Interpretation: stale MCP session cleanup occurred before the later MCP server process started. Current process list shows MCP server stack running.

5. Repeated shader unsupported warnings in worker logs.
   - Artifacts: `Logs/AssetImportWorker0.log`, `Logs/AssetImportWorker1.log`.
   - Evidence class: STATIC_LOG.
   - Detail: `Hidden/Simulation/Synthetic Lit`, `Simulation/Room X-Ray`, `Simulation/Standard Lit`, `Simulation/URP/Room X-Ray`, `Simulation/URP/Lit`, `Hidden/Simulation/URP/Synthetic` all have all subshaders removed.
   - Interpretation: repeated package/simulation shader noise during import worker domain reload. It can hide real shader failures; shader owner should triage if these shaders are expected to render.

6. Package Manager warning is expected for AssetImportWorker command line.
   - Artifact: worker logs.
   - Evidence class: STATIC_LOG + STATIC_PROCESS.
   - Detail: workers launched with `-noUpm`; logs report Package Manager cannot connect.
   - Interpretation: expected worker noise. Do not steer on this alone.

7. `Attempted to call .Dispose on an already disposed CancellationTokenSource`.
   - Artifact: `Docs/AgentLogs/UnityEditor_visual_audit_restart.log`, line 20346.
   - Evidence class: STATIC_LOG.
   - Interpretation: isolated lifecycle warning near Android ADB scan entries. Track if repeated; not enough evidence for a halt.

## Suspected Owner Actions

- Active visual/material owner likely edited Photic1428 materials/shader and `02_HECTON_WORLD.unity`, then triggered repeated imports and MCP screenshots. Evidence: recent project file mtimes and redirected Unity log import paths.
- Batch20 agent 2017 produced texture pipeline adversarial review outputs and recent MCP screenshots. Evidence: `Docs/AgentLogs/LOG_2017.md`, `Status_2017.md`, `Rationale_2017.md`, `Docs/Reports/Batch20/2017_*`, and screenshot mtimes in the last 4 hours.
- Earlier Batch20 agents 2015 and 2016 produced survival/material debt reports before the latest visual churn. Evidence: `Docs/Reports/Batch20/2015_*`, `2016_*`, and their logs/status/rationale.
- MCP capture/code execution was used by some active owner. Evidence: repeated `MCPDynamicCode` stacks in the redirected Unity log and MCP screenshot artifacts.
- Data Monolith file imports occurred in the default Editor log. Evidence: repeated `static_data.h8bin` import lines. This may be from data bake/watchers or asset database refresh, not enough to assign an owner by itself.

## Steer Vs Wait

Decision: STEER LIGHTLY, DO NOT INTERRUPT IMPORT HELPERS YET.

Reasons:
- There is a real import-worker transport error and repeated compile/import churn.
- There is no live `csc`, `dotnet`, `MSBuild`, or Bee process in the observed process list.
- Main Unity and worker processes are responding.
- The worker logs are stale after 11:51:04, but the redirected main log last wrote at 14:24:23. Stale worker log does not prove a hang.
- Recent active work is visual/material/MCP screenshot oriented; interrupting Unity can destroy context or force more import churn.

Steer now:
- Tell active visual/MCP owners to stop issuing new MCP dynamic capture commands until the current editor/import state is inspected by the owner with Unity-side state.
- Tell owners not to trigger builds or new script edits until the last `Unexpected transport error from import worker 0` is acknowledged.
- Ask the owner of Photic1428/scene edits to batch material/scene writes instead of small repeated saves.
- Ask the data/import owner to check why `static_data.h8bin` is imported repeatedly if it is still happening.

Wait:
- Do not kill Unity, AssetImportWorker, ShaderCompiler, UnwrapCL, ILPP, or MCP server processes from this watcher.
- Do not delete `Library`, `Temp`, `.pid`, worker logs, or generated artifacts.
- Do not launch `dotnet build`, Unity tests, or Unity MCP tools from this watcher.

Escalate only if fresh evidence shows:
- Main Unity PID stops responding.
- Editor redirected log stops advancing while CPU stays high and helper processes remain stuck for a measured interval.
- A new compile attempt starts and fails with concrete `CS####` errors.
- AssetImportWorker transport errors repeat after a fresh editor restart or fresh asset import.

## What Not To Interrupt

- Ongoing Unity editor session PID 52264.
- AssetImportWorker0/1 until active owner verifies they are stale from inside Unity or by fresh process/log delta.
- Shader compiler and UnwrapCL helpers while Unity remains alive.
- MCP server stack unless the active MCP owner confirms no task is using it.
- Photic1428 material/scene import sequence unless it continues to retrigger after owners stop writing files.

## Residual Risk

- This report is not Unity-console verification. It is static log/process/file evidence only.
- Logs have mixed recency: redirected editor log is current, default Editor log is stale, worker logs are stale relative to redirected log.
- No runtime/profiler/playmode health claim is made.
- Low/Middle/High/Ultra consequence: no runtime scalability change. Operationally, repeated import/compile churn burns all hardware lanes equally and blocks validation; high-end machines only hide the pain, they do not remove the owner-process defect.
