# LOG_SCREEN_SPACE_REFRACTION

## 2026-05-16 - Session Start

What was wrong: Porthole and mask glass refraction task had no agent status/rationale/log files on disk for this batch.
What was done: Created task status, rationale, and log files for SCREEN_SPACE_REFRACTION.
Cinematic Cheats used: Screen-space fake selected as primary route; physical glass/water raytracing rejected before implementation.
Exact Microseconds saved: Pending measurement; no runtime code changed yet.

## 2026-05-16 - Screen-Space Snell Refraction Core

What was wrong: Visor glass relied on fabricated scene color or the old fullscreen blit path. It did not have a shared low-cost Snell approximation, an IOR LUT, explicit `_CameraOpaqueTexture` refraction, depth rejection, or dirt-gated intensity. Full verification was impossible at that checkpoint because the shared project failed in unrelated core/fauna/AI/animation/VFX files.

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

Build/validation: Stale-pattern scan found no old raw Snell LUT/depth/clamp path. Forbidden-pattern scan still finds no `NativeArray`, `EventBus`, managed delegate lane, `Update`, `string.Format`, Unity object search, singleton `.Instance`, `GrabPass`, `RenderGraphUtils`, `AddBlitPass`, compute thread groups, group barriers, or DX-only `tex2D` in touched visor/refraction files. `git diff --check` reports no whitespace errors, only LF/CRLF warnings. That build retry was blocked outside this domain with 22 errors in `SubmarineFluidDynamics.cs(614-635)`: `VaultNativeBuffer<>` type not found.

## 2026-05-16 - Shader Vector Global Guard

What was wrong: The fullscreen visor shader finite-guarded many scalar gates, but still read `_HectonVisorFluidLocalVelocity`, `_HectonScreenSpaceRainParams`, and `_GlobalWind` directly before droplet flow, silt drift, rain density/exposure, and wind `rsqrt` math. A bad vector global could still contaminate UV offsets or rain overlay math on mobile/Metal GPUs.

What was done: Added `ResolveFinite4`, resolved local velocity once in `Frag`, and passed that finite value into droplet, silt, and refraction offset functions. Rain params and global wind now resolve to stable fallback vectors before density, area scale, exposure, wind speed, and wind direction math. No new texture, buffer, render pass, signal lane, file I/O, or managed allocation was added.

Cinematic Cheats used: The refraction still uses the cheap screen-space Snell approximation, low-tier chromatic fallback, inverse dirt/depth gates, procedural salt crystals, and screen-space silt shimmer. Invalid motion/rain vectors now collapse to a controlled fake instead of trying to simulate or recover physical state.

Exact Microseconds saved: 0 us certified. CPU cost remains 0.0 us/frame. GPU adds a small finite-check boundary around vector globals; exact GPU microseconds require Unity profiler after the shared build clears.

