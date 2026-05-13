# STREAMING_IO_BACKPRESSURE Log

## 2026-05-13 - Drive Latency Clamp

What was wrong: World residency assumed SSD-like Addressables completion. On MicroSD/slow laptop storage, immediate-radius chunks can remain pending long enough for player or mounted transport velocity to outrun loaded geometry.

What was done: Bound `IStreamingBackpressureService` through `GlobalRegistry`, published `StorageDebtSignal` and `StreamingTurbulenceSignal` through `GlobalSignals`, cached the scalar in `SystemDispatcher`, tracked Addressables start times in `NativeArray<double>`, computed latency EWMA with `math.lerp(..., 0.08f)`, computed oldest immediate pending debt, and applied `MaxSpeed *= (1 - debt * 0.8)` through existing player/mount clamp paths. High debt halves prediction distance, uses LOD1/proxy fallback, writes CrashTelemetry, dumps `Docs/AgentLogs/Dump_STREAMING_IO_BACKPRESSURE.bin` on NaN/time fault, and shows the PDA data-link degraded icon when debt > 0.6.

Cinematic Cheats used: Fake thick-current turbulence signal, PDA degraded-link icon, LOD1/collision proxy fallback under debt, prediction halving instead of physically simulating storage stall as water force.

Exact Microseconds saved: Per-frame Addressables completion polling moved from frame Tick to SlowTick/10 Hz equivalent; estimated 10-25 us/frame average saved at 256-512 chunk definition scale. Managed timestamp objects rejected; estimated 3-8 us saved under load churn and 0 B/frame. Managed event broadcast rejected; estimated 6 us saved per burst and 0 B/frame. Real fluid resistance rejected; estimated >100 us/frame avoided versus a physical cover-up path. Scalar clamp read cost is estimated <0.05 us per movement clamp.

Verification: `rg` found no targeted loading coroutines/WaitUntil in the streaming/movement files. `dotnet build C:\hades\Hecton8\Hecton8.Core.csproj -v:minimal` failed with 97 global dependency errors before STREAMING_IO_BACKPRESSURE could be isolated; log saved to `Docs/AgentLogs/Build_STREAMING_IO_BACKPRESSURE.log`. Unity MCP script refresh timed out after 60 s and `read_console` returned no Unity session. STATUS: PENDING VERIFICATION.
