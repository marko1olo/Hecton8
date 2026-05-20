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

## Runtime Boundary

Problem: `MantaScooter` was the real Seaglide-equivalent owner; `Assets/_Project/Scripts/Equipment` is absent in this branch.
Solution: Remove `Rigidbody` storage/velocity reads from `MantaScooter`; it now resolves player motion through `PlayerRuntimeContextService` and writes `SeaglidePropulsionRequestDTO` to `SeaglideHydrodynamicsRuntime`.
Rejected Alternatives: Leaving `GetTransportPropulsionForce()` active in `HectonPlayerMovement` or reading `_playerRigidbody.linearVelocity` for movement/presentation. Both preserve the legacy component physics path.
Scalability potential: Low keeps authored motion with coarser hydrodynamic cadence; Middle/High/Ultra increase drag precision, flow fidelity, cavitation/audio richness through continuous quality.
Hardware Impact: i3/MX350 removes per-tool Rigidbody velocity polling and legacy force contribution from the player movement branch. Exact microseconds require Unity profiler; static expected saving is one managed component physics read plus one legacy force path per active tool tick.

## Burst Force Pipeline

Problem: Handheld propulsion needs water drag/current/strain without direct body mutation.
Solution: Added `SeaglideHydrodynamicsRuntime`, explicit-layout DTOs, Burst jobs for thrust, drag, current advection, metabolism, audio parameters, telemetry, and a `PhysicsApplySystem.SeaglideQueue` bridge.
Rejected Alternatives: Direct `Rigidbody.AddForce`, `FixedUpdate`, managed particle spawning, and binary quality tiers. Central `PhysicsApplySystem` remains the only body application point.
Scalability potential: Low uses dominant-axis speed and triangle-wave current fake; Middle blends toward quadratic drag; High samples Vault flow records; Ultra spends saved cycles on visual/audio signal detail, not extra gameplay truth.
Hardware Impact: i3/MX350 gets cache-aligned 64/128-byte DTO streaming and no hot managed allocations. The 1000-record mock generator exists to measure worst-case request pressure once build CPU is clear.

## Black Box And Editor Gates

Problem: A NaN force or layout drift must be diagnosable without chat history.
Solution: Added 300-entry `SeaglideTelemetryEntry` Vault ring, fault dump path `Docs/AgentLogs/Dump_SHINOBU_227.bin`, editor layout trap, x-ray window, scanner report, and debug force gizmo.
Rejected Alternatives: String logs after crash, runtime debug UI, and unchecked sequential layout.
Scalability potential: Low devices keep telemetry cheap; high devices use the same physical truth and richer editor/presentation diagnostics.
Hardware Impact: Hot path remains unmanaged. Editor allocations are isolated behind `#if UNITY_EDITOR`.
