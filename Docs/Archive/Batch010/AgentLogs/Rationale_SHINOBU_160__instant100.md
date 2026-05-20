# Rationale_SHINOBU_160

Evidence State: PENDING_VERIFICATION / UNITY_IMPORT_ATTEMPTED / COMPILE_BLOCKED_BY_DEPENDENCIES

## Decision 2026-05-20-01 - Active Memory Reconstruction

Problem: Active status/rationale/log files for SHINOBU_160 were absent after Batch010 archival while active source and route docs still existed.
Solution: Restore active files from archive as a base, then add this current re-entry record. Treat current source and active `CURRENT_BATCH.md` as authority.
Rejected Alternatives: Trust chat summary; copy archive and leave it stale; ignore missing active logs.
Scalability potential: Low/Middle/High/Ultra behavior unchanged; process truth restored.
Hardware Impact: 0 us runtime.

## Decision 2026-05-20-02 - Frame Domain Repair

Problem: Runtime used `Time.frameCount` in mock seed, queue-processing frame index, telemetry frame, and dump throttle.
Solution: Use `DispatcherTimingDTO.FrameId`; if zero, advance an owner-local fallback counter. Session timestamp advances from dispatcher `FrameDelta`; worker raw payload may stamp wall-clock header off the main thread.
Rejected Alternatives: Keep Unity global frame reads; use main-thread wall-clock every POST_SIMULATION; change dispatcher contracts.
Scalability potential: Low avoids an unnecessary Unity time read; High/Ultra retain existing event density controls.
Hardware Impact: Static estimate <1 us/frame; determinism hygiene is the primary gain. Profiler proof pending.

## Decision 2026-05-20-03 - Deterministic Mock RNG

Problem: Mock analytics used a custom LCG seeded from Unity frame count, not the mandated deterministic RNG route.
Solution: Use `Unity.Mathematics.Random` seeded by `SystemHash ^ SectorHash ^ SimulationFrame`, with sector hash derived from mock AUP sector.
Rejected Alternatives: Keep LCG; use `UnityEngine.Random`; remove CI/editor fallback.
Scalability potential: Mock remains opt-in; Ultra stress can increase event density without gameplay truth dependence.
Hardware Impact: Mock-only path; no normal gameplay cost.

## Decision 2026-05-20-04 - Fail-Closed Ingress Ownership

Problem: `TryRecordEvent` could reach the static ingress queue when no active exporter owned it.
Solution: Return false immediately when `s_active == null`; active route still owner-thread gates and pressure-culls.
Rejected Alternatives: Static queue accepts stale writes; public `ParallelWriter` without producer fence.
Scalability potential: All tiers fail closed during teardown.
Hardware Impact: 0 us steady-state beyond existing branch; removes stale native write risk.

## Decision 00 - Native Analytics Route Shape

Problem: Analytics capture must accept deaths/resources/routes/perf spikes from many domains without making those domains depend on an exporter implementation or pay main-thread network/serialization cost.
Solution: Use unmanaged `AnalyticEventDTO` ingress through native queue/staging buffers owned by Vault handles, drain during POST_SIMULATION, hand fixed binary batches to a dedicated `H8_Analytics_IO` background thread.
Rejected Alternatives: `UnityWebRequest` or `HttpClient.SendAsync` from gameplay; `JsonUtility.ToJson`; per-event send; direct concrete references from KCC/combat/resource systems.
Scalability potential: Low drops routine analytics early and keeps critical flush; Middle retains route/death/resource summaries; High increases heatmap density; Ultra permits larger backlog and richer editor diagnostics without changing gameplay truth.
Hardware Impact: MX350/i3 gains are from removing JSON/network stalls from frame path. Static estimate before source fit: 100-5000 us avoided per telemetry burst, 0 B hot-path GC target.
First 20 Minutes moment: Proof/route-testability for Copper Wire route observations across swim, resource pickup, tool/craft transitions, hazards, deaths, and return-path routing.
Route impact: makes the route more testable and debuggable by exporting binary heatmap/critical analytics without adding main-thread network/compression work.
Proof required: clean Unity Console after foreign compile wall clears, Play Mode route run, profiler/GC capture, `.h8log` disk fallback fault test, and endpoint send/replay evidence.
Parked work rejected: no extra gameplay simulation, no live per-event analytics stream, no per-frame movement spam, and no unfenced cross-domain `NativeQueue.ParallelWriter`.

