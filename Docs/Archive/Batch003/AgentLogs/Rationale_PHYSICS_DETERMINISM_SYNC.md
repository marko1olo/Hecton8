# Rationale_PHYSICS_DETERMINISM_SYNC

Status: PENDING VERIFICATION

## Session Start

Problem: Floating-point drift in locomotion/physics authority can desync replay or future lockstep clients.
Solution: Use Sync-Fence hash, millimeter quantization, deterministic buffers, NativeQueue-style signal contracts, and blackbox telemetry.
Rejected Alternatives: Full fixed-point physics is too expensive for i3/MX350 and violates the prompt. Direct Unity Rigidbody callback authority is not deterministic.
Scalability potential: Low uses hash-only fence and reduced resolver iterations. Middle uses deterministic force packet accumulation. High adds richer contact sampling. Ultra adds presentation-only secondary motion after authority completes.
Hardware Impact: Expected low-end gain is from avoiding synchronous queries, sqrt, and unmanaged callback ordering. Microsecond estimates remain PENDING VERIFICATION until profiler/runtime evidence exists.

## Mandate Selection

Problem: Task touches physics determinism, AUP, sync, telemetry, registry, and zero-GC hot paths.
Solution: Loaded 8 mandates: physics integrity, multithreaded body determinism, AUP precision, rsqrt, sync/reconciliation, postmortem telemetry, global registry, zero-GC.
Rejected Alternatives: Reading unrelated AI/render/UX mandates would pollute scope and slow execution.
Scalability potential: The selected mandates include Low/Middle/High/Ultra policy anchors.
Hardware Impact: Focus remains on i3/MX350 hot-path cost and deterministic replay safety.

## Input Signal Migration

Problem: `PlayerKinematicsRuntime` consumed input through direct service reads, allowing fixed-tick ownership to drift from capture frame ordering.
Solution: Added `PhysicsDeterminismSignals.InputSignal` NativeQueue lane; `InputDispatcher` publishes captured state, and KCC drains latest queued input inside the simulation lane.
Rejected Alternatives: Polling `Input.GetAxis()` or `GlobalRegistry.Input.GetState()` from KCC. Both are standard Unity-style shortcuts and both hide frame ordering.
Scalability potential: Low drains latest only. Middle can drain bounded packets. High/Ultra can preserve replay sequence numbers without changing KCC authority.
Hardware Impact: Expected low-end impact is roughly -0.8 us/frame net after removing service lookup churn; queue enqueue/dequeue adds less than 0.5 us/frame.

## Determinism Assembly

Problem: Deterministic math helpers placed in Core would pull unrelated dependencies into Burst hot paths.
Solution: Added `Hecton8.Physics.Determinism` with primitive-only helpers, contracts-only reference, and `noEngineReferences=true`.
Rejected Alternatives: UnityEngine-dependent helper library or local duplicated snap/hash functions in each component. Both increase drift risk.
Scalability potential: Low only uses millimeter snap/hash. High/Ultra can add LUTs or stronger approximations behind the same isolated API.
Hardware Impact: No runtime overhead from assembly layout. Direct helper calls inline; deterministic sine avoids hardware transcendental cost during impact roll.

## Quantization And Sync-Fence

Problem: KCC/Rigidbody state can diverge by sub-millimeter deltas that compound over long sessions.
Solution: Snap position/velocity after integration, at staged state writes, and before motor `MovePosition`; publish FNV-1a Sync-Fence every 300 FastTicks.
Rejected Alternatives: Full fixed-point state or per-frame network hash. Full fixed-point is too slow; per-frame hash wastes frame budget.
Scalability potential: Low/Middle run 300-frame authority fences. High can add richer telemetry comparison. Ultra can spend saved cycles on non-authoritative visual overkill after the deterministic state commits.
Hardware Impact: Snap costs about 0.5 us/frame; fence hash costs about 2 us every 300 fast ticks. This is cheaper than correcting visible replay divergence.

## Double Buffer And Post Simulation Swap

Problem: Writing Rigidbody state directly from fixed integration lets other systems observe a partially updated authority state.
Solution: Added `_stateRead` and `_stateWrite`; fixed tick stages writes, PostFixed tick drains corrections and commits state once.
Rejected Alternatives: Direct `Rigidbody.MovePosition` in fixed job output. Standard Unity flow is fast but not auditable under many agents/systems.
Scalability potential: Low uses one entity buffer. High/Ultra can extend to multiple player-authority bodies with the same swap window.
Hardware Impact: Two 64-byte NativeArray slots and one copy per commit; roughly 0.6 us/frame on target CPU.

