# DOC_GLOBAL_DOCS_REFRESH Status

Date: 2026-05-19
Status: ACTIVE R35 / PRIOR HISTORY ARCHIVED

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

## R29 Checklist

- [x] Re-read active status/rationale/log and attempted prompt extraction from `Docs/Tasks/CURRENT_BATCH.md`. DOD: prompt extraction returned `PROMPT_NOT_FOUND`; active R27/R28 disk memory and archived history were used. Rejected: relying on compacted chat only. Estimate: 0 us runtime.
- [x] Integrated read-only root/architecture subagent audits. DOD: Franklin found stale Mod API validator wording, global-authority R27 boundary headings, proof-plan-as-proof wording, and route-card example mismatch; Maxwell found Trauma, Signal Corridor, and Dispatch source/proof wording drift. Rejected: ignoring subagent exact line findings. Estimate: 0 us runtime.
- [x] Corrected active stale-gate wording. DOD: `HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, `PROJECT_STATE_STATIC_XRAY.md`, `GLOBAL_SIGNAL_CORRIDOR.md`, and `Reports/README.md` now record Mod API static validation as PASS instead of current red gate. Rejected: preserving R27 stale ModCommand blocker after R28/R29 PASS output. Estimate: 0 us runtime.
- [x] Tightened global-authority review semantics. DOD: `GLOBAL_AUTHORITY_*` docs now carry R28/R29 boundary context, `GREEN` requires attached runtime/profiler/player evidence for runtime-facing routes, and a proof plan alone is `YELLOW` unless documentation-only. Rejected: letting proof plans pass as proof. Estimate: 0 us runtime.
- [x] Demoted remaining static proof overclaims. DOD: `TRAUMA_GLITCH_SYSTEM.md` no longer claims code-level validation proves compile/Console/GC cleanliness; `GLOBAL_SIGNAL_CORRIDOR.md` and `DISPATCH_PIPELINE.md` scope inventories to the R27 source-counter snapshot. Rejected: treating static source sweeps as Unity/runtime/profiler proof. Estimate: 0 us runtime.
- [x] Promoted R29 through root/architecture/report entrypoints and wrote `Docs/Reports/2026-05-19_DOCUMENTATION_R29_ROOT_ARCHITECTURE_STALE_GATE_GLOBAL_AUTHORITY_LOCAL.md`. DOD: root README, governance, root reference, global architecture map, runtime plan, systems contracts, quality gates, project atlas, architecture README, actuality ledger, and Reports README now route current DOC_GLOBAL root/architecture order through R29 before R28/R27. Rejected: leaving R29 edits hidden behind R28 read order. Estimate: 0 us runtime.

## R29 Validation

- Targeted stale red-gate/proof-plan scan over active root/architecture/report surfaces: no hits.
- Disallowed stale R28-latest scan: no disallowed hits; remaining R28 mentions are prior-boundary wording.
- Scoped active root/architecture/report/Archivarius R4 marker scan: `ScopeFiles=162`, `MissingCount=0`, `DuplicateCount=0`.
- Scoped local markdown link scan: `ScopeFiles=162`, `MissingLinks=0`.
- `python Tools\test_architecture_atlas.py`: exit `0`, `10` tests OK.
- JSON parse spot check: `ok=5`, `bad=0`, `missing=0`.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: exit `0`, `Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`.
- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6549 missing=57`.
- Scoped root/architecture `git diff --check -- Docs Tools ':!Docs/Tasks/*' ':!Docs/AgentLogs/*' ':!Docs/Archive/**' ':!Docs/Modding/**'`: exit `0`, line-ending warnings only.

## R29 Current Blockers

- No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, platform run, campaign telemetry, or visual-route proof exists for R29.
- `Tools/AtlasCheck.py` remains red on `57` missing RealtimeCSG vendor icon/readme image references.
- Mod API static validation passes in R29, but this is static validator proof only, not mod runtime proof.
- Source counters remain the R27 capture-time static orientation until a newer counter pass is intentionally run.

## R30 Checklist

