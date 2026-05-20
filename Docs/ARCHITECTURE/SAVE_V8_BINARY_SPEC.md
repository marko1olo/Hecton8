# SAVE_V8_BINARY_SPEC
Date: 2026-05-07

Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R46 root/architecture interior-authority/route-field/proof-language correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM. These anchors prove only that the cited local paths exist at this capture time; they are not compile, Unity import, Play Mode, profiler, GC, player-build, save/load, platform, or visual proof.
- `Assets/_Project/Scripts/SaveBinaryStorage.cs`
- `Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs`
- `Assets/_Project/Scripts/SaveBinaryStorageNativeArrayExtensions.cs`
- `Assets/_Project/Scripts/SaveSystem/SaveMasterHashV10.cs`

## 2026-05-17 Live-Version Supersession

Status: LEGACY SPEC SNAPSHOT FOR VERSION AUTHORITY.

This document still preserves useful indexed-sector layout history, but its `0x0008` and
`CurrentHeaderSize = 52` claims are not the current runtime truth.

Current source-backed authority:

- `SaveBinaryStorage.CurrentVersion = 0x000B`.
- `SaveBinaryStorage.CurrentHeaderSize = 56`.
- `SaveBinaryStorage.AlignedSectionHeaderVersion = 0x000B`.
- `SaveMasterHashV10.HeaderVersion = 0x000A` is staged hash-helper context, not the active writer version.

Use `HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` and
`SUBNAUTICA2_EA_TO_HECTON8_PRODUCTION_CONTRACTS.md` before changing save code or reporting live
save compatibility. Do not use this file alone to claim current save version, header size, or
future-version rejection behavior.

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical data boundary snapshot: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Historical actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json` (historical snapshot only; do not use for current counts or proof).
- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Visual-realistic-fake doctrine snapshot: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`; re-check `.agents-skills` for newer mandates before implementation.
- Historical May 14/R43 CLI compile wording is stale report text, not current proof. Current static/tool boundary is R46 root/architecture interior-authority/route-field/proof-language correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`) (R45 prior R43/R44 residue/proof-artifact/source-counter correction); R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers; AtlasCheck fails `ATLAS_CHECK_FAIL references=6741 missing=59` (one Dynamic Decals missing vendor asset ref, RealtimeCSG vendor icon/readme image refs, and missing HabitatDamageBakePipeline source ref in the current atlas); Mod API static validation passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only; do not treat PASS as current proof without artifact path, command, timestamp, environment, and output. Unity import, Console, Play Mode, profiler, GCMonitor, player build, scene wiring, save/load, and visual proof remain PENDING VERIFICATION.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
Historical 2026-05-04 boundary:

- This is the binary save container specification, not save/load runtime proof.
- Historical project-state orientation previously started at `Docs/Reports/2026-05-06_DOCUMENTATION_SYNCHRONIZATION_PASS.md`, then `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, then `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.
- `SaveBinaryStorage.cs` is the source-backed storage owner; slot traversal, corruption fallback, paging, and long-session persistence still require fresh Unity/runtime validation.

## Scope

Historical HECTON-8 save container snapshot `0x0008`.
Current runtime version authority is listed in the 2026-05-17 live-version supersession above.
Owner: `Assets/_Project/Scripts/SaveBinaryStorage.cs`.

This document describes the indexed-block storage container used for FileStream/native-window random access to persistent-world sectors. Older memory-mapped wording in this file is stale unless explicitly marked historical.

## Top-Level Layout

File order:

1. `SaveFileHeader` (`52` bytes)
2. `IndexedSectorDirectoryHeader` (`16` bytes)
3. `SectorEntry[4096]` fixed slot directory
4. compressed metadata block
5. compressed sector blocks

The file is treated as a binary layout addressed by offsets:

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

`SaveFileHeader` is contractually located at offset `0`. Runtime validation of real save files still requires linked save/load/corruption artifacts with command, timestamp, environment, and output.

Important fields for indexed paging:

- `MagicValue`
- `Version`
- `Flags`
- `PlayerOffset`
- `HashPayload64`
- `HashHeader64`

Historical v8 validation rules:

- `MagicValue == 0x48454354`
- `Version == 0x0008` for indexed block reads in this historical snapshot
- `Flags` must contain `FlagIndexedSectorBlocks`
- `HashHeader64` must match recomputed header hash
- future versions (`> 0x0008`) were rejected by the v8-era `TryValidateHeader(...)` contract; re-check current source before reporting live behavior

### IndexedSectorDirectoryHeader

Legacy v8 file-format illustration only. These snippets are not current runtime DTO authority;
current save truth is active writer `0x000B` / 56-byte header / `AlignedSectionHeaderVersion = 0x000B`, while `SaveMasterHashV10.HeaderVersion = 0x000A` remains staged helper context. Runtime layouts must follow ARM64 natural/explicit alignment.

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

1. open/read the file through `SaveBinaryStorage.AsyncWriteManager`
2. validate `SaveFileHeader`
3. validate indexed v8 requirement:
   - `Flags & FlagIndexedSectorBlocks`
   - `Version == 0x0008`
4. read `IndexedSectorDirectoryHeader`
5. hash-probe `SectorEntry[4096]` by `SectorHash`
6. copy only the required byte window through cached native read windows
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

- unresolved on current disk as of the latest static root/architecture boundary; dictionary-LZ4 remains future-only unless a fresh path check/artifact tuple links the owner source path, command/tool, timestamp, environment, and output
- dictionary size target: `64 KB`
- keep dictionary-LZ4 as a future contract until a current owner path is added

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

Status: PENDING VERIFICATION


Verification: PENDING VERIFICATION


