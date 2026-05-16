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

## 2026-05-16 - Titanium binding and platform audit pass

What was still wrong:
- `SlowTick()` still contained a dependency recovery path that read `GlobalRegistry` after gameplay started.
- The compass runtime had no cold public binding API for a physical tool or bootstrap-owned dependency injection.
- ARM64 layout was `Pack = 1` but not size-explicit for compass state/snapshot/blackbox.
- GUID scan found no serialized prefab/scene reference to `DiegeticGyroCompassRuntime`; scene wiring is not proven.

What was done:
- Removed the SlowTick registry fallback. Ticks now use cached `_playerContext`/`_vault` or return.
- Added `InjectDependencies(...)` for cold bootstrap injection.
- Added `ConfigurePhysicalBinding(...)` for physical tool binding of root, dial pivot, TMP label, indirect mesh, and material.
- Made struct sizes explicit: `CompassBlackBoxEntry` = 40 bytes, `CompassStateDTO` = 136 bytes, `InertialNavigationSnapshot` = 120 bytes. Compass signals remain explicit 80/32 bytes in core.
- Added High/Ultra-only optional anomaly failure particles, gated by power, anomaly > 0.8, quality tier, system stress, and a 128-particle hard cap per late-frame pass.

Cinematic cheats used:
- MX350 keeps the triangle-noise Dear Lie and no particle emission.
- High/Ultra spends saved CPU on local compass-glass salt/static burst emission when a physical emitter is assigned.
- The physical binding API preserves diegetic mapping without adding a screen-space fallback.

Exact microseconds saved:
- Removed SlowTick dependency polling: estimated 1-4 us per SlowTick on stressed/low-tier runs.
- Explicit struct sizes: no frame saving; reduces ARM64/Quest layout ambiguity.
- Optional particle burst: zero cost on MX350/mobile/stress paths; High/Ultra may deliberately spend 0-20 us/frame during saturated anomalies.

Validation:
- Forbidden-pattern scan passed for navigation domain: no private `NativeArray`, `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, `.ToString`, TMP `.text`, `SetText`, `Camera.main`, euler polling, EventBus, managed delegates, `new List`, `foreach`, `GameObject.Find`, coroutine, or direct `H8Memory.Allocate`.
- Duplicate scan found only one `AnomalyProximitySignal` and one `CompassCalibratedSignal`, both in `GlobalSignals.cs`.
- Serialized GUID scan found only the script `.meta`; no prefab/scene binding exists yet. Runtime code is ready, Unity scene wiring remains unverified.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /v:minimal` fails outside compass at `EcosystemRuntimeInstaller.cs` missing `Hecton8.AI.Ecosystem` and `SubmarineFluidDynamics.cs` missing `VaultNativeBuffer<>`.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 /v:minimal` fails outside compass because `Temp/obj/Assembly-CSharp/project.assets.json` is missing.
- Final status remains not `VERIFIED MASTER GRADE`; build and scene binding are not proven.

## 2026-05-16 - Physical authoring bridge and build-wall recheck

What was still wrong:
- The runtime had cold binding APIs, but no serialized authoring bridge existed in the navigation domain.
- A bad `TextMeshProUGUI` binding could leave `_diegeticTextValid` false after the author replaced it with valid world-space TMP text.
- Unity scene/prefab wiring still is not proven; no Unity MCP scene resources are exposed in this session.

What was done:
- Added/verified `DiegeticGyroCompassPhysicalBinding` as the cold physical-tool bridge. It maps tool root, dial pivot, cardinal TMP, indirect dial mesh/material, and optional High/Ultra anomaly particles into `DiegeticGyroCompassRuntime`.
- Kept dependency injection cold: startup only, using `GlobalRegistry.Player`, `GlobalRegistry.DataVault`, and `GlobalRegistry.ScalabilityTier`; gameplay ticks still use cached dependencies or return.
- Reset `_diegeticTextValid` before validating a TMP binding, so corrected diegetic text authoring recovers without a runtime restart.

Cinematic cheats used:
- Low/MX350 remains the triangle-noise Dear Lie with snapped cardinal output and no particle emission.
- Middle keeps physical pivot rotation only.
- High/Ultra can bind indirect dial rendering and local compass-glass salt/static bursts while gameplay truth remains the same SOA compass state.

Exact microseconds saved:
- Authoring bridge: 0 us steady-frame cost; it prevents hot lookup debt by doing all mapping during startup/cold paths.
- Text validation reset: no measurable frame saving; it removes a dead-label authoring failure.
- Maintaining no hot registry fallback: preserves the earlier estimated 1-4 us per SlowTick on stressed/low-tier runs.

Validation:
- Re-read `CURRENT_BATCH.md` XML for `COMPASS_GYRO_STABILIZER`; task count remains 18 and domain remains `Assets/_Project/Scripts/UI/Navigation/`.
- Navigation forbidden-pattern scan found no private `NativeArray`, `new NativeArray`, `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, `.ToString`, TMP `.text`, `SetText`, `Camera.main`, `transform.eulerAngles`, EventBus, managed delegates, `new List`, `foreach`, `FindObjectOfType`, `GameObject.Find`, coroutine, or direct `H8Memory.Allocate`.
- `git diff --check` on touched compass/contract files reports only LF-to-CRLF warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /v:minimal` succeeded once in this pass with 0 warnings and 0 errors.
- After concurrent worktree movement, latest `dotnet build Hecton8.Core.csproj --no-restore -m:1 /v:quiet /clp:ErrorsOnly` fails outside compass at `SubmarineFluidDynamics.cs(2004)` for missing `RefreshNativeStateViewsFromVault`.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 /v:minimal` fails outside compass: `RealtimeCSG.csproj` references many missing source files under `Assets/RealtimeCSG/...`, then `SubmarineFluidDynamics.cs` reports missing hot-swap/native-state helpers.
- Final status remains not `VERIFIED MASTER GRADE`; scene binding and full project build are not proven.
