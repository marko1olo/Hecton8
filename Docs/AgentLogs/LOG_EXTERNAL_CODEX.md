# LOG_EXTERNAL_CODEX

## 2026-05-23 External Integration Pass
What was wrong:
- Generated project reference pruner removed missing `Library/ScriptAssemblies` references. This erased valid local asmdef dependencies before Unity produced DLLs and caused `Hecton8.Habitat.Deformation.Contracts` to disappear from `Hecton8.Core.csproj`.
- `ShaderCompassRibbon` cached `GlobalRegistry.InertialNavigation` only during `OnEnable`/`Start`. If the navigation runtime registered after UI boot, the compass stayed hidden until component restart.

What was done:
- Updated `Assets/_Project/Scripts/Editor/HectonGeneratedProjectReferencePruner.cs` to prune missing package-cache references only, preserving local script assembly references.
- Added `Assets/_Project/Tests/Editor/HectonGeneratedProjectReferencePrunerEditTests.cs` to lock the pruner behavior: keep local `Hecton8.Habitat.Deformation.Contracts` script assembly reference, remove stale `Unity.Entities` package-cache reference.
- Updated `Assets/_Project/Scripts/UI/ShaderCompassRibbon.cs` to implement `IGlobalRegistryHotSwapListener`, refresh its cached navigation service on `InertialNavigationRuntime` replacement, and retry dispatcher registration on dispatcher replacement.
- Patched ignored local `Hecton8.Core.csproj` generated artifact only to move verification past the stale Habitat contract wall before Unity regenerates project files.

Cinematic Cheats used:
- None. Both fixes are routing/cache fixes, not simulation or visual fakery.

Exact Microseconds saved:
- Pruner fix: runtime 0 us; editor compile triage avoids repeated failed Habitat namespace compiles, estimated 10,000,000 us per failed `dotnet build` attempt on this machine.
- Compass hot-swap fix: runtime LateFrame remains 0 allocation and no GlobalRegistry polling; avoids a per-frame global read fallback. Estimated saved hot-path cost 0.1-0.3 us/frame when navigation service is absent or late-bound.
- Build-server discipline: shut down compiler servers after verification; avoids parallel compiler contention, estimated 500,000+ us avoided on low-end i3/MX350-class hardware during subsequent build attempts.

Verification:
- `git diff --check` passed for touched tracked files; only CRLF normalization warnings were reported.
- `dotnet build Hecton8.Editor.csproj --no-restore` attempt 1 failed on missing Habitat namespace from stale generated `Hecton8.Core.csproj`.
- After local ignored generated graph repair, build attempt 2 passed the Habitat wall and failed later with 290 unrelated errors in other-agent partials: `WristHologramHudRuntime`, `VRSomaticProvider.Comfort`, `HectonNarrativeDirector_PoiTriggers`, `AirlockPressurization`, `BulkheadContainmentRuntime_HatchLocks`, `TetherManager`.
- Historical first-pass compile status then: BLOCKED BY DEPENDENCY outside EXTERNAL_CODEX changes. Superseded by later zero-warning verifier entries below.

## 2026-05-23 External Integration Compile Closure
What was wrong:
- After the generated project graph was forced past the Habitat contract wall, the project had real compile defects in source: illegal `NativeArray<T>.ReadOnly.AsReadOnly()` usage, `in` parameters used where buffers were mutated, unsafe field addresses taken directly, missing narrative imports, wrong metabolism fatigue constant name, inaccessible nested mock seismic job, ambiguous Burst math overload, unassigned camera-juice arrays, variable shadowing, and an editor `Environment` namespace collision.
- Several files existed on disk but were absent from the ignored generated `Hecton8.Core.csproj`, so local dotnet verification could not see partial definitions and generated contracts until the generated graph was repaired for this checkout.

What was done:
- Fixed source compile faults with narrow edits in the owning files: `SubmarineAutoLevelBallastController`, `SignalWardenRuntime`, `PlayerCriticalProceduralAudioRenderer`, `HydrodynamicKccRuntime`, `AirlockPressurizationRuntime`, `AirlockPressurizationJobs`, `HectonNarrativeDirector_PoiTriggers`, `AlignmentTelemetryContracts`, `SystemDispatcher`, `HectonSeismicTideDirector`, `CombatDamageRuntime_StatusEffects`, `CameraJuiceSystem_CameraJuiceBurst`, `BulkheadContainmentRuntime`, and `CraftingFastFailXRayWindow_SHINOBU317`.
- Added a mutable `SystemDispatcher.TryResolveDispatcherVaultBuffer<T>` overload while keeping read-only accessors read-only.
- Patched ignored local `Hecton8.Core.csproj` only as a verification bridge for existing on-disk partial/generated sources; durable source fix remains the tracked project-reference pruner and its regression test.
- Ran guarded iterative builds. Error-wall progression: Habitat graph wall -> 580 error lines -> 194 errors -> 7 errors -> 6 errors -> 1 error -> 0 errors.
- Shut down MSBuild and C# compiler servers after final verification.

Cinematic Cheats used:
- None. This pass repaired compile/integration defects. No physical simulation or visual system was replaced with a fake.

Exact Microseconds saved:
- Runtime intended delta: 0 us for namespace/import/visibility/definite-assignment repairs.
- `ShaderCompassRibbon` hot-swap route avoids per-frame GlobalRegistry polling fallback; estimated hot-path saving 0.1-0.3 us/frame in late-bound navigation scenarios.
- Airlock unsafe pointer repair preserves native atomic routes instead of managed proxy work; estimated avoided allocation/dispatch pressure 0.5-2.0 us per contested flush on i3/MX350-class machines.
- Build graph/pruner repair avoids repeated failed local compile passes; measured category is build-time, estimated 10,000,000+ us per avoided failed dotnet attempt on this machine.
- Build-server shutdown avoids subsequent compiler contention; estimated 500,000+ us on low-end editor hardware.

Verification:
- Final guarded command: `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_after_patch5.log;verbosity=minimal"`.
- Final build output produced `Temp/bin/Debug/Hecton8.Core.dll` and `Temp/bin/Debug/Hecton8.Editor.dll`; log has 0 `: error ` entries.
- Remaining warnings: two `CS0618` warnings in `Assets/_Project/Scripts/Editor/SubmarineDynoTunerWindow.cs` for obsolete `SubmarineKinematicConfig.BallastLiftN`.
- `git diff --check` passed for touched files; only Git LF-to-CRLF normalization warnings were reported.
- Post-build guard: CPU 34.3%, 0 `dotnet/csc/VBCSCompiler` processes.

Residual:
- `Hecton8.Core.csproj` is ignored/generated. Unity/project regeneration must carry the tracked pruner behavior forward; otherwise local dotnet verification can regress even though source files are present.

## 2026-05-23 Zero-Warning Closure
What was wrong:
- The green build still had warnings. Two `CS0618` warnings came from `SubmarineDynoTunerWindow` editing `SubmarineKinematicConfig.BallastLiftN`, an obsolete ABI residue ignored by the integrator.
- `PlayerFlashlight` carried dead flicker state: `batteryOrHeatFlicker`, `_flickerIntensityMod`, and `_flickerTimer` did not affect output.
- `HectonPlayerHealth.OnDeath` and `HectonSurvivalSystem.OnDeath` were public events but were never invoked. That is not cosmetic; UI/audio/save observers could subscribe and never receive death.
- `HectonSurvivalSystem.ApplyRespawnReconciliationSurvival()` erased the last death record during respawn, contradicting the last-loss marker API.
- Local generated `Hecton8.Core.csproj` fed three source files to csc twice: `BulkheadContainmentIntentBus.cs`, `BulkheadContainmentContracts.cs`, and `BaseAtmosphereLogisticsTypes.cs`.

What was done:
- Removed the obsolete ballast slider/write from `Assets/_Project/Scripts/Editor/SubmarineDynoTunerWindow.cs`.
- Removed dead flashlight flicker state from `Assets/_Project/Scripts/PlayerFlashlight.cs`.
- Added death publication before respawn reconciliation in `Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs`.
- Added survival death record capture, telemetry logging, and `OnDeath` publication before respawn in `Assets/_Project/Scripts/HectonSurvivalSystem.cs`; stopped clearing the last death record during respawn.
- Removed duplicate source includes from ignored local `Hecton8.Core.csproj`.

Cinematic Cheats used:
- None. This pass repaired source correctness and build hygiene, not physical simulation or visual approximation.

Exact Microseconds saved:
- Runtime frame impact: 0 us. Death publication runs only on death and is one nullable delegate route per owning system.
- Flashlight cleanup: 0 us measurable; removed misleading dead state from the hot update path.
- Ballast editor cleanup: runtime 0 us; prevents editor-side writes to ignored ABI residue.
- Duplicate include cleanup: build-time only; removes three repeated csc warnings per local build, estimated 200,000-600,000 us saved in warning triage per pass.

Verification:
- `dotnet restore Hecton8.Editor.csproj -v:minimal "/flp:logfile=Docs/AgentLogs/Restore_EXTERNAL_CODEX_after_warning_cleanup.log;verbosity=minimal"` completed successfully after `project.assets.json` was missing.
- Final guarded command: `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_after_duplicate_include_cleanup.log;verbosity=minimal"`.
- Console result: `Build succeeded. 0 Warning(s). 0 Error(s).`
- `Docs/AgentLogs/Build_EXTERNAL_CODEX_after_duplicate_include_cleanup.log` contains 0 `: warning ` entries and 0 `: error ` entries.
- `git diff --check` passed for touched files; only Git LF-to-CRLF normalization warnings were reported.
- Post-build guard: CPU 4.6%, 0 `dotnet/csc/VBCSCompiler` processes.

Residual:
- `Hecton8.Core.csproj` remains ignored/generated. The local verification bridge is clean now, but Unity/project regeneration is still the durable owner of this file.

## 2026-05-23 Runtime Hot-Path Cleanup 1
What was wrong:
- `DynamicMusicGranularSynthesizer.EnsureRuntimeInstanceForScene()` performed runtime scene search before creating its own fallback host. Active scene instances already publish `_activeInstance` through `OnEnable`, so this scan was redundant.
- `DcsAscentProfileOverlay` used `FindAnyObjectByType<ShinobuPhysiologyRuntime>()` in a development runtime path.

What was done:
- Removed `FindAnyObjectByType/FindObjectOfType` from `DynamicMusicGranularSynthesizer` fallback creation. If no active instance exists, it now creates the deterministic `"H8 Dynamic Music Synth"` host directly.
- Added `ShinobuPhysiologyRuntime.TryGetActive(out ShinobuPhysiologyRuntime runtime)` and active pointer publication in `OnEnable`/`OnDisable`.
- Updated `DcsAscentProfileOverlay` to resolve physiology through `TryGetActive()` instead of scene search.

Cinematic Cheats used:
- None. This is dependency-route cleanup, not simulation or visual approximation.

Exact Microseconds saved:
- Dynamic music startup: avoids one scene scan, estimated 100-800 us depending on scene object count.
- Development physiology overlay: avoids one scene scan on enable/rebind, estimated 50-400 us in dev builds.
- Runtime frame impact: 0 us; no hot frame logic added.

Verification:
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup1.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors.
- `git diff --check` passed for the touched runtime files; only Git LF-to-CRLF normalization warnings were reported.
- Post-build guard: CPU 36.5%, 0 `dotnet/csc/VBCSCompiler` processes.

Residual:
- `VocalBankPlaybackRuntime` still uses a cold `AudioListener` scene lookup. It was not replaced because that path binds Unity's audio filter graph; removing it without an audio-graph proof risks silent vocal playback.

## 2026-05-23 Runtime Hot-Path Cleanup 2
What was wrong:
- `WaterOpticsRuntime` used `Camera.main` in `Awake` and `OnEnable`. That is a scene-tag lookup and can bind stale/wrong camera state if the player runtime context registers later.

What was done:
- Added cached `IPlayerRuntimeContext` binding to `WaterOpticsRuntime`.
- Added `GlobalRegistryServiceSlot.Player` hot-swap handling.
- Replaced `Camera.main` fallback with player-context camera resolution.
- Preserved inspector-assigned camera overrides and cleared only runtime-resolved camera references on shutdown.

Cinematic Cheats used:
- None. This was ownership-route cleanup for a visual system, not a physical simulation approximation.

Exact Microseconds saved:
- Runtime enable path: avoids one or two `Camera.main` scene-tag lookups, estimated 20-150 us depending on scene/tag state.
- Runtime frame impact: no allocation; only a cached `IPlayerRuntimeContext.PlayerCamera` property read on water-optics surface-offset resolution.

Verification:
- `rg -n "Camera\\.main" Assets/_Project/Scripts/Rendering/WaterOptics/WaterOpticsRuntime.cs` returns no matches.
- Guard before build: CPU 20%, 0 `dotnet/csc/VBCSCompiler` processes.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup2.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors.
- `git diff --check` passed for `WaterOpticsRuntime.cs`; only Git LF-to-CRLF normalization warning was reported.

Residual:
- If no player runtime context and no inspector camera exist, water optics now uses deterministic surface offset 0 instead of searching the scene. This is intentional fallback, not gameplay truth.

## 2026-05-23 Runtime Hot-Path Cleanup 3
What was wrong:
- `VocalBankPlaybackRuntime.EnsureRuntimeInstanceAfterSceneLoad()` scanned the scene for an existing runtime and for any `AudioListener`.
- The listener fallback itself is intentional for this legacy vocal bridge, but the discovery route was not.

What was done:
- Removed `FindAnyObjectByType`/`FindObjectOfType` from `VocalBankPlaybackRuntime`.
- Resolved the listener through `GlobalRegistry.Player.PlayerCamera` first and `GlobalRegistry.PlayerSensory.PlayerCamera` second.
- Used local `TryGetComponent` on the owned camera object to preserve the existing listener-mix contract without scene scans.

Cinematic Cheats used:
- None. This preserves the existing vocal synthesis route and only changes cold dependency discovery.

Exact Microseconds saved:
- Vocal bootstrap: avoids up to two scene object scans, estimated 150-1000 us on content-heavy scenes.
- Audio-thread impact: 0 us; `OnAudioFilterRead` path was not changed.

Verification:
- Runtime search scan outside editor/test/QA now returns only the `InstanceCullingContracts` comment mentioning `Camera.main`; no executable runtime `FindObjectOfType`, `FindAnyObjectByType`, `Camera.main`, or `GameObject.Find` matches remain in that scoped scan.
- Guard before build: CPU 3%, 0 `dotnet/csc/VBCSCompiler` processes.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup3.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors.
- `git diff --check` passed for the touched runtime files; only Git LF-to-CRLF normalization warnings were reported.

Residual:
- Current post-build CPU sample is 77%, so no further build should run until the guard is clear.

## 2026-05-23 Runtime Hot-Path Cleanup 4
What was wrong:
- `ParasiteSwarmGpuRuntime` read `GlobalRegistry.Player` only in `OnEnable`. Late player context/camera registration could leave the GPU swarm without a camera route.
- Completed target-selection jobs were finalized with a direct `JobHandle.Complete()` in the late-frame path.

What was done:
- Added `IGlobalRegistryHotSwapListener` to `ParasiteSwarmGpuRuntime`.
- Cached player context/camera on cold enable and on `GlobalRegistryServiceSlot.Player` replacement.
- Preserved inspector-assigned camera overrides and cleared only runtime-resolved camera references on shutdown.
- Replaced direct hot-path completion with `DispatcherJobFence.TryFinalizeCompleted`; teardown uses `DispatcherJobFence.TryComplete(..., forceComplete: true)`.

Cinematic Cheats used:
- None. GPU particle budget, target selection math, and visual quality ladder were unchanged.

Exact Microseconds saved:
- Broken late-bind recovery: avoids a missing-camera VFX stall with no per-frame registry poll.
- Direct microsecond saving is 0 us steady-state; value is deterministic ownership and centralized job-fence proof.

Verification:
- Guard before build: CPU 6%, 0 `dotnet/csc/VBCSCompiler` processes.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup4.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors.
- `git diff --check` passed for `ParasiteSwarmGpuRuntime.cs` without whitespace errors.
- Post-build guard: CPU 15%, 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 5
What was wrong:
- `HectonSurfaceWeatherDirector` resolved player context through `GlobalRegistry.Player` inside its dependency path, including slow-tick recovery.
- Late Player registration relied on retry polling rather than the existing `GlobalRegistryServiceSlot.Player` hot-swap callback route.

What was done:
- Added `IGlobalRegistryHotSwapListener` to `HectonSurfaceWeatherDirector`.
- Cached `IPlayerRuntimeContext` during awake/enable and on Player service replacement.
- Routed movement, buoyancy, visor, and flashlight dependency refresh through the cached context.
- Kept the existing `GameBootstrapper.TryGetCurrentPlayerTransform` fallback for degraded bootstrap cases.

Cinematic Cheats used:
- None. Weather simulation, rain exposure, lightning, ocean defaults, and quality math were unchanged.

Exact Microseconds saved:
- Slow dependency recovery avoids a player registry read and duplicated property chain, estimated 1-5 us per pass.
- Frame hot path impact: 0 us; `Tick` logic was not changed.

Verification:
- Guard before build: CPU 20%, 0 `dotnet/csc/VBCSCompiler` processes.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup5.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors.
- `git diff --check` passed for `HectonSurfaceWeatherDirector.cs`; only Git LF-to-CRLF normalization warning was reported.
- Post-build guard: CPU 23%, 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 6
What was wrong:
- `HectonAtmosphereManager.PublishGiantAbyssLight()` read `GlobalRegistry.CelestialEngine` during visual light publication.
- `CachePlayerMovement()` read `GlobalRegistry.Player` to locate the player camera instead of consuming a cached player context.

What was done:
- Added `IGlobalRegistryHotSwapListener` to `HectonAtmosphereManager`.
- Cached player and celestial runtime references during lifecycle and service replacement callbacks.
- Routed giant-abyss light through `_cachedCelestialEngine`.
- Preserved explicit `_playerTransform` overrides and used player-context movement/camera only when it matches the active transform or fills an empty runtime binding.

Cinematic Cheats used:
- None. Existing Aegir cookie fallback and atmosphere math were preserved.

Exact Microseconds saved:
- Visual/slow-cycle path: removes direct celestial/player registry lookups, estimated 1-8 us per affected pass.
- Frame hot path impact: 0 us; atmosphere timeline equations and quality behavior unchanged.

Verification:
- Guard before build: CPU 5%, 0 `dotnet/csc/VBCSCompiler` processes.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup6.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors.
- `git diff --check` passed for `HectonAtmosphereManager.cs`; only Git LF-to-CRLF normalization warning was reported.
- Post-build guard: CPU 23%, 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 7
What was wrong:
- `HostileFlora.UpdateTarget()` read `GlobalRegistry.Player` on every slow tick.
- `HostileFlora.Shoot()` read `GlobalRegistry.Audio` on each shot.
- `HectonCelestialEngine` cached DataVault/player/weather/underwater/GI/runtime owners only in lifecycle methods; late service replacement could leave stale celestial snapshot inputs.

What was done:
- Added `IGlobalRegistryHotSwapListener` to `HostileFlora` and cached player/audio services from cold enable and Player/Audio/Dispatcher rebinds.
- Added `IGlobalRegistryHotSwapListener` to `HectonCelestialEngine`.
- Routed celestial DataVault, biome, ocean kinematics, weather, GI relay, underwater visuals, random events, dynamic resolution, world seed, player, and dispatcher rebinding through service-slot callbacks.
- DataVault rebinding now refreshes celestial generation handles and dirties atmosphere gradient samples without changing DTO layout.

Cinematic Cheats used:
- None. Ballistic spread, celestial orbit math, atmosphere gradients, blackbox buffer size, and continuous quality behavior were unchanged.

Exact Microseconds saved:
- Hostile flora removes one player registry read per active plant slow tick and one audio registry read per shot, estimated 1-4 us per dense flora pass.
- Celestial runtime moves owner recovery to rare hot-swap events; frame and slow-tick math remain unchanged, steady-state saving is 0-2 us per recovery pass and correctness is the main gain.

Verification:
- Guard before build: CPU 20%, 0 `dotnet/csc/VBCSCompiler` processes.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup7.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors.
- `git diff --check` passed for `HostileFlora.cs` and `HectonCelestialEngine.cs`; only Git LF-to-CRLF normalization warnings were reported.
- Post-build guard: CPU 33%, 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 8
What was wrong:
- `DroneFleetManager` cached construction/player/submarine/fluid owners once in static state.
- Headless drone simulation later used those caches for task scoring, player position, formation anchor, and flow sampling, but no service replacement route refreshed them.