Build/validation: Vector-global scan shows `_HectonVisorFluidLocalVelocity`, `_HectonScreenSpaceRainParams`, and `_GlobalWind` are now read through `ResolveFinite4` before flow/rain math. Forbidden-pattern scan returns no `NativeArray`, `EventBus`, managed delegate lane, `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, Unity object search, singleton `.Instance`, `AddBlitPass`, `RenderGraphUtils`, `GrabPass`, compute thread groups, group barriers, or DX-only `tex2D` in touched visor/refraction files. A C# build pass after the vector guard was blocked outside this domain with 17 errors, first in `PredatorCognitionDomain.cs` (`NativeArray<float3>.Clear`, `AsParallelWriter`, missing `_speciesTuningById`) and `DroneFleetManager.cs` (`double3` to `float3` conversion).

## 2026-05-16 - Shared Build Lock Retry

What was wrong: After the documentation closure, the newest `dotnet build` retries failed before C# compilation because `Temp/obj/Hecton8.Core/Hecton8.Core.sourcelink.json` is locked by another build process.

What was done: Retried the same build once after a short wait, then queried active `dotnet.exe` processes. Multiple concurrent `dotnet build Hecton8.Core.csproj` commands were observed in the shared workspace. I did not kill them and did not delete `Temp/obj`.

Cinematic Cheats used: None. This was validation infrastructure only; runtime cheats remain screen-space Snell, low-tier chromatic fallback, depth/dirt masks, salt-crystal ALU, and silt shimmer.

Exact Microseconds saved: 0 us measured. The lock has no runtime impact. Exact visor CPU/GPU microseconds remain pending profiler.

Build/validation: That command failed with 0 warnings and 1 error at `Microsoft.SourceLink.Common.targets(56,5)`: cannot write `Temp/obj/Hecton8.Core/Hecton8.Core.sourcelink.json` because another process held the file. Previous static scans remained clean.

## 2026-05-16 - Full Visor Scalar Guard

What was wrong: Vector globals were guarded, but scalar shader globals in the mesh visor and fullscreen fluid pass could still enter HUD/foveation/glitch/distortion/refraction math as NaN before the final color or UV clamps.

What was done: `SuitVisor.shader` now resolves HECTON HUD fog/frost, suit-health glitch, VR comfort, foveation, stress vignette, HUD focus blur, and visual-static seed through finite helpers. `Hecton_VisorFluidDistortion.shader` now guards droplet scale, runoff speed, edge exponent, lateral/forward/edge strengths, fluid speed, distortion strength, depth softness, low-tier/homeostasis flags, Snell strength, water density, and ambient dust response through finite scalar helpers.

Cinematic Cheats used: No physical simulation was added. Low-tier remains chromatic/dirt/depth fake; High/Ultra retain procedural salt crystals and silt shimmer, now with invalid controls collapsing to stable zero/fallback values.

Exact Microseconds saved: 0 us certified. CPU cost is 0.0 us/frame. GPU cost is added finite-check ALU only; exact GPU microseconds require Unity profiler.

Build/validation: Raw HECTON-global scan no longer finds direct `saturate(_Hecton...)`, raw `_Hecton...xyz/w` access, or raw HECTON vector arithmetic in `SuitVisor.shader`; fullscreen fluid scalar knobs route through finite helpers. Forbidden-pattern scan is clean for touched files. That build retry reached C# and failed outside this domain with 0 warnings and 1 error: `TetherManager.cs(266,58) CS0426 TetherFireRequest does not exist in TetherSignals`.

## 2026-05-16 - Shared Finite Helper Consolidation

What was wrong: The NaN guard pass left two local helper families in the visor shaders. Functionally acceptable, but fragile: shared Snell refraction should not depend on duplicated local finite-guard code.

What was done: Added `HectonFiniteValue`, `HectonFiniteNonNegative`, and `HectonFinite4` to `Hecton_SnellRefractionCore.hlsl`. Removed the local finite helper implementations from `Hecton_VisorFluidDistortion.shader` and `SuitVisor.shader`; both shaders now call the shared helpers.

Cinematic Cheats used: No new simulation. This preserves the existing Dear Lie stack: low-tier chromatic fallback, bounded screen-space Snell, inverse dirt/depth gates, procedural salt crystals, and visor-space suspended silt.

Exact Microseconds saved: 0 us certified. CPU cost is 0.0 us/frame. GPU instruction shape should inline to the same finite-check ALU; exact microseconds require Unity profiler.

Build/validation: Stale helper scan finds no `ResolveFinite4`, `ResolveFiniteScalar`, `ResolveFiniteNonNegative`, `HectonResolveFinite4`, or `HectonResolveFiniteScalar`. Raw HECTON-global scan remained clean for targeted visor shader patterns. First build retry hit SourceLink file lock; SourceLink-disabled retry reached C# and failed outside VFX/POST with 0 warnings and 23 errors, first in `DiegeticGyroCompassRuntime.cs` and `EcosystemDirector.cs`. This was later superseded by the dynamic division guard build success.

## 2026-05-16 - Dynamic Division Guard

What was wrong: Some shader denominators were bounded by surrounding code but still expressed as direct `/` operations. That is weak evidence for the mobile NaN-vaccination rule.

What was done: Converted dynamic divisions to guarded reciprocal math for screen texel size, sonar contact/fade timing, foveated quantization, droplet cell normalization, and fluid radial edge direction. Literal Bayer table divisions and fixed HUD-box normalization remain unchanged because their denominators are compile-time constants.

Cinematic Cheats used: No new simulation. This keeps the same screen-space Snell, chromatic low-tier fake, depth/dirt masks, salt crystal fake, and visor-space silt shimmer.

Exact Microseconds saved: 0 us certified. GPU instruction cost is equivalent reciprocal math; exact microseconds require Unity profiler.

Build/validation: Dynamic slash scan now leaves only constants/comments/includes plus guarded `rcp`/`rsqrt` paths. Forbidden-pattern scan is clean for touched visor/refraction files. SourceLink-disabled `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:ContinuousIntegrationBuild=false -p:EnableSourceControlManagerQueries=false -p:EnableSourceLink=false -v:minimal -clp:Summary` succeeded: `Hecton8.Core -> Temp/bin/Debug/Hecton8.Core.dll`, 1 warning, 0 errors. Warning: `MSB3061` could not delete locked `Hecton8.Core.sourcelink.json` held by `csc (67600)`.

## 2026-05-16 - Literal Division And Boundary Guard

What was wrong: The denominator audit still had literal Bayer `/ 16.0` constants and a fixed HUD battery-box vector divide, which were safe but kept audit noise alive. The mesh visor also still trusted several material and engine-fed values near approximate normalization, screen/depth W reciprocal math, HUD close occlusion, glass alpha, static/hazard/bios controls, grime/mask/crack controls, and final refraction-adjacent color gates.

What was done: Replaced the Bayer table divisions with a `0.0625` multiply, replaced the HUD battery-box divide with multiply constants, finite-guarded approximate normalization inputs, resolved refraction-adjacent mesh visor material controls through the shared finite helpers, guarded screen-position and depth W before reciprocal use, reused a finite HUD close-occlusion distance, and zeroed any non-finite fullscreen fluid base offset before deriving the Snell normal.

Cinematic Cheats used: No physical simulation was added. This preserves the Dear Lie stack: `_CameraOpaqueTexture` screen-space Snell, low-tier chromatic fallback, inverse dirt/depth gates, procedural salt crystals, and visor-space suspended silt shimmer.

Exact Microseconds saved: 0 us certified. CPU cost is 0.0 us/frame. GPU cost is small finite-check ALU; constant divides became compile-time multiply constants. Exact GPU microseconds still require Unity profiler.

Build/validation: An initial normal build retry failed outside domain in `TetherInstance.cs` on missing `IsFrameCooldownActive`; no tether code was edited. After shader cleanup, targeted slash/raw-uniform scan reports only shader names, include paths, and comments. Forbidden-pattern scan remains clean for touched visor/refraction files. `git diff --check` reports no whitespace errors, only existing LF/CRLF warnings. SourceLink-disabled and normal build checkpoints briefly succeeded with 0 warnings and 0 errors, but latest retries are now blocked outside VFX/POST: normal build fails in `EcosystemDirector.cs(5970-6027)` on duplicate index helper members, and SourceLink-disabled build fails in `LockstepStateValidator.cs(408-417)` on missing lockstep/system-glitch lane constants. Unity runtime shader compilation, RenderGraph Frame Debugger order, platform shader compile, and profiler timings remain pending.

## 2026-05-16 - Uber Post Tier Shed And Finite Boundary

What was wrong: `HectonVisorUberPost.shader` was still target 4.5 despite no compute/UAV/group-memory path, accepted raw shader globals around visor physiology and waterline polish, let low-tier PC enter the non-mobile 16-tap shaft loop, and used `pow` inside that loop. `SuitVisor.shader` still had several raw material/global knobs near droplet density, chromatic split, sonar, hypoxia, HUD tint, smoothness, reflection, and screen-size static noise.

What was done: Lowered Uber post to target 3.5, included the shared finite helper boundary, finite-guarded Uber screen params, waterline, brine, light shaft, comfort, dirt, crack, pressure, heat, hypoxia, bleeding, and UV offset math. Low-tier now returns zero for light shafts. The 16-tap path uses `FastRadialFalloff01` polynomial falloff instead of `pow`. SuitVisor now resolves the remaining raw knobs through finite aliases before use.

Cinematic Cheats used: Light shafts remain a post-process fake. Low tier drops the shaft loop entirely. High/Ultra keep a 16-tap fake with polynomial falloff. No raymarching, no particles, no new textures, no material clones, no new buffers, and no disk reads were added.

Exact Microseconds saved: Not certified. CPU cost is 0.0 us/frame. Low-tier GPU should skip the 16-tap shaft loop, and High/Ultra replace tap-loop `pow` with multiply/lerp ALU. Exact GPU microseconds require Unity profiler.

Build/validation: `rg` confirms no `#pragma target 4.5`, `pow`, `tex2D`, compute thread-group tokens, RW resources, or `GrabPass` in Uber post. Targeted SuitVisor raw-uniform scan is clean except declarations and finite aliases. Broader shader risk scan reports only benign shader header text and target 3.5 declarations. `git diff --check` reports no whitespace errors, only LF/CRLF warnings. Normal build currently fails at shared SourceLink file lock. SourceLink-disabled build reaches C# and fails outside VFX/POST in `SubmarineFluidDynamics.cs(1853,60)` and `(4582,68)` on ambiguous `float3`/`Vector3` subtraction.

