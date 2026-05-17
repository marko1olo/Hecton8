# LOG_SUBNAUTICA_RESEARCHER

## 2026-05-15 Research Session

What was wrong: User needs an evidence-based Subnautica reference extraction pass, but no active batch prompt/ID exists and installed game files are proprietary.
What was done: Initialized separate research status/rationale/log files. Final findings pending.
Cinematic Cheats used: Pending.
Exact Microseconds saved: Research-only; 0us measured. Future estimates require profiler proof.

## 2026-05-15 Foundation-Focused Subnautica Reference Audit

What was wrong:
- Original question could drift into asset/code copying. That is not useful for the current Hecton8 phase. User clarified the priority is codebase foundation.
- Hecton8 has many first-party systems, but the evidence says parts of the production foundation are not proven: Addressables data folder exists with 0 files, no built catalog evidence, and some runtime wiring is only indirectly visible through binary scene strings/editor validators.
- Previous broad statement �cannot borrow� was too coarse. Correct boundary: study everything legally observable; borrow architecture/patterns; do not copy proprietary assets/code/text/audio or GPL/AGPL code into a non-compatible codebase.

What was done:
- Inspected local Subnautica install at `C:\Games\Subnautica` as file taxonomy only. No Unity bundle extraction, no `Assembly-CSharp.dll` decompile, no asset/code/text copying.
- Inspected Hecton8 docs, data folders, scenes, key source owners, Addressables folder state, save/world/modding code surfaces, and editor validators.
- Researched public/current sources:
  - Unknown Worlds terrain format: https://unknownworlds.com/news/terrain-data-format
  - Nautilus modding API: https://github.com/SubnauticaModding/Nautilus
  - BepInEx.Subnautica pack: https://github.com/toebeann/BepInEx.Subnautica
  - Subnautica TerrainPatcher: https://github.com/Esper89/Subnautica-TerrainPatcher
  - Nitrox: https://github.com/SubnauticaNitrox/Nitrox
  - QModManager archive: https://github.com/SubnauticaModding/QModManager

Subnautica local evidence:
- Addressables payload: `Subnautica_Data\StreamingAssets\aa\StandaloneWindows64` has 5,467 `.bundle` files, about 4.675 GB total. `catalog.json` is about 12.0 MB and parses to `AddressablesMainContentCatalog`, 21,090 internal IDs, 4 provider IDs, 47 resource types.
- Bundle naming taxonomy shows production content split into scenes, prefab bundles, world meshes, discrete assets, precursor/base/wreck/fragment/PDA/vehicle/creature groups. This is a payload topology pattern, not borrowable content.
- Terrain/world cache: `SNUnmanagedData\Build18` has `CompiledOctreesCache` 5,416 files / 1,147.35 MB, `CellsCache` 1,606 files / 159.8 MB, `BatchObjectsCache` 2,975 files / 3.07 MB.
- Official Unknown Worlds terrain article states the core terrain is Dual Contouring over voxel data, 160m batches, 125 octrees per batch, 32m octrees, 1m voxel resolution, material plus signed-distance per voxel.
- Save slot layout: `slot0000` uses tiny `gameinfo.json` 345 bytes, `global-objects.bin`, `scene-objects.bin`, 25 zipped cell-cache files totaling 20.82 MB, screenshot sidecar, time capsule images. This proves split save topology, not monolithic world save.
- Audio topology: 20 `.bank` files, split by category such as music/player/creatures/env/vehicles/tools/interface. Hecton8 should borrow the category-streaming idea only; not FMOD files or bank format.
- Mod loader residue: `doorstop_config.ini` targets BepInEx, but no local `BepInEx` or `QMods` folder exists. Local install is not a reliable mod-source tree.

Hecton8 foundation evidence:
- `Assets\AddressableAssetsData` exists but contains 0 files. This is P0. A directory is not a content pipeline.
- `PlatformCompatibilityAudit.cs` currently marks Addressables project data as PASS if the directory exists, and separately says groups must be created before streaming readiness. This audit should be stricter.
- Key code exists and is large enough to be real architecture, not stubs:
  - `WorldChunkResidencyManager.cs`: 3,792 lines, Addressables-backed chunk residency, telemetry 300, memory guard, tiered load dispatch budgets.
  - `HectonVoxelEngine.cs`: 7,626 lines, voxel mesh/physics pipeline, black-box capacity, deferred collider upload, GlobalRegistry integration.
  - `GameBootstrapper.cs`: 4,593 lines, bootstrap graph, Addressables dependency prewarm hooks, registry service slots.
  - `H8BinaryWorldPager.cs`: 1,211 lines, fixed 256 KB sectors, 8,192 sectors, read/write queues 64, telemetry 300, corruption tracking.
  - `ModAssetManager.cs`: 442 lines, mod AssetBundle/raw fallback asset loading.
- H8 paging payload types already exist: `VoxelDeltaRle`, `InventoryState`, `ChunkDehydratedMetadata`, `WfcOutpostState`. This is the right direction, but it is save/delta focused. It does not yet prove a base-world cache/bundle contract equivalent to Subnautica's compiled terrain/object caches.
- H8 `WorldChunkStreamingProfile.asset`: world size 15,000m, chunk 192m, chunk cell 64m, macro zone 768m, visual/data residency radii. This maps well to Subnautica's batch/octree split as a clean-room macro/micro structure.
- Scene binary string scan of `02_HECTON_WORLD.unity` found HectonVoxelEngine, HectonUnderwaterVisuals, FaunaDirector, SpatialAudioManager, WorldProceduralScatterDirector, PlayerPDA. It found no direct strings for WorldChunkResidencyManager, SaveManager, Addressable, GlobalRegistry, ScannerTool, SubmarineCoreDirector. Because the scene is binary serialized, this is only evidence for further Unity validation, not final proof.
- Editor tools exist to wire/validate systems: `WorldRuntimeBootstrapAuthoring`, `WorldStreamingWiringValidator`, `MapMagicWorldValidator`, `HeadlessSimulationValidator`. They should be moved into mandatory build gates for foundation claims.

What Hecton8 should tactically borrow as foundation:
1. Real Addressables topology: generate groups/labels/catalo g for world cells, item prefabs, UI, audio, biome visuals, low/high texture tiers. Subnautica proves the content graph must be explicit and inspectable.
2. Base-world payload split: define H8 analogs for `TerrainCells`, `ObjectBatches`, `CompiledVisibility`, `CompiledPhysicsProxy`, `SaveCellDeltas`. Do not wait for content polish.
3. Macro/micro world contract: H8 already has 768m macro / 192m chunk / 64m cell. Lock this into file naming, hash IDs, streaming keys, debug tooling, and save-page payload types.
4. Compiled cache layer: add baked PVS/octree/material/physics-proxy files for H8 chunks. Hecton8 runtime generation should be fallback, not the only path.
5. Save slot split: keep H8's fixed-sector pager, but expose an external slot topology: metadata/screenshot/global state/scene state/per-cell deltas/corruption audit. Subnautica's `gameinfo.json` + binaries + zipped cells is the model class.
6. Mod handler taxonomy: use Nautilus as a clean-room reference for handler categories: prefab registration, recipe/craft data, PDA/log entries, save data, world entities, config UI. Do not copy GPL code.
7. Terrain patch contract: use TerrainPatcher as a clean-room pattern only: separate patch file, load order, ignore marker, optional dependency. Do not reference AGPL DLL/code. H8 can define `.h8terrainpatch`/`.h8voxelpatch` later.
8. Category audio/content packs: do not use FMOD bank files. Use the split idea: player, tools, vehicles, creatures, env, UI, music, story, base/submarine categories with tiered residency.

What Hecton8 appears to lack or has not proved:
- Built Addressables groups and catalog. Evidence: folder is empty.
- Generated chunk payload files for base world terrain/object/visibility/physics cache. Save pager exists; base cache contract is not proven.
- Mandatory validation that world streaming managers are present and wired in the runtime scene before build acceptance.
- A strict Addressables audit that checks non-empty settings, group count, entry count, labels, build output, catalog, and player runtime load proof.
- A clean-room mod data contract equivalent to Nautilus handler categories. H8 has `ModdingAPI`, but taxonomy and SDK proof are not yet comparable.
- Explicit first-hour route density metrics for scanner/PDA/fragments/resources as data contracts. Current focus is foundation, so this should be schema-first, not content-first.

Cinematic Cheats used:
- No simulation or visuals were implemented. Cheat applied at architecture level: borrow file topology and precompiled proxy concepts instead of copying or simulating reference-game internals.
- Recommended future cheat: compiled visibility/physics/material proxy payloads and cheap far visual proxies, with high-tier overkill loaded only by tiered Addressables labels.

Exact Microseconds saved:
- Exact measured runtime savings for this pass: 0us. No player code path changed.
- Exact runtime savings from proposed changes cannot be claimed without Unity player/profiler runs. Expected benefit category is hitch prevention and IO predictability, not guaranteed average frame-time reduction.

Foundation priority order:
1. P0: Fix Addressables proof. Empty `Assets/AddressableAssetsData` cannot pass readiness. Create groups, labels, content build, catalog verification, and runtime smoke load.
2. P0: Define H8 world payload schema and file naming for macro/chunk/cell/payload type. Add payload types beyond save deltas if needed.
3. P0: Promote `WorldStreamingWiringValidator`, `MapMagicWorldValidator`, and Addressables audit into build gates.
4. P1: Add baked compiled cache layer for visibility/physics/material proxy data per chunk/cell.
5. P1: Align save slot on disk to explicit metadata/global/scene/chunk-delta sidecars while keeping fixed-sector binary pager internally.
6. P1: Clean-room modding handler taxonomy and SDK validator.
7. P2: Content-density schemas for scanner/PDA/fragments/resources once the payload pipeline is real.

## 2026-05-15 Second Pass - Addressables Replacement and Modding Foundation

What was wrong:
- The project has Addressables installed, but the payload proof is missing. `Packages/manifest.json` includes `com.unity.addressables` 2.7.6, `com.unity.modules.assetbundle`, and `com.unity.sharp-zip-lib`; asmdefs define `UNITY_ADDRESSABLES_EXIST`. However `Assets/AddressableAssetsData` is empty, `Assets/StreamingAssets` does not exist, and `Assets/Resources` only contains small settings/input assets.
- `AsyncLoadHelper` is not a hidden replacement. It is a legacy ABI wrapper that fails immediately and logs that runtime Resources/Addressables loading is not available.
- `PlatformCompatibilityAudit` currently passes Addressables project data when the directory exists. That is too weak. It does not verify settings content, groups, entries, labels, build output, catalog existence, or runtime smoke load.
- Hecton8 has several content-adjacent systems, but none is a complete replacement for Addressables by itself.
- The modding SDK/runtime story is inconsistent. Builder emits `mod.json` and copies DLLs, but the loader does not reflection-load external assemblies and requires explicit factory registration. Builder also does not emit `RequiredAPIVersion`, while the loader disables manifests with missing/zero RequiredAPIVersion.
- `PROJECT_CONTENT_LEDGER.md` currently has no `MOD_COMPATIBLE` entries, so mod requests for first-party project prefab paths/GUIDs are effectively blocked by the allowlist guard.
- `ModCommandDispatcher.RegisterKernel` has no project callers. Non-intrinsic command opcodes that require a kernel will reject as `MissingKernel`.

