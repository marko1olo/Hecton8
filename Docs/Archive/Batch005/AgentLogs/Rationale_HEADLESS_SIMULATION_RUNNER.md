# Rationale_HEADLESS_SIMULATION_RUNNER

Status: PENDING UNITY RUNTIME VERIFICATION / QA SCOPED-COMPILE CLEAN / GLOBAL EXTERNAL WALLS OUTSIDE DOMAIN

## Decision 0: Disk Memory Bootstrap

Problem: The headless QA assignment requires persistent state before implementation and the required files were absent.
Solution: Create status and rationale files before code work so context compression does not erase assignment state.
Rejected Alternatives: Chat-only tracking is rejected by batch protocol and cannot survive agent handoff.
Scalability potential: Low/Middle/High/Ultra tiers unaffected; this is process infrastructure.
Hardware Impact: 0 runtime cost on i3/MX350. No player build path touched.

## Decision 1: Headless QA Boundary

Problem: The prompt targets CI/CD pipeline work, but the runner must inspect ecology, gas, memory, signals, and AUP without owning those domains.
Solution: Add a QA-owned headless assembly that consumes GlobalRegistry interfaces and GlobalSignals lanes, with only small contract additions where no audit surface exists.
Rejected Alternatives: Direct references to concrete gameplay managers in the simulation loop were rejected because 20+ agents are editing in parallel and registry/event surfaces are the sanctioned coupling points.
Scalability potential: Low uses the same audit route with minimal graphics/audio/UI disabled; Middle/High/Ultra can keep richer ecology/gas math alive while the runner remains unchanged.
Hardware Impact: Low-end i3/MX350 avoids rendering/audio/UI bootstrap work; expected gain is dominated by disabled presentation services, runner hot path remains sub-0.1 ms by design.

## Decision 2: Evidence Filter

Problem: QA output can become fake if logs are described as verified before compile/runtime proof exists.
Solution: Mark checklist work as PENDING VERIFICATION until compile and editor/player evidence are captured; use status file as the durable evidence ledger.
Rejected Alternatives: Chat-only success claims and optimistic estimates are rejected by the QA mandate.
Scalability potential: Low/Middle/High/Ultra unaffected; this controls reporting quality.
Hardware Impact: 0 runtime impact.

## Decision 3: Crash Signal Lane

Problem: The prompt requires CrashTelemetrySignal consumption, but GlobalSignals only had TelemetryAnomalySignal and ProgressionEventSignal lanes.
Solution: Add a 32-byte CrashTelemetrySignal NativeQueue lane with writer, publish, dequeue, validation, and disposal parity with existing signal lanes.
Rejected Alternatives: Reading CrashTelemetryBuffer directly was rejected because it would couple QA to a concrete runtime owner instead of the signal bus.
Scalability potential: Low/Middle/High/Ultra all use the same 32-byte event packet; high-end can produce more postmortem detail through flags without changing queue layout.
Hardware Impact: 64 queued packets at 32 bytes is about 2 KB persistent native memory; MX350/i3 impact is negligible.

## Decision 4: Headless Boot Silence

Problem: Headless CI cannot pay for presentation systems or audio while running a math soak.
Solution: Recognize -h8headless in GameBootstrapper and bypass presentation prewarm plus RenderDispatcher, SpatialAudioManager, ConnectionSplineBatchRenderer, and DebrisManager initialization.
Rejected Alternatives: Initialize-and-disable was rejected because it still allocates GameObjects, render buffers, audio services, and log noise before the runner starts.
Scalability potential: Low tier gets the biggest win by skipping presentation bootstrap; High/Ultra spend saved cycles on actual ecology/gas math instead of invisible rendering.
Hardware Impact: On i3/MX350 the gain is expected to be milliseconds per frame during bootstrap/runtime because graphics/audio/UI work is not created.

## Decision 5: 100x Time Dilation

