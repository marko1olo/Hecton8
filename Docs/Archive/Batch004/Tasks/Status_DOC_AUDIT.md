# DOC_AUDIT Status

Agent: DOC_AUDIT  
Domain: Documentation / Project Reality Audit  
Source task: Direct user continuation request, no matching `<AGENT_PROMPT id="DOC_AUDIT">` in `Docs/Tasks/CURRENT_BATCH.md`.  
Status: PENDING VERIFICATION  
Batch note: Previous DOC_AUDIT state was found under `Docs/Archive/Batch003/`; active R5 state is restarted here because current `Docs/Tasks/` had no DOC_AUDIT status file.  
Evidence class ceiling: STATIC_SOURCE / STATIC_DOC / FILESYSTEM / PACKAGE_LOCK unless explicitly noted otherwise.  

## Mandates Read

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/PROJECT_LTS_Compatibility_Layer.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `.agents-skills/CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `.agents-skills/UI_Data_Streaming_ZeroGC_Optimization.txt`
- `.agents-skills/PROG_Quest_State_Graph_Logic.txt`
- `.agents-skills/UI_Diegetic_Physical_Interfaces.txt`
- `.agents-skills/GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_GPU_Occlusion_Culling_6000.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

## Continuation R5 - 2026-05-13

- [x] Verify Unity/project configuration claims against source-of-truth files | Justification: DOD practice = ProjectSettings/Packages evidence beats prose; verified `ProjectVersion.txt`, `manifest.json`, `EditorBuildSettings.asset`, `QualitySettings.asset`, and URP asset GUID mappings; rejected stale prose claims about Low renderer; estimate: 0 us/frame.
- [x] Audit authority docs for package, scene-flow, URP, and forbidden-dependency drift | Justification: DOD practice = stable docs must match current engine/project surface; separated clean UPM manifest state from physical legacy asset contamination; rejected "forbidden package absent from manifest" as equivalent to "asset tree clean"; estimate: 0 us/frame.
- [x] Patch R5 contradictions in stable/active docs only | Justification: DOD practice = narrow authority corrections, no archive churn; patched X-Ray, Docs README, Reports README, Project State X-Ray, Global Architecture Map, Archivarius Project Atlas, Settings guide, and script changelog; estimate: 0 us/frame.
- [x] Append R5 report to `Docs/AgentLogs/LOG_DOC_AUDIT.md` and rationale decision | Justification: DOD practice = disk report required; recorded package/config drift, stale persistence/settings docs, and asset contamination distinction; estimate: 0 us/frame.
- [x] Run R5 static verification pass | Justification: DOD practice = readback/grep/path/package probes before chat report; verified stale ES3/PlayerPrefs/SaveData v2/settings lines removed or superseded, R5 package facts present, and `git diff --check` warnings are line-ending only; estimate: 0 us/frame.

## Continuation R5 Addendum - Save / Persistence X-Ray - 2026-05-13

- [x] Re-verified save/persistence inventory statically | Justification: DOD practice = source, scene YAML, meta GUID, and artifact-tail evidence only; rejected Unity/dotnet execution per user constraint; estimate: 0 us/frame runtime impact.
- [x] Promoted persistence findings to `Docs/PROJECT_STATE_STATIC_XRAY.md` | Justification: DOD practice = durable docs beat temporary agent logs; rejected chat-only reporting; estimate: 0 us/frame runtime impact.
- [x] Recorded key technical risk | Justification: DOD practice = memory/atomicity/proof gaps must be explicit; rejected "save code exists therefore ready"; estimate: boot allocation risk identified at about 132 MB native staging, frame cost unmeasured.
- [ ] Runtime verification remains blocked by user constraint | Justification: DOD practice = no fake PASS without Unity/PlayMode/profiler/player artifact; rejected stale May 5 log as proof; estimate: pending.

## Continuation R6 Addendum - Package / Player Settings Drift - 2026-05-13

