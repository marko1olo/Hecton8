# LOG_13ATMA

## 2026-05-27 - Atmosphere / Celestial / Sky Beauty Audit

Scope: Direct user assignment for atmosphere, sky beauty, celestial bodies, orbital cycles, sunrises/sunsets/eclipses/cloud/weather visuals. `Docs/Tasks/CURRENT_BATCH.md` has no `<AGENT_PROMPT id="13ATMA">`; neighboring prompts were rejected.

What was wrong:
- `[ExecuteAlways]` editor preview was effectively dead in `ObserverRelativeCelestialBody` and `SkySystemFollowCamera` because editor guards returned on `!Application.isPlaying`.
- `ObserverRelativeCelestialBody` polled `GlobalRegistry.Player` while resolving the observer camera and resolved atmosphere from `ResolveTimeSeconds()`.
- `ObserverRelativeCelestialBody.CurrentDirection` could mutate cached parent/observer references while being read.
- `GlobalWeatherDirector` used `BiomeMatrixDirector.ActiveRuntimeInstance` in the weather tick/LUT route.
- Existing tests covered private/manual paths but not the actual edit-mode `OnEnable` regressions or getter purity.

What was done:
- `Assets/_Project/Scripts/ObserverRelativeCelestialBody.cs`
  - Restored edit-mode `OnEnable`, `OnValidate`, and editor update execution while keeping compile guard.
  - Added cold cached `IPlayerRuntimeContext`.
  - Added cold/hot-swap atmosphere runtime binding.
  - Removed registry resolution from `ResolveTimeSeconds()`.
  - Split placement solve into caching and non-caching routes so `CurrentDirection` remains a pure read.
- `Assets/_Project/Scripts/SkySystemFollowCamera.cs`
  - Restored edit-mode `OnEnable` and `EditorTick` execution.
  - Added cached player camera route before fallback player/camera scans.
  - Refreshed player context on `GlobalRegistryServiceSlot.Player`.
- `Assets/_Project/Scripts/Environment/GlobalWeatherDirector.cs`
  - Cached `BiomeMatrixDirector` through `GlobalRegistry.BiomeMatrix`.
  - Refreshed it through `GlobalRegistryServiceSlot.BiomeMatrixRuntime`.
  - Removed hot `BiomeMatrixDirector.ActiveRuntimeInstance` read from weather depth resolution.
- `Assets/_Project/Tests/Editor/HectonCelestialEngineEditTests.cs`
  - Added edit-mode `SkySystemFollowCamera` OnEnable follow test.
  - Added edit-mode celestial parent-local capture OnEnable test.
  - Added `CurrentDirection` purity regression test.

Cinematic cheats used:
- Preserved observer-relative celestial placement instead of physical orbit scale.
- Kept orbit motion as deterministic apparent sky math, not N-body simulation.
- Kept weather/depth coupling as cached presentation read, not scene search or physical atmosphere solve.
- Chose editor proof and cached routes so runtime budget can buy denser fog/shafts/celestial presentation instead of dependency lookup.

Exact microseconds saved:
- Observer camera registry polling removed: estimated 2-8 us/frame in missing-observer fallback scenes on i3/MX350.
- `CurrentDirection` hidden `TryGetComponent` side effect removed: estimated 1-5 us per external sampling burst with orbiting bodies.
- Weather biome singleton poll removed: estimated 1-3 us per weather update/LUT refresh route.
- Edit-mode preview fix: 0 runtime us; prevents bad authored sky placement from becoming runtime correction work.
- Total runtime estimate for affected fallback paths: 4-16 us per frame/sampling burst, depending on scene reference health. No profiler microtrace was produced in this pass.

Verification:
- `git diff --check` passed on touched source/test files.
- Targeted `rg` scans confirmed:
  - no remaining `EditorApplication.isCompiling || !Application.isPlaying` guard in the touched preview paths,
  - no `BiomeMatrixDirector.ActiveRuntimeInstance` in `GlobalWeatherDirector`,
  - no direct `GlobalRegistry.Player` in the hot celestial observer resolve path.
- `dotnet build Hecton8.slnx --no-restore` was launched only after no build processes were active and CPU dropped below threshold. It failed after ~511.5 s with warning-heavy Unity/package output; actual error lines were not isolated before transcript truncation.
- After build-server shutdown, CPU samples returned to 57-99 percent, so an errors-only rerun was not launched under the local build rules.

Residual risk:
- Full compile status is not green. Current code changes have static proof, but workspace-level compile failure still needs an errors-only rerun when CPU/build gate is clear.
- Dirty shared atmosphere/celestial files modified by other agents were inspected but not refactored to avoid cross-agent overwrite.

## 2026-05-27 - Firmament Quality Contract Continuation

What was wrong:
- `HectonCelestialEngine` firmament cubemap bake used hard MX350/mid/high VRAM buckets. That is a binary quality switch in the sky domain.
- The old firmament resolution resolver also published telemetry and mutated `_firmamentResolutionWarningPublished`, so a `Resolve*`-named path was not pure.

What was done:
- `Assets/_Project/Scripts/HectonCelestialEngine.cs`
  - Replaced binary VRAM caps with continuous `GlobalQualityWeight` and continuous graphics-memory budget.
  - Added a power-of-two floor snap so cubemap allocation never exceeds the resolved budget.
  - Moved resolution clamp telemetry into `PublishFirmamentResolutionClampWarningIfNeeded()`.
  - Left existing dirty non-13ATMA changes in the same file untouched.
- `Assets/_Project/Tests/Editor/HectonCelestialEngineEditTests.cs`
  - Added regression probes for continuous firmament memory budget.
  - Added power-of-two floor proof for non-overbudget cubemap resolution.

Cinematic cheats used:
- Kept firmament as a precomputed visual bake and budgeted cubemap, not physical star simulation.
- Preserved apparent sky/orbit presentation while scaling fidelity continuously from survival hardware to visual overkill.

Exact microseconds saved:
- Runtime hot path: 0 us direct, because this path is startup/bake/presentation allocation.
- Startup/bake protection: avoids accidental 4K/8K cubemap dispatch on MX350-class hardware; expected savings are multiple milliseconds and tens to hundreds of MB VRAM depending on requested resolution. No profiler capture was produced.
- Resolve purity: removes hidden telemetry/mutation from compute path; deterministic reasoning gain, not a measured frame-time delta.

