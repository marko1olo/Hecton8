# LOG_EXTINCTION_LUT_SAMPLER

## Extinction LUT Sampler Report
What was wrong:
- Beer-Lambert LUT existed on disk, but there was no global runtime loader or shader sampling path.
- Underwater/fog color was still partly driven by RenderSettings.fogColor writes, which fights shader/post-process extinction.
- Low-end and high-end paths were not separated: uniform water color gave no red-light extinction depth cue.

What was done:
- Added `Assets/_Project/Art/Shaders/Hecton_WaterExtinction.hlsl` as the shared packed-LUT sampling include.
- Integrated extinction into `Hecton8_UberNoir.hlsl`: LOW vertex sample, non-LOW per-pixel sample, albedo extinction, fog tint, and IGN stabilization.
- Integrated post stack hooks in `Hecton_NoirDepthFog.shader` and `Hecton_ScooterVolumetricShafts.shader`.
- Added cold `LutArrayResolver` in `Assets/_Project/Scripts/Rendering/` to load `Data/Visuals/Water_Extinction_Matrix.bin` as packed 4096x4096 R16F, bind `_ExtinctionLUT` globally, and fall back to ARGB32 if half sampling is unavailable.
- Published `_ExtinctionLUTRuntime` from underwater visuals and `_ExtinctionLUTWeatherParams` from weather intensity without inventing a new signal type.
- Removed runtime `RenderSettings.fogColor` assignments from underwater/celestial visual color authority while keeping lifecycle restore/read paths.

Cinematic cheats used:
- Packed 2D LUT instead of physical spectral light transport.
- Vertex-only LUT sample on `_MATH_LOD_LOW`.
- Per-pixel extinction only where visual payoff is high.
- IGN fog dither without adding a noise texture dependency.
- Branchless emissive mask exemption via `lerp()`.

Verification:
- LUT byte count verified: 33,554,432 bytes.
- Static shader brace check passed for `Hecton_WaterExtinction.hlsl`, `Hecton8_UberNoir.hlsl`, `Hecton_NoirDepthFog.shader`, and `Hecton_ScooterVolumetricShafts.shader`.
- `_MATH_LOD_LOW` pragma exists in `Assets/_Project/Art/Shaders/Core/Hecton8_UberNoir.shader`; include uses compile-time LOW gates.
- Emissive exemption scan found mask + `lerp()` and no emissive `if` branch.
- Targeted Roslyn compile of `Assets/_Project/Scripts/Rendering/LutArrayResolver.cs` exits 0 after replacing obsolete `FormatUsage` calls with `GraphicsFormatUsage`.
- `git diff --check` returned exit 0; only CRLF normalization warnings.
- `dotnet build Hecton8.Core.csproj --no-restore` failed with 105 existing cross-domain errors. Full dump: `Docs/AgentLogs/Dump_EXTINCTION_LUT_SAMPLER_Build.txt`. No build-log hits for the extinction files or touched shader/C# integration files.

Exact microseconds saved:
- Exact measured profiler savings: unavailable; project compile is blocked, so no Unity profiler run was possible.
- Engineering estimates recorded for review: LOW vertex path saves 40-140 us/frame versus per-pixel material LUT at 1080p; packed LUT fake saves 80-250 us/frame versus an 8-step raymarch; global binding saves 5-40 us/frame in material-heavy scenes; IGN procedural dither saves 10-35 us/frame versus a new texture sample. These are estimates, not measured data.

Final state:
- Core EXTINCTION_LUT_SAMPLER tasks are implemented.
- Final validation is dependency-blocked by the existing compile wall, not by the extinction implementation.

## 2026-05-16 Second-Pass Multiplatform / Data Sovereignty Report
What was wrong:
- The first loader still carried startup IO risk: a 32 MiB managed full-file staging pattern was too blunt for Steam Deck MicroSD and Android heap pressure.
- Extinction runtime/weather globals had more than one producer surface.
- The compile wall hid real integration blockers: missing TetherFiredSignal, deleted ModuloSimulationBucketer service, generic inference/API drift, and duplicate helper methods in unrelated files.

What was done:
- Reworked `LutArrayResolver` to stream the LUT through a 128 KiB staging buffer into `GetRawTextureData<byte>()`.
- Routed extinction runtime and weather state through `HectonShaderGlobalDataVaultBridge` DataVault-backed slots.
- Added DataVault-resolved `ModuloSimulationBucketer` to replace the deleted bucketer compile wall without restoring private persistent NativeArray fields.
- Fixed small compile blockers encountered while advancing validation: Tether signal aliasing, VaultProbe generic inference, XR refresh request API guard, duplicate GlobalDataVault ABI validator, and duplicate VehicleDocking helper methods.
- Re-ran shader audits: extinction shader path uses packed `TEXTURE2D` + `LOAD_TEXTURE2D`; no compute thread groups, RWTexture, groupshared memory, or D3D-only syntax.

