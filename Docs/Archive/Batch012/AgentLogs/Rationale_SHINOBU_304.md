# SHINOBU_304 Rationale

Status: STATIC VERIFIED / BUILD BLOCKED BY CPU GUARD

## Decision 001 - Existing Owner Boundary
Problem: The task requests voxel A* but the repo already has `PathFunnelNavmeshRuntime` and `VoxelDynamicNavGridRuntime`; creating another hot-path manager would split authority.
Solution: Extend the existing AI pathfinding runtime with a partial file and keep world navgrid data as an input snapshot.
Rejected Alternatives: New `HectonVoxelAStarManager` was rejected because it creates a second owner and compile-wall risk. Editing `VoxelDynamicNavGridRuntime` directly was rejected because that is World-domain ownership.
Scalability potential: Low uses fewer node expansions and delayed completion; Middle keeps standard A* slices; High/Ultra can raise expansion budget and smoothing samples without changing gameplay truth layout.
Hardware Impact: On i3/MX350 the yield budget avoids frame stalls; expected savings are from replacing immediate full path solves with bounded slices, not from fake profiler numbers.

## Decision 002 - Data Route
Problem: Path requests/results must cross AI/fauna/steering boundaries without managed queues or hot registry polling.
Solution: Store request ring, solver state, heap, raw chain, result waypoints, tuning, species profiles, and telemetry in `GlobalDataVault` using explicit `BufferID`s owned by `SystemID.AIPathfinding`.
Rejected Alternatives: `Queue<PathRequest>` and managed `Path` objects were rejected due to GC and cache misses. `GlobalRegistry` hot polling was rejected by the authority boundary.
Scalability potential: Capacity scales by vault buffer length and continuous tuning values; no binary low/high route.
Hardware Impact: Flat arrays improve L1 behavior on MX350-class CPUs; expected hot path allocation stays 0 B by static design.

## Decision 003 - SDF String Pulling
Problem: Raw voxel A* paths are jagged and a 2D funnel projection is invalid in vertical caves.
Solution: Use 3D SDF line-of-sight string pulling over the raw voxel chain, sampling clearance along the segment before deleting intermediate nodes.
Rejected Alternatives: Bezier smoothing was rejected because it can cut through rock. Unity physics ray/sphere casts were rejected because they synchronize physics and violate the SDF authority route.
Scalability potential: Low reduces lookahead/sample count smoothly; Middle keeps moderate sample spacing; High/Ultra increases lookahead and sample density for cleaner creature motion.
Hardware Impact: Weak devices retain safe paths with fewer checks; high-end devices spend saved cycles on smoother movement, not gameplay authority divergence.

## Decision 004 - Physical Simulation Fake
Problem: Full cave collision probing for every path node would exceed the 0.1 ms suspicion line.
Solution: Treat the SDF scalar field as the collision proxy; clearance is a mathematical fake of physical volume checks.
Rejected Alternatives: Per-node `Physics.SphereCast` and explicit collision boxes were rejected due to main-thread sync and allocation risk.
Scalability potential: Low uses coarser slices and weighted A*; Middle uses standard weighted A*; High uses denser smoothing; Ultra uses maximum smoothing samples while consuming the same DTO layout.
Hardware Impact: i3/MX350 avoids physics-scene synchronization; exact microseconds require Unity profiler proof.

## Decision 005 - NativeMinHeap Over Uninitialized Vault Memory
Problem: A* open-set lookup cannot scan all open nodes every expansion and cannot allocate managed heap nodes.
Solution: Implement `NativeMinHeap` over `NativeArray<VoxelPathHeapNode>` plus `NativeArray<int>` heap positions, with search-stamped node records guarding uninitialized memory.
Rejected Alternatives: `SortedSet`, `PriorityQueue`, `List<Node>`, and full-buffer clears were rejected due to GC, boxing risk, and memory bandwidth waste.
Scalability potential: Low reduces node expansion budget but keeps heap correctness; Middle/High/Ultra increase budget without changing heap layout.
Hardware Impact: Heap operations touch compact 24B entries and integer positions; expected gain on i3/MX350 is lower cache miss rate versus managed nodes.