Verification:
- `git diff --check` passed on `HectonCelestialEngine.cs`, `HectonCelestialEngineEditTests.cs`, `Status_13ATMA.md`, and `Rationale_13ATMA.md`.
- Source/test `rg` found no remaining `FirmamentMx350`, `FirmamentMidVram`, or `ResolveFirmamentCubemapResolution` tokens.
- `rg` confirmed `ComputeFirmamentCubemapResolution`, `PublishFirmamentResolutionClampWarningIfNeeded`, `ResolveFirmamentMemoryBudget01`, and `ResolvePowerOfTwoFloor`.
- Build gate was legal before compile: no `dotnet/csc/MSBuild/VBCSCompiler`, CPU ~37.6 percent.
- `dotnet build Hecton8.slnx --no-restore -v:q /clp:ErrorsOnly /nr:false` failed after ~311.7 s with 364 errors. Visible blockers are third-party/workspace dependency failures: Astar missing `Ionic/ClipperLib/Poly2Tri`, Candice missing `Mono.Data.Sqlite`, MapMagic duplicate editor symbols against `Library/ScriptAssemblies`, MeshBaker missing core symbols, Technie old `MeshCollider` API, NiceVibrations/ShaderGraph editor importer references.
- `dotnet build-server shutdown` completed after `VBCSCompiler` remained.

Residual risk:
- Solution compile is still red due external package/workspace failures. No Unity Console, Play Mode, profiler, GCMonitor, player build, or visual capture proof was produced.
- `HectonCelestialEngine.cs` contains unrelated dirty hunks from other work in the same file; this pass did not revert or normalize them.

## 2026-05-27 - Surface Weather Editor Contract Continuation

What was wrong:
- `HectonSurfaceWeatherDirector.Reset()` and `OnValidate()` were blocked by `!Application.isPlaying`. That made edit-mode authored defaults, child `SurfaceWeatherVfxRig` discovery, and serialized depth range correction unreachable.
- Mutating dependency binders used `Resolve*` names, which conflicts with the project rule that read/resolve accessors are pure.
- Ocean visual apply/restore paths could refresh provider binding through `ResolveOceanKinematics()`, creating hidden dependency work from late-frame presentation code.

What was done:
- `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs`
  - Removed the play-mode rejection from editor `Reset()` and `OnValidate()` while preserving the compile guard.
  - Renamed mutating dependency routes to `RefreshPlayerMovementReference()`, `RefreshOceanKinematicsBinding()`, `RefreshSceneOwnedReferences()`, and `RefreshOwnedWeatherVfxRig()`.
  - Added `ReadCachedOceanKinematics()` and made ocean default/cache/apply/restore paths use the cached provider only.
  - Kept existing dirty dependency-cache work in the same file intact; no unrelated rollback was made.
- `Assets/_Project/Tests/Editor/HectonCelestialEngineEditTests.cs`
  - Added `SurfaceWeatherDirectorResetBindsOwnedVfxRigInEditMode`.
  - Added `SurfaceWeatherDirectorOnValidateClampsSuppressionDepthInEditMode`.
  - Added an explicit `LogAssert.Expect` for the authored-rig validation error emitted by `SurfaceWeatherVfxRig` in the test fixture.

Cinematic cheats used:
- Preserved the surface weather director as a presentation binder for authored storm/rain/ocean signals, not a physical atmospheric solver.
- Kept ocean state handoff as cached visual state application, not service polling or scene search.
- Used editor-time authoring validation to prevent runtime correction work.

Exact microseconds saved:
- Runtime hot path: estimated 1-4 us/frame variance reduction in scenes where late-frame ocean visual apply/restore would otherwise refresh provider binding.
- Editor validation: 0 runtime us; prevents invalid weather/VFX rig setup from shipping into runtime.
- GC impact: no new runtime allocations by static inspection.

Verification:
- `rg` found no old `EditorApplication.isCompiling || !Application.isPlaying` guard or old `ResolvePlayerMovementReference`, `ResolveOceanKinematics`, `ResolveSceneOwnedReferences`, `ResolveOwnedWeatherVfxRig` tokens in `HectonSurfaceWeatherDirector.cs`.
- `rg` confirmed the new `Refresh*` and `ReadCachedOceanKinematics()` routes.
- `git diff --check` passed on `HectonSurfaceWeatherDirector.cs` and `HectonCelestialEngineEditTests.cs`.
- Build gate was legal before compile: no `dotnet/csc/MSBuild/VBCSCompiler`, CPU ~44.1 percent.
- `dotnet build Hecton8.slnx --no-restore -v:q /clp:ErrorsOnly /nr:false` failed after ~273.5 s with 364 errors. The visible blockers remain external package/workspace failures: Astar missing `Ionic/ClipperLib/Poly2Tri`, MapMagic duplicate editor symbols against `Library/ScriptAssemblies`, MeshBaker missing core symbols, Technie removed `MeshCollider` API, NiceVibrations/ShaderGraph editor importer references.
- `dotnet build-server shutdown` completed after `VBCSCompiler` remained.

Residual risk:
- Solution compile remains red for external packages. No Unity Console test run, play-mode runtime pass, profiler capture, or visual capture was possible from this compile state.
- `HectonSurfaceWeatherDirector.cs` was already dirty with dependency-cache edits before this continuation; this pass preserved and built on them instead of reverting them.

## 2026-05-27 - Seismic AUP Precision Guard Continuation

What was wrong:
- `SeismicWaveMath.CalculateSeismicDisplacement()` subtracted receiver/epicenter in `double3`, but cast the result to `float3` before proving the distance was inside the active seismic wavefront. A finite AUP separation such as `1e40` becomes float infinity; old code then substituted distance `1f`, creating a false local quake displacement.
- The seismic event job had the same failure mode: non-finite `float3` distance became `1f`, so far/overflowed epicenters could create local camera jitter, turbidity, and presentation falloff.
- 13KRA-owned volumetric/noir lighting dump routes were visible during audit, but their status/rationale/log files document that ownership. They were not edited by 13ATMA.

