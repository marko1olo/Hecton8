# Rationale_DOCKING_AUTOPILOT_SPLINE

## Decision 1 - Accept Injected Batch Prompt

Problem: Earlier disk state lacked `DOCKING_AUTOPILOT_SPLINE`, but the user supplied the prompt and `Docs/Tasks/CURRENT_BATCH.md` now contains the exact XML block.

Solution: Supersede the previous missing-prompt blocker, re-extract the XML by CLI, and use `DOCKING_AUTOPILOT_SPLINE | PHYSICS/VEHICLES | 18` as the active assignment.

Rejected Alternatives: Continuing with the stale blocker was rejected because it no longer matches disk evidence. Synthesizing beyond the XML was rejected; Phase 1 stays constrained to tasks 1-3.

Scalability potential: Low/Middle/High/Ultra behavior is deferred to later tasks, but Phase 1 must preserve the boundary for math LOD and current compensation.

Hardware Impact: 0 us/frame so far. This decision only corrects task authority.

## Decision 2 - Domain Bridge Is Necessary

Problem: The XML domain names `Assets/_Project/Scripts/Physics/Vehicles/Automation/`, while the current repo already has vehicle automation contracts in `Assets/_Project/Scripts/Vehicles/Automation/` and docking handlers in `Assets/_Project/Scripts/Construction/`.

Solution: Place the new docking autopilot source in the XML path `Assets/_Project/Scripts/Physics/Vehicles/Automation/` while keeping the namespace `Hecton8.Vehicles.Automation` compatible with existing docking signals. Touch `GlobalRegistry`, `GlobalDataVault` enum definitions, `Hecton8.Core.csproj`, and `VehicleDockingModule` only as cross-domain integration points required by Phase 1 and dotnet verification.

Rejected Alternatives: Creating a parallel orphan automation tree was rejected because it would split namespaces and leave existing handlers on the old path. Moving existing files was rejected because it is out-of-scope and unsafe with 20+ parallel agents.

Scalability potential: A central service/data buffer lets low-tier 10 Hz solve and high-tier overkill smoothing be layered without rewriting handler ownership.

Hardware Impact: Expected Phase 1 runtime cost is neutral-to-lower on i3/MX350 by replacing repeated object-space interpolation with cached spline state and service snapshots. Static estimate: ~2-4 us saved per active dock versus old linear/nlerp path, 0 B/frame.

## Decision 3 - Mandate Set For Phase 1

Problem: Docking spans movement authority, physics scheduling, service lifetime, data ownership, and future telemetry. Editing only the visible Lerp call would preserve architectural rot.

Solution: Apply these mandates: `CORE_Submarine_Vehicles_Kinematics_AUP`, `PHYS_Physics_Integrity_Determinism_ForceMode`, `ARCH_Global_Registry_ServiceLocator_DI_Init`, `MATH_AUP_Determinism_Sync`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `DBG_Telemetry_Crash_Reporting_PostMortem`, `ARCH_Execution_Phases`, and `CORE_Weather_Abyssal_FlowField_Currents`.

Rejected Alternatives: Unity `AnimationCurve`, `Vector3.Lerp`, `Quaternion.Slerp`, direct `Transform.position` authority, and singleton polling were rejected by prompt and mandates.

Scalability potential: Low: fixed cheap Bezier sample; Middle: service-buffered spline state; High: current-aware tangent and motion vectors; Ultra: later Hermite/visual overkill without changing authority data.

Hardware Impact: Target is 0 B/frame and sub-0.1 ms. Any additional cost must stay in cold registration or job-friendly value math.

## Decision 4 - Phase 1 Spline Authority

Problem: `VehicleDockingModule` advanced docking by linearly interpolating AUP position and nlerping rotation. That produced game-object motion and kept docking authority local to the handler.

Solution: Build `ActiveSplineData` once at capture using double3 P0-P3 control points. During `FixedTick`, evaluate cubic Bernstein position and derivative tangent, convert the double-precision result back through AUP, and rotate with guarded `Quaternion.LookRotation` from tangent/up. Cache `IDockingAutopilotService` outside the hot path and mirror active data into a `GlobalDataVault` buffer when the service exists.

Rejected Alternatives: `Vector3.Lerp`, `Quaternion.Slerp`, local nlerp, `AnimationCurve`, per-frame `GlobalRegistry.TryGet`, and local `NativeArray` ownership were rejected. The standard Unity approach is too easy to desync across origin shifts and too opaque for later Burst/job telemetry.

