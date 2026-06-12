# VOXEL_PAGING_SCOUT_REPORT_X_015_APEX_ADDENDUM

Agent: X_015
Scope: read-only re-audit of `H8BinaryWorldPager.cs`, `VoxelDeltaProcessor.cs`, `VoxelSurfaceNetsVault.cs`, and active DTO contracts.
Source code edits: none.

## 1. Disk RLE Packet Layout

There are two RLE layers. They must not be merged.

Outer pager layer:

- Container: `H8BinaryWorldPager` page at `ResolveOffset(sectorHash)`.
- Page header: fixed 64 bytes.
- Stored payload: either raw page payload or pager byte-RLE if `PageFlagCompressed` is set.
- Pager byte-RLE record is manually emitted as 3 bytes: byte value, little-endian ushort run length. It is not the voxel deformation DTO and is not 8-byte aligned. Evidence: `TryCompressRle` writes `value`, low run byte, high run byte at `H8BinaryWorldPager.cs` lines 2153-2174; `TryDecompressRle` consumes the same 3-byte record at lines 2177-2192.

Inner VXRL deformation payload after outer pager decompression:

- Payload offset 0: `VoxelDeltaHeaderDTO`, explicit size 32.
- Payload offset 32: `CompressedBytes`, which may be raw packed `VoxelDeltaRleRunDTO` bytes or LZ4 block bytes.
- The header does not persist raw-vs-LZ4 flags. `HeaderFlagLz4` and `HeaderFlagRaw` are stored in counters/telemetry, not in `VoxelDeltaHeaderDTO`. Evidence: header fields are only sector hash, compressed size, uncompressed size, checksum, and two pads at `VoxelDeltaCompressionArchitecture.cs` lines 31-38; compression job sets counter flags at line 1495.

`VoxelDeltaHeaderDTO` byte map:

| Offset | Size | Field | Type | Evidence |
|---:|---:|---|---|---|
| 0 | 8 | SectorHash | ulong | lines 31-36, writer lines 675-683 |
| 8 | 4 | CompressedSize | uint | lines 34, 681 |
| 12 | 4 | UncompressedSize | uint | lines 35, 682 |
| 16 | 8 | XXHash3Checksum | ulong | lines 36, 683 |
| 24 | 4 | _pad0 | uint | lines 37, 684 |
| 28 | 4 | _pad1 | uint | lines 38, 685 |

`VoxelDeltaRleRunDTO` byte map:

| Offset | Size | Field | Type | Meaning |
|---:|---:|---|---|---|
| 0 | 2 | StartIndex | ushort | First flat cell index in 32^3 chunk. |
| 2 | 2 | RunLength | ushort | Consecutive cell count. Zero is rejected on load. |
| 4 | 1 | SdfValue | sbyte | Quantized runtime density. |
| 5 | 1 | MaterialId | byte | Material for all cells in run. |
| 6 | 1 | Flags | byte | Delta flags for all cells in run. |
| 7 | 1 | Reserved0 | byte | Written as zero by encoder. |

Alignment proof:

- `VoxelDeltaRleRunDTO` is `[StructLayout(LayoutKind.Explicit, Size = 8)]` with `FieldOffset` declarations covering offsets 0 through 7 exactly. Evidence: `VoxelDeltaCompressionArchitecture.cs` lines 17-27.
- The active encoder assigns every field, including `Reserved0 = 0`, before storing the run. Evidence: lines 1252-1259.
- The active packer uses `UnsafeUtility.SizeOf<VoxelDeltaRleRunDTO>()` as the stride and copies whole records with `UnsafeUtility.MemCpy`. Evidence: lines 1400 and 1420.
- Runtime self-audit expects `UnsafeUtility.SizeOf<VoxelDeltaHeaderDTO>() == 32` and `UnsafeUtility.SizeOf<VoxelDeltaRleRunDTO>() == 8`. Evidence: lines 756-757 and 770-771.
- Because the DTO uses explicit offsets and explicit size, there is no compiler-inserted hidden padding inside the 8-byte stride. ARM64 array stride is exactly 8 bytes if the `NativeArray` base pointer is aligned, which Unity native allocations provide for primitive/unmanaged payloads.

Capacity math:

