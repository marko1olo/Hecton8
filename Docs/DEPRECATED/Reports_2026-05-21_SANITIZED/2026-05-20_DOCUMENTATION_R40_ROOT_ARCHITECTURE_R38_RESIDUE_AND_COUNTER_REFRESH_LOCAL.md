# 2026-05-20 DOC_GLOBAL R40 - Root/Architecture R38 Residue And Counter Refresh

Date: 2026-05-20
Prompt: DOC_GLOBAL_DOCS_REFRESH
Domain: Echelon 9.83 Chronicler / Project Documentation Currency
Evidence class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC
Runtime proof: ABSENT

## Scope

- Root anchors: `AGENTS.md`, `MASTER_RELEASE_WORK_PLAN.md`, `BUILD_PLAYTEST_ISSUES.md`.
- Root documentation and indexes: `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ROOT_DOCS_REFERENCE.md`, `Docs/Reports/README.md`, `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, `Docs/SYSTEMS_CONTRACTS.md`, `Docs/QUALITY_GATES.md`, `Docs/PROJECT_STATE_STATIC_XRAY.md`, `Docs/PROJECT_ATLAS.md`.
- Architecture surface: `Docs/ARCHITECTURE/*.md`, with focused edits to global-authority route cards, H-Phi/static metric wording, architecture README, and actuality ledger.
- Generated/static tooling surface: `Tools/BuildArchitectureAtlas.py`, `Tools/test_architecture_atlas.py`, `Docs/DEPENDENCY_GRAPH.md`, `Docs/DEPENDENCY_GRAPH.json`, `Docs/DEPENDENCY_GRAPH.cache.json`.

## What Was Wrong

- Active entrypoint docs had R40 boundary blocks at the top, but interior lines still told readers to start at R38 or R39.
- Some root and architecture docs still used `latest` language for archived SHINOBU_02 SignalCritical/Full audit artifacts. Those artifacts are historical static-source evidence until rerun.
- `Docs/ROOT_DOCS_REFERENCE.md` still advertised the R38 report as the latest DOC_GLOBAL boundary.
- `Docs/DOC_GOVERNANCE.md` carried R40 source-counter values under an R38 label.
- `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md` had an R38 heading on an R40 current-state section.
- Several global-authority route-card surfaces needed sharper owner/phase/capacity/proof wording so static inventories were not mistaken for route approval.

## R40 Counter Snapshot

These are local static source/file counts only, not compile, Unity, profiler, GC, player-build, save/load, or visual proof.

| Metric | R40 value |
|---|---:|
| `Assets/_Project/**/*.cs` | 1960 |
| `Assets/_Project/Scripts/**/*.cs` | 1901 |
| non-test first-party C# excluding `Assets/_Project/Tests*` | 1936 |
| project physical lines | 1341123 |
| script physical lines | 1321033 |
| non-test physical lines | 1335006 |
| first-party asmdefs | 133 |
| non-test first-party asmdefs | 131 |
| broad `interface` token hits project-wide | 324 |
| broad `interface` token hits under scripts | 321 |
| direct interface declaration lines | 271 |
| direct public interfaces in `GlobalRegistryContracts.cs` | 62 |
| `GlobalRegistry.` line hits | 6056 |
| publish/subscribe line hits | 571 |
| native-collection line hits | 15465 |
| `GlobalSignals.cs` `NativeQueue<...>` refs | 115 |
| direct `GlobalSignals.CreateQueue(...)` slots | 73 |
| `SignalBus<T>.EnsureInitialized()` lanes inside `GlobalSignals.cs` | 135 |
| `SignalBus<T>.Configure/EnsureInitialized` hits inside `GlobalSignals.cs` | 271 |
| typed `SignalBus<T>.EnsureInitialized()` matches across scripts | 266 |

## Edits Made

- Promoted R40 through active root and architecture entrypoints as the latest static DOC_GLOBAL root/architecture boundary.
- Demoted archived SHINOBU_02 SignalCritical/Full audit wording from current/latest language to archived historical artifact wording.
- Repaired root/architecture report read order so R40 precedes R39 and R38.
- Added explicit R40 report entries to `Docs/Reports/README.md` and `Docs/ROOT_DOCS_REFERENCE.md`.
- Reframed `Docs/ARCHITECTURE/SYSTEM_INTERCONNECT_MATRIX.md` static route inventories as orientation only, not route approval.
- Filled global-authority route-card fields in focused SHINOBU route-card docs where active text implied a route but lacked owner/phase/capacity/proof framing.
- Kept AtlasCheck red as a blocker instead of claiming atlas verification.

## Validation

- `python Tools\BuildArchitectureAtlas.py`: PASS; regenerated `Docs/DEPENDENCY_GRAPH.md` and `Docs/DEPENDENCY_GRAPH.json`.
- `python Tools\test_architecture_atlas.py`: PASS, `10` tests.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: BLOCKED by filesystem permissions. Default pycache path failed with `Permission denied: Tools\__pycache__\BuildArchitectureAtlas.cpython-313.pyc...`; workspace-local `PYTHONPYCACHEPREFIX=Temp\docglobal_r40_pycache` also failed on pyc rename with `WinError 5`.
- AST parse fallback for `Tools/BuildArchitectureAtlas.py`, `Tools/AtlasCheck.py`, and `Tools/test_architecture_atlas.py`: PASS, `Files=3`. This is syntax evidence only, not bytecode proof.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: PASS, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- Active non-archive/non-deprecated Docs JSON parse: `JsonFiles=130`, `Bad=0`.
- Active root/architecture/report-index R4 marker scan: `ScopeFiles=106`, `Missing=0`, `Duplicate=0`.
- Root/index markdown link scan: `MarkdownLinksChecked=54`, `Missing=0`.
- Targeted stale R38/R39-current and archived-proof wording scan across active root/architecture scope: `TargetedStaleHits=0` after excluding historical `Docs/Archive`, `Docs/DEPRECATED`, `Docs/AgentLogs`, and `Docs/Tasks`.
- Fresh R40 source-counter recapture: `ProjectCs=1960`, `ScriptCs=1901`, `NonTestCs=1936`, `ProjectLines=1341123`, `ScriptLines=1321033`, `NonTestLines=1335006`, `Asmdefs=133`, `NonTestAsmdefs=131`, `InterfaceHitsProject=324`, `InterfaceHitsScripts=321`, `InterfaceDecls=271`, `RegistryInterfaces=62`, `GlobalRegistryHits=6056`, `PubSubHits=571`, `NativeHits=15465`, `NativeQueueRefs=115`, `CreateQueueSlots=73`, `EnsureLanes=135`, `ConfigureEnsure=271`, `ScriptTypedLanes=266`.
- `python Tools\AtlasCheck.py`: FAIL, `ATLAS_CHECK_FAIL references=6638 missing=58`. Missing refs are `Assets/Dynamic Decals/Resources/Decal.obj` plus RealtimeCSG vendor icon/readme image paths.
- Scoped `git diff --check` for root/architecture/R40 report/generated atlas/status/rationale/log files: PASS with line-ending warnings only.

## Runtime Proof Boundary

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, analytics endpoint, network send, or visual-route proof was run during R40. All touched documentation remains static/source/tool evidence only unless a separate fresh runtime artifact is linked.
