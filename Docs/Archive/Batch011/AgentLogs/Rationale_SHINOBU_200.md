# Rationale_SHINOBU_200

Date: 2026-05-20
Agent: SHINOBU_200
Status: STATIC SOURCE UPDATED - COMPILE BLOCKED BY EXTERNAL CORE DEPENDENCY WALL

## Baseline Decision 00 - Domain And Mandate Scope

Problem: The assignment touches global signal transport, native memory layout, AUP-bearing spatial hashing, telemetry, and Burst job scheduling. Direct edits outside Core/SignalBus would create cross-domain coupling under active multi-agent work.

Solution: Constrain primary edits to Core signal/native transport surfaces unless an existing contract proves a cross-domain interface. Use typed SignalBus lanes, NativeQueue bridge lanes only where already owned, and source scans before adding any surface.

Rejected Alternatives: A new concrete subsystem referenced by combat/audio/AI classes was rejected because it would invent dependencies on concurrent agents. A monolithic global runtime signal was rejected because the Signal Lane Segregation mandate bans catch-all lanes.

Scalability potential: Low uses bounded snapshots and coalescence; Middle keeps full nearby truth; High keeps richer telemetry; Ultra spends saved CPU in VISUAL_SYNC consumers, not unbounded simulation.

Hardware Impact: Expected gain on i3/MX350 is from removing high-contention CAS lanes and avoiding false sharing; no numeric runtime claim until profiler/GCMonitor proof exists.

## Baseline Decision 01 - First-20-Minutes Route

Problem: Core signal congestion does not directly add content but can block the Copper Wire vertical slice by making combat/audio/interaction feedback unstable under stress.

Solution: Treat this as a route-blocker removal for stable first-20-minutes feedback and telemetry, specifically preventing event storms from stalling SIMULATION/POST_SIMULATION.

Rejected Alternatives: Labeling it as visual polish was rejected; this is foundational transport and crash-forensics work.

Scalability potential: Low must degrade event density/coalesce; Middle keeps deterministic event truth; High/Ultra may expose richer x-ray telemetry and presentation overload.

Hardware Impact: Reduces worst-case worker-thread contention on weak silicon; exact microseconds remain PENDING VERIFICATION.

## Decision 02 - 48-Byte Mock Payload Surgery

Problem: `SignalWardenMockDamageSignal`, `MockRockCollisionSignal`, and `MacroCollisionSignal` were 48-byte explicit structs. Contiguous arrays of 48-byte elements straddle 64-byte cache lines, so adjacent worker writes can invalidate the same line.

Solution: Expanded all three to 64 bytes and wired validation through `GlobalSignals.InitializeAllQueues`. Added source thread, batch id, frame, and AUP-cell hash fields inside the new 64-byte envelope.

Rejected Alternatives: Leaving 48-byte payloads and relying on allocator alignment was rejected because allocator alignment does not stop element N+1 from sharing a cache line with element N. Expanding to 128 bytes was rejected for these mock payloads because no field needs that budget.

Scalability potential: Low uses same 64-byte payload with fewer active slice bytes. Middle keeps coalesced truth. High/Ultra can spend saved downstream iteration on richer audio/VFX.

Hardware Impact: Expected i3/MX350 gain comes from eliminating element-boundary false sharing in mock pressure arrays; measured microseconds remain unavailable until compile/runtime proof.

## Decision 03 - Thread-Local Scratchpad Instead Of Global CAS

Problem: A single `NativeQueue<T>.ParallelWriter` serializes high-frequency producers through atomic queue internals. The Core/Signals scan found this directly in `MockRockCollisionAggregationJob`; global project scans found many other lanes, but most are outside SHINOBU_200 domain.

Solution: Removed the Core mock aggregation queue writer and added DataVault-owned front/back thread-local byte scratchpads, 64 per-worker headers, `[NativeSetThreadIndex]`, a deterministic commit job, and a bounded Vault-backed overflow ring (`73053`/`73054`) for saturated slices.

Rejected Alternatives: Rewriting all `SignalBus<T>.ParallelWriter` consumers was rejected because it crosses active domain ownership and would manufacture dependencies on 20+ concurrent agents. A managed `ConcurrentQueue<T>` was rejected by Zero-GC policy.

Scalability potential: Low uses 2 KB slices and more overflow. Middle uses intermediate stride. High/Ultra use larger active strides and coalesce less aggressively only where tuning allows.

Hardware Impact: Removes hot-path atomic cursor reservation for mock contention. Expected savings are highest on ARM64/MX350-class shared-cache systems; no profiler proof yet.

## Decision 04 - Deterministic Serial Commit With Dear Lie Coalescence

Problem: A prefix-copy-only commit would preserve every event and overload downstream presentation/audio consumers. A parallel block copy cannot safely coalesce because output cardinality changes per AUP cell.

Solution: `SignalThreadLocalCommitJob` walks worker slices in deterministic thread order and fuses same-cell mock damage signals by AUP-cell hash. It keeps max damage, normalized combined normal, ORed flags, and committed output count.

Rejected Alternatives: A pure prefix-sum plus `IJobParallelFor` copy was rejected for the coalescing path because it would require an additional deterministic compaction pass. Forwarding all granular events was rejected because it spends performance on invisible detail.

Scalability potential: Low coalesces dense storms early. Middle preserves nearby event density until capacity. High/Ultra can raise slice capacity and output count through Vault tuning.

Hardware Impact: Expected downstream iteration savings exceed the commit cost during dense collision/audio storms; exact commit microseconds are recorded by the editor path but not verified in runtime.

## Decision 05 - Vault Tuning And CSV

Problem: Hardcoded buffer size creates either wasted memory on weak devices or artificial caps on high-end hardware. The task also requires designer/operator control without recompilation.

Solution: Added 64-byte `SignalThreadContentionTuning64` in DataVault, UI Toolkit controls for capacity multiplier, coalescence grid, and quality override, and a zero-row-allocation `signal_corridor_capacities.csv` parser over Vault byte scratch.

Rejected Alternatives: Binary low/high quality branches were rejected. `string.Split`, `int.Parse`, and managed dictionaries were rejected because cold boot parsing still must not create per-row garbage.

Scalability potential: Low uses small active slices under VRAM pressure. Middle expands stride continuously. High/Ultra can push capacity multiplier to preserve more events before overflow.

Hardware Impact: Expected low-end gain is bounded memory traffic and fewer OOM risks. High-end gets more event headroom without changing code.

## Decision 06 - Black Box And Rollback Boundary

Problem: A transient signal corridor must be diagnosable after NaN/overflow but must not become authoritative rollback state.

Solution: Added 300-entry `SignalThreadContentionTelemetryEntry` ring, `Dump_SHINOBU_200.bin`, overflow>5 dump trigger, non-finite/orphan dump trigger, and `ExcludedFromRollbackMerkle` telemetry flag.

Rejected Alternatives: Chat-only reporting was rejected because CTO workflow reads disk logs. Serializing scratchpads into rollback/Merkle state was rejected because signals are re-emitted by authoritative systems during resimulation.

Scalability potential: Low records minimal 64-byte rows. Middle/High/Ultra retain same black-box cost while richer diagnostics consume editor-only paths.

Hardware Impact: Telemetry cost is 64 bytes/frame plus commit counters; expected negligible on i3/MX350, but runtime proof is absent.

## Decision 07 - Flattened Job Containers And O(N) Coalescence

Problem: The first implementation passed Vault `NativeArray` fields through a nested writer-context facade inside `GenerateSignalThreadContentionMockJob`. Unity Job reflection can reject nested NativeContainers before Burst compiles. The first commit coalescence also scanned committed output linearly, creating O(N^2) behavior under dense event storms.

Solution: Flattened `NativeArray<byte>` and `NativeArray<SignalThreadLocalHeader64>` directly onto the mock `IJobParallelFor`. Added Vault buffer `73052` as an uninitialized `int[8192]` coalescence bucket table reset over the active range by the commit job. Same-cell fusion now resolves through hash buckets with deterministic linear probing.

Rejected Alternatives: Keeping the facade inside the scheduled job was rejected as a compile-wall risk. Keeping linear scan was rejected because 4096 output rows can create millions of comparisons during exactly the stress case SHINOBU_200 exists to kill.

Scalability potential: Low quality shrinks active stride and still gets O(N) commit behavior. Middle keeps stable bucket load. High/Ultra use more event headroom before overflow without changing algorithmic class.

Hardware Impact: Expected i3/MX350 gain is reduced commit-side branch/cache churn during dense mock storms. Estimated 200-600 us under 4k dense same-cell events; no profiler proof because build remains CPU-guard blocked.

## Decision 08 - Spatial Heatmap And Release-Only Lifecycle Naming

Problem: The first heatmap visualized per-worker pressure, not committed spatial event density requested by Task 20. The SHINOBU-owned telemetry ring had a method named `Dispose()` although it only dropped Vault handles and did not own backing memory.

Solution: `SignalThreadContentionHeatmapGizmo` now reads `TryGetCommittedSignals` and draws committed AUP-cell density wire cubes in Scene View. `SignalTelemetryRingBuffer.Dispose()` was renamed to `ReleaseHandlesOnly()` and the `GlobalSignals` teardown call was updated.

Rejected Alternatives: Keeping worker bars was rejected because it does not show where gameplay event pressure exists in world space. Keeping a fake `Dispose()` name was rejected because it implies memory ownership outside DataVault.

Scalability potential: Heatmap is editor-only and capped by `maxDrawnCells`. Runtime lifecycle remains handle-only; weak devices pay no gizmo cost.

Hardware Impact: Runtime hot path unchanged by gizmo. Release-only naming avoids accidental foreign manual-dispose patterns; no microsecond claim.

## Decision 09 - Cold Layout Guard And Sector-Origin Commit Contract

Problem: Size-only validation can pass while field offsets drift, which is exactly the ARM64 false-sharing/alignment failure Task 02 exists to prevent. The commit job also carried `SectorOriginAup = double3.zero`, which is harmless only when every producer precomputes `AupCellHash`; it is wrong for external or future payloads that rely on commit-time fallback hashing.

Solution: Added `SignalThreadContentionLayoutGuard` under `UNITY_EDITOR || DEVELOPMENT_BUILD` using `UnsafeUtility.SizeOf<T>()` and `UnsafeUtility.GetFieldOffset(...)` for the six SHINOBU-owned 64-byte DTO rows. It is called during cold bootstrap/layout validation and during first scratchpad initialization only, then skipped for already-initialized accessor paths. Added `ScheduleCommit(uint frame, double3 sectorOriginAup, JobHandle dependency, out JobHandle handle)` and a matching mock overload while preserving the legacy overloads.

Rejected Alternatives: Leaving `ValidateSignalSize<T>` alone was rejected because it cannot detect a reordered 64-byte struct. Running the layout guard on every accessor was rejected because editor heatmap and telemetry reads would pay reflection cost. Creating a new Vault-owned `NativeQueue` for overflow was rejected in this pass because `GlobalDataVault` has no queue primitive; pretending a `NativeQueue` is a plain Vault row would be false ownership.

