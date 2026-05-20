# SHINOBU_154 Rationale

Status: STATIC IMPLEMENTATION IN PROGRESS - UNITY/BURST RUNTIME PROOF BLOCKED BY DEPENDENCY COMPILE WALL

## Initial Scope Decision

Problem: Dynamic entity saves require byte-level delta persistence without JSON, reflection, object traversal, or main-thread file stalls.
Solution: Build within SaveSystem/Core memory reality discovered on disk. Use explicit unmanaged DTOs, AUP sector keys, Burst extraction jobs, RLE preconditioning, native LZ4 binding if present, and WAL handoff through a bounded staging buffer.
Rejected Alternatives: JSON/BinaryFormatter/object graph traversal rejected by task and SaveManager mandate; direct `.sav` writes rejected by atomic save contract; dictionary LZ4 rejected unless existing binding supports it and corpus evidence exists.
Scalability potential: Low uses RLE-fast and coarse profiling cadence; Middle uses RLE+LZ4 fast; High uses deeper LZ4 effort if latency budget allows; Ultra spends saved CPU on denser telemetry and editor diagnostics, not larger authority structs.
Hardware Impact: i3/MX350 target is reduced autosave hitch by moving extraction/compression to jobs and disk write to WAL background path; estimated hot main-thread savings are pending source inspection and compile verification.

## First-20-Minutes Route

Problem: Autosave hitch during early scavenging/base-module placement can break the first 20 minutes route.
Solution: Persist only deltas for dropped items, fauna state records, base modules, and depleted nodes so route-critical scavenging state survives without visible freeze.
Rejected Alternatives: Full snapshot saves and scene-wide serialization rejected due unbounded latency and managed heap pressure.
Scalability potential: Same byte format scales by sector paging; high-tier improves diagnostics/compression effort without changing gameplay truth.
Hardware Impact: PENDING MEASUREMENT; static target is sub-0.1 ms scheduling overhead on main thread, worker latency visible only in telemetry.

## Loop 1 - Tasks 01-05

Problem: `entity_save_schema.h8bin` and dedicated entity delta DTOs were absent; live entity save truth is spread across managed/Unity-facing compatibility types.
Solution: Added `EntityDeltaCompressionArchitecture` with deterministic emergency schema bytes, explicit `EntityDeltaHeaderDTO` and `EntityDeltaDataRecordDTO`, Vault buffers now spanning `70340..70357`, and `GenerateMockEntityStateJob`.
Rejected Alternatives: Editing `ModuleDTO`, `WorldStateDTO`, `ProceduralWorldStateDTO`, `PersistentWorldItemRecord`, or `FaunaBrain` was rejected because those are live compatibility/domain-owner surfaces and would cause cross-domain blast radius. New save-only DTOs avoid CS1612 and `Pack=1` without rewriting unrelated owners.
Scalability potential: Low uses sparse mock mutation, byte-RLE and low write cadence; Middle uses RLE plus fast LZ4; High increases hash-table coverage; Ultra raises diagnostics/profile fidelity without changing authoritative payload schema.
Hardware Impact: Expected i3/MX350 win is removal of object graph traversal and serializer allocations from autosave. Static estimate: thousands of records reduce to `deltaCount * 80` bytes before compression; measured microseconds pending guarded compile/runtime proof.

## Loop 2 - Tasks 06-10

Problem: Saving every entity slot produces a full snapshot and blocks on CPU/disk.
Solution: Added `EntityTombstonePruneJob`, `ExtractEntityDeltaJob`, dense byte pack, RLE preconditioner, deterministic LZ4-format Burst job, and WAL payload pack routed to `IAsyncPersistenceService`.
Rejected Alternatives: `NativeList<byte>` in the prompt was rejected for the runtime route because the H-PHI Vault law requires pre-owned Vault buffers and explicit cursors. Native LZ4 P/Invoke was rejected inside Burst because the existing project route does not expose a Burst-callable dictionary binding; the current house pattern is a deterministic LZ4 block encoder in a Burst job.
Scalability potential: Compression effort is `math.lerp`/smoothstep-driven by `GlobalQualityWeight`, I/O pressure, and disk latency. Below 0.3 quality the LZ4 hash coverage collapses toward 512 slots, match probe step widens, and write Hz trends to the low endpoint.
Hardware Impact: Expected i3/MX350 win is bounded worker compression cost under I/O pressure and no main-thread disk write. Exact frame cost remains pending Unity/Burst proof.

