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

## Entry 2026-05-16 - Marine Snow Kernel Group Query Pass

What was wrong:
- `HectonMarineSnowRenderer` duplicated compute shader thread-group dimensions with C# constants and shift math.
- That was legal for the current shader text but brittle for Metal/Quest/Steam Deck/backend variants because dispatch chunking was not derived from the actual kernel contract.
- Unity platform validation still could not be honestly completed.

What was done:
- Added per-kernel `ComputeShader.GetKernelThreadGroupSizes` queries for simulation, initialization, sonar accumulate, sonar clear, and fog clear.
- Cached sanitized particle group widths and 2D clear tile dimensions, with invalid or >1024-thread totals falling back to the known 64/8x8 defaults.
- Reworked particle and clear chunk dispatch math to use queried dimensions instead of `ThreadGroupShift`, fixed group constants, or `(dimension + 7) >> 3`.
- Retained <=512-group chunking and the 8,000 low / 100,000 high particle budget split.

Cinematic Cheats used:
- Low/Toaster remains the radial wake fake with 8,000 particles, no 3D flow, no curl, and no SDF collision.
- High/Ultra keep 100,000 particles, abyssal flow texture, curl fake, headlight emission, sonar glow, and fog injection; the dispatch contract is now backend-aware rather than hardcoded.

Exact Microseconds saved:
- 0 us measured. No profiler/player capture was run.
- No frame-time gain is claimed. This is platform correctness and shader ABI hardening.
- Runtime overhead is a cold-path kernel query during buffer setup, not per-particle hot-path work.

Validation:
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly` returned EXIT=0 in `Docs/AgentLogs/Dotnet_VOLUMETRIC_SILT_ADVECTION_Loop10_KernelQuery.log`.
- Shader thread-group parse reports 8x8x1, 8x8x1, 1x1x1, and 64x1x1 kernels, all <=1024 threads.
- Scoped VFX scan reports no standard Unity update loops, no `string.Format`, no interpolation `$"`, no scene discovery, no legacy `EventBus`, no coroutine, no `Resources.Load`, and no `Camera.main`.
- `git diff --check` is clean except CRLF normalization warnings.
- Unity default batchmode was attempted twice: the first run collided with another Unity process, and the retry stalled in script compilation/ILPP before platform shader validation. The owned Unity/ILPP processes were terminated. DX12/Vulkan validation remains blocked and is not claimed.

## Entry 2026-05-16 - Typed Lane and Hot-Swap Cache Pass

What was wrong:
- VFX runtime paths still contained legacy latest-state signal reads and bridge publishes after the marine-snow core had moved to typed signal/data-vault architecture.
- Camera juice telemetry resolved `GlobalRegistry.DataVault` inside its telemetry path, and camera/material/marine-snow systems still had repeated service lookup shape instead of hot-swap cached services.
- Low-tier marine-snow fake wander used hash samples in [0, 1], causing a positive X/Z drift bias instead of suspended symmetric silt.

What was done:
- `HectonMarineSnowRenderer` now pushes vehicle wake impulses through `SignalBus<FluidImpulseSignal>.Push`.
- `MaterialDecayRuntime` now consumes `ReadOnlySpan<PlayerStressSignal>` and pushes `ToolAcousticSignal` through its typed lane.
- `CameraJuiceSystem` now consumes `ReadOnlySpan<SeismicSignal>` and caches Player/Submarine/Dispatcher/DynamicResolution/VRAM/DataVault through hot-swap listeners.
- `GlobalSignals.Publish(in SeismicSignal)` now mirrors the existing seismic payload into `SignalBus<SeismicSignal>`; no new signal type was invented.
- `CameraJuiceSystem` and `MaterialDecayRuntime` preserve cached DataVault pointers during compaction fences and invalidate only handles/readiness.
- `Hecton_MarineSnow.compute` centers low-tier wander hash to [-1, 1] before scaling.

Cinematic Cheats used:
- Low/Toaster marine snow remains 8,000 particles, radial wake fake, symmetric hash wander, no 3D noise, no curl texture, no SDF collision.
- Camera seismic response remains triangle-pulse presentation shake driven by a typed snapshot, not physical camera simulation.
- High/Ultra keep 100,000 particles, abyssal flow texture, curl fake, sonar glow, fog density injection, and headlight emission.