Scalability potential: Low quality keeps the same minimum stride and hash-cell coalescence, with layout proof preventing cache-line regressions. Middle/High/Ultra can increase payload headroom without changing the sector-origin hash contract.

Hardware Impact: Expected gain is correctness and regression prevention, not a new measured microsecond saving. The patch protects ARM64/i3/MX350 cache-line assumptions by failing cold when offsets drift. Runtime proof remains absent because compile is still blocked by CPU guard.

## Decision 10 - Vault-Backed Overflow Ring Instead Of Shared SignalBus Fallback

Problem: The previous overflow fallback pushed saturated mock payloads into `SignalBus<SignalWardenMockDamageSignal>.ParallelWriter`. That avoided data loss, but it failed Task 11's merge requirement because the payloads bypassed SHINOBU's finalized committed snapshot. Draining the shared typed SignalBus queue inside SHINOBU commit would steal unrelated lane traffic and violate owner-local routing.

Solution: Added two SHINOBU-owned Vault buffers: `73053` for `SignalWardenMockDamageSignal[1024]` and `73054` for `SignalThreadOverflowHeader64[1]`. Normal producers still write to per-thread slices with no shared cursor. Only after slice capacity failure do they reserve an overflow slot through a CAS monotonic write cursor in the 64-byte overflow header. Each overflow slot is published by a per-payload sequence tag, so the commit job never drains a reserved-but-unwritten row. `SignalThreadLocalCommitJob` advances a monotonic read cursor, runs the same AUP-hash coalescence path, records overflow/dropped counts, and leaves concurrently published external rows for the next drain instead of resetting the queue.

Loop 11 refinement: SHINOBU-owned atomic reads now use `Interlocked.CompareExchange(ref value, 0L, 0L)` instead of `Interlocked.Read`. This follows existing deterministic Burst precedent in `InventoryRoutingNetwork.ClearInventoryContainerRangeJob` and avoids depending on a less-proven Burst intrinsic.

Rejected Alternatives: A new persistent private `NativeQueue<T>` was rejected because it would restore manual native ownership and `Dispose()` lifecycle outside the Vault. Pretending Unity `NativeQueue<T>` is "requested from Vault" was rejected because `GlobalDataVault` has no queue primitive. Draining the existing typed `SignalBus<T>` queue was rejected because SHINOBU_200 does not own all producers on that lane.

Scalability potential: Low quality uses smaller thread-local slices and can hit the bounded overflow more often, but overflow is capped and telemetry-visible. Middle stays mostly thread-local. High/Ultra expand slice headroom and preserve more events before the overflow ring is touched.

Hardware Impact: Expected impact is preserving the zero-contention normal path while bounding rare saturation. Overflow still uses an atomic cursor, but only after a thread has exhausted its private cache-line slice. Runtime microsecond proof remains absent because CPU guard blocks compile/profiling.

## Decision 11 - CSV Platform Selection Without Managed Maps

Problem: The capacity CSV parser computed platform hashes but applied every valid row. With the checked-in row order, `rtx4090` could override `quest3`, `steamdeck`, or `mx350`, invalidating continuous scalability tuning on weak devices.

Solution: Parse rows into a local value-type candidate, resolve the current target platform hash from cold `Application.platform`/`SystemInfo` signals, apply only the exact row, and fall back to `pc` if no exact row exists. The parser still operates over `ReadOnlySpan<byte>` and manual integer parsing.

Rejected Alternatives: A managed `Dictionary<string, Row>` and `string.ToLowerInvariant()` were rejected because Task 19 requires zero-row-allocation parsing. Relying on CSV row order was rejected because designers should not have to make the active platform the last line.

Scalability potential: Low/Quest rows now actually select smaller thread-local windows; Middle devices get intermediate stride; RTX rows can still choose maximum lock-free capture without code changes.

Hardware Impact: Corrects cold tuning selection for i3/MX350/Quest-class devices. No runtime microsecond claim; hot path is unchanged after the row is applied.

## Decision 12 - SHINOBU-Owned CSV Scratch And Checked-In Capacity Source

Problem: `SignalThreadContentionCsvHotSwap` had a parser but no checked-in human-readable capacity source in the checkout. It also borrowed the older generic `SignalTuningTable` CSV scratch buffer `73042`, which made the Task 19 H-Phi proof ambiguous. SHINOBU_258 later moved the authoring source out of runtime `StreamingAssets` into `Assets/_SourceData/Signals` to satisfy the Data Monolith text-runtime gate.

Solution: Added Vault buffer `73055` as `SignalThreadContentionCsvScratch byte[8192]`, resolved through `SignalThreadLocalScratchpad.TryOpenCsvScratchForLoad`. The current checked-in authoring source is `Assets/_SourceData/Signals/signal_corridor_capacities.csv` with platform/min/max/output rows and a stable Unity `.meta`; player/runtime builds return false from the CSV loader instead of reading text. Platform label hashing lowercases ASCII bytes before FNV-1a folding without allocating strings.

Rejected Alternatives: Parser-only completion was rejected because a missing data file leaves designers with no tuning bridge. Continuing to share buffer `73042` was rejected because SHINOBU_200 must list every Vault handle it requests at boot and should not hide behind a pre-existing generic scratch lane.

Scalability potential: Low/MX350 rows cap active lock-free stride lower and output fewer committed rows; middle hardware can increase stride; high/ultra rows preserve maximum event detail. The runtime curve remains continuous because CSV values only define endpoints consumed by `math.lerp`/smooth polynomial math.

Hardware Impact: Hot path unchanged. Cold boot now requests one additional 8192-byte Vault byte scratch row. The practical gain is preventing allocation-based CSV fallback or missing-tuning drift; runtime microsecond proof remains absent.

## Decision 13 - Scoped Compile-Wall Proof

Problem: The SHINOBU_200 assignment requires no direct sibling-domain runtime dependency. The touched lane lives inside the existing `Hecton8.Core` assembly, and that pre-existing assembly still lists several sibling runtime references in `Hecton8.Core.asmdef`.

Solution: Do not add any new asmdef reference or sibling-domain source using for SHINOBU_200. Keep the new thread-contention corridor inside the existing Core signal surface and route memory through `GlobalDataVault` buffer IDs `73043..73055`.

Rejected Alternatives: Removing legacy `Hecton8.Core.asmdef` sibling references was rejected in this lane because it is an integrator-level compile-wall migration with high blast radius and no safe compile window. Claiming the whole Core assembly is clean was rejected because the asmdef proves otherwise.

Scalability potential: Low/Middle/High/Ultra scalability remains in the SHINOBU-owned math and buffer sizing path; assembly decoupling debt is documented instead of hidden.

Hardware Impact: No runtime microsecond claim. The gain is avoiding a risky asmdef churn patch while preserving SHINOBU's source-level compile isolation.

## Decision 14 - CSV Exact-Read Guards

Problem: `SignalThreadContentionCsvHotSwap.TryLoad` used one `FileStream.Read(Span<byte>)` over the full Vault scratch span. A short read or oversized file could result in silent prefix parsing and wrong live tuning. The same hazard existed in the neighboring Core signal tuning CSV loader.

Solution: Reject empty files, reject files larger than the available Vault scratch buffer, read until the exact declared byte count is consumed, and fail if the stream returns zero before that count. Keep parsing over `ReadOnlySpan<byte>` after the exact-read gate.

Rejected Alternatives: Continuing with one read was rejected because `FileStream.Read` is not a whole-file contract. `File.ReadAllBytes` was rejected because it allocates managed memory and violates the cold-tuning bridge discipline.

Scalability potential: Low/Middle/High/Ultra rows now only apply when the entire authoring file is available and valid, preventing truncated high-end rows or fallback rows from silently driving device capacity.

Hardware Impact: Hot path unchanged. Cold boot performs at most one bounded loop over an 8192-byte scratch region for SHINOBU contention tuning.

## Decision 15 - Phase-Local Vault Generation Resolves

Problem: `SignalThreadLocalScratchpad` retained private static `NativeArray<T>` fields for buffers `73043..73055`. Those aliases pointed at Vault-owned memory, but the code shape still looked like private persistent array ownership and weakened the H-PHI proof. It also kept obsolete pointer-bearing `VaultBufferHandle<T>` descriptors even though `GlobalDataVault` exposes pointer-free `VaultGenerationHandle<T>`.

Solution: Replace SHINOBU-owned static `NativeArray<T>` aliases with `VaultGenerationHandle<T>` descriptors only. Each public method resolves the exact transient `NativeArray<T>` views it needs through `IDataVault.TryResolveHandle(...)` immediately before scheduling a job, mutating the tuning DTO, reading telemetry, returning an editor snapshot, or loading CSV bytes. If a same-vault resolve fails because a generation changed, initialization now drops the initialized flag and reacquires fresh generation handles instead of staying permanently faulted.

Rejected Alternatives: Keeping static aliases was rejected because it forces reviewers to trust narrative ownership instead of seeing handle-only data sovereignty in source. Keeping `VaultBufferHandle<T>` was rejected because it stores a cached pointer and is marked as a legacy migration bridge by the Vault API.

Scalability potential: Low/Middle/High/Ultra stride behavior is unchanged. The patch improves relocation/compaction tolerance across all hardware because phase-local resolves fail closed if a generation changes instead of preserving stale aliases.

Hardware Impact: Hot path still schedules with concrete `NativeArray<T>` fields inside Burst jobs. The added resolution is outside the inner producer loop, immediately before schedule/readback. Stale-generation reacquire is cold recovery logic only. Runtime microsecond proof remains absent because CPU guard blocks compile/profiling.

## Decision 16 - Hash Boundary NaN Vaccine And Active-Slice Clamp

Problem: The AUP hash path validated signal AUPs at current writer call sites, but did not defend itself from a non-finite caller-provided sector origin. The commit job also bounded worker reads by max stride and cursor, not by the worker header's recorded active stride.

Solution: `SignalThreadLocalAupHash.ComputeCellHash(...)` now rejects non-finite AUP, non-finite sector origin, and overflowed local `float3` casts by returning sentinel hash `1u`. `SignalThreadLocalCommitJob` clamps each worker read to `min(header.ActiveStrideBytes, ThreadStrideBytes)` before payload decoding.

Rejected Alternatives: Relying on every caller to sanitize sector origins was rejected because the hash function is the final bucket gate. Trusting `WriteCursorBytes` alone was rejected because a stale or corrupted header could cause inactive bytes to be decoded after quality/capacity changes.

Scalability potential: Low-quality active slices are now respected by the commit reader; Middle/High/Ultra still use the same continuous stride endpoints without expanding unsafe reads.

Hardware Impact: Correctness and NaN-containment patch only. No runtime microsecond claim until compile/profiler proof exists.

## Decision 17 - No Interface Fallback Dispatch In Frame Paths

