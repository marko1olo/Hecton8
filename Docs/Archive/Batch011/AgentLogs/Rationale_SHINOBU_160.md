# Rationale_SHINOBU_160

Evidence State: PENDING_VERIFICATION / STATIC_POLISH_PASS / BUILD_BLOCKED_BY_DEPENDENCY_WALL

## Decision 2026-05-20-21 - Hot Ring Overflow Is Cursor Truth

Problem: After moving ingress to Vault rings, hot facade saturation returned `false` and incremented `_hotDroppedDelta`, while `AnalyticsIngressCursorDTO.RoutineOverflowDrops` / `CriticalOverflowDrops` only reflected mock job saturation. That split hid live ring saturation from the cursor/control row and would double-count if cursor overflow were added without changing the facade path.
Solution: Change `TryWriteIngressEvent` from bool to a three-state integer result: rejected, accepted, overflow. Ring-full paths increment the lane-specific cursor overflow field, update the cursor state hash, and return `IngressWriteOverflow`; `TryRecordEvent` returns false but skips `_hotDroppedDelta` for that case because `ProcessAnalyticsQueueJob` folds cursor overflows into `AnalyticsCountersDTO.DroppedEvents` exactly once.
Rejected Alternatives: Keep all hot saturation as generic drops; increment both hot dropped delta and cursor overflow; widen the drain budget to reduce overflow; allocate a managed diagnostic queue; add a new cross-domain pressure route.
Scalability potential: Low devices retain aggressive continuous culling and get exact ring-saturation proof without queue growth. Middle/High/Ultra can run denser telemetry and still identify the precise lane that saturated under network or worker pressure. The logic is continuous because quality/backlog gating still determines accepted pressure before the fixed ring path.
Hardware Impact: Hot accepted writes remain O(1). Hot full-ring rejection adds one cursor increment and one cursor hash, then defers counter folding to POST_SIMULATION. No GC, no `NativeQueue`, no main-thread network, and no build was launched under the user/process guard.

## Decision 2026-05-20-20 - Vault-Owned Ingress Rings

Problem: The active runtime still held routine and critical `NativeQueue<AnalyticEventDTO>` as persistent static exporter-owned state. Earlier rationale accepted this as an API compromise, but the current mandate and original task require a native DTO ring buffer in Vault and zero persistent private native collection ownership.
Solution: Remove `AnalyticsEventIngress` and all `NativeQueue<AnalyticEventDTO>` ownership. Add Vault buffers `71874 RoutineIngress`, `71875 CriticalIngress`, and `71876 IngressCursor`. `AnalyticsIngressCursorDTO` is an explicit 64-byte control row with routine/critical read/write cursors, capacities, overflow counters, frame, and hash. Hot facade writes to locked Vault pointers through `UnsafeUtility.AsRef<AnalyticsIngressCursorDTO>` and `AnalyticEventDTO*`; Burst mock/drain jobs receive `NativeArray` views with `[NoAlias]`.
Rejected Alternatives: Keep sentinel-registered queues; add a new `IDataVault` queue allocation API during a concurrent batch; resolve Vault metadata on every hot event; expose a `NativeQueue.ParallelWriter`; use managed queues or channels.
Scalability potential: Low devices retain the same continuous quality/backlog pressure shedding but now ingress memory is fixed and inspectable in Vault. Middle/High/Ultra retain denser route/resource/mock telemetry through larger quality budgets without changing memory ownership. Overflow is explicit cursor telemetry, not hidden queue growth.
Hardware Impact: Hot event path remains O(1) ring write and two cursor reads/writes, with no per-event Vault metadata lookup, no managed allocation, and no native queue block growth. Expected frame saving versus dynamic native queue pressure is unmeasured behind the foreign compile wall; static risk reduction is removal of a persistent private native allocation path and stricter cache-line cursor accounting.

## Decision 2026-05-20-19 - Worker Atomic State And Cold Guard Hardening

