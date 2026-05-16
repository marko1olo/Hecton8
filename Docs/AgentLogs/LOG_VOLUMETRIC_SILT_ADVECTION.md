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

## Entry 2026-05-16 - Multiplatform H-Phi Inquisition

What was wrong:
- The renderer still owned two persistent native data lanes: wake job result and 300-frame blackbox telemetry.
- Marine-snow CPU/GPU structs used default sequential packing or `Pack=4`, which is not acceptable for Quest/ARM64 ABI paranoia.
- The touched VFX budget row still used default sequential layout.

What was done:
- Added `BufferID.MarineSnowWakeJobResult` and `BufferID.MarineSnowTelemetryRing`.
- Moved wake-job output and blackbox telemetry ownership to `GlobalDataVault`; renderer now stores only vault handles and invalidates on compaction.
- Added explicit `Pack = 1` and `Size` to marine-snow particle/frame/wake/telemetry structs and `VfxComputeParticleBudget`.
- Added `UnsafeUtility.SizeOf` ABI checks before runtime tick.
- Re-ran platform scans: no persistent native arrays, `H8Memory.Allocate`, `Update`/`LateUpdate`/`FixedUpdate`, `string.Format`, scene discovery, or `distance()` calls in touched marine-snow files.

Cinematic Cheats used:
- Low/Toaster: 8,000 particles, radial wake fake, no 3D flow texture, no curl, no silt collision.
- High/Ultra: 100,000 particles, abyssal 3D flow texture, curl fake, headlight emission boost.
- Blackbox writes only on fixed telemetry ring; disk dump only on fault, avoiding normal Steam Deck/MicroSD pressure.

Exact Microseconds saved:
- Renderer-owned native allocation churn/leak surface removed: estimated 0 us steady-state frame gain, operational risk reduction only.
- ABI validation is cold-path; expected frame cost 0 us after state is ready.
- Existing visual-fake savings remain the prior estimates: 240 us CPU-side avoided versus CPU particle/discovery paths before profiler proof; GPU collision/noise wins still require capture.

Validation:
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies /p:UseSharedCompilation=false /m:1` still fails on unrelated Lockstep/SubmarineFluid/Ecosystem errors. Filtered log contains no `HectonMarineSnowRenderer.cs`, `H8Memory.cs`, or marine-snow BufferID errors.
- Shader static audit: particle kernels use 64-thread groups, clear kernels use 64/1, no `distance()`, no wave intrinsics/groupshared use, guarded `rsqrt`/`rcp` paths.
