# HECTON8_KINEMATICS Rationale

Status: PENDING VERIFICATION

## Assignment Binding

Problem: The active request supplies the kinematics master prompt in chat rather than a `CURRENT_BATCH.md` XML block.
Solution: Use `HECTON8_KINEMATICS` as the operative ID and keep status/rationale/log state under that ID.
Rejected Alternatives: Overwriting the existing `Status_HECTON-8.md` was rejected because that file belongs to a deterministic replay assignment.
Scalability potential: Process-only; runtime unaffected.
Hardware Impact: No runtime impact.

## Hit-Stop Gate

Problem: The 25-task audit found no centralized high-speed collision hit-stop despite the prompt requiring 0.05 timeScale for 0.1 seconds above 20 m/s.
Solution: Add a `GlobalPhysicsStateManager` kinematic hit-stop gate. Player collision processing only submits the relative speed; the manager owns timeScale capture, 0.1 unscaled-second countdown, and restore.
Rejected Alternatives: Writing `Time.timeScale` directly inside `HectonPlayerMovement` was rejected because controller-local timeScale writes conflict with pause/scene systems. Coroutine restore was rejected because gameplay coroutines allocate and violate the tick model.
Scalability potential: Low/Middle/High/Ultra identical; the effect is impact-only and has no idle cost.
Hardware Impact: Impact path adds one finite check and manager call; expected cost <1 us only when collision events are processed.

## AUP Speculative Hover Tide

Problem: AUP stale-hit discard had a one-frame speculative hover, but the height was a flat controller constant rather than the global triangle-wave tide illusion required by the prompt.
Solution: Expose `GlobalPhysicsStateManager.ResolveSpeculativeHoverHeightMeters(baseHeight, time)` using celestial tide when valid and the existing triangle wave fallback otherwise. `HectonPlayerMovement` seeds the shift-frame hover height and folds it into ground probe tolerance for the one speculative frame.
Rejected Alternatives: Running a new sine/tide simulation in the controller was rejected because it duplicates global physics state and adds trig. A full buoyancy/ground reconciliation was rejected because one stale-frame hover is visual glue, not gameplay truth.
Scalability potential: Low uses the same triangle fake; High/Ultra inherit celestial tide when present. No extra visual branch is needed.
Hardware Impact: One shift-frame scalar calculation; expected cost <0.1 us outside origin shifts.

## OMEGA Wake-Silt Threshold

Problem: `HectonPlayerMotor` recomputed the wake-silt speed threshold square inside the emission gate even though the threshold is a compile-time constant.
Solution: Add `WakeSiltEmissionSpeedThresholdMetersPerSecondSq` and compare directly against it.
Rejected Alternatives: Reworking wake-silt emission cadence was rejected because the existing cooldown and AbyssalFluidDecalManager handoff are already bounded and changing them would alter presentation behavior.
Scalability potential: Low/Middle/High/Ultra all benefit equally because this is a hot-path scalar removal with no quality tradeoff.
Hardware Impact: Saves one float multiply per wake-silt gate evaluation; estimated <0.01 us per call, PENDING PROFILER MEASUREMENT.

## OMEGA Audit Result

Problem: The polish mandate requires proof that no newly touched kinematics math regressed into honest high-cost simulation.
Solution: Targeted scans across player movement, motor, camera rig, input handler, motor native state, global physics state, and `DistanceMath` found no hot `sqrt`, unconditional `normalize`, `Slerp`, `Vector3.Distance`, runtime strings, `foreach`, or runtime randomness. The only `math.sin/cos` hits are cold LUT initialization in `HectonPlayerMovement`.
Rejected Alternatives: Broad-editing every division or `new Vector3` was rejected because value-type construction is not GC and mass edits would create regression risk without profiler evidence.
Scalability potential: Low tier uses dominant-axis/triangle approximations; High/Ultra retain rsqrt close-lane fidelity and shader Math LOD pushes.
Hardware Impact: Scan-confirmed zero new GC in targeted hot paths; measured runtime cost remains PENDING UNITY PROFILER.
