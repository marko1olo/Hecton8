# DOC_GLOBAL R44 Root/Architecture Internal Residue and Exact Route Fields

Date: 2026-05-20
Agent: DOC_GLOBAL_DOCS_REFRESH
Scope: root documentation and `Docs/ARCHITECTURE`
Evidence class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC

## Boundary

This is a local documentation-currency pass. It updates active root and architecture documentation internals after R43. It does not claim Unity import, clean Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, platform run, visual route proof, or runtime performance proof.

R44 supersedes R43 only where it corrects:

- stale R42/R43 currentness residue inside root and architecture documents
- exact route-card field labels for global-authority review
- proof wording that implied static scans, fixture text, or attempted builds were current artifact-backed proof
- active architecture-map source-counter residue that still used the R42 tuple

R44 source-scale counter and AtlasCheck red-state baseline: `ProjectCs=2050`, `ScriptCs=1989`, `NonTestCs=2024`, `ProjectLines=1399032`, `ScriptLines=1378730`, `NonTestLines=1392642`, `Asmdefs=141`, `NonTestAsmdefs=139`, `InterfaceHitsProject=345`, `InterfaceHitsScripts=342`, `InterfaceDecls=279`, `RegistryInterfaces=62`, `GlobalRegistryHits=6201`, `PubSubHits=526`, `NativeHits=17840`, `NativeQueueRefs=116`, `CreateQueueSlots=73`, `EnsureLanes=135`, `ConfigureEnsure=271`, `ScriptTypedLanes=1345`.

## Changes

- Root authority chains in `Docs/ROOT_DOCS_REFERENCE.md`, `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`, `Docs/QUALITY_GATES.md`, and `Docs/SYSTEMS_CONTRACTS.md` no longer start their active DOC_GLOBAL chain at R42.
- `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md` now uses the R43 counter tuple in its interior current-state section instead of the stale R42 `2029/1970/2003` file counts and old signal/native lane counts.
- Duplicate `R43/R43` supersession residue was removed from active architecture boundary text.
- Active architecture route cards now carry exact `Overflow/failure` and `Shutdown/disposal` labels in addition to owner, phase, cadence, capacity, `Proof required before GREEN`, and `Review disposition`.
- `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, `Docs/ARCHITECTURE/CONSTRUCTION_SOCKET_CSR_SOLVER_SHINOBU_217.md`, and `Docs/ARCHITECTURE/SHINOBU_205_AUP_PRECISION_ROUTE_CARD.md` now demote static scans, fixture text, and attempted build wording unless a full artifact tuple is linked.
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BURN_DOWN_PLAN.md` now treats the R26 no-regression row as a historical static pressure snapshot subordinate to the current DOC_GLOBAL boundary and AtlasCheck red state.

## Validation

- `python Tools\BuildArchitectureAtlas.py`: PASS, regenerated `Docs/DEPENDENCY_GRAPH.md` and `Docs/DEPENDENCY_GRAPH.json`.
- `python Tools\test_architecture_atlas.py`: PASS, `10` tests.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: PASS.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: PASS, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- Active non-archive/non-deprecated Docs JSON parse: `JsonFiles=169`, `Bad=0`.
- R44 root/architecture boundary scan: `R44BoundaryScope=117`, `Missing=0`.
- Active architecture route-card exact-label scan: `RouteCardFiles=14`, `Missing=0`.
- Targeted stale-current/proof scan: no active hits for R43-as-current, R43 blocker labels, stale `references=6736 missing=59`, stale `DOC_GLOBAL R43 ->`, or stale AUP "current CLI result" wording in the scoped root/architecture surface.
- Scoped `git diff --check`: PASS, exit `0`, line-ending warnings only.
- `python Tools\AtlasCheck.py`: FAIL, `ATLAS_CHECK_FAIL references=6739 missing=59`. Missing refs remain one Dynamic Decals vendor asset ref, RealtimeCSG vendor icon/readme image refs, and `Assets/_Project/Scripts/Habitat/Deformation/Editor/HabitatDamageBakePipeline.cs`.

Runtime proof remains absent: no Unity import, Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, platform run, network send, or visual-route proof was run.