Problem: `SignalBusRegistry` still retained an `ISignalLane[]` fallback route and frame loops could call `lane.FlushPreSimulation(...)` and `lane.ClearPostSimulation()` virtually for non-generated lanes. Even if expected fallback count is zero, the code path violated the hot-path interface-array mandate.

Solution: `FlushPreSimulation()` and `ClearPostSimulationSnapshots()` now dispatch only through generated generic direct lane calls. Non-generated lane registration records the lane in the cold registry for telemetry/disposal, marks registration overflow, and emits a development error requesting generated direct dispatch before gameplay use.

Rejected Alternatives: Keeping the virtual fallback for convenience was rejected because it preserves object-oriented dispatch in the frame corridor. Function-pointer fallback was rejected for this pass because Unity/C# function pointer support is a higher compile-risk change than fail-fast direct-dispatch enforcement under a CPU-blocked compile window.

Scalability potential: Low/Middle/High/Ultra lanes all use direct generic dispatch; dynamic unsupported lanes fail visibly instead of silently adding virtual frame work.

Hardware Impact: Removes O(fallback lanes) virtual calls from pre/post simulation frame paths. Measured microseconds remain unavailable because build/profiling is CPU-guard blocked.

## Decision 18 - Exact-Read Priority CSV And Unsafe Length Fences

Problem: The older `SignalPriorityCsvHotSwap` loader could parse a prefix read and did not reject oversized files. The SHINOBU scratchpad unsafe pointer path also relied on requested Vault capacity rather than checking byte buffer length at the writer/commit boundaries.

Solution: Priority CSV loading now rejects empty/oversized files and loops until the declared file length is read. `SignalThreadLocalWriteContext.IsValid`, both mock writer paths, `SignalThreadLocalCommitJob.Execute`, and `ResolveBuffers` now verify byte-buffer capacity before pointer math.

Rejected Alternatives: `File.ReadAllBytes` was rejected because it allocates. Leaving capacity proof only in `GetGenerationHandle(...)` was rejected because unsafe code should fail closed if a resolved buffer is shorter than expected.

Scalability potential: All hardware tiers keep the same continuous capacity curve; weak devices benefit from fail-closed bounds instead of stale/inactive byte decoding during quality downshift.

Hardware Impact: Hot producer writes add a simple length comparison after active-slice checks; expected cost is below measurement noise versus the avoided OOB/corruption failure. Runtime proof remains pending.

## Decision 19 - Read-Only Snapshot Fence And Vault Length Checks

Problem: `SignalThreadContentionHeatmapGizmo` was a consumer of the finalized snapshot but requested the writable `NativeArray<SignalWardenMockDamageSignal>` view. `ResolveBuffers` also compressed every handle and length proof into a single chained boolean expression, making stale/undersized Vault failures harder to isolate.

Solution: Added `TryGetCommittedSignalsReadOnly(...)`, returning `NativeArray<SignalWardenMockDamageSignal>.ReadOnly` from the resolved committed snapshot. The heatmap now consumes that read-only view. `ResolveBuffers` now validates minimum lengths for the SHINOBU Vault buffers before callers schedule unsafe pointer jobs.

Rejected Alternatives: Removing or narrowing the existing writable snapshot accessor was rejected because public API removal during an active batch can break other agents. Keeping `ResolveBuffers` at `IsCreated` checks only was rejected because it does not prove byte capacity before unsafe jobs run.

Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. The read-only fence protects consumer tooling from mutating event pressure evidence while all quality-weight capacity scaling remains in the existing tuning row.

Hardware Impact: No measured runtime microsecond gain. Hot Burst producer/commit paths are unchanged; the new checks are cold/managed boundary validation, and the heatmap is editor-only.

## Decision 20 - UI Toolkit Waterfall Graph Without Per-Refresh Strings

Problem: Task 18 requires a zero-GC waterfall graph, but the tuner window refreshed telemetry through `_metricsLabel.text` with string concatenation and `ToString("X8")` on every editor update. It was a numeric label, not the requested graph.

Solution: Added `TryGetTelemetryReadOnly(...)` over the Vault telemetry ring and replaced the label refresh with `SignalThreadContentionWaterfallGraph`, a UI Toolkit `VisualElement` that draws bars through `Painter2D` from read-only telemetry rows. `OnInspectorUpdate` now only calls `MarkDirtyRepaint()`.

Rejected Alternatives: Keeping the label was rejected because it allocates strings and fails the literal waterfall-graph requirement. Copying telemetry into a managed editor array was rejected because the Vault ring is already the authoritative black box.

Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Editor visibility improves because overflow/dropped pressure turns red while normal written/commit pressure stays in the blue-to-hot continuum.

Hardware Impact: Runtime hot path unchanged. Editor repaint string churn is removed from this file; no measured profiler proof exists because Unity import/profiler is not available and CPU guard blocks compile.

## Decision 21 - Adjacent Core Signal Vault Handle Eviction

Problem: After the SHINOBU scratchpad migration, the same Core signal file still contained adjacent legacy Vault surfaces: `SignalTuningTable` stored static `NativeArray<T>` aliases and `SignalTelemetryRingBuffer` stored obsolete pointer-bearing `VaultBufferHandle<T>` descriptors.

Solution: Migrated buffers `73038..73042` to `VaultGenerationHandle<T>` descriptors. `SignalTuningTable` now resolves profiles/count/CSV scratch views per call and no longer stores static `NativeArray<T>` aliases. `SignalTelemetryRingBuffer` resolves ring/cursor views through `IDataVault.TryResolveHandle(...)`.

Rejected Alternatives: Rewriting unrelated Core/Memory disposers was rejected as outside the SHINOBU lane. Leaving same-file signal legacy handles was rejected because it undermines the DataVault sovereignty proof in the exact subsystem under surgery.

Scalability potential: Runtime quality-weight behavior is unchanged. The patch improves relocation/generation safety across Low/Middle/High/Ultra hardware without adding a new data route.

Hardware Impact: Hot producer/commit jobs are unchanged. Tuning/profile/black-box access pays phase-local resolve calls on cold/report paths only; no profiler measurement exists because CPU guard blocks compile/runtime proof.

## Decision 22 - Closed-Generic Fallback Dispatch Instead Of Blocking Sibling Lanes

Problem: Loop 18 removed `ISignalLane[]` virtual fallback dispatch from frame flush/clear, but a fresh static audit found `230` distinct `SignalBus<T>` references and only `135` Core-generated direct lanes. Blocking every non-generated lane would starve sibling-owned signals such as fabrication, terminal, subtitle, economy, and VFX lanes. Adding those concrete sibling payload types to Core would violate the compile wall.

Solution: Keep generated direct generic calls for Core-known lanes, and add a cold-registered `SignalLaneDispatch[]` operation table for non-generated lanes. Each closed `SignalBus<T>` registers cached static delegates for flush, clear, and telemetry copy. Frame fallback no longer indexes `ISignalLane[]` or calls virtual interface methods; non-generated lanes drain through closed-generic operations while preserving no direct Core reference to sibling runtime types.

Rejected Alternatives: Re-adding `ISignalLane[]` hot fallback was rejected because interface arrays in frame loops violate the IL2CPP devirtualization mandate. Expanding the Core direct list with sibling-domain payload types was rejected because it would add forbidden compile-wall references. Hard-blocking non-generated lanes was rejected because it silently breaks existing typed SignalBus traffic outside Core.

Scalability potential: Low/Middle/High/Ultra tiers keep the generated direct path for the 135 Core lanes. Fallback work is proportional only to registered non-generated lanes and remains a typed operation table, not an object-oriented catch-all bus. Sibling domains can later generate their own direct dispatch without Core learning their concrete payload types.

Hardware Impact: Expected impact is removal of per-frame virtual interface dispatch while avoiding signal starvation. Measured runtime microseconds remain unavailable because CPU guard still blocks compile/profiler proof.

## Decision 23 - Telemetry Sampler Must Not Rehydrate Interface Properties

Problem: `ISignalLane` was reduced to cold disposal only, but `GlobalSignals.ReportSignalLaneTelemetry()` still attempted to call `SignalBusRegistry.GetLaneAt(...)` and read per-lane properties through the interface. Static source showed `GetLaneAt` had no registry definition and the interface no longer exposed those properties, so this was both a compile-risk and a relapse into object-oriented lane sampling.

Solution: Rewire `ReportSignalLaneTelemetry()` to use `SignalBusRegistry.TryCopyTelemetryAt(...)`, which invokes the already-registered closed-generic telemetry delegate for each lane. `SignalBus<T>.CopyTelemetryStatic(...)` now writes `_pushedLastFlush` and `_corruptedSignalTotal` into `SignalLaneTelemetry.Reserved2` as a stable 64-bit packed value: low 32 bits are pushed-last-flush, high 32 bits are corrupted-total. Existing public telemetry field offsets and total size stay unchanged.

Rejected Alternatives: Re-expanding `ISignalLane` with diagnostic properties was rejected because it restores virtual property reads in the signal telemetry path. Increasing `SignalLaneTelemetry` beyond 32 bytes was rejected because it changes a public NativeArray/DataVault telemetry stride. Adding a second registry table just for pushed/corrupted counters was rejected because the current telemetry delegate already carries a reserved 64-bit lane.

Scalability potential: Low/Middle/High/Ultra lanes use the same direct/closed-generic telemetry route. Weak devices avoid per-lane virtual property dispatch during telemetry reporting; high tiers retain exact pushed/corrupted counters for richer diagnostics without widening the telemetry row.

Hardware Impact: Static effect is removal of O(laneCount) interface-property calls and a compile-risk in `ReportSignalLaneTelemetry()`. Runtime microsecond proof remains absent because the CPU build guard still reports `CPU=100`, `dotnet=0`, `csc=0`.

## Decision 24 - Third-Party Dispose Is A Boundary, Not SHINOBU Surgery

Problem: A repository scan for vendor/package `.Dispose()` calls found call sites in Easy Save 3, DOTweenPro, Crest, and Unity ShaderGraph under `Assets/Plugins` and `Packages`. The user asked for manual Dispose removal, but these are third-party/vendor-owned code paths.

Solution: Do not mutate third-party packages from the SHINOBU_200 signal-contention lane. Record the scan result and keep SHINOBU-owned Vault cleanup named `ReleaseHandlesOnly()` where the code only drops DataVault handles. Core `SignalBus<T>.Dispose()` remains the native queue owner lifecycle surface inside Core.

Rejected Alternatives: Editing Easy Save, DOTween, Crest, or ShaderGraph source was rejected because `AGENTS.md` forbids third-party asset mutation without an explicit cleanup task. Removing Core native queue shutdown was rejected because it would leak owner-owned queues and is not a third-party dispose call.

Scalability potential: No quality-tier behavior changes. This preserves package integrity while keeping Core signal memory ownership explicit.

Hardware Impact: No runtime microsecond claim. This is a boundary/provenance decision, not an optimization patch.

## Decision 25 - No Managed Lane Adapter Objects In The Signal Registry