- Hard VXRL payload cap: 262080 bytes.
- Header: 32 bytes.
- Theoretical max run records by constant: `(262080 - 32) / 8 = 32756`. Evidence: constants at lines 195-198.
- Effective raw pack cap in `VoxelDeltaRlePackJob`: `DestinationBytes.Length - 64 = 262016` bytes, so `262016 / 8 = 32752` records. Evidence: line 1402.
- Worst full dirty chunk with one-cell alternating runs: `32768 * 8 = 262144` RLE bytes; with header, `262176` bytes. This exceeds the page payload cap by 96 bytes and exceeds the packer raw reserve by 128 bytes.
- Therefore current VXRL path cannot persist worst-case one-cell-per-run geometry as one sector. It must prune, compress smaller, split, or fail the write.

## 2. 262080-Byte Sector Limit and Address Tables

Constants:

- Sector header: 64 bytes.
- Sector size: 256 KiB = 262144 bytes.
- Sector payload: `262144 - 64 = 262080` bytes.
- Max sectors: 8192.
- Directory page: 4096 bytes.
- Directory header: 64 bytes.
- Directory entry: 16 bytes.
- Directory slot count: `(4096 - 64) / 16 = 252`.

Write enqueue:

- `TryEnqueueWrite` rejects `byteCount > SectorPayloadBytes`. Evidence: `H8BinaryWorldPager.cs` lines 194-205.
- The pager does not split oversized payloads. One enqueue equals one page/sector.
- Write arena slot offset is `slot * 262080`; slot cursor is masked by 31 over 32 write slots. Evidence: lines 230-237.

Write commit:

- Input page payload is optionally compressed by the outer byte-RLE layer. If compression is not smaller or does not fit scratch, raw payload is stored. Evidence: lines 1310-1320 and 2153-2174.
- Page header stores raw byte count and stored byte count separately. Evidence: lines 1327-1337 and 2247-2272.
- Disk offset formula is direct, not directory driven:

```text
sectorIndex = unchecked((ulong)sectorHash) & 8191
sectorOffset = 4096 + sectorIndex * 262144
```

Evidence: `ResolveOffset` lines 1696-1700.

- File end for last possible sector is `4096 + 8192 * 262144 = 2147487744` bytes.
- The write path writes only `64 + storedBytes`, not a zero-padded full 262144-byte sector. Evidence: memory-mapped view length at lines 2078-2094 and copy calls at lines 2102-2105; fallback writes header then stored span at lines 1359-1363.
- Stale bytes can remain after a smaller rewrite inside the same sector, but reads are bounded by `storedBytes` and `rawBytes` from the header. Evidence: read validation lines 1457-1465 and read spans lines 1476-1499.

Page header byte map:

| Offset | Size | Field |
|---:|---:|---|
| 0 | 4 | PageMagic `H8PG` |
| 4 | 2 | PageVersion |
| 6 | 2 | SectorHeaderBytes |
| 8 | 4 | payloadType |
| 12 | 4 | flags |
| 16 | 8 | sectorHash |
| 24 | 4 | rawBytes |
| 28 | 4 | storedBytes |
| 32 | 4 | payload check |
| 36 | 4 | frame |
| 40 | 4 | sourceHash |
| 44 | 4 | requestId |
| 48 | 16 | zero reserved by `MemClear` |

Evidence: writer lines 2247-2272.

Directory:

- Directory header is generated by `EnsureDirectoryPage`. Offsets: 0 magic, 4 version, 6 directory bytes, 8 sector size, 12 max sectors, 16 slot count, 20 entry bytes. Evidence: lines 1718-1728.
- Directory entry is 16 bytes: offset 0 sectorHash, offset 8 sectorOffset. Evidence: lines 1769-1779.
- Directory entry write offset is `64 + ResolveDirectorySlot(sectorHash) * 16`. Evidence: lines 1784-1788.
- `ResolveDirectorySlot` uses `mixed & (DirectorySlotCount - 1)`, i.e. `mixed & 251`, not modulo 252. Evidence: lines 1703-1709.
- Because 252 is not a power of two, this mask reaches only 128 distinct slots. The directory table is collision-prone and cannot represent all 252 slots uniformly.
- Read path ignores the directory and recomputes `ResolveOffset(sectorHash)` directly. Evidence: async read line 1400; direct read line 497.

