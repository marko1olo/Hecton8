# Rationale 1424 - QA Watchdog Bot

Date: 2026-05-28
Status: PENDING VERIFICATION

## Decision 000 - Domain Boundary

Problem: The assignment demands runtime verification but forbids modifying production systems to make tests pass.
Solution: Keep implementation in `Assets/_Project/Scripts/QA/QA_WatchdogBot.cs` and dev/editor guards; use cached/reflection bridges where production APIs are not stable.
Rejected Alternatives: Direct changes to physics, rendering, DataVault, bootstrap, or player controllers would contaminate the test and violate domain ownership.
Scalability potential: Low tier records essential metrics and avoids expensive observer work; middle/high/ultra can record richer telemetry only in terminal exports or lower-cadence bridges.
Hardware Impact: On i3/MX350 the bot must stay below QA/debug overhead targets; static plan budgets hot observation under 50 microseconds, pending profiler proof.

## Decision 001 - Mandate Selection

Problem: QA Watchdog crosses profiling, memory, bootstrap, AUP, and kinematic domains.
Solution: Apply mandates for zero GC, performance budgets, postmortem telemetry, struct layout, bootstrap sequencing, execution phases, AUP precision, and physics determinism.
Rejected Alternatives: Reading all registry files would waste time and increase context noise; ignoring registry would violate batch protocol.
Scalability potential: Metric cadence and export payload can scale by continuous `GlobalQualityWeight` without changing truth ownership.
Hardware Impact: Low tier keeps per-frame collection minimal; high/ultra can spend terminal export/detail budget after gameplay truth stops.

## Decision 002 - Profiler Counter Route

Problem: The bot must prove frame-time, GC allocation, and VRAM without injecting observer garbage.
Solution: Start `ProfilerRecorder` objects once on cold path and sample `LastValue` into packed structs in `LateFrameTick`; source ledger records official counter names from Unity's profiler counter reference.
Rejected Alternatives: `Time.deltaTime` plus Unity Profiler UI screenshots do not capture GC alloc or VRAM and cannot generate deterministic CSV. `ProfilerRecorderHandle.GetAvailable` was rejected for this bot because direct counter names already exist locally and handle enumeration allocates cold discovery noise.
Scalability potential: Low keeps five counters; middle/high/ultra can add terminal-only correlation columns without changing hot record shape or gameplay authority.
Hardware Impact: i3/MX350 expected read cost below 18 us before vault write; zero managed allocation in hot sampling.

## Decision 003 - Scene Route Mismatch

Problem: Assignment demands menu-to-open-world, but source shows `MainMenuController` new game currently routes to `01_ORBIT`.
Solution: Resolve `MainMenuController` through a preallocated scene-root traversal, invoke `StartGame(string.Empty)` directly to exercise menu code, then fall back to `GlobalRegistry.Scene.LoadScene("02_HECTON_WORLD")` if the active scene becomes orbit or the world route times out.
Rejected Alternatives: Editing `MainMenuController.newGameTargetSceneName` would mutate production menu ownership; direct `SceneManager.LoadScene` first would bypass the system under audit; `Object.FindObjectsByType<MonoBehaviour>` and reflection were removed because they allocate/overwork the observer route.
Scalability potential: Same route on all tiers; higher tiers only retain richer terminal evidence.
Hardware Impact: Menu traversal and fallback scene request are cold-only; hot cost is 0 us after simulation starts.

## Decision 004 - Kinematic Injection Without PhysX Probes

Problem: Ten-kilometer traversal needs obstacle survival but watchdog cannot own physics scene queries.
Solution: Publish deterministic input overrides and use KCC velocity freshness/stuck feedback for lateral/vertical triangle-wave recovery.
Rejected Alternatives: Per-frame raycasts and Rigidbody nudges would contaminate physics measurements and risk hidden allocations/solver work.
Scalability potential: Low uses small lateral perturbation; middle/high/ultra increase route variety through continuous `HomeostasisBrain.GlobalQualityWeight` without altering truth ownership.
Hardware Impact: i3/MX350 avoids raycast cost; estimated 5-9 us for input publication and scalar state.

## Decision 005 - Metric Storage

