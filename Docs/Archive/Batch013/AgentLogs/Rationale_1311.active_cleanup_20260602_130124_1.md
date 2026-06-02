# Rationale_1311 - SIGNAL_CORRIDOR_SPSC_ARCHITECT

Status: STRICT STATIC GREEN / RUNTIME NOT PROVEN

## 2026-05-25 Phase 0 Bootstrap

Problem: The requested prompt path was described as root `current_batch.md`, but active batch reality is `Docs/Tasks/CURRENT_BATCH.md`; exact `<AGENT_PROMPT id="1311">` exists there.
Solution: Use CLI extraction from `Docs/Tasks/CURRENT_BATCH.md` with an attribute-tolerant XML tag match. Treat only the `id="1311"` block as active assignment.
Rejected Alternatives: Reading the neighboring `id="1300"` block or archived batches; both would contaminate domain and task count.
Scalability potential: No runtime effect. Prevents wrong-domain edits that could break core signal ownership.
Hardware Impact: 0 us runtime; process hygiene only.

## 2026-05-25 Phase 0 Source Audit

Problem: Active `SignalBus<T>` storage is not the `SpscSignalRingBuffer<T>` fallback. `SignalBusRuntime.cs` declares `private static NativeQueue<T> _queue`, allocates it during lane initialization, gates push with `_queue.Count`, enqueues in `TryPush`, exposes `NativeQueue<T>.ParallelWriter`, and drains with `_queue.TryDequeue` during flush.
Solution: Record every active queue allocation/read/write/flush surface in `Docs/Reports/SIGNAL_SPSC_PHASE0_LEDGER_1311.json` before code mutation.
Rejected Alternatives: Claiming the project already uses SPSC because `SpscSignalRingBuffer<T>` exists on disk. Existence is not call-site integration.
Scalability potential: Low/MX350 gains only after Phase 1 removes `NativeQueue<T>.Count` and queue-linked storage from active lanes. Middle/High/Ultra can spend the saved flush budget on richer telemetry and visual-only consumers, but no runtime gain is claimed from this audit.
Hardware Impact: Static audit only; measured gain absent. Expected target after replacement is removal of O(N) Count traversal and less cache-hostile queue traversal during signal flush.

Problem: `SpscSignalRingBuffer<T>` has a 64-byte explicit `PaddedSignalIndex`, but the parent struct is implicit sequential and contains `NativeArray<T>` before `_head`/`_tail`. Source proves the cursor wrappers are 64 bytes and adjacent; it does not prove parent `_head` offset 0 or `_tail` offset 64.
Solution: Mark cursor alignment as partial source proof. Phase 1 must either move cursor state into an explicit 128-byte header or accept a documented deviation from the prompt's parent-layout wording because `NativeArray<T>` size varies with Unity safety configuration.
Rejected Alternatives: Writing a fake byte-offset claim without compiled `UnsafeUtility.GetFieldOffset` evidence, or applying `[StructLayout(LayoutKind.Explicit, Size=128)]` blindly over a generic native-container wrapper.
Scalability potential: Low/Middle benefit from avoiding false sharing if the header is made explicit. High/Ultra benefit only if producer storms no longer serialize on one shared queue cursor.
Hardware Impact: Static source effect only. No microsecond claim without profiler or Burst/editor layout proof.

Problem: A strict two-file replacement cannot remove `NativeQueue<T>.ParallelWriter` from job producers without breaking public writer signatures. Broad project source stores `SignalBus<T>.ParallelWriter` as `NativeQueue<T>.ParallelWriter` in multiple job structs.
Solution: Phase 1 must introduce an unmanaged `SignalBus<T>.ParallelWriter` backed by a CAS-reserved MPSC ring and then migrate producer fields/calls, or keep a documented legacy bridge that is not claimed as complete removal.
Rejected Alternatives: Keeping a hidden NativeQueue bridge while reporting "NativeQueue eliminated"; rejected as a false report.
Scalability potential: Low tier needs bounded drops with no queue growth. Middle uses normal frame caps. High/Ultra can retain richer telemetry and visual-only overflow consumers after core gameplay lanes are bounded.
Hardware Impact: Source-only risk model. No runtime numbers yet.

## 2026-05-25 Ring Primitive Patch

Problem: The previous SPSC fallback placed cursor padding in two private 64-byte structs, but the parent wrapper still hid cursor offsets behind `NativeArray<T>` and other fields. It did not satisfy the actual cache-line proof needed for cursor isolation.
Solution: Introduced `SignalRingCursorState` with `[StructLayout(LayoutKind.Explicit, Size = 128)]`, `Head` at byte 0, and `Tail` at byte 64. `SpscSignalRingBuffer<T>` now stores the cursor state in a native one-row header and mutates it through volatile/interlocked operations.
Rejected Alternatives: Forcing explicit layout onto the generic parent wrapper. That would be a fake proof because Unity `NativeArray<T>` embeds configuration-dependent safety state.
Scalability potential: Low/MX350 avoids producer/consumer cursor false sharing once the primitive is active. Middle/High/Ultra can raise telemetry richness after the queue traversal cost is removed.
Hardware Impact: No measured microseconds. Static source effect only; compile and profiler proof pending.

