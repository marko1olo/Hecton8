# DOC_GLOBAL_DOCS_REFRESH Rationale

Date: 2026-05-19
Status: ACTIVE R27 STUB / PRIOR HISTORY ARCHIVED

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