Problem: The prompt demands fixed-size telemetry and black-box state without persistent MonoBehaviour-owned native arrays.
Solution: Use GlobalDataVault buffers `74240/74241` with fixed record sizes; keep preallocated managed arrays only as bootstrap fallback if the vault is absent.
Rejected Alternatives: Adding new global enum values would widen core API churn; persistent `NativeArray` fields in the MonoBehaviour would violate project memory rules.
Scalability potential: Low records 36k compact frames plus 300 black-box entries; higher tiers spend saved cycles on terminal CSV density instead of hot heap growth.
Hardware Impact: Vault write expected 12-35 us per frame on low silicon; managed fallback remains allocation-free after cold setup.

## Decision 006 - Reporting Boundary

Problem: Reports are required, but file I/O in hot path would falsify the measurements.
Solution: CSV, metadata JSON, and binary dump are written only after completion/failure; hot path only queues terminal export.
Rejected Alternatives: Streaming CSV every frame was rejected because `StreamWriter` and formatting would allocate and distort GC proof.
Scalability potential: Low emits essential rows; middle/high/ultra can emit more terminal metadata after simulation stops.
Hardware Impact: 0 us hot reporting cost; terminal export cost is outside gameplay frame truth.

## Decision 007 - Profiler Namespace Hygiene

Problem: `QA_WatchdogBot.cs` uses `ProfilerRecorder.UnitType`, which requires the profiling core assembly in this project layout.
Solution: Add `Unity.Profiling.Core` to `Assets/_Project/Scripts/QA/Hecton8.QA.asmdef`, scoped only to the QA assembly.
Rejected Alternatives: Moving the bot into Core would violate domain boundaries; hard-assuming all counter units would make frame-time and VRAM conversion fragile.
Scalability potential: No runtime truth change. Low/high/ultra all use the same profiler route.
Hardware Impact: 0 us hot impact; assembly reference is compile-time only.

## Decision 008 - Visual Hot-Path Purity

Problem: Value-type constructors such as `new Vector2` do not allocate, but the prompt demands manual absence of `new` syntax in metric extraction.
Solution: Replace hot value constructors and struct initializers with field assignment from `default`.
Rejected Alternatives: Relying on CLR stack allocation semantics would pass technically but fail the mandated visual audit.
Scalability potential: Same behavior on all tiers.
Hardware Impact: No measurable runtime delta; audit clarity improves.

## Decision 009 - p95 Enforcement

Problem: QUALITY_GATES requires `<=16.67ms p95`, while the prompt also requires 25ms x3 spike failure.
Solution: Maintain a fixed 2048-frame bucket ring and fail during simulation when rolling p95 exceeds 16.67ms or 25ms persists for three samples.
Rejected Alternatives: Sorting frame records every frame would allocate or consume excessive CPU; terminal-only p95 would detect too late.
Scalability potential: Low uses 1ms buckets; high/ultra may add finer terminal charts without changing hot record layout.
Hardware Impact: Ring update is under 2 us; cold p95 scan is 128 buckets once per second.

## Decision 010 - DataVault Stress API Reality

Problem: The prompt names `TryRunLiveCompactionSlice`, but the repository's `IDataVault` exposes `RequestEditorForceDefragmentation` and `FrostTickDefrag` instead.
Solution: Use the existing explicit PRE_SIMULATION defrag route with maximum stress scalar and active lock mask every 60 seconds.
Rejected Alternatives: Inventing a missing API would create compile failure; reflection into private relocation methods would bypass ownership gates.
Scalability potential: Low/middle/high/ultra all use the same cadence by default; future quality weighting can reduce stress cadence only for non-watchdog modes, not for this proof run.
Hardware Impact: 0 us per-frame cost; once-per-60s stress may be heavy by design because it is the system under test.

## Decision 011 - Sentinel Hook

Problem: `NativeMemorySentinel` is an unsafe Core class and the prompt names `ValidateZeroLeaks`, which may not exist in this codebase.
Solution: Terminal reflection calls `ValidateZeroLeaks` if present; otherwise it calls the existing `AssertNoAllocationsAfterServiceShutdown("QA_WATCHDOG_BOT_1424")`.
Rejected Alternatives: Adding a new Core sentinel method would violate QA domain; direct unsafe reference would require widening the QA asmdef unsafe surface.
Scalability potential: Same terminal assertion on all tiers.
Hardware Impact: 0 us hot cost; terminal-only reflection allocation is outside measured gameplay.

