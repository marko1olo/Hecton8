# Rationale_13VOD

## Session Bootstrap
Problem: No `<AGENT_PROMPT id="13VOD">` exists in `Docs/Tasks/CURRENT_BATCH.md`; user supplied a direct domain assignment.
Solution: Treat the direct user message as one broad audit-and-fix directive and keep evidence in 13VOD status/log files.
Rejected Alternatives: Claiming a nonexistent XML task count would corrupt the batch protocol; reading neighboring prompt blocks would pollute scope.
Scalability potential: Audit will target continuous `GlobalQualityWeight`, Math LOD, and visual-fake-first water behavior across weak, middle, high, and ultra devices.
Hardware Impact: None yet. Baseline discovery only; no runtime code changed.

## CurrentVolume Read-Accessor Purity
Problem: `CurrentVolume.SampleAt`, `SampleCombinedCurrent`, and `Sample` were read paths but refreshed transform/AUP/sample-time caches during sampling. Hot consumers include buoyancy gather, player current, ambient motion, flow visualizer, tether force, and `HectonFluidEngine` read models.
Solution: Moved cache refresh into dispatcher-owned update/fixed lanes, kept sample methods reading cached fields, and added dispatcher hot-swap rebind. Shared ambient sample time now has an owner-phase writer and a pure fallback read.
Rejected Alternatives: Recomputing matrices per sample was pure but burns CPU across every current consumer; migrating all consumers to a new native snapshot contract is correct long-term but too broad for this pass.
Scalability potential: Low devices avoid repeated transform/AUP cache writes inside every sample; middle/high/ultra keep continuous authored flow vectors and can spend saved cycles on denser presentation.
Hardware Impact: Estimated 2-8 us/frame saved when 8-32 volumes are sampled by buoyancy/player/presentation paths; zero managed allocation added.

## CurrentVolume Direction Fidelity and Capacity
Problem: Authored directional/radial/vortex currents collapsed to dominant axes, producing taxicab water forces; active volume `List`/`HashSet` had initial capacity but no hard runtime cap.
Solution: Replaced axis snapping with finite `rsqrt` normalization and capped active registration at 32 with dev-build error reporting.
Rejected Alternatives: Keeping axis snapping was cheaper but visibly wrong and polluted gameplay drag coupling; allowing registry growth is easy but violates deterministic capacity.
Scalability potential: Weak devices still use scalar math only; high/ultra devices receive smooth authored flow without changing storage layout.
Hardware Impact: Adds a few scalar ops per affected volume sample; avoids managed resize spikes if content exceeds the authored volume cap.

## Seaglide Quality-Invariant Force Law
Problem: `GlobalQualityWeight` changed seaglide authoritative force output by blending speed approximation, drag law, fallback flow magnitude, and flow force strength.
Solution: Made thrust/drag/flow forces use the quality-1 deterministic path for every device; `GlobalQualityWeight` now only scales cavitation presentation in this job.
Rejected Alternatives: Keeping low-quality force shortcuts would make hardware settings alter movement truth; adding another approximate force path needs profiler/error proof not present here.
Scalability potential: Weak devices pay a small exact-math cost for identical physics; high/ultra spend quality on cavitation/VFX rather than authority divergence.
Hardware Impact: Estimated +0.2-1.0 us for active seaglide rows on i3/MX350 class hardware; removes cross-device movement divergence.

## Ocean Provider Arbitration Cadence
Problem: `OceanKinematicsRuntimeService.Tick` refreshed provider availability every core frame, polling Crest/fallback providers in a hot lane.
Solution: Provider selection refreshes immediately on register/unregister/hot-swap and otherwise probes availability at 0.5 s cadence.
Rejected Alternatives: Removing all probes risks missing late Crest collision-provider readiness; keeping every-frame arbitration violates no-hot-polling.
Scalability potential: Weak devices avoid needless per-frame Crest provider checks; high/ultra keep fast enough recovery for late provider availability.
Hardware Impact: Estimated 0.5-3 us/frame saved while the ocean runtime is active, depending on provider count and Crest readiness.

## Ambient Water Motion LOD
Problem: Ambient decorative water motion used hard distance bands without `GlobalQualityWeight` and flickered when objects sat near thresholds.
Solution: Added one-byte per-object LOD band hysteresis and quality-scaled cadence masks; gameplay authority is untouched.
Rejected Alternatives: Full per-object native LOD tables were excessive for 128 decorative props; leaving fixed divisors ignored the continuous-quality mandate.
Scalability potential: Low quality keeps authored cadence; middle/high/ultra continuously increase visual update density while hysteresis prevents band chatter.
Hardware Impact: Estimated neutral to -4 us/frame on low-end scenes with many props; one byte state per prop and no managed allocations.

## Crest Underwater Pass Bridge
Problem: `CrestBridge.EnsureUnderwaterPass` could run `GetComponent` from repeated underwater ownership calls, and `_copyOceanMaterialParamsEachFrame` was assigned every time.
Solution: Return the cached `UnderwaterRenderer` when it already belongs to the camera; write the Crest copy flag only when it changes.
Rejected Alternatives: Rewriting `HectonUnderwaterVisuals` camera-stack ownership is a larger visual-system change; this bridge fix removes the hot component lookup without moving ownership.
Scalability potential: Weak devices avoid repeated Unity component lookup; high/ultra keep the same Crest visual pass without extra bridge churn.
Hardware Impact: Estimated 0.5-2 us/frame saved when underwater visuals are active and ownership checks run every LateFrame.

## Surface Weather Cached Water Routes
Problem: Surface weather visual sync read `GlobalRegistry.FluidSurfaceCurrent` and `GlobalRegistry.OceanKinematics` through `ResolveSurfaceY()` / `ResolveOceanKinematics()` while applying water and ocean bindings.
Solution: Cache `IFluidSurfaceCurrentReadModel` and `IHectonOceanKinematicsService` during cold dependency resolve and hot-swap callbacks; LateFrame reads cached interfaces only.
Rejected Alternatives: Leaving registry fallback in visual sync hides boot-order bugs and violates cold DI ownership; pushing all weather to a new SignalBus snapshot is better long-term but broader than this pass.
Scalability potential: Weak devices avoid registry route checks during active rain/ocean binding; middle/high/ultra retain exact same water state while spending frame time on VFX density.
Hardware Impact: Estimated 0.2-1.5 us/frame saved when surface weather is active on i3/MX350 class hardware.

## Swim Presentation Single Driver
Problem: `PlayerSwimPresentationController` registered as an updatable owner while `HectonPlayerMovement` also force-drove `SyncFromLocomotion()` in the same render frame.
Solution: Removed dispatcher update ownership from swim presentation; movement is the single state driver and the presentation controller keeps only LateFrame shader flushing.
Rejected Alternatives: Keeping a frame guard still leaves two owners and order-dependent smoothing if the frame source changes; moving camera juice away from movement is too broad.
Scalability potential: Weak devices avoid duplicate presentation integration; high/ultra keep richer pose/shader work without non-deterministic stroke phase drift.
Hardware Impact: Estimated 2-8 us/frame saved while swimming; primary gain is deterministic presentation state, not raw CPU.

## Swim Hot Lookup Cleanup
Problem: Swim presentation retried `TryGetComponent` for `PlayerTransportCoordinator` from hot locomotion feel code, and movement helpers used `GlobalRegistry.PhysicsStateEvents` fallback from movement/impact paths.
Solution: Runtime feel code now reads cached coordinator only; movement helper paths use cached `_physicsStateEvents` populated by dependency injection and hot-swap.
Rejected Alternatives: Lazy fallback is convenient but turns missing dependency wiring into hot polling and hides route failures.
Scalability potential: Weak devices avoid Unity component lookup and registry fallback during transport/swim contact frames; high/ultra keep the same feel contract.
Hardware Impact: Estimated 0.5-3 us/frame saved on transport or wall-contact frames.

## Ambient Water Motion Rest AUP and Flow Direction
Problem: Decorative water motion never captured rest AUP, so distance LOD treated those objects as always-near; visual current direction also collapsed diagonal currents to a dominant axis.
Solution: Capture rest AUP once from runtime position, put no-AUP fallback objects on medium cadence, and normalize visual current direction with `rsqrt`.
Rejected Alternatives: Per-frame AUP reconstruction would be accurate but moves AUP work into every visual tick; dominant-axis direction was cheap but visibly wrong.
Scalability potential: Weak devices cadence-throttle unbound decorative props; middle/high/ultra retain continuous quality-scaled update density and smoother diagonal sway.
Hardware Impact: Estimated neutral to -4 us/frame in prop-heavy scenes; adds one cold AUP capture per ambient object.

## Water Transition and Crest Depth-Cache Cadence
Problem: Surface-exit gravity scanned the whole `WaterTransitionSignal` snapshot on repeated fixed calls in the same dispatcher frame; disabled Crest runtime depth-cache sync still reached hierarchy reference resolution from LateFrame.
Solution: Gate signal snapshot consumption to once per dispatcher frame and early-exit disabled depth-cache runtime sync before `GetComponentsInChildren` reference traversal.
Rejected Alternatives: A dedicated per-player queued transition lane is cleaner but broader; enabling Crest runtime depth cache conflicts with the existing RenderGraph ocean-depth validator.
Scalability potential: Weak devices avoid repeated fixed scans and visual-sync hierarchy traversal; high/ultra keep the current RenderGraph depth route until a verified continuous-quality depth cache replacement exists.
Hardware Impact: Estimated 0.5-5 us/frame saved during transition-heavy or depth-cache sync frames.

## Ocean Adapter Vault Publish Guard
Problem: `OceanAdapterVaultRoute.TryPublishWaterLevel` and `TryRecordTelemetry` called `TryOpenOrAcquireLane`, so a runtime publish/telemetry helper could allocate or grow DataVault generation handles if boot did not establish the lanes first.
Solution: Keep `TryAcquireBootHandles` as the only allocator and change publish/telemetry helpers to `TryOpenExistingLane`, which fails closed unless an exact BufferID/generation handle already exists.
Rejected Alternatives: Silent runtime repair through `EnsureGenerationHandle` hides boot-order defects and turns GlobalDataVault into a global heap; replacing the whole ocean query route is broader and needs separate owner migration.
Scalability potential: Low devices avoid an unexpected allocation/generation hitch on water-level or telemetry write; middle/high/ultra keep the same DTO route and can spend budget on ocean visuals instead of recovery work.
Hardware Impact: 0.0 us steady-state saving claimed from static proof; prevents an unmeasured allocation/generation fallback spike on i3/MX350 class hardware when boot wiring is broken.

## Crest Material Integrity
Problem: `Crest4KinematicsAdapter.TryGetSurfaceWeatherState` advertised foam controls after reading `OceanRenderer.OceanMaterial`, and `ApplySurfaceWeatherState` wrote `_WaveFoamStrength`, `_WaveFoamCoverage`, and `_FoamScale` with `Material.SetFloat`.
Solution: Treat Crest ocean material foam as authored donor state, not runtime weather output. The legacy bridge now reports/applies only Crest wind speed; first-party weather foam remains carried by Hecton shader globals, VFX, and `ShinobuOceanSurfaceAtmosphereRuntime`.
Rejected Alternatives: MaterialPropertyBlock is forbidden for standard geometry here and would still be a runtime override path; keeping `SetFloat` violates the Crest third-party integrity rule; cloning material at runtime is explicitly banned.
Scalability potential: Weak devices avoid material property churn and SRP/material-state ambiguity; middle/high/ultra can still spend quality on first-party foam/VFX routes without mutating donor Crest assets.
Hardware Impact: 0.0 us steady-state saving claimed from static proof; removes material override risk and small unmeasured material property churn from surface-weather transitions.

## Surface Weather Math Buffer Hot-Route Guard
Problem: `HectonSurfaceWeatherDirector.ScheduleWeatherMathJob` and `TryCompleteWeatherMathJob` called `TryOpenOrAcquireWeatherJobOutput`, so water/ocean weather binding could reach an acquire wrapper from Tick/LateFrame paths if the weather job output handle was missing.
Solution: Hot schedule/complete/cold-seed paths now call `TryOpenWeatherJobOutput` only. Buffer acquisition stays in `EnsureWeatherMathBuffers`, which is part of runtime initialization, not the recurring schedule/complete path.
Rejected Alternatives: Leaving acquire in the hot helper hides boot-order defects and mirrors the DataVault heap fallback pattern fixed in the ocean adapter; moving the whole weather system to a new signal/vault contract is broader and not required for this defect.
Scalability potential: Low devices avoid hidden allocation attempts during weather-to-water binding; middle/high/ultra keep the same math output and can spend quality on VFX/shader density.
Hardware Impact: 0.0 us steady-state saving claimed from static proof; prevents an unmeasured DataVault allocation attempt in water/ocean surface-weather scheduler fault cases.

