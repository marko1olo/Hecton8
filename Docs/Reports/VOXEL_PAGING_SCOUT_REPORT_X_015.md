# VOXEL_PAGING_SCOUT_REPORT_X_015

Agent: X_015  
Role: VOXEL_PAGING_AND_SECTOR_LAYOUT_SCOUT  
Mode: read-only source audit. No C# source edits.

## Scope

Primary files audited:

- `Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs`
- `Assets/_Project/Scripts/VoxelDeltaProcessor.cs`
- `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs`

Supporting contracts audited:

- `VoxelDeltaCompressionArchitecture.cs`
- `SaveDeltaCompression.cs`
- `VoxelDeltaPersistenceDTO.cs`
- `VoxelSurfaceNetsContracts.cs`
- `PersistencePagingContracts.cs`
- `SaveBinaryStorage.cs`
- `SaveManager.cs`
- `WorldChunkResidencyManager.cs`

## Pager Envelope

`H8BinaryWorldPager` owns the on-disk world page envelope. Constants are declared at `H8BinaryWorldPager.cs:27-62`.

- Sector size: 262144 bytes.
- Sector header: 64 bytes.
- Sector payload: 262080 bytes.
- Max sectors: 8192.
- Directory page: 4096 bytes at file offset 0.
- Sector offset formula: `4096 + (((ulong)sectorHash & 8191) * 262144)` at `H8BinaryWorldPager.cs:1696-1700`.
- Max file span if all sectors are written: 2147487744 bytes.

Page header, 64 bytes, from `H8BinaryWorldPager.cs:2247-2272`:

| Offset | Bytes | Field |
| --- | ---: | --- |
| 0 | 4 | PageMagic `0x48385047` |
| 4 | 2 | Version |
| 6 | 2 | HeaderBytes |
| 8 | 4 | PayloadType |
| 12 | 4 | Flags |
| 16 | 8 | SectorHash |
| 24 | 4 | RawBytes |
| 28 | 4 | StoredBytes |
| 32 | 4 | RawPayloadHash32 |
| 36 | 4 | Frame |
| 40 | 4 | SourceHash |
| 44 | 4 | RequestId |
| 48 | 16 | Reserved zero |

Directory header is written at `H8BinaryWorldPager.cs:1712-1760`. Each directory entry is 16 bytes: `SectorHash` at offset 0 and `SectorOffset` at offset 8, written at `H8BinaryWorldPager.cs:1769-1789`.

Hard risk: `DirectorySlotCount` is 252, but `ResolveDirectorySlot` masks with `DirectorySlotCount - 1` at `H8BinaryWorldPager.cs:1703-1709`. Mask value 251 reaches only 128 slot values. The sector body route still has 8192 offsets; the directory metadata route has elevated collision risk.

## Outer Byte RLE

The pager outer RLE is byte-run encoding over the complete raw page payload, not voxel-cell RLE.

Record format, from `H8BinaryWorldPager.cs:2153-2175`:

| Offset | Bytes | Field |
| --- | ---: | --- |
| 0 | 1 | Value |
| 1 | 2 | RunLength little-endian |

Limit facts:

- Max run length: 65535.
- Max raw payload: 262080 bytes.
- Worst forced RLE size: 786240 bytes.
- Actual worst stored size: 262080 bytes, because expanded RLE is rejected and raw payload is stored.
- Uniform 262080-byte payload encodes as 4 records, 12 bytes.
- Decode validates run length and final output length at `H8BinaryWorldPager.cs:2177-2192`.

## VXRL Pager Payload

`VoxelDeltaCompressionArchitecture` creates the `VXRL` payload passed into `H8BinaryWorldPager`. Route evidence: `VoxelDeltaCompressionArchitecture.cs:584-592`, `SaveManager.cs:863-872`, read request at `WorldChunkResidencyManager.cs:1438-1451`.

Payload limits from `VoxelDeltaCompressionArchitecture.cs:189-198`:

- Outer payload cap: 262080 bytes.
- `VoxelDeltaHeaderDTO`: 32 bytes.
- `VoxelDeltaRleRunDTO`: 8 bytes.
- Max runs per single VXRL page: `(262080 - 32) / 8 = 32756`.

`VoxelDeltaHeaderDTO`, from `VoxelDeltaCompressionArchitecture.cs:31-39` and explicit little-endian writer at `VoxelDeltaCompressionArchitecture.cs:675-704`:

| Offset | Bytes | Field |
| --- | ---: | --- |
| 0 | 8 | SectorHash |
| 8 | 4 | CompressedSize |
| 12 | 4 | UncompressedSize |
| 16 | 8 | XXHash3Checksum |
| 24 | 4 | _pad0 |
| 28 | 4 | _pad1 |

`VoxelDeltaRleRunDTO` is 8 bytes:

| Offset | Bytes | Field |
| --- | ---: | --- |
| 0 | 2 | StartIndex |
| 2 | 2 | RunLength |
| 4 | 1 | SdfValue |
| 5 | 1 | MaterialId |
| 6 | 1 | Flags |
| 7 | 1 | Reserved0 |

A 32768-cell checkerboard delta produces 32768 single-cell runs. That is 262144 run bytes plus 32 header bytes, 262176 total. It does not fit one pager payload. Encoder overflow/fatal path is visible at `VoxelDeltaCompressionArchitecture.cs:1250-1266`, finalization at `VoxelDeltaCompressionArchitecture.cs:1328-1335`, and persistence rejection at `VoxelDeltaCompressionArchitecture.cs:575-592`.

