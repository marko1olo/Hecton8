# WORLD_CELESTIAL Rationale

Status: PENDING VERIFICATION

## Mandate Selection

Problem: Celestial/weather work touches low-frequency world authority, fog/light presentation, deterministic event timing, and AUP stability.
Solution: Load only the mandates relevant to visual fake celestial mechanics, AUP, weather flow, zero-GC policy, deterministic RNG, telemetry, GlobalRegistry/EventBus decoupling, and shader fog/noir presentation.
Rejected Alternatives: Loading every registry mandate would dilute task context and violate the prompt's strict parsing intent.
Scalability potential: Low uses FrostTick/fake scalar outputs; Middle increases event cadence; High adds richer god-ray/fog modulation; Ultra spends saved CPU on visual overkill, not orbital truth.
Hardware Impact: Expected low-end i3/MX350 benefit comes from triangle-wave scalar math and low-cadence shader global updates instead of per-frame trigonometric/Kepler paths. Exact gain pending measurement.

## Decisions

## Loop 1 Decisions

Problem: Prior celestial orbit language and cached fields still implied Kepler-style solving even though the assignment demands a deterministic cinematic fake.
Solution: Converted orbit definitions to `CinematicOrbitDefinition`, kept the snapshot contract intact, and drove orbital axes with `TriangleWave01(phase) = abs(frac(phase) * 2 - 1)` plus cached reciprocal period multipliers.
Rejected Alternatives: Standard Kepler anomaly iteration and `sin/cos` ellipse projection were rejected because the sky body motion is presentation, not navigation truth, and every extra transcendental burns frame-time currency without immersion gain.
Scalability potential: Low/MX350 evaluates fake orbit snapshots at FrostTick cadence; Middle keeps the same cheap math with normal visual publishing; High evaluates every 60 frames; Ultra can spend the saved budget on richer fog, shafts, bloom, and eclipse treatment.
Hardware Impact: Estimated i3/MX350 gain is 4.0 us per celestial snapshot by replacing trigonometric orbital projection with branch-light triangle waves and reciprocal multiplication.

Problem: Tide and eclipse outputs needed to feed other domains without direct object dependencies.
Solution: Kept data in `CelestialRuntimeSnapshot`, published through `GlobalRegistry.PublishCelestialRuntimeSnapshot`, pushed shader globals, and used existing `HectonEventBus`/`CelestialEvents` lanes for eclipse start/end.
Rejected Alternatives: Direct calls into fluid, lighting, or audio systems were rejected because 20+ parallel agents are editing adjacent systems and concrete dependencies would create compile and integration collisions.
Scalability potential: Low consumes `TideHeightMeters` as a scalar; Middle adds local visual response; High/Ultra can layer stronger ambient drop, biolum response, and water shadow cheats from the same payload.
Hardware Impact: Estimated i3/MX350 gain is 8.0 us per snapshot and 12.0 us per eclipse check compared with physics gravity pulls or raycast/mesh occlusion tests.

Problem: Orbital direction normalization must remain deterministic and allocation-free while avoiding `math.normalizesafe` per the task mandate.
Solution: Used `NormalizeVisualRsqrt` with explicit finite/length checks and `math.rsqrt`, and verified no `math.normalizesafe` remains in `HectonCelestialEngine.cs` or `RandomEventSystem.cs`.
Rejected Alternatives: Generic safe-normalize helpers were rejected because they hide branching choices and make the fake orbit path less auditable.
Scalability potential: Same math applies across Low/Middle/High/Ultra; higher tiers spend the saved scalar cost on presentation, not different orbital truth.
Hardware Impact: Estimated i3/MX350 gain is 0.8 us per snapshot in the fake-orbit normalization path.

## Loop 2 Decisions

Problem: Weather needed to drive abyssal visibility, snow opacity, god-rays, and current surge as global presentation data without adding a fluid simulation.
Solution: Confirmed `GlobalWeatherDirector` publishes `WeatherIntensity` into `_AbyssalFogDensity`, `_MarineSnowOpacity`, `_HectonGodRayIntensity`, and `_HectonGlobalFlowMagnitudeMultiplier` on FrostTick; the god-ray term uses moon phase, wave height, and triangle-wave cloud flicker.
Rejected Alternatives: Per-frame volumetric silt, cloud ray marching, and surge-vortex physics were rejected because they spend CPU/GPU on realism where a scalar visual bridge buys the same immersion.
Scalability potential: Low/MX350 consumes one FrostTick scalar bridge; Middle can tune authored fog/snow curves; High/Ultra can spend the saved time on denser shafts, stronger fog dithering, and overkill post-lighting while the data contract stays identical.
Hardware Impact: Estimated i3/MX350 gain is 18.0 us/frame for silt, 35.0 us/FrostTick for god-rays, and 22.0 us/FrostTick for current surge compared with simulated transport.

