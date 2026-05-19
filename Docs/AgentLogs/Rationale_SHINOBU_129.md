# Rationale_SHINOBU_129

## Decision 001 - Stop On Missing XML Directive

Problem: The user assigned `SHINOBU_129`, but `Docs/Tasks/CURRENT_BATCH.md` contains no `<AGENT_PROMPT id="SHINOBU_129">` block. CLI extraction failed, and `rg` confirmed only 20 prompt blocks in the current batch.

Solution: Stop implementation and record a blocker. The DOD practice is strict batch parsing: only the extracted XML block is authoritative. Relevant mandates read before any coding decision: `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `ARCH_Execution_Phases`, `ARCH_Global_Registry_ServiceLocator_DI_Init`, `ARCH_Signal_Lane_Segregation`, `MATH_AUP_Determinism_Sync`, `MATH_Deterministic_RNG_SlotMachine`, `DBG_Telemetry_Crash_Reporting_PostMortem`.

Rejected Alternatives: Rejected inventing a 20-task tide/seismic plan from chat text. Rejected using neighboring `SHINOBU_120` because strict parsing says neighboring tasks must be deleted from memory. Rejected editing Atmosphere/Celestial code without domain-specific XML authorization.

Scalability potential: No runtime system was changed. If a valid prompt arrives, the intended architecture must scale low/middle/high/ultra through continuous `GlobalQualityWeight`: low uses triangle-wave tide/seismic scalars at low cadence; middle evaluates more harmonics; high/ultra spend saved cycles on richer renderer/audio responses, not planetary simulation.

Hardware Impact: 0 us saved in runtime because no code was added. Avoided an unauthorized compile-risk change on i3/MX350.

## Black Box Position

Superseded by Decision 014: `Dump_SHINOBU_129.bin` is now active alongside the XML-required celestial dump path.

## Decision 002 - Correct Prompt Extraction Regex And Resume

Problem: The initial blocker used an exact regex for `<AGENT_PROMPT id="SHINOBU_129">`, but the active batch file stores the prompt as `<AGENT_PROMPT id="SHINOBU_129" role="CELESTIAL_TIDE_SEISMIC_GENERATOR" chat_name="SHINOBU_129">`. The old regex produced a false negative.

Solution: Re-extracted the block with the attribute-aware CLI regex `<AGENT_PROMPT id="SHINOBU_129"[^>]*>[\s\S]*?</AGENT_PROMPT>`, confirmed 20 tasks, and converted the status ledger to ACTIVE / PENDING VERIFICATION. This preserves strict batch isolation without borrowing neighboring prompts.

Rejected Alternatives: Rejected continuing with the stale blocker after `rg` proved line 1516 contains the prompt. Rejected copying the user chat text as the sole task source because the on-disk XML is now present and authoritative.

Scalability potential: The implementation must consume continuous `GlobalQualityWeight` for harmonic count, mock time cadence, and presentation tremor richness. Low/MX350 uses one principal tide harmonic and sparse seismic oscillator evaluation; middle enables solar/secondary terms; high/ultra spends saved CPU on richer renderer/audio shader scalars, not physical orbit simulation.

Hardware Impact: Correcting the regex has 0 runtime cost and prevents unauthorized scope drift. Expected runtime work remains sub-0.1 ms because macro events are scalar harmonic jobs over fixed-size Vault buffers, not scene object movement or collider broadphase churn.

## Decision 003 - Remove Physical Macro-World Authority

Problem: The prompt forbids physical celestial/tide authority. Static scan found `Assets/_Project/Prefabs/Sky_System.prefab` had a live `SphereCollider` on a 25 km visual sky sphere. Main production scene/prefab scans did not find an active tide/ocean BoxCollider authority, but the sky collider was still a broadphase participant for a visual macro object.

Solution: Removed only the collider component from `Sky_System.prefab`; retained the visual mesh and camera-follow script. Celestial authority is now scalar-only through `CelestialStateDTO.GlobalTideLevel` and `EclipsePhase01`.

Rejected Alternatives: Rejected moving the sky object to a different layer because the collider would still be serialized physics authority. Rejected deleting the mesh because sky rendering is outside this task. Rejected scene-wide collider edits in gameplay prefabs because those were unrelated UI/vehicle/sargassum triggers.

Scalability potential: Low devices skip broadphase work for a 25 km visual sphere. Middle/high/ultra keep the same sky visuals while shaders consume the eclipse scalar for visual overkill.

Hardware Impact: Estimated 35 us saved on low-end physics steps where broadphase contact refreshes include large visual colliders; worst-case hitch risk removed during camera-follow/origin movement.

## Decision 004 - Vault-Owned Celestial Scalar DTOs

Problem: Tide, eclipse, and tremor state must be rollback-safe, blittable, ARM64-aligned, and readable by renderer/audio/physics without managed state or property copies.

Solution: Added `CelestialStateDTO` as `[StructLayout(LayoutKind.Explicit, Size = 32)]`: offset 0 `float GlobalTideLevel`, 4 `float EclipsePhase01`, 8 `float SeismicTremorIntensity`, 12 `uint ActiveEventFlags`, 16 `double CurrentSimulationTime`, 24-31 explicit pad bytes. Added 64B `CelestialTuningDTO`, 32B `CelestialFlowModifierDTO`, 32B `CelestialOrbitalParameterDTO`, and 64B `CelestialTelemetryEntry`. All persistent memory is requested from `GlobalDataVault` with BufferIDs 70109-70116. Hot jobs write through raw pointers and `UnsafeUtility.AsRef`.

Rejected Alternatives: Rejected properties because they become method calls and risk CS1612 copies on NativeArray values. Rejected private persistent NativeArray/NativeHashMap fields because Vault ownership is mandatory. Rejected `[StructLayout(Pack=1)]` because ARM64 unaligned loads are unacceptable.

Scalability potential: Low evaluates one harmonic and updates at about 5 Hz via quality interval. Middle evaluates two to three harmonics. High/ultra evaluates four harmonic rows and gives shaders richer scalar input without adding planetary GameObjects.

Hardware Impact: 32B state copy is one cache-friendly payload. Low-tier skips three `sincos` evaluations per solve; estimated 1-4 us per macro solve on i3/MX350-class CPU.

## Decision 005 - Mathematical Clock And Seismic Dear Lie

Problem: Real orbital mechanics, moving terrain, or Rigidbody quake forces would be expensive and nondeterministic across rollback/netcode targets.

Solution: Added `GenerateMockTimeAcceleratorsJob`, `CelestialMechanicsJob`, and renamed the fault-slot kernel to `SeismicEvaluationJob`. All Burst jobs use `CompileSynchronously = true`, `FloatMode.Deterministic`, and `FloatPrecision.Standard`. Seismic ruptures emit `SeismicShockwaveSignal` with `double3 EpicenterAUP`, magnitude, radius, and intensity. Listeners compute local `float3` deltas after subtracting AUPs.

Rejected Alternatives: Rejected GameObject moon orbits, physics terrain displacement, camera shake as a direct component reference, and `UnityEngine.Random`. Rejected `FloatMode.Fast` because this domain is rollback-relevant.

Scalability potential: Low quality collapses seismic noise and harmonic richness with continuous curves; middle keeps scalar shockwave response; high/ultra spend the saved CPU on shader/audio/camera presentation using the same signal route.

Hardware Impact: Dear Lie complexity is O(activeQuakeSlots + activeHarmonics), fixed at 16 + 1..4, instead of O(scene bodies), O(terrain vertices), or O(trigger overlaps).

## Decision 006 - Human Control Without Runtime GC

Problem: Designers need to tune lunar speed, tide amplitude, seismic frequency, and orbital rows without C# recompiles or managed parsing in the runtime path.

Solution: Replaced the old IMGUI tuner with a UI Toolkit `Macro Environment Tuner` under `#if UNITY_EDITOR`. Added a cold `orbital_parameters.csv` byte parser using Vault scratch memory and FNV-1a hashes. Orbital rows are stored in fixed Vault NativeArray slots because this project already exposes stable Vault buffer handles; a direct private NativeHashMap field would violate the Vault Law.

