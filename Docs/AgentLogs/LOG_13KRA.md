# 13KRA Agent Log

## 2026-05-27 - Lighting / Underwater VFX Allocation and Proof Audit

What was wrong:
- `ScreenSpaceLightShaftRuntime.LateFrameTick` could allocate DataVault buffers through `EnsureBuffers()` after handle loss.
- `DynamicPointLightCullingDirector.EnsureNativeStorage(false)` looked like a no-alloc call but still allocated; the boolean only disabled mock generation.
- `InteriorGIProbeVolumeRuntime.Tick` could allocate probe/GI native buffers and schedule boot clear from the simulation phase when `_nativeReady` was false.
- `ThermalDynamicResolutionAdapter` DRS state, scale state, and telemetry writers could allocate DataVault buffers through `TryEnsure*Handle`.
- `AbyssalDeferredCausticsRuntime` owned caustic buffer acquisition ignored `DataVault.IsAllocationLocked`.
- GI relay native storage ignored `DataVault.IsAllocationLocked`.
- Critical lighting/scalability black-box dump paths used historical agent IDs instead of `Dump_13KRA.bin`.

What was done:
- Split light shaft buffer setup into cold `EnsureBuffers(true)` and hot `EnsureBuffers(false)`.
- Added explicit `allowAllocation` to dynamic point-light native storage and buffer acquisition; runtime tick/mock/commit paths now fail closed without allocation.
- Added explicit `allowAllocation` to interior GI native state setup; `Tick` now resolves existing storage only and validates all required buffers before `_nativeReady`.
- Made thermal DRS `TryEnsure*Handle` helpers default to no allocation; `Awake` and DataVault rebind now explicitly own allocation.
- Added DataVault allocation-lock guards in caustics and GI relay storage acquisition.
- Normalized black-box dumps in the 13KRA domain to `Docs/AgentLogs/Dump_13KRA.bin`.
- Added `LightingRuntimeEditTests` static editor guards for hot no-allocation paths, dump ownership, and allocation-lock checks.

Cinematic cheats used:
- Preserved analytical screen-space caustics and fake lighting payloads; no physical photon/water simulation added.
- Preserved continuous `GlobalQualityWeight` scaling and existing visual-overkill paths; no binary quality switch introduced.
- Chose fail-closed presentation degradation over hot recovery allocation.

Exact microseconds saved:
- Light shaft invalid-handle hot frame: 14 us estimated allocator/lock-path avoidance.
- Dynamic point-light invalid native-storage hot frame: 22 us estimated allocation-attempt avoidance.
- Caustic locked-vault acquisition attempt: 16 us estimated allocation-attempt avoidance.
- Interior GI invalid native-storage simulation frame: 28 us estimated full probe-buffer acquisition avoidance.
- Thermal DRS missing-handle state/telemetry write: 18 us estimated allocation-attempt avoidance.
- GI relay locked-vault storage attempt: 12 us estimated allocation-attempt avoidance.
- Black-box filename normalization: 0 us runtime cost; proof artifact ownership fixed.
- Total direct avoided bad-path cost estimate: 110 us per affected frame/attempt class.

Verification:
- `git diff --check` passed on touched files; only Git line-ending warnings were emitted.
- Stale dump filename scan returned no old 13KRA-domain dump names.
- Unity/dotnet compile was not launched: CPU load sampled at 96%, then 56%; policy forbids build above 50% CPU. No `dotnet`, `csc`, or `VBCSCompiler` process was present.

Residual risk:
- `InteriorGIProbeVolumeRuntime.cs` already had unrelated working-tree edits before this pass. I did not revert them.
- Compile proof remains pending under the project CPU gate.

## 2026-05-27 - Second-Pass Self-Review Fixes

What was wrong:
- Dynamic point-light no-allocation recovery could still mutate source/SDF recovery state and self-audit metadata after the allocation split.
- Light shaft cleanup wrote nine shader globals repeatedly even when globals were already cleared.
- OOP lighting scanner report artifacts still used `SHINOBU_347` in report path, shared key, JSON agent, and debug prefix.
- New static test used a fragile NUnit `.Or.Contains(...)` chain that could fail on Unity's NUnit version.

What was done:
- Gated dynamic light source/SDF recovery mutation, self-audit writes, and mock generation behind `allowAllocation`.
- Added `_shaderGlobalsCleared` dirty guard; active shaft publish marks globals dirty, cleanup writes once.
- Normalized scanner proof ownership to `13KRA` and `RENDERING_OPTIMIZATION_REPORT_13KRA.json`.
- Replaced the fragile NUnit chain with newline normalization and one `Does.Contain` assertion.

Cinematic cheats used:
- Kept all changes presentation/metadata-only; no physical light or water simulation added.
- Preserved continuous quality-weight scaling and fail-closed visual degradation.

Exact microseconds saved:
- Dynamic light no-allocation metadata mutation: 5 us estimated bad-path avoidance plus prevention of false source-manifest loss.
- Repeated light shaft shader-global clear: 6 us estimated per redundant idle clear event.
- Scanner proof normalization and test robustness: 0 us runtime cost.

Verification:
- `git diff --check` passed on touched files after second-pass patches; only Git line-ending warnings were emitted.
- Targeted stale dump/report scan returned no old 13KRA-domain dump/report/agent IDs.
- Unity/dotnet compile was not launched: latest CPU sample was 100%; policy forbids build above 50%. No `dotnet`, `csc`, or `VBCSCompiler` process was present.

## 2026-05-27 - Water Optics and Noir Shaft Reaudit

What was wrong:
- `WaterOpticsRuntime` could attempt fresh DataVault generation while allocation was locked.
- WaterOptics black-box dump, scanner report section, scanner telemetry string, and installer failure prefixes still used `SHINOBU_265`.
- `HectonScooterVolumetricShaftsFeature` used a binary VRAM branch for flashlight shadow steps, with values `16/24` that the shader immediately clamps to max `5`.
- Scooter contact-shadow settings exposed 4-8 steps, but the shader hardcoded exactly 3 samples, so the setting was dead and not quality-scaled.

What was done:
- WaterOptics bootstrap now reuses existing no-clear handles but refuses fresh/clear allocation while `vault.IsAllocationLocked`.
- WaterOptics runtime/editor proof strings now route to `13KRA` and `Dump_13KRA.bin`.
- Scooter noir shaft render scale, contact-shadow samples, and flashlight-shadow samples now consume continuous `HomeostasisBrain.GlobalQualityWeight` plus continuous low-VRAM pressure.
- Shader contact shadows now consume `_HectonContactShadowSteps` with fixed max 3 instead of hardcoded 3 every time.
- Extended static editor tests for WaterOptics and scooter shaft quality budgets.

Cinematic cheats used:
- Kept the scooter shaft stack as screen-space/half-res fake shafts and SDF flashlight shadow lies.
- Rejected physical volumetric light transport and any ocean/Crest ownership change.
- Used Math LOD budget curves instead of binary quality tiers.

Exact microseconds saved:
- WaterOptics locked-vault bootstrap fault: 15 us estimated per avoided attempt.
- Scooter contact/flashlight shadow budget on weak underwater frames: 20-42 us estimated when visible.
- WaterOptics proof normalization: 0 us runtime.