What was done:
- Audited actual package/content state: Addressables package present, addressable project data empty, no StreamingAssets output, no Mods root.
- Audited content-pipeline layers:
  - `AsyncLoadHelper`: disabled runtime loader.
  - `ItemCatalog`: direct prefab fallback plus optional Addressables world-prefab prewarm/release.
  - `AssetLoadDispatcher`: priority/budget traffic controller, not a loader.
  - `AssetLifecycleGovernor`: residency/refcount/deferred release and Addressables cache cleanup policy.
  - `GameBootstrapper`: Addressables dependency/UI/tier prewarm hooks plus direct ObjectPool warmup.
  - `WorldChunkResidencyManager`: intended Addressables/additive-scene chunk residency contract.
  - `PersistentWorldRegistry`: save/page hydration that waits on ItemCatalog world-prefab prewarm.
  - `H8StaticDataArena`: binary static-data monolith loader targeting `StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`; output absent.
  - `PrefabRegistry`: stable IDs for already available prefabs, not IO.
- Audited modding layers:
  - `ModLoader`: manifest scan under project-root `Mods`, content-only bundle/localization support, explicit factory-only managed mod path.
  - `ModAssetManager`: on-disk AssetBundle load via `AssetBundle.LoadFromFile`, raw PNG fallback, path traversal guard, 8 MB raw texture cap, first-party asset allowlist gate.
  - `HectonAPI`: hash/command/save/data-overlay public contract; direct Unity object refs intentionally blocked.
  - `ModCommandDispatcher`: fixed queues, quotas, AUP path, render instance submission, intrinsic voxel/flow/acoustic paths, missing kernel registration evidence.
  - `ModRuntimeState` and `ModWorldPersistenceManager`: namespaced mod save payloads and internal persistent mod-spawn restore path.
  - `ModBuilderWindow`: AssetBundle build and manifest emission, with RequiredAPIVersion gap.

Verdict:
- Hecton8 does not currently have a full Addressables replacement.
- Hecton8 has the skeleton of a better content-residency architecture, but it is half-wired until one packaging source of truth exists.
- The real current runtime fallback is direct scene/prefab references plus ObjectPoolManager and ScriptableObject data, with Addressables hooks active but not backed by project data.
- The modding asset lane is real for content-only AssetBundles/raw textures/localization/save overlays. External managed DLL mods are not operational as a BepInEx-style loader under the current code.

Tactical foundation priorities:
1. Decide one core packaging contract now: real Unity Addressables, or a custom AssetBundle manifest. Do not keep package-defined Addressables and empty settings as a false-positive state.
2. Make Addressables readiness fail hard unless settings are non-empty, groups exist, labels exist, entries exist, content build output exists, catalog exists, and a runtime smoke load succeeds.
3. Keep `AssetLoadDispatcher`, `AssetLifecycleGovernor`, and `WorldChunkResidencyManager` as the foundation, but rename/document their roles: scheduler, residency governor, chunk residency owner. None should pretend to be the catalog/build pipeline.
4. Build or disable the Data Monolith path. `H8StaticDataArena` expects `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`; the folder is absent now.
5. Fix `ModBuilderWindow` manifest output to include `RequiredAPIVersion = 2`.
6. Align managed mod support with reality: either content-only/external-data only, or explicit preloaded factory mods only. Do not advertise copied external DLLs as loaded runtime code.
7. Register command kernels for promised opcodes or remove/hide those opcodes from the public mod contract.
8. Populate `PROJECT_CONTENT_LEDGER.md` with intentional `MOD_COMPATIBLE` allowlist entries only after the resource residency policy is stable.

Cinematic Cheats used:
- No runtime simulation changed. The tactical cheat is architectural: replace ad hoc runtime lookup with prebuilt catalogs, baked chunk payloads, direct far proxies, and tier labels so low hardware gets cheap deterministic data and high hardware gets visual overkill.

Exact Microseconds saved:
- This pass changed no runtime code. Exact measured runtime savings: 0us.
- Expected future gain category: fewer main-thread load stalls and cleaner memory residency. Exact microseconds require Unity player profiling after groups/bundles/monolith output exist.
---

## 2026-05-15 - Third Research Pass: Gates, Cache Contracts, Audio, PDA

Agent: SUBNAUTICA_RESEARCHER
Scope: research-only. Runtime code/assets were not changed.

What was wrong:
- Addressables readiness was over-signaled by package/folder presence. `Assets/AddressableAssetsData` is empty, `Assets/StreamingAssets` is absent, and no `BuildPlayerContent` call was found in first-party editor code.
- Critical world/content validators exist, but several are menu diagnostics rather than build-blocking gates: `WorldStreamingWiringValidator`, `MapMagicWorldValidator`, `HectonProjectAuditor`, `HeadlessSimulationValidator`.
- `PlatformCompatibilityAudit` marks Addressables project data as PASS if the directory exists, which is a false readiness signal for production payloads.
- `HectonTextureImportDictator.ResolveTieredTextureGroup()` returns `settings.DefaultGroup` before looking for/creating `Hecton_TextureStreaming_Auto`, so tier labels can land in the default group instead of a dedicated texture streaming group.
- H8 save/pager architecture is strong, but explicit base-world cache payload families are not named: terrain base cells, object batches, visibility/proxy cells, physics proxy cells, audio biome banks.
- Audio import policy still has large risks: 45 WAV, 89 OGG, 3 MP3 under `Assets/_Project/Audio`; 52 audio metas with `loadType: 0`, 45 with `preloadAudioData: 1`, 98 with `forceToMono: 0`. `Atmos 1.wav` is a 25.27 MB ambience source with `loadType: 0`, `quality: 1`, `preloadAudioData: 1`, `loadInBackground: 0`.
- PDA/scanner/lore runtime systems exist, but first-hour route/data density is not build-blocked. Lore data currently includes small authored sets: AudioLogs, Quests, DepthZones, SuitUpgrades, Registries.
- Modding has content-only foundations, but is not BepInEx/Nautilus-equivalent: external managed DLL loading is explicitly blocked without factory registration, `ModBuilderWindow` writes `EntryAssembly` but omits `RequiredAPIVersion`, no callers register `ModCommandDispatcher.RegisterKernel`, and `Mods` root does not exist.

What was done:
- Re-read AGENTS.md, project domain file, and task-relevant mandates for Addressables, world streaming, save persistence, bootstrap gates, audio, UI/lore streaming.
- Re-counted local Subnautica topology:
  - StreamingAssets directories: `aa`, `AssetBundles`, `SNUnmanagedData`, `SteamVR`.
  - Addressables bundle prefixes: `duplicateassetssorted` 1718 bundles / 2016.66 MB; `precursor` 386 / 628.42 MB; `main-discrete` 3 / 216.87 MB; `main.unity` 1 / 163.26 MB; `lost` 79 / 150.53 MB.
  - SNUnmanagedData Build18: `CompiledOctreesCache` 5416 files / 1147.35 MB; `CellsCache` 1606 / 159.8 MB; `BatchObjectsCache` 2975 / 3.07 MB.
  - Saves: `slot0000` has 31 files / 22,039,715 bytes; options has 2 files / 13,022 bytes.
  - Audio banks: largest are `music.bank` 183.28 MB, `Player.bank` 170.68 MB, `Creatures.bank` 56.65 MB, `Env.bank` 56.21 MB, `Cyclops.bank` 40.52 MB.
- Checked public sources as clean-room pattern evidence only:
  - Unknown Worlds terrain format: 160 m batches, 125 octrees per batch, 32 m octrees, 1 m voxel resolution, binary versioned optoctree data.
  - Nautilus current docs/API: handlers for prefabs, crafting, known tech, loot distribution, PDA/story goals, save data, sprites/audio/options.
  - TerrainPatcher: AGPL-3.0, patch file/load-order model, current release v1.2.5 on May 1 2026.
  - Nitrox: GPL-3.0, multiplayer sync foundation with latest release 1.8.1.0 on Jan 7 2026.
  - QModManager: GitHub API reports archived=True; archive timestamp is not exposed by the checked API response. Last repository push is 2023-05-09. Treat QMods layout as historical/deprecated only.
- Compared those patterns against Hecton8 runtime/editor code and documented P0/P1 foundation priorities.

Cinematic Cheats used / recommended:
- Prefer base-world baked caches and proxy payloads over runtime simulation truth: terrain/object/visibility/physics/audio cells.
- Treat Subnautica optoctree/cell/cache topology as evidence for chunk contracts, not as source material.
- Audio should buy immersion through categorized residency and tiered layers, not through always-preloaded giant WAVs.
- PDA/route density should be validated as authored data graphs, not generated through expensive runtime discovery.

Exact Microseconds saved:
- Current pass: 0us, research-only, no runtime code changed.
- Projected savings remain PENDING VERIFICATION. Expected wins are hitch/memory-risk reduction after build gates and payload contracts are implemented; exact values require Unity player build, Profiler, memory and Addressables telemetry.

Foundation priorities:
1. P0: Add a build-blocking Addressables/content gate: non-empty settings, required groups/labels, catalog build result, bundle-size caps, `StreamingAssets`/remote catalog policy, and no false PASS from directory existence.
2. P0: Convert world streaming/map stack validators into a prebuild/CI acceptance path or create a strict wrapper that fails the build for missing critical scene/profile/content wiring.
3. P0: Add AudioImportPolicy gate for large clips, preload flags, load type, mono rules for 3D SFX, and per-category residency.
4. P0: Define base-world cache payload families beside current save deltas: TerrainCellBase, ObjectBatchBase, VisibilityProxy, PhysicsProxy, AudioBiomeBank, Route/DiscoveryGraph.
5. P1: Fix mod SDK manifest/runtime mismatch: builder writes `RequiredAPIVersion`, content-only/mod-DLL behavior is explicit, command kernels are either registered or blocked at package validation.
6. P1: Add first-hour route coverage validation: scan fragments, PDA entries, known-tech unlocks, quest beats, depth/biome discovery, resource/crafting path.
7. P1: Fix texture Addressables group selection so tiered textures do not silently enter DefaultGroup.

Regression model:
- CPU: no change now. Future validators add editor/build time only.
- GC: no runtime change now. Future import/content gates should reduce runtime churn risk but must be profiled.
- Memory: no change now. Audio/import and Addressables grouping should reduce accidental residency, pending measurement.
- Cadence: no runtime cadence change now. Future streaming payload gates must preserve async cadence and release queues.
- Correctness: no code correctness risk introduced by this research pass. Risk remains that current content pipeline can look valid while shipping no real Addressables catalog.
External source verification, 2026-05-16:
- TerrainPatcher GitHub API latest release: v1.2.5, published 2026-05-01T22:15:08Z.
- Nitrox GitHub API latest release: 1.8.1.0, published 2026-01-07T18:50:51Z.
- QModManager GitHub API repository state: archived=True, pushed_at=2023-05-09T23:22:51Z.
- Nautilus GitHub API latest release endpoint currently returns sml/2.15.0.1 from 2023; current handler taxonomy must be treated as source/docs evidence, not release-fresh proof.
- BepInEx.Subnautica GitHub API repository state: archived=False, pushed_at=2026-05-14T12:12:36Z.
---

## 2026-05-16 - Fourth Research Pass: What To Dig Out Next

Agent: SUBNAUTICA_RESEARCHER
Scope: research-only. Runtime code/assets were not changed.

