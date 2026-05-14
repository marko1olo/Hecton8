# Rationale_ALPHA_LEVIATHAN_COGNITION

Status: COMPILE VERIFIED / RUNTIME PENDING

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

## Decision 11

Problem: Acoustic ping consumption converted AUP with the implicit current floating-origin state, while the later cognition input captured the origin separately.
Solution: Capture `HectonFloatingOrigin.CurrentTotalOffset` once at evaluation start and use `AUPMath.ToRuntimeFloat3(in acousticSignal.PositionAup, floatingOriginOffset)` for acoustic targets and `CognitionInput.FloatingOriginOffset`.
Rejected Alternatives: Keeping `PositionAup.ToRuntimeFloat3()` was rejected because an origin shift between acoustic conversion and input packing can produce a mixed-frame target. Caching a Transform-only position was rejected because it discards AUP authority.
Scalability potential: Low, Middle, High, and Ultra use the same deterministic coordinate basis; high-tier presentation can add more fog and audio without changing target authority.
Hardware Impact: i3/MX350 cost is one captured `Vector3` and one `float3` assignment per slow evaluation; no heap allocation and no extra per-frame work.

## Decision 12

Problem: The batch contract says phase 3 is `Strike`, but the gameplay DOD requires that phase to veer off and avoid damage.
Solution: Expose `AlphaLeviathanPhase.Strike = 3` and alias `VeerOff` to `Strike`, preserving the existing no-hit behavior while matching the byte contract.
Rejected Alternatives: Renaming all behavior to real `Strike` was rejected because it implies impact damage. Leaving only `VeerOff = 3` was rejected because it weakens prompt conformance for telemetry and external readers.
Scalability potential: No runtime change; telemetry remains stable across device tiers and can be interpreted by tools as phase 3.
Hardware Impact: Compile-time alias only; 0 us runtime.

## Decision 13

Problem: The false charge must maximize `PlayerStress01`, but relying only on the acoustic roar makes stress timing dependent on the physiology slow tick ordering.
Solution: Publish a one-shot `PlayerStressSignal` at roar emission through `GlobalSignals`, using apex/acoustic flags and no direct physiology state mutation.
Rejected Alternatives: Directly modifying `PlayerStressMetricsRuntime` was rejected as cross-domain ownership. Publishing every frame during Feint was rejected as queue spam. A custom stress interface was rejected because `GlobalSignals` already has the decoupled contract.
Scalability potential: Low receives the same immediate visor/audio stress cue with one queued signal. High and Ultra can layer richer chromatic/audio presentation on the same `Stress01=1` event.
Hardware Impact: One 32-byte signal on false-charge transition only; 0 B/frame hot-path cost.

## Decision 14

Problem: A gaze/headlight break could force Hidden for only one 10Hz evaluation before circling resumed, making the vanish readable in code but weak in play.
Solution: Add `AlphaHiddenHoldSeconds = 1.15f` and hold phase 0 after a gaze/retinal break before returning to circling.
Rejected Alternatives: Holding Hidden indefinitely was rejected because it can stall the first-hour stalking beat. Adding a new timer lane was rejected because `StalkingPhaseStartTimes` already carries phase age without extra memory.
Scalability potential: Low keeps the cheap radial fake for a readable disappearance. High and Ultra use the same authority window to sell a longer SDF/fog dive with richer presentation.
Hardware Impact: One scalar comparison on the 10Hz Alpha branch; 0 B/frame.

## Decision 15

Problem: Alpha black-box telemetry marked SDF dive from retinal exposure even on low tier, and did not recompute the player gaze break bit for postmortem dumps.
Solution: Recompute gaze dot in telemetry with `math.rsqrt`; set `PlayerGazeBreak` when dot >= 0.8, and set `SdfDiveRequested` only when the Alpha is Hidden, has a player target, and high-tier smooth steering is active.
Rejected Alternatives: Trusting directive-local flags was rejected because they are not persisted in the output packet. Marking every retinal event as SDF was rejected because low-tier deliberately uses radial fake steering.
Scalability potential: Low telemetry now proves the cheap radial path. High/Ultra telemetry proves when the expensive SDF visual fake was actually requested.
Hardware Impact: Two rsqrt-normalized vectors per active Alpha telemetry write; slow-path post-evaluation only, no heap allocation.

## Decision 16

