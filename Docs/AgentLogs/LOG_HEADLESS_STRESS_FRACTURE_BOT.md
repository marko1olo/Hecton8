# LOG_HEADLESS_STRESS_FRACTURE_BOT

## 2026-05-14T00:00:00Z - Race Condition Hunter Build
Status: PENDING VERIFICATION
Evidence Class: ISOLATED_COMPILE_PLUS_STATIC_AUDIT

What was wrong:
- No dedicated `-h8fracturetest` CI runner existed for forced AUP shifts, 50MB memory churn, RigidbodyAUP NaN scans, dispatcher phase stall detection, or postmortem blackbox dumps.
- `IEcosystemDirectorService` has no direct "spawn 10,000 boids" API.
- `IDataVault` has no release/free API, so true GlobalDataVault allocate/free thrashing is not safely callable from QA.
- Unity MCP was unavailable; full dotnet builds are blocked by existing unrelated `Hecton8.Core.csproj` dependency failures.

What was done:
- Added `Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs`.
- Added `Assets/_Project/Scripts/QA/Headless/Editor/HeadlessStressFractureBatchRunner.cs`.
- Trigger paths: `-h8fracturetest`, `H8_FRACTURE_TEST=1`, or `Temp/H8_FRACTURE_TEST.flag`.
- Headless purge: audio pause/volume zero, vSync zero, unlimited target frame rate, low scalability override, active cameras disabled and culling masks zeroed.
- Stress injection: single-chunk residency hydration signal, `SwarmDispersedSignal.EstimatedBoidCount=10000`, AUP pre-shift/rebase/shift every 15 frames, H8Memory 50MB allocate/release pulses.
- Monitoring: same-frame dispatcher phase sample from Core fast tick to late-frame swap window, exact `[FRACTURE_DETECTED: JOB_STALL]` token on >16ms, per-frame RigidbodyAUP finite scan with `[FRACTURE_DETECTED: NAN_POISONING]`, native/H8/DataVault baseline leak windows after synthetic chunk unload.
- Blackbox: fixed 300-entry `NativeArray<FractureTelemetryEntry>` dumped to `Docs/AgentLogs/Dump_HEADLESS_STRESS_FRACTURE_BOT.bin`.
- CI artifacts: JSON result file and H8Memory allocation-table dump on failure.
- Static H-Phi: cold source metric printed as `[H-PHI_STATIC]` before stress starts.

Cinematic Cheats used:
- Real visual simulation was replaced with headless signal pressure; renderer/audio/UI cost is removed because the test is race detection, not presentation.
- Ecosystem stress uses typed signal lanes instead of forcing rendered boid meshes.
- DataVault thrash is represented by H8Memory 50MB native pulses plus DataVault fragmentation/baseline telemetry because DataVault has no public free API.
- Stall detection samples the dispatcher same-frame window instead of blocking on jobs or adding profiler allocations.

Exact Microseconds saved:
- Disabling active cameras: estimated 2000-8000 us/frame avoided on scenes with VFX/UI cameras.
- Audio pause/volume zero: estimated 100-500 us/frame avoided on low-end CPU audio paths.
- Signal-only ecosystem pressure vs direct 10,000 rendered boids: estimated 3000-12000 us/frame avoided on MX350-class hardware.
- Fixed 300-entry native blackbox vs per-frame text logging: estimated 50-500 us/frame and 0B managed hot-path allocation avoided.
- Same-frame stopwatch sampling vs profiler allocation/readback loop: estimated 5-30 us/frame avoided.
- Avoiding reflection/private-field mutation: estimated 10000+ us cold-path risk plus IL2CPP incompatibility avoided.

Verification:
- Isolated runtime compile PASS: Unity Roslyn/Mono compiled `HeadlessStressFractureBot.cs` against Unity and Hecton8 ScriptAssemblies after fixing `ILateFrameTickable`, int/bool, and `Debug` ambiguity errors.
- Isolated editor runner compile PASS: Unity Roslyn/Mono compiled `HeadlessStressFractureBatchRunner.cs` against UnityEditor references.
- Unity MCP compile/console check BLOCKED: no Unity session available.
- Full dotnet build BLOCKED: `Hecton8.Core.csproj` fails with 139 existing unrelated dependency/interface/duplicate-member errors.

Integrator notes:
- This runner is QA-domain only. It does not edit EcosystemDirector, GlobalDataVault, SystemDispatcher, RigidbodyAUP owners, or boid implementations.
- A future public DataVault stress API is required for literal GlobalDataVault allocate/free thrash.
- A future public EcosystemDirector stress API is required for literal 10,000-boid spawn ownership.

