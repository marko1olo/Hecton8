# Agent 1422 Rationale - POWER_GRID_JACOBI_SOLVER_HARDENER

Status: IMPLEMENTED / STATIC VERIFIED / BUILD BLOCKED BY CPU CONTENTION

## Non-Trivial Decisions

### D000 - Startup Discipline

Problem: Agent 1422 requires isolated prompt extraction, domain verification, and mandate selection before code changes.
Solution: Extracted only `<AGENT_PROMPT id="1422">` using PowerShell regex over `Docs/Tasks/CURRENT_BATCH.md`; selected power-grid, ARM64 layout, zero-GC, native memory/jobs, telemetry, registry, signal-lane, and math-gate mandates.
Rejected Alternatives: Reading neighboring batch prompts was rejected because the batch protocol forbids architectural bleed. Starting code edits before mandate scan was rejected because AGENTS.md requires mandate identification before coding.
Scalability potential: Low/Middle/High/Ultra all depend on bounded solver iteration and continuous quality damping; current phase is source archaeology only.
Hardware Impact: 0 microseconds runtime gain yet; setup prevents unauthorized churn and avoids build CPU contention on i3/MX350-class hosts.

### D001 - Runtime Solver Owner Identification

Problem: The prompt names `PowerGridManager`, but the active runtime voltage distribution is executed inside `LogisticsNetworkGraph.EvaluateGraphJob.ApplyTwoPassPowerDeltaPropagation`; `PowerGridManager` is a cold bootstrap/read facade.
Solution: Record both paths in a JSON ledger and harden the actual Burst relaxation job first.
Rejected Alternatives: Editing manager-level facades was rejected because it would not change voltage math. Building new code before locating the active solver was rejected because it would produce fake progress.
Scalability potential: Low uses fewer bounded relaxations; Middle adds residual stability; High/Ultra can spend more iterations on visible voltage fidelity without changing gameplay authority.
Hardware Impact: 0 microseconds measured at this phase; expected gain comes from replacing wasted branches and enforcing a quality-scaled cap.

### D002 - Sparse CSR Retention

Problem: Jacobi-style power solve needs matrix iteration, but a dense matrix would explode memory bandwidth and cache misses.
Solution: Keep the existing CSR-style sparse arrays (`NodeEdgeOffsets`, `EdgeDestinations`, `EdgeConductance`) as the matrix representation and adjust iteration semantics in place.
Rejected Alternatives: Dense adjacency matrices and managed dictionaries were rejected as too slow and allocation-prone for ARM64/mobile.
Scalability potential: Low runs the same compact graph with loose convergence; Middle/High/Ultra increase pass fidelity without changing storage.
Hardware Impact: Static architectural saving only; avoids O(N^2) bandwidth on i3/MX350-class hosts.

### D003 - Continuous Quality Damping Target

Problem: `GlobalQualityWeight` currently changes only propagation gain, not maximum iterations or residual epsilon.
Solution: Define the target as `activeMaxIterations = lerp(min,max,quality)` and `activeEpsilon = lerp(loose,strict,quality)` with a bounded loop.
Rejected Alternatives: Binary quality switches and hard-coded two passes were rejected because they break the scalability pillar.
Scalability potential: Low/Middle/High/Ultra all remain on one continuous scalar curve.
Hardware Impact: Expected low-end impact is fewer passes and earlier residual exit; exact microseconds deferred until final verification.

### D004 - DTO ABI Preservation

Problem: Task 13 requires explicit DTO refactoring, but the power DTOs already use explicit 32/32/64-byte layouts and validator offsets.
Solution: Preserve current ABI and only add aliases/counters if implementation requires it.
Rejected Alternatives: Repacking DTOs was rejected because it would raise serialization, Burst, and DataVault compatibility risk without measurable benefit.
Scalability potential: Stable DTOs support identical save/network identity across Low/Middle/High/Ultra.
Hardware Impact: 0 microseconds direct gain; avoids cache/ABI regression.

### D005 - Telemetry Ring Reuse