Problem: Static audit found remaining race and cold-guard gaps: worker flags used non-atomic read/modify/write, worker accumulation count was not published with a release write, facade-rejected non-finite AUP never incremented `NonFiniteEvents`, runtime startup relied on editor layout validation, and `NativeQueue` capacity was only documented through `NativeMemorySentinel`.
Solution: Add CAS-based `SetWorkerFlag`/`ClearWorkerFlag`, publish worker accumulation count through `Volatile.Write`, add `_hotNonFiniteDelta` flushed into Vault counters, call `AnalyticsLayout.ValidateOrThrow()` during cold `OnEnable`, and prewarm both routine/critical `NativeQueue` lanes through cold enqueue/dequeue.
Rejected Alternatives: Keep volatile read/modify/write; treat mock/worker counts as eventually visible; count facade NaNs as generic drops only; rely on editor tests for player runtime; replace the queue with a new Vault ABI during a concurrent batch.
Scalability potential: Low devices now see backlog/fault/non-finite pressure without weak-memory blind spots; Middle/High/Ultra retain the same dense telemetry route with stronger worker-state correctness. Queue prewarm moves native block growth to cold boot instead of gameplay pressure.
Hardware Impact: Worker flag CAS executes only on fault/state transitions; worker count publish is one volatile write per accumulation batch/flush; non-finite hot path adds two atomic increments only for invalid input; queue prewarm is cold boot cost. Normal valid telemetry frame cost remains unchanged.

## Decision 2026-05-20-18 - Mock Queue Pressure Accounting

Problem: The emergency mock generator wrote directly into the routine `NativeQueue<AnalyticEventDTO>` from a Burst job, bypassing the owner-local hot-path enqueued and pending backlog counters. Under CI/mock load, `ResolveBacklogPressureEvents()` could under-report the real queue pressure and delay continuous culling.
Solution: After `GenerateMockAnalyticsEventsJob.Run()`, add the generated `eventCount` into `_hotEnqueuedDelta` and `_ingressPendingEstimate` with `Interlocked.Add`. This keeps mock fallback load visible to the same pressure model used by live producers.
Rejected Alternatives: Leave mock events invisible because they are fallback-only; query `NativeQueue` count in the hot path; widen the drain budget; move mock generation to managed code; add a second mock-only counter route.
Scalability potential: Low devices now collapse mock density as soon as fallback telemetry creates queue pressure; Middle/High/Ultra still scale from 20 to 500 mock events/sec through `GlobalQualityWeight` without a binary tier switch.
Hardware Impact: Adds two atomic additions only once per mock generation burst, not per event and not in normal gameplay when mock is disabled. It prevents hidden queue growth during CI/fallback stress; measured microseconds remain pending behind the foreign compile wall.

## Decision 2026-05-20-17 - Continuous Burst Drain Decimation

Problem: The hot facade used continuous quality/backlog probability, but `ProcessAnalyticsQueueJob` still had a hard `pressureCull` branch that dropped every routine event once background backlog exceeded the threshold. This created a binary behavior inside the Burst drain and could erase whole routine samples during worker pressure.
Solution: Replace the boolean drain cull with a deterministic `dropMilli` probability derived from backlog pressure. `ShouldDropRoutineDuringDrain` hashes event type, timestamp, backlog, and full AUP double bits via `math.aslong` so Burst stays deterministic and spatially distributed.
Rejected Alternatives: Keep all-or-nothing drain culling; rely only on the producer facade; introduce mutable RNG state; use `UnityEngine.Random`; widen the drain budget under pressure.
Scalability potential: Low devices still shed aggressively, but retain a representative heatmap/resource sample; Middle/High/Ultra progressively retain more routine data through the same continuous curve. Critical telemetry remains uncullable.
Hardware Impact: Adds one O(1) hash only for pressured routine events in the drain job. It avoids losing analytical visibility while preserving bounded drain work; measured microseconds remain pending behind the foreign compile wall.

## Decision 2026-05-20-16 - HTTP Endpoint Scheme Gate

Problem: Endpoint configuration is cold CSV data. If a designer or CI config injects a non-HTTP URI, `WebRequest.Create` can produce a non-HTTP request type or throw before request setup, making every worker flush hit the generic failure path and hiding the real configuration fault behind repeated fallback writes.
Solution: Add `IsHttpEndpoint` and reject anything except `http://` or `https://` before creating `HttpWebRequest`. The worker records response code `-3`, increments fault telemetry, and routes the batch to disk fallback without touching Unity APIs or main-thread network work.
Rejected Alternatives: Trust CSV correctness; use `Uri.TryCreate` and broaden managed parsing; switch to `HttpClient`; allow `file://` or `ftp://`; perform validation on the gameplay thread.
Scalability potential: Low devices avoid repeated exception churn from malformed config; Middle/High/Ultra keep the same external analytics route but with a deterministic failure code and disk fallback.
Hardware Impact: 0 us gameplay frame cost. Worker path adds two ordinal prefix checks per network flush; avoids exception setup costs and opaque repeated worker faults for invalid schemes, unmeasured.

## Decision 2026-05-20-15 - HTTP Failure Response Disposal

