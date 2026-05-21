# 2026-05-21 Documentation R48 Root / Architecture Date Rollover, AtlasCheck, and Counter Refresh

Date: 2026-05-21
Agent: DOC_GLOBAL_DOCS_REFRESH
Scope: active root documentation, active `Docs/*.md` / `Docs/*.txt` entrypoints, active `Docs/ARCHITECTURE/*.md`, generated architecture atlas metadata, and DOC_GLOBAL status/rationale/log.
Evidence class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC.

## Boundary

R48 supersedes R47 only for active root/architecture documentation currentness, AtlasCheck red-state wording, generated atlas metadata, and volatile source-counter orientation.

R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction. Older R44 through R34 layers remain historical static correction layers where their exact claims differ from R48.

Runtime proof remains absent. This pass did not run Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, platform run, shader import, network send, or visual-route proof.

## Findings

- Active root and architecture entrypoints still presented R47 as the latest/current DOC_GLOBAL boundary after the local date rolled to 2026-05-21 and the R48 pass began.
- Live `python Tools\AtlasCheck.py` output changed from the R47 documented tuple to `ATLAS_CHECK_FAIL references=6861 missing=64`.
- The generated atlas tooling and tests hardcoded a stale `references=6779 missing=60` tuple.
- Active root/architecture source-counter orientation still carried the R47 tuple after current disk churn.
- Twenty-two active architecture documents created or exposed by concurrent domain work had no R48 boundary block.
- `Docs/Reports/README.md`, `MASTER_RELEASE_WORK_PLAN.md`, `BUILD_PLAYTEST_ISSUES.md`, `Docs/PROJECT_ATLAS.md`, `Docs/QUALITY_GATES.md`, `Docs/DOC_GOVERNANCE.md`, and architecture currentness ledgers still carried interior R47/R46/R42 current wording after the R48 boundary existed.
- `DATA_MONOLITH_H8BIN_SPEC.md` had a binary spec, but the runtime integration/readiness boundary was not separated into a stable architecture contract.

## Current Source-Scale Orientation

Static source scan under `Assets/_Project`:

- `ProjectCs=2153`
- `ScriptCs=2089`
- `NonTestCs=2125`
- `ProjectLines=1475407`
- `ScriptLines=1454328`
- `NonTestLines=1468456`
- `Asmdefs=152`
- `NonTestAsmdefs=150`
- `InterfaceHitsProject=343`
- `InterfaceHitsScripts=340`
- `InterfaceDeclsProject=317`
- `InterfaceDeclsScripts=315`
- `GlobalRegistryContractsPublicInterfaces=62`
- `GlobalRegistryHits=6230`
- `PublishSubscribeDirectCallLines=307`
- `SignalCorridorBroadHits=2656`
- `NativeHits=23919`
- `NativeQueueRefs=904`
- `CreateQueueSlots=73`
- `EnsureLanes=277`
- `ConfigureEnsure=505`
- `ScriptTypedLanes=1441`

These counters are volatile STATIC_SOURCE orientation only. They are not compile, Unity import, runtime, profiler, GC, player-build, or platform proof.

## AtlasCheck Red State

`python Tools\AtlasCheck.py` still exits `1`:

`ATLAS_CHECK_FAIL references=6861 missing=64`

Current missing-reference classes include:

- one Dynamic Decals vendor asset reference: `Assets/Dynamic Decals/Resources/Decal.obj`
- RealtimeCSG vendor icon/readme image references
- `Assets/_Project/Scripts/Editor/HectonMaskChannelPacker.cs`
- `Assets/_Project/Scripts/Editor/HectonMaterialChannelPackValidator.cs`
- `Assets/_Project/Scripts/Gameplay/HectonScannerProjectionState.cs`
- `Assets/_Project/Scripts/Habitat/Deformation/Editor/HabitatDamageBakePipeline.cs`
- `Assets/_Project/Scripts/IBuildPlacementRule.cs`
- `Assets/_Project/Scripts/PlacementGhost.cs`

The generated atlas remains STATIC_SOURCE only until `Tools\AtlasCheck.py` exits `0`.

## Validation Snapshot

- `python Tools\BuildArchitectureAtlas.py`: PASS.
- `python Tools\test_architecture_atlas.py`: PASS, `10` tests.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: PASS.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: PASS, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- Active non-archive/non-deprecated Docs JSON parse: `JsonFiles=197`, `Bad=0`.
- Active root/architecture/report-index R48 boundary scan: `R48BoundaryScope=144`, `Missing=0`.
- Active architecture route-card field scan including exact route labels: `RouteCardFiles=18`, `Missing=0`.
- Scoped root/architecture/status/rationale/log/generated-atlas `git diff --check`: PASS, line-ending warnings only.
- `python Tools\AtlasCheck.py`: FAIL, `ATLAS_CHECK_FAIL references=6861 missing=64`.

Runtime proof remains absent. No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, shader import, network send, or visual-route proof was run.
