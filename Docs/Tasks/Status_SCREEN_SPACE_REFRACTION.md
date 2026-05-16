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
- [x] 13. BLACKBOX_LOGGING | Done | DOD: `HectonVisorFluidDistortionFeature` records a 300-frame packed DataVault telemetry ring via `BufferID.VisorRefractionBlackBox` and dumps `Docs/AgentLogs/Dump_SCREEN_SPACE_REFRACTION.bin` only on non-finite input | Alternative rejected: feature-owned persistent NativeArray or per-frame text logging | Estimate: 48 bytes/frame written when the player camera is evaluated; exact us pending profiler
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
- Loop 5 polish: read `[VI. OMEGA POLISH MANDATE]`; cannot honestly mark VERIFIED MASTER GRADE because `dotnet build` is blocked outside this task. Anti-bloat scan found no `AddBlitPass`, `RenderGraphUtils`, `GrabPass`, `GC.Alloc`, singleton `.Instance`, or Unity object search calls in touched code; the only `NativeArray` in visor code is a DataVault alias.

## Continuation Inquisition - 2026-05-16

- [x] 19. MULTIPLATFORM_AUDIT | Done | DOD: visor post shader uses `#pragma target 3.5`, no compute kernels/thread groups, no DX-only `tex2D`/group-memory path, and `VisorRefractionTelemetryEntry` uses `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 48)]` for ARM64/Quest packing | Alternative rejected: SM4.5 fragment target with no feature need | Estimate: 0.0 us/frame CPU; GPU target change only
- [x] 20. DATA_SOVEREIGNTY_REPAIR | Done | DOD: 300-frame heartbeat is stored in `GlobalRegistry.DataVault` under `SystemID.Vfx`; no `new NativeArray` owner was added | Alternative rejected: private persistent telemetry array | Estimate: 48 bytes/frame DataVault write on active player-camera evaluation; exact us pending profiler
- [x] 21. SIGNAL_AND_EVENT_AUDIT | Done | DOD: touched visor/refraction files contain no `EventBus`, managed delegate lane, `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, Unity object search, singleton `.Instance`, `AddBlitPass`, `RenderGraphUtils`, or `GrabPass` matches | Alternative rejected: adding a new water-density event or duplicate signal lane | Estimate: 0.0 us/frame
- [x] 22. GOD_MODE_VISOR_POLISH | Done | DOD: High/Ultra path drives `_HectonVisorFluidVisualOverkill` and adds ALU-only procedural salt-crystal growth on clean, depth-valid wet glass; Low/MX350 forces it to zero | Alternative rejected: raymarch/POM/particle systems inside this post pass | Estimate: 0.0 us/frame CPU; GPU ALU cost unmeasured and tier-gated
- [x] 23. CONTINUATION_VALIDATION | BLOCKED BY DEPENDENCY | DOD: re-extracted XML assignment, ran static audits, and ran `dotnet build`; compile fails outside VFX/POST in UI compass, lockstep, homeostasis, item pickup, and tether signal contracts | Alternative rejected: editing unrelated domains to fake completion | Estimate: blocker, no frame estimate

## Continuation Verification Log

- Re-extracted `SCREEN_SPACE_REFRACTION` prompt from `Docs/Tasks/CURRENT_BATCH.md` with CLI regex after continuation pass.
- Static multiplatform scan found no compute thread groups, group barriers, `tex2D`, `GrabPass`, `AddBlitPass`, `RenderGraphUtils`, standard `Update` methods, managed delegates, `EventBus`, or per-frame string formatting in touched visor/refraction files.
- Native memory scan found `NativeArray<VisorRefractionTelemetryEntry>` only as a DataVault alias returned by `vault.GetBuffer<...>(BufferID.VisorRefractionBlackBox, 300, SystemID.Vfx, ClearMemory)`. No `new NativeArray` owner or `H8Memory.Allocate` path was added.
- Fault I/O scan found `Path`, `Directory`, `FileStream`, and `BinaryWriter` only in `DumpBlackBoxOnce`, gated by `BlackBoxFlagNonFiniteInput`; no per-frame disk read/write path was added, preserving Steam Deck/MicroSD pressure.
- Continuation compile check: `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` failed outside this domain with 67 errors, first in `DiegeticGyroCompassRuntime.cs`, `LockstepStateValidator.cs`, `HomeostasisBrain.cs`, `PickupItem.cs`, and `TetherSignals.cs`.

## Sovereignty Recheck - 2026-05-16

- [x] 24. VAULT_HANDLE_EVICTION | Done | DOD: removed the visor feature's `NativeArray<VisorRefractionTelemetryEntry>` field and local declaration; blackbox now uses `VaultBufferHandle<VisorRefractionTelemetryEntry>` and resolves a pointer through `IDataVault.ResolveBuffer` | Alternative rejected: retaining a DataVault alias with a `NativeArray` type in the system file | Estimate: same 48-byte heartbeat write; exact us pending profiler
- [x] 25. STATELESS_RING_INDEX | Done | DOD: removed private telemetry cursor and last-frame fields; ring slot is derived from `Time.frameCount % blackBoxLength` | Alternative rejected: feature-owned cursor state | Estimate: saves two field reads/writes per evaluated player-camera frame; exact us unmeasured
- [x] 26. RETRY_VALIDATION | BLOCKED BY DEPENDENCY | DOD: re-ran static audit and `dotnet build`; visor/refraction files have zero `NativeArray` tokens and no forbidden hot-path patterns, while build fails outside domain | Alternative rejected: modifying XR, biolum, vault diagnostics, audio, or submarine structural files | Estimate: blocker, no frame estimate

## Sovereignty Verification Log

- `rg NativeArray Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs` returned no matches after replacing the blackbox alias with `VaultBufferHandle`.
- Domain hot-path scan returned no `EventBus`, managed delegate lane, standard `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, Unity object search, singleton `.Instance`, `AddBlitPass`, `RenderGraphUtils`, `GrabPass`, compute thread groups, group barriers, or DX-only `tex2D` in touched visor/refraction files.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -p:ContinuousIntegrationBuild=false -p:EnableSourceControlManagerQueries=false -v:minimal -clp:Summary` failed outside domain with 39 errors, first in `HectonXRRuntimeState.cs`, `BiolumPulseSyncRuntime.cs`, `VaultProbeUtility.cs`, `SpatialAudioManager.cs`, and `SubmarineStructuralGrid.cs`.
