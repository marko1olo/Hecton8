# Status 1424 - QA Watchdog Bot

Date: 2026-05-28
Agent: 1424
Domain: ECHELON 9 / QA Watchdog Bot
Status: PENDING VERIFICATION

## Prompt Extraction

- Source: `Docs/Tasks/CURRENT_BATCH.md`
- Extracted block: `<AGENT_PROMPT id="1424">`
- Task count: 20
- Hygiene: no pre-existing `Status_1424.md` or `Rationale_1424.md` found.

## Relevant Mandates Read

- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `DATA_Runtime_Struct_Layout_ARM64.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `ARCH_Execution_Phases.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`

## [ANALYSIS]

Target: create `Assets/_Project/Scripts/QA/QA_WatchdogBot.cs` plus proof ledgers/reports for automated PlayMode endurance inspection.
Affected systems: QA/dev-build instrumentation, Unity ProfilerRecorder, scene sequencing, player input/kinematic injection adapter, DataVault/sentinel reflection bridges, CSV/JSON report export.
Zero GC proof: hot metric path must only read `ProfilerRecorder.CurrentValue`, write value-type records into preallocated arrays, update primitive counters, and avoid strings, LINQ, boxing, allocations, Debug.Log, scene searches, and `StringBuilder` until terminal serialization.
State check: status/rationale ledgers created first; code must cache scene/player references outside hot sampling; native/persistent allocations avoided unless required by existing authority; final CSV emission happens once at run end/abort.
Rule quote: `QUALITY_GATES.md` requires `0 B/frame` GC, `<=16.67ms p95`, frame time above `25ms` for 3 frames triggers load-shed/fault, and runtime proof cannot be claimed from static source.

## Loop 1 - Tasks 01-05

- [x] Task 01 EXHAUSTIVE_PROFILER_API_INQUISITION | DOD: `Docs/Reports/QA_WATCHDOG_PROFILER_COUNTERS_1424.json` records Unity ProfilerRecorder counters, categories, units, fallbacks, and fatal thresholds. Rejected: ad hoc `Time.deltaTime` only, because it cannot prove GC/VRAM. Hot estimate: recorder reads plus packing under 18 us before DataVault lock cost.
- [x] Task 02 SCENE_WIRING_FORENSIC_ANALYSIS | DOD: source route checked: `00_BOOTSTRAP` -> `01_MAIN_MENU`; `MainMenuController.StartGame("")` currently routes new game to `01_ORBIT`, so watchdog records this and falls back through `ISceneService.LoadScene("02_HECTON_WORLD")`. Rejected: editing menu scene fields and UI clicks. Hot estimate: 0 us after scene is reached.
- [x] Task 03 NAVIGATION_VECTOR_MAPPING | DOD: input enters through `CoreDeterminismSignals.TryPublishInputOverride` with deterministic forward/lateral/vertical triangle-wave perturbation and KCC velocity feedback. Rejected: PhysX raycasts per frame because they add non-owned physics work. Hot estimate: 5-9 us.
- [x] Task 04 ZERO_GC_STORAGE_ARCHITECTURE_PLANNING | DOD: 32-byte `QAWatchdogFrameMetric1424`, 64-byte black-box DTO, GlobalDataVault buffer IDs `74240/74241`, fixed managed fallback only before vault availability. Rejected: `List<T>`, per-frame `StringBuilder`, persistent MonoBehaviour-owned `NativeArray`. Hot estimate: 12-35 us depending on vault lock.
- [x] Task 05 TELEMETRY_AND_REPORTING_PLANNING | DOD: runtime paths fixed for CSV, metadata JSON, and binary black-box dump; static metadata artifact written. Rejected: chat-only report and runtime Debug.Log spam. Hot estimate: 0 us; all serialization is terminal cold path.

Loop 1 verification: static source/API check completed; real `dotnet build` intentionally deferred under CPU/build policy. No runtime proof claimed.

## Loop 2 - Tasks 06-10

- [x] Task 06 WATCHDOG_SCAFFOLDING_MATERIALIZATION | DOD: `Assets/_Project/Scripts/QA/QA_WatchdogBot.cs` created under `#if UNITY_EDITOR || DEVELOPMENT_BUILD`; recorders start cold and dispose on shutdown. Rejected: release-build bleed and Update-loop observer. Hot estimate: 0 us for setup after run starts.
- [x] Task 07 ZERO_GC_METRIC_EXTRACTION_IMPLEMENTATION | DOD: `LateFrameTick` calls `SampleMetricHot`, packs a 32-byte struct, writes fixed slots, updates p95 bucket ring. Rejected: `List<T>`, per-frame CSV, formatting, `ProfilerRecorderHandle` enumeration during sampling. Hot estimate: 30-70 us including two vault-lock attempts on low silicon.
- [x] Task 08 AUTONOMOUS_SCENE_SEQUENCER | DOD: state machine waits bootstrap/menu/world, resolves `MainMenuController` through preallocated root traversal, invokes `StartGame(string.Empty)` directly, then uses `ISceneService.LoadScene("02_HECTON_WORLD")` fallback for orbit mismatch. Rejected: scene-field mutation, UI raycaster clicks, and per-tick MonoBehaviour array search. Hot estimate: 0 us during simulation.
- [x] Task 09 KINEMATIC_DRIVE_INJECTION | DOD: `FastTick` publishes `PlayerInputState` overrides through `CoreDeterminismSignals`; KCC velocity freshness is required to accumulate distance and avoid false success. Rejected: direct Rigidbody teleport and per-frame physics raycasts. Hot estimate: 5-9 us.
- [x] Task 10 FATAL_THRESHOLD_TRIPWIRES | DOD: GC >0 during simulation, VRAM >=1600 MB, p95 >16.67 ms, or 25 ms x3 frames queues failure and terminal CSV/dump. Rejected: warning-only reporting. Hot estimate: 2-4 us for primitive checks.