## Underwater Shared Ocean Material Write Purge
Problem: `HectonUnderwaterVisuals.ApplyOceanMaterialBindings` and `ApplyGIRelaySurfaceEmission` still resolved `bridge.OceanMaterial` and wrote first-party underwater scatter/GI parameters into the shared ocean material. With Crest active, that is a runtime override of a third-party ocean asset.
Solution: Keep writes on first-party `oceanUnderwaterMaterial` and shader globals only. `ResolveOceanMaterial` remains available for read-only fallback sampling, but underwater visual sync no longer applies material bindings or GI relay color to the shared bridge material.
Rejected Alternatives: MaterialPropertyBlock is forbidden for standard geometry and would still create a parallel override path; leaving writes in place violates Crest integrity; deleting all read-only fallback sampling is riskier and not required for this defect.
Scalability potential: Low devices avoid material-state churn and SRP batching ambiguity; middle/high/ultra can still scale underwater visual density through first-party shader globals, render passes, and VFX without mutating Crest material assets.
Hardware Impact: 0.0 us steady-state saving claimed from static proof; removes unmeasured material property churn and third-party asset mutation risk.

## Surface Weather Hot Service-Cache Closure
Problem: `HectonSurfaceWeatherDirector` still read `GlobalRegistry.CelestialEngine` while publishing weather shader globals and `GlobalRegistry.Audio` when flushing thunder playback. These are water/weather presentation paths reached from LateFrame-style output flushes.
Solution: Cached `IAudioService` alongside the existing fluid/ocean/celestial dependencies, refreshed it through cold dependency resolve and `OnGlobalRegistryServiceReplaced`, and made shader-global publication check the cached `celestialEngine` field only.
Rejected Alternatives: Leaving direct registry reads because thunder is rare would preserve a hidden cross-domain dependency in a water/weather owner; pushing thunder through a new signal lane is broader and unnecessary because `IAudioService` already has a registry slot and hot-swap event.
Scalability potential: Low devices avoid repeated registry route checks in active weather output; middle/high/ultra keep the same shader/audio behavior and spend quality on VFX density, not dependency polling.
Hardware Impact: Estimated 0.0-0.5 us/frame saved in active surface-weather output, depending on weather state; primary gain is deterministic dependency ownership with no managed allocation added.

## Underwater GI Relay Authority Cache
Problem: `HectonUnderwaterVisuals.IsGIRelayAmbientAuthorityActive()` was a static helper that polled `GlobalRegistry.GIRelay` from underwater ambient/render-settings application paths.
Solution: Added a cached `IGIRelaySystem` dependency, populated it during runtime dependency cache and `GIRelayRuntime` hot-swap, and made the authority check an instance read over the cached field.
Rejected Alternatives: Keeping the registry read because it is a cheap boolean check still violates hot-route dependency ownership; moving GI relay authority into a new water signal is broader because the service already exposes the exact read model.
Scalability potential: Low devices avoid global lookup churn during underwater lighting updates; middle/high/ultra keep GI relay ambient authority without tying visual density to registry polling.
Hardware Impact: Estimated 0.0-0.5 us/frame saved in active underwater visual frames; no managed allocation added.

## Underwater Biome Fog DataVault Prewarm Gate
Problem: `HectonUnderwaterVisuals.ScheduleBiomeFogBlendJob()` could call `EnsureBiomeFogBlendBuffers()` from the recurring slow-tick transition scheduler, and that path could reach `GlobalDataVault.EnsureGenerationHandle`.
Solution: Added an `allowAcquire` gate. Runtime dependency caching and DataVault hot-swap prewarm/acquire buffers with `allowAcquire: true`; the recurring scheduler uses `allowAcquire: false` and only opens already-created handles.
Rejected Alternatives: Letting the scheduler repair missing buffers hides boot-order defects and turns a visual transition into an allocation point; disabling biome fog blending entirely would remove a useful visual fake rather than fixing ownership.
Scalability potential: Low devices avoid allocation/generation hitches during biome transitions; middle/high/ultra keep the job-based visual fog blend when boot prewarm succeeds.
Hardware Impact: 0.0 us steady-state saving claimed; removes an unmeasured fault-path allocation/generation hitch on i3/MX350 class hardware.

## Crest Depth-Cache Disabled/Celestial Route Cleanup
Problem: `HectonCrestOceanDepthCacheBootstrap` could keep scheduling disabled runtime depth-cache visual sync and call `TryGetComponent` from that path; tidal height cache modulation also polled `GlobalRegistry.CelestialEngine`.
Solution: Disabled runtime depth-cache mode now clears pending sync in `SlowTick` and exits `TryConfigureAndPopulate` before component resolution. The celestial engine is cached in cold runtime setup and refreshed via `CelestialEngineRuntime` hot-swap.
Rejected Alternatives: Leaving the disabled path alive as a harmless diagnostic route still costs recurring work; enabling Crest runtime depth cache remains rejected until the RenderGraph depth route is replaced with proof.
Scalability potential: Low devices avoid component lookup and disabled visual sync churn; middle/high/ultra retain authored/local depth caches and tidal modulation through cached dependencies.
Hardware Impact: Estimated 0.0-2.0 us/frame saved only when disabled depth-cache sync was pending; prevents hidden component lookup in the disabled route.

## Ocean Registry Read-Accessor Purity
Problem: `HectonOceanRegistry.ActiveProvider` was a property getter but could call `OceanKinematicsRuntimeService.EnsureRuntimeInstance()`, creating a runtime service GameObject/AddComponent when a read facade was accessed before bootstrap.
Solution: The getter now reads only existing `GlobalRegistry.OceanKinematics` or an already-registered `OceanKinematicsRuntimeService`, returning null if neither exists.
Rejected Alternatives: Auto-repairing from a getter hides bootstrap defects and violates read accessor purity; moving all callers immediately to injected `IHectonOceanKinematicsService` is cleaner but broader.
Scalability potential: Low devices avoid a fault-path allocation/hierarchy mutation during ocean queries; middle/high/ultra keep the same provider route after bootstrap.
Hardware Impact: 0.0 us steady-state saving claimed; prevents a cold fault-path GameObject/AddComponent allocation from a gameplay read.

## Analytical Flow Direction Fidelity
Problem: `HectonFluidEngine` normalized some authored water forces by snapping them to dominant axes: active thruster flow direction, abyssal vortex axis, cavitation burst direction, and cavitation shockwave radial vectors. This turns diagonal currents and impulses into taxicab water behavior and changes gameplay-facing current force direction without a documented LOD contract.
Solution: Preserve finite authored direction with scalar normalization for event/setter paths and the analytical thruster flow application. Existing dominant-axis usage remains only in explicit low-detail vector-noise/surface-normal approximation paths where the code already has a detail flag.
Rejected Alternatives: Keeping axis quantization for "stability" was rejected because the input validation already rejects non-finite/zero vectors and `math.rsqrt` normalization is bounded; using `GlobalQualityWeight` to switch between axis and normalized force was rejected because quality must not alter gameplay truth.
Scalability potential: Low devices still avoid dense fluid simulation and keep low-detail noise approximations; middle/high/ultra receive correct diagonal thruster/vortex/cavitation response without changing buffer layout or authority ownership.
Hardware Impact: 0.0 us steady-state saving claimed. Setter/event paths pay a few scalar operations only when a thruster/vortex/cavitation burst is queued; no managed allocation added.

## Submarine Depressurization Direction Fidelity
Problem: `SubmarineFluidDynamics.ResolveDepressurizationAcceleration` and `SafeNormalize` used dominant-axis snapping for breach suction and breach probe vectors. That made pressure/flood response pull bodies along cardinal axes instead of toward the actual breach point.
Solution: Changed depressurization acceleration and compartment breach probe direction to finite scalar normalization, and removed the dead private dominant-axis helper after all call sites were gone.
Rejected Alternatives: Keeping axis snapping as a cheap pressure fake was rejected because this is gameplay-facing force direction, not a visual-only slosh cue; making it quality-dependent was rejected because quality cannot alter physics truth.
Scalability potential: Low devices keep coarse compartment/flood math and bounded contact collection; middle/high/ultra get correct diagonal pressure direction without changing DTOs, DataVault lanes, or job schedules.
Hardware Impact: 0.0 us steady-state saving claimed. Breach/pressure paths pay a few scalar ops only when resolving depressurization/probe direction; no managed allocation added.

## Biome Matrix Runtime Component-Search Guard
Problem: `BiomeMatrixDirector.ResolveReferences()` could call `playerTransform.TryGetComponent(out _playerMovement)` from runtime slow-tick refresh if the player movement dependency was missing. That hides broken `IPlayerRuntimeContext` wiring and violates the no scene/component search rule for runtime water/biome evaluation.
Solution: Keep the fallback component lookup only outside Play Mode. Runtime now uses `IPlayerRuntimeContext.PlayerMovement` from cold cache/hot-swap; if the dependency is missing, depth/water evaluation fails visibly instead of searching the scene.
Rejected Alternatives: Keeping the fallback because it is slow-tick, not per-frame, was rejected because the doctrine bans hot route repair through component search; adding another registry lookup was rejected because the runtime context already owns the fact.
Scalability potential: Low devices avoid broken-DI lookup churn; middle/high/ultra keep exact same biome/water behavior when the player context is wired correctly.
Hardware Impact: Estimated 0.0-0.5 us per slow-tick only in broken dependency cases; no managed allocation added.

## Swim Blockout Rig Single Driver
Problem: `PlayerSwimBlockoutRig` registered itself as an updatable `ITickable/IUpdatable` while `PlayerSwimPresentationController.LateFrameTick()` also called `swimBlockoutRig.SyncFromPresentation(dt, true)`. The forced late-frame call bypassed the rig frame guard, so the rig could integrate pose twice in the same frame.
Solution: Removed the rig's update/tick ownership and `TryRegisterUpdatable` path. The presentation controller remains the sole pose driver; the rig keeps only `ILateFrameTickable` for queued renderer visibility flush.
Rejected Alternatives: Leaving the rig self-driven for fallback convenience was rejected because two owners create order-dependent pose blend and extra per-frame work; removing the controller call was rejected because the controller owns the presentation truth and already has mode/profile state.
Scalability potential: Low devices avoid duplicate arm/body pose math; middle/high/ultra keep the same visible swim rig while spending budget on richer presentation rather than repeated integration.
Hardware Impact: Estimated 2-8 us/frame saved while the swim blockout rig is active; no managed allocation added.

## Ocean Adapter Sample Authority Invariance
Problem: `EmergencyMockOceanKinematicsAdapter` and `CrestOceanRuntimeAdapter` accepted `GlobalQualityWeight` and used it to change ocean sample amplitude, detail waves, budget simplification, latency, and `SimplifiedByQualityBudget` status. Those DTOs carry `WaterHeight`, `SurfaceVelocity`, and `WaveNormal`, so hardware quality could change water/buoyancy truth.
Solution: The deferred/mock sample jobs now ignore the visual quality scalar and use one deterministic wave approximation for every device. `GlobalQualityWeight` remains part of higher-level visual/telemetry contracts, not this authority sample output.
Rejected Alternatives: Keeping quality-budgeted water height was rejected because it makes physics/sample truth device-dependent. Keeping only the simplified flag while computing full results was rejected because it would lie to consumers.
Scalability potential: Low devices still use the cheap analytical fallback rather than Crest readback truth; middle/high/ultra should buy visual overkill through first-party ocean shaders/VFX, not different water heights.
Hardware Impact: 0.0 us saving claimed. Low tier may pay extra polynomial wave terms for full deterministic fallback output; no managed allocation, DTO layout, or authority route change was added.

## Ocean Kinematics Vault Authority Invariance
Problem: `OceanKinematicsJobs` and `OceanKinematicsVaultRuntime` still used `GlobalQualityWeight` to pick active wave octaves, sine/cosine polynomial fidelity, active-octave telemetry fallback, and macro-state rollback hash input. That means water query DTOs and rollback/sync identity could vary by hardware quality.
Solution: DataVault analytical/mock wave jobs now always use the configured max octave limit and full cheap polynomial path. Macro state still records quality as telemetry, but active-octave resolution and macro hash identity no longer depend on it.
Rejected Alternatives: Keeping quality-scaled octaves was rejected because it changes `WaterHeight`, `SurfaceVelocity`, and `WaveNormal`; keeping quality inside the macro hash was rejected because presentation quality must not alter authority/sync identity.
Scalability potential: Low devices pay the full cheap analytical fallback path for identical water truth; middle/high/ultra should use quality for visual foam, spray, caustics, shader density, and telemetry cadence, not different ocean authority.
Hardware Impact: 0.0 us saving claimed. Low tier may spend several extra scalar wave terms per scheduled query batch; no managed allocation, DTO layout, DataVault lane, or authority route change was added.

