# LOG - SHINOBU_SYSTEMIC_SURGEON

## 2026-05-21 Systemic DOD Sanitation Pass

What was wrong:
- Burst result lanes in selected jobs lacked proven read/write alias metadata.
- Tether/Verlet constraint output could keep stale uninitialized force/tension slots after active constraint count shrank.
- Addressables TTL scheduling resolved native views before locking the owning DataVault buffers.
- Autopilot SDF avoidance marched all feelers even when the origin SDF sample proved clear water.
- Dynamic decal ring overwrote active slots at capacity.
- Bilateral reconstruction could still over-sharpen at low render scale if CPU/material constants drifted.
- Blackbox dictionary CSV loader allocated a `string[]` per line.
- Generic dump validation for `.bin`/`.h8dump` forensic files was missing.

What was done:
- Added `[WriteOnly, NoAlias]` metadata in `FluidImpulseJob`, selected `AudioVirtualizationJobs` lanes, `SolveTetherConstraintsJob`, and `VerletConstraintRelaxationDTOJob`.
- Added `MaxTensionForceNewtons = 250000f`, denominator clamps, tension clamps, finite direction guards, capped force writes, and inactive output clearing to tether/Verlet paths.
- Reordered `AssetLifecycleGovernor.ScheduleAddressableTtlEvaluation()` to lock TTL/DataVault buffers before resolving native tracker views.
- Added an origin SDF clearance early-out to `EvaluateCollisionAvoidanceJob`.
- Added oldest-10-percent exponential fade and drop-until-faded overwrite guard to `GenerateDecalMatricesJob`.
- Added shader-side continuous scale-deficit ringing guard plus bounded reconstruction dither in `Hecton_BilateralUpsample.shader`.
- Replaced `BlackboxXRayViewer` dictionary `Split` parsing with span slicing.
- Added `TelemetryDumpValidatorWindow` at `Hecton8/Diagnostics/Telemetry Dump Validator`.

Cinematic cheats used:
- SDF feeler gate: one scalar signed-distance sample replaces full feeler raymarches in clear water.
- Decal fade: exponential opacity fake replaces physical slot-preservation complexity.
- Reconstruction guard: scale-deficit curve and tiny dither mask ringing instead of expensive reconstruction.

Exact microseconds saved:
- Verified profiler measurements: none. Build/profiler execution was blocked by CPU guard reporting 100%.
- Static estimates only: Burst alias metadata 1-8 us per active pass; tether fault recovery 1-4 us avoided; SDF early-out saves up to `activeVehicles * feelers * steps` SDF samples in clear water. These are not marked verified.

Verification:
- `git diff --check` passed for touched files.
- `dotnet build` was not launched because CPU was 100% and AGENTS.md forbids build under >50% CPU.
- No `dotnet` or `csc` process was active during the check.

Remaining blocked/pending:
- Task 01 broad private persistent allocation eviction needs explicit Vault route cards before touching GasDynamicsSolver/PersistentWorldRegistry ownership.
- Task 04 remaining AUP precision hits in VoxelDynamicNavGridRuntime, HectonWorldGenerator, CrashTelemetryBuffer, and GpuScatterLodManager were not patched.
- Task 06 GlobalSignals false-sharing risks remain pending.
- Task 11 PowerVoltageSolverJob residual convergence gap remains pending; no active call site found.
- Task 16 hull deformation GPU bridge remains pending.
- Task 18 master heatmap window remains pending.
- Task 19 runtime CSV ingestion needs a separate full pass; only the editor blackbox dictionary split was fixed.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="TetherForcePacketDTO" size="64" fields="ApplicationAUP@0 double3; Force@24 float3; Tension@36 float; CableId@40 int; BodySlot@44 int; Flags@48 uint; FrameIndex@52 uint; pad@56 ulong" />
    <DTO name="AutopilotFeelerResultDTO" size="64" fields="StartRuntime@0 float3; EndRuntime@12 float3; HitRuntime@24 float3; Repulsion@36 float3; HitDistance@48 float; SdfDensity@52 float; FeelerIndex@56 uint; Flags@60 uint" />
    <DTO name="AutopilotTuningDTO" size="128" fields="float scalars@0..28; SdfOrigin@32 float3; SdfCellSize@44 float3; SdfDimensions@56 int3; SdfRangeMeters@68 float; remaining existing fields unchanged" />
    <DTO name="DecalInstanceDTO" size="80" fields="LocalToWorld@0 float4x4; MaterialHash@64 uint; Opacity01@68 float; LifetimeSeconds@72 float; Flags@76 uint" />
    <DTO name="DecalRequestSignal" size="64" fields="ImpactAup@0 double3; Normal@24 float3; RadiusMeters@36 float; ProjectionDepthMeters@40 float; LifetimeSeconds@44 float; MaterialHash@48 uint; Flags@52 uint; StableSeed@56 uint; SourceFrame@60 uint" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="BufferID.AddressableHeapTrackers" use="TTL evaluation lock-before-resolve" />
    <BUFFER id="BufferID.AddressableHeapTimeToLive" use="TTL evaluation lock-before-resolve" />
    <BUFFER id="BufferID.AddressableHeapTrackerFlags" use="TTL evaluation lock-before-resolve" />
    <BUFFER id="BufferID.AddressableHeapHandleMap" use="TTL evaluation lock-before-resolve" />
    <BUFFER id="Autopilot owner-resolved SDF/avoidance lanes" use="SDF early-out consumed existing EncodedSdf and result buffers; no new buffer allocated" />
    <BUFFER id="DynamicDecal owner ring" use="existing DecalInstanceDTO ring; no new buffer allocated" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added to Burst jobs or shader path" />
  <AUP status="patched paths preserve double AUP subtraction before float downcast where spatial AUP math was touched" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <QUALITY status="DRS shader uses continuous scale-deficit curve; no binary low-tier switch added" />
</SELF_AUDIT>

<SELF_AUDIT id="SHINOBU_SYSTEMIC_SURGEON" pass="BulkheadBatteryFinalCadenceSinks">
  <WHAT_WAS_WRONG>
    Bulkhead closure progression and battery charger cadence still had stale quality-shaped authority paths after the broader power/construction detachment. Bulkhead used a quality-derived cadence scale inside the Burst job, and charger scheduling locked tuning state to sample quality before cadence resolution.
  </WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>
    `UpdateBulkheadClosureJob` now uses a canonical literal cadence multiplier. `BatteryChargerLogisticsRuntime` now assigns canonical authority quality directly, resolves cadence to 60Hz, and removes unused quality sampling helpers that could reintroduce editor override cadence thinning.
  </WHAT_WAS_DONE>
  <CINEMATIC_CHEATS>
    Charger/bulkhead presentation can still scale through shader globals, telemetry/UI refresh, and VFX density. The charge transfer and containment closure facts no longer scale with hardware quality.
  </CINEMATIC_CHEATS>
  <MICROSECONDS_SAVED estimate="0">
    No measured speed claim. Static effect: one hot DataVault tuning lock/read was removed from charger scheduling, and bulkhead closure no longer branches through quality math.
  </MICROSECONDS_SAVED>
  <VERIFICATION>
    Targeted scan found no `SampleQualityWeightUnderTuningLock`, `ResolvePendingQualityWeight`, `ResolveQualityWeight`, or `ResolveGlobalQualityWeight` in `BatteryChargerLogisticsRuntime.cs`. Targeted scan confirms `UpdateBulkheadClosureJob` uses a literal cadence scale. `git diff --check` passed for the touched bulkhead and charger files. Build was not launched because CPU load was 54%, above the AGENTS.md threshold.
  </VERIFICATION>
</SELF_AUDIT>

<SELF_AUDIT id="SHINOBU_SYSTEMIC_SURGEON" pass="PowerConstructionAuthorityDetachment">
  <WHAT_WAS_WRONG>
    Power and construction systems used hardware/global quality to change solver convergence, thermal cadence, adaptive solve slices, drone steering/A* cadence, docking obstacle raycast segmentation, repair signal quality tier, and bulkhead authority cadence. Those paths mutate power, heat, repair, and containment facts.
  </WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>
    `PowerSolverConvergenceMath` now exposes canonical authority quality and returns full propagation iterations, strict tolerance, omega 1.0, and full residual sampling. `PowerGrid`, `PowerGridManager`, `LogisticsNetworkGraph`, and `SubmarineOsThermalGridRuntime` feed canonical quality into power/thermal authority. `DroneFleetManager` uses canonical authority quality for steering, A* solve budget, update interval, and docking obstacle probes, and repair signals no longer poll `GlobalRegistry.ScalabilityTier`. `BulkheadContainmentRuntime` uses canonical quality for tuning and simulation cadence.
  </WHAT_WAS_DONE>
  <CINEMATIC_CHEATS>
    Shader brownout payloads, phantom drone draw count, drone render distance, bulkhead shader quality vector, and scavenging VFX multiplier remain presentation lanes. Solver truth is not used as the Dear Lie surface.
  </CINEMATIC_CHEATS>
  <MICROSECONDS_SAVED estimate="0">
    No speed claim. Weak devices spend canonical base-system authority math; visual/device savings remain in shader, draw, VFX, and telemetry fanout.
  </MICROSECONDS_SAVED>
  <VERIFICATION>
    Targeted `git diff --check` passed for power/logistics/construction files with line-ending warnings only. Targeted scans show remaining `HomeostasisBrain.GlobalQualityWeight` in `PowerGridManager` brownout shader publish, `BulkheadContainmentRuntime` shader global upload, `DroneFleetManager.ResolveGlobalQualityWeight` visual helper, and `ScavengingLootOracle` VFX multiplier only; no patched authority helper still depends on hardware/global quality.
  </VERIFICATION>
</SELF_AUDIT>

<SELF_AUDIT id="SHINOBU_SYSTEMIC_SURGEON" pass="PhysicsAuthorityQualityDetachmentII">
  <WHAT_WAS_WRONG>
    KCC SDF squeeze, seaglide hydrodynamics, exosuit kinematics, and habitat fluid incursion still converted device quality or stress into authority math. The affected outputs include SDF normals/step width, thrust cadence, metabolism cadence, force packets, collision damping, SDF collision iterations, flood solver cadence, ingress cap, and flooded-mass angular drag.
  </WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>
    `SdfSqueezeJob` now uses full authority quality and stops converting `SystemStress01` into slow-cadence output flags. `SeaglideHydrodynamicsRuntime` and `CalculateSeaglideThrustJob` use `SeaglideSimdMath.AuthoritativeQualityWeight` for solver cadence and thrust math. `ExosuitKinematicsRuntime`, `ExosuitMathGuards`, `ExosuitKinematicIntegrationJob`, and `ExosuitSdfCollisionJob` use `ExosuitMathGuards.AuthoritativeQualityWeight`. `HabitatFluidIncursionDirector` and jobs use `HabitatFluidIncursionMath.AuthoritativeQualityWeight`, max solver iterations, full ingress cap, and canonical angular drag.
  </WHAT_WAS_DONE>
  <CINEMATIC_CHEATS>
    Presentation quality remains valid only for non-authority output: seaglide audio/bubbles, KCC wake/turbulence outside this job, exosuit silt/haptics presentation, habitat waterline wobble/shader upload density, and telemetry context. Physics truth is no longer a low-tier visual fake.
  </CINEMATIC_CHEATS>
  <MICROSECONDS_SAVED estimate="0">
    No speed claim. This pass intentionally spends canonical physics math on weak devices to remove rollback divergence. Savings must be bought back in VFX/audio/shader lanes.
  </MICROSECONDS_SAVED>
  <VERIFICATION>
    Targeted `rg` found no remaining `HomeostasisBrain.GlobalQualityWeight`, `GlobalRegistry.ScalabilityTier`, `SystemStress01 &gt;`, `MathLodSurvival`, or `ScalabilityTierProfileByte` authority hits in the patched physics files. Targeted `git diff --check` passed with line-ending warnings only. Compile was not launched because CPU load reported 100%.
  </VERIFICATION>
</SELF_AUDIT>

<SELF_AUDIT id="SHINOBU_SYSTEMIC_SURGEON" pass="PredatorCognitionAlphaLeviathanAuthority">
  <WHAT_WAS_WRONG>
    Predator cognition used global quality, scalability tier, frame pressure, and high-tier steering flags to alter AI cadence, mesofauna perception/tuning, predator steering, and Alpha Leviathan stalk math. These are gameplay/encounter facts, not presentation budget.
  </WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>
    `PredatorCognitionDomain` now uses canonical cognition quality 1.0 for mesofauna quality inputs, disables retinal low-cadence mode, removes the unused scalability-tier registry poll, and forces smooth predator steering authority. `LeviathanStalkJob` now uses precision math LOD independent of `SystemStress01` and `MathLodSurvival`.
  </WHAT_WAS_DONE>
  <CINEMATIC_CHEATS>
    Dear Lie lane preserved only as presentation: predator silhouettes, SDF visual overkill, shader/noise payloads, and optional telemetry can still be scaled later without changing cognition facts.
  </CINEMATIC_CHEATS>
  <MICROSECONDS_SAVED estimate="0">
    No speed claim. This pass spends canonical AI work to remove hardware-divergent behavior; one hot `GlobalRegistry.ScalabilityTierProfileByte` read was removed from initialization.
  </MICROSECONDS_SAVED>
  <VERIFICATION>
    `rg` confirms no remaining `GlobalRegistry.ScalabilityTierProfileByte`, `_scalabilityTierProfileByte`, `SystemDispatcher.HomeostasisPressureLevel`, or frame-delta pressure reads in `PredatorCognitionDomain.cs`. `rg` confirms `LeviathanStalkJob.cs` no longer reads `SystemStress01` or `MathLodSurvival`. Targeted `git diff --check` passed with line-ending warnings only.
  </VERIFICATION>
</SELF_AUDIT>

## Hydrodynamic KCC Authority Quality Detachment

What was wrong:
- `HydrodynamicKccRuntime` allowed `GlobalQualityWeight` to alter fallback/mock input, environment mock fields, flow/SDF sampling quality, added mass, acceleration, SDF friction, slope slide, drag, collision iteration count, and rollback replay frame budget.
- These values feed movement and rollback state. They are not presentation-only and cannot vary by device.

What was done:
- Added `HydrodynamicKccMath.AuthoritativeQualityWeight = 1f`.
- Fixed `ResolveIterationCount` to return the full 8-iteration authority path.
- Routed mock input, environment mock generation, environmental force integration, slope friction, and rollback replay count through canonical authority.
- Kept real `GlobalQualityWeight` only on wake/turbulence, telemetry work estimates, and visual sync interpolation.

Cinematic Cheats used:
- Wake radius and turbulence remain presentation fakes. Low quality can still reduce visible water disturbance while kinematic state stays invariant.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: 0 us speed claim. This patch buys deterministic motion/rollback parity, not raw CPU savings.

Verification:
- Targeted scan shows authority jobs now use `HydrodynamicKccMath.AuthoritativeQualityWeight`; remaining real-quality reads are telemetry/visual sync or tuning publication.
- Targeted `git diff --check` passed for `HydrodynamicKccRuntime.cs` with only line-ending warnings.
- No dotnet build was launched under the user's command discipline.

## Buoyancy Force And Sleep Authority Quality Detachment

What was wrong:
- `BuoyancyDisplacementRuntime` used `GlobalQualityWeight` to thin evaluator stride and ambient-current wake polling.
- `EvaluateBuoyancyJob` used scheduler/tuning quality in force, drag, flow, surface snap, dense-layer density, sleep threshold, and static-promotion math.
- Those rows produce physics force packets and sleep state. They are authority, not visual LOD.

What was done:
- Added `BuoyancyDisplacementConstants.AuthoritativeQualityWeight = 1f`.
- Fixed buoyancy evaluator stride to the full authority pass.
- Fixed ambient-current poll cadence to the canonical full-quality path.
- Routed `EvaluateBuoyancyJob` force/sleep math through canonical quality 1.0.
- Kept real quality in telemetry and editor SIMD benchmark lanes only.

Cinematic Cheats used:
- No physical shortcut remains in force/sleep truth. Quality is still available for debug/visual surfaces where buoyancy can be represented as presentation rather than force authority.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: 0 us speed claim. This patch removes deterministic divergence and may cost more on weak devices by restoring canonical physics.

Verification:
- Targeted scan shows no `schedulerQuality` or `tuningQuality` input in the buoyancy evaluator kernel.
- Targeted `git diff --check` passed for `BuoyancyDisplacementContracts.cs`, `BuoyancyDisplacementJobs.cs`, and `BuoyancyDisplacementRuntime.cs` with only line-ending warnings.
- No dotnet build was launched under the user's command discipline.

## Tether Verlet Authority Quality Detachment

What was wrong:
- `TetherInstance` used quality tier and `HomeostasisBrain.GlobalQualityWeight` to alter Verlet point count, default constraint iterations, and fallback damping.
- Those settings affect constraint force and payload motion. They are not visual-only.

What was done:
- Default Verlet constraint iterations now use `VerletUltraIterationCount`.
- Verlet segment count now uses the canonical `VerletDefaultSegmentCount`.
- Fallback velocity damping now uses `VerletHighVelocityDamping`.
- Explicit tuning override for constraint iterations remains because it is authored tuning, not hardware pressure.

Cinematic Cheats used:
- The low-quality taut straight-line cable remains confined to `UpdateVerletVisualUpload`, so presentation can collapse curvature without changing force authority.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: 0 us speed claim; canonical authority may cost more on weak devices.

Verification:
- Targeted scan confirms quality weight is no longer consumed by `ResolveVerletIterationCount`, `ResolveVerletSegmentCount`, or `ResolveVerletVelocityDamping`.
- Targeted `git diff --check` passed for `TetherInstance.cs` with only line-ending warnings.
- No dotnet build was launched under the user's command discipline.

## Vegetation Abyssal Path Authority Tier Removal

What was wrong:
- `VegetationNavGridSynchronizer` polled `GlobalRegistry.ScalabilityTier` inside path scheduling.
- Low/Mid tiers reduced string-pull portal lookahead and DDA samples, changing the final route.

What was done:
- Removed the hot registry tier read from abyssal path smoothing.
- `ResolveAbyssalPathPortalLookAhead` now returns the canonical high-lookahead value.
- `ResolveAbyssalPathDdaSampleCap` now uses the configured safe cap rather than low/mid caps.

Cinematic Cheats used:
- None in authority. Path debug/visualization remains the valid presentation lane for future shedding.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: 0 us speed claim; route parity is the correction.

Verification:
- Targeted scan shows no `GlobalRegistry.ScalabilityTier` read in `VegetationNavGridSynchronizer.cs`.
- Targeted `git diff --check` passed for `VegetationNavGridSynchronizer.cs` with only line-ending warnings.
- No dotnet build was launched under the user's command discipline.

## Stress Spawn Director Authority Quality Detachment

What was wrong:
- `StressDrivenSpawnDirector` let `GlobalQualityWeight` alter candidate score, budget, spawn probability, hidden spawn radius, distant despawn radius, and spawned cognition radii/speed/sensory weights.
- Those values decide encounter truth and AI initialization.

What was done:
- Added `StressDrivenSpawnDirector.AuthoritativeQualityWeight = 1f`.
- Candidate scoring, threat budget, spawn probability, hidden placement radius, distant cull radius, debug cull radius, and cognition-input quality now use the authoritative constant.
- Input/selection/telemetry still store real quality as forensic context.

Cinematic Cheats used:
- None in encounter truth. Future cost shedding belongs in spawn debug visualization, density presentation, audio/VFX, or purely visual population surfacing.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: 0 us speed claim; deterministic encounter parity is the correction.

Verification:
- Targeted scan found no remaining `Smooth01(input.GlobalQualityWeight)`, `selection.GlobalQualityWeight` behavior math, or `math.lerp(...GlobalQualityWeight...)` in the patched authority expressions.
- Targeted `git diff --check` passed for `StressDrivenSpawnDirector.cs`.
- No dotnet build was launched under the user's command discipline.

## 2026-05-21 - SHINOBU_SYSTEMIC_SURGEON Physiology Haldane Authority Quality Detachment

