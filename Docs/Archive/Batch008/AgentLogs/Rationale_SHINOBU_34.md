# Rationale_SHINOBU_34

Status: PENDING VERIFICATION / CORE COMPILE PASS RECORDED EARLIER / CURRENT EXTERNAL WALL

## Decision 00 - Agent Slot Initialization
Problem: SHINOBU_34 had no status or rationale file, but the batch protocol requires disk-backed memory before implementation.
Solution: Created fresh status and rationale files, recorded prompt extraction method, selected save/Merkle/LZ4/zero-GC/native-memory/telemetry/global-registry/execution-phase mandates.
Rejected Alternatives: Proceeding from chat memory or neighboring agent status files; this violates strict prompt isolation and causes cross-domain contamination.
Scalability potential: Low tier avoids repeated rediscovery work; middle/high/ultra tiers get the same deterministic implementation path without divergent agent state.
Hardware Impact: 0 us runtime impact; prevents agent-side architectural drift, not a shipped code path.

## Decision 01 - Legacy Header Archaeology Boundary
Problem: Archive search found save-era rationale and logs, but active source authority reports v9 `SaveBinaryStorage` with 56-byte storage header and staged v10 72-byte hash header. Implementing the Merkle layer against stale v8 byte counts would corrupt live saves.
Solution: Keep v8 as migration history only and add `SaveMerkleEmergencyHeader64` plus `GenerateEmergencyMockHeader()` for absent legacy schema files. The emergency header is 64 bytes, Magic `0x48454354`, Version `0x0009`, and records current Merkle/SectorEntry struct sizes.
Rejected Alternatives: Reusing 52-byte or 56-byte historical headers for the new Merkle WAL was rejected because the new path needs aligned root-hash and sector-directory metadata. Reading archive binaries into managed arrays was rejected by the zero-GC save mandate.
Scalability potential: Low tier pays no scan in the hot path and receives a deterministic fallback. Middle/high/ultra tiers can still run richer migration audits without changing the frame pipeline.
Hardware Impact: Emergency header write is a cold 64-byte memcpy, estimated 3 us worst case on i3/MX350 storage init, 0 us during autosave hashing.

## Decision 02 - Core Save JSON Boundary
Problem: Persistence scan found no core `SaveSystem` JSON serialization, but found mod sidecar `JsonUtility.ToJson` under `ModdingAPI`. Rewriting it directly would cross the assigned save/Merkle domain and risk mod API behavior.
Solution: The new Merkle implementation accepts only unmanaged DataVault-style byte leaves and reserves `0x4D50` MODP-prefixed sectors for mod payload isolation. CRC failure on a mod subblock is recoverable without contaminating base sectors.
Rejected Alternatives: A cross-domain mod persistence rewrite was rejected because SHINOBU_34 owns core save delta architecture, not the mod API contract surface. Keeping JSON in the base save path was not an option; no such base path was present.
Scalability potential: Low tier drops corrupt mod sectors cheaply; middle/high/ultra tiers can allocate more saved budget to high-fidelity base-state sectors while mod garbage remains quarantined.
Hardware Impact: 0 us in core hot path; sidecar routing prevents JSON-sized payloads from entering the Merkle WAL.

## Decision 03 - Raw Merkle DTOs and ARM64 Layouts
Problem: The Merkle tree and delta records need direct Burst writes; properties or unpadded structs cause CS1612 copies and ARM64 unaligned reads.
Solution: Added raw-field blittable DTOs: `MerkleNodeDTO` 32 bytes, `SectorEntry` 32 bytes, `StateDeltaRecordDTO` 64 bytes, `Lz4SubBlockHeader` 32 bytes, and manifest assertions for size/offsets.
Rejected Alternatives: `Pack=1`, C# properties, and managed wrappers were rejected. They save a few source lines but cost deterministic memory layout and job write clarity.
Scalability potential: Low/MX350 gets aligned reads and smaller delta records. Middle/high/ultra tiers can compare hashes in cache-line-friendly strides and spend saved frame time on higher visual state fidelity.
Hardware Impact: Expected 1-2 us saved on ARM64 directory/delta scans versus unaligned 28-byte sector entries; avoids copy penalties in 4096-leaf diff jobs.

