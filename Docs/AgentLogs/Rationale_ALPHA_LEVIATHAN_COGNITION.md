# Rationale_ALPHA_LEVIATHAN_COGNITION

Status: PENDING VERIFICATION

## Decision 1

Problem: Batch asks for Alpha Leviathan stalking AI but forbids custom MonoBehaviours.
Solution: Extend `PredatorCognitionDomain` Burst job and the existing `FaunaBrain` managed-to-native bridge only.
Rejected Alternatives: A new `AlphaLeviathanStalker` component would violate the prompt and add scene wiring risk. A separate runtime manager would add singleton pressure and cross-agent dependency.
Scalability potential: Low uses byte phase + axis/radial math. Middle uses fog-ring tangent. High uses smoother rsqrt steering. Ultra can spend saved CPU on richer presentation/roar/IK without new authority.
Hardware Impact: i3/MX350 hot-path target remains native array reads/writes and scalar math; expected managed GC gain versus component orchestration is 0 B/frame.

## Decision 2

Problem: `PredatorData` is not a current source type; the real cognition data owner is `PredatorCognitionDomain`.
Solution: Treat the domain-owned SoA native banks as the predator data surface and add `NativeArray<byte>` phase state there.
Rejected Alternatives: Inventing a `PredatorData` type would duplicate source authority and risk interface drift. Extending `CognitionCore` would disturb its 64-byte layout.
Scalability potential: Byte lane is cheap on low hardware and leaves high-tier calculations free to improve visual stalking.
Hardware Impact: 256 byte session allocation for phase state plus sentinel metadata; no per-frame heap pressure.

## Decision 3

Problem: Current `FaunaBrain.cs`, `GlobalSignals.cs`, and `Hecton8.Core.asmdef` already contain uncommitted edits.
Solution: Preserve and patch around current working-tree state.
Rejected Alternatives: Reverting or checking out files is forbidden; blind overwrite would erase another agent's work.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact.

## Decision 4

Problem: The Alpha needs to stalk from fog distance without becoming another direct chase predator.
Solution: Phase 1 computes tangent steering from `cross(Up, awayFromPlayer)` and adds a small radial correction toward `FogEnd - 10m`.
Rejected Alternatives: NavMesh orbiting and waypoint rings were rejected because they add scene setup and can desync under AUP shifts. Pure circle physics was rejected because it spends CPU on realism the player cannot inspect in fog.
Scalability potential: Low/Middle use dominant-axis approximation for stable silhouette orbit. High/Ultra use rsqrt steering and can buy richer S-curve/presentation with saved CPU.
Hardware Impact: i3/MX350 cost is scalar vector math on a 10Hz slow tick; static estimate below 0.1 us per active Alpha evaluation, 0 B/frame.

## Decision 5

Problem: Looking at the monster must make it vanish, but real geometry hiding/pathfinding is too expensive and brittle for MX350.
Solution: High tier blends radial escape with one SDF gradient/down vector; Low tier uses radial flee only.
Rejected Alternatives: DDA path search, NavMesh dive targets, and physics burrowing were rejected as too slow and unpredictable for a scare beat.
Scalability potential: Low = radial break. Middle = deterministic down break. High = SDF-biased dive. Ultra = same authority plus visual overkill through fog, roar, IK, particles.
Hardware Impact: Low tier avoids SDF sampling; expected saved cost is the gradient query branch per 10Hz evaluation.

## Decision 6

Problem: False charge must spike stress without creating an early-game death path.
Solution: Phase 2 forces Feint presentation, 30 m/s speed multiplier, one roar signal, and clears `ShouldAttack`; Phase 3 veers up/away before 15m.
Rejected Alternatives: Reusing Attacking + attack cooldown was rejected because attack code can still hit if target range and cooldown align. A scripted cutscene was rejected because it breaks systemic AI authority.
Scalability potential: Low gets the same readable feint using cheap steering. High/Ultra can spend presentation budget on Doppler roar and fog silhouettes.
Hardware Impact: Hot path is one phase branch and a speed multiplier; managed `AcousticPingSignal` publish occurs only on phase transition, not every frame.

## Decision 7

