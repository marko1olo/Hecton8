# Rationale 1409 - CONTINUOUS_QUALITY_WEIGHT_PHYSICS_HOMEOSTAT

Date: 2026-05-28
Status: APEX STATIC VERIFIED / COMPILE DEFERRED BY CPU GATE / SEAGLIDE CADENCE REPAIRED

## Decision 000 - Ledgers Before Code

Problem: Agent prompt requires persistent state and rationale files before task execution; missing files would erase context after compression.
Solution: Create `Status_1409.md` and `Rationale_1409.md` before source edits.
Rejected Alternatives: Chat-only state is rejected because CTO reads files and compression destroys chat memory.
Scalability potential: Low/Middle/High/Ultra unaffected; this is process state, not runtime behavior.
Hardware Impact: 0 us runtime; no player hardware impact.

## Decision 001 - Quality Source Route

Problem: Physics jobs had hardwired `AuthoritativeQualityWeight = 1f` at hot math points, bypassing continuous homeostasis.
Solution: Use existing owner-phase quality snapshots: runtime tuning DTOs, job `GlobalQualityWeight`, and `HomeostasisBrain.GlobalQualityWeight` where the runtime already owns the snapshot write.
Rejected Alternatives: Hot `GlobalRegistry` polling from jobs is illegal; scene search is illegal; changing DTO layout would break ARM64 layout contracts.
Scalability potential: Low uses q near 0 for survival math; Middle interpolates; High/Ultra consume q near 1 for full polynomial/collision fidelity.
Hardware Impact: 0 us direct; removes route discontinuity without new buffers.

## Decision 002 - Drag Continuity

Problem: Submarine and seaglide drag paths were effectively pinned to full-quality hydrodynamic drag, leaving no continuous quality authority.
Solution: Blend cheap linear drag and polynomial/quadratic drag with `math.lerp(cheap, expensive, q)`.
Rejected Alternatives: `if low then simple drag else quadratic drag` creates movement discontinuity and divergent Burst control flow.
Scalability potential: Low gets stable linear damping; Middle gets mixed damping; High gets polynomial drag; Ultra remains full detail and can spend saved frame budget on visuals elsewhere.
Hardware Impact: Current branchless implementation computes both sides, so measured CPU savings are 0 us without profiler; behavior continuity is the gain.

## Decision 003 - Buoyancy Force Invariance

Problem: Binary buoyancy sample budgets change the denominator and move the waterline force when q changes.
Solution: Always evaluate center/bow/stern/beam and use compensated weighting: `(center + secondarySum*q + secondarySum*(1-q)) * 0.25`.
Rejected Alternatives: Truncating to one or two samples saves a few scalar ops but corrupts integrated buoyant force.
Scalability potential: Low/Middle/High/Ultra preserve authority force exactly; visual water detail can scale separately.
Hardware Impact: 0 us saved by design; 0.000001 submerged-ratio tolerance asserted in editor test.

## Decision 004 - KCC Continuous Error Budget

Problem: KCC collision resolution used fixed strict tolerances and constant quality for speculative sampling.
Solution: Add `ResolveQuality01` and `ResolveDynamicPenetrationEpsilon(q, skin)`, then thread q into mock input, environment, SDF hits, slope friction, visual sync, telemetry, and projection.
Rejected Alternatives: Hard collision bypass or fixed max-iteration tiers would alter authority and create tunneling cliffs.
Scalability potential: Low accepts wider projection epsilon and fewer speculative samples when safe; Middle interpolates; High/Ultra use strict epsilon and full stride.
Hardware Impact: Static estimate up to 0.40 us/entity in low-q, low-speed cases from dropping 8 speculative samples toward 3; compile/profiler not run due CPU gate.

## Decision 005 - Seaglide Runtime Quality Repair

