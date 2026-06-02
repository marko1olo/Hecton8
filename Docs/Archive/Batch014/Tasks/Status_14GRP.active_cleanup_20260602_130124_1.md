# Status_14GRP

ID: 14GRP
Domain: Graphics Scalability / Rendering / VFX
Batch prompt: no `<AGENT_PROMPT id="14GRP">` exists in `Docs/Tasks/CURRENT_BATCH.md`; direct user prompt is active directive.
Status: SOURCE PROOF PASS; LATEST RUNTIME BUILD BLOCKED BY GENERATED PROJECT GRAPH; UNITY EDITOR/PLAYMODE PROOF PENDING

## Loop 1
- [x] Extract and bound active directive. DOD: CLI `Select-String` against `CURRENT_BATCH.md`; rejected neighboring batch prompts; estimate 120 us static scan.
- [x] Read domain/mandates. DOD: AGENTS/domain file plus 6 graphics/zero-GC mandates; rejected broad archive mining; estimate 900 us file IO.
- [x] Static graphics-domain hot-path audit. DOD: `rg` scans for registry, component, material, update, build-lock hazards; rejected Unity import/build spam; estimate 2.4 ms source scan.
- [x] Patch one proven graphics-domain defect. DOD: scalar capacity captured under guard, manifest committed after release, editor regression added; rejected over-engineered subsystem rewrite; estimate 0 runtime us.
- [x] Static syntax proof. DOD: diff check plus brace/block parser; Roslyn load blocked by Windows PowerShell CLR mismatch; rejected `dotnet build` under 82% CPU; estimate 3.1 ms source scan.
- [x] Remove HUD font material lookup from visual drain. DOD: TMP font/material readiness refresh runs in cold lifecycle/SlowTick, `LateFrameTick` consumes cached references only; rejected per-frame TMP material property access; estimate 0-5 us avoided on swap frames.

## Evidence Notes
- First-20-minutes route impact: rendering stability and scalable visual quality remove stalls and visual regressions during initial world entry.
- Unity/runtime proof: PENDING VERIFICATION; no PlayMode/profiler/device capture available in this turn.
- Build throttle: two runtime compile gates total, each after a code batch and only under CPU gate. Latest `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`: 0 errors, 0 warnings, 27.56 s. Editor/test assembly skipped because another `dotnet build Hecton8.slnx -maxcpucount:1` was running and CPU was 51-88%.
- Hot lookup proof: graphics/runtime scoped scan found no `GlobalRegistry.Get<T>()`, `GetComponent<T>()`, `TryGetComponent<T>()`, `Camera.main`, `.material`, or `.materials` inside detected hot methods.
- Phase proof: patched dynamic light mock seed completes cold seed work before manifest publication; presentation logic was not moved into simulation ticks.
- Lock proof: patched method releases mock seed guard in `finally` before source manifest commit; DataVault write-lock spot checks found single-lock helpers with `finally` release.
- HUD proof: `VisorHUDController.LateFrameTick()` drains `LabelSwapScheduler` with `_queuedHudFontMaterial`; `UpdateBiosFontSwapState()` does not prewarm, resolve primary font, or read `.material`.

## Loop 2
- [x] Re-scan RenderGraph/legacy command paths. DOD: `rg` for `Camera.AddCommandBuffer`, `RemoveCommandBuffer`, `Graphics.Blit`, `CommandBuffer.Blit`, `override void Execute`; rejected runtime camera command fallback; estimate 1.8 ms source scan.
- [x] Decommission HUD legacy camera command-buffer scissor. DOD: `VisorHUDController` no longer contains `AddCommandBuffer`, `RemoveCommandBuffer`, `CameraEvent`, or `new CommandBuffer`; rejected keeping built-in fallback inside URP domain; estimate 0 steady-frame us.
- [x] Update regression tests. DOD: 14GRP and existing visor scalability source-shape tests assert no legacy camera command-buffer path; rejected untested source deletion; estimate 0 runtime us.
- [x] Runtime assembly compile gate. DOD: `Assembly-CSharp.csproj` built under CPU gate with 0 errors / 0 warnings; rejected editor assembly build at 77% CPU; estimate 21.43 s wall time.
- [x] Deep command-buffer ownership follow-up, shader-global lane. DOD: `GlobalShaderDispatcher` no longer owns `CommandBuffer` or calls `Graphics.ExecuteCommandBuffer`; direct `Shader.SetGlobal*` stays inside `LateFrameTick` and mirrors existing fallback bridge semantics; rejected changing compute-dispatch command buffers blindly; estimate cold alloc -1 CommandBuffer, visual-sync publish work unchanged.

