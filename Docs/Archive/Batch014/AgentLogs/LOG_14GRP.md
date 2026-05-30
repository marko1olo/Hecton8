# LOG_14GRP

Session opened. Domain: Graphics Scalability / Rendering / VFX. Active XML prompt absent for `14GRP`; direct user prompt governs.

Entry:
What was wrong: Dynamic point-light mock seed path referenced NativeArray aliases after their guarded scope, blocking compilation and weakening proof that mutation windows were flattened.
What was done: Captured scalar `seededCapacity` under the mock seed guard, released the guard in `finally`, committed the source manifest using scalar data only, and added an editor regression that scans the method shape.
Cinematic Cheats used: Preserved DTO/mock-light culling data path; no Unity `Light` objects, no per-frame scene discovery, no physical light simulation.
Exact Microseconds saved: 0 runtime us measured; 0 runtime cost added; one failed compile/import cycle avoided under 82% CPU load. Runtime profiler proof remains pending.

Entry:
What was wrong: HUD bios font swap drain read `_queuedHudFont.material` from `LateFrameTick`, creating a hot visual-sync property lookup on swap frames.
What was done: Added cached terminal/primary/queued font material fields, moved readiness/material refresh to cold lifecycle and SlowTick, and added a source-shape editor regression proving `LateFrameTick` and font-swap state update do not read `.material`.
Cinematic Cheats used: Kept the staged 18-label visual swap budget; no instant whole-HUD rebuild, no allocation-heavy UI refresh.
Exact Microseconds saved: Estimated 0-5 us on font swap frames; steady-frame cost unchanged; profiler measurement pending.

Entry:
What was wrong: Visor HUD retained a legacy camera command-buffer scissor fallback in runtime source, conflicting with RenderGraph-only URP rules.
What was done: Removed `AddCommandBuffer`, `RemoveCommandBuffer`, `CameraEvent`, and `new CommandBuffer` from `VisorHUDController`; updated 14GRP and existing visor source-shape tests.
Cinematic Cheats used: Kept visor clipping responsibility in RenderGraph/URP presentation instead of hidden built-in camera command buffers.
Exact Microseconds saved: 0 us measured on URP steady state; hidden legacy fallback removed. Direct compute command buffers in `GlobalShaderDispatcher` and `ParasiteSwarmGpuRuntime` remain recorded follow-up, not silently accepted.

Entry:
What was wrong: Compile proof was previously static-only because CPU throttle was violated.
What was done: After CPU dropped to 33%, ran one runtime compile gate: `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`. Result: build succeeded, 0 warnings, 0 errors. Editor/test assembly skipped at 77% CPU after build.
Cinematic Cheats used: None; verification only.
Exact Microseconds saved: No runtime performance claim. CPU saved by refusing repeated/editor builds under throttle.

Entry:
What was wrong: `GlobalShaderDispatcher` owned a persistent `CommandBuffer` for plain shader-global publication, then executed it every visual-sync dispatch. That route was heavier than the existing bridge fallback, which already uses direct `Shader.SetGlobal*`.
What was done: Removed `UnityEngine.Rendering`, static command-buffer state, cold command-buffer allocation, ready gate, and `Graphics.ExecuteCommandBuffer` from `GlobalShaderDispatcher`. Replaced all global vector/float/buffer/texture uploads with direct `Shader.SetGlobal*` inside `LateFrameTick`. Added `GraphicsScalability14GRPEditTests.GlobalShaderDispatcher_PublishesGlobalsWithoutCommandBuffer()`.
Cinematic Cheats used: Kept shader globals as cheap presentation scalars/buffers; no physical fog simulation, no RenderGraph ceremony for scalar globals, no compute-dispatch surgery.
Exact Microseconds saved: One cold `CommandBuffer` allocation removed and one visual-sync command-buffer execute removed per global dispatch. Exact us requires profiler capture; runtime compile proof succeeded: `Assembly-CSharp.csproj`, 0 warnings, 0 errors, 27.56 s.

