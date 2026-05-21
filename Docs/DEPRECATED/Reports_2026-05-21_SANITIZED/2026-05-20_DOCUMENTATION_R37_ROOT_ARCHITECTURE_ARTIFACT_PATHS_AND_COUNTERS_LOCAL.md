# 2026-05-20 Documentation R37 Root / Architecture Artifact Paths And Counters

Date: 2026-05-20
Status: STATIC VALIDATION RECORDED / ATLASCHECK RED / RUNTIME PROOF ABSENT
Scope: root authority docs, `Docs/ARCHITECTURE`, static source counters, archived artifact references

## Boundary

R37 is a local-only DOC_GLOBAL root/architecture documentation pass. It corrects artifact paths after Batch010 archival, demotes stale proof wording, and recaptures source-scale counters after concurrent source churn.

R36 remains the latest authority-spine/domain-map correction. R35 remains the prior R4/counter-residue correction. R34 remains the prior full source-counter refresh, now superseded by R37 where exact counts differ. No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime, platform run, analytics endpoint, network send, or visual-route proof was produced.

## What Was Wrong

- Active root and architecture entrypoints still started their read order at R36 while R37 edits had already corrected active artifact paths and counters.
- Several active docs still cited `Docs/AgentLogs/...` paths for SHINOBU_02, SHINOBU_154, SHINOBU_160, and SHINOBU_143 artifacts that had moved under `Docs/Archive/Batch010/AgentLogs/`.
- R34 source-count values were still promoted as current root/architecture orientation after fresh R37 source churn changed C# file counts, line counts, asmdef counts, interface token hits, and typed signal-lane counts.
- Large-owner tables omitted current line-heavy owners such as generated `UI/Localization/H8LocHashes.cs`, `Core/GlobalSignals.cs`, and `SpatialAudioManager.cs`.
- Root work-plan docs still contained proof-like wording around capture-time compile/render/memory observations without linked current artifacts.

## What Was Done

- Added R37 to `Docs/README.md`, `Docs/ROOT_DOCS_REFERENCE.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/Reports/README.md`, and `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Updated current source-scale orientation to the R37 static scan:
  - `1960` C# files under `Assets/_Project`
  - `1901` C# files under `Assets/_Project/Scripts`
  - `1936` non-test C# files excluding `Assets/_Project/Tests*`
  - `1338727` project physical lines
  - `1318650` script physical lines
  - `1332673` non-test physical lines
  - `322` broad `interface` token hits project-wide, `319` under scripts
  - `271` direct interface declaration lines
  - `62` direct public interfaces in `GlobalRegistryContracts.cs`
  - `133` first-party asmdefs, `131` excluding test dirs
  - `73` direct `GlobalSignals.CreateQueue(...)` slots
  - `135` typed `SignalBus<T>.EnsureInitialized()` lanes inside `GlobalSignals.cs`
  - `265` broader script-level typed `SignalBus<T>.EnsureInitialized()` matches
- Updated global-authority orientation values where source churn changed native collection hit totals.
- Repointed active artifact references to Batch010 archive paths where active `Docs/AgentLogs` copies are absent.
- Kept static-source and static-tool evidence below runtime/profiler/player-build claims.

## Static Validation

- `python Tools\BuildArchitectureAtlas.py`: exit `0`; regenerated `Docs\DEPENDENCY_GRAPH.md` and `Docs\DEPENDENCY_GRAPH.json`.
- `python Tools\test_architecture_atlas.py`: exit `0`, `10` tests OK.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: exit `0`.
- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6638 missing=58`; missing set starts with `Assets/Dynamic Decals/Resources/Decal.obj` and also contains RealtimeCSG vendor icon/readme image refs.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: exit `0`, `Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- Active non-archive Docs JSON parse: `JsonFiles=130`, `Bad=0`.
- Active root/architecture/report-index R4 marker scan: `ScopeFiles=106`, `Missing=0`, `Duplicate=0`.
- Architecture source-anchor filesystem scan: `SourceAnchorPathsChecked=460`, `Missing=20`; missing anchors are documented absent/blocker paths concentrated in `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, `DRONE_FLEET_PROTOCOL.md`, `ROUTE_CARD_SHINOBU_157_AUTOPILOT.md`, `SAVE_V8_BINARY_SPEC.md`, and SHINOBU route-card docs.
- Root/architecture/report markdown link scan: `ScopeFiles=285`, `MarkdownLinksChecked=62`, `Missing=0`.
- Scoped `git diff --check` over root/architecture/docs/tool changes: exit `0`, line-ending warnings only.

## Runtime Proof

No runtime proof was run in R37. Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, platform run, campaign telemetry, analytics endpoint, network send, and visual-route proof remain pending verification.