## Decision 04 - Blind Inventory Mocking
Problem: The Merkle system cannot depend on another agent's inventory SOA, but still must prove byte-level change detection.
Solution: Added `MockInventoryData` and jobs that generate deterministic payloads, create leaf descriptors, and flip exactly one 4-byte word deep inside a selected record.
Rejected Alternatives: Directly referencing inventory runtime types was rejected because it creates a compile dependency on an unrelated domain. Random managed test data was rejected because it allocates and is not deterministic.
Scalability potential: Low tier can validate with compact 128-byte records; middle/high/ultra tiers can increase mock counts or source byte spans without altering the Merkle math.
Hardware Impact: Mock generation is test/cold path. The mutation itself is one aligned 32-bit XOR, sub-microsecond on i3/MX350.

## Decision 05 - Burst XXHash3 Merkle Topology
Problem: Autosave hitches come from treating the save as one giant blob. A one-byte inventory/base change should not force a full world serialization pass.
Solution: Implement a fixed 4096-leaf, 16-way Merkle tree: leaves hash raw unmanaged bytes with `xxHash3.Hash128(void*, length, seed)`, branches reduce 4096->256->16->1 into `MerkleNodeDTO`.
Rejected Alternatives: Walking Unity objects, JSON serialization, or saving a whole sector after any mutation were rejected. A binary flat checksum was also rejected because it identifies "changed" but not "where changed."
Scalability potential: Low = fewer active descriptors and coarser leaf coverage; Middle = default 4096 leaves; High = same topology with more frequent checks; Ultra = every-frame root validation and richer visual-state payloads.
Hardware Impact: Branch reduction is estimated 20-60 us for 4369 nodes. Leaf hashing target remains <500 us for 50 MB on high-tier hardware; low-tier reduces active descriptor count.

## Decision 06 - Fixed-Arena Delta Extraction
Problem: `NativeList<byte>` auto-growth can allocate when the player triggers an unexpected large delta, creating the exact autosave microfreeze this task is supposed to kill.
Solution: Use a preallocated `NativeArray<byte>` delta arena plus `NativeArray<int>` counters. `StateDeltaRecordDTO` is copied before raw leaf bytes, and overflow sets a flag instead of allocating.
Rejected Alternatives: Managed `List<byte>`, `byte[]`, and auto-growing `NativeList<byte>` were rejected. They hide growth cost and make worst-case save time unpredictable.
Scalability potential: Low tier caps arena size and drops cosmetic payloads; middle/high/ultra can increase arena capacity and keep more optional state.
Hardware Impact: Changed-leaf overhead is estimated 2-6 us plus memcpy. One-rock deltas stay tiny instead of rewriting megabytes.

## Decision 07 - WAL Commit Isolation
Problem: SSD or MicroSD flushes must not block simulation. The repo already has `H8BinaryWorldPager` as the save-domain background worker, so duplicating thread ownership risks queue races.
Solution: Add `TryAppendCompressedWalMmf()` as the Merkle WAL append primitive for `slot_0.wal`: 64-byte header, MMF write path, record CRC, fallback FileStream path. It is worker-thread intended and keeps main-thread ownership out of the file flush.
Rejected Alternatives: Main-thread `FileStream.Flush(true)` was rejected. Creating a second unrelated thread/queue was rejected because the existing pager already owns save I/O lifecycle and backpressure.
Scalability potential: Low/MicroSD uses throttled worker writes; middle/high/ultra can increase WAL bandwidth without changing Merkle/delta code.
Hardware Impact: Main thread cost is 0 us when called from the pager worker. I/O cost is isolated; MMF avoids an extra managed payload copy.