Cinematic cheats used:
- Beer-Lambert remains a packed 2D LUT, not a volumetric transport simulation.
- LOW remains vertex-sampled; high tier keeps per-pixel material/post/shaft tint.
- Fog banding uses procedural IGN rather than a new sampled noise atlas dependency.

Exact microseconds saved:
- Exact profiler measurements remain unavailable because the project does not compile.
- IO pressure improvement: managed cold staging reduced from 33,554,432 bytes to 131,072 bytes.
- Estimated runtime savings remain unchanged from the first report: LOW vertex path 40-140 us/frame versus per-pixel object sampling, packed LUT fake 80-250 us/frame versus an 8-step raymarch, global binding 5-40 us/frame in material-heavy scenes.

Current validation:
- LUT byte count verified at 33,554,432 bytes.
- Shader brace checks pass: WaterExtinction 12/12, UberNoir 56/56, NoirDepthFog 13/13, ScooterVolumetricShafts 89/89.
- `git diff --check` passed on the extinction/audit touched set.
- `dotnet build Hecton8.Core.csproj` still fails. Current dump has 185 pre-summary errors led by `LockstepStateValidator`, `SubmarineFluidDynamics`, `EcosystemDirector`, and `SargassumMicroFaunaBoids`.
- I am not marking VERIFIED MASTER GRADE. That would be false.

## 2026-05-16 Third-Pass DataVault / ARM Layout Polish
What was wrong:
- `LutArrayResolver` still directly wrote extinction vector globals after the bridge existed, leaving duplicate authority for `_ExtinctionLUTParams`, `_ExtinctionLUTRuntime`, and `_ExtinctionLUTWeatherParams`.
- `HectonUnderwaterVisuals` still owned six Persistent NativeArrays for biome fog transition blending.
- The biome fog Burst payload structs did not declare explicit packed layout, which is unacceptable for ARM/Quest layout stability.

What was done:
- Added a DataVault-backed `_ExtinctionLUTParams` slot to `HectonShaderGlobalDataVaultBridge`.
- Routed `LutArrayResolver` success/fallback vector publishes through the bridge; the resolver now binds only `_ExtinctionLUT` directly.
- Added dedicated `BufferID` values for underwater biome fog sample/source/AUP/result buffers.
- Replaced the private biome fog Persistent NativeArrays with `VaultBufferHandle<T>` fields and vault-resolved local views at schedule/commit time.
- Added `[StructLayout(LayoutKind.Sequential, Pack = 1)]` to `BiomeTransitionSample`, `BiomeTransitionFogSource`, and `BiomeTransitionFogResult`.

Cinematic cheats used:
- Kept the fog transition as a one-lane Burst blend over preauthored color/turbidity data, not a water simulation.
- Kept Beer-Lambert as the packed LUT path; no new raymarch or compute dependency was added for LOW.

Exact microseconds saved:
- Exact measured profiler delta: unavailable; Unity/player validation is blocked by the current compile wall.
- Runtime estimate for the DataVault migration: 0 us intended visual-cost change; this is ownership/stability debt removal.
- Previous rendering estimates still stand as estimates, not measurements: LOW vertex path 40-140 us/frame versus per-pixel material LUT; packed LUT fake 80-250 us/frame versus an 8-step raymarch.

Current validation:
- `rg` finds no `new NativeArray`/`new NativeList` ownership in the scoped rendering files; remaining NativeArray mentions are texture/readback views, vault-resolved local views, or Burst job parameters.
- `rg` finds no `string.Format`, `Update`, `LateUpdate`, `FixedUpdate`, `File.ReadAllBytes`, or `EventBus` use in `LutArrayResolver`, `HectonShaderGlobalDataVaultBridge`, or the biome fog job path. The only `Action<>` hit is a static cached AsyncGPUReadback callback.
- `git diff --check` passed for `HectonUnderwaterVisuals`, `BiomeTransitionFogBlendJobs`, and `H8Memory` with CRLF warnings only.
- Latest `dotnet build Hecton8.Core.csproj` fails with 23 errors in external active-work files: `EcosystemRuntimeInstaller.cs` references missing `Hecton8.AI.Ecosystem`, and `SubmarineFluidDynamics.cs` references missing `VaultNativeBuffer<>`. The latest dump contains no errors in `HectonUnderwaterVisuals`, `LutArrayResolver`, `HectonShaderGlobalDataVaultBridge`, `BiomeTransitionFogBlendJobs`, or `H8Memory`.
- I am still not marking VERIFIED MASTER GRADE. The latest disk build dump is not green.

## 2026-05-16 Fourth-Pass Android URI / Compile Revalidation Report
What was wrong:
- `LutArrayResolver` skipped URL-style `Application.streamingAssetsPath` values. Android/Quest APK StreamingAssets can resolve through `jar:`/URI access, so the LUT path was not universal unless a persistent copy already existed.
- The status and rationale still recorded an external compile wall after parallel work changed the disk state.