Problem: `TrySendCompressedBatch` read `WebException.Response` as `HttpWebResponse` to preserve the HTTP status code, but did not dispose that response object on failure. Repeated endpoint failures could retain response/socket resources on `H8_Analytics_IO`, turning network fault handling into a slow resource leak.
Solution: Convert the exception response through `as HttpWebResponse`, write the status code inside a `try`, and always call `response.Dispose()` in `finally`. Null responses still write `-1`; generic send failures still write `-2`.
Rejected Alternatives: Ignore failed response disposal because it is off-main-thread; wrap the whole send path in `HttpClient`; move network retry logic to UnityWebRequest; drop the status-code telemetry; add broad managed retry queues.
Scalability potential: Low devices avoid background socket/resource accumulation during offline or captive-network play; Middle/High/Ultra retain the same dense telemetry behavior without leaking worker-side handles under endpoint failure storms.
Hardware Impact: 0 us gameplay frame cost. Fault-path worker overhead is one dispose call per failed HTTP response; expected to save unbounded resource churn over long sessions, unmeasured.

## Decision 2026-05-20-14 - AUP-Seeded Routine Cull Gate

Problem: Routine pressure culling hashed only event hash, timestamp, and backlog. Multiple route/resource observations with the same event type in the same second could share the same gate result and be dropped or retained as a cohort instead of producing a spatially distributed deterministic sample.
Solution: Pass `eventAup` into `ShouldAcceptHotPathEvent` and fold the three IEEE754 double lanes into the FNV-style gate hash before applying the `GlobalQualityWeight`/backlog drop probability. This keeps culling deterministic, allocation-free, and spatially distributed without introducing mutable RNG state.
Rejected Alternatives: Use `UnityEngine.Random`; add a per-event managed GUID; keep timestamp-only cohort culling; make a binary low-tier switch; add a global random state that rollback/replay cannot reproduce.
Scalability potential: Low quality now keeps a representative spatial sample under pressure instead of dropping whole same-second cohorts; Middle/High/Ultra retain denser route/resource heatmap detail through the same continuous curve.
Hardware Impact: Hot path adds three double-bit folds only when routine backlog pressure is already active. Expected cost is sub-microsecond per pressured routine event on desktop and low single-digit microseconds in synthetic bursts on weak CPUs, unmeasured; avoids analytics invisibility loss without touching critical events.

## Decision 2026-05-20-13 - Disk Fallback Unique Publication

Problem: `WriteDiskFallback` generated final filenames from `DateTime.UtcNow.Ticks` only and deleted `finalPath` before `File.Move`. If two worker writes share a timestamp string or a stale file exists, valid backlog data can be overwritten. If `.tmp` publication fails, temporary residue can remain outside replay scope.
Solution: Add a worker-side monotonic `_fallbackFileSequence` to the timestamp stem, create `.tmp` files with `FileMode.CreateNew`, remove the final-path delete, and cleanup `.tmp` on failed publication through a fault-counting helper. Existing `.h8log` data is no longer deleted by publication.
Rejected Alternatives: Trust timestamp uniqueness; keep deleting final `.h8log` before move; use GUIDs; leave `.tmp` cleanup to manual disk hygiene; move fallback writes to main thread.
Scalability potential: Low devices keep bounded disk fallback without overwrite loops; Middle/High/Ultra can queue denser analytics without making disk publication probabilistically lossy. Quality math and gameplay truth remain unchanged.
Hardware Impact: 0 us gameplay frame cost. Worker path adds one `Interlocked.Increment` and a slightly longer filename per fallback write; it prevents valid backlog loss and tmp residue under collision/fault conditions. Estimated steady worker overhead <1 us per fallback write plus filesystem cost, unmeasured.

## Decision 2026-05-20-12 - Disk Replay Partial Read Poison Cleanup

Problem: `TryFlushDiskBacklogUnchecked` handled zero-byte, invalid, and successfully replayed `.h8log` files, but a short read against the expected closed-file length returned immediately. Under atomic `.tmp -> .h8log` publication, that short read is a poison-file condition; returning silently can make the worker retry the same file after every later successful send.
Solution: Treat `read != length` as a worker fault, set `WorkerFlagFaulted`, mark the file with `deleteAfterRead`, and delete it only after the `FileStream` scope exits. Payload validation and resend are skipped for the partial buffer. The replay loop remains bounded to 8 files per flush and all work stays on `H8_Analytics_IO`.
Rejected Alternatives: Trust `FileStream.Read` to always fill the span; leave partial files on disk forever; delete while the stream is open; broaden stream sharing; move replay cleanup to the gameplay thread; perform multiple blocking read retries in the worker backlog pass.
Scalability potential: Low devices avoid repeated worker replay churn from corrupt fallback files; Middle/High/Ultra drain backlog progressively without turning disk cleanup into main-thread work. The quality curve and gameplay truth remain unchanged.
Hardware Impact: 0 us gameplay frame cost. Worker fault-path saving is bounded to poisoned files: avoids repeated open/read/return loops and preserves black-box fault visibility, estimated 100-3000 us per poisoned replay attempt, unmeasured.

