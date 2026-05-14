# Rationale_HEADLESS_STRESS_FRACTURE_BOT

Status: PENDING VERIFICATION
Evidence Class: STATIC_DOC

## Decision 0: Domain Boundary
Problem: The prompt requests a destructive stress rig touching boids, AUP, DataVault, dispatcher, and native memory, but the assigned domain is QA/CI.
Solution: Implement as a dedicated test runner/CI harness that uses existing interfaces, registry lookups, or cold-path reflection probes only when interfaces are absent. The runner must not take ownership of gameplay systems.
Rejected Alternatives: Direct edits to boid, AUP, DataVault, or dispatcher internals; those would create cross-domain dependencies and sabotage parallel agents.
Scalability potential: Low = headless minimal render/audio with deterministic fixed load; Middle = same load with broader counters; High = added telemetry snapshots; Ultra = full visual-overkill not applicable because CI headless disables rendering by design.
Hardware Impact: i3/MX350 gains from render/audio silence and bounded telemetry; target is exposing race defects, not adding frame cost to shipped gameplay.

## Decision 1: Evidence Ceiling
Problem: Static source edits cannot prove race-free execution, 0 GC, or Unity player behavior.
Solution: Mark implementation as PENDING VERIFICATION until CLI compile and Unity/headless artifacts exist.
Rejected Alternatives: Reporting success from grep or local compile alone.
Scalability potential: Low/Middle/High/Ultra all use identical evidence labeling; richer tiers only add optional telemetry density.
Hardware Impact: Prevents false-positive QA claims that would waste low-end profiling time.

## Decision 2: Ecosystem Stress Surface
Problem: `IEcosystemDirectorService` exposes read/audit/mutation-pressure methods but no public command to spawn 10,000 boids in one chunk.
Solution: Use existing decoupled signal lanes: `SectorResidencyHydratedSignal` for a single chunk and `SwarmDispersedSignal.EstimatedBoidCount=10000` as the load request. This creates pressure without private-field mutation.
Rejected Alternatives: Reflection into `SargassumMicroFaunaBoids.boidCount`, direct scene search, or editing EcosystemDirector internals; these violate domain boundaries and break IL2CPP safety.
Scalability potential: Low = signal-only data pressure; Middle = same plus biomass audit; High = wider signal volume; Ultra = real spawn requires a future public ecosystem stress API.
Hardware Impact: i3/MX350 avoids forced renderer/GPU boid mutation while still testing queue races; high-end machines can extend by adding a real public stress API later.

## Decision 3: DataVault Free API Gap
Problem: `IDataVault` can allocate/grow named buffers through `GetBuffer<T>` but exposes no release/free operation, so rapid allocate/free in GlobalDataVault cannot be done safely from QA.
Solution: Execute the 50MB churn through `H8Memory.Allocate<byte>`/`H8Memory.Release` and monitor DataVault allocated bytes/fragmentation around synthetic chunk unloads.
Rejected Alternatives: Adding a QA-only `BufferID`, using existing owner buffer IDs, or mutating `GlobalDataVault` with a private free path; each risks corrupting parallel systems.
Scalability potential: Low = 50MB scratch pulse; Middle = configurable pulse size; High = multiple scratch lanes; Ultra = future allocator-owned stress interface for DataVault defrag overkill.
Hardware Impact: i3/MX350 gets bounded 50MB pressure with deterministic release; top-tier can raise frame count or pulse size without changing gameplay code.

## Decision 4: Dispatcher Stall Measurement
Problem: Wall-clock distance between fast ticks is normally near 16.6ms at 60Hz, so using frame-to-frame delta would constantly false-positive against a 16ms threshold.
Solution: Mark a timestamp at the runner's Core fast tick and evaluate elapsed time in the late-frame swap window of the same Unity frame.
Rejected Alternatives: Comparing consecutive fast-tick timestamps or blocking on job handles; both either false-positive or create the stall being measured.
Scalability potential: Low = single stopwatch sample; Middle = add profiler marker readback if Unity session provides it; High/Ultra = wider phase histogram in the blackbox.
Hardware Impact: Stopwatch sample cost is sub-microsecond class on i3/MX350 and negligible on high-end silicon.

