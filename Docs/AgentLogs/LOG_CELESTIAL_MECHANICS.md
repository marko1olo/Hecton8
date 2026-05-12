# CELESTIAL_MECHANICS Log

## Session Start

What was wrong: Directional Light rotation and eclipse behavior were assigned for replacement with a SlowTick deterministic visual fake.
What was done: Prompt extracted from CURRENT_BATCH.md; domain and task count identified; mandates selected; fresh status/rationale/log files created.
Cinematic Cheats used: Eclipse will be intensity/ambient/fog scalar work, not volumetric occlusion.
Exact Microseconds saved: PENDING VERIFICATION. No profiler capture yet.

## Final Report - 2026-05-12

What was wrong: The celestial lane had to stop treating the sun, eclipse, storm, and lightning as expensive real-time lighting problems. Full assembly verification is unstable because unrelated parallel-agent files are red.

What was done:
- Added `CelestialOrbitMathJob : IJob` with Burst attributes, `float3` math, persistent `NativeArray<CelestialOrbitJobOutput>`, and late-frame commit.
- Kept Directional Light rotation under SlowTick ownership; Low/MX350/Unknown locks the sun at 45 degrees.
- Preserved eclipse as a scalar fake: directional intensity, sky blend, ambient SH probe, `_HectonAtmosphereColor`, `_EclipseOcclusion`, and `_HectonCelestialBiolumMultiplier`.
- Added storm scalar override through `WeatherEvents`/`GlobalRegistry` and `_HectonStormCloudDensity`.
- Added additive `WeatherEvents.Lightning`; `HectonSurfaceWeatherDirector` raises it, celestial sets `_HectonLightningFlash = 1.0`, then decays with `math.lerp`.
- Replaced atmosphere `Gradient.Evaluate` calls with manual key interpolation into persistent `NativeArray<float4>` samples and runtime manual lerp.
- Added abyssal early-out below -200m/depth >= 200m.
- Added 300-frame native blackbox telemetry with binary dump path `Docs/AgentLogs/Dump_CELESTIAL_MECHANICS.bin`.
- Logged Atmosphere recon in `RECON_CELESTIAL_MECHANICS.md`; no competing `Update()` Directional Light rotation found.

Cinematic Cheats used:
- Eclipse is scalar/ambient/fog, not volumetric shadowing.
- Celestial orbits are cinematic circular/triangle-wave approximations with precomputed reciprocals, not high-fidelity orbital mechanics.
- Storm and lightning are shader globals, not dynamic light sources.
- Low tier uses locked sun angle and sky/ambient color fakery.
- Abyssal depth skips surface celestial CPU entirely.

Exact Microseconds saved:
- Orbit job / deferred commit: 25-70 us per snapshot tick.
- SlowTick/low-tier light lock: 80-300 us on low-end frames versus dynamic rotation/shadow churn.
- Eclipse scalar instead of volumetric path: 500-2000 us avoided during eclipse scenes.
- Ambient SH scalar update instead of probe/light churn: 20-80 us.
- `_HectonAtmosphereColor` global instead of per-material updates: 10-40 us.
- Weather storm scalar path: 15-50 us.
- Lightning shader scalar instead of scene lights: 100-500 us per flash burst.
- Abyss cull: 50-150 us per abyss SlowTick.
- Manual packed gradient sampling: 5-20 us during LUT/global refresh.
- Total expected protected range under combined eclipse/storm/low-tier pressure: 805-3210 us, scene-dependent and not profiler-measured.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1` passed once after implementation with 3 unrelated warnings.
- Latest direct core build is blocked by unrelated `World/AcousticOcclusionUtility.cs` missing `AcousticSurfaceResponse` and `UI/PDAMapTab.cs` duplicate `SonarPointCloudPoint`.
- `Assembly-CSharp.csproj` is blocked by unrelated `UI/PDAMapTab.cs` missing `TryResolvePointCloudCamera`, `IsPointCloudVisibleToCamera`, and `TryResolvePredatorAupBuffer`.
- Static proof: no `Gradient.Evaluate` matches in `HectonCelestialEngine.cs`; no `Vector3`, `math.sqrt`, `math.normalize`, or direct division matches inside `CelestialOrbitMathJob`.

Status: PENDING VERIFICATION due unrelated global compile dependency.

## Quality Pass - 2026-05-12

What was wrong: First-use native buffers could still allocate on the first celestial SlowTick, and surface weather could race celestial for `_HectonLightningFlash` ownership.

What was done:
- Prewarmed orbit output, blackbox, and packed atmosphere gradient samples in play-mode `OnEnable`.
- Reset `_HectonLightningFlash` when the celestial owner starts.
- Raised `WeatherEvents.Lightning` from job-driven surface weather lightning, not only the managed fallback path.
- Gated surface weather `_HectonLightningFlash` writes so it only acts as fallback when no celestial owner is registered.

Cinematic Cheats used: Lightning remains one shader scalar with decay; storm remains one cloud-density scalar; no dynamic lightning light source was introduced.

Exact Microseconds saved:
- First SlowTick allocation hitch removed: estimated 20-100 us plus GC-risk elimination.
- Lightning ownership race removed while keeping shader-only burst path: 100-500 us protected per flash versus real light churn.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1` succeeded with 3 unrelated warnings and 0 errors.
- Static scans found no `Gradient.Evaluate`, no `Vector3`/`math.sqrt`/`math.normalize`/direct division in `CelestialOrbitMathJob`, and no `foreach`/`string.Format`/`.ToString(` in touched celestial/weather files.