- [x] Compared manifest, package lock, embedded package folders, and package metadata | Justification: DOD practice = package truth requires all package surfaces, not manifest alone; rejected "manifest clean = project clean"; estimate: 0 us/frame runtime impact.
- [x] Checked PlayerSettings defines and release metadata | Justification: DOD practice = scripting symbols define actual compile surface; found live `DOTWEEN` and heavy Standalone vendor symbols plus template app identifiers; estimate: 0 us/frame runtime impact.
- [x] Promoted R6 drift into stable docs | Justification: DOD practice = durable source-of-truth docs must record config drift; estimate: 0 us/frame runtime impact.
- [ ] Unity import/build compatibility proof remains blocked by user constraint | Justification: DOD practice = Crest/MicroSplat/Unity6000 compatibility cannot be proven statically; estimate: pending.

## Continuation R7 - AGENTS Authority Reality Patch - 2026-05-13

- [x] Verify primary agent authority against source-of-truth project files | Justification: DOD practice = highest authority docs must not contradict `ProjectSettings`/URP assets; verified `Abyss (Low)` maps to `URP_Low`, `URP_Low` maps to `Mobile_Renderer`, and render scale is `0.85`; estimate: 0 us/frame.
- [x] Patch `AGENTS.md` and `.codexrules/AGENTS.md` only where static evidence disproves them | Justification: DOD practice = minimal authority correction, no doctrine rewrite; patched Low renderer/scale, absent `_ThirdParty` wording, legacy package contamination note, and no-new-ES3 instruction; estimate: 0 us/frame.
- [x] Append R7 rationale/report and run static verification | Justification: DOD practice = disk report plus readback/grep/diff check; verified no stale Low=`PC_Renderer` or Low scale `0.65` hits remain in agent authority and diff check has no whitespace errors; estimate: 0 us/frame.

## Continuation R8 Addendum - World / Scatter / Streaming X-Ray - 2026-05-13

- [x] Audited world/scatter/streaming large-file responsibility | Justification: DOD practice = line-count and owner-role evidence before calling bloat; found large files are load-bearing scatter/residency/sampling/vegetation systems, not trivial filler; rejected "large file = useless heap"; estimate: 0 us/frame static audit.
- [x] Audited runtime creation and registration paths | Justification: DOD practice = bootstrap creation proof beats code existence; found `GameBootstrapper` creates `PersistentWorldRegistry` but does not create scatter/field/chunk/MapMagic/vegetation/streaming managers; rejected editor authoring tools as runtime proof; estimate: 0 us/frame static audit.
- [x] Audited world data and streaming profile surface | Justification: DOD practice = serialized data inventory plus profile readback; found `285` world `.asset` files, real proxy/final variants, and a 15 km streaming profile; rejected empty-prototype classification; estimate: 0 us/frame static audit.
- [x] Promoted R8 findings into stable docs | Justification: DOD practice = durable docs beat temporary AgentLogs; patched `PROJECT_STATE_STATIC_XRAY.md`, current report, docs index, and architecture map; estimate: 0 us/frame runtime impact.
- [ ] Runtime scene/profiler/Addressables proof remains blocked by user constraint | Justification: DOD practice = no fake PASS without Unity scene load, validators, Memory Profiler, low-tier run, or player build; estimate: pending.

## Continuation R9 - Root Docs / Atlas Governance Boundary - 2026-05-13

- [x] Reconcile DOC_AUDIT status/rationale numbering drift | Justification: DOD practice = agent logs must be internally auditable before new claims; found R8 before R7 in status and duplicated `Decision 006`; estimate: 0 us/frame.
- [x] Audit root markdown/log/json surface and root mirror handling | Justification: DOD practice = filesystem count beats stale cleanup prose; current root has 6 markdown, 3 log, 3 json, and 0 txt files; estimate: 0 us/frame.
- [x] Patch root governance/reference/atlas boundary docs | Justification: DOD practice = compatibility mirrors and generated snapshots must not outrank canonical docs; patched root reference, governance, atlas, index, architecture map, current report, and project-state boundary; estimate: 0 us/frame.
- [x] Append R9 rationale/report and static verification | Justification: DOD practice = disk report plus grep/diff/readback; verified root counts, unique Rationale decision headings, R7/R8/R9 status order, atlas boundary text, `EasySave3` editor asmdef evidence, and `git diff --check` line-ending warnings only; estimate: 0 us/frame.