Problem: After the frame and telemetry routes moved to generated direct calls and closed-generic delegates, `SignalBusRegistry` still retained an `ISignalLane` interface and one `SignalLaneAdapter` managed object per closed `SignalBus<T>`. The adapter only forwarded cold disposal, but its existence kept an object-oriented registry shape in the signal corridor.

Solution: Delete `ISignalLane` and `SignalLaneAdapter`. `SignalBusRegistry` now stores `SignalLaneDisposeDelegate[]` for cold teardown, alongside the existing closed-generic flush, clear, and telemetry delegates. `SignalBus<T>` registers a cached static dispose delegate instead of a managed adapter instance.

Rejected Alternatives: Keeping the adapter as "cold only" was rejected because the batch mandate is to eliminate the object-oriented lane spine, not merely keep it out of the tightest loops. Using runtime type handles as registry identity was rejected because it would add more managed metadata handling than the static delegate identity already requires.

Scalability potential: Low/Middle/High/Ultra runtime signal dispatch stays on the same generated direct plus closed-generic fallback route. The patch removes one cold managed adapter object per lane and makes the route shape easier to audit for IL2CPP devirtualization.

Hardware Impact: Hot-path measured savings remain `0 us` until profiler proof. Static effect is removal of the last interface/adapter object from the Core SignalBus registry; current source scans show `ISignalLane=0`, `SignalLaneAdapter=0`, `_lanes=0`, and `GetLaneAt=0`.

## Decision 26 - Corrupted-Only Lane Telemetry Must Not Disappear

Problem: After dropped and corrupted counters were separated, a lane with corrupted payloads but no snapshot rows and no dropped rows could bypass per-lane crash telemetry because the reporting gate only considered snapshot and dropped counts.

Solution: Preserve the 32-byte `SignalLaneTelemetry` ABI by keeping corrupted-total in `Reserved2` high32 and marking corrupted lanes through `Flags` bit `16`. `ReportSignalLaneTelemetry()` treats `corruptedCount > 0` as a critical reporting condition, sends a saturated dropped-plus-corrupted count to `CrashTelemetryBuffer.ReportSignalLaneStats(...)`, and keeps exact dropped/corrupted totals separate for the 300-frame signal telemetry ring.

Rejected Alternatives: Folding corrupted payloads back into `DroppedCount` was rejected because it hides the difference between capacity loss and payload corruption. Expanding `SignalLaneTelemetry` with a new public field was rejected because it changes a DataVault/NativeArray stride used by diagnostics. Reintroducing interface diagnostic properties was rejected because it restores the object dispatch path already removed from SignalBus.

Scalability potential: Low/Middle/High/Ultra tiers keep identical telemetry semantics. Weak devices do not pay a new allocation or widened row; high tiers retain exact forensic counters for black-box inspection.

Hardware Impact: Runtime microsecond proof remains absent. Static effect is forensic correctness without widening the telemetry row or reintroducing interface dispatch.

## Decision 27 - Do Not Repair External Core Compile Wall From Signal Lane

Problem: A focused `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1` was finally allowed by the build guard and failed with 75 errors. The failures are broad missing-domain symbols and assembly-boundary issues outside SHINOBU_200 ownership: `Hecton8.Equipment`, `Hecton8.Logistics.Grid`, `SoundEmissionSignal`, `SocketDefinitionDTO`, docking/world/audio bridge interfaces, `WfcOutpost*`, and related symbols.

Solution: Treat the compile as a dependency wall, not a SHINOBU signal regression. Keep the SHINOBU static gates as the local proof: touched source braces are balanced, owned forbidden-pattern scans are clean, registry interface/adapter residue is zero, and no build error points at `GlobalSignals.cs` or `SignalWardenRuntime.cs`.

Rejected Alternatives: Editing Gameplay, Power, Construction, Audio, World, or Equipment symbols from this lane was rejected because it violates domain ownership and would create cross-agent churn. Re-running the same Core build was rejected because it would reproduce the same dependency wall and waste CPU.

Scalability potential: No Low/Middle/High/Ultra behavior change. This is compile-wall triage only.

Hardware Impact: No runtime microsecond claim. The build probe consumed about 30 seconds wall-clock and produced dependency evidence; no profiler/runtime proof is available.

## Decision 28 - NativeDisableParallelForRestriction Requires Source-Local Proof

Problem: SHINOBU Burst paths used `NativeDisableParallelForRestriction` on worker byte slices, per-thread headers, overflow payloads, and overflow control rows. The code shape was correct, but the native-memory mandate requires a three-paragraph safety justification immediately at the suppressed field, not only in external logs.

Solution: Add source-local `SAFETY_JUSTIFICATION_PARAGRAPH_1/2/3` blocks above every SHINOBU `NativeDisableParallelForRestriction` field. Each proof names why Unity's array-wide aliasing concern is a false positive, which alternatives were rejected, and the invariant: one `[NativeSetThreadIndex]` producer writes one cache-line-aligned row/slice, overflow slots are CAS-reserved and sequence-published, and commit reads only after the producer dependency.

Rejected Alternatives: Removing `NativeDisableParallelForRestriction` was rejected because Unity's safety system cannot model the fixed worker-slice partition and would block the intended job shape. Moving the proof only into Rationale or the route card was rejected because the mandate requires the proof next to the unsafe source declaration.

Scalability potential: Low/Middle/High/Ultra behavior is unchanged. This closes review risk without adding runtime work or widening any DTO.

Hardware Impact: No runtime microsecond change. The value is safety-audit hardening for the ARM64 no-false-sharing route.

## Decision 29 - Legacy Publish Alias Must Not Double-Enqueue

Problem: `GlobalSignals.CreateQueue(ref legacyQueue, ...)` configures a closed `SignalBus<T>` lane and stores a copy of that same native queue handle in the legacy field. Several `Publish(...)` overloads then executed `_legacyQueue.Enqueue(payload)` and `SignalBus<T>.Push(payload)`, meaning one gameplay fact could reserve two nodes in the same MPSC queue and appear twice in the next frame snapshot. Other legacy-only publish overloads bypassed `SignalBus<T>.Push(...)`, so they skipped the shared finite-guard, load-shed, and telemetry path even though `TryDequeue*` already reads from `SignalBus<T>.TryReadFrame(...)`.

Solution: Remove direct legacy alias enqueues from `GlobalSignals.Publish(...)` and route every legacy payload through `SignalBus<T>.Push(...)`. Repoint legacy `NativeQueue<T>.ParallelWriter` wrapper properties to `SignalBus<T>.ParallelWriter` so there is one canonical writer access path; public signatures remain unchanged for cross-agent compatibility. Delete the unused private legacy `PrewarmQueue<T>(ref NativeQueue<T>, int)` helper because it preserved a dead direct-enqueue pattern after the facade stopped using alias queues for publish. The old `NativeQueue<T>` fields remain only as compatibility handles for initialization/disposal method shapes and `TryDequeue*` signatures, not as a second publish route.

Rejected Alternatives: Deleting all legacy queue fields and writer properties was rejected because external callers still depend on those public symbols during this batch. Keeping duplicate enqueues was rejected because it doubles MPSC contention and corrupts event cardinality. Replacing public `NativeQueue<T>.ParallelWriter` return types with a new SHINOBU writer was rejected because that is a public API break and would require coordinated producer rewrites outside this lane.

Scalability potential: Low-tier devices remove one redundant queue reservation and payload copy per affected main-thread publish, which matters most under CPU pressure. Middle tiers get cleaner telemetry and less accidental snapshot inflation. High/Ultra retain the same typed SignalBus surface while downstream systems can spend preserved headroom on richer audio/VFX signal interpretation.

Hardware Impact: Measured proof is absent. Static effect is removal of direct legacy `_...Signals.Enqueue(...)` calls and alias-field `.AsParallelWriter()` calls; the only remaining queue enqueue/writer operation is the canonical `SignalBus<T>` path. Expected benefit on i3/MX350/ARM64-class hardware is lower queue-node churn and fewer redundant MPSC atomic reservations on affected legacy publish lanes.

## Decision 30 - Do Not Chase External Core Compile Wall After Alias Patch

Problem: Loop 29 changed C# behavior, so a focused compile probe was warranted when the CPU guard opened. The probe failed with `76` errors, but the diagnostics again target broad dependency gaps outside SHINOBU_200 ownership: missing `Hecton8.Equipment`, `Hecton8.Logistics.Grid`, `SoundEmissionSignal`, `SocketDefinitionDTO`, docking/world/audio bridge interfaces, `WfcOutpost*`, `VRAMMonitor`, `H8BinaryWorldPager`, and related symbols. No diagnostic names `GlobalSignals.cs` or `SignalWardenRuntime.cs`.

Solution: Record the compile failure as the same external Core dependency wall and stop after one focused attempt. Keep SHINOBU verification at static source gates plus the absence of owned-file diagnostics in the failed compile output.

Rejected Alternatives: Fixing Gameplay, Power, Construction, Audio, World, or Save symbols from the signal-contention lane was rejected as cross-domain churn. Retrying the same build twice more was rejected because the error class is not transient and command discipline forbids compile spam.

Scalability potential: No Low/Middle/High/Ultra behavior change. This decision protects production velocity while keeping the SignalBus patch bounded to Core signal routing.

Hardware Impact: No runtime microsecond claim. The build probe cost `16.44 s` and produced dependency-wall evidence; profiler/runtime proof remains blocked until the broader Core compile wall is cleared.

## Decision 31 - Read-Looking Signal Accessors Must Be Pure

Problem: `SignalBus<T>.SnapshotCount`, `SnapshotGeneration`, `GetFrameSnapshot()`, and `GetFrameSnapshotArray()` called a private `TryResolveFrameSnapshot(...)` helper that could refresh a Vault generation handle and clamp `_frameSnapshotCount`. The public `TryReadFrame(out T)` name was worse: it advanced `_legacyReadCursor`, so callers were consuming data through a read-looking API. This violated the new global systems doctrine that `Get*`, `TryGet*`, `Resolve*`, and `Read*` accessors must not mutate global state.

Solution: Split the route. Pure snapshot readers now call `TryReadFrameSnapshot(...)`, which only resolves the already cached Vault generation handle, clamps count into a local variable, and returns a read-only span/view without writing global lane state. Destructive legacy iteration is now named `TryConsumeFrame(...)`; the `GlobalSignals.TryDequeue*` bridge and the few direct consumers in Core determinism, camera juice, terminal command, save chunk dehydration, and atmosphere base-transition handling were repointed to that explicit consume API. Mutating cold helpers were renamed to `OpenQueueForLegacyGlobalSignals()`, `TryOpenFrameSnapshotForOwnerWrite(...)`, and `TryFindFrameSnapshotVaultForBootstrap(...)`.

Rejected Alternatives: Keeping a compatibility shim named `TryReadFrame` was rejected because the doctrine violation would remain in source and future consumers would copy the wrong API. Changing consumer behavior to non-destructive reads was rejected because existing `while(TryDequeue...)` loops intentionally drain the per-frame cursor. Removing the legacy dequeue bridge entirely was rejected because cross-domain consumers still depend on it during this batch.

