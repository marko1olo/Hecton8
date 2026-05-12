# Status_RENDER_FILLRATE

Agent: RENDER_FILLRATE
Role: RENDER_POLICE
Domain: ECHELON 8 PRESENTATION & UX / Rendering Fillrate
Task Count: 20
Batch Source: Docs/Tasks/CURRENT_BATCH.md
Status: PENDING VERIFICATION

## Mandates Loaded
- REND_URP_Graphics_HotPath_Optimization_HLOD.txt
- REND_Shader_Noir_Aesthetics_Dithering_Fog.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- REND_GPU_Sovereignty.txt
- REND_DescriptorBinding_Reality_Check.txt

## Intake
- [x] Extract `<AGENT_PROMPT id="RENDER_FILLRATE">` | Justification: CLI regex over `Docs/Tasks/CURRENT_BATCH.txt` captured the full XML block; rejected chat-only task memory and missing `.md` assumption. Estimate: 60 us.
- [x] Verify task count | Justification: counted explicit numbered objectives in the extracted XML; rejected neighboring prompt leakage. Estimate: 20 us.
- [x] Verify status hygiene | Justification: target status/rationale files were missing or empty before initialization; rejected reading prior unrelated fill-rate logs. Estimate: 30 us.

## Core Tasks
- [x] 1. DITHERED TRANSPARENCY ONLY | Justification: `SuitVisor.shader` and `Hecton_HUD_AcousticRadarOverlay.shader` no longer use Transparent queue or SrcAlpha blending; both use AlphaTest/Cutout coverage with Bayer dither clip. Rejected alpha blend and runtime material hacks. Estimate: 180 us saved per full-screen visor/radar overlap on MX350, PENDING VERIFICATION.
- [x] 2. STENCIL MASKED HUD | Justification: visor pass writes stencil ref 1 and acoustic radar HUD reads `Stencil == 1`; existing diegetic HUD shader already reads ref 1. Rejected drawing UI outside the helmet and hiding it by alpha. Estimate: 70 us saved in hidden helmet-frame pixels, PENDING VERIFICATION.
- [x] 3. Z-PREPASS FOR WATER | Justification: added RenderGraph `HectonFillrateDepthPrepassFeature` and `Hecton_FillrateDepthOnly.shader` to write water/terrain/voxel depth before transparent/refractive passes. Rejected late transparent water depth and per-object script mutation. Estimate: 110 us saved where water/silt is occluded, PENDING VERIFICATION.
- [x] 4. HALF-RES VFX RENDERING | Justification: half-res particle feature already composites via bilateral depth-aware shader; filtering now includes AlphaTest/Cutout FX queues so dithered smoke/plumes are captured. Rejected full-resolution alpha particles and transparent-queue-only filtering. Estimate: 220 us saved during dense smoke, PENDING VERIFICATION.
- [x] 5. AAA NOIR CONTRAST | Justification: `Hecton_CoreLit.hlsl` now applies a depth-weighted black-crush curve down to the mandated abyss floor luminance. Rejected pure `#000000` because the shader aesthetics mandate reserves a nonzero abyss floor. Estimate: 30 us visual-fake cost to hide far low-poly artifacts, PENDING VERIFICATION.
- [x] 6. BLUE NOISE SHADOW DITHER | Justification: shared CoreLit now resolves MX350 shadow attenuation through screen-space IGN/TAA dither when soft-shadow variants are stripped; DryZone, voxel rock, scatter, wreck, and leviathan lighting route main-light shadow attenuation through the helper. Rejected multi-tap PCF because MX350 fill/bandwidth is the bottleneck. Estimate: 95 us saved versus soft shadow variants on low tier, PENDING VERIFICATION.
- [x] 7. VOLUMETRIC FOG JITTER | Justification: volumetric compute fog and scooter shaft shader now use temporal IGN phase offsets for ray/fake radial taps; NoirDepthFog no longer advertises Transparent queue. Rejected texture blue-noise fetches and full raymarch expansion. Estimate: 45 us saved by preserving low tap counts without banding, PENDING VERIFICATION.
- [x] 8. ALU CAUSTICS FAKE | Justification: shared procedural caustics now uses a 3-sine overlap in the pixel shader with no caustic texture sample. Rejected projected caustic textures and mesh decals. Estimate: 60 us texture bandwidth saved in caustic-heavy views, PENDING VERIFICATION.
- [x] 9. DEPTH-FADED ALPHA | Justification: visor, abyssal smoke, and leak plume dither coverage now fades against SceneDepth before clip to remove hard intersection lines while staying AlphaTest/Cutout. Rejected soft-particle alpha blending. Estimate: 80 us saved versus transparent soft particles, PENDING VERIFICATION.
- [x] 10. TAA MOTION VECTOR FIX | Justification: existing indirect vegetation motion-vector shader computes current and previous vertex-displaced positions and the renderer submits Object motion passes for near/far vegetation; audited and kept instead of duplicating a second path. Rejected camera-only motion for vertex-swaying kelp. Estimate: 35 us TAA resolve stability gain, PENDING VERIFICATION.
- [x] 11. LOD SHADER SWITCHING | Justification: indirect vegetation near/far split now defaults to 20m, and kelp/GPUI kelp use `HectonCoreLitResolveFlatNoirLod` to skip normal-map sampling and suppress spec/rim/transmission in far flat-noir mode. Rejected extra material swaps per instance. Estimate: 140 us saved in distant kelp fields, PENDING VERIFICATION.
- [x] 12. STENCIL VISOR OVERLAY | Justification: `SuitVisor.shader` writes Stencil ref 1 and `Hecton_HUD_AcousticRadarOverlay.shader` compares Equal ref 1 via `_StencilRef`; this duplicates task 2 as explicit overlay evidence. Rejected alpha-hidden HUD pixels. Estimate: 70 us saved in helmet-frame pixels, PENDING VERIFICATION.
- [x] 13. OPAQUE DEPTH PREPASS | Justification: `HectonFillrateDepthPrepassFeature` uses RenderGraph depth-only override material on Water/Terrain/VoxelCave layers before transparent/refractive/silt work. Rejected late depth writes and `Execute` command-buffer paths. Estimate: 110 us saved in occluded water/silt pixels, PENDING VERIFICATION.
- [x] 14. LIGHT PROBE APPROXIMATION | Justification: leviathan fauna ambient now uses vertex SH (`ambientSH`) passed from the vertex stage, and fauna shader strips point-light keywords. Rejected real-time point lights/additional-light loops for fauna bodies. Estimate: 65 us saved for large fauna lighting, PENDING VERIFICATION.
- [x] 15. SHADER VARIANT STRIPPING | Justification: existing `HectonShaderVariantStripper` is confirmed active as `IPreprocessShaders`/`IPreprocessBuildWithReport` and strips `POINT_LIGHTS`/point keywords by default unless `HECTON_MX350_SHADER_STRIP=0`. Rejected material-only keyword pruning. Estimate: build/runtime variant pressure reduced, microsecond runtime value PENDING VERIFICATION.
- [x] 16. SCREEN-SPACE DECALS | Justification: active abyssal fluid/blood aftermath states now feed `DeferredDecalPass` as screen-space projected decal data; the mesh path is an explicit fallback and its shader is cutout/dithered. Rejected transparent quad decals and particle alpha. Estimate: 120 us saved in rupture-fluid views, PENDING VERIFICATION.
- [x] 17. REFRACTION MATH LOD | Justification: `SuitVisor.shader` now uses static hash UV offset when refraction scale is low and a second scene tap only at high refraction scale; existing controller low/medium/high quality mapping drives the scale. Rejected always-on multi-tap refraction. Estimate: 55 us saved on low visor quality, PENDING VERIFICATION.
- [x] 18. ZERO-TEXTURE BIOLUM | Justification: kelp and coral authored glow pulses are now driven by `_Time.y`, world position, and mesh UV/shape masks; emissive texture-mask contribution was removed from authored biolum pulse masks. Rejected emissive mask textures for pulse identity. Estimate: 35 us texture/ALU blend pressure saved in dense flora views, PENDING VERIFICATION.
- [x] 19. BRG PROPERTY PACKING | Justification: `HectonVegetationInstanceData.BioluminescenceColor` now packs RGB color plus 8-bit biolum intensity and 8-bit damage state in one Vector4 lane; the indirect vegetation shader decodes that packed alpha. Rejected adding a new BRG metadata buffer or widening the 64-byte stride. Estimate: 25 us memory-fetch pressure saved in vegetation-heavy views, PENDING VERIFICATION.
- [x] 20. OMEGA COMPILE CHECK | Justification: added `HectonTransparentOverdrawBuildGuard` as an editor build gate scanning `02_HECTON_WORLD` dependencies and failing when estimated transparent pixel overlap factor exceeds 2.5. Rejected profiler-only/manual audits. Estimate: 0 us runtime, build-time guard, PENDING VERIFICATION.

