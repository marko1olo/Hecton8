# Docs Index

Date: 2026-05-15
Status: PENDING VERIFICATION

Purpose: stable documentation entry point. Dated reports are evidence snapshots and counters, not the permanent project brain. If a dated report changes policy, promote the rule into `AGENTS.md`, `.agents-skills`, or one of the stable authority docs below.

## 2026-05-15 Current-Disk Build / H-Phi Boundary

Evidence class: `CLI_COMPILE` plus `STATIC_SOURCE_FULL_SCAN`. Runtime proof remains absent.

- Latest observed Core CLI compile artifact: `Docs/AgentLogs/Build_DOC_AUDIT_R49_20260515_205546_AfterKinematicsTierCacheCore.log` with exit summary `Docs/AgentLogs/Build_DOC_AUDIT_R49_20260515_205546_AfterKinematicsTierCacheCore.exit.txt` reports `EXIT=0`, `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, and output `Temp\bin\Debug\Hecton8.Core.dll`.
- Earlier same-day DOC_AUDIT Core artifacts include `Docs/AgentLogs/Build_DOC_AUDIT_CONTINUATION_20260515_183508_Hecton8Core.log` failing on stale generated-CLI visibility of `MacroDatabasePayloadFlags` and `Docs/AgentLogs/Build_INTEGRATION_ASSEMBLY_SURGEON_20260515_203354_CurrentDisk36.log` failing on a transient non-existent `ScalabilityTierBindingBridge` reference. The later clean R49 artifact supersedes those failed attempts for current-disk CLI status only.
- R49 H-Phi static budget artifact: `Docs/AgentLogs/HPhi_DOC_AUDIT_R49_20260515_210144_AfterKinematicsTierCacheBudgetGate.json` with exit summary `Docs/AgentLogs/HPhi_DOC_AUDIT_R49_20260515_210144_AfterKinematicsTierCacheBudgetGate.exit.txt` reports `EXIT=0`. Scores include `DataSovereignty=0.021306032`, `MemoryAlignment=0.506309148`, `HPhiStaticRisk=0.000636091`, and `RiskIntegration=0.058965935`; static counters include `GlobalRegistrySurface=5060/5075`, `GetComponentCalls=321/321`, `NativeArrayRefs=7074/7074`, `ManagedFormatSurface=534/564`, `JobCompleteSurface=58/58`, `PrimaryManagedRuntimeRisk=147/177`, `DuplicateSignalNames=0`, `UnityUpdateMethods=0`, `LegacyEventPublish=28/28`, `LinqSurface=3/5`, `CoroutineSurface=0/0`, and `AupPrecisionRisk=0`.
- Core graph debt in that H-Phi artifact remains at the current budget ceiling: `CoreAsmdefDebtReferenceCount=25`, `GeneratedProjectDebtReferenceCount=10`, `SourceBackedBridgeDebtReferenceCount=14`, `SourceBackedCompileBridgeDebtReferenceCount=8`, and `ProjectReferenceReplacementDebtReferenceCount=6`.
- DOC_HONEST_ANALYSIS R3 rechecked H-Phi at `Docs/AgentLogs/HPhi_DOC_HONEST_ANALYSIS_R3_20260515_CurrentStaticSummary.json`, found transient unused Core asmdef reference drift (`Hecton8.World.GPR`), aligned current file/index so `Assets/_Project/Scripts/Hecton8.Core.asmdef` contains no such Core reference, and then passed the Core graph gate at `Docs/AgentLogs/HPhi_DOC_HONEST_ANALYSIS_R3_20260515_CoreGraphAfterGprPrune.json` with Core debt still `25/10/14/8/6` and unused Core candidates cleared. Follow-up Core CLI compile artifact `Docs/AgentLogs/Build_DOC_HONEST_ANALYSIS_R3_20260515_AfterGprAsmdefPrune_Hecton8Core.log` exits `0` with `Build succeeded`, `0 Warning(s)`, and `0 Error(s)`.
- This supersedes earlier same-day MemoryAlignment failure artifacts and the interim `HPhi_DOC_AUDIT_R47_20260515_201801_AfterWfcWaveBudgetGate.json` failure (`GlobalRegistrySurface=5076 > 5075`) for current-disk static H-Phi status only. It does not prove Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, save/load route, or visual quality.

## 2026-05-13 DOC_AUDIT X-Ray Override

Read `Reports/2026-05-13_DOC_AUDIT_XRAY.md` before trusting May 11 counters or proof links.

Current static audit facts:

- the cited May 11 build artifacts `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.summary.txt` and `.log` are absent from the current filesystem
- May 11 compile-success claims remain report text only; the current compile evidence is the separate R43 external root `Hecton8*.csproj` no-restore CLI recheck, not restored May 11 artifacts
- 2026-05-15 root cleanup supersedes the May 13 root counters: current root scan sees only `AGENTS.md`, `MASTER_RELEASE_WORK_PLAN.md`, and `BUILD_PLAYTEST_ISSUES.md` in the documentation/evidence scope; root `.log`, `.json`, `.xml`, `.png`, `.zip`, and stale cleanup-script artifacts were moved under `Docs/DEPRECATED/`.
- May 13 root text surface was no longer the May 11 shape: that scan saw `6` `.md`, `3` `.log`, and `3` `.json` files before the May 15 cleanup
- direct `Docs/` root no longer contains `не откр.md`; the stale batch-prompt dump was moved to `DEPRECATED/Root_Stale_Batch_Prompt_Dumps_2026-05-13/`
- current first-party asmdef count is `24`; previous `13`, `22`, and `23` asmdef atlas claims are stale
- source-count values are volatile in the active workspace; the R4 static refresh sees `1411` project C# files, `1365` script C# files, `869871` project source lines, `852315` script source lines, `215` interface declaration hits, `51` direct public interfaces in `GlobalRegistryContracts.cs`, and `24` first-party asmdefs
- R5/R6 package/config scan: Unity pin `6000.4.1f1`; URP `17.4.0`; Addressables `2.7.6`; Input System `1.19.0`; AI Navigation `2.0.11`; normative BuildSettings scenes remain `00_BOOTSTRAP`, `01_MAIN_MENU`, `02_HECTON_WORLD`; forbidden UPM IDs are absent, but physical legacy Astar/Easy Save/Demigiant/DarkTonic folders and live `DOTWEEN`/vendor scripting defines remain contamination; embedded Crest/MicroSplat/ShaderGraph package drift requires Unity import/build proof
- R7 authority patch: `AGENTS.md` and `.codexrules/AGENTS.md` now match current Low URP mapping (`URP_Low` -> `Mobile_Renderer`, render scale `0.85`) and no longer instruct agents to extend Easy Save 3 usage
- R8 world/scatter scan: large world files contain real scatter/residency/sampling/vegetation systems and `Assets/_Project/Data/World` has `285` `.asset` files, but production scene wiring and Addressables payload readiness remain unproven by static evidence
- R9/R10 root/atlas boundary: root authority is still only `AGENTS.md`, `MASTER_RELEASE_WORK_PLAN.md`, and `BUILD_PLAYTEST_ISSUES.md`; `BROKEN_PREFABS.md` now lives at `Docs/Reports/2026-05-13_BROKEN_PREFABS_STATIC_SNAPSHOT.md`, former root `PROJECT_ATLAS.md` and `TERRAIN_AND_BIOME_REALITY_MAP.md` now live in `Docs/DEPRECATED/Root_Compatibility_Mirrors_2026-05-15/`, and `Docs/PROJECT_ATLAS.md` remains an asmdef graph snapshot only
- R11 SpaceEngine research cleanup: `Docs/SPACE_ENGINE_RESEARCH/SPACE_ENGINE_MATH_INTEGRATION_2026-05-05.md` now uses the current MapMagic node path and marks its compile/smoke data as historical until the current Unity smoke harness is rerun
- R12 Omega smoke artifact drift: current `Library/OmegaAutonomySmokeTester.json` reads `FAIL` on `nativeSentinelBalance` (`allocationDelta=2`, `trackedByteDelta=2560`); older saved PASS / OMEGA smoke artifacts remain scoped historical evidence only
- R13 active documentation manifest boundary: `Docs/Reports/*ACTIVE_DOCUMENTATION_MANIFEST.json` files are dated generated snapshots only; their counts/build states/authority lists are superseded by `Docs/Reports/README.md` and `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`
- R14/R19/R20/R21/R23/R26/R27/R28 gameplay/resource-loop scan: item/catalog/recipe/fabricator/inventory/scarcity/logistics code is real; R14 found `23 / 27` resource-node harvest items lacking `worldPrefab` and duplicate copper authority, R19 reduced the primary-harvest `worldPrefab` gap to `16 / 27`, R20/R21 made `ContentSanityValidator` fail resource-node yield refs that are non-catalog, lack `worldPrefab`, or violate pickup prefab contract, R21 reduced current resource-node primary-harvest gaps to `0 / 27` missing `worldPrefab` and `0 / 27` non-catalog, R23 added duplicate `ItemData.PersistentId` / catalog ambiguity validation, R26 added quest item/prerequisite route validation, R27 added recipe/result/ingredient/catalog validation plus craft-quest recipe-output validation, and R28 added scan-gate warnings for scan-locked recipes without known generic/prefab unlock routes; runtime first-hour pickup/craft/quest remains `PENDING VERIFICATION`
- R15 AI/Fauna scan: static fauna data coverage is real (`22` recursive creature archetypes, `22` fauna data templates, `108` fauna biome datasets, `432` non-null biome spawn prefab entries, `13` fauna family profiles, `6` generated proxy prefabs), but current static scans did not prove serialized `FaunaDirector`/`WorldFaunaSpawnRegistry` production-scene wiring; bootstrap falls back to `DemiurgeFaunaSimulationService.Shared` when no real fauna director registers `IFaunaSim`, and `.codex-artifacts/fauna-omega-smoke-2026-05-05.log` is a return-code-`1` artifact, not PASS
- R16/R18/R22/R24/R25 Tools/PDA/first-hour scan: tool/scan/interaction code and data are real (`12` tool ItemData assets, `12` held prefabs, `12` world prefabs, all tool ItemData `worldPrefab` refs non-null); R16 found hidden `ToolLoadoutProvisioner` startup grants, R18 disabled/gated them, R22 made `PlayerPDA.Open()` fail closed without a configured panel/tab shell, R24 added `ContentSanityValidator` gates for active tool metadata -> held prefab -> ItemData/catalog/worldPrefab routes, and R25 added a player-prefab dev-provisioner startup flag gate; `LogicSpanner` remains orphan metadata/source without item/prefab/catalog/recipe route and visible PDA shell wiring remains `PENDING VERIFICATION` because `DiegeticPDAController` scene/prefab placement was not proven
- R17 Rendering/Visor/Shader scan: render architecture is substantial (`136` shader-like files under `_Project`, `21` visor renderer-feature files with `RecordRenderGraph`, active Low/Mobile post/noir/SSDO/shaft stack), but GPU Resident Drawer/GPU occlusion are disabled in scanned URP assets, many visor features still use `AddUnsafePass`, and scene wiring/perf/VRAM/Frame Debugger proof remains `PENDING VERIFICATION`
- R29/R30/R31/R32/R33/R36/R37/R38 compile/persistence reconciliation: Unity `6000.4.1f1` batchmode import/script-compilation evidence exists at `Library/Codex_DOC_AUDIT_UnityBatchCompile.log`; R37 added local Unity Bee/Roslyn temp-output probes for `Hecton8.Core.Memory` and `Hecton8.Core`, both exit code `0`; R38 demoted that full-Core result as stale under then-current churn. R43 later superseded the compile-blocked note with a clean external root `Hecton8*.csproj` no-restore CLI recheck, while Unity MCP Console remains unavailable, so this is still compile/source evidence only. `SaveManager` / `H8BinaryWorldPager` now fail-close a locked `world_data.h8bin` pager instead of throwing through bootstrap; R30/R31/R32/R33/R36/R37/R38 further enforce single-writer page-file sharing, joinable pager worker shutdown, no stale pending counters after unexpected pager worker faults, no per-chunk global voxel snapshot writes, lazy pager file open outside `InitializeNativeBuffers()`, first-use allocation for large save buffers, pager-fault staging guard, Core.Memory asmdef boundary hygiene, WFC outpost MacroDB bitmask persist/restore contract coverage, and no orphaned voxel pager prefetch from chunk load. Runtime save/load, WFC outpost restore, Memory Profiler, and chunk-local voxel hydrate proof remain `PENDING VERIFICATION`.
- R39/R40/R41/R42/R43 generated-project correction and compile sweep: R39 found `Hecton8.Core.asmdef` references `23` first-party assemblies absent from current `Hecton8.Core.csproj` and added editor-only `CSPROJ001`. R40 confirmed a non-destructive Unity batchmode project-refresh attempt did not regenerate the stale root projects, then added a source-backed `Directory.Build.targets` bridge instead of editing generated `.csproj` files. R41 serially rechecked every root `Hecton8*.csproj`; R42 propagated that boundary into active reference docs. R43 rechecked all eight root projects as single-project no-restore builds: Core, Editor, PlayModeTests, World.Contracts, World.Dots, Bootstrap.Contracts, Input.Generated, and Input each returned `0 Warning(s)` / `0 Error(s)` with `LASTEXITCODE=0` after restore assets and referenced `Temp\bin\Debug` DLLs existed. Fresh no-restore attempts can still fail on missing `Temp\obj\...\project.assets.json`, missing referenced `Temp\bin\Debug` DLLs, or shared `Temp\obj` locks under concurrent agents. Full restore graphs carry vendor/package warnings, but the isolated root no-restore surface is clean. This is `CLI_COMPILE` evidence only, not Unity Console/Play Mode/profiler/player-build proof; Unity MCP still fails at `127.0.0.1:8088/mcp`.
- R34 player-movement large-file check: current `HectonPlayerMovement.cs` is 740,426 bytes / 13,240 lines and remains a load-bearing fused integration hub, not simple walking code. R34 adds a narrow ladder snap component cache in the fixed locomotion path. Runtime locomotion/profiler proof remains `PENDING VERIFICATION`.
- R35 world-streaming/PDA HLOD upload check: `PDAMapTab` now gates the fixed `16 x float4` HLOD POI GPU upload by `IStreamingBackpressureService.ActiveImpostorVersion` and count; `WorldChunkResidencyManager` separates point/read-model versioning from renderer matrix dirty versioning so PDA fade can update without forcing matrix uploads. Runtime PDA map/profiler proof remains `PENDING VERIFICATION`.

Evidence class is STATIC_SOURCE / STATIC_DOC / FILESYSTEM / PACKAGE_LOCK plus R29 UNITY_BATCHMODE_IMPORT_COMPILE and local Bee/Roslyn probes where explicitly named. PlayMode, profiler, GCMonitor, player build, scene wiring, save/load, and visual runtime proof remain absent.

## Stable Authority Spine

- `../AGENTS.md` - global operating contract.
- `../.agents-skills/README.md` - mandate registry index and conflict-resolution policy.
- `../.agents-skills/*` - task-specific technical mandates; read selectively before implementation.
- `HECTON8_GLOBAL_ARCHITECTURE_MAP.md` - whole-project architecture map.
- `HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md` - main runtime execution anchor.
- `PROCEDURAL_ASSET_PIPELINE.md` - primary procedural asset contract.
- `SYSTEMS_CONTRACTS.md` - non-asset system contracts.
- `QUALITY_GATES.md` - acceptance gates and proof requirements.
- `ARCHITECTURE/README.md` - stable architecture pack index.
- `ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md` - stable visual-realistic-fake / cinematic-cheat ledger.
- `ARCHIVARIUS REPORTS/01_GENERAL_INFO/README.md` - stable Archivarius orientation index.
- `ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/README.md` - report-vault trust boundary.
- `DOC_GOVERNANCE.md` - placement rules for active/reference/archive docs.
- `ROOT_DOCS_REFERENCE.md` - root/doc boundary and relocation map.
- `PROJECT_STATE_STATIC_XRAY.md` - durable static project-state risk register; not runtime proof.

## Current Evidence Snapshots

- `Reports/2026-05-13_DOC_AUDIT_XRAY.md` - current documentation reality override; demotes missing May 11 build artifacts, root/doc surface drift, source-count drift, stale `Current compile-only evidence` lines, interface-count drift, Archivarius path drift, asmdef count drift, package/player-settings drift, world/scatter wiring proof gaps, root mirror/atlas scope confusion, stale manifest authority, gameplay resource-loop proof gaps, AI/Fauna data-vs-runtime-wiring proof gaps, and Tools/PDA first-hour proof gaps.
- `Reports/2026-05-15_COMPUTE_AUDIT/README.md` - grouped compute-cost report bundle moved out of repository root; static/report evidence only.
- `Reports/2026-05-13_BROKEN_PREFABS_STATIC_SNAPSHOT.md` - generated prefab snapshot moved out of repository root; not Unity import, Console, Play Mode, or player-build proof.
- `PROJECT_STATE_STATIC_XRAY.md` - current durable static audit anchor for runtime-spine, large-file, scatter, Addressables, audio-memory, third-party contamination, gameplay economy/resource acquisition, AI/Fauna data-vs-runtime-wiring, Tools/PDA first-hour route hygiene, test-depth, and verification-gap findings.
- `Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md` - historical May 11 documentation/data boundary; superseded by the May 13 X-Ray for current counters and missing-artifact evidence.
- `Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json` - latest historical machine-readable active documentation manifest snapshot; its counters, authority list, and build-state fields are not current authority after the May 13 X-Ray.
- `Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md` - latest `.agents-skills` actuality pass for the visual-realistic-fake doctrine; supersedes conflicting simulate-first mandate wording where 2026-05-11 overrides were added.
- `Reports/2026-05-08_DOCUMENTATION_CONTINUATION_SYNC.md` - previous documentation continuation sync; superseded by the May 11 current-data continuation for compile/counter freshness.
- `Reports/2026-05-08_ACTIVE_DOCUMENTATION_MANIFEST.json` - previous machine-readable active documentation manifest.
- `Reports/2026-05-07_MAIN_DOCUMENTATION_CURRENT_STATE_REFRESH.md` - previous main documentation refresh; historical after the May 8 continuation sync.
- `Reports/2026-05-07_FINAL_INQUISITION_NATIVE_SCANNER.md` - latest final-inquisition fallback compile/MCP-blocked boundary.
- `Reports/2026-05-07_LIVE_CHURN_CONTINUATION_SYNC.md` - previous live-churn synchronization boundary; superseded by May 8 and then May 11 evidence for current counters.
- `Reports/2026-05-07_PROJECT_ATLAS_SYNCHRONIZATION_PASS.md` - previous Project Atlas/source-count synchronization pass and May 7 static inventory boundary.
- `Reports/2026-05-07_BRUTAL_SYNCHRONIZATION_REPORT.md` - current May 7 hallucination check, manifest, stale-symbol, native-lifecycle, and documentation cleanup boundary.
- `Reports/2026-05-07_ACTIVE_DOCUMENTATION_MANIFEST.json` - previous machine-readable active documentation manifest.
- `Reports/2026-05-06_DOCUMENTATION_SYNCHRONIZATION_PASS.md` - previous broad documentation synchronization pass; historical after the May 8 synchronization pass.
- `Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md` - latest documentation sorting and authority map.
- `Reports/2026-05-04_DOCUMENTATION_HEADER_ARCHIVE_QUEUE.md` - current header normalization and archive/move queue.
- `Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md` - latest documentation sweep and current May 4 build/guard/MCP evidence boundary.
- `Reports/2026-05-04_WARNING_CLEANUP.md` - latest first-party warning cleanup; Core build and post-refresh Unity console readback are clean for this slice.
- `Reports/2026-05-05_OMEGA_AUTONOMY_FORENSIC_HARDENING.md` - latest bounded Omega-autonomy evidence; latest serial Core build is `0 Warning(s)` / `0 Error(s)`, while the last warning-bearing Omega log is dependency/vendor-only with `0` first-party `Assets/_Project/Scripts` warning matches.
- `Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md` - latest foundation guard repair addendum; source guard exits `0`.
- `Reports/2026-05-04_CELESTIAL_ORBITAL_PROTOCOL_METEOR_REPORT.md` - latest celestial/meteor protocol source-build evidence; runtime visual/audio/profiler proof absent.
- `Reports/2026-05-04_CELESTIAL_ORBITAL_PROTOCOL_REPORT.md` - celestial orbital protocol source-build evidence; PlayMode smoke and profiler proof absent.
- `Reports/2026-05-04_CELESTIAL_ENVIRONMENT_ORBITAL_SYNC_REPORT.md` - celestial environment sync source-build evidence; runtime/profiler proof absent.
- `Reports/TERRAIN_AND_BIOME_REALITY_MAP.md` - canonical terrain/biome report; former root duplicate was moved to `DEPRECATED/Root_Compatibility_Mirrors_2026-05-15/`.
- `Reports/2026-05-03_FOUNDATION_HARDENING_CONTINUATION.md` - latest foundation-hardening evidence and runtime-risk boundary.
- `Reports/2026-05-03_SETTINGS_PERSISTENCE_REGISTRY_REBIND.md` - latest settings/UserOptions registry rebind and persistence-order hardening.
- `Reports/2026-05-03_HABITAT_GRAPH_ANCHOR_STATE_HARDENING.md` - habitat graph anchor-state correction and verification boundary.
- `Reports/2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md` - previous documentation sweep and historical May 2 local build-evidence addendum.
- `Reports/2026-05-01_CURRENT_PROJECT_STATE.md` - dated conceptual evidence retained for reference; not the first authority entry point.
- `ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md` - current workspace atlas.
- `ARCHIVARIUS REPORTS/01_GENERAL_INFO/DOC_AUTHORITY_CLASSIFICATION.md` - current importance sorting for active, reference, archive, and deprecated docs.
- `ARCHIVARIUS REPORTS/01_GENERAL_INFO/CONCEPTUAL_SYSTEM_AUTHORITY_MAP.md` - current concept-level classification of load-bearing, transitional, presentation, experimental, and historical systems.

## Current Audit Outputs

- `Reports/2026-05-15_COMPUTE_AUDIT/README.md`
- `Reports/2026-05-13_BROKEN_PREFABS_STATIC_SNAPSHOT.md`
- `Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`
- `Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`
- `Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`
- `Reports/2026-05-08_DOCUMENTATION_CONTINUATION_SYNC.md`
- `Reports/2026-05-08_ACTIVE_DOCUMENTATION_MANIFEST.json`
- `Reports/2026-05-07_MAIN_DOCUMENTATION_CURRENT_STATE_REFRESH.md`
- `Reports/2026-05-07_FINAL_INQUISITION_NATIVE_SCANNER.md`
- `Reports/2026-05-07_LIVE_CHURN_CONTINUATION_SYNC.md`
- `Reports/2026-05-07_PROJECT_ATLAS_SYNCHRONIZATION_PASS.md`
- `Reports/2026-05-07_BRUTAL_SYNCHRONIZATION_REPORT.md`
- `Reports/2026-05-07_ACTIVE_DOCUMENTATION_MANIFEST.json`
- `Reports/2026-05-06_DOCUMENTATION_SYNCHRONIZATION_PASS.md`
- `Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`
- `Reports/2026-05-04_DOCUMENTATION_HEADER_ARCHIVE_QUEUE.md`
- `Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`
- `Reports/2026-05-04_WARNING_CLEANUP.md`
- `Reports/2026-05-05_OMEGA_AUTONOMY_FORENSIC_HARDENING.md`
- `Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`
- `Reports/2026-05-04_CELESTIAL_ORBITAL_PROTOCOL_METEOR_REPORT.md`
- `Reports/2026-05-04_CELESTIAL_ORBITAL_PROTOCOL_REPORT.md`
- `Reports/2026-05-04_CELESTIAL_ENVIRONMENT_ORBITAL_SYNC_REPORT.md`
- `Reports/2026-05-04_HYDRAULIC_EROSION_ENGINE_SURGERY_LOG.md` - hydraulic erosion implementation/surgery report; import/compile, MapMagic graph, harness output, GCMonitor, and profiler proof pending.
- `Reports/TERRAIN_AND_BIOME_REALITY_MAP.md`
- `Reports/2026-05-03_FOUNDATION_HARDENING_CONTINUATION.md`
- `Reports/2026-05-03_SETTINGS_PERSISTENCE_REGISTRY_REBIND.md`
- `Reports/2026-05-03_HABITAT_GRAPH_ANCHOR_STATE_HARDENING.md`
- `Reports/2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md` - historical May 2 sweep, not latest authority.
- `Reports/2026-05-01_CURRENT_PROJECT_STATE.md`
- `Reports/2026-05-01_OBJECTIVE_PROJECT_CONCLUSION.md`
- `Reports/2026-05-01_EDITOR_LOG_CONSOLE_STABILIZATION.md`
- `Reports/2026-05-01_EVENT_BUS_SPATIAL_HASH_COMPILE_DELTA.md`
- `Reports/2026-05-01_COMPILE_STABILIZATION_CONTINUATION.md`
- `Reports/2026-05-01_EVENT_CASCADE_RECHECK.md`
- `Reports/2026-05-01_ZERO_GC_JOBS_CONTINUATION_DELTA.md`
- `ARCHIVARIUS REPORTS/01_GENERAL_INFO/DOC_AUTHORITY_CLASSIFICATION.md`
- `ARCHIVARIUS REPORTS/01_GENERAL_INFO/CONCEPTUAL_SYSTEM_AUTHORITY_MAP.md`
- `2026-04-30_Codex_Full_Project_Forensic_Audit/README.md`
- `Reports/TOTAL_CODEBASE_AUDIT_V2.md`
- `Reports/OMEGA_CORE_ENFORCEMENT_2026-05-01.md`
- `Reports/AWAITABLE_MEMORY_COMPACTION_SURGERY_LOG.md`
- `Reports/DOOMSDAY_FLAW_REPORT.md`
- `ARCHITECTURE/SYSTEM_INTERCONNECT_MATRIX.md`

Current-state rule:

- use `Reports/2026-05-13_DOC_AUDIT_XRAY.md` as the latest documentation counter/status override
- use `Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json` only as the latest historical machine-readable active documentation manifest snapshot; do not use its counters, authority list, or build-state fields as current proof
- use `Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md` as historical May 11 documentation/data evidence, not the latest counter/status boundary
- use `Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md` as the latest `.agents-skills` visual-fake doctrine boundary; it is documentation-only and does not certify runtime behavior
- use `Reports/2026-05-08_DOCUMENTATION_CONTINUATION_SYNC.md` as the previous R186 documentation/build boundary when May 11 docs do not cover the question
- use `Reports/2026-05-07_MAIN_DOCUMENTATION_CURRENT_STATE_REFRESH.md` as the previous main-documentation counter/status boundary after the final inquisition runtime patch and latest May 7 MCP console retry
- use `Reports/2026-05-07_FINAL_INQUISITION_NATIVE_SCANNER.md` as the latest final-inquisition compile/MCP-blocked evidence boundary
- use `Reports/2026-05-07_LIVE_CHURN_CONTINUATION_SYNC.md` as the previous same-day live-churn boundary and supersession layer for conflicting May 7 counters where the new main refresh does not provide newer data
- use `Reports/2026-05-07_PROJECT_ATLAS_SYNCHRONIZATION_PASS.md` as the Project Atlas/source-count synchronization layer when not contradicted by the live-churn continuation report
- use `Reports/2026-05-07_BRUTAL_SYNCHRONIZATION_REPORT.md` as the current hallucination-check, stale-symbol, manifest, native-lifecycle, and whitespace-cleanup report
- use `Reports/2026-05-06_DOCUMENTATION_SYNCHRONIZATION_PASS.md` as the previous broad documentation synchronization layer, not the current counter authority
- use `Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md` as the latest documentation sorting and authority classification map
- use `Reports/2026-05-04_DOCUMENTATION_HEADER_ARCHIVE_QUEUE.md` as the current header normalization and archive/move queue
- use `Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md` as the latest broad documentation-sweep and current May 4 build/guard/MCP evidence addendum
- use `Reports/2026-05-04_WARNING_CLEANUP.md` as the latest first-party warning-cleanup addendum for Core compile and current post-refresh Unity console warning readback
- use `Reports/2026-05-05_OMEGA_AUTONOMY_FORENSIC_HARDENING.md` as the current bounded Omega-autonomy hardening report and current dependency-warning classification for the Omega build slice
- use `Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md` as the latest foundation guard source/build addendum
- use the May 4 celestial reports as source-build evidence only; do not claim runtime celestial/meteor behavior without PlayMode/profiler proof
- use `Reports/2026-05-01_CURRENT_PROJECT_STATE.md` as the conceptual entry point for system ownership and active risks; the filename is retained as a stable anchor and now includes May 4 evidence
- use `Reports/2026-05-03_FOUNDATION_HARDENING_CONTINUATION.md` as the latest dated foundation-hardening implementation addendum, after the May 4 sweep
- use `Reports/2026-05-03_SETTINGS_PERSISTENCE_REGISTRY_REBIND.md` for the latest dated settings/UserOptions registry rebind and persistence-order hardening
- use `Reports/2026-05-03_HABITAT_GRAPH_ANCHOR_STATE_HARDENING.md` for the latest dated habitat graph anchor-state scratch-buffer correction
- use `Reports/2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md` as historical May 2 documentation/build evidence only
- use older dated reports as evidence only after checking their latest delta sections
- do not treat archive/deprecated bundles as active authority unless a current index links them explicitly; standard agent context loading excludes `DEPRECATED/`, `Reports/DEPRECATED/`, `_Archive/`, and `ARCHIVARIUS REPORTS/03_OBSOLETE/`

Current verification boundary:

- historical May 13 R4 static scan reported `Docs/**/*.md` total `918`, active markdown `283`, active non-`Docs/Reports` markdown `203`, active direct `Docs/Reports/*.md` `80`, docs JSON `10`, root `.md` files `6`, root `.log` files `3`, root `.json` files `3`, and direct `Docs/*.md` files `11`; the May 15 root cleanup supersedes the root file counts, and broad active counts must be rerun before use
- current May 13 R4 source-count orientation reports `Assets/_Project/**/*.cs` `1411`, `Assets/_Project/Scripts/**/*.cs` `1365`, first-party non-test C# files `1401`, project physical lines `869871`, script physical lines `852315`, non-test physical lines `867132`, direct scripts `336`, interface declaration hits `215`, `GlobalRegistryContracts.cs` direct public interfaces `51`, and first-party asmdefs `24`; source counts include the current dirty workspace state and are not runtime proof
- current May 13 R5 package/config orientation reports `ProjectVersion.txt` `6000.4.1f1`; `Packages/manifest.json` lacks `com.demigiant.dotween`, `com.darktonic.masteraudio`, `com.moodkie.easysave`, and `com.arongranberg.astar`; legacy physical asset folders still exist for Astar (`605` files), Easy Save 3 (`422` files), Demigiant (`357` files, including DOTween/DOTweenPro), and DarkTonic/MasterAudio (`346` runtime files plus separate editor/resources files); first-party `.cs` scan found no active DG.Tweening/ES3/Easy Save/MasterAudio/DarkTonic usage
- historical May 11 active manifest reported `Docs/**/*.md` total `449`, active markdown `236`, active non-report docs `166`, direct `Docs/Reports/*.md` `70`, active JSON `15`, root `.md` files `5`, root `.txt`/`.log` files `2`, `Docs` total files `897`, and active markdown header debt `0`; those counters are superseded by the May 13 R4 static scan where they conflict
- historical May 11 source-count orientation reported `Assets/_Project/**/*.cs` `1306`, `Assets/_Project/Scripts/**/*.cs` `1262`, project physical lines `770577`, script physical lines `753858`, direct scripts `335`, and `GlobalRegistryContracts.cs` public interfaces `40`; those counters are superseded by the May 13 R4 static scan where they conflict
- May 11 report text claimed completed full Core dependency build evidence at `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.summary.txt`, but the May 13 DOC_AUDIT filesystem check did not find that summary or raw log; treat it as stale report text until the artifact is restored or replaced. It is not Unity Console, Play Mode, profiler, GCMonitor, player-build, frame-time, memory, import, scene-wiring, or visual-quality proof
- no fresh Unity MCP, Unity Console, or Play Mode proof was captured in the May 11 documentation continuation; older MCP readbacks are historical only
- previous May 7 main documentation refresh scan reported `Docs/**/*.md` total `443`, active markdown `230`, active non-report docs `163`, full active markdown header debt `0`, root `.md` files `5`, root `.txt`/`.log` files `0`, `Docs/Reports/*.md` `67`, `Assets/_Project/**/*.cs` `1233`, `Assets/_Project/Scripts/**/*.cs` `1192`, project physical lines last observed `683064`, script physical lines last observed `667771`, and `GlobalRegistryContracts.cs` public interfaces `39`; those counters are superseded by the May 11 evidence boundary
- previous May 7 build-master Core artifact `CodexArtifacts/2026-05-07_BUILD_MASTER_CORE_BUILD.log` reports `Build FAILED`, `55 Warning(s)`, `2 Error(s)`; its blockers were `HectonVoxelEngine.cs(4143,47)` missing `GlobalRegistry.PlayerRigidbody` and `HectonVoxelEngine.cs(4144,62)` missing `GlobalRegistry.PlayerMovement`; the missing May 11 artifact does not currently supersede this with artifact-backed proof
- latest May 6 Unity MCP editor readback reports active scene `00_BOOTSTRAP`, Play Mode off, compiling false, ready for tools, console error/warning entries `0`, render textures `37`, render texture bytes `56,320,492`, renderer `PC_Renderer`, and `9` renderer features; this is editor-state proof only, not Play Mode/profiler/player-build proof
- latest May 7 recursive `Docs` filesystem scan inventories `866` non-meta files; archive/deprecated/extracted research payloads remain evidence/provenance, not active authority
- latest May 11 official Unity release-page check found Unity `6000.4.6f1` released on `2026-05-05`, newer than the local project pin `6000.4.1f1`; do not treat this as permission to upgrade without the LTS migration protocol
- historical May 13 root markdown included `BROKEN_PREFABS.md`; it has since moved to `Reports/2026-05-13_BROKEN_PREFABS_STATIC_SNAPSHOT.md` and remains a generated prefab-audit snapshot, not documentation authority
- latest May 6 domain purge reports remain scoped source/pattern/diff evidence only; they explicitly leave full build blocked by missing `Assets/_Project/Scripts/SavePredictivePagingMath.cs` and Unity MCP validation unavailable without an active session
- latest May 5 Omega bounded Core build evidence is `CodexArtifacts/dotnet-h8core-omega-autonomy-doc-continuation-build.log`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, and `0` first-party `Assets/_Project` matches
- last May 5 Omega warning-bearing baseline is `CodexArtifacts/dotnet-h8core-omega-autonomy-current-build5.log`: `Build succeeded`, `48 Warning(s)`, `0 Error(s)`; warning owners are dependency/vendor surfaces only: Unity URP PackageCache, GPUInstancer, Den.Tools/MapMagic, Crest, WaveHarmonic.Crest, Unity ShaderGraph/Core Editor PackageCache
- first-party warning check against `CodexArtifacts/dotnet-h8core-omega-autonomy-current-build5.log` also returned `0` `Assets/_Project/Scripts` matches; older artifact logs may contain first-party warnings and must not be treated as current build output without rerunning the matching build
- May 5 Omega smoke evidence drift: `CodexArtifacts/unity-omega-smoke-2026-05-05-doc-continuation.log` is absent in the current filesystem, and current `Library/OmegaAutonomySmokeTester.json` reads `FAIL` on `nativeSentinelBalance` (`allocationDelta=2`, `trackedByteDelta=2560`). Older saved PASS/OMEGA artifacts remain scoped historical evidence only, not current Play Mode/profiler/GCMonitor/player-build proof.
- Active documentation manifest JSON boundary: the May 6, May 7, May 9, and May 11 `Docs/Reports/*ACTIVE_DOCUMENTATION_MANIFEST.json` files are historical generated snapshots. They must not be used as current counts, current authority lists, or current compile/runtime proof.
- latest May 13 R16/R18/R22 tools/PDA/first-hour interface static audits report `12` tool ItemData assets, `12` held prefabs, `12` world prefabs, all tool ItemData worldPrefab refs non-null, and a real tool/scan/interaction stack. R16 found first-hour contamination from `Player.prefab` `ToolLoadoutProvisioner` startup grants and root `Data_Copper`; R18 disabled the startup flags, release-gated provisioning, and switched starter copper to cataloged raw `Data_Copper`; R22 made `PlayerPDA.Open()` fail closed without a configured panel/tab shell and added a `ContentSanityValidator` tripwire. Treat tools as real architecture, but visible first-hour PDA shell remains `PENDING VERIFICATION` because `DiegeticPDAController` placement was not proven in `_Project` scenes/prefabs.
- latest May 13 R19 resource pickup data hardening moved `ResourceNodeTemplate_CopperVein` and three barter offers to cataloged raw copper, and wired existing matching pickup prefabs into six raw resource ItemData assets.
- latest May 13 R20/R21 resource route hardening extended `ContentSanityValidator` for resource-node yield catalog/worldPrefab/pickup-contract checks, added the remaining harvest raw resources to `ItemCatalog`, assigned existing pickup shells to the remaining harvest ItemData, changed barter authoring to raw copper, and added a small direct `ItemData.worldPrefab` fallback in `ItemCatalog` for missing/failed Addressables entries. Current static resource-node primary-harvest gaps are `0 / 27` missing `worldPrefab` and `0 / 27` non-catalog; no Unity route proof was run.
- latest May 13 R23 item identity hardening extended `ContentSanityValidator` for duplicate `ItemData.PersistentId`, null/duplicate `ItemCatalog` entries, missing runtime descriptors, and catalog lookup ambiguity. Static YAML scan still finds the legacy root/raw `Data_Copper` duplicate; it is now an editor-validator failure candidate, not a silent manual note. The validator was not run in Unity.
- latest May 13 R27 recipe route hardening extended `ContentSanityValidator` for `RecipeData` runtime hash uniqueness, result/ingredient catalog descriptors, positive quantities, explicit fabrication groups, and `QuestData.OnCraftCompleted` recipe-output routes. Static scan found `41` recipe assets and `Recipe_Scanner.asset` outputs `Item_Tool_Scanner`, but no Unity fabricator/craft/quest/PDA/save-load route proof was run.
- latest May 13 R28 scan-gate hardening extended `ContentSanityValidator` with `RecipeScanGateWarnings`: `scan.resource_node` has an obvious generic source, while `scan.expedition_contact`, `scan.resource_cache`, and `scan.structure_relay` are currently recipe/editor-authoring visible without static prefab/scene/data unlock proof. The validator was not run in Unity.
- latest May 13 R17 rendering/visor/shader audit reports active custom renderer stacks on Low/Mobile and PC tiers, `21` visor `ScriptableRendererFeature` files implementing `RecordRenderGraph`, `16` visor feature files still using `AddUnsafePass`, disabled GPU Resident Drawer/GPU occlusion in scanned URP assets, and `136` shader-like `_Project` files. Treat renderer code as real and fake-first in important places, but do not claim MX350 readiness, GPU occlusion savings, RenderGraph optimality, or visual quality without Frame Debugger/Profiler/Memory Profiler/player-route evidence.
- historical May 4 warning-cleanup local Core build `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` returned `0 Warning(s)` and `0 Error(s)`; its Unity console readback after clear/script refresh returned `0` error/warning entries and is historical after later workspace churn
- historical May 4 post-repair local Core build `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal` returned `0 Warning(s)` and `0 Error(s)`
- latest May 14 DOC_AUDIT R43 external root-project recheck reports single-project no-restore CLI builds at `0 Warning(s)` / `0 Error(s)` and `LASTEXITCODE=0` for `Hecton8.Core.csproj`, `Hecton8.Editor.csproj`, `Hecton8.PlayModeTests.csproj`, `Hecton8.World.Contracts.csproj`, `Hecton8.World.Dots.csproj`, `Hecton8.Bootstrap.Contracts.csproj`, `Hecton8.Input.Generated.csproj`, and `Hecton8.Input.csproj`; fresh first no-restore attempts can fail on missing `Temp\obj` restore assets, missing referenced `Temp\bin\Debug` DLLs, or shared `Temp\obj` file locks until restore/build and build-server cleanup are rerun. Full restore graphs still show vendor/package warnings. Unity MCP Console still fails at `127.0.0.1:8088/mcp`, so this is not Play Mode, profiler, GCMonitor, player-build, scene-wiring, or visual-quality proof
- fresh May 4 post-repair/current recheck foundation guard scan exited `0`; latest generated guard report timestamp is `2026-05-04 23:33:55`; current source gate inventory is `.Run(` `0`, hot-path `.Run(` review `0`, `.Complete(` text hits `5`, guarded dispatcher completion sites `1`, `UnsafeUtility.MemCpy outside guard` `0`, unauthorized Unity loop methods `0`, and runtime Find API text hits `8`
- historical May 4 active `Docs/**/*.md` inventory excluding archive/deprecated/obsolete was `194` after the sorting/header queue reports and root-log relocation; superseded by the May 7 active markdown count `230`
- historical May 4 source snapshot was `1118` first-party `.cs` files under `Assets/_Project` and `1078` `.cs` files under `Assets/_Project/Scripts`; superseded by the May 7 source count `1232` / `1191`
- earlier May 4 documentation-sweep MCP readback after retry: active scene `01_MAIN_MENU`, editor in Play Mode transition, console errors `0`, console warnings `18`, render textures `32`, render texture bytes `68215964`; latest current MCP recheck reports active scene `00_BOOTSTRAP`, Play Mode off, compiling false, ready for tools, console error/warning entries `0`, and is not Play Mode proof
- historical May 3 Unity batchmode evidence exists in `Temp/CodexArtifacts/unity-batch-2026-05-03-foundation-hardening-after-watchdogs.log`: batchmode exited successfully, Tundra build succeeded, Mono reloaded, and the strict compiler-failure scan found `0` matches
- historical May 3 full local Core build `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false` returned `0 Error(s)` and `0 Warning(s)` after stale generated Entities package references were pruned through source-backed Editor/MSBuild guards
- historical May 3 foundation guard scan reported synchronous job `.Run(` sites `0` as a hard gate, hot-path synchronous job `.Run(` review sites `0`, blind registry flag drift `0`, origin-shift listener blind flag drift `0`, direct `InputManager.Instance` sites `0`, release-reachable direct/one-hop hot-path `Debug.Log` sites `0`, broad physics masks `0`, and runtime Find API hits outside Editor folders `0`; the current post-repair May 4 guard scan exits `0`
- historical May 3 optional DOTS and Editor project checks returned `0 Error(s)` and `0 Warning(s)` for `Hecton8.World.Dots.csproj` and `Hecton8.Editor.csproj`; latest May 4 DOTS build now has `0 Warning(s)` / `0 Error(s)`
- historical May 3 scoped PlayMode test compile returned `0 Error(s)` and `0 Warning(s)` after `Hecton8.PlayModeTests.asmdef`/MSBuild project-reference wiring to `Hecton8.Core`, generated `Temp\bin\Debug` missing-reference pruning, and stale `LogAssertion` calls were replaced with Unity Test Framework `LogAssert`; May 4 PlayModeTests restore build has `0 Warning(s)` / `0 Error(s)`
- Unity-generated command-line projects share `Temp\obj`; build evidence must be collected serially, because parallel local `dotnet build` runs can create false `CS2012` file-lock failures
- earlier post-anchor-state full local Core build returned `0 Error(s)` and `1 Warning(s)` from `MSB3026` file-copy retry on `Temp\obj\Hecton8.Core\Hecton8.Core.dll`; latest full Core, Editor, and warning-gate reruns did not reproduce it
- earlier full verbose ProjectReferences builds reported third-party/package warnings from URP, GPUInstancer, Crest, Den.Tools, WaveHarmonic.Crest, and ShaderGraph; those were not patched under the third-party asset integrity rule
- historical May 3 Unity EditMode spatial-hash self-test evidence passed `3/3` in `Temp/CodexArtifacts/editmode-results-2026-05-03-spatialhash-selftest-after-collections.xml`
- bounded PlayMode gameplay, GCMonitor, profiler, memory retention, scene/prefab readback, and player-build proof were not captured in the May 4 documentation sweep; May 4 MCP readback is editor-only
- May 2 dotnet evidence remains historical compile evidence: restore build `0 Error(s)`, `136 Warning(s)`; latest post-restore `--no-restore` rerun `0 Error(s)`, `73 Warning(s)`

## Active Architecture

- `ARCHITECTURE/AUP_PRECISION_STANDARDS.md`
- `ARCHITECTURE/AUDIO_DSP_PIPELINE.md`
- `ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md`
- `ARCHITECTURE/DISPATCH_PIPELINE.md`
- `ARCHITECTURE/DRONE_FLEET_PROTOCOL.md`
- `ARCHITECTURE/ECS_DOTS_ADOPTION_PLAN.md`
- `ARCHITECTURE/EQUIPMENT_SOA_LAYOUT.md`
- `ARCHITECTURE/FLOW_FIELD_MATH.md`
- `ARCHITECTURE/HABITAT_LOGISTICS_GRAPH.md`
- `ARCHITECTURE/HECTON_PHI_STATIC_METRIC.md`
- `ARCHITECTURE/HEADLESS_ECOSYSTEM_SIMULATION.md`
- `ARCHITECTURE/KINEMATICS_AUP_INTEGRATION.md`
- `ARCHITECTURE/KINETIC_ENTANGLEMENT.md`
- `ARCHITECTURE/MIGRATORY_FLORA_SYSTEM.md`
- `ARCHITECTURE/ORGANIC_ENTROPY_MATH.md`
- `ARCHITECTURE/PROJECT_CONTENT_LEDGER.md`
- `ARCHITECTURE/QUEST_DAG_PROTOCOL.md`
- `ARCHITECTURE/REACTIVE_ECONOMY_SYSTEM.md`
- `ARCHITECTURE/SAVE_PAGING_PROTOCOL.md`
- `ARCHITECTURE/SAVE_V8_BINARY_SPEC.md`
- `ARCHITECTURE/SCANNER_DATA_MINING.md`
- `ARCHITECTURE/SEISMIC_GEOLOGY_SYSTEM.md`
- `ARCHITECTURE/SUBMARINE_OS_MANUAL.md`
- `ARCHITECTURE/SYSTEM_INTERCONNECT_MATRIX.md`
- `ARCHITECTURE/THIRD_PARTY_POISON.md`
- `ARCHITECTURE/TRAUMA_GLITCH_SYSTEM.md`
- `ARCHITECTURE/URP_SCREENSHOT_PIPELINE.md`
- `ARCHITECTURE/ZERO_GC_FABRICATION.md`
- `ARCHITECTURE/ZERO_GC_UI_PIPELINE.md`

## Procedural World / Content

- `Flora_Pipeline/README.md`
- `PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`
- `ARCHITECTURE/SEISMIC_GEOLOGY_SYSTEM.md`
- `QUALITY_GATES.md`

## Reference Bundles

- `Flora_Pipeline/README.md`
- `Scatter_Runtime/README.md`
- `AI_Fauna/README.md`
- `Legacy_World_Reference/README.md`
- `Legacy_Backlog/README.md`

Canonical-path rule:

- use the bundle paths above as navigation anchors and obey each bundle's current-state boundary notes
- old flat redirect stubs for Flora/Scatter were moved to `DEPRECATED/Root_Redirect_Stubs_2026-05-01/`
- current canonical entry points are `Flora_Pipeline/README.md` and `Scatter_Runtime/README.md`

## Root-Level Active Anchors

- `../MASTER_RELEASE_WORK_PLAN.md`
- `../BUILD_PLAYTEST_ISSUES.md`

## Archived Material

- `_Archive/README.md`
- `_Archive/2026-04-16_Workspace_Cleanup/README.md`
- `_Archive/2026-04-16_Workspace_Cleanup/MANIFEST.md`
- `_Archive/2026-04-29_Two_Day_Stale_Active_Docs/README.md`
- `DEPRECATED/README.md`
- `DEPRECATED/2026-04-29_Audit_Bundles/README.md`
- `DEPRECATED/External_And_Log_Bundles/README.md`
- `DEPRECATED/External_And_Log_Bundles/Root_Evidence_2026-05-15/README.md`
- `DEPRECATED/Root_Compatibility_Mirrors_2026-05-15/README.md`
- `DEPRECATED/External_And_Log_Bundles/Root_Logs_2026-05-04/README.md`
- `DEPRECATED/External_And_Log_Bundles/Root_Logs_2026-05-05/README.md`
- `DEPRECATED/Root_Redirect_Stubs_2026-05-01/README.md`
- `DEPRECATED/Encoding_Damaged_2026-05-01/README.md`
- `Reports/DEPRECATED/2026-04-29_Static_Audit_Snapshots/README.md`

Archive note:

- stale root-level dated execution docs older than two days were moved to `_Archive/2026-04-29_Two_Day_Stale_Active_Docs/`
- External idea/log bundles, including the old Deepseek/Gemini/Sargassum prompt folders, raw Codex logs, and former repository-root Unity/Codex logs, now live under `Docs/DEPRECATED/External_And_Log_Bundles/`
- May 15 root evidence/log/artifact spill now lives under `Docs/DEPRECATED/External_And_Log_Bundles/Root_Evidence_2026-05-15/`
- Former root compatibility mirrors now live under `Docs/DEPRECATED/Root_Compatibility_Mirrors_2026-05-15/`
- flat Flora/Scatter redirect stubs no longer live in root `Docs`
- encoding-damaged geology production notes no longer live in root `Docs`; use `PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md` and `ARCHITECTURE/SEISMIC_GEOLOGY_SYSTEM.md`
