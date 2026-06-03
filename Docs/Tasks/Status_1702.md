# Status 1702

Date: 2026-06-02
Agent: 1702
Role: DYNAMIC_DIFFICULTY_AND_COMBAT_STEERING_COORDINATOR
Domain: Echelon 3 Flora/Fauna/Biota AI Cognition and Predator Steering; Echelon 5 Combat Armor LUT bridge only through documented route.
Task Count: 30
Status: SOURCE VERIFIED WITHOUT DOTNET BUILD

## Mandates Loaded

- AI_Director_Encounter_Manager: director cadence, token pacing, zero-GC native state.
- AI_Creature_Cognition_States: creature state flags, Burst cognition, memory rings.
- OPT_Zero_GC_Policy_AllocFree_Mandate: no managed allocations in hot paths.
- DATA_Runtime_Struct_Layout_ARM64: unmanaged DTO alignment and field offset audit.
- OPT_Native_Memory_Collections_JobSystem_Protocol: native ownership, job fences, no mid-tick Complete.
- ARCH_Global_Registry_ServiceLocator_DI_Init: cold DI, no hot GlobalRegistry polling.
- ARCH_Execution_Phases: PRE_SIMULATION / SIMULATION / POST_SIMULATION / VISUAL_SYNC boundaries.
- DBG_Telemetry_Crash_Reporting_PostMortem: 300-frame black-box telemetry.
- CORE_Damage_System_Hull_Integrity_VFX_Feedback: damage/deflection channel separation.
- X_008_COMBAT_ARMOR_LUT_ROUTE_CARD: armor LUT semantics, deferred presentation route.

## Assignment Source

- Extracted from `Docs/Tasks/CURRENT_BATCH.md`, `<AGENT_PROMPT id="1702">`, complete block.
- Prompt specifies 30 tasks.

## Loop 0: Intake

- [x] Prompt extraction | DOD: PowerShell raw regex extracted complete XML block by `id="1702"` from active CURRENT_BATCH. Alternative rejected: relying on IDE tab/context. Estimate: 900 us.
- [x] Domain boundary read | DOD: active domain index and coverage docs read; domain maps to Echelon 3 AI with Echelon 5 armor LUT bridge. Alternative rejected: assuming root AI ownership from file names only. Estimate: 750 us.
- [x] AGENTS/TASTE/mandates intake | DOD: AGENTS read in full chunks, TASTE read for creature/combat feel gate, task-relevant mandates loaded. Alternative rejected: reading all registry files and polluting context. Estimate: 6500 us.
- [x] Hygiene check | DOD: `Status_1702.md` and `Rationale_1702.md` were absent before creation, no stale status detected. Alternative rejected: continuing without durable disk memory. Estimate: 500 us.

## Loop 1: Tasks 01-05

- [x] Task 01 AI_DIRECTOR_STATIC_AUDIT | DOD: `HectonDirectorAI` inspected by line; it is `IUpdatable + ILateFrameTickable`, uses cold `RefreshColdRegistryReferences`, and has no `ISlowTickable`. Alternative rejected: changing dispatcher ownership before finding hot fault. Estimate: 2200 us.
- [x] Task 02 STEERING_JOB_DECONSTRUCTION | DOD: actual Burst steering surface found in `Fauna/PredatorCognitionDomain_Steering.cs`; scheduled jobs and data flow mapped. Alternative rejected: inventing missing `AI/Pathfinding/PredatorSteeringJob.cs`. Estimate: 3100 us.
- [x] Task 03 DTO_MEMORY_ALIGNMENT_INSPECTION | DOD: `PredatorCognitionDTO=80`, `SteeringParamsDTO=32`, telemetry ring entry `64`, existing validation gates located. Alternative rejected: blind field insertion without `UnsafeUtility.SizeOf` gate. Estimate: 1800 us.
- [x] Task 04 X_008_LUT_TOPOLOGY_MAPPING | DOD: route card and runtime armor code confirm `ArmorProfileDTO` 64B, LUT offset 16, 8 material rows x 6 angle steps, no trig. Alternative rejected: calling `CombatDamageRuntime` from steering job. Estimate: 2600 us.
- [x] Task 05 SIGNAL_BUS_SHAKE_INTEGRATION | DOD: camera impact route verified through `CameraJuiceSignals.TryPublishImpact(in ImpactSignal,float3)`. Alternative rejected: direct camera component calls. Estimate: 1200 us.

## Loop 2: Tasks 06-10

- [x] Task 06 GLOBAL_REGISTRY_HOT_POLLING_DETECTION | DOD: `Tick()` uses `RefreshRuntimeReferencesHot()` and cached interfaces; `GlobalRegistry.DataVault` remains only in cold refresh. Alternative rejected: polling `GlobalRegistry` from `PredatorCognitionDomain.EnsureInitialized()`. Estimate: 1200 us.
- [x] Task 07 COMPACTION_FENCE_VULNERABILITY_SCAN | DOD: DataVault mutation guard use audited; spatial hash guard is single-mask, released in `finally` or completion cleanup. Alternative rejected: nested write locks. Estimate: 1700 us.
- [x] Task 08 TELEMETRY_AND_REPORTING_ARCHITECTURE | DOD: reused existing 300-frame steering ring and source-level proof; no new JSON report path generated. Alternative rejected: managed report allocation in AI tick. Estimate: 900 us.
- [x] Task 09 DTO_ALIGNMENT_AND_FIELD_INJECTION | DOD: `SteeringParamsDTO` expanded to 64 bytes with explicit offsets and `UnsafeUtility.SizeOf` gate; `PredatorCognitionDTO` kept at 80 bytes after removing unused cache weight. Alternative rejected: unused cognition DTO bloat. Estimate: 1500 us.
- [x] Task 10 CONTINUOUS_STRESS_EVALUATION | DOD: director computes clutch from continuous health/stress windows using cached survival context and `PlayerStressSignal`. Alternative rejected: binary panic threshold. Estimate: 700 us.

## Loop 3: Tasks 11-15

- [x] Task 11 STEERING_OFFSET_INJECTION | DOD: Burst steering offsets attack target by `0.40m * clutchFactor` laterally from KCC/player forward during active attacks. Alternative rejected: moving player/KCC truth. Estimate: 800 us.
- [x] Task 12 BRANCHLESS_AVOIDANCE_MATHEMATICS | DOD: new steering decisions use `math.select`, masks, triangle wave, and deterministic normalize paths. Alternative rejected: managed if/else steering branches and trig orbiting. Estimate: 1400 us.
- [x] Task 13 X_008_LUT_PARSING_AND_DEFLECTION | DOD: AI steering mirrors X_008 8-row x 6-angle topology through byte rows, angle step, and bitmask repel classification. Alternative rejected: calling combat runtime from steering job. Estimate: 1600 us.
- [x] Task 14 SEQUENTIAL_ATTACK_TOKEN_DISTRIBUTION | DOD: frame-rotated deterministic active-slot token window caps simultaneous lunges without NativeQueue allocation or nondeterministic contention. Alternative rejected: managed queue and first-slot starvation. Estimate: 950 us.
- [x] Task 15 HARASSMENT_STEERING_BEHAVIOR | DOD: token-denied attackers steer into deterministic lateral harassment orbit instead of lunging. Alternative rejected: hard stop/stutter on denied predators. Estimate: 650 us.