Scalability potential: Low/MX350: one cubic sample per fixed docking step and vault slot writes only while active. Middle: cached service buffer for AI/drone consumers. High: later current compensation and STP vectors can read the same spline state. Ultra: Hermite/zero-jerk polishing can swap progress shaping without changing P0-P3 authority.

Hardware Impact: Estimated gain on i3/MX350 is ~2-4 us per active dock and 0 B/frame. The main win is not raw math; it is removing singleton/search risk and keeping spline state blittable for jobs.

## Decision 5 - Compile Result Classification

Problem: Full `dotnet build Assembly-CSharp.csproj` and focused `dotnet build Hecton8.Core.csproj --no-restore` cannot finish green in the current shared worktree.

Solution: Fix the docking-specific compile surface by adding the new source to `Hecton8.Core.csproj`, then classify remaining errors as external dependencies: `Hecton8.VFX.Wakes` contracts missing, screen-space light shaft contracts missing, and `EcosystemDirector` not matching new `IEcosystemDirectorService` methods.

Rejected Alternatives: Reverting unrelated agent work or editing VFX/ecosystem domains was rejected as out-of-scope sabotage. Reporting build success was rejected because the compiler is objectively red.

Scalability potential: Not impacted by the external compile blockers. Docking code remains value-type and vault-backed.

Hardware Impact: 0 us/frame. This is integration evidence only.

## Decision 6 - Burst Kernel Without Scheduling Policy

Problem: Phase 2 requires a Burst cubic solver and tangent math, but the physics mandate forbids adding a same-frame `Schedule().Complete()` trap or inventing a dispatcher policy inside the docking service. The later H-Phi audit also rejected local `NativeArray<T>` declarations inside the docking automation job.

Solution: Add `CubicBezierJob : IJobParallelFor` as a pure unsafe kernel over vault-compatible pointer lanes with explicit lengths. The job calls the same Bernstein evaluator used by the scalar path and emits derivative tangent for LookRotation consumers. Scheduling remains the responsibility of a future dispatcher/autopilot system.

Rejected Alternatives: Running all spline batches on the main thread only, adding a hidden immediate-complete job path, local `NativeArray<T>` job lanes, or using Unity `AnimationCurve` were rejected. Float3 control points were rejected because AUP-scale docking would warp far from origin.

Scalability potential: Low/MX350: scalar or small batched solve at low active counts. Middle: jobified 64-slot vault lane. High: downstream current compensation can add a second vector lane. Ultra: later Hermite smoothing can write a different progress lane while the solver remains unchanged.

Hardware Impact: Estimated cost target is under 0.1 ms for the 64-slot batch on i3/MX350 and 0 B/frame. The job avoids managed dispatch decisions and only carries blittable data.

## Decision 7 - Current Compensation Is A Velocity Command, Not A Global Force

Problem: The XML requires abyssal-flow compensation, but the docking handler owns a kinematic capture sequence. Applying global current physics or forcing every entity through the fluid field would violate the flow mandate and risk frame spikes.

Solution: Cache `HectonFluidEngine` outside the hot path, sample `TrySampleModAbyssalFlow` only for the active docking vehicle, and subtract the flow vector from the path velocity command. The spline remains the authoritative AUP path; current compensation drives thruster/wake/motion-vector intent.

Rejected Alternatives: Global per-entity current force was rejected by the abyssal-current mandate. Per-frame `GlobalRegistry.Fluid` polling was rejected by the registry mandate. Letting water drift the kinematic path was rejected because docking must be predictable and controllable.

Scalability potential: Low: no flow if the cached fluid runtime is missing, and one sample only when docking is active. Middle: one previous-frame flow sample. High: same sample feeds wake advection. Ultra: future fluid owner can replace the sampler without changing docking authority.

Hardware Impact: Estimated cost is <3 us per active dock on i3/MX350 and 0 B/frame. The fake-first channel buys visible thruster/wake compensation without simulating broad fluid truth.

## Decision 8 - Math LOD Split For Toaster And Overkill

Problem: Full-rate cubic evaluation is cheap at one active dock but still violates the scalability pillar if MX350 and RTX receive the same treatment.