Entry:
What was wrong: `ThermalDynamicResolutionAdapter.WriteTelemetry()` held the telemetry DataVault guard while a non-finite DRS state could trigger black-box dump and `ResetInvalidScaleStateAndCommit()`. The reset path mutates scale state through a separate guard, creating a nested write-lock path during fault recovery.
What was done: Converted `WriteTelemetry()` to return whether it performed invalid-state reset, kept ring writes inside one telemetry guard, released the guard in `finally`, then performed dump/reset outside that guard. `RecoverInvalidScaleState()` now calls direct reset only when telemetry acquisition failed. Added `GraphicsScalability14GRPEditTests.ThermalDrsTelemetry_ReleasesTelemetryGuardBeforeInvalidStateReset()`.
Cinematic Cheats used: Preserved DRS as a presentation-scale recovery cheat; no physical thermal simulation and no managed telemetry copy added.
Exact Microseconds saved: No steady-frame us measured. The concrete win is removal of an unbounded nested-lock stall/deadlock vector on non-finite recovery. Source checks passed; normal runtime build failed before C# compile on generated MSBuild project-reference cycle in `Unity.RenderPipelines.Universal.Runtime.csproj` / `MoreMountains.Tools.csproj`; one narrower `/p:BuildProjectReferences=false` pass also failed before C# compile on `MoreMountains.Tools.csproj`; MSBuild node-reuse process stopped by PID after failure.

Entry:
What was wrong: `FontStreamingManager` could still reach TMP font material/readiness evaluation from `LateFrameTick()` via `LocalizedFontResolver.IsFontReady()` and `targetFont.material`.
What was done: Added cached `_primaryFontMaterial`, `_biosFallbackFont`, and `_biosFallbackFontMaterial`; refreshed them in `OnEnable`, `Start`, `HandleSceneLoaded`, and language-change cold paths; made visual-sync readiness and swap queueing consume cached material references only. Added a source-shape regression.
Cinematic Cheats used: Kept staged UI font swap as a cheap presentation trick; no full HUD rebuild, no runtime material instantiation.
Exact Microseconds saved: Estimated 0-5 us on font swap frames; steady-frame cost unchanged. Static proof passed; runtime compile is blocked before C# by generated project graph.

Entry:
What was wrong: `VfxComputeParticleBudgetCatalog.ApplyKillSwitchCount()` hard-returned zero for bubble/debris pools under `NonCriticalVfxMask`, creating binary visual loss instead of graceful scalability.
What was done: Replaced zero kill with pressure-smoothed survival: `EmergencyNonCriticalVfxMultiplierPermille = 125`, `EmergencyBubbleSurvivalCount = 32`, and `EmergencyDebrisSurvivalCount = 8`. Added regression coverage for bubble/debris survival and ungated count preservation.
Cinematic Cheats used: Preserve sparse bubbles/debris as cheap depth/motion fakes under emergency pressure instead of simulating expensive ambience or cutting it completely.
Exact Microseconds saved: No profiler us measured. Low-tier emergency cost remains bounded at 12.5% of active non-critical pools plus floor, not full pool; visual continuity preserved.

Entry:
What was wrong: `BiolumPulseSyncRuntime.DumpBlackBox()` held `BlackBoxGuardMask` and then acquired `BlackBoxDumpScratchGuardMask` inside snapshot serialization, creating a nested DataVault guard path during NaN/job-overrun fault handling.
What was done: Added persistent owner-owned `NativeArray<BiolumPulseTelemetryEntry>[300]` snapshot. Fault dump now copies ring entries under `BlackBoxGuardMask`, releases in `finally`, then serializes snapshot bytes under `BlackBoxDumpScratchGuardMask`. Added setup/dispose coverage and a source-shape regression proving split guards.
Cinematic Cheats used: Keep the 300-frame black box as cheap high-level visual telemetry; no managed allocation, no direct file write on the presentation frame, no physical simulation.
Exact Microseconds saved: Steady-frame cost 0 us. Adds one cold 19.2 KB native allocation. Fault-frame deadlock/stall vector removed; exact fault-frame us needs profiler capture.