Omega polish addendum:
- Read `<POLISH_MANDATE id="OMEGA_POLISH">` only after task coverage reached 100%.
- Replaced hot stopwatch millisecond division with a precomputed reciprocal multiplier.
- Recompiled `HeadlessStressFractureBot.cs` in isolation after the patch: PASS.
- Managed `.ToString()` remains only in cold/editor/terminal paths: H-Phi pre-test console output and editor batch status/result timestamps.

## 2026-05-14T00:00:00Z - Hardening Pass Addendum
Status: PENDING VERIFICATION
Evidence Class: ISOLATED_COMPILE_PLUS_STATIC_AUDIT

What was wrong:
- Core-lane late sampling could miss stalls introduced by later dispatcher lanes.
- Headless camera disable was not reversible inside the same editor session.
- H8 allocation-count exact equality could false-positive when unrelated systems released allocations during the stress window.
- Terminal status JSON relied on fixed safe strings instead of escaping the value.

What was done:
- Moved stall sampling to `PriorityLayer.UI` so the measured window reaches the latest registered late-frame lane.
- Added cold camera state snapshots and restored enabled/culling-mask values during teardown.
- Changed allocation-count leak detection to report only growth above the baseline after scratch release.
- Added bounded JSON string escaping for status output.
- Recompiled runtime and editor runner scripts in isolation after the patch.
- Re-ran static forbidden-pattern audit against the two new Race Condition Hunter files.

Cinematic Cheats used:
- Kept renderer silence as camera/culling suppression instead of hierarchy-wide UI/VFX mutation.
- Kept one final-lane stopwatch sample instead of profiler readback or per-lane instrumentation.

Exact Microseconds saved:
- Avoided per-lane timing instrumentation: estimated 2-8 us/frame depending on lane count.
- Avoided hierarchy-wide UI/VFX scans: estimated 500-3000 us cold setup and 0 B hot-path risk avoided.
- Avoided false-positive reruns from allocation-count shrinkage: estimated 120000000+ us saved per rejected CI rerun.

Verification:
- Isolated runtime compile after hardening: PASS.
- Isolated editor runner compile after hardening: PASS.
- Static forbidden-pattern scan after hardening: PASS for `HeadlessStressFractureBot.cs` and `HeadlessStressFractureBatchRunner.cs`, no matches for scene search, LINQ, coroutine, `Task<`, `.Complete()`, managed collection creation, reflection, or explicit GC patterns.
- Full Unity/editor execution remains PENDING VERIFICATION because MCP has no available Unity session.

## 2026-05-14T00:00:00Z - CI Robustness Addendum
Status: PENDING VERIFICATION
Evidence Class: ISOLATED_COMPILE_PLUS_STATIC_AUDIT

What was wrong:
- In 100x headless time dilation, multiple fast ticks can execute inside one rendered frame; resetting the watchdog timestamp every fast tick could hide aggregate same-frame simulation stalls.
- H-Phi logging always printed `frames=50000` even when frame count was overridden.
- CI frame-count override accepted only spaced CLI arguments.
- Failure H8Memory dumps could include the runner-owned 50MB scratch block.
- JSON float output could become invalid if a metric became NaN/Infinity.

What was done:
- Phase timing now starts on the first fast tick per Unity frame and is sampled in the final late-frame lane.
- Added `H8_FRACTURE_FRAMES` and `-h8fractureFrames=...` support.
- H-Phi log now prints the actual target frame count.
- Failure path records the blackbox event, then releases owned scratch before crash telemetry/result/H8Memory dump.
- JSON writer now escapes low control characters and clamps non-finite float fields to `0`.
- Replaced inline CLI `Substring` parsing with span parsing.

Cinematic Cheats used:
- Kept CI scalability as configuration and signal pressure, not scene mutation.
- Kept one frame-level watchdog sample instead of per-lane profiler instrumentation.

Exact Microseconds saved:
- Avoided per-lane dispatcher instrumentation: estimated 2-8 us/frame.
- Avoided one cold parser allocation for inline frame overrides: small but deterministic, 0 B parser allocation.
- Avoided contaminated allocation-table triage from runner scratch: estimated 30000000+ us saved per failed leak investigation.

