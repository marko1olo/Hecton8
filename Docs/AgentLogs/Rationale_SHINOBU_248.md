# SHINOBU_248 Rationale

Status: STATIC PASS / EIGHTH POLISH PASS APPLIED / COMPILE BLOCKED BY CPU GATE
Evidence class: STATIC_SOURCE until compile/runtime artifacts exist.

## Mandate Selection

Problem: Shockwave inverse-square math can produce non-finite force packets at the explosion epicenter.
Solution: Use physics, AUP, zero-GC, ARM64 layout, black-box telemetry, visual-fake, and noir shader mandates as the governing constraint set before code edits.
Rejected Alternatives: Using Unity Rigidbody/PhysX queries directly in the shockwave loop; using managed collections or runtime scene searches; using binary quality toggles for visuals.
Scalability potential: Low uses fixed safe math and cheaper visual scalar; Middle retains stable physics with moderate cavitation; High/Ultra spend saved CPU on richer shader distortion, not more gameplay truth.
Hardware Impact: Expected low-end i3/MX350 gain comes from avoiding PhysX overlap and CPU particle bubbles; exact microseconds are PENDING static/compile/runtime verification.

## Batch Hygiene

Problem: Required SHINOBU_248 status and rationale files were missing at session start.
Solution: Create fresh active batch files under Docs/Tasks and Docs/AgentLogs.
Rejected Alternatives: Writing only chat status; using old archived batch data.
Scalability potential: File-backed state survives context compression and parallel-agent noise.
Hardware Impact: No runtime hardware impact; workflow control only.

## Inverse-Square Epsilon Guard

Problem: Explosion epicenter overlap produced `distSq == 0`, making `PeakPressure / distSq` non-finite and allowing force packet contamination.
Solution: Compute `rawDistanceSq`, finite-select invalid values to zero, then clamp with `math.max(..., 0.0001f)`. Direction is `delta * math.rsqrt(math.max(distanceSq, epsilon))`, so the same guarded denominator controls normalization. Mark `EpsilonClamped` for black-box telemetry.
Rejected Alternatives: Branching zero distance into no-force; it hides the blast and fails the singularity test. PhysX overlap/raycast fallback was rejected as slower and non-deterministic for the Burst kernel.
Scalability potential: Low keeps the same stable physical truth with cheaper candidate acceptance. Middle keeps all critical receivers. High/Ultra spend spare time on more cavitation spheres and shader intensity, not more authority routes.
Hardware Impact: On i3/MX350, expected gain is avoiding non-finite drain fallout and wake/audio secondary churn; static estimate 10-18 us per heavy blast frame. Runtime profiler proof pending.

## Force Transport DTO

Problem: Rich `ShockwaveForcePacketDTO` is 64 bytes and carries AUP/application metadata; hot transport only needs force, target, flags, and optional torque.
Solution: Add explicit 32-byte `ForcePacketDTO`, backed by its own GlobalDataVault buffer ID 71571, with UnsafeUtility offset validation.
Rejected Alternatives: Reusing only the 64-byte packet for every drain or adding managed wrapper objects. Both waste cache and violate the zero-GC hot path.
Scalability potential: Low scans tighter rows under capped packet budget. Middle/High retain rich packet for diagnostics. Ultra can raise visual fidelity without changing transport layout.
Hardware Impact: For 512 candidate slots, static cache-traffic estimate is roughly 3 us/frame saved on i3/MX350 class silicon. Exact profiler proof pending.

## Visual Cavitation Link

Problem: Physical pressure needed a visible cavitation bubble without simulating water volumes or CPU particles.
Solution: Reuse `CavitationVisualSphereDTO` shader buffer. `UpdateCavityShaderParamsJob` derives radius/intensity/age from shockwaves, and upload intensity is multiplied by continuous `GlobalQualityWeight`.
Rejected Alternatives: ParticleSystem bubbles, instantiated decals, or CPU-side fluid simulation. Those spend frame time on fake physics instead of the intended Sweet Lie shader.
Scalability potential: Low uploads a small sphere budget with dim intensity. Middle expands upload count. High/Ultra increase distortion and visible radius while gameplay force remains identical.
Hardware Impact: Static estimate 80-250 us/frame saved versus CPU particle bubble fanout on low-end GPU/CPU combinations. Runtime GPU timing pending.

## Acoustic Bridge

