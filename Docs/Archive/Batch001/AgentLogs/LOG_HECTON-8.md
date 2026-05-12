# HECTON-8 Deterministic Replay Log

Status: PENDING VERIFICATION

## 2026-05-11 - Replay Foundation Loop 1/2

What was wrong:
The project had telemetry and crash buffers, but no deterministic replay layer that snapshots registered DOD native buffers into a circular `replay.bin` and binds MathGuard NaN faults to a replay dump request.

What was done:
Added pointer-backed snapshot source export to `NativeMemorySentinel`.
Added `DeterministicReplaySeed` with frame-indexed LCG seed composition.
Added `DodReplayRecorder` with 128-byte snapshot headers, segment headers, input event journal, delta-skip using `math.select`, 499 MB circular MMF writer, and `ThreadPriority.Lowest` background thread.
Connected `MathGuard.DrainInvalidNumberErrors` to `DodReplayRecorder.RequestFullStateDump` and existing crash telemetry NaN recovery.
Changed existing blackbox/telemetry background thread priorities from `BelowNormal` to `Lowest`.
Added UI Toolkit `DodReplayScrubberWindow` for editor-only replay header scrubbing and adjacent snapshot byte comparison.
Generated `.meta` files for new scripts.

Cinematic Cheats used:
No physical simulation was added. Replay overlay and wireframe drift rendering are deferred; current loop uses binary/editor inspection only, zero gameplay visual cost.

Exact Microseconds saved:
PENDING MEASUREMENT. Expected savings are from skipping unchanged segment payload copies and moving MMF writes to a background thread, but no profiler/GCMonitor evidence exists yet.

Verification:
Loop 1 compile passed: `dotnet build Hecton8.Core.csproj` and `dotnet build Hecton8.Editor.csproj`, 0 warnings, 0 errors.
Loop 2 editor-only syntax passed: `dotnet build Hecton8.Editor.csproj --no-dependencies`, 0 warnings, 0 errors.
Loop 2 verification is blocked by unrelated dirty files in GPU scatter/audio domains. Status remains PENDING VERIFICATION.
