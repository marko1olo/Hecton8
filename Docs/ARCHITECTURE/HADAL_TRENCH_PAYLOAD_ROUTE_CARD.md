# HADAL_TRENCH_PAYLOAD_ROUTE_CARD

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

Date: 2026-05-21
Owner: SHINOBU_241
Domain: World Generation / Offline Voxel SDF Trench Baking
Evidence Class: STATIC_SOURCE_ONLY
Runtime Status: PENDING BAKE / PENDING BOOT CONSUMER / PENDING PLAYER PROOF

## Authority

One fact: immutable hadal trench voxel sector payload.
One owner: SHINOBU_241 offline forge until a streaming consumer owns runtime hydration.
One route: `Assets/StreamingAssets/Hecton8/HadalTrenches/hadal_trench_sector_0000.h8bin`.
One proof artifact: `Docs/Reports/TRENCH_BAKE_REPORT.json` plus `Docs/Reports/SHINOBU_241_SELF_AUDIT.xml`.

This route is not `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
It is a separate StreamingAssets payload outside the `DataMonolith/` subtree. `H8StaticDataArena` will reject it because the path, magic, BIOS header, section table, and checksum contract differ from the Data Monolith format.

## Binary Contract

Magic: `0x54523848` written little-endian as bytes `H8RT`.
Version: `1`.
Header bytes: `160`.
Endian marker: `0x01020304`.
Schema hash: `0xA2410002`.
Section alignment: `8` bytes.
Checksum type: `1` = FNV-1a 64-bit over density payload, vent payload, and adaptive block payload.

Header fields include resolution, sector origin AUP, voxel size, compression mode, compressed bytes, uncompressed RLE bytes, run count, vent count, adaptive block count, payload offsets, total file bytes, and rollback-excluded flag.

## Payload Sections

Density section: RLE rows, optionally LZ4 block-compressed, preceded by uncompressed and compressed byte counts.
Vent section: 64-byte `ThermalVentSpawnDTO` rows.
Adaptive section: 32-byte `HadalTrenchAdaptiveBlockDTO` rows. Offset 12 stores the actual `BlockSizeVoxels` value chosen by the continuous `GlobalQualityWeight` curve; it is not a lossy log2 code.
The writer inserts explicit zero padding between density->vent and vent->adaptive sections so recorded offsets remain 8-byte aligned. The FNV-1a payload hash excludes padding bytes and covers only useful density, vent, and adaptive payload bytes.

All SDF truth is static terrain data. It must not enter rollback Merkle state. A future runtime consumer may hash the payload for identity, but rollback snapshots must exclude the full voxel density bytes.

## Validator

`HadalTrenchPayloadValidator` verifies file existence, magic/version, endian marker, header bytes, schema hash, checksum type, rollback flag, density prelude byte counts, section offsets, section alignment, declared byte counts, total file bytes, and FNV-1a payload hash.

## Pending Work

No claim is made that this payload is loaded by `H8StaticDataArena`.
No `H8DataSectionId` has been added for trench density, vents, or adaptive blocks.
No `static_data.h8bin` integration proof exists.
No runtime Vault buffer is allocated by SHINOBU_241 because the current implementation is editor/offline source and local scratch only.

## R48 Exact Route Field Normalization

Route ID: HADAL_TRENCH_PAYLOAD_ROUTE_CARD
Owner: SHINOBU_241
Instrument: documented route instrument in this file; no new route is accepted from this normalization block alone.
Producer phase: EDITOR_BAKE / OFFLINE_SOURCE.
Consumer phase: none accepted; future runtime hydration requires a new route card before boot integration. Hot GlobalRegistry polling is forbidden.
Cadence/capacity: bounded cadence/capacity documented below; no hot dynamic allocation or unbounded queue growth is implied.
Overflow/failure: fail closed, clamp/drop/coalesce as documented below, and treat dump paths as planned/generated-on-fault until a timestamped artifact exists.
Shutdown/disposal: owner/Vault/SignalBus lifecycle documented below; visual/debug consumers do not own native memory.
Proof required before GREEN: fresh compile/import, Play Mode route, profiler/GC, platform/player proof where runtime-facing, and linked artifact path with command, timestamp, environment, and output.
Review disposition: YELLOW / STATIC_SOURCE_ONLY.
