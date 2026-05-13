# Status_CAUSTICS_PROJECTION_ENGINEER

Prompt: CAUSTICS_PROJECTION_ENGINEER
Role: VFX_TECHNICAL_ARTIST
Domain: VFX.Caustics / Analytical Light Transport
Task Count: 19
Status: PENDING VERIFICATION

## Mandates Selected Before Coding
- [x] ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- [x] ARCH_Project_Bootstrap_Sequence_Init_Safety.txt
- [x] MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- [x] GPU_Compute_Kernels_Kernels_Optimization_MX350.txt
- [x] GPU_Compute_Warp_Sizing_Mobile.txt
- [x] REND_GPU_Sovereignty.txt
- [x] OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- [x] DBG_Telemetry_Crash_Reporting_PostMortem.txt

## Phase 1
- [x] 1. SINGLETON ERADICATION | DOD: `rg` found no `CausticsManager.Instance`; added `ICausticsService` and `GlobalRegistry.RegisterCausticsService`; `GameBootstrapper` cold-reflects the isolated graphics service. Rejected: Core asmdef direct reference to Graphics.Caustics, because it creates a cycle. Estimate: 0 us/frame; bootstrap-only reflection.
- [x] 2. SIGNAL MIGRATION | DOD: `AnalyticalCausticsService` consumes `WeatherEvents` snapshot payload and derives cloud cover from weather intensity/storm state. Rejected: polling `HectonSurfaceWeatherDirector` every frame. Estimate: 0 us idle; one scalar update per weather event.
- [x] 3. ASMDEF ISOLATION | DOD: created `Assets/_Project/Scripts/Graphics/Caustics/Hecton8.Graphics.Caustics.asmdef` referencing Core contracts/memory. Rejected: leaving caustics inside monolithic Core. Estimate: compile-organization only.
- [x] 4. DEAD CODE HUNT | DOD: first-party caustics use no Unity `Projector`/`DecalProjector`; legacy `CausticsProjectorManager` now exits when `GlobalRegistry.Caustics` exists. Rejected: deleting the old script and risking missing prefab GUID fallout. Estimate: removes slow-tick override when service is active.

## Phase 2
- [x] 5. THE WAVE BINDING | DOD: `HectonFluidEngine` publishes its 16 Gerstner waves plus meta into `GlobalDataVault`; caustics reads `BufferID.OceanGerstnerWaves` first. Rejected: CPU `TrySampleWaveKinematics` dependency. Estimate: 512-byte copy on fluid update; no managed GC.
- [x] 6. COMPUTE DISPATCH | DOD: wrote `Assets/_Project/Art/Shaders/Hecton_CausticsGenerator.compute`, 512x512 dispatch, 8x8 thread groups, player AUP-centered projection, bootstrap-injected without `Resources.Load`. Rejected: projector/RT blit pass and `Assets/Resources` runtime loading. Estimate: skipped on Unknown/Low/MX350; 4096 groups on Mid+ only after lazy compute resource allocation.
- [x] 7. REFRACTION MATH | DOD: compute shader derives Gerstner gradients and uses slope perturbation; static `rg` found no `asin`/`acos`. Rejected: ray marching and true Snell solve. Estimate: one Gerstner derivative solve per pixel; Mid caps to 8 waves, High to 12, Ultra to 16.
- [x] 8. CHROMATIC ABERRATION | DOD: RGB edge split is driven by the refraction vector from the single wave solve instead of re-running the full wave loop per color. Rejected: three full Gerstner solves per pixel; it was visual vanity at 3x math cost. Estimate: removes two 512x512 full-wave evaluations per dispatch.

