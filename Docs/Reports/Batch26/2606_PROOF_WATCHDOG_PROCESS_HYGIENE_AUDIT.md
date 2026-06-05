# 2606 Proof Watchdog Process Hygiene Audit

Status: STATIC WATCHDOG AUDIT / PROCESS SNAPSHOT / LOG-TAIL REJECT
Agent: 2606
Date: 2026-06-04
Workspace: `C:\hades\Hecton8`
Evidence class: STATIC_DOC + STATIC_FILESYSTEM + STATIC_LOG_TAIL + PROCESS_SNAPSHOT

## Boundary

No Unity Editor control, no Play Mode, no screenshots, no dotnet build, no process kill, no cleanup, no asset edit, no code edit.

Write scope was this file only.

## Authority Read

- `AGENTS.md`
- `performance.md`
- `presentation.md`
- `quality.md`
- `Docs/Reports/Batch25/2505_VISUAL_PROOF_WATCHDOG_GATE.md`
- `Docs/Reports/Batch25/BATCH25_SYNTHESIS_FOR_UNITY_OWNER.md`

Mandates loaded:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`

`Docs/Actual Domains of Project.txt`: missing. Narrow domain inferred: process/log/proof hygiene for visual proof packets.

## Current Verdict

Current proof state: REJECTED VISUAL PROOF.

Reasons:

- `1474` has six screenshot files, but no manifest/checksum/camera/depth/quality/log metadata file was found in `Docs/Screenshots/MCP`.
- No `1475` screenshot or manifest file was found.
- The copied Unity proof log is dirty.
- The live Unity `Editor.log` is dirty.
- Current Unity session started after the copied `1474b` proof log, so `1474b` is not the latest live session log.
- Active Unity/MCP helper processes exist. No build/proof run should be started from this audit.

This audit does not judge the visual content of the screenshots. The packet fails before taste review.

## Objective Clock

- Audit clock: `2026-06-04 20:54:10 +04:00`
- CPU snapshot at audit: `Intel i5-1135G7`, 4 cores / 8 logical processors, WMI `LoadPercentage=100`
- Later 2-second process sample: Unity PID `11440` consumed `0.562` CPU seconds, approximate total CPU `3.5%`

CPU was not stable enough to justify launching a build or proof capture. No build was launched.

## Screenshot Evidence

Active proof directory: `Docs/Screenshots/MCP`

Latest files:

| File | Last write | Bytes | SHA256 |
|---|---:|---:|---|
| `h8_1474_surface_coast_aegir_ui_off.png` | 2026-06-04 19:25:09 | 676489 | `047F62921B4024DB7F064808EE4C321C195C468782FB4081ECE9EADEEC9624A2` |
| `h8_1474_shoreline_close_1m.png` | 2026-06-04 19:25:14 | 677531 | `E94584D4C360865B44E47C6F735C796C494A42B41130D12F1952F618D3654A63` |
| `h8_1474_underwater_0_5m.png` | 2026-06-04 19:25:18 | 670798 | `ED187CC7E54D7FFF83FE705DFC434DE3AFA2EC9EF350972A555200975C8677D2` |
| `h8_1474_underwater_20_50m_route.png` | 2026-06-04 19:25:22 | 676153 | `677AD0764F86DE55B6FAC108EC1B56A34AEC1A68B71BC3A1B3746225E8D3F36F` |
| `h8_1474_aegir_celestial_long.png` | 2026-06-04 19:25:27 | 670893 | `993AE5E551F2038B12A9908AE0B5D157B470336660DE1E2392657FF9339BCEFB` |
| `h8_1474_regression_low_oblique.png` | 2026-06-04 19:25:31 | 675808 | `47029C89FDCD53794848960C64DC6660FC4A65D4B1ECB70A5AE6603C5B53B742` |

Other `1474` diagnostic screenshots:

- `h8_1474_diag_surface_from_mcp.png`, 2026-06-04 18:43:40, 239210 bytes
- `h8_1474_diag_shore_foam_from_mcp.png`, 2026-06-04 18:43:42, 210813 bytes
- `h8_1474_diag_underwater_route_from_mcp.png`, 2026-06-04 18:43:43, 304493 bytes

Manifest check:

- `Docs/Screenshots/MCP`: no `h8_1474*manifest*`, no `h8_1475*`, no `*manifest*` match found.

Packet reject codes:

- `MISSING_METADATA`
- `NO_1475_PACKET`
- `DIRTY_LOG`
- `PROCESS_CONTEXT_NOT_CLEAN`

## Screenshot Storage Hygiene

Directory snapshot:

| Path | File count | Latest file | Latest write |
|---|---:|---|---:|
| `Assets/Screenshots` | 0 | none | none |
| `Docs/Screenshots` | 1097 recursive | `Docs/Screenshots/MCP/h8_1474_regression_low_oblique.png` | 2026-06-04 19:25:31 |
| `Docs/Screenshots/MCP` | 227 | `h8_1474_regression_low_oblique.png` | 2026-06-04 19:25:31 |
| `Docs/Orchestration/Captures` | 217 | `steer_1474_false_views_sent_actual.png` | 2026-06-04 19:34:32 |
| `Docs/GeneratedAssets` | 58 | `Gemini/README_GENERATION_QUEUE_20260604.md` | 2026-06-04 17:38:08 |
| `MarketingAssets/01_Screenshots` | 0 | none | none |
| `MarketingAssets/02_Video/CaptureRaw` | 0 | none | none |
| `MarketingAssets/03_Steam/Screenshots` | 0 | none | none |
| `MarketingAssets/04_Presskit/Screenshots` | 0 | none | none |
| `MemoryCaptures` | 0 | none | none |

Active `Assets/Screenshots` import-loop evidence: none. Directory exists but is empty.

Current `Assets` screenshot/proof scan:

- No active `Assets` file matched screenshot/capture/MCP/proof/diag/`h8_14xx` image naming.
- `Assets/_Project/Art/Materials/RuntimeVisualProof` exists with material/meta files, not screenshots.

Archived junk/non-fresh evidence:

- `Docs/_Archive/WorkspaceHygiene_1331/Assets/Assets__Screenshots__*.png` contains historical screenshots moved out of `Assets`. These are archive records only and must not be used as fresh proof.

## Unity Log Evidence

Copied proof log:

`Docs/AgentLogs/UnityEditor_visual_audit_restart_1474b.log`

- Last write: 2026-06-04 20:10:02
- Size: 1,118,757 bytes
- Newer than final `1474` screenshot at 19:25:31: yes
- Clean: no

Reject token counts in copied proof log:

| Token | Count |
|---|---:|
| `Error` | 9 |
| `Exception` | 87 |
| `Warning` | 23 |
| `LogError` | 4 |
| `Found 1 leak` | 88 |
| `Leak Detected` | 4 |
| `not valid. Loading of assembly skipped` | 1037 |
| `CompileScripts` | 12 |
| `Asset Pipeline Refresh` | 50 |
| `H8_PLAYMODE_EXIT` | 0 |
| `forced` | 15 |
| `Access token is unavailable` | 1 |

Tail reject signals:

- Lines `12318` through `12665`: repeated `Found 1 leak(s) from callstack`.
- Lines `12685` through `12745`: repeated `not valid. Loading of assembly skipped`.

Live Unity Editor log:

`C:\Users\danat\AppData\Local\Unity\Editor\Editor.log`

- Last write: 2026-06-04 20:52:56
- Size: 119,216 bytes
- Stable over a 2-second recheck: yes
- Clean: no

Reject token counts in live Editor log:

| Token | Count |
|---|---:|
| `Error` | 17 |
| `Exception` | 71 |
| `Warning` | 3 |
| `LogError` | 8 |
| `Found 1 leak` | 0 |
| `Leak Detected` | 0 |
| `not valid. Loading of assembly skipped` | 61 |
| `CompileScripts` | 2 |
| `Asset Pipeline Refresh` | 3 |
| `H8_PLAYMODE_EXIT` | 0 |
| `forced` | 0 |
| `Access token is unavailable` | 1 |

Live log tail reject signals:

- Line `970`: MCP WebSocket connection failed.
- Line `976`: `UnityEngine.Debug:LogError`.
- Line `1036`: failed to start MCP transport.
- Line `1042`: `UnityEngine.Debug:LogWarning`.
- Lines `1100`, `1166`, `1297`: repeated MCP WebSocket connection failures.
- Lines `1106`, `1172`, `1303`: repeated `UnityEngine.Debug:LogError`.
- Lines `988-1027`, `1118-1157`, `1184-1223`, `1315-1354`: task/socket exception paths.

MCP bridge later connected to `http://127.0.0.1:8088`, but later connection does not erase earlier errors in the same log session. The log remains dirty for proof acceptance.

