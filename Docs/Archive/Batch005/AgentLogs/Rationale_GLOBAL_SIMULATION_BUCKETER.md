# Rationale_GLOBAL_SIMULATION_BUCKETER

Status: PENDING VERIFICATION

## Decision 0: Establish Source Of Truth
Problem: SlowTick clumping must be fixed without inventing direct dependencies across parallel agent work.
Solution: Use the extracted XML prompt, AGENTS.md, domain map, and task-relevant mandates as the only authority before code changes.
Rejected Alternatives: Directly editing suspected SlowTick code without registry/dispatcher audit was rejected because it would risk API invention and hidden dependencies.
Scalability potential: Low uses wider buckets and flat CPU load; Middle keeps stable cadence; High/Ultra can spend saved CPU on denser visual systems while preserving deterministic authority.
Hardware Impact: Expected gain on i3/MX350 is spike flattening, not total work elimination. Exact microseconds saved are PENDING PROFILER.

## Decision 1: Use Core Interface Boundary
Problem: A bucketer touches AI, voxel, thermodynamics, and dispatcher code. Direct concrete references would couple parallel agents.
Solution: Prefer an `ISimulationBucketer` service registered through `GlobalRegistry` and consumed through stable contract calls outside hot dependency lookup paths.
Rejected Alternatives: `BucketManager.Instance` and direct class references were rejected because AGENTS.md forbids singleton access and cross-domain concrete coupling.
Scalability potential: Low/MX350 can stretch cadence to reduce spikes; Ultra can process richer non-authoritative visual workload after authority buckets stay flat.
Hardware Impact: Avoids per-frame global searches and removes synchronous clump spikes. Exact microseconds saved are PENDING PROFILER.

## Decision 2: Round Cadence Domains To Power-Of-Two Masks
Problem: The assignment requested slow=60/cold=600, but the recursive verification protocol explicitly rejects hot modulo/division in Burst-visible bucket math.
Solution: Use fast=4, standard slow=64, low slow=128, cold=512. All active bucket checks resolve through `& (PowerOfTwo - 1)`.
Rejected Alternatives: `% 60`, `% 120`, `% 600`, and a mixed exact-count + division path were rejected because they create slower hot math and more branch cases.
Scalability potential: Low uses 128 buckets for toaster-grade flattening; Middle/High use 64 with optional two active buckets; Ultra spends the stable budget on visual overkill instead of authority spikes.
Hardware Impact: Estimated 5-25 us saved per broad bucket evaluation pass and larger indirect savings from flattened slow-tick bursts. Exact profiler capture is blocked by external compile errors.

## Decision 3: Bootstrap-Owned Persistent NativeArray Registry
Problem: Entity-to-bucket assignment needs stable storage without managed allocation churn and without inventing a singleton.
Solution: `GameBootstrapper` creates `ModuloSimulationBucketer` once, the service allocates persistent `NativeArray<int>` through `H8Memory`, and shutdown unregisters/disposes it.
Rejected Alternatives: `Dictionary<uint,int>`, per-system arrays, or transient frame allocations were rejected because they violate Zero-GC and cross-domain ownership rules.
Scalability potential: Low can keep a smaller active slice while preserving the same entity table; High/Ultra can raise visual workload without changing the authority contract.
Hardware Impact: Estimated 60-250 us saved under fauna-scale lookup pressure versus managed table churn; no per-frame GC allocation introduced.

## Decision 4: Dispatcher Is The Only Frame Authority
Problem: Local per-system timers caused all slow systems to wake on the same cadence, creating a spike every sixth frame on 60fps targets.
Solution: Advance the bucketer in `SystemDispatcher.Update()` after time state refresh and before simulation lanes. Dispatcher gates `IBucketedSlowTickable` objects and reports bucket load telemetry.
Rejected Alternatives: Local counters inside fauna, voxel, and thermal systems were rejected because independent timers drift and still clump after scene transitions.
Scalability potential: Low reduces active slow buckets; High can process two slow buckets per frame when debt is absent; Ultra keeps authority flat and buys stronger presentation systems.
Hardware Impact: Estimated 300-1500 us spike flattening at 5000 fauna, plus 200-900 us deferred under job-admission debt. Exact numbers require profiler after the compile wall clears.

