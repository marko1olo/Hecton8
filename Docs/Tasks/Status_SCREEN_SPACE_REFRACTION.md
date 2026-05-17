# Status_SCREEN_SPACE_REFRACTION

Prompt: SCREEN_SPACE_REFRACTION
Role: VFX_TECHNICAL_ARTIST
Domain: Assets/_Project/Art/Shaders/Post/
Task count: 18
Status: CORE COMPLETE / CURRENT CSHARP BUILD BLOCKED OUTSIDE VFX/POST / UNITY RUNTIME PENDING

## Mandates Read

- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- REND_URP_Graphics_HotPath_Optimization_HLOD.txt
- REND_Shader_Noir_Aesthetics_Dithering_Fog.txt
- REND_DescriptorBinding_Reality_Check.txt
- REND_VR_Stencil_Masking.txt
- CORE_Damage_System_Hull_Integrity_VFX_Feedback.txt

## Batch Prompt

Extracted from Docs/Tasks/CURRENT_BATCH.md with id SCREEN_SPACE_REFRACTION.

## Loop Plan

- Loop 1: tasks 1-5, compile/static verification.
- Loop 2: tasks 6-10, compile/static verification.
- Loop 3: tasks 11-15, compile/static verification.
- Loop 4: tasks 16-18, compile/static verification.
- Loop 5: self-review, polish mandate, final compile/log.

## Checklist

- [x] 1. PURGE_SINGLETONS | Done | DOD: no singleton or new manager added; existing visor/render feature owners reused | Alternative rejected: new global refraction manager | Estimate: 0.0 us/frame
- [x] 2. DEBT_CLEANUP | Done | DOD: `rg GrabPass` found no first-party GrabPass shader/material debt; old blit utility path removed from fluid feature | Alternative rejected: legacy GrabPass / compatibility blit path | Estimate: saves one legacy copy path when feature is active; exact us pending profiler
- [x] 3. DATA_EVICTION | Done | DOD: `SuitVisor.shader` and fluid shader now sample `_CameraOpaqueTexture`; fluid RenderGraph pass declares `cameraOpaqueTexture` | Alternative rejected: fabricated scene surrogate / per-object capture | Estimate: avoids per-glass grab; exact us pending profiler
- [x] 4. BURST_ALGORITHM | Done | DOD: GPU-bound shader work only; no Burst/CPU algorithm created | Alternative rejected: CPU-side image deformation | Estimate: 0.0 us/frame CPU
- [x] 5. AUP_INTEGRITY | Done | DOD: all new math is screen-space/depth-space; no world position authority added | Alternative rejected: AUP/world-space refraction dependency | Estimate: 0.0 us/frame CPU
- [x] 6. DOD_SOA_LAYOUT | Done | DOD: compact IOR LUT added as `_HectonVisorIorLut` and `_HectonVisorFluidIorLut`, sanitized in C# and HLSL | Alternative rejected: material-cloned per-instance index data | Estimate: static ALU only; exact us pending profiler
- [x] 7. SIGNAL_FLOW | Done | DOD: `_HectonWaterDensitySignal` read from shader global first, then `GlobalRegistry.FluidSimulation` density fallback | Alternative rejected: hard dependency on fluid scene object | Estimate: one cached material scalar upload on change; exact us pending profiler
- [x] 8. LOW_TIER_FAKE | Done | DOD: MX350-class memory threshold and low-tier shader branches use chromatic-only opaque samples | Alternative rejected: full Snell normal perturbation on low-tier path | Estimate: saves high-path normal Snell ALU/sample blend when active; exact us pending profiler
- [x] 9. HIGH_END_OVERKILL | Done | DOD: clean glass and droplet masks perturb `_CameraOpaqueTexture` UV by bounded normal/refraction vector | Alternative rejected: raytrace/reflection probe physical refraction | Estimate: static extra opaque samples only where mask is active; exact us pending profiler
- [x] 10. REACTIVE_VFX | Done | DOD: hull stress drives jitter/chromatic fallback through existing `CurrentHullStress01` and `_HullStressFlicker` signals | Alternative rejected: simulated glass vibration physics | Estimate: shader-only fake; exact us pending profiler
- [x] 11. STP_STABILIZATION | Done | DOD: existing feature injection remains `BeforeRenderingPostProcessing`, keeping refraction before post/TAA/STP stack | Alternative rejected: after-post distortion shimmer | Estimate: 0.0 us/frame change
- [x] 12. NAN_VACCINATION | Done | DOD: all UV perturbations route through finite checks and `HectonClampUvOffset` with `[-0.1, 0.1]` hard cap | Alternative rejected: blind UV math | Estimate: static ALU only; exact us pending profiler
- [x] 13. BLACKBOX_LOGGING | Done | DOD: `HectonVisorFluidDistortionFeature` records a 300-frame packed DataVault telemetry ring via `BufferID.VisorRefractionBlackBox` and dumps `Docs/AgentLogs/Dump_SCREEN_SPACE_REFRACTION.bin` only on non-finite input | Alternative rejected: feature-owned persistent NativeArray or per-frame text logging | Estimate: 48 bytes/frame written when the player camera is evaluated; exact us pending profiler
- [x] 14. TRIPLE_STRIKE_REPAIR | Done | DOD: RenderGraph path migrated to `AddRasterRenderPass`; a later build verified the code compile boundary before a later unrelated `SubmarineFluidDynamics.cs` regression | Alternative rejected: editing unrelated dependency churn outside assigned domain | Estimate: 0.0 us/frame CPU topology change; exact render cost pending Unity profiler
- [x] 15. HOMEOSTASIS_ADAPTATION | Done | DOD: low-tier memory detection and hull-stress threshold force chromatic-only fallback | Alternative rejected: constant expensive path under load | Estimate: saves high-path Snell branch under fallback; exact us pending profiler
- [x] 16. DEPTH_TEST | Done | DOD: `SuitVisor.shader` compares scene depth against glass depth via `HectonDepthBehindMask`; fluid pass binds camera depth and fades by valid scene depth | Alternative rejected: full-screen blind distortion | Estimate: static depth sample/ALU; exact us pending profiler
- [x] 17. MASK_DIRT | Done | DOD: refraction strength is multiplied by inverse dirt/grime/frost/crack/dust masks | Alternative rejected: uniform distortion through dirty glass | Estimate: static ALU mask; exact us pending profiler
- [x] 18. FINAL_VALIDATION | Done with caveat | DOD: clean build checkpoints succeeded earlier before later shared-workspace regressions; latest SourceLink-disabled retry is blocked outside VFX/POST in `TetherManager.cs`; Unity shader/runtime/profiler verification remains pending | Alternative rejected: claiming runtime/profiler numbers from C# build validation or editing unrelated domains | Estimate: exact us pending profiler