## Compile Recheck - 2026-05-12

What was wrong: Previous final report carried stale dependency-block status from parallel-agent compile drift.

What was done: Re-ran `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1`; build succeeded with 62 warnings and 0 errors. Rechecked Unity MCP; script refresh timed out waiting for editor readiness and console read returned `no_unity_session`.

Cinematic Cheats used: No new runtime cheat in this step; verification-only pass.

Exact Microseconds saved: 0 us. This pass changes evidence quality, not runtime cost.

Verification: Dotnet compile is clean for project assemblies. Unity Console/Play Mode/profiler verification remains PENDING because the Unity session was unavailable/timed out.

## Final Recheck - 2026-05-12

What was wrong: Abyss cull skipped the timeline, which also skipped lightning scalar decay. Fallback/low-tier snapshots did not apply the eclipse biolum overkill multiplier.

What was done:
- Abyss cull now clears `_HectonLightningFlash` before returning.
- Fallback/low-tier celestial snapshots now apply the same eclipse biolum multiplier used by the main orbit path.
- Re-ran full project compile and diff hygiene checks.

Cinematic Cheats used:
- Abyss uses scalar clear instead of running surface weather/celestial timeline.
- Fallback path buys visual overkill through one scalar biolum multiplier, not extra lights or volumetrics.

Exact Microseconds saved:
- Abyss timeline remains skipped: 50-150 us per abyss SlowTick preserved.
- Stale flash fix costs under 5 us via one shader global scalar write.