Scalability potential: Low-tier devices benefit from clearer cold/hot boundaries because read paths no longer attempt handle refresh or counter repair. Middle tiers keep the same deterministic snapshot semantics. High/Ultra tiers keep the same frame snapshot data, but tooling and consumers can distinguish pure inspection from destructive consumption when feeding richer downstream VFX/audio interpretation.

Hardware Impact: Measured runtime proof is absent. Static effect is removal of hidden mutation from read-looking snapshot APIs and elimination of `SignalBus<...>.TryReadFrame` call sites in the changed source. No DTO layout, BufferID, save identity, or authority route changed.

## Decision 32 - Scratchpad TryGet APIs Cannot Bootstrap The Vault

Problem: SHINOBU's own `SignalThreadLocalScratchpad` read facades still called `EnsureInitializedFromRegistry()`. That helper can initialize Vault generation handles and use `GlobalDataVault.TryGetLatestCreated()` if the registry slot is absent. This is acceptable only on explicit owner/writer/bootstrap/crash paths; it is not acceptable behind `TryGetLatestTelemetry`, `TryGetTelemetryReadOnly`, `TryGetTuning`, `TryGetCsvScratch`, `TryGetThreadHeader`, or read-only committed snapshot access.

Solution: Added `IsInitializedForRead()` and `TryReadCommittedSignals(...)` as non-mutating cached-handle gates. The public `TryGet*` facades now fail closed when the scratchpad has not been initialized instead of bootstrapping the Vault. The writable committed-snapshot accessor was renamed from `TryGetCommittedSignals(...)` to `TryOpenCommittedSignalsForOwner(...)` so a mutable owner surface does not masquerade as a pure read.

Rejected Alternatives: Keeping `EnsureInitializedFromRegistry()` in editor/diagnostic `TryGet*` methods was rejected because it hides global authority mutation in read-looking APIs. Removing initialization from writer and scheduler paths was rejected because those paths explicitly own setup and would fail producer routes unnecessarily. Returning default fake telemetry was rejected because the black-box surface must show absence of initialized truth, not fabricate data.

Scalability potential: Low-tier devices avoid surprise cold Vault acquisition when editor/diagnostic reads touch the lane. Middle tiers get deterministic failure when the owner phase has not initialized. High/Ultra tiers keep the same telemetry and tuning data once the owner route has initialized, with clearer separation between inspection and mutation.

Hardware Impact: Measured runtime proof is absent. Static effect is removal of hidden GlobalRegistry/Vault bootstrap from SHINOBU `TryGet*` read facades while leaving writer/scheduler/mutation paths intact.

## Decision 33 - Telemetry Ring Reads Cannot Initialize The Vault

Problem: `SignalTelemetryRingBuffer.CopyFrames(...)` is a diagnostic copy/read surface, but it used a private `TryResolveRing(...)` helper that could fall through to `GlobalRegistry.DataVault` and call `Initialize()`. The same source also exposed mutable CSV file scratch buffers as `TryGetCsvScratch(...)`, despite those buffers being written by cold file loaders.

Solution: Split telemetry ring access by authority. `CopyFrames(...)` now calls `TryReadRing(...)`, which fails closed unless cached Vault handles are already initialized. `ReportFrame(...)` uses `TryOpenRingForOwnerWrite(...)`, and crash dump uses `TryOpenRingForCrashDump(...)`. Mutable CSV scratch buffers in `SignalTuningTable` and `SignalThreadLocalScratchpad` were renamed to `TryOpenCsvScratchForLoad(...)`, and the two span-based CSV loaders were repointed.

Rejected Alternatives: Keeping `CopyFrames(...)` on a helper that initializes through `GlobalRegistry.DataVault` was rejected because diagnostic reads must not mutate global state. Keeping `TryGetCsvScratch(...)` was rejected because returning writable `NativeArray<byte>` under a `TryGet*` name violates the read-accessor doctrine. Moving scratch storage to local arrays was rejected because the CSV loaders are already cold, Vault-owned, and zero-GC through `ReadOnlySpan<byte>`.

Scalability potential: Low-tier devices avoid surprise Vault bootstrap when editor diagnostics poll signal telemetry. Middle tiers keep deterministic failure when the owner phase has not initialized. High/Ultra tiers preserve the same 300-frame black-box data and the same hot-reload CSV bridge after explicit owner initialization.

Hardware Impact: Measured runtime proof is absent. Static effect is removal of hidden initialization from a diagnostic copy surface and removal of mutable `TryGetCsvScratch` names from SHINOBU-owned signal source. No BufferID, DTO layout, save identity, or authority route changed.

## Decision 34 - Producer Writer Acquisition Must Use An Open Verb In Owned Routes

Problem: `SignalBus<T>.ParallelWriter` is a property surface that can cold-initialize a lane and returns a mutable `NativeQueue<T>.ParallelWriter`. It is a producer/open operation, not a pure read. SHINOBU-owned `GlobalSignals.*SignalWriter` facades and already-touched Core/Terminal bridge producers still routed through that property, which preserved behavior but hid the authority transition behind property syntax.

Solution: Added `SignalBus<T>.OpenParallelWriter()` and repointed all `GlobalSignals.*SignalWriter` facades plus `MemorySentinelRuntime` and `TerminalOsRuntime` direct bridge producers to it. The legacy `ParallelWriter` property remains as a compatibility facade that delegates to `OpenParallelWriter()` so sibling domains outside SHINOBU_200 are not broken by a broad API removal.

Rejected Alternatives: Removing `ParallelWriter` globally was rejected because static scan shows many sibling-domain producers still reference it, and changing all of them would expand beyond the SHINOBU signal-core lane. Leaving owned code on the property was rejected because producer acquisition should advertise that it can open or initialize lane infrastructure. Making `ParallelWriter` fail closed without initialization was rejected because that would silently break existing producers without a full compile/profiler pass.

Scalability potential: Low-tier devices get clearer cold/open boundaries for writer acquisition without extra work in the producer hot path. Middle tiers keep the same explicit typed lanes. High/Ultra tiers retain the same writer capacity and snapshot flow while letting future high-frequency producers move to thread-local scratchpad APIs without mistaking the compatibility property for the preferred route.

Hardware Impact: Measured runtime proof is absent. Static effect is removal of direct `SignalBus<...>.ParallelWriter` call sites from SHINOBU-owned `GlobalSignals` facades and already-touched Core/Terminal bridge files. External-domain property call sites remain as compatibility debt; no DTO layout, BufferID, save identity, or authority route changed.

## Decision 35 - Producer Routes Cannot Bootstrap From Latest Created Vault

Problem: `SignalThreadLocalScratchpad.EnsureInitializedFromRegistry()` was no longer used by read facades, but producer/scheduler/mutation paths still used it. That helper could fall back to `GlobalDataVault.TryGetLatestCreated()` if cached `_vault` and `GlobalRegistry.DataVault` were missing. This is acceptable for crash diagnostics, not for normal producer routes that should prove owner initialization.

Solution: Split initialization by authority. `TryAcquireWriteContext`, `ScheduleCommit`, `TryPushAsynchronousOverflow`, `ScheduleOrphanedLockAutopsy`, `RecordLastCommitMicroseconds`, and `MutateTuning` now use `EnsureInitializedForOwnerRoute()`, which only uses cached `_vault` from explicit initialization and fails closed if absent. `DumpToDisk()` uses `EnsureInitializedForCrashDumpRoute()`, the only SHINOBU contention route still allowed to consult `GlobalRegistry.DataVault` and `GlobalDataVault.TryGetLatestCreated()`.

Rejected Alternatives: Keeping the latest-created fallback in producer routes was rejected because it can hide missing dependency injection and bind to the wrong Vault under tests or editor tooling. Removing crash fallback was rejected because black-box dump recovery is specifically allowed for crash/diagnostic routes. Adding `[Obsolete]` to every legacy writer property was rejected for this pass because repo-wide sibling-domain callers could turn warnings into a new compile wall.

Scalability potential: Low-tier devices avoid surprise Vault bootstrap work on producer paths. Middle tiers keep deterministic fail-closed behavior when owner initialization is missing. High/Ultra tiers keep the same Vault-backed buffers and coalescence path after explicit initialization; no quality-dependent truth route changes.

Hardware Impact: Measured runtime proof is absent. Static effect is containment of `GlobalDataVault.TryGetLatestCreated()` to `EnsureInitializedForCrashDumpRoute()` and addition of the generic `GlobalSignals.OpenSignalWriterForProducerPhase<TSignal>()` maintained producer API. No DTO layout, BufferID, save identity, or authority route changed.

## Decision 36 - Generic SignalBus Bootstrap Must Fail Closed Without Owner Vault

Problem: `SignalBus<T>.EnsureInitialized()` could reach `TryFindFrameSnapshotVaultForBootstrap(...)`, and that helper used `GlobalDataVault.TryGetLatestCreated()` if `GlobalRegistry.DataVault` was absent. Even though the helper was named bootstrap, `EnsureInitialized()` is reachable from producer APIs such as `TryPush` and writer opening, so the fallback could bind normal runtime traffic to a diagnostic/latest-created Vault. `SignalTelemetryRingBuffer.ReportFrame(...)` also had a registry fallback in the owner write path.

Solution: `TryFindFrameSnapshotVaultForBootstrap(...)` now accepts only `GlobalRegistry.DataVault` and fails closed when owner injection is absent. `SignalTelemetryRingBuffer.TryOpenRingForOwnerWrite(...)` now uses only cached `_vault` and `_initialized`; cold initialization must happen through `SignalTelemetryRingBuffer.Initialize()`, not the per-frame report path.

Rejected Alternatives: Documenting the latest-created fallback as bootstrap was rejected because the call chain is producer-reachable. Reinitializing the telemetry ring during `ReportFrame(...)` was rejected because it hides registry polling behind a frame-owned write path. Removing the SHINOBU contention crash fallback was rejected because crash dump recovery remains a legitimate diagnostic route.

Scalability potential: Low-tier devices avoid surprise dependency lookup and possible Vault acquisition from signal pushes or telemetry frame writes. Middle tiers keep deterministic fail-closed behavior when owner boot did not wire the Vault. High/Ultra tiers preserve all same snapshot, coalescence, and telemetry capacity after explicit initialization.

Hardware Impact: Measured runtime proof is absent. Static effect is removal of `GlobalDataVault.TryGetLatestCreated()` from generic SignalBus snapshot bootstrap and removal of `GlobalRegistry.DataVault` fallback from per-frame signal telemetry owner writes. No DTO layout, BufferID, save identity, or authority route changed.

## Decision 37 - Already-Touched Terminal Bridge Cannot Use Latest-Created Vault