## Decision 012 - Dry Run Simulation

Problem: A 10km autonomous swim can fail silently if the bot fakes distance, overflows metric storage, or gets stuck.
Solution: Distance only advances from fresh KCC velocity; no velocity for 30 seconds fails the run; metric and black-box buffers are circular; stuck state increases deterministic lateral/vertical perturbation.
Rejected Alternatives: Advancing distance on intended input would generate false success; unbounded metric storage would allocate or overflow.
Scalability potential: Low uses deterministic triangle-wave recovery; high/ultra can increase terminal evidence density, not route authority.
Hardware Impact: i3/MX350 keeps movement logic under 10 us; no physics raycast load added.

Dry Run Trace:
Frame 1000: bot is in Simulation only after `02_HECTON_WORLD` is active; input override publishes forward/lateral/vertical intent; metric write index wraps by modulo.
Frame 5000: if voxel streaming raises frame time to 18ms, p95 ring records the bucket and CSV later shows the pressure; the run does not fail unless rolling p95 exceeds 16.67ms or the 25ms x3 rule trips.
Frame 5001: 60-second defrag cadence may call `FrostTickDefrag`; if DataVault lock boundaries are broken, the black-box ring captures the last 300 frames before terminal dump.
Overflow case: after 36,000 samples, the metric cursor overwrites oldest rows; no allocation or bounds growth occurs.

## Decision 013 - Build Contention

Problem: Task 15 authorizes a build only if host CPU is below 50 percent and no compiler process is active.
Solution: Sampled CPU at 100 percent and found active `dotnet` process `57828`; build marked `[BLOCKED BY CONTENTION]`.
Rejected Alternatives: Launching another build would violate the explicit host-protection rule and contaminate sibling agent throughput.
Scalability potential: Static validation continues; build can be reattempted later when contention clears without changing code.
Hardware Impact: Avoided additional compiler load on already saturated CPU.

## Decision 014 - Mock GC Fuzzer

Problem: The watchdog must prove the GC alarm can catch contamination, but normal runs must remain sterile.
Solution: Add a separate editor-only fuzzer component armed by menu item; it allocates `new byte[1024]` in `Update` only when explicitly enabled.
Rejected Alternatives: Baking allocation into the watchdog would invalidate every run; auto-arming from domain load would contaminate sibling tests.
Scalability potential: Same hostile fixture on all hardware tiers.
Hardware Impact: Deliberately harmful only in contaminated test mode; not part of standard watchdog runtime.

## Decision 015 - CSV Serialization

Problem: CTO dashboard needs graphable evidence, but serialization is allocation-heavy.
Solution: Serialize only after completion/failure with `StreamWriter`, iterating fixed slots from DataVault or fallback arrays.
Rejected Alternatives: Per-second live CSV writes were rejected because file I/O and formatting can allocate and distort the metric under observation.
Scalability potential: Low emits compact columns; high/ultra can add terminal-only correlation fields later.
Hardware Impact: 0 us hot path; terminal write cost outside simulation measurement.

## Decision 016 - Hot Path Static Audit

Problem: Without a PlayMode run, the only available proof is source-level sterility.
Solution: Inspected `LateFrameTick`, `SampleMetricHot`, `WriteMetricHot`, `WriteBlackBoxHot`, and `EvaluateMetricHot` lines 738-1006; no `new`, `ToString`, reflection, scene search, `StreamWriter`, `BinaryWriter`, `StringBuilder`, or `Debug.Log` exists in the metric loop. Expanded scan over lines 618-1037 also returned zero hits for the mandated hot-path text filters.
Rejected Alternatives: Claiming runtime zero-GC without executing PlayMode would be fake reporting.
Scalability potential: Same sterile observer loop on all tiers.
Hardware Impact: Static audit estimates hot observer cost at 30-70 us with vault locking on i3/MX350; profiler proof remains pending.

## Decision 017 - Architecture Validation Assertion