Rejected Alternatives: Rejected `OnGUI`, `EditorGUILayout`, `string.Split`, JSON hydration, and private NativeHashMap ownership. Rejected direct dependency on other domains for biolum/fauna tuning; eclipse events route via `SignalBus<EclipseGameplayEventPayload>`.

Scalability potential: Low devices consume the same sanitized DTO values but fewer harmonics. High/ultra can raise amplitude/frequency/rows through CSV and tuner without recompilation.

Hardware Impact: Editor-only UI has no player runtime cost. CSV parsing is cold and uses a 4096B Vault scratch buffer; runtime hot path remains allocation-free.

## Decision 007 - Black Box And Verification Boundary

Problem: A macro-state NaN or solver hitch must produce forensic data instead of a vague crash report, and claims must be separated from compile proof.

Solution: Added a 300-entry `CelestialTelemetryEntry` ring in Vault and dump path `Docs/AgentLogs/Dump_CELESTIAL_SURGEON.bin`; Decision 014 adds the agent-ID mirror `Docs/AgentLogs/Dump_SHINOBU_129.bin`. Static scans currently pass for no hot DTO properties, no `Pack=1`, no `FloatMode.Fast`, no IMGUI facade calls, no `Time.deltaTime`, no `UnityEngine.Random`, no `string.Split`, and no private NativeArray/List/HashMap declarations in the edited file. Build has not been launched yet because the user's CPU/process gate must be checked immediately before doing so.