## Decision 5: AUP Barrier Uses Cadence Throttle, Not Special Physics
Problem: Bucketing is frame-based while AUP/floating-origin shifts are spatial; staggered interpolation can tear during origin-shift locks.
Solution: Treat active AUP locks as a bucketer barrier, force one active slow bucket, and replace frame-count modulo watchdogs with countdown state.
Rejected Alternatives: Recomputing all staggered entities on an AUP frame was rejected because it recreates the exact spike the bucketer removes.
Scalability potential: Low devices get minimal authority work during shifts; Ultra can layer visual smoothing while authority remains single-bucket deterministic.
Hardware Impact: Estimated 50-200 us spike flattening during shift/watchdog frames and lower visible tear risk.

## Decision 6: Voxel And Biolum Use Fast Bucket Fakes
Problem: Queue drains and distant biolum LOD checks can clump when driven from raw frame modulo checks.
Solution: Hash carve events/zones into the 4-bucket fast domain and only process matching active fast buckets. This is a scheduling fake, not a physics simulation.
Rejected Alternatives: Full queue drain and `Time.frameCount % 3` LOD gating were rejected because they synchronize unrelated work on the same frame.
Scalability potential: Low spreads expensive world edits across frames; High/Ultra can spend the saved frame budget on richer particle/light response.
Hardware Impact: Estimated 100-600 us spike flattening during voxel carve bursts and 25-90 us around distant biolum update bursts.

## Decision 7: Thermodynamics Uses 1/8 Slice Instead Of Exact 1/6
Problem: The prompt asked for 1/6 Jacobi diffusion, but the same prompt requires replacing modulo with power-of-two masks after core completion.
Solution: Slice the 32^3 thermal grid into 8 equal chunks, schedule one slice per job, and use shift/mask coordinate decode in the owned Jacobi job.
Rejected Alternatives: Exact 1/6 slicing and `% Width`/`% Depth` decode were rejected because they put division/modulo back into a Burst job.
Scalability potential: Low amortizes diffusion across more frames with stable visuals; Ultra can increase visual projection quality because the authority heat grid no longer clumps.
Hardware Impact: Estimated 500-1800 us spike flattening during thermal diffusion sweeps; total work remains similar, frame pacing improves.

## Decision 8: Stop At First External Compile Wall
Problem: The first Unity compile failed in `DeployableSdfDrillRuntime`, a mining-domain file that did not implement unrelated interfaces.
Solution: Mark Task 18 as `[BLOCKED BY DEPENDENCY]`, record the exact files/errors, and avoid editing another agent's domain.
Rejected Alternatives: Patching mining runtime interfaces from this bucketer task was rejected as cross-domain sabotage risk.
Scalability potential: Bucketer code remains isolated and ready for verification after the external compile wall clears.
Hardware Impact: No runtime impact; verification is blocked, not implementation.

## Decision 9: Prevent High-Tier Bucket Overlap
Problem: A two-active-bucket high-tier mode can silently double-sample entities if it slides a `{active, active+1}` window every frame.
Solution: Convert active slow buckets into non-overlapping power-of-two groups. High tier runs bucket groups `{0,1}`, `{2,3}`, etc.; low/debt/AUP keeps one bucket per frame.
Rejected Alternatives: A sliding active-window check was rejected because adjacent frames would repeat half the work and corrupt frame pacing estimates.
Scalability potential: Low stays at 128 one-bucket frames; Middle keeps 64 one-bucket frames when under debt; High/Ultra run two clean buckets per frame with deterministic no-overlap exposure.
Hardware Impact: Estimated 50-300 us duplicate-work avoidance under future registered slow systems, with exact profiler capture still blocked by external compile walls.

## Decision 10: Remove Slow-Accumulator Aliasing For Bucketed Systems
Problem: `IBucketedSlowTickable` objects registered in dispatcher slow lanes were sampled only on the 0.1s slow accumulator, so active bucket IDs could alias against the sample frame and starve or clump future systems.
Solution: Add a dedicated per-frame bucketed slow pass guarded by `_bucketedSlowTickableCount <= 0`; normal slow ticks now skip `IBucketedSlowTickable` objects entirely.
Rejected Alternatives: Keeping bucket checks inside `RunSlowTick` was rejected because it looks correct in small tests while failing at frame-rate/cadence gcd boundaries.
Scalability potential: Low pays zero extra work when no bucketed slow systems are registered; High/Ultra can register future bucketed services without sampling artifacts.
Hardware Impact: 0 us hot overhead when count is zero except one integer branch. With future bucketed systems, expected gain is spike removal rather than total work reduction.