## Analytical Buoyancy Wave Authority Invariance
Problem: `AnalyticalGerstnerWaveJobs` used `GlobalQualityWeight` to generate mock spectrum steepness/jitter, choose active octave budget, fade octave weights, and blend cubic versus seventh-order trig. Those values directly feed `OceanSampleResultDTO.WaterHeight`, `SurfaceNormal`, and `Displacement` for buoyancy/water samples.
Solution: Mock spectrum generation now uses one deterministic full-fidelity cheap spectrum. Analytical evaluation always uses the configured max octave limit, full octave weights, and quality-1 polynomial path; quality remains recorded in tuning/telemetry only.
Rejected Alternatives: Keeping low-quality octave/trig shortcuts was rejected because it changes buoyancy water truth per device. Splitting DTOs into visual and authority lanes is the correct larger design, but this pass fixes the existing authority lane without changing layout.
Scalability potential: Low devices still use the SHINOBU_263 analytical fake instead of physical ocean simulation; middle/high/ultra should buy overkill through visual foam, caustics, spray, and ocean shaders, not different buoyancy heights.
Hardware Impact: 0.0 us saving claimed. Low tier may pay additional scalar polynomial/octave work per query batch; no managed allocation, DTO layout, DataVault lane, or authority route change was added.

## SIMD Buoyancy Hydrodynamic Force Invariance
Problem: `BuoyancySimdVectorization` used `GlobalQualityWeight` and `ApproximationQualityWeight` to alter turbulence acceleration and sine polynomial fidelity inside vectorized/scalar hydrodynamic force jobs. The output is `OutputForces`, so hardware quality changed physics forces.
Solution: Force jobs now use the full cheap sine polynomial path (`quality=1`, degree 7) and apply authored `TurbulenceAmplitude` directly. Quality remains available for telemetry/tuning metadata, not force math.
Rejected Alternatives: Leaving approximation knobs in force jobs was rejected because they are not visual LOD; they change acceleration. Moving turbulence to a separate visual-only wake lane is a larger design and not required to stop device-dependent force output here.
Scalability potential: Low devices keep SIMD/vectorized fake hydrodynamics instead of expensive fluid simulation; middle/high/ultra should spend saved/available quality on water visuals, not alternate force truth.
Hardware Impact: 0.0 us saving claimed. Low tier may pay seventh-order polynomial math in force lanes; no managed allocation, DTO layout, DataVault lane, or schedule change was added.

## Async Buoyancy Readback Authority Invariance
Problem: Async GPU/mock buoyancy readback still used `GlobalQualityWeight` to choose request sample budget, active shader wave count/contribution, smoothing alpha, dead-reckoning decay, mock ripple amplitude, and CPU wave phase direction fidelity. Those paths feed `ReadbackResolvedHeightDTO.HeightAupY`, `LocalHeight`, `VelocityY`, and request `ResultHeight`.
Solution: Added one explicit authoritative quality constant and routed the readback authority lane through it. Sample budget now uses the configured max, smoothing/dead-reckoning use the previous quality-1 constants, GPU wave contribution is full, mock ripple amplitude is fixed, and CPU phase direction uses the full cheap trig approximation.
Rejected Alternatives: Keeping quality-scaled readback was rejected because it makes buoyancy height truth hardware-dependent. Moving this immediately into separate visual/authority shader kernels is cleaner long-term but broader and unnecessary for stopping the current violation.
Scalability potential: Low devices keep async GPU/mock readback as the cheap fake instead of physical water simulation, but they do not receive different buoyancy heights. Middle/high/ultra should spend quality on visual wave detail, foam, spray, caustics, and telemetry cadence outside this authority lane.
Hardware Impact: 0.0 us saving claimed. Low tier may pay max request budget and full wave contribution in the async height path; no managed allocation, DTO layout, DataVault lane, or authority route change was added.

## Submarine Ballast Buoyancy Authority Invariance
Problem: `SubmarineAutoLevelBallastController` and `SubmarineBallastBuoyancyContracts` used `GlobalQualityWeight` to select active ballast sample budget and mock fluid density, and the force job had a quality-derived fallback sample count. Those values feed `SubmarineBallastForcePacketDTO.NetForce`, then `QueueAmbientForce`, so hardware quality changed submarine buoyancy.
Solution: Added a ballast authoritative quality constant, removed quality-driven budget smoothing/hysteresis, made the active sample budget always use the full four cheap probe points, and made mock fluid density use the previous quality-1 path. `GlobalQualityWeight` remains written only as tuning/telemetry metadata.
Rejected Alternatives: Keeping quality-scaled sample count was rejected because `ActiveSamples` changes submerged ratio and force output. A separate visual ballast slosh lane is a valid larger design, but this pass fixes the existing force lane without changing DTO layout or DataVault ownership.
Scalability potential: Low devices still use the cheap four-point ballast fake instead of real pressure/fluid simulation; middle/high/ultra can spend quality on bubble, wake, ballast hiss, and interior slosh visuals outside the authoritative force packet.
Hardware Impact: 0.0 us saving claimed. Low tier may pay up to four scalar submerged-ratio samples and fixed high mock density math; no managed allocation, DTO layout, DataVault lane, or force route change was added.

## Submarine 6DOF Hydrodynamics Authority Invariance
Problem: `SubmarineDynamicsContracts` used `GlobalQualityWeight` inside submarine movement authority: mock fluid density micro-layer bias, added-mass tensor blend, rotational damping, and 6D integrator update cadence. Those paths change linear velocity, angular velocity, rotation, and hydrodynamics telemetry hashes.
Solution: Added a submarine dynamics authoritative quality constant. The density micro-layer, tensor blend, and rotational damping now use the authoritative value, while the integrator authority cadence resolves to full update fraction. Existing `GlobalQualityWeight` fields remain as telemetry/input metadata, not physics math.
Rejected Alternatives: Keeping quality cadence and tensor fallback was rejected because dead-reckoning/skipped solver frames and reduced tensor blend alter hull motion. Splitting a low-cost visual-only submarine sway lane is valid later, but this pass fixes the actual 6DOF authority path.
Scalability potential: Low devices keep the cheap analytical submarine hydrodynamics fake, but movement is identical to high/ultra. Middle/high/ultra should spend quality on exterior wake, cavitation visuals, interior vibration, and horizon UI, not alternate hull physics.
Hardware Impact: 0.0 us saving claimed. Low tier may pay full authority cadence and full tensor/damping path; no managed allocation, DTO layout, DataVault lane, or signal route change was added.

## Submarine Mock Flood Signal Authority Invariance
Problem: `SubmarineDynamicsRuntime.TryPushMockFloodSignal` used `GlobalQualityWeight` to change fallback flood signal probability from 1/96 to 1/16. When `enableMockSignals` is active, that changes `MockFloodSignal.WaterMassKg`, flood mass state, center of mass, and 6DOF hull dynamics by hardware quality.
Solution: Removed the quality parameter and fixed the fallback probability to the previous quality-1 path. `ResolveMathLodQualityWeight()` remains available for telemetry and visual/policy metadata, not mock water-mass event generation.
Rejected Alternatives: Keeping mock-only quality scaling was rejected because fallback routes still feed authoritative state when enabled. Disabling mock signals outright was rejected because the route is useful for isolated tests and fallback diagnostics.
Scalability potential: Low devices and high/ultra now receive identical fallback flood event cadence. Device quality should buy presentation overlays, alarms, interior water visuals, and audio, not different water-mass injection.
Hardware Impact: 0.0 us saving claimed. Low tier may see the same higher fallback event probability as high/ultra when mock signals are enabled; no managed allocation, DTO layout, SignalBus lane, or DataVault route change was added.

## Airlock Pressure/Water Authority Invariance
Problem: `AirlockPressurizationRuntime` and `EvaluateAirlockCyclesJob` used `GlobalQualityWeight` to change simulation tick interval and pump pressure equalization speed. That changes airlock water volume, pressure state, collision blocking, stress spikes, and telemetry hashes by device quality.
Solution: Added an explicit airlock authoritative quality constant and `ResolveAuthorityTickInterval()`. Runtime cadence and telemetry tick reporting now use the authority interval, and pump equalization uses the authority scalar. The job keeps the visual quality scalar only for bubble and acoustic signal cadence.
Rejected Alternatives: Keeping quality-scaled tick cadence was rejected because accumulated `DeltaTime` and pump equalization alter authority state. Making low-tier airlocks update less often is acceptable only for a separate visual facade, not this water/pressure DTO lane.
Scalability potential: Low devices keep the cheap Torricelli/equalization fake but receive identical pressure/water truth; middle/high/ultra should spend quality on bubbles, fog, acoustic density, UI, and shader presentation.
Hardware Impact: 0.0 us saving claimed. Low tier may pay full 0.016 s authority cadence in the airlock fake; no managed allocation, DTO layout, DataVault lane, or signal route change was added.

## Abyssal Cavitation Force Authority Invariance
Problem: `AbyssalCavitationRuntime` used `GlobalQualityWeight` inside authority cavitation paths. Mock detonations changed shockwave max radius, peak pressure, and expansion speed. Force packet evaluation changed noncritical candidate acceptance, radius scale, shell width, SDF multi-tap dampening, and SDF interpolation. Those values feed `ShockwaveForcePacketDTO.Force`, `Pressure`, and transport `ForceVector`.
Solution: Added `AbyssalCavitationConstants.AuthoritativeQualityWeight = 1f` and routed mock shockwave generation plus force packet/SDF authority math through it. Existing `GlobalQualityWeight` remains available for tuning telemetry and visual sphere intensity/quality metadata only.
Rejected Alternatives: Keeping quality-scaled cavitation forces was rejected because force packets are gameplay authority, not presentation. Disabling SDF multi-tap on low tier was rejected because it changes occlusion truth; if performance is a problem, the visual fake must move to a separate non-authority lane.
Scalability potential: Low devices keep the cheap shockwave/SDF fake but receive identical impulses. Middle/high/ultra should spend quality on visual cavitation spheres, foam, shock rings, audio, and post effects, not alternate physics packets.
Hardware Impact: 0.0 us saving claimed. Low tier may pay the full cheap SDF interpolation/multi-tap and mock shockwave path; no managed allocation, DTO layout, DataVault lane, or force route change was added.

## Vehicle Damage Flood/Buoyancy Authority Invariance
Problem: `VehicleComponentDamage` used `GlobalQualityWeight` inside submarine-adjacent damage authority. Mock damage signal count, mock signal integrity/radius/armor pierce, explosive propagation radius cap, and fire probability changed by device quality. The resulting state feeds `FloodWaterMassKg`, `BuoyancyScalar`, breach/fire hazards, and `StateHash`.
Solution: Added `VehicleDamageConstants.AuthoritativeQualityWeight = 1f`. Mock signal generation and explosive reduction now use the authority constant. Mock signal count resolves to the full authored count. `EvaluateVehicleSystemsJob` still records visual quality in `state.QualityWeight`, but fire probability uses the authority constant.
Rejected Alternatives: Keeping quality-scaled damage spread was rejected because damage/flood/buoyancy are gameplay state. Reducing flood/damage math on low tier through quality was rejected; a separate visual damage decal or alarm cadence lane is the correct scalability route.
Scalability potential: Low devices keep the cheap grid damage fake but receive identical flood/buoyancy state. Middle/high/ultra should spend quality on hull decals, sparks, leak particles, waterline visuals, alarm audio, and UI detail.
Hardware Impact: 0.0 us saving claimed. Low tier may pay full mock signal count and full six-cell propagation cap; no managed allocation, DTO layout, DataVault buffer, SignalBus route, or state hash layout change was added.

## Habitat Flood Solver Authority Invariance
Problem: `HabitatFluidIncursionDirector` used `GlobalQualityWeight` to choose fixed solver cadence, BFS node budget, and solver iteration count. Those knobs change compartment water volumes, pressure equalization, flood mass, dynamic center of mass, flood signals, and summary hashes by device quality.
Solution: Routed solver cadence, BFS node budget, and solver iterations through `HabitatFluidIncursionMath.AuthoritativeQualityWeight`. Removed the unused quality field from `FluidIngressJob`. `GlobalQualityWeight` remains in the tuning DTO for visual waterline wobble and metadata.
Rejected Alternatives: Keeping quality-scaled cadence was rejected because accumulated `solverDeltaTime` and skipped BFS nodes alter flood truth. Reducing BFS on weak devices was rejected because topology traversal changes which rooms exchange water.
Scalability potential: Low devices keep the cheap compartment graph fake but solve the same graph as high/ultra. Middle/high/ultra should spend quality on waterline shader wobble, audio muffling density, internal flood visuals, and UI diagnostics.
Hardware Impact: 0.0 us saving claimed. Low tier may pay full 0.016 s authority cadence plus max BFS/iteration budget; no managed allocation, DTO layout, DataVault buffer, or SignalBus route change was added.