Rejected Alternatives: Rejected chat-only reporting and optimistic verification. Rejected broad `dotnet build` before static scans and CPU/process gate.

Scalability potential: The black box cost is fixed 19.2 KB for celestial plus existing seismic telemetry. Low/middle/high/ultra share the same crash evidence and only differ in harmonic/noise work.

Hardware Impact: Telemetry write is one 64B row on slow tick/solve path. Dump is fault-path file IO only.

## Decision 008 - Compile Gate Result And Scope Boundary

Problem: Verification required a compile check, but the user's CPU/process rule forbids launching `dotnet build` under high system load or while another `dotnet`/`csc` is active. Initial CPU samples were 65.2, 78.6, and 87.5 percent, so build was blocked. A later check returned 18.3, 21.5, and 17.5 percent with no `dotnet`/`csc` process.

Solution: Launched the narrow command `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` only after the gate cleared. The build failed on unrelated Visor/Somatic dependencies: missing `UberNoirReconstructionConstantsDTO`, `MockReconstructionInputSignal`, `ReconstructionTelemetryEntry`, `UberNoirReconstructionVaultIds`, `VrComfortProfileDTO`, and `ComfortTelemetryEntry`. No compiler error was reported from `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs`.

Rejected Alternatives: Rejected fixing `HectonVisorUberPostFeature.cs` or `SomaticTunerWindow.cs`; those are outside `SHINOBU_129` domain and would be architectural sabotage without integrator authorization. Rejected repeated build attempts because the first failure is an unrelated compile wall, not a SHINOBU code error.

Scalability potential: No runtime change. Verification remains static plus blocked-build evidence until the missing Visor/Somatic contracts are restored by their owners.

Hardware Impact: Build attempt consumed about 13.6 seconds. No runtime CPU/memory impact.

## Decision 009 - Fault-Line Rupture Tightening

Problem: The initial fault-line job processed active quake slots but did not itself turn dormant fault rows into rupture events when a stress oscillator crossed threshold. That under-satisfied Task 07 and made Task 08 depend too heavily on the mock narrative spawner.

Solution: Added `TryRuptureDormantFault` inside `SeismicEvaluationJob`. Dormant fault rows keep their AUP epicenter and evaluate deterministic 1D `noise.snoise` stress. When stress crosses the continuous threshold, the job writes a finite magnitude into the fault slot and enqueues `SeismicShockwaveSignal` through `SignalBus<SeismicShockwaveSignal>.ParallelWriter`.

Rejected Alternatives: Rejected a main-thread random rupture loop because it would duplicate fault ownership and weaken Burst determinism. Rejected applying forces to terrain or bases directly; listeners receive AUP + scalar and compute their own local effect.

Scalability potential: Low quality still scans the same fixed 16 fault slots but suppresses expensive presentation noise; high/ultra keep richer shock/silt/camera response through shader/audio lanes.