Loop 2 verification: `git diff --check` produced only an existing line-ending warning on the QA asmdef; hot-path text scan found no `new`, formatting, reflection, scene search, file I/O, or `Debug.Log` in `LateFrameTick` metric extraction functions. Real build still deferred.

## Loop 3 - Tasks 11-15

- [x] Task 11 DATAVAULT_DEFRAGMENTATION_STRESSOR | DOD: every 60 simulation seconds the bot calls `RequestEditorForceDefragmentation()` and `FrostTickDefrag(0.2f, 1f, PreSimulation, ActiveBurstLockMask)`. Rejected: nonexistent `TryRunLiveCompactionSlice` symbol. Hot estimate: 0 us except once-per-60s cold stressor.
- [x] Task 12 NATIVE_SENTINEL_ASSERTION_HOOK | DOD: completion/failure and `Application.quitting` invoke sentinel validation by reflection, preferring `ValidateZeroLeaks` if present and falling back to `AssertNoAllocationsAfterServiceShutdown`. Rejected: direct unsafe class dependency in QA asmdef. Hot estimate: 0 us; terminal only.
- [x] Task 13 COMPILE_WALL_AND_NAMESPACE_HYGIENE | DOD: QA asmdef updated with `Unity.Profiling.Core`; bot stays in `Hecton8.QA`, guarded from release builds, no Core production edits. Rejected: moving profiler harness into Core. Hot estimate: 0 us.
- [x] Task 14 DRY_RUN_VERIFICATION_EXECUTION | DOD: rationale records frame-1000/5000/defrag/overflow simulation; circular buffer overwrites oldest records and does not overflow. Rejected: unbounded telemetry growth. Hot estimate: fixed-slot wrap under 2 us.
- [BLOCKED BY CONTENTION] Task 15 BATCHED_COMPILATION_AND_EXECUTION_CHECK | CPU sample was 100 percent and an active `dotnet` process existed (`Id 57828`), so launching a build would violate the project rule. Static verification continues; no compile success is claimed.

Loop 3 verification: prompt block re-extracted with corrected id regex; build gate blocked by contention, not by code evidence.

## Loop 4 - Tasks 16-18

