# LOG_CORE_DELTA_COMPRESSION

## 2026-05-11 CORE_DELTA_COMPRESSION

Status: PENDING VERIFICATION

What was wrong:
- Native voxel snapshots still serialized dense per-chunk SDF/material/flags payloads for dirty chunks. That violates delta-only persistence and wastes IO on sparse edits.
- Chunk payload validation was aggregate-oriented; one bad voxel payload could invalidate the entire snapshot path.
- Save packing primitives for entity health/hunger/status, AUP half-local offsets, zero-alloc byte swap, and strict 64-byte header did not exist as a small unmanaged save contract.
- Indexed sector override append/header-flip existed, but appended replacement sectors left stale blocks behind.
- The project has external compile blockers outside save domain, so build verification cannot be honestly marked clean.

What was done:
- Added `Assets/_Project/Scripts/SaveSystem/SaveDeltaCompression.cs`.
- Added `SaveVoxelDeltaRun8`: `StartIndex ushort`, `RunLength ushort`, quantized `SdfValue sbyte`, material, flags, reserved byte.
- Added `PackedEntityState32` and `PackEntityState32`/`UnpackEntityState32` using health10/hunger10/status12.
- Added `QuantizedAupSectorHalf3`: int3 sector + 6-byte half local offset.
- Added `StrictSaveFileHeader64` and `SaveChunkHeader32`.
- Added bitwise zero-alloc `ByteSwap32`, `ByteSwap64`, and `ByteSwap32InPlace`.
- Reworked `VoxelDeltaProcessor.CaptureNativeSnapshot` new-format snapshots to `NativeSnapshotDeltaRleMagic`.
- Added sparse RLE writers for dirty chunks and compacted chunks.
- Added `NativeSnapshotChunkHeaderDeltaRle` with `PayloadHash64`.
- Added load-time XXHash3 validation and corruption skip. Hash/malformed failures publish `SAVE_CORRUPTION_HASH` and discard only that chunk delta.
- Added RLE validation for sorted, non-overlapping runs before native state allocation.
- Added `SaveBinaryStorage.TryCompactIndexedPersistentWorldSectors`.
- Added single-flight background defrag dispatch after sector override commits.
- Wrote `Docs/AgentLogs/RECON_CORE_DELTA_COMPRESSION.md` with forbidden API scan.
- Updated `Docs/Tasks/Status_CORE_DELTA_COMPRESSION.md` and `Docs/AgentLogs/Rationale_CORE_DELTA_COMPRESSION.md`.

Cinematic cheats used:
- SDF delta persistence uses signed-byte quantization instead of half/float precision. Visual mesh rebuild hides sub-byte density loss; deterministic base terrain remains authority.
- Uniform compacted chunks use 2-byte SDF payload plus header instead of full 32^3 dense data.
- Background defrag is a maintenance pass with single-flight dispatch; no foreground exact file packing during gameplay frames.
- Hash failure drops a chunk delta and trusts procedural base regeneration instead of expensive recovery.

Exact microseconds saved:
- Sparse dirty chunk snapshot: estimated 220-520 us saved per dirty-light chunk on i3/MX350 by avoiding dense 160 KB native payload copies.
- Adjacent RLE compression: estimated 40-180 us saved per laser-cut style chunk by shrinking compression input.
- Bit-packed entity state: estimated 0.02 us/entity less serialization copy and 8 bytes/entity saved.
- AUP half-local quantization: estimated 0.03 us/entity less write bandwidth.
- Unsafe native inventory shadow dump: existing path verified, estimated 80-300 us saved on full inventory save.
- Background sector compaction: estimated 300-900 us saved on later loads after repeated sector overwrites by restoring sequential locality.
- Byte swap: estimated 0.01 us/value and zero GC versus temporary byte arrays.
- Closure-free defrag queue polish: one managed closure allocation removed per sector defrag request.

Verification:
- `validate_script Assets/_Project/Scripts/VoxelDeltaProcessor.cs`: passed, 0 diagnostics.
- `validate_script Assets/_Project/Scripts/SaveSystem/SaveDeltaCompression.cs`: passed, 0 diagnostics.
- `validate_script Assets/_Project/Scripts/SaveBinaryStorage.cs`: passed once after first compaction edit; later retries timed out on the large file, but broad dotnet compile reached unrelated project blockers without save-system errors.
- `dotnet build .\Hecton8\Assembly-CSharp.csproj --no-restore --nologo -v:q -clp:ErrorsOnly`: failed on unrelated `Assets/_Project/Scripts/HectonSurvivalSystem.cs(298,29): SurvivalPhysiologyScalarResult` in `Hecton8.Core.csproj`.
- Earlier Unity console also contained unrelated `PlayerCriticalProceduralAudioRenderer.cs` missing symbol errors and a pre-existing Burst `catch` filter error in `SaveBinaryStorage.cs`.

