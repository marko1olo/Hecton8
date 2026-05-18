<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# 2026-05-13 DOC_AUDIT X-Ray

Date: 2026-05-13
Status: PENDING VERIFICATION
Evidence class: STATIC_SOURCE / STATIC_DOC / FILESYSTEM / PACKAGE_LOCK / UNITY_BATCHMODE_IMPORT_COMPILE / CLI_COMPILE
Scope: documentation authority, root/doc surface, package pins, build-settings text, asmdef graph, and stale proof references.

## Current Boundary

This report now includes one Unity `6000.4.1f1` batchmode import/script-compilation artifact from DOC_AUDIT R29 plus static async pager / save-buffer hardening from R30/R31/R32/R33/R36/R37/R38, generated-project/asmdef drift validation from R39, source-backed CLI compile recovery from R40, root `Hecton8*.csproj` CLI compile sweep from R41, active reference-doc propagation from R42, player-movement ladder hot-path cache hardening from R34, and HLOD PDA upload version gating from R35. R37 added local Unity Bee/Roslyn temp-output probes for `Hecton8.Core.Memory` and `Hecton8.Core` with exit code `0`, R38 demoted the full-Core success as stale under active churn, R39 narrowed the first external-build blocker to generated `Hecton8.Core.csproj` missing `23` first-party references from `Hecton8.Core.asmdef`, R40 bridged the stale generated project surface without editing generated `.csproj` files, R41 records all root `Hecton8*.csproj` projects at `0 Warning(s)` / `0 Error(s)` under the final serial no-restore CLI compile surface, and R42 propagates that boundary into active reference docs that still carried the older May 13 missing-artifact-only line. It still does not claim Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, save/load correctness, WFC outpost restore correctness, locomotion correctness, PDA map correctness, or visual quality proof.

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

- Item/catalog data is real, not placeholder-only: `73` ItemData assets excluding `ItemCatalog.asset`, `73` unique catalog refs after R21, `41` recipe assets, and `27` resource-node templates.
- Recipe data is internally coherent in the static parse: `149` non-script recipe item refs resolve to current item assets, and no recipe ref points outside the catalog.
- Resource-node data had a hard break candidate at the R14 snapshot: `23 / 27` template harvest items had `worldPrefab: {fileID: 0}`. R19 reduced the current primary-harvest gap to `16 / 27`; R21 later reduced the current primary-harvest gap to `0 / 27` with existing pickup shells.
- `ResourceNode.TrySpawnLoot()` returns success early for template-driven extractor items, so legacy pooled loot is skipped. Incremental yield calls `PersistentWorldRegistry.TryRegisterDroppedItem(itemData, ...)`, and that path rejects null `worldPrefab`.
- Result after R21: the specific static catalog/worldPrefab blocker is closed, but runtime pickup emission is still unproven until Unity shows hydration, interaction, inventory, quest, and save/load behavior.
- At the R14 snapshot, copper was split across two ItemData assets with the same `stableId: Data_Copper`: root `Assets/_Project/Data/Items/Data_Copper.asset` was used by `ResourceNodeTemplate_CopperVein` / barter and was not in `ItemCatalog`; raw `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset` was cataloged and used by recipes. R19 moved the checked copper node/barter refs to the raw cataloged asset, and R21 changed `BarterBootstrapAuthoring` to load raw cataloged copper.
- PlayerInventory is substantial and load-bearing, not a toy list: native SOA mirrors, grid anchors, stack counts, condition, craft locks, genetics/quality, mass/volume/radiation caches, degradation, pressure crush, reactive chemistry, and save shadow state.
- Crafting/fabricator code is substantial: bounded native recipe evaluation, scan locks, power/scarcity hooks, physical output emission, and scene string evidence for `Forward_Fabricator`, `Trial_Fabricator`, `HectonFabricatorUI`, and starter resource nodes.
- Resource scarcity is runtime-installed by `EconomyRuntimeInstaller`, but authored scarcity directives are not proven populated. `ResourceDistributionDirector`, `ProceduralOreSpawner`, and `FluidPipeGraphRuntime` contain serious code, but static scans do not prove scene placement or bootstrap creation for the production route.

Action:

- Promoted this to `Docs/PROJECT_STATE_STATIC_XRAY.md` as the stable gameplay-economy addendum.
- Current verdict: the gameplay economy spine is real and the previously visible static resource-node catalog/worldPrefab hole is closed, but first-hour resource-loop readiness remains `PENDING VERIFICATION` until runtime route proof exists.

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
- At the R16 snapshot, `Player.prefab` `ToolLoadoutProvisioner` was enabled with `provisionInventoryOnStart=1`, `assignCoreLoadoutOnStart=1`, and `provisionConstructionMaterialsOnStart=1`. It could grant the full tool kit and starter material on Start. R18 later disabled/gated this path.
- At the R16 snapshot, the provisioner starter material was root `Data_Copper` GUID `84877e24023afe648a6682f49f11defa`, the non-catalog copper asset already flagged in R14. R18 moved the provisioner reference to cataloged raw copper.
- `WorldShippingContentFilter` suppresses named trial/staging scene hierarchies. Static source did not show a strip path for player-attached `ToolLoadoutProvisioner` or smoke components.
- `Player.prefab` `PlayerPDA` has null `pdaPanel`, null `pdaCanvasGroup`, and null tab refs. `PlayerPDA.Open()` can still switch input/cursor/depth-of-field/events with no panel/tabs configured.
- Binary scene string scans found PDA tab components in `02_HECTON_WORLD.unity` and `03_HECTON_WORLD_CREST5.unity`, so the correct finding is not "no PDA UI assets".
- `DiegeticPDAController.cs` is the source bridge that calls `PlayerPDA.ConfigureUI(...)`, but its class string and MonoScript GUID `8f05da9f4a7a4158a04d6cc0e0f9d8c2` were not found in `_Project` scenes/prefabs.
- `PDARuntimeInstaller` and `ProgressionRuntimeInstaller` add PDA/logbook/progression systems, but they do not add `DiegeticPDAController`.

