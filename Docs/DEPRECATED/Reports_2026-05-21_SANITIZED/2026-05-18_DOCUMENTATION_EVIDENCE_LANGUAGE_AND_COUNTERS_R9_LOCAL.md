<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# 2026-05-18 Documentation Evidence Language And Counters R9 Local

Date: 2026-05-18
Status: LOCAL_ONLY STATIC_DOC / STATIC_SOURCE / PY_TOOL; ATLASCHECK FAIL; RUNTIME PROOF ABSENT

## Scope

R9 continued `DOC_GLOBAL_DOCS_REFRESH` after the user repeated the demand to update documentation interiors, not only indexes.

`Docs/Tasks/CURRENT_BATCH.md` currently has no `<AGENT_PROMPT id="DOC_GLOBAL_DOCS_REFRESH">` tag and no `<POLISH_MANDATE>` tag. This pass therefore continues the already-open `DOC_GLOBAL_DOCS_REFRESH` status/rationale/log trail instead of inventing a new batch prompt.

## What Was Wrong

- Active docs still used `SOURCE VERIFIED`, `STATIC DESIGN VERIFIED`, `OFFLINE SIM VERIFIED`, and similar wording without Unity runtime artifacts.
- Several active docs still pointed at May 15 Core/H-Phi artifacts under `Docs/AgentLogs/...`, but those artifacts currently exist under `Docs/Archive/Batch007/AgentLogs/...` or `Docs/Archive/Batch006/AgentLogs/...`.
- R8 source counters were already stale under concurrent source writes.
- `Docs/PROJECT_ATLAS.md` still stated the R6 `95` first-party asmdef count.
- `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` and `Docs/Modding/Future_Command_Kernel_Reservations.md` lacked the R4 interior actuality boundary.
- R7/R8 reports could be misread as current live atlas snapshots instead of historical local-pass evidence.

## Current Static Source Snapshot

Command class: `rg --files`, `rg -c '^'`, and source grep.

Evidence class: `STATIC_SOURCE / FILESYSTEM`.

| Surface | R9 value |
|---|---:|
| `Assets/_Project/**/*.cs` | 1729 |
| `Assets/_Project/Scripts/**/*.cs` | 1676 |
| first-party non-test C# files | 1712 |
| project C# physical lines by `rg -c '^'` | 1127320 |
| script C# physical lines by `rg -c '^'` | 1108505 |
| non-test C# physical lines by `rg -c '^'` | 1123322 |
| interface declaration hits under `Assets/_Project` using the R8-compatible `I*` interface regex | 267 |
| direct public interfaces in `GlobalRegistryContracts.cs` | 63 |
| first-party asmdefs under `Assets/_Project` | 106 |

These are volatile static-source counters. They are not compile, Unity import, runtime, profiler, GC, player-build, or visual proof.

## Live Atlas Snapshot

`python Tools/BuildArchitectureAtlas.py` regenerated the live atlas during R9.

| Atlas field | R9 value |
|---|---:|
| generated | `2026-05-18 01:28:18` |
| C# source files scanned under `Assets/` and `Packages/` | 4993 |
| C# line count scanned under `Assets/` and `Packages/` | 1781793 |
| first-party source files under `Assets/_Project/Scripts/` | 1675 |
| first-party source line count by atlas cache method | 1109765 |
| asmdefs scanned | 166 |
| first-party asmdefs under `Assets/_Project` | 106 |
| signal union count | 225 |
| queue lane count | 56 |

Atlas source-file count differs from the stable `rg --files Assets/_Project/Scripts -g '*.cs'` count because the atlas generator filters and classifies project roots differently. Atlas line counts use its byte/newline method; stable authority counters use the R8-compatible `rg -c '^'` method for continuity.

## Active Documentation Corrections

Updated volatile source-counter and asmdef interiors:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/PROJECT_STATE_STATIC_XRAY.md`
- `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`
- `Docs/PROJECT_ATLAS.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/README.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/INTERFACE_STRATEGY.md`

Updated evidence-path and absent-artifact language:

- `Docs/PROJECT_STATE_STATIC_XRAY.md`
- `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`
- `Docs/ARCHITECTURE/HECTON_PHI_STATIC_METRIC.md`
- `Docs/QUALITY_GATES.md`

Updated unsafe evidence language:

- `Docs/ARCHITECTURE/ARENA_ALLOCATOR_2_0.md`
- `Docs/ARCHITECTURE/BOOT_SEQUENCE_TOPOLOGY.md`
- `Docs/ARCHITECTURE/DATA_MONOLITH_H8BIN_SPEC.md`
- `Docs/ARCHITECTURE/CORE_REPLAY_DETERMINISM.md`
- `Docs/ARCHITECTURE/SCALABILITY_MATRIX.md`
- `Docs/ARCHITECTURE/GLOBAL_REGISTRY_SERVICE_LOCATOR.md`
- `Docs/ARCHITECTURE/GLOBAL_SIGNAL_CORRIDOR.md`
- `Docs/ARCHITECTURE/COOP_MERKLE_STATE_DELTA_PROTOCOL.md`
- `Docs/Design/Save_Binary_Header.md`
- `Docs/Modding/Net_Protocol_v1.md`

Updated R4 interior-boundary coverage:

- `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`
- `Docs/Modding/Future_Command_Kernel_Reservations.md`

Updated historical report/index framing:

- `Docs/Reports/README.md`
- `Docs/Reports/2026-05-17_DOCUMENTATION_DEPENDENCY_ATLAS_R7_LOCAL.md`
- `Docs/Reports/2026-05-17_DOCUMENTATION_ATLAS_AND_COUNTERS_R8_LOCAL.md`

## Verification Boundary

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, or visual proof was run in R9.

`Tools/AtlasCheck.py` still fails on the RealtimeCSG vendor icon/readme image reference family. That is a real blocker for atlas verification, not a documentation pass success.

## Static Validation

- `python Tools/BuildArchitectureAtlas.py`: exit `0`; live atlas generated at `2026-05-18 01:28:18`.
- `python Tools/test_architecture_atlas.py`: exit `0`; `9` tests OK.
- `python -m py_compile Tools/BuildArchitectureAtlas.py Tools/AtlasCheck.py Tools/test_architecture_atlas.py`: exit `1`; blocked by permission denied while writing a temporary file under `Tools/__pycache__`.
- AST parse fallback for `Tools/BuildArchitectureAtlas.py`, `Tools/AtlasCheck.py`, and `Tools/test_architecture_atlas.py`: `AST_PARSE_OK 3`.
- JSON parse for atlas/cache/modding schema/actuality manifest: `JSON_PARSE_OK 4`.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: `Status: PASS`, schema revision `14`, source signals `170`, allowed projected signals `2`, denied-by-default signals `168`.
- Active root/architecture/modding `.md` / `.txt` R4 boundary scan, excluding the prompt dump and a SHINOBU-owned active file, is `79 / 79`.
- Active-doc evidence-language grep found no hits for the overclaim patterns corrected in R9.
- `python Tools/AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6444 missing=57`.
- `git diff --check -- Docs Tools ':!Docs/Tasks/CURRENT_BATCH.md'`: exit `0`; Git line-ending warnings only.

## Cinematic Cheats / Microseconds

No runtime physical simulation was added. No gameplay code was changed. Runtime cost change: `0 us/frame`.

R9 used documentation/source correction only: downgrade over-strong proof words, point evidence at existing archive paths, and keep volatile counters explicitly static-source scoped.