## Decision 08 - Dear Lie Dehydration
Problem: Dynamic presentation state like mid-air rotation and fish transforms explodes save size while adding no stable gameplay truth.
Solution: `DearLieDehydrationJob` records a stable-rest or needs-wake flag plus quantized sector-local AUP. Runtime simulation rebuilds motion from authoritative state after load.
Rejected Alternatives: Saving every moving object's exact transform was rejected because it buys false precision with file bloat. Direct ecosystem dependencies were rejected; this implementation only saves generic mocked DTO state.
Scalability potential: Low = save only rest/needs-wake and coarse half offsets; Middle = normal sector half offsets; High/Ultra = more frequent state snapshots if visual continuity budget allows.
Hardware Impact: Expected tens of KB saved per far dynamic sector; job cost estimated sub-10 us per 1000 records on i3/MX350.

## Decision 09 - LZ4 Subblock WAL Payloads
Problem: Raw sparse deltas are still too large when a cluster of sectors changes, and monolithic compression forces load to allocate/decompress more than needed.
Solution: `Lz4SubBlockCompressionJob` writes strict subblocks with `Lz4SubBlockHeader`, CRC32 over stored bytes, 16-byte payload alignment, and raw fallback when compression is not profitable.
Rejected Alternatives: Whole-save compression was rejected because it creates load-time spikes. LZ4 dictionary compression was rejected for this pass because the mandate file says dictionary use needs benchmark proof and bindings are not present in current source.
Scalability potential: Low = 16KB blocks with raw fallback; Middle = same blocks with normal WAL cadence; High = more frequent block commits; Ultra = can raise subblock size via CSV for fewer headers when I/O is fast.
Hardware Impact: Estimated 30-120 us per 256KB delta depending entropy; prevents multi-MB decompression spikes on low-end silicon.

## Decision 10 - Tombstone Pruning
Problem: DataVault tombstones avoid shifts but dead records must not be preserved forever in the save payload.
Solution: `TombstonePruneJob` copies only records with an alive bit into a fixed destination arena before hashing/extraction. It uses stride and flag offsets supplied by the owning domain.
Rejected Alternatives: Compacting owner arrays directly was rejected because SHINOBU_34 does not own inventory/base memory. Saving tombstones was rejected because it guarantees file growth over long playthroughs.
Scalability potential: Low = aggressive pruning before hash; Middle/high/ultra = same deterministic pruning with larger arenas if owners publish more state.
Hardware Impact: Estimated 1-3 us per 1000 records plus memcpy on i3/MX350; long-play save size stays bounded.

## Decision 11 - I/O Throttle and AUP Quantization
Problem: Slow MicroSD writes and double3 spatial payloads are two independent sources of autosave stalls and file bloat.
Solution: Add `ResolveWalBudgetBytesPerFrame()` with 16MB/s slow-I/O cap and route save spatial data through sector key + half3 quantization.
Rejected Alternatives: Unbounded background writes were rejected for Steam Deck/MicroSD. Serializing full double3 positions was rejected when sector-local half precision is already sufficient for reload.
Scalability potential: Low = throttle and coarser cadence; Middle = default cadence; High = higher write budget; Ultra = spends saved bytes on richer visual-overkill state, not more core precision.
Hardware Impact: Throttle math is ~0.05 us. Quantized AUP reduces spatial payload by about 70% with negligible CPU cost.

## Decision 12 - MODP Sidecar Isolation
Problem: Mod payloads are untrusted and can be malformed, bloated, or corrupt. They cannot share authority with base-game sectors.
Solution: Reserve `0x4D50` MODP-prefixed sector keys, mark delta records with `LeafFlagModPayload`, and make WAL validation skip corrupt mod records instead of rolling back clean core records.
Rejected Alternatives: Mixing mod and core payloads was rejected because a mod CRC failure would poison the authoritative save. Throwing on mod corruption was rejected because the base save must remain loadable.
Scalability potential: Low = drop corrupt mod sector; Middle/high/ultra = larger mod sidecars allowed without affecting core integrity.
Hardware Impact: Normal path cost is one mask compare; failed mod sector avoids a full rollback and Unity crash.

