# DOC_GLOBAL_DOCS_REFRESH Rationale

Date: 2026-05-19
Status: ACTIVE R35 / PRIOR HISTORY ARCHIVED

Prior full rationale history is archived at `Docs/Archive/Batch009/AgentLogs/Rationale_DOC_GLOBAL_DOCS_REFRESH.md`. The active file was absent during R27 closeout, so this file records the current live decision without rewriting archived rationale.

## Decision 35: R35 Root / Architecture R4, Artifact, And Validation Residue

Problem: After R34, active root/architecture docs still had a R35 report present but several entrypoints began at R34 or described R35 validation as pending. Active architecture body notes still carried old R34/R33 headings, and the current AtlasCheck gate changed after atlas regeneration. Root release/playtest docs cited absent active `Docs/AgentLogs` proof paths, absent screenshots, and an absent `GasGiantRotationDriver.cs` source path. Several architecture docs treated absent CSV/tuning inputs too close to live sources. Global authority route wording could still be read as runtime proof when only static source visibility existed.

Solution: Treat R35 as a static documentation/currentness correction layer. Promote R35 through root and architecture entrypoints; add missing R4/source-anchor coverage to SHINOBU_138 and SHINOBU_160; regenerate the atlas; update current AtlasCheck wording to `ATLAS_CHECK_FAIL references=6653 missing=58`; demote absent active AgentLogs, screenshots, source files, and CSV/tuning inputs to historical/pending/static-only wording; add global-authority proof caveats for `GlobalRegistry`, `SignalBus<T>`, `GlobalSignals`, `HectonEventBus`, and `GlobalDataVault`; and record R35 validation in the R35 report/status/log.

Rejected Alternatives: Leaving R35 validation as pending was rejected after the static validation suite ran. Keeping R34 as the first read-order boundary was rejected because R35 exists and changed active docs. Creating placeholder screenshots, CSVs, vendor assets, AgentLogs, `GasGiantRotationDriver.cs`, `LogisticsPipeEvents.cs`, or archived water physics files was rejected as fake evidence. Treating static source, atlas, or Mod API validator output as Unity/runtime/profiler/player-build proof was rejected because no such proof was run.

Scalability potential: Low-tier readers get exact current boundaries and no longer chase absent artifacts as proof. Middle-tier review gets source anchors and optional tuning inputs separated from runtime evidence. High/Ultra review can focus on real Unity import, player-build, profiler/GC, AtlasCheck vendor cleanup, and project-file stale include cleanup instead of re-auditing documentation trust.

Hardware Impact: 0 us/frame. Documentation/tooling only. No runtime optimization or microsecond saving is claimed without profiler evidence.

Evidence Class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC / READ_ONLY_SUBAGENT_AUDIT. Runtime verification remains PENDING VERIFICATION.

## Decision 34: R34 Root / Architecture Source-Counter Refresh

Problem: After R33, active root/architecture docs still depended on R27 for physical-line/source-counter truth, several entrypoints temporarily described the R34 report as absent, active architecture docs carried `ATLAS_CHECK_FAIL references=6677 missing=57` after a fresh atlas reported `references=6705 missing=57`, four architecture route/platform docs lacked R4 actuality boundaries, and source-anchor sections included expected future dump artifacts as if they were present source paths. Project-file wording also risked preserving stale claims that `ChemicalInfluenceGrid.cs` was missing even though the file exists.

Solution: Treat R34 as a static root/architecture source-counter refresh. Capture current source/file/interface/asmdef/signal counts; write `Docs/Reports/2026-05-19_DOCUMENTATION_R34_ROOT_ARCHITECTURE_SOURCE_COUNTER_REFRESH_LOCAL.md`; promote R34 through root and architecture entrypoints; update active AtlasCheck wording to `ATLAS_CHECK_FAIL references=6705 missing=57`; add R4 boundaries to the platform portability, hydrodynamic KCC, dynamic light culling, and vehicle damage route docs; separate future dump artifacts from source-anchor sections; regenerate the atlas; and record current project-file blockers exactly.

