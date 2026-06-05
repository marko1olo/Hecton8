# Content Save Slot Topology

Authority: CONTENT_AUTHORITY_DICTATOR

Date: 2026-05-17

Status: PENDING VERIFICATION

Owner domain: persistence / content save slot topology

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not save/load roundtrip, migration, Steam Cloud, or player-build proof.

- `Assets/_Project/Scripts/Core/Content/ContentSaveSlotTopology.cs`

- `Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs`

- `Assets/_Project/Scripts/Core/Content/ContentAssetHashMap.cs`

- `Assets/_Project/Scripts/Core/Content/Editor/ContentAuthorityBuildValidators.cs`

- `Assets/_Project/Scripts/SaveBinaryStorage.cs`

## `.sav`

- Player delta only: position/AUP, inventory deltas, equipped tools, health/O2/stress deltas, local quest deltas.

- Atomic write only: `slot_{n}.tmp` -> checksum verify -> `slot_{n}.sav`, with `slot_{n}.bak` before overwrite.

- Forbidden: static world asset paths, prefab references, derived terrain, derived flora/fauna, cached Addressables handles.

## `H8_MacroDB`

- World-state pages only: sector overrides, harvested resource deltas, regrowth state, persistent wreck/debris deltas, sector hydration payloads.

- File shape: `H8_MacroDB/sector_{hash:X16}.h8page`.

- Eviction and compaction remain MacroDatabase/DataVault owned, not UI owned.

## World Seed Derived

- Deterministic from seed: base terrain, unmodified SDF, biome membership, default loot placement, default flora/fauna distribution, HLOD identity, non-mutated lore placement.

- Rebuild from seed and hash registry; never serialize duplicate meshes, materials, or textures into save slots.

## Static Data And Sector Payloads

- `static_data.h8bin` / DataMonolith owns immutable static tables and generated DB authority.

- Sector payloads own baked base-world cache families: object batches, visibility/physics proxies, biome sidecars, audio/discovery sidecars, and other non-player-delta world cache data.

- These payloads are not save-slot data and are not made authoritative by cached Addressables handles.

## Asset Registry Rule

- Save data stores stable uint hashes only.

- `ContentAssetHashMap` resolves those hashes to Unity object/visual/audio asset bindings during load where the project deliberately uses Addressables-style delivery.

- Immutable static data and baked world cache truth resolve through DataMonolith/sector payload manifests, not through save files or cached Addressables handles.

- Missing required hash is a build blocker, not a runtime fallback.