Problem: The local post-patch scan included `TerminalOsRuntime.cs` because SHINOBU_200 had already repointed its SignalBus producers to `OpenParallelWriter()`. That file still initialized UI native buffers through `GlobalDataVault.TryGetLatestCreated()`. It is not SHINOBU signal ownership, but leaving a normal runtime latest-created fallback inside an already-touched bridge file would contradict the same global authority rule being enforced on the Core signal route.

Solution: Replace the TerminalOS native-resource fallback with cached `_vault` or owner-published `GlobalRegistry.DataVault`. Missing registry injection now fails closed with the existing `FaultVaultUnavailable` path; the UI bridge no longer binds itself to an arbitrary latest-created Vault.

Rejected Alternatives: Broadly scanning and fixing every repo-wide `TryGetLatestCreated()` caller was rejected because it would cross many active sibling domains. Ignoring the TerminalOS hit was rejected because the file is already in the SHINOBU writer-route patch and the fix is a narrow authority containment. Adding a new UI DataVault service route was rejected because that belongs to the TerminalOS owner, not the Core SignalBus MPSC lane.

Scalability potential: Low-tier devices avoid accidental native-buffer allocation against the wrong Vault during UI initialization. Middle tiers retain the same TerminalOS buffer capacities and signal lanes after proper owner injection. High/Ultra tiers keep the same presentation fidelity; no quality-dependent truth, layout, save identity, or authority owner changes.

Hardware Impact: Measured runtime proof is absent. Static effect is that the four touched source files now contain `GlobalDataVault.TryGetLatestCreated()` only in `SignalWardenRuntime.EnsureInitializedForCrashDumpRoute()`, the documented crash/diagnostic exception. The latest build guard remains `CPU=100`, `dotnet=0`, `csc=0`, so no compile was launched.

## Decision 38 - Subagent Read-Accessor Findings Require Narrow Source-Contract Fixes

Problem: The read-only sidecar audit found three remaining source-contract issues in files already touched by the SHINOBU producer-route patch. `MemorySentinelRuntime.TryGetTunerSnapshot(...)` was read-looking but could call `EnsureVaultBuffers(...)` and `ResolveRuntimeState(...)`, which can acquire Vault buffers and write default runtime state. `MemorySentinelRuntime.TryResolveOrAcquire(...)` and `TerminalOsRuntime.ResolveNativeBuffer(...)` were private helpers with read-looking names that acquire/open Vault buffers. `TerminalOsRuntime.GetTerminalStateRef(...)` returned a mutable ref and set fault state on failure.

Solution: Keep the public tuner snapshot API name but make the implementation read-only: it now requires existing handles and reads the runtime DTO locally without calling buffer acquisition or default-state mutation. Rename the private acquisition helpers to `OpenOrAcquireVaultBuffer(...)` and `OpenNativeBufferForOwner(...)`. Rename the mutable terminal ref surface to `OpenTerminalStateRefForOwner(...)` and `OpenTerminalStateRefForOwnerUnchecked(...)`.

Rejected Alternatives: Broadly editing every repo-wide `TryResolveOrAcquire*` pattern was rejected because those helpers belong to other active domains. Renaming `TryGetTunerSnapshot(...)` was rejected because the editor tuner already depends on that public API and the source violation was solved by making it pure. Keeping `GetTerminalStateRef(...)` was rejected because mutable ref access plus fault mutation violates the read-accessor doctrine even if no external caller was found.

Scalability potential: Low-tier devices avoid surprise Vault allocation or runtime-state repair when an editor tuner reads MemorySentinel state. Middle tiers get clearer setup versus read boundaries. High/Ultra tiers preserve the same diagnostics and UI state access after owner initialization; no quality-dependent truth or authority route changes.

Hardware Impact: Measured runtime proof is absent. Static effect is removal of the specific subagent-reported read-accessor violations in the touched Core/Terminal files. No BufferID, DTO layout, SignalBus payload stride, save identity, rollback exclusion, or gameplay truth owner changed.

## Decision 39 - Mutating Resolve Names In Adjacent Proof Files Must Be Evicted

Problem: A follow-up source scan found more private `Resolve*` methods in already-touched bridge files that were not pure. `TerminalOsRuntime.ResolveAttentionCamera(...)` mutates `_attentionCameraCache` and `_nextCameraResolveFrame`; `ResolveComputeKernel(...)` discovers a compute kernel and writes `_blitKernel`, `_threadsX`, and `_threadsY`; `ResolveGazeInput(...)` mutates `_inputPressedLastFrame`; `ResolveTerminalStatePointer(...)` returns a mutable state pointer. `MemorySentinelRuntime.ResolveRuntimeState(...)` writes default runtime state and pending mod mask into Vault storage, and `ResolveTargets(...)` mutates target rows and `_targetCount`.

Solution: Rename only private call sites in files already touched by the SHINOBU signal-route proof. TerminalOS now uses `RefreshAttentionCameraForOwner(...)`, `TryCaptureCameraFrameForOwner(...)`, `EnsureComputeKernelForOwner(...)`, `CaptureGazeInputForOwner(...)`, `CaptureGazePoseForOwner(...)`, `SelectStateBuffer(...)`, and `OpenTerminalStatePointerForOwner(...)`. MemorySentinel now uses `OpenRuntimeStateForOwner(...)`, `RefreshTargetsForOwner(...)`, and `FindValidationRulesCsvPathCold(...)`.

Rejected Alternatives: Broadly renaming every repo-wide `Resolve*` method was rejected as cross-domain churn. Leaving the mutating methods unchanged because they are private was rejected because the global read-accessor doctrine applies to source contracts, not just public APIs. Changing behavior was rejected; this pass preserves the owner route and only makes mutating/open/cold intent explicit.

Scalability potential: Low-tier devices gain clearer owner/cold boundaries without runtime behavior changes. Middle tiers keep the same cadence and bridge data flow. High/Ultra tiers preserve visual/UI diagnostics and MemorySentinel telemetry while future audits can distinguish pure calculations from owner mutation and cold discovery.

Hardware Impact: Measured runtime proof is absent. Static effect is removal of stale private mutating `Resolve*` names from the touched TerminalOS/MemorySentinel proof set. No BufferID, DTO layout, SignalBus payload stride, save identity, rollback exclusion, authority owner, or quality curve changed.

## Decision 40 - Snapshot Mutation Must Use Owner-Open Semantics

Problem: `SignalBus<T>.TransformSnapshot(...)` and `SignalBus<T>.FilterSnapshot(...)` mutate frame snapshot rows in-place, but they still obtained the backing `NativeArray<T>` through `TryReadFrameSnapshot(...)`. The helper itself was pure, yet using it in a write path kept a source-contract ambiguity directly against the global read-accessor doctrine.

Solution: Repoint the two mutating methods to `TryOpenFrameSnapshotForOwnerWrite(...)`. The owner-open helper resolves the cached generation handle, clamps `_frameSnapshotCount` to the array length, and explicitly represents writable owner-phase access. Pure consumers keep `TryReadFrameSnapshot(...)`; destructive cursor consumption remains explicitly named `TryConsumeFrame(...)`.

Rejected Alternatives: Renaming public `TransformSnapshot(...)` and `FilterSnapshot(...)` was rejected because their names already describe mutation and external callers may exist. Deleting the methods was rejected because generated Core lane filters depend on them. Leaving the helper call unchanged was rejected because future audits would keep flagging a write operation routed through a read helper.

Scalability potential: Low-tier devices keep the same bounded snapshot and coalescence path. Middle tiers keep deterministic frame snapshot mutation semantics. High/Ultra tiers preserve richer signal snapshots and visual-sync consumers without changing gameplay truth ownership, DTO layout, or authority route.

Hardware Impact: Measured runtime proof is absent. Static effect is source-contract hardening only; no BufferID, payload stride, save identity, rollback exclusion, quality curve, or hot-path algorithm changed.

## Decision 41 - Editor Blocking Benchmark Must Be Named As Blocking

Problem: The UI Toolkit contention tuner intentionally force-completes mock generation and commit jobs to produce an editor-side microsecond sample. The method name `RunMockContention(...)` hid that blocking behavior even though it is fenced inside `#if UNITY_EDITOR`.

Solution: Rename the method to `RunMockContentionEditorBlocking(...)` and point the editor button at that explicit method. The runtime dispatcher route still returns `JobHandle`s and does not use this editor button path.

Rejected Alternatives: Removing the blocking benchmark was rejected because Task 05 and Task 18 require an isolated mock contention generator and editor stress harness. Moving the benchmark to a runtime path was rejected because same-frame schedule/readback loops are forbidden without profiler proof and would violate dispatcher-owned completion windows.

Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. The editor tool remains a manual x-ray harness for observing continuous capacity and quality tuning without turning its blocking measurement pattern into a hidden runtime route.

Hardware Impact: Measured runtime proof is absent. Static effect is naming disclosure only; runtime hot path unchanged.

## Decision 42 - SPSC Ring Indices Need Interlocked Mutation

Problem: `SpscSignalRingBuffer<T>` retained `Volatile.Write(...)` for `_head` and `_tail` mutations. The type is a Core signal escape hatch and the native-memory mandate treats weak ARM memory ordering as the baseline; cross-thread index mutations require `Interlocked.Exchange(...)` or `CompareExchange(...)`, while `Volatile.Read(...)` is used for observation.

Solution: Change `Clear()`, producer enqueue tail publication, and consumer dequeue head publication to `Interlocked.Exchange(...)`. Keep `_head`/`_tail` reads as `Volatile.Read(...)`. Update `Docs/ARCHITECTURE/GLOBAL_SIGNAL_CORRIDOR.md` so the documented SPSC contract matches source.

Rejected Alternatives: Leaving the old writes was rejected because the code violated the explicit barrier rule even if current static source scans find no live C# callsite for the generic type. Replacing the whole SPSC wrapper with `NativeQueue<T>` was rejected because it would convert a single-producer/single-consumer escape hatch into an MPSC lane and reintroduce unnecessary atomic queue machinery.

Scalability potential: Low-tier and mobile/ARM targets gain stronger memory-ordering safety without changing payload shape or SignalBus authority. Middle/High/Ultra behavior is unchanged; this patch protects correctness rather than adding visual fidelity.

Hardware Impact: Measured runtime proof is absent. Static effect is barrier correctness only. There is no live callsite proof for `SpscSignalRingBuffer<T>` in current first-party C# source, so no microsecond saving is claimed.

## Decision 43 - Global Corridor Docs Cannot Endorse Legacy MPSC For Producer Storms

Problem: `Docs/ARCHITECTURE/GLOBAL_SIGNAL_CORRIDOR.md` still said `NativeQueue<T>.ParallelWriter` was the correct MPSC path when multiple jobs produce the same event type. That text contradicted SHINOBU_200's implemented route and original task law: high-frequency same-lane producers must avoid shared CAS queue contention and use per-thread scratch plus deterministic commit.