- [x] Re-read active status/rationale and attempted prompt extraction from `Docs/Tasks/CURRENT_BATCH.md`. DOD: prompt extraction returned `PROMPT_NOT_FOUND`; active R29 disk memory and archived prior history were used. Rejected: relying on compacted chat only. Estimate: 0 us runtime.
- [x] Ran three read-only subagent audits and integrated root/architecture findings. DOD: subagents found R28-as-current architecture residue, stale May 4/May 7 latest/current wording, global-authority heading drift, BinaryHygiene active-path absence, Dispatch R27-current wording, and static PASS portability gaps. Rejected: treating subagent output as advisory without line-level verification. Estimate: 0 us runtime.
- [x] Corrected active architecture R28-as-current residue. DOD: active architecture docs now route current root/architecture boundary through R29/R30 context and retain R28 as prior interior-boundary correction. Rejected: leaving R28 wording because it was added intentionally in the prior pass. Estimate: 0 us runtime.
- [x] Corrected root/report latest/current and proof wording. DOD: old May 4/May 7/May 13 entries are historical scoped evidence; BinaryHygiene row cites `Docs/Archive/Batch009/AgentLogs/BinaryHygiene_SHINOBU_50.json`; acoustic LUT row is static source/prefab evidence, not Unity proof. Rejected: preserving old `latest/current/clean` labels where they read as current proof. Estimate: 0 us runtime.
- [x] Added static-tool caveats around Mod API PASS language. DOD: repeated architecture `Status=PASS` wording now says static-tool orientation only and requires artifact path, command, timestamp, environment, and output before reuse as proof. Rejected: claiming PASS output as portable runtime/mod proof. Estimate: 0 us runtime.
- [x] Wrote `Docs/Reports/2026-05-19_DOCUMENTATION_R30_ROOT_ARCHITECTURE_INTERNAL_CURRENTNESS_LOCAL.md` and promoted R30 through root/architecture/report entrypoints. DOD: root README, governance, root reference, global architecture map, runtime plan, systems contracts, quality gates, project atlas, architecture README, actuality ledger, and Reports README now place R30 before R29/R28/R27. Rejected: leaving R30 hidden as an unindexed report. Estimate: 0 us runtime.

## R30 Validation

