# Rationale 1620 - Integration Stress And QA Bot Supervisor

Status: PENDING VERIFICATION
Evidence Class: STATIC_SOURCE until Unity PlayMode/profiler artifacts exist.

## Decision 000 - Operating Envelope

Problem: 1620 must add Editor automation and QA verdict tooling without starving the parallel agent cluster or mutating runtime truth.
Solution: Limit first pass to `Assets/_Project/Editor/QA/`, `Assets/_Project/Scripts/QA/`, `Docs/Tasks/Status_1620.md`, `Docs/AgentLogs/Rationale_1620.md`, and final 1620 report/log paths. Treat all runtime readiness claims as `PENDING VERIFICATION` unless backed by fresh Unity artifacts.
Rejected Alternatives: Running `dotnet build` immediately was rejected because user explicitly banned heavy compilation after small edits and AGENTS forbids build under CPU/compiler contention. Creating global runtime dependencies was rejected because QA supervisor must observe through existing CSV/log/sentinel surfaces.
Scalability potential: Low uses static/log parsing and coarse counters; Middle adds ProfilerRecorder gates; High adds CSV percentile scans and deadlock dumps; Ultra allows richer telemetry analysis without changing gameplay truth.
Hardware Impact: Avoiding unnecessary build and runtime polling prevents cluster CPU starvation on i3/MX350-class hosts; estimated saved host wall time is build-dependent and not claimed as measured.

## Decision 001 - QA Metric Surface

Problem: Supervisor must evaluate Agent 1524 output without inventing telemetry fields.
Solution: Treat `QA_WATCHDOG_ENDURANCE_REPORT_1524.csv` as the owned metric route. Required columns are `state`, `frame_time_ms`, `gc_alloc_bytes`, `vram_mb`, `distance_m`, `rolling_p95_ms`, and `fail_reason_code`; optional columns are foveation/mipmap/quality fields if a later agent adds them.
Rejected Alternatives: Directly querying QA bot internals was rejected because `Hecton8.QA.Editor` is not referenced by the mandated `Assets/_Project/Editor` assembly route and would create a compile-wall dependency.
Scalability potential: Low parses fixed CSV columns; Middle adds optional mip/foveation columns; High/Ultra can add more columns without supervisor runtime truth ownership.
Hardware Impact: CSV byte scanning is bounded by appended bytes and avoids scene searches; estimated live poll cost remains below 100 microseconds per 8 KB slice on i3/MX350-class hosts.

## Decision 002 - Editor Automation Route

Problem: Supervisor must start the existing watchdog without coupling to its assembly.
Solution: Write `Temp/H8_QA_WATCHDOG_1524.flag`, open `Assets/_Project/Scenes/00_BOOTSTRAP.unity`, and set `EditorApplication.isPlaying = true` from an Editor update callback.
Rejected Alternatives: TestRunner orchestration was rejected for the primary route because the watchdog is scene/flag driven and the direct EditorApplication route is already proven by `QAWatchdogBatchRunner1524`.
Scalability potential: Low uses manual menu start; Middle uses batchmode menu entry; High/Ultra can wrap the same route in CI without changing gameplay code.
Hardware Impact: No runtime polling dependency is added; startup path is cold and expected to cost editor milliseconds only.

## Decision 003 - Non-Blocking Log Parser

Problem: Unity keeps `Editor.log` open while the supervisor must read crash lines in real time.
Solution: Track a byte offset and read only appended bytes with `FileShare.ReadWrite | FileShare.Delete`, then scan preallocated byte buffers for `error CS`, `NullReferenceException`, `IndexOutOfRangeException`, `AccessViolationException`, and sentinel leak signatures.
Rejected Alternatives: `File.ReadLines`, string split, regex, and full-file reads were rejected because they allocate, risk file contention, and scale poorly with long Editor logs.
Scalability potential: Low reads every 250 ms; Middle raises buffer size; High/Ultra can add more byte patterns without switching to managed regex.
Hardware Impact: 8 KB incremental reads avoid blocking the Unity logger; estimated hot scan is under 80 microseconds per slice on low-end silicon.

## Decision 004 - Deadlock Dump Route

Problem: Freeze diagnosis requires lock evidence without mutating global runtime state.
Solution: Deadlock is declared after two Editor update deltas above 500 ms or one confirmed second. Latest APEX decree rejects 1620 disk dump proof, so the supervisor now exposes `DeadlockSnapshot1620` in memory for source/test proof.
Rejected Alternatives: Private-field mutation, scene searches, binary 1620 dump files, and interactive `Process.Kill()` were rejected.
Scalability potential: Low captures scalar masks in memory; Middle adds memory pressure scalars; High/Ultra can append public block snapshots if stable no-mutation APIs exist.
Hardware Impact: No cost during normal frames beyond two double comparisons; no 1620 fault dump I/O remains.