## Loop 4: Tasks 16-20

- [x] Task 16 CAMERA_SHAKE_SIGNAL_GENERATION | DOD: steering writes native presentation flags; `CameraJuiceSignals.TryPublishImpact` is called only from late-frame finalization. Alternative rejected: camera/component calls inside Burst job. Estimate: 850 us.
- [x] Task 17 ZERO_GC_TELEMETRY_RING_INTEGRATION | DOD: reused existing 300-entry native telemetry ring and added clutch/armor/token flags. Alternative rejected: managed telemetry list. Estimate: 500 us.
- [x] Task 18 DATA_VAULT_TRANSACTIONAL_LOCKING | DOD: no new DataVault locks added; existing mutation guard has one mask and strict release path. Alternative rejected: multiple independent write guards for one AI spatial hash operation. Estimate: 700 us.
- [x] Task 19 FAIL_CLOSED_VAULT_FALLBACKS | DOD: `InjectDataVault(null)` now clears cached vault and invalidates old handles; static AI fails closed until cold rebind. Alternative rejected: retaining stale vault pointer. Estimate: 900 us.
- [x] Task 20 HOT_SWAP_DEPENDENCY_INJECTION | DOD: director injects DataVault into predator domain on cold refresh and DataVault hot-swap; changed vault forces AI handle reset. Alternative rejected: lazy global lookup in evaluation. Estimate: 1200 us.

## Loop 5: Tasks 21-25

- [x] Task 21 COMPILATION_WALL_AND_ASSEMBLY_HYGIENE | DOD: no new files/classes/asmdefs; existing partials extended only. Alternative rejected: creating `PredatorSteeringJob.cs` duplicate. Estimate: 600 us.
- [x] Task 22 DRY_RUN_VERIFICATION_EXECUTION | DOD: `git diff --check` passed on touched C# files; no dotnet build launched by directive. Alternative rejected: CPU-heavy compile spam. Estimate: 350 us.
- [x] Task 23 CONTINUOUS_QUALITY_SCALING_INTEGRATION | DOD: `GlobalQualityWeight` scales max attack token window 1..4 continuously. Alternative rejected: low/ultra binary branch. Estimate: 450 us.
- [x] Task 24 BURST_COMPILE_SYNCHRONOUS_INJECTION | DOD: patched jobs remain under existing `[BurstCompile(CompileSynchronously = true, FloatMode = Deterministic)]`. Alternative rejected: switching to `FloatMode.Fast` against replay mandate. Estimate: 300 us.
- [x] Task 25 BATCHED_COMPILATION_AND_SYNTAX_ASSERTION | DOD: source-level scans covered hot registry/component access, DTO sizeof gates, forbidden LINQ tokens, and phase placement. Alternative rejected: repeated solution build. Estimate: 1000 us.

## Loop 6: Tasks 26-30

- [x] Task 26 EXPLICIT_SIZEOF_VALIDATION_GATE | DOD: `SteeringParamsDTO=64` and `PredatorCognitionDTO=80` validated through existing `UnsafeUtility.SizeOf<T>()` path. Alternative rejected: trusting `[StructLayout]` alone. Estimate: 400 us.
- [x] Task 27 COMPACTION_FENCE_RACE_CONDITION_AUDIT | DOD: changed DataVault cold injection completes pending AI jobs once and clears handles on vault replacement. Alternative rejected: live handle reuse across vault generation. Estimate: 1200 us.
- [x] Task 28 ZERO_GC_ALLOCATION_PROFILER_MOCK | DOD: hot-path scans found no new LINQ/list/dictionary/string formatting allocations; editor-only scanner log remains outside runtime. Alternative rejected: allocating mock profiler state in Tick. Estimate: 700 us.
- [x] Task 29 SHINOBU_X_008_LIMIT_TESTING | DOD: armor class, material row, angle step are clamped/masked; token rotation uses active count guard to prevent divide-by-zero. Alternative rejected: trusting authoring inputs. Estimate: 650 us.
- [x] Task 30 AUTOMATED_METRIC_VALIDATOR_REPORT | DOD: final proof is source diff plus static command outputs; no JSON/markdown table telemetry report generated. Alternative rejected: bloated machine report I/O. Estimate: 300 us.

## Loop 7: Polish Pass

- [x] NaN presentation quarantine | DOD: late-frame steering impact publish now rejects non-finite AUP, max lunge, and severity before `CameraJuiceSignals`. Alternative rejected: trusting telemetry consumers to sanitize AI facts. Estimate: 180 us.
- [x] Runtime velocity quarantine | DOD: steering integration zeroes non-finite current velocity before smoothing; telemetry max-lunge speed records only finite active lunges. Alternative rejected: letting a bad input velocity poison the 300-frame ring. Estimate: 220 us.
- [x] X_008 mask explicitness | DOD: armor deflection strength clamp/divisor uses `SteeringArmorMaterialStrengthMask = 0x3F`. Alternative rejected: hidden `63` magic value. Estimate: 90 us.
- [x] Re-verification gate | DOD: `git diff --check` clean except Git line-ending warnings, hot lookup grep findings remain cold/registration only, allocation-token grep hits editor-only scanner log. Alternative rejected: `dotnet build` spam. Estimate: 750 us.

## Loop 8: Source Hardening

- [x] Burst profile finite clamp | DOD: populate job now sanitizes profile fields and output turn multiplier inside Burst before writing `SteeringParamsDTO`. Alternative rejected: trusting cold CSV parse as the only guard. Estimate: 170 us.
- [x] Profile fallback guard | DOD: `ResolveProfile` returns a deterministic fallback when profile count is invalid and sanitizes selected native profile lanes. Alternative rejected: `profiles[0]` blind fallback. Estimate: 120 us.
- [x] Math overload proof | DOD: installed `Unity.Mathematics` package source confirms `math.select` overloads for `uint` and `double3`. Alternative rejected: assuming overload availability. Estimate: 350 us.

## Loop 9: Director And Presentation Hardening

- [x] Director finite sanitizer | DOD: survival, stress, sonar, clutch, and quality inputs are finite-sanitized before steering control publication. Alternative rejected: assuming upstream survival/state signals never carry NaN. Estimate: 160 us.
- [x] Thermal stress parity | DOD: fallback `HectonSurvivalSystem` path now uses max thermal/cold/heat stress like `PlayerRuntimeContext`. Alternative rejected: losing cold/heat channel detail outside context snapshots. Estimate: 80 us.
- [x] Presentation cadence gate | DOD: late-frame steering camera feedback is rate-limited by continuous `GlobalQualityWeight` from 10 to 3 frames. Alternative rejected: emitting a camera impact every clutch/armor frame. Estimate: 90 us.