What was done:
- `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs`
  - Added shared `SeismicDirectorConstants.MinSeismicDistanceSq` and `MaxSeismicEvaluationDistanceMeters`.
  - Moved `GlobalQualityWeight`, wave radii, and wave band calculation ahead of float conversion.
  - Added double-space influence gating before `float3` conversion in `SeismicWaveMath`.
  - Updated the seismic job path so far/non-finite/out-of-wave deltas produce zero local falloff instead of fake 1 m proximity.
- `Assets/_Project/Tests/Editor/HectonCelestialEngineEditTests.cs`
  - Added `SeismicWaveMathRejectsFarFiniteAupBeforeFloatCast`.
  - Added `SeismicWaveMathKeepsNearWaveFiniteAfterAupSubtract`.

Cinematic cheats used:
- Rejected physical seismic simulation. The fix is a deterministic presentation fake: only active wavefront proximity buys visual shake.
- Far or impossible AUP distances are treated as no local visual influence, preserving frame budget and authorial control.

Exact microseconds saved:
- Estimated 0.5-2 us per impossible far seismic event consumer by skipping arrival/falloff/noise and downstream shake accumulation.
- GC impact: 0 B new runtime allocations by static inspection.
- Correctness gain is larger than measured time: false local quake/sky/weather shake from far AUP overflow is removed.

Verification:
- `rg` confirmed `MinSeismicDistanceSq`, `MaxSeismicEvaluationDistanceMeters`, the two seismic tests, and removal of the old `distSqRaw` fallback token.
- `git diff --check` passed on `HectonSeismicTideDirector.cs` and `HectonCelestialEngineEditTests.cs`.
- Build gate was legal before compile: no `dotnet/csc/MSBuild/VBCSCompiler`, CPU ~7 percent.
- `dotnet build Hecton8.slnx --no-restore -v:q /clp:ErrorsOnly /nr:false` failed after ~421.5 s with 364 errors and 3126 warnings. Visible blockers remain external package/workspace failures: MapMagic duplicate/missing types, MeshBaker missing core symbols, Technie removed `MeshCollider` API, NiceVibrations/ShaderGraph editor importer references. No visible errors pointed at `HectonSeismicTideDirector.cs` or `HectonCelestialEngineEditTests.cs`.
- `dotnet build-server shutdown` completed.

Residual risk:
- Solution compile remains red for external packages. No Unity Console test run, play-mode runtime pass, profiler capture, or visual capture was possible from this compile state.
- Runtime microsecond estimates need profiler confirmation after workspace dependency repair.

## 2026-05-27 - Editor Runtime Boundary And Atmosphere Preview Continuation

What was wrong:
- `ShinobuStormPropagationRuntime.OnEnable()` could claim the runtime singleton/static owner path in edit mode. That is a runtime weather propagation owner, not an editor preview component.
- `HectonAtmosphereManager.OnEnable()`, `EditorTick()`, and `OnValidate()` were still gated by `!Application.isPlaying`, so the edit-mode preview branch was unreachable. Scene View sun/sky-cycle authoring could not rely on live preview.

What was done:
- `Assets/_Project/Scripts/Atmosphere/StormPropagation/ShinobuStormPropagationRuntime.cs`
  - Added an edit-mode early return before runtime claim, registry listener registration, origin-shift registration, and DataVault setup.
- `Assets/_Project/Scripts/HectonAtmosphereManager.cs`
  - Fixed editor gates so `OnEnable` and `EditorTick` reject compiling, not edit mode.
  - Fixed `OnValidate` to run in edit mode and reject play mode.
  - Kept runtime registration/service ownership inside the existing `Application.isPlaying` branch.
- `Assets/_Project/Tests/Editor/StormPropagationRuntimeEditTests.cs`
  - Added `StormPropagationRuntimeDoesNotClaimRuntimeInEditMode`.
- `Assets/_Project/Tests/Editor/AtmosphereManagerEditorPreviewTests.cs`
  - Added `AtmosphereManagerOnEnableMarksEditorPreviewDirtyInEditMode`.
  - Added `AtmosphereManagerOnValidateRunsInEditMode`.

Cinematic cheats used:
- Kept storm propagation as one deterministic runtime scalar/fake route for flow, fog, audio, and biolum cues.
- Restored editor-time sky/sun preview instead of adding runtime sky correction logic.

Exact microseconds saved:
- Runtime hot path: 0 us direct; no runtime loop was changed.
- Editor/runtime boundary: prevents cold registry/listener/vault setup from edit-mode activation.
- Indirect runtime saving: removes play-mode-only sky correction passes caused by invalid authored atmosphere state. No profiler measurement was produced.

Verification:
- `git diff --check` passed on `ShinobuStormPropagationRuntime.cs`, `HectonAtmosphereManager.cs`, and the new editor test files.
- `rg` confirmed the new storm runtime edit-mode regression and atmosphere preview regression tests.
- Build was not launched. Active `dotnet` PID 33480 was already running and CPU sampled up to 100 percent. Project rules forbid a second compile under that load.

Residual risk:
- No Unity Console test execution, solution compile, play-mode pass, profiler capture, or visual capture was possible while another compile job owned the machine.
- The repository remains globally red from the previously recorded external MapMagic/MeshBaker/Technie/NiceVibrations/ShaderGraph failure profile.

## 2026-05-27 - Surface Thunder Authoring Contract Continuation

What was wrong:
- `SurfaceWeatherProfile` exposed storm timing controls, but runtime ignored them.
- `SurfaceWeatherMathJob.TriggerLightning()` used a hard-coded 0.1 s lightning flash and raw `thunderDistance / SpeedOfSoundMetersPerSecond`.
- The direct `HectonSurfaceWeatherDirector.TriggerLightning()` fallback had the same hard-coded flash and raw-delay path.
- Result: heavy rain/electrical storm profiles could not author slow cinematic thunder roll-in or different flash durations.

What was done:
- `Assets/_Project/Scripts/Atmosphere/SurfaceWeatherMath.cs`
  - Added shared `SurfaceThunderMath`.
  - Added authored flash duration clamp: invalid or non-positive falls back to 0.1 s; valid values clamp to 0.01-0.5 s.
  - Added authored thunder delay solve: scaled distance / sound speed clamped to `thunderDelayMin/Max`.
  - Routed `SurfaceWeatherMathJob.TriggerLightning()` through the shared helper.
