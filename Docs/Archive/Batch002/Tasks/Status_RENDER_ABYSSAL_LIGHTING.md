# Status_RENDER_ABYSSAL_LIGHTING

PROMPT IDENTIFIED: RENDER_ABYSSAL_LIGHTING
ROLE: NOIR_LIGHTING_TECH
DOMAIN: ECHELON 7 - ATMOSPHERE & CELESTIAL / ABYSSAL RENDERING
TASK COUNT: 15
STATUS: PENDING VERIFICATION (OMEGA POLISH COMPLETE; COMPILE BLOCKED BY DEPENDENCY)

## Mandates Loaded
- REND_Shader_Noir_Aesthetics_Dithering_Fog.txt
- REND_Abyssal_Lighting_Voxel_Occlusion_Shadows.txt
- REND_URP_Graphics_HotPath_Optimization_HLOD.txt
- GPU_Compute_Kernels_Kernels_Optimization_MX350.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt

## Checklist
- [x] 1. NOIR_LIT SHADER | DOD: `Hecton_CoreLit.hlsl` exposes `_MATH_LOD_LOW/_MATH_LOD_HIGH`; `Hecton_AbyssalVoxelRock` uses `SampleSH`, one main directional light, and excludes additional lights on low tier. | Alternative rejected: Unity point/spot lights and unrestricted `_ADDITIONAL_LIGHTS`. | Estimate: -18 us GPU per 100 visible cave chunks on MX350.
- [x] 2. DITHERED FOG BLEND | DOD: `Hecton_NoirDepthFog.shader` now uses exponential depth fog with TAA/IGN-style transition-edge dithering plus marine-snow density. | Alternative rejected: flat Unity linear fog and full-res volumetric fog for the base pass. | Estimate: +3 us GPU, saves visual overdraw versus full volumetrics by roughly -140 us.
- [x] 3. SPHERICAL HARMONICS PROXY [BLOCKED BY DEPENDENCY] | DOD: dynamic lighting path is constrained to `SampleSH`/main directional in the core-lit shader; no voxel-chunk SH grid provider was found in this domain. | Alternative rejected: adding direct fish/drone dependencies or runtime point lights. | Estimate: -25 us GPU versus per-object realtime lights; true voxel SH grid requires upstream provider.
- [x] 4. VOXEL AO INJECTION | DOD: existing `VoxelNormalJob` reads quantized `densityField`, writes `ambientOcclusionValues`, `VoxelColorJob` bakes AO into vertex colors, and mesh upload carries UV1.w for shader AO. | Alternative rejected: SSAO/HBAO screen solve. | Estimate: -220 us GPU on MX350 versus SSAO, +6 us CPU during cold chunk build.
- [x] 5. LIGHT SHAFT COMPUTE | DOD: `Hecton_VolumetricLight.compute` is half-res and now caps to 12 steps high / 4 steps low; `VolumetricLightFeature` resolves the same budget. | Alternative rejected: full-res raymarch and 16/32 step VFX profile defaults. | Estimate: -180 us GPU low tier, +90 us high tier for visible overkill.
- [x] 6. JITTERED RAYMARCHING | DOD: existing `ResolveInterleavedGradientNoise` jitters the raymarch start; retained and statically verified. | Alternative rejected: random texture lookup and unjittered banded march. | Estimate: 0 us extra texture cost, -1 temporal banding artifact class.
- [x] 7. BIOLUMINESCENCE MASK | DOD: 16 global glow points published by `HectonBiolumDiffusionVolume` and consumed by `HectonCoreLitEvaluateGlowPointRadiance`. | Alternative rejected: Unity Point Lights for leviathans/flares. | Estimate: -300 us GPU versus 16 realtime point lights; +8 us CPU upload when points exist.
- [x] 8. SQUARED DISTANCE FALLOFF | DOD: glow attenuation uses `dot(delta, delta)` and range squared; no `length()` in the glow loop. | Alternative rejected: `length()`/sqrt falloff. | Estimate: -2 us GPU per 16-point evaluation batch.
- [x] 9. CAUSTICS PROJECTION | DOD: existing directional-light straight-down projected procedural caustics remain active through `HectonCoreLitEvaluateProjectedCausticsScattering`; no point-light caustics added. | Alternative rejected: dynamic caustic light projectors. | Estimate: -70 us GPU versus projected light setup.
- [x] 10. DEPTH CRUSH CURVE | DOD: `HectonCoreLitApplyDepthCrushCurve` applies `pow(col, 2.2)` below 500m, with a low-tier square approximation. | Alternative rejected: LUT-only contrast that ignores world depth. | Estimate: +4 us GPU high tier, +1 us low tier.
- [x] 11. SUBMARINE HEADLIGHT TUBE | DOD: `Hecton_FlashlightConeSilt.shader` is a transparent additive depth-fade cone shader. | Alternative rejected: realtime Spotlight volume. | Estimate: -160 us GPU versus spotlight shadows.
- [x] 12. EMISSION PULSE | DOD: existing sonar globals from acoustic ping (`_SonarRevealOriginWS`, `_SonarRevealWaveParams`, `_SonarWaveFront`) now boost glow-point and biolum volume radiance. | Alternative rejected: material instance pulses or renderer scans. | Estimate: 0 GC; +1 us shader math near active ping.
- [x] 13. REMOVE SSAO | DOD: Unity AssetDatabase removed `ScreenSpaceAmbientOcclusion` subfeatures from `PC_Renderer.asset` and `PC_High_Renderer.asset`; static `rg` finds none in renderer assets. | Alternative rejected: disabling but leaving serialized SSAO. | Estimate: -220 us GPU MX350.
- [x] 14. RECONNAISSANCE PROTOCOL | DOD: `Docs/AgentLogs/RECON_RENDER_ABYSSAL_LIGHTING.md` logs 992 scanned materials and 194 Standard/URP Lit flags. | Alternative rejected: chat-only report. | Estimate: 0 runtime impact.
- [x] 15. OMEGA COMPILE CHECK [BLOCKED BY DEPENDENCY] | DOD: Edited shader error was fixed; targeted static audit passed for lighting files; `dotnet build Hecton8.Core.csproj` still fails outside this domain with `HectonSurvivalSystem.cs(298,29)` missing `SurvivalPhysiologyScalarResult` and `HectonBoidController.cs(73,86)` missing `IAcousticPingEventListener.OnAcousticPing(in AcousticPingEvent)`. Unity `read_console` retry was unavailable because the session did not answer ping. | Alternative rejected: editing survival/fauna contracts from a render prompt. | Estimate: 0 us runtime; verification blocked.

