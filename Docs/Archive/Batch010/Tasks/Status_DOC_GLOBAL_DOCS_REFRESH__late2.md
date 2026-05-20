# DOC_GLOBAL_DOCS_REFRESH Status

Status file recreated on 2026-05-20 after the active `Docs/Tasks` working set was archived/removed by concurrent workspace activity. Historical full status snapshots remain in `Docs/Archive/Batch008`, `Docs/Archive/Batch009`, and `Docs/Archive/Batch010`.

## R36 Checklist

- [x] Re-read available disk memory and attempted prompt extraction from `Docs/Tasks/CURRENT_BATCH.md`. DOD: prompt extraction returned `PROMPT_NOT_FOUND`; active status/rationale were absent on disk during the late R36 write, so archived status/rationale plus current source/docs were used. Rejected: relying on compacted chat only. Estimate: 0 us runtime.
- [x] Integrated three read-only subagent audits. DOD: subagents correctly caught the temporary R36-report absence before the report existed; `Docs/Reports/2026-05-20_DOCUMENTATION_R36_ROOT_ARCHITECTURE_AUTHORITY_SPINE_LOCAL.md` was then created and validated. Rejected: leaving R36 as an absent promised path. Estimate: 0 us runtime.
- [x] Promoted R36 through active root and architecture surfaces. DOD: root/architecture entrypoints now route through R36 -> R35 -> R34, with R36 as authority-spine/domain-map correction, R35 as prior R4/counter-residue correction, and R34 as prior source-counter/physical-line refresh. Rejected: keeping R35 as latest after R36 edits existed. Estimate: 0 us runtime.
- [x] Corrected root domain-map internals. DOD: `Docs/Actual Domains of Project.txt` now carries the R36 boundary, keeps the 9-echelon / 85-domain model, and fixes fused domain `9/10`, fused `23/24`, and stray `A Funnel Smoothing:*` formatting without changing ownership semantics. Estimate: 0 us runtime.
- [x] Closed root-file R4 gaps. DOD: `BUILD_PLAYTEST_ISSUES.md` and `MASTER_RELEASE_WORK_PLAN.md` now carry R36 root/architecture actuality boundaries; root/architecture R4 scan reports missing `0`, duplicate `0`. Estimate: 0 us runtime.
- [x] Regenerated atlas and ran static validation. DOD: atlas build/tests/py_compile, AtlasCheck, Mod API validator, JSON parse, R4 marker scan, source-anchor scan, markdown link scan, project-file/filesystem check, and scoped diff-check results are recorded below. Rejected: claiming Unity/runtime/profiler/player-build proof. Estimate: 0 us runtime.

## R36 Validation

- `python Tools\BuildArchitectureAtlas.py`: exit `0`; regenerated `Docs/DEPENDENCY_GRAPH.md` and `Docs/DEPENDENCY_GRAPH.json`.
- `python Tools\test_architecture_atlas.py`: exit `0`, `10` tests OK.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: exit `0`.
- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6637 missing=58`; missing set is `Assets/Dynamic Decals/Resources/Decal.obj` plus RealtimeCSG vendor icon/readme image refs.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: exit `0`, `Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- Active non-archive Docs JSON parse: `JsonFiles=122`, `Bad=0`.
- Root/architecture R4 marker scan: `ScopeFiles=101`, `Missing=0`, `Duplicate=0`.
- Architecture source-anchor filesystem scan: `SourceAnchorPathsChecked=262`, `Missing=0`.
- Root/architecture markdown link scan: `ScopeFiles=101`, `MarkdownLinksChecked=54`, `Missing=0`.
- Project-file/filesystem check: `ChemicalInfluenceGrid.cs` exists; `LogisticsPipeEvents.cs`, `HectonWaterPhysics.cs`, `HectonWaterPhysicsEditor.cs`, and `Assets/Dynamic Decals/Resources/Decal.obj` are absent; the R36 report exists.
- Scoped `git diff --check -- Docs Tools BUILD_PLAYTEST_ISSUES.md MASTER_RELEASE_WORK_PLAN.md ':!Docs/Tasks/*' ':!Docs/AgentLogs/*' ':!Docs/Archive/**' ':!Docs/Modding/**'`: exit `0`, line-ending warnings only.

## R36 Current Blockers

- No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, platform run, campaign telemetry, analytics endpoint, network send, or visual-route proof exists for R36.
- `Tools/AtlasCheck.py` remains red on `58` missing refs: one Dynamic Decals missing vendor asset ref plus RealtimeCSG vendor icon/readme image refs.
- Project-file stale includes remain: `Hecton8.Core.csproj` references absent `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`; `Assembly-CSharp.csproj` references absent `Assets/_Project/_Archive/HectonWaterPhysics.cs` and `Assets/_Project/_Archive/HectonWaterPhysicsEditor.cs`.
- Optional/pending artifacts remain absent and are documented as absent: root visual-readback screenshots, `Assets/_Project/Scripts/GasGiantRotationDriver.cs`, and multiple architecture CSV/tuning inputs.
