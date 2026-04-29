# SAVE_PAGING_PROTOCOL

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