Problem: SPSC cannot safely accept multiple producers. Existing job producers expose `NativeQueue<T>.ParallelWriter`, so a real replacement needs MPSC reservation semantics.
Solution: Added `MpscSignalRingBuffer<T>` with bounded power-of-two storage, CAS tail reservation, per-slot publication tickets, and a nested unmanaged `ParallelWriter`. Consumer reads only when the expected slot ticket is published.
Rejected Alternatives: Using SPSC for job producers, which would corrupt under concurrent writes; keeping Unity `NativeQueue<T>.ParallelWriter`, which preserves the current queue-backed lie.
Scalability potential: Low tier drops on full without allocation or growth. Middle uses configured frame caps. High/Ultra can increase optional diagnostic/visual lanes after contention is bounded.
Hardware Impact: Expected saving is removal of NativeQueue linked storage and Count traversal only after SignalBus integration. Current patch is primitive-only; no runtime saving claimed.

Problem: Build verification is required but the CPU guard blocked it.
Solution: Sampled CPU after source patch; `_Total` processor time was 83.1%, so no `dotnet build` was launched. Scoped `git diff --check` passed with CRLF warnings only.
Rejected Alternatives: Violating the explicit no-build-under-load rule.
Scalability potential: No runtime effect.
Hardware Impact: 0 us runtime; verification blocked by host load.

Problem: Agent tracking files did not exist for `1311`, so the mandated state machine had no local disk memory.
Solution: Create `Docs/Tasks/Status_1311.md` and `Docs/AgentLogs/Rationale_1311.md` before source mutation.
Rejected Alternatives: Chat-only tracking; rejected because context compression loses state and project rules require disk evidence.
Scalability potential: No runtime effect. Prevents incomplete or duplicated passes.
Hardware Impact: 0 us runtime; process hygiene only.

## 2026-05-25 Paranoid Review Correction

Problem: The first explicit cursor header used `int Head` and `int Tail`. That isolated cache lines, but it left implicit 4-byte gaps after each cursor and kept a shorter wraparound horizon than required for a long-running signal corridor.
Solution: Convert cursor fields to `long`, keep all padding as explicit 8-byte `ulong`, and record the complete byte map in `Docs/Reports/SIGNAL_SPSC_PARANOID_REVIEW_1311.md`.
Rejected Alternatives: Keeping `int` cursors and explaining the implicit holes as harmless; rejected because the user requested a byte-exact ARM64 map and all 8-byte fields first.
Scalability potential: Low/MX350 avoids false sharing and long-session wrap risk once active lanes are migrated. Middle/High/Ultra get stable cursor math under longer telemetry and signal-storm sessions.
Hardware Impact: No measured microseconds. Source-level correction only; cache-line proof improved from partial to explicit header proof.

Problem: The first MPSC producer loop used a 32-attempt CAS cap. Under real contention this can create a false drop even when capacity becomes available after repeated CAS losses.
Solution: Replace the bounded attempt loop with a lock-free retry loop that returns false only when the ring is observed full. Per-slot `long` tickets still protect the consumer from reading a reserved but unpublished slot.
Rejected Alternatives: Keeping the 32-attempt cap as a cheap anti-spin guard; rejected because it violates the no-fake-drop goal for the primitive. Reusing `NativeQueue<T>.ParallelWriter` remains rejected because it preserves the current active queue-backed path.
Scalability potential: Low tier still drops when capacity is genuinely full. Middle/High/Ultra avoid artificial loss during producer storms, trading transient CAS spin for data integrity.
Hardware Impact: No profiler number. Expected effect is fewer false drops under contention after SignalBus integration.

Problem: The user requested paranoid managed-logic and native-array review. `SpscSignalRingBuffer.cs` needed a concrete scan result instead of a broad "zero-GC" claim.
Solution: Scan for `new`, string formatting, `ToString`, LINQ, foreach, exceptions, debug logging, interpolated strings, and concat. Only hit is `new ParallelWriter(...)`, a value-type construction. Documented native collections and dispose routes line-by-line.
Rejected Alternatives: Claiming zero-GC without disclosing the value-type `new` expression and native header arrays.
Scalability potential: No runtime change. It prevents hidden managed work from entering the hot signal path.
Hardware Impact: 0 us measured; static proof only.

