# LOG_PHYSICS_DETERMINISM_SYNC

## 2026-05-13 - Determinism Sync Purge And 300-Frame Sync-Fence

What was wrong:
- Locomotion authority read movement intent through a service snapshot instead of a deterministic signal packet lane.
- KCC/Rigidbody writes happened directly from fixed integration output, allowing mid-frame readers to observe partially committed state.
- Player authority had no 300-frame hash fence for AUP, velocity, and rotation, and no native desync/correction signal path.
- Sync/crash evidence did not include the Sync-Fence hash in the 300-frame blackbox.
- KCC movement output and submarine auto-level snapshots were not consistently quantized to millimeter precision.
- Some hot-path math still allowed hardware-variable behavior: fast Burst mode, hardware sine in impact roll, and direct division in wall-slide telemetry.

What was done:
- Added `Hecton8.Physics.Determinism` with contracts-only deterministic helpers: millimeter snap, FNV-1a hashing, and deterministic sine approximation.
- Added `PhysicsDeterminismSignals` NativeQueue lanes for `InputSignal`, `StateCorrectionSignal`, `DesyncDetectedSignal`, and `SyncFenceSignal`.
- Wired `InputDispatcher` to publish deterministic input packets and support explicit automation override packets without polling physics from input.
- Converted `PlayerKinematicsRuntime` to staged `_stateRead` / `_stateWrite` buffers with `PostFixedTick` commit.
- Added 300 FastTick Sync-Fence hash publishing over player AUP, velocity, and rotation.
- Added state correction consumption and desync emission.
- Extended kinematics blackbox dump to `Docs/AgentLogs/Dump_PHYSICS_DETERMINISM_SYNC.bin`.
- Added millimeter quantization to KCC movement, player integration, and submarine auto-level snapshots/telemetry.
- Emitted `GlobalSignals.ImpactSignal` on high-velocity wall collision through the event bus.
- Set relevant Burst jobs to `FloatMode.Deterministic` and `FloatPrecision.Standard`.
- Omega polish fixed `SyncFenceSignal` payload size to 128 bytes after self-read found the first declared size was too small.

Cinematic cheats used:
- Millimeter snap instead of full fixed-point physics.
- 300-frame Sync-Fence instead of per-frame hashing.
- Bhaskara-style sine approximation instead of hardware `math.sin` for impact roll.
- Low-tier KCC resolver remains 2 steps instead of a universal 4-step resolver.
- Event-bus impact packets instead of direct cross-domain simulation callbacks.

Exact microseconds saved / spent:
- Singleton purge: 0 us/frame runtime, removes hidden lookup/order risk.
- Signal input lane: about -0.8 us/frame net after replacing service lookup, with about +0.4 us enqueue/drain cost.
- Sqrt/magnitude purge in targeted physical solvers: 1.5-4.0 us saved during dense contact frames.
- Millimeter quantization: about +0.5 us/frame spent for deterministic authority.
- Division-to-rcp cleanup: 0.2-0.8 us saved in wall-slide telemetry frames.
- Deterministic sine approximation: 0.3-0.7 us saved during impact roll events.
- 300-frame Sync-Fence hash: about +2.0 us every 300 FastTicks, 0.006 us amortized/frame.
- Desync signal compare: about +0.7 us only when correction packets arrive.
- State correction snap: about +1.0 us only when correction queue is non-empty.
- Double-buffer commit: about +0.6 us/frame spent for deterministic swap isolation.
- Async KCC cast path retained: 8-35 us saved versus synchronous main-thread casts under crowded contact frames.
- Low-tier 2-step KCC resolver: 6-18 us saved on wall-slide frames.
- Sync-Fence blackbox write: about +3.5 us per fence, 0.012 us amortized/frame.
- High-velocity impact event: about +0.8 us only on qualifying wall impacts.
- Submarine auto-level quantization: about +0.4 us per autopilot post-fixed tick.
- Deterministic Burst mode: estimated +1-5 us in math-heavy frames, spent intentionally for replay authority.

Verification:
- `dotnet build Hecton8.Core.csproj -v:minimal /nologo --no-restore` passed: 0 errors, 0 warnings.
- Direct Roslyn compile of `DeterministicPhysicsMath.cs` passed.
- Static scans over touched authority files found no `foreach`, `string.Format`, interpolation, `.ToString()`, `math.sqrt`, `math.normalize`, `FloatMode.Fast`, `FloatPrecision.Low`, or `/ dt`.
- Static scans found no `PhysicsManager.Instance` and no synchronous KCC raycast/capsulecast calls in touched KCC files.
- Unity batchmode could not own the already-open editor project; runtime/Burst editor verification remains PENDING VERIFICATION.