Entry:
What was wrong: Latest code batch needed verification without pretending generated project graph errors are C# compile errors.
What was done: Ran brace parser, scoped source proofs, hot forbidden-token scan, `git diff --check` for tracked runtime files, and whitespace scan for the untracked regression file. One throttle-compliant runtime `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` ran at 36% CPU with no active build processes and failed before C# compile on generated `ResolveProjectReferences` cycles in `Unity.RenderPipelines.Universal.Runtime.csproj` and `MoreMountains.Tools.csproj`; leftover `dotnet` node-reuse process was stopped. Roslyn AST parse from local plugin assemblies was attempted but unavailable in Windows PowerShell due `Roslyn.Utilities.StringTable`.
Cinematic Cheats used: None; verification only.
Exact Microseconds saved: No runtime claim. CPU protected by one build attempt only; no orphan build processes left.

Entry:
What was wrong: `SuitHUDV4CanvasOverlay.ApplyAcousticRadarVisuals()` read `Image.material` in the acoustic radar visual-sync path to check whether the runtime material was bound.
What was done: Added `_acousticRadarOverlayMaterialBound`, replaced the hot material getter comparison with `BindAcousticRadarOverlayMaterial()`, and made disposal/disable paths clear the owner-state flag. Added `GraphicsScalability14GRPEditTests.SuitHud_AcousticRadarVisualSyncUsesCachedMaterialBindingState()`.
Cinematic Cheats used: Kept acoustic radar as a shader-driven visor fake; no new simulation, no extra material instances, no canvas rebuild.
Exact Microseconds saved: Estimated 0-2 us on radar-active visual-sync frames; steady memory delta is one bool. Static proof passed: `SUIT_HUD_ACOUSTIC_BINDING_SOURCE_PROOF=PASS`, brace parser pass, hot-token scan pass. Build not launched because another `dotnet build Hecton8.slnx -maxcpucount:1 --no-restore...` was active at PID 58308 and CPU was 48%.
2026-05-30 Loop 7 - Marine snow continuous pressure policy

What was wrong:
- `HectonMarineSnowRenderer.BuildContinuousScalabilityParams()` still used binary policy multipliers for pressure masks: advection and fake occlusion were effectively hard-disabled.
- `BuildContinuousPressureBudget()` also collapsed masked flow resampling to zero cadence.

What was done:
- Added `VfxComputeParticleBudgetCatalog.ResolvePolicyQualityWeight()` with pressure-smoothed floor..1 policy compression.
- Added `ResolvePolicyFlowResampleFrames()` with sparse 1..16 frame survival cadence for masked advection.
- Routed marine snow flow, SDF collision, and depth collision quality through continuous policy weights.
- Replaced hard `flowResampleFrames = 0` with the sparse cadence resolver.
- Added editor regression coverage in `GraphicsScalability14GRPEditTests`.

Cinematic cheats used:
- Kept marine snow as presentation-only drift/depth belief, not fluid truth.
- Preserved sparse visual motion under pressure instead of simulating more particles or more flow samples.

Exact microseconds saved:
- Profiler proof pending. CPU delta is expected to be below measurement noise because the added math runs during budget refresh, not per-particle CPU simulation.
- Exact verified saving: zero compiler CPU consumed in this loop because CPU was 99.1% and the build gate blocked compilation.

Verification:
- `git diff --check`: pass except existing LF/CRLF warnings.
- Brace parser: depth 0/min 0 for touched files.
- Source proof: `MARINE_SNOW_CONTINUOUS_POLICY_SOURCE_PROOF=PASS`.
- Hot-token scan: `HOT_FORBIDDEN_SCAN_DONE`, no forbidden token printed.
- Build: not launched; CPU 99.1% > 50% gate.