Problem: Static reports from previous agents are not runtime proof.
Solution: Static analysis is a theory; PlayMode endurance is reality. By forcing the engine through a 10km autonomous swim while continuously triggering defragmentation and strictly asserting a zero-GC, 16.67ms boundary, this Watchdog mathematically proves whether the massive architectural refactoring of Batch 13 and 14 has actually yielded a stable, performant game, or merely a collection of green compilation logs.
Rejected Alternatives: Treating compile success, static scans, or chat reports as equivalent to a 10km PlayMode endurance run is fake evidence.
Scalability potential: Low hardware gets the same truth test with compact telemetry; middle/high/ultra can preserve more terminal evidence and visual-overkill correlation without changing truth ownership.
Hardware Impact: The watchdog is designed to spend under 0.1 ms per observed frame on i3/MX350; actual proof remains pending because host contention blocked build/PlayMode execution.

## Decision 018 - Metadata Hash Report

Problem: The CTO needs a machine-readable proof artifact showing exactly what harness was deployed.
Solution: `Docs/Reports/QA_WATCHDOG_METADATA_1424.json` now records configuration, thresholds, fuzzer fixture status, build contention, and SHA-256 hashes of modified QA files.
Rejected Alternatives: Human-only summary in chat or un-hashed artifact list.
Scalability potential: Same metadata schema on all tiers; future runtime runs can overwrite status fields with measured results.
Hardware Impact: 0 us hot path; metadata is terminal/static evidence only.

## Decision 019 - DataVault Writer Lock Integrity Re-Audit

Problem: A successful `TryAcquireWriteLock` in `WriteMetricHot` or `WriteBlackBoxHot` could skip `ReleaseWriteLock` when the returned buffer was invalid, the slot was outside capacity, or an exception occurred before the explicit release line.
Solution: Split acquisition into `bool lockAcquired`, assign the returned `NativeArray<T>` inside `try`, write only after validation, and release inside `finally` when `lockAcquired` is true.
Rejected Alternatives: Keeping the chained conditional was too fragile for relocation/deadlock safety. Adding a wrapper class was rejected because it would allocate or widen API surface.
Scalability potential: Low/middle/high/ultra all use the same lock law; quality scaling must never affect ownership or release semantics.
Hardware Impact: Adds one primitive flag and one branch per DataVault write; expected under 1 us on i3/MX350 and prevents catastrophic vault relocation stalls.

## Decision 020 - Menu Automation Without Array Search

Problem: The initial menu route used `Object.FindObjectsByType<MonoBehaviour>` and reflection from a tick-driven state; this could allocate a managed array before simulation and contaminate watchdog evidence.
Solution: Reference `MainMenuController` directly, cache it, traverse the active scene roots with a preallocated `List<GameObject>` scratch, recurse through `Transform` children, and call `StartGame(string.Empty)` directly.
Rejected Alternatives: Per-tick MonoBehaviour array search, `GetComponentInChildren<T>()` array-prone shortcuts, and reflection `MethodInfo.Invoke` were rejected as unnecessary observer work.
Scalability potential: Same deterministic route on all tiers; high-tier does not get extra menu search cost because route quality is not visual fidelity.
Hardware Impact: Removes scene-wide managed array allocation and reflection invoke cost from the route; expected savings are workload-dependent, with worst cases in large menu hierarchies.

## Decision 021 - Continuous Quality Weight Repair

Problem: `globalQualityWeight` was only serialized to metadata; it did not consume the project authority `HomeostasisBrain.GlobalQualityWeight` and did not scale runtime behavior.
Solution: Cache `HomeostasisBrain.GlobalQualityWeight` through `ResolveGlobalQualityWeight01`, fallback only on non-finite values, and drive swim lateral/vertical perturbation amplitudes with continuous `math.lerp` ranges.
Rejected Alternatives: Binary `isLowEnd` branches and quality-dependent truth thresholds were rejected. Frame-time, GC, VRAM, DTO layout, DataVault IDs, and distance authority remain invariant.
Scalability potential: Low uses restrained triangle-wave route variation; middle/high/ultra progressively increase route richness and stuck-escape amplitude without changing pass/fail truth.
Hardware Impact: Four lerps plus scalar stores in the input publish path, 0 B GC. On i3/MX350 this is cheaper than PhysX raycast avoidance; on high-tier it spends saved physics cost on more dynamic traversal evidence.

