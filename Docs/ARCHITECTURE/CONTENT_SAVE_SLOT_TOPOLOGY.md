# Content Save Slot Topology

Authority: CONTENT_AUTHORITY_DICTATOR
Date: 2026-05-17
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-19 DOC_GLOBAL R28 Interior Note

R28 reread confirmed this file remains a static content/save-slot topology contract, not save/load roundtrip, migration, Steam Cloud, or player-build proof. Current root/architecture boundary is `Docs/Reports/2026-05-19_DOCUMENTATION_R28_ROOT_ARCHITECTURE_INTERIOR_BOUNDARY_LOCAL.md`, with R27 source counters retained until a newer counter pass reruns them. Current static gates: `Tools/AtlasCheck.py` remains red on `57` RealtimeCSG vendor references; `Docs/Modding/Validate_Mod_API_Static.ps1` now passes (`Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`). Unity/runtime/profiler/player-build proof remains absent.

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