Solution: Math LOD 0 solves at 10 Hz and manually interpolates position between cached samples. Math LOD 2 uses a seventh-order Hermite progress curve with zero endpoint jerk before Bezier evaluation. Homeostasis stress above 0.8 disables the high-end curve and falls back to basic inertial progress.

Rejected Alternatives: Instant low-tier snap was rejected because it destroys the heavy docking feel. `AnimationCurve` was rejected by XML. A middle-ground fixed mode was rejected because HECTON-8 requires toaster and overkill paths, not one balanced path.

Scalability potential: Low/MX350: 10 Hz spline solve plus manual interpolation. Middle: fixed-tick Bezier. High: Hermite zero-jerk progress. Ultra: saved cycles can go to wake/fluid presentation instead of more path authority.

Hardware Impact: Low-tier spline solve cadence drops by about 80% versus 50 Hz. High-tier adds only scalar polynomial math and is shed under stress.

## Decision 9 - Reactive VFX Uses Existing Wake/Fluid Lanes

Problem: The XML names `VehicleWakeSignal`, but the repository already has `WakeGeneratedSignal` and `FluidImpulseSignal` consumers for procedural wake and fluid advection.

Solution: Publish `WakeGeneratedSignal` with the vehicle source flag and `FluidImpulseSignal` with bounded radius/lifetime at 10 Hz while docking. This keeps VFX domain ownership intact and avoids direct particle spawning from the physics handler.

Rejected Alternatives: Adding an orphan `VehicleWakeSignal` was rejected because no consumer exists. Spawning particles or writing fluid buffers directly was rejected as cross-domain sabotage.

Scalability potential: Low: signal cadence is already 10 Hz and consumers can discard by tier. Middle: flora wake buffer receives sparse procedural points. High/Ultra: fluid advection can use the impulse lane for richer turbulent wakes.

Hardware Impact: One bounded signal pair every 0.1 s during active docking. 0 B/frame in the docking path; visual owners pay or shed their own budgets.

## Decision 10 - Blackbox, Handoff, And Abort Are Signal-Driven

Problem: Large docking deviation or NaN cannot be explained after the fact without a 300-frame record, and moonpool/WFC handoff must not create a direct dependency from physics into construction animation code.

Solution: Extend the existing docking telemetry ring with spline deviation, target position, flow velocity, command velocity, owner hash, request id, and flags. Dump to `Dump_DOCKING_AUTOPILOT_SPLINE.bin` on invalid pose/deviation. Emit `DockingCompleteSignal` at t > 0.95 and `DockingFailedSignal` on abort.

Rejected Alternatives: Silent snapping after a >5 m deviation was rejected because it hides real faults. Direct WFC animation calls were rejected because the signal lane is already the decoupled contract.

Scalability potential: Low/Middle/High/Ultra use the same fixed telemetry footprint. Cheap devices get crash evidence without extra frame allocation; top-tier can consume the same signals for richer animation and fluid reaction.

Hardware Impact: The ring is fixed at 300 entries. Added fields increase persistent memory only; hot-path writes remain straight-line value copies with no managed allocation.

## Decision 11 - Compile Wall Is External

Problem: Focused build still fails, but errors now resolve to unrelated shared-worktree files: `EcosystemDirector`, `SubmarineFluidDynamics`, and `LockstepStateValidator`.

Solution: Run a filtered rebuild for docking file names. It returned no `VehicleDockingModule`, `DockingAutopilotService`, or docking signal errors, so task 18 is marked blocked by dependency instead of green.

Rejected Alternatives: Editing ecosystem, submarine hydrodynamics native-state handles, or lockstep constants was rejected as outside the docking prompt. Reporting success was rejected because `dotnet build` exits nonzero.

Scalability potential: No runtime impact. This is integration state only.

Hardware Impact: 0 us/frame. Compile blocker classification only.

## Decision 12 - Vault-Owned Telemetry Cursor

Problem: The blackbox telemetry ring was moved into `GlobalDataVault`, but the write cursor still lived inside `VehicleDockingModule`. Multiple docking modules could therefore overwrite a shared ring with private cursor state and break the last-300-frame evidence trail.

Solution: Add `BufferID.VehicleDockingTelemetryCursor` and store the cursor in a one-element `GlobalDataVault` int buffer owned by `SystemID.VehiclesPhysics`. `VehicleDockingModule` now resolves both the ring and cursor through `VaultBufferHandle<T>`, sanitizes the cursor without modulo on the hot write path, and only dumps to disk on abort/NaN.

