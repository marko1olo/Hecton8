# LOG_13VOD

## Session Start - 2026-05-27
What was wrong: 13VOD has no XML prompt in current batch; direct user assignment defines the water/swimming/Crest/ocean-link domain.
What was done: Created 13VOD tracking files before code changes.
Cinematic Cheats used: None yet.
Exact Microseconds saved: 0 us measured; discovery phase only.

## Audit/Fix Pass - 2026-05-27
What was wrong: `CurrentVolume.Sample*` mutated transform/AUP/sample-time caches from read paths. This violated pure read accessor rules and put repeated hidden cache writes behind fluid, buoyancy, player, tether, and ambient consumers.
What was done: `CurrentVolume` now refreshes cache from dispatcher update/fixed owner phases, rebinds on dispatcher hot-swap, and keeps sample methods read-only over cached state. Active authored-current registration is capped at 32 with dev-build error reporting.
Cinematic Cheats used: Kept triangle-wave ambient current fake; removed axis-snapped taxicab flow and used cheap finite `rsqrt` normalization for authored direction fidelity.
Exact Microseconds saved: Estimated 2-8 us/frame when 8-32 current volumes are sampled by several consumers. No profiler run because compile/runtime proof was gated by CPU load.

What was wrong: Seaglide hydrodynamic force output depended on `GlobalQualityWeight`; low quality changed speed, drag model, fallback flow magnitude, and flow force.
What was done: Seaglide authoritative thrust/drag/flow now uses one deterministic force path. `GlobalQualityWeight` remains only for cavitation presentation in this job.
Cinematic Cheats used: Cavitation stays scalable visual-only; movement truth no longer uses the visual quality scalar.
Exact Microseconds saved: No saved CPU; expected +0.2-1.0 us for active seaglide rows on i3/MX350 class hardware, paid to eliminate cross-device movement divergence.

What was wrong: Ocean provider arbitration polled provider availability every core tick, including Crest availability reads.
What was done: Provider selection now refreshes immediately on register/unregister/hot-swap and otherwise probes at 0.5 s cadence.
Cinematic Cheats used: None. This is route hygiene.
Exact Microseconds saved: Estimated 0.5-3 us/frame while ocean runtime is active, depending on provider count and Crest readiness.

What was wrong: Ambient water motion had hard distance cadence bands, no `GlobalQualityWeight`, and no threshold hysteresis.
What was done: Added one-byte per-object LOD band state, 1.12x exit hysteresis, and quality-scaled cadence masks. Gameplay authority untouched.
Cinematic Cheats used: Preserved triangle-wave bob/sway fake and used quality to buy presentation density instead of simulation.
Exact Microseconds saved: Estimated neutral to -4 us/frame on low-end scenes with many props; high quality intentionally spends more visual updates.

What was wrong: Crest underwater bridge could repeat `GetComponent<UnderwaterRenderer>` from ownership checks and assign Crest `_copyOceanMaterialParamsEachFrame` redundantly.
What was done: `CrestBridge.EnsureUnderwaterPass` returns the cached renderer for the same camera and writes the private Crest flag only on value change.
Cinematic Cheats used: None. This removes adapter churn while retaining the same visual pass.
Exact Microseconds saved: Estimated 0.5-2 us/frame when underwater visuals call ownership checks in LateFrame.

Verification: `git diff --check` passed for touched runtime files and 13VOD logs. `dotnet`/`csc` compile was not launched because CPU measured 77.54%, then 100%, then 77.96%; project rule forbids build launch above 50% CPU or while build work is active.

## Audit/Fix Pass 2 - Weather, Swim Driver, Transition, Depth Sync
What was wrong:
- Surface weather visual sync still resolved fluid/ocean services through `GlobalRegistry`.
- Swim presentation had two runtime owners: dispatcher update and movement-forced sync.
- Transport swim feel retried `TryGetComponent` in the hot path.
- Movement impact/hover helpers used hot `GlobalRegistry.PhysicsStateEvents` fallback.
- Ambient water props without AUP bypassed distance LOD; current sway collapsed diagonals to axes.
- Water transition fixed path could rescan the same signal snapshot multiple times per dispatcher frame.
- Disabled Crest runtime depth-cache sync still reached hierarchy reference resolution from LateFrame.

What was done:
- Cached fluid and ocean service routes in `HectonSurfaceWeatherDirector`.
- Made `HectonPlayerMovement` the sole swim presentation state driver; `PlayerSwimPresentationController` keeps LateFrame shader flush only.
- Removed hot transport coordinator component retries and physics-state registry fallbacks.
- Captured ambient rest AUP once and medium-cadenced no-AUP fallback objects.
- Replaced ambient dominant-axis current direction with normalized visual direction.
- Gated water transition signal scanning once per dispatcher frame.
- Early-exited disabled Crest depth-cache runtime sync before hierarchy scans.

Cinematic Cheats used:
- Kept ambient water as triangle-wave presentation with normalized fake flow, not physical simulation.
- Kept disabled Crest runtime depth-cache camera route because current validator routes depth through RenderGraph; avoided pretending a third-party capture camera is safe.

Exact Microseconds saved:
- Weather cached refs: estimated 0.2-1.5 us/frame during active surface weather.
- Single swim driver: estimated 2-8 us/frame while swimming and removes double integration.
- Hot lookup cleanup: estimated 0.5-3 us/frame on transport or collision frames.
- Ambient AUP/cadence/flow: estimated neutral to -4 us/frame in prop-heavy scenes.
- Transition/depth-cache cadence: estimated 0.5-5 us/frame during repeated fixed transition or visual-sync depth frames.

Verification:
- `git diff --check` passed for modified runtime files with line-ending warnings only.
- Compile not run: CPU measured 65.33%, then 99.04%, then 99.42%; project protocol forbids dotnet/csc launch above 50%. `csc.exe` was not observed.
## 2026-05-27 - 13VOD Audit/Fix Pass 12 - Ocean Adapter DataVault publish guard
What was wrong -> `OceanAdapterVaultRoute.TryPublishWaterLevel` and `TryRecordTelemetry` used `TryOpenOrAcquireLane`; missing boot lanes could be repaired by `EnsureGenerationHandle` from a runtime publish/telemetry helper.
What was done -> Changed those helpers to `TryOpenExistingLane`, requiring an existing exact BufferID/generation handle. `TryAcquireBootHandles` remains the only allocator for request/result/telemetry/profile/water-level/CSV lanes.
Cinematic Cheats used -> No new physical simulation. Kept ocean telemetry/water-level as data DTOs and rejected runtime allocation repair as hidden infrastructure work.
Exact Microseconds saved -> 0.0 us steady-state claimed; the fix removes an unmeasured failure-path DataVault allocation/generation hitch risk rather than a measured per-frame cost.

## 2026-05-27 - 13VOD Audit/Fix Pass 13 - Crest material integrity
What was wrong -> `Crest4KinematicsAdapter` read and wrote Crest `OceanMaterial` foam floats from the surface-weather bridge, violating the third-party integrity rule against runtime material overrides for Crest.
What was done -> Removed Crest foam material property IDs, material reads, and material `SetFloat` writes from the legacy bridge. The bridge now advertises only wind-speed support for surface-weather application.
Cinematic Cheats used -> Foam remains a first-party presentation concern through shader globals/VFX/ocean-surface runtime data, not a Crest material mutation pretending to be simulation truth.
Exact Microseconds saved -> 0.0 us steady-state claimed; removes unmeasured material property churn and runtime asset override risk.

## 2026-05-27 - 13VOD Audit/Fix Pass 14 - Surface weather math buffer hot-route guard
What was wrong -> `HectonSurfaceWeatherDirector` schedule/complete paths used `TryOpenOrAcquireWeatherJobOutput`, allowing recurring weather-to-water binding work to reach an acquire wrapper.
What was done -> Replaced those hot calls with `TryOpenWeatherJobOutput` and removed the acquire wrapper. Initial allocation remains in `EnsureWeatherMathBuffers`.
Cinematic Cheats used -> No simulation added. The existing surface-weather math DTO remains the cheap driver for ocean/weather presentation.
Exact Microseconds saved -> 0.0 us steady-state claimed; removes an unmeasured DataVault allocation-attempt risk in scheduler fault cases.

Verification -> `git diff --check` passed for `HectonSurfaceWeatherDirector.cs`, `OceanAdapterVaultRoute.cs`, `Crest4KinematicsAdapter.cs`, 13VOD status/rationale/log, and the Crest quarantine architecture note. Targeted `rg` found no `TryOpenOrAcquireWeatherJobOutput`, no ocean-adapter `TryOpenOrAcquireLane(`, and no Crest foam `OceanMaterial`/`SetFloat` path in the touched files. Compile not launched: CPU measured 58.01%, then 90.92%; `csc.exe` count was 0, but CPU exceeded the 50% build gate.

## 2026-05-27 - 13VOD Audit/Fix Pass 15 - Underwater shared ocean material write purge
What was wrong -> `HectonUnderwaterVisuals` still applied GI relay and underwater/ocean binding writes to `bridge.OceanMaterial`, which is a Crest material when the active ocean bridge is Crest.
What was done -> Removed writes from `ApplyGIRelaySurfaceEmission` and `ApplyOceanMaterialBindings` to the shared bridge ocean material. First-party `oceanUnderwaterMaterial` and shader globals remain the writable presentation surface.
Cinematic Cheats used -> Underwater belief is carried by first-party material/globals/render-pass presentation, not by mutating third-party ocean material state.
Exact Microseconds saved -> 0.0 us steady-state claimed; removes unmeasured material property churn and third-party material override risk.

## 2026-05-27 - 13VOD Audit/Fix Pass 16 - Surface weather service-cache closure
What was wrong -> `HectonSurfaceWeatherDirector` still read `GlobalRegistry.CelestialEngine` while publishing weather shader globals and `GlobalRegistry.Audio` when flushing queued thunder playback. Those paths are water/weather output flushes, not cold boot wiring.
What was done -> Added cached `IAudioService` ownership, refreshed it through cold resolve and `OnGlobalRegistryServiceReplaced`, and changed weather shader global publication to use the cached `celestialEngine` field instead of a direct registry read.
Cinematic Cheats used -> No new physical weather simulation. Thunder remains a queued event fake and rain/lightning remains shader-global presentation.
Exact Microseconds saved -> Estimated 0.0-0.5 us/frame in active surface-weather output; main win is deterministic cached dependency ownership, not measurable raw CPU.

Verification -> Targeted `rg` now finds `GlobalRegistry.Audio`/`GlobalRegistry.CelestialEngine` in `HectonSurfaceWeatherDirector` only in cold cache/editor assignment lines, not shader publish or thunder playback. `git diff --check` passed for the touched file and 13VOD docs with CRLF warnings only. Compile not launched: CPU measured 97%; `csc.exe` count was 0, but CPU exceeded the 50% build gate.

## 2026-05-27 - 13VOD Audit/Fix Pass 17 - Underwater GI relay authority cache
What was wrong -> `HectonUnderwaterVisuals.IsGIRelayAmbientAuthorityActive()` polled `GlobalRegistry.GIRelay` from underwater ambient/render-settings application paths.
What was done -> Added cached `IGIRelaySystem` ownership in `CacheRuntimeDependencies`, refreshed it on `GlobalRegistryServiceSlot.GIRelayRuntime`, and changed the authority check to read the cached field.
Cinematic Cheats used -> Underwater ambient remains a render-settings/shader presentation fake. No extra lighting simulation added.
Exact Microseconds saved -> Estimated 0.0-0.5 us/frame in active underwater visual frames; main win is removing hidden global dependency from lighting ownership.

Verification -> Targeted `rg` now finds `GlobalRegistry.GIRelay` in `HectonUnderwaterVisuals` only in the cold runtime dependency cache line. `git diff --check` passed for `HectonUnderwaterVisuals.cs` with CRLF warnings only. Compile still not launched under the CPU build gate.

## 2026-05-27 - 13VOD Audit/Fix Pass 18 - Underwater biome fog DataVault prewarm gate
What was wrong -> The underwater biome fog transition scheduler could call `EnsureBiomeFogBlendBuffers()` from `SlowTick`, and that helper could reach `GlobalDataVault.EnsureGenerationHandle`.
What was done -> Added `allowAcquire` to the buffer ensure path. Cold runtime dependency cache and DataVault hot-swap prewarm with acquisition allowed; the recurring scheduler now opens existing handles only and fails closed when buffers are missing.
Cinematic Cheats used -> Biome fog remains a cheap job-driven visual blend/fake; no physical fluid or volumetric simulation added.
Exact Microseconds saved -> 0.0 us steady-state claimed; removes an unmeasured DataVault allocation/generation hitch risk during biome transitions.