Exact Microseconds saved:
- 0 us measured. No profiler/player capture was run.
- No frame-time gain is claimed. The concrete result is removal of legacy latest-state polling/bridge publishes from VFX paths and service cache hardening.
- Added seismic typed-lane mirroring is one additional native queue push per seismic publish; accepted to remove VFX latest-state dependency without changing the producer API.

Validation:
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly` returned EXIT=0 in `Docs/AgentLogs/Dotnet_VOLUMETRIC_SILT_ADVECTION_Loop11_TypedLaneCamera.log`.
- Static VFX scans report no `GlobalSignals.Publish`, no `GlobalSignals.TryGetLatest`, no legacy `EventBus`, no standard Unity update loops, no `string.Format`, no interpolation `$"`, no scene discovery, no coroutine, no `Resources.Load`, and no `Camera.main` under `Assets/_Project/Scripts/VFX`.
- Shader audit reports no `distance()`, no wave intrinsics, no groupshared/SV_Group, no `ComputeBuffer`, no `SetData`, and no `GetData` in the marine-snow compute/render path.
- Thread-group scan reports 8x8x1, 1x1x1, and 64x1x1 declarations, all under the 1024-thread Metal/Quest limit.
- `git diff --check` is clean except CRLF normalization warnings.
- Unity DX12/Vulkan validation is not claimed. A Unity process was already active (`Docs/AgentLogs/UnityProcess_VOLUMETRIC_SILT_ADVECTION_Loop11.log`), so no second platform compile was started.

## Entry 2026-05-17 - VFX Hot-Swap, Release I/O, and Platform Timeout Pass

What was wrong:
- `BiolumPulseSyncRuntime` still had registry fallback shape, compaction-fence risk, and release path probing outside StreamingAssets.
- `CarveDebrisComputeRenderer` had a concurrent staged regression that put scalability tier sampling back into the tick path.
- DX12/Vulkan validation was still unproven.

What was done:
- Added Biolum hot-swap rebind handling for Dispatcher/DataVault, compaction-fence refusal, vault generation refresh, and removal of the stale DataVault fallback helper.
- Restricted Biolum profile probing so player builds only check StreamingAssets; project `Docs` probes stay editor/development only.
- Corrected CarveDebris live source so low/high tier decisions are cached from cold seed or `ScalabilityChangedEvent`, not per-frame `GlobalRegistry` polling.
- Re-ran VFX debt scans and marine-snow shader portability scans.
- Attempted Unity batchmode with `-force-d3d12` and `-force-vulkan`; both timed out before shader/API validation and were killed cleanly.

Cinematic Cheats used:
- Low/Toaster remains fake-first: 8,000 marine-snow particles, radial wake, symmetric hash wander, no 3D noise, no SDF collision.
- High/Ultra still spend saved budget on 100,000 marine-snow particles, abyssal flow texture, curl fake, headlight emission, sonar glow, fog injection, and debris overkill paths.

Exact Microseconds saved:
- 0 us measured. No profiler/player capture was run.
- No frame-time gain is claimed.
- Release I/O risk is reduced by avoiding project `Docs` profile probes outside editor/development builds.

Validation:
- Static VFX scans found no standard `Update`/`LateUpdate`/`FixedUpdate`, `string.Format`, interpolation `$"`, scene discovery, coroutine, `Resources.Load`, `Camera.main`, `GlobalSignals.Publish`, `GlobalSignals.TryGetLatest`, legacy `EventBus`, or managed delegate patterns under `Assets/_Project/Scripts/VFX`.
- Marine-snow shader scan found no `distance()`, wave intrinsics, groupshared/SV_Group, `ComputeBuffer`, `SetData`, or `GetData`; thread groups remain under 1024.
- `dotnet restore` returned EXIT=0. `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies --no-incremental /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly` returned EXIT=0 in `Docs/AgentLogs/Dotnet_VOLUMETRIC_SILT_ADVECTION_Loop12_VfxDomain_Long.log` before the final tick-poll correction.
- Post-correction `dotnet build` reruns returned EXIT=-1 with empty logs while many concurrent `dotnet build` jobs held project state; exact final-source compile success is not claimed.
- Unity DX12 and Vulkan batchmode logs are `Docs/AgentLogs/Unity_VOLUMETRIC_SILT_ADVECTION_Loop12_DX12.log` and `Docs/AgentLogs/Unity_VOLUMETRIC_SILT_ADVECTION_Loop12_Vulkan.log`; both timed out before platform shader/API validation. Error scan found no `error CS`, shader error, exception, fatal abort, compilation failure, or owned VFX name before timeout.