2026-05-30 Loop 8 - Shadow pressure continuity and single-trail draw cleanup

What was wrong:
- Marine snow still had a binary fake-shadow policy cap: the volumetric pressure mask forced tap count to middle tier through renderer-local math.
- `NativeTrailRenderer` drew one generated trail mesh through `Graphics.DrawMeshInstanced` with a retained one-element matrix array.

What was done:
- Added `VfxComputeParticleBudgetCatalog.ResolvePolicyShadowTaps()` with pressure-smoothed compression.
- Routed `HectonMarineSnowRenderer.BuildContinuousPressureBudget()` and `_debugBudgetedShadowTaps` through the shared resolver.
- Replaced `NativeTrailRenderer.Render()` single-instance `DrawMeshInstanced` call with `Graphics.DrawMesh` and removed `_drawMatrices`.
- Added editor regression coverage for shadow tap compression, binary shadow cap removal, and single-mesh trail rendering.

Cinematic cheats used:
- Kept marine snow depth/fog occlusion as a fake tap budget, not physical scattering truth.
- Kept trails as one generated presentation mesh, not a new batching/BRG subsystem.

Exact microseconds saved:
- Profiler proof pending. Verified concrete savings are one cold `Matrix4x4[1]` allocation removed and no single-instance instanced draw path in `NativeTrailRenderer`.
- Static source proof passed: `GRAPHICS_POLICY_SOURCE_PROOF=PASS`, brace parser pass, hot forbidden-token scan no matches.
- One throttled build attempt ran only at CPU 43.9% with no active compilers and failed before C# compile on generated `ResolveProjectReferences` cycles in `Unity.RenderPipelines.Universal.Runtime.csproj` and `MoreMountains.Tools.csproj`; residual `dotnet` PID 11448 stopped.

2026-05-30 Loop 9 - Fluid decal fallback and single-hologram draw cleanup

What was wrong:
- `AbyssalFluidDecalManager` defaulted to `screenSpaceFluidDecals=true`, but `CopyScreenSpaceDecals()` had no source callsite. Aftermath decals could become invisible while still consuming update work.
- Pressure sprays always prepared up to the full matrix payload, with no continuous `GlobalQualityWeight` / pressure draw limit.
- `PDADataLogTab` rendered one selected lore hologram through `DrawMeshInstanced` and retained `Matrix4x4[1]`.

What was done:
- Added screen-space consumer tracking in `CopyScreenSpaceDecals()` and a two-frame fallback grace window. Mesh fallback now activates only when the screen-space collector is absent.
- Added `ResolvePressureSprayDrawLimit()` and routed pressure spray matrix append through a continuous quality/pressure draw cap with an 18.75% floor.
- Replaced PDA lore hologram `DrawMeshInstanced` with `Graphics.DrawMesh` and deleted `_hologramMatrices`.
- Added source-shape regression coverage for abyssal fallback, pressure spray continuity, and PDA single-mesh draw.

Cinematic cheats used:
- Kept fluid aftermath as screen-space/procedural quad presentation, not fluid truth.
- Kept pressure spray as sparse fake ribbon density controlled by quality pressure, not a physical jet simulation.
- Kept PDA hologram as one presentation mesh, not a new batching subsystem.

Exact microseconds saved:
- Profiler proof pending. Verified concrete savings: one cold `Matrix4x4[1]` field removed from `PDADataLogTab`, full pressure-spray submission avoided under low quality/pressure, and no invisible fluid-decal update sink when the screen-space collector is absent.
- Static proof passed: `ABYSSAL_FLUID_DECAL_SOURCE_PROOF=PASS`, `ABYSSAL_HOT_FORBIDDEN_SCAN=PASS`, `PDA_HOLOGRAM_SOURCE_PROOF=PASS`, `PDA_HOLOGRAM_HOT_SCAN=PASS`, brace parser pass, `git diff --check` pass except LF/CRLF warnings.
- Build not launched: CPU sampled 61.3%, 64.8%, then 97.5%, all above the 50% gate; no compiler processes existed and no orphan process was created.

