# LOG_SCREEN_SPACE_REFRACTION

## 2026-05-16 - Session Start

What was wrong: Porthole and mask glass refraction task had no agent status/rationale/log files on disk for this batch.
What was done: Created task status, rationale, and log files for SCREEN_SPACE_REFRACTION.
Cinematic Cheats used: Screen-space fake selected as primary route; physical glass/water raytracing rejected before implementation.
Exact Microseconds saved: Pending measurement; no runtime code changed yet.

## 2026-05-16 - Screen-Space Snell Refraction Core

What was wrong: Visor glass relied on fabricated scene color or the old fullscreen blit path. It did not have a shared low-cost Snell approximation, an IOR LUT, explicit `_CameraOpaqueTexture` refraction, depth rejection, or dirt-gated intensity. Full verification was impossible because the shared project currently fails in unrelated core/fauna/AI/animation/VFX files.

What was done: Added `Assets/_Project/Art/Shaders/Post/Hecton_SnellRefractionCore.hlsl`; updated `SuitVisor.shader` to sample `_CameraOpaqueTexture`, use depth-buffer foreground rejection, clamp UV offsets, apply inverse dirt masks, and fall back to chromatic-only low-tier refraction; updated `Hecton_VisorFluidDistortion.shader` with opaque/depth sampling, droplet-mask Snell offsets, and chromatic fallback; migrated `HectonVisorFluidDistortionFeature.cs` from `AddBlitPass` to `AddRasterRenderPass`, binding `_BlitTexture`, `_CameraDepthTexture`, and `_CameraOpaqueTexture` explicitly.

Cinematic Cheats used: No raytracing, no physical glass simulation, no per-object GrabPass. Low-tier path uses chromatic split only. High path uses bounded normal/droplet UV offsets from `_CameraOpaqueTexture`. Stress/homeostasis degrades to the fake path instead of spending samples during visual chaos.

Exact Microseconds saved: 0 us measured and certified. Profiling is blocked by unrelated compile failures. Static expected saving is removal of per-object grab-style capture and old blit utility dependency; exact numbers require a clean Unity/profiler run.

Build/validation: `dotnet restore Hecton8.Core.csproj` succeeded. `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` failed outside this task: missing `Hecton8.Core.Signals`, missing `Hecton8.AI.Perception`, missing animation fauna IK types, missing `IResolutionScalerService`, and `HectonMarineSnowRenderer` interface drift. Static checks found no `AddBlitPass`, `RenderGraphUtils`, `GrabPass`, `new NativeArray`, `GC.Alloc`, singleton `.Instance`, or Unity object search calls in touched files.

## 2026-05-16 - Continuation Multiplatform Inquisition

What was wrong: The prior blackbox waiver was no longer defensible under the continuation mandate, and the post shader still carried a higher shader-model target than its actual fragment work required. The system also needed a strict cross-platform/I/O/data-sovereignty audit against the touched visor/refraction files.

What was done: Confirmed the visor post feature has a packed 300-frame DataVault heartbeat at `BufferID.VisorRefractionBlackBox`, records wetness, hull stress, water density, fallback flags, quality tier, camera dimensions, velocity magnitude, and hash state, and dumps `Docs/AgentLogs/Dump_SCREEN_SPACE_REFRACTION.bin` only on non-finite input. Confirmed the fullscreen shader uses target 3.5 and High/Ultra-only `_HectonVisorFluidVisualOverkill` salt-crystal growth, while Low/MX350 forces the extra path off.

Cinematic Cheats used: Toaster path remains chromatic split plus bounded dirt/depth masks. High/Ultra salt growth is procedural ALU, not particles, raymarching, POM, or a physical crystal simulation. Volumetric silt wake and hull dent work were rejected as out-of-domain for this VFX/POST refraction agent.

Exact Microseconds saved: 0 us certified. New heartbeat cost is a 48-byte DataVault write when the player camera is evaluated; exact CPU microseconds pending profiler. Shader overkill is CPU 0.0 us/frame and GPU ALU-only, tier-gated, exact GPU microseconds pending profiler.

