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

## OMEGA POLISH CHANGES
Problem: The first scatter pass still had two honest calculations: a C# square-root for movement-noise radius and an HLSL divide for predator radius falloff.
Solution: Replaced movement radius with a velocity-squared gate (`VelocitySq * 1/144`) and replaced HLSL `distSq / radiusSq` with `distSq * rcp(radiusSq)`. Scatter normalization already uses `rsqrt`.
Rejected Alternatives: Exact player movement speed and exact division ratio. They do not improve the fish shatter read and spend cycles better used on visual density.
Scalability potential: Low = squared-speed gate plus 4 threat loops; Middle = same gate with higher population; High = full 16 threat loop; Ultra = full loop plus acoustic shock visuals and fluid drift retained.
Hardware Impact: Removes one CPU `sqrt` per consumed movement signal and one GPU divide per active predator-threat check. Estimated MX350 gain is small per event (<5 us CPU, shader ALU saved at scale), but it removes the avoidable slow-path.
Final Git Diff: Boid scatter implementation paths are present in repository HEAD. Current working diff in shared files includes unrelated biomass/inventory changes from other agents; this agent's remaining diff is status/rationale/log reporting. Unrelated dirty work was not reverted.

## Decision 5: Predator Buffer Binding Fallback
Problem: Unity compute dispatch requires declared structured buffers to be bound before dispatch; `_PredatorAUPBuffer` could be absent during Sargassum startup if `EncounterDirector` registration lags.
Solution: Added a zeroed 16-slot fallback `GraphicsBuffer` in Sargassum and bound it during static compute setup. The published `EncounterDirector` buffer still overrides it when valid, so active threat ownership remains with the director.
Rejected Alternatives: Delaying all boid simulation until EncounterDirector exists, shader keyword branching, or creating a second active predator feed. Standard Unity service timing was too brittle because one missing buffer can fail dispatch even with loop count zero.
Scalability potential: Low = fallback costs 256 B and zero threat loops until data arrives; Middle = director buffer overrides with capped loops; High = full 16-slot director buffer; Ultra = full slots plus acoustic panic visuals, no CPU boid path.
Hardware Impact: Prevents a startup/scene-streaming dispatch failure for a fixed 256 B VRAM reserve. MX350 runtime cost is effectively 0 us after initialization because the fallback is only a binding target and is not read when count is zero.

## Decision 6: Closest-Predator Slot Discipline
Problem: Low tier reads only slots 0-3, so arbitrary predator ordering can waste the three predator reads on distant threats while a closer predator is outside the loop.
Solution: Keep slot 0 as player and maintain predator AUP slots sorted by distance to the player. Live tracked predators are refreshed in place and re-sorted without scanning the 1024 headless pool every frame; headless ids are identified by a bounded id range instead of a broad bitmask.
Rejected Alternatives: Full 16-threat loop on MX350, per-frame scan of all 1024 headless entities, or CPU flocking. The full loop spends ALU on low silicon; the full headless scan spends main-thread time to solve a 16-slot ordering problem.
Scalability potential: Low = player plus 3 closest published predators; Middle = same sorted buffer with broader loop if tier policy expands; High/Ultra = full 16 sorted slots plus acoustic panic overlays.
Hardware Impact: MX350 keeps the four threat checks meaningful. CPU cost is bounded to a 16-slot insertion sort on live updates and full 1024 scan only on cold publication events. Estimated avoided GPU work remains up to 12 checks per boid while improving threat relevance.
