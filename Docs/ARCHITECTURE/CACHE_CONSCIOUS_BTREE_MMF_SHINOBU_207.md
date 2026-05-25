# Cache-Conscious MMF B-Tree

Owner: SHINOBU_207

Domain: Echelon 1 Core & Memory Infrastructure

Status: source implemented, Unity compile/profiler proof blocked by foreign dependency wall plus CPU guard on 2026-05-20.

## Binary Layout

- `.h8bin` static data and Babel dictionaries keep existing flat tables only as slice metadata.

- A 64-byte aligned B-Tree section is inserted immediately after the table:

  - `treeOffset = AlignUp64(tableOffset + tableCount * tableStrideBytes)`

  - `treeEndOffset = payloadOrRecordsOffset`

  - `rootOffset = treeEndOffset - 64`

- Static balance payload records keep their 48-byte DTO ABI.
- Every `H8StaticDataLookupEntry.Offset` is 64-byte aligned after the B-Tree section.
- A resolved record fits inside one cache line instead of straddling two.

- `BTreeNodeDTO` is exactly 64 bytes: 7 sorted `uint` keys, 8 `uint` child/value lanes, and 1 metadata lane.

- Leaf child lanes store record/index ordinals. Internal child lanes store file offsets to child nodes.

- AUP-indexed logs use the spatial Morton variant:

  - `MortonBTreeNodeDTO` is exactly 64 bytes: 4 sorted `ulong` Morton keys, 5 `uint` child/value lanes, metadata, and explicit padding.

  - `SpatialMortonBTreeRecordDTO` and `SpatialMortonLevelEntryDTO` are 16-byte rows.

  - `SpatialMortonBTreeCompiler.TryBuild` consumes caller-owned `NativeArray` buffers and writes the root last.

## H8LR Lore Blob

- H8LR keeps its 16-byte header and 16-byte records; header reserved remains zero.

- The B-Tree is inferred from the 64-byte aligned gap between record table end and first payload offset.

- Old flat-only H8LR blobs fail validation. Re-run `python Tools/LorePacker.py --check --hash-audit --list` to rebuild.

## Runtime Rules

- Runtime lookup performs bounded B-Tree traversal only. No `NativeParallelHashMap`, no flat midpoint binary search, no managed dictionary in lookup truth.

- MMF section and node bounds fail closed at the binary boundary.
- Section tail math uses `ulong`.
- Node guard: `offset <= treeEndOffset - 64` after proving `treeEndOffset >= 64`.
- No traversal path relies on wrapped `offset + 64` arithmetic.

- B-Tree traversal/search jobs use deterministic Burst mode: `TraverseBTreeJob`, `DispatchBulkBTreeSearchJob`, `TraceBTreeTraversalJob`, `SpatialMortonRangeQueryJob`, and `BabelBTreeSearchKernel`.

- AUP spatial queries use `HashAupDouble3ToMorton64` before lookup. `TryFindMortonRangeFirstValue` is non-recursive and uses a fixed stack, not a pointer-heavy Octree.

- `GlobalQualityWeight` controls only cache-touch prefetch stride. Tree topology and lookup truth are identical across Low, Middle, High, and Ultra.

- `H8CacheBTree.ScheduleTelemetryPostSimulationFlush` exposes the POST_SIMULATION flush job for dispatcher integration.

- `BTreeTelemetryEntry`, `BTreeTelemetryAccumulatorDTO`, and `BTreeTuningProfileDTO` are 64-byte explicit structs.

- Vault buffer IDs reserved by this domain:

  - `72070` B-Tree telemetry ring, 300 entries.

  - `72071` B-Tree telemetry cursor.

  - `72072` B-Tree telemetry accumulator.

  - `72073` B-Tree tuning profiles, 16 entries.

- Crash dumps use `Docs/AgentLogs/Dump_SHINOBU_207.bin`.

## Human Control

- `Data/Balance/btree_tuning_profiles.csv` is parsed through `BTreeTuningCsvParser` from `ReadOnlySpan<byte>` into Vault-owned unmanaged DTOs.

- `Hecton8/Core/Data/B-Tree Topology X-Ray` loads `.h8bin`/`.h8loc` payloads, draws node topology, reads B-Tree telemetry, and runs a synchronous trace job for raw key input.

## Ledger State

- `Data/Balance/Baked/H8StaticData.bin` is 1328 bytes with `CacheBTreeFlag`, B-Tree offset 320, B-Tree bytes 192, records offset 512, 64-byte aligned record payload starts, and payload CRC `0x598EF439`.

- `Data/Balance/Baked/Babel_Dictionary.h8bin` is 1616 bytes with `CacheBTreeFlag`, B-Tree offset 448, B-Tree bytes 320, and data offset 768.

- `Tools/UpgradeStaticBTreePayloads.py --check` validates the current small balance payload CRCs and B-Tree lookups outside Unity.

- `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` records `Data/Lore/Encyclopedia.h8bin` as 43536 bytes with one 64-byte B-Tree node at offset 64.

- Ledger status: `READER_PRESENT_PENDING_UNITY_PROOF`, not runtime-ready.
- Missing evidence: Unity import, MMF map, GC, profiler, player route.
- Latest targeted C# build attempt failed on a 188-error foreign dependency wall outside SHINOBU_207.
- Latest retry guard reports CPU load `100`.

- New B-Tree telemetry Vault buffers are documented in `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_ROUTE_CARD_BTREE_TELEMETRY_SHINOBU_207.md` with review result `YELLOW` until runtime proof exists.