## Decision 006 - Dear-Lie Async Time Slicing
Problem: Long 3D cave routes can require thousands of expansions and must not block a frame.
Solution: Persist `VoxelPathSolverState`, node stamps, parent chain, heap count, and raw path buffers in the vault; each job expands only a continuous quality-scaled node budget, then yields.
Rejected Alternatives: Synchronous same-frame full solve and coroutine-managed search objects were rejected due to frame stalls and managed allocation.
Scalability potential: Low stretches a route over more frames; Middle computes at default budget; High/Ultra increases the expansion budget and smoothness.
Hardware Impact: On MX350-class hardware the expensive route becomes predictable work slices; wall-time proof pending Unity run.

## Decision 007 - Telemetry Timing Boundary
Problem: The mandate asks for exact Burst execution time, but Burst jobs cannot safely use managed `Stopwatch` inside the kernel and profiler counters are not available in this CLI pass.
Solution: Record node counts and flags inside Burst, then patch `BurstMicros` from owner-side schedule-to-finalize wall latency without forcing incomplete jobs. NaN or over-budget latency requests the 300-frame raw dump.
Rejected Alternatives: Fake per-node microsecond constants were rejected. Blocking `Complete()` to measure the job was rejected because it violates the non-blocking pathfinder requirement.
Scalability potential: Low latency captures longer async spans; High/Ultra can use the same telemetry DTO with profiler-grade integration later.
Hardware Impact: Telemetry patch is owner-side and bounded; exact kernel microseconds still require Unity profiler instrumentation.

## Decision 008 - Editor Tooling And Shared Report Collision
Problem: The required report path already contained another agent's untracked report; blindly overwriting would erase useful evidence.
Solution: Generate the SHINOBU_304 report at the mandated path and preserve a compact summary of the previous SHINOBU_307/303 report.
Rejected Alternatives: Skipping the report was rejected by Task 19. Embedding the entire previous report was rejected after PowerShell metadata inflated the file.
Scalability potential: Editor-only tooling does not affect runtime quality tiers.
Hardware Impact: No runtime hardware impact; scanner is editor/tool-only.

## Decision 009 - Heap Stamp And Phase Boundary Repair
Problem: `ShinobuVoxelPathHeapPositions` is allocated with `UninitializedMemory`; reading it as the first source of truth can corrupt decrease-key lookup on a fresh search. `FastTickVoxelAStar` also finalized scheduled jobs when they happened to complete between late-frame passes, creating a hidden main-thread completion site.
Solution: Move authoritative heap position into search-stamped `VoxelPathNodeRecord.HeapPosition`; `NativeMinHeap` reads positions only after `SearchId` validation and mirrors positions to the legacy int buffer for debug/interop. Clear transient yield flags at each resumed evaluation pass. Restrict `FastTickVoxelAStar` to scheduling only; completion and telemetry patching happen in `LateFrameTickVoxelAStar` after `IsCompleted`.
Rejected Alternatives: Clearing the entire heap-position array every search was rejected as a memory-bandwidth tax over the full voxel cell capacity. Keeping hot `TryFinalizeCompleted` was rejected because it can execute a completion fence outside the dispatcher-owned late-frame window.
Scalability potential: Low quality can stretch one route over more frames without stale yield flags contaminating final results. Middle/High/Ultra can raise node budgets while preserving heap correctness and the same DTO layout.
Hardware Impact: On i3/MX350-class CPUs the repair avoids full-buffer clear and removes a hidden completion stall. Expected savings are structural; exact microseconds still require Unity profiler or Burst timing proof after the compiler guard clears.

## Decision 010 - Telemetry And Smoothing Guard Tightening
Problem: `SmoothPathStringPullingJob` relied on caller-side SDF view validation, and telemetry `AverageNodesExpanded` stored total expanded nodes rather than a per-frame average.
Solution: Add an explicit `SdfDistances.IsCreated` gate to smoothing and compute `AverageNodesExpanded = total / elapsedSearchFrames` in both evaluation and smoothing telemetry.
Rejected Alternatives: Leaving telemetry as total was rejected because it makes the black-box field lie under long time-sliced searches. Throwing on missing SDF was rejected; jobs fail closed.
Scalability potential: Low quality longer searches now report a lower average instead of a misleading cumulative total; high quality larger budgets remain comparable through the same field.
Hardware Impact: One integer divide per telemetry write, outside the node expansion loop. No hot node cost.

