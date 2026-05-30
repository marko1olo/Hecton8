# Rationale 1421 - Fluid CSR Logistics

Date: 2026-05-28
Status: PENDING VERIFICATION

## Decision 000 - Mandate Selection
Problem: Fluid pipe optimization touches flooding, logistics graph traversal, native memory ownership, ARM64 DTO layout, telemetry, registry boundaries, and zero-GC hot paths.
Solution: Read eight mandates before code: fluid incursion, logistics CSR graph flow, native memory/jobs, ARM64 layout, crash telemetry, registry/DI, zero-GC, visual-fake-first.
Rejected Alternatives: Reading only the batch prompt would miss DataVault and ARM64 constraints. Reading every mandate would waste time without task relevance.
Scalability potential: Low uses sliced logistics cadence and cheap visual fakes; Middle keeps CSR full graph but lower iterations; High increases cadence/iterations; Ultra spends saved CPU on presentation-only overkill, not gameplay truth bloat.
Hardware Impact: Expected low-end i3/MX350 gain is from replacing pointer-chasing/dynamic frontier growth with sequential NativeArray scans. Static estimate only; no profiler proof yet.

## Decision 001 - Initial Hygiene
Problem: Status_1421.md and Rationale_1421.md were absent at session start.
Solution: Create fresh files and mark all task states as PENDING VERIFICATION.
Rejected Alternatives: Proceeding with chat memory only violates anti-amnesia protocol.
Scalability potential: File-backed state prevents duplicated work after context compaction and supports staged verification loops.
Hardware Impact: No runtime impact; process hygiene only.

## Decision 002 - Do Not Rewrite Existing CSR Sump Solver
Problem: The prompt described an object graph with dynamic queues, but current source already stores the sump topology in GlobalDataVault CSR buffers and schedules Burst jobs over NativeArray views.
Solution: Preserve the existing CSR authority route: PumpNodes, PipeEdges, CsrOffsets, CsrDestinations, CsrConductance, CsrFlow, CsrFlatEdgeIndex, and CsrWriteCursor. Patch only proven defects.
Rejected Alternatives: A destructive rewrite would duplicate buffer ownership and risk breaking an existing DataVault route without evidence of managed graph traversal.
Scalability potential: Low uses fewer solve passes and longer cadence; Middle/High/Ultra increase passes/cadence while retaining identical truth layout.
Hardware Impact: Avoided churn on MX350/i3. Static estimate: preserves existing sequential access; no measured profiler delta.

## Decision 003 - Base Flood BFS Is Adjacent And Already Fixed-Queue CSR
Problem: Flooded-base BFS could have been the prompt target, but `HabitatDirtyRegionRebuildJob` already uses `EdgeOffsets`, `EdgeDestinations`, `TraversalQueue`, `VisitStamp`, and `IslandIds`.
Solution: Document it as compliant and leave it untouched.
Rejected Alternatives: Replacing the BFS with another BFS would increase regression surface with no deleted allocation.
Scalability potential: Low can slice dirty node seeds; Ultra can process more dirty islands per frame without changing the queue model.
Hardware Impact: Existing fixed queue prevents dynamic allocation; static scan found no NativeList/NativeQueue in the inspected flood BFS route.

## Decision 004 - Quality Weight Defect In Sump Runtime
Problem: `SlowTick` resolved a real `GlobalQualityWeight` but cadence used an authoritative constant through `ResolveAuthoritySolveCadenceSeconds()`. Pressure pass count was also hard-coded to two passes.
Solution: Replace cadence with `ResolveSolveCadenceSeconds(quality)` and add `ResolveDrainageDeltaPassCount(quality)` from 1 to 4 passes using smoothstep.
Rejected Alternatives: Keeping fixed cadence/pass count violates the continuous scalability mandate. Binary low/high mode was rejected.
Scalability potential: Minimum Survival gets 1 pass and slower cadence; Middle gets 2; High gets 3; Ultra gets 4 and fast cadence. Truth ownership and DTO layout stay constant.
Hardware Impact: Low-end i3/MX350 can skip 1-3 pressure passes per solve versus Ultra, saving static work proportional to node count. No measured profiler proof yet.