## Entry 2026-05-17 - Dispatcher Hot-Swap Inquisition

What was wrong:
- VFX registration paths still had direct `GlobalRegistry.Dispatcher` readiness gates after DataVault and other runtime services had been moved to cached hot-swap state.
- `NativeTrailRenderer` had a flawed service replacement shape where render-dispatcher readiness could also mark tick-dispatch readiness.
- Current compile/platform validation still had to be rechecked after these final-source edits.

What was done:
- `HectonMarineSnowRenderer` now cold-seeds and hot-swaps Dispatcher readiness through `GlobalRegistryServiceSlot.Dispatcher`, then registers ticks from cached readiness.
- `MaterialDecayRuntime` now uses the same cached Dispatcher readiness instead of checking `GlobalRegistry.Dispatcher` in `TryRegisterTick`.
- `CameraJuiceSystem` now registers update/slow/late-frame lanes through cached `_dispatcher` service state and no longer gates unregister on a registry read.
- `NativeTrailRenderer` now registers for GlobalRegistry hot-swap callbacks and keeps separate `_dispatcherReady` and `_renderDispatcherReady` flags.
- Re-ran static VFX debt scans, struct layout scan, marine-snow shader portability scan, `git diff --check`, and scoped C# build.

Cinematic Cheats used:
- Low/Toaster remains the cheap marine-snow path: 8,000 particles, radial wake fake, centered hash wander, no 3D flow lookup, no curl texture, no SDF collision.
- High/Ultra still spend the budget on 100,000 particles, abyssal flow texture, curl fake, headlight emission, sonar glow, fog density injection, and existing camera/material/biolum presentation layers.

Exact Microseconds saved:
- 0 us measured. No profiler/player capture was run.
- No frame-time win is claimed. The gain is service hot-swap correctness and removal of direct dispatcher registry reads under VFX.

