# [ARCHIVE] Pre-Strict Architecture Snapshot

Date: 2026-05-24
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Original: Docs/ARCHITECTURE/SOA_INVENTORY_QUERY_ENGINE.md
Rule: historical snapshot only; not active doctrine.

# SOA Inventory Query Engine

Owner: `PlayerInventory`
Agent: `SHINOBU_316`
Status: pending Unity compile/profiler verification.

## Route

`PlayerInventory` remains the inventory authority. `SoaInventoryQueryEngine` is a hash-only Burst utility layer over parallel Vault lanes:

- `BufferID.ShinobuInventoryHashes` / `NativeArray<uint>` / minimum 512 rows for rollback parity
- `BufferID.ShinobuInventoryQuantities` / rollback storage `NativeArray<int>` / SHINOBU_316 hot-kernel view `NativeArray<uint>` via zero-copy `Reinterpret` / minimum 512 rows for rollback parity
- `BufferID.ShinobuInventoryDurabilities` / `NativeArray<float>` / minimum 512 rows for rollback parity
- `BufferID.ShinobuInventoryActiveSlotCount` / `NativeArray<int>[1]`
- `BufferID.ShinobuInventorySoaTelemetry` / `InventorySoaTelemetryEntry[300]`
- `BufferID.ShinobuInventorySoaTelemetryCursor` / `NativeArray<int>[1]`
- `BufferID.ShinobuInventorySoaCapacityProfiles` / `InventoryCapacityProfileDTO[64]`

No standalone manager owns inventory truth and no SHINOBU_316 private persistent `NativeArray` is retained. Existing `InventoryChangedSignal` and `InventoryEvents` remain the publication route.

Read-style accessors (`TryReadFastFailInventorySoA`, `TryReadLatestSoaQueryTelemetry`, `TryReadSoaInventoryXRay`) use Vault `TryReadHandle` through `SoaInventoryQueryEngine.TryReadVaultBuffers`. Owner publication, dump, and editor mutation command processing are explicit owner/diagnostic write paths.

## Query

`QueryInventoryHashJob` scans `ItemHashIDs` and `Quantities` as parallel `NativeArray<uint>` lanes. Existing rollback storage for quantities remains `NativeArray<int>`; `SoaInventoryQueryEngine.AsUIntQuantityView` exposes the same 32-bit Vault memory to the query jobs without copy or allocation. On AVX2-capable x86 the hash path compares eight lanes through `mm256_cmpeq_epi32` and `mm256_movemask_epi8`; on SSE2 it compares four lanes through `cmpeq_epi32` and `movemask_epi8`; on ARM NEON it compares four lanes through `vceqq_u32` and extracts lane masks with `vgetq_lane_u32`; fallback uses `uint4` and `math.bitmask`. Matching lane extraction uses `math.tzcnt`. Mutation result DTOs preserve the SIMD proof bit used by the scan (`AVX2`, `SSE2`, or `NEON`) plus the unsigned quantity view bit.

`QueryInventoryHashBatchJob` admits query count through continuous `GlobalQualityWeight` via `InventoryRoutingNetwork.ResolveTimeSliceBatchSize`. No binary quality switch exists.

## Mutation

`MutateInventoryQuantityJob` receives the quantity lane as `NativeArray<uint>` and applies deltas with `Interlocked.CompareExchange` on the same 32-bit cell reinterpreted as `int` for the CAS primitive. A zero quantity with `RemoveWhenZero` triggers swap-and-pop compaction:

1. atomically decrement active count,
2. copy the last active row into the removed slot,
3. clear the former last row.

The dense SoA lane is deterministic by active-count order. The 2D grid remains authoritative for UI placement.

Editor X-Ray manual injection is queued as scalar hash/delta intent and drained from the `PlayerInventory` owner phase. It does not allocate `TempJob` result buffers and does not force same-frame `JobHandle.Complete()` from the button callback.

## AUP Drop

`TryDropOneItemToWorldSignalAup` adds the local drop offset to the source AUP in `double3`, subtracts the committed origin in double precision, then downcasts to runtime `Vector3`.

## Black Box

`PlayerInventory` writes one owner-phase telemetry entry per late frame into the Vault-owned fixed 300-entry `InventorySoaTelemetryEntry` ring. The owner row uses scalar counters, not private native buffers:

- `QuantityTotal`: active dense item rows.
- `MatchCount`: admitted query count for the frame.
- `MutationDelta`: mutation requests scheduled in the frame.
- `MutationIndex`: swap-and-pop removals observed in the frame.
- `EstimatedMicroseconds`: continuous quality-weighted frame estimate until dispatcher/profiler timing is available.

Fault or estimated query time above `0.2 ms` dumps:

`Docs/AgentLogs/Dump_SHINOBU_316.bin`

The dump contains magic/version, cursor, entry count, entry size, layout hash, and raw 64-byte entries.