## Sump Pump Drainage Cadence Authority Invariance
Problem: `SumpPumpPipeGridRuntime.SlowTick()` used `GlobalQualityWeight` to choose drainage solve cadence. This changes pump evacuation timing, quantized pump remainders/mass error, frame evacuated volume, and drainage state hash timing by device quality.
Solution: Added `SumpPumpPipeGridConstants.AuthoritativeQualityWeight = 1f` and routed solver cadence through `ResolveAuthoritySolveCadenceSeconds()`. The live quality value is still passed to the solver for tuning telemetry and visual flow publication budgets.
Rejected Alternatives: Keeping low-quality slow cadence was rejected because accumulated solve windows and quantized pump evacuation are authority state. Changing visual spline flow publish budget was rejected because that path only calls `ConnectionSplineBatchRenderer.SetPipeNodeFlow`.
Scalability potential: Low devices keep the cheap graph/pump fake but run the same drainage cadence as high/ultra. Middle/high/ultra should spend quality on pipe flow visuals, pump hum, spray, panel readouts, and diagnostics.
Hardware Impact: 0.0 us saving claimed. Low tier may pay the same 0.1 s drainage solve cadence as high/ultra; no managed allocation, DTO layout, DataVault buffer, or SignalBus route change was added.

## Bulkhead/Hatch Pressure Cadence Authority Invariance
Problem: `BulkheadContainmentRuntime` used `GlobalQualityWeight` to choose authority cadence from 5 Hz to 30 Hz, while hatch pressure locks used quality to choose tick interval from 0.2 s to 0.016 s. `UpdateHatchFsmJob` also used quality to scale slam `MovementAcousticSignal.Volume`, which can affect non-visual consumers.
Solution: Bulkhead authority cadence now resolves from the explicit authority quality constant. Hatch tuning and runtime pressure accumulation use `HatchLockMath.ResolveAuthorityTickIntervalSeconds()`. The hatch FSM receives an explicit acoustic authority weight. Live quality remains only in tuning/telemetry/shader metadata.
Rejected Alternatives: Keeping low-quality cadence was rejected because skipped closure/pressure windows change containment timing and pressure-lock state. Treating acoustic volume as visual was rejected because it is emitted through first-party `SignalBus<MovementAcousticSignal>`, not a shader-only lane.
Scalability potential: Low devices keep the cheap bulkhead/hatch fake but evaluate the same authority windows as high/ultra. Middle/high/ultra should spend quality on hatch LEDs, condensation, spray, groan audio, and diagnostics, not alternate pressure/lock truth.
Hardware Impact: 0.0 us saving claimed. Low tier may pay full 30 Hz bulkhead cadence and 0.016 s hatch pressure tick interval; no managed allocation, DTO layout, DataVault buffer, or SignalBus route change was added.

## Shinobu Ocean Surface Readback Authority Invariance
Problem: `ShinobuOceanSurfaceAtmosphereRuntime` registers as `IHectonOceanKinematics`, but `GlobalQualityWeight` controlled wave evaluation time quantization, GPU readback sample budget, readback active wave count, compute shader quality, and telemetry state hash. These paths feed `TrySampleWaveKinematics`, `GetWaterHeight`, `GetWaveNormal`, and `WaterlineBreachSignal`.
Solution: Added `OceanSurfaceAtmosphereConstants.AuthoritativeQualityWeight = 1f` and routed the readback/authority sample lane through it. Live quality still drives visual ocean LOD/shader globals and metadata, but readback sample truth and state hash no longer vary by device quality.
Rejected Alternatives: Keeping quality-scaled readback budget was rejected because low-tier devices could miss queued water-height queries and return stale/sea-level fallback. Keeping quality inside the telemetry hash was rejected because presentation quality must not alter state identity.
Scalability potential: Low devices keep the cheap async GPU readback fake instead of physical ocean simulation, but receive the same sampled water truth as high/ultra. Middle/high/ultra should spend quality on radial grid density, foam, spray, caustics, and atmosphere visuals.
Hardware Impact: 0.0 us saving claimed. Low tier may pay max readback sample budget and full readback wave count; no managed allocation, DTO layout, DataVault handle, or provider route change was added.

## BuoyancyObject Ground SDF Probe Authority Invariance
Problem: `BuoyancyObject.ResolveGroundSdfStepMeters` used `HomeostasisBrain.GlobalQualityWeight` to choose coarse/fine SDF ray step. That ground hit suppresses buoyancy when an object is above/island-grounded, so low/high device quality could disagree on whether buoyancy is applied.
Solution: Added explicit `AuthoritativeQualityWeight = 1f` and routed SDF step selection through it. The probe still uses the existing terrain/SDF read model and fixed cadence; no new buffers or scene search were added.
Rejected Alternatives: Keeping coarse low-tier SDF step was rejected because missed or shifted ground contact changes buoyancy suppression. Moving to PhysX casts was rejected because this component explicitly avoids pulling PhysX into player-adjacent water state.
Scalability potential: Low devices keep the cheap SDF fake but use the same ground-hit resolution as high/ultra. Middle/high/ultra should spend quality on wake, splash, foam, contact visuals, and diagnostics, not alternate buoyancy enablement.
Hardware Impact: 0.0 us saving claimed. Low tier may pay fine SDF step on configured ground-check cadence; no managed allocation, DTO layout, registry route, or runtime ownership change was added.

## Submarine Autopilot SDF/Flow Authority Invariance
Problem: `SubmarineAutopilotSdfNavigator` used live quality to choose solver cadence, SDF feeler count, ray-march step count, SDF interpolation, SDF gradient normals, and flow-field interpolation. Those paths write `AutopilotStateDTO.DesiredVelocity`, avoidance state, waypoint advancement timing, and telemetry hash inputs.
Solution: Added `SubmarineAutopilotConstants.AuthoritativeQualityWeight = 1f` and routed autopilot authority through it. Tuning still stores `GlobalQualityWeight` as metadata, but `ResolvedQualityWeight` is now an authority constant, and the scheduler uses an authority cadence resolver.
Rejected Alternatives: Keeping low-tier reduced feelers/nearest-neighbor SDF was rejected because it changes avoidance direction and collision clearance. Moving this to Unity physics casts was rejected because the SDF fake is cheaper, deterministic, and already DataVault-owned.
Scalability potential: Low devices keep the SDF/flow fake but solve the same route as high/ultra. Middle/high/ultra should spend quality on sonar overlays, wake, autopilot UI, thruster VFX, and diagnostics, not alternate submarine steering truth.
Hardware Impact: 0.0 us saving claimed. Low tier may pay max feeler count, max cheap SDF steps, trilinear SDF/flow, and full cadence; no managed allocation, DTO layout, DataVault buffer, or SignalBus route change was added.

## Seaglide Metabolism Cadence Authority Invariance
Problem: `SeaglideHydrodynamicsRuntime.AdvanceMetabolismCadence` used the live resolved quality to choose battery metabolism cadence. `ProcessSeaglideMetabolismJob` writes `SeaglideStateDTO.BatteryLevel`, so low/high device quality could drain the seaglide battery on different frame windows.
Solution: Routed metabolism cadence through `SeaglideSimdMath.AuthoritativeQualityWeight`. Live quality remains stored in tuning/telemetry and sent to cavitation/presentation jobs, while `ResolvedQualityWeight` now records the authority constant.
Rejected Alternatives: Keeping slower low-tier battery cadence was rejected because battery level is gameplay/economy state. Removing metabolism cadence entirely was rejected because the cadence fake is a useful cheap battery integration throttle when it is deterministic.
Scalability potential: Low devices keep the scalar cadence fake but drain battery on the same authority schedule as high/ultra. Middle/high/ultra should spend quality on cavitation bubbles, motor audio, wake streaks, and HUD feedback, not alternate battery truth.
Hardware Impact: 0.0 us saving claimed. Low tier may pay min metabolism cadence; no managed allocation, DTO layout, DataVault buffer, force route, or SignalBus route change was added.

## Hydrodynamic KCC SDF Collision Authority Invariance
Problem: `HydrodynamicKccRuntime` still used live `GlobalQualityWeight` in the water KCC collision probe lane: SDF interpolation blended nearest/trilinear by device quality and speculative probe count could shrink on low tier. KCC telemetry also estimated collision work/turbulence from live quality.
Solution: Routed `BuildSdfCollisionHitsJob` SDF quality and speculative sample count through `HydrodynamicKccMath.AuthoritativeQualityWeight`. The public iteration/sample resolvers now ignore presentation quality for authority math, and `KinematicTelemetryAggregateJob` uses the same authority constant for blackbox compute/turbulence estimates.
Rejected Alternatives: Keeping nearest-neighbor/low probe count for weak devices was rejected because collision hit order, penetration, and slide response can diverge. Moving the path to PhysX casts was rejected because the SDF fake is cheaper, deterministic, and already DataVault-owned.
Scalability potential: Low devices keep the cheap SDF grid fake but solve the same collision probes as high/ultra. Middle/high/ultra should spend quality on swim wake, camera water feel, foam, sonar overlays, and visual smoothing, not alternate collision truth.
Hardware Impact: 0.0 us saving claimed. Low tier may pay max cheap SDF probe stride and trilinear blend; no managed allocation, DTO layout, DataVault lane, SignalBus route, or visual sync contract changed.

## Hydrodynamic KCC Hot DataVault Acquire Gate
Problem: `HydrodynamicKccRuntime.FixedTick()` and `LateFrameTick()` called `EnsureVaultBuffers()`, and that method could reach `vault.EnsureGenerationHandle<T>()`. Missing KCC buffers could therefore allocate/generate DataVault lanes from recurring movement/visual-sync phases.
Solution: Added an `allowAcquire` gate to `EnsureVaultBuffers`. Cold OnEnable, DataVault hot-swap, and editor/profile ingestion keep acquisition enabled; recurring FixedTick/LateFrameTick now pass `allowAcquire: false` and fail closed unless buffers already exist.
Rejected Alternatives: Leaving hot repair was rejected because it hides boot-order defects and turns GlobalDataVault into a runtime heap. Removing the KCC DataVault route was rejected because the existing native lanes provide rollback, telemetry, and zero-GC jobs.
Scalability potential: Low devices avoid fault-path allocation hitches during swimming/KCC motion. Middle/high/ultra keep the same native buffers and can spend quality on visual water feedback rather than recovery work.
Hardware Impact: 0.0 us steady-state saving claimed. Prevents unbounded allocation/generation stalls on i3/MX350 class hardware when boot wiring is broken; no DTO, job schedule, or SignalBus contract changed.

## Shoreline Foam Visual-Sync Allocation Gate
Problem: `ShorelineFoamGraftRuntime.VisualSyncTick` called `EnsureVaultState` every ocean visual-sync frame, and that helper could call `vault.EnsureGenerationHandle<T>()`. The same hot path also called `EnsureGpuBuffersCold`, which could allocate `GraphicsBuffer` pairs after a lost or missing buffer.
Solution: Added `ShorelineFoamGraftRuntime.EnsureColdState` and call it from `OceanSinglePassRuntime.EnsureVaultState`. Runtime visual sync now passes `allowAcquire: false`, adopts only existing DataVault handles, and exits unless prewarmed GPU buffers are valid.
Rejected Alternatives: Keeping hot repair was rejected because shoreline foam is visual, but visual sync still must not become a DataVault/GPU heap repair lane. Disabling shoreline foam was rejected because the cheap foam graft is a valid cinematic fake.
Scalability potential: Low devices avoid allocation stalls from missing shoreline foam lanes or buffers. Middle/high/ultra keep the same continuous-quality foam density and shader loop limits after cold prewarm.
Hardware Impact: 0.0 us steady-state saving claimed. Prevents unmeasured DataVault/GPU allocation spikes on i3/MX350 class hardware; no DTO layout, shader contract, or render-feature API changed.

## Abyssal Fluid Decal Direction and MPB Cleanup
Problem: `AbyssalFluidDecalManager` collapsed pressure-spray and voxel cave-in dust vectors to dominant axes, producing taxicab fluid motion. Its fallback draw path could also call `MaterialPropertyBlockRegistry.GetOrCreateLegacyBlock` while drawing decals or pressure sprays.
Solution: Replaced dominant-axis vector resolution with finite normalization and made hot draw paths fail closed when the cold-created property block is missing. Awake/OnEnable still own material property block acquisition.
Rejected Alternatives: Keeping axis snapping was rejected because it breaks authored leak direction and makes water-fluid visuals look grid-bound. Creating an MPB during draw was rejected because fallback visuals must not repair managed rendering state in the hot lane.
Scalability potential: Low devices keep the cheap quad/decal fake with smoother direction. Middle/high/ultra can spend quality on screen-space decals, wake tearing, and richer fluid aftermath without alternate vector behavior.
Hardware Impact: 0.0-0.2 us/frame saved only when the fallback draw path had lost its property block. Adds a few scalar ops on event registration; no allocation, DTO, SignalBus, or shader contract changed.

