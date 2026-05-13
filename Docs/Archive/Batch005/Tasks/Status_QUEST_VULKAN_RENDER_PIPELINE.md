# QUEST_VULKAN_RENDER_PIPELINE Status

Agent: QUEST_VULKAN_RENDER_PIPELINE  
Domain: GRAPHICS_PROGRAMMER / VR Somatic Comfort / URP Rendering  
Task Count: 15  
Status: PENDING VERIFICATION

## Prompt Source

- Direct chat dispatch received 2026-05-13.
- `Docs/Tasks/CURRENT_BATCH.md` was scanned by CLI for `<AGENT_PROMPT id="QUEST_VULKAN_RENDER_PIPELINE">`; prompt was not present.
- Re-read cadence will use this status file plus the direct assignment summary below because no active batch XML block exists on disk.

## Relevant Mandates Read

- `REND_Foveated_Simulation_LOD.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `REND_DescriptorBinding_Reality_Check.txt`
- `REND_VR_Stencil_Masking.txt`
- `GPU_Compute_Warp_Sizing_Mobile.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## Direct Assignment Summary

1. Create `URP_Quest_VR.asset`; disable Depth Texture and Opaque Texture.
2. Force Single Pass Instanced or Multiview for VR without damaging PC/other platforms.
3. Configure Unity 6 RenderGraph/tile-memory-friendly behavior where project APIs allow.
4. Add `OculusFfrEnforcer.cs` to enable Fixed Foveated Rendering High/HighTop via OpenXR/Oculus API.
5. Validate `HectonVisorUberPost` depth fog under SPI/stereo matrices.
6. Query `SystemInfo.systemMemorySize`; treat `< 8000` MB with Quest guards only when XR/Android evidence exists.
7. Apply Quest-only runtime mip bias via `QualitySettings.masterTextureLimit = 1`.
8. Add Android shader preprocessor stripping HDR, soft shadows, directional lightmap variants.
9. Enable 4x MSAA on the Quest URP asset; avoid FXAA for VR.
10. Replace flood waterline depth dependency with mathematical frustum/Y-plane fake, no `_CameraDepthTexture` on Android.
11. Maintain zero runtime GC.
12. Push VR eye texture resolution and FFR level to Blackbox/telemetry if an existing interface is present.
13. Scan for `Camera.targetTexture`; flag manual RT allocations > 1024x1024 on Quest.
14. Audit `HLOD_INSTANCE_CULLING` compute shaders for Vulkan-safe syntax.
15. Verify shader compilation for Android/Vulkan target.

## State Machine