Verification -> `rg` confirms the recurring scheduler calls `EnsureBiomeFogBlendBuffers(allowAcquire: false)` while the `EnsureGenerationHandle` call remains behind the `allowAcquire: true` cold/hot-swap prewarm route. `git diff --check` passed for touched runtime files and 13VOD docs with CRLF warnings only. Re-extracted `CURRENT_BATCH.md`: still `NO_PROMPT_13VOD`. Compile not launched: CPU measured 100%; `csc.exe` count was 0, but CPU exceeded the 50% build gate.

## 2026-05-27 - 13VOD Audit/Fix Pass 19 - Crest depth-cache disabled/celestial route cleanup
What was wrong -> Disabled Crest runtime depth-cache sync still had recurring pending visual-sync work and a disabled-path `TryGetComponent` fallback. Tidal cache water-level modulation also read `GlobalRegistry.CelestialEngine` from the depth-cache population route.
What was done -> `SlowTick` now clears and exits when runtime depth-cache camera mode is disabled; `TryConfigureAndPopulate` exits before component resolution in disabled mode. Celestial engine is cached in cold runtime setup and refreshed from `CelestialEngineRuntime` hot-swap.
Cinematic Cheats used -> Kept runtime depth-cache camera disabled and retained the cheaper RenderGraph/authored-depth direction; no extra Crest capture simulation added.
Exact Microseconds saved -> Estimated 0.0-2.0 us/frame only when disabled depth-cache sync was pending; removes hidden component lookup and registry read from the visual-sync route.

Verification -> Targeted `rg` shows `GlobalRegistry.CelestialEngine` only in the cold cache method for this bootstrap, and the disabled depth-cache branch no longer contains `TryGetComponent`. `git diff --check` passed for the touched Crest bootstrap file with CRLF warnings only.

## 2026-05-27 - 13VOD Audit/Fix Pass 20 - Ocean registry read-accessor purity
What was wrong -> `HectonOceanRegistry.ActiveProvider` was a read property but could instantiate `OceanKinematicsRuntimeService` via `EnsureRuntimeInstance()` when the service route was missing.
What was done -> The getter now reads existing `GlobalRegistry.OceanKinematics` or an already registered runtime instance only. Missing bootstrap now returns null instead of creating a GameObject from a read accessor.
Cinematic Cheats used -> None. This is authority-route hygiene for ocean reads.
Exact Microseconds saved -> 0.0 us steady-state claimed; prevents a fault-path GameObject/AddComponent allocation and hidden global mutation from an ocean read.

Verification -> `rg` confirms `HectonOceanRegistry.ActiveProvider` no longer calls `EnsureRuntimeInstance()`. `git diff --check` passed for `HectonOceanRegistry.cs` with CRLF warnings only.
## 2026-05-27 - 13VOD Audit/Fix Pass 21 - Analytical flow direction fidelity
What was wrong -> `HectonFluidEngine` still snapped several authored water-force vectors to dominant axes: active thruster flow, abyssal vortex axis, cavitation burst direction, and cavitation shockwave radial direction. That is a harmful shortcut for gameplay-facing current/impulse vectors because diagonal water intent collapses into axis-aligned forces.
What was done -> Replaced those paths with finite scalar normalization and changed analytical thruster flow application to use normalized stored direction. Low-detail vector-noise axis snapping was left intact because that path is an explicit Math LOD/detail approximation, not an authored force direction.
Cinematic Cheats used -> Kept cheap vector-noise LOD and triangle/noise water fakes; rejected axis snapping for gameplay-facing event/current impulses where the player feels the force.
Exact Microseconds saved -> 0.0 us steady-state claimed. Event/setter paths add a few scalar ops when a burst/thruster/vortex is queued; runtime value is correctness, not CPU saving.

Verification -> Targeted `rg` confirms thruster/vortex/cavitation direction paths now call `NormalizeOrDefault` or `ResolveDirectionOrDefault`. Batch prompt re-extracted after Pass 21; result remains `NO_PROMPT_13VOD`.

## 2026-05-27 - 13VOD Audit/Fix Pass 22 - Submarine depressurization direction fidelity
What was wrong -> `SubmarineFluidDynamics` used dominant-axis snapping for breach suction and compartment breach probe direction. Internal water pressure should pull toward the actual breach vector; axis snapping made diagonal breaches resolve as cardinal-axis force.
What was done -> `ResolveDepressurizationAcceleration` now uses `SafeNormalize`, `SafeNormalize` now performs finite scalar normalization, and the unused dominant-axis helper was removed.
Cinematic Cheats used -> Kept the cheap compartment proxy and bounded contact collection. Rejected axis snapping for force direction because this is pressure gameplay truth, not a visual-only fake.
Exact Microseconds saved -> 0.0 us steady-state claimed. A few scalar ops are added on breach/pressure direction resolution; no managed allocation added.

Verification -> Targeted `rg` shows no remaining `DominantAxisOrDefault` call in `SubmarineFluidDynamics`; `git diff --check` passed for touched runtime/proof files with CRLF warnings only.

## 2026-05-27 - 13VOD Audit/Fix Pass 23 - Biome matrix runtime component-search guard
What was wrong -> `BiomeMatrixDirector.ResolveReferences()` could repair a missing player movement dependency with `playerTransform.TryGetComponent` during runtime slow tick. That hides broken `IPlayerRuntimeContext` wiring in a water/biome route.
What was done -> The component fallback now runs only when not playing. Runtime player movement comes from `IPlayerRuntimeContext.PlayerMovement` through cold cache/hot-swap.
Cinematic Cheats used -> None. This is dependency-route hygiene for biome/water evaluation.
Exact Microseconds saved -> Estimated 0.0-0.5 us per slow-tick only when DI is broken; primary win is no scene/component repair in runtime.

Verification -> Targeted `rg` shows the only remaining `TryGetComponent` in `BiomeMatrixDirector` is guarded by `!Application.isPlaying`; `git diff --check` passed for touched runtime/proof files with CRLF warnings only.

## 2026-05-27 - 13VOD Audit/Fix Pass 24 - Swim blockout rig single-driver cleanup
What was wrong -> `PlayerSwimBlockoutRig` registered as an updatable pose driver while `PlayerSwimPresentationController.LateFrameTick()` also forced `swimBlockoutRig.SyncFromPresentation(dt, true)`. The forced call bypassed the rig frame guard, so pose math could run twice per frame.
What was done -> Removed `ITickable/IUpdatable`, the `Tick()` method, and `TryRegisterUpdatable` ownership from the rig. The presentation controller is the sole pose driver; the rig only flushes queued renderer visibility in LateFrame.
Cinematic Cheats used -> Kept blockout rig as a cheap near-camera presentation fake; removed duplicate simulation-style ownership.
Exact Microseconds saved -> Estimated 2-8 us/frame while swim blockout rig is active; no managed allocation added.

Verification -> Targeted `rg` confirms no `ITickable`, `IUpdatable`, `Tick`, `TryRegisterUpdatable`, or `UnregisterUpdatable` remains in `PlayerSwimBlockoutRig`; the only pose drive call is `PlayerSwimPresentationController` late-frame sync. Batch prompt re-extracted after Pass 24; result remains `NO_PROMPT_13VOD`.

## 2026-05-27 - 13VOD Audit/Fix Pass 25 - Ocean adapter sample authority invariance
What was wrong -> `EmergencyMockOceanKinematicsAdapter` and `CrestOceanRuntimeAdapter` used `GlobalQualityWeight` to change ocean sample amplitude, detail waves, budget simplification, latency, and simplified status. That made `WaterHeight`, `SurfaceVelocity`, and `WaveNormal` depend on hardware quality.
What was done -> Removed quality-dependent wave math from both sample jobs. The interface parameter is explicitly discarded in these authority routes, and all devices now get the same deterministic fallback/deferred ocean sample output.
Cinematic Cheats used -> Kept the ocean fallback as a cheap analytical fake instead of real Crest readback/physical simulation. Rejected using the quality scalar to alter physics-facing water truth; quality must buy presentation elsewhere.
Exact Microseconds saved -> 0.0 us saving claimed. Low tier may spend a few extra scalar wave operations per scheduled fallback sample; accepted to remove device-dependent water authority.

Verification -> Targeted `rg` found no remaining `GlobalQualityWeight`, `RequestCount`, or `SimplifiedByQualityBudget` references in the two touched adapter files. `git diff --check` passed for those files with CRLF warnings only. Compile not launched: CPU later measured 14.41% but one `dotnet` process was already running; `csc.exe` count was 0.

## 2026-05-27 - 13VOD Audit/Fix Pass 26 - Ocean kinematics vault authority invariance
What was wrong -> `OceanKinematicsJobs` and `OceanKinematicsVaultRuntime` still used `GlobalQualityWeight` to select active wave octaves, sine/cosine polynomial fidelity, fallback active-octave counters, and macro-state hash input. That made water sample DTOs and rollback/sync identity device-quality-dependent.
What was done -> Analytical/mock wave jobs now use the configured max octave count and full cheap polynomial path for all devices. Active-octave resolution no longer accepts quality, and `ComputeMacroHash` no longer mixes `GlobalQualityWeight`.
Cinematic Cheats used -> Kept analytical wave evaluation as the cheap ocean fake. Rejected quality-scaled physics-facing wave truth; quality must buy presentation VFX/shader density outside the authority query path.
Exact Microseconds saved -> 0.0 us saving claimed. Low tier may pay extra scalar wave terms per scheduled query batch; accepted to keep ocean authority identical across hardware.

Verification -> Targeted `rg` found no remaining `math.lerp(1f, maxOctaves|octaveLimit)`, `SinPolynomial(phase, quality)`, `CosPolynomial(phase, quality)`, `AsUInt32(state.GlobalQualityWeight)`, or quality-fed `ResolveActiveOctaves` in the touched files. `git diff --check` passed for the touched runtime files with CRLF warnings only. Build gate cleared at CPU 21.2%, `csc.exe` 0, `dotnet` 0; `dotnet build Hecton8.Core.csproj --no-restore` failed because `Temp/CodexBuild/Unity.RenderPipelines.Core.Editor/Unity.RenderPipelines.Core.Editor.dll` and `Temp/CodexBuild/Unity.ShaderGraph.Editor/Unity.ShaderGraph.Editor.dll` are missing. Current solution/project generation also does not include the latest first-party Crest/Environment asmdef projects, so no green compile is claimed.

## 2026-05-27 - 13VOD Audit/Fix Pass 27 - Analytical buoyancy wave authority invariance
What was wrong -> `AnalyticalGerstnerWaveJobs` used `GlobalQualityWeight` inside the SHINOBU_263 authority wave lane: mock spectrum steepness/jitter, active octave budget, octave fade weights, and trig polynomial fidelity changed by device quality. That feeds `OceanSampleResultDTO.WaterHeight`, `SurfaceNormal`, and `Displacement`.
What was done -> Mock spectrum generation now uses one deterministic full cheap spectrum. Analytical wave evaluation uses configured max octaves, full octave weights, and quality-1 polynomial calls. `GlobalQualityWeight` remains in tuning/telemetry only.
Cinematic Cheats used -> Kept Gerstner analytical waves as the cheap buoyancy ocean fake. Rejected using quality to cheapen physics-facing water samples; quality should buy visuals outside this DTO lane.
Exact Microseconds saved -> 0.0 us saving claimed. Low tier may pay extra scalar octave/trig work per query batch; accepted to keep buoyancy water truth identical across hardware.

Verification -> `git diff --check` passed for `AnalyticalGerstnerWaveJobs.cs` with CRLF warnings only. Targeted `rg` confirmed no `BuildWave(..., q)`, quality-fed active-octave resolve, or `math.lerp(1f, maxLimit, q)` remains. The remaining `SinPolynomial(... quality)`/`CosPolynomial(... quality)` hits are the reusable method signatures, not authority call sites. Re-extracted `CURRENT_BATCH.md` after Pass 27; result remains `NO_PROMPT_13VOD`.