## Decision 005 - Blackbox Route Correction
Problem: Existing dump path was `Dump_1306_Construction_SumpPump.bin`, not the assigned `Dump_1421_FluidLogistics.bin`.
Solution: Change the runtime dump path and mark dump headers with `DumpedBlackBox`.
Rejected Alternatives: Leaving the stale path would misroute crash evidence and violate assignment-specific forensic ownership.
Scalability potential: No runtime solve effect; dump remains one-shot background write.
Hardware Impact: No frame impact in normal path. Crash path copies at most 64 + 300*64 bytes into a preallocated byte buffer.

## Decision 006 - Stress Harness Scope
Problem: Task 16 requires a 10000-node/30000-edge proof, while runtime capacities remain 2000/6000 for production sump buffers.
Solution: Add an editor-only test that directly allocates larger TempJob arrays, builds CSR with `BuildCsrPipeGraphJob`, schedules `EvaluatePipePressureDeltaPassJob`, and validates finite pressure output.
Rejected Alternatives: Raising runtime production capacities would alter memory budget and route authority without a design request.
Scalability potential: Low production remains capped; test-only stress proves algorithmic scaling headroom without increasing runtime resident memory.
Hardware Impact: No runtime impact. Editor test allocation is outside gameplay hot path.

## Decision 007 - Build Gate Blocked
Problem: Compilation gate requires CPU below 50% and no compiler process. CPU sample returned 93%; compiler process list was empty.
Solution: Do not launch dotnet build. Mark Task 15 as BLOCKED_BY_CONTENTION and rely on static inspection.
Rejected Alternatives: Running dotnet build under 93% CPU violates the batch CPU guard and risks blocking sibling agents.
Scalability potential: Process-level only.
Hardware Impact: Avoided additional CPU contention on the shared host.

## Decision 008 - Fluid Pipe Edge-List Locality Debt
Problem: The sump route was already CSR, but the adjacent `FluidPipePressureSolveJob` still scanned a flat edge-list via `ConnectionSources` and `ConnectionDestinations`. That is zero-GC, but it forces every solve to walk the whole connection set instead of touching contiguous per-node spans.
Solution: Add DataVault CSR buffers `PipeConnectionOffsetsBufferId=72101`, `PipeConnectionCsrDestinationsBufferId=72102`, and `PipeConnectionWriteCursorBufferId=72103`; schedule `BuildFluidPipeCsrJob` only when topology is dirty; feed `ConnectionOffsets` and `ConnectionCsrDestinations` into the pressure job.
Rejected Alternatives: Keeping the edge-list scan because it already avoided GC was too weak for the cache-locality requirement. Rebuilding CSR every solve was rejected because unchanged topology should not pay the prefix-sum cost.
Scalability potential: Low devices pay CSR rebuild only after node/pipe edits and then solve by contiguous offsets; Middle/High/Ultra spend saved traversal time on faster cadence and richer presentation signals, not extra gameplay truth.
Hardware Impact: Expected i3/MX350 benefit is proportional to avoiding full edge-list scans on nodes with few local connections. No profiler proof yet; Unity execution is still pending.

## Decision 009 - Continuous Fluid Pipe Cadence
Problem: `FluidPipeGraphRuntime.SlowTick()` used `AuthoritativeCadenceSeconds` directly, ignoring the global continuous quality scalar.
Solution: Resolve `HomeostasisBrain.GlobalQualityWeight`, sanitize to 0..1, and call `FluidPipeGraphConstants.ResolveCadenceSeconds(float)`.
Rejected Alternatives: Binary low/high mode and fixed cadence were rejected. The legacy `ResolveCadenceSeconds(FluidPipeMathLod)` overload remains only as a compatibility shim.
Scalability potential: Minimum Survival runs at 1.0 s cadence, mid quality resolves around 0.525 s, High approaches 0.1 s, Ultra runs 0.05 s. DTO layout and authority route do not change.
Hardware Impact: Low-end devices reduce solve frequency by up to 20x versus Ultra pipe pressure cadence. This is static math, not measured profiler data.