## Decision 11: Compile Wall Rechecked And Left External
Problem: After second-pass edits, Unity's compile wall moved from mining-domain interface errors to `GlobalDataVault.cs` memory-defrag symbols and asmdef references owned by another in-flight subsystem.
Solution: Re-run targeted validation for bucketer-owned files, record the exact current console wall, and avoid patching incomplete memory-defrag work from this bucketer prompt.
Rejected Alternatives: Adding local stubs or forcing `Hecton8.Core.Memory` to reference root `Hecton8.Core` was rejected because it risks circular asmdef damage and would mask another agent's incomplete architecture.
Scalability potential: Bucketer remains isolated and deterministic while the memory-defrag owner repairs their dependency boundary.
Hardware Impact: No runtime impact from this decision; verification remains blocked by external compile errors, not by the bucketer pass.

## Decision 12: Mirror Registry Service Instead Of Trusting Cached Bucketer
Problem: Dispatcher and voxel integrations could keep a stale bucketer pointer after bootstrap teardown or registry service replacement, causing unsliced work or a disposed service reference.
Solution: Dispatcher now mirrors `GlobalRegistry.SimulationBucketer` every simulation frame, and voxel reacquires when the cached service is null or uninitialized. Bootstrap also initializes an externally registered bucketer if it exists without native storage.
Rejected Alternatives: Hot-swap listener registration was rejected for this pass because a single static registry read is cheaper, simpler, and avoids adding another listener lifecycle path to a central frame authority.
Scalability potential: Low/MX350 retains the 128-bucket flattening after scene transitions; High/Ultra retain two-bucket grouped cadence after service replacement.
Hardware Impact: Dispatcher pays one static service read per frame, estimated below 0.1 us. Avoided fallback queue clumps remain worth 100-600 us on voxel-heavy frames.

## Decision 13: Clamp Entity Bucket Capacity Before Power-Of-Two Rounding
Problem: External callers can pass pathological entity capacity values; unchecked round-up can overflow or request a massive persistent native allocation.
Solution: Add `SimulationBucketConstants.MaxEntityCapacity = 1 << 20` and clamp `ModuloSimulationBucketer.Initialize` before power-of-two rounding. The public helper also guards values above `0x40000000`.
Rejected Alternatives: Trusting bootstrap-only default capacity was rejected because the service contract is public and other agents can call it directly.
Scalability potential: Low devices stay at 8192 by default; High/Ultra can safely scale up to 1,048,576 bucket entries (4 MiB) without allocator surprise.
Hardware Impact: 0 us hot-path cost. Cold initialization avoids accidental multi-GB allocation attempts and catastrophic editor/runtime stalls.

## Decision 14: Third Compile Wall Remains External
Problem: After third-pass edits, Unity compile advanced past the memory-defrag wall and now fails in `VehicleSubOsCockpitRuntime.cs` for missing `Hecton8.UI.Diegetic`/`IDiegeticDamageHologramReadModel`.
Solution: Record the exact current wall and keep GLOBAL_SIMULATION_BUCKETER `PENDING VERIFICATION` instead of editing UI diegetic work outside this task.
Rejected Alternatives: Adding UI namespace stubs from the bucketer task was rejected because it would fake an unrelated contract and hide the owning agent's dependency break.
Scalability potential: Bucketer remains ready for profiler verification once UI diegetic compile errors clear.
Hardware Impact: No runtime impact; compile verification remains blocked externally.

## OMEGA POLISH CHANGES
Problem: The post-task polish mandate required an anti-bloat pass after core tasks were checked/blocked.
Solution: Re-read all `<POLISH_MANDATE id="OMEGA_POLISH">` blocks after the checklist reached done/blocked state, then ran diff-focused scans for managed `foreach`, `string.Format`, `$"`, `.ToString(`, `math.sqrt`, `Mathf.Sqrt`, `math.normalize`, `.normalized`, and added `%` modulo in the bucketer diff. No added hot-path violations were found. Exact cadence work already uses bitmasks and `math.rcp` where a reciprocal is needed.
Rejected Alternatives: Editing unrelated legacy `foreach`/string hits in third-party plugins and other agents' files was rejected because this task owns simulation bucketing, not a whole-repo Zero-GC cleanup.
Scalability potential: Low/MX350 keeps the 128-bucket slow cadence and 1/8 thermal slices; Middle keeps 64-bucket authority; High can process two slow buckets while debt is zero; Ultra spends the stable CPU budget on visual overkill rather than more authority work.
Hardware Impact: Estimated net effect remains spike flattening: 300-1500 us for fauna, 100-600 us for voxel bursts, 500-1800 us for thermal diffusion, 200-900 us during admission debt, and 25-200 us on watchdog/LOD/AUP frames. Runtime profiler capture remains blocked by global compile dependencies.