## Sargassum Crest Facade Hot RT Allocation Gate
Problem: `SargassumCrestDampingController.LateFrameTick` could call `RefreshFacadeTextures`, which called `EnsureFacadeResources` and created or resized two `RenderTexture` facade targets. A late-ready or dimension-changing sargassum density field could therefore allocate GPU resources from recurring ocean/Crest visual sync.
Solution: Added an `allowAllocate` path. Awake/OnEnable and Sargassum drag/cut hot-swap may allocate facade RTs; LateFrame refresh passes `allowAllocate: false` and disables facade globals if matching RTs were not already created.
Rejected Alternatives: Leaving allocation in LateFrame was rejected because facade textures are visual-donor state, not an emergency repair lane. Disabling the facade permanently was rejected because it is a cheap first-party ocean damping fake that keeps Crest materials untouched.
Scalability potential: Low devices avoid GPU allocation stalls during sargassum/ocean frames. Middle/high/ultra retain the same facade compute and can spend quality on denser canopy/cut-mask visuals after cold prewarm.
Hardware Impact: 0.0 us steady-state saving claimed. Prevents unmeasured `RenderTexture` allocation spikes on i3/MX350 class hardware when density textures appear late or resize; no shader global contract changed.

## Fluid Dynamic Wake Payload Hot Acquire Gate
Problem: `HectonFluidEngine.TryGetDynamicWakeGpuPayload` is a render payload read path, but it called `EnsureDynamicWakeGpuBuffers` and `TryResolveDynamicWakeVaultBuffers` that could create GraphicsBuffers and DataVault wake lanes when state was missing.
Solution: Prewarm dynamic wake GPU buffers and DataVault handles from Awake, OnEnable, and DataVault hot-swap. The payload path now checks `AreDynamicWakeGpuBuffersReady()` and resolves DataVault handles with `allowAllocate: false`.
Rejected Alternatives: Leaving payload-side repair was rejected because read accessors must not allocate or create vault lanes. Dropping dynamic wake payload entirely was rejected because it is a valid cheap VFX advection source when prewarmed correctly.
Scalability potential: Low devices avoid render-payload allocation stalls. Middle/high/ultra still get dynamic wake advection uploads when the cold path prepared the native/GPU state.
Hardware Impact: 0.0 us steady-state saving claimed. Prevents unmeasured GraphicsBuffer/DataVault allocation spikes on i3/MX350 class hardware if dynamic wake state is missing; no shader, DTO, or render-feature contract changed.

## Shared Gerstner Wave Publish Acquire Gate
Problem: `HectonFluidEngine.PublishGerstnerWaveDataVault` publishes shared ocean wave state from the recurring wave population path, but when the vault was not allocation-locked it called `OpenOrAcquireFluidVaultBuffer`, which can create `OceanGerstnerWaves` and `OceanGerstnerWaveMeta` lanes during publication.
Solution: The publish path now uses `TryOpenExistingFluidVaultBuffer` for both shared Gerstner lanes and fails closed if cold prewarm did not create them. Allocation remains in `EnsureSharedGerstnerDataVaultBuffers`, reached from resize/prewarm ownership.
Rejected Alternatives: Keeping publish-side repair was rejected because it hides boot-order defects and makes the wave state publish lane a DataVault heap fallback. Moving shared Gerstner waves into a new signal DTO was broader and unnecessary for this defect.
Scalability potential: Low devices avoid fault-path DataVault allocation stalls during ocean/buoyancy wave publication. Middle/high/ultra keep the same wave DTO and can spend quality on foam, wake, spray, and shader density after prewarm.
Hardware Impact: 0.0 us steady-state saving claimed. Prevents unmeasured `EnsureGenerationHandle` spikes on i3/MX350 class hardware if shared Gerstner lanes are missing; no DTO layout, shader uniform, or provider route changed.

## Fluid Advection Visual-State Allocation Gate
Problem: `HectonFluidEngine.LateFrameTick` called `EnsureFluidAdvectionVisualState` every frame, and that helper could create DataVault-backed native buffers, `GraphicsBuffer` pairs, a fallback `Texture3D`, and fallback `RTHandle`. Splashdown and bubble/debris event paths could also call native state ensure from recurring lanes.
Solution: Added `allowAllocate` gates to fluid advection native/GPU/texture ensure methods. Awake, OnEnable, and DataVault hot-swap prewarm with allocation enabled. LateFrame, splashdown bubble rings, external bubble bursts, and debris signal drain use `allowAllocate:false` and fail closed unless prewarm succeeded.
Rejected Alternatives: Keeping hot repair was rejected because a visual advection fake must not become a runtime heap and GPU-resource repair lane. Disabling advection was rejected because bounded particles are a cheap cinematic water-fluid fake when prewarmed.
Scalability potential: Low devices avoid allocation stalls during water-fluid visual sync and event bursts. Middle/high/ultra keep the same advection particle caps and can spend quality on richer flow/SDF/dynamic wake inputs after cold prewarm.
Hardware Impact: 0.0 us steady-state saving claimed. Prevents unmeasured NativeArray/DataVault, `GraphicsBuffer`, `Texture3D`, and `RTHandle` allocation spikes on i3/MX350 class hardware if advection state is missing; no DTO, compute shader, or render-feature contract changed.

## Fluid Advection RenderGraph Texture-Handle Allocation Gate
Problem: `TryBuildFluidAdvectionRenderGraphPayload` is a read-model/payload build route used during render pass recording, but `ResolveFluidAdvectionFlowTextureHandle` and `ResolveFluidAdvectionSdfTextureHandle` could allocate `RTHandle` wrappers for flow or voxel SDF textures.
Solution: Payload build now calls both texture-handle resolvers with `allowAllocate:false`. If a flow or SDF handle was not prewarmed, the payload falls back to the already-owned empty texture and clears the texture/SDF active flags instead of allocating.
Rejected Alternatives: Allocating `RTHandle` from payload build was rejected because read accessors and render recording must not repair resource ownership. Forcing SDF/flow dispatch failure was rejected because the empty texture is a cheaper and safer visual fake fallback.
Scalability potential: Low devices avoid RTHandle allocation stalls during render pass recording. Middle/high/ultra keep flow/SDF advection only when handles were prewarmed and can spend quality on dynamic wake, flow texture, and SDF detail without a hot allocation route.
Hardware Impact: 0.0 us steady-state saving claimed. Prevents unmeasured `RTHandle` allocation spikes on i3/MX350 class hardware when abyssal flow or voxel SDF textures appear late; no render-feature payload layout or compute shader contract changed.

## Dynamic Wake Payload Owner-Phase Upload and RenderGraph Resource Declaration
Problem: `TryGetDynamicWakeGpuPayload` was exposed through a read-model interface but uploaded DataVault wake arrays into ping-pong `GraphicsBuffer`s, changed `_activeDynamicWakeBuffer`, and flipped `_dynamicWakeUploadBufferIndex`. `HectonFluidAdvectionRenderFeature` also bound those wake buffers in compute without declaring them to RenderGraph as read resources.
Solution: Moved dynamic wake upload/state flip into `LateFrameTick` through `RefreshDynamicWakeGpuPayload`, gated by advection readiness and active particle count. `TryGetDynamicWakeGpuPayload` now reads cached active buffers/params only. The RenderGraph pass imports `DynamicWakeBuffer` and `DynamicWakeVectorBuffer` and calls `UseBuffer(..., AccessFlags.Read)` for both.
Rejected Alternatives: Keeping the upload in the getter was rejected because read accessors must not mutate GPU state or vault-derived payload state. Leaving buffers undeclared was rejected because RenderGraph cannot reason about hazards/lifetime for external resources it does not see.
Scalability potential: Low devices avoid redundant wake uploads when no advection particles exist and keep the empty-buffer visual fake. Middle/high/ultra keep continuous-quality dynamic wake advection when prewarmed buffers and particles are active, with explicit RenderGraph resource tracking.
Hardware Impact: 0.0 us steady-state saving claimed. Prevents unnecessary two-buffer wake uploads in inactive advection frames and removes an unmeasured RenderGraph synchronization hazard; no DTO, shader property, or DataVault lane contract changed.

## Giant Wake and Buoyancy Torque Direction Fidelity
Problem: Giant wake current direction and buoyancy job gyroscopic/shear torque axes used dominant-axis quantization. These vectors feed current force/torque output, not shader-only presentation, so diagonal water currents could become taxicab physics.
Solution: Replaced those authority/current torque axes with finite normalization. Kept the existing dominant-axis branch in `ResolveSurfaceNormalLod` because that path is an explicit low-detail surface-normal approximation controlled by authored flags.
Rejected Alternatives: Keeping dominant-axis torque was rejected because it changes real buoyancy angular response. Replacing all dominant-axis helpers was rejected because vector-noise and low-detail normal presentation paths still intentionally use cheap quantization.
Scalability potential: Low devices pay only a few scalar normalization ops per buoyancy row while preserving the cheap analytical current fake. Middle/high/ultra receive smoother diagonal giant wake and tidal shear behavior without changing DTOs or buffer layout.
Hardware Impact: 0.0 us saving claimed. Expected cost is sub-microsecond for typical object counts; correctness/feel gain outweighs the extra rsqrt operations on i3/MX350 class hardware.

## Splashdown Impulse Event Allocation Gate
Problem: The splashdown impulse route could allocate DataVault-backed native buffers from `ScheduleSplashdownImpulseField` and create ping-pong `GraphicsBuffer`s from `UploadSplashdownImpulseBuffer`, which is reached during LateFrame completion after the prologue ocean handoff.
Solution: Added `allowAllocate` gates to `EnsureSplashdownImpulseState` and `EnsureSplashdownImpulseGpuBuffer`. Awake, OnEnable, and DataVault hot-swap prewarm the state with allocation enabled; event scheduling and upload completion require existing buffers and fail closed if cold ownership did not prepare them.
Rejected Alternatives: Leaving lazy allocation on the first splashdown was rejected because the prologue handoff is a visible frame and should not pay native/GPU heap repair. Disabling the splashdown field entirely was rejected because it is a bounded cinematic fake that can be prewarmed safely.
Scalability potential: Low devices avoid allocation spikes during the ocean handoff and still get bubble-ring fallback if the rich vector-field prewarm is absent. Middle/high/ultra keep the richer splashdown flow impulse through prewarmed buffers.
Hardware Impact: 0.0 us steady-state saving claimed. Prevents unmeasured Native/DataVault and `GraphicsBuffer` allocation stalls on i3/MX350 class hardware during first splashdown impact.

## Fluid Advection RenderGraph Dispatch Contract Naming
Problem: The fluid advection RenderGraph route was named `IFluidAdvectionRenderGraphReadModel` and exposed `TryBuildFluidAdvectionRenderGraphPayload`, but the method necessarily claims a one-shot dispatch payload, clears the queue flag, applies pending origin-shift delta, and flips ping-pong parity. The name hid a deliberate mutation behind a read-model contract.
Solution: Renamed the interface to `IFluidAdvectionRenderGraphDispatchSource` and the method to `TryClaimFluidAdvectionRenderGraphPayload`. Updated `GlobalRegistry`, service-slot type resolution, `HectonFluidEngine`, and the render feature to use the explicit dispatch-source contract.
Rejected Alternatives: Making the method pure without an execution/consume callback was rejected because multi-camera render recording could dispatch the same compute payload more than once. Leaving the name unchanged was rejected because it violates the read-accessor doctrine at the contract level.
Scalability potential: Low, middle, high, and ultra devices keep identical dispatch behavior. The benefit is architectural: render code now depends on an explicit one-shot dispatch source rather than a misleading read model.
Hardware Impact: 0.0 us runtime saving claimed. No buffer, DTO, shader, or job layout changed; this removes contract ambiguity and future misuse risk.

## Maelstrom and Submarine Wake Hot Registry Fallback Removal
Problem: `TryPublishMaelstromDamage` and `TryResolveSubmarineWakePayload` called `RefreshRuntimeActorContextsIfMissing`, which can read `GlobalRegistry.Player` and `GlobalRegistry.Submarine` from recurring water hazard/wake paths when a cached context is missing or destroyed.
Solution: Added cached-context accessors that only validate and null stale Unity objects. Registry reads remain in cold setup and the existing `GlobalRegistryServiceSlot.Player/Submarine` hot-swap handler.
Rejected Alternatives: Keeping the fallback was rejected because missing DI should not turn water hazard/wake code into a registry polling lane. Calling `Find`/component search was not considered acceptable because the registry already owns these service routes.
Scalability potential: Low devices avoid hidden dependency polling in active maelstrom/wake frames. Middle/high/ultra retain the same hazard and wake behavior when contexts are correctly wired through cold setup or hot-swap.
Hardware Impact: Estimated 0.0-0.5 us/frame saved only in broken/missing-context cases; no DTO, buffer, physics, or SignalBus contract changed.

