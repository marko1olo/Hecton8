# Status_EXTINCTION_LUT_SAMPLER

Prompt: EXTINCTION_LUT_SAMPLER
Role: GRAPHICS_PROGRAMMER
Domain: RENDERING/SHADERS
Task count: 18
State: FIFTH PASS COMPLETE; EXTINCTION PATH IMPLEMENTED; ANDROID STREAMINGASSETS URI STAGING ADDED; UNUSED EXTINCTION SAMPLER REMOVED; DOTNET CORE COMPILE GREEN; UNITY PLAYER/PROFILER VALIDATION PENDING

Relevant mandates identified before coding:
- REND_Shader_Noir_Aesthetics_Dithering_Fog.txt: Beer-Lambert/noir fog belongs in shader/post, LOW tier uses cheap LUT/dither, no physical atmosphere simulation.
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt: C# resolver is cold-only; no Tick/Update hot-path allocations.
- REND_URP_Graphics_HotPath_Optimization_HLOD.txt: URP-only shader variant discipline; LOW/MX350 load-shed path required.
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt: depth/world-space math must be camera-relative and finite.
- ARCH_Signal_Lane_Segregation.txt: cross-domain state must not duplicate managed event/global authority.
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt: persistent NativeArray ownership must live in GlobalDataVault or a named vault owner.
- STRM_Async_Asset_Upload_Texture_Settings.txt: texture payload staging must avoid full managed heap copies and respect platform asset path differences.

## Phase 1
- [x] 1. PURGE_SINGLETONS | N/A per prompt | DOD: no singleton added; resolver is static cold bootstrap only | Alternative rejected: manager MonoBehaviour/singleton | Hot-path estimate: 0 us.
- [x] 2. DEBT_CLEANUP | Removed runtime RenderSettings.fogColor writes from HectonUnderwaterVisuals and HectonCelestialEngine; left snapshot restore/read paths intact | DOD: authority split removed without deleting lifecycle restoration | Alternative rejected: keeping global fog color as color authority | Estimate: prevents 2-8 us/script state churn and color race per affected camera update.
- [x] 3. DATA_EVICTION | Added cold LutArrayResolver reading Water_Extinction_Matrix.bin into a packed 4096x4096 Texture2D via sequential 128 KiB staging and GetRawTextureData<byte>() upload; URL-style StreamingAssets paths now stage through DownloadHandlerFile into temporary cache for Android/Quest before the same chunked upload; routed extinction params/runtime/weather through HectonShaderGlobalDataVaultBridge; evicted HectonUnderwaterVisuals biome-fog transition persistent arrays into GlobalDataVault handles | DOD: no 32 MiB ReadAllBytes allocation, no UnityWebRequest downloadHandler.data, no Update allocation, no private persistent biome-fog NativeArray owner in the rendering visual system | Alternative rejected: byte[] full-file staging, runtime CPU sampling, per-material upload, DownloadHandlerBuffer, and component-owned Persistent NativeArrays | Estimate: saves 20-80 us/frame versus repeated binding/copy patterns; cold Steam Deck MicroSD pressure reduced from one 32 MiB managed read to sequential chunks; Android APK path no longer silently requires a pre-copied persistent file; biome-fog vault migration targets 0 us visual change with ownership cleanup.
- [x] 4. BURST_ALGORITHM | N/A GPU-bound | DOD: no fake CPU/Burst path | Alternative rejected: CPU light transport | Hot-path estimate: 0 us.
- [x] 5. AUP_INTEGRITY | Shader uses shifted world position produced by H8UberNoirObjectToAupWorld and clamps finite depth against runtime sea surface | DOD: AUP camera-relative path preserved | Alternative rejected: raw object-space depth | Estimate: prevents precision loss; performance delta 0-2 us.

