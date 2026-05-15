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