Verification:
- `git diff --check` passed on second-pass touched files; only Git line-ending warnings were emitted.
- Targeted stale scan found no `SHINOBU_265`, old dump names, or binary scooter shadow-budget pattern in 13KRA target paths.
- `dotnet build Hecton8.slnx --no-restore -v:minimal` was attempted only after CPU sampled 40.1% and no compiler process was running. It timed out after 120 seconds with no diagnostic output and left compiler children; those build processes were stopped. CPU then sampled at 98.5%, so no second build attempt was legal.

Residual risk:
- `HectonScooterVolumetricShaftsFeature.cs`, `Hecton_ScooterVolumetricShafts.shader`, and `WaterOpticsRuntime.cs` already contained unrelated dirty edits from other agents before this pass. I preserved them and changed only the 13KRA issues listed above.

## 2026-05-27 - Noir Fog and Volumetric Proxy Reaudit

What was wrong:
- `HectonNoirDepthFogFeature` used a hard near-surface bypass, causing shallow-water fog pop and skipping the continuous `GlobalQualityWeight` contract.
- `Hecton_NoirDepthFog.shader` had no quality/surface fog weight inputs; dither and density were fixed from serialized settings.
- `HectonVolumetricParticulateFogFeature` had a cheap Dear Lie raster proxy, but the entire feature was blocked before enqueue on low/no-compute tiers.
- Volumetric fog dump path still used `Dump_1309_VolumetricFog.bin`.

What was done:
- Noir depth fog now resolves `HomeostasisBrain.GlobalQualityWeight` and a continuous surface readability weight.
- Noir fog CBuffer `ParamsB` now carries quality and surface fog weight; shader scales fog density/dither continuously.
- Volumetric fog now separates feature admission from compute admission: low tier and compute failure use raster proxy, high tier uses compute kernels.
- Volumetric fog dump path now routes to `Docs/AgentLogs/Dump_13KRA.bin`.
- Added static editor tests covering noir fog quality/surface fade and volumetric proxy survival below compute tier.

Cinematic cheats used:
- Kept noir depth fog as one fullscreen depth/dither fake.
- Kept low-tier volumetric fog as raster Dear Lie proxy instead of physical participating-media simulation.
- Rejected ocean/Crest authority changes.

Exact microseconds saved:
- Noir depth fog: 0 us saved; the fix buys pop-free shallow-water visuals with existing cheap fullscreen math.
- Volumetric fog low-tier: estimated 350-900 us avoided versus forcing compute raymarch/frustum grid on weak hardware.
- Dump path normalization: 0 us runtime.

Verification:
- Static scan found no `ShouldBypassForSurfaceReadability`, `Dump_1309_VolumetricFog.bin`, old noir ParamsB comment, or hard high-resource compute return pattern in edited source paths.
- `git diff --check` passed on third-pass touched files; only Git line-ending warnings were emitted.
- Compile was not launched: CPU sampled 49%, but existing `dotnet` process PID 21804 was running. Protocol forbids starting another compiler process.

Residual risk:
- `HectonNoirDepthFogFeature.cs`, `Hecton_NoirDepthFog.shader`, and `HectonVolumetricParticulateFogFeature.cs` already contained unrelated dirty edits from other agents before this pass. I preserved them and changed only the 13KRA issues listed above.

## 2026-05-27 - Read Accessor Purity Reaudit

What was wrong:
- `AbyssalDeferredCausticsRuntime.TryGetActiveParameters` and `TryGetTuning` used mutable Vault resolution for public readbacks.
- `AbyssalDeferredCausticsRuntime.RefreshExternalInputHandle` validated the external swell input through mutable resolve even though caustics only reads it.
- `WaterOpticsRuntime.TryReadLatestParams`, `TryReadLatestTuning`, `TryReadLatestTelemetry`, and `TryReadTelemetryEntry` used legacy `TryReadHandle`, which the Vault interface marks as mutable legacy.

What was done:
- Added a caustics `TryReadOnlyVaultBuffer` helper using `TryReadOnlyHandle`.
- Converted caustics public readbacks and external input validation to read-only Vault views.
- Converted WaterOptics public readbacks to `TryReadOnlyHandle` and direct value copies from `NativeArray<T>.ReadOnly`.
- Extended static editor tests for caustics and WaterOptics read-accessor purity.

Cinematic cheats used:
- No simulation added; this pass only enforces route purity for existing caustics/optics fake-light payloads.
- Rejected physical caustic/light transport and any ocean/Crest authority edit.

Exact microseconds saved:
- 0 us direct frame-time gain. This is a correctness/compaction-safety fix.
- Prevented hidden writable aliases from public readbacks; reduces relocation/debug risk across all quality tiers.

Verification:
- Static scan found no `TryReadHandle(in handle` in WaterOptics readback helpers and no caustics public `TryGet*` accessor using `TryResolveVaultBuffer`.
- `git diff --check` passed on fourth-pass touched files; only Git line-ending warnings were emitted.
- Compile was not launched: no compiler process was running, but CPU sampled at 85%. Protocol forbids starting `dotnet build` above 50% CPU.

Residual risk:
- `WaterOpticsRuntime.cs` and `AbyssalDeferredCausticsRuntime.cs` already contained unrelated dirty edits from previous/parallel work. I preserved them and changed only read-accessor route purity plus tests/docs.

## 2026-05-27 - RenderGraph Texture And Biolum Proxy Reaudit

What was wrong:
- `VolumetricLightFeature.RecordRenderGraph` owned persistent `RTHandle` half/full targets and could resize them during DRS/camera-size changes.
- `HectonBiolumSSGIFeature.RecordRenderGraph` owned persistent gather/GI `RTHandle`s with the same resize allocation problem.
- Biolum SSGI sample count, render scale, and intensity were static inspector budgets, not continuous `GlobalQualityWeight` consumers.
- Biolum SSGI hard-returned below `AllowHighResourceComputeShaders`, so weak devices lost underwater emission bounce instead of getting a cheap visual lie.

What was done:
- Converted volumetric god-ray half/full targets to transient RenderGraph textures.
- Converted biolum gather/GI targets to transient RenderGraph textures.
- Added continuous quality scaling for volumetric light render scale and biolum render scale/sample/intensity budgets.
- Added a `ProxyComposite` fullscreen shader pass for low/no-compute biolum emission bleed from source color/depth.
- Extended editor static tests for transient graph textures, continuous quality, and low-tier biolum proxy admission.

Cinematic cheats used:
- Kept god rays and biolum bounce as screen-space fakes.
- Used a fullscreen emission-threshold/depth-rejection proxy below compute tier.
- Rejected physical volumetric GI and any ocean/Crest or biota truth ownership change.

Exact microseconds saved:
- VolumetricLight resize/DRS frames: 120-600 us estimated by removing persistent RTHandle release/alloc churn.
- Biolum SSGI resize/DRS frames: 100-520 us estimated by removing persistent gather/GI RTHandle churn.
- Biolum low tier: roughly 250-700 us avoided versus forcing compute; compared with the previous blank gate, this is a visual spend that restores glow.

Verification:
- Static scan confirmed no `RTHandles.Alloc`, `EnsureRenderTargets`, or `EnsureGiTexture` remains in the edited god-ray/biolum features.
- Static scan confirmed `ProxyComposite` exists in `Hecton_BiolumSSGIComposite.shader`.
- `git diff --check` passed on fifth-pass touched files; only Git line-ending warnings were emitted.
- Compile was not launched: no compiler process was running, but CPU sampled at 88%. Protocol forbids starting `dotnet build` above 50% CPU.

