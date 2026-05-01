# SAVE_PAGING_PROTOCOL

Status: REFERENCE
Verification: PENDING VERIFICATION

2026-05-01 current-state boundary:

- This is the save paging protocol contract, not evidence that every sector path has been stress-tested.
- Current project-state orientation starts at `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.
- Dirty-sector commit, `.sectmp` recovery, `.bak` fallback, and MMF offset correctness must be verified with runtime save/load/corruption tests before status can improve.

## Scope

Owner files:

- `Assets/_Project/Scripts/SaveBinaryStorage.cs`
- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`

Container version: `0x0008`

This document defines dirty-sector commit behavior for the fixed-slot `4096` entry MMF save container.

## Dirty Sector Rule

The save system does **not** rewrite the entire file when a single persistent-world sector changes.

Dirty sector commit path:

1. registry writes one temp sector override block (`.sectmp`)
2. storage opens the main `.sav` via MMF
3. storage resolves the sector slot by `SectorHash`
4. storage reuses the old slot in-place if the new compressed block fits
5. storage appends to EOF only when the new block exceeds the existing slot
6. storage patches one `SectorEntry`
7. storage recomputes directory hash and header hash
8. storage deletes the temp override file

Sector paging integrity path:

1. storage reads one indexed sector block via MMF offset
2. block codec decompresses protected `16 KB` sub-blocks independently
3. each sub-block verifies a stored low-32-of-`XXHash3-64` checksum
4. if any protected sub-block fails, storage rejects the primary sector block
5. storage retries that same `SectorHash` from `"{slot}.sav.bak"`
6. only the failed sector falls back; the rest of the world remains resident

## Construction Graph Integrity Boundary

`ConstructionDTO` currently lives in the indexed metadata block, not in a standalone persistent-world sector. The metadata block is still protected by the same `16 KB` LZ4 sub-block framing and low-32-of-`XXHash3-64` validation used for sectors, so `ModuleGraphNodeDTO` payload bytes are protected against partial compressed-block corruption.

Sector-local fallback semantics require a separate construction-sector key before a failed construction graph can be restored independently from `*.sav.bak` while leaving inventory metadata from the primary save resident. Until that sector split exists, a failed metadata block remains a whole-metadata failure, while persistent-world sector failures remain sector-local.

## Master Index

Directory layout is fixed:

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
struct IndexedSectorDirectoryHeader
{
    public uint SectorCount;
    public int ChunkSizeMeters;
    public int MetadataCompressedSize;
    public int MetadataDecompressedSize;
}

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 28)]
struct SectorEntry
{
    public long SectorHash;
    public long ByteOffset;
    public int CompressedSize;
    public int DecompressedSize;
    public uint Checksum;
}
```

Directory capacity:

```text
4096 slots
```

Slot address:

```text
entryOffset = CurrentHeaderSize + IndexedSectorDirectoryHeaderSize + (slotIndex * sizeof(SectorEntry))
```

## Commit Target Resolution

Given:

- `existingEntry`
- `overrideCompressedSize`
- `originalLength`

Commit target math:

```text
if sector slot exists and overrideCompressedSize <= existingEntry.CompressedSize:
    writeOffset   = existingEntry.ByteOffset
    newFileLength = originalLength
    mode          = REUSE_IN_PLACE

else if sector slot exists and overrideCompressedSize > existingEntry.CompressedSize:
    writeOffset   = originalLength
    newFileLength = originalLength + overrideCompressedSize
    mode          = APPEND_AND_RELOCATE

else if sector slot does not exist:
    writeOffset   = originalLength
    newFileLength = originalLength + overrideCompressedSize
    mode          = INSERT_NEW_SLOT
```

## In-Place Reuse

When the new block fits the old slot:

```text
memcpy(filePtr + existingEntry.ByteOffset, overrideBlockPtr, overrideCompressedSize)
trailingSlack = existingEntry.CompressedSize - overrideCompressedSize
if trailingSlack > 0:
    memset(filePtr + existingEntry.ByteOffset + overrideCompressedSize, 0, trailingSlack)