## Process Evidence

Observed project Unity process:

| Process | PID | Start time | Path / command |
|---|---:|---:|---|
| `Unity.exe` | 11440 | 2026-06-04 20:50:59 | `C:\Program Files\Unity\Hub\Editor\6000.4.1f1\Editor\Unity.exe -projectPath C:\hades\Hecton8` |
| `uvx.exe` | 11884 | 2026-06-04 20:52:45 | `C:\Users\danat\.local\bin\uvx.exe` |
| `python.exe` | 5780 | 2026-06-04 20:52:46 | `C:\Users\danat\AppData\Local\Programs\Python\Python313\python.exe` |
| `python.exe` | 14256 | 2026-06-04 20:52:46 | `C:\Users\danat\AppData\Local\uv\cache\archive-v0\RJ_chQxyWZNEJKFu2TSbF\Scripts\python.exe` |

Compiler/build process state:

- `dotnet`: not observed.
- `csc`: not observed.
- `MSBuild`: not observed.
- `VBCSCompiler`: not observed.

Log import/compile state:

- Copied proof log contains `CompileScripts=12` and `Asset Pipeline Refresh=50`.
- Live Editor log contains `CompileScripts=2` and `Asset Pipeline Refresh=3`.

Therefore: no active compiler process was sampled, but both relevant Unity logs contain compile/import history. This is dirty until a new post-capture log window proves quiet state.

