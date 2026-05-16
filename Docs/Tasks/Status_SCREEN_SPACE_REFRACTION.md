# Status_SCREEN_SPACE_REFRACTION

Prompt: SCREEN_SPACE_REFRACTION
Role: VFX_TECHNICAL_ARTIST
Domain: Assets/_Project/Art/Shaders/Post/
Task count: 18
Status: CORE COMPLETE / BUILD BLOCKED BY DEPENDENCY

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
- [x] 13. BLACKBOX_LOGGING | Done | DOD: verified N/A because this is non-critical visual post and no CPU simulation state is owned here | Alternative rejected: new NativeArray telemetry owner for shader-only post | Estimate: 0.0 us/frame CPU
- [x] 14. TRIPLE_STRIKE_REPAIR | BLOCKED BY DEPENDENCY | DOD: RenderGraph path migrated to `AddRasterRenderPass`; compile verification blocked after unrelated errors in core/fauna/gameplay/VFX domains | Alternative rejected: editing unrelated dependency churn outside assigned domain | Estimate: blocker, no frame estimate
- [x] 15. HOMEOSTASIS_ADAPTATION | Done | DOD: low-tier memory detection and hull-stress threshold force chromatic-only fallback | Alternative rejected: constant expensive path under load | Estimate: saves high-path Snell branch under fallback; exact us pending profiler
- [x] 16. DEPTH_TEST | Done | DOD: `SuitVisor.shader` compares scene depth against glass depth via `HectonDepthBehindMask`; fluid pass binds camera depth and fades by valid scene depth | Alternative rejected: full-screen blind distortion | Estimate: static depth sample/ALU; exact us pending profiler
- [x] 17. MASK_DIRT | Done | DOD: refraction strength is multiplied by inverse dirt/grime/frost/crack/dust masks | Alternative rejected: uniform distortion through dirty glass | Estimate: static ALU mask; exact us pending profiler
- [x] 18. FINAL_VALIDATION | BLOCKED BY DEPENDENCY | DOD: `dotnet build` attempted after restore and static shader scans completed; build fails in unrelated core/fauna/AI/animation/VFX files | Alternative rejected: chat-only completion or out-of-domain dependency edits | Estimate: 0.0 us/frame

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
- Loop 5 polish: read `[VI. OMEGA POLISH MANDATE]`; cannot honestly mark VERIFIED MASTER GRADE because `dotnet build` is blocked outside this task. Anti-bloat scan found no `AddBlitPass`, `RenderGraphUtils`, `GrabPass`, `new NativeArray`, `GC.Alloc`, singleton `.Instance`, or Unity object search calls in touched code.