## Abyssal Fluid Decal Event Resource Repair Gate
Problem: Public fluid decal event sinks called `EnsureRenderingResources(true)`. If cold setup failed or state was lost, event handling could call `ResolveSharedQuadMesh()` / `Resources.GetBuiltinResource` and attempt rendering-state repair during spray/silt/splash events.
Solution: Added `IsPresentationReady()` and changed all public decal/spray/silt/splash registration entry points to fail closed unless storage, material, MPB, and quad mesh were already prepared. Awake/OnEnable remain the resource creation owners.
Rejected Alternatives: Keeping event-side repair was rejected because aftermath VFX should not repair rendering state under gameplay/event pressure. Creating fallback materials at runtime remains forbidden for this path.
Scalability potential: Low devices avoid fault-path resource lookups during clustered fluid aftermath events. Middle/high/ultra keep the same capped decal/spray fake when prewarmed resources are valid.
Hardware Impact: 0.0 us steady-state saving claimed. Prevents unmeasured resource lookup/repair spikes on weak hardware when presentation setup is incomplete.

## Buoyancy Native Capacity Owner-Phase Gate
Problem: `HectonFluidEngine.FixedTick` could call `ReallocateNativeArrays(count)` when buoyancy object count exceeded `_nativeCapacity`. That resize released/recreated DataVault/native/GPU buoyancy state during the physics lane, and `ReleaseIdleNativeBuffersIfNeeded` freed buffers at zero objects so the next registration could provoke another allocation path.
Solution: Added owner-phase prewarm through Awake/OnEnable/DataVault hot-swap, moved registration to a bool-return contract that only accepts objects when native capacity is ready, retained runtime idle buffers until engine disable/destroy, and made `FixedTick` validate ready capacity with `allowAllocate:false`.
Rejected Alternatives: Keeping fixed-tick capacity doubling was rejected because it hides heap/DataVault/GPU work inside physics. Hard-capping all scenes at 256 was rejected because high-density water set pieces need authored capacity; the new serialized prewarm capacity defaults higher and remains explicit.
Scalability potential: Low devices retain one predictable native footprint and avoid first-object-after-idle stalls. Middle/high/ultra can raise `prewarmedBuoyancyCapacity` for dense debris/ocean scenes without changing authority math or DTO layout.
Hardware Impact: 0.0 us steady-state saving claimed. Prevents unmeasured native/DataVault/GPU allocation stalls on i3/MX350 class hardware; adds only a cheap readiness flag check in the fixed path after prewarm.

## Underwater HUD/Photophobia Hot RT Allocation Gate
Problem: `HectonUnderwaterVisuals.UpdateHudFogLuminanceDownsample` and `UpdateFlashlightPhotophobiaField` called resource ensure methods that could create `RenderTexture` targets from recurring underwater visual update paths.
Solution: Added `allowAllocate` gates to HUD fog luminance and photophobia resource ensures, prewarmed both in `OnEnable`, and made hot visual updates use `allowAllocate:false` with fail-closed behavior. Added a fast ready-state return to avoid repeated compute kernel/resource checks after prewarm.
Rejected Alternatives: Keeping first-use allocation was rejected because the first HUD/flashlight visual frame can coincide with high underwater presentation cost. Disabling the effects was rejected because the 1x1 luminance and 128x128 photophobia field are cheap cinematic fakes when prewarmed.
Scalability potential: Low devices avoid surprise RT allocation during underwater HUD/flashlight activity. Middle/high/ultra keep the same visual features and can spend quality on fog/photophobia richness without hot resource creation.
Hardware Impact: 0.0 us steady-state saving claimed. Prevents unmeasured `RenderTexture` allocation stalls on i3/MX350 class hardware; hot path now exits through cached ready flags unless resources were lost.

## Underwater Visual Direction Fidelity
Problem: `HectonUnderwaterVisuals.ResolveSafeDirection` treated any non-near-unit vector as an error and collapsed it to `DominantAxisOrDefault`. Biome fog transition anchors and shallow beam orientation therefore lost diagonal direction and produced taxicab underwater presentation.
Solution: Keep the existing zero/NaN guard, preserve already-near-unit vectors, and normalize finite non-unit vectors with `math.rsqrt`. Removed the now-dead dominant-axis helper from this route.
Rejected Alternatives: Keeping dominant-axis fallback was rejected because it is a lossy visual approximation with no explicit LOD gate. Calling `Vector3.normalized` was rejected because the local math path already uses `math` and avoids Unity helper ambiguity.
Scalability potential: Low devices pay at most one scalar `rsqrt` on setup/update paths when the vector is not already normalized. Middle/high/ultra keep authored diagonal fog/beam direction without a separate quality branch.
Hardware Impact: 0.0 us saving claimed. Runtime cost is sub-microsecond and only on affected presentation direction resolves; removed branch/helper code reduces future misuse risk.

## Async Buoyancy Readback Active-Range and Authority Payload Cleanup
Problem: `AsyncBuoyancyReadbackRuntime.DispatchGpuReadback` requested the full GPU request buffer even when only a small active prefix was dispatched. The mock/apply Burst job structs and math helpers also carried `GlobalQualityWeight` fields/parameters after the authority path had already been forced invariant, making the contract look quality-dependent when it is not.
Solution: Use the `AsyncGPUReadback.Request(GraphicsBuffer, int size, int offset, ...)` active-range overload and compute the byte count from active request count and DTO stride. Remove dead `GlobalQualityWeight` fields from readback jobs and remove unused quality parameters from authority math helpers. Quality remains only as telemetry metadata where it does not alter resolved water truth.
Rejected Alternatives: Keeping the full 512-slot request was rejected because it wastes readback bandwidth on inactive entries. Keeping dead quality hooks was rejected because it invites future device-dependent buoyancy truth regressions.
Scalability potential: Low devices avoid unnecessary GPU readback bandwidth when active buoyancy request count is below capacity. Middle/high/ultra keep the same max active sample budget and can spend quality on water presentation without changing readback authority output.
Hardware Impact: 0.0 CPU us steady-state claimed. At default 512 request capacity and 16-byte request DTO stride, active 64 requests read back 1024 bytes instead of 8192 bytes, saving 7168 bytes per GPU readback; no managed allocation added.

## Shinobu Ocean Surface Readback Active-Range Cleanup
Problem: `ShinobuOceanSurfaceAtmosphereRuntime.DispatchWaveHeightReadback` uploaded and dispatched only the active wave-height query count, but `AsyncGPUReadback.Request(resultBuffer)` read back the full 64-slot result buffer. This wastes GPU readback bandwidth on inactive result slots.
Solution: Compute the readback byte count from active sample count and `float4` stride, guard it against the result buffer capacity, and call the byte-range `AsyncGPUReadback.Request` overload. The water-height/normal truth path, authority quality, DTO layout, and ring semantics stay unchanged.
Rejected Alternatives: Keeping full-buffer readback was rejected because the code already tracks exact active count. Creating per-count GPU buffers was rejected because it would add allocation/lifetime complexity for a small bounded ring.
Scalability potential: Low devices avoid extra GPU readback bytes for the common 1-4 query path. Middle/high/ultra keep the 64-sample capacity for denser ocean debug/probe use without changing shader or provider contracts.
Hardware Impact: 0.0 CPU us steady-state claimed. At 64 `float4` result slots, active 4 reads 64 bytes instead of 1024 bytes, saving 960 bytes per ocean wave-height readback; no allocation or extra job added.

## Underwater URP Camera Data Hot Lookup Cleanup
Problem: `HectonUnderwaterVisuals.LateFrameTick` calls `EnsureGameplayCameraStackEnabled` and `EnsureOceanUnderwaterPassOwnership` every visual frame. Those routes repeatedly resolved `UniversalAdditionalCameraData` through `TryGetComponent` for the same main/space cameras while enforcing depth/color textures and URP camera composition.
Solution: Cache main and space `UniversalAdditionalCameraData` beside the camera references, invalidate on Unity missing-reference/null state, and pass the known `Camera` into texture-requirement enforcement so the post-processing check does not re-query the component.
Rejected Alternatives: Leaving the lookup because `TryGetComponent` is non-alloc was rejected; non-alloc is still unnecessary main-thread native work in a per-frame underwater visual route. Rebuilding the full camera-stack ownership system was rejected because the current defect is a narrow cache miss, not a route redesign.
Scalability potential: Low devices avoid repeated camera component queries while underwater visuals own the stack. Middle/high/ultra keep the same URP stack, Crest underwater pass ownership, and post-processing behavior, and can spend saved time on fog, motes, beams, and photophobia.
Hardware Impact: Estimated 0.2-1.0 us/frame saved on i3/MX350 class hardware during active underwater visual sync, depending on camera count and bridge ownership checks. No allocation, DTO, shader, or third-party Crest material route changed.

## Internal Flood Waterline Hot Registry Polling Cleanup
Problem: `InternalFloodWaterlineRuntime.AdvanceWaterlinePresentation` called `RefreshCachedDependencies(false)` every LateFrame. The method used a 30-tick retry to read `GlobalRegistry.Player` and `GlobalRegistry.HabitatGraph` when cached services were missing, turning an internal waterline presentation path into a recurring registry polling lane.
Solution: Replaced the retry refresh with `CacheRuntimeDependenciesCold()` during service initialization. Runtime replacement remains handled by `IGlobalRegistryHotSwapListener`, so LateFrame only reads cached interfaces and current SignalBus snapshots.
Rejected Alternatives: Keeping the 30-tick fallback was rejected because missing DI should fail closed instead of silently polling global identity from presentation. Adding component or scene search fallback was rejected because player/habitat ownership already has explicit registry service slots.
Scalability potential: Low devices avoid hidden dependency polling during damaged or late-bound scenes. Middle/high/ultra keep the same internal flood waterline shader, droplet, exhale, and telemetry behavior when services are wired through cold init or hot-swap.
Hardware Impact: Estimated 0.0-0.3 us/frame saved only in missing-dependency cases. Main gain is deterministic ownership: no DTO, shader global, DataVault buffer, or SignalBus contract changed.

## Visor Fluid Render Dependency Hot Registry Cleanup
Problem: `HectonVisorFluidDistortionFeature.AddRenderPasses` builds runtime state per render camera. Its `ResolvePlayerContext()` and `ResolveFluidSimulation()` helpers read `GlobalRegistry.Player` and `GlobalRegistry.FluidSimulation` whenever cached services were null, missing a camera, or not ready. That made a render pass repair dependency identity.
Solution: Added cold `CacheRenderDependenciesCold()` in `OnEnable` and `Create`. The render path now only consumes cached services, and `IGlobalRegistryHotSwapListener` remains the runtime replacement path for Player and FluidSimulation.
Rejected Alternatives: Keeping fallback registry reads was rejected because render feature code can execute per camera and should fail closed when DI is absent. Searching the scene or adding a renderer-local service lookup was rejected because registry service slots already define ownership.
Scalability potential: Low devices avoid dependency polling across camera stacks and scene transitions. Middle/high/ultra keep the same visor refraction, wet lens, rain, hull-stress, density signal, and visual-overkill scaling when services are bound.
Hardware Impact: Estimated 0.0-0.5 us/render camera saved only in missing/not-ready dependency cases. No shader property layout, black-box telemetry DTO, DataVault buffer, or render pass behavior changed for correctly wired scenes.

## Fluid Fixed-Tick Weather Snapshot Coherence
Problem: `HectonFluidEngine.FixedTick` resolved a weather snapshot for abyssal flow/sleep-path wave uniforms, then resolved weather again before buoyancy wave population and GPU buoyancy queueing. Within one physics tick this can mix two service snapshots if weather publishes between reads, and it repeats a service call in a hot physics path.
Solution: Resolve `fixedWeatherSnapshot` once after observer retry and reuse it for `QueueAbyssalFlowVisualSync`, early-exit wave uniform publishing, `PopulateGerstnerWaveData`, and `QueueGpuBuoyancySampling`.
Rejected Alternatives: Leaving duplicate reads was rejected because physics tick consumers should share one immutable owner-phase snapshot. Caching weather globally across frames was rejected because weather ownership and frame cadence stay with the weather service.
Scalability potential: Low devices save a small service call and avoid inconsistent water visuals/forces in overloaded frames. Middle/high/ultra keep the same weather-driven wave and abyssal detail, with a cleaner per-tick contract.
Hardware Impact: Estimated 0.0-0.2 us/fixed tick saved. No authority math, DTO layout, shader property, DataVault lane, or SignalBus contract changed.