What was wrong:
- The prior Addressables argument was framed too broadly. The useful Subnautica evidence is not "use Unity Addressables for everything"; it is catalog + bundle lanes + AOT/link preservation + baked cache families.
- The statement "H8 has no packer" was wrong. `H8DataMonolithCompiler` exists. The real failure is source data and enforcement: `_SourceData` exists but is empty, `Assets/StreamingAssets` is absent, and `static_data.h8bin` is absent.
- H8 world pager payload names are still save/delta-oriented: `VoxelDeltaRle`, `InventoryState`, `ChunkDehydratedMetadata`, `WfcOutpostState`. Subnautica proves base-world cache must be named separately from player save state.
- Audio policy exists as `HectonAudioPostprocessor`, but it is not a build gate and misses the largest root-level `Atmos *.wav` sources.
- H8 modding has a good DOD boundary, but not enough content handler overlays for practical community mods: PDA/databank, scan/fragment, known-tech/unlock, loot distribution, audio registry.
- ContentSanity validates many references, but it is a menu action. First-hour route density is not a build-blocking acceptance contract.

What was done:
- Parsed local Subnautica Addressables data:
  - `catalog.json`: 12,016,061 bytes.
  - `settings.json`: build target `StandaloneWindows64`, Addressables version `1.19.11`, locator `AddressablesMainContentCatalog`, max concurrent web requests 500.
  - Catalog internals: 21,090 internal IDs, 4 provider IDs, 2,624,036 key-data chars, 836,236 entry-data chars, 5,546,380 extra-data chars.
  - Addressables bundle lane counts: `assets_bundle` 1,717 files / 2,016.50 MB, `prefab` 3,727 / 1,887.47 MB, `unity` 13 / 298.08 MB, `worldmeshes` 1 / 191.79 MB.
  - Top bundles include `main-discrete_assets_worldmeshes` 191.79 MB, `main.unity` 163.26 MB, duplicate assets bundle 83.84 MB, and large precursor/gun/rocket prefab or scene bundles.
- Parsed Subnautica `AddressablesLink/link.xml`:
  - 1,243 preserved `Assembly-CSharp` types, file size 74,252 bytes.
  - High-frequency preserved families include Cyclops 50, Base 44, Water 31, Spawn 24, Damage 22, Sound 20, Story 18, Creature 17, PDA 15, Vehicle 12, Power 11, Prefab 10, Terrain/FMOD/Resource 9 each.
  - Tactical meaning: even a custom H8 monolith still needs a generated prefab/component preservation contract for IL2CPP and bundle instantiation.
- Quantified Subnautica baked world cache:
  - `CompiledOctreesCache`: 5,416 files / 1,147.35 MB, max 9.73 MB.
  - `CellsCache`: 1,606 files / 159.80 MB, max 1.216 MB.
  - `BatchObjectsCache`: 2,975 files / 3.07 MB, max 16.9 KB.
  - Sidecars: `biomeMap.bin` 1 MB, `index.txt`, `meta.txt`, `biomes.csv`, `signals.csv`.
  - Saved slot `slot0000`: cell cache zips plus `global-objects.bin`, `scene-objects.bin`, `gameinfo.json`, screenshot and timecapsules. `gameinfo.json` confirms separate durable game flags such as exosuit/base presence and corruption state.
- Re-audited H8 DataMonolith:
  - `H8DataMonolithCompiler` bakes `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` from `Assets/_SourceData`.
  - Sections include Items, Creatures, Biomes, Recipes, BiomeHeatmap, QuestNodes, QuestEdges, LootCdf, VoxelMaterials, AudioClipRegistry, NarrativeTriggers, SectorPageDirectory, LocalizationUtf8, and others.
  - It has source watchers and hot reload. It validates blittable layout and duplicate item/creature/biome hashes.
  - `_SourceData` exists but has no files. `Assets/StreamingAssets` and `static_data.h8bin` do not exist.
  - `Assets/_Project/Data` holds 1,236 `.asset` files, so there is a ScriptableObject lake but no populated monolith source-of-truth.
- Re-audited H8 audio:
  - Audio source totals under `Assets/_Project/Audio`: 45 WAV / 291.90 MB, 89 OGG / 171.57 MB, 3 MP3 / 2.22 MB.
  - Postprocessor-managed by current root rules: 121 files / 230.27 MB.
  - Unmanaged: 16 files / 235.41 MB, dominated by root-level `Atmos 1.wav` through `Atmos 5 Loop.wav`, each roughly 21.97-25.27 MB.
  - `Underwater Ambient.wav` is managed only by name fallback; root `Atmos` files are not.
  - Managed SFX policy forces ADPCM, mono, 22050 Hz, but `ResolveSfxLoadType()` always returns `DecompressOnLoad`.
  - Managed ambient/music policy forces Vorbis, 44100 Hz, Q0.7, `CompressedInMemory`.
  - Validators are menu items, not `IPreprocessBuildWithReport`.
- Compared H8 mod API to public Nautilus handler taxonomy:
  - H8 public API covers Events, Input, Commands, Resources, Telemetry, Items, Crafting, Recycling, Construction, Ecosystem, Localization, UI, World hash query, SaveState, Mods diagnostics.
  - H8 correctly blocks direct Unity prefab/audio/texture/GameObject/Transform access from mods and pushes mods toward hashes/commands.
  - Missing data-overlay handler lanes: PDA/databank entries, scanner/fragment entries, known-tech or blueprint unlock gates, loot CDF overlays, audio registry overlays.
  - Existing mismatch remains: `ModBuilderWindow` writes `EntryAssembly` but no `RequiredAPIVersion`; `ModLoader` rejects `RequiredAPIVersion <= 0`; IL2CPP path refuses dynamic external managed assembly loading; factory registration is required but not what the builder advertises.
- Rechecked H8 first-hour/lore skeleton:
  - Authored assets include 11 QuestData assets, 5 AudioLogData assets, 5 depth zones, 13 research assets, scanner item/recipe/tool metadata, and lore registries.
  - `ContentSanityValidator` checks quest references, item/catalog route errors, PDA shell risks, prefab contracts, tool data, flora/fauna/resource/base templates, but is launched through `Hecton-8/Validate Content`.
  - `LoreHashBuildPreprocessor` rebakes lore hashes before build, but that is not first-hour route density acceptance.

Cinematic Cheats used / recommended:
- Split truth from presentation:
  - `TerrainCellBase`: base terrain/SDF/voxel cell data.
  - `ObjectBatchBase`: tiny static debris/resource transform+hash batches, no GameObject spawn storm.
  - `VisibilityPhysicsProxyBase`: PVS/SDF/physics visibility proxy, independently resident.
  - `AudioBiomeBank`: per-biome audio keys/residency, not giant preload.
  - `DiscoveryRouteBase`: scanner/PDA/quest route graph for validation and onboarding density.
- Keep H8 DOD mod API boundary. Borrow Nautilus handler categories, not loader design or GPL code.
- Treat Subnautica FMOD bank split as taxonomy: music, player, creatures, environment, vehicles, tools, loot, interface. Do not copy banks.
- Add a generated H8 prefab/component preservation manifest; this is the AOT equivalent of Subnautica's link preservation lane.

Exact Microseconds saved:
- Current pass: 0us, research-only.
- Expected future savings are not claimed as measured. Target categories:
  - fewer main-thread activation spikes from ObjectBatchBase instead of spawned static GameObjects;
  - lower audio memory pressure after root `Atmos` WAV governance and tiered load policy;
  - less boot/runtime managed data scanning after populated `static_data.h8bin`;
  - fewer IL2CPP/mod/bundle failures from generated type and manifest contracts.

P0 Batch007 recommendations:
1. DataMonolith Source Gate: decide source of truth and implement either SO -> `_SourceData` exporter or `_SourceData` as authoritative. Fail build if required sections are empty or `static_data.h8bin` is missing/stale.
2. World Payload Taxonomy: extend payload constants/manifest to include `TerrainCellBase`, `ObjectBatchBase`, `VisibilityPhysicsProxyBase`, `AudioBiomeBank`, `DiscoveryRouteBase`. Keep save deltas separate.
3. Prefab Type Manifest: generate a first-party/mod-safe component preserve list or `link.xml` equivalent for bundled prefabs. Do not rely on incidental `[Preserve]` attributes.
4. AudioImportPolicyGate: convert menu validation into build gate. Cover all audio under `Assets/_Project/Audio`, including root `Atmos` and UI/VO. Platform/tier rule: SFX never streaming; long music/large ambience can be Streaming on low-memory/mobile, CompressedInMemory where budget allows.
5. Mod SDK Manifest Fix: builder must write `RequiredAPIVersion = 2`; UI must stop implying arbitrary DLLs work under IL2CPP. Add package validation for content-only vs explicit factory mods.
6. Mod Overlay Handlers: add data-only overlays for PDA/databank, scan entries/fragments, known-tech/quest flags, loot CDF, audio registry. Merge via MacroDB/DataMonolith/mod overlay, not direct SO mutation.
7. FirstHourRouteDensityGate: build-block route acceptance after content pipeline is stable: pod exit, resource collection, scanner craft, scan targets, one unlock, one PDA/audio log, one danger/biome beat.

P1/P2 recommendations:
- Addressables/custom bundle decision doc: H8 may keep custom monolith for world data, but Unity prefab/resource lanes still need a catalog/manifest and runtime smoke load.
- Convert selected ContentSanity checks into CI/prebuild gate or wrap them in one strict build validator.
- Add saved-game cache semantics: authoritative save payloads vs repairable/generated caches. Subnautica's slot layout separates game info/global/scene state from cached cells.
- Add per-zone bundle/payload size budgets. Subnautica has some huge legal bundles; H8 should not let a single low-tier mandatory payload become an 80-190 MB choke point.

Proof limits:
- Local Subnautica files were inspected only as taxonomy/metadata. No proprietary asset/code extraction, decompilation, mesh/audio/text copying, or binary reverse engineering was performed.
- Public mod repositories/docs were used as clean-room handler/source taxonomy. GPL/AGPL/LGPL code is not reusable in H8 without an explicit licensing decision.
- No Unity compile or playmode test was run because no runtime code was changed.
---

# SUBNAUTICA_RESEARCHER FIFTH PASS - SIDECARE CACHE / CONTENT AUTHORITY / SCANNER ROUTE
Date: 2026-05-16
Mode: RESEARCH ONLY. NO RUNTIME CODE CHANGED. NO PROPRIETARY ASSET OR CODE EXTRACTION.

