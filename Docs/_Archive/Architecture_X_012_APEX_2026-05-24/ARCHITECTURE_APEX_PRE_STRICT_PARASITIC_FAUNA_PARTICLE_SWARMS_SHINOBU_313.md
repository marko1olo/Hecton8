# [ARCHIVE] Pre-Strict Architecture Snapshot

Date: 2026-05-24
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Original: Docs/ARCHITECTURE/PARASITIC_FAUNA_PARTICLE_SWARMS_SHINOBU_313.md
Rule: historical snapshot only; not active doctrine.

# PARASITIC_FAUNA_PARTICLE_SWARMS_SHINOBU_313

Owner: SHINOBU_313 / VFX presentation.

## Route

- Thermal producers publish `ThermalSourceSignal`; parasite VFX stages those contract signals into `ShinobuParasiteTargetCandidates` in `GlobalDataVault`.
- `ExtractParasiteTargetsJob` scores the staged candidates and `SelectTopParasiteTargetsJob` ranks the top 16 by that score before localizing AUP targets by subtracting camera `double3` AUP before the float cast.
- Camera AUP comes only from cached `IPlayerRuntimeContext.TryGetPlayerPoseSnapshot`; the runtime no longer reconstructs camera AUP from scene `Transform` state or a local origin shadow, and active compute requires a resolved `renderCamera`.
- Top targets are staged as `ParasiteTargetDTO[16]` and uploaded through a ping-pong `GraphicsBuffer.LockBufferForWrite` pair.
- Camera-relative draw state is uploaded through ping-pong `H8ParasiteDrawParams[1]` buffers, so shader expansion adds camera world position after compute keeps particle math near zero without writing the buffer consumed by the previous draw.
- Per-dispatch compute uniforms are grouped into a single 64-byte `ParasiteFrameParamsDTO` row and uploaded through a ping-pong `GraphicsBuffer`.
- `Hecton_ParasiteSwarm.compute` owns particle init, advection, Dear Lie hull attachment, rebase, cull, and indirect draw args.
- `CS_AdvectParasites` resets non-finite particle state to deterministic dormant positions before integration so cull is not the only NaN defense.
- Shader normalize returns a finite fallback on zero/non-finite vectors, attraction force uses a radius-squared denominator floor, and cull rejects non-finite `Life01`.
- The compute shader uses local `H8FiniteScalar` / `H8Finite3` predicates instead of backend-specific `isfinite()` calls.
- Curl noise and dormant positions use bounded polynomial `H8FastSin` / `H8FastCos` helpers, avoiding native shader trig calls in the per-particle advection path.
- Inactive target slots are skipped before `_H8ParasiteTargets[i]` is read, so low-target frames cannot convert stale/uninitialized target rows into `0 * NaN` acceleration.
- Zero-target frames keep GPU particle state resident, dispatch only the indirect-args clear kernel, and skip advection, cull, draw, and budget-spike telemetry.
- Runtime compute kernel resolution uses `HasKernel` before `FindKernel`; missing kernels take the no-compute telemetry path instead of throwing during `OnEnable`.
- The rebase kernel is mandatory for the active compute path because AUP shifts cannot be drawn from stale particle-local coordinates.
- The serialized parasite material is also mandatory for active compute; missing material takes the no-compute path instead of running GPU work that cannot be drawn.
- `Graphics.DrawProceduralIndirect` renders the surviving visual parasites.

## Authority Fence

Particle buffers, target staging, telemetry, profiles, and scanner summaries are presentation-only.
They are not added to rollback Merkle descriptors or gameplay state rings.
No parasite damage or camera obstruction signal is published by this renderer.
Existing `HullDeformedSignal`, `CameraFrustumSignal`, `ThermalSourceSignal`, and `AupShiftSignal` lanes were verified; this system consumes `ThermalSourceSignal` for visual attraction and `AupShiftSignal` for GPU rebase.
Runtime assembly references only Core/Core.Contracts/Core.Memory plus Unity packages; it does not reference sibling World, Thermodynamics, or KCC runtime assemblies/namespaces.

## Vault Write Discipline