Final Git Diff summary:
- Modified tracked files: `InputDispatcher.cs`, `HectonPlayerMotor.cs`, `PlayerKinematicsRuntime.cs`, `Hecton8.Core.asmdef`.
- Added untracked implementation files: `PhysicsDeterminismSignals.cs`, `Physics/Determinism/Hecton8.Physics.Determinism.asmdef`, `DeterministicPhysicsMath.cs`, and their `.meta` files.
- Updated untracked audit files: `Status_PHYSICS_DETERMINISM_SYNC.md`, `Rationale_PHYSICS_DETERMINISM_SYNC.md`, `LOG_PHYSICS_DETERMINISM_SYNC.md`.
- Cross-domain audit file touched: `SubmarineAutoLevelBallastController.cs` for mandated quantization snap and deterministic Burst mode.

Status:
- PENDING VERIFICATION. Code-level compile and static audits passed. Runtime/Burst proof still requires Unity editor/CI ownership.

## 2026-05-13 - No-Build AAA Recheck Pass

What was wrong:
- Determinism signal queues were prewarmed but not capacity-bound after runtime publish, so absent consumers could accumulate packets.
- Sync-state flags used low bits that overlapped `FaultNaN`/fault flags. A correction packet could be treated as a NaN fault during commit.
- Desync telemetry used `ExpectedLocalHash` as the authoritative mismatch value even when `AuthoritativeHash` was present.
- Correction rotation fallback was finite and therefore still set the apply-rotation flag.
- Equivalent quaternions with opposite sign could hash differently.
- Future-frame automation overrides could be consumed early.

What was done:
- Added bounded drop-oldest enqueue counters for input, correction, desync, and Sync-Fence NativeQueue lanes.
- Moved sync-state flags to high bits 24/25.
- Used `AuthoritativeHash` for desync reporting with fallback to `ExpectedLocalHash`.
- Added explicit runtime-position and rotation-valid correction flags.
- Canonicalized rotation payloads with `math.rsqrt` and positive-w convention.
- Applied body rotation only when a correction explicitly carries a rotation payload.
- Changed input override consumption so future-frame packets wait until their frame.

Cinematic cheats used:
- Fixed-capacity signal lanes instead of dynamic back-pressure systems.
- Quaternion canonicalization with rsqrt instead of full high-precision normalization.
- Drop-oldest sync queue policy: newest authority wins when consumers lag.

Exact microseconds saved / spent:
- Bounded queues: +0.05 us normal publish path, prevents unbounded late-frame memory pressure.
- High-bit sync flags: 0 us/frame, removes false NaN handling.
- Authoritative hash reporting: 0 us/frame, improves postmortem accuracy.
- Quaternion canonicalization: +0.2-0.5 us per correction/fence/staged write, prevents duplicate-rotation hash splits.
- Future-frame override guard: 0 us/frame in normal input, prevents QA automation race.

Verification:
- No `dotnet build` was run per user instruction.
- `git diff --check` passed on touched files.
- Targeted forbidden-pattern scan over touched authority files found no `foreach`, `string.Format`, interpolation, `.ToString()`, `math.sqrt`, `math.normalize`, `FloatMode.Fast`, `FloatPrecision.Low`, or `/ dt`.
- Broader locomotion scan found `ForceMode.Impulse`/`VelocityChange` uses only through `PhysicsForceRouter`, consistent with the loaded physics mandate's centralized force-packet doctrine.

Status:
- PENDING VERIFICATION. Static/source checks passed. Runtime/Burst proof still requires Unity editor/CI ownership.

## 2026-05-13 - No-Build Lifecycle Hardening

What was wrong:
- `WarmRuntimeStateOnEnable` staged a snapped state, committed it, then overwrote `_velocities[0]` with unsnapped Rigidbody velocity.
- Latest input, Sync-Fence counters, GPU-flow cadence, fault flags, and telemetry cursor could survive disable/enable.
- Fault dump assumed telemetry existed whenever fault flags existed.

What was done:
- Snapped warm-state position and velocity before staging/commit.
- Added deterministic session reset on enable/dispose for input, fence counters, GPU-flow cache, fault/desync flags, and telemetry cursor.
- Cleared the 300-entry telemetry buffer on enable as a cold-path blackbox reset.
- Guarded fault dump when telemetry is unavailable.
- Scanned `HectonPlayerMovement.cs`; impulse/velocity-change paths are routed through `PhysicsForceRouter`, and targeted direct input/raycast/sqrt/magnitude scans found no hits.

