# DATA_MONOLITH_ARCHIVIST Rationale

Status: PENDING VERIFICATION

## Decision Log

### Loop 1 / Tasks 1-5

Problem: Whole-world save pressure still had no segmented chunk pager entry point.
Solution: Extended `IAsyncPersistenceService` with async page write/read/copy/telemetry/flush methods and implemented `H8BinaryWorldPager` as the registered `SaveManager` backend.
Rejected Alternatives: Static singleton pager and direct world-to-save references were rejected because 20+ agents are modifying adjacent systems; GlobalRegistry contract isolates the dependency.
Scalability potential: Low uses fixed-sector writes only; Middle/High keep background RLE enabled; Ultra spends the saved main-thread time on richer residency visuals instead of more IO truth.
Hardware Impact: i3/MX350 estimate: 300-2500 us main-thread stall avoided per dehydrated chunk versus monolithic pack/write; queue enqueue target below 15 us.

Problem: Prompt required a `ChunkDehydratedSignal`, but the project only exposed sector hydration/dehydration signals.
Solution: Added a 64-byte `ChunkDehydratedSignal` lane and mirrored residency dehydration into it with absolute AUP-derived `SectorHash`.
Rejected Alternatives: Reusing only `SectorDehydratedSignal` was rejected because it hides the persistence intent and makes save draining depend on ecology consumers.
Scalability potential: Low drains two signals per tick; higher tiers can raise the cap after profiler proof.
Hardware Impact: i3/MX350 estimate: <12 us for empty drain, bounded by fixed tick cap under burst dehydration.

Problem: A new paging assembly was required without creating an asmdef cycle into `SaveManager`.
Solution: Added `Hecton8.Core.Persistence.Paging` asmdef pointing to Contracts, with runtime integration kept in the main project assembly where existing native memory sentinels and service owners live.
Rejected Alternatives: Moving `SaveManager` into the paging asmdef was rejected because it would force broad reference surgery across bootstrap, inventory, caves, and UI.
Scalability potential: Clean contracts permit later Burst/job-side readers without dragging MonoBehaviours into the paging assembly.
Hardware Impact: Runtime cost is 0 us; compile isolation reduces future integration churn.

Problem: Pager needed one monolith handle, not per-chunk file churn.
Solution: Opened `world_data.h8bin` once with `FileOptions.Asynchronous | FileOptions.RandomAccess`; close/flush happens through service shutdown and application quit.
Rejected Alternatives: SQLite package import was rejected as third-party/package risk and unnecessary for the mandated `offset = SectorHash % MaxSectors * SectorSize` pager.
Scalability potential: Low/Middle/High/Ultra all use the same disk IO path; overkill tiers spend saved frame time on visuals.
Hardware Impact: i3/MX350 estimate: avoids OS directory/file churn and removes per-chunk open/close spikes, expected 200-1000 us saved during clustered dehydrates.

Problem: Dead-code hunt found PlayerPrefs and sync append use outside the Data Monolith ownership boundary.
Solution: The new pager path contains no PlayerPrefs and no append writes; unrelated UI/settings/dev/quest legacy hits are recorded but not rewritten in this domain pass.
Rejected Alternatives: Global PlayerPrefs purge was rejected because it crosses UI/settings/dev domains and risks breaking menu handoff without an assigned interface migration.
Scalability potential: Pager remains deterministic on all tiers; legacy cleanup needs a separate owner.
Hardware Impact: Pager path impact is 0 managed persistence allocations and no synchronous append stalls.

### Loop 2 / Tasks 6-10

Problem: Chunk pages need stable addressing across floating-origin shifts.
Solution: Voxel pages use the residency chunk id as the absolute AUP sector hash and write at `SectorHash % MaxSectors * SectorSize`; inventory and metadata use deterministic salted derivatives to avoid overwriting the voxel page while staying rooted in the same absolute hash.
Rejected Alternatives: Runtime position hashing was rejected because AUP rebases would change page addresses. A payload-type-only offset was rejected because it breaks locality.
Scalability potential: Low/Middle/High/Ultra share identical addressing. High tiers can add a directory/B-tree overlay later without invalidating sector pages.
Hardware Impact: i3/MX350 estimate: offset calculation is ~1 us and removes dictionary/file lookup on the main thread.

