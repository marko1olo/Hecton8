# Rationale: OCEAN_CHEMISTRY_ENGINEER

Domain: ENVIRONMENT_ENGINEER / Environment.Fluids  
Target: i3 / MX350 / Unity 6  
Status: PENDING VERIFICATION

## Mandate Basis

The implementation must replace trigger-driven brine with math-plane evaluation. Runtime checks must be allocation-free and compatible with absolute-universe coordinates. Rendering must use shader/post-process fakes rather than extra water meshes. Cross-domain coupling must use GlobalRegistry interfaces or EventBus signals.

## Decisions

### Bootstrap Memory

Problem: The assigned brine prompt is embedded inside a shared batch file with neighboring agent prompts. Mixing directives would create architectural drift.

Solution: Extracted only the OCEAN_CHEMISTRY_ENGINEER XML block from Docs/Tasks/CURRENT_BATCH.md and created this rationale plus Status_OCEAN_CHEMISTRY_ENGINEER.md before code edits.

Rejected Alternatives: Reading the full batch through memory or MCP view was rejected because it risks truncation and neighboring-prompt bleed.

Scalability potential: Low tier keeps behavior deterministic through scalar plane checks; high tier can spend saved cycles on stronger fog/audio treatment.

Hardware Impact: Prevents wasted engineering on irrelevant systems; runtime gain is not measurable, but prevents cross-domain churn on i3/MX350.

### Loop 1: Brine Authority Plane

Problem: Existing brine was authored as visual/hazard data and did not provide an allocation-free density truth for player or buoyancy systems.

Solution: Added `Hecton8.Environment.Fluids.Contracts` for constants/sample structs and `Hecton8.Environment.Fluids` for Burst-safe plane math. `ResourceDistributionDirector` now exposes `TrySampleBrineLayer`, returning absolute plane height, runtime height, 50m cartography sector hash, density multiplier, and toxicity as a blittable sample. `HectonFluidEngine` stores samples in NativeArray SOA lanes.

Rejected Alternatives: A new `BrineManager` singleton was rejected because AGENTS forbids singleton authority. Trigger volumes were rejected because task explicitly required AUP.y plane evaluation and because prefab scans found no brine trigger prefab requiring YAML mutation.

Scalability potential: Low tier pays one scalar plane check and hard fog later. Middle uses the same density truth with soft visual fade. High/Ultra can spend saved CPU on richer post fog, caustic occlusion, and audio muffle without adding real fluid simulation.

Hardware Impact: i3/MX350 avoids collider broadphase and MonoBehaviour trigger dispatch. Expected save is scene-dependent; conservative estimate is 4-12 us/frame in brine-heavy zones versus trigger volumes, with <0.01 us/object inside the Burst buoyancy branch.

### Loop 1: Density Signal Lane

Problem: Player brine entry needed cross-domain broadcast without direct dependencies on audio, gas, AI, or rendering consumers.

Solution: Added `FluidDensityChangedSignal` as a 64-byte NativeQueue lane in `GlobalSignals`. `HectonPlayerMovement` publishes only on state transitions and caches brine submersion seconds locally.

Rejected Alternatives: Per-frame event spam and string event ids were rejected. Direct calls into audio/gas from the signal itself were rejected; concrete consumers remain separate.

Scalability potential: Low tier receives only transition truth. High/Ultra can read the latest signal for richer procedural muffle and visor response without changing player movement.

Hardware Impact: Steady-state cost is 0 us for queue writes. Transition frame enqueue is estimated below 0.6 us on low-end silicon.

### Loop 2: Brine Density And Visual Fake

Problem: Heavy brine needed to affect buoyancy and player control, but real viscous-fluid simulation would exceed the frame-time budget and create unpredictable acceleration.

Solution: `HectonFluidEngine` multiplies density by 3.0 only when runtime Y is below the absolute brine plane after subtracting floating-origin shift. Brine lift is clamped to 9g to prevent runaway upward acceleration. Player swimming uses the same density truth: 40% speed penalty plus higher scalar drag.

Rejected Alternatives: Full fluid viscosity simulation, trigger-based volume membership, and changing Rigidbody mass were rejected. They are either too expensive, non-deterministic under origin shifts, or corrupt authored movement tuning.

Scalability potential: Low uses the same scalar branch and hard fog. Middle/High retain soft fog. Ultra can layer richer color grading/audio muffle because the authority path stays scalar.

Hardware Impact: i3/MX350 branch cost is estimated below 0.01 us/object in the Burst job; player KCC penalty is below 0.02 us/fixed tick. The removed brine mesh path saves draw calls for generated pools.

### Loop 2: Shader Plane And Caustic Kill

Problem: Generated brine meshes and fog volumes would add draw calls and still fail to give visor/camera-space density feedback.

Solution: `HectonVisorUberPost` samples scene depth, reconstructs world space, and applies green-yellow fog when `worldY < _BrineHeightY`. Low tier uses `_BrineFogHardClip` for a hard plane. `Hecton_CoreLit` returns zero projected caustics under the active brine plane.

Rejected Alternatives: Physical brine water meshes, CPU-side matrix allocation/upload, or extra local fog volumes. The existing URP post path already has the right visual authority.

Scalability potential: Low = hard clip plane. Middle = soft plane fade. High = harsh depth fog. Ultra = same cheap truth with stronger post/audio layering.

Hardware Impact: One depth sample in the existing post pass replaces generated mesh/fog draw calls. Expected low-end win is 20-80 us in brine-rich scenes depending on prior pool count; shader cost is GPU-side and quality-tier gated.

### Loop 3: Audio, Toxicity, And Origin Safety

Problem: Brine submersion needed sensory and survival consequences without adding trigger components, per-frame managed events, or direct cross-domain ownership.

Solution: `HectonPlayerMovement` exposes brine state and density multiplier as scalar properties. Audio raises the existing abyssal low-pass target when the player is inside brine. Toxicity routes through `IGasDynamicsSolver.TryApplyPlayerRoomCarbonDioxideEquivalentPressure`, flooring the active room at standard CO2 +10 kPa while submerged instead of accumulating pressure every fixed tick. AUP checks continue to subtract `HectonFloatingOrigin.CurrentTotalOffset.y` before comparing runtime Y to absolute brine planes. Low tier publishes `_BrineFogHardClip = 1` so the post shader uses a hard plane.

Rejected Alternatives: A new audio filter component was rejected because it allocates/duplicates an existing DSP lane. Additive CO2 injection was rejected because it becomes frame-rate dependent and can detonate room pressure. Runtime-stored brine heights were rejected because floating-origin shifts would invalidate sectors.

Scalability potential: Low = scalar plane check, hard fog, reused low-pass. Middle = soft fog plus same toxicity truth. High = harsher depth fog and audio density response. Ultra = visual/audio overkill still rides the same deterministic plane authority.

Hardware Impact: i3/MX350 pays one scalar audio mix and one gas-room floor write while submerged, estimated below 0.05 us/fixed tick combined. No new managed allocations, no new physics queries, no extra audio components.