## Decision 5: Blackbox and Hot-Path GC
Problem: Race failures must be explainable after process exit, but string logging and managed collections in the stress path would pollute the signal.
Solution: Store the last 300 frames in a fixed `NativeArray<FractureTelemetryEntry>` and only do managed JSON/binary IO on terminal paths.
Rejected Alternatives: Per-frame logs, CSV rows, LINQ summaries, or managed lists; all add GC or IO noise to the test.
Scalability potential: Low = 300 entries at 64 bytes each; Middle = add counters; High = extra native rings; Ultra = separate per-system rings if future stress APIs exist.
Hardware Impact: 19.2KB ring footprint is negligible on i3/MX350 and leaves high-end devices free for larger diagnostic rings.

## Decision 6: Verification Reality
Problem: Unity MCP is unavailable and generated csproj files do not include the headless asmdef, while full project dotnet builds fail on pre-existing dependency errors.
Solution: Compile the new runtime/editor files directly with Unity's Roslyn/Mono against `Library/ScriptAssemblies`, then record full-build failure as unrelated dependency blockage.
Rejected Alternatives: Claiming full compile success, changing unrelated project references, or reverting other agents' pending dependency work.
Scalability potential: Low/Middle/High/Ultra all remain PENDING VERIFICATION until Unity imports and compiles the scripts in-editor.
Hardware Impact: Avoids wasting low-end CI cycles on false root-cause hunts outside this QA runner.

## OMEGA POLISH CHANGES
Problem: Final anti-bloat audit found one same-frame hot division in dispatcher phase timing and confirmed managed string formatting exists only on cold/editor/terminal paths.
Solution: Replaced stopwatch milliseconds conversion with a precomputed reciprocal multiplier. Kept cold H-Phi `ToString` because the metric must be printed before the test and it is outside the stress hot path.
Rejected Alternatives: Removing H-Phi value from CI output, hiding it behind editor-only code, or adding profiler marker readback that would allocate or require unavailable Unity session state.
Scalability potential: Low = reciprocal stopwatch conversion and signal-only pressure; Middle = configure frame count; High = widen native telemetry; Ultra = future public DataVault/Ecosystem stress APIs for visual-overkill stress without private mutation.
Hardware Impact: i3/MX350 saves the per-frame floating division in phase timing; high-end impact is negligible but keeps the hot path mechanically clean.
Cinematic Cheats Used: signal pressure instead of rendered boid spawn, camera/audio purge instead of simulating visual load, H8Memory scratch pulse as DataVault-free substitute, fixed native blackbox instead of per-frame text telemetry.
Final Git Diff: new QA files only plus `Status/Rationale/LOG_HEADLESS_STRESS_FRACTURE_BOT`; no gameplay/core code modified by this agent.

## Decision 7: Hardening Pass After Self-Read
Problem: The first working runner had four production risks: same-frame stall timing sampled only the Core late lane instead of the latest lane, headless camera disable did not restore editor camera state on cleanup, exact H8 allocation-count equality could false-positive if unrelated systems released old allocations during the test, and JSON status text was not escaped.
Solution: Register the late sampler on `PriorityLayer.UI`, snapshot and restore active camera enabled/culling-mask state in cold setup/teardown, treat H8 allocation count as a leak only when it grows above baseline after scratch release, and write terminal status with a bounded JSON escape helper.
Rejected Alternatives: Sampling every late lane, because that adds redundant hot-path calls; broad scene traversal for UI/VFX shutdown, because it violates zero-GC and domain boundaries; exact allocation count equality, because parallel agents and unrelated systems can reduce counts; relying on fixed status constants forever, because terminal artifacts must remain valid JSON.
Scalability potential: Low = final-lane one-sample stall timing and reversible camera silence; Middle = same logic with configurable frame count; High = wider native telemetry counters; Ultra = future Unity player run with public stress APIs and full profiler/GCMonitor capture.
Hardware Impact: i3/MX350 avoids false build failures and persistent editor camera damage while keeping the hot path to one stopwatch delta and one native ring write; high-end hardware gains cleaner CI artifacts rather than extra runtime visual load.