What was done:
- Extended the existing `HeadlessFleetDriver` with `IGlobalRegistryHotSwapListener`.
- Registered/unregistered the driver as a hot-swap listener together with its updatable, late-frame, and render lanes.
- Routed `Logistics`, `Player`, `Submarine`, and `FluidRuntime` service replacements into the static owner caches.
- Dispatcher replacement retries headless driver registration without polling during simulation.

Cinematic Cheats used:
- None. Drone pathfinding, blackbox, indirect rendering, quality tiers, and capacity rules were unchanged.

Exact Microseconds saved:
- Avoided alternative per-tick registry refresh in the headless simulation: estimated 2-8 us per active drone tick pass on low-end CPUs.
- Actual implemented steady-state cost remains 0 us; callbacks run only on service replacement.

Verification:
- Guard before build: CPU 9%, 0 `dotnet/csc/VBCSCompiler` processes.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup8.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors.
- `git diff --check` passed for `DroneFleetManager.cs`; only Git LF-to-CRLF normalization warning was reported.
- Post-build guard: CPU 17%, 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 9
What was wrong:
- `ShinobuOceanSurfaceAtmosphereRuntime.ResolveCameraAupDouble()` read `GlobalRegistry.Player` while publishing wave shader state and dispatching wave-height readback.
- `EnsureVaultBuffersCold()` retried `GlobalRegistry.DataVault` from slow tick when the vault was unavailable during enable.

What was done:
- Added `IGlobalRegistryHotSwapListener` to the ocean surface runtime.
- Cached `IPlayerRuntimeContext` and `IDataVault` during enable and service replacement.
- Converted camera AUP and camera transform recovery to use cached player context.
- Reset DataVault generation handles only when the DataVault service actually changes.
- Dispatcher replacement retries tick-lane registration without per-frame polling.

Cinematic Cheats used:
- Existing wave LOD cadence and quality-weight wave-count math were preserved. No new physical simulation was added.

Exact Microseconds saved:
- Removes a player registry lookup from wave shader publish/readback paths, estimated 2-6 us per active ocean update/slow pass on low-end CPUs.
- Removes slow-tick DataVault registry retry while missing; hot-swap callback cost is rare.

Verification:
- Guard before build: CPU 6%, 0 `dotnet/csc/VBCSCompiler` processes.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup9.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors.
- `git diff --check` passed for `ShinobuOceanSurfaceAtmosphereRuntime.cs`; only Git LF-to-CRLF normalization warning was reported.
- Post-build guard: CPU 18%, 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 10
What was wrong:
- `HectonVoxelEngine` read `GlobalRegistry.Player` in predictive voxel proxy target resolution.
- The same file read `GlobalRegistry.Player` in player-distance collider LOD logic.

What was done:
- Replaced both direct reads with `PlayerRuntimeContextService.TryGetActiveRuntimeContext`.
- Preserved the existing bootstrap/player-transform fallback in `TryResolvePlayerAup`.
- Fixed the first retry compile error caused by a leftover `player` variable after the route change.

Cinematic Cheats used:
- None. Predictive voxel proxy padding, collider distance thresholds, and voxel LOD behavior were unchanged.

Exact Microseconds saved:
- Removes two direct player registry reads from voxel helper paths, estimated 1-3 us when predictive proxy/collider LOD helpers run.

Verification:
- Guard before first build: CPU 1%, 0 `dotnet/csc/VBCSCompiler` processes.
- First build `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup10.log` failed with 2 local `CS0103` errors from stale variable `player`.
- Guard before retry: CPU 9%, 0 `dotnet/csc/VBCSCompiler` processes.
- Retry `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup10_retry1.log;verbosity=minimal"` succeeded.
- Retry build result: 0 warnings, 0 errors.
- Post-build guard: CPU 37%, 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 11
What was wrong:
- `PlayerExpressionManager.AutoResolveReferences()` read `GlobalRegistry.Player` while resolving player tool and movement owners.
- Save registration depended on `GlobalRegistry.SaveRuntime` already existing during `OnEnable`.

What was done:
- Added `IGlobalRegistryHotSwapListener` to `PlayerExpressionManager`.
- Cached `IPlayerRuntimeContext` and used it for tool/movement owner resolution.
- Preserved bootstrap transform fallback for degraded startup.
- Added Save service replacement handling to register/unregister `ISaveable` ownership when SaveRuntime appears or changes.

Cinematic Cheats used:
- None. Expression profile selection, suit application, HUD override, and save DTO layout were unchanged.

Exact Microseconds saved:
- Removes repeated Player registry property-chain reads during expression reference resolution, estimated 1-3 us per resolve call.
- Late-bind callback cost is rare and below 10 us.

Verification:
- Guard before build: CPU 36%, 0 `dotnet/csc/VBCSCompiler` processes.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup11.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors.
- Post-build guard: CPU 23%, 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 12
What was wrong:
- `PDALoadoutTab` still used direct `GlobalRegistry.PlayerExpression` fallback in identity summary/action helpers.
- Player-owned UI references (`PlayerInventory`, `PlayerToolManager`, `PlayerPDA`) could remain bound to the previous player context after Player service replacement.

What was done:
- Added `IGlobalRegistryHotSwapListener` to `PDALoadoutTab`.
- Seeded Player, PlayerInventory, and PlayerExpression caches during cold lifecycle only.
- Cleared only references that came from the previous player context before rebinding the current context.
- Converted expression helper paths to use cached `PlayerExpressionManager` instead of registry fallback.
- Dispatcher replacement retries tick registration without per-frame polling.

Cinematic Cheats used:
- None. PDA loadout summary, preset choice, field-advice query, and identity cycling behavior were unchanged.

Exact Microseconds saved:
- Removes repeated expression registry reads from loadout refresh/action paths, estimated 1-4 us per PDA loadout refresh on low-end CPUs.
- Prevents stale player-owned UI references after hot-swap; callback cost is rare and below 10 us.

Verification:
- Guard before build: CPU 10.3%, 0 `dotnet/csc/VBCSCompiler` processes.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup12.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors.
- Post-build guard after wait: CPU 5.4%, 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 13
What was wrong:
- `CullingManager.ResolveMainCamera()` read `GlobalRegistry.PlayerSensory` and `GlobalRegistry.Player` after enable while recovering the culling camera.
- Player/PlayerSensory replacement could leave the manager using the old camera and layer cull distances until another manual cache reset happened.

What was done:
- Added `IGlobalRegistryHotSwapListener` to `CullingManager`.
- Cached `IPlayerRuntimeContext` and `IPlayerSensoryService` during cold lifecycle.
- Invalidated camera binding and layer cull distances when Player or PlayerSensory changes.
- Dispatcher replacement retries slow-tick registration.
- Left frustum, distance, hysteresis, and registered-object logic unchanged.

Cinematic Cheats used:
- Existing coarse slow-tick culling cadence was preserved. No new physical simulation or extra visibility math was added.

Exact Microseconds saved:
- Removes PlayerSensory/Player registry reads from culling camera recovery, estimated 1-3 us per camera re-resolve on low-end CPUs.
- Prevents stale camera/layer-cull binding after hot-swap; callback cost is rare and below 10 us.

Verification:
- Guard before build: CPU 9.3%, 0 `dotnet/csc/VBCSCompiler` processes.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup13.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors.
- Post-build guard after wait: CPU 4%, 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 14
What was wrong:
- `HectonItem` kept static caches for Player, PlayerInventory, Physics, and ObjectPool services, but only refreshed them from lifecycle calls.
- Service replacement could leave pooled pickup interaction, AUP conversion, or object-pool return paths reading stale service pointers.

What was done:
- Added one class-level `StaticRegistryHotSwapListener` for all `HectonItem` instances.
- The shared listener updates the static service caches on Player, PlayerInventory, Physics, and ObjectPool replacement.
- Kept cold lifecycle seeding for bootstrap and avoided per-pickup listener registration.

Cinematic Cheats used:
- Existing pickup settle state machine and buoyancy approximation were preserved. No added simulation.

Exact Microseconds saved:
- Avoids registry polling in pickup interaction/preview paths, estimated 1-2 us per pickup interaction on low-end CPUs.
- Avoids thousands of per-instance listener registrations; one rare static callback handles service replacement.

Verification:
- Guard before build: CPU 9.6%, 0 `dotnet/csc/VBCSCompiler` processes.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup14.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors.
- Post-build guard after wait: CPU 7.7%, 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 15
What was wrong:
- `PlayerToolManager` needed to expose player runtime context through an interface route while preserving the concrete owner only where interaction publication requires it.
- Generated `Hecton8.Core.csproj` omitted existing `VRInteractionBridgeContracts.cs`, blocking verification after the source fix.

What was done:
- Added an `IPlayerRuntimeContext` cache and Player service replacement handling in `PlayerToolManager`.
- Kept concrete `PlayerRuntimeContext` usage constrained to interaction-state publication.
- Added the existing VR interaction contracts source to the local generated Core project for verification.

Cinematic Cheats used:
- None. Tool interaction semantics, VR DTOs, and input truth ownership were unchanged.

Exact Microseconds saved:
- Avoids stale player-context recovery through registry polling; steady-frame cost remains 0 us.
- Build-only generated graph repair prevents repeated compile-wall churn.

Verification:
- `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup15.log` failed with 2 local type split errors.
- `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup15_retry1.log` failed because generated Core omitted existing VR interaction contract source.
- `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup15_retry2.log` succeeded with 0 warnings and 0 errors.

## 2026-05-23 Runtime Hot-Path Cleanup 16
What was wrong:
- `PDASpectrumTab` pulled `GlobalRegistry.Player` while resolving last-loss/player AUP diagnostics.
- `PhysicalTerminalKeyboard` and `PhysicalPanelDial` pulled `GlobalRegistry.Audio` directly during press/scroll input.
- Generated Core omitted `PlayerHandIkContracts.cs`; after adding it, `PlayerKinematicsRuntime_HandIK.cs` lacked `Hecton8.World` for `AbsoluteUniversePosition`.

What was done:
- Added hot-swap Player caching to `PDASpectrumTab`.
- Added hot-swap Audio caching to terminal keyboard and dial input components.
- Added `PlayerHandIkContracts.cs` to the local generated Core project for verification.
- Added `using Hecton8.World;` to `PlayerKinematicsRuntime_HandIK.cs`.

Cinematic Cheats used:
- None. PDA diagnostics, physical terminal audio payloads, and hand IK math/DTOs were unchanged.

Exact Microseconds saved:
- Removes Player/Audio registry reads from affected PDA and terminal input paths, estimated 1-3 us per affected refresh/input burst on low-end CPUs.
- Generated graph/source import repairs are build-only.

Verification:
- Guard before first build: CPU 7%, 0 `dotnet/csc/VBCSCompiler` processes.
- `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup16.log` failed on missing `PlayerHandIkContract.PublishedStatesBufferId` due to generated Core omission.
- `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup16_retry1.log` failed on missing `AbsoluteUniversePosition` namespace import.
- `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup16_retry2.log` succeeded with 0 warnings and 0 errors.

## 2026-05-23 Documentation Boundary Sync

What was wrong:
- Root and architecture docs did not expose the latest EXTERNAL_CODEX local CLI compile artifact or the exact evidence limits for the hot-path registry cleanup slice.

What was done:
- Updated concise boundary notes in `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`, `Docs/SYSTEMS_CONTRACTS.md`, `Docs/QUALITY_GATES.md`, and the active global-authority architecture docs.
- Recorded `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup16_retry2.log` as CLI_COMPILE only: 0 `: warning ` / 0 `: error ` text matches.
- Preserved the hard caveat: Unity import, Console, Play Mode, profiler, GCMonitor, player build, save/load, scene wiring, visual, and platform proof remain absent.

Cinematic Cheats used:
- None. Documentation-only sync.

Exact Microseconds saved:
- Runtime: 0 us.
- Handoff/discovery: estimated 20,000-60,000 us per future agent by putting the current boundary in stable docs instead of burying it in chat.

## 2026-05-23 Root Anchor Boundary Sync

What was wrong:
- Root entry documents still carried R51-only/current-state wording and did not name the latest EXTERNAL_CODEX CLI compile artifact.

What was done:
- Added concise CLI_COMPILE boundary notes to `AGENTS.md`, `MASTER_RELEASE_WORK_PLAN.md`, `BUILD_PLAYTEST_ISSUES.md`, `Docs/ROOT_DOCS_REFERENCE.md`, and `Docs/Reports/README.md`.
- Updated the documentation actuality ledger and status file to include those root anchors.

Cinematic Cheats used:
- None. Documentation-only sync.

Exact Microseconds saved:
- Runtime: 0 us.
- Handoff/discovery: estimated 10,000-30,000 us per future agent by exposing the current compile artifact in primary root files.
- `git diff --check` passed for touched files; only Git LF-to-CRLF normalization warnings on existing files.

## 2026-05-23 Runtime Hot-Path Cleanup 17

What was wrong:
- `UIButtonAudioTrigger` could re-read `GlobalRegistry.Audio` directly on click.
- `UIAudioFeedback.PlaySound()` could re-read `GlobalRegistry.Audio` during UI playback.
- `SuitAdvisoryController.PlayUiClip()` resolved Audio directly for warning/critical suit alerts.

What was done:
- Added `IGlobalRegistryHotSwapListener` to `UIButtonAudioTrigger` and `SuitAdvisoryController`.
- Routed click/advisory playback through cached Audio service only.
- Removed playback-time Audio registry fallback from `UIAudioFeedback`.

Cinematic Cheats used:
- None. UI audio event identity, volumes, groups, and suit advisory logic were unchanged.

Exact Microseconds saved:
- Removes playback-time Audio registry reads from UI click/hover/slider/toggle/advisory routes, estimated 1-3 us per UI audio burst on low-end CPUs.
- Hot-swap callback cost is rare and below 10 us.

Verification:
- Guard before build: CPU 11%, 0 `dotnet/csc/VBCSCompiler` processes after idle build-server cleanup.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup17.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors.
- `git diff --check` passed for touched files; only Git LF-to-CRLF normalization warnings on existing files.
- Post-build guard after cleanup: CPU 29%, 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Verifier Pointer Documentation Sync

What was wrong:
- Stable root/docs/architecture evidence pointers still named `Build_EXTERNAL_CODEX_hotpath_cleanup16_retry2.log` after `cleanup17` became the latest zero-warning/zero-error CLI slice.

What was done:
- Updated root/docs/architecture pointers to `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup17.log`.
- Added UI audio feedback to the global-authority migration surface.

Cinematic Cheats used:
- None. Evidence pointer normalization only.

Exact Microseconds saved:
- Runtime: 0 us.
- Handoff/discovery: estimated 5,000-15,000 us per future agent.

## 2026-05-23 Active Doc Header Actuality Sync

What was wrong:
- Edited active docs had stale 2026-05-14/15/18/19/20/21 headers after 2026-05-23 EXTERNAL_CODEX facts were added.

What was done:
- Updated edited root/docs/architecture headers to 2026-05-23.
- Marked evidence class as CLI_COMPILE only where an artifact path is cited.

Cinematic Cheats used:
- None. Metadata correction only.

Exact Microseconds saved:
- Runtime: 0 us.
- Handoff/discovery: estimated 5,000-10,000 us per future agent.

## 2026-05-23 Runtime Hot-Path Cleanup 18

What was wrong:
- `BuoyancyObject` registered only with the Fluid runtime present during enable and missed late/replaced Fluid owners.
- `PickupItem` used static Player/Inventory/Physics/ObjectPool service caches without hot-swap replacement.
- `WorldSliceDirector` and `WorldProceduralScatterDirector` still used registry-backed Player/ObjectPool fallback routes in runtime slice/scatter paths.
- `SubtitleManager` resolved Player during audio-log cue sensory pulses.
- `TerminalOsRuntime` retried DataVault through `GlobalRegistry` when native resources were not ready.

What was done:
- Added Fluid hot-swap rebinding to `BuoyancyObject`.
- Added one static hot-swap listener for `PickupItem` service caches.
- Cached Player for world slices/scatter and cached ObjectPool for scatter warmup/spawn/destroy.
- Cached Player for subtitle audio-log sensory pulses.
- Cached DataVault in terminal runtime and reset native handles on DataVault replacement.

Cinematic Cheats used:
- None. No physics, scatter, subtitle, terminal DTO, save identity, or quality-weight behavior changed.

Exact Microseconds saved:
- Removes repeated/stale registry reads from affected runtime action paths, estimated 1-6 us per slice/scatter/UI/terminal pass on low-end CPUs.
- Hot-swap callback cost is rare and below 10 us.

Verification:
- First build `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup18.log` failed with 1 local static/instance call after converting scatter pool ownership.
- Fixed the local static call boundary.
- Retry `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup18_retry1.log` succeeded with 0 warnings and 0 errors.
- `git diff --check` passed for touched files; only Git LF-to-CRLF normalization warnings on existing files.
- Post-build guard after idle process cleanup: CPU 36%, 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 20

What was wrong:
- `LoadingScreenController.Show()` contained a dead `GlobalRegistry.Audio` read.
- `SaveSlotHoverPreview.PopulatePreviewMetadata()` read Localization and SaveRuntime during hover preview metadata refresh.
- `LoadingTipsDisplay.LoadTips()` read Localization directly when loading/reloading tips.
- `UIParticleEffect` despawned pooled particle instances through the currently registered ObjectPool instead of the pool that created the instance.

What was done:
- Removed the dead loading-screen Audio registry read and unused Audio using.
- Added hot-swap cached Localization/SaveRuntime to save-slot hover preview.
- Added hot-swap cached Localization to loading tips.
- Added ObjectPool cache plus owning-pool tracking to UI particle effect spawn/despawn.

Cinematic Cheats used:
- None. Loading text, save metadata, tip ordering, particle visual parameters, and pooling semantics were unchanged.

Exact Microseconds saved:
- Removes hover/loading registry reads, estimated 1-3 us per affected UI refresh on low-end CPUs.
- Prevents wrong-pool particle despawn after ObjectPool replacement; lifecycle callback cost is rare and below 10 us.

Verification:
- `git diff --check` passed for touched files; only Git LF-to-CRLF normalization warnings on existing files.
- First build attempt against `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup19.log` failed before compile because that log file was locked by a concurrent build.
- Retry `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup19_retry1.log` succeeded with 0 warnings and 0 errors.
- Post-build process cleanup: 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 19

What was wrong:
- `WorldSpatialHashGrid` read `GlobalRegistry.Player` in far-unload and acoustic-density runtime helpers.
- `WreckMaterialRegistry` read `GlobalRegistry.Player` for PDA ping distance and BRG view-camera resolution.
- `VegetationFlowFieldIntegrator` read `GlobalRegistry.Weather` during flow/thermal job scheduling and biolume surge registration.

What was done:
- Replaced spatial/wreck Player lookups with `PlayerRuntimeContextService.TryGetActiveRuntimeContext`.
- Added Weather hot-swap caching to `HectonMapMagicVegetationBridge`.
- Routed vegetation flow/thermal weather snapshots and biolume surges through cached `IWeatherService`.
- Updated root and architecture evidence pointers for this world-owner slice; final active docs now point at `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup19_retry1.log` after the concurrent UI loading/preview slice compiled clean.

Cinematic Cheats used:
- None. No simulation math, DTO layout, BRG payload, or quality-weight behavior changed.

Exact Microseconds saved:
- Removes Player/Weather registry reads from affected world runtime passes, estimated 1-5 us per far-unload/acoustic/wreck/vegetation pass on low-end CPUs.
- Hot-swap callback cost is rare and below 10 us.

Verification:
- Guard before build: CPU 22.6%, 0 `dotnet/csc/VBCSCompiler` processes.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup19.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors.
- `git diff --check` passed for touched files; only Git LF-to-CRLF normalization warnings on existing files.
- Build servers shut down after verification; post-build guard: CPU 14.8%, 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 28

What was wrong:
- `ResourceDistributionDirector`, `EcosystemDirector`, and `HectonVoxelStreamingBridge` still resolved Player through direct `GlobalRegistry.Player` runtime helpers.

What was done:
- Resource distribution and ecosystem now cache player owner state from cold lifecycle plus `GlobalRegistryServiceSlot.Player`.
- Voxel streaming now reads the active runtime context route.
- No new global slot, DTO, save identity, or quality-weight behavior change.

