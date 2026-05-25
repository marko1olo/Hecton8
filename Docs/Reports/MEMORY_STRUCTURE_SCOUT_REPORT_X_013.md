# MEMORY_STRUCTURE_SCOUT_REPORT_X_013

Evidence class: STATIC_SOURCE. Source files were not modified.

## Source Scope

| File | Lines | Role |
|---|---:|---|
| `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` | 4529 | `IDataVault`, handles, metadata, locks, alias pins, defrag, arena relocation |
| `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` | 4065 | `SystemID`, `BufferID`, sentinel descriptors, allocation records, raw allocation/reallocation |

No production partial definitions were found for `GlobalDataVault`, `H8Memory`, or `IDataVault`. The only non-source hit was a Python test string stub in `Tools/test_global_authority_gate.py`.

## Declarations

`GlobalDataVault.cs`: `MemoryDefragPhase` at line 19, `IDataVault` at line 29, `GlobalDataVault` at line 412.

Metadata structs: `VaultGenerationHandle<T>` 224, `VaultBufferHandle<T>` 238, `VaultSliceHandle<T>` 262, `VaultRelocationRecord` 278, `VaultBufferMeta` 295, `VaultTelemetrySnapshot` 315, `VaultMemoryBudgetEntry` 333, `VaultMemoryBlockSnapshot` 347, `VaultArenaBlock` 364, `MemoryDefragTelemetryEntry` 377, `MacroDatabasePayloadCacheEntry` 467.

Nested job structs: `InitializeVaultMetadataJob` 4391, `InitializeVaultBudgetEntriesJob` 4404, `GenerateMockVaultRelocationJob` 4415, `SweepOrphanedHandlesJob` 4445, `VaultDefragmentationJob` 4487.

`H8Memory.cs`: `SystemID` 16, `BufferID` 89-2005, `H8AllocationFlags` 2008, `H8BlockState` 2019, `H8MemoryTelemetryFlags` 2026, `BlockDescriptor` 2044, `H8AllocationRecord` 2062, `H8MemoryTelemetryEntry` 2082, `FatalMemoryException` 2100, `H8Memory` 2165.

## Layout Findings

All audited metadata structs use `LayoutKind.Sequential` with explicit `Size` or `LayoutKind.Explicit` with `Size`. A target-file scan found no `StructLayout(... Pack=...)` usage in `GlobalDataVault.cs` or `H8Memory.cs`.

| Struct | Size | Layout | ARM64 result | Evidence |
|---|---:|---|---|---|
| `VaultGenerationHandle<T>` | 16 | Sequential | PASS | `GlobalDataVault.cs:223-230`, `787-791` |
| `VaultBufferHandle<T>` | 16 | Sequential | PASS | `GlobalDataVault.cs:237-244`, `792-795` |
| `VaultSliceHandle<T>` | 32 | Sequential | PASS | `GlobalDataVault.cs:261-272`, `796-803` |
| `VaultRelocationRecord` | 32 | Explicit | PASS | `GlobalDataVault.cs:277-292`, `818-826` |
| `VaultBufferMeta` | 64 | Explicit | PASS | `GlobalDataVault.cs:294-312`, `869-884` |
| `VaultTelemetrySnapshot` | 64 | Explicit | PASS | `GlobalDataVault.cs:314-330`, `827-839` |
| `VaultMemoryBudgetEntry` | 32 | Explicit | PASS | `GlobalDataVault.cs:332-341`, `840-845` |
| `VaultMemoryBlockSnapshot` | 48 | Explicit | PASS | `GlobalDataVault.cs:346-361`, `846-857` |
| `VaultArenaBlock` | 32 | Explicit | PASS | `GlobalDataVault.cs:363-374`, `885-892` |
| `MemoryDefragTelemetryEntry` | 128 | Explicit | PASS | `GlobalDataVault.cs:376-407`, `893-920` |
| `MacroDatabasePayloadCacheEntry` | 48 | Explicit | PASS | `GlobalDataVault.cs:466-471`, `939-940` |
| `BlockDescriptor` | 40 | Explicit | PASS | `H8Memory.cs:2043-2056`, `2396-2406` |
| `H8AllocationRecord` | 48 | Explicit | PASS | `H8Memory.cs:2061-2076`, `2414-2426` |
| `H8MemoryTelemetryEntry` | 64 | Explicit | PASS | `H8Memory.cs:2081-2098`, `2434-2448` |