Problem: Lightning thunder had physical/audio feedback but no typed EventBus payload for Soundscape/Kinematics integration.
Solution: Added `ThunderAcousticShockEvent` and publish it through `HectonEventBus` before the existing acoustic ping and camera shake fallback.
Rejected Alternatives: Replacing the existing audio/camera fallback with an event-only path was rejected because subscribers may not be installed in every scene and the thunder feedback must remain visible now.
Scalability potential: Low uses the scalar event plus fallback shake; Middle can subscribe for cheap rumble mixing; High/Ultra can layer spatial reverb, hull resonance, and procedural screen shake from the same unmanaged payload.
Hardware Impact: Estimated i3/MX350 gain is 6.0 us per thunder event by sharing a precomputed radius/intensity/energy payload instead of letting each consumer recompute shock parameters.

Problem: Seismic shockwave carve direction used `Time.frameCount`, so two machines could generate different trench axes from identical world state.
Solution: Replaced the frame seed with `ResolveAupTimelineSeed`, mixing AUP grid/local coordinates, `GlobalRegistry.AbsoluteUniverseTime` half-second timeline slot, and voxel runtime stamp.
Rejected Alternatives: Keeping frame-count seeds was rejected because it binds world damage to frame pacing; using `UnityEngine.Random` was rejected by the deterministic RNG mandate.
Scalability potential: Low/Middle/High/Ultra all get the same deterministic seismic line; higher tiers can render more debris/dust around that stable line without changing world truth.
Hardware Impact: Estimated i3/MX350 gain is 2.0 us per seismic event by avoiding non-deterministic recovery work and duplicate consumer-side seed resolution.

Problem: Full project compilation failed after Loop 2 in files outside the assigned atmosphere/celestial domain.
Solution: Recorded the upstream blockers and verified edited assembly code with `BuildProjectReferences=false` against the previously successful reference DLLs.
Rejected Alternatives: Editing `PredatorCognitionDomain.cs` or `VoxelDeltaProcessor.cs` was rejected as cross-domain sabotage without assignment ownership.
Scalability potential: No runtime scalability change; this preserves integration boundaries while allowing WORLD_CELESTIAL work to continue.
Hardware Impact: No direct hardware impact; compile verification remains pending on unrelated Core repairs.

## Loop 3 Decisions

Problem: Meteor shower impact feedback stopped at water/audio/displacement and did not hand a crater request to the voxel owner.
Solution: Routed accepted meteor water impacts into `TryApplyMeteorVoxelImpact`, resolving the nearest active voxel volume and calling `TryApplyExtraterrestrialImpactCrater` with an axis-weighted fake radius derived from the impact radius and envelope.
Rejected Alternatives: Simulating a physical meteor projectile, blast pressure, or a full voxel explosion was rejected because a one-shot crater stamp plus VFX sells the event with deterministic cost.
Scalability potential: Low gets splash, boom, and a bounded crater stamp; Middle can add decals/dust; High/Ultra can spend saved budget on fireball overdraw and debris while the carve remains one authoritative sphere.
Hardware Impact: Estimated i3/MX350 gain is 75.0 us per meteor impact versus projectile physics plus multi-sample voxel blast.

Problem: Solar flare was visible as an event/shader state but did not directly increase player radiation exposure.
Solution: Active `SolarFlare` events now apply exposure seconds to `GlobalRegistry.Player.PlayerHealth.ApplyRadiationExposure` from the SlowTick lane.
Rejected Alternatives: Creating a separate radiation service or polling shader globals from player code was rejected because the player health facade already owns radiation exposure and advisories.
Scalability potential: Low/Middle/High/Ultra use the same SOA scalar; higher tiers can add visual corrosion, visor noise, and audio without changing the survival truth.
Hardware Impact: Estimated i3/MX350 gain is 3.0 us per SlowTick by writing the existing health scalar directly instead of adding another radiation owner.