Problem: The solver has a 300-frame black-box ring, but the logistics dump path is agent 1319 and no convergence cap/residual state is recorded.
Solution: Reuse the fixed ring and route fault dumps to `Docs/AgentLogs/Dump_1422_PowerGrid.bin`; add convergence flags through native counters or fixed telemetry fields during implementation.
Rejected Alternatives: Managed per-frame diagnostics and string logs were rejected as hot-path GC risk.
Scalability potential: Low records cap hits when damping is aggressive; Middle/High/Ultra record stricter convergence behavior without extra allocations.
Hardware Impact: Hot-path target remains near zero; cold dump cost is accepted only on fault.

### D006 - Solver Telemetry Vault Counter

Problem: The existing black-box ring records frame-level state but not active Jacobi iteration cap, residual epsilon, or last residual.
Solution: Added one graph-local `PowerGridCounter64` DataVault buffer at offset 45 and passed it into the Burst solve job as `SolverTelemetry`.
Rejected Alternatives: Extending the 64-byte ring row was rejected because it would break existing readers and ABI expectations; managed side-channel logs were rejected for GC.
Scalability potential: Low/Middle/High/Ultra all write the same fixed counter with different encoded iteration and epsilon values.
Hardware Impact: Fixed 64-byte native allocation per graph; hot-path cost is one native struct write per solve.

### D007 - Data Gathering Kept Flat

Problem: The prompt asks to purge dynamic solver data structures; active voltage data already uses flat CSR/native arrays.
Solution: Kept existing `NodeEdgeOffsets`, `EdgeDestinations`, conductance, potential, and source arrays; added no temporary NativeList/NativeHashMap to the solve path.
Rejected Alternatives: Rebuilding topology into new containers per solve was rejected as allocation and cache churn.
Scalability potential: Low gets the same cache-friendly matrix as Ultra; fidelity changes through iteration count and epsilon only.
Hardware Impact: No measured microseconds yet; architecture avoids extra memory traffic on i3/MX350.

### D008 - Continuous Iteration Damping

Problem: Fixed two-pass relaxation cannot scale solver precision continuously with hardware budget.
Solution: Implemented `round(lerp(2,8,q))` iteration cap and `lerp(0.075,0.003,q)` residual epsilon in the runtime solver and the public two-pass compatibility job.
Rejected Alternatives: Integer quality buckets and unlimited convergence loops were rejected.
Scalability potential: Low uses two loose passes; Middle interpolates pass budget; High/Ultra spend up to eight passes with stricter residual stop.
Hardware Impact: Low-end worst-case pass count is bounded at 2; Ultra worst-case is bounded at 8. Measured microseconds still pending.

### D009 - Branchless Neighbor Row Accumulation

Problem: The neighbor row loops used `continue` branches for invalid edges, damaged nodes, and ruptures, creating branch prediction noise inside the matrix accumulation path.
Solution: Replaced those row-loop exits with clamped indices, boolean masks, `math.select`, and live-conductance multiplication.
Rejected Alternatives: Leaving branchy row accumulation was rejected; dense matrix SIMD was rejected because sparse CSR is the correct data shape.
Scalability potential: All quality levels use the same deterministic row math; only pass count/residual threshold changes.
Hardware Impact: Expected low-end benefit is fewer branch stalls; no numeric cycle claim until stress run.

### D010 - Lock Envelope Preservation

Problem: Adding solver telemetry creates another mutable native buffer that could be missed by the graph lock mask.
Solution: Inserted `_solverTelemetryHandle` into graph mutation lock bit 45 and reverse unlock order; lock failure writes a black-box reason flag through the existing cold telemetry path.
Rejected Alternatives: Unlocked telemetry writes and same-frame forced job completion were rejected.
Scalability potential: Lock semantics are invariant across Low/Middle/High/Ultra.
Hardware Impact: One extra DataVault lock check per scheduled graph evaluation; no managed allocation.

### D011 - Burst Alias Contract

Problem: The solver jobs passed many native views without explicit alias information, weakening Burst's ability to reason about vector lanes.
Solution: Added `[ReadOnly, NoAlias]` to immutable CSR/capacity arrays and `[NoAlias]` to mutable voltage, flag, summary, and telemetry vectors in the solver jobs.
Rejected Alternatives: Copying solver arrays to prove alias separation was rejected as memory bandwidth waste.
Scalability potential: Low/Middle/High/Ultra use identical alias-safe views.
Hardware Impact: Compiler optimization hint only; measured NEON impact requires Burst compile/profiler.