ARM64 basis: every audited total size is a multiple of 8. Every 8-byte field or pointer starts at an 8-byte offset. `MacroDatabasePayloadCacheEntry.Pointer` is at offset 40; the nested `MacroDatabasePayloadHandle` is 40 bytes and contains `_pad0` at `MacroDatabaseContracts.cs:146`.

## Lock Facts

`BlockFlagLocked = 1 << 1` at `GlobalDataVault.cs:435`.

`TryAcquireWriteLock<T>` signature: `public bool TryAcquireWriteLock<T>(in VaultGenerationHandle<T> handle, SystemID systemID, out NativeArray<T> buffer) where T : struct` at `GlobalDataVault.cs:1435`.

Facts:
- Rejects `SystemID.Unknown` at `1438-1439`.
- Validates handle generation at `1445-1449`.
- Validates caller owner against `meta.Owner` at `1451-1455`.
- Stores writer owner in `VaultBufferMeta.ActiveWriterSystemID` by `Interlocked.CompareExchange` at `1464-1467`.
- Sets `VaultArenaBlock.Reserved0 |= BlockFlagLocked` at `1469`.
- Increments `VaultArenaBlock.Reserved1` at `1470`.
- Does not set `_activeLocks` / `ActiveBurstLockMask`.
- Calls `TryResolveHandle` after setting the block lock at `1491-1492`; that resolver rejects `_compactionFence != 0` at `1269`.

`ReleaseWriteLock<T>` signature: `public bool ReleaseWriteLock<T>(in VaultGenerationHandle<T> handle, SystemID systemID) where T : struct` at `GlobalDataVault.cs:1503`.

Release facts:
- Requires `ActiveWriterSystemID == systemID` at `1524-1527`.
- Calls `ReleaseWriterBlockLock` at `1529`.
- Clears `ActiveWriterSystemID` by `Interlocked.CompareExchange` at `1532`.
- `ReleaseWriterBlockLock` decrements `Reserved1` at `1548` and clears `BlockFlagLocked` from `Reserved0` when count reaches zero at `1549-1550`.

`TryLockBuffer(BufferID bufferId, SystemID lockOwner)` at `1803`:
- Rejects `BufferID.Unknown` at `1805-1806`.
- Rejects `meta.ActiveWriterSystemID != 0` at `1815-1816`.
- Increments `Reserved1` at `1822`.
- Sets `Reserved0 |= BlockFlagLocked` at `1823`.
- Sets active burst bit at `1841`.
- `lockOwner` is not read or stored.

`TryUnlockBuffer(BufferID bufferId, SystemID lockOwner)` at `1853`:
- Rejects `Reserved1 == 0` at `1865-1867`.
- Decrements `Reserved1` at `1869`.
- Clears `BlockFlagLocked` when `Reserved1 == 0` at `1870-1871`.
- Clears active burst bit if no locked block with the same bit remains at `1874`, `1935-1970`.
- `lockOwner` is not read or validated.

`ActiveBurstLockMask` is `_activeLocks` read through `Volatile.Read` at `542`. The bit is `1 << ((uint)(int)bufferId & 31u)` at `1915-1918`. This is a 32-bit collision domain.

## Alias Pinning

`TryOpenAliasBuffer<T>` rejects `_compactionFence != 0` at `1195-1198`, creates a `NativeArray` view at `1237`, then calls `MarkExternalView` at `1240`. `MarkExternalView` sets `BlockFlagExternalView` in `Reserved0` at `3352`. Defrag checks `BlockFlagExternalView` at `3187-3190` and `3254-3258`.

## Defrag Flow

Entry: `FrostTickDefrag(float elapsedSeconds, float systemStress01, MemoryDefragPhase phase, uint activeBurstLockMask)` at `2191`.

Sequence:
1. Rejects uninitialized or null arena at `2193-2194`.
2. Rejects invalid elapsed/stress values at `2198-2207`.
3. Rejects non-`PreSimulation` phase at `2210-2215`.
4. Computes burst/stress gates at `2217-2223`.
5. Runs `AnalyzeGaps` and telemetry/block validation at `2225-2231`.
6. If fragmented or force requested, estimates largest move candidate at `2233-2238`.
7. Calls `TryRunLiveCompactionSlice` only when not stress halted and no active burst lock at `2240-2242`.

