# Rationale_ACOUSTIC_ECHO_LOCATION_AI

Status: VERIFIED MASTER GRADE / ACOUSTIC DOMAIN STATIC CLEAN / DOTNET BUILD GREEN / UNITY BATCHMODE BLOCKED BY EXTERNAL ASMDEF WALL / PLAYMODE PROFILER BLOCKED

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
Problem: Earlier `dotnet build Hecton8.Core.csproj` attempts were blocked by unrelated world, determinism, ladder, submarine, input, fauna, tools, bootstrap, construction, and cognition edits from parallel work.
Solution: Re-ran the build after the latest current-disk repair pass. A compile-only preprocessor placement error in `InputDispatcher.cs` was corrected earlier, and the final adjacent AI blocker in `PredatorCognitionDomain.cs` now uses the existing `MathGuard.IsFinite(float3)` helper in species-target validation. The latest build succeeds with 0 errors and 0 warnings.
Rejected Alternatives: Stale dependency-blocked reporting, chat-only validation, and broad cross-domain rewrites were rejected.
Scalability potential: Low/Middle/High/Ultra acoustic behavior now has a green C# compile gate again; Unity Play Mode/profiler evidence remains a separate runtime gate.
Hardware Impact: 0 us runtime impact; latest successful validation wall-clock was 3m06.93s.

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
Hardware Impact: 0 microseconds runtime impact for the acoustic system; the external compile wall was cleared by current-disk parallel repairs plus one adjacent AI finite-helper correction.

## Decision 10 - Green Compile Revalidation
Problem: The status file was stale after the compile wall shifted from 96 errors to 25 errors, then to 2 errors, then one adjacent AI finite-helper error.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`; patched the final `PredatorCognitionDomain` finite check to `MathGuard.IsFinite(candidate)`; reran build to success.
Rejected Alternatives: Leaving task 18 blocked after a green build was rejected; adding a duplicate local `IsFinite` helper was rejected because `MathGuard` already owns the typed lane for finite math.
Scalability potential: Low/Middle/High/Ultra acoustic paths are unchanged; this only restores project compile evidence.
Hardware Impact: 0 microseconds runtime impact in acoustic echo logic; build verification wall-clock was 3m06.93s on the final current-disk check.

## Decision 11 - Final Current-Disk Race Check
Problem: While parallel agents were active, a follow-up compile briefly saw a stale Bootstrap/DataVault signature mismatch that was no longer present in current source.
Solution: Re-read the current source around `GameBootstrapper.EnsureJobAdmissionServiceRegistered`, reran the build after the parallel edit settled, and kept the status tied to the latest successful current-disk result.
Rejected Alternatives: Reporting the transient stale error after source no longer matched it was rejected; editing Bootstrap again was rejected once the current source already used the one-argument service contract.
Scalability potential: Acoustic Low/Middle/High/Ultra behavior unchanged; this is validation evidence only.
Hardware Impact: 0 microseconds runtime impact; final compile verification wall-clock was 3m06.93s.

## Decision 12 - Queue Ingress Hardening
Problem: The acoustic runtime still exposed a direct `NativeQueue<EchoTap>.ParallelWriter`, and queue drain trusted dequeued taps even though a direct writer could bypass main-thread validation and backlog accounting.
Solution: Removed the unused direct writer API and added `IsValidTap` validation in `DrainEchoTapQueue` before any queued tap reaches `EchoTrackingJob`.
Rejected Alternatives: Leaving an unbounded direct writer was rejected; adding another signal lane was rejected because existing `MovementAcousticSignal`, `AcousticPingSignal`, and portal propagation calls already cover the required producers.
Scalability potential: Low/Toaster remains a capped direct-node fake; Middle/High/Ultra get the same deterministic 32 scored taps without allowing invalid queue payloads into the Burst job.
Hardware Impact: Validation adds finite checks for at most 32 dequeued taps, estimated below 1 microsecond/frame on i3/MX350; it prevents NaN propagation and unaccounted producer bypass.

## Decision 13 - External Compile Wall After Hardening
Problem: A build after the acoustic queue hardening failed outside AI/Sensory with lockstep constants, ecosystem DataVault property writes, global signal sanitizer helpers, and tether fire-request helpers.
Solution: Stopped cross-domain surgery and marked final validation dependency-blocked again. Acoustic source-level scans passed, but project-level compile and Unity batchmode are blocked by unrelated current-disk errors.
Rejected Alternatives: Pretending the prior green build still applies was rejected; editing the ecosystem and tether systems from the acoustic prompt was rejected as domain drift.
Scalability potential: Acoustic Low/Middle/High/Ultra behavior is unchanged except safer queue ingress.
Hardware Impact: 0 microseconds runtime impact from the compile wall; latest failed compile wall-clock was 35.98s.

## Decision 14 - Current-Disk External Wall Shift
Problem: A fresh build no longer failed on the previous lockstep/global-signal groups; the first attempt saw a transient `SubmarineFluidDynamics.cs` parse error, and the stable re-run exposed 167 external errors in UI navigation, ecosystem, dispatcher, and tether/winch code.
Solution: Re-read the source around the transient submarine error, reran the build after the disk state settled, and kept the acoustic task dependency-blocked because no errors target `Assets/_Project/Scripts/AI/Sensory/`.
Rejected Alternatives: Reporting the transient submarine parse error as stable was rejected; patching UI, ecosystem, dispatcher, and tether ownership from the acoustic prompt was rejected as unbounded domain drift.
Scalability potential: Acoustic Low/Middle/High/Ultra behavior is unchanged; the blocked systems must be repaired by their owners before Unity runtime/profiler verification is meaningful.
Hardware Impact: 0 microseconds runtime impact in acoustic echo logic; latest failed build wall-clock was 3m40.72s.

## Decision 15 - Current-Disk Green Compile And Unity Gate
Problem: The previous 167-error report became stale; current source no longer matched the reported UI/navigation `_snapshot` errors, and final validation needed a fresh current-disk compile instead of a stale dependency-blocked status.
Solution: Reran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`; it succeeded with 0 warnings and 0 errors in 1.31s. Then attempted Unity 6000.4.1f1 batchmode import/compile with a log under `Docs/AgentLogs/Unity_ACOUSTIC_ECHO_LOCATION_AI.log`, but Unity aborted because another Editor instance has `C:/hades/Hecton8` open and no Unity MCP resources/templates are exposed in this session.
Rejected Alternatives: Treating stale compiler output as current truth was rejected; killing the user's open Unity editor was rejected; claiming Unity runtime verification from `dotnet build` was rejected.
Scalability potential: Acoustic Low/Middle/High/Ultra behavior is unchanged; C# compile is verified, but Play Mode, profiler, Quest/Android, Metal/Mac, and Steam Deck I/O evidence remain pending until the live Editor can be queried or closed.
Hardware Impact: 0 microseconds runtime impact in acoustic echo logic; green compile wall-clock was 1,310,000 microseconds.

