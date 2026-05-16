# LOG: VOLUMETRIC_SILT_ADVECTION

## Entry 2026-05-16

What was wrong: The user-provided prompt ID did not exist in `CURRENT_BATCH.md`; the authoritative matching tag is `VOLUMETRIC_SILT_ADVECTION`.
What was done: Created status, rationale, and log files under the actual batch ID. Read task XML, domain file, AGENTS, and relevant mandates before source edits.
Cinematic Cheats used: Presentation-only silt; no SDF collision; low-tier radial drift instead of expensive flow/noise.
Exact Microseconds saved: PENDING VERIFICATION. No profiler capture yet; no runtime code edited yet.

## Entry 2026-05-16 - Marine Snow GPU Silt Advection

What was wrong:
- Marine snow was visually too clear and not coupled to submarine throttle wake.
- Existing allocation bootstrap wrote particle positions on CPU and uploaded both ping-pong buffers.
- Silt could still enter scene-depth collision on higher tiers, wasting ALU for presentation-only debris.
- There was no VFX-local 300-frame blackbox for active silt state.

What was done:
- Added GPU `InitializeParticles` kernel and removed CPU particle position seeding from `HectonMarineSnowRenderer`.
- Added `SiltParticle` compute/render shader contracts, `_DynamicWakes` / `_DynamicWakeVectors` consumption, `_AupShiftOffset`, max speed clamp, 50m wrap, headlight cone boost, high-tier abyssal 3D texture plus curl fake, and low-tier radial wake fake.
- Exposed existing FluidEngine dynamic wake GPU payload through `TryGetDynamicWakeGpuPayload`; no duplicate VFX wake buffer was created.
- Added Burst `BuildVehicleWakeSignalJob` fed by `VehicleCommandSignal(Throttle)` and routed wake impulses through `FluidImpulseSignal`.
- Switched rendering to `Graphics.RenderMeshIndirect` quads and added URP motion-vector pass.
- Added 300-frame `MarineSnowTelemetryEntry` NativeArray ring, `ActiveSiltCount` telemetry publish, and `Docs/AgentLogs/Dump_VOLUMETRIC_SILT_ADVECTION.bin` dump path for non-finite state.
- Added homeostasis stress shed: `SystemStress01 > 0.8` forces low-tier active count/scalability.

Cinematic Cheats used:
- MX350: 8,000 particles, radial wake vector, no 3D flow texture, no curl noise, no silt collision.
- Middle: bounded flow path with existing buffers.
- High/Ultra: 100,000 particles, 3D abyssal flow texture, fake curl swirl, headlight cone emission multiplier.
- Silt clips through floors; density sells the effect instead of SDF truth.

Exact Microseconds saved:
- Estimated CPU-side savings from eliminated polling/discovery/CPU particle update paths: 240 us per heavy wake frame before profiler proof.
- Removed 64B x 100,000 particle cold bootstrap upload path: ~6.4 MiB avoided on high/ultra initialization.
- Silt collision skip saves depth/SDF ALU per silt particle; exact GPU us requires capture after the unrelated project compile wall is cleared.

Validation:
- `git diff --check` clean except existing CRLF normalization warnings.
- Static scans: no `GameObject.Find`, no `FindObjectOfType`, no compute/render shader `distance()`, particle kernels use 64 threads.
- Unity default, DX12, and Vulkan batchmode runs were attempted. All failed before VFX shader/API validation because of pre-existing C# errors in Audio/Physics/Editor assemblies. No touched VFX file was named in those logs.
