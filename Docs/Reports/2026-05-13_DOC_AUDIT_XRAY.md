# 2026-05-13 DOC_AUDIT X-Ray

Date: 2026-05-13
Status: PENDING VERIFICATION
Evidence class: STATIC_SOURCE / STATIC_DOC / FILESYSTEM / PACKAGE_LOCK only
Scope: documentation authority, root/doc surface, package pins, build-settings text, asmdef graph, and stale proof references.

## Current Boundary

This report does not claim Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, or visual quality proof.

Build errors are not the center of this audit. The center is whether documentation still matches current disk reality.

## Facts Observed

| Area | Current static fact |
|---|---|
| Unity project pin | `ProjectSettings/ProjectVersion.txt` = `6000.4.1f1` |
| URP package | `com.unity.render-pipelines.universal` = `17.4.0` |
| Addressables package | `com.unity.addressables` = `2.7.6` |
| Input System package | `com.unity.inputsystem` = `1.19.0` |
| AI Navigation package | `com.unity.ai.navigation` = `2.0.11` |
| Embedded package drift | `packages-lock.json` and physical `Packages/` include embedded Crest `5.4.1`, MicroSplat `3.9.0`, and embedded ShaderGraph; `manifest.json` does not list those package IDs directly |
| Crest compatibility surface | Crest package metadata targets Unity `2022.3` and lock dependencies mention RP Core/ShaderGraph `14.0.11`; current project is Unity `6000.4.1f1` / URP `17.4.0` |
| DOTS/Entities manifest | `com.unity.entities` is absent from `Packages/manifest.json` |
| Forbidden UPM package IDs | `com.demigiant.dotween`, `com.darktonic.masteraudio`, `com.moodkie.easysave`, and `com.arongranberg.astar` are absent from `Packages/manifest.json` |
| Scripting define contamination | PlayerSettings define `DOTWEEN` on multiple platforms; Standalone also defines `CREST_OCEAN`, `CREST_URP`, `__MICROSPLAT__`, `MAPMAGIC2`, `GPU_INSTANCER`, `ODIN_INSPECTOR`, `BAKERY_INCLUDED`, `VLB_URP`, and other vendor symbols |
| XR package config | no `com.unity.xr.management` or `com.unity.xr.openxr` in manifest/lock; only legacy `ProjectSettings/XRSettings.asset` was observed |
| Release metadata | template app identifiers remain for Android/Standalone/iPhone; product name is `Submerge`, bundle version `0.1.0` |
| BuildSettings scenes | `00_BOOTSTRAP`, `01_MAIN_MENU`, `02_HECTON_WORLD` |
| Quality / URP project config | current quality index `0` = `Surface (Medium)` -> `URP_Medium`; `Abyss (Low)` -> `URP_Low`; `Orbit (High)` -> `URP_High` |
| Low URP renderer mapping | `URP_Low (PC_RPAsset).asset` points to `Mobile_Renderer.asset`, not `PC_Renderer.asset` |
| Forbidden legacy asset folders | still physically present: Astar `37` dirs / `605` files; Easy Save 3 `22` dirs / `422` files; Demigiant `13` dirs / `357` files including DOTween/DOTweenPro; DarkTonic/MasterAudio `32` dirs / `346` files / about `51 MB` |
| First-party forbidden runtime usage scan | no first-party `.cs` hits for `DG.Tweening`, `DOTween`, `ES3`, `Easy Save`, `MasterAudio`, or `DarkTonic`; Astar appears as dormant archetype/editor labels and `ThirdPartyStrippingGuard` text |
| Root markdown | `6` files: `AGENTS.md`, `BROKEN_PREFABS.md`, `BUILD_PLAYTEST_ISSUES.md`, `MASTER_RELEASE_WORK_PLAN.md`, `PROJECT_ATLAS.md`, `TERRAIN_AND_BIOME_REALITY_MAP.md` |
| Root logs/json | `3` `.log` files and `3` `.json` files remain in repository root |
| First-party asmdefs | `24` under `Assets/_Project` |
| New/previously unindexed asmdefs | `Assets/_Project/Scripts/Core/Memory/Hecton8.Core.Memory.asmdef`; `Assets/_Project/Scripts/Physics/Determinism/Hecton8.Physics.Determinism.asmdef` |
| Source file churn | `Assets/_Project/**/*.cs` moved during this audit from `1403` to `1411`; treat exact counts as volatile |
| Latest R4 source line snapshot | `Assets/_Project/**/*.cs` = `869871` physical lines; `Assets/_Project/Scripts/**/*.cs` = `852315` physical lines |
| Latest R4 source counters | `1411` project C# files; `1365` script C# files; `1401` non-test C# files; `336` direct `Scripts/*.cs` files |
| Interface declaration hits | `215` under `Assets/_Project` |
| `GlobalRegistryContracts.cs` direct public interfaces | `51`; older `33`, `40`, and `41` dashboard/manifest counts are stale |
| Direct `Docs` root | after concurrent churn: `11` direct `.md` files plus `Actual Domains of Project.txt`; new direct doc observed: `PROJECT_STATE_STATIC_XRAY.md` |
| Docs corpus R4 snapshot | `918` markdown files under `Docs`; `283` active markdown after excluding `DEPRECATED`, `Archive`, `_Archive`, `AgentLogs`, `Tasks`, and obsolete surfaces; `203` active non-`Docs/Reports` markdown; `80` active direct report markdown; `10` docs JSON |

