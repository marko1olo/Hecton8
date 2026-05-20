# Rationale_SHINOBU_222

Status: POLISH_POWER_AUTHORITY_STATIC_PASS_COMPILE_BLOCKED_BY_CPU_GATE
Agent: SHINOBU_222

## Decision 000 - Domain Boundary

Problem: Pump/pipe evacuation belongs to Habitat & Vehicles logistics but depends on Fluid Incursion, Power Grid, Visual Sync, and telemetry.
Solution: Treat drainage as a stateless math kernel over Vault-style buffers, with cold adapters only for editor/debug surfaces.
Rejected Alternatives: Per-pipe MonoBehaviour loops, water particle actors, PhysX colliders, recursive container balancing; all violate logistics mandate and generate frame risk.
Scalability potential: Low uses minimal Jacobi iterations and shader-only visual flow; Middle/High/Ultra spend saved CPU on denser visual proxies and telemetry density, not per-droplet truth.
Hardware Impact: Expected low-end i3/MX350 gain is removal of managed traversal and particle churn; exact microseconds remain PENDING VERIFICATION until compile/profiler evidence.

## Decision 001 - Mandate Set

Problem: Task spans zero-GC hot paths, Native containers, ARM64 DTO layout, logistics graph math, water volume integration, AUP precision, dispatcher phase ownership, and Black Box telemetry.
Solution: Apply mandates OPT_Zero_GC, OPT_Native_Memory, DATA_Runtime_Struct_Layout_ARM64, LOGI_Energy_Networks, PHYS_Fluid_Incursion, MATH_AUP_Determinism_Sync, ARCH_Execution_Phases, and DBG_Telemetry.
Rejected Alternatives: Reading unrelated rendering/AI mandates would inflate context without improving the solver.
Scalability potential: Mandates enforce Low/Middle/High/Ultra continuous quality rather than binary platform branches.
Hardware Impact: Better cache-local math and no managed churn are aimed at sub-0.1ms logistics evaluation on low-end silicon; measured proof absent.

## Decision 002 - Legacy Pump Drain Authority Removed

Problem: `FluidPipeGraphRuntime.ApplyPumpInputs` and `HabitatGraphManager.ApplyWaterPumpDrainage` drained `BaseModule` objects through active `WaterPumpModule` registries and graph traversal, making pump evacuation object-authoritative.
Solution: Retired both call bodies and deleted the private traversal helper used only by old pump drainage. Drain authority now sits in `SumpPumpPipeGridRuntime`, which reads and writes Vault buffers.
Rejected Alternatives: Keeping the old traversal as fallback would create two water authorities and nondeterministic mass deltas under rollback.
Scalability potential: Low uses zero scene traversal for pump drain; Middle/High/Ultra can spend saved CPU on denser shader flow diagnostics.
Hardware Impact: Expected i3/MX350 gain is removal of per-pump managed graph walking and recursive/BFS room drain checks; exact microseconds remain PENDING VERIFICATION because build/profiler is CPU-gated.

## Decision 003 - Vault DTO Layout And Buffer IDs

Problem: Pump and pipe state needed raw snapshotable memory with no CS1612 property copies.
Solution: Added explicit DTOs: `PumpNodeDTO` 32 bytes with required offsets, `PipeEdgeDTO` 64 bytes, `DrainageTuningDTO` 64 bytes, `DrainageTelemetryEntry` 64 bytes, and owner-local `SumpPumpDrainageBufferIds` Vault lanes assigned to `95820..95842`.
Rejected Alternatives: Reusing managed `WaterPumpModule` fields or local runtime `NativeArray` ownership would break Vault sovereignty and rollback memcpy.
Scalability potential: Low/Middle/High/Ultra all share the same flat buffers; quality changes alter solver cadence and visual payload, not data authority.
Hardware Impact: 32/64-byte layouts keep sequential reads predictable on ARM64 and low-end x86; measured cache savings are PENDING VERIFICATION.

## Decision 004 - CSR Builder And Jacobi Solver