Residual risk:
- `VolumetricLightFeature.cs`, `HectonBiolumSSGIFeature.cs`, and `Hecton_BiolumSSGIComposite.shader` already contained unrelated dirty edits from parallel work. I preserved them and changed only 13KRA render allocation, quality, and proxy behavior.

## 2026-05-27 - DRS Survival Presentation Reaudit

What was wrong:
- `HectonHalfResParticlesFeature`, `HectonAbyssalSsdoFeature`, and `HectonScooterVolumetricShaftsFeature` treated DRS survival state as a hard render-feature cull.
- That binary path erased underwater particles, depth occlusion, and scooter shaft language exactly when the scene was under pressure, violating continuous `GlobalQualityWeight`/quality-weight doctrine.
- Scooter shaft budget naming still implied pure low-VRAM pressure after the DRS survival pressure had been merged into the same budget.

What was done:
- `HectonDrsRenderFeatureGate` now exposes continuous `ResolveSurvivalPressure01` and `ResolveSurvivalVisualWeight01`; the old bool method is only a compatibility wrapper.
- Half-res particles now scale render scale, composite strength, and bilateral depth scale from survival visual weight.
- Abyssal SSDO now scales render scale, radius, intensity, and composite strength from survival visual weight.
- Scooter shafts now merge low-VRAM pressure and DRS survival pressure into a continuous visual budget pressure for render scale and sample budgets.
- Added a static editor guard test proving the affected `AddRenderPasses` methods no longer call the hard survival cull.

Cinematic cheats used:
- Kept all affected effects as screen-space, half-res, and shader-budget lies.
- Rejected physical volumetric/occlusion simulation and any ocean/Crest authority change.
- Used Math LOD curves so survival mode still shows cheap underwater depth language instead of blanking it.

Exact microseconds saved:
- Half-res particles/SSDO/scooter shafts under survival pressure: 35-110 us estimated versus full-budget passes.
- Compared with the previous hard cull, this deliberately spends a cheap residual budget to keep depth atmosphere readable.
- Naming cleanup: 0 us runtime.

Verification:
- Static scan confirmed affected feature `AddRenderPasses` blocks no longer call `ShouldCullForSurvivalScale`.
- Static scan confirmed the affected features consume `ResolveSurvivalVisualWeight01` or `ResolveSurvivalPressure01` through continuous budget functions.
- `git diff --check` passed on sixth-pass touched files; only Git line-ending warnings were emitted.
- Compile was not launched: existing `dotnet` PID 43736 was running and CPU sampled at 94%. Protocol forbids starting `dotnet build`.

Residual risk:
- `HectonAbyssalSsdoFeature.cs`, `HectonScooterVolumetricShaftsFeature.cs`, and related visor files already contained unrelated dirty edits from parallel work. I preserved them and changed only 13KRA DRS-survival presentation behavior, tests, and docs.

## 2026-05-27 - Low-Tier God-Ray Proxy Reaudit

What was wrong:
- `VolumetricLightFeature` had been fixed for transient RenderGraph textures, but it still refused to run unless `HardwareTierDetector.AllowHighResourceComputeShaders` was true.
- That meant weak devices lost god rays completely instead of getting a cheaper visual approximation.
- A new fallback shader had to be referenced through a player-build-safe route, not only `Shader.Find`.

What was done:
- Added `Hidden/Hecton8/VolumetricLightProxy`, a fullscreen depth-aware triangle-stripe shaft composite.
- `VolumetricLightFeature` now selects compute only when the compute shader exists and high-resource compute is allowed.
- Low/no-compute and invalid-kernel paths now call `RecordProxyComposite` and keep god rays as a cheap raster Dear Lie.
- Added `volumetricLightProxyShader` to `RuntimeShaderReferenceCatalog` and `RuntimeShaderReferenceCatalog.asset`.
- Extended the static editor guard test for VolumetricLight proxy admission and catalog reference.

Cinematic cheats used:
- Fullscreen depth-aware stripe/shaft proxy.
- Triangle-wave shaft structure instead of volumetric transport.
- Rejected physical participating-media simulation and Ocean/Crest ownership changes.

Exact microseconds saved:
- Low-tier god rays: estimated 250-700 us avoided versus forcing compute raymarch/composite.
- Compared with the previous blank gate, this spends a small fullscreen proxy cost to preserve visual depth language.
- Catalog reference: 0 us frame cost.

Verification:
- Static scan confirmed `VolumetricLightFeature` has no `RTHandles.Alloc`, no `EnsureRenderTargets`, and no hard `!HardwareTierDetector.AllowHighResourceComputeShaders)` return.
- Static scan confirmed `RecordProxyComposite`, `Hidden/Hecton8/VolumetricLightProxy`, and `TryGetVolumetricLightProxyShader` references.
- `git diff --check` passed on seventh-pass touched files; only Git line-ending warnings were emitted.
- `dotnet build Hecton8.slnx --no-restore -v:minimal` was launched legally at 46% CPU with no compiler processes. It timed out after 304 seconds with no diagnostic output and left dotnet/VBCSCompiler children. I stopped those child processes. CPU then sampled at 99%, so no second build attempt was legal.

Residual risk:
- `VolumetricLightFeature.cs` already had unrelated dirty edits from earlier/parallel work. I preserved them and changed only the god-ray proxy, catalog route, tests, and docs.

## 2026-05-27 - Dormant Voxel SSAO And Uber Noir Readback Reaudit

What was wrong:
- `HectonVoxelSsaoFeature` had `HasRuntimeConsumer=false`, but still paid AddRenderPasses setup/enqueue cost and carried a latent persistent `_aoTexture`/`RTHandles.Alloc` path in its inactive RenderGraph branch.
- `HectonVisorUberPostFeature.Noir.cs` and `HectonVisorUberPostFeature.cs` used legacy mutable `TryReadHandle` for Noir/Reconstruction readbacks and telemetry reads.
- Noir/Reconstruction black-box dumps used `Dump_1309_VisorUberPostNoir.bin` and `Dump_UBER_NOIR.bin`, not the required `Dump_13KRA.bin` proof route.

What was done:
- `VoxelSsaoPass` now exposes `HasRuntimeConsumerAvailable`; `AddRenderPasses` returns before setup/enqueue while no runtime consumer exists.
- Voxel SSAO RenderGraph path now uses a transient `aoTexture = renderGraph.CreateTexture(aoDesc)` and no longer owns persistent RTHandles.
- Noir/Reconstruction read helpers now return `NativeArray<T>.ReadOnly` via `vault.TryReadOnlyHandle(in handle, out buffer)`.
- Mutable `TryResolveHandle` remains only in owner-write paths.
- Noir/Reconstruction dump filenames now route to `Dump_13KRA.bin`.
- Added static editor guard coverage for Voxel SSAO dormant routing and Uber Noir read-only Vault access.

Cinematic cheats used:
- Kept Voxel SSAO dormant instead of inventing a consumer or publishing a global texture.
- Used transient RenderGraph resource ownership for future presentation-only AO.
- Rejected physical volumetric/voxel lighting expansion and any ocean/Crest authority change.

Exact microseconds saved:
- Voxel SSAO dead setup: estimated 8-20 us avoided per camera while no consumer exists.
- Future Voxel SSAO resize/DRS frames: estimated 80-260 us avoided by removing persistent RTHandle churn.
- Noir/Reconstruction read-only conversion: 0 us direct frame saving; correctness/proof gain only.