Final Git Diff, scoped:
- `Assets/_Project/Scripts/SaveSystem/SaveDeltaCompression.cs`: new unmanaged save packing and RLE run contracts.
- `Assets/_Project/Scripts/VoxelDeltaProcessor.cs`: sparse RLE capture/load, sbyte SDF quantization, per-chunk hash validation, corruption fallback.
- `Assets/_Project/Scripts/SaveBinaryStorage.cs`: indexed sector compaction and single-flight background defrag dispatch.
- `Docs/Tasks/Status_CORE_DELTA_COMPRESSION.md`: task state and verification blockers.
- `Docs/AgentLogs/Rationale_CORE_DELTA_COMPRESSION.md`: decisions and Omega Polish notes.
- `Docs/AgentLogs/RECON_CORE_DELTA_COMPRESSION.md`: forbidden API reconnaissance output.

Integrator notes:
- Full project compile is blocked outside this domain. Do not treat this save-system patch as cleanly verified until external Hecton8.Core errors are fixed.
- `SaveBinaryStorage.cs` had extensive pre-existing dirty changes in the shared worktree before this agent's edits. I did not revert them.

## 2026-05-12 R&D CONTINUATION

Status: PENDING VERIFICATION

What was wrong:
- Sparse RLE payloads stored signed-byte SDF values, but the run detector still split runs on raw half-bit differences. This preserved precision that was discarded before disk write and left compression on the table.

What was done:
- Updated dirty-chunk RLE counting and writing to compare `QuantizeSdfByte(state.SdfValueBits[index])`.
- Updated compacted-chunk RLE counting and writing to compare quantized signed-byte values from `ResolveCompactedMergedCell`.
- Kept material and flags as hard run boundaries.

Cinematic Cheats used:
- Terrain scar persistence treats near-equal SDF half values as identical when they quantize to the same signed byte. Mesh rebuild and deterministic base terrain hide the discarded sub-byte density detail.

Exact Microseconds saved:
- Estimated 8-70 us saved per noisy dirty chunk on i3/MX350 by reducing run count, payload hash length pressure, and compression input.

Verification:
- `validate_script Assets/_Project/Scripts/VoxelDeltaProcessor.cs`: passed, 0 diagnostics after the patch.

## 2026-05-12 R&D CONTINUATION PASS 2

Status: PENDING VERIFICATION

What was wrong:
- Sparse RLE payload writes used a separate `runIndex` from `payloadCursor`, then advanced the byte cursor after the payload was already written. This was valid, but it kept two sources of truth for byte position inside a save-file boundary.

What was done:
- Replaced indexed `SaveVoxelDeltaRun8` payload writes in dirty and compacted chunk writers with streamed cursor writes.
- Added `WriteSparseRleRun(snapshotPtr, ref cursor, in run)` so each serialized run advances the byte cursor immediately.
- Added a cursor-length guard before payload hashing and chunk-header backfill.

Cinematic Cheats used:
- None added. This pass is persistence hygiene: the existing signed-byte SDF visual compression remains the active cheat.

Exact Microseconds saved:
- Estimated 2-20 us saved per noisy chunk on i3/MX350 by removing per-run index addressing and post-write cursor reconciliation. More important: lower chance of silent payload/header drift before `PayloadHash64`.

Verification:
- `validate_script Assets/_Project/Scripts/VoxelDeltaProcessor.cs`: passed, 0 diagnostics after the cursor-streaming patch.

## 2026-05-12 R&D CONTINUATION PASS 3

Status: PENDING VERIFICATION

What was wrong:
- Sparse RLE load decode validated run bounds but still set dirty bits one voxel at a time. That repeated word-index math and `DirtyCellCount++` inside every loaded cell, wasting the compression proof the RLE header already gives.

What was done:
- Added run-span dirty-mask expansion in `SetDirtyRunBits`.
- Changed `TryLoadSparseRlePayload` to fill SDF/material/flags arrays per flat index while setting dirty-mask words per run.
- Assigned `DirtyCellCount` once from the decoded validated run length total.