### D012 - Read Accessor Purity

Problem: Public power read methods resolved live native arrays through owner properties and `TryGetNodePotential` wrote telemetry on NaN.
Solution: Switched read accessors to `IDataVault.TryReadOnlyHandle` and removed the telemetry write from `TryGetNodePotential`; failures now close with default/false.
Rejected Alternatives: Read-time black-box publication rejected by global read accessor doctrine.
Scalability potential: UI/light consumers across all tiers read immutable snapshots or fail closed.
Hardware Impact: No measured runtime gain; removes hidden mutation route.

### D013 - ABI-Stable Telemetry Aliases

Problem: Task 13 asked for explicit telemetry DTO support, but changing `PowerTelemetryEntry` size would break existing readers.
Solution: Added `SolverIterationCount` and `SolverMaxIterations` field aliases at existing offsets 56 and 60 and extended the layout validator.
Rejected Alternatives: New row size or field reordering rejected as ABI breakage.
Scalability potential: Low/Ultra convergence data can be decoded without changing ring width.
Hardware Impact: 0 microseconds; metadata only.

### D014 - Fixed Telemetry Implementation

Problem: Cap hits and divergent solves needed forensic proof without heap activity.
Solution: Wrote convergence state into a fixed `PowerGridCounter64`; `WritePowerBlackBoxSample` encodes iteration count and max iteration count into `ReasonFlags` high bytes and dumps divergent/nonfinite states to `Dump_1422_PowerGrid.bin`.
Rejected Alternatives: Managed background dump setup in the solve path rejected. Existing cold binary writer remains only on fault.
Scalability potential: Low records aggressive cap hits; High/Ultra records stricter convergence behavior.
Hardware Impact: One native counter write per solver execution; cold dump cost only on catastrophic path.

### D015 - Build Gate Blocked

Problem: Build verification is required but host CPU load was sampled at 96 percent.
Solution: Did not run `dotnet build`; marked Task 15 `[BLOCKED_BY_CONTENTION]` and continued with static verification.
Rejected Alternatives: Running build under >50 percent CPU was rejected by explicit prompt constraint.
Scalability potential: Not runtime-relevant.
Hardware Impact: Avoided build contention on the host CPU; no compile resource consumed.

### D016 - Stress Harness Extension

Problem: Existing fuzzer coverage did not explicitly compare loose and strict `GlobalQualityWeight` runs in one proof test.
Solution: Added an editor test that runs the 5,000-node fuzzer at quality 0 and 1, checks zero managed allocation deltas, and asserts strict quality consumes a larger bounded iteration budget.
Rejected Alternatives: Writing an isolated toy graph was rejected because the existing fuzzer already owns the project-grade hostile CSR harness.
Scalability potential: Low/Ultra comparison is explicit; Middle/High remain covered by the same continuous interpolation formulas.
Hardware Impact: Test execution not measured due build contention.

### D017 - Black-Box Dump Route

Problem: Existing logistics dump path pointed to an older agent file name.
Solution: Changed dump target to `Docs/AgentLogs/Dump_1422_PowerGrid.bin` and wired divergent solver flags into dump eligibility.
Rejected Alternatives: New hot-path file writer rejected; existing cold binary dump path is retained.
Scalability potential: Same forensic route across all quality tiers.
Hardware Impact: 0 hot-path disk cost unless catastrophic flag is raised.

### D018 - Layout Validator Extension

Problem: New solver iteration aliases needed validator coverage without changing telemetry ABI.
Solution: Added offset checks for `SolverIterationCount` at 56 and `SolverMaxIterations` at 60.
Rejected Alternatives: Adding a larger telemetry struct rejected because 64-byte black-box rows are already consumed by editor tooling.
Scalability potential: All tiers share same row layout.
Hardware Impact: Editor/static validation only.

### D019 - Static Zero-GC Verification

Problem: Profiler execution is blocked, but hot-path allocation regressions still need a static screen.
Solution: Scanned modified C# files for dynamic collections, managed iteration patterns, string formatting, forced completes, and reference-type construction.
Rejected Alternatives: Declaring runtime 0 B GC without execution rejected.
Scalability potential: No quality tier introduces managed allocation in the modified solve path.
Hardware Impact: No runtime measurement yet.

### D020 - Proof Artifact