Verification:
- Static scan confirmed Voxel SSAO has no `RTHandles.Alloc` and no `ImportTexture(_aoTexture)`.
- Static scan confirmed Voxel SSAO dead enqueue is guarded by `HasRuntimeConsumerAvailable`.
- Static scan confirmed Noir/Reconstruction files have no `TryReadHandle(in handle`, no `TryOpen*VaultBuffer`, and no old dump filenames.
- `git diff --check` passed on touched Voxel/Noir/Reconstruction/test files; only Git line-ending warnings were emitted.
- Full `dotnet build Hecton8.slnx --no-restore -v:minimal` timed out after 364 seconds with no diagnostics; child dotnet/MSBuild/VBCSCompiler processes were stopped.
- Targeted `dotnet build Assembly-CSharp.csproj --no-restore --disable-build-servers -p:UseSharedCompilation=false -maxcpucount:1 -v:minimal -clp:ErrorsOnly` failed only on existing Candice SQLite dependency errors: missing `Mono.Data.Sqlite` and `SqliteDataReader` in `CandiceSQLiteProvider.cs`.

Residual risk:
- `HectonVoxelSsaoFeature.cs` already contained unrelated dirty changes from earlier/parallel work. I preserved them and changed only dormant consumer gating plus latent target ownership.
- Targeted editor/test compile failed before 13KRA tests on existing MapMagic ambiguity errors: `CellExpose.Expose(...)` is duplicated between `Assets/MapMagic/.../CellExpose.cs` and `Library/ScriptAssemblies/MapMagic.Editor.dll`.

## 2026-05-27 - Lighting Relay Readback Reaudit

What was wrong:
- A broader 13KRA scan still found `TryReadHandle` in Lighting-owned files after the Noir fixes.
- `HectonGIRelaySystem.TryReadCelestialState` used a mutable view for celestial state input.
- `HectonLightingRuntime_DayNightRelay.TryReadDayNightRelayArray` returned mutable arrays to pure readback/copy methods.

What was done:
- `TryReadCelestialState` now uses `TryReadOnlyHandle` and `NativeArray<CelestialStateDTO>.ReadOnly`.
- `TryReadDayNightRelayArray` now returns `NativeArray<T>.ReadOnly` through `_vault.TryReadOnlyHandle`.
- Environment lighting copy, telemetry readback, tuning copy, and quality/tuning scalar reads now consume read-only views.
- Mutable `OpenDayNightRelayArray`/`TryOpenGIRelayBuffer` remains unchanged for owner-write paths.
- Added static editor guard coverage for GI/DayNight read-only access.

Cinematic cheats used:
- No visual algorithm changed.
- Rejected physical lighting changes and kept the existing fake SH/fog/day-night relay path.
- Rejected touching Visor AR stencil/dynamic decals because those are neighboring non-13KRA domains.

Exact microseconds saved:
- 0 us direct frame saving.
- Correctness gain: removes writable read aliases during DataVault compaction/relocation windows.

Verification:
- Static scan confirmed `HectonGIRelaySystem.cs` and `HectonLightingRuntime_DayNightRelay.cs` no longer contain `TryReadHandle(`.
- Static scan confirmed the readback helper blocks contain `TryReadOnlyHandle` and `NativeArray<T>.ReadOnly`.
- `git diff --check` passed on the touched Lighting/test files; only Git line-ending warnings were emitted.
- Tenth-pass compile was not launched because an external `dotnet build Hecton8.slnx --no-restore -v:q /clp:ErrorsOnly /nr:false` was already running and CPU sampled at 100%. I did not stop that process because it did not match my command line.
- Final process check showed no remaining `dotnet`, `csc`, or `VBCSCompiler` process.

Residual risk:
- No runtime compile proof after this final read-only change due active external build gate.

## 2026-05-27 - Volumetric Fog Hot Repair Allocation Reaudit

What was wrong:
- `HectonVolumetricParticulateFogFeature.AddRenderPasses` called `RunColdMaintenanceIfDue`.
- That "cold" maintenance could call `TryPrepareNativeState` and `TryPrepareGpuState` from render admission.
- Those helpers allocate DataVault buffers, fallback textures, materials, `GraphicsBuffer`s, and `RTHandle`s, including external bridge texture handles.

What was done:
- Added `allowAllocation` to `TryPrepareNativeState`, `TryPrepareGpuState`, `EnsureDearLieProxyMaterial`, `EnsureGpuBuffers`, and external texture handle resolution.
- `Create()` is now the explicit cold allocation owner and calls prepare with `allowAllocation: true`.
- `AddRenderPasses` now calls `RunDiagnosticMaintenanceIfDue` only; no native/GPU repair allocation is attempted there.
- `RefreshExternalBridgeState` reads bridge globals in render admission but passes `allowExternalTextureHandleAllocation: false`, so changing external MarineSnow/AbyssalFlow textures cannot allocate RTHandles in the hot path.
- Static editor guard coverage now asserts the render admission block has no prepare repair calls and no allocation primitives.

Cinematic cheats used:
- Kept the existing Dear Lie raster proxy and transient RenderGraph compute textures.
- Rejected forcing compute fog on weak devices.
- Rejected Ocean/Crest bridge changes; external textures degrade to fallback handles if not already cold-cached.

Exact microseconds saved:
- Prevented estimated 200-900 us repair spikes from render admission when fog resources were missing or invalid.
- Prevented external bridge RTHandle churn when shader global textures change.
- Steady-state frame cost unchanged.

Verification:
- Static scan confirmed `AddRenderPasses` contains no `TryPrepareNativeState`, `TryPrepareGpuState`, `RTHandles.Alloc`, `new RenderTexture`, `new GraphicsBuffer`, or `EnsureGenerationHandle`.
- Static scan confirmed `Create()` calls native/GPU prepare with `allowAllocation: true`.
- `git diff --check` passed on the touched fog/test files; only Git line-ending warnings were emitted.
- Targeted runtime compile still fails only on existing Candice SQLite dependency errors: missing `Mono.Data.Sqlite` and `SqliteDataReader` in `CandiceSQLiteProvider.cs`.

Residual risk:
- If an external MarineSnow/AbyssalFlow texture appears only after `Create()`, volumetric fog now uses the safe empty fallback until a true cold lifecycle prepares the handle. This is deliberate: no render-admission RTHandle allocation.

## 2026-05-27 - 13KRA Final Verification Boundary Update

What was wrong:
- Global `git diff --check` is not clean because `Docs/Tasks/CURRENT_BATCH.md` has unrelated trailing whitespace at lines 1490, 1593, 1619, and 1652.
- A separate `dotnet build Hecton8.slnx --no-restore -v:q /clp:ErrorsOnly /nr:false` is active with MSBuild child nodes and `VBCSCompiler`, so another compile launch would violate the build gate.

What was done:
- Rechecked `HectonVolumetricParticulateFogFeature.AddRenderPasses`; it still contains diagnostic maintenance and no-allocation bridge refresh, with no `TryPrepareNativeState`, `TryPrepareGpuState`, `RTHandles.Alloc`, `new RenderTexture`, `new GraphicsBuffer`, or `EnsureGenerationHandle`.
- Recorded the unrelated global whitespace failure and active external build in `Status_13KRA.md` / `Rationale_13KRA.md`.