## Blackbox And Crash Evidence

Problem: Desync without pre-crash/fence history is not debuggable.
Solution: Store last 300 telemetry frames with Sync-Fence hash and dump to `Docs/AgentLogs/Dump_PHYSICS_DETERMINISM_SYNC.bin` on NaN/teleport/desync fault.
Rejected Alternatives: Managed text logs or "latest state only" dumps. Both lose useful forensic history and can allocate.
Scalability potential: Low keeps fixed-size binary. High/Ultra can add optional richer presentation-side diagnostics without changing authority buffer.
Hardware Impact: Fixed 300-entry NativeArray already exists in cold allocation path; fence telemetry adds about 3.5 us every 300 ticks.

## Event Bus Impact

Problem: High-speed KCC wall impacts need to notify damage/audio without coupling locomotion to those domains.
Solution: Emit `GlobalSignals.ImpactSignal` from wall-slide contact when blocked speed exceeds 4 m/s.
Rejected Alternatives: Direct damage method call or audio callback in KCC. That creates cross-domain dependencies and callback order drift.
Scalability potential: Low only publishes impact packet. Middle can route damage. High/Ultra can add presentation impact layers from the same event.
Hardware Impact: No cost in normal motion; about 0.8 us only on high-speed wall impacts.

## Submarine Autopilot Audit

Problem: Vehicle auto-level telemetry and snapshots can become a second authority drift source.
Solution: Snap submarine runtime position and linear velocity in auto-level snapshot/telemetry and mark PID Burst job deterministic.
Rejected Alternatives: Leave vehicle path unsnapped because it is cross-domain. Prompt explicitly required audit, and the file was already in active worktree.
Scalability potential: Low uses snapped ballast authority. High/Ultra can layer visual-only hull sway after snapped physics.
Hardware Impact: Roughly +0.4 us per autopilot tick; acceptable for consistency between player and vehicle authority.

## Compile Verification

Problem: Unity batchmode could not compile because an existing editor process owns the project; first generated project passes exposed stale source/reference metadata.
Solution: Repaired local generated project metadata enough for a C# compile and ran `dotnet build Hecton8.Core.csproj --no-restore`, which passed with 0 errors and 0 warnings. Direct compiled the contracts-only math helper.
Rejected Alternatives: Killing the user's open Unity editor or reverting other agents' files. Both violate collaboration constraints.
Scalability potential: Compile proof is code-level only. Runtime/Burst proof still requires the owner editor or CI.
Hardware Impact: No runtime impact. Verification status remains PENDING VERIFICATION until Unity/Burst runtime evidence exists.

## Omega Polish Changes

Problem: Final anti-bloat audit required proof that the implementation did not introduce honest simulation, GC churn, or hardware-variable math in the authority path.
Solution: Re-ran scans over touched authority files for `foreach`, `string.Format`, interpolation, `.ToString()`, `math.sqrt`, `math.normalize`, `/ dt`, `FloatMode.Fast`, and `FloatPrecision.Low`; no matches. Fixed `SyncFenceSignal` struct size to 128 bytes after self-read found the declared payload size was too small.
Rejected Alternatives: Expanding into unrelated presentation files or third-party packages. That would be churn, not polish.
Scalability potential: Cinematic cheats used are millimeter quantization instead of fixed-point, 300-frame fence instead of per-frame hash, Bhaskara-style sine approximation instead of hardware `sin`, 2-step low-tier KCC resolver, and event-bus impact routing instead of direct simulation callbacks.
Hardware Impact: Low/i3-MX350 path keeps authority cheap: no synchronous KCC raycasts, no sqrt/normalize in touched authority files, no managed formatting, and no per-frame hash. High/Ultra can spend recovered budget on presentation-only visual overkill after the deterministic state commits.

## No-Build Recheck Pass

