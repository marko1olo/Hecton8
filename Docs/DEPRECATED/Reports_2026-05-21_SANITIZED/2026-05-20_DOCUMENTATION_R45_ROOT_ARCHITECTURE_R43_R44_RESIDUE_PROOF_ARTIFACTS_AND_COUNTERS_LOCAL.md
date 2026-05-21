# DOC_GLOBAL R45 Root/Architecture R43/R44 Residue, Proof Artifacts, and Counters

Date: 2026-05-20
Agent: DOC_GLOBAL_DOCS_REFRESH
Scope: root documentation and `Docs/ARCHITECTURE`
Evidence class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC

## Boundary

This is a local documentation-currency pass. It updates active root and architecture documentation internals after R44. It does not claim Unity import, clean Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, platform run, visual route proof, or runtime performance proof.

R45 supersedes R44 only where it corrects:

- R43/R44 current-boundary residue still advertised inside active root or architecture entrypoints
- proof wording that treated local scan prose or `git diff --check` text as artifact-backed proof
- active source-scale counters after current workspace churn
- generated atlas boundary/test labels after R45 report creation

R45 source-scale counter baseline: `ProjectCs=2052`, `ScriptCs=1991`, `NonTestCs=2026`, `ProjectLines=1401183`, `ScriptLines=1380785`, `NonTestLines=1394758`, `Asmdefs=141`, `NonTestAsmdefs=139`, `InterfaceHitsProject=345`, `InterfaceHitsScripts=342`, `InterfaceDecls=280`, `RegistryInterfaces=63`, `GlobalRegistryHits=6199`, `PubSubHits=575`, `NativeHits=18045`, `NativeQueueRefs=116`, `CreateQueueSlots=73`, `EnsureLanes=135`, `ConfigureEnsure=271`, `ScriptTypedLanes=1345`.

These counters are STATIC_SOURCE orientation only. They are not compile, Unity import, runtime, profiler, GC, or platform proof.

## Changes

- `Docs/PROJECT_ATLAS.md`, `Docs/H8_GLOSSARY.md`, and `Docs/TECHNICAL_FAQ.md` no longer identify R43 as the current root/architecture boundary.
- Root entrypoints now start the DOC_GLOBAL root/architecture read order at R45, then R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 and older correction layers.
- Active architecture interiors that carried `Current static/tool boundary is R44` wording now point at the R45 boundary while keeping R44 as prior.
- `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` no longer treats SHINOBU_131 local scan text and `git diff --check` prose as artifact-backed verification.
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, `Docs/ARCHITECTURE/README.md`, `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, `Docs/PROJECT_STATE_STATIC_XRAY.md`, `Docs/DOC_GOVERNANCE.md`, and `Docs/README.md` now carry the R45 counter and read-order boundary.
- `MASTER_RELEASE_WORK_PLAN.md` and `BUILD_PLAYTEST_ISSUES.md` now start their root current-state boundary at R45 instead of R42.
- Active architecture boundary blocks no longer keep a stale first paragraph that says `Current root/architecture boundary is R44` before a later R45 paragraph.
- `Tools/BuildArchitectureAtlas.py` and `Tools/test_architecture_atlas.py` now emit and expect R45 boundary labels.

## Validation

- `python Tools\BuildArchitectureAtlas.py`: PASS; regenerated `Docs/DEPENDENCY_GRAPH.md` and `Docs/DEPENDENCY_GRAPH.json`.
- `python Tools\test_architecture_atlas.py`: PASS, `10` tests.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: PASS.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: PASS, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- Active non-archive/non-deprecated Docs JSON parse: PASS, `JsonFiles=169`, `Bad=0`.
- Active root/architecture R45 boundary scan: PASS, `R45BoundaryScope=112`, `Missing=0`.
- Active architecture route-card exact-label scan: PASS, `RouteCardFiles=14`, `Missing=0`.
- Specific stale-current scan: PASS, `SpecificStaleCurrentHits=0`.
- Strict proof-overclaim scan: PASS, `StrictProofOverclaimHits=0`.
- Scoped root/architecture/status/rationale/log/generated-atlas `git diff --check`: PASS, exit `0`, line-ending warnings only.
- `python Tools\AtlasCheck.py`: FAIL, `ATLAS_CHECK_FAIL references=6741 missing=59`; missing refs remain one Dynamic Decals vendor asset ref, RealtimeCSG vendor icon/readme image refs, and `Assets/_Project/Scripts/Habitat/Deformation/Editor/HabitatDamageBakePipeline.cs`.

Runtime proof remains absent: no Unity import, Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, platform run, network send, or visual-route proof was run.