Cinematic Cheats used:
- No new rendering cheat added in this verification-only step.

Exact Microseconds saved:
- 0 runtime us in this step. Process impact: avoided parallel build contention and avoided editing a cross-agent batch-control document outside 13KRA scope.

## 2026-05-27 - Bilateral DRS Hot Initialization Reaudit

What was wrong:
- `HectonBilateralDrsUpscalerRuntime.RunOwnerPreSimulation` and `RunOwnerVisualSync` could retry initialization after a fault.
- That initialization path allocated Vault buffers, CSV scratch, `GraphicsBuffer` constants, dispatcher bridge objects, and dump directory state.
- Bilateral DRS proof artifacts still used `Dump_SHINOBU_236.bin` / `[SHINOBU_236]`.
- `TryReadEditorTuning` used a mutable Vault resolve for a readback path.

What was done:
- Added `allowAllocation` to service preparation, Vault acquisition, CSV scratch acquisition, and constant-buffer acquisition.
- Cold routes pass `allowAllocation: true`: `OnEnable`, editor tuning/profile operations, and DataVault replacement.
- Hot routes pass `allowAllocation: false`: `PreSimulation` and `VisualSync` now resolve existing state only.
- Dump path is now `Docs/AgentLogs/Dump_13KRA.bin`; dev logs use `[13KRA]`.
- Editor tuning readback now uses `TryReadOnlyHandle`.
- Added static editor guard coverage for Bilateral DRS hot no-allocation behavior.

Cinematic Cheats used:
- Kept the existing Dear Lie edge-mask clear: compute clear when available, raster 1x1 black edge mask when compute is missing.
- Rejected adding a CPU/raster full upscaler without profiler proof.

Exact Microseconds saved:
- Prevented estimated 80-430 us repair spikes when invalid DRS state previously tried to allocate from hot phases.
- Steady-state frame cost unchanged.

Verification:
- Static scan confirmed hot `RunOwnerPreSimulation`/`RunOwnerVisualSync` blocks contain no `EnsureGenerationHandle`, `new GraphicsBuffer`, `TryRegisterHotSwapListener`, `EnsureBlackBoxDumpPathCold`, or `RegisterDispatcherRouteAllOrFail`.
- Static scan confirmed no `SHINOBU_236`, `Dump_SHINOBU_236.bin`, or `[SHINOBU_236]` remains in Bilateral DRS runtime/feature source.
- `git diff --check` passed on Bilateral DRS/test touched files; only Git line-ending warnings were emitted.
- Compile was not launched because an external `dotnet build Assembly-CSharp.csproj --no-restore -nologo -clp:ErrorsOnly -maxcpucount:1 /nr:false` is already running and CPU sampled at 80%.

## 2026-05-27 - UberNoir Runtime Bridge Hot Telemetry Reaudit

What was wrong:
- `HectonUberNoirRuntimeBridge.PushBlackBox` used telemetry setup from `LateFrameTick`.
- That setup could call `EnsureGenerationHandle` for `ShaderFeatureTelemetryRing` when the ring was missing or invalid.
- Fault dump capture also called the same setup path, so a NaN/layout fault could allocate before dumping.
- Dump ownership was split across four historical files: integrator/extinction `.bin` and `.h8dump` outputs.

What was done:
- Added `EnsureTelemetryBuffer(bool allowAllocation)`.
- Cold routes pass `allowAllocation: true`: `Awake`, `OnEnable`, and DataVault replacement.
- Hot routes pass `allowAllocation: false`: `PushBlackBox` and `DumpBlackBox`.
- Telemetry readback stays on `TryReadOnlyHandle`; owner writes use `TryAcquireWriteLock`.
- Dump output is now one proof file: `Docs/AgentLogs/Dump_13KRA.bin`.
- Added static editor guard coverage in `LightShaftRuntimeEditTests`.

Cinematic Cheats used:
- Kept shader-feature mask publication as a cheap global-state lie driven by continuous quality/stress values.
- Rejected physical noir/light integration and rejected new Ocean/Crest dependencies.
- Rejected multiple dump formats because proof ownership matters more than duplicate artifacts.

Exact Microseconds saved:
- Prevented estimated 20-80 us late-frame allocator spikes when the telemetry ring is missing or invalid.
- Removed three extra fault-time file writes; estimated 600-2000 us only during fault dump, not steady frame time.
- Steady-state frame cost unchanged.

Verification:
- Static scan confirmed `PushBlackBox` and `DumpBlackBox` call `EnsureTelemetryBuffer(allowAllocation: false)` and contain no `EnsureGenerationHandle`.
- Static scan confirmed cold `Awake`, `OnEnable`, and DataVault replacement call `EnsureTelemetryBuffer(allowAllocation: true)`.
- Static scan confirmed no old integrator/extinction dump names remain in `HectonUberNoirRuntimeBridge.cs`.
- `git diff --check` passed on `HectonUberNoirRuntimeBridge.cs` and `LightShaftRuntimeEditTests.cs`; only Git line-ending warnings were emitted.
- Compile was not launched because `VBCSCompiler.exe` PID 19312 is active and CPU sampled at 100%.

## 2026-05-27 - Global Shader Dispatcher Hot Allocation Reaudit

What was wrong:
- `GlobalShaderDispatcher.LateFrameTick` called command-buffer and shader-slot ensure paths.
- Those paths could allocate `CommandBuffer` or `ShaderGlobalState` if cold setup missed or DataVault was replaced.
- `RecordTelemetry` and `DumpTelemetry` reused the same allocation-capable shader-slot path.
- Editor readbacks used mutable slot views.
- Dump ownership used `Dump_CBUFFER_DISPATCH.bin` and a duplicate `.h8dump`.
- If Vault/slot lock was unavailable during fault dump, no dump file was written.

What was done:
- Added `TryEnsureCommandBuffer(bool allowAllocation)`.
- Added `allowAllocation` to `EnsureShaderGlobalSlotsRuntime` and static shader-slot preparation.
- Cold routes pass true: `Awake`, `OnEnable`, DataVault replacement, and editor tuning write.
- Hot routes pass false: `LateFrameTick`, `RecordTelemetry`, and `DumpTelemetry`.
- Editor readbacks now use `TryReadCachedShaderGlobalSlots` with `TryReadOnlyHandle`.
- Dump output is now `Docs/AgentLogs/Dump_13KRA.bin`.
- Fault dump now zero-clears a stack snapshot and writes `TelemetryFlagVaultUnavailable` when Vault is unavailable instead of silently returning.
- Added static editor guard coverage in `LightShaftRuntimeEditTests`.

Cinematic Cheats used:
- Kept global shader state as a cheap command-buffer/global-vector dispatch.
- Rejected physical fog/light integration and rejected Ocean/Crest producer changes.
- Fail-closed behavior uses existing cached/fallback globals instead of allocator repair.

Exact Microseconds saved:
- Prevented estimated 35-140 us allocator spikes when shader slots or command buffer are missing during late-frame dispatch.
- Removed one duplicate fault-time dump write; estimated 300-1000 us only during fault dump.
- Steady-state frame cost unchanged.