## Decision 011 - Subagent Static Audit Repairs
Problem: Independent audit found hot DataVault reacquire/grow paths, WFC handle lookup in `FastTick`, endpoint-only LOS under one-sample smoothing, hash-modulo result overwrite, fixed waypoint output clamp, and editor writes without a writer fence.
Solution: Move base path-funnel handle acquisition to cold bootstrap/hotswap, cache the WFC grid handle cold, and keep hot views try-resolve/read-only without growth. Change result selection to linear probing with oldest-terminal eviction. Change LOS samples to interior points. Clamp smoothed waypoints by actual per-result segment capacity. Add `TryReadVaultBuffer` for read APIs and lock editor tuning writes with `TryAcquireWriteLock`.
Rejected Alternatives: Clearing or reallocating result tables per request was rejected as bandwidth waste. A global result queue was rejected because it creates a second authority route. Keeping modulo overwrite was rejected because completed paths can disappear under collisions. Editor raw mutation was rejected because it bypasses vault writer ownership.
Scalability potential: Low uses one interior SDF read per LOS segment and bounded node slices; Middle increases smoothing via continuous tuning; High/Ultra can consume larger waypoint segments and line samples without DTO layout changes or binary quality switches.
Hardware Impact: On i3/MX350-class hardware the hot metadata/grow branch is removed from `FastTick`; the added linear probe is admission/finalization-only and bounded by result capacity, not node expansions. Exact microseconds require Unity profiler after build guard clears.

## Decision 012 - Cold CSV Writer Fence And Waypoint Default
Problem: The cold CSV bridge mutated species profile buffers through a plain resolve path, and the public default waypoint cap still carried the old 512 value while the runtime arena was 4096.
Solution: Gate `TryLoadVoxelPathingProfiles` behind `GlobalDataVault.TryAcquireWriteLock`/`ReleaseWriteLock` for both profile payload and count buffers under `SystemID.AIPathfinding`. Raise `VoxelAStarConstants.DefaultWaypointCapacity` to 4096 so default tuning matches the segmented arena.
Rejected Alternatives: Keeping resolve-based writes was rejected because editor/cold mutation still needs explicit writer ownership. Leaving 512 as default was rejected because low result counts could silently cap smoothing below available arena capacity.
Scalability potential: Low still consumes the same DTO route and can emit fewer waypoints by tuning; Middle/High/Ultra can spend the full arena on cleaner cave motion without changing BufferIDs or save identity.
Hardware Impact: CSV writer locks are cold/editor-only and add 0 hot-frame cost. The waypoint default changes output capacity, not per-node A* expansion cost; profiler proof remains blocked by CPU/compiler guard.

## Decision 013 - Job-Owned Buffer Read/Write Fence
Problem: Static API audit found that public request enqueue, public result reads, editor tuning writes, and debug/telemetry reads could touch the same Vault buffers while evaluate or smoothing jobs were scheduled.
Solution: Add `IsVoxelAStarJobActive()` as a pure owner fence and fail closed for request enqueue/result reads while voxel jobs are active. Late-frame telemetry fault readback, editor telemetry graph, and debug gizmo now skip reads while jobs own their buffers. Tuner writes are deferred until no voxel job is active.
Rejected Alternatives: A managed staging queue was rejected because it reintroduces GC/managed ownership. Publishing a second result snapshot was rejected for this pass because it requires new BufferIDs and ABI/doc expansion; fail-closed preserves safety with minimal surface.
Scalability potential: Low stretches searches over more frames and therefore may reject/retry more enqueue/read attempts; Middle/High/Ultra finish jobs sooner via continuous node budget scaling. Authority route and DTO layout stay unchanged.
Hardware Impact: Runtime cost is a single branch on enqueue/result read and late-frame telemetry check. It prevents Unity safety-handle races without adding heap allocations or blocking `Complete()`.