What was wrong: `ShinobuPhysiologyRuntime` let `GlobalQualityWeight` change authoritative physiology cadence, and `IntegrateBloodGasTensionsJob` reduced active Haldane tissue compartments on lower quality. That made decompression risk, bends flags, gas stress signals, and damage timing depend on hardware pressure.

What was done: physiology authority cadence is fixed at `AuthoritativeUpdateIntervalSeconds = 0.016f`; the blood-gas and CNS jobs receive `AuthoritativeQualityWeight = 1f`; `ResolveActiveCompartmentCount` returns `ShinobuPhysiologyConstants.TissueCompartmentCount` for all callers. The existing 16-byte `TissueCompartmentDTO`, 80-byte `DecompressionStateDTO`, and 64-byte `PhysiologyStateSignal` layouts were not changed.

Cinematic Cheats used: none in health truth. Quality remains eligible only for visual hypoxia/narcosis presentation after route separation.

Exact Microseconds saved: 0 us claimed. This pass intentionally spends full 16-compartment authority math on weak devices to remove rollback divergence.

Verification: targeted `git diff --check` passed for `ShinobuPhysiologyJobs.cs` and `ShinobuPhysiologyRuntime.cs` with only LF-to-CRLF warnings. No dotnet build was launched.

## 2026-05-21 - SHINOBU_SYSTEMIC_SURGEON Ecosystem Swarm Authority Quality Detachment

What was wrong: `ShinobuEcosystemBalancer` let `GlobalQualityWeight` and stress reduce active boid rows, skip update lanes, shrink spatial hash/neighbor budgets, widen simulation delta, weaken neighbor solve weight, and reduce macro rehydration spawn density. Those rows feed biomass, symbiosis, and encounter threat cost, so the branch was not visual-only.

What was done: added a canonical `AuthoritativeQualityWeight` and routed simulation truth through it. Active entity budget now keeps full capacity, update stride is fixed to 1, neighbor/sample/hash budgets stay full, flocking uses fixed 1/60 second delta and full solve weight, and the macro biomass pass receives authority quality. The render payload and culling params keep the real visual quality weight.

Cinematic Cheats used: render density and GPU culling remain the visual fake; ecology truth no longer fakes fewer living rows.

Exact Microseconds saved: 0 us claimed. This pass trades low-tier shortcut savings for deterministic biomass/encounter truth; visible swarm rendering remains the scalable budget.

Verification: targeted `git diff --check` passed for `ShinobuEcosystemBalancer.cs` with only LF-to-CRLF warning. No dotnet build was launched.

## Habitat Hydrodynamic Stress Registry Poll Removal

What was wrong:
- `HabitatGraphManager.ApplyHydrodynamicStress` read `GlobalRegistry.ScalabilityTier` inside the recurring hydrodynamic stress phase.
- The tier then influenced analytical stress precision, flood traversal pressure-root math, module stress upload behavior, and low-tier stress feedback.

What was done:
- Replaced the hot registry read with a canonical `HectonQualityTier.Ultra` authority path for this pass.
- Existing helper signatures and legacy visual flags remain intact to avoid ABI churn; the active runtime no longer lets hardware tier change pressure/flood/stress truth.

Cinematic Cheats used:
- None in the authority path. Habitat pressure and flood propagation are gameplay truth. Visual savings must move to shader stress upload density, groan cadence, and optional telemetry in a separate pass.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: 0 us speed claim. The pass removes a hot registry poll and deterministic divergence, but may execute more full analytical math on weak devices.

Verification:
- `rg` found no `GlobalRegistry.ScalabilityTier` in `HabitatGraphManager.cs`.
- Targeted `git diff --check` passed for `HabitatGraphManager.cs`; only line-ending warnings were reported.
- Compile was not launched because CPU load reported 100%, and AGENTS.md forbids launching a build over 50%.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="HabitatFloodConnection" size="16" fields="DestinationIndex@0 int; CsrEdgeIndex@4 int; FlowResistance@8 float; Reserved0@12 uint" />
    <DTO name="HabitatFloodBlackBoxEntry" size="48" fields="Frame@0 int; NodeCount@4 ushort; EdgeCount@6 ushort; FloodedRoomCount@8 ushort; Reserved0@10 ushort; BaseTotalStress@12 float; MaxRoomWaterLevel01@16 float; TotalWaterVolumeM3@20 float; TotalIngressVolumeM3@24 float; StateHash@28 uint; Flags@32 uint; pad@36 uint; pad@40 ulong" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="HabitatGraphManager existing owner-local graph arrays" use="flood/stress arrays remain sentinel-registered scene-lifetime owner data; no new Vault buffer ID created" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="no coordinate conversion changed in this pass" />
  <QUALITY_TRUTH status="hardware tier no longer changes habitat pressure/flood/stress authority" />
  <DEPENDENCIES status="hot GlobalRegistry scalability poll removed; no direct sibling assembly dependency added" />
  <COMPLETE_CALLS status="no .Complete() added; existing flood propagation fence unchanged" />
</SELF_AUDIT>

## RTG / FluidPipe / Metabolism / WorldSampler / Laser Truth Detachment Pass

What was wrong:
- RTG isotope decay changed cadence by hardware tier, moving output/dead-state transitions between ColdTick and FrostTick.
- Fluid pipe pressure solving changed cadence through `GlobalRegistry.ScalabilityTier`, so rupture and room-exchange timing could diverge.
- Metabolism used quality to stretch the integration cadence and to degrade thermal/chemical sampling used by health truth.
- `GlobalWorldSampler` used quality for terrain/SDF sample cadence, interpolation quality, normal estimation, and raymarch step count.
- Laser cutter SDF carve progress used the quality curve that should belong only to sparks/glow/presentation.

What was done:
- RTG decay now runs on one 1s authority cadence; FrostTick registration and the serialized force-low cadence route are gone.
- `RtgDecayJob` uses deterministic Burst flags and `[ReadOnly]/[WriteOnly]/[NoAlias]` metadata on its native slices.
- Fluid pipe runtime uses `FluidPipeGraphConstants.AuthoritativeCadenceSeconds` at 0.1s and no longer reads scalability tier. The legacy LOD helper returns the same cadence for ABI callers.
- `FluidPipeRuptureRecord` is explicit 48 bytes, and `FluidPipePressureSolveJob` uses deterministic Burst flags plus precise alias metadata.
- Metabolism cadence is fixed to `NominalSlowTickSeconds`, and `MetabolicIntegrationJob` receives canonical quality 1.0 for authority sampling. Telemetry and shader globals still receive real quality.
- `GlobalWorldSampler` authority helpers now return canonical full-quality sampling, raymarch uses full step budget, and telemetry still records the incoming quality weight.
- Laser carve progress uses an authoritative curve of 1.0; spark count, glow lifetime/radius, impact intensity, and work estimate remain visual quality outputs.

Cinematic Cheats used:
- RTG/pipe/metabolism/world sampling are gameplay truth and were not cheapened.
- Laser retained the Dear Lie: shader dents, sparks, decals, and glow continue to scale continuously while actual carve/battery progress remains invariant.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimates: RTG below 1 us per decay batch from alias proof; fluid pipe below 1 us per solve from alias proof; metabolism/world sampler/laser truth paths make 0 us speed claims because they intentionally spend canonical math for parity.

Verification:
- RTG scans found no `UsesLowTierCadence`, `forceLowTierCadence`, `LowTierCadence`, `IFrostTickable`, `FrostTick`, `_registeredFrost`, or `GlobalRegistry.ScalabilityTier` in `RadioisotopeThermalGenerator.cs`.
- Fluid pipe scans found runtime cadence uses `AuthoritativeCadenceSeconds`; `FluidPipePressureSolveJob` has deterministic Burst flags and alias metadata.
- Metabolism scans confirm fixed cadence and `integrationJob.GlobalQualityWeight = 1f`, while telemetry still receives `quality`.
- World sampler scans confirm canonical quality functions and raymarch quality of 1.0.
- Laser scans confirm `authoritativeCarveCurve = 1f` feeds `EstimateSdfCarve01` while quality still drives visual outputs.
- Targeted `git diff --check` passed for all files in this pass; only line-ending warnings were reported.
- Compile was not launched because CPU load reported 100%, and AGENTS.md forbids launching a build over 50%.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="RtgTelemetryEntry" size="32" fields="Frame@0 uint; SourceId@4 uint; OutputWatts@8 float; NormalizedOutput01@12 float; AverageHealth01@16 float; ActiveRtgs@20 ushort; Flags@22 byte; pad@23 byte; pad@24 ulong" />
    <DTO name="FluidPipeRuptureRecord" size="48" fields="NodeIndex@0 int; NetworkId@4 int; RoomIndex@8 int; FrameIndex@12 int; PressureKPa@16 float; Contents@20 float; Flow01@24 float; NodeHash@28 uint; ContentKind@32 byte; Flags@33 byte; Reserved@34 ushort; pad@36 uint; pad@40 ulong" />
    <DTO name="FluidPipeTelemetryEntry" size="32" fields="FrameIndex@0 int; NodeCount@4 int; RuptureCount@8 int; NanCount@12 int; TotalWater@16 float; TotalOxygen@20 float; MaxPressureKPa@24 float; StateHash@28 uint" />
    <DTO name="TerrainSampleResult" size="64" fields="Normal@0 float3; Distance@12 float; LocalPosition@16 float3; HeightMeters@28 float; Distance2D@32 float; Distance3D@36 float; SeaDistance@40 float; GradientEpsilon@44 float; StateHash@48 uint; SectorIndex@52 ushort; MaterialID@54 byte; Flags@55 byte; SampleRevision@56 int; BiomeHash@60 uint" />
    <DTO name="GlobalWorldSamplerTelemetryEntry" size="64" fields="Distance@0 float; SdfDistance@4 float; Height@8 float; SmoothMinEstimateNs@12 int; Frame@16 uint; QueryHash@20 uint; SampleCount@24 int; WarningCode@28 int; Normal@32 float3; MaterialID@44 byte; Flags@45 byte; SectorIndex@46 ushort; Reserved0@48 int; Reserved1@52 int; Reserved2@56 int; Reserved3@60 int" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="RtgStartTimes/RtgHalfLives/RtgBaseOutput/RtgCurrentOutput/RtgOutputNormalized/RtgFlags/RtgTelemetryRing" use="existing RTG Vault lanes; no new buffer ID created" />
    <BUFFER id="FluidPipe existing owner-local scene scratch" use="local graph arrays remain sentinel-registered scene scratch; full Vault migration still needs approved BufferIDs" />
    <BUFFER id="Metabolism existing Vault handles" use="states, AUPs, exertion, toxin, rule, tuning, thermal/chemical readback, telemetry, signal lanes; no new buffer ID created" />
    <BUFFER id="GlobalWorldSampler existing Vault aliases" use="height, SDF, materials, counters, telemetry, biome, erosion, active sectors; no new buffer ID created" />
    <BUFFER id="LaserCutter existing Vault lanes" use="requests, hit results, deformation, battery drain, decals, VFX, telemetry; no new buffer ID created" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="metabolism, world sampler, and laser preserve double-space origin subtraction before local float math" />
  <QUALITY_TRUTH status="RTG power, pipe pressure, metabolism state, terrain/SDF sampling, and laser carve progress no longer change by hardware quality" />
  <SCALABILITY status="quality remains in presentation/telemetry lanes: metabolism shader globals, world sampler telemetry/probe, laser sparks/glow/VFX" />
  <DEPENDENCIES status="no direct sibling assembly dependency added" />
  <COMPLETE_CALLS status="no .Complete() added; existing dispatcher and teardown fences unchanged" />
  <ALIASING status="RTG and fluid pipe jobs now declare read-only/write-only/noalias lanes where proven" />
</SELF_AUDIT>

## Construction Deconstruction DFS Authority Repair

What was wrong:
- `ConstructionManager.ProcessDeconstructionRequestAfterRayValidated` derived a `skipDfs` flag from hardware tier.
- `HabitatGraphManager.TryValidateDeconstructionRollback` accepted that flag and returned success without isolation DFS on Unknown/Low/Mx350, changing construction legality by device.

What was done:
- Removed the runtime tier decision and pinned deconstruction validation to the single authority route.
- Removed the skip parameter from `TryValidateDeconstructionRollback`, making the DFS gate non-optional at the call surface.

Cinematic Cheats used:
- None. This is topology truth, not presentation. Performance must be bought through fixed graph storage and dispatcher cadence, not by skipping legality checks.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: 0 us speed claim. The pass removes divergent construction truth rather than optimizing the DFS.

Verification:
- `rg` found no `ShouldSkipDeconstructionDfsForTier` or `skipIsolationDfs` in the patched construction files.
- Targeted `git diff --check` passed for `ConstructionManager.cs` and `HabitatGraphManager.cs`; only line-ending warnings were reported.
- Compile was not launched because CPU load reported 100%, and AGENTS.md forbids launching a build over 50%.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="unchanged" reason="no new construction DTO or signal struct was created in this pass" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="existing construction/habitat graph owners" use="unchanged; no new Vault buffer ID created" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="no spatial math changed" />
  <QUALITY_TRUTH status="hardware tier no longer changes deconstruction graph legality" />
  <DEPENDENCIES status="no direct sibling assembly dependency added" />
  <COMPLETE_CALLS status="no .Complete() added" />
</SELF_AUDIT>

## Loot Magnet Authority Pull Determinism And Alias Pass

What was wrong:
- The prior low-tier lerp path changed loot attraction and acquisition timing by hardware tier.
- `LootMagnetJob` mutated authoritative loot AUP/velocity/flags under `FloatMode.Fast` and exposed native lanes without full alias/read-write metadata.
- `LootMagnetVaultViews` still used a C# property for created-buffer checks.

What was done:
- Verified no active `LowTierMode`, `lowTierMode`, or `IsLowTier` route remains in loot magnet scheduling.
- Switched `LootMagnetJob` to deterministic Burst compile flags.
- Marked `EntityAups`, `EntityFlags`, and `EntityVelocities` as non-overlapping read/write lanes with `[NoAlias]`; marked item hash/quantity inputs `[ReadOnly, NoAlias]`; marked signal events `[WriteOnly, NoAlias]`.
- Removed the unused `LowTierLerpRate` constant and converted `LootMagnetVaultViews.IsCreated` into a static `in` helper.

Cinematic Cheats used:
- Kept acoustic/wake signal emission as the presentation fake. Quality pressure belongs in those presentation budgets and shader/audio detail, not in loot movement truth.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us per active pull batch on i3/MX350-class CPUs from alias proof. Main gain is deterministic acquisition parity.

Verification:
- `rg` found no active `LowTierMode`, `lowTierMode`, `IsLowTier`, or `LowTierLerpRate` in loot magnet files.
- `rg` confirmed deterministic Burst flags and `[NoAlias]` metadata on the pull job native lanes.
- Targeted `git diff --check` passed for `LootMagnetSystem.cs`, `LootMagnetPullJob.cs`, and `LootMagnetContracts.cs`; only line-ending warnings were reported.
- Compile was not launched because CPU load reported 100%, and AGENTS.md forbids launching a build over 50%.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="LootMagnetSignalEvent" size="128" fields="PositionAup@0 AbsoluteUniversePosition(48); Velocity@48 float3(12); ItemHash@60 uint; Quantity@64 uint; DistanceSq@68 float; Frame@72 uint; Flags@76 uint; pad@80..127 six ulongs" />
    <DTO name="LootMagnetTelemetryEntry" size="128" fields="PlayerAup@0 AbsoluteUniversePosition(48); SampleLootAup@48 AbsoluteUniversePosition(48); Frame@96 uint; ActiveCount@100 uint; ActiveLootPullsCount@104 uint; AcquiredCount@108 uint; FlagsHash@112 uint; Flags@116 uint; PeakMagnetVelocity@120 float; Reserved@124 uint" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="LootMagnet existing handles" use="EntityAups, EntityFlags, EntityVelocities, EntityItemHashes, EntityQuantities, SignalEvents, Telemetry; no new buffer ID created" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="pull job preserves AUP double cell math and casts only local deltas to float3" />
  <QUALITY_TRUTH status="hardware tier no longer changes loot pull/acquisition truth" />
  <DEPENDENCIES status="no direct sibling assembly dependency added" />
  <COMPLETE_CALLS status="no .Complete() added; existing dispatcher swap path preserved" />
  <ALIASING status="EntityAups/EntityFlags/EntityVelocities [NoAlias]; item inputs [ReadOnly, NoAlias]; SignalEvents [WriteOnly, NoAlias]" />
</SELF_AUDIT>

## Narrative POI Quality Continuum And Hash Row Pinning

What was wrong:
- `HectonNarrativeDirector` still used `GlobalRegistry.ScalabilityTier` to select POI scan cadence and to enable a branch-safe dominant-axis pre-cull.
- The native `NarrativeNode` value stored in `NativeHashMap<uint, NarrativeNode>` had implicit layout instead of a pinned ARM64-safe row.
- Triggered POI result arrays were producer-only but lacked `[WriteOnly]` metadata.

What was done:
- Replaced binary low/default/high POI scan cadence with `HomeostasisBrain.GlobalQualityWeight` smoothstep interpolation from `1.0s` to `0.5s`.
- Made the dominant-axis pre-cull unconditional because it is a safe rejection before the exact `math.lengthsq` radius test and does not alter POI truth.
- Pinned `NarrativeNode` to `[StructLayout(LayoutKind.Explicit, Size = 16)]` and marked triggered output arrays `[WriteOnly, NoAlias]`.

Cinematic Cheats used:
- Kept narrative POI detection as a cheap mathematical sphere test over 64 native slots. No trigger colliders, per-POI GameObject polling, or physics overlap queries were introduced.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us per POI scan from removing tier polling and improving Burst alias intent; runtime proof remains pending.

Verification:
- Targeted scan found no `LowTierAupScanIntervalSeconds`, `DefaultAupScanIntervalSeconds`, `HighTierAupScanIntervalSeconds`, `ShouldUseDominantAxisPreCull`, or `GlobalRegistry.ScalabilityTier` remaining in `HectonNarrativeDirector.cs`.
- Targeted `git diff --check` passed for `HectonNarrativeDirector.cs` with only line-ending warnings.
- Compile was not launched because CPU load reported `100%` despite no active compiler process names.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="NarrativeNode" size="16" fields="DiscoveryHash@0 uint; Reserved0@4 ushort; Reserved1@6 ushort; pad@8 ulong" />
    <DTO name="NarrativeTriggerTelemetryEntry" size="80" fields="Frame@0 uint; PoiHash@4 uint; StateMask@8 ulong; PlayerGridX@16 long; PlayerGridY@24 long; PlayerGridZ@32 long; PlayerRuntime@40 float3; PoiRuntime@52 float3; Flags@64 byte; pad@65 byte; pad@66 ushort; pad@68 uint; pad@72 ulong" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-created" use="Narrative POI arrays remain owner-local scene lifetime and NativeMemorySentinel-registered until approved Narrative BufferIDs/route card exist" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="existing runtime-local POI slots and AUP grid guards preserved; no absolute float hash route added" />
  <QUALITY_TRUTH status="hardware tier no longer selects POI scan cadence or pre-cull branch" />
  <SCALABILITY status="POI cadence scales continuously through HomeostasisBrain.GlobalQualityWeight; POI identity/save mask/signal ownership unchanged" />
  <COMPLETE_CALLS status="no .Complete() added; existing DispatcherJobFence finalization path preserved" />
  <ALIASING status="triggered output arrays now carry [WriteOnly, NoAlias]; read/write state/count arrays remain unrestricted except [NoAlias]" />
</SELF_AUDIT>

## Player Narcosis Hardware Branch Removal

What was wrong:
- `HectonPlayerMovement` set `_runtimeNarcosisLowTierStaticLookOnly` from `SystemInfo.graphicsMemorySize <= 2048`.
- During narcosis, low-memory devices returned only scaled look input and skipped the authored deterministic look drift.

What was done:
- Removed the low-memory boolean and its `Awake()` assignment.
- Renamed the scale floor constant to `RuntimeNarcosisLookScaleFloor`.
- Kept severity-driven look scaling and always executes the deterministic triangle-wave drift when the player provides look intent.

Cinematic Cheats used:
- Narcosis remains a cheap deterministic triangle-wave input/camera illusion. No postprocess dependency, camera physics, or per-frame animation asset sampling was added.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: 0 us speed claim; low devices spend the same scalar drift math as high devices to preserve gameplay feedback parity.