- [BLOCKED RUNTIME] Task 16 MOCK_FAILURE_FUZZER_TEST | DOD implemented: `Assets/_Project/Scripts/QA/Editor/QAWatchdogGcAllocationFuzzer1424.cs` intentionally allocates `new byte[1024]` in `Update` and is manually armed by menu item. Rejected: auto-arming the fuzzer, because it would contaminate normal QA runs. Runtime contaminated PlayMode proof not executed because build/host contention blocked verification; no alarm-success claim is made.
- [x] Task 17 CSV_SERIALIZATION_ROUTINE | DOD: terminal-only `WriteCsvCold` serializes frame, state, frame time ms, GC bytes, VRAM MB, batches, setpass, distance, AUP, and flags to `Docs/Reports/QA_WATCHDOG_ENDURANCE_REPORT_1424.csv`. Rejected: per-frame streaming. Hot estimate: 0 us.
- [x] Task 18 ZERO_COMPILATION_HOT_PATH_VERIFICATION | DOD: lines 738-1006 were inspected for `LateFrameTick`, sampling, metric writes, black-box writes, and tripwire checks; lines 618-1037 were inspected for simulation input plus metric route. Both spans contain zero hits for reference-style `new`, formatting, `.ToString()`, LINQ, `foreach`, scene search, file I/O, or `Debug.Log`. Rejected: relying on runtime profiler proof that was not run. Hot estimate: static audit only, measured proof pending PlayMode.

Loop 4 verification: fuzzer harness exists but was not executed; hot-path visual scan completed from source lines 738-1006 and expanded source lines 618-1037.

## Loop 5 - Tasks 19-20

- [x] Task 19 ARCHITECTURE_VALIDATION_ASSERTION | DOD: exact mandated validation paragraph is written in `Docs/AgentLogs/Rationale_1424.md`. Rejected: claiming static analysis equals endurance truth. Hot estimate: documentation only.
- [x] Task 20 AUTOMATED_METRIC_VALIDATOR_REPORT | DOD: `Docs/Reports/QA_WATCHDOG_METADATA_1424.json` contains watchdog config, thresholds, zero-GC extractor audit, fuzzer status, build contention state, and SHA-256 hashes for modified QA files. Rejected: unstructured final report. Hot estimate: 0 us.

Loop 5 verification: metadata report written; SHA-256 hashes recorded; runtime execution remains blocked/unclaimed.

## Build Policy

- `dotnet build` is forbidden unless CPU <= 50 percent and no `csc.exe` or active dotnet compile is present.
- Real Unity PlayMode execution is not claimed unless a fresh Unity artifact exists.
- Final recheck before report: CPU was 99 percent with active `csc` process `43496` and `dotnet` process `35512`, so no build or PlayMode stress run was launched.

## APEX Re-Audit - 2026-05-28

- [x] DataVault lock release defect fixed | DOD: `WriteMetricHot` and `WriteBlackBoxHot` now acquire writer locks into a local `lockAcquired` flag and release them from `finally` blocks. Rejected: chained `if (TryAcquireWriteLock && IsCreated && slot < Length)` because a successful lock could skip release on invalid buffer/slot or exception. Hot estimate: +1 branch, 0 B GC, under 1 us.
- [x] Main-menu search allocation removed from route tick | DOD: `Object.FindObjectsByType<MonoBehaviour>` plus reflection `MethodInfo.Invoke` was replaced with direct `MainMenuController.StartGame(string.Empty)` and a preallocated root scratch list traversal. Rejected: per-tick scene-wide MonoBehaviour array search. Route estimate: no managed array allocation; O(root + child count) until controller cached.
- [x] Continuous scalability repaired | DOD: watchdog now samples `HomeostasisBrain.GlobalQualityWeight` into `_resolvedQualityWeight01`; lateral and vertical swim perturbation amplitudes scale continuously with `math.lerp` from low to ultra. Rejected: local serialized `globalQualityWeight` that only appeared in terminal metadata and binary `isLowEnd` switches. Hot estimate: four lerps in input publish path, 0 B GC.
- [x] APEX static hot-path scan rerun | DOD: `SimulationHot` lines `618-1037` and `LateMetric` lines `738-1006` returned `0` hits for reference-style `new`, `string.Format`, `.ToString()`, `foreach`, LINQ materializers, and scene-search APIs. Rejected: claiming PlayMode zero-GC without runtime profiler data.
- [BLOCKED BY CONTENTION] Final build/PlayMode gate | CPU sample immediately before build decision: `CPU_LOAD_PERCENT=99`; active process rows: `csc pid 43496`, `dotnet pid 35512`. Build was not launched because CPU > 50 percent and compiler/dotnet processes were active. Compile and endurance success remain unclaimed.