## Loop 3
- [x] Remove global shader upload command buffer. DOD: source scan confirms no `CommandBuffer`, `ExecuteCommandBuffer`, `TryEnsureCommandBuffer`, or `HasCommandBufferReady` remains in `GlobalShaderDispatcher`; rejected RenderGraph overreach for scalar/vector shader globals; estimate cold alloc -1 and one command-buffer submit removed per visual dispatch.
- [x] Add source-shape regression. DOD: `GraphicsScalability14GRPEditTests.GlobalShaderDispatcher_PublishesGlobalsWithoutCommandBuffer()` asserts direct `Shader.SetGlobal*` route and phase markers; rejected unguarded refactor without test lock; estimate 0 runtime us.
- [x] Static syntax/source proof. DOD: `git diff --check`, forbidden-token scans, brace parser with string/char stripping, and source-shape scans pass; rejected build while CPU was 71-100% or foreign dotnet was active.
- [x] Runtime assembly compile gate. DOD: one post-patch `Assembly-CSharp.csproj` build under 37% CPU, no csc/MSBuild/dotnet present at launch; result 0 errors / 0 warnings / 27.56 s.
- [ ] Compute command-buffer route remains owned. DOD pending: `ParasiteSwarmGpuRuntime` uses command buffer for batched compute dispatch and indirect args, owner comment `SHINOBU_313`; not modified without compute ownership migration proof.

## Loop 4
- [x] DRS telemetry lock flattening. DOD: `ThermalDynamicResolutionAdapter.WriteTelemetry()` now writes ring data under telemetry guard, releases in `finally`, then performs black-box dump and invalid-state reset outside that guard; rejected nested DataVault write lock on invalid state; estimate unprofiled, removes pathological stall/deadlock vector.
- [x] Invalid-state fallback preserved. DOD: `RecoverInvalidScaleState()` resets directly only when telemetry acquisition fails; rejected double reset after telemetry-owned detection; estimate 0 steady-frame us.
- [x] Source-shape regression added. DOD: `GraphicsScalability14GRPEditTests.ThermalDrsTelemetry_ReleasesTelemetryGuardBeforeInvalidStateReset()` asserts release-before-dump/reset and blocks locked dump call inside `WriteTelemetry`; rejected untested lock-order refactor.
- [x] Static validation. DOD: method-order extraction, hot-method forbidden-token scan, brace parser, and `git diff --check` pass; line-ending warnings are repo-wide CRLF notices, not whitespace errors.
- [x] Compile throttle observed. DOD: normal runtime build launched only at CPU 41% with no active build processes and failed before C# compile on generated `ResolveProjectReferences` cycle in `Unity.RenderPipelines.Universal.Runtime.csproj` and `MoreMountains.Tools.csproj`; one narrower `/p:BuildProjectReferences=false` pass launched only after CPU returned to 43% and failed before C# compile on `_GetCopyToOutputDirectoryItemsFromTransitiveProjectReferences` in `MoreMountains.Tools.csproj`; MSBuild node-reuse process was stopped by PID after the failed pass.