## Phase 3
- [x] 9. RENDER TEXTURE | DOD: service lazily allocates one persistent `RenderTextureDescriptor` using `GraphicsFormat.R8G8B8A8_UNorm` only when a compute-capable tier can dispatch. Rejected: HDR/float RT and Low-tier idle RT allocation. Estimate: 1 MB VRAM only on Mid+ compute path.
- [x] 10. GLOBAL BINDING | DOD: publishes `_HectonCausticsMap`, `_HectonCausticsAUP`, and synchronized legacy caustic globals. Rejected: material property blocks. Estimate: global set only, no per-renderer work.
- [x] 11. CORE LIT INTEGRATION | DOD: `Hecton_CoreLit.hlsl` samples `_HectonCausticsMap` when active and retains procedural fallback; existing scene-depth fade kills caustics by 50m. Rejected: replacing all local caustic shader paths. Estimate: one texture sample on active analytical path.
- [x] 12. OCCLUSION MASK | DOD: caustic mask multiplies `GetMainLight(TransformWorldToShadowCoord(positionWS)).shadowAttenuation`. Rejected: unshadowed fake light. Estimate: reuses URP main light shadow sampling.

## Phase 4
- [x] 13. AUP SHIFT SAFETY | DOD: service implements `IOriginShiftListener` and rebases cached projection origin by `ShiftOffset`. Rejected: assuming player recenter alone is enough. Estimate: event-only scalar updates.
- [x] 14. MATH LOD | DOD: Unknown/Low/MX350/H8 low-memory disables compute and releases compute-only resources; HLSL procedural caustics remain as fragment fallback. Rejected: lower-res compute on low tier. Estimate: saves full dispatch plus 512 RT and wave upload resources on MX350.
- [x] 15. DEPTH GATE | DOD: if resolved player runtime AUP Y is below -100m, dispatch is disabled, projected intensity is zeroed, and global active flag is zero; re-enable requires rising above -95m to avoid threshold flicker. Rejected: shader-only depth fade, procedural abyss fallback, and single-threshold state churn. Estimate: saves dispatch and fragment fallback energy in abyss.
- [x] 16. ZERO-GC | DOD: hot path uses persistent `NativeArray`, `GraphicsBuffer`, cached property IDs, and no per-frame managed allocation by design. Rejected: managed wave arrays/List upload. Estimate: 0 managed bytes/frame by code inspection; profiler proof pending.
- [x] 17. BLACKBOX DUMP | DOD: service owns `NativeArray<CausticTelemetryEntry>[300]`, writes active state/hash/context/dispatch wave count each late frame, and dumps once per non-finite incident to `Docs/AgentLogs/Dump_CAUSTICS_PROJECTION_ENGINEER.bin`. Rejected: chat-only failure reports and repeated per-frame dump storms. Estimate: one struct write/frame.
- [x] 18. EXECUTION PHASE | DOD: registers `ILateFrameTickable` under `PriorityLayer.Environment`, the local VISUAL_SYNC equivalent. Rejected: `Update()` or slow tick. Estimate: one late-frame dispatch gate.
- [x] 19. OMEGA COMPILE CHECK | [BLOCKED BY DEPENDENCY]: D3D11 compute syntax error was found and fixed (`line` token renamed to `ridgeLine`); current Unity console has no caustics shader/C# entries and `AnalyticalCausticsService.cs` / `BootstrapController.cs` validate with 0 Unity MCP diagnostics. Runtime compute discovery no longer uses forbidden `Resources.Load`; `00_BOOTSTRAP` binds the compute shader through `BootstrapController` and transfers it to runtime `GameBootstrapper`. Full project compile is currently blocked by non-caustics `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs` missing `MethodImpl` imports and fileless import errors. Vulkan platform compute compile is still not proven. Rejected: editing Gameplay/Core dependency blockers from VFX domain without explicit ownership. Estimate: verification blocked at external compile wall.