- Targeted stale R28/latest/proof-current scan over active root/architecture/report surfaces: `missing=0`.
- Markdown/txt R4 marker scan: `ScopeFiles=88`, `MissingCount=0`, `DuplicateCount=0`.
- Local markdown link scan: `ScopeFiles=88`, `MissingLinks=0`.
- `python Tools\test_architecture_atlas.py`: exit `0`, `10` tests OK.
- JSON parse spot check: `ok=5`, `bad=0`, `missing=0`.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: exit `0`, `Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`.
- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6549 missing=57`.
- Scoped root/architecture `git diff --check -- Docs Tools ':!Docs/Tasks/*' ':!Docs/AgentLogs/*' ':!Docs/Archive/**' ':!Docs/Modding/**'`: exit `0`, line-ending warnings only.

## R30 Current Blockers

- No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, platform run, campaign telemetry, or visual-route proof exists for R30.
- `Tools/AtlasCheck.py` remains red on `57` missing RealtimeCSG vendor icon/readme image references.
- Mod API static validation passes in R30, but this is static validator proof only, not mod runtime proof.
- Source counters remain the R27 capture-time static orientation until a newer counter pass is intentionally run.

## R31 Checklist

- [x] Re-read active status/rationale and attempted prompt extraction from `Docs/Tasks/CURRENT_BATCH.md`. DOD: prompt extraction returned `PROMPT_NOT_FOUND`; active R30 disk memory and archived prior history were used. Rejected: relying on compacted chat only. Estimate: 0 us runtime.
- [x] Integrated read-only root/architecture subagent audits. DOD: Dirac found root latest/current/absent-artifact residue; Goodall found stale global-authority headings, Dispatch R29 boundary text, absent source/artifact paths, and a Mod API proof wording gap. Rejected: treating subagent output as advisory without local verification. Estimate: 0 us runtime.
- [x] Propagated R31 through active root/architecture boundary lines. DOD: `Docs/Reports/2026-05-19_DOCUMENTATION_R31_ARCHITECTURE_CURRENT_BOUNDARY_PROPAGATION_LOCAL.md` is now the latest root/architecture DOC_GLOBAL boundary; R30 is prior internal-currentness, R29 prior stale-gate/global-authority, R28 prior interior-boundary, and R27 latest source-counter/index snapshot. Rejected: creating a R31 report while leaving current lines pointed at R30. Estimate: 0 us runtime.
- [x] Corrected root/report latest/current residue and absent artifact/source paths. DOD: May 3/May 11/May 17 rows in active entrypoints are historical; missing `Temp/CodexArtifacts/unity-batch-2026-05-03-foundation-hardening-after-watchdogs.log`, missing `CodexArtifacts/2026-05-07_ORPHANED_SCRIPT_AUDIT.csv`, missing `SaveCompressionDictionary.cs`, and shorthand source paths are now bounded. Rejected: creating placeholder artifacts or source files as fake proof. Estimate: 0 us runtime.
- [x] Closed active architecture R4 marker gaps. DOD: seven active architecture docs received R4 actuality boundaries; final R4 scan reports `ScopeFiles=89`, missing `0`, duplicate `0`. Rejected: excluding those files from active architecture scope. Estimate: 0 us runtime.
- [x] Wrote R31 report and ran static validation. DOD: targeted stale scan, R4 scan, local link scan, atlas tests, JSON parse, Mod API validator, AtlasCheck, and scoped diff-check results are recorded below. Rejected: claiming Unity/runtime/profiler/player-build proof. Estimate: 0 us runtime.

## R31 Validation

- Targeted stale-current/proof scan over active root/architecture/report surfaces: `missing=0`.
- Markdown/txt R4 marker scan: `ScopeFiles=89`, `MissingCount=0`, `DuplicateCount=0`.
- Local markdown link scan: `ScopeFiles=88`, `MissingLinks=0`.
- `python Tools\test_architecture_atlas.py`: exit `0`, `10` tests OK.
- JSON parse spot check: `ok=5`, `bad=0`, `missing=0`.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: exit `0`, `Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`.
- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6549 missing=57`.
- Scoped root/architecture `git diff --check -- Docs Tools ':!Docs/Tasks/*' ':!Docs/AgentLogs/*' ':!Docs/Archive/**' ':!Docs/Modding/**'`: exit `0`, line-ending warnings only.

## R31 Current Blockers

- No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, platform run, campaign telemetry, or visual-route proof exists for R31.
- `Tools/AtlasCheck.py` remains red on `57` missing RealtimeCSG vendor icon/readme image references.
- Mod API static validation passes in R31, but this is static validator proof only, not mod runtime proof.
- Source counters remain the R27 capture-time static orientation until a newer counter pass is intentionally run.

## R32 Checklist

- [x] Re-read active status/rationale and attempted prompt extraction from `Docs/Tasks/CURRENT_BATCH.md`. DOD: prompt extraction returned `PROMPT_NOT_FOUND`; active R31 disk memory and archived prior history were used. Rejected: relying on compacted chat only. Estimate: 0 us runtime.
- [x] Integrated read-only architecture subagent findings. DOD: the PDA streamer R4 gap, procedural-wreckage route-card R4 gap, route-card `STATIC GREEN` overclaim, source-anchor gaps, historical-manifest wording, Subnautica2 current-proof wording, and temporary Roslyn wording were locally verified and patched. Rejected: treating subagent output as proof without file checks. Estimate: 0 us runtime.
- [x] Created the R32 artifact and promoted R32 through active root/architecture entrypoints. DOD: `Docs/Reports/2026-05-19_DOCUMENTATION_R32_ARCHITECTURE_R4_AND_PROOF_WORDING_LOCAL.md` exists and active root/architecture docs no longer say the R32 report is absent. Rejected: leaving R32 references as stale after creating the report. Estimate: 0 us runtime.
- [x] Corrected current Mod API static validator wording. DOD: `Docs\Modding\Validate_Mod_API_Static.ps1` now reports `Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`; active root/architecture docs use that current static-tool tuple. Rejected: preserving stale `14/160`, `15/161`, or using the PASS as runtime proof. Estimate: 0 us runtime.
- [x] Closed R4 and local-link gates for the active root/architecture/report scope. DOD: R4 scan `ScopeFiles=81`, missing `0`, duplicate `0`; local markdown links `ScopeFiles=81`, missing `0`. Rejected: excluding the procedural-wreckage global-authority route card after it was found active. Estimate: 0 us runtime.
- [x] Wrote R32 report and ran static validation. DOD: stale scan, R4 scan, link scan, atlas unit tests, JSON parse, Mod API validator, AtlasCheck, and scoped diff-check results are recorded below. Rejected: claiming Unity/runtime/profiler/player-build proof. Estimate: 0 us runtime.