Problem: Final report requires measured timings, hashes, formulas, and verification state; timing is unavailable because the build gate is blocked.
Solution: Wrote `POWER_GRID_OPTIMIZATION_REPORT_1422.json` with exact hashes and null measured timing fields marked as blocked, not fabricated.
Rejected Alternatives: Inventing microseconds rejected.
Scalability potential: Report records Low/Middle/High/Ultra math behavior and blocked execution evidence.
Hardware Impact: No runtime impact; report is offline proof.

### D021 - APEX Offset Audit

Problem: The solver telemetry DataVault lane used repeated literal offset `45`, which is functionally correct but weak evidence for Data Sovereignty auditing.
Solution: Added `SolverTelemetryBufferOffset = 45` and routed allocation, lock, and reverse unlock through that symbol. BufferID route is `731300 + instanceId * 64 + SolverTelemetryBufferOffset`; first graph instance resolves to `731409`.
Rejected Alternatives: Leaving the magic number was rejected because the audit requires one owner and one route. Moving it into global `PowerGridBufferIds` was rejected because this is a per-graph local buffer lane, not the shared Jacobi contract ring.
Scalability potential: Low/Middle/High/Ultra all use the same fixed native counter route; fidelity only changes through continuous iteration and epsilon formulas.
Hardware Impact: 0 microseconds runtime change; auditability improvement only.

### D022 - Writer Fence API Correction

Problem: APEX audit found the proof text was too weak: graph mutation locking used helper names `TryLockGraphBuffer`/`UnlockGraphBuffer`, but the mandate requires explicit `TryAcquireWriteLock`/`ReleaseWriteLock` evidence.
Solution: Routed `TryLockGraphBuffer<T>` through `IDataVault.TryAcquireWriteLock` at `LogisticsNetworkGraph.cs:2331` and `UnlockGraphBuffer<T>` through `IDataVault.ReleaseWriteLock` at `LogisticsNetworkGraph.cs:2399`. Existing `try/finally` envelopes remain the release proof; `ScheduleEvaluationSlice` acquires at line 3024 and releases at line 3095.
Rejected Alternatives: Renaming every helper call was rejected as noisy churn. Reintroducing legacy `TryLockBuffer` was rejected because it weakens DataVault compaction safety language and proof.
Scalability potential: Low/Middle/High/Ultra share identical lock ownership; quality only changes solve work, not data authority.
Hardware Impact: 0 microseconds claimed. The patch changes writer-fence API precision, not algorithm cost.

### D023 - Residual Domain Debt Initially Deferred

Problem: Residual scan found `PowerGrid.cs` still owns persistent `NativeArray` fields for thermal and battery dispatch at lines 178-186 and allocates them at 1311-1342 / 1815-1828. It also uses managed `HashSet`, `List`, and `Dictionary` caches. `PowerVoltageSolverJob` at `PowerGridJacobiContracts.cs:531` is a public one-pass compatibility job with no rg-discovered schedule/use.
Solution: Initially recorded the debt instead of mutating broad ownership late. This decision was superseded by D026 after the user explicitly ordered continued domain cleanup; the direct thermal/battery arrays are now migrated to DataVault scratch lanes.
Rejected Alternatives: Broad final-hour migration rejected because it crosses thermal/battery ownership and could break unrelated gameplay without compile/profiler proof. Rewriting `PowerVoltageSolverJob` into an iterative `IJobParallelFor` was rejected because node-local internal iteration is not a valid full Jacobi solve without a dispatcher-level front/back loop.
Scalability potential: Current Jacobi runtime scales Low/Middle/High/Ultra continuously. Residual `PowerGrid.cs` debt still needs future Low/Middle/High/Ultra route design for thermal/battery dispatch.
Hardware Impact: No microseconds claimed for unpatched debt. Risk is memory sovereignty/compliance, not a measured current-frame regression in the modified Jacobi kernel.

### D024 - Branchless Quality Sanitization Correction