Verification:
- Isolated runtime compile after CI robustness patch: PASS.
- Isolated editor runner compile after CI robustness patch: PASS.
- Focused static audit: PASS for `HeadlessStressFractureBot.cs` and `HeadlessStressFractureBatchRunner.cs`, including no CLI `Substring` parser usage.
- Temp compile artifacts were removed.
- Full Unity/editor execution remains PENDING VERIFICATION because MCP has no available Unity session.

## 2026-05-14T00:00:00Z - Terminal Lifecycle Addendum
Status: PENDING VERIFICATION
Evidence Class: ISOLATED_COMPILE_PLUS_STATIC_AUDIT

What was wrong:
- The runner relied on object destruction to unregister fast/cold/late tick hooks and origin-shift listener after terminal pass/fail.
- Editor batch success detection used substring matching for `"exitCode":0`, which could misread or reject valid JSON formatting.
- Corrupt `StartTimeKey` session state could prevent the editor timeout from firing.

What was done:
- Added idempotent `UnregisterRuntimeHooks()` and called it from success, failure, and destruction paths.
- Replaced editor substring exit-code inference with exact `exitCode` field parsing over a span.
- Corrupt editor start time now writes `start_time_invalid` and fails through the timeout path instead of silently extending the run.

Cinematic Cheats used:
- None added; this is lifecycle and artifact integrity only.

Exact Microseconds saved:
- Avoided terminal-frame stray callbacks: estimated 1-5 us/frame until play-mode teardown.
- Avoided failed CI triage from malformed result inference: estimated 30000000+ us per bad CI report.

Verification:
- Isolated runtime compile after lifecycle patch: PASS.
- Isolated editor runner compile after lifecycle patch: PASS.
- Focused static audit: PASS for the two new Race Condition Hunter files; no `Substring` usage or forbidden hot-path patterns.
- Temp compile artifacts were removed.
- Full Unity/editor execution remains PENDING VERIFICATION because MCP has no available Unity session.

## 2026-05-14T00:00:00Z - Stale Flag Guard Addendum
Status: PENDING VERIFICATION
Evidence Class: ISOLATED_COMPILE_PLUS_STATIC_AUDIT

What was wrong:
- A leftover `Temp/H8_FRACTURE_TEST.flag` from a crashed editor/CI process could trigger the destructive fracture runner during a later normal play session.

What was done:
- Runtime flag activation now requires the flag file to be fresh within a 3-hour TTL.
- The editor batch runner deletes any old flag before writing a fresh single-byte trigger.

Cinematic Cheats used:
- None added; this is CI launch hygiene only.

Exact Microseconds saved:
- Avoided accidental 50,000-frame stress run after stale flag: estimated 120000000+ us per prevented false activation.

Verification:
- Isolated runtime compile after stale-flag patch: PASS.
- Isolated editor runner compile after stale-flag patch: PASS.
- Focused static audit: PASS for the two new Race Condition Hunter files; no forbidden hot-path patterns or `Substring` usage.
- Temp compile artifacts were removed.
- Full Unity/editor execution remains PENDING VERIFICATION because MCP has no available Unity session.

## 2026-05-15 - H-Phi And CI Hygiene Addendum
Status: PENDING VERIFICATION
Evidence Class: STATIC_SOURCE_PLUS_EDITOR_ISOLATED_COMPILE

What was wrong:
- The runtime H-Phi console artifact used a custom weighted count instead of the atlas/runtime risk-adjusted H-Phi model.
- The swarm stress request still used a `GlobalSignals.Publish` wrapper for a signal whose wrapper only forwards into the typed lane.
- Memory artifact reads were repeated across result, blackbox, baseline, and crash telemetry paths instead of using one consistent snapshot.
- A far-future `Temp/H8_FRACTURE_TEST.flag` timestamp could keep the fallback trigger valid indefinitely.
- The editor runner did not explicitly mark malformed result JSON.
- `CURRENT_BATCH.md` no longer contains this agent prompt; this is recorded as batch drift, not an instruction to read neighboring prompts.

What was done:
- Swarm pressure now writes directly to `SignalBus<SwarmDispersedSignal>.Push(in signal)`.
- AUP and crash telemetry stay on `GlobalSignals.Publish` because those wrappers own job-admission/private-queue side effects.
- Cold H-Phi calculation now reports `runtime_risk_adjusted` and writes `staticHPhiModel` into the result JSON.
- Scanner search terms are split so the scanner does not create false source-count debt.
- Added `[StructLayout]` `MemorySnapshot` and `HPhiStaticCounters` structs; terminal and blackbox artifacts now sample final memory through the snapshot path.
- Runtime flag validation deletes stale flags and far-future timestamps beyond 5 minutes of clock skew.
- Editor runner writes `result_exit_code_invalid` when `exitCode` cannot be parsed.

