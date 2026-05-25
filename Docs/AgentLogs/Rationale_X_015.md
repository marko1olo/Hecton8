# Rationale_X_015

Status: COMPLETE

## Phase 0 Intake

Problem: X_015 must audit voxel paging/RLE/pooling without mutating C# source.
Solution: Use CLI extraction of the X_015 XML block, read task-relevant mandates, then perform static source and byte-layout audit only.
Rejected Alternatives: Editing or instrumenting C# was rejected by assignment constraint. Relying on architecture docs was rejected because active source lines are required.
Scalability potential: Low/Middle/High/Ultra report will separate fixed save-format truth from optional quality-scaled runtime presentation; GlobalQualityWeight must not alter DTO layout or save identity.
Hardware Impact: Static audit has no runtime cost. Findings target prevention of sector overflow and native buffer leaks on i3/MX350 class devices.

## Mandates Selected

- VOX_Voxel_World_Logic_Carving_Persistence.txt
- STRM_World_Streaming_Residency_Chunk_Management.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- DATA_Save_Persistence_Binary_Delta_Checksum.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt

## Decision 01 - Pager Envelope Boundary

Problem: Future agents can confuse `H8BinaryWorldPager` sector payload capacity with voxel delta RLE run capacity.
Solution: Split report into outer pager envelope, VXRL pager payload, and native snapshot record formats. The hard outer payload cap is 262080 bytes; VXRL has a 32-byte header inside that cap.
Rejected Alternatives: Treating all RLE as one format was rejected because `H8BinaryWorldPager.TryCompressRle` is byte RLE over arbitrary page payload, while `VoxelDeltaRleRunDTO` is a voxel run stream.
Scalability potential: Low uses raw fallback and hard page cap; Middle uses sparse RLE under cap; High uses LZ4/raw selection; Ultra can use saved cycles for denser visuals but cannot alter save payload identity.
Hardware Impact: Prevents failed page writes on i3/MX350 by enforcing cap before disk queue. Estimated avoided retry path: 400 us per bad page write.

## Decision 02 - Directory Slot Risk

Problem: DirectorySlotCount is 252 but ResolveDirectorySlot uses bitmasking with 251.
Solution: Report the exact collision risk as metadata-route defect: only 128 values are reachable. No code changed due read-only assignment.
Rejected Alternatives: Calling it harmless was rejected because directory entries are the proof artifact for sector hash to sector offset, even though the body offset path uses an 8192-sector mask.
Scalability potential: Low through Ultra all need the same directory truth; GlobalQualityWeight must not alter save lookup route.
Hardware Impact: Fixing later would reduce unnecessary lookup ambiguity and recovery work on low-end storage. Read-only audit saves 0 us at runtime.

## Decision 03 - Native Snapshot Dense-Equivalent Limit

Problem: Native snapshot code has dense fallback per chunk, but scratch capacity only budgets 256 dense-equivalent records plus 256 uniform records.
Solution: Report the exact scratch formula: 16 + 256 * 135208 + 256 * 44 = 34624528 bytes, with risk when dense-equivalent records exceed 256.
Rejected Alternatives: Reporting only `SaveBinaryStorage.RawPayloadCapacityBytes` was rejected because actual `VoxelDeltaProcessor` borrowed scratch capacity is lower than 64 MiB.
Scalability potential: Low should split or defer dense-equivalent records; Middle should compact before save; High/Ultra can spend cycles on better pre-save densification, not on larger DTO truth.
Hardware Impact: Avoids failed full snapshot copy attempts. Estimated avoided low-end copy failure path: 2500 us per saturated snapshot attempt.

## Decision 04 - Source Integrity

Problem: Assignment prohibits C# edits and asks for a structural audit only.
Solution: Generated JSON, Markdown, status, rationale, and log files only. Build skipped because no source/project metadata changed.
Rejected Alternatives: Adding probes or layout asserts into C# was rejected because it violates non-destructive scout scope.
Scalability potential: Report is zero-runtime-cost and can guide later Low/Middle/High/Ultra implementations.
Hardware Impact: Runtime impact is 0 us. Documentation prevents future allocation and overflow defects before they hit low-end devices.

## APEX Decision 05 - RLE Layer Separation

Problem: The previous report could be read as if the 3-byte pager byte-RLE stream and the 8-byte voxel deformation run DTO were one format.
Solution: The APEX addendum separates outer page storage, inner VXRL header, and deformation run records. It also records that VXRL raw/LZ4 flags are telemetry counters, not persisted header fields.
Rejected Alternatives: Describing "the RLE packet" as one structure was rejected because it hides the pager's optional byte-RLE layer and the VXRL LZ4/raw ambiguity.
Scalability potential: Low/Middle/High/Ultra can tune compression effort, but must not change the 32-byte header or 8-byte run DTO stride.
Hardware Impact: Prevents false sector budgeting on i3/MX350 class devices. Estimated avoided failed write path: 400-900 us per oversized sector attempt.

## APEX Decision 06 - Compaction Race Honesty

Problem: The compaction flow has a write-version guard, but update order allows compaction copy jobs and later carve commits to overlap theoretically.
Solution: Report both facts: the version gate normally prevents dirty overlay removal after later writes, but `_chunkWriteVersions.TrySet` failure can weaken that gate and create a theoretical deformation-loss path.
Rejected Alternatives: Declaring the path race-free was rejected because `Tick` schedules compaction and `LateFrameTick` commits carve writes without an explicit dependency on the compaction copy job.
Scalability potential: Low devices need fewer overlapping jobs; Middle/High/Ultra can use more aggressive compaction only with explicit job dependencies or version reservation.
Hardware Impact: Avoids silent loss under pressure. Fixing later should cost near 0 us in normal frames if implemented as a dependency/version protocol rather than blocking completes.