Problem: The core row accumulation was branchless, but the quality scalar sanitization still used C# ternary fallback in modified Burst jobs/helpers. This is not an allocation bug, but it weakens the branchless proof and leaves fallback behavior inconsistent with the compatibility voltage job.
Solution: Replaced quality fallback ternaries with `math.select(1f, value, math.isfinite(value))` in `TwoPassPowerGridSolverJob`, runtime Jacobi helpers, adaptive solve budget, and `PowerVoltageSolverJob`. Nonfinite quality now consistently falls back to authoritative quality `1f`, not survival quality `0f`.
Rejected Alternatives: Leaving ternaries was rejected because the user explicitly asked for vector-select discipline. Falling back to 0 was rejected because a corrupt quality scalar should fail toward stable authoritative solve, not silently reduce solver effort.
Scalability potential: Low/Middle/High/Ultra still follow the same continuous curve when quality is finite; nonfinite quality becomes deterministic high-fidelity fallback instead of an accidental binary downgrade.
Hardware Impact: No measured microseconds. Static benefit is branchless quality sanitization and stronger Burst/NEON proof.

### D025 - Branchless Finite Sanitization Correction

Problem: A deeper APEX scan found remaining `math.isfinite(...) ? ... : ...` ternaries in modified Jacobi hot paths and solver telemetry writes. They were not allocation bugs, but they left scalar branch syntax inside the Burst-facing voltage path.
Solution: Replaced targeted finite fallbacks with `math.select` for potential/capacity/gain/injection/telemetry sanitization in `LogisticsNetworkGraph.cs`. Unsafe bounds-protection ternaries that prevent out-of-range reads were not rewritten blindly.
Rejected Alternatives: Rewriting every ternary in the full file was rejected because many are cold managed scheduling, bounds guards, or unrelated logistics summaries. Claiming full branchless file-wide proof was rejected; the proof is scoped to targeted finite/quality sanitization and row accumulation.
Scalability potential: Low/Middle/High/Ultra still use the same continuous iteration/epsilon curve; finite corruption now sanitizes through deterministic vector-select style without changing gameplay authority.
Hardware Impact: No measured microseconds. Static benefit is cleaner Burst-lane expression in the Jacobi hot sections; runtime NEON proof still requires Burst Inspector/profiler after build gate clears.

### D026 - PowerGrid Scratch DataVault Migration

Problem: Follow-up domain audit found `PowerGrid.cs` still owned direct persistent `NativeArray` scratch storage for battery dispatch and cable thermal diffusion. This was outside the main Jacobi kernel, but still inside the Power domain and violated the Data Sovereignty direction more than the report should tolerate.
Solution: Replaced the nine direct native fields with `VaultGenerationHandle<T>` lanes using `PowerGridScratchBufferBase=731700`, `PowerGridScratchBufferStride=16`, and offsets `0..8`. `PowerGridManager` now injects the active `IDataVault` when grids are created/split and rebinds existing grids on DataVault service replacement. Thermal snapshot, thermal schedule, and battery dispatch acquire writer locks through `IDataVault.TryAcquireWriteLock` and release through `ReleaseWriteLock` in `finally`. The thermal Burst job also received `[NoAlias]` and masked invalid neighbors/hull sink with `math.select`/conductance masks.
Rejected Alternatives: Keeping the old `Allocator.Persistent` arrays was rejected after the residual audit. Using `GlobalDataVault.TryGetLatestCreated()` inside `PowerGrid` was rejected because that route is bootstrap/editor/diagnostic-only. Disabling battery dispatch to avoid migration was rejected because it changes gameplay truth instead of fixing ownership. A separate physical thermal simulation was rejected; the existing cheap CSR diffusion remains the cinematic approximation and scales by cadence/iteration budget.
Scalability potential: Low keeps the same cheap visual thermal approximation with bounded iterations and fail-closed locks; Middle/High/Ultra can spend more cadence/iteration budget through the existing continuous quality path without changing DTO/save authority. The battery path remains deterministic scratch math, not a new simulation.
Hardware Impact: No measured microseconds. Static gain: removed nine direct persistent native owner fields and two `new NativeArray` allocation regions from `PowerGrid.cs`. Expected low-end benefit is lower ownership/fragmentation risk and fewer branch stalls in the thermal pass; profiler proof remains blocked by CPU contention.

### D027 - Single-Lane Scratch Lock Flattening

