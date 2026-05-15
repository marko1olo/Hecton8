# Arena Allocator 2.0

Date: 2026-05-12
Status: SOURCE VERIFIED / RUNTIME PENDING
Owner Source: `Assets/_Project/Scripts/Core/HectonArenaAllocator.cs`

## Constants

| Constant | Value | Meaning |
|---|---:|---|
| `DefaultArenaBytes` | 104,857,600 | default persistent arena reservation |
| `CacheLineAlignment` | 64 | base allocation alignment |
| `MinimumAllocationAlignment` | 16 | API floor before normalization |
| `ArenaBufferCount` | 2 | double-buffered read/write arenas |
| `MaxArenaAlignment` | 4096 | rejected above this |
| `MaxSlabCount` | 64 | upper bound for processor-derived slab count |
| `OwnerTelemetryCapacity` | 32 | fixed owner byte accounting slots |

## Physical Layout

```text
basePtr
  arena 0
    slab 0
    slab 1
    ...
  arena 1
    slab 0
    slab 1
    ...
```

The allocator reserves one persistent unmanaged block with `UnsafeUtility.Malloc(capacity, 64, Allocator.Persistent)`. Each frame writes into one arena while the previous frame can remain readable through the other arena.

## Slab Resolution

`ResolveSlabCount()` uses `SystemInfo.processorCount`, clamps to at least 1, and caps at `MaxSlabCount`.

`ResolveAlignedCapacity()` splits the requested capacity into two arenas, divides each arena by slab count, and rounds slab capacity down to a 64-byte boundary.

## Bump Pointer Logic

Allocation path:

```text
NormalizeAlignment(requested)
arenaIndex = write arena
preferredSlab = thread-static slab
try preferred slab
try other slabs
on failure publish OOM telemetry
```

`TryAllocateFromSlab` reads the slab cursor, aligns it, computes `nextCursor`, and commits with `Interlocked.CompareExchange`. The allocation is lock-free at slab level. Contention retries only the losing CAS.

## Alignment Law

`NormalizeAlignment`:

1. clamps below 16 to 16
2. raises below 64 to 64
3. rejects above 4096
4. rounds up to next power of two

This makes hot scratch arrays cache-line safe and SIMD-friendly.

## Frame Boundary

`EndFrameSwap()` is dispatcher-owned:

1. publish last-frame high-water accounting
2. swap read/write arena indices
3. reset the new write arena cursors
4. recreate safety handle for the new write arena when collection checks are enabled
5. increment frame sequence

## Telemetry

The allocator records:

- used bytes
- last-frame high-water bytes
- OOM count
- owner frame bytes
- owner high-water bytes

OOM path publishes through `GlobalTelemetryBus.PublishPerformanceWarning(...)` with `ArenaOomHash`.

## H8Memory Tracking Gate

Persistent H8 allocations are all-or-nothing:

- tracking-table initialization clamps caller capacity to `MaxTrackingCapacity`;
- native arrays and raw allocations are exposed only after owner tracking succeeds;
- raw reallocation registers the replacement block before freeing the old block;
- if allocation tracking or memory-map descriptor registration fails after native memory is acquired, the new allocation is freed and `FatalMemoryException` is thrown;
- block descriptor storage grows up to `MaxTrackingCapacity` instead of silently dropping new descriptor evidence;
- freed non-vault allocation descriptors are tombstoned with `Bytes=0` so descriptor slots are reusable across allocation churn;
- reused tombstone descriptor slots advance generation from the previous slot unless the incoming domain generation is already higher;
- descriptor generation advances are normalized through one positive-generation helper on reuse, free, and owner-key mutation paths;
- tracking-table growth rebuilds the pointer-owner map all-or-nothing; duplicate/corrupt pointer evidence aborts growth before old tables are disposed;
- owner-gated free and raw reallocation cross-check the pointer-owner map before trusting the allocation record scan;
- read-only aliases require a concrete `SystemID` reader at the DataVault and H8Memory boundaries;
- generation handles and direct buffer aliases fail closed unless the target block is marked as externally viewed before returning;
- DataVault arena initialization and block splitting fail closed if H8 sub-block descriptors cannot be registered.
- DataVault bootstrap clamps caller capacity to `MaxBufferCapacity`; MacroDB native-cache reserve requests above that ceiling fail closed before persistent maps/lists are allocated.
- MacroDB native payload insertion is rejected above the DataVault-local 256 KiB payload ceiling before native memory is allocated.

## Legal Uses

- frame-transient scratch buffers
- job staging arrays with bounded lifetime
- read-after-write use that survives exactly one frame through the read arena

## Forbidden

- persistent game state
- managed payloads
- holding returned `NativeArray<T>` after the next frame boundary unless the owner explicitly uses read-arena semantics
- using the arena as a replacement for save/load persistent storage
- allocating in a loop without owner hash telemetry

## Hardware Impact

| Tier | Expected Effect |
|---|---|
| i3/MX350 | avoids TempJob churn and lowers allocator spikes during dense frame work |
| mid tier | reduces allocation jitter in simulation and presentation bridges |
| high/ultra | buys room for wider visual systems without letting GC become the bottleneck |

STATUS: SOURCE VERIFIED / RUNTIME PENDING