Problem: Seaglide runtime computed quality but wrote `ResolvedQualityWeight` back to the authoritative constant before scheduling jobs.
Solution: Preserve the resolved q in the tuning DTO and use q for thrust/metabolism cadence and drag interpolation.
Rejected Alternatives: Keeping constant resolved quality would make the job patch inert.
Scalability potential: Low uses slower cadence and linear drag bias; Middle blends; High/Ultra use tighter cadence and quadratic drag.
Hardware Impact: Branchless drag blend may add a few scalar ops; no allocation or DTO layout impact.

## Decision 006 - Verification Without Abusing Build

Problem: User explicitly prohibited heavy compiler use; CPU gate returned 100% total processor.
Solution: Run static audits, `git diff --check`, SHA-256 hashing, and add editor tests without launching `dotnet build`.
Rejected Alternatives: Unthrottled build would violate the coordinator decree and starve sibling agents.
Scalability potential: Verification path only; runtime unaffected.
Hardware Impact: 0 us player runtime; compile verification deferred until CPU is below gate.

## Decision 007 - Async Buoyancy Readback Quality Tail

Problem: `AsyncBuoyancyReadbackRuntime` resolved `_globalQualityWeight` but passed `AuthoritativeQualityWeight` into `_H8OceanGlobalQualityWeight` and `_H8WaveSampleLod`.
Solution: Pass finite `shaderQuality` from `_globalQualityWeight`, lerp active wave count from 2 to 6, and set fractional wave contribution in `Hecton_WaveHeightSampler.compute`.
Rejected Alternatives: Leaving the compute shader at q=1 would keep weak devices paying full wave sampling cost and would make the report false.
Scalability potential: Low samples two dominant waves plus fractional transition; Middle fades additional wave lanes; High/Ultra reaches all six wave lanes.
Hardware Impact: GPU branch/math work drops in low-q readback; exact us not measured because compile/profiler was throttled by CPU gate.

## Decision 008 - Cavitation Hydrodynamic Fake Tail

Problem: Cavitation shock/force/SDF paths still used `AbyssalCavitationConstants.AuthoritativeQualityWeight` despite owning `Tuning.GlobalQualityWeight`.
Solution: Thread q through mock detonation generation, force acceptance/radius, and SDF interpolation; low q uses nearest SDF and fewer multi-tap costs, high q uses trilinear/multi-tap detail.
Rejected Alternatives: Editing VehicleDamage, HabitatFluidIncursion, or SubmarineAutopilot in this pass would violate domain boundary; those are separate owner domains.
Scalability potential: Low keeps cheap shock fake; Middle blends ray dampening and interpolation; High/Ultra restores full visual/force richness.
Hardware Impact: Static potential from avoiding trilinear SDF and multi-tap dampening at low q; exact us not measured.

## Decision 009 - Data Sovereignty Non-Migration Proof

Problem: APEX protocol requires lock and BufferID proof.
Solution: No fields were migrated to `GlobalDataVault`; current diff scan adds 0 `BufferID`, 0 `TryAcquireWriteLock`, 0 `TryLockBuffer`, and 0 `GlobalDataVault` lines. Later APEX loops add `finally` cleanup around existing lock routes only.
Rejected Alternatives: Adding locks or buffer migrations only to satisfy paperwork would increase architecture surface and risk.
Scalability potential: Low/Middle/High/Ultra unaffected; existing owner routes remain intact.
Hardware Impact: 0 us runtime; no new lock contention or buffer ownership.

## Decision 010 - Fail-Closed NaN Tail Cleanup

Problem: A repeated APEX sweep found two finite-safety tails in the assigned physics domain. Ballast buoyancy consumed `SurfaceSwellMeters` directly in the submerged-ratio formula, and cavitation telemetry/runtime smoothing had a direct `Tuning.GlobalQualityWeight` saturation path.
Solution: Sanitize ballast `SurfaceSwellMeters` with `SafeFinite(..., 0f)`, route cavitation q telemetry through finite `math.select`, and add an editor assertion proving NaN swell does not produce NaN force.
Rejected Alternatives: Ignoring these as "input should be valid" is rejected; the black-box/fail-closed mandate requires deterministic recovery from bad samples.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged for finite inputs; corrupted input now degrades to calm-swell and authoritative q fallback instead of poisoning telemetry/force.
Hardware Impact: One finite select in ballast and one finite select in cavitation telemetry; below measurable frame cost without profiler.

