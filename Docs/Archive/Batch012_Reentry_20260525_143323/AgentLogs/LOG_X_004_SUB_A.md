# LOG_X_004_SUB_A

What was wrong: Eight world/ecology files were checked for residual presentation API leaks in simulation hot paths. The old X_004 JSON report is partially stale against live source, so each finding was revalidated by current call chain and phase.

What was done: No source files edited. Current source line scans and helper-chain reads were performed for FloraInteractionManager, SargassumCrestDampingController, SargassumGlobalDragManager, AbyssalThermalManager, SargassumCutManager, SargassumMicroFaunaBoids, HectonCaveVoxelLightingVolume, and EcosystemDirector. Mandates read: OPT_Zero_GC, ARCH_Execution_Phases, ARCH_Global_Registry_DI, ARCH_Signal_Lane_Segregation, REND_Instanced_Flora_Physics, REND_URP_Graphics_HotPath, REND_VFX_Fluid_Aesthetics, OPT_Cinematic_Cheat.

Cinematic Cheats used: report-only patch plan recommends DTO/scalar staging and VISUAL_SYNC shader/VFX fakes. No runtime implementation performed.

Exact microseconds saved: 0 us verified. Static inspection only. Expected savings are pending Unity profiler/GCMonitor proof after source patches.

Findings summary:
- FloraInteractionManager: real residual leaks in Tick/SlowTick through direct Shader globals, ParticleSystem emission, ComputeShader wake simulation, texture clear/global publication, flow-field buffer global binding, and reset paths. Existing queue/LateFrame path is partial only.
- SargassumCrestDampingController: real residual leaks. Tick/SlowTick call PublishGlobals and DisableLegacyInputs, which mutate shader globals, Renderer.enabled, and Transform.localScale.
- SargassumGlobalDragManager: real residual leaks. Tick/SlowTick perform dynamic texture Apply, shader globals, RenderMeshInstanced, BRG material/buffer/bounds mutation, Material creation fallback, ObjectPool/Transform chunk fallback, and Texture2D.Apply paths.
- AbyssalThermalManager: mixed. Old Tick thermal map upload findings are stale because UploadThermalMapTextureIfDirty is now LateFrameTick. Real leaks remain in FixedTick local thermal Shader.SetGlobal*, Tick smoke compute dispatch/MPB upload/Graphics.RenderPrimitives, and thermal bubble globals if reached from Tick helper chain.
- SargassumCutManager: old Shader.SetGlobal fatal lines are mostly stale because Tick/SlowTick now queue globals and LateFrameTick publishes. Real residual leaks remain in Tick/RegisterExternalCut through debris particle emit, ComputeShader.Dispatch, and Graphics.SetRenderTarget damage-volume clear paths.
- SargassumMicroFaunaBoids: real residual leaks. Tick dispatches boid compute kernels and calls RenderCurrentBuffer, which mutates MPB and submits Graphics.RenderMeshIndirect. Slow/cold fallback texture creation is a first-use presentation hazard.
- HectonCaveVoxelLightingVolume: old fatal findings are stale in current source. Tick queues; LateFrameTick uploads Texture3D and shader globals. Remaining source risk is not fatal under this mission unless LateFrame registration fails.
- EcosystemDirector: real residual leaks in SlowTick predator AUP visual globals and buffer upload, plus external hot route FaunaBrain.Tick -> PublishBiolumFlashBang -> Shader.SetGlobalVector. Biomass overgrowth SetGlobalFloat is currently reached from LateFrame/cold routes and is not classified as fatal from inspected evidence.

Verification: Unity import, Play Mode, profiler, GCMonitor, Frame Debugger, and compile were not run. No source edits were made, so compile was intentionally skipped.

Detailed leak classification:

1. FloraInteractionManager.cs
Real leaks:
- `Tick:1845 -> PublishSubmarineWashGlobals:2620 -> Shader.SetGlobalVector:2625-2723`.
- `Tick:1845 -> PublishDamageReactionGlobal:5568 -> Shader.SetGlobalVector:5574/5581`.
- `Tick:1845 -> TryEmitSedimentBursts:2360 -> EmitSedimentBurst:2400 -> ParticleSystem.Play:2406 / ParticleSystem.Emit:2425`.
- `Tick:1845 -> ProcessProceduralWakeTick:2785 -> PublishProceduralWakeBuffer:3055 -> Shader.SetGlobalVectorArray/Int/Vector/Float:3141-3147`.
- `Tick:1845 -> RefreshFlowFieldGlobals:6946 -> PublishFlowFieldGlobals:7005 -> Shader.SetGlobalBuffer/Vector/Int:7008-7013 -> PublishCulledFloraGlobals:7017 -> Shader.SetGlobalBuffer/Int:7021-7026`.
- `Tick:1845 -> PublishPlayerRuntimePosition:7044 -> Shader.SetGlobalVector:7054/7061`.
- `Tick:1845 null-player branch -> ResetInteractionGlobals:7614 -> Shader.SetGlobal*:7616-7638 and Shader.SetGlobalBuffer:7663`.
- `SlowTick:1934 -> RefreshModuleParasiteState -> PublishParasiteInfectionGlobals:5933 -> Shader.SetGlobalVectorArray/Vector:5935-5937`.
- Additional current-source leaks outside old report: `Tick -> ProcessProceduralWakeTick -> _wakeTrailSimulationCompute.Dispatch:7385` and reset path `ResetInteractionGlobals -> ClearWakeTrailTextures:7412` with RenderTexture/GL clear work.
False positives/allowed:
- `LateFrameTick:1920 -> FlushInteractionVisualSync:2130` is already VISUAL_SYNC style and allowed in principle, though the same class still leaks direct Tick paths.
Minimal zero-GC VISUAL_SYNC patch:
- Replace direct `Publish*Globals` calls in Tick/SlowTick with fixed pending structs: wash, damage reaction, flow field metadata, wake globals, parasite anchors, player runtime, reset flags.
- Keep `GraphicsBufferUploadUtility` and Shader.SetGlobal* only in `LateFrameTick`.
- Replace `ParticleSystem` sediment burst with a `SignalBus<EnvironmentSignal>` or owner-local fixed queue consumed by a VFX renderer in VISUAL_SYNC.
- Move wake compute dispatch and RenderTexture clears to LateFrameTick or render owner; Tick only appends wake DTO commands.

2. SargassumCrestDampingController.cs
Real leaks:
- `Tick:189 -> PublishGlobals -> Shader.SetGlobalTexture/Vector/Float:449-466`.
- `SlowTick:229 -> RefreshFacadeTextures -> PublishGlobals -> Shader.SetGlobal*:449-466`.
- `Tick/SlowTick -> DisableLegacyInputs:507 -> ApplyLegacyInputState:542 -> Transform.localScale:549/554 and Renderer.enabled:550/555`.
False positives/allowed:
- Renderer fields at 106-108 are boundary leaks by type, but fatal only when mutated through Tick/SlowTick helper chain above.
Minimal zero-GC VISUAL_SYNC patch:
- Add/keep `ILateFrameTickable`; Tick/SlowTick only detect density/drift/cut-mask changes and set pending facade state.
- Move `PublishGlobals` and `ApplyLegacyInputState` into LateFrameTick.
- Prefer one shader-side active scalar over Transform scale/Renderer.enabled suppression for Crest legacy inputs.

3. SargassumGlobalDragManager.cs
Real leaks:
- `SlowTick:1563 -> RefreshDynamicTextures:4424 -> Texture2D.Apply:4486/4488 and ClearDensityTexture/ClearSinkTexture:4404/4414 -> Apply:4411/4421`.
- `SlowTick:1563 -> PublishShaderGlobals:2642 -> Shader.SetGlobalVector/Texture/Float:2659/2665/2671/2677`.
- `Tick:1595 -> RefreshDynamicTextures:4424 -> Texture2D.Apply:4486/4488`.
- `Tick:1595 -> PublishShaderGlobals:2642 -> Shader.SetGlobal*:2659-2677`.
- `Tick:1595 -> Graphics.RenderMeshInstanced:1635`.
- `Tick:1595 -> UpdateScavengerHosts:3314 -> EnsureScavengerRenderResources:3172 -> new Material:3184 when fallback is missing`.
- `Tick:1595 -> DrawScavengers:3458 -> EnsureScavengerBrgMaterial:3488 -> new Material:3498; Material.SetBuffer:3471; BatchRendererGroup.SetGlobalBounds:3482; SetBatchBuffer:3572`.
- `SlowTick:1563 -> EvaluateBuoyancyCollapseZones -> TrySpawnCollapseChunks:2432 -> ObjectPool spawn and Transform.localScale:2500 fallback`.
- `SlowTick/Tick -> ResolveActiveNestingPrototypes:2684 -> EnsureFallbackNestingResources:2696 -> new Material:2706/2722 and Material.SetColor:2712/2713/2728/2729` if fallback is first needed in hot path.
False positives/allowed:
- `OnEnable/Awake` calls to render resource ensure/publish are cold, not fatal by phase, but they prove mixed ownership.
Minimal zero-GC VISUAL_SYNC patch:
- Tick/SlowTick write density/sink dirty flags, collapse request DTOs, scavenger matrix count/bounds DTOs.
- LateFrame/render owner performs Texture2D.Apply, Shader.SetGlobal*, BRG registration/bounds/buffer sync, and RenderMeshInstanced.
- Prewarm fallback materials in cold bootstrap or remove runtime material creation; no hot fallback construction.
- Collapse chunks: if visual-only, LateFrame VFX queue; if gameplay collision truth, publish a bounded PhysicsSignal/owner command, not transform fallback in ecology slow tick.

