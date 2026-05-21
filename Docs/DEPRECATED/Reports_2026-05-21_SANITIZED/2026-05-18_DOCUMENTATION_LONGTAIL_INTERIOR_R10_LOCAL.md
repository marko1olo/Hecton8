<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# 2026-05-18 Documentation Longtail Interior R10 Local

Date: 2026-05-18
Status: LOCAL_ONLY STATIC_DOC / STATIC_SOURCE / PY_TOOL / RUNTIME PENDING VERIFICATION
Owner: DOC_GLOBAL_DOCS_REFRESH

## Scope

R10 continues the DOC_GLOBAL local-only documentation refresh after an interrupted pass left active docs pointing at this report before the report existed.

Touched active documentation interiors and indexes include:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/Reports/README.md`
- `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`
- `Docs/PROJECT_STATE_STATIC_XRAY.md`
- `Docs/AI_Fauna/*`
- `Docs/Flora_Pipeline/*`
- `Docs/Scatter_Runtime/*`
- `Docs/SPACE_ENGINE_RESEARCH/*` entry docs and reference-kernel docs
- `Docs/Design/*` entry docs
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/*`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/README.md`
- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/README.md`

Historical reports, archives, generated `bin` / `obj` outputs, extracted SpaceEngine source dumps, and active third-party/vendor references were not rewritten as current truth.

## Current Static Source Snapshot

R10 source counts were recaptured from the live dirty workspace after concurrent source churn:

| Metric | Value |
|---|---:|
| `Assets/_Project/**/*.cs` | 1739 |
| `Assets/_Project/Scripts/**/*.cs` | 1686 |
| first-party non-test C# files | 1722 |
| project physical lines by `rg -c '^'` | 1134113 |
| script physical lines by `rg -c '^'` | 1115245 |
| non-test physical lines by `rg -c '^'` | 1130062 |
| interface declaration hits by source grep | 296 |
| direct public interfaces in `GlobalRegistryContracts.cs` | 63 |
| first-party asmdefs under `Assets/_Project` | 107 |

These are volatile static counts. They are not compile, Unity import, Play Mode, profiler, GCMonitor, player build, scene wiring, save/load, or visual proof.

## Atlas Regeneration

`python Tools/BuildArchitectureAtlas.py` regenerated:

- `Docs/DEPENDENCY_GRAPH.md`
- `Docs/DEPENDENCY_GRAPH.json`
- `Docs/DEPENDENCY_GRAPH.cache.json`

Generated atlas timestamp: `2026-05-18 09:02:02`.

Atlas summary from JSON:

| Metric | Value |
|---|---:|
| source files under `Assets` / `Packages` | 5004 |
| source lines under `Assets` / `Packages` | 1789087 |
| first-party source files under `Assets/_Project/Scripts` | 1686 |
| first-party source lines under `Assets/_Project/Scripts` | 1116913 |
| asmdefs scanned | 167 |
| first-party asmdefs | 107 |
| exact `Hecton8.Core` dependent assemblies | 46 |
| `Hecton8.Core*` family dependent assemblies | 74 |
| signal count | 228 |
| queue lane count | 56 |

Atlas generation succeeded, but atlas verification did not.

## Corrections Made

- Replaced partial R10 counters `1738 / 1685 / 1721` with the live R10 recapture `1739 / 1686 / 1722`.
- Replaced R9 current-counter language in governance, root index, global architecture map, and static X-Ray docs.
- Added the R10 report link to active indexes that already referenced it.
- Downgraded `Library/Codex_DOC_AUDIT_UnityBatchCompile.log` wording in active docs because the R10 filesystem check did not find that file.
- Converted scatter, AI/Fauna, Flora, and Archivarius “current project truth” phrases that pointed to May 4 / May 1 reports into R9/R10 read-order boundaries.
- Kept dated reports and archive bundles historical instead of mutating old evidence snapshots.

## Validation

Commands and results:

- `python Tools/BuildArchitectureAtlas.py`: exit `0`.
- `python Tools/test_architecture_atlas.py`: exit `0`, `9` tests.
- AST parse of `Tools/BuildArchitectureAtlas.py`, `Tools/AtlasCheck.py`, `Tools/test_architecture_atlas.py`: `AST_PARSE_OK 3`.
- JSON parse of `Docs/DEPENDENCY_GRAPH.json`, `Docs/DEPENDENCY_GRAPH.cache.json`, `Docs/Modding/Signal_Schema.json`, `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json`: `JSON_PARSE_OK 4`.
- `powershell -ExecutionPolicy Bypass -File Docs\Modding\Validate_Mod_API_Static.ps1`: `Status: PASS`, schema revision `14`, source signals `170`.
- R10 authored Markdown scope boundary scan over Design, AI/Fauna, Flora, Scatter, and SpaceEngine docs: `37 / 37`, missing `0`.
- Scoped evidence-language scan after fixes: only allowed authority/read-order phrases remained; no active scoped `SOURCE VERIFIED`, `STATIC DESIGN VERIFIED`, `PYTHON_REFERENCE_FUZZ_VERIFIED`, `OFFLINE SIM VERIFIED`, `ATLAS VERIFIED`, `ENCYCLOPEDIA VERIFIED`, `Verified LateUpdate`, or `Verified Search` overclaim remained.

## Blockers

- `python Tools/AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6456 missing=58`.
- Missing references are still dominated by `57` RealtimeCSG vendor icon/readme image paths. The remaining non-vendor missing reference is `Docs/AgentLogs/LOG_SHINOBU_06.md`.
- During R10 validation, `Docs/Tasks/CURRENT_BATCH.md`, `Docs/Tasks/Status_DOC_GLOBAL_DOCS_REFRESH.md`, `Docs/AgentLogs/Rationale_DOC_GLOBAL_DOCS_REFRESH.md`, and `Docs/AgentLogs/LOG_DOC_GLOBAL_DOCS_REFRESH.md` were temporarily absent from the working tree. DOC_GLOBAL status/rationale/log recreation initially collided with a concurrent workspace writer, then succeeded after the task/log folders stabilized. Unrelated deleted task/log files were not restored by this agent.

## Evidence Boundary

R10 is local-only `STATIC_DOC`, `STATIC_SOURCE`, `FILESYSTEM`, and `PY_TOOL` evidence. No Unity Editor import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, or visual-route proof exists for this pass.
