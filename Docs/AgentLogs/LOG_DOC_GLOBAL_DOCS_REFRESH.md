# DOC_GLOBAL_DOCS_REFRESH Log

Date: 2026-05-19
Status: ACTIVE R31 / PRIOR HISTORY ARCHIVED

Prior full log history is archived at `Docs/Archive/Batch009/AgentLogs/LOG_DOC_GLOBAL_DOCS_REFRESH.md`. The active file was absent during R27 closeout, so this file records the current live completion without rewriting archived history.

## R27 Root / Architecture Index Counter Local

What was wrong: R26 was still advertised as latest in active root/architecture/report/Archivarius entrypoints after the user requested continued root/architecture documentation work. Source line counters had drifted, active HPhi paths pointed to files now under `Docs/Archive/Batch009`, generated atlas JSON lacked machine-readable AtlasCheck red-gate status, and global authority docs carried inconsistent recapture values.

What was done: Wrote `Docs/Reports/2026-05-19_DOCUMENTATION_R27_ROOT_ARCHITECTURE_INDEX_COUNTER_LOCAL.md`; promoted R27 through root README, governance, root reference, static X-Ray, global architecture map, runtime plan, systems contracts, quality gates, Reports README, architecture README/actuality ledger, H-Phi metric doc, global authority docs, signal corridor, dispatch/boot docs, SHINOBU pages, Archivarius indexes, and the HFI report; recaptured counters at `1818 / 1761 / 1797` C# files and `1204221 / 1184559 / 1199376` physical lines; fixed active HPhi archive paths; added generated atlas `atlas_check_status`; regenerated dependency graph markdown/json.

Cinematic Cheats used: none; documentation/tooling-only pass.

Exact Microseconds saved: 0 claimed. R27 did not run Unity/profiler/GCMonitor/player-build proof.

Validation: `BuildArchitectureAtlas.py` regenerated dependency graph markdown/json; atlas unit tests passed (`10`); atlas tools `py_compile` passed; JSON parse spot check passed (`5`, bad `0`, missing `0`); R27 scoped R4 scan `111`, missing `0`, duplicate `0`; generated atlas JSON exposes the current AtlasCheck red-gate status; `AtlasCheck` still fails with `ATLAS_CHECK_FAIL references=6549 missing=57` on RealtimeCSG vendor image/readme references; Mod API static validator still fails on `[MOD_API_STATIC_VALIDATION] Missing ModCommand sequential size declaration.`; scoped root/architecture `git diff --check` excluding volatile `Docs/Tasks`, `Docs/AgentLogs`, `Docs/Archive`, and unrelated `Docs/Modding` exited `0` with line-ending warnings only. Wider all-doc diff-check is red on unrelated concurrent trailing whitespace in four `Docs/Modding` files. Runtime proof remains absent.

## R28 Root / Architecture Interior Boundary Local

What was wrong: R27 was current for source counters/indexes, but 25 active architecture documents still carried only generic R4 actuality text without explicit current DOC_GLOBAL root/architecture blocker context. The active HFI report lacked a R4/R28 boundary. After validation, the previous Mod API static validator blocker was stale because `Validate_Mod_API_Static.ps1` now passes.

What was done: Wrote `Docs/Reports/2026-05-19_DOCUMENTATION_R28_ROOT_ARCHITECTURE_INTERIOR_BOUNDARY_LOCAL.md`; added explicit R28 interior notes to 25 active architecture docs; promoted R28 in root README, governance, root reference, global architecture map, runtime execution plan, systems contracts, quality gates, project atlas, architecture README, actuality ledger, and Reports README; added the missing R4/R28 boundary to the HFI report; changed active current-gate wording so Mod API static validation is PASS and only AtlasCheck remains red.

Cinematic Cheats used: none. This was documentation evidence hygiene, not runtime simulation.

Exact Microseconds saved: 0 claimed. R28 did not run Unity/profiler/GCMonitor/player-build proof.

Validation: R28 interior note scan `25`; scoped R4 scan `ScopeFiles=325`, missing `0`, duplicate `0`; scoped local markdown link scan `MissingCount=0`, `Files=0`, `ScopeFiles=157`; stale Mod API red-blocker scan over active root/architecture/report surfaces returned no hits; `python Tools\test_architecture_atlas.py` passed `10` tests; JSON parse spot check passed `5`, bad `0`, missing `0`; `python Tools\AtlasCheck.py` still fails with `ATLAS_CHECK_FAIL references=6549 missing=57`; `Docs\Modding\Validate_Mod_API_Static.ps1` now passes (`Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`); scoped root/architecture `git diff --check` excluding volatile task/log/archive/modding paths exited `0` with line-ending warnings only. Runtime proof remains absent.

## R29 Root / Architecture Stale Gate + Global Authority Local

What was wrong: Active root/architecture docs still contained stale current-red Mod API validator wording after the validator had passed. Several global-authority docs still carried R27 boundary headings, and the review checklist could be read as allowing `GREEN` with only a proof plan. `TRAUMA_GLITCH_SYSTEM.md` treated code-level validation too close to compile/Console/GC proof. R29 edits existed and therefore root/architecture entrypoints needed to stop advertising R28 as the latest DOC_GLOBAL boundary.