## Decision 13 - Zero-Init and Telemetry Black Box
Problem: Zeroing the Merkle tree and logging errors through managed strings would waste the same frame time the save system is trying to preserve.
Solution: Add `AllocateNodeTree()` with `NativeArrayOptions.UninitializedMemory`, a 300-frame `SaveMerkleTelemetryEntry` ring, `TelemetryWriteJob`, and binary dump writer for `Dump_SAVE_MERKLE_TREE.bin`.
Rejected Alternatives: Default-cleared NativeArrays were rejected for per-frame/tree churn. `Debug.Log` and managed telemetry lists were rejected because faults need binary post-mortem data without hot-path GC.
Scalability potential: Low = same 300-frame ring, sparse writes; Middle/high/ultra = richer root/CRC counters while preserving fixed ring size.
Hardware Impact: Zero-init bypass saves an estimated 2-5 us per tree allocation/init. Telemetry ring write is estimated 0.2 us.

## Decision 14 - State Delta X-Ray and Corruption Injection
Problem: Background save deltas are invisible unless a human can see changed branches and force corruption tests.
Solution: Add `State Delta X-Ray` EditorWindow reading published Merkle snapshots, drawing a 16x16 changed-branch grid, validating WAL, and exposing a `Corrupt Sector` button that overwrites four bytes in the WAL.
Rejected Alternatives: A log-only diagnostic was rejected because it would not show hot sectors spatially. Crashing on corruption was rejected; recovery must be visible and repeatable.
Scalability potential: Low = editor-only inspection with no runtime cost; Middle/high/ultra = more frequent snapshot publication and richer branch visualization if needed.
Hardware Impact: 0 us in player builds; editor-only diagnostics.

## Decision 15 - CSV Override Parser
Problem: Save constants such as subblock size and WAL budget must be tunable without recompiling, but text parsing cannot allocate or drag JSON into runtime saves.
Solution: Add a fixed native-scratch CSV monitor/parser. It streams `save_schema_overrides.csv` into `NativeArray<byte>`, hashes ASCII keys, and updates unmanaged `SaveMerkleRuntimeConfig`.
Rejected Alternatives: JSON, `string.Split`, `File.ReadAllBytes`, and ScriptableObject runtime reload were rejected because they allocate and couple save constants to managed/editor systems.
Scalability potential: Low = 16KB subblocks and 16MB/s cap; Middle = default config; High/Ultra = larger subblocks or higher budgets through CSV when hardware can afford it.
Hardware Impact: Tiny CSV parse is estimated <50 us cold; 0 us if file timestamp is unchanged.

## Decision 16 - Real Cosmetic Delta Prune Before LZ4
Problem: The low-tier cosmetic policy was not strong enough if the LZ4 job only reported bytes over the cosmetic threshold after compression. That accounting does not shrink the WAL payload and can hide autosave bloat.
Solution: `CosmeticDeltaPayloadPruneJob` now parses the fixed delta byte stream, rewrites `StateDeltaRecordDTO.DeltaPayloadOffset`, preserves non-cosmetic records with `UnsafeUtility.MemMove`, and drops only `LeafFlagCosmetic` records when `CosmeticDropThresholdBytes` is exceeded. `Lz4SubBlockCompressionJob` no longer writes fake cosmetic-drop counters; it only reports stored/raw/block/failure metrics.
Rejected Alternatives: Dropping bytes inside the LZ4 compressor was rejected because compression must remain a deterministic byte-to-byte transform with stable CRC semantics. Counting over-budget bytes without removing records was rejected as false telemetry.
Scalability potential: Low = compact the record stream before compression and keep core truth; Middle = same path, threshold usually not crossed; High = larger threshold through CSV; Ultra = threshold can be high or disabled while still keeping deterministic subblocks.
Hardware Impact: Estimated 8-35 us for large changed-record streams on i3/MX350, dominated by linear memmove. The win is reduced MicroSD/SSD write pressure and fewer autosave microstalls, not raw CPU savings.