## Verification Log

- Initial status file did not exist; created fresh for this batch.
- Rationale file did not exist; created fresh for this batch.
- Loop 1 compile check: `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` failed with unrelated `GlobalSignals.cs(580,50) CS0535 SignalLaneAdapter.FlushPreSimulation(bool,int)` missing. Build-server shutdown completed.
- Re-extracted `SCREEN_SPACE_REFRACTION` prompt from `Docs/Tasks/CURRENT_BATCH.md` with CLI regex after Loop 1.
- Loop 2 static check: `git diff --check` clean for touched refraction files except existing LF/CRLF warnings; `rg` confirmed LUT, water-density, low-tier, stress, depth, and dirt bindings.
- Loop 2 compile check: `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` failed in unrelated files: `FaunaBrain.cs(3421,35)`, `VehicleMotor.cs(1864,35)`, `HectonPlayerMotor.cs(1361,35)` all `uint` to `ushort`. Build-server shutdown completed.
- Loop 3 restore-state correction: `dotnet build ... --no-restore` hit `NETSDK1004` missing `Temp/obj/Hecton8.Core/project.assets.json`; ran `dotnet restore Hecton8.Core.csproj` successfully.
- Loop 3 compile check after restore failed in unrelated domains: `FaunaBrain.cs` missing `Hecton8.Core.Signals`, `PredatorCognitionDomain.cs` missing `Hecton8.AI.Perception`, `FaunaKinematicsRuntime.cs` missing animation fauna IK types, `GlobalRegistry.cs` missing `IResolutionScalerService`, and `HectonMarineSnowRenderer.cs` missing `IVehicleCommandSignalListener.OnVehicleCommandSignal`. Marked task 14 blocked by dependency.
- Build-server shutdown hung in this shared multi-agent workspace; killed only local `dotnet build-server shutdown` / `dotnet build Hecton8.Core.csproj --no-restore` child processes after verifying command lines. Other concurrent build processes were left alone.
- Re-extracted `SCREEN_SPACE_REFRACTION` prompt from `Docs/Tasks/CURRENT_BATCH.md` with CLI regex after Loop 3.
- Loop 4 static check: `rg` confirmed `_CameraOpaqueTexture`, `SampleSceneDepth`, `HectonDepthBehindMask`, `HectonInverseDirtMask`, and inverse dirt bindings in touched shader paths. `git diff --check` reported no whitespace errors, only LF/CRLF warnings on existing files.
- Loop 5 polish: read `[VI. OMEGA POLISH MANDATE]`; cannot honestly mark VERIFIED MASTER GRADE because Unity runtime shader/platform/profiler verification is still pending. Anti-bloat scan found no `AddBlitPass`, `RenderGraphUtils`, `GrabPass`, `GC.Alloc`, singleton `.Instance`, or Unity object search calls in touched code; the only native memory in visor code is a DataVault handle.

## Continuation Inquisition - 2026-05-16