## Phase 2
- [x] 6. DOD_SOA_LAYOUT | _ExtinctionLUT, _ExtinctionLUTParams, _ExtinctionLUTRuntime, _ExtinctionLUTWeatherParams are global shader state | DOD: SoA global vectors/textures, no material loop | Alternative rejected: per-renderer texture assignment | Estimate: saves 5-40 us/frame in material-heavy scenes.
- [x] 7. SIGNAL_FLOW | Turbidity enters LUT Y through HectonUnderwaterVisuals current turbidity plus GlobalWeatherDirector intensity shift via HectonShaderGlobalDataVaultBridge | DOD: DataVault-backed shader global slots; no invented WeatherStateSignal dependency | Alternative rejected: direct Shader.SetGlobalVector ownership in multiple systems or direct-coupling to weather internals | Estimate: 0 us extra hot-path beyond existing global publish.
- [x] 8. LOW_TIER_FAKE | _MATH_LOD_LOW samples extinction once in H8UberNoirVertex and passes half3 varying | DOD: MX350 path reduces per-pixel bandwidth | Alternative rejected: per-pixel LUT everywhere | Estimate: saves 40-140 us/frame at 1080p object-heavy underwater views.
- [x] 9. HIGH_END_OVERKILL | Non-LOW path samples per-pixel and Hecton_ScooterVolumetricShafts applies extinction to shafts | DOD: high tier spends saved cycles on cinematic color depth | Alternative rejected: LOW-only flat tint | Estimate: adds 15-70 us/frame when active, buying per-pixel depth fidelity.
- [x] 10. REACTIVE_VFX | UberNoir albedo multiplies by extinction and NoirDepthFog blends fog/source color by extinction | DOD: material and post stack both respond | Alternative rejected: fog-only override | Estimate: no CPU cost; GPU cost is 3 packed samples per relevant shading point.

## Phase 3
- [x] 11. STP_STABILIZATION | Added IGN fog dither in H8UberNoirFogIgnDither | DOD: no new noise texture allocation/dependency | Alternative rejected: atlas dependency or temporal random texture | Estimate: saves 10-35 us/frame versus extra texture sample path.
- [x] 12. NAN_VACCINATION | Depth/turbidity/wavelength finite checks and saturate clamps before packed index | DOD: no out-of-range LUT texel index when active | Alternative rejected: trusting authored floats | Estimate: 1-3 ALU per sample; avoids undefined reads.
- [x] 13. BLACKBOX_LOGGING | N/A per prompt; shader path has no critical CPU sim state | DOD: no fake NativeArray telemetry added | Alternative rejected: bogus circular buffer for stateless shader include | Hot-path estimate: 0 us.
- [x] 14. TRIPLE_STRIKE_REPAIR | Loader checks R16_SFloat and R16G16B16A16_SFloat sample support; falls back to ARGB32 quantized texture if RHalf is unsupported | DOD: target format capability is probed before upload | Alternative rejected: assuming desktop half formats | Estimate: fallback trades precision for compatibility; hot-path remains one texture sample.
- [x] 15. HOMEOSTASIS_ADAPTATION | N/A per prompt | DOD: no homeostasis owner invented | Alternative rejected: hidden gameplay feedback loop | Hot-path estimate: 0 us.

## Phase 4
- [x] 16. SHADER_STRIPPING | Existing Core/Hecton8_UberNoir.shader has #pragma multi_compile _ _MATH_LOD_LOW; include uses compile-time gates | DOD: per-pixel extinction path compiled out under LOW | Alternative rejected: runtime branch only | Estimate: saves 40-140 us/frame on LOW variant.
- [x] 17. BIOLUM_EXEMPTION | Emissive mask uses surface.orm.a and lerp(extinctAlbedo, surface.albedo, emissiveMask); no emissive if branch | DOD: branchless material mask | Alternative rejected: keyword/if branch | Estimate: saves divergence; 0-8 us/frame depending material coverage.
- [x] 18. FINAL_VALIDATION | Static shader/file checks pass; dotnet build Hecton8.Core.csproj --no-restore --no-dependencies /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly exits 0 with 0 warnings and 0 errors in Docs/AgentLogs/Dump_EXTINCTION_LUT_SAMPLER_Build.txt | DOD: latest C# compile dump is green; Unity shader import, player build, RenderDoc, GCMonitor, and visual capture remain pending because no Unity Editor/MCP run was available | Alternative rejected: claiming runtime/profiler proof from dotnet alone | Estimate: 0 us.

