# DOC_GLOBAL R43 Root / Architecture Route-Card And Counter-Residue Refresh

Date: 2026-05-20
Agent: DOC_GLOBAL_DOCS_REFRESH
Domain: Echelon 9.83 Chronicler / Project Documentation Currency
Evidence class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC
Runtime status: PENDING VERIFICATION

## Scope

Local-only root and architecture documentation refresh. This pass updates internal claims in active root/architecture entrypoints and route cards. It does not claim Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, platform run, network send, analytics endpoint, or visual-route proof.

## What Was Wrong

- Active root/architecture entrypoints still promoted R40/R41/R42 as the newest boundary after the R43 route-card and AtlasCheck red-state correction started.
- `Docs/ARCHITECTURE/README.md`, `Docs/Reports/README.md`, `Docs/PROJECT_ATLAS.md`, `Docs/SYSTEMS_CONTRACTS.md`, `Docs/QUALITY_GATES.md`, `Docs/ROOT_DOCS_REFERENCE.md`, and `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md` contained stale read-order or latest/current wording.
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` still had a malformed R41 latest row and stale static gate wording.
- `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/PROJECT_ATLAS.md`, and `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md` had stale first-party asmdef wording (`133/131`, `137/135`, or `139/137`) despite the final R43 static source orientation being `141/139`.
- Several active architecture route cards had route text without a complete explicit route-card field set: route id, producer phase, consumer phase, cadence, capacity, overflow/failure mode, shutdown/disposal, proof required before GREEN, and review disposition.
- `Tools/AtlasCheck.py` red state drifted after the R43 atlas regeneration path: the current blocker is not the older `references=6728 missing=58`; the transient pre-report check saw `references=6736 missing=60` because this R43 report path did not exist yet.

## What Was Updated

- Added R43 root/architecture actuality boundary text to `AGENTS.md` and the active root/architecture/report indexes.
- Promoted R43 as the current DOC_GLOBAL root/architecture boundary in active entrypoints; R42 is now prior counter/route-boundary/proof-label evidence.
- Corrected first-party asmdef orientation to `141` under `Assets/_Project` and `139` excluding test dirs.
- Updated root/architecture authority surfaces to use the current AtlasCheck red tuple and to keep it static/tool-only.
- Normalized route-card fields in the initial route-card sweep:
  - `Docs/ARCHITECTURE/SHINOBU_113_HYDRODYNAMIC_KCC_ROUTE_CARD.md`
  - `Docs/ARCHITECTURE/SHINOBU_138_CHEMICAL_INFLUENCE_GRID_ROUTE_CARD.md`
  - `Docs/ARCHITECTURE/SHINOBU_151_DYNAMIC_POINT_LIGHT_CULLING_ROUTE_CARD.md`
  - `Docs/ARCHITECTURE/SHINOBU_156_ABYSSAL_CAVITATION_ROUTE_CARD.md`
  - `Docs/ARCHITECTURE/ROUTE_CARD_SHINOBU_157_AUTOPILOT.md`
  - `Docs/ARCHITECTURE/SHINOBU_200_SIGNAL_THREAD_CONTENTION_ROUTE_CARD.md`
  - `Docs/ARCHITECTURE/SHINOBU_205_AUP_PRECISION_ROUTE_CARD.md`
  - `Docs/ARCHITECTURE/MACRO_ECOSYSTEM_MATHEMATICIAN.md`
- Expanded the R43 route-card scan to all active `*ROUTE_CARD*.md` files under `Docs/ARCHITECTURE` and fixed missing `Proof required before GREEN` / `Review disposition` wording in `GLOBAL_AUTHORITY_ROUTE_CARD_BTREE_TELEMETRY_SHINOBU_207.md`, `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`, `PROCEDURAL_WRECKAGE_GLOBAL_AUTHORITY_ROUTE_CARD_SHINOBU_121.md`, `SHINOBU_125_SCAVENGING_LOOT_ORACLE_ROUTE_CARD.md`, `SHINOBU_158_BUOYANCY_ROUTE_CARD.md`, `SHINOBU_218_DEPTH_BASED_INTEGRITY_ROUTE_CARD.md`, and `BASE_ATMOSPHERE_LOGISTICS_ROUTE_CARD_SHINOBU_221.md`.
- Added the missing active boundary to `Docs/ARCHITECTURE/BASE_ATMOSPHERE_LOGISTICS_SHINOBU_221.md`.
- Updated `Tools/BuildArchitectureAtlas.py` and `Tools/test_architecture_atlas.py` for the R43 boundary and AtlasCheck red-state text.

## Validation

- `python Tools\BuildArchitectureAtlas.py`: PASS; regenerated `Docs/DEPENDENCY_GRAPH.md`, `Docs/DEPENDENCY_GRAPH.json`, and `Docs/DEPENDENCY_GRAPH.cache.json`.
- `python Tools\test_architecture_atlas.py`: PASS, `10` tests.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: PASS.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: PASS, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- Active non-archive/non-deprecated Docs JSON parse: PASS, `JsonFiles=168`, `Bad=0`, `Utf16Fallback=0`.
- R43 route-card field scan: PASS, `RouteCardFiles=14`, `Missing=0`.
- Active root/architecture R43 boundary scan: PASS, `R43BoundaryScope=116`, `Missing=0`.
- Targeted stale R42/R41/proof-wording scan across active root/architecture scope: PASS, `StaleScan=0`.
- Scoped root/architecture/status/rationale/log/generated-atlas `git diff --check`: PASS, exit `0`, line-ending warnings only.
- Final R43 source-counter recapture: `ProjectCs=2047`, `ScriptCs=1986`, `NonTestCs=2021`, `ProjectLines=1394096`, `ScriptLines=1373895`, `NonTestLines=1387908`, `Asmdefs=141`, `NonTestAsmdefs=139`, `InterfaceHitsProject=375`, `InterfaceHitsScripts=370`, `InterfaceDecls=272`, `RegistryInterfaces=62`, `GlobalRegistryHits=6131`, `PubSubHits=310`, `NativeHits=23109`, `NativeQueueRefs=115`, `CreateQueueSlots=73`, `EnsureLanes=135`, `ConfigureEnsure=271`, `ScriptTypedLanes=1343`.
- `python Tools\AtlasCheck.py`: FAIL, `ATLAS_CHECK_FAIL references=6736 missing=59`; missing refs are one Dynamic Decals vendor asset reference, RealtimeCSG vendor icon/readme image refs, and `Assets/_Project/Scripts/Habitat/Deformation/Editor/HabitatDamageBakePipeline.cs`.

## Runtime Boundary

No runtime evidence was produced in this pass. No Unity import, clean Unity Console, Play Mode, Burst Inspector, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, analytics endpoint, or visual-route proof is implied.