Problem: Alpha stalking was keyed to generic apex predator status, which could make every Leviathan-class apex inherit the first-hour PresenceCircle false-charge AI.
Solution: Add `UseAlphaLeviathanCognition` as an explicit cognition flag from `FaunaBrain` through `CreatureUtilityContext` into `PredatorCognitionDomain`; gate 10Hz Alpha cadence, Alpha telemetry, SDF dive, false charge override, roar, and stress spike on that flag.
Rejected Alternatives: Keeping `IsApexPredator` as the gate was rejected because AmbushBurst and SentinelPressure Leviathans are different encounter contracts. Adding a new component or singleton registry was rejected because the batch forbids custom MonoBehaviours and direct dependencies.
Scalability potential: Low = only the intended Alpha pays the 10Hz psychological-stalking branch. Middle = other apex predators keep normal utility cadence. High = PresenceCircle Alpha spends saved budget on SDF/fog presentation. Ultra = the same flag can drive extra roar, IK, and fog silhouette overkill without changing generic apex AI.
Hardware Impact: i3/MX350 avoids unnecessary Alpha telemetry writes and 10Hz SDF/gaze branches for non-Alpha Leviathans; expected gain scales with non-Alpha apex count, static estimate ~0.05-0.12 us avoided per non-Alpha apex slow eval plus no false roar/stress queue write.

## Decision 17

Problem: Alpha telemetry could resolve a default `PlayerTargetAup` when no player/acoustic target was present, creating finite but misleading postmortem samples with a fake far-away player.
Solution: When `HasPlayerTarget` is absent, keep the telemetry target position equal to the Alpha position, store `DistanceToPlayerMeters = 0`, and set local telemetry bit 5 as `AlphaLeviathanTelemetryNoPlayerTarget`.
Rejected Alternatives: Adding a new public `AlphaLeviathanTelemetryFlags.NoPlayerTarget` contract bit was rejected because the generated `Hecton8.AI.Cognition` project can be stale outside Unity and previously hid new contract members from `Hecton8.Core.csproj`. Resolving default AUP was rejected because it corrupts black-box evidence.
Scalability potential: Low/Middle/High/Ultra all get cleaner dumps without extra memory. High-end telemetry tooling can decode bit 5 as no-target while the 64-byte layout stays stable.
Hardware Impact: Replaces one default-AUP conversion with a branch and local assignment for no-target Alpha samples; static estimate saves the double3 conversion path on those samples and stays 0 B/frame.

## Decision 18

Problem: Mixed legacy species-profile Leviathans with a non-Leviathan archetype could still opt into Alpha cognition through broad `useLeviathanPresence`, even when the encounter type was not `PresenceCircle`.
Solution: Tighten `ShouldUseAlphaLeviathanCognition()` so the mixed legacy branch requires `useFeintRush` or `useLeviathanPresence && LeviathanEncounterType.PresenceCircle`.
Rejected Alternatives: Keeping broad `useLeviathanPresence` was rejected because AmbushBurst/SentinelPressure-style legacy hybrids would inherit first-hour fog stalking and false-charge stress. Removing the species-profile fallback entirely was rejected because legacy Alpha content still needs a migration bridge.
Scalability potential: Low = fewer accidental 10Hz Alpha evaluations on cheap devices. Middle = non-Alpha Leviathans keep their normal cadence. High = only PresenceCircle/feint encounters spend budget on SDF fog dive. Ultra = overkill presentation remains bound to the authored Alpha gate.
Hardware Impact: i3/MX350 avoids unnecessary Alpha phase telemetry, gaze/SDF checks, and false-charge queue writes for misconfigured legacy hybrids; static estimate remains ~0.05-0.12 us avoided per non-Alpha apex slow eval, 0 B/frame.

## Decision 19

Problem: Sixth-pass verification exposed a shared compile wall: `GlobalSignals.cs` referenced `ScanLogChangedSignal`, and the struct existed in the same file, but the `Hecton8.Core` namespace block lacked the explicit alias pattern used by other signal types.
Solution: Add `using ScanLogChangedSignal = Hecton8.Core.Signals.ScanLogChangedSignal;` beside the existing signal aliases.
Rejected Alternatives: Moving the struct, duplicating the signal, or rewriting PDA/scan-log consumers was rejected because that would cross unrelated ownership and add behavior churn. Reverting another agent's scan-log signal work was rejected because the signal is already wired by consumers.
Scalability potential: No runtime behavior change. The shared signal lane remains typed and NativeQueue-backed; the fix only restores compiler name resolution.
Hardware Impact: Alias-only compile fix, 0 us runtime and 0 B/frame.

## Decision 20

Problem: Alpha phase state could become stale when stalking was interrupted by losing the player/acoustic target or by rival apex visibility. On reacquire, an old Circling/FalseCharge phase age could trigger an immediate or incoherent false-charge beat.
Solution: Add `ResetAlphaLeviathanInterruptedPhase(slot, currentTime)` in the Burst predator evaluation path and call it whenever `UseAlphaLeviathanCognition` is set but the Alpha override is interrupted. The reset keeps the slot in Hidden and refreshes `StalkingPhaseStartTimes` during interruption.
Rejected Alternatives: Preserving stale phase was rejected because it lets old timing leak across target loss. Resetting only telemetry was rejected because movement authority would still resume from old state. Allocating another interruption timer lane was rejected because the existing phase timestamp lane is enough.
Scalability potential: Low = reacquire restarts with the cheap Hidden radial fake. Middle = clean fog-ring re-entry. High = SDF dive can sell the reacquire. Ultra = presentation can layer over a deterministic hidden restart instead of masking stale charge timing.
Hardware Impact: One byte write and one float write on interrupted Alpha slow evaluations only; static estimate <0.01 us per interrupted Alpha 10Hz eval on i3/MX350, 0 B/frame.

