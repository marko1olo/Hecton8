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

R45 root/architecture R43/R44 residue/proof-artifact/source-counter correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not GCMonitor, NativeMemorySentinel, allocation telemetry, or player-build proof.

- `Assets/_Project/Scripts/Core/HectonArenaAllocator.cs`
- `Assets/_Project/Scripts/Core/NativeArenaAllocator.cs`
- `Assets/_Project/Scripts/Core/UnsafeArenaAllocator.cs`
- `Assets/_Project/Scripts/Core/NativeArenaArray.cs`

## 2026-05-20 DOC_GLOBAL R45 Root/Architecture Boundary Note

R45 root/architecture R43/R44 residue/proof-artifact/source-counter correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`) (R44 prior internal-residue/exact-route-field/proof-wording correction) keeps this file as a static memory/allocator contract, not GCMonitor, NativeMemorySentinel, or runtime allocation proof. Current DOC_GLOBAL boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`; R44 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R44_ROOT_ARCHITECTURE_INTERNAL_RESIDUE_EXACT_ROUTE_FIELDS_LOCAL.md`; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current static gates: `Tools/AtlasCheck.py` remains red on `ATLAS_CHECK_FAIL references=6741 missing=59` (one Dynamic Decals missing vendor asset ref, RealtimeCSG vendor icon/readme image refs, and missing HabitatDamageBakePipeline source ref in the current atlas); `Docs/Modding/Validate_Mod_API_Static.ps1` passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only. Runtime proof remains absent.

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