Rejected Alternatives: Keeping `_dockTelemetryCursor` was rejected because it violates data sovereignty for a global ring. Per-module rings were rejected because they multiply persistent memory and fragment crash evidence. Managed collections were rejected by the zero-GC mandate.

Scalability potential: Low/MX350 and Quest keep one fixed 300-entry ring plus one int cursor. Middle/High/Ultra can consume the same stable blackbox trail without changing docking code. The VFX overkill path remains signal-driven through existing wake/fluid lanes, not physics-owned particles.

Hardware Impact: 0 B/frame. The added vault buffer is one int. Hot telemetry writes replace a private field read/write with a resolved pointer read/write; no measured profiler microseconds are available, and no runtime saving is claimed.

## Decision 13 - Multiplatform Audit Boundary

Problem: The inquisition requested ARM64 layout, Metal shader compliance, Steam Deck I/O pressure, and high-end overkill. The docking prompt owns physics/vehicles automation, not graphics shader authoring.

Solution: Validate all docking structs touched by this prompt use `Pack = 1` with fixed `Size`, confirm no docking-domain shader/compute assets exist, keep blackbox I/O abort-only, and use existing typed wake/fluid/completion/failure `SignalBus<T>` lanes for downstream visual systems.

Rejected Alternatives: Adding a duplicate `VehicleWakeSignal` was rejected because `WakeGeneratedSignal` and `FluidImpulseSignal` already exist. Adding raymarching/POM/SSS from the docking physics domain was rejected as cross-domain sabotage.

Scalability potential: Low: 10 Hz spline solve and fixed blackbox ring. Middle: regular Bezier solve with current compensation. High: zero-jerk Hermite progress when `SystemStress01 <= 0.8`. Ultra: saved physics cycles feed wake/fluid consumers through typed lanes.

Hardware Impact: 0 us/frame measured. Static evidence shows no docking-owned shader dispatch, no per-frame file I/O, no private persistent native buffers, and no managed Lerp/Slerp path in audited docking files.

## Decision 14 - Remove NativeArray Text From Docking Automation

Problem: The service had evicted persistent data to the vault, but the Burst job still declared `NativeArray<T>` fields. That was technically a scheduler lane, not storage, but the H-Phi mandate is textual and strict enough that the declaration itself was debt.

Solution: Convert `CubicBezierJob` to an unsafe pointer-lane job using `ActiveSplineData*`, `float*`, and `DockingSplineSample*` plus explicit lane lengths. The hot kernel still evaluates pure Bernstein math and writes a sample only after bounds and null guards pass.

Rejected Alternatives: Keeping `NativeArray<T>` fields was rejected because it weakens the data-sovereignty proof. Managed `ReadOnlySpan<T>` was rejected for Burst job execution. Scheduling/completing the job inside the service was rejected because the dispatcher owns job policy.

Scalability potential: Low can keep scalar service evaluation. Middle/High/Ultra can schedule the pointer-lane job from vault-resolved buffers without reintroducing local native storage.

Hardware Impact: 0 B/frame. No measured microseconds are claimed. Bounds/null checks add scalar guard work only inside scheduled batch execution.

## Decision 15 - Fail-Closed Ownership And Teardown

Problem: `TryWriteActiveSpline` trusted the caller's slot ownership after bounds checking, and shutdown used the normal resolve path that could allocate the spline vault buffer just to clear it. Idle telemetry also resolved the vault before checking whether docking was active.

Solution: Reject writes when the existing active/reserved slot owner does not match the incoming spline owner. Add an existing-buffer resolve path for shutdown so teardown never creates a new active-spline buffer. Move the idle telemetry guard before vault pointer resolution.

Rejected Alternatives: Trusting slot callers was rejected because ownership bugs become cross-vehicle path corruption. Allocating during shutdown was rejected because it pollutes memory evidence. Resolving telemetry every idle tick was rejected because the system can prove no blackbox sample is required before touching the vault.

Scalability potential: Low/Quest/Steam Deck avoid unnecessary idle pointer resolution and teardown allocation. Middle/High/Ultra keep the same behavior but get stricter slot isolation when multiple docking modules run.

Hardware Impact: No measured profiler data. Static effect: idle non-docking ticks skip telemetry vault resolution entirely, and teardown avoids a possible one-time vault buffer allocation.