## Decision 010 - APEX Compile Throttle Recheck
Problem: The APEX pass touched job signatures and lock masks, making compilation desirable but still gated by host contention.
Solution: Re-sampled CPU after the CSR patch. CPU was 85%, compiler process list was empty, so no dotnet build was launched.
Rejected Alternatives: Running build under 85% CPU would violate the explicit user/developer guard.
Scalability potential: Process-level only.
Hardware Impact: Avoided additional shared-host contention; runtime impact absent.

## Decision 011 - Habitat Flood Budget Was Still Fixed Ultra
Problem: A follow-up domain sweep found `HabitatGraphManager.ApplyHydrodynamicStress` feeding graph flood incursion with a fixed `HectonQualityTier.Ultra`. That violated the continuous scalability pillar for flooded-base traversal load even though the underlying flood jobs already used fixed queues and CSR-style graph arrays.
Solution: Resolve `HomeostasisBrain.GlobalQualityWeight`, pass it into `ApplyGraphFluidIncursion`, and compute `ResolveGraphFloodNodeBudget(float)` with a smooth 64..512 node budget. The flood job data path remains fixed-queue and DataVault-backed; only the per-tick work budget changes.
Rejected Alternatives: Keeping fixed Ultra would punish low-end devices. A binary `if (lowEnd)` gate was rejected because the project requires continuous quality. Rewriting all tier-based HabitatGraphManager analytical modules in this pass was rejected because several routes are outside the 1421 pipe/sump/flood budget authority and need separate owner review.
Scalability potential: Minimum Survival processes 64 graph flood nodes per tick; Middle trends near 288; High/Ultra approach 512. Cheap devices get bounded flood work; top-tier machines spend budget on quicker flood propagation and presentation stability without changing DTO identity or save authority.
Hardware Impact: Static low-end i3/MX350 benefit is proportional to skipping up to 448 graph-node visits per flood tick versus the previous fixed Ultra route. No profiler measurement exists yet.

## Decision 012 - APEX Compile Throttle Recheck After Habitat Patch
Problem: The habitat flood budget patch is small but touches a live runtime method, so compile/test would be useful if allowed.
Solution: Re-sampled CPU after the habitat patch. CPU was 87%, compiler process list was empty, so no dotnet build was launched. A final lightweight gate sample later returned CPU 100% with an existing `dotnet` PID 57416, reinforcing the no-build decision.
Rejected Alternatives: Running build under 87-100% CPU or while another dotnet process exists would violate the explicit compilation throttling rule and interfere with parallel agents.
Scalability potential: Process-level only.
Hardware Impact: Avoided additional shared-host contention; runtime impact absent.

## Decision 013 - Strict Finally Release For Fluid Pipe Completion
Problem: `FluidPipeGraphRuntime.CompleteSolve` released the solve lock mask after `DispatcherJobSwap.TryComplete`, but the release was not syntactically inside a `finally` block. That was not acceptable for the APEX evidence rule even though the normal branch released.
Solution: Capture `_solveLockMask` before completion, set a `completed` flag only after `TryComplete` succeeds, and release the captured mask inside `finally`.
Rejected Alternatives: Leaving the release as a straight-line call was too weak for the lock proof. Releasing before job completion was rejected because the job still owns mutable DataVault views.
Scalability potential: No visual or quality change; this is failure-mode hardening.
Hardware Impact: Normal runtime cost is one `uint` local and one `bool` branch. No measured profiler proof.

## Decision 014 - Sump Schedule Failure Must Complete Pending Chain Before Unlock
Problem: Wrapping `ScheduleDrainageSolve` in `finally` would be unsafe if a partial job chain had already been scheduled and then a later schedule/register step failed; locks cannot be released while a pending job may still touch the buffers.
Solution: Track the latest pending `JobHandle` during the schedule chain. On failure, `finally` completes that pending chain, then calls `UnlockJobBuffers()`. On success, the normal LateFrame completion path still owns the unlock.
Rejected Alternatives: Blind `finally { UnlockJobBuffers(); }` was rejected because it could release DataVault locks while worker jobs still run. Running a same-frame complete on every success was rejected because it would destroy async scheduling and frame pacing.
Scalability potential: No quality change. It preserves async work on success and only pays completion cost on failure cleanup.
Hardware Impact: Normal path adds a `JobHandle` local and two booleans. Failure path may block to complete pending work, which is correct fail-close behavior. No measured profiler proof.

