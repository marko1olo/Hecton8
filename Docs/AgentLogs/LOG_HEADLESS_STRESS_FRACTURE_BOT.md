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