What was wrong:
- Prior high-level verdict was still too coarse. The real foundation split is not simply "Addressables yes/no". It is: baked world sidecars, asset hash maps, route unlock proof, prefab preservation, and mod overlay contracts.
- H8 has new ContentAuthority source files, but the actual data assets are absent: `Assets/AddressableAssetsData` has 0 files, `ContentAssetHashMap` asset count is 0, `ContentVfxPrewarmManifest` asset count is 0, and no scene/prefab/data wiring was found for `ContentAuthorityRuntime`.
- H8 DataMonolith compiler exists, but source truth is empty: `Assets/_SourceData` file count is 0, `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is absent.
- Scanner first-hour route is not proven. `Recipe_Scanner.asset` requires `scan.expedition_contact`. Current Prefabs/Scenes/Data search found no authored production instance of that entry, only editor bootstrap scripts capable of generating it. A current real prefab hit exists for `Item_Titanium.prefab` with `resource.titanium_fragment`, but that does not unlock scanner.
- Subnautica sidecars show important authored discovery/cache metadata that H8 sector pages do not yet model.

What was done:
- Inspected Subnautica `SNUnmanagedData/Build18` sidecars:
  - `biomeMap.bin` 1,048,578 bytes.
  - `index.txt` 46,008 bytes. First fields identify world/grid/index parameters, then numeric cell records.
  - `meta.txt` says `BlockPrefabs`.
  - `biomes.csv` has 20 lines including header.
  - `signals.csv` has 15 lines including header: 14 authored signal anchors with biome, batch, position, description.
  - Signal taxonomy: `HeatSignature`, `CaveEntrance`, `BalancingRock`, `HugePillar`, `HugeKoosh`, `CoralArch`, `FloatingIsland`, `SecretCave`, `GiantMushroomTree`.
- Inspected Subnautica saved-slot cache topology:
  - `slot0000/CellsCache`: 25 zip files / 20.82 MB.
  - Example zip entries are named `baked-batch-cells-<batch>-<x>-<z>.bin`; first sampled zips had 21, 30, 55, 62, 63, 63, 77, and 72 entries.
  - Slot root contains `gameinfo.json`, `global-objects.bin` 88,743 bytes, `scene-objects.bin` 57,921 bytes, `screenshot.jpg`.
  - Pattern: generated/repairable cell cache is separated from global/scene durable state.
- Rechecked H8 DataMonolith accepted CSV tables:
  - `items`, `item`, `creatures`, `creature_traits`, `genome`, `biomes`, `recipes`, `biome_heatmap`, `quest_nodes`, `quest_edges`, `loot`, `loot_cdf`, `voxel_materials`, `audio`, `audio_registry`, `vfx`, `vfx_scalars`, `tool_heat`, `hull`, `submarine_hull`, `narrative_triggers`, `physics_materials`, `ghost_modules`, `radiation`, `radiation_map`, `spawn_credits`, `sop_errors`, `hud_layout`, `sector_pages`.
  - `sector_pages` currently parses only `sector_id/id`, `biome_id`, `file_offset`, `byte_count`, `aup_x`, `aup_z`.
- Rechecked ContentAuthority scaffold:
  - `ContentAuthorityBuildPreprocessor` exists and fails builds for missing Addressables groups `Core`, `High_Res`, `Overkill`.
  - `ContentAssetHashMap` provides hash->address/asset/mesh/tier/biome/LOD/dependency metadata and binary lookup.
  - `ObjectBatchBase` defines static mesh/material/instance/chunk payloads and BRG binding contract.
  - `VisibilityProxyBase` is only a MonoBehaviour AABB/frustum gate, not a baked sector PVS payload.
  - Gap: no concrete `ObjectBatchBase` asset, baker, BRG binding implementation, `ContentAssetHashMap` asset, VFX manifest, or runtime scene binding found.
- Rechecked scanner/progression code and data:
  - `ScanLogSystem` archives discovered entries by hash and persists scan log DTOs.
  - `ScanEvents` queues `ScanEventPayload` through bounded NativeQueues and metadata cache.
  - `ScannableTarget` registers into world spatial hash and DataVault-backed lore entity AUP/hash buffers.
  - `ScannableFragment` can emit `EntryDiscovered` on completed research scans.
  - `ResearchDirector` listens for scan entries and unlocks lore/quests from `XenoBiologyTree` nodes.
  - `Fabricator` hides locked recipes via scan log revision and unlock masks; scanner recipe remains blocked until `scan.expedition_contact` is archived.
  - `ScanIntelValidator` and `ContentSanityValidator` are menu validators, not build gates.
- Rechecked public mod/source references as clean-room taxonomy only:
  - Unknown Worlds terrain format article: useful public format concepts.
  - Nautilus handler docs: useful handler taxonomy.
  - BepInEx.Subnautica: loader pack reference, not H8 architecture target.
  - Nitrox: GPL-3.0, taxonomy only.
  - TerrainPatcher: AGPL-3.0 and warns non-AGPL mods away from direct interaction, taxonomy only.

Cinematic Cheats / tactical borrowables:
- Borrow sidecar pattern, not files: `WorldSidecarManifest`, `SectorSignalAnchor`, `DiscoveryRouteBase`, `ObjectBatchDirectory`, `VisibilityPhysicsProxyDirectory`.
- Borrow cache separation: base/generated world cell cache separate from durable save deltas/global/scene state.
- Borrow handler categories from mature modding: PDA entry overlay, scan entry overlay, known-tech/quest flag overlay, loot CDF overlay, audio registry overlay.
- Borrow prefab preservation idea from Addressables/link preservation: generated `H8PrefabTypeManifest` or link/preserve contract for first-party and mod-safe bundled prefab components.
- Borrow signal anchor idea for first-hour route validation: every recipe scan gate must resolve to at least one reachable authored source in the production world or an explicit startup grant.

Exact Microseconds saved:
- Current pass: 0us, research-only.
- Future estimates remain unmeasured until Unity player profiling:
  - ObjectBatchBase/BRG should remove static debris GameObject activation spikes.
  - World sidecar manifests should reduce scene scans and runtime string/path lookup.
  - Audio import/build gates should reduce memory pressure and load hitches.
  - FirstHourRouteDensityGate saves QA/support loops, not frame time.

P0 findings:
1. `Recipe_Scanner` route lock: `scan.expedition_contact` is required but not currently proven in production scene/prefab/data content. Add a build/preplay route validator or author the probe into the real world. Do not rely on editor bootstrap scripts.
2. DataMonolith is code-only until `_SourceData` is populated and `static_data.h8bin` is produced/staleness-checked. Add build gate for required non-empty sections.
3. ContentAuthority is scaffold-only until `ContentAssetHashMap` assets, VFX manifests, Addressables settings/groups/entries, and scene runtime binding exist.
4. H8 sector page schema is too thin. Add sidecar/payload families for discovery signals, object batches, visibility/physics proxies, audio biome banks, and payload version/repairability.
5. Menu validators must become build/preplay gates for content authority, first-hour scanner route, audio import policy, and DataMonolith staleness.

P1 findings:
- `VisibilityProxyBase` is useful as a component gate but is not equivalent to Subnautica baked PVS/CompiledOctrees. Build a baked proxy payload lane instead of piling more MonoBehaviours into sectors.
- `ObjectBatchBase` has the right shape but no concrete asset/baker/BRG implementation found. It should own static wreck/debris/resource dressing before those become scene GameObjects.
- Scanner/research code still carries string/SO surfaces. Acceptable in cold authoring/event paths for now, but MacroDB/DataMonolith should own hashed scan entries and quest gates before scale-up.
- `ScannableFragment` uses MaterialPropertyBlock for scan glow on standard geometry. This conflicts with the SRP Batcher mandate unless the shader/material lane explicitly opts into this effect or moves to an instanced/GraphicsBuffer path.

Proof limits:
- Local Subnautica install was inspected as file taxonomy and metadata only. No decompilation, binary reverse engineering, asset copying, text/audio/mesh extraction, or proprietary payload reuse.
- Public repositories/docs were used as current source taxonomy only. GPL/AGPL/LGPL code remains non-reusable in H8 without a deliberate licensing decision.
- No compile/playmode/profiler run was performed because no runtime code was changed.
# SUBNAUTICA_RESEARCHER SIXTH PASS - MULTIPLATFORM / H-PHI / FOUNDATION INQUISITION
Date: 2026-05-16
Mode: RESEARCH ONLY. NO RUNTIME CODE CHANGED. NO PROPRIETARY ASSET OR CODE EXTRACTION.

What was wrong:
- Some native/binary payload structs are not strict enough for the multiplatform mandate. Several queues use `[StructLayout(LayoutKind.Sequential)]` without explicit `Pack`/`Size`.
- One authoring struct is actively misleading: `ContentAssetEntry` has `[StructLayout(Pack = 1)]` while containing `string`, Addressables `AssetReference`, `GameObject`, `Mesh`, `Material`, `bool`, and `uint[]`.
- H-Phi is partial. Systems use `NativeMemorySentinel` and some DataVault buffers, but multiple systems still own local NativeArrays/NativeQueues/NativeHashMaps.
- Scanner has a Burst job that is scheduled and immediately completed in the same method.
- Shader thread groups are currently within the 1024 ceiling, but three compute shaders require `#pragma target 5.0`, so they are not a universal Quest/Android/low-tier path.
- DataMonolith and content authority have validators/scaffolding, but disk payloads are empty.