## Decision 005 - Verdict Contract

Problem: Final proof must be machine-readable but not generate noisy intermediate reports.
Solution: Latest APEX decree moved proof from JSON to pristine C# source and edit-mode audits. The supervisor keeps metrics in memory and uses source-level tests for p95/VRAM/GC/leak/deadlock routes.
Rejected Alternatives: Periodic JSON snapshots, final 1620 JSON verdicts, and binary proof dumps were rejected by operator decree as useless disk noise.
Scalability potential: Low keeps source/test proof; Middle can run Unity Test Runner when host is idle; High/Ultra can attach CI artifacts without changing runtime truth ownership.
Hardware Impact: Removes 1620 report I/O entirely; zero frame and finalization report cost.

## Decision 006 - Sentinel Compatibility

Problem: Prompt names `NativeMemorySentinel.ValidateZeroLeaks()`, but the project currently exposes `AssertNoAllocationsAfterServiceShutdown(string)`.
Solution: Reflection first attempts `ValidateZeroLeaks`, then falls back to `AssertNoAllocationsAfterServiceShutdown("WatchdogSupervisor1620")`; active count and tracked bytes are captured from public properties.
Rejected Alternatives: Adding a new sentinel API was rejected as outside QA domain and unnecessary for the supervisor gate.
Scalability potential: Low catches active allocation count; Middle captures tracked bytes; High/Ultra can map labels from sentinel diagnostics if a public snapshot method is added.
Hardware Impact: Reflection is teardown-only; no gameplay frame cost.

## Decision 007 - Build Policy

Problem: Task 15 asks for a build, but the operator explicitly banned `dotnet build` unless critical.
Solution: Do not build after these Editor/test additions. Use static source inspection, `git diff --check`, targeted text scans, and source-level test coverage as the current proof. Mark build as policy-blocked unless Unity reports a compile wall.
Rejected Alternatives: Running MSBuild to satisfy ceremony was rejected because it would violate current operator instructions and cluster CPU policy.
Scalability potential: Low avoids build contention; Middle can run Unity Test Runner when cluster quiet; High/Ultra can add CI-only execution.
Hardware Impact: Saves one full compile pass; exact microseconds not measured because the build was intentionally not launched.

## Decision 008 - Release Verdict Honesty

Problem: The user requested a release readiness verdict, but no Unity PlayMode endurance run or Test Runner execution was launched in this pass.
Solution: Mark status as `PENDING VERIFICATION` in the 1620 ledger and avoid fabricated runtime p95, peak VRAM, GC, or leak measurements.
Rejected Alternatives: Claiming readiness from static code alone or writing a fake JSON verdict was rejected because QA evidence mandates distinguish source presence from runtime proof.
Scalability potential: Low uses static verdict to block release; Middle runs the supervisor menu/test runner; High/Ultra attaches CI/batchmode and richer telemetry once host contention permits.
Hardware Impact: Avoided false runtime workload and removed final JSON write from the shared host.

## Decision 009 - Unity Meta Hygiene

Problem: New files under `Assets/` without `.meta` files create Unity asset database churn and unstable GUID assignment.
Solution: Added `.meta` files for the new QA folder, supervisor script, and edit-mode test script with generated GUIDs.
Rejected Alternatives: Letting Unity auto-generate metadata later was rejected because this is a shared multi-agent repository and deterministic file identity matters.
Scalability potential: Low keeps asset import stable; Middle/High/Ultra preserve references if future menus/tests link these assets.
Hardware Impact: No runtime impact; prevents editor import noise on weak hosts.

## Decision 010 - APEX Proof Route Correction

Problem: Latest operator decree rejects JSON and binary dump proof artifacts; existing supervisor still contained disk verdict/dump writers from the original 1620 prompt.
Solution: Remove JSON verdict writing, delete the obsolete JSON file, and replace deadlock binary dump with `DeadlockSnapshot1620` captured in memory. Add C# edit-mode audits that prove hot lookup absence, phase-safe fuzzer timing, single DataVault write locks, and build-throttle compliance.
Rejected Alternatives: Keeping JSON/dump writers behind a flag was rejected because the requested proof route is now source code, not dormant I/O code. Leaving the fuzzer in `Update()` was rejected because QA fixtures still shape project architecture and must not normalize raw per-frame Unity callbacks.
Scalability potential: Low runs no disk telemetry and no fuzzer tick when disarmed; Middle uses dispatcher tick only when armed; High/Ultra can add richer in-memory snapshots without increasing I/O.
Hardware Impact: Removes deadlock JSON/dump I/O and raw `Update()` dispatch from the QA fuzzer. Normal disarmed cost is 0 us; armed fuzzer cost is intentional 1024 B/frame GC tripwire only during hostile QA.