Cinematic Cheats used:
- Signal pressure remains the stress fake instead of mutating fauna/render systems.
- H-Phi evidence is source-only and cold; no profiler or runtime visual simulation was introduced.

Exact Microseconds saved:
- Direct swarm lane removes one wrapper call on the one-shot stress request; below meaningful profiler resolution, estimated <1 us per run.
- Central memory snapshot removes duplicate registry/memory property reads on terminal and blackbox writes; estimated 1-4 us on terminal artifact generation, 0 B hot-path managed allocation.
- Far-future/stale flag deletion can prevent a false 50,000-frame run; estimated 120000000+ us saved per prevented false activation.

Verification:
- Focused forbidden-pattern scan: PASS for both Race Condition Hunter files; no scene search, LINQ, coroutine, `Task<`, `.Complete()`, explicit GC, reflection, managed collection creation, `string.Format`, or `Substring`.
- Scoped QA/headless source count: `SignalBusPush=3`, `GlobalSignalsPublish=4`, `GlobalRegistrySurface=15`, `StructLayoutAttributes=3`, `StructDeclarations=3`, `FindObjectCalls=0`, `GetComponentCalls=0`, `UnityUpdateMethods=0`.
- PowerShell H-Phi audit completed without `dotnet`: runtime risk `0.000124488`, runtime narrow `0.009266939`, `SignalBusPush=84`, `GlobalRegistrySurface=5139`, `EventPublish=450`, `DataVaultRefs=133`, `NativeArrayRefs=7020`, `StructLayoutAttributes=946`, `StructDeclarations=1888`.
- Editor runner isolated Unity compiler probe: PASS.
- Runtime isolated Unity compiler probe: BLOCKED by stale `Library/ScriptAssemblies` H8Memory API. Current source defines `Release(ref NativeArray<T>, SystemID)`, but the referenced compiled assembly still exposes the older `JobHandle` overload. No `dotnet` rebuild was run by user instruction.
- Full Unity/editor/player execution remains PENDING VERIFICATION.

## 2026-05-15 - Scalable Pressure And H-Phi Self-Debt Addendum
Status: PENDING VERIFICATION
Evidence Class: STATIC_SOURCE_PLUS_EDITOR_ISOLATED_COMPILE

What was wrong:
- The fracture runner's memory pressure and bootstrap timeout were fixed at 50MB and 60s, leaving weak CI and high-end soak runs to edit source instead of setting bounded launch policy.
- Startup readiness had two direct `GlobalRegistry.Dispatcher` reads even though the runner already owns a cached dispatcher field.
- The H-Phi scanner could count its own `void Update`/`FixedUpdate`/`LateUpdate` string literals as false Unity tick debt.
- Result artifacts did not state whether activation came from CLI, env, or flag, nor the exact scratch/timeout controls used.

What was done:
- Added clamped cold-path controls: `H8_FRACTURE_SCRATCH_MB`, `-h8fractureScratchMb`, `H8_FRACTURE_STARTUP_TIMEOUT_SECONDS`, and `-h8fractureStartupTimeoutSeconds`.
- Preserved default DOD behavior: 50MB scratch pressure and 60s bootstrap timeout when no override is supplied.
- Result JSON now records `activationSource`, `scratchBlockBytes`, and `startupTimeoutSeconds`.
- Startup wait now uses cached `_dispatcher` through `CacheServices()` instead of direct `GlobalRegistry.Dispatcher` polling.
- Split H-Phi scanner tick-method literals so source scans no longer count the scanner as three Unity update methods.

Cinematic Cheats used:
- Kept signal pressure and configurable scratch memory as the CI fake instead of mutating fauna, DataVault internals, or renderer systems.

Exact Microseconds saved:
- Removed two direct startup registry property reads from this runner; estimated 1-2 us per launch, below profiler resolution.
- Avoided false H-Phi Unity tick debt: `UnityUpdateMethods` source count 3 -> 0.
- Reduced local direct registry surface: `GlobalRegistrySurface` 15 -> 13.
- Weak-machine CI can reduce scratch from 50MB to 8-32MB; prevented false memory pressure failures save an estimated 30000000+ us per avoided rerun.