## Loop 5
- [x] Font streaming hot-material transitive path closed. DOD: `FontStreamingManager.LateFrameTick()`, `EvaluatePendingFontReadiness()`, and `BeginSwapQueue()` no longer read `.material` or `LocalizedFontResolver.IsFontReady`; font material/readiness cache refresh occurs in `OnEnable`, `Start`, `HandleSceneLoaded`, and language-change cold paths; rejected per-frame TMP material probing; estimate 0-5 us avoided on swap frames.
- [x] Non-critical VFX kill switch made continuous. DOD: `VfxComputeParticleBudgetCatalog.ApplyKillSwitchCount()` preserves pressure-scaled bubble/debris survival floors (`32` bubbles, `8` debris, `125 permille` emergency multiplier) instead of returning zero; rejected binary visual pop under pressure; estimate unprofiled, preserves cheap ambience on low tier.
- [x] Biolum black-box dump lock flattening. DOD: `DumpBlackBox()` no longer holds the ring guard while acquiring scratch guard; ring entries copy to a persistent `NativeArray<BiolumPulseTelemetryEntry>[300]` snapshot under `BlackBoxGuardMask`, release in `finally`, then serialize to scratch under `BlackBoxDumpScratchGuardMask`; rejected managed copy and nested DataVault guards; estimate removes pathological dump-frame deadlock vector.
- [x] Regression coverage extended. DOD: `GraphicsScalability14GRPEditTests` now covers font cached material path, non-critical VFX survival floors, and biolum black-box guard split; rejected undocumented source-only edits.
- [x] Static validation passed. DOD: touched runtime/test brace parser passed, `git diff --check` passed for tracked runtime files, untracked regression file whitespace passed, scoped source proof emitted `FONT_VFX_BIOLUM_SOURCE_PROOF=PASS`, and hot-method forbidden-token scan emitted `HOT_FORBIDDEN_SCAN_DONE`.
- [x] Compile throttle observed again. DOD: one runtime build launched only after CPU 36% and no `dotnet/MSBuild/csc`; it failed before C# compile on the same generated `ResolveProjectReferences` cycle in `Unity.RenderPipelines.Universal.Runtime.csproj` and `MoreMountains.Tools.csproj`; no project files were modified; residual `dotnet` node-reuse process was stopped by PID. Roslyn AST parse was attempted from `Assets/Plugins/Roslyn` but unavailable in Windows PowerShell due `Roslyn.Utilities.StringTable` initializer failure; no further build spam.

