# Rationale_SUBMARINE_AUTOPILOT

## Decision 001 - Authority Boundary
Problem: Submarine auto-level must not become a singleton or direct input consumer.
Solution: Publish a narrow `ISubmarineState` read model through `GlobalRegistry` and drive commands through a native queued signal bus.
Rejected Alternatives: `Submarine.Instance` and direct `GlobalRegistry.Input` polling inside the vehicle controller; both couple the vehicle domain to global state/input.
Scalability potential: Low uses a master ballast scalar; Middle/High/Ultra keep per-tank visual overkill and full PID telemetry.
Hardware Impact: i3/MX350 avoids four independent tank targets and keeps the command path bounded to a small queue scan.

## Decision 002 - Auto-Level Physics
Problem: Legacy kinematic pitch steering cannot satisfy deterministic physical stabilizers.
Solution: Use a Burst `IJob` to solve world-up error against `float3(0,1,0)` and enqueue torque through `PhysicsForceRouter`.
Rejected Alternatives: `Rigidbody.AddTorque` in the controller or `MoveRotation` pitch correction; both bypass the existing physics write owner or fake rotation authority.
Scalability potential: Low clamps to master ballast and lower torque; Ultra can run per-tank mass bias with stronger visual response.
Hardware Impact: PID solve is one scheduled job with one output slot; target budget is under 0.02 ms on i3/MX350.

## Decision 003 - Ballast As Cinematic Mass
Problem: Physical water/air simulation inside ballast tanks would be too expensive and not controllable.
Solution: Treat ballast as four SOA fill scalars, compute weighted local CoM, and inject ballast mass into the existing hydrodynamic mass path.
Rejected Alternatives: Fluid particles, per-tank rigidbody children, or compartment flood reuse; all are heavier and harder to tune.
Scalability potential: Low uses a single scalar; Middle uses front/back bias; High/Ultra uses four tanks and aggressive CoM movement.
Hardware Impact: Saves allocation and transform traversal; estimate 0.03-0.08 ms saved versus component-per-tank simulation on low-end silicon.

## Decision 004 - Power And Recovery
Problem: Ballast changes need logistics coupling and impact recovery without direct power graph or fauna dependencies.
Solution: Cache `IPowerGridService` at enable-time, update it through `IGlobalRegistryHotSwapListener`, and use `CombatDamageRuntime` listener feedback for massive impact reset.
Rejected Alternatives: Direct `LogisticsNetworkGraph` reads and fauna-specific Leviathan references; both violate domain boundaries.
Scalability potential: Pump denial still leaves stable low-cost auto-level torque; high-end builds can spend saved budget on audio/visual feedback.
Hardware Impact: Removes pump-path registry polling; one cached interface call per pump request and no scene scans.

## Decision 005 - Black Box
Problem: PID windup or NaN failures must be reconstructable after crash.
Solution: Keep a fixed 300-entry `NativeArray<SubmarinePidTelemetryEntry>` ring and dump to `Docs/AgentLogs/Dump_SUBMARINE_AUTOPILOT.bin` on anomaly.
Rejected Alternatives: Debug logs or managed lists; logs miss frame history and lists allocate.
Scalability potential: Same telemetry format across tiers; high-end can add visualization later without changing controller state.
Hardware Impact: Fixed memory footprint, no hot-path GC.

## OMEGA POLISH CHANGES
Problem: PID job used `math.normalizesafe` and `math.length`, which hides square-root work behind convenience math.
Solution: Replaced both normalization paths with explicit `lengthsq * math.rsqrt` and replaced integral windup length with `lengthsq * math.rsqrt`.
Rejected Alternatives: Keeping convenience math because the vectors are short; rejected because the prompt explicitly demanded anti-bloat scrutiny.
Scalability potential: Low/MX350 path keeps one master ballast scalar and cheaper torque scale; High/Ultra still receives four-tank CoM bias.
Hardware Impact: Avoids hidden sqrt in the Burst PID hot path; expected gain is small but deterministic, under 1 us per fixed solve on i3/MX350.