Solution: Reword the global corridor contract so `ParallelWriter` is explicitly a retained low-frequency legacy bridge. The documented high-frequency route is now the SHINOBU thread-local corridor: per-worker scratch, 64-byte payload layout, deterministic post-simulation commit, coalescence, and telemetry-visible overflow. Source comments on `OpenLegacyMpscWriter()`, `OpenParallelWriter()`, and `ParallelWriter` now match that contract.

Rejected Alternatives: Adding `[Obsolete]` attributes was rejected because sibling-domain legacy call sites still exist and warning-as-error configurations could create a compile wall. Rewriting every legacy writer call in the repo was rejected because that crosses active domain ownership. Leaving the stale wording was rejected because future agents would read the doc as approval to route producer storms back through a shared MPSC queue.

Scalability potential: Low-tier devices avoid high-frequency CAS producer storms by treating `ParallelWriter` as a rare bridge. Middle tiers keep the same explicit SignalBus lanes. High/Ultra tiers preserve richer event detail through larger thread-local buffers and visual-sync consumers, not by increasing shared queue contention.

Hardware Impact: Measured runtime proof is absent. Static effect is source/document contract hardening only; no BufferID, payload stride, save identity, rollback exclusion, queue type, or authority owner changed.

## Decision 44 - Compatibility Writer Properties Must Not Advertise Broad Burst Producer Use

Problem: After the main route documentation was corrected, the individual `GlobalSignals.*SignalWriter` XML summaries still used broad wording such as `Burst jobs or background producers`. That text was enough to mislead future producers back into the retained MPSC bridge for high-frequency work.

Solution: Rewrite the property summaries so every retained writer property is explicitly a legacy bridge for low-frequency compatibility producers. Also correct `GLOBAL_SIGNAL_CORRIDOR.md` consume/publish wording: producer-side surfaces are `Publish(in T)` and typed `SignalBus<T>.Push/TryPush`; destructive cursor consumption is `TryConsumeFrame(...)` or retained bridge `TryDequeue*`; snapshot reads are read-only.

Rejected Alternatives: Removing the writer properties was rejected because they are public compatibility ABI for sibling domains. Adding compiler warnings was rejected because warnings-as-errors could break unrelated lanes. Leaving broad per-property XML summaries was rejected because source comments are part of the architecture contract in this batch.

Scalability potential: Low-tier devices keep the legacy writer route as rare bridge traffic only. Middle tiers keep typed signal lanes and explicit snapshot consumption. High/Ultra tiers get richer event presentation through thread-local capacity and visual-sync consumers, not by routing producer storms into a shared MPSC queue.

Hardware Impact: Measured runtime proof is absent. Static effect is comment/document contract hardening only; no BufferID, payload stride, save identity, rollback exclusion, queue type, runtime branch, or authority owner changed.

## Decision 45 - Typed Writer Acquisition Must Not Wake Every Direct Queue

Problem: `GlobalSignals.OpenSignalWriterForProducerPhase<TSignal>()` still called broad `GlobalSignals.EnsureInitialized()`. That method routes to `InitializeAllQueues()`, which reads quality/homeostasis, initializes tuning/telemetry/scratch lanes, and prewarms the legacy direct `NativeQueue` fields. Opening one typed compatibility writer must not perform global queue boot work.

Solution: Remove the broad `EnsureInitialized()` call and delegate directly to `SignalBus<TSignal>.OpenLegacyMpscWriter()`. The closed generic SignalBus lane still performs its own `EnsureInitialized()` and registers/flattens the requested typed route only.

Rejected Alternatives: Keeping the broad init was rejected because it hides cold allocation/prewarm behind a producer-open facade. Deleting the compatibility writer APIs was rejected because sibling-domain callers still depend on the ABI and warning churn can become a compile wall. Moving all direct queues to lazy typed SignalBus lanes was rejected as an integrator-scale migration outside SHINOBU_200.

Scalability potential: Low-tier devices avoid accidental all-lane queue prewarm when a rare legacy writer is opened. Middle tiers keep explicit typed bridge lanes. High/Ultra tiers retain the same event capacity after proper bootstrap; visual overkill still comes from thread-local capacity and downstream presentation, not broad queue wake-up.

Hardware Impact: Measured runtime proof is absent. Static effect is narrower writer acquisition: one typed lane open instead of broad `InitializeAllQueues()` side effects. No BufferID, queue type, DTO layout, signal payload stride, save identity, rollback exclusion, producer phase, or authority owner changed.

## Decision 46 - Adjacent Core MPSC Helpers Need Explicit Open Surfaces

Problem: The read-only sidecar found direct `.AsParallelWriter()` residue outside the SHINOBU SignalBus route. Most hits are sibling-domain ownership, but two Core helpers were within the safe proof surface. `ThreadSafeCommandQueue.TryGetParallelWriter(...)` initialized a native queue behind a `TryGet*` name, and `BurstCallbackQueue.ParallelWriter` hid writer acquisition behind a property.

Solution: Add explicit open surfaces. `ThreadSafeCommandQueue.OpenLegacyMpscWriter()` and `TryOpenParallelWriter(...)` perform initialization and writer acquisition; the old `AsParallelWriter()` compatibility alias delegates to the open method, while `TryGetParallelWriter(...)` no longer initializes storage and only reads already-created state. `BurstCallbackQueue.OpenParallelWriter()` performs the writer conversion; the `ParallelWriter` property remains as a compatibility alias with explicit documentation.

Rejected Alternatives: Rewriting every repo-wide `.AsParallelWriter()` hit was rejected because atmosphere, world, power, quest, UI, visor, and modding lanes belong to other active domain owners. Removing compatibility aliases was rejected because public call sites may exist outside the static scan. Replacing the callback/structural queues with thread-local scratch was rejected without volume profiling; these are low-frequency bridge helpers, not the SHINOBU high-frequency signal storm corridor.

Scalability potential: Low-tier devices gain clearer boundaries that prevent accidental queue initialization from read-named calls. Middle tiers keep existing structural-command and callback semantics. High/Ultra tiers do not route visual overkill through these low-frequency queues; richer output remains bought through thread-local SignalBus capacity and downstream presentation.

Hardware Impact: Measured runtime proof is absent. Static effect is source-contract hardening and reduced hidden initialization risk in Core helper APIs. No BufferID, DTO layout, signal payload stride, save identity, rollback exclusion, queue owner, or GlobalQualityWeight truth route changed.

## Decision 47 - SHINOBU Capacity CSV Is Source Data, Not Runtime StreamingAssets

Problem: SHINOBU_200 proof files still contained old wording that named the former runtime `StreamingAssets` signal-capacity CSV as the Task 19 capacity source. SHINOBU_258's active Data Monolith gate intentionally deletes runtime text payloads from `StreamingAssets` and moves signal CSVs to `Assets/_SourceData/Signals`. Leaving the old wording would make future audits misclassify the deletion as missing SHINOBU data or tempt a bad restore of runtime CSV truth.

Solution: Treat `Assets/_SourceData/Signals/signal_corridor_capacities.csv` as the current editor/source-data authoring truth. Keep `SignalThreadContentionCsvHotSwap.TryLoadDefault()` and `TryLoad(string)` fenced to `UNITY_EDITOR`; in player/runtime builds they return false before file I/O. Runtime capacity truth still needs a baked binary/Vault route before Data Monolith readiness can be claimed.

Rejected Alternatives: Restoring the deleted `StreamingAssets` CSV was rejected because it violates the active text-runtime migration. Removing the parser was rejected because designers still need a source-data tuning bridge in editor. Claiming binary readiness was rejected because `static_data.h8bin` is still absent in the current filesystem gate.

Scalability potential: Low/MX350/Quest authoring rows remain available to editor/bake tooling, Middle keeps intermediate stride rows, and High/Ultra rows keep maximum event-detail headroom. Runtime quality remains driven by the existing continuous `GlobalQualityWeight` and Vault tuning values, not by runtime text reads.

Hardware Impact: No runtime microsecond claim. Static effect is authority cleanup: player builds no longer rely on a text file under `StreamingAssets`, and SHINOBU-owned docs now match the source-data migration.

## Decision 48 - Burst Callback Counter Must Use H8Memory Ownership

Problem: `BurstCallbackQueue` still allocated its persistent pending-count lane with `new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory)` and disposed it directly. The queue is a low-frequency Core callback bridge rather than the SHINOBU high-frequency signal corridor, but it is inside the already-touched Core writer surface and the direct allocation bypassed H8Memory owner tracking.

Solution: Allocate the counter through `Hecton8.Core.Memory.H8Memory.Allocate<int>(..., SystemID.CoreDiagnostics, Allocator.Persistent, ClearMemory)` and release it through the matching H8Memory owner route in both synchronous and job-dependent dispose paths. If counter allocation fails, the constructor unregisters and disposes the already-created native queue, clears capacity, and returns fail-closed. Existing `NativeMemorySentinel` registration remains because the callback queue already exposes diagnostics through that tracker.

Rejected Alternatives: Moving the single callback counter into a new DataVault BufferID was rejected because it would widen the global memory contract for a currently unreferenced helper without dispatcher/runtime proof. Removing `BurstCallbackQueue` was rejected because it is a public Core utility surface. Rewriting `NativeQueue<int>` storage was rejected because Unity's queue owns its internal nodes and this pass targets the explicit direct NativeArray allocation we can prove.

Scalability potential: Low-tier devices avoid an unowned persistent counter lane in the callback bridge. Middle/High/Ultra behavior is unchanged; event-volume scalability remains on the SHINOBU thread-local signal corridor rather than this retained low-frequency MPSC bridge.

Hardware Impact: Measured runtime proof is absent. Static effect is H8Memory owner proof and safer failure cleanup for a one-int counter; no BufferID, SignalBus payload stride, queue route, save identity, rollback exclusion, quality curve, or dispatcher dependency changed.

## Decision 49 - Remove Dead Core Imports That Imply Sibling Runtime Coupling

Problem: `GlobalSignals.cs` imported `Hecton8.World` and `ThreadSafeCommandQueue.cs` imported `Hecton8.Caves`. Static symbol search showed no unqualified World/Caves symbols in those files, and `Hecton8.Core.asmdef` does not reference `Hecton8.World.*` or `Hecton8.Caves.*` runtime assemblies. Leaving dead imports in Core signal/command infrastructure weakens compile-wall evidence and misleads future agents into thinking sibling-runtime access is approved.

Solution: Remove the two dead namespace imports only. No asmdef reference, BufferID, DTO layout, queue route, signal payload, save identity, rollback exclusion, or runtime branch changed.

Rejected Alternatives: Editing `Hecton8.Core.asmdef` was rejected because the assembly definition already lacks World/Caves runtime references and broad asmdef churn risks a compile wall. Rewriting biome/cave signal payloads was rejected because the current symbols are Core contract DTOs or local constants, not sibling-domain types.

Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The value is iteration-speed protection: Core signal and structural-command files no longer advertise dead sibling-domain imports.

Hardware Impact: No runtime microsecond claim. Static compile-wall proof improved by removing misleading direct namespace imports; no generated code, Burst job, memory allocation, or job dependency changed.

