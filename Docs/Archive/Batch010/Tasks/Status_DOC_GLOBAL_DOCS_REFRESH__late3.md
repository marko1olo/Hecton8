# DOC_GLOBAL_DOCS_REFRESH Status

Active status file recreated on 2026-05-20 after concurrent workspace archival removed the live `Docs/Tasks` copy. Historical full snapshots remain under `Docs/Archive/Batch008`, `Docs/Archive/Batch009`, and `Docs/Archive/Batch010`.

## R36 Checklist

- [x] Re-read available disk memory and attempted `CURRENT_BATCH.md` prompt extraction; result was `PROMPT_NOT_FOUND`.
- [x] Created `Docs/Reports/2026-05-20_DOCUMENTATION_R36_ROOT_ARCHITECTURE_AUTHORITY_SPINE_LOCAL.md` after read-only subagents correctly caught the temporary absent R36 path.
- [x] Promoted R36 through active root and architecture surfaces; R35 remains prior R4/counter-residue, R34 remains prior source-counter/physical-line refresh.
- [x] Updated `Docs/Actual Domains of Project.txt` with the R36 boundary and repaired malformed 9/10, 23/24, and Funnel Smoothing lines without changing the 9-echelon / 85-domain model.
- [x] Added R36/R4 root boundaries to `BUILD_PLAYTEST_ISSUES.md` and `MASTER_RELEASE_WORK_PLAN.md`.
- [x] Regenerated atlas and ran static validation.

## R36 Validation

- `python Tools\BuildArchitectureAtlas.py`: exit `0`.
- `python Tools\test_architecture_atlas.py`: exit `0`, `10` tests OK.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: exit `0`.
- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6637 missing=58`.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: exit `0`, `Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- JSON parse: `JsonFiles=122`, `Bad=0`.
- R4 scan: `ScopeFiles=101`, `Missing=0`, `Duplicate=0`.
- Source-anchor scan: `SourceAnchorPathsChecked=262`, `Missing=0`.
- Markdown link scan: `ScopeFiles=101`, `MarkdownLinksChecked=54`, `Missing=0`.
- Project-file/filesystem check: `ChemicalInfluenceGrid.cs` exists; `LogisticsPipeEvents.cs`, `HectonWaterPhysics.cs`, `HectonWaterPhysicsEditor.cs`, and `Assets/Dynamic Decals/Resources/Decal.obj` are absent; R36 report exists.
- Targeted diff-check: exit `0`; line-ending warnings only.

## R36 Current Blockers

- No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, mod runtime, platform, analytics endpoint, network send, or visual proof exists for R36.
- AtlasCheck remains red on Dynamic Decals / RealtimeCSG vendor references.
- Generated project stale includes remain for `LogisticsPipeEvents.cs`, `HectonWaterPhysics.cs`, and `HectonWaterPhysicsEditor.cs`.
