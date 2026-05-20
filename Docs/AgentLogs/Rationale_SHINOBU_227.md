# Rationale_SHINOBU_227

Status: PENDING VERIFICATION
Agent: SHINOBU_227
Domain: Echelon 4 Player, Kinematics & Tools / Scooter (Seaglide) Kinematics

## Initial Architecture Decision

Problem: The assignment targets Seaglide propulsion paths that may still manipulate Rigidbody or run FixedUpdate locally.
Solution: Source-first archaeology, then replace or add a data-only Burst pipeline that emits force packets for the central physics owner.
Rejected Alternatives: Direct `Rigidbody.AddForce`, local `FixedUpdate`, object-instantiated cavitation, and per-frame registry polling. These violate PhysicsApplySystem ownership, Zero-GC, execution phase law, and global authority boundaries.
Scalability potential: Low uses coarse drag/current cadence and presentation fakes; Middle uses full thrust/drag cadence with cached flow; High increases telemetry/audio/VFX signal richness; Ultra spends saved cycles on visual cavitation/audio detail, not extra gameplay truth.
Hardware Impact: i3/MX350 target avoids main-thread PhysX sync and per-object component traversal. Estimated benefit cannot be claimed without profiler proof; static expected saving is removal of per-frame managed/component physics touchpoints.

## First-20-Minutes Route

Problem: Player handheld underwater travel must not produce control hitches in the opening route.
Solution: Treat Seaglide propulsion as a route blocker removal for player traversal responsiveness during Copper Wire exploration.
Rejected Alternatives: Delaying vehicle physics behind future input/physics agents; mock job isolates math under load without waiting for full player spawn path.
Scalability potential: Same movement truth across tiers, variable cadence and presentation richness.
Hardware Impact: Keeps MX350 hot path under suspicion threshold until profiler proof exists.