Problem: Normal gameplay dilation is clamped to 4x, but the assignment requires 100x for CI headless simulation.
Solution: Add RequestHeadlessTimeDilation with a separate 100x clamp and have the runner call it once after dispatcher registration.
Rejected Alternatives: Raising the normal TimeDilationMaximumScalar was rejected because it would let gameplay systems accidentally enter 100x outside QA.
Scalability potential: Low/Middle/High/Ultra share the same time source; High/Ultra can absorb the larger workload while Low remains protected by headless presentation bypass.
Hardware Impact: Cold scalar write only; no measurable per-frame overhead beyond the systems intentionally simulating faster.

## Decision 6: Ghost Player AUP

Problem: CI needs chunk/ecology/AUP movement pressure without spawning the player prefab or UI/camera stack.
Solution: Use a Burst IJob over NativeArray<GhostState> to integrate 3D noise movement in AUP space and swap in LateFrame; publish AUP shift signals at 5000m grid crossings.
Rejected Alternatives: Driving HectonPlayerMovement was rejected because it would initialize player, camera, input, UI, and animation dependencies.
Scalability potential: Low uses the same cheap mathematical ghost; High/Ultra can keep expensive downstream ecosystem/gas math active while the ghost cost stays bounded.
Hardware Impact: Estimated <20 us/job on i3/MX350; no managed hot-loop allocations by code inspection.

## Decision 7: Metrics And Exit Discipline

Problem: Long-run QA must fail deterministically on leaks, gas invalidity, predator extinction, or NaN, and must leave evidence before quit.
Solution: Write daily CSV through a preallocated byte-buffer FileStream, track 10-day H8Memory/NativeMemory growth, check gas pressure arrays, and dump a 300-frame NativeArray blackbox before Application.Quit.
Rejected Alternatives: Debug.Log telemetry and post-exit cleanup were rejected; logs can flood CI and post-exit work is not reliable.
Scalability potential: Low/Middle/High/Ultra use identical CSV/result/blackbox evidence; top-tier can run longer or with shorter day seconds via args.
Hardware Impact: Daily cold IO only; hot loop remains NativeArray/job based. Low-end storage impact is bounded to one CSV row per simulated day and one binary dump on exit.

## Decision 8: Compile Wall Handling

Problem: Unity compile is currently blocked by SaveManager not implementing new IAsyncPersistenceService members owned by another dependency lane.
Solution: Stop changing unrelated SaveManager code, mark Task 18 [BLOCKED BY DEPENDENCY], record exact compiler errors, and leave headless runner scripts targeted-validated.
Rejected Alternatives: Editing SaveManager blindly would cross the CI/CD domain boundary and risk sabotaging persistence work owned by another agent.
Scalability potential: Low/Middle/High/Ultra unaffected; this is dependency hygiene.
Hardware Impact: 0 runtime impact until the dependency wall is resolved.

## Decision 9: Runner Hardening Pass

Problem: The first runner pass was functionally scoped but too loose for a 100-day CI soak: default 86400-second days made the run impractical, startup timeout parsing happened from command-line args during ColdTick, signal drains were unbounded, and daily audits could burst after a hitch.
Solution: Use the project gameplay-day convention of 3600 seconds by default while preserving `-h8headlessDaySeconds`, parse startup timeout once in cold state, cap signal drains at 128 per frame, cap daily audits at 4 per FrostTick, and keep ghost AUP movement advancing before ecology readiness while daily audit accumulation remains gated by ecology readiness.
Rejected Alternatives: A real-time 86400-second default was rejected because a 100-day 100x CI run would take about 24 hours before audit completion; unlimited queue draining was rejected because a producer bug could turn QA into the hitch source.
Scalability potential: Low tier finishes the soak in a tractable window with bounded event/audit work; Middle/High/Ultra can lower day seconds through args or produce denser downstream ecology/gas work without changing the runner.
Hardware Impact: On i3/MX350, the saved worst-case work is bounded to 128 progression events + 128 crash events per FastTick and 4 daily CSV/audit passes per FrostTick; hot-loop managed allocation remains 0 bytes by inspection.

## Decision 10: Compile Evidence Reclassification

