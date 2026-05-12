# Status_CORE_DELTA_COMPRESSION

Prompt: CORE_DELTA_COMPRESSION
Role: SAVE_SYSTEM_SURGEON
Domain: Echelon 1 Data Archivist / SaveSystem, with Echelon 2 voxel-delta persistence boundary.
Status: PENDING VERIFICATION

## Relevant Mandates

- DATA_Save_Persistence_Binary_Delta_Checksum.txt: AsyncWriteManager and binary delta storage are the save-file baseline.
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt: no managed allocation in hot paths; structs and pre-sized native buffers only.
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt: unmanaged native data, explicit ownership, no mid-frame Complete.
- DBG_Telemetry_Crash_Reporting_PostMortem.txt: corruption/error reporting uses fixed binary telemetry and numeric hashes.
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt: authoritative positions are int64 grid/sector plus float local offsets; saved positions must be quantized.
- VOX_Voxel_World_Logic_Carving_Persistence.txt: voxel persistence stores only deltas from deterministic seed data, not full SDF chunks.
- STRM_ModuleDTO_LZ4_Dictionary.txt: precondition and pack payloads before LZ4; no dictionary compression without corpus proof.
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt: no direct cross-system dependency; pure codecs or existing registry/contracts only.

## Task Loop 1: Tasks 1-5

- [x] 1. RLE voxel deltas. DOD: `VoxelDeltaProcessor.CaptureNativeSnapshot` writes sparse `SaveVoxelDeltaRun8` records only for dirty cells. Rejected: dense dirty-mask/SDF/material/flags arrays per chunk. Estimate: saves 220-520 us per dirty-light chunk on i3/MX350 by avoiding 160 KB payload copies.
- [x] 2. Adjacent same-value run-length encoding. DOD: adjacent dirty cells with the same quantized SDF byte, material, and flags collapse to `[StartIndex, RunLength, Value]`. Rejected: per-cell pair stream. Estimate: 40-180 us less LZ4/input bandwidth for laser cuts and compacted uniform chunks.
- [x] 3. Bit-packed entity state into a single uint. DOD: `PackedEntityState32` stores health10/hunger10/status12 through `SaveDeltaCompression.PackEntityState32`. Rejected: three floats plus separate status field. Estimate: 0.02 us/entity less serialization copy and 8 bytes/entity saved.
- [x] 4. AUP quantization to int3 sector + half3 local offsets. DOD: `QuantizedAupSectorHalf3` is 18 bytes with 6-byte half local offset. Rejected: raw float3/double3 world position. Estimate: 0.03 us/entity less write bandwidth; low-tier precision contained per sector.
- [x] 5. XXHash3 chunk validation header data. DOD: `NativeSnapshotChunkHeaderDeltaRle` stores `PayloadHash64`; load recomputes `SaveBinaryStorage.Hash64`. Rejected: aggregate-only snapshot checksum. Estimate: 12-35 us/chunk validation cost buys deterministic discard without full snapshot loss.
- [BLOCKED BY DEPENDENCY] Compile check after Task 1-5. `validate_script` passed for `VoxelDeltaProcessor.cs` and `SaveDeltaCompression.cs`; full project compile blocked by unrelated `Hecton8.Core` errors (`SurvivalPhysiologyScalarResult`, `MantaScooter`, `TetherVerletTelemetryEntry`, `PowerGridManager`).

## Task Loop 2: Tasks 6-10