```

Then patch:

```text
SectorEntry.ByteOffset        = existingEntry.ByteOffset
SectorEntry.CompressedSize    = overrideCompressedSize
SectorEntry.DecompressedSize  = overrideDecompressedSize
SectorEntry.Checksum          = overrideChecksum
```

## Append / Relocate

When the new block does not fit:

```text
writeOffset = originalLength
newFileLength = originalLength + overrideCompressedSize
```

The block is copied to EOF. The old region becomes reclaimable slack. Defrag later compacts it.

## Atomic Header / Directory Update

After block write:

1. patch the `SectorEntry`
2. if a new slot was inserted, increment `IndexedSectorDirectoryHeader.SectorCount`
3. recompute:

```text
directoryHash64 = XXHash3-64(directoryHeader + all 4096 sector slots)
HashPayload64   = metadataHash64 ^ directoryHash64
HashHeader64    = XXHash3-64(header-with-HashHeader64-zeroed)
```

4. flush MMF view

This preserves metadata block bytes and updates only:

- one sector block
- one sector entry
- optionally the directory header count
- file header hashes

## Tombstone Protocol

Deleted scene-authored entities are persisted as tombstones keyed by `InstanceUid`.

Rules:

- `ItemFlags` includes `Deleted`
- `InstanceUid` is the authoritative identity
- `ItemHashIndex = ushort.MaxValue`
- chunk index remains valid so the tombstone pages with its original sector
- load path registers the deleted UID before any active record is hydrated

Result:

- save-file deletion wins over scene-authored respawn
- paged sectors suppress ghost entities without whole-scene scans

## Zero-GC Blit Rule

Binary block relocation/overwrite path uses:

- MMF view pointers
- `UnsafeUtility.MemCpy`
- `UnsafeUtility.MemClear`
- native temporary staging (`NativeArray<byte>`)

Managed `byte[]` staging is forbidden in the block commit lane.

## Protected 16 KB Block Validation

Indexed sector blocks and temp sector override blocks use protected internal LZ4 sub-blocks:

```text
[compressedLength:int][rawLength:int][checksum:uint]
```

Rules:

- `rawLength` max = `16384`
- `checksum` = low 32 bits of `XXHash3-64(rawBlockBytes)`
- validation happens immediately after each sub-block decompress
- one bad sub-block invalidates only that sector block load, not the whole save

## Defrag Relationship

Dirty-sector commit may create holes when a block relocates to EOF.

Those holes are reclaimed later by indexed defrag. Dirty commit does **not** compact synchronously.

## Async Dehydration Pipeline

Sector dehydration has two lanes:

1. main thread snapshots Unity-owned state (`Transform`, pooled proxy state, `Rigidbody` velocity) into unmanaged records
2. worker jobs sort, compress, and checksum the unmanaged payload

The main thread is not allowed to call compression and then block for completion.

Entity-state temp page scheduling:

```text
List<EntityDataRecord> scratch
    -> NativeArray<EntityDataRecord>(Allocator.TempJob)
    -> TryScheduleIndexedSectorEntityStateOverrideWrite()
    -> BuildSectorEntityStateSortEntriesJob
    -> RadixSortSectorEntityStateEntriesJob
    -> ExtractSortedSectorEntityStatesJob
    -> CompressSectorEntityStateJob
```

Completion rule:

```text
Tick:
    if handle.IsCompleted == false:
        do nothing
    if same SectorHash has an earlier queued write:
        do nothing
    else:
        TryCompleteIndexedSectorEntityStateOverrideWrite(ref handle)
        write compressed temp block to MMF-backed temp path
        dispose TempJob buffers
```

Shutdown rule:

```text
completed handles are flushed first
uncompleted handles are disposed with NativeArray.Dispose(JobHandle)
no mid-frame Complete() is allowed on the runtime dehydration lane
```

The compression job uses the static `64 KB` save dictionary copied into job-owned native scratch before scheduling. The job writes protected `16 KB` LZ4 sub-blocks and stores low-32-of-`XXHash3-64` for each raw sub-block.

## Time-Sliced Hydration Apply

Dense base restore is applied through the `SaveManager` load loop with a hard frame budget:

```text
LoadApplyFrameBudgetTicks = Stopwatch.Frequency / 250
```

That is a `4.0 ms` budget. During `ISaveable.LoadFromSaveData` application:

```text
deadline = Stopwatch.GetTimestamp() + LoadApplyFrameBudgetTicks
for each saveable:
    LoadFromSaveData(data)
    if Stopwatch.GetTimestamp() >= deadline:
        await Awaitable.NextFrameAsync()
        deadline = Stopwatch.GetTimestamp() + LoadApplyFrameBudgetTicks
```

The rule is intentionally conservative: hydration may span frames, but it cannot monopolize the main thread during dense module/base restores.
