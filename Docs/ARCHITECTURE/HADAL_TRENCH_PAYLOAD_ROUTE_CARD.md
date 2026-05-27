# HADAL_TRENCH_PAYLOAD_ROUTE_CARD

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

It is a separate StreamingAssets payload outside `DataMonolith/`. `H8StaticDataArena` rejects it: path, magic, BIOS header, section table, and checksum contract differ.

## Binary Contract

Magic: `0x54523848` written little-endian as bytes `H8RT`.

Version: `1`.

Header bytes: `160`.

Endian marker: `0x01020304`.

Schema hash: `0xA2410002`.

Section alignment: `8` bytes.

Checksum type: `1` = FNV-1a 64-bit over density payload, vent payload, and adaptive block payload.

Header fields: resolution, sector AUP, voxel size, compression mode, compressed bytes, RLE bytes, run count, vent count, adaptive blocks, payload offsets, file bytes, rollback flag.

## Payload Sections

Density section: RLE rows, optionally LZ4 block-compressed, preceded by uncompressed and compressed byte counts.

Vent section: 64-byte `ThermalVentSpawnDTO` rows.

Adaptive section: 32-byte `HadalTrenchAdaptiveBlockDTO` rows. Offset 12 stores the actual `BlockSizeVoxels` value chosen by the continuous `GlobalQualityWeight` curve; it is not a lossy log2 code.

- Writer inserts explicit zero padding between density->vent and vent->adaptive sections.
- Recorded offsets remain 8-byte aligned.
- FNV-1a payload hash excludes padding bytes.
- Hash covers only useful density, vent, and adaptive payload bytes.

- SDF truth is static terrain data.
- It must not enter rollback Merkle state.
- Future runtime consumers may hash the payload for identity.
- Rollback snapshots must exclude full voxel density bytes.

## Validator

`HadalTrenchPayloadValidator` verifies:

- File existence, magic/version, endian marker, header bytes, schema hash.
- Checksum type, rollback flag, density prelude byte counts.
- Section offsets, section alignment, declared byte counts.
- Total file bytes and FNV-1a payload hash.

## Pending Work

No claim is made that this payload is loaded by `H8StaticDataArena`.

No `H8DataSectionId` has been added for trench density, vents, or adaptive blocks.

No `static_data.h8bin` integration proof exists.

No runtime Vault buffer is allocated by SHINOBU_241 because the current implementation is editor/offline source and local scratch only.

## Route Field Contract

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