- `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs`
  - Routed direct fallback lightning/queued thunder through the same helper.
- `Assets/_Project/Tests/Editor/SurfaceWeatherMathEditTests.cs`
  - Added `ThunderDelayUsesAuthoredScaleAndClamp`.
  - Added `LightningFlashDurationUsesAuthoredDurationWithSafeFallback`.

Cinematic cheats used:
- Kept thunder as deterministic scalar presentation timing, not physical acoustic propagation.
- Used authored scale/clamps to buy storm mood and player belief without extra simulation or allocations.
- Kept both job and direct fallback branches on one formula so profile behavior does not drift by execution path.

Exact microseconds saved:
- CPU saved: effectively 0 us; this is a correctness/authoring fix, not a speed fix.
- Runtime delta: estimated <0.1 us per lightning event on i3/MX350.
- GC impact: 0 B new runtime allocations by static inspection.
- Visual value: authored profiles now control storm pacing, so high/ultra can spend existing VFX budget on longer roll-in and stronger lightning composition without new truth routes.

Verification:
- `git diff --check` passed on `SurfaceWeatherMath.cs`, `HectonSurfaceWeatherDirector.cs`, `SurfaceWeatherMathEditTests.cs`, and the new `.meta`.
- `rg` confirmed no old `LightningFlashSeconds` token or direct `thunderDistance / SpeedOfSoundMetersPerSecond` route remains in the touched files.
- Build gate was legal before compile: no `dotnet/csc/MSBuild/VBCSCompiler`, CPU ~21.9 percent.
- `dotnet build Hecton8.slnx --no-restore -v:q /clp:ErrorsOnly /nr:false` failed after ~421.2 s with 3117 warnings and 364 errors. Visible blockers remain external package/workspace failures: MapMagic duplicate `CellExpose` and missing MapMagic namespaces, missing Odin attributes in `BuoyancyProfile`, missing `BufferID` members in world systems, duplicate vegetation member, NiceVibrations/ShaderGraph editor importer references, and Technie removed `MeshCollider` API.
- `dotnet build-server shutdown` completed after `VBCSCompiler` remained.

Residual risk:
- No Unity Console test run, play-mode storm pass, profiler capture, or audio/visual capture was produced yet.
- Full solution compile remains dependent on the previously recorded external package/workspace failure profile.

## 2026-05-27 - Weather Event Lane Cold Warmup Continuation

What was wrong:
- `WeatherEvents.TryRaiseSnapshotUpdated()` and `TryRaiseLightning()` call `EnsureInitialized()`.
- If no listener registered before the first producer publish, `WeatherEvents` could allocate persistent `NativeQueue` lanes from the first weather snapshot/lightning event path.
- Producer order is not a proof artifact. `GlobalWeatherDirector` and `HectonSurfaceWeatherDirector` own weather publishing, so they must warm the lane before hot publication.

What was done:
- `Assets/_Project/Scripts/Environment/WeatherEvents.cs`
  - Added `PrepareCold()` as an explicit cold initialization surface for the weather signal lane.
- `Assets/_Project/Scripts/Environment/GlobalWeatherDirector.cs`
  - Calls `WeatherEvents.PrepareCold()` during runtime state initialization before weather snapshots can be published.
- `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs`
  - Calls `WeatherEvents.PrepareCold()` during runtime state initialization before lightning can be published.
- `Assets/_Project/Tests/Editor/WeatherEventsEditTests.cs`
  - Added `PrepareColdCreatesNativeQueuesBeforeFirstPublish`, resetting static state before/after and asserting both native queues are created by warmup.

Cinematic cheats used:
- Kept weather broadcasts as small unmanaged snapshot/lightning payloads. No managed event bus, no physical storm propagation, no listener-order dependency.
- Warmup is cold; hot weather event publishing stays a fixed queue enqueue.

Exact microseconds saved:
- Steady-state CPU saved: 0 us; behavior after warmup is unchanged.
- First-event hitch avoided: estimated tens to hundreds of microseconds on i3/MX350 by moving two persistent `NativeQueue` allocations out of gameplay publication.
- GC impact: 0 B hot path by static inspection.

Verification:
- `git diff --check` passed on `WeatherEvents.cs`, `GlobalWeatherDirector.cs`, `HectonSurfaceWeatherDirector.cs`, `WeatherEventsEditTests.cs`, and the new `.meta`.
- `rg` confirmed `WeatherEvents.PrepareCold()` in both producers before their `TryRaise*` paths.
- Build was not launched after this loop. Active `dotnet` PID 9648, active `VBCSCompiler` PID 52544, and CPU sampled 100 percent; project rules forbid another compile in that state.

Residual risk:
- No Unity Console test run, play-mode weather broadcast stress, profiler capture, or player-build proof was produced yet.
- Full solution compile remains blocked by the external 364-error workspace profile recorded above.

## 2026-05-27 - Global Weather Editor Runtime Boundary Continuation

What was wrong:
- `GlobalWeatherDirector` registered `GlobalRegistry.Weather` from edit-mode lifecycle.
- The same edit-mode path initialized runtime weather state, published shader globals, raised weather snapshots, and could allocate the noir fog LUT resources.
- This component is not an editor preview tool. Weather service ownership belongs to runtime play mode.

What was done:
- `Assets/_Project/Scripts/Environment/GlobalWeatherDirector.cs`
  - Added `Application.isPlaying` gates to `Awake()` and `OnEnable()`.
  - Added edit-mode cleanup path for disable/destroy that only clears stale residue owned by the same instance.
  - Left runtime tick, slow tick, frost tick, late-frame publication, weather event warmup, and cached biome dependency route unchanged.
- `Assets/_Project/Tests/Editor/GlobalWeatherDirectorEditTests.cs`
  - Added `GlobalWeatherDirectorDoesNotRegisterOrInitializeRuntimeInEditMode`.
  - Test resets `GlobalRegistry._weather`, adds the component in edit mode, and asserts no weather-service claim, no runtime init, and no LUT texture allocation.

Cinematic cheats used:
- No physical weather simulation added.
- Kept weather presentation as runtime scalar/vector/LUT fake owned by one service route.
- Editor authoring no longer spends runtime visual-resource budget or publishes shader state.

