# 2026-05-19 DOC_GLOBAL R29 Root / Architecture Stale Gate + Global Authority Correction

Date: 2026-05-19
Agent: DOC_GLOBAL_DOCS_REFRESH
Scope: root and `Docs/ARCHITECTURE` active documentation surfaces
Evidence class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC / READ_ONLY_SUBAGENT_AUDIT
Runtime status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-19 R29 Report Actuality Boundary

This report is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this report links a fresh evidence artifact.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Boundary

This is a local-only documentation currency pass. It updates active root/architecture docs and entrypoints where R28 left stale internal claims.

R29 does not recapture source counters. R27 remains the latest deliberate source-counter/index snapshot:

- `Assets/_Project/**/*.cs`: `1818`
- `Assets/_Project/Scripts/**/*.cs`: `1761`
- first-party non-test C# excluding `Assets/_Project/Tests*`: `1797`
- project/script/non-test physical lines: `1204221 / 1184559 / 1199376`
- broad `interface` token hits / direct interface declarations: `342 / 267`
- direct public interfaces in `GlobalRegistryContracts.cs`: `62`
- first-party asmdefs: `123`
- direct `GlobalSignals.CreateQueue(...)` slots: `73`
- typed `SignalBus<T>.EnsureInitialized()` lanes: `133`

R29 keeps the R28 tool boundary: `Tools/AtlasCheck.py` remains red on `57` RealtimeCSG vendor icon/readme references, while `Docs/Modding/Validate_Mod_API_Static.ps1` now passes (`Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`). The Mod API result is static validator proof only, not mod runtime proof.

## What Was Wrong

- Active root/architecture surfaces still described the Mod API static validator as blocked by missing `ModCommand` sequential size after R28 had already rerun it green.
- Two SHINOBU architecture pages pointed to the R27 root/architecture correction while carrying R28 static-gate text.
- Six `GLOBAL_AUTHORITY_*` docs still had an R27 boundary heading even though the active root/architecture boundary was R28 and this pass is now R29.
- `GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md` allowed `GREEN` with an evidence/proof plan. A proof plan is not proof for runtime-facing global routes.
- `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md` had an example titled accepted while its status remained `PROPOSED`.
- `TRAUMA_GLITCH_SYSTEM.md` implied code-level validation could prove compile and Unity Console cleanliness, and its GC/hot-path wording read too close to profiler truth.
- `GLOBAL_SIGNAL_CORRIDOR.md` and `DISPATCH_PIPELINE.md` listed source-scan facts without explicitly scoping them to the R27 source-counter snapshot.
- Root/report/architecture entrypoints still routed readers to R28 as the latest DOC_GLOBAL root/architecture boundary after R29 edits existed.

## Updated Documents

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ROOT_DOCS_REFERENCE.md`
- `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`
- `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`
- `Docs/SYSTEMS_CONTRACTS.md`
- `Docs/QUALITY_GATES.md`
- `Docs/PROJECT_ATLAS.md`
- `Docs/PROJECT_STATE_STATIC_XRAY.md`
- `Docs/ARCHITECTURE/README.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- `Docs/ARCHITECTURE/DISPATCH_PIPELINE.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_OPERATING_MODEL.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`
- `Docs/ARCHITECTURE/GLOBAL_SIGNAL_CORRIDOR.md`
- `Docs/ARCHITECTURE/SHINOBU_61_VOXEL_SURFACE_NETS.md`
- `Docs/ARCHITECTURE/SHINOBU_FLORA_FAUNA_SYMBIOSIS.md`
- `Docs/ARCHITECTURE/TRAUMA_GLITCH_SYSTEM.md`
- `Docs/Reports/README.md`

## Corrections

- Replaced stale current-red Mod API claims with the current static validator result: `Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`.
- Promoted active read order to R29 -> R28 -> R27 -> R26 -> R25 -> R24 -> R23 -> R22 -> subordinate layers.
- Marked R28 as the prior interior-boundary correction and R27 as the latest source-counter/index snapshot.
- Tightened `GREEN` route review: runtime-facing routes require attached evidence artifacts; a proof plan alone is `YELLOW` unless the change is documentation-only.
- Renamed the route-card example from accepted to proposed because its status is `PROPOSED` and no runtime evidence artifacts are attached.
- Demoted trauma GC/compile/Console wording to source-orientation only.
- Scoped signal and dispatch inventory wording to the R27 source-counter snapshot.

## Not Proven

R29 did not run Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, platform run, campaign telemetry, or visual-route proof.

R29 did not prove runtime zero-GC, frame time, visual quality, scene wiring, asset import health, or mod execution health.

## Validation

Validation results are recorded in `Docs/Tasks/Status_DOC_GLOBAL_DOCS_REFRESH.md` and `Docs/AgentLogs/LOG_DOC_GLOBAL_DOCS_REFRESH.md` for this pass.

Expected persistent blocker:

- `python Tools\AtlasCheck.py` remains red on `ATLAS_CHECK_FAIL references=6549 missing=57`, all current missing refs in the known RealtimeCSG vendor icon/readme image class.

STATUS: LOCAL STATIC DOC/SOURCE CORRECTION COMPLETE / RUNTIME PENDING VERIFICATION