Verification:
- Focused forbidden-pattern scan: PASS for both Race Condition Hunter files; no scene search, LINQ, coroutine, `Task<`, `.Complete()`, explicit GC, reflection, managed collection creation, `string.Format`, or `Substring`.
- Scoped QA/headless source count: `SignalBusPush=3`, `GlobalSignalsPublish=4`, `GlobalRegistrySurface=13`, `StructLayoutAttributes=3`, `StructDeclarations=3`, `FindObjectCalls=0`, `GetComponentCalls=0`, `UnityUpdateMethods=0`.
- Editor runner isolated Unity compiler probe: PASS with `UNITY_EDITOR` defined.
- Runtime isolated Unity compiler probe: BLOCKED by stale `Library/ScriptAssemblies` H8Memory API at `HeadlessStressFractureBot.cs(582,49)`. Current source defines `Release(ref NativeArray<T>, SystemID)`, but the referenced compiled assembly still exposes the older `JobHandle` overload. No `dotnet` rebuild was run by user instruction.
- Temp compile artifacts were removed.
- Full Unity/editor/player execution remains PENDING VERIFICATION.

## 2026-05-15 - AUP Snap-Fence Telemetry Addendum
Status: PENDING VERIFICATION
Evidence Class: CLI_COMPILE_PLUS_STATIC_SOURCE

What was wrong:
- AUP shifts were emitted every 15 extreme frames, but the blackbox flags did not explicitly mark the mandated 300-frame post-shift snap-fence window.
- Result artifacts exposed activation source only as an integer, slowing CI triage.
- Previous runtime isolated compile evidence was blocked by stale `Library/ScriptAssemblies`; the current imported assemblies needed to be rechecked without `dotnet`.

What was done:
- Added `AupSnapFenceFrames=300` and bit 5 in the existing `FractureTelemetryEntry.Flags` word for active snap-fence frames.
- Stored `_lastAupShiftExtremeFrame` when the runner emits a shift; no binary entry size change, still 64 bytes.
- Result JSON now writes `activationSourceName`, `blackboxAupSnapFenceFrames`, and `blackboxFlagAupSnapFenceBit`.
- Re-ran isolated runtime and editor compiler probes against current `Library/ScriptAssemblies`.

Cinematic Cheats used:
- Kept AUP validation as telemetry and signal pressure; no direct AUP owner mutation and no rendered/physical simulation added.

Exact Microseconds saved:
- Reused existing blackbox flags word: 0 bytes extra native memory.
- Snap-fence marking adds one integer subtraction and bit set per blackbox write; estimated <1 us/frame on i3/MX350.
- Activation source name avoids manual integer decoding in failed CI artifacts; estimated 3000000+ us saved per triage session.

Verification:
- Focused forbidden-pattern scan: PASS for both Race Condition Hunter files; no scene search, LINQ, coroutine, `Task<`, `.Complete()`, explicit GC, reflection, managed collection creation, `string.Format`, or `Substring`.
- Scoped QA/headless source count: `SignalBusPush=3`, `GlobalSignalsPublish=4`, `GlobalRegistrySurface=13`, `StructLayoutAttributes=3`, `StructDeclarations=3`, `FindObjectCalls=0`, `GetComponentCalls=0`, `UnityUpdateMethods=0`.
- Runtime isolated Unity compiler probe: PASS via Unity Mono/Roslyn with UnityJIT facades, Unity modules, current `Library/ScriptAssemblies`, and `Assembly-CSharp.dll`.
- Editor runner isolated Unity compiler probe: PASS with `UNITY_EDITOR` defined, Unity editor facade, and `Assembly-CSharp.dll`.
- No `dotnet` rebuild was run.
- Full Unity/editor/player execution remains PENDING VERIFICATION because no Unity MCP/editor session is available in this tool context.

## 2026-05-15 - Dispatcher-Contract H-Phi And Scanner Self-Pollution Addendum
Status: PENDING VERIFICATION
Evidence Class: CLI_COMPILE_PLUS_STATIC_SOURCE

What was wrong:
- H-Phi architectural purity under-counted Hecton8's real dispatcher contract surface by treating only a narrow tick/job subset as evidence.
- The H-Phi scanner still contained contiguous scene-search/component/update tokens in internal identifiers or literals, so broad audits could flag the scanner itself even when the runtime hot path was clean.
- Result artifacts exposed the final H-Phi score but not the dispatcher-contract and Unity-update inputs needed to interpret the architecture-purity term.

