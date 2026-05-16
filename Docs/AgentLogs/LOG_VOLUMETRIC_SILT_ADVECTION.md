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

## Entry 2026-05-16 - VFX Domain Data Sovereignty Pass

What was wrong:
- `CameraJuiceSystem` still owned a persistent 300-frame telemetry `NativeArray` with `Pack=4`.
- `MaterialDecayRuntime` still owned a persistent blackbox `NativeArray`, had an unpacked state struct, and allocated a temporary native pixel array for fallback rust detail generation.
- `CarveDebrisComputeRenderer` used DataVault, but stored five resolved `NativeArray` views as persistent fields, making the renderer stateful storage owner.

What was done:
- Added `CameraJuiceTelemetryRing = 272` and `MaterialDecayBlackBox = 273` BufferIDs.
- Converted camera juice telemetry and material decay blackbox to GlobalDataVault handle leases.
- Converted carve debris persistent storage fields to `VaultBufferHandle<T>` and resolved scoped NativeArray views only when running the operation/job.
- Changed material decay fallback rust atlas fill to use `Texture2D.GetRawTextureData<Color32>()`; no temporary `new NativeArray` remains.
- Added explicit `Pack = 1` / `Size` and runtime ABI checks for the camera/material VFX telemetry structs.

Cinematic Cheats used:
- Existing Toaster mode remains fake-first: low debris capacity, cheap radial/triangle/dot math, no extra per-frame I/O.
- High/Ultra retains the expensive-looking path where it matters: denser debris, flow/SDF hooks, marine-snow 3D flow/curl, headlight emission.

Exact Microseconds saved:
- 0 us measured. No profiler capture was run.
- Estimated steady-frame change: 0 to negligible. These edits buy data sovereignty, compaction safety, and cold allocation reduction, not a proven frame-time win.
- One temporary 512x512 `NativeArray<Color32>` cold allocation was removed from material fallback atlas generation.

Validation:
- VFX static scan returned no persistent `NativeArray` fields, no direct `new NativeArray`, no `H8Memory.Allocate`, no unpacked VFX structs, no standard Unity update loops, no `string.Format`, no scene discovery, and no legacy `EventBus` hit under `Assets/_Project/Scripts/VFX`.
- Shader scan found no `distance()`, no wave intrinsics, no groupshared/SV_Group path in the marine-snow shaders; thread groups remain 64/64/1.
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly` returned EXIT=0.
- Unity DX12/Vulkan platform validation remains PENDING VERIFICATION; project-level player validation is still separate from this scoped compile.

## Entry 2026-05-16 - Marine Snow Dispatch Cap Pass

What was wrong:
- High/Ultra marine-snow budget is 100,000 particles. At 64 threads/group, a direct particle dispatch is 1,563 groups.
- Sonar glow and fog density clear kernels can also exceed 512 total groups at high render scales.
- Cold allocation comments still claimed the old 32,768 particle / 2.0 MiB cap.

What was done:
- Added `_MarineSnowDispatchOffset` to `Hecton_MarineSnow.compute`.
- Chunked `CSMain`, `InitializeParticles`, and `AccumulateSonarGlow` in `HectonMarineSnowRenderer` so each dispatch call is <=512 groups.
- Added `_MarineSnowDispatchTileOffset` and chunked sonar/fog 2D clears under the same <=512 group policy.
- Corrected marine-snow buffer comments to 100,000 * 64B = 6.4 MiB per particle buffer and corrected the render comment to `RenderMeshIndirect`.

Cinematic Cheats used:
- Low/Toaster still keeps 8,000 particles, radial wake fake, no 3D flow, no curl, no SDF collision.
- High/Ultra keep 100,000 particles, abyssal flow texture, curl fake, sonar glow, and fog density injection without relying on one oversized GPU dispatch.

Exact Microseconds saved:
- 0 us measured. No profiler/player capture was available.
- Estimated frame-time gain is not claimed. The concrete win is bounded dispatch scheduling: 100,000-particle kernels split from one 1,563-group call into four <=512-group calls.
- Runtime cost can increase by several extra dispatch calls on High/Ultra; this is intentional policy compliance and TDR/scheduling risk reduction, not an optimization claim.

Validation:
- Static shader audit: marine-snow thread groups are 64, 64, and 1; no `distance()`, no wave intrinsics, no groupshared/SV_Group.
- Static VFX audit: no persistent VFX `NativeArray` fields, no direct `new NativeArray`, no `H8Memory.Allocate`, no unpacked VFX structs, no standard Unity update loops, no `string.Format`, no scene discovery, no legacy `EventBus` under `Assets/_Project/Scripts/VFX`.
- `git diff --check` clean except CRLF normalization warnings.
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly` returned EXIT=1 on unrelated `Assets/_Project/Scripts/Ecosystem/EcosystemRuntimeInstaller.cs(1,18) CS0234`. No touched marine-snow file was named.

## Entry 2026-05-16 - Marine Snow NaN and Atomic Saturation Pass

What was wrong:
- High/Ultra marine-snow density can send many particles into the same sonar/fog texel. Uncapped encoded `InterlockedAdd` contributions risked signed integer accumulator overflow or wrap.
- The compute kernel clamped velocity, but the full `SiltParticle` payload did not have a final finite-state quarantine before buffer writeback.
- Two VFX profile validators still used interpolated editor warning strings, creating noise in the VFX debt scan.

What was done:
- Added `MARINE_SNOW_MAX_ENCODED_SPLAT = 4096.0` and capped sonar/fog encoded splats before atomic add.
- Added non-finite/zero rejection for encoded sonar/fog contributions.
- Added `IsFiniteSiltParticle` in `Hecton_MarineSnow.compute`; invalid particles respawn deterministically, then hard-fallback to a safe camera-space particle if respawn math is also invalid.
- Replaced interpolated warnings in `ShakeProfile` and `BiomeProfile` with constant warning strings.

Cinematic Cheats used:
- Saturation instead of physical energy conservation: dense silt glow/fog clamps visually instead of accumulating unbounded math.
- Hard fallback particle is a presentation-safe reset, not a simulated recovery.
- Low/Toaster still runs 8,000 particles with radial wake fake; High/Ultra keep 100,000 particles, abyssal flow texture, curl fake, sonar glow, and fog injection.

Exact Microseconds saved:
- 0 us measured. No profiler/player capture was run.
- No frame-time gain is claimed. This pass buys fault containment and accumulator stability.
- Added GPU predicates may cost a small amount of ALU; the accepted tradeoff is preventing NaN propagation and atomic wrap.

Validation:
- Shader scan confirmed `MARINE_SNOW_MAX_ENCODED_SPLAT`, `IsFiniteSiltParticle`, `BuildHardFallbackParticle`, `_MarineSnowDispatchOffset`, and `_MarineSnowDispatchTileOffset` are present.
- Static shader audit: marine-snow thread groups are 64, 64, and 1; no case-sensitive `distance()` intrinsic call, no wave intrinsics, no groupshared/SV_Group, no `ComputeBuffer`, no `SetData`, no `GetData`.
- File-specific profile scan reports only constant `Debug.LogWarning` calls in `ShakeProfile` and `BiomeProfile`; no interpolation or `string.Format` remains there.
- `git diff --check` clean except CRLF normalization warnings.
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly` returned EXIT=0 in `Docs/AgentLogs/Dotnet_VOLUMETRIC_SILT_ADVECTION_Loop9.log` and `Docs/AgentLogs/Dotnet_VOLUMETRIC_SILT_ADVECTION_Loop9_PostDocs.log`.
- Unity DX12/Vulkan/player validation remains pending and is not claimed.