## Required Clean-Log Window

Future visual packets need a clean log window, not just a screenshot set.

Minimum clean window:

1. Start from a Unity session whose log has no unresolved compile/import/error storm before capture.
2. Wait until all import, domain reload, ILPP, compile, and MCP startup noise is complete.
3. Capture all required screenshots from one route state and one continuous `GlobalQualityWeight` value.
4. After final screenshot, wait at least 60 seconds with the log file stable.
5. The accepted window is from the last pre-capture quiet marker through 60 seconds after the final screenshot.
6. The log `LastWriteTime` must be newer than the final screenshot.
7. The log tail must cover the capture and post-capture closure.

Forbidden tokens in the clean window:

- `Error`
- `Exception`
- `Warning`
- `LogError`
- `Found 1 leak`
- `Leak Detected`
- `shader error`
- `material error`
- `not valid. Loading of assembly skipped`
- `CompileScripts`
- `Asset Pipeline Refresh`
- `H8_PLAYMODE_EXIT`
- `forced`
- `Access token is unavailable`
- MCP WebSocket connection failure
- MCP transport startup failure

Any one token rejects the packet unless the manifest isolates it outside the capture window and a later clean post-capture tail proves stability.

## Future Packet Acceptance Checklist

Reject immediately if any item fails:

- Six required views are not present.
- Any required screenshot is outside `Docs/Screenshots/MCP` or another approved `Docs` proof directory.
- Any screenshot is written under `Assets`.
- Packet lacks a manifest.
- Manifest lacks SHA256, file size, NTFS timestamp, local/UTC timestamp, scene, route state, camera transform/FOV, depth band, capture source, UI state, `GlobalQualityWeight`, render scale, post stack, underwater/foam/caustic/fog/water state, harness version, and log path.
- Filename quality label is binary or stale instead of continuous `qNNN` from `GlobalQualityWeight`.
- Log path is missing, stale, older than final screenshot, or still moving.
- Log clean window is shorter than 60 seconds after final screenshot.
- Log contains any reject token in the clean window.
- Any compile/import/domain reload/process startup event overlaps the capture window.
- `dotnet`, `csc`, `MSBuild`, `VBCSCompiler`, or high CPU/import activity is active during capture.
- Screenshot set reuses diagnostic files or older packet images.
- Any required view is visually false-labeled.
- Surface/shore/photic/medium-depth/Aegir proof is dark, muddy, empty, flat, hidden by fog, or below the Subnautica-level floor.
- Shoreline lacks organic foam/wet contact proof.
- Lit shallow/underwater receivers lack justified caustic response.
- Underwater route lacks near/mid/far structure, substrate, biota or scale cue, and return/readability cue.
- Any hard slab, plane, curtain, service cube, waterline cut, ceiling lid, or occlusion helper is visible.

Accept only if all items pass:

- Six same-session screenshots exist.
- One manifest covers all six screenshots and matches hashes/timestamps/sizes.
- One clean log is newer than the final screenshot and stable for at least 60 seconds.
- No screenshot import-loop evidence exists under `Assets`.
- No compiler/import/process-noise evidence exists during the capture window.
- Visual judge form passes every view with no reject code.
- Compact/Low readability is preserved.
- Middle looks genuinely good, not merely functional.
- High spends extra budget on richer water, material, sky, caustic, silt, and route detail without changing truth.
- Ultra adds sensory density only; it does not hide unresolved geometry, stale logs, or runtime faults.

## Final Watchdog Sentence

The current packet remains rejected. Future packets are accepted only when screenshot completeness, manifest integrity, process quiet state, stable clean log tail, and visual gates all pass from the same capture session.