- [x] 19. MULTIPLATFORM_AUDIT | Done | DOD: visor post shader uses `#pragma target 3.5`, no compute kernels/thread groups, no DX-only `tex2D`/group-memory path, and `VisorRefractionTelemetryEntry` uses `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 48)]` for ARM64/Quest packing | Alternative rejected: SM4.5 fragment target with no feature need | Estimate: 0.0 us/frame CPU; GPU target change only
- [x] 20. DATA_SOVEREIGNTY_REPAIR | Done | DOD: 300-frame heartbeat is stored in `GlobalRegistry.DataVault` under `SystemID.Vfx`; no `new NativeArray` owner was added | Alternative rejected: private persistent telemetry array | Estimate: 48 bytes/frame DataVault write on active player-camera evaluation; exact us pending profiler
- [x] 21. SIGNAL_AND_EVENT_AUDIT | Done | DOD: touched visor/refraction files contain no `EventBus`, managed delegate lane, `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, Unity object search, singleton `.Instance`, `AddBlitPass`, `RenderGraphUtils`, or `GrabPass` matches | Alternative rejected: adding a new water-density event or duplicate signal lane | Estimate: 0.0 us/frame
- [x] 22. GOD_MODE_VISOR_POLISH | Done | DOD: High/Ultra path drives `_HectonVisorFluidVisualOverkill` and adds ALU-only procedural salt-crystal growth on clean, depth-valid wet glass; Low/MX350 forces it to zero | Alternative rejected: raymarch/POM/particle systems inside this post pass | Estimate: 0.0 us/frame CPU; GPU ALU cost unmeasured and tier-gated
- [x] 23. CONTINUATION_VALIDATION | Done | DOD: re-extracted XML assignment, ran static audits, and a later `dotnet build` succeeded before a subsequent unrelated `SubmarineFluidDynamics.cs` compile regression | Alternative rejected: editing unrelated domains to fake completion | Estimate: 0.0 us/frame CPU, GPU exact us pending profiler

## Continuation Verification Log

- Re-extracted `SCREEN_SPACE_REFRACTION` prompt from `Docs/Tasks/CURRENT_BATCH.md` with CLI regex after continuation pass.
- Static multiplatform scan found no compute thread groups, group barriers, `tex2D`, `GrabPass`, `AddBlitPass`, `RenderGraphUtils`, standard `Update` methods, managed delegates, `EventBus`, or per-frame string formatting in touched visor/refraction files.
- Native memory scan found `NativeArray<VisorRefractionTelemetryEntry>` only as a DataVault alias returned by `vault.GetBuffer<...>(BufferID.VisorRefractionBlackBox, 300, SystemID.Vfx, ClearMemory)`. No `new NativeArray` owner or `H8Memory.Allocate` path was added.
- Fault I/O scan found `Path`, `Directory`, `FileStream`, and `BinaryWriter` only in `DumpBlackBoxOnce`, gated by `BlackBoxFlagNonFiniteInput`; no per-frame disk read/write path was added, preserving Steam Deck/MicroSD pressure.
- Continuation compile check: `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` failed outside this domain with 67 errors, first in `DiegeticGyroCompassRuntime.cs`, `LockstepStateValidator.cs`, `HomeostasisBrain.cs`, `PickupItem.cs`, and `TetherSignals.cs`.

## Sovereignty Recheck - 2026-05-16

- [x] 24. VAULT_HANDLE_EVICTION | Done | DOD: removed the visor feature's `NativeArray<VisorRefractionTelemetryEntry>` field and local declaration; blackbox now uses `VaultBufferHandle<VisorRefractionTelemetryEntry>` and resolves a pointer through `IDataVault.ResolveBuffer` | Alternative rejected: retaining a DataVault alias with a `NativeArray` type in the system file | Estimate: same 48-byte heartbeat write; exact us pending profiler
- [x] 25. STATELESS_RING_INDEX | Done | DOD: removed private telemetry cursor and last-frame fields; ring slot is derived from `Time.frameCount % blackBoxLength` | Alternative rejected: feature-owned cursor state | Estimate: saves two field reads/writes per evaluated player-camera frame; exact us unmeasured
- [x] 26. RETRY_VALIDATION | Done | DOD: re-ran static audit and a later `dotnet build`; visor/refraction files have zero `NativeArray` tokens and no forbidden hot-path patterns, with that validation later superseded by the `SubmarineFluidDynamics.cs` blocker below | Alternative rejected: modifying XR, biolum, vault diagnostics, audio, or submarine structural files | Estimate: 0.0 us/frame CPU, GPU exact us pending profiler

## Sovereignty Verification Log

- `rg NativeArray Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs` returned no matches after replacing the blackbox alias with `VaultBufferHandle`.
- Domain hot-path scan returned no `EventBus`, managed delegate lane, standard `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, Unity object search, singleton `.Instance`, `AddBlitPass`, `RenderGraphUtils`, `GrabPass`, compute thread groups, group barriers, or DX-only `tex2D` in touched visor/refraction files.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:ContinuousIntegrationBuild=false -p:EnableSourceControlManagerQueries=false -v:minimal -clp:Summary` failed outside domain with 39 errors, first in `HectonXRRuntimeState.cs`, `BiolumPulseSyncRuntime.cs`, `VaultProbeUtility.cs`, `SpatialAudioManager.cs`, and `SubmarineStructuralGrid.cs`.

## Visual Overkill Recheck - 2026-05-16

- [x] 27. VISOR_SILT_OVERKILL | Done | DOD: added High/Ultra-gated `ComputeSuspendedSiltMask` to the fullscreen visor post shader; Low/MX350 remains zero through `_HectonVisorFluidVisualOverkill` | Alternative rejected: actual particle system or fluid wake dependency in VFX/POST | Estimate: 0.0 us/frame CPU; GPU ALU only, exact us pending profiler
- [x] 28. OVERKILL_STATIC_AUDIT | Done | DOD: `git diff --check` clean for the shader change, and forbidden hot-path scan still finds no forbidden `Update`, `string.Format`, `EventBus`, `GrabPass`, `RenderGraphUtils`, `AddBlitPass`, compute thread groups, group barriers, or `tex2D` in touched visor/refraction files | Alternative rejected: adding texture samples or compute dispatch | Estimate: no CPU cost, GPU cost unmeasured
- [x] 29. POST_SILT_BUILD_RETRY | Done | DOD: build was retried after the shared SourceLink lock cleared and succeeded at that checkpoint with 0 warnings and 0 errors | Alternative rejected: killing unknown concurrent agent processes | Estimate: 0.0 us/frame CPU, GPU exact us pending profiler

## NaN Hardening Recheck - 2026-05-16

- [x] 30. SHADER_UNIFORM_NAN_HARDENING | Done | DOD: refraction-critical visor uniforms now use `HectonFinite01` or explicit `isfinite` before driving Snell strength, visual overkill, wetness, stress, rain, lightning, dust, and thermal gates | Alternative rejected: trusting `saturate()` on non-finite shader inputs | Estimate: GPU ALU only, exact us pending profiler
- [x] 31. NAN_STATIC_AUDIT | Done | DOD: targeted scan no longer finds raw `saturate()` on the high-risk refraction uniforms; forbidden hot-path scan still finds no local `NativeArray`, `EventBus`, managed delegate lane, `Update`, `string.Format`, `GrabPass`, `AddBlitPass`, compute thread groups, group barriers, or `tex2D` in touched visor/refraction files | Alternative rejected: broad out-of-domain shader rewrite | Estimate: 0.0 us/frame CPU
- [x] 32. SNELL_CORE_FINITE_GUARD | Done | DOD: shared Snell helper now finite-guards `nDotV` and `strength` before bend/amplitude math, so downstream shader callers cannot pass NaN through the common UV-offset path | Alternative rejected: guarding only per-call-site uniforms | Estimate: GPU ALU only, exact us pending profiler
- [x] 33. POST_NAN_BUILD_RETRY | Done | DOD: build retried after NaN hardening and succeeded at that checkpoint with 0 warnings and 0 errors | Alternative rejected: killing unknown concurrent agent processes | Estimate: 0.0 us/frame CPU, GPU exact us pending profiler

## NaN Hardening Verification Log

- Re-extracted `SCREEN_SPACE_REFRACTION` prompt from `Docs/Tasks/CURRENT_BATCH.md` before the continuation edit.
- Targeted shader scan confirmed raw high-risk refraction uniform gates were replaced by finite-safe gates in `Hecton_VisorFluidDistortion.shader` and `SuitVisor.shader`.
- Shared Snell core scan confirmed `HectonSnellBend01` now uses `HectonFinite01(nDotV)` and `HectonSnellUvOffset` zeros non-finite `strength` before applying amplitude.
- Forbidden-pattern scan returned no `NativeArray`, `EventBus`, managed delegate lane, standard `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, Unity object search, singleton `.Instance`, `AddBlitPass`, `RenderGraphUtils`, `GrabPass`, compute thread groups, group barriers, or DX-only `tex2D` in touched visor/refraction files.
- `git diff --check` reported no whitespace errors for the touched shader/log files, only existing LF/CRLF warnings.
- Previous `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:ContinuousIntegrationBuild=false -p:EnableSourceControlManagerQueries=false -v:minimal -clp:Summary` checkpoint succeeded: `Hecton8.Core -> C:\hades\Hecton8\Temp\bin\Debug\Hecton8.Core.dll`, 0 warnings, 0 errors, 00:01:20.37.