2026-05-30 Loop 10 - UI hologram presentation payload cleanup

What was wrong:
- `SuitHUDV4CanvasOverlay` retained scanner hologram mesh-era fields even though the active scanner hologram is a flat canvas fake.
- `HectonFabricatorUI` rendered the selected recipe preview as one `DrawMeshInstanced` instance with a retained one-element matrix array.

What was done:
- Removed `_scannerHologramMatrices`, `_scannerHologramPropertyBlock`, `_scannerHologramMaterial`, `_scannerHologramFallbackMesh`, and the Awake scanner `MaterialPropertyBlock` allocation.
- Changed `RenderSelectedRecipeHologram()` to convert its `float4x4` to stack `Matrix4x4` and call `Graphics.DrawMesh`.
- Preserved ingredient fan-out instancing through `_hologramMatrixBuffer` and `RenderActiveRecipeHologram()`.
- Added editor source-shape regressions for scanner flat-hologram cleanup and fabricator selected-preview direct draw.

Cinematic cheats used:
- Kept scanner hologram as cheap flat canvas scanline/body/core rectangles, not a mesh/material simulation.
- Kept selected fabricator preview as one presentation mesh, not a new batching subsystem.

Exact microseconds saved:
- Profiler proof pending. Verified concrete savings: one stale scanner `Matrix4x4[1]`, one stale scanner `MaterialPropertyBlock`, and one selected-recipe `Matrix4x4[1]` payload removed.
- Static proof passed: `SINGLE_INSTANCE_PRESENTATION_SOURCE_PROOF=PASS`, `FABRICATOR_SELECTED_HOLOGRAM_SOURCE_PROOF=PASS`, `SUIT_SCANNER_FLAT_HOLOGRAM_SOURCE_PROOF=PASS`, `PRESENTATION_HOT_FORBIDDEN_SCAN=PASS`, brace parser pass, `git diff --check` pass except LF/CRLF warnings.
- Build not launched: CPU sampled 66.1%, 93.0%, and 76.8%, above the 50% gate; no compiler process output was present and no orphan process was created.

2026-05-30 Loop 11 - Saving progress HUD material binding hardening

What was wrong:
- `SuitHUDV4CanvasOverlay` DATA save lamp/needle pulse binding used `Image.material` equality reads as state checks.
- Canvas rebuild could make a cached binding assumption stale if the Image refs were recreated.

What was done:
- Added `_savingProgressDataLampPulseMaterialBound` and `_savingProgressDataNeedlePulseMaterialBound`.
- Replaced material equality checks with `BindSavingProgressPulseMaterials()`.
- Reset the flags in `DisposeSavingProgressPulseRuntimeResources()`, `BuildSavingProgressHierarchy()`, and `InvalidateVisualCaches()`.
- Added editor source-shape regression coverage for the save pulse binding route.

Cinematic cheats used:
- Kept the DATA save indicator as a cheap shader-time UI pulse, not a particle or mesh effect.
- Kept material ownership explicit instead of reading Unity Graphic material state at runtime.

Exact microseconds saved:
- Profiler proof pending. Expected saving is small and lifecycle-bound, roughly 0-2 us on save-indicator setup/dispose frames.
- Static proof passed: `SAVING_PROGRESS_BINDING_SOURCE_PROOF=PASS`, `SUIT_HUD_PRESENTATION_HOT_SCAN=PASS`, brace parser pass, `git diff --check` pass except LF/CRLF warning.
- Build not launched: CPU was under gate (30.7%, 22.7%, 37.5%) and no compiler processes were listed, but the latest generated project graph is known to fail before C# compile; rerunning it for this UI-only patch would be build spam. No orphan process was created.

2026-05-30 Loop 12 - HUD text factory TMP material cache