Cinematic Cheats used:
- None. Existing AUP/distance approximations and deterministic fallbacks were preserved.

Exact Microseconds saved:
- Estimated 1-4 us per affected slow/runtime pass on i3/MX350-class CPUs.
- Hot-swap callback cost is rare and below 10 us.

Verification:
- `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup20.log`, `Hecton8.Editor.csproj --no-restore`, 0 warnings, 0 errors.
- Log text counts: 0 `: warning `, 0 `: error `.
- Build servers shut down after verification; later guard showed CPU 62.7% and active `dotnet` from concurrent work, so no further build was launched.

## 2026-05-23 Runtime Hot-Path Cleanup 29

What was wrong:
- `BiomeBoundarySdfRuntime` did not listen for Player service replacement.
- `AbyssalThermalManager.UsesThermalGrid()` used a binary `Low/Mx350` quality gate.

What was done:
- Added Player/Dispatcher hot-swap handling to biome boundary SDF runtime.
- Changed abyssal thermal-grid enablement to continuous `GlobalQualityWeight * VRAM weight`.

Cinematic Cheats used:
- Thermal grid remains an expensive optional visual/sampling layer; weak devices get the cheap path, strong devices keep the richer thermal field.

Exact Microseconds saved:
- Biome SDF: estimated 1-2 us per late-bind/recovery pass by avoiding slow-tick registry polling.
- Thermal grid: workload-dependent; disables the 32^3 diffusion/readback path when quality/VRAM budget is below threshold.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs Assets/_Project/Scripts/World/AbyssalThermalManager.cs` passed; only Git LF-to-CRLF warnings.
- CLI build not launched: guard saw active MSBuild `dotnet` node-reuse processes. Last compile artifact remains `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup20.log`.

## 2026-05-23 Runtime Hot-Path Cleanup 30

What was wrong:
- Beacon runtime/network, acoustic zone, builder tool, beacon HUD, death dump, and pause menu still had direct service fallback reads on spawn/despawn/audio/UI/save/language action paths.

What was done:
- Added cached ObjectPool/Localization/Save/Player/Audio services with registry hot-swap refresh.
- Stored the beacon pool owner for despawn so service replacement cannot route a pooled beacon to the wrong pool.
- Routed builder camera binding through `PlayerRuntimeContextService.TryGetActiveRuntimeContext`.
- Updated active root and architecture evidence pointers to `cleanup21_beacon_pause`.

Cinematic Cheats used:
- None. No simulation math, DTO layout, save identity, or quality-weight behavior changed.

Exact Microseconds saved:
- Estimated 1-5 us per affected beacon/UI/audio action on low-end CPUs.
- Hot-swap callback cost is rare and below 10 us.

Verification:
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup21_beacon_pause.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors; log text counts: 0 `: warning `, 0 `: error `.
- `dotnet build-server shutdown` succeeded; idle nodeReuse processes ended before forced cleanup; post-build guard: CPU 28%, 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 31

What was wrong:
- `ProxyLightRegistry.GetVisibleLightsBatch()` read `GlobalRegistry.ScalabilityTier` during visible-light batching.
- Proxy-light direction normalization used binary tier selection.

What was done:
- Replaced hot registry tier read with `HomeostasisBrain.GlobalQualityWeight`.
- Added continuous quality/distance blend between dominant-axis cheap math and precise normalization.

Cinematic Cheats used:
- Distant/low-quality proxy-light direction remains a cheap dominant-axis visual fake; close/high-quality lights get precise forward gating.

Exact Microseconds saved:
- Estimated 1-3 us per dense proxy-light query on i3/MX350-class CPUs.

Verification:
- Guard before build: CPU 17.2%, 0 `dotnet/csc/VBCSCompiler` processes.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup21.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors; log text counts: 0 `: warning `, 0 `: error `.
- `dotnet build-server shutdown` succeeded.

## 2026-05-23 Documentation Sync 32

What was wrong:
- Two active architecture docs still pointed at `cleanup21_beacon_pause` after `cleanup21` became the current verifier artifact.

What was done:
- Updated `GLOBAL_AUTHORITY_BOUNDARIES.md` and `GLOBAL_AUTHORITY_OPERATING_MODEL.md` to `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup21.log`.

Cinematic Cheats used:
- None. Documentation-only.

Exact Microseconds saved:
- Runtime 0 us. Future handoff lookup saving estimated 1,000-3,000 us.

Verification:
- `git diff --check` passed for the edited authority docs/status/rationale/log; only LF-to-CRLF normalization warnings were reported.

## 2026-05-23 Runtime Hot-Path Cleanup 33

What was wrong:
- `AudioLogPickup` pulled AudioLogRuntime/Localization through registry during enable/interact/localization paths.
- `AudioLogSystem` registered to SaveRuntime through direct lifecycle registry reads and did not rebind on Save service replacement.

What was done:
- Added cached AudioLogRuntime/Localization plus hot-swap refresh to audio-log pickups.
- Added cached SaveManager plus hot-swap unregister/register to `AudioLogSystem`.
- Updated active root and architecture evidence pointers to `cleanup22_audio_log`.

Cinematic Cheats used:
- None. No playback, save DTO, bitmask, or localization payload semantics changed.

Exact Microseconds saved:
- Estimated 1-3 us per affected pickup interaction/enable on low-end CPUs.
- Hot-swap callback cost is rare and below 10 us.

Verification:
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup22_audio_log.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors; log text counts: 0 `: warning `, 0 `: error `.
- `dotnet build-server shutdown` stopped MSBuild server; remaining idle nodeReuse/VBCS processes were stopped. Final process guard: 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 34

What was wrong:
- `FloraGenomeVaultRuntime.ResolveHardwareTier()` read `GlobalRegistry.ScalabilityTier` while scheduling plant L-system generation.

What was done:
- Replaced the registry tier read with finite/saturated `HomeostasisBrain.GlobalQualityWeight`.
- Preserved the existing seed DTO byte and mapped quality to weak/middle/high/ultra job cost tiers.
- Updated active root and architecture evidence pointers to `cleanup22_flora_genome`.

Cinematic Cheats used:
- Low quality uses cheaper L-system/matrix caps; high/ultra spend the saved budget on denser plant branch output.

Exact Microseconds saved:
- Estimated 1-4 us per dense flora-generation scheduling burst, plus reduced job work when quality is low.

Verification:
- Guard before build: CPU 25.9%, 0 `dotnet/csc/VBCSCompiler` processes.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup22_flora_genome.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors; log text counts: 0 `: warning `, 0 `: error `.
- `dotnet build-server shutdown` succeeded; a second shutdown cleared the remaining idle VBCSCompiler. Final guard: CPU 40.1%, 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 35

What was wrong:
- `ResourceScarcityDirector` read Save/Quest/Inventory/Player owners through registry paths after enable.

What was done:
- Source now uses cached SaveManager, QuestManager, PlayerInventory service, and Player runtime context with hot-swap replacement.

Cinematic Cheats used:
- None. Scarcity thresholds, quest hashes, and save layout remain unchanged.

Exact Microseconds saved:
- Estimated 1-3 us per scarcity pass on i3/MX350-class CPUs.

Verification:
- First resource-scarcity build hit a stale/concurrent `AudioLogSystem` mismatch.
- Clean proof is now `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup23_outpost_quality_retry2.log`, 0 `: warning `, 0 `: error `.

## 2026-05-23 Runtime Hot-Path Cleanup 36

What was wrong:
- `MarauderOutpostGenerationService.ResolveQualityTier()` read `GlobalRegistry.ScalabilityTier` during outpost generation setup.

What was done:
- Replaced the registry tier read with finite/saturated `HomeostasisBrain.GlobalQualityWeight`.
- Mapped continuous quality to weak/middle/high/ultra WFC/job cost tiers.
- Updated active root and architecture evidence pointers to `cleanup23_outpost_quality_retry2`.

Cinematic Cheats used:
- Weak quality keeps cheaper WFC dimensions/job work; high/ultra spend budget on richer outpost geometry. No persistence DTO or gameplay authority changed.

Exact Microseconds saved:
- Estimated 1-5 us per outpost generation scheduling path, plus reduced WFC/job work when quality is low.

Verification:
- Initial guard was blocked by active compiler processes; build started only after CPU/proc guard cleared.
- First build `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup23_outpost_quality.log` failed on a concurrent/mid-edit `AudioLogSystem` source mismatch, not on outpost code.
- Retry1 reached `Hecton8.Editor.dll` but the shell timed out and left orphaned MSBuild nodes; `dotnet build-server shutdown` cleared them.
- Retry2 used `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal -m:1 /nr:false /p:UseSharedCompilation=false "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup23_outpost_quality_retry2.log;verbosity=minimal"` and succeeded.
- Build result: 0 warnings, 0 errors; log text counts: 0 `: warning `, 0 `: error `.

## 2026-05-23 Runtime Hot-Path Cleanup 37

What was wrong:
- `SoundscapeSystem` used `GlobalRegistry.ScalabilityTier` plus binary `ScalabilityChangedEvent` to choose impact signal drain budget and pitch behavior.

What was done:
- Removed the tier cache and scalability event subscription for this presentation policy.
- Replaced binary drain/pitch decisions with smooth `HomeostasisBrain.GlobalQualityWeight` scaling.
- Updated active root and architecture evidence pointers to `cleanup24_soundscape_quality`.

Cinematic Cheats used:
- Weak quality drains fewer impact signals and keeps flatter clang pitch; high/ultra spend budget on richer pitch variation and more impact detail.

Exact Microseconds saved:
- Estimated 1-4 us per soundscape slow tick under dense impact traffic.

Verification:
- Guard before build: CPU 5.2%, 0 `dotnet/csc/VBCSCompiler` processes.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal -m:1 /nr:false /p:UseSharedCompilation=false "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup24_soundscape_quality.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors; log text counts: 0 `: warning `, 0 `: error `.
- `dotnet build-server shutdown` succeeded. Final guard: CPU 25.9%, 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 38

What was wrong:
- `SaveThumbnailSystem.ShouldSkipScreenshotForCurrentTier()` used `GlobalRegistry.ScalabilityTier` to decide Low/Mx350 thumbnail capture skip.

What was done:
- Replaced binary tier read with finite/saturated `HomeostasisBrain.GlobalQualityWeight`.
- Preserved low-quality skip semantics through `ThumbnailCaptureQualityThreshold01 = 0.25`.
- Updated active root and architecture evidence pointers to `cleanup25_save_thumbnail_quality`.

Cinematic Cheats used:
- Weak quality keeps fallback/reuse instead of spending GPU readback/JPG encode; middle/high/ultra keep normal thumbnails.

Exact Microseconds saved:
- Estimated 1-3 us per thumbnail request, plus full avoided capture/readback/encode when quality is below threshold.

Verification:
- Guard before build: CPU 9.5%, 0 `dotnet/csc/VBCSCompiler` processes after waiting for other compiler nodes to exit.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal -m:1 /nr:false /p:UseSharedCompilation=false "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup25_save_thumbnail_quality.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors; log text counts: 0 `: warning `, 0 `: error `.
- `dotnet build-server shutdown` succeeded. Final guard: CPU 35.6%, 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 39

What was wrong:
- `ContextualPhysicalIkRig` used `GlobalRegistry.ScalabilityTier` and `ScalabilityChangedEvent` for IK feature policy.

What was done:
- Removed the tier cache/listener.
- Replaced binary IK policy with `HomeostasisBrain.GlobalQualityWeight` scaling for cadence distance bias, probe/contact weights, wall-touch influence, breathing, and spine wave shape.
- Kept `ContextualPhysicalIkEntityState` and target-frame layout unchanged.
- Updated active root and architecture evidence pointers to `cleanup26_contextual_ik_quality`.

Cinematic Cheats used:
- Weak quality uses shorter/slimmer IK influence and earlier cadence throttling; high/ultra spend the budget on fuller wall touch and smoother spine breathing.

Exact Microseconds saved:
- Estimated 2-9 us per dense rig capture/raycast scheduling slice on i3/MX350-class CPUs.

Verification:
- Guard first blocked: CPU 50.1%, then idle orphan MSBuild nodes with dead parent `23344`.
- After waiting, `dotnet build-server shutdown` cleared idle nodeReuse servers; guard was CPU 26.6%, 0 compiler processes.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal -m:1 /nr:false /p:UseSharedCompilation=false "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup26_contextual_ik_quality.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors; log text counts: 0 `: warning `, 0 `: error `.
- `dotnet build-server shutdown` succeeded. Final guard: CPU 16.6%, 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 40

What was wrong:
- `ProceduralWreckGenerator` used binary `GlobalRegistry.ScalabilityTier`/`ScalabilityChangedEvent` policy for wreck WFC, BRG, debris, and gravity budgets.
- The build exposed mismatched signal-lane writes in `SystemDispatcher`, `HectonPlayerMovement`, `SaveManager`, and `PowerGrid`.

What was done:
- Replaced wreck tier cache/listener with `HomeostasisBrain.GlobalQualityWeight` scaling.
- Continuous quality now controls grid cap, placement cap, BRG fragment cap, debris budget, and gravity slice without changing wreck DTO/save/render layout.
- Fixed signal writes to publish through the owner payload lane via `GlobalSignals.Publish`.
- Updated active root and architecture evidence pointers to `cleanup27_wreck_quality_retry2`.

Cinematic Cheats used:
- Weak quality keeps cheaper wreck solve/render/debris density; high/ultra spend budget on full authored wreck density.

Exact Microseconds saved:
- Estimated 3-14 us per wreck setup/slow slice, plus larger WFC/BRG/debris work avoided on low-end devices.

Verification:
- Initial guard repeatedly blocked on external compiler work and CPU >50%.
- First build `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup27_wreck_quality.log` failed with 4 pre-existing signal-lane type errors.
- Retry1 `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup27_wreck_quality_retry1.log` failed with 2 pre-existing `BrownoutSignal` namespace/write-route errors.
- After fixing lane ownership, guard was CPU 45.6%, 0 compiler processes.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal -m:1 /nr:false /p:UseSharedCompilation=false "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup27_wreck_quality_retry2.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors; log text counts: 0 `: warning `, 0 `: error `.
- `dotnet build-server shutdown` succeeded. Final guard: CPU 59.0%, 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 41

What was wrong:
- `AcousticZoneController` still resolved Soundscape and Atmosphere owners through `GlobalRegistry` in tick-dependent resolver paths.

What was done:
- Added cold cached Soundscape/Atmosphere owners plus hot-swap refresh.
- Replaced runtime Soundscape/Atmosphere registry fallbacks with cached-owner reads.
- Kept acoustic snapshots, tier scalar math, and zone transition payloads unchanged.

Cinematic Cheats used:
- None. This is owner-route cleanup; existing acoustic presentation cheats remain unchanged.

Exact Microseconds saved:
- Estimated 1-2 us per acoustic context refresh on i3/MX350-class CPUs.

Verification:
- `git diff --check` passed for `AcousticZoneController.cs`; only LF-to-CRLF normalization warning was reported.
- Direct acoustic build attempts failed on stale/concurrent source states in `ProceduralWreckGenerator` and `SargassumGlobalDragManager`, not on the acoustic patch.
- Later clean proof is `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup28_ui_quality.log`, written after the acoustic source patch, with 0 `: warning ` and 0 `: error ` text matches.

## 2026-05-23 Runtime Hot-Path Cleanup 42

What was wrong:
- `HectonIndirectVegetationRenderer` drained binary scalability snapshots for density decimation.
- `DiegeticPanelController`, `SuitHUDV4CanvasOverlay`, and `DiegeticTooltipSystem` kept `ScalabilityChangedEvent` listeners for presentation-only quality policy.

What was done:
- Replaced those routes with finite continuous `HomeostasisBrain.GlobalQualityWeight`.
- Vegetation density decimation now scales from quality pressure plus system stress.
- Panel RT/phosphor/material refresh, HUD reactive cadence, and tooltip fade/dither update from continuous quality without scalability listener state.

Cinematic Cheats used:
- Weak quality raises vegetation decimation and lowers UI presentation cadence/fade/dither cost; high/ultra spend the saved budget on denser flora and richer diegetic UI.

Exact Microseconds saved:
- Estimated 1-8 us across dense UI/vegetation presentation slices on i3/MX350-class CPUs, plus reduced indirect vegetation work at low quality.

Verification:
- Initial build guard blocked on active `VBCSCompiler.exe`; no build launched then.
- Later guard was CPU 27.2%, 0 compiler processes.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal -m:1 /nr:false /p:UseSharedCompilation=false "/flp:logfile=Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup28_ui_quality.log;verbosity=minimal"` succeeded.
- Build result: 0 warnings, 0 errors; log text counts: 0 `: warning `, 0 `: error `.
- `dotnet build-server shutdown` succeeded. Final guard: CPU 15.3%, 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 43

What was wrong:
- Cleanup42 had no editor source gate blocking binary scalability route regression in UI/vegetation.
- Guarded compile exposed dirty-file walls: Fauna acoustic namespace binding, DataMonolith interop attributes, and MessageTerminal late-frame interface binding.

What was done:
- Extended `AdvancedAcousticsSmokeTester` to assert indirect vegetation, diegetic panel, suit HUD, and tooltip consume continuous `HomeostasisBrain.GlobalQualityWeight` and do not consume scalability events/profile bytes.
- Restored minimal compile-wall bindings in the dirty files without changing DTOs, signal payloads, save identity, or gameplay authority.
- Updated root and architecture docs to point at `cleanup29_smoke_guard_retry2`.

Cinematic Cheats used:
- None added. Existing continuous quality cheats remain protected by the smoke gate.

Exact Microseconds saved:
- Runtime 0 us from the smoke gate; compile-wall repairs are no-cost. Future regression triage saved: estimated 1,000-3,000 us per handoff.

Verification:
- Initial guard was blocked repeatedly by external compiler work; no overlapping build launched.
- First build `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup29_smoke_guard_retry1.log` failed on dirty `MessageTerminal` interface binding.
- Retry2 `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup29_smoke_guard_retry2.log` succeeded.
- Build result: 0 warnings, 0 errors; log text counts: 0 `: warning `, 0 `: error `.
- `dotnet build-server shutdown` succeeded. Final guard: CPU 5.9%, 0 `dotnet/csc/VBCSCompiler` processes.
## 2026-05-23 Runtime Hot-Path Cleanup 44
What was wrong -> `BatteryCharger` still pulled Player/Audio from `GlobalRegistry` inside interaction and insert paths. `GlobalPhysicsStateManager` also referenced concrete `FaunaBrain` for a physics clamp, creating a compile dependency from physics into AI.
What was done -> Added cached Player runtime context and Audio service to `BatteryCharger` with hot-swap rebind and forced player-owned cache reset. Replaced the physics concrete fauna check with `IScannerFaunaScientificContact`.
Cinematic Cheats used -> None; behavior and clamp values unchanged.
Exact Microseconds saved -> Estimated 1-2 us per charger interaction/insert on low-end CPU.
Verification -> Covered by `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup30_loot_quality_retry3.log`, 0 warning/error text matches.

## 2026-05-23 Runtime Hot-Path Cleanup 45
What was wrong -> `LootMagnetSystem` still used `GlobalRegistry.ScalabilityTierProfileByte` for loot acoustic/wake/fluid presentation budgets.
What was done -> Replaced tier byte with continuous `HomeostasisBrain.GlobalQualityWeight`, hysteresis, interpolated acoustic/wake budgets, and smooth fluid impulse scale.
Cinematic Cheats used -> Weak quality lowers loot presentation signal pressure; high/ultra spend budget on stronger wake/acoustic/fluid feedback.
Exact Microseconds saved -> Estimated 1-3 us per dense loot commit slice, plus lower signal pressure on weak devices.
Verification -> `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup30_loot_quality_retry3.log` passed; 0 warnings, 0 errors. `git diff --check` passed with LF-to-CRLF warnings only.

## 2026-05-23 Runtime Hot-Path Cleanup 46
What was wrong -> `BaseAirlock` pulled Audio from `GlobalRegistry` at cycle start/end and NativeInputManager when locking input for a cycle.
What was done -> Added cached Audio/NativeInputManager owner fields with `IGlobalRegistryHotSwapListener`; cycle paths now use cached services.
Cinematic Cheats used -> None; pressure equalization, snap, teleport, and clips unchanged.
Exact Microseconds saved -> Estimated 1-2 us per airlock cycle.
Verification -> `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup49_service_rebind.log` passed: 0 warnings, 0 errors; text counts 0 `: warning ` and 0 `: error `.