Problem: The Alpha must ignore biomass/ecology commands while still sharing the fauna runtime.
Solution: Gate `ApplyEcologyChainOverrides` behind `!IsApexPredator()` in `FaunaBrain`.
Rejected Alternatives: Encoding ecology exceptions in `EcosystemDirector` was rejected because it would cross domain ownership and risk other agents' migration/biomass work.
Scalability potential: No visual downgrade; saved CPU is spent on fog stalking instead of food-chain branch work.
Hardware Impact: i3/MX350 avoids the ecology override chain for Alpha slow ticks.

## Decision 8

Problem: AUP shifts can invalidate world-space stalking targets.
Solution: Consume acoustic pings as AUP, store player target AUP in `CognitionInput`, and resolve runtime coordinates inside the Burst domain using the current floating-origin offset.
Rejected Alternatives: Caching `Transform.position` as the authoritative target was rejected because it can drift after origin shifts.
Scalability potential: Same path supports low and ultra hardware; only presentation changes by tier.
Hardware Impact: AUP conversion is slow-tick only; no per-frame heap pressure.

## Decision 9

Problem: Failures in AI phase logic need postmortem evidence.
Solution: Add a fixed 300-entry `NativeArray<AlphaLeviathanTelemetryEntry>` circular buffer and dump it to `Docs/AgentLogs/Dump_ALPHA_LEVIATHAN_COGNITION.bin` on invalid numeric state.
Rejected Alternatives: Text logging every tick was rejected as GC and I/O noise. Unbounded history was rejected as memory creep.
Scalability potential: Low devices pay fixed memory only; high devices retain enough data to diagnose visual overkill interactions.
Hardware Impact: 19.2 KB fixed telemetry storage; no dynamic allocation after initialization.

## Decision 10

Problem: Compile proof could not be completed through the local generated project.
Solution: Mark compile as dependency-blocked and keep status `PENDING VERIFICATION`; do not claim runtime proof.
Rejected Alternatives: Editing generated `.csproj` files was rejected because Unity owns asmdef project generation. Claiming success from static scans was rejected as fake reporting.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact.

## OMEGA POLISH CHANGES

Problem: Anti-bloat audit required proof that stalking math did not become an honest simulation.
Solution: Kept the fog orbit as a visual fake: tangent vector + small radial correction, not a physical pursuit solver. Kept dive as SDF-biased direction fake, not pathfinding. Kept false charge as Feint with damage flag stripped.
Rejected Alternatives: 3D spline orbit, NavMesh path, real collision attack, and per-frame roar/logging were rejected as bloat.
Scalability potential: Low = radial flee + dominant-axis approximation. Middle = fog-ring tangent. High = SDF-biased dive. Ultra = same authority plus visual/audio overkill.
Hardware Impact: Source proof indicates 0 B/frame in Alpha hot path; fixed telemetry cost is 19.2 KB. Static math estimate remains under 0.1 us per active Alpha slow-tick branch on i3/MX350-class hardware.

Cinematic Cheats used:
- Fog silhouette ring: `FogEnd - 10m`, tangent steer, radial correction.
- SDF dive fake: high tier uses one gradient-biased down vector; Low tier radial only.
- Dear lie false charge: 30 m/s Feint, roar signal, no `ShouldAttack`.
- Stress without death: veer up/away at <15m.

Final Git Diff:
- `Assets/_Project/Scripts/AI/Cognition/Hecton8.AI.Cognition.asmdef`
- `Assets/_Project/Scripts/AI/Cognition/AlphaLeviathanCognitionContracts.cs`
- `Assets/_Project/Scripts/Core/GlobalSignals.cs`
- `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs`
- `Assets/_Project/Scripts/Fauna/FaunaBrain.Compatibility.cs`
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs`
- `Assets/_Project/Scripts/Hecton8.Core.asmdef`
- `Docs/Tasks/Status_ALPHA_LEVIATHAN_COGNITION.md`
- `Docs/AgentLogs/Rationale_ALPHA_LEVIATHAN_COGNITION.md`

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` failed behind 131 global generated-reference errors.
- Unity MCP refresh timed out after 60s; console was unavailable because no Unity session was attached.
- Static scans found Alpha distance/direction code using `math.rsqrt` and no new managed collection/LINQ path in the Alpha hot branch.
