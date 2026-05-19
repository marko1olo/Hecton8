# 2026-05-19 Documentation R24 Root / Architecture Actuality

Date: 2026-05-19
Status: STATIC DOC/SOURCE UPDATE COMPLETE / RUNTIME PROOF ABSENT

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## R4 Interior Actuality Boundary

This report is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/README.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied. This is a static documentation/source/filesystem/tooling pass.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Scope

R24 focused on root documentation and `Docs/ARCHITECTURE` authority surfaces. Archivarius and forensic entry points were patched only where they route readers into root/architecture truth.

## R24 Static Source Snapshot

PowerShell static scan at capture time:

- `Assets/_Project/**/*.cs`: `1815`.
- `Assets/_Project/Scripts/**/*.cs`: `1759`.
- First-party non-test C# files excluding `Assets/_Project/Tests*`: `1795`.
- Project/script/non-test physical lines: `1200142 / 1180569 / 1195404`.
- Broad `interface` token hits under `Assets/_Project`: `342`.
- Direct interface declaration lines under `Assets/_Project`: `267`.
- Direct public interfaces in `GlobalRegistryContracts.cs`: `62`.
- First-party asmdefs under `Assets/_Project`: `121`.
- `GlobalSignals.InitializeAllQueues()` direct `CreateQueue(...)` slots: `73`.
- `InitializeCategorySignalLanes()` typed `SignalBus<T>.EnsureInitialized()` lanes: `133`.

These are dirty-workspace static counters. They are not compile, Unity import, runtime, profiler, or player-build proof. The generated atlas uses its own newline-count method for source-line fields; this report uses the PowerShell physical-line method above.

## Corrections

- Promoted R24 as the current root/architecture DOC_GLOBAL boundary in root README, governance, root reference, global architecture map, Reports index, Archivarius indexes, and the forensic bundle README.
- Replaced R22 `1811 / 1755 / 1791` and `117` asmdef current-language blocks with the R24 snapshot above.
- Regenerated `Docs/DEPENDENCY_GRAPH.md/json` and fixed `Tools/BuildArchitectureAtlas.py` so git-listed but missing `.cs` paths are not counted as scanned source files.
- Changed atlas status wording from generation-implies-ready wording to a separate `AtlasCheck` gate requirement.
- Demoted architecture proof-language residue in `SHINOBU_41_Geological_Synthesis.md`, `SHINOBU_61_APEX_COGNITION.md`, co-op Merkle data-truth text, and Subnautica 2 static snapshot headings.
- Added R4/provenance boundaries to active architecture `.diff` provenance files without claiming those patches are current shader proof.

## Validation

- `python Tools\BuildArchitectureAtlas.py`: exit `0`; regenerated `Docs/DEPENDENCY_GRAPH.md` and `Docs/DEPENDENCY_GRAPH.json`.
- `Docs/DEPENDENCY_GRAPH.md`: generated atlas reports `1759` first-party C# source files under `Assets/_Project/Scripts/` and `121` first-party asmdefs under `Assets/_Project/`.
- `python Tools\test_architecture_atlas.py`: exit `0`, `10` tests OK.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: exit `0`.
- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6566 missing=57`; missing references remain RealtimeCSG vendor icon/readme image paths.
- JSON parse spot check: `JSON_OK=6`, `JSON_BAD=0`.
- R24 scoped root/architecture/report R4 marker scan, including active architecture `.diff` provenance files: `ScopeFiles=75`, `MissingCount=0`, `DuplicateCount=0`.
- R24 targeted stale root/architecture scan found no remaining actionable hits for old `1814 / 1758 / 1794` counters, old R24 `119` asmdef language, `ATLASCHECK REQUIRED`, `dotnet is not on PATH`, ambiguous `latest DOC_GLOBAL reports`, `SOURCE HARDENED`, `BUILD PASS`, or stale Subnautica 2 `Current Proof` headings.
- `git diff --check -- Docs Tools ':!Docs/Tasks/*' ':!Docs/AgentLogs/*' ':!Docs/Archive/**'`: exit `0`, line-ending warnings only.
- Wider `git diff --check -- Docs Tools ':!Docs/Tasks/CURRENT_BATCH.md'`: exit `1` due unrelated concurrent trailing whitespace in `Docs/Tasks/Status_SHINOBU_69.md`; not edited by this pass.

## Evidence Limits

- No Unity import.
- No Unity Console.
- No Play Mode.
- No profiler or GCMonitor.
- No Memory Profiler or Frame Debugger.
- No player build.
- No save/load route proof.
- No visual-route proof.
