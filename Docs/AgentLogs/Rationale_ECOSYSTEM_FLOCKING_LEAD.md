# Rationale_ECOSYSTEM_FLOCKING_LEAD

STATUS: PENDING VERIFICATION

## Decision 0: Initial Constraint Set
Problem: GPU boids lack threat awareness; CPU flocking is explicitly banned.
Solution: Use existing GPU-resident boid path and feed a compact threat buffer to the compute shader. Apply flee math in HLSL using rsqrt-safe normalization and tier-capped loops.
Rejected Alternatives: CPU OverlapSphere avoidance, CPU boid iteration, duplicate boid buffers, direct concrete dependencies across domains.
Scalability potential: Low = player plus 3 predators; Middle = player plus 7 predators; High = 16 threats; Ultra = 16 threats plus acoustic shock visual overkill if supported by existing signals.
Hardware Impact: Expected win on i3/MX350 is avoiding CPU O(N) fish work and avoiding extra VRAM. Exact microseconds saved: PENDING GPU/Profiler capture.

