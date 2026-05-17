# Subnautica 2 EA To HECTON-8 Production Contracts

Date: 2026-05-17
Owner: SUBNAUTICA_RESEARCHER
Scope: clean-room public reference research plus current HECTON-8 source audit.
Runtime changes: none.

## Legal Boundary

Do not copy Subnautica or Subnautica 2 assets, binaries, decompiled code, shader code, save payloads, or proprietary data. Borrow only public contract shapes, file taxonomy lessons, tool categories, and release-process pressure.

Allowed inputs used here:

- Official Unknown Worlds roadmap: https://unknownworlds.com/en/news/subnautica-2-early-access-roadmap
- Official Steam app page: https://store.steampowered.com/app/1962700/Subnautica_2/
- Public mod ecosystem references: Nautilus, Nitrox, BepInEx.Subnautica, TerrainPatcher, QModManager.
- Current local HECTON-8 source and docs.

## External Reading

Subnautica 2 is Early Access, not 1.0. Steam lists release and Early Access release date as 14 May 2026. Steam describes single-player, online co-op, cross-platform multiplayer, Steam Cloud, 4-player co-op copy, DirectX 12, and Early Access plans for more biomes, creatures, craftables, features, and narratives.

Unknown Worlds' first public roadmap sequence is the important production signal:

1. EA launch.
2. EA 1.1 quality-of-life update:
   - Biomods system.
   - Blight encounters.
   - Wrecks gameplay.
   - Vehicle docking and fabrication.
   - PDA databank.
   - Voicelog priority system.
   - More passive biomod slots.
   - Storage cache.
   - Sprint.
3. EA 1.2 co-op centric update:
   - HUD signals.
   - Base Builder Tool.
   - Pinned recipes system.
   - Voice chat.
   - Emotes.
   - Player trading.
   - Player revive.
   - Additional customizations.
4. Future major expansion updates:
   - Expand the world.
   - New biomes.
   - New creatures.
   - New resources.
   - New tools.
   - New vehicle.
   - Next chapter of the story.
5. Continuous bug fixes, balance tuning, optimization, feedback intake.

Tactical meaning for HECTON-8: do not chase screenshots first. The competitive foundation is update cadence plus save/schema stability, content packaging, co-op-safe state, feedback telemetry, and route density.

## HECTON-8 Current Proof Snapshot

### Static Content

Current disk proof:

- `Assets/_SourceData`: 0 files.
- `Assets/StreamingAssets`: missing in the current checkout during this pass.
- `Assets/AddressableAssetsData`: 0 files.
- `ContentAssetHashMap` assets: 0.
- `ContentVfxPrewarmManifest` assets: 0.
- `ObjectBatchBase` assets: 0.
- `VisibilityProxyBase` assets: 0.
- `Assets/_Project/Data/Biomes/RuntimeVisualProfiles`: 216 `.asset` files.

The code scaffold exists. The shipped payload does not.

Relevant source:

- `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompiler.cs`
  - `SourceFolder = "Assets/_SourceData"`.
  - `BalanceSourceFolder = "Data/Balance"`.
  - output path `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
  - `ValidateCsvRowHashes` requires `id/hash32` pairs for balance rows.
- `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs`
  - loads `Hecton8/DataMonolith/static_data.h8bin` from `Application.streamingAssetsPath`.
  - current boot path tolerates missing file through `GameBootstrapper`.
- Current `Data/Balance/*.csv` headers have `Id` but no `hash32`.

Contract gap: HECTON-8 has a monolith reader/compiler shape, but no proved mandatory build artifact. This is the exact equivalent of shipping a Subnautica-style world without its baked sidecars.

### Save And Schema

Current source truth:

- `SaveBinaryStorage.CurrentVersion = 0x0009`.
- `SaveBinaryStorage.CurrentHeaderSize = 56`.
- `SaveBinaryStorage` writes indexed sector blocks with `FlagIndexedSectorBlocks` and `FlagProtectedLz4Blocks`.
- `SaveBinaryStorage` uses fixed indexed directory capacity `4096`.
- `SaveMasterHashV10.HeaderVersion = 0x000A`.
- `SaveMasterHashV10.HeaderSizeBytes = 72`.
- `SaveDeltaCompression` core packed records are explicit `Pack = 1` with fixed sizes.
- `SaveManager` has 300-frame save/WFC telemetry rings.

Current doc drift:

- `Docs/ARCHITECTURE/SAVE_V8_BINARY_SPEC.md` still presents container `0x0008` and header-size assumptions.
- `Docs/ARCHITECTURE/SAVE_PAGING_PROTOCOL.md` still presents container `0x0008`.
- `Docs/ARCHITECTURE/COOP_MERKLE_STATE_DELTA_PROTOCOL.md` is strong static design but runtime pending.

Contract gap: HECTON-8 cannot run Early Access-style updates safely while save docs, runtime storage, and staged v10 hash header are split across three truths.

Required contract:

- Generate one `SAVE_LIVE_VERSION_LEDGER.md` or generated manifest from code constants.
- Mark v8 as historical.
- Mark v9 as current storage.
- Mark v10 as staged master-state hash header until integrated into storage.
- Add CI/doc gate that fails when `SaveBinaryStorage.CurrentVersion`, header size, or flags drift without ledger update.

### Sector Paging I/O

Positive:

- Indexed sector storage exists.
- MODP payload sector prefix exists.
- Some MODP paths stream directory slots from mapping and avoid full payload decompression.

Risk:

- `TryReadIndexedDirectory` returns `SectorEntry[]`.
- Current read path allocates `new SectorEntry[IndexedSectorDirectorySlotCount]`.
- The paging protocol explicitly says not to allocate `SectorEntry[4096]` for mod payload scans.

Contract gap: this is acceptable as cold migration/debug only, but not as a normal load/mod scan path on Steam Deck MicroSD or memory-tight Android.

Required contract:

- Add a zero-GC directory-window scan API for targeted sector lookup.
- Keep full `SectorEntry[]` only behind cold repair/defrag tooling.
- Add a profiler gate for "load save with 100 mod payload sectors" with allocation count and wall time.

### Mod Manifest And Overlay API

Current source truth:

- `ModLoader.CurrentAPIVersion = 2`.
- `ModLoader` disables manifests with `RequiredAPIVersion <= 0`.
- `ModLoader` consumes `ModPriority`.
- `ModMetadata` contains `Dependencies`, `RequiredAPIVersion`, and `ModPriority`.
- `ModBuilderWindow.ModManifestData` emits `Id`, `Name`, `Version`, `Author`, `Dependencies`, `EntryAssembly`, and `EntryType`.
- `ModBuilderWindow.ModManifestData` does not emit `RequiredAPIVersion` or `ModPriority`.

Contract gap: the SDK can generate a manifest the runtime rejects. That is a day-one modding failure.

Public Subnautica mod ecosystem lesson:

- Nautilus succeeded because high-level content handlers exist: craft data, craft tree, prefab, PDA, known tech, loot distribution, world entity database, save data, options, language, sound, sprites, story goals.
- TerrainPatcher proves patch-package dependency/load-order/conflict concepts are essential.
- Nitrox proves multiplayer retrofits are expensive when state authority was not designed from day one.

HECTON-8 must not clone these implementations or licenses. It must borrow the handler taxonomy:

- `PDAOverlayHandler`.
- `ScanOverlayHandler`.
- `KnownTechOverlayHandler`.
- `LootDistributionOverlayHandler`.
- `WorldDistributionOverlayHandler`.
- `LocalizationOverlayHandler`.
- `AudioBankOverlayHandler`.
- `PersistentModPayloadHandler`.

All handlers must resolve to hashes, ContentAuthority manifests, and binary payloads. No mod-facing Unity object handles.

### Persistent Mod World

Current source truth:

- `ModWorldPersistenceManager` has an internal `SpawnPersistentPrefab`.
- It records scene hash, AUP grid/local position, asset name, mod id, and record list.
- It serializes through `ModSaveStateStore.SetModString` using JSON.
- Public `HectonAPI.World.SpawnPersistentPrefab` throws and instructs mods to submit a `ModCommand`.
- Public `HectonAPI.Resources.LoadPrefab` also throws and instructs mods to resolve prefab hashes.

Positive: direct GameObject access is blocked.

Contract gap: there must be a proved command route from mod request to persistent spawn. If no `ModPersistentSpawnCommand` reaches `ModWorldPersistenceManager`, the feature is a facade.

Required contract:

- Add/verify a hash-only persistent spawn command.
- Input fields: `modHash`, `prefabHash`, `sectorHash`, quantized AUP local position, yaw/pitch/roll quantization, persistence flags.
- Validate through ContentAuthority.
- Persist through MODP binary sector payload, not unbounded JSON text.

### Quest, Scan, And First-Hour Route

Current source truth:

- `QuestStateManager` uses a 320-word packed state layout.
- Quest words are explicitly segmented: quest, item, scan, lore, beacon, entity destroy, deadlock.
- `CapturePackedStateSnapshot` exists.
- `SaveManager` captures packed quest state.
- `SaveBinaryPayloadCodec` still writes legacy string lists for `questActiveIds`, `questCompletedIds`, `missionActiveIds`, and `missionCompletedIds`.
- `ScanEvents` has native queues and hashed metadata caching.

Positive: the packed state foundation is real.

Contract gap: Early Access updates need schema migration and route verification. Legacy string lists can remain for compatibility, but they cannot remain the product truth.

Required contract:

- First-hour route validator must be build-blocking, not menu-only.
- Packed quest words are authoritative.
- Legacy strings are compatibility export/import only.
- Add migration tests across last N save versions.
- Add route overlay support so content/mod updates can add scan/quest nodes without reauthoring scene references.

### Co-op Readiness

Current source truth:

- `COOP_MERKLE_STATE_DELTA_PROTOCOL.md` is a serious static design.
- `HectonNetworkManager.cs` is a placeholder with TODOs.

Subnautica 2 market lesson: co-op shipped in the Early Access surface, and their roadmap immediately hardens co-op with voice, trading, revive, HUD signals, and base-builder improvements.

HECTON-8 action:

- Do not ship fake co-op.
- Build local loopback first: deterministic state hash, save/load hash equality, packet encode/decode, rollback/jitter simulation.
- Promote co-op only when state leaves, persistence deltas, inventory deltas, base operations, and mod payload deltas are authority-owned and replayable.

## Tactical Backlog

### P0 - Foundation Contracts

1. Static data build artifact gate.
   - Build must fail if `static_data.h8bin` is required and missing.
   - Balance CSVs need generated `hash32` columns or compiler-side deterministic hash emission.
   - Bootstrap must not silently treat missing monolith as acceptable outside editor/dev mode.

2. Save live version ledger.
   - Resolve v8 docs vs v9 storage vs staged v10 hash header.
   - Generate doc/manifest from constants.
   - Add migration/version smoke tests.

3. Mod SDK manifest fix.
   - `ModBuilderWindow` must emit `RequiredAPIVersion = 2`.
   - Add `ModPriority`.
   - Add validation that a built mod package loads under `ModLoader`.

4. Zero-GC sector directory scan.
   - Replace normal-path `SectorEntry[4096]` allocations with native/windowed scan.
   - Keep full array only for cold repair.

5. Persistent mod spawn command route.
   - Hash-only command.
   - ContentAuthority validation.
   - MODP binary payload persistence.

### P1 - Live Product Strength

1. First-hour route gate.
   - Scanner, PDA, quest, fabricator, and mission route proof must block builds.
   - Route validator must consume packed quest/scan hashes.

2. Content overlay handlers.
   - Implement PDA, scan, known-tech, loot/world distribution, localization, audio, and save payload overlays.
   - Enforce dependency/load-order/conflict diagnostics.

3. Co-op local loopback.
   - No transport promises until deterministic state hash and replay work locally.

4. Platform I/O matrix.
   - Steam Deck MicroSD: streaming save/content read budgets.
   - Android/Quest: allocation and alignment proof.
   - Mac/Metal: shader/threadgroup proof.
   - PC high-tier: optional overkill packs, not a heavier baseline.

### P2 - Visual Counterplay

The Subnautica 2 screenshot surface is catchable with fakes:

- Fog LUT bands.
- Baked/projected caustics.
- Triangle-noise silt.
- Billboard/impostor flora clusters.
- Cockpit/visor framing.
- Reactive-but-cheap fauna stimulus scores.

HECTON-8 differentiation must be not "brighter coral but darker." It must be NASA-punk noir:

- pressure failures;
- acoustic threat readability;
- black-box telemetry fiction integrated into UI;
- hull dents and field repairs;
- salt crystals on visor;
- volumetric silt wakes;
- industrial wreck silhouette language.

Low tier: cheap LUTs, dithered silt, impostor flora, sparse but readable silhouettes.
Middle: authored biome/object batches and route density.
High: reactive fauna, denser VFX, higher material taps.
Ultra: visor salt, volumetric silt, high-tier POM/raymarch/SSS, dense flora sway, hull deformation VFX.

## Do Not Borrow

- Do not adopt Unity Addressables as the world truth store just because Subnautica used large Addressables catalogs. HECTON-8's intended truth is DataMonolith plus sector payloads.
- Do not expose GameObjects to mods as the primary runtime API.
- Do not clone GPL/AGPL mod code into proprietary/runtime code.
- Do not treat Subnautica 2's UE5 visuals as the architecture. The architecture is update cadence, content packaging, save compatibility, co-op authority, and feedback telemetry.

## Acceptance Tests

The next integrator pass should be able to prove:

- `static_data.h8bin` exists or the build fails intentionally.
- Save version ledger matches `SaveBinaryStorage` and `SaveMasterHashV10`.
- `ModBuilderWindow` produces a manifest accepted by `ModLoader`.
- Mod overlay package can add one scan entry, one PDA entry, one known-tech unlock, one loot distribution record, and one persistent binary save payload.
- Persistent mod spawn command resolves a prefab hash without exposing a GameObject to mod code.
- Sector lookup path performs targeted MODP read without allocating `SectorEntry[4096]`.
- First-hour route validator is build-blocking.
- Co-op local loopback passes deterministic master-state hash after save/load.

## Microsecond Claim

This pass changed documentation only: 0us runtime impact.

Future estimates must be profiler-backed. No claimed microsecond savings are accepted from this research document alone.