## Decision 022 - Final Build Gate Honesty

Problem: The user requested final proof, but the host-protection rule forbids build when CPU exceeds 50 percent or another dotnet/compiler process is active.
Solution: Sampled `CPU_LOAD_PERCENT=99` with active `csc` process `43496` and active `dotnet` process `35512`; build and PlayMode were not launched.
Rejected Alternatives: Running `dotnet build` under 99 percent CPU with active compiler/dotnet processes would violate the batch protocol and could disrupt sibling agents.
Scalability potential: Static proof is preserved; runtime proof can be attempted later with the same artifacts when host load permits.
Hardware Impact: Avoided compiler load on a saturated host; no compile-success or runtime-zero-GC claim is made.

## Decision 023 - APEX Follow-Up Evidence Hygiene

Problem: After reflection was removed from the main-menu route, the black-box field still used the stale name `MenuReflectionAttempts`. The route scratch list also had capacity 64, which could grow if the menu scene had more roots than expected.
Solution: Rename the field/counter to `MenuResolveAttempts` while preserving `FieldOffset(48)` and the 64-byte black-box layout; increase `_sceneRootScratch` to 512 and annotate all watchdog-owned cold allocations.
Rejected Alternatives: Leaving the stale name would poison proof artifacts. Replacing scene-root traversal with Unity array search was rejected because it reintroduces managed allocations. Creating a global menu registry route was rejected because QA must not widen production authority for a test harness.
Scalability potential: Low tier avoids scratch-list growth on larger menus; middle/high/ultra keep the same route truth and spend no extra simulation-frame work. Quality scaling still uses the continuous `HomeostasisBrain.GlobalQualityWeight` scalar only for swim motion richness.
Hardware Impact: Cold memory increases by bounded scratch capacity only; it avoids unbounded list growth and terminal reflection argument allocation. Hot-path scan after the patch remains 0 hits for reference-style `new`, formatting, `.ToString`, `foreach`, LINQ, scene search, file I/O, and `Debug.Log`.

## Decision 024 - Final Build Gate Re-Sample

Problem: A final compile check is requested, but the host-protection rule forbids launching builds when CPU load exceeds 50 percent.
Solution: Re-sampled build gate after the APEX follow-up: `CPU_LOAD_PERCENT=68`, active compiler process `none`. No build was launched because CPU still exceeded the threshold.
Rejected Alternatives: Running `dotnet build` at 68 percent CPU would violate `Compilation Resource Throttling` even without an active compiler process.
Scalability potential: Static artifacts remain valid for later verification. Runtime truth still requires Unity compile/import/PlayMode when host load permits.
Hardware Impact: Avoided adding compiler load to a busy machine. Compile success, PlayMode endurance, fuzzer alarm success, and runtime zero-GC remain pending.

## Decision 025 - Route and Metric Hot-Path Hygiene

Problem: The watchdog still had non-fatal but dirty observer patterns: scene route checks could read `Scene.name`, frame-time fallback used `UnityEngine.Time`, the sampler duplicated KCC/player-state freshness reads, and graphics-driver fallback was evaluated even when the recorder was valid.
Solution: Route checks now compare build indices from `EditorBuildSettings`; frame fallback uses the already-delivered dispatcher `FastTick` delta; KCC/player freshness flags are cached from `IntegrateDistanceHot`; graphics fallback is called only when the profiler recorder is invalid.
Rejected Alternatives: Keeping string scene reads was rejected because it weakens hot-path proof. Per-frame `Time.unscaledDeltaTime` fallback was rejected because the dispatcher already owns frame delta. Duplicated context reads were rejected because a QA observer should not amplify global-service access.
Scalability potential: Low devices avoid redundant observer work; middle/high/ultra keep identical truth checks and spend quality scalar only on route richness through `HomeostasisBrain.GlobalQualityWeight`.
Hardware Impact: Saves small but real hot-path work: one scene string route risk removed, one Unity time property fallback removed, two duplicate freshness reads removed from the sampler, and one graphics fallback call avoided when recorder data is valid.