## Decision 011 - Compilation Throttle Evidence

Problem: APEX protocol asks to assert no build spam while dotnet processes already exist on the host.
Solution: Static source scan confirms supervisor/tests do not contain `dotnet build`, `ProcessStartInfo`, or `System.Diagnostics.Process`. Process scan showed existing `dotnet` processes, but this pass launched none.
Rejected Alternatives: Killing unknown dotnet processes was rejected as cross-agent sabotage; running another build was rejected by explicit operator ban.
Scalability potential: Low keeps local verification to byte/text scans; Middle/High can run Unity Test Runner only when host contention is cleared.
Hardware Impact: Avoided one full build and did not add compiler contention.

## Decision 012 - Batch Runner Parser Hardening

Problem: QA/headless batch runners used `File.ReadLines` and one CSV `Split`, which allocates managed line strings during polling and scales badly when endurance reports grow.
Solution: Replaced these with bounded `FileStream` byte scanners using `FileShare.ReadWrite | FileShare.Delete`; the long 1524 CSV route now tails by byte offset with pending-line carryover, and JSON exit codes use direct ASCII byte parsers.
Rejected Alternatives: Keeping line enumeration was rejected because it hides per-line allocation. Pulling in a shared parser assembly was rejected because these runners sit in separate editor namespaces and the patch should not create new compile-wall dependencies.
Scalability potential: Low reads small result files with fixed 4 KB buffers; Middle handles long watchdog CSV streams in 8 KB slices and 4 KB line caps; High/Ultra can increase static buffer size without changing contracts.
Hardware Impact: Removes line/string array allocation from 0.25 s polling paths and prevents repeated full-file scans of the 10 km watchdog CSV. Estimated gain on i3/MX350-class hosts is small per poll early and material as the CSV grows.

## Decision 013 - Expanded APEX Source Audit

Problem: Initial APEX lock/hot-loop proof covered only the primary watchdog files while QA headless bots also own DataVault write paths and dispatcher ticks.
Solution: Extended `WatchdogSupervisor1620EditTests` to audit headless simulation, stress fracture, and Shinobu hot loops for cold lookup absence, and to assert single-write-lock plus `finally` release on every discovered QA DataVault write path.
Rejected Alternatives: Verbal proof was rejected. A generic regex parser for all C# methods was rejected as brittle and more code than the known QA lock surface needs.
Scalability potential: Low keeps exact source audits; Middle expands method list as QA bots grow; High/Ultra can replace exact signatures with a Roslyn analyzer when build/test contention is acceptable.
Hardware Impact: Test-only source reads; no gameplay frame cost and no build process launched.

## Decision 014 - Supervisor Verdict Semantics

Problem: CSV terminal parsing treated `Completed` and `Failed` as one boolean terminal state, so a failed watchdog row could request stop with failure code 0.
Solution: Added `_csvTerminalFailed`, mapped failed terminal rows to `WatchdogRuntimeFailed`, and added a failed-row edit test with fail reason code preservation.
Rejected Alternatives: Inferring failure only from nonzero `fail_reason_code` was rejected because state ownership is explicit in the CSV `state` field and must not depend on optional reason semantics.
Scalability potential: Low keeps a single bool and int; Middle/High/Ultra can map reason codes to richer labels without changing the terminal route.
Hardware Impact: Adds two byte-field comparisons per parsed CSV row; estimated below 1 microsecond per row.

## Decision 015 - VRAM Homeostasis Gate Correction

Problem: Over-budget VRAM with response columns present but inactive did not set `_homeostasisUnproven`; only missing columns did. That allowed a stalled scalability system to appear acceptable.
Solution: Mark homeostasis unproven when VRAM exceeds 1.6 GB and either response telemetry is absent or foveation/mip/quality response remains inactive.
Rejected Alternatives: Waiting for multiple over-budget samples was rejected for the supervisor gate because a single over-budget sample with explicit no-response is already a contract failure in the stress run.
Scalability potential: Low catches no-response; Middle tracks sustained response ratio; High/Ultra can add separate thresholds for texture mips and foveation tiers.
Hardware Impact: Reuses parsed row fields; no extra I/O and no allocations.

