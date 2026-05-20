# DOC_GLOBAL_DOCS_REFRESH Log

Date: 2026-05-19
Status: ACTIVE R33 / PRIOR HISTORY ARCHIVED

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

## R32 Architecture R4 And Proof-Wording Local

What was wrong: R31 did not close the next interior layer. Active architecture docs still labeled a May 17 actuality manifest as current, `SHINOBU_125_SCAVENGING_LOOT_ORACLE_ROUTE_CARD.md` used `STATIC GREEN` without a linked GREEN review artifact tuple, `PDA_ENCYCLOPEDIA_STREAMER.md` and the procedural-wreckage global-authority route card lacked R4 boundaries, several runtime-contract docs lacked local source anchors, Subnautica2 sections used current-proof wording for static snapshots, temporary Roslyn/net10 wording was too strong, and concurrent SHINOBU_02 edits marked R32 absent before the R32 report artifact existed.

What was done: Wrote `Docs/Reports/2026-05-19_DOCUMENTATION_R32_ARCHITECTURE_R4_AND_PROOF_WORDING_LOCAL.md`; promoted R32 through active root/architecture/report entrypoints; added R4/current-boundary wording; added source anchors for thermodynamics, flora sway, HFI, macro ecosystem, procedural wreckage, loot oracle, PDA streamer, voxel surface nets, and flora/fauna symbiosis; demoted the May 17 actuality manifest to historical snapshot wording; demoted `STATIC GREEN`; corrected Mod API static validator tuple to `SchemaRevision=16`, `SourceSignals=162`; and preserved runtime-proof absence.

Cinematic Cheats used: none. This was documentation evidence hygiene, not runtime simulation.

Exact Microseconds saved: 0 claimed. R32 did not run Unity/profiler/GCMonitor/player-build proof.

Validation: targeted stale-current/proof scan returned no hits; R4 marker scan `ScopeFiles=81`, missing `0`, duplicate `0`; local markdown link scan `ScopeFiles=81`, missing links `0`; `python Tools\test_architecture_atlas.py` passed `10` tests; active non-archive docs JSON parse `JsonFiles=131`, ok `131`, bad `0`; `Docs\Modding\Validate_Mod_API_Static.ps1` passed (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`); `python Tools\AtlasCheck.py` still fails with `ATLAS_CHECK_FAIL references=6549 missing=59`; scoped `git diff --check` excluding volatile task/log/archive/modding paths exited `0` with line-ending warnings only. Runtime proof remains absent.

## R33 Root / Architecture R32 Residue + Source Anchors Local

What was wrong: R32 was still visible as current inside active root/architecture interiors, route cards existed without R4 boundaries, several runtime-facing architecture docs lacked verified source anchors, stale `SceneBootstrap` wording pointed at a non-existent first-party file, binary payload docs mixed root `Data` payloads with absent `Assets/_Project/Data` mirrors, absent drone/input CSVs were described too strongly, SteamDB wording could read as HECTON-8 platform proof, and several active architecture files contained repeated pasted document bodies. Regenerated AtlasCheck output changed the current red gate from `59` to `57` missing refs.

What was done: Wrote `Docs/Reports/2026-05-19_DOCUMENTATION_R33_ROOT_ARCHITECTURE_R32_RESIDUE_SOURCE_ANCHORS_LOCAL.md`; promoted R33 through root and architecture entrypoints; corrected scene readiness to `GameBootstrapper`, `SceneGuard`, and `WorldLODSceneBootstrap`; corrected `SYSTEM_INTERCONNECT_MATRIX.md` event wording to `GameBootstrapperEventPayload`/listener/flush ownership; added source anchors and R4 boundaries to First 20, SHINOBU_157 Autopilot, and SHINOBU_158 Buoyancy route docs; verified `221` source-anchor paths; trimmed repeated architecture bodies; demoted absent CSV/input-profile and SteamDB proof wording; regenerated the atlas; and updated active AtlasCheck wording to `57` RealtimeCSG vendor icon/readme image refs only.

Cinematic Cheats used: none. This was documentation evidence hygiene, not runtime simulation.

Exact Microseconds saved: 0 claimed. R33 did not run Unity/profiler/GCMonitor/player-build proof.

Validation: `python Tools\BuildArchitectureAtlas.py` regenerated dependency graph markdown/json; `python Tools\test_architecture_atlas.py` passed `10` tests; atlas tools `py_compile` passed; `python Tools\AtlasCheck.py` failed with `ATLAS_CHECK_FAIL references=6671 missing=57` on RealtimeCSG vendor icon/readme image refs only; `Docs\Modding\Validate_Mod_API_Static.ps1` passed (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`); active non-archive Docs JSON parse `JsonFiles=131`, bad `0`; R4 marker scan `ScopeFiles=83`, missing `0`, duplicate `0`; source-anchor path scan `221`, missing `0`; duplicate heading-body scan `0`; scoped markdown links `53`, missing `0`; scoped `git diff --check` excluding volatile task/log/archive/modding paths exited `0` with line-ending warnings only. Runtime proof remains absent.

## R34 Root / Architecture Source-Counter Refresh Local

What was wrong: R33 corrected source-anchor and duplicate-body residue, but active root/architecture docs still treated R27 as the current physical-line/source-counter snapshot. Some entrypoints described the R34 report as absent after R34 work existed. Active architecture docs carried old AtlasCheck reference totals (`6677`) while the regenerated atlas reported `6705`. Four route/platform docs lacked R4 actuality boundaries. Two expected dump artifact paths were inside `Source Anchors` sections. Older compile-blocker wording could still imply `ChemicalInfluenceGrid.cs` was missing.

