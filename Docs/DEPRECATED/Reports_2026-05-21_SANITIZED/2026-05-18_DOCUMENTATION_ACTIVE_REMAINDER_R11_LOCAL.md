<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# 2026-05-18 Documentation Active Remainder R11 Local

Date: 2026-05-18
Status: LOCAL_ONLY STATIC_DOC / STATIC_SOURCE / PY_TOOL / RUNTIME PENDING VERIFICATION
Owner: DOC_GLOBAL_DOCS_REFRESH

## Scope

R11 continues the DOC_GLOBAL documentation refresh after R10. This pass targeted active entry surfaces that still risked treating R10 counters, absent artifacts, or legacy backlog proof language as current truth.

Touched active documentation interiors and indexes include:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/Reports/README.md`
- `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`
- `Docs/PROJECT_STATE_STATIC_XRAY.md`
- `Docs/PROJECT_ATLAS.md`
- `Docs/QUALITY_GATES.md`
- `Docs/AI_Fauna/README.md`
- `Docs/Flora_Pipeline/README.md`
- `Docs/Scatter_Runtime/README.md`
- `Docs/Scatter_Runtime/SCATTER_REFACTOR_EXECUTION_PLAN.md`
- `Docs/ARCHITECTURE/INPUT_DETERMINISM_SHINOBU_36.md`
- `Docs/ARCHITECTURE/PROJECT_CONTENT_LEDGER.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/README.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/README.md`
- `Docs/Legacy_Backlog/*`

Historical reports, archives, generated `bin` / `obj` outputs, and third-party/vendor references were not rewritten as current truth.

## Current Static Source Snapshot

R11 source counts were recaptured from the live dirty workspace after concurrent source churn:

| Metric | Value |
|---|---:|
| `Assets/_Project/**/*.cs` | 1742 |
| `Assets/_Project/Scripts/**/*.cs` | 1689 |
| first-party non-test C# files | 1725 |
| project physical lines by `rg -c '^'` | 1138660 |
| script physical lines by `rg -c '^'` | 1119546 |
| non-test physical lines by `rg -c '^'` | 1134363 |
| interface declaration hits under `Assets/_Project` | 296 |
| interface declaration hits under `Assets/_Project/Scripts` | 294 |
| direct public interfaces in `GlobalRegistryContracts.cs` | 63 |
| first-party asmdefs under `Assets/_Project` | 107 |

These are volatile static counts. They are not compile, Unity import, Play Mode, profiler, GCMonitor, player build, scene wiring, save/load, or visual proof.

## Atlas Regeneration

`python Tools/BuildArchitectureAtlas.py` regenerated:

- `Docs/DEPENDENCY_GRAPH.md`
- `Docs/DEPENDENCY_GRAPH.json`
- `Docs/DEPENDENCY_GRAPH.cache.json`

Generated atlas timestamp: `2026-05-18 14:06:17`.

Atlas summary from JSON:

| Metric | Value |
|---|---:|
| source files under `Assets` / `Packages` | 5007 |
| source lines under `Assets` / `Packages` | 1793645 |
| first-party source files under `Assets/_Project/Scripts` | 1689 |
| first-party source lines under `Assets/_Project/Scripts` | 1121225 |
| asmdefs scanned | 167 |
| first-party asmdefs | 107 |
| exact `Hecton8.Core` dependent assemblies | 46 |
| `Hecton8.Core*` family dependent assemblies | 74 |
| signal count | 227 |
| queue lane count | 56 |

Atlas generation succeeded, but atlas verification did not.

## Corrections Made

- Reframed R10 source counts as historical capture data and promoted R11 spot-check counters only as volatile static-source orientation.
- Fixed `Docs/DOC_GOVERNANCE.md` contradictory first-party asmdef count from `106` to `107`.
- Replaced active `Current project truth` wording with `current authority spine` language.
- Marked `Library/OmegaAutonomySmokeTester.json` and `CodexArtifacts/unity-omega-smoke-2026-05-05-doc-continuation.log` absent in the R11 filesystem check instead of presenting a current FAIL JSON.
- Removed `UNITY_BATCHMODE_IMPORT_COMPILE` from `Docs/PROJECT_STATE_STATIC_XRAY.md` evidence class because the cited batchmode artifact is absent from the current filesystem.
- Added a R4/R10-style actuality boundary to `Docs/ARCHITECTURE/INPUT_DETERMINISM_SHINOBU_36.md` and changed its `Runtime truth` wording to a static/source contract with runtime proof pending.
- Marked `CodexArtifacts/2026-05-07_ORPHANED_SCRIPT_AUDIT.csv` absent before any deletion-candidate use.
- Corrected active Archivarius indexes and Project Atlas counters to the R11 read-order.
- Added legacy backlog proof boundaries for `Verified in Unity`, `passed`, `Feature Complete`, `Zero GC`, `0 bytes`, ES3/Easy Save, and ready/done language.

## Read-Only Subagent Inputs

Three read-only audits were used:

- Architecture/static X-Ray/atlas surfaces: found stale R10 counters, stale asmdef count, missing R4 boundary in `INPUT_DETERMINISM_SHINOBU_36.md`, absent orphaned-script CSV, and absent active-path H-Phi summary.
- Modding/reports indexes: found stale R10-as-current counters, contradictory asmdef count, and absent Omega smoke artifacts.
- Design/world/legacy entry surfaces: found legacy backlog Unity-verified, passed, feature-complete, 0-GC, ES3/Easy Save, and 0-byte claims needing historical boundaries.

Subagents were read-only. The primary agent integrated the patches.

## Validation

Commands and results:

- `python Tools/BuildArchitectureAtlas.py`: exit `0`.
- `python Tools/test_architecture_atlas.py`: exit `0`, `9` tests.
- `python Tools/AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6457 missing=57`.
- AST parse of `Tools/BuildArchitectureAtlas.py`, `Tools/AtlasCheck.py`, `Tools/test_architecture_atlas.py`: `AST_PARSE_OK 3`.
- JSON parse of `Docs/DEPENDENCY_GRAPH.json`, `Docs/DEPENDENCY_GRAPH.cache.json`, `Docs/Modding/Signal_Schema.json`, `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json`: `JSON_PARSE_OK 4`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File Docs\Modding\Validate_Mod_API_Static.ps1`: `Status: PASS`, schema revision `14`, source signals `170`.
- R11 touched-doc boundary scan: `21 / 21`, missing `0`.
- Scoped evidence-language scan after fixes: no active scoped hits for banned overclaim phrases.
- `git diff --check -- Docs Tools ':!Docs/Tasks/CURRENT_BATCH.md'`: exit `0`, line-ending warnings only.

## Blockers

- `python Tools/AtlasCheck.py` remains red on `57` missing RealtimeCSG vendor icon/readme image references.
- No Unity Editor import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, or visual-route proof exists for this pass.
- `Docs/Tasks` and `Docs/AgentLogs` are volatile under concurrent agents. DOC_GLOBAL evidence files were absent at R11 start and had to be reconstructed from disk/report evidence.

## Evidence Boundary

R11 is local-only `STATIC_DOC`, `STATIC_SOURCE`, `FILESYSTEM`, `PY_TOOL`, and `READ_ONLY_SUBAGENT_AUDIT` evidence. It is not runtime proof.