Verification:
- Targeted scan found no `RuntimeNarcosisLowTierLookScaleFloor`, `_runtimeNarcosisLowTierStaticLookOnly`, or low-memory `SystemInfo.graphicsMemorySize <= 2048` narcosis branch remaining in `HectonPlayerMovement.cs`.
- Targeted `git diff --check` passed for `HectonPlayerMovement.cs` with only line-ending warnings.
- Compile was not launched because CPU load reported `100%`.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="none-modified" size="n/a" fields="narcosis patch changes scalar control math only; no DTO or signal ABI changed" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-created" use="player movement narcosis state remains owner-local scalar state; no native buffer route changed" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="no spatial calculation changed" />
  <QUALITY_TRUTH status="graphics-memory tier no longer changes narcosis look feedback" />
  <SCALABILITY status="quality shedding must remain on visual/audio presentation layers, not player input feedback" />
  <COMPLETE_CALLS status="no .Complete() added" />
  <ALIASING status="no NativeArray field changed in this patch" />
</SELF_AUDIT>

## Player/Vehicle CCD Quality Truth Detachment

What was wrong:
- Player and vehicle scheduled sweep consumption changed collision response by hardware tier through `KinematicCcdMath.IsLowTier(...)`.
- Low tier skipped slide and squeeze paths and could zero projected velocity where high tier continued geometry-aware movement.

What was done:
- `HectonPlayerMotor` scheduled sweep now keeps `lowTierStop = false` for authoritative CCD.
- Player SDF squeeze now uses the full gradient sample path for movement truth.
- `VehicleMotor` scheduled sweep now keeps `lowTierStop = false` and no longer polls `GlobalRegistry.ScalabilityTierProfileByte` in the sweep hot path.

Cinematic Cheats used:
- None in collision truth. The patch intentionally refuses to use hardware quality as a movement fake. Visual/audio consequences remain downstream signal consumers.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: 0 us speed claim; low hardware may pay a small additional sweep resolution cost, buying deterministic movement correctness.

Verification:
- Source scan shows no remaining `KinematicCcdMath.IsLowTier(...)` use in `HectonPlayerMotor.cs` or `VehicleMotor.cs`.
- Targeted `git diff --check` passed for both files with only line-ending warnings.
- Compile was not launched because CPU load reported 100%.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="ScheduledSweepState" status="unchanged existing player/vehicle sweep state" />
    <DTO name="HighSpeedImpactSignal" status="existing 64-byte signal layout unchanged" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none added" use="movement CCD patch does not allocate or add Vault handles" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="impact signals still resolve runtime point to AUP before publish" />
  <COMPLETE_CALLS status="no .Complete() added" />
  <QUALITY status="hardware tier no longer changes player/vehicle CCD position or velocity truth" />
</SELF_AUDIT>

## Inventory Economy Alias Metadata Pass

What was wrong:
- Inventory ledger jobs had clear producer/consumer lanes but exposed most `NativeArray<T>` fields without `[NoAlias]`, `[ReadOnly]`, or `[WriteOnly]`.
- Burst had less proof than the actual ledger dataflow.

What was done:
- Marked immutable recipe/query/index lanes `[ReadOnly, NoAlias]`.
- Marked result, telemetry, carry totals, equip, broken-tool, destroyed-debris, craftable, accepted, and mock consume lanes `[WriteOnly, NoAlias]`.
- Left hash/quantity/durability transaction lanes read-write with `[NoAlias]` because ledger helpers read current slot state before mutation.

Cinematic Cheats used:
- None. This is an authoritative ledger job-contract pass.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us per ledger batch; primary gain is Burst alias/vectorization eligibility.

Verification:
- Targeted source scan confirmed all active inventory ledger job native lanes now carry access metadata.
- Targeted `git diff --check` passed for `Shinobu19EconomyLedger.cs` with only line-ending warnings.
- Compile was not launched because CPU load reported 100%.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="CraftingRecipeDTO" status="unchanged explicit ledger DTO" />
    <DTO name="EconomyTelemetryEntry" status="unchanged ledger telemetry row" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="existing inventory ledger buffers" use="no new persistent buffers or Vault IDs added" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="loot magnet already subtracts PlayerAup - SectorOriginAup before float local query; unchanged" />
  <COMPLETE_CALLS status="no .Complete() added" />
  <ALIASING status="inventory ledger NativeArray lanes now carry explicit read/write/noalias metadata where proven" />
</SELF_AUDIT>

## Inventory Economy Burst Determinism Flags

What was wrong:
- Fifteen `Shinobu19EconomyLedger` jobs used bare `[BurstCompile]`.
- These jobs mutate or validate inventory/crafting/loot state, so implicit Burst mode is not acceptable for rollback-sensitive gameplay truth.

What was done:
- Replaced every bare inventory ledger Burst attribute with `CompileSynchronously = true`.
- Set `FloatMode = FloatMode.Deterministic`.
- Set `FloatPrecision = FloatPrecision.Standard`.

Cinematic Cheats used:
- None. This is compiler-contract hardening for authoritative economy kernels.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: 0 us steady-state; deterministic compile mode may trade speed for cross-platform truth safety.

Verification:
- `rg \"\\[BurstCompile\\]\" Assets/_Project/Scripts/Inventory/Shinobu19EconomyLedger.cs` returns no hits.
- `rg \"BurstCompile\\(\"` confirms all fifteen ledger job attributes now include deterministic flags.
- Targeted `git diff --check` passed for `Shinobu19EconomyLedger.cs` with only line-ending warnings.
- Compile was not launched because the first guard had active `csc`/`dotnet` at 99.81% CPU and the follow-up guard still reported 98.66% CPU.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="CraftingRecipeDTO" status="existing explicit inventory ledger DTO; unchanged in this pass" />
    <DTO name="MockConsumeSignal" status="existing ledger signal DTO; unchanged in this pass" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="existing inventory ledger buffers" use="no new persistent buffers or Vault IDs added" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="no spatial math changed" />
  <COMPLETE_CALLS status="no .Complete() added" />
  <BURST status="15 authoritative ledger jobs now use explicit deterministic Burst compile flags" />
</SELF_AUDIT>

## Combat Damage Quality Truth Detachment

What was wrong:
- `ProcessDamageQueueJob` used binary low/high math LOD to decide whether armor-normal projection affected damage.
- That made authoritative health dependent on hardware quality and global tier state.

What was done:
- Removed hot `GlobalRegistry.MathPrecision` and `GlobalRegistry.ScalabilityTier` reads from combat runtime policy.
- Cached `SignalBusRegistry.GlobalQualityWeight01` at schedule time.
- Always evaluates finite directional armor proof for damage truth.
- Uses continuous smoothstep quality only for visual wound detail: surface-normal amplitude and deterministic high-fidelity wound dither.

Cinematic Cheats used:
- Wound detail is now a deterministic visual dither over the result stream instead of a heavier CPU wound solver. Health/status truth remains quality-invariant.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us per combat batch; primary gain is deterministic truth separation and one less hot global-tier dependency.

Verification:
- Source scan found no remaining `ResolveRuntimeMathLod`, `_cachedMathPrecision`, `_cachedScalabilityTier`, `GlobalRegistry.ScalabilityTier`, or `GlobalRegistry.MathPrecision` references in `CombatDamageRuntime.cs`.
- Targeted `git diff --check` passed for `CombatDamageRuntime.cs` with only line-ending warnings.
- Compile was not launched because CPU load reported 100%.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="CombatDamageResult" size="128" fields="TargetId@0 int; SourceId@4 int; DamageType@8 uint; StatusBits@12 uint; PreviousHealth@16 float; NextHealth@20 float; AppliedDamage@24 float; MaxHealth@28 float; Direction@32 float3; TraumaLevel@44 byte; Flags@46 ushort; Channel@48 byte; DirectionOctant@49 byte; LocalPoint@52 float3; SurfaceNormal@64 float3; Depth@76 float; tail padding@80..127" />
    <DTO name="CombatTelemetryEntry" size="64" fields="FrameIndex@0 uint; Sequence@4 uint; hashes@8..28 uint; health/damage@32..40 float; LocalPoint@44 float3; Flags@56 ushort; TraumaLevel@58 byte; DirectionOctant@59 byte; Reserved@60 uint" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none added" use="CombatDamageRuntime still owns local sentinel-registered arrays; full Vault eviction blocked pending approved CombatDamageRuntime BufferIDs/route card" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="no spatial AUP math changed" />
  <COMPLETE_CALLS status="no .Complete() added" />
  <ALIASING status="previous read/write/noalias job metadata preserved" />
  <QUALITY status="GlobalQualityWeight affects visual detail only; damage, health, status, and result ownership do not scale with hardware quality" />
</SELF_AUDIT>

## Russell Alias Namespace Verification

What was wrong:
- Namespace verification was required after the top-hit alias pass.
- A stale assumption treated `[NoAlias]` as an unsafe-namespace attribute. Local package source proves it is `Unity.Burst.NoAliasAttribute`.

What was done:
- Read `Library/PackageCache/com.unity.burst*/Runtime/NoAliasAttribute.cs`.
- Confirmed Debris/Combat already have `using Unity.Burst;`.
- Removed the unnecessary Debris/Combat unsafe namespace imports to preserve using discipline.

Cinematic Cheats used:
- None. This is a compile-hygiene closure for Burst alias metadata.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: 0 us runtime; keeps the previous below-1-us alias/vectorization proof viable.

Verification:
- Package source confirms `[NoAlias]` resolves through `Unity.Burst`.
- Targeted `git diff --check` passed for scanner/acoustic/debris/combat/Delta Crusher files and log files with only line-ending warnings after namespace cleanup.
- Compile was not launched because active `csc` and `dotnet` processes were present and CPU load reported 100%.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <LANE name="DebrisSimulationJob.ReadStates" access="ReadOnly NoAlias" />
    <LANE name="DebrisSimulationJob.WriteStates" access="WriteOnly NoAlias" />
    <LANE name="ProcessDamageQueueJob.Results" access="WriteOnly NoAlias" />
    <LANE name="ProcessCombatStatusJob.ResultsBySlot" access="WriteOnly NoAlias" />
    <LANE name="ProcessCombatStatusJob.ResultActiveBySlot" access="WriteOnly NoAlias" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="existing debris/combat owner lanes" use="no new persistent buffers or Vault IDs added" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="no spatial math changed" />
  <COMPLETE_CALLS status="no .Complete() added" />
  <ALIASING status="NoAlias metadata resolves through Unity.Burst; unused unsafe imports were removed" />
</SELF_AUDIT>

## Russell Scan Top-Hit Alias Pass

What was wrong:
- The delegated scan found scanner, acoustic path, debris simulation, and combat jobs with output-only or read-only native lanes missing explicit access metadata.
- Combat authoritative state jobs used bare `[BurstCompile]` instead of deterministic compile flags.

What was done:
- `ScannerSpatialQueryJob` result/result-count/query-stat outputs now carry `[WriteOnly, NoAlias]`.
- `AcousticPathJob` now marks read-only graph lanes, read-write scratch lanes, and write-only result lane explicitly.
- `DebrisSimulationJob` now marks front state read-only and back state write-only.
- `ProcessDamageQueueJob` and `ProcessCombatStatusJob` now use deterministic Burst directives, read-only input metadata, write-only result lanes, and `[NoAlias]` on read-write health/status/scratch lanes.

Cinematic Cheats used:
- Preserved existing approximate scanner, acoustic graph, and visual debris paths. No expensive raycast fanout, CPU acoustic simulation, or rigidbody debris path was introduced.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us per affected cadence; primary gain is stricter Burst alias proof and deterministic compile behavior.

Verification:
- Source scans confirm the patched scanner/acoustic/debris/combat native lanes carry the intended attributes.
- Targeted `git diff --check` passed for all four files.
- Compile was not launched because CPU reported 100%.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <LANE name="ScannerSpatialQueryJob.Results/ResultCount/QueryStats" access="WriteOnly NoAlias" />
    <LANE name="AcousticPathJob.Result" access="WriteOnly NoAlias" />
    <LANE name="DebrisSimulationJob.WriteStates" access="WriteOnly NoAlias" />
    <LANE name="Combat result lanes" access="WriteOnly NoAlias" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="existing scanner/acoustic/debris/combat owner buffers" use="no new buffers allocated" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="scanner/acoustic/combat coordinate logic unchanged" />
  <COMPLETE_CALLS status="no .Complete() added" />
  <ALIASING status="subagent top-hit producer-only lanes patched; read-write blockers left read-write" />
</SELF_AUDIT>

## Delta Crusher Output Alias And Continuous Cap Curve

What was wrong:
- `ShinobuDeltaCrusherJobs.cs` had producer-only native lanes without explicit `[WriteOnly, NoAlias]`.
- The active debris cap and particles-per-carve route used low/high tier booleans instead of the continuous `GlobalQualityWeight01` scalar.

What was done:
- Added `[WriteOnly, NoAlias]` to accepted/result/stat/debris output lanes and `[ReadOnly, NoAlias]` to proven read-only lanes.
- Added `ShinobuDeltaCrusher.SmoothQuality01` and changed `ResolveDebrisCap` to accept a quality scalar.
- `CarveDebrisComputeRenderer` now caches `SignalBusRegistry.GlobalQualityWeight01` once per tick and uses it for active capacity and particles-per-carve interpolation.

Cinematic Cheats used:
- Preserved the compute-driven debris fake: GPU advection, indirect draw args, SDF/flow sampling, and lightweight mirror uploads. No rigidbody debris or GameObject spawning was introduced.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us CPU; larger effect is continuous GPU workload shedding under quality pressure.

Verification:
- Source scan confirms `ResolveDebrisCap` no longer takes low/high tier booleans and the only call now passes `GlobalQualityWeight01`.
- Source scan confirms Delta Crusher native output lanes now carry `[WriteOnly, NoAlias]`.
- Targeted `git diff --check` passed for Delta Crusher job/runtime files.
- Compile was not launched because CPU reported 100%.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <STRUCT name="DebrisParticleDTO" size="32" fields="Position@0 float3, Radius@12 float, Velocity@16 float3, MaterialHash@28 uint" />
    <STRUCT name="DeltaCrusherMockLaserFireSignal" size="64" fields="AupPosition@0 double3, Radius@24 float, DeltaDensity@28 sbyte, ChunkState@29 byte, Reserved0@30 ushort, MaterialHash@32 uint, Frame@36 uint, pad@40..63" />
    <STRUCT name="ChunkCarveDispatchDTO" size="64" fields="ChunkCoord@0 int3, MinCell@12 int3, Span@24 int3, Active@36 byte, pad@37..63" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="CarveDebris positions/velocities/requests/job-state/blackbox existing Vault handles" use="no new buffers allocated" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="existing carve signal AUP conversion path unchanged" />
  <COMPLETE_CALLS status="no .Complete() added" />
  <ALIASING status="producer-only NativeArray lanes now carry WriteOnly NoAlias; read-write debris physics lane remains read-write NoAlias only" />
</SELF_AUDIT>

## Procedural Coral/Wreckage Output Alias Tightening

What was wrong:
- `ProceduralCoralJobs.cs` and `ProceduralWreckageJobs.cs` had many producer-only native lanes annotated with `[NoAlias]` but not `[WriteOnly]`.
- The actual dataflow separates GPU/telemetry/loot/proxy outputs from read-write counters, grids, branch state, and telemetry cursors.

What was done:
- Added `[WriteOnly, NoAlias]` to coral sector trigger, L-system telemetry, spatial cell, render matrix, indirect args, GPU sway, sync pulse, collision proxy, and self-audit output lanes.
- Added `[WriteOnly, NoAlias]` to wreckage sector trigger, collapse node output, collapse telemetry, debris node output, render matrix, indirect args, GPU scalar, loot request, collision proxy, and self-audit output lanes.
- Left read-modify-write counters, grids, branch/node state, debug cells, post-render telemetry patch lanes, and telemetry cursors unrestricted by write-only intent.

Cinematic Cheats used:
- Preserved existing procedural fake path: coral L-system growth and wreckage WFC/debris curl noise emit GPU matrices and indirect draw args instead of simulating physical growth, rigidbody debris, or GameObject instances.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us per generation/render extraction cadence; primary gain is stricter Burst alias proof on GPU/Signal staging lanes.

Verification:
- Source scan confirms the newly patched output-only native lanes carry `[WriteOnly, NoAlias]`.
- Targeted `git diff --check` passed for `ProceduralCoralJobs.cs` and `ProceduralWreckageJobs.cs`.
- Compile was not launched because CPU reported 100% and `dotnet`/`csc` were active.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <LANE name="Coral render/indirect/GPU/proxy/output lanes" access="WriteOnly NoAlias" layout="existing DTO layouts unchanged" />
    <LANE name="Wreckage render/indirect/GPU/loot/proxy/output lanes" access="WriteOnly NoAlias" layout="existing DTO layouts unchanged" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="ProceduralCoral existing owner buffers" use="no new buffers allocated" />
    <BUFFER id="ProceduralWreckage existing owner buffers" use="no new buffers allocated" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="existing double3 sector/camera subtraction paths preserved before float downcast" />
  <COMPLETE_CALLS status="no .Complete() added" />
  <ALIASING status="producer-only NativeArray lanes now carry explicit WriteOnly NoAlias; read-write lanes left unrestricted" />
</SELF_AUDIT>

## 2026-05-21 Continuation Patch - Fauna Interaction Response Layout Purge

What was wrong:
- `FaunaInteractionResponse` carried get-only properties for fauna interaction response scalars and a bool force-retreat flag.

What was done:
- Converted it to `[StructLayout(LayoutKind.Explicit, Size = 32)]`.
- Replaced `ForceRetreat` with raw byte field `ForceRetreatFlag`.
- Added `FaunaInteractionResponse.ShouldForceRetreat(in response)`.
- Updated `FaunaBrain` to use the static predicate.
- Added `BinaryLayoutManifest` assertions for the 32-byte payload.

Cinematic Cheats used:
- Fauna reaction remains a compact authored response row; no per-interaction behavior object or physics simulation was introduced.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate only: below 1 us per interaction.

Verification:
- Stale `response.ForceRetreat` scan returned no hits in the touched fauna files.
- `git diff --check` passed for the fauna response files.
- Unity compile was not launched because the CPU-under-50 guard still lacks a valid sample.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="FaunaInteractionResponse" size="32">
      <FIELD name="DamageMultiplier" offset="0" size="4" />
      <FIELD name="RetreatDurationSeconds" offset="4" size="4" />
      <FIELD name="FearImpulse01" offset="8" size="4" />
      <FIELD name="InteractionKind" offset="12" size="1" />
      <FIELD name="ForceRetreatFlag" offset="13" size="1" />
      <PAD bytes="14..31" size="18" />
    </DTO>
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-new" use="authoring response row; no DataVault allocation added" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="no spatial math changed" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="raw readonly fields plus static in predicate; no response accessor properties remain" />
</SELF_AUDIT>

## 2026-05-21 Continuation Patch - Physics Impact Signal Layout Purge

What was wrong:
- `PhysicsImpactSignal` was a high-frequency deferred impact payload with get-only C# properties and no explicit byte layout.
- Downstream impact listeners used `impactSignal.IsHeavy`, preserving an accessor on the fan-out path.

What was done:
- Converted `PhysicsImpactSignal` to `[StructLayout(LayoutKind.Explicit, Size = 128)]` with readonly raw fields.
- Replaced the heavy predicate with `PhysicsImpactSignal.IsHeavy(in signal)`.
- Updated impact audio/acoustic consumers in `PlayerCriticalProceduralAudioRenderer`, `SpatialAudioManager`, and `AcousticZoneController`.
- Added `BinaryLayoutManifest` checks for the 128-byte signal layout.

Cinematic Cheats used:
- The route remains a deferred scalar impact signal for audio/VFX/camera response. No per-contact persistent physics simulation or object graph was introduced.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate only: below 1 us per impact burst; main value is cache-aligned layout and removal of accessor calls in listener fan-out.