Exact microseconds saved:
- Runtime steady-state saved: 0 us; gameplay code path is intentionally unchanged.
- Edit-mode/resource hitch avoided: one cold `Texture2D` allocation plus one `Color[]` LUT allocation and weather event publication during authoring.
- GC impact: runtime 0 B change by static inspection; editor allocation path removed for this component lifecycle.

Verification:
- `git diff --check` passed on `GlobalWeatherDirector.cs`, `GlobalWeatherDirectorEditTests.cs`, and the new `.meta`.
- `rg` confirmed `GlobalRegistry.RegisterWeatherService(this)` remains behind `Application.isPlaying` lifecycle and the new edit-mode regression exists.
- Build was not launched. Active `dotnet` PID 62864, active `VBCSCompiler` PID 6448, and CPU sampled ~68.5 percent; project rules forbid another compile in that state.

Residual risk:
- No Unity Console test run, play-mode weather service pass, profiler capture, or player-build proof was produced.
- Full solution compile remains blocked by the external workspace/package failure profile already recorded.

## 2026-05-27 - Surface Weather Editor Runtime Boundary Continuation

What was wrong:
- `HectonSurfaceWeatherDirector` did runtime cold-start work when added/enabled outside play mode.
- The edit-mode path could cache DataVault, read runtime registry services, register as an origin-shift listener, warm weather event queues, acquire runtime buffer handles, and run cold weather math.
- This is not required for authoring; `Reset` and `OnValidate` already own editor defaults and clamps.

What was done:
- `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs`
  - Kept fallback profile/editor default setup in `Awake`.
  - Added play-mode gates before DataVault, registry, origin listener, service registration, weather event warmup, and job seeding.
  - Added edit-mode stale-residue cleanup for disable/destroy without starting the runtime route.
- `Assets/_Project/Tests/Editor/SurfaceWeatherDirectorEditTests.cs`
  - Added `SurfaceWeatherDirectorDoesNotRegisterOrInitializeRuntimeInEditMode`.
  - Test asserts no `GlobalRegistry.SurfaceWeather`, no `HectonFloatingOrigin` listener registration, no `_runtimeStateInitialized`, and no `_dataVault` cache after edit-mode component creation.

Cinematic cheats used:
- No new weather physics.
- Preserved existing scalar/job weather fake for runtime only.
- Editor mode now remains authoring-only; runtime visual budget is spent only when gameplay starts.

Exact microseconds saved:
- Runtime steady-state saved: 0 us; runtime path is intentionally preserved.
- Edit-mode cold work avoided: DataVault handle resolution/acquisition path plus cold weather math seed and origin listener registration.
- Estimated avoided authoring hitch: tens to hundreds of microseconds on i3/MX350 depending on DataVault state.
- GC impact: no new runtime allocation path by static inspection.

Verification:
- `git diff --check` passed on `HectonSurfaceWeatherDirector.cs`, `SurfaceWeatherDirectorEditTests.cs`, and the new `.meta`.
- `rg` confirmed service/origin registration remains behind play-mode lifecycle and the edit-mode regression exists.
- Build was not launched. Active `dotnet` PID 62864 and active `VBCSCompiler` PID 6448 were present; CPU sampled ~32.8 percent, but no-parallel-dotnet rule blocks compile.

Residual risk:
- No Unity Console test run, play-mode surface-weather pass, profiler capture, or player-build proof was produced.
- Full solution compile remains blocked by the external workspace/package failure profile already recorded.

## 2026-05-27 - Atmosphere VFX/Ocean Runtime Editor Boundary Continuation

What was wrong:
- `SurfaceWeatherVfxRig` registered itself with `HectonFloatingOrigin` from edit-mode `OnEnable`.
- `ShinobuOceanSurfaceAtmosphereRuntime` started runtime work from edit-mode `OnEnable`: registry reads, DataVault buffer hydration, GPU buffer creation, wave upload, ocean provider registration, dispatcher lane registration, and shader global publication.
- Ocean readback dispatch was armed by default before any runtime owner phase.

What was done:
- `Assets/_Project/Scripts/Atmosphere/SurfaceWeatherVfxRig.cs`
  - Added an `Application.isPlaying` gate before origin listener registration.
  - Left disable/destroy unregister calls intact so stale residue from older editor activations can still be cleared.
- `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs`
  - Added an `Application.isPlaying` gate before DataVault, GPU, ocean-provider, dispatcher, and shader work.
  - Changed `_readbackDispatchEnabled` to default-off; runtime `OnEnable` arms it explicitly.
  - Preserved unrelated pre-existing wave/readback edits already present in the dirty file.
- `Assets/_Project/Tests/Editor/SurfaceWeatherDirectorEditTests.cs`
  - Added `SurfaceWeatherVfxRigDoesNotRegisterOriginListenerInEditMode`.
  - Added `OceanSurfaceAtmosphereRuntimeDoesNotStartRuntimeFromEditMode`.
  - Tests assert no origin listener, no ocean runtime claim, no ocean provider registration, no dispatcher flags, no vault readiness, no readback dispatch, and no wave/readback GPU buffers in edit mode.

Cinematic cheats used:
- No new physical ocean/weather simulation.
- Kept ocean surface atmosphere as a runtime presentation provider, not an editor-started simulation.
- Preserved lightning as cheap authored line geometry plus shader/event fakes.

Exact microseconds saved:
- Runtime steady-state saved: 0 us; play-mode runtime paths are intentionally preserved.
- Edit-mode cold work avoided: DataVault hydration, ocean provider service creation risk, dispatcher registration attempts, shader global publication, two wave `GraphicsBuffer`s, and six readback/query/result `GraphicsBuffer`s.
- Estimated avoided authoring hitch: hundreds of microseconds to several milliseconds on i3/MX350 depending on graphics driver and vault state.
- GC impact: no new runtime allocation path by static inspection.

Verification:
- `git diff --check` passed on `SurfaceWeatherVfxRig.cs`, `ShinobuOceanSurfaceAtmosphereRuntime.cs`, and `SurfaceWeatherDirectorEditTests.cs` with line-ending warnings only.
- `rg` confirmed the new play-mode gates and edit-mode regression tests.
- Build was not launched. No build processes were active, but repeated CPU gates still spiked above 50 percent: 100/76.3 percent, then 58.7 percent, then 66.7/75.6/53.4 percent; project rules forbid compile above 50 percent CPU.