## Decision 014 - Editor Scene-Search Eviction
Problem: The first concurrency fence used `Resources.FindObjectsOfTypeAll` in editor callbacks to discover active pathfinding runtimes. Editor-only is not gameplay-hot, but it normalizes scene scanning and can allocate during graph/gizmo repaint.
Solution: Maintain a static voxel-job active count in the runtime owner, incremented on evaluate/smooth schedule and decremented on late-frame finalize or teardown. Editor facades read that static count through `PathFunnelNavmeshRuntime.IsAnyVoxelAStarJobActive()`.
Rejected Alternatives: Keeping editor scene scans was rejected because it violates the compile-wall/scene-search discipline even outside gameplay hot paths. Adding a new Vault buffer for a one-bit editor fence was rejected as unnecessary ABI expansion.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; this is an ownership/read-fence cleanup only.
Hardware Impact: Removes editor repaint scene-search allocation pressure. Runtime adds one integer increment/decrement per scheduled job, not per expanded A* node.

## Decision 015 - Scanner Structural Pass
Problem: `OOP_NavMesh_Scanner` used direct string search over raw source. It could count forbidden tokens inside comments or string literals and did not prove Task 19's AST/structural intent.
Solution: Replace it with an editor-only lightweight syntax scanner: comments and string/char literals are stripped, type/method/invocation node counters are accumulated, and forbidden route regexes run over stripped code. It writes both the shared report and the stable SHINOBU_304 report.
Rejected Alternatives: Adding Roslyn to the isolated pathfinding editor asmdef was rejected because it expands assembly dependencies and compile-wall risk. Leaving raw `IndexOf` was rejected because the report could lie on comment/literal tokens.
Scalability potential: No runtime tier impact; this is a proof artifact.
Hardware Impact: Editor/tool-only. Runtime cost remains 0.

## Decision 016 - Static Job Fence Memory Ordering
Problem: The editor/runtime active-job bridge used a plain static `int`; this removed scene scans but did not state a memory-ordering contract for editor repaint/gizmo callbacks.
Solution: Read the bridge through `System.Threading.Volatile.Read` and mutate it with `System.Threading.Interlocked` on schedule/finalize/teardown paths only.
Rejected Alternatives: Keeping the plain integer was rejected because stale reads can re-open editor vault reads while a job is active. A Vault buffer for one counter was rejected as ABI expansion with no hot-path value.
Scalability potential: Low/Middle/High/Ultra path quality is unchanged; the fence protects tooling/readback correctness without changing gameplay truth or DTO layout.
Hardware Impact: Runtime pays one atomic increment/decrement per scheduled job. Per-node A* cost remains unchanged on i3/MX350-class hardware.

## Decision 017 - Raw Pointer NativeMinHeap
Problem: The prompt required `NativeMinHeap` over raw pointers; the previous heap was zero-GC but still carried NativeArray views inside the heap helper.
Solution: Convert `NativeMinHeap` to an unsafe pointer-backed view using `NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks`, while keeping NativeArray fields on `EvaluateVoxelPathJob` so Unity still tracks dependencies and lifetime.
Rejected Alternatives: A managed priority queue was rejected for GC. A NativeArray-only heap was rejected because it undershot the raw-pointer mandate. Fully unchecked pointer math was rejected because bounds checks are still required for memory safety.
Scalability potential: Low runs fewer expansions and therefore fewer heap operations; Middle/High/Ultra raise node budgets without changing the heap ABI or request/result DTOs.
Hardware Impact: Heap sift/pop/decrease operations avoid NativeArray wrapper access inside the helper. Expected benefit is small but localized to open-set churn; exact microseconds require Burst profiler after build guard clears.

## Decision 018 - BufferID Collision Repair
Problem: SHINOBU_304 used `71930..71946`, but `71940..71946` were already claimed by `ShorelineFoamConstants` ocean rendering lanes. That would alias unrelated Vault buffers by numeric ID.
Solution: Move the SHINOBU_304 central enum range to `73420..73436` and document the rejected `71930..71946` range in the binary payload ledger.
Rejected Alternatives: Keeping the collision was rejected because type names do not protect DataVault numeric identity. Moving into `729xx` was rejected because ocean/flora ranges already occupy the nearby space. Local casts were rejected because SHINOBU_304 already has central enum ownership.
Scalability potential: Quality behavior is unchanged. This repair preserves one-owner/one-route identity across all hardware tiers.
Hardware Impact: No speed claim. The fix prevents cross-domain memory corruption and invalid debug/proof data under shared Vault access.