Problem: The earlier dependency wall was real at the time, but later evidence showed the QA assembly compiled while MCP console access failed after a WebSocket/domain-reload break.
Solution: Reclassify Task 18 as QA assembly compile verified using `validate_script` 0 diagnostics plus `Library/ScriptAssemblies/Hecton8.QA.Headless.dll` last write at 2026-05-13 18:17:45 and Unity log Csc/ILPP/copy evidence.
Rejected Alternatives: Claiming a fully clean Unity console was rejected because `read_console` failed with "Unity session not ready"; leaving the stale SaveManager blocker unchanged was rejected because current log evidence moved past it.
Scalability potential: Low/Middle/High/Ultra unaffected; this is evidence hygiene and CI readiness tracking.
Hardware Impact: 0 runtime impact. It prevents wasted integrator time chasing stale compile errors against the QA assembly.

## Decision 11: Evidence Artifact Hardening

Problem: The runner could leave ambiguous CI evidence if a stale result survived from an older run, if a batch timeout happened before runtime wrote JSON, or if blackbox dump order wrapped without a reader-visible count/header.
Solution: Delete stale result/CSV/blackbox artifacts before batch start, write timeout fallback JSON from the editor runner, write runtime result JSON through a temporary file before final move, include `evidenceFailureFlags`, and dump blackbox entries oldest-to-newest with magic, count, entry size, and cursor metadata. Batch file cleanup, result reads, and status writes now fail closed instead of throwing through `EditorApplication.update`.
Rejected Alternatives: Relying on console output or final file presence alone was rejected because MCP can disconnect during domain reload and stale artifacts can fake a green CI result.
Scalability potential: Low/Middle/High/Ultra all share identical evidence files. Low-end devices avoid unbounded retry/log loops; top-tier devices can run denser or longer simulations without changing artifact parsing.
Hardware Impact: Normal hot path remains 0 managed bytes by inspection. Added work is cold batch setup or exit IO only; on i3/MX350 the gain is not frame time but false-positive prevention and bounded editor-update failure behavior.

## Decision 12: Scoped Compile Proof

Problem: The active Unity Bee graph `1300b0aEDbg.dag` references a missing `Hecton8.Core.ref.dll` after the global compile stopped on external non-QA errors in Visor, UI, and MacroDatabase code, so a full editor compile cannot currently prove QA code alone.
Solution: Compile current QA source through the previous complete Unity Roslyn response files in `Library/Bee/artifacts/1900b0aEDbg.dag`: `Hecton8.QA.Headless.rsp` and `Hecton8.QA.Headless.Editor.rsp`. Both exited 0 after the latest hardening edits, and generated QA dll/ref artifacts at 2026-05-13 19:40-19:41.
Rejected Alternatives: Editing Visor/UI/MacroDatabase code was rejected because those domains are outside the CI/CD headless QA boundary and are not critical QA cross-domain interfaces. Claiming full project green was rejected because the global compile wall still exists.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; scoped compile proof keeps CI ownership clean while other agents repair their domain.
Hardware Impact: 0 runtime impact. It saves integrator time by separating QA assembly validity from unrelated render-pipeline compile churn.

## Decision 13: Deferred Bootstrap Scene Enforcement

Problem: `HeadlessSimulationBatchRunner.Run` set the headless flag before returning during Unity compilation, but the later `Tick` path could start play mode without reopening `Assets/_Project/Scenes/00_BOOTSTRAP.unity`.
Solution: Add `TryEnsureBootstrapScene` and call it from both immediate and deferred play-entry paths. Missing or failed bootstrap scene open now writes fallback result JSON and stops batch with exit code 1 instead of running an arbitrary active scene.
Rejected Alternatives: Trusting the currently open scene was rejected because CI must be deterministic and scene state in the editor can be stale from any other agent.
Scalability potential: Low/Middle/High/Ultra all run the same bootstrap path. Top-tier visual systems remain bypassed by headless bootstrap; low-tier avoids accidental active-scene presentation load.
Hardware Impact: One cold editor scene path check/open. Runtime frame cost remains 0 us.

## Decision 14: CSV Evidence Fail-Closed