What was done:
- Added cold URL-style StreamingAssets staging in `LutArrayResolver`.
- The staging path builds the URI safely without `Path.Combine` on a URL, downloads to `Application.temporaryCachePath/Hecton8/WaterExtinction/Water_Extinction_Matrix.bin` through `DownloadHandlerFile`, validates the exact 33,554,432 byte count, then reuses the existing 128 KiB scratch-buffer upload into `Texture2D.GetRawTextureData<byte>()`.
- Kept filesystem StreamingAssets, persistentDataPath, and project data fallback paths intact.
- Re-ran scoped platform scans and C# compile validation.

Cinematic cheats used:
- Beer-Lambert remains a 4096x4096 packed R16/ARGB32 fallback LUT.
- LOW remains vertex-sampled; high tier remains per-pixel material/post/shaft tint.
- No raymarch, Texture3D-only path, compute prefilter, or new atlas dependency was added.

Exact microseconds saved:
- Hot-path measured delta: 0 us by construction; the new Android path runs only during cold bootstrap.
- Managed cold staging remains 131,072 bytes for the matrix upload path, not 33,554,432 bytes.
- Android first boot now pays a cold APK-to-cache file copy; exact milliseconds are unmeasured in this shell.
- Existing estimates remain estimates, not profiler measurements: LOW vertex path 40-140 us/frame versus per-pixel object LUT; packed LUT fake 80-250 us/frame versus an 8-step raymarch; global binding 5-40 us/frame in material-heavy scenes.

Current validation:
- LUT byte count verified at 33,554,432 bytes.
- `rg` finds no `File.ReadAllBytes`, `downloadHandler.data`, `UnityWebRequest.Get`, `StartCoroutine`, `async`, or `Update()` in `LutArrayResolver.cs`.
- Scoped `git diff --check` passed with CRLF warnings only.
- Struct layout scan confirms `[StructLayout(LayoutKind.Sequential, Pack = 1)]` on the biome transition payload structs.
- Latest `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly` exits 0 with 0 warnings and 0 errors. Dump: `Docs/AgentLogs/Dump_EXTINCTION_LUT_SAMPLER_Build.txt`.
- Unity shader import, Android device APK staging, RenderDoc, GCMonitor, and player build validation remain pending. I am not claiming those from a dotnet compile.

## 2026-05-16 Fifth-Pass Shader Binding / Platform Sweep
What was wrong:
- `Hecton_WaterExtinction.hlsl` still declared an unused `sampler_ExtinctionLUT` even though the extinction LUT is read through integer `LOAD_TEXTURE2D`.

What was done:
- Removed the unused sampler declaration from the extinction include.
- Ran a domain-wide `numthreads` sweep under `Assets/_Project/Art/Shaders`; maximum product found was 512, below the 1024 Metal/Quest limit.
- Re-ran brace counts on the extinction shader consumers.

Cinematic cheats used:
- No visual-model change. Beer-Lambert remains a packed texture fake; LOW remains vertex-sampled and High remains per-pixel/post/shaft-tinted.

Exact microseconds saved:
- Runtime measured delta: 0 us expected and unmeasured; this is binding/platform hygiene.
- Potential backend benefit: one unnecessary sampler binding surface removed from the extinction path.

Current validation:
- `rg` finds no `sampler_ExtinctionLUT` declaration; only `LOAD_TEXTURE2D(_ExtinctionLUT, int2(texel))` remains.
- Brace counts: `Hecton_WaterExtinction.hlsl` 12/12, `Hecton8_UberNoir.hlsl` 61/61, `Hecton_NoirDepthFog.shader` 13/13, `Hecton_ScooterVolumetricShafts.shader` 89/89.
- Scoped `git diff --check` passed with CRLF warnings only.
- Latest dotnet build still exits 0 with 0 warnings and 0 errors. Dump: `Docs/AgentLogs/Dump_EXTINCTION_LUT_SAMPLER_Build.txt`.

## 2026-05-16 Sixth-Pass DataVault / Blackbox Polish
What was wrong:
- `HectonUberNoirRuntimeBridge` still owned direct shader-global writes for `_HectonUberNoirRuntimeParams` and `_HectonActiveShaderFeatureMask`.
- The shader-runtime blackbox path dumped only `Dump_UBER_NOIR_INTEGRATOR.bin`; this prompt required an EXTINCTION-named dump artifact on fault.

What was done:
- Added UberNoir runtime and feature-mask slots to `HectonShaderGlobalDataVaultBridge`.
- Routed dirty-flagged UberNoir runtime uploads through `HectonShaderGlobalDataVaultBridge.PublishUberNoirRuntime(...)`.
- Removed the direct shader property IDs from `HectonUberNoirRuntimeBridge`.
- Preserved the existing 300-frame Pack=1 telemetry ring and mirrored fault dumps to both `Dump_UBER_NOIR_INTEGRATOR.bin` and `Dump_EXTINCTION_LUT_SAMPLER.bin`.
- Re-ran C# compile after a concurrent external `ArchitectEyeVisualizer` compile wall cleared.

Cinematic cheats used:
- No new simulation. Beer-Lambert remains a packed LUT fake; the high-tier visual feature mask remains the gate for POM, caustics, refraction, wake silt, hull dents, and overkill diagnostics.