## Native Snapshot

`VoxelDeltaProcessor` has a separate native snapshot used by the main save route (`SaveManager.cs:2918-2964`). This is not the pager sector envelope.

Current snapshot header is 16 bytes, `NativeSnapshotDeltaRleAlignedMagic`, evidence `VoxelDeltaProcessor.cs:6045-6052` and write path `VoxelDeltaProcessor.cs:2197-2206`.

Chunk header is 40 bytes, evidence `VoxelDeltaProcessor.cs:6087-6102`.

Payload modes:

- Dense: 135168 payload bytes, 135208 total record bytes. Evidence `VoxelDeltaProcessor.cs:2356-2362`.
- Uniform SDF RLE: 1 raw byte, padded to 4 bytes, 44 total record bytes. Evidence `VoxelDeltaProcessor.cs:2381-2420`.
- Sparse delta RLE: 8-byte `SaveVoxelDeltaRun8` records. Worst forced full checkerboard is 262144 bytes and does not fit 262080, but active native snapshot code switches to dense when sparse payload exceeds 135168. Evidence `VoxelDeltaProcessor.cs:2423-2454`, `VoxelDeltaProcessor.cs:2482-2562`, `VoxelDeltaProcessor.cs:2364-2367`.

Native snapshot scratch capacity is:

`16 + 256 * 135208 + 256 * 44 = 34624528 bytes`.

Evidence: `VoxelDeltaProcessor.cs:2369-2379` and scratch allocation `VoxelDeltaProcessor.cs:4802-4824`. This fits 256 dense-equivalent records plus 256 uniform records. It does not fit 512 dense-equivalent records. More than 256 non-uniform dense-equivalent records can make snapshot copy fail.

Sparse RLE load validation at `VoxelDeltaProcessor.cs:3216-3294` requires:

- payload multiple of 8 bytes;
- positive run length;
- monotonic non-overlapping runs;
- `startIndex <= ChunkCellCount - runLength`;
- decoded dirty count equals declared dirty count.

## Chunk Recycler

`VoxelDeltaProcessor` chunk delta pool:

- Pool slots: 256.
- Per slot: 4096 dirty mask bytes, 65536 SDF bits bytes, 32768 material bytes, 32768 cell flag bytes.
- Per slot total: 135168 native bytes.
- Pool native total: 34603008 bytes.

Evidence: pool creation and slicing at `VoxelDeltaProcessor.cs:4851-4890`; vault buffer lengths at `VoxelDeltaProcessor.cs:4892-4935`.

Lease flow:

- `TryLeaseChunkState` pops one free slot and clears all storage before use: `VoxelDeltaProcessor.cs:4937-4960`.
- `ReleaseChunkState` resets and returns the slot to the free stack: `VoxelDeltaProcessor.cs:4963-4982`.
- `ClearChunkStateStorage` memclears dirty mask, SDF bits, material IDs, cell flags, and resets dirty count: `VoxelDeltaProcessor.cs:5031-5058`.
- On pool exhaustion, lease fails and emits one performance warning until free slots rise above 64.

Compaction scratch, from `VoxelDeltaProcessor.cs:4705-4785`:

- Source SDF scratch: 2146689 bytes.
- Dirty mask: 4096 bytes.
- Delta SDF: 65536 bytes.
- Material copy: 32768 bytes.
- Flags copy: 32768 bytes.
- Output SDF: 65536 bytes.
- Output material: 32768 bytes.
- Output flags: 32768 bytes.
- Uniform flag: 1 byte.
- Total: 2413930 bytes.
- Single lease guard is active.

Scheduled carve write buffer:

- Capacity: 131072 records.
- Record stride: 32 bytes.
- Total: 4194304 bytes.
- Evidence: `VoxelDeltaProcessor.cs:83`, `VoxelDeltaProcessor.cs:4529-4586`, `VoxelDeltaProcessor.cs:5552-5565`.

## Surface Nets Vault

`VoxelSurfaceNetsVault` is a meshing working-set vault, not a disk sector writer. No RLE encoder or pager sector layout is implemented there.

Constants and DTO strides are in `VoxelSurfaceNetsContracts.cs:7-40` and `VoxelSurfaceNetsContracts.cs:91-337`. Allocation is in `VoxelSurfaceNetsVault.cs:117-228`.

Known native vault bytes: 3335708.

Black box dump writes 32-byte header plus 300 telemetry entries of 64 bytes, total 19232 bytes. Evidence: `VoxelSurfaceNetsVault.cs:672-680`, `VoxelSurfaceNetsVault.cs:993-1012`.

## Findings

1. Directory slot math is wrong for 252 slots because bitmasking requires a power-of-two slot count.
2. A single VXRL page cannot represent 32768 single-cell runs. Maximum is 32756 runs.
3. Native snapshot dense fallback protects per-chunk payload size, but scratch capacity protects only 256 dense-equivalent chunk records plus 256 uniform records.
4. Chunk pool starvation is fail-closed and zero-GC. It refuses a lease instead of allocating emergency memory.

## Verification

No C# source was modified. No build was run because this audit created reports only and did not alter code or project metadata.