Residual risk:
- No Unity Console edit-mode test run, play-mode ocean/weather pass, profiler capture, or player-build proof was produced.
- Full solution compile remains blocked by the external workspace/package failure profile already recorded.

## 2026-05-27 - Surface Weather Job Teardown Fence Continuation

What was wrong:
- `HectonSurfaceWeatherDirector.DisposeWeatherMathBuffers()` attempted pending weather job completion with `forceComplete: false`.
- If the job was still running, disposal returned before releasing the weather job output handle.
- `ClearEditorRuntimeResidue()` then cleared `_weatherJobScheduled` and `_weatherJobPrimed` anyway, producing false local state.
- Runtime `OnDisable()` unregistered services/listeners but did not dispose weather math buffers at all.

What was done:
- `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs`
  - Replaced parameterless disposal with `DisposeWeatherMathBuffers(bool forceCompletePendingJob)`.
  - `OnDisable`, `OnDestroy`, and editor stale cleanup now force-complete pending weather jobs before releasing the output handle.
  - Normal per-frame weather completion remains non-forced through `TryCompleteWeatherMathJob()`.
  - Removed manual editor cleanup flag clearing that could mask a failed disposal.
- `Assets/_Project/Tests/Editor/SurfaceWeatherDirectorEditTests.cs`
  - Added `SurfaceWeatherDirectorTeardownForcesPendingWeatherJobFence`.
  - Test verifies forced teardown call sites and rejects the old parameterless dispose/masked-flag pattern.

Cinematic cheats used:
- No new weather physics.
- Preserved the existing scheduled scalar/job weather fake.
- Teardown now pays the synchronization cost once only when the owner is being disabled/destroyed.

Exact microseconds saved:
- Runtime steady-state saved: 0 us; normal weather job cadence is unchanged.
- Teardown cost: possible one-time forced job completion if a weather job is pending.
- Failure cost avoided: stale DataVault output handle plus false `_weatherJobScheduled=false` state after failed non-forced completion.
- Estimated i3/MX350 impact: no steady-frame gain; prevents teardown/re-enable recovery variance and undefined stale output reads.
- GC impact: no new runtime allocation path by static inspection.

Verification:
- `git diff --check` passed on `HectonSurfaceWeatherDirector.cs` and `SurfaceWeatherDirectorEditTests.cs` with line-ending warning only.
- `rg` confirmed three forced teardown call sites, the bool disposal signature, the regression test, and the preserved non-forced normal frame completion.
- Build was not launched. No build processes were active, but CPU samples were 50.9, 24.1, 45.7, 65.6, 42.2 percent; project rules forbid compile above 50 percent CPU.

Residual risk:
- No Unity Console edit-mode test run, play-mode weather teardown pass, profiler capture, or player-build proof was produced.
- Full solution compile remains blocked by the external workspace/package failure profile already recorded.

## 2026-05-27 - Ocean Surface Quality Route Continuation

What was wrong:
- `ShinobuOceanSurfaceAtmosphereRuntime` sampled `_globalQualityWeight`, but wave cadence/readback/telemetry routes still used max-quality constants.
- `_timeSeconds` was quantized through `AuthoritativeQualityWeight`.
- GPU wave-height readback sample budget, readback active wave count, AUP phase bases, shader readback quality uniform, telemetry active wave count, and telemetry hash did not fully consume the continuous quality value.
- Low-tier hardware could therefore pay max-quality readback/cadence cost while only part of the visible shader path scaled.

What was done:
- `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs`
  - `_timeSeconds` now uses `ResolveWaveEvaluationTime(..., _globalQualityWeight)`.
  - Wave-height readback sample budget now uses `_globalQualityWeight`.
  - Readback active wave count, AUP phase bases, LOD quality, shader quality upload, and readback LOD vector now use `_globalQualityWeight`.
  - Telemetry active wave count and wave state hash now use `_globalQualityWeight`.
  - Preserved unrelated pre-existing compute/readback safety edits already present in the dirty file.
- `Assets/_Project/Tests/Editor/ShinobuOceanSurfaceAtmosphereEditTests.cs`
  - Added `RuntimeQualityWeight_DrivesWaveCadenceReadbackAndTelemetry`.
  - Test rejects old authoritative-quality bypass tokens and requires the runtime path to consume `_globalQualityWeight`.

Cinematic cheats used:
- No physical ocean truth added.
- Kept ocean surface as a quality-scaled deterministic presentation fake.
- Low quality reduces cadence and readback samples instead of changing gameplay authority or DTO layout.

Exact microseconds saved:
- Low-tier readback frames avoid max-quality sample traffic; budget now scales down toward 4 samples.
- Wave evaluation time can quantize at lower cadence instead of 60 Hz-equivalent presentation updates.
- Estimated i3/MX350 savings: tens to hundreds of microseconds on readback frames depending on GPU driver and readback queue pressure.
- High/Ultra cost intentionally remains high-quality/overkill.
- GC impact: no new runtime allocation path by static inspection.

Verification:
- `git diff --check` passed on `ShinobuOceanSurfaceAtmosphereRuntime.cs` and `ShinobuOceanSurfaceAtmosphereEditTests.cs` with line-ending warnings only.
- `rg` found no remaining `authorityQuality` or old authoritative-quality bypass tokens in runtime.
- `rg` confirmed `_globalQualityWeight` drives wave time, readback budget, active wave count, shader quality, and telemetry hash.
- Build was not launched. Active `dotnet` PID 36124 existed and CPU samples were 100.0, 100.0, 92.0, 100.0, 100.0 percent.

Residual risk:
- No Unity Console edit-mode test run, GPU readback runtime pass, profiler capture, or player-build proof was produced.
- Full solution compile remains blocked by the external workspace/package failure profile already recorded.

## 2026-05-27 - Celestial Snapshot And Sky Camera Route Continuation

What was wrong:
- `HectonCelestialEngine.ClearCelestialRuntimeSnapshot()` published an empty `CelestialRuntimeSnapshot` through `GlobalRegistry` unconditionally.
- The clear route can be reached from edit-mode disable/destroy cleanup, so authoring actions could erase runtime celestial snapshot state outside play mode.
- `SkySystemFollowCamera.ResolveTargetCamera()` still used runtime `Camera.GetAllCameras()` and tagged-main-camera fallback when explicit/cached camera data was missing.
- That scene scan violated the hot-path dependency doctrine and could scale with camera count in the sky-follow path.