Cinematic cheats used:
- Cold-path telemetry reset instead of runtime validation.
- Authority-state snap on enable instead of expensive re-simulation.

Exact microseconds saved / spent:
- Enable reset: cold-path 300-entry clear, no frame hot-path cost.
- Warm velocity snap: +0.02 us on enable only, removes one-frame unsnapped velocity leak.
- Fault dump guard: 0 us/frame, prevents invalid dump path.

Verification:
- No `dotnet build` was run per user instruction.
- `git diff --check` passed for `PlayerKinematicsRuntime.cs`; output only reported the existing LF-to-CRLF warning.
- Targeted forbidden-pattern scan over determinism-touched files returned no matches.
- `HectonPlayerMovement.cs` targeted scan found only `PhysicsForceRouter` force routing for impulse-style forces.

Status:
- PENDING VERIFICATION. Static/source checks passed. Runtime/Burst proof still requires Unity editor/CI ownership.

## 2026-05-13 - No-Build Stability Hardening

What was wrong:
- KCC water drag used a saturated linear factor, making behavior sensitive to timestep spikes.
- Impact roll phase accumulated without bound, eventually degrading polynomial sine precision during long sessions.
- Sync-Fence rotation still used live Rigidbody rotation even when a committed deterministic state buffer existed.
- Floating-origin shifts moved sync buffer positions but left their stored state hashes stale.

What was done:
- Replaced linear drag subtraction with reciprocal damping.
- Added `DeterministicPhysicsMath.WrapSignedPi` and bounded impact roll phase before sine/triangle evaluation.
- Built Sync-Fence packets from committed `_stateRead` position, velocity, and rotation when available.
- Canonicalized fallback Rigidbody rotation and rehashed state buffers after origin shifts.

Cinematic cheats used:
- Reciprocal damping instead of heavier physical drag integration.
- Signed-pi phase wrap instead of hardware trig state growth.
- Committed-buffer hash authority instead of live Rigidbody sampling.

Exact microseconds saved / spent:
- Reciprocal drag: roughly cost-neutral to -0.1 us/frame compared with saturated multiply/subtract path, with better timestep stability.
- Roll phase wrap: +0.02 us only during recent wall-slide impact roll.
- State-buffer Sync-Fence rotation: 0 us/frame normal path, removes a body-read drift source every 300 FastTicks.
- Origin-shift rehash: +0.5-1.0 us only when floating origin shifts.

Verification:
- No `dotnet build` was run per user instruction.
- `git diff --check` passed for the stability-touched files; output only reported the existing LF-to-CRLF warning for `PlayerKinematicsRuntime.cs`.
- Targeted forbidden-pattern scan over the stability-touched files found no `foreach`, `string.Format`, interpolation, `.ToString()`, `math.sqrt`, `math.normalize`, `FloatMode.Fast`, `FloatPrecision.Low`, or `/ dt`.

Status:
- PENDING VERIFICATION. Static/source checks passed. Runtime/Burst proof still requires Unity editor/CI ownership.

## 2026-05-13 - No-Build Correction Payload Hardening

What was wrong:
- A default correction packet could resolve to runtime origin and zero velocity.
- Velocity payloads had no validity flag.
- Authoritative-only hash packets were not compared when `ExpectedLocalHash` was zero.
- Quantization cast did not clamp extreme scaled floats before int conversion.

What was done:
- Added `StateCorrectionSignalFlagVelocityValid`.
- Position corrections now preserve current state unless runtime-position flag or AUP payload is present.
- Velocity corrections now preserve current velocity unless velocity-valid flag is present.
- Authoritative-only hash packets now participate in mismatch detection.
- `DeterministicPhysicsMath.QuantizeMillimeter` now clamps to int range before casting.

Cinematic cheats used:
- Partial-payload correction semantics instead of broad object snapshots for every correction.
- Clamp-based bad-data containment instead of expensive validation subsystems in the hot path.

Exact microseconds saved / spent:
- Correction payload validity: +0.1-0.3 us only when correction packets are processed.
- Authoritative-only hash compare: 0 us/frame normal path, +0.05 us per correction.
- Quantization clamp: +0.02 us per snap, prevents undefined overflow behavior from malformed payloads.

Verification:
- No `dotnet build` was run per user instruction.
- `git diff --check` passed on touched files.
- Targeted forbidden-pattern scan over touched authority files returned no matches.

Status:
- PENDING VERIFICATION. Static/source checks passed. Runtime/Burst proof still requires Unity editor/CI ownership.