## Loop 3 - Tasks 11-16

Problem: Delta bytes need corruption detection, rollback determinism, and postmortem telemetry.
Solution: Checksums use `SaveStateMerkleTree.Hash128` folded to 64 bits into `EntityDeltaHeaderDTO.XXHash3Checksum`; the read helper verifies bytes before hydration. The pipeline uses deterministic Burst float mode and simulation-frame/tick inputs, stores integer AUP sector coordinates plus local offsets, and writes a 300-entry telemetry ring with latency spike dump support.
Rejected Alternatives: Unity `Time.deltaTime`, `UnityEngine.Random`, absolute `float3` world coordinates, and synchronous file writes were rejected. The WAL handoff uses the existing save pager background thread and typed payload route instead of direct file ownership.
Scalability potential: Low/Middle/High/Ultra tiers share the same byte format; quality changes only workload cadence, LZ4 probe depth, RLE threshold, and diagnostics. This prevents save incompatibility across hardware.
Hardware Impact: Expected i3/MX350 win is bounded cache-line-friendly DTO scanning and false-sharing-padded block counters. Static layout proof: header 32 bytes, entity record 80 bytes, block counter 64 bytes.

## Global Authority Route Card

Owner: `SystemID.SavePersistence`
Fact: Dynamic entity save deltas.
Route: live producer systems write flat `EntityDeltaDataRecordDTO` records into `GlobalDataVault`; `EntityDeltaCompressionArchitecture.ScheduleCompressionPipeline` consumes current/baseline Vault arrays and returns a `JobHandle`; completed WAL bytes are enqueued through `IAsyncPersistenceService.TryEnqueueChunkPageWrite`.
Buffers: `SaveEntityDeltaSchemaBytes`, `SaveEntityDeltaCurrentRecords`, `SaveEntityDeltaBaselineRecords`, `SaveEntityDeltaRecords`, `SaveEntityDeltaBlockCounters`, `SaveEntityDeltaDenseBytes`, `SaveEntityDeltaRleBytes`, `SaveEntityDeltaCompressedBytes`, `SaveEntityDeltaLz4HashTable`, `SaveEntityDeltaHeaders`, `SaveEntityDeltaCounters`, `SaveEntityDeltaTelemetryRing`, `SaveEntityDeltaTelemetryCursor`, `SaveEntityDeltaTuning`, `SaveEntityDeltaSectorStats`, `SaveEntityDeltaCsvScratch`, `SaveEntityDeltaProfiles`, `SaveEntityDeltaWalPayloadBytes`.
Proof: Static source and layout manifest hooks added. Unity import, Burst Inspector, profiler, WAL replay, and save/load route proof remain pending.

## Compile Wall Reality Check