Problem: CSV write errors could escape from `FrostTick`, and a row-buffer overflow could leave partial row bytes that `Dispose` would flush during shutdown.
Solution: Convert daily CSV writes to `TryWriteDailyCsv`, add `EvidenceCsvWriteFailed`, fail with `[CSV_WRITE_FAILED]`, make row overflow explicit, discard pending row bytes on failure, and make CSV dispose non-throwing.
Rejected Alternatives: Allowing partial CSV rows or relying on Unity exception logs was rejected because MCP/console access can fail during domain reload and CI evidence must remain file-authoritative.
Scalability potential: Low/Middle/High/Ultra keep the same hot-loop behavior. On high-end long-run tests, evidence failure stays deterministic instead of corrupting logs after millions of frames.
Hardware Impact: Normal daily CSV path is unchanged except one cold try boundary. Hot `FastTick`/`LateFrameTick` remains 0 managed bytes by inspection.

## Decision 15: Headless Runtime Policy Restoration

Problem: Headless mode forcibly changed editor/player runtime globals and log filtering, which could contaminate later manual play sessions or chained editor jobs after the runner object is destroyed.
Solution: Capture the previous runtime policy once before forcing headless mode, then restore `Application.runInBackground`, `Application.targetFrameRate`, `QualitySettings.vSyncCount`, `Time.captureFramerate`, and `Debug.unityLogger.filterLogType` during `OnDestroy`.
Rejected Alternatives: Leaving globals forced after teardown was rejected because CI jobs and human editor sessions can run in the same Unity process; per-frame policy reapplication was rejected because the values only need cold setup and teardown.
Scalability potential: Low/Middle/High/Ultra all keep the same headless math path while active. High/Ultra visual sessions after a QA run regain their prior presentation policy instead of inheriting a silent unlimited-frame headless profile.
Hardware Impact: 0 us hot-path impact on i3/MX350. Added work is five cold reads at startup and five cold writes at teardown.

## Decision 16: Critical Cross-Domain Compile Wall Triage

Problem: Unity console reported `HectonUnderwaterVisuals` missing the `IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced` member, blocking editor validation evidence for QA even though the file is outside the CI/CD domain.
Solution: Inspect the file with `rg`, confirm only one hot-swap listener implementation remains, and route `DynamicResolutionRuntime` replacement through `RefreshAdaptiveBudgetResponse()` so adaptive-budget cached state is valid after service swaps. Recompile through `Hecton8.Core.rsp` to prove `HectonUnderwaterVisuals` is no longer in the active error set.
Rejected Alternatives: Broad underwater visual refactoring was rejected as domain sabotage. Ignoring the wall was rejected because the error blocked objective QA validation and the fix stayed to the hot-swap interface boundary.
Scalability potential: Low tier keeps cheap adaptive settings current after registry swaps; Middle/High/Ultra can safely rebind richer dynamic-resolution services without stale underwater budget state.
Hardware Impact: 0 us steady-state hot-path impact on i3/MX350. Refresh work is only on rare registry service replacement; global compile remains blocked by separate Visor/SystemDispatcher/Fauna ownership errors.

## OMEGA POLISH CHANGES