## Decision 8: CI Artifact Robustness
Problem: Headless time dilation can drive multiple fast ticks inside one rendered frame, so resetting the stall timestamp every fast tick under-reported total same-frame simulation cost. CI also needed frame-count override without command-line spacing fragility, valid JSON under control/non-finite values, and failure dumps without the runner's own 50MB scratch allocation contaminating allocation tables.
Solution: Start the phase stopwatch only on the first fast tick observed per rendered frame, support `H8_FRACTURE_FRAMES` and `-h8fractureFrames=...`, release owned scratch after recording the failure blackbox event and before terminal dumps, write non-finite float JSON as `0`, escape low control characters as `\u00XX`, and parse inline frame values with `ReadOnlySpan<char>` instead of `Substring`.
Rejected Alternatives: Dispatcher instrumentation, because it would cross the QA domain boundary; profiler marker readback, because no Unity session is available and it can allocate; keeping the scratch allocation in H8Memory dumps, because it hides real leak suspects; accepting only spaced CLI arguments, because CI wrappers often collapse flags into `name=value`.
Scalability potential: Low = same one-sample watchdog and 50MB bounded scratch; Middle = env-driven frame count for shorter smoke runs; High = longer soak via CI variable; Ultra = public dispatcher/DataVault stress interfaces plus player-profiler capture when Unity execution is available.
Hardware Impact: i3/MX350 gets accurate multi-fast-tick stall detection without extra per-lane instrumentation; high-end machines can increase `H8_FRACTURE_FRAMES` for longer soak without changing source.

## Decision 9: Terminal Lifecycle Closure
Problem: After a pass/fail result, the runner set `_finished` but left registered tick/origin hooks alive until object destruction, and the editor batch runner inferred success by searching for the exact substring `"exitCode":0`.
Solution: Add one idempotent `UnregisterRuntimeHooks()` method and call it from success, failure, and destruction. Parse the result JSON `exitCode` field structurally with span parsing and fail fast when the batch runner start-time session value is corrupt.
Rejected Alternatives: Waiting for `Application.Quit`/play-mode teardown, because editor batch shutdown can take more than one update; using JSON libraries, because the tiny artifact field does not justify extra dependencies or allocations; keeping substring matching, because whitespace or `exitCode` formatting changes would misreport CI.
Scalability potential: Low = deterministic hook shutdown and exact result parsing; Middle = same parser with more result fields; High = structured CI artifact validator; Ultra = Unity-executed batch run with console/profiler attachment when MCP/editor access exists.
Hardware Impact: i3/MX350 avoids stray callback churn during terminal frames; high-end impact is negligible but CI correctness is stronger.

## Decision 10: Stale Flag Activation Guard
Problem: The fallback flag trigger `Temp/H8_FRACTURE_TEST.flag` can survive an editor crash or killed CI process and accidentally start the fracture runner during a later normal play session.
Solution: Treat the flag as valid only when its last-write timestamp is no older than 3 hours, and delete any existing flag before the editor batch runner writes a fresh one.
Rejected Alternatives: Removing the flag path entirely, because batch-mode editor invocation needs a process-local trigger when command-line propagation is unavailable; parsing a rich flag payload, because file mtime is enough and avoids more IO/string surface.
Scalability potential: Low = stale-run prevention with current single-byte flag; Middle = configurable flag TTL; High = signed run token; Ultra = fully structured CI job manifest when the build farm owns the launch protocol.
Hardware Impact: i3/MX350 avoids accidental headless stress activation and wasted long-run heat; high-end benefit is CI correctness, not frame-time.

