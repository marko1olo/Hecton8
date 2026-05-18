<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# 2026-05-07 Live Churn Continuation Sync
Date: 2026-05-07
Status: PENDING VERIFICATION (BLOCKED BY MCP)
Scope: top-layer documentation synchronization while source and same-day reports are being modified concurrently.

## Mandates Followed

- `.agents-skills/PROJECT_LTS_Compatibility_Layer.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`

## Why This Exists

Earlier May 7 reports and indexes were being edited while the source tree was still changing.
This report supersedes same-day numeric counters where they conflict.

Do not use any same-day line-count claim as stable unless it is newer than this report and has its own stability window.

## Current Filesystem Snapshot

Snapshot time: `2026-05-07`, local workspace scan during active source churn.

| Surface | Count |
|---|---:|
| `Docs/**/*.md` after build-master documentation refresh | `443` |
| active markdown excluding archive/deprecated/report-deprecated/obsolete after build-master documentation refresh | `230` |
| active non-report markdown | `163` |
| direct `Docs/Reports/*.md` after build-master documentation refresh | `67` |
| non-meta files under `Docs` after final inquisition report | `871` |
| active non-obsolete `Docs/**/*.md` header debt | `0` |
| repository-root `.md` files | `5` |
| repository-root `.txt` files | `0` |
| repository-root `.log` files | `0` |

## Current Source Snapshot

| Surface | Count |
|---|---:|
| `Assets/_Project/**/*.cs` | `1233` |
| `Assets/_Project/Scripts/**/*.cs` | `1192` |
| `Assets/_Project/**/*.cs` physical lines | `683064` |
| `Assets/_Project/Scripts/**/*.cs` physical lines | `667771` |
| `GlobalRegistryContracts.cs` direct public interfaces | `39` |

Latest observed source write before this report:

- source file count and line count changed again during churn recheck
- latest observed source count: `1233` / `1192`
- latest observed source line count: `683064` / `667771`
- latest observed source write during this continuation: `Assets/_Project/Scripts/Core/InputDispatcher.cs` at `2026-05-07 19:02:39 +04:00`
- latest manifest source-count snapshot: `2026-05-07 19:03:28 +04:00`
- latest repeated source-count window before the manifest held unchanged from `18:38:44` to `18:39:32` at `1233` project files, `1192` script files, `678994` project physical lines, `663701` script physical lines, and `570338` `Measure-Object -Line` script lines.
- build-master documentation refresh later observed `683064` project physical lines, `667771` script physical lines, and `573698` `Measure-Object -Line` script lines; it supersedes the earlier stable window for numeric orientation.
- source churn continued through this documentation window; this report stops chasing additional unrequested source writes after the `19:03:28 +04:00` manifest snapshot.

Source churn is active. These line counts are restamped as the latest filesystem snapshot, not a stability proof.

## Build Evidence Boundary

`CodexArtifacts/2026-05-07_BUILD_MASTER_CORE_BUILD.log` reports:

- `Build FAILED.`
- `55 Warning(s)`
- `2 Error(s)`
- active blockers: `HectonVoxelEngine.cs(4143,47)` missing `GlobalRegistry.PlayerRigidbody`; `HectonVoxelEngine.cs(4144,62)` missing `GlobalRegistry.PlayerMovement`

Older successful same-day logs predate later source writes observed in this continuation. They are scoped historical Core evidence only.
It is not current whole-project build proof, Play Mode proof, GCMonitor proof, profiler proof, player-build proof, memory-retention proof, scene/prefab proof, or package-upgrade proof.

Latest local compile check after the repair/native-sentinel/doc continuation:

- command: `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies -v:minimal /nr:false /p:UseSharedCompilation=false`
- log: `CodexArtifacts/2026-05-07_CONTINUATION_CORE_NODEPS_BUILD.log`
- result: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, exit code `0`
- boundary: this is `Hecton8.Core.csproj` only with `--no-dependencies`; the attempted full dependency build was aborted after tool timeout and is not success evidence.

Latest MCP verification attempts:

- `refresh_unity(mode=if_dirty, scope=scripts, compile=request, wait_for_ready=true)` returned timeout after `60s`.
- `read_console(types=error,warning)` returned `Unity session not ready` / `ping not answered`.
- post-build `read_console(types=error,warning)` retry again returned `Unity session not ready` / `ping not answered`.
- status remains `PENDING VERIFICATION`; no clean Unity console claim is allowed from this attempt.

## External Source Boundary

Official Unity source checks were rerun during this continuation:

- local project pin: Unity `6000.4.1f1`
- newer official same-line release observed: Unity `6000.4.5f1`
- official Addressables package page still resolves `com.unity.addressables@2.7` to released package version `2.7.6`
- no Unity or package upgrade was performed

Under `PROJECT_LTS_Compatibility_Layer`, release drift is not an upgrade instruction. Upgrade requires a migration branch, compile dry-run, warning catalog, adapter review, CI/perf gates, and regression proof.

Official URLs checked:

- `https://unity.com/releases/editor/whats-new/6000.4.5f1`
- `https://docs.unity3d.com/Packages/com.unity.addressables@2.7/manual/index.html`

## Current Blockers / Non-Claims

- `Assets/_Project/Scripts/SavePredictivePagingMath.cs` is still absent.
- May 6 Grand Purge reports remain scoped source/pattern/diff evidence only.
- Source is changing; exact line counts are volatile.
- Do not claim documentation is permanently synchronized. This report is the latest observed state, not a daemon.
- Do not claim `ARCHIVE SYNCHRONIZED VERIFIED`.
- Do not claim runtime readiness from this pass.

## Regression Model

CPU: documentation-only edits. No runtime code path changed here.

GC: no gameplay code changed here. Measured `0 B/frame` proof absent.

Memory: no scenes, prefabs, textures, Addressables groups, native containers, render textures, project settings, or packages changed here.

Cadence: no tick, bootstrap, dispatcher, scene transition, asset loading, physics, UI, or rendering cadence changed here.

Correctness: top-level documentation now records that same-day source counts are volatile and that older May 7 build logs do not prove later source writes.

Status: PENDING VERIFICATION (BLOCKED BY MCP)
