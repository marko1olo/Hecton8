# Rationale_ECOSYSTEM_FLOCKING_LEAD

STATUS: PENDING VERIFICATION

## Decision 0: Initial Constraint Set
Problem: GPU boids lack threat awareness; CPU flocking is explicitly banned.
Solution: Use existing GPU-resident boid path and feed a compact threat buffer to the compute shader. Apply flee math in HLSL using rsqrt-safe normalization and tier-capped loops.
Rejected Alternatives: CPU OverlapSphere avoidance, CPU boid iteration, duplicate boid buffers, direct concrete dependencies across domains.
Scalability potential: Low = player plus 3 predators; Middle = player plus 7 predators; High = 16 threats; Ultra = 16 threats plus acoustic shock visual overkill if supported by existing signals.
Hardware Impact: Expected win on i3/MX350 is avoiding CPU O(N) fish work and avoiding extra VRAM. Exact microseconds saved: PENDING GPU/Profiler capture.

## Decision 1: Predator AUP Buffer Ownership
Problem: Fish require player and predator threat awareness, but direct CPU flocking and cross-domain concrete dependencies are banned.
Solution: Reused EncounterDirector's 16-slot `_PredatorAUPBuffer`; slot 0 is refreshed from frame player/sub position, slots 1-15 remain apex predators. Sargassum binds the buffer through `IEncounterDirectorService`.
Rejected Alternatives: New Sargassum-owned threat buffer, direct `EncounterDirector` reference, CPU object query per fish. Standard Unity component lookup was rejected because it would allocate/branch across scene objects and violate domain boundaries.
Scalability potential: Low = 4 threat checks; Middle = 8 implied by runtime tier budget; High = 16; Ultra = 16 plus acoustic shock visual overkill.
Hardware Impact: MX350 pays a 16-float4 upload, not O(5000) CPU avoidance. Estimated gain versus CPU fish flee: 100-400 us on i3-class silicon, pending profiler capture.

## Decision 2: HLSL Scatter Math
Problem: `normalize(boid - threat)` can produce NaN under overlap and costs more than needed for panic presentation.
Solution: Added `SafeNormalizeRsqrt` and squared-radius gates. Scatter vector feeds acceleration and velocity; flee state breaks cohesion to 0.1 for visual shatter.
Rejected Alternatives: HLSL `normalize`, CPU-side flee velocity, and spatial hash rebuild for predators. Standard realism was rejected; a cinematic repulsion fake buys the intended swarm split.
Scalability potential: Low = 4-loop shader cap; Middle = 8 threats if tier policy expands; High = 16 threats; Ultra = 16 threats with stronger acoustic panic visuals.
Hardware Impact: Avoids CPU readback and limits MX350 ALU. Estimated low-tier saving: 12 threat tests per boid, roughly 60k comparisons avoided at 5000 boids.

## Decision 3: Acoustic Shock Routing
Problem: Acoustic pings need a one-frame school scatter without adding a bespoke predator dependency.
Solution: Drained typed `AcousticPingSignal` snapshots, injected a short massive threat and published `SwarmDispersedSignal` through `SignalBus`.
Rejected Alternatives: Direct predator notification, string event, long-lived physics shockwave. Standard Unity broadcast was rejected due to allocations and domain coupling.
Scalability potential: Low = capped 4 acoustic signals consumed per frame; Middle/High = same cap but stronger shader response; Ultra = larger radius/visual panic already supported by authoring.
Hardware Impact: Fixed-size signal scan and one massive-threat write. Estimated cost under 10 us on i3/MX350, pending capture.

## Decision 4: AUP Shift Handling
Problem: Headless predators stored runtime positions that could diverge after floating-origin shifts.
Solution: Predator upload reconstructs headless runtime position from `AbsoluteUniversePositionBlit` before writing `_PredatorAUPBuffer`; Sargassum boids continue using existing origin-shift listener.
Rejected Alternatives: Shader-side global offset accumulation or duplicate `AupShiftSignal` consumption in Sargassum. Duplicate consumption was rejected because it can double-apply a shift.
Scalability potential: Low/Middle/High/Ultra all use the same coordinate invariant; high tiers spend saved precision on visual density, not more CPU.
Hardware Impact: Prevents rare catastrophic flee misalignment with one AUP conversion per predator publication. Cost is bounded by 15 predator slots in normal publication, not boid count.