Exact microseconds saved:
- Exact profiler measurements remain unavailable; no Unity player/profiler run was executed.
- Expected runtime delta for the bridge reroute: 0 us measured, with dirty-flag gating unchanged.
- Fault dump mirror cost: 0 us in normal frames; file I/O occurs only after layout/non-finite/vault faults.

Current validation:
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly` exits 0 with 0 warnings and 0 errors. Dump: `Docs/AgentLogs/Dump_EXTINCTION_LUT_SAMPLER_Build.txt`.
- `rg` finds no direct UberNoir runtime shader-global property IDs or direct UberNoir `Shader.SetGlobal*` calls in `HectonUberNoirRuntimeBridge.cs`; the remaining UberNoir write is centralized in `HectonShaderGlobalDataVaultBridge.cs`.
- `rg` finds no `sampler_ExtinctionLUT`, `File.ReadAllBytes`, `downloadHandler.data`, `UnityWebRequest.Get`, or emissive `if` branch in the scoped extinction files.
- Unity shader import, Android/Quest device staging, RenderDoc, GCMonitor, and player build validation remain pending.

## 2026-05-16 Seventh-Pass Analytical Fallback / Single Bind Audit
What was wrong:
- The low-memory/mobile analytical fallback was selected by the C# resolver, but shader consumers did not consistently use an analytical extinction path when `_ExtinctionLUTRuntime.x` disabled the LUT. Post fog and scooter shafts could collapse to white/no-op extinction instead of the intended Dear-Lie Beer-Lambert look.
- `LutArrayResolver` still bound `_ExtinctionLUT` on the disabled path through `Texture2D.blackTexture`, which weakened the "texture bound globally once" rule.
- The status files overclaimed a current green C# compile after parallel non-rendering edits changed the disk state.

What was done:
- Added shared analytical Beer-Lambert resolve helpers in `Hecton_WaterExtinction.hlsl`, with finite depth clamps, turbidity floor, `exp2` attenuation, and an explicit inactive early return before any LUT sample call.
- Routed UberNoir, NoirDepthFog, and ScooterVolumetricShafts through `H8WaterExtinctionResolveRgbByWorld` / `H8WaterExtinctionResolveRgbByDepthMeters`.
- Changed the disabled-path resolver publish to `PublishWaterExtinctionAnalyticalFallback()` and removed the black-texture `_ExtinctionLUT` bind. The only remaining `_ExtinctionLUT` bind is the real loaded texture path in `LutArrayResolver`.
- Re-ran scoped shader brace checks, binding scans, debt scans, git whitespace checks, domain shader thread-group audit, and dotnet compile.

Cinematic cheats used:
- Toaster/mobile mode now uses an ALU Beer-Lambert fake instead of uploading or sampling the 32 MiB LUT under pressure.
- High tier keeps the packed LUT path for per-pixel material color, post fog, and scooter volumetric shafts.
- No raymarch, no Texture3D-only dependency, no compute prefilter, no sampled `lerp` fallback, and no emissive branch were added.

Exact microseconds saved:
- Exact profiler measurements remain unavailable; no Unity player/profiler run was executed.
- Expected fallback hot-path change: texture bandwidth is removed when LUT inactive; cost is a small ALU block and `exp2` per sampled point.
- Expected cold-memory change: fallback mode avoids the 33,554,432 byte LUT upload path entirely; normal LUT upload still uses the 131,072 byte scratch staging path.
- Prior microsecond values remain estimates, not measurements: LOW vertex sampling saves 40-140 us/frame versus broad per-pixel object LUT sampling; packed LUT fake saves 80-250 us/frame versus an 8-step raymarch.

Current validation:
- Brace counts pass: `Hecton_WaterExtinction.hlsl` 20/20, `Hecton8_UberNoir.hlsl` 63/63, `Hecton_NoirDepthFog.shader` 13/13, `Hecton_ScooterVolumetricShafts.shader` 89/89.
- `_ExtinctionLUT` bind scan shows one real `Shader.SetGlobalTexture(_ExtinctionLutId, _extinctionTexture)` call, no `Texture2D.blackTexture` fallback bind, and no `lerp(analytical, sampledLut, active)` resolve path.
- Scoped debt scan finds no `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, `File.ReadAllBytes`, `downloadHandler.data`, `UnityWebRequest.Get`, `StartCoroutine`, local `new NativeArray`, local `new NativeList`, `EventBus`, `Action<>`, `Func<>`, `sampler_ExtinctionLUT`, or emissive `if` branch in the scoped extinction files.
- Domain shader thread-group audit found no literal `numthreads` product above 1024 in `Assets/_Project/Art/Shaders`.
- `git diff --check` passes on scoped files with CRLF warnings only.
- Latest `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly` fails with 12 external non-rendering errors in `GameBootstrapper`, `PlayerTool`, `PlayerToolManager`, `PlayerNoiseEmitter`, `FluidFeedbackListener`, and `GlobalSignals`. Dump: `Docs/AgentLogs/Dump_EXTINCTION_LUT_SAMPLER_Build.txt`.
- I am not marking VERIFIED MASTER GRADE. The current disk build is blocked by external errors and Unity runtime/profiler/player validation remains pending.