## Decision 01 - DTO Layout

Problem: AUP analytics can be corrupted by float world coordinates and ARM64 misalignment.
Solution: Implement `[StructLayout(LayoutKind.Explicit, Size = 32)]` with `uint EventHashID` at 0, `uint TimestampSeconds` at 4, `double3 EventAUP` at 8. Add editor/static layout validation with `UnsafeUtility.SizeOf` and `UnsafeUtility.OffsetOf`.
Rejected Alternatives: C# properties; sequential layout; `float3`; JSON object; packed layout.
Scalability potential: Same 32-byte truth record on all tiers. High/Ultra can add auxiliary health/debug buffers, not bloat the core DTO.
Hardware Impact: One 32-byte cache-friendly event minimizes memory bandwidth; estimated <1 us per ordinary enqueue under capacity on i3/MX350 pending profiler proof.

## Decision 02 - Compression Honesty

Problem: Task asks for LZ4 or similar unmanaged compression, but current mandate warns not to claim dictionary/native LZ4 without verified bindings.
Solution: Archaeology will verify existing LZ4 bindings. If native LZ4 binding exists, use it outside the main thread or a Burst-compatible compression job where available; otherwise implement a simple Burst RLE block compressor and label it as RLE, not fake LZ4.
Rejected Alternatives: `System.IO.Compression` in gameplay; fake LZ4; LZ4 dictionary without bound native API/corpus proof.
Scalability potential: Low uses smaller batches and aggressive culling; Middle/High/Ultra can spend background CPU on denser route payloads.
Hardware Impact: Compression stays off the gameplay thread or in Burst jobs; expected main-thread savings versus JSON/string compression remains >100 us per burst, pending measurement.

## Decision 03 - Vault ID Lane

Problem: Candidate `70840..70849` collided with active procedural wreckage and topographical sonar owner-local lanes.
Solution: Move SHINOBU_160 to owner-local `BufferID` casts `71860..71873` after focused source/architecture scan found no active Vault owner in that range. Do not add global enum members during concurrent buffer churn.
Rejected Alternatives: Reusing `70840..70849`; expanding `H8Memory.BufferID` with globally named analytics symbols; sharing CoreDiagnostics buffers from another route.
Scalability potential: Low uses the same 32-byte DTO ring at lower fill rates; Middle/High/Ultra raise retained event density without changing buffer identity.
Hardware Impact: Collision avoidance prevents false aliasing and defensive runtime guards. Estimated saved fault-repair time: 1,000,000+ us integration churn avoided; runtime frame saving is 0 us direct.

## Decision 04 - Queue Ownership Boundary

Problem: The assignment requires a `NativeQueue<AnalyticEventDTO>`, but current `IDataVault` exposes `NativeArray`/handle storage and has no queue allocation surface.
Solution: Vault owns persistent truth buffers, counters, tuning, staging, telemetry, CSV scratch, and heatmap debug. The ingress `NativeQueue` is exporter-owned, `NativeMemorySentinel` registered, and drained into Vault during POST_SIMULATION.
Rejected Alternatives: Pretending `GlobalDataVault` can allocate a queue; replacing the queue with a direct ring write and violating the explicit NativeQueue task; adding a broad IDataVault API mutation in a concurrent batch.
Scalability potential: Low pressure drains and culls early; Ultra can enqueue richer producer events while Vault remains the forensic source.
Hardware Impact: Queue hot enqueue remains unmanaged and pointerless. Estimated <1 us per normal enqueue on i3/MX350 pending profiler proof; DataVault ABI risk avoided.

## Decision 05 - Background Batch Transport

Problem: Per-event send or per-frame compression turns analytics into a hitch source.
Solution: POST_SIMULATION copies staged structs into a preallocated double handoff buffer. `H8_Analytics_IO` accumulates up to the byte threshold or 60 seconds, force-flushes critical hashes, RLE-compresses off-thread, posts with `HttpWebRequest`, and writes `.h8log` fallback files on failure.
Rejected Alternatives: `UnityWebRequest`; coroutine send; managed JSON; GZip on the gameplay thread; immediate packet per event.
Scalability potential: Low culls routine events before accumulation; Middle retains 5-second route samples; High/Ultra spend background CPU on denser telemetry and backlog retry.
Hardware Impact: Removes main-thread network/compression from telemetry. Static estimate: 100-5000 us avoided per telemetry burst, 0 B intended gameplay hot-path allocation.