Verification:
- Static scan confirmed `LateFrameTick`, `RecordTelemetry`, and `DumpTelemetry` call shader-slot preparation with `allowAllocation: false`.
- Static scan confirmed hot blocks contain no `EnsureGenerationHandle`.
- Static scan confirmed no `Dump_CBUFFER_DISPATCH.bin`, `Dump_CBUFFER_DISPATCH.h8dump`, `DumpH8DumpFileName`, or `TryResolveCachedShaderGlobalSlots` remains in `GlobalShaderDispatcher.cs`.
- Static scan confirmed `DumpTelemetry` zero-clears `telemetrySnapshot` and writes `Dump_13KRA.bin` with `TelemetryFlagVaultUnavailable` on missing Vault/slot access.
- `git diff --check` passed on `GlobalShaderDispatcher.cs` and `LightShaftRuntimeEditTests.cs`; only Git line-ending warnings were emitted.
- Targeted runtime compile failed only on existing Candice SQLite dependency errors: missing `Mono.Data.Sqlite` and `SqliteDataReader` in `CandiceSQLiteProvider.cs`.
- Editor/test compile was not launched because an external `dotnet build .\Hecton8.slnx --no-restore -nologo -clp:ErrorsOnly -maxcpucount:1 /p:UseSharedCompilation=false /nr:false` with `csc.exe` is active and CPU sampled at 79%.

## 2026-05-27 - Shader Global Fallback State Reaudit

What was wrong:
- `GlobalShaderDispatcher.ExecuteGlobalDispatch` set `HectonShaderGlobalDataVaultBridge` active after a successful command-buffer publish.
- A later `LateFrameTick` could return early before command-buffer execution because command buffer, layout, Vault, or slot lock was unavailable.
- The bridge still believed the dispatcher was active, so `FlushFallbackVisualSync` skipped fallback shader-global publication.
- Underwater fog, caustic, biolum, physiology-noir, and pressure/death presentation globals could stay stale until a later producer publish happened to mark fallback dirty.

What was done:
- `GlobalShaderDispatcher.LateFrameTick` now marks the dispatcher inactive before no-alloc resolve work.
- `ExecuteGlobalDispatch` still marks the dispatcher active only after `Graphics.ExecuteCommandBuffer`.
- `HectonShaderGlobalDataVaultBridge.SetVisualSyncDispatcherActive(false)` now marks fallback shader globals dirty.
- Added static editor guard coverage in `LightShaftRuntimeEditTests`.

Cinematic Cheats used:
- Kept fallback as cheap global vector publication.
- Rejected physical fog/light simulation and rejected Ocean/Crest producer edits.
- Rejected immediate failure-path publication; fallback remains owned by the visual-sync bridge.

Exact Microseconds saved:
- No steady-state CPU saving; this is correctness under failure.
- Prevents multi-frame stale shader-state artifacts after a dispatch failure without allocator repair.
- Direct added cost is one static bool transition per late-frame attempt; no GC and no extra healthy-frame shader uploads.

Verification:
- Static scan confirmed `GlobalShaderDispatcher.LateFrameTick` calls `SetVisualSyncDispatcherActive(false)` before no-alloc resolve work.
- Static scan confirmed `ExecuteGlobalDispatch` calls `SetVisualSyncDispatcherActive(true)` only after command-buffer execution.
- Static scan confirmed `HectonShaderGlobalDataVaultBridge.SetVisualSyncDispatcherActive(false)` calls `MarkFallbackShaderGlobalsDirty()`.
- `git diff --check` passed on `GlobalShaderDispatcher.cs`, `HectonShaderGlobalDataVaultBridge.cs`, and `LightShaftRuntimeEditTests.cs`; only Git line-ending warnings were emitted.
- Compile was not launched because `VBCSCompiler.exe` PID 47372 is active; protocol forbids starting another `dotnet build` while a compiler server is running.

## 2026-05-27 - WaterOptics Telemetry Marker Render-Pass Reaudit

What was wrong:
- `HectonWaterOpticsTelemetryFeature` defaulted `enableCommandBufferMarker=true`.
- The pass only called `BeginSample/EndSample`.
- It forced `AllowPassCulling(false)` and attached active color as `ReadWrite`.
- This added a production RenderGraph pass with no player-visible water optics output and no black-box value.

What was done:
- Default marker toggle is now false.
- `AddRenderPasses` and `RecordRenderGraph` both use `IsTelemetryMarkerAllowed`.
- `IsTelemetryMarkerAllowed` allows the marker only in `UNITY_EDITOR || DEVELOPMENT_BUILD`.
- Release/player builds return false even if an old renderer asset serialized the toggle as true.
- Added static editor guard coverage in `LightShaftRuntimeEditTests`.

Cinematic Cheats used:
- Rejected profiler-marker scaffolding as a runtime "effect".
- Kept real water optics as shader/global-state presentation, not a diagnostic render pass.
- Rejected Ocean/Crest edits.

Exact Microseconds saved:
- Estimated 5-25 us per eligible camera by removing the forced marker pass and its color attachment dependency from production.
- No visual cost; the pass had no visual output.

Verification:
- Static scan confirmed default `enableCommandBufferMarker=false`.
- Static scan confirmed `RecordRenderGraph` and `AddRenderPasses` use `IsTelemetryMarkerAllowed`.
- Static scan confirmed release builds return false outside `UNITY_EDITOR || DEVELOPMENT_BUILD`.
- `git diff --check` passed on `HectonWaterOpticsTelemetryFeature.cs`, `LightShaftRuntimeEditTests.cs`, and 13KRA docs; only Git line-ending warnings were emitted.
- Compile was not launched because `dotnet.exe` PID 9648 and `VBCSCompiler.exe` PID 52544 are active and CPU sampled at 100%.

## 2026-05-27 - WaterOptics Installer Marker Contract Reaudit

What was wrong:
- Runtime WaterOptics marker was off-by-default and release-gated, but `WaterOpticsRendererFeatureInstaller` still repaired `settings.enableCommandBufferMarker` to true.
- Build verification also required the marker toggle to be true.
- That meant editor/build repair could keep reauthoring a non-visual marker pass as desired default state, contradicting the production-safe runtime contract.

What was done:
- `EnsureFeatureSettings` now writes `enableCommandBufferMarker=false`.
- `VerifyFeatureSettings` now requires `!markerProperty.boolValue`.
- Build failure wording now describes development-safe settings instead of an opaque marker lane.
- Static editor coverage now asserts installer repair does not write the marker true.

Cinematic Cheats used:
- Kept the feature reference as a diagnostic hook, but rejected diagnostic marker work as default presentation.
- Kept real underwater optics work in shader/global-state presentation routes.
- Rejected Ocean/Crest edits and rejected hand-editing renderer assets without a source guard.

Exact Microseconds saved:
- Preserves the previous estimated 5-25 us per eligible camera by preventing repair tools from re-enabling the marker pass.
- No visual loss; the marker pass has no player-visible output.

Verification:
- Static scan confirmed `VerifyFeatureSettings` contains `!markerProperty.boolValue`.
- Static scan confirmed `EnsureFeatureSettings` writes `enableCommandBufferMarker` false.
- Static scan confirmed tests reject `EnsureBool(...enableCommandBufferMarker..., true)`.
- Static scan confirmed `Assets/_Project/Data` has no serialized `enableCommandBufferMarker` toggle.
- `git diff --check` passed on `WaterOpticsRendererFeatureInstaller.cs` and `LightShaftRuntimeEditTests.cs`; only Git line-ending warnings were emitted.
- Targeted runtime compile failed only on existing Candice SQLite dependency errors: missing `Mono.Data.Sqlite` and `SqliteDataReader` in `CandiceSQLiteProvider.cs`.
- Editor/test compile was not launched because CPU sampled at 63% after the runtime compile; protocol forbids starting another `dotnet build` while CPU is above 50%.