## Common Helper Recheck - 2026-05-16

- [x] 34. SNELL_COMMON_DEPTH_LUT_GUARD | Done | DOD: `Hecton_SnellRefractionCore.hlsl` now finite-guards raw IOR LUT components, depth inputs, softness, clamp bounds, `nDotV`, and Snell strength before `max`, `smoothstep`, `rcp`, or UV math | Alternative rejected: relying on C# sanitization or per-call-site guards only | Estimate: GPU ALU only, exact us pending profiler
- [x] 35. WATER_DENSITY_BLACKBOX_FLAG | Done | DOD: non-finite shader-global or fluid-simulation water density now sets `BlackBoxFlagNonFiniteInput` before sanitizing to zero and can trigger the 300-frame dump path | Alternative rejected: silently dropping invalid cross-domain water density | Estimate: two finite checks when player camera is evaluated; exact us pending profiler
- [x] 36. POST_COMMON_GUARD_BUILD_RETRY | BLOCKED BY DEPENDENCY | DOD: static scans passed after common-helper guard; `dotnet build` retried and now fails outside this domain in `SubmarineFluidDynamics.cs` on missing `VaultNativeBuffer<>` | Alternative rejected: editing submarine/fluid ownership to force a green build | Estimate: blocker, no frame estimate

## Common Helper Verification Log

- Shared Snell core stale-pattern scan found no old raw `rawIorLut` max chain, raw depth `smoothstep`, raw clamp-bound `min(max(abs(maxComponentAbs)))`, or parameterless `ResolveWaterDensitySignal01()` call.
- Forbidden-pattern scan returned no `NativeArray`, `EventBus`, managed delegate lane, standard `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, Unity object search, singleton `.Instance`, `AddBlitPass`, `RenderGraphUtils`, `GrabPass`, compute thread groups, group barriers, or DX-only `tex2D` in touched visor/refraction files.
- `git diff --check` reported no whitespace errors for touched shader/code/log files, only existing LF/CRLF warnings.
- That `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:ContinuousIntegrationBuild=false -p:EnableSourceControlManagerQueries=false -v:minimal -clp:Summary` retry failed outside this domain with 22 errors, all `SubmarineFluidDynamics.cs(614-635) CS0246 VaultNativeBuffer<> could not be found`.

## Shader Vector Guard Recheck - 2026-05-16

- [x] 37. SHADER_VECTOR_GLOBAL_GUARD | Done | DOD: fullscreen visor shader now resolves finite local-velocity, rain-parameter, and wind vectors before droplet flow, silt drift, rain exposure, and `rsqrt` wind direction math | Alternative rejected: relying only on C# material sanitation for GPU-side vector globals | Estimate: GPU ALU only, exact us pending profiler
- [x] 38. VECTOR_GUARD_STATIC_AUDIT | Done | DOD: raw `_HectonVisorFluidLocalVelocity`, `_GlobalWind`, and `_HectonScreenSpaceRainParams` use is now limited to uniform declarations and `ResolveFinite4` boundary reads; forbidden-pattern scan remains clean for touched visor/refraction files | Alternative rejected: broad whole-shader rewrite outside the refraction pass | Estimate: 0.0 us/frame CPU
- [x] 39. POST_VECTOR_GUARD_BUILD_RETRY | BLOCKED BY DEPENDENCY | DOD: `dotnet build` retried after vector guard; it now fails outside this domain in `PredatorCognitionDomain.cs` and `DroneFleetManager.cs` | Alternative rejected: editing fauna or construction ownership to force a green build | Estimate: blocker, no frame estimate

## Shader Vector Guard Verification Log

- Vector-global scan confirms `_HectonVisorFluidLocalVelocity`, `_GlobalWind`, and `_HectonScreenSpaceRainParams` are read through `ResolveFinite4` before flow/rain math.
- Forbidden-pattern scan returned no `NativeArray`, `EventBus`, managed delegate lane, standard `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, Unity object search, singleton `.Instance`, `AddBlitPass`, `RenderGraphUtils`, `GrabPass`, compute thread groups, group barriers, or DX-only `tex2D` in touched visor/refraction files.
- `git diff --check` reported no whitespace errors for touched shader/code/log files, only existing LF/CRLF warnings.
- That `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:ContinuousIntegrationBuild=false -p:EnableSourceControlManagerQueries=false -v:minimal -clp:Summary` retry failed outside this domain with 17 errors, first in `PredatorCognitionDomain.cs` (`NativeArray<float3>.Clear`, `AsParallelWriter`, missing `_speciesTuningById`) and `DroneFleetManager.cs` (`double3` to `float3` conversion).

## Shared Build Lock Recheck - 2026-05-16

- [x] 40. POST_DOC_BUILD_RETRY | BLOCKED BY SHARED LOCK | DOD: retried `dotnet build` twice after documentation closure; both attempts failed before C# because `Temp/obj/Hecton8.Core/Hecton8.Core.sourcelink.json` is locked by another process | Alternative rejected: terminating unknown concurrent agents' `dotnet build` processes | Estimate: blocker, no frame estimate