## 3. Chunk SDF Buffer Reuse and Race Audit

Dirty chunk pool:

- Pool capacity is 256 chunk states.
- Each pool slot owns subarrays: 1024 dirty mask words, 32768 SDF ushort values, 32768 material bytes, 32768 cell flags. Evidence: `VoxelDeltaProcessor.cs` lines 4873-4882.
- Vault buffer lengths are `256 * 1024` dirty mask words and `256 * 32768` cell entries for each cell buffer. Evidence: lines 4907-4929.
- Lease path pops a slot, resets metadata, then clears all arrays. Evidence: lines 4955-4959 and `ClearChunkStateStorage` lines 5031-5058.
- Release path resets metadata and returns the slot to the free stack; it does not clear arrays on release. The next lease clears them. Evidence: lines 4963-4982.

Compaction path:

- Scheduling captures `snapshotWriteVersion = ResolveChunkWriteVersion(request.Address)`. Evidence: line 3965 and stored into request at line 3977.
- It schedules a `VoxelDeltaCopyChunkStateJob` that reads the live dirty arrays and copies them into scratch arrays. Evidence: lines 4021-4031; job reads source arrays at lines 5683-5705.
- The actual compaction job reads scratch arrays, not the pool arrays. Evidence: job assignment lines 4004-4019.
- Commit is non-blocking until the scheduled handle is complete. Evidence: lines 4103-4109.
- If the source sonar version changed, compaction aborts and releases scratch without removing `_chunkStates`. Evidence: lines 4113-4120.
- Current commit stores compacted state only for uniform output. Evidence: lines 4123-4140.
- Dirty state removal happens only when `uniformCompaction`, current write version equals captured write version, and `_chunkStates.TryRemove` succeeds. Evidence: lines 4143-4148.
- `ReleaseChunkState` then returns the old pool slot. Evidence: lines 4147 and 4963-4982.

Carve write version:

- Scheduled carve commit writes cells with `SetCell`, stores the chunk state, increments version, then queues compaction. Evidence: lines 3562-3576.
- `SetCell` marks the dirty bit and increments `DirtyCellCount` on first touch. Evidence: lines 4265-4274.
- `IncrementChunkWriteVersion` reads current version and `TrySet`s `version + 1`; if registry insertion fails, it only writes a black-box sample. Evidence: lines 3814-3819.
- The registry is a fixed linear array; `TrySet` fails when no free slot exists. Evidence: lines 6208-6234 and 6311-6319.

Race result:

- The pager worker does not alias live chunk SDF arrays. `TryEnqueueWrite` copies payload bytes into the pager write arena before the worker writes disk. Evidence: `H8BinaryWorldPager.cs` lines 233-247.
- There is a theoretical race window in compaction: `Tick` can schedule a compaction copy job, then `LateFrameTick` can commit scheduled carve writes before the compaction copy job has necessarily completed. Evidence: `Tick` order lines 351-364; `LateFrameTick` order lines 369-372; compaction copy scheduling lines 4021-4038; carve commit writes lines 3562-3576.
- The version gate prevents normal scheduled-carve loss by refusing to remove the dirty overlay if any later carve incremented the write version. Evidence: lines 4143-4148.
- The version gate is not absolute: if `_chunkWriteVersions.TrySet` fails, the code records a black-box sample but continues. In that condition, a later uniform compaction may see an unchanged version and remove a dirty state that received writes after compaction copied its inputs. That is a theoretical deformation-loss path.
- Non-uniform compaction currently does not replace the dirty state at commit; dirty arrays remain live and are not recycled. Evidence: only uniform branch at lines 4133-4148.

`VoxelSurfaceNetsVault` involvement:

- `VoxelSurfaceNetsVault` owns meshing buffers only: density, vertices, indices, cell vertex map, meshing states, tuning, telemetry, AABBs, modified signals, priorities, indirect args, mock density, physics bake requests, HZB tiles. Evidence: `VoxelSurfaceNetsVault.cs` lines 134-223.
- Surface nets contracts define mesh DTO strides and capacities, not disk RLE packet format. Evidence: `VoxelSurfaceNetsContracts.cs` lines 7-40 and 91-131.
- It does not write VXRL sector payloads and does not recycle `VoxelDeltaProcessor` dirty SDF pools.