## Decision 17 - Counter-Driven Job Chain Instead of Mid-Frame Complete
Problem: Delta extraction produces byte length in a counter, but compression needs that length. Reading it on the main thread would force a `Complete()` between jobs and reintroduce the autosave microfreeze this task exists to remove.
Solution: Added `ScheduleVaultDeltaWalPipeline()` and counter-driven source-length fields. `CosmeticDeltaPayloadPruneJob` and `Lz4SubBlockCompressionJob` read the previous job's `CounterBytes` inside their own scheduled execution. LZ4 metrics moved to counter slots 8-11 so delta/prune telemetry remains available.
Rejected Alternatives: Completing the delta job, reading `CounterBytes`, then scheduling LZ4 was rejected as a hidden synchronization wall. A monolithic all-in-one job was rejected because it would obscure the individual Merkle, prune, and compression verification points.
Scalability potential: Low = zero main-thread sync between hash and compression; Middle = same pipeline with default budgets; High/Ultra = larger buffers and more frequent validation without changing dependency topology.
Hardware Impact: Removes an estimated 0.05-0.6 ms sync-risk spike on busy autosaves, depending worker timing and changed-sector count.

## Decision 18 - Runtime Layout Rebuild and Real X-Ray Branch Mask
Problem: Several SHINOBU_34 structs were sized correctly but did not put 8-byte lanes first. The editor X-Ray also painted the first N cells instead of the actual dirty branches, which is misleading forensic UI.
Solution: Reordered `StateDeltaRecordDTO`, `SaveMerkleWalAppendHeader`, `SaveMerkleTelemetryEntry`, `SaveMerkleEmergencyHeader64`, `Lz4SubBlockHeader`, and `SaveMerkleEditorSnapshot` where applicable, then updated `BinaryLayoutManifest`. `PublishEditorSnapshot()` now can derive a 256-bit Level2 changed-branch mask from current/previous Merkle trees, and the X-Ray grid reads that mask.
Rejected Alternatives: Keeping size-only alignment was rejected because ARM64 audit requires offset-level proof. Count-only grid visualization was rejected because it cannot identify the sector cluster writing to disk.
Scalability potential: Low = tighter branch visibility for eliminating save bloat; Middle = default diagnostics; High/Ultra = richer editor forensics without runtime player cost.
Hardware Impact: Runtime scan benefit is estimated 1-3 us on ARM64 WAL/telemetry passes through predictable 8-byte lanes. X-Ray cost is editor-only.

<SELF_AUDIT>
  <json_or_byte_array_hot_path>No JsonUtility/System.Text.Json/new byte[] in `SaveStateMerkleTree.cs`; static scan clean for the Merkle save layer.</json_or_byte_array_hot_path>
  <sector_entry_layout>SectorEntry = 32 bytes: SectorHash 0, ByteOffset 8, CompressedSize 16, DecompressedSize 20, Checksum 24, _pad0 28.</sector_entry_layout>
  <cs1612_properties>No DTO `{ get; set; }`; Merkle/delta records are raw unmanaged fields and jobs write native arrays directly.</cs1612_properties>
  <mock_dependencies>Inventory/base/ecosystem dependencies are mocked locally via `MockInventoryData`, `MockStatePayload`, and generic leaf descriptors.</mock_dependencies>
  <editor_facade>`State Delta X-Ray` EditorWindow exists with Merkle snapshot visualization, WAL validation, and `Corrupt Sector` injector.</editor_facade>
</SELF_AUDIT>

