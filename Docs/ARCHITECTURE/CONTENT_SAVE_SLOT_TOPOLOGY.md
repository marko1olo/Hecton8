# Content Save Slot Topology

Authority: CONTENT_AUTHORITY_DICTATOR
Date: 2026-05-17
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not save/load roundtrip, migration, Steam Cloud, or player-build proof.

- `Assets/_Project/Scripts/Core/Content/ContentSaveSlotTopology.cs`
- `Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs`
- `Assets/_Project/Scripts/Core/Content/ContentAssetHashMap.cs`
- `Assets/_Project/Scripts/Core/Content/Editor/ContentAuthorityBuildValidators.cs`
- `Assets/_Project/Scripts/SaveBinaryStorage.cs`

## 2026-05-20 DOC_GLOBAL R46 Root/Architecture Boundary Note

R51 root/architecture encoding/boundary/read-order/route-card/source-counter correction (`Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md`) keeps this file as a static architecture/source contract, not runtime proof. Current DOC_GLOBAL boundary is `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md`; R50 remains prior at `Docs/Reports/2026-05-21_DOCUMENTATION_R50_ROOT_ARCHITECTURE_ATLAS_REGEN_R48_INTERIOR_DUMPTARGET_AND_COUNTER_DRIFT_LOCAL.md`; R49 remains prior at `Docs/Reports/2026-05-21_DOCUMENTATION_R49_ROOT_ARCHITECTURE_ATLASCHECK_BOUNDARY_ROUTE_FIELDS_AND_COUNTER_DRIFT_LOCAL.md`; R48 remains prior at `Docs/Reports/2026-05-21_DOCUMENTATION_R48_ROOT_ARCHITECTURE_DATE_ROLLOVER_ATLASCHECK_AND_COUNTER_REFRESH_LOCAL.md`; R47 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md`; R46/R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current static gates: `Tools/AtlasCheck.py` remains red on `ATLAS_CHECK_FAIL references=6881 missing=60` (one Dynamic Decals vendor asset ref, RealtimeCSG vendor icon/readme image refs, and missing HectonMaskChannelPacker and HectonMaterialChannelPackValidator source refs in the current atlas); `Docs/Modding/Validate_Mod_API_Static.ps1` passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only. Runtime proof remains absent.

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