Verification:
- Stale `impactSignal.IsHeavy`, `impactSignal.HasPointAup`, and `impactSignal.PointAup` scans returned no hits.
- `git diff --check` passed for the touched impact signal files.
- Unity compile was not launched; no dotnet/csc process was active, but the CPU guard still lacks a valid under-50 sample.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="PhysicsImpactSignal" size="128">
      <FIELD name="PrimaryBodyId" offset="0" size="8" />
      <FIELD name="SecondaryBodyId" offset="8" size="8" />
      <FIELD name="_pointAup" offset="16" size="48" />
      <FIELD name="Point" offset="64" size="12" />
      <FIELD name="Normal" offset="76" size="12" />
      <FIELD name="Force" offset="88" size="4" />
      <FIELD name="Intensity" offset="92" size="4" />
      <FIELD name="MassVelocity" offset="96" size="4" />
      <FIELD name="WeightClass" offset="100" size="1" />
      <FIELD name="PrimaryAudioMaterialId" offset="101" size="1" />
      <FIELD name="SecondaryAudioMaterialId" offset="102" size="1" />
      <FIELD name="_hasPointAup" offset="103" size="1" />
      <PAD bytes="104..127" size="24" />
    </DTO>
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-new" use="impact route unchanged; no DataVault allocation added" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="payload preserves 48-byte AUP at offset 16; legacy runtime point fallback still resolves through floating-origin double offset" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="raw readonly fields plus static in predicates; no accessor properties remain on PhysicsImpactSignal" />
</SELF_AUDIT>

## 2026-05-21 Continuation Patch - Persistent World DTO Property Purge

What was wrong:
- `PersistentWorldItemRecord` used property accessors for `Quantity`, `Flags`, and flag predicates while the records are stored in `NativeList<PersistentWorldItemRecord>`.
- `PersistentWorldDeltaRecord` used boolean/validity accessors in persistence and save-section loops.
- Direct `_records[i].IsCollected` / `_records[i].IsDeleted` style reads were a CS1612/struct-copy hazard and became a compile wall once properties were removed.

What was done:
- `PersistentWorldItemRecord` now has raw fields: `Position@0`, `ItemPersistentIdHash@48`, `ItemPersistentId@56`, `ChunkId@184`, `Quantity@196`, `InstanceUid@200`, `Flags@204`, explicit pad bytes `205..207`, and tail padding through byte `255`.
- Boolean checks moved to static `in` helper predicates on `PersistentWorldItemRecord`.
- `PersistentWorldDeltaRecord` boolean and validity checks moved to static `in` helper predicates without changing its 64-byte save ABI.
- Stale call sites were converted in `PersistentWorldRegistry`, `PlayerExplorationTracker`, `FloraRegrowthDirector`, and `SaveBinaryStorage`.
- `BinaryLayoutManifest` now asserts the live 256-byte `PersistentWorldItemRecord` layout and the corrected field offsets.

Cinematic Cheats used:
- No new simulation was introduced. Persistence remains sector/delta based; visual hydration and regrowth routes consume compact state instead of simulating offscreen world objects.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate only: below 1 us per persistence scan; main gain is removing DTO accessor calls and direct `NativeList` indexer property copy risk.

Verification:
- Stale-property scan for `_packedQuantityAndFlags`, `QuantityMask`, `FlagsShift`, `record.Is*`, `deltaRecord.Is*`, `expandedRecord.Is*`, `seedRecord.IsFloraSeedPending`, and `_records[i].Is*` returned no hits in the touched persistence files.
- `git diff --check` passed for the persistence DTO purge files.
- Unity compile was not launched: no dotnet/csc process was active, but CPU probes timed out/unavailable and `wmic` is not installed, so the AGENTS.md CPU-under-50 guard could not be proven.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="PersistentWorldItemRecord" size="256">
      <FIELD name="Position" offset="0" size="48" />
      <FIELD name="ItemPersistentIdHash" offset="48" size="8" />
      <FIELD name="ItemPersistentId" offset="56" size="128" />
      <FIELD name="ChunkId" offset="184" size="12" />
      <FIELD name="Quantity" offset="196" size="4" />
      <FIELD name="InstanceUid" offset="200" size="4" />
      <FIELD name="Flags" offset="204" size="1" />
      <PAD bytes="205..207" size="3" />
      <PAD bytes="208..255" size="48" />
    </DTO>
    <DTO name="PersistentWorldDeltaRecord" size="64" fields="layout unchanged; properties removed only" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-new" use="PersistentWorldRegistry still owns existing local native lists; Task 01 route-card work remains pending" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="persistence AUP field layout unchanged" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="direct DTO field reads plus static in predicates; no property-based NativeList element reads remain in touched files" />
</SELF_AUDIT>

## 2026-05-21 Continuation Patch - Fabrication ReadOnly Pointer Tightening

What was wrong:
- `EmitFabricationSignalsJob` had `[ReadOnly, NoAlias]` on `Jobs`, but the loop still read that lane through `GetUnsafeBufferPointerWithoutChecks`, exposing a mutable pointer.

What was done:
- Hoisted `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(Jobs)` once before the loop.
- Read each `FabricationJobDTO` as `ref readonly`.
- Kept `FabricationRuntimeDTO` on a separate mutable pointer because the job legitimately marks completion observed and dirty flags.

Cinematic Cheats used:
- Fabrication continues to emit scalar progress and GPU payloads instead of spawning per-piece assembly objects or managed callbacks.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate only: below 1 us steady-state; removes repeated pointer lookup and hardens Burst read-only alias proof.

Verification:
- `git diff --check` passed for `FabricationAssemblerRuntime.cs`.
- Unity compile not launched in this step; CPU/dotnet guard still applies.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="FabricationJobDTO" size="32" fields="TargetAUP@0 double3; Progress01@24 float; TargetPrefabHash@28 uint" />
    <DTO name="FabricationRuntimeDTO" size="96" fields="unchanged; mutable state lane remains separate from read-only job lane" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-new" use="no new Vault route; existing fabrication buffers unchanged" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="existing TargetAUP finite guard preserved" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="Jobs read through read-only pointer and ref readonly; Runtime remains mutable NoAlias lane" />
</SELF_AUDIT>

## 2026-05-21 Continuation Patch - Scatter AUP, Hull Bridge Audit, Chronicler Facade

What was wrong:
- `GPUScatterDirector` added a float-cast origin offset to local scatter cells in the compute shader, preserving absolute-float jitter risk at large AUP offsets.
- Scatter quality still used tier-like route decisions for cull distance, pixel threshold, and budget.
- Task 16 status was stale: the hull deformation GPU bridge already exists and needed evidence, not a duplicate state path.
- Task 18 lacked one master UI Toolkit facade over the existing telemetry rings.

What was done:
- Reworked scatter AUP hashing so the CPU stores the origin as `double2`, snaps field origins in double space, computes a stable cell-base index, and sends that index to the compute shader.
- Replaced scatter tier decisions with `GlobalQualityWeight01` smoothstep/lerp curves for cull distance, projected pixel radius, and instance budget.
- Audited `HullIntegrityRuntime`, `HullIntegrityTypes`, and `Hecton8_UberNoir.hlsl`: the existing `DeformationStateDTO` route is Vault-backed, 64-byte validated, double-buffer uploaded, and consumed by the vertex shader in local space.
- Added `ChroniclerDiagnosticHeatmapWindow`, an editor-only UI Toolkit facade over GlobalDataVault telemetry, SignalBus lane telemetry, signal frame rings, dispatcher phase/fence telemetry, SignalThreadLocalScratchpad contention, and GlobalTelemetryBus blackbox data.

Cinematic Cheats used:
- GPU scatter remains a shader/compute fake; no CPU scatter simulation or GameObject path was added.
- Hull dents remain a structured-buffer vertex displacement fake; no CPU mesh mutation or collider deformation path was added.
- Chronicler reads existing rings and fixed strips; no new runtime diagnostic owner was introduced.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimates only: scatter AUP repair is correctness/visual stability; continuous scatter quality can reduce GPU candidate pressure at low quality but requires scene profiling; Chronicler is editor-only and costs 0 us at runtime.

Verification:
- `git diff --check` passed for `GPUScatterDirector.cs`, `Hecton_GpuScatter.compute`, and `ChroniclerDiagnosticHeatmapWindow.cs`.
- Compile was not launched: CPU counter reported 83.39%, above the AGENTS.md 50% build guard. No `dotnet` or `csc` process was listed during that check.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="ScatterTelemetryEntry" size="64" fields="Frame@0 uint; Flags@4 uint; Center@8 float3; AupOffsetXZ@20 float2; RadiusMeters@28 float; CellSizeMeters@32 float; GridResolution@36 int; CandidateCount@40 int; BiomeHash@44 uint; VisibleCount@48 uint; StateHash@52 uint; OriginShiftSequence@56 uint; BlobChecksumLo@60 uint" />
    <DTO name="DeformationStateDTO" size="64" fields="LocalPosition@0 float3; Radius@12 float; Normal@16 float3; Depth@28 float; Age@32 float; Severity@36 float; DamageTypeHash@40 uint; SourceHash@44 uint; Frame@48 uint; Flags@52 uint; Reserved0@56 uint; Reserved1@60 uint" />
    <DTO name="SignalLaneTelemetry" size="32" fields="LaneHash@0 uint; QueuedBeforeFlush@4 int; SnapshotCount@8 int; DroppedCount@12 int; CoalescedCount@16 int; Flags@20 byte; Reserved0@21 byte; Reserved1@22 ushort; Reserved2@24 ulong" />
    <DTO name="SignalTelemetryFrame" size="64" fields="Frame@0 uint; TotalPushedSignals@4 uint; PeakSignalsPerFrame@8 uint; CoalescedSignals@12 uint; DroppedSignals@16 uint; CorruptedSignals@20 uint; ActiveLaneCount@24 uint; Flags@28 uint; GlobalQualityMilli@32 uint; SystemStressMilli@36 uint; Reserved0@40 ulong; Reserved1@48 ulong; Reserved2@56 ulong" />
    <DTO name="SignalThreadContentionTelemetryEntry" size="64" fields="Frame@0 uint; Flags@4 uint; WrittenSignals@8 uint; CoalescedSignals@12 uint; DroppedSignals@16 uint; OverflowSignals@20 uint; NonFiniteSignals@24 uint; ThreadCount@28 uint; ActiveStrideBytes@32 uint; PeakThreadWriteBytes@36 uint; GlobalQualityMilli@40 uint; VramPressureMilli@44 uint; BufferIndex@48 uint; BatchId@52 uint; CommitMicroseconds@56 uint; LastAupHashLow@60 uint" />
    <DTO name="DispatcherFenceTelemetryEntry" size="64" fields="FrameId@0 uint; ScheduledJobCount@4 uint; SafetyBypassCount@8 uint; DomainMask@12 uint; SimulationWaitMs@16 float; FixedWaitMs@20 float; AupHardFenceMs@24 float; GlobalQualityWeight@28 float; MasterSimulationHandleBits@32 ulong; PhysicsHandleBits@40 ulong; AudioHandleBits@48 ulong; NetcodeHandleBits@56 ulong" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="70090" use="Hull DeformationStates DeformationStateDTO ring" />
    <BUFFER id="70091" use="Hull impact scratch" />
    <BUFFER id="70092" use="Hull deformation telemetry" />
    <BUFFER id="70093" use="Hull deformation telemetry cursor" />
    <BUFFER id="70094" use="Breach jets buffer" />
    <BUFFER id="70095" use="Breach jet args" />
    <BUFFER id="70096" use="Hull material strength table" />
    <BUFFER id="70097" use="Hull material CSV scratch" />
    <BUFFER id="70098" use="External pressure scalar" />
    <BUFFER id="70099" use="Pending visual impacts" />
    <BUFFER id="73038" use="SignalTelemetryRingBuffer 300-frame ring read by Chronicler" />
    <BUFFER id="73039" use="SignalTelemetryRingBuffer cursor read by Chronicler" />
    <BUFFER id="73049" use="SignalThreadLocalScratchpad contention telemetry read by Chronicler" />
    <BUFFER id="73050" use="SignalThreadLocalScratchpad contention cursor read by Chronicler" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added to runtime hot paths; Chronicler allocations are editor-only cold buffers and fixed UI elements" />
  <AUP status="scatter stable cell base now computed from double-space origin offset before float shader dispatch; no absolute world float addition remains in scatter hash path" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <QUALITY status="scatter cull radius, projected pixel threshold, and instance budget consume continuous GlobalQualityWeight01 curves" />
  <DEPENDENCY_GRAPH status="Chronicler reads Core contracts only; no sibling runtime domain dependency introduced" />
</SELF_AUDIT>

## 2026-05-21 Continuation Patch - AUP, Signal Padding, Jacobi Gate

What was wrong:
- `VoxelDynamicNavGridRuntime.QueueLocalizedSdfPatch()` and `HectonWorldGenerator.ChunkOrigin()` converted absolute coordinates to float before origin subtraction.
- `GlobalSignals.SpscSignalRingBuffer<T>` placed producer and consumer cursors in the same cache line.
- Active logistics Jacobi loops damped residual growth but did not enforce the required three-growth fast-fail clamp.

What was done:
- Rewrote voxel dynamic nav patch center and world chunk origin conversion to construct absolute `double3` first and call `HectonFloatingOrigin.ToRuntimePosition(double3)`.
- Replaced SPSC head/tail `int` fields with 64-byte explicit-layout padded index structs.
- Updated cache-line-critical signal stride debt to accept exact 64-byte multiples up to 192 bytes.
- Added three-successive-residual-growth gates to active logistics Jacobi loops; on trip, the previous stable potential buffer is copied forward and divergent flags are marked.

Cinematic Cheats used:
- Jacobi fast-fail preserves the last believable stable electrical state instead of spending more iterations trying to simulate oscillation.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimates only: SPSC padding 1-3 us under high contention; Jacobi gate avoids remaining iteration work only during oscillation; AUP repairs are correctness fixes, not speed claims.

Verification:
- `git diff --check` passed for continuation files.
- Compile was not launched: CPU counter reported 61.28% and seven `dotnet` processes were active. `rg` found no `.sln` or `.csproj` under the project root.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="SpscSignalRingBuffer.PaddedSignalIndex" size="64" fields="Value@0 int; pad@8..56 ulong[7]" />
    <DTO name="Existing TetherTensionSignal" size="192" fields="unchanged; accepted because stride is exact 64-byte multiple" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-new" use="No new Vault buffer introduced; patches operate on existing owner buffers and legacy bridge structs" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added to patched hot paths" />
  <AUP status="voxel localized SDF patch and world chunk origin now subtract origin in double before float downcast" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <QUALITY status="Jacobi iteration budget still comes from continuous quality resolver; residual gate does not alter DTO layout or authority route" />
</SELF_AUDIT>

## 2026-05-21 Continuation Patch - Local Queue Eviction And Producer Guards

What was wrong:
- `TerrainChunkGeneratedEvents` owned a private persistent `NativeQueue<TerrainChunkGeneratedSignal>` instead of using the central signal authority.
- Audio emergency acoustic fallback wrote three hash-map rows without checking preallocated capacity.
- Auxiliary Burst producers wrote directly to legacy MPSC writers without local finite guards.

What was done:
- Converted `TerrainChunkGeneratedSignal` to `ISignal` while keeping its explicit 64-byte layout.
- Replaced the terrain chunk private queue with `SignalBus<TerrainChunkGeneratedSignal>` configured for 32 max frame signals and 4 survival signals.
- Added bounded `TryAdd` writes to `GenerateEmergencyMockAcoustics(NativeParallelHashMap<uint, AcousticMaterialCoefficientDTO>)`.
- Added write-only/noalias metadata and finite pre-enqueue guards for auxiliary flare, sonar, and tether signal writers.

Cinematic Cheats used:
- Terrain event pressure is handled by a small fixed signal lane and survival frame limit instead of trying to process every terrain notification under stress.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimates only: local queue eviction has no hot-path us claim; auxiliary finite shield is below 1 us steady-state but avoids corrupted-signal recovery; fallback capacity guard is cold-path correctness.

Verification:
- `git diff --check` passed.
- Compile was not launched: CPU was 18.67%, but seven `dotnet` processes were active and AGENTS.md forbids starting another build while dotnet/csc is running.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="TerrainChunkGeneratedSignal" size="64" fields="ChunkX@0 int; ChunkZ@4 int; TerrainEntityHash@8 uint; HeightmapResolution@12 int; CacheRevision@16 int; TerrainPosition@20 float3; TerrainSize@32 float3; Frame@44 uint; Flags@48 byte; Reserved0@49 byte; Reserved1@50 ushort; Reserved2@52 uint; pad@56 ulong" />
    <DTO name="AuxiliaryFlareLightSignal" size="64" fields="AUP_Position@0 double3; Intensity@24 float; RangeMeters@28 float; SourceHash@32 uint; Frame@36 uint; ColorRgb@40 float3; QualityWeight@52 float; Flags@56 uint; Reserved0@60 uint" />
    <DTO name="AuxiliarySonarRequestSignal" size="64" fields="AUP_Position@0 double3; CurrentRadius@24 float; Intensity@28 float; SourceHash@32 uint; Frame@36 uint; ExpansionRate@40 float; MaxRadius@44 float; Flags@48 uint; Reserved0@52 uint; Reserved1@56 ulong" />
    <DTO name="AuxiliaryTetherConnectionSignal" size="64" fields="ProjectileAup@0 double3; AnchorAup@24 double3; RestLength@48 float; SourceHash@52 uint; Frame@56 uint; Flags@60 uint" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-new" use="Terrain chunk events now use SignalBus lane hash 0x54434753; no new Vault buffer introduced" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added to Burst jobs or runtime signal publish/dequeue paths" />
  <AUP status="modified auxiliary signal paths preserve double3 AUP payloads; no absolute float spatial calculation added" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <QUALITY status="SignalBus terrain lane uses continuous registry stress/quality frame limiting; no binary hardware branch added" />
</SELF_AUDIT>

## 2026-05-21 Continuation Patch - Symbiosis Emergency Mock Bulk Clear

What was wrong:
- `GenerateEmergencyMockSymbiosisJob` used scalar loops to clear inactive native slots and did not mark proven output-only arrays as write-only for Burst alias analysis.
- Negative fallback request counts could publish negative active counts into tuning/boid DTOs even though no rows were written.
- The center AUP input was trusted without a finite-local check before deriving mock flora/fish AUPs.

What was done:
- Clamped flora, fish, and link limits to non-negative native capacities before any DTO write.
- Sanitized non-finite center AUP to default before offset generation.
- Added `[WriteOnly, NoAlias]` to output-only fallback arrays while leaving `FloraAups` read/write because active AUPs are read back for fish anchors.
- Replaced scalar tail clears with `UnsafeUtility.MemClear` over native array ranges.
- Fixed the Chronicler facade assembly-visibility risk by replacing an internal scratchpad scale constant with a local fixed editor scale.

Cinematic Cheats used:
- Emergency ecosystem data remains a small deterministic mock profile, not a simulated ecology bootstrap. The fallback seeds a believable interaction scaffold and lets later runtime jobs own the real behavior.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimates only: no hot-path savings; cold fallback tail clearing moves from per-element stores to bulk memory clear. Expected gain depends on Vault capacity and memory bandwidth.

Verification:
- `git diff --check` passed for `ChroniclerDiagnosticHeatmapWindow.cs` and `ShinobuFloraFaunaSymbiosisSolver.cs`.
- Compile was not launched: CPU counter reported 77.79%; no dotnet/csc process was active, but AGENTS.md forbids starting a build over 50% CPU.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="SymbiosisAup48" size="48" fields="GridX@0 long; GridY@8 long; GridZ@16 long; LocalX@24 float; LocalY@28 float; LocalZ@32 float; pad@36 uint; pad@40 ulong" />
    <DTO name="SymbiosisFloraDTO" size="48" fields="LocalPosition@0 float3; Biomass@12 float; FloraHash@16 uint; ChemicalMask@20 uint; OxygenRate@24 float; ToxicPotency@28 float; CamouflageRadius@32 float; FeedingRadius@36 float; Flags@40 uint; pad@44 uint" />
    <DTO name="SymbiosisFloraAupDTO" size="64" fields="PositionAup@0 SymbiosisAup48; FloraHash@48 uint; SectorHash@52 uint; SpatialCellHash@56 int; StableSeed@60 uint" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="ShinobuSymbiosisFlora" use="bulk-cleared fallback Flora tail" />
    <BUFFER id="ShinobuSymbiosisFloraAups" use="bulk-cleared fallback AUP tail and active readback for fish anchors" />
    <BUFFER id="ShinobuSymbiosisLinks" use="bulk-cleared fallback links" />
    <BUFFER id="ShinobuSymbiosisTuning" use="bulk-cleared then fallback row 0" />
    <BUFFER id="ShinobuSymbiosisCounters" use="bulk-cleared then fallback row 0" />
    <BUFFER id="ShinobuSymbiosisMockBoids" use="bulk-cleared then fallback row 0" />
    <BUFFER id="ShinobuSymbiosisMockFish" use="bulk-cleared fallback fish tail" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added; fallback writes stay inside Vault-owned NativeArrays" />
  <AUP status="mock generation uses finite center AUP and OffsetAup double reconstruction before compact AUP storage" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="[WriteOnly, NoAlias] applied only to output-only buffers; mixed read/write FloraAups left unmarked WriteOnly" />