What was wrong:
- `SuitHUDV4CanvasOverlay.CreateText()` read `label.font.material` for every generated TMP label during HUD hierarchy rebuild.
- That made TMP/Graphic material getter state part of the hierarchy factory instead of owner-owned cached state.

What was done:
- Added `_cachedFontMaterialAsset0/1` and `_cachedFontSharedMaterial0/1`.
- Changed `CreateText()` to resolve one local `TMP_FontAsset` and call `ResolveFontSharedMaterial(resolvedFont)`.
- Added two-slot identity cache and cleared it in `InvalidateVisualCaches()`.
- Added editor source-shape regression coverage for the cache route.

Cinematic cheats used:
- Kept HUD text as TMP labels with cached shared material, not runtime material clones or mesh text effects.
- Kept the cache bounded to two likely fonts instead of over-engineering a managed map.

Exact microseconds saved:
- Profiler proof pending. Expected saving is cold rebuild-only and small; steady-frame cost remains 0 us.
- Static proof passed: `SUIT_HUD_FONT_MATERIAL_CACHE_SOURCE_PROOF=PASS`, `SUIT_HUD_HOT_TOKEN_SCAN=PASS`, brace parser pass, `git diff --check` pass except LF/CRLF warning.
- Build not launched: foreign `dotnet` PID 44748 was active and CPU sampled 67.7%, 100%, 100%; no compiler process was started by 14GRP.
2026-05-30 Loop 13
What was wrong: `SuitHUDV4CanvasOverlay.ApplyDitheredBackgroundMaterial()` still used `image.material != _ditheredUiBackgroundMaterial` before binding the runtime dither backdrop. That is a Unity Graphic material getter state route in HUD presentation setup.
What was done: Removed the getter comparison and bound `_ditheredUiBackgroundMaterial` directly after cold runtime-resource ensure. Added `GraphicsScalability14GRPEditTests.SuitHud_DitheredBackgroundBindingDoesNotReadGraphicMaterial()` to block rollback.
Cinematic cheats used: Kept the dithered alpha-clip backdrop as a shader/UI fake. No physical surface, no mesh path, no new subsystem.
Exact microseconds saved: Runtime profiler pending. Static estimate: 0-1 us on dithered backdrop bind frames; steady-frame delta 0 us.
Verification: `git diff --check` pass except LF/CRLF warning; comment-aware brace parser depth 0/min 0/state code for HUD and test; `DITHERED_BACKGROUND_MATERIAL_SOURCE_PROOF=PASS`; `LOOP13_HOT_TOKEN_SCAN=PASS`. Build skipped by throttle: CPU 99.4/99.4/97.3 and foreign `dotnet` PID 65920 active.
2026-05-30 Loop 14
What was wrong: `FakeRadarBlipController` and `AcousticRadarSphereRenderer` sanitized non-finite `GlobalQualityWeight` to visual overkill (`1f`). A corrupted quality signal could therefore push fake radar presentation to max blip/matrix capacity.
What was done: Added `SanitizeQualityWeight01()` fallback `0f` and `ResolveQualityCapacity()` in both radar fakes. Fake hostile radar and acoustic voxel radar now fail toward minimum readable capacity under NaN/Inf quality.
Cinematic cheats used: Kept both radar systems as cheap presentation fakes. No gameplay truth, no physics, no new global route.
Exact microseconds saved: Profiler pending. Under invalid quality, hostile radar avoids up to 48 blips and 8 thermal ghosts; acoustic radar avoids up to 48 voxel matrices. Valid-quality steady-state cost remains scalar helper math.
Verification: `git diff --check` pass except LF/CRLF warnings; comment-aware brace parser depth 0/min 0/state code; `RADAR_QUALITY_SANITIZE_SOURCE_PROOF=PASS`; `LOOP14_RUNTIME_HOT_TOKEN_SCAN=PASS`; `LOOP14_TEST_SOURCE_SHAPE=PASS`. Build not launched: known generated MSBuild graph fails before C# compile; CPU 45.3/38.4/38.9 and no compiler process listed.