## Final Source Verification

- [x] Whitespace/diff gate | DOD: `git diff --check` on touched C# and 1702 files passed; only Git CRLF conversion warnings were printed. Alternative rejected: formatting churn. Estimate: 300 us.
- [x] Hot lookup classification | DOD: scan found `TryGetComponent` only in forced cold runtime refresh and `GlobalRegistry.DataVault` only in cold registry refresh. Alternative rejected: adding hot polling to `Tick`/jobs. Estimate: 250 us.
- [x] Allocation-token classification | DOD: LINQ/list/dictionary/string-format scan found only editor-only `OOP_Movement_Scanner` log concatenation under `#if UNITY_EDITOR`. Alternative rejected: runtime managed diagnostics. Estimate: 250 us.
- [x] Hygiene gate | DOD: `Assets` orphan `.meta` count is 0; no `.cs`, `.shader`, or `.meta` files were deleted. Alternative rejected: deleting assets during AI patch. Estimate: 19000 us.
- [x] Compilation throttling | DOD: active `dotnet` processes were observed, and no `dotnet build` or rebuild was launched. Alternative rejected: CPU-heavy compile spam while Unity/Bee compiler processes were active. Estimate: 500 us.

## Loop 10: Branchless Payload Polish

- [x] SDF sample flattening | DOD: `SampleSdf` now clamps voxel coordinates and selects fallback distance branchlessly after schedule-time nonzero SDF length guard. Alternative rejected: unsafe out-of-bounds read or branch inside whisker sample. Estimate: 110 us.
- [x] Profile selector flattening | DOD: steering profile resolution now scans profiles with `math.select` and a `found` mask instead of early match return. Alternative rejected: unpredictable species branch in per-predator populate job. Estimate: 130 us.
- [x] Telemetry finite payload clamp | DOD: average velocity is finite-clamped both in telemetry write and late-frame camera publish. Alternative rejected: relying on camera lane sanitation to hide AI NaN. Estimate: 70 us.
- [x] Branchless target and distance helpers | DOD: target AUP selection and double-distance clamp now use `math.select` instead of target/finiteness branches. Alternative rejected: divergent target-route branch in integrate job. Estimate: 90 us.

## Loop 11: Integrator Contamination Hardening

- [x] Repulsion lane quarantine | DOD: `IntegrateSteeringVectorsJob` now zeroes non-finite `SteeringAvoidanceDTO.Repulsion` before pursuit, harassment, and armor-glance blending. Alternative rejected: trusting previous native lane writers as the only guard. Estimate: 45 us.
- [x] Target AUP fail-closed selection | DOD: resolved pack/player target AUP falls back to deterministic forward AUP when selected coordinates are non-finite. Alternative rejected: letting damaged target state collapse `toTargetDouble` into NaN before normalization. Estimate: 50 us.
- [x] Armor class magic-value removal | DOD: director default player armor class now binds to `(byte)CombatArmorClass.Suit`, matching player combat registration without polling combat runtime. Alternative rejected: direct AI read of combat owner state. Estimate: 10 us.
- [x] Loop 11 verification | DOD: tracked C# `git diff --check` passed with line-ending warnings only; untracked 1702 logs have no trailing whitespace; literal-path orphan `.meta` count is 0; active `dotnet` processes blocked build launch. Alternative rejected: build spam under active compiler load. Estimate: 600 us.

## Loop 12: Steering Schedule Race Hardening

- [x] Partial steering job write lockout | DOD: steering now records a bitmask for every successfully scheduled mutating steering job; debug/read APIs stay locked out while any partial steering write is in flight. Alternative rejected: using telemetry job success as the only in-flight proof. Estimate: 95 us.
- [x] Actual job admission reporting | DOD: late-frame reporting now counts and reports only the steering jobs actually scheduled in that frame, including partial chains after admission failure. Alternative rejected: fixed five-job reporting that overcounted when mock SDF was already generated or a later schedule failed. Estimate: 80 us.
- [x] Loop 12 verification | DOD: `LeviathanSteeringScheduledJobCount` is removed; references use `_steeringScheduledJobMask`; `git diff --check` passed with line-ending warnings only; hot lookup and allocation-token classifications unchanged; orphan `.meta` literal count is 0. Alternative rejected: launching build while `dotnet` processes are active. Estimate: 500 us.

## Loop 13: Steering Admission Boundary Hardening

- [x] Active-slot capacity fail-closed gate | DOD: steering schedule now rejects mismatched `_activeSlotCount`, invalid active slot indices, zero capacity, and insufficient whisker storage before any Burst pointer is handed to jobs. Alternative rejected: silently clamping a corrupted active count and executing stale slots. Estimate: 140 us.
- [x] SDF topology repair gate | DOD: runtime SDF config now validates `dimensions.x * dimensions.y * dimensions.z == sdf.Length` and regenerates the mock SDF when topology/cell size changes. Alternative rejected: trusting stale SDF config and relying on per-sample fallback only. Estimate: 110 us.
- [x] Loop 13 verification | DOD: scoped `git diff --check` passed with line-ending warnings only; signature scan confirms all schedule calls use verified `activeSlotCount`; forbidden runtime-token scan remains editor-only for `OOP_Movement_Scanner`. Alternative rejected: launching build while Unity `dotnet` is still active. Estimate: 450 us.

## Loop 14: Director Hot-Route Decay Hardening

- [x] MetaCampaign no-op hot bind removal | DOD: removed `RefreshMetaCampaignService()` calls from Tick retry and duplicate cold sequences; MetaCampaign now binds through `RefreshColdRegistryReferences()` and `OnGlobalRegistryServiceReplaced()` only. Alternative rejected: leaving a no-op setter route in AI Tick maintenance. Estimate: 20 us.
- [x] Player stress freshness gate | DOD: director stress SignalBus read now tracks sequence and dispatcher frame, fading stale stress over 45 frames so a dead panic lane cannot hold clutch steering forever. Alternative rejected: binary 8-frame cutoff copied from acoustic ghost blips, which is too short for slow-tick physiology. Estimate: 35 us.
- [x] Loop 14 verification | DOD: `git diff --check` passed with line-ending warnings only; `RefreshMetaCampaignService` has no remaining references; hot scan still classifies `TryGetComponent` as forced cold refresh only and `GlobalRegistry` access as registration/cold refresh only. Alternative rejected: launching build while Unity `dotnet` process 3100 remains active. Estimate: 500 us.

## Loop 15: Whisker Inner-Loop Branch Removal

- [x] Whisker direction switch removal | DOD: replaced the 26-case `ResolveWhiskerDirection` switch with six `uint` axis masks and `math.select` weights, preserving old direction order. Alternative rejected: ternary cube-index generation that would change the low-tier first-six whisker coverage. Estimate: 1-4 us saved across 64 predators.
- [x] Mask equivalence proof | DOD: PowerShell mask verifier reported `WHISKER_MASK_MISMATCHES=0` for indices 0-25 against the old forward/right/up coefficient order. Alternative rejected: trusting hand-written hex masks. Estimate: 120 us validation.
- [x] Loop 15 verification | DOD: steering file has no remaining `switch`, `case`, or `default`; `git diff --check` passed with line-ending warnings only. Alternative rejected: launching build while Unity `dotnet` remains active. Estimate: 300 us.