Rejected Alternatives: Preserving R27 as the latest physical-line/source-counter snapshot was rejected because R34 deliberately recaptured those counters. Leaving "R34 absent/restored" wording was rejected after the report was created. Creating placeholder RealtimeCSG icons, dump files, `LogisticsPipeEvents.cs`, or archive physics files was rejected as fake evidence. Claiming Unity/runtime/profiler/player-build proof was rejected because none was run.

Scalability potential: Low-tier readers get a single current root/architecture source scale and do not chase absent report wording. Middle-tier review gets verified source anchors and exact project-file blockers. High/Ultra review can focus on actual Unity import, runtime proof, AtlasCheck vendor-reference cleanup, and project-file stale include cleanup instead of rechecking documentation authority.

Hardware Impact: 0 us/frame. Documentation/tooling only. No runtime optimization or microsecond saving is claimed without profiler evidence.

Evidence Class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC / READ_ONLY_SUBAGENT_AUDIT. Runtime verification remains PENDING VERIFICATION.

## Decision 27: R27 Root / Architecture Index Counter Correction

Problem: After R26, active root/architecture/index documents still presented R26 as the latest DOC_GLOBAL boundary. Source physical-line counters drifted again, `Docs/AgentLogs/HPhi_SHINOBU_02_current2.json` was absent after Batch009 archival, generated atlas JSON did not expose the AtlasCheck red gate, Archivarius indexes still had R24/R26 current wording, and global-authority docs carried mismatched `5872 / 578` counters beside a newer `5871 / 606` recapture.

Solution: Treat R27 as a static root/architecture/index correction layer. Promote `Docs/Reports/2026-05-19_DOCUMENTATION_R27_ROOT_ARCHITECTURE_INDEX_COUNTER_LOCAL.md` as current DOC_GLOBAL root/architecture/source-counter boundary; recapture source counters; update root, architecture, Reports, and Archivarius entrypoints; replace active HPhi artifact references with `Docs/Archive/Batch009/AgentLogs/HPhi_SHINOBU_02_current2.json`; patch `Tools/BuildArchitectureAtlas.py` to emit `artifacts.atlas_check_status`; regenerate atlas markdown/json; and record R27 validation.

Rejected Alternatives: Preserving R26 as current was rejected because R27 source-line counters now read `1204221 / 1184559 / 1199376`, and active indexes would misroute readers. Creating placeholder HPhi or RealtimeCSG files was rejected as fake evidence. Claiming Unity/runtime/profiler/player-build proof was rejected because none was run. Mutating historical archive bodies was rejected; only active entrypoints and current report/tooling were updated.

Scalability potential: Low-tier readers get exact red gates and avoid false proof language. Middle-tier review gets current source scale and active read order. High/Ultra review can target real Unity import, profiler, player build, ModCommand size repair, and RealtimeCSG reference cleanup rather than re-auditing documentation trust.

Hardware Impact: 0 us/frame. Documentation/tooling only. No runtime optimization or microsecond saving is claimed without profiler evidence.

Evidence Class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC / READ_ONLY_SUBAGENT_AUDIT. Runtime verification remains PENDING VERIFICATION.

## Decision 33: R33 Root / Architecture R32 Residue, Source Anchors, And Duplicate-Body Cleanup

Problem: After R32, active root/architecture docs still contained stale R32-as-current headings, old AtlasCheck `59` wording, route cards without R4 boundaries, source-anchor gaps, stale `SceneBootstrap` ownership wording, binary payload path confusion, absent CSV/input-profile claims, SteamDB-as-proof wording, and repeated pasted bodies in several architecture docs. A regenerated atlas changed the current AtlasCheck red gate from `59` to `57`, limited to RealtimeCSG vendor icon/readme image refs.