Validation:
- Static VFX scans found no direct `GlobalRegistry.Dispatcher`, no standard `Update`/`LateUpdate`/`FixedUpdate`, no `string.Format`, no interpolation `$"`, no scene discovery, no coroutine, no `Resources.Load`, no `Camera.main`, no legacy `EventBus`, and no managed delegate patterns under `Assets/_Project/Scripts/VFX`.
- VFX struct scan found explicit `Pack = 1` and `Size = ...` on marine snow, camera juice, material decay, biolum, debris, wake, and VFX budget structs.
- Marine-snow shader scan found no `distance()`, wave intrinsics, groupshared/SV_Group, `ComputeBuffer`, `SetData`, or `GetData`; particle kernels remain 64-thread and clear kernels are under 1024 threads.
- `git diff --check` is clean except CRLF normalization warnings.
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies --no-incremental /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly` returned EXIT=1 in `Docs/AgentLogs/Dotnet_VOLUMETRIC_SILT_ADVECTION_Loop13_HotSwapVfx.log` on unrelated `Assets/_Project/Scripts/SubmarineFluidDynamics.cs(729,43) CS0102`; the touched VFX files are not named.
- Unity DX12/Vulkan validation remains blocked and is not claimed.

## Entry 2026-05-17 - Dispatcher Identity Hot-Swap Pass

What was wrong:
- Boolean dispatcher readiness was not enough. A dispatcher service could be replaced while VFX systems kept `_registered` true, preventing registration into the replacement dispatcher lanes.
- `NativeTrailRenderer` had already split tick/render readiness, but it still needed service identity tracking so a replacement RenderDispatcher unregisters the old render registration.
- Final validation after the hot-swap edits had not completed.

What was done:
- `HectonMarineSnowRenderer` now tracks cached `ITickDispatcher` identity and unregisters stale update registration when the dispatcher changes.
- `MaterialDecayRuntime` now tracks cached `ITickDispatcher` identity and re-registers against the current dispatcher only.
- `CameraJuiceSystem` now compares dispatcher identity, unregisters stale update/slow/late-frame lanes, then registers against the replacement dispatcher.
- `BiolumPulseSyncRuntime` now gates update registration on cached dispatcher presence and unregisters on dispatcher identity change.
- `NativeTrailRenderer` now tracks both `ITickDispatcher` and `RenderDispatcher` identities and unregisters stale tick/origin/render registrations independently.

Cinematic Cheats used:
- Low/Toaster remains fake-first: 8,000 marine-snow particles, radial wake, centered hash wander, no 3D noise, no SDF collision, and cheap presentation reactions.
- High/Ultra remain overkill: 100,000 marine-snow particles, abyssal flow texture, curl fake, headlight emission, sonar glow, fog density injection, and richer camera/material/biolum responses.

Exact Microseconds saved:
- 0 us measured. No profiler/player capture was run.
- No frame-time gain is claimed. The fix prevents stale service registration after dispatcher replacement.

Validation:
- Static VFX scans found no direct `GlobalRegistry.Dispatcher`, no standard `Update`/`LateUpdate`/`FixedUpdate`, no `string.Format`, no interpolation `$"`, no scene discovery, no coroutine, no `Resources.Load`, no `Camera.main`, no legacy `EventBus`, and no managed delegate patterns under `Assets/_Project/Scripts/VFX`.
- Native allocation scan found no `new NativeArray`, no `Allocator.Persistent/Temp/TempJob`, no `H8Memory.Allocate`, and no `byte[]` allocation patterns under VFX.
- Marine-snow shader scan found no `distance()`, wave intrinsics, groupshared/SV_Group, `ComputeBuffer`, `SetData`, or `GetData`; reciprocal and `rsqrt` sites are guarded by max/finite checks.
- `git diff --check` is clean except CRLF normalization warnings.
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies --no-incremental /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly` timed out and produced an empty `Docs/AgentLogs/Dotnet_VOLUMETRIC_SILT_ADVECTION_Loop14_DispatcherHotSwapIdentity.log` while many concurrent dotnet/MSBuild jobs were active. Compile success is not claimed.
- Unity DX12/Vulkan validation remains blocked and is not claimed.

## Entry 2026-05-17 - Dynamic Wake Sovereignty Pass

What was wrong:
- Marine snow status claimed the FluidEngine `_DynamicWakes` / `_DynamicWakeVectors` GPU ring was used, but the live shader still sampled `_GlobalWakeBuffer[16]`, `_GlobalWakeVectors[16]`, and `_GlobalWakeParams`.
- That kept marine snow coupled to a global shader vector-array mirror instead of the authoritative FluidEngine wake payload.

What was done:
- `Hecton_MarineSnow.compute` now declares and samples `StructuredBuffer<float4> _DynamicWakes`, `StructuredBuffer<float4> _DynamicWakeVectors`, and `_DynamicWakeParams`.
- `HectonMarineSnowRenderer` now binds dynamic wake buffers from cached `HectonFluidEngine.TryGetDynamicWakeGpuPayload`.
- Wake params are sanitized before dispatch: low tier clamps to 4 slots, high/ultra can use 16 slots, and active count is clamped to slot limit.
- Empty GPU fallback buffers are bound when FluidEngine has no valid dynamic wake payload.

Cinematic Cheats used:
- Low/Toaster keeps the cheap fake: 8,000 particles, 4 wake slots, radial low-tier wake behavior, no 3D flow lookup when LOD is low, no SDF collision.
- High/Ultra keep the overkill path: 100,000 particles, 16 dynamic wake slots, abyssal flow texture, curl fake, headlight emission, sonar glow, and fog injection.

Exact Microseconds saved:
- 0 us measured. No profiler/player capture was run.
- No microsecond savings are claimed. The fix is data sovereignty and removal of the stale global wake mirror from marine snow.

Validation:
- Did not run dotnet rebuild or Unity platform compile in this loop per user instruction.
- Static scan found no `_GlobalWakeBuffer`, `_GlobalWakeVectors`, `_GlobalWakeParams`, `ResolveGlobalWakeFlow`, or `Shader.GetGlobalVector(ShaderIds.GlobalWake...)` in the owned marine-snow files.
- Static VFX scans found no standard `Update`/`LateUpdate`/`FixedUpdate`, no `string.Format`, no interpolation `$"`, no scene discovery, no coroutine, no `Resources.Load`, no `Camera.main`, no legacy `EventBus`, no managed delegate patterns, and no forbidden native allocation patterns under VFX.
- Marine-snow shader scan found no `distance()`, wave intrinsics, groupshared/SV_Group, append/consume buffers, `SetData`, or `GetData`.
- `git diff --check` is clean except CRLF normalization warnings.

