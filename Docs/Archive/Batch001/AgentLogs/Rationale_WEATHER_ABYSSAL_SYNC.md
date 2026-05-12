# WEATHER_ABYSSAL_SYNC Rationale

Status: PENDING VERIFICATION

## Decision 1: Atmospheric Bridge Cadence

Problem: Surface weather currently drives sky/ocean presentation independently from abyssal fog, marine snow, god-rays, and flow surge. Updating every frame would spend CPU on values that shader smoothing can hide.
Solution: Publish a compact atmospheric bridge parameter set on FrostTick and cold init/disable only. Shaders interpolate with their own material/global smoothing. This matches visual-fake-first and frame-time dictatorship.
Rejected Alternatives: Per-frame cloud/fog orchestration and per-camera volume mutation were rejected because the player sees the resulting fog/light, not the physical cloud state.
Scalability potential: Low uses one scalar fog/snow modulation and one triangle-wave occlusion; Middle adds shader polynomial scattering; High adds multi-octave caustics in shader; Ultra can spend saved CPU on richer volumetric GPU visuals.
Hardware Impact: Estimated low-end i3/MX350 gain is ~8-15 us/frame CPU by removing nonessential shader global churn from HotTick.

## Decision 2: WeatherIntensity Semantics

Problem: `WeatherIntensity` is documented and published as transition alpha, but abyssal consumers use it as storm/current strength. During transition from storm to calm, transition alpha rises while actual storm force falls, which can invert flow behavior.
Solution: Resolve `WeatherIntensity01` from source/target phase severity and transition blend. Calm=0, Storm/CurrentSurge=1, transition blends between those truths.
Rejected Alternatives: Adding another field was rejected because existing consumers already read `WeatherIntensity`; duplicating semantics would leave old consumers wrong.
Scalability potential: Low/Middle consume one scalar; High/Ultra can add finer state weighting later without breaking the existing ABI.
Hardware Impact: Estimated gain is correctness plus ~5-20 us/frame avoided versus consumers correcting state independently.

## Decision 3: Thunder Shock Event Surface

Problem: Lightning visual/audio response exists, but gameplay shock is coupled to optional thunder clips. No clip means no acoustic/seismic response.
Solution: Dispatch an `AcousticPingEvent` through existing `PhysicsEventBus` and a camera shake through `GlobalRegistry.CameraJuice` from the thunder playback lane before optional audio clip checks.
Rejected Alternatives: A new string event channel, direct soundscape dependency, or per-listener loop were rejected. Existing NativeQueue-backed physics events satisfy the decoupling rule.
Scalability potential: Low uses one ping/shake event; Middle can add DSP thunder synthesis listener; High/Ultra can add pressure-wave rendering listeners without changing lightning code.
Hardware Impact: Estimated event overhead is single-digit microseconds per thunder event and zero steady-frame cost.

## Decision 4: MX350 Fog Overdraw Fallback

Problem: Storm silt plus abyssal fog can become fill-rate heavy on MX350 if implemented as full-res volumetric overdraw.
Solution: The code exposes scalar pressure only; the required renderer fallback is half-resolution fog composite with blue-noise dither and depth-aware bilateral upscale.
Rejected Alternatives: Full-resolution raymarching on MINIMAL/MX350 was rejected without profiler proof under 1.8ms GPU raymarching budget.
Scalability potential: Low half-res composite, Middle full-res cheap Beer-Lambert, High multi-step light shafts, Ultra richer volumetric scatter.
Hardware Impact: Estimated GPU saving for half-res composite is 0.4-1.2 ms on MX350 depending on fog coverage; CPU patch impact is negligible.

## Verification Result

Problem: Required compilation check cannot complete because `Hecton8.Core.csproj` currently fails in `ScannerTool.cs` on `DataArchaeologyRuntime`, an unrelated gameplay/scanner dependency already present in the dirty worktree.
Solution: Do not revert or patch outside domain. Record the compile blocker and keep weather changes bounded to `GlobalWeatherDirector`, `HectonSurfaceWeatherDirector`, and the weather contract comment.
Rejected Alternatives: Fixing `ScannerTool.cs` from the weather pass was rejected because it crosses into scanner/archaeology ownership and the file already contains unrelated user/agent edits.
Scalability potential: No weather scalability impact.
Hardware Impact: No runtime impact from the verification blocker. Weather bridge remains a FrostTick scalar publisher with no managed allocation path found by static scan.