Problem: Pipe pressure must solve on a connected network without lock contention or per-edge object calls.
Solution: Build a CSR matrix from flat `PipeEdgeDTO` data, then run Jacobi relaxation with double-buffered pressure arrays and deterministic Burst float mode.
Rejected Alternatives: NativeParallelMultiHashMap neighbor traversal and per-node managed collections were rejected due non-contiguous access and poorer cache predictability.
Scalability potential: Low uses one iteration; Middle increases pressure settling; High/Ultra reach eight iterations through continuous `GlobalQualityWeight`.
Hardware Impact: CSR turns pipe iteration into contiguous linear reads; expected low-end gain is lower L1 miss rate versus hash lookup, exact microseconds PENDING VERIFICATION.

## Decision 005 - Atomic Quantized Evacuation

Problem: Multiple pumps can drain the same Fluid Incursion room, and floating deltas can create or destroy water.
Solution: Quantize evacuation into `MassQuantumM3` units, store per-pump remainder, and apply water deltas with float-bit CAS using `Interlocked.CompareExchange`.
Rejected Alternatives: Plain float writes would race; raw `Exchange` would lose concurrent drains. CAS preserves compare-before-write semantics.
Scalability potential: Low can use coarse quantum and fewer solver steps; Middle/High/Ultra can tighten quantum and spend cycles on richer pressure resolution without changing authority.
Hardware Impact: CAS appears only on active pump drains, not every pipe edge; i3/MX350 cost should stay bounded by pump count, exact microseconds PENDING VERIFICATION.

## Decision 006 - Dear Lie Visual Flow

Problem: Moving water particles or meshes through pipes burns CPU on presentation, not gameplay authority.
Solution: Write edge flow scalars to `DrainagePipeFlowGpuDTO` and upload them through a double-buffered `GraphicsBuffer`; also publish scalar flow to the existing connection spline renderer.
Rejected Alternatives: CPU mesh animation, physical liquid actors, and particle simulation were rejected as non-authoritative visual waste.
Scalability potential: Low uses sparse shader panning; Middle/High/Ultra can increase shader/normal-map intensity from the same scalar payload.
Hardware Impact: CPU side is a linear memcpy into a locked buffer after the job fence; exact visual-upload microseconds PENDING VERIFICATION.

## Decision 007 - AUP Downhill Conductance

Problem: Gravity-assisted pipe flow must work at 100km scale without collider checks or float absolute coordinate drift.
Solution: Store node locations as `double3` AUP, subtract source from destination in double precision, cast only the local delta to `float3`, then dot against gravity for conductance boost.
Rejected Alternatives: Rigidbody/collider pipe checks and absolute `float3` world positions were rejected for broadphase cost and precision failure.
Scalability potential: Low/Middle/High/Ultra all use the same cheap scalar; high tiers spend the saved cost on visuals, not physics.
Hardware Impact: One dot product during rare CSR rebuild, not every frame; low-end cost is negligible compared with physics broadphase.

## Decision 008 - Compile Gate

Problem: Project rule forbids `dotnet build` when total CPU is above 50% or any `csc.exe`/`dotnet` compiler is active.
Solution: Checked CPU repeatedly; samples stayed above the gate with no compiler processes. Latest `Win32_Processor.LoadPercentage` reported 100%. Build was not launched.
Rejected Alternatives: Forcing a build would violate batch protocol and steal CPU from other active agents.
Scalability potential: Verification waits for a clean machine window; implementation remains static-pass only until then.
Hardware Impact: No extra compiler load was added to the shared workstation.

## Decision 009 - BufferID Collision Repair

Problem: Static source audit showed drainage candidate IDs `70820..70841` collide with graphics culling, toxic atmosphere, sonar, and procedural wreckage local `BufferID` casts.
Solution: Move drainage to owner-local numeric lanes `95820..95842` in `SumpPumpDrainageBufferIds`, including a separate per-pump mass-error lane for deterministic telemetry.
Rejected Alternatives: Keeping `70820..70841` would allow unrelated systems to alias the same Vault rows; central enum additions would widen the compile wall for a drainage-owned contract.
Scalability potential: Low/Middle/High/Ultra all resolve the same owner-local drainage lanes; quality changes alter work, not ownership.
Hardware Impact: Prevents catastrophic cache/data corruption from buffer aliasing; exact microseconds are not meaningful, but correctness risk is removed before runtime.

## Decision 010 - Pointer-Free Vault Descriptors

