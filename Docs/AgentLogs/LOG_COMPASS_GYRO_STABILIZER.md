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

## 2026-05-16 - Indirect dial GPU bandwidth repair

What was still wrong:
- The High/Ultra indirect physical dial used `ComputeBuffer.SetData` to upload one matrix every draw.
- The dial matrix upload was single-buffered and had no dirty check, so unchanged transforms still paid upload and potential sync cost.
- The navigation asmdef did not permit the unsafe lock/write path required by the project bandwidth mandate.

What was done:
- Replaced compass indirect args and dial matrix buffers with `GraphicsBuffer`.
- Added double-buffered dial matrix storage: buffer A and buffer B alternate on real upload.
- Removed the managed `Matrix4x4[1]` upload cache.
- Wrote indirect args and matrix data through `LockBufferForWrite` plus `UnsafeUtility.MemCpy`.
- Added dirty suppression for heading, position, rotation, and scale so unchanged dial frames reuse the last published GPU buffer.
- Enabled unsafe code for `Hecton8.UI.Navigation.asmdef` because the MemCpy upload path requires it.

Cinematic cheats used:
- Low/MX350 still uses snapped cardinal text and triangle-noise drift with no GPU indirect path.
- Middle still uses authored physical pivot rotation.
- High/Ultra keeps the indirect diegetic dial and spends saved upload bandwidth on glass chromatic overkill and optional anomaly particle bursts when stress is low.

Exact microseconds saved:
- Removed per-draw `ComputeBuffer.SetData`: estimated 2-8 us on unchanged dial frames, plus reduced CPU/GPU sync risk.
- Removed managed `Matrix4x4[1]` upload cache: no hot allocation saving because it was cold, but it removes one managed object from the dial path.
- Double-buffering: no fixed CPU saving; reduces stall risk on Steam Deck and PC when the renderer consumes the previous matrix buffer.

Validation:
- Re-read `Status_COMPASS_GYRO_STABILIZER.md`, `Rationale_COMPASS_GYRO_STABILIZER.md`, and the original XML prompt from `CURRENT_BATCH.md`.
- Navigation scan now finds no `ComputeBuffer`, `.SetData`, or managed matrix array in `Assets/_Project/Scripts/UI/Navigation`.
- Forbidden-pattern scan remains clean for private `NativeArray`, `new NativeArray`, standard `Update`/`LateUpdate`/`FixedUpdate`, managed formatting, TMP `.text`, `SetText`, camera polling, EventBus, managed delegates, object lookup, coroutine, and direct `H8Memory.Allocate`.
- `git diff --check -- Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs Assets/_Project/Scripts/UI/Navigation/Hecton8.UI.Navigation.asmdef` reports only LF-to-CRLF warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /v:minimal` is blocked outside compass by construction drone double3/float3 conversion errors in `DroneFleetManager.cs` and `DroneCognitionJob.cs`.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 /v:minimal` is blocked outside compass by missing RealtimeCSG source files before compass assembly proof.
- Unity batchmode compile was attempted with log at `Docs/AgentLogs/Unity_COMPASS_GYRO_STABILIZER_loop10.log`. It stopped outside compass in editor/audio/core dependency errors, and the log contains no `DiegeticGyroCompass` or `Hecton8.UI.Navigation` errors.
- Final status remains not `VERIFIED MASTER GRADE`; scene binding and full project build are not proven.

## 2026-05-16 - Vault state eviction pass

What was still wrong:
- The compass runtime still held gameplay authority in private fields after the earlier NativeArray eviction: prior AUP, signal-derived power/anomaly/stress, calibration request, drift clock, frame sequence, blackbox cursor, and snapshot cache.
- `CompassStateDTO` was vault-owned but not complete enough to be the only gameplay state authority.

What was done:
- Expanded `CompassStateDTO` to `Pack = 1, Size = 176`.
- Added vault fields for previous AUP, system stress, noise clock, blackbox cursor, and reserved padding.
- Moved calibration request into a state flag.
- Moved frame sequence to `state.Frame`.
- Moved blackbox write cursor to `state.BlackBoxCursor`.
- Replaced the private snapshot cache with `BuildSnapshot(in CompassStateDTO)` from vault state.
- Updated cadence, overkill, particle gating, velocity, and blackbox dump logic to consume vault state.