<POLISH_SELF_AUDIT>
  <task_matrix>01 PASS; 02 PASS; 03 PASS; 04 PASS; 05 PASS; 06 PASS; 07 PASS; 08 PASS; 09 PASS; 10 PASS; 11 PASS; 12 PASS; 13 PASS; 14 PASS; 15 PASS; 16 PASS; 17 PASS; 18 PASS; 19 PASS; 20 PASS.</task_matrix>
  <arm64_layouts>MerkleNodeDTO 32 bytes: HashLo 0, HashHi 8, SectorKey 16, ChildMask 20, _pad0 24. SectorEntry 32 bytes: SectorHash 0, ByteOffset 8, CompressedSize 16, DecompressedSize 20, Checksum 24, _pad0 28. Lz4SubBlockHeader 32 bytes: Magic 0, RawBytes 8, SourceOffsetBytes 16, Crc32 20, flags 24.</arm64_layouts>
  <zero_gc_check>Owned hot jobs use NativeArray, raw pointers, XXHash3, fixed counters, and stack/value DTOs. No LINQ, managed byte arrays, JSON, NativeList growth, or DTO properties in `SaveStateMerkleTree.cs`.</zero_gc_check>
  <aup_check>AUP save path uses sector key plus `QuantizedAupSectorHalf3`; absolute world data is represented as sector-local half offsets for save size, not direct float-casted 100km coordinates.</aup_check>
  <dear_lie_check>Dynamic exact rotation/boid transform state is not saved. `DearLieDehydrationJob` saves stable-rest or needs-wake plus quantized AUP so simulation can resume deterministically.</dear_lie_check>
  <dependency_check>No inventory/base/ecosystem direct runtime dependency was added. Cross-domain state enters as unmanaged byte spans/descriptors; editor facade reads a published save-domain snapshot.</dependency_check>
  <h_phi_check>Persistent arrays are expected from GlobalDataVault BufferIDs `SaveMerkle*`; the only allocation helper explicitly uses `NativeArrayOptions.UninitializedMemory` for vault/bootstrap ownership, not update-loop ownership.</h_phi_check>
  <blackbox_check>300-frame `SaveMerkleTelemetryEntry` ring and `TryDumpTelemetry()` binary dump path are implemented for `Docs/AgentLogs/Dump_SAVE_MERKLE_TREE.bin`.</blackbox_check>
  <compile_guard>Core project compile passed once with 0 warnings and 0 errors. Later core/editor compiles hit concurrent external files (`ShinobuLogisticsRouter`, editor reference walls), not SHINOBU_34-owned code.</compile_guard>
</POLISH_SELF_AUDIT>

<LOOP_7_SELF_AUDIT>
  <task_matrix>01 PASS; 02 PASS; 03 PASS; 04 PASS; 05 PASS; 06 PASS; 07 PASS; 08 PASS; 09 PASS; 10 PASS; 11 PASS; 12 PASS; 13 PASS; 14 PASS; 15 PASS; 16 PASS; 17 PASS; 18 PASS; 19 PASS; 20 PASS.</task_matrix>
  <arm64_layouts>MerkleNodeDTO 32 bytes: HashLo 0, HashHi 8, SectorKey 16, ChildMask 20, _pad0 24. SectorEntry 32 bytes: SectorHash 0, ByteOffset 8, CompressedSize 16, DecompressedSize 20, Checksum 24, _pad0 28. StateDeltaRecordDTO 64 bytes: SectorKey 0, Flags 4, SourceOffsetBytes 8, DataLength 12, DeltaPayloadOffset 16, CompressedOffset 20, PreviousHashLo 24, PreviousHashHi 32, NewHashLo 40, NewHashHi 48, Crc32 56, _pad0 60.</arm64_layouts>
  <zero_gc_check>`MerkleChangedLeafExtractionJob`, `CosmeticDeltaPayloadPruneJob`, and `Lz4SubBlockCompressionJob` use NativeArray, raw pointers, fixed counters, and no managed allocations, LINQ, boxing, or string work in Execute.</zero_gc_check>
  <aup_check>Save spatial data remains sector key plus `QuantizedAupSectorHalf3`; absolute double3 is reduced to sector-local half offsets, not direct float world coordinates.</aup_check>
  <dear_lie_check>The physical fake is still stable-rest/needs-wake dehydration for transient motion; low-tier cosmetic records can be dropped before WAL compression.</dear_lie_check>
  <dependency_check>No sibling runtime domain dependency was added. The prune/compress stages operate on unmanaged delta byte streams from DataVault buffers and do not reference inventory/base/ecosystem types.</dependency_check>
  <blackbox_check>300-frame `SaveMerkleTelemetryEntry` ring is still present; no JSON telemetry was added.</blackbox_check>
  <compile_guard>Loop 7 core build hit external compile wall in `GlobalTelemetryBus.Blackbox.cs`, `GlobalPhysicsStateManager.cs`, and `SubmarineDynamicsRuntime.cs`; SHINOBU_34-owned files emitted no errors in the captured output.</compile_guard>