What was done:
- Re-read `Status_SUBNAUTICA_RESEARCHER.md` and `Rationale_SUBNAUTICA_RESEARCHER.md`.
- Checked `Docs/Tasks/CURRENT_BATCH.md`; no active `<AGENT_PROMPT id="SUBNAUTICA_RESEARCHER">` block was found.
- Re-read relevant mandates: `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`, `ARCH_Signal_Lane_Segregation.txt`, `STRM_Async_Asset_Upload_Texture_Settings.txt`, `REND_GPU_Occlusion_Culling_6000.txt`.
- Audited content/runtime structs: `ContentAssetBinaryRecord` is valid at 32 bytes; `ContentAssetEntry` is managed authoring data, not a native binary record; `ContentBundleRefState`, `ContentAuthorityTelemetryEntry`, `ObjectBatchInstance`, `ObjectBatchChunk`, and `ContentLoreBlockIndex` have explicit sizes; `ScanEventPayload`, pager command/result/telemetry structs, multiple mod AUP/render/raycast/reject/memory payloads, and `ModRegistryEventPayload` need explicit Pack/Size or a documented managed-only exemption.
- Audited native ownership: `ScannableTarget` uses `GlobalRegistry.DataVault` for lore AUP/hash buffers, but `H8BinaryWorldPager`, `ScanEvents`, `ScannerTool`, `ModCommandDispatcher`, `ModRegistryEvents`, `ModResourceRegistry`, `ModEventProjectionBridge`, and `H8StaticDataArena` still own local native containers. Most are sentinel-registered, but sentinel registration is not stateless data sovereignty.
- Audited scanner stability: `LoreCandidateDotProductJob` guards rsqrt paths; scanner writes a 300-entry blackbox ring; `TryResolveScientificLoreCandidate` still schedules and immediately completes a job; production summaries use `FixedCharBuffer`, while development-only legacy summary uses `string.Format`.
- Audited shader portability: found compute thread groups at 64, 256, or 512 total threads; no text-scan hit above 1024. `ParticleUpdate.compute`, `Hecton_VolumetricLight.compute`, and `HectonHudFogLuminance.compute` use `#pragma target 5.0` and need tier/platform gates.
- Audited I/O: `H8BinaryWorldPager` uses persistent random-access FileStream, fixed arenas, CRC, RLE, and corrupt-read blackbox dumps; `H8StaticDataArena` uses `File.Exists`/`File.ReadAllBytes` on `Application.streamingAssetsPath`; `ContentLoreBinaryProvider` memory-maps editor/standalone and synchronously reads fallback streams elsewhere.
- Rechecked payload state: `Assets/AddressableAssetsData` has 0 files; `Assets/_SourceData` has 0 files; `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is absent; no `ContentAssetHashMap`, `ContentVfxPrewarmManifest`, or concrete content authority asset was found under current data paths.
- Rechecked first-hour scan route: recipes require `scan.expedition_contact`, `scan.resource_cache`, `scan.structure_relay`, and `scan.resource_node`; current real prefab/data/scene search found `Item_Titanium.prefab` with `entryId: resource.titanium_fragment`; no real prefab/scene/data instance was found for `scan.expedition_contact`, `scan.resource_cache`, or `scan.structure_relay`.

Cinematic Cheats / tactical borrowables:
- From Subnautica sidecars: keep the split between baked base world, generated cell cache, signal anchors, and durable player save state.
- For H8 low tier: use sidecar-driven hashed route anchors, cheap dot-product scanner vision, LUT/triangle-noise presentation, and procedural fallback pages.
- For H8 high/ultra tier: attach salt crystals, volumetric silt wake, procedural dents, raymarch detail, and 16-tap POM to `ContentTier.Overkill`, not to core gameplay contracts.
- For scanner: use previous-frame native result or a cheap capped scalar loop; spend saved cycles on hologram/readability, not immediate job completion overhead.
- For world debris: turn `ObjectBatchBase` into a real baked BRG/indirect payload lane before adding more static scene GameObjects.

Exact Microseconds saved:
- Current pass: 0us, research-only.
- No compile/playmode/profiler run, because no runtime code was changed.
- Future savings are unmeasured. Do not claim exact microseconds until a Unity player profile compares before/after.

P0 findings:
1. `ContentAssetEntry` must stop pretending to be a packed binary/native struct. Keep `ContentAssetBinaryRecord` as the packed record; make authoring entry explicitly managed/Serializable only.
2. Native queue payloads need explicit layout audit: `ScanEventPayload`, pager command/result/telemetry payloads, mod AUP/render/raycast/reject/memory payloads, and registry payloads.
3. `H8StaticDataArena` cannot ship universal Android/Quest loading through `File.ReadAllBytes(Application.streamingAssetsPath)` without a platform loader or pre-copied persistent path.
4. ContentAuthority build gate is real, but content payload is still empty: Addressables settings files, hash maps, VFX manifests, and DataMonolith blob are absent.
5. Scanner craft route is still not proven in production content for `scan.expedition_contact`.

P1 findings:
- `ScannerTool.TryResolveScientificLoreCandidate` immediate `handle.Complete()` violates the spirit of the job-system mandate. Replace with previous-frame completion or no-job direct loop when candidate count is small.
- Local native owners are sentinel-registered, not stateless. Decide which singleton I/O owners are explicitly exempt and move shared/event buffers to GlobalDataVault typed lanes.
- `ScanEvents` and `ModRegistryEvents` are bounded NativeQueue buses, but they are still legacy static buses, not typed `SignalBus` lanes.
- Mod public payloads need stronger Pack/Size contracts before external mods are treated as stable ABI.
- Shader Model 5 compute files must be behind feature/tier gates with Dear Lie fallback for Quest/Android/MX350.

Proof limits:
- This pass did not edit runtime code.
- This pass did not parse proprietary Subnautica binaries or copy proprietary files.
- Text search is not the same as compiled kernel validation; Unity build/preprocess can catch final compute kernel sizes, but current disk state likely fails content authority before that proof is meaningful.
## SUBNAUTICA_RESEARCHER - Seventh Pass / Foundation Arbitration

Scope: Research-only. No runtime code changed. All Subnautica inspection remained clean-room file taxonomy; no assets/code/binaries were copied or parsed for implementation.

What was wrong / corrected:
- Previous ContentAuthority wording was partly stale. Current source now has DataVault-backed bundle refs, telemetry, and pending load state, plus a real `ContentAuthorityBuildPreprocessor`.
- ContentAuthority is still not a populated content authority. Disk proof: `Assets/AddressableAssetsData` files=0, `Assets/_SourceData` files=0, `Assets/StreamingAssets` missing, and no `ContentAssetHashMap`/`ContentVfxPrewarmManifest` assets found.
- DataMonolith compiler exists. The real gap is source/output/build freshness: `_SourceData` is empty and `static_data.h8bin` is absent. Runtime loader still uses `Application.streamingAssetsPath` + `File.Exists`/`File.ReadAllBytes`, which is not Android/Quest-proof and doubles peak boot memory.
- Audio has improved. `AudioImportDictator` is now a final-order audio policy and adds a 50 MB preloaded-audio build gate. Current metas still have large root Atmos WAVs as DecompressOnLoad/preload, so the gate must catch stale imports until reimported.
- Modding contract is still broken. `ModLoader` rejects `RequiredAPIVersion <= 0`, while `ModBuilderWindow` still does not emit `RequiredAPIVersion` in generated `mod.json`.
- H8 world pager payload vocabulary is still too save-delta oriented. Subnautica clean-room taxonomy shows distinct base-world terrain cells, batch objects, compiled proxy/PVS-like data, biome/signal sidecars, and save deltas.
- Shader portability gates exist, but some are not strict by default. `HectonHudFogLuminance.compute`, `Hecton_VolumetricLight.compute`, and `ParticleUpdate.compute` use `#pragma target 5.0`; these need explicit tier/platform fences.

Subnautica evidence re-counted:
- `C:\Games\Subnautica\Subnautica_Data\StreamingAssets\SNUnmanagedData\Build18`: BatchObjectsCache 2,975 files / 3,218,027 bytes; CellsCache 1,606 files / 167,561,255 bytes; CompiledOctreesCache 5,416 files / 1,203,085,204 bytes; biomeMap/index/meta/biomes/signals sidecars present.
- `C:\Games\Subnautica\SNAppData\SavedGames\slot0000`: CellsCache 25 files / 21,836,090 bytes; global-objects.bin 88,743 bytes; scene-objects.bin 57,921 bytes; gameinfo.json 345 bytes.

Batch 007 tactical queue:
1. Fix `ModBuilderWindow` manifest output: emit `RequiredAPIVersion = 2` and `ModPriority`, and stop implying arbitrary external DLL loading unless a registered factory exists.
2. Add DataMonolith build freshness gate: fail if `_SourceData`/`Data/Balance` changed and `static_data.h8bin` is absent/stale/empty; decide whether zero-record blob is allowed only in dev.
3. Add Android/Quest-safe monolith loader path: UnityWebRequest or platform file abstraction, chunked native copy, no full managed `byte[]` staging for large blobs.
4. Populate minimal ContentAuthority proof assets: Core/High_Res/Overkill Addressables settings, at least one `ContentAssetHashMap`, and one VFX prewarm manifest or explicit waiver.
5. Split `ContentAssetEntry` authoring from binary layout: remove misleading `[StructLayout(Pack=1)]` from managed-reference authoring record; keep `ContentAssetBinaryRecord` as the packed 32-byte blob record.
6. Add base-world payload family constants: `TerrainCellBase`, `ObjectBatchBase`, `VisibilityPhysicsProxyBase`, `AudioBiomeBank`, `DiscoveryRouteBase`.
7. Reimport/fail stale audio metas: root Atmos WAVs must stream/no-preload; build gate should apply policy or compare importer state before budget estimation.
8. Promote first-hour scan route validation to build/CI after generated scene/bootstrap proof exists: `scan.expedition_contact`, `scan.resource_cache`, `scan.structure_relay` must be reachable or intentionally waived.
9. Make shader portability strict for mobile/XR/Metal builds and leave High/Ultra PC overkill behind explicit tier groups.

Cinematic cheats retained:
- Toaster route: authored sidecars, fixed-size records, LUT/audio-bank/proxy payloads, low-frequency validation gates, no runtime physics/PVS fantasy.
- God-mode route: overkill visual/audio payloads are allowed only after Core contracts are deterministic and tier isolated.

Exact microseconds saved: 0us measured. This pass changed documentation/logs only. Any future runtime savings require populated content, player build profiling, and platform captures.
## Eighth Pass - Integration Truth And Foundation Residue

What was wrong -> The latest integration logs use strong "BUILD GREEN" language, but that phrase was too broad for the actual proof. Current C# compilation is green, while Unity import, Play Mode, Addressables build, Android/Quest IL2CPP, Metal shader build, Steam Deck storage pressure, and runtime profiler budgets are still unproven. The Core assembly boundary also hides an AI/Ecosystem implementation inside the root Core asmdef.

What was done -> Read `LOG_INTEGRATION_ASSEMBLY_SURGEON.md` and `Dump_COMPILE_ERROR.txt`, then verified current disk directly. Ran `dotnet build Hecton8.Core.csproj --no-restore -v:q /clp:ErrorsOnly`: build succeeded, 0 warnings, 0 errors, 00:00:05.21. Ran `dotnet build Hecton8.Editor.csproj --no-restore -v:q /clp:ErrorsOnly /m:1`: build succeeded, 36 warnings, 0 errors, 00:02:20.99. Cross-checked asmdefs, StructLayout markers, GlobalSignals tether validators, ContentAuthority payload state, audio metas, ModBuilder/ModLoader manifest contract, H8 world payload constants, native ownership, Update hooks, and EventBus/delegate surfaces.

Cinematic Cheats used -> No new runtime cheats were implemented. Tactical reference remains clean-room: use Subnautica-like sidecar topology as a contract vocabulary, not copied data. Low tier keeps cheap fakes and strict payload absence prevention; Ultra keeps visual overkill only through explicit tiered content gates.

Exact Microseconds saved -> Research-only 0us. No frame-time or memory-savings number is claimed. Build timings above are compile verification times, not runtime optimization results.

Verification -> Core compile is green now. Editor compile is green now but carries 36 warnings not yet triaged. Current payload facts remain bad: `Assets/AddressableAssetsData` files=0, `Assets/_SourceData` files=0, `Assets/StreamingAssets` missing, `static_data.h8bin` missing, `ContentAssetHashMap` assets=0, `ContentVfxPrewarmManifest` assets=0. Audio metas still show 45 `loadType=0 preload=1` clips, with top WAVs 23-32 MB. ModBuilder still omits `RequiredAPIVersion`, while ModLoader v2 rejects missing/zero required API. H8 page payload constants still do not name `TerrainCellBase`, `ObjectBatchBase`, `VisibilityPhysicsProxyBase`, `AudioBiomeBank`, or `DiscoveryRouteBase`.

Status -> EIGHTH PASS COMPLETE. Build is currently C# green; foundation is not product green. P0 queue remains: 1) populate minimal content authority payload and monolith output, 2) fix mod manifest v2 SDK output, 3) force audio reimport or fail stale meta drift, 4) split managed `ContentAssetEntry` from binary layout claims, 5) move AI/Ecosystem implementation out of root Core or formalize contract-only layout ownership, 6) classify no-Pack structs and local NativeContainer owners, 7) add platform build/player/profiler gates before claiming AAA readiness.

---

## NINTH PASS - FOUNDATION CLEAN-ROOM ARBITRATION (2026-05-16)

Agent: SUBNAUTICA_RESEARCHER
Domain: External reference research / codebase foundation comparison
Mode: RESEARCH ONLY. No runtime code changed. No proprietary Subnautica assets/code copied, parsed, decompiled, or extracted.

### What was wrong

1. Several earlier claims were stale after parallel project edits. The largest correction is audio: the big root ambience WAVs are no longer imported as DecompressOnLoad/preload=1. Current metas show `Underwater Ambient.wav` and Atmos loops as Streaming (`loadType: 2`) with `preloadAudioData: 0`.
2. ContentAuthority has real build validators, but the payload state is still empty. `Assets/AddressableAssetsData` exists with 0 files, `Assets/_SourceData` exists with 0 files, `Assets/StreamingAssets` is missing, and no `ContentAssetHashMap` or `ContentVfxPrewarmManifest` assets were found.
3. DataMonolith has a compiler, but shipping boot still accepts a missing blob. `GameBootstrapper.InitializeBootstrapDataMonolith` returns true for `H8DataBlobLoadStatus.Missing` because it calls `TryInitializeFromStreamingAssets(... failIfMissing:false ...)`.
4. `H8StaticDataArena.TryReadWholeFileIntoArena` still stages the entire blob through `File.ReadAllBytes`, causing a managed byte[] copy before native blit and lacking Android/Quest StreamingAssets/JAR proof.
5. Mod SDK still emits a manifest the runtime rejects. `ModLoader.CurrentAPIVersion = 2` and rejects `RequiredAPIVersion <= 0`; `ModBuilderWindow.ModManifestData` does not emit RequiredAPIVersion or ModPriority.
6. World cache vocabulary is still too narrow. `H8WorldPagePayloadTypes` names only `VoxelDeltaRle`, `InventoryState`, `ChunkDehydratedMetadata`, and `WfcOutpostState`. It does not name base-world cache lanes equivalent to terrain cells, object batches, visibility/physics proxy, audio biome banks, or discovery-route payloads.
7. `ObjectBatchBase` and `VisibilityProxyBase` exist as abstract scaffolds only. No concrete derived classes or assets were found in current source/assets.
8. H-Phi/DataVault purity is not solved. Static audit shows broad native ownership and managed surface despite zero raw Unity Update methods.