## Loop 6
- [x] Mandates and active prompt rechecked. DOD: read AGENTS/domain plus `REND_URP_Graphics_HotPath_Optimization_HLOD`, `REND_GPU_Sovereignty`, `REND_VFX_Fluid_Aesthetics_Compute_Particles`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First`, and `UI_Diegetic_Physical_Interfaces`; `CURRENT_BATCH.md` still has no `14GRP` XML block; rejected neighboring prompts.
- [x] HUD acoustic radar hot material getter removed. DOD: `SuitHUDV4CanvasOverlay.ApplyAcousticRadarVisuals()` now calls `BindAcousticRadarOverlayMaterial()` and contains no `.material` token; binding uses `_acousticRadarOverlayMaterialBound` state instead of `Image.material` comparison; rejected per-frame Graphic material getter.
- [x] Acoustic radar cleanup made binding-state owned. DOD: `DisposeAcousticRadarRuntimeResources()` and `DisableAcousticRadarOverlayImage()` clear the material and reset `_acousticRadarOverlayMaterialBound` without material equality reads; rejected material getter as owner-state source.
- [x] Regression added. DOD: `GraphicsScalability14GRPEditTests.SuitHud_AcousticRadarVisualSyncUsesCachedMaterialBindingState()` asserts visual-sync method has no `.material` access and bind/dispose use cached state.
- [x] Static validation passed. DOD: `git diff --check` pass for `SuitHUDV4CanvasOverlay.cs`, brace parser pass for HUD/test files, source proof emitted `SUIT_HUD_ACOUSTIC_BINDING_SOURCE_PROOF=PASS`, and scoped hot-token scan emitted `HOT_FORBIDDEN_SCAN_DONE`.
- [x] Build throttle enforced. DOD: build not launched because an existing `dotnet build Hecton8.slnx -maxcpucount:1 --no-restore...` process was active at PID 58308 and CPU was 48%; no project graph files modified and no orphan process created by 14GRP.

## Loop 7
- [x] Mandates and active prompt rechecked. DOD: status/rationale were read first; `CURRENT_BATCH.md` still has no `14GRP` XML block; domain stayed graphics/VFX; rejected neighboring agent prompts.
- [x] Marine snow binary pressure policy removed. DOD: `VfxComputeParticleBudgetCatalog.ResolvePolicyQualityWeight()` converts advection/volumetric policy masks into continuous floor..1 weights; rejected `mask ? 0f : 1f` feature death; estimate 0 managed GC, scalar ALU only.
- [x] Sparse flow survival cadence added. DOD: `ResolvePolicyFlowResampleFrames()` keeps masked flow resampling on a non-zero 1..16 frame cadence instead of `flowResampleFrames = 0`; rejected hard disable pop under pressure; estimate unprofiled, fewer stalls than full cadence and better motion than dead flow.
- [x] Marine snow renderer integrated. DOD: `BuildContinuousScalabilityParams()` consumes continuous policy weights for flow/collision/depth; `BuildContinuousPressureBudget()` consumes sparse cadence resolver; rejected compute shader rewrite and RenderGraph ownership changes.
- [x] Regression added. DOD: `MarineSnowPolicyMasks_CompressInsteadOfBinaryDisable()` checks weight/cadence math; `MarineSnowRenderer_UsesContinuousPolicyWeightsForFlowAndCollision()` locks source shape against binary policy rollback.
- [x] Static validation passed. DOD: `git diff --check` returned only LF/CRLF warnings, brace parser passed for touched files, source proof emitted `MARINE_SNOW_CONTINUOUS_POLICY_SOURCE_PROOF=PASS`, hot-token scan emitted `HOT_FORBIDDEN_SCAN_DONE`.
- [x] Build throttle enforced. DOD: build not launched because CPU sample was 99.1% (>50% gate); no compiler process was started by 14GRP and no orphan process was created.

## Loop 8
- [x] Mandates and active prompt rechecked. DOD: read status/rationale first, read AGENTS plus graphics/zero-GC/cinematic-cheat mandates, and confirmed `CURRENT_BATCH.md` has no `14GRP` XML block; rejected neighboring prompts.
- [x] Marine snow shadow tap policy made continuous. DOD: added `VfxComputeParticleBudgetCatalog.ResolvePolicyShadowTaps()` so `VolumetricFogHighResMask` compresses fake depth/fog taps by pressure instead of hard-clamping to middle tier; rejected `shadowTaps = math.min(...MiddleQualityShadowTaps)`; estimate scalar ALU only during budget refresh.
- [x] Marine snow renderer integrated. DOD: `BuildContinuousPressureBudget()` and `_debugBudgetedShadowTaps` now pass `pressureLevel` into the shared shadow tap resolver; rejected duplicated renderer-side binary policy math.
- [x] Single-trail draw path simplified. DOD: `NativeTrailRenderer.Render()` now uses `Graphics.DrawMesh` for one generated trail mesh and removed the retained one-element instancing matrix array; rejected `DrawMeshInstanced` for `DrawInstanceCount = 1`; estimate cold alloc -1 Matrix4x4 array and lower render-submit overhead, profiler pending.
- [x] Regression coverage extended. DOD: `GraphicsScalability14GRPEditTests` now imports `System`, checks shadow-tap compression math, blocks binary shadow cap rollback, and asserts `NativeTrailRenderer` has no `_drawMatrices` or `DrawMeshInstanced`.
- [x] Static validation passed. DOD: `git diff --check` returned only LF/CRLF warnings, brace parser passed for touched files, source proof emitted `GRAPHICS_POLICY_SOURCE_PROOF=PASS`, and scoped forbidden-token scan returned no matches.
- [x] Compile throttle enforced with one attempt. DOD: launched exactly one `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` only after CPU sampled 43.9% and no `dotnet/MSBuild/csc` process existed; it failed before C# compile on the known generated `ResolveProjectReferences` cycle in `Unity.RenderPipelines.Universal.Runtime.csproj` and `MoreMountains.Tools.csproj`; residual `dotnet` node-reuse process was stopped by PID 11448.

## Loop 9
- [x] Mandates and active prompt rechecked. DOD: status/rationale read first, AGENTS/domain/TASTE plus graphics/VFX/zero-GC/cinematic-cheat mandates re-read; exact `CURRENT_BATCH.md` match for `14GRP` returned no XML block; neighboring prompt output ignored.
- [x] Abyssal fluid decals made visible when screen-space collector is absent. DOD: `AbyssalFluidDecalManager` now tracks `CopyScreenSpaceDecals()` consumer frames and draws the mesh fallback only when no active collector has reported within two frames; rejected leaving `screenSpaceFluidDecals=true` as a silent visual sink; estimate restores effect with no new hot allocation.
- [x] Pressure spray draw cost made continuous. DOD: `ResolvePressureSprayDrawLimit()` scales pressure-spray matrix submission from `GlobalQualityWeight` and `HomeostasisBrain.PressureLevel`, preserving a 18.75% floor instead of binary on/off; rejected full 64-ribbon submission during emergency pressure; estimate scalar ALU only plus fewer submitted quads under low quality.
- [x] PDA data-log hologram single-instance payload removed. DOD: `PDADataLogTab.RenderSelectedLoreHologram()` now uses `Graphics.DrawMesh` and removed retained `Matrix4x4[1]`; rejected `DrawMeshInstanced` for one proxy mesh; estimate cold alloc -1 Matrix4x4 array and lower draw API ceremony.
- [x] Regression coverage extended. DOD: `GraphicsScalability14GRPEditTests` now asserts abyssal collector fallback/continuous pressure limit source shape and PDA hologram non-instanced draw shape; rejected unguarded presentation-path edits.
- [x] Static validation passed. DOD: `git diff --check` returned only LF/CRLF warnings, brace parser passed for touched files, `ABYSSAL_FLUID_DECAL_SOURCE_PROOF=PASS`, `ABYSSAL_HOT_FORBIDDEN_SCAN=PASS`, `PDA_HOLOGRAM_SOURCE_PROOF=PASS`, `PDA_HOLOGRAM_HOT_SCAN=PASS`.
- [x] Compile throttle enforced. DOD: build not launched because CPU sampled 61.3% then 64.8% (>50% gate) with no compiler processes; no `dotnet` or `csc` process was started by 14GRP.

## Loop 10
- [x] Mandates and active state rechecked. DOD: status/rationale read first; direct 14GRP graphics directive remained active because no `CURRENT_BATCH.md` XML block exists; rejected neighboring prompt bleed.
- [x] Dead scanner hologram mesh payload removed. DOD: `SuitHUDV4CanvasOverlay` scanner hologram is a flat canvas fake, so stale `Matrix4x4[1]`, `MaterialPropertyBlock`, material, mesh fields and Awake allocation were deleted; rejected keeping unused runtime resources for a non-mesh path; estimate cold alloc -1 array and -1 MaterialPropertyBlock.
- [x] Fabricator selected hologram single-instance draw simplified. DOD: selected recipe preview now converts `float4x4` to stack `Matrix4x4` and uses `Graphics.DrawMesh`; ingredient fan-out batch keeps `DrawMeshInstanced` and `_hologramMatrixBuffer`; rejected destroying real 16-instance batching; estimate cold alloc -1 Matrix4x4 array and lower draw API ceremony.
- [x] Regression coverage extended. DOD: `GraphicsScalability14GRPEditTests` now asserts no scanner mesh payload and no selected-recipe instancing payload while preserving fabricator batch instancing.
- [x] Static validation passed. DOD: `git diff --check` returned only LF/CRLF warnings, brace parser passed for three touched files, `SINGLE_INSTANCE_PRESENTATION_SOURCE_PROOF=PASS`, `FABRICATOR_SELECTED_HOLOGRAM_SOURCE_PROOF=PASS`, `SUIT_SCANNER_FLAT_HOLOGRAM_SOURCE_PROOF=PASS`, `PRESENTATION_HOT_FORBIDDEN_SCAN=PASS`.
- [x] Compile throttle enforced. DOD: build not launched because CPU sampled 66.1%, 93.0%, 76.8% (>50% gate); no compiler process output was present and no orphan process was created.

## Loop 11
- [x] Mandates and active prompt rechecked. DOD: status/rationale read first, AGENTS/domain plus graphics/GPU/zero-GC mandates re-read, exact `CURRENT_BATCH.md` match for `14GRP` still absent; rejected neighboring prompt bleed.
- [x] Saving-progress pulse material getter debt removed. DOD: `SuitHUDV4CanvasOverlay.EnsureSavingProgressPulseRuntimeResources()` now calls `BindSavingProgressPulseMaterials()` and no longer compares `Image.material`; dispose uses owner flags instead of material equality reads; rejected Graphic material getter as state source; estimate 0-2 us avoided on save-indicator lifecycle frames.
- [x] Hierarchy rebuild safety preserved. DOD: `BuildSavingProgressHierarchy()` and `InvalidateVisualCaches()` reset `_savingProgressDataLampPulseMaterialBound` and `_savingProgressDataNeedlePulseMaterialBound` before new image refs can be bound; rejected stale binding flags after canvas rebuild.
- [x] Regression coverage extended. DOD: `GraphicsScalability14GRPEditTests.SuitHud_SavingProgressPulseUsesCachedMaterialBindingState()` asserts no `.material ==` / `.material !=` comparisons in ensure/bind/dispose and verifies flag reset on hierarchy build.
- [x] Static validation passed. DOD: `git diff --check` returned only LF/CRLF warning, brace parser depth 0/min 0, `SAVING_PROGRESS_BINDING_SOURCE_PROOF=PASS`, `SUIT_HUD_PRESENTATION_HOT_SCAN=PASS`.
- [x] Compile throttle enforced without spam. DOD: CPU samples were 30.7%, 22.7%, 37.5% and no compiler processes were listed, but `dotnet build` was not launched because the latest known project graph fails before C# compile and this loop had source-only UI binding changes; no orphan process was created.

## Loop 12
- [x] Mandates and active state rechecked. DOD: status/rationale read first; 14GRP remains direct graphics directive with no `CURRENT_BATCH.md` XML block; rejected adjacent task bleed.
- [x] HUD TMP font material lookup cached. DOD: `SuitHUDV4CanvasOverlay.CreateText()` now resolves a local `TMP_FontAsset` once and binds `fontSharedMaterial` through `ResolveFontSharedMaterial()` instead of reading `label.font.material` for every created label; rejected repeated TMP material getter during canvas rebuild; estimate cold rebuild cost reduced, no steady-frame work added.
- [x] Font material cache invalidation added. DOD: two-slot font/material cache is reset in `InvalidateVisualCaches()` so copied/rebuilt HUD configs cannot retain stale TMP material ownership; rejected unbounded dictionary/cache.
- [x] Regression coverage extended. DOD: `GraphicsScalability14GRPEditTests.SuitHud_TextCreationUsesCachedFontSharedMaterial()` asserts CreateText no longer reads `label.font.material`, resolver has two identity slots, and invalidation clears the cache.
- [x] Static validation passed. DOD: `git diff --check` returned only LF/CRLF warning, brace parser depth 0/min 0, `SUIT_HUD_FONT_MATERIAL_CACHE_SOURCE_PROOF=PASS`, `SUIT_HUD_HOT_TOKEN_SCAN=PASS`.
- [x] Compile throttle enforced. DOD: build not launched because foreign `dotnet` PID 44748 was active and CPU sampled 67.7%, 100%, 100%; no compiler process was started by 14GRP.

## Loop 13
- [x] Mandates and active state rechecked. DOD: status/rationale read first, AGENTS/domain and graphics/zero-GC/cinematic-cheat mandates re-read, and exact `CURRENT_BATCH.md` match for `14GRP` still absent; rejected neighboring prompt bleed.
- [x] Dithered HUD background material getter removed. DOD: `SuitHUDV4CanvasOverlay.ApplyDitheredBackgroundMaterial()` now binds `_ditheredUiBackgroundMaterial` without reading `image.material !=`; rejected Unity `Graphic.material` getter as state oracle even in cold hierarchy rebuild paths; estimate 0-1 us avoided per dithered backdrop bind and lower material-state drift risk.
- [x] Regression coverage extended. DOD: `GraphicsScalability14GRPEditTests.SuitHud_DitheredBackgroundBindingDoesNotReadGraphicMaterial()` asserts the dithered bind path calls `EnsureDitheredUiBackgroundRuntimeResources()`, writes the material, and contains no `image.material ==` / `image.material !=` comparisons.
- [x] Static validation passed. DOD: `git diff --check` returned only LF/CRLF warning, comment-aware brace parser returned depth 0/min 0/state code for HUD and tests, `DITHERED_BACKGROUND_MATERIAL_SOURCE_PROOF=PASS`, `LOOP13_HOT_TOKEN_SCAN=PASS`.
- [x] Compile throttle enforced. DOD: build not launched because CPU sampled 99.4%, 99.4%, 97.3% and foreign `dotnet` PID 65920 was active; no compiler process was started by 14GRP.

## Loop 14
- [x] Mandates and active state rechecked. DOD: status/rationale read first; 14GRP remains direct graphics directive with no `CURRENT_BATCH.md` XML block; rejected neighboring prompt bleed.
- [x] Radar presentation invalid-quality fallback fixed. DOD: `FakeRadarBlipController` and `AcousticRadarSphereRenderer` now sanitize non-finite `HomeostasisBrain.GlobalQualityWeight` to `0f` before resolving blip/matrix capacity; rejected invalid quality resolving to overkill visual load; estimate prevents up to 48 extra fake radar blips and 8 thermal ghosts under NaN/Inf quality fault.
- [x] Capacity math centralized per component. DOD: both radar fakes now route capacity through `ResolveQualityCapacity()` and `SmoothStep01()` consumes sanitized quality; rejected duplicated `math.lerp`/`math.round` blocks with unsafe fallback semantics.
- [x] Regression coverage extended. DOD: `GraphicsScalability14GRPEditTests.RadarPresentation_InvalidQualityFallsBackToMinimumCapacity()` asserts sanitized global quality route, minimum-capacity resolver, and `? value : 0f` fallback in both radar fakes.
- [x] Static validation passed. DOD: `git diff --check` returned only LF/CRLF warnings, comment-aware brace parser returned depth 0/min 0/state code for both radar files and tests, `RADAR_QUALITY_SANITIZE_SOURCE_PROOF=PASS`, `LOOP14_RUNTIME_HOT_TOKEN_SCAN=PASS`, `LOOP14_TEST_SOURCE_SHAPE=PASS`.
- [x] Compile throttle enforced without spam. DOD: CPU sampled 45.3%, 38.4%, 38.9% and no compiler process was listed, but `dotnet build` was not launched because the current project graph repeatedly fails before C# compile and this loop had source-proven UI/VFX capacity math only; no orphan process was created.