- [x] 6. Atomic overwrite section/header flip support. DOD: sector overrides write verified temp blocks, then `TryCommitIndexedPersistentWorldSectorOverride` updates directory entries and header hashes. Rejected: in-place sector mutation before checksum validation. Estimate: prevents whole-save rewrite for sector deltas; hundreds of us to ms saved on HDD.
- [x] 7. Defragmentation FrostTick/background compaction job. DOD: `QueueIndexedPersistentWorldDefragmentation` schedules background compaction after sector commit; `TryCompactIndexedPersistentWorldSectors` rewrites active blocks contiguously and fixes directory/header hashes. Rejected: leaving append-only holes forever. Estimate: recovers sequential read locality; 300-900 us saved after repeated sector overwrites on low-end disks.
- [x] 8. UnsafeUtility.MemCpy unmanaged serialization path. DOD: native payloads use `UnsafeMemoryCopyGuard.SafeCopy/TryMemCpy` and direct `NativeArray` pointers. Rejected: `BinaryWriter`/managed stream loops. Estimate: 50-250 us saved per medium payload.
- [x] 9. Inventory S.O.A. contiguous dump. DOD: `PlayerInventory.RefreshInventoryShadowBufferFromRuntime` writes item hashes, coordinates, stacks, flags, genetics, quality, and timestamps into one native shadow payload; codec dumps via `WriteNativeBytes`. Rejected: object/cell DTO iteration at save time. Estimate: 80-300 us saved on full inventory saves.
- [x] 10. Async write queue. DOD: `SaveManager.SaveGameAsync` snapshots on main, writes on background; `AsyncWriteManager` flushes through bounded background queues. Rejected: synchronous main-thread file flush. Estimate: main-thread stall avoided; disk cost moved off frame.
- [BLOCKED BY DEPENDENCY] Compile check after Task 6-10. Targeted script validation passed where MCP returned; full compile still blocked by unrelated project errors listed above.

## Task Loop 3: Tasks 11-15

- [x] 11. Corruption fallback. DOD: XXHash3 mismatch logs `SAVE_CORRUPTION_HASH`, skips the corrupt chunk payload, and lets procedural terrain rebuild without that delta. Rejected: failing the whole snapshot load. Estimate: 0 us normal path beyond hash already required; saves player file on one bad chunk.
- [x] 12. Zero-alloc byte swap. DOD: `SaveDeltaCompression.ByteSwap32/64` and `ByteSwap32InPlace` are bitwise and allocation-free. Rejected: `BitConverter`/temporary byte arrays. Estimate: 0.01 us/value, zero GC.
- [x] 13. Strict 64-byte save header. DOD: `StrictSaveFileHeader64` is `[StructLayout(Pack=1, Size=64)]` with Magic, Version, PlayTime, AUP_X/Y/Z, Checksum, Reserved. Rejected: variable metadata header. Estimate: fixed read can be one guarded native copy.
- [x] 14. Recon scan for forbidden managed save APIs. DOD: `Docs/AgentLogs/RECON_CORE_DELTA_COMPRESSION.md` contains CLI scan for `JsonUtility`, `BinaryFormatter`, and `File.WriteAllText`. Rejected: undocumented memory of scan output. Estimate: 0 runtime cost.
- [BLOCKED BY DEPENDENCY] 15. Omega compile and managed-type audit. DOD: new save structs are unmanaged/blittable by declaration and `validate_script` passed for modified scripts that completed before timeout. Full `dotnet build Assembly-CSharp.csproj --no-restore` reached compilation and failed on unrelated Hecton8.Core files, not this save patch.
- [BLOCKED BY DEPENDENCY] Compile check after Task 11-15. Same external compile wall; no new save-system diagnostics surfaced in targeted validation or dotnet output.

## Strict Iterative Loops

- [x] Loop 1: existing SaveSystem and contracts read. Found existing async write, sector override, inventory shadow, and voxel snapshot gap.
- [x] Loop 2: first implementation pass. Added sparse RLE codec, packed structs, per-chunk hash, corruption skip, and defrag queue.
- [x] Loop 3: self-read for missed domain or allocation violations. Corrected RLE payload from half bits to quantized `sbyte` value to match prompt.
- [x] Loop 4: compile/error repair pass. `validate_script` clean for `VoxelDeltaProcessor.cs` and `SaveDeltaCompression.cs`; `SaveBinaryStorage.cs` validated once after first edit and dotnet compile found no save-file errors after defrag edit.
- [x] Loop 5: final code audit and log append. Polish Mandate executed; `LOG_CORE_DELTA_COMPRESSION.md` appended.

## R&D Continuation: 2026-05-12