### What was done

- Re-read `AGENTS.md`, `Docs/Actual Domains of Project.txt`, task status/rationale, active batch search for `SUBNAUTICA_RESEARCHER`, and relevant mandates: Addressables lifecycle, native memory/jobs, and crash telemetry.
- Deep-read `ContentAuthorityBuildValidators.cs`, `ContentAssetHashMap.cs`, `ContentRuntimeServices.cs`, `ObjectBatchBase.cs`, `VisibilityProxyBase.cs`, `H8DataMonolithCompiler.cs`, `H8StaticDataArena.cs`, `H8DataMonolithTypes.cs`, and `GameBootstrapper.cs`.
- Deep-read `AudioImportDictator.cs`, `ModBuilderWindow.cs`, and `ModLoader.cs`.
- Re-counted current HECTON-8 payload state:
  - `Assets/AddressableAssetsData`: 0 files.
  - `Assets/_SourceData`: 0 files.
  - `Assets/StreamingAssets`: missing.
  - `ContentAssetHashMap`: 0 assets found.
  - `ContentVfxPrewarmManifest`: 0 assets found.
- Re-counted current audio metas:
  - 101 clips: `loadType=2, preload=0`.
  - 28 clips: `loadType=0, preload=1`.
  - 7 clips: `loadType=0, preload=0`.
  - 2 clips: `loadType=1, preload=0`.
  - 19 metas: no direct load/preload key pair.
  - Remaining preloaded source bytes: about 4.67 MB, mostly short SFX/footsteps/UI/thruster/movement.
- Re-counted local Subnautica clean-room taxonomy:
  - `StreamingAssets/aa/StandaloneWindows64`: 5,467 bundle files, 4,675,241,727 bytes, largest 201,107,619 bytes.
  - Addressables catalog: 12,016,061 bytes, 5,467 bundle references, provider families include AssetBundleProvider, BundledAssetProvider, SceneProvider, LegacyResourcesProvider.
  - `SNUnmanagedData/Build18/BatchObjectsCache`: 2,975 files / 3,218,027 bytes.
  - `SNUnmanagedData/Build18/CellsCache`: 1,606 files / 167,561,255 bytes.
  - `SNUnmanagedData/Build18/CompiledOctreesCache`: 5,416 files / 1,203,085,204 bytes.
  - Build18 root sidecars: `biomeMap.bin`, `biomes.csv`, `index.txt`, `meta.txt`, `signals.csv`.
  - Local save slot `slot0000`: 25 `CellsCache` zip files / 21,836,090 bytes plus `global-objects.bin`, `scene-objects.bin`, `gameinfo.json`, screenshot, and timecapsules.
- Ran H-Phi audit:
  - RuntimeHPhiNarrow: 0.065373415.
  - RuntimeHPhiRisk: 0.004501506.
  - AllSourceHPhiNarrow: 0.058738381.
  - DataSovereignty: 0.123322148.
  - MemoryAlignment: 0.53010279.
  - BinarySafeRatio: 0.021536955.
  - NativeArrayRefs: 7315.
  - DataVaultRefs: 1029.
  - ManagedFormatSurface: 539.
  - JobCompleteSurface: 73.
  - UnityUpdateMethodsRaw / UnityUpdateMethods: 0 / 0.
- Refreshed compile proof:
  - Initial no-restore Editor build failed because `Temp/obj/Hecton8.Editor/project.assets.json` was missing.
  - Normal `dotnet build Hecton8.Editor.csproj -v:q /clp:ErrorsOnly /m:1` succeeded with 48 warnings / 0 errors in 00:02:28.31.
  - Follow-up `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal /m:1` succeeded with 0 warnings / 0 errors in 00:02:23.78.
  - `dotnet build Hecton8.Core.csproj --no-restore -v:q /clp:ErrorsOnly` succeeded with 0 warnings / 0 errors in 00:00:03.56.

### Cinematic cheats / tactical borrow list

- Borrow taxonomy, not assets/code: Subnautica separates Addressables bundles, static base-world caches, and save-slot deltas. HECTON-8 should preserve that separation instead of treating save deltas as authored world payload.
- Low/toaster lane: procedural fallback plus sparse deltas, tiny Core content map, short SFX preload only, streamed ambience, cheap discovery-route hashes, LUT/dot-product visual lies.
- Middle lane: generated `static_data.h8bin`, ContentAssetHashMap, Core/High_Res groups, object-batch sector payloads, route validators promoted from warnings to build gates.
- High lane: visibility/physics proxy payloads, audio biome banks, BRG object batches, read-coalesced sector pages, platform-specific compute gates.
- Ultra lane: overkill prop density, volumetric silt, visor salt crystals, high-tier POM/raymarch/VFX, isolated in Overkill group and optional monolith sections so MX350/Quest builds are not poisoned.

### Exact microseconds saved

0us measured. This was a research and verification pass only. No runtime code changed. Any future savings from object batches, DataMonolith loader changes, Addressables payload generation, or mod manifest fixes require Unity profiler, Memory Profiler, Addressables build, and player-platform proof.

### Proof limits

- Dotnet build proves C# project compilation only.
- No Unity import, domain reload, Play Mode, Addressables build, Android/Quest IL2CPP, Metal shader compile, Steam Deck MicroSD I/O, or runtime profiler proof was produced in this pass.
- Subnautica inspection stayed at file/catalog taxonomy level. No proprietary binary payloads were parsed and no proprietary assets/code were copied.

### P0 queue from this pass

1. Generate minimal Addressables settings/groups and a `ContentAssetHashMap`/`ContentVfxPrewarmManifest` asset set, or formally disable the ContentAuthority build gate for non-production configs only.
2. Add a DataMonolith prebuild freshness gate: required source-data existence, required `static_data.h8bin`, version/hash match, and Android/Quest-safe loader path.
3. Fix ModBuilder manifest v2 output: emit `RequiredAPIVersion = 2` and `ModPriority = 0`, then build/load a test content-only mod.
4. Add base-world payload constants/records: `TerrainCellBase`, `ObjectBatchBase`, `VisibilityPhysicsProxyBase`, `AudioBiomeBank`, `DiscoveryRouteBase`.
5. Create concrete object-batch/visibility-proxy asset classes and a small generated payload sample before wiring more world content.
6. Promote first-hour recipe scan-gate missing-route warnings to build-blocking once bootstrap-generated routes are represented as build-verifiable assets.
7. Classify H-Phi native ownership exceptions: vault-owned shared state, singleton infrastructure, mod-facing managed bridges, and true migration targets.

## TENTH PASS - BUILD-GATE / STATIC-DATA / H-PHI ARBITRATION - 2026-05-17

### What was wrong

1. Validator coverage is mixed. `ContentAuthorityBuildPreprocessor` is a real build gate and calls `ContentAuthorityBuildValidators.RunAllBuildValidators()` at callback order -9000. `ContentSanityValidator` and `ScanIntelValidator` are menu-only. Missing recipe scan routes currently produce `RecipeScanGateWarningCount`, not a build-blocking error.
2. Static data is split. `Data/Balance/Baked/H8StaticData.bin` and `Babel_Dictionary.h8bin` exist, but the boot monolith `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is still absent.
3. `H8DataBaker` and `H8DataMonolithCompiler` are not the same contract. The existing Balance CSVs satisfy the smaller baker, but the monolith compiler treats `Data/Balance` as hash-authoritative and requires `hash32` pairs for `Id` fields. Current `Items.csv`, `Fauna.csv`, `Economy.csv`, and `Physics.csv` have no `hash32` column.
4. Addressables is installed, not populated. `com.unity.addressables` 2.7.6 is in `Packages/manifest.json`; `AddressablesCompatibility.cs` is intentionally empty; `Assets/AddressableAssetsData` has 0 files. ContentAuthority expects `Core`, `High_Res`, and `Overkill` groups, but no complete project bootstrap creates the full settings/group/hash-map/VFX-manifest payload.
5. H-Phi is still architecture debt, not just rhetoric. Current static audit improved slightly, but the top native-risk files still carry 0 DataVault refs and large local NativeContainer ownership.
6. Black-box coverage is uneven. `VoxelDeltaProcessor`, `LogisticsNetworkGraph`, and `ProceduralWreckGenerator` have 300-frame dump paths. `HectonMapMagicVegetationBridge` declares `AbyssalPathTelemetryEntry` and counters, but no writer/dump usage was found. `SubmarineAtmosphereSystem` and `DestructibleOrganicManager` expose critical state and native pools without a 300-frame system black-box found in this pass.

### What was done

- Re-read current Status/Rationale and relevant mandates before continuing.
- Traced build gate coverage:
  - `ContentAuthorityBuildValidators.RunAllBuildValidators()` blocks Resources.Load usage, Addressables settings/groups, hash-map integrity, wrong tier group assignment, binary layout drift, object batch payload errors, lore I/O budget violations, runtime prefab binding issues, compute thread groups above 1024, and VFX prewarm manifest issues.
  - `ContentSanityValidator` remains menu-only at `Hecton-8/Validate Content`.
  - `ScanIntelValidator` remains menu-only at `Hecton/Validation/Validate Scan Intel` and validates only the active scene.
- Audited static-data reality:
  - `Data/Balance/Items.csv`, `Fauna.csv`, `Economy.csv`, and `Physics.csv` exist and contain small v1.2 sample data.
  - `Data/Balance/Baked/H8StaticData.bin` = 896 bytes.
  - `Data/Balance/Baked/Babel_Dictionary.h8bin` = 1284 bytes.
  - `Assets/_SourceData` is still empty.
  - `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is absent.
  - `H8StaticDataArena.TryReadWholeFileIntoArena()` still uses `File.ReadAllBytes` before native blit.
- Audited Addressables:
  - Package installed: `com.unity.addressables` 2.7.6.
  - Settings payload absent: `Assets/AddressableAssetsData` contains 0 files.
  - Compatibility shim is intentionally empty because the package exists.
- Re-ran current H-Phi audit at `2026-05-17 00:36:33 +04:00`:
  - RuntimeHPhiNarrow: 0.068171189.
  - RuntimeHPhiRisk: 0.004701846.
  - AllSourceHPhiNarrow: 0.061251437.
  - AllSourceHPhiRisk: 0.003883613.
  - DataSovereignty: 0.128544198.
  - MemoryAlignment: 0.530332681.
  - BinarySafeRatio: 0.021526419.
  - AupPrecisionIntegrity: 1.
  - RuntimeFiles: 1344.
  - RuntimeLines: 920269.
  - SignalBusPush: 421.
  - GlobalRegistrySurface: 5303.
  - EventPublish: 26.
  - UnityUpdateMethodsRaw / UnityUpdateMethods: 0 / 0.
  - DataVaultRefs: 1079.
  - NativeArrayRefs: 7315.
  - ManagedFormatSurface: 539.
  - JobCompleteSurface: 73.
  - PrimaryNativeOwnershipRisk: 5832.