</SELF_AUDIT>

## 2026-05-21 Continuation Patch - Fabrication Signal Producer Shield

What was wrong:
- `EmitFabricationSignalsJob` used producer-only `NativeQueue<T>.ParallelWriter` fields with safety suppression but no `[WriteOnly]` metadata.
- Fabrication completion, tick, and deconstruct signal emission did not locally reject non-finite target AUP payloads before enqueue.

What was done:
- Marked fabrication job input as `[ReadOnly, NoAlias]`.
- Marked completed/tick/deconstruct producer writers as `[WriteOnly, NoAlias, NativeDisableContainerSafetyRestriction]`.
- Added finite `double3` target AUP guards before all fabrication/deconstruction signal enqueues.

Cinematic Cheats used:
- Fabrication progress remains a scalar GPU payload and sparse signal emission path; no per-piece simulated assembly objects or managed callbacks were introduced.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimates only: below 1 us steady-state; the value is reduced queue pollution and improved Burst alias metadata.

Verification:
- `git diff --check` passed for `FabricationAssemblerRuntime.cs`.
- Compile was not launched: CPU counter reported 100%; no dotnet/csc process was active, but AGENTS.md forbids starting a build over 50% CPU.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="FabricationJobDTO" size="32" fields="TargetAUP@0 double3; Progress01@24 float; TargetPrefabHash@28 uint" />
    <DTO name="FabricationRuntimeDTO" size="96" fields="FabricatorAUP@0 double3; active fields unchanged; explicit layout already present" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="existing-fabrication-vault-handles" use="no new Vault allocation; patch only changes writer metadata and finite gates" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added; signal writes remain native queue producers" />
  <AUP status="fabrication signals now reject non-finite TargetAUP double3 before enqueue" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="Jobs marked ReadOnly/NoAlias; producer writers marked WriteOnly/NoAlias" />
</SELF_AUDIT>

## 2026-05-21 Continuation Patch - Gas Dynamics Producer Metadata

What was wrong:
- `GasDynamicsStepJob` writes toxicity signals and one telemetry entry through output-only lanes, but the writer and telemetry buffer were not marked write-only.

What was done:
- Marked the toxicity queue writer `[WriteOnly, NoAlias, NativeDisableContainerSafetyRestriction]`.
- Marked the gas telemetry ring `[WriteOnly, NoAlias]`.

Cinematic Cheats used:
- Existing room/base hibernation remains the Dear Lie: sleeping rooms preserve pressure state instead of simulating continuous gas diffusion every tick.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimates only: below 1 us steady-state; patch improves alias metadata and queue hygiene, not algorithmic cost.

Verification:
- `git diff --check` passed for `GasDynamicsSolver.cs`.
- Compile was not launched: CPU counter reported 100%; no dotnet/csc process was active, but AGENTS.md forbids starting a build over 50% CPU.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="GasDynamicsTelemetryEntry" size="32" fields="existing explicit telemetry entry; write-only ring path unchanged" />
    <DTO name="ToxicitySignal" size="existing" fields="unchanged; queue writer metadata only" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-new" use="no Vault route created; GasDynamics private allocation eviction remains pending route-card work" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="no spatial AUP math changed" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="output-only queue writer and telemetry ring now carry WriteOnly/NoAlias metadata" />
</SELF_AUDIT>

## 2026-05-21 Continuation Patch - Player Noise And Fauna Cognition DTO Purge

What was wrong:
- `NoiseSystem.PlayerNoiseSignal` was a runtime signal payload with get-only properties and managed bool lanes.
- `CreatureUtilityContext` and `CreatureUtilityEvaluation` were active fauna cognition value payloads with property-backed boolean state.
- `BinaryLayoutManifest` asserted new signal/DTO layouts only after those payloads were made `[BinaryBlittableSafe]`.

What was done:
- Converted `PlayerNoiseSignal` to an explicit 96-byte row: `PositionAup@0`, `Position@48`, scalar lanes `60..87`, `ReportedFrame@88`, `FlashlightOnFlag@92`, `IsActiveSonarPingFlag@93`, pad@94.
- Converted `CreatureUtilityContext` to an explicit 256-byte row with Vector3 lanes `0..167`, scalar lanes `168..228`, `Flags@232`, and tail padding.
- Converted `CreatureUtilityEvaluation` to an explicit 80-byte row with direction/look vectors `0..23`, scalar lanes `24..52`, `LegacyState@56`, `Flags@60`, `StateMask@62`, and tail padding.
- Replaced all changed boolean property reads with static `in` predicate helpers.

Cinematic Cheats used:
- The acoustic path remains a scalar awareness snapshot and spatial transient, not a physical acoustic wave simulation.
- Fauna cognition keeps scalar utility/flags and foveated cadence; no per-agent heavy behavior-tree heap state was introduced.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimates only: below 1 us per signal dispatch and below 1 us per active fauna cognition tick; main value is explicit ARM64-safe layout and accessor-copy removal.

Verification:
- Targeted stale-property scan found no remaining `.FlashlightOn`, `.IsActiveSonarPing`, `context.Has*`, or `evaluation.EmitThreatPulse` property reads for the modified payloads.
- `git diff --check` passed for `NoiseSystem.cs`, `FaunaBrain.Compatibility.cs`, `FaunaBrain.cs`, `FaunaSensorSuite.cs`, `VegetationFlowFieldIntegrator.cs`, `FaunaDataTemplate.cs`, `GlobalPhysicsStateManager.cs`, and `BinaryLayoutManifest.cs`.
- Compile was not launched under command discipline; no dotnet/csc process was active, but no CPU-under-50 proof was established in this patch window.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="NoiseSystem.PlayerNoiseSignal" size="96" fields="PositionAup@0 size48; Position@48 size12; MovementSpeedSqr@60 size4; TransportBoost01@64 size4; TransportSignature@68 size4; ToolUseNoise01@72 size4; AcousticTransmission01@76 size4; AcousticLowPassCutoffHz@80 size4; SignalRadiusMeters@84 size4; ReportedFrame@88 size4; FlashlightOnFlag@92 size1; IsActiveSonarPingFlag@93 size1; pad@94 size2" />
    <DTO name="CreatureUtilityContext" size="256" fields="14xVector3@0..167 size168; scalar floats@168..224 size60; FlockCount@228 size4; Flags@232 size2; pad@234 size22" />
    <DTO name="CreatureUtilityEvaluation" size="80" fields="DesiredDirection@0 size12; AcousticHeadLookTarget@12 size12; scores/scalars@24..48 size28; PackRoleCode@52 size4; LegacyState@56 size4; Flags@60 size2; StateMask@62 size1; pad@63 size17" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-new" use="DTO property purge only; no persistent allocation ownership changed" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added; property reads replaced by static in predicates" />
  <AUP status="PlayerNoiseSignal stores compact AUP at offset 0 and no absolute float cast was introduced" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="no job aliases changed in this patch; layout manifest now asserts modified DTO byte offsets" />
</SELF_AUDIT>

## 2026-05-21 Continuation Patch - Survival Death Record Layout Purge

What was wrong:
- `SurvivalDeathRecord` was a persisted telemetry record with get-only properties and no explicit byte contract.
- Death UX, profile events, and save hydration pass the record by value.

What was done:
- Converted `SurvivalDeathRecord` to `[BinaryBlittableSafe] [StructLayout(LayoutKind.Explicit, Size = 64)]`.
- Ordered doubles first, then runtime position/scalar lows, then cause byte and full manual tail padding.
- Added `BinaryLayoutManifest` assertions under the gameplay layout namespace.

Cinematic Cheats used:
- None added; this is cold telemetry hygiene.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: 0 us hot path; removes property/layout ambiguity on cold death/profile/UI paths.

Verification:
- Targeted property scan found no surviving `SurvivalDeathRecord` accessor properties.
- `git diff --check` passed for `HectonSurvivalSystem.cs` and `BinaryLayoutManifest.cs`.
- Compile was not launched under command discipline.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="SurvivalDeathRecord" size="64" fields="LifeDurationSeconds@0 size8; PeakDepthMeters@8 size8; Position@16 size12; LowestOxygenNormalized@28 size4; LowestEnergyNormalized@32 size4; LowestIntegrityNormalized@36 size4; Cause@40 size1; pad@41 size23" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-new" use="persisted telemetry row only; no persistent native allocation changed" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="record stores runtime death position only; no new absolute-float world math introduced" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="no job aliasing changed" />
</SELF_AUDIT>

## 2026-05-21 Continuation Patch - GasDynamics BaseAwake Vault Fallback Eviction

What was wrong:
- `GasDynamicsSolver.ResolveBaseAwakeStateBuffer` requested `BufferID.HabitatBaseAwakeState` from the Vault, but allocated a private persistent fallback array if the Vault was unavailable.
- That fallback created a second possible owner for base-awake state.

What was done:
- Resolved the Vault base-awake buffer before allocating local gas SOA lanes.
- Removed the fallback `new NativeArray<byte>(safeBaseCapacity, Allocator.Persistent, ...)`.
- Made `EnsureNativeState` fail closed without allocating the rest of the gas arrays when `HabitatBaseAwakeState` cannot be resolved.

Cinematic Cheats used:
- Existing gas hibernation remains the Dear Lie: sleeping bases preserve scalar state instead of simulating all rooms every tick.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: 0 us hot path; one cold fallback persistent allocation route removed.

Verification:
- Search confirmed the `safeBaseCapacity` fallback allocation is gone.
- `git diff --check` passed for `GasDynamicsSolver.cs`.
- Compile was not launched under command discipline.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="BaseAwakeState" size="byte lane" fields="Vault BufferID.HabitatBaseAwakeState; no local fallback layout" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="BufferID.HabitatBaseAwakeState" use="base awake truth; required for GasDynamicsSolver initialization" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="no spatial math changed" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="no job aliasing changed" />
</SELF_AUDIT>

## 2026-05-21 Continuation Patch - BIOS Scanner AUP And PersistentWorld Route Proof

What was wrong:
- `HectonScanRenderRegistry` handed a BIOS loot highlight center to the shader as an absolute float vector.
- `Hidden_Hecton_BiosDiagnostic.shader` added `_TotalUniverseOffset` to depth-derived runtime world position and compared two large float positions.
- `PersistentWorldRegistry` still owns many private persistent containers, but no safe PersistentWorld Vault authority route exists in `BufferID`.

What was done:
- Scanner loot sphere output now subtracts `GlobalSignals.CurrentRuntimeOriginAup()` from the cached loot AUP in double precision before downcasting to `float3`.
- BIOS shader now compares runtime `worldPos` directly against the runtime-local sphere center and no longer reads `_TotalUniverseOffset`.
- Integrated read-only subagent evidence: full PersistentWorld eviction is blocked until approved PersistentWorld-owned BufferIDs and route cards exist. AI and save-compression buffers were explicitly rejected as wrong owners.

Cinematic Cheats used:
- The BIOS highlight remains a screen-space depth-buffer fake. No physics overlap, raycast fan, or mesh query was introduced.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: 0 us CPU claim; shader removes one vector add and eliminates large-coordinate shimmer risk.

Verification:
- Targeted search found no remaining `absoluteWorld`, `_TotalUniverseOffset`, or `DowncastLocalDelta(centerAup.ToAbsoluteDouble3())` use in the scanner/shader pair.
- `git diff --check` passed for `HectonScanRenderRegistry.cs`, `Hidden_Hecton_BiosDiagnostic.shader`, status, and rationale files.
- Compile was not launched because CPU load reported 100%; no dotnet/csc process was active.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="BIOSLootSphereShaderPayload" size="16" fields="runtimeLocalCenter.xyz@0 size12; radius@12 size4" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-new" use="scanner shader payload is transient material constant; no persistent ownership changed" />
    <BUFFER id="PersistentWorldRegistry" use="blocked: no approved PersistentWorld-owned BufferID route exists for live records/indexes/queues" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added; shader fake preserved" />
  <AUP status="centerAup - runtimeOriginAup in double before float downcast; shader uses runtime-local comparison only" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="no job aliasing changed in this patch" />
</SELF_AUDIT>

## 2026-05-21 Continuation Patch - Seismic Fault Fallback Native Clear

What was wrong:
- Seismic static-data and emergency fallback loaders cleared event slots with scalar loops.
- Legacy binary load could leave stale slots if invalid or short records were skipped after previous data was present.

What was done:
- Added `ClearSeismicEvents(NativeArray<SeismicEventDTO>)` using `NativeArrayUnsafeUtility.GetUnsafePtr` and `UnsafeUtility.MemClear`.
- Static-data load, legacy binary load, and `GenerateEmergencyMockFaults` now bulk-clear the Vault event array before writing sanitized records.
- The legacy 40-byte fault file ABI was left unchanged.

Cinematic Cheats used:
- Seismic fallback remains a small authored fault table. No continuous tectonic simulation or per-vertex ocean force field was added.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: 0 us hot path; cold clear changes from scalar stores to one native bulk clear.

Verification:
- Targeted search shows `ClearSeismicEvents` at all three loader/fallback entry points and `UnsafeUtility.MemClear` in the helper.
- `git diff --check` passed for `HectonSeismicTideDirector.cs`.
- Compile was not launched because CPU load reported 100%; no dotnet/csc process was active.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="SeismicEventDTO" size="40" fields="EpicenterAUP@0 size24; Magnitude@24 size4; Frequency@28 size4; DecayRate@32 size4; EventTypeHash@36 size4; legacy ABI retained" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="SeismicDirectorConstants.EventSlotsBuffer" use="Vault-owned seismic event slots cleared in bulk before fallback/static records are installed" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added; fallback clear uses native MemClear" />
  <AUP status="fallback epicenters remain finite double3 AUP; no absolute float cast added" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="no job aliasing changed in this patch" />
</SELF_AUDIT>

## 2026-05-21 Continuation Patch - Seismic Evaluation Alias Metadata

What was wrong:
- `SeismicEvaluationJob` output pointers and the shockwave `NativeQueue<T>.ParallelWriter` were write-only by design but not declared as write-only to Burst.

What was done:
- Marked `Shake`, `TurbiditySpike`, `Telemetry`, and `MockSilt` pointers with `[WriteOnly, NoAlias, NativeDisableUnsafePtrRestriction]`.
- Marked `ShockwaveWriter` with `[WriteOnly, NoAlias, NativeDisableContainerSafetyRestriction]`.
- Left `Events` as read/write `[NoAlias]` because the job reads quake state and decays magnitude in place.

Cinematic Cheats used:
- Seismic shake remains a bounded scalar/sine/noise approximation, not rigid-body propagation through terrain or water.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us; primarily alias proof and queue output hygiene.

Verification:
- Source read-back confirmed the output pointer and queue writer attributes.
- `git diff --check` passed for `HectonSeismicTideDirector.cs`.
- Compile was not launched because CPU load reported 100%; no dotnet/csc process was active.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="SeismicShockwaveSignal" size="64" fields="unchanged existing signal payload; writer metadata only" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="SeismicDirectorConstants.EventSlotsBuffer" use="read/write event state lane intentionally not WriteOnly" />
    <BUFFER id="SeismicDirector shake/turbidity/telemetry/mock-silt handles" use="write-only output lanes now annotated for Burst alias analysis" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="job already subtracts CameraAUP and event EpicenterAUP in double before float cast" />
  <COMPLETE_CALLS status="no new .Complete() added; dispatcher fence completion path unchanged" />
  <ALIASING status="output pointers and MPSC queue writer now WriteOnly/NoAlias; event pointer remains read/write NoAlias" />
</SELF_AUDIT>

## 2026-05-21 Continuation Patch - Cultivation Slot ARM64 Layout

What was wrong:
- `CultivationSlotState` is a `NativeArray` row, but the 8-byte `GeneticsMask` lane was placed after a 4-byte item hash and manual padding.
- The layout had no `BinaryLayoutManifest` proof.

What was done:
- Reordered the explicit layout to `GeneticsMask@0`, `SeedItemHashId@8`, `Growth01@12`, `Quality01@16`, `_pad0@20`, `_pad1@24`.
- Added `[BinaryBlittableSafe]`.
- Added cold-boot manifest assertions for the nested construction DTO.

Cinematic Cheats used:
- Cultivation remains a four-slot scalar physiology table. No plant mesh simulation or per-leaf biology was introduced.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us per cultivation/atmosphere scan; value is deterministic row layout and manifest coverage.

Verification:
- Source read-back confirmed byte offsets and manifest assertions.
- `git diff --check` passed for `CultivationManager.cs` and `BinaryLayoutManifest.cs`.
- Compile was not launched because CPU load reported 100%; no dotnet/csc process was active.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="CultivationSlotState" size="32" fields="GeneticsMask@0 size8; SeedItemHashId@8 size4; Growth01@12 size4; Quality01@16 size4; _pad0@20 size4; _pad1@24 size8" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-existing" use="private cultivation slot array remains pending approved Cultivation BufferID route" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="no spatial math changed" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="no job aliasing changed in this patch" />
</SELF_AUDIT>

## Survival Legacy Database Split Removal

What was wrong: The survival injected database legacy parser still used `Split('|')` for both header and row tokenization, creating managed arrays during database hydration and leaving a stale string hash parser beside the span-native runtime parser.
What was done: Header/row parsing now uses `ReadOnlySpan<char>` with `TryReadNextDelimitedToken`; required column extraction scans tokens by index without array allocation; hash parsing routes through the existing span overload. `git diff --check` passed for the file.
Cinematic Cheats used: None. This is data ingestion hardening. The active survival runtime continues to use native row records instead of managed row objects for gameplay lookup.
Exact Microseconds saved: 0 us hot path. Cold-load allocation removal only; estimated gain is reduced GC pressure during injected table parse.

<SELF_AUDIT>
  <Task>Task 19 CSV_INGESTOR_ZERO_GC_HARDENING continuation</Task>
  <TouchedFiles>
    <File>Assets/_Project/Scripts/HectonSurvivalSystem.cs</File>
  </TouchedFiles>
  <ParserRoute>
    <HeaderTokenizer>ReadOnlySpan&lt;char&gt; + TryReadNextDelimitedToken</HeaderTokenizer>
    <RowTokenizer>ReadOnlySpan&lt;char&gt; + TryGetRequiredColumnValue span scan</RowTokenizer>
    <RemovedManagedArrays>string[] headerTokens; string[] rowTokens</RemovedManagedArrays>
    <RemainingManagedBoundary>stableId.ToString() only for legacy SurvivalDatabaseItemParameters constructor; active NativeArray parser unaffected</RemainingManagedBoundary>
  </ParserRoute>
  <VaultBufferIDs>None changed</VaultBufferIDs>
  <StructLayouts>None changed in this pass</StructLayouts>
  <GCAllocations>Hot path: 0 bytes introduced. Cold legacy parser: per-line string[] allocation removed.</GCAllocations>
  <CompileGuard>No build launched because CPU was 100% and AGENTS.md forbids build over 50% CPU.</CompileGuard>
</SELF_AUDIT>

## Voxel Sculptor CSV Split Removal

