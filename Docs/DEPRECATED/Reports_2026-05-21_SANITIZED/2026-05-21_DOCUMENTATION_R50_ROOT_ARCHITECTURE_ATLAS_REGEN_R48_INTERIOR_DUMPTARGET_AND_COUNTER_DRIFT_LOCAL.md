# 2026-05-21 Documentation R50 Root / Architecture Atlas Regen, R48 Interior, Dump Target, and Counter Drift

Date: 2026-05-21
Agent: DOC_GLOBAL_DOCS_REFRESH
Scope: active root documentation, active `Docs/*.md` / `Docs/*.txt` entrypoints, active `Docs/ARCHITECTURE/*.md`, generated architecture atlas metadata, and DOC_GLOBAL status/rationale/log.
Evidence class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC.

## Boundary

R50 supersedes R49 only for active root/architecture documentation currentness, generated atlas regeneration, stale R48 interior-boundary wording, planned/generated-on-fault dump target wording, AtlasCheck red-state wording, and volatile source-counter orientation.

R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. Older R45 through R34 layers remain historical static correction layers where their exact claims differ from R50.

Runtime proof remains absent. This pass did not run Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, platform run, shader import, network send, or visual-route proof.

## Findings

- `Docs/DEPENDENCY_GRAPH.md` had regenerated back to an R47 boundary and stale repository-scale counters while the active root/architecture boundary was already R49.
- `Tools\BuildArchitectureAtlas.py` and `Tools\test_architecture_atlas.py` hard-coded a stale AtlasCheck tuple and report label instead of carrying the current generated AtlasCheck red state.
- Multiple active architecture documents still had an interior line that named R48 as the current static/tool boundary beneath an R49 top boundary.
- `Docs/ARCHITECTURE/DATA_MONOLITH_RUNTIME_INTEGRATION.md` still had an R48 current-status heading.
- `Docs/ARCHITECTURE/SHINOBU_248_SHOCKWAVE_NAN_ROUTE_CARD.md` appeared during R50 concurrent churn without an R50 actuality boundary or exact route-card fields.
- Several binary-payload and route-card rows used `Proof artifacts` or `Dump path` wording for planned black-box dump targets, allowing a planned/generated-on-fault path to read like an existing runtime artifact.
- Current source counters drifted under concurrent source changes.

## Current Source-Scale Orientation

Static source scan under `Assets/_Project`:

- `ProjectCs=2163`
- `ScriptCs=2099`
- `NonTestCs=2135`
- `ProjectLines=1491028`
- `ScriptLines=1469978`
- `NonTestLines=1484103`
- `Asmdefs=154`
- `NonTestAsmdefs=152`
- `InterfaceHitsProject=354`
- `InterfaceHitsScripts=351`
- `InterfaceDeclsProject=278`
- `InterfaceDeclsScripts=277`
- `GlobalRegistryContractsPublicInterfaces=62`
- `GlobalRegistryDotHits=6212`
- `PublishSubscribeDirectCallLines=306`
- `SignalCorridorBroadHits=3210`
- `NativeCollectionHits=19731`
- `NativeQueueGenericRefs=904`
- `CreateQueueCalls=73`
- `SignalBusEnsureInitializedHits=279`
- `SignalBusConfigureOrEnsureHits=512`
- `ScriptTypedLanes=1448`

These counters are volatile STATIC_SOURCE orientation only. They are not compile, Unity import, runtime, profiler, GC, player-build, or platform proof.

## AtlasCheck Red State

`python Tools\AtlasCheck.py` still exits `1` with `ATLAS_CHECK_FAIL references=6868 missing=60`.

The current missing set is one Dynamic Decals vendor asset reference, 57 RealtimeCSG vendor icon/readme image references, `Assets/_Project/Scripts/Editor/HectonMaskChannelPacker.cs`, and `Assets/_Project/Scripts/Editor/HectonMaterialChannelPackValidator.cs`.

The current generated atlas remains STATIC_SOURCE only until `Tools\AtlasCheck.py` exits `0`.

## Validation Snapshot

- `python Tools\BuildArchitectureAtlas.py`: PASS.
- `python Tools\test_architecture_atlas.py`: PASS, `10` tests.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: PASS.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: PASS, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- Active non-archive/non-deprecated Docs JSON parse: `JsonFiles=199`, `Bad=0`.
- Active root/architecture/report-index R50 boundary scan: `R50BoundaryScope=156`, `Missing=0`.
- Active architecture route-card exact-field scan: `RouteCardFiles=20`, `Missing=0`.
- Strict stale-current/proof scan over active root/architecture surfaces: `StrictProofOrStaleHits=0`.
- Active architecture dump-label scan: no `Blackbox dump path:` or `Dump path:` overclaim hits.
- Scoped root/architecture/status/rationale/log/generated-atlas `git diff --check`: PASS, exit `0`, line-ending warnings only.
- `python Tools\AtlasCheck.py`: FAIL, `ATLAS_CHECK_FAIL references=6868 missing=60`; missing refs remain one Dynamic Decals vendor asset ref, 57 RealtimeCSG vendor icon/readme image refs, `Assets/_Project/Scripts/Editor/HectonMaskChannelPacker.cs`, and `Assets/_Project/Scripts/Editor/HectonMaterialChannelPackValidator.cs`.
- Runtime proof: absent. No Unity import, Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, platform run, network send, shader import, or visual-route proof was run.
