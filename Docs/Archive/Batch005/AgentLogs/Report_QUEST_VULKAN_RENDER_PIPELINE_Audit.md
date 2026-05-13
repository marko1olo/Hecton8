# QUEST_VULKAN_RENDER_PIPELINE Audit

Status: PENDING VERIFICATION

## Quest URP Asset

- `Assets/_Project/Data/URP_Quest_VR.asset`
- Depth Texture: disabled (`m_RequireDepthTexture: 0`)
- Opaque Texture: disabled (`m_RequireOpaqueTexture: 0`)
- HDR: disabled (`m_SupportsHDR: 0`)
- MSAA: 4x (`m_MSAA: 4`)
- Render scale: 1.0
- Store actions: Discard (`m_StoreActionsOptimization: 1`)
- XR/native render pass prefilter: enabled

## Quest Renderer

- `Assets/_Project/Data/Quest_VR_Renderer.asset`
- Native render pass: enabled
- Depth priming: disabled
- Copy depth: disabled
- Depth-heavy copied features disabled:
  - `HectonScooterVolumetricShaftsFeature`
  - `HectonHalfResParticlesFeature`
  - `HectonAbyssalSsdoFeature`
  - `HectonNoirDepthFogFeature`
- Kept active:
  - `HectonVRBrownoutFeature`
  - `ShapesRenderFeature`
  - `HectonFluidAdvectionRenderFeature`
  - `HectonVisorUberPostFeature`
  - `HectonAtmosphereSootFeature`

## Stereo

- `PlayerSettings.stereoRenderingPath`: `Instancing`
- `HectonVisorUberPost.shader` has `UNITY_VERTEX_OUTPUT_STEREO`, `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO`, and calls `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input)` in fragment.

## Depth Resolve

- Mobile shader variants compile out `DeclareDepthTexture.hlsl` and depth-sampling light shafts/brine fog via `#if !defined(SHADER_API_MOBILE)`.
- Internal flood waterline uses `Camera.WorldToViewportPoint` against an absolute Y-plane sample, not `_CameraDepthTexture`.
- Remaining `SampleSceneDepth` text in `HectonVisorUberPost.shader` is inside the non-mobile branch and remains for PC/desktop quality.

## RenderTexture Recon

Manual `RenderTexture`/`Camera.targetTexture` allocations that need Quest clamp review before release:

- `Assets/_Project/Scripts/UI/DiegeticPanelController.cs:1096` panel RT descriptor from `requiredResolution`.
- `Assets/_Project/Scripts/UI/DiegeticPanelController.cs:1117` assigns `panelCamera.targetTexture`.
- `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:212` default UI RT width is `1024`; exterior feed path must stay <=1024 on Quest.
- `Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs:994` allocates cockpit `RenderTexture` from caller width/height.
- `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:1687` sonar glow RT from `targetWidth/targetHeight`.
- `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:1735` fog density RT from `targetWidth/targetHeight`.
- `Assets/_Project/Scripts/World/Biolum/HectonBiolumDiffusionVolume.cs:295` square diffusion descriptor from `resolution`.
- `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs:1906` depth pyramid RT; size logic counts 1024/2048/4096 thresholds.
- `Assets/_Project/Scripts/World/GPUScatterDirector.cs:1379` depth pyramid RT; size logic counts 1024/2048/4096 thresholds.
- `Assets/_Project/Scripts/World/SargassumCutManager.cs:995` damage volume RT; existing low cap returns min(maskResolution, 1024).
- `Assets/_Project/Scripts/HectonCelestialEngine.cs:1031` firmament MX350 cap is 2048; Quest should use <=1024 unless visually proven.

## HLOD Vulkan Compute

- `Assets/_Project/Art/Shaders/InstanceCulling.compute` uses `#pragma require compute`.
- It uses `AppendStructuredBuffer<float4x4>` and C# `GraphicsBuffer.CopyCount`; this is Unity Vulkan-compatible.
- No `only_renderers d3d*`, `exclude_renderers vulkan`, `RWByteAddressBuffer`, `globallycoherent`, wave intrinsics, or DirectX-only syntax found in `InstanceCulling.compute`.