## Decision 2026-05-20-11 - Disk Replay Delete After Close

Problem: `TryFlushDiskBacklog` deleted invalid `.h8log` files while their `FileStream` was still open with `FileShare.Read`. On Windows this can throw during the worker replay path and leave corrupt fallback files replaying every flush. Open/read/delete exceptions also bubbled to the outer worker loop.
Solution: Mark zero-byte, corrupt, and successfully replayed fallback files with `deleteAfterRead`, exit the `using FileStream` scope, then delete after the handle is closed through `TryDeleteReplayFile`. Wrap replay in `TryFlushDiskBacklogUnchecked` behind a fault-counting shell so poison files increment telemetry instead of killing `H8_Analytics_IO`. The replay loop remains bounded to 8 files per flush and still validates RLE/raw payload headers before send/delete.
Rejected Alternatives: Broaden the stream to `FileShare.Delete`; keep deleting inside the open stream scope; let worker-loop catch terminate replay on poison files; leave corrupt files on disk forever; move replay cleanup to the main thread.
Scalability potential: Low devices avoid worker retry churn from poison files; Middle/High/Ultra drain backlog progressively without turning disk cleanup into gameplay frame work. This does not change quality math or gameplay truth.
Hardware Impact: 0 us gameplay frame cost because the fix stays on `H8_Analytics_IO`. Worker-side savings are fault-path only: avoids repeated open/read/exception churn and thread restart/dead replay risk for corrupt fallback files, estimated 100-5000 us per poisoned replay attempt, unmeasured.

## Decision 2026-05-20-10 - Reflectionless Runtime Layout Guard

Problem: `AnalyticsLayout` validated DTO offsets through `typeof(T).GetField(...)` and `System.Reflection.FieldInfo`. This is cold boot/fatal-path code, but runtime reflection inside Core Diagnostics is unnecessary IL2CPP surface and violates the mandate direction when a pointer-safe unmanaged proof is available.
Solution: Replace generic reflection offset lookup with three explicit offset helpers for `AnalyticEventDTO`. Each helper creates a local unmanaged DTO and computes the field offset by subtracting `UnsafeUtility.AddressOf(ref owner)` from `UnsafeUtility.AddressOf(ref field)`. Editor tests keep `Marshal.OffsetOf` as an independent guard, but the runtime no longer uses reflection for the primary DTO layout.
Rejected Alternatives: Leave reflection because the path is cold; remove offset validation entirely and rely only on tests; add a broad Core layout helper during a concurrent batch.
Scalability potential: Low/Middle/High/Ultra behavior unchanged. This removes an avoidable AOT/reflection dependency from the boot guard while preserving the same ARM64 layout proof.
Hardware Impact: Runtime frame impact is 0 us; cold boot avoids reflection metadata lookup. Static estimate: 1-20 us cold-path reduction plus lower IL2CPP metadata surface, unmeasured.

## Decision 2026-05-20-09 - KCC Anchor Freshness And Mock Load Scaling

Problem: The status claimed KCC anchor refresh was independent from route heatmap emission, but the code still returned before reading KCC signals until the heatmap timer elapsed. The emergency mock path also injected a fixed 500 events/sec, which is acceptable for stress but not for a thermal/load-scaled fallback.
Solution: Read the latest KCC velocity signal every POST_SIMULATION call and update `_lastKnownPlayerAup` immediately when the AUP is finite; only route sample emission remains gated by `HeatmapSampleSeconds` and the heatmap flag. Scale mock event count continuously with `GlobalQualityWeight` from 20 to 500 and collapse toward 25 percent of that count under backlog pressure using `math.step`, guarded division, and smoothstep quality.
Rejected Alternatives: Leave death/perf anchors stale for up to the route timer; emit route samples every frame; keep a fixed CI/mock load; add a concrete KCC reference or a second gameplay route.
Scalability potential: Low emits sparse mock telemetry and preserves fresh critical anchors; Middle keeps normal 5s route review; High/Ultra can drive denser deterministic mock stress without changing gameplay truth.
Hardware Impact: Fresh KCC anchor scan is an existing typed signal span loop with no allocation. Mock fallback avoids up to 480 events/sec on low quality and collapses further under pressure; estimated avoided POST_SIMULATION drain/copy work is up to 5000-15000 us during synthetic stress, unmeasured.

