# TERRAIN_CHUNK_PAGING_SYSTEM - SHINOBU_245

Evidence class: STATIC_SOURCE / FILESYSTEM. Runtime proof remains pending Unity import, script compile, Play Mode, profiler, GCMonitor, Memory Profiler, payload validator, streaming boot, missing-file dump test, stale-handle/release test, and player build.

## Authority Route

- Owner: World streaming runtime, `SHINOBU_245`.

- Fact: local terrain chunk residency and immutable sidecar bytes.

- Route: camera `double3` AUP snapshot -> Burst sector residency job -> preallocated worker request ring -> background `H8_Terrain_Pager` FileStream read/decode -> Vault staging bytes -> `VISUAL_SYNC` commit into active Vault bytes.

- Proof target: 300-entry `PagerTelemetryEntry` Vault ring plus planned/generated-on-fault `Docs/AgentLogs/Dump_SHINOBU_245.bin`; no existing dump artifact is implied unless a timestamped trigger/output is linked.

## Vault Buffers

`71740..71758` owner: `SystemID.WorldStreaming`.

- Lanes: metadata, sector coords, staging bytes, active bytes, compressed scratch.
- Lanes: request/result rings, job scratch, telemetry, tuning, counters.
- Lanes: freed-slot scratch, hardware profiles, CSV scratch, telemetry dump snapshot bytes.

- Bootstrap stores only `VaultGenerationHandle<T>` descriptors plus raw aliases captured after successful Vault locks.
- It does not retain private `NativeArray<T>` fields.
- Method-local `NativeArray<T>` views are resolved only when scheduling Burst jobs or parsing cold CSV data.
- Lock acquisition is all-or-fail with a lock mask and partial-unlock rollback; initialization aborts if any required buffer cannot be locked.

- Cold bootstrap proves byte-slab capacities before Vault acquisition.
- Active/staging slabs: `maxChunkSlots * chunkByteCapacity`.
- Compressed scratch slabs: `chunkByteCapacity + chunkByteCapacity/255 + 16`.
- Overflow or invalid capacity sets `TelemetryFaultCapacityOverflow`.
- Initialization aborts before native aliases exist.

- Worker/result safety is fenced without consuming metadata padding: while a slot is `Loading`, `FileOffset@12` temporarily stores the request `Sequence`; after success it is overwritten with the committed byte count.
- A worker result mutates metadata only when sector hash, sequence-in-`FileOffset`, and `Loading` state still match.
- Shutdown releases generation handles only after worker termination and already-completed job finalization are confirmed.
- Teardown does not force-complete pager jobs; unresolved jobs leave Vault buffers locked for deferred release rather than allowing a hidden main-thread stall or freeing memory under live native work.

## Binary Payload

`ChunkMetadataDTO=32`:

| Lane | Bytes |
| --- | --- |
| `SectorHash@0` | 8 |
| `BufferIdRef@8` | 4 |
| `FileOffset@12` | 4 |
| `StateFlags@16` | 4 |
| `DistanceSq@20` | 4 |
| Pad `24..31` | 8 |

Layout proof:

- Player: fixed offset constants plus `UnsafeUtility.SizeOf<ChunkMetadataDTO>()`.
- Editor: `UnsafeUtility.GetFieldOffset` under `UNITY_EDITOR`.
- Rejected route: `Marshal.OffsetOf`.

`TerrainChunkFileHeaderDTO=32`, little-endian magic `H8CB` (`0x42433848`): `Version@4`, `StoredBytes@8`, `UncompressedBytes@12`, `Compression@16`, `PayloadOffset@20`, `Crc32@24`, `Flags@28`.

- Runtime accepts raw and LZ4 payloads only behind this header.
- Unheaded raw file fallback is rejected for real files; mock payload generation is the only headerless path.
- Header sizes are validated as unsigned values before any cast to `int`; CRC32 value `0` is legal and still verified against the computed payload CRC.
- LZ4 stored bytes are validated against the compressed scratch bound, while uncompressed bytes are validated against the active/staging chunk capacity.
- LZ4 extension lengths are accumulated with explicit overflow and remaining-output bounds before any native copy.

## Boundaries

- Data Monolith: not ready and not claimed. These are StreamingAssets sidecars, not `static_data.h8bin` sections.

- Rollback: excluded. Terrain bytes and residency metadata are local environmental state, not StateRingBuffer/Merkle truth.

- Compile wall: runtime depends on Core/Core.Data/Core.Memory contracts and Unity packages; no offline baker/editor runtime assembly reference is introduced by SHINOBU_245.

- Continuous scalability: `GlobalQualityWeight` and latency EWMA scale radius/queue/commit budget smoothly. No binary hardware tier switch changes DTO layout or authority route.

- Blackbox:
  - dispatcher telemetry copies the `300`-entry ring into Vault buffer `71758` only on new fault masks;
  - frame/fault data publishes through one packed `Interlocked.Exchange`;
  - persistent pager worker wakes after publish;
  - worker writes `Dump_SHINOBU_245.bin` from the snapshot off the main thread;
  - dump I/O never races live telemetry writes;
  - worker liveness uses a volatile heartbeat timestamp;
  - pending/loading work plus inactive or stale heartbeat sets `TelemetryFaultIo` before dump request.
- Scanner: SHINOBU_245 `Synchronous_IO_Scanner` whitelists file/stream I/O only by current statement span and explicit local markers.
- Runtime sector paths are built into fixed boot-allocated char/UTF-8 buffers and opened through native handles.
- Worker no longer allocates a per-load sector path `string`.