Problem: A repeatable signal-contract gate was needed, but running `dotnet run` or a build violates the user's "rare build" instruction.
Solution: Execute the already-built `SignalBusContractAuditCli` net10 binary directly. The net8 binary failed because .NET 8 runtime is absent; the net10 binary succeeded and wrote `Docs/Reports/SIGNAL_SPSC_CONTRACT_AUDIT_1311.json` and `.md`.
Rejected Alternatives: Launching `dotnet build` or `dotnet run`; rejected under current build restraint. Ignoring the existing scanner; rejected because it provides independent source-classified evidence.
Scalability potential: No runtime change. The scanner keeps signal boundary debt visible before integration.
Hardware Impact: 0 us runtime in player; editor/tooling only.

Problem: The master assignment is not fully complete while `SignalBusRuntime.cs` remains backed by `NativeQueue<T>`.
Solution: Keep Task 06+ open and record the exact active queue red-zone lines in `Docs/Reports/SIGNAL_SPSC_PARANOID_REVIEW_1311.md`.
Rejected Alternatives: Reporting the primitive as a full active-lane replacement.
Scalability potential: Actual low-end savings require later removal of `_queue.Count`, `_queue.Enqueue`, and `_queue.TryDequeue` from active lanes. High/Ultra benefits also remain pending until integration.
Hardware Impact: Current measured gain is 0 us. Expected future gain cannot be claimed without the active path rewrite and profiler proof.

## 2026-05-25 Active Path Ring Integration

Problem: `SignalBusRuntime.cs` main-thread active path still used `_queue.Count`, `_queue.Enqueue`, and `_queue.TryDequeue`, which contradicted the SPSC/MPSC prompt even after the primitive was added.
Solution: Replace the active `_queue` field with `_ring` backed by `MpscSignalRingBuffer<T>`. `TryPush` now sanitizes and publishes through `_ring.TryEnqueue`; flush/drop/clear routes use `CountPendingSignals`, `TryDequeuePendingSignal`, and `ClearPendingSignals`.
Rejected Alternatives: Keeping `_queue` as the primary lane storage and only adding a ring sidecar; rejected because it would preserve the O(N) `NativeQueue.Count` path for normal publishes.
Scalability potential: Low/MX350 can now avoid NativeQueue Count traversal for main-thread pushes and normal ring flushes. Middle/High/Ultra still need job writer migration before producer storms stop touching the legacy queue bridge.
Hardware Impact: No profiler number. Static expected saving is removal of `_queue.Count` and `_queue.Enqueue` from `TryPush`; measured microseconds remain 0 until Unity profiler proof.

Problem: Public `SignalBus<T>.ParallelWriter`, `OpenParallelWriter`, and `TryEnqueueBounded` are consumed by many job structs as `NativeQueue<T>.ParallelWriter`. A direct type flip would break broad neighboring domains.
Solution: Keep a lazy `_legacyQueue` bridge only for the old public writer API. It allocates on first legacy writer open, prewarms itself, is tracked by `NativeMemorySentinel` id, and is drained by the same flush helper after the ring. Add `OpenRingParallelWriter`, `RingParallelWriter`, and a `TryEnqueueBounded(MpscSignalRingBuffer<T>.ParallelWriter, ...)` overload so migrated producers can use the first-party ring without waiting for a global signature flip.
Rejected Alternatives: Returning default legacy writers and silently dropping all job-produced signals; rejected as gameplay data loss. Migrating every job struct in this pass; rejected as a cross-domain compile wall outside the two-file assignment boundary without a build window.
Scalability potential: Low tier still pays legacy queue cost only for unmigrated job producers. Middle/High/Ultra need per-domain migration to `MpscSignalRingBuffer<T>.ParallelWriter` or a first-party SignalBus writer before the old bridge can be deleted.
Hardware Impact: NativeQueue hot cost is reduced but not eliminated. Any exact saving claim is invalid without profiling and after job producer migration.

Problem: Sentinel queue labels used per-type string concatenation through `typeof(T).Name + ".Queue"`.
Solution: Store the sentinel registration id for the legacy queue and unregister by id. Use a constant label `SignalBus.LegacyQueue`; remove the unused snapshot-label method.
Rejected Alternatives: Keeping per-type string labels and pretending the string concat scan was clean.
Scalability potential: No frame-time gain expected. It removes a cold managed allocation from the legacy bridge path.
Hardware Impact: 0 us measured; cold allocation hygiene only.

Problem: Development fault logs used string concatenation with `typeof(T).FullName`.
Solution: Replace them with constant `H8Debug` messages. Detailed type specificity is already available through lane hash/configuration counters and static scanner artifacts.
Rejected Alternatives: Keeping verbose fault logs in development builds; rejected because the current review demanded no string concatenation in runtime code.
Scalability potential: No player-release effect under current guards. Development fault paths avoid managed concat pressure.
Hardware Impact: 0 us measured; dev/fault path only.

## 2026-05-25 Scanner / ARM64 / Black-Box Correction Pass

