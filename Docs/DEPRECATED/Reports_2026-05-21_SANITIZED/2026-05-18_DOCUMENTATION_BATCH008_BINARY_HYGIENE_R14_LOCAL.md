<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# Documentation Batch008 Binary Hygiene R14 Local

Date: 2026-05-18
Agent: DOC_GLOBAL_DOCS_REFRESH
Domain: Echelon 9.83 Chronicler / Project Documentation Currency
Status: STATIC DOC / FILESYSTEM EVIDENCE, RUNTIME PENDING VERIFICATION
Evidence class: STATIC_DOC / FILESYSTEM / READ_ONLY_SUBAGENT_AUDIT / PY_TOOL / POWERSHELL_STATIC

## Scope

R14 updated active documentation that still treated pre-Batch008 binary hygiene rows as current truth.
Historical reports remain provenance. Active entry points and stable ledgers now point readers to the
Batch008 archived evidence instead of active `Docs/AgentLogs` paths.

## Batch008 Evidence

- Current hygiene artifact: `Docs/Archive/Batch008/AgentLogs/BinaryHygiene_H8BIN_GRAVEYARD_AUDITOR_RECHECK2.json`.
- Status: `BINARY_HYGIENE_FAILED`.
- Alignment: 16 bytes.
- Global verifier scope: 65 `.bin` / `.h8bin` files.
- Misaligned files: 16.
- Product misalignment: `Data/Balance/Baked/Babel_Dictionary.h8bin`, 1295 bytes, remainder 15.
- Other misalignments: 15 Bakery editor/plugin fixtures under `Assets/Editor/x64/Bakery`.
- Product/generated reference scan: `Docs/Archive/Batch008/AgentLogs/H8BIN_GRAVEYARD_AUDITOR_ReferenceScan.csv`.
- Reference scan rows: 47, with `.bin=27`, `.h8bin=19`, `.bytes=1`; aligned 46, unaligned 1; code references 10, no code references 37.
- Archive manifests: initial move 320 items, late move 41 items, junk sweep moved 84 items.
- Junk sweep blocked 2 locked active files and wrote 2 locked snapshots.
- Locked files still active after archive move: `Docs/AgentLogs/QA_Endurance_Log.csv` and `Docs/AgentLogs/Unity_SHINOBU_38_Run_final_exitprocess.log`.
- Current active folder spot check during R14: `Docs/AgentLogs` 9 files, `Docs/Tasks` 4 files. The Batch008 log line that active folders were empty is only true for the earlier move moment, not the current workspace.

## Documents Updated

- `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`: routed H8BIN evidence to Batch008 archive paths and added the archive movement log.
- `Docs/Modding/Net_Protocol_v1.md`: demoted the old aligned-payload count 46, binary-file count 46, and unaligned-count 0 rows from current locks to historical 2026-05-17 rows superseded by Batch008 RECHECK2.
- `Docs/ARCHITECTURE/COOP_MERKLE_STATE_DELTA_PROTOCOL.md`: demoted the old economy/data-truth binary-unaligned-zero row to historical and added current Batch008 failure context.
- `Docs/Reports/Economy_DataTruth_Inquisition_LOOT_TABLE_ENTROPY_AUDIT.md`: changed the binary hygiene status from historical PASS wording to Batch008-superseded failure wording.
- `Docs/Reports/Nightly_Build_Report.md`: demoted historical binary hygiene/align scan rows and documented the missing `.codex-artifacts` validation logs.
- Affected metric/data-truth report rows were mechanically demoted from live-looking PASS labels to `HISTORICAL_PASS_SUPERSEDED...` or `HISTORICAL_STATIC_PASS_SUPERSEDED` where they referenced pre-Batch008 binary hygiene.

## Not Proven

R14 did not run Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler,
Frame Debugger, player build, save/load route, binary rebake, runtime payload load, Addressables
integration, or visual proof. No runtime microseconds are claimed.

## Validation

- `python Tools/test_architecture_atlas.py`: exit `0`, `9` tests OK.
- `powershell -NoProfile -ExecutionPolicy Bypass -File Docs\Modding\Validate_Mod_API_Static.ps1`: `Status: PASS`, schema revision `14`, source signals `170`.
- JSON parse: `JSON_OK 8` for dependency graph/cache, mod signal schema, active documentation actuality manifest, Batch008 binary hygiene, and Batch008 move manifests.
- Targeted pre-Batch008 binary-proof scan: no active non-archive markdown hits after R14 wording cleanup.
- Targeted Archivarius latest/current override scan: no stale R10/R13-latest hits in the scoped indexes.
- `python Tools/AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6457 missing=57`; all missing references remain RealtimeCSG vendor icon/readme image files.
- `git diff --check -- Docs Tools ':!Docs/Tasks/CURRENT_BATCH.md'`: exit `0`, line-ending warnings only.