## Decision 019 - External Compile Wall Containment
Problem: Guarded incremental solution build failed before SHINOBU_304 validation could complete because `Gameplay/Combat/CombatDamageRuntime.cs` references missing `ArmorProfileDTO`, `CombatStatusEffectState`, `ArmorPenetrationTelemetryEntry`, `ArmorPenetrationDebugHitDTO`, and `ArmorPenetrationTuningDTO`.
Solution: Classify the build as blocked by external Combat-domain dependency and record the raw log at `Docs/AgentLogs/Build_SHINOBU_304_loop14_incremental.log`.
Rejected Alternatives: Editing Combat DTOs from the voxel pathfinding domain was rejected as cross-domain sabotage. Relaunching repeated builds was rejected after the external error set was stable.
Scalability potential: No SHINOBU_304 quality behavior changed.
Hardware Impact: No runtime impact. The only cost was an incremental compiler pass; no SHINOBU_304 code errors were emitted in the captured error set.

## Decision 020 - Result Freshness Fence
Problem: A new request could be enqueued after the previous search finished, then public readers could see the previous terminal result for the same requester during the idle frame before the next time-sliced search wrote new state.
Solution: Invalidate same-requester terminal result slots to `Queued` on enqueue with finite/clamped radius, then write a `Searching` result record during `BeginSearch`. `TryReadVoxelPathResult` already returns only terminal statuses, so stale terminal paths are hidden without adding a second authority route.
Rejected Alternatives: A managed pending-result dictionary was rejected for GC and duplicate truth. Clearing the whole result table was rejected as bandwidth waste and would destroy other requesters' terminal evidence.
Scalability potential: Low quality stretches searches over more frames but now exposes no stale terminal result during those gaps; Middle/High/Ultra reduce the gap by higher node budgets. DTO layout, BufferIDs, and save identity are unchanged.
Hardware Impact: Bounded linear scan over result slots happens only on enqueue. Per-node A* heap/string-pulling hot cost remains unchanged.

## Decision 021 - Fault Dump Boundary
Problem: Subagent audit flagged managed file I/O in `TryDumpVoxelAStarBlackBox`.
Solution: Keep the dump as late-frame, post-job, fault-only diagnostic I/O because the mandate explicitly requires `Docs/AgentLogs/Dump_SHINOBU_304.bin` on NaN/over-budget fault. Normal route evaluation never calls this path, and it is gated after `IsVoxelAStarJobActive()` is false.
Rejected Alternatives: Removing the dump was rejected because it breaks the black-box mandate. Moving crash dump ownership to a new global logger was rejected as cross-domain expansion in this pass.
Scalability potential: Quality tiers are unchanged; the dump is proof traffic, not gameplay truth.
Hardware Impact: No hot-frame cost unless a fault occurs. Fault-frame stall remains a known diagnostic cost to buy post-mortem evidence.

## Decision 022 - Span Waypoint Read Accessor
Problem: `PathResultDTO` exposed `WaypointStart` and `WaypointCount`, but external consumers had no zero-GC public route to copy the smoothed `VoxelPathWaypointDTO` segment without reaching into the Vault directly.
Solution: Add `TryReadVoxelPathWaypoints(uint requesterEntityHash, Span<VoxelPathWaypointDTO> destination, out int waypointCount)`. It reads only terminal results, requires caller-owned memory, rechecks the active-job fence before copying, and returns the required count when the caller's span is too small.
Rejected Alternatives: Returning a managed array was rejected for GC. Exposing the raw waypoint `NativeArray` or handle was rejected because it leaks pathfinding Vault ownership across domains.
Scalability potential: Low quality emits fewer waypoints through tuning; High/Ultra can emit cleaner paths into the same caller-owned span. DTO layout, BufferIDs, and result identity are unchanged.
Hardware Impact: Cost is a bounded waypoint copy after path completion. A* expansion, heap operations, and string-pulling jobs are unchanged.
