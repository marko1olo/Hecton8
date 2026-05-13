# Rationale_MAELSTROM_KINEMATICS

Status: PENDING VERIFICATION

## Decision 0 - Authority Route
Problem: The prompt requests `AnomalySpawnedSignal(Maelstrom)`, but `GlobalSignals.cs` is already dirty before this agent touched it. Editing it would risk overwriting another agent/user change.
Solution: Use existing `HectonFluidEngine` analytical flow ownership for the first implementation pass and avoid dirty global signal mutation. If a new signal lane is unavoidable, add it only after inspecting the dirty diff.
Rejected Alternatives: Adding a new `WhirlpoolManager.Instance` violates singleton eradication. Editing dirty `GlobalSignals.cs` blindly violates shared-worktree safety. Collider triggers/AreaEffectors violate deterministic physics rules.
Scalability potential: Low = one maelstrom, suction only. Middle = two maelstroms, suction plus tangent. High = richer tangent and visual warp. Ultra = stronger GPU particle swirl and post warp without extra PhysX.
Hardware Impact: On i3/MX350, replacing trigger stay/AddForce with 1-2 squared-distance samples avoids broadphase and managed callback churn; expected hot-path cost is microseconds, not tenths of a millisecond.

## Decision 1 - Visual Fake First
Problem: A physically simulated whirlpool would invite trigger volumes, per-body PhysX force application, and unpredictable solver outcomes.
Solution: Treat maelstroms as a deterministic field: kinematics sample a bounded array, VFX/audio/post process sell the phenomenon, and only event-horizon damage becomes gameplay truth.
Rejected Alternatives: Per-particle water simulation, AreaEffector, PointEffector, and OnTriggerStay all spend CPU on invisible causes instead of visible player belief.
Scalability potential: Low = mathematical suction and sparse particle swirl. Middle = tangent velocity and rumble. High = spiral UV warp and boid panic. Ultra = dense particle vortex and stronger distortion.
Hardware Impact: Low-end avoids trigger callbacks and per-rigidbody solver noise; top-tier spends saved CPU/GPU headroom on marine snow density and visor distortion.