## Shared Build Lock Verification Log

- `Get-CimInstance Win32_Process -Filter "name = 'dotnet.exe'"` shows multiple concurrent `dotnet build Hecton8.Core.csproj` processes in the shared workspace. They were not killed.
- That build command failed with 0 warnings and 1 error: `Microsoft.SourceLink.Common.targets(56,5)` could not write `Temp/obj/Hecton8.Core/Hecton8.Core.sourcelink.json` because another process held the file.

## Shader Scalar Guard Recheck - 2026-05-16

- [x] 41. MESH_VISOR_GLOBAL_SCALAR_GUARD | Done | DOD: `SuitVisor.shader` now resolves HECTON HUD/VR/health/foveation vectors and glitch seed through finite helpers before final visor color, HUD distortion, chromatic split, and foveated dither math | Alternative rejected: trusting raw shader globals after refraction-only scalar hardening | Estimate: GPU ALU only, exact us pending profiler
- [x] 42. FLUID_POST_SCALAR_GUARD | Done | DOD: `Hecton_VisorFluidDistortion.shader` now finite-guards scalar material knobs for droplet scale, runoff speed, edge exponent, streak strengths, distortion strength, depth softness, low-tier/homeostasis flags, Snell strength, water-density input, and ambient dust response | Alternative rejected: relying only on C# material sanitation | Estimate: GPU ALU only, exact us pending profiler
- [x] 43. POST_SCALAR_GUARD_BUILD_RETRY | BLOCKED BY DEPENDENCY | DOD: `dotnet build` retried after scalar guard and now fails outside this domain in `TetherManager.cs(266,58)` on missing `TetherSignals.TetherFireRequest` | Alternative rejected: editing tether signal ownership to force a green build | Estimate: blocker, no frame estimate

## Shader Scalar Guard Verification Log

- Raw HECTON-global scan found no remaining `saturate(_Hecton...)`, raw `_Hecton...xyz/w` component access, or direct raw HECTON vector arithmetic in `SuitVisor.shader`; fullscreen fluid scalar uses are routed through `HectonFinite01`, `ResolveFiniteScalar`, `ResolveFiniteNonNegative`, or `ResolveFinite4`.
- Forbidden-pattern scan returned no `NativeArray`, `EventBus`, managed delegate lane, standard `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, Unity object search, singleton `.Instance`, `AddBlitPass`, `RenderGraphUtils`, `GrabPass`, compute thread groups, group barriers, or DX-only `tex2D` in touched visor/refraction files.
- `git diff --check` reported no whitespace errors for touched shader/code/log files, only existing LF/CRLF warnings.
- That `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:ContinuousIntegrationBuild=false -p:EnableSourceControlManagerQueries=false -v:minimal -clp:Summary` retry failed outside this domain with 0 warnings and 1 error: `TetherManager.cs(266,58) CS0426 TetherFireRequest does not exist in TetherSignals`.

## Shared Helper Recheck - 2026-05-16

- [x] 44. SHARED_FINITE_HELPER_CONSOLIDATION | Done | DOD: moved scalar/vector finite fallback helpers into `Hecton_SnellRefractionCore.hlsl` as `HectonFiniteValue`, `HectonFiniteNonNegative`, and `HectonFinite4`; removed duplicate local helper implementations from both visor shaders | Alternative rejected: two shader-local helper families drifting independently | Estimate: no CPU cost; GPU ALU unchanged in kind, exact us pending profiler
- [x] 45. SHARED_HELPER_STATIC_AUDIT | Done | DOD: stale helper scan finds no remaining `ResolveFinite4`, `ResolveFiniteScalar`, `ResolveFiniteNonNegative`, `HectonResolveFinite4`, or `HectonResolveFiniteScalar`; raw HECTON-global scan remains clean for targeted visor patterns | Alternative rejected: manual review without grep evidence | Estimate: 0.0 us/frame CPU
- [x] 46. POST_SHARED_HELPER_BUILD_RETRY | BLOCKED BY DEPENDENCY | DOD: first retry hit a shared SourceLink lock; SourceLink-disabled retry reached C# and failed outside VFX/POST in `DiegeticGyroCompassRuntime.cs` and `EcosystemDirector.cs` | Alternative rejected: editing UI/navigation/world ownership or deleting shared build intermediates | Estimate: blocker, no frame estimate

## Shared Helper Verification Log

- `rg` confirmed both visor shaders now consume the shared finite helpers from `Assets/_Project/Art/Shaders/Post/Hecton_SnellRefractionCore.hlsl`; no local duplicate finite-helper names remain.
- Forbidden-pattern scan remains clean for touched visor/refraction files.
- `git diff --check` reported no whitespace errors for touched shader/code/log files, only existing LF/CRLF warnings.
- That `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:ContinuousIntegrationBuild=false -p:EnableSourceControlManagerQueries=false -p:EnableSourceLink=false -v:minimal -clp:Summary` retry failed outside this domain with 0 warnings and 23 errors, first in `DiegeticGyroCompassRuntime.cs` (`DumpBlackBoxOnce`, `ResolveVelocity`, missing `_blackBoxCursor`/AUP fields) and `EcosystemDirector.cs` (`NativeArrayUnsafeUtility`/upload generic type inference).

## Dynamic Division Guard Recheck - 2026-05-16

- [x] 47. DYNAMIC_DIVISION_GUARD | Done | DOD: replaced dynamic shader `/` operations for screen texel size, sonar wave/fade timing, foveated quantization, droplet cell normalization, and fluid radial direction with bounded `rcp` paths | Alternative rejected: relying on implicit nonzero engine uniforms | Estimate: GPU ALU shape equivalent; exact us pending profiler
- [x] 48. DIVISION_STATIC_AUDIT | Done | DOD: remaining slash scan in visor/refraction shaders is limited to literal-constant lookup divisions and include/comment text; dynamic denominators now use `rcp(max(...))` or pre-sanitized `rsqrt(max(...))` | Alternative rejected: manual-only NaN review | Estimate: 0.0 us/frame CPU
- [x] 49. POST_DIVISION_BUILD_RETRY | Done with warning | DOD: SourceLink-disabled `dotnet build` succeeded: `Hecton8.Core -> Temp/bin/Debug/Hecton8.Core.dll`, 1 warning, 0 errors | Alternative rejected: killing the compiler process that still locks `Hecton8.Core.sourcelink.json` | Estimate: build validation only

## Dynamic Division Guard Verification Log

- Dynamic slash audit confirms screen texel, sonar, foveated quantization, droplet grid normalization, and fluid radial direction use guarded reciprocal math. Remaining `/` hits are constant Bayer/HUD-box denominators or comments/includes.
- Forbidden-pattern scan returned no `NativeArray`, `EventBus`, managed delegate lane, standard `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, Unity object search, singleton `.Instance`, `AddBlitPass`, `RenderGraphUtils`, `GrabPass`, compute thread groups, group barriers, or DX-only `tex2D` in touched visor/refraction files.
- `git diff --check` reported no whitespace errors for touched shader/code/log files, only existing LF/CRLF warnings.
- Latest `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:ContinuousIntegrationBuild=false -p:EnableSourceControlManagerQueries=false -p:EnableSourceLink=false -v:minimal -clp:Summary` succeeded with 1 warning and 0 errors. Warning: `MSB3061` could not delete `Temp/obj/Hecton8.Core/Hecton8.Core.sourcelink.json` because `csc (67600)` holds it.