Problem: APEX integrator audit found that `LogisticsNetworkGraph` graph-local buffers were already moved to a one-active-bit mutation guard, but `PowerGrid` thermal scratch BufferIDs still used contiguous local offsets. That made the thermal job mutation guard cover several active lock bits at once. A collision audit also proved that the first attempted `731700` one-lane stride collided with graph-local BufferIDs at higher runtime IDs.
Solution: Moved `PowerGrid` scratch BufferIDs to `PowerGridScratchBufferBase=2731700`, spaced local lanes by `PowerGridScratchBufferLockLaneStride=32`, and kept each grid instance salted by `(Id & 31)`. `ResolveScratchMutationGuardBit` now derives the bit from the resolved BufferID, so all thermal scratch buffers for one grid collapse to one active lock lane. `LogisticsNetworkGraph.AddNode/AddEdge/AddProducer/AddConsumer` now ensure capacity by required count before the mutation guard, without reading mutable native-view `.Length` properties before locking.
Rejected Alternatives: Keeping a multi-bit thermal mutation guard was rejected because it leaves a deadlock-contention proof gap. Reusing `731700` with one-lane stride was rejected after exact collision evidence. Per-buffer write locks around the scheduled thermal job were rejected because they would hold multiple write locks simultaneously.
Scalability potential: Low/Middle/High/Ultra keep identical data authority. Weak devices benefit from fewer unrelated DataVault stalls; high-end devices keep thermal visual overkill cadence without widening lock ownership.
Hardware Impact: No measured microseconds. Static proof only: no collisions across 1024 logistics graph instances and 1025 power grid instances in the audited formulas; CPU stayed at 100 percent, so build/profiler execution was not legal.

### D028 - Cold Component Lookup Cache

Problem: APEX integrator audit found one remaining `GetComponent` path in `PowerGrid.BuildGraphSnapshot`. It was not inside `Tick`, `LateFrameTick`, `FixedUpdate`, or a Burst `Execute`, but it still ran during node traversal when rebuilding the power topology and weakened the cold-cache proof.
Solution: Added `_overloadServiceCache` keyed by `BaseModule` and routed overload thermal `FluidDynamics`/`DamageReceiver` resolution through `ResolveCachedOverloadServices`. The node traversal now reads cached service references; cache misses are isolated to the cold resolver and purged on `RemoveNode`.
Rejected Alternatives: Leaving the lookup in the traversal was rejected because it keeps scene-search language near solver data gathering. Caching atmosphere room index was rejected because room/runtime activity can change and must stay refreshed per topology rebuild.
Scalability potential: Low/Middle/High/Ultra keep identical electrical truth. Weak devices avoid repeated component lookup during dirty graph rebuilds; high tiers keep thermal/overload presentation without adding physics.
Hardware Impact: No measured microseconds. Static proof: scoped hot-method lookup/GC scan remains zero; broad scan now shows `GetComponent` only inside the cold cache-miss resolver.

### D029 - Topology Lookup Cold-Warm Enforcement

Problem: D028 still left a possible first-read cache miss in `BuildGraphSnapshot` if a `BaseModule` entered the grid without prior overload service resolution. That path is slow-tick, not Burst, but it weakens the claim that component lookup is cold-cached before topology rebuild.
Solution: Added cold warming in `PowerGrid.AddNode`, cache transfer/warming in `AbsorbAll`, cached `ISubmarineAtmosphereRoomMutationSink` with the existing `SubmarineFluidDynamics` and `IDamageReceiver`, and changed `BuildGraphSnapshot` to read cached payload only. `ReadCachedOverloadServices` never searches the hierarchy.
Rejected Alternatives: Editing `BaseModule` to expose new public service accessors was rejected as cross-domain public API churn. Performing fallback `GetComponent` inside `BuildGraphSnapshot` was rejected because it keeps scene search in the simulation rebuild path. Caching room index was rejected because room membership can change and must stay resolved against current transform.
Scalability potential: Low/Middle/High/Ultra keep one electrical truth route. Low-end devices avoid first-miss lookup during dirty graph rebuild; high-end tiers keep overload/flood heat presentation without adding physical simulation.
Hardware Impact: No measured microseconds. Static proof: `BUILD_GRAPH_SNAPSHOT_LOOKUP_HITS=0`, scoped hot-method banned scan total `0`, and current `PowerGrid.cs` SHA-256 is `AC1B34374ED04DFF3275D103AB347F2DCFD63B92958AAC6E4F34791A805822C5`.