Problem: Task 11 previously relied on a generic contract audit and did not provide a dedicated `OOP_SignalSpsc_Scanner` proof artifact.
Solution: Added `Tools/OOP_SignalSpsc_Scanner.py`. It scans the two target files, call sites under `Assets/_Project/Scripts`, byte-offset DTO maps, AUP tokens, asmdef references, and dump-path evidence. It intentionally returns RED while any `NativeQueue<T>` bridge or legacy writer call site remains.
Rejected Alternatives: Suppressing compatibility bridge findings to make the report green; rejected because the prompt requires proof of complete active-lane removal.
Scalability potential: Low/MX350 receives no runtime gain from the scanner. It prevents false release claims that would leave queue traversal and writer contention hidden. Middle/High/Ultra benefit only after the remaining writer migration work is actually done.
Hardware Impact: 0 us runtime; tool-only. Scanner wall time observed locally, no player cost.

Problem: The scanner found ARM64 field-order violations in existing signal telemetry DTOs: `SignalLaneTelemetry` and `SignalTelemetryFrame` stored 4-byte fields before 8-byte fields.
Solution: Reordered explicit `FieldOffset` declarations so 8-byte fields lead, followed by 4-byte fields, then 2-byte and 1-byte fields. Sizes remain 32B and 64B respectively.
Rejected Alternatives: Leaving the structs unchanged because they were already multiple-of-8; rejected because the active mandate requires largest-to-smallest field order, not only total size divisibility.
Scalability potential: Low/MX350/Quest-class ARM64 paths avoid avoidable misalignment debt in telemetry snapshots. High/Ultra can add richer telemetry only if the same order and size laws remain intact.
Hardware Impact: No measured microseconds. Static layout hygiene only; Unity/Burst/IL2CPP offset proof still absent.

Problem: The black-box dump target did not match the batch prompt. Existing signal corridor dump path was `Docs/AgentLogs/Dump_SIGNAL_CORRIDOR.bin`.
Solution: Changed `SignalTelemetryRingBuffer.DumpPath` to `Docs/AgentLogs/Dump_1311_SignalCorridor.bin` and updated the summary comment. The existing 300-frame vault-backed ring remains the single owner of signal-corridor black-box telemetry.
Rejected Alternatives: Creating a duplicate black-box ring in `SignalBusRuntime.cs`; rejected because it would split telemetry authority and duplicate a Core/Signals vault route already present.
Scalability potential: No frame-time change. Low tier keeps the same 19.2KB telemetry footprint; High/Ultra can inspect the same dump without increasing hot-path payload size.
Hardware Impact: 0 us measured in gameplay. Dump I/O remains cold/fault path and synchronous in source; background-thread proof is still absent.

Problem: Full `NativeQueue<T>` removal remains blocked by cross-domain job writer fields and call sites.
Solution: Leave the legacy bridge visible and red. The dedicated scanner reports 148 `NativeQueue<T>.ParallelWriter` field hits, 111 legacy writer requests, and 58 `TryEnqueueBounded` legacy call sites.
Rejected Alternatives: Deleting the bridge in `SignalBusRuntime.cs` without migrating job structs; rejected because it would create a compile wall and drop job-produced gameplay signals.
Scalability potential: Low/MX350 still pays legacy queue cost for unmigrated job producers. Middle/High/Ultra cannot claim complete lock-free signal corridor until those producers move to `MpscSignalRingBuffer<T>.ParallelWriter` or a first-party writer facade.
Hardware Impact: Current measured gain remains 0 us. Main-thread `TryPush` ring path is improved statically, but complete removal savings are unmeasured and unrealized while bridge traffic remains.

## 2026-05-25 Legacy Writer Deletion / Job Producer Migration

Problem: The compatibility bridge preserved `NativeQueue<T>.ParallelWriter`, which made the previous report non-releaseable even though main-thread `TryPush` had moved to `_ring`.
Solution: Delete the legacy queue field/API/overload from `SignalBusRuntime.cs`; make `OpenParallelWriter`, `ParallelWriter`, and `RingParallelWriter` return `MpscSignalRingBuffer<T>.ParallelWriter`; migrate SignalBus-owned job writer fields/calls across the touched domains to the first-party writer type.
Rejected Alternatives: Keeping `NativeQueue<T>.ParallelWriter` for source compatibility; rejected because the batch constraint says completely eliminate `NativeQueue<T>` from active `SignalBus<T>` lanes.
Scalability potential: Low/MX350 no longer pays Unity queue bridge cost for migrated job producers. Middle/High/Ultra can raise optional signal telemetry and visual-only lanes within bounded ring capacity instead of relying on queue growth.
Hardware Impact: 0 us measured. Static proof now shows 0 SignalBus/NativeQueue writer intersections; exact frame saving still requires Unity profiler proof.

