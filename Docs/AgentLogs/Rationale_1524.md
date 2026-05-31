# Rationale 1524 - QA Watchdog Bot

Problem: Current batch prompt is 1524, but the live QA watchdog source is a carried-over 1424 harness.
Solution: Reuse the existing QA-only harness architecture and correct ownership/path/switches for 1524 instead of rewriting production runtime systems.
Rejected Alternatives: Full rewrite would risk new compile debt and duplicate already-present dispatcher/DataVault/ProfilerRecorder routes. Editing physics/KCC/rendering to help the test pass is outside domain.
Scalability potential: Low runs same sterile observation with sparse CSV terminal output; Middle/High/Ultra can increase route coverage through continuous `GlobalQualityWeight`-scaled steering amplitude without changing gameplay truth.
Hardware Impact: Expected new hot cost is no worse than existing watchdog: recorder reads, struct packing, ring writes. Exact microseconds are PENDING PROFILER PROOF.

Problem: XML task asks for JSON ledgers/metadata, while the live coordinator instruction rejects useless JSON reports and binary dumps during work.
Solution: Keep durable proof in `Status_1524.md`, `Rationale_1524.md`, `LOG_1524.md`, source scans, and terminal CSV output. Do not add runtime binary dump output. Treat existing DataVault blackbox ring as in-memory telemetry, not disk dump.
Rejected Alternatives: Writing `QA_WATCHDOG_METADATA_1524.json` just to satisfy stale prompt text; emitting `Dump_1524.bin` during normal QA run.
Scalability potential: CSV is graphable and cheap; high-end lanes can collect denser samples in memory without changing file schema.
Hardware Impact: Avoids cold JSON/binary writer churn and reduces disk I/O contention on low-end storage; hot path unchanged.

Problem: QA bot needs to drive movement without direct dependency on absent or unstable KCC internals.
Solution: Use existing `CoreDeterminismSignals.TryPublishInputOverride` hot lane and validate actual movement through `TryGetLatestKccVelocityFloat3` plus `PlayerRuntimeContextService` snapshots.
Rejected Alternatives: Direct KCC field mutation, transform teleport, or synchronous physics casts. Those would change gameplay truth and invalidate QA.
Scalability potential: Low uses cheap triangle-wave steering; Middle/High/Ultra can raise lateral/upward perturbation amplitude smoothly by quality weight for broader obstacle escape without binary quality switches.
Hardware Impact: Signal write is value-type only; no scene search or allocation in the movement loop. Estimated under 10 us before dispatcher overhead, PENDING PROFILER PROOF.

Problem: 10km runtime proof needs telemetry storage without contaminating GC metrics.
Solution: Use fixed 32-byte `QAWatchdogFrameMetric` ring and fixed 64-byte blackbox entries through DataVault when available; fallback managed arrays only in cold setup before the run.
Rejected Alternatives: `List<T>`, `StringBuilder`, CSV stream, or local persistent `NativeArray` owned by MonoBehaviour in hot path.
Scalability potential: 36,000-frame ring covers 10 minutes at 60Hz; circular overwrite prevents overflow on long runs. Ultra proof can run longer with same bounded memory by sacrificing oldest samples.
Hardware Impact: Fixed footprint around 1.17 MB managed fallback or DataVault equivalent; no per-frame allocation.

Problem: Compile verification can steal CPU from the project cluster.
Solution: Gate any `dotnet build` behind CPU <= 50% and no `dotnet/csc/VBCSCompiler` process; if blocked, record contention and rely on static scans.
Rejected Alternatives: Blind full build. Unity import claim from stale logs.
Scalability potential: N/A; host-resource policy.
Hardware Impact: Prevents compiler contention on shared workstation. Runtime impact 0 us.