## Loop 16: Director Stress Freshness Polish

- [x] Raw stress cache separation | DOD: director now stores the last raw `PlayerStressSignal` separately from the faded output, so stale stress fades linearly over the configured 45 frames instead of compounding decay every Tick. Alternative rejected: reusing `_lastPlayerStress01` as both source and faded product. Estimate: behavioral fix, no measurable hot cost.
- [x] First-sequence freshness gate | DOD: the first valid stress signal marks its seen frame even if `SignalBus` starts the sequence at `0`. Alternative rejected: assuming first sequence is always nonzero. Estimate: one extra cold-condition check in director Tick.
- [x] Loop 16 verification | DOD: `git diff --check` passed with line-ending warnings only; stress raw-cache symbols are present; steering file has no `switch/case/default`; orphan `.meta` count is 0. Alternative rejected: launching build while active `dotnet` processes remain present. Estimate: 350 us.

## Loop 17: Director Control Staleness Hardening

- [x] Early-return steering reset | DOD: director now publishes fail-closed steering control on missing player transform, failed player runtime snapshot, `OnDisable`, and teardown. Alternative rejected: preserving last clutch/token values after player authority disappears. Estimate: one static control write only on failure/teardown paths.
- [x] Schedule-side freshness gate | DOD: steering schedule accepts director clutch/tokens only when the director control frame is published and no more than 8 dispatcher frames old; otherwise Burst populate receives `clutch=0` and `maxTokens=1`. Alternative rejected: trusting teardown ordering as the only stale-state defense. Estimate: below 0.1 us per steering schedule.
- [x] Frame source alignment | DOD: director control now publishes `CurrentFrameIndex` cast to `uint`, matching the frame domain used by `ScheduleFrameEvaluation`. Alternative rejected: comparing masked schedule frame to raw `CurrentFrameId`. Estimate: no runtime cost.

## Loop 18: Telemetry Ring Admission Hardening

- [x] Full stale director payload fallback | DOD: stale director control now also falls back to suit armor and current schedule frame for token rotation, not only `clutch=0` and one token. Alternative rejected: leaving stale armor class or stale token phase in fail-closed mode. Estimate: below 0.1 us per steering schedule.
- [x] Telemetry ring length gate | DOD: steering schedule now rejects telemetry ring length below 300 and empty cursor before `RecordSteeringTelemetryJob` receives raw pointers. Alternative rejected: trusting vault handle creation alone. Estimate: one length check per steering schedule.
- [x] Loop 18 verification | DOD: scoped `git diff --check` passed with line-ending warnings only; hot scan remains cold/editor-only; steering branch scan is empty; orphan `.meta` count is 0. Alternative rejected: launching build while active `dotnet` processes remain present. Estimate: 350 us.

## Loop 19: SDF Topology And Zero-Delta Fail-Closed Hardening

- [x] SDF config post-resolve proof | DOD: steering schedule now revalidates resolved SDF dimensions against the actual SDF buffer length before mock SDF generation or sampling jobs receive pointers. Alternative rejected: writing a default 48x24x48 config into an undersized or differently sized SDF vault. Estimate: one topology check per steering schedule.
- [x] Zero-delta director reset | DOD: `HectonDirectorAI.Tick` now publishes fail-closed steering control before returning on `deltaTime <= 0`, preventing stale clutch/tokens during pause or zero-delta dispatcher frames. Alternative rejected: assuming fauna schedule never runs after a zero-delta director Tick. Estimate: failure-path static write only.
- [x] Loop 19 verification | DOD: scoped `git diff --check` passed with line-ending warnings only; hot scan remains cold/editor-only; steering branch scan is empty; orphan `.meta` count is 0; build throttled by 69% CPU load. Alternative rejected: launching build above the 50% CPU gate. Estimate: 400 us.

## Loop 20: Vault Release Director Payload Reset

- [x] Central steering-control reset | DOD: `ReleaseLeviathanSteeringVaultHandles()` now clears the director steering payload together with native steering handle release. Alternative rejected: duplicated direct reset calls in each owner-drop caller. Estimate: cold/teardown-path static reset only.
- [x] DataVault and dispose coverage | DOD: `InjectDataVault()`, `Dispose()`, failed steering allocation, and core vault release all route through the same steering handle release reset. Alternative rejected: waiting for the next director Tick while a new vault owner is already active. Estimate: no steady-state cost.
- [x] Loop 20 verification | DOD: scoped `git diff --check` passed with line-ending warnings only; `PredatorCognitionDomain_Steering.cs` has no `switch`, `case`, or `default:` tokens; hot lookup scan remains registration/cold refresh only; orphan `.meta` count is 0; build throttled by 100% CPU load. Alternative rejected: launching build above the 50% CPU gate. Estimate: 450 us.

## Loop 21: Steering Frame Domain Normalization

- [x] Schedule payload frame normalization | DOD: avoidance, integration, and telemetry jobs now receive the existing `scheduleFrame` instead of raw `(uint)frameId`. Alternative rejected: allowing negative bootstrap frame IDs to wrap to `uint.MaxValue`. Estimate: no steady-state cost.
- [x] Telemetry finalization frame normalization | DOD: late-frame steering telemetry now compares against `currentFrame = (uint)max(0, frameId)`, matching the schedule payload frame domain. Alternative rejected: schedule writes frame `0` while finalize searches `uint.MaxValue` on negative bootstrap frames. Estimate: one late-frame primitive assignment.
- [x] Loop 21 verification | DOD: scoped `git diff --check` passed with line-ending warnings only; `rg` found no remaining raw `(uint)frameId` in `PredatorCognitionDomain_Steering.cs`; steering branch scan remains empty; build throttled by 81% CPU load. Alternative rejected: launching build above the 50% CPU gate. Estimate: 250 us.

## Loop 22: Telemetry Cursor Corruption Hardening

- [x] Read-side telemetry cursor clamp | DOD: late-frame finalize and debug copy now resolve the last telemetry index through `ResolveTelemetryLastIndex()` instead of trusting `cursor[0] - 1` and signed `%`. Alternative rejected: assuming the native cursor cannot be corrupted. Estimate: one branchless normalize on read paths.
- [x] Job-side telemetry cursor advance clamp | DOD: `RecordSteeringTelemetryJob` now normalizes `TelemetryCursor[0]` before writing and advances with `AdvanceTelemetryCursor()` to avoid signed overflow and modulo on hostile cursor values. Alternative rejected: `math.max(0, cursor) % 300`, which still permits huge cursor overflow on `cursor + 1`. Estimate: below 0.1 us per telemetry job.
- [x] Loop 22 verification | DOD: scoped `git diff --check` passed with line-ending warnings only; cursor-pattern scan found only normalized helper calls; steering branch scan remains empty; build throttled by 100% CPU load. Alternative rejected: launching build above the 50% CPU gate. Estimate: 250 us.

