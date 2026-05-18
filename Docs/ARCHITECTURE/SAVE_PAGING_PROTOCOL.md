# SAVE_PAGING_PROTOCOL
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
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Verification: PENDING VERIFICATION

## 2026-05-17 Live-Version Supersession

Status: LEGACY SPEC SNAPSHOT FOR VERSION AUTHORITY.

This document still preserves useful dirty-sector and indexed-paging design history, but its
`Container version: 0x0008` line is not the current runtime truth.

Current source-backed authority:

- `SaveBinaryStorage.CurrentVersion = 0x0009`.
- `SaveBinaryStorage.CurrentHeaderSize = 56`.
- `SaveMasterHashV10.HeaderVersion = 0x000A`.

Use `HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` and
`SUBNAUTICA2_EA_TO_HECTON8_PRODUCTION_CONTRACTS.md` before changing save code or reporting live
save compatibility. Do not use this file alone to claim the current save container version.

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical data boundary snapshot: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Current actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json`.
- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Visual-realistic-fake doctrine snapshot: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`; re-check `.agents-skills` for newer mandates before implementation.
- May 14 DOC_AUDIT override: the cited May 11 compile artifact is absent from the current filesystem; treat that May 11 compile-success line as stale report text. R43 rechecked the current external root `Hecton8*.csproj` no-restore CLI compile surface at `0 Warning(s)` / `0 Error(s)` after restore assets and referenced `Temp\bin\Debug` DLLs exist; full restore graphs still carry vendor/package warnings, and shared `Temp\obj` locks can create transient evidence noise. Runtime, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, import, scene wiring, and visual quality remain `PENDING VERIFICATION`.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
Historical 2026-05-04 boundary:

- This is the save paging protocol contract, not evidence that every sector path has been stress-tested.
- Historical project-state orientation previously started at `Docs/Reports/2026-05-06_DOCUMENTATION_SYNCHRONIZATION_PASS.md`, then `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, then `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.
- Dirty-sector commit, `.sectmp` recovery, `.bak` fallback, and FileStream/native-window offset correctness must be verified with runtime save/load/corruption tests before status can improve.

## Scope

Owner files:

- `Assets/_Project/Scripts/SaveBinaryStorage.cs`
- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`

Historical container version snapshot: `0x0008`.
Current runtime version authority is listed in the 2026-05-17 live-version supersession above.

This document defines dirty-sector commit behavior for the fixed-slot `4096` entry FileStream/native-window save container.

## Dirty Sector Rule

The save system does **not** rewrite the entire file when a single persistent-world sector changes.

Dirty sector commit path:

1. registry writes one temp sector override block (`.sectmp`)
2. storage opens the main `.sav` through `SaveBinaryStorage.AsyncWriteManager`
3. storage resolves the sector slot by `SectorHash`
4. storage reuses the old slot in-place if the new compressed block fits
5. storage appends to EOF only when the new block exceeds the existing slot
6. storage patches one `SectorEntry`
7. storage recomputes directory hash and header hash
8. storage deletes the temp override file

Sector paging integrity path:

1. storage reads one indexed sector block through cached native read windows
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

Directory validation uses subtraction-based bounds checks. Readers must validate sector ranges as:

```text
SectorEntry.ByteOffset >= metadataEndOffset
SectorEntry.CompressedSize > 0
SectorEntry.ByteOffset <= fileLength - SectorEntry.CompressedSize
```

Do not validate sector end with `ByteOffset + CompressedSize <= fileLength`; malformed large offsets can overflow signed addition before the comparison.

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

4. queue/throttle FileStream flush for the changed file range

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

- FileStream-backed native read windows
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

## Mod Payload Sidecar Sectors

Mod save payloads are stored as isolated `16 KB` sub-sector records under the `MODP` sector hash prefix. The main save metadata still carries a hashed fallback key, but the FileStream/native-window sidecar is the authoritative large-payload path.

Batch load path:

1. storage opens and validates the `.sav` once
2. storage streams the fixed `4096` `SectorEntry` slots through cached native read windows
3. storage rejects entries whose byte ranges overlap metadata or exceed file bounds
4. storage decompresses only `MODP` sectors into native scratch
5. each `MODP` sector verifies the sector checksum and payload checksum
6. the mod facade receives validated payload bytes through a cached handler

Rules:

- do not allocate `SectorEntry[4096]` for mod payload scans
- do not reopen the save file once per mod payload
- payload strings are UTF-16; odd payload byte lengths are corruption and must be rejected
- mod payload failure is isolated from player-world save load, but editor/development builds must report the failure

## Defrag Relationship

Dirty-sector commit may create holes when a block relocates to EOF.


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
        write compressed temp block to FileStream-backed temp path
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