## Decision 015 - Compile Throttle After Lock Patch
Problem: The lock-finally patch makes compilation desirable, but the shared host remained busy.
Solution: Re-sampled CPU twice after the lock patch. Samples were 69% with existing `dotnet` PID 54548 and 100% with existing `dotnet` PID 30776. No build was launched.
Rejected Alternatives: Running dotnet build above 50% CPU or while another dotnet process exists violates the explicit throttling rule.
Scalability potential: Process-level only.
Hardware Impact: Avoided additional shared-host contention; runtime impact absent.

## Decision 016 - Remove Residual Habitat Tier Plumbing
Problem: After the flood-budget fix, `HabitatGraphManager` still used private `HectonQualityTier` routing for analytical hull stress and module stress shader upload. That kept a binary-ish quality route in the flooded-base presentation path.
Solution: Replace private tier plumbing with `float globalQualityWeight`. Analytical stress now uses `ResolveAnalyticalDetailWeight(float)` and scales current/reinforcement detail continuously. Module stress shader displacement now uses `ResolveModuleStressDisplacementMaxMeters(float)`, and compromised signals receive a 0..255 quality profile byte derived from the same scalar.
Rejected Alternatives: Keeping the tier bridge was rejected after the APEX sweep. Computing expensive current/reinforcement at quality 0 and multiplying by zero was also rejected; the detail path now skips that work below epsilon. Changing the external `BaseModuleCompromisedSignal.LowTierVisualOnlyFlag` name was rejected because it is an existing signal contract outside this owner.
Scalability potential: Minimum Survival uses depth-only analytical stress and zero displacement. Middle smoothly increases detail and deformation. High/Ultra approach full current/reinforcement contribution and module stress displacement overkill.
Hardware Impact: Low-end i3/MX350 avoids ambient-current/reinforcement detail when `GlobalQualityWeight` is effectively zero. No profiler measurement exists yet.

## Decision 017 - Compile Throttle After Habitat Tier Patch
Problem: The Habitat tier removal touched runtime private methods and should be compiled when allowed.
Solution: Re-sampled CPU. CPU was 88% with existing `dotnet` PID 45520. A final gate sample later returned CPU 100% with no compiler/dotnet process. No build was launched.
Rejected Alternatives: Running dotnet build at 88-100% CPU or while another dotnet process exists violates the explicit throttling rule.
Scalability potential: Process-level only.
Hardware Impact: Avoided additional shared-host contention; runtime impact absent.

## Decision 018 - Shared Low-Mask Vault Guard For Fluid Logistics
Problem: The first APEX lock-flattening pass used high-bit mutation guards. `GlobalDataVault` blocks writer-lock conflicts through low 32-bit active-lock lanes, so high-bit guards only protected relocation/compaction and were too weak as an integrator proof.
Solution: Use one shared low-mask guard `0xFFFFF2FF` in both `FluidPipeGraphRuntime` and `SumpPumpPipeGridRuntime`. The mask covers pipe buffer lanes 72080..72103, sump buffer lanes 95820..95845, fluid compartment lanes 70780..70781, and power lanes 70850/70857. Sump hot job windows no longer take per-buffer `TryLockBuffer`; they resolve handles only after acquiring this one guard.
Rejected Alternatives: Keeping separate high-bit guards was rejected because pipe and sump could overlap with two active guards. Restoring many per-buffer write locks was rejected because it recreates the deadlock surface.
Scalability potential: Low devices fail closed under contention instead of stacking locks; higher devices still run the same CSR data path when the domain guard is available.
Hardware Impact: Normal path replaces 20+ buffer lock/unlock calls with one atomic guard acquisition/release. No profiler measurement exists.

## Decision 019 - Cached Hatch Presentation Component
Problem: `HabitatGraphManager.PublishEmergencyLockdownState` called `TryGetComponent` inside the per-node emergency lockdown loop.
Solution: Cache `TransitionHatchMeshState` in `ModuleRecord` during cold module population and use the cached reference in the hot loop.
Rejected Alternatives: Leaving `TryGetComponent` in the loop violates the cold-lookup rule. Building a new registry was rejected because module records already own the cold cache.
Scalability potential: Low devices avoid component lookup during flooded-base presentation sync; higher devices spend saved time on continuous flood/stress presentation.
Hardware Impact: Static saving is one component lookup per active module per lockdown publish. No profiler measurement exists.