## Continuation R10 - Active Root Anchor Proof Boundary - 2026-05-13

- [x] Patch active root anchors that still promoted absent May 11 build artifacts as current evidence | Justification: DOD practice = missing artifact cannot be current proof; scoped to `BUILD_PLAYTEST_ISSUES.md` and `MASTER_RELEASE_WORK_PLAN.md`; estimate: 0 us/frame.
- [x] Patch `BROKEN_PREFABS.md` snapshot boundary | Justification: DOD practice = generated snapshot must not read as Unity import/Console proof; estimate: 0 us/frame.
- [x] Promote R10 finding to current report/index/log/rationale and verify | Justification: DOD practice = durable docs plus grep/diff/readback; verified stale `Current May 11 Core compile-only evidence is` phrase is gone from active root anchors, R10 notes are present, and `git diff --check` reports line-ending warnings only; estimate: 0 us/frame.

## Continuation R11 - SpaceEngine Research Proof Boundary - 2026-05-13

- [x] Audit SpaceEngine research doc paths and smoke/build proof language | Justification: DOD practice = current source paths and artifact schema beat dated integration prose; found current MapMagic node under `Scripts/Plugins/MapMagic`, old Library smoke JSON from 2026-05-05 lacks new timing fields; estimate: 0 us/frame.
- [x] Patch SpaceEngine research doc and promote R11 to current report/log/rationale | Justification: DOD practice = active research docs must not sell old compile/smoke as current proof; patched SpaceEngine research doc, X-Ray, Docs index, Reports index, rationale, and log; estimate: 0 us/frame.
- [x] Static verification of R11 | Justification: DOD practice = grep/readback/diff-check before report; verified current SpaceEngine paths, old Library smoke JSON timestamp/schema gap, R11 report/rationale/log entries, and `git diff --check` line-ending warnings only; estimate: 0 us/frame.

## Continuation R12 - Omega Smoke Artifact Drift - 2026-05-13

- [x] Audit Omega smoke artifacts and current Library JSON | Justification: DOD practice = newest artifact content beats older embedded PASS snippets; found current `Library/OmegaAutonomySmokeTester.json` status `FAIL` on `nativeSentinelBalance`; estimate: 0 us/frame.
- [x] Patch Omega/SpaceEngine docs and indexes to reflect current FAIL / historical PASS split | Justification: DOD practice = PASS labels must remain scoped and current artifact failures must be visible; patched SpaceEngine Omega docs, Docs index, Reports index, and current X-Ray; estimate: 0 us/frame.
- [x] Promote R12 rationale/log/report and verify | Justification: DOD practice = disk report plus grep/diff/readback; verified current `Library/OmegaAutonomySmokeTester.json` `FAIL`, absent `CodexArtifacts/unity-omega-smoke-2026-05-05-doc-continuation.log`, R12 report/rationale/log entries, and no remaining active current-PASS Omega phrases in checked docs; estimate: 0 us/frame.

## Continuation R13 - Active Documentation Manifest Boundary - 2026-05-13

- [x] Audit active documentation manifest JSON files | Justification: DOD practice = generated manifests are evidence snapshots, not evergreen authority; found four `ACTIVE_DOCUMENTATION_MANIFEST` JSON files dated May 6, May 7, May 9, and May 11 with stale counts/build-state surfaces; estimate: 0 us/frame.
- [x] Patch manifest top-level boundaries | Justification: DOD practice = preserve historical evidence while preventing false current-proof use; added `docAuditR13Boundary` to each manifest and demoted counts/build states to snapshot-only evidence; estimate: 0 us/frame.
- [x] Promote R13 rationale/log/report and verify | Justification: DOD practice = JSON parse/readback/diff check before report; verified `docAuditR13Boundary` exists in all four active manifest JSON files, May 9 `coveredCurrentSource` is demoted to `false`, and current reports/indexes carry the boundary; estimate: 0 us/frame.