## Continuation R2 - 2026-05-13

User requested continuing actuality maintenance after the first pass.

Additional static findings:

- `Docs/AI_Fauna/*`, `Docs/Flora_Pipeline/*`, `Docs/Scatter_Runtime/*`, `Docs/Legacy_*/*`, and many `Docs/ARCHITECTURE/*` reference files still used `Current compile-only evidence:` for the missing May 11 artifact.
- Those lines were not historical framing. They made an absent file look like the current compile proof.
- The active docs corpus is larger than the May 11 manifest claimed: current R4 static scan sees `918` markdown files under `Docs`, not `449`.
- Concurrent churn added `Docs/PROJECT_STATE_STATIC_XRAY.md` as a direct `Docs` root static risk register during R2; active direct Docs root now has `11` markdown files.
- Current source interface hits are `215`, not the previously recorded `204`; source churn is active.

Action:

- Replaced active non-report `Current compile-only evidence:` lines with a May 13 DOC_AUDIT override that states the artifact is absent and runtime verification remains pending.
- Historical dated reports remain snapshots unless an active index promotes them.

## Continuation R3 - 2026-05-13

Additional static findings:

- `GlobalRegistryContracts.cs` now has `51` direct public interfaces. `INTERFACE_HEALTH_DASHBOARD.md` and Archivarius atlas text still carried a `41`-interface current count.
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO` linked `INTERFACE_HEALTH_DASHBOARD.md` and `EVENT_FLOW_MAP.md` as local files; current files live under `../02_ACTUAL_REPORTS/`.
- Archivarius atlas still pointed MapMagic node paths under `Assets/_Project/Scripts/World/`; current nodes live under `Assets/_Project/Scripts/Plugins/MapMagic/`.
- Archivarius atlas still listed `Assets/_Project/UI`; current tree has no direct `Assets/_Project/UI` folder. UI runtime code is under `Assets/_Project/Scripts/UI`.

Action:

- Updated interface-count override and current interface-name list in `ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/INTERFACE_HEALTH_DASHBOARD.md`.
- Requalified Archivarius 01 references to `../02_ACTUAL_REPORTS/INTERFACE_HEALTH_DASHBOARD.md` and `../02_ACTUAL_REPORTS/EVENT_FLOW_MAP.md`.
- Corrected MapMagic node paths and absent UI folder wording in `ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md`.

## Continuation R4 - 2026-05-13

Additional static findings:

- The previous active-doc counter model accidentally included `Docs/Archive`. R4 treats `Docs/Archive` as archive, not active documentation.
- R4 current active markdown count under the explicit active-doc model is therefore `283`, not the earlier inflated `536`.
- `EVENT_BUS_MAP.md` still listed absent `Assets/_Project/Scripts/SceneBootstrap.cs`; current scene event/runtime owner path is `Assets/_Project/Scripts/Core/SceneRuntimeService.cs`.
- `SYSTEM_INTERCONNECT_MATRIX.md` used short editor path `Editor/KinematicGhostDebugger.cs`; current source path is `Assets/_Project/Scripts/Editor/KinematicGhostDebugger.cs`.
- `GLOSSARY.md` referenced stale `PlayerMovement.cs`; current source file is `HectonPlayerMovement.cs`.

Action:

- Updated the active-doc counter boundary and the three source-path references above.
- Kept archive and deprecated bundles as historical evidence, not current authority.

## Continuation R5 - 2026-05-13

Additional static/package findings:

- Project pin is still `6000.4.1f1`; package pins of interest are URP `17.4.0`, Addressables `2.7.6`, Input System `1.19.0`, AI Navigation `2.0.11`, and Memory Profiler `1.1.12`.
- `Packages/manifest.json` is clean of the forbidden UPM IDs checked above, but the physical `Assets` tree is not clean because legacy Astar, Easy Save 3, DOTween, and MasterAudio folders remain.
- `ThirdPartyStrippingGuard` audits `Crest`, `MapMagic`, `Steamworks`, `GPUInstancer`, `AstarPathfindingProject`, and `Feel`; it does not currently name `Easy Save 3`, `Demigiant`, `DOTween`, `DarkTonic`, or `MasterAudio`.
- `UserOptionsPersistence` writes `Application.persistentDataPath/options.h8cfg` with a fixed `64 KB` payload buffer and `JsonUtility` inside the portable file wrapper. It is not PlayerPrefs and not Easy Save 3.
- `SaveData.CurrentVersion` is `68`; tool durability save fields are plain `Dictionary<string, float>` / `Dictionary<string, bool>` serialized by `SaveBinaryPayloadCodec`, not `ES3SerializableDictionary`.

Action:

- Promoted the distinction between package-lock cleanliness and physical asset contamination into stable/active docs.
- Corrected script-local settings/save docs that still described PlayerPrefs, Easy Save 3, `SaveData v2`, and ES3 dictionary state as current.

## Continuation R6 - 2026-05-13

Additional package/player-settings findings:

- `manifest.json` is not sufficient package truth. `packages-lock.json` and physical `Packages/` show embedded `com.waveharmonic.crest`, `com.jbooth.microsplat.core`, `com.jbooth.microsplat.urp2022`, and `com.unity.shadergraph`.
- Crest package metadata is `5.4.1`, Unity `2022.3`, while the current project is Unity `6000.4.1f1` / URP `17.4.0`; lock dependency text still mentions RP Core/ShaderGraph `14.0.11`.
- MicroSplat package metadata is `3.9.0`, with core targeting Unity `2019.4` and URP2022 support targeting Unity `2022.2`.
- `ProjectSettings.asset` scripting defines include `DOTWEEN` on many platforms even though first-party static source does not show active DOTween usage.
- Standalone define surface activates many vendor integrations at once: Crest, MicroSplat, MapMagic, GPUInstancer, Odin, Amplify, Shapes, MoreMountains/NiceVibrations, Bakery, VLB, RealtimeCSG, and DOTween.
- XR/VR cannot be called platform-ready from config: no XR Management or OpenXR package entries were found; `XRSettings.asset` is only a small legacy settings file.
- Release metadata is still template-grade: Android/Standalone/iPhone application identifiers are Unity template IDs.

Action:

- Promoted package/player-settings drift into `Docs/PROJECT_STATE_STATIC_XRAY.md`.
- Tightened the statement from "forbidden packages absent" to "forbidden UPM IDs absent, but physical folders and scripting defines still contaminate the build surface."
- Marked Crest/MicroSplat/Unity 6000 compatibility as requiring Unity import/build proof, not static confidence.

## Continuation R7 - 2026-05-13

Primary authority findings:

- `AGENTS.md` and `.codexrules/AGENTS.md` still stated Low tier renderer as `PC_Renderer` and Low render scale as `0.65`.
- Current source-of-truth assets show `Abyss (Low)` -> `URP_Low (PC_RPAsset).asset`, `URP_Low` -> `Mobile_Renderer.asset`, and `m_RenderScale: 0.85`.
- Both agent authority files listed `Assets/_ThirdParty/` as the third-party location, but the current static scan does not find that folder; actual contamination is under `Assets/Plugins`, `Assets/AstarPathfindingProject`, `Assets/Resources`, and physical `Packages/`.
- Both files still had a legacy Easy Save 3 instruction to add `[ES3NonSerializable]`, which conflicted with the current forbidden-backend policy.

Action:

- Patched `AGENTS.md` and `.codexrules/AGENTS.md` to Low renderer `Mobile_Renderer`, Low scale `0.85`, current third-party contamination wording, and "no new ES3 usage" wording.
- Evidence remains static only. No Unity import/build/player validation was run.

## Continuation R8 - 2026-05-13

World/scatter/streaming findings:

- `WorldProceduralScatterDirector.cs` is about `526.5 KB` / `10620` lines and remains the live scatter owner, not dead filler.
- `HectonMapMagicVegetationBridge.cs`, `WorldProceduralFieldSampler.cs`, and `WorldChunkResidencyManager.cs` are large because they contain real residency, sampling, vegetation, Addressables/additive-scene, NativeCollection, and telemetry machinery.
- `WorldChunkStreamingProfile.asset` exists with a `15000 m` world, `192 m` chunks, `64 m` cells, `768 m` macro zones, and `180/420/900/1800` radius bands.
- `Assets/_Project/Data/World` has `285` `.asset` files, including `78` family profiles, `37` procedural placement rules, `35` flora templates, `33` procedural families, and `13` procedural biome contexts.
- Procedural family assets contain real proxy/final variant data: static scan found `62` proxy-only entries and `179` final-ready entries.
- The runtime guarantee is weaker than the code: `GameBootstrapper` creates `PersistentWorldRegistry`, but does not create scatter, field sampler, chunk residency, MapMagic, vegetation, streaming, slice, or scatter-budget managers.
- Static text scene/prefab/data scans still do not prove serialized world-runtime manager wiring or `WorldChunkStreamingProfile.asset` assignment in the production world scene.
- `WorldRuntimeBootstrapAuthoring` and `WorldStreamingWiringValidator` provide editor authoring/validation paths, but these are not runtime proof.
- `WorldChunkResidencyManager` has Addressables code through `UNITY_ADDRESSABLES_EXIST`, but `Assets/AddressableAssetsData` is still absent in the current filesystem scan.
- `HectonMapMagicVegetationBridge` default native vegetation pool budget is `256 MB`; this can buy visuals on high-end machines but requires Memory Profiler proof before any MX350/toaster claim.

Action:

- Promoted this into `Docs/PROJECT_STATE_STATIC_XRAY.md` under `World / Scatter / Streaming Wiring Addendum`.
- Classification: serious world architecture, unproven production scene/runtime wiring.

## Continuation R9 - 2026-05-13

Root docs / atlas governance findings:

- Current root scan remains `6` markdown files, `3` log files, `3` json files, and `0` txt files.
- Active root authority remains only `AGENTS.md`, `MASTER_RELEASE_WORK_PLAN.md`, and `BUILD_PLAYTEST_ISSUES.md`.
- `BROKEN_PREFABS.md` is a generated snapshot, not prefab-health proof without fresh Unity import/Console evidence.
- `PROJECT_ATLAS.md` is a compatibility mirror. The detailed current asmdef graph is `Docs/PROJECT_ATLAS.md`.
- `TERRAIN_AND_BIOME_REALITY_MAP.md` is a compatibility mirror / stale legacy surface. The canonical current report is `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md`.
- `PROJECT_ATLAS.md` and `Docs/PROJECT_ATLAS.md` are asmdef graph snapshots only. They do not supersede package/player-settings drift findings, third-party contamination findings, URP Low mapping, or runtime verification boundaries.
- `Hecton8.Editor` referencing `EasySave3` in asmdef data is dependency-contamination evidence, not approval for Easy Save 3 as a runtime save backend.

Action:

- Updated `Docs/ROOT_DOCS_REFERENCE.md`, `Docs/DOC_GOVERNANCE.md`, `PROJECT_ATLAS.md`, and `Docs/PROJECT_ATLAS.md` with root mirror and atlas scope boundaries.
- Reconciled DOC_AUDIT status/rationale numbering drift so R7/R8/R9 logs are readable and there is no duplicate `Decision 006`.

## Continuation R10 - 2026-05-13

Active root anchor proof-boundary findings:

- `BUILD_PLAYTEST_ISSUES.md` and `MASTER_RELEASE_WORK_PLAN.md` still framed the absent May 11 Core build artifact as current compile-only evidence.
- The May 13 DOC_AUDIT filesystem check did not find `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.summary.txt` or `.log`; those files cannot be cited as current proof until restored or replaced.
- `BROKEN_PREFABS.md` said missing scripts = `0` without an in-file proof boundary. That table is a generated/static snapshot, not Unity import/Console/Play Mode/player-build proof.

Action:

- Updated `BUILD_PLAYTEST_ISSUES.md` and `MASTER_RELEASE_WORK_PLAN.md` to point at this May 13 X-Ray first and demote the May 11 compile-success line to stale report text.
- Added a `PENDING VERIFICATION` proof-boundary header to `BROKEN_PREFABS.md`.

## Continuation R11 - 2026-05-13

SpaceEngine research doc findings:

- `Docs/SPACE_ENGINE_RESEARCH/SPACE_ENGINE_MATH_INTEGRATION_2026-05-05.md` listed `Assets/_Project/Scripts/World/HectonSpaceEngine098MapMagicNodes.cs`, but the current file is `Assets/_Project/Scripts/Plugins/MapMagic/HectonSpaceEngine098MapMagicNodes.cs`.
- Current static readback found `SpaceEngine098TerrainKernels.cs`, `Hecton8.SpaceEngine098Terrain.asmdef`, `HectonSpaceEngine098MapMagicNodes.cs`, `SpaceEngine098TerrainSmokeTester.cs`, and `SpaceEngine098TerrainSmokeTestRunner.cs`.
- `Library/SpaceEngine098TerrainSmokeTester.json` exists with last write time `2026-05-05 22:20:59`, but it is old-schema output and does not contain the current per-node timing fields.
- The SpaceEngine compile-gate line is historical report text. DOC_AUDIT R11 did not run Unity, `dotnet`, Play Mode, profiler, GCMonitor, or the smoke harness.

Action:

- Patched the SpaceEngine research doc path and proof boundary.
- Replaced `SPACE-ENGINE MATH INTEGRATED` readiness wording with static-present / runtime-pending wording.

## Continuation R12 - 2026-05-13

Omega smoke artifact drift findings:

- Current `Library/OmegaAutonomySmokeTester.json` exists with last write time `2026-05-05 17:28:38` and status `FAIL`.
- The failure field is `nativeSentinelBalance.pass=false`, with `allocationDelta=2` and `trackedByteDelta=2560`.
- `Docs/SPACE_ENGINE_RESEARCH/OMEGA_AUTONOMY_UNITY_SMOKE_CODEX_2026-05-05.json` remains an older scoped PASS artifact with last write time `2026-05-05 05:41:36`.
- `Docs/SPACE_ENGINE_RESEARCH/HectonSandboxAbyssalShelfStandaloneSmoke.json` is newer than the inline old snippet and currently reports `MACRO SHELF VERIFIED`, not `OMEGA_VERIFIED`.
- `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfJobs.cs` has a newer filesystem timestamp than the May 5/May 7 smoke artifacts, so those artifacts cannot prove the current source snapshot.
- `CodexArtifacts/unity-omega-smoke-2026-05-05-doc-continuation.log` is absent from the current filesystem.

Action:

- Patched `Docs/SPACE_ENGINE_RESEARCH/OMEGA_AUTONOMY_CODEX_AUDIT_2026-05-05.md`, `Docs/SPACE_ENGINE_RESEARCH/OMEGA_AUTONOMY_AUDIT.md`, `Docs/README.md`, and `Docs/Reports/README.md` to demote older PASS/OMEGA labels and expose the current Library FAIL.

## Continuation R13 - 2026-05-13

Active documentation manifest boundary findings:

- `Docs/Reports/2026-05-06_ACTIVE_DOCUMENTATION_MANIFEST.json`, `2026-05-07_ACTIVE_DOCUMENTATION_MANIFEST.json`, `2026-05-08_ACTIVE_DOCUMENTATION_MANIFEST.json`, and `2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json` are generated snapshots, not evergreen authority.
- Their count fields, `sourceCounts`, authority lists, `buildState`, and per-file `entries` are scoped to their generation timestamps only.
- The May 9/R186 and May 11 build states do not become current compile/runtime proof after later workspace churn.
- The May 9 manifest's `coveredCurrentSource` field was demoted to `false`; the original snapshot value is preserved as `originalSnapshotCoveredCurrentSource=true`.
- The May 11 manifest references CodexArtifacts summary/log paths already demoted by R10 because the files are absent in the current filesystem.

Action:

- Added a top-level `docAuditR13Boundary` object to all four active manifest JSON files.
- Current manifest authority is this X-Ray report plus `Docs/Reports/README.md`; the historical JSON manifests remain usable only as dated audit trail.

## Continuation R14 - 2026-05-13

Gameplay economy / resource-loop findings:

- Item/catalog data is real, not placeholder-only: `73` ItemData assets excluding `ItemCatalog.asset`, `69` unique catalog refs, `41` recipe assets, and `27` resource-node templates.
- Recipe data is internally coherent in the static parse: `149` non-script recipe item refs resolve to current item assets, and no recipe ref points outside the catalog.
- Resource-node data has a hard break candidate: `23 / 27` template harvest items have `worldPrefab: {fileID: 0}`.
- `ResourceNode.TrySpawnLoot()` returns success early for template-driven extractor items, so legacy pooled loot is skipped. Incremental yield calls `PersistentWorldRegistry.TryRegisterDroppedItem(itemData, ...)`, and that path rejects null `worldPrefab`.
- Result: static source/data evidence supports "many resource nodes can take damage/deplete while pickup emission fails" until proven otherwise in Unity.
- Copper is split across two ItemData assets with the same `stableId: Data_Copper`: root `Assets/_Project/Data/Items/Data_Copper.asset` is used by `ResourceNodeTemplate_CopperVein` / barter and is not in `ItemCatalog`; raw `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset` is cataloged and used by recipes.
- PlayerInventory is substantial and load-bearing, not a toy list: native SOA mirrors, grid anchors, stack counts, condition, craft locks, genetics/quality, mass/volume/radiation caches, degradation, pressure crush, reactive chemistry, and save shadow state.
- Crafting/fabricator code is substantial: bounded native recipe evaluation, scan locks, power/scarcity hooks, physical output emission, and scene string evidence for `Forward_Fabricator`, `Trial_Fabricator`, `HectonFabricatorUI`, and starter resource nodes.
- Resource scarcity is runtime-installed by `EconomyRuntimeInstaller`, but authored scarcity directives are not proven populated. `ResourceDistributionDirector`, `ProceduralOreSpawner`, and `FluidPipeGraphRuntime` contain serious code, but static scans do not prove scene placement or bootstrap creation for the production route.

Action:

- Promoted this to `Docs/PROJECT_STATE_STATIC_XRAY.md` as the stable gameplay-economy addendum.
- Current verdict: the gameplay economy spine is real, but first-hour resource-loop readiness is `PENDING VERIFICATION` and likely blocked by authored data consistency before any GameObject polish matters.

## Continuation R15 - 2026-05-13

AI/Fauna data vs runtime-wiring findings:

- Recursive static data inventory found `22` creature archetype assets under `Assets/_Project/Data/AI/CreatureArchetypes`, `22` fauna data templates under `Assets/_Project/Data/Fauna`, `108` fauna biome datasets, `13` fauna family profiles, and `6` generated proxy prefabs.
- The `108` fauna biome datasets currently contain `432` `possibleCreatures` entries with non-null prefab references and `0` entries with `prefab: {fileID: 0}` under the actual spawn-entry field. They also contain `17` large-threat macro-zone archetype refs.
- `FaunaDirector` is substantial runtime code: registry-backed `IFaunaSim` service registration, adaptive perf budgets, spawn ring/culling, biome/depth/zone scaling, spawn registry resolution, pool warmup, acoustic panic commands, resident data-only simulation, and dispatcher/late-frame lanes.
- `WorldFaunaSpawnRegistry` is also real code: ordinary anchors, large-threat macro zones, runtime reef anchors, chunk/macro-zone buckets, procedural-state availability checks, and pooled anchor buckets.
- Static script-GUID search found no serialized `FaunaDirector`, `WorldFaunaSpawnRegistry`, `FaunaRuntimeSmokeTester`, or `EcosystemRuntimeInstaller` hits in current `Assets` scenes/prefabs/assets. This is not runtime absence proof, but it does mean the current static scan cannot prove production-scene wiring.
- `EcosystemRuntimeInstaller.EnsureRuntimeSystems()` creates `FaunaGeneticsManager`, `EcosystemHealthDirector`, and `MigrationDirector` under `__HECTON_ECOSYSTEM_RUNTIME`; it does not create `FaunaDirector` or `WorldFaunaSpawnRegistry`.
- `GameBootstrapper.EnsureFaunaSimulationRegistered()` uses active `FaunaDirector` if one exists, but if no real fauna simulation registers, it registers `DemiurgeFaunaSimulationService.Shared`. That fallback reports ready and has `ResidentSlotCapacity = 0`, so it proves service-slot safety, not visible fauna.
- `WorldRuntimeBootstrapAuthoring` can add/configure `WorldFaunaSpawnRegistry`, but `ConfigureFaunaDirector()` returns when no `FaunaDirector` already exists. That is editor authoring support, not proof the production scene currently owns the director.
- `.codex-artifacts/fauna-omega-smoke-2026-05-05.log` is not a PASS artifact in the current filesystem. It reports `.codex-artifacts is not a valid directory name`, executes `FaunaRuntimeSmokeTesterRunner.RunOmegaHeadlessSmoke`, and exits with Unity return code `1` without a visible `FAUNA_OMEGA_SMOKE_RESULT` PASS line.

Action:

- Patched `Docs/AI_Fauna/*` to keep fauna roster/coverage useful while demoting it from runtime spawn proof.
- Promoted the AI/Fauna data-vs-runtime-wiring boundary into `Docs/PROJECT_STATE_STATIC_XRAY.md`, this report, `Docs/README.md`, `Docs/Reports/README.md`, and `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`.
- Current verdict: AI/Fauna authored data is real and dense enough for a vertical-slice ecology pass, but visible production fauna remains `PENDING VERIFICATION` until a scene/runtime proof shows active `FaunaDirector`, active `WorldFaunaSpawnRegistry`, nonzero resident slots or active creatures, and a fresh PASS smoke/profiler artifact.

## Continuation R16 - 2026-05-13

Tools / PDA / first-hour interface findings:

- Tool data is real and internally stronger than resource pickup data: `12` tool ItemData assets, `12` held prefabs, `12` world prefabs, and all tool ItemData `worldPrefab` refs are non-null.
- Tool metadata has one orphan: `ToolMetadata_LogicSpanner.asset` and `LogicSpannerTool.cs` exist, but no `Item_Tool_LogicSpanner.asset`, held prefab, world prefab, catalog ref, or recipe ref was found.
- `Player.prefab` owns the important runtime spine: `PlayerToolManager`, `PlayerPDA`, `ToolLoadoutProvisioner`, `ScanLogSystem`, `PDAExchangeSystem`, and `PlayerInteraction`.
- `PlayerToolManager`, `PlayerTool`, `ModularEquipmentEngine`, `ScannerTool`, `ScanEvents`, `ScanLogSystem`, `PlayerInteraction`, `HectonItem`, `QuestStateManager`, and `QuestGraphEvaluator` are substantial source systems, not empty wrappers.
- `ScannerTool.cs` is about `141 KB` and contains real scan execution, non-alloc spatial-hash contact collection, scan/discovery events, focused dispatcher raycast support, feedback, and scan-log hooks.
- `Player.prefab` `ToolLoadoutProvisioner` is enabled with `provisionInventoryOnStart=1`, `assignCoreLoadoutOnStart=1`, and `provisionConstructionMaterialsOnStart=1`. It can grant the full tool kit and starter material on Start.
- The provisioner starter material is root `Data_Copper` GUID `84877e24023afe648a6682f49f11defa`, the non-catalog copper asset already flagged in R14.
- `WorldShippingContentFilter` suppresses named trial/staging scene hierarchies. Static source did not show a strip path for player-attached `ToolLoadoutProvisioner` or smoke components.
- `Player.prefab` `PlayerPDA` has null `pdaPanel`, null `pdaCanvasGroup`, and null tab refs. `PlayerPDA.Open()` can still switch input/cursor/depth-of-field/events with no panel/tabs configured.
- Binary scene string scans found PDA tab components in `02_HECTON_WORLD.unity` and `03_HECTON_WORLD_CREST5.unity`, so the correct finding is not "no PDA UI assets".
- `DiegeticPDAController.cs` is the source bridge that calls `PlayerPDA.ConfigureUI(...)`, but its class string and MonoScript GUID `8f05da9f4a7a4158a04d6cc0e0f9d8c2` were not found in `_Project` scenes/prefabs.
- `PDARuntimeInstaller` and `ProgressionRuntimeInstaller` add PDA/logbook/progression systems, but they do not add `DiegeticPDAController`.

Action:

- Promoted this to `Docs/PROJECT_STATE_STATIC_XRAY.md` as the stable tools/PDA/first-hour interface addendum.
- Current verdict: tool/scan/interaction architecture is real, but first-hour truth is contaminated by startup dev provisioning and PDA bridge proof remains `PENDING VERIFICATION`.
- Required later runtime route: clean start with no dev all-tools grant -> acquire/craft/equip scanner -> open visible PDA shell -> scan resource/copper -> quest/log/inventory state updates.

## Broken Evidence References

The stable docs and May 11 report cite:

- `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.summary.txt`
- `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.log`

Current filesystem check did not find either artifact under `C:\hades\Hecton8`.

Consequence:

- May 11 compile-success text remains a dated report claim.
- It is not artifact-backed in the current workspace until the missing files are restored or a new build artifact is captured.
- Any stable doc claiming this artifact as current proof must be read through this May 13 override.

## Root / Docs Hygiene Findings

| Finding | Action |
|---|---|
| stale Cyrillic-named direct `Docs/*.md` batch dump | moved to `Docs/DEPRECATED/Root_Stale_Batch_Prompt_Dumps_2026-05-13/`; it is not current docs authority |
| `PROJECT_ATLAS.md` exists both in root and `Docs/` | root copy is a compatibility mirror; canonical detailed copy is `Docs/PROJECT_ATLAS.md` |
| root `.log` and `.json` files are back | classify as evidence/noise, not authority |
| older atlas docs claimed `13`, `22`, or `23` asmdefs | stale; current static scan finds `24` |

## Contract-To-Source X-Ray

`Docs/SYSTEMS_CONTRACTS.md` contains historical or target file labels. Current first-party source scan found:

| Claimed/target file | Current source status |
|---|---|
| `SaveVersioning.cs` | absent |
| `SaveMigrator.cs` | absent; `SaveDataMigration.cs` and `SaveDataMigration_AupV8.cs` exist |
| `SteamManager.cs` | present under `Assets/_Project/Scripts/Plugins/Steam/` |
| `CloudSaveSync.cs` | absent; `SaveSystem/SteamCloudSaveConflictResolver.cs` exists |
| `UnderwaterAudioProcessor.cs` | absent; `SpatialAudioManager.cs` is current audio service owner |
| `CrashTelemetry.cs` | absent; `CrashTelemetryBuffer.cs` exists |
| `DebugConsole.cs` | absent |
| `BenchmarkRunner.cs` | absent |
| `ProbeGridGenerator.cs` | absent |
| `ControlRemapper.cs` / `AccessibilitySettings.cs` | absent |
| `EphemeralEventDirector.cs` / `DepthChallengeTracker.cs` | absent |

Consequence: those labels are target-contract names, not implemented-file proof.

## Risk Model

CPU: no runtime code changed.  
GC: no runtime code changed.  
Memory: no runtime code changed.  
Cadence: no runtime code changed.  
Correctness: documentation trust improved by demoting missing artifacts and stale navigation claims.

## Non-Claims

- No compile run was performed for this report.
- No Unity MCP run was performed.
- No profiler/GC/runtime data was captured.
- No claim of fixed build, clean Console, or working gameplay is made.

STATUS: PENDING VERIFICATION
