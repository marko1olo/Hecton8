# Rationale_CORE_DELTA_COMPRESSION

Status: PENDING VERIFICATION

## Decision 0: Batch Memory Bootstrap

Problem: CORE_DELTA_COMPRESSION had no status or rationale file, so context compression would erase task state.
Solution: Created file-backed state before code edits. DOD practice: explicit disk memory and prompt extraction from `Docs/Tasks/CURRENT_BATCH.md`.
Rejected Alternatives: Chat-only status was rejected because the batch protocol treats disk logs as authority. Reusing another agent log was rejected because it would contaminate this task boundary.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; this is process containment.
Hardware Impact: 0 microseconds runtime impact on i3/MX350.

## Decision 1: Sparse RLE Voxel Delta Snapshot

Problem: Native voxel snapshots still paid dense per-chunk payload cost even when only a few cells were modified.
Solution: Replaced new snapshot writes with `NativeSnapshotDeltaRleMagic` chunks containing `SaveVoxelDeltaRun8` records: `StartIndex ushort`, `RunLength ushort`, quantized `SdfValue sbyte`, material, and flags. DOD practice: delta-only storage from deterministic base terrain, with adjacent same-value runs.
Rejected Alternatives: Keeping full dirty-mask/SDF/material/flags arrays was too large for HDD stalls. Saving only `[Index, Value]` without material/flags was rejected because material replacement and carve mode are required to reconstruct gameplay-visible terrain. Per-cell pairs without runs were rejected because long laser cuts and compacted chunks become avoidable repetition.
Scalability potential: Low uses sbyte density and few bytes per modified run; Middle/High keep enough saved frame time to spend on visible mesh rebuild/VFX; Ultra can keep more destructive edits without save-file growth becoming the bottleneck.
Hardware Impact: Estimated 220-520 us saved per dirty-light chunk on i3/MX350 by avoiding dense 160 KB chunk snapshots; uniform compacted chunks collapse to one 2-byte payload plus header.

## Decision 2: Per-Chunk XXHash3 With Skip-On-Fail

Problem: A single corrupted voxel delta payload could invalidate the entire save load path or crash during native copy/decode.
Solution: Added `PayloadHash64` to the delta RLE chunk header and verified with `SaveBinaryStorage.Hash64` before decode. On mismatch or malformed RLE, the loader publishes `SAVE_CORRUPTION_HASH`, skips that chunk, and lets deterministic procedural terrain regenerate base data.
Rejected Alternatives: Whole-file checksum only was rejected because it gives no recoverable chunk boundary. Throwing on hash failure was rejected because the prompt requires discard/regenerate, not crash. Managed exception logging was rejected for hot-path discipline and telemetry consistency.
Scalability potential: Low devices skip bad chunks cheaply and keep loading; Middle/High/Ultra can validate more chunks without sacrificing the whole save.
Hardware Impact: Hash cost estimated 12-35 us per chunk payload; failure path saves the entire save load from aborting.

## Decision 3: Packed Save Primitives

Problem: Entity state, AUP positions, byte swapping, and strict header structure had no small reusable unmanaged contract for save code.
Solution: Added `SaveDeltaCompression` with unmanaged structs: `PackedEntityState32`, `QuantizedAupSectorHalf3`, `StrictSaveFileHeader64`, and `SaveChunkHeader32`, plus zero-alloc byte swaps.
Rejected Alternatives: Raw floats/doubles and ad hoc byte arrays were rejected because they waste bandwidth and invite managed allocations. A general serializer was rejected because this domain needs fixed binary layout.
Scalability potential: Low gets smallest stable payloads; Middle/High/Ultra get deterministic binary contracts for more entity/state volume.
Hardware Impact: Entity state saves 8 bytes/entity versus two floats plus status; AUP local offsets save 6 bytes per position versus float3 locals and far more versus double3.

## Decision 4: Atomic Append Plus Background Defragmentation

Problem: Indexed sector overrides append verified replacement blocks and flip directory/header pointers, leaving inactive sector blocks behind.
Solution: Added `TryCompactIndexedPersistentWorldSectors` and `QueueIndexedPersistentWorldDefragmentation`. After a sector commit, a single-flight background worker rewrites header, directory, metadata, and active sector blocks contiguously, then recomputes header/payload hashes.
Rejected Alternatives: In-place mutation was rejected because a failed write can poison the active sector. Leaving append-only holes forever was rejected because repeated overwrites degrade read locality and file size.
Scalability potential: Low storage gets smaller sequential files; Middle/High/Ultra retain atomic overwrite safety while avoiding unbounded sector growth.
Hardware Impact: Estimated 300-900 us saved on later loads after repeated sector overwrites on low-end HDD/slow SSD; background compaction prevents a foreground frame stall.