Verification:
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1` succeeded with 0 warnings and 0 errors.
- `git diff --check` returned no whitespace errors; only CRLF normalization warnings for existing working-copy line endings.
- Unity Console/Play Mode/profiler verification remains PENDING because Unity MCP had no active console session after refresh timeout.

## Hardening Recheck - 2026-05-12

What was wrong: Later audit found three strict-review risks: destroy-path event cleanup was not explicit enough, lightning flash decay was tied to coarse global update cadence, and atmosphere density still used `AnimationCurve.Evaluate` even though gradient evaluation had already been removed.

What was done:
- Added explicit `BiomeMatrixEvents.Unregister(this)` and `WeatherEvents.Unregister(this)` in destroy cleanup.
- Moved active `_HectonLightningFlash` decay into `LateFrameTick` with epsilon shutdown so idle frames do not upload the shader scalar.
- Replaced all remaining celestial `.Evaluate(...)` calls with manual keyframe Hermite interpolation for atmosphere density curves.
- Re-ran static scans and dotnet compile attempts.

Cinematic Cheats used:
- Lightning remains one shader scalar, not a runtime light.
- Atmosphere density is deterministic manual curve math, not Unity evaluator calls.
- No physical sky/volumetric path was added.

Exact Microseconds saved:
- Listener cleanup: no frame cost; prevents retained callbacks after teardown.
- Active-only lightning decay: avoids idle shader uploads; preserves 100-500 us saved versus dynamic light flash paths.
- Manual density curve sampling: estimated 5-20 us protected during atmosphere LUT/global refresh pressure.

Verification:
- `rg "\.Evaluate\("` found no matches in the touched celestial/weather files.
- `CelestialOrbitMathJob` slice has no `Vector3`, `math.sqrt`, `math.normalize`, or direct division matches.
- Touched files have no `foreach`, `string.Format`, or `.ToString(` matches.
- `git diff --check` on touched code files returned no whitespace errors, only CRLF normalization warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1` is blocked by unrelated voxel errors: missing `EnsureVoxelSurfaceMeshAvailableAsync` and `EnsureVoxelPhysicsBakeMeshAvailableAsync` in `HectonVoxelEngine.cs`.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1` hits the same voxel wall with 12 unrelated package/editor warnings.
- Unity MCP `validate_script` timed out inside its regex validator on `HectonCelestialEngine.cs`; `read_console` failed because the Unity session ping did not answer.

Status at that checkpoint: PENDING VERIFICATION due external compile wall. Superseded by the later green dotnet builds recorded below.

## Lightning Fidelity Recheck - 2026-05-12

What was wrong: Weather lightning events carried a scalar intensity, but celestial promoted every strike to full-strength flash. Blackbox telemetry also read `_HectonLightningFlash` back from shader globals instead of using owned CPU state.

What was done:
- Preserved `WeatherEvents.RaiseLightning(float)` intensity by consuming `payload.WeatherIntensity`.
- Kept the stronger of active and incoming flash intensity so weak follow-up strikes do not dim an active flash.
- Added `_lastUploadedLightningFlash01` and `UploadLightningFlashShaderGlobal()` to skip redundant shader scalar writes.
- Removed `_HectonLightningFlash` shader readback from blackbox telemetry.

Cinematic Cheats used:
- Lightning remains one shader scalar and authored weather/VFX intensity, not a runtime light or shadow source.
- Telemetry records owned scalar state; no scene query or shader-state dependency is needed.

Exact Microseconds saved:
- Shader readback removed from each blackbox write: estimated 1-5 us protected per SlowTick telemetry write.
- Redundant flash upload skip: sub-micro to low single-digit microseconds, scene/driver dependent.
- Dynamic lightning-light path still avoided: 100-500 us per strike burst.

Verification:
- `rg` found no `_HectonLightningFlash` shader readback; the only direct `_HectonLightningFlash` shader write is inside `UploadLightningFlashShaderGlobal`.
- `.Evaluate(`, `foreach`, `string.Format`, and `.ToString(` scans remain clean in touched celestial/weather files.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1` succeeded with 0 warnings and 0 errors.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1` succeeded with 0 warnings and 0 errors.
- Unity Console/Play Mode/profiler verification remains PENDING: two MCP `read_console` attempts returned `no_unity_session`.

## Storm Upload Gate Recheck - 2026-05-12

What was wrong: `_HectonStormCloudDensity` could be uploaded from event and SlowTick paths even when the scalar was unchanged.

What was done:
- Added `_lastUploadedStormCloudDensity01`.
- Added `UploadStormCloudDensityShaderGlobal()` with finite clamp, saturate, and epsilon skip.
- Forced a storm scalar clear during celestial runtime reset.
- Re-ran static scans, core build, broad assembly build, and Unity console attempt.

Cinematic Cheats used:
- Storm remains a single shader density scalar.
- No volumetric storm or extra light source was added.

Exact Microseconds saved:
- Redundant storm scalar uploads avoided during steady weather: estimated 1-5 us on affected refreshes, driver dependent.
- Existing weather fake budget remains protected: 15-50 us versus concrete weather-scene lookup/material churn.

Verification:
- Only direct `_HectonStormCloudDensity` shader write is now inside `UploadStormCloudDensityShaderGlobal`.
- No storm shader readback exists.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1` succeeded with 53 warnings and 0 errors. Warnings are URP/GPUInstancer/Crest/WaveHarmonic plus unrelated `HectonFluidEngine` unused fields.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1` succeeded with 11 warnings and 0 errors. Warnings are third-party/editor package warnings.
- Unity Console/Play Mode/profiler verification remains PENDING: MCP `read_console` failed with ping not answered.

## Evidence Hygiene Correction - 2026-05-12

What was wrong: The Omega verification paragraph still framed the superseded voxel compile wall as the latest state, even though later dotnet checks succeeded.

What was done:
- Corrected the rationale to preserve those blocked checks as history only.
- Recorded the current verification boundary: dotnet builds compile, Unity Console/Play Mode/profiler evidence is still unavailable.

Cinematic Cheats used:
- None. Documentation evidence correction only.

Exact Microseconds saved:
- 0 us runtime. Prevents integration-time waste from chasing a stale compile-wall report.

Verification:
- Documentation-only change. No code path changed.

## Lightning Cadence Recheck - 2026-05-12

What was wrong: `_HectonLightningFlash` decay had two owners. `LateFrameTick` decayed active flashes, but `UpdateGlobalShaderData()` could also decay the scalar during SlowTick shader refresh.

What was done:
- Replaced the SlowTick call to `UpdateLightningFlashShaderGlobal(false)` with `UploadLightningFlashShaderGlobal(_lightningFlash01, false)`.
- Kept `LateFrameTick` as the only cadence owner for flash decay.
- Re-ran static scans only. No `dotnet build` was run after the user's no-build instruction.

Cinematic Cheats used:
- Lightning remains a single shader scalar.
- No runtime light, shadow source, coroutine, or weather-side timer was added.

Exact Microseconds saved:
- Redundant SlowTick scalar lerp path removed: low single-digit microseconds at most, device dependent.
- Dynamic lightning-light path remains rejected: 100-500 us avoided per strike burst on MX350-class hardware.

Verification:
- `UpdateLightningFlashShaderGlobal()` is only called from `LateFrameTick`.
- The only direct `_HectonLightningFlash` shader write remains inside `UploadLightningFlashShaderGlobal()`.
- Unity Console/Play Mode/profiler evidence remains unavailable; status stays PENDING VERIFICATION.

## Atmosphere LUT Cadence Recheck - 2026-05-12

What was wrong:
- Editor-only atmosphere LUT sampling still used `Gradient.Evaluate`, which broke the strict static mandate even though runtime used packed samples.
- LUT rebuild/publish hid immediate LUT shader-global and `PushSkyToRenderSettings()` work, creating duplicate global/render-state pushes on timeline refreshes and some rebuild paths.

What was done:
- Routed editor and runtime LUT color sampling through packed `NativeArray<float4>` atmosphere gradient samples.
- Moved `OnValidate` sample invalidation before forced LUT rebuild so authored gradients do not bake stale samples.
- Added explicit publish routing through `EnsureCelestialAtmosphereLutReady`, `UpdateDynamicCelestialAtmosphere`, `RebuildCelestialAtmosphereLut`, and `PublishCelestialAtmosphereLut`.
- Kept `RunCelestialTimeline` and `ManualRebakeLut` as the visible owners of final LUT shader-global publish and `PushSkyToRenderSettings()` after global shader data is current.

Cinematic Cheats used:
- 8-sample packed atmosphere gradient fake instead of direct Unity gradient evaluator.
- One shader-global/render-state publish lane per celestial refresh instead of hidden duplicate writes.

Exact Microseconds saved:
- Estimated 5-20 us protected from evaluator/authoring-path ambiguity under LUT rebuild pressure.
- Estimated low single-digit microseconds protected by avoiding duplicate LUT shader globals plus ambient/fog/render-state push on LUT-present celestial refreshes.

Verification:
- `rg "\.Evaluate\("` returns no matches in `HectonCelestialEngine.cs`, `Environment/WeatherEvents.cs`, or `Atmosphere/HectonSurfaceWeatherDirector.cs`.
- Static call-site scan shows LUT publish/update paths carry explicit publish/render-settings intent.
- No `dotnet build` was run due the user's explicit no-build instruction. Status remains `PENDING VERIFICATION`.