## Literal Division And Boundary Recheck - 2026-05-16

- [x] 50. LITERAL_DIVISION_NOISE_PURGE | Done | DOD: replaced Bayer table `/ 16.0` constants and the fixed HUD battery-box `/ float2(0.17, 0.055)` normalization with multiply constants; arithmetic slash scan now leaves only shader names, include paths, and comments | Alternative rejected: leaving benign constants that keep failing automated divide audits | Estimate: GPU ALU equivalent; exact us pending profiler
- [x] 51. MESH_VISOR_FINITE_BOUNDARY | Done | DOD: `SuitVisor.shader` now finite-guards approximate normalization inputs, strongest-light signal, screen-position/depth W, HUD close occlusion range, glass alpha, static/hazard/bios knobs, grime/mask/crack controls, and refraction-adjacent material gates; fullscreen fluid post pass now zeroes non-finite base offsets before Snell normalization | Alternative rejected: relying on material ranges or engine screen/depth uniforms as proof of NaN safety | Estimate: GPU ALU only, exact us pending profiler
- [x] 52. POST_LITERAL_GUARD_BUILD_RETRY | BLOCKED BY DEPENDENCY | DOD: SourceLink-disabled and normal build checkpoints briefly succeeded with 0 warnings and 0 errors, but latest retries now fail outside VFX/POST in `EcosystemDirector.cs` duplicate members and `LockstepStateValidator.cs` missing lane constants | Alternative rejected: editing world/determinism ownership to force a green build | Estimate: blocker, no frame estimate

## Literal Division And Boundary Verification Log

- Initial normal build retry reached C# and failed outside this domain with `TetherInstance.cs(1649,17)` and `TetherInstance.cs(2592,17)` missing `IsFrameCooldownActive`; no tether files were edited.
- After shader cleanup, `rg -n "[^/]/[^/]|saturate\(_GlassAlpha|saturate\(_LensGrimeIntensity|saturate\(_WaterDropletMaskInfluence|saturate\(_PressureLensCrackIntensity|abs\(_StaticNoise|saturate\(_Hazard|saturate\(_BiosRecoveryMode|max\(_HudCloseOcclusionDistance|/ 16\.0|/ float2\(0\.17|_StaticNoise \*"` on the touched shaders reports only shader names, include paths, and comments.
- Forbidden-pattern scan returned no `NativeArray`, `EventBus`, managed delegate lane, standard `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, Unity object search, singleton `.Instance`, `AddBlitPass`, `RenderGraphUtils`, `GrabPass`, compute thread groups, group barriers, or DX-only `tex2D` in touched visor/refraction files.
- `git diff --check` reported no whitespace errors for touched shader/code files, only existing LF/CRLF warnings.
- Checkpoint `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:ContinuousIntegrationBuild=false -p:EnableSourceControlManagerQueries=false -p:EnableSourceLink=false -v:minimal -clp:Summary` succeeded: `Hecton8.Core -> Temp/bin/Debug/Hecton8.Core.dll`, 0 warnings, 0 errors, elapsed 00:00:01.01.
- Checkpoint `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:ContinuousIntegrationBuild=false -p:EnableSourceControlManagerQueries=false -v:minimal -clp:Summary` succeeded: `Hecton8.Core -> Temp/bin/Debug/Hecton8.Core.dll`, 0 warnings, 0 errors, elapsed 00:00:01.90.
- Latest normal retry now fails outside VFX/POST with 5 errors in `EcosystemDirector.cs(5970-6027)`: duplicate `ResolveVaultIndexCapacity`, `ClearIndexEntries`, `TryUpsertIndexEntry`, and `TryFindIndexEntry` members.
- Latest SourceLink-disabled retry now fails outside VFX/POST with 8 errors in `LockstepStateValidator.cs(408-417)`: missing `LockstepSnapshotSignalCapacity`, `LockstepSnapshotLaneHash`, `SystemGlitchSignalCapacity`, and `SystemGlitchLaneHash`.
- Unity runtime shader compilation, RenderGraph Frame Debugger ordering, platform shader compilation on Quest/Android/Metal/Steam Deck, and exact profiler microseconds remain pending.

## Uber Post And Suit Raw Uniform Recheck - 2026-05-16

- [x] 53. UBER_POST_PLATFORM_TARGET | Done | DOD: `HectonVisorUberPost.shader` dropped from `#pragma target 4.5` to `#pragma target 3.5` after static audit found no compute, UAV, group memory, or SM4.5-only path | Alternative rejected: carrying a higher shader model without a feature requirement | Estimate: 0.0 us/frame CPU; platform shader compile pending
- [x] 54. UBER_POST_LOW_TIER_SHED | Done | DOD: non-mobile 16-tap light shafts now return zero when `_HectonUberLowTier` is active, and per-tap `pow` was replaced by `FastRadialFalloff01` polynomial falloff | Alternative rejected: letting MX350 pay high-sample post cost or using real volumetric shafts in toaster mode | Estimate: saves the 16-tap shaft loop on low tier; exact GPU us pending profiler
- [x] 55. UBER_AND_SUIT_FINITE_BOUNDARY | Done | DOD: Uber post now consumes shared finite helpers for screen params, waterline, brine, light shaft, comfort, dirt, crack, pressure, heat, hypoxia, bleeding, and UV offset boundaries; `SuitVisor.shader` now finite-guards droplet density, chromatic strength, sonar vectors, hypoxia, HUD tint alpha, smoothness, reflection strength, and screen-size static noise | Alternative rejected: trusting material ranges or Unity global params as final GPU boundary | Estimate: GPU ALU only; exact us pending profiler
- [x] 56. POST_UBER_BUILD_RETRY | BLOCKED BY DEPENDENCY | DOD: SourceLink-disabled `dotnet build` reached C# and failed outside VFX/POST in `SubmarineFluidDynamics.cs` ambiguous `float3`/`Vector3` subtraction | Alternative rejected: editing submarine/fluid ownership to force a green build | Estimate: blocker, no frame estimate

