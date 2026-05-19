# DOC_GLOBAL_DOCS_REFRESH Status

Date: 2026-05-19
Status: ACTIVE R27 STUB / PRIOR HISTORY ARCHIVED

Prior full status history is archived at `Docs/Archive/Batch009/Tasks/Status_DOC_GLOBAL_DOCS_REFRESH.md`. The active file was absent during R27 closeout, so this file records the current live DOC_GLOBAL state without rewriting the archived history.

## R27 Checklist

- [x] Re-read available DOC_GLOBAL status/rationale/log history from `Docs/Archive/Batch009` and attempted prompt extraction from `Docs/Tasks/CURRENT_BATCH.md`. DOD: `CURRENT_BATCH.md` extraction returned `PROMPT_NOT_FOUND`; archived evidence files were used as disk memory. Rejected: relying on compacted chat only. Estimate: 0 us runtime.
- [x] Ran read-only root/architecture/index subagent audits. DOD: Curie/Parfit/Hypatia returned exact findings for stale R26 current-boundary wording, stale counters, missing R27 routing, generated-atlas red-gate metadata, Archivarius R24/R26 drift, and global-authority count mismatch. Rejected: filename sorting. Estimate: 0 us runtime.
- [x] Captured R27 volatile static source counters. DOD: `1818 / 1761 / 1797` C# files, `1204221 / 1184559 / 1199376` physical lines, `342 / 267` interface orientation, `62` direct registry interfaces, `123` first-party asmdefs, `73` direct queue slots, `133` typed signal lanes. Rejected: preserving R26 counts as current. Estimate: 0 us runtime.
- [x] Updated root/architecture authority surfaces and active indexes. DOD: R27 promoted in root README/governance/root reference/static X-Ray/global architecture map/Project Atlas/runtime plan/systems contracts/quality gates, Reports README, architecture README/actuality ledger, H-Phi metric doc, global authority docs, signal corridor, dispatch/boot docs, SHINOBU pages, and Archivarius indexes. Rejected: mutating historical archive bodies as current truth. Estimate: 0 us runtime.
- [x] Wrote `Docs/Reports/2026-05-19_DOCUMENTATION_R27_ROOT_ARCHITECTURE_INDEX_COUNTER_LOCAL.md` and regenerated atlas artifacts. DOD: `Tools/BuildArchitectureAtlas.py` now emits JSON `artifacts.atlas_check_status`. Rejected: markdown-only blocker status. Estimate: 0 us runtime.
- [x] Ran R27 static validation gates. DOD: atlas generation/tests/py_compile, JSON parse, R4 scan, AtlasCheck, Mod API validator, targeted stale-current scan, and scoped diff-check recorded. Rejected: hiding current Modding validator failure or RealtimeCSG AtlasCheck blocker. Estimate: 0 us runtime.

## R27 Static Snapshot

- `Assets/_Project/**/*.cs`: `1818`.
- `Assets/_Project/Scripts/**/*.cs`: `1761`.
- First-party non-test C# excluding `Assets/_Project/Tests*`: `1797`.
- Project/script/non-test physical lines: `1204221 / 1184559 / 1199376`.
- Broad `interface` token hits: `342`.
- Direct interface declaration lines: `267`.
- Direct public interfaces in `GlobalRegistryContracts.cs`: `62`.
- First-party asmdefs: `123`.
- Direct `GlobalSignals.CreateQueue(...)` slots: `73`.
- Typed `SignalBus<T>.EnsureInitialized()` lanes: `133`.

## R27 Validation