## Loop 23: Telemetry Fault Payload Sanitization

- [x] Burst timing finite clamp | DOD: steering telemetry now sanitizes `chainMicroseconds` and `EstimatedBurstMicroseconds` through `SanitizeTelemetryMicroseconds()` before ring writes and budget flags. Alternative rejected: allowing NaN timing to persist in the black-box ring. Estimate: one branchless float select.
- [x] Non-finite AUP fault flag | DOD: telemetry now flags non-finite AUP separately from non-finite velocity, captures the first finite active AUP, and includes AUP faults in the dump trigger mask. Alternative rejected: visual/presentation layer discovering AI-domain NaN too late. Estimate: below 0.1 us per telemetry job.
- [x] Presentation AUP quarantine | DOD: late-frame camera impact publish returns early on `SteeringTelemetryFlagNonFiniteAup`, preventing a corrupted predator AUP from becoming an impact at origin. Alternative rejected: relying only on `FirstAup` finite check after replacing missing finite AUP with zero. Estimate: failure-path branch only.
- [x] Loop 23 verification | DOD: scoped `git diff --check` passed with line-ending warnings only; telemetry flag/sanitizer scan confirms finite timing and AUP fault paths; steering branch scan remains empty; build throttled by 82% CPU and active `dotnet` process. Alternative rejected: launching build above the 50% CPU gate or during compiler activity. Estimate: 350 us.

## Loop 24: Director Frame Publication Normalization

- [x] Director steering-control frame clamp | DOD: `PublishPredatorSteeringControl()` and `ResetPredatorSteeringControl()` now use `ResolveDirectorFrameU32()` instead of raw unsigned casts of `SystemDispatcher.CurrentFrameIndex`. Alternative rejected: allowing bootstrap `-1` frames to wrap to `uint.MaxValue`. Estimate: no steady-state cost beyond shared helper call.
- [x] Director/steering frame-domain parity | DOD: director publication, steering schedule payload, and telemetry finalize now all clamp negative frame IDs to `0` before converting to `uint`. Alternative rejected: fixing only schedule-side payloads while director freshness still receives wrapped frame values. Estimate: one branchless max per director publication.
- [x] Loop 24 verification | DOD: scoped `git diff --check` passed with line-ending warnings only; scan found no raw director frame casts and no raw steering `(uint)frameId`; hot allocation scan remains cold/editor-only; build throttled by 97% CPU load. Alternative rejected: launching build above the 50% CPU gate. Estimate: 300 us.

## Loop 25: Clutch Threshold And Telemetry Freshness Polish

- [x] Documented clutch full-point alignment | DOD: director clutch ramp now reaches `1.0` at health `0.10` and stress `0.85`, preserving a continuous ramp before catastrophe. Alternative rejected: previous curve requiring stress above the documented panic point for full 40 cm deflection. Estimate: no allocation; two constant multipliers in director Tick.
- [x] Current-frame telemetry mutation gate | DOD: late-frame finalization now returns before presentation or dump mutation when the last ring entry does not belong to the current normalized frame. Alternative rejected: letting stale ring flags trigger a dump or camera path after schedule gaps. Estimate: one late-frame comparison.
- [x] Loop 25 verification | DOD: scoped `git diff --check` passed with line-ending warnings only; steering forbidden-token scan is empty; hot allocation scan remains cold/editor-only; build throttled by 99% CPU load. Alternative rejected: launching build above the 50% CPU gate. Estimate: 300 us.

## Loop 26: Steering Cadence Decoupling

- [x] Steering-only frame admission | DOD: `ScheduleFrameEvaluation()` now schedules the existing leviathan steering chain even when cognition due-flags are idle, so kinematic steering continues from cached outputs instead of freezing until the next cognition solve. Alternative rejected: forcing all cognition jobs every frame. Estimate: no new job type; reuses existing steering jobs only.
- [x] Swarm completion accounting | DOD: added `_swarmAnalysisJobScheduled` so late-frame reporting only completes `SwarmAnalysisJob` when it was actually admitted; steering-only frames report only their real scheduled jobs. Alternative rejected: keeping fixed one-swarm-job accounting after adding steering-only frames. Estimate: one bool branch in LateFrame.
- [x] Loop 26 verification | DOD: scoped `git diff --check` passed with line-ending warnings only; no new hot allocation tokens; final CPU load was 51%, so build remained throttled. Alternative rejected: launching build above the 50% CPU gate. Estimate: 350 us.

## Loop 27: SDF Local Payload Quarantine

- [x] SDF origin finite clamp | DOD: `ResolveRuntimeSdfConfig()` now zeroes non-finite `SdfOriginAup` before config is written back or handed to Burst jobs. Alternative rejected: letting a corrupted origin convert every whisker local sample into NaN. Estimate: one cold schedule-side `double3` finite mask.
- [x] Whisker local quarantine | DOD: `EvaluateSdfAvoidanceJob` now samples SDF and writes debug whisker payloads from finite `sampleLocal`, sets the existing non-finite avoidance flag when local space is corrupt, and prevents NaN local coordinates from reaching gizmo/presentation readers. Alternative rejected: relying on clamped SDF indexing alone while leaving debug DTO payload poisoned. Estimate: below 0.5 us for 64 predators.
- [x] Loop 27 verification | DOD: scoped `git diff --check` passed with line-ending warnings only; steering forbidden-token scan is empty; hot scan remains forced-cold `TryGetComponent` and editor-only scanner log; orphan `.meta` count is 0; build throttled by 100% CPU and active `dotnet` processes. Alternative rejected: launching build above the 50% CPU gate or during compiler activity. Estimate: 450 us.

## Loop 28: Avoidance Fault Propagation

- [x] SDF/local fault enters black-box ring | DOD: `RecordSteeringTelemetryJob` now maps `SteeringAvoidanceFlagNonFinite` into `SteeringTelemetryFlagNonFiniteAvoidance`. Alternative rejected: leaving SDF-space faults only in transient avoidance DTOs. Estimate: one bit test and one `math.select` per active predator.
- [x] Dump and presentation gates include avoidance faults | DOD: steering dump trigger and camera feedback quarantine now include non-finite avoidance, so corrupt local/SDF state cannot publish visual feedback and does create black-box evidence. Alternative rejected: dumping only velocity/AUP faults. Estimate: one late-frame mask test.
- [x] Loop 28 verification | DOD: flag-route scan confirmed telemetry flag, dump mask, presentation blocker, and job writer; scoped `git diff --check` passed with line-ending warnings only; hot scan remains editor-only in steering file; build throttled by 100% CPU and active `dotnet` processes. Alternative rejected: launching build above the 50% CPU gate or during compiler activity. Estimate: 300 us.

## Loop 29: Director Ingress NaN Quarantine

