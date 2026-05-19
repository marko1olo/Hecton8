# DOC_GLOBAL_DOCS_REFRESH R27 Root / Architecture Index Counter Pass

Date: 2026-05-19
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-19 R27 Interior Actuality Boundary

This report is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, platform run, campaign telemetry, or visual-route proof is implied unless this report links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Scope

R27 is a local root/architecture documentation pass. It updates active entrypoints and architecture indexes after R26 so that R26 is no longer presented as the newest DOC_GLOBAL layer.

Edited surfaces include root documentation indexes, architecture entrypoints, global-authority docs, generated atlas metadata, and Archivarius active indexes. Historical archive bodies remain historical snapshots.

## Static Snapshot

- `Assets/_Project/**/*.cs`: `1818`.
- `Assets/_Project/Scripts/**/*.cs`: `1761`.
- First-party non-test C# files excluding `Assets/_Project/Tests*`: `1797`.
- Project/script/non-test physical lines: `1204221 / 1184559 / 1199376`.
- Broad `interface` token hits under `Assets/_Project`: `342`.
- Direct interface declaration lines under `Assets/_Project`: `267`.
- Direct public interfaces in `GlobalRegistryContracts.cs`: `62`.
- First-party asmdefs under `Assets/_Project`: `123`.
- `GlobalSignals.InitializeAllQueues()` direct `CreateQueue(...)` slots: `73`.
- `SignalBus<T>.EnsureInitialized()` lanes in `GlobalSignals.cs`: `133`, including `DebugSignal`.

These are capture-time `STATIC_SOURCE` counters from a dirty multi-agent workspace. Rerun before exact use.

## Corrections

- Promoted `Docs/Reports/2026-05-19_DOCUMENTATION_R27_ROOT_ARCHITECTURE_INDEX_COUNTER_LOCAL.md` as the current DOC_GLOBAL root/architecture/source-counter boundary in active root, architecture, report, and Archivarius indexes.
- Demoted R26 to a prior root/architecture HFI/blocker/source-counter boundary while keeping its HFI/global-authority risk promotion intact.
- Updated root and architecture source-counter lines to R27 physical-line values and direct interface declaration count.
- Corrected global-authority static orientation counts from stale `5872 / 578` to `5871 / 606` where the same doc already carried the newer recapture.
- Reworded `Global Signal Corridor` so `GlobalSignals` is a retained direct-queue bridge and `SignalBus<T>` initialization surface, not a generic first-party event-bus permission slip.
- Added generated-atlas JSON `atlas_check_status` metadata so the RealtimeCSG AtlasCheck red gate is machine-visible, not only markdown-visible.

## Current Red Gates

- `Tools\AtlasCheck.py` is expected to remain red unless the RealtimeCSG vendor icon/readme references are restored or deliberately excluded from that evidence class.
- `Docs\Modding\Validate_Mod_API_Static.ps1` is expected to remain red until `ModCommand` sequential size declaration is repaired.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, platform run, campaign telemetry, and visual proof were not run in this documentation pass.

## Validation

- `python Tools\BuildArchitectureAtlas.py`: exit `0`; regenerated `Docs\DEPENDENCY_GRAPH.md` and `Docs\DEPENDENCY_GRAPH.json`.
- `python Tools\test_architecture_atlas.py`: exit `0`; `10` tests OK.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: exit `0`.
- JSON parse spot check: `ok=5`, `bad=0`, `missing=0`.
- R27 scoped root/architecture/report/Archivarius R4 marker scan: `ScopeFiles=111`, `MissingCount=0`, `DuplicateCount=0`.
- Generated atlas JSON now exposes `artifacts.atlas_check_status = RED: Tools/AtlasCheck.py exits 1 on 57 RealtimeCSG vendor icon/readme image references; generated atlas is STATIC_SOURCE only until AtlasCheck exits 0.`
- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6549 missing=57`; missing references are RealtimeCSG vendor icon/readme image paths.
- `powershell -NoProfile -ExecutionPolicy Bypass -File Docs\Modding\Validate_Mod_API_Static.ps1`: exit `1`, `[MOD_API_STATIC_VALIDATION] Missing ModCommand sequential size declaration.`
- R27 targeted stale-current scan over active root/architecture/report/Archivarius entrypoints found only intentional prior-boundary mentions of R26/R25/R24; no actionable R26-as-current source-counter line remained in the scoped surfaces.
- Scoped root/architecture `git diff --check -- Docs Tools ':!Docs/Tasks/*' ':!Docs/AgentLogs/*' ':!Docs/Archive/**' ':!Docs/Modding/**'`: exit `0`, line-ending warnings only.
- Wider `git diff --check -- Docs Tools ':!Docs/Tasks/*' ':!Docs/AgentLogs/*' ':!Docs/Archive/**'`: exit `1` on unrelated concurrent trailing whitespace in `Docs/Modding/API_Surface_Audit_Matrix.md`, `Docs/Modding/Command_Audit_Matrix.md`, `Docs/Modding/Payload_Layout_Audit_Matrix.md`, and `Docs/Modding/Runtime_Verification_Playbook.md`.