## 2026-05-27 - UberPost Internal Waterline Transition Reaudit

What was wrong:
- `HectonVisorUberPostFeature.ResolveInternalWaterlineParams` used a hard `cameraY < waterlineY - 0.03f` threshold.
- Crossing that threshold jumped the internal waterline split to a full-screen water mask.
- This is a binary underwater/flood transition inside the noir post stack, not a continuous presentation budget.

What was done:
- Added `InternalWaterlineFullScreenSplit`, `InternalWaterlineSubmergeOffsetMeters`, and `InternalWaterlineSubmergeFadeMeters`.
- Added `ResolveInternalWaterlineSubmergedWeight01` using `Smooth01`.
- `ResolveInternalWaterlineParams` now lerps the viewport split into full-screen mask by the continuous submerge weight.
- Added static editor coverage rejecting the old hard threshold.

Cinematic Cheats used:
- Used one scalar smoothstep and one lerp instead of physical water interface simulation.
- Kept Ocean/Crest water ownership untouched.
- Kept the effect inside the existing single fullscreen UberPost pass.

Exact Microseconds saved:
- 0 us saved; this is visual correctness.
- Added scalar cost is below measurement noise; no allocations, no new render target, no new pass.

Verification:
- Static scan confirmed `ResolveInternalWaterlineParams` calls `ResolveInternalWaterlineSubmergedWeight01`.
- Static scan confirmed split line uses `math.lerp(viewportSplit, InternalWaterlineFullScreenSplit, submerged01)`.
- Static scan confirmed no `cameraY < waterlineY - 0.03f` remains in `HectonVisorUberPostFeature.cs`.
- `git diff --check` passed on `HectonVisorUberPostFeature.cs` and `LightShaftRuntimeEditTests.cs`; only Git line-ending warnings were emitted.
- Compile was not launched for this pass because external `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` PID 62864 and `VBCSCompiler.exe` PID 6448 are active and CPU sampled at 60%.

## 2026-05-27 - Volumetric Fog Dear Lie Shader Target Reaudit

What was wrong:
- `Hecton_VolumetricFog_DearLie.shader` is the cheap raster fallback for low/no-compute particulate fog.
- Both passes still used `#pragma target 4.5`.
- Unity documents target 4.5 as ES3.1/SM5-class capability, including compute/random-write feature level. That is wrong for a fullscreen fallback that should survive below high-resource compute.
- `HectonVolumetricParticulateFogFeature.cs` still had stale `SHINOBU_233` cold-allocation owner comments.

What was done:
- Lowered `DearLieProxy` pass to `#pragma target 3.5`.
- Lowered `BilateralComposite` pass to `#pragma target 3.5`.
- Removed stale `SHINOBU_233` comments from the volumetric fog feature.
- Added static editor guard coverage: proxy shader must contain target 3.5, must not contain target 4.5, and the feature source must not contain `SHINOBU_233`.

Cinematic Cheats used:
- Kept the fallback as one fullscreen depth-aware fog lie.
- Rejected compute-class shader requirements for the cheap proxy path.
- Kept Ocean/Crest and gameplay truth untouched.

Exact Microseconds saved:
- 0 us direct frame saving.
- The measurable win is platform survival: weak/mobile/low-tier devices get the fog fallback instead of losing it due shader target requirements.

Verification:
- Static scan confirmed `Hecton_VolumetricFog_DearLie.shader` has two `#pragma target 3.5` entries and no `#pragma target 4.5`.
- Static scan confirmed no `SHINOBU_233` remains in `HectonVolumetricParticulateFogFeature.cs`.
- `git diff --check` passed on `Hecton_VolumetricFog_DearLie.shader`, `HectonVolumetricParticulateFogFeature.cs`, and `LightShaftRuntimeEditTests.cs`; only Git line-ending warnings were emitted.
- Targeted runtime compile failed only on existing Candice SQLite dependency errors: missing `Mono.Data.Sqlite` and `SqliteDataReader` in `CandiceSQLiteProvider.cs`. No 13KRA source file appeared in the error list.
- Editor/test compile was not launched because CPU sampled at 86.9% after the runtime compile; protocol forbids starting another `dotnet build` while CPU is above 50%.

## 2026-05-27 - Deferred Caustics Shader Target Reaudit

What was wrong:
- `Hecton_DeferredCaustics.shader` is a cheap fullscreen caustics composite.
- It used `#pragma target 4.5` despite having no compute kernel, `StructuredBuffer`, RW texture, RW structured buffer, or byte-address buffer.
- That makes the shallow/depth caustic beauty layer depend on a compute-class shader target for no visual benefit.

What was done:
- Lowered the deferred caustics shader target to `#pragma target 3.5`.
- Added `DeferredCausticsFullscreenProxyUsesLowTierShaderTarget` static editor coverage.
- The test rejects target 4.5 and rejects StructuredBuffer/RW/ByteAddress dependencies in this proxy.

Cinematic Cheats used:
- Kept caustics as procedural depth/fullscreen math instead of physical photon simulation.
- Kept SDF cavern occlusion as bounded optional visual sampling behind quality weight.
- Rejected Ocean/Crest swell authority edits.

Exact Microseconds saved:
- 0 us direct frame saving.
- The measurable win is platform survival: low-tier raster hardware keeps the caustic layer instead of losing it to an unnecessary SM5/ES3.1-class target.

Verification:
- Static scan confirmed `Hecton_DeferredCaustics.shader` contains `#pragma target 3.5`.
- Static scan confirmed no `#pragma target 4.5`, `StructuredBuffer`, `RWTexture`, `RWStructuredBuffer`, or `ByteAddressBuffer` remains in that shader.
- `git diff --check` passed on `Hecton_DeferredCaustics.shader` and `LightShaftRuntimeEditTests.cs`; only Git line-ending warnings were emitted.
- Compile was not launched for this pass: no compiler process was visible, but CPU sampled at 69.9%, above the 50% build gate.

## 2026-05-27 - Abyssal Caustics Proof Owner Reaudit

What was wrong:
- `AbyssalDeferredCausticsRuntime` already writes the 13KRA dump route.
- `AbyssalCausticsContracts.cs` still described caustic Vault lanes as `SHINOBU-owned`.
- `AbyssalCausticsLayoutAudit.cs` still printed `SHINOBU_232` in editor layout audit messages.

What was done:
- Replaced stale caustics contract owner comments with `13KRA-owned`.
- Replaced editor layout audit message prefix with `13KRA`.
- Added static editor coverage rejecting `SHINOBU-owned` and `SHINOBU_232` in this caustics proof route.

Cinematic Cheats used:
- No visual algorithm change.
- Kept caustics as procedural/depth presentation and kept Ocean/Crest input producers untouched.

Exact Microseconds saved:
- 0 us runtime.
- The improvement is proof correctness: caustic lane comments, editor layout audit, and runtime dump ownership now agree.