Problem: The inherited watchdog could report clean GC/VRAM if a required `ProfilerRecorder` counter failed to bind on this Unity version.
Solution: Treat frame time, GC allocated in frame, and gfx memory recorders as critical. Missing critical recorder now stamps a metric flag and fails terminal state with `ProfilerRecorderUnavailable`.
Rejected Alternatives: Using fallback zeroes for GC or VRAM; this creates a false pass and invalidates QA evidence.
Scalability potential: Low/Middle/High/Ultra all use the same proof route; optional render counters remain telemetry, not pass/fail authority.
Hardware Impact: Adds one hot bool branch and flag OR. Estimated under 1 us; exact value pending profiler proof.

Problem: Managed fallback arrays were allocated after profiler recorders started, so a cold fallback could pollute first-frame GC evidence.
Solution: Resolve DataVault and prepare storage before starting recorders. Fallback remains cold, bounded, and visible through the `ManagedFallback` flag.
Rejected Alternatives: Subtracting or ignoring first-frame GC samples. That hides contamination instead of removing it.
Scalability potential: Low devices avoid false GC failures from setup; Ultra runs can still use the same fixed DataVault capacity without schema drift.
Hardware Impact: Moves roughly 1.17 MB fallback allocation outside the measurement window. Hot runtime unchanged.

Problem: GC alarm testing needs a hostile allocation source, but normal endurance runs must remain sterile.
Solution: Add `QAWatchdogGcAllocationFuzzer1524` as a PlayMode-only, manually armed fixture. The `Update` loop allocates 1024 B/frame only when armed; normal runs pay a static bool read for the metric flag.
Rejected Alternatives: Always-on fuzzer, editor-only one-shot allocation as the only test, or modifying production gameplay systems to allocate.
Scalability potential: Low/Middle/High/Ultra normal runs remain identical; hostile mode isolates failure detection regardless of device tier.
Hardware Impact: Normal path estimated under 1 us. Armed test intentionally spends 1024 B/frame to prove tripwire behavior.

Problem: Unity compile console showed `QA_WatchdogBot.cs` resolving bare `Environment` against the project namespace `Hecton8.Environment`.
Solution: Qualify the calls as `System.Environment.GetEnvironmentVariable` and `System.Environment.GetCommandLineArgs`.
Rejected Alternatives: Adding a namespace alias or renaming the project `Hecton8.Environment` namespace. The local qualification is minimal and avoids cross-domain churn.
Scalability potential: N/A; cold batch bootstrap route only.
Hardware Impact: No hot runtime effect. Cold autorun check still allocates only Unity/system command-line strings already provided by the runtime.

Problem: Global Unity console contains unrelated compile errors outside QA, preventing honest PlayMode endurance proof.
Solution: Stop at targeted QA script validation and record global compile as blocked by other domains. Do not edit unrelated Narrative, Rendering, World, or Tools files from the QA watchdog assignment.
Rejected Alternatives: Broad cross-domain fixes without task ownership; claiming PlayMode proof from a project that cannot compile globally.
Scalability potential: N/A; integration blocker.
Hardware Impact: Avoids additional full-compile churn while another `dotnet` process is active.

Problem: The watchdog flipped `_state` to `Completed`/`Failed` after writing the triggering gameplay metric, so the CSV could lack any terminal verdict row.
Solution: Capture a cold terminal metric after sentinel validation and append it directly to the CSV with a `TerminalSample` flag. Also write the terminal sample to the blackbox ring.
Rejected Alternatives: Teaching the batch runner to infer failure from the absence of a terminal row; this hides the missing proof artifact instead of fixing it.
Scalability potential: Low/Middle/High/Ultra all get the same deterministic CSV verdict, independent of frame count or ring wrap.
Hardware Impact: Cold-only one-row CSV append at terminal export. Hot path unchanged.

Problem: Existing editor runner targets legacy `H8_QA_ENDURANCE_10KM` and a text result file, while the 1524 watchdog starts from `H8_QA_WATCHDOG_1524` and proves through CSV.
Solution: Add `QAWatchdogBatchRunner1524` as a separate editor-only runner. It writes the 1524 flag, opens `00_BOOTSTRAP`, enters PlayMode, waits for the 1524 CSV, and derives exit code from the terminal row.
Rejected Alternatives: Mutating `QAEnduranceBatchRunner` and breaking the older `QAEnduranceWatchdogBot` route; depending on JSON/TXT status files despite the coordinator's CSV-only proof requirement.
Scalability potential: Editor/manual and batchmode routes use the same CSV verdict; weak machines can timeout cleanly without corrupting proof, high-end machines simply finish sooner.
Hardware Impact: Editor-only polling at 4 Hz. Runtime game hot path cost 0 us.