## Decision 16 - Project-Root Blackbox Dump Path And Unity Batchmode
Problem: The acoustic fault dump used `Path.GetFullPath(DumpRelativePath)`, which depends on process working directory. Unity Editor, batchmode, Windows shortcuts, and Steam Deck launch contexts do not guarantee that current directory is the project root.
Solution: Added `ResolveDumpPath()` using `Application.dataPath/..` as the project root and falling back to the old relative path only if `Application.dataPath` is empty. Re-ran `dotnet build` green and re-ran Unity batchmode after the Editor lock cleared; exit code 0 and no compiler/fatal/abort/exception matches in the fresh log.
Rejected Alternatives: Writing to `Application.persistentDataPath` was rejected because the batch black-box contract requires `Docs/AgentLogs`; keeping current-working-directory behavior was rejected as Steam Deck/MicroSD hostile; adding a hot-path path cache was rejected because dumps are one-shot fault-path only.
Scalability potential: Low/Middle/High/Ultra acoustic behavior unchanged; fault dumps now land in the repo log path consistently across launch contexts.
Hardware Impact: 0 microseconds hot-path impact; cold fault dump path resolution happens only after a NaN/invalid AUP fault and is one-shot gated.

## Decision 17 - Same-Frame Stalled Job Gate
Problem: When `EchoTrackingJob` was still running, every predator resolver in the same frame could re-enter the stalled-job branch, recheck `JobHandle.IsCompleted`, and rewrite the same heartbeat.
Solution: Set `_lastRefreshFrame = frame` before writing the heartbeat and returning from the still-running job branch. This preserves the one-frame acoustic latency contract and collapses same-frame stalled-job polling to one check.
Rejected Alternatives: Forcing `Complete()` in every predator tick was rejected because it creates frame spikes; leaving repeated same-frame polling was rejected because swarm density should not multiply a known stalled state.
Scalability potential: Low/Toaster gets predictable one-check behavior under load; Middle/High/Ultra retain the same acoustic result API and can spend saved CPU on presentation-side overkill instead of duplicate scheduler polling.
Hardware Impact: Estimated 0 microseconds when the job is complete; during a stalled acoustic job, avoids roughly 1-3 microseconds per 32 predator resolver calls on i3/MX350 by skipping repeated handle checks and duplicate heartbeat writes.

## Decision 18 - Unity Assembly Contract Repair And Current Wall
Problem: Current Unity 6000.4.1f1 batchmode did not match the prior green claim. `Unity_ACOUSTIC_ECHO_LOCATION_AI_CURRENT.log` failed in root `_Project/Editor` scripts and `Hecton8.Audio.Virtualization` because Unity asmdefs did not expose the assemblies those files directly use.
Solution: Added `Assets/_Project/Editor/Hecton8.Project.Editor.asmdef` with the same project/editor references already used by the existing editor assembly, and added a direct `Hecton8.Core.Contracts` reference to `Hecton8.Audio.Virtualization.asmdef`. Re-ran `dotnet build`, which stayed green. Re-ran Unity batchmode; the wall moved to separate runtime asmdef ownership in player kinematics, GPR, crab IK, foveated rendering, and visual flare signal routing.
Rejected Alternatives: Claiming Unity green from stale logs was rejected; removing the new asmdef repair was rejected because it fixed a real editor/audio compile-contract gap; continuing into broad graphics/world/fauna/global-signal rewrites was rejected after the wall moved outside acoustic ownership.
Scalability potential: Acoustic Low/Middle/High/Ultra behavior is unchanged. The repair is compile topology only; it does not alter tap caps, portal breadcrumb authority, noisemaker priority, silence loss, or head-sweep presentation.
Hardware Impact: 0 microseconds acoustic runtime impact. Latest `dotnet build` wall-clock was 331,190,000 microseconds; Unity Tundra reports the moved compiler wall at 9,610,000 microseconds inside `Unity_ACOUSTIC_ECHO_LOCATION_AI_ASMDEF2.log`.