Problem: Full build verification is blocked outside this domain.
Solution: Added new source files to `Hecton8.Core.csproj`, reran `Hecton8.Core.csproj` compile with project references disabled, and audited returned errors.
Rejected Alternatives: Reporting green compile or editing Bootstrap/Cartography/Narrative systems outside assigned vehicle domain.
Scalability potential: No runtime impact; compile wall documented for integrator.
Hardware Impact: None.

Problem: Follow-up scan found a mounted command bridge lookup in the tick path and immediate Low/MX350 math mode switching.
Solution: Moved `SubmarineAutoLevelBallastController` discovery/installation into cold drive-reference resolution, cached power grid service with hot-swap updates, and added a 2.5 second math LOD hysteresis gate.
Rejected Alternatives: Retrying `TryGetComponent` from `Tick`, polling `GlobalRegistry.PowerGrid` in pump work, or switching four-tank/master-ballast mode immediately on every quality flag change.
Scalability potential: Low/MX350 keeps stable master-ballast math after the hold window; High/Ultra keeps per-tank ballast bias and stronger visual center-of-mass response without mode flicker.
Hardware Impact: Removes one mounted hot-path component lookup and one pump-path registry read; expected gain is small but deterministic, about 1-3 us on i3/MX350 during active piloting.

Problem: A zero body entity id could turn a mounted vehicle command into a broadcast, and the listener fallback used runtime object id lookup inside command dispatch.
Solution: Cache the transport command target id during cold reference resolution, fall back to the cached GameObject entity id when body entity id is zero, reject zero-id commands at bus ingress and listener dispatch, and compare only cached ids in `OnVehicleCommandSignal`.
Rejected Alternatives: Allowing target id zero to mean "current submarine" or recomputing Unity object ids during every command dispatch.
Scalability potential: Multi-sub scenes remain bounded to one command target; Low through Ultra use the same primitive-id path.
Hardware Impact: Removes Unity id lookup from command dispatch and prevents cross-submarine command bleed; expected CPU gain under 1 us, correctness gain is material.

Problem: Disabling the auto-level controller could leave the last ballast mass inside the hydrodynamic cargo mass path.
Solution: On unregister, push zero ballast mass into `SubmarineFluidDynamics`, clear cached ballast mass, and clear the cached power-grid service reference.
Rejected Alternatives: Depending on scene teardown to destroy `SubmarineFluidDynamics` or waiting for the next fixed tick to overwrite stale mass.
Scalability potential: All tiers get deterministic teardown; High/Ultra visual CoM bias cannot survive after owner disable.
Hardware Impact: Cold lifecycle write only; no frame cost.

Problem: Multiple enabled submarine controllers could collide on the singleton `GlobalRegistry.SubmarineState` read-model slot.
Solution: Register the read model only when the slot is empty or already owned by this controller; secondary controllers keep command-bus behavior without hijacking the active HUD/read-model owner and use hot-swap notification to claim the slot if it becomes empty.
Rejected Alternatives: Allowing `RegisterService` slot hijack errors or replacing the single HUD-facing read model with a multi-read registry outside this task boundary.
Scalability potential: Multi-sub scenes can run per-target controllers while one active read model remains authoritative for HUD consumers.
Hardware Impact: Cold lifecycle branch only; no fixed-step cost.

Problem: Dynamic prefab composition could initialize mounted transport before `SubmarineCoreDirector`, causing the auto-level command bridge to cache a permanent miss.
Solution: Only mark the bridge resolved after a controller is found or installed, and leave missing-core cases eligible for the next cold reference-resolution pass.
Rejected Alternatives: Retrying discovery in the mounted tick path or accepting a silent kinematic fallback after late composition.
Scalability potential: Low through Ultra prefabs can compose vehicle modules in different orders without adding runtime discovery cost.
Hardware Impact: Cold lifecycle retry only; zero fixed-step cost.

Problem: Serialized tuning fields had inspector attributes but incomplete editor validation.
Solution: Clamp PID gains, combat thresholds, vent audio threshold, and LOD hold time in `OnValidate`.
Rejected Alternatives: Trusting inspector attributes only; runtime scripts and prefab merges can bypass visual inspector constraints.
Scalability potential: Keeps Low/MX350 LOD hold values bounded while allowing High/Ultra stronger PID tuning inside nonnegative limits.
Hardware Impact: Editor/cold validation only; no player-frame cost.