## Fluid Surface Read-Route Purity and Water-Level Owner Snapshot
Problem: `IFluidSurfaceCurrentReadModel.CurrentWaterLevelY`, `GetFlowAtPosition`, and `GetWaterHeightAtPosition` are read routes, but they called the cinematic water-level resolver. That resolver writes `GlobalPhysicsStateManager` frame cache and can read weather. `FixedTick` also published water level before resolving the weather snapshot used by waves, abyssal flow, and buoyancy.
Solution: Added an owner-published water-level snapshot and timestamp in `HectonFluidEngine`. `PublishCurrentWaterLevelUniform(in WeatherRuntimeSnapshot)` is now the only route that updates the cinematic water level/cache. Read routes return `ReadPublishedCurrentWaterLevelY()`. `FixedTick` resolves one `fixedWeatherSnapshot` first and passes it into water-level publication, wave uniforms, abyssal flow, Gerstner population, and GPU buoyancy queueing.
Rejected Alternatives: Keeping recomputation in getters was rejected because read accessors must not mutate global cache state or pull new service snapshots. Making every consumer recompute tides locally was rejected because it duplicates ownership and risks diverging shader/UI/flow/wave timing.
Scalability potential: Low devices avoid redundant weather reads and hidden frame-cache mutation from water queries. Middle, high, and ultra devices keep the same cinematic tide, celestial tide, shader global, UI water-surface, Gerstner, and abyssal flow behavior through one per-owner-phase snapshot.
Hardware Impact: Estimated 0.0-0.2 us/fixed tick saved by eliminating the duplicate weather snapshot path and avoiding cache recompute from read callers. No DTO layout, shader property ID, DataVault lane, authority wave math, or GlobalQualityWeight route changed.

## Visor Raw-History Camera Data Retry Cleanup
Problem: `HectonVisorUberPostFeature.AddRenderPasses` requests raw color history for temporal reconstruction. If the render camera lacks `UniversalAdditionalCameraData` history, `UpdateRawColorHistoryRequest` retried `renderCamera.TryGetComponent<UniversalAdditionalCameraData>` every render pass.
Solution: Added a per-camera raw-history access cache that stores both successful `ICameraHistoryReadAccess` and negative results. The component lookup now happens only when the render camera changes or the cache is cleared during dispose/null-camera handling.
Rejected Alternatives: Keeping per-pass `TryGetComponent` was rejected because render features can run per camera and should fail closed when temporal history is not available. Adding a periodic retry was rejected because dynamic history repair is not the owner route for URP camera configuration.
Scalability potential: Low devices avoid repeated native component lookups in visor reconstruction frames where history is unsupported. Middle, high, and ultra devices keep temporal reconstruction when the camera provides history; unavailable history uses the existing non-temporal shader path.
Hardware Impact: Estimated 0.0-0.4 us/render camera saved only on cameras requesting reconstruction without history access. No shader constants, DataVault handles, render-pass topology, or waterline parameters changed.

## Underwater Shallow Sun Beam Hot Retry Cleanup
Problem: `HectonUnderwaterVisuals.UpdateShallowSunBeam` calls `ResolveShallowSunBeam` from the underwater LateFrame presentation route. When the optional `Underwater_ShallowSunBeam` child or its `Light` component is absent, the resolver repeated `Transform.Find` and `TryGetComponent<Light>` every frame.
Solution: Added a per-main-camera negative cache for the missing beam child and a per-transform negative cache for the missing `Light` component. Existing assigned lights still bind their transform directly. Cache state is cleared on disable.
Rejected Alternatives: Keeping endless retries was rejected because optional presentation assets must fail closed, not run a hot repair loop. Adding periodic retries was rejected because dynamic prefab repair is not the underwater visual owner route.
Scalability potential: Low devices avoid recurring hierarchy/component lookup in underwater scenes that intentionally omit the beam fake. Middle, high, and ultra devices keep the same shallow sun beam effect when the child/light is authored, and the saved frame time can go to motes, fog, caustics, or photophobia.
Hardware Impact: Estimated 0.0-0.3 us/frame saved only in missing/incomplete optional beam setups. No shader globals, light intensity math, biome/ecology multipliers, or water-state authority changed.

## Underwater Motes and Exhale Hot Retry Cleanup
Problem: `HectonUnderwaterVisuals.UpdateUnderwaterSuspendedMotes` and exhale/bubble presentation can call optional VFX resolvers from LateFrame. When `Underwater_SuspendedMotes` or `Underwater_ExhaleBubbles` children/components are absent, the resolvers repeated `Transform.Find` and `TryGetComponent` on every underwater frame.
Solution: Added per-main-camera child search caches and per-child component negative caches for suspended motes and exhale bubbles. Missing optional VFX now fail closed until the camera changes or the component is assigned through serialized/cold setup.
Rejected Alternatives: Keeping recurring lookup was rejected because optional presentation should not become a repair loop. Periodic retry was rejected because dynamic prefab mutation is not the owner route for these authored underwater visual children.
Scalability potential: Low devices avoid recurring hierarchy/component lookups in scenes that omit optional motes or exhale bubbles. Middle, high, and ultra devices keep the same effects when authored and can spend saved time on marine snow GPU particles, fog, caustics, and photophobia.
Hardware Impact: Estimated 0.0-0.5 us/frame saved only in missing/incomplete optional VFX scenes. No shader globals, particle emission math, marine snow state, or water gameplay truth changed.

## Giant Wake Read-Route Snapshot Cleanup
Problem: `GetFlowAtPosition` and `ResolveGiantWakeCurrentForDepth` could call `ResolveGiantWakeCurrentBase()` when `_resolvedGiantWakeCurrent` was zero. That function reads the celestial direction service. These flow queries are read/model routes and should consume owner-published water/current snapshots, not repair giant wake state on demand.
Solution: Analytical flow and depth-fade helpers now use `_resolvedGiantWakeCurrent` as the immutable snapshot. `ResolveGiantWakeCurrentForDepth` also computes depth from `ReadPublishedCurrentWaterLevelY()` instead of raw `waterLevel`, keeping tide/celestial presentation coherence.
Rejected Alternatives: Keeping read-time celestial fallback was rejected because read accessors must not reach side services to repair state. Duplicating giant-wake calculation in every query was rejected because the fluid owner already refreshes the snapshot in `FixedTick`.
Scalability potential: Low devices avoid unnecessary celestial service reads in mod/flow query paths. Middle, high, and ultra devices keep giant wake behavior once the owner phase has published the snapshot, with coherent depth fade against cinematic water level.
Hardware Impact: Estimated 0.0-0.2 us/query saved only when the snapshot is zero or stale. No DTO layout, force job math, shader ID, DataVault lane, or quality route changed.

## Visor Fluid Black-Box Render Allocation Gate
Problem: `HectonVisorFluidDistortionFeature.WriteBlackBoxFrame` runs from `AddRenderPasses`. Its `TryEnsureBlackBoxLease()` could call `IDataVault.EnsureGenerationHandle<VisorRefractionTelemetryEntry>()` when the black-box descriptor was missing, turning render telemetry into a DataVault allocation/repair route.
Solution: Split the route. `TryEnsureBlackBoxLease()` now resolves only an already-created handle through cached `_dataVault` and read-only validation. New `EnsureBlackBoxLeaseCold()` owns allocation and is called only from `OnEnable`, `Create`, and `DataVault` hot-swap after cold vault caching; it refuses fresh allocation during `IsAllocationLocked` or compaction fence.
Rejected Alternatives: Keeping allocation in the render helper was rejected because render passes can execute per camera and must not repair native storage. Disabling black-box telemetry was rejected because the 300-frame ring is required crash proof; the correct fix is owner-phase prewarm plus existing-only render writes.
Scalability potential: Low devices avoid an unbounded native allocation/generation hitch on the first affected render camera. Middle, high, and ultra devices keep the same visor refraction/wet-lens telemetry and can spend quality on visual-overkill distortion rather than recovery work.
Hardware Impact: 0.0 us steady-state saving claimed. Prevents fault-path DataVault allocation spikes on i3/MX350 class hardware when the ring was missing; hot render path now performs descriptor validation/read-write only.

## Underwater Optional Transition and Sun Lookup Retry Cleanup
Problem: `HectonUnderwaterVisuals` still retried optional hierarchy/component lookups for transition camera VFX, `Suit_Visor`, and `Sun_Body`. The transition paths run on thermocline/submerge/surface events; sun visual resolution also runs from slow runtime owner checks when the optional child is absent.
Solution: Added negative caches keyed by current main camera, player root, and sun light. Missing optional objects now fail closed until the owner context changes or serialized references are assigned. Cache state resets on disable beside the existing optional underwater VFX caches.
Rejected Alternatives: Repeating `TryGetComponent`/`Transform.Find` because these are rare was rejected; optional presentation assets should not repair themselves from event/slow owner routes. Periodic retry was rejected because dynamic prefab mutation is not the owner contract for these water presentation children.
Scalability potential: Low devices avoid small but recurring hierarchy/component lookup costs in scenes that omit optional transition visor/sun-body fakes. Middle, high, and ultra devices keep the same transition impulses and sun visual hiding when authored, and saved budget remains available for fog, motes, caustics, and photophobia.
Hardware Impact: Estimated 0.0-0.3 us per transition event or slow-tick in missing optional setups. No shader global, camera-stack, water-state, or quality route changed.

## Underwater Runtime Space-Camera Lookup Cadence Guard
Problem: `EnsureRuntimeVisualOwners()` can call `ResolveSpaceCamera()` from the LateFrame visual-owner path. When runtime `SpaceCamera` is absent, the resolver searched `playerCamera`, `mainCamera`, and `mainCamera.parent` with `Transform.Find("SpaceCamera")` every pass.
Solution: Added `_nextRuntimeSpaceCameraResolveTime` and reused the existing one-second runtime camera retry interval. Runtime missing-space-camera search is now cadence-limited; editor preview lookup stays unchanged.
Rejected Alternatives: A permanent negative cache was rejected because `SpaceCamera` can be created late under the same player/main camera root. Keeping every-frame search was rejected because optional camera recovery does not need LateFrame hierarchy traversal.
Scalability potential: Low devices avoid recurring hierarchy search in scenes without a space camera. Middle, high, and ultra devices still recover late-created camera stacks within one second and keep the same celestial camera composition when authored.
Hardware Impact: Estimated 0.0-1.0 us/frame saved only while runtime `SpaceCamera` is absent. No shader global, render-stack ownership, water-state, or quality route changed.

## Visor Reconstruction Vault Allocation-Lock Guard
Problem: `HectonVisorUberPostFeature.EnsureReconstructionVaultHandle<T>()` opened existing handles first, but if the descriptor was missing it called `EnsureGenerationHandle<T>()` without honoring `IDataVault.IsAllocationLocked` or the compaction fence. This is a cold visual setup route, but it still must not allocate during a locked DataVault phase.
Solution: Keep the existing-handle read path first, then fail closed before fresh allocation when `_dataVault.IsCompactionFenceActive` or `_dataVault.IsAllocationLocked` is true.
Rejected Alternatives: Letting cold setup allocate under lock was rejected because allocation windows are owned by DataVault policy, not by renderer feature convenience. Moving reconstruction telemetry to a different store was rejected because the existing DataVault ring is the documented proof route.
Scalability potential: Low devices avoid locked-phase allocation stalls or invalid generation churn during visor reconstruction setup. Middle, high, and ultra devices keep the same reconstruction constants, telemetry, aesthetic profile, CSV scratch, and mock signal lanes when the vault is open.
Hardware Impact: 0.0 us steady-state saving claimed. Prevents allocation-policy violations and possible native generation hitch on i3/MX350 class hardware when the setup route runs during locked DataVault phases.

## Underwater URP Camera Data Negative-Cache Cleanup
Problem: The cached URP camera-data helper remembered successful `UniversalAdditionalCameraData` lookups but not missing results. A camera without URP data could therefore force repeated `TryGetComponent<UniversalAdditionalCameraData>` calls from underwater visual-owner routes.
Solution: Added per-main and per-space-camera missing-result flags, threaded them through `TryResolveCameraDataCached`, and reset the cache on disable. Valid data remains reused directly; missing data fails closed until the camera changes or the component cache is explicitly cleared by lifecycle.
Rejected Alternatives: Periodic retry was rejected because URP camera data is an owner/configuration component, not something the underwater presenter should repair every frame. Removing texture requirement enforcement was rejected because authored URP cameras still need the depth/color/postprocess flags.
Scalability potential: Low devices avoid repeated native component probes in incomplete camera setups. Middle, high, and ultra devices keep the same main/space camera composition, ocean pass ownership, and underwater postprocessing when URP data is present.
Hardware Impact: Estimated 0.0-0.4 us/frame saved only on incomplete/non-URP camera setups. No shader global, water-state authority, DataVault lane, or quality route changed.