`TryRunLiveCompactionSlice` at `3144`:
- Rejects `_memMoveBlockedByStress`, `_allocationLock`, existing `_compactionFence`, active locks, invalid block state, or null arena at `3146-3155`.
- Sets `_compactionFence` with `Interlocked.Exchange` at `3161`.
- Per candidate, rechecks `HasActiveBurstLocks` at `3167-3170`.
- Selects a free block followed by an occupied block at `3173-3179`.
- Skips locked block when `(Reserved0 & BlockFlagLocked) != 0 || Reserved1 != 0` at `3181-3185`.
- Skips external view when `(Reserved0 & BlockFlagExternalView) != 0` at `3187-3190`.
- Calls `TryMoveOccupiedBlockLeft` at `3201`.
- Clears `_compactionFence` in `finally` at `3213-3215`.

`TryMoveOccupiedBlockLeft` at `3232`:
- Verifies adjacency, occupied/free state, sizes, key, and arena bounds at `3241-3250`.
- Rechecks lock/external flags at `3254-3258`.
- Verifies 64-byte alignment at `3261-3267`.
- Verifies metadata and old pointer at `3277-3290`.
- Moves bytes with `UnsafeUtility.MemMove(newAddress, oldAddress, occupiedBlock.Bytes)` at `3292-3293`.
- Writes moved/free blocks at `3308-3309`.
- Updates H8 descriptors at `3310-3311`.
- Updates metadata at `3313-3316`.
- Updates `_buffers` through `PublishMovedBufferPointer` at `3317`, implemented at `3840-3843`.
- Records relocation at `3318`, implemented at `3800-3827`.

Arena growth relocation:
- `TryEnsureVaultBuffer` can call `TryGrowArenaForBytes` at `1012`, `1076`, and `1114`.
- `TryGrowArenaForBytes` blocks `_allocationLock` and `_compactionFence` at `3642-3650`, but does not check `HasActiveBurstLocks`.
- `TryGrowArena` calls `H8Memory.ReallocateRaw` at `3690-3698`.
- `H8Memory.ReallocateRaw` uses `UnsafeUtility.MemMove` at `2741`, unregisters/frees the old pointer at `2752-2753`.
- `RefreshBlocksAfterArenaRelocation` updates versions, metadata, buffer pointers, and relocation records at `3764-3797`.

## Risk Vectors

`RISK-GDV-001 RED_STATIC`: `TryAcquireWriteLock` does not check `_compactionFence` before mutating `ActiveWriterSystemID`, `Reserved0`, and `Reserved1`. `TryResolveHandle` blocks the returned view when the fence is active, but the mutation has already happened. Evidence: `1435-1499`, `1269`, `3161-3215`.

`RISK-GDV-002 RED_STATIC`: `TryLockBuffer(BufferID,SystemID)` and `TryUnlockBuffer(BufferID,SystemID)` ignore `lockOwner` and do not check `_compactionFence`. Evidence: `1803-1842`, `1853-1875`.

`RISK-GDV-003 RED_STATIC`: arena growth relocation can move the entire arena without checking `_activeLocks` or the caller-provided `activeBurstLockMask`. Evidence: `1012`, `1076`, `1114`, `3642-3650`, `3690-3698`, `H8Memory.cs:2741`, `H8Memory.cs:2752-2753`. Failure mode: stale external job pointer after arena reallocation.

`RISK-GDV-004 YELLOW_STATIC`: `ActiveBurstLockMask` is 32 bits and uses `bufferId & 31`. Collision blocks unrelated buffers conservatively. Evidence: `1915-1918`, `1935-1970`.

`RISK-GDV-005 YELLOW_STATIC`: explicit writer locks from `TryAcquireWriteLock` do not set `_activeLocks`; defrag sees them only by per-block `Reserved0`/`Reserved1`, not by `ActiveBurstLockMask` telemetry. Evidence: `1435-1499`, `542`, `1841`.

## Verification

No C# source files were edited by X_013. Post-audit `git status` reports `GlobalDataVault.cs` and `H8Memory.cs` as dirty in the shared worktree; X_013 used read-only commands only against those files. No compile/build was run because the task is read-only source archaeology. The report is static-source evidence only, not runtime proof.