## Decision 06 - Verification Guard Discipline

Problem: Compile verification is mandatory, but AGENTS forbids launching dotnet when CPU is above 50% or when `dotnet`/`csc` is already active.
Solution: Performed process and CPU guard checks before build. No `dotnet`/`csc` process was active, but CPU averaged `99.68%` on first check and `82.63%` on retry, so compile/test launch was blocked by policy and recorded as pending verification instead of falsifying success.
Rejected Alternatives: Starting dotnet under load; claiming compile success from static scans; ignoring the build guard.
Scalability potential: Low/Middle/High/Ultra behavior remains static-source implemented, but runtime proof is deliberately withheld until a legal compile window exists.
Hardware Impact: Avoids compounding current workstation contention. Runtime impact estimate remains unmeasured; claimed savings are static engineering estimates only.

## Decision 07 - Vault-Owned Worker Memory

Problem: The first static pass used private managed `AnalyticEventDTO[]` and `byte[]` arrays for handoff, accumulation, raw payload, compressed payload, and CSV readback. That kept gameplay hot capture clean but violated the stronger polish mandate's H-PHI wording for private array state.
Solution: Move handoff A/B, worker accumulation, raw batch scratch, compressed scratch, and dump snapshot into owner-local Vault buffers `71867..71873`, lock worker-owned buffers against compaction while `H8_Analytics_IO` is alive, and keep only `VaultBufferHandle<T>` fields in the runtime. CSV reads now stream directly into Vault `CsvScratch`.
Rejected Alternatives: Keep managed arrays as "background-only"; add a new `IDataVault` NativeQueue API during a concurrent batch; store long-lived private `NativeArray<T>` fields.
Scalability potential: Low and thermal-throttled devices shed routine events before the handoff buffers fill; Ultra keeps the same buffers but spends background CPU on denser payloads and backlog replay.
Hardware Impact: Removes large managed scratch arrays from exporter state and prevents GC heap pressure during analytics stress. Static frame estimate remains <1 us enqueue and 100-5000 us avoided versus main-thread JSON/network bursts; profiler proof pending.

## Decision 08 - POST_SIMULATION Burst Run Instead Of Fence

Problem: `IDispatcherSystem.PostSimulationTick` has no JobHandle return channel, so `Schedule().Complete()` inside POST_SIMULATION creates a needless scheduler fence while still blocking the main thread.
Solution: Use `IJob.Run()` for the two tiny POST_SIMULATION jobs. The jobs remain Burst-decorated with exact fast float directives, but the code no longer pretends an asynchronous dependency chain exists in a void post-phase callback.
Rejected Alternatives: Move the analytics drain into Simulation just to return a handle; keep `Schedule().Complete()`; hand-roll a managed loop and abandon Task 06/17 Burst job requirements.
Scalability potential: Low devices avoid scheduler overhead for small drains; High/Ultra still get Burst-compiled tight loops and larger retained event thresholds.
Hardware Impact: Expected scheduler overhead removal is single-digit to tens of microseconds per drain depending on worker load; unmeasured until legal profiler run.

## Decision 09 - Handoff Visibility State

Problem: The first double-buffer handoff made `Pending` visible before DTO copy completion. Because the worker wakes both by signal and by timeout, it could observe stale index/count or reset the batch before publication.
Solution: Add `BatchStateWriting` and publish with `Idle -> Writing -> Pending -> Idle`. The main thread copies the Vault handoff buffer, then writes index/count, then flips to `Pending` as the only worker-visible state.
Rejected Alternatives: Relying on `AutoResetEvent` ordering; adding a managed lock around the handoff; copying into private managed arrays again.
Scalability potential: Low/Middle/High/Ultra all keep the same O(N) copy cost with deterministic visibility. Higher tiers may stage more events, but no tier exposes torn handoff data.
Hardware Impact: Removes a race, not a steady-frame cost center. Expected runtime cost is one extra interlocked exchange per batch, under 1 us on i3/MX350; prevents dropped or duplicated batches under worker timeout pressure.

