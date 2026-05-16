# Rationale_ACOUSTIC_ECHO_LOCATION_AI

Status: CORE COMPLETE / FINAL VALIDATION BLOCKED BY DEPENDENCY

## Decision 0 - Batch Memory Initialization
Problem: Agent-local status and rationale files were missing at session start.
Solution: Created persistent batch-local files before code edits so future context compression cannot erase assignment state.
Rejected Alternatives: Chat-only tracking was rejected because the batch protocol requires disk-backed state.
Scalability potential: Low/Middle/High/Ultra unaffected; this is process memory only.
Hardware Impact: 0 microseconds runtime impact on i3/MX350.

## Mandate Selection
Problem: Blind predator acoustic navigation crosses AI cognition, audio DSP, AUP authority, signal flow, and crash telemetry.
Solution: Bound implementation to 8 mandates: acoustic sonar, DSP queue discipline, AI cognition, pathing, AUP determinism, zero-GC, blackbox telemetry, and signal lane segregation.
Rejected Alternatives: Reading unrelated rendering/worldgen mandates was rejected as noise outside AI/Sensory ownership.
Scalability potential: Low uses last-node fake; Middle uses acoustic breadcrumbs; High adds IK sweep; Ultra can add richer breadcrumb memory without changing gameplay API.
Hardware Impact: Static planning only; expected hot-path target remains under 0.1 ms by using bounded fixed buffers.

## Decision 1 - Portal AUP As Hunt Authority
Problem: The existing predator acoustic branch could consume exact player AUPs, making blind cave hunts robotic.
Solution: Route acoustic hunt targets through `AcousticEchoLocationRuntime`; portal-propagated sound writes `AcousticPathResult.LastPortalAup` into `EchoTap.PortalAup`, and `EchoTrackingJob` writes that value to `InvestigateAup`.
Rejected Alternatives: Direct `NoiseSystem.PlayerNoiseSignal.PositionAup`, direct `AcousticPingSignal.PositionAup` as primary predator target, or runtime `Vector3` as authority were rejected because they leak player truth or lose AUP determinism.
Scalability potential: Low = direct source node fake; Middle = loudest breadcrumb trail; High = portal breadcrumb plus head sweep; Ultra = same API can accept richer portal tap density without changing predator cognition.
Hardware Impact: Expected i3/MX350 cost is under 0.02 ms for 32 taps, replacing repeated direct player acoustic distance logic.

## Decision 2 - Fixed Native Queue And One-Frame Job Latency
Problem: DSP and signal producers need to feed predators without managed allocations or direct object dependencies.
Solution: Use a persistent `NativeQueue<EchoTap>` plus fixed `NativeArray<EchoTap>[32]`; refresh drains bounded taps and schedules `EchoTrackingJob`, with frame N predators usually consuming frame N-1 output.
Rejected Alternatives: Blocking job completion every predator tick was rejected because it trades predictability for stalls; managed event delegates were rejected for GC risk.
Scalability potential: Low drops excess taps after 32; Middle keeps stable loudest selection; High/Ultra can increase producer richness while the consumer cap remains deterministic.
Hardware Impact: One-frame latency buys stable frame time on i3/MX350; expected saved stall risk is 20-80 us on predator-heavy frames.

## Decision 3 - Fauna Consumer Boundary
Problem: The sensory domain had no existing direct predator consumer; the current consumer lives in `Assets/_Project/Scripts/Fauna/`.
Solution: Minimal cross-domain hook: `CreatureUtilityBrain.Evaluate` asks the sensory runtime for an echo result and only passes portal AUP to cognition when the target is acoustic-only.
Rejected Alternatives: Rewriting `PredatorCognitionDomain` or inventing a new pathing subsystem was rejected as refactor-loop scope creep.
Scalability potential: Low/Middle retain existing steering; High/Ultra add optional IK head-look target without changing cognition memory layout.
Hardware Impact: Fauna hook is a few scalar branches and one AUP conversion per predator tick; expected cost is below 5 us/predator.

## Decision 4 - Visual Fake For High-End Head Sweep
Problem: The mandate wanted a predatory head sweep while approaching echo portals without physical smell/current simulation.
Solution: Compute `HeadSweep01` from a cheap sine and target distance, then offset the existing head-look target laterally only when no visual player target is active.
Rejected Alternatives: Full shark-like sensory cone simulation, IK graph rewrite, or per-bone search patterns were rejected as expensive and outside AI/Sensory.
Scalability potential: Low = no sweep; Middle = target-only; High = lateral sweep; Ultra = can widen sweep amplitude/frequency through the same output.
Hardware Impact: Approximate cost is 2-4 us/frame on i3/MX350, no allocations, buys visible predatory behavior.

## Decision 5 - Black Box And DSP Guarding
Problem: Echo taps can carry invalid DSP counts, NaN attenuation, or impossible portal locals.
Solution: Clamp sonar tap reads to read-only length and 32 taps, finite-check every AUP/local/intensity, and log a 300-entry native black-box ring with fault dump to `Docs/AgentLogs/Dump_ACOUSTIC_ECHO_LOCATION_AI.bin`.
Rejected Alternatives: Debug.Log-only diagnostics and trusting DSP tap counts were rejected because they fail the postmortem requirement.
Scalability potential: Low drops invalid/overflow taps; Middle/High/Ultra keep the same deterministic fault path.
Hardware Impact: Guard cost is estimated at 1-3 us/frame; avoided crash diagnosis time is not runtime but directly protects low-end stability.

## Decision 6 - Build Dependency Wall
Problem: `dotnet build Hecton8.Core.csproj` cannot currently reach this agent's code because unrelated batch edits deleted `Assets/_Project/Scripts/Core/Bucketing/ModuloSimulationBucketer.cs`; earlier attempts also exposed tether contract churn outside the AI/Sensory domain.
Solution: Stop dependency repair at the wall, do not revert other agents' Bucketing/Tether work, and mark final validation blocked by dependency.
Rejected Alternatives: Recreating Bucketing or reverting other agents' files was rejected as architectural sabotage outside assigned domain.
Scalability potential: Runtime design remains Low/Middle/High/Ultra ready; validation is blocked by external compile topology, not acoustic design.
Hardware Impact: 0 us runtime impact; compile gate remains unverified until Integrator resolves the deleted bucketer source.