Problem: `SumpPumpPipeGridRuntime` persisted legacy pointer-bearing `VaultBufferHandle<T>` fields, violating the current Vault descriptor ledger.
Solution: Replace all persistent handles with `VaultGenerationHandle<T>` and resolve method-local `NativeArray<T>` views through `IDataVault.TryResolveHandle`.
Rejected Alternatives: Leaving the obsolete bridge would still work mechanically but preserves stale-pointer debt and defrag fragility.
Scalability potential: Descriptor-only storage survives Vault generation churn across low-memory devices and high-end visual overkill sessions.
Hardware Impact: Removes persistent raw pointer alias risk during relocation/compaction; low-end silicon avoids undefined stale pointer reads under memory pressure.

## Decision 011 - Parallel Counter False-Sharing Removal

Problem: Parallel pump drains aggregated frame volume, active pump count, power draw, and mass error into adjacent `int` counters with `Interlocked.Add`, creating cache-line contention.
Solution: Each pump writes only its own DTO rate and per-pump mass-error row; `DrainageTelemetryRecorderJob` performs one linear reduction after the parallel drain.
Rejected Alternatives: Padding each counter to 64 bytes would fix false sharing but keep unnecessary atomic contention. A single reducer is cheaper and deterministic.
Scalability potential: Low uses the same linear telemetry pass with fewer solver iterations; High/Ultra can spend saved contention budget on richer GPU pipe visuals.
Hardware Impact: Expected gain on i3/MX350 is lower MESI traffic during multi-pump drains; exact microseconds remain pending profiler proof.

## Decision 012 - CSR Capacity And Cold Write Fences

Problem: CSR prefix offsets could exceed real destination/flow buffer capacity under malformed edge counts, and cold CSV/tuning/mock writes were not explicitly fenced.
Solution: Cap per-node CSR counts during prefix construction to the minimum destination/conductance/flow/flat-index capacity; acquire Vault locks for CSV profile writes, tuning edits, and mock topology generation.
Rejected Alternatives: Trusting designer edge counts or editor-only writes would make overflow bugs intermittent and hard to reproduce.
Scalability potential: The same bounded CSR matrix feeds all quality tiers; High/Ultra iterations cannot read beyond valid contiguous rows.
Hardware Impact: Prevents out-of-bounds cache pollution and protects low-end devices from invalid memory churn; exact runtime delta pending compile/profiler gate.

## Decision 013 - Compile-Wall Local Buffer IDs And Job Route

Problem: The collision repair initially placed SHINOBU_222 drainage IDs in the central `H8Memory.BufferID` enum, widening a shared compile surface for a domain-owned lane. The cold mock path also called `DrainageMockNetworkJob.Execute()` directly, bypassing the normal job extension route.
Solution: Remove drainage IDs from the central enum and declare `SumpPumpDrainageBufferIds` as owner-local numeric `BufferID` casts `95820..95842` in the drainage contract. Replace direct `job.Execute()` with `job.Run()` for deterministic cold mock generation through Unity job extensions.
Rejected Alternatives: Keeping central enum entries would make a drainage-lane edit touch a high-conflict core file. Scheduling the mock job and immediately completing it would add an unnecessary fence in a cold utility path; direct `Execute()` was too easy to confuse with a hot-path bypass.
Scalability potential: Low/Middle/High/Ultra all resolve the same Vault lanes; quality still changes solver iterations and shader scalar richness, not ownership or compile routing.
Hardware Impact: Compile-wall risk is reduced for shared workstation iteration. Runtime microsecond gain is not claimed; the meaningful correction is architectural isolation and Burst-compatible job invocation.

## Decision 014 - Lock-Before-Resolve And Bounded Mutation

Problem: Solver scheduling resolved Vault `NativeArray<T>` views before acquiring buffer locks, and shared Fluid/Power reads used direct `TryGetBuffer`, which creates external views without explicit generation-handle proof. CSR capacity trimming also needed a per-source write bound, not only a global valid-edge bound. Float-bit CAS in the evacuation path was unbounded.
Solution: Acquire SHINOBU_222 owner-local locks before resolving generation handles. Resolve shared Fluid Incursion and Power potential through method-local `VaultGenerationHandle<T>` descriptors, locking Fluid for the scheduled job lifetime and Power only for the copy. Add `slot < NodeEdgeOffsets[source + 1]` before each CSR write after prefix cap. Bound `AtomicDrainVolume` to 64 CAS attempts and sanitize delta time/current rate before quantization. Release all owner-local generation handles on teardown.
Rejected Alternatives: Continuing to rely on `TryGetBuffer` would preserve stale external-view ambiguity. Padding counters would not fix CSR overwrite or CAS nontermination. Releasing only graphics buffers would leak drainage Vault references across enable/disable cycles.
Scalability potential: Low quality still runs the shortest solver chain but now uses the same lock/order proof as High/Ultra; high tiers can spend extra iterations without risking CSR row corruption under malformed graphs.
Hardware Impact: Prevents cache pollution from CSR overwrite, reduces live-relocation/stale-view risk on low-memory devices, and caps worst-case CAS contention instead of allowing pathological spin.

