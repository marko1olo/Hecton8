# Rationale_CORE_SAVE_MMF

Status: PENDING VERIFICATION - LOCAL BUILD CLEAN

## Initialization

Problem: `Docs/Tasks/CURRENT_BATCH.md` requested by the user does not exist on disk.
Solution: Used CLI extraction against `Docs/Tasks/CURRENT_BATCH.txt`, which contains `<AGENT_PROMPT id="CORE_SAVE_MMF">`.
Rejected Alternatives: Do not invent a batch file; do not rely on IDE open tabs.
Scalability potential: Low/Middle/High/Ultra unaffected; this is workflow hygiene only.
Hardware Impact: 0 us runtime impact on i3/MX350.

Problem: Batch source changed during parallel-agent work; earlier `CURRENT_BATCH.md` was absent, later it existed with the same `<AGENT_PROMPT id="CORE_SAVE_MMF">` block.
Solution: Re-extracted the prompt from `Docs/Tasks/CURRENT_BATCH.md` by CLI and corrected the status source path.
Rejected Alternatives: Keep stale `.txt` source evidence after the canonical `.md` appeared.
Scalability potential: Low/Middle/High/Ultra unaffected; reduces audit drift only.
Hardware Impact: 0 us runtime impact on i3/MX350.

## Loop 1 - Tasks 1-6

Problem: Legacy/fallback payload reads still opened a read-only MMF over the entire save, defeating the four-window paging rule for non-indexed files.
Solution: Changed fallback payload read to copy the header and compressed payload through `AsyncWriteManager.TryCopyFromCachedReadWindow` / `TryCopyFileRangeToNativeArray`, using the existing 4 x 1MB window pool and native compressed scratch buffer.
Rejected Alternatives: Keep whole-file mapping for "rare" legacy files; this still causes HDD page faults on weak disks.
Scalability potential: Low uses 1MB demand windows; Middle/High keeps prefetch hot; Ultra can spend saved IO latency on denser visual-sector paging.
Hardware Impact: Estimated 42000 us worst-case HDD page-fault burst avoided on i3/MX350 when a 200MB container is touched.

Problem: Prefetch only helped paths already using cached windows.
Solution: Routed fallback compressed payload reads through the cached-window copy helper so the 256KB edge prefetch worker now applies to that path too.
Rejected Alternatives: Add a second prefetch system; duplicate queues would race the same MMF cache.
Scalability potential: Low avoids boundary stalls; Middle/High/Ultra can increase visible save-backed debris/item density without foreground IO stalls.
Hardware Impact: Estimated 3000 us boundary stall avoided per window crossing on weak HDD.

Problem: Load hydration budget was too wide for the prompt requirement.
Solution: Set `HydrationScheduler.FrameBudgetMilliseconds` to exactly 2.0 and `FrameBudgetTicks` to `Stopwatch.Frequency / 500`; existing load apply loop already awaits `Awaitable.NextFrameAsync()`.
Rejected Alternatives: Frame-count chunking without a stopwatch budget; it would not enforce a 2.0ms ceiling.
Scalability potential: Low keeps frame pacing stable; Middle/High/Ultra can restore more visible content over extra frames without hitches.
Hardware Impact: Returns about 2000 us main-thread time per hydration frame versus the prior 4.0ms slice.

Problem: AUP coordinate compression needed millimeter short3 evidence.
Solution: Retained existing `QuantizedAupLocalOffsetShort3` (6 bytes) and chunk-center millimeter pack/unpack helpers; no serialized float3 path was added.
Rejected Alternatives: Float3 local offsets; cheap to code, twice the footprint and less cache-friendly.
Scalability potential: Low reduces IO and cache pressure; Ultra can spend saved bandwidth on more saved entities/VFX residues.
Hardware Impact: Estimated 6000 us IO/decode saved per 1000 entity positions on i3/MX350-class storage.

Problem: Uniform voxel RLE job wiring drifted: caller provided a byte flag but the job expected an int run header, causing compile failure and risking loss of the 2-byte uniform chunk path.
Solution: Restored byte `UniformFlag` wiring and added the missing `Hecton8.Core.Signals` namespace for the debris signal compile dependency.
Rejected Alternatives: Convert the uniform flag allocation to `NativeArray<int>`; that expands a one-byte signal and mismatches `CompactedChunkState`.
Scalability potential: Low keeps empty/solid chunks to tiny payloads; High/Ultra can keep richer voxel edits elsewhere because uniform regions cost almost nothing.
Hardware Impact: Estimated 65000 us IO/decode avoided when a full 32^3 uniform chunk skips the expanded payload.

