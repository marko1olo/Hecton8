# Rationale_SHINOBU_227

Status: SIGNAL POLISH STATIC COMPLETE / BUILD BLOCKED BY CPU GATE
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

## Verification Gate

Problem: Final compile is mandatory but project rule forbids `dotnet build` when CPU load is above 50 percent or any `dotnet`/`csc` is active.
Solution: Checked CPU/process gate repeatedly. CPU still returns 100 percent; no `dotnet`/`csc` process was active on latest check. Static grep and `git diff --check` were used instead. Build remains blocked by protocol, not by an observed compiler error.
Rejected Alternatives: Launching `dotnet build` under 100 percent CPU load to claim compliance. That violates the explicit batch rule and risks starving the shared 20-agent workspace.
Scalability potential: No runtime code changed for this decision. Static gate confirms low/middle/high/ultra paths remain continuous `GlobalQualityWeight`, not binary switches.
Hardware Impact: Certified exact microseconds saved remain 0 until profiler/build evidence exists. Static expectation is removal of one Manta Rigidbody velocity poll and one legacy transport force path per active tool tick; not reported as measured.

## Ultra Polish Pass

Problem: The first static pass still had avoidable hot-path debt: per-solve force packet buffer clearing, fixed-rate hydrodynamic solve cadence under low quality, editor graph repaint allocation, and black-box telemetry that did not record enough final packet state.
Solution: Force packets now trust `ForcePackets` as the authoritative length and do not clear stale rows. Hydrodynamic solve cadence continuously lerps from the fixed tick toward 20 Hz and scales the emitted force by accumulated solver dt. Telemetry records last flow force, battery, compute micros, and budget faults. The editor graph scratch buffer is allocated once in the editor cold path.
Rejected Alternatives: A new `NativeQueue` route was rejected because the existing PhysicsApplySystem/Vault packet bridge is the active authority route and adding another global route without a route card would split ownership. A binary low/high physics switch was rejected because the quality law requires continuous degradation. Per-repaint editor arrays were rejected because they hide GC in diagnostics.
Scalability potential: Low quality sheds solver frequency and uses dominant-axis/triangle-current approximations; middle quality blends drag and cadence; high/ultra returns to fixed-tick solve cadence and spends saved cycles on richer visual/audio signals instead of heavier gameplay truth.
Hardware Impact: i3/MX350 expected static gain is removal of up to 131072 bytes of packet buffer writes per scheduled solve plus fewer low-quality hydrodynamic solves. Exact microseconds remain unmeasured because compile/profiler are blocked by CPU gate.

## SignalBus Closure Pass

Problem: The previous pass computed audio and cavitation DTOs but left the final DSP/VFX route as Vault data only. That satisfied data separation but not the literal signal-lane requirement for Task 02 and Task 11.
Solution: `SeaglideAudioSignalDTO` now carries `TargetEntityHash` and `FrameIndex` without changing its 64-byte size. After the Burst jobs complete, `SeaglideHydrodynamicsRuntime` publishes bounded `ToolAcousticSignal` and `BubbleSpawnSignal` packets through the existing typed `SignalBus` lanes. Signal lanes are warmed during cold boot.
Rejected Alternatives: Instantiating particle prefabs, creating a new bespoke VFX queue, or publishing all 1024 mock rows every frame. Existing global lanes already own audio/VFX transport and include load shedding; duplicating them would split authority.
Scalability potential: Low quality publishes one presentation signal packet with reduced bubble intensity; higher weights smoothly increase the publish budget up to four packets and restore full intensity. Physics truth stays unchanged.
Hardware Impact: i3/MX350 avoids prefab churn and DSP Rigidbody polling. Added work is a bounded post-solver signal publish over 1-4 packets, not over the entire 1024-row mock buffer.

## Compile Gate Recheck

Problem: A compile pass is required, but the build gate is meaningful only if the project files include the edited Seaglide sources and the CPU is below the mandated threshold.
Solution: Checked generated csproj coverage and process load. Current generated csproj files list `MantaScooter.cs` but not the newly added Seaglide source files. CPU then returned to 100 percent before any safe compile launch.
Rejected Alternatives: Running dotnet against stale csproj files to report a false pass, or running a build under the 100 percent CPU gate.
Scalability potential: No runtime behavior changed by this gate.
Hardware Impact: Certified measured savings remain 0 us until Unity regenerates project files and compile/profiler can run under the CPU rule.