What was wrong: `ShinobuVoxelSculptorWindow` is editor-only but located under the runtime script tree, and its tuning CSV importer allocated `string[]` rows with `Split(',')`.
What was done: Replaced split parsing with span token cursor helpers, ASCII header matching, and span-based numeric parsing. Verified `_Project/Scripts` excluding Editor/Test folders has no remaining `.Split(` hits, and targeted `git diff --check` passed for the file.
Cinematic Cheats used: None. This is CSV/tooling hardening for the debris tuning bake path.
Exact Microseconds saved: 0 us runtime. Cold editor import removes one managed array allocation per parsed tuning row.

<SELF_AUDIT>
  <Task>Task 19 CSV_INGESTOR_ZERO_GC_HARDENING continuation</Task>
  <TouchedFiles>
    <File>Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs</File>
  </TouchedFiles>
  <ParserRoute>
    <Tokenizer>ReadOnlySpan&lt;char&gt; cursor over comma-delimited row</Tokenizer>
    <RemovedManagedArrays>string[] cells</RemovedManagedArrays>
    <RuntimeScope>#if UNITY_EDITOR only; no player hot path</RuntimeScope>
  </ParserRoute>
  <VaultBufferIDs>None changed</VaultBufferIDs>
  <StructLayouts>None changed in this pass</StructLayouts>
  <GCAllocations>Runtime: 0 bytes introduced. Editor cold path: per-row string[] allocation removed.</GCAllocations>
  <CompileGuard>No build launched because CPU was 100% and AGENTS.md forbids build over 50% CPU.</CompileGuard>
</SELF_AUDIT>

## Scatter Backend Native Layout Hardening

What was wrong:
- Scatter backend candidate and cell-state data enters NativeArray/Burst paths, but adjacent quota, cell, config, parity, result, schedule, and shadow-completion DTOs still used implicit layout or accessor properties.
- `ScatterSimulationCandidate` carried validity as a managed `bool` lane before this continuation patch, and the shadow parity payload carried a managed `string` label in the transfer struct.

What was done:
- `ScatterSimulationLayerQuota` = 16 bytes, `ScatterSimulationQuotaState` = 64 bytes, `ScatterSimulationCellState` = 32 bytes, `ScatterSimulationParitySnapshot` = 64 bytes, `ScatterSimulationConfig` = 128 bytes, and `ScatterSimulationCandidate` = 64 bytes with explicit field offsets.
- `ScatterBackendParityReference` = 32 bytes, `ScatterBackendScheduleRequest` = 96 bytes, `ScatterBackendShadowScheduleContext` = 80 bytes, and `ScatterBackendShadowCompletion` = 128 bytes with raw fields.
- Shadow completion now stores `ParityStatusCode` as a byte; string label resolution happens only at the director debug boundary.
- `BinaryLayoutManifest.VerifyWorldScatterLayouts()` now asserts the scatter external-contract sizes and offsets through reflection without adding a World.Contracts dependency to Core source.

Cinematic Cheats used:
- Scatter remains a BRG/backend seam and parity shadow pass. No per-flora physics, GameObject instantiation path, or mesh-collider route was introduced. The pass preserves the existing visual scatter fake and only hardens its native transfer payloads.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us per scatter pass; main gain is ARM64-safe native rows, one-cache-line candidate payloads, a 64-byte counter row, and removal of accessor/string/bool hazards from the Burst-backed seam.

Verification:
- Targeted stale-property scan returned no accessor properties in the patched scatter native DTO files.
- Targeted stale-bool scan found no `IsValid = true` / `bool IsValid` scatter candidate route.
- `git diff --check` passed for scatter layout files, `ScatterEvaluator.cs`, `WorldProceduralScatterDirectorBackendIntegration.cs`, and `BinaryLayoutManifest.cs`.
- Compile was not launched because CPU load reported 57%; no dotnet/csc/VBCSCompiler process was active, but AGENTS.md forbids build over 50% CPU.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="ScatterSimulationLayerQuota" size="16" fields="PlacementsPerCell@0 size4; CellStride@4 size4; FamilyIndex@8 size4; _pad0@12 size4" />
    <DTO name="ScatterSimulationQuotaState" size="64" fields="Ground@0 size16; Cluster@16 size16; Structure@32 size16; Spawn@48 size16" />
    <DTO name="ScatterSimulationCellState" size="32" fields="CellKey@0 size8; CellX@8 size4; CellZ@12 size4; Height@16 size4; HeightSource@20 size4; BiomeInfluencePacked@24 size4; Eligibility@28 size1; Suppression@29 size1; DirtyFlags@30 size1; _pad0@31 size1" />
    <DTO name="ScatterSimulationParitySnapshot" size="64" fields="CandidateChecksum@0 size8; CellChecksum@8 size8; CandidateCount@16 size4; GroundCount@20 size4; ClusterCount@24 size4; StructureCount@28 size4; SpawnCount@32 size4; EligibleGroundCells@36 size4; EligibleClusterCells@40 size4; EligibleStructureCells@44 size4; EligibleSpawnCells@48 size4; DirtyCellCount@52 size4; SuppressedCellCount@56 size4; _pad0@60 size4" />
    <DTO name="ScatterSimulationConfig" size="128" fields="QuotaState@0 size64; PlayerPosition@64 size12; CellSize@76 size4; SurfaceYOffset@80 size4; Seed@84 size4; RadiusCells@88 size4; GroundPlacementsPerCell@92 size4; ClusterPlacementsPerCell@96 size4; StructureCellStride@100 size4; SpawnCellStride@104 size4; GroundFamilyIndex@108 size4; ClusterFamilyIndex@112 size4; StructureFamilyIndex@116 size4; SpawnFamilyIndex@120 size4; DefaultEligibility@124 size1; DefaultSuppressionState@125 size1; DirtyFlags@126 size1; _pad0@127 size1" />
    <DTO name="ScatterSimulationCandidate" size="64" fields="CellKey@0 size8; Position@8 size12; Rotation@20 size4; Scale@24 size4; Score@28 size4; FamilyIndex@32 size4; LayerIndex@36 size4; HeightSource@40 size4; IsValid@44 size1; _pad0@45 size1; _pad1@46 size2; _pad2@48 size8; _pad3@56 size8" />
    <DTO name="ScatterBackendParityReference" size="32" fields="CandidateChecksum@0 size8; CandidateCount@8 size4; GroundCount@12 size4; ClusterCount@16 size4; StructureCount@20 size4; SpawnCount@24 size4; _pad0@28 size4" />
    <DTO name="ScatterBackendScheduleRequest" size="96" fields="ParityReference@0 size32; ObserverPosition@32 size12; CellSize@44 size4; SurfaceYOffset@48 size4; Seed@52 size4; TotalCells@56 size4; RadiusCells@60 size4; GroundBudget@64 size4; ClusterBudget@68 size4; StructureStride@72 size4; SpawnStride@76 size4; EligibilityMask@80 size1; DefaultSuppressionState@81 size1; DirtyFlags@82 size1; _pad0@83 size1; _pad1@84 size4; _pad2@88 size8" />
    <DTO name="ScatterBackendShadowCompletion" size="128" fields="BackendParity@0 size64; ClassicParity@64 size32; CandidateCount@96 size4; ClassicQueuedCandidateCount@100 size4; CandidateDelta@104 size4; GroundDelta@108 size4; ClusterDelta@112 size4; StructureDelta@116 size4; SpawnDelta@120 size4; CandidateChecksumMatchFlag@124 size1; HasParityMatchFlag@125 size1; IsJobActiveFlag@126 size1; ParityStatusCode@127 size1" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-existing" use="scatter working memory remains local scene scratch; no approved Scatter BufferID route exists" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added; managed parity string removed from transfer payload and resolved only at debug owner boundary" />
  <AUP status="no spatial AUP math changed in this patch; scatter cell positions remain runtime-local inputs" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="existing ScatterCellEvaluationJob retains ReadOnly/NoAlias metadata; candidate output and counter lanes stay isolated" />
</SELF_AUDIT>

## Scatter Backend Binding Sentinel Registration

What was wrong:
- `ScatterBackendBindingState` allocated persistent scene-lifetime `NativeArray<float>` height samples and `NativeArray<ScatterSimulationCellState>` bridge buffers without `NativeMemorySentinel` registration.

What was done:
- Added `NativeMemoryOwner = ScatterBackendBindingState` and `NativeAllocationLifetime.Scene`.
- Registered `_heightSamples` and `_cellStates` immediately after allocation.
- Replaced direct dispose on resize/shutdown with `NativeMemorySentinel.UnregisterNativeArray` followed by `Dispose()`.

Cinematic Cheats used:
- None. This is native lifecycle accounting. Scatter remains the same BRG/shadow backend visual-placement seam.

Exact Microseconds saved:
- 0 us hot path. Static value is leak/fragmentation visibility and deterministic scene teardown accounting.

Verification:
- Source read-back confirmed registration after both allocations and unregister-before-dispose on resize/shutdown.
- `git diff --check` passed for `ScatterBackendBindingState.cs`.
- Compile was not launched because CPU load reported 100%; no dotnet/csc/VBCSCompiler process was active.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="none-new" size="unchanged" fields="no DTO layout changed in this lifecycle patch" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-existing" use="scatter backend binding arrays remain owner-local scratch; no approved scatter BufferID route exists" />
  </VAULT_BUFFER_IDS>
  <NATIVE_ALLOCATIONS>
    <ARRAY field="_heightSamples" owner="ScatterBackendBindingState" lifetime="Scene" sentinel="registered-after-allocation; unregistered-before-dispose" />
    <ARRAY field="_cellStates" owner="ScatterBackendBindingState" lifetime="Scene" sentinel="registered-after-allocation; unregistered-before-dispose" />
  </NATIVE_ALLOCATIONS>
  <GC_HOT_PATHS status="no managed allocation added to gameplay hot path" />
  <AUP status="no spatial math changed" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="no job aliasing changed in this lifecycle patch" />
</SELF_AUDIT>

## Scatter Working Memory Bulk Zero

What was wrong:
- `WorldProceduralScatterDirector.ScatterWorkingMemory.ResetGridPlacementSpatialCache` cleared four native int/float scratch arrays through a scalar generic loop, even though every live call only needed zeroed unmanaged memory.

What was done:
- Added `Unity.Collections.LowLevel.Unsafe` to the scatter working memory file.
- Replaced the generic value loop with `UnsafeUtility.MemClear` over `NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks`.
- Kept the helper constrained to `where T : unmanaged`, and kept the existing sentinel/owner-local lifecycle unchanged.

Cinematic Cheats used:
- None added. This preserves the existing scatter placement fake and reduces CPU reset overhead around its native scratch buffers.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us per scatter cache reset; four small scalar loops collapse to native bulk zero.

Verification:
- Source read-back confirmed all four scratch clears route through the unmanaged `MemClear` helper.
- `git diff --check` passed for `WorldProceduralScatterWorkingMemory.cs`.
- Compile was not launched because CPU load reported 100%; no dotnet/csc/VBCSCompiler process was active, but AGENTS.md forbids build over 50% CPU.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="none-new" size="unchanged" fields="no DTO layout changed in this scratch-clear patch" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-existing" use="scatter working memory remains owner-local scene scratch; no approved scatter BufferID route exists" />
  </VAULT_BUFFER_IDS>
  <NATIVE_CLEARS>
    <ARRAY field="CandidateAcceptanceClusterAccentCountsScratch" clear="UnsafeUtility.MemClear" />
    <ARRAY field="CandidateAcceptanceStructureAccentCountsScratch" clear="UnsafeUtility.MemClear" />
    <ARRAY field="CandidateAcceptanceClusterAccentRoleMaxRatiosScratch" clear="UnsafeUtility.MemClear" />
    <ARRAY field="CandidateAcceptanceStructureAccentRoleMaxCountsScratch" clear="UnsafeUtility.MemClear" />
  </NATIVE_CLEARS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="no spatial math changed" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="no job aliasing changed; scratch clear remains owner-local" />
</SELF_AUDIT>

## Biome Transition Alias Tightening

What was wrong:
- Biome transition DTO rows were already explicit and Vault-backed, but several Burst jobs left output-only buffers as plain `[NoAlias]`.
- `BlendAtmosphereJob` wrote `BlendMask[0]` and then read it back only to hash state, preventing the mask lane from being declared write-only.

What was done:
- Marked emergency mock `States` and `Centers` as `[WriteOnly, NoAlias]`.
- Marked proximity `Influence` and `BiomeChangedWriter` as `[WriteOnly, NoAlias]`.
- Marked atmosphere `CurrentAtmosphere` and `BlendMask`, shader `ShaderPayload`, acoustic `AcousticStage`, telemetry `TelemetryRing`, and CSV ingest `States/Centers/Counters` as `[WriteOnly, NoAlias]`.
- Reworked `BlendAtmosphereJob` to hash a stack-local `BiomeBlendMaskDTO` copy, eliminating the `BlendMask[0]` readback inside that producer job.

Cinematic Cheats used:
- The existing Dear Lie remains shader/atmosphere driven: biome fog, absorption, audio blend, and dither payload are scalar GPU signals, not per-particle fluid simulation or scene-object churn.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us per transition cadence; the gain is tighter Burst alias proof on seven jobs and removal of one native buffer readback in the mask producer.

Verification:
- Source read-back confirmed output-only annotations and local-mask hashing.
- `git diff --check` passed for `BiomeTransitionFogBlendJobs.cs`.
- Compile was not launched because CPU load reported 100%; no dotnet/csc/VBCSCompiler process was active, but AGENTS.md forbids build over 50% CPU.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="BiomeStateDTO" size="64" fields="unchanged explicit layout; BiomeTransitionNativeLayout validates offsets" />
    <DTO name="BiomeCenterDTO" size="64" fields="unchanged explicit layout; CenterAup@0 double3 before scalar radii and hashes" />
    <DTO name="BiomeInfluenceDTO" size="64" fields="unchanged explicit layout; four-lane hashes/weights/distances/state indices" />
    <DTO name="CurrentAtmosphereDTO" size="128" fields="unchanged explicit layout; shader payload source plus influence row" />
    <DTO name="BiomeBlendMaskDTO" size="64" fields="unchanged explicit layout; mask producer now write-only" />
    <DTO name="BiomeTransitionTelemetryEntry" size="64" fields="unchanged explicit layout; 300-frame Vault telemetry ring row" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="BiomeTransitionStates" use="BiomeStateDTO Vault-owned state rows" />
    <BUFFER id="BiomeTransitionCenters" use="BiomeCenterDTO Vault-owned AUP center rows" />
    <BUFFER id="BiomeTransitionInfluences" use="BiomeInfluenceDTO write-only proximity output" />
    <BUFFER id="BiomeTransitionCurrentAtmosphere" use="CurrentAtmosphereDTO write-only blend output" />
    <BUFFER id="BiomeTransitionBlendMask" use="BiomeBlendMaskDTO write-only mask output" />
    <BUFFER id="BiomeTransitionShaderPayload" use="float4 shader CBuffer payload slots" />
    <BUFFER id="BiomeTransitionAcousticStage" use="BiomeAcousticStageDTO write-only audio staging" />
    <BUFFER id="BiomeTransitionTelemetryRing" use="BiomeTransitionTelemetryEntry 300-frame ring" />
    <BUFFER id="BiomeTransitionCounters" use="read/write cadence and blackbox counters where required" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="existing jobs subtract center/player in double before float local distance; no regression" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="output-only NativeArray/NativeQueue lanes now marked WriteOnly+NoAlias; counters left read/write where required" />
</SELF_AUDIT>

## BRG Vegetation Job Bool Payload Purge

What was wrong:
- `HectonBatchRendererGroupUtility.BuildMatrixVisibilityMaskJob` and `FinalizeSingleDrawCommandOutputJob` carried public bool fields in Burst job payloads.
- `HectonIndirectVegetationRenderer.BuildVegetationVisibilityMaskJob` and `FinalizeVegetationDrawOutputJob` also carried public bool pass/culling fields in Burst job payloads.

What was done:
- Replaced `EnableCpuCulling`, `ReceiveShadows`, vegetation far/depth/shadow/motion/darkness pass fields with byte-backed `*Flag` lanes.
- Converted scheduler assignments to explicit `? (byte)1 : (byte)0`.
- Marked visibility mask producer arrays as `[WriteOnly, NoAlias]`.

Cinematic Cheats used:
- Existing vegetation renderer remains BRG/indirect draw based. No GameObject spawning, no collider forest, and no per-instance CPU physics were introduced.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us per culling callback; primary gain is stable Burst job ABI and alias proof, not algorithmic complexity reduction.

Verification:
- Targeted stale-bool scan returned no remaining public bool pass/culling fields in the patched BRG job structs.
- `git diff --check` passed for `HectonBatchRendererGroupUtility.cs` and `HectonIndirectVegetationRenderer.cs`.
- Compile was not launched because CPU load reported 100%; no dotnet/csc/VBCSCompiler process was active, but AGENTS.md forbids build over 50% CPU.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <JOB name="BuildMatrixVisibilityMaskJob" changed="EnableCpuCulling bool -> byte EnableCpuCullingFlag; VisibilityMask WriteOnly+NoAlias" />
    <JOB name="FinalizeSingleDrawCommandOutputJob" changed="ReceiveShadows bool -> byte ReceiveShadowsFlag" />
    <JOB name="BuildVegetationVisibilityMaskJob" changed="EnableCpuCulling/UseFarPass/UseShadowPass/BypassDarknessCulling bools -> byte flags; VisibilityMask WriteOnly+NoAlias" />
    <JOB name="FinalizeVegetationDrawOutputJob" changed="UseFarPass/UseDepthPass/UseDepthFarPass/UseShadowPass/UseMotionPass/UseMotionFarPass bools -> byte flags" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-changed" use="BRG culling uses existing renderer-owned native buffers; no new persistent allocation route" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="no spatial AUP math changed; renderer matrices remain runtime-local" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="visibility mask producer arrays marked WriteOnly+NoAlias" />
</SELF_AUDIT>

## Thermal Slumping Bool Payload Purge

What was wrong:
- `ThermalSlumpingJob` carried `public bool WriteWearMask` inside a Burst `IJobParallelFor` payload.

What was done:
- Replaced the bool with `public byte WriteWearMaskFlag`.
- Updated the runtime job branch checks to `WriteWearMaskFlag != 0`.
- Updated the editor and MapMagic schedule sites to assign byte zero.

Cinematic Cheats used:
- None added. This is ABI hygiene for an existing deterministic terrain deformation pass.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: no runtime hot-path claim; removes one platform-sensitive bool lane from the job payload.

Verification:
- Stale symbol scan found no remaining `WriteWearMask` job field or assignment.
- `git diff --check` passed for `ThermalSlumpingJob.cs`, `HydraulicErosionSmokeTester.cs`, `ErosionTestHarness.cs`, and `HectonHydraulicErosionMapMagicNode.cs`.
- Compile was not launched because CPU load reported 97%; AGENTS.md forbids build over 50% CPU.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <JOB name="ThermalSlumpingJob" changed="WriteWearMask bool -> byte WriteWearMaskFlag" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-changed" use="job consumes caller-provided native height/wear buffers; no persistent allocation route changed" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="no world-scale spatial math changed" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="existing Input/Output NoAlias metadata retained; optional wear lane remains read/write" />
</SELF_AUDIT>

## Procedural Wreck Mesh Merge Flag Packing

What was wrong:
- `CombineMeshDataJob` carried `HasNormals`, `HasUvs`, and `HasColors` as public bool fields inside a Burst job payload.

What was done:
- Added `AttributeFlagNormals`, `AttributeFlagUvs`, and `AttributeFlagColors` constants.
- Replaced the three bool fields with one `uint AttributeFlags` mask.
- Updated both mesh-merge construction sites to compose the mask from source mesh attributes.

Cinematic Cheats used:
- Existing procedural wreck color fallback remains a cheap hash-based rust/algae visual fake when a source mesh lacks vertex colors.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us per wreck mesh copy job; reduces job payload ambiguity and packs attribute branch state into one scalar lane.

Verification:
- Stale symbol scan found no remaining `HasNormals`/`HasUvs`/`HasColors` job fields or assignments.
- `git diff --check` passed for `ProceduralWreckGenerator.cs`.
- Compile was not launched because CPU load reported 100%; AGENTS.md forbids build over 50% CPU.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <JOB name="CombineMeshDataJob" changed="HasNormals/HasUvs/HasColors bools -> uint AttributeFlags" flags="Normals=1; Uvs=2; Colors=4" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-changed" use="wreck mesh merge consumes Mesh.MeshData and writable mesh native arrays; no persistent Vault route changed" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added to gameplay hot path" />
  <AUP status="local wreck mesh coordinates only; no AUP math changed" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="existing destination native arrays remain NoAlias" />
