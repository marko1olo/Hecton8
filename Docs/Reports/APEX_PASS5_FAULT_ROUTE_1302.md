# APEX Pass 5 - 1302 Fault Route

Status: PENDING VERIFICATION
Agent: 1302
Scope: `Assets/_Project/Scripts/Physics`, excluding Tethers/Cable ownership lanes where noted.

## What Was Wrong

- Pass 4 removed only the local vehicle damage dump writer.
- Static scan still found fault-path managed file writers in vehicle/submarine automation code that 1302 can own:
  - `VehicleComponentDamageRuntime.TryWriteBlackBoxDump`
  - `SubmarineDynamicsRuntime.TryWriteHydrodynamicsBlackBoxDump`
  - `SubmarineDynamicsRuntime_Gyroscopes.TryWriteGyroBlackBoxDump`
  - `SubmarineAutopilotSdfNavigator.WriteTelemetryDump`
- `GlobalTelemetryBus.TryDumpBlackboxNow` can cold-call `EnsureBlackboxInitialized`, which may allocate vault buffers, build paths, start threads, and lock Core buffers if the route was not initialized before the fault.

## What Changed

- Removed the four local Physics fault dump writer methods above.
- Added cold route warmup:
  - `VehicleComponentDamageRuntime.WarmCoreBlackboxRoute`
  - `SubmarineDynamicsRuntime.WarmCoreBlackboxRoute`
  - `SubmarineAutopilotSdfNavigator.WarmCoreBlackboxRoute`
- Added fault-path guards:
  - `_coreBlackboxWarmed`
  - `GlobalTelemetryBus.BlackboxActiveFrameCount > 0`
- Fault branches now publish fixed-hash events and call Core only after the warmup proof is present.

## Static Evidence

- Prompt re-extracted: `Docs/Reports/PROMPT_1302_REEXTRACTED_PASS5.txt`
- Task count: 20 via `Docs/Reports/PROMPT_1302_TASK_HEADERS_PASS5.txt`
- DTO target offset map: `Docs/Reports/DTO_OFFSET_MAP_1302_PASS5_TARGETS.json`
  - Structs checked: 6
  - Multiple-of-8 failures: 0
- Fault route scan: `Docs/Reports/STRICT_PHYSICS_FAULT_ROUTE_SCAN_1302_PASS5.json`
  - Touched fault writer hits: 0
  - Core bridge hits: 3
  - Broad runtime-scoped dump hits still present outside this patch: 62
- `git diff --check` passed for touched source files; only LF-to-CRLF warnings.

## Release Honesty

- No dotnet build, Unity import, player build, profiler, or GCMonitor run was launched.
- Physics-local fault file writers are removed from the patched vehicle/submarine/autopilot nodes.
- Broader Physics still has runtime local dump writers in Cavitation, Buoyancy, KCC, Exosuit, Seaglide, HabitatFluid, plus excluded Tether/Cable lanes.
- Core `GlobalTelemetryBus` still writes dumps through managed C# IO internally. A literal native-only dump writer remains a Core/native bridge task.

