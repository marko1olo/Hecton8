# Status_CORE_TELEMETRY

Agent: STABILITY_WATCHDOG
Prompt ID: CORE_TELEMETRY
Domain: CORE & MEMORY INFRASTRUCTURE / Crash Telemetry + Scalability Dictator
Task Count: 20
Status: PENDING VERIFICATION

## Hygiene
- [x] Prompt extracted from `Docs/Tasks/CURRENT_BATCH.txt`, then re-extracted from `Docs/Tasks/CURRENT_BATCH.md` using CLI regex. DOD: strict XML isolation by `id="CORE_TELEMETRY"`. Alternative rejected: reading neighboring prompts or inferred tasks. Estimate: 80 us.
- [x] Fresh status file created. DOD: disk-backed anti-amnesia state. Alternative rejected: chat-only progress. Estimate: 35 us.

## Core Tasks
- [x] 1. Real-Time Pacing Analysis | DOD: `Time.unscaledDeltaTime` feeds a 64-slot `NativeRingBuffer<float>` and O(1) rolling sum. Alternative rejected: managed `float[]`/`List` cadence storage. Estimate: 2 us/frame.
- [x] 2. Scalability Dispatch | DOD: <14ms 64-frame average raises `SystemDegradationLevel.Optimal` and pushes `_MATH_LOD_HIGH`. Alternative rejected: static hardware-only tier with no runtime recovery. Estimate: 4 us/dispatch frame.
- [x] 3. Degradation Response | DOD: >18ms average sustained for 3 seconds raises Critical and pushes `_MATH_LOD_LOW`. Alternative rejected: single-frame panic downgrade. Estimate: 4 us/dispatch frame.
- [x] 4. Hysteresis Guard | DOD: existing 10-second cooldown gates state changes. Alternative rejected: immediate High/Low flipping. Estimate: <1 us/frame.
- [x] 5. 1024-Slot Ring | DOD: `GlobalTelemetryBus` retains `NativeRingBuffer<TelemetryEvent>` capacity 1024 and wraps with mask `index & 1023`. Alternative rejected: modulo or unbounded queue. Estimate: 1-2 us/publish.
- [x] 6. 64-Byte Alignment | DOD: `TelemetryEvent` is `[StructLayout(Size = 64)]` with reserved padding fields. Alternative rejected: variable managed payloads. Estimate: 0 us beyond copy cost.
- [x] 7. Binary Dump Decoupling | DOD: main thread queues `RequestEmergencyFlushAsync`; writer thread copies native scratch to `.h8dump` with pointer/span/MMF paths. Alternative rejected: main-thread file I/O and managed `byte[]` dump staging. Estimate: 0 us main-thread I/O, export off-thread.
- [x] 8. Numeric Hash Telemetry | DOD: log condition/stack trace are reduced to FNV-1a `uint` hashes before binary telemetry. Alternative rejected: raw string stack traces in dump. Estimate: cold fault path only.
- [x] 9. Precomputed Reciprocals | DOD: byte-to-MB conversions use `GlobalTelemetryBus.BytesToMegabytes = 0.000000953674f`; scan found no `bytes / 1048576f` hot path. Alternative rejected: division in telemetry conversions. Estimate: saves one divide per conversion.
- [x] 10. Cached RAM Bounds | DOD: `RuntimeWatchdog` caches `SafeBoundBytes` once in static state and samples profiler allocation, not OS RAM, per cadence. Alternative rejected: per-frame `SystemInfo.systemMemorySize`. Estimate: 0 OS polls/frame.
- [x] 11. NaN-Propagation Detector | DOD: `MathGuard.TryAcceptFinite(float3)` returns dominant-axis fallback and enqueues `0x4E414E21` for telemetry drain. Alternative rejected: throwing or raw exception logging in math ingress. Estimate: 1-3 us/fault, no cost on finite path beyond branch.
- [x] 12. Dominant-Axis Telemetry | DOD: `GlobalTelemetryBus.PublishDominantAxisTelemetry` records bot hash plus exact `math.distancesq` or dominant-axis magnitude-sq context; drone fleet emits at existing 60-frame telemetry cadence. Alternative rejected: string bot names, per-frame fauna scan, or direct profiler markers. Estimate: <=8 events/60 frames, <20 us cadence frame pending Unity profiler.
- [x] 13. MMF Registry Guard | DOD: `RuntimeWatchdog.SampleRegistryHeartbeatsIfDue` checks `IServiceHeartbeat.TickCount` every 60s and logs stale service-slot hash. Alternative rejected: per-frame registry heartbeat polling. Estimate: 255 slot checks/60s, 0 us/frame steady.
- [x] 14. Draw Call Addition | DOD: BRG managers call `FrameTimeWatchdog.ReportBatchRendererGroupBatchCount(int)`, then telemetry emits integer batch totals without Unity Profiler API. Alternative rejected: `ProfilerRecorder`/Unity Profiler draw-call scraping. Estimate: O(1) add/report per render manager.
- [x] 15. Noir Memory Alarm | DOD: memory breach uses cached 95% bound and broadcasts `MemoryBreachEvent` plus performance warning for Visor/UI listeners. Alternative rejected: direct visor coupling or per-frame OS RAM query. Estimate: 1 profiler allocation sample per 12 frames.
- [x] 16. Shader Fallback Monitor | DOD: asset load path checks `Material.shader.name` for `InternalErrorShader`, swaps stable checkerboard fallback, logs shader/material hashes. Alternative rejected: waiting for visible pink render artifacts or string material names in telemetry. Estimate: cold asset-load only.
- [x] 17. Input Lag Analyzer | DOD: `InputLatencyTracker.SampleInputSystemClockDeltaMs()` compares Input System clock (`InputState.currentTime`) with `Time.unscaledTimeAsDouble`; watchdog logs `INPUT_LAG_HASH` above 50ms with cooldown. Alternative rejected: render-completion latency only. Estimate: one double diff in LateFrame.
- [x] 18. Thread Stall Monitor | DOD: `BlackBoxHeartbeatThread` expects main-thread ping, waits 2000ms, calls background emergency flush, kills process outside editor. Alternative rejected: frame-count watchdog on frozen main thread. Estimate: 50ms sleep probe on background thread.
- [x] 19. Telemetry Privacy Filter | DOD: `.h8dump` payloads are fixed binary structs; log strings become FNV-1a hashes; crash export writes native scratch span, no raw stack/path/user text. Alternative rejected: stack trace sidecar or managed text payload. Estimate: no hot-path string payload.
- [x] 20. Watchdog Resource Limit | DOD: persistent telemetry storage remains below 5MB; removed 64KB managed crash export staging; hot work remains branch/ring/cadence gated. Alternative rejected: unbounded queues, per-frame full snapshots, or managed dump buffers. Estimate: under 0.05ms/frame pending Unity profiler.