## Uber Post And Suit Raw Uniform Verification Log

- `rg` confirmed `HectonVisorUberPost.shader` has `#pragma target 3.5` and no `#pragma target 4.5`, `pow`, `tex2D`, `SV_Group`, `numthreads`, `groupshared`, `GroupMemoryBarrier`, `RWTexture`, `RWStructured`, or `GrabPass` hits.
- Targeted `SuitVisor.shader` scan found no remaining raw hot-path uses of `_WaterDropletDensity`, `_ScaledScreenParams.xy`, `_ChromaticAberration`, `saturate(_SonarGridParams0`, `_SonarRevealWaveParams.w`, `_SonarRevealOriginWS.xyz`, `saturate(_HypoxiaLevel`, `saturate(_HUD_Color.a`, `saturate(_Smoothness`, or `_EnvReflStrength` beyond declarations and finite boundary aliases.
- Broader shader risk scan for `#pragma target 4.5`, `pow`, raw `saturate(_...)`, `length`, direct arithmetic `/`, DX compute tokens, and `GrabPass` reports only benign shader file header text and `#pragma target 3.5` declarations in touched visor/refraction shaders.
- `git diff --check` reported no whitespace errors for touched shader/code files, only existing LF/CRLF warnings.
- Latest normal `dotnet build` retry failed before C# at SourceLink file lock: `Temp/obj/Hecton8.Core/Hecton8.Core.sourcelink.json` is held by another process.
- Latest SourceLink-disabled `dotnet build` retry failed outside VFX/POST with 2 errors: `SubmarineFluidDynamics.cs(1853,60)` and `(4582,68)` ambiguous operator resolution between `float3.operator -(float3,float3)` and `Vector3.operator -(Vector3,Vector3)`.
- Unity runtime shader compilation, RenderGraph Frame Debugger ordering, platform shader compilation on Quest/Android/Metal/Steam Deck, and exact profiler microseconds remain pending.

## Uber Fragment Boundary Recheck - 2026-05-17

- [x] 57. UBER_FRAGMENT_UV_FINITE_CLOSURE | Done | DOD: `HectonVisorUberPost.shader` now sanitizes the stereo-transformed fragment UV once in `Frag`; internal water, droplet, comfort, lens dirt, and brine fog helpers also fail closed on non-finite UV/world-position inputs | Alternative rejected: assuming XR stereo transform and world-position reconstruction can never return invalid coordinates | Estimate: GPU ALU only; exact us pending profiler
- [x] 58. POST_FRAGMENT_BOUNDARY_STATIC_AUDIT | Done | DOD: Uber post risk scan reports no `#pragma target 4.5`, `pow`, raw `saturate(_...)`, direct arithmetic `/`, `tex2D`, compute thread tokens, RW resources, or `GrabPass`; only `rawUv` finite-boundary assignment remains | Alternative rejected: manual-only shader review | Estimate: 0.0 us/frame CPU
- [x] 59. POST_FRAGMENT_BOUNDARY_BUILD_RETRY | BLOCKED BY DEPENDENCY | DOD: SourceLink-disabled `dotnet build` reached C# and failed outside VFX/POST in `TetherManager.cs(20,92)` missing `ISlowTickable.SlowTick()` | Alternative rejected: editing tether ownership to force a green build | Estimate: blocker, no frame estimate

## Uber Fragment Boundary Verification Log