## Decision 10 - Producer Writer And Shutdown Fence

Problem: Public `NativeQueue<AnalyticEventDTO>.ParallelWriter` exposure had no producer-job fence/refcount, and shutdown could wait only 750 ms while `HttpWebRequest` was blocked.
Solution: Remove the public analytics parallel writer until a real producer-fence contract exists. Store the active background `HttpWebRequest`, abort it on the first failed join, then retry the bounded join before releasing Vault locks. Pointer-to-span conversion now lives inside safe-signature helper bodies.
Rejected Alternatives: Allowing cached cross-domain writers to survive exporter disable; unlocking Vault while a worker might still dereference buffers; waiting for the full network timeout; widening unsafe methods across the runtime surface.
Scalability potential: Low devices avoid unsafe producer fan-out; High/Ultra can still record denser events through the facade until a scheduled-producer route is designed.
Hardware Impact: Shutdown abort is not a per-frame path. Removing public writer avoids undefined native access under scene unload; estimated frame impact is 0 us steady-state, with a shutdown worst-case reduction from 30,000 ms network timeout to roughly 1,500 ms bounded join attempt.

## Decision 11 - Worker Cached Pointer Views

Problem: `VaultBufferHandle<T>.Resolve` routes through `GlobalDataVault.ResolveBuffer`, which reads Vault metadata maps. The background I/O thread must not touch those owner-side maps while the main thread may allocate, compact, or inspect Vault state.
Solution: Lock worker buffers at boot, then have `H8_Analytics_IO` create transient `NativeArray<T>` views directly from the cached pointer and length already stored in `VaultBufferHandle<T>`. Main-thread code still uses normal handle resolution; worker-owned `HandoffA/B`, `WorkerAccum`, `RawBatchScratch`, and `CompressedScratch` use `CreateLockedWorkerView`.
Rejected Alternatives: Continue resolving Vault handles on the background thread; copy back to managed arrays; add broad thread locks inside `GlobalDataVault`.
Scalability potential: Low devices keep metadata contention out of thermal survival frames; High/Ultra can push larger background batches without making Vault map lookups part of I/O cadence.
Hardware Impact: Avoids cross-thread NativeHashMap/map access and removes several metadata lookups per batch. Static estimate: 2-20 us saved per worker flush plus removal of a race class; profiler proof pending.

## Decision 12 - Compile Wall Dependency Block

Problem: Static source verification is insufficient, but Unity batchmode compilation failed before a clean SHINOBU_160 proof because unrelated domains already contain compiler errors and one Burst ILPostProcessor failure.
Solution: Keep SHINOBU_160 edits confined to the analytics/exporter domain, record `Docs/AgentLogs/Unity_SHINOBU_160_compile.log`, and classify the result as dependency wall. The log proves Unity imported `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs`; targeted log search found no `AsynchronousTelemetryExporter*.cs(` compiler errors. Blocking errors are in `Physics/HabitatFluidIncursionJobs.cs`, `World/ProceduralCoral/*`, `World/ProceduralWreckage/*`, `Narrative/Prologue/AwaitableDropSequenceDirector.cs`, and `Hecton8.MockDomain.Runtime` Burst ILPP.
Rejected Alternatives: Edit foreign domains without ownership; rerun compiles while Roslyn `VBCSCompiler.dll` remains active; report compile success from static scans.
Scalability potential: No runtime tier behavior changes. This decision protects the compile wall and keeps analytics architecture isolated until the foreign dependency wall is cleared.
Hardware Impact: Runtime frame impact is 0 us. Developer-hardware impact is avoiding redundant compile launches while an active compiler server remains; estimated avoided workstation contention is minutes of churn, not a gameplay microsecond saving.

## Decision 13 - Hot Recorder Pressure Gate

