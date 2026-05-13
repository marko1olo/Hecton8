# LOG_QUEST_VULKAN_RENDER_PIPELINE

## 2026-05-13 Quest Vulkan TBDR Pass

What was wrong:
- Quest-class TBDR path shared too much PC renderer behavior: depth/opaque copies, depth-driven post assumptions, no dedicated Quest URP asset, no enforced Single Pass Instanced evidence, and no hardware FFR runtime.
- Android shader variants kept HDR/soft-shadow/directional-lightmap baggage.
- Manual RenderTexture/`Camera.targetTexture` sites were not documented as Quest VRAM risks.
- Unity compile verification was blocked first by an already-dirty `HectonUnderwaterVisuals` hot-swap listener insertion, then by broad missing domain assemblies/types outside this graphics pass.

What was done:
- Created `Assets/_Project/Data/URP_Quest_VR.asset` and `Assets/_Project/Data/Quest_VR_Renderer.asset`; disabled depth texture, opaque texture, HDR, and depth-heavy renderer features; set Quest asset to 4x MSAA and tile-friendly store actions.
- Verified Android graphics API as Vulkan and stereo rendering path as Instancing through Unity Editor APIs.
- Added `Assets/_Project/Scripts/Core/OculusFfrEnforcer.cs` with Android+Vulkan+XR Quest guards, `SystemInfo.systemMemorySize` gate, FFR level 0.85, Quest-only mip limit, persistent 300-frame blackbox, and eye texture/FFR telemetry handoff.
- Extended `HectonXRRuntimeState` so hardware foveation state drives foveated shader globals without managed per-frame telemetry.
- Updated `HectonVisorUberPostFeature` and `HectonVisorUberPost.shader` so Quest/mobile path does not request depth input, does not include `DeclareDepthTexture`, does not compile `SampleSceneDepth` helpers, and computes internal water plane mask from camera ray vs `_InternalWaterlineY`.
- Extended `HectonShaderVariantStripper` with Android-only HDR/soft-shadow/directional-lightmap stripping.
- Added `QuestVulkanRenderPipelineConfigurator` and wired Android build configuration to regenerate/verify Quest assets.
- Wrote `Report_QUEST_VULKAN_RENDER_PIPELINE_Audit.md` with RT allocation reconnaissance and HLOD compute Vulkan audit.
- Removed duplicate malformed hot-swap listener blocks from already-dirty `HectonUnderwaterVisuals.cs`, preserving the earlier complete implementation and avoiding a broad revert.

Cinematic cheats used:
- Depth fog/shaft fidelity is traded off only on mobile: Quest uses no `_CameraDepthTexture`, no depth resolve, and a geometric Y-plane waterline lie.
- FFR buys center clarity by lowering peripheral cost at the XR compositor/subsystem level.
- Texture mip clamp is runtime-only on Quest; source assets remain untouched for PC/Ultra.

Exact microseconds saved, estimates pending hardware capture:
- Depth/opaque resolve removal: 700-1300 us on Quest scenes that previously forced copies.
- Single Pass Instanced vs Multi Pass: 600-1200 us.
- Depthless visor/brine/mobile waterline path: 300-800 us.
- Hardware FFR level 0.85: 900-1800 us fill reduction depending eye texture size.
- Disabled depth-heavy Quest renderer features: 400-900 us.
- Shader stripping: compile/runtime memory pressure reduction; no deterministic per-frame us assigned.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore` executed. Result: failed with 107 unrelated missing-domain/type errors (`Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Audio.Propagation`, `BrineLayerSample`, `MacroSwarm`, `AcousticAup`, etc.).
- Unity C# script validation returned 0 errors for `OculusFfrEnforcer.cs`, `HectonVisorUberPostFeature.cs`, and the structurally repaired `HectonUnderwaterVisuals.cs`; remaining validator warnings are broad heuristics (`+=`/Rigidbody usage), not surfaced compile errors.
- Unity console polling was unavailable on the final retry because the MCP Unity session did not answer ping.
- Task 15 remains `[BLOCKED BY DEPENDENCY]`; overall status remains `PENDING VERIFICATION`.