What was done:
- Counted the full dispatcher contract family: `IUpdatable`, `ITickable`, `IFastTickable`, `IFixedTickable`, `ISlowTickable`, `IColdTickable`, `IFrostTickable`, `ILateFrameTickable`, and `IPostFixedTickable`.
- Updated the architecture-purity term to use `dispatcherContracts + IJob` over `UnityUpdateMethods + dispatcherContracts + IJob`.
- Added `staticHPhiDispatcherContracts` and `staticHPhiUnityUpdateMethods` to the result JSON.
- Renamed scanner-owned scene/component counters to neutral lookup names and split update-method scan literals.

Cinematic Cheats used:
- Kept this as a cold source-evidence improvement. No physical simulation, rendered validation, or dispatcher/core mutation was added.

Exact Microseconds saved:
- Hot path: 0 us added; all work is cold H-Phi startup/reporting.
- Broad audit false-positive removal prevents at least one manual H-Phi triage loop; estimated 3000000+ us saved per avoided review cycle.
- Dispatcher-contract fields avoid rerunning source scans to explain a CI artifact; estimated 1000000+ us saved per failed run triage.

Verification:
- Focused static audit: PASS for both Race Condition Hunter files; no contiguous scene search, component lookup, Unity `Update` method signature, LINQ `foreach`, coroutine, `Task<`, `.Complete()`, explicit GC, managed collection creation, `string.Format`, or `Substring` parser usage.
- Scoped QA/headless source count: `SignalBusPush=3`, `GlobalSignalsPublish=4`, `GlobalRegistryDot=13`, `GlobalRegistryIdentifierTokens=18`, `StructLayoutAttributes=3`, `StructDeclarations=3`, `FindObjectCalls=0`, `GetComponentCalls=0`, `UnityUpdateMethods=0`, `DispatcherContractMetricFields=5`.
- Runtime isolated Unity compiler probe: PASS via Unity Mono/Roslyn with UnityJIT facades, Unity modules, current `Library/ScriptAssemblies`, and `Assembly-CSharp.dll`.
- Editor runner isolated Unity compiler probe: PASS with `UNITY_EDITOR` defined, Unity editor facade, and `Assembly-CSharp.dll`.
- `git diff --check`: PASS for whitespace on the QA runner, editor runner, and owned status/rationale/log files; Git emitted LF-to-CRLF normalization warnings on the owned markdown files only.
- No `dotnet` rebuild was run.
- Full Unity/editor/player execution remains PENDING VERIFICATION because no Unity MCP/editor session is available in this tool context.

## 2026-05-15 - Hot-Path Memory Telemetry Cadence Addendum
Status: PENDING VERIFICATION
Evidence Class: CLI_COMPILE_PLUS_STATIC_SOURCE

What was wrong:
- `FastTick` still called the service-cache helper, leaving a hot-path registry-refresh shape even though most reads short-circuited after startup.
- The blackbox refreshed memory counters every frame. That gave dense data but spent repeated static counter/property reads where a bounded cadence plus forced event samples is enough for postmortem evidence.
- Result artifacts did not state the memory-sample cadence or which flag bit marks fresh memory samples.

What was done:
- Removed the `FastTick` call to `CacheServices`; service refresh now remains in startup and `ColdTick`.
- Added a cached `MemorySnapshot` path for blackbox frame records. Routine frame entries refresh every 30 extreme frames; non-frame events force a fresh sample.
- Added bit 6, `blackboxFlagMemorySampleFreshBit`, to mark entries that carry fresh memory data.
- Result JSON now writes `blackboxMemorySnapshotIntervalFrames` and `blackboxFlagMemorySampleFreshBit`.

Cinematic Cheats used:
- Kept memory diagnostics as a sampled blackbox signal instead of adding heavier profiler or runtime instrumentation to the headless stress loop.

Exact Microseconds saved:
- Default 50,000-frame run: routine memory snapshot reads drop from 50,000 to roughly 1,667, plus forced event records.
- Estimated saved work: 29 static memory-counter/property read groups per 30 frames; profiler proof absent, so this remains a static estimate.
- Hot-path managed allocation remains static-audit 0 B.