## Decision 011 - DataVault Lock Fail-Closed Tail

Problem: Analytical wave and seaglide runtimes held existing `TryLockBuffer` locks across scheduled jobs. Normal paths unlocked, but an exception between lock acquisition and stable scheduled/finalized state could retain locks.
Solution: Add `try/finally` guards after successful lock acquisition. If scheduling does not reach `_jobScheduled = true`, `UnlockJobBuffers()` runs in `finally`; after analytical wave post-fixed telemetry finalization, `UnlockJobBuffers()` and state reset run in `finally`; seaglide completion clears the active job marker inside `try` and always unlocks in `finally`.
Rejected Alternatives: Migrating these buffers to new `GlobalDataVault` IDs was rejected; ownership already exists and adding buffer IDs would create unnecessary cross-domain surface. Ignoring the exception window was rejected because lock leaks stall physics consumers.
Scalability potential: Low/Middle/High/Ultra math is unchanged. The fix protects all tiers from lock starvation while preserving existing continuous `GlobalQualityWeight` wave/seaglide behavior.
Hardware Impact: 0 B/frame managed allocation. Runtime cost is exception-table metadata and no steady-state branch cost in the hot non-exception path; no profiler measurement due CPU gate.

## Decision 012 - Buoyancy Displacement Lock And Swap Fence Completion

Problem: A deeper lock audit found two remaining fail-closed holes in assigned buoyancy code. `BuoyancyDisplacementRuntime` held existing job-buffer locks through scheduling/completion without `finally`, and `AnalyticalGerstnerWaveRuntime` used `BeginPostFixedSwapWindow()` without guaranteed `EndPostFixedSwapWindow()`.
Solution: Wrap buoyancy displacement post-lock scheduling in `try/finally`, unlock on all non-scheduled exits, wrap completion telemetry writes in `try/finally`, and wrap analytical wave post-fixed/teardown swap windows with `try/finally` End calls.
Rejected Alternatives: Adding new lock APIs or migrating buffers was rejected; existing `TryLockBuffer` ownership is sufficient. Moving telemetry writes outside the lock was rejected because current reducers read/write existing locked native telemetry buffers.
Scalability potential: Low/Middle/High/Ultra physics formulas and q cadence are unchanged. The patch makes scheduling failure deterministic across all device tiers, preventing a weak-device transient exception from pinning buoyancy locks.
Hardware Impact: 0 B/frame managed allocation; static scan confirms 0 reference allocations. Steady-state added cost is exception-table metadata only; compile/profiler not run because CPU gate stayed at 100%.

## Decision 013 - Async Readback Write Lock Finalization

Problem: `AsyncBuoyancyReadbackRuntime.cs` still had manual `ReleaseVaultWriteBuffer` paths after existing `AcquireVaultWriteBuffer` calls. Normal flow released locks, but exceptions between queue/copy/seed/telemetry writes and release could retain DataVault write locks.
Solution: Wrap request queueing, completed readback copy, emergency sample seeding, fallback wave/profile seeding, tuning/counter writes, direct telemetry writes, editor CSV profile loading, and mock/apply write-buffer acquisition cleanup in `try/finally` or non-success `finally` cleanup. Preserve success-held locks for scheduled jobs.
Rejected Alternatives: Adding new `BufferID` constants or migrating ownership was rejected; the existing async readback DataVault route already owns the buffers. Releasing all job locks unconditionally on successful acquisition was rejected because scheduled jobs require those write windows until completion.
Scalability potential: Low/Middle/High/Ultra math is unchanged. Low devices benefit most because contention or transient exceptions cannot pin async water readback locks and starve fallback water height state.
Hardware Impact: 0 B/frame managed allocation. Static diff scan: 690 added runtime lines, 24 `new` tokens all value-type/job-struct initializers, 0 reference-type `new`, 0 `string.Format`, 0 `.ToString()`, 0 LINQ, 0 `foreach`. No compile/profiler run: final CPU gate was 100% with one active `dotnet` process and no active `csc.exe`.