Problem: Broad project scan still reports 16 `NativeQueue<T>.ParallelWriter` fields after migration.
Solution: Classify them by payload intersection. The scanner intersects `NativeQueue<T>.ParallelWriter` payload types with `SignalBus<T>.ParallelWriter/OpenParallelWriter/TryEnqueueBounded` payload types; current intersection is 0, so the remaining queues are non-SignalBus local lanes.
Rejected Alternatives: Rewriting unrelated drone/world/mod/KCC queues under this signal-corridor prompt; rejected as cross-domain churn without proof they are signal lanes.
Scalability potential: Low/Middle keep scope controlled. High/Ultra can address those queues in their owning domains later without destabilizing the signal corridor.
Hardware Impact: 0 us measured. No claim is made for unrelated queue paths.

Problem: The fuzzer source introduced two new DTOs that were not included in the byte-offset proof.
Solution: Extend `Tools/OOP_SignalSpsc_Scanner.py` with `SignalStormFuzzerPayload1311` and `SignalStormFuzzerResult1311` byte maps. Scanner report now emits five DTO maps.
Rejected Alternatives: Manually listing fuzzer offsets only in chat; rejected because the proof artifact must survive context loss.
Scalability potential: No player runtime cost; fuzzer is editor-only. It increases confidence in MPSC stress behavior when Unity execution is allowed.
Hardware Impact: 0 us player runtime. Fuzzer stress execution not run in this pass.

Problem: The XML mandates POST_SIMULATION flushing, but live dispatcher ownership currently calls `SignalCorridorRuntime.FlushPreSimulation()` from `SystemDispatcher.cs:5046`.
Solution: Do not falsify completion. Mark Task 07 as static ring integration complete but phase-mandate incomplete until consumer ordering can be audited and moved safely.
Rejected Alternatives: Blindly moving the flush call to post-simulation; rejected because existing consumers may rely on pre-simulation snapshots and this would silently change frame semantics.
Scalability potential: Low/High unchanged until phase is moved with proof. A wrong phase move would damage determinism on every tier.
Hardware Impact: 0 us measured; architecture risk noted, not patched.

## 2026-05-25 APEX Re-Audit Patch

Problem: `SignalBusRuntime.cs` still used `typeof(T).FullName` for fallback lane hashing and `type.FullName` for a hidden Atmosphere-specific policy exception. That violated the managed text scan and created a concealed cross-domain rule inside Core.
Solution: Replace fallback hash generation with `BurstRuntime.GetHashCode32<T>()` and delete the `ToxicityExposureSignal` `FullName` exception. The owner domain still configures Toxicity lanes explicitly where it owns the DTO; Core no longer string-matches a neighboring type name.
Rejected Alternatives: Adding a direct `typeof(Hecton8.Atmosphere.ToxicityExposureSignal)` dependency to preserve the special case; rejected as a horizontal Core-to-Atmosphere coupling. Keeping `FullName`; rejected because the user asked for a hard managed-text audit.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged for configured lanes. Unconfigured lanes keep default capacity and a stable Burst type hash; no hot string walk remains in project source.
Hardware Impact: 0 us measured. Removes cold managed metadata reads from the audited source; no frame-time claim.

Problem: `_parallelWriterBudget` was allocated with a direct `new NativeArray<int>(..., Allocator.Persistent, ...)`, bypassing the project memory owner ledger even though the array is persistent per lane.
Solution: Allocate the budget through `H8Memory.Allocate<int>(..., SystemID.CoreDataVault, Allocator.Persistent, ...)`, release it through `H8Memory.Release(...)`, and fail closed by disposing the ring and releasing the frame snapshot if budget allocation fails.
Rejected Alternatives: Leaving a raw persistent `NativeArray` as "small enough"; rejected because the native-memory mandate requires ownership tracking, not size excuses.
Scalability potential: Low tier gains cleaner memory-fault accounting; High/Ultra can increase lane counts without anonymous persistent arrays.
Hardware Impact: 0 us measured. Adds owner tracking on cold lane initialization; no hot-path allocation added.

Problem: `SignalTelemetryRingBuffer.DumpToDisk()` was called synchronously from drop-storm and corruption telemetry routes.
Solution: Add `RequestDumpToDiskAsync()` with a lazy persistent background worker, pre-resolved dump path on the dispatcher thread, fail-closed fallback to synchronous dump if the worker cannot start, and broad cold-path exception containment inside the writer.
Rejected Alternatives: Creating a fresh `Task` per fault; rejected due per-fault managed scheduling churn. Calling Unity `Application.dataPath` from the worker; rejected because Unity API access must stay on the main thread.
Scalability potential: Low/MX350 avoids dispatcher-thread binary I/O on storm/corruption routes. Middle/High/Ultra keep the same 19.2KB black-box footprint and can inspect the dump without inflating signal payloads.
Hardware Impact: 0 us measured. Expected saving is removal of synchronous dump I/O from the dispatcher fault trigger; not profiler-proven.