## Iteration Log
- Loop 0: Prompt extracted from CURRENT_BATCH.md. Status/rationale missing, clean start. Mandates read. Code not touched yet.
- Loop 1: Implemented shader include, UberNoir hooks, post fog hook, volumetric shaft hook. Rejected raymarch/simulation.
- Loop 2: Implemented cold LUT loader and global shader IDs. Re-read prompt block. Rejected per-material binding.
- Loop 3: Removed legacy runtime fog color writes while preserving lifecycle restore/read sites. Rejected RenderSettings color as active extinction authority.
- Loop 4: Self-review found loader in an autoReferenced:false graphics asmdef; moved resolver to Scripts/Rendering core bridge path. Rejected adding a cyclic asmdef dependency.
- Loop 5: Ran LUT byte-count check, grep audits, brace checks, emissive-branch scan, _MATH_LOD_LOW scan, targeted Roslyn compile for LutArrayResolver, git diff --check, and dotnet build with dump. Compile blocked by unrelated project errors; changed files do not appear in build errors.
- Loop 6: Re-read CURRENT_BATCH prompt and second-pass user directive. Replaced full-file LUT read with sequential 128 KiB staging and kept the texture upload global/cold.
- Loop 7: Routed extinction runtime/weather shader globals through HectonShaderGlobalDataVaultBridge slots instead of direct multi-owner Shader.SetGlobalVector calls.
- Loop 8: Cleared small compile blockers encountered during integration: TetherFiredSignal aliasing, DataVault-only ModuloSimulationBucketer resurrection for the deleted bucketing service, VaultProbe generic inference, XR refresh request API guard, duplicate GlobalDataVault ABI validator, and duplicate VehicleDocking helper methods.
- Loop 9: Re-ran dotnet build. Build wall expanded to 185 active cross-domain errors outside extinction ownership; stopped per 3-strike dependency discipline instead of rewriting SubmarineFluidDynamics/Ecosystem/Sargassum from this shader prompt.
- Loop 10: Removed split extinction-vector authority from LutArrayResolver by adding a DataVault-backed `_ExtinctionLUTParams` bridge slot beside runtime/weather slots.
- Loop 11: Migrated HectonUnderwaterVisuals biome-fog transition buffers from component-owned Persistent NativeArrays to GlobalDataVault handles and added `[StructLayout(LayoutKind.Sequential, Pack = 1)]` to the related Burst payload structs.
- Loop 12: Re-ran static scans and dotnet build. Touched rendering files are absent from the latest error dump; build is blocked by external EcosystemRuntimeInstaller and SubmarineFluidDynamics errors introduced in parallel work.
- Loop 13: Re-read CURRENT_BATCH prompt and actual AGENTS/domain files. Added Android/Quest URL-style StreamingAssets staging via UnityWebRequest + DownloadHandlerFile into temporary cache, then reused the existing 128 KiB file-to-texture upload. Rejected DownloadHandlerBuffer and downloadHandler.data because they recreate the 32 MiB managed staging failure.
- Loop 14: Re-ran git diff --check, scoped no-allocation/string/EventBus scans, Pack=1 struct scan, LUT byte count, and dotnet build. Latest Hecton8.Core.csproj build exits 0 with 0 warnings and 0 errors; Unity runtime validation remains pending.
- Loop 15: Ran domain shader thread-group sweep; largest `numthreads` product under `Assets/_Project/Art/Shaders` is 512, below the 1024 Metal/Quest limit. Removed unused `SAMPLER(sampler_ExtinctionLUT)` from `Hecton_WaterExtinction.hlsl` because extinction uses integer `LOAD_TEXTURE2D`. Re-ran brace counts and dotnet build; latest Hecton8.Core.csproj build exits 0 with 0 warnings and 0 errors.

## Omega Polish Audit
- Emissive exemption scan: no `if` branch tied to emissive terms; exemption is `emissiveMask = saturate(surface.orm.a)` plus `lerp(extinctAlbedo, surface.albedo, emissiveMask)`.
- Anti-bloat pass: loader moved out of isolated graphics asmdef; no cyclic asmdef reference added; no new MonoBehaviour manager added.
- Status discipline: C# compile is green in the latest dump, but runtime/profiler/player status is still pending because dotnet does not prove Unity shader import, Android APK staging at device runtime, RenderDoc capture, or GCMonitor numbers.
- Multiplatform audit: extinction shader uses TEXTURE2D/LOAD_TEXTURE2D only, no sampler state, no compute thread groups, no RWTexture, no D3D-only path, no per-frame C# Update, no File.ReadAllBytes, no UnityWebRequest.Get, and no downloadHandler.data. URL-style StreamingAssets paths stage to a file cache through DownloadHandlerFile before chunked upload. Domain-wide compute thread-group sweep found a maximum product of 512, below the 1024 limit; existing non-extinction Texture3D use remains in voxel/SDF/flow compute paths outside this LUT path.
- Data sovereignty audit: extinction globals now publish through DataVault-backed bridge slots, including `_ExtinctionLUTParams`. HectonUnderwaterVisuals biome-fog transition persistent arrays are evicted to GlobalDataVault handles; remaining NativeArray mentions in the scoped rendering files are texture/readback views, vault-resolved local views, or Burst job parameters. HectonCelestialEngine remains outside the rendering/shader domain and is not claimed fixed.
