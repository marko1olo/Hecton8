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