Cinematic cheats used:
- Low/MX350 still uses the triangle-noise drift lie and snapped cardinal text.
- Middle keeps only physical pivot rotation.
- High/Ultra retains the indirect physical dial, glass chromatic overkill scalar, and optional local anomaly particles, all driven from the same vault state.

Exact microseconds saved:
- Gameplay state eviction: 0 us direct measured saving; the value is deterministic ownership and fewer hidden cache paths.
- DTO growth: +40 bytes for one vault record, below measurable frame cost on MX350/i3.
- Removing private snapshot cache: avoids a duplicate state write in `CommitCompletedState`, estimated below 1 us.

Validation:
- Re-read `Status_COMPASS_GYRO_STABILIZER.md`, `Rationale_COMPASS_GYRO_STABILIZER.md`, and the original XML prompt from `CURRENT_BATCH.md`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /v:minimal` succeeds with 0 warnings and 0 errors after the DTO expansion.
- Static scan shows no private compass gameplay fields for power, anomaly, stress, calibration, noise clock, frame sequence, prior AUP, or blackbox cursor.
- Navigation forbidden-pattern scan still finds no `ComputeBuffer`, `.SetData`, standard `Update`/`LateUpdate`/`FixedUpdate`, `string.Format`, TMP `.text`, `SetText`, `Camera.main`, euler polling, EventBus, managed delegates, object lookup, coroutine, or direct `H8Memory.Allocate`.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 /v:minimal` is blocked before compile by missing `Temp/obj/Assembly-CSharp/project.assets.json`.
- Unity batchmode compile was attempted with log at `Docs/AgentLogs/Unity_COMPASS_GYRO_STABILIZER_loop11b.log`. It stops outside compass in editor/audio/core dependency errors; the log contains no `DiegeticGyroCompass`, `Hecton8.UI.Navigation`, `InertialNavigationContracts`, or `CompassStateDTO` errors.
- Serialized GUID scan still finds no prefab/scene binding for the compass runtime or physical binding.
- Final status remains not `VERIFIED MASTER GRADE`; scene binding and full project build are not proven.

## 2026-05-16 - Evidence alignment and NativeArray audit

What was wrong:
- Status/log evidence still named loop 11 while the latest Unity proof artifact is `Unity_COMPASS_GYRO_STABILIZER_loop11b.log`.
- The NativeArray audit needed explicit wording because the remaining hits are required vault/job views, not private ownership.