## 2026-05-17 - Uber Fragment UV Closure

What was wrong: The Uber post helpers still assumed the XR-transformed fragment UV and depth/world-position reconstruction would always be finite before screen texture sampling.

What was done: Sanitized the stereo-transformed fragment UV once in `Frag`, then added local fail-closed UV/world-position fallbacks in internal water, droplet, comfort, lens dirt, light shaft, and brine fog helpers.

Cinematic Cheats used: No simulation was added. The pass remains a screen-space fake stack: chromatic damage, waterline refraction, dirt, cracks, brine fog, and tier-gated light shafts.

Exact Microseconds saved: Not certified. CPU cost is 0.0 us/frame. GPU cost is finite-check ALU only; exact GPU microseconds require Unity profiler.

Build/validation: Uber post risk scan now reports only the `rawUv` finite-boundary assignment; no `#pragma target 4.5`, `pow`, raw `saturate(_...)`, direct arithmetic `/`, `tex2D`, compute tokens, RW resources, or `GrabPass`. Forbidden hot-path scan is clean for touched visor/refraction files. `git diff --check` reports no whitespace errors, only LF/CRLF warnings. SourceLink-disabled build reaches C# and currently fails outside VFX/POST with `TetherManager.cs(20,92) CS0535 TetherManager does not implement ISlowTickable.SlowTick()`. Unity runtime shader compile, platform shader compile, Frame Debugger order, and profiler timings remain pending.