Problem: The XML POST_SIMULATION mandate still conflicts with the live dispatcher call at `SystemDispatcher.cs:5046`, while static scan shows many `GetFrameSnapshot()` consumers expecting snapshots during simulation.
Solution: Leave the phase move blocked pending consumer-order proof and keep the blocker explicit in `Status_1311.md`.
Rejected Alternatives: Moving `FlushPreSimulation()` blindly to late frame; rejected because it can shift every signal snapshot by one frame and break determinism.
Scalability potential: No tier changes until the phase migration is proven.
Hardware Impact: 0 us measured.

## 2026-05-25 POST_SIMULATION Phase Closure

Problem: Task 07 was still objectively incomplete because the dispatcher drained typed signal rings during pre-simulation and then cleared snapshots after late-frame consumers. This violated the XML phase route and kept a stale clear delegate in `SignalBusRegistry`.
Solution: Split lifecycle into `PreSimulationHeartbeat()` and `FlushPostSimulation()`. Pre-sim now only refreshes `GlobalQualityWeight` and stress scalars. The active ring drain runs from `SystemDispatcher.cs:5453` through `SignalCorridorRuntime.FlushPostSimulation()` and `SignalBusRegistry.FlushPostSimulation()`. Removed the registry clear delegate and the post-simulation snapshot clear route; snapshots remain the next-frame read view until the next post-sim drain overwrites them.
Rejected Alternatives: Moving the flush directly after `RunMasterPostSimulationPhase`; rejected because late-frame tickables would start reading current-frame snapshots instead of the previous deterministic view. Keeping a clear-after-flush call; rejected because it would erase the next-frame snapshot before consumers can read it.
Scalability potential: Low tier keeps one bounded drain per frame and no extra snapshot buffer. Middle/High/Ultra keep the same signal visibility model and can raise optional lane caps through continuous `GlobalQualityWeight` without extra phase work.
Hardware Impact: 0 us measured. Static work removes one cold delegate route and one per-lane clear dispatch slot; no frame-time claim without Unity profiler.

Problem: The paranoid `new` scan was previously binary and did not distinguish hot managed allocation from value-type/native-container construction.
Solution: Extend `Tools/OOP_SignalSpsc_Scanner.py` to emit `newExpressionClassifications` and phase-route gates. At that pass, the report marked value-type writer, `ReadOnlySpan<T>`, `float3`, and `MpscSignalRingBuffer<T>` construction as `managedHeap=no`, while three registry arrays were still marked `managedHeap=yes hotPath=no`; that array debt is superseded by the native function-pointer dispatch patch below.
Rejected Alternatives: Claiming absolute Zero-GC while cold static arrays still exist in source; rejected as a false report.
Scalability potential: Low/Middle/High/Ultra hot paths were unaffected by those cold arrays. The later function-pointer table removes that cold managed dispatch debt but remains compile/runtime unproven.
Hardware Impact: 0 us measured. Scanner-only proof artifact; no runtime saving claimed.

## 2026-05-25 Native Function Pointer Dispatch / Fuzzer Scratch Correction

Problem: The remaining cold registry arrays and closed generic delegates in `SignalBusRuntime.cs` were managed heap objects even outside the hot enqueue/dequeue path. That made the strict Zero-GC report weaker than the scanner status suggested.
Solution: Replace the managed delegate arrays with one `NativeArray<SignalLaneDispatch>` allocated through `H8Memory`. `SignalLaneDispatch` stores unmanaged function pointers for dispose, post-simulation flush, and telemetry copy. Scanner byte map: 32B total; `Dispose@0`, `Flush@8`, `CopyTelemetry@16`, `_pad0@24`, `_pad1@28`, `FlushDuringSimulationPause@30`, `_pad2@31`.
Rejected Alternatives: Keeping cold managed delegates and documenting them as harmless; rejected because the user explicitly required no managed registry debt. Generating a concrete DTO table; rejected because closed generic signal lanes are configured dynamically and a static table would drift.
Scalability potential: Low/MX350 no longer pays managed registry array cold-start debt for lane dispatch. Middle/High/Ultra can register more lanes without creating managed delegate tables, but runtime lane count still remains bounded by `LaneCapacity`.
Hardware Impact: 0 us measured. Expected saving is cold-start GC pressure removal only; hot path was already no-alloc by static scan.

Problem: The editor-only fuzzer still allocated `NativeArray<byte>` directly with `Allocator.TempJob`, outside the H8 memory owner route.
Solution: Convert fuzzer scratch to `H8Memory.Allocate<byte>(..., SystemID.CoreDataVault, Allocator.TempJob, ClearMemory)` and release through `H8Memory.Release`. The fuzzer remains under `#if UNITY_EDITOR`; managed `Thread`, `ManualResetEventSlim`, object state, and JSON report strings are editor harness scaffolding, not player/runtime signal lane code.
Rejected Alternatives: Moving the fuzzer into player runtime to avoid editor-only managed constructs; rejected because Task 10 asks for an editor-only concurrency fuzzer, not shipped gameplay code. Suppressing the native-array hit in the report; rejected because the native-memory mandate is source-owner based.
Scalability potential: No player runtime effect. The fuzzer can stress low/middle/high/ultra lane capacities in editor once Unity execution is allowed.
Hardware Impact: 0 us player runtime; editor-only verification path.