## Decision 026 - Final Build Gate After Route/Metric Patch

Problem: The proof mandate asks for final compilation, but the project host rule forbids build launch when CPU exceeds 50 percent or compiler/dotnet work is active.
Solution: Re-sampled after artifact updates: `CPU_LOAD_PERCENT=100`, active `dotnet pid 51008`. Build launch count stayed `0`.
Rejected Alternatives: Running `dotnet build` under 100 percent CPU with an active dotnet process would violate the explicit compilation throttling rule and disrupt concurrent agents.
Scalability potential: No runtime system change. Static artifacts remain ready for a later compile/PlayMode pass when contention clears.
Hardware Impact: Avoided adding compiler load to a saturated machine. Compile success and PlayMode endurance remain unclaimed.

## Decision 027 - Endurance CSV Writer Flattening

Problem: `QAEnduranceCsvWriter` previously owned a background writer thread, async file stream, monitor lock, and wake event. That architecture was overbuilt for a QA proof harness and created a false parallel I/O surface inside the endurance domain.
Solution: Convert the writer to a bounded in-memory record ring with `TryEnqueue(in QAEnduranceCsvRecord)` and terminal-only `FlushCold()`. The old helper `ClearRecordWindow` was removed after the thread path was deleted. `StopRun(true, ...)` is the only flush route.
Rejected Alternatives: Keeping live background I/O was rejected because it smuggles file work outside the simulation/presentation phase model. Streaming every CSV row was rejected because the current user protocol explicitly rejects bloated runtime I/O proof artifacts.
Scalability potential: Low hardware pays only fixed array writes during the run. Middle/high/ultra can retain denser terminal CSV rows through capacity settings without adding a writer thread or changing gameplay truth.
Hardware Impact: Removes thread wake, lock contention, async `WriteAsync().GetAwaiter().GetResult()`, and live `FileStream` ownership from the run. Hot enqueue remains primitive array copy and cursor math; terminal flush cost is outside gameplay truth.

## Decision 028 - Source-Only Build Gate Honesty

Problem: The host gate briefly became available (`CPU_LOAD_PERCENT=39`, compiler processes `none`), but a search of generated `.csproj` files found no references to `QA_WatchdogBot`, `QAEnduranceWatchdogBot`, or `QAEnduranceBatchRunner`. A later re-sample after log write returned `CPU_LOAD_PERCENT=97` with active `dotnet pid 44748`.
Solution: Do not run a misleading `dotnet build`. The only meaningful compile proof for these QA asmdef scripts is Unity import/console or a regenerated Unity project file, which is not available through current tools. After the re-sample, build launch is also forbidden by CPU/process policy.
Rejected Alternatives: Running a build that does not include the touched files would create false evidence. Claiming Roslyn AST proof was also rejected because the available SDK/Roslyn path is not currently validated in this shell.
Scalability potential: No runtime system change. Avoided unnecessary compiler work while preserving a clean handoff for Unity import/PlayMode.
Hardware Impact: `dotnet build` launch count remains `0`; no orphan compiler processes were created. Verification class remains `STATIC_SOURCE`, not `CLI_COMPILE`, `UNITY_CONSOLE`, `PLAYMODE`, or `PROFILER`.

## Decision 029 - Endurance Save Phase Deferral

Problem: `QAEnduranceWatchdogBot.FastTick` could start `SaveAsync(save)` from the simulation tick after a distance threshold. The call is rare, but it still enters persistence work from the wrong phase and weakens the APEX dependency proof.
Solution: `RequestSaveIfAvailable` now only advances the next save distance, validates the cached save service, records the event, and stores `_queuedSaveService` plus `_saveRequestQueued`. `LateFrameTick` consumes the queued reference and starts `SaveAsync(save)` after simulation has settled.
Rejected Alternatives: Leaving direct async save kickoff in `FastTick` was rejected as a phase violation. Switching to `IAsyncPersistenceService.TryRequestSave(byte slotIndex)` was rejected because the endurance harness intentionally uses the dedicated `qa_endurance_10km` slot name, while the async queue contract is manual-slot-index based.
Scalability potential: Low hardware avoids persistence kickoff inside simulation cadence. Middle/high/ultra keep identical save truth; quality scaling remains continuous and does not change save identity or pass/fail thresholds.
Hardware Impact: Hot state transfer is two references/booleans and existing black-box/CSV event writes, 0 B source-level GC. Persistence cost is now explicitly post-simulation and still pending runtime measurement.

