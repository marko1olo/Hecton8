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