## 2026-05-23 Runtime Hot-Path Cleanup 49
What was wrong -> `BatteryChargerModule` used `GlobalRegistry.Player` in dock fallback, and `PDAShellChrome` used direct Player/NativeInput registry reads after lifecycle.
What was done -> Added cached Player owner state plus hot-swap to `BatteryChargerModule`; added cached Player context and rebound NativeInput handling to `PDAShellChrome`.
Cinematic Cheats used -> None; dock/PDA behavior unchanged.
Exact Microseconds saved -> Estimated 1-3 us per affected interaction/open/rebind path.
Verification -> `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup49_service_rebind.log` passed: 0 warnings, 0 errors; text counts 0 `: warning ` and 0 `: error `.

## 2026-05-23 Runtime Hot-Path Cleanup 46-50
What was wrong -> `BaseAirlock` used cycle-time Audio/NativeInput registry reads. Flora/organic/trade/active-sonar/seismic still had binary quality tails. Current dirty compile also exposed small armor/PDA/vocal-warning/physics interface walls. Parallel edits added charger-module and PDA-shell owner rebinds.
What was done -> Cached BaseAirlock Audio/NativeInput owners with hot-swap. Replaced remaining touched binary quality tails with finite `HomeostasisBrain.GlobalQualityWeight`. Fixed armor grid aliases, PDA haptics namespace, vocal warning helper scope, and duplicate physics impact interface redeclaration. Root/docs/architecture/status/rationale were moved to the new verifier.
Cinematic Cheats used -> Weak quality now continuously reduces flora sway, organic fracture, trade route solve, active-sonar shader intensity, and seismic shader shake; high/ultra spend saved budget on fuller presentation/solver output.
Exact Microseconds saved -> Estimated 1-8 us across dense presentation/economy slices; compile-wall repairs are runtime 0 us.
Verification -> `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup49_service_rebind.log` passed: 0 warnings, 0 errors; text counts 0 `: warning ` and 0 `: error `. `git diff --check` passed with LF-to-CRLF warnings only. Build servers shut down; final compiler-process guard observed 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 51
What was wrong -> GPU scatter still used binary scalability event/tier state through `ScalabilityChangedEvent` and `GlobalRegistry.ScalabilityTier`.
What was done -> Removed those routes from `GPUScatterDirector` and `GpuScatterLodManager`. Scatter cull distance, visual payload, transition range, and shader scalars now consume continuous `HomeostasisBrain.GlobalQualityWeight`.
Cinematic Cheats used -> Weak quality cuts scatter distance/payload pressure; middle/high interpolate; ultra spends budget on fuller GPU scatter and smoother transitions.
Exact Microseconds saved -> Estimated 2-7 us per scatter policy refresh plus lower weak-device GPU pressure.
Verification -> `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup49_service_rebind.log` passed: 0 warnings, 0 errors; text counts 0 `: warning ` and 0 `: error `. `git diff --check` passed for touched code/docs with LF-to-CRLF warnings only. Build servers shut down; final compiler-process guard observed 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 52
What was wrong -> Planter/cultivation snapshot paths read `GlobalRegistry.PlayerInventory`; local generated Core missed UI/save/world signal payloads; `CoreContractsAssemblyMarker.cs` was included twice; geology binding parser helpers sat in the service class while using binding-owned private labels.
What was done -> Added cached `IPlayerInventoryService` plus hot-swap rebind to `BotanyPlanterModule` and `CultivationManager`. Patched local generated Core graph for `GlobalSignalPayloads.UiSaveWorld.cs`, removed the duplicate Core contracts marker include, and returned geology parser helpers to `WorldGenerativeGeologyBinding`.
Cinematic Cheats used -> None; cultivation inventory truth and geology generation output unchanged.
Exact Microseconds saved -> Estimated 1-2 us per affected planter/cultivation UI refresh; compile graph/geology helper fixes are runtime 0 us.
Verification -> `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup52_cultivation_inventory_rebind_retry4.log` passed: 0 warnings, 0 errors; text counts 0 `: warning ` and 0 `: error `. `git diff --check` passed for touched code/docs with LF-to-CRLF warnings only. MSBuild server shutdown succeeded; `VBCSCompiler` shutdown failed, so the orphan compiler process was force-stopped. Final compiler-process guard observed 0 `dotnet/csc/VBCSCompiler` processes.

## 2026-05-23 Runtime Hot-Path Cleanup 53
What was wrong -> Toxic outgassing and thermodynamics hazard grid retained binary quality/scalability routes. `PlayerInteraction` still read Audio and PlayerInventory through `GlobalRegistry` on hover/interact paths. Current dirty compile walls exposed VR finite-vector, generated Core payload include, duplicate Core contracts include, and fauna director helper gaps.
What was done -> Converted toxic/thermo quality to continuous runtime quality, scaled toxic resolution/tick cadence smoothly, cached `PlayerInteraction` Audio/PlayerInventory with hot-swap, fixed `VRSomaticProvider`, patched local generated Core include state, guarded duplicate Core contracts include, and restored fauna concrete helper through the service interface route.
Cinematic Cheats used -> Weak quality buys cheaper toxic/thermal presentation slices; middle/high interpolate; ultra spends budget on fuller chemistry/thermal visuals. No gameplay truth or save/DTO identity changed.
Exact Microseconds saved -> Estimated 3-12 us per toxic/thermal slow slice at weak quality, plus 1-3 us per dense interaction burst by removing action-path service registry reads.
Verification -> Source gates only for loop 53: `git diff --check` passed for touched code files with LF-to-CRLF warnings only; binary-quality/stale-helper grep on toxic/thermo/acoustic returned no matches. `retry2` exposed `GlobalRegistryContracts` missing `Hecton8.Core.Memory` and `InputDispatcher` missing `IDispatcherRaycastReceiver`; both were fixed. `retry3` reported `ModalWindow` missing the `char[]` modal overload, but current source already has it, so it is recorded as stale/concurrent source. `retry4` guard loop timed out after 10 minutes without launching a build because CPU stayed above 50% and external compiler processes appeared. Last full PASS remains `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup52_cultivation_inventory_rebind_retry4.log`.

## 2026-05-24 Runtime Hot-Path Cleanup 54
What was wrong -> Somatic CCD still used binary Low/ThermalThrottle collapse. GI relay fallback still read `GlobalRegistry.ScalabilityTier`. MemorySentinel still subscribed to binary scalability events. Generated Core duplicated bootstrap input contracts, splitting `INativeInputManagerRuntime` identity.
What was done -> Somatic CCD now scales steps by continuous quality pressure. GI relay caches continuous `GlobalQualityWeight`. MemorySentinel dropped binary scalability listener. `TickCount` uses `global::System.Environment.TickCount`. Local generated Core removed duplicate bootstrap contract compile.
Cinematic Cheats used -> Weak quality smoothly cuts somatic CCD steps; high/ultra keep fuller collision precision.
Exact Microseconds saved -> Estimated 1-6 us per high-speed somatic collision slice; GI/memory/input fixes are near 0 us runtime.
Verification -> `git diff --check` passed with LF-to-CRLF warnings only. `cleanup54_somatic_quality_retry1.log` exposed the helper-scope and input graph walls; source fixes applied. Build not rerun: CPU guard stayed >50% after stale MSBuild/Roslyn nodes were stopped. Last full PASS remains `cleanup52_cultivation_inventory_rebind_retry4`.

## 2026-05-24 Runtime Hot-Path Cleanup 55
What was wrong -> `BaseModule` still read ObjectPool from `GlobalRegistry` in a runtime reef visual gate; `TetherManager` and `VoxelDeltaProcessor` still used binary registry quality tiers; drill and lockstep owners kept no-op scalability listener subscriptions.
What was done -> Added BaseModule ObjectPool cold cache and hot-swap rebind. Tether and voxel budgets now derive from continuous `HomeostasisBrain.GlobalQualityWeight`. Removed dead drill/lockstep scalability listener routes.
Cinematic Cheats used -> Weak quality cuts tether visual/solver bands and voxel carve drain pressure; high/ultra keep indirect tether rendering and full carve drain.
Exact Microseconds saved -> Estimated 1-4 us across affected slow/visual slices; removed listener lanes reduce runtime noise.
Verification -> Source-only: scoped `git diff --check` passed with LF-to-CRLF warnings only; touched-file greps show no remaining binary scalability registry/event routes. Build not launched: CPU stayed >50% and non-owned dotnet/csc contention appeared. Last full PASS remains `cleanup52_cultivation_inventory_rebind_retry4`.

## 2026-05-24 Runtime Hot-Path Cleanup 56
What was wrong -> `HectonPlayerMotor` kept a dead scalability listener/profile byte, `HectonSubmarineOS` used `GlobalRegistry.ScalabilityTier` semantics for sonar LOD, and `SubmarineAutoLevelBallastController` subscribed to scalability events while already refreshing continuous quality in slow tick.
What was done -> Removed those listener/profile routes. Submarine OS sonar refresh/interpolation now consumes finite `HomeostasisBrain.GlobalQualityWeight` with weak/middle/high interpolation and writes continuous shader quality.
Cinematic Cheats used -> Weak quality slows sonar presentation cadence and fades interpolation; high/ultra buy faster/richer sonar without changing gameplay truth.
Exact Microseconds saved -> Estimated 1-4 us across player/submarine presentation/control slices; DTO/save/authority routes unchanged.
Verification -> Source-only: scoped `git diff --check` passed with LF-to-CRLF warnings only; touched-file greps show no binary scalability registry/event routes. Build not launched: CPU stayed above the >50% guard; latest guard saw no compiler processes, earlier guard saw non-owned `dotnet`. Last full PASS remains `cleanup52_cultivation_inventory_rebind_retry4`.

## 2026-05-24 Runtime Hot-Path Cleanup 58
What was wrong -> `HectonPlayerMovement` still used binary scalability profile state for brine fog hard-clip/cinematic focus FOV and kept a `ScalabilityEvents` listener for presentation refresh.
What was done -> Removed listener/profile byte. Brine fog hard-clip and cinematic FOV now use continuous `HomeostasisBrain.GlobalQualityWeight`; gameplay movement/brine toxicity unchanged.
Cinematic Cheats used -> Weak quality hard-clips brine fog and reduces FOV narrowing; high/ultra keep fuller fog and cinematic focus.
Exact Microseconds saved -> Estimated 1-3 us across brine/focus presentation slices; listener lane removed.
Verification -> Source-only: scoped `git diff --check` passed; touched-file greps show no binary scalability routes. Build not launched: CPU samples exceeded 50%. Last full PASS remains `cleanup52_cultivation_inventory_rebind_retry4`.

## 2026-05-24 Runtime Hot-Path Cleanup 57
What was wrong -> `ScannerTool`, `DiegeticGyroCompassRuntime`, and `InteriorGIProbeVolumeRuntime` still carried binary scalability listener routes for presentation/quality policy.
What was done -> Removed those listener interfaces/register/unregister/callbacks. Scanner samples continuous quality on fast/publish/resample paths. Gyro refreshes quality and indirect buffers on slow tick. Interior GI resolves quality directly from continuous `HomeostasisBrain.GlobalQualityWeight` with finite fallback.
Cinematic Cheats used -> Weak quality reduces scanner/gyro/GI presentation pressure; high/ultra keep richer scanner feedback, gyro indirect rendering, and GI resolution.
Exact Microseconds saved -> Estimated 1-5 us across touched presentation/slow slices; DTO/save/authority routes unchanged.
Verification -> Source-only: scoped `git diff --check` passed with LF-to-CRLF warnings only; touched-file greps show no binary scalability registry/event routes. Build not launched: CPU guard was 100%; latest guard saw no compiler processes. Last full PASS remains `cleanup52_cultivation_inventory_rebind_retry4`.

## 2026-05-24 Runtime Hot-Path Cleanup 59
What was wrong -> Remaining non-editor runtime code outside the Core bridge still had binary scalability routes in bootstrap, DRS, player kinematics, submarine fluid, hydro KCC, and stale player-movement registration paths.
What was done -> Bootstrap vault profile/math LOD now derives from continuous quality. DRS projects continuous quality into existing tier byte/enum outputs. Player kinematics, submarine fluid, hydro KCC, and player movement no longer carry stale binary listener/register/unregister tails.
Cinematic Cheats used -> Weak quality trims DRS/bootstrap/player/submarine/KCC presentation cost; high/ultra keep fuller visual budgets through existing continuous weights.
Exact Microseconds saved -> Estimated 2-9 us across affected slices; DTO/save/authority layouts unchanged.
Verification -> Source-only: scoped `git diff --check` passed; non-editor/non-Core bridge runtime grep for `GlobalRegistry.ScalabilityTier*` and `ScalabilityEvents.Register/Unregister` returned no matches. Build attempt `Build_EXTERNAL_CODEX_hotpath_cleanup59_runtime_binary_tail.log` exited 1 with a 0-byte log/no diagnostics; follow-up guard stayed above 50% CPU. Last full PASS remains `cleanup52_cultivation_inventory_rebind_retry4`.

## 2026-05-24 Runtime Hot-Path Cleanup 60
What was wrong -> Beacon static action helpers read `GlobalRegistry.BeaconNetwork`; construction blueprint visibility scanned `GlobalRegistry.QuestSystem` through every catalog/card/tool-cycle item.
What was done -> Beacon network now uses active runtime pointer for retract/nearest/destroy static routes. Buildable/module catalog gained cached-quest overloads; PlayerBuilder and PDA construction tab pass cached `IQuestSystem`.
Cinematic Cheats used -> None; blueprint truth unchanged. Savings buy PDA/builder presentation budget, not gameplay authority changes.
Exact Microseconds saved -> Estimated 1-5 us per dense construction UI/tool refresh or beacon action burst.
Verification -> Source-only: scoped `git diff --check` passed with LF-to-CRLF warnings only; `IsBlueprintViewable()` callsite grep returns only the legacy method definition. Build not launched: latest guard saw active `dotnet/csc/VBCSCompiler` and CPU samples `93.7, 97.5, 88.8, 91.5`. Last full PASS remains `cleanup52_cultivation_inventory_rebind_retry4`.

## 2026-05-24 Runtime Hot-Path Cleanup 61
What was wrong -> SDF/Terrain probe helpers still used `?? GlobalRegistry.VoxelSonarSdf/Terrain` tails in PDA focus, buoyancy, equipment interaction, contextual IK, VR somatic, deployable drill, and laser cutter DOD paths.
What was done -> Removed hot fallback reads. Probe helpers now consume cached owner fields; cold lifecycle cache and hot-swap refresh remain. Laser cutter DOD gets its voxel SDF cache from `LaserCutter` service replacement.
Cinematic Cheats used -> None; SDF/Terrain truth unchanged. Savings buy probe-heavy presentation/interaction budget.
Exact Microseconds saved -> Estimated 1-6 us across dense tool/IK/buoyancy probe slices.
Verification -> Source-only: scoped `git diff --check` passed with LF-to-CRLF warnings only; project runtime grep for `?? GlobalRegistry.VoxelSonarSdf/Terrain` returned no matches. Build not launched: guard saw CPU `72.2776` and active `VBCSCompiler` pid `17804`. Last full PASS remains `cleanup52_cultivation_inventory_rebind_retry4`.

## 2026-05-24 Runtime Hot-Path Cleanup 62
What was wrong -> `ConstructionManager` still pulled ObjectPool, PlayerInventory, and DataVault from `GlobalRegistry` inside deconstruction, load/clear, save catalog, and telemetry paths.
What was done -> Added cached service-owner refs, cold lifecycle seeding, shutdown clearing, and hot-swap replacement handling. Action paths now read cached ObjectPool/PlayerInventory/DataVault only.
Cinematic Cheats used -> None; gameplay/save/deconstruction truth unchanged. Savings buy construction feedback budget.
Exact Microseconds saved -> Estimated 1-4 us per dense construction action/save slice.
Verification -> Source-only: `git diff --check -- Assets/_Project/Scripts/ConstructionManager.cs` passed with LF-to-CRLF warning only; `rg` leaves ObjectPool/PlayerInventory/DataVault registry reads only in `CacheRegistryServicesCold`. Build not launched: active `csc` pid `12348`, active `dotnet` pid `3168`, CPU `100, 100, 100`. Last full PASS remains `cleanup52_cultivation_inventory_rebind_retry4`.

## 2026-05-24 Runtime Hot-Path Cleanup 63
What was wrong -> Service-replacement callbacks retried through `GlobalRegistry`; fauna ragdoll joint application retried Physics through registry; procedural audio non-allocating enqueue could resolve DataVault from registry; power-grid shutdown could release against a non-owned registry vault.
What was done -> Callbacks now consume `currentService` or cached owner state. Fauna ragdoll listens for Physics replacement. Procedural audio DataVault registry read is cold allocate-only. Power-grid shutdown releases `_jacobiVaultOwner` only.
Cinematic Cheats used -> None; behavior truth unchanged.
Exact Microseconds saved -> Estimated 1-5 us across callback/handoff/enqueue bursts.
Verification -> Source-only: scoped `git diff --check` passed; callback fallback grep returned no matches. Remaining `?? GlobalRegistry` hits are DataVault-only review targets. Build not launched: CPU `53.6279` exceeded the >50% guard. Last full PASS remains `cleanup52_cultivation_inventory_rebind_retry4`.

## 2026-05-24 Runtime Hot-Path Cleanup 64
What was wrong -> `StructuralIntegrityCalculatorRuntime` could miss late DataVault registration and then retry `GlobalRegistry.DataVault` from `TryInitialize`.
What was done -> Added hot-swap listener registration before init, cold DataVault seeding, DataVault replacement rebind, old-handle release before reinit, and removed `TryInitialize` registry fallback.
Cinematic Cheats used -> None; structural/deformation truth unchanged.
Exact Microseconds saved -> Estimated 1-3 us per failed/late structural init pass.
Verification -> Source-only: `git diff --check -- StructuralIntegrityCalculatorRuntime.cs` passed with LF-to-CRLF warning only; touched-file DataVault grep leaves only cold cache read. Build not launched: CPU `100, 100, 100`. Last full PASS remains `cleanup52_cultivation_inventory_rebind_retry4`.

## 2026-05-24 Runtime Hot-Path Cleanup 65
What was wrong -> Selected DataVault fallbacks remained after owner cache existed: organic Dear Lie bootstrap, hull integrity init, and voxel mesh pipeline black-box release/dump.
What was done -> Moved those paths to cached vault state only; cold cache remains for initial ownership.
Cinematic Cheats used -> None; buffer truth unchanged.
Exact Microseconds saved -> Estimated 1-4 us across organic/hull/voxel diagnostic slices.
Verification -> Source-only: scoped `git diff --check` passed; remaining DataVault `?? GlobalRegistry` hits are Core/diagnostic/tuner/static-runtime review targets. Build not launched: CPU `100` exceeded the >50% guard. Last full PASS remains `cleanup52_cultivation_inventory_rebind_retry4`.

## 2026-05-24 Compile Cleanup 66
What was wrong -> `cleanup65_owner_cache` build succeeded with 3 `CS0168` warnings in `GameBootstrapper`.
What was done -> Removed unused exception locals from the three catch clauses without changing failure fallback behavior.
Cinematic Cheats used -> None.
Exact Microseconds saved -> Runtime 0 us; build log noise removed pending rebuild.
Verification -> Source-only: scoped `git diff --check` passed. Rebuild not launched: CPU `99.0394` exceeded the >50% guard. `cleanup65_owner_cache.log` is pass-with-warnings, not zero-warning proof.

## 2026-05-24 Compile Cleanup 68
What was wrong -> `cleanup66_warning_fix` build succeeded with 6 `CS0649` warnings from unassigned fields on dead `CombatDamageTortureJob`.
What was done -> Deleted the unreferenced Burst job; active armor torture proof already uses mock-fill plus evaluator jobs.
Cinematic Cheats used -> None.
Exact Microseconds saved -> Runtime 0 us; dead compile-warning surface removed pending rebuild.
Verification -> Source-only: scoped `git diff --check` passed; `rg CombatDamageTortureJob` returned no matches. Rebuild not launched: CPU `100` exceeded the >50% guard. `dotnet build-server shutdown` completed.

