# APEX PASS6 Runtime Dump Route 1302

Date: 2026-05-25
Agent: 1302
Domain: `Assets/_Project/Scripts/Physics`, excluding Tether/Cable/Harpoon ownership lanes.
Build policy: no `dotnet build`, no Unity build, no assembly rebuild launched.

## Static Verdict

- `Docs/Reports/STRICT_PHYSICS_FAULT_ROUTE_SCAN_1302_PASS6.json`
  - touched source files scanned: 18
  - local fault-writer forbidden hits in touched files: 0
  - Core blackbox bridge hits: 58
  - cold read/path IO hits still present: 45
  - broad residual not editor/not tether: 1
- `Docs/Reports/DTO_OFFSET_MAP_1302_PASS6_TARGETS.json`
  - DTO targets: 17
  - found: 17
  - missing: 0
  - size % 8 violations: 0
- `git diff --check` on touched 1302 files: pass, only Git LF-to-CRLF warnings.

## Runtime Fault Writers Removed

- `HabitatFluidIncursionDirector.cs:1451` guards fault dump on `_coreBlackboxWarmed && BlackboxActiveFrameCount > 0`; `:1458-1459` publishes `HFFT/HFDP`.
- `AnalyticalGerstnerWaveRuntime.cs:872` guards fault dump; `:879-880` publishes `GFFT/GFDP`.
- `AsyncBuoyancyReadbackRuntime.cs:1441` guards fault dump; `:1448-1449` publishes `ARFT/ARDP`.
- `BuoyancyDisplacementRuntime.cs:1856` guards main buoyancy fault; `:1868-1869` publishes `BUFT/BUDP`.
- `BuoyancyDisplacementRuntime.cs:1882` guards SIMD fault; `:1889-1890` publishes `BSFT/BSDP`.
- Prior pass routes remain intact: Vehicle, Submarine dynamics, Submarine gyro, Autopilot, Cavitation, Exosuit, KCC, Seaglide.

## Removed Local IO Symbols

Touched 1302 source now has zero hits for:

- `FileMode.Create`
- `Directory.CreateDirectory`
- `BinaryWriter`
- `File.Replace`, `File.Move`, `File.Delete`
- `WriteTelemetryDump`, `TryWriteTelemetryDump`, `TryWriteSleepTelemetryDump`
- `WriteUInt32LittleEndian`, `WriteTelemetryRange`
- dead `DumpRelativePath` constants in the patched Cavitation/Buoyancy contracts

## Residuals

- `Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:1415`
  - Residual: `WriteShinobu37PhysicsCullingFrameDump(BinaryWriter writer)`.
  - Classification: stream helper only; local `FileMode.Create` is owned by root `Assets/_Project/Scripts/GlobalPhysicsStateManager.cs:3316-3336`, outside the strict Physics folder patch surface.
- `Assets/_Project/Scripts/Physics/HarpoonTensionSolver328.cs:1095-1096`
  - Classification: explicit Tether DTO/Telemetry lane (`TetherStateDTO`, `TetherTelemetryEntry`, `RecordTetherTelemetryJob`), excluded by 1302 prompt.
- Core residual: `GlobalTelemetryBus.TryDumpBlackboxNow` still uses Core managed IO internally. Physics now routes to Core; native-only crash file ownership remains a Core bridge task.

## AUP Formula Check

- KCC canonical formula: `HydrodynamicKccRuntime.cs:426-433`
  - `double3 delta = Sanitize(aup - sectorOriginAup, double3.zero);`
  - clamp local double delta
  - return `new float3((float)delta.x, (float)delta.y, (float)delta.z)`
- Async readback scalar fix: `AsyncBuoyancyReadbackJobs.cs:189-194`
  - `double heightAupY = CameraAupY + localHeight`
  - finite check is double-space; no absolute AUP vector cast to float.
- Vehicle depth fix: `VehicleComponentDamageRuntime.cs` uses double `seaLevelAupY - rootAup.y` before float clamp.

## Overengineering Check

- No new simulation loop, no new Burst job, no new native container, no new DTO authority.
- Existing Dear Lie paths remain: Gerstner coarse grid, buoyancy mock/LUT CSV profiles, KCC local float projection after double delta.
- Fault route work is one Core event push plus Core dump trigger on failure only; hot frame steady-state cost remains 0 us beyond cold warmup state.
