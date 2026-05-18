# 2026-05-18 Documentation R22 Counter Drift And Validation

Date: 2026-05-18
Status: STATIC DOC/SOURCE/FILESYSTEM PASS; RUNTIME PROOF ABSENT

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Report Snapshot Boundary

This report is a local DOC_GLOBAL static documentation/source/filesystem snapshot. It is not Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, platform, campaign-performance, or visual-route proof.

Historical counters and old `PASS` / `VERIFIED` labels in older reports remain evidence snapshots only where current source and this report disagree.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Scope

R22 follows R21 because concurrent source churn changed the active counter surface before final validation. R21 remains a historical local snapshot; R22 is the newer capture-time static orientation for active docs that need exact source scale.

This pass patched active authority entry points and indexes that still advertised the R21 counter set as the current boundary.

## R22 Static Source Snapshot

- `Assets/_Project/**/*.cs`: `1811`.
- `Assets/_Project/Scripts/**/*.cs`: `1755`.
- First-party non-test C# files excluding `Assets/_Project/Tests*`: `1791`.
- Project/script/non-test physical lines: `1195623 / 1176132 / 1190969`.
- Broad `interface` token hits under `Assets/_Project`: `342`.
- Direct interface declaration lines under `Assets/_Project`: `267`.
- Direct public interfaces in `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`: `63`.
- First-party asmdefs under `Assets/_Project`: `117`.
- `GlobalSignals.InitializeAllQueues()` direct `CreateQueue(...)` slots: `73`.
- `InitializeCategorySignalLanes()` typed `SignalBus<T>.EnsureInitialized()` lanes: `133`.

These are volatile dirty-workspace static counters. Rerun before exact use.

## Corrections

- Promoted R22 as the current DOC_GLOBAL counter/validation boundary in root, Reports, Archivarius, architecture, procedural, forensic, and competitive-analysis entry points.
- Demoted R21 source counts to prior snapshot status where active docs previously called them current.
- Updated active architecture tables from R21 `1807 / 1750 / 1787` and `1190428 / 1170223 / 1185755` to R22 `1811 / 1755 / 1791` and `1195623 / 1176132 / 1190969`.
- Updated broad interface orientation from R21 `306 / 257` to R22 `342 / 267`; `GlobalRegistryContracts.cs` direct public interface count remains `63`.
- Preserved the evidence boundary: no Unity/runtime/profiler/player-build/visual proof is claimed from static docs/source scans.

## Evidence Limits

- No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, platform run, campaign telemetry, or visual proof was captured.
- `Tools/AtlasCheck.py` remains expected to fail until RealtimeCSG vendor icon/readme image references are restored or excluded by policy.
- Historical report JSON/markdown evidence is not mutated into current runtime proof.

## Validation

- `python Tools\BuildArchitectureAtlas.py`: exit `0`; regenerated `Docs/DEPENDENCY_GRAPH.md` and `Docs/DEPENDENCY_GRAPH.json`.
- `Docs/DEPENDENCY_GRAPH.md`: generated atlas reports `1755` first-party C# source files under `Assets/_Project/Scripts/`.
- `python Tools\test_architecture_atlas.py`: exit `0`, `10` tests OK.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: exit `0`.
- `powershell -NoProfile -ExecutionPolicy Bypass -File Docs\Modding\Validate_Mod_API_Static.ps1`: `Status: PASS`, `SchemaRevision: 14`, `SourceSignals: 160`, `AllowedProjectedSignals: 2`, `DeniedByDefaultSignals: 158`, `PublicApiMethods: 35`.
- JSON parse spot check: `JSON_OK=9`, missing `0`, bad `0`.
- R22 active md/txt R4 marker scan after adding `88` missing boundaries: `ScopeFiles=394`, `MissingCount=0`, `DuplicateCount=0`.
- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6558 missing=57`; all missing refs are RealtimeCSG vendor icon/readme image paths.
- `git diff --check -- Docs Tools ':!Docs/Tasks/CURRENT_BATCH.md'`: exit `0`; line-ending warnings only.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof was run for R22.
