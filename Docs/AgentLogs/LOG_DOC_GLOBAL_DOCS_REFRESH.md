# DOC_GLOBAL_DOCS_REFRESH Log

Date: 2026-05-19
Status: ACTIVE R27 STUB / PRIOR HISTORY ARCHIVED

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
