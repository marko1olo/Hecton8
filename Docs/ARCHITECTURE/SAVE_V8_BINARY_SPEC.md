# SAVE_V8_BINARY_SPEC
Date: 2026-05-07

Status: PENDING VERIFICATION
Verification: PENDING VERIFICATION

2026-05-04 current-state boundary:

- This is the binary save container specification, not save/load runtime proof.
- Current project-state orientation starts at `Docs/Reports/2026-05-06_DOCUMENTATION_SYNCHRONIZATION_PASS.md`, then `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, then `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.
- `SaveBinaryStorage.cs` is the source-backed storage owner; slot traversal, corruption fallback, paging, and long-session persistence still require fresh Unity/runtime validation.

## Scope

HECTON-8 save container `0x0008` (`SaveBinaryStorage.CurrentVersion`).
Owner: `Assets/_Project/Scripts/SaveBinaryStorage.cs`.

This document describes the indexed-block storage container used for MMF-backed random access to persistent-world sectors.

## Top-Level Layout

File order:

1. `SaveFileHeader` (`52` bytes)
2. `IndexedSectorDirectoryHeader` (`16` bytes)
3. `SectorEntry[4096]` fixed slot directory
4. compressed metadata block
5. compressed sector blocks

The file is memory-mapped and treated as:

```text
[SaveFileHeader]
[IndexedSectorDirectoryHeader]
[SectorEntry slot 0]
...
[SectorEntry slot 4095]
[Metadata Block]
[Sector Block A]
[Sector Block B]
...
```

## Header Structures

### SaveFileHeader

The validated file header sits at offset `0`.

Important fields for indexed paging:

- `MagicValue`
- `Version`
- `Flags`
- `PlayerOffset`
- `HashPayload64`
- `HashHeader64`

Validation rules:

- `MagicValue == 0x48454354`
- `Version == 0x0008` for indexed block reads
- `Flags` must contain `FlagIndexedSectorBlocks`
- `HashHeader64` must match recomputed header hash
- future versions (`> 0x0008`) are rejected by `TryValidateHeader(...)`

### IndexedSectorDirectoryHeader

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
struct IndexedSectorDirectoryHeader
{
    public uint SectorCount;
    public int ChunkSizeMeters;
    public int MetadataCompressedSize;
    public int MetadataDecompressedSize;
}
```

Semantics:

- `SectorCount`: number of populated sector slots
- `ChunkSizeMeters`: chunk size used for persistent-world packing
- `MetadataCompressedSize`: byte length of metadata block on disk
- `MetadataDecompressedSize`: raw metadata payload size after LZ4/token expansion

## Fixed Master Index

Directory capacity is fixed at `4096` slots.

```csharp
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

Slot semantics:

- empty slot: `CompressedSize <= 0`
- populated slot: valid `ByteOffset`, `CompressedSize`, `DecompressedSize`, `Checksum`

### Slot Address Math

Constants:

- `CurrentHeaderSize = 52`
- `IndexedSectorDirectoryHeaderSize = 16`
- `SectorEntrySize = 28`
- `IndexedSectorDirectoryCapacity = 4096`

Directory byte span:

```text
directoryBytes = 16 + (4096 * 28) = 114704 bytes
```

Metadata block offset:

```text
metadataBlockOffset = CurrentHeaderSize + directoryBytes
                    = 52 + 114704
                    = 114756
```

Entry pointer for slot `i`:

```text
entryOffset = CurrentHeaderSize + IndexedSectorDirectoryHeaderSize + (i * sizeof(SectorEntry))
```

### Slot Resolution

Sector hashes are placed with open addressing over the fixed slot array.

Probe start:

```csharp
ulong hash = unchecked((ulong)sectorHash);
hash ^= hash >> 33;
hash *= 0xff51afd7ed558ccdUL;
hash ^= hash >> 33;
slot = (int)(hash & (4096 - 1));
```

Collision policy:

- linear probe
- insert into first empty slot
- lookup stops at first empty slot or exact `SectorHash`

## Block Format

Each metadata or sector block is written as:

1. `IndexedSectorBlockHeader` (`8` bytes)
2. compressed payload bytes

```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 8)]
struct IndexedSectorBlockHeader
{
    public uint Flags;
    public uint Reserved;
}
```

Flags:

- `FlagLz4Blocks`
- `FlagTokenSubstitution`
- `FlagStaticDictionary`

## Random Access Paging Protocol

To page a sector:

1. memory-map the file
2. validate `SaveFileHeader`
3. validate indexed v8 requirement:
   - `Flags & FlagIndexedSectorBlocks`
   - `Version == 0x0008`
4. read `IndexedSectorDirectoryHeader`
5. hash-probe `SectorEntry[4096]` by `SectorHash`
6. create `MemoryMappedViewStream(ByteOffset, CompressedSize)`
7. read only that block
8. decompress block
9. verify sector checksum
10. hydrate records

No full-file sequential scan is required for sector paging.

## Compression Protocol

The block writer evaluates:

1. plain LZ4
2. static-dictionary LZ4
3. token-substituted LZ4
4. token-substituted + static-dictionary LZ4

Smallest block wins.

Static dictionary owner:

- `Assets/_Project/Scripts/SaveCompressionDictionary.cs`
- dictionary size: `64 KB`

Dictionary seeds include:

- zeroed quest words
- repeated inventory coordinates
- repeated hash/state patterns
- chunk word patterns
- common UTF-16 metadata strings

## Integrity Protocol

### Header Integrity

`HashHeader64` is recomputed from the header with `HashHeader64 = 0` during hashing.

### Directory Integrity

The payload hash for v8 is:

```text
HashPayload64 = metadataHash64 ^ directoryHash64
```

Where:

- `metadataHash64 = XXHash3-64(raw metadata payload)`
- `directoryHash64 = XXHash3-64(directory header + all 4096 slots)`

### Sector Block Integrity

Each populated `SectorEntry.Checksum` stores:

```text
uint sectorChecksum = low32(XXHash3-64(raw sector payload))
```

Read path:

1. decompress sector block
2. verify `decompressedLength == SectorEntry.DecompressedSize`
3. recompute `low32(XXHash3-64(raw sector payload))`
4. compare with `SectorEntry.Checksum`
5. reject hydration on mismatch

## Defragmentation

Defrag operates only on indexed sector blocks.

Slack threshold:

```text
IndexedSectorDefragSlackThresholdBytes = 50 MB
```

Compaction rules:

1. preserve header + full fixed directory + metadata block
2. rewrite populated sector blocks contiguously after metadata
3. patch `SectorEntry.ByteOffset`
4. recompute directory hash
5. recompute `HashPayload64`
6. recompute `HashHeader64`
7. truncate file to compact length

## Rejection Rules

Indexed sector paging must reject:

- missing indexed flag
- header version not equal to `0x0008`
- future save version
- directory count > `4096`
- directory/file bound overruns
- checksum mismatch
- decompressed size mismatch

## Evidence Points In Code

- `SaveBinaryStorage.CurrentVersion`
- `TryValidateHeader(...)`
- `TryValidateIndexedBlockStorageHeader(...)`
- `TryReadIndexedDirectory(...)`
- `TryReadIndexedPersistentWorldDirectory(...)`
- `TryLoadIndexedPersistentWorldSectors(...)`
- `TryDefragmentIndexedPersistentWorldSectors(...)`
