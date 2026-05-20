# DOC_GLOBAL_DOCS_REFRESH Status

Date: 2026-05-20
Status: IN PROGRESS / STATIC DOCUMENTATION ONLY

Prompt extraction from `Docs/Tasks/CURRENT_BATCH.md`: `PROMPT_NOT_FOUND`.

Active memory note: this file was recreated after concurrent workspace archival/deletion of active `Docs/Tasks/Status_DOC_GLOBAL_DOCS_REFRESH.md`. Historical full snapshots remain under `Docs/Archive/Batch008`, `Docs/Archive/Batch009`, and `Docs/Archive/Batch010`.

## Current Scope

User scope: root and architecture documentation. Study internals, correct stale/current/proof wording, add actuality boundaries, verify local links/source anchors where possible, and write evidence into docs instead of sorting files.

Evidence law: static docs/source scans are not Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, platform run, or visual proof.

Relevant mandates read for this continuation:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/ARCH_Signal_Lane_Segregation.txt`
- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

## R36 Completed Baseline

- [x] Re-read AGENTS authority, domain map, mandate registry, archived DOC_GLOBAL status/rationale/log, and attempted prompt extraction. DOD: prompt extraction returned `PROMPT_NOT_FOUND`; active status/rationale/log absence was detected. Rejected: relying only on compacted chat. Estimate: 0 us runtime.
- [x] Promoted `Docs/Reports/2026-05-20_DOCUMENTATION_R36_ROOT_ARCHITECTURE_AUTHORITY_SPINE_LOCAL.md` as latest local root/architecture authority-spine and domain-map correction. DOD: R36 report exists on disk and active root/architecture entrypoints route through R36. Rejected: leaving R35 validation-pending residue after R36 static validation. Estimate: 0 us runtime.
- [x] Corrected `Docs/Actual Domains of Project.txt` active domain formatting and R36 boundary. DOD: fused domain lines were split, `Funnel Smoothing` typo corrected, 9-echelon / 85-domain model retained. Rejected: reordering domains instead of fixing internal text. Estimate: 0 us runtime.
- [x] Added R4/R36 boundaries to root work-plan surfaces. DOD: `BUILD_PLAYTEST_ISSUES.md` and `MASTER_RELEASE_WORK_PLAN.md` carry active actuality boundaries. Rejected: treating root files as outside documentation scope. Estimate: 0 us runtime.
- [x] Regenerated atlas and ran static validation. DOD: validations recorded in the R36 report and current LOG. Rejected: claiming Unity/runtime/profiler/player-build proof. Estimate: 0 us runtime.

## R36 Validation Snapshot

- `python Tools\BuildArchitectureAtlas.py`: exit `0`.
- `python Tools\test_architecture_atlas.py`: exit `0`, `10` tests OK.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: exit `0`.
- `python Tools\AtlasCheck.py`: exit `1`, `ATLAS_CHECK_FAIL references=6637 missing=58`.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: exit `0`, `Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- Active non-archive Docs JSON parse: `JsonFiles=122`, `Bad=0`.
- Root/architecture R4 marker scan: `ScopeFiles=101`, `Missing=0`, `Duplicate=0`.
- Architecture source-anchor scan: `SourceAnchorPathsChecked=262`, `Missing=0`.
- Root/architecture markdown link scan: `ScopeFiles=101`, `MarkdownLinksChecked=54`, `Missing=0`.
- Targeted R36 diff-check: exit `0`, line-ending warnings only.

## R36 Blockers

- No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, mod runtime, platform run, analytics endpoint, network send, or visual-route proof was run.
- `Tools/AtlasCheck.py` remains red on `58` missing references: `Assets/Dynamic Decals/Resources/Decal.obj` plus RealtimeCSG vendor icon/readme image refs.
- Generated project stale includes remain: `LogisticsPipeEvents.cs`, `HectonWaterPhysics.cs`, and `HectonWaterPhysicsEditor.cs` are absent while referenced by generated project files.

## R37 Checklist

- [x] Recreate active DOC_GLOBAL status/rationale/log after concurrent deletion. DOD: required files exist in active `Docs/Tasks` and `Docs/AgentLogs`. Rejected: restoring unrelated deleted SHINOBU files. Estimate: 0 us runtime.
- [ ] Re-scan root/architecture docs for R36/R35/R34 residue, false proof language, absent artifact references, and missing R4/current boundaries. DOD: exact files and patches recorded. Estimate: 0 us runtime.
- [ ] Patch verified documentation defects only. DOD: root/architecture interiors updated with evidence-class language and current blockers. Estimate: 0 us runtime.
- [ ] Regenerate/validate static documentation artifacts. DOD: atlas/tests/json/R4/link/source-anchor/diff checks recorded. Estimate: 0 us runtime.
- [ ] Write R37 report and append final log/rationale. DOD: `Docs/Reports/2026-05-20_DOCUMENTATION_R37_*.md` exists and entrypoints point to it if it changes current orientation. Estimate: 0 us runtime.