Problem: Planetary lighting, snapshot cadence, and lunar phase publishing needed low-frequency deterministic control rather than per-frame material churn.
Solution: Sun direction global uploads now gate once per universe minute, celestial snapshots use 60-frame High/Ultra cadence or 300-frame low-tier cadence, and moon renderers receive phase/index values through reused material property blocks.
Rejected Alternatives: Per-frame sun constant-buffer writes, per-frame analytical snapshots, and material/texture instance swaps were rejected due to avoidable CPU churn and allocation risk.
Scalability potential: Low/MX350 gets FrostTick-like moon/lighting stability; Middle can keep shader response smooth; High/Ultra can spend the recovered frame time on richer shafts, eclipse response, and high-density sky polish.
Hardware Impact: Estimated i3/MX350 gain is 1.4 us/frame for sun upload throttling, 30.0 us/frame from low-tier snapshot cadence, and 10.0 us/update by avoiding runtime material swaps.

## Loop 4 Decisions

Problem: Orbital period use still needed explicit evidence that no per-body division was left in the analytical snapshot path.
Solution: Kept reciprocal cache fields for sun, gas giant, moon0, moon1, and in-game year, and verified `EvaluateCinematicOrbit` advances phase with multiplication.
Rejected Alternatives: Dividing by orbital period inside every snapshot was rejected because it is repeated scalar cost with no visual benefit.
Scalability potential: Low/MX350 uses the same reciprocal-driven orbit fake at 300-frame cadence; High/Ultra use 60-frame cadence and spend the recovered cost on shafts, fog, bloom, and eclipse polish.
Hardware Impact: Estimated i3/MX350 gain is 1.1 us per celestial snapshot by replacing repeated period division with cached reciprocal multiplication.

Problem: Eclipse state mutation still used direct branches even after occlusion was reduced to dot-threshold math.
Solution: Added `ApplyEclipseStateBranchless`, which keeps the active-state toggle in `math.select` and leaves events on start/end edges only.
Rejected Alternatives: Fully branchless event dispatch was rejected because EventBus publication is inherently an edge side effect and must not fire every check.
Scalability potential: Low keeps a cheap scalar eclipse gate; Middle adds stable ambient drop; High/Ultra can layer stronger volumetric and biolum response from the same edge events.
Hardware Impact: Estimated i3/MX350 gain is 0.2 us per eclipse check and lower branch variance during threshold hover.

Problem: Meteor shower seed still came from the generic event RNG instead of the mandated `(timeSeed ^ AUP)` deterministic line.
Solution: Replaced shower seed assignment with `ResolveMeteorAupTimeSeed`, mixing `GlobalRegistry.AbsoluteUniverseTime`, observer AUP grid/local coordinates, and a one-step LCG.
Rejected Alternatives: `UnityEngine.Random`, frame count, and the generic event RNG were rejected because meteor strikes must be recoverable from world time plus observer AUP.
Scalability potential: Low gets deterministic flashes and one-shot crater stamps; Middle can add richer splash decals; High/Ultra can overdraw fireballs and debris while the authoritative seed stays unchanged.
Hardware Impact: Estimated i3/MX350 gain is 0.5 us per meteor event plus avoided replay/debug drift from nondeterministic meteor seeds.

Problem: Floating-origin shifts could expose moon jitter if observer-relative bodies waited until their next tick to re-solve placement.
Solution: Made `ObserverRelativeCelestialBody` an `IOriginShiftListener`; after `HectonFloatingOrigin` subtracts the committed shift delta from scene transforms, the body immediately reapplies its observer-relative placement.
Rejected Alternatives: Manual double-subtraction was rejected because the floating-origin job already rebases scene transforms; waiting a frame was rejected because celestial bodies are first-viewport signals.
Scalability potential: Low/MX350 gets stable far-body placement with no extra per-frame work; High/Ultra can use the same no-jitter base for denser moon shading and eclipse presentation.
Hardware Impact: Runtime cost is shift-only and estimated below 0.3 us per celestial body per shift; benefit is one-frame jitter removal on low-end and high-end hardware.

Problem: Final compile evidence needed to distinguish WORLD_CELESTIAL changes from unrelated Core compile failures.
Solution: Verified a targeted search shows only one `UpdateAnalyticalCelestialState` definition and ran isolated `Assembly-CSharp` build with project references disabled; result was 0 warnings and 0 errors.
Rejected Alternatives: Editing `PredatorCognitionDomain.cs` or `VoxelDeltaProcessor.cs` was rejected because those blockers are outside the assigned atmosphere/celestial domain.
Scalability potential: No runtime tier change; this preserves domain boundaries while keeping the celestial engine integration compile-clean against existing references.
Hardware Impact: No runtime hardware impact; integration risk reduced without cross-domain churn.