Problem: The current static gate is green but still not release proof.
Solution: Keep the release gate red for runtime evidence: no Unity compile, no Burst/IL2CPP proof, no GCMonitor/profiler capture, no fuzzer execution, and no observed dump artifact.
Rejected Alternatives: Calling scanner green a release pass; rejected because static text cannot prove scheduler, Burst, platform layout, or fuzzer correctness.
Scalability potential: No tier claim until profiler and fuzzer evidence exist.
Hardware Impact: 0 us measured.

## 2026-05-25 Cold Registration Gate / Partial Native Allocation Cleanup

Problem: `SignalBusRegistry.Register` mutated `_laneCount` and `_laneDispatch` without a cold serialization gate. Two closed generic lanes first-touched from different threads could race the dispatch-table allocation or publish the same lane index.
Solution: Add `_registrationGate`, enter it with `Interlocked.CompareExchange(ref _registrationGate, 1, 0)`, release it with `Volatile.Write(ref _registrationGate, 0)`, and wrap both `Register` and `DisposeAll`. Telemetry read accessors now acquire `_laneCount` with `Volatile.Read` before reading the function-pointer table. The lane dispatch path remains an unmanaged function-pointer table; the gate is cold registration/lifecycle only.
Rejected Alternatives: Using `lock`/`Monitor`; rejected because it adds managed synchronization to the audited registry path. Leaving the race documented only; rejected because the fix is local and does not cross domain boundaries.
Scalability potential: Low/MX350 avoids rare cold-start registry corruption under concurrent first-touch. Middle/High/Ultra can register more lanes without creating managed delegate tables or corrupting the dispatch slot index.
Hardware Impact: 0 us measured. Hot enqueue/dequeue path unchanged; cold registration now spins only during first lane registration or lifecycle dispose.

Problem: SPSC/MPSC constructors could leave a partially created native buffer if one `H8Memory.Allocate` succeeded and a later allocation failed. `SignalBus<T>.EnsureInitialized` also returned on failed `_ring.IsCreated` without cleaning a partial ring.
Solution: SPSC constructor disposes when `_buffer` or `_cursor` is absent. MPSC constructor disposes when `_buffer`, `_publishedTickets`, or `_cursor` is absent. `EnsureInitialized` now disposes and resets `_ring` on failed creation, and still releases the frame snapshot if writer-budget allocation fails.
Rejected Alternatives: Relying on `IsCreated == false` as proof nothing was allocated; rejected because a composite native container can be partially allocated while reporting unusable as a whole.
Scalability potential: Low/Middle avoid persistent native leaks during memory pressure. High/Ultra can raise lane capacity without making allocation-failure cleanup ambiguous.
Hardware Impact: 0 us measured. Failure-path cleanup only; no hot-path cost.

Problem: The scanner did not prove those fail-closed paths, so the correction could disappear into source without a durable audit artifact.
Solution: Extend `Tools/OOP_SignalSpsc_Scanner.py` with `failClosedHits` for registration gate, partial allocation cleanup, failed ring disposal, snapshot release, and async dump request. Current report remains `GREEN_STATIC_ONLY`.
Rejected Alternatives: Reporting fail-closed behavior in chat only; rejected because project rules require disk proof artifacts.
Scalability potential: No runtime effect. Prevents regression of failure cleanup during future lane-count or capacity scaling work.
Hardware Impact: 0 us player runtime; scanner/tooling only.

## 2026-05-25 APEX Clear Semantics / Registration Latch / Fuzzer Allocation Failure

Problem: `MpscSignalRingBuffer<T>.Clear()` reset `Tail` to zero and scrubbed all publication tickets. Under concurrent producers this can invalidate the CAS reservation monotonic counter and turn a fail-closed drop into cursor corruption. The ticket scrub is also O(capacity) work in a path used for overflow recovery.
Solution: Make SPSC and MPSC clear operations advance `Head` to the currently observed `Tail` with `Interlocked.Exchange(ref cursor->Head, tail)`. Do not reset `Tail`; do not loop through `_publishedTickets`.
Rejected Alternatives: Keeping tail reset because it makes the ring look empty in debugger; rejected because producer tickets must stay monotonic. Clearing every ticket on overflow; rejected because it creates O(N) work exactly when the system is already under pressure.
Scalability potential: Low/MX350 avoids a recovery-path capacity walk and avoids rare producer cursor corruption. Middle/High/Ultra can increase bounded lane capacity without making clear cost scale with capacity.
Hardware Impact: 0 us measured. Static effect removes an O(capacity) clear loop and preserves CAS reservation correctness; profiler proof absent.