## Decision 50 - Structural Command Rollback Must Not Dispatch Through UI Domain

Problem: The sidecar audit found `ThreadSafeCommandQueue.ExecuteCommand(...)` still dispatching `UndoPDAState` through `Hecton8.UI.PDAEvents.RaiseUndoRequest(...)`. That placed a Core structural-command queue on a concrete sibling UI namespace instead of the Core-owned state route. The current UI event listener path does only one relevant side effect: `UIStateStore.TryRollbackPDAState(payload.PayloadA <= 0 ? 1 : payload.PayloadA)`.

Solution: Route `UndoPDAState` directly to `UIStateStore.TryRollbackPDAState(command.IntValue <= 0 ? 1 : command.IntValue)`. This preserves the existing non-positive-frame clamp while removing the Core-to-UI concrete dispatch. No public DTO, queue, SignalBus payload, BufferID, rollback snapshot, shader payload, or save identity changed.

Rejected Alternatives: Keeping the UI event dispatch was rejected because Core infrastructure must not depend on sibling concrete domains. Publishing a new SignalBus lane was rejected because current evidence shows no independent UI listeners for `UndoRequest`; adding a new lane would create an unused route and a new contract surface. Calling `UIStateStore.TryRollbackPDAState(command.IntValue)` without the clamp was rejected because it diverged from the current `PDAEvents.ApplySimulationSideEffects(...)` behavior.

Scalability potential: Low-tier devices avoid one managed UI-domain event queue hop for structural undo. Middle tiers retain deterministic Core state rollback. High/Ultra tiers keep presentation behavior unchanged; visual overkill remains downstream of `UIStateStore` state observation, not a Core command dispatch into UI code.

Hardware Impact: No measured runtime microsecond claim. Static effect is compile-wall and authority hardening: `ThreadSafeCommandQueue.cs` contains no `Hecton8.UI` reference or `PDAEvents.RaiseUndoRequest(...)` call, and the rollback path stays inside Core state storage.

## Decision 51 - Residual Core Sibling References Are Integrator/Core-Owner Debt

Problem: A broader Core scan after the queue patch found direct sibling references in `SystemDispatcher`, `GlobalRegistry`, `GlobalRegistryContracts`, runtime context services, diagnostics viewers, and player context managers. These are not the SHINOBU-owned signal corridor files. Some are core authority surfaces that intentionally expose or flush cross-domain services today; replacing them requires a dispatcher/registry route-card migration, not a narrow MPSC queue patch.

Solution: Record the residual inventory and leave those massive Core files unedited in this pass. Keep the SHINOBU-owned/touched signal and command surface clean: `ThreadSafeCommandQueue.cs`, `GlobalSignals.cs`, and `BurstCallback.cs` now have no concrete `Hecton8.UI` hit, no dead World/Caves import, and no forbidden direct counter allocation/dispose pattern.

Rejected Alternatives: Mass-rewriting `SystemDispatcher` UI/mod flushes was rejected because it is a broad phase-owner migration and risks breaking presentation dispatch. Editing `GlobalRegistry`/contracts was rejected because those are shared core identity surfaces, not SHINOBU lane-owned implementation files. Ignoring the scan was rejected because the compile-wall proof must state what is fixed and what remains outside this lane.

Scalability potential: Low-tier devices benefit from the narrow queue cleanup without destabilizing dispatcher boot/flush order. Middle tiers keep current registry/context behavior. High/Ultra tiers are unaffected; visual overkill routing still belongs to downstream presentation consumers, not a SHINOBU-owned rewrite of global registry services.

Hardware Impact: No runtime microsecond claim. Static effect is risk containment: touched SHINOBU signal/command files are clean, while remaining cross-domain Core surfaces are explicitly classified as integrator/core-owner debt pending a separate route-carded migration.

## Decision 52 - Structural Command Writer Alias Cannot Keep TryGet Naming

Problem: `ThreadSafeCommandQueue.TryGetParallelWriter(...)` no longer initialized storage after the previous pass, but it still returned a mutable `NativeQueue<EntityCommand>.ParallelWriter` through a `TryGet*` name. The global accessor doctrine requires read-looking APIs to be pure inspection paths. A mutable producer writer is not a read accessor even when storage is already initialized.

Solution: Delete `TryGetParallelWriter(...)`. The maintained structural-command writer routes are `TryOpenParallelWriter(...)` and `OpenLegacyMpscWriter()`, both named as open/producer paths. `AsParallelWriter()` remains only as a legacy compatibility alias because first-party source still has a known pattern of legacy writer APIs, while `TryGetParallelWriter` had no repo caller.

Rejected Alternatives: Keeping the alias was rejected because the name itself violates the source contract and invites future misuse. Marking it obsolete was rejected because there is no first-party caller to migrate and warnings can become compile-wall noise. Removing `AsParallelWriter()` in the same pass was rejected because the wider ecosystem still has many `AsParallelWriter` compatibility surfaces and that removal should be route-carded separately.

Scalability potential: Low-tier devices gain clearer command ingress boundaries; no accidental read-looking path can be copied into a hot producer route. Middle/High/Ultra behavior is unchanged; structural command storms still require owner-local batching before the retained MPSC bridge.

Hardware Impact: No runtime microsecond claim. Static effect is source-contract hardening only: repo-wide source scan finds no `TryGetParallelWriter` after deletion, and the focused touched-file forbidden-pattern scan remains clean.

## Decision 53 - Sidecar Purity Closure Without Annotation Theater

Problem: After repeated polish passes, the SHINOBU signal corridor needed an independent check for read-accessor purity, writer exposure, Burst flags, `[NoAlias]`, documented cache-line debt, and crash-only `GlobalDataVault.TryGetLatestCreated()` usage. The risk was either missing a real mutation path or creating churn by annotating non-job context fields that do not influence Burst vectorization.

Solution: Consumed the Gauss sidecar audit and recorded it as clean for the requested SHINOBU scope. Read-looking audited paths do not allocate/grow native buffers, publish, complete jobs, mutate global state, or search scene. Direct `.AsParallelWriter()` remains inside explicit compatibility/open methods only. SHINOBU Burst jobs retain deterministic synchronous Burst attributes and `[NoAlias]` on relevant `NativeArray` fields. The only cache-line-critical non-64/128 payloads remain `ToolAcousticSignal` and `TetherTensionSignal`, both documented as open route-card debt. `TryGetLatestCreated()` is present only in the crash/fault route.

Rejected Alternatives: Changing non-job context fields only to satisfy a text pattern was rejected because `[NoAlias]` matters to Burst kernel fields, not cold managed facades. Padding `ToolAcousticSignal` or splitting `TetherTensionSignal` was rejected because both cross broad producer/consumer ownership and already have explicit migration gates. Reopening the removed `TryGetParallelWriter` compatibility API was rejected because the sidecar confirmed explicit writer vocabulary is sufficient in the audited first-party scope.

Scalability potential: Low-tier devices keep the thread-local signal corridor and avoid hidden writer/read-accessor regressions. Middle tiers keep the same deterministic snapshot route. High/Ultra keep richer event-detail headroom through Vault tuning and presentation consumers, not through expanded legacy MPSC surfaces.

Hardware Impact: No profiler microsecond claim. Static proof improved by closing the sidecar loop and preserving only documented compatibility bridges; runtime build/import proof remains blocked by the CPU guard.

## Decision 54 - Combat Damage Codec Must Not Pull World AUP Type Into Core Signal Surface

Problem: Russell's read-only sidecar found that `CombatDamageSignalCodec` still referenced `global::Hecton8.World.AbsoluteUniversePosition` and `OffsetAbsoluteMeters(...)` from `Assets/_Project/Scripts/Core/GlobalSignals.cs`. The dead `using Hecton8.World` cleanup was not enough because the concrete sibling type was fully qualified inside the codec. That weakens the Core signal compile-wall proof and makes a low-level codec depend on a World implementation type.

Solution: Replace the concrete World AUP object reconstruction with Core-owned double precision origin math: `HectonFloatingOrigin.CurrentTotalOffsetDouble + new double3(runtimePoint.x, runtimePoint.y, runtimePoint.z)`. The helper is private, finite-guarded, allocation-free, and preserves the public `double3 FromRuntimePoint(...)` API and the existing `CombatDamageSignal` ABI. Runtime-to-AUP conversion stays in double precision before any local float projection, satisfying the 100 km jitter rule without importing a World DTO.

Rejected Alternatives: Moving `CombatDamageSignalCodec` into World was rejected because call sites span combat, fauna, vehicles, construction, habitat, and Core context code. Changing the public codec return type to a World AUP contract was rejected because it would mutate every producer/consumer and likely force asmdef churn. Reusing `GlobalSignals.CurrentRuntimeOriginAup()` was rejected in this codec because it still exposes the concrete World AUP type at the call boundary.

Scalability potential: Low-tier devices keep the cheapest conversion path: one cached-origin read, one double3 add, and finite guards. Middle tiers keep the same deterministic signal ABI. High/Ultra tiers do not gain extra gameplay truth here; visual overkill remains downstream of the signal consumers and shader/VFX presentation lanes.

Hardware Impact: No measured microsecond claim. Static effect is compile-wall and branch-surface hardening. The patch removes a concrete sibling-domain type from a touched Core codec while avoiding a cross-domain migration that would increase build churn and merge risk.

## Decision 55 - Legacy AUP DTO Aliases Are Safer Than Broad World Import Or Broken Source

Problem: `GlobalSignals.cs` has many pre-existing `AbsoluteUniversePosition` and `AbsoluteUniversePositionBlit` fields across signal DTOs. Removing `using Hecton8.World;` without replacing those symbol imports risks a compile failure before any Burst or Unity import validation can run. The goal is to remove the codec's concrete World AUP construction path, not silently invalidate every legacy AUP-bearing signal contract.

Solution: Add two explicit aliases at the top of `GlobalSignals.cs`: `AbsoluteUniversePosition = Hecton8.World.AbsoluteUniversePosition` and `AbsoluteUniversePositionBlit = Hecton8.World.AbsoluteUniversePositionBlit`. This keeps the existing source compiling against the current AUP DTO while preventing a broad namespace import from hiding future World usage. The combat codec remains on the Core floating-origin `double3` projection path and no longer calls World AUP methods.

Rejected Alternatives: Restoring broad `using Hecton8.World;` was rejected because it reopens the full namespace and hides future direct use. Converting every AUP-bearing signal to a new Core contract DTO was rejected because it is a major ABI migration across signal producers, consumers, save paths, and tests. Leaving the import removed was rejected because static source already shows unresolved AUP type names under normal C# rules.

Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. This is compile-risk containment and boundary clarity, not a new fidelity path. The existing continuous `GlobalQualityWeight` route and SHINOBU thread-local buffers are untouched.

Hardware Impact: No runtime microsecond claim. Static effect is lower compile-wall risk: the file keeps the old AUP DTO dependency explicit and narrow while the hot codec path avoids World method calls.