Problem: First build after MMF changes failed on `VoxelDeltaProcessor` compile errors.
Solution: Fixed the stale RLE uniform flag field and missing signal namespace, then reran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false`; build succeeded with 0 warnings and 0 errors.
Rejected Alternatives: Mark compile blocked after one attempt; the failures were local and mechanically fixable.
Scalability potential: Low/Middle/High/Ultra unaffected directly; compile integrity restored for subsequent save work.
Hardware Impact: 0 us runtime impact beyond the task fixes above.

Problem: Maintenance save booleans could regress into byte-per-bool storage.
Solution: Verified `SaveSlotMaintenanceRecord` uses bit constants and `PackStateFlags()` / `ApplyStateFlags(byte)` for a single-byte state field.
Rejected Alternatives: Separate serialized bools; they waste bytes and branch through more fields during diagnostics.
Scalability potential: Low/Middle/High/Ultra unaffected visually; keeps slot metadata cheap enough for boot scans.
Hardware Impact: Estimated 4 us saved per maintenance record IO on i3/MX350-class storage.

Problem: Persistent entity save records must not serialize managed item strings.
Solution: Verified sector/delta serialization uses `ItemPersistentIdHash`, compact hash indexes, and FNV-style persistent ID hashing; legacy inventory string IDs are converted via `LocHash.Compute` during migration/read.
Rejected Alternatives: Store `FixedString128Bytes` or managed item IDs in sector deltas; readable, but wrong for IO and cache.
Scalability potential: Low keeps dropped-item persistence cheap; High/Ultra can retain more environmental debris because entity identity is numeric.
Hardware Impact: Estimated 18000 us and 120KB saved per 1000 persistent item records versus string IDs.

Problem: One corrupt persistent-world sector must not poison the entire save load.
Solution: Verified indexed load validates aggregate checksum root but quarantines individual failed sector reads/checksums, attempts sector backup, clears the local error, and continues loading the remaining sectors.
Rejected Alternatives: Abort on first bad sector; maximally safe but destroys survivability of large streamed worlds.
Scalability potential: Low can regenerate missing sectors procedurally; High/Ultra can preserve unaffected visual sectors while replacing only the damaged page.
Hardware Impact: Estimated 240000 us avoided reload/regeneration when one sector fails.

Problem: Cloud comparison on boot must not load the whole save container.
Solution: Verified `TryReadCloudMetadataHeader128` reads at most 128 bytes through the cached MMF window and extracts timestamp/checksum fields.
Rejected Alternatives: Call full metadata/payload reader for cloud sync; it decompresses unnecessary data.
Scalability potential: Low gets fast boot cloud checks; High/Ultra keep room for richer local metadata without full payload IO.
Hardware Impact: Estimated 50000 us boot IO saved on weak HDD.

## Loop 2 - Tasks 11-15

Problem: Critical save data must not sit behind large visual-sector payloads.
Solution: Verified indexed writer places header + directory + critical metadata/player/quest block before persistent-world sector pages, with visual dropped-item sectors written after the metadata block.
Rejected Alternatives: One monolithic compressed payload; simple but forces player/quest restore to wait on EOF visual data.
Scalability potential: Low reads critical front matter only; High/Ultra can append more visual sector pages without delaying boot-critical state.
Hardware Impact: Estimated 48000 us seek/decompress avoided on weak HDD during boot/load.

Problem: Dirty sector writes can corrupt the main save if patched directly.
Solution: Verified sector overrides are written as `.sectmp`, include checksum/header data, are decompressed/verified before commit, then patch directory/header and queue throttled flush.
Rejected Alternatives: Direct dirty-sector patch into `slot_0.sav`; faster in the happy path but unrecoverable on interruption.
Scalability potential: Low can survive interrupted writes; High/Ultra can tolerate many visual-sector overrides without endangering core metadata.
Hardware Impact: Estimated 35000 us recovery and replay work avoided per interrupted sector write.

Problem: Load candidate fallback must not allocate managed lists during repair/audit.
Solution: Verified `SaveManager` uses persistent instance/static `NativeArray<SaveLoadCandidate>` with capacity 9 and locked static scratch for repair/audit paths.
Rejected Alternatives: `List<SaveLoadCandidate>`; easier to append but violates zero-GC hot-path policy.
Scalability potential: Low keeps save repair deterministic; High/Ultra unaffected except fewer incidental allocations.
Hardware Impact: Estimated 35 us GC/allocation overhead avoided per candidate pass.

Problem: AUP coordinate serialization needs a raw 48-byte blit, not field-by-field writer churn.
Solution: Verified `AbsoluteUniversePositionBlit` is explicit 48 bytes and entity data paths store AUP structs through native records/raw pointer copies.
Rejected Alternatives: Serialize grid/local components one field at a time; readable but slower and more error-prone for alignment.
Scalability potential: Low keeps entity state compact; Ultra can store more persistent actors before hitting IO budget.
Hardware Impact: Estimated 9000 us saved per 1000 AUP records.

Problem: Sector commit path should not rehash the metadata block when cached low32 metadata state already validates.
Solution: Added `TryRecoverCachedIndexedMetadataHashLow32`, deriving metadata hash from cached header payload hash and directory hash, then validating low32 through the checksum root before patching the sector.
Rejected Alternatives: Decompress and hash metadata on every sector override commit; redundant because the commit only mutates directory/sector data.
Scalability potential: Low makes sector commits cheaper on weak disks; High/Ultra can commit more visual-sector patches per minute without touching critical metadata.
Hardware Impact: Estimated 22000 us saved per sector commit on i3/MX350-class storage.

Problem: Compile verification after tasks 11-15 is blocked by another domain.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false`; build now fails in `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` due 12 missing world/wreckage methods (`TryUnregisterWreckSlowTick`, `ProcessNearFieldDebris`, `PrepareWreckWorldState`, etc.).
Rejected Alternatives: Edit `ProceduralWreckGenerator.cs`; that is outside Data Archivist/MMF domain and would violate the domain boundary without a save-code justification.
Scalability potential: Low/Middle/High/Ultra blocked only by external compile dependency; Data Archivist files emitted no reported compiler errors in this pass.
Hardware Impact: 0 us runtime impact from the dependency itself.