## R32 Validation

- Targeted stale-current/proof scan over active root/architecture/report surfaces: no hits.
- Markdown R4 marker scan: `ScopeFiles=81`, `Missing=0`, `Duplicate=0`.
- Local markdown link scan: `ScopeFiles=81`, `MissingLinks=0`.
- `python Tools\test_architecture_atlas.py`: exit `0`, `10` tests OK.
- Active non-archive docs JSON parse: `JsonFiles=131`, ok `131`, bad `0`.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: exit `0`, `Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6549 missing=59`.
- Scoped root/architecture `git diff --check -- Docs Tools ':!Docs/Tasks/*' ':!Docs/AgentLogs/*' ':!Docs/Archive/**' ':!Docs/Modding/**'`: exit `0`, line-ending warnings only.

## R32 Current Blockers

- No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, platform run, campaign telemetry, or visual-route proof exists for R32.
- `Tools/AtlasCheck.py` remains red on `59` missing references: RealtimeCSG vendor icon/readme images plus absent `VaultXRayWindow.cs` and `HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`.
- Mod API static validation passes in R32, but this is static validator proof only, not mod runtime proof.
- Source counters remain mixed: R27 is the latest deliberate physical-line counter snapshot; a concurrent SHINOBU_02 static spot check updated active file/interface/asmdef orientation in `Docs/DOC_GOVERNANCE.md`, but not physical lines.

## R33 Checklist

- [x] Re-read active status/rationale and attempted prompt extraction from `Docs/Tasks/CURRENT_BATCH.md`. DOD: prompt extraction returned `PROMPT_NOT_FOUND`; active R32 disk memory and archived prior history were used. Rejected: relying on compacted chat only. Estimate: 0 us runtime.
- [x] Promoted R33 through active root/architecture entrypoints and wrote `Docs/Reports/2026-05-19_DOCUMENTATION_R33_ROOT_ARCHITECTURE_R32_RESIDUE_SOURCE_ANCHORS_LOCAL.md`. DOD: root README, governance, root reference, global architecture map, runtime plan, systems contracts, quality gates, project atlas, architecture README, actuality ledger, and Reports README route current root/architecture DOC_GLOBAL order through R33 before R32. Rejected: leaving R32 advertised as latest after R33 edits existed. Estimate: 0 us runtime.
- [x] Corrected R32 residue in active architecture bodies. DOD: stale `DOC_GLOBAL R32 Current Boundary Note`, R32-as-current boundary lines, and old `59` AtlasCheck wording were corrected to R33/current `57` RealtimeCSG-only AtlasCheck wording. Rejected: preserving exact-path residue after regenerated AtlasCheck output no longer contained those refs. Estimate: 0 us runtime.
- [x] Added and verified source anchors for runtime-facing architecture route/contract docs. DOD: source-anchor filesystem scan checked `221` paths with `0` missing. Rejected: source-anchor sections that point at absent files or generic system names. Estimate: 0 us runtime.
- [x] Cleaned active architecture document-body duplication. DOD: repeated R4 markers were reduced to one per active file, duplicated repeated-body cycles were trimmed, and duplicate heading-body scan reports `DUP_HEADING_GT2=0`. Rejected: only sorting docs while repeated bodies remained inside active files. Estimate: 0 us runtime.
- [x] Closed active architecture R4 boundary gaps. DOD: `FIRST_20_MINUTES_ROUTE_BRIEF.md`, `ROUTE_CARD_SHINOBU_157_AUTOPILOT.md`, and `SHINOBU_158_BUOYANCY_ROUTE_CARD.md` now carry R4/R33 boundaries; final scan reports `ScopeFiles=83`, `Missing=0`, `Duplicate=0`. Rejected: excluding new route cards from active architecture scope. Estimate: 0 us runtime.
- [x] Regenerated atlas and ran static validation. DOD: atlas build/tests/py_compile, JSON parse, Mod API validator, AtlasCheck, R4 scan, source-anchor scan, local markdown link scan, duplicate-heading scan, and scoped diff-check results are recorded below. Rejected: claiming Unity/runtime/profiler/player-build proof. Estimate: 0 us runtime.

