# 2026-05-20 Documentation R36 Root / Architecture Authority Spine

Date: 2026-05-20
Status: STATIC VALIDATION RECORDED / ATLASCHECK RED / RUNTIME PROOF ABSENT
Scope: root authority docs, `Docs/ARCHITECTURE`, `Docs/Actual Domains of Project.txt`, reports index

## Boundary

R36 is a local-only DOC_GLOBAL root/architecture documentation pass. It corrects authority-spine wording and the domain-map entrypoint after R35. It does not create Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime, platform, analytics endpoint, network send, or visual-route proof.

R35 remains the prior R4/counter-residue correction. R34 remains the prior source-counter and physical-line refresh. R33 remains the prior R32-residue/source-anchor correction. R32 remains the prior R4/proof-wording correction. R31 remains the prior current-boundary propagation correction. R30 remains the prior internal-currentness correction. R29 remains the prior stale-gate/global-authority correction. R28 remains the prior interior-boundary correction. R27 is historical source-counter/index evidence superseded by R34.

## What Was Wrong

- `Docs/DOC_GOVERNANCE.md` and `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md` still described R35 validation as pending even though R35 had recorded its static atlas/test/static-validator tuple.
- Root and architecture entrypoints lacked a disk-backed R36 authority-spine boundary once R36 edits started.
- `Docs/Actual Domains of Project.txt` remained a 2026-05-17 R4-only authority file with no current R36/R35/R34 boundary.
- The domain map contained local formatting defects: domain `9` and `10` were fused on one line, domain `23` and `24` were fused on one line, and domain `26` had a stray `A Funnel Smoothing:*` label.
- Active architecture body notes still pointed to R35 as the latest DOC_GLOBAL root/architecture boundary after R36 authority edits existed.

## What Was Done

- Promoted R36 through active root and architecture authority surfaces.
- Kept R35 as the prior R4/counter-residue correction and R34 as the prior source-counter/physical-line refresh.
- Corrected R35 validation-pending residue to static/tool-only R35 evidence wording.
- Added the R36 current boundary to `Docs/Actual Domains of Project.txt`.
- Repaired the malformed 9-echelon / 85-domain map lines without changing the domain ownership model.
- Preserved evidence law: static docs/source/tool outputs are not runtime, Unity, profiler, player-build, save/load, or visual proof.

## Static Validation

- `python Tools\BuildArchitectureAtlas.py`: exit `0`; regenerated `Docs/DEPENDENCY_GRAPH.md` and `Docs/DEPENDENCY_GRAPH.json`.
- `python Tools\test_architecture_atlas.py`: exit `0`, `10` tests OK.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: exit `0`.
- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6637 missing=58`; missing set is `Assets/Dynamic Decals/Resources/Decal.obj` plus RealtimeCSG vendor icon/readme image refs.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: exit `0`, `Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- Active non-archive Docs JSON parse: `JsonFiles=122`, `Bad=0`.
- Root/architecture R4 marker scan: `ScopeFiles=101`, `Missing=0`, `Duplicate=0`.
- Architecture source-anchor filesystem scan: `SourceAnchorPathsChecked=262`, `Missing=0`.
- Root/architecture markdown link scan: `ScopeFiles=101`, `MarkdownLinksChecked=54`, `Missing=0`.
- Project-file/filesystem check: `ChemicalInfluenceGrid.cs` exists; `LogisticsPipeEvents.cs`, `HectonWaterPhysics.cs`, `HectonWaterPhysicsEditor.cs`, and `Assets/Dynamic Decals/Resources/Decal.obj` are absent; this R36 report exists on disk.
- Scoped `git diff --check -- Docs Tools BUILD_PLAYTEST_ISSUES.md MASTER_RELEASE_WORK_PLAN.md ':!Docs/Tasks/*' ':!Docs/AgentLogs/*' ':!Docs/Archive/**' ':!Docs/Modding/**'`: exit `0`, line-ending warnings only.

## Runtime Proof

No runtime proof was run in R36. Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime smoke, platform run, campaign telemetry, analytics endpoint, network send, and visual-route proof remain pending verification.