- [x] Task 1: URP Quest asset mutation | DOD: `Assets/_Project/Data/URP_Quest_VR.asset` created with `m_RequireDepthTexture: 0`, `m_RequireOpaqueTexture: 0`, `m_MSAA: 4`, Quest renderer reference | Rejected: mutating shared PC/mobile asset | Estimate: 700-1300 us GPU bandwidth saved on Quest scenes that previously forced depth/opaque resolves.
- [x] Task 2: Single Pass Instanced / Multiview | DOD: Android stereo path verified as `Instancing` through Unity Editor API | Rejected: Multi Pass VR | Estimate: 600-1200 us CPU/GPU stereo submission saved.
- [x] Task 3: RenderGraph/TBDR configuration | DOD: Quest renderer disables depth-heavy features and visor RenderGraph pass skips depth input/use on Quest depthless path; URP store action optimization enabled | Rejected: full post stack with depth attachment | Estimate: 400-900 us bandwidth saved on Quest.
- [x] Task 4: FFR enforcer | DOD: `OculusFfrEnforcer.cs` applies `XRDisplaySubsystem.foveatedRenderingLevel=0.85`, gated to Android+Vulkan+XR Quest-class runtime | Rejected: Oculus SDK direct calls because Oculus/OpenXR packages are absent from `Packages/manifest.json` | Estimate: 900-1800 us fragment cost saved in peripheral lens area.
- [x] Task 5: Noir fog stereo sync | DOD: `HectonVisorUberPost` keeps SPI macros, gates depthless TBDR path, and feeds foveation globals through `HectonXRRuntimeState` | Rejected: duplicate Quest-only post shader | Estimate: 40-90 us CPU avoided by keeping one pass/material path.
- [x] Task 6: Unified memory gate | DOD: `QuestVulkanRuntimePolicy` queries `SystemInfo.systemMemorySize`; `<8000` MB is tracked as strict Quest memory gate and `<9000` MB/Quest signature as family gate | Rejected: RAM-only detection without Android/Vulkan/XR evidence | Estimate: prevents 0 us PC regression; enables Quest-only savings.
- [x] Task 7: Quest mipmap bias | DOD: runtime applies `QualitySettings.masterTextureLimit >= 1` and `globalTextureMipmapLimit >= 1` only on Quest runtime | Rejected: asset import downscale | Estimate: 75% texture residency reduction for affected mipmapped textures.
- [x] Task 8: Shader stripping | DOD: Android build strips HDR, soft shadow, directional/mixed lightmap variants in `HectonShaderVariantStripper` | Rejected: global stripping for all platforms | Estimate: build/runtime memory pressure reduced; no PC feature loss.
- [x] Task 9: MSAA hardware | DOD: Quest URP asset set to 4x MSAA and HDR off; FXAA not introduced | Rejected: post AA for VR | Estimate: visual edge stability gained with low TBDR cost.
- [x] Task 10: Depth resolve fake | DOD: mobile shader excludes `DeclareDepthTexture`, excludes `SampleSceneDepth`, and computes internal water plane mask from camera ray vs `_InternalWaterlineY`; desktop depth fog remains desktop-only | Rejected: `_CameraDepthTexture` on Android | Estimate: 300-800 us bandwidth saved by removing mobile depth resolve.
- [x] Task 11: Zero-GC audit | DOD: runtime FFR path uses cached static list and persistent `NativeArray` blackbox; no `foreach`; dump allocations only on diagnostic path | Rejected: per-frame subsystem list allocation | Estimate: 0 B/frame target maintained.
- [x] Task 12: Telemetry | DOD: FFR level and eye texture dimensions pushed into `HectonXRRuntimeState` and a 300-frame fixed blackbox ring; dump path `Docs/AgentLogs/Dump_QUEST_VULKAN_RENDER_PIPELINE.bin` | Rejected: string/log telemetry per frame | Estimate: 0 B/frame telemetry.
- [x] Task 13: RenderTexture reconnaissance | DOD: `Report_QUEST_VULKAN_RENDER_PIPELINE_Audit.md` lists `Camera.targetTexture` and manual RT allocations that need Quest review | Rejected: silent acceptance of >1024 manual RTs | Estimate: prevents 1-16 MB per target from becoming hidden Quest VRAM debt.
- [x] Task 14: Vulkan HLOD compute audit | DOD: `InstanceCulling.compute` checked for Vulkan-safe pragmas/buffers; no DirectX-only syntax found in audited file | Rejected: assuming DX compute syntax portability | Estimate: avoids Android/Vulkan runtime bind failure.
- [BLOCKED BY DEPENDENCY] Task 15: Android/Vulkan shader compile check | DOD: `dotnet build Hecton8.Core.csproj --no-restore` executed after local fixes; Unity asset APIs verified Android Vulkan + SPI | Rejected: declaring green compile while unrelated assemblies/types are missing | Estimate: compile verification blocked by 107 unrelated errors (`Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Audio.Propagation`, `BrineLayerSample`, `MacroSwarm`, etc.).

## Loop Log

- Loop 0: Initialized state. No code changed yet. Status remains PENDING VERIFICATION.
- Loop 1: Read mandates/domain/batch location; direct prompt recorded because `CURRENT_BATCH.md` did not contain this agent XML.
- Loop 2: Scanned URP/XR packages and project assets; found Unity 6 URP 17.4, Android graphics API Vulkan, stereo path Instancing, no Oculus/OpenXR package dependency available.
- Loop 3: Created Quest URP/renderer assets and Quest-safe runtime/editor code; preserved PC/other platform paths behind Android+Vulkan+XR gates.
- Loop 4: Re-read shader/runtime code; closed the mobile `_CameraDepthTexture` gap by compiling out depth include/helpers and using shader-side absolute Y-plane math for internal waterline.
- Loop 5: Ran validations/build; removed malformed duplicate hot-swap listener block from already-dirty `HectonUnderwaterVisuals.cs`; build now blocked by external missing domain dependencies, not this Quest pipeline patch.
- Loop 6: Wrote audit report and final status/rationale/log evidence. Overall status remains PENDING VERIFICATION until Android/Vulkan shader compile and Quest hardware FFR are verified.