## R33 Validation

- `python Tools\BuildArchitectureAtlas.py`: exit `0`; regenerated `Docs/DEPENDENCY_GRAPH.md` and `Docs/DEPENDENCY_GRAPH.json`.
- `python Tools\test_architecture_atlas.py`: exit `0`, `10` tests OK.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: exit `0`.
- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6671 missing=57`; missing set is RealtimeCSG vendor icon/readme image refs only.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: exit `0`, `Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- Active non-archive Docs JSON parse: `JsonFiles=131`, `Bad=0`.
- Active architecture R4 marker scan: `ScopeFiles=83`, `Missing=0`, `Duplicate=0`.
- Source-anchor filesystem scan: `SourceAnchorPathsChecked=221`, `Missing=0`.
- Duplicate architecture heading-body scan: `DUP_HEADING_GT2=0`.
- Scoped local markdown link scan: `MarkdownLinksChecked=53`, `Missing=0`.
- Targeted stale-current scan: no disallowed R32-current, `59` AtlasCheck, `SchemaRevision=14`, or `SourceSignals=160` hits; remaining R33/R32/R31 chain mentions are current read-order text, not stale-current claims.
- Scoped root/architecture `git diff --check -- Docs Tools ':!Docs/Tasks/*' ':!Docs/AgentLogs/*' ':!Docs/Archive/**' ':!Docs/Modding/**'`: exit `0`, line-ending warnings only.

## R33 Current Blockers

- No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, platform run, campaign telemetry, or visual-route proof exists for R33.
- `Tools/AtlasCheck.py` remains red on `57` missing references: RealtimeCSG vendor icon/readme image refs only in the current regenerated atlas.
- Mod API static validation passes in R33, but this is static validator proof only, not mod runtime proof.
- R27 remains the latest deliberate DOC_GLOBAL source-counter/index and physical-line snapshot until a deliberate full counter pass reruns.

## R34 Checklist

- [x] Re-read active status/rationale and attempted prompt extraction from `Docs/Tasks/CURRENT_BATCH.md`. DOD: prompt extraction returned `PROMPT_NOT_FOUND`; active R33 disk memory, AGENTS authority, `Docs/Actual Domains of Project.txt`, and task-relevant mandates were used. Rejected: relying on compacted chat or stale root/current lines. Estimate: 0 us runtime.
- [x] Recaptured root/architecture source scale and signal counts. DOD: R34 counters record `1924 / 1867 / 1902` C# files, `1304459 / 1284763 / 1298736` physical lines, `344 / 339` broad interface hits, `269` direct interface declarations, `62` direct `GlobalRegistryContracts.cs` public interfaces, `129 / 127` first-party asmdefs, `73` direct `GlobalSignals.CreateQueue(...)` slots, `133` typed lanes inside `GlobalSignals.cs`, and `254` broader script typed-lane matches. Rejected: continuing to cite R27 as current physical-line/source-counter truth. Estimate: 0 us runtime.
- [x] Promoted disk-backed R34 through active root/architecture entrypoints and wrote `Docs/Reports/2026-05-19_DOCUMENTATION_R34_ROOT_ARCHITECTURE_SOURCE_COUNTER_REFRESH_LOCAL.md`. DOD: root README, governance, root reference, global architecture map, runtime plan, systems contracts, quality gates, project atlas, architecture README, actuality ledger, and Reports README no longer advertise R34 as absent. Rejected: leaving active docs with "R34 absent/restored" wording after the report existed. Estimate: 0 us runtime.
- [x] Corrected active architecture interiors. DOD: R34 boundary notes supersede R33/R27 source-counter wording, R4 gaps were closed for platform/KCC/light/damage docs, expected fault dump paths were separated from `Source Anchors`, and source-anchor scan reports `257` checked paths with `0` missing. Rejected: using source-anchor sections for future dump artifacts or absent file placeholders. Estimate: 0 us runtime.
- [x] Corrected stale compile-blocker wording. DOD: docs now record that `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs` exists; `Hecton8.Core.csproj` still references absent `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`; `Assembly-CSharp.csproj` still references absent `Assets/_Project/_Archive/HectonWaterPhysics.cs` and `Assets/_Project/_Archive/HectonWaterPhysicsEditor.cs`. Rejected: preserving older both-missing ChemicalInfluenceGrid wording. Estimate: 0 us runtime.
- [x] Regenerated atlas and ran static validation. DOD: atlas build/tests/py_compile, AtlasCheck, Mod API static validator, JSON parse, R4 marker scan, source-anchor scan, local-link scan, stale-current scan, project-file path check, and scoped diff-check results are recorded below. Rejected: claiming Unity/runtime/profiler/player-build proof. Estimate: 0 us runtime.