4. AbyssalThermalManager.cs
Real leaks:
- `FixedTick:1214 -> ProcessThermalGameplayTarget -> PublishLocalThermalPresentation:1744 -> Shader.SetGlobalFloat:1768/1776/1782`.
- `FixedTick:1214 else branch -> PublishLocalThermalPresentation:1234 -> Shader.SetGlobalFloat:1768/1776/1782`.
- `Tick:1102 -> UpdateThermalPresentationDecay:1789 -> Shader.SetGlobalFloat` when condensation changes.
- `Tick:1102 -> BindSmokeUniforms:3755 -> MPB SetBuffer/Vector/Float/Color:3781-3788`.
- `Tick:1102 -> blackSmokeCompute.Dispatch:3803 -> RenderSmoke:3821 -> Graphics.RenderPrimitives:3832`.
- `Tick/Slow helper route -> PublishThermalBubbleCommands:3472 -> Shader.SetGlobalInt/VectorArray:3484/3491` if still reached from `AdvanceThermalGpuRefresh` in Tick.
False positives/allowed:
- Old `Tick -> UploadThermalMapTextureIfDirty` findings are stale in current source; method is now called from `LateFrameTick:1194 -> UploadThermalMapTextureIfDirty:2236`, so `SetPixelData/Apply/SetGlobalTexture:2260-2262` is allowed by phase.
- Scanner labels for smoke uniform writes as Material.Set* are imprecise; current source uses `MaterialPropertyBlock.Set*`, still real because it is reached from Tick.
Minimal zero-GC VISUAL_SYNC patch:
- FixedTick writes local thermal presentation DTO only; LateFrame flushes local heat/temperature/condensation globals.
- Tick updates thermal simulation scalars and smoke particle state intent only; smoke compute dispatch, MPB mutation, render submission, and bubble command globals move to LateFrameTick/render pass.
- Keep thermal map upload in LateFrameTick; add dirty coalescing if not already measured.

5. SargassumCutManager.cs
Real leaks:
- Old Shader.SetGlobal report entries are stale: `Tick:492` and `SlowTick:555` now call `QueueGlobalPublish:1373`; `LateFrameTick:568 -> PublishGlobals:1379` performs Shader.SetGlobal*.
- Current residual leak: `Tick:492 -> EmitDebrisBurst:1055 -> SargassumDebrisParticleSystem.EmitBurst:1060`.
- Current residual leak: `RegisterExternalCut:396 -> ProcessQueuedMaskUpdate:1197 -> ComputeShader.Dispatch:1238` and `ProcessQueuedDamageVolumeUpdate:1312 -> ComputeShader.Dispatch:1364`.
- Current residual leak: `Tick:492/SlowTick:555 -> ProcessQueuedMaskUpdate/ProcessQueuedDamageVolumeUpdate -> ComputeShader.Dispatch:1238/1364`.
- Current residual leak: damage-volume clear path `ClearDamageVolumeTextures -> Graphics.SetRenderTarget:1175/1177/1179` when bounds/clear are triggered.
False positives/allowed:
- `PublishGlobals:1379 -> Shader.SetGlobal*:1383-1452` is allowed when reached from `LateFrameTick:568`.
- Cold `Awake/OnEnable -> PublishGlobals:457/465` is not a hot-path fatal by itself.
Minimal zero-GC VISUAL_SYNC patch:
- Tick/RegisterExternalCut only append cut, damage-volume, and debris DTOs to fixed buffers and mark dirty.
- LateFrameTick drains queued cut/damage compute dispatches, debris VFX requests, and shader global publication.
- If cut mask is gameplay truth, separate authoritative cut state from visual mask texture; gameplay reads DTO/native mask, not RenderTexture.