Build/validation: Re-extracted the XML assignment from `Docs/Tasks/CURRENT_BATCH.md`. Static audit found no compute thread groups, DX-only texture calls, `EventBus`, managed delegate lane, standard `Update` methods, `string.Format`, Unity object search, singleton `.Instance`, `AddBlitPass`, `RenderGraphUtils`, or `GrabPass` in touched visor/refraction files. `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` still fails outside this domain with 67 errors, first in `DiegeticGyroCompassRuntime.cs`, `LockstepStateValidator.cs`, `HomeostasisBrain.cs`, `PickupItem.cs`, and `TetherSignals.cs`.

## 2026-05-16 - DataVault Handle Eviction Pass

What was wrong: The visor feature still contained a literal `NativeArray<VisorRefractionTelemetryEntry>` alias and private telemetry cursor state. It was vault-owned, but the code surface still looked like local NativeArray ownership.

What was done: Replaced the alias with `VaultBufferHandle<VisorRefractionTelemetryEntry>`, resolved the live vault pointer only when writing telemetry, and removed private cursor/last-frame fields. The ring index is now derived from `Time.frameCount % blackBoxLength`, keeping the heartbeat deterministic without feature-owned cursor state.

Cinematic Cheats used: No new simulation, no per-frame I/O, no managed buffer. The blackbox is a fixed 300-frame binary heartbeat and only dumps on non-finite input.

Exact Microseconds saved: Not measured. Runtime work remains one 48-byte heartbeat write when evaluated. Removing the cursor/last-frame fields saves two trivial field writes/reads per evaluated player-camera frame; exact us pending profiler.

Build/validation: `rg NativeArray Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs` returns no matches. Domain scan found no forbidden hot-path patterns. `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:ContinuousIntegrationBuild=false -p:EnableSourceControlManagerQueries=false -v:minimal -clp:Summary` fails outside this domain with 39 errors, first in `HectonXRRuntimeState.cs`, `BiolumPulseSyncRuntime.cs`, `VaultProbeUtility.cs`, `SpatialAudioManager.cs`, and `SubmarineStructuralGrid.cs`.

## 2026-05-16 - High Tier Suspended Silt Fake

What was wrong: The visor overkill path had salt crystals but no suspended silt impression. Adding real volumetric silt or wake particles would exceed the VFX/POST refraction boundary and create cross-domain runtime ownership.

What was done: Added `ComputeSuspendedSiltMask` to `Hecton_VisorFluidDistortion.shader`. It uses procedural noise, hashed specks, wetness/rain activity, depth validity, inverse dirt, local velocity drift, and `_HectonVisorFluidVisualOverkill`. Low/MX350 stays off because the overkill uniform resolves to zero.

Cinematic Cheats used: Screen-space silt shimmer, not particles, raymarching, wake fluid simulation, or texture-driven volume.

Exact Microseconds saved: 0 us measured. CPU cost is 0.0 us/frame. GPU cost is added fragment ALU only on High/Ultra where the overkill uniform is non-zero; exact us pending profiler after shared build clears.

Build/validation: Shader diff passes `git diff --check` except LF/CRLF warnings. Forbidden-pattern scan still finds no `Update`, `string.Format`, `EventBus`, managed delegate lane, Unity object search, singleton `.Instance`, `GrabPass`, `RenderGraphUtils`, `AddBlitPass`, compute thread groups, group barriers, or DX-only `tex2D` in touched visor/refraction files.

Post-silt build retry: `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:ContinuousIntegrationBuild=false -p:EnableSourceControlManagerQueries=false -v:minimal -clp:Summary` failed before C# compilation because `Temp/obj/Hecton8.Core/Hecton8.Core.sourcelink.json` was locked by another process. Unknown concurrent build processes were not killed.

## 2026-05-16 - Shader Uniform NaN Hardening

What was wrong: The visor Snell/refraction shaders already clamped final UV perturbation, but several upstream uniform gates still trusted raw `saturate()`, and the shared Snell helper accepted raw `nDotV`/`strength`. A non-finite wetness, stress, rain, lightning, visual-overkill, dust, thermal, or Snell-strength value could contaminate shader math before the clamp.

What was done: Replaced the refraction-critical uniform gates in `Hecton_VisorFluidDistortion.shader` with `HectonFinite01`, including salt growth, suspended silt, wetness/stress/intensity, rain, lightning, dust reveal, ambient dust tint, and thermal culling. Replaced the mesh visor refraction controls in `SuitVisor.shader` with `HectonFinite01` and explicit `isfinite` for `_HectonVisorSnellStrength`. Hardened `Hecton_SnellRefractionCore.hlsl` so `HectonSnellBend01` finite-guards `nDotV` and `HectonSnellUvOffset` zeros non-finite `strength`.