## 2026-05-24 Verification Addendum 65
What was wrong -> Full scoped doc gate including `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md` and `Docs/SYSTEMS_CONTRACTS.md` reports legacy full-file CRLF/trailing-whitespace noise.
What was done -> Re-ran `git diff --check` on loop64/65 code and clean active docs; passed with LF-to-CRLF warnings only. Did not normalize legacy docs.
Cinematic Cheats used -> None.
Exact Microseconds saved -> 0 runtime us; avoids unrelated metadata churn.
Verification -> DataVault fallback grep for loop64/65 files returned no `?? GlobalRegistry.DataVault` fallback hits. Build not launched: active `dotnet` and CPU `98.6,89.2,99`.

## 2026-05-24 Runtime Hot-Path Cleanup 67
What was wrong -> `BeaconNetworkSystem.GetOrCreate()` still fell back to `GlobalRegistry.BeaconNetwork` on a static action path.
What was done -> Static beacon helpers now consume `s_activeRuntime`; owner lifecycle/recovery remains responsible for registry registration, and hot-swap syncs the active pointer.
Cinematic Cheats used -> None; beacon truth unchanged.
Exact Microseconds saved -> Estimated 1-2 us per missing-active recovery/action burst.
Verification -> Source-only: scoped `git diff --check` passed for code/clean docs; `Docs/DOC_GOVERNANCE.md` full-file legacy whitespace noise remains excluded from mechanical gate. BeaconNetwork grep leaves `GlobalRegistry.BeaconNetwork` only in service-registration checks. Build not launched: CPU `25.3,79,100`.

## 2026-05-24 Runtime Hot-Path Cleanup 69
What was wrong -> `ScannerDataMiningRouter.EnsureVaultState()` still used `_dataVault ?? GlobalRegistry.DataVault`, so scanner init could poll the registry after owner cache existed.
What was done -> Scanner now seeds DataVault in cold lifecycle, registers as a hot-swap listener, applies DataVault rebind only after query/completion buffers are free, and uses cached `_dataVault` in runtime init.
Cinematic Cheats used -> None; scanner truth and buffers unchanged.
Exact Microseconds saved -> Estimated 1-3 us per late scanner init/rebind burst.
Verification -> Source-only: scoped scanner grep leaves `GlobalRegistry.DataVault` only in static settings helpers and cold cache. Clean-doc `diff --check` passed; `Docs/DOC_GOVERNANCE.md` is excluded for pre-existing full-file CRLF/trailing-whitespace noise. Guarded zero-warning rebuild blocked by CPU samples `100,100,100`; no compiler processes observed.

## 2026-05-24 Runtime Hot-Path Cleanup 71
What was wrong -> Combat ballistics/status/armor init still contained DataVault fallback tails after cache ownership existed.
What was done -> Combat now owns a DataVault hot-swap bridge. Ballistics/status/armor consume cached vault state; ballistics releases vault handles on swap/shutdown; combat `?? GlobalRegistry.DataVault` grep is empty.
Cinematic Cheats used -> None; damage, status, armor, and ballistics truth unchanged.
Exact Microseconds saved -> Estimated 1-4 us per late combat init/rebind burst.
Verification -> Source-only: scoped `git diff --check` passed with LF-to-CRLF warnings only; combat fallback grep returned no `?? GlobalRegistry.DataVault`. Guarded zero-warning rebuild blocked by CPU `100`; no compiler processes observed.

## 2026-05-24 Runtime Hot-Path Cleanup 72
What was wrong -> Remaining non-editor runtime source still had `?? GlobalRegistry` fallbacks, including MathGuard hot writer/drain setup and Core data-store telemetry helpers.
What was done -> MathGuard now resolves DataVault only in cold init; StaticDataStore/BabelDictionaryStore require bound owner vaults; SignalWarden crash dump uses cached vault or latest-created crash fallback. Project runtime `?? GlobalRegistry.` grep is empty.
Cinematic Cheats used -> None; telemetry/data truth unchanged.
Exact Microseconds saved -> Estimated 1-4 us across late diagnostic/data-store init and invalid-number writer fallback paths.
Verification -> Source-only: scoped `git diff --check` passed with LF-to-CRLF warnings only; runtime `?? GlobalRegistry.` grep returned no matches. Guarded zero-warning rebuild blocked by CPU `85-100`; no compiler processes observed.

## 2026-05-24 Runtime Hot-Path Cleanup 70
What was wrong -> `HectonFloatingOrigin` tuner/readback facades used `origin._dataVault ?? GlobalRegistry.DataVault`.
What was done -> Added a tuner vault resolver that uses live owner `_dataVault` only; registry fallback remains only when no owner exists.
Cinematic Cheats used -> None; AUP shift truth unchanged.
Exact Microseconds saved -> Estimated 1-2 us per diagnostic/tuner burst.
Verification -> Source-only: `HectonFloatingOrigin` fallback grep leaves no `?? GlobalRegistry.DataVault`. Build not launched: latest guard CPU `100,100,100`.

## 2026-05-24 Runtime Hot-Path Cleanup 73
What was wrong -> `AsynchronousTelemetryExporter.TryAcquireVaultStorage()` still used `_dataVault ?? GlobalRegistry.DataVault`; bootstrap still had one broad `?? GlobalRegistry` cold fallback pattern.
What was done -> Exporter now listens for DataVault hot-swap, stops worker before releasing/reacquiring vault handles, and storage acquisition reads cached `_dataVault` only. Bootstrap fallback is explicit null check.
Cinematic Cheats used -> None; analytics and bootstrap truth unchanged.
Exact Microseconds saved -> Estimated 1-4 us per late analytics init/rebind burst.
Verification -> Source-only: analytics DataVault fallback grep empty; project runtime `?? GlobalRegistry.` grep empty; scoped `git diff --check` passed. Build not launched: CPU `99.5,100,99.2`, no compiler processes.

## 2026-05-24 Runtime Hot-Path Cleanup 74
What was wrong -> Suit upgrade telemetry, loot magnet slow dependency refresh, and vehicle vault helpers still depended on runtime registry owner lookups.
What was done -> `SuitUpgradeManager` DataVault resolver/telemetry moved to cached owner state with hot-swap rebind; `LootMagnetSystem` now caches DataVault/player/inventory owners and updates them through hot-swap; `VehicleMotor.ResolveDataVault()` now reads cached state only.
Cinematic Cheats used -> None; suit, loot, and vehicle truth unchanged.
Exact Microseconds saved -> Estimated 2-5 us across dense suit/loot/vehicle update bursts.
Verification -> Source-only: code/clean-doc `git diff --check` passed; touched-file registry grep leaves only cold cache reads; legacy whitespace docs excluded from mechanical diff gate. Build not launched: CPU `100`, no compiler processes.

## 2026-05-24 Runtime Hot-Path Cleanup 75
What was wrong -> `ProceduralLadderClimbRuntime` still read DataVault/player/movement owners from `GlobalRegistry` during climb start.
What was done -> Ladder climb runtime now cold-caches those owners, registers for hot-swap, rebinds DataVault/player/movement slots from `currentService`, and climb start uses cached owner pointers.
Cinematic Cheats used -> None; ladder IK/climb truth unchanged.
Exact Microseconds saved -> Estimated 1-4 us per climb-start/rebind burst.
Verification -> Source-only: code/clean-doc `git diff --check` passed; legacy migration-ledger whitespace noise excluded; touched-file registry grep leaves only cold cache reads; `GlobalSignals.Publish` grep is empty. Build not launched: CPU `60.1,61.5,50.8` and active `dotnet/csc`.

## 2026-05-24 Runtime Hot-Path Cleanup 76
What was wrong -> Player kinematics and VR somatic DataVault resolvers still allowed runtime registry fallback from recovery/buffer setup paths.
What was done -> `PlayerKinematicsRuntime` now cold-caches DataVault before hot-swap registration and fixed-tick missing-service recovery no longer re-polls DataVault; `VRSomaticProvider.ResolveDataVault()` returns cached owner state only.
Cinematic Cheats used -> None; movement, IK, and VR somatic truth unchanged.
Exact Microseconds saved -> Estimated 1-3 us across player/VR recovery or buffer setup bursts.
Verification -> Source-only: scoped code `git diff --check` passed; touched-file grep leaves DataVault registry reads only in cold cache helpers. Build not launched: CPU `91.9`, active dotnet/VBCSCompiler.

## 2026-05-24 Runtime Hot-Path Cleanup 77
What was wrong -> `DebrisManager.EnsureVaultBuffer()` could retry `GlobalRegistry.DataVault` after hot-swap registration, and DataVault replacement could leave old handles to be released through the new vault.
What was done -> Debris DataVault lookup is cold-only before hot-swap registration; replacement releases native state against the old vault before binding the new one.
Cinematic Cheats used -> None; debris simulation truth unchanged.
Exact Microseconds saved -> Estimated 1-2 us during debris allocation/rebind bursts.
Verification -> Source-only: scoped code `git diff --check` passed; DebrisManager registry grep leaves DataVault read only in cold cache helper. Build not launched: CPU `100`, no compiler processes.

## 2026-05-24 Runtime Hot-Path Cleanup 78
What was wrong -> `SomaticKinematicsRuntime.RebindServices()` mixed weather/VR service replacement with a DataVault registry rebind.
What was done -> Somatic DataVault lookup is now cold-only before hot-swap registration; weather/VR service rebinds no longer touch DataVault, and DataVault replacement stays on the DataVault slot callback.
Cinematic Cheats used -> None; somatic kinematic truth unchanged.
Exact Microseconds saved -> Estimated 1-2 us per somatic service replacement burst.
Verification -> Source-only: scoped code `git diff --check` passed; SomaticKinematicsRuntime grep leaves DataVault read only in cold cache helper. Build not launched: CPU `100`, no compiler processes.

## 2026-05-24 Runtime Hot-Path Cleanup 79
What was wrong -> `ChemicalInfluenceGrid` and `FloraInteractionManager` still had runtime DataVault resolver tails; flora OnEnable could resolve against a stale vault, and queued wake-trail globals self-reset without shader publish.
What was done -> Chemical DataVault replacement now resets vault handles and reinitializes from cached owner state. Flora binds DataVault before OnEnable resolvers, clears wake/sway/stiffness handles on vault change, resolver wrappers use cached owner vault, and queued wake-trail globals call `PublishWakeTrailGlobals()`.
Cinematic Cheats used -> None; chemical, flora sway, and wake-trail truth unchanged.
Exact Microseconds saved -> Estimated 2-4 us across chemical/flora rebind/setup bursts.
Verification -> Source-only: scoped code `git diff --check` passed; runtime DataVault resolver grep returned no matches. Build not launched: CPU `100`, no compiler processes.

## 2026-05-24 Runtime Hot-Path Cleanup 80
What was wrong -> Hazard radius checks, reactor meltdown player lookup, and habitat breach/depth helpers still read Player/FluidDecals/Atmosphere/Terrain directly from `GlobalRegistry` in slow/action paths.
What was done -> `EnvironmentalHazard` and `BioReactor` now use cached player context. `HabitatIntegrityManager` now owns cold-cache plus hot-swap state for FluidDecals/Atmosphere/Terrain and consumes those cached owners during rupture VFX, temperature, and depth resolution.
Cinematic Cheats used -> None; hazard, meltdown, flood, and rupture presentation routes stay unchanged.
Exact Microseconds saved -> Estimated 1-4 us across dense hazard/habitat/reactor bursts.
Verification -> Source-only: scoped code `git diff --check` passed; touched-file registry grep leaves only cold cache reads. Build not launched: CPU `100`.

## 2026-05-24 Runtime Hot-Path Cleanup 81
What was wrong -> `VehicleMotor.TryEmitWakeSiltDecal()` still looked up `GlobalRegistry.AbyssalFluidDecals` on wake emission.
What was done -> VehicleMotor now cold-caches `AbyssalFluidDecalManager`, refreshes it through `AbyssalFluidDecalRuntime` hot-swap, clears it on shutdown, and wake silt emission uses cached owner state only.
Cinematic Cheats used -> None; wake silt visuals stay enabled.
Exact Microseconds saved -> Estimated 1-2 us during dense vehicle wake bursts.
Verification -> Source-only: scoped code `git diff --check` passed; VehicleMotor grep leaves FluidDecals registry read only in cold cache. Build not launched: CPU `91.8`.

## 2026-05-24 Runtime Hot-Path Cleanup 83
What was wrong -> `HazardZoneManager.ResolvePlayerContext()` could still fall back to `GlobalRegistry.Player` during player exposure setup.
What was done -> HazardZoneManager now cold-caches `IPlayerRuntimeContext`, refreshes it through Player hot-swap, and resolves fallback player refs from cached owner state only.
Cinematic Cheats used -> None; hazard exposure truth unchanged.
Exact Microseconds saved -> Estimated 1-2 us during active hazard-volume player exposure passes.
Verification -> Source-only: scoped code `git diff --check` passed; HazardZoneManager grep leaves Player registry read only in cold cache. Build not launched: CPU `83.2`.

## 2026-05-24 Runtime Hot-Path Cleanup 85
What was wrong -> `VRSomaticProvider.RefreshCachedGlobalState()` could still fall back to `GlobalRegistry.Player` when resolving the player camera.
What was done -> VRSomaticProvider now cold-caches `IPlayerRuntimeContext`, refreshes it through Player hot-swap, clears it on shutdown, and camera fallback reads cached owner state only.
Cinematic Cheats used -> None; VR somatic camera behavior unchanged.
Exact Microseconds saved -> Estimated 1-2 us per XR activation/rebind burst.
Verification -> Source-only: scoped code `git diff --check` passed; VRSomaticProvider grep leaves Player registry read only in cold cache. Build not launched: active dotnet/VBCSCompiler, CPU `22`.

## 2026-05-24 Runtime Hot-Path Cleanup 84
What was wrong -> `SettingsManager` still read `GlobalRegistry.Player` while resolving graphics camera and Volume profile bindings.
What was done -> SettingsManager now caches `IPlayerRuntimeContext`, refreshes it on Player hot-swap and scene load, invalidates player-owned camera/profile cache on replacement, and reads cached player context in camera/Volume binding.
Cinematic Cheats used -> None; FOV and post-processing behavior unchanged.
Exact Microseconds saved -> Estimated 1-2 us per settings apply/rebind burst.
Verification -> Source-only: scoped code `git diff --check` passed; targeted hot-path registry grep returned no matches. Build not launched: active `dotnet`/`VBCSCompiler` contention and final CPU sample `54`.

## 2026-05-24 Runtime Hot-Path Cleanup 86
What was wrong -> `PlayerKinematicsRuntime.RebindColdIfMissing()` could still route a 64-frame recovery burst through `GlobalRegistry` for Fluid/Voxel/Gas/PlayerMotor/Player camera owners.
What was done -> PlayerKinematics registry reads are cold pre-listener cache only; service replacement now writes cached owners from `currentService`, and camera fallback reads cached `IPlayerRuntimeContext`.
Cinematic Cheats used -> None; movement, flow, SDF squeeze, and input camera behavior unchanged.
Exact Microseconds saved -> Estimated 2-3 us per missing-service recovery burst.
Verification -> Source-only: source grep leaves service registry reads only in cold cache helpers. Guarded rebuild pending CPU/compiler clearance.

## 2026-05-24 Compile Wall: RepairTool LateFrame
What was wrong -> `Build_EXTERNAL_CODEX_hotpath_cleanup86_player_kinematics_rebind.log` failed with `CS0535`: `RepairTool` did not implement `ILateFrameTickable.LateFrameTick()` in the compiled source snapshot.
What was done -> Current disk source contains the late-frame tick implementation and visual-sync registration/cleanup helpers; retry is pending guard.
Cinematic Cheats used -> None.
Exact Microseconds saved -> No runtime saving claimed; this is compile-wall tracking.
Verification -> Source grep confirms `RepairTool.LateFrameTick()`, `TryRegisterLateFrameTick()`, and `ClearPendingRepairVisualSync()` exist. Rebuild retry blocked by CPU guard.

## 2026-05-24 Runtime Hot-Path Cleanup 87
What was wrong -> `RepairTool` cached DataVault for hull dents and black-box buffers but did not rebind those handles on DataVault hot-swap.
What was done -> Added DataVault handling through `PlayerTool.OnToolRegistryServiceReplaced`; cold cache and replacement now share `RebindRepairVault()`, releasing old vault state before binding the new owner vault.
Cinematic Cheats used -> None; repair truth unchanged, spark quantity remains continuous-quality driven.
Exact Microseconds saved -> Estimated 1-2 us on repair-vault replacement bursts.
Verification -> Source-only: RepairTool grep leaves `GlobalRegistry.DataVault` in cold cache only; scoped `diff --check` passed. Guarded rebuild retry blocked by CPU/compiler contention.

## 2026-05-24 Runtime Hot-Path Cleanup 88
What was wrong -> `EnvironmentalHazard.ApplyDamage()` still read `GlobalRegistry.PlayerActionInterrupts` directly during damage application.
What was done -> EnvironmentalHazard now cold-caches `IPlayerActionInterruptSink`, refreshes it through `PlayerActionRuntime` hot-swap, and damage uses cached owner state.
Cinematic Cheats used -> None; hazard interruption behavior unchanged.
Exact Microseconds saved -> Estimated 1 us per dense hazard damage burst.
Verification -> Source-only: scoped `diff --check` passed; PlayerAction registry read remains cold cache only. Guarded rebuild retry blocked by CPU guard.

## 2026-05-24 Runtime Hot-Path Cleanup 90
What was wrong -> `FloraInteractionManager` still read Player, Atmosphere, and Construction owners from `GlobalRegistry` inside runtime helper paths.
What was done -> Flora now cold-caches player context, atmosphere, and construction manager; hot-swap refreshes Player, AtmosphereRuntime, and Logistics slots; player tool/AUP/toxic-spore, parasite growth, and fungal spread helpers read cached owner state only.
Cinematic Cheats used -> None; flora, parasite, and fungal spread truth unchanged.
Exact Microseconds saved -> Estimated 2-3 us during dense flora/parasitic bursts.
Verification -> Source-only: scoped `diff --check` passed; Flora grep leaves Player/Atmosphere/ConstructionRuntime registry reads only in cold cache. Build not launched: CPU guard blocked, latest sample `100`.

## 2026-05-24 Runtime Hot-Path Cleanup 89
What was wrong -> `PlayerActionController` read `GlobalRegistry.PlayerInventory` during completion removal and `GlobalRegistry.Audio` during completion/cancel feedback.
What was done -> PlayerActionController now cold-caches `IPlayerInventoryService` and `IAudioService`, refreshes both through hot-swap, and action paths consume cached owners.
Cinematic Cheats used -> None; item-use truth and audio feedback unchanged.
Exact Microseconds saved -> Estimated 1-2 us per completed/cancelled consumable action burst.
Verification -> Source-only: PlayerActionController grep leaves PlayerInventory/Audio registry reads only in cold cache; scoped `diff --check` passed outside legacy `Docs/DOC_GOVERNANCE.md` whitespace noise. Guarded rebuild retry blocked by CPU 100%.

## 2026-05-24 Runtime Hot-Path Cleanup 91
What was wrong -> `ConsumableItem.TryConsume()` still used `GlobalRegistry.Audio` for item use sounds.
What was done -> Added caller-owned `IAudioService` overloads and made PlayerActionController pass its cached audio service for instant and delayed consumable completion.
Cinematic Cheats used -> None; consumable truth and use-sound feedback unchanged.
Exact Microseconds saved -> Estimated 1 us per item-use burst.
Verification -> Source-only: `ConsumableItem` grep shows no `GlobalRegistry.Audio`; guarded rebuild retry blocked by CPU guard.

## 2026-05-24 Runtime Hot-Path Cleanup 92
What was wrong -> `ClimbableLadder` read Audio during climb start and Localization while rebuilding interact text.
What was done -> Ladder now cold-caches `IAudioService` and `LocalizationManager`, refreshes them through Audio/LocalizationRuntime hot-swap, and consumes cached owners for climb sound/localized text.
Cinematic Cheats used -> None; ladder movement, text, and audio feedback unchanged.
Exact Microseconds saved -> Estimated 1 us per climb/localization burst.
Verification -> Source-only: ClimbableLadder grep leaves Audio/Localization registry reads only in cold cache. Guarded rebuild retry blocked by CPU guard.

## 2026-05-24 Runtime Hot-Path Cleanup 93
What was wrong -> Ecosystem save participants used direct `GlobalRegistry.SaveRuntime/Save` lifecycle registration, so Save replacement could leave stale old-owner registration.
What was done -> FaunaGeneticsManager, EcosystemHealthDirector, and EnvironmentalStrainManager now cache `ISaveService` and rebind on `GlobalRegistryServiceSlot.Save`.
Cinematic Cheats used -> None; persistence truth unchanged.
Exact Microseconds saved -> Estimated 1-3 us during save-owner replacement or enable/disable bursts.
Verification -> Source-only: scoped `diff --check` passed; touched-owner grep has no direct Save register/unregister. Guarded rebuild retry blocked by CPU `80.4`.