## Cinematic Cheats Used
- Modulo time-slicing fakes continuous slow simulation with staggered authority buckets.
- Thermal diffusion uses 1/8 Jacobi grid slices instead of full-grid physical diffusion per cold tick.
- Biolum distant LOD uses a fast-bucket scheduling fake instead of honest per-frame distance-update cadence.
- Voxel queue ingress uses deterministic fast-bucket deferral instead of immediate physical queue drain.
- AUP safety uses single-bucket cadence throttling instead of recomputing every staggered entity on shift frames.

## Final Git Diff Summary
Generated with `git diff --stat -- <bucketer-owned paths>` after Omega polish. The stat includes concurrent same-file changes by other agents in shared files; bucketer-owned additions are the simulation bucketer contract/asmdef/concrete service and the scoped call-site integrations listed here.

```text
Assets/_Project/Scripts/Core/Contracts/SimulationBucketingContracts.cs      new
Assets/_Project/Scripts/Core/Bucketing/Hecton8.Core.Bucketing.asmdef        new
Assets/_Project/Scripts/Core/Bucketing/ModuloSimulationBucketer.cs          new
Assets/_Project/Scripts/Hecton8.Core.asmdef                                 + bucketing reference
Assets/_Project/Scripts/Core/GlobalRegistry.cs                              + SimulationBucketer service slot wiring
Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs                     + ISimulationBucketer slot id
Assets/_Project/Scripts/Core/Memory/H8Memory.cs                             + SystemID.SimulationBucketer
Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs                       + bootstrap lifecycle registration
Assets/_Project/Scripts/Core/SystemDispatcher.cs                            + SIMULATION phase advance/gating/telemetry
Assets/_Project/Scripts/Fauna/FaunaBrain.cs                                 + bucketed slow tick and interpolation alpha
Assets/_Project/Scripts/VoxelDeltaProcessor.cs                              + 4-way fast-bucket queue slicing
Assets/_Project/Scripts/World/AbyssalThermalManager.cs                      + 1/8 Jacobi thermal slicing
Assets/_Project/Scripts/World/Biolum/HectonBiolumZone.cs                    + frame-clump LOD removal
Assets/_Project/Scripts/GlobalPhysicsStateManager.cs                        + AUP watchdog countdown gate
Assets/_Project/Scripts/HectonFloatingOrigin.cs                             + precision watchdog countdown gate
Docs/Tasks/Status_GLOBAL_SIMULATION_BUCKETER.md                             + full checklist and loop evidence
Docs/AgentLogs/Rationale_GLOBAL_SIMULATION_BUCKETER.md                      + rationale and Omega polish evidence
```

## Build Evidence
`dotnet build Hecton8.Core.csproj` exit code: 1. First failures are missing/generated assembly references (`Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Audio.Propagation`, stale `Hecton8.Core.Bucketing` csproj reference, missing world/audio/vehicle contract types). This is a global generated-project dependency wall. Targeted Unity validation for new bucketer files is clean and Unity console reports only unrelated mining-domain interface errors.

Second-pass Unity compile request completed to idle. Current console errors are in `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs`: missing `Hecton8.Core.NativeMemorySentinel`, `Hecton8.Core.NativeAllocationLifetime`, `_gapAuditResult`, `VaultGapAuditJob`, `VaultGapAuditResult`, `FragmentationRatioThreshold`, and `Hecton8.Core.GlobalRegistry`. This matches another memory-defrag workstream, not the bucketer implementation.

Third-pass Unity compile request timed out once while compiling, then returned idle. Current console errors are in `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs`: missing `Hecton8.UI.Diegetic` namespace and `IDiegeticDamageHologramReadModel`, followed by a Unity entry-point exception. This matches another UI diegetic workstream, not the bucketer implementation.