## Loop 3 - Tasks 16-20

Problem: Save thread affinity can drift if Unity `Time.frameCount` is read after switching to a background worker.
Solution: Verified the save pipeline captures `SaveContextFrameData.CaptureMainThread()` before `Awaitable.BackgroundThreadAsync()` and passes it as a readonly struct into compression timing.
Rejected Alternatives: Read Unity frame data in background code; undefined thread affinity and nondeterministic logs.
Scalability potential: Low/Middle/High/Ultra unaffected visually; diagnostics stay deterministic under save pressure.
Hardware Impact: Estimated 15 us debug/race overhead avoided per save.

Problem: Binary bool reads still used a direct branch assignment.
Solution: Changed `BufferReader.ReadBool` to assign through `math.select(false, true, byteValue != 0)`.
Rejected Alternatives: Branch at every bool decode; acceptable alone but noisy across save DTOs.
Scalability potential: Low keeps DTO hydration cheaper; High/Ultra negligible but consistent with branchless decode mandate.
Hardware Impact: Estimated 3 us saved per 1000 bool reads on i3/MX350-class CPU.

Problem: Binary struct alignment mandate conflicts with existing serialized v9 layout.
Solution: Verified active hot structs (`EntityDataRecord` 64, `PersistentWorldDeltaRecord` 32, compact deltas 16, AUP blits 48, protected block headers 64, cloud header 128) are aligned; marked legacy v9 header/directory structs as format-compatibility blocked instead of mutating offsets silently.
Rejected Alternatives: Change `SaveFileHeader`/`SectorEntry` sizes in-place; that breaks existing save file offsets and old v9 readers.
Scalability potential: Low keeps backward load safe; High/Ultra need a formal v10 migration if full header/directory padding is required.
Hardware Impact: 0 us changed until version migration owner approves a format bump.

Problem: Reused native memory windows can leak stale bytes into decode/write paths.
Solution: Verified and expanded `UnsafeUtility.MemClear` use on raw payload buffers, compressed staging copies, 128-byte header stack reads, sector raw reuse, and override trailing slack.
Rejected Alternatives: Trust uninitialized `NativeArray` memory; faster only until stale bytes corrupt checksum/debug paths.
Scalability potential: Low avoids rare corruption; High/Ultra can reuse larger native pools safely.
Hardware Impact: Estimated 5000 us post-corruption diagnosis avoided; memclear cost remains bounded to active byte ranges.