What was done:
- Updated task status to loop 12 and pointed latest Unity evidence at `Docs/AgentLogs/Unity_COMPASS_GYRO_STABILIZER_loop11b.log`.
- Re-ran `rg` over loop 11b for `DiegeticGyroCompass`, `Hecton8.UI.Navigation`, `Assets\\_Project\\Scripts\\UI\\Navigation`, `InertialNavigationContracts`, `CompassStateDTO`, and `error CS`; only external audio/editor/core compile errors are present.
- Re-ran NativeArray sovereignty scan. Navigation still has no private `NativeArray` fields and no `new NativeArray` allocations. Remaining hits are vault views, blackbox helper parameters, and required Burst job views over vault-owned `CompassStateDTO` and `NativeArray<float>` output.
- Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 /v:minimal`; it succeeds with 0 warnings and 0 errors.
- Re-ran `dotnet build Assembly-CSharp.csproj --no-restore -m:1 /v:minimal`; it remains blocked outside compass by 216 `RealtimeCSG.csproj` CS2001 missing-source errors.
- Re-ran `git diff --check` on touched compass/doc files; it reports only LF-to-CRLF warnings.
- Searched `CURRENT_BATCH.md` for a separate `<POLISH_MANDATE>` tag; none exists. The only polish directive in this batch is the XML section VI requirement to reach `VERIFIED MASTER GRADE`, which remains blocked by scene binding and external build proof.

Cinematic Cheats used:
- None added in this pass. Existing Low/Middle/High/Ultra math ladder remains unchanged.

Exact Microseconds saved:
- 0 us runtime. This pass corrected evidence and audit specificity, not frame cost.

## 2026-05-16 - Indirect draw submission and presentation vault eviction

What was wrong:
- The High/Ultra indirect dial submission was coupled to presentation state changes. `Graphics.DrawMeshInstancedIndirect` must be submitted every rendered frame, so a stable heading could make the physical compass dial disappear.
- Cardinal, shader, particle-debt, dial-transform, and matrix-buffer cache values still lived as private fields on `DiegeticGyroCompassRuntime`.
- A quick attempt to enlarge the core gameplay `CompassStateDTO` with presentation cache fields was rejected because it mixed gameplay truth with UI cache state and broke the standalone contract build in the current local assembly graph.

What was done:
- Added `CompassPresentationStateDTO` as a packed 80-byte navigation-owned vault DTO.
- Added `BufferID.CompassPresentationState = 467` without shifting existing buffer IDs.
- Moved presentation cache state into `GlobalDataVault`: last cardinal, shader scalars, particle debt, dial heading, dial transform, dial matrix buffer index, and presentation flags.
- Changed the High/Ultra path so `Graphics.DrawMeshInstancedIndirect` is submitted every active LateFrame while matrix uploads stay dirty-gated through the existing double-buffered `GraphicsBuffer.LockBufferForWrite` path.
- Kept `CompassStateDTO` at `Pack = 1, Size = 176` as gameplay authority only.
- Hardened the AUP velocity reciprocal: non-finite or epsilon-scale `deltaTime` now returns zero velocity before division, and the denominator is clamped with `math.max`.

Cinematic cheats used:
- Low/MX350 remains the Dear Lie path: SlowTick-compatible drift, snapped cardinal text through `SetCharArray`, and no indirect draw.
- Middle remains a physical pivot rotation path when authored.
- High/Ultra now keep the indirect dial visible even at a stable heading, and the saved upload bandwidth remains available for glass chromatic response and optional local anomaly particles.

Exact microseconds saved:
- Presentation vault eviction: 0 us direct measured saving; this is ownership and deterministic-state hardening.
- Dirty matrix upload preservation: keeps the previous 2-8 us estimated saving on unchanged High/Ultra dial frames versus per-frame upload.
- Per-frame indirect draw submission: intentional cost, not a saving. Required for visual correctness; no fake microsecond claim.
- Velocity reciprocal hardening: 0 us measured saving; one finite check plus one `math.max`, accepted to prevent NaN propagation.

Validation:
- Re-read `Status_COMPASS_GYRO_STABILIZER.md`, `Rationale_COMPASS_GYRO_STABILIZER.md`, and the original XML prompt from `CURRENT_BATCH.md`.
- Navigation forbidden-pattern scan found no `ComputeBuffer`, `.SetData`, managed matrix array, private `NativeArray`, `new NativeArray`, standard `Update`/`LateUpdate`/`FixedUpdate`, managed formatting, TMP `.text`, `SetText`, `Camera.main`, `transform.eulerAngles`, EventBus, managed delegates, object lookup, coroutine, or direct `H8Memory.Allocate`.
- Struct audit: `CompassStateDTO` = 176 bytes, `InertialNavigationSnapshot` = 120 bytes, `CompassBlackBoxEntry` = 40 bytes, `CompassPresentationStateDTO` = 80 bytes, all with `Pack = 1`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /v:minimal` succeeds with 0 warnings and 0 errors.
- `dotnet restore Assembly-CSharp.csproj -v:minimal` succeeds.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 /v:minimal` fails outside compass with 216 missing-source `RealtimeCSG.csproj` CS2001 errors.
- Unity batchmode loop 13 wrote `Docs/AgentLogs/Unity_COMPASS_GYRO_STABILIZER_loop13.log` and requested script compilation without compass/navigation errors. Loop 13b stayed alive for 600 seconds after requesting script compilation and was terminated; `Docs/AgentLogs/Unity_COMPASS_GYRO_STABILIZER_loop13b.log` contains no compass/navigation/compiler errors, but it is not a completed Unity compile proof.
- Final status remains not `VERIFIED MASTER GRADE`; scene binding and full Unity/project build proof are still blocked.

## 2026-05-16 - Integration typed-lane revalidation

What was wrong:
- A later integration drift restored `GlobalSignals.InitializeAllQueues()` inside `DiegeticGyroCompassRuntime.ConfigureSignalLanes()`.
- That was compile-legal but architecture-invalid for the compass domain because it initializes unrelated signal queues from UI/navigation code.

What was done:
- Replaced the broad global queue initialization with explicit compass-owned lane setup for `AnomalyProximitySignal` and `CompassCalibratedSignal`.
- Loop 14 now keeps that literal with bounded `SignalBus<T>.Configure(...)` calls for the owned lanes and `EnsureInitialized()` for consumed lanes.
- Revalidated through the Integration compile/static gate.

Cinematic Cheats used:
- None added. Existing Low/Middle/High/Ultra compass presentation ladder remains unchanged.

Exact Microseconds saved:
- 0 us runtime measured.

Verification:
- `Docs/AgentLogs/Build_INTEGRATION_ASSEMBLY_SURGEON_20260516_inquisition31_typed_compass_final.log`: green, 0 warnings, 0 errors.
- `Docs/AgentLogs/Scan_INTEGRATION_ASSEMBLY_SURGEON_20260516_inquisition32_static_final.txt`: `DIEGETIC_COMPASS_GLOBAL_INIT_HITS=0`.

## 2026-05-16 - Startup/hot-poll/depth repair

What was wrong:
- `DiegeticGyroCompassPhysicalBinding.Awake()` still resolved and bound another component. That violates the startup rule: `Awake` is self-init only.
- `DiegeticGyroCompassRuntime.OnEnable()` was still doing cold dependency resolution and potential High-tier GPU buffer setup before `Start()`.
- Legacy `ShaderCompassRibbon` read `GlobalRegistry.InertialNavigation` inside `LateFrameTick()`.
- `Hecton_UI_CompassRibbon.shader` used `ZTest Always`, allowing a legacy world-space fallback to draw through geometry.

What was done:
- `DiegeticGyroCompassPhysicalBinding.Awake()` now only defaults `toolRoot`; runtime resolution, dependency injection, and binding run in `Start()` or post-start re-enable.
- `DiegeticGyroCompassRuntime.OnEnable()` now configures/ensures signal lanes and registers only; player/vault dependency resolution and indirect buffer creation remain in `Start()`/explicit injection.
- Compass-owned lanes now explicitly configure bounded capacities and lane hashes before initialization: anomaly = 8 expected / 16 max / 4 low-tier, calibration = 4 expected / 8 max / 2 low-tier.
- `ShaderCompassRibbon` now caches `IInertialNavigationService` during cold startup and uses the cached field in `LateFrameTick()`.
- `Hecton_UI_CompassRibbon.shader` now uses `ZTest LEqual`.

Cinematic Cheats used:
- Low/MX350 remains snapped cardinal text and triangle drift; no extra particles or indirect dial.
- Middle remains physical pivot rotation.
- High/Ultra retain indirect dial submission, glass chromatic scalar, and optional local anomaly particles. No new physical simulation was added.

Exact Microseconds saved:
- 0 us measured.
- Removed one legacy per-LateFrame registry service read when `ShaderCompassRibbon` is manually present. No quantified microsecond claim.
- `ZTest LEqual` is a correctness/depth-occlusion repair, not a CPU saving claim.

Verification:
- Re-read status/rationale, AGENTS, domain map, XML prompt, and relevant mandates from disk.
- GUID scan found no serialized prefab/scene reference to `DiegeticGyroCompassRuntime`, `DiegeticGyroCompassPhysicalBinding`, or `ShaderCompassRibbon`.
- Static scan finds no standard `Update`/`LateUpdate`/`FixedUpdate`, `string.Format`, `Camera.main`, eulers, `ComputeBuffer`, `.SetData`, `GlobalSignals.InitializeAllQueues`, private/local `NativeArray` allocation, EventBus, managed delegates, object lookup, coroutine, or direct `H8Memory.Allocate` in the compass navigation path.
- Shader scan found no compute kernels, threadgroups, or DirectX-only path in the compass ribbon shader.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /v:minimal` succeeds with 0 warnings and 0 errors.
- `dotnet build Assembly-CSharp.csproj --no-restore --disable-build-servers -p:UseSharedCompilation=false -m:1 -v:quiet -clp:ErrorsOnly` fails outside compass with 216 missing-source `RealtimeCSG.csproj` CS2001 errors.
- `git diff --check` on touched files reports only LF-to-CRLF warnings.
- Final status remains not `VERIFIED MASTER GRADE`; Unity scene binding and full project build proof are still blocked.
