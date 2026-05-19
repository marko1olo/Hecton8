# Documentation R25 Root / Architecture Long-Tail Local

Date: 2026-05-19
Status: PENDING VERIFICATION
Evidence class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC / READ_ONLY_SUBAGENT_AUDIT

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-19 R25 Interior Actuality Boundary

This report is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this report links a fresh evidence artifact. Historical counters and older version claims are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Scope

R25 continued the root/architecture documentation refresh after R24. The pass focused on internal truth, not sorting:

- root authority: `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ROOT_DOCS_REFERENCE.md`, `Docs/PROJECT_STATE_STATIC_XRAY.md`, `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, `Docs/PROJECT_ATLAS.md`, `Docs/Reports/README.md`
- architecture authority: `Docs/ARCHITECTURE/README.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, signal/dispatch/H-Phi/binary payload docs, global authority docs, and SHINOBU architecture pages in the current root/architecture path
- active Archivarius entry points that mirror root/architecture counters

Archives and dated historical reports were not rewritten as live truth. Active indexes were corrected where they advertised stale evidence as current.

## R25 Static Snapshot

PowerShell static source scan after concurrent workspace churn:

- `Assets/_Project/**/*.cs`: `1818`
- `Assets/_Project/Scripts/**/*.cs`: `1761`
- first-party non-test C# files excluding `Assets/_Project/Tests*` and `Assets/_Project/Scripts/Tests*`: `1797`
- project/script/non-test physical lines: `1203180 / 1183533 / 1198350`
- broad `interface` token hits under `Assets/_Project`: `342`
- direct interface declaration lines under `Assets/_Project`: `268`
- direct public interfaces in `GlobalRegistryContracts.cs`: `62`
- first-party asmdefs under `Assets/_Project`: `121`
- direct `GlobalSignals.CreateQueue(...)` slots: `73`
- typed `SignalBus<T>.EnsureInitialized()` lanes in `GlobalSignals.cs`: `133`, including `DebugSignal`
- direct `Docs/` root markdown files: `16`

These values are capture-time static orientation only. They are not compile or runtime proof.

Generated atlas after R25:

- `Docs/DEPENDENCY_GRAPH.md`: generated `2026-05-19 02:17:58`
- atlas first-party C# source files under `Assets/_Project/Scripts/`: `1761`
- atlas first-party asmdefs under `Assets/_Project`: `121`
- `AtlasCheck` remains a separate failing gate, not implied by atlas generation

## Corrections

- Promoted R25 as the current DOC_GLOBAL root/architecture/source-counter boundary in root README, governance, root reference, global architecture map, reports README, architecture README, actuality ledger, and Archivarius entry points.
- Demoted May 14/R43 root `Hecton8*.csproj` wording from "current compile proof" to historical `CLI_COMPILE` output requiring rerun artifact path, command, timestamp, environment, and active-workspace output before current use.
- Added `Docs/ARCHITECTURE/AI_PACING_MODEL.md` to close the missing `AI_PACING_MODEL.md` reference from `HEADLESS_ECOSYSTEM_SIMULATION.md`.
- Added R4 actuality boundaries to `GLOBAL_AUTHORITY_BOUNDARIES.md`, `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`, and `SHINOBU_69_VOLUMETRIC_PLASMA_BEAM.md`.
- Updated `GLOBAL_SIGNAL_CORRIDOR.md`, `BOOT_SEQUENCE_TOPOLOGY.md`, and `HECTON8_GLOBAL_ARCHITECTURE_MAP.md` to use the R25 `73` direct queue / `133` typed lane orientation.
- Updated `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` and root README so static main-runtime wired product payloads are `Acoustic_LUT.bin`, `Water_Extinction_Matrix.bin`, and `Biolum_Profiles.bin`; runtime load proof remains absent.
- Demoted H-Phi, dispatch, co-op Merkle, SHINOBU static-scan, and signal-audit proof language where historical tool output could be read as current runtime or compile proof.
- Fixed `Docs/Actual Domains of Project.txt` typo `ELCHELON 8` to `ECHELON 8`.

## Validation

- `python Tools\BuildArchitectureAtlas.py`: exit `0`; regenerated `Docs/DEPENDENCY_GRAPH.md`, `Docs/DEPENDENCY_GRAPH.json`, and cache.
- `python Tools\test_architecture_atlas.py`: exit `0`; `10` tests OK.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: exit `0`.
- JSON parse spot check: `JSON_OK=4`, `JSON_BAD=0`.
- R25 scoped root/architecture/report R4 marker scan: `ScopeFiles=80`, `MissingCount=0`, `DuplicateCount=0`.
- R25 targeted stale scan over root/architecture authority surfaces found no actionable `current external root`, `Current R43`, false `SOURCE VERIFIED`, stale R24 counter snapshot, or old R24 physical-line hits after the patches.
- Scoped `git diff --check -- Docs Tools ':!Docs/Tasks/*' ':!Docs/AgentLogs/*' ':!Docs/Archive/**'`: exit `0`, line-ending warnings only.

## Blockers

- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6569 missing=57`; missing refs remain RealtimeCSG vendor icon/readme image paths.
- `powershell -NoProfile -ExecutionPolicy Bypass -File Docs\Modding\Validate_Mod_API_Static.ps1`: exit `1`; current failure is `[MOD_API_STATIC_VALIDATION] Missing ModCommand sequential size declaration.` This is a current Modding/static validator blocker, not a root/architecture runtime proof.
- No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform/device run, campaign telemetry, or visual-route proof was captured in R25.
- Source counters remain volatile under concurrent agents; rerun before using exact values as gates.
