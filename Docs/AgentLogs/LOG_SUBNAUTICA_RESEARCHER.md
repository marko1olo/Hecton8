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
- Previous broad statement “cannot borrow” was too coarse. Correct boundary: study everything legally observable; borrow architecture/patterns; do not copy proprietary assets/code/text/audio or GPL/AGPL code into a non-compatible codebase.

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