## 2026-05-27 - 13VOD Audit/Fix Pass 28 - SIMD buoyancy hydrodynamic force invariance
What was wrong -> `BuoyancySimdVectorization` used `GlobalQualityWeight` and `ApproximationQualityWeight` inside vectorized/scalar force jobs. Quality scaled turbulence acceleration and chose sine approximation fidelity, so `OutputForces` differed by hardware quality.
What was done -> `VectorizedHydrodynamicsJob`, `VectorizedHydrodynamicsLane4Job`, and `ScalarHydrodynamicsReferenceJob` now call `SinPolynomial(..., 1f, 7)` and apply authored turbulence amplitude directly.
Cinematic Cheats used -> Kept the cheap turbulence sine fake and SIMD hydrodynamic approximation. Rejected quality-scaled force truth; visual water wake/noise should consume quality instead.
Exact Microseconds saved -> 0.0 us saving claimed. Low tier may pay full seventh-order polynomial cost in this fake force lane; accepted for deterministic force output.

Verification -> `git diff --check` passed for `BuoyancySimdVectorization.cs` with CRLF warnings only. Targeted `rg` shows only the three force call sites as `SinPolynomial(phase, 1f, 7)` plus reusable approximator internals; no hydrodynamic force path remains with `float q`, `approximationWeight`, or `turbulenceAmplitude * q`.

## 2026-05-27 - 13VOD Audit/Fix Pass 29 - Async buoyancy readback authority invariance
What was wrong -> Async GPU/mock buoyancy readback used `GlobalQualityWeight` in the authority height lane: sample budget, smoothing alpha, dead-reckoning decay, shader active wave contribution, mock ripple amplitude, and CPU wave phase direction fidelity could vary by hardware quality. That feeds resolved buoyancy heights and request results.
What was done -> Added `AsyncBuoyancyReadbackConstants.AuthoritativeQualityWeight = 1f` and used it for the readback authority lane. Sample budget now resolves to max samples, smoothing/dead-reckoning use quality-1 constants, shader wave contribution is full, mock ripple amplitude is fixed, and CPU wave phase direction uses quality-1 trig.
Cinematic Cheats used -> Kept the async GPU/mock wave-height sampler as the cheap fake. Rejected using quality to reduce physics-facing height truth; visual ocean shaders/VFX should consume quality instead.
Exact Microseconds saved -> 0.0 us saving claimed. Low tier may pay max configured sample/wave cost in this async fake; accepted to keep buoyancy water truth identical across hardware.

Verification -> `git diff --check` passed for `AsyncBuoyancyReadbackContracts.cs`, `AsyncBuoyancyReadbackJobs.cs`, `AsyncBuoyancyReadbackRuntime.cs`, and `Hecton_WaveHeightSampler.compute` with CRLF warnings only. Targeted `rg` found no remaining quality-fed `ResolveSmoothingAlpha`, `ResolveSampleBudget`, `ResolveMockLocalHeight`, `ResolveShaderActiveWaveIndex`, `ResolveWavePhaseBases`, `WaveLaneDirection`, `SetFloat(OceanQualityId)`, shader wave contribution, or dead-reckoning decay path in the touched authority lane.

## 2026-05-27 - 13VOD Audit/Fix Pass 30 - Submarine ballast buoyancy authority invariance
What was wrong -> Submarine ballast buoyancy used `GlobalQualityWeight` to smooth/choose active sample budget, select mock fluid density, and derive the force-job fallback sample count. This changed submerged ratio and `SubmarineBallastForcePacketDTO.NetForce`, which is applied through `QueueAmbientForce`.
What was done -> Removed quality-driven ballast sample-budget smoothing and hysteresis, fixed the authority budget at four cheap probe points, removed the mock-displacement job quality field, and fixed mock density to the previous quality-1 value. `GlobalQualityWeight` stays in tuning/telemetry only.
Cinematic Cheats used -> Kept four-point ballast sampling and triangle mock swell as the cheap fake. Rejected quality-scaled force truth; quality should buy bubbles, hiss, wake, and slosh presentation instead.
Exact Microseconds saved -> 0.0 us saving claimed. Low tier may pay the full four-sample fake and high-density mock path; accepted for identical submarine buoyancy force across hardware.

Verification -> `git diff --check` passed for `SubmarineAutoLevelBallastController.cs` and `SubmarineBallastBuoyancyContracts.cs` with CRLF warnings only. Targeted `rg` found no remaining ballast sample-budget quality lerp, sample-budget hysteresis, mock-density `math.lerp(1015f, 1065f, q)`, or mock job `GlobalQualityWeight` field. Re-extracted `CURRENT_BATCH.md` after Pass 30; result remains `NO_PROMPT_13VOD`.

## 2026-05-27 - 13VOD Audit/Fix Pass 31 - Submarine 6DOF hydrodynamics authority invariance
What was wrong -> Submarine 6DOF movement still used `GlobalQualityWeight` in authority math: mock fluid density micro-layer bias, added-mass tensor blend, rotational damping, and quality cadence could alter solver skips, linear velocity, angular velocity, and rotation.
What was done -> Added `SubmarineDynamicsConstants.AuthoritativeQualityWeight = 1f`, routed density/tensor/damping authority math through that value, and replaced quality-derived integrator cadence with full authority cadence. Quality remains in DTO/job fields for telemetry and presentation inputs.
Cinematic Cheats used -> Kept analytical added-mass, four-vector fake density, and dead-reckoning support. Rejected quality-driven solver skip/tensor fallback for physics truth; use visual wake/cavitation/interior shake for scalability instead.
Exact Microseconds saved -> 0.0 us saving claimed. Low tier may pay full authority cadence/tensor path; accepted for identical hull movement across devices.

Verification -> `git diff --check` passed for `SubmarineDynamicsContracts.cs` with CRLF warnings only. Targeted `rg` found no remaining `ResolveQualityUpdateFraction`, no `math.lerp(0.25f, 1f, ...)` quality cadence, and no local `float quality = math.saturate(math.isfinite(globalQualityWeight)` in density/tensor/damping helpers.

## 2026-05-27 - 13VOD Audit/Fix Pass 32 - Submarine mock flood signal authority invariance
What was wrong -> `SubmarineDynamicsRuntime.TryPushMockFloodSignal` used `GlobalQualityWeight` to alter fallback flood signal probability from 1/96 to 1/16. With `enableMockSignals` active, that changes flood mass and therefore submarine movement state.
What was done -> Removed the quality parameter from the mock flood signal generator and fixed probability to the previous quality-1 value. Quality reads remain only for job metadata and telemetry stride.
Cinematic Cheats used -> Kept deterministic mock flood injection as a cheap test/fallback fake. Rejected quality-scaled water mass events.
Exact Microseconds saved -> 0.0 us saving claimed. Low tier gets the same fallback event cadence as high/ultra; no allocation or route changes.

Verification -> `git diff --check` passed for `SubmarineDynamicsRuntime.cs` with CRLF warnings only. Targeted `rg` shows `TryPushMockFloodSignal(frame)` has no quality parameter and the old `math.lerp(1f / 96f, 1f / 16f, curved)` probability path is gone.

## 2026-05-27 - 13VOD Audit/Fix Pass 33 - Airlock pressure/water authority invariance
What was wrong -> `AirlockPressurization` used `GlobalQualityWeight` to alter the simulation tick interval and pump pressure equalization speed. That made water volume, pressure, collision blocking, stress/flood outcomes, and state hashes quality-dependent.
What was done -> Added `AirlockPressurizationConstants.AuthoritativeQualityWeight` and `ResolveAuthorityTickInterval()`. Runtime scheduling and telemetry tick reporting use the authority interval; pump pressure equalization uses the authority scalar. `GlobalQualityWeight` still drives bubble and acoustic cadence only.
Cinematic Cheats used -> Kept the Torricelli/equalization airlock model as a cheap water/pressure fake. Rejected quality-scaled authority cadence; quality buys bubbles, fog, audio density, and editor metadata instead.
Exact Microseconds saved -> 0.0 us saving claimed. Low tier may pay full authority cadence; no allocation, DTO layout, DataVault lane, or SignalBus route changes.

Verification -> `git diff --check` passed for `AirlockPressurizationContracts.cs`, `AirlockPressurizationRuntime.cs`, `AirlockPressurizationJobs.cs`, and `Editor/AirlockPressurizationEditor.cs` with CRLF warnings only. Targeted `rg` shows authority cadence uses `ResolveAuthorityTickInterval()`, pump pressure uses `AuthoritativeQualityWeight`, and quality remains in the job only as `visualQuality` for VFX/audio cadence. Re-extracted `CURRENT_BATCH.md` after Pass 33; result remains `NO_PROMPT_13VOD`.

## 2026-05-27 - 13VOD Audit/Fix Pass 34 - Abyssal cavitation force authority invariance
What was wrong -> `AbyssalCavitationRuntime` used `GlobalQualityWeight` in force authority: mock shockwave radius/pressure/speed, force candidate acceptance, noncritical radius scale, shell width, SDF ray dampening, and SDF interpolation could vary by device quality.
What was done -> Added `AbyssalCavitationConstants.AuthoritativeQualityWeight = 1f`, removed quality from the mock detonation job contract, and routed cavitation force/SDF authority math through the constant. `GlobalQualityWeight` still drives visual shader sphere quality metadata and visual upload limits.
Cinematic Cheats used -> Kept the cheap shockwave and voxel/analytical SDF fake. Rejected using quality to cheapen physics-facing cavitation impulses; quality should buy visual rings, foam, bubbles, audio, and post effects instead.
Exact Microseconds saved -> 0.0 us saving claimed. Low tier may pay full cheap SDF/multi-tap/mock shockwave math; accepted for identical cavitation force packets across hardware.

Verification -> Targeted `rg` confirms mock and force authority paths use `AbyssalCavitationConstants.AuthoritativeQualityWeight`; the remaining `Smooth01(Tuning.GlobalQualityWeight)` hit is in `UpdateCavityShaderParamsJob` visual output. `git diff --check` passed for cavitation files with CRLF warnings only.

## 2026-05-27 - 13VOD Audit/Fix Pass 35 - Vehicle damage flood/buoyancy authority invariance
What was wrong -> `VehicleComponentDamage` used `GlobalQualityWeight` in submarine-adjacent authority: mock damage signal count/shape, explosive propagation radius cap, and fire probability changed by device quality. Those paths feed flood mass, buoyancy scalar, hazard signals, and state hash.
What was done -> Added `VehicleDamageConstants.AuthoritativeQualityWeight = 1f`, removed quality fields from mock/reduction jobs, fixed mock count to the authored max, and kept quality only as `VehicleDamageStateDTO.QualityWeight` metadata in the evaluator.
Cinematic Cheats used -> Kept grid damage, mock impacts, and hazard signals as cheap fakes. Rejected quality-scaled damage/flood truth; quality should buy hull decals, leaks, sparks, alarms, and waterline visuals.
Exact Microseconds saved -> 0.0 us saving claimed. Low tier may pay full mock signal count and six-cell propagation cap; accepted for identical flood/buoyancy state across hardware.

Verification -> Targeted `rg` shows no `GlobalQualityWeight` fields remain in `GenerateMockVehicleDamageJob` or `ApplyVehicleDamageReductionJob`, no quality-fed mock count remains, and `EvaluateVehicleSystemsJob` uses `AuthoritativeQualityWeight` for fire probability while keeping visual quality only in state metadata. `git diff --check` passed for vehicle damage files with CRLF warnings only.

## 2026-05-27 - 13VOD Audit/Fix Pass 36 - Habitat flood solver authority invariance
What was wrong -> `HabitatFluidIncursionDirector` used `GlobalQualityWeight` for flood solver cadence, BFS node budget, and solver iteration count. That made compartment water volume, pressure equalization, dynamic flood mass, center of mass, and flood signals device-quality-dependent.
What was done -> Solver cadence now uses `ResolveAuthoritySolverWindowSeconds()`, BFS uses `ResolveAuthorityBfsNodeBudget()`, and tuning uses `ResolveAuthoritySolverIterations()`. Removed the unused quality field from `FluidIngressJob`; quality remains only for waterline wobble/metadata.
Cinematic Cheats used -> Kept the scalar compartment graph and quantized milliliter transfer fake. Rejected quality-scaled graph solve; quality should buy visual waterlines, leak particles, acoustic muffling, and diagnostics.
Exact Microseconds saved -> 0.0 us saving claimed. Low tier may pay full cadence/max BFS/max iteration budget; accepted for identical internal water state across hardware.

Verification -> Targeted `rg` shows authority helpers drive solver window, BFS budget, and solver iterations; remaining `GlobalQualityWeight` in this path feeds `FluidWaterlineMassSummaryJob` visual wobble and tuning metadata. `git diff --check` passed for habitat fluid files with CRLF warnings only. Re-extracted `CURRENT_BATCH.md` after Pass 36; result remains `NO_PROMPT_13VOD`.