Target extraction acquires explicit Vault write locks for `ShinobuParasiteTargets`, `ShinobuParasiteTargetCandidates`, and `ShinobuParasiteTargetCount` before scheduling Burst writer jobs.
Those locks are released only after the one-frame-late `JobHandle.IsCompleted` fence and `Complete`, or during teardown.
Telemetry ring/cursor writes acquire short per-frame write locks.
No private persistent `NativeArray`, `NativeList`, or `NativeHashMap` owns parasite simulation state.

## Shader Warmup

The compute shader uses fixed kernels and no variant keywords.
Startup resolves kernels, runs the particle init dispatch, binds draw buffers, and touches material pass 0 only when a serialized material is assigned.
Runtime does not call `Shader.Find` or synthesize a fallback `Material`; render resources are asset-owned and missing material fails closed.
Quality changes remain uniforms and buffer counts, not shader permutations.

## Dear Lie

Exact mesh collision is rejected. The compute shader locks particles to a spherical target shell when distance is less than `AttractionRadius`.
Velocity blends toward target velocity, producing hull infestation visuals without triangle tests or CPU physics.

## Scalability

`GlobalQualityWeight` continuously scales particle budget, curl contribution, and dispatch group count.
The hard supported GPU ceiling is 2,000,000 particles; the serialized cold default is 500,000 so mobile does not pay million-particle VRAM by default.
The live budget is additionally clamped to the allocated ping-pong particle `GraphicsBuffer.count`; raising serialized capacity after allocation cannot over-dispatch beyond physical GPU rows.
Low: 5k particles, 64-wide groups, one-octave curl.
Middle: increased density and flow response.
High: higher curl strength and target density.
Ultra: up to configured cap without changing authority, DTO layout, save data, or rollback hash inputs.

## Black Box

`SwarmTelemetryEntry[300]` records frame, target count, particle budget, quality, estimated GPU microseconds, strongest target, state hash, flags, and rebase frame.
It also records target overflow when the staged candidate count exceeds the 16-target GPU envelope.
Fault path dumps a 64-byte little-endian `H8P3` header plus raw telemetry rows to `Docs/AgentLogs/Dump_SHINOBU_313.bin` on target overflow, estimated GPU budget spike, or invalid math.
Header fields include version, header bytes, row stride, row count, write cursor, and payload byte count.

## Legacy Particle Findings

Scoped VFX/Environment scan found no `HectonVFXRuntime`, `LeechAI`, `Bugs`, or `SwarmTarget`.
The VFX `ParticleSystem` hit is `CameraJuiceSystem` speed lines and is unrelated to parasite authority.
Prefab scan evidence found `PFB_Support_Pocket_Hazard.prefab` contains mesh silhouettes named `ParasiteA` and `ParasiteB`, plus three ParticleSystems named `VentBubbleColumn_Secondary`, `VentBubbleColumn_LOD1`, and `VentBubbleColumn_Main`.
The prefab ParticleSystems are vent presentation, not parasite swarm authority, so raw YAML deletion was rejected.

## Timing Discipline

GPU advection consumes a fixed `1/60` visual tick.
The runtime does not feed `Time.deltaTime` or `Time.time` into the compute shader.
Runtime visual phase and telemetry frame IDs advance through a private fixed-step counter that wraps the shader phase through a 4096-tick bounded phase ramp, avoiding large-time precision loss and Unity frame-clock dependency in runtime simulation.
Burst thermal-target scoring is sqrt-free: candidate distance is computed once in double precision, range-checked, then converted to a guarded `rsqrt` distance proxy for the visual ranking score.
The editor scanner still stamps reports with `Time.frameCount`; that stamp is editor-only evidence, not parasite runtime truth.

## Human Tuning

`parasite_behavior_profiles.csv` is parsed through a byte-span CSV route into fixed Vault profile rows.
`AbyssalParasiteTunerWindow` can reload the CSV into Vault without C# recompilation and edits tuning through UI Toolkit controls.
`Biological_Particle_Scanner` regenerates the shared rendering report with DTO, route, shader-safety, scalability, and compile-gate fields preserved.