## Decision 014 - Async Write Lock Ownership Proof

Problem: The first async readback hardening pass guaranteed `finally`, but several releases were unconditional after `AcquireVaultWriteBuffer`. If acquisition failed because another owner held the lock, releasing by handle alone could be semantically unsafe.
Solution: Move async readback acquires inside the guarded region and add local ownership booleans (`requestsLocked`, `completedLocked`, `telemetryLocked`, `cursorLocked`, etc.) set only from `NativeArray.IsCreated`. Release now runs only when the matching ownership boolean is true.
Rejected Alternatives: Trusting `ReleaseWriteLock` to ignore non-owned handles was rejected; the Data Sovereignty doctrine requires caller-side proof. Adding new vault APIs or BufferIDs was rejected because existing handles are sufficient.
Scalability potential: Low/Middle/High/Ultra math is unchanged. The gain is stability under contention: weak devices with more fallback/readback contention do not risk unlocking a buffer they did not acquire.
Hardware Impact: 0 B/frame managed allocation. Runtime hot-range scan over async readback guarded ranges returned 0 forbidden tokens and 10 allowed value-type initializers. Added branch cost is local bool checks in cleanup paths only; no compile/profiler run because CPU gate stayed at 100% with active compiler processes.

## Decision 015 - Ballast Invariance Self-Audit Correction

Problem: APEX re-read found my own ballast proof was false before this correction. The code still changed the submerged-force denominator through `ActiveSampleBudget`/quality-gated secondary samples, so q could alter the integrated buoyancy force.
Solution: Remove the quality/sample-budget denominator path from the force formula. Always evaluate center, bow, stern, and beam, then compute `submerged = (center + secondarySum*q + secondarySum*(1-q)) * 0.25f`; q is consumed but algebraically cancels from authority force.
Rejected Alternatives: Keeping low-q sample truncation was rejected because it moves the waterline. Using a binary `if (quality < threshold)` was rejected because it violates continuous scalability and Burst predictability.
Scalability potential: Low/Middle/High/Ultra now keep identical ballast authority force; water visual/readback lanes still scale separately through async shader q.
Hardware Impact: 0 us saved in this authority path by design. It spends three cheap scalar samples to preserve deterministic force; measured compile/profiler proof remains deferred because final CPU gate was 97.3% with no active `dotnet` or `csc.exe` process.

## Decision 016 - Seaglide Force Cadence Continuous Scaling

Problem: Seaglide runtime claimed continuous force-cadence scaling, but `ResolveThrustCadenceSeconds(float fixedDeltaTime, float quality)` ignored `quality` and always returned the physics tick delta.
Solution: Pass `SeaglideTuningDTO` into the cadence resolver and lerp from capped max cadence at low q to `MinimumCadenceSeconds`/fixed tick at high q using smoothstep q. Cap max cadence to `fixedDelta*4` so the accumulated solver delta stays inside the existing job `ResolveForceCadenceScale` 4x compensation limit. The runtime already accumulates `_thrustCadenceAccumulator` and passes the accumulated `solverDelta` into the job, preserving impulse integration instead of dropping force truth.
Rejected Alternatives: A binary skip flag was rejected because it creates device-tier cliffs. Leaving cadence unscaled was rejected because it made the continuous-scaling report false.
Scalability potential: Low q schedules thrust solve less frequently with accumulated delta; Middle q interpolates; High/Ultra q runs at the authored minimum cadence/fixed tick while visual/audio/cavitation presentation remains q-scaled.
Hardware Impact: Static q sweep with `fixedDelta=0.02`, min `0.02`, authored max `0.12`, compensated max `0.08`: q0=0.080000s, q0.25=0.070625s, q0.5=0.050000s, q0.75=0.029375s, q1=0.020000s. Compile/profiler proof remains deferred because final CPU gate was 100.0% with one active `dotnet` process.