Action:

- Promoted this to `Docs/PROJECT_STATE_STATIC_XRAY.md` as the stable tools/PDA/first-hour interface addendum.
- Current verdict at R16: tool/scan/interaction architecture is real, but first-hour truth was contaminated by startup dev provisioning and PDA bridge proof remained `PENDING VERIFICATION`.
- R18 later hardens the provisioning defect by disabling startup grants, release-guarding the dev helper, and switching starter copper to cataloged raw `Data_Copper`.
- Required later runtime route: clean start with no dev all-tools grant -> acquire/craft/equip scanner -> open visible PDA shell -> scan resource/copper -> quest/log/inventory state updates.

## Continuation R17 - 2026-05-13

Rendering / visor / shader performance-boundary findings:

- Static shader inventory under `Assets/_Project`: `136` shader-like files (`101` `.shader`, `31` `.compute`, `4` `.hlsl`), `191` `#pragma multi_compile` lines, `13` `#pragma shader_feature` lines, and `66` `numthreads` declarations.
- `Mobile_Renderer.asset` currently has `8` active / `2` inactive features, `PC_Renderer.asset` has `8` active / `5` inactive features, and `PC_High_Renderer.asset` has `10` active / `2` inactive features.
- URP assets have SRP Batcher enabled, but GPU Resident Drawer and GPU occlusion are disabled across the scanned tier assets (`m_GPUResidentDrawerMode: 0`, `m_GPUResidentDrawerEnableOcclusionCullingInCameras: 0`). Do not claim GRD/GPU occlusion savings.
- `21` first-party visor `ScriptableRendererFeature` files implement `RecordRenderGraph`; `16` still use `AddUnsafePass`, `4` use `AddComputePass`, and `1` uses obsolete `AddRenderPass<T>`.
- Active renderer YAML keeps the low/mobile path visually ambitious: VR brownout, scooter volumetric shafts, half-res particles, abyssal SSDO, Shapes, noir depth fog, visor uber post, and atmosphere soot are active on `Mobile_Renderer.asset`.
- `HectonScooterVolumetricShaftsFeature` is fake-first in source: the settings tooltip says shaft generation performs zero world raymarch steps and the material upload sends `_HectonShaftRaymarchSteps = 0`; the serialized `raymarchSteps: 8` value in renderer YAML is legacy/compat state, not runtime proof of world volumetric raymarching.
- `ScreenSpaceLightShaftRuntime`, `GroundPenetratingRadarRuntime`, and `InstanceCullingService` contain bounded source architecture and 300-frame black-box telemetry rings, but GUID scans did not prove those components serialized in `_Project` scenes/prefabs/assets.
- `GroundPenetratingRadarRuntime` caps the fake sensor route at `64` max rays, `16` low-tier rays, `10` raymarch steps, `128` pings, and renders pings through `Graphics.RenderMeshIndirect` plus `Hecton_GroundRadarPingIndirect.shader`.

Action:

- Promoted the renderer/visor/shader boundary to `Docs/PROJECT_STATE_STATIC_XRAY.md`, `Docs/README.md`, `Docs/Reports/README.md`, `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, and `Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md`.
- Current verdict: render architecture is substantial and fake-first in important places, but low-tier visual cost, RenderGraph optimality, GPU occlusion, shader variant pressure, and scene wiring remain `PENDING VERIFICATION`.
- Required later proof: Frame Debugger / RenderGraph Viewer for active renderer features, Profiler GPU+CPU capture on Low/MX350 target, Memory Profiler VRAM snapshot, shader variant report, and a player-route visual capture for visor/noir/sonar/GPR/light-shaft states.

## Continuation R18 - 2026-05-13

First-hour dev provisioning hardening:

- `Player.prefab` `ToolLoadoutProvisioner` startup flags are now `0`: `provisionInventoryOnStart`, `assignCoreLoadoutOnStart`, and `provisionConstructionMaterialsOnStart`.
- `ToolLoadoutProvisioner` default `provisionConstructionMaterialsOnStart` is now `false`.
- `ToolLoadoutProvisioner` now gates provisioning, construction-material grants, quick-slot assignment, and startup preset application behind `UNITY_EDITOR || DEVELOPMENT_BUILD`; non-development release builds return without mutating inventory/loadout.
- Provisioner starter copper now resolves to cataloged raw `Data_Copper` GUID `7a9f752461931354e865d30b319c0f35` instead of root non-catalog GUID `84877e24023afe648a6682f49f11defa`.

Action:

- Patched `Assets/_Project/Scripts/ToolLoadoutProvisioner.cs`.
- Patched `Assets/_Project/Prefabs/Player.prefab`.
- Updated the project-state X-Ray and documentation indexes so the R16 risk does not remain current after the hardening.

Remaining boundary:

- This is source/prefab static proof only. Unity import, Play Mode, profiler, player build, clean first-hour route, and PDA shell proof remain `PENDING VERIFICATION`.

## Continuation R19 - 2026-05-13

Resource pickup data canonicalization:

- `ResourceNodeTemplate_CopperVein` harvest output now references cataloged raw `Data_Copper` GUID `7a9f752461931354e865d30b319c0f35`, not root non-catalog GUID `84877e24023afe648a6682f49f11defa`.
- `Offer_Illumination`, `Offer_RelayStarter`, and `Offer_RepairLoop` copper costs now also reference cataloged raw copper.
- Existing matching pickup prefabs are now wired into `ItemData.worldPrefab` for `Data_Copper`, `Data_FiberKelp`, `Data_HydrocarbonResin`, `Data_MembraneTissue`, `Data_SilicaShards`, and `Data_SilverOre`.
- Together with already-wired `Data_TitaniumScrap` and `Data_SulfurClumps`, the obvious early raw-resource pickup prefab pairs now have non-null `worldPrefab` refs.
- Static harvest-ref recount at the R19 checkpoint: `16 / 27` primary harvest items still had null `worldPrefab`, and `3 / 27` still pointed at non-catalog ItemData. R21 later closed those static catalog/worldPrefab gaps.

Action:

- Patched six raw resource ItemData assets, `ResourceNodeTemplate_CopperVein`, and three barter offers.
- Updated stable docs and indexes so R14's copper/worldPrefab finding is no longer treated as fully current.

Remaining boundary:

- This is static YAML/GUID proof only. Unity import, Play Mode pickup, `InteractionEvents.ItemCollected`, inventory acceptance, quest update, save/load, profiler, and player-build proof remain `PENDING VERIFICATION`.

## Continuation R20 - 2026-05-13

Resource content validator hardening:

- `ContentSanityValidator.ValidateResourceNodeTemplates()` now loads the active `ItemCatalog` and inspects serialized `ResourceNodeTemplate.harvestYield` / `rarityDrops`.
- Resource-node yield entries now error if the item ref is null, not `ItemData`, has empty `PersistentId`, is not the active `ItemCatalog` entry for its hash, has null `ItemData.worldPrefab`, or has a world prefab with no valid asset path.
- The validator summary now includes `ResourceNodeYieldMissingWorldPrefab` and `ResourceNodeYieldNotCataloged` counters.

Action:

- Patched `Assets/_Project/Scripts/Editor/ContentSanityValidator.cs`.
- Promoted the validator boundary into stable docs so resource gaps are enforced by tooling, not manual memory. R21 later expanded this contract check and closed the then-remaining static catalog/worldPrefab gaps.

Remaining boundary:

- This is static source proof only. The Unity editor validator menu was not run, no Console output was captured, and no compile/import proof exists in this pass.

## Continuation R21 - 2026-05-13

Resource pickup route closure:

- Added `Data_CarbonGraphite`, `Data_PressureDiamond`, and `Data_VoidGlassMeteorite` to `ItemCatalog`. Current non-catalog ItemData count under `Data/Items` is now `1`: legacy root `Data_Copper.asset`.
- Assigned existing pickup shells to the remaining resource-node harvest ItemData. Current resource-node primary harvest recount: `0 / 27` missing `worldPrefab`, `0 / 27` non-catalog.
- `BarterBootstrapAuthoring` now loads `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset`, not root `Assets/_Project/Data/Items/Data_Copper.asset`.
- `ItemCatalog` now falls back to direct serialized `ItemData.worldPrefab` when Addressables world-prefab lookup has no usable entry or failed load result. This is required by current static reality: `com.unity.addressables` exists, but `Assets/AddressableAssetsData` is absent.
- `ContentSanityValidator` now also validates resource-yield world-prefab contract: `PickupItem` or `HectonItem`, plus `Collider` and `Rigidbody`.

Action:

- Patched `ItemCatalog.asset`, ten raw resource ItemData assets, `ItemCatalog.cs`, `BarterBootstrapAuthoring.cs`, and `ContentSanityValidator.cs`.
- Promoted the current `0 / 27` catalog/worldPrefab state into stable docs.

Remaining boundary:

- This is still static proof only. Unity import, Addressables catalog behavior, ObjectPool hydration, pickup interaction, inventory acceptance, quest completion, save/load, profiler, and player-build proof remain `PENDING VERIFICATION`.

## Continuation R22 - 2026-05-13

PDA headless open guard:

- `PlayerPDA.Open()` now fails closed unless a PDA panel and at least one tab are resolved.
- PDA input-map switching now guards missing/uninitialized `GlobalRegistry.Input`.
- `ContentSanityValidator` now validates `Player.prefab` for headless PDA shell risk and reports `PlayerPdaHeadlessOpenRisk` plus bridge warnings.
- The guard returns before setting `IsOpen`, switching input, touching cursor state, requesting depth of field, playing open audio, or raising opened events when the physical shell is missing.
- This is static source proof only; visible PDA shell and `DiegeticPDAController` scene/prefab route remain `PENDING VERIFICATION`.

## Continuation R23 - 2026-05-13

Item identity / catalog validator hardening:

- Active DOC_AUDIT state files had been archived under `Docs/Archive/Batch004/`; active R22/R23 status/rationale/log were recreated with Batch004 as historical memory.
- Static YAML scan found exactly one duplicate `ItemData.PersistentId` group under `Assets/_Project/Data`: root `Assets/_Project/Data/Items/Data_Copper.asset` and cataloged raw `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset` both author `Data_Copper`.
- `ContentSanityValidator` now reports duplicate `ItemData.PersistentId` across data assets.
- `ContentSanityValidator` now validates `ItemCatalog.allItems` for null entries, duplicate hash / `PersistentId` entries, missing runtime descriptors, and `ItemCatalog.HasLookupAmbiguity`.
- The validator summary now includes `ItemDataDuplicatePersistentId`, `ItemCatalogNullEntries`, `ItemCatalogDuplicateHashes`, `ItemCatalogMissingRuntimeDescriptors`, and `ItemCatalogLookupAmbiguities`.

Action:

- Verified current `Assets/_Project/Scripts/Editor/ContentSanityValidator.cs` contains the item/catalog identity gates.
- Promoted the R23 validator boundary into stable docs.

Remaining boundary:

- Unity MCP `validate_script` returned `0` diagnostics for `ContentSanityValidator.cs`. This is not Unity menu execution or import proof.
- `dotnet build Hecton8.Editor.csproj` restored packages but remains blocked by existing `Hecton8.Core` missing namespace/type errors before useful editor-validator proof.
- The Unity editor validator menu was not run, no Console output was captured, and no import/player route proof exists in this pass.

## Continuation R24 - 2026-05-13

Tool route / LogicSpanner validator hardening:

- Static metadata reference recount found `13` `ToolMetadata_*.asset` files and `12` held tool prefabs.
- `12 / 13` tool metadata assets are referenced by held prefabs. `ToolMetadata_LogicSpanner.asset` has only its own asset/meta reference route, matching the earlier orphan-content finding.
- `ContentSanityValidator` now validates held `PlayerTool` prefabs for non-null `ToolMetadata`, non-null `ItemData`, `ItemCategory.Tool`, valid `ItemCatalog` runtime descriptor, and non-null tool `worldPrefab`.
- `ContentSanityValidator` now validates active `ToolMetadata` assets for duplicate/empty `toolID` and reports active metadata with no held prefab route as orphan gameplay content.
- The validator summary now includes `ToolMetadata`, `ToolHeldPrefabs`, `ToolMetadataOrphans`, and `ToolRouteErrors`.

Remaining boundary:

- This is static source/documentation proof only. The Unity validator menu was not run, no Console output was captured, and no runtime tool acquisition/equip/drop proof exists in this pass.
- Expected current validator behavior from static evidence: `ToolMetadataOrphans=1` for `ToolMetadata_LogicSpanner.asset` until the full item/prefab/catalog/recipe route is authored or the metadata is quarantined outside active data.

## Continuation R25 - 2026-05-13

Player dev provisioner startup regression gate:

- Static prefab YAML still shows canonical `Player.prefab` `ToolLoadoutProvisioner` startup grant flags disabled: `provisionInventoryOnStart: 0`, `assignCoreLoadoutOnStart: 0`, `provisionConstructionMaterialsOnStart: 0`, and `startupPreset: {fileID: 0}`.
- `ContentSanityValidator` now validates canonical `Player.prefab` `ToolLoadoutProvisioner` startup flags.
- The validator summary now includes `PlayerDevProvisionerStartupRisk`.
- Expected current validator behavior from static evidence: `PlayerDevProvisionerStartupRisk=0`; if any startup grant flag is re-enabled, the validator should error.

Remaining boundary:

- This is static source/prefab proof only. The Unity validator menu was not run, no Console output was captured, and no clean first-hour no-dev-grant runtime route was executed.

## Continuation R26 - 2026-05-13

Quest item / prerequisite route validator:

- Static first-hour quest scan found `Data_TitaniumScrap`, `Item_Tool_Scanner`, and cataloged raw `Data_Copper` present in `ItemCatalog`.
- The legacy root `Data_Copper` remains outside `ItemCatalog`; this is still covered by R23 duplicate identity validation and should not be treated as current first-hour copper authority.
- Checked first-hour prerequisites resolve to existing quest IDs: `quest_first_hour_exit_lifepod` and `quest_first_hour_collect_titanium`.
- `QuestGraphEvaluator` already consumes `InteractionEvents.ItemCollected` and `CraftingEvents.CraftCompleted`; the new hardening is authored data validation, not runtime route proof.
- `ContentSanityValidator` now validates `QuestData.questId` uniqueness, `prerequisiteQuestIds`, item/craft `triggerId`, item/craft `completionId`, and non-empty `criticalItemId` against active quest/catalog data.
- The validator summary now includes `Quests` and `QuestRouteErrors`.

Remaining boundary:

- This is static source/data proof only. The Unity validator menu was not run, no Console output was captured, and no pickup/craft/quest/PDA/save-load route was executed.

## Continuation R27 - 2026-05-13

Recipe / craft completion route validator:

- Static recipe scan found `41` `RecipeData` assets under `Assets/_Project/Data/Crafting/Recipes`.
- `Recipe_Scanner.asset` outputs `Item_Tool_Scanner` and has two authored ingredient entries. This is static YAML evidence only; it is not proof that the player can gather the ingredients, open a fabricator, craft, receive the item, complete the quest, see PDA feedback, or survive save/load.
- `ContentSanityValidator` now validates `RecipeData` runtime hash uniqueness, result item catalog descriptors, positive result quantities, explicit fabrication groups, non-empty ingredient lists, positive ingredient amounts, and ingredient catalog descriptors.
- `ContentSanityValidator` now cross-checks `QuestData.OnCraftCompleted` trigger/completion IDs against valid recipe result persistent IDs.
- The validator summary now includes `Recipes` and `RecipeRouteErrors`.

Remaining boundary:

- This is static source/data proof only. The Unity validator menu was not run, no Console output was captured, and no fabricator UI/craft completion/PDA/save-load route was executed.

## Continuation R28 - 2026-05-13

Recipe scan-gate route warning:

- Static scan found `scan.resource_node` has a visible generic runtime source in `ScanLogSystem` / `ScannerTool`.
- `scan.expedition_contact`, `scan.resource_cache`, and `scan.structure_relay` are currently visible in recipe assets and editor authoring scripts, but no current `_Project` prefab/scene/data route was found by static grep.
- `ConstructionBootstrapAuthoring` source can create proving-ground `ScannableTarget` probes for those IDs, but editor authoring capability is not production scene/prefab unlock proof.
- `ContentSanityValidator` now collects known generic scan IDs and authored `ScannableTarget` prefab entry IDs under `Assets/_Project/Prefabs`.
- `ContentSanityValidator` now warns when `RecipeData.requiredScanEntryId` has no known prefab/generic route. The validator summary now includes `RecipeScanGateWarnings`.

Remaining boundary:

- This is static source/data proof only. The Unity validator menu was not run, no Console output was captured, and no scan interaction, recipe unlock, fabricator UI, craft completion, PDA, or save-load route was executed.

## Continuation R29 - 2026-05-13

Unity compile / async world pager reconciliation:

- Current stale `dotnet build Hecton8.Core.csproj --no-restore` remains non-authoritative for this workspace: it failed with `154` missing namespace/type errors from generated `.csproj` reference drift across split asmdefs.
- `H8BinaryWorldPager` was kept in a safe public C# surface: public write/copy methods are no longer unsafe-call sites, while internal NativeArray copy/header serialization remains inside unsafe blocks.
- `SaveManager` now owns an `IAsyncPersistenceService` world-pager bridge for chunk page writes, reads, completed-read copy, telemetry, and flush.
- Duplicate `SaveManager` dehydration-drain and pager-saving-notification methods were collapsed to one bounded route. The retained route drains at most `2` chunk dehydration signals per tick and writes voxel delta, inventory shadow, and chunk metadata payloads.
- Fresh Unity Console before the final batch run showed no current C# compile wall, but did expose a runtime bootstrap fault: `IOException: Sharing violation on path ... world_data.h8bin`, followed by `SaveManager` CoreServices failure and `BIOS ERROR 0xBOOT_TIMEOUT`.
- `H8BinaryWorldPager.Initialize()` now fail-closes on `IOException` / `UnauthorizedAccessException`: it records an initialization fault, increments IO telemetry, emits a development warning, and leaves read/write APIs rejected instead of throwing through bootstrap.
- `SaveManager` checks `HasInitializationFault` before reinitializing the pager, avoiding per-frame retry/log spam after a locked file.
- Generated `Library/BurstCache` was deleted after a Burst hash-cache exception; Unity `6000.4.1f1` batchmode import/script compilation was run with `Library/Codex_DOC_AUDIT_UnityBatchCompile.log`.
- Strict scan of that batch log found no `error CS`, no bootstrap dependency exception, and no `BIOS ERROR`; the log includes script compilation requests, `DisplayProgressbar: Compiling Scripts`, `Application.AssetDatabase Initial Refresh End`, and `Exiting batchmode successfully`.
- A later read-only Unity MCP Console readback first returned `0` errors and `7` warnings from ADB/Crest/MCP bridge/serializer surfaces; final read-only Console readback returned `0` log entries.

Remaining boundary:

- This is compile/import and source-route evidence only. It is not Play Mode, not a save/load roundtrip, not corrupted-sector recovery, not backup recovery, not profiler/GC/memory proof, not player build proof, and not gameplay route proof.

## Continuation R30 - 2026-05-13

Async world pager static X-Ray / overclaim correction:

- R29's compile-clean pager state was not sufficient persistence correctness. Static review found `world_data.h8bin` still opened with `FileShare.ReadWrite`, which allowed concurrent page-file writers.
- `H8BinaryWorldPager` now opens with `FileShare.Read`, so diagnostic readers are allowed but a second writer should fail closed instead of silently corrupting fixed page slots.
- The pager worker now has a bounded shutdown handshake through `_workerStopLock`, `Monitor.Wait`, and `Monitor.PulseAll` before native queue/arena disposal.
- Invalid ready read results now release their result map entry/read slot instead of poisoning the fixed read-result capacity.
- Empty sparse page headers and valid headers for a different sector/payload are now classified as `Missing`, not `Corrupt`; this prevents false corruption dumps for unwritten random-access sectors and fixed-slot hash collisions.
- `SaveManager.EnqueueChunkDehydrationPayloads()` no longer captures the full global `VoxelDeltaProcessor` native snapshot for every dehydrated chunk. The current sidecar dehydration route writes inventory shadow plus chunk metadata only.
- `WorldChunkResidencyManager.RequestLoad()` no longer enqueues orphaned `VoxelDeltaRle` pager prefetch requests. Ticket retirement exists, but there is still no chunk-local voxel payload apply path, so voxel chunk hydration remains unproven.

Remaining boundary:

- This is static source correction only. No Unity import, Play Mode, save/load roundtrip, backup recovery, corrupted-page recovery, profiler, GCMonitor, Memory Profiler, player build, or frame-time proof was run in R30.

## Continuation R31 - 2026-05-13

SaveManager world pager cold-boot trim / regression guard:

- A post-R30 static check found `worldPagerVoxelDeltaSnapshot` had reappeared in `SaveManager.EnqueueChunkDehydrationPayloads()` under concurrent workspace churn.
- The returned block again captured the global `VoxelDeltaProcessor` snapshot per dehydrated chunk and attempted a `VoxelDeltaRle` sidecar write. It has been removed again.
- Current dehydration sidecar writes are inventory shadow plus chunk metadata only. There is still no claim of chunk-local voxel persistence.
- `InitializeNativeBuffers()` no longer calls `EnsureWorldPagerInitialized()`. `world_data.h8bin` is opened only when chunk sidecar IO is actually requested, not during `SaveManager.Awake()` / `InitializeService()` native-buffer allocation.
- At the R31 boundary this still did not fix the main raw/compressed/staging save-buffer boot residency; R32 below moves those large buffers to first-use allocation. Memory Profiler/runtime proof is still required.

Remaining boundary:

- Static source correction only. No Unity import, Play Mode, save/load roundtrip, backup recovery, corrupted-page recovery, profiler, GCMonitor, Memory Profiler, player build, or frame-time proof was run in R31.

## Continuation R32 - 2026-05-13

SaveManager large buffer lazy allocation:

- Static source review showed `SaveManager.Awake()` / `InitializeService()` still allocated the main persistence working set at boot: 64 MB raw save payload, about 68 MB compressed payload, and 10 MB staging.
- `InitializeNativeBuffers()` now initializes only the save black-box telemetry ring and the `9`-entry load-candidate scratch.
- Full save calls `EnsureSaveWorkingBuffers()` before snapshot/write pipeline work.
- Load calls `EnsureSavePayloadBuffer()` and `EnsureLoadCandidateScratch()` before marking the service busy.
- Chunk dehydration calls `EnsureSaveStagingBuffer()` before inventory/metadata sidecar writes.
- The large buffers remain persistent after first use. R32 is a cold-boot residency fix, not proof that after-first-use memory policy is final.

Remaining boundary:

- Static source correction only. No Unity import, Play Mode, save/load roundtrip, backup recovery, corrupted-page recovery, profiler, GCMonitor, Memory Profiler, player build, or frame-time proof was run in R32.

## Continuation R33 - 2026-05-13

SaveManager fault-path allocation guard:

- Static review after R32 found chunk dehydration could allocate the 10 MB staging arena even when pager initialization had faulted and writes would reject.
- `EnqueueChunkDehydrationPayloads()` now returns before staging allocation unless `_worldPager` exists, `IsInitialized` is true, and `HasInitializationFault` is false.
- `LoadGameAsync()` now performs first-use raw-buffer/candidate-scratch allocation inside the load `try`; `candidates` starts as default, and the existing clear helper handles default arrays.
- This reduces avoidable fault-path residency and keeps low-memory load allocation failures inside the normal failure/cleanup path.

Remaining boundary:

- Static source correction only. No Unity import, Play Mode, save/load roundtrip, backup recovery, corrupted-page recovery, profiler, GCMonitor, Memory Profiler, player build, or frame-time proof was run in R33.

## Continuation R34 - 2026-05-13

HectonPlayerMovement ladder snap hot-path cache:

- Current `HectonPlayerMovement.cs` is 740,426 bytes / 13,240 lines. The older large-file numbers in stable docs were stale.
- Static review confirms this is a fused player integration hub: locomotion, KCC, water, transport, hazards, camera, AUP repair, probes, and telemetry.
- A narrow fixed locomotion issue was found: ladder spline snap resolved `ClimbableLadder` from the recent ladder probe collider via `TryGetComponent`.
- The method now caches positive ladder component resolution by collider instance id and clears stale cache on failed resolution.
- This is not a broad decomposition and does not claim player movement runtime correctness.

Remaining boundary:

- Static source correction only. No Unity import, Play Mode, ladder interaction, profiler, GCMonitor, player build, or frame-time proof was run in R34.

## Continuation R35 - 2026-05-13

HLOD PDA overlay upload version gate:

- Static review of the `WORLD_STREAMING_LOD_MANAGER` handoff found `PDAMapTab.TryResolveHlodImpostorAupBuffer()` uploaded the fixed `16`-point HLOD AUP buffer every map build while active impostor points existed.
- `IStreamingBackpressureService` now exposes `ActiveImpostorVersion`.
- `WorldChunkResidencyManager` now keeps a separate point/read-model version for PDA HLOD points, distinct from the renderer matrix version.
- `PDAMapTab` now caches uploaded HLOD version/count, clamps count to the native point array length, clears trailing fixed slots only when the read model changes, and skips the HLOD buffer upload when version/count are unchanged.
- Fade progress advances the point version without forcing renderer matrix uploads.

Remaining boundary:

- Static source correction only. No Unity import, Play Mode, PDA map route, profiler, GCMonitor, Frame Debugger, player build, or frame-time proof was run in R35.

## Continuation R36 - 2026-05-13

Recurrent world-pager voxel snapshot regression guard:

- Post-R35 regression grep found `worldPagerVoxelDeltaSnapshot` back in `SaveManager.EnqueueChunkDehydrationPayloads()`.
- The reintroduced block captured a global `VoxelDeltaProcessor` snapshot for each dehydrated chunk and wrote it as `H8WorldPagePayloadTypes.VoxelDeltaRle`.
- The block was removed again. Chunk dehydration sidecar writes are limited to inventory shadow and chunk metadata until a real chunk-local voxel capture/apply contract exists.
- Scoped grep now finds no `FileShare.ReadWrite`, no `worldPagerVoxelDeltaSnapshot`, and no direct `RequestAsyncPagerRead(chunkId)` in `H8BinaryWorldPager.cs`, `SaveManager.cs`, or `WorldChunkResidencyManager.cs`.

Remaining boundary:

- Static source correction only. No Unity import, Play Mode, save/load roundtrip, corrupted-page recovery, profiler, GCMonitor, Memory Profiler, player build, or frame-time proof was run in R36.

## Continuation R37 - 2026-05-13

Unity C# wall reconciliation / pager thread guard:

- Post-R36 recheck found `H8BinaryWorldPager` had reintroduced `async void RunWorkerAsync()` / `Awaitable.BackgroundThreadAsync()` under concurrent churn.
- `H8BinaryWorldPager` now owns a named background `Thread` through `_workerThread`, uses `RunWorkerLoop()`, and joins before monitor fallback during shutdown.
- `GlobalDataVault` stays inside the `Hecton8.Core.Memory` asmdef boundary: current scoped grep finds no Burst attribute, Unity.Mathematics dependency, GlobalSignals dependency, or MemoryAddressShiftSignal dependency.
- Local Unity Bee/Roslyn temp-output probes returned exit code `0` for `Hecton8.Core.Memory` and `Hecton8.Core`.
- Scoped grep also finds no `FileShare.ReadWrite`, no `worldPagerVoxelDeltaSnapshot`, and no direct `RequestAsyncPagerRead(chunkId)` in the pager integration files.

Remaining boundary:

- Unity MCP `read_console` returned `Unity session not available`. This is local compile/source evidence only; no Unity import, Play Mode, save/load roundtrip, corrupted-page recovery, profiler, GCMonitor, Memory Profiler, player build, or frame-time proof was run in R37.

## Continuation R38 - 2026-05-13

World pager worker fault accounting / WFC outpost persistence contract:

- Active `DOC_AUDIT` status/rationale/log files were recreated after Batch005 moved the previous active files into `Docs/Archive/Batch005/`.
- `H8BinaryWorldPager.RunWorkerLoop()` now processes dequeued write/read commands through per-command accounting wrappers.
- Unexpected command-level faults now decrement the already-dequeued pending counter in `finally`, record fault telemetry, mark the pager fail-closed, zero exposed pending write/read counters, request worker shutdown, and dump the existing black-box telemetry.
- `IAsyncPersistenceService` WFC outpost methods are now implemented by the current `SaveManager`: mutable WFC cell flags are held in DataVault `BufferID.WfcOutpostGrid`, packed by `PackWfcOutpostMutableStateJob`, encoded through the existing `SaveBinaryPayloadCodec` bitmask payload, deduplicated by one-sector packed hash, committed through `IMacroDatabaseService.MarkDirty`, and restored from `MacroDatabasePayloadHandle` through `TryGetPayload`.
- Local probes: `Hecton8.Core.Contracts` rebuilt from the current `Assets/_Project/Scripts/Core/Contracts/*.cs` source list with exit code `0`; `Hecton8.Core.Memory` probe exit code `0`; temporary `Hecton8.Audio.Virtualization.*`, `Hecton8.World.Contracts`, `Hecton8.AI.Cognition`, and `Hecton8.Animation.IK` probes exit `0`. The manual `Hecton8.World.Contracts` probe emitted type-conflict warnings because the stale base response still referenced the old World.Contracts ref while compiling current World.Contracts sources.
- Full local `Hecton8.Core` Bee/Roslyn probe remains blocked by unrelated active churn in `SpatialAudioManager.cs`, `ScannerTool.cs`, `SubmarineAutoLevelBallastController.cs`, `HectonArenaAllocator.cs`, `HectonFluidEngine.cs`, `UI/SuitHUDV4CanvasOverlay.cs`, `UI/InteractionUI.cs`, and `FaunaBrain.cs`. The current error set is not caused by the R38 persistence files, but it means there is no current full Core compile-success claim.
- Unity MCP `read_console` again returned `Unity session not available`.

Remaining boundary:

- R38 is source/local-probe evidence only. No Unity import, Play Mode, save/load roundtrip, WFC outpost MacroDB restore route, corrupted-page recovery, profiler, GCMonitor, Memory Profiler, player build, or frame-time proof was run.

## Continuation R39 - 2026-05-13

Generated project / asmdef drift tripwire:

- Fresh `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` now fails on generated project reference drift before the old R38 line-level blocker list is meaningful.
- `Assets/_Project/Scripts/Hecton8.Core.asmdef` already references the relevant first-party assemblies. The current generated `Hecton8.Core.csproj` does not contain `23` of those first-party references.
- Missing generated references found by live comparison: `Hecton8.AI.Cognition`, `Hecton8.AI.Ecology.Migration`, `Hecton8.Animation.IK`, `Hecton8.Audio.Echolocation`, `Hecton8.Audio.Propagation`, `Hecton8.Audio.Virtualization`, `Hecton8.Audio.Virtualization.Contracts`, `Hecton8.Core.Bucketing`, `Hecton8.Core.Database`, `Hecton8.Core.Persistence.Paging`, `Hecton8.Core.Scheduling`, `Hecton8.Environment.Fluids`, `Hecton8.Environment.Fluids.Contracts`, `Hecton8.Inventory.Algorithms`, `Hecton8.Inventory.Corrosion`, `Hecton8.Inventory.Corrosion.Contracts`, `Hecton8.Physics.CCD`, `Hecton8.Physics.Tethers.Contracts`, `Hecton8.SpaceEngine098Terrain`, `Hecton8.UI.Diegetic.Contracts`, `Hecton8.Vehicles.Physics.Contracts`, `Hecton8.World.GPR`, and `Hecton8.World.Terrain`.
- `HectonComplianceValidator` now has an editor-only `CSPROJ001` validation step that compares `Hecton8.Core.asmdef` against the generated `Hecton8.Core.csproj` and reports missing generated references before external `dotnet build` output is accepted as source evidence.
- `git diff --check` on the validator is clean except LF/CRLF conversion warning. `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal -p:BuildProjectReferences=false -clp:ErrorsOnly` is blocked before syntax proof by missing `Temp/bin/Debug/Hecton8.Core.dll`. Unity MCP `read_console` fails at `127.0.0.1:8088/mcp`.

Remaining boundary:

- R39 is editor/source validation only. Unity MCP Console is unavailable, Unity menu validation was not run, and the generated project files were not regenerated in this pass.

## Continuation R40 - 2026-05-14

Source-backed MSBuild bridge / Core CLI compile recovery:

- Non-destructive Unity batchmode project-refresh attempt: `Unity.exe -batchmode -quit -nographics -projectPath C:\hades\Hecton8 -logFile Library\Codex_DOC_AUDIT_ProjectRefresh_R40.log`. The log connected licensing and set the project path, but terminated early and did not regenerate the stale root `.csproj` files.
- Current Bee response files expose newer source truth than the root generated projects: `Hecton8.Core.rsp` includes current Core-side files missing from `Hecton8.Core.csproj`, and `Hecton8.World.Contracts.rsp` includes contract files missing from `Hecton8.World.Contracts.csproj`.
- `Directory.Build.targets` now bridges the stale generated project surface without directly editing generated `.csproj` files. It adds missing source includes and existing first-party `Library/ScriptAssemblies` references for `Hecton8.Core`, plus missing contract source includes for `Hecton8.World.Contracts`.
- `PlayerLookTargetPromptCache` is restored as a real fixed cache under `Hecton8.Core`; the previous file content was only an empty comment claiming the type lived in `GlobalSignals.cs`, while current source search found no such type there.
- One first-party warning exposed after dependency rebuild, `PlayerCriticalProceduralAudioRenderer.PrologueSplashdownSineSweepProbeJob.NormalizedTime`, was removed by deleting an unused private probe job. The active splashdown audio path remains `RenderPrologueSplashdownSample()`.
- Controlled serial CLI compile now passes: `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -v:minimal -clp:Summary` -> `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.
- Controlled serial CLI compile for `Hecton8.World.Contracts.csproj` now also passes with `0 Warning(s)` and `0 Error(s)`.
- Unity MCP `read_console` was rechecked after the CLI compile pass and still fails at `http://127.0.0.1:8088/mcp`.

Remaining boundary:

- R40 is `CLI_COMPILE` plus `STATIC_SOURCE` evidence only. It is not Unity import, Unity Console, Play Mode, save/load route proof, WFC runtime proof, profiler, GCMonitor, Memory Profiler, player build, frame-time proof, or scene/prefab wiring proof.

## Continuation R41 - 2026-05-14

Root `Hecton8*.csproj` compile sweep:

- Initial `--no-restore` attempts for `Hecton8.Editor.csproj`, `Hecton8.PlayModeTests.csproj`, and `Hecton8.World.Dots.csproj` failed on missing `Temp\obj\...\project.assets.json`. This was restore-state evidence, not C# source failure.
- Serial restore/build recreated the missing MSBuild assets and referenced temp DLLs. The Editor restore/build reached `0 Error(s)` with one vendor warning in `Assets\MapMagic\Tools\Extensions\Texture2DExtensions.cs`; PlayModeTests and World.Dots restore/builds reached `0 Warning(s)` / `0 Error(s)`.
- A later post-doc sanity rerun showed the same restore-state volatility can recur for `Hecton8.Core.csproj`: first no-restore pass failed on missing `Temp\obj\Hecton8.Core\project.assets.json`, then serial restore/build recreated the assets. The full restore graph emitted vendor/package warnings from URP/GPUInstancer/Crest/ShaderGraph plus the MapMagic/Den.Tools warning; these are not first-party root no-restore warnings.
- Final serial no-restore compile with `-m:1 /nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -v:minimal -clp:Summary` returned `0 Warning(s)` / `0 Error(s)` for every root Hecton8 project: `Hecton8.Core.csproj`, `Hecton8.Editor.csproj`, `Hecton8.PlayModeTests.csproj`, `Hecton8.World.Contracts.csproj`, `Hecton8.World.Dots.csproj`, `Hecton8.Bootstrap.Contracts.csproj`, `Hecton8.Input.Generated.csproj`, and `Hecton8.Input.csproj`.
- The sweep was deliberately serial. Parallel Unity-generated project builds can create false evidence through shared `Temp\obj` lock/output races.
- Unity MCP `read_console` still fails at `http://127.0.0.1:8088/mcp`.

Remaining boundary:

- R41 is `CLI_COMPILE` evidence only. It is not Unity Console, Play Mode, Unity test execution, profiler, GCMonitor, Memory Profiler, player build, frame-time proof, scene/prefab wiring proof, or visual-quality proof.

## Continuation R42 - 2026-05-14

Active reference-doc override propagation:

- Scan scope: active non-archive/non-deprecated markdown, excluding dated report snapshots.
- Finding: `35` active reference docs still had the old one-line May 13 override that only demoted the missing May 11 compile artifact and kept runtime proof pending. Three additional Archivarius/architecture surfaces carried equivalent May 11 artifact wording.
- Action: `38` active markdown files were mechanically updated to say the May 11 artifact remains absent/stale, while May 14 R41 is the current external root `Hecton8*.csproj` no-restore CLI compile boundary at `0 Warning(s)` / `0 Error(s)` after restore assets exist.
- Boundary preserved: these docs still say full restore graphs can carry vendor/package warnings, and Unity Console, Play Mode, profiler, GCMonitor, player build, scene wiring, frame-time, memory, import, and visual quality remain `PENDING VERIFICATION`.

Remaining boundary:

- R42 is `STATIC_DOC` synchronization only. It adds no new runtime, Unity Console, Play Mode, profiler, GCMonitor, player-build, or visual proof.

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

CPU: R29 adds fail-closed persistence initialization and a bounded world-pager dehydration drain; R30/R31/R36/R37 remove the per-dehydrated-chunk global voxel snapshot write attempt and restore joinable pager worker ownership; R38 adds cold-path pager fault accounting and WFC outpost MacroDB bit packing without adding normal-frame work outside WFC signal drain/service calls; R39 adds editor-only generated-project reference validation and no runtime work; R40 adds a build-surface `Directory.Build.targets` bridge and a bounded prompt cache used by existing prompt call sites; R41 adds no runtime work and only reclassifies external compile evidence after a serial root-project sweep; R32 moves large save-buffer allocation from boot to first persistence use; R33 avoids staging allocation on pager fault path; R34 removes repeated ladder component resolution for the same collider in the fixed ladder-snap path; R35 skips unchanged PDA HLOD fixed-buffer uploads. No profiler measurement was captured.
GC: R30/R31/R36/R37 remove one potentially large cold-path native snapshot capture from chunk dehydration. R38 uses existing native staging/packed-word scratch for WFC outpost persistence and does not add managed hot-path allocations by source inspection. R40's restored prompt cache owns fixed cold arrays and copies caller-provided strings into fixed char storage; it does not allocate on the copy path by source inspection. R32 changes NativeArray allocation timing, not managed DTO save/load allocation behavior. No GCMonitor proof was captured.
Memory: normal pager initialization owns fixed native arenas/queues and telemetry, but R31 makes that initialization lazy instead of part of `InitializeNativeBuffers()`. R32 also makes the 64 MB raw, about 68 MB compressed, and 10 MB staging save buffers first-use allocations; R33 prevents the 10 MB staging allocation when the pager is faulted/uninitialized. Memory Profiler proof remains absent.
Cadence: chunk dehydration ingestion is capped at `2` signals/tick. R35 PDA HLOD uploads now follow point-version/count changes instead of every unchanged map build. R38 WFC outpost state signal drain is capped at `8` state-change signals/tick and `4` sector-hydration signals/tick by current source.
Correctness: documentation trust improved by demoting missing artifacts/stale navigation claims, recording the current Unity batch compile/import boundary, correcting the false chunk-local voxel pager readiness claim, recording that the R38 full Core probe was blocked by unrelated active churn, recording that the R39 first external Core blocker was generated `Hecton8.Core.csproj` drift against `Hecton8.Core.asmdef`, recording that R40 recovered controlled external `Hecton8.Core` / `Hecton8.World.Contracts` CLI compile through a source-backed bridge rather than generated-project edits, recording that R41 serially confirms the current root `Hecton8*.csproj` CLI compile surface is clean while Unity runtime/editor-console proof remains absent, and propagating that current boundary into active reference docs through R42.

## Non-Claims

- A Unity batchmode import/script-compilation run was performed for R29 only; it is not runtime proof.
- Unity MCP was used earlier in this continuation but the live MCP editor session disappeared; final runtime/editor verification is still unavailable. R40/R41 verification used controlled local `dotnet build` evidence only.
- No profiler/GC/runtime data was captured.
- No claim of PlayMode-clean Console, save/load correctness, player build, profiler-clean frame time, or working gameplay is made.

STATUS: PENDING VERIFICATION