</SELF_AUDIT>

## Lore Unlock Native Sentinel Registration

What was wrong:
- `LoreDatabaseManager._unlockedWords` allocated a persistent `NativeArray<uint>` for industrial lore unlock bits without `NativeMemorySentinel` registration.

What was done:
- Added `NativeMemoryOwner = LoreDatabaseManager` and session lifetime.
- Registered `_unlockedWords` after allocation in `EnsureUnlockStorage`.
- Unregistered `_unlockedWords` before deferred disposal in `OnDestroy`.

Cinematic Cheats used:
- None. This is allocation lifecycle accounting for a fixed save/lore bitmask.

Exact Microseconds saved:
- 0 us hot path. Static value is leak/fragmentation visibility and correct native allocation accounting.

Verification:
- Source read-back confirmed register-after-allocation and unregister-before-dispose.
- `git diff --check` passed for `LoreDatabaseManager.cs`.
- Compile was not launched because CPU load reported 100%; AGENTS.md forbids build over 50% CPU.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="none-new" size="unchanged" fields="no DTO layout changed in this sentinel-only patch" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-existing" use="lore unlock bitmask remains owner-local save state; no approved lore BufferID route exists" />
  </VAULT_BUFFER_IDS>
  <NATIVE_ALLOCATIONS>
    <ARRAY field="_unlockedWords" owner="LoreDatabaseManager" lifetime="Session" sentinel="registered-after-allocation; unregistered-before-dispose" />
  </NATIVE_ALLOCATIONS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="no spatial math changed" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="no Burst alias lanes changed" />
</SELF_AUDIT>

## Indirect Vegetation Native Read Token Accessor Purge

What was wrong:
- `HectonIndirectVegetationNativeReadBuffer` carried NativeArrays and a producer `JobHandle` through get-only auto-properties.
- It also carried `HasExplicitBounds` as a bool property and `IsValid` as an accessor-backed predicate.

What was done:
- Converted the token to raw readonly fields.
- Replaced `HasExplicitBounds` with byte `HasExplicitBoundsFlag`.
- Added static `IsValid(in readBuffer)` and `HasExplicitBounds(in readBuffer)` helpers.
- Updated renderer native-buffer sync call sites to use static helpers.

Cinematic Cheats used:
- Existing vegetation upload remains BRG/native-buffer based. No GameObject or CPU collider path was introduced.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us per native buffer sync; primary gain is removing accessor-backed copies from the native handoff seam.

Verification:
- Stale accessor scan found no `readBuffer.IsValid` or `readBuffer.HasExplicitBounds` call sites.
- `git diff --check` passed for `HectonIndirectVegetationContracts.cs` and `HectonIndirectVegetationRenderer.cs`.
- Compile was not launched because CPU load reported 100%; no dotnet/csc/VBCSCompiler process was active.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <TOKEN name="HectonIndirectVegetationNativeReadBuffer" fields="raw readonly NativeArray<Matrix4x4>; NativeArray<HectonVegetationInstanceData>; int InstanceCount; int BufferIndex; JobHandle ProducerHandle; byte HasExplicitBoundsFlag; Bounds DrawBounds" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-changed" use="producer owns native front/back buffers; token only transfers read ownership to renderer" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="no spatial math changed; draw bounds remain producer-supplied" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="native arrays remain producer-owned; token no longer uses accessor properties" />
</SELF_AUDIT>

## MapMagic Height Payload Accessor Purge

What was wrong:
- `TerrainHeightSamplePayload` carried a `NativeArray<ushort>` heightmap alias through auto-properties.
- `TerrainHeightSamplePayload.IsValid` and `QuantizedHeightmapPayload.IsValid` were accessor predicates on native terrain payload tokens.

What was done:
- Converted `TerrainHeightSamplePayload` to raw readonly fields.
- Replaced both validity properties with static `IsValid(in payload)` helpers.
- Updated MapMagic runtime bridge, fauna kinematics terrain fallback, geology seam heightmap copy, and procedural ore spawner call sites.

Cinematic Cheats used:
- Existing terrain-height handoff remains a quantized R16 sample alias. No terrain collider, raycast, or mesh sampling path was introduced.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us per terrain-height payload resolution; primary gain is no accessor-backed token validation.

Verification:
- Focused scan found no remaining `TerrainHeightSamplePayload` or `QuantizedHeightmapPayload` `payload.IsValid` call sites in touched paths.
- `git diff --check` passed for MapMagic height payload accessor purge files.
- Compile was not launched under current build discipline.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <TOKEN name="TerrainHeightSamplePayload" fields="raw readonly NativeArray&lt;ushort&gt; HeightSamples; Vector3 TerrainPosition; Vector3 TerrainSize; int HeightmapResolution; int CacheRevision" />
    <TOKEN name="QuantizedHeightmapPayload" fields="raw readonly NativeArray&lt;ushort&gt; HeightSamples; Vector3 TerrainPosition; Vector3 TerrainSize; int HeightmapResolution; int CacheRevision" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="TerrainSeamHeightmap" use="existing geology/IK consumer copy target; payload purge does not add ownership" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="no spatial math changed; terrain payload still carries runtime-space tile origin and existing consumers preserve their AUP conversions" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="bridge-owned height sample aliases now validated through static in helpers instead of accessor properties" />
</SELF_AUDIT>

## Scatter Runtime Rule Token Accessor Purge

What was wrong:
- Dense scatter scoring tokens used get-only auto-properties while being copied through lists, dictionaries, and `in` helper calls.
- The rule token also carries managed authoring references, making explicit layout/Vault migration a separate data-monolith problem rather than a safe local patch.

What was done:
- Converted `ScatterRuntimeRuleEntry`, `ScatterBiomeScoreContext`, `ScatterPatternScoreContext`, `ScatterCandidatePreview`, `ScatterPreviewGizmoRecord`, and `ScatterCandidate` to raw readonly fields.
- Preserved field names and existing scatter owner routes.

Cinematic Cheats used:
- Scatter remains rule-scored into BRG/placement buffers; no collider/scene-search route was introduced.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us per scatter scoring/reconcile pass; primary gain is removing property methods from hot scoring tokens.

Verification:
- `git diff --check` passed for `WorldProceduralScatterDirector.cs`.
- Compile was not launched under current build discipline.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <TOKEN name="ScatterRuntimeRuleEntry" fields="raw readonly managed refs + enum/int/float/bool lanes; not explicit-layout because it is not blittable" />
    <TOKEN name="ScatterCandidate" fields="raw readonly Placement; Family; Rule; HeatmapChannel; Heat; Score" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-added" use="scatter runtime rules remain authoring-managed owner data; numeric Vault bake requires separate route-card" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="no spatial math changed" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="managed scoring tokens now raw-field only; no Burst NativeArray alias lanes changed" />
</SELF_AUDIT>

## Terrain Hole Native Bool Eviction

What was wrong:
- `TerrainHoleMaskBuildJob` emitted terrain-hole results into `NativeArray<bool>`.
- `TerrainHoleRecord` was a Burst `NativeArray` input row without explicit layout.

What was done:
- Replaced the native mask lane with `NativeArray<byte>` where `1` means terrain remains and `0` means hole.
- Converted byte flags into Unity's required `bool[,]` only at `TerrainData.SetHolesDelayLOD` staging.
- Pinned `TerrainHoleRecord` to `[StructLayout(LayoutKind.Explicit, Size = 32)]`.
- Added `[ReadOnly, NoAlias]` to hole input and `[WriteOnly, NoAlias]` to byte output.

Cinematic Cheats used:
- Terrain hole masking stays a cheap rasterized byte mask. No terrain collider or per-hole physics query path was introduced.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us per tile terrain-hole mask job; main gain is deterministic native flag width and alias proof.

Verification:
- Focused scan found no remaining `NativeArray<bool>` for terrain-hole masks.
- `git diff --check` passed for `HectonMapMagicVegetationBridge.cs` and `VegetationTerrainHoleSynchronizer.cs`.
- Compile was not launched under current build discipline.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="TerrainHoleRecord" size="32" fields="X@0 float; Y@4 float; Z@8 float; Radius@12 float; RadiusSq@16 float; HoleId@20 int; SourceType@24 byte; pad@25..31" />
    <BUFFER name="TerrainHoleMaskNative" element="byte" meaning="1 terrain; 0 hole" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-added" use="terrain-hole mask remains per-tile MapMagic owner scratch; Unity API requires managed bool staging at final apply" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added; existing bool[,] Unity staging remains reusable per tile" />
  <AUP status="no spatial origin math changed" />
  <COMPLETE_CALLS status="no new .Complete() added; existing DispatcherJobSwap completion window retained" />
  <ALIASING status="TerrainHoles ReadOnly/NoAlias; Output WriteOnly/NoAlias" />
</SELF_AUDIT>

## World Streaming Row Layout Pinning

What was wrong:
- `TerrainHoleStreamingRecord` and `HLODData` are NativeArray streaming/render rows but relied on default struct layout.

What was done:
- Pinned `TerrainHoleStreamingRecord` to explicit 32 bytes.
- Pinned `HLODData` to explicit 48 bytes.
- Preserved public field names and existing consumers.

Cinematic Cheats used:
- Existing HLOD/impostor flow remains data-driven BRG/render payloads. No scene-object scan or collider route was added.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us per streaming/HLOD scan; primary gain is deterministic row stride.

Verification:
- Source scan confirmed both streaming rows now have explicit layouts.
- `git diff --check` passed for `HectonWorldStreamingTypes.cs`.
- Compile was not launched under current build discipline.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="TerrainHoleStreamingRecord" size="32" fields="Position@0 Vector3; Radius@12 float; HoleId@16 int; SourceType@20 byte; pad@21..31" />
    <DTO name="HLODData" size="48" fields="Center@0 Vector3; Size@12 Vector3; Fade01@24 float; StructureId@28 int; Type@32 byte; pad@33..47" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-added" use="existing HectonMapMagicVegetationBridge native streaming/HLOD arrays retain ownership" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="no spatial math changed" />
  <COMPLETE_CALLS status="no new .Complete() added" />
  <ALIASING status="row layout only; existing NativeArray read lanes retained" />
</SELF_AUDIT>

## Cave Graph Temp Native Bool Eviction

What was wrong:
- `CaveGraphGenerator.GenerateEntrances` used `NativeArray<bool>` for temp used-room flags.

What was done:
- Replaced the temp native bool scratch with `NativeArray<byte>`.
- Changed checks/writes to `usedRooms[r] != 0` and `usedRooms[bestRoom] = 1`.

Cinematic Cheats used:
- Existing cave entrance generation remains a score-based selection fake, not a physics/raycast search.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us in cold cave generation; primary gain is removing native bool representation.

Verification:
- Runtime script sweep now finds no `NativeArray<bool>` hits except the patched byte scratch references.
- `git diff --check` passed for `CaveGraphGenerator.cs`.
- Compile was not launched because CPU guard was 100% in the latest probe.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <BUFFER name="usedRooms" element="byte" lifetime="Allocator.Temp" meaning="0 unused; 1 consumed" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none" use="caller-owned cold cave generation temp scratch; no persistent route" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="no spatial precision math changed" />
  <COMPLETE_CALLS status="no .Complete() involved" />
  <ALIASING status="local temp scratch only" />
</SELF_AUDIT>

## Scatter Runtime Context Accessor Purge

What was wrong:
- Scatter sampling/reconcile context tokens still exposed get-only properties, and `ScatterPlacement.RuntimePosition` hid AUP-to-runtime conversion behind a property accessor.
- Secondary-biome/status flags remained bool-backed in transfer structs that are passed by `in` across the scatter runtime seam.

What was done:
- Converted `SamplingSnapshot`, `ScatterBackendRuntimeStatus`, `ScatterSamplingBeginContext`, and `ScatterBiomeTransitionContext` to raw readonly fields.
- Converted `ScatterPlacement` property state to raw fields and replaced `.RuntimePosition` call sites with `ReadRuntimePosition()`.
- Changed `ScatterBiomeTransitionContext.HasSecondary` and `ScatterBackendRuntimeStatus` booleans to byte-backed lanes at the boundary.

Cinematic Cheats used:
- The existing scatter route remains a budgeted BRG/proxy placement fake with sampled biome/terrain inputs. No collider query, per-object physics, or new scene hierarchy scan was added.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us per scatter sampling/reconcile pass; primary gain is removing property/accessor dispatch and bool-backed transfer flags from dense scatter context reads.

Verification:
- Focused property scan returned no get-only property hits in the modified scatter context/status/snapshot files.
- Targeted `git diff --check` passed for the modified scatter runtime context files.
- Compile was not launched because CPU guard reported 100% with no dotnet/csc/VBCSCompiler process active.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <TOKEN name="SamplingSnapshot" fields="RuntimeCenter Vector3; AbsoluteCenter Vector3; CenterCellX int; CenterCellZ int; CaptureTime float" note="managed stack token, not NativeArray/binary row" />
    <TOKEN name="ScatterBackendRuntimeStatus" fields="ActiveBackendKind enum; ActiveBackendKindLabel string; ResolvedExecutionMode enum; ResolvedExecutionModeLabel string; ResolutionReason string; HasFacade byte; IsJobActive byte; IsJobCompleted byte" note="managed status token, not NativeArray/binary row" />
    <TOKEN name="ScatterSamplingBeginContext" fields="Rules IReadOnlyList; RuntimeCenter Vector3; AbsoluteCenter Vector3; cell/budget ints; Now float" note="managed sampling context, not NativeArray/binary row" />
    <TOKEN name="ScatterBiomeTransitionContext" fields="HasSecondary byte; SecondaryProfile ref; SecondaryFamily ref; SecondaryBiomeContext ref; SecondaryScoreContext value; PrimaryWeight float; SecondaryWeight float" note="managed scoring context, not NativeArray/binary row" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-added" use="scatter placement/rule contexts remain owner-local managed authoring/runtime tokens; no approved BufferID route exists" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="runtime conversion is explicit via ReadRuntimePosition; no absolute float cast added" />
  <COMPLETE_CALLS status="no .Complete() added" />
  <ALIASING status="managed context accessor purge only; no NativeArray lane changed" />
</SELF_AUDIT>

## Fauna Perception Snapshot Flag Packing

What was wrong:
- `FaunaPerceptionSnapshot` used seven public bool fields for player/tool presence, AUP, velocity, forward, flashlight, and scavenge-tool state.
- The snapshot is a repeated fauna sensory transfer seam, so bool field width and accessor-free intent needed a tighter contract.

What was done:
- Replaced the bool fields with a single `uint Flags` lane.
- Added static `in` predicates on `FaunaPerceptionSnapshot`.
- Updated `FaunaBrain` producers to set flag bits and `FaunaSensorSuite` consumers to use the static predicates.

Cinematic Cheats used:
- Fauna perception still consumes cached player/tool snapshots and AUP deltas; no collider-based truth scan or per-creature scene search was added.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us per active fauna sensory tick; primary gain is compact flag reads and removing bool field ABI ambiguity from the snapshot.

Verification:
- Stale `snapshot.Has*` / `perceptionSnapshot.Has*` field scan returned no legacy bool field usages for `FaunaPerceptionSnapshot`.
- Targeted `git diff --check` passed for `FaunaSensorSuite.cs` and `FaunaBrain.cs`.
- Compile was not launched under the current CPU/build discipline.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <TOKEN name="FaunaPerceptionSnapshot" fields="PlayerAup; PlayerPosition; PlayerVelocity; PlayerForward; ScavengeToolPosition; ScavengeToolAup; ScavengeToolOwner; Flags uint" note="managed sensory token with Component ref; not NativeArray/binary/Burst row" />
    <FLAGS name="FaunaPerceptionSnapshot.Flags" bits="0 HasPlayer; 1 HasPlayerAup; 2 HasPlayerVelocity; 3 HasPlayerForward; 4 PlayerFlashlightOn; 5 HasScavengeTool; 6 HasScavengeToolAup" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-added" use="fauna perception snapshot remains stack/local sensory transfer between FaunaBrain and FaunaSensorSuite" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="existing player/tool AUP fields retained; no absolute float cast added" />
  <COMPLETE_CALLS status="no .Complete() added" />
  <ALIASING status="managed snapshot flag packing only; no NativeArray lane changed" />
</SELF_AUDIT>

## Indirect Vegetation Binding Cache Flag Packing

What was wrong:
- `HectonIndirectVegetationRenderer` still used public bool fields in repeated render binding, compute binding, indirect-args clear, and CPU culling scratch cache records.
- These are not binary DTO rows, but they are hot render/culling state structs read every vegetation draw path and should not retain platform-sensitive bool lanes.

What was done:
- Replaced `UseGpuIndirect`, `IsValid`, `IsShadowKernel`, `IsClearKernel`, and `ActiveHandleValid` cache fields with byte-backed `*Flag` fields.
- Updated all assignment and comparison sites to write `BindingFlagTrue/False` or compare `!= 0`.
- Left the structs as managed cache records because they contain Unity objects, `GraphicsBuffer`, `NativeArray<T>`, and `JobHandle` wrappers.

Cinematic Cheats used:
- Preserved the existing BRG/GPU indirect vegetation path, HZB/depth-pyramid culling, deterministic density decimation, and shader wind/snap fake. No GameObject foliage spawning, collider queries, or per-instance CPU animation was introduced.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us per vegetation render/cull path; primary gain is deterministic flag width and simpler cache comparisons in repeated binding checks.

Verification:
- Legacy field-name scan shows no remaining binding/scratch bool field uses; remaining `.IsValid()` matches are `GraphicsBuffer` extension calls or the static native-read-buffer predicate.
- Targeted `git diff --check` passed for `HectonIndirectVegetationRenderer.cs`.
- Compile was not launched because CPU guard reported 100% with no dotnet/csc/VBCSCompiler process active.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <TOKEN name="MaterialBindingState" fields="Material ref; GraphicsBuffer refs; Vector4; float lanes; UseGpuIndirectFlag byte; IsValidFlag byte" note="managed render cache token, not NativeArray/binary row" />
    <TOKEN name="ComputeCullBindingState" fields="ComputeShader ref; int Kernel; GraphicsBuffer refs; IsShadowKernelFlag byte; IsValidFlag byte" note="managed compute binding cache token" />
    <TOKEN name="ComputeSnapBindingState" fields="ComputeShader ref; int Kernel; GraphicsBuffer refs; IsClearKernelFlag byte; IsValidFlag byte" note="managed compute binding cache token" />
    <TOKEN name="IndirectArgsClearBindingState" fields="ComputeShader ref; GraphicsBuffer ref; Mesh ref; int mesh constants; IsValidFlag byte" note="managed indirect args cache token" />
    <TOKEN name="CpuCullingScratchBuffer" fields="NativeArray byte/float4 lanes; JobHandle ActiveHandle; VisibilityCapacity int; ActiveHandleValidFlag byte" note="owner-local native scratch transport; not a NativeArray element row" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="none-added" use="vegetation binding cache and CPU fallback scratch remain owner-local render cache; existing Sentinel registration and exemption labels preserved" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="no spatial math changed" />
  <COMPLETE_CALLS status="no .Complete() added; existing dispatcher fence completion path unchanged" />
  <ALIASING status="cache flag packing only; existing NativeArray alias metadata from prior BRG pass preserved" />
</SELF_AUDIT>

## TBDR Native Support Flag Packing

What was wrong:
- `TBDRVertexBudgetVault`, `TBDRTextureStreamingTracker`, and `TBDRPipelineTelemetryRecorder` used public bool fields to gate Vault ownership, cold fallback disposal, external telemetry-ring ownership, and dump state.
- These records sit around persistent/Vault-backed native buffers and blackbox telemetry; their lifecycle flags should not remain platform-sensitive public bool lanes.

What was done:
- Added a local `TBDRByteFlags` helper.
- Replaced `UsesGlobalDataVault`, `UsesExternalRing`, and `Dumped` with byte-backed flag fields.
- Updated all registration, disposal, acquisition, dump, and overflow guard sites to compare byte flags directly.