## Loop Log
- Loop 0: Prompt extracted with CLI from Docs/Tasks/CURRENT_BATCH.md. Status/Rationale initialized. No code touched.
- Loop 1: Re-extracted prompt after first implementation block. Added registry contract, caustics asmdef/service, compute shader, data-vault wave buffer publication, shader injection, and old manager guard.
- Loop 2: Ran `dotnet build Hecton8.Core.csproj`; build is blocked by unrelated cross-agent asmdef/reference errors and Ecosystem duplicate method, not a caustics-specific result.
- Loop 3: Ran Unity `refresh_unity` and console read; Unity reports one current compile error in `EcosystemDirector.cs`, unrelated to caustics.
- Loop 4: Validated `AnalyticalCausticsService.cs` with Unity MCP `validate_script`; diagnostics returned 0 warnings and 0 errors.
- Loop 5: Re-read prompt and static-searched own compute/service/shader for banned `asin`/`acos`, old singleton, and caustic projector dependency. No banned Snell functions or `CausticsManager.Instance` found.
- Loop 6: Parsed `OMEGA_POLISH` only after all core tasks were checked/blocked. Replaced direct compute `normalize`/`sqrt` with `rsqrt` math, replaced C# `math.normalizesafe` and `Marshal.SizeOf` in the caustics service, re-extracted the prompt with CLI, and revalidated `AnalyticalCausticsService.cs` at 0 warnings / 0 errors. Static scan found no direct `sqrt(`, `normalize(`, `asin`, `acos`, `math.normalize`, `Marshal.SizeOf`, `foreach`, `string.Format`, interpolated strings, or `.ToString()` in caustics-owned hot code.
- Loop 7: Patient re-audit. Removed the 3x full-wave RGB compute loop, added tier wave caps, moved compute buffers/RT to lazy Mid+ allocation, made Unknown tier use fallback, avoided duplicate-service ticking, prevented black-box dump storms, and fixed the D3D11 compute shader reserved-token error. Unity console now reports no caustics errors; current blocker is `WorldChunkResidencyManager.cs` outside VFX domain.
- Loop 8: Resource-policy re-audit. Moved `Hecton_CausticsGenerator.compute` from `Assets/Resources` to `Assets/_Project/Art/Shaders` while preserving `.meta`, removed `Resources.Load`, added bootstrap serialized compute injection, added abyss hysteresis, added a missing-kernel latch, and hardened reflection scratch cleanup with `try/finally`. Static scan confirms no caustics `Resources.Load`.
- Loop 9: Bootstrap binding recheck. Found `00_BOOTSTRAP` owns `BootstrapController`, not serialized `GameBootstrapper`; runtime-added `GameBootstrapper` could not retain an authored compute reference. Added a `BootstrapController` compute slot, bound it to GUID `27b7cf5d630bd8d4dbc699ff38f19ac2` in `00_BOOTSTRAP`, and added cold handoff from `BootstrapController` to runtime `GameBootstrapper` before caustics registration. `BootstrapController.cs` validates at 0 diagnostics; `GameBootstrapper.cs` Unity validator still reports its pre-existing duplicate-signature false blocker, while the live Unity console reports only non-caustics `GlobalDataVault.cs` errors.
- Loop 10: Abyss semantics recheck. Found compute dispatch was disabled below -100m but procedural fallback could still receive nonzero projected intensity. `PublishShaderGlobals` now zeros intensity while depth-gated, and `PublishDisabledGlobals` clears projected params so disabled/destroyed services do not leave stale fallback light. `AnalyticalCausticsService.cs` validates at 0 diagnostics after the patch.
- Loop 11: Resource lifecycle recheck. Found release/re-enable could allocate a fresh wave scratch buffer while keeping the old ocean wave version, causing the vault path to skip scratch refill and upload zero waves. Reset `_lastWaveMetaVersion` on scratch allocation and compute-resource release. `AnalyticalCausticsService.cs` validates at 0 diagnostics; caustics console filter returns 0 entries. Current full compile blocker is non-caustics `PlayerKinematicsRuntime.cs` missing `MethodImpl` imports plus fileless import errors.