## Decision 020 - Compile Throttle After Integrator Patch
Problem: The lock and component-cache patches should be compiled, but the host was under active compiler load.
Solution: Re-sampled CPU and compiler processes. CPU was 100%; `csc` PID 55420 and `dotnet` PID 50684 were active. No build was launched.
Rejected Alternatives: Running `dotnet build` at 100% CPU with active compiler processes violates the explicit throttling rule.
Scalability potential: Process-level only.
Hardware Impact: Avoided additional shared-host contention; runtime impact absent.

## Decision 021 - Cadence Retry And Habitat Guard Flattening
Problem: Shared DataVault guards can reject a sump solve schedule under contention; clearing `_solveAccumulator` before schedule success silently drops a drainage tick. Habitat graph, flood-room, and module-stress grouped paths also held 4-9 write locks simultaneously.
Solution: Make `ScheduleDrainageSolve` return schedule success and clear `_solveAccumulator` only on success. Replace grouped Habitat write-lock chains with one low-lane mutation guard per group: graph `0x0000000000000FF8`, room `0x0000000080000007`, stress `0x0000000078000000`, flood propagation combined `0x0000000082000FFF`.
Rejected Alternatives: Retrying with immediate same-frame completion was rejected because it would destroy async pacing. Keeping the Habitat multi-lock chains was rejected because strict lock flattening requires one write exclusion token per grouped view, not cascading locks. Holding separate summary, room, and graph exclusion tokens for flood propagation was rejected as the same deadlock vector under a different name.
Scalability potential: Low devices retain cadence pressure under contention instead of losing solve windows; middle/high/ultra retain the same CSR/flood data path and use freed lock overhead for visual presentation, not extra gameplay authority.
Hardware Impact: Sump low-end i3/MX350 avoids starvation from transient guard contention. Habitat grouped paths replace up to 9 write-lock acquisitions/releases with one guard acquisition/release. No profiler measurement exists.

## Decision 022 - Compilation Throttle After Combined Flood Guard
Problem: The combined flood propagation guard changed scheduling and finalize ownership, so compilation would be useful if allowed.
Solution: Re-sampled CPU and compiler processes. CPU was 100%; no `dotnet` or `csc` process was active. No build was launched because the explicit CPU threshold is below 50%.
Rejected Alternatives: Running `dotnet build` at 100% CPU would violate the throttling rule even without an active compiler process.
Scalability potential: Process-level only.
Hardware Impact: Avoided adding compile load to a saturated shared host.

## Decision 023 - Fluid Schedule Retry And Dump Route
Problem: `FluidPipeGraphRuntime.SlowTick` cleared `_solveAccumulator` before `ScheduleSolve` proved that the DataVault guard and job chain were acquired. `ScheduleSolve` also advanced `_frameIndex` and `_telemetryCursor` while constructing the job, before schedule success. The pipe black-box dump path still used `Dump_1306_Construction.bin`.
Solution: Make `ScheduleSolve` return schedule success, clear `_solveAccumulator` only on success, and advance frame/telemetry counters only after the job is scheduled. Correct the dump path to `Dump_1421_FluidLogistics.bin`.
Rejected Alternatives: Dropping cadence under contention was rejected because it hides missed fluid simulation windows. Advancing counters before schedule success was rejected because failed schedule attempts should not consume telemetry identity.
Scalability potential: Low devices under contention retry without losing cadence; middle/high/ultra keep identical CSR truth and only vary cadence through `GlobalQualityWeight`.
Hardware Impact: Normal path adds two int locals and one bool return. Static hot scan found no reference allocations. No profiler measurement exists.

