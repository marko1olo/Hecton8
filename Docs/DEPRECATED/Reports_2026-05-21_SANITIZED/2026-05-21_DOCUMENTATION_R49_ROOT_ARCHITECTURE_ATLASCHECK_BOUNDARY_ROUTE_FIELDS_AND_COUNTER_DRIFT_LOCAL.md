# 2026-05-21 Documentation R49 Root / Architecture AtlasCheck, Boundary, Route Fields, and Counter Drift

Date: 2026-05-21
Agent: DOC_GLOBAL_DOCS_REFRESH
Scope: active root documentation, active `Docs/*.md` / `Docs/*.txt` entrypoints, active `Docs/ARCHITECTURE/*.md`, generated architecture atlas metadata, and DOC_GLOBAL status/rationale/log.
Evidence class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC.

## Boundary

R49 supersedes R48 only for active root/architecture documentation currentness, AtlasCheck red-state wording, R49 boundary-gap closure, exact route-card field labels, and volatile source-counter orientation.

R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. Older R45 through R34 layers remain historical static correction layers where their exact claims differ from R49.

Runtime proof remains absent. This pass did not run Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, platform run, shader import, network send, or visual-route proof.

## Findings

- Active root and architecture entrypoints still presented R48 as the latest/current DOC_GLOBAL boundary after the R49 pass began.
- Live `python Tools\AtlasCheck.py` output changed from the R48 documented tuple to `ATLAS_CHECK_FAIL references=6861 missing=65`.
- The additional missing reference is `Assets/_Project/Scripts/Construction/AutonomousExtractorJobs.cs`, carried by the generated atlas cache.
- Three active architecture files lacked a complete current DOC_GLOBAL boundary at different points in the R49 pass: `Docs/ARCHITECTURE/SHINOBU_235_DEEP_SEA_NOIR_ROUTE_CARD.md`, `Docs/ARCHITECTURE/TERRAIN_CHUNK_PAGING_SYSTEM_SHINOBU_245.md`, and `Docs/ARCHITECTURE/BIOME_WEIGHT_MAP_BAKER_SHINOBU_243.md`.
- One active root-doc surface still carried the older R48 boundary after the R49 promotion: `Docs/TECH_ART_PBR_SURFACE_DOCTRINE.md`.
- Two route cards used a combined `Producer/consumer phase` label instead of exact `Producer phase` / `Consumer phase` fields: `Docs/ARCHITECTURE/SHINOBU_235_DEEP_SEA_NOIR_ROUTE_CARD.md` and `Docs/ARCHITECTURE/HADAL_TRENCH_PAYLOAD_ROUTE_CARD.md`.
- Root and architecture source-counter orientation still carried the R48 tuple after current disk churn.
- Several root roadmap/playtest lines use capture-time `clean`, `0 errors`, `completed`, or readback wording; R49 adds a current caveat that such rows are historical/capture-time unless they link artifact path, command/tool, timestamp, environment, and full output.

## Current Source-Scale Orientation

Static source scan under `Assets/_Project`:

- `ProjectCs=2152`
- `ScriptCs=2088`
- `NonTestCs=2124`
- `ProjectLines=1481474`
- `ScriptLines=1460382`
- `NonTestLines=1474507`
- `Asmdefs=154`
- `NonTestAsmdefs=152`
- `InterfaceHitsProject=353`
- `InterfaceHitsScripts=350`
- `InterfaceDeclsProject=277`
- `InterfaceDeclsScripts=276`
- `GlobalRegistryContractsPublicInterfaces=62`
- `GlobalRegistryHits=6223`
- `PublishSubscribeDirectCallLines=307`
- `SignalCorridorBroadHits=2685`
- `NativeHits=19574`
- `NativeQueueRefs=904`
- `CreateQueueSlots=73`
- `EnsureLanes=277`
- `ConfigureEnsure=505`
- `ScriptTypedLanes=1440`

These counters are volatile STATIC_SOURCE orientation only. They are not compile, Unity import, runtime, profiler, GC, player-build, or platform proof.

## AtlasCheck Red State

`python Tools\AtlasCheck.py` still exits `1`:

`ATLAS_CHECK_FAIL references=6861 missing=65`

Current missing-reference classes include:

- one Dynamic Decals vendor asset reference: `Assets/Dynamic Decals/Resources/Decal.obj`
- RealtimeCSG vendor icon/readme image references
- `Assets/_Project/Scripts/Construction/AutonomousExtractorJobs.cs`
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
- Active root/architecture/report-index R49 boundary scan: `R49BoundaryScope=155`, `Missing=0`.
- Active architecture route-card exact-field scan: `RouteCardFiles=22`, `Missing=0`.
- Targeted stale-current/proof scan: no active scoped hits for R48-as-current, stale R48-only boundary in the checked root/architecture set, stale `ATLAS_CHECK_FAIL references=6861 missing=64`, or literal route-card `` `n`` separators after repair.
- Scoped root/architecture/status/rationale/log/generated-atlas `git diff --check`: PASS, exit `0`, line-ending warnings only.
- `python Tools\AtlasCheck.py`: FAIL, `ATLAS_CHECK_FAIL references=6861 missing=65`; missing refs are listed above.
- Runtime proof: absent. No Unity import, Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, platform run, network send, shader import, or visual-route proof was run.