## Iteration Log
- Loop 0: Extracted XML prompt from `Docs/Tasks/CURRENT_BATCH.md`. Status and rationale files created. No runtime code touched yet.
- Loop 1: Tasks 1-5 implemented or verified. Shader redefinition surfaced in Unity console and was fixed. Full compile blocked by unrelated dependencies.
- Loop 2: Re-extracted original prompt after task 3 boundary. Tasks 6-10 implemented or verified by static shader searches.
- Loop 3: Tasks 11-15 executed. AssetDatabase recon wrote `RECON_RENDER_ABYSSAL_LIGHTING.md` and removed Unity SSAO renderer features.
- Loop 4: Self-audit removed duplicate `_HectonMathLodMode` declaration; `rg` confirms no `ScreenSpaceAmbientOcclusion`, `HBAO`, or `SSAO` strings remain in project renderer data assets.
- Loop 5: Unity console recheck shows no edited shader errors. Remaining errors are outside this prompt domain: `MantaScooter`, `PowerGridManager`, and `SaveBinaryStorage` Burst.
- Loop 6: Omega polish executed. Targeted audit found no hot-path managed `foreach`, no string interpolation in touched runtime bridge, Editor-only `.ToString()` in the shader stripper, tier-gated safe normalization, rsqrt use in volumetric compute, and external `dotnet build` blockers listed above.
- Loop 7: Honest AAA R&D continuation. Fixed the biolum proxy bridge's bandwidth gap: valid zones are compacted, non-finite zone data is guarded and telemetered, 32-point compute-buffer uploads are skipped when the quantized payload hash is unchanged, and 16 glow shader globals are only republished on count/hash changes. Static audit shows only cold arrays and guarded upload sites. `dotnet build` still fails outside render with 76 errors/5 warnings in unrelated core/save/audio/fauna symbols; no `HectonBiolumDiffusionVolume.cs` error appeared in the compiler tail.

## Honest R&D Addendum
- [x] BIOLUM BANDWIDTH DIRTY FLAGS | DOD: `HectonBiolumDiffusionVolume` now hashes quantized point payloads and skips unchanged `GraphicsBufferUploadUtility.UploadArray` and `Shader.SetGlobalVectorArray` calls. | Alternative rejected: pushing globals every Tick because the arrays are small; this violates bandwidth discipline on MX350. | Estimate: -3 to -8 us CPU/GPU-driver overhead on static biolum fields, 0 B/frame.
- [x] GLOW DATA COMPACTION | DOD: null zones no longer leave stale holes in `_pointUpload`; valid zones are packed into a dense prefix before count publication. | Alternative rejected: returning source `safeCount` with stale skipped slots. | Estimate: correctness fix; prevents false ghost glow and stale compute injection.
- [x] NON-FINITE INPUT GUARD | DOD: glow positions are finite-checked before rendering upload; bad scalar color/range/intensity inputs are clamped and emit one `MathGuardInvalidNumber` event per frame. | Alternative rejected: passing NaN/INF into shader globals and hoping the shader masks it. | Estimate: 0 us normal path beyond scalar branches; crash-dump value is higher than the branch cost.
