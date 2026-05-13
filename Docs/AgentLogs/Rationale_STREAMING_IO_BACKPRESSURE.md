# STREAMING_IO_BACKPRESSURE Rationale

STATUS: PENDING VERIFICATION

## Session Setup

Problem: Steam Deck MicroSD / slow disk latency can allow the player to outrun chunk residency, exposing unloaded world holes.
Solution: Track Addressables latency in preallocated native timestamp storage, derive `storageDebt01`, route it through registry/signals, and clamp locomotion as a diegetic thick-current pressure.
Rejected Alternatives: Per-load Stopwatch objects, coroutines, WaitUntil, or per-frame polling every handle; all create GC or frame jitter and hide the actual IO bottleneck.
Scalability potential: Low uses clamp plus proxies; Middle loads LOD1 early; High keeps clamp smooth with richer cover-up VFX; Ultra spends saved cycles on visual turbulence while preserving deterministic residency.
Hardware Impact: Expected low-end i3/MX350 benefit is reduced void exposure and lower IO polling cost; measured proof absent because compile is blocked by unrelated project dependency errors.

## Decisions

Problem: Cross-domain consumers needed IO pressure without `IOManager.Instance` or concrete streaming manager references.
Solution: Added `IStreamingBackpressureService` to contracts and registered `WorldChunkResidencyManager` through `GlobalRegistry.StreamingBackpressure`; hot-path consumers use `SystemDispatcher.StreamingStorageDebt01`.
Rejected Alternatives: Singleton manager access, `GlobalRegistry.Get<T>()` inside movement Tick, or direct `WorldChunkResidencyManager` references in player/mount code.
Scalability potential: Low tier reads one scalar and slows; Middle tier also trims prediction; High/Ultra can subscribe to signals for richer feedback without movement coupling.
Hardware Impact: Removes per-consumer lookup pressure; estimated <0.05 us hot-path scalar read.

Problem: IO latency needed broadcast to VFX, UI, telemetry, and systems without managed event churn.
Solution: Added fixed-size `StorageDebtSignal` and `StreamingTurbulenceSignal` lanes in `GlobalSignals`; latest debt is cached as milli-units for scalar readers.
Rejected Alternatives: Managed delegates, UnityEvents, string event names, or per-consumer polling of streaming internals.
Scalability potential: Low can ignore the turbulence lane; High/Ultra can attach particle, audio, and PDA treatments without touching residency code.
Hardware Impact: O(1) signal push, 0 B/frame steady state, estimated 6 us saved on bursty broadcast compared with managed multicast and allocations.

Problem: Load age needed to survive AUP shifts and not allocate per request.
Solution: Added persistent `NativeArray<double> _loadStartTimes` and `NativeArray<byte> _loadImmediateRadiusFlags`, keyed by chunk index, using absolute unscaled time.
Rejected Alternatives: `Stopwatch`, `DateTime`, dictionaries, or transform-derived timing.
Scalability potential: Low tracks only scalar debt; High/Ultra can expose historical latency for more expensive streaming diagnostics.
Hardware Impact: 0 B/frame, estimated 3-8 us saved under load churn by avoiding managed timestamp objects.

Problem: Per-frame Addressables `IsDone` polling scales badly with chunk count and steals time while storage is already late.
Solution: Moved Addressables load and cache-clear polling from Tick into SlowTick, while leaving request dispatch in the streaming cadence.
Rejected Alternatives: Scanning every handle every frame or coroutine waiters per chunk.
Scalability potential: Low tier reduces IO bookkeeping cadence; High tier can spend saved time on visible proxy quality instead of polling.
Hardware Impact: Approx. 83 percent fewer handle scans at 60 FPS versus 10 Hz/SlowTick; estimated 10-25 us/frame average saved at 256-512 definitions.

Problem: Raw latency spikes would jerk player speed if applied directly.
Solution: Applied requested EWMA `math.lerp(_latencyEwmaMs, latencyMs, 0.08f)` and then smoothed published debt with a second `math.lerp` before clamps.
Rejected Alternatives: Last-sample clamp, binary debt thresholds, or immediate MaxSpeed edits outside existing velocity clamping.
Scalability potential: Low gets stable slowdown; Ultra can exaggerate visual current without creating physical jerk.
Hardware Impact: Sub-us math cost; prevents player-control instability from transient IO spikes.

Problem: Immediate-radius chunks are more dangerous than distant speculative chunks.
Solution: Added oldest immediate pending age and `criticalHoleDebt = max(0, oldestPendingMs - 250.0)` into storage debt.
Rejected Alternatives: Treating all pending loads equally or throttling only by pending count.
Scalability potential: Low prioritizes avoiding visible holes; High/Ultra can continue speculative richness when immediate radius is clear.
Hardware Impact: Bounded SlowTick scan; expected win is correctness, not raw CPU.

Problem: Slow storage needs a cinematic cover that does not simulate water truth.
Solution: High debt emits `StreamingTurbulenceSignal` and PDA degraded-link state; movement feels like thick current while residency catches up.
Rejected Alternatives: Real fluid simulation, force fields, or dynamic particle physics as authority.
Scalability potential: Low shows minimal icon/current; Middle adds turbulence; High/Ultra can run richer particles/audio from the same scalar.
Hardware Impact: Avoids a >0.1 ms physical fluid path; signal cost is fixed and small.