## 2026-05-17 - Screen Params Boundary Closure

What was wrong: The last shader audit still found helper hash/static paths multiplying raw UVs by `_ScreenParams`. Those values are normally stable, but they are still direct GPU sampling/noise boundaries and fail the mobile NaN-vaccination standard.

What was done: Added shared `HectonFinite2` to `Hecton_SnellRefractionCore.hlsl`. `Hecton_VisorFluidDistortion.shader` now sanitizes fullscreen stereo UVs, interleaved-gradient UVs, dust scratches, suspended-silt specks, and glitch static screen params. `SuitVisor.shader` now sanitizes frost/lens/grime/scratch/crack helper UVs, stereo `screenUV`, HUD-distorted UVs after glitch offsets, and all touched raw `_ScreenParams` / `_ScaledScreenParams` hash paths through finite aliases.

Cinematic Cheats used: No simulation was added. Low tier keeps chromatic/depth/dirt fakes. High/Ultra keep the visual overkill stack: salt crystals, suspended silt, lens grime, pressure cracks, HUD glitch, VR comfort, and BIOS recovery polish, now behind finite screen-space inputs.

Exact Microseconds saved: 0 us certified. CPU cost is 0.0 us/frame. GPU cost is finite-check ALU only; exact GPU microseconds require Unity profiler.

Build/validation: Targeted `_ScreenParams` scan now reports only finite alias declarations; no raw `_ScreenParams.xy`, `_ScreenParams.yx`, `_ScreenParams.y`, or `_ScaledScreenParams.xy` use remains in touched visor/refraction shaders. Broader shader risk scan reports only the `SuitVisor.shader` file header comment. Forbidden hot-path scan is clean for touched files. `git diff --check` reports no whitespace errors, only LF/CRLF warnings. SourceLink-disabled build reaches C# and currently fails outside VFX/POST with `FaunaBrain.Compatibility.cs(109,6) CS0246 FlagsAttribute/Flags could not be found`; warning is duplicate `System.Runtime.CompilerServices` using in `HectonPlayerMovement.cs`. Unity runtime shader compile, platform shader compile, Frame Debugger order, and profiler timings remain pending.

## 2026-05-17 - Shared Depth Boundary Closure

What was wrong: Scene-depth reads still trusted raw `SampleSceneDepth`, `_ZBufferParams`, `LinearEyeDepth`, and reconstructed world positions before feeding refraction masks, sonar contour masks, brine fog, mobile waterline math, and the high-tier shaft fake.

What was done: Added shared depth helpers in `Hecton_SnellRefractionCore.hlsl`: `HectonFinite3`, `HectonInvalidSceneRawDepth`, `HectonFiniteSceneRawDepth`, and `HectonSceneDepthValid01`. Fullscreen fluid, SuitVisor, and Uber post now sanitize raw depth, finite-guard `_ZBufferParams`, and fail reconstructed world positions to stable values before depth-dependent math.

Cinematic Cheats used: No physical water, raytracing, particles, or new textures. Low tier still drops to cheap chromatic/depth/dirt fakes. High/Ultra keep fake shafts, brine fog, salt crystals, silt shimmer, HUD glitch, grime, and cracks behind finite depth gates.

Exact Microseconds saved: 0 us certified. CPU cost is 0.0 us/frame. GPU cost is finite-check ALU only; exact GPU microseconds require Unity profiler.

Build/validation: Depth scan shows `SampleSceneDepth` wrapped by `HectonFiniteSceneRawDepth`, `LinearEyeDepth` using finite `zBufferParams`, and depth-valid reversed-Z logic centralized in the shared helper. Broader risk scan reports only intentional shared helper depth checks plus the `SuitVisor.shader` file header comment. Forbidden hot-path scan is clean for touched files. `git diff --check` reports no whitespace errors, only LF/CRLF warnings. SourceLink-disabled build retry timed out after 184 seconds under shared `dotnet` contention; an unrelated `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /maxcpucount:1 -nr:false` remained active and was not killed. Unity runtime shader compile, platform shader compile, Frame Debugger order, and profiler timings remain pending.