Problem: Source review after the initial implementation found three authority risks: NativeQueue signal lanes could grow if consumers were absent, sync-state flags overlapped low fault bits, and correction packets could report the expected hash instead of the authoritative hash. Rotation payloads also needed stable sign/length canonicalization before hashing.
Solution: Added bounded enqueue/drop-oldest counters to all determinism signal queues; moved sync-state flags to bits 24/25; routed desync telemetry through `AuthoritativeHash` with fallback to `ExpectedLocalHash`; canonicalized quaternions with `math.rsqrt` and positive-w convention; required explicit correction flags for runtime position and rotation payloads; prevented future-frame automation overrides from consuming early.
Rejected Alternatives: Leaving queues unbounded because consumers are "expected", or using `math.normalize`/hardware rotation behavior. Both are poor authority engineering.
Scalability potential: Low tier now has fixed queue memory pressure even without consumers. High/Ultra can add more sync consumers without risking signal buildup or equivalent-rotation hash splits.
Hardware Impact: Queue bounding adds one branch and optional oldest-drop per publish when full; normal path stays O(1). Quaternion canonicalization costs one `rsqrt` only on staged state writes/corrections/fences, not every render frame.

## No-Build Hardening Pass

Problem: A default or partially populated `StateCorrectionSignal` could preserve no explicit position/velocity validity and still be interpreted as origin/zero velocity. Authoritative-only hash packets also needed comparison, not just expected-local hash packets. Quantization could cast out-of-range scaled floats to int.
Solution: Added `StateCorrectionSignalFlagVelocityValid`; correction position and velocity now preserve current authoritative state unless their payload is explicitly valid or an AUP payload is present; authoritative-only hashes are compared; millimeter quantization clamps before int conversion.
Rejected Alternatives: Treating default zero payloads as valid authority. Origin is a real coordinate, so valid origin snaps now require the runtime-position flag or a nonzero AUP payload.
Scalability potential: Low tier avoids catastrophic origin snaps from malformed packets. High/Ultra can send partial correction packets for velocity-only or rotation-only reconciliation without forcing unwanted transforms.
Hardware Impact: Validity checks add a few branch operations only on correction packets. Quantization clamp adds two comparisons per snap and prevents overflow behavior under bad data.

## No-Build Stability Pass

Problem: Source review found four remaining deterministic stability risks: linear drag clamped by timestep, unbounded impact-roll phase precision loss, Sync-Fence rotation sourced from live Rigidbody instead of committed state, and stale state hashes after floating-origin shifts.
Solution: Changed body drag to reciprocal damping, exposed deterministic signed-pi wrapping and bounded roll phase, used committed `_stateRead` rotation/position/velocity for Sync-Fence packets, canonicalized fallback body rotation, and rehashed sync buffers after origin shifts.
Rejected Alternatives: Hardware `math.sin`, `math.normalize`, live Rigidbody hash authority, or per-frame origin recomputation. Those approaches are easier but less auditable under replay/sync.
Scalability potential: Low keeps cheap reciprocal damping and triangle roll. Middle/High/Ultra keep stable authority hashes while spending visual budget on presentation-only roll and impact layers.
Hardware Impact: Reciprocal drag costs one `rcp` and removes clamp discontinuity. Roll wrap adds one helper call only while wall impact roll is active. State-buffer fence hashing is cost-neutral and reduces cross-system drift risk.

## No-Build Lifecycle Pass

Problem: Re-enabling `PlayerKinematicsRuntime` could stage a snapped state and then overwrite `_velocities[0]` with unsnapped Rigidbody velocity. Session-local counters, latest input, GPU-flow cadence, telemetry cursor, and desync dump flags could also survive component disable/enable.
Solution: Warm-state enable now snaps position and velocity before staging/committing, and deterministic session state is explicitly reset on enable and dispose. Fault dump now exits if telemetry is unavailable. `HectonPlayerMovement.cs` was scanned for direct input/physics violations; impulse paths found were already routed through `PhysicsForceRouter`.
Rejected Alternatives: Letting the next fixed tick clean up stale state or preserving prior telemetry across component lifecycle. That leaves one-frame authority drift and bad postmortem evidence.
Scalability potential: Low tier avoids stale advection/input after reactivation. High/Ultra can resume presentation effects independently after authority state is reset.
Hardware Impact: Cold-path reset costs one 300-entry telemetry clear on enable. Hot path is unchanged. The payoff is removing a one-frame unsnapped velocity leak and stale Sync-Fence cadence.