## Decision 016 - Unsigned DataVault Mask Snapshot

Problem: `ActiveMutationGuardMask` is `ulong`; converting it through `Convert.ToInt64` can overflow when high bits are set during a deadlock.
Solution: `ReadLongProperty` now handles `long`, `int`, `uint`, and `ulong` explicitly, preserving bit patterns with unchecked conversion for unsigned masks.
Rejected Alternatives: Returning zero for all unsigned values was rejected because lock masks are the evidence needed by the integrator.
Scalability potential: Low captures current masks; Middle/High/Ultra can add more public unsigned counters without changing the snapshot helper.
Hardware Impact: Fault-path only; normal frame cost unchanged.

## Decision 017 - Job Execute Audit

Problem: APEX protocol explicitly names `Execute`, but the earlier hot-loop audit covered dispatcher ticks and selected helpers, not every QA headless job execute body.
Solution: Added a source audit that extracts every `public void Execute` body in `Shinobu38QaWatchdogRuntime.cs` and `PowerGridJacobiStressFuzzer.cs` and checks for `GlobalRegistry`/`GetComponent` cold lookup tokens.
Rejected Alternatives: Manually asserting the current grep output was rejected because future job additions need a test gate, not a one-time observation.
Scalability potential: Low covers known QA job files; Middle adds more QA job source files; High/Ultra can move to Roslyn once compile/test contention clears.
Hardware Impact: Edit-test source scan only; no runtime cost and no build process launched.

## Decision 018 - PlayMode Exit Terminal Poll

Problem: A watchdog run can write a terminal CSV row and exit PlayMode before the supervisor's next 250 ms poll; the first retry patch could classify that valid exit as `PLAYMODE_EXITED_BEFORE_TERMINAL`.
Solution: In the not-playing-after-start branch, poll `Editor.log`, poll CSV, flush a partial terminal row, persist offsets, and let `RequestTerminalStop()` decide `Completed`/`Failed` before raising `PlayModeExitedUnexpectedly`.
Rejected Alternatives: Treating any PlayMode exit after start as fatal was rejected because CSV is the watchdog-owned truth route. Increasing poll cadence was rejected because it spends CPU to hide an ordering bug.
Scalability potential: Low preserves correct terminal ownership at 250 ms polling; Middle/High/Ultra can keep the same exit contract while adding richer terminal columns.
Hardware Impact: Adds one cold final CSV/log tail read only after PlayMode stops. Normal running frame cost is unchanged; no `dotnet build`, Test Runner, or PlayMode run was launched by this pass.

## Decision 019 - Startup Delta And Partial Log Guard

Problem: The first tick after PlayMode entry could inherit editor startup/open-scene latency and trip the deadlock detector before simulation had actually produced a frame. Also, a fatal `Editor.log` line without a trailing newline could remain buffered and be missed at finalization.
Solution: Added `PlayModeObservedKey`; on the first observed PlayMode tick the supervisor resets deadlock counters and uses zero delta. Added `FlushPartialLogLine()` on finalization and post-start PlayMode-exit paths.
Rejected Alternatives: Raising the deadlock threshold was rejected because it weakens the actual freeze gate. Polling `Editor.log` more frequently was rejected because it spends CPU and still misses unterminated lines without an explicit flush.
Scalability potential: Low keeps strict deadlock thresholds without false startup positives; Middle/High/Ultra can tighten runtime thresholds later because startup latency is no longer mixed into simulation timing.
Hardware Impact: First PlayMode tick adds one `SessionState` bool check and scalar reset. Partial log flush is cold finalization work only. No build or PlayMode run was launched.

## Decision 020 - Source Audit Closure For Cold Lookups And Job Completion

Problem: Existing proof showed hot methods were clean, but did not prove where remaining `TryGetComponent` tokens live or whether QA job runtimes had raw `JobHandle.Complete()` calls.
Solution: Extended `WatchdogSupervisor1620EditTests` to assert every QA watchdog/fuzzer `TryGetComponent` occurrence is inside named `*Cold` methods, and to assert Shinobu/Jacobi job runtimes contain no `.Complete(` while using `DispatcherJobFence.TryComplete`.
Rejected Alternatives: Grep-only manual observation was rejected because future QA edits need an executable source gate. A generic AST analyzer was deferred because the local Roslyn load still fails under Windows PowerShell dependency binding.
Scalability potential: Low keeps exact source gates; Middle expands the allowed cold-method list if new QA probes are added; High/Ultra can replace the helper with Roslyn when the dependency issue is solved.
Hardware Impact: Test-only source scanning. Runtime frame cost is 0 us. No compiler process was started.

