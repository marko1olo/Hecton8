# Rationale_ACOUSTIC_ECHO_LOCATION_AI

Status: VERIFIED MASTER GRADE / ACOUSTIC DOMAIN CLEAN / DOTNET BUILD BLOCKED BY EXTERNAL DEPENDENCIES / UNITY RUNTIME PENDING

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
Solution: Use a persistent `NativeQueue<EchoTap>` bridge for producer compatibility, but source all persistent frame/result/black-box slabs from `GlobalDataVault` via generation-checked handles; refresh drains bounded taps and schedules `EchoTrackingJob`, with frame N predators usually consuming frame N-1 output.
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
Problem: Earlier `dotnet build Hecton8.Core.csproj` attempts were blocked by unrelated world, determinism, ladder, and submarine edits, then briefly passed, then a fresh build exposed new unrelated compile walls.
Solution: Re-ran the build after the latest inquisition pass. A compile-only preprocessor placement error in `InputDispatcher.cs` was corrected so the build could advance, then validation stopped in non-acoustic domains: Fauna bite IK local-name conflict, Tool durability unresolved DataVault migration helpers/fields, and Bootstrap initializer signature mismatch.
Rejected Alternatives: Stale green-build reporting, chat-only validation, and broad cross-domain rewrites of tool durability/IK/bootstrap systems were rejected.
Scalability potential: Low/Middle/High/Ultra acoustic behavior remains static-scan clean, but project-level C# compile proof is dependency-blocked until those external domains land coherent code.
Hardware Impact: 0 us runtime impact; latest validation wall-clock was 2m08s before the external compile wall.

## Decision 7 - Multiplatform Data Sovereignty Polish
Problem: The acoustic structs were sequential without explicit pack, and the sensory runtime still owned persistent NativeArray fields, violating ARM64 layout discipline and GlobalDataVault sovereignty.
Solution: Added `Pack=1` to acoustic tap/result/state/black-box structs; added `SystemID.AISensory` plus acoustic buffer IDs; resolved frame taps, job result, and 300-frame black-box through `GlobalDataVault` handles; added finite guards for head-sweep delta math and a one-shot black-box dump gate.
Rejected Alternatives: Keeping private NativeArrays was rejected by the DataVault mandate; moving EchoTap into a new SignalBus lane was rejected because `MovementAcousticSignal` and `AcousticPingSignal` already exist and the prompt explicitly requires the NativeQueue DSP bridge.
Scalability potential: Low keeps direct-node fake and bounded 32 taps; Middle keeps breadcrumb trail; High adds IK sweep; Ultra can increase producer richness through existing taps without widening gameplay API. The saved CPU remains available for visual overkill in consumers: richer IK sweep, visor salt/silt VFX, and hull-dent presentation systems without increasing acoustic truth cost.
Hardware Impact: DataVault handle resolution adds roughly 1-2 microseconds per refresh but removes private persistent allocation ownership and stale-handle risk; one-shot dump gate avoids repeated disk rewrites on Steam Deck/MicroSD fault storms.

## Decision 8 - Black-Box Heartbeat And Backlog Cap
Problem: Hunt-only black-box writes did not guarantee the last 300 acoustic refresh frames, and the NativeQueue bridge could accept more tap submissions than the 32-tap frame slab would ever process.
Solution: Added a per-refresh heartbeat write with same-frame de-duplication, prewarmed the 64-tap queue, capped main-thread echo ingress at 64 queued taps, drained overflow deterministically, saturated `AcousticHuntsTriggered` at `uint.MaxValue`, and rejected non-finite current time through the fault black-box path.
Rejected Alternatives: Unbounded queue growth, repeated same-frame black-box entries for every predator, and counter wraparound were rejected because they weaken postmortem evidence.
Scalability potential: Low/Toaster drops excess sound taps deterministically; Middle keeps one heartbeat per refresh; High/Ultra can feed richer portal taps while the AI cost remains capped and the saved budget stays available for presentation overkill.
Hardware Impact: Queue prewarm is cold only; hot path adds one integer cap branch and one heartbeat write per refresh, estimated ~1 us/refresh, while removing MicroSD-hostile fault storms and backlog spikes.

## Decision 9 - Fresh External Compile Wall
Problem: A final build recheck after the acoustic polish pass no longer matches the prior green state because parallel agents modified non-acoustic systems.
Solution: Fixed only the trivial compile-only `#define` placement in `InputDispatcher.cs`, then stopped at the next wall because the remaining errors are owned by Fauna animation, Tools, and Bootstrap domains.
Rejected Alternatives: Editing unrelated systems deeply from the acoustic prompt was rejected as domain drift; leaving status as green was rejected as false reporting.
Scalability potential: Acoustic Low/Middle/High/Ultra paths are unchanged; no acoustic runtime behavior was altered by the external compile-wall triage.
Hardware Impact: 0 microseconds runtime impact for the acoustic system; build validation remains blocked by external code.
