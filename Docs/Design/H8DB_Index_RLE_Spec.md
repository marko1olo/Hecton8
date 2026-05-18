# H8DB Index And RLE Spec

Owner: MACRO_DB_INDEX_OPTIMIZER  
Date: 2026-05-17
Status: DATABASE OPTIMIZED / PENDING UNITY VERIFICATION  

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

Scope: `.h8db` macro database index, payload padding, cache eviction, voxel delta RLE audit.

## Node Padding Spec

`.h8db` uses a 4096-byte header and 4096-byte B-tree nodes.

| Field | Value |
|---|---:|
| File header | 4096 bytes |
| Node size | 4096 bytes |
| Node alignment | 4096 bytes |
| Payload header | 32 bytes |
| Payload alignment | 16 bytes |
| Node max keys | 169 |
| Node min degree | 85 |
| Node computed bytes | 4080 |
| Node tail padding | 16 bytes |

Rules:

- Every node offset must be a 4096-byte multiple.
- Every payload record offset and append pointer must be a 16-byte multiple.
- Payload record size is `Align16(32 + payloadBytes)`.
- The 16-byte node tail padding is reserved. It is not a data field and must stay zeroed by `ClearNode`.
- Version 1 keeps `NodeMaxKeys=169` to preserve the existing B-tree split invariant `maxKeys = 2 * minDegree - 1`.

Rejected alternative: changing `NodeMaxKeys` to 168 for 16-byte array starts. That breaks the current split invariant or requires a file-format version bump. The safe optimization is binary search inside existing nodes.

## Index Optimization

Runtime lookup/update now uses lower-bound binary search inside each B-tree node instead of linear scan.

- Worst-case per-node comparisons before: 169.
- Worst-case per-node comparisons after: 8.
- Estimated saved comparisons: up to 161 per node visit.
- File ABI: unchanged.

This reduces MicroSD hydration stalls without touching node layout.

## Sector Hash Collision Test

Sector hash source: FNV-1a style 64-bit mix over signed sector `x/y/z` and `sectorSize`.

Theoretical probability for 10,000,000 random distinct sector coordinates:

```text
expected collision pairs = n(n-1) / (2 * 2^64)
                         = 0.000002710505
probability of at least one collision ~= 0.000002710501
```

The Python audit tool prints the observed simulation result and the theoretical birthday bound. A zero-observed-collision run is expected; it does not prove impossibility.

## RLE Efficiency Audit

Current native voxel snapshot RLE uses `SaveVoxelDeltaRun8`:

```text
StartIndex u16
RunLength  u16
SdfValue   s8
MaterialId u8
Flags      u8
Reserved   u8
```

Current SDF quantization is signed 8-bit over `[-8m, +8m]`.

| Quantization | Step | Max error | Verdict |
|---|---:|---:|---|
| 8-bit signed over +/-8m | 0.06299m | 0.03150m | KEEP for LOD0/LOD1 player-facing chunks |
| 4-bit signed over +/-8m | 1.06667m | 0.53333m | REJECT for player-facing mesh truth |
| 4-bit signed over +/-0.5m narrow band | 0.06667m | 0.03333m | ACCEPT only for LOD2+ visual RLE with saturation |

Fact: 4-bit density saves 50% of the density lane, not 50% of the full `SaveVoxelDeltaRun8` record. If metadata remains unchanged, full run saving is 6.25%. A 50% full-payload saving requires a new far-LOD-only packed run format, not a blind density nibble.

Policy:

- LOD0/LOD1: keep signed 8-bit SDF RLE.
- LOD2+: allow 4-bit narrow-band visual density only after LOD hysteresis; never use it for collision, carving authority, or save-affecting near-field mesh truth.
- Dense/uniform chunks: keep uniform RLE and LZ4-after-pack; do not add LZ4 dictionary mode without corpus proof.

## GlobalDataVault Macro-Cache LRU

Rules implemented for the macro payload cache:

- `TryGetMacroDatabasePayload` and successful store refresh an unsigned access tick.
- When capacity is full, `TryStoreMacroDatabasePayload` evicts the clean payload with the oldest access tick.
- Dirty payloads are not evicted by automatic LRU; caller must flush or explicitly evict after persistence.
- Access tick wrap clears the tick sidecar and restarts at `1`, making old entries eligible before the newly touched payload.
- Cache byte accounting subtracts through a saturating helper so corrupt/stale handles cannot drive stats below zero.
- Manual `EvictDistant` remains authoritative for distance-based shedding.
- No managed collections are added to runtime cache state.

Low/MX350:

- Smaller cache capacity, larger dead zone between hydration and dehydration.
- If page faults exceed 2/sec, increase hydration radius by one sector band before increasing cache capacity.

High/Ultra:

- Larger cache capacity and hydration radius.
- Saved CPU from binary search buys wider preload, not more synchronous file IO.

## B-Tree Vs Flat RLE

B-tree is superior to flat RLE for a 100km world because the player hydrates sparse, spatially local sectors. Flat RLE requires scanning or secondary indexing to find a sector; the B-tree gives bounded key lookup and append-only payload replacement with compaction. RLE remains the payload codec, not the world-scale locator.

Flat RLE is acceptable only inside a resolved payload block after the B-tree has found the sector.