Problem: Continuing high-res residency while storage is late creates more debt.
Solution: Debt > 0.25 halves forward prediction; high debt promotes LOD1/collision proxy fallback instead of high-res prefab warming.
Rejected Alternatives: Keep speculative prediction and LOD0 warm under storage saturation.
Scalability potential: Low uses proxy first; Middle recovers to LOD0 when debt falls; High/Ultra can keep overkill visuals when storage is healthy.
Hardware Impact: Estimated 40-200 us avoided during stressed residency spikes plus reduced storage queue pressure.

Problem: Backpressure failures need post-mortem evidence.
Solution: Debt, latency EWMA, oldest pending, and pending count are written to `CrashTelemetryBuffer`; NaN/time faults dump binary data to `Docs/AgentLogs/Dump_STREAMING_IO_BACKPRESSURE.bin`.
Rejected Alternatives: Debug.Log-only errors or "unknown" crash reports.
Scalability potential: Low gets fixed-size black box; High/Ultra can add richer downstream analyzers without changing runtime debt math.
Hardware Impact: Fixed ring writes, 0 B/frame steady state.

Problem: Compile verification could not complete after implementation.
Solution: Ran `dotnet build C:\hades\Hecton8\Hecton8.Core.csproj -v:minimal`, saved the full log, and marked compile as dependency-blocked.
Rejected Alternatives: Fixing unrelated domains inside this batch, reverting other agents' dirty changes, or reporting green compile.
Scalability potential: None; this is build hygiene.
Hardware Impact: None at runtime. Build wall errors include missing `Hecton8.Core.Scheduling`, `Hecton8.Core.Memory.Layout`, `Hecton8.Audio.Propagation`, `IGroundRadarService`, `IInertialNavigationService`, `SoundEmissionSignal`, and `Hecton8.Physics.CCD`.

## Low / Middle / High / Ultra

Low: Scalar speed clamp, LOD1/proxy fallback, half prediction at debt > 0.25, PDA icon only. Looks controlled on weak disk/hardware and avoids holes.
Middle: Same clamp plus turbulence signal consumers and faster recovery to LOD0 when debt drops.
High: Uses saved polling time to keep stronger turbulence, richer proxy visuals, and more aggressive visual masking while storage catches up.
Ultra: Keeps the same deterministic debt scalar but allows overkill VFX/audio responders from `StreamingTurbulenceSignal`; no extra physics authority.

## Regression Model

CPU: Positive in polling cadence; small added SlowTick scans and scalar reads.
GC: Expected 0 B/frame for new paths; measured proof absent.
Memory: Adds two persistent native arrays sized by chunk capacity plus fixed signal lanes.
Cadence: Load completion polling delayed to SlowTick; debt and clamp smoothness reduce abrupt response.
Correctness: Immediate-radius pending loads dominate debt; distant speculative loads do not over-throttle unless latency EWMA is high.
Failure modes: Missing Unity compile due external dependency wall; Unity editor refresh timeout; no runtime GC/frame capture.

## OMEGA POLISH CHANGES

Dear Lie Audit: The runtime response is intentionally fake. High IO debt does not simulate water resistance; it pushes `StreamingTurbulenceSignal`, clamps existing MaxSpeed, and lets the player read the disk stall as thick current/data-link degradation. No 1D LUT replaced the debt formula because the assignment requires live EWMA/oldest-pending terms and the formula is SlowTick scalar math.

Scalability Matrix: Low uses the scalar clamp, proxy fallback, and PDA icon. Middle adds turbulence consumers. High/Ultra can spend saved polling time on visual/audio overkill from `StreamingTurbulenceSignal` while keeping the same deterministic backpressure authority.

Frame Time Dictatorship: Owned storage debt math uses multipliers (`0.0023`, `0.001`, `0.002`) instead of divisions. The recursive clamp audit found the abrupt path risk and the final scalar is smoothed with `math.lerp` before player and mount clamps consume it.

Zero-GC Purge: The owned streaming timestamp path is `NativeArray<double>` plus value fields. Targeted `rg` audit over streaming/movement/UI telemetry files found no new `math.sqrt`, `math.normalize`, managed `foreach`, `string.Format`, or `.ToString()` in the STREAMING_IO_BACKPRESSURE path.

Silo Audit: Cross-domain edits were limited to contracts, registry, signal bus, dispatcher scalar, movement clamp consumers, PDA display, and crash telemetry. This is justified because the backpressure scalar is a cross-domain runtime contract; direct concrete dependencies were avoided.

Cinematic Cheats Used: Thick-current turbulence signal, degraded data-link PDA icon, LOD1/collision proxy fallback, and prediction halving. No physical fluid simulation was added.

Final Git Diff Evidence: `git diff --name-only -- owned paths` in the current shared workspace reports `Assets/_Project/Scripts/Core/GlobalSignals.cs`, `Assets/_Project/Scripts/HectonPlayerMovement.cs`, and this rationale file as active unstaged diff. The primary streaming backpressure anchors are present in HEAD/worktree for `WorldChunkResidencyManager`, `GlobalRegistry`, `GlobalRegistryContracts`, `SystemDispatcher`, `CrashTelemetryBuffer`, `HectonPlayerMotor`, `MountablePlayerTransport`, `PDAShellChrome`, and `Assets/_Project/Scripts/World/Streaming/Hecton8.World.Streaming.asmdef`; `git show HEAD` confirms those anchors already exist in the current index baseline.

Compile Gate: `dotnet build C:\hades\Hecton8\Hecton8.Core.csproj -v:minimal` remains PENDING VERIFICATION due global dependency errors unrelated to the storage debt path. Full log is `Docs/AgentLogs/Build_STREAMING_IO_BACKPRESSURE.log` (ignored by `.gitignore`).