## Entry 2026-05-17 - Dynamic Wake Naming Inquisition

What was wrong:
- After the dynamic wake binding fix, owned marine-snow code still had `GlobalWake` telemetry/debug names and shader capacity constants.
- That stale naming contradicted the FluidEngine `_DynamicWakes` authority path and made a repeat regression more likely.

What was done:
- `Hecton_MarineSnow.compute` now uses `HECTON_DYNAMIC_WAKE_CAPACITY` and `HECTON_DYNAMIC_WAKE_LOW_TIER_CAPACITY`.
- `HectonMarineSnowRenderer` telemetry/debug names now use `DynamicWakeCount` / `_debugDynamicWakeCount`.
- The blackbox telemetry struct size and dump write order remain unchanged.

Cinematic Cheats used:
- Low/Toaster still uses 8,000 particles, 4 dynamic wake slots, radial fake wake behavior, no SDF collision for silt, and no low-tier 3D flow lookup.
- High/Ultra still use 100,000 particles, 16 dynamic wake slots, abyssal flow texture, curl fake, headlight emission, sonar glow, and fog injection.

Exact Microseconds saved:
- 0 us measured. No profiler/player capture was run.
- No runtime savings are claimed. This is an interface clarity and regression-prevention fix.

Validation:
- Did not run dotnet rebuild or Unity platform compile.
- Static scan found no `GlobalWake`, `_GlobalWakeBuffer`, `_GlobalWakeVectors`, `_GlobalWakeParams`, `ResolveGlobalWakeFlow`, or `Shader.GetGlobalVector(ShaderIds.GlobalWake...)` in `HectonMarineSnowRenderer.cs` or `Hecton_MarineSnow.compute`.
- Static VFX scans found no standard `Update`/`LateUpdate`/`FixedUpdate`, no `string.Format`, no interpolation `$"`, no scene discovery, no coroutine, no `Resources.Load`, no `Camera.main`, no legacy `EventBus`, and no managed delegate patterns under VFX.
- Marine-snow shader scan found no `distance()`, wave intrinsics, groupshared/SV_Group, append/consume buffers, `SetData`, or `GetData`.

## Entry 2026-05-17 - Dynamic Wake Live-Source Regression Repair

What was wrong:
- The live source had regressed after the previous dynamic wake repair. `Hecton_MarineSnow.compute` again contained `_GlobalWakeBuffer[16]`, `_GlobalWakeVectors[16]`, `_GlobalWakeParams`, and `ResolveGlobalWakeFlow`.
- `HectonMarineSnowRenderer` again read `Shader.GetGlobalVector(ShaderIds.GlobalWakeParamsId)`, so marine snow was not bound to the FluidEngine dynamic wake GPU ring.
- The status file was only a claim until the current files were repaired again.

What was done:
- Replaced the shader global wake arrays with `StructuredBuffer<float4> _DynamicWakes` and `StructuredBuffer<float4> _DynamicWakeVectors`.
- Replaced `_GlobalWakeParams` with `_DynamicWakeParams` and `ResolveGlobalWakeFlow` with `ResolveDynamicWakeFlow`.
- `HectonMarineSnowRenderer` now binds `_DynamicWakes`, `_DynamicWakeVectors`, and `_DynamicWakeParams` from cached `HectonFluidEngine.TryGetDynamicWakeGpuPayload`.
- Dynamic wake buffers are validated with `GraphicsBuffer.IsValid()` and fall back to the existing empty GPU buffer when unavailable.
- Telemetry naming is `DynamicWakeCount`; blackbox telemetry struct size and dump write order remain unchanged.

