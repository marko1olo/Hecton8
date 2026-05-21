# DOC_GLOBAL R35 Root / Architecture R4 And Counter-Residue Correction

Date: 2026-05-19
Agent: DOC_GLOBAL_DOCS_REFRESH
Scope: `Docs` root entrypoints plus active `Docs/ARCHITECTURE` documents
Status: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC. Runtime proof remains PENDING VERIFICATION.

## Boundary

R35 supersedes R34 only for root/architecture documentation current-boundary residue, R4 route-card coverage, and stale counter wording in active entrypoints.

R34 remains the prior source-counter and physical-line refresh. R33 remains the prior R32-residue/source-anchor correction. R32 remains the prior R4/proof-wording correction. R31 remains the prior current-boundary propagation layer. R30 remains the prior internal-currentness layer. R29 remains the prior stale-gate/global-authority layer. R28 remains the prior interior-boundary layer. R27 is historical source-counter/index evidence superseded by R34.

This report does not prove Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, platform run, campaign telemetry, or visual route quality.

## Changes

- Added the missing R4 actuality boundary to `Docs/ARCHITECTURE/SHINOBU_138_CHEMICAL_INFLUENCE_GRID_ROUTE_CARD.md`.
- Added source-anchor wording for `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs`; the path exists on disk.
- Demoted the SHINOBU_138 verification paragraph to static source/filesystem orientation only; build/import remains absent.
- Corrected active root/X-Ray counter residue so the R34 counter pass is report-backed static source orientation, not volatile or superseded by the older SHINOBU_02 spot check.
- Promoted active root/architecture current-boundary text to R35, while preserving R34 as the prior source-counter/physical-line refresh.
- Corrected root `BUILD_PLAYTEST_ISSUES.md` and `MASTER_RELEASE_WORK_PLAN.md`: absent active `Docs/AgentLogs/...CurrentDisk53.log` / `...CurrentDiskBudgetGate22.json` references now point to archived Batch007 evidence and are historical CLI/static evidence only; missing screenshot/source anchors are demoted to historical/pending artifact references.
- Updated active AtlasCheck wording after atlas regeneration: current local result is `ATLAS_CHECK_FAIL references=6653 missing=58`, not the prior `6705 / 57` or read-only-audit `6705 / 123` tuples.
- Added the missing R4/source-anchor boundary to `Docs/ARCHITECTURE/ASYNCHRONOUS_TELEMETRY_EXPORTER_SHINOBU_160.md`.
- Tightened global authority proof wording in dispatcher/interconnect/drone/scavenging/autopilot docs: `GlobalRegistry`, `SignalBus<T>`, `GlobalSignals`, `HectonEventBus`, and `GlobalDataVault` visibility is `YELLOW` without owner, producer/consumer phase, capacity/overflow, failure/telemetry, and artifact tuple.
- Demoted absent CSV/tuning inputs in active architecture docs to optional/pending inputs: light culling, cable materials, tool hardware specs, decal material profiles, vehicle handling profiles, item volume specs, hull materials, terminal layouts, fabrication timings, apex predator stats, and biome atlas overrides.

## Validation

- `python Tools\BuildArchitectureAtlas.py`: exit `0`; regenerated `Docs/DEPENDENCY_GRAPH.md` and `Docs/DEPENDENCY_GRAPH.json`.
- `python Tools\test_architecture_atlas.py`: exit `0`, `10` tests OK.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: exit `0`.
- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6653 missing=58`; missing set is one `Assets/Dynamic Decals/Resources/Decal.obj` reference plus RealtimeCSG vendor icon/readme image refs.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: exit `0`, `Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- Active non-archive Docs JSON parse: `JsonFiles=132`, `Ok=132`, `Bad=0`.
- Root/architecture R4 marker scan: `ScopeFiles=105`, `Missing=0`, `Duplicate=0`.
- Architecture source-anchor filesystem scan: `SourceAnchorPathsChecked=256`, `Missing=0`.
- Local markdown link scan over root/architecture/report entrypoint scope: `ScopeFiles=94`, `MarkdownLinksChecked=54`, `Missing=0`.
- Project-file/filesystem check: `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs` exists; `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`, `Assets/_Project/_Archive/HectonWaterPhysics.cs`, and `Assets/_Project/_Archive/HectonWaterPhysicsEditor.cs` are absent while still referenced by generated project files.
- Static artifact checks: active `Docs/AgentLogs/...CurrentDisk53.log` / `...CurrentDiskBudgetGate22.json` copies are absent, Batch007 archive copies exist; listed visual-readback screenshots and `Assets/_Project/Scripts/GasGiantRotationDriver.cs` are absent in the current checkout and are historical/pending references only.

## Current Blockers

- No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, platform run, campaign telemetry, or visual-route proof exists for R35.
- `Tools\AtlasCheck.py` remains red on `58` missing refs: one Dynamic Decals missing vendor asset ref plus RealtimeCSG vendor icon/readme image refs.
- Project-file stale includes remain: `Hecton8.Core.csproj` references absent `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`; `Assembly-CSharp.csproj` references absent `Assets/_Project/_Archive/HectonWaterPhysics.cs` and `Assets/_Project/_Archive/HectonWaterPhysicsEditor.cs`.
- Several docs now intentionally list absent historical/pending artifacts as absent, not as proof: archived May 15 CLI/static logs, visual readback screenshots, `GasGiantRotationDriver.cs`, and optional CSV tuning inputs.