## 2026-05-16 Eighth-Pass Compile-Seam Repair / Current Validation
What was wrong:
- The prior build dump was stale. After parallel work, the old 12-error wall cleared, but the current build exposed 23 active errors.
- `DiegeticGyroCompassRuntime` had stale references to deleted private `_lastActualAup`, `_hasLastActualAup`, and `_blackBoxCursor` fields even though compass state now lives in vault-owned `CompassStateDTO`.
- `EcosystemDirector` passed vault wrapper structs into generic unsafe pointer/upload APIs; C# 9 does not use implicit conversions for generic type inference there.

What was done:
- Patched `DiegeticGyroCompassRuntime` so velocity history uses `CompassStateDTO.PreviousActualAUP` plus `FlagHasPreviousAup`.
- Patched compass blackbox writes/dumps to use `CompassStateDTO.BlackBoxCursor`.
- Patched high-tier compass failure VFX to pass the current `CompassStateDTO` into `ShouldUseVisualOverkill`.
- Patched `EcosystemDirector` unsafe pointer and graphics-buffer upload calls to resolve vault wrappers to explicit `NativeArray<T>` and specify generic element types.
- Re-ran stale-reference scans, scoped `git diff --check`, and dotnet compile validation.

Cinematic cheats used:
- No new simulation, raymarch, particle truth, or physical water/light work was added.
- Existing visual fake ladder remains unchanged: analytical Beer-Lambert on fallback/mobile, packed LUT on active material/post/shaft paths.

Exact microseconds saved:
- Exact profiler measurements remain unavailable; no Unity player/profiler run was executed.
- Expected hot-path delta from compile-seam repairs: 0 us. These edits restore typed access to existing vault-owned state and GPU upload buffers; they add no per-frame allocation, file I/O, or extra shader sampling.
- Prior extinction estimates remain estimates, not measurements: LOW vertex sampling saves 40-140 us/frame versus broad per-pixel object LUT sampling; packed LUT fake saves 80-250 us/frame versus an 8-step raymarch.

Current validation:
- `rg` finds no deleted compass `_blackBoxCursor`, `_hasLastActualAup`, or `_lastActualAup` fields; no no-arg `ShouldUseVisualOverkill()` call remains.
- `rg` finds no untyped ecosystem `GetUnsafeBufferPointerWithoutChecks(_...)`, `GetUnsafeReadOnlyPtr(_...)`, or `_floraPredatorAupUpload` generic inference call.
- Scoped `git diff --check` passes with CRLF warnings only.
- Latest `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly` exits 0 with 0 warnings and 0 errors. Dump: `Docs/AgentLogs/Dump_EXTINCTION_LUT_SAMPLER_Build.txt`.
- Unity shader import, Android/Quest device staging, RenderDoc, GCMonitor, Memory Profiler, player build, and visual capture remain pending. I am not claiming those from dotnet.

## 2026-05-16 Ninth-Pass Unity Import / Bucketing Cycle Repair
What was wrong:
- Unity 6000.4.1f1 batch import was not validated. The first real import attempt failed before shader validation.
- One failure belonged to code this workstream had already touched: `ModuloSimulationBucketer.Initialize(int)` referenced `GlobalRegistry` from `Hecton8.Core.Bucketing`, but `Hecton8.Core` references `Hecton8.Core.Bucketing`, creating a Unity asmdef cycle.
- Parallel non-rendering work also changed the current compile state; the latest dotnet dump is no longer green.

What was done:
- Ran Unity batchmode import with logs written to `Docs/AgentLogs/Unity_EXTINCTION_LUT_SAMPLER_Import.log`.
- Patched `ModuloSimulationBucketer.Initialize(int)` to reuse an injected `_dataVault` instead of touching `GlobalRegistry`.
- Patched `GameBootstrapper.EnsureSimulationBucketerRegistered()` to inject `GlobalRegistry.DataVault` through the concrete cold bootstrap overload.
- Re-ran Unity batchmode import with `Docs/AgentLogs/Unity_EXTINCTION_LUT_SAMPLER_Import_AfterBucketer.log`.

Cinematic cheats used:
- No new simulation. Extinction remains a Beer-Lambert fake: analytical ALU fallback on low-memory/mobile, packed LUT on active middle/high/ultra material/post/shaft paths.
- No raymarch, no Texture3D-only path, no emissive branch, no sampled fallback `lerp`, and no extra texture bind were added.

Exact microseconds saved:
- Exact profiler measurements remain unavailable; no Unity player/profiler run was executed.
- Bucketing asmdef cycle repair expected runtime delta: 0 us. It changes cold bootstrap injection only.
- Existing extinction estimates remain estimates, not measurements: LOW vertex sampling saves 40-140 us/frame versus broad per-pixel object LUT sampling; packed LUT fake saves 80-250 us/frame versus an 8-step raymarch; global bind avoids 5-40 us/frame of material-loop style churn in material-heavy scenes.

