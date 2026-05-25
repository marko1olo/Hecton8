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