What was done:
- `Assets/_Project/Scripts/HectonCelestialEngine.cs`
  - Kept visual/shader cleanup behavior.
  - Gated global celestial snapshot publication behind `Application.isPlaying`.
- `Assets/_Project/Scripts/SkySystemFollowCamera.cs`
  - Removed runtime camera buffer and `Camera.GetAllCameras()` fallback.
  - Removed tagged-main-camera resolver.
  - Runtime target resolution now uses explicit `runtimeCamera`, cached resolved camera, or cached `IPlayerRuntimeContext.PlayerCamera`.
  - Sea-level owner fallback now prefers cached `IPlayerRuntimeContext.PlayerMovement` before explicit cold camera fallback.
  - Edit-mode Scene View camera fallback remains editor-only.
- `Assets/_Project/Tests/Editor/HectonCelestialEngineEditTests.cs`
  - Added `ClearCelestialRuntimeSnapshotDoesNotPublishGlobalSnapshotInEditMode`.
  - Added `SkySystemFollowCameraRuntimeRouteDoesNotSceneScanCameras`.
  - Regression coverage is source/static for the camera route and private-state edit-mode proof for global celestial snapshot publication.

Cinematic cheats used:
- No physical sky/orbit/weather simulation added.
- Preserved deterministic presentation routes.
- Removed scene discovery and edit-mode global mutation so saved cost can buy richer horizon, clouds, shafts, and celestial composition.

Exact microseconds saved:
- Celestial snapshot gate: runtime steady-frame saved 0 us; prevents false edit-mode global state invalidation and downstream first-frame recovery work.
- Sky camera route: removes worst-case O(camera count) scan/fill from runtime fallback frames.
- Estimated i3/MX350 savings in multi-camera fallback scenes: 5-40 us on affected sky-follow frames.
- High/Ultra behavior remains capable of visual overkill; truth ownership and DTO layout unchanged.
- GC impact: no new runtime allocation path by static inspection; removed the runtime camera-buffer scene discovery dependency.

Verification:
- `git diff --check` passed on `SkySystemFollowCamera.cs`, `HectonCelestialEngine.cs`, and `HectonCelestialEngineEditTests.cs` with line-ending warnings only.
- `rg` confirmed `Camera.GetAllCameras`, `ResolveTaggedRuntimeMainCamera`, `RuntimeCameraBufferSize`, and `_runtimeCameraBuffer` are absent from runtime source.
- `rg` confirmed `TryResolveCachedPlayerCamera()` and cached `playerContext.PlayerMovement` route exist.
- `rg` confirmed `ClearCelestialRuntimeSnapshot()` now gates `GlobalRegistry.PublishCelestialRuntimeSnapshot` behind `Application.isPlaying`.
- Build was not launched. Active `dotnet` PIDs 13180 and 30368, active `VBCSCompiler` PID 26996, and CPU samples 69, 41, 63, 57, 56 percent violated the build gate.

Residual risk:
- No Unity Console edit-mode test run, play-mode sky-follow pass, profiler capture, or player-build proof was produced.
- Static analysis sub-agent found additional atmosphere-domain risks still open: ocean read accessors that mutate readback queues, `GlobalWeatherDirector` late-frame celestial registry read, harmonic quality stepping, and atmosphere manager biome lazy resolve from slow tick.
- Full solution compile remains blocked by the external workspace/package failure profile already recorded.

## 2026-05-27 - Ocean Read Purity And Weather-Celestial Snapshot Route

What was wrong:
- `ShinobuOceanSurfaceAtmosphereRuntime` public read accessors queued wave-height readbacks from getter-like calls.
- `TryGetSurfaceWeatherState()` tried to complete the wave-parameter job from a read-looking API.
- Those paths violated pure read doctrine and could mutate GPU queues or job fences from buoyancy/sky/ocean consumers.
- `GlobalWeatherDirector.PublishAtmosphericBridgeShaderState()` hot-read `GlobalRegistry.CelestialRuntimeSnapshot` from late-frame shader publish.
- Weather/celestial visual coupling therefore bypassed cached owner dependency routing.

What was done:
- `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs`
  - Removed read-path `QueueWaveHeightSample` calls from `TrySampleWaveKinematics`, `GetWaterHeight`, and `GetWaveNormal`.
  - Removed `TryCompleteWaveParameterKernel()` from `TryGetSurfaceWeatherState()`.
  - Added `TryEvaluateWaveKinematicsSnapshot()`.
  - Read path now consumes completed readback data first.
  - If no completed sample exists and no wave-parameter job is scheduled, it evaluates current `WaveParametersDTO` snapshot through `HectonOceanSurfaceMath.EvaluateWavesDetailed`.
  - If a job is scheduled or data is unavailable/non-finite, it fails closed instead of synchronizing or inventing wave detail.
- `Assets/_Project/Tests/Editor/ShinobuOceanSurfaceAtmosphereEditTests.cs`
  - Added `RuntimeOceanReadAccessors_DoNotQueueWaveReadbacks`.
  - Test rejects `QueueWaveHeightSample` and `TryCompleteWaveParameterKernel()` from read accessor regions and requires deterministic snapshot evaluation.
- `Assets/_Project/Scripts/Environment/GlobalWeatherDirector.cs`
  - Added cached `HectonCelestialEngine` dependency.
  - Cold resolve reads `GlobalRegistry.CelestialEngine` once.
  - Hot-swap updates handle `GlobalRegistryServiceSlot.CelestialEngineRuntime`.
  - Atmospheric bridge now reads `ReadCachedCelestialRuntimeSnapshot()` instead of `GlobalRegistry.CelestialRuntimeSnapshot`.
- `Assets/_Project/Tests/Editor/GlobalWeatherDirectorEditTests.cs`
  - Added `AtmosphericBridgePublishUsesCachedCelestialEngineSnapshot`.
  - Test rejects the global celestial snapshot poll from the bridge region and requires cached celestial engine/hot-swap route.