Solution: Treat R33 as a static root/architecture actuality layer. Promote `Docs/Reports/2026-05-19_DOCUMENTATION_R33_ROOT_ARCHITECTURE_R32_RESIDUE_SOURCE_ANCHORS_LOCAL.md` as latest; correct root and architecture entrypoints; anchor scene readiness to `GameBootstrapper`, `SceneGuard`, and `WorldLODSceneBootstrap`; add R4/source-anchor boundaries to First 20, Autopilot, and Buoyancy route docs; trim repeated pasted architecture bodies; verify `221` source-anchor paths; regenerate the architecture atlas; and update current AtlasCheck wording to `57` RealtimeCSG-only missing refs.

Rejected Alternatives: Leaving R32 headings in place was rejected because R33 edits and the R33 report exist. Preserving `59` AtlasCheck wording was rejected after regenerated `Tools\AtlasCheck.py` output reported `references=6671 missing=57`. Creating placeholder vendor icons, CSVs, or source files was rejected as fake evidence. Claiming Unity/runtime/profiler/player-build proof was rejected because none was run. Recapturing physical source-line counters was rejected because this pass corrected root/architecture documentation interiors; R27 remains the latest deliberate physical-line counter snapshot.

Scalability potential: Low-tier readers get one current root/architecture authority chain and no longer chase duplicated docs or stale missing-path blockers. Middle-tier review can use source anchors without filesystem guessing. High/Ultra review can focus on real Unity/runtime evidence and the remaining RealtimeCSG atlas reference cleanup.

Hardware Impact: 0 us/frame. Documentation/tooling only. No runtime optimization or microsecond saving is claimed without profiler evidence.

Evidence Class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC. Runtime verification remains PENDING VERIFICATION.

## Decision 32: R32 Architecture R4 Chain And Proof-Wording Correction

Problem: After R31, active root/architecture docs still had interior evidence drift: May 17 actuality manifests were labeled current, one route card used `STATIC GREEN` without a GREEN artifact tuple, `PDA_ENCYCLOPEDIA_STREAMER.md` and `PROCEDURAL_WRECKAGE_GLOBAL_AUTHORITY_ROUTE_CARD_SHINOBU_121.md` lacked R4 boundaries, several runtime-contract docs lacked local source anchors, Subnautica2 sections used current-proof wording for static snapshots, and concurrent SHINOBU_02 edits temporarily demoted R32 as absent until an R32 artifact existed.

Solution: Treat R32 as a static root/architecture R4-chain/proof-wording layer. Create `Docs/Reports/2026-05-19_DOCUMENTATION_R32_ARCHITECTURE_R4_AND_PROOF_WORDING_LOCAL.md`; promote R32 through root/architecture/report entrypoints; add R4/current-boundary text; add local source anchors; demote historical manifest and current-proof wording; update Mod API static validator tuple to `Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`; and keep AtlasCheck red.

Rejected Alternatives: Leaving R31 as latest after R32 edits was rejected because the R32 report now exists and active docs changed. Preserving `STATIC GREEN` was rejected because no GREEN review artifact tuple was linked. Treating static snapshots, temporary Roslyn checks, or Mod API PASS as Unity/runtime proof was rejected because no Unity import, Play Mode, profiler, GCMonitor, player build, or mod runtime smoke was run. Recapturing physical source-line counters was rejected because this pass corrected root/architecture documentation interiors; R27 remains the latest deliberate physical-line counter snapshot.

Scalability potential: Low-tier readers get exact current boundaries and do not chase absent R32 or historical manifest proof. Middle-tier review can distinguish static source anchors from runtime readiness. High/Ultra review can focus on real Unity/runtime evidence and AtlasCheck missing references.

Hardware Impact: 0 us/frame. Documentation/tooling only. No runtime optimization or microsecond saving is claimed without profiler evidence.

Evidence Class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC / READ_ONLY_SUBAGENT_AUDIT. Runtime verification remains PENDING VERIFICATION.

## Decision 28: R28 Root / Architecture Interior Boundary Correction