Hardware Impact: Adds one 2D noise sample only for dormant slots. Worst fixed case: 16 noise samples per scheduled seismic job; still O(16), no scene queries, no collider overlaps.

## Decision 010 - Deterministic Frame Authority And Nonblocking Job Completion

Problem: Post-polish static review found `Time.frameCount` still flowing into mock camera state, narrative mock triggers, shockwave/panic/audio/debris/damage signals, celestial mechanics, eclipse payloads, and telemetry. It also found `Tick()` passing raw `deltaTime` to the seismic job and `LateFrameTick()` calling `Complete()` unconditionally.

Solution: Added `ResolveSimulationFrame()` backed by the deterministic director tick/sequence, replaced every edited-file `Time.frameCount` read, and scheduled seismic evaluation with the normalized `SimulationTickDelta`. Renamed the oscillator completion path to `SeismicEvaluation` and gated `Complete()` behind `JobHandle.IsCompleted` unless shutdown/disable forces cleanup. Legacy binary float/double hydration now uses manual little-endian integer assembly plus `math.asfloat` / `BitConverter.Int64BitsToDouble`, so the code no longer depends on platform byte order for fault records.

Rejected Alternatives: Rejected keeping Unity frame tags as "visual only" because the same payloads are rollback-adjacent macro event evidence and can leak into deterministic consumers. Rejected blindly completing every late frame because it can stall render sync. Rejected editing scheduler/core dispatcher APIs outside SHINOBU scope.

Scalability potential: Low devices avoid incidental main-thread stalls when the seismic job is still running; middle/high/ultra keep the same scalar event route and can spend saved frame time on shader/audio presentation. No binary quality switch was introduced.

Hardware Impact: Expected gain is hitch avoidance rather than steady-state ALU reduction. On i3/MX350-class hardware, skipping a premature `Complete()` can avoid a sub-frame stall whenever the seismic job spills past late-frame polling; deterministic frame IDs remove rollback audit ambiguity at 0 runtime allocation cost.

## Decision 011 - Hot Struct Initializer Noise Purge

Problem: Zero-GC scan still surfaced `new Struct { ... }` in runtime publication paths. These were value-type initializers, not heap allocations, but they weaken auditability under the project rule that gameplay code should not rely on the `new` keyword.

Solution: Replaced hot-path struct object initializers for seismic snapshots, signals, telemetry rows, Burst job structs, and scalar solve returns with `default` plus explicit field writes. Cold/editor/dump allocations remain isolated to bootstrap, UI Toolkit, file IO, and forensic dump paths.

Rejected Alternatives: Rejected leaving the code as-is with a verbal explanation because grep-based audit is part of the batch protocol. Rejected refactoring cold dump/editor creation paths because that would add churn outside frame-critical code.

Scalability potential: No math change. The benefit is deterministic audit clarity across low/middle/high/ultra tiers while retaining the same continuous quality curve.

Hardware Impact: Runtime allocation impact remains 0 B. Microsecond impact is negligible; saved cost is human verification time and false-positive avoidance in automated zero-GC scans.

## Decision 012 - Project-Standard Deterministic RNG For Mock Ruptures

Problem: The mock narrative quake injector used deterministic LCG/Hash01 sampling. It was stable, but the project mandate explicitly requires `Unity.Mathematics.Random` for deterministic gameplay RNG seeded from simulation authority.

Solution: Replaced mock narrative probability and AUP/magnitude sampling with `Unity.Mathematics.Random`. The seed is `LCG_Hash(WorldSeed ^ Sequence ^ bucket ^ ResolveSimulationFrame())`, with a non-zero fallback before `InitState`. The job still writes a fixed unmanaged `MockNarrativeTriggerSignal`.

Rejected Alternatives: Rejected `UnityEngine.Random` outright. Rejected keeping custom Hash01 sampling because it forces auditors to prove equivalence instead of seeing the mandated RNG primitive.

Scalability potential: Same result surface across low/middle/high/ultra. Quality scaling remains controlled by probability/tuning and downstream presentation, not by nondeterministic RNG.

Hardware Impact: One small value-type RNG state in a cold slow-tick mock job. No heap allocation; cost is below the fixed quake-slot evaluation budget.

