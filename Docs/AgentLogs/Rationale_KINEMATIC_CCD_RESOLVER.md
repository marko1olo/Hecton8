# KINEMATIC_CCD_RESOLVER Rationale

Status: PENDING VERIFICATION

## Initial Decision

Problem: 30 m/s kinematic/manual movement can tunnel through 0.5 m collision surfaces because discrete fixed-step collision checks are insufficient.
Solution: inspect existing kinematics and physics contracts, then add a bounded CCD/deflection kernel only at high speed. This keeps low-speed movement on cheaper existing discrete checks.
Rejected Alternatives: enabling Unity built-in CCD is not sufficient for manual/kinematic MovePosition flows; adding arbitrary velocity clamps hides tunneling and damages locomotion feel.
Scalability potential: Low uses one collision bounce and stop-on-hit; Middle/High use slide deflection; Ultra can preserve more impact consequence signals and visual juice without increasing authority complexity.
Hardware Impact: expected low-end benefit is fewer physics correction spikes and fewer penetration recovery paths on i3/MX350; exact microseconds are PENDING VERIFICATION until profiler data exists.

## Mandate Binding

Problem: CCD is gameplay authority, not visual-only simulation.
Solution: physical sweep is allowed because player/vehicle/leviathan collision correctness breaks without it; consequences remain event/fake-driven where possible.
Rejected Alternatives: simulating contact stacks or per-surface physics truth; those exceed the 0.1 ms suspicion threshold without need.
Scalability potential: collision authority stays simple; presentation can scale through sparks, haptics, camera bias, and audio on higher tiers.
Hardware Impact: avoiding extra physical simulation preserves MX350 frame budget; numbers are PENDING VERIFICATION.