Problem: Dehydrated chunks need voxel and inventory state staged without pulling streaming into save internals.
Solution: `SaveManager` consumes `ChunkDehydratedSignal`, captures existing voxel RLE snapshots, copies player inventory shadow state, and enqueues both into native pager queues.
Rejected Alternatives: Residency-manager direct writes were rejected because streaming must not own save serialization. Managed DTO packaging was rejected for hot-path GC.
Scalability potential: Low drains two signals per tick; Middle/High/Ultra can raise the drain cap after profiler proof or batch multiple writes per worker loop.
Hardware Impact: i3/MX350 estimate: enqueue-only path stays under 40 us excluding existing snapshot capture; disk stall moved off main thread.

Problem: Disk writes must not block the main thread.
Solution: One background `Awaitable` drains native read/write queues, performs deterministic RLE compression, and writes fixed sectors through the persistent file stream.
Rejected Alternatives: `Task.Run` per page and coroutines were rejected due allocation/control loss. Per-sector file handles were rejected due microSD latency.
Scalability potential: Low keeps compression only when it wins; high-end devices keep the same IO but reclaim main-thread budget for visual overkill.
Hardware Impact: i3/MX350 estimate: saves 500-2500 us on burst dehydration by removing synchronous file positioning/write from the frame.

Problem: Streaming must request old diffs without waiting on disk.
Solution: `WorldChunkResidencyManager.RequestLoad` fires an async pager read ticket after a chunk is admitted to the native load queue.
Rejected Alternatives: Blocking load until disk returns was rejected; procedural pristine fallback remains the authoritative miss path.
Scalability potential: Low accepts misses cheaply; higher tiers can prefetch farther by issuing more tickets from predictive streaming.
Hardware Impact: i3/MX350 estimate: read-ticket enqueue ~4 us; no frame wait.

Problem: Loaded bytes must be consumable by Burst-side voxel/ecosystem paths.
Solution: Pager reads/decompresses into native fixed slots and exposes a `TryCopyCompletedPage` API that `MemCpy`s into caller-owned `NativeArray<byte>`.
Rejected Alternatives: Returning `byte[]` or managed streams was rejected because it allocates and blocks Burst interop.
Scalability potential: The same native payload can later be passed directly to voxel/ecosystem jobs once those owners consume the ticket.
Hardware Impact: i3/MX350 estimate: 20-80 us native copy for typical page payload; 0 managed allocations.

### Loop 3 / Tasks 11-15

Problem: Corrupt or partial sectors must not crash chunk streaming.
Solution: Every read validates magic/version/sector hash/payload type/CRC32. Missing/corrupt pages return a non-ready status and leave the existing residency/Addressables/procedural path untouched as the pristine fallback.
Rejected Alternatives: Exceptions, forced save repair, or blocking re-read loops were rejected because streaming must keep moving.
Scalability potential: Low silently falls back; Middle/High/Ultra can add visual masking or prefetch retries without changing the page contract.
Hardware Impact: i3/MX350 estimate: CRC costs ~35 us per typical 16 KB payload and prevents crash recovery stalls.

Problem: Main thread cannot wait for the IO worker.
Solution: Native queues and the completion map are protected with `SpinLock`; disk operations are only performed after `Awaitable.BackgroundThreadAsync`.
Rejected Alternatives: `lock` around main-thread disk calls and `Task.Wait` were rejected as frame-time violations.
Scalability potential: Low keeps one worker; high-end can batch more commands per worker loop if profiling proves it buys visuals.
Hardware Impact: i3/MX350 estimate: uncontended lock 2-6 us; disk wait removed from frame.

Problem: Floating-origin shifts must not invalidate sector files.
Solution: Page keys are absolute residency chunk hashes. Runtime transforms never enter the pager address.
Rejected Alternatives: Hashing `Transform.position`, camera-relative coordinates, or shifted local grid coords was rejected.
Scalability potential: All hardware tiers share identical persistence truth; no tier-specific key drift.
Hardware Impact: i3/MX350 estimate: no extra cost beyond offset math.