Cinematic Cheats used:
- Preserved the existing TBDR hardware-budget fake: a fixed vertex/tile/texture budget and blackbox ring guide culling/streaming pressure instead of simulating GPU driver behavior or per-object visibility proof on the CPU.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: 0 us hot-path timing claim; the value is deterministic lifecycle state and no public bool lanes around native-support records.

Verification:
- Focused scan found no remaining `UsesGlobalDataVault`, `UsesExternalRing`, or `Dumped` bool field names in `TBDRPipelineSurgeonTypes.cs`.
- Targeted `git diff --check` passed for `TBDRPipelineSurgeonTypes.cs`.
- Compile was not launched because CPU guard reported 100% and `dotnet`/`csc` processes were active.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <TOKEN name="TBDRVertexBudgetVault" fields="NativeArray lanes; VaultGenerationHandle lanes; counts/generation; UsesGlobalDataVaultFlag byte" note="native-support owner record, not NativeArray/binary row" />
    <TOKEN name="TBDRTextureStreamingTracker" fields="Texture2DArray ref; SliceTable NativeArray; SliceTableHandle; counts/generation; UsesGlobalDataVaultFlag byte" note="managed/native tracker, not NativeArray element row" />
    <TOKEN name="TBDRPipelineTelemetryRecorder" fields="Ring NativeArray; WriteIndex int; DumpedFlag byte; UsesExternalRingFlag byte" note="blackbox recorder wrapper around 32-byte telemetry entries" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="70820" use="TBDR vertex budget counters" />
    <BUFFER id="70821" use="TBDR tile warnings" />
    <BUFFER id="70822" use="TBDR transparent quad counters" />
    <BUFFER id="70823" use="TBDR telemetry ring" />
    <BUFFER id="70835" use="TBDR texture streaming slice table" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="no spatial math changed" />
  <COMPLETE_CALLS status="no .Complete() added" />
  <ALIASING status="lifecycle flag packing only; native row layouts and aliases unchanged" />
</SELF_AUDIT>

## DRS/WaterOptics Output Alias Tightening

What was wrong:
- Bilateral DRS and WaterOptics jobs already used explicit DTO layouts and `[NoAlias]`, but several output-only native lanes lacked `[WriteOnly]`.
- This left producer-only GPU constant and telemetry lanes less explicit than the actual dataflow.

What was done:
- Marked `GenerateMockDrsStateJob.MockState` as `[WriteOnly, NoAlias]`.
- Marked `CalculateUpscalerParamsJob.Parameters` and `Telemetry` as `[WriteOnly, NoAlias]`.
- Marked `GenerateMockWaterOpticsJob.Output` and `CopyWaterOpticsToMappedBufferJob.Destination` as `[WriteOnly, NoAlias]`.

Cinematic Cheats used:
- Preserved the shader-side DRS/ringing and spectral-water presentation fakes. No CPU reconstruction pass, no per-pixel managed loop, and no gameplay truth route was added.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us per render parameter cadence; primary gain is stricter Burst write/alias metadata.

Verification:
- Source scan confirms the patched fields carry `[WriteOnly, NoAlias]`.
- No-index diff check for the untracked rendering files reported only line-ending warnings.
- Compile was not launched because CPU guard reported 100% with no dotnet/csc/VBCSCompiler process active.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="UpscalerParamsDTO" size="32" fields="ResolutionParams@0 float4; FilterParams@16 float4" />
    <DTO name="UpscalerTelemetryEntry" size="64" fields="FrameIndex@0 uint; Flags@4 uint; scalar lanes@8..28; ResolutionParams@32 float4; FilterParams@48 float4" />
    <DTO name="WaterOpticsDTO" size="64" fields="Absorption@0 float4; Scattering@16 float4; DirectionalLight@32 float4; QualityDepth@48 float4" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="DRS owner handles" use="existing Bilateral DRS Vault handles for parameters, tuning, telemetry, profiles, mock state" />
    <BUFFER id="WaterOptics owner handles" use="existing WaterOptics Vault handles for params, tuning, profiles, telemetry, csv scratch" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="no spatial math changed" />
  <COMPLETE_CALLS status="no .Complete() added" />
  <ALIASING status="output-only NativeArray lanes now carry [WriteOnly, NoAlias]; telemetry cursor intentionally remains read/write" />
</SELF_AUDIT>

## SeedShip Anomaly Job Alias Tightening

What was wrong:
- `SeedShipMockAupRebaseJob` and `SeedShipAnomalyFieldJob` marked native lanes `[NoAlias]` but did not declare output-only or read-only access for command/telemetry/rebase buffers.
- Burst had less proof than the actual dataflow: the anomaly field state is read-write, but glitch/HUD/thermal/telemetry outputs are producer-only and rebase input is consumer-only.

What was done:
- Added `[WriteOnly, NoAlias]` to mock rebase output.
- Added `[WriteOnly, NoAlias]` to glitch command, HUD signal, thermal source, and telemetry output lanes.
- Added `[ReadOnly, NoAlias]` to the field-job rebase input lane.
- Left field, tuning, globals, and leviathan state as read-write because they are explicitly read and then mutated.

Cinematic Cheats used:
- Preserved the deterministic anomaly-field and shader-command fake. No heavy field solver, collider volume scan, or per-entity physics effect was introduced.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: below 1 us per anomaly cadence; primary gain is stricter alias/write proof for non-overlapping output lanes.

Verification:
- Focused source scan confirms the patched SeedShip native lanes carry `[ReadOnly]` or `[WriteOnly]` where proven.
- Targeted `git diff --check` passed for `SeedShipAnomalyJobs.cs`.
- Compile was not launched because CPU guard reported 80% with no dotnet/csc/VBCSCompiler process active.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <LANE name="SeedShipMockAupRebaseJob.RebaseSignals" access="WriteOnly NoAlias" />
    <LANE name="SeedShipAnomalyFieldJob.GlitchCommands" access="WriteOnly NoAlias" />
    <LANE name="SeedShipAnomalyFieldJob.HudSignals" access="WriteOnly NoAlias" />
    <LANE name="SeedShipAnomalyFieldJob.ThermoSources" access="WriteOnly NoAlias" />
    <LANE name="SeedShipAnomalyFieldJob.RebaseSignals" access="ReadOnly NoAlias" />
    <LANE name="SeedShipAnomalyFieldJob.Telemetry" access="WriteOnly NoAlias" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="SeedShip owner handles" use="existing SeedShip anomaly Vault lanes; no new buffers allocated" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="existing double3 player/epicenter subtraction path unchanged" />
  <COMPLETE_CALLS status="no .Complete() added" />
  <ALIASING status="producer-only and consumer-only NativeArray lanes now carry explicit attributes; read-write state lanes left unrestricted" />
</SELF_AUDIT>

## Submarine Ballast And Docking Quality Truth Detachment

What was wrong:
- `SubmarineAutoLevelBallastController` let low hardware math LOD alter authoritative tank distribution, flood cadence, PID torque gain, max torque, maelstrom sampling, dynamic flood drag tensor, and tail-heavy fluid impulse emission.
- `VehicleDockingModule` used `GlobalRegistry.ScalabilityTier` to choose docking spline math, and low-tier evaluation resampled/interpolated poses on a separate cadence from higher tiers.
- `DockingAutopilotMath.ResolveDockingProgress01` could switch between inertial and zero-jerk Hermite progress based on math LOD and system stress, making docking pose time a hardware/stress artifact.

What was done:
- Removed tier-derived ballast/PID/flood branches. Ballast now always applies four-tank authority, flood solve cadence is fixed, PID gains/max torque are unscaled, maelstrom sampling receives the canonical full approximation byte, flood drag tensor is always available when flood mass exists, and fluid impulse emission is no longer suppressed by math LOD.
- Removed docking scalability-tier lookup, low-tier spline resampling fields, low-tier helper methods, and stress-gated progress branching. Docking now writes `DockingAutopilotMath.AuthoritativeMathLod` into the existing explicit spline DTO and uses one canonical inertial progress path.
- Kept existing explicit DTO layouts and Vault/service routes intact; no new direct sibling assembly dependency was introduced.

Cinematic Cheats used:
- Preserved the existing magnetic docking spline and wake/flow visual path instead of adding heavier physics simulation. Quality should buy wake/bubble/shader richness outside authority, not alter docking or submarine motion.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: 0 us speed claim. This pass intentionally spends the same deterministic authority math on all hardware to remove rollback divergence.

Verification:
- `rg` found no `lowMath`, `LowTier`, `lowTier`, `ResolveLowMath`, `ScalabilityTier`, `GlobalRegistry.ScalabilityTier`, `ResolveDockingMathLodByte`, `IsLowDockingMathTier`, `ResolveSystemStress01`, `HomeostasisHermite`, `_lowTier`, `torqueScale`, or `AdvanceMathLod` hits in the three patched files.
- Targeted `git diff --check` passed for `SubmarineAutoLevelBallastController.cs`, `VehicleDockingModule.cs`, and `DockingAutopilotService.cs`; only line-ending warnings were reported.
- Compile was not launched because CPU guard reported 99.81% with no dotnet/csc/VBCSCompiler process active.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="SubmarineAutoLevelBallastController.PidJobOutput" size="80" fields="TorqueWorld@0 float3; MaelstromAcceleration@12 float3; Integral@24 float3; Error@36 float3; Derivative@48 float3; IntegralWindup@60 float; Flags@64 uint; pad@68 uint; pad@72 ulong" />
    <DTO name="SubmarinePidTelemetryEntry" size="128" fields="Frame@0 int; StateHash@4 uint; Flags@8 uint; IntegralWindup@12 float; SystemStress01@16 float; float3 lanes@20..103; BallastWaterMassKg@104 float; DynamicFloodWaterMassKg@108 float; DynamicFloodAngularDragMultiplier@112 float; CriticalFloodActive@116 byte; pad@117 byte; pad@118 ushort; pad@120 ulong" />
    <DTO name="ActiveSplineData" size="144" fields="P0@0 double3; P1@24 double3; P2@48 double3; P3@72 double3; TargetForward@96 float3; TargetUp@108 float3; OwnerHash@120 uint; RequestId@124 uint; DurationSeconds@128 float; Progress01@132 float; MathLod@136 byte; State@137 byte; Flags@138 byte; Reserved@139 byte; ReservedTail@140 uint" />
    <DTO name="DockTelemetryEntry" size="128" fields="Frame@0 int; State@4 byte; HasPower@5 byte; HasRelativeAup@6 byte; Reserved@7 byte; scalar lanes@8..19; Position@24 float3; SplineTargetPosition@36 float3; CommandVelocity@48 float3; FlowVelocity@60 float3; Rotation@72 float4; GridX@88 long; GridY@96 long; GridZ@104 long; OwnerHash@112 uint; RequestId@116 uint; RuntimeFlags@120 uint; ReservedTail@124 uint" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="SubmarineAutoLevelBallastController existing handles" use="ballast fill, tank positions, PID output, flood mass output, telemetry, room water levels, room volumes, room local AUPs" />
    <BUFFER id="DockingAutopilot existing service handle" use="ActiveSplineData slots and sample path; no new buffer ID created" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="existing double3 docking spline endpoints and flood pivot math preserved; no absolute float route added" />
  <QUALITY_TRUTH status="hardware tier no longer changes ballast, PID, flood, maelstrom, or docking pose authority" />
  <DEPENDENCIES status="no direct sibling assembly dependency added; existing registry/service caching preserved" />
  <COMPLETE_CALLS status="no .Complete() added; existing DispatcherJobSwap completion windows preserved" />
  <ALIASING status="existing [ReadOnly, NoAlias] maelstrom and mass-solver lanes preserved" />
</SELF_AUDIT>

## Fauna Path And Tool Kinematics Quality Truth Detachment

What was wrong:
- `FaunaSteeringEngine` and `CreatureUtilityBrain` read hardware quality tier/math precision to decide apex smooth steering and cognition steering flags.
- `FunnelSmoothingJob` used `MathLod` and `Stressed` to shrink portal lookahead, changing waypoint output under performance pressure.
- `ToolKinematicsRuntime` latched a low LOD and emitted a low-tier snap trigger; `ToolKinematicsContracts` then skipped full IK, recoil, pivot compensation, and SDF raymarch depth under stress.

What was done:
- Fauna apex steering is now species-role based: leviathan/apex behavior gets its authored steering path on every device, and smaller fauna keep the existing cheaper dominant-axis path because that is species behavior, not hardware pressure.
- Path funnel now records the compatibility result byte as Ultra and uses one authoritative lookahead cap.
- Tool kinematics no longer emits low-tier snap triggers, full IK always runs, SDF raymarch hit truth always uses the canonical maximum step budget, recoil/pivot compensation always runs, and beam ring sides scale as a visual-only continuous curve from stress.
- Added `[ReadOnly, NoAlias]` and `[WriteOnly, NoAlias]` metadata to the patched tool kinematics job lanes where access mode is proven.

Cinematic Cheats used:
- Kept the tool beam mesh and sparkle path as presentation fakes. The expensive-looking laser/beam visual can shed ring sides continuously while SDF hit truth stays invariant.

Exact Microseconds saved:
- Verified profiler measurements: none.
- Static estimate: 0 us speed claim for truth paths. Visual beam mesh cost still falls with stress because ring sides resolve from a continuous 4..8 curve.

Verification:
- Fauna targeted scan found no `GlobalRegistry.ScalabilityTier`, `TargetMathPrecision`, `MathPrecisionLevel`, `HomeostasisBrain.GlobalQualityWeight`, `RefreshScalabilityRouteCold`, `ResolveMathLodDirection`, `MoveTowardsMathLod`, `HighTierApex`, or `ApexSmoothSteeringTierMask` hits in the patched fauna files.
- Tool targeted scan found no active `_latchedLod`, `_pendingLod`, `_lodInitialized`, `ResolveLod`, or `FlagLowTierFallback` path; legacy `LowTierSnap` constants remain only as ABI bits and are cleared by the producer job.
- Path funnel targeted scan confirms no `ResolveEffectiveMathLod` remains and lookahead is a fixed authoritative function.
- Targeted `git diff --check` passed for fauna, path funnel, and tool kinematics files with only line-ending warnings.
- Compile was not launched because active `csc`/`dotnet` processes were present and CPU load reported 100%.

<SELF_AUDIT>
  <BYTE_LAYOUTS>
    <DTO name="ToolKinematicsFrameInputDTO" size="96" fields="CameraAup@0 double3; ControllerLocalPosition@24 float3; ControllerRotation@36 quaternion; ShoulderLocalPosition@52 float3; PoleLocalDirection@64 float3; DeltaTime@76 float; SystemHealthIndex@80 float; TriggerFlags@84 uint; FrameIndex@88 uint; pad@92 uint" />
    <DTO name="ToolIkOutputDTO" size="64" fields="Shoulder@0 float3; Elbow@12 float3; Wrist@24 float3; UpperRotation@36 quaternion; Flags@52 uint; ComputeMicrosecondsEstimate@56 float; pad@60 uint" />
    <DTO name="ToolKinematicsTelemetryEntry" size="64" fields="FrameIndex@0 uint; ToolHash@4 uint; ToolHeatLevel@8 float; EnergyRemaining@12 float; HitDistance@16 float; RaymarchStepCount@20 int; IkComputeTimeMicroseconds@24 float; Flags@28 uint; ToolLocalPosition@32 float3; HitPoint@44 float3; MaterialHash@56 uint; pad@60 uint" />
    <DTO name="PathFunnelResult" size="32" fields="WaypointCount@0 int; ProcessedPortalCount@4 int; Iterations@8 int; Flags@12 uint; Status@16 byte; MathLod@17 byte; BlockedCellIndex@18 ushort; CorridorHash@20 uint; Frame@24 uint; Reserved0@28 uint" />
  </BYTE_LAYOUTS>
  <VAULT_BUFFER_IDS>
    <BUFFER id="ToolKinematics existing handles" use="states, frame inputs, hit results, IK outputs, recoil states, tuning, screen exports, telemetry, signal lanes, beam vertices/counts, pose outputs" />
    <BUFFER id="PathFunnel existing owner handles" use="path funnel runtime result/waypoint/AUP buffers; no new buffer ID created" />
  </VAULT_BUFFER_IDS>
  <GC_HOT_PATHS status="no managed allocation added" />
  <AUP status="tool AUP local conversion and path AUP waypoint conversion unchanged; no absolute float route added" />
  <QUALITY_TRUTH status="hardware/stress no longer changes fauna steering authority, path funnel output, tool IK, recoil, or SDF hit truth" />
  <SCALABILITY status="visual-only tool beam ring sides scale continuously from stress-derived quality" />
  <COMPLETE_CALLS status="no .Complete() added; existing dispatcher fence/finalization path preserved" />
  <ALIASING status="tool producer-only lanes now carry [WriteOnly, NoAlias]; read-only lanes carry [ReadOnly, NoAlias]" />
</SELF_AUDIT>

<SELF_AUDIT id="SHINOBU_SYSTEMIC_SURGEON" pass="PredatorCognitionAlphaLeviathanAuthority">
  <WHAT_WAS_WRONG>
    Predator cognition used global quality, scalability tier, frame pressure, and high-tier steering flags to alter AI cadence, mesofauna perception/tuning, predator steering, and Alpha Leviathan stalk math. These are gameplay/encounter facts, not presentation budget.
  </WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>
    `PredatorCognitionDomain` now uses canonical cognition quality 1.0 for mesofauna quality inputs, disables retinal low-cadence mode, removes the unused scalability-tier registry poll, and forces smooth predator steering authority. `LeviathanStalkJob` now uses precision math LOD independent of `SystemStress01` and `MathLodSurvival`.
  </WHAT_WAS_DONE>
  <CINEMATIC_CHEATS>
    Dear Lie lane preserved only as presentation: predator silhouettes, SDF visual overkill, shader/noise payloads, and optional telemetry can still be scaled later without changing cognition facts.
  </CINEMATIC_CHEATS>
  <MICROSECONDS_SAVED estimate="0">
    No speed claim. This pass spends canonical AI work to remove hardware-divergent behavior; one hot `GlobalRegistry.ScalabilityTierProfileByte` read was removed from initialization.
  </MICROSECONDS_SAVED>
  <VERIFICATION>
    `rg` confirms no remaining `GlobalRegistry.ScalabilityTierProfileByte`, `_scalabilityTierProfileByte`, `SystemDispatcher.HomeostasisPressureLevel`, or frame-delta pressure reads in `PredatorCognitionDomain.cs`. `rg` confirms `LeviathanStalkJob.cs` no longer reads `SystemStress01` or `MathLodSurvival`. Targeted `git diff --check` passed with line-ending warnings only.
  </VERIFICATION>
</SELF_AUDIT>

<SELF_AUDIT id="SHINOBU_SYSTEMIC_SURGEON" pass="VolcanicDrillSaveAuthority">
  <WHAT_WAS_WRONG>
    Volcanic force direction/debris lift, deployable drill extraction catch-up, and save macro database compaction tier all changed with hardware quality/tier. These paths mutate force, inventory, macro persistence, or save compaction facts.
  </WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>
    `VolcanicUpdraftDirector` now uses canonical quality for vent turbulence and mock debris lift while retaining real quality for visual wake/flow payloads. `DeployableSdfDrillRuntime` pins mining authority to Ultra math LOD and removes the cold scalability-tier read. `SaveManager` pins macro database compaction tier to canonical Middle.
  </WHAT_WAS_DONE>
  <CINEMATIC_CHEATS>
    Volcanic curl, flow density, visual overkill, particle budgets, and presentation debris quantity remain visual budget lanes. Drill visual carve density should be rebuilt as non-persistent presentation state if needed.
  </CINEMATIC_CHEATS>
  <MICROSECONDS_SAVED estimate="0">
    No speed claim. Registry tier reads were removed from drill dependency caching and save compaction tier resolution; authority math intentionally remains canonical.
  </MICROSECONDS_SAVED>
  <VERIFICATION>
    Targeted `rg` found no `GlobalRegistry.ScalabilityTier` in `DeployableSdfDrillRuntime.cs` or `SaveManager.cs`. Targeted `git diff --check` passed for volcanic, drill, and save files with line-ending warnings only.
  </VERIFICATION>
</SELF_AUDIT>