Problem: The completed headless QA path still needed the batch `OMEGA_POLISH` audit: anti-bloat inspection, zero-GC scan, build proof, and explicit domain-boundary justification.
Solution: Extracted `POLISH_MANDATE id="OMEGA_POLISH"` from `CURRENT_BATCH.md`, re-scanned `HeadlessSimulationRunner.cs` for `Debug.Log`, `RenderMeshIndirect`, `foreach`, `List`, and `Dictionary`, revalidated both QA scripts, revalidated `HectonUnderwaterVisuals.cs`, reran both scoped QA Roslyn compiles, and ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1`.
Rejected Alternatives: Treating the older "no polish tag" note as authoritative was rejected after direct CLI extraction found the complete tag. Ignoring the red `dotnet build` was rejected; it is recorded as a global missing-type wall outside QA.
Scalability potential: Low keeps the cheapest headless math-only path with restored editor/runtime policy after teardown. Middle/High/Ultra keep High math stress inside the runner while later visual sessions recover their prior frame/log policy. The runner still avoids rendering, audio, UI, camera, and real player dependencies.
Hardware Impact: 0 us added to `FastTick`, `FrostTick`, and `LateFrameTick` on i3/MX350. Runtime-policy capture/restore is cold-only. The scoped QA Roslyn compiles and `dotnet build` are editor/CI proof paths, not runtime cost.

Final Git Diff:
- `Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs`: untracked QA file includes runtime-policy capture/restore at `ForceHeadlessRuntimePolicy`/`RestoreRuntimePolicy`; no hot-loop allocations added.
- `Assets/_Project/Scripts/QA/Headless/Editor/HeadlessSimulationBatchRunner.cs`: untracked QA batch runner remains script-validator clean.
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`: shared dirty file; this pass only requires the hot-swap `DynamicResolutionRuntime` branch to call `RefreshAdaptiveBudgetResponse()`. Other uncommitted hunks in this file pre-existed or are owned by parallel work and were not reverted.
- `Docs/Tasks/Status_HEADLESS_SIMULATION_RUNNER.md`, `Docs/AgentLogs/Rationale_HEADLESS_SIMULATION_RUNNER.md`, `Docs/AgentLogs/LOG_HEADLESS_SIMULATION_RUNNER.md`: updated durable evidence for Loop 10 and OMEGA polish.

## Decision 17: Awaitable Startup Guard

Problem: The runner used `async void Start()`, which can hide exceptions and continue after object teardown if the awaited dispatcher wait resumes late.
Solution: Convert lifecycle entry to `void Start()` and launch `RunStartupAsync(destroyCancellationToken)`. The async body owns its `try/catch`, catches cancellation without failure output, and passes the token into `WaitForDispatcherAndStart`.
Rejected Alternatives: Keeping `async void` was rejected by project async policy. Adding `Update()` polling was rejected because the runner already has a cold `Awaitable` path and gameplay-style Update loops are forbidden.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged while active. The gain is failure containment: cancelled startup no longer leaves a late continuation trying to register into dead editor/player state.
Hardware Impact: 0 us in `FastTick`, `FrostTick`, and `LateFrameTick` on i3/MX350. One cold Awaitable startup continuation remains.

## Decision 18: Deferred Ghost Buffer Disposal

Problem: `OnDestroy` completed the pending ghost job before disposing native arrays. That is safe for correctness but creates an avoidable teardown sync point.
Solution: If the ghost job is pending, unregister the two job-owned ghost arrays from `NativeMemorySentinel` and dispose them through `NativeArray.Dispose(JobHandle)`. Non-job-owned blackbox/memory arrays still dispose immediately.
Rejected Alternatives: Blind immediate disposal was rejected as use-after-free risk. Completing the job just to dispose was rejected because the job scheduler can own the disposal dependency directly.
Scalability potential: Low avoids a teardown stall on weak CPUs. High/Ultra gain nothing visually from a blocking teardown sync, so the saved sync budget stays available for actual simulation/visual work in chained runs.
Hardware Impact: 0 us steady-state frame cost. Teardown avoids a potential worker-thread wait; exact microseconds are not measured because Unity MCP/profiler is unavailable.

## Decision 19: Stack-Formatted Result Numbers

Problem: Runtime result JSON wrote numeric values through `.ToString(CultureInfo.InvariantCulture)`. The path is cold, but OMEGA polish explicitly rejects avoidable managed string formatting in QA runtime evidence code.
Solution: Replace numeric result writes with overloads that use stack `Span<char>` plus `TryFormat`, then write the span directly to `StreamWriter`.
Rejected Alternatives: Leaving `.ToString()` because it is cold was rejected; the runtime runner is QA evidence infrastructure and should not carry avoidable managed formatting debt.
Scalability potential: Low devices avoid exit-path garbage during fail-fast CI loops. High/Ultra devices get cleaner evidence code without changing the simulation path.
Hardware Impact: 0 us in frame hot paths. Exit/result writing uses stack buffers only for numeric formatting.