## Decision 013 - Post-Polish Build Gate Refusal

Problem: After deterministic frame/RNG/noise cleanup, a compile sanity check would be useful, but the user explicitly forbids launching `dotnet build` when CPU is above 50 percent or another `dotnet`/`csc` process is active.

Solution: Checked the gate before building. CPU samples were 100/100/100 percent and an existing `dotnet` process was active (`Id=44020`). No build was launched. Verification remains static plus the earlier blocked build proof until the machine is idle and the unrelated Visor/Somatic compile wall is fixed.

Rejected Alternatives: Rejected starting another build under 100 percent CPU. Rejected killing the active `dotnet` process because it may belong to another agent. Rejected editing Visor/Somatic dependencies outside SHINOBU scope to clear the previous compile wall.

Scalability potential: No runtime change. This protects parallel-agent throughput and developer hardware during batch execution.

Hardware Impact: Prevented a second compiler workload on a saturated system. Runtime cost is 0.

## Decision 014 - Dual Black Box Dump Path

Problem: The XML task names `Docs/AgentLogs/Dump_CELESTIAL_SURGEON.bin`, while the repository-level AGENTS protocol requires `Docs/AgentLogs/Dump_[AgentID].bin`. Writing only one path would leave either task-local QA or global crash tooling without the expected artifact.

Solution: The celestial black-box dump now writes the same 300-frame `CelestialTelemetryEntry` ring to both `Dump_CELESTIAL_SURGEON.bin` and `Dump_SHINOBU_129.bin` from one serialization helper.

Rejected Alternatives: Rejected renaming the XML path because Task 16 explicitly names it. Rejected ignoring the agent-ID path because AGENTS.md is the top-level authority. Rejected dumping during healthy frames; IO remains fault-path only.

Scalability potential: No steady-state cost on any tier. Low/middle/high/ultra only pay duplicated file IO after non-finite state or solver budget breach.

Hardware Impact: Runtime hot path remains 0 us. Fault-path dump writes an extra 19.2 KB file, acceptable for forensic correctness.

## Decision 015 - Explicit Layout For Seismic Support DTOs

Problem: The primary celestial DTOs were explicit, but older seismic/mock DTOs at the top of `HectonSeismicTideDirector.cs` still used sequential layout with `Size`. That is legal, but it leaves ARM64 padding proof dependent on compiler field packing instead of visible offsets.

Solution: Converted `SeismicEventDTO`, `ShakeOffsetDTO`, `SeismicTuningDTO`, `MockCameraPosition`, `MockSiltSignal`, `SeismicBaseModuleMock`, private solve results, and tide telemetry rows to `LayoutKind.Explicit` with concrete `FieldOffset` values and padding fields. Field order and payload sizes remain unchanged for externally stored rows.

Rejected Alternatives: Rejected relying on sequential layout for hot Vault rows. Rejected changing payload size because downstream buffers and dumps already assume the existing sizes.

Scalability potential: No visual/math change. Explicit DTO proof applies equally across low/middle/high/ultra.

Hardware Impact: Runtime cost is 0. Risk reduction: eliminates accidental ARM64 layout drift and makes cache-line math auditable by grep.

## Decision 016 - Editor Telemetry Graph Reads The Ring

Problem: The tuner exposed sliders and progress bars, but Task 17 specifically requires a live graph reading the telemetry ring. Progress bars prove current state only; they do not prove the 300-frame black-box buffer is usable by designers.

Solution: Added a UI Toolkit `VisualElement` graph using `generateVisualContent` and `Painter2D`. It reads `CelestialTelemetryBuffer` directly from `GlobalDataVault` and draws tide and eclipse series from the existing unmanaged ring. No runtime code path changes.

Rejected Alternatives: Rejected IMGUI graph drawing. Rejected managed arrays or cached lists for graph samples because the telemetry ring already owns the data. Rejected adding a runtime renderer dependency.

Scalability potential: Editor-only. Low/middle/high/ultra runtime behavior is unchanged; designers can tune quality and tide/eclipses from live ring evidence.

Hardware Impact: Runtime impact is 0. Editor repaint reads at most 300 telemetry rows and draws two polylines.