Problem: Missing/invalid save magic should abort on the first 8 bytes with a numeric error state, not later with string-only failures.
Solution: Added `ErrorCodeHeaderReadFailed`, `ErrorCodeMagicMismatch`, `LastReadErrorCode`, and `TryReadHeaderPrefixFastFail`; binary-container/header/payload paths now use the 8-byte prefix gate before larger mapping/decompression.
Rejected Alternatives: Preserve string-only errors and discover bad files after mapping/decompression.
Scalability potential: Low gets instant invalid-save rejection; High/Ultra unaffected except less wasted IO on corrupt/cloud-stale artifacts.
Hardware Impact: Estimated 40000 us avoided on invalid large save files.

Problem: Compile verification after tasks 16-20 is still blocked externally.
Solution: Reran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false`; current failures moved to `GlobalSignals.cs` missing signal types, `FaunaBrain.cs` missing `FaunaTier1LodProxyEntry`, and `ConstructionManager.cs` missing `IOriginShiftListener.OnOriginShift`.
Rejected Alternatives: Edit core signal/fauna/construction files from the Data Archivist prompt; no direct save/MMF justification and outside domain boundary.
Scalability potential: Low/Middle/High/Ultra blocked by external compile dependency only; Save/MMF files produced no reported compiler errors.
Hardware Impact: 0 us runtime impact from external dependency.

Problem: Agent identity naming conflict between role (`DATA_ARCHIVIST`) and prompt id (`CORE_SAVE_MMF`).
Solution: Use `CORE_SAVE_MMF` for required task/rationale/log filenames because the extracted prompt explicitly mandates those paths.
Rejected Alternatives: `Status_DATA_ARCHIVIST.md` would not match the prompt completion evidence contract.
Scalability potential: Low/Middle/High/Ultra unaffected; this prevents reporting drift.
Hardware Impact: 0 us runtime impact on i3/MX350.

## Loop 4 - Static Self Review

Problem: The post-task diff had to be audited for stale signatures after `SaveBinaryStorage` load APIs gained a compressed scratch buffer.
Solution: Re-scanned all touched call sites for `TryReadMetadata`, `TryLoadSaveData`, `TryReadPayload`, `TryCopyFileRangeToNativeArray`, `TryRecoverCachedIndexedMetadataHashLow32`, `TryReadHeaderPrefixFastFail`, `ReadBool`, and `SaveContextFrameData`; no stale Data Archivist call site remained, and the final clean build confirmed the signatures compile.
Rejected Alternatives: Trust the compiler only without static call-site review; earlier external-domain failures had masked this until the final pass.
Scalability potential: Low/Middle/High/Ultra all retain the same scratch-buffer contract; no hidden managed fallback path remains in the save load surface.
Hardware Impact: Estimated 0 us direct runtime gain; prevents invalid fallback behavior on weak disks.

Problem: The Omega zero-GC scan found added `new` and string interpolation hits in the touched diff.
Solution: Classified the hits. Cold setup allocations are background worker/file/native buffer initialization; error interpolations live on failure paths returning `out string error`. The added hot binary bool read uses `math.select`, and the added MMF copy path uses native buffers and `UnsafeUtility.MemClear`.
Rejected Alternatives: Replace cold diagnostic strings with a char-buffer pool now; that would expand scope without removing frame-time pressure, while the numeric `LastReadErrorCode` already covers hot invalid-save gating.
Scalability potential: Low avoids runtime GC in save paging/hydration paths; High/Ultra keep diagnostic strings on cold failure surfaces only.
Hardware Impact: Estimated 0 us change beyond existing task savings; no additional per-frame allocation was introduced by the Data Archivist hot path.

Problem: Alignment audit still found legacy serialized structures that are not 16/32/64 bytes.
Solution: Kept the compatibility block on `SaveFileHeader` size 56 and `SectorEntry` size 28; active hot/current structs remain aligned, while old v9 on-disk layout requires a formal version migration before padding.
Rejected Alternatives: Silently pad legacy structs; that corrupts existing save offsets and would be worse than the alignment defect.
Scalability potential: Low keeps old saves loadable; Ultra needs a v10 format owner to buy full header/directory alignment cleanly.
Hardware Impact: 0 us changed; future v10 migration can improve directory cache behavior.

Problem: Silo audit showed `VoxelDeltaProcessor.cs` in the touched file set.
Solution: Kept the justification narrow: task 6 explicitly covers voxel RLE save payloads, and the edit restored the RLE uniform byte flag plus a missing compile namespace. No world/fauna/construction code was edited.
Rejected Alternatives: Patch unrelated `ProceduralWreckGenerator`, `GlobalSignals`, `FaunaBrain`, or `ConstructionManager` errors from this agent; those belong to other domains.
Scalability potential: Low gets the 2-byte uniform chunk path; High/Ultra can spend saved voxel IO on richer non-uniform edits.
Hardware Impact: Estimated 65000 us IO/decode avoided on uniform chunks remains valid.

Problem: Build health changed after parallel agents cleared the external-domain compile blockers.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false`; final result succeeded with 0 warnings and 0 errors in 00:01:21.37.
Rejected Alternatives: Keep the stale blocked status after new clean-build evidence.
Scalability potential: Low/Middle/High/Ultra no longer blocked at compile level; runtime scalability still follows the MMF/IO paths above.
Hardware Impact: 0 us runtime impact from verification itself.

