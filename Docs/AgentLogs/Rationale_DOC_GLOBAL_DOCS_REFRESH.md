# DOC_GLOBAL_DOCS_REFRESH Rationale

Date: 2026-05-19
Status: ACTIVE R31 / PRIOR HISTORY ARCHIVED

Prior full rationale history is archived at `Docs/Archive/Batch009/AgentLogs/Rationale_DOC_GLOBAL_DOCS_REFRESH.md`. The active file was absent during R27 closeout, so this file records the current live decision without rewriting archived rationale.

## Decision 27: R27 Root / Architecture Index Counter Correction

Problem: After R26, active root/architecture/index documents still presented R26 as the latest DOC_GLOBAL boundary. Source physical-line counters drifted again, `Docs/AgentLogs/HPhi_SHINOBU_02_current2.json` was absent after Batch009 archival, generated atlas JSON did not expose the AtlasCheck red gate, Archivarius indexes still had R24/R26 current wording, and global-authority docs carried mismatched `5872 / 578` counters beside a newer `5871 / 606` recapture.

Solution: Treat R27 as a static root/architecture/index correction layer. Promote `Docs/Reports/2026-05-19_DOCUMENTATION_R27_ROOT_ARCHITECTURE_INDEX_COUNTER_LOCAL.md` as current DOC_GLOBAL root/architecture/source-counter boundary; recapture source counters; update root, architecture, Reports, and Archivarius entrypoints; replace active HPhi artifact references with `Docs/Archive/Batch009/AgentLogs/HPhi_SHINOBU_02_current2.json`; patch `Tools/BuildArchitectureAtlas.py` to emit `artifacts.atlas_check_status`; regenerate atlas markdown/json; and record R27 validation.

Rejected Alternatives: Preserving R26 as current was rejected because R27 source-line counters now read `1204221 / 1184559 / 1199376`, and active indexes would misroute readers. Creating placeholder HPhi or RealtimeCSG files was rejected as fake evidence. Claiming Unity/runtime/profiler/player-build proof was rejected because none was run. Mutating historical archive bodies was rejected; only active entrypoints and current report/tooling were updated.

Scalability potential: Low-tier readers get exact red gates and avoid false proof language. Middle-tier review gets current source scale and active read order. High/Ultra review can target real Unity import, profiler, player build, ModCommand size repair, and RealtimeCSG reference cleanup rather than re-auditing documentation trust.

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