## APEX Follow-Up - 2026-05-28

- [x] Evidence naming defect fixed | DOD: black-box field and internal counter renamed from `MenuReflectionAttempts` to `MenuResolveAttempts` after reflection was removed. `FieldOffset(48)` and 64-byte struct size remain unchanged. Rejected: keeping a misleading field name in proof artifacts.
- [x] Cold route allocation risk reduced | DOD: `_sceneRootScratch` capacity increased from 64 to 512 and all watchdog-managed cold allocations now carry explicit `COLD ALLOC` owner comments. Rejected: allowing `List<GameObject>` to grow if a large main-menu scene exceeds 64 root objects.
- [x] Terminal sentinel allocation removed | DOD: terminal `new object[]` for `AssertNoAllocationsAfterServiceShutdown` was replaced by preallocated `_sentinelAssertArgs`. Rejected: per-terminal reflection argument allocation when the proof harness can account for it upfront.
- [x] APEX static hot-path scan rerun after follow-up | DOD: `FastTickRouteAndSimulation` lines `430-1008`, `SimulationHot` lines `620-1008`, and `LateMetric` lines `740-1008` returned `0` hits for reference-style `new`, `string.Format`, `.ToString()`, `foreach`, LINQ materializers, scene-search APIs, file I/O, and `Debug.Log`.
- [BLOCKED BY CONTENTION] Final build/PlayMode gate re-sampled | CPU sample immediately before build decision: `CPU_LOAD_PERCENT=68`; active compiler process: `none`. Build was still not launched because CPU > 50 percent. Compile, PlayMode, fuzzer, and zero-runtime-GC success remain unclaimed.

## APEX Route/Metric Hygiene Follow-Up - 2026-05-28

- [x] Hot route scene-name reads removed | DOD: `FastTickWaitBootstrap` and `FastTickWaitWorld` now compare `SceneManager.GetActiveScene().buildIndex` against constants mapped from `ProjectSettings/EditorBuildSettings.asset` lines `9/12/15/18`: bootstrap index `0`, main menu `1`, orbit `2`, world `3`. Rejected: per-frame `.name` string reads in the route state machine.
- [x] Frame-time fallback de-UnityTime'd | DOD: `_lastFastDeltaTimeSeconds` is written only from validated `FastTick(float deltaTime)` input and used by `ReadRecorderMicroseconds`; hot metric extraction no longer reads `UnityEngine.Time`. Rejected: `Time.unscaledDeltaTime` fallback inside `SampleMetricHot`.
- [x] Redundant hot reads removed | DOD: `_lastKccVelocityFresh` and `_lastPlayerStateFresh` are produced by `IntegrateDistanceHot` and consumed by `SampleMetricHot`, so the sampler does not duplicate KCC/player-state reads. Rejected: double-reading runtime context just to set CSV flags.
- [x] Graphics fallback gated | DOD: `Profiler.GetAllocatedMemoryForGraphicsDriver()` is called only if the Gfx Used Memory recorder is invalid. Rejected: eager fallback call when recorder data is already valid.
- [x] Corrected APEX static hot-path scan | DOD: literal-signature scan returned `FastTickRouteAndSimulation` lines `436-1021`, `SimulationHot` lines `631-1021`, and `LateMetric` lines `755-1021`; every span returned `0` hits for reference-style `new`, `string.Format`, `.ToString()`, `foreach`, LINQ materializers, scene-name reads, scene-search APIs, `UnityEngine.Time`, file I/O, and `Debug.Log`.
- [BLOCKED BY CONTENTION] Final build gate re-sampled after artifact update | CPU sample before compile decision: `CPU_LOAD_PERCENT=100`; active process: `dotnet pid 51008`. `dotnet build` launch count this turn remains `0`.
