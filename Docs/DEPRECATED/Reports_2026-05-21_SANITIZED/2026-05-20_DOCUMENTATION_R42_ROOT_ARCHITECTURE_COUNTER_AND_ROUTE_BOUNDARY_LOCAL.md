# 2026-05-20 DOCUMENTATION R42 ROOT/ARCHITECTURE COUNTER AND ROUTE BOUNDARY LOCAL

Date: 2026-05-20
Status: STATIC VALIDATION COMPLETE / ATLASCHECK RED / RUNTIME PENDING
Owner: DOC_GLOBAL_DOCS_REFRESH
Evidence class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC

## Scope

R42 is a local root/architecture documentation correction. It updates active root and `Docs/ARCHITECTURE` entry points after R41 to remove stale R40/R41 read-order residue, unlinked DOC_AUDIT R42/R43/R45 proof-label wording, stale global-authority counters, missing R4 actuality boundaries on newly active architecture route docs, and capture-time runtime/playtest language that was too close to current proof.

This report does not claim Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, platform run, visual-route proof, or runtime performance proof.

## R42 Static Source Snapshot

Volatile source snapshot from the active dirty workspace:

- `Assets/_Project/**/*.cs`: `2029`
- `Assets/_Project/Scripts/**/*.cs`: `1970`
- non-test C# files excluding `Assets/_Project/Tests*`: `2003`
- project physical lines: `1382236`
- script physical lines: `1362107`
- non-test physical lines: `1375742`
- first-party asmdefs: `139`
- first-party asmdefs excluding test dirs: `137`
- broad `interface` token hits: `325` project-wide / `322` under scripts
- direct interface declaration lines: `302`
- direct public interfaces in `GlobalRegistryContracts.cs`: `66`
- `GlobalRegistry.` line hits under `Assets/_Project`: `6101`
- publish/subscribe line hits under `Assets/_Project`: `1200`
- native collection line hits under `Assets/_Project`: `16397`
- `GlobalSignals.cs` `NativeQueue<...>` refs: `116`
- `GlobalSignals.CreateQueue(...)` slots: `73`
- typed `SignalBus<T>.EnsureInitialized()` lanes inside `GlobalSignals.cs`: `135`
- `SignalBus<T>.Configure/EnsureInitialized` hits inside `GlobalSignals.cs`: `271`
- broader script-level typed-lane matches: `1328`

These numbers are orientation only. They are not compile, runtime, profiler, or quality-gate proof and must be rerun before exact planning use.

## Corrections

- Promoted active root/architecture current-boundary wording from R41 to this disk-backed R42 report.
- Corrected old R40-as-latest and R41-only read-order lines in root entry points, report indexes, `Docs/ROOT_DOCS_REFERENCE.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/QUALITY_GATES.md`, `Docs/PROJECT_ATLAS.md`, `Docs/PROJECT_STATE_STATIC_XRAY.md`, and architecture authority docs.
- Replaced stale R38/R40/R41 counter tuples where active docs presented them as current, especially `GlobalRegistry.`, publish/subscribe, native-collection, asmdef, and physical-line counters.
- Demoted unlinked DOC_AUDIT R42/R43/R45 wording to historical CLI/report text unless a report path plus artifact path, command, timestamp, environment, and output tuple exists.
- Added R4 actuality boundaries and static-proof limits to newly active architecture route/asset documents that were missing the standard boundary.
- Demoted root playtest/roadmap runtime-language rows that cited capture-time live logs, editor readbacks, subjective visual notes, or timing values without artifact tuples.

## Known Blockers

- `Tools/AtlasCheck.py` remains red after R42 atlas regeneration: `ATLAS_CHECK_FAIL references=6728 missing=58`, with one Dynamic Decals missing vendor asset reference plus RealtimeCSG vendor icon/readme image references.
- Bytecode proof now exists for the atlas tools in this pass; R41's pycache permission blocker no longer applies to R42.
- Runtime proof remains absent.

## Validation

- Atlas generator: PASS; regenerated dependency graph markdown and JSON.
- Atlas unit tests: PASS, `10` tests.
- Atlas tool bytecode compile: PASS, `3` files.
- Mod API static validator: PASS, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- Active non-archive/non-deprecated Docs JSON parse: PASS, `JsonFiles=157`, `Bad=0`, `Utf16Fallback=1`.
- Root/architecture/report-index R42 boundary scan: PASS, `ScopeFiles=125`, `Missing=0`.
- Targeted stale-current scan for R41/R40/current-proof residue in active root/architecture scope: no hits.
- AtlasCheck: FAIL, `ATLAS_CHECK_FAIL references=6728 missing=58`; this keeps the generated atlas STATIC_SOURCE only, not verified.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, platform run, visual-route proof, and runtime performance proof: NOT RUN.