## Continuation R14 - Gameplay Economy / Resource Loop X-Ray - 2026-05-13

- [x] Audit item, catalog, recipe, and resource-node data references | Justification: DOD practice = authored data graph beats claims about "systems"; found `73` ItemData assets, `69` catalog refs, `41` recipes, `27` resource-node templates, duplicate `Data_Copper` authority, and `23 / 27` harvest items without `worldPrefab`; estimate: 0 us/frame static audit.
- [x] Audit inventory, fabricator, scarcity, resource-node, and logistics code paths | Justification: DOD practice = source path from mining to quest/craft must close; found real inventory SOA/crafting/fabricator/scarcity/logistics code, but `ResourceNode` drop emission depends on `PersistentWorldRegistry.TryRegisterDroppedItem`, which refuses null `worldPrefab`; estimate: 0 us/frame static audit.
- [x] Promote R14 gameplay-loop findings into durable docs | Justification: DOD practice = stable docs beat temporary chat/log memory; promoted the resource acquisition seam, duplicate copper data, and pipe/procedural wiring proof gaps to `PROJECT_STATE_STATIC_XRAY`, current X-Ray report, docs indexes, architecture map, rationale, and log; estimate: 0 us/frame.
- [ ] Runtime gameplay proof remains blocked by user constraint | Justification: DOD practice = no fake PASS without Unity/PlayMode/profiler/player route; required later route is mine copper -> `InteractionEvents.ItemCollected` -> inventory contains `Data_Copper` -> `quest_copper_sample` completes -> craft `Copper Wire` -> save/load; estimate: pending.

## Continuation R15 - AI/Fauna Data vs Runtime Wiring X-Ray - 2026-05-13

- [x] Audit fauna authored data coverage | Justification: DOD practice = recursive asset/file inventory before trusting roster prose; found `22` creature archetype assets, `22` fauna data templates, `108` fauna biome datasets, `432` non-null biome spawn prefab entries, `17` large-threat macro-zone archetype refs, `13` fauna family profiles, and `6` generated proxy prefabs; estimate: 0 us/frame static audit.
- [x] Audit fauna bootstrap and scene-wiring proof boundary | Justification: DOD practice = service readiness must be separated from visible runtime ownership; found `EcosystemRuntimeInstaller` creates genetics/health/migration managers but not `FaunaDirector`/`WorldFaunaSpawnRegistry`, while `GameBootstrapper` falls back to `DemiurgeFaunaSimulationService.Shared` with `ResidentSlotCapacity = 0`; estimate: 0 us/frame static audit.
- [x] Audit current fauna smoke artifact | Justification: DOD practice = current artifact content beats intended runner output; `.codex-artifacts/fauna-omega-smoke-2026-05-05.log` reports invalid `.codex-artifacts` directory and ends with Unity return code `1`, so it is not PASS; estimate: 0 us/frame static audit.
- [x] Promote R15 findings into durable docs | Justification: DOD practice = active docs must not let asset coverage masquerade as runtime proof; patched `Docs/AI_Fauna/*`, current X-Ray report, docs indexes, project-state X-Ray, architecture map, rationale, and log; estimate: 0 us/frame.
- [ ] Runtime visible-fauna proof remains blocked by user constraint | Justification: DOD practice = no fake PASS without Unity scene load, active `FaunaDirector`, active `WorldFaunaSpawnRegistry`, nonzero real `IFaunaSim` resident capacity, visible spawn proof, profiler/GC data, and fresh `FAUNA_OMEGA_SMOKE_RESULT` PASS; estimate: pending.

## Continuation R16 - Tools / PDA / First-Hour Interface X-Ray - 2026-05-13