Problem: `Assets/_Project/Scripts/Hecton8.Core.asmdef` already owns `SaveSystem` and has pre-existing references to several sibling runtime assemblies. Splitting the save lane into a new asmdef during this pass would create a high-risk circular boundary because `BinaryLayoutManifest`, `SaveManager`, and the existing voxel/Merkle save lanes currently live in the root Core assembly shape.
Solution: Do not mutate the root asmdef in this lane. Keep SHINOBU_154 code limited to `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, `Hecton8.Core.Memory.Layout`, Unity Burst/Collections/Jobs/Mathematics, and BCL/Unity editor surfaces. Record the asmdef reality rather than pretending the root compile wall is clean.
Rejected Alternatives: Adding `Hecton8.SaveSystem.asmdef` now was rejected because it would force cross-assembly rewiring of existing save contracts and likely break import for unrelated save lanes.
Scalability potential: Local file-level dependency hygiene is preserved; full compile-wall repair needs an integrator-owned asmdef split.
Hardware Impact: Avoids a broad recompile/refactor blast radius while keeping the entity delta compressor source free of new sibling-domain routes.

## Compile Gate

Problem: AGENTS forbids launching dotnet/compile when CPU load is above 50 percent or another compiler is running.
Solution: Checked process and CPU state before build; no `dotnet`/`csc` process was visible, but CPU load reported 100 percent, so no build was launched.
Rejected Alternatives: Starting `dotnet build` under thermal/CPU load to satisfy checklist theater.
Scalability potential: Protects iteration hardware and avoids compile wall contention with the 20+ parallel-agent batch.
Hardware Impact: Prevents avoidable developer-machine stalls; compile proof remains pending.

## Loop 6 - OnDrawGizmos and Ref Mutation Polish

Problem: Task 19 explicitly requires an `OnDrawGizmos` hook, and the hot delta/prune jobs still relied on `NativeArray<T>` indexers for record and counter mutation.
Solution: Added `EntityDeltaGizmoProbe.OnDrawGizmos`, which reads `SaveEntityDeltaSectorStats` and draws green-to-red sector wire boxes without mutating Vault truth. Added `ElementAsRef<T>` and `ElementAsReadOnlyRef<T>` helpers backed by `UnsafeUtility.AsRef`; `GenerateMockEntityStateJob`, `EntityTombstonePruneJob`, and `ExtractEntityDeltaJob` now use ref access for hot record/counter writes and read-only refs for baseline/current comparison.
Rejected Alternatives: Keeping the SceneView delegate as the only visualizer was rejected because it did not satisfy the literal OnDrawGizmos hook. Leaving indexer mutation was rejected because the mandate demands ref/native pointer mutation in cache-hot DTO loops.
Scalability potential: Low through Ultra tiers share the same editor visual truth; heat color scales continuously by compressed bytes rather than switching tier labels. Ref access does not change save format, only lowers copy risk in the scanner path.
Hardware Impact: i3/MX350 expected gain is small per entity but accumulates across thousands of records by avoiding avoidable DTO copy paths in extraction/pruning; exact microseconds still require Unity/Burst profiler proof.

## Static Gate Refresh

Problem: New polish could reintroduce forbidden patterns or whitespace defects.
Solution: Ran focused `git diff --check`, forbidden hot-path pattern scan, direct sibling namespace scan, and compiler process/CPU gate. Added stable Unity `.meta` files for `EntityDeltaCompressionArchitecture.cs`, `EntityDeltaGizmoProbe.cs`, and `EntitySaveTunerWindow.cs`.
Rejected Alternatives: Running `dotnet build` while CPU telemetry reports 100 percent, which violates the project build-protection rule and the user's explicit instruction.
Scalability potential: Static hygiene remains independent of hardware tier; compile/runtime proof waits for a safe machine window.
Hardware Impact: No additional runtime allocation surfaced by static scan; build proof remains blocked by CPU gate.

## Loop 7 - Task 20 Burst Audit Hardening

Problem: The 99 percent compression-ratio proof existed as a cold managed helper over telemetry, but Task 20 requires an embedded self-audit routine that can participate in the job graph.
Solution: Added `EntityDeltaCompressionRatioAuditJob` with deterministic Burst flags and `[NoAlias]` fields. It scans the 300-frame telemetry ring, ignores fatal samples, writes `CounterAuditSamples`, `CounterAuditSmallerPayloads`, and `CounterAuditPass`, and is scheduled through `ScheduleCompressionRatioSelfAudit`.
Rejected Alternatives: Treating the Markdown `<SELF_AUDIT>` block as the only verification mechanism was rejected. A managed test-only loop remains available for editor/cold validation, but the authoritative recurring audit now runs against Vault telemetry as a job.
Scalability potential: Low/Middle/High/Ultra use identical audit math; quality changes workload and payload size, not the pass criterion. This prevents low-tier saves from silently accepting full snapshots as normal.
Hardware Impact: Audit cost is bounded to 300 telemetry entries and one counters buffer write; expected i3/MX350 cost is negligible relative to compression jobs, but profiler proof remains pending.

## Loop 7 Static Gate

Problem: The new Burst audit job could have broken source hygiene or Burst directive consistency.
Solution: Re-ran focused `git diff --check`, forbidden-pattern scan, Burst directive scan, `[NoAlias]` scan, untracked file trailing-whitespace/brace/meta checks, and guarded compiler-process check.
Rejected Alternatives: Launching a build while CPU telemetry failed to produce a safe below-50-percent reading.
Scalability potential: The audit path is fixed 300-entry work and does not add hardware-tier branching.
Hardware Impact: No source-level allocation or direct sibling namespace regression was found; compile/runtime proof remains blocked by build gate.

## Loop 9 - Audit Chain Integration

Problem: A schedulable audit job still depends on callers remembering to schedule it after the compressor, which is exactly how verification rots.
Solution: `ScheduleCompressionPipeline` now schedules `EntityDeltaCompressionRatioAuditJob` immediately after `EntityDeltaTelemetryRecordJob` when telemetry buffers are present. The returned `JobHandle` includes extraction, pruning, dense packing, RLE, LZ4, checksum, WAL payload pack, telemetry, and ratio audit.
Rejected Alternatives: Leaving audit as an optional external call was rejected because it weakens Task 20 and makes CI/playmode coverage dependent on call-site discipline.
Scalability potential: The fixed 300-entry audit remains independent of hardware tier; `GlobalQualityWeight` changes payload generation, not the proof path.
Hardware Impact: Adds one tiny job after telemetry. Expected i3/MX350 cost is bounded to 300 ring reads and 3 counter writes; runtime profiler proof remains pending.

## Loop 10 - RLE Stream Header and WAL Replay Validation

Problem: RLE preconditioning can legitimately bypass pair encoding and copy dense bytes, but the previous inner stream had no self-describing mode marker. A loader could verify the outer checksum and still misinterpret raw dense bytes as `{run,value}` pairs.
Solution: Added `EntityDeltaRleStreamHeaderDTO` as a 16-byte inner stream header before the RLE/raw payload. It stores magic, exactly one mode bit, dense byte count, and stored payload byte count. The RLE preconditioner writes this header before LZ4; the WAL read helper validates the inner stream when the LZ4 stage stores raw bytes, and `TryValidateRleStreamPayload` is available for post-decompression validation by load code.
Rejected Alternatives: Encoding RLE/raw mode in `EntityDeltaHeaderDTO._pad0` was rejected because those bytes are reserved alignment padding in the assigned 32-byte header. Guessing by byte pattern was rejected because arbitrary raw dense bytes can look like pair data.
Scalability potential: Low/Middle/High/Ultra all use the same stream contract. Quality changes whether RLE wins and how much LZ4 probes, not how the loader interprets bytes.
Hardware Impact: Adds 16 bytes per entity-delta payload and a cold validation scan over RLE pairs when replaying raw stream bytes. It prevents corrupt or ambiguous WAL hydration without adding frame hot-path allocations.

## Loop 11 - Post-Pack WAL Envelope Audit

Problem: The checksum job validates `CompressedBytes`, but the actual WAL buffer is a later packed copy with its own byte-count cursor. A stale counter or bad copy would not be detected until load if the pipeline returned immediately after pack.
Solution: Added `EntityWalPayloadEnvelopeAuditJob` after `EntityWalPayloadPackJob`. It runs in Burst, reads the serialized outer header from `WalPayloadBytes`, compares it to `Headers[0]`, verifies compressed/uncompressed sizes against the packed byte count, recomputes the checksum over the packed payload, and validates the raw RLE stream when LZ4 was bypassed. No-payload headers pass as no-op saves without setting fatal failure. `TryEnqueueEntityDeltaWalWrite` now requires `CounterWalEnvelopeAuditPass == 1` and rejects out-of-range byte counts instead of clamping.
Rejected Alternatives: Rehashing inside `TryEnqueueEntityDeltaWalWrite` was rejected because that would move O(payload) work back to the main-thread enqueue boundary. Trusting the pack job blindly was rejected because Task 20 requires verification to be embedded in the dependency graph.
Scalability potential: The audit uses the same byte format on every tier. Low quality may bypass LZ4 more often; in that case the audit validates the raw RLE envelope in the worker chain rather than changing load semantics.
Hardware Impact: Adds one worker job and one extra checksum pass over packed bytes. It does not allocate managed memory and does not block the caller with `Complete()`. Profiler proof remains pending.

## Loop 12 - Burst Load-Side WAL Decode Path

Problem: A save-only compressor with a checksum helper still leaves Task 11 half-proven: the load side must verify before decompression and hydrate raw records without managed buffers.
Solution: Added `ScheduleWalPayloadDecodePipeline`, `EntityWalPayloadDecodeJob`, and `EntityRleStreamExpandToRecordsJob`. The first job reads the WAL header, rejects bad byte counts, verifies XXHash3 over the packed payload, and either copies raw RLE bytes or decodes the deterministic LZ4 block into Vault `RleBytes`. The second job validates the RLE stream header, expands pairs or raw dense bytes into Vault `DenseBytes`, and copies whole 80-byte records into `DeltaRecords`.
Rejected Alternatives: A managed loader using `byte[]`/`MemoryStream` was rejected by the prompt. Decompressing during `TryReadAndVerifyWalPayload` was rejected because it would force caller-side blocking and blur verification with hydration.
Scalability potential: Low quality often reaches raw RLE, Middle uses RLE+LZ4, High/Ultra can spend more LZ4 probe work; the decode contract is stable across all tiers.
Hardware Impact: Adds two worker jobs to the load/replay path and no managed allocations. Main-thread work remains scheduling and later dependency tracking; compile/runtime proof remains pending.

## Loop 13 - Public Contract and No-Op WAL Guard

Problem: The Vault-backed counter buffer is 32 entries, but the public enqueue helper was only guarding access through `CounterWalPayloadBytes` before reading `CounterWalEnvelopeAuditPass`. A short test or integration buffer could throw before returning `false`. The cold WAL verifier also rejected the same header-only zero-delta payload that the Burst decode path treats as a valid no-op replay.
Solution: Tightened `TryEnqueueEntityDeltaWalWrite` to require `CounterCapacity`, added exact header-only no-op acceptance to `TryReadAndVerifyWalPayload`, and made `EntityDeltaRleStreamHeaderDTO` public so layout manifest/editor tests remain legal if SaveSystem is split out of the current root Core asmdef later.
Rejected Alternatives: Silently clamping counter access was rejected because it would hide a broken integration buffer. Treating no-delta autosaves as corrupt was rejected because it creates false load failures for sectors with no modified entities.
Scalability potential: Low/Middle/High/Ultra all share this byte contract; the change only hardens boundary validation and does not alter compression workload.
Hardware Impact: Removes an exception edge from cold/public route validation and keeps no-op WAL replay branch O(1). No hot-path managed allocation is introduced.

## Loop 14 - Symmetric Entity WAL Read Facade

Problem: Entity WAL writes use an entity-specific pager key derived from `SectorHash + EntityDeltaRle`, but no matching helper existed for reads. A future loader could request the raw sector hash and miss the page even though the write path was correct.
Solution: Added `TryRequestEntityDeltaWalRead(...)` overloads for `ulong sectorHash` and `int3 sectorCoord`, plus `TryCopyCompletedEntityDeltaWalPayload(...)`. These route only through `IAsyncPersistenceService`, validate request id/ticket payload type, and feed caller-owned `NativeArray<byte>` that can be passed to `ScheduleWalPayloadDecodePipeline`.
Rejected Alternatives: Adding a new pager interface or SaveManager-specific call was rejected because it would widen the compile wall and violate the owner-local route. Guessing mixed keys at call sites was rejected because one fact needs one route.
Scalability potential: The helper is O(1) and tier-independent; Low through Ultra differ only in compressed payload size and compression effort.
Hardware Impact: Prevents failed read retries and avoids any managed staging buffer on hydration. Main-thread work remains a small request/copy call into the existing async pager service.

## Loop 15 - WAL File Handle Async Flag Repair

Problem: `H8BinaryWorldPager` already owns a background worker thread for WAL and page writes, but the WAL append stream was opened with `WriteThrough | SequentialScan` only. The SHINOBU_154 task explicitly requires asynchronous WAL semantics, and the data stream already used `FileOptions.Asynchronous`.
Solution: Added `FileOptions.Asynchronous` to the existing WAL `FileStream` open flags. The queue, worker thread, byte format, lock discipline, and owner remain unchanged.
Rejected Alternatives: Moving entity WAL writes into a new FileStream owned by the compressor was rejected because it would bypass the existing pager/WAL owner and create a second persistence authority. Rewriting the pager to `WriteAsync` was rejected in this lane because it is broader worker-loop surgery and the immediate contract breach is the file-handle flag.
Scalability potential: Low/Middle/High/Ultra all use the same pager; the flag allows OS async file semantics without adding per-tier routes.
Hardware Impact: Main thread remains unaffected. The worker write path keeps write-through durability while no longer opening the WAL handle as a purely synchronous file handle.

## Loop 16 - Dedicated WAL Payload Buffer

Problem: The compressor reused `RleBytes` as the serialized WAL payload after pack. That saved one buffer but created a load-side alias hazard: if a completed page is copied back into `RleBytes`, the decode job reads the WAL source while also writing the decompressed RLE stream into the same array.
Solution: Added `SaveEntityDeltaWalPayloadBytes` (`70357`) and a `WalPayloadBytes` field in `EntityDeltaCompressionVaultBufferSet`. WAL pack/audit/enqueue now use this dedicated Vault buffer. The no-argument decode overload reads `WalPayloadBytes` and writes into `RleBytes`, preserving non-overlapping source and destination buffers.
Rejected Alternatives: In-place LZ4/RLE replay was rejected because overlapping `MemCpy`/LZ4 decode is not a valid safety contract. Allocating a temporary managed `byte[]` was rejected by the zero-GC and Vault laws.
Scalability potential: One extra pre-owned byte buffer is tier-independent; Low through Ultra still scale compression effort continuously.
Hardware Impact: Costs one preallocated staging buffer. It removes a replay corruption class without adding per-frame allocations or main-thread file I/O.

## Loop 17 - Compile Gate Reality

Problem: CPU briefly dropped below the 50 percent gate and no compiler process was listed, but generated Unity `.csproj` files are stale. `Hecton8.Core.csproj` includes `H8BinaryWorldPager.cs` but not new SHINOBU_154 assets such as `EntityDeltaCompressionArchitecture.cs`, so a direct `dotnet build` would report missing types from `BinaryLayoutManifest` for project-generation reasons rather than source truth.
Solution: Do not run `dotnet build` against stale generated projects. Keep static gates current and record that real compile proof requires Unity import/project regeneration or Unity MCP console access.
Rejected Alternatives: Manually editing Unity-generated `.csproj` files was rejected because those files are generated artifacts and would create churn. Running a known false-negative build was rejected because it wastes machine time and pollutes logs.
Scalability potential: No runtime impact; this protects iteration hardware during the multi-agent batch.
Hardware Impact: Avoided a meaningless compile wall and preserved the user's build-protection mandate.

## Loop 18 - Saturating Finalize Counters

Problem: `EntityDeltaFinalizeJob` accumulated block-level `uint` counters with unchecked `+=`. Normal entity counts are far below overflow, but a corrupted counter row or future larger capacity could wrap to a small payload size and make downstream validation reason about false bytes.
Solution: Added `SaturatingAdd(uint,uint)` and applied it to delta, active, tombstone, dense-byte, pruned, and dehydrated aggregate counters. Overflow now clamps high and then saturates to `int.MaxValue` at the counter export boundary.
Rejected Alternatives: Leaving natural wrap was rejected because it creates silent corruption. Promoting all counters to `ulong` was rejected for this patch because the public counter buffer and telemetry fields are currently `int/uint`; saturating aggregation hardens the existing ABI without widening DTOs.
Scalability potential: Low through Ultra share the same overflow-safe math; no quality branch is introduced.
Hardware Impact: Adds a few scalar comparisons in a one-row finalize job per save, not per entity. It prevents bad capacity math from reaching WAL pack.

## Loop 19 - Canonical Endian Record Stream

Problem: Dense entity records were previously moved into the RLE stream with raw DTO `MemCpy`. That is fast on current little-endian Unity targets, but it makes the WAL payload implicitly host-endian and could silently hydrate corrupt coordinates, hashes, or float locals if legacy/network bytes arrive in another byte order.
Solution: `EntityDeltaDensePackJob` now writes each `EntityDeltaDataRecordDTO` field to dense bytes through fixed little-endian offsets. `EntityRlePreconditionJob` marks the stream with `RleStreamFlagLittleEndianRecords`; validation rejects ambiguous streams with no endian marker, both endian markers, or non-whole-record dense byte counts. Extraction and replay hydration reject non-finite local AUP offsets by setting the fatal counter instead of writing corrupt positions. The replay expansion job now hydrates records through explicit little-endian or big-endian readers and writes back through `UnsafeUtility.AsRef` instead of raw record `MemCpy`. `EntityLz4CompressionJob` also rejects zero-length hash tables and clamps active hash slots to the actual Vault hash-table length.
Rejected Alternatives: Keeping host-endian `MemCpy` was rejected because it treats platform byte order as an unstated contract. Converting the outer WAL header only was rejected because payload records are the actual authority data. Adding managed `BinaryReader`/`BinaryWriter` was rejected by the zero-GC and raw-byte mandate.
Scalability potential: Low/Middle/High/Ultra still share one payload format. Quality changes compression effort and write cadence only; endian correctness does not fork by hardware tier.
Hardware Impact: Adds fixed scalar stores/loads per changed entity during save/load worker jobs. Expected i3/MX350 cost is bounded by delta count and buys deterministic replay safety; no main-thread stall, allocation, or extra Vault ownership is introduced.

## Loop 20 - Global Authority Route Card

Problem: The compressor adds/uses a long-lived `GlobalDataVault` and WAL persistence route. A rationale paragraph is not enough under `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`; missing owner/phase/cadence/failure/proof fields would leave the route reviewable only by tribal memory.
Solution: Added `Docs/Tasks/Route_SHINOBU_154_EntityDeltaCompression.md` with route ID `SAVE_ENTITY_DELTA_WAL_RLE_LZ4`, owner, instrument, producer/consumer phase, cadence, quality behavior, buffer IDs, capacity, failure mode, telemetry fields, stale-handle behavior, rejected alternatives, monolith risk, and proof requirements. Review result is deliberately `YELLOW` because Unity/Burst/profiler/WAL replay artifacts are still absent.
Rejected Alternatives: Treating the status checklist as the route card was rejected because it lacks the template's shutdown, stale-handle, and review-result fields. Marking `GREEN` was rejected because the route is runtime-facing and static source is not runtime proof.
Scalability potential: Route card locks one format and one owner across Low/Middle/High/Ultra; quality only changes cadence and compression effort.
Hardware Impact: Documentation-only; no runtime cost. It reduces integration ambiguity for the multi-agent batch.

## Loop 21 - Native Range Alias Guard

Problem: The Burst job fields are marked `[NoAlias]`, but that promise was previously enforced only by distinct Vault buffer IDs and route discipline. A bad integration test or stale Vault resolver could still pass overlapping `NativeArray` views and make the compiler's no-alias assumption false.
Solution: Added stack-only native byte-range overlap guards before save and replay scheduling. `HasCompressionPipelineAliasViolation` covers the current/baseline/delta records, block counters, dense/RLE/compressed/WAL bytes, LZ4 hash table, headers, counters, telemetry, tuning, sector stats, and profiles. `HasWalDecodeAliasViolation` covers WAL source, RLE destination, dense destination, hydrated records, headers, and counters. Alias failure schedules `EntityScheduleFailureJob`, a deterministic Burst job that sets fatal counters/header/stats and returns a tracked `JobHandle` without starting the unsafe pipeline.
Rejected Alternatives: Trusting BufferID uniqueness alone was rejected because `[NoAlias]` is a code-generation contract, not a documentation wish. Blocking with `JobHandle.Complete()` to inspect data was rejected because alias detection needs only pointer ranges and must not serialize the pipeline. Allocating a managed list of ranges was rejected by zero-GC policy; the guard uses fixed `stackalloc` ranges.
Scalability potential: Low/Middle/High/Ultra all use the same guard. It adds a bounded pre-schedule pointer-range scan and does not change compression quality math.
Hardware Impact: O(16^2) cold scheduling comparisons for save and O(6^2) for replay. Expected cost is below profiler noise compared to entity scanning; it prevents undefined Burst vectorization behavior on all CPUs, including ARM64 NEON.

## Loop 23 - Scheduling Profiler Anchors

Problem: The route card still had `Profiler marker: pending`, leaving no named profiler anchor for main-thread scheduling cost even though runtime proof remains unavailable.
Solution: Added static `ProfilerMarker`s around `ScheduleCompressionPipeline` and `ScheduleWalPayloadDecodePipeline`: `H8.Save.EntityDelta.ScheduleCompression` and `H8.Save.EntityDelta.ScheduleDecode`. The markers stay outside Burst jobs and therefore do not introduce managed profiler scopes into worker kernels.
Rejected Alternatives: Adding `ProfilerMarker.Auto()` inside Burst jobs was rejected because those jobs must remain unmanaged mathematical kernels. Leaving route-card profiler fields empty was rejected because acceptance requires named capture points.
Scalability potential: Markers are tier-independent. They expose scheduling cost while quality continues to affect compression effort and cadence.
Hardware Impact: Cold static profiler marker allocation at domain load, stack-scope marker on scheduling calls. No per-entity cost and no gameplay DTO bloat.

## Loop 24 - Alias Range Capacity Clamp

Problem: The pre-schedule alias guard used fixed stack ranges sized exactly to the current save/replay buffer count. That was correct today but brittle: a later buffer addition could write past the stack range before the overlap scan ran.
Solution: Added explicit `RangeCapacity` constants and changed the helper to `TryAddNativeRange`. If a created native range would exceed stack capacity, the guard returns alias violation and schedules the existing fatal failure job instead of silently skipping an unchecked buffer.
Rejected Alternatives: Allocating a managed range list was rejected by zero-GC policy. Silently ignoring overflow was rejected because it would preserve stack safety while weakening the `[NoAlias]` proof.
Scalability potential: Low/Middle/High/Ultra use the same bounded guard; no quality branch is introduced.
Hardware Impact: One integer compare per potential range during scheduling. This is below measurable worker cost and prevents stack overwrite in the compile-wall lane.

## Loop 25 - Unity Compile Wall Attempt

Problem: Static gates cannot prove Burst import, generated asset inclusion, or Unity compiler acceptance. CPU/dotnet gates opened, so the next valid proof path was Unity batchmode, not stale generated `.csproj` build.
Solution: Ran Unity `6000.4.1f1` batchmode with log `Docs/AgentLogs/Unity_SHINOBU_154_Compile.log`. The import log lists `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs`, `EntityDeltaGizmoProbe.cs`, `Editor/EntitySaveTunerWindow.cs`, and `H8BinaryWorldPager.cs` in the script asset set. Compilation then failed on non-SHINOBU domains before a clean project-wide proof could exist.
Rejected Alternatives: Running `dotnet build Hecton8.Core.csproj` was rejected because generated projects still omit new SHINOBU assets. Fixing `Physics/HabitatFluidIncursionJobs.cs`, Narrative, Wreckage, Coral, or `Hecton8.MockDomain.Runtime` from this SaveSystem lane was rejected as domain sabotage.
Scalability potential: No runtime change. The failed proof preserves ownership: SaveSystem remains owner-local while compile-wall owners must fix their own assemblies.
Hardware Impact: Unity script compilation took 40.599537 seconds and exited with return code 1 due external compile errors. No SHINOBU file diagnostic appears in the compile log, but Task 20 cannot be marked green until the dependency wall is removed and Unity/Burst profiler proof is rerun.
