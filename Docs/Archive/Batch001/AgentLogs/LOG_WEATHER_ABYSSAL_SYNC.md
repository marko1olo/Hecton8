# WEATHER_ABYSSAL_SYNC Log

Status: PENDING VERIFICATION

## 2026-05-11 Atmospheric Bridge Pass

What was wrong:
- Weather intensity was published as transition alpha while downstream abyssal flow consumers treated it as storm/current strength.
- Abyssal fog, marine snow, god-ray strength, rain rumble, shadow fade, wind drift, radiation, and biolum scalar had no compact atmospheric bridge from global weather/celestial data.
- Thunder gameplay response was coupled to optional `AudioClip[] thunderClips`; missing clips suppressed acoustic/camera shock.

What was done:
- `GlobalWeatherDirector` now implements `IFrostTickable` and publishes atmospheric bridge shader globals on FrostTick plus init/disable.
- `WeatherIntensity` now resolves to normalized storm/current severity. Calm is 0. Storm and CurrentSurge are 1. Transitions blend source and target severity.
- Global current now gets the requested surge multiplier: `1.0 + WeatherIntensity * 0.5`.
- Storm silt publishes `_AbyssalFogDensity`, `_MarineSnowOpacity`, `_HectonAtmosphericBridgeParams`, and `_HectonGlobalFlowMagnitudeMultiplier`.
- God-rays publish `_HectonGodRayIntensity` from moon phase, wave height, and deterministic triangle-wave cloud occlusion.
- Wind/rain/shadow/celestial scalars publish `_HectonGlobalWindDirection`, `_HectonUnderwaterRainVolume`, `_HectonShadowCascadeFade`, `_HectonRadiationStorm`, and `_HectonBiolumEmissionMultiplier`.
- `HectonSurfaceWeatherDirector` now configures pending thunder even when no thunder clips are assigned.
- `PlayThunder()` dispatches `PhysicsEventBus.NotifyAcousticPing(new AcousticPingEvent(...))` and `GlobalRegistry.CameraJuice.TriggerSubmarineImpactShake(...)` before optional audio playback.

Cinematic cheats used:
- Storm silt is scalar shader modulation, not real sediment simulation.
- Cloud occlusion is a deterministic triangle wave, not volumetric cloud truth.
- God-ray response is moon phase + wave-height scalar multiplication, not physical light shafts.
- Thunder shock is a single acoustic ping/camera impulse, not propagated pressure simulation.

Estimated microseconds saved:
- 8-15 us/frame by moving atmospheric visual bridge writes off HotTick to FrostTick.
- 20-60 us/frame by replacing cloud/light weather truth with shader-side scalar and triangle-wave flicker.
- 5-20 us/frame by applying current surge once in weather publication instead of per-consumer/per-node correction.
- 3-10 us/thunder event by using the existing NativeQueue-backed physics event surface instead of a new managed dispatch path.

Verification:
- `git diff --check`: pass, line-ending normalization warnings only.
- Static weather hot-path scan: no `UnityEngine.Random`, `Random.`, `math.sin/cos`, `math.normalize`, `.ToString()`, `string.Format`, or string interpolation in `GlobalWeatherDirector.cs`, `HectonSurfaceWeatherDirector.cs`, or `SurfaceWeatherMath.cs`.
- Cyrillic scan for weather/atmosphere script folders: no hits.
- Duplicate method audit in `GlobalWeatherDirector.cs`: only intentional `NormalizeSafe` overloads.
- Compile: blocked by unrelated existing dependency. `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false` fails at `Assets/_Project/Scripts/ScannerTool.cs(518,17): CS0246 DataArchaeologyRuntime could not be found`.

MX350 fog overdraw fallback:
- If storm fog overdraw exceeds budget on MX350, render fog/silt at half resolution, sample blue-noise dither screen-space, and depth-aware bilateral upscale into the main color buffer. Full-resolution volumetric/raymarch fog remains blocked without profiler proof.