## R34 Validation

- `python Tools\BuildArchitectureAtlas.py`: exit `0`; regenerated `Docs/DEPENDENCY_GRAPH.md` and `Docs/DEPENDENCY_GRAPH.json`.
- `python Tools\test_architecture_atlas.py`: exit `0`, `10` tests OK.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: exit `0`.
- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6705 missing=57`; missing set is RealtimeCSG vendor icon/readme image refs only.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: exit `0`, `Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- Active non-archive Docs JSON parse: `JsonFiles=132`, `Bad=0`.
- Root/architecture R4 marker scan: `ScopeFiles=104`, `Missing=0`, `Duplicate=0`.
- Architecture source-anchor filesystem scan: `SourceAnchorPathsChecked=257`, `Missing=0`.
- Local markdown link scan over root/architecture/report entrypoint scope: `ScopeFiles=104`, `MarkdownLinksChecked=62`, `Missing=0`.
- Targeted stale-current scan for R34-absent/restored wording, old AtlasCheck tuples, stale ChemicalInfluenceGrid missing wording, and R33-latest wording: `NO_HITS`.
- Project-file/filesystem check: `ChemicalInfluenceGrid.cs` exists; `LogisticsPipeEvents.cs`, `HectonWaterPhysics.cs`, and `HectonWaterPhysicsEditor.cs` are absent while still referenced by generated project files.
- Scoped root/architecture `git diff --check -- Docs Tools ':!Docs/Tasks/*' ':!Docs/AgentLogs/*' ':!Docs/Archive/**' ':!Docs/Modding/**'`: exit `0`, line-ending warnings only.

## R34 Current Blockers

- No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, platform run, campaign telemetry, or visual-route proof exists for R34.
- `Tools/AtlasCheck.py` remains red on `57` missing RealtimeCSG vendor icon/readme image references.
- Project-file stale includes remain: `Hecton8.Core.csproj` references absent `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`; `Assembly-CSharp.csproj` references absent `Assets/_Project/_Archive/HectonWaterPhysics.cs` and `Assets/_Project/_Archive/HectonWaterPhysicsEditor.cs`.
- Mod API static validation passes in R34, but this is static validator proof only, not mod runtime proof.

## R35 Checklist