## Decision 030 - Trap Recovery Phase Flattening

Problem: `FastTick -> CheckStuck -> RecoverFromTrap` previously wrote `Rigidbody.position`, queued velocity resets, and called `WakeUp()` immediately from the fast simulation chain.
Solution: `RecoverFromTrap` now computes a finite target position and writes only primitive pending state. `LateFrameTick` applies Rigidbody position, velocity queue requests, wake-up, and transform position after the simulation phase.
Rejected Alternatives: Keeping immediate Rigidbody mutation in `FastTick` was rejected because it mixes QA recovery presentation/repair with simulation sampling. Adding raycasts or a physical path solver was rejected by the Cinematic Cheat Protocol; deterministic lift remains the cheap recovery fake.
Scalability potential: Low uses the same cheap vertical lift. Middle/high/ultra can increase visual richness elsewhere through `HomeostasisBrain.GlobalQualityWeight`, but recovery truth remains predictable and invariant.
Hardware Impact: Moves existing work to the late phase; no new allocation path. Expected low-end gain is stability/evidence hygiene rather than measured frame-time reduction.

## Decision 031 - Terminal Export Lifecycle Hardening

Problem: `Application.quitting` only queued terminal export. During shutdown there may be no next `LateFrameTick`, so CSV and sentinel evidence could be lost on the exact lifecycle path the task requires.
Solution: Add `FinalizeLifecycleStopCold` and route `OnDisable`, `OnDestroy`, and `Application.quitting` through it. If the run is active and terminal output is unwritten, it marks a lifecycle failure when needed and calls `WriteTerminalArtifactsCold` immediately.
Rejected Alternatives: Waiting for another frame after `Application.quitting` was rejected as non-deterministic. Writing JSON or binary dumps was rejected by the current source-only protocol.
Scalability potential: Same behavior on all tiers; this does not change gameplay truth, metric DTO layout, or quality scaling.
Hardware Impact: Cold shutdown-only file/sentinel work. Hot-path cost is 0 us.

## Decision 032 - Endurance CSV Capacity Bound

Problem: The legacy endurance CSV queue capacity of 64 was too small after live writer removal. A 10km high/ultra run can produce 40 distance samples, 20 PDA samples, 5 save events, memory checks, start/complete, plus traps/origin shifts, making drops likely.
Solution: Increase `CsvQueueCapacity` to 256. This is a bounded cold allocation and preserves terminal-only flush.
Rejected Alternatives: Reintroducing live file streaming or a background writer was rejected because it pollutes runtime proof. Dynamic list growth was rejected by Zero-GC policy.
Scalability potential: Low hardware pays a fixed larger cold buffer only. Middle/high/ultra retain denser evidence without runtime I/O.
Hardware Impact: Adds about 15 KB more managed cold record storage for 80-byte records versus 64 capacity. Runtime enqueue remains array copy and cursor math.

## Decision 033 - Shinobu38 Hot Text Filter Cleanup

Problem: QA-wide scan found `new` syntax in `Shinobu38QaWatchdogRuntime.FastTick`, `BotNavigationJob.Execute`, and transitive `Shinobu38MockTerrainSdf.SampleNormal`. These were struct/job constructions, not heap allocations, but they violate the APEX text-proof policy.
Solution: Replace job, DTO, `float2`, `float3`, and `double3` construction in those hot/transitive methods with `default` locals and field assignment.
Rejected Alternatives: Leaving the code semantically correct but text-filter dirty was rejected because the user explicitly requires source-code proof. Adding helper allocation or classes was rejected.
Scalability potential: No behavior change. Quality-weighted rich normal sampling remains continuous and still blends cheap and rich normals rather than a binary low-end switch.
Hardware Impact: No measured runtime gain claimed. Static source proof is cleaner; Burst should lower both forms similarly, pending Unity/Burst compile.