## Loop 5 - OMEGA POLISH CHANGES

Problem: The Dear Lie audit required identifying honest work replaced by cheaper fakes.
Solution: Replaced full-file MMF touching with four 1MB cached read windows and 256KB edge prefetch; kept local position persistence as millimeter `short3`; preserved uniform voxel chunks as a 2-byte RLE state; recovered cached metadata hash low32 instead of rehashing/decompressing metadata on sector commit.
Rejected Alternatives: Whole-file read-only mapping, serialized float3 offsets, full 32^3 voxel payloads, and metadata rehash on every sector override.
Scalability potential: Low uses demand windows, 2.0ms hydration, 2-byte uniform chunks, and no redundant metadata hash. Middle keeps prefetch/flush queues smooth. High can retain more sector pages. Ultra can buy denser visual sector persistence with the saved IO.
Hardware Impact: Estimated combined hot-case savings: 42000 us page-fault burst, 3000 us edge stall, 2000 us hydration slice, 6000 us per 1000 positions, 65000 us uniform chunk IO/decode, 22000 us per sector commit, 40000 us invalid-save fast fail.

Problem: Frame-time dictatorship requires suspicion for anything over 0.1ms on an i3/MX350-class target.
Solution: The load restore slice is capped at 2.0ms with `Awaitable.NextFrameAsync()` yielding already present in the pipeline, while MMF read/prefetch and flush work stay off the foreground path. Sector failure quarantine prevents whole-save replay after one bad sector.
Rejected Alternatives: Main-thread full hydration, foreground flush, or full reload on sector checksum failure.
Scalability potential: Low keeps stutter contained; High/Ultra can scale visible persisted debris and sector detail because critical metadata stays front-loaded.
Hardware Impact: Foreground stalls reduced by the task-level estimates above; no added per-frame Tick was introduced.

Problem: Zero-GC purge needed final evidence from the diff.
Solution: Diff scan showed added allocations/error strings only in cold setup, IO, or failure-reporting surfaces; Data Archivist hot paths use persistent/native buffers, bounded arrays, and integer error codes. `git diff --check` reported no whitespace errors, only CRLF normalization warnings on touched files.
Rejected Alternatives: Rewrite unrelated pre-existing diagnostic strings and third-party/project-wide allocations; that is outside the save MMF task and would be a refactoring loop.
Scalability potential: Low avoids GC spikes in save paging; High/Ultra keep richer diagnostics without frame pressure.
Hardware Impact: Estimated 35 us GC/alloc avoided per candidate pass from bounded native candidate scratch; no new hot allocation cost accepted.

Problem: Final Git diff had to be captured for audit without confusing parallel-agent edits as sole Data Archivist output.
Solution: Captured the touched-file diff stat and noted that the worktree is shared with parallel agents.
Rejected Alternatives: Present the full repository diff as exclusive work; that would misattribute unrelated changes.
Scalability potential: Low/Middle/High/Ultra unaffected; audit hygiene only.
Hardware Impact: 0 us runtime impact.

Final Git diff stat at Omega audit:
Assets/_Project/Scripts/HydrationScheduler.cs     |   4 +-
Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs |  88 ++-
Assets/_Project/Scripts/SaveBinaryStorage.cs      | 802 ++++++++++++++--------
Assets/_Project/Scripts/SaveManager.cs            |  17 +-
Assets/_Project/Scripts/VoxelDeltaProcessor.cs    | 155 ++++-
5 files changed, 720 insertions(+), 346 deletions(-)