6. SargassumMicroFaunaBoids.cs
Real leaks:
- `Tick:2056 -> boidCompute.Dispatch:2164/2165/2168` executes GPU simulation/compute from hot simulation phase.
- `Tick:2056 -> RenderCurrentBuffer:7026 -> UploadBoidRenderPropertiesIfNeeded:6966 -> MPB SetBuffer/SetFloat/SetTexture:6970-7018`.
- `Tick:2056 -> RenderCurrentBuffer:7026 -> UploadHitFlashPropertiesIfNeeded:6941 -> MPB SetFloat/Vector/Color:6946-6953`.
- `Tick:2056 -> RenderCurrentBuffer:7026 -> Graphics.RenderMeshIndirect:7057`.
- `SlowTick:2201 -> RefreshSpawnData/EnsureBufferCapacity -> EnsureFallbackAbyssalFlowTexture:4288 -> Texture3D.SetPixel/Apply:4301-4302` on first fallback creation.
- Additional dispatch helpers: clear/stat/spatial/PBD/origin shift compute dispatches at 6628, 6672, 6680, 7951, 7954 must be phase-owned if reached from Tick/fallback render paths.
False positives/allowed:
- `Shader.PropertyToID` static fields are cold and not fatal.
Minimal zero-GC VISUAL_SYNC patch:
- Split boid simulation ownership from boid rendering. Tick may write simulation command DTOs or schedule owner-approved jobs; render dispatch/property upload/draw submission moves to VISUAL_SYNC/render owner.
- Convert MPB use to constant/GraphicsBuffer where possible; MPB is not the preferred standard-geometry path.
- Prewarm fallback 1x1 flow texture in cold bootstrap or replace with a shared static texture asset.

7. HectonCaveVoxelLightingVolume.cs
Real leaks:
- No current fatal Tick presentation leak observed after live re-read. `Tick:164` calls `FinalizeScan:495`, but FinalizeScan now sets `_textureUploadDirty` and queues globals instead of applying texture/shader directly.
- `LateFrameTick:216 -> Texture3D.SetPixelData/Apply:222-223 -> FlushGlobals:590 -> Shader.SetGlobal*:592-619` is phase-correct.
False positives/allowed:
- Old report entries `Tick -> EnsureResources`, `Tick -> FinalizeScan`, and `Tick -> PublishGlobals` are stale against current source.
- `PublishInactiveGlobals:685` is still called from cold lifecycle paths; not a hot fatal unless registration/lifecycle misuse calls it from a dispatcher lane.
Minimal zero-GC VISUAL_SYNC patch:
- Keep current queue/LateFrame model.
- Verify `TryRegister` includes LateFrame lane registration and add telemetry if LateFrameTick is not registered.
- Optional: move lifecycle inactive global reset into same queued path except editor/cold teardown.

8. EcosystemDirector.cs
Real leaks:
- `SlowTick:2108 -> PublishFloraPredatorAupBuffer:5563 -> GraphicsBufferUploadUtility.UploadNativeArray:5593 -> PublishFloraPredatorAupGlobals:5613 -> Shader.SetGlobalBuffer/Vector/Int:5618/5619/5627`.
- `SlowTick:2108 missing player branch -> PublishFloraPredatorAupGlobals:2130 -> Shader.SetGlobal*:5618/5619/5627`.
- `SlowTick:2108 -> PublishApexPresenceFake:2131/5610 -> Shader.SetGlobalFloat:5646 -> PublishGlobalOceanPanic:5630 -> Shader.SetGlobalFloat:5637`.
- External hot route verified: `FaunaBrain.Tick -> HandleAttackPerform -> TriggerBiolumFlashBang -> EcosystemDirector.PublishBiolumFlashBang:1836 -> Shader.SetGlobalVector:1839/1840`.
False positives/allowed:
- `PublishBiomassTelemetryAndEvents:6051 -> Shader.SetGlobalFloat:6101` is reached from `LateFrameTick -> CompleteScheduledSimulation` and load/cold route in inspected source, so not fatal from current evidence.
- Awake/teardown shader resets at 3625-3628 and 3828-3832 are lifecycle/cold unless invoked from hot helpers.
Minimal zero-GC VISUAL_SYNC patch:
- SlowTick writes predator AUP upload count, apex flag, panic scalar, and flash-bang DTO to owner-local pending state or typed visual signal.
- LateFrameTick uploads `_floraPredatorAupBuffer` and shader globals.
- Change `PublishBiolumFlashBang` into `QueueBiolumFlashBang` or a typed SignalBus payload; no Shader.SetGlobal* callable from FaunaBrain hot logic.

Patch priority:
1. SargassumGlobalDragManager and SargassumMicroFaunaBoids are the largest active presentation/simulation mergers because they submit/draw/render from Tick.
2. FloraInteractionManager is broadest shader/particle leak surface and already has partial queue infrastructure to finish.
3. AbyssalThermalManager has dangerous FixedTick presentation writes and Tick render submission.
4. SargassumCrestDampingController is small and mechanically fixable.
5. SargassumCutManager and HectonCaveVoxelLightingVolume already have partial/latest queue fixes; finish compute/VFX separation and verify.
6. EcosystemDirector requires a small visual DTO queue plus external hot-call rename to prevent FaunaBrain from invoking shader globals.