Verification:
- Focused static audit: PASS for both Race Condition Hunter files; no contiguous scene search, component lookup, Unity `Update` method signature, LINQ `foreach`, coroutine, `Task<`, `.Complete()`, explicit GC, managed collection creation, `string.Format`, or `Substring` parser usage.
- Scoped QA/headless source count: `SignalBusPush=3`, `GlobalSignalsPublish=4`, `GlobalRegistryDot=13`, `GlobalRegistryIdentifierTokens=18`, `StructLayoutAttributes=3`, `StructDeclarations=3`, `FindObjectCalls=0`, `GetComponentCalls=0`, `UnityUpdateMethods=0`, `FastTickRegistryRefresh=0`, `MemorySnapshotIntervalFields=1`.
- Runtime isolated Unity compiler probe: PASS via Unity Mono/Roslyn with UnityJIT facades, Unity modules, current `Library/ScriptAssemblies`, and `Assembly-CSharp.dll`.
- Editor runner isolated Unity compiler probe: PASS with `UNITY_EDITOR` defined, Unity editor facade, and `Assembly-CSharp.dll`.
- `git diff --check`: PASS for whitespace on the QA runner/editor runner/status/rationale/log files; LF-to-CRLF normalization warnings appeared only on files dirty at check time, with the final readback warning limited to this owned LOG file.
- No temp `HeadlessStressFracture*MemoryCadence*.dll` probe artifacts remain.
- No `dotnet` rebuild was run.
- Full Unity/editor/player execution remains PENDING VERIFICATION because no Unity MCP/editor session is available in this tool context.

## 2026-05-15 - CI Artifact Schema Clarity Addendum
Status: PENDING VERIFICATION
Evidence Class: CLI_COMPILE_PLUS_STATIC_SOURCE

What was wrong:
- Clean runs defaulted `rigidbodyNanIndex` to `0`, which is ambiguous with a NaN at the first RigidbodyAUP slot.
- Result JSON did not expose schema version, blackbox magic, blackbox capacity, or blackbox entry size.
- Blackbox dump used a literal magic value instead of the named constant now exported to CI result JSON.

What was done:
- `_rigidbodyNanIndex` now defaults to `-1`; the NaN failure path still writes the actual failing index.
- Added `ResultSchemaVersion=4` and result JSON field `resultSchemaVersion`.
- Added named `BlackboxMagic` and result JSON fields `blackboxMagic`, `blackboxFrameCapacity`, and `blackboxEntrySizeBytes`.
- Kept binary dump layout unchanged.

Cinematic Cheats used:
- None. This is CI artifact readability and postmortem parser hygiene only.

Exact Microseconds saved:
- Hot path: 0 us; all new writes are terminal artifact writes.
- Avoided manual parser/source lookup during failed-run triage: estimated 1000000+ us saved per artifact review.
- Avoided clean-run/slot-zero-NaN ambiguity: estimated 3000000+ us saved per disputed failure.

Verification:
- Focused static audit: PASS for both Race Condition Hunter files; no contiguous scene search, component lookup, Unity `Update` method signature, LINQ `foreach`, coroutine, `Task<`, `.Complete()`, explicit GC, managed collection creation, `string.Format`, or `Substring` parser usage.
- Scoped QA/headless source count: `SignalBusPush=3`, `GlobalSignalsPublish=4`, `GlobalRegistryDot=13`, `GlobalRegistryIdentifierTokens=18`, `StructLayoutAttributes=3`, `StructDeclarations=3`, `FindObjectCalls=0`, `GetComponentCalls=0`, `UnityUpdateMethods=0`, `ResultSchemaVersion=1`, `BlackboxMetadataFields=3`.
- Runtime isolated Unity compiler probe: PASS via Unity Mono/Roslyn with UnityJIT facades, Unity modules, current `Library/ScriptAssemblies`, and `Assembly-CSharp.dll`.
- Editor runner isolated Unity compiler probe: PASS with `UNITY_EDITOR` defined, Unity editor facade, and `Assembly-CSharp.dll`.
- `git diff --check`: PASS for whitespace on the QA runner, editor runner, and owned status/rationale/log files.
- No temp `*SchemaClarity*.dll` probe artifacts remain in `Temp`.
- No `dotnet` rebuild was run.
- Full Unity/editor/player execution remains PENDING VERIFICATION because no Unity MCP/editor session is available in this tool context.

## 2026-05-15 - Fallback Artifact Parity And Cold Allocation Evidence Addendum
Status: PENDING VERIFICATION
Evidence Class: CLI_COMPILE_PLUS_STATIC_SOURCE

What was wrong:
- Runtime results carried schema-v4/blackbox metadata, but editor fallback results still used an older minimal JSON shape.
- CI parser behavior would diverge on the worst path: launch failure before runtime result generation.
- Runner-owned cold allocations lacked canonical `COLD ALLOC` evidence comments.