Cinematic cheats used:
- No physical ocean simulation added.
- Read fallback uses existing deterministic Gerstner-style snapshot math already owned by ocean presentation.
- Weather/celestial bridge remains shader-parameter presentation, not a second truth owner.
- Low/Middle/High/Ultra all keep one owner route; fidelity is bought through existing quality/cadence systems, not binary behavior switches.

Exact microseconds saved:
- Ocean read accessors: removes per-query GPU readback enqueue pressure; estimated i3/MX350 savings 5-30 us on dense query frames that previously queued extra samples.
- Ocean read accessors: avoids potential hidden job-drain stalls; worst-case avoided stall can exceed 1000 us if a read path tried to complete pending wave work.
- Weather bridge: removes one hot global celestial snapshot access per atmospheric shader publish; estimated i3/MX350 variance reduction 1-3 us/frame on bridge frames.
- Runtime GC impact: no new managed allocation path by static inspection.

Verification:
- `git diff --check` passed for `ShinobuOceanSurfaceAtmosphereRuntime.cs`, `ShinobuOceanSurfaceAtmosphereEditTests.cs`, `GlobalWeatherDirector.cs`, and `GlobalWeatherDirectorEditTests.cs` with line-ending warnings only.
- Source slice proof: ocean read accessor region contains no `QueueWaveHeightSample`.
- Source slice proof: ocean read accessor and surface-weather read regions contain no `TryCompleteWaveParameterKernel()`.
- Source slice proof: ocean runtime contains `TryEvaluateWaveKinematicsSnapshot`, fail-closed job guard, and `HectonOceanSurfaceMath.EvaluateWavesDetailed`.
- Source slice proof: atmospheric bridge region contains no `GlobalRegistry.CelestialRuntimeSnapshot` and uses `ReadCachedCelestialRuntimeSnapshot()`.
- Source proof: weather director contains cached celestial engine field and `GlobalRegistryServiceSlot.CelestialEngineRuntime` handling.
- Build was not launched. Active `VBCSCompiler` PID 24496 existed. CPU samples were 70.9, 85.8, 61.6, 27.1, 48.6 percent on the first gate and 69.5, 40.9, 52.2, 51.6, 54.4 percent on the second gate.

Residual risk:
- No Unity Console edit-mode test run, play-mode ocean query pass, GPU readback runtime pass, profiler capture, or player-build proof was produced.
- Remaining atmosphere-domain candidates still worth auditing: harmonic quality stepping and atmosphere manager biome lazy resolve from slow tick.
- Full solution compile remains blocked by the external workspace/package failure profile already recorded.

## 2026-05-27 - Atmosphere Procedural Biome Dependency Route

What was wrong:
- `HectonAtmosphereManager.RefreshProceduralBiomeInfluenceSnapshotIfNeeded()` runs from the atmosphere `SlowTick` timeline.
- If `_proceduralFieldSampler` was null, that hot route called `WorldRuntimeReferenceUtility.TryResolveWorldProceduralFieldSampler()`.
- The helper falls back to `WorldProceduralFieldSampler.ActiveRuntimeInstance`, not the documented cached registry service route.
- Biome matrix initialization similarly used `WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector()` and its active-instance fallback.
- Atmosphere is a consumer of world biome influence, not the owner of world procedural sampling. It should cache the owner service or fail closed.

What was done:
- `Assets/_Project/Scripts/HectonAtmosphereManager.cs`
  - `CacheRegistryRuntimeReferences()` now caches `GlobalRegistry.BiomeMatrix` and `GlobalRegistry.ProceduralFieldSampler`.
  - Added `GlobalRegistryServiceSlot.BiomeMatrixRuntime` hot-swap handling.
  - Added `GlobalRegistryServiceSlot.ProceduralFieldSamplerRuntime` hot-swap handling.
  - Removed `WorldRuntimeReferenceUtility.TryResolveWorldProceduralFieldSampler()` from the biome influence refresh path.
  - Replaced active-instance biome matrix refresh with `RefreshBiomeMatrixDirectorFromRegistry()`.
  - Added `ClearProceduralBiomeInfluenceState()` so sampler loss clears stale fog/color influence and hysteresis.
- `Assets/_Project/Tests/Editor/AtmosphereManagerEditorPreviewTests.cs`
  - Added `AtmosphereManagerRuntimeBiomeRoutesUseCachedRegistryServices`.
  - Test rejects slow-tick lazy procedural sampler resolve and active-instance biome matrix fallback.
  - Test requires cached registry and hot-swap routes for both dependencies.

Cinematic cheats used:
- No new biome/atmosphere simulation.
- Atmosphere remains a deterministic visual consumer: fog color, density, attenuation, sky tint.
- If world sampler authority is absent, atmosphere falls back to authored/base profiles instead of inventing procedural influence.
- Low/Middle/High/Ultra use the same authority route; higher tiers can still spend visual budget on richer biome haze once the valid world owner exists.

Exact microseconds saved:
- Removes active-instance fallback from atmosphere biome refresh.
- Estimated i3/MX350 savings: 2-8 us on refresh frames where sampler or biome matrix was null.
- More important: removes stale-authority risk and prevents hidden recovery work from early-world surface/underwater visibility.
- Runtime GC impact: no new managed allocation path by static inspection.

Verification:
- `git diff --check` passed for `HectonAtmosphereManager.cs` and `AtmosphereManagerEditorPreviewTests.cs` with line-ending warning only.
- Source slice proof: biome refresh region contains no `WorldRuntimeReferenceUtility`.
- Source proof: `WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref _biomeMatrixDirector)` is absent.
- Source proof: `GlobalRegistry.ProceduralFieldSampler`, `GlobalRegistry.BiomeMatrix`, `ProceduralFieldSamplerRuntime`, and `BiomeMatrixRuntime` routes exist.
- Build was not launched. Active `dotnet` PID 24312 and active `VBCSCompiler` PID 50784 violated the build gate. CPU samples were 27.6, 30.3, 31.3, 31.4, 31.4 percent.

Residual risk:
- No Unity Console edit-mode test run, Play Mode atmosphere/biome transition pass, profiler capture, GC capture, or player-build proof was produced.
- Full solution compile remains blocked by shared active build/compiler processes and the external workspace/package failure profile already recorded.