What was done: Wrote `Docs/Reports/2026-05-19_DOCUMENTATION_R34_ROOT_ARCHITECTURE_SOURCE_COUNTER_REFRESH_LOCAL.md`; promoted R34 through root README, governance, root reference, global architecture map, runtime plan, systems contracts, quality gates, project atlas, architecture README, actuality ledger, Reports README, and active architecture boundary notes; recaptured source counters (`1924 / 1867 / 1902` C# files, `1304459 / 1284763 / 1298736` physical lines, `344 / 339` interface-token hits, `269` direct interface declarations, `62` direct registry interfaces, `129 / 127` first-party asmdefs, `73 / 133 / 254` signal-lane counts); added R4 boundaries to `PLATFORM_PORTABILITY_PROOF_LADDER.md`, `SHINOBU_113_HYDRODYNAMIC_KCC_ROUTE_CARD.md`, `SHINOBU_151_DYNAMIC_POINT_LIGHT_CULLING_ROUTE_CARD.md`, and `Vehicle_Component_Damage_Router_SHINOBU_152.md`; split future dump paths out of source-anchor scans; corrected project-file blocker wording for `ChemicalInfluenceGrid.cs`, `LogisticsPipeEvents.cs`, `HectonWaterPhysics.cs`, and `HectonWaterPhysicsEditor.cs`; regenerated the atlas.

Cinematic Cheats used: none. This was documentation evidence hygiene, not runtime simulation.

Exact Microseconds saved: 0 claimed. R34 did not run Unity/profiler/GCMonitor/player-build proof.

Validation: `python Tools\BuildArchitectureAtlas.py` regenerated dependency graph markdown/json; `python Tools\test_architecture_atlas.py` passed `10` tests; atlas tools `py_compile` passed; `python Tools\AtlasCheck.py` failed with `ATLAS_CHECK_FAIL references=6705 missing=57` on RealtimeCSG vendor icon/readme image refs only; `Docs\Modding\Validate_Mod_API_Static.ps1` passed (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`); active non-archive Docs JSON parse `JsonFiles=132`, bad `0`; root/architecture R4 marker scan `ScopeFiles=104`, missing `0`, duplicate `0`; source-anchor filesystem scan `257`, missing `0`; local markdown link scan `ScopeFiles=104`, `MarkdownLinksChecked=62`, missing `0`; targeted stale-current scan returned `NO_HITS`; project-file/filesystem check found `ChemicalInfluenceGrid.cs` present while `LogisticsPipeEvents.cs`, `HectonWaterPhysics.cs`, and `HectonWaterPhysicsEditor.cs` are absent but still referenced; scoped `git diff --check` excluding volatile task/log/archive/modding paths exited `0` with line-ending warnings only. Runtime proof remains absent.

## R35 Root / Architecture R4, Artifact, And Validation Residue Local

What was wrong: R35 existed as a report but active root/architecture entrypoints still had R34-first read-order residue and validation-pending wording. One active architecture route card lacked a R4 boundary. AtlasCheck changed after regeneration from the prior R34 `6705 / 57` tuple to the current `6653 / 58` tuple. Root release/playtest docs cited absent active AgentLogs, absent screenshots, and absent `GasGiantRotationDriver.cs` too close to current proof. Several architecture docs treated absent CSV/tuning files as live inputs. Global authority route wording still needed artifact-tuple caveats.

What was done: Promoted R35 through root README, root reference, runtime plan, systems contracts, quality gates, architecture README, H-Phi static metric, actuality ledger, Reports README, `BUILD_PLAYTEST_ISSUES.md`, and `MASTER_RELEASE_WORK_PLAN.md`; added R4/source anchors to `ASYNCHRONOUS_TELEMETRY_EXPORTER_SHINOBU_160.md`; kept the SHINOBU_138 ChemicalInfluenceGrid boundary/source anchor; updated AtlasCheck wording to `ATLAS_CHECK_FAIL references=6653 missing=58`; demoted absent active AgentLogs, screenshots, `GasGiantRotationDriver.cs`, CSV/tuning inputs, and global-authority static routes to historical/pending/static-only evidence; wrote validation into `Docs/Reports/2026-05-19_DOCUMENTATION_R35_ROOT_ARCHITECTURE_R4_AND_COUNTER_RESIDUE_LOCAL.md`.

Cinematic Cheats used: none. This was documentation evidence hygiene, not runtime simulation.

Exact Microseconds saved: 0 claimed. R35 did not run Unity/profiler/GCMonitor/player-build proof.

Validation: `python Tools\BuildArchitectureAtlas.py` regenerated dependency graph markdown/json; `python Tools\test_architecture_atlas.py` passed `10` tests; atlas tools `py_compile` passed; `python Tools\AtlasCheck.py` failed with `ATLAS_CHECK_FAIL references=6653 missing=58` on one Dynamic Decals missing vendor asset ref plus RealtimeCSG vendor icon/readme image refs; `Docs\Modding\Validate_Mod_API_Static.ps1` passed (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`); active non-archive Docs JSON parse `JsonFiles=132`, ok `132`, bad `0`; root/architecture R4 marker scan `ScopeFiles=105`, missing `0`, duplicate `0`; source-anchor filesystem scan `256`, missing `0`; local markdown link scan `ScopeFiles=94`, `MarkdownLinksChecked=54`, missing `0`; targeted stale-current scan returned no hits; project-file/filesystem check found `ChemicalInfluenceGrid.cs` present while `LogisticsPipeEvents.cs`, `HectonWaterPhysics.cs`, and `HectonWaterPhysicsEditor.cs` are absent but still referenced; scoped `git diff --check` exited `0` with line-ending warnings only. Runtime proof remains absent.