## Loop State
- Loop 1: Tasks 1-5 implemented. Compile blocked by unrelated project errors.
- Loop 2: Tasks 6-10 implemented. Compile blocked by unrelated project errors.
- Loop 3: Tasks 11-15 implemented. Compile blocked by unrelated project errors.
- Loop 4: Tasks 16-20 implemented. Runtime compile passed; broader editor compile is blocked by an unrelated combat duplicate-method dependency.
- Loop 5: Strict self-audit complete. Omega polish replaced late divisions with `rcp`, confirmed cold-only allocations in runtime additions, and verified scoped domain rationale.
- Loop 6: Broad anti-alpha sweep complete. Removed remaining `Blend SrcAlpha`, HECTON additive presentation blends, and `Transparent` render queues from `_Project/Art/Shaders`, `_Project/Shaders/UI`, and archived `_Project/_Archive/HectonOcean.shader`; retained only non-alpha Crest damping and hidden editor overdraw heatmap exceptions.

## Verification
- Runtime compile: PASSED. `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` completed with 0 warnings and 0 errors after Omega polish.
- Core compile: PASSED. `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` completed with 0 warnings and 0 errors after package restore regenerated missing assets files.
- Editor compile: PASSED. `dotnet build .\Hecton8.Editor.csproj --no-restore -v:minimal` completed with 0 warnings and 0 errors after dependency owners resolved the transient combat/core blockers.
- Runtime compile after broad sweep: PASSED. `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` completed with 0 warnings and 0 errors.
- Editor compile after broad sweep: PASSED. `dotnet build .\Hecton8.Editor.csproj --no-restore -v:minimal` completed with 0 warnings and 0 errors.
- Static alpha blend scan: PASSED. `rg -n -F "Blend SrcAlpha" -- Assets/_Project` returned no matches.
- Static transparent queue/tag scan: PASSED. `rg -n -P '"Queue"\s*=\s*"Transparent' -- Assets/_Project` and `rg -n -P '"RenderType"\s*=\s*"Transparent"' -- Assets/_Project` returned no matches.
- Static non-off blend scan: PENDING EXCEPTIONS. `rg -n -P --glob '*.shader' "^\s*Blend\s+(?!(Off|One\s+Zero)\b)" Assets/_Project` now returns only `Crest_SargassumFoamDamping.shader`, `Crest_SargassumWaveDamping.shader`, and hidden editor `Hecton_OverdrawHeatmap.shader`; runtime HECTON presentation additive/alpha passes were converted.
- Unity batchmode import/compile: PARTIAL PASS / BLOCKED BY DEPENDENCY. `Unity.exe -batchmode -nographics -quit -projectPath C:\hades\Hecton8 -logFile Docs/AgentLogs/Unity_RENDER_FILLRATE.log` compiled and copied `Hecton8.Optimization.Editor.dll`, then failed later in unrelated `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompiler.cs(745,34)` and `(746,34)` float-to-uint compile errors before shader import verdict.
- Unity/Frame Debugger/RenderDoc: PENDING, no capture available.
- GC: PENDING, no profiler capture available.
- GPU frame time: PENDING, no MX350 capture available.