- `python Tools\BuildArchitectureAtlas.py`: exit `0`.
- `python Tools\test_architecture_atlas.py`: exit `0`, `10` tests OK.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: exit `0`.
- JSON parse spot check: `ok=5`, `bad=0`, `missing=0`.
- R4 marker scan: `ScopeFiles=111`, `MissingCount=0`, `DuplicateCount=0`.
- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6549 missing=57`.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: exit `1`, missing `ModCommand` sequential size declaration.
- Scoped root/architecture `git diff --check -- Docs Tools ':!Docs/Tasks/*' ':!Docs/AgentLogs/*' ':!Docs/Archive/**' ':!Docs/Modding/**'`: exit `0`, line-ending warnings only.
- Wider `git diff --check -- Docs Tools ':!Docs/Tasks/*' ':!Docs/AgentLogs/*' ':!Docs/Archive/**'`: exit `1` on unrelated concurrent trailing whitespace in four `Docs/Modding` files.

## R27 Current Blockers

- No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, platform run, campaign telemetry, or visual-route proof exists for R27.
- `Tools/AtlasCheck.py` remains red on `57` missing RealtimeCSG vendor icon/readme image references.
- Mod API static validator remains red on missing `ModCommand` sequential size declaration.
- Wider all-doc diff-check remains red on unrelated concurrent Modding trailing whitespace outside the current root/architecture scope.
- Source counters are volatile under concurrent agents; R27 values are capture-time static documentation/source orientation, not runtime or compile proof.

## R28 Checklist

- [x] Re-read active status/rationale and attempted prompt extraction from `Docs/Tasks/CURRENT_BATCH.md`. DOD: prompt extraction still returned `PROMPT_NOT_FOUND`; active R27 stub and archived history remain the disk memory. Rejected: relying on compacted chat only. Estimate: 0 us runtime.
- [x] Ran root/architecture interior scans for stale current-boundary language, local markdown links, R4 marker coverage, and explicit R28 note gaps. DOD: local link scan returned `MissingCount=0`; initial R4 scan exposed the active HFI report boundary gap, which was patched. Rejected: treating generic R4 wording as enough for the requested root/architecture interior pass. Estimate: 0 us runtime.
- [x] Added R28 interior notes to 25 active architecture files that lacked explicit current DOC_GLOBAL root/architecture blocker context. DOD: `rg -l "DOC_GLOBAL R28 Interior Note" Docs\ARCHITECTURE` count is `25`. Rejected: sorting filenames or mutating historical reports as current truth. Estimate: 0 us runtime.
- [x] Promoted R28 through root/architecture/report entrypoints. DOD: `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ROOT_DOCS_REFERENCE.md`, `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`, `Docs/SYSTEMS_CONTRACTS.md`, `Docs/QUALITY_GATES.md`, `Docs/PROJECT_ATLAS.md`, `Docs/ARCHITECTURE/README.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, and `Docs/Reports/README.md` now route current root/architecture DOC_GLOBAL order through R28 before R27. Rejected: leaving R27 advertised as latest overall boundary after R28 interior edits. Estimate: 0 us runtime.
- [x] Corrected validation drift after live tool rerun. DOD: `Docs\Modding\Validate_Mod_API_Static.ps1` now passes (`Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`), so active architecture docs and the R28 report no longer claim a current Mod API red gate. Rejected: preserving stale red ModCommand text after objective PASS output. Estimate: 0 us runtime.
- [x] Wrote `Docs/Reports/2026-05-19_DOCUMENTATION_R28_ROOT_ARCHITECTURE_INTERIOR_BOUNDARY_LOCAL.md` and recorded R28 validation. DOD: atlas unit tests pass, JSON parse passes, link/R4 scans pass, `AtlasCheck` remains red on RealtimeCSG vendor refs, scoped diff-check exits `0` with line-ending warnings only. Rejected: claiming Unity/runtime/profiler/player-build proof. Estimate: 0 us runtime.

## R28 Validation

- R28 interior note scan: `25` active architecture docs.
- Scoped active root/architecture/direct-report/Archivarius R4 marker scan: `ScopeFiles=325`, `MissingCount=0`, `DuplicateCount=0`.
- Scoped local markdown link scan: `MissingCount=0`, `Files=0`, `ScopeFiles=157`.
- Stale current-red Mod API blocker scan over active root/architecture/report surfaces: no hits.
- `python Tools\test_architecture_atlas.py`: exit `0`, `10` tests OK.
- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6549 missing=57`.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: `Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`.
- JSON parse spot check: `ok=5`, `bad=0`, `missing=0`.
- Scoped root/architecture `git diff --check -- Docs Tools ':!Docs/Tasks/*' ':!Docs/AgentLogs/*' ':!Docs/Archive/**' ':!Docs/Modding/**'`: exit `0`, line-ending warnings only.

## R28 Current Blockers

- No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, platform run, campaign telemetry, or visual-route proof exists for R28.
- `Tools/AtlasCheck.py` remains red on `57` missing RealtimeCSG vendor icon/readme image references.
- Mod API static validation is no longer a current red gate in the R28 local run; it is static validator proof only, not mod runtime proof.
- Source counters remain the R27 capture-time static orientation until a newer counter pass is intentionally run.