- Inspected top H-Phi native ownership risk seams:
  - `World/HectonMapMagicVegetationBridge.cs`: 166 NativeArray refs in audit, 0 DataVault refs; owns vegetation/threat/flow/HLOD/megwreck/path memory via local `VegetationNativeMemory`; declares abyssal telemetry but no write/dump usage found.
  - `Power/LogisticsNetworkGraph.cs`: 145 NativeArray refs, 0 DataVault refs; owns power graph/publish buffers; 300-frame `Dump_LOGI_POWER_ROUTING.bin` exists.
  - `SubmarineAtmosphereSystem.cs`: 132 NativeArray refs, 0 DataVault refs; owns room gas/pressure/temperature arrays and pressure event queues; no 300-frame atmosphere black-box found.
  - `World/DestructibleOrganicManager.cs`: 125 NativeArray refs, 0 DataVault refs; owns per-flora NativeHashMaps for health, destroyed, regrowth, maturation, acoustic cadence, runtime flags; no 300-frame organic black-box found.
  - `VoxelDeltaProcessor.cs`: 92 NativeArray refs, 0 DataVault refs; owns carve queue/snapshot/compaction buffers; 300-frame `Dump_WORLD_VOXEL_CAVING.bin` exists.
  - `World/ProceduralWreckGenerator.cs`: 66 NativeArray refs, 0 DataVault refs; owns WFC/debris/artifact/collision/burial buffers; 300-frame `Dump_WORLD_WRECKAGE.bin` exists.
  - `World/VegetationFlowFieldIntegrator.cs`: 107 NativeArray refs, 0 DataVault refs; partial class writes flow/threat/thermal/native path lanes owned by `HectonMapMagicVegetationBridge`.

### Cinematic cheats / tactical borrow list

- Borrow Subnautica taxonomy, not proprietary payloads: distinct static world caches, bundle catalog, sidecar metadata, and save deltas.
- Low lane: one authoritative static-data path, tiny Core Addressables group, streamed ambience, hashed scan-route proof, and procedural fallbacks.
- Middle lane: build-fresh `static_data.h8bin`, generated `ContentAssetHashMap`, concrete object-batch and visibility-proxy payload samples, and scan-route warnings promoted after generated routes are asset-visible.
- High lane: BRG object batches, visibility/physics proxy cache, audio biome banks, coalesced sector reads, and current H-Phi shared-state snapshots moved to vault-owned lanes.
- Ultra lane: Overkill Addressables group and optional monolith sections for dense wreck dressing, volumetric silt, visor salt crystals, high-tier POM/raymarch/VFX, without poisoning MX350/Quest builds.

### Exact microseconds saved

0us measured. This pass changed documentation only. No runtime code or content assets were modified. Future savings from object batching, DataMonolith streaming, or DataVault migration require Unity profiler, Memory Profiler, Addressables build, and player-platform proof.

### Proof limits

- This pass did not run Unity import, Play Mode, Addressables build, Android/Quest IL2CPP, Metal shader compile, Steam Deck storage tests, or runtime profiling.
- H-Phi audit is static-source evidence. It proves risk shape, not runtime leak or frame cost.
- Subnautica remained clean-room file taxonomy only. No proprietary Subnautica binary payloads were parsed, copied, or decompiled.

### P0 queue refined

1. Decide which static-data path is authoritative. If `static_data.h8bin` is the boot contract, add hash columns or schema reconciliation so `Data/Balance` can feed `H8DataMonolithCompiler`.
2. Add a prebuild DataMonolith freshness gate: fail if required source changed and `static_data.h8bin` is absent/stale/empty.
3. Generate minimal Addressables settings plus `Core`, `High_Res`, and `Overkill` groups, then generate at least one `ContentAssetHashMap` and `ContentVfxPrewarmManifest` asset.
4. Promote `ContentSanityValidator` and `ScanIntelValidator` route checks into a build/preplay gate after bootstrap-generated scan routes are represented as assets or monolith route records.
5. Add missing black-box rings/dumps for Atmosphere, DestructibleOrganic, and the abyssal path/vegetation flow slice.
6. Move published shared snapshots, not local scratch, from the top H-Phi files into DataVault/typed lanes: threat grid, flow field, power node state, atmosphere room state, flora lifecycle state.
7. Keep local scratch NativeContainers where they are private job work buffers, but document them as accepted owner exceptions with sentinel lifetime and no forced `.Complete()` hot-path proof.

Post-report compile proof:
- Core: `dotnet build Hecton8.Core.csproj --no-restore -v:q /clp:ErrorsOnly` succeeded with 0 warnings / 0 errors in 00:01:17.81.
- Editor: `dotnet build Hecton8.Editor.csproj --no-restore -v:q /clp:ErrorsOnly /m:1` succeeded with 47 warnings / 0 errors in 00:00:50.44.
- Proof limit: C# project compilation only. No Unity import, playmode, player build, IL2CPP, Android/Quest, Metal, or Steam Deck proof claimed.

---

## ELEVENTH PASS - SUBNAUTICA 2 UE5 / EARLY ACCESS VISUAL REFERENCE - 2026-05-17

Agent: SUBNAUTICA_RESEARCHER
Domain: External reference research / codebase foundation comparison
Mode: RESEARCH ONLY. Official web/source comparison plus screenshot inspection. No runtime code changed.

### What was wrong

1. User phrasing said Subnautica 2 "вышла". Current verified state is Early Access / Xbox Game Preview from 2026-05-14, not final 1.0.
2. Screenshot comparison alone is dangerous. The visible rendering surface is catchable, but the real competitive bar is content density, co-op, platform presets, creature reactivity, base building, save/versioning, and Early Access cadence.
3. UE5 is not magic in the screenshots. The official stills show strong art direction: color fog, clean silhouettes, stylized flora, modular base forms, particles, caustics, and readable co-op/vehicle composition.

### What was done

- Checked official Unknown Worlds Early Access/Roadmap pages.
- Checked Steam app 1962700 store/API data: Early Access release date, features, screenshots, platforms, and system requirements.
- Checked Xbox Wire Game Preview article for Xbox/PC/Game Pass/ROG Xbox Ally/performance preset context.
- Checked KRAFTON press material for UE5 creature AI claims: behavior trees, stimulus systems, and simulated tentacle animation for Collector Leviathan.
- Downloaded and visually inspected six official 1920x1080 Steam screenshots:
  - screenshot 0: underwater base, heavy haze, coral/flora clusters, bright module lights, readable white/yellow base forms.
  - screenshot 1: interior base room, glossy modular panels, pool/vehicle display, clean lighting, rounded sci-fi forms.
  - screenshot 2: co-op underwater exploration, bright shallow-water biome, rock arches, dense yellow/orange flora clusters, readable multi-player scale.
  - screenshot 3: darker biome, scan/tool composition, creature silhouettes, particulate fog, bioluminescent white/blue clusters.
  - screenshot 4: deeper blue biome, large purple anemone forms, vehicle cockpit framing, caustic floor lighting.
  - screenshot 5: orange hostile/thermal biome, silhouette staging, particle embers/bubbles, vehicle trail, strong monochrome mood band.

### Facts gathered

- Steam: Subnautica 2, developer/publisher Unknown Worlds Entertainment, Early Access Release Date 14 May 2026, Windows only on Steam, DirectX 12, 50 GB storage, optional online co-op and cross-platform multiplayer categories.
- Unknown Worlds: Early Access starts 05.14.26; roadmap points to new biomes, creatures, craftables, story and feature expansions through Early Access.
- Xbox Wire: Game Preview availability on Xbox Series X|S, Xbox on PC, ROG Xbox Ally/Ally X, Xbox Game Pass Ultimate, and PC Game Pass; article mentions Unreal Insights and ROG Ally graphics presets.
- KRAFTON press: Collector Leviathan described with Unreal Engine 5 behavior trees, stimulus systems reacting to light/sound/player actions, and simulated tentacle animation.
- Secondary market signal: 2026-05-15 reports said Unknown Worlds/KRAFTON announced 2 million Early Access copies in 12 hours and about 651,000 peak concurrent players across Steam/Epic/Xbox. Treat as market signal, not technical proof.

### Screenshot verdict

HECTON-8 can chase the screenshot surface. No official still required impossible rendering. The look is mostly controllable composition and cheap-perceptual tricks: fog color bands, strong silhouettes, stylized clusters, local emissive accents, particles, caustics, and mood-biome palettes. The danger is production discipline: authoring enough varied biomes/creatures/base pieces, keeping co-op state stable, and shipping platform presets without poisoning low hardware.

### Cinematic cheats / tactical borrow list

- Low/toaster: 1D depth/fog LUT, triangle-noise silt, billboard flora islands, baked/projected caustics, fixed bubble sheets, cheap emissive accent masks.
- Middle: streamed biome object batches, authored color-fog volumes, base-piece silhouette library, scan-route content packs, Tadpole/vehicle readability equivalents.
- High: reactive fauna via typed stimulus lanes, layered silt wakes, denser flora sway, better material normals/POM only near camera.
- Ultra: visor salt crystals, volumetric silt in wake, procedural hull dents, dense abyssal noir lighting, Overkill-only VFX/particles isolated from MX350/Quest tiers.

### Exact microseconds saved

0us measured. Research-only pass. No runtime code/content changed. Future savings require implementation and profiler proof.

### Proof limits

- Screenshots were official Steam stills, not live frame captures or profiler captures.
- No Subnautica 2 files were extracted or reverse engineered.
- No Unreal project internals were inspected. UE5 details are limited to official/press statements.
- No HECTON-8 compile was run in this pass because only documentation/research files were touched.

### Sources

- https://unknownworlds.com/en/news/subnautica-2-early-access-released
- https://unknownworlds.com/en/news/subnautica-2-early-access-roadmap
- https://store.steampowered.com/app/1962700/Subnautica_2/
- https://news.xbox.com/en-us/2026/05/04/subnautica-2-game-preview/
- https://press.krafton.com/en-GB/UNKNOWN-WORLDS-REVEALS-THE-COLLECTOR-LEVIATHAN-IN-SUBNAUTICA-2

---

## TWELFTH PASS - SUBNAUTICA 2 LAUNCH RECEPTION / COMPETITIVE THREAT MODEL - 2026-05-17

Agent: SUBNAUTICA_RESEARCHER
Domain: External reference research / codebase foundation comparison
Mode: RESEARCH ONLY. Web/source comparison; no runtime code changed.

### What was wrong

1. Screenshot-only comparison is too shallow. The visible rendering is catchable, but the production system around it is the real threat.
2. Subnautica 2 should not be treated as a finished 1.0 benchmark. It is a high-traction Early Access/Game Preview product, which means content cadence and feedback loop matter as much as current content.
3. HECTON-8 cannot win by being a darker clone. It needs harsher systems, better stability discipline, and a distinct audiovisual identity.

### What was done

- Re-checked current post-launch press/impression coverage after the 2026-05-14 Early Access launch.
- Preserved prior Steam screenshot inspection: six official 1920x1080 screenshots were already downloaded and inspected.
- Compared visual surface, systemic promise, co-op/save model, platform budget, review friction, and HECTON-8 counter-position.

### Additional findings

- PC Gamer reports the developers described Subnautica 2 as bigger and more polished than previous Unknown Worlds Early Access launches, with open development/community feedback as a core operating model.
- PCGamesN impressions point to a strong first-ten-hours survival/exploration loop, dread, base/equipment progression, and visible under-construction Early Access boundaries.
- PC Gamer co-op guide confirms up to four players, singleplayer saves can become multiplayer worlds, friends can join/leave, but no character import into another player world, blunt guest base-editing permissions, and no revive system at launch.
- GamesRadar reports secondary launch signal: 2 million Early Access copies in 12 hours and about 651,000 peak concurrent players across PC/Xbox ecosystem. Treat as market signal, not technical proof.
- Negative-review themes in public coverage include EULA/ToS pushback, missing Early Access features, and comfort/settings requests such as FOV complaints. This is a tactical opening for HECTON-8 polish/comfort discipline.