Note: current X-Ray report already contains a parallel `R15` AI/Fauna boundary; this tools/PDA layer uses `R16` to avoid overwriting concurrent work.

- [x] Audit tool data, held prefabs, world prefabs, and metadata | Justification: DOD practice = data/prefab inventory before judging large files; found `12` tool ItemData assets, `12` held prefabs, `12` world prefabs, `13` ToolMetadata assets, all tool ItemData `worldPrefab` refs non-null, and orphan `LogicSpanner` metadata/source with no item/prefab/catalog/recipe route; estimate: 0 us/frame static audit.
- [x] Audit player-prefab tool/PDA/dev wiring | Justification: DOD practice = serialized player prefab beats code intent; found `PlayerToolManager`, `PlayerPDA`, `ToolLoadoutProvisioner`, `ScanLogSystem`, `PDAExchangeSystem`, and `PlayerInteraction` on `Player.prefab`; also found `ToolLoadoutProvisioner` enabled with `provisionInventoryOnStart=1`, `assignCoreLoadoutOnStart=1`, `provisionConstructionMaterialsOnStart=1`, and root `Data_Copper` starter material; estimate: 0 us/frame static audit.
- [x] Audit PDA shell placement and runtime installer boundaries | Justification: DOD practice = separate backend code from scene-mounted UI bridge; found PDA tab components in world scenes, `Player.prefab` `PlayerPDA` has null `pdaPanel`/`pdaCanvasGroup`/tabs, `DiegeticPDAController` source calls `PlayerPDA.ConfigureUI`, but its MonoScript GUID was not found in `_Project` scenes/prefabs; estimate: 0 us/frame static audit.
- [x] Promote R16 tool/PDA/first-hour findings into durable docs | Justification: DOD practice = first-hour truth must survive AgentLogs cleanup; promoted the real tool/scan/interaction stack, dev loadout contamination, PDA bridge proof gap, and LogicSpanner orphan to `PROJECT_STATE_STATIC_XRAY`, current X-Ray report, docs indexes, architecture map, rationale, and log; estimate: 0 us/frame.
- [ ] Runtime tool/PDA/first-hour proof remains blocked by user constraint | Justification: DOD practice = no fake PASS without Unity/PlayMode/profiler/player route; required later route is clean start with no dev inventory grant -> acquire/craft/equip scanner -> open PDA with visible diegetic shell -> scan copper/resource -> quest and inventory state update; estimate: pending.

## Continuation R17 - Rendering / Visor / Shader Reality X-Ray - 2026-05-13

Note: R17 is a static renderer/visor/shader documentation correction pass. It does not supersede the later R18 first-hour provisioning hardening section.

- [x] Audit URP tier assets and active renderer-feature topology | Justification: DOD practice = serialized renderer assets beat renderer prose; verified Low/Mobile render scale `0.85/0.8`, Medium/High `1`, SRP Batcher enabled, GPU Resident Drawer and its camera occlusion disabled in visible URP assets, and active feature counts Mobile `8`, PC `8`, PC High `10`; estimate: 0 us/frame static audit.
- [x] Audit visor ScriptableRendererFeature implementation surface | Justification: DOD practice = source inventory before claiming RenderGraph or hot-path quality; found `21` first-party visor renderer features, all with `RecordRenderGraph`, `16` using `AddUnsafePass`, `4` using `AddComputePass`, and `HectonVisorUberPostFeature` still using obsolete `AddRenderPass<T>` under a pragma; estimate: 0 us/frame static audit.
- [x] Audit heavy shader/compute surfaces and cinematic-cheat boundaries | Justification: DOD practice = variant and compute surface must be bounded before performance claims; found `136` shader-like files, `101` `.shader`, `31` `.compute`, `4` `.hlsl`, `191` `multi_compile`, `13` `shader_feature`, `66` `numthreads`, with variant hotspots in terrain, biolum, rock, coral, and kelp shaders; estimate: 0 us/frame static audit.
- [x] Audit lighting/radar/culling runtime evidence boundaries | Justification: DOD practice = source readiness must be separated from scene-mounted proof; found screen-space light shafts, ground radar, and instance culling implement caps/telemetry/black-box dumps and visual-fake/indirect paths, but static GUID scans found no serialized `_Project` scene/prefab placement for their runtime MonoBehaviours; estimate: 0 us/frame static audit.
- [x] Promote R17 findings into durable docs | Justification: DOD practice = active docs must not sell ambitious renderer source as measured MX350/runtime proof; patched `PROJECT_STATE_STATIC_XRAY`, current X-Ray report, docs indexes, architecture map, cinematic-cheats ledger, rationale, and log; estimate: 0 us/frame.
- [x] Reconcile R17 with existing R18 first-hour hardening | Justification: DOD practice = a new audit layer must not downgrade later static facts; corrected active indexes that still described `ToolLoadoutProvisioner` startup grants as current after R18 disabled/gated them; estimate: 0 us/frame.
- [ ] Runtime renderer proof remains blocked by user constraint | Justification: DOD practice = no fake PASS without Unity/Frame Debugger/RenderDoc or profiler capture; required later route is target scene load -> tiered URP asset selection -> feature timing/VRAM/GC capture -> shader variant and scene-placement proof; estimate: pending.