Cinematic Cheats used: No new simulation. The low path remains chromatic-only; High/Ultra keep screen-space salt and silt fakes, now finite-gated before they affect `_CameraOpaqueTexture` sampling.

Exact Microseconds saved: 0 us certified. CPU cost remains 0.0 us/frame. GPU cost is added finite-check ALU, exact us pending profiler; the value is stability, not measured speed.

Build/validation: Targeted scan no longer finds raw high-risk refraction uniform gates. Shared Snell core scan confirms `HectonFinite01(nDotV)` and non-finite `strength` fallback. Forbidden-pattern scan still finds no `NativeArray`, `EventBus`, managed delegate lane, `Update`, `string.Format`, Unity object search, singleton `.Instance`, `GrabPass`, `RenderGraphUtils`, `AddBlitPass`, compute thread groups, group barriers, or DX-only `tex2D` in touched visor/refraction files. `git diff --check` reports no whitespace errors, only LF/CRLF warnings.

## 2026-05-16 - Dotnet Build Recovered

What was wrong: Previous verification was blocked first by unrelated compile debt and then by a shared SourceLink file lock. That left the refraction work at static-audit-only status.

What was done: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:ContinuousIntegrationBuild=false -p:EnableSourceControlManagerQueries=false -v:minimal -clp:Summary` after the shader hardening pass.

Cinematic Cheats used: No new runtime cheat in this step; this was validation only. The implemented visual cheats remain `_CameraOpaqueTexture` Snell approximation, low-tier chromatic fallback, depth/dirt gates, salt-crystal ALU growth, and suspended silt shimmer.

Exact Microseconds saved: 0 us measured. Build verification does not provide frame timing. Unity profiler/Frame Debugger/GCMonitor remain required for exact CPU/GPU microseconds.

Build/validation: Build succeeded: `Hecton8.Core -> C:\hades\Hecton8\Temp\bin\Debug\Hecton8.Core.dll`, 0 warnings, 0 errors, elapsed 00:01:20.37. Runtime visual verification, RenderGraph execution ordering in Unity, platform shader compilation, and profiler numbers remain pending.

## 2026-05-16 - Common Snell Boundary Hardening

What was wrong: The shared Snell helper still let non-finite LUT, depth, softness, and clamp-bound values reach `max`, `smoothstep`, or `min` before final UV clamp. Water density sanitization also discarded invalid shader/global fluid density without marking the blackbox reason flag.

What was done: Hardened `Hecton_SnellRefractionCore.hlsl` so raw IOR values fall back to stable air/water/glass defaults, depth and softness values are finite before depth gating, and clamp bounds collapse to zero when invalid. Updated `HectonVisorFluidDistortionFeature.cs` so invalid `_HectonWaterDensitySignal` or `GlobalRegistry.FluidSimulation.WaterDensityKilogramsPerCubicMeter` sets `BlackBoxFlagNonFiniteInput` before the safe fallback.

Cinematic Cheats used: No new physical simulation. This protects the existing cheap screen-space Snell, chromatic fallback, salt crystal fake, and suspended silt fake.

Exact Microseconds saved: 0 us certified. CPU adds two finite checks when the player camera is evaluated; GPU adds small helper ALU. Exact CPU/GPU us pending Unity profiler.

Build/validation: Stale-pattern scan found no old raw Snell LUT/depth/clamp path. Forbidden-pattern scan still finds no `NativeArray`, `EventBus`, managed delegate lane, `Update`, `string.Format`, Unity object search, singleton `.Instance`, `GrabPass`, `RenderGraphUtils`, `AddBlitPass`, compute thread groups, group barriers, or DX-only `tex2D` in touched visor/refraction files. `git diff --check` reports no whitespace errors, only LF/CRLF warnings. Latest `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:ContinuousIntegrationBuild=false -p:EnableSourceControlManagerQueries=false -v:minimal -clp:Summary` is blocked outside this domain with 22 errors in `SubmarineFluidDynamics.cs(614-635)`: `VaultNativeBuffer<>` type not found.
