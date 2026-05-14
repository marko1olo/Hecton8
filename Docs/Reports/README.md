# Reports

Date: 2026-05-14
Status: PENDING VERIFICATION (DOC_AUDIT R29 UNITY BATCH COMPILE + R30/R31/R32/R33/R36/R37/R38 PERSISTENCE STATIC HARDENING + R39 GENERATED-PROJECT ASMDEF TRIPWIRE + R40 SOURCE-BACKED CLI COMPILE BRIDGE + R41 ROOT HECTON8 CLI COMPILE SWEEP + R42 ACTIVE REFERENCE DOC PROPAGATION + R34 PLAYER HOT-PATH CACHE + R35 HLOD PDA UPLOAD GATE / ROOT HECTON8 CLI BUILDS CLEAN THROUGH BRIDGE / RUNTIME PROOF ABSENT)

Purpose: canonical drop zone for reports, audits, counters, and validation writeups.

Dated reports are evidence snapshots. Durable project policy belongs in `AGENTS.md`, `.agents-skills/README.md`, task-relevant `.agents-skills/*`, and stable docs such as `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`, `Docs/SYSTEMS_CONTRACTS.md`, `Docs/QUALITY_GATES.md`, `Docs/ARCHITECTURE/README.md`, and `Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md`.

## 2026-05-13 Evidence Override

`2026-05-13_DOC_AUDIT_XRAY.md` is the current documentation-reality override.
It found the May 11 build artifacts cited by stable docs absent from the current filesystem:

- `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.summary.txt`
- `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.log`

Therefore May 11 compile-success claims remain dated report text. DOC_AUDIT R29 captured a replacement Unity `6000.4.1f1` batchmode import/script-compilation artifact at `Library/Codex_DOC_AUDIT_UnityBatchCompile.log`; it is compile/import evidence only, not PlayMode/runtime proof.

R2/R3/R4/R5/R6/R7/R8/R9/R10/R11/R12/R13/R14/R15/R16/R17/R18/R19/R20/R21/R22/R23/R24/R25/R26/R27/R28/R29/R30/R31/R32/R33/R34/R35/R36/R37/R38 refresh also found active non-report docs still calling that missing file `Current compile-only evidence`, stale interface-count claims, Archivarius path drift, an inflated active-doc counter that included `Docs/Archive`, package-surface wording that confused clean UPM manifest state with physical legacy asset contamination, primary `AGENTS` authority drift around Low URP mapping / Easy Save 3 wording, world/scatter runtime-wiring proof gaps, root mirror/atlas scope confusion, active root anchors still promoting the missing May 11 artifact as current evidence, SpaceEngine research proof/path drift, Omega smoke artifact drift, active documentation manifest JSON files whose count/build-state surfaces needed historical snapshot boundaries, a gameplay resource-loop break candidate where most resource-node harvest items lacked world prefabs while the drop registry rejects null prefabs, an AI/Fauna proof gap where real data coverage was being treated too close to runtime spawn readiness, a Tools/PDA first-hour proof gap where R16 found dev provisioning contamination, R18 statically hardened it, R22 added a fail-closed PDA open guard, R24 added active tool metadata route validation, and R25 added player-prefab dev-provisioner startup flag validation while runtime route/PDA shell proof remains absent, a renderer/visor/shader proof gap where substantial RenderGraph-era source and active low-tier post features still lack Frame Debugger/Profiler/Memory Profiler proof, R19/R20 resource-pickup data and validator hardening, R21 static closure of resource-node harvest catalog/worldPrefab gaps to `0 / 27` missing worldPrefab and `0 / 27` non-catalog, R23 item/catalog identity validator hardening for duplicate PersistentIds and catalog ambiguity, R24 tool metadata orphan / held tool route validation, R25 hidden startup-grant regression validation, R26 quest item/prerequisite route validation, R27 recipe/result/ingredient/catalog plus craft-quest recipe-output validation, R28 scan-gate warnings for scan-locked recipes without known generic/prefab unlock routes, R29 async world-pager compile/bootstrap hardening for locked `world_data.h8bin`, R30 correction of false chunk-local voxel pager claims, R31 lazy pager boot trim / regression guard, R32 lazy allocation for large save buffers, R33 pager-fault staging guard / load allocation failure envelope, R34 player-movement ladder snap hot-path cache, R35 HLOD PDA upload version gating, R36 recurrent world-pager voxel snapshot regression removal, R37 local Bee/Roslyn Core.Memory/Core compile reconciliation plus joinable pager worker guard, and R38 pager worker pending-counter fault accounting plus WFC outpost persistence contract implementation. Current static counters after concurrent churn: `918` markdown files under `Docs`, `283` active markdown, `203` active non-`Docs/Reports` markdown, `80` active direct report markdown, `10` docs JSON, `1411` project C# files, `1365` script C# files, `869871` project source lines, `852315` script source lines, `215` interface declaration hits, `51` direct public interfaces in `GlobalRegistryContracts.cs`, and `24` first-party asmdefs.

`Docs/Reports/*ACTIVE_DOCUMENTATION_MANIFEST.json` files are historical generated snapshots. DOC_AUDIT R13 added top-level boundaries to the May 6, May 7, May 9, and May 11 manifests; their counts, `sourceCounts`, `currentAuthority*`, `buildState`, and `entries` are not current authority after later workspace churn.