Problem: After declaring the VRAM recorder critical, `QA_WatchdogBot` still called `Profiler.GetAllocatedMemoryForGraphicsDriver()` when that recorder was missing.
Solution: Remove the fallback call from both hot sampling and terminal capture. Missing VRAM recorder now records zero VRAM for that row and fails through `ProfilerRecorderUnavailable`.
Rejected Alternatives: Keeping a fallback profiler API in the hot observation path; it adds measurement ambiguity in a branch that cannot produce a valid pass.
Scalability potential: All device tiers get the same proof rule: recorder present or run invalid. No tier-specific silent downgrade.
Hardware Impact: Saves one profiler fallback call on recorder-missing frames; normal valid-recorder path unchanged.

Problem: The CSV verdict row proved Completed/Failed state but did not expose the exact failure reason or final run counters without reading the internal blackbox ring.
Solution: Extend only the cold CSV export schema with terminal forensic fields: fail reason code/name, final distance, rolling p95, spike streak, DataVault write failures, defrag request count, menu resolve attempts, and scene fallback attempts. Non-terminal metric rows keep zero forensic fields; `TerminalSample` marks the authoritative row.
Rejected Alternatives: Enlarging the 32-byte hot metric DTO or duplicating blackbox data into every frame row. Both would either violate the explicit metric footprint or create misleading per-row history.
Scalability potential: Low-tier devices produce the same compact terminal verdict; Middle/High/Ultra runs can be compared by final distance, p95, and stress counters without a second artifact.
Hardware Impact: Hot path unchanged. Added work is cold-only CSV writes after terminal export; estimated 0 us during simulation.

Problem: Full project compile remains blocked outside QA, and host build policy forbids another heavy build during active compiler/CPU contention.
Solution: Use targeted Unity-MCP validation for the changed QA script and record the unrelated global console blockers separately. Latest gate: CPU 68% and active `dotnet pid 17540`; QA validation returned 0 errors/0 warnings.
Rejected Alternatives: Running `dotnet build` anyway, or editing unrelated Input/Audio/Editor test contracts from the QA watchdog domain.
Scalability potential: N/A; integration/resource gate.
Hardware Impact: Avoided full compiler load on a busy host; runtime effect 0 us.

Problem: Terminal sentinel failure was only reflected as a metric flag; a run that reached 10km could still serialize `Completed` even when `NativeMemorySentinel` reported a leak or threw through reflection.
Solution: Convert terminal sentinel failure into `Failed / NativeSentinelLeak` before capturing the terminal metric. Treat reflection exceptions as sentinel failure so CSV is still written and the failure is visible.
Rejected Alternatives: Keeping `Completed` with a `NativeSentinelFailed` flag, or allowing reflection errors to abort CSV generation entirely.
Scalability potential: Low/Middle/High/Ultra all use the same terminal truth: teardown leak invalidates the run. No device tier can silently downgrade memory integrity.
Hardware Impact: Cold-only terminal branch. Hot simulation cost remains 0 us.

Problem: The batch runner status file only recorded generic `runtime_fault`, which was too weak for unattended CI triage.
Solution: Parse the terminal CSV `fail_reason` column in the editor runner and write cause-bearing statuses such as `runtime_fault_GcAlloc` or `runtime_fault_NativeSentinelLeak`.
Rejected Alternatives: Keeping runner status generic and forcing humans to open the CSV for every failure.
Scalability potential: Weak machines that timeout, fail recorder binding, or fail sentinel teardown get distinct status lines; high-end endurance runs still produce the same `completed` status.
Hardware Impact: Editor-only string split after CSV exists. Runtime game cost 0 us.
