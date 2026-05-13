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

## CCD Kernel Isolation

Problem: Player and vehicle CCD need shared math without turning locomotion into another Core dumping ground.
Solution: created Hecton8.Physics.CCD with KinematicCcdMath, using finite checks, rsqrt normalization, hit fraction rollback, dot-plane slide, KE math, and low-tier gate helpers.
Rejected Alternatives: duplicating math in each motor; moving helper into GlobalPhysicsStateManager; enabling built-in Unity CCD for manual MovePosition flows.
Scalability potential: Low = speed gate and stop-on-hit; Middle = one slide projection; High = slide plus native consequence signals; Ultra = same authority with overkill VFX consumers downstream.
Hardware Impact: i3/MX350 estimate is 25-70 us/frame saved at low speed by not scheduling sweeps and 8 us/impact saved by low-tier stop-on-hit. Profiler verification blocked.

## Deferred Sweep Authority

Problem: Same-frame cast/complete would fix tunneling but would violate the job swap discipline and stall the main thread.
Solution: reused existing deferred CapsulecastCommand buffers in HectonPlayerMotor and VehicleMotor, consuming results only after DispatcherJobSwap completion. Shift sequence and body-bind epoch reject stale player results; vehicle origin shift invalidates ready/pending sweep state.
Rejected Alternatives: Physics.CapsuleCast hot-path call for player/submarine; recursive two-bounce resweep; direct dependency on systems still being edited by other agents.
Scalability potential: Low = one hit, halt; Middle/High = tangent slide from preallocated multi-hit lane; Ultra = downstream VFX/audio/haptic amplification without extra authority queries.
Hardware Impact: deferred query avoids an estimated 18 us/frame blocking cost under high-speed locomotion and removes crash-class AUP long-sweep risk.

## Consequence Signals

Problem: Impact consequences need to be visible and debuggable without making locomotion spawn particles, call haptics, or shake cameras directly.
Solution: added HighSpeedImpactSignal and HapticRequest lanes, reused ImpactSignal/DebrisSpawnSignal/DamageSignal, and passed exact hit normal to CameraJuiceSignals for player/vehicle/fauna impacts.
Rejected Alternatives: managed events, direct prefab instantiation, direct device calls, speed-only damage.
Scalability potential: Low consumes minimal haptics/debris or ignores signals; Middle consumes sparks and camera; High/Ultra can layer screen/audio/particle overkill using the same fixed payload.
Hardware Impact: signal fan-out estimates 40 us/impact saved versus direct VFX spawn and 15 us/impact saved versus direct haptic device call on low-end silicon.

## Leviathan Lunge Guard

Problem: Predator cheat-lunge isolated teleport can pass the head through habitat geometry because it bypasses regular contact solving.
Solution: added a preallocated NonAlloc capsule sweep in FaunaBrain lunge path, clamps target AUP before teleport, slides on single walls, and stops on low tier or corner contact.
Rejected Alternatives: full physics lunge simulation; allocating a fresh RaycastHit array; letting animation collision events repair after penetration.
Scalability potential: Low = stop at first hit; Middle/High = deflected lunge; Ultra = consequence signals available for cinematic spark/audio layers.
Hardware Impact: estimated 10 us cheaper than full contact simulation and zero GC in the lunge hot path. Uses a cold RaycastHit scratch array because FaunaBrain is MonoBehaviour-side, not Burst job authority.

## Leviathan Consequence Parity

Problem: Leviathan lunge CCD had authority and debris, but did not emit the same impact audio, camera, haptic, and massive-damage packet set as player/vehicle CCD.
Solution: FaunaBrain now publishes ImpactSignal, CameraJuiceSignals directional impact, HapticRequest, and gated Hecton8.Core.Signals.DamageSignal from the same HighSpeedImpactSignal data. Debris intensity now reuses the impact intensity scalar.
Rejected Alternatives: direct camera shake from FaunaBrain; direct VFX or haptic device calls; separate fauna-only damage pathway; always damaging null/unknown targets.
Scalability potential: Low devices can ignore the extra native lanes or consume only debris; Middle/High can add camera and haptics; Ultra can layer lunge impact audio/VFX without new collision queries.
Hardware Impact: estimated 3 us saved versus separate screen-shake math, 15 us saved versus direct haptic call, and 40 us saved versus direct spark spawn on i3/MX350-class devices. No GC introduced.

## Verification Blocker

Problem: Editor compile verification could not be completed in this session.
Solution: attempted Unity MCP refresh/console and checked the active Unity launch log. Static review confirms CCD math normalizes with math.rsqrt and slide uses Velocity - dot(Velocity, Normal) * Normal.
Rejected Alternatives: reporting green compile without evidence; reverting CCD work because unrelated assemblies are failing.
Scalability potential: unchanged; authority paths are implemented but require integration compile after global blockers clear.
Hardware Impact: runtime estimates remain engineering estimates, not profiler measurements. Active blockers in the Unity log are unrelated compile failures in FaunaBrain.Foveated, ModEventProjectionBridge, SpectrumSystem, plus Burst resolution failure for Hecton8.Vehicles.VFX.

## OMEGA POLISH CHANGES