## 2026-05-27 - 13VOD Audit/Fix Pass 37 - Sump pump drainage cadence authority invariance
What was wrong -> `SumpPumpPipeGridRuntime.SlowTick()` used `GlobalQualityWeight` to decide drainage solve cadence. That changes pump evacuation timing, quantized pump remainder/mass error, telemetry state timing, and room drainage outcomes by device quality.
What was done -> Added `SumpPumpPipeGridConstants.AuthoritativeQualityWeight = 1f` and changed slow-tick cadence to `ResolveAuthoritySolveCadenceSeconds()`. Runtime quality remains available for tuning telemetry and visual flow publication only.
Cinematic Cheats used -> Kept the cheap drainage graph and visual flow publication fake. Rejected quality-scaled pump authority timing; quality should buy flow ribbons, pump audio, spray, and UI readouts.
Exact Microseconds saved -> 0.0 us saving claimed. Low tier may pay the same 0.1 s cadence as high/ultra; accepted for identical sump drainage timing.

Verification -> Targeted `rg` shows solver cadence now uses `ResolveAuthoritySolveCadenceSeconds()` and `SumpPumpPipeGridConstants.AuthoritativeQualityWeight`; remaining `GlobalQualityWeight` hits in sump drainage are tuning/telemetry or visual flow publication budget. `git diff --check` passed for sump pump files with CRLF warnings only.
## Pass 38 - Bulkhead/Hatch Pressure Cadence Authority Invariance
What was wrong: bulkhead closure cadence and hatch pressure-lock tick interval were device-quality dependent. Hatch slam acoustic signal volume also used `GlobalQualityWeight` before publishing to `SignalBus<MovementAcousticSignal>`.
What was done: routed bulkhead cadence through explicit authority quality, added hatch authority tick resolver, used authority interval for hatch pressure accumulation/tuning, and passed an acoustic authority weight to `UpdateHatchFsmJob`.
Cinematic Cheats used: kept the cheap graph/pressure fake; quality must buy hatch/water presentation, not alternate containment timing.
Exact Microseconds saved: 0.0 us claimed. Low-tier may pay the full authority cadence; the gain is identical pressure/containment truth across devices.

## 2026-05-27 - 13VOD Audit/Fix Pass 39 - Shinobu ocean surface readback authority invariance
What was wrong -> `ShinobuOceanSurfaceAtmosphereRuntime` is an active `IHectonOceanKinematics` provider, but readback wave time, sample budget, active wave count, compute quality, and telemetry hash used `GlobalQualityWeight`.
What was done -> Added `OceanSurfaceAtmosphereConstants.AuthoritativeQualityWeight = 1f` and routed the readback/sample authority lane through it. Visual shader LOD still consumes live quality.
Cinematic Cheats used -> Kept async GPU readback as the cheap water-height fake. Rejected device-quality-dependent sample truth; quality should buy visual ocean density, foam, spray, caustics, and atmosphere.
Exact Microseconds saved -> 0.0 us claimed. Low tier may pay max readback budget/full wave count; accepted for identical `WaterHeight`/`WaveNormal` behavior.

Verification -> Targeted `rg` shows readback budget, readback compute quality, active readback wave count, wave evaluation time, and telemetry hash now use authority quality. `git diff --check` passed for ocean surface atmosphere files with CRLF warnings only. Re-extracted `CURRENT_BATCH.md` after Pass 39; result remains `NO_PROMPT_13VOD`.

## 2026-05-27 - 13VOD Audit/Fix Pass 40 - BuoyancyObject ground SDF probe authority invariance
What was wrong -> `BuoyancyObject.ResolveGroundSdfStepMeters` used `HomeostasisBrain.GlobalQualityWeight` to choose SDF ray step size. That path gates above-ground/island-grounded buoyancy suppression, so low-tier quality could miss or shift ground hits and produce different buoyancy enablement.
What was done -> Added explicit `AuthoritativeQualityWeight = 1f` and routed SDF step selection through it. The existing SDF/terrain fake and cadence remain; only the quality source changed from presentation quality to authority quality.
Cinematic Cheats used -> Kept the cheap SDF probe instead of PhysX casts or physical shore interaction. Rejected quality-scaled probe truth; quality should buy contact foam, splash, wake detail, and diagnostics.
Exact Microseconds saved -> 0.0 us claimed. Low tier may pay fine SDF step on the configured ground-check cadence; accepted for identical buoyancy suppression across devices.

Verification -> Targeted `rg` shows `ResolveGroundSdfStepMeters` uses `AuthoritativeQualityWeight` and no longer reads `HomeostasisBrain.GlobalQualityWeight`. `git diff --check` passed for `BuoyancyObject.cs` with CRLF warnings only.

## 2026-05-27 - 13VOD Audit/Fix Pass 41 - Submarine autopilot SDF/flow authority invariance
What was wrong -> `SubmarineAutopilotSdfNavigator` used live quality for solver cadence, feeler count, SDF ray steps, SDF interpolation/gradient, and flow-field interpolation. This changed avoidance and `DesiredVelocity` across devices.
What was done -> Added an explicit autopilot authority quality constant. `ResolvedQualityWeight` is now fixed to authority quality, jobs receive that constant, the scheduler uses authority cadence, and the stale live-quality resolver was removed from the file.
Cinematic Cheats used -> Kept the SDF and precomputed flow-field fakes. Rejected quality-scaled autopilot truth; quality should buy sonar, wake, thruster, UI, and diagnostic presentation.
Exact Microseconds saved -> 0.0 us claimed. Low tier may pay full cheap autopilot SDF/flow cost; accepted for identical steering state.

Verification -> Targeted `rg` shows no `HomeostasisBrain.GlobalQualityWeight`, `MathLodRuntimeConfig`, `ResolveRuntimeQualityWeight`, or `ResolveSchedulingQualityWeight` remains in `SubmarineAutopilotSdfNavigator.cs`; authority jobs receive `tuning.ResolvedQualityWeight`, which is assigned from `SubmarineAutopilotConstants.AuthoritativeQualityWeight`. `git diff --check` passed for the file with CRLF warnings only.

## 2026-05-27 - 13VOD Audit/Fix Pass 42 - Seaglide metabolism cadence authority invariance
What was wrong -> Seaglide battery metabolism cadence used live quality. That changed when `ProcessSeaglideMetabolismJob` drains `BatteryLevel`, so low/high settings could diverge in gameplay resource state.
What was done -> `AdvanceMetabolismCadence` now uses `SeaglideSimdMath.AuthoritativeQualityWeight`; `ResolvedQualityWeight` records authority quality while live `GlobalQualityWeight` remains for telemetry and cavitation/presentation.
Cinematic Cheats used -> Kept the cadence-throttled metabolism fake. Rejected quality-scaled battery truth; quality buys bubbles, wake, motor audio, and HUD feedback.
Exact Microseconds saved -> 0.0 us claimed. Low tier may pay min metabolism cadence; accepted for identical seaglide battery state.

Verification -> Targeted `rg` shows `AdvanceMetabolismCadence(solverDelta, tuningDto)`, cadence lerps with `SeaglideSimdMath.AuthoritativeQualityWeight`, and `ResolvedQualityWeight` assignments use authority quality. `git diff --check` passed for `SeaglideHydrodynamicsRuntime.cs` with CRLF warnings only. Re-extracted `CURRENT_BATCH.md` after Pass 42; result remains `NO_PROMPT_13VOD`.

## 2026-05-27 - 13VOD Audit/Fix Pass 43 - Hydrodynamic KCC SDF collision authority invariance
What was wrong -> `HydrodynamicKccRuntime` still let live quality choose SDF collision interpolation and speculative probe count in the water KCC lane. That can change collision hits, penetration, slide response, and blackbox compute/turbulence telemetry by device quality.
What was done -> `BuildSdfCollisionHitsJob` now uses `HydrodynamicKccMath.AuthoritativeQualityWeight` for SDF blend and sample count. KCC iteration/sample resolvers ignore presentation quality for authority math, and the telemetry aggregate estimates work with the same authority constant.
Cinematic Cheats used -> Kept the DataVault SDF grid fake. Rejected low-tier nearest/probe reduction inside collision truth; quality should buy wake, foam, sonar/UI overlays, and visual smoothing.
Exact Microseconds saved -> 0.0 us claimed. Low tier may pay max cheap SDF probe stride and trilinear blend; accepted for identical swim/KCC collision behavior.

Verification -> Targeted `rg` shows SDF collision and KCC telemetry use `HydrodynamicKccMath.AuthoritativeQualityWeight`; remaining live quality in the file is visual smoothing, signal metadata, or tuning storage. `git diff --check` passed for `HydrodynamicKccRuntime.cs` with CRLF warnings only.

## 2026-05-27 - 13VOD Audit/Fix Pass 44 - Hydrodynamic KCC hot DataVault acquire gate
What was wrong -> `HydrodynamicKccRuntime.FixedTick()` and `LateFrameTick()` called `EnsureVaultBuffers()`, which could call `vault.EnsureGenerationHandle<T>()`. A missing movement buffer could allocate or generate DataVault lanes from recurring KCC phases.
What was done -> Added `allowAcquire` to `EnsureVaultBuffers`. OnEnable, DataVault hot-swap, and editor/profile ingestion keep acquisition; FixedTick and LateFrameTick now pass `allowAcquire: false` and fail closed unless the cold path already created all lanes.
Cinematic Cheats used -> Kept the native DataVault SDF/KCC fake. Rejected runtime heap repair from movement; boot/hot-swap owns lane acquisition.
Exact Microseconds saved -> 0.0 us steady-state claimed. Prevents allocation/generation stalls on low silicon when KCC boot wiring is broken.

Verification -> Targeted `rg` shows recurring `FixedTick` and `LateFrameTick` use `EnsureVaultBuffers(allowAcquire: false)`, while `EnsureGenerationHandle<T>` remains reachable only through the acquire-enabled path. `git diff --check` passed for `HydrodynamicKccRuntime.cs` with CRLF warnings only.

## 2026-05-27 - 13VOD Audit/Fix Pass 45 - Shoreline foam visual-sync allocation gate
What was wrong -> `ShorelineFoamGraftRuntime.VisualSyncTick` could call `vault.EnsureGenerationHandle<T>()` and create `GraphicsBuffer` pairs from the recurring ocean visual-sync lane. Missing shoreline foam state therefore had a hidden DataVault/GPU allocation repair path.
What was done -> Added `ShorelineFoamGraftRuntime.EnsureColdState` and prewarmed it from `OceanSinglePassRuntime.EnsureVaultState`. Visual sync now uses `allowAcquire: false`, adopts existing handles only, skips CSV/seed work, and requires already-valid GPU buffers.
Cinematic Cheats used -> Kept shoreline foam graft as a cheap screen/ocean visual fake. Rejected runtime heap repair; cold prewarm owns foam lanes and GPU buffers.
Exact Microseconds saved -> 0.0 us steady-state claimed. Prevents unmeasured allocation stalls on weak devices if shoreline foam boot wiring is incomplete or GPU buffers are lost.

Verification -> Targeted `rg` shows `VisualSyncTick` calls `EnsureVaultState(... allowAcquire: false)` and no longer calls `EnsureGpuBuffersCold`; `EnsureGenerationHandle<T>` remains behind `allowAcquire` and `vault.IsAllocationLocked` checks. `git diff --check` passed for ocean single-pass files with CRLF warnings only. Re-extracted `CURRENT_BATCH.md` after Pass 45; result remains `NO_PROMPT_13VOD`.

## 2026-05-27 - 13VOD Audit/Fix Pass 46 - Abyssal fluid decal direction/allocation cleanup
What was wrong -> `AbyssalFluidDecalManager` used dominant-axis direction quantization for pressure sprays and voxel cave-in dust drift. Its fallback mesh draw path could also create a legacy `MaterialPropertyBlock` during draw if the cold-owned block was missing.
What was done -> Replaced axis snapping with finite vector normalization and removed hot `GetOrCreateLegacyBlock` calls from draw functions. MPB acquisition remains in Awake/OnEnable.
Cinematic Cheats used -> Kept the cheap quad/decal fluid aftermath fake. Rejected taxicab spray/dust direction and hot rendering-state repair.
Exact Microseconds saved -> 0.0-0.2 us/frame only in broken fallback draw state. Main gain is cleaner water-fluid motion with no hot allocation repair.

Verification -> Targeted `rg` shows no `DominantAxisOrDefault` remains in `AbyssalFluidDecalManager.cs`; `GetOrCreateLegacyBlock` remains only in Awake/OnEnable. `git diff --check` passed for the file with CRLF warnings only.