Verification:
- Static scan confirmed no `SHINOBU_232` or `SHINOBU-owned` remains in `Assets/_Project/Scripts/Rendering/AbyssalCaustics`.
- `git diff --check` passed on the caustics contracts, caustics layout audit, deferred caustics shader, status/rationale/log, and `LightShaftRuntimeEditTests.cs`; only Git line-ending warnings were emitted.
- Targeted runtime compile failed only on existing Candice SQLite dependency errors: missing `Mono.Data` and `SqliteDataReader` in `CandiceSQLiteProvider.cs`. No 13KRA source file appeared in the error list.
- Editor/test compile was not launched after that runtime compile because CPU sampled at 69.3%, above the 50% build gate.

## 2026-05-27 - DRS Survival Hard-Cull API Reaudit

What was wrong:
- `HectonDrsRenderFeatureGate` exposed `ShouldCullForSurvivalScale()` even though underwater consumers now use continuous survival pressure/visual weight.
- The helper was unused, but it preserved a binary hard-cull route for future fog, particle, SSDO, or shaft code.
- The static test still asserted the hard-cull threshold, so the guard was enforcing the wrong doctrine.

What was done:
- Removed `ShouldCullForSurvivalScale()`.
- Kept `ResolveSurvivalPressure01()` and `ResolveSurvivalVisualWeight01()` as the only shared gate outputs.
- Updated the editor static guard to reject `ShouldCullForSurvivalScale(` and `>= 0.999f` in the gate.

Cinematic Cheats used:
- Kept underwater presentation as a continuous scalar degradation.
- Rejected blanking entire effects under survival DRS pressure.
- Kept Ocean/Crest and DRS ownership untouched.

Exact Microseconds saved:
- 0 us immediate runtime; the removed helper was not called.
- Prevents future regressions where survival frames buy performance by deleting underwater fog/particles/shafts instead of scaling them.

Verification:
- Static scan confirmed `HectonDrsRenderFeatureGate.cs` and current underwater presentation consumers no longer contain `ShouldCullForSurvivalScale`.
- Static scan confirmed consumers still call `ResolveSurvivalPressure01()` or `ResolveSurvivalVisualWeight01()`.
- `git diff --check` passed on `HectonDrsRenderFeatureGate.cs`, `LightShaftRuntimeEditTests.cs`, and 13KRA docs; only Git line-ending warnings were emitted.
- Compile was not launched: `VBCSCompiler.exe` PID 20352 is active and CPU sampled at 88.5%.

## 2026-05-27 - Bilateral DRS Black-Box Boundary Correction

What was wrong:
- A first pass incorrectly treated `Docs/AgentLogs/Dump_1335_BilateralDrs.bin` as a 13KRA stale route.
- Domain/status cross-check showed Bilateral DRS/upscaler has an active 1335 proof route, while 13KRA owns underwater presentation effects and their quality behavior.

What was done:
- Reverted the Bilateral DRS dump path to `Docs/AgentLogs/Dump_1335_BilateralDrs.bin`.
- Removed the 13KRA static assertion that claimed the Bilateral DRS dump must be `Dump_13KRA.bin`.
- Kept the guard rejecting the old `Dump_SHINOBU_236.bin` owner.

Cinematic Cheats used:
- None. This is proof routing only; visual/quality behavior is unchanged.

Exact Microseconds saved:
- 0 us runtime. The fix prevents cross-agent proof-route sabotage, not frame cost.

## 2026-05-27 - Abyssal Lighting Editor Owner Tag Reaudit

What was wrong:
- `AbyssalLightingTunerWindow` logged loaded-scene Unity probe group diagnostics with `[SHINOBU_131]`.
- That is editor-only, but it still poisons lighting proof attribution.

What was done:
- Changed the log tag to `[13KRA]`.
- Added editor static coverage rejecting `[SHINOBU_131]`.

Cinematic Cheats used:
- None. This is diagnostic ownership only.

Exact Microseconds saved:
- 0 us runtime.

## 2026-05-27 - Dynamic Point-Light Contract Owner Reaudit

What was wrong:
- `DynamicPointLightCullingContracts.cs` still named `SHINOBU_151` as owner/assignment source for dynamic point-light Vault IDs and the 32-byte culling result record.
- Runtime dumps and 13KRA status already route this lighting lane through the current owner, so the contract proof text was stale.

What was done:
- Replaced the stale owner comments with 13KRA proof text.
- Left all numeric `BufferID` casts and explicit DTO field offsets untouched.
- Added editor static coverage rejecting `SHINOBU_151` in the contract file.

Cinematic Cheats used:
- None. This is proof attribution only; dynamic light culling math and visual budgets are unchanged.

Exact Microseconds saved:
- 0 us runtime.
- The gain is audit correctness: dynamic lighting contract evidence now points to the active 13KRA owner.

Verification:
- Static scan found no `SHINOBU_151` in `DynamicPointLightCullingContracts.cs`; only the editor guard contains the rejected token.
- Broader 13KRA source scan found no stale `SHINOBU_`, `Dump_SHINOBU`, `LIGHT_DIRECTOR`, `ABYSSAL_LIGHTING_TECH`, `LIGHTING_SURGEON`, `DRS_SURGEON`, or `SURGEON` tokens in the audited lighting/caustics/water-optics source paths.
- `git diff --check` passed on the contract file, editor tests, status, rationale, and log; only Git line-ending warning for the contract file was emitted.
- Compile was not launched: `dotnet.exe` PID 24312 and `VBCSCompiler.exe` PID 50784 are active, so the build gate forbids another `dotnet build`.

## 2026-05-27 - GI Storage Fail-Closed Reaudit

What was wrong:
- `HectonGIRelaySystem.AcquireBuffer` threw `InvalidOperationException` on failed DataVault native-buffer acquisition.
- `InteriorGIProbeVolumeRuntime.AcquireBuffer` and `ResolveArray` could also abort when the Vault was unavailable or invalid.
- GI relay readiness did not explicitly prove all SH/day-night buffers before profile initialization and `_nativeStorageReady`.

What was done:
- GI relay acquisition now returns default handles on Vault acquisition failure.
- GI relay storage setup now requires `HasRequiredGIRelayStorage()` and `EnsureDayNightRelayNativeStorage()` before SH profile writes.
- Day/night relay storage now validates all required buffers through `HasRequiredDayNightRelayStorage()` before tuning/profile initialization.
- Interior GI acquisition/resolve now returns default handles/arrays instead of throwing; existing `HasRequiredNativeBuffers()` handles the fail-closed state.
- Added static editor guards rejecting the old exception strings and checking the readiness gates.

Cinematic Cheats used:
- No new simulation.
- Missing GI storage now degrades to no/fallback lighting instead of runtime abort.
- Existing continuous quality/cadence/profile paths remain untouched.

Exact Microseconds saved:
- 35-140 us avoided on faulted storage frames by removing exception construction/unwind.
- 0 us steady-state saving; this is a failure-mode correction, not a hot-path optimization.

Verification:
- Static scan confirmed no runtime source in the GI relay/day-night/interior GI storage paths contains the old acquisition exception strings or `throw new InvalidOperationException`.
- Editor static guards were added in `LightShaftRuntimeEditTests.cs`.
- `git diff --check` passed on tracked GI relay, day/night relay, and Interior GI files with only Git line-ending warnings; direct trailing-whitespace scan passed on the untracked editor guard and 13KRA log files.
- Compile was not launched: latest gate check found `dotnet.exe` PIDs 53916 and 67740 active and CPU sampled at 99%, so the build gate forbids another `dotnet build`.