Current validation:
- `Hecton8.Core.Bucketing.dll` now compiles during Unity import and copies to `Library/ScriptAssemblies`.
- Unity import still exits 1. Current blocking errors are external to extinction: `Assets/_Project/Scripts/Audio/Virtualization/AudioVirtualizationJobs.cs` assembly/reference errors and `_Project/Editor` missing-reference errors in `HectonDevToolsMenu`, `HectonRenderPipelineValidator`, `HectonSurfacePainter`, `RockDataBakerWindow`, and `SaveSlotManagerWindow`.
- Latest `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly` exits 1 on external `SargassumMicroFaunaBoids` and `TetherInstance` errors. Dump: `Docs/AgentLogs/Dump_EXTINCTION_LUT_SAMPLER_Build.txt`.
- Runtime/profiler/player validation remains pending. I am not marking VERIFIED MASTER GRADE while the current disk build and Unity import are blocked.

## 2026-05-17 Tenth-Pass Core Compile Revalidation / Unity Boundary
What was wrong:
- The previous status carried stale Sargassum/Tether and audio/editor blocker text after the disk state moved under parallel work.
- Current core validation hit small compile seams outside the extinction shader path: missing lockstep signal constants, a missing `Hecton8.Core.Memory.Defrag` project reference needed by `GlobalDataVault`/`SystemDispatcher`, and an invalid `NativeSlice.IsCreated` guard in the compass blackbox path.
- Unity import still fails before shader/player validation on external gameplay, GPR, fauna, global-signal, and VR foveation asmdef/compiler errors.

What was done:
- Added the missing lockstep signal constants required by `LockstepStateValidator`.
- Added the missing `Hecton8.Core.Memory.Defrag` reference to `Hecton8.Core.csproj`.
- Repaired the compass blackbox guard to use slice length instead of a nonexistent `NativeSlice.IsCreated` property.
- Re-ran core dotnet validation and Unity 6000.4.1f1 batch import after the core build turned current.

Cinematic cheats used:
- No visual-model change. Extinction remains analytical ALU Beer-Lambert on mobile/low-memory fallback, vertex-sampled on LOW, packed LUT per-pixel in material/post paths on higher tiers, and shaft-tinted on high/ultra.
- No raymarch, Texture3D-only dependency, compute prefilter, emissive branch, sampled fallback `lerp`, or extra `_ExtinctionLUT` bind was added.

Exact microseconds saved:
- Compile-seam repairs: 0 us expected runtime delta; this pass changed validation/assembly seams only.
- Exact profiler measurements remain unavailable; no Unity player/profiler run completed.
- Existing values remain estimates, not measurements: LOW vertex sampling saves 40-140 us/frame versus broad per-pixel object LUT sampling; packed LUT fake saves 80-250 us/frame versus an 8-step raymarch; single global bind avoids 5-40 us/frame of material-loop churn in material-heavy scenes.

Current validation:
- Latest `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies --no-incremental --disable-build-servers /p:UseSharedCompilation=false /p:RunAnalyzers=false /nr:false /m:1 -v:minimal /clp:ErrorsOnly` exits 0 with 2 warnings and 0 errors. Dump: `Docs/AgentLogs/Dump_EXTINCTION_LUT_SAMPLER_Build.txt`.
- Unity batch import log `Docs/AgentLogs/Unity_EXTINCTION_LUT_SAMPLER_Import_AfterCoreGreen.log` still exits 1. Current blockers are external to extinction: missing `Hecton8.Core.Determinism` in `PlayerKinematicsRuntime`, missing `Hecton8.World.GPR` / `GroundRadarTelemetryEntry` / `GroundRadarConstants` in `GroundPenetratingRadarRuntime`, missing `IDataVault` and `VaultBufferHandle<>` imports in `ProceduralCrabLegIKRuntime`, missing `VisualFlareSignal` in `GlobalSignals`, and missing `FoveatedRenderingCaps` in `FoveatedRenderCommander`.
- Post-update scoped debt scans returned no matches for hot-path `Update`/`LateUpdate`/`FixedUpdate`, `string.Format`, `EventBus`, local `new NativeArray`/`new NativeList`, `File.ReadAllBytes`, `downloadHandler.data`, `UnityWebRequest.Get`, `StartCoroutine`, managed delegate types, `sampler_ExtinctionLUT`, black-texture fallback binds, or emissive `if` branches in the extinction/rendering surface.
- `git diff --check` passed for the tracked extinction docs/rendering files with CRLF warnings only.
- Runtime/profiler/player validation remains pending. I am not marking VERIFIED MASTER GRADE while Unity import is blocked.