## Decision 021 - Endurance Runner Fail-Fast Bootstrap

Problem: `QAEnduranceBatchRunner` could start PlayMode in the current scene when `00_BOOTSTRAP.unity` was missing and could throw out of menu/batch entry on flag/result file I/O. Its update callback also returned silently if `ActiveKey` was externally cleared.
Solution: Added guarded `TryWriteFlagFile()`, `TryEnsureBootstrapScene()`, `ExitCodeKey`, inactive `Detach()`, and exception-shielded status/delete writes. Missing or failed bootstrap now requests nonzero stop instead of starting an undefined scene.
Rejected Alternatives: Leaving bootstrap optional was rejected because QA endurance must test the same project boot route every time. Letting file I/O exceptions bubble was rejected because it leaves batch state uncleared.
Scalability potential: Low avoids false scene-specific passes; Middle/High/Ultra can reuse the same runner from editor menu or batchmode with deterministic bootstrap routing.
Hardware Impact: Cold runner startup only. No gameplay frame cost. It prevents orphaned editor update callbacks when state is cleared.

## Decision 022 - Headless Runner Inactive Detach

Problem: Headless simulation and stress-fracture batch runners returned from `Tick()` when `ActiveKey` was false, but did not detach their `EditorApplication.update` callbacks in that branch.
Solution: Added immediate `Detach()` before returning from inactive `Tick()` for both headless batch runners and locked this behavior with the 1620 batch-runner source audit.
Rejected Alternatives: Relying on domain reload or normal completion was rejected because externally cleared session state must not leave idle editor callbacks alive.
Scalability potential: Low removes idle editor callback work; Middle/High/Ultra keep deterministic batch runner lifecycle during long multi-agent sessions.
Hardware Impact: Normal active run cost unchanged. Inactive orphan callback cost becomes 0 after one update.

## Decision 023 - Shinobu Batch Runner Lifecycle And Parser Hygiene

Problem: `Shinobu38QaWatchdogBatchRunner` shared the same runner hazards as the other QA batch paths: optional bootstrap scene, unguarded flag/status writes, inactive callback without detach, and one-byte result scanning via `ReadByte()`.
Solution: Added guarded `TryWriteFlagFile()`, `TryEnsureBootstrapScene()`, inactive `Detach()`, buffered `FileContainsPattern()` result scanning with `FileShare.ReadWrite | FileShare.Delete`, and exception-shielded status writes.
Rejected Alternatives: Leaving SHINOBU outside 1620 source audits was rejected because it is a QA headless runner using the same editor lifecycle lane. Keeping `ReadByte()` was rejected because the other batch runners already moved to shared fixed byte buffers.
Scalability potential: Low prevents wrong-scene endurance passes; Middle/High/Ultra preserve deterministic batchmode/editor behavior without per-byte syscalls.
Hardware Impact: Active runtime cost unchanged. Result polling shifts from per-byte file reads to 4 KB buffered reads; inactive callback cost becomes 0 after one update.

## Decision 024 - Jacobi Editor Runner Pending Job Finalization

Problem: `JacobiStressFuzzerWindow.PollPendingRun()` returned on a null pending run without detaching the editor update callback, and `FinishPendingRun()` relied on the happy path to dispose the scheduled native run and clear the progress UI.
Solution: Null pending-run now detaches `EditorApplication.update`, clears the progress bar, and reenables the run button. Completion now copies `_pendingRun` to a local, calls `Complete()` inside `try`, logs completion exceptions, and disposes/clears callback/progress/button state in `finally`.
Rejected Alternatives: Leaving cleanup to `OnDisable()` was rejected because long editor sessions can keep a hidden callback alive. Blocking job completion earlier was rejected because the code already waits for `IsCompleted()` and forced completion before readiness would spend editor CPU.
Scalability potential: Low avoids stale editor callbacks during manual QA. Middle/High/Ultra keep the same scheduled Burst chain and only improve failure cleanup; no gameplay truth or solver math changes.
Hardware Impact: Active run cost is unchanged. Stale callback cost becomes 0 after one editor update; pending native buffers are disposed even if completion throws. No build, Test Runner, or PlayMode run was launched because external `dotnet` processes are active.