Problem: Math LOD selection still read `GlobalRegistry.ScalabilityTier` and `GlobalRegistry.MathPrecision` from the fixed-step helper chain.
Solution: Cache `_desiredLowMathLod` during cold setup and refresh it from `ScalabilityEvents`; `AdvanceMathLod` now uses only the cached boolean plus hysteresis.
Rejected Alternatives: Per-fixed registry reads or hiding the lookup behind a helper named `Resolve*`.
Scalability potential: Low/MX350 can degrade ballast math without polling; High/Ultra stays on four-tank bias until an explicit scalability event changes the cached profile.
Hardware Impact: Removes two registry property reads from each submarine fixed step; expected gain under 1 us on i3/MX350, but the main gain is architectural compliance.

Problem: `FrameTimeWatchdog` can degrade math precision without publishing a `ScalabilityEvents` payload.
Solution: Add an explicitly budgeted `ISlowTickable` snapshot refresh that compares cached scalability tier and math precision, then updates `_desiredLowMathLod` only on change.
Rejected Alternatives: Restoring fixed-step registry reads, or adding a new global math-precision event lane without owning the full platform-integration contract.
Scalability potential: Low/MX350 and watchdog-degraded machines fall back to master ballast within the slow cadence; High/Ultra stays four-tank unless the cached precision/tier changes.
Hardware Impact: Removes the fixed-step cost while adding two registry reads only on slow tick; expected cost under 1 us per slow cadence.

Problem: Air-release vent throttling and telemetry frame stamping used Unity wall-clock/frame globals in the fixed-step controller path.
Solution: Replace the vent throttle with `_airReleaseCooldownSeconds` advanced by the dispatcher-provided fixed delta and stamp telemetry with `_tickCount`.
Rejected Alternatives: Continuing to read `Time.time`/`Time.frameCount` because they are cheap.
Scalability potential: All tiers get deterministic cooldown behavior tied to physics cadence; High/Ultra can layer richer audio without changing command physics.
Hardware Impact: No measurable CPU gain; removes hidden global time dependency from the control loop.

Problem: Mounted sweep-impact feedback used `Time.time` while executing in the fixed vehicle kinematics path.
Solution: Replace `_nextMountedImpactFeedbackTime` with `_mountedImpactFeedbackCooldownSeconds` and advance it from the dispatcher-provided fixed delta.
Rejected Alternatives: Leaving wall-clock time inside physics cadence because the impact feedback is visual/audio adjacent.
Scalability potential: Low/MX350 avoids non-deterministic haptic/audio spam under hitchy frames; High/Ultra can add richer impact feedback without changing cadence ownership.
Hardware Impact: No measurable CPU gain; deterministic cooldown state and no engine time dependency.

Problem: A thrown command listener could leave `VehicleCommandSignalBus` permanently marked as dispatching.
Solution: Wrap dispatch with `try/finally` and clear `_isDispatching` before promoting reentrant commands.
Rejected Alternatives: Assuming listeners never fail; one fault would corrupt command scheduling for all active submarines.
Scalability potential: Multi-sub scenes keep the shared command lane recoverable under listener faults.
Hardware Impact: No allocation; normal-path branch cost is negligible versus correctness.

Final Git Diff Summary:
- Added `SubmarineAutoLevelBallastController.cs` and `VehicleCommandSignals.cs`.
- Extended `ISubmarineRuntimeContext.cs`, `GlobalRegistry.cs`, and `GlobalRegistryContracts.cs` for `ISubmarineState`.
- Updated `MountablePlayerTransport.cs` to publish vehicle commands and suppress kinematic pitch when PID ballast is active.
- Updated `SubmarineCoreDirector.cs` to install the controller at runtime if missing.
- Updated `SubmarineFluidDynamics.cs` to include ballast water mass in hydrodynamic cargo mass/draft.
- Added `AirRelease` procedural audio routing via existing ping renderer.
- Added task/rationale status files for SUBMARINE_AUTOPILOT.