## 2026-05-17 Thirteenth-Pass Light-Shaft Vault Eviction / Blackbox Reality Check
What was wrong:
- Disk state contradicted the status: `HectonUberNoirRuntimeBridge` only wrote `Dump_UBER_NOIR_INTEGRATOR.bin`, not the required `Dump_EXTINCTION_LUT_SAMPLER.bin`.
- `ScreenSpaceLightShaftRuntime` owned three persistent VFX `NativeArray` buffers locally through `H8Memory.Allocate`: top contributions, temporal history, and the 300-frame telemetry ring.
- `LightShaftContribution` had no explicit Pack=1/Size layout, leaving ARM64/Quest layout evidence weaker than the current mandate requires.

What was done:
- Restored populated and empty fault dump mirroring to `Dump_EXTINCTION_LUT_SAMPLER.bin`.
- Added `BufferID.LightShaftTopContributions`, `BufferID.LightShaftHistoryContributions`, and `BufferID.LightShaftTelemetryRing`.
- Converted `ScreenSpaceLightShaftRuntime` from component-owned persistent arrays to `GlobalDataVault` handles, resolved only inside locked frame-buffer windows.
- Added `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]` to `LightShaftContribution` and `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]` to `LightShaftTelemetryEntry`.

Cinematic cheats used:
- No new physical lighting simulation. Shafts remain a capped screen-space fake: LOW keeps the 8-tap budget and high tier keeps the richer tint path already driven by extinction.
- No raymarch, Texture3D-only dependency, compute prefilter, emissive branch, sampled fallback `lerp`, or extra `_ExtinctionLUT` bind was added.

Exact microseconds saved:
- Measured savings: 0 us; no profiler/player capture completed.
- Expected normal-frame visual delta: 0 us. The pass changes ownership and dump correctness, not sample count or render passes.
- Blackbox mirror cost is fault-path file I/O only.

Current validation:
- `git diff --check` passes for the touched files with CRLF warnings only.
- Scoped scans show no `H8Memory.Allocate`, `H8Memory.Release`, `private NativeArray`, `new NativeArray`, `File.ReadAllBytes`, `downloadHandler.data`, `UnityWebRequest.Get`, `Texture2D.blackTexture`, `sampler_ExtinctionLUT`, legacy `EventBus`, or managed delegate matches in the scoped extinction/rendering/shaft paths.
- Targeted `_ExtinctionLUT` scan still shows one real `Shader.SetGlobalTexture(_ExtinctionLutId, _extinctionTexture)` bind and one shader `LOAD_TEXTURE2D(_ExtinctionLUT)` site in the active LUT helper.
- One targeted `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies --no-incremental --disable-build-servers /p:UseSharedCompilation=false /p:RunAnalyzers=false /nr:false /m:1 -v:q /clp:ErrorsOnly` was run after the enum/signature change. It exits 1 on external player presentation signal project/include drift: `PlayerFootstepSignal`, `PlayerWaterSplashSignal`, `PlayerExhaleSignal`, `PlayerSprintStateSignal`, `PlayerFatalPressureSignal`, and `PlayerTransportBailoutSignal`. The dump is `Docs/AgentLogs/Dump_EXTINCTION_LUT_SAMPLER_Build_AfterLightShaftVault.txt`.
- The build dump contains no errors naming `ScreenSpaceLightShaftRuntime`, `ScreenSpaceLightShaftSource`, `HectonUberNoirRuntimeBridge`, `LutArrayResolver`, or the extinction shader files.
- Runtime/profiler/player validation remains pending. I am not marking VERIFIED MASTER GRADE while compile/import validation is blocked.

## 2026-05-17 Twelfth-Pass Signal/Caps Seam Repair / Unity Boundary
What was wrong:
- Unity import was blocked before shader validation by two type-resolution seams outside the Beer-Lambert shader files: `VisualFlareSignal` lived in the downstream lighting source file while `GlobalSignals` needed it in core, and `FoveatedRenderCommander` used Unity 6000 `FoveatedRenderingCaps` without `UnityEngine.Rendering`.
- The status file still named those as active blockers after the disk state moved.

What was done:
- Added one core `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]` `VisualFlareSignal` contract in `GlobalSignals.cs`.
- Removed the duplicate light-shaft-local `VisualFlareSignal` definition from `ScreenSpaceLightShaftRuntime.cs`.
- Added `using UnityEngine.Rendering` to `FoveatedRenderCommander.cs`.
- Ran targeted scans and one Unity 6000.4.1f1 batch import. No dotnet rebuild was run in this pass.

Cinematic cheats used:
- No visual-model change. Extinction remains analytical ALU Beer-Lambert on mobile/low-memory fallback, vertex-sampled on LOW, packed LUT per-pixel in material/post paths on higher tiers, and shaft-tinted on high/ultra.
- No raymarch, Texture3D-only dependency, compute prefilter, emissive branch, sampled fallback `lerp`, or extra `_ExtinctionLUT` bind was added.