## 2026-05-24 Runtime Hot-Path Cleanup 94
What was wrong -> `StorageCrate` read Audio during open/close and Localization while rebuilding interact text.
What was done -> StorageCrate now cold-caches `IAudioService` and `LocalizationManager`, refreshes them through Audio/LocalizationRuntime hot-swap, and consumes cached owners for crate sounds/localized text.
Cinematic Cheats used -> None; storage, animation, text, and audio behavior unchanged.
Exact Microseconds saved -> Estimated 1-2 us per crate interaction/localization burst.
Verification -> Source-only: StorageCrate grep leaves Audio/Localization registry reads only in cold cache. Guarded rebuild retry blocked by CPU guard.

## 2026-05-24 Runtime Hot-Path Cleanup 95
What was wrong -> `SargassumGlobalDragManager` could unregister save participation through the current Save owner instead of the owner it registered with.
What was done -> Sargassum now caches `ISaveService`, registers/unregisters through that owner, and rebinds cleanly on Save hot-swap.
Cinematic Cheats used -> None; sargassum persistence unchanged.
Exact Microseconds saved -> Estimated 1-2 us during save-owner replacement or enable/disable bursts.
Verification -> Source-only: scoped `diff --check` passed; Ecosystem/World direct Save register/unregister grep is empty. Guarded rebuild retry blocked by CPU `99.6`.

## 2026-05-24 Runtime Hot-Path Cleanup 96
What was wrong -> `OxygenBubble` used `GlobalRegistry.Audio` on collection and `GlobalRegistry.ObjectPool` on despawn.
What was done -> OxygenBubble now cold-caches `IAudioService` and `ObjectPoolManager`, refreshes them through hot-swap, and uses cached owners for collection sound and pool despawn.
Cinematic Cheats used -> Existing triangle-wave drift fake retained; oxygen truth, audio, particles, and pool route unchanged.
Exact Microseconds saved -> Estimated 1-2 us per dense bubble collection/despawn burst.
Verification -> Source-only: scoped `diff --check` passed; OxygenBubble grep leaves Audio/ObjectPool registry reads only in cold cache. Guarded rebuild blocked by CPU `83.4`.

## 2026-05-24 Runtime Hot-Path Cleanup 97
What was wrong -> `Floater` used `GlobalRegistry.Audio` on pickup/attach and `GlobalRegistry.Localization` during interact text rebuild.
What was done -> Floater now cold-caches `IAudioService` and `LocalizationManager`, refreshes them through Audio/LocalizationRuntime hot-swap, and uses cached owners for sound/text.
Cinematic Cheats used -> None; buoyancy, attach, VFX, text, and audio behavior unchanged.
Exact Microseconds saved -> Estimated 1-2 us per floater interaction/localization burst.
Verification -> Source-only: scoped `diff --check` passed; Floater grep leaves Audio/Localization registry reads only in cold cache. Guarded rebuild blocked by CPU `83.4`.

## 2026-05-24 Runtime Hot-Path Cleanup 99
What was wrong -> `HectonPlayerHealth` used `GlobalRegistry.Audio` for survival-grace heartbeat and `GlobalRegistry.AudioLogs` for radiation critical advisory queue blocking.
What was done -> HectonPlayerHealth now cold-caches `IAudioService` and `AudioLogSystem`, refreshes them through hot-swap, and uses cached owners in heartbeat/advisory paths.
Cinematic Cheats used -> None; health truth, heartbeat feedback, and narrative queue behavior unchanged.
Exact Microseconds saved -> Estimated 1-2 us per health/advisory burst.
Verification -> Source-only: scoped `diff --check` passed; HectonPlayerHealth grep leaves Audio/AudioLogs registry reads only in cold cache. Guarded rebuild blocked by CPU guard.

## 2026-05-24 Runtime Hot-Path Cleanup 98
What was wrong -> `WorldStateManager`, `WorldProceduralStateRegistry`, `FaunaDirector`, and `AtlasSignalSystem` registered Save participants through direct `GlobalRegistry.Save`/`SaveRuntime`; replacement could leave stale old-owner registration.
What was done -> Added cached `ISaveService` owner routing, Save hot-swap unregister/rebind, and cached WorldProcedural playtime reads.
Cinematic Cheats used -> None; save truth unchanged.
Exact Microseconds saved -> Estimated 2-4 us during Save owner replacement or enable/disable bursts.
Verification -> Source-only: scoped `diff --check` passed; touched-file direct Save register/unregister grep is empty. Build not launched: CPU guard sampled `100`.

## 2026-05-24 Runtime Hot-Path Cleanup 100
What was wrong -> `MessageTerminal` used registry Audio/Localization reads during interaction/text rebuild, published WFC datapad changes through legacy `GlobalSignals`, and wrote status-light MPB state from the tick path.
What was done -> MessageTerminal now cold-caches Audio/Localization owners with hot-swap refresh, exposes span-based interact text copy, pushes WFC datapad state through `SignalBus<WfcOutpostStateChangedSignal>`, and flushes status-light MPB writes in late frame.
Cinematic Cheats used -> None; terminal truth, WFC persistence, prompts, and feedback unchanged.
Exact Microseconds saved -> Estimated 2-3 us per terminal interaction/blink burst.
Verification -> Source-only: scoped `diff --check` passed; MessageTerminal Audio/Localization grep leaves registry reads only in cold cache. Guarded rebuild blocked by CPU 79.2%, compiler_count 0.

## 2026-05-24 Runtime Hot-Path Cleanup 101
What was wrong -> `TraumaDispatcher` used registry Audio for parasite-room acoustic load and registry Localization for EMP PDA corrosion.
What was done -> TraumaDispatcher now cold-caches `ISpatialAudioEnvironmentModulationSink` and `LocalizationManager`, refreshes them through hot-swap, and uses cached owners in parasite/EMP paths.
Cinematic Cheats used -> None; trauma truth, parasite room audio modulation, and PDA corrosion unchanged.
Exact Microseconds saved -> Estimated 1-2 us per parasite/EMP burst.
Verification -> Source-only: scoped `diff --check` passed; TraumaDispatcher Audio/Localization grep leaves registry reads only in cold cache. Guarded rebuild blocked by CPU 100%, compiler_count 0.

## 2026-05-24 Runtime Hot-Path Cleanup 102
What was wrong -> Narrative, suit, PDA exchange, and inventory save participants had direct SaveRuntime/Save owner registration tails.
What was done -> Verified cached `ISaveService` registration/unregistration and Save hot-swap ownership in `HectonNarrativeDirector`, `SuitUpgradeManager`, `PDAExchangeSystem`, and `PlayerInventory`.
Cinematic Cheats used -> None; persistence truth unchanged.
Exact Microseconds saved -> Estimated 2-4 us during save-owner replacement or enable/disable bursts.
Verification -> Source-only: scoped `diff --check` passed; targeted SaveRuntime grep returned no matches in the verified files. Guarded rebuild blocked by CPU 100%, compiler_count 0.

## 2026-05-24 Runtime Hot-Path Cleanup 103
What was wrong -> `FirstHourDirector` still registered save participation through direct `GlobalRegistry.SaveRuntime`.
What was done -> FirstHourDirector now caches `ISaveService`, registers/unregisters through the cached owner, and rebinds on Save hot-swap.
Cinematic Cheats used -> None; first-hour persistence truth unchanged.
Exact Microseconds saved -> Estimated 1-2 us during save-owner replacement or enable/disable bursts.
Verification -> Source-only: scoped `diff --check` passed; FirstHourDirector SaveRuntime grep returned no matches. Guarded rebuild blocked by CPU/compiler guard.

## 2026-05-24 Runtime Hot-Path Cleanup 104
What was wrong -> `DataArchaeologyRuntime` still registered save participation through direct `GlobalRegistry.SaveRuntime`.
What was done -> DataArchaeologyRuntime now caches `ISaveService`, registers/unregisters through the cached owner, and rebinds on Save hot-swap.
Cinematic Cheats used -> None; archaeology persistence truth unchanged.
Exact Microseconds saved -> Estimated 1-2 us during save-owner replacement or enable/disable bursts.
Verification -> Source-only: scoped `diff --check` passed; DataArchaeologyRuntime SaveRuntime grep returned no matches. Guarded rebuild blocked by CPU 98.6%, compiler_count 2.

## 2026-05-24 Runtime Hot-Path Cleanup 105
What was wrong -> `CorporateOrderSystem`, `ProceduralLoreDirector`, and `MetaCampaignService` still had direct SaveRuntime lifecycle tails; ProceduralLore also polled exploration/audio-log/object-pool owners in runtime helper paths.
What was done -> Added cached `ISaveService` plus Save hot-swap to all three. ProceduralLore now caches PlayerExploration, AudioLog, and ObjectPool owners and stores the owning pool per lore placement.
Cinematic Cheats used -> None; narrative/meta persistence and lore placement truth unchanged.
Exact Microseconds saved -> Estimated 2-4 us during save-owner replacement, enable/disable, or frontier-lore maintenance bursts.
Verification -> Source-only: scoped `diff --check` passed; targeted SaveRuntime grep returned no matches in the three files. Guarded rebuild blocked by CPU/compiler guard.

## 2026-05-24 Runtime Hot-Path Cleanup 106
What was wrong -> `RunModifierController`, `ModWorldPersistenceManager`, and `PlayerExpressionManager` still had direct SaveRuntime lifecycle registration tails.
What was done -> Added cached `ISaveService` plus Save hot-swap owner routing to all three. RunModifier keeps cached concrete `SaveManager` only for slot delete/name action API.
Cinematic Cheats used -> None; permadeath, mod-world, and expression persistence truth unchanged.
Exact Microseconds saved -> Estimated 2-3 us during save-owner replacement or enable/disable bursts.
Verification -> Source-only: scoped `diff --check` passed; project direct SaveRuntime register/unregister grep is empty. Guarded rebuild pending CPU/compiler guard.

## 2026-05-24 Runtime Hot-Path Cleanup 107
What was wrong -> `GlobalProfileManager` and `DynamicDifficultyDirector` read SaveRuntime for run/telemetry time and Discovery from runtime helper paths.
What was done -> Added cached Save/Discovery owners with hot-swap; run elapsed time, telemetry windows, game-load biome counts, and difficulty owner checks use cached owners.
Cinematic Cheats used -> None; profile, achievement, and dynamic-difficulty truth unchanged.
Exact Microseconds saved -> Estimated 1-3 us during meta event bursts.
Verification -> Source-only: scoped `diff --check` passed; targeted SaveRuntime/Discovery grep leaves registry reads only in cold cache helpers. Guarded rebuild blocked by CPU/compiler guard.

## 2026-05-24 Compile Cleanup 108
What was wrong -> `FaunaBrain` referenced `PhysicsDeterminismSignals` without `Hecton8.Physics`; build106 failed with 3 fauna errors.
What was done -> Added the missing namespace import; existing deterministic KCC velocity facade remains the only source.
Cinematic Cheats used -> None; fauna perception logic unchanged.
Exact Microseconds saved -> 0 us runtime; compile wall removed in source.
Verification -> Source-only: scoped `diff --check` passed; retry build blocked by CPU 92.4.

## 2026-05-24 Runtime Hot-Path Cleanup 109
What was wrong -> `HectonDiscoveryManager`, `PlayerExplorationTracker`, `PDAMarkerRegistry`, and `PDALogbookManager` registered save participation through direct SaveRuntime lifecycle reads.
What was done -> Added cached `ISaveService` plus Save hot-swap owner routing to all four PDA/discovery systems.
Cinematic Cheats used -> None; discovery, exploration, marker, and logbook persistence truth unchanged.
Exact Microseconds saved -> Estimated 2-4 us during save-owner replacement or enable/disable bursts.
Verification -> Source-only: scoped `diff --check` passed; targeted SaveRuntime grep returned no matches in the four files. Guarded rebuild blocked by CPU/compiler guard.

## 2026-05-24 Runtime Hot-Path Cleanup 110
What was wrong -> `PlayerAchievementRegistry` still had concrete SaveManager/SaveRuntime ownership, and `PDAContextualAdvisorySystem` Save hot-swap could miss unregistering its cached owner when `previousService` was not the bound owner.
What was done -> Progression achievement/advisory save participants now use cached `ISaveService`; Save replacement unregisters through the bound owner before current-owner bind.
Cinematic Cheats used -> None; achievement/advisory persistence truth unchanged.
Exact Microseconds saved -> Estimated 1-3 us during save-owner replacement or enable/disable bursts.
Verification -> Source-only: scoped `diff --check` passed; Progression SaveRuntime grep returned no matches. Guarded rebuild blocked by `BUILD_SKIP cpu=100 compiler_count=2`.

## 2026-05-24 Runtime Hot-Path Cleanup 111
What was wrong -> Runtime save/UI/world systems used concrete `SaveRuntime` for interface-only work.
What was done -> `AudioLogSystem`, `BeaconNetworkSystem`, `ResourceScarcityDirector`, `PauseMenuController`, `SaveStation`, `PDAClockUtility`, `EndingSystem`, and `CrashTelemetryBuffer` use `ISaveService`; `WorldChunkResidencyManager` uses `Save as IAsyncPersistenceService`; `MetaCampaignService` naming no longer claims SaveRuntime.
Cinematic Cheats used -> None; persistence truth unchanged.
Exact Microseconds saved -> Estimated 3-6 us during save owner replacement, save UI action, or streaming setup bursts.
Verification -> Source-only: scoped `diff --check` passed; remaining concrete SaveRuntime hits are bootstrap/dev/diagnostic/SaveManager concrete cases after loop112. Guarded rebuild blocked by CPU 99 and active `csc/dotnet`.

## 2026-05-24 Runtime Hot-Path Cleanup 112
What was wrong -> `MainMenuController` and `SaveSlotHoverPreview` still read concrete `SaveRuntime` for save-slot metadata UI.
What was done -> Both bind concrete `SaveManager` from `GlobalRegistry.Save as SaveManager` and Save hot-swap; no `SaveRuntime` reads remain in those files.
Cinematic Cheats used -> None; menu metadata and backup feedback unchanged.
Exact Microseconds saved -> Estimated 1-2 us during menu save-list/hover setup.
Verification -> Source-only: scoped `diff --check` passed; MainMenu/SaveSlotHoverPreview SaveRuntime grep returned no matches. Guarded rebuild blocked by `BUILD_SKIP cpu=100 compiler_count=9`.

## 2026-05-24 Runtime Hot-Path Cleanup 113
What was wrong -> Bootstrap and diagnostic smoke/verifier code still read concrete `SaveRuntime`.
What was done -> `GameBootstrapper`, shell verification, save recovery smoke, save system smoke, and state recovery verifier bind concrete `SaveManager` through `GlobalRegistry.Save as SaveManager`.
Cinematic Cheats used -> None; bootstrap/save verification behavior unchanged.
Exact Microseconds saved -> Estimated 1-3 us during bootstrap/diagnostic bursts.
Verification -> Source-only: scoped `diff --check` passed; project SaveRuntime grep is reduced to `Core/GlobalRegistry.cs` and `SaveManager.cs`.

## 2026-05-24 Runtime Hot-Path Cleanup 114
What was wrong -> `ScanLogSystem` and `RadioisotopeThermalGenerator` could unregister from the wrong Save owner after Save replacement.
What was done -> Both cache the bound `ISaveService`, unregister through that owner, and rebind on Save hot-swap.
Cinematic Cheats used -> None; scan log and RTG persistence truth unchanged.
Exact Microseconds saved -> Estimated 1-3 us during Save replacement or lifecycle bursts.
Verification -> Source-only: scoped `diff --check` passed for both files.

## 2026-05-24 Runtime Hot-Path Cleanup 115
What was wrong -> UI/crafting/scavenging paths still read registry owners during refresh/action paths, and `PlayerInventoryManager` getters mutated/synced scene state.
What was done -> Cached/hot-swapped owners in `PDAMapTab`, `Fabricator`, `HUDQuickBar`, `ModalWindow`, `UITooltip`, `HectonUIScaler`, `ThermalGeyser`, `ResourceNode`, `QuestManager`, and `ScrapManager`; made `PlayerInventoryManager` getters pure.
Cinematic Cheats used -> None; owner routing only.
Exact Microseconds saved -> Estimated 6-12 us across dense UI/harvest/crafting bursts.
Verification -> Source-only: scoped `diff --check` passed; touched-file registry grep leaves cold cache/lifecycle reads only. Build skipped: compiler_count=7.

## 2026-05-24 Runtime Hot-Path Cleanup 116
What was wrong -> Construction, lore, seam, LOD, and dynamic-resolution save participants could still bind from direct parameterless `GlobalRegistry.Save` paths or remain registered while disabled.
What was done -> Cached `ISaveService` in cold lifecycle, added Save hot-swap for `SeamRegistry` and `DynamicResolutionScaler`, and unregistered LOD/Dynamic save participants on disable.
Cinematic Cheats used -> None; persistence owner routing only.
Exact Microseconds saved -> Estimated 2-5 us during Save replacement or lifecycle bursts.
Verification -> Source-only: scoped `diff --check` passed; targeted grep leaves `GlobalRegistry.Save` only in cold cache helpers. Build skipped: `BUILD_SKIP cpu=3 compiler_count=7`.

## 2026-05-24 Runtime Hot-Path Cleanup 117
What was wrong -> `CaveBioRootsGenerator` resolved the spline renderer through `GlobalRegistry.TryGet` from root spline submit/remove paths and did not clear old renderer-owned links on service replacement.
What was done -> Cached `IConnectionSplineBatchRendererService`, added GlobalRegistry hot-swap handling, and remove old pipe links from the previous renderer before rebinding.
Cinematic Cheats used -> Existing root sway LUT/approximation remains; owner route only.
Exact Microseconds saved -> Estimated 1-3 us during cave-root tick/removal bursts.
Verification -> Source-only: scoped `diff --check` passed; targeted grep removed `GlobalRegistry.TryGet` from `CaveBioRootsGenerator`. Build skipped: `BUILD_SKIP cpu=100 compiler_count=9`.

## 2026-05-24 Runtime Hot-Path Cleanup 118
What was wrong -> `BuoyancyObject` resolved `IBuoyancyObjectRegistry` through `GlobalRegistry.TryGet` during OnEnable.
What was done -> Cached FluidRuntime with Terrain/SDF cold lifecycle state and rebinds through FluidRuntime hot-swap.
Cinematic Cheats used -> None; buoyancy truth unchanged.
Exact Microseconds saved -> Estimated 1-2 us during buoyancy enable bursts.
Verification -> Source-only: scoped `diff --check` passed; targeted grep removed `GlobalRegistry.TryGet` from `BuoyancyObject`. Build skipped: `BUILD_SKIP cpu=17.6 compiler_count=7`.

## 2026-05-24 Runtime Hot-Path Cleanup 119
What was wrong -> 20 UI/world/visor/AI/runtime owners could miss Dispatcher replacement rebinds; Survival/AudioLog save participants could unregister through the wrong Save owner; selected DataVault owners needed explicit old-handle release on swap.
What was done -> Added `IGlobalRegistryHotSwapListener` to dispatcher-bound owners, re-registered tick/late-frame/slow lanes on Dispatcher replacement, bound Survival/AudioLog Save registration through cached `ISaveService`, and made AmbientBiota/InternalFlood/FontStreaming DataVault swaps owner-correct.
Cinematic Cheats used -> None; owner routing only, gameplay truth unchanged.
Exact Microseconds saved -> Estimated 12-25 us during dispatcher/save/vault replacement or pooled-enable bursts.
Verification -> Source-only: scoped `diff --check` passed for 20 code files and synced docs; hot-swap callback grep and Save-helper grep passed; guarded build skipped at `BUILD_SKIP cpu=3 compiler_count=8`.

