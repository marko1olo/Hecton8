# [ARCHIVE] X_012 Historical Report

Archive date: 2026-05-23
Reason: removed from active documentation corpus by X_012; historical evidence only.
Active index: ../../Reports/README.md

# 2026-05-21 Documentation R51 Root / Architecture Encoding, Boundary, Read-Order, and Route Gaps

Date: 2026-05-21
Agent: DOC_GLOBAL_DOCS_REFRESH
Scope: active root documentation, active `Docs/*.md` / `Docs/*.txt` entrypoints, active `Docs/ARCHITECTURE/*.md`, generated architecture atlas metadata, and DOC_GLOBAL status/rationale/log.
Evidence class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC.

## Boundary

R51 supersedes R50 only for active root/architecture documentation currentness, AGENTS/root-doc encoding repair, newly visible architecture boundary gaps, root read-order cleanup, route-card/static-contract gap repair, and volatile source/AtlasCheck orientation.

R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. Older R45 through R34 layers remain historical static correction layers where their exact claims differ from R51.

Runtime proof remains absent. This pass did not run Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, platform run, shader import, network send, or visual-route proof.

## Mandates Consulted

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/ARCH_Pentarchy_Audit.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/ARCH_Signal_Lane_Segregation.txt`
- `.agents-skills/DATA_Runtime_Struct_Layout_ARM64.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## Findings

- `AGENTS.md`, `MASTER_RELEASE_WORK_PLAN.md`, `BUILD_PLAYTEST_ISSUES.md`, `Docs/ARCHITECTURE/AUDIO_DSP_PIPELINE.md`, `Docs/ARCHITECTURE/HABITAT_LOGISTICS_GRAPH.md`, `Docs/ARCHITECTURE/QUEST_DAG_PROTOCOL.md`, `Docs/ARCHITECTURE/README.md`, and `Docs/H8_GLOSSARY.md` contained active mojibake or damaged symbol text. R51 repaired the active text without converting it to runtime proof.
- `Docs/PROCEDURAL_ASSET_PIPELINE.md` contained unrecoverable question-mark placeholder text and mojibake in active procedural-asset instructions. R51 replaced it with a clean static production contract: deliver production assets, prefer visual fakes, keep route ownership explicit, and do not claim runtime readiness from static docs.
- `Docs/ARCHITECTURE/Buoyancy_Sleep_State_SHINOBU_249.md`, `Docs/ARCHITECTURE/KCC_ENVIRONMENTAL_INTEGRATION_SHINOBU_250.md`, and the newly visible `Docs/ARCHITECTURE/SHINOBU_251_SUBMARINE_ADDED_MASS_ROUTE_CARD.md` appeared without the current DOC_GLOBAL actuality boundary.
- `Docs/ARCHITECTURE/SHINOBU_249_BUOYANCY_SLEEP_ROUTE_CARD.md` used a combined producer/consumer field. R51 split it into exact `Producer phase` / `Consumer phase` fields and demoted its dump target to planned/generated-on-fault.
- `Docs/ARCHITECTURE/SHINOBU_251_SUBMARINE_ADDED_MASS_ROUTE_CARD.md` lacked `Review disposition`; R51 added the missing field and demoted its dump target wording.
- Several active root and architecture interior docs still presented R50 as the current boundary. R51 promoted those interiors to R51 and left R50 as a prior static correction layer.
- Current source counters and AtlasCheck state drifted after concurrent source/doc churn and needed recapture.

## Current Source-Scale Orientation

R51 static source scan under `Assets/_Project`:

- `ProjectCs=2169`
- `ScriptCs=2104`
- `NonTestCs=2140`
- `ProjectLines=1494334`
- `ScriptLines=1473147`
- `NonTestLines=1487272`
- `Asmdefs=154`
- `NonTestAsmdefs=152`
- `InterfaceHitsProject=354`
- `InterfaceHitsScripts=351`
- `InterfaceDeclsProject=278`
- `InterfaceDeclsScripts=277`
- `GlobalRegistryContractsPublicInterfaces=62`
- `GlobalRegistryDotHits=6192`
- `PublishSubscribeDirectCallLines=306`
- `SignalCorridorBroadHits=3210`
- `NativeCollectionHits=19840`
- `NativeQueueGenericRefs=904`
- `CreateQueueCalls=73`
- `SignalBusEnsureInitializedHits=279`
- `SignalBusConfigureOrEnsureHits=512`
- `ScriptTypedLanes=1448`

These are volatile STATIC_SOURCE orientation numbers only. They are not compile or runtime proof.

## AtlasCheck Red State

`python Tools\AtlasCheck.py` remains red:

- `ATLAS_CHECK_FAIL references=6881 missing=60`
- Missing refs: one Dynamic Decals vendor asset ref, 57 RealtimeCSG vendor icon/readme image refs, `Assets/_Project/Scripts/Editor/HectonMaskChannelPacker.cs`, and `Assets/_Project/Scripts/Editor/HectonMaterialChannelPackValidator.cs`.

Generated atlas files were refreshed after the R51 text changes. Atlas generation and unit tests passing do not make the atlas `VERIFIED`; `Tools/AtlasCheck.py` still exits non-zero.

## Validation Snapshot

- `python Tools\BuildArchitectureAtlas.py`: PASS.
- `python Tools\test_architecture_atlas.py`: PASS, `10` tests.
- `python -m py_compile Tools\BuildArchitectureAtlas.py Tools\AtlasCheck.py Tools\test_architecture_atlas.py`: PASS.
- `Docs\Modding\Validate_Mod_API_Static.ps1`: PASS, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`.
- Active non-archive/non-deprecated Docs JSON parse with UTF-8/UTF-16 decoding: `JsonFiles=133`, `Bad=0`.
- Active root/architecture/report-index R51 boundary scan: `R51BoundaryScope=140`, `Missing=0`.
- Active architecture route-card exact-field scan: `RouteCardFiles=22`, `Missing=0`.
- Strict stale/proof scan over active root/architecture surfaces: `StrictProofOrStaleHits=0`.
- Active root/architecture mojibake scan: `MojibakeStrictFiles=0`.
- Active architecture dump-label overclaim scan: `DumpLabelOverclaimHits=0`.
- Scoped `git diff --check -- AGENTS.md MASTER_RELEASE_WORK_PLAN.md BUILD_PLAYTEST_ISSUES.md Docs Tools`: PASS, exit `0`, line-ending warnings only.
- `python Tools\AtlasCheck.py`: FAIL, `ATLAS_CHECK_FAIL references=6881 missing=60`.
- Runtime proof remains absent. No Unity import, Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load, platform run, shader import, network send, or visual-route proof was run.