Cinematic Cheats used:
- None added. Existing signed-byte SDF persistence remains the visual approximation; this pass removes load-side bookkeeping waste.

Exact Microseconds saved:
- Estimated 5-55 us saved on i3/MX350 when loading long-run dirty chunks. Sparse one-cell edits are effectively unchanged; long laser cuts and compacted sparse chunks benefit.

Verification:
- Unity `validate_script Assets/_Project/Scripts/VoxelDeltaProcessor.cs` basic: passed, 0 diagnostics after editor recovery.
- `git diff --check -- Assets/_Project/Scripts/VoxelDeltaProcessor.cs`: no whitespace errors.
- `dotnet build .\Hecton8\Hecton8.Core.csproj --no-restore --nologo -v:m -clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false`: failed on unrelated missing shared symbols (`HectonPersistentPathPolicy`, `InventorySoAUtility`, `HectonNativeBridge`, `HectonThreadPriorityPolicy`, etc.) and `PlayerInventory` ambiguity. No `VoxelDeltaProcessor.cs` error surfaced.

## 2026-05-12 R&D CONTINUATION PASS 4

Status: PENDING VERIFICATION

What was wrong:
- Uniform native RLE chunks still stored a 2-byte half payload while sparse native RLE stored signed-byte SDF values. The smallest chunk format was inconsistent and carried precision already discarded elsewhere in the save path.

What was done:
- Changed `NativeSnapshotUniformSdfRlePayloadBytes` to one byte.
- Changed uniform native RLE writer to persist `QuantizeSdfByte(compactedState.RleSdfValueBits)`.
- Added `NativeSnapshotLegacyUniformSdfRlePayloadBytes` and load support for both current 1-byte and legacy 2-byte uniform payloads.

Cinematic Cheats used:
- Uniform terrain scar persistence now uses the same signed-byte SDF visual approximation as sparse RLE. Deterministic base terrain and mesh rebuild carry the visible truth; sub-byte SDF precision is not worth save bytes.

Exact Microseconds saved:
- Estimated sub-1 us per uniform compacted chunk on i3/MX350. The measurable byte gain is 1 byte per uniform chunk and 50% less payload input for that uniform chunk hash/compression path.

Verification:
- Unity `validate_script Assets/_Project/Scripts/VoxelDeltaProcessor.cs` basic: passed, 0 diagnostics.
- `git diff --check -- Assets/_Project/Scripts/VoxelDeltaProcessor.cs`: no whitespace errors.
- `read_console`: only unrelated `NativeArenaArrayEditTests` missing Burst symbols and existing `SaveBinaryStorage.cs` Burst `catch` filter error surfaced.
- A later `refresh_unity` compile wait timed out after 60s; no clean full-project build is claimed.

## 2026-05-12 R&D CONTINUATION PASS 5

Status: PENDING VERIFICATION

What was wrong:
- Dirty and compacted sparse native writers repeated the RLE run-count scan even though snapshot sizing already counted runs before allocation. The write path then scanned again to emit records.

What was done:
- Removed writer-local `CountSparseDirtyRuns` and `CountCompactedSparseRuns` calls.
- Changed dirty and compacted sparse writers to stream runs once and compute `PayloadByteLength` from `cursor - payloadCursor`.
- Replaced the old void run write with `TryWriteSparseRleRun`, which keeps a per-run bounds guard before writing to the native snapshot buffer.

Cinematic Cheats used:
- None added. This is CPU/IO hygiene around the existing signed-byte terrain-scar persistence cheat.

Exact Microseconds saved:
- Estimated 20-140 us saved per snapshot on i3/MX350 depending on dirty chunk count and compacted sparse count. The byte format is unchanged.

Verification:
- Unity `validate_script Assets/_Project/Scripts/VoxelDeltaProcessor.cs` basic: passed, 0 diagnostics.
- `git diff --check -- Assets/_Project/Scripts/VoxelDeltaProcessor.cs`: no whitespace errors.
- `dotnet build .\Hecton8\Hecton8.Core.csproj --no-restore --nologo -v:q -clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false`: failed on unrelated missing shared symbols (`HectonPersistentPathPolicy`, `HectonThreadPriorityPolicy`, `SteamDeckInputPal`, `HectonNativeBridge`, etc.). No `VoxelDeltaProcessor.cs` error surfaced.
- `read_console` retry failed because Unity MCP was not ready; the earlier script validation remains the scoped evidence.