## Decision 024 - Hatch Presentation VISUAL_SYNC Split
Problem: `PublishEmergencyLockdownState` applied hatch mesh/root presentation inside graph/flood paths. That mixed simulation state publication with visual presentation work.
Solution: Keep `BaseModule.SetEmergencyBulkheadLockdown` immediate as gameplay state, but queue hatch adjacent flags as bytes in `ModuleRecord` and flush `TransitionHatchMeshState.ApplyAdjacentFlags` only from `FlushVisualSync`. Cache `MeshFilter` in cold `Awake`/editor `OnValidate` so hot visual sync does not resolve components.
Rejected Alternatives: Delaying emergency gameplay lockdown was rejected because it can affect authority behavior. Keeping mesh/root swaps in flood path was rejected because presentation belongs in visual sync.
Scalability potential: Low devices can coalesce multiple graph/flood state changes into one visual flush; higher tiers still get immediate visual correctness after the settled phase.
Hardware Impact: Adds two fields to managed `ModuleRecord` and one dirty flag. Flush path is a bounded for-loop over the existing module list with no heap allocation. No profiler measurement exists.

## Decision 025 - Final Integrator Audit Boundary
Problem: The worktree contains many unrelated modified files from parallel agents, so a global proof would mix domains and produce false ownership.
Solution: Re-read the 1421 prompt, status, and rationale, then audit only the four 1421 source files and 1421 ledgers. Use focused text scans for forbidden hot-path tokens and CPU/compiler gate samples before any possible build.
Rejected Alternatives: Claiming a repository-wide compile result without running it was rejected. Running `dotnet build` at 76% or 97% CPU was rejected by the explicit `<50%` throttle.
Scalability potential: No runtime behavior change; this preserves domain isolation while keeping fluid/sump/habitat flood code verifiable.
Hardware Impact: No runtime impact. Build CPU contention avoided on the shared host; post-patch samples stayed above threshold at 100%, 54%, and 59%.

## Decision 026 - Sump Teardown Guard Finally Closure
Problem: `TryFinalizeMockSeedNoWait` released the shared DataVault guard after `ClearRuntimeScalarBuffers` as straight-line code, and forced mock/solver teardown relied on caller cleanup. That did not satisfy the strict APEX proof form for guard release.
Solution: Wrap mock seed finalize, forced mock seed teardown, and forced solver teardown state mutation in local `try/finally` blocks that call `UnlockJobBuffers()` after the relevant job has completed.
Rejected Alternatives: Keeping caller-level cleanup was rejected because local ownership is easier to prove and survives future caller edits.
Scalability potential: No quality route change. Low devices under teardown or DataVault hot-swap cannot strand the shared guard if scalar cleanup is interrupted.
Hardware Impact: Normal gameplay path unchanged except mock-seed finalize teardown safety; no profiler measurement exists.

## Decision 027 - Adjacent Logistics Pipe Scheduler Flattening
Problem: `LogisticsPipeTransportScheduler` remained inside the pipe domain and still used a managed `List<LogisticsPipeNode>` plus seven chained `TryAcquireWriteLock` calls for CSR sort buffers 72054..72060. That violated the lock-flattening proof and kept O(n) `RemoveAt` shifting in the shared slow-tick scheduler.
Solution: Replace the registry with a fixed 128-slot `LogisticsPipeNode[]`, swap-remove dead entries, and track `_activeTopologyVersion` so completed topological orders are only replayed when the active node set is unchanged. Replace the seven writer locks with one low-lane mutation guard `0x000000001FC00000`; all sort NativeArray views are resolved under this one guard and released through completion/failure/teardown paths.
Rejected Alternatives: Leaving `List<T>` was rejected because hot compaction should not shift a managed collection. Canceling every pending sort on register/unregister was rejected because it would force avoidable same-frame completion. Keeping seven write locks was rejected because it recreates the deadlock vector already removed from sump/habitat paths.
Scalability potential: Low devices avoid O(n) removal shifts and lock cascades; Middle/High/Ultra keep the same CSR topological order route and spend any saved work on cadence/presentation, not extra authority.
Hardware Impact: Normal slow-tick removes up to seven write-lock acquire/release pairs from the sort path and bounds registry mutation to O(1) swap-remove. No profiler measurement exists.

## Decision 028 - Compile Throttle After Scheduler Patch
Problem: The adjacent scheduler patch changes runtime C# and should be compiled when allowed.
Solution: Re-sampled CPU and compiler processes. CPU was 77%, then 59%; no `dotnet` or `csc` process was active. No build was launched because the explicit CPU threshold is below 50%.
Rejected Alternatives: Running `dotnet build` at 77% or 59% CPU violates the compilation throttling rule.
Scalability potential: Process-level only.
Hardware Impact: Avoided adding compile load to a saturated shared host.