## Decision 11: H-Phi And CI Artifact Hygiene
Problem: The headless result used a custom H-Phi weighted count instead of the current atlas/runtime audit formula, the swarm stress packet used `GlobalSignals.Publish` even though that wrapper only forwards to the typed `SwarmDispersedSignal` lane, memory totals were sampled through repeated registry reads across artifacts, future-dated flag files could stay valid forever, and malformed result JSON did not leave an explicit runner status.
Solution: Route swarm pressure directly through `SignalBus<SwarmDispersedSignal>.Push(in signal)`, keep AUP/crash wrappers because they own job-admission/private-queue side effects, replace the cold H-Phi scanner with runtime risk-adjusted source counters that exclude editor scripts, split scanner pattern literals so the scanner does not pollute the source audit, centralize memory artifact reads in a `[StructLayout]` `MemorySnapshot`, delete stale or far-future flag triggers, and log `result_exit_code_invalid` when the editor runner sees malformed result JSON.
Rejected Alternatives: Bypassing `GlobalSignals.Publish` for AUP and crash telemetry was rejected because those wrappers mutate job-admission state or private crash queues. Running `dotnet build` was rejected by explicit user instruction. Claiming Unity/player validation from static H-Phi output was rejected as false evidence. Editing DataVault/core APIs for a QA-owned gain was rejected as domain sabotage.
Scalability potential: Low = typed signal ingress, cold H-Phi evidence, and stale-run prevention with no render cost; Middle = same with configurable frame count; High = longer no-code soak through CI variables; Ultra = future public ecosystem/DataVault stress APIs plus player-profiler H-Phi evidence when Unity execution is available.
Hardware Impact: i3/MX350 avoids accidental 50,000-frame stale-flag stress runs and keeps per-frame runner work at one native snapshot write. The direct swarm lane removes one one-shot wrapper call below profiler resolution; the material improvement is cleaner H-Phi synaptic evidence and more consistent terminal artifacts.
Verification: Focused forbidden-pattern scan passed; editor runner isolated compile passed; runtime isolated compile is blocked by stale `Library/ScriptAssemblies` exposing the old `H8Memory.Release(ref NativeArray<T>, JobHandle)` overload while current source defines `Release(ref NativeArray<T>, SystemID)`. PowerShell H-Phi audit completed without `dotnet`: runtime risk `0.000124488`, runtime narrow `0.009266939`.

## Decision 12: Scalable Pressure Controls And H-Phi Self-Debt
Problem: The QA runner defaulted to one fixed 50MB scratch pulse and one fixed 60s bootstrap timeout, which is acceptable for the original DOD but brittle across slow CI, MX350-class machines, and top-tier soak machines. Startup readiness also used two direct `GlobalRegistry.Dispatcher` reads outside the existing dependency cache, and the H-Phi scanner's own tick-method literals could be counted as false Unity tick debt.
Solution: Preserve the default 50MB pressure and 60s timeout, add clamped cold-path overrides through `H8_FRACTURE_SCRATCH_MB`/`-h8fractureScratchMb` and `H8_FRACTURE_STARTUP_TIMEOUT_SECONDS`/`-h8fractureStartupTimeoutSeconds`, write `activationSource`, `scratchBlockBytes`, and `startupTimeoutSeconds` into the result JSON, route startup wait through cached `_dispatcher`, and split scanner tick-method literals. Current source also emits AUP precision integrity fields in the H-Phi artifact and those fields are covered by the current compile/static checks.
Rejected Alternatives: Hardware auto-detection in the runner was rejected because CI should be explicit and reproducible. Editing DataVault or H8Memory APIs was rejected as outside the QA domain. Keeping direct `GlobalRegistry.Dispatcher` polling was rejected because the file already has a cache path and local H-Phi should not pay extra registry surface. Running `dotnet` rebuilds was rejected by explicit user instruction.
Scalability potential: Low = CI can set 8-32MB scratch and longer timeout on weak machines while still exercising the race paths. Middle = default 50MB/60s unchanged. High = 128MB scratch and longer soak windows. Ultra = 256MB scratch with longer bootstrap tolerance when the editor/player environment is slow but the hardware can absorb more memory pressure.
Hardware Impact: i3/MX350 can reduce scratch pressure without source edits and avoid false bootstrap failures on a cold editor. Local source H-Phi improved by reducing direct registry surface from 15 to 13 and scanner false Unity tick count from 3 to 0. Estimated startup saving is below profiler resolution, roughly 1-2 registry property reads per launch; the material gain is reproducible CI pressure scaling and cleaner H-Phi evidence.
Verification: Focused forbidden-pattern scan passed; editor runner isolated compile passed with `UNITY_EDITOR` defined; runtime isolated compile is still blocked only by the stale `Library/ScriptAssemblies` H8Memory overload mismatch at `HeadlessStressFractureBot.cs(582,49)`. No `dotnet` rebuild was run.