## Fluid Read-Only DataVault Resolve De-Duplication
Problem: `FloaterPositions`, `BuoyancyResults`, `TryGetActiveMaelstroms`, and `TryGetActiveWhirlpoolFlows` checked `FluidVaultBuffer<T>.IsCreated` and then called `AsReadOnly()`. Both paths resolve DataVault handles, causing duplicate read-side descriptor work.
Solution: Returned `AsReadOnly()` directly and let that single route validate the handle, buffer creation, and required length.
Rejected Alternatives: Adding a second cached read-only field was rejected because DataVault generation handles already own validity. Keeping the double check was rejected because public read-model consumers can call these routes frequently.
Scalability potential: Low devices avoid redundant DataVault read validation in UI/debug/consumer reads. Middle, high, and ultra devices keep the same native buffer capacity and can spend saved time on richer water visuals.
Hardware Impact: Estimated 0.0-0.2 us/query saved for read-model consumers. No DTO layout, buffer ID, authority route, or allocation behavior changed.

## Flow Field Visualizer Fluid-Service Polling Cleanup
Problem: `FlowFieldVisualizer` cached `IFluidSurfaceCurrentReadModel` in `OnEnable`, but grid recalculation and non-job sample evaluation still read `GlobalRegistry.FluidSurfaceCurrent`. In the non-job branch this could happen once per grid sample.
Solution: Made the visualizer implement `IGlobalRegistryHotSwapListener`, centralised subscription in `CacheFluidCurrent`, and changed recalculation/sample paths to use `_subscribedFluidCurrent` only. `GlobalRegistry.FluidSurfaceCurrent` remains the cold enable-time seed route.
Rejected Alternatives: Leaving registry reads because the tool is editor-facing was rejected; diagnostic tools still teach bad hot-path ownership and can run large grids. Polling on every recalculation was rejected because hot-swap already provides the correct dependency replacement lane.
Scalability potential: Low devices/editor sessions avoid unnecessary registry reads during large current-grid previews. Middle, high, and ultra devices keep identical visualizer output and recover service replacement through hot-swap.
Hardware Impact: Estimated 0.0-0.5 us/recalculation plus one registry read per non-job sample avoided. No gameplay water truth, CurrentManager math, or visualization buffer layout changed.

## Async Buoyancy DataVault Compaction-Fence Guard
Problem: `AsyncBuoyancyReadbackRuntime.EnsureVaultDescriptor<T>()` adopted existing DataVault descriptors first and then checked only `IsAllocationLocked` before `EnsureGenerationHandle<T>()`. During DataVault compaction, the async buoyancy cold/hot-swap route could still generate a new native lane.
Solution: Added `IsCompactionFenceActive` to the fresh allocation gate while preserving the existing-handle adoption path.
Rejected Alternatives: Blocking all reads during compaction was rejected because existing descriptors can be valid and read-only adoption is not allocation. Moving async buoyancy state out of DataVault was rejected because the readback DTO lanes are already the project ownership route.
Scalability potential: Low devices avoid compaction-phase generation spikes. Middle, high, and ultra devices keep identical async buoyancy readback truth and capacity when the vault is open.
Hardware Impact: 0.0 us steady-state saving claimed. Prevents fault-path DataVault generation work on i3/MX350 class hardware during compaction; no DTO layout, shader, or authority math changed.

## BuoyancyObject DataVault Hot-Swap Registry Cleanup
Problem: `BuoyancyObject.OnGlobalRegistryServiceReplaced()` handled a DataVault service replacement by reading `GlobalRegistry.BuoyancyObjectRegistry`. That slot is the fluid runtime dependency and should be updated by the FluidRuntime hot-swap route, not by a DataVault event.
Solution: Removed the DataVault-event registry read. The DataVault case now rebinds only the cached fluid runtime when active/unregistered; FluidRuntime replacement remains the only place that changes the cached dependency.
Rejected Alternatives: Keeping the fallback read was rejected because it hides dependency ownership and turns unrelated DataVault replacement into service-locator polling. Adding scene search fallback was rejected because buoyancy runtime identity is already routed through GlobalRegistry hot-swap.
Scalability potential: Low devices avoid a small dependency lookup during vault churn. Middle, high, and ultra devices get cleaner water-object registration semantics with no behavior change when services are correctly wired.
Hardware Impact: 0.0 us steady-state saving claimed. Removes one cold fault-path registry read and reduces route ambiguity; no force, mass, or water sample path changed.

## Water Render DataVault Fence Sweep
Problem: `WaterOpticsRuntime`, `OceanSinglePassRuntime`, and `ShorelineFoamGraftRuntime` had visual/mock setup paths that could call `EnsureGenerationHandle<T>()` while only checking allocation lock, or in one ocean mock/runtime helper not checking the DataVault allocation state before allocation.
Solution: Added compaction-fence gates to WaterOptics and ShorelineFoam allocation paths. Changed OceanSinglePass mock/runtime acquire to adopt existing handles first, then block fresh generation during `IsCompactionFenceActive` or allocation lock.
Rejected Alternatives: Disabling ocean render mock fallback was rejected because editor/CI still needs the constant-buffer fake. Allocating anyway because these are cold/editor paths was rejected because DataVault compaction ownership is global and must be respected outside hot loops too.
Scalability potential: Low devices avoid visual setup hitches during vault compaction or editor mock generation. Middle, high, and ultra devices keep WaterOptics, shoreline foam, and single-pass ocean visual-overkill paths when the vault is open.
Hardware Impact: 0.0 us steady-state saving claimed. Prevents fault-path DataVault generation and possible native allocation spikes; no shader constant layout, RenderGraph contract, or ocean visual quality curve changed.

## Core Fluid/Ocean/Buoyancy DataVault Fence Sweep
Problem: Wider water-domain audit found the same lock-only allocation assumption in `HectonFluidEngine`, underwater biome fog, Crest ocean kinematics, analytical Gerstner waves, buoyancy displacement, and Shinobu ocean surface atmosphere. Existing code treated `IsAllocationLocked` as the only stop signal even though DataVault compaction is also an allocation-forbidden phase.
Solution: Added `IsCompactionFenceActive` beside allocation lock in fresh DataVault/native acquisition gates, while preserving existing handle adoption/read paths. Ocean surface atmosphere now computes whether any handle is missing; it blocks only if a fresh allocation is required during compaction/lock, so already-created handles can still resolve.
Rejected Alternatives: A broad blind rewrite of every `EnsureGenerationHandle<T>` in the repo was rejected because many are outside 13VOD domain or editor-only. Blocking all water reads during compaction was rejected because the correct rule is no fresh allocation, not no immutable snapshot reads.
Scalability potential: Low devices avoid compaction-phase spikes across water simulation, underwater visuals, Crest kinematics, buoyancy displacement, and ocean atmosphere. Middle, high, and ultra devices keep full visual and physics capacity once allocation windows are open.
Hardware Impact: 0.0 us steady-state saving claimed. Prevents locked-phase allocation spikes on i3/MX350 class hardware; no gameplay truth ownership, DTO layout, GlobalQualityWeight route, or save identity changed.

## Crest Depth-Cache Hierarchy Scan De-Duplication
Problem: `HectonCrestOceanDepthCacheBootstrap.TryConfigureAndPopulate()` resolved `OceanRenderer`, then helper routes could rescan `OceanDepthCache` children multiple times in the same configure/bootstrap pass. `ResetCrestSimulationForOriginShift()` also called the full reference resolver even though the origin-shift reset needs only the ocean renderer.
Solution: Added an explicit `RefreshDepthCacheScratch()` helper and a `TryResolveReferences(resolveDepthCache)` flag. Configure/bootstrap now refresh the depth-cache scratch once and pass `refreshScratch:false` into authored-cache and legacy-cache helpers. Origin-shift reset uses `resolveDepthCache:false`, avoiding an unrelated depth-cache scan.
Rejected Alternatives: Keeping repeated scans was rejected because Crest depth-cache setup is cold/rare but can still traverse authored child hierarchies during floating-origin events. Caching a permanent depth-cache array was rejected because authored caches can be added/removed by setup and the existing scratch-list route is safer.
Scalability potential: Low devices avoid duplicate hierarchy traversal during ocean bootstrap and rare 5000 m AUP rebase. Middle, high, and ultra devices keep the same authored local depth cache behavior and can spend the saved budget on stronger ocean presentation instead of discovery work.
Hardware Impact: Estimated 0.0-8.0 us per configure/origin-shift pass depending on child count. No Crest material, runtime camera path, water-level truth, DTO layout, or GlobalQualityWeight route changed.

## Visor-Water DataVault Compaction Allocation Guard Sweep
Problem: Water/visor presentation paths still had fresh DataVault generation points that checked allocation lock incompletely or not at the final allocation site: mock reconstruction input, noir tuning/noir vault handles, and volumetric particulate fog native state. During compaction these routes could attempt `EnsureGenerationHandle<T>()`.
Solution: Added `IsCompactionFenceActive` checks beside allocation lock before fresh handle generation while preserving existing-handle read/adoption. Volumetric fog native-state allocation now has an explicit final guard before allocating params, point lights, telemetry, and extinction profile lanes.
Rejected Alternatives: Treating visor/fog as outside water was rejected because underwater visor reconstruction, noir fog, and particulate fog are presentation routes for water state. Blocking all read-only handle access during compaction was rejected; the defect is fresh allocation, not immutable read.
Scalability potential: Low devices avoid compaction-phase generation spikes in underwater visor and fog setup. Middle, high, and ultra devices keep the same continuous quality-scaled reconstruction/noir/particulate presentation lanes once the vault is open.
Hardware Impact: 0.0 us steady-state saving claimed. Prevents locked-phase DataVault generation/allocation stalls on i3/MX350 class hardware; no shader constants, telemetry DTO layout, save identity, or gameplay truth ownership changed.

## Underwater Secondary Crest Pass Purge Cadence Guard
Problem: `HectonUnderwaterVisuals.EnsureOceanUnderwaterPassOwnership()` called `PurgeSecondaryUnderwaterPasses()` every LateFrame. That helper enumerates cameras through `Camera.GetAllCameras()` even though the Crest bridge normally has one cached underwater pass and camera/pass ownership rarely changes.
Solution: Added a dirty/cadence gate. Purge runs immediately when main camera, space camera, main pass, or space pass ownership changes; otherwise it runs on the existing one-second runtime camera retry cadence. The purge implementation and allowed main/space pass behavior stay unchanged.
Rejected Alternatives: Removing purge entirely was rejected because stale secondary Crest passes can survive camera ownership changes. Running `GetComponent<UnderwaterRenderer>()` across cameras was rejected because it would turn cleanup into component polling and widen the bridge contract.
Scalability potential: Low devices avoid recurring camera enumeration in stable underwater play. Middle devices keep the same recovery within one second. High and ultra devices keep Crest underwater ownership while saved CPU can be spent on fog, motes, beams, and visor reconstruction.
Hardware Impact: Estimated 0.0-2.0 us/frame saved on i3/MX350 class hardware while underwater ownership is stable, depending on active camera count. No shader globals, Crest material ownership, water state, DTO layout, or GlobalQualityWeight route changed.

## Fluid Read-Route Weather Snapshot Purity
Problem: `HectonFluidEngine.GetFlowAtPosition`, `GetWaterHeightAtPosition`, and `TrySampleModAbyssalFlow` are public read routes, but they read the weather service independently through `ResolveWeatherSnapshot()`. That can mix a query-time weather snapshot with the owner-published water-level/current snapshot from `FixedTick`.
Solution: Store the weather snapshot whenever `PublishCurrentWaterLevelUniform(in WeatherRuntimeSnapshot)` publishes the water-level owner snapshot. Read routes now consume `ReadPublishedWeatherSnapshot()` plus `ReadPublishedCurrentWaterLevelY()`.
Rejected Alternatives: Keeping service reads in getters was rejected because read accessors should consume immutable owner snapshots. Making each caller pass weather explicitly was rejected because these are public read-model interfaces and the fluid owner already has the correct snapshot lane.
Scalability potential: Low devices avoid small repeated service reads during flow/height queries. Middle/high/ultra keep deterministic water and current sampling from the last owner frame, with the same Gerstner and phantom-current math.
Hardware Impact: Estimated 0.0-0.3 us/query saved in query-heavy frames. Main gain is coherence: no DTO layout, save identity, water authority math, or quality route changed.

## Crest Kinematics Quality-Authoring Text Cleanup
Problem: `Crest4KinematicsAdapter` inspector text still said `GlobalQualityWeight` continuously resolves active Gerstner octave count. That contradicts the current invariant that quality must not change water truth, and can mislead future authoring back into quality-dependent physics.
Solution: Changed the tooltip to state that the octave limit is deterministic and `GlobalQualityWeight` is telemetry-only for water truth.
Rejected Alternatives: Removing the field was rejected because `GlobalQualityWeight` still belongs in telemetry/contracts. Leaving the stale text was rejected because inspector guidance is part of the contract surface for designers and technical artists.
Scalability potential: Low, middle, high, and ultra devices keep identical water truth. Quality remains available for presentation/telemetry, not active octave authority.
Hardware Impact: 0.0 us runtime. Prevents future regression rather than saving frame time.