## Decision 029 - Pipe Scheduler Topology Signature
Problem: The sorted logistics-pipe order was versioned only by active node register/unregister. Endpoint changes or rupture state changes could invalidate the DAG without changing the active registry count or version.
Solution: Add a cached `SchedulerTopologyKey` to `LogisticsPipeNode` and compute a zero-GC int signature during scheduler refresh. `LogisticsPipeTransportScheduler` now replays a completed sorted order only when active count, registry version, and topology signature all match.
Rejected Alternatives: Completing and rebuilding the sort immediately on every endpoint refresh was rejected because it would force avoidable same-frame work. Hashing strings or managed IDs was rejected because hot scheduling must stay allocation-free.
Scalability potential: Low devices fall back to current active order only during real topology churn; Middle/High/Ultra regain sorted DAG replay after the next background sort without corrupting cargo order.
Hardware Impact: Adds integer arithmetic per active pipe during existing scheduler refresh. It prevents stale-order correctness faults without allocating memory or adding scene lookups.

## Decision 030 - Logistics Route Scratch Guard Flattening
Problem: `LogisticsRouteScratchMemory` still held seven DataVault write locks for CSR route scratch buffers 72032..72038. This was adjacent to the pipe logistics domain and recreated the lock chain removed from sump and pipe scheduler paths.
Solution: Replace the seven writer locks with one mutation guard `0x000000000000007F`. Handle ensure and NativeArray resolution now occur while the guard is held; `BaseLogisticsNetwork.TryResolveNearestStorageEndpoint` continues to release through its existing `finally`.
Rejected Alternatives: Keeping per-buffer writer locks was rejected because nested lock chains are the deadlock vector under DataVault alias lanes. Moving route scratch to managed arrays was rejected because the BFS route already has CSR-compatible unmanaged scratch.
Scalability potential: Low devices avoid lock cascades during storage-route BFS. Higher tiers keep the same CSR route and can spend saved contention budget on visual/production cadence elsewhere.
Hardware Impact: Replaces seven lock acquisitions/releases with one guard acquisition/release for route BFS scratch. No profiler measurement exists.

## Decision 031 - Compile Throttle After Route Scratch Patch
Problem: The route scratch lock patch changes runtime C# and should be compiled when allowed.
Solution: Re-sampled CPU and compiler processes. CPU was 99%, then 93%; no `dotnet` or `csc` process was active. No build was launched because the explicit CPU threshold is below 50%.
Rejected Alternatives: Running `dotnet build` at 99% or 93% CPU violates the compilation throttling rule.
Scalability potential: Process-level only.
Hardware Impact: Avoided adding compile load to a saturated shared host.

## Decision 032 - Siege Snapshot Lock De-Nesting
Problem: `HabitatGraphManager.PublishSiegeTargetSnapshot` acquired graph write views and then a separate siege-target write buffer, although the graph data was read-only node flags. That violated the one-write-token proof form.
Solution: Move siege snapshot publication outside graph mutation-guard windows. It now reads `HabitatGraphNodesBufferId` through a read-only Vault view and acquires only the siege-target write buffer. Dead flood-propagation-summary write-lock state was removed.
Rejected Alternatives: Keeping graph write views for read-only flags was rejected because it nests ownership tokens. Copying node flags to a managed temporary array was rejected because it would add allocation/GC risk.
Scalability potential: Low devices avoid an unnecessary graph guard during siege snapshot publication; higher tiers keep identical snapshot data and use saved lock slack elsewhere.
Hardware Impact: Removes one graph mutation guard from siege target snapshot generation. No profiler measurement exists.

## Decision 033 - Compile Throttle After Siege Lock Patch
Problem: The siege snapshot lock patch changes runtime C# and should be compiled when allowed.
Solution: Re-sampled CPU and compiler processes. CPU was 81%, then 100%; no `dotnet` or `csc` process was active. No build was launched because the explicit CPU threshold is below 50%.
Rejected Alternatives: Running `dotnet build` at 81-100% CPU violates the compilation throttling rule.
Scalability potential: Process-level only.
Hardware Impact: Avoided adding compile load to a saturated shared host.