## Verification Log
- [x] Mandatory mandates loaded before code.
- [x] Existing codebase scanned before code.
- [x] Build pass 1 after tasks 1-5: `dotnet build Hecton8.Core.csproj --no-restore` green, 0 warnings, 0 errors.
- [x] Build pass 2 after tasks 6-10: `dotnet build Hecton8.Core.csproj --no-restore -maxcpucount:1 /p:UseSharedCompilation=false` green, 0 warnings, 0 errors.
- [ ] Build pass 3 after tasks 11-15: `[BLOCKED BY DEPENDENCY]` external dirty files fail compile: `HectonCelestialEngine.cs` missing orbit helper methods; `SubmarineFluidDynamics.cs` missing `ImpactSignal`.
- [ ] Build pass 4 after tasks 16-20: `[BLOCKED BY DEPENDENCY]` external dirty files fail compile across 3 attempts: latest `VoxelDeltaProcessor.cs` signature/type errors and missing `DebrisSpawnSignal`; previous attempts also hit celestial/audio/submarine dirty files. No diagnostics in CORE_TELEMETRY edits.
- [x] Five strict self-review loops completed. Loop 1 privacy scan: no raw stack/path/user payload in `.h8dump`, log text hashes only. Loop 2 ring/struct scan: 1024 slot ring and 64-byte struct intact. Loop 3 reciprocal/RAM scan: no `bytes / 1048576f`, RAM query only in cache init. Loop 4 draw/input/stall scan: no Unity Profiler draw-call API, Input System skew path present, 2000ms stall path present. Loop 5 resource scan: native buffers bounded, managed crash/live export staging removed.
- [x] Polish mandate parsed after core tasks only. OMEGA audit completed with status retained as `PENDING VERIFICATION` because core prompt requires it and external compile wall prevents verification.
- [x] Continuation polish audit: current CORE_TELEMETRY diff re-scanned for managed scratch buffers, string formatting/interpolation, foreach, `ToString`, exact sqrt/normalize additions, and whitespace errors. DOD: only stack/native span dump writes remain. Alternative rejected: touching external compile blockers. Estimate: 0 us/frame.