- `rg -n "#pragma target 4\.5|pow\(|saturate\(_|abs\(_|max\(_|min\(_|length\(|/ |tex2D|SV_Group|numthreads|groupshared|GroupMemoryBarrier|RWTexture|RWStructured|GrabPass|ComputeWorldSpacePosition\(uv|floor\(uv"` on `HectonVisorUberPost.shader` reports only `rawUv` assignment and finite-boundary sanitization lines.
- Forbidden hot-path scan returned no `NativeArray`, `EventBus`, managed delegate lane, standard `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, Unity object search, singleton `.Instance`, `AddBlitPass`, `RenderGraphUtils`, `GrabPass`, compute thread groups, group barriers, or DX-only `tex2D` in touched visor/refraction files.
- `git diff --check` reported no whitespace errors for touched shader/code/log files, only existing LF/CRLF warnings.
- Latest SourceLink-disabled `dotnet build Hecton8.Core.csproj --no-restore` failed outside VFX/POST with 1 error: `TetherManager.cs(20,92) CS0535 TetherManager does not implement ISlowTickable.SlowTick()`.
- Unity runtime shader compilation, RenderGraph Frame Debugger ordering, platform shader compilation on Quest/Android/Metal/Steam Deck, and exact profiler microseconds remain pending.

## Screen Params Boundary Recheck - 2026-05-17

- [x] 60. SHARED_FINITE2_HELPER | Done | DOD: added `HectonFinite2` to `Hecton_SnellRefractionCore.hlsl` so float2 UV/screen-space boundaries share the same fail-closed shader helper family as scalar/vector4 refraction guards | Alternative rejected: duplicating local float2 guard code in each shader | Estimate: GPU ALU only; exact us pending profiler
- [x] 61. VISOR_SCREEN_PARAM_UV_CLOSURE | Done | DOD: `Hecton_VisorFluidDistortion.shader` and `SuitVisor.shader` now sanitize fullscreen/stereo UVs, helper UVs, HUD-distorted UVs, tile scales, and `_ScreenParams`/`_ScaledScreenParams` use before hash/floor/static/noise paths | Alternative rejected: trusting engine screen params and interpolated UVs as always finite on mobile/Metal | Estimate: CPU 0.0 us/frame; GPU finite-check ALU only, exact us pending profiler
- [x] 62. POST_SCREEN_PARAM_BUILD_RETRY | BLOCKED BY DEPENDENCY | DOD: static shader and hot-path scans passed after screen-param closure; SourceLink-disabled `dotnet build` reached C# and failed outside VFX/POST in `FaunaBrain.Compatibility.cs` on missing `FlagsAttribute`/`Flags` | Alternative rejected: editing Fauna compatibility ownership to force a green build | Estimate: blocker, no frame estimate

## Screen Params Boundary Verification Log

- Targeted `_ScreenParams` scan now reports only finite boundary aliases in `HectonVisorUberPost.shader`, `SuitVisor.shader`, and `Hecton_VisorFluidDistortion.shader`; raw `_ScreenParams.xy`, `_ScreenParams.yx`, `_ScreenParams.y`, and `_ScaledScreenParams.xy` use no longer appear in touched visor/refraction shaders.
- Broader shader risk scan for `#pragma target 4.5`, `pow`, raw `saturate(_...)`, direct arithmetic `/`, DX compute tokens, `GrabPass`, and raw `_ScreenParams` paths reports only the `SuitVisor.shader` file header comment.
- Forbidden hot-path scan returned no `NativeArray`, `EventBus`, managed delegate lane, standard `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, Unity object search, singleton `.Instance`, `AddBlitPass`, `RenderGraphUtils`, `GrabPass`, compute thread groups, group barriers, or DX-only `tex2D` in touched visor/refraction files.
- `git diff --check` reported no whitespace errors for touched shader/code/log files, only existing LF/CRLF warnings.
- Latest SourceLink-disabled `dotnet build Hecton8.Core.csproj --no-restore` failed outside VFX/POST with 1 warning and 2 errors: `FaunaBrain.Compatibility.cs(109,6) CS0246 FlagsAttribute could not be found` and `FaunaBrain.Compatibility.cs(109,6) CS0246 Flags could not be found`. Warning: duplicate `System.Runtime.CompilerServices` using in `HectonPlayerMovement.cs`.
- Unity runtime shader compilation, RenderGraph Frame Debugger ordering, platform shader compilation on Quest/Android/Metal/Steam Deck, and exact profiler microseconds remain pending.

## Depth Boundary Recheck - 2026-05-17

- [x] 63. SHARED_DEPTH_VALIDITY_HELPERS | Done | DOD: added `HectonFinite3`, `HectonInvalidSceneRawDepth`, `HectonFiniteSceneRawDepth`, and `HectonSceneDepthValid01` to the shared Snell core so raw depth and world-position boundaries fail closed per reversed-Z mode | Alternative rejected: repeating platform-specific depth-valid preprocessor blocks in each shader | Estimate: GPU ALU only; exact us pending profiler
- [x] 64. VISOR_DEPTH_SAMPLE_CLOSURE | Done | DOD: fullscreen fluid, mesh visor, and Uber post now finite-sanitize `SampleSceneDepth`, `_ZBufferParams`, `LinearEyeDepth`, reconstructed world positions, sonar contour depth offsets, and low-tier/mobile waterline depth fallback before refraction/shaft/brine/sonar decisions | Alternative rejected: trusting depth buffer and `_ZBufferParams` as always finite on mobile/Metal | Estimate: CPU 0.0 us/frame; GPU finite-check ALU only, exact us pending profiler
- [x] 65. POST_DEPTH_BUILD_RETRY | BLOCKED BY VALIDATION CONTENTION | DOD: static shader and hot-path scans passed; SourceLink-disabled `dotnet build` was retried but timed out after 184 seconds while multiple concurrent `dotnet` builds were active in the shared workspace | Alternative rejected: killing unknown concurrent build processes or editing unrelated Fauna/tether/world systems | Estimate: validation blocker, no frame estimate

## Depth Boundary Verification Log

- Depth scan now shows `SampleSceneDepth` calls wrapped by `HectonFiniteSceneRawDepth`, `LinearEyeDepth` calls using finite `zBufferParams`, and raw `UNITY_REVERSED_Z`/depth-valid `step` logic centralized in `Hecton_SnellRefractionCore.hlsl`.
- Broader shader risk scan for `#pragma target 4.5`, `pow`, raw `saturate(_...)`, direct arithmetic `/`, DX compute tokens, raw `_ScreenParams` paths, raw `_ZBufferParams`, and raw depth-valid checks reports only the shared helper's intentional validity checks plus the `SuitVisor.shader` file header comment.
- Forbidden hot-path scan returned no `NativeArray`, `EventBus`, managed delegate lane, standard `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, Unity object search, singleton `.Instance`, `AddBlitPass`, `RenderGraphUtils`, `GrabPass`, compute thread groups, group barriers, or DX-only `tex2D` in touched visor/refraction files.
- `git diff --check` reported no whitespace errors for touched shader/code/log files, only existing LF/CRLF warnings.
- Latest SourceLink-disabled `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false ...` timed out after 184 seconds. Process inspection showed another `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /maxcpucount:1 -nr:false` still active; it was not killed.
- Unity runtime shader compilation, RenderGraph Frame Debugger ordering, platform shader compilation on Quest/Android/Metal/Steam Deck, and exact profiler microseconds remain pending.