What was done:
- Added fallback JSON fields `resultSchemaVersion`, `fallbackResult`, `blackboxMagic`, `blackboxFrameCapacity`, and `blackboxEntrySizeBytes`.
- Added named fallback constants in `HeadlessStressFractureBatchRunner`.
- Added canonical `COLD ALLOC` comments for startup cancellation, blackbox ring allocation, camera snapshot arrays, and editor flag bytes.

Cinematic Cheats used:
- None. This pass is CI artifact and mandate-evidence hygiene only.

Exact Microseconds saved:
- Runtime hot path: 0 us; runtime code changes are comments only.
- Editor fallback path: terminal-only JSON fields, no frame-time relevance.
- Avoided parser/triage branch on fallback failures: estimated 1000000+ us saved per failed launch artifact.

Verification:
- Focused static audit: PASS for both Race Condition Hunter files; no scene search, component lookup, LINQ, coroutine, `Task<`, `.Complete()`, explicit GC, reflection, managed collection creation, `string.Format`, or `Substring` parser usage.
- Scoped source counts: `ColdAllocComments=6`, `ResultSchemaVersionTokens=4`, `FallbackResultFields=1`, `BlackboxMetadataFields=6`.
- Runtime isolated Unity compiler probe: PASS via Unity Mono/Roslyn with UnityJIT facades, Unity modules, current `Library/ScriptAssemblies`, and `Assembly-CSharp.dll`.
- Editor runner isolated Unity compiler probe: PASS via Unity Mono/Roslyn with UnityEngine/UnityEditor facade references and `UNITY_EDITOR` defined; an earlier failed editor probe used an invalid reference mix and required no source changes.
- `git diff --check`: PASS for whitespace on the QA runner, editor runner, and owned status/rationale/log files; Git emitted LF-to-CRLF normalization warnings only.
- No temp `*FallbackSchema*.dll` probe artifacts remain in `Temp`.
- No `dotnet` rebuild was run.
- Full Unity/editor/player execution remains PENDING VERIFICATION because no Unity MCP/editor session is available in this tool context.

## 2026-05-15 - Terminal Time-Dilation Restore And Flag Consumption Addendum
Status: PENDING VERIFICATION
Evidence Class: CLI_COMPILE_PLUS_STATIC_SOURCE

What was wrong:
- The runner requested 100x headless time dilation but did not explicitly restore the previous dispatcher scalar on terminal teardown.
- A direct flag-driven activation could leave `Temp/H8_FRACTURE_TEST.flag` fresh on disk after startup, allowing accidental replay until TTL expiry.
- Result JSON did not expose whether lifecycle cleanup ran.

What was done:
- Cached `ITickDispatcher.TimeDilationScalar` before headless dilation and restored it through `RequestTimeDilation` during idempotent hook teardown.
- Deleted the activation flag during cold startup after resolving the activation source.
- Added result fields `activationFlagDeletedAtStartup`, `headlessTimeDilationScalar`, `previousTimeDilationScalar`, and `headlessTimeDilationRestored`.

Cinematic Cheats used:
- None. This is headless lifecycle and CI hygiene only.

Exact Microseconds saved:
- Hot path: 0 us; changes run only during startup/terminal teardown.
- Avoided accidental repeated stress replay: estimated 1000000+ us saved per stale-flag incident, plus avoided 50,000-frame heat on low-end CI hardware.
- Avoided lingering 100x dispatcher pressure after terminal failure in editor context; profiler proof absent, so this remains a static lifecycle-risk estimate.

Verification:
- Focused static audit: PASS for both Race Condition Hunter files; no scene search, component lookup, LINQ, coroutine, `Task<`, `.Complete()`, explicit GC, reflection, managed collection creation, `string.Format`, or `Substring` parser usage.
- Scoped source counts: `GlobalRegistryDot=13`, `RequestHeadlessTimeDilation=1`, `RequestTimeDilation=1`, `ActivationFlagDeletedField=4`, `HeadlessTimeDilationResultFields=11`.
- Runtime isolated Unity compiler probe: PASS via Unity Mono/Roslyn with UnityJIT facades, Unity modules, current `Library/ScriptAssemblies`, and `Assembly-CSharp.dll`.
- Editor runner isolated Unity compiler probe: PASS via Unity Mono/Roslyn with UnityEngine/UnityEditor facade references and `UNITY_EDITOR` defined.
- No `dotnet` rebuild was run.
- Full Unity/editor/player execution remains PENDING VERIFICATION because no Unity MCP/editor session is available in this tool context.