DOC_AUDIT R14/R19/R20/R21/R23/R26/R27/R28 adds a gameplay-economy boundary: source/data proves a real item->inventory->crafting->fabricator spine. R14 found first-hour resource-loop readiness pending because `23 / 27` resource-node harvest items lacked `worldPrefab`, template-driven `ResourceNode` drops depend on a registry path that rejects null prefabs, and duplicate `Data_Copper` assets split node/barter data from catalog/recipe data. R19 reduced the primary-harvest `worldPrefab` gap to `16 / 27`, R20 added validator tripwires, R21 reduced the current resource-node primary-harvest gaps to `0 / 27` missing `worldPrefab` and `0 / 27` non-catalog, R23 extended `ContentSanityValidator` to catch duplicate `ItemData.PersistentId` / catalog ambiguity, R26 added quest item/prerequisite route validation, R27 added recipe/result/ingredient/catalog validation plus `OnCraftCompleted` recipe-output validation, and R28 added warning validation for scan-locked recipes without known generic/prefab unlock routes. Runtime pickup/craft/quest proof remains pending.

DOC_AUDIT R15 adds an AI/Fauna boundary: static fauna data coverage is real (`22` recursive creature archetype assets, `22` fauna data templates, `108` fauna biome datasets, `432` non-null biome spawn prefab entries, `13` fauna family profiles, `6` generated proxy prefabs), but current static scans did not prove serialized production-scene `FaunaDirector`/`WorldFaunaSpawnRegistry` wiring. `GameBootstrapper` can register the ready headless `DemiurgeFaunaSimulationService.Shared` fallback when no real director registers `IFaunaSim`, and the visible `.codex-artifacts/fauna-omega-smoke-2026-05-05.log` ends with return code `1`.

DOC_AUDIT R16/R18/R22/R24/R25 adds a Tools/PDA/first-hour boundary: tool data and interaction code are real (`12` tool ItemData assets, `12` held prefabs, `12` world prefabs, all tool ItemData `worldPrefab` refs non-null). R16 found enabled `ToolLoadoutProvisioner` startup grants; R18 disabled/gated that dev provisioning and switched starter copper to cataloged raw `Data_Copper`; R22 made `PlayerPDA.Open()` fail closed without a configured panel/tab shell and added a PDA validator tripwire; R24 added tool metadata route gates for held prefab, ItemData, catalog descriptor, and world prefab; R25 added a player-prefab dev-provisioner startup flag gate. `LogicSpanner` is still orphan metadata/source with no item/prefab/catalog/recipe route, and visible PDA shell wiring remains pending because `DiegeticPDAController` placement was not proven by static scene/prefab scan.

DOC_AUDIT R17 adds a Rendering/Visor/Shader boundary: active renderer tiers carry a real custom post/noir/SSDO/shaft stack and `21` visor renderer-feature files implement `RecordRenderGraph`, but `16` visor feature files still use `AddUnsafePass`, GPU Resident Drawer/GPU occlusion are disabled in scanned URP tier assets, and no Frame Debugger, RenderGraph Viewer, Profiler, Memory Profiler, RenderDoc, player build, or visual capture proves MX350 readiness or pass-graph optimality.

DOC_AUDIT R29/R30/R31/R32/R33 adds a compile/persistence boundary: generated `.csproj` builds remain stale around split asmdefs, but Unity `6000.4.1f1` batchmode import/script compilation completed in `Library/Codex_DOC_AUDIT_UnityBatchCompile.log` with no `error CS`, bootstrap dependency exception, or `BIOS ERROR` entries. Later read-only Unity MCP Console readback first returned `0` errors and `7` non-C# warnings, then final readback returned `0` log entries. `SaveManager` / `H8BinaryWorldPager` now fail-close locked async page persistence instead of throwing through bootstrap; R30/R31/R32/R33 harden single-writer page-file semantics, bounded pager shutdown, read-slot cleanup, sparse/collided page fallback, lazy pager file open outside `InitializeNativeBuffers()`, first-use allocation for large save buffers, pager-fault staging guard, and remove false chunk-local voxel snapshot/prefetch behavior. This is not PlayMode, save/load, corrupted-sector recovery, profiler, GCMonitor, Memory Profiler, or player-build proof.

DOC_AUDIT R35 adds a world-streaming/PDA bandwidth boundary: `IStreamingBackpressureService.ActiveImpostorVersion` and the PDA-side uploaded version/count cache prevent unchanged HLOD POI data from being resent to the fixed `16 x float4` PDA buffer every map build. Fade progress uses a separate point/read-model version, not the renderer matrix version. This is static source evidence only; no PDA map route, Frame Debugger, profiler, GCMonitor, PlayMode, or player-build proof was run.

DOC_AUDIT R36 re-applies the async pager truth boundary after concurrent churn: `SaveManager.EnqueueChunkDehydrationPayloads()` again no longer captures a global `VoxelDeltaProcessor` snapshot or writes `VoxelDeltaRle` as chunk-local sidecar data. Current scoped grep finds no `FileShare.ReadWrite`, no `worldPagerVoxelDeltaSnapshot`, and no direct `RequestAsyncPagerRead(chunkId)` in the pager integration files. Runtime save/load proof remains absent.

DOC_AUDIT R37 rechecks that boundary after more concurrent churn: `H8BinaryWorldPager` again uses a joinable named `Thread`/`RunWorkerLoop()` instead of `async void`/`Awaitable.BackgroundThreadAsync`; `Hecton8.Core.Memory` and `Hecton8.Core` local Unity Bee/Roslyn temp-output probes both return exit code `0`. Unity MCP Console is unavailable, so this is not live Unity import or runtime proof.