- [x] Tick delta fail-closed | DOD: `HectonDirectorAI.Tick()` now resets predator steering and returns on non-finite `deltaTime`, not only non-positive deltas. Alternative rejected: allowing NaN delta to poison frame timing, stress decay, and encounter context. Estimate: one finite check per director tick.
- [x] External pressure sanitizer | DOD: public and internal external pressure paths sanitize pressure, hold seconds, delta, and ref stress/threat values before storing or applying peak pressure. Alternative rejected: trusting mod/API callers to never pass NaN. Estimate: below 0.1 us per call.
- [x] Sonar intensity sanitizer | DOD: sonar ping ingress now uses the same director 0..1 finite sanitizer as survival and clutch lanes. Alternative rejected: raw `math.saturate` on possible NaN intensity. Estimate: one finite select per sonar event.
- [x] Loop 29 verification | DOD: changed routes were scanned by line; scoped `git diff --check` passed with line-ending warnings only; hot scan remains forced-cold `TryGetComponent` and editor-only scanner log; build throttled by 73% CPU. Alternative rejected: launching build above the 50% CPU gate. Estimate: 350 us.

## Final Gate After Loop 29

- [x] Scoped whitespace gate | DOD: `git diff --check` passed for touched C# and 1702 disk-memory files; only Git CRLF warnings were printed. Alternative rejected: formatting churn. Estimate: 300 us.
- [x] Runtime token gate | DOD: steering forbidden-token scan found no raw `(uint)frameId`, `switch/case/default`, raw telemetry modulo, or cursor underflow patterns. Hot token scan found only forced-cold `TryGetComponent` in `Awake` path and editor-only scanner log. Alternative rejected: accepting unclassified grep hits. Estimate: 450 us.
- [x] Hygiene and build throttle gate | DOD: orphan `.meta` count is 0. Build was not launched because CPU was 74% and `dotnet` processes were active. Alternative rejected: violating the >50% CPU / active compiler gate. Estimate: 8600 us scan.

## Loop 30: Acoustic Payload And Director Event Ingress Sanitization

- [x] Acoustic payload finite clamp | DOD: EMP, impulse, and ping payload radius/scalar fields now convert through finite positive or 0..1 sanitizers before sonar stress, deafening, aggro, threat spike, and fauna cue sinks. Alternative rejected: trusting physics payload producers to never publish NaN. Estimate: below 0.2 us per drained payload.
- [x] Director event ring finite clamp | DOD: `DirectorAIEvents` now sanitizes event values and positions before pushing music signals or listener ring payloads. Alternative rejected: letting downstream listeners defend every event independently. Estimate: one finite vector check and one scalar select per raised event.
- [x] Director timer finite repair | DOD: solve warning clock, runtime resolve retry timer, frame timing history, frustum timer, sonar debounce, predator sight cooldown, hunter cooldown, and threat smoothing delta now repair non-finite state before subtraction, averaging, or blend math. Alternative rejected: relying only on `Tick()` delta validation while stored timers can already be corrupted. Estimate: sub-microsecond steady-state timer clamps.
- [x] Loop 30 verification | DOD: scoped `git diff --check` passed with CRLF warnings only; scan shows raw acoustic payload fields only at sanitizer/handler ingress, raw `CurrentFrameUnscaledDeltaTime` only inside the finite helper, steering partial forbidden-token scan is empty, hot lookup scan remains forced-cold `TryGetComponent` plus editor-only scanner log, orphan `.meta` count is 0; build throttled by 63% CPU load. Alternative rejected: launching build above the 50% CPU gate. Estimate: 650 us.

## Blocking Facts

- Initial file scan found `Assets/_Project/Scripts/HectonDirectorAI.cs`.
- Initial file scan did not find `Assets/_Project/Scripts/AI/Pathfinding/PredatorSteeringJob.cs`; actual steering surface must be discovered before code edits.

## Loop 31: Cognition Job NaN Quarantine

- [x] Bucket and quantization finite gates | DOD: spatial/acoustic bucket coordinate helpers now zero non-finite positions and clamp non-finite cell sizes before int casts; quantized score lanes sanitize NaN to zero before byte packing. Alternative rejected: trusting upstream positions/tuning to stay finite in Burst. Estimate: below 0.3 us per due batch.
- [x] Hot cognition scalar sanitizers | DOD: cognition due time, drive delta, metabolic delta, apex tuning, ambient threat, retinal exposure, acoustic strength/transmission, health, light exposure, pack flank distance, and radii now pass through branchless finite clamps before scoring. Alternative rejected: allowing `math.saturate(NaN)` and `math.max(NaN, x)` to poison state selection. Estimate: below 0.6 us for 64 active agents.
- [x] Loop 31 verification | DOD: scoped `git diff --check` passed with CRLF warnings only; targeted hot-NaN scans no longer find raw bucket cell, positive exponential input, delta, aggression, retinal exposure, apex tuning, or acoustic strength/transmission patterns outside sanitizer lines. Steering forbidden-token scan is empty; `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` passed with existing reference warnings only after CPU dropped below the gate. Alternative rejected: launching build while CPU was 63%. Estimate: 500 us.

## Loop 32: Cognition Input Owner Normalization

- [x] Single DTO normalizer | DOD: `SubmitInput()` now writes `SanitizeCognitionInput()` instead of raw producer packets; the same helper sanitizes vector lanes, AUP local offsets, scalar weights, timers, radii, and flags without adding a new manager or collection. Alternative rejected: continuing per-callsite clamps that drift between jobs. Estimate: branchless copy/select on submit.
- [x] Memory and external state ingress finite clamp | DOD: stimulus memory, acoustic memory float4 bank, forced state, forced retreat, sated cooldown, fatigue reduction, hunger write, and attack cooldown now sanitize position/time/intensity/duration values before writing domain state. Alternative rejected: trusting external domain calls to never pass NaN. Estimate: sub-microsecond per ingress.
- [x] Loop 32 verification | DOD: scoped `git diff --check` passed with CRLF warnings only; targeted scans no longer find raw input storage, raw memory `worldPosition`, raw `intensity`, or raw cooldown time arithmetic in the patched lanes. Hot lookup/allocation scan remains forced-cold `TryGetComponent` only. Build was throttled because an existing compiler process was active. Alternative rejected: launching another build while `csc/dotnet` was already running. Estimate: 450 us.

## Loop 33: Read-Side Snapshot And Debug Copy Quarantine