## OMEGA POLISH CHANGES

Problem: Anti-bloat audit required proof that stalking math did not become an honest simulation.
Solution: Kept the fog orbit as a visual fake: tangent vector + small radial correction, not a physical pursuit solver. Kept dive as SDF-biased direction fake, not pathfinding. Kept false charge as Feint with damage flag stripped.
Rejected Alternatives: 3D spline orbit, NavMesh path, real collision attack, and per-frame roar/logging were rejected as bloat.
Scalability potential: Low = radial flee + dominant-axis approximation. Middle = fog-ring tangent. High = SDF-biased dive. Ultra = same authority plus visual/audio overkill. Non-Alpha apex encounters and mixed legacy non-PresenceCircle hybrids no longer inherit Alpha overkill work; no-target telemetry remains cheap and readable.
Hardware Impact: Source proof indicates 0 B/frame in Alpha hot path; fixed telemetry cost is 19.2 KB only for active Alpha telemetry. Static math estimate remains under 0.1 us per active Alpha slow-tick branch on i3/MX350-class hardware.

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
- `Docs/AgentLogs/LOG_ALPHA_LEVIATHAN_COGNITION.md`
- `Docs/Tasks/Status_ALPHA_LEVIATHAN_COGNITION.md`
- `Docs/AgentLogs/Rationale_ALPHA_LEVIATHAN_COGNITION.md`

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` failed behind 131 global generated-reference errors.
- Second-pass `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` failed behind 127 global generated/cross-asmdef reference errors, including stale project generation for `Hecton8.AI.Cognition`.
- Third-pass `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` failed behind 132 generated/cross-asmdef reference errors. The Alpha-facing compiler line remains stale generated project visibility for `AlphaLeviathanTelemetryEntry`; no new local syntax error was isolated before the reference wall.
- Fourth-pass `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` first failed on a generated `Hecton8.World.Contracts.dll` file lock from another process; serialized `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /clp:ErrorsOnly` succeeded with 0 errors.
- Fifth-pass `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /clp:ErrorsOnly` succeeded with 0 errors after the no-target telemetry hardening.
- Sixth-pass `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /clp:ErrorsOnly` initially failed on missing `ScanLogChangedSignal` name resolution in shared `GlobalSignals.cs`; after adding the explicit alias, the same command succeeded with 0 errors. A later contention retry returned exit 1 with no diagnostics while Unity/Roslyn and another build were active; after those processes cleared, the same serialized command succeeded again with 0 errors in 2.51s.
- Seventh-pass `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /clp:ErrorsOnly` first failed on missing generated `Temp/bin/Debug` dependencies during Unity/Roslyn churn; after the generated DLLs repopulated, the same command succeeded with 0 errors in 1:41.69.
- Unity MCP refresh timed out after 60s; console was unavailable because no Unity session was attached.
- Second-pass Unity MCP refresh and console read failed at transport level: HTTP request to `127.0.0.1:8088/mcp` could not be sent.
- Third-pass Unity MCP refresh and console read failed at the same `127.0.0.1:8088/mcp` transport.
- Static scans found Alpha distance/direction code using `math.rsqrt` and no new managed collection/LINQ path in the Alpha hot branch.
- Second-pass static scans found no remaining `PositionAup.ToRuntimeFloat3()` call in `FaunaBrain.Compatibility.cs`; acoustic AUP now uses the explicit captured origin.
- Third-pass static scans found no `math.sqrt`, `math.normalize`, `.normalized`, `Mathf.Sqrt`, or `math.length(...)` in the Alpha-scoped files.
- Fourth-pass static scans confirmed Alpha behavior is gated by `UseAlphaLeviathanCognition`, with generic `IsApexPredator` retained for non-Alpha apex systems only. Allocation scan found no new managed collection/LINQ path in the Alpha hot branch.
- Fifth-pass static scans found no new managed collection/LINQ path, no `math.sqrt`, no `math.normalize`, no `.normalized`, and no `math.length(...)` in the Alpha-scoped hot path. `git diff --check` reported only LF-to-CRLF warnings.
- Sixth-pass scan confirmed the mixed legacy species-profile branch now requires `useFeintRush` or `useLeviathanPresence + PresenceCircle`; `git diff --check` reported only LF-to-CRLF warnings.
- Seventh-pass static scan confirmed `ResetAlphaLeviathanInterruptedPhase` adds no managed allocations, LINQ, sqrt, normalize, or length calls in the Alpha hot path.
