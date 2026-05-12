# LOG_CORE_TELEMETRY

## 2026-05-11 - CORE_TELEMETRY Watchdog Pass
Status: PENDING VERIFICATION

What was wrong:
- Frame pacing used managed 60-sample storage instead of the mandated 64-sample `NativeRingBuffer`.
- Telemetry dump paths had managed staging risks and log fault text was not reduced to fixed numeric payloads.
- Bot math LOD telemetry had no binary event for exact `math.distancesq` versus dominant-axis approximation.
- Shader fallback detected unsupported shaders but not Unity pink/error shader names.
- Input lag monitoring measured completed input-to-render latency but did not explicitly compare Input System time against `Time.unscaledTime`.
- Crash/live telemetry retained managed file scratch buffers.

What was done:
- `FrameTimeWatchdog` now records `Time.unscaledDeltaTime` in a 64-slot native ring, computes O(1) moving average, dispatches `_MATH_LOD_HIGH` under 14ms and `_MATH_LOD_LOW` after 18ms sustained for 3s with 10s cooldown.
- `GlobalTelemetryBus` keeps a 1024-slot `NativeRingBuffer<TelemetryEvent>`, exports 64-byte binary events, hashes diagnostic strings with FNV-1a, and adds `DominantAxisTelemetry`.
- `CrashTelemetryBuffer` writes fixed binary structs only; log conditions/stack traces are hashed; live and crash export write native/stack spans via `ReadOnlySpan<byte>`.
- `DroneFleetManager` publishes bot distance metrics at the existing 60-frame fleet cadence: high tiers use `math.distancesq`; low tiers use dominant-axis magnitude-sq.
- `AssetLifecycleGovernor` detects `Material.shader.name` containing `InternalErrorShader`, swaps the stable checkerboard fallback, and logs numeric material/shader hashes.
- `RuntimeWatchdog` samples Input System clock skew against `Time.unscaledTimeAsDouble`, logs `INPUT_LAG_HASH` above 50ms, preserves 60s registry heartbeat checks, BRG integer batch reporting, memory breach events, and 2000ms black-box stall kill path.

Cinematic cheats used:
- Low-tier bot distance telemetry uses dominant-axis magnitude-sq instead of exact distance squared.
- Runtime shader math LOD switches to `_MATH_LOD_LOW` when frame pacing is critical; High/Ultra regain `_MATH_LOD_HIGH` only after sustained headroom.
- Memory breach and shader fallback are event-driven fakes for visual alarm/glitch systems, not direct expensive UI scans.

Exact microseconds saved, pending profiler verification:
- Frame sample storage: 0 heap growth; estimated managed-array churn avoided: 0.5 us/frame risk removed.
- Cached RAM bound: estimated OS/system-memory poll avoided: 6 us/sample.
- Dominant-axis low-tier drone metric: estimated 3 us saved per 60-frame drone telemetry cadence at 8 drones.
- Removed crash export managed copy: estimated 35 us/export and one 64KB managed retained buffer removed.
- Removed live telemetry managed scratch: 32 bytes retained managed heap removed; no hot GC gain, export path stays stack/native.

Scalability Dispatch code:
```csharp
private static void DispatchScalabilityIfNeeded(float deltaTime, float averageFrameTimeSeconds)
{
    bool criticalAverage = averageFrameTimeSeconds > CriticalAverageThresholdSeconds;
    _criticalAverageSeconds = math.select(0f, _criticalAverageSeconds + deltaTime, criticalAverage);

    if (_criticalAverageSeconds >= CriticalSustainSeconds)
    {
        TrySwitchScalability(MathLodMode.Low, SustainedFrameCriticalHash, DegradeMathLodLowMask | DegradeCriticalLevelMask, averageFrameTimeSeconds * 1000f, CriticalAverageThresholdSeconds * 1000f, SystemDegradationLevel.Critical);
        return;
    }

    if (averageFrameTimeSeconds < OptimalAverageThresholdSeconds)
        TrySwitchScalability(MathLodMode.High, SustainedFrameOptimalHash, DegradeMathLodHighMask, averageFrameTimeSeconds * 1000f, OptimalAverageThresholdSeconds * 1000f, SystemDegradationLevel.Optimal);
}
```

64-byte `TelemetryEvent` struct:
```csharp
[StructLayout(LayoutKind.Sequential, Size = 64)]
public struct TelemetryEvent
{
    public uint FrameIndex;
    public uint EventType;
    public uint SubjectHash;
    public uint ContextHash;
    public float ScalarValue;
    public float3 WorldPosition;
    public uint Reserved0, Reserved1, Reserved2, Reserved3;
    public uint Reserved4, Reserved5, Reserved6, Reserved7;
}
```

Final Git Diff:
- `Assets/_Project/Scripts/Construction/DroneFleetManager.cs`
- `Assets/_Project/Scripts/Core/FrameTimeWatchdog.cs`
- `Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs`
- `Assets/_Project/Scripts/Core/InputDispatcher.cs`
- `Assets/_Project/Scripts/Core/RuntimeWatchdog.cs`
- `Assets/_Project/Scripts/CrashTelemetryBuffer.cs`
- `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs`
- Code diff stat: 7 files changed, 316 insertions, 237 deletions.
- Disk memory docs: `Docs/Tasks/Status_CORE_TELEMETRY.md`, `Docs/AgentLogs/Rationale_CORE_TELEMETRY.md`, this log.

Build evidence:
- Pass 1 after tasks 1-5: green, 0 warnings, 0 errors.
- Pass 2 after tasks 6-10: green, 0 warnings, 0 errors.
- Later passes blocked by external dirty files. Latest blocker: `VoxelDeltaProcessor.cs` signature/type errors and missing `DebrisSpawnSignal`. No build diagnostics referenced CORE_TELEMETRY files.

## 2026-05-11 - Continuation Polish Audit
Status: PENDING VERIFICATION

What was wrong:
- The log/rationale still carried the pre-continuation diff stat after the managed scratch cleanup landed.
- The current `CrashTelemetryBuffer.cs` implementation needed a fresh direct scan to prove no retained managed dump scratch remained.

What was done:
- Re-scanned CORE_TELEMETRY diff for `new byte[]`, `string.Format`, interpolation additions, `foreach`, `ToString`, `math.sqrt`, `math.normalize`, and whitespace errors.
- Confirmed live telemetry writes use `stackalloc` plus `ReadOnlySpan<byte>`.
- Confirmed crash export writes native scratch directly through `ReadOnlySpan<byte>`.
- Updated disk state with the current code diff stat: 7 files changed, 316 insertions, 237 deletions.

Cinematic cheats used:
- No new runtime cheat added. Existing low-tier drone distance telemetry still uses dominant-axis magnitude-sq; High/Ultra keeps exact `math.distancesq`.

Exact microseconds saved, pending profiler verification:
- Managed live/crash scratch retained heap: 0 bytes after cleanup.
- Hot path delta from this continuation pass: 0 us/frame; cleanup is retained-memory/privacy hygiene.

Build evidence:
- Not rerun after documentation-only updates. Last compiler proof remains blocked by external `VoxelDeltaProcessor.cs` errors; no diagnostics referenced CORE_TELEMETRY files.