Problem: The prompt asks for Math LOD but disk IO has no visual approximation.
Solution: Marked Math LOD as N/A for persistence truth. Low/Middle/High/Ultra vary queue budgets and visual spend, not saved data correctness.
Rejected Alternatives: Dropping low-tier pages or reducing checksum coverage was rejected because saves are authoritative.
Scalability potential: Low gets visually nice fallback via existing pristine generation; Ultra uses saved time for overkill streaming masks/visuals.
Hardware Impact: i3/MX350 estimate: deterministic page format prevents cross-tier bugs.

Problem: Writer/reader paths must stay zero-GC.
Solution: The pager allocates fixed native arenas once, uses stackalloc headers, native maps/queues, and `UnsafeUtility` copies. Managed `FileStream` is a cold persistent handle only.
Rejected Alternatives: Managed `byte[]`, `MemoryStream`, LINQ, coroutines, and per-page task creation were rejected.
Scalability potential: Low avoids allocation spikes; Ultra can burn recovered time on richer effects instead of GC recovery.
Hardware Impact: i3/MX350 estimate: 0 B/frame hot path, avoiding GC spikes that would exceed the 0.1 ms suspicion threshold.

### Loop 4 / Tasks 16-18

Problem: Pager failures need post-mortem data, not speculation.
Solution: `H8BinaryWorldPager` keeps a 300-entry native telemetry ring and writes `Dump_DATA_MONOLITH_ARCHIVIST.bin` on corrupt reads. Public telemetry exposes `PendingDiskWrites` and `PageFaults`.
Rejected Alternatives: Debug.Log-only reports and unbounded text logs were rejected because they allocate and do not survive crash analysis reliably.
Scalability potential: Low devices keep minimal counters; Ultra can visualize page faults and IO debt in debug overlays without changing the data path.
Hardware Impact: i3/MX350 estimate: telemetry snapshot ~3 us; dump only on corrupt/failure path.

Problem: Saving notification must not spam HUD.
Solution: `SaveManager` emits a hash-only `HUDNotificationSignal` only when pending background writes exceed 10 and rearms after the queue drops.
Rejected Alternatives: Per-page notifications and string payloads were rejected for GC and UX noise.
Scalability potential: Low gets one restrained cue during IO debt; high-end can bind richer UI/VFX to the same signal if desired.
Hardware Impact: i3/MX350 estimate: threshold poll ~2 us, no strings.

Problem: Awaitable file-stream compilation needed verification despite stale dotnet project files.
Solution: Unity `validate_script` returned 0 errors for `SaveManager.cs`, `GlobalSignals.cs`, `WorldChunkResidencyManager.cs`, and `H8BinaryWorldPager.cs`; Unity console showed a Burst internal compiler exception unrelated to C# syntax.
Rejected Alternatives: Treating stale `dotnet build Assembly-CSharp.csproj` as authoritative was rejected because generated Unity csproj files did not include new assets and already fail on unrelated missing asmdef references.
Scalability potential: No runtime impact.
Hardware Impact: 0 us runtime.

### Loop 5 / Task 19

Problem: Persistent monolith handles can corrupt `world_data.h8bin` if left open during quit.
Solution: `SaveManager.OnApplicationQuit` calls `FlushWorldPager()` and then `ShutdownServiceState()`. `H8BinaryWorldPager.Dispose()` requests worker shutdown, waits bounded spins, flushes, disposes the stream, and releases native queues/arenas.
Rejected Alternatives: Relying on finalizers or `OnDestroy` only was rejected because domain reload and quit order are not a persistence contract.
Scalability potential: All tiers use identical close semantics. Low devices pay only shutdown cost; high-end devices retain the same deterministic close path.
Hardware Impact: Runtime frame impact is 0 us; shutdown cost is bounded and outside gameplay.

## OMEGA POLISH CHANGES