Cinematic Cheats used:
- Low/Toaster keeps 8,000 particles, 4 dynamic wake slots, radial fake wake behavior, no SDF collision, and no low-tier 3D flow lookup.
- High/Ultra keeps 100,000 particles, 16 dynamic wake slots, abyssal flow texture, curl fake, headlight emission, sonar glow, and fog injection.

Exact Microseconds saved:
- 0 us measured. No profiler/player capture was run.
- No microsecond savings are claimed. This repair restores data sovereignty and removes the global wake mirror from the live marine-snow path.

Validation:
- Did not run dotnet rebuild or Unity platform compile.
- Static scan found no `GlobalWake`, `_GlobalWakeBuffer`, `_GlobalWakeVectors`, `_GlobalWakeParams`, `ResolveGlobalWakeFlow`, `GlobalWakeParamsId`, `_debugGlobalWake`, or `GlobalWakeCount` in `HectonMarineSnowRenderer.cs` or `Hecton_MarineSnow.compute`.
- Dynamic wake scan confirms `_DynamicWakes`, `_DynamicWakeVectors`, `_DynamicWakeParams`, `ResolveDynamicWakeFlow`, `DynamicWakeCount`, and `TryGetDynamicWakeGpuPayload`.
- Marine-snow shader scan found no `distance()`, wave intrinsics, groupshared/SV_Group, append/consume buffers, `SetData`, or `GetData`.
- VFX update-loop scan found no standard `Update`/`LateUpdate`/`FixedUpdate`.
- `git diff --check` reports only CRLF normalization warnings.

## Entry 2026-05-17 - Dynamic Wake Concurrent Regression Lockdown

What was wrong:
- The live marine-snow files were overwritten back to the old `_GlobalWake*` path during the next pass.
- `Hecton_MarineSnow.compute` again contained `_GlobalWakeBuffer[16]`, `_GlobalWakeVectors[16]`, `_GlobalWakeParams`, and `ResolveGlobalWakeFlow`.
- `HectonMarineSnowRenderer` again contained `GlobalWakeParamsId`, `_boundGlobalWakeParams`, `_debugGlobalWakeCount`, `RefreshGlobalWakeBinding`, and `GlobalWakeCount`.
- Adjacent `CarveDebrisComputeRenderer` had two Burst job structs with `[StructLayout(Pack = 1)]` and no explicit `Size`, even though those structs contain platform-dependent `NativeArray<T>` handles.

What was done:
- Repaired the live marine-snow shader back to `StructuredBuffer<float4> _DynamicWakes`, `StructuredBuffer<float4> _DynamicWakeVectors`, `_DynamicWakeParams`, and `ResolveDynamicWakeFlow`.
- Repaired `HectonMarineSnowRenderer` to bind `_DynamicWakes`, `_DynamicWakeVectors`, and `_DynamicWakeParams` from cached `HectonFluidEngine.TryGetDynamicWakeGpuPayload`.
- Kept wake params sanitized: low tier clamps to 4 dynamic wake slots, high/ultra can use 16, active count cannot exceed slot limit.
- Kept blackbox telemetry ABI stable while using the `DynamicWakeCount` name.
- Removed false packed layout attributes from `AgeCarveDebrisMirrorJob` and `CarveDebrisInjectBatchJob`; binary/GPU-facing VFX structs still carry explicit `Size`.

Cinematic Cheats used:
- Low/Toaster: 8,000 marine-snow particles, 4 wake slots, radial fake wake behavior, no silt SDF collision, no low-tier 3D flow lookup.
- High/Ultra: 100,000 marine-snow particles, 16 dynamic wake slots, abyssal flow texture, curl fake, headlight emission, sonar glow, and fog injection.

Exact Microseconds saved:
- 0 us measured. No profiler/player capture was run.
- No microsecond savings are claimed. This is correctness, data sovereignty, and platform-layout debt cleanup.