## 2026-05-27 - 13VOD Audit/Fix Pass 47 - Sargassum Crest facade hot RT allocation gate
What was wrong -> `SargassumCrestDampingController.LateFrameTick` could refresh facade textures and allocate or resize `RenderTexture` targets when a density field appeared late or changed dimensions.
What was done -> Added an allocation gate to facade refresh/resource creation. Awake/OnEnable and Sargassum hot-swap can allocate; recurring LateFrame refresh passes `allowAllocate: false` and fails closed by disabling facade globals if the prewarmed RTs do not match.
Cinematic Cheats used -> Kept the first-party sargassum facade as the cheap ocean-damping/oil-film fake. Rejected hot GPU allocation repair and still avoided touching Crest materials.
Exact Microseconds saved -> 0.0 us steady-state claimed. Prevents unmeasured `RenderTexture` allocation stalls on weak devices during late-ready or resized sargassum fields.

Verification -> Targeted `rg` shows LateFrame uses `RefreshFacadeTextures(... allowAllocate: false)`, while Awake/OnEnable/hot-swap use `allowAllocate: true`. `git diff --check` passed for `SargassumCrestDampingController.cs` with CRLF warnings only.

## 2026-05-27 - 13VOD Audit/Fix Pass 48 - Fluid dynamic wake payload hot acquire gate
What was wrong -> `HectonFluidEngine.TryGetDynamicWakeGpuPayload` could allocate dynamic wake GraphicsBuffers and acquire DataVault wake lanes from a render payload read path.
What was done -> Prewarmed dynamic wake GPU buffers and DataVault handles in Awake, OnEnable, and DataVault hot-swap. The payload path now requires `AreDynamicWakeGpuBuffersReady()` and calls `TryResolveDynamicWakeVaultBuffers(... allowAllocate: false)`.
Cinematic Cheats used -> Kept dynamic wake advection as a cheap VFX fake. Rejected render-payload heap repair; cold/hot-swap owns the buffers.
Exact Microseconds saved -> 0.0 us steady-state claimed. Prevents unmeasured GraphicsBuffer/DataVault allocation stalls when dynamic wake state is missing.

Verification -> Targeted `rg` shows payload read uses `allowAllocate: false`, while Awake/OnEnable/DataVault hot-swap prewarm with `allowAllocate: true`. `git diff --check` passed for `HectonFluidEngine.cs` with CRLF warnings only. Re-extracted `CURRENT_BATCH.md` after Pass 48; result remains `NO_PROMPT_13VOD`.

## 2026-05-27 - 13VOD Audit/Fix Pass 49 - Shared Gerstner wave publish acquire gate
What was wrong -> `HectonFluidEngine.PublishGerstnerWaveDataVault` could acquire shared `OceanGerstnerWaves` and `OceanGerstnerWaveMeta` DataVault lanes from the recurring wave publication path whenever the vault was not allocation-locked.
What was done -> Publish now opens existing handles only through `TryOpenExistingFluidVaultBuffer` and fails closed if the cold prewarm path did not prepare the lanes. `EnsureSharedGerstnerDataVaultBuffers` remains the owner-side allocator.
Cinematic Cheats used -> Kept shared Gerstner wave DTOs as the cheap ocean/buoyancy wave fake. Rejected hot DataVault lane repair; boot/resize prewarm owns the buffer route.
Exact Microseconds saved -> 0.0 us steady-state claimed. Prevents unmeasured `EnsureGenerationHandle` stalls during wave publication on weak devices when shared lanes are missing.

Verification -> Targeted `rg` shows `PublishGerstnerWaveDataVault` uses `TryOpenExistingFluidVaultBuffer` only, while `OpenOrAcquireFluidVaultBuffer` remains in `EnsureSharedGerstnerDataVaultBuffers`. `git diff --check` passed for `HectonFluidEngine.cs` and 13VOD protocol docs with CRLF warning only.

## 2026-05-27 - 13VOD Audit/Fix Pass 50 - Fluid advection visual-state allocation gate
What was wrong -> `HectonFluidEngine.LateFrameTick` could create fluid advection native buffers, GPU buffers, a fallback `Texture3D`, and `RTHandle` state through `EnsureFluidAdvectionVisualState`. Splashdown/bubble/debris event paths could also repair missing native state.
What was done -> Added `allowAllocate` to fluid advection native/GPU/empty-texture ensure methods. Cold setup and DataVault hot-swap allocate; LateFrame and particle event drains use existing state only and fail closed when prewarm is missing.
Cinematic Cheats used -> Kept bounded fluid particle advection as the cheap cinematic water/debris/bubble fake. Rejected recurring GPU/native/texture repair inside visual sync.
Exact Microseconds saved -> 0.0 us steady-state claimed. Prevents unmeasured Native/DataVault/GraphicsBuffer/Texture3D/RTHandle stalls on weak devices when advection state is missing.

Verification -> Targeted `rg` shows `EnsureFluidAdvectionVisualState(allowAllocate: false)` in `LateFrameTick`, `EnsureFluidAdvectionState(allowAllocate: false)` in bubble/splashdown/debris event paths, and acquire-enabled prewarm only in Awake/OnEnable/DataVault hot-swap. `git diff --check` passed for `HectonFluidEngine.cs` and 13VOD protocol docs with CRLF warning only.

## 2026-05-27 - 13VOD Audit/Fix Pass 51 - Fluid advection RenderGraph texture-handle allocation gate
What was wrong -> `TryBuildFluidAdvectionRenderGraphPayload` could allocate `RTHandle` wrappers for abyssal flow or voxel SDF textures while building the render payload.
What was done -> Payload build resolves flow/SDF handles with `allowAllocate:false`; missing handles fall back to the owned empty texture and disable texture/SDF flags instead of allocating.
Cinematic Cheats used -> Kept empty texture fallback as the visual fake. Rejected RenderGraph payload-side RTHandle repair; prewarmed handles own rich flow/SDF advection.
Exact Microseconds saved -> 0.0 us steady-state claimed. Prevents unmeasured RTHandle allocation stalls during render pass recording.

Verification -> Targeted `rg` shows payload build calls both flow/SDF handle resolvers with `allowAllocate:false`, clears `hasFlowTexture`/`sdfActive` on missing handles, and only retains RTHandle allocation behind acquire-enabled resolver paths. `git diff --check` passed for `HectonFluidEngine.cs` and 13VOD protocol docs with CRLF warning only. Re-extracted `CURRENT_BATCH.md` after Pass 51; result remains `NO_PROMPT_13VOD`.

## 2026-05-27 - 13VOD Audit/Fix Pass 52 - Dynamic wake payload owner-phase upload and RG declaration
What was wrong -> `TryGetDynamicWakeGpuPayload` was a read-model getter but performed two GPU uploads, changed active wake buffers, and flipped the ping-pong index. The fluid advection RenderGraph pass also bound dynamic wake buffers without declaring them as read resources.
What was done -> Moved dynamic wake upload/state flip into `LateFrameTick` owner phase via `RefreshDynamicWakeGpuPayload`, gated by advection readiness and active particle count. The getter now returns cached buffers/params only. RenderGraph now imports `DynamicWakeBuffer` and `DynamicWakeVectorBuffer` and declares them with `UseBuffer(..., AccessFlags.Read)`.
Cinematic Cheats used -> Kept empty-buffer fallback for no-wake/no-particle frames. Rejected hidden getter-side upload and undeclared external-buffer binding.
Exact Microseconds saved -> 0.0 us steady-state claimed. Prevents redundant wake upload work when no advection particles are active and removes an unmeasured RenderGraph hazard/lifetime ambiguity.

Verification -> Targeted `rg` shows `LateFrameTick` owns `RefreshDynamicWakeGpuPayload`, `TryGetDynamicWakeGpuPayload` reads `_activeDynamicWakeParams` only, and the RenderGraph pass declares both dynamic wake buffers. `git diff --check` passed for `HectonFluidEngine.cs` and `HectonFluidAdvectionRenderFeature.cs` with CRLF warnings only. `dotnet build` was not launched because CPU gate reported 100% and existing `dotnet` processes were running.

## 2026-05-27 - 13VOD Audit/Fix Pass 53 - Giant wake/current torque direction fidelity
What was wrong -> Giant wake current and buoyancy job flow/shear torque axes collapsed diagonal water motion to dominant axes, and those axes contribute to `resultTorques` rather than only VFX.
What was done -> Replaced giant wake direction, gyroscopic current torque axis, standard/wake shear axes, and shear cross axis with finite normalization. Left the explicit low-detail surface-normal dominant-axis LOD untouched.
Cinematic Cheats used -> Kept the cheap analytical current/torque fake. Rejected taxicab current torque in authority output; retained axis snapping only where it is a declared low-detail normal/vector-noise approximation.
Exact Microseconds saved -> 0.0 us saving claimed. Adds a few scalar ops/rsqrt calls per buoyancy row for diagonal-current correctness.

Verification -> Targeted `rg` shows the current/torque routes now call `NormalizeOrDefault`, while `DominantAxisOrDefault` remains only in helper definitions, explicit surface-normal LOD, and known vector-noise low-sample presentation. `git diff --check` passed for `HectonFluidEngine.cs` with CRLF warning only.

## 2026-05-27 - 13VOD Audit/Fix Pass 54 - Splashdown impulse event allocation gate
What was wrong -> `ScheduleSplashdownImpulseField` could allocate splashdown impulse native/DataVault state from an event path, and `UploadSplashdownImpulseBuffer` could create ping-pong GPU buffers during LateFrame job completion.
What was done -> Added `allowAllocate` gates to splashdown impulse native and GPU ensures. Awake/OnEnable/DataVault hot-swap prewarm the state; scheduling and upload completion use existing buffers only and fail closed if prewarm is absent.
Cinematic Cheats used -> Kept the bounded splashdown vector-field impulse as a cinematic fake. Rejected first-impact heap repair; bubble-ring fallback still survives when rich buffers are unavailable.
Exact Microseconds saved -> 0.0 us steady-state claimed. Prevents unmeasured native/GPU allocation stalls during prologue ocean handoff splashdown.

Verification -> Targeted `rg` shows cold paths call `EnsureSplashdownImpulseState/GpuBuffer(... allowAllocate: true)`, while event scheduling and upload completion use `allowAllocate:false`. `git diff --check` passed for `HectonFluidEngine.cs` with CRLF warning only. Re-extracted `CURRENT_BATCH.md` after Pass 54; result remains `NO_PROMPT_13VOD`.

## 2026-05-27 - 13VOD Audit/Fix Pass 55 - Fluid advection RenderGraph dispatch contract naming
What was wrong -> The fluid advection RenderGraph route was labeled `ReadModel`, but its payload method legitimately consumes a queued dispatch, clears the queued flag, applies pending shift, and flips ping-pong parity. That is not a pure read accessor.
What was done -> Renamed the contract to `IFluidAdvectionRenderGraphDispatchSource` and the method to `TryClaimFluidAdvectionRenderGraphPayload`; updated `GlobalRegistry`, slot type mapping, `HectonFluidEngine`, and the render feature.
Cinematic Cheats used -> None. This is contract hygiene for the existing bounded GPU fluid advection fake.
Exact Microseconds saved -> 0.0 us. Behavior is unchanged; the win is preventing future misuse of a mutable one-shot route as a read model.

Verification -> Targeted `rg` found no remaining `IFluidAdvectionRenderGraphReadModel` or `TryBuildFluidAdvectionRenderGraphPayload`; all active call sites use `IFluidAdvectionRenderGraphDispatchSource` and `TryClaimFluidAdvectionRenderGraphPayload`. `git diff --check` passed for the contract, registry, engine, and render feature with CRLF warnings only.

## 2026-05-27 - 13VOD Audit/Fix Pass 56 - Maelstrom/submarine wake hot registry fallback removal
What was wrong -> Maelstrom damage and submarine wake payload routes could call `RefreshRuntimeActorContextsIfMissing`, which reads `GlobalRegistry.Player/Submarine` from recurring water hazard/wake code if cached contexts were missing.
What was done -> Added cached-context accessors that only validate/null stale Unity objects. Hot paths now use cached player/submarine contexts; cold setup and hot-swap remain the only registry routes.
Cinematic Cheats used -> None. This is route ownership cleanup around maelstrom damage and wake payload generation.
Exact Microseconds saved -> 0.0-0.5 us/frame only in missing/destroyed-context cases; primary gain is no hidden registry polling.