Problem: Blast pressure must hit audio as a deafening event, but adding a new signal lane during a hot fix risks route debt.
Solution: Add 64-byte `AcousticDeafeningSignal` as a local derived DTO and bridge its intensity into existing `AcousticPingSignal`.
Rejected Alternatives: New GlobalSignals direct queue, HectonEventBus managed event, or per-listener scene search. All are worse for hot broadcast ownership.
Scalability potential: Low receives one bounded ping. Middle/High/Ultra can let audio systems scale filtering/ducking from the same intensity float.
Hardware Impact: Static estimate 8 us/event saved by reusing an initialized SignalBus lane. Exact DSP path timing pending.

## Singularity Harness

Problem: Manual gameplay explosions are poor proof for division-by-zero because exact overlap is rare and non-repeatable.
Solution: Add `GenerateMockSingularityExplosionJob` and editor button. It places entity and blast at identical AUP with peak pressure high enough to exercise the epsilon clamp.
Rejected Alternatives: Relying on QA repro video or ad hoc inspector value edits. Those do not prove the math path.
Scalability potential: No runtime cost unless explicitly invoked. Low through Ultra use the same deterministic harness.
Hardware Impact: 0 runtime cost in normal play; static debug iteration saving estimated at 40-50 us/setup cycle plus human time.

## Data And Report Route

Problem: Ordnance tuning and audit proof needed disk-backed artifacts, not chat claims.
Solution: Add `ordnance_blast_profiles.csv` with bounded ingest/fallback, create `Tools/Division_By_Zero_Scanner.py`, preserve previous physics report under `preservedPreviousReport`, and emit SHINOBU_248 self-audit XML.
Rejected Alternatives: Hard-coded blast tables only, overwriting SHINOBU_227 report history, or broad edits to unrelated Physics owners.
Scalability potential: Low ships minimal profiles. Middle/High/Ultra can tune visual intensity and force scale from data without authority route changes.
Hardware Impact: No hot runtime cost after ingest; static load path remains bounded by existing scratch buffer.

## Verification Boundary

Problem: Compile proof was required but the project rule forbids `dotnet build` when CPU is above 50 percent or csc/dotnet is already active.
Solution: Checked CPU/dotnet/csc before compile. CPU was 100 percent, no dotnet/csc process was active, so build was not launched and status is `COMPILE BLOCKED BY CPU GATE`.
Rejected Alternatives: Ignoring the CPU gate or claiming Unity import proof from static scans. Both would be false reports.
Scalability potential: Verification only; no runtime tier impact.
Hardware Impact: Avoided adding build load to an already saturated machine.

## Second Polish Pass

Problem: First pass still treated some assignment details as implicit: epsilon was hardcoded, non-critical radius shedding was mostly stochastic, SDF occlusion was a midpoint probe, tuner did not expose all requested controls, and force gizmo drew shockwave spheres without force arrows.
Solution: Reused prior 64-byte tuning padding for `InverseSquareMultiplier`, `EpsilonClampValue`, and `SdfOcclusionDampening`; added layout offset validation; changed inverse-square math to use the Vault-backed epsilon and multiplier; added branchless non-critical radius scale through `math.select`; upgraded SDF dampening to low-cost midpoint at low quality and 3-point p25/p50/p75 blend at high quality; added tuner sliders, histogram bars, and force-vector gizmo arrows from ForcePacketDTO rows.
Rejected Alternatives: Growing `AbyssalCavitationTuningDTO` past 64 bytes; adding a new tuning buffer; using true Physics.Raycast/OverlapSphere occlusion; drawing debug arrows by searching scene objects; using binary quality branches for gameplay truth.
Scalability potential: Low uses 50% non-critical radius, one SDF sample, small shader budget, and dim cavitation. Middle blends SDF richness and radius. High/Ultra run full radius for all receivers and richer shader distortion without changing force truth ownership.
Hardware Impact: Additional static estimate 25-90 us/frame saved in debris-heavy blasts from radius culling; 12-35 us/blast saved by midpoint SDF collapse under low quality. Runtime profiler proof pending.

## Route Documentation