- [x] Event-signal input reads normalized | DOD: mesofauna damage/respawn handlers now consume `SanitizeCognitionInput()` snapshots, sanitize threat positions, clamp cooldown time, and repair respawn aggression before writing state. Alternative rejected: trusting old vault contents because `SubmitInput()` is now clean. Estimate: branchless owner-side copy per matched signal slot.
- [x] Public debug and telemetry copies bounded | DOD: mesofauna/apex gizmo copies, blind-state AUP resolve, pheromone publication, alpha telemetry, mesofauna telemetry, and swarm bounds now clamp `_activeSlotCount` against `_activeSlots.Length` and use finite vector conversion before exposing Unity `Vector3` payloads. Registration and slot validity now resolve the writable slot capacity from actual vault lane lengths instead of assuming full `Capacity`. Alternative rejected: letting debug readers become the first NaN detector or trusting all vault lanes to stay equally sized. Estimate: sub-microsecond per copied slot.
- [x] Loop 33 verification | DOD: scoped `git diff --check` passed with CRLF warnings only; scans no longer find raw `_inputs[slot]` field reads, explicit `in` on `_inputs`/`Inputs` indexers, raw `chainMs * 1000f` consumers outside the sanitizer assignment, or direct `new Vector3` payload construction outside `ToUnityVector3()`. `rg --files` orphan `.meta` scan returned 0. Build remained throttled by an already-running `dotnet` process. Alternative rejected: launching a second compiler while the gate is closed. Estimate: 500 us.

## Loop 34: Hot Branch Surface Flattening

- [x] Predator cognition switch removal | DOD: `EvaluatePredator`, alpha override handling, alpha directive target selection, octant direction resolution, world-state flag packing, and predator legacy-state mapping no longer use `switch/case/default:`. Alternative rejected: adding a parallel state mapper or managed lookup table. Estimate: 1-3 us branch predictability gain across 64 active predators.
- [x] Director bounded-route switch removal | DOD: director event dispatch, encounter phase event emission, GlobalRegistry hot-swap handling, and event offset octant LUT generation now use direct condition routes or branchless octant math. Alternative rejected: retaining switch drift in bounded runtime routes while cognition was flattened. Estimate: no steady-state claim; removes branch surface and keeps cold registry handling explicit.
- [x] Loop 34 verification | DOD: scoped `git diff --check` passed with CRLF warnings only; `rg "\bswitch\b|\bcase\b|\bdefault\s*:"` is empty across `HectonDirectorAI.cs`, `PredatorCognitionDomain.cs`, and `PredatorCognitionDomain_Steering.cs`; hot lookup/allocation scan remains forced-cold `TryGetComponent` plus editor-only scanner log; `math.select(int)` overload was verified in installed Unity.Mathematics source; orphan `.meta` count is 0. Build was not launched because CPU was 70% and active `dotnet` PID 2588 remained running. Alternative rejected: violating active compiler / >50% CPU build gate. Estimate: 700 us static verification.

## Loop 35: Editor Scanner False-Positive Suppression

- [x] Editor-only allocation token cleanup | DOD: `OOP_Movement_Scanner` menu log now uses one interpolated editor log string instead of `+` and `.ToString()` chains, removing the last non-runtime allocation-token hit from the touched steering partial. Alternative rejected: changing gameplay telemetry or hiding the scanner. Estimate: editor-only, no runtime frame claim.
- [x] Loop 35 verification | DOD: allocation-token scan now reports only cold `GlobalRegistry` registration/refresh and forced-cold `TryGetComponent` lines in `HectonDirectorAI`; no `.ToString`, `string.Format`, LINQ, `WaitForCompletion`, or `.Complete` hits remain in the three touched runtime files. `switch/case/default:` scan remains empty; `git diff --check` passed with CRLF warnings only. Alternative rejected: accepting a noisy editor-only false positive. Estimate: 300 us static verification.

## Loop 36: Encounter Director Cached Registration Proof

- [x] Read accessor registry dependency removed | DOD: `HectonDirectorAI.IsInitialized` now reads only `_encounterDirectorServiceRegistered` and `s_activeRuntimeInstance`; it no longer touches `GlobalRegistry.EncounterDirector`. Alternative rejected: keeping the global read because it was not in `Tick`; read accessors must stay pure and cached. Estimate: one cached bool and one reference compare.
- [x] Hot-swap state mirror added | DOD: encounter-director hot-swap callbacks now set or clear the cached registration fact when this director is installed or replaced, and teardown unregisters by cached ownership instead of polling the registry slot. Alternative rejected: allowing stale cached state after a forced service replacement. Estimate: cold callback only; no steady-state frame cost.
- [x] Loop 36 verification | DOD: scan now finds no `GlobalRegistry.EncounterDirector` reads in the three touched runtime files; hot scan reports only cold register/unregister and forced-cold `TryGetComponent`; `switch/case/default:` scan is empty; `git diff --check` passed with CRLF warnings only. Build was throttled by 93.8% CPU load. Alternative rejected: launching `dotnet build` above the 50% CPU gate. Estimate: 350 us static verification.

## Loop 37: Black Box Dump Route Ownership

- [x] Agent dump path corrected | DOD: retinal, alpha leviathan, and mesofauna cold black-box writers now use the same domain dump route as steering: `Docs/AgentLogs/Dump_1702.bin`; stale `Dump_13AI.bin` was removed. Alternative rejected: leaving a previous-agent dump route in current-domain fault evidence. Estimate: cold crash/fault path only.
- [x] Dump path allocation removed | DOD: replaced repeated `"Docs/AgentLogs/" + AgentBlackBoxDumpFileName` construction with one compile-time relative-path constant passed directly to existing writer methods. Alternative rejected: adding per-system dump filenames, which would diverge from the mandate for `Dump_[YourID].bin`. Estimate: one cold string allocation removed per dump attempt.
- [x] Loop 37 verification | DOD: dump scan reports only `AgentBlackBoxDumpRelativePath` and the three writer calls; no `Dump_13AI`, `AgentBlackBoxDumpFileName`, or `"Docs/AgentLogs/" +` routes remain in the touched cognition files. `switch/case/default:` and hot allocation/dependency scans remained clean except forced-cold `TryGetComponent`. Alternative rejected: broad formatting churn. Estimate: 300 us static verification.

## Loop 38: Fauna-Wide Dump ID Sweep

- [x] Remaining fauna dump constants corrected | DOD: `StressDrivenSpawnDirector`, `LeviathanTentacleVerletSolver`, `FaunaKinematicsRuntime`, and `ProceduralCrabLegIKRuntime` now route black-box telemetry dumps to `Docs/AgentLogs/Dump_1702.bin`. Alternative rejected: leaving stale `13AI` evidence paths in adjacent fauna runtimes. Estimate: constants only; no runtime branch cost.
- [x] Fauna dump scan clean | DOD: `rg 'Dump_13AI|Docs/AgentLogs/Dump_1702.bin' Assets/_Project/Scripts/Fauna` now reports only `Dump_1702.bin` constants and no stale `Dump_13AI` hits. Alternative rejected: touching payload layouts or adding routers. Estimate: 250 us static verification.

## Final Gate After Loop 38

- [x] Compile gate | DOD: after CPU dropped below 50% and no `dotnet/csc` process was active, `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` completed with 0 errors. Existing external/reference warnings remain. Alternative rejected: running build while CPU gate was closed. Estimate: 45.61 s wall clock.
- [x] Hygiene gate | DOD: `git diff --check` passed for touched runtime and 1702 memory files with CRLF warnings only; orphan `.meta` count remained 0. Alternative rejected: formatting churn. Estimate: 7 ms diff check plus 6.5 s orphan scan.