## Continuation R18 - First-Hour Dev Provisioning Hardening - 2026-05-13

- [x] Disable player-prefab startup tool/material grants | Justification: DOD practice = first-hour route must not start from hidden dev inventory; changed `Player.prefab` `ToolLoadoutProvisioner` startup flags to `0`; rejected relying on docs warning while serialized flags remained hot; estimate: 0 us/frame.
- [x] Add release guard to `ToolLoadoutProvisioner` provisioning paths | Justification: DOD practice = dev helpers must not mutate release inventory/loadout even if accidentally serialized; guarded startup/manual provisioning and preset/loadout assignment behind `UNITY_EDITOR || DEVELOPMENT_BUILD`; estimate: 0 us/frame in release because guarded methods return cold-path only.
- [x] Canonicalize provisioner starter copper to cataloged raw copper | Justification: DOD practice = dev/testing shortcuts must not inject non-catalog item authority; changed serialized starter material and editor auto-resolve path from root `Data_Copper` GUID `84877e24023afe648a6682f49f11defa` to raw cataloged GUID `7a9f752461931354e865d30b319c0f35`; estimate: 0 us/frame.
- [x] Promote R18 fix into durable docs | Justification: DOD practice = current docs must supersede the R16 finding after code/prefab hardening; patched status, rationale, log, project-state X-Ray, current X-Ray, docs indexes, and architecture map; estimate: 0 us/frame.
- [ ] Runtime first-hour proof remains blocked by user constraint | Justification: DOD practice = static prefab/source fix is not Play Mode proof; required later route is clean start -> resource acquisition -> quest/craft/equip scanner -> visible PDA shell -> scan/log update; estimate: pending.

## Continuation R19 - Resource Pickup Data Canonicalization - 2026-05-13

- [x] Canonicalize first-hour copper data refs | Justification: DOD practice = cataloged ItemData authority must own the resource route; changed `ResourceNodeTemplate_CopperVein` and three barter offers from root non-catalog `Data_Copper` GUID `84877e24023afe648a6682f49f11defa` to cataloged raw GUID `7a9f752461931354e865d30b319c0f35`; estimate: 0 us/frame.
- [x] Wire existing pickup prefabs into matching raw resource ItemData | Justification: DOD practice = do not invent a code grant when authored pickup prefabs already exist; set non-null `worldPrefab` refs for cataloged `Data_Copper`, `Data_FiberKelp`, `Data_HydrocarbonResin`, `Data_MembraneTissue`, `Data_SilicaShards`, and `Data_SilverOre`; estimate: 0 us/frame static data change.
- [x] Recount resource-node pickup risk after data patch | Justification: DOD practice = current docs must reflect post-fix data state; static harvest refs now show `16 / 27` primary harvest items still missing `worldPrefab` and `3 / 27` refs still not cataloged, down from R14 `23 / 27` and `4 / 27`; estimate: 0 us/frame.
- [x] Promote R19 into durable docs | Justification: DOD practice = first-hour data fixes must not live only in chat or transient logs; patched status, rationale, log, project-state X-Ray, current X-Ray, docs indexes, and architecture map; estimate: 0 us/frame.
- [ ] Runtime resource route proof remains blocked by user constraint | Justification: DOD practice = static data canonicalization is not Play Mode proof; required later route is mine copper -> pickup/registry emission -> `InteractionEvents.ItemCollected` -> inventory contains cataloged `Data_Copper` -> quest/craft path updates; estimate: pending.