## Decision 5: Verification Wall

Problem: Full project compile cannot be cleanly attributed to this task because unrelated project errors exist outside the save domain.
Solution: Used Unity `validate_script` for modified scripts and `dotnet build Assembly-CSharp.csproj --no-restore` for broad compile evidence. The build reached C# compilation and failed in unrelated Hecton8.Core files: `SurvivalPhysiologyScalarResult`, `MantaScooter`, `TetherVerletTelemetryEntry`, and `PowerGridManager`.
Rejected Alternatives: Editing unrelated gameplay/physics/power files was rejected as domain sabotage. Reporting a clean build was rejected because the compiler data is objective.
Scalability potential: Runtime unaffected; verification remains blocked until owning agents fix their compile errors.
Hardware Impact: 0 microseconds runtime impact.

## OMEGA POLISH CHANGES

Problem: First sparse RLE pass stored half SDF bits in each run, which was accurate but ignored the prompt's `[Index ushort, Value sbyte]` compression target.
Solution: Converted sparse RLE run values to quantized signed bytes using precomputed scale constants. Decode expands the byte back to half bits for the existing voxel delta buffers. Cinematic cheat: density persistence accepts quantized visual SDF deltas because deterministic terrain and mesh rebuild hide sub-byte precision loss.
Rejected Alternatives: Keeping half SDF was rejected as save bloat. A lookup table was unnecessary because the conversion is two multiplies against constants on cold save/load paths. Full float restoration was rejected because this is terrain scar persistence, not scientific simulation.
Scalability potential: Low keeps payloads tiny on i3/MX350; Middle/High/Ultra can store more deformation history and spend saved IO on stronger cut VFX.
Hardware Impact: Saves 1 byte per run versus half-only value and keeps run struct aligned at 8 bytes with material/flags.

Problem: Background defrag used a lambda capture in the ThreadPool queue path.
Solution: Replaced the lambda with a cached `WaitCallback` and a single-flight static path slot. Cinematic cheat: defrag is a cold background maintenance pass, so no per-frame scheduling object is needed.
Rejected Alternatives: Keeping the lambda was rejected by the Zero-GC purge. Running defrag synchronously was rejected because it would turn saved IO into a foreground stall.
Scalability potential: Low avoids managed queue allocation during sector maintenance; Middle/High/Ultra keep atomic append safety with cheaper cleanup dispatch.
Hardware Impact: Removes one managed closure allocation per sector defrag request; runtime frame impact remains 0 us because this is background IO.

Problem: Polish scan found no new `foreach`, `math.sqrt`, or `math.normalize` in the save patch, but existing unrelated `.ToString()` calls remain in `SaveBinaryStorage`.
Solution: Left unrelated legacy formatting untouched because it is outside the new implementation and not part of the hot RLE path. The edited code uses `for` loops, bitmasks, precomputed reciprocals/constants, and unmanaged structs.
Rejected Alternatives: Broad cleanup of legacy formatting was rejected as refactoring-loop scope creep.
Scalability potential: Low/Middle/High/Ultra unaffected except the new path remains allocation-free.
Hardware Impact: 0 us beyond the RLE and defrag savings already listed.

## Decision 6: Honest R&D RLE Value Semantics

Problem: The first sbyte RLE implementation wrote quantized `SdfValue` but still decided run boundaries using raw half SDF bits. That was honest but under-compressed: two cells that decode to the same saved byte could be split into separate runs.
Solution: Changed dirty and compacted run counting/writing to compare the same quantized sbyte value that is written to disk. DOD practice: compression semantics now match payload semantics.
Rejected Alternatives: Keeping half-bit comparisons was rejected because it preserves precision already discarded by the payload. Widening the payload back to half was rejected as save bloat and a rollback of the R&D target.
Scalability potential: Low gets fewer runs and smaller writes on noisy laser cuts; Middle/High/Ultra can persist denser destruction histories with the same IO envelope.
Hardware Impact: Estimated extra 8-70 us saved per noisy dirty chunk on i3/MX350 depending on how many near-equal half values collapse into one signed-byte run.

## Decision 7: Streamed RLE Payload Cursor