Problem: The polish mandate forbids unconditional sqrt when rsqrt is sufficient for impact presentation speed.
Solution: replaced VehicleMotor rejected-velocity speed and FaunaBrain lunge impact speed with `speedSq * math.rsqrt(speedSq)` after finite/epsilon checks.
Rejected Alternatives: keeping `math.sqrt(math.max(...))`; lookup table was rejected because impact speed is already scalar and rsqrt is cheaper without memory indirection.
Scalability potential: Low/Middle/High/Ultra all use the same cheap scalar authority; overkill remains in downstream signals, not collision math.
Hardware Impact: estimated 1-2 us saved per impact cluster on i3/MX350, profiler blocked by global compile state.

Problem: Cross-domain edits were necessary for native signal and blackbox routing.
Solution: Core signal/telemetry edits are limited to fixed-size payload lanes and GlobalPhysicsStateManager counters; Fauna edit is limited to the required Leviathan lunge guard.
Rejected Alternatives: direct object references from locomotion to VFX/haptics/camera; unmanaged code reaching across domains without signals.
Scalability potential: Low devices may ignore consequence lanes; high-end devices can consume the same payload for visual overkill.
Hardware Impact: signal indirection is estimated cheaper than direct spawns by 40 us/impact on low-end silicon.

Problem: Build verification required by OMEGA could not produce a clean result.
Solution: ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`; latest run after buffer hygiene failed with 113 errors from stale/missing asmdef references, including Hecton8.Core.Scheduling, Hecton8.Environment.Fluids, Hecton8.Core.Memory.Layout, audio propagation types, and stale Hecton8.Physics.CCD csproj generation.
Rejected Alternatives: marking VERIFIED MASTER GRADE without a green compile; editing unrelated dependency assemblies outside the prompt domain.
Scalability potential: unchanged.
Hardware Impact: no runtime measurement possible until integrator clears project compile.

Problem: Player CCD reused a multi-command RaycastHit buffer without the explicit pre-schedule clear used by VehicleMotor, creating a risk that stale contacts survive after a later sweep returns fewer hits.
Solution: added a fixed 32-slot default clear before scheduling the player CapsulecastCommand batch. The loop is gated behind the high-speed CCD path and matches the existing vehicle cleanup pattern.
Rejected Alternatives: trusting undocumented command overwrite behavior; allocating a fresh RaycastHit buffer; clearing in the consume path after stale data might already be read.
Scalability potential: Low avoids phantom impact/haptic/camera packets; High/Ultra can safely consume richer consequence signals without stale contacts.
Hardware Impact: estimated 1-3 us only on scheduled high-speed frames, exchanged for avoiding false wall collision recovery and downstream signal cost.

Problem: Fauna CCD consequence routing used `hit.collider.GetEntityId()` while player and vehicle CCD already use `RaycastHit.colliderEntityId`.
Solution: switched Leviathan target hashing to `hit.colliderEntityId`, removing the managed collider property dereference and making all CCD impact emitters use the same value-data hit identity path.
Rejected Alternatives: keeping the collider object lookup; adding a fauna-specific helper; using legacy GetInstanceID.
Scalability potential: Low tier avoids unnecessary object dereference during impact; High/Ultra receive the same consequence fidelity from cleaner payload generation.
Hardware Impact: estimated sub-1 us per Leviathan impact, but it removes a fragile managed reference path from a gameplay-critical consequence emitter.

Problem: Repeating full `dotnet build Hecton8.Core.csproj` after local CCD edits no longer produced useful signal; it timed out behind the same generated-csproj/reference churn and left MSBuild nodes alive.
Solution: treated the timeout as PENDING VERIFICATION evidence, then ran `dotnet build-server shutdown` to remove stale MSBuild/Roslyn nodes created by the attempt. Unity's own Roslyn compiler process was left alone.
Rejected Alternatives: killing all dotnet processes blindly; claiming build success; editing unrelated generated project references.
Scalability potential: unchanged.
Hardware Impact: no runtime impact; prevents local build-server CPU churn while Unity remains blocked by unrelated assemblies.

Final Git Diff:
`git status --short -- scoped files`:
- M Assets/_Project/Scripts/Fauna/FaunaBrain.cs
- M Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs
- M Assets/_Project/Scripts/HectonPlayerMovement.cs
- M Docs/AgentLogs/LOG_KINEMATIC_CCD_RESOLVER.md
- M Docs/AgentLogs/Rationale_KINEMATIC_CCD_RESOLVER.md
- M Docs/Tasks/Status_KINEMATIC_CCD_RESOLVER.md

`git diff --stat -- scoped files`:
- Assets/_Project/Scripts/Fauna/FaunaBrain.cs: 38 insertions, 2 deletions
- Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs: 3 insertions
- Assets/_Project/Scripts/HectonPlayerMovement.cs: 19 insertions, 8 deletions
- Docs/AgentLogs/LOG_KINEMATIC_CCD_RESOLVER.md: 48 insertions
- Docs/AgentLogs/Rationale_KINEMATIC_CCD_RESOLVER.md: 30 insertions, 2 deletions
- Docs/Tasks/Status_KINEMATIC_CCD_RESOLVER.md: 14 insertions, 6 deletions

Note: current worktree contains concurrent non-CCD edits from other agents in the same scoped files. I did not revert them.