## Decision 034 - PDA/Sonar Phase Deferral

Problem: `QAEnduranceWatchdogBot.FastTick` triggered PDA open/close commands and sonar ping publication when the distance threshold was crossed. The cadence is sparse, but the work is presentation/UX-oriented and does not belong in the simulation phase.
Solution: `FastTick` now records only `_pendingPdaRadarAup` and `_pdaRadarRequestQueued`, advances the distance threshold once, and leaves command emission to `LateFrameTick` through `FlushQueuedPdaRadarLate`.
Rejected Alternatives: Keeping direct `ThreadSafeCommandQueue` and `SignalBus<SonarPingSignal>` calls in `FastTick` was rejected as a phase-law violation. A physical scan/avoidance simulation was rejected; this remains a cheap presentation pulse.
Scalability potential: Low tier avoids UI/sonar command work in the simulation tick. Middle/high/ultra retain the same truth route and can still scale radar radius/intensity continuously through `ResolveEnduranceQuality01`.
Hardware Impact: Hot state transfer is one AUP struct and one bool, 0 B source-level GC. No profiler-measured microsecond claim.

## Decision 035 - Shinobu38 Writer Stop Fail-Closed

Problem: `StopFileWriter` returned early if the DataVault cursor buffer could not be resolved, leaving the background file writer reference unjoined and the wake event undisposed. That is an orphan-thread risk during teardown.
Solution: Add `_fileWriterStopRequested`; stop now records the request before cursor resolution, signals the event, joins for 2000ms, interrupts once on timeout, joins for another 500ms, and records `FileWriterFlagException` when cursor shutdown cannot be proven.
Rejected Alternatives: Leaving a background thread alive was rejected. `Thread.Abort` was rejected as unsafe and unavailable in modern runtimes. Removing the entire Shinobu38 file writer in this pass was rejected because it is a larger headless-system contract change outside the immediate 1424 watchdog route.
Scalability potential: Low devices avoid editor/headless teardown stalls. High/ultra behavior is unchanged except for stricter lifecycle closure.
Hardware Impact: Shutdown-only code. Runtime frame impact is 0 us; thread lifecycle proof still requires Unity/headless execution.

## Decision 036 - Editor Polling Cadence Gate

Problem: Four editor batch runners polled result files directly from `EditorApplication.update`. This is editor-only, but it creates unnecessary filesystem checks at editor-frame cadence and kept file I/O text inside `Tick`.
Solution: Add a 0.25s `ShouldPollNow()` gate and move file polling into `PollRunState`/`PollBatchState` for `QAEnduranceBatchRunner`, `HeadlessSimulationBatchRunner`, `HeadlessStressFractureBatchRunner`, and `Shinobu38QaWatchdogBatchRunner`.
Rejected Alternatives: Keeping per-update `File.Exists` was rejected as needless editor CPU/I/O pressure. Using `FileSystemWatcher` was rejected because it adds a managed watcher lifecycle and platform edge cases for a simple batch result sentinel.
Scalability potential: Cheap editor hosts poll at 4Hz instead of every editor update; strong hosts get identical run semantics without extra complexity.
Hardware Impact: Source-level reduction from editor-frame polling to 4Hz. No runtime gameplay impact and no profiler-measured microsecond claim.

## Decision 037 - Final Build Gate Re-Sample

Problem: A compile check is still requested, but project rules forbid build launch above 50 percent CPU and generated `.csproj` files still do not reference the touched QA scripts.
Solution: Re-sampled after the phase/I-O cleanup: `CPU_LOAD_PERCENT=71`, compiler processes `none`; `.csproj` search for touched QA files returned no matches. Build launch count remains `0`.
Rejected Alternatives: Running `dotnet build` at 71 percent CPU would violate the host throttle. Running a generated-project build that excludes the touched files would create false compile evidence.
Scalability potential: No runtime change. Static artifacts are ready for Unity import/console verification when host/editor conditions allow it.
Hardware Impact: Avoided adding compiler load to a busy host. Verification remains `STATIC_SOURCE`, not `CLI_COMPILE`, `UNITY_CONSOLE`, `PLAYMODE`, or `PROFILER`.