What was done: Wrote `Docs/Reports/2026-05-19_DOCUMENTATION_R29_ROOT_ARCHITECTURE_STALE_GATE_GLOBAL_AUTHORITY_LOCAL.md`; promoted R29 through root README, governance, root reference, global architecture map, runtime execution plan, systems contracts, quality gates, project atlas, architecture README, actuality ledger, and Reports README; corrected stale Mod API PASS/blocked wording in active surfaces; updated six `GLOBAL_AUTHORITY_*` docs; tightened `GREEN` review semantics; renamed the route-card example to proposed; demoted Trauma GC/compile/Console wording; scoped Signal Corridor and Dispatch inventory to the R27 source-counter snapshot.

Cinematic Cheats used: none. This was documentation evidence hygiene, not runtime simulation.

Exact Microseconds saved: 0 claimed. R29 did not run Unity/profiler/GCMonitor/player-build proof.

Validation: targeted stale red-gate/proof-plan scan returned no hits; disallowed stale R28-latest scan returned no disallowed hits; scoped R4 scan `ScopeFiles=162`, missing `0`, duplicate `0`; scoped local markdown link scan `ScopeFiles=162`, missing links `0`; `python Tools\test_architecture_atlas.py` passed `10` tests; JSON parse spot check passed `5`, bad `0`, missing `0`; `Docs\Modding\Validate_Mod_API_Static.ps1` passed (`Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`); `python Tools\AtlasCheck.py` still fails with `ATLAS_CHECK_FAIL references=6549 missing=57`; scoped `git diff --check` excluding volatile task/log/archive/modding paths exited `0` with line-ending warnings only. Runtime proof remains absent.

## R30 Root / Architecture Internal Currentness Local

What was wrong: After R29, active architecture files still used R28 as current boundary text, global-authority headings still named the R28 interior boundary, old May report entries in root/report indexes still used latest/current/clean wording, `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` cited absent active `Docs/AgentLogs/BinaryHygiene_SHINOBU_50.json`, and Mod API static `Status=PASS` wording lacked enough artifact-tuple caution for reuse as proof.

What was done: Wrote `Docs/Reports/2026-05-19_DOCUMENTATION_R30_ROOT_ARCHITECTURE_INTERNAL_CURRENTNESS_LOCAL.md`; promoted R30 through root README, governance, root reference, global architecture map, runtime plan, systems contracts, quality gates, project atlas, architecture README, actuality ledger, and Reports README; changed R28-as-current architecture wording to R29/R30 context; corrected six global-authority headings; demoted stale May latest/current labels; changed BinaryHygiene citation to `Docs/Archive/Batch009/AgentLogs/BinaryHygiene_SHINOBU_50.json`; changed acoustic LUT wording to static source/prefab evidence only; and added static-tool caveats to repeated Mod API PASS text.

Cinematic Cheats used: none. This was documentation evidence hygiene, not runtime simulation.

Exact Microseconds saved: 0 claimed. R30 did not run Unity/profiler/GCMonitor/player-build proof.

Validation: targeted stale R28/latest/proof-current scan returned `missing=0`; markdown/txt R4 marker scan `ScopeFiles=88`, missing `0`, duplicate `0`; local markdown link scan `ScopeFiles=88`, missing links `0`; `python Tools\test_architecture_atlas.py` passed `10` tests; JSON parse spot check passed `5`, bad `0`, missing `0`; `Docs\Modding\Validate_Mod_API_Static.ps1` passed (`Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`); `python Tools\AtlasCheck.py` still fails with `ATLAS_CHECK_FAIL references=6549 missing=57`; scoped `git diff --check` excluding volatile task/log/archive/modding paths exited `0` with line-ending warnings only. Runtime proof remains absent.

## R31 Architecture Current-Boundary Propagation Local

What was wrong: After R30, active root/architecture docs still had boundary propagation residue. Architecture notes and global-authority surfaces could still point at R30/R29/R28 as current, direct root/report entrypoints still carried old May 3/May 11/May 17 latest/current wording, `DISPATCH_PIPELINE.md` still said the global boundary was R29, seven active architecture docs lacked R4 actuality boundaries, and several docs cited absent or shorthand artifact/source paths.

What was done: Wrote `Docs/Reports/2026-05-19_DOCUMENTATION_R31_ARCHITECTURE_CURRENT_BOUNDARY_PROPAGATION_LOCAL.md`; promoted R31 through root README, governance, root reference, global architecture map, runtime plan, systems contracts, quality gates, project atlas, architecture README, actuality ledger, Reports README, and architecture body notes; corrected six `GLOBAL_AUTHORITY_*` headings; corrected Dispatch boundary text; demoted old May latest/current rows to historical evidence; marked missing May 3 batch artifact, missing orphaned-script CSV, absent `SaveCompressionDictionary.cs`, and shorthand haptics/performance source paths; and added R4 boundaries to seven active architecture documents.

Cinematic Cheats used: none. This was documentation evidence hygiene, not runtime simulation.

Exact Microseconds saved: 0 claimed. R31 did not run Unity/profiler/GCMonitor/player-build proof.

Validation: targeted stale-current/proof scan returned `missing=0`; markdown/txt R4 marker scan `ScopeFiles=89`, missing `0`, duplicate `0`; local markdown link scan `ScopeFiles=88`, missing links `0`; `python Tools\test_architecture_atlas.py` passed `10` tests; JSON parse spot check passed `5`, bad `0`, missing `0`; `Docs\Modding\Validate_Mod_API_Static.ps1` passed (`Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`); `python Tools\AtlasCheck.py` still fails with `ATLAS_CHECK_FAIL references=6549 missing=57`; scoped `git diff --check` excluding volatile task/log/archive/modding paths exited `0` with line-ending warnings only. Runtime proof remains absent.