Problem: Sector and ring-slot addressing used honest modulo even though every count was power-of-two.
Solution: Replaced `% MaxSectors`, `% WriteSlotCount`, and `% ReadSlotCount` with bitmask addressing (`& mask`). This is the cinematic cheat: direct bucket snap instead of division.
Rejected Alternatives: Keeping modulo for readability was rejected; the pager is a hot bridge between streaming and disk.
Scalability potential: Low devices avoid integer division on every page; high-end devices spend the saved budget on richer streaming masks instead of IO math.
Hardware Impact: i3/MX350 estimate: 1-3 us saved during clustered dehydration/read-ticket bursts.

Problem: Anti-bloat audit required proof of no forbidden string/collection/math patterns in the new pager.
Solution: Scanned `H8BinaryWorldPager.cs` and `PersistencePagingContracts.cs` for `foreach`, `string.Format`, interpolation, `.ToString()`, `math.sqrt`, `math.normalize`, `PlayerPrefs`, append writes, `Task.Run`, and coroutines. No hits in the new pager/contract files.
Rejected Alternatives: Broad project cleanup was rejected because the repository contains unrelated third-party/legacy offenders outside this domain.
Scalability potential: New pager remains deterministic and 0-GC across tiers.
Hardware Impact: 0 B/frame.

Final Git Diff:
- Added `Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs`
- Added `Assets/_Project/Scripts/Core/Contracts/PersistencePagingContracts.cs`
- Added `Assets/_Project/Scripts/Core/Persistence/Paging/Hecton8.Core.Persistence.Paging.asmdef`
- Added `Assets/_Project/Scripts/Core/Persistence/Paging/PersistencePagingAssemblyMarker.cs`
- Modified `Assets/_Project/Scripts/SaveManager.cs`
- Modified `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`
- Modified `Assets/_Project/Scripts/Core/GlobalSignals.cs`
- Modified `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs`
- Modified `Docs/Tasks/Status_DATA_MONOLITH_ARCHIVIST.md`
- Modified `Docs/AgentLogs/Rationale_DATA_MONOLITH_ARCHIVIST.md`

Diff Caveat: Shared files already contained unrelated work from other agents, so raw `git diff --stat` over those paths includes pre-existing changes. Scoped Data Monolith edits are listed above.