### Competitive threat model

1. Visual surface: catchable with HECTON-8 fog, caustics, object batches, particles, emissives, biome palettes, and composition discipline.
2. Loop density: dangerous. Their first-hour survival/crafting/exploration readability is the bar we must beat with pressure, sonar, wreck salvage, atmosphere, and noir dread.
3. Co-op/save state: dangerous if we later add co-op. Even if HECTON-8 stays singleplayer initially, the save/persistence contract must not block future shared worlds.
4. Platform presets: dangerous. They already talk Xbox/ROG Ally preset work. HECTON-8 needs low/mid/high/ultra settings as real content-budget gates, not cosmetic toggles.
5. Feedback cadence: dangerous. Early Access community machinery can outrun better tech if our data/mod/content pipeline remains brittle.
6. Identity: opportunity. Subnautica 2 is bright alien-ocean adventure; HECTON-8 should own deep-sea noir, industrial horror, pressure, acoustic threat, hull damage, silt, salt, and black-box systems.

### Tactical response for HECTON-8

- Low/toaster: readable first-hour route, hard FOV/comfort settings, cheap fog LUTs, dithered particles, billboard flora/silt, stable save schema.
- Middle: DataMonolith route/content pipeline, Addressables/content-authority payloads, object-batch biome dressing, platform preset gates.
- High: pressure/atmosphere/acoustic systems with black-box telemetry, reactive fauna via typed stimulus lanes, dense but controlled VFX.
- Ultra: visor salt crystals, volumetric silt wakes, procedural hull dents, overkill abyssal light shafts, high-tier POM/raymarch only in isolated Overkill packs.

### Exact microseconds saved

0us measured. Research-only pass. No runtime code/content changed.

### Proof limits

- Press/impression coverage is not profiler evidence.
- Launch sales/concurrency are market signals, not quality proof.
- Screenshots are official stills, not captured runtime frame analysis.
- No Subnautica 2 files, assets, or Unreal internals were inspected.

### Sources

- https://www.pcgamer.com/games/survival-crafting/subnautica-2-devs-say-its-bigger-and-more-polished-than-any-of-the-studios-previous-early-access-launches/
- https://www.pcgamer.com/games/survival-crafting/subnautica-2-multiplayer-co-op-guide/
- https://www.pcgamesn.com/subnautica-2/early-access-impressions
- https://www.gamesradar.com/games/survival/subnautica-2-makes-a-splash-with-2-million-copies-sold-in-12-hours-18-000-positive-steam-reviews-and-651-000-concurrent-players-across-pc-and-xbox/
- https://store.steampowered.com/app/1962700/Subnautica_2/

Dossier output:
- Created `Docs/Reports/SUBNAUTICA_2_UE5_REFERENCE_DOSSIER.md`.
- Contents: verified facts, screenshot audit, visual threat analysis, real threat analysis, borrow/do-not-borrow list, HECTON-8 counterposition, tactical P0/P1/P2 tasks, proof limits, and sources.
- Runtime impact: 0us. Documentation-only pass.

---

## THIRTEENTH PASS - NON-AGENT SUBNAUTICA 2 DOC PROMOTION - 2026-05-17

Agent: SUBNAUTICA_RESEARCHER
Domain: External reference research / codebase foundation comparison
Mode: RESEARCH/DOCUMENTATION ONLY. No runtime code changed.

### What was wrong

1. The Subnautica 2 research existed in reports and logs, but the user asked for important findings to be documented outside `AgentLogs` and `Tasks`.
2. The project needed a sharper dream counterposition, not another feature-parity checklist.
3. Screenshot observations needed to become fake-first rendering tactics tied to HECTON-8 tier contracts.
4. Tactical findings needed a stable architecture backlog so integrators can convert research into build gates and payload work.

### What was done

- Created `Docs/Design/HECTON8_DREAM_VS_SUBNAUTICA2_COUNTERPOSITION.md`.
  - Captures HECTON-8 identity: NASA-punk / deep-sea noir engineering survival.
  - Defines dream pillars: visible pressure, visibility collapse, acoustic threat, industrial wrecks, expensive player instruments.
  - Defines first-hour route expectations and low/middle/high/ultra tier contract.
  - Lists foundation blockers: production monolith, ContentAuthority payloads, first-hour route gate, biome authority, typed stimulus lanes, black boxes, platform budgets.

- Created `Docs/Design/SUBNAUTICA2_SCREENSHOT_VISUAL_CHEATS.md`.
  - Converts six official screenshot surfaces into HECTON-8 visual-fake tactics.
  - Maps base haze, interior readability, shallow density, scanner darkness, vehicle framing, and thermal palette into fake-first approaches.
  - Defines cheap Low-tier carriers and Overkill high-tier targets.
  - Defines required biome visual authority fields and build gates.

- Created `Docs/ARCHITECTURE/SUBNAUTICA2_TO_HECTON8_TACTICAL_BACKLOG.md`.
  - Converts research into P0/P1/P2 architecture tasks.
  - P0: `static_data.h8bin`, ContentAuthority payload generation, first-hour route gate, biome visual authority, black-box coverage, comfort settings.
  - P1: typed creature stimulus lanes, object-batch world dressing payloads, save/schema migration harness, platform preset matrix.
  - P2: Overkill visual pack, feedback ingestion loop, co-op-ready state boundaries.

### Cinematic cheats used

- 1D depth/fog LUT instead of full volumetric truth on low hardware.
- Triangle-noise silt and fixed particle sheets instead of per-particle fluid simulation.
- Billboard/impostor flora and object batches instead of spawned GameObject ecology.
- Projected caustic decals and animated sheets instead of expensive caustic volume on low tiers.
- Scalar pressure/hull stress driving decals, audio, haptics, and visor masks instead of continuous physical deformation.
- Overkill-only raymarch/POM/silt/visor features isolated from gameplay truth.

### Exact microseconds saved

0us measured. Documentation-only pass. No runtime code, assets, scenes, prefabs, or project settings changed.

### Proof limits

- No Unity import, Play Mode, profiler, Frame Debugger, Memory Profiler, or player build was run in this pass.
- No Subnautica 2 files, binaries, assets, or Unreal internals were inspected.
- The new documents are architecture/design targets, not runtime readiness proof.

---

## FOURTEENTH PASS - SOURCE-BACKED IMPLEMENTATION HANDOFF - 2026-05-17

Agent: SUBNAUTICA_RESEARCHER
Domain: External reference research / codebase foundation comparison
Mode: RESEARCH/DOCUMENTATION ONLY. No runtime code changed.

### What was wrong

1. The tactical backlog identified the right pressure points, but it was still too abstract for integration work.
2. Some HECTON-8 systems have real code but no populated payloads, which can be misread as readiness.
3. First-hour, ContentAuthority, visual tier, and platform work needed exact file targets and gates.

### What was done

- Read current file evidence for:
  - DataMonolith compiler, arena, bootstrap, and Balance CSV compatibility.
  - ContentAuthority build validators, runtime hash map/VFX/tier code, and empty Addressables project data.
  - ObjectBatchBase and VisibilityProxyBase contracts.
  - FirstHourDirector, ContentSanityValidator, ScanIntelValidator, ScanLogSystem, quest and recipe assets.
  - Biome runtime visual profile inventory.
  - HectonVisualOverkillContract, ContentTieredGroupPolicy, and ThermalDynamicResolutionAdapter.
  - AcousticEchoLocationRuntime, SargassumMicroFaunaBoids, SignalBus usage, and legacy GlobalSignals/event-bus surfaces.
  - GlobalTelemetryBus, CrashTelemetryBuffer, BlackBoxHeartbeatThread, and platform compatibility audit.

- Created `Docs/ARCHITECTURE/SUBNAUTICA2_HECTON8_IMPLEMENTATION_HANDOFF.md`.
  - Maps every tactical lesson to current source files.
  - Separates real implementation, missing payloads, and missing build gates.
  - Defines P0 work orders: monolith build gate, ContentAuthority payload bootstrap, first-hour route verifier, object-batch world dressing, biome visual authority gate, stimulus lane cleanup, and platform matrix proof.
  - Defines P1 work: save/schema cadence, comfort/trust, feedback ingestion, and co-op-ready state boundaries.
  - Records explicit non-goals: no proprietary copying, no return to standard Addressables as world architecture, no Overkill route dependency, no menu-only validator proof.

### Cinematic cheats used

- Low tier: 1D fog/depth LUTs, triangle-noise silt, billboard clusters, sparse silhouettes, projected caustics.
- Middle: authored biome packs, route-proven gameplay, object-batch density.
- High: typed creature stimuli, platform-tuned VFX, silt wakes, hull-dent presentation.
- Ultra: optional Overkill pack for visor salt, volumetric silt, procedural hull dents, high-tier POM/raymarch/SSS.

### Exact microseconds saved

0us measured. Documentation-only source audit. No runtime code, assets, scenes, prefabs, project settings, or import settings changed.

### Proof limits

- No Unity import, Play Mode, profiler, Memory Profiler, Frame Debugger, or player build was run.
- No Android/Quest, macOS/Metal, Linux/Steam Deck, or high-end PC Overkill device validation was run.
- The handoff is a work map, not runtime readiness proof.

## FIFTEENTH PASS - P0 PROOF MATRIX AND PUBLIC MOD ECOSYSTEM DEEP-DIVE - 2026-05-17

What was wrong -> The research lane had strong findings scattered across chat, archived logs, and prior handoff docs, but the current anti-amnesia files were missing from `Docs/Tasks` and `Docs/AgentLogs`. Foundation readiness was still easy to overstate because validators and abstract contracts existed while actual payload files were absent. Modding looked safer than classic Subnautica modding philosophically, but the SDK/runtime manifest contract still rejected SDK-built packages unless manually repaired.

What was done -> Restored the SUBNAUTICA_RESEARCHER Status/Rationale/LOG from `Docs/Archive/Batch007` into the live docs path. Reconfirmed current payload facts: 0 files in `_SourceData`, `StreamingAssets`, and `AddressableAssetsData`; 0 authored ContentAssetHashMap, ContentVfxPrewarmManifest, ObjectBatchBase, and VisibilityProxyBase payload assets found; 216 biome runtime visual profiles found. Rechecked ModLoader API version and rejection paths against ModBuilderWindow emitted fields. Opened current Steam Subnautica 2 and public Nautilus/Nitrox/TerrainPatcher/BepInEx.Subnautica/QModManager sources. Created two stable project docs: `Docs/ARCHITECTURE/HECTON8_P0_FOUNDATION_PROOF_MATRIX.md` and `Docs/Reports/SUBNAUTICA_PUBLIC_MOD_ECOSYSTEM_DEEPDIVE.md`.

Cinematic Cheats used -> Documentation only. The tactical visual conclusion remains fake-first: Core gets fog LUTs, dither/triangle-noise particles, impostor flora, projected caustics, and strict silhouettes; Overkill gets optional visor salt, volumetric silt wake, procedural hull dents, high-tier POM/raymarch/SSS/particles. No runtime shader or gameplay code changed in this pass.

Exact Microseconds saved -> 0us measured. This pass prevents false readiness claims and creates implementation targets. Any future performance savings require implementing the gates/payloads and profiling on low-end, Steam Deck, Android/Quest, Metal/Mac, and high-end PC.

Proof limits -> No Unity import, Addressables build, Android/Quest IL2CPP build, Metal shader compile, Steam Deck storage test, or Memory Profiler capture was run. No proprietary Subnautica payload was parsed or copied. Public GPL/AGPL projects were treated as taxonomy/reference only, not source to import.
