# LOG_COMPASS_GYRO_STABILIZER

## 2026-05-16 - Diegetic 64-bit gyro compass

What was wrong:
- Existing compass path was a perfect screen-space ribbon installed by `ProgressionRuntimeInstaller`.
- Heading authority came from camera orientation instead of player/AUP navigation state.
- No vault-owned compass state/output buffer existed.
- No drifting gyro model, anomaly interference, power death, calibration reset, or compass blackbox existed.

What was done:
- Added `DiegeticGyroCompassRuntime` under `Assets/_Project/Scripts/UI/Navigation/`.
- Added vault-owned `CompassStateDTO`, `CompassOutputSlot`, `CompassState`, `CompassHeadingOutput`, and `CompassBlackBox`.
- Added Burst `GyroDriftJob` with global +Z north, local frame sequence, finite guards, `math.fmod` heading normalization, anomaly drift, and wild spin over 0.8 interference.
- Moved compass signal payload ownership to `GlobalSignals.cs` so `AnomalyProximitySignal` and `CompassCalibratedSignal` match the core signal lane owner.
- Removed runtime installation of `ShaderCompassRibbon`; legacy ribbon now only works from world-space Canvas and reads `IInertialNavigationService`.
- Added low-tier `SetCharArray` cardinal output, high-tier indirect dial draw, shader chromatic/power scalars, `SystemHealthSignal` SlowTick fallback, `SurvivalVitalsChangedSignal` <1% death, and fixed 300-entry blackbox dump.

Cinematic cheats used:
- Compass error is a bounded visual fake: heading catch-up plus coherent noise, not a physical magnetometer/gyro simulation.
- Anomaly failure uses scalar interference and shader chromatic aberration instead of particles or simulation.
- Low tier gets snapped cardinal labels; high tier spends saved cycles on indirect physical dial presentation.

Exact microseconds saved:
- Removed screen ribbon install and camera yaw path: estimated 7-20 us/frame plus avoided Canvas dirty work.
- SOA output buffer instead of managed polling: estimated 3 us/frame.
- Typed signal snapshots instead of delegates/string events: estimated 6 us/frame.
- `SetCharArray` cardinal output instead of TMP `.text`: estimated 3 us/update and zero hot GC.
- SlowTick under stress/low tier instead of fixed 60Hz: estimated 5-15 us/frame on i3/MX350.
- Blackbox ring write: fixed cost about 2 us/frame; buys postmortem state instead of console-only failure.

Validation:
- Static hazard scan passed for touched compass files: no `CompassUI.Instance`, `Camera.main`, camera eulers, TMP `.text`, `SetText`, `Time.frameCount`, `StartCoroutine`, managed formatting, or non-ASCII punctuation.
- Build strike 1: `Hecton8.Core.csproj --no-restore` failed before compass on missing `ProceduralLadderClimbRuntime`, `ItemData`, `OrganicDebrisProfile`.
- Build strike 2: `Hecton8.Core.csproj --no-restore -m:1` failed in `FaunaKinematicsRuntime`; `Assembly-CSharp.csproj` failed on missing `Temp/obj/Assembly-CSharp/project.assets.json`.
- Build strike 3: exposed compass signal payload placement; repaired. Rebuild then failed on unrelated walls: `Hecton8.VFX.Wakes`, `IDockingAutopilotService`, `ActiveSplineData`, and ecosystem service method mismatches.
- Final status: compass work implemented; full build blocked by external dependency wall. No `VERIFIED MASTER GRADE` claim made.

## 2026-05-16 - Multiplatform inquisition pass

What was still wrong:
- Compass runtime held private `NativeArray` handles to DataVault buffers. Data lived in the vault, but the handle cache was still private state and failed a strict H-Phi reading.
- `CompassStateDTO`, `InertialNavigationSnapshot`, and `CompassBlackBoxEntry` did not all explicitly use `Pack = 1`.
- Low tier still used coherent noise when a cheaper mathematical lie was enough.
- High-tier indirect drawing had no explicit GLES/no-compute/no-instancing guard.

What was done:
- Removed persistent `NativeArray` fields from `DiegeticGyroCompassRuntime`; buffer views are now transient and always resolved from `GlobalDataVault`.
- Set compass DTO/snapshot/blackbox structs to `StructLayout(..., Pack = 1)`.
- Added velocity clamping before float cast to block overflow-to-infinity on tiny delta/AUP spike cases.
- Replaced low-tier drift noise with triangle noise; high/ultra gets two-octave drift and `_CompassOverkill01` material scalar.
- Added `SupportsIndirectDial()` guard for GLES/mobile/unsupported indirect-render paths.
- Updated `NavigationUiAssemblyMarker` so it no longer lies about contract-only dependencies.

Cinematic cheats used:
- Low tier: triangle-wave drift lie instead of coherent noise.
- Middle: one coherent-noise drift sample.
- High/Ultra: two-octave drift and indirect physical dial plus glass overkill scalar.

Exact microseconds saved:
- Removing low-tier coherent noise: estimated 1-3 us per scheduled compass tick on i3/MX350.
- Removing cached NativeArray ownership: no direct frame saving; it removes private state and handle-lifetime risk.
- GLES/unsupported indirect guard: prevents invalid draw submission; frame saving is platform-dependent, estimated 5-20 us when the invalid path would have been attempted.
- Velocity clamp: no performance win; prevents mobile GPU/consumer NaN poisoning after AUP delta spikes.

Validation:
- Domain scan after patch found no private `NativeArray`, `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, `.ToString`, TMP `.text`, `SetText`, `Camera.main`, euler polling, EventBus, managed delegate use, `new List`, `foreach`, `GameObject.Find`, coroutine, or direct `H8Memory.Allocate`.
- Compass packing verified: `CompassBlackBoxEntry`, `CompassStateDTO`, `InertialNavigationSnapshot`, `AnomalyProximitySignal`, and `CompassCalibratedSignal` are `Pack = 1`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /v:minimal` still fails outside compass: missing `Hecton8.Core.Bucketing` / `ModuloSimulationBucketer` in `GameBootstrapper`.
- `dotnet build Assembly-CSharp.csproj -m:1 /v:minimal` timed out after 124 seconds. No green build claim.