Problem: Omega mandate required `dotnet build Hecton8.Core.csproj` proof after polish.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore` after the bitmask changes.
Rejected Alternatives: Claiming Unity validation as final master-grade proof was rejected because the Omega mandate explicitly names `dotnet build`; pretending stale csproj failures are pager-local was also rejected.
Scalability potential: No runtime effect. Verification remains blocked by generated project state and unrelated global compile dependencies.
Hardware Impact: 0 us runtime. Build result: 154 errors in stale/global references including `Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Physics.CCD`, `Hecton8.Audio.*`, inventory/ecosystem interfaces, and stale visibility of new pager assets. Final status: PENDING VERIFICATION.

## 2026-05-13 HARDENING RECHECK

Problem: Static review found `SaveManager.EnqueueChunkDehydrationPayloads` only queued inventory and metadata, so Task 7 was functionally incomplete.
Solution: Restored voxel delta enqueue by resolving the registered `VoxelDeltaProcessor`, capturing the existing native delta snapshot with `Allocator.Persistent`, copying it into the pager as `H8WorldPagePayloadTypes.VoxelDeltaRle`, and disposing the temporary native array immediately after the pager arena copy.
Rejected Alternatives: Editing voxel internals for a new chunk-local serializer was rejected because the voxel domain owns that data model. Direct writes from `WorldChunkResidencyManager` were rejected because streaming must not own save serialization.
Scalability potential: Low uses the existing sparse RLE snapshot and accepts procedural fallback on misses; Middle can raise dehydration drain caps after profiler proof; High/Ultra can replace the whole-snapshot API with a chunk-local voxel contract without changing pager sectors.
Hardware Impact: i3/MX350 estimate: 12-40 us enqueue path excluding existing snapshot capture; 0 B/frame after the transient snapshot is disposed. Current limitation: snapshot capture cost is owned by `VoxelDeltaProcessor` and still needs runtime profiling.

Problem: The pager had regressed back to a raw managed worker thread, violating the explicit Awaitable consumer requirement.
Solution: Removed `_workerThread`, `new Thread`, `Join`, and `RunWorkerLoop`; `StartWorker` now launches `RunWorkerAsync`, which switches to `Awaitable.BackgroundThreadAsync` before draining native queues.
Rejected Alternatives: Keeping managed `Thread` with join semantics was rejected because it bypasses Unity's Awaitable debt model. `Task.Run` was rejected for allocation/control loss.
Scalability potential: Low keeps one controlled worker; Middle/High/Ultra can increase batch work per loop after profiling without changing the main-thread API.
Hardware Impact: i3/MX350 estimate: 500-2500 us clustered dehydration disk stall remains off-frame; shutdown waits are bounded and outside gameplay.

Problem: Async read requests in `WorldChunkResidencyManager` were fire-and-forget, leaving completed pager results able to accumulate until the fixed result map filled.
Solution: Added a 16-ticket native retention ring and `TryRetireCompletedChunkPage`; `LateFrameTick` retires one completed/missing/corrupt result per frame without copying payload bytes.
Rejected Alternatives: Allocating managed ticket lists or copying every prefetch page into a scratch payload was rejected. Ignoring tickets was rejected because it converts async reads into permanent pager backpressure.
Scalability potential: Low retires one ticket per late frame; Middle/High can increase `PagerReadRetireBudget` after telemetry; Ultra can wire a true hydration consumer behind the same ticket contract.
Hardware Impact: i3/MX350 estimate: ~4 us typical ticket retire, prevents read-slot/result-map exhaustion during streaming bursts.

Problem: Worker shutdown without a raw thread handle needed bounded notification so native arenas are not freed while the worker is still draining.
Solution: Added a cold `Monitor.Wait`/`PulseAll` shutdown gate around `_workerRunning`; if shutdown still times out, the pager marks initialization fault and intentionally skips native disposal to avoid use-after-free.
Rejected Alternatives: Indefinite main-thread wait was rejected. Blind native disposal after a timeout was rejected because it risks background use-after-free.
Scalability potential: Same behavior on all tiers; low-end devices may leak pager native arenas only on pathological shutdown timeout rather than crash/corrupt save data.
Hardware Impact: 0 us during gameplay; bounded 250 ms shutdown wait only during quit/reinitialize fault paths.

Problem: Anti-bloat scan still found allocation-prone interpolation in a pager warning path.
Solution: Replaced the interpolated warning with explicit string concatenation and reran a case-sensitive scan for forbidden hot-path patterns.
Rejected Alternatives: Leaving it because it is cold was rejected; the prompt asked for strict zero-GC posture in the new pager.
Scalability potential: No tier difference; cold fault logging stays allocation-minimal.
Hardware Impact: 0 B/frame.

Problem: Fresh validation could not establish Unity compile truth.
Solution: Retried Unity MCP `validate_script` for the pager, contracts, SaveManager, GlobalRegistryContracts, and WorldChunkResidencyManager; every call returned `no_unity_session`. Ran `dotnet build Hecton8.Core.csproj`; it restored then failed on 95 global/generated-project errors.
Rejected Alternatives: Marking the work verified from static scans alone was rejected. Editing generated csproj files was rejected because they are Unity-generated and would create metadata churn.
Scalability potential: No runtime effect. Verification must resume when Unity MCP/session is available.
Hardware Impact: 0 us runtime. Current status remains PENDING VERIFICATION.

## 2026-05-13 SECOND DRIFT RECHECK

Problem: Current disk state did not match the previous hardening report; multi-agent edits had reintroduced raw worker-thread ownership in `H8BinaryWorldPager`.
Solution: Removed the live `_workerThread`, `new Thread`, `Join`, and `RunWorkerLoop` state again. The current worker is `RunWorkerAsync`, switches through `Awaitable.BackgroundThreadAsync`, pulses a bounded shutdown wait, and marks the pager initialization fault if a fatal worker exception escapes.
Rejected Alternatives: Trusting the old scan was rejected. Managed `Thread` ownership was rejected because the prompt requires the Unity Awaitable consumer path.
Scalability potential: Low keeps a single controlled worker; Middle/High/Ultra can batch worker loop drains later without changing the registry contract.
Hardware Impact: i3/MX350 estimate: 500-2500 us burst IO stall remains off-frame; fatal worker death no longer leaves the pager initialized but inert.

Problem: Current disk state also lost the voxel delta enqueue in `SaveManager.EnqueueChunkDehydrationPayloads`.
Solution: Reapplied voxel snapshot capture and `VoxelDeltaRle` pager write before inventory and metadata writes, then disposed the transient native snapshot after the pager arena copy.
Rejected Alternatives: Claiming Task 7 complete from stale rationale was rejected. Adding a new voxel serializer was rejected because voxel ownership is outside this agent domain.
Scalability potential: Low uses existing sparse RLE snapshot and pristine fallback; High/Ultra can later replace whole-snapshot capture with a chunk-local voxel contract behind the same page type.
Hardware Impact: i3/MX350 estimate: 12-40 us enqueue excluding existing voxel snapshot capture; 0 B/frame after disposal.

Problem: Validation truth changed after Unity MCP recovered.
Solution: Ran Unity `validate_script` on the five touched runtime scripts. All returned 0 errors and 0 warnings. Then checked console; whole-project compile remains blocked by an unrelated duplicate member in `SuitHUDV4CanvasOverlay.cs`.
Rejected Alternatives: Marking full project verified was rejected because the Editor console still has a compile error. Treating the unrelated UI error as a Data Monolith defect was rejected because it is outside the domain boundary.
Scalability potential: No runtime effect. The pager slice is script-clean; project-level verification requires the UI owner or integrator to clear the duplicate method.
Hardware Impact: 0 us runtime.

Problem: MCP validation itself logged a package regex timeout error during duplicate-method scanning.
Solution: Recorded it as a tool-side console artifact; it did not produce diagnostics for the touched scripts.
Rejected Alternatives: Clearing the console or hiding the artifact was rejected; objective logs matter more than clean optics.
Scalability potential: No runtime effect.
Hardware Impact: 0 us runtime.

## 2026-05-13 CORE MEMORY / REGISTRY COMPILE-MEDIC PASS

Problem: Unity console exposed `GlobalDataVault.cs` as a Core.Memory compile blocker, but current disk had drifted again during concurrent edits.
Solution: Re-read current `GlobalDataVault.cs`; confirmed the earlier signal/Burst/Core upward dependency was already gone, removed stale private constants left behind by the direct native gap audit, and avoided adding any reference from `Hecton8.Core.Memory` back to `Hecton8.Core`.
Rejected Alternatives: Adding `Hecton8.Core` to `Hecton8.Core.Memory.asmdef` was rejected because `Hecton8.Core` already references `Hecton8.Core.Memory`, creating an assembly cycle. Reintroducing GlobalSignals from Core.Memory was also rejected for the same reason.
Scalability potential: Low keeps deterministic direct gap scanning; Middle/High/Ultra can restore a Burst/job audit later only through a lower-level contract, not an upward Core dependency.
Hardware Impact: i3/MX350 estimate: 0 us runtime change; compile dependency repair only.

Problem: `H8Memory.cs` exposes `JobHandle` through a public release API while `Hecton8.Core.Memory.asmdef` did not explicitly reference `Unity.Jobs`.
Solution: Added `Unity.Jobs` to the Core.Memory asmdef references.
Rejected Alternatives: Relying on transitive package visibility was rejected because asmdefs must describe their own public surface.
Scalability potential: No runtime tier effect. The assembly boundary is clearer for future native-memory work.
Hardware Impact: 0 us runtime.

Problem: `HectonUnderwaterVisuals.cs` had duplicated hot-swap/cache blocks after registry interface churn, causing contradictory missing/duplicate listener compile states.
Solution: Removed the duplicate lower hot-swap listener blocks and kept the more complete implementation that refreshes cached registry services, player camera state, fluid runtime, atmosphere runtime, and depth zone state.
Rejected Alternatives: Removing `IGlobalRegistryHotSwapListener` from the class was rejected because the runtime caches registry services. Changing the core interface again was rejected because many systems already implement it.
Scalability potential: Low avoids stale service references after runtime replacement; high-end tiers keep richer underwater visuals bound to the same service-cache path without reflection or per-frame lookups.
Hardware Impact: i3/MX350 estimate: 0 us steady-state; hot-swap cost is event-only.

Problem: Full-project verification still could not be closed.
Solution: Validated `GlobalDataVault.cs`, `H8Memory.cs`, `PlayerKinematicsRuntime.cs`, and `HectonUnderwaterVisuals.cs` through Unity MCP where possible; then forced Unity refreshes and recorded the latest console/MCP state.
Rejected Alternatives: Marking `VERIFIED` from per-file validation was rejected because Unity MCP became not-ready after the last refresh and whole-project console truth could not be re-polled.
Scalability potential: No runtime effect.
Hardware Impact: 0 us runtime. Current status remains PENDING VERIFICATION.

## 2026-05-13 THIRD DRIFT RECHECK

Problem: Current `SaveManager.EnqueueChunkDehydrationPayloads` drifted back to inventory and metadata writes only, losing the mandated voxel delta page path.
Solution: Restored `VoxelDeltaProcessor.CaptureNativeSnapshot(Allocator.Persistent)`, registered the transient snapshot for native-memory tracking, wrote `H8WorldPagePayloadTypes.VoxelDeltaRle` at the absolute chunk sector hash, and disposed the temporary `NativeArray<byte>` in `finally` after the pager copied into its arena.
Rejected Alternatives: Inventory/metadata-only dehydration was rejected because Task 7 explicitly requires RLE voxel deltas. A new chunk-local voxel serializer was rejected because voxel data ownership belongs outside this domain.
Scalability potential: Low keeps existing sparse RLE snapshots and procedural fallback on misses; Middle can raise the dehydration drain cap after profiler proof; High/Ultra can replace whole-snapshot capture with a chunk-local voxel contract behind the same payload type.
Hardware Impact: i3/MX350 estimate: 12-40 us enqueue excluding current snapshot capture; 0 B/frame after snapshot disposal. The remaining capture cost is owned by `VoxelDeltaProcessor` and still needs runtime profiling.

Problem: Current `H8BinaryWorldPager` drifted back to raw managed thread ownership again, violating the Awaitable consumer task.
Solution: Removed `_workerThread`, `new Thread`, `.Join`, and `RunWorkerLoop`; `StartWorker` now launches `RunWorkerAsync`, which switches through `Awaitable.BackgroundThreadAsync` before queue draining. Shutdown still uses the cold bounded `Monitor.Wait`/`PulseAll` gate around `_workerRunning`.
Rejected Alternatives: Keeping `Thread` plus `Join` was rejected because the prompt names the Unity Awaitable consumer path. `Task.Run` remains rejected for allocation/control loss.
Scalability potential: Low keeps one controlled worker; Middle/High/Ultra can later tune queue-drain batch counts without changing the registry or page contract.
Hardware Impact: i3/MX350 estimate: 500-2500 us burst dehydration disk stall remains off-frame; shutdown wait stays outside gameplay.

Problem: Verification still cannot close objectively.
Solution: Static scans now show no forbidden worker/string/collection patterns in pager/contract files, `git diff --check` reports only CRLF normalization warnings, and `dotnet build Hecton8.Core.csproj --no-restore` was rerun to capture current blockers.
Rejected Alternatives: Editing generated `.csproj` files was rejected because Unity owns them. Claiming a Unity compile pass was rejected because Unity MCP returned `no_unity_session` for validation and console reads.
Scalability potential: No runtime effect. Verification must resume when Unity MCP is available and generated project files include the new pager assets.
Hardware Impact: 0 us runtime. Build result: 90 errors in stale/global references including `Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Physics.CCD`, `Hecton8.Audio.*`, inventory/ecosystem/audio/tether types, and stale visibility of `H8BinaryWorldPager`. Current status remains PENDING VERIFICATION.