DOC_AUDIT R38 hardens the same persistence boundary but demotes the old R37 full-Core success as stale under current churn: `H8BinaryWorldPager` now decrements dequeued write/read pending counters in `finally` on unexpected command faults and fails closed with black-box dump; current `SaveManager` now implements WFC outpost state MacroDB bitmask persist/restore through DataVault `WfcOutpostGrid`, `PackWfcOutpostMutableStateJob`, `SaveBinaryPayloadCodec`, and `IMacroDatabaseService`. Local probes for current `Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, temporary audio-virtualization refs, world contracts, AI cognition, and animation IK exit `0`, but full `Hecton8.Core` is blocked by unrelated active errors in audio/scanner/submarine-buffer/arena/fluid/UI/fauna files. No runtime proof is claimed.

DOC_AUDIT R39/R40/R41/R42 adds a generated-project/asmdef drift boundary, a temporary source-backed CLI compile bridge, a serial root-project compile sweep, and active reference-doc propagation of that boundary. R39 found `Assets/_Project/Scripts/Hecton8.Core.asmdef` references `23` first-party assemblies absent from current generated `Hecton8.Core.csproj`, and `HectonComplianceValidator` now reports this as `CSPROJ001`. R40 confirmed a non-destructive Unity batchmode project-refresh attempt did not regenerate the stale root `.csproj` files, then repaired external compile evidence through `Directory.Build.targets` instead of editing generated projects or adding fake stubs. R41 serially rechecked every root `Hecton8*.csproj`; final `--no-restore -m:1 /nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -v:minimal -clp:Summary` compile surface is `0 Warning(s)` / `0 Error(s)` for `Hecton8.Core`, `Hecton8.Editor`, `Hecton8.PlayModeTests`, `Hecton8.World.Contracts`, `Hecton8.World.Dots`, `Hecton8.Bootstrap.Contracts`, `Hecton8.Input.Generated`, and `Hecton8.Input`. Missing `Temp\obj\...\project.assets.json` can still make first no-restore attempts fail until restore is rerun; full restore graphs emit vendor/package warnings from URP/GPUInstancer/Crest/ShaderGraph and MapMagic/Den.Tools, but the final first-party no-restore root sweep is clean. R42 propagated this May 14/R41 boundary into `38` active reference docs that still carried the older May 13 missing-artifact-only override. This remains CLI compile evidence only; Unity Console, Play Mode, profiler, GCMonitor, player build, and scene wiring proof are absent.

## Naming

- single-file report: `YYYY-MM-DD_TaskName.md`
- multi-file report bundle: `YYYY-MM-DD_TaskName/`

## Rule

- do not create new report files in repo root
- do not drop one-shot reports loose in `Docs/`
- when a report is older than `5` days and no longer drives current work, archive it

## Current Evidence Snapshots

- `2026-05-13_DOC_AUDIT_XRAY.md`
- `2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`
- `2026-05-08_DOCUMENTATION_CONTINUATION_SYNC.md`
- `2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`
- `2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`
- `2026-05-08_ACTIVE_DOCUMENTATION_MANIFEST.json`
- `2026-05-07_OMEGA_FINAL_INQUISITION.md`
- `2026-05-07_MAIN_DOCUMENTATION_CURRENT_STATE_REFRESH.md`
- `2026-05-07_FINAL_INQUISITION_NATIVE_SCANNER.md`
- `2026-05-07_OMEGA_HEAVENS_SHADER_AVALANCHE.md`
- `2026-05-07_AUTONOMOUS_ERROR_WARNING_CLEANUP.md`
- `2026-05-07_LIVE_CHURN_CONTINUATION_SYNC.md`
- `2026-05-07_PROJECT_ATLAS_SYNCHRONIZATION_PASS.md`
- `2026-05-07_BRUTAL_SYNCHRONIZATION_REPORT.md`
- `2026-05-07_ACTIVE_DOCUMENTATION_MANIFEST.json`
- `2026-05-07_NATIVE_COLLECTION_LEAK_AUDIT.md`
- `2026-05-07_NATIVE_COLLECTION_LIFECYCLE_AUDIT.md`
- `2026-05-06_DOCUMENTATION_SYNCHRONIZATION_PASS.md`
- `2026-05-06_ACTIVE_DOCUMENTATION_MANIFEST.json`
- `2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`
- `2026-05-05_OMEGA_AUTONOMY_FORENSIC_HARDENING.md`
- `2026-05-04_DOCUMENTATION_HEADER_ARCHIVE_QUEUE.md`
- `2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`
- `2026-05-04_WARNING_CLEANUP.md`
- `2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`
- `2026-05-04_CELESTIAL_ORBITAL_PROTOCOL_METEOR_REPORT.md`
- `2026-05-04_CELESTIAL_ORBITAL_PROTOCOL_REPORT.md`
- `2026-05-04_CELESTIAL_ENVIRONMENT_ORBITAL_SYNC_REPORT.md`
- `2026-05-04_HYDRAULIC_EROSION_ENGINE_SURGERY_LOG.md`
- `TERRAIN_AND_BIOME_REALITY_MAP.md`
- `2026-05-03_FOUNDATION_HARDENING_CONTINUATION.md`
- `2026-05-03_FOUNDATION_GUARD_SIGNAL_CLEANUP.md`
- `2026-05-03_OPTIMIZATION_REGISTRY_OWNERSHIP.md`
- `2026-05-03_SETTINGS_PERSISTENCE_REGISTRY_REBIND.md`
- `2026-05-03_HABITAT_GRAPH_ANCHOR_STATE_HARDENING.md`
- `2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md`
- `2026-05-01_CURRENT_PROJECT_STATE.md`
- `2026-05-01_OBJECTIVE_PROJECT_CONCLUSION.md`
- `2026-05-01_EDITOR_LOG_CONSOLE_STABILIZATION.md`
- `2026-05-01_EVENT_BUS_SPATIAL_HASH_COMPILE_DELTA.md`
- `2026-05-01_COMPILE_STABILIZATION_CONTINUATION.md`
- `2026-05-01_EVENT_CASCADE_RECHECK.md`
- `2026-05-01_ZERO_GC_JOBS_CONTINUATION_DELTA.md`
- `TOTAL_CODEBASE_AUDIT_V2.md`
- `OMEGA_CORE_ENFORCEMENT_2026-05-01.md`
- `AWAITABLE_MEMORY_COMPACTION_SURGERY_LOG.md`
- `DOOMSDAY_FLAW_REPORT.md`

`2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md` is historical May 11 documentation/data evidence; `2026-05-13_DOC_AUDIT_XRAY.md` is the current static override for counters, missing-artifact evidence, gameplay resource-loop evidence, AI/Fauna wiring evidence, and tools/PDA/first-hour interface evidence.
It claimed R186 supersession with `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.summary.txt`, but the May 13 DOC_AUDIT filesystem check did not find that summary or raw log. Treat the May 11 compile-success line as stale report text until artifacts are restored or replaced. The May 11 report recorded counters: `Docs/**/*.md` `449`, active markdown `236`, direct `Docs/Reports/*.md` `70`, active JSON `15`, `Assets/_Project/**/*.cs` `1306`, `Assets/_Project/Scripts/**/*.cs` `1262`, project physical lines `770577`, script physical lines `753858`, and `GlobalRegistryContracts.cs` direct public interfaces `40`. Those counters are historical and source/doc only, not Unity Console, Play Mode, profiler, GCMonitor, player-build, frame-time, memory, import, scene-wiring, or visual-quality proof.
The same pass records an official Unity release-page check: Unity `6000.4.6f1` exists with release date `2026-05-05`, newer than the local project pin `6000.4.1f1`. This is version drift only, not upgrade approval.

`2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json` is the latest historical machine-readable active documentation manifest snapshot. Its counters, authority list, build-state fields, and entries are not current authority after the May 13 DOC_AUDIT X-Ray.
It lists active markdown files with parsed `Date`, `Status`, title, byte-size, and `requiresRuntimeProof` fields as May 11 snapshot data. It supersedes the May 8 manifest structure only as historical manifest format, not the May 13 missing-artifact/status override.

DOC_AUDIT R16/R18/R22/R24/R25 boundary: tool/scan/interaction architecture is source/data real, with `12` tool ItemData assets, `12` held prefabs, `12` world prefabs, all tool `worldPrefab` refs non-null, `PlayerToolManager`, `ScannerTool`, `ScanEvents`, `ScanLogSystem`, and quest/interaction event paths present. First-hour readiness is still not proven: R18 removed the hidden startup grants and root-copper provisioner reference statically, R22 made headless `PlayerPDA.Open()` fail closed, R24 added editor validation for active tool metadata -> held prefab -> ItemData/catalog/worldPrefab routes, and R25 added a startup-grant regression gate, but `DiegeticPDAController` placement was not proven by class/GUID scan. This is static evidence only; no Unity, Play Mode, profiler, GCMonitor, player build, or scene import validation was run.

DOC_AUDIT R19/R21 boundary: resource pickup data is materially cleaner. `ResourceNodeTemplate_CopperVein`, `Offer_Illumination`, `Offer_RelayStarter`, and `Offer_RepairLoop` now use cataloged raw `Data_Copper`; existing pickup shells are wired into all current resource-node primary harvest ItemData. Static recount now shows `0 / 27` primary resource-node harvest refs with null `worldPrefab` and `0 / 27` non-catalog refs. No Unity route proof was run.

DOC_AUDIT R20/R21 boundary: `ContentSanityValidator` now validates resource-node `harvestYield` and `rarityDrops` item refs against active `ItemCatalog` membership, non-null `ItemData.worldPrefab`, and pickup prefab contract, and reports `ResourceNodeYieldMissingWorldPrefab` / `ResourceNodeYieldNotCataloged` / `ResourceNodeYieldInvalidWorldPrefabContract` counters. This is static source evidence only; the Unity validator was not executed.

`2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md` is the current `.agents-skills` visual-fake doctrine boundary.
It records that the registry was not fully current for the current production direction: several physical/visual realism mandates still pushed simulate-first, Unity Joint, broad flow/lighting, or default HRTF paths. It adds `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt` and override blocks to the affected mandates. This is documentation-only. It does not prove Unity import, Play Mode, profiler, GCMonitor, player build, frame time, memory retention, or visual quality.

`2026-05-08_DOCUMENTATION_CONTINUATION_SYNC.md` is the previous documentation synchronization counter boundary with a 2026-05-09 R186 addendum.
It superseded conflicting May 7/May 8 source-count and build-blocker statements at the R186 boundary. It recorded then-current counters: `Assets/_Project/**/*.cs` `1292`, `Assets/_Project/Scripts/**/*.cs` `1248`, project physical lines `759122`, script physical lines `742892`, and `GlobalRegistryContracts.cs` direct public interfaces `40`. The then-latest full Core dependency build was `CodexArtifacts/2026-05-09_R186_CORE_FULLGRAPH_SERIAL_NORESTORE_BUILD.log`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, `DOTNET_EXIT_CODE=0`, `CS_WRITES_AFTER_START=0`, `CS_WRITES_AFTER_END=0`. This is historical local `dotnet` compile evidence only.

`2026-05-08_ACTIVE_DOCUMENTATION_MANIFEST.json` is the previous historical machine-readable active documentation manifest snapshot.
It remains historical R186 snapshot evidence only where the May 11 historical manifest does not cover a field, and it is not current compile/runtime proof.

`2026-05-07_MAIN_DOCUMENTATION_CURRENT_STATE_REFRESH.md` is the previous main documentation synchronization boundary.
It is historical for counters and compile status. Use the May 11 continuation, May 11 manifest, and May 8/R186 continuation before citing any May 7 numeric or build-state claim.

`2026-05-07_FINAL_INQUISITION_NATIVE_SCANNER.md` is the latest final-inquisition fallback compile/MCP boundary.
It records editor-only scanner changes, its then-current `PENDING FINAL UNITY PROOF` status, repeated MCP console failure, and fallback Core build evidence. Read the main documentation refresh first for the latest documentation counters and latest successful console retry.

`2026-05-07_LIVE_CHURN_CONTINUATION_SYNC.md` is the previous same-day live-churn boundary.
It remains useful for churn methodology, but its numeric counters are superseded by `2026-05-07_MAIN_DOCUMENTATION_CURRENT_STATE_REFRESH.md` where they conflict.

`2026-05-07_AUTONOMOUS_ERROR_WARNING_CLEANUP.md` is the latest autonomous compile-repair and documentation addendum.
It records the current first-party compile cleanup around bootstrap warmup API drift, procedural audio duplicate helper drift, crash telemetry stale monitor references, missing repair/XR source inclusion, wreck decal value types, obsolete Unity 6 API warnings, and `PhysicalHandController` native-buffer sentinel/deferred-disposal repair. Evidence is isolated `Hecton8.Core` and `Hecton8.Editor` builds at `0 Warning(s)` / `0 Error(s)`, full Core dependency graph `0 Error(s)` with vendor/dependency warnings, full Editor graph `0 Warning(s)` / `0 Error(s)`, and Unity-style `Assembly-CSharp` compile `0 Error(s)` with one vendor/editor warning in `Crest.Helpers.Editor.csproj`. That report left MCP console proof pending because Unity MCP returned `ping not answered`; the May 8 build-master sweep keeps MCP proof blocked until a fresh console read is captured.

`2026-05-07_PROJECT_ATLAS_SYNCHRONIZATION_PASS.md` is the previous Project Atlas/source-count synchronization report.
It remains useful for Project Atlas/source-count methodology, but its numeric source counters are superseded by `2026-05-08_DOCUMENTATION_CONTINUATION_SYNC.md` when they conflict. It also records stale-symbol source proof for `itemGeneticsWords`, `MinimumDensity`, and `MaximumDensity`, and keeps runtime verification `PENDING`.

`2026-05-07_BRUTAL_SYNCHRONIZATION_REPORT.md` is the current static documentation integrity report.
It records the earlier May 7 static documentation integrity sweep. Its numeric counters are superseded by `2026-05-07_LIVE_CHURN_CONTINUATION_SYNC.md` when they conflict. It remains authority for hallucination-pattern scan results, deprecated stub state, cinematic-cheat ledger inclusion, five-artery Mega-Bus synchronization, zero-GC UI doctrine, native collection leak/lifecycle audit status, and the diff artifact path. It is not Play Mode, profiler, console-clean compile, current-source whole-project build, or runtime leak proof.

`2026-05-07_ACTIVE_DOCUMENTATION_MANIFEST.json` is a historical machine-readable active documentation manifest snapshot.
It lists `230` active markdown files with parsed `Date`, `Status`, title, byte-size, and `requiresRuntimeProof` fields as May 7 snapshot data. Its source count fields are historical only and are not current compile/runtime proof.

`2026-05-06_DOCUMENTATION_SYNCHRONIZATION_PASS.md` is the previous broad documentation synchronization report.
Read the May 8 synchronization report first for current counters. The May 6 report remains historical editor-state/source-documentation evidence.

`2026-05-06_ACTIVE_DOCUMENTATION_MANIFEST.json` is a previous historical machine-readable active documentation manifest snapshot.
It is superseded by later historical manifests and by the May 13 DOC_AUDIT X-Ray for current status/counter authority.

`2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md` is the latest documentation sorting and authority classification map.
It records root text handling, active `Docs/` bundle classes, first-read reports, historical/evidence-only reports, the original dirty-worktree boundary, the follow-up relocation of repository-root logs into a deprecated evidence bundle, and the 2026-05-05 documentation influx count boundary. Read the May 6 synchronization report first for current counters.

`2026-05-05_OMEGA_AUTONOMY_FORENSIC_HARDENING.md` is a bounded Omega-autonomy hardening evidence report.
It records the PowerGrid native-sentinel and job-barrier telemetry changes, BaseLogisticsNetwork route BFS `.Run()` removal, procedural audio overflow telemetry, the explicit-EventSystem `MainMenuInputRoutingGuard` smoke fix, repair telemetry code/dedupe proof, runtime-watchdog registry hijack smoke, `HectonWorldGenerator.ActiveRuntimeInstance` compile-blocker cleanup through `GlobalRegistry.WorldSeedProvider`, scoped no-`IJob.Run()`/`JobHandle.Complete()` scan, scoped native-memory registration scan, `Hecton8.Core` build evidence, and an older scoped `OmegaAutonomySmokeTester` JSON `PASS`. DOC_AUDIT R12 found current `Library/OmegaAutonomySmokeTester.json` reads `FAIL` on `nativeSentinelBalance` (`allocationDelta=2`, `trackedByteDelta=2560`), so the older smoke `PASS` is historical scoped evidence only. The latest serial Omega Core build in that report is `Build succeeded`, `0 Warning(s)`, `0 Error(s)`; the last warning-bearing Omega baseline had `48 Warning(s)` owned by dependency/vendor code and `0` first-party `Assets/_Project/Scripts` warning matches. Current authority status remains `PENDING VERIFICATION`; no Play Mode/profiler/GCMonitor/player-build proof is claimed.

`2026-05-04_DOCUMENTATION_HEADER_ARCHIVE_QUEUE.md` is the current structural cleanup queue.
It previously recorded `41` active markdown files still missing `Date:`; the May 6 synchronization pass closed that debt and then normalized the remaining `_Archive`, `Reports`, and `DEPRECATED` markdown provenance headers. Current full `Docs/**/*.md` header debt is `0`. It also notes that repository-root `.log` evidence artifacts have been moved to dated `Docs/DEPRECATED/External_And_Log_Bundles/Root_Logs_*` bundles and keeps archive/deprecated move candidates queued for a separate pass. It also records the editor-only documentation authority smoke guard, its three-pass stress runner, failed-audit telemetry hook, CI-facing `RunBatchAll` entrypoint, direct Roslyn compile evidence, and the current Unity licensing/project-lock blocker that prevents claiming batch smoke proof.

`2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md` is the latest broad documentation and local evidence addendum.
It records the current source/doc counts, fresh `Hecton8.Core` and `Hecton8.Editor` compile results `0 Warning(s)` / `0 Error(s)`, historical `Hecton8.World.Dots` restore build `1 Warning(s)` / `0 Error(s)`, `Hecton8.PlayModeTests` restore build `0 Warning(s)` / `0 Error(s)`, and the pre-repair foundation guard failure. Read `2026-05-04_WARNING_CLEANUP.md` and `2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md` next for the current warning-clean and guard-clean source/build addenda.

`2026-05-04_WARNING_CLEANUP.md` is the latest first-party warning cleanup addendum.
It records the current Core compiler warning cleanup in `HectonVoxelEngine`, `PlayerCriticalProceduralAudioRenderer`, `HectonCelestialEngine`, and `ModalWindow`, the `SettingsPanel` post-reload listener cache guard, the `DSPThreadSafetySmokeTester` smoke-test expectation update, the `MainMenuController.Update()` dispatcher-bypass removal, plus the `SystemDispatcher` late-frame load-shedding and slow-phase console warning reroute back to telemetry-only reporting. Latest evidence is local Core, Editor, DOTS, and PlayModeTests builds at `0 Warning(s)` / `0 Error(s)`, foundation guard exit `0`, audio DSP smoke-test `PASS`, and a post-clear/script-refresh Unity console readback with `0` error/warning entries. It is not Play Mode, profiler, GC, or player-build proof.

`2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md` is the latest foundation guard repair addendum.
It records the `CrashTelemetryBuffer` unsafe-copy guard repair, `MainMenuController.Update()` fallback removal, regenerated foundation guard scan exit `0`, raw `UnsafeUtility.MemCpy` outside guard `0`, unauthorized Unity loop methods `0`, fresh `Hecton8.Core` / `Hecton8.Editor` compiles `0 Warning(s)` / `0 Error(s)`, and Unity MCP editor readback with active scene `00_BOOTSTRAP`, console errors `0`, and warnings `0`. It is not Play Mode, GCMonitor, profiler, menu UX, save/load, or player-build proof.

`2026-05-01_CURRENT_PROJECT_STATE.md` is conceptual evidence, not the first authority entry point.
Stable entry starts at `AGENTS.md`, `.agents-skills/README.md`, `Docs/README.md`, `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`, `Docs/SYSTEMS_CONTRACTS.md`, `Docs/QUALITY_GATES.md`, and `Docs/ARCHITECTURE/README.md`. This report does not replace source files or runtime verification logs.

`2026-05-04_CELESTIAL_ORBITAL_PROTOCOL_METEOR_REPORT.md` is the latest celestial/meteor protocol report.
It records source/build/controlled-console evidence, but `CelestialSyncSmokeTester` did not execute because no scene object existed in the active scene. It is not PlayMode visual/audio/profiler proof.

`2026-05-04_CELESTIAL_ORBITAL_PROTOCOL_REPORT.md` and `2026-05-04_CELESTIAL_ENVIRONMENT_ORBITAL_SYNC_REPORT.md` are May 4 celestial source/build reports.
They are source-build evidence only and do not prove runtime predator migration, tidal cache motion, biolum response, meteor visuals, audio booms, or GC.

`2026-05-04_HYDRAULIC_EROSION_ENGINE_SURGERY_LOG.md` is the current hydraulic erosion implementation/surgery report.
It records added Burst/job/editor harness files and explicitly leaves Unity import/compile, MapMagic graph execution, PNG harness output, GCMonitor, and profiler capture as pending.

`TERRAIN_AND_BIOME_REALITY_MAP.md` is the canonical terrain/biome report.
The root `TERRAIN_AND_BIOME_REALITY_MAP.md` path is not active authority.

`2026-05-03_FOUNDATION_HARDENING_CONTINUATION.md` is the latest dated foundation-hardening implementation addendum.
Read `2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md` and `2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md` first for current guard/build/MCP truth; May 3 guard-clean statements are historical scan-time evidence.
It records removal of a forced PhysX-bake completion path in `HectonWorldGenerator`, async mesh-build watchdogs in `ProceduralWreckGenerator`, the reduced floating-origin watchdog, `UserOptionsPersistence` ownership via bootstrap/GlobalRegistry, the `BaseAirlock` dispatcher-registration retry plus safe-teleport and pre-cycle spawn-validation hardening, `BaseAirlockEvents` NativeQueue runtime lane, `SealedDoor` empty-dispatcher-registration removal and progress-cadence coalescing, `LoreDatabaseManager` save-registration ownership/edit-mode guard hardening, `HabitatGraphManager` traversal-scratch separation and native-disposal defaulting, bounded organic/drop durability drains, dispatcher-raycast sidecar tail clearing, tether visual `.Run()` removal, `PlayerInventory` inline-kernel `.Run()` removal, `CraftingSystem` inline-kernel `.Run()` removal, `QuestStateManager` direct-kernel `.Run()` removal, `SargassumMicroFaunaBoids` one-element prime `.Run()` removal, `FaunaSimulationEngine` hibernation catch-up `.Run()` removal, `FloraInteractionManager` scheduled cascade phase seed publication, fallback beacon material/despawn ownership, interaction/fabricator cold-allocation comment compliance, `SubtitleManager` singleton-removal compile drift, the generated Entities-package warning/error gate, stale `Hecton8.Editor.csproj` audit-helper includes, and generated-output pruning for PlayMode test compile recovery. May 3 evidence included Unity batchmode success with strict C# failure scan `0`, local Core build `0 Error(s)` / `0 Warning(s)`, local Editor/Input/optional DOTS builds `0 Error(s)` / `0 Warning(s)`, scoped PlayMode test compile `0 Error(s)` / `0 Warning(s)`, and Unity EditMode spatial-hash self-test `3/3` passed. Earlier full verbose builds showed third-party/package warnings from URP, GPUInstancer, Crest, Den.Tools, WaveHarmonic.Crest, and ShaderGraph. Unity batch also logged a licensing access-token update error. Local `dotnet build` evidence must be serial because shared `Temp/obj` can create false `CS2012` locks under parallel builds. It is not Play Mode, GCMonitor, MCP runtime-console, profiler, save/load roundtrip, construction graph teardown soak, malformed-airlock-prefab proof, moving-base airlock transition proof, airlock event-listener churn proof, sealed-door laser-cut progress/VFX proof, tether visual scene proof, inventory degradation/reactive-chemistry scene proof, crafting/deconstruction gameplay proof, quest progression gameplay proof, fauna hibernation restore proof, flora cascade visual/profiler proof, fallback beacon deploy/retract/save-load color or prefab-pool-failure despawn proof, PlayMode test execution, foveated/raycast burst, subtitle HUD scene proof, tech-art audit menu execution, vendor-warning cleanup, licensing repair, or memory-retention proof.
Latest addendum inside that report removes the `QuestStateManager.EvaluateSignal()`, `SargassumMicroFaunaBoids.PrimeFoveatedSimulationDecision()`, and `FaunaSimulationEngine.RunHibernationCatchUp()` synchronous `IJob*.Run()` sites, fixes fallback beacon material/despawn ownership so later fallback deployments cannot recolor older fallback beacons through a shared mutable material or route non-pooled fallback objects into pool despawn after prefab-spawn failure, moves `PlayerFlashlight`, `HectonFabricatorUI`, interaction prompt UIs, `DiegeticTooltipSystem`, and `PDAIntrusionManager` input subscription/action ownership to lifecycle/hot-swap paths, caches `PlayerInteraction.ActiveInteractKey`, and moves `EndingTerminalInteractable` prompt localization to lifecycle/language-change caches. Current `2026-05-03_FOUNDATION_GUARD_SCAN.md` was regenerated after the May 4 repair and now reports `500` global registry self-registration inventory sites, `0` remaining `.Run(` sites as a hard gate, `0` hot-path `.Run(` review sites, `5` completion `.Complete(` text hits, `0` `UnsafeUtility.MemCpy outside guard`, `8` runtime Find API review hits, `0` direct `InputManager.Instance` sites, release-reachable direct hot-path debug-log sites `0`, release-reachable one-hop debug-log review sites `0`, `Optimization singleton residue = 0`, unauthorized Unity loop methods `0`, legacy coroutine sites `0`, forbidden runtime asset API sites `0`, and broad physics layer masks outside Editor `0`. Latest May 4 serial Core, Editor, DOTS, and PlayModeTests builds report `0 Warning(s)` / `0 Error(s)`.

Historical verification note: `Fabricator.cs` release-log preprocessor drift was repaired, the stale `Hecton8.Core.csproj` include for the existing NativeQueue-backed `BaseAirlockEvents.cs` was restored, controlled scoped Core build reported `0 Warning(s)` / `0 Error(s)`, and Unity batchmode fallback strict C# failure scan reported `0`.

`2026-05-03_FOUNDATION_GUARD_SIGNAL_CLEANUP.md` records guard-signal cleanup around asset dispatch.
`AssetLoadDispatcher.Complete(int, bool)` was renamed to `AcknowledgeDispatchRequest(int, bool)` so the `.Complete(` guard no longer mixes asset ticket acknowledgement with real `JobHandle` fences. May 3 guard state at report time was `.Run(` sites `0`, hot-path `.Run(` review sites `0`, `.Complete(` text hits `1`, guarded dispatcher completion sites `1`. Current May 4 post-repair guard state is `.Run(` sites `0`, `.Complete(` text hits `5`, guarded dispatcher completion sites `1`, `UnsafeUtility.MemCpy outside guard = 0`, and unauthorized Unity loop methods `0`. Evidence remains source/build only. It is not Play Mode, GCMonitor, Addressables streaming, or memory-retention proof.

`2026-05-03_OPTIMIZATION_REGISTRY_OWNERSHIP.md` records the Optimization/VRAM ownership cleanup.
`AssetLifecycleGovernor`, `AssetLoadDispatcher`, `VRAMMonitor`, `VRAMPressureMonitor`, `RenderTextureLifecycleTracker`, `RenderTexturePool`, `VisorRTManager`, `CameraRTManager`, `PostFXRTManager`, and `UIRTManager` no longer retain private static `_instance` or internal `Instance` residue. Duplicate authority now resolves through `GlobalRegistry` slots. Evidence is local static scan, full local `Hecton8.Core` compile `0 Warning(s)` / `0 Error(s)`, Unity batchmode script/import compile with strict failure scan `0`, and foundation guard `Optimization singleton residue = 0`. It is not Play Mode, MCP console, GCMonitor, VRAM residency, duplicate-scene-object, or memory-retention proof.

`2026-05-03_SETTINGS_PERSISTENCE_REGISTRY_REBIND.md` records the latest settings/UserOptions persistence-order hardening.
`SettingsManager` now prefers `GlobalRegistry.Settings` for safe access, registers as an `IGlobalRegistryHotSwapListener`, refreshes `GlobalRegistry.UserOptions` before load/save helpers, and receives a bootstrap UI-phase `RefreshPersistenceFromRegistry()` call after settings runtime registration. Evidence is local `Hecton8.Core` compile only: `0 Warning(s)`, `0 Error(s)`, plus scoped diff check. It is not settings-panel scene flow, PlayerPrefs persistence, save/reload roundtrip, Play Mode, GCMonitor, profiler, or MCP console proof.

`2026-05-03_HABITAT_GRAPH_ANCHOR_STATE_HARDENING.md` records the habitat graph anchor-state correction.
`HabitatGraphManager` now separates authoritative `_anchorReachability` from BFS traversal scratch via a persistent `_traversalVisited` buffer, guards the global siege-target snapshot with a publishing owner token, and resets that static snapshot at subsystem registration. `LoreDatabaseManager` now flushes its deferred native unlock-word disposal job without calling `.Complete()`. Evidence is local dotnet compile only: latest scoped Input and Core commands returned `0 Error(s)` / `0 Warning(s)`. It is not Play Mode, GCMonitor, scene/prefab, logistics gameplay, predator siege cognition, graph replacement/disposal, lore save/load, domain reload leak detection, or memory-retention proof.

`2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md` is the previous documentation-sweep addendum.
It records the active-doc inventory boundary, dirty-worktree risk, conflicting old logs, fresh local `dotnet build ./Hecton8.Core.csproj` result: `0 Error(s)`, `136 Warning(s)`, elapsed `00:01:24.05`, and latest post-restore `--no-restore` rerun result: `0 Error(s)`, `73 Warning(s)`, elapsed `00:00:23.95`.

`2026-05-01_OBJECTIVE_PROJECT_CONCLUSION.md` is the current blunt project-level verdict.
It is source/doc-backed, but still not Play Mode proof.

`2026-05-01_EDITOR_LOG_CONSOLE_STABILIZATION.md` is the current local `Editor.log` evidence for console-spam mitigation.
It supersedes older same-day statements that the editor console had known C# warnings or `SetResource` spam in the latest reachable local log, but it is not Play Mode or profiler proof.

`2026-05-01_EVENT_BUS_SPATIAL_HASH_COMPILE_DELTA.md` records the May 1 compile-clean source migration around listener-backed Sargassum/Emergency relay events, the Burst spatial-hash `in` argument fix, and the May 1 MCP console zero-entry check. Current console truth is the May 4 post-repair/current recheck: editor console error/warning entries `0`. The earlier May 4 documentation-sweep `18` warning snapshot is historical Play Mode-transition evidence, not the current editor-console boundary.

`2026-05-01_COMPILE_STABILIZATION_CONTINUATION.md` supersedes the older compile-evidence line numbers after the `VegetationJobRecovery.cs.meta` restoration. It records the Bee file-lock/internal-error recovery, current `Editor.log` compile/reload success, and final MCP console zero-entry check.

`2026-05-01_EVENT_CASCADE_RECHECK.md` corrects stale event-bus audit claims.
It confirms the source-present `HectonEventBus` depth cap and keeps NativeQueue generation split as the remaining event-cascade risk. As of 2026-05-03, `ModRegistryEvents`, `BootstrapEvents`, `LocalizationEvents`, `InteractionEvents`, `CraftingEvents`, `ScanEvents`, `SaveEvents`, `InventoryEvents`, `WeatherEvents`, `QuestEvents`, `PowerGridTelemetryEvents`, `NarrativeEvents`, `NotificationEvents`, `FirstHourEvents`, `EndingEvents`, `AudioLogEvents`, `AtmosphereEvents`, `EclipseGameplayEvents`, `AcousticZoneEvents`, `PhysicsEventBus`, `CelestialEvents`, `FluidFeedbackEvents`, `RepairDroneTorchAcousticEvents`, `ElectrolysisAcousticEvents`, `AudioCaptionEvents`, `SpectrumEvents`, `ProceduralAudioEvents`, `HectonSubmarineOsEvents`, `LaserCutterEvents`, `MapMagicBiomeEvents`, `BiomeMatrixEvents`, `DirectorAIEvents`, `HectonDroneFleetEvents`, `FlashlightEvents`, `PlayerSignalEvents`, `HighPressureEvents`, `FatalPressureImplosionEvents`, `ModuleStatusEvents`, `BaseAirlockEvents`, `DepthZoneEvents`, `SoundscapeEvents`, `EmergencyServiceRelayEvents`, `SargassumGlobalDragManager`, `AtlasSignalEvents`, `PlayerExpressionEvents`, `BaseIntegrityEvents`, `PDAIntrusionEvents`, `PDAEvents`, `SceneBootstrap`, `ObjectPoolDiagnostics`, `PerformanceEvents`, `RandomEventEvents`, `Atlas6Events`, and `GlobalRegistry` service rebound events have source-level generation split; remaining lanes still require review and runtime proof.

## Active Secondary Reports

These are useful, but not first-read project-state authority:

- `CI_VALIDATION_HOOKS_SURGERY_LOG.md`
- `2026-05-03_FOUNDATION_GUARD_SCAN.md`
- `NAVGRID_LEAK_PURGE_SURGERY_LOG.md`
- `OMEGA_PURGE_SURGERY_LOG.md`
- `GC_SINGLETON_KILL_LIST.md`

## Evidence Artifacts

Patch files are evidence artifacts, not narrative authority:

- `2026-04-29_Habitat_Logistics_Graph_Diff.patch`
- `NAVGRID_LEAK_PURGE_DIFF.patch`
- `2026-05-05_OMEGA_AUTONOMY_FORENSIC_HARDENING_DIFF.patch`

For full doc importance sorting, read:

- `2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`
- `2026-05-04_DOCUMENTATION_HEADER_ARCHIVE_QUEUE.md`
- `../ARCHIVARIUS REPORTS/01_GENERAL_INFO/DOC_AUTHORITY_CLASSIFICATION.md`

## Deprecated

Historical static snapshots moved out of the active report root:

- `DEPRECATED/2026-04-29_Static_Audit_Snapshots/ILLEGAL_SINGLETONS.md`
- `DEPRECATED/2026-04-29_Static_Audit_Snapshots/GC_HOTPATH_VIOLATIONS.md`
- `DEPRECATED/2026-04-29_Static_Audit_Snapshots/BOOT_ORDER_VIOLATIONS.md`
- `DEPRECATED/2026-04-29_Static_Audit_Snapshots/NATIVE_ALLOCATION_AUDIT.md`