Exact microseconds saved:
- Signal/caps seam repair: 0 us expected normal-frame runtime delta. This is contract placement and namespace resolution only.
- Exact profiler measurements remain unavailable; no Unity player/profiler run completed.
- Existing estimates remain estimates, not measurements: LOW vertex sampling saves 40-140 us/frame versus broad per-pixel object LUT sampling; packed LUT fake saves 80-250 us/frame versus an 8-step raymarch; single global bind avoids 5-40 us/frame of material-loop churn in material-heavy scenes.

Current validation:
- Targeted scan shows exactly one `VisualFlareSignal` definition in `GlobalSignals.cs`.
- Targeted scan shows `FoveatedRenderCommander.cs` imports `UnityEngine.Rendering` and keeps Unity's `SystemInfo.foveatedRenderingCaps` path.
- Extinction debt scan returned no matches for `File.ReadAllBytes`, `downloadHandler.data`, `UnityWebRequest.Get`, `sampler_ExtinctionLUT`, `Texture2D.blackTexture`, or emissive `if` branches in the scoped rendering/shader paths.
- `git diff --check` passes for the touched signal/foveation files with CRLF warnings only.
- Unity batch import log `Docs/AgentLogs/Unity_EXTINCTION_LUT_SAMPLER_Import_AfterSignalCapsFix.log` no longer reports `VisualFlareSignal` or `FoveatedRenderingCaps`. It exits 1 on external `ProceduralCrabLegIKRuntime`, `LeviathanTentacleVerletSolver`, `SubmarineFluidDynamics`, and `SargassumMicroFaunaBoids` errors.
- Runtime/profiler/player validation remains pending. I am not marking VERIFIED MASTER GRADE while Unity import is blocked.

## 2026-05-17 Eleventh-Pass Blackbox Alias / Current Unity Boundary
What was wrong:
- The docs claimed `HectonUberNoirRuntimeBridge` fault dumps mirrored to `Dump_EXTINCTION_LUT_SAMPLER.bin`, but current code only wrote `Dump_UBER_NOIR_INTEGRATOR.bin` after worktree drift.
- Current core compile validation moved through unrelated compile seams before it could prove the extinction path: equipment padding width, player movement helper import, and an editor-only `System.Type` import in the acoustic zone controller.
- Unity import blockers changed again; the old GPR/fauna/player list is no longer current.

What was done:
- Patched `HectonUberNoirRuntimeBridge` so both full blackbox dumps and empty fallback dumps write `Dump_EXTINCTION_LUT_SAMPLER.bin` alongside the existing integrator dump.
- Re-ran scoped extinction/rendering debt scans.
- Re-ran `dotnet build Hecton8.Core.csproj` and Unity 6000.4.1f1 batch import.
- Preserved current build/import evidence in `Docs/AgentLogs/Dump_EXTINCTION_LUT_SAMPLER_Build.txt` and `Docs/AgentLogs/Unity_EXTINCTION_LUT_SAMPLER_Import_AfterBlackboxAlias.log`.

Cinematic cheats used:
- No visual-model change. Extinction remains analytical ALU Beer-Lambert on mobile/low-memory fallback, vertex-sampled on LOW, packed LUT per-pixel in material/post paths on higher tiers, and shaft-tinted on high/ultra.
- No raymarch, Texture3D-only dependency, compute prefilter, emissive branch, sampled fallback `lerp`, or extra `_ExtinctionLUT` bind was added.

Exact microseconds saved:
- Blackbox alias: 0 us normal-frame runtime delta; extra file write occurs only on fault dump.
- Validation compile-seam repairs: 0 us expected runtime delta; no shader sample, render pass, hot-path allocation, or texture upload was added.
- Existing values remain estimates, not measurements: LOW vertex sampling saves 40-140 us/frame versus broad per-pixel object LUT sampling; packed LUT fake saves 80-250 us/frame versus an 8-step raymarch; single global bind avoids 5-40 us/frame of material-loop churn in material-heavy scenes.

Current validation:
- Latest `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies --no-incremental --disable-build-servers /p:UseSharedCompilation=false /p:RunAnalyzers=false /nr:false /m:1 -v:minimal /clp:ErrorsOnly` exits 0 with 1 warning and 0 errors.
- Unity batch import log `Docs/AgentLogs/Unity_EXTINCTION_LUT_SAMPLER_Import_AfterBlackboxAlias.log` exits 1. Current blockers are external to extinction: missing `VisualFlareSignal` in `GlobalSignals` and missing `FoveatedRenderingCaps` in `FoveatedRenderCommander`.
- Post-update scoped debt scans returned no matches for hot-path `Update`/`LateUpdate`/`FixedUpdate`, `string.Format`, `EventBus`, local `new NativeArray`/`new NativeList`, `File.ReadAllBytes`, `downloadHandler.data`, `UnityWebRequest.Get`, `StartCoroutine`, managed delegate types, `sampler_ExtinctionLUT`, black-texture fallback binds, or emissive `if` branches in the extinction/rendering surface.
- `git diff --check` passed for the tracked extinction docs/rendering files with CRLF warnings only.
- Runtime/profiler/player validation remains pending. I am not marking VERIFIED MASTER GRADE while Unity import is blocked.
