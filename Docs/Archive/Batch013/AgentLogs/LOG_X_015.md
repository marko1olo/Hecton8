# LOG_X_015

## 2026-05-25 - VOXEL_PAGING_AND_SECTOR_LAYOUT_SCOUT

What was wrong:

- Future voxel/persistence work lacked one byte-level ledger for the pager envelope, VXRL payload, native snapshot chunk records, and chunk recycler buffers.
- `H8BinaryWorldPager.ResolveDirectorySlot` uses `mixed & (DirectorySlotCount - 1)` while `DirectorySlotCount` is 252. This reaches 128 slot values, not 252.
- A single VXRL pager payload can store only 32756 8-byte RLE runs plus a 32-byte header. A 32768-cell single-run-per-cell pattern exceeds one page.
- `VoxelDeltaProcessor` native snapshot scratch capacity covers 256 dense-equivalent records plus 256 uniform records, not 512 dense records.

What was done:

- Extracted X_015 assignment from `Docs/Tasks/CURRENT_BATCH.md` and ignored neighboring agent prompts.
- Read selected voxel, streaming, layout, save, zero-GC, telemetry, and registry mandates.
- Audited `H8BinaryWorldPager.cs`, `VoxelDeltaProcessor.cs`, and `VoxelSurfaceNetsVault.cs` read-only.
- Audited supporting DTO and route contracts: `VoxelDeltaCompressionArchitecture.cs`, `SaveDeltaCompression.cs`, `VoxelDeltaPersistenceDTO.cs`, `VoxelSurfaceNetsContracts.cs`, `PersistencePagingContracts.cs`, `SaveBinaryStorage.cs`, `SaveManager.cs`, and `WorldChunkResidencyManager.cs`.
- Wrote the byte-level JSON report to `Docs/Reports/VOXEL_PAGING_SCOUT_REPORT_X_015.json`.
- Wrote the Markdown report to `Docs/Reports/VOXEL_PAGING_SCOUT_REPORT_X_015.md`.

Cinematic cheats used:

- Confirmed pager outer byte-RLE rejects expansion and falls back to raw storage instead of forcing a realistic but oversized compression stream.
- Confirmed native snapshot sparse RLE switches to dense when sparse payload exceeds dense payload size.
- Confirmed `VoxelSurfaceNetsVault` is visual meshing memory, not persistence truth; no sector layout should be inferred from it.

Exact microseconds saved:

- Runtime saved by this read-only pass: 0 us.
- Future guard estimate: avoiding one failed 32768-run VXRL write saves one pager rejection path and one retry path, estimated 400 us per avoided sector write on i3/MX350 class CPU.
- Future guard estimate: early split/dense routing for native snapshot dense-equivalent overflow saves one failed snapshot copy of up to 34624528 bytes, estimated 2500 us per avoided full-copy attempt on low-end silicon.

Verification:

- C# source edits: none.
- Build: not run. Reason: reports only; no source or project metadata changed.

## APEX Re-Audit - X_015

What was wrong: The first report did not draw a hard boundary between pager byte-RLE, VXRL header payload, and 8-byte voxel deformation run records. It also did not state the effective packer cap caused by `DestinationBytes.Length - 64`.

What was done: Re-read `H8BinaryWorldPager.cs`, `VoxelDeltaCompressionArchitecture.cs`, `SaveDeltaCompression.cs`, `VoxelDeltaProcessor.cs`, `VoxelSurfaceNetsVault.cs`, and active contracts. Wrote `Docs/Reports/VOXEL_PAGING_SCOUT_REPORT_X_015_APEX_ADDENDUM.md`.

Findings:
- `VoxelDeltaRleRunDTO` is exactly 8 bytes: ushort start, ushort length, sbyte SDF, byte material, byte flags, byte reserved.
- VXRL payload is 32-byte header plus stored bytes; raw-vs-LZ4 is not persisted in the VXRL header, only in counters/telemetry.
- Pager sector payload cap is exactly 262080 bytes. Pager does not split oversized payloads.
- Direct sector offset is `4096 + (((ulong)sectorHash & 8191) * 262144)`.
- Directory slot calculation masks by 251 although slot count is 252, so only 128 slots are reachable.
- Worst one-cell-run chunk requires 262176 bytes with VXRL header, which exceeds the sector cap by 96 bytes. Effective raw pack cap is 32752 records because of a 64-byte staging reserve.
- Dirty SDF pool slots are cleared on lease, not on release.
- Normal scheduled carve writes are protected by `WriteVersion`, but if `_chunkWriteVersions.TrySet` fails, that guard can fail open. That is a theoretical deformation-loss path under registry pressure.

Cinematic Cheats used: none; read-only persistence audit.

Exact Microseconds saved: 0 us runtime from this audit. Expected avoided future failure cost: 400-900 us per rejected oversized sector write, plus prevention of save corruption from stale compaction removal.