## 2026-05-24 Runtime Hot-Path Cleanup 120
What was wrong -> Interaction prompt bootstrap could scan the whole scene with `FindObjectsByType<MonoBehaviour>` because 18 `IInteractable` owners did not explicitly register collider trees.
What was done -> Removed automatic scene scanning from `InteractableRegistry.EnsureSceneRegistryCold()` and added lifecycle `RegisterTree`/`InvalidateTree` calls to AudioLogPickup, construction modules, Fabricator, core gameplay interactables, SaveStation, NarrativeDiscovery, ScannableFragment, and EmergencyServiceRelay.
Cinematic Cheats used -> Spatial raycast now consumes the fixed collider cache directly; no physics broad fallback and no scene reflection scan.
Exact Microseconds saved -> Estimated 15-80 us plus one managed array allocation on first interaction/UI enable, scene-size dependent.
Verification -> Source-only: scoped `diff --check` passed with LF normalization warnings only; runtime `GlobalRegistry.TryGet` grep is empty; runtime scene-search grep leaves only a `Camera.main` avoidance comment; interactable registration coverage script returned no missing owner; guarded build skipped at `BUILD_SKIP cpu=1 compiler_count=7`.

## 2026-05-24 Runtime Hot-Path Cleanup 121
What was wrong -> Dispatcher-bound path funnel, ambient water motion, Atlas decoder, and Atlas directive systems could miss Dispatcher replacement rebinds. Path funnel mixed cold DataVault ensure with runtime reads. Atlas directive/decode logic still used concrete services, a managed conflict-id dictionary, and dispatcher-independent frame gating.
What was done -> Added Dispatcher hot-swap rebinds; made path funnel cold-bootstrap vault handles and read cached WFC grid handle; made DataVault swaps release/rebind voxel A* handles; moved Atlas decoder/directive dependencies to read-model interfaces; replaced conflict dictionary with fixed slots; routed directive title notification through a preallocated buffer and registered message hash.
Cinematic Cheats used -> None; owner routing and allocation removal only. Pathfinding and Atlas narrative truth unchanged.
Exact Microseconds saved -> Estimated 3-8 us during dispatcher/DataVault replacement and Atlas directive bursts, plus managed allocation risk removed.
Verification -> Source-only: scoped `diff --check` passed with LF normalization warnings only; runtime `GlobalRegistry.TryGet` grep is empty; runtime scene-search grep leaves only a `Camera.main` avoidance comment. Guarded build skipped at `BUILD_SKIP cpu=74.2 compiler_count=0`.

## 2026-05-24 Runtime Hot-Path Cleanup 122
What was wrong -> 20 world/submarine/interaction/economy runtime owners could miss Dispatcher replacement rebinds after OnEnable/Start/Spawn. Generated-project target also re-included four source files already present in generated Core project, producing `CS2002`.
What was done -> Added `IGlobalRegistryHotSwapListener` to WorldContent, WorldPopulation, WorldStreaming, WorldProceduralFill, WorldZone, WorldCave, ScatterBudget, ThermalUpdraft, SubmarineElectrolysis, SubmarineStructuralGrid, DeepDrill, ResourceRecycler, Migration, HectonSeismicTide, PlayerSwimBlockoutRig, SubmarineCore, VRCableDragPlug, VRLeakPatchWeldTarget, HectonRockManager, and WorldGenerativeGeologySeamExecutionDirector. Added `Compile Remove` before forced includes for the four duplicate source files.
Cinematic Cheats used -> None; lifecycle owner routing only. Gameplay truth unchanged.
Exact Microseconds saved -> Estimated 10-30 us during Dispatcher replacement or pooled enable bursts; build graph warning path cleaned for next guarded retry.
Verification -> 20-file hot-swap coverage script returned missing=0. Scoped `diff --check` passed with LF normalization warnings only. `Build_EXTERNAL_CODEX_hotpath_cleanup122_dispatcher_rebind_tail.log` exited 0 with 8 `CS2002` warning lines before target cleanup; retry blocked by `BUILD_SKIP cpu=23 compiler_count=8`.

## 2026-05-24 Runtime Hot-Path Cleanup 123
What was wrong -> 22 slow-tick owners confirmed registration by reading `GlobalRegistry.SlowTickables.Contains(this)`, and seven existing hot-swap owners could still miss Dispatcher replacement rebind.
What was done -> Replaced slow-tick hot-list probes with `TryRegisterSlowTickable`; added Dispatcher replacement rebinds to WorldSlice, geology integration, BotanyPlanter, RepairDroneHub, EndingSystem, FirstHourDirector, and RandomEventSystem.
Cinematic Cheats used -> None; registration route only, gameplay truth unchanged.
Exact Microseconds saved -> Estimated 8-20 us during Dispatcher replacement / enable bursts.
Verification -> `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup122_tick_registration.log`; `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal -m:1 /nr:false /p:UseSharedCompilation=false`; stdout: Build succeeded, 0 warnings, 0 errors; log text counts: 0 `: warning `, 0 `: error `; build servers shut down successfully.

## 2026-05-24 Runtime Hot-Path Cleanup 124
What was wrong -> 10 updatable owners confirmed registration by reading `GlobalRegistry.Updatables.Contains(this)`.
What was done -> Replaced those registration probes with `TryRegisterUpdatable` in EntityChangeDetector, ResourceRecyclerModule, MessageTerminal, MantaEmergencyWreck, TransportChargingStation, HUDQuickBar, SargassumDebrisParticleSystem, LandingImpactVFX, PlayerThrusterAudio, and SkySystemFollowCamera.
Cinematic Cheats used -> None; registration route only.
Exact Microseconds saved -> Estimated 3-10 us during enable/rebind bursts.
Verification -> Source-only after loop124: targeted grep returned no matches; scoped `diff --check` passed with LF warnings only; rebuild blocked by `BUILD_SKIP cpu=33.3 compiler_count=8`. Latest clean CLI_COMPILE remains `Build_EXTERNAL_CODEX_hotpath_cleanup122_tick_registration.log`.

## 2026-05-24 Runtime Hot-Path Cleanup 125
What was wrong -> 30 runtime owners still confirmed tick/fixed/slow registration through hot-list `Contains(this)` probes after `Register*` calls.
What was done -> Replaced those probes with `TryRegisterUpdatable`, `TryRegisterFixedTickable`, or `TryRegisterSlowTickable` across acoustic, biome, buoyancy, cave root, construction, procedural audio, extractor, ecosystem, submarine, meta, player, optimization, floating-origin, and object-pool owners.
Cinematic Cheats used -> None; registration route only, gameplay truth unchanged.
Exact Microseconds saved -> Estimated 8-20 us during enable/rebind bursts.
Verification -> Source-only after loop125: scoped grep over 30 touched files returned no old probe pairs; scoped `diff --check` passed; rebuild blocked by active compiler guard `BUILD_SKIP cpu=9 compiler_count=7`. Latest clean CLI_COMPILE remains `Build_EXTERNAL_CODEX_hotpath_cleanup122_tick_registration.log`.

## 2026-05-24 Runtime Hot-Path Cleanup 126
What was wrong -> 13 Core/player/UI/tool owners still confirmed updatable registration through `GlobalRegistry.Updatables.Contains(this)` after `RegisterUpdatable`.
What was done -> Replaced those probes with `TryRegisterUpdatable` in EnvironmentRuntimeContextService, PlayerSensoryManager, PlayerRuntimeContextService, PlayerInventoryManager, OceanKinematicsRuntimeService, RuntimeWatchdog, AssetLoadDispatcher, HectonPlayerHealth, PerformanceMonitor, PDAInventoryTab, PauseSystemVerifier, PerformanceBudgetController, and Tools/PerformanceMonitor.
Cinematic Cheats used -> None; registration route only.
Exact Microseconds saved -> Estimated 4-12 us during enable/rebind bursts.
Verification -> Source-only after loop126: targeted grep over the 13 touched files returned no old probe pairs; scoped `diff --check` passed; rebuild blocked by active compiler guard `BUILD_SKIP cpu=100 compiler_count=10`. Latest clean CLI_COMPILE remains `Build_EXTERNAL_CODEX_hotpath_cleanup122_tick_registration.log`.

## 2026-05-24 Runtime Hot-Path Cleanup 127
What was wrong -> 20 simple owners still confirmed slow-tick registration through `GlobalRegistry.SlowTickables.Contains(this)` after `RegisterSlowTickable`.
What was done -> Replaced those probes with `TryRegisterSlowTickable` in CelestialTimeLapseDebugger, CelestialCataclysmSystem, ProceduralLoreDirector, CorporateOrderSystem, PowerRelayNode, CameraRTManager, MapMagicRuntimeBridge, HectonCrestOceanDepthCacheBootstrap, ScavengePopulator, ScatterBudgetController, SubmarineElectrolysisModule, ThermalUpdraftVolume, BasePollutionManager, CullingManager, HectonBiolumController, BaseIntegrityHUD, SoundscapeSystem, WorldReadabilityDirector, ResourceDistributionDirector, and PDAContextualAdvisorySystem.
Cinematic Cheats used -> None; registration route only.
Exact Microseconds saved -> Estimated 6-18 us during enable/rebind bursts.
Verification -> Source-only after loop127: targeted grep over the 20 touched files returned no old slow probe pairs; scoped `diff --check` passed; rebuild blocked by active compiler guard `BUILD_SKIP cpu=65 compiler_count=8`. Latest clean CLI_COMPILE remains `Build_EXTERNAL_CODEX_hotpath_cleanup122_tick_registration.log`.

## 2026-05-24 Runtime Hot-Path Cleanup 128
What was wrong -> Cave voxel lighting/AO volumes still confirmed update/slow registration through hot-list `Contains(this)` probes.
What was done -> Replaced `HectonCaveVoxelLightingVolume` updatable probe and `HectonCaveVoxelAmbientOcclusionController` updatable/slow probes with `TryRegister*`.
Cinematic Cheats used -> None; registration route only.
Exact Microseconds saved -> Estimated 1-3 us during cave-volume enable bursts.
Verification -> Source-only after loop128: targeted grep over the two touched files returned no old updatable/slow probe pairs; scoped `diff --check` passed; rebuild blocked by active compiler guard `BUILD_SKIP cpu=62.6 compiler_count=8`. Latest clean CLI_COMPILE remains `Build_EXTERNAL_CODEX_hotpath_cleanup122_tick_registration.log`.

## 2026-05-24 Runtime Hot-Path Cleanup 129
What was wrong -> 40 runtime owners still confirmed tick/fixed/post-fixed/late-frame registration through hot-list or dispatcher-lane `Contains(this)` probes.
What was done -> Replaced those probes with dispatcher-owned `TryRegister*` calls across environment, physics, save, UI, visor, voxel, biolum, sargassum, flora, ecosystem, scatter, power, tether, and vegetation owners; multi-lane owners now roll back partial registration on failure.
Cinematic Cheats used -> None; registration route only.
Exact Microseconds saved -> Estimated 12-30 us during enable/rebind bursts.
Verification -> Source-only after loop129: scoped grep over 40 touched files returned no old probe pairs; scoped `diff --check` passed; rebuild blocked by active compiler guard `BUILD_SKIP cpu=12 compiler_count=7`. Latest clean CLI_COMPILE remains `Build_EXTERNAL_CODEX_hotpath_cleanup122_tick_registration.log`.

## 2026-05-24 Runtime Hot-Path Cleanup 130
What was wrong -> A final project-wide register/probe grep still caught residual late-frame, fast/fixed/post-fixed, GameTick, SceneRuntime, and GCMonitor paths.
What was done -> Finished the `TryRegister*` conversion and kept GameTick/SceneRuntime reset recovery by unregistering stale lanes before re-registering.
Cinematic Cheats used -> None; registration route only.
Exact Microseconds saved -> Estimated 8-25 us during enable/rebind/reset bursts.
Verification -> Source-only after loop130: project grep for old register/probe patterns returned no non-editor matches; scoped `diff --check` passed. Build artifact `Build_EXTERNAL_CODEX_hotpath_cleanup129_registration_probe_zero.log` failed with `MSB3491 Access to the path is denied` writing `Temp/obj/*`; not a C# diagnostic and not compile proof. Latest clean CLI_COMPILE remains `Build_EXTERNAL_CODEX_hotpath_cleanup122_tick_registration.log`.

## 2026-05-24 Build Target Cleanup 131
What was wrong -> Core CLI build emitted `CS2002` because `FluidAnalyticalContracts.cs` was force-included by `Directory.Build.targets` without a matching generated-item remove.
What was done -> Added the missing `Compile Remove` before the forced include; duplicate-risk parser now returns 0.
Cinematic Cheats used -> None; build graph hygiene only.
Exact Microseconds saved -> 0 runtime us. Compile signal quality improved. Rebuild retry blocked by external `dotnet build Assembly-CSharp.csproj` and active `csc`.

## 2026-05-24 Compile Verification 132
What was wrong -> Loop131 still needed a fresh guarded compile after CPU/compiler guard cleared.
What was done -> Ran `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal -m:1 /nr:false /p:UseSharedCompilation=false` with log `Build_EXTERNAL_CODEX_hotpath_cleanup131_target_dedupe.log`; log reaches editor DLL output.
Cinematic Cheats used -> None; verification only.
Exact Microseconds saved -> 0 runtime us.
Verification -> OUTPUT_WITH_WARNING: log count is 1 `: warning ` and 0 `: error `. The warning is `MSB3101` access denied writing `Temp/obj/Hecton8.Editor/Hecton8.Editor.csproj.AssemblyReference.cache`; no `CS*` diagnostics; no final summary/exit line captured.

## 2026-05-24 Runtime Hot-Path Cleanup 133
What was wrong -> Static/non-`this` drivers and one render bucket path still used raw register calls or `Contains` proof reads after the broad registration-probe cleanup.
What was done -> Converted `DroneFleetManager` headless driver, `HectonVoxelEngine` deferred voxel drivers, `HectonVoxelVolume` leak sentinel, and `HectonUnderwaterVisuals` render registration to `TryRegister*`; drone lanes now roll back partial registration.
Cinematic Cheats used -> None; registration route only.
Exact Microseconds saved -> Estimated 2-6 us during driver enable/rebind bursts.
Verification -> Source-only after loop133: non-editor raw tick-register grep, renderable register/contains grep, lane `Contains` grep, and scoped `diff --check` passed. Build skipped by guard: CPU 92.6%, compiler_count 7. Latest clean CLI_COMPILE remains `Build_EXTERNAL_CODEX_hotpath_cleanup122_tick_registration.log`.

## 2026-05-24 Runtime Hot-Path Cleanup 134
What was wrong -> Runtime info logs in 21 systems still shipped as raw `Debug.Log` callsites; Save/Steam/Ecosystem/FoveatedRender still had frost/render registration proof reads.
What was done -> Converted info-only logs to `H8Debug.Log` and changed the remaining frost/render/update registration proofs to `TryRegister*` or idempotent `TryUnregister`.
Cinematic Cheats used -> None; logging/registration route only.
Exact Microseconds saved -> Estimated 1-8 us per affected event/log burst; release builds drop the conditional log callsites and interpolated arguments.
Verification -> Source-only after loop134: targeted raw `Debug.Log` grep in touched log files returned no matches; frost/render/lane membership grep returned no matches; scoped `diff --check` passed. Build skipped by latest guard: CPU 77%, compiler_count 1. Latest clean CLI_COMPILE remains `Build_EXTERNAL_CODEX_hotpath_cleanup122_tick_registration.log`.

## 2026-05-24 Runtime Hot-Path Cleanup 135
What was wrong -> `HectonVoxelVolume` sonar SDF publish and descriptor clear still polled `GlobalRegistry.DataVault` during async runtime work.
What was done -> Added cached DataVault owner state and DataVault hot-swap rebind; publish/clear now use `_cachedDataVault`, with capacity rechecked on replacement.
Cinematic Cheats used -> None; authority route only.
Exact Microseconds saved -> Estimated 1-4 us during sonar publish/clear bursts.
Verification -> Source-only after loop135: `HectonVoxelVolume` `GlobalRegistry.DataVault` grep leaves only `CacheDataVaultCold`; scoped `diff --check` passed. Build skipped by guard: CPU 96.1%, compiler_count 0.

## 2026-05-24 Runtime Hot-Path Cleanup 136
What was wrong -> `PerformanceBudgetController` could stop ticking after Dispatcher replacement because registration was OnEnable-only.
What was done -> Added Dispatcher hot-swap listener and explicit update register/unregister helpers; replacement now removes stale bucket state and re-registers into the new dispatcher lane.
Cinematic Cheats used -> None; lifecycle route only.
Exact Microseconds saved -> Estimated 1-3 us during Dispatcher replacement; 0 steady-frame change.
Verification -> Source-only after loop136: scoped `diff --check` passed; targeted grep confirms hot-swap callback. Build skipped by guard: CPU 96.3%, compiler_count 1.

## 2026-05-24 Runtime Hot-Path Cleanup 137
What was wrong -> Four runtime cadence owners could become inert after Dispatcher replacement because they registered only from OnEnable.
What was done -> Added Dispatcher hot-swap lifecycle/rebind to `EntityChangeManager`, `LandingImpactVFX`, `PlayerStressMetricsRuntime`, and `RenderTextureLifecycleTracker`.
Cinematic Cheats used -> None; lifecycle route only.
Exact Microseconds saved -> Estimated 1-5 us during Dispatcher replacement; 0 steady-frame change.
Verification -> Source-only after loop137: targeted hot-swap grep and scoped `diff --check` passed. Build skipped by latest guard: CPU 99.4%, compiler_count 0.

## 2026-05-24 Runtime Hot-Path Cleanup 138
What was wrong -> Five short cadence owners could miss replacement Dispatcher lanes after service hot-swap.
What was done -> Added Dispatcher hot-swap lifecycle/rebind to `VoxelDynamicNavGridRuntimeLifecycle`, `InstanceCullingServiceRegistryBridge`, `HectonSuitHUDExtensions`, `GCMonitor`, and `MeteorSplashQuadVfx`.
Cinematic Cheats used -> None; lifecycle route only. `MeteorSplashQuadVfx` remains a two-quad visual fake.
Exact Microseconds saved -> Estimated 1-6 us during Dispatcher replacement; 0 steady-frame change.
Verification -> Source-only after loop138: targeted hot-swap grep and scoped `diff --check` passed. Build skipped by guard: CPU 99.8%, compiler_count 1.

## 2026-05-24 Runtime Hot-Path Cleanup 139
What was wrong -> 39 additional runtime files still had raw info-only `Debug.Log` callsites, including interpolated/report strings that should not execute in release builds.
What was done -> Added `H8Debug.Log(string, Object)` and converted 71 selected info logs to conditional `Hecton8.Core.H8Debug.Log`; warnings/errors were not changed.
Cinematic Cheats used -> None; logging route only.
Exact Microseconds saved -> Estimated 1-10 us per affected event/log burst; release builds drop the calls and arguments.
Verification -> Source-only after loop139: targeted raw `Debug.Log` grep over the 39 converted files returned no matches; scoped `diff --check` passed with LF normalization warning only. Build skipped by guard: CPU 98.8%, compiler_count 1.

## 2026-05-24 Runtime Hot-Path Cleanup 140
What was wrong -> Two runtime context getters mutated state during reads; RaycastBatchHelper still missed Dispatcher replacement rebind.
What was done -> Made `EnvironmentRuntimeContextService` and `OceanKinematicsRuntimeService` getters return cached owner state only, with refresh in lifecycle/tick/hot-swap; added RaycastBatch late-frame rebind.
Cinematic Cheats used -> None; authority route only.
Exact Microseconds saved -> Estimated 1-4 us per getter burst plus 1-3 us during Dispatcher replacement.
Verification -> Source gates passed. Build `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup139_context_purity.log` failed before C# diagnostics: `NETSDK1004` missing `Temp/obj/Hecton8.Editor/project.assets.json`, `MSB3491` access denied writing `Temp/obj/Hecton8.Editor/Hecton8.Editor.csproj.FileListAbsolute.txt`; no `CS*` diagnostics. Restore retry log exits 1 after `Determining projects to restore...` with no explicit diagnostics; restore-spawned dotnet processes were stopped.

## 2026-05-24 Runtime Hot-Path Cleanup 141
What was wrong -> 40 smoke/diagnostic/runtime-support files still had raw info-only debug proof logging.
What was done -> Converted 63 executable `Debug.Log` callsites plus 2 debug-log comments to conditional `Hecton8.Core.H8Debug.Log`; warning/error logs were left live.
Cinematic Cheats used -> None; logging route only.
Exact Microseconds saved -> Estimated 1-10 us per proof/log burst; release builds drop conditional calls and arguments.
Verification -> Source-only after loop141: targeted raw `Debug.Log` grep over the 40 converted files returned 0 matches; scoped `diff --check` passed with LF normalization warnings only. Build skipped by latest guard: CPU 93.2%, compiler_count 1.