Problem: The previous proof artifacts did not register the SHINOBU_248 payload boundary in the central binary ledger and did not include a route card for global authority review.
Solution: Added `Docs/ARCHITECTURE/SHINOBU_248_SHOCKWAVE_NAN_ROUTE_CARD.md` and a SHINOBU_248 addendum to `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
Rejected Alternatives: Leaving route proof only in the chat or status file; claiming Data Monolith readiness from a CSV bridge.
Scalability potential: Documentation only; runtime route remains unchanged.
Hardware Impact: 0 runtime cost.

## Third Polish Pass - Vault Descriptor Hygiene

Problem: The second pass still persisted `VaultBufferHandle<T>` fields in `AbyssalCavitationRuntime`. Current Core marks that type as a legacy pointer-bearing migration bridge, which leaves stale-handle ambiguity even when the cached pointer is not trusted.
Solution: Replace persistent Cavitation handles with 16-byte `VaultGenerationHandle<T>` descriptors acquired by `IDataVault.GetGenerationHandle(...)`; every use opens a method-local `NativeArray<T>` view through `IDataVault.TryResolveHandle(...)` via `OpenVaultView(...)`.
Rejected Alternatives: Keeping `VaultBufferHandle<T>` because it already worked statically; adding local native buffers; widening DTOs or changing BufferID identities; converting the force route into a new direct `NativeQueue` lane when the current doctrine treats ad hoc MPSC queues as legacy debt.
Scalability potential: Low through Ultra keep identical DTO layouts and force truth. The change affects ownership safety, not visual fidelity. High/Ultra still spend saved CPU on shader cavitation, not additional gameplay routes.
Hardware Impact: Static estimate 1-3 us saved only during fault/relocation windows by avoiding stale pointer refresh/recovery work on low-end silicon. Normal-frame runtime gain is not claimed without profiler proof.

## Third Polish Pass - Prompt And Gate Hygiene

Problem: A strict exact-tag extraction of `CURRENT_BATCH.md` returned no SHINOBU_248 prompt because the active tag includes extra attributes. Treating that miss as truth would corrupt scope; treating memory as truth would violate anti-amnesia.
Solution: Re-ran an attribute-aware CLI extraction and recovered the full 20-task SHINOBU_248 block. Re-ran scanner, focused residue grep, and diff hygiene after the descriptor migration.
Rejected Alternatives: Borrowing neighboring prompts; ignoring the failed exact regex; launching a build while CPU was above the project gate.
Scalability potential: Proof hygiene only; no runtime route change.
Hardware Impact: Avoided a build on a saturated machine. CPU gate measured 99.7 percent with no dotnet/csc process; compile remains pending.

## Fourth Polish Pass - Fault Route And Hot Tick Guard

Problem: Static review still found three runtime hazards: crash dumps only occurred after non-finite telemetry finalization, `TryDumpBlackBox` could write relative to an unstable current directory and truncate the existing artifact, and hot tick entry points could call `EnsureInitialized`, which cold-polls `GlobalRegistry.DataVault`.
Solution: Register an editor/development `Application.logMessageReceived` fault hook during cold initialization; guard reentrant dump attempts with `Interlocked.Exchange`; make `TryDumpBlackBox` fail closed unless `IsRuntimeReady` and no job is scheduled; resolve the dump path from the Unity project root and write through `.tmp` plus atomic replace/move. `ScheduleSimulation`, force flushes, shader sync, entity writes, and detonation queues now use `IsRuntimeReady` instead of hot initialization.
Rejected Alternatives: Dumping while the telemetry writer job may still be mutating the ring; using `Directory.GetCurrentDirectory()` as a proof path; direct `FileMode.Create` on the final dump; allowing FixedTick to initialize Vault ownership through `GlobalRegistry`; adding a broad force-sink API outside the SHINOBU_248 ownership boundary.
Scalability potential: Low through Ultra use the same crash-proof route and DTO layout. Quality scaling remains in physics candidate acceptance, SDF sample count, and shader cavitation intensity; fault handling does not change gameplay truth or visual tier behavior.
Hardware Impact: Normal-frame cost remains effectively zero except boolean checks on hot entry. Static estimate is 1-4 us saved in missing-Vault/generation-mismatch hot ticks and 2-8 us/heavy force drain when `RigidbodySlot` resolves before hash fallback. Profiler proof remains pending.

## Fourth Polish Pass - Force Drain Resolver

Problem: Force application still used folded-hash lookup as the first body resolution path in `DrainCavitationForcePackets`, which wastes work when the authoritative packet already carries a body slot.
Solution: Resolve `GlobalPhysicsStateManager` once per drain, use `TryResolveTrackedBodyByIndex(manager, RigidbodySlot, targetHash)` first, then fall back to folded hash lookup only when the slot is stale or absent.
Rejected Alternatives: Per-packet `TryGetRuntimeManager` calls; a direct `Rigidbody[]` dependency in the Vault packet route; editing GlobalPhysicsStateManager internals or asmdef boundaries during this domain patch.
Scalability potential: Low devices benefit from slot-first resolution under capped budgets; High/Ultra can spend the saved CPU on stronger shader cavitation without changing force truth.
Hardware Impact: Static estimate 2-8 us per heavy drain on low-end silicon when most packets contain valid slots. Runtime profiler proof pending.

## Out-Of-Scope Integrator Debt

Problem: Galileo identified broad assembly and editor dependency debt plus the remaining `PhysicsApplySystem.EnsureRuntimeInstance()` same-drain sink lookup. The binary ledger also has an active broad SHINOBU_ARCHIVARIUS rewrite relative to HEAD, outside this domain.
Solution: Kept them documented instead of editing unrelated assembly files or public force APIs. SHINOBU_248 changed only Cavitation runtime/editor/data/docs plus the in-domain partial drain implementation and appended its range/addendum to the current ledger shape without reverting cross-agent document work.
Rejected Alternatives: Broad asmdef surgery, EasySave3 editor reference edits, restoring/replacing the ledger's non-SHINOBU_248 rewrite, or inventing a sink injection API in a parallel-agent codebase without an owner mandate.
Scalability potential: No runtime tier change.
Hardware Impact: No runtime claim.

## Fifth Polish Pass - Singularity Direction Fallback

Problem: The inverse-square denominator was guarded, but exact AUP overlap still made `delta == 0`, so the computed radial direction became zero and the singularity path could produce pressure/cavitation telemetry without a non-zero physical impulse.
Solution: Add `ResolveShockDirection` inside `EvaluateSanitizedShockwaveJob`. It keeps normal radial direction when `rawDistanceSq > epsilon`; when the denominator is epsilon-clamped, it uses a deterministic hash-derived unit vector from entity hash, source hash, frame index, and SHINOBU_248 source salt. No RNG state, no scene query, and no PhysX normal lookup are introduced.
Rejected Alternatives: Returning zero force for exact overlap; using `UnityEngine.Random`; using `Unity.Mathematics.Random` state for a direction that must not mutate gameplay state; asking PhysX for an overlap normal; adding a managed table of fallback vectors.
Scalability potential: Low through Ultra keep the same gameplay truth and DTO layout. Visual cavitation still scales continuously through `GlobalQualityWeight`; the fallback only defines a physically usable direction at a mathematical singularity.
Hardware Impact: Normal non-overlap path pays one boolean select and keeps radial rsqrt. Singularity path avoids downstream zero-force diagnostic churn; profiler proof pending.

## Fifth Polish Pass - Compile Gate

Problem: After the singularity-direction patch, compile proof was still requested by protocol, but the machine violated both build gates.
Solution: Rechecked CPU and compiler processes before build. CPU sampled 100.0 percent and an external `dotnet` process was active (`PID 29148`), so no rebuild was launched.
Rejected Alternatives: Running `dotnet build` into a saturated machine; killing a process not owned by SHINOBU_248; claiming compile proof from static scanner output.
Scalability potential: Verification only.
Hardware Impact: Avoided adding another compiler workload to a saturated host.

## Sixth Polish Pass - Non-Finite Terminal Clamp And Fallback Cost

Problem: Static readback found two remaining hot-kernel risks. First, a non-finite accumulated force cleared the vector but left `forceSq` as `NaN`, allowing an active zero-force packet to pass the later comparisons. Second, the deterministic singularity fallback used eager `math.select`, so `HashUnitDirection` was computed even when the radial direction was valid.
Solution: Set `forceSq = 0f` immediately when non-finite accumulated force is detected, causing the normal low-force gate to return without packet publication. Change `ResolveShockDirection` to return radial direction before computing the hash fallback, and wrap new uint hash multiplications in `unchecked`. Tighten all shockwave `IsActive` helpers to require finite radius, max radius, peak pressure, expansion speed, and epicenter AUP.
Rejected Alternatives: Keeping an active zero-force packet for telemetry; paying three hash mixes per normal shockwave/entity pair; relying on C# comparison behavior to reject infinity; adding managed diagnostics in the job.
Scalability potential: Low devices avoid unnecessary integer hash work on every normal force pair. Middle/High/Ultra behavior is unchanged except for stricter poison rejection.
Hardware Impact: Static estimate saves roughly 8-14 integer operations per non-singular shockwave/entity pair. Runtime profiler proof pending.

## Sixth Polish Pass - Compile Gate

Problem: Compile proof remains required, but the project rule forbids `dotnet build` while the host CPU is above 50 percent or a compiler process is active.
Solution: Rechecked the gate after sixth-pass static verification. CPU sampled 100.0 percent and no `dotnet`/`csc` process was active, so no rebuild was launched.
Rejected Alternatives: Launching a build into a saturated host; treating scanner success as compile proof.
Scalability potential: Verification only.
Hardware Impact: Avoided adding compiler load to a saturated machine.

## Seventh Polish Pass - Subagent Audit Closure

Problem: Mencius found proof drift and release-path hazards: code registered the cavitation Vault/jobs under `SystemID.Physics` while docs claimed `VehiclesPhysics`, the legacy SHINOBU_156 card still looked like a live owner for `71560..71570`, mock harness APIs could force-complete outside an explicit editor/dev fence, `SlowTick` and gizmos could trigger cold/global access, dump finalization depended only on `File.Replace`, and the scanner accepted generic `safe` text as guard proof.
Solution: Change `OwnerSystem` to `SystemID.VehiclesPhysics`; mark `SHINOBU_156_ABYSSAL_CAVITATION_ROUTE_CARD` historical/superseded for the live SHINOBU_248 route; fence mock scheduling and `injectMockOnEnable` behind `UNITY_EDITOR || DEVELOPMENT_BUILD`; make `SlowTick` fail closed unless the runtime is ready; make gizmos borrow the cached runtime Vault; add `File.Replace` delete+move fallback; require scanner denominator `math.max` or epsilon proof instead of broad safe-token acceptance.
Rejected Alternatives: Papering over the owner mismatch in docs only; editing unrelated DataVault internals; keeping forced-complete public release APIs for convenience; leaving `GlobalRegistry.DataVault` in gizmo callbacks; accepting broad scanner context as proof.
Scalability potential: Low through Ultra keep the same physics truth and visual scaling. These changes reduce route ambiguity and release risk without changing DTO layout or authority identity.
Hardware Impact: No steady-state runtime gain claimed. Missing-Vault editor callbacks avoid a small cold access path; scanner precision increased warnings from 7 to 69 while Cavitation runtime errors remain 0.

## Seventh Polish Pass - Compile Gate

Problem: After subagent audit closure and stricter scanner proof, compile proof was still requested but host CPU remained above the project gate.
Solution: Rechecked CPU/compiler state. CPU sampled 80.9 percent and no `dotnet`/`csc` process was active, so no rebuild was launched.
Rejected Alternatives: Running `dotnet build` above the 50 percent CPU limit; claiming compile proof from scanner/XML checks.
Scalability potential: Verification only.
Hardware Impact: Avoided adding compiler load while the host was still saturated.

## Eighth Polish Pass - Hot Writer Fail-Closed Closure

Problem: Static grep after the seventh pass still found `EnsureInitialized()` in public mutation surfaces. `TryApplyTuning`, `TryWriteSdfVolume`, and `TryClearSdfVolume` are not owner-phase bootstraps; letting them cold-poll `GlobalRegistry.DataVault` blurs the route even if they are usually editor-driven.
Solution: Convert those three writer APIs to `IsRuntimeReady` fail-closed gates. Keep `EnsureInitialized()` only in cold lifecycle/bootstrap/editor surfaces: `Awake`, `OnEnable`, cold CSV load, editor refresh/mutator, and editor/development mock injection. `RefreshTelemetryReadout` now uses `TrySampleLatestTelemetry` directly, so the 0.2-second editor read loop remains a pure read.
Rejected Alternatives: Initializing Vault ownership from every SDF/tuning write call; treating editor periodic telemetry as harmless even though it could mask runtime absence; deleting cold CSV/editor bootstrap paths and breaking the tuning facade.
Scalability potential: Low through Ultra keep identical physics truth, DTO layout, and shader quality curves. This pass tightens authority route hygiene only; it does not alter `GlobalQualityWeight` scaling.
Hardware Impact: Static estimate 1-4 us avoided in missing-Vault/generation-mismatch public writer calls and editor-only cold access churn removed from telemetry polling. Profiler proof remains pending.

## Eighth Polish Pass - Compile Gate

Problem: Static code and scanner proof were refreshed, but the build rule still forbids compilation when CPU is above 50 percent or compiler processes are active.
Solution: Rechecked the gate with elevated CIM access after sandbox denied the non-elevated query. Latest CPU sample was 99 percent and no `dotnet`/`csc` process was active, so no rebuild was launched.
Rejected Alternatives: Running `dotnet build` into a saturated host; reporting compile proof from static scanner output.
Scalability potential: Verification only.
Hardware Impact: Avoided adding compiler load to a fully saturated machine.