Validation:
- Did not run dotnet rebuild or Unity platform compile.
- Delayed static scan found no `GlobalWake`, `_GlobalWake*`, `HECTON_GLOBAL_WAKE`, `ResolveGlobalWakeFlow`, `GlobalWakeParamsId`, `_debugGlobalWake`, or `GlobalWakeCount` in owned marine-snow files.
- Dynamic wake scan confirms `_DynamicWakes`, `_DynamicWakeVectors`, `_DynamicWakeParams`, `ResolveDynamicWakeFlow`, `DynamicWakeCount`, and `TryGetDynamicWakeGpuPayload`.
- Delayed VFX struct scan found no `StructLayout(... Pack = 1)` entries without explicit `Size` under `Assets/_Project/Scripts/VFX`.
- VFX debt scan found no standard update loops, `string.Format`, interpolation `$"`, scene discovery, coroutine, `Resources.Load`, `Camera.main`, legacy `EventBus`, `GlobalSignals.Publish/TryGetLatest`, or managed delegate patterns under `Assets/_Project/Scripts/VFX`.
- Marine-snow shader scan found no `distance()`, wave intrinsics, groupshared/SV_Group, append/consume buffers, `SetData`, or `GetData`.
- `git diff --check` reports only CRLF normalization warnings.

## Entry 2026-05-17 - Fluid Advection Dynamic Wake Contract Pass

What was wrong:
- `Hecton_FluidAdvection.compute` still declared `_GlobalWakeBuffer`, `_GlobalWakeVectors`, and `_GlobalWakeParams`.
- `HectonFluidEngine.BindFluidAdvectionCompute` already binds `_DynamicWakes`, `_DynamicWakeVectors`, and `_DynamicWakeParams`.
- `CarveDebrisComputeRenderer` only copied global wake params, so adjacent debris/fluid advection could miss the actual FluidEngine wake buffers.

What was done:
- `Hecton_FluidAdvection.compute` now consumes `StructuredBuffer<float4> _DynamicWakes`, `StructuredBuffer<float4> _DynamicWakeVectors`, and `_DynamicWakeParams`.
- `ApplyGlobalWakes` became `ApplyDynamicWakes` and all fluid/debris advection call sites use it.
- `CarveDebrisComputeRenderer` now resolves the dynamic wake payload through cached `HectonFluidEngine.TryGetDynamicWakeGpuPayload`.
- Debris advection validates both `GraphicsBuffer` handles and falls back to the existing empty float4 buffer when FluidEngine has no valid payload.
- Wake params are sanitized locally: low tier clamps to 4 slots, high/ultra can use 16, active count cannot exceed slot limit.

Cinematic Cheats used:
- Low/Toaster: 4 wake slots for debris, low-tier advection gate, cheap bounded vector math, no extra texture/noise work added.
- High/Ultra: FluidEngine dynamic wake ring drives debris/fluid advection while marine snow keeps 100,000 particles, 16 wake slots, abyssal flow texture, curl fake, headlight emission, sonar glow, and fog injection.

Exact Microseconds saved:
- 0 us measured. No profiler/player capture was run.
- No microsecond savings are claimed. This is a correctness and data-sovereignty repair.

Validation:
- Did not run dotnet rebuild or Unity platform compile.
- Delayed static scan found no local `GlobalWake`, `_GlobalWake*`, `HECTON_GLOBAL_WAKE`, `ApplyGlobalWakes`, `ResolveGlobalWakeParamsForCompute`, or `GlobalWakeParamsId` in marine-snow, carve-debris, or fluid-advection owned files.
- Dynamic wake scan confirms `_DynamicWakes`, `_DynamicWakeVectors`, `_DynamicWakeParams`, `ApplyDynamicWakes`, `TryGetDynamicWakeGpuPayload`, and `ResolveDynamicWakePayloadForCompute`.
- Marine-snow/fluid-advection shader scans found no `distance()`, wave intrinsics, groupshared/SV_Group, append/consume buffers, `SetData`, or `GetData`.
- VFX C# debt scan found no standard update loops, `string.Format`, interpolation `$"`, scene discovery, coroutine, `Resources.Load`, `Camera.main`, legacy `EventBus`, `GlobalSignals.Publish/TryGetLatest`, or managed delegate patterns.
- `git diff --check` reports only CRLF normalization warnings.
