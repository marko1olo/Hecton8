# STREAMING_IO_BACKPRESSURE Status

PROMPT: STREAMING_IO_BACKPRESSURE
ROLE: STREAMING_ARCHITECT
DOMAIN: World Streaming / Addressables Residency
TASK COUNT: 19
STATUS: PENDING VERIFICATION

## Mandates Selected Before Coding

- [x] ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- [x] STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt
- [x] STRM_World_Streaming_Residency_Chunk_Management.txt
- [x] STRM_DirectStorage_Reality_Check.txt
- [x] STRM_Async_Standard.txt
- [x] OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- [x] DBG_Telemetry_Crash_Reporting_PostMortem.txt
- [x] CORE_Submarine_Vehicles_Kinematics_AUP.txt

## State Machine Checklist

- [x] Task 1: SINGLETON ERADICATION | DOD: Added `IStreamingBackpressureService` and `GlobalRegistry.StreamingBackpressure` registration slot. | Alternative rejected: `IOManager.Instance` singleton. | Estimate: 2 us cold-path lookup avoided per consumer bind.
- [x] Task 2: SIGNAL MIGRATION | DOD: Added `StorageDebtSignal` and typed `SignalBus<StorageDebtSignal>` lane with latest scalar cache. | Alternative rejected: managed C# events and string event IDs. | Estimate: 6 us avoided per broadcast burst plus 0 B/frame.
- [x] Task 3: ASMDEF ISOLATION | DOD: Added `Assets/_Project/Scripts/World/Streaming/Hecton8.World.Streaming.asmdef` referencing World.Contracts and Core. | Alternative rejected: moving active manager files mid-batch. | Estimate: 0 us runtime, lower compile/domain bleed when populated.
- [x] Task 4: DEAD CODE HUNT | DOD: Targeted `rg` audit found no `WaitUntil`/coroutine path in streaming/movement targets; Addressables handle polling moved to `SlowTick`. | Alternative rejected: per-frame wait state and coroutine load gates. | Estimate: 10-25 us/frame average saved at 256-512 chunk definition scale.
- [x] Task 5: LATENCY TRACKING S.O.A. | DOD: Added `NativeArray<double> _loadStartTimes` keyed by chunk index plus immediate-radius byte flags. | Alternative rejected: per-request Stopwatch/class tracking. | Estimate: 0 B/frame and 3-8 us avoided during load churn.
- [x] Task 6: MEASUREMENT | DOD: Load start recorded before `Addressables.LoadAssetAsync`; completion computes `(CurrentUnscaledTimeSeconds - start) * 1000.0`. | Alternative rejected: frame-count approximation. | Estimate: sub-us arithmetic, correct across AUP/floating-origin shifts.
- [x] Task 7: EWMA SMOOTHING | DOD: Latency EWMA now uses `math.lerp(_latencyEwmaMs, latencyMs, 0.08f)`. | Alternative rejected: hard snap to last IO sample. | Estimate: 0.1 us/sample, prevents velocity clamp jitter.
- [x] Task 8: CRITICAL HOLE DEBT | DOD: Immediate-radius pending loads scan oldest age and compute `max(0, oldestPendingMs - 250.0)`. | Alternative rejected: all pending loads treated equal. | Estimate: bounded SlowTick scan only, prevents unloaded near-field holes.
- [x] Task 9: STORAGE DEBT SCALAR | DOD: Implemented saturate formula from EWMA, oldest pending, and critical hole debt. | Alternative rejected: threshold-only binary slowdown. | Estimate: sub-us SlowTick math, smoother player-facing response.
- [x] Task 10: VELOCITY CLAMP | DOD: `SystemDispatcher` publishes scalar; `HectonPlayerMovement` and `MountablePlayerTransport` apply `MaxSpeed *= (1 - debt * 0.8)` through a static helper. | Alternative rejected: direct streaming-manager dependency inside movement. | Estimate: <0.05 us hot-path cached int read.
- [x] Task 11: VISUAL COVER-UP | DOD: High debt publishes `StreamingTurbulenceSignal` for visual-only thick-current cover-up. | Alternative rejected: real fluid/force simulation. | Estimate: avoids >100 us/frame fluid truth path.
- [x] Task 12: PREDICTION HALVING | DOD: When debt > 0.25, velocity-forward prediction distance is halved before scheduling residency. | Alternative rejected: keeping speculative IO pressure constant while storage is behind. | Estimate: IO queue pressure reduction, CPU win depends on active chunks.
- [x] Task 13: PROXY FALLBACK | DOD: High debt promotes chunks as LOD1/proxy fallback and skips high-res prefab warm path. | Alternative rejected: waiting for LOD0 under slow disk. | Estimate: 40-200 us avoided during stressed residency spikes.
- [x] Task 14: ZERO-GC | DOD: Timestamp/debt state uses persistent `NativeArray`/value fields; no new hot-path managed collections. | Alternative rejected: Dictionary/list timestamp bookkeeping. | Estimate: 0 B/frame.
- [x] Task 15: ASYNC HANDLE AGING | DOD: `PollAddressableLoads` and cache-clear polling removed from `Tick` and called from `SlowTick`. | Alternative rejected: `handle.IsDone` scan per chunk every frame. | Estimate: ~83 percent fewer handle scans at 60 FPS with 10 Hz/SlowTick cadence.
- [x] Task 16: AUP SHIFT SAFETY | DOD: Timestamps use absolute unscaled time from `SystemDispatcher.CurrentUnscaledTimeSeconds`, not transform/world position. | Alternative rejected: position-derived or frame-derived latency. | Estimate: correctness win, no measurable CPU cost.
- [x] Task 17: BLACKBOX DUMP | DOD: Debt/latency/pending loads write into `CrashTelemetryBuffer`; NaN/time faults dump to `Docs/AgentLogs/Dump_STREAMING_IO_BACKPRESSURE.bin`. | Alternative rejected: chat/log-only diagnosis. | Estimate: fixed ring write, 0 B/frame steady state.
- [x] Task 18: UI INDICATOR | DOD: PDA chrome has a small degraded data-link icon when debt > 0.6, driven by scalar bucket refresh. | Alternative rejected: TMP string spam or modal notification loop. | Estimate: 0 B/frame during stable bucket; visual state update only on bucket change.
- [BLOCKED BY DEPENDENCY] Task 19: OMEGA COMPILE CHECK | DOD: Ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /p:BuildInParallel=false /clp:ErrorsOnly /v:minimal` and saved `Docs/AgentLogs/Build_STREAMING_IO_BACKPRESSURE.log`. | Alternative rejected: claiming green compile while project has unresolved external batch dependencies/build graph drift. | Estimate: 113 errors before verification, including missing `Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Core.Memory.Layout`, `Hecton8.Audio.Propagation`, `IGroundRadarService`, `IWorldResourceSpawnerReadModel`, `SoundEmissionSignal`, and `Hecton8.Physics.CCD`.

## Verification Loops

- [x] Loop 1: Tasks 1-5 implemented; build attempted; compile blocked by external unresolved namespaces.
- [x] Loop 2: Tasks 6-10 implemented; clamp path audited for direct dependency and GC; build log rechecked.
- [x] Loop 3: Tasks 11-15 implemented; coroutine/WaitUntil audit returned no target hits; polling cadence moved to SlowTick.
- [x] Loop 4: Tasks 16-19 implemented; Unity refresh requested and timed out after 60 s; console unavailable due no Unity session.
- [x] Loop 5: Recursive clamp audit completed; EWMA changed to literal `math.lerp`; published scalar is smoothed before player/mount speed clamp.
- [x] Loop 6: Professional upgrade pass completed; added idle EWMA recovery, hysteresis for prediction/turbulence/proxy/data-link states, and PDA icon hysteresis.

## Verification Evidence

- `rg` coroutine audit: no targeted `WaitUntil`, `StartCoroutine`, `IEnumerator`, or `yield return` hits in streaming/movement files.
- `dotnet build`: failed with 113 existing dependency/build-graph errors; see `Docs/AgentLogs/Build_STREAMING_IO_BACKPRESSURE.log`.
- Unity MCP `refresh_unity`: timed out waiting for editor readiness after 60 s.
- Unity MCP `validate_script` and `read_console`: failed with `no_unity_session`.
- `git diff --check`: existing trailing whitespace in `Docs/Tasks/CURRENT_BATCH.md`; no STREAMING_IO_BACKPRESSURE-owned whitespace issue identified.
- Upgrade static checks: no owned `WaitUntil`, coroutine, `string.Format`, `.ToString()`, `math.sqrt`, or `math.normalize` added to the streaming/PDA patch.