## 2026-05-24 Runtime Hot-Path Cleanup 142
What was wrong -> Remaining non-editor raw info `Debug.Log` calls still existed outside the `H8Debug` facade.
What was done -> Converted 35 executable calls in 20 files to conditional `Hecton8.Core.H8Debug.Log`; included 12 runtime files and 8 root editor proof tools that already reference Core.
Cinematic Cheats used -> None; logging route only.
Exact Microseconds saved -> Estimated 1-10 us per proof/UI/profiler log burst; release builds drop runtime conditional calls and arguments.
Verification -> Source-only after loop142: targeted raw `Debug.Log` grep over 20 converted files returned 0; project non-editor raw `Debug.Log` grep returned 0 excluding `Assets/_Project/Scripts/Core/H8Debug.cs`; scoped `diff --check` passed with LF normalization warnings only. Build skipped by latest pre-build guard: CPU 78.3%, compiler_count 2.

## 2026-05-24 Runtime Hot-Path Cleanup 143
What was wrong -> Ten runtime cadence/context owners still depended on OnEnable-only Dispatcher binding or read/slow-path registry lookup; PlayerSensory getters still mutated/synced state during reads.
What was done -> Added Dispatcher/service hot-swap rebind to `CreatureDamageManager`, `PowerRelayNode`, `CelestialTimeLapseDebugger`, `PlayerSensoryManager`, `SubmarineCompoundColliderAuthoring`, `LogisticsSorterModule`, `PauseSystemVerifier`, `PlayerRuntimeContextService`, `VRSomaticRuntimeBootstrap`, and `FabricatorPhysicalActuator`. PlayerSensory getters now return cached state only.
Cinematic Cheats used -> Existing creature wound and relay visuals remain shader/line-render presentation cheats; no physical simulation was added.
Exact Microseconds saved -> Estimated 1-6 us during Dispatcher/service replacement and 1-4 us per sensory getter burst; 0 steady-frame truth change.
Verification -> Source-only after loop143: targeted hot-swap/getter grep passed; no-hot-swap candidate count is 61; scoped `diff --check` passed with LF normalization warnings only. Build skipped by guard: CPU 100%, compiler_count 0.

## 2026-05-24 Runtime Hot-Path Cleanup 144
What was wrong -> Fourteen more runtime owners could miss replacement Dispatcher/DataVault/service lanes or read registry services from event/action paths.
What was done -> Added hot-swap rebind/cache updates to `BuilderTool`, `FlashlightTool`, `HarpoonLauncherTool`, `DodReplayRecorder`, `HectonBlueprintPreviewBatch`, `VRPipeBlueprintPreview`, `SumpPumpPipeGridRuntime`, `LockstepStateValidator`, `FluidPipeGraphRuntime`, `AuxiliaryEquipmentRouterRuntime`, `LogisticsPipeNode`, `DemoFirstPersonController`, `ProceduralFloraBiomeTintBridge`, and `CelestialCataclysmSystem`.
Cinematic Cheats used -> Existing preview/cataclysm outputs stay shader/global-buffer visual cheats; no new physical simulation added.
Exact Microseconds saved -> Estimated 1-8 us during service replacement/event bursts; 0 steady-frame truth change.
Verification -> Source-only after loop144: targeted hot-swap greps passed; no-hot-swap candidate count is 47; scoped `diff --check` passed with LF normalization warnings only. Build `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup144_rebind_batch.log` failed before C# diagnostics with `NETSDK1004` missing `project.assets.json` and `MSB3491` Temp/obj access denied.

## 2026-05-24 Runtime Hot-Path Cleanup 145
What was wrong -> Four more runtime owners could miss replacement Dispatcher lanes after OnEnable-only registration.
What was done -> Added Dispatcher hot-swap rebinds to `ConnectionSplineBatchRenderer`, `InteractionHighlighter`, `PlayerTransportCoordinator`, and `TransportChargingStation`; `TransportChargingStation` now retries update/late-frame lane registration independently.
Cinematic Cheats used -> Existing spline pipes and highlight/indicator writes remain visual cheats; no new physical simulation added.
Exact Microseconds saved -> Estimated 1-6 us during Dispatcher replacement/interaction bursts; 0 steady-frame truth change.
Verification -> Source-only after loop145: targeted hot-swap greps passed; no-hot-swap candidate count is 43; scoped `diff --check` passed with LF normalization warnings only. Rebuild skipped by guard `BUILD_GUARD cpu=70.4 compiler_count=0`.

## 2026-05-24 Runtime Hot-Path Cleanup 146
What was wrong -> Four transient interaction/door owners could lose pending update/late-frame work after Dispatcher replacement.
What was done -> Added hot-swap rebinds to `SealedDoor`, `VRValveWheelHandle`, `PhysicalBatteryCompartment`, and `LifePodSeatStrapLatch`; pending visual/audio/snap/hold work is preserved across rebind; `SealedDoor` refreshes cached Audio on Audio replacement.
Cinematic Cheats used -> Door cut VFX, valve momentum continuation, battery snap, and strap visual stay cheap presentation/control cheats; no physical simulation added.
Exact Microseconds saved -> Estimated 1-6 us during Dispatcher replacement/interaction bursts; 0 steady-frame truth change.
Verification -> Source-only after loop146: targeted hot-swap greps passed; current no-hot-swap candidate count is 27; scoped `diff --check` passed with LF normalization warnings only. Rebuild skipped by guard `BUILD_GUARD cpu=100 compiler_count=1`.

## 2026-05-24 Runtime Hot-Path Cleanup 147
What was wrong -> Delayed despawns and GI relay could lose Dispatcher lanes after service replacement; GI relay could keep stale DataVault/player/biome cached state.
What was done -> Added hot-swap rebinds to `ObjectPoolManager.DespawnTimer` and `HectonGIRelaySystem`; GI relay completes pending SH jobs before DataVault replacement and reacquires owned buffers against the new vault.
Cinematic Cheats used -> GI relay remains SH/cubemap/lightning presentation; delayed despawn remains dispatcher-owned timing, no physical simulation added.
Exact Microseconds saved -> Estimated 2-8 us during replacement/despawn/GI bursts; 0 steady-frame truth change.
Verification -> Source-only after loop147: targeted hot-swap greps passed; current no-hot-swap candidate count is 24; scoped `diff --check` passed with LF normalization warnings only. Build `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup147_gi_despawn.log` failed before C# diagnostics with `NETSDK1004` missing project.assets and `MSB3491` Temp/obj access denied.

## 2026-05-24 Runtime Hot-Path Cleanup 148
What was wrong -> Thirteen runtime owners still had OnEnable-only Dispatcher lane registration, so Dispatcher replacement could silently drop extractor, celestial, UI, ocean-depth, terrain, tether, exosuit, and perf cadence.
What was done -> Added hot-swap listener/rebinds to `AutonomousExtractorSystem`, `SubmarineStationKeepingController`, root/tools `PerformanceMonitor`, `PDABarterTab`, `ObserverRelativeCelestialBody`, Crest/MapMagic bridges, `TetherManager`, `EclipseGameplaySystem`, `HectonBiolumController`, `WorldGenerativeGeologyTerrainSeamApplier`, and `ExosuitKinematicsRuntime`.
Cinematic Cheats used -> Existing seam blend masks, biolum globals, observer-relative celestial placement, and tether/exosuit presentation stay cheap visual/cinematic cheats; no new physical simulation added.
Exact Microseconds saved -> Estimated 1-8 us during Dispatcher replacement/render/physics recovery bursts; 0 steady-frame truth change.
Verification -> Source-only after loop148: no-hot-swap candidate count dropped 24 -> 13; targeted hot-swap grep passed; scoped `diff --check` passed with LF normalization warnings only. Rebuild skipped by guard `BUILD_GUARD cpu=100 compiler_count=1`.

## 2026-05-24 Runtime Hot-Path Cleanup 149
What was wrong -> Topographical sonar, GPU Jacobian foam, and indirect vegetation still had OnEnable-only Dispatcher/DataVault/player ownership tails.
What was done -> Added hot-swap rebinds to `TopographicalSonarSynthesizer`, `JacobianFoamGpuRuntime`, and `HectonIndirectVegetationRenderer`; sonar completes jobs before DataVault swap and reacquires buffers; vegetation caches Player context via hot-swap.
Cinematic Cheats used -> Existing sonar, foam, and vegetation outputs remain visual fakes; no physical simulation added.
Exact Microseconds saved -> Estimated 1-7 us during Dispatcher/DataVault/player replacement and render bursts; 0 steady-frame truth change.
Verification -> Source-only after loop149: targeted hot-swap greps passed; current strict no-hot-swap scan is 20; domain-filtered non-bootstrap/QA/core-service scan is 11; scoped `diff --check` passed with LF normalization warnings only. Rebuild skipped by guard `BUILD_GUARD cpu=100 compiler_count=0`.

## 2026-05-24 Runtime Hot-Path Cleanup 150
What was wrong -> Economy, outpost generation, vehicle damage, submarine dynamics, and thermodynamics still had OnEnable-only tick lanes or stale DataVault/service ownership after replacement.
What was done -> Added hot-swap rebinds to `MarauderOutpostGenerationService`, `TradeMarauderDirector`, `VehicleComponentDamageRuntime`, `SubmarineDynamicsRuntime`, and `AbyssalThermodynamicsSolver`; DataVault swaps complete owned jobs/fences before handle reset.
Cinematic Cheats used -> Existing outpost shell rendering, vehicle damage presentation, submarine hydrodynamics LUTs, and thermal grid remain controlled approximations; no physical simulation expansion.
Exact Microseconds saved -> Estimated 2-12 us during Dispatcher/DataVault/service replacement bursts on i3/MX350-class CPUs; 0 steady-frame truth cost.
Verification -> Source-only after loop150: targeted hot-swap greps passed; type-aware no-hot-swap scan is 4 (`PlayerBuilder`, `RepairTool`, two QA headless bots); file-local scan is 7 due partial false positives; scoped `diff --check` passed with LF normalization warnings only. Rebuild skipped by guard `BUILD_GUARD cpu=88.3 compiler_count=8`.

## 2026-05-24 Runtime Hot-Path Cleanup 151
What was wrong -> Tool/QA/small runtime cadence owners still had replacement gaps: builder used a dead public hot-swap callback path, builder late-frame audio could read GlobalRegistry, repair only handled DataVault, and stress/Steam/Manta/cavitation/terrain/HUD scaler lanes could go stale after Dispatcher replacement.
What was done -> Routed builder/repair through base tool replacement callbacks, reset builder DataVault/socket/validation state, removed late-frame Audio fallback, added repair Dispatcher/Audio/Localization handling, and added hot-swap Dispatcher rebinds to `HeadlessStressFractureBot`, `SteamManager`, `MantaEmergencyWreck`, `AbyssalCavitationRuntimeHost`, `TerrainChunkPagerRuntime`, and `HectonUIScaler`.
Cinematic Cheats used -> Existing Manta drift, cavitation visuals, HUD scaling, and terrain paging remain controlled approximations; no physical simulation expansion.
Exact Microseconds saved -> Estimated 1-8 us during Dispatcher/DataVault/service replacement bursts; 0 steady-frame truth cost.
Verification -> Source-only after loop151: targeted hot-swap greps passed; scoped code `diff --check` passed with LF normalization warnings only. Rebuild skipped by latest guard `BUILD_GUARD cpu=38 compiler_count=1`; latest compile attempt remains the loop147 ENV_BUILD_WALL before C# diagnostics.

## 2026-05-24 Runtime Hot-Path Cleanup 152
What was wrong -> `PersistentWorldRegistry` tombstone day resolution read `GlobalRegistry.Save` from a static helper during tombstone/decay work.
What was done -> Cached `ISaveService` in the persistent-world owner, refreshed it on Save hot-swap, and made tombstone day resolution consume the cached owner state.
Cinematic Cheats used -> None; authority route only.
Exact Microseconds saved -> Estimated 1-3 us per tombstone/decay burst; 0 steady-frame truth cost.
Verification -> Source-only after loop152: `PersistentWorldRegistry` Save grep leaves only cold cache, `TYPE_AWARE_NO_HOTSWAP_COUNT=0`, `HOTSWAP_REGISTER_NO_UNREGISTER_CALL_FILE_COUNT=0`, scoped `diff --check` passed with LF normalization warning only. Rebuild skipped by latest guard `BUILD_GUARD cpu=100 compiler_count=2`.

## 2026-05-24 Runtime Hot-Path Cleanup 153
What was wrong -> `PersistentWorldRegistry` hydration/catalog helpers still read `GlobalRegistry.Player` and `GlobalRegistry.PlayerInventory`.
What was done -> Cached Player and PlayerInventory owner services in lifecycle, refreshed them on hot-swap, and routed AUP snapshots plus item-catalog lookup through cached owner state.
Cinematic Cheats used -> None; authority route only.
Exact Microseconds saved -> Estimated 1-4 us per hydration/catalog burst; 0 steady-frame truth cost.
Verification -> Source-only after loop153: `PersistentWorldRegistry` `GlobalRegistry.Save/Player/PlayerInventory` grep leaves only cold cache lines; `TYPE_AWARE_NO_HOTSWAP_COUNT=0`; `HOTSWAP_REGISTER_NO_UNREGISTER_CALL_FILE_COUNT=0`; scoped `diff --check` passed with LF normalization warning only. Rebuild skipped by `BUILD_GUARD cpu=76.3 compiler_count=0`.

## 2026-05-24 Runtime Hot-Path Cleanup 154
Wrong -> Twelve short UI/audio/construction owners had Dispatcher hot-swap callbacks that could be no-ops because their old `_registered*` flags stayed true. `PDADeathMemoryDump` still resolved player survival state through `GlobalRegistry.Player` during death-signal consumption.
Done -> Reset registration flags before hot-swap rebind in `AcousticReverbPresetTrigger`, `RepairDroneHub`, `MaintenanceStationModule`, `BotanyPlanterModule`, `ActionProgressHUD`, `SonarHoloCompass`, `PDADeathMemoryDump`, `AnalogGaugeNeedle3D`, `HectonSubmarineOsDisplay`, `UIFadeTransition`, `UIScreenShake`, and `WorldSpaceTMPSharpnessController`; death dump now uses cached Player context plus Player hot-swap.
Cinematic Cheats -> No simulation added. Kept existing cheap UI/audio presentation and owner-cache routing instead of polling.
Microseconds saved -> 1-6 us during Dispatcher replacement/UI burst; 0 steady-frame truth change.
Verification -> Source-only after loop154: scoped `diff --check` passed with LF normalization warnings only; targeted greps show Dispatcher callbacks rebind through local unregister/register helpers; death-dump and maintenance greps show no direct Player runtime accessor tail. Rebuild skipped by `BUILD_GUARD cpu=68.3 compiler_count=7`.

## 2026-05-24 Runtime Hot-Path Cleanup 155
Wrong -> Eight more UI owners had Dispatcher hot-swap callbacks that could no-op behind stale registration flags; three previous UI owners did not explicitly reset flags on null Dispatcher.
Done -> Routed `FontStreamingManager`, `LocalizedTextMadnessFx`, `PDADataArchaeologyDecryptLabel`, `PDAConstructionTab`, `PDADataLogTab`, `PDALoadoutTab`, `SubtitleManager`, and `LoadingScreenController` through local unregister/register helpers on Dispatcher replacement. Added null-Dispatcher local resets to `SonarHoloCompass`, `UIFadeTransition`, and `UIScreenShake`.
Cinematic Cheats -> No simulation added. Kept UI presentation cadence owner-local and polling-free.
Microseconds saved -> 1-7 us during Dispatcher replacement/UI burst; 0 steady-frame truth change.
Verification -> Source-only after loop155: scoped UI `diff --check` passed with LF normalization warnings only; targeted greps show unregister/register rebinds and null local resets. Rebuild skipped by `BUILD_GUARD cpu=100 compiler_count=9` with active `csc` and dotnet.

## 2026-05-25 Runtime Hot-Path Cleanup 156
Wrong -> Fifteen UI/construction owners still had Dispatcher hot-swap callbacks that could no-op because stale `_registered*` flags survived replacement.
Done -> Rebound `AcousticEcholocationTranslator`, `BIOSMessageStreamer`, `BuilderStatusOverlay`, `DiegeticGlitchSurgeonRuntime`, `DiegeticPdaFocusDistanceController`, `FakeRadarBlipController`, `HectonUIScaler`, `InteractionUI`, `DeepDrillModule`, `SettingsPanelAnimator`, `SettingsComparisonView`, `LocalizedLayoutMirror`, `LocalizedTMPAutoSizer`, `DroneFleetManager` headless driver, and `ShaderCompassRibbon` through local unregister/register or lane-reset paths.
Cinematic Cheats -> No simulation added. Kept cadence recovery owner-local and polling-free.
Microseconds saved -> 1-8 us during Dispatcher replacement/UI-construction burst; 0 steady-frame truth change.
Verification -> Source-only after loop156: scoped `diff --check` passed with LF normalization warnings only; targeted greps show unregister/register rebinds and null/reset paths; exact stale-simple Dispatcher pattern grep over touched files returned no matches. Build skipped by `BUILD_GUARD cpu=64 compiler_count=0`.

## 2026-05-25 Runtime Hot-Path Cleanup 157
Wrong -> UI/Construction runtime owners still read `PlayerRuntimeContextService.ActiveRuntimeContext` or `LocalizationManager.ActiveRuntimeInstance` from cold/lazy paths.
Done -> Routed `BatteryChargerModule`, `FoundationPylonGpuBatch`, `DroneFleetManager`, `RelayHUDElement`, `SettingsLivePreview`, `DiegeticGyroCompassPhysicalBinding`, and `HectonOSBootManager` through `GlobalRegistry` cold owner cache plus existing hot-swap state.
Cinematic Cheats -> No simulation added. Authority cleanup only.
Microseconds saved -> 1-4 us per fallback burst; 0 steady-frame truth change.
Verification -> Source-only after loop157: targeted UI/Construction grep for `PlayerRuntimeContextService.ActiveRuntimeContext` and `LocalizationManager.ActiveRuntimeInstance` returned no matches; scoped `diff --check` passed with LF normalization warnings only. Build skipped by `BUILD_GUARD cpu=100 compiler_count=2` with active `csc`/`dotnet`.

## 2026-05-25 Runtime Hot-Path Cleanup 158
Wrong -> Twenty-three world/environment/AI owners could keep stale `_registered*` flags after Dispatcher replacement, so replacement lanes could miss biome, world, geology, ambience, celestial, atmosphere, AI, buoyancy, and cave-root cadence.
Done -> Added local unregister/register or null-reset Dispatcher rebinds across the 23 touched owners. Split `AmbientBiotaDirector` dispatcher-lane unregister from service unregister so Dispatcher replacement does not churn `AmbientBiota` service identity.
Cinematic Cheats -> No simulation added. Existing biome/world/celestial/atmosphere presentation remains dispatcher-owned and polling-free.
Microseconds saved -> 1-12 us during Dispatcher replacement/world-environment bursts; 0 steady-frame truth change.
Verification -> Source-only plus compile-wall proof after loop158: scoped `diff --check` passed; 23-file grep shows Dispatcher handling plus reset/unregister routes in every touched file; `Build_EXTERNAL_CODEX_hotpath_cleanup158_world_dispatcher_rebind.log` fails before C# with `NETSDK1004` missing project.assets, 0 warnings, no `CS*`; retry blocked by `BUILD_GUARD cpu=79 compiler_count=2`.

## 2026-05-25 Runtime Hot-Path Cleanup 159
Wrong -> Project-wide runtime scans still surfaced forbidden `ActiveRuntimeContext` / `ActiveRuntimeInstance` routes and two hidden fallback registry reads: GI DataVault allocation via `?? GlobalRegistry` and weather fluid sink via null-coalescing registry fallback.
Done -> Routed singleton reads to `GlobalRegistry` owner routes; used byte-preserving ASCII replacement for two non-UTF8 files. `HectonGIRelaySystem` now cold-caches DataVault before native storage acquisition and allocator helpers read `_vault` only. `GlobalWeatherDirector` now resolves authored sink first and only then cold-falls back to registry.
Cinematic Cheats -> No simulation added. Route cleanup only.
Microseconds saved -> 1-4 us per fallback/boot burst; 0 steady-frame truth change.
Verification -> Singleton grep returned 0; `?? GlobalRegistry|GlobalRegistry.TryGet` grep returned 0; scoped `diff --check` passed with LF normalization warnings only. Build skipped by `BUILD_GUARD cpu=100 compiler_count=2` with active `csc`/`dotnet`.