Verification -> Targeted `rg` shows `RefreshRuntimeActorContextsIfMissing` remains only in cold setup, while maelstrom damage and submarine wake payload use `TryGetCachedPlayerRuntime` / `TryGetCachedSubmarineRuntime`. `git diff --check` passed for `HectonFluidEngine.cs` with CRLF warning only.

## 2026-05-27 - 13VOD Audit/Fix Pass 57 - Abyssal fluid decal event resource repair gate
What was wrong -> Public fluid aftermath sinks could call `EnsureRenderingResources(true)` from event paths and repair quad/material state there, including `Resources.GetBuiltinResource` if the shared quad was missing.
What was done -> Added `IsPresentationReady()` and made cable fluid, rupture fluid, pressure spray, seismic dust, voxel cave-in dust, wake silt, and water splash entry points fail closed unless cold-owned storage/rendering resources are already valid.
Cinematic Cheats used -> Kept capped quad/decal/spray fake. Rejected event-side rendering resource repair.
Exact Microseconds saved -> 0.0 us steady-state claimed. Prevents unmeasured resource lookup/repair spikes when VFX setup is incomplete.

Verification -> Targeted `rg` shows no public event registration entry still calls `EnsureRenderingResources(true)`; all use `IsPresentationReady()`. `git diff --check` passed for `AbyssalFluidDecalManager.cs` with CRLF warning only. Re-extracted `CURRENT_BATCH.md` after Pass 57; result remains `NO_PROMPT_13VOD`.

## 2026-05-27 - 13VOD Audit/Fix Pass 58 - Buoyancy native capacity owner-phase gate
What was wrong -> `HectonFluidEngine.FixedTick` could grow buoyancy Native/DataVault capacity, and idle zero-object paths released the same buffers, making next registration capable of causing physics-lane allocation churn.
What was done -> Added prewarmed buoyancy capacity in Awake/OnEnable/DataVault hot-swap, changed buoyancy registration to return success only when capacity is ready, retained runtime idle buffers until engine teardown, and made FixedTick use `allowAllocate:false` capacity validation.
Cinematic Cheats used -> None. This protects the existing cheap analytical buoyancy path; visual budgets remain separate.
Exact Microseconds saved -> 0.0 us steady-state claimed. Prevents unmeasured native/DataVault/GPU allocation stalls on first object after idle or on over-capacity registration.

Verification -> Targeted `rg` shows no `ReallocateNativeArrays(count)` or `count > _nativeCapacity` fixed-tick growth path remains; `Register(BuoyancyObject)` now returns bool and `BuoyancyObject` retries after Start/DataVault hot-swap. `git diff --check` passed for `HectonFluidEngine.cs` and `BuoyancyObject.cs` with CRLF warnings only. `dotnet build` was not launched because CPU gate reported 100% and 8 existing `dotnet` processes were running.

## 2026-05-27 - 13VOD Audit/Fix Pass 59 - Underwater HUD/photophobia hot RT allocation gate
What was wrong -> Underwater HUD fog luminance and flashlight photophobia update paths could create `RenderTexture` targets on first use from recurring visual code.
What was done -> Prewarmed the HUD luminance and photophobia textures in `OnEnable`; hot update calls now pass `allowAllocate:false` and fail closed if resources are missing. Ready resources return before repeated kernel/resource validation.
Cinematic Cheats used -> Kept the 1x1 HUD luminance reduction and 128x128 photophobia field as cheap visual fakes. Rejected first-use RT allocation inside underwater presentation.
Exact Microseconds saved -> 0.0 us steady-state claimed. Prevents unmeasured `RenderTexture` allocation stalls when HUD/flashlight underwater visuals first activate.

Verification -> Targeted `rg` shows `OnEnable` calls both resource ensures with `allowAllocate:true`, while `UpdateHudFogLuminanceDownsample` and `UpdateFlashlightPhotophobiaField` use `allowAllocate:false`. `git diff --check` passed for `HectonUnderwaterVisuals.cs` with CRLF warning only. `dotnet build` was not launched because CPU gate reported 58.4% and one existing `dotnet` process was running.

## 2026-05-27 - 13VOD Audit/Fix Pass 60 - Underwater visual direction fidelity
What was wrong -> `HectonUnderwaterVisuals.ResolveSafeDirection` collapsed non-unit diagonal directions to a dominant axis. That affected biome fog transition anchors and shallow beam orientation, making underwater visual direction grid-bound.
What was done -> The helper now rejects zero/non-finite vectors, preserves near-unit inputs, and normalizes finite non-unit vectors with `math.rsqrt`. The dead dominant-axis helper was removed.
Cinematic Cheats used -> Kept the cheap biome fog anchor and beam fake. Rejected taxicab vector approximation because this route had no explicit low-tier LOD gate.
Exact Microseconds saved -> 0.0 us saving claimed. The change adds one `rsqrt` only for non-normalized presentation vectors and preserves diagonal authoring.

Verification -> Targeted `rg` shows only two `ResolveSafeDirection` call sites and no `DominantAxisOrDefault` remains in `HectonUnderwaterVisuals.cs`; `git diff --check` passed for the file with CRLF warning only. Re-extracted `CURRENT_BATCH.md` after Pass 60; result remains `NO_PROMPT_13VOD`. CPU gate was clear at 46.0%, `dotnet` 0, `csc` 0; `dotnet build Hecton8.Core.csproj --no-restore` failed on existing project-wide errors: Odin/reference mismatch, duplicate source/member definitions, and missing world/vegetation/wreck `BufferID` entries. No green compile claimed.

## 2026-05-27 - 13VOD Audit/Fix Pass 61 - Async buoyancy readback active-range cleanup
What was wrong -> Async buoyancy GPU readback requested the whole request buffer even when only an active prefix had been dispatched. Mock/apply jobs and authority math helpers also still carried dead `GlobalQualityWeight` inputs after the path was made authority-invariant.
What was done -> `DispatchGpuReadback` now requests only `activeRequestCount * sizeof(ReadbackRequestDTO)` bytes from the `GraphicsBuffer`, guarded against buffer stride/count limits. Dead quality fields were removed from readback jobs and unused quality parameters were removed from authority math helpers.
Cinematic Cheats used -> Kept the deterministic analytical/mock readback fake. Rejected quality-dependent readback truth and rejected inactive-slot GPU readback bandwidth.
Exact Microseconds saved -> 0.0 CPU us steady-state claimed. Default capacity 512 at 16 bytes/request reads 8192 bytes; active 64 now reads 1024 bytes, saving 7168 bytes per GPU readback.

Verification -> Targeted `rg` shows no readback job `public float GlobalQualityWeight` remains, authority math helpers no longer accept quality arguments, and `DispatchGpuReadback` uses `AsyncGPUReadback.Request(requestBuffer, readbackBytes, 0, null)`. `git diff --check` passed for `AsyncBuoyancyReadbackRuntime.cs` and `AsyncBuoyancyReadbackJobs.cs` with CRLF warnings only. `dotnet build` was not launched because CPU gate reported 71% and one existing `dotnet` process was running.

## 2026-05-27 - 13VOD Audit/Fix Pass 62 - Shinobu ocean readback active-range cleanup
What was wrong -> `ShinobuOceanSurfaceAtmosphereRuntime` dispatched only active wave-height queries but read back the full 64-slot result buffer.
What was done -> Added active byte-count calculation for `float4` result readbacks and switched to `AsyncGPUReadback.Request(resultBuffer, readbackBytes, 0, null)`.
Cinematic Cheats used -> Kept the bounded GPU wave-height probe fake. Rejected per-count buffer churn and full inactive-slot readback.
Exact Microseconds saved -> 0.0 CPU us steady-state claimed. Active 4 of 64 samples now reads 64 bytes instead of 1024 bytes, saving 960 bytes per readback.

Verification -> Targeted `rg` shows `DispatchWaveHeightReadback` computes `readbackBytes`, guards zero/overflow, and uses the byte-range readback overload. `git diff --check` passed for `ShinobuOceanSurfaceAtmosphereRuntime.cs` with CRLF warning only. CPU gate was clear at 47%, `dotnet` 0, `csc` 0; `dotnet build Hecton8.Core.csproj --no-restore` failed with existing project-wide categories: assembly/reference conflicts, duplicate source includes, missing Odin attributes, duplicate members, and missing world/vegetation/wreck `BufferID` entries. No green compile claimed.

## 2026-05-27 Pass 63 - Underwater URP Camera Data Hot Lookup Cleanup
What was wrong -> `HectonUnderwaterVisuals` enforced URP depth/color texture and camera-stack composition from `LateFrameTick`, but still called `TryGetComponent<UniversalAdditionalCameraData>` on main/space cameras in that recurring path.
What was done -> Added cached `UniversalAdditionalCameraData` handles for main and space cameras, reused them in `EnsureGameplayCameraStackEnabled`, `ApplyGameplayCameraCompositionMode`, `EnsureOceanUnderwaterPassOwnership`, and `EnsureOceanCameraOwnership`, and invalidated caches on Unity missing/null references.
Cinematic Cheats used -> None new. This preserves the existing cheap visual-stack fake and removes route overhead instead of adding simulation.
Exact Microseconds saved -> Estimated 0.2-1.0 us/frame during active underwater visual sync on i3/MX350 class hardware. Exact runtime proof remains pending Unity profiler capture.
Verification -> Targeted `rg` shows normal main/space URP texture/composition calls use `EnsureCameraTextureRequirementsCached` and no direct `EnsureCameraTextureRequirements(mainCamera)` remains. `git diff --check` passed for touched code/docs with CRLF warning only. `dotnet build` was not launched because CPU gate reported 100% and 8 existing `dotnet` processes were running.

## 2026-05-27 Pass 64 - Internal Flood Waterline Hot Registry Polling Cleanup
What was wrong -> `InternalFloodWaterlineRuntime.AdvanceWaterlinePresentation` retried `GlobalRegistry.Player` and `GlobalRegistry.HabitatGraph` every 30 LateFrame ticks when dependencies were missing. That made internal waterline presentation a recurring registry polling lane.
What was done -> Removed the 30-tick dependency refresh state and replaced it with cold `CacheRuntimeDependenciesCold()` during service initialization. Runtime replacement remains handled by `IGlobalRegistryHotSwapListener`.
Cinematic Cheats used -> Kept the existing shader-global waterline/droplet fake. No simulation or authority state added.
Exact Microseconds saved -> 0.0-0.3 us/frame only in missing-dependency cases; normal wired scenes keep equivalent steady-state cost. Primary gain is deterministic dependency ownership.

Verification -> Targeted `rg` shows only cold `CacheRuntimeDependenciesCold()` reads `GlobalRegistry.Player/HabitatGraph`; no `RefreshCachedDependencies`, dependency retry interval, or LateFrame refresh state remains. `git diff --check` passed for touched code/docs with CRLF warning only. `dotnet build` was not launched because CPU gate reported 100% and 0 existing `dotnet`/`csc` compiler processes.

## 2026-05-27 Pass 65 - Visor Fluid Render Dependency Hot Registry Cleanup
What was wrong -> `HectonVisorFluidDistortionFeature.AddRenderPasses` could reach `GlobalRegistry.Player` and `GlobalRegistry.FluidSimulation` through runtime-state helpers on every render pass when dependencies were missing or not ready.
What was done -> Added cold render dependency caching in `OnEnable` and `Create`. Render path helpers now only validate cached services; `IGlobalRegistryHotSwapListener` remains the runtime replacement route.
Cinematic Cheats used -> Kept the existing visor refraction/wet-lens shader fake and black-box telemetry. No simulation or high-cost fluid pass added.
Exact Microseconds saved -> 0.0-0.5 us/render camera only in missing/not-ready dependency cases; normal wired scenes retain equivalent steady-state cost.

Verification -> Targeted `rg` shows `ResolvePlayerContext` and `ResolveFluidSimulation` only validate cached services; registry reads are confined to cold `CacheRenderDependenciesCold()` in `OnEnable/Create` and service hot-swap callbacks. `git diff --check` passed for touched code/docs with CRLF warnings only. `dotnet build` was not launched because CPU gate reported 100% and 0 existing `dotnet`/`csc` compiler processes.

## 2026-05-27 Pass 66 - Fluid Fixed-Tick Weather Snapshot Coherence
What was wrong -> `HectonFluidEngine.FixedTick` read weather once for abyssal flow and early wave uniform paths, then read it again before buoyancy wave population and GPU buoyancy queueing. That is a redundant hot service read and can mix snapshots inside one physics tick.
What was done -> Introduced one `fixedWeatherSnapshot` for the tick and reused it for abyssal flow visual sync, early-exit ocean wave uniform publication, Gerstner wave population, and GPU buoyancy queueing.
Cinematic Cheats used -> Kept the existing weather-driven Gerstner/abyssal flow fake. No new simulation added.
Exact Microseconds saved -> 0.0-0.2 us/fixed tick. The larger value is consistency: ocean surface, buoyancy, and abyssal flow consume one snapshot per physics tick.