## Continuation R20 - Resource Content Validator Hardening - 2026-05-13

- [x] Extend `ContentSanityValidator` resource-node checks | Justification: DOD practice = content holes need editor-time tripwires, not repeated manual audits; validator now checks `ResourceNodeTemplate.harvestYield` and `rarityDrops` item refs for null, active `ItemCatalog` ownership, and non-null `ItemData.worldPrefab`; estimate: 0 us/frame editor-only.
- [x] Add validator counters for resource-yield defects | Justification: DOD practice = failures must be visible in summary, not buried in log spam; added `ResourceNodeYieldMissingWorldPrefab` and `ResourceNodeYieldNotCataloged` counts to the validator summary; estimate: 0 us/frame.
- [x] Promote R20 into durable docs | Justification: DOD practice = validator behavior changes must be recorded with the data hardening trail; patched status, rationale, log, project-state X-Ray, current X-Ray, docs indexes, and architecture map; estimate: 0 us/frame.
- [ ] Unity validator execution remains blocked by user constraint | Justification: DOD practice = static code review is not editor execution; no Unity menu run, Console output, or asset import validation was performed; estimate: pending.

## Continuation R21 - Resource Pickup Route Closure - 2026-05-13

- [x] Close resource-node catalog membership gaps | Justification: DOD practice = inventory acceptance requires active `ItemCatalog` runtime descriptors; added `Data_CarbonGraphite`, `Data_PressureDiamond`, and `Data_VoidGlassMeteorite` to `ItemCatalog`, leaving only legacy root `Data_Copper` non-cataloged; estimate: 0 us/frame.
- [x] Close resource-node primary-harvest `worldPrefab` gaps | Justification: DOD practice = `PersistentWorldRegistry.TryRegisterDroppedItem` rejects null `ItemData.worldPrefab`; assigned existing cheap pickup shells to the remaining resource-node harvest items, reducing current primary-harvest null world-prefab refs from `16 / 27` to `0 / 27`; estimate: 0 us/frame data refs, runtime prefab visuals reuse existing pools.
- [x] Add Addressables-missing fallback for small item pickup prefabs | Justification: DOD practice = current filesystem has no `Assets/AddressableAssetsData`, so Addressables-only world-prefab lookup can fail despite serialized `ItemData.worldPrefab`; `ItemCatalog` now falls back to direct `ItemData.worldPrefab` when no valid Addressables entry or load result exists; estimate: lookup-only cold/hydration path, no per-frame allocation added.
- [x] Harden bootstrap and validator against regression | Justification: DOD practice = authoring scripts must not reintroduce fixed data; `BarterBootstrapAuthoring` now loads raw cataloged copper, and `ContentSanityValidator` also checks resource-yield world prefabs for `PickupItem`/`HectonItem`, `Collider`, and `Rigidbody`; estimate: editor-only validator.
- [ ] Runtime resource route proof remains blocked by user constraint | Justification: DOD practice = static catalog/prefab/code closure is not Play Mode proof; required later route remains mine copper -> hydrated pickup -> `InteractionEvents.ItemCollected` -> inventory contains cataloged `Data_Copper` -> quest/craft path updates; estimate: pending.