Problem: The sparse RLE writer counted payload bytes, then wrote each run with a separate `runIndex` against `payloadCursor`, and finally jumped `cursor` by the whole payload length. Correct output, but two independent position states make corruption audits harder and add avoidable index math per run.
Solution: Replaced indexed payload writes with `WriteSparseRleRun(snapshotPtr, ref cursor, in run)`. The writer advances the byte cursor per run and checks `cursor == payloadCursor + payloadBytes` before the chunk hash and header are written. DOD practice: one authoritative cursor for serialized byte position, plus fail-fast guard.
Rejected Alternatives: Leaving indexed writes was rejected because it hides payload-position drift until load. Writing to a managed temporary run buffer was rejected because it violates zero-GC save paths. Raw pointer increments without the final length guard were rejected because this is a persistence boundary, not a visual-only buffer.
Scalability potential: Low avoids extra arithmetic on high-run dirty chunks; Middle/High/Ultra keep the same compact format while making larger destructive edits easier to audit and validate.
Hardware Impact: Estimated 2-20 us saved per noisy chunk on i3/MX350 depending on run count. The larger gain is reduced persistence corruption surface before `PayloadHash64`.

## Decision 8: RLE Load Dirty-Mask Span Expansion

Problem: Sparse RLE load validation already proves sorted, non-overlapping runs, but decode still called `SetDirtyBit` and incremented `DirtyCellCount` once per cell. Long runs paid repeated word-index math even though the mask span is known at run granularity.
Solution: Added `SetDirtyRunBits` and changed `TryLoadSparseRlePayload` to mark dirty mask words per run, fill SDF/material/flags arrays in the same flat index window, and assign `DirtyCellCount` once from the decoded count. DOD practice: use validation proof to remove redundant per-cell bookkeeping.
Rejected Alternatives: Keeping per-cell `SetDirtyBit` was rejected because it wastes work on the exact long runs RLE is meant to create. A managed temporary dirty-mask buffer was rejected because load stays native/persistent and zero-GC. Skipping dirty mask writes entirely was rejected because downstream systems read the mask for rebuild and save decisions.
Scalability potential: Low loads long laser cuts and compacted sparse chunks with less scalar bit work; Middle/High/Ultra can tolerate denser destruction histories without turning load into bit-twiddle noise.
Hardware Impact: Estimated 5-55 us saved on i3/MX350 when loading long-run dirty chunks. Small sparse edits see negligible change; the patch mainly improves high-run-length saves.

## Decision 9: Uniform RLE Signed-Byte Native Payload

Problem: Sparse RLE chunks already save SDF as signed byte, but the special uniform native RLE chunk still wrote a 2-byte half/ushort payload. That kept an inconsistent precision rule and made the smallest chunk format larger than necessary.
Solution: Changed uniform native RLE writer to store `QuantizeSdfByte(RleSdfValueBits)` as one byte and changed the loader to accept both the new 1-byte payload and the legacy 2-byte half payload. DOD practice: format shrink without forced incompatibility for existing native snapshots.
Rejected Alternatives: Keeping exact half was rejected because uniform compacted terrain scar persistence is the same visual approximation domain as sparse signed-byte RLE. Rejecting legacy 2-byte payloads was rejected because it turns an R&D compression pass into a hidden save-format regression. Adding a new magic number was rejected because the header's payload length already disambiguates current and legacy uniform payloads.
Scalability potential: Low keeps uniform compacted chunks at the smallest legal payload; Middle/High/Ultra keep format consistency so more compacted sectors can be retained under the same IO envelope.
Hardware Impact: Saves 1 byte per uniform compacted chunk and reduces hash/compression input by 50% for that payload. Per-chunk CPU gain is sub-1 us on i3/MX350; the real value is format consistency and no compatibility break.

## Decision 10: Writer-Side Run Count Scan Removal

Problem: Native snapshot capture already performs a sizing pre-pass that counts sparse RLE runs to allocate an exact `NativeArray<byte>`. The writer then counted the same runs again before writing, then scanned a third time to emit records. That was correct but wasteful on dirty-heavy saves.
Solution: Removed the writer-local `CountSparseDirtyRuns` and `CountCompactedSparseRuns` calls. Writers now reserve only the header, stream each `SaveVoxelDeltaRun8` through a bounded `TryWriteSparseRleRun`, and compute `PayloadByteLength` from `cursor - payloadCursor` before hashing and backfilling the chunk header.
Rejected Alternatives: Keeping the duplicate writer count was rejected because the allocation pre-pass already proves the upper bound. A managed temporary run list was rejected because this path is native save serialization. Removing bounds checks entirely was rejected because persistence code needs fail-fast protection if a future run-boundary edit diverges from sizing logic.
Scalability potential: Low avoids redundant full-chunk scans on MX350/i3 when multiple chunks are dirty; Middle/High/Ultra can retain larger destructive histories with lower save snapshot CPU cost before LZ4.
Hardware Impact: Estimated 20-140 us saved per snapshot on i3/MX350 depending on dirty chunk count, compacted sparse chunk count, and run density. Zero byte-format change.
