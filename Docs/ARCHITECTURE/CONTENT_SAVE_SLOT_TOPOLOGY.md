# Content Save Slot Topology

Authority: CONTENT_AUTHORITY_DICTATOR
Status: PENDING VERIFICATION

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

## Asset Registry Rule
- Save data stores stable uint hashes only.
- `ContentAssetHashMap` resolves those hashes to Addressables/prefab/mesh bindings during load.
- Missing required hash is a build blocker, not a runtime fallback.