Verification -> Targeted `rg` shows `fixedWeatherSnapshot` feeds FixedTick consumers and no `abyssalWeatherSnapshot` remains. `git diff --check` passed for touched code/docs with CRLF warning only. Re-extracted `CURRENT_BATCH.md` after Pass 66; result remains `NO_PROMPT_13VOD`. `dotnet build` was not launched because CPU gate reported 88% and one existing `dotnet` process was running.

## 2026-05-27 Pass 67 - Fluid Surface Read-Route Purity
What was wrong -> Fluid surface read routes (`CurrentWaterLevelY`, `GetFlowAtPosition`, `GetWaterHeightAtPosition`) recomputed cinematic water level and mutated `GlobalPhysicsStateManager` frame cache. `FixedTick` also still used one weather read for water-level publish and another for waves/abyssal/buoyancy.
What was done -> Added an owner-published cinematic water-level snapshot and timestamp. Read routes now return `ReadPublishedCurrentWaterLevelY()`. `FixedTick` resolves one `fixedWeatherSnapshot` first and passes it into water-level publication, wave uniform publication, abyssal visual sync, Gerstner population, and GPU buoyancy queueing.
Cinematic Cheats used -> Kept the existing triangle-wave/celestial tide cinematic fake. Rejected per-read recomputation and duplicate service snapshots.
Exact Microseconds saved -> 0.0-0.2 us/fixed tick estimated. Main gain is contract correctness: read routes no longer mutate global frame cache and water/flow/wave consumers use one owner snapshot.

Verification -> Targeted `rg` confirms read routes use `ReadPublishedCurrentWaterLevelY`, `GetFlowAtPosition` uses `ResolveWaterLevelTimeSeconds(in weatherSnapshot)`, and `FixedTick` calls `PublishCurrentWaterLevelUniform(in fixedWeatherSnapshot)`. `git diff --check` passed for touched code/docs with CRLF warnings only. CPU gate was clear at 33%, `dotnet` 0, `csc` 0; `dotnet build Hecton8.Core.csproj --no-restore` failed on existing project-wide errors: System.* reference conflicts, duplicate source includes, missing Odin attributes, duplicate members, and missing world/vegetation/wreck `BufferID` entries. No green compile claimed.

## 2026-05-27 Pass 68 - Visor Raw-History Camera Data Retry Cleanup
What was wrong -> `HectonVisorUberPostFeature.AddRenderPasses` could retry `TryGetComponent<UniversalAdditionalCameraData>` every render pass when temporal reconstruction requested raw color history but the render camera had no usable history access.
What was done -> Added a per-camera raw-history access cache, including negative results. `TryResolveHistoryReadAccess` now performs the component lookup only on camera-cache miss or after explicit cache clear.
Cinematic Cheats used -> Kept the existing non-temporal reconstruction fallback and internal waterline shader fake. Rejected per-pass camera repair/retry.
Exact Microseconds saved -> 0.0-0.4 us/render camera estimated only when temporal history is requested but unavailable. Normal history-enabled cameras keep equivalent behavior.

Verification -> Targeted `rg` confirms cached raw-history fields, `ClearRawColorHistoryAccessCache`, and one `TryGetComponent<UniversalAdditionalCameraData>` site guarded by camera-cache miss. `git diff --check` passed for touched code/docs with CRLF warnings only. Full compile was not rerun because Pass 67 already confirmed the project-wide build is blocked by existing Odin/reference/duplicate/BufferID errors outside this change.

## 2026-05-27 Pass 69 - Underwater Shallow Sun Beam Hot Retry Cleanup
What was wrong -> `HectonUnderwaterVisuals.UpdateShallowSunBeam` could call `Transform.Find("Underwater_ShallowSunBeam")` and then `TryGetComponent<Light>` every LateFrame when the optional shallow beam child/light was absent.
What was done -> Added per-main-camera negative caching for the beam child and per-transform negative caching for the beam light component. Serialized/assigned lights still bind their transform directly.
Cinematic Cheats used -> Kept the shallow sun beam as a cheap light/presentation fake. Rejected repeated runtime repair for an optional visual asset.
Exact Microseconds saved -> 0.0-0.3 us/frame estimated only in missing/incomplete optional beam setups. Authored beam setups keep equivalent behavior.

Verification -> Targeted `rg` confirms `_shallowSunBeamSearchCamera`, `_shallowSunBeamLightSearchTransform`, and `_shallowSunBeamLightSearchCompleted` guard the hierarchy/component lookup. `git diff --check` passed for touched code/docs with CRLF warnings only. Re-extracted `CURRENT_BATCH.md` after Pass 69; result remains `NO_PROMPT_13VOD`. Full compile was not rerun because Pass 67 already confirmed existing project-wide Odin/reference/duplicate/BufferID errors outside this change.

## 2026-05-27 Pass 70 - Underwater Motes/Exhale Hot Retry Cleanup
What was wrong -> `ResolveUnderwaterParticles` and `ResolveUnderwaterExhaleBubbles` could repeat `Transform.Find` plus `TryGetComponent` every underwater presentation frame when optional motes/exhale children or components were absent.
What was done -> Added per-main-camera child-search caches and per-child component negative caches for suspended motes and exhale bubbles. Missing optional VFX now fail closed instead of retrying every LateFrame.
Cinematic Cheats used -> Kept suspended motes and exhale bubbles as authored visual fakes. Rejected runtime hierarchy/component repair for optional VFX.
Exact Microseconds saved -> 0.0-0.5 us/frame estimated only in incomplete optional VFX scenes. Authored VFX setups keep equivalent behavior.

Verification -> Targeted `rg` confirms `_underwaterSuspendedMotesSearchCamera`, `_underwaterExhaleBubblesSearchCamera`, child-transform caches, and completion flags guard the lookups. `git diff --check` passed for touched code/docs with CRLF warnings only. Full compile was not rerun because Pass 67 already confirmed existing project-wide Odin/reference/duplicate/BufferID errors outside this change.

## 2026-05-27 Pass 71 - Giant Wake Read-Route Snapshot Cleanup
What was wrong -> Analytical/mod flow read routes could call `ResolveGiantWakeCurrentBase()` when `_resolvedGiantWakeCurrent` was zero, pulling celestial direction from a read path instead of consuming the fluid owner snapshot. Depth fade also used raw `waterLevel`, not the published cinematic water level.
What was done -> Flow queries now pass `_resolvedGiantWakeCurrent` directly. `ResolveGiantWakeCurrentForDepth` uses the published cinematic water-level snapshot and no longer repairs giant wake from celestial service.
Cinematic Cheats used -> Kept giant wake as a bounded analytical current fake. Rejected read-time celestial repair and duplicate wake calculation.
Exact Microseconds saved -> 0.0-0.2 us/query estimated only when the snapshot is absent/stale. Main gain is pure snapshot consumption.

Verification -> Targeted `rg` confirms `ResolveGiantWakeCurrentBase()` remains on owner `FixedTick` refresh paths, while flow query/depth fade paths use `_resolvedGiantWakeCurrent` and `ReadPublishedCurrentWaterLevelY()`. `git diff --check` passed for touched code/docs with CRLF warnings only. Full compile was not rerun because Pass 67 already confirmed existing project-wide Odin/reference/duplicate/BufferID errors outside this change.

## 2026-05-27 Pass 72 - Visor Fluid Black-Box Render Allocation Gate
What was wrong -> `HectonVisorFluidDistortionFeature.WriteBlackBoxFrame` executes from `AddRenderPasses`, but its lease helper could allocate the `VisorRefractionBlackBox` DataVault ring via `EnsureGenerationHandle<VisorRefractionTelemetryEntry>()`.
What was done -> Split existing-only render lease resolution from cold allocation. `TryEnsureBlackBoxLease()` now only validates existing descriptors; `EnsureBlackBoxLeaseCold()` allocates/prewarms from `OnEnable`, `Create`, and DataVault hot-swap, and refuses fresh allocation while `IDataVault.IsAllocationLocked` or compaction fence is active.
Cinematic Cheats used -> Kept the fixed 300-frame black-box telemetry ring and visor refraction shader fake. Rejected render-path storage repair and rejected deleting crash telemetry.
Exact Microseconds saved -> 0.0 us steady-state claimed. Prevents a fault-path native/DataVault generation hitch on render cameras when the black-box ring is missing.

Verification -> Targeted `rg` confirms `WriteBlackBoxFrame` calls only `TryEnsureBlackBoxLease`, `EnsureGenerationHandle<VisorRefractionTelemetryEntry>` appears only in `EnsureBlackBoxLeaseCold`, and cold ensure checks `IsAllocationLocked`. `git diff --check` passed for the visor feature with CRLF warning only. Re-extracted `CURRENT_BATCH.md` after Pass 72; result remains `NO_PROMPT_13VOD`. `dotnet build` was not launched because CPU gate reported 91.1% and one existing `dotnet` process was running.

## 2026-05-27 Pass 73 - Underwater Optional Lookup Retry Cleanup
What was wrong -> `HectonUnderwaterVisuals` still retried optional `LandingImpactVFX`, `Suit_Visor`, and `Sun_Body` lookup paths when those authored presentation objects were absent.
What was done -> Added negative caches for transition camera VFX per main camera, transition visor child/component per player root, and sun-body child per sun light. Caches reset on disable.
Cinematic Cheats used -> Kept the transition runoff/distortion and sun-body hide/show as cheap presentation fakes. Rejected event/slow-tick runtime repair loops for optional visual children.
Exact Microseconds saved -> 0.0-0.3 us per transition event or slow-tick in missing optional setups; authored setups keep equivalent behavior.

Verification -> Targeted `rg` and `Select-String` confirm `_transitionCameraVfxSearch*`, `_transitionVisorSearch*`, and `_sunVisualSearch*` guard the `TryGetComponent`/`Transform.Find` sites. `git diff --check` passed for `HectonUnderwaterVisuals.cs` with CRLF warning only. `dotnet build` was not launched because CPU gate reported 100% and no compiler process was active.

## 2026-05-27 Pass 74 - Underwater SpaceCamera Lookup Cadence Guard
What was wrong -> `ResolveSpaceCamera()` could run from the runtime visual-owner path and call `Find("SpaceCamera")` on player/main/parent transforms every pass while the optional space camera was absent.
What was done -> Added `_nextRuntimeSpaceCameraResolveTime` and limited runtime missing-space-camera searches to `RuntimeCameraResolveRetryInterval`. Editor preview lookup remains unchanged.
Cinematic Cheats used -> Kept the optional celestial/space camera composition fake. Rejected hard negative-cache because late-created camera stacks must still recover.
Exact Microseconds saved -> 0.0-1.0 us/frame only in missing runtime `SpaceCamera` setups. Authored camera stacks keep equivalent behavior.

Verification -> Targeted `rg` confirms `_nextRuntimeSpaceCameraResolveTime` gates `ResolveSpaceCamera`; `Select-String` confirms remaining `SpaceCamera` hierarchy finds are in the guarded runtime resolver or editor preview path. `git diff --check` passed for `HectonUnderwaterVisuals.cs` with CRLF warning only. `dotnet build` was not launched because CPU gate reported 82.5% and no compiler process was active.

## 2026-05-27 Pass 75 - Visor Reconstruction Vault Allocation-Lock Guard
What was wrong -> `HectonVisorUberPostFeature.EnsureReconstructionVaultHandle<T>()` could call `EnsureGenerationHandle<T>()` during a DataVault allocation lock or compaction fence after existing descriptor recovery failed.
What was done -> Existing handles are still resolved first. Fresh allocation now fails closed before `EnsureGenerationHandle<T>()` if `_dataVault.IsCompactionFenceActive` or `_dataVault.IsAllocationLocked`.
Cinematic Cheats used -> Kept the visor reconstruction constants/telemetry DataVault proof route and bilateral/temporal reconstruction fake. Rejected locked-phase renderer storage repair.
Exact Microseconds saved -> 0.0 us steady-state claimed. Prevents fault-path DataVault generation/allocation hitch during locked setup phases.

Verification -> Targeted `rg` confirms `EnsureReconstructionVaultHandle<T>` checks `IsCompactionFenceActive || IsAllocationLocked` before `EnsureGenerationHandle<T>`. `git diff --check` passed for `HectonVisorUberPostFeature.cs` with CRLF warning only. Re-extracted `CURRENT_BATCH.md` after Pass 75; result remains `NO_PROMPT_13VOD`. `dotnet build` was not launched because CPU gate reported 60.5% and no compiler process was active.

