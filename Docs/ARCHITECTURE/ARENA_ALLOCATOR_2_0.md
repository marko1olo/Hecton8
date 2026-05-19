# Arena Allocator 2.0

Date: 2026-05-12
Status: STATIC_SOURCE REVIEWED / RUNTIME PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R32 architecture R4/proof-wording correction is the latest artifact-backed local static DOC_GLOBAL boundary for architecture/root documentation. R31 remains the prior current-boundary propagation layer, R30 remains the prior internal-currentness layer, R29 remains the prior stale-gate/global-authority layer, R28 remains the prior interior-boundary layer, and R27 remains the latest source-counter/index snapshot until rerun.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-19 DOC_GLOBAL R32 Current Boundary Note

R32 artifact-backed reread evidence keeps this file as a static memory/allocator contract, not GCMonitor, NativeMemorySentinel, or runtime allocation proof. Current root/architecture boundary is `Docs/Reports/2026-05-19_DOCUMENTATION_R32_ARCHITECTURE_R4_AND_PROOF_WORDING_LOCAL.md`; R31 remains the prior current-boundary propagation correction. R30 remains the prior internal-currentness correction, R29 remains the prior stale-gate/global-authority correction, R28 remains the prior interior-boundary correction, and R27 source counters are retained until a newer counter pass reruns them. Current static gates: `Tools/AtlasCheck.py` remains red on `59` missing refs (RealtimeCSG vendor refs plus absent `VaultXRayWindow.cs` and `HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`); `Docs/Modding/Validate_Mod_API_Static.ps1` now passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only; do not treat PASS as current proof without artifact path, command, timestamp, environment, and output. Unity/runtime/profiler/player-build proof remains absent.

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

STATUS: STATIC_SOURCE REVIEWED / RUNTIME PENDING