## Decision 015 - Owner Job Fence Registration

Problem: The final scheduled drainage chain was stored in `_solverHandle` and finalized through `DispatcherJobFence`, but it was not registered with `H8Memory` as an active owner job. Vault teardown/defrag diagnostics would not have a central owner fence for SHINOBU_222 memory.
Solution: Register `_solverHandle` with `H8Memory.RegisterActiveJob(OwnerSystem, _solverHandle)` immediately after scheduling `DrainageTelemetryRecorderJob`, which is the final dependency in the chain.
Rejected Alternatives: Relying only on the local `_solverScheduled` flag protects this MonoBehaviour path but does not publish an owner fence to shared memory infrastructure.
Scalability potential: No quality-tier divergence; every tier reports the same final chain handle while low/high quality only changes iteration count.
Hardware Impact: Runtime microsecond gain is not claimed. The impact is safer teardown/defrag coordination under memory pressure on low-end devices.

## Decision 016 - Boot Fail-Close Vault Handle Validation

Problem: `TryResolveAndInitializeBuffers()` acquired 23 owner-local `VaultGenerationHandle<T>` descriptors and then initialized tuning without proving every descriptor resolved to the required row count. A partial Vault acquisition under compaction, starvation, or type drift could leave `_buffersReady` true enough to retry later hot work through default handles.
Solution: Add `ValidateOwnedBuffers()` and `HasResolvedBuffer<T>()` immediately after handle acquisition. Every SHINOBU_222 owner-local lane must resolve through `IDataVault.TryResolveHandle` with its minimum length before tuning initialization. Any failure calls `ReleaseOwnedBuffers()`, resets all descriptors, and returns false.
Rejected Alternatives: Letting the solver schedule path discover missing buffers is later and noisier. Adding a managed list of descriptors would be simpler but unnecessary and violates the zero-GC discipline even on a cold bootstrap path.
Scalability potential: Low/Middle/High/Ultra all share the same boot proof; quality only changes solver iterations and shader richness after the buffer set is complete.
Hardware Impact: Runtime microsecond gain is not claimed. The gain is fail-close memory safety under low-memory/compaction pressure before any scheduled Burst chain can touch invalid Vault rows.

## Decision 017 - Front Back Conservation Drain Lock

Problem: `EvacuateWaterVolumeJob` applied two independent float-bit CAS drains to the Fluid Incursion front/back compartment rows, then used `min(drainedFront, drainedBack)` for pump rate and reported the delta as mass error. If one CAS drained more than the other under contention or pre-existing front/back drift, SHINOBU_222 could create a new authoritative mismatch while only recording it after the fact.
Solution: Add `DrainageRoomDrainLock64`, a 64-byte padded per-room lock row on owner-local Vault lane `95843`. The solver clears locks in a Burst job, then `EvacuateWaterVolumeJob` acquires the target room lock with bounded 64-attempt `Interlocked.CompareExchange`, sanitizes front/back water rows, computes one `actualDrained = min(front, back, quantizedRequest)`, and writes the identical delta to both buffers before releasing the lock.
Rejected Alternatives: Keeping independent CAS and treating the mismatch as telemetry would preserve a known conservation fault. Mirroring the Fluid DTO locally was rejected because `GlobalDataVault.ComputeTypeHash<T>()` includes `typeof(T).TypeHandle`, so a local mirror would fail Vault type validation against the Fluid-owned buffer.
Scalability potential: Low quality still runs fewer Jacobi iterations, but same-room pump contention is deterministic and front/back conservation-safe across all tiers. High/Ultra can add visual flow richness from the same scalar output without increasing Fluid authority complexity.
Hardware Impact: Each contested room lock is isolated to a 64-byte row to avoid false sharing. Expected low-end cost is one bounded lock only for active pump-room drains, replacing two independent CAS loops that could leave duplicated-buffer drift.