## 2026-05-27 Pass 76 - Underwater URP Camera Data Negative Cache
What was wrong -> `HectonUnderwaterVisuals.TryResolveCameraDataCached` cached successful `UniversalAdditionalCameraData` lookups only. Missing URP camera data forced repeated `TryGetComponent<UniversalAdditionalCameraData>` from underwater visual-owner routes.
What was done -> Added negative cache flags for main/space camera data, passed them through the cached resolver, and reset camera-data cache state on disable.
Cinematic Cheats used -> Kept the existing URP depth/color/postprocess enforcement that supports underwater post/fog fake. Rejected frame-by-frame camera component repair.
Exact Microseconds saved -> 0.0-0.4 us/frame estimated only on incomplete camera setups.

Verification -> Targeted `rg` confirms all runtime cached camera-data routes pass the negative-cache flag. `git diff --check` passed for touched files with CRLF warnings only.

## 2026-05-27 Pass 77 - Fluid Read-Only DataVault Resolve Cleanup
What was wrong -> Public fluid read-model routes checked `FluidVaultBuffer<T>.IsCreated` and then called `AsReadOnly()`, resolving DataVault state twice.
What was done -> `FloaterPositions`, `BuoyancyResults`, `TryGetActiveMaelstroms`, and `TryGetActiveWhirlpoolFlows` now call `AsReadOnly()` directly and use its single validation path.
Cinematic Cheats used -> No simulation change. Kept the same analytical current/wake buffer fakes and native DataVault ownership.
Exact Microseconds saved -> 0.0-0.2 us/query estimated for read-model consumers.

Verification -> Targeted `rg` confirms the `IsCreated ? AsReadOnly() : default` duplicate pattern is gone in `HectonFluidEngine.cs`. `git diff --check` passed with CRLF warnings only.

## 2026-05-27 Pass 78 - Flow Visualizer Fluid Registry Polling Cleanup
What was wrong -> `FlowFieldVisualizer` read `GlobalRegistry.FluidSurfaceCurrent` during grid recalculation and in `SampleCurrentAt`; the non-job path could read it once per grid sample.
What was done -> Added `IGlobalRegistryHotSwapListener`, centralized fluid-current subscription in `CacheFluidCurrent`, and made recalculation/sample routes consume `_subscribedFluidCurrent`.
Cinematic Cheats used -> Kept the diagnostic current-field preview fake. Rejected registry polling in sample loops.
Exact Microseconds saved -> 0.0-0.5 us/recalculation plus one registry read per non-job sample avoided.

Verification -> Targeted `rg` confirms `GlobalRegistry.FluidSurfaceCurrent` remains only in `OnEnable` cold wiring for `FlowFieldVisualizer`, and sampling uses `_subscribedFluidCurrent`. Re-extracted `CURRENT_BATCH.md` after Pass 78; result remains `NO_PROMPT_13VOD`. Full compile was not launched because CPU gate reported 100% and two existing `dotnet` processes were running.

## 2026-05-27 Pass 79 - Async Buoyancy DataVault Compaction-Fence Guard
What was wrong -> `AsyncBuoyancyReadbackRuntime.EnsureVaultDescriptor<T>()` blocked fresh descriptor generation during allocation lock but not during DataVault compaction.
What was done -> Existing descriptors still adopt first; fresh `EnsureGenerationHandle<T>()` now fails closed when `IsAllocationLocked` or `IsCompactionFenceActive` is true.
Cinematic Cheats used -> Kept async readback as the cheap GPU/CPU buoyancy sampling bridge. Rejected compaction-phase storage repair.
Exact Microseconds saved -> 0.0 us steady-state. Prevents fault-path DataVault generation during compaction.

Verification -> Targeted `rg` confirms the async buoyancy allocation guard includes allocation lock and compaction fence. `git diff --check` passed with CRLF warnings only.

## 2026-05-27 Pass 80 - BuoyancyObject DataVault Hot-Swap Registry Cleanup
What was wrong -> `BuoyancyObject` handled DataVault replacement by reading `GlobalRegistry.BuoyancyObjectRegistry`, mixing a vault event with fluid runtime dependency resolution.
What was done -> Removed the registry read from the DataVault case. The object rebinds only cached fluid runtime there; `FluidRuntime` hot-swap owns actual dependency replacement.
Cinematic Cheats used -> No visual fake changed. This is dependency-route cleanup for buoyancy ownership.
Exact Microseconds saved -> 0.0 us steady-state; removes one cold fault-path registry read and avoids route ambiguity.

Verification -> Targeted `rg` confirms `BuoyancyObjectRegistry` is read only in cold dependency caching, not DataVault hot-swap. `git diff --check` passed with CRLF warnings only.

## 2026-05-27 Pass 81 - Water Render DataVault Fence Sweep
What was wrong -> WaterOptics, OceanSinglePass, and ShorelineFoam allocation paths could generate DataVault handles during compaction, and OceanSinglePass mock/runtime acquire did not consistently adopt existing handles before allocation.
What was done -> Added compaction-fence gates and existing-handle adoption before fresh `EnsureGenerationHandle<T>()` in those render/ocean paths.
Cinematic Cheats used -> Kept mock ocean constant buffer, shoreline foam, and WaterOptics as bounded visual fakes. Rejected allocation repair while the vault compacts.
Exact Microseconds saved -> 0.0 us steady-state. Prevents cold/editor/render fault allocation spikes.

Verification -> Targeted `rg` confirms render/ocean allocation guards include compaction fence and allocation lock. Re-extracted `CURRENT_BATCH.md`; result remains `NO_PROMPT_13VOD`.

## 2026-05-27 Pass 82 - Core Fluid/Ocean/Buoyancy DataVault Fence Sweep
What was wrong -> Core fluid buffers, underwater biome fog, Crest ocean kinematics, analytical Gerstner waves, buoyancy displacement, and ocean surface atmosphere used allocation-lock-only gates near DataVault allocation paths.
What was done -> Added `IsCompactionFenceActive` beside allocation lock in fresh allocation routes. Ocean surface atmosphere now blocks only when missing handles require allocation, preserving existing-handle resolution.
Cinematic Cheats used -> Preserved all cheap water fakes: analytical waves, biome fog, ocean atmosphere, Crest kinematics bridge, and buoyancy readback. Rejected compaction-phase native/DataVault repair.
Exact Microseconds saved -> 0.0 us steady-state. Prevents locked-phase spikes on low-end hardware; no gameplay truth, DTO layout, or quality route changed.

Verification -> Targeted `rg` confirms touched water-domain allocation-lock gates now include compaction fence where allocation can occur. `git diff --check` passed for all touched code/docs with CRLF warnings only. `dotnet build Hecton8.Core.csproj --no-restore` was launched after CPU gate (`CPU=40.0`, `dotnet=0`, `csc=0`) and still fails on pre-existing project-wide System.* reference conflicts, duplicate source includes, missing Odin attributes, duplicate members, and missing world/wreck/vegetation `BufferID` entries.

## 2026-05-27 Pass 83 - Crest Depth-Cache Hierarchy Scan De-Duplication
What was wrong -> Crest depth-cache bootstrap/configure could call depth-cache child discovery more than once in the same route, and origin-shift simulation reset resolved depth-cache references despite only needing `OceanRenderer`.
What was done -> `TryResolveReferences` now accepts `resolveDepthCache:false`. Bootstrap/configure refresh `_depthCacheScratch` once and reuse it for authored-cache and legacy-cache decisions. Origin-shift reset skips depth-cache discovery.
Cinematic Cheats used -> Kept Crest sea-floor depth cache as a bounded ocean rendering fake. Rejected extra runtime discovery work and did not re-enable the disabled realtime capture camera path.
Exact Microseconds saved -> 0.0-8.0 us per configure/origin-shift pass estimated from avoided hierarchy scans; actual scene cost remains `PENDING VERIFICATION`.

Verification -> Targeted `rg` confirms `TryResolveReferences(resolveDepthCache:false)`, centralized `RefreshDepthCacheScratch()`, and `refreshScratch:false` helper calls. `git diff --check` passed for the Crest file with CRLF warning only.

## 2026-05-27 Pass 84 - Visor-Water DataVault Compaction Allocation Guard Sweep
What was wrong -> Mock reconstruction, noir tuning/noir handle acquisition, and volumetric particulate fog native-state setup could still attempt fresh DataVault handle generation during compaction.
What was done -> Added compaction-fence checks beside allocation lock at the final fresh-allocation gates while preserving existing handle adoption/read paths.
Cinematic Cheats used -> Kept underwater visor reconstruction, noir post, and particulate fog as presentation fakes fed by water state. Rejected compaction-phase storage repair from renderer/editor setup routes.
Exact Microseconds saved -> 0.0 us steady-state. Prevents fault-path DataVault generation/allocation stalls on low-end hardware.

Verification -> Targeted `rg` confirms the touched `EnsureGenerationHandle<T>` allocation gates include `IsCompactionFenceActive || IsAllocationLocked`. `git diff --check` passed for touched code/docs with CRLF warnings only. Crest runtime depth-cache PNG readback was inspected and left unchanged because it is editor/development-only and unreachable while `HectonRuntimeDepthCacheCameraDisabled` is true. Re-extracted `CURRENT_BATCH.md` after Pass 84; result remains `NO_PROMPT_13VOD`. `dotnet build Hecton8.Core.csproj --no-restore` was launched after CPU gate (`CPU=24`, `dotnet=0`, `csc=0`) and still fails on pre-existing project-wide System.* reference conflicts, duplicate source includes, missing Odin attributes, duplicate members, and missing world/wreck/vegetation `BufferID` entries.

## 2026-05-27 Pass 85 - Underwater Secondary Crest Pass Purge Cadence Guard
What was wrong -> `HectonUnderwaterVisuals` enumerated all runtime cameras every LateFrame to purge secondary Crest underwater passes. In stable ownership this repeated `Camera.GetAllCameras()` without new information.
What was done -> Added owner/pass dirty tracking plus one-second cadence fallback. Purge still runs immediately on main/space/pass changes and periodically for late external bridge mutations.
Cinematic Cheats used -> Kept Crest underwater pass as a single owned visual fake for main camera composition. Rejected broad camera component polling.
Exact Microseconds saved -> Estimated 0.0-2.0 us/frame while underwater camera ownership is stable, depending on camera count.

Verification -> Targeted `rg` confirms `_nextSecondaryUnderwaterPassPurgeTime`, `_secondaryUnderwaterPassPurge*` fields, and `PurgeSecondaryUnderwaterPassesIfNeeded()` route.

## 2026-05-27 Pass 86 - Fluid Read-Route Weather Snapshot Purity
What was wrong -> Fluid public flow/height/mod-flow read routes pulled `ResolveWeatherSnapshot()` directly, so consumers could mix query-time weather with owner-published water-level/current state.
What was done -> Published the weather snapshot beside the water-level snapshot in `FixedTick`. `GetFlowAtPosition`, `GetWaterHeightAtPosition`, and `TrySampleModAbyssalFlow` now consume `ReadPublishedWeatherSnapshot()`.
Cinematic Cheats used -> Preserved analytical Gerstner and phantom-current fakes. Rejected per-query service polling.
Exact Microseconds saved -> Estimated 0.0-0.3 us/query in query-heavy frames; primary gain is snapshot coherence.

Verification -> Targeted `rg` confirms read routes use `ReadPublishedWeatherSnapshot()` and `ResolveWeatherSnapshot()` remains on owner publication/fixed tick paths.

## 2026-05-27 Pass 87 - Crest Kinematics Quality-Authoring Text Cleanup
What was wrong -> The Crest kinematics inspector tooltip still claimed `GlobalQualityWeight` controls active Gerstner octave count, contradicting the current water-truth invariant.
What was done -> Updated the tooltip to state deterministic octave limit and telemetry-only quality semantics.
Cinematic Cheats used -> No runtime fake changed. This removes misleading authoring guidance that could reintroduce quality-dependent water physics.
Exact Microseconds saved -> 0.0 us runtime.

Verification -> Targeted `rg` confirms the stale tooltip text is gone. `git diff --check` passed for touched code/docs with CRLF warnings only. Compile gate was closed (`CPU=100`, `dotnet=1`, `csc=0`), so no build was launched. Re-extracted `CURRENT_BATCH.md` after Pass 87; result remains `NO_PROMPT_13VOD`.