</LOOP_7_SELF_AUDIT>

<LOOP_9_SELF_AUDIT>
  <task_matrix>01 PASS; 02 PASS; 03 PASS; 04 PASS; 05 PASS; 06 PASS; 07 PASS; 08 PASS; 09 PASS; 10 PASS; 11 PASS; 12 PASS; 13 PASS; 14 PASS; 15 PASS; 16 PASS; 17 PASS; 18 PASS; 19 PASS; 20 PASS.</task_matrix>
  <arm64_layouts>StateDeltaRecordDTO 64 bytes: PreviousHashLo 0, PreviousHashHi 8, NewHashLo 16, NewHashHi 24, SourceOffsetBytes 32, DataLength 36, DeltaPayloadOffset 40, CompressedOffset 44, SectorKey 48, Flags 52, Crc32 56, _pad0 60. SaveMerkleWalAppendHeader 64 bytes: LogicalOffset 0, TimestampTicks 8, RootHashLo 16, RootHashHi 24, RawBytes 32, StoredBytes 36, Magic 40, Flags 44, BlockCount 48, Frame 52, RecordCrc32 56, Version 60, HeaderBytes 62. SaveMerkleTelemetryEntry 64 bytes: RootHashLo 0, RootHashHi 8, TotalBytesHashed 16, DeltaBytesGenerated 20, TreeComputeTimeMs 24, Frame 28, Flags 32, ChangedLeaves 36, WalBytesWritten 40, CrcFailures 44, IoFailures 48, _pad0 52, _pad1 56.</arm64_layouts>
  <zero_gc_check>`ScheduleVaultDeltaWalPipeline()` chains jobs through dependencies and NativeArray counters; it does not allocate and does not require `.Complete()` between delta/prune/LZ4. Static scan found no `new NativeArray`, JSON, managed byte arrays, LINQ, `foreach`, or hot string APIs in owned runtime/editor files.</zero_gc_check>
  <aup_check>AUP remains sector-local: `double3` absolute coordinates are quantized through `QuantizedAupSectorHalf3`; no absolute AUP is cast directly to float.</aup_check>
  <dear_lie_check>Stable-rest/needs-wake dehydration remains the physical fake; cosmetic deltas can be dropped pre-LZ4 on constrained storage tiers.</dear_lie_check>
  <dependency_check>Vault buffers are resolved through `IDataVault` and `BufferID.SaveMerkle*`; no inventory/base/ecosystem direct dependency was added.</dependency_check>
  <h_phi_check>`TryResolveVaultBuffers()` resolves all Merkle trees, descriptors, delta bytes, pruned bytes, compressed bytes, LZ4 headers, counters, hash table, and telemetry ring from GlobalDataVault-owned BufferIDs.</h_phi_check>
  <blackbox_check>300-frame `SaveMerkleTelemetryEntry` remains active and now has an 8-byte-first layout.</blackbox_check>
  <compile_guard>Core compile passed with 0 errors. Editor compile still hits external tuner/window walls before SHINOBU_34 errors.</compile_guard>
</LOOP_9_SELF_AUDIT>