Problem: `SignalBusRegistry.Register` returned `void`, while `SignalBus<T>.EnsureRegistered()` always latched `_registered = true`. If native dispatch storage allocation failed or lane capacity overflowed, a lane could believe it was registered while no dispatch slot existed.
Solution: Change `Register` to return `bool`; return false for null function pointers, dispatch storage failure, and lane overflow; latch `_registered` from that return value. Overflow now logs once with `Interlocked.Exchange(ref _registrationOverflow, 1)`.
Rejected Alternatives: Leaving the failure visible only through a log; rejected because the caller state would still be false-green. Throwing a managed exception; rejected by hot-runtime/fail-closed policy and IL2CPP/Burst boundary risk.
Scalability potential: Low/Middle avoid silent loss of lanes under memory pressure or over-capacity configuration. High/Ultra can raise lane count only with a real dispatch slot; false registration is gone.
Hardware Impact: 0 us measured. Cold registration only; hot enqueue/dequeue path unchanged.

Problem: The editor-only fuzzer allocated the ring and `seen` scratch but did not fail closed if either allocation failed. A verification harness that continues after invalid native allocation can create misleading RED/exception noise instead of a deterministic failure result.
Solution: Add an immediate allocation guard: if `!ring.IsCreated || !seen.IsCreated`, set `DroppedWrites` and `MissingWrites` to `expectedWrites`, set `StatusRed`, compute the result hash, and return.
Rejected Alternatives: Letting the fuzzer attempt to run and rely on managed exceptions or Unity safety checks; rejected because the harness is supposed to prove signal transport, not allocator crash behavior.
Scalability potential: No player runtime cost. The same fuzzer can now report deterministic allocation failure across low/middle/high/ultra editor configurations.
Hardware Impact: 0 us player runtime; editor-only fail-closed proof.

## 2026-05-25 APEX Dispatch Clamp / Writer-Side Payload Guard

Problem: `SignalBusRegistry.FlushRegisteredSignalLanes`, `CopyTelemetry`, and `TryCopyTelemetryAt` trusted `_laneCount` after native dispatch storage lifetime changes. If dispatch storage was absent or `_laneCount` was stale versus `_laneDispatch.Length`, a read accessor or flush could index invalid native storage instead of failing closed.
Solution: Guard `_laneDispatch.IsCreated` before reading dispatch entries and clamp dispatch iteration to `Math.Min(Volatile.Read(ref _laneCount), _laneDispatch.Length)`. `TryCopyTelemetryAt` now also rejects indexes outside native dispatch length.
Rejected Alternatives: Trusting the cold registration gate as sufficient. It protects normal mutation, but fail-closed rules require readers to survive stale state and partial teardown.
Scalability potential: Low/MX350 avoids crash-like behavior under memory pressure or lifecycle race. Middle/High/Ultra can scale lane count inside `LaneCapacity` without read paths assuming perfect dispatch storage state.
Hardware Impact: 0 us measured. Added only O(1) guards on diagnostic/flush access; hot writer ring path unchanged except for the sanitizer below.

Problem: `SignalBus<T>.TryEnqueueBounded` allowed job-produced payloads into the MPSC ring before the existing post-simulation sanitizer. Corrupt NaN payloads would be dropped during flush, but they still occupied ring capacity until then.
Solution: Run `SignalPayloadFiniteGuards.Sanitize(ref signal)` before writer-budget decrement. On non-zero guard code, increment `_corruptedSignalTotal` and return false. No `GlobalTelemetryBus.PublishMathGuardInvalidNumber` call is made in this writer route because that telemetry path touches thread/application/global bus state and is not acceptable inside Burst/job producers.
Rejected Alternatives: Calling `GlobalTelemetryBus` from the job writer path; rejected as managed telemetry coupling. Leaving sanitizer only at flush; rejected because corrupt payloads could consume bounded ring capacity for the frame.
Scalability potential: Low tier preserves scarce bounded capacity by rejecting invalid writes immediately. Middle/High/Ultra get the same deterministic drop semantics while using higher frame caps through `GlobalQualityWeight`.
Hardware Impact: 0 us measured. Static cost is one generic guard dispatch before accepted job writes; expected benefit is capacity preservation during corrupt producer storms. Burst compile proof remains absent.

Problem: The scanner did not encode these two failure invariants, so the report could stay green after a future regression.
Solution: Add `dispatch_storage_guard`, `dispatch_length_clamp`, `writer_sanitize_before_budget`, and `writer_corrupt_drop` to `Tools/OOP_SignalSpsc_Scanner.py`; regenerate report.
Rejected Alternatives: Manual audit only; rejected because this task requires repeatable static evidence.
Scalability potential: No runtime effect. It keeps future low/middle/high/ultra lane scaling from erasing failure guards silently.
Hardware Impact: 0 us runtime; tooling only.
