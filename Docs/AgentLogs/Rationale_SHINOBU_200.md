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

Problem: `SignalThreadContentionCsvHotSwap` had a parser but no `Assets/StreamingAssets/signal_corridor_capacities.csv` asset in the checkout. It also borrowed the older generic `SignalTuningTable` CSV scratch buffer `73042`, which made the Task 19 H-Phi proof ambiguous.

Solution: Added Vault buffer `73055` as `SignalThreadContentionCsvScratch byte[8192]`, resolved through `SignalThreadLocalScratchpad.TryGetCsvScratch`. Added `Assets/StreamingAssets/signal_corridor_capacities.csv` with platform/min/max/output rows and a stable Unity `.meta`. Platform label hashing now lowercases ASCII bytes before FNV-1a folding without allocating strings.

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
