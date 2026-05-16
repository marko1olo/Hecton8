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