Problem: After R27, 25 active architecture documents still had only generic R4 actuality text and did not carry an explicit current DOC_GLOBAL root/architecture boundary note. The active HFI report also lacked a R4/R28 boundary. A live Mod API static validator rerun changed the current red-gate state: the prior missing `ModCommand` sequential-size blocker is now repaired and the validator passes.

Solution: Add explicit R28 interior notes to the 25 architecture files, promote R28 through the root/architecture/report entrypoints, add a R4/R28 boundary to the HFI report, write `Docs/Reports/2026-05-19_DOCUMENTATION_R28_ROOT_ARCHITECTURE_INTERIOR_BOUNDARY_LOCAL.md`, and update active docs to record Mod API static validation as PASS while keeping `Tools/AtlasCheck.py` red on RealtimeCSG vendor references. R27 remains the latest source-counter/index boundary.

Rejected Alternatives: Leaving R27 as latest overall boundary was rejected because R28 changed active architecture interiors. Keeping stale Mod API red text was rejected after the validator returned `Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, and `ModCommandSizeBytes=64`. Recapturing source counters was rejected for this pass because the user scoped root/architecture interior docs; R27 counters remain the latest deliberate counter capture. Claiming Unity/runtime/profiler/player-build proof was rejected because none was run.

Scalability potential: Low-tier readers get current red/green gates without chasing stale ModCommand text. Middle-tier reviewers can distinguish R28 interior docs from R27 source counters. High/Ultra review can focus on real AtlasCheck vendor-reference cleanup and actual Unity/runtime validation.

Hardware Impact: 0 us/frame. Documentation/tooling only. No runtime optimization or microsecond saving is claimed without profiler evidence.

Evidence Class: STATIC_DOC / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC. Runtime verification remains PENDING VERIFICATION.

## Decision 29: R29 Root / Architecture Stale Gate + Global Authority Correction

Problem: After R28, active root/architecture surfaces still contained stale current-red Mod API validator wording, two SHINOBU docs still referenced the R27 root/architecture correction, global-authority docs still carried R27 boundary headings, `GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md` allowed `GREEN` with a proof plan, `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md` titled a proposed example as accepted, and `TRAUMA_GLITCH_SYSTEM.md` overclaimed static code validation as compile/Console/GC truth.

Solution: Treat R29 as a static stale-gate/global-authority correction layer. Promote `Docs/Reports/2026-05-19_DOCUMENTATION_R29_ROOT_ARCHITECTURE_STALE_GATE_GLOBAL_AUTHORITY_LOCAL.md` as the latest root/architecture DOC_GLOBAL boundary; update active Mod API static validator wording to PASS; retain AtlasCheck as red; tighten global-authority `GREEN` semantics so runtime-facing routes require evidence artifacts; demote trauma proof language; and scope signal/dispatch inventory wording to the R27 source-counter snapshot.

Rejected Alternatives: Recapturing all source counters was rejected for this pass because the objective drift was stale wording and proof semantics, not a requested source-counter pass; R27 remains the latest deliberate counter snapshot. Leaving R28 as latest was rejected because R29 changed active entrypoints and architecture interiors. Treating a proof plan as proof was rejected because runtime-facing global routes need concrete compile/Console/profiler/GC/player artifacts. Claiming Unity/runtime/profiler/player-build proof was rejected because none was run.

Scalability potential: Low-tier readers get correct current gates and do not chase a repaired ModCommand blocker. Middle-tier reviewers can distinguish static validator proof from mod runtime proof. High/Ultra review can spend time on actual AtlasCheck vendor-reference cleanup, Unity import/runtime proof, and global-authority route evidence instead of re-litigating stale documentation semantics.

Hardware Impact: 0 us/frame. Documentation/tooling only. No runtime optimization or microsecond saving is claimed without profiler evidence.

Evidence Class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC / READ_ONLY_SUBAGENT_AUDIT. Runtime verification remains PENDING VERIFICATION.

## Decision 30: R30 Root / Architecture Internal Currentness Correction

Problem: After R29, active root/architecture documents still contained internal currentness drift: many architecture files described R28 as the current boundary, global-authority headings still named R28, root/report indexes labeled old May 4/May 7/May 13 slices as latest/current, `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` cited an absent active BinaryHygiene artifact path, and repeated `Status=PASS` wording could be reused as proof without artifact tuple.

Solution: Treat R30 as a static root/architecture internal-currentness layer. Promote `Docs/Reports/2026-05-19_DOCUMENTATION_R30_ROOT_ARCHITECTURE_INTERNAL_CURRENTNESS_LOCAL.md` as the latest root/architecture DOC_GLOBAL boundary; keep R29 as prior stale-gate/global-authority correction; demote R28 to prior interior-boundary correction; keep R27 as latest source-counter snapshot; correct stale latest/current labels; cite the archived BinaryHygiene artifact path; and require artifact path, command, timestamp, environment, and output before static PASS text can be reused as proof.

Rejected Alternatives: Recapturing all source counters was rejected because this pass corrected proof/currentness wording, not source scale; R27 remains the latest deliberate counter snapshot. Claiming the Mod API PASS as runtime proof was rejected because no mod runtime smoke was run. Creating placeholder BinaryHygiene or RealtimeCSG files was rejected as fake evidence. Expanding into AI/Fauna and Flora findings was deferred because the user scoped this pass to root/architecture docs.

Scalability potential: Low-tier readers get exact proof limits and do not chase old R28/May labels as current. Middle-tier reviewers can distinguish R30 internal-currentness from R27 source counters and R29 stale-gate fixes. High/Ultra review can focus on actual Unity/runtime proof and the RealtimeCSG atlas blocker.

Hardware Impact: 0 us/frame. Documentation/tooling only. No runtime optimization or microsecond saving is claimed without profiler evidence.

Evidence Class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC / READ_ONLY_SUBAGENT_AUDIT. Runtime verification remains PENDING VERIFICATION.

## Decision 31: R31 Architecture Current-Boundary Propagation

Problem: After R30, active root/architecture documentation still had propagated residue: many architecture body notes named R30 or earlier as current once R31 work existed, root/report entrypoints still carried May 3/May 11/May 17 latest/current wording, six global-authority headings lagged their body boundary, one Dispatch line still said R29 was global current, seven active architecture docs lacked R4 actuality boundaries, and several active docs cited absent or shorthand source/artifact paths.

Solution: Treat R31 as a static root/architecture current-boundary propagation layer. Promote `Docs/Reports/2026-05-19_DOCUMENTATION_R31_ARCHITECTURE_CURRENT_BOUNDARY_PROPAGATION_LOCAL.md` as latest; demote R30 to prior internal-currentness correction, R29 to prior stale-gate/global-authority correction, and R28 to prior interior-boundary correction; add the seven missing R4 boundaries; demote old latest/current report language; correct absent `SaveCompressionDictionary.cs`, missing May 3 batch artifact, missing orphaned-script CSV, and shorthand haptics/performance source paths; and keep Mod API PASS as static-tool orientation only without standalone artifact tuples.

Rejected Alternatives: Leaving R30 as current after writing R31 was rejected because it would immediately stale the active entrypoints. Creating placeholder artifacts or source files was rejected as fake evidence. Recapturing source counters was rejected because this pass corrected root/architecture documentation currentness; R27 remains the latest deliberate counter snapshot. Claiming Unity/runtime/profiler/player-build proof was rejected because none was run.

Scalability potential: Low-tier readers get a single current authority chain and do not chase absent logs or old May "latest" rows. Middle-tier review can distinguish R31 documentation currentness from R27 source counters. High/Ultra review can spend time on actual Unity/runtime evidence and the RealtimeCSG atlas blocker instead of re-auditing stale documentation routing.

Hardware Impact: 0 us/frame. Documentation/tooling only. No runtime optimization or microsecond saving is claimed without profiler evidence.

Evidence Class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC / READ_ONLY_SUBAGENT_AUDIT. Runtime verification remains PENDING VERIFICATION.