- [x] Re-read active status/rationale and attempted prompt extraction from `Docs/Tasks/CURRENT_BATCH.md`. DOD: prompt extraction returned `PROMPT_NOT_FOUND`; active disk memory and root/architecture source/docs were used. Rejected: relying on compacted chat only. Estimate: 0 us runtime.
- [x] Integrated three read-only subagent audits. DOD: root entrypoint R34-first residue, architecture body R34/R33-current residue, AtlasCheck drift, absent artifact links, global-authority proof caveats, and CSV/screenshot/source-anchor gaps were locally verified before patching. Rejected: trusting subagent output without filesystem/tool checks. Estimate: 0 us runtime.
- [x] Promoted R35 through active root and architecture surfaces. DOD: root README, root reference, runtime plan, systems contracts, quality gates, architecture README, actuality ledger, H-Phi static metric, Reports README, `BUILD_PLAYTEST_ISSUES.md`, and `MASTER_RELEASE_WORK_PLAN.md` now route current root/architecture orientation through R35 and preserve R34 as prior source-counter refresh. Rejected: leaving R35 report present but entrypoints starting at R34. Estimate: 0 us runtime.
- [x] Closed R4/source-anchor residue. DOD: `SHINOBU_138_CHEMICAL_INFLUENCE_GRID_ROUTE_CARD.md` and `ASYNCHRONOUS_TELEMETRY_EXPORTER_SHINOBU_160.md` now carry R4 boundaries and source anchors; source-anchor scan reports `256` paths, missing `0`. Rejected: excluding active route cards from architecture scope. Estimate: 0 us runtime.
- [x] Corrected artifact and tuning-input overclaims. DOD: absent active May 15 AgentLogs, missing screenshot readbacks, missing `GasGiantRotationDriver.cs`, absent CSV/tuning inputs, and global authority routes are now historical/pending/static-only where appropriate. Rejected: creating placeholder logs, screenshots, CSVs, or source files as fake proof. Estimate: 0 us runtime.
- [x] Regenerated atlas and ran static validation. DOD: atlas build/tests/py_compile, AtlasCheck, Mod API validator, JSON parse, R4 scan, source-anchor scan, markdown link scan, stale-current scan, project-file/filesystem check, and scoped diff-check results are recorded below. Rejected: claiming Unity/runtime/profiler/player-build proof. Estimate: 0 us runtime.

## R35 Validation

- `python Tools\BuildArchitectureAtlas.py`: exit `0`; regenerated `Docs/DEPENDENCY_GRAPH.md` and `Docs/DEPENDENCY_GRAPH.json`.
- `python Tools\test_architecture_atlas.py`: exit `0`, `10` tests OK.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: exit `0`.
- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6653 missing=58`; missing set is one Dynamic Decals missing vendor asset ref plus RealtimeCSG vendor icon/readme image refs.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: exit `0`, `Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- Active non-archive Docs JSON parse: `JsonFiles=132`, `Ok=132`, `Bad=0`.
- Root/architecture R4 marker scan: `ScopeFiles=105`, `Missing=0`, `Duplicate=0`.
- Architecture source-anchor filesystem scan: `SourceAnchorPathsChecked=256`, `Missing=0`.
- Local markdown link scan over root/architecture/report entrypoint scope: `ScopeFiles=94`, `MarkdownLinksChecked=54`, `Missing=0`.
- Targeted stale-current scan for old AtlasCheck tuples, validation-pending wording, R34-first current boundaries, active AgentLogs proof wording, and visual-readback proof wording: no hits.
- Project-file/filesystem check: `ChemicalInfluenceGrid.cs` exists; `LogisticsPipeEvents.cs`, `HectonWaterPhysics.cs`, and `HectonWaterPhysicsEditor.cs` are absent while still referenced by generated project files.
- Scoped `git diff --check -- Docs Tools BUILD_PLAYTEST_ISSUES.md MASTER_RELEASE_WORK_PLAN.md ':!Docs/Tasks/*' ':!Docs/AgentLogs/*' ':!Docs/Archive/**' ':!Docs/Modding/**'`: exit `0`, line-ending warnings only.

## R35 Current Blockers

- No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, platform run, campaign telemetry, analytics endpoint, network send, or visual-route proof exists for R35.
- `Tools/AtlasCheck.py` remains red on `58` missing refs: one Dynamic Decals missing vendor asset ref plus RealtimeCSG vendor icon/readme image refs.
- Project-file stale includes remain: `Hecton8.Core.csproj` references absent `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`; `Assembly-CSharp.csproj` references absent `Assets/_Project/_Archive/HectonWaterPhysics.cs` and `Assets/_Project/_Archive/HectonWaterPhysicsEditor.cs`.
- Optional/pending artifacts remain absent and are documented as absent: root visual-readback screenshots, `Assets/_Project/Scripts/GasGiantRotationDriver.cs`, and multiple architecture CSV/tuning inputs.