## Decision 2026-05-20-08 - Partial Init And Deferred Teardown Hardening

Problem: The dual-lane ingress queue initializer allocated persistent native queues before both sentinel registrations were complete. The editor facade could also refresh before UI labels existed, and `OnDestroy` did not run cleanup if a previous `OnDisable` failed to stop the worker on its first bounded path but a later retry succeeded.
Solution: Wrap ingress initialization in rollback cleanup. Add `TeardownStoppedWorkerState()` and call it only after `StopWorker()` succeeds from either `OnDisable` or `OnDestroy`. Add cold editor null guards before telemetry labels are touched.
Rejected Alternatives: Clear `s_active` while `H8_Analytics_IO` might still hold locked Vault buffers; leave half-registered queues after sentinel failure; assume Unity editor update order. Standard Unity optimism is too brittle under scene unload and editor-domain reload.
Scalability potential: Low/Middle/High/Ultra runtime math is unchanged; this protects lifecycle correctness while the same continuous quality culling remains active.
Hardware Impact: No steady-frame cost. One cold branch in editor refresh and one cold rollback path. Estimated 0 us gameplay frame impact; prevents native leak and stale active-owner state during shutdown stress.

## Decision 2026-05-20-05 - Bounded Drain And Critical Lane

Problem: The prior queue processor staged up to the quality threshold, then drained the rest of the routine queue in the same POST_SIMULATION call to find critical overflow. Under telemetry stress this is O(total queued backlog) and can violate the invisibility mandate.
Solution: Split ingress into routine and critical `NativeQueue<AnalyticEventDTO>` lanes. `ProcessAnalyticsQueueJob` drains critical first, then routine, and all dequeue work is bounded by `drainBudget = min(stagingCapacity, round(lerp(10,1000,GlobalQualityWeight)))`.
Rejected Alternatives: Keep one queue and scan all overflow; allow critical telemetry to wait behind routine heatmap spam; add direct cross-domain event dependencies.
Scalability potential: Low drains about 10 records/frame and sheds routine events; Middle/High/Ultra raise the continuous budget toward 1000 without a binary tier switch.
Hardware Impact: Worst-case POST_SIMULATION cost is bounded by the quality curve instead of native queue backlog size. Static estimate: avoids millisecond-class drains during synthetic 500+/sec stress; profiler proof pending.

## Decision 2026-05-20-06 - Vault Memory Accounting Field

Problem: Task 16 requires reporting memory consumed by telemetry buffers, but the 64-byte telemetry DTO only exposed event/file/network counters.
Solution: Replaced the unused offset-60 field with `VaultBytes`, computed from the active `VaultBufferHandle<T>.Length * UnsafeUtility.SizeOf<T>()` for every SHINOBU_160 buffer lane and displayed it in the editor facade.
Rejected Alternatives: Add a second telemetry struct; store memory usage in docs only; repurpose `QueueDepthEstimate` and lose backlog visibility. The same pass rewired backlog readout to `ResolveBacklogPressureEvents()` so telemetry reports ingress pending + handoff + worker accumulation instead of only `_pendingBatchCount`.
Scalability potential: Low shows fixed memory pressure for thermal tuning; Ultra can justify larger buffers with explicit bytes instead of guesswork.
Hardware Impact: One cold arithmetic pass per telemetry write; expected <1 us. The value prevents hidden Vault growth on i3/MX350.

## Decision 2026-05-20-07 - Contract Signal Ingestion

Problem: The exporter had a native pipeline and public facade, but existing gameplay truth lanes for deaths/resources/perf were not bridged into analytics unless another domain explicitly called the facade.
Solution: In POST_SIMULATION, read existing contract `SignalBus` snapshots for `EntityDeathSignal`, `ItemAcquiredSignal`, `SurvivalVitalsChangedSignal`, and `FrameTimeSignal` with index loops. Convert AUP-bearing signals directly to `double3`; for player death/perf signals without AUP, use the last KCC AUP sample as a bounded observation anchor. KCC anchor refresh continues even when route heatmap emission is disabled, so critical/perf anchoring is not tied to route export density.
Rejected Alternatives: Add direct references to combat/resource/KCC implementations; wait for every producer domain to call this manager; sample GameObjects/transforms; invent a new signal lane during a concurrent batch.
Scalability potential: Low records sparse critical/resource events and sheds routine; Middle/High/Ultra retain more route/perf observations through the same quality curve.
Hardware Impact: Reads only current frame snapshots with simple loops; no allocations, no Unity object lookup. Static estimate: <10 us for normal sparse signal counts, profiler proof pending.

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
