# 2026-05-20 Documentation R38 Root/Architecture Source-Counter Drift And Boundary Local

Date: 2026-05-20
Status: STATIC DOC/SOURCE UPDATE COMPLETE / RUNTIME PROOF ABSENT
Domain: DOC_GLOBAL_DOCS_REFRESH / Echelon 9.83 Chronicler

## Scope

R38 updates active root and `Docs/ARCHITECTURE` documentation after current source churn changed source-scale counters and left R36/R37 boundary residue in root/architecture entrypoints.

This report is documentation/source/tool evidence only. It is not Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, platform, analytics, network, or visual proof.

## Current R38 Static Source Counters

- first-party C# files under `Assets/_Project`: `1960`
- first-party C# files under `Assets/_Project/Scripts`: `1901`
- first-party non-test C# files excluding `Assets/_Project/Tests*`: `1936`
- project physical lines under `Assets/_Project`: `1167207`
- script physical lines under `Assets/_Project/Scripts`: `1149917`
- non-test physical lines excluding `Assets/_Project/Tests*`: `1161785`
- broad `interface` token hits under `Assets/_Project`: `347`
- broad `interface` token hits under `Assets/_Project/Scripts`: `342`
- direct interface declaration lines under `Assets/_Project`: `272`
- direct public interfaces in `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`: `62`
- first-party asmdefs under `Assets/_Project`: `133`
- first-party asmdefs excluding test dirs: `131`
- `GlobalRegistry.` line hits under scripts: `6069`
- `Publish(...)` / `Subscribe(...)` line hits under scripts: `526`
- native collection line hits under scripts: `21454`
- `GlobalSignals.cs` `NativeQueue<...>` refs: `115`
- `SignalBus<T>.Configure(...)` / `EnsureInitialized(...)` hits inside `GlobalSignals.cs`: `271`
- direct `GlobalSignals.CreateQueue(...)` slots inside `GlobalSignals.cs`: `73`
- typed `SignalBus<T>.EnsureInitialized()` lanes inside `GlobalSignals.cs`: `135`
- broader script-level typed `SignalBus<T>.EnsureInitialized()` matches: `266`

These counters are volatile orientation data under active multi-agent source churn. They supersede R37 where exact values differ. They do not prove compile or runtime behavior.

## Root/Architecture Corrections

- Promoted active root/architecture boundary text from R37 to R38.
- Kept R37 as the prior artifact-path/proof-wording/source-counter layer.
- Kept R36 as the prior authority-spine/domain-map layer.
- Updated root source-counter blocks in `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, and `Docs/PROJECT_STATE_STATIC_XRAY.md`.
- Updated `Docs/ARCHITECTURE/README.md`, architecture boundary notes, and root work-plan ledgers that still called R36 or R37 the latest boundary.
- Preserved AtlasCheck as red until `Tools/AtlasCheck.py` exits `0`.

## Static Validation Boundary

- `python Tools\BuildArchitectureAtlas.py`: PASS.
- `python Tools\test_architecture_atlas.py`: PASS, `10` tests.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: PASS.
- `python Tools\AtlasCheck.py`: FAIL, `ATLAS_CHECK_FAIL references=6638 missing=58`.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: PASS, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- Active non-archive Docs JSON parse: `JsonFiles=130`, `Bad=0`.
- Active root/architecture/report-index R4 marker scan: `ScopeFiles=88`, `Missing=0`, `Duplicate=0`.
- Root/index markdown link scan: `ScopeFiles=14`, `MarkdownLinksChecked=54`, `Missing=0`.
- Targeted stale R36/R37-current scan: no current-boundary residue requiring patch; one historical `PROJECT_STATE_STATIC_XRAY.md` R37 paragraph is superseded inline by R38/R43.
- CRLF escape residue scan: no hits.
- `git diff --check -- Docs Tools BUILD_PLAYTEST_ISSUES.md MASTER_RELEASE_WORK_PLAN.md ':!Docs/Tasks/*' ':!Docs/AgentLogs/*' ':!Docs/Archive/**' ':!Docs/Modding/**'`: exit `0`, line-ending warnings only.

Runtime proof remains absent.