## Decision 018 - Mock Job Route Regression Correction

Problem: Post-conservation forbidden scan found `DrainageMockNetworkJob` still called through direct `job.Execute()` at `SumpPumpPipeGridRuntime.cs:283`, contradicting the earlier job-route correction and bypassing Unity's job extension path.
Solution: Replace the direct call with `job.Run()` and rerun the forbidden-pattern scan across SHINOBU_222 runtime/jobs/contracts/editor files.
Rejected Alternatives: Leaving `Execute()` because the mock generator is cold would preserve a known protocol breach and make future hot-path exceptions easier to miss.
Scalability potential: No quality-tier behavioral change. Low/Middle/High/Ultra all use the same deterministic mock data route for profiling and debugging.
Hardware Impact: Runtime microsecond gain is not claimed. The impact is route correctness and static proof that cold mock generation does not normalize direct job entry.

## Decision 019 - Latest Compile Gate Sample

Problem: C# changes need a compile check, but project protocol forbids launching a build under high CPU load or while `dotnet`/`csc` is already active.
Solution: Sampled CPU and compiler processes after the static regression correction. One sample reported active `dotnet`/`csc`; the final sample reported no active compiler processes but still 100% CPU, so no build was launched.
Rejected Alternatives: Starting another `dotnet build` would violate the explicit gate and contend with another agent/build process.
Scalability potential: No runtime scalability effect. Verification remains static-only until the workstation is below the gate.
Hardware Impact: Avoided adding compiler load to a saturated shared workstation.

## Decision 020 - Power Fail-Closed And Quantized Drain Clamp

Problem: `HydratePowerPotentialFromVault` used `1.0` fallback power when the Logistics Power Vault row was missing, locked, empty, or shorter than the drainage node count. That made pump speed depend on a synthetic default instead of Vault data. `EvacuateWaterVolumeJob` also cast quantized drain units directly to `int`, so corrupted pump rate/remainder input could overflow the cast before the room lock path.
Solution: Missing or undersized Logistics pressure now writes `0.0` power potential and marks `MissingPowerVault`; pumps halt mathematically until the Vault owner publishes power rows. Evacuation now checks request finiteness, clamps quantized units to `MaxQuantizedDrainUnitsPerPump`, and drops absurd clipped remainder instead of preserving poisoned magnitude.
Rejected Alternatives: Keeping full-power fallback would violate the user's data-authority requirement. Letting the int cast overflow and relying on later mass-error telemetry would be too late; the poison has to be stopped before mutation.
Scalability potential: Low/Middle/High/Ultra all consume the same Vault-owned power scalar; quality changes solver cadence and visuals, not energy truth. High tiers can still amplify shader flow from legitimate Vault power.
Hardware Impact: Negligible ALU cost: one finite check and clamp per active pump. Low-end benefit is avoiding NaN/overflow propagation into Fluid and telemetry rows.

## Decision 021 - Jacobi Power Fallback And Signed Quantization Fence

Problem: Static readback showed the Jacobi pressure job still read `PowerPotential` with fallback `1.0`, and the quantized drain unit value was only upper-bounded before the `int` cast. A missing/out-of-range power row could still run a pump at full pressure inside the solver, and a negative corrupted remainder could enter an implementation-defined float-to-int conversion path.
Solution: Change `PipePressureSolverJob` to sanitize missing/non-finite power as `0.0`, return `MissingPowerVault` when Logistics pressure is shorter than the copied node range, and clamp quantized units into `[0, MaxQuantizedDrainUnitsPerPump]` before integer conversion.
Rejected Alternatives: Relying on boot-time buffer validation would not protect against later short shared power rows. Letting the negative path fall through to `quantizedUnits <= 0` after conversion is too late because the conversion itself is the unstable point.
Scalability potential: Low/Middle/High/Ultra now share the same power truth: no Vault row means no pump energy. Quality can still scale Jacobi iterations and shader flow intensity, but not invent pump throughput.
Hardware Impact: One saturating fallback and one `math.clamp` per active pump/solver node. Low-end benefit is deterministic fail-closed behavior with no wasted drain work under missing power data.
