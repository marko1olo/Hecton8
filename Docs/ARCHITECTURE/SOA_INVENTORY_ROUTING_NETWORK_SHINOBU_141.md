# SOA Inventory Routing Network - SHINOBU_141

Status: static source integration pending Unity compile/import proof.

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not inventory runtime, transaction correctness, UI route, profiler, GC, save/load, or player-build proof.

- `Assets/_Project/Scripts/Inventory/Routing/InventoryRoutingNetwork.cs`

- `Assets/_Project/Scripts/Inventory/Routing/Hecton8.Inventory.Routing.Runtime.asmdef`

- `Assets/_Project/Scripts/Editor/InventoryRouting/InventoryRoutingNetworkTunerWindow.cs`

- `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs`

- `Assets/_Project/Scripts/Core/MemorySentinelRuntime.cs`

## Authority

Runtime truth is the vault-owned `NativeArray<InventorySlotDTO>` at `BufferID.ShinobuInventorySlots`.

Object inventories remain compatibility/user-facing surfaces only; hot routing, aggregation, rollback

snapshotting, and transaction mutation must use the SOA buffers.

Compile-wall boundary: the runtime source is isolated under `Hecton8.Inventory.Routing.Runtime`. Inventory routing edits do

not force the root `Hecton8.Core` assembly to recompile. The runtime asmdef references `Hecton8.Core`,

`Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, and Unity packages only; it does not reference scene-facing construction,

power, logistics, storage, AI, physics, world, or rendering sibling runtime assemblies.

## First 20 Minutes Route Impact

First 20 Minutes moment: resource -> craft/repair/build -> save/load.

Route impact: Copper Wire/fabricator/base-support checks need fast and deterministic resource availability without

freezing on hundreds of lockers. This system makes that route testable by moving query truth into fixed SOA buffers and

by adding a data-only bridge for legacy storage owners to publish stable container windows before the live route is wired.

Proof required: Unity import, Console, Play Mode through the selected Copper Wire route, fabricator/resource query stress,

0B GC hot-path capture, profiler frame sample, save directory diff, and reload verification that inventory quantities and

reservation state return to the same route state.

Parked work rejected: direct edits to `BaseLogisticsNetwork`/`StorageCrate` are parked until their owner supplies stable

container hash, AUP, and reservation authority. A blind replacement would create competing truths between legacy

`_reservedSlotIds` and SOA `ReservedLock`.

## Vault Buffers

| BufferID | Value | Payload | Purpose |

|---|---:|---|---|

| `ShinobuInventorySlots` | `73120` | `InventorySlotDTO` | Authoritative global slot array. |

| `ShinobuInventoryActiveSlotCount` | `73121` | `int[1]` | Dense active prefix after compaction. |

| `ShinobuInventoryQueryResults` | `73122` | `InventoryQueryResultDTO[]` | Stable query output for UI/crafting. |

| `ShinobuInventoryQueryCounters` | `73123` | `InventoryAtomicCounter64[]` | False-sharing-safe atomic aggregation counters. |

| `ShinobuInventoryRoutingTelemetry` | `73124` | `InventoryRoutingTelemetryEntry[300]` | 300-frame black-box ring. |

| `ShinobuInventoryRoutingTelemetryCursor` | `73125` | `int[1]` | Telemetry ring cursor. |

| `ShinobuInventoryRoutingTuning` | `73126` | `InventoryRoutingTuningDTO[1]` | Runtime/editor tuning surface. |

| `ShinobuInventoryUiSnapshotA` | `73127` | `InventorySlotDTO[]` | UI double buffer A. |

| `ShinobuInventoryUiSnapshotB` | `73128` | `InventorySlotDTO[]` | UI double buffer B. |

| `ShinobuInventoryStackLimits` | `73129` | `InventoryStackLimitDTO[]` | CSV-hydrated stack limits. |

| `ShinobuInventoryContainerRanges` | `73130` | `InventoryContainerRangeDTO[]` | Atomic container-to-slot-window claims for legacy/authoritative storage publishers. |

| `ShinobuInventoryContainerRangeCount` | `73131` | `int[1]` | High-water range count updated by atomic max. |

| `ShinobuInventoryContainerSyncResult` | `73132` | `InventoryContainerRangeDTO[1]` | Diagnostic result for the most recent snapshot publish/clear operation. |

## DTO Layout

`InventorySlotDTO` is explicit 32 bytes:

- `0..3`: `uint ItemHashID`

- `4..7`: `uint Quantity`

- `8..15`: `ulong ContainerAUPHash`

- `16..19`: `uint ConditionFlags`

- `20..23`: `uint ReservedLock`

- `24..31`: private `ulong _pad0`

The 64-bit AUP hash is 8-byte aligned. The stride is 32 bytes, exactly two 16-byte lanes and an 8-byte multiple.

`InventoryAtomicCounter64` is 64 bytes so worker threads do not mutate adjacent counters inside the same L1 cache line.

`InventoryContainerRangeDTO` is explicit 32 bytes:

- `0..7`: `ulong ContainerHash`

- `8..15`: `ulong ContainerAUPHash`

- `16..19`: `int SlotStart`

- `20..23`: `int SlotCapacity`

- `24..27`: `int ActiveSlotCount`

- `28..31`: `uint StateFlags`

This DTO is not a hot atomic counter, so it is not padded to 64 bytes. Range claims use atomic compare-exchange on

`ContainerHash`; slot mutation remains owned by the chained publish job.

## Execution Route

`InventoryRoutingNetwork.EnsureBuffers()` allocates vault handles only. No persistent private native arrays are declared

by the runtime facade.

The preferred query route is:

1. `ClearInventoryAtomicCountersJob`

2. `AggregateAvailableResourcesPaddedJob`

3. `FlushPaddedTotalsToHashMapJob`

Callers supply a temporary `NativeParallelHashMap<uint,int>` and the vault-owned padded counters. The parallel phase

uses `Interlocked.Add` into 64-byte counters; the serial flush writes the caller-owned hash map for O(1) lookup by item hash.

For dense item catalogs, `ScheduleResourceHashIndexLookup()` clears or reuses caller-owned open-addressed `int` key/total

arrays, streams the active slot window once, then resolves requested hashes through `LookupResourceHashIndexJob`. This makes

the per-item lookup expected O(1) after the deterministic O(N) streaming build. Both padded-counter and hash-index routes

can run as cumulative slices by skipping the clear stage after the first frame.

`InventoryTransactionJob` locks source and destination slots with `Interlocked.CompareExchange` on `ReservedLock`.

The transfer is the Dear Lie: integer quantities change instantly, then `LogisticsTransferSignal` carries presentation

data for pipe UV flow or shader effects. The signal carries source/destination AUP hashes plus a local midpoint offset,

not an absolute float world midpoint. No physical item is simulated.

## Legacy Storage Sync Boundary

Static audit found the current scene-facing path still loops `BaseLogisticsNetwork -> StorageCrate.CountItemByHash`.

It cannot be directly replaced until the construction/gameplay owner provides a stable container identity and AUP;

otherwise SOA `ReservedLock` and legacy `_reservedSlotIds` would become parallel truths and allow double-spend.

The inventory-owned bridge is therefore data-only:

1. Owner supplies a stable `ulong containerHash`, authoritative `double3 containerAUP`, and caller-owned native item hash/quantity/lock arrays.

2. `PublishInventoryContainerSnapshotJob` claims/resolves a fixed SOA slot window, acquires `ContainerRangeMutating`

   through atomic compare-exchange on `StateFlags`, then rewrites only that window.

3. Published slots are marked with `ConditionContainerRangePinned`; legacy reservation state is mirrored into `ReservedLock`.

4. `ClearInventoryContainerRangeJob` clears and releases the range when the owner unregisters the storage container.

Container windows use a fixed `DefaultContainerSlotStride` of 64 slots. A publish request wider than that stride fails with

`ContainerRangeCapacityExceeded`; variable-width window starts are forbidden because `rangeIndex * requestedSlotCapacity`

can overlap unrelated containers. Existing or incoming ranges whose `SlotStart` is not aligned to the fixed stride are

rejected by the publish path.

`ShinobuInventoryContainerSyncResult` is diagnostic only after this repair. State mutation no longer depends on the

shared result slot, so parallel publishers cannot corrupt the claim->publish path by overwriting `ResultRange[0]`.

Callers that need per-container result reads still need a unique result index or a serialized owner readback lane.

No `StorageCrate`, `PowerGrid`, or construction type is referenced from the inventory domain. The storage owner remains the single source of scene mutation semantics.

Pinned ranges are preserved by `CompactInventoryArrayJob`; non-pinned SOA slots still compact normally.

`InventoryTransactionJob` treats `ConditionContainerRangePinned` as slot-window metadata, not item condition data, so

emptying or filling a pinned slot does not release it to compaction.

`ScheduleTransactions()` is the supported transaction entrypoint when presentation signals are enabled; it initializes the

`LogisticsTransferSignal` lane and supplies the queue writer to the Burst job.

## Scalability

`ResolveTimeSliceBatchSize()` consumes continuous `GlobalQualityWeight` through a smooth polynomial curve. Low thermal

weight collapses large scans into small predictable chunks; high weight permits larger slot windows and richer presentation

signals. No binary low/high branch owns query behavior.

Non-finite quality weights collapse to 0.0. Non-finite AUP deltas fail closed for distance-gated queries.

## Rollback

`InventoryRollbackSnapshotJob` performs a blind contiguous `UnsafeUtility.MemCpy` of `InventorySlotDTO[]` into caller-owned

rollback bytes. The job is deterministic and does not depend on Unity time.

## Human Control

`Hecton/Inventory/SOA Routing Tuner` is isolated under `Hecton8.InventoryRouting.Editor`. It references the runtime

asmdef plus Core/Memory because the editor facade resolves `GlobalRegistry.DataVault`, edits vault-backed tuning,

generates the 100k mock network, dumps telemetry, and draws a memory-layout heatmap.

Unity import hygiene: stable `.meta` files exist for `Inventory/Routing`, `InventoryRoutingNetwork.cs`, the runtime asmdef,

the editor folder, tuner window, and editor asmdef. Unity import/runtime proof remains pending.
