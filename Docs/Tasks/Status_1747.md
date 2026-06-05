# Status 1747 - Ambient Particle And Marine Snow Scatter Director

Agent ID: 1747
Scope: Ambient particulate, marine snow, volumetric silt, GPU particle budgets, static proof packet.

## Checklist

- [DONE] Task 01 SCATTER_MANAGER_STATIC_AUDIT - Scoped owners were mapped. Actual ambient marine-snow owner is `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs`; `WorldProceduralScatterDirector.Bridge.cs` was not present; `WreckageScatterManager.cs` is not the ambient snow owner.
- [DONE] Task 02 PARTICLE_SYSTEM_API_ALIGNMENT_INSPECTION - Existing route uses GPU buffers, compute, and indirect procedural draw. Legacy Shuriken motes remain fallback only when GPU marine snow is unavailable.
- [DONE] Task 03 FLOW_FIELD_MATHEMATICAL_MODELING - Existing compute path resolves flow field, dynamic wakes, propwash events, shallow-water field data, curl-noise fake, and quality-gated collision lanes.
- [DONE] Task 04 TURBIDITY_EMISSION_SCALING_MATH - `HectonUnderwaterVisuals` derives particulate density from depth, biome turbidity, darkness, daylight clarity, bottom silt, submerge impulse, ecology multiplier, adaptive scale, and frame-time emission scale.
- [DONE] Task 05 GLOBAL_REGISTRY_HOT_POLLING_DETECTION - Scoped marine-snow owner has no `GlobalRegistry.Get<` hits. Registry property reads are cached in enable/hot-swap/cold paths.
- [DONE] Task 06 COMPACTION_FENCE_VULNERABILITY_SCAN - Marine-snow DataVault binding checks `IsCompactionFenceActive`, clears readiness, and fails closed before resolving native buffers.
- [DONE] Task 07 TELEMETRY_AND_REPORTING_ARCHITECTURE - JSON report path defined as `Docs/Reports/AMBIENT_VFX_DIRECTOR_REPORT_1747.json`.
- [NOT APPLICABLE] Task 08 SCATTER_MANAGER_PURIFICATION - No verified global ambient CPU particle hot path was found to replace. Existing GPU route is already the stronger owner.
- [DONE] Task 09 CAMERA_BOUND_EMITTER_IMPLEMENTATION - Existing renderer draws from a camera-centered shell and camera-local bounds via `Graphics.DrawProceduralIndirect`; no scene-wide snow globe owner was added.
- [DONE] Task 10 ZERO-GC_TURBIDITY_SYNC - Existing density handoff is scalar GPU state, not CPU particle-array mutation. Runtime 0 B/frame remains profiler-pending.
- [DONE] Task 11 PROCEDURAL_FLOW_FIELD_INJECTION - Existing compute shader injects flow, wakes, propwash, shallow-water velocity, and synchrony parameters into particle velocity.
- [DONE] Task 12 FLIPBOOK_MATERIAL_AND_NORMAL_BINDING - Existing shader uses baked mask and normal atlases with 8x8 atlas defaults and flipbook phase over particle life/time.
- [DONE] Task 13 DEPTH_FADE_AND_CULLING_MATH - Existing shader uses alpha-test queue, AlphaToMask, clipped dither coverage, distance fade, visible-index buffer, camera shell wrap, and quality-gated depth/SDF collision.
- [DONE] Task 14 EVENT_DRIVEN_SILT_BURSTS - Existing path harvests procedural wake sources and propwash events into GPU-driven silt response. Specific collision-impact screenshot/profiler proof is pending.
- [NOT APPLICABLE] Task 15 ZERO-GC_DAMAGE_SIGNALS_FOR_VFX - Damage-router/toxic-vent signal work is cross-domain and no verified ambient-marine-snow defect required edits.
- [DONE] Task 16 DISABLE_UNUSED_SYSTEMS_DURING_CLEAR_WATER - Existing renderer early-outs when effective density is below epsilon and publishes zero sonar/fog globals.
- [DONE] Task 17 DRY_RUN_VERIFICATION_EXECUTION - Edge-case checklist below.
- [DONE] Task 18 CONTINUOUS_QUALITY_SCALING_INTEGRATION - Existing route uses continuous `GlobalQualityWeight`, Homeostasis pressure, render scale, VRAM pressure, kill-switch masks, and quality-gated shader lanes. Stale task 500/5000 hard caps were rejected in favor of the existing continuous budget catalog.
- [NOT APPLICABLE] Task 19 BATCHED_COMPILATION_AND_SYNTAX_ASSERTION - No C# source change was made; no compile-impacting work required a build.
- [DONE] Task 20 EXPLICIT_PARTICLE_COUNT_VALIDATION_GATE - Existing budget catalog bounds marine snow at 8000/14336/100000/100000 before pressure, VRAM, density, and kill-switch compression.
- [DONE] Task 21 COMPACTION_FENCE_RACE_CONDITION_AUDIT - Existing native-state route backs off if DataVault compaction is active and retries after the rebind cadence.
- [PENDING VERIFICATION] Task 22 ZERO_GC_ALLOCATION_PROFILER_MOCK - Static scan found no `GetParticles`/`SetParticles` in `HectonMarineSnowRenderer`; runtime profiler allocation proof was not captured.
- [PENDING VERIFICATION] Task 23 FILL_RATE_LIMIT_TESTING - Static shader proof shows AlphaTest, AlphaToMask, dither coverage, and visible-index culling; Frame Debugger/fill-rate measurement was not captured.
- [DONE] Task 24 AUTOMATED_METRIC_VALIDATOR_REPORT - Required JSON report written with static evidence and explicit runtime-proof gaps.

## Edge-Case Checklist

- Rapid descent: camera-shell wrap keeps particles local to the player instead of leaving a world-space trail behind the submarine.
- Strong current: GPU velocity blends toward resolved flow instead of teleporting particle positions.
- Clear photic water: density scalar can drop below epsilon, skipping simulation/render work and clearing sonar/fog particle globals.
- Silt basin or storm/deep route: biome turbidity, darkness, bottom silt, and weather/flow synchrony raise density and motion without changing gameplay truth.
- Weak device: continuous budget compression lowers active count, flow/depth quality lanes, render-scale pressure, and VRAM response while preserving authored textured particles.
- Ultra device: same DTO layout and authority route, but higher quality enables larger budget, more frequent flow/depth detail, normal atlas lighting, and overkill particle density.
- Runtime `0 B/frame`, actual GPU timing, screenshot quality, and Frame Debugger overdraw proof remain pending because no Unity play/profiler capture was run.