Problem: The static facade previously enqueued first, then let POST_SIMULATION cull later. It also touched Vault counters per event, forcing owner metadata work and races outside the exporter phase.
Solution: `TryRecordEvent` now rejects non-owner-thread calls, applies a continuous backlog pressure gate before enqueue, and stores accepted/dropped deltas in atomics. POST_SIMULATION flushes those deltas into Vault counters before the Burst drain. Critical hashes bypass routine pressure.
Rejected Alternatives: Public `NativeQueue` writer without producer fence; per-event Vault counter writes; drain-only culling after queue growth.
Scalability potential: Low devices hit the `math.lerp(low, ultra, smoothQuality)` threshold early and shed routine telemetry before queue memory grows; Middle keeps sparse route/resource summaries; High/Ultra retain denser routine analytics while critical failure data still bypasses.
Hardware Impact: Removes one Vault resolve/write from every accepted producer event and caps queue growth under pressure. Static estimate: 2-15 us saved per bursty producer frame versus per-event counter writes; profiler proof pending.

## Decision 14 - Worker-Owned Blackbox Dump Snapshot

Problem: Fault handling wrote black-box files from POST_SIMULATION through `Directory.CreateDirectory`, `FileStream`, and `File.Copy`. That can stall the frame exactly when analytics is faulting.
Solution: Add Vault buffer `71873` `DumpSnapshot` sized to `32 + 300 * 64` bytes. The main thread copies the fixed telemetry ring into that Vault snapshot and sets a pending dump state; `H8_Analytics_IO` writes `Dump_SHINOBU_160.bin` and `Dump_ANALYTICS_CRASH.bin`.
Rejected Alternatives: Main-thread fault FileStream; managed dump object; using worker-owned raw payload scratch for dump bytes and racing with telemetry sends.
Scalability potential: All tiers pay only a bounded memory copy on rare fault; disk I/O never enters the gameplay frame. Low devices keep fault reporting invisible unless the worker itself is dead.
Hardware Impact: Converts rare main-thread file I/O from milliseconds of potential stall into a 19232-byte memory copy plus worker-side disk write. Frame saving is fault-path only, unmeasured.

## Decision 15 - Disk Fallback Atomicity

Problem: Direct final `.h8log` writes can leave partial replay files after crash or disk-full. `Directory.GetFiles` allocates a string array per replay.
Solution: Write fallback payloads to `.tmp`, flush, then atomically rename to `.h8log`. Replay validates RLE/raw payload headers before send/delete and processes at most 8 files per flush using `Directory.EnumerateFiles` enumerator rather than full array materialization.
Rejected Alternatives: Direct final writes; replaying every `.h8log` blindly; unbounded backlog scan per successful send.
Scalability potential: Low devices bound replay work; High/Ultra can still drain backlog over multiple flushes without gameplay involvement.
Hardware Impact: Removes corrupt-file replay risk and caps worker-side disk scan. Main-thread impact remains 0 us because fallback stays on `H8_Analytics_IO`.

## Decision 16 - Assembly Internal API Avoidance

Problem: Subagent audit found `H8Memory.CreateNativeArrayView<T>` is `internal` in Core.Memory and may be inaccessible from SHINOBU once the foreign compile wall clears.
Solution: Replace the worker cached pointer view with `NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(handle.ptr, handle.Length, Allocator.None)` and assign a temp safety handle under `ENABLE_UNITY_COLLECTIONS_CHECKS`.
Rejected Alternatives: Change Core.Memory visibility; add `InternalsVisibleTo`; keep a likely `CS0122` risk.
Scalability potential: No tier behavior changes. It preserves the cached-pointer worker model without adding Vault metadata access.
Hardware Impact: Runtime cost should be equivalent to the prior view construction. The gain is compile-wall isolation and no Core.Memory API churn.

## Decision 17 - Compression Job Honesty

Problem: `CompressAnalyticsBufferJob` exists, but the live worker cannot safely schedule Unity Jobs from the dedicated I/O thread. Claiming the worker path executes Burst compression would be false.
Solution: Keep the Burst RLE job as an unmanaged kernel and test target, but document that live `H8_Analytics_IO` uses equivalent unmanaged span RLE off-main-thread. Do not move compression back to POST_SIMULATION just to claim Burst execution.
Rejected Alternatives: Main-thread compression; scheduling Unity Jobs from `H8_Analytics_IO`; managed GZip; fake LZ4 claim.
Scalability potential: Low sheds routine events before compression; High/Ultra spend background CPU on larger RLE chunks without touching frame time.
Hardware Impact: Main-thread compression remains 0 us intended. Worker CPU cost is background-only; packet size reduction remains data-dependent and unmeasured.