## Decision 034 - Fixed Pump And Endpoint Registries
Problem: Two 1421-adjacent registries still used managed collection objects after the CSR and scratch-buffer work: `WaterPumpModule` used `List<WaterPumpModule>` and `BaseLogisticsNetwork` used `List<StorageEndpoint>`, `List<FabricatorEndpoint>`, and `List<RecyclerEndpoint>`.
Solution: Replace the pump registry with `WaterPumpModule[32]` plus explicit count/slot tracking. Replace logistics endpoint registries with fixed endpoint arrays and explicit counts. Endpoint removal preserves order with bounded array compaction because storage fallback order can affect deterministic crate selection; pump removal uses slot tracking because pump order has no external contract in current source.
Rejected Alternatives: Keeping managed `List<T>` was rejected because capacity prewarming still leaves a managed collection abstraction in the route/pump domain. Using swap-remove for storage endpoints was rejected because `TryResolveFirstStorageEndpoint` depends on stable registration order when CSR route fallback is used.
Scalability potential: Low devices avoid managed list mutation and endpoint list metadata during registration churn; middle/high/ultra keep identical gameplay selection semantics and spend saved contention budget on presentation/cadence elsewhere.
Hardware Impact: Removes managed `List` registry objects from pump and endpoint ownership. Storage/fabricator/recycler unregister remains O(n) but bounded at 64/32/32 and allocation-free. No profiler measurement exists.

## Decision 035 - Compile Throttle After Registry Patch
Problem: The pump/endpoint registry patch changes runtime C# and should be compiled when the shared host permits it.
Solution: Re-sampled CPU and compiler processes. CPU was 100%; no `dotnet` or `csc` process was active. No build was launched because the explicit CPU threshold is below 50%.
Rejected Alternatives: Running `dotnet build` at 100% CPU violates the compilation throttling rule.
Scalability potential: Process-level only.
Hardware Impact: Avoided adding compile load to a saturated shared host.

## Decision 036 - Route BFS Power CSR Reuse
Problem: `BaseLogisticsNetwork.TryResolveNearestStorageEndpoint` still rebuilt route BFS CSR by walking `PowerGrid.TopologyNodes` and `PowerNode.Neighbors`, preserving a managed topology walk beside the zero-GC route scratch arrays.
Solution: Expose read-only `LogisticsNetworkGraph` CSR offsets/destinations through `PowerGrid`, validate `PowerNode.GraphScratchVersion`/index against the current graph build, copy the read-only CSR into existing route scratch buffers under the single `LogisticsRouteScratchMemory` mutation guard, then run the unchanged fixed-queue BFS.
Rejected Alternatives: Returning mutable PowerGrid CSR arrays to construction was rejected because it would leak graph ownership. Keeping the managed topology fallback was rejected because it defeats the route locality proof. Rebuilding CSR from neighbors was rejected because the PowerGrid already owns the authoritative flat graph snapshot.
Scalability potential: Low devices avoid pointer-chasing `List<PowerNode>.Neighbors` route builds; middle/high/ultra keep the same BFS semantics and spend saved CPU on simulation/presentation cadence elsewhere.
Hardware Impact: Route BFS now copies contiguous `int` spans (`Offsets[0..N]`, `Destinations[0..E]`) instead of counting and resolving managed neighbor lists. No profiler measurement exists.

## Decision 037 - Compile Throttle And Roslyn Parse Check After Route CSR Patch
Problem: The route CSR reuse patch changes runtime C# and crosses a Construction-to-Power internal contract, so syntax verification was required without violating the build throttle.
Solution: Re-sampled CPU/compiler state: CPU was 100%, compiler processes were none, so `dotnet build` was not launched. Ran a lightweight in-memory Roslyn syntax parse through `csi.exe` over 11 touched C# files; result was `TOTAL_SYNTAX_ERRORS=0`.
Rejected Alternatives: Running `dotnet build` at 100% CPU was rejected by the explicit throttle. Claiming compile success from text scans alone was rejected because Roslyn parse was available without MSBuild.
Scalability potential: Process-level only.
Hardware Impact: Avoided shared-host build contention while still parsing edited C# syntax through Roslyn.