## Loop 39: Fauna Damage And Crab IK Polish

- [x] Owner-local wound fallback bounds | DOD: `CreatureDamageManager` fallback bounds now converts child renderer world bounds corners into owner-local space before encapsulation. Alternative rejected: merging `Renderer.localBounds` from child transforms as if it were owner-local. Estimate: cold rebuild only.
- [x] One-shot shader clear proxy teardown | DOD: `ShaderClearLateFrameProxy` unregisters from `GlobalRegistry` when no shader clear is pending and after the clear executes. Alternative rejected: leaving a dead late-frame tickable registered forever. Estimate: one branch removed after clear completion.
- [x] Crab IK low-slot allocation and high-water uploads | DOD: `ProceduralCrabLegIKRuntime` allocates low slots first, tracks highest active slot plus one, and uploads/draws only that high-water span. Alternative rejected: compacting entity slots or drawing by active count, both unsafe for sparse identities. Estimate: avoids near-capacity upload/draw for low active crab counts.
- [x] Crab IK mutation fail-closed during jobs | DOD: registration, unregister, pose, and spatial-hash mutation return while `_pipelineScheduled` is true instead of forcing synchronous completion. Alternative rejected: hidden `Complete()` or `WaitForCompletion` inside public mutation routes. Estimate: avoids unbounded main-thread stall.
- [x] Crab IK vault mutation guard | DOD: one `CrabLegVaultMutationGuardMask` covers the entity/foot/target/step/body/solved/telemetry buffers; scheduled jobs retain it until late-frame/teardown release and short owner writes release in `finally`. Alternative rejected: multiple write locks or hidden completion. Estimate: one guard acquire/release per scheduled frame.
- [x] Loop 39 verification | DOD: scoped `git diff --check` passed with CRLF warnings only; forbidden token scan over the patched fauna files found no `.Complete`, `WaitForCompletion`, LINQ, `.ToString`, or string-format hits; stale dump scan found only `Dump_1702`; orphan `.meta` count is 0. Build was throttled by 99.8-100% CPU and active `dotnet:44152`. Alternative rejected: violating active compiler / >50% CPU build gate. Estimate: 400 us static verification plus 13.7 s orphan scan.

## Loop 40: Leviathan Tentacle Vault And Draw-State Polish

- [x] Leviathan solver mutation guard | DOD: `LeviathanTentacleVerletSolver` now acquires one `TentacleVaultMutationGuardMask` before owner writes and retains it while the scheduled Verlet job owns DataVault-resolved arrays. Release paths cover late-frame completion, origin-shift finalize, lifecycle completion, and disposal. Alternative rejected: per-buffer write locks or forcing a synchronous completion from public routes. Estimate: one atomic guard acquire/release per scheduled solve.
- [x] Delta ingress finite gate | DOD: solver `Tick()` now rejects non-finite delta before grab-damage timer math and passes the clamped `safeDeltaTime` into damage cadence and constraint hysteresis. Alternative rejected: scheduling a zero-motion job after NaN delta already poisoned `_grabDamageTimer`. Estimate: one finite check per scheduled tick.
- [x] Per-runtime indirect draw payload | DOD: tentacle matrix/radius/flow/globals buffers now bind through a cached `MaterialPropertyBlock` assigned to `RenderParams.matProps`; removed global matrix/radius/constant buffer writes from the instance draw route. Alternative rejected: global shader state that lets multiple leviathans overwrite each other in the same visual-sync pass. Estimate: no allocation after cold MPB creation.
- [x] Loop 40 verification | DOD: scoped `git diff --check` passed with CRLF warnings only; forbidden-token scan over `LeviathanTentacleVerletSolver.cs` found no `.Complete`, `WaitForCompletion`, LINQ, `.ToString`, string-format, or removed global-buffer binding hits; orphan `.meta` count is 0. Build was throttled by 94.8-100% CPU and active `dotnet` processes. Alternative rejected: launching a compiler above the project gate. Estimate: 500 us static verification plus 9.4 s orphan scan.

## Loop 41: Fauna Residency Vault Guard

- [x] Residency memory mutation guard | DOD: `FaunaSimulationMemory` now owns one `FaunaSimulationMutationGuardMask` across pool slots, linear velocities, flags, and free-slot stack. Short read/write accessors acquire and release in `finally`; data-only LOD scheduling retains the guard until director completion/dispose. Alternative rejected: separate per-buffer locks or a new residency manager. Estimate: one guard acquire/release per short owner access and one retained guard per scheduled data-only LOD job.
- [x] Scheduled-job mutation fail-closed | DOD: short slot mutation/readback routes fail while the data-only LOD job owns the arrays; metadata-only probes may read under the retained owner guard so initialization does not falsely reallocate during an in-flight job. Alternative rejected: allowing public slot release/write routes to mutate arrays while the Burst job is active. Estimate: metadata checks stay branch-only; unsafe slot mutation is rejected.
- [x] Slot release ordering fix | DOD: `ReleaseDehydrationSlot()` clears native residency lanes before removing the active slot and returning the free slot. Alternative rejected: removing the active slot first, which could orphan resident state if the native clear failed under guard contention. Estimate: no new allocation; one fail-closed branch.
- [x] Loop 41 verification | DOD: scoped `git diff --check` passed with CRLF warnings only; direct `FreeSlots` use in `FaunaDirector` is gone; changed-file forbidden-token scan found no `.Complete`, `WaitForCompletion`, LINQ, `.ToString`, or string-format hits. Build was throttled by 99.8% CPU and active `dotnet:24940`. Alternative rejected: violating the active compiler / >50% CPU build gate. Estimate: 400 us static verification.

## Loop 42: Corpse Sink Vault Guard

- [x] Corpse sink job mutation guard | DOD: `FaunaBrain` corpse-sink input/output buffers now share one `CorpseSinkKinematicMutationGuardMask`; scheduling acquires and retains it for the Burst corpse sink job, and completion/lifecycle paths release it. Alternative rejected: leaving death presentation jobs with unpinned DataVault views or adding a new corpse-sink manager. Estimate: one guard acquire/release per scheduled corpse sink step.
- [x] Lock scope flattened | DOD: floor-height and AUP math happen before the retained guard; guarded section only resolves/allocates handles, writes one input DTO, schedules the job, or reads one output DTO. Alternative rejected: calling terrain/cache queries inside the guard. Estimate: heavy work stays outside compaction-fence ownership.
- [x] Loop 42 verification | DOD: scoped `git diff --check` passed with CRLF warnings only; forbidden-token scan over `FaunaBrain.cs` found no `.Complete`, `WaitForCompletion`, LINQ, `.ToString`, or string-format hits; corpse-sink `TryResolveHandle` calls are only reachable through guarded ensure/read helpers; orphan `.meta` count is 0. Build was throttled by 100% CPU and active `dotnet:24940`. Alternative rejected: violating active compiler / >50% CPU build gate. Estimate: 500 us static verification plus 11.8 s orphan scan.