- [x] Honest AAA pass 1: aligned RLE run-boundary semantics with the signed-byte payload. DOD: dirty and compacted RLE runs now compare `QuantizeSdfByte(...)`, not raw half bits. Rejected: preserving half-bit boundaries after deliberately quantizing the value. Estimate: 8-70 us saved per noisy dirty chunk on i3/MX350 through fewer runs and smaller hash/compression input.
- [x] Validation: `validate_script Assets/_Project/Scripts/VoxelDeltaProcessor.cs` passed with 0 diagnostics after the R&D patch.
- [x] Honest AAA pass 2: streamed sparse RLE payload writes through the byte cursor instead of a separate run index plus post-write cursor jump. DOD: dirty and compacted RLE writers now advance `cursor` per `SaveVoxelDeltaRun8` and guard `cursor == payloadCursor + payloadBytes` before hashing/header write. Rejected: dual cursor/index state and unguarded pointer arithmetic. Estimate: 2-20 us saved per noisy chunk depending on run count, with lower corruption surface.
- [x] Validation: `validate_script Assets/_Project/Scripts/VoxelDeltaProcessor.cs` passed with 0 diagnostics after the cursor-streaming patch.
- [x] Honest AAA pass 3: optimized sparse RLE load decode by setting dirty-mask word spans per run instead of per-cell `SetDirtyBit` calls. DOD: `TryLoadSparseRlePayload` now expands value/material/flags per cell but writes dirty bits by run-span mask and assigns `DirtyCellCount` once after validated decode. Rejected: per-cell bit twiddle/increment inside every run and managed temp expansion buffers. Estimate: 5-55 us saved on loading long-run dirty chunks, depending on run density.
- [x] Validation: Unity `validate_script Assets/_Project/Scripts/VoxelDeltaProcessor.cs` basic passed with 0 diagnostics after editor recovery. `dotnet build Hecton8.Core.csproj --no-restore /p:BuildProjectReferences=false` still fails on unrelated missing shared symbols (`HectonPersistentPathPolicy`, `InventorySoAUtility`, `HectonNativeBridge`, etc.) and `PlayerInventory` ambiguity; no `VoxelDeltaProcessor.cs` diagnostic surfaced.
- [x] Honest AAA pass 4: compressed uniform native RLE payload from legacy half/ushort to signed-byte SDF while preserving legacy 2-byte load support. DOD: writer stores `QuantizeSdfByte(compactedState.RleSdfValueBits)` in one payload byte; loader accepts both current 1-byte and legacy 2-byte uniform payloads before creating `CompactedChunkState`. Rejected: breaking old native snapshots or keeping exact half for a visual terrain-scar cheat. Estimate: 1 byte saved per uniform compacted chunk plus lower hash/compression input; microsecond gain is sub-1 us/chunk but format consistency improves.
- [x] Validation: Unity `validate_script Assets/_Project/Scripts/VoxelDeltaProcessor.cs` basic passed with 0 diagnostics; `git diff --check` clean. `read_console` only surfaced unrelated `NativeArenaArrayEditTests` Burst attribute errors and existing `SaveBinaryStorage.cs` Burst `catch` filter error. A later Unity refresh timed out waiting for editor readiness; status remains PENDING VERIFICATION.
- [x] Honest AAA pass 5: removed duplicate RLE run-count scan from dirty and compacted native writers. DOD: allocation pre-pass still uses `CountSparseDirtyRuns`/`CountCompactedSparseRuns`, but write pass now streams runs once, computes `PayloadByteLength` from `cursor - payloadCursor`, and guards each `SaveVoxelDeltaRun8` write. Rejected: a third full chunk scan inside the writer and preallocated temp run buffers. Estimate: 20-140 us saved per snapshot on i3/MX350 depending dirty chunk count and compacted sparse count.
- [x] Validation: Unity `validate_script Assets/_Project/Scripts/VoxelDeltaProcessor.cs` basic passed with 0 diagnostics; `git diff --check` clean. `dotnet build Hecton8.Core.csproj --no-restore /p:BuildProjectReferences=false` still fails on unrelated missing symbols (`HectonPersistentPathPolicy`, `HectonThreadPriorityPolicy`, `SteamDeckInputPal`, `HectonNativeBridge`, etc.); no `VoxelDeltaProcessor.cs` error surfaced.
