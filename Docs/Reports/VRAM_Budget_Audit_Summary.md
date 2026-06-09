# VRAM Budget Audit Summary

Generated: 2026-06-10T00:47:19
Evidence class: STATIC_SOURCE / FILESYSTEM. Runtime residency is PENDING VERIFICATION.
Scan roots: Assets, Packages, Data. Non-import roots such as Docs/AgentLogs are excluded from asset residency totals.

## Summary

- Texture files scanned: 1855
- Mesh files scanned: 301
- RenderTexture assets scanned: 1
- Total BC7 no-mip estimate: 1607.34 MiB
- Total BC7 full-mip estimate: 2143.12 MiB
- Runtime-candidate BC7 full-mip estimate: 2143.12 MiB
- First-party production BC7 full-mip estimate: 1429.63 MiB
- MX350 texture budget: 900 MiB
- Critical overflow trigger: 1228.8 MiB
- [CRITICAL_VRAM_OVERFLOW] All scanned textures exceed 1.2GB static full-mip BC7 threshold.
- [CRITICAL_VRAM_OVERFLOW] Runtime-candidate textures exceed 1.2GB static full-mip BC7 threshold.
- Texture VRAM crime rows: 726
- Texture source-container risk rows: 19
- First-party texture source-container risk rows: 2
- Static mesh geometry estimate: 48.04 MiB / 200 MiB geometry budget
- First-party static mesh geometry estimate: 6.51 MiB
- Mesh single-asset geometry estimate redlines: 1
- Mesh redline/risk rows: 18
- Mesh importer risk rows: 18
- First-party mesh importer risk rows: 16
- Static RenderTexture estimate: 7.03 MiB / 320 MiB RT+Depth budget
- RenderTexture redline/risk rows: 1
- Runtime RenderTexture source hotspots: 65
- First-party large textures with streaming mips off: 137
- link.xml status: LINK_XML_PRESENT_STATIC_ONLY

## Top First-Party Texture Directories

| Directory | Count | BC7 full mip MiB | VRAM crime rows |
|---|---:|---:|---:|
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY) | 12 | 56.00 | 0 |
| Assets/_Project/Art/TEXTURES/Generated/GeminiBiomeMaterialIntake_20260607/SourceCleaned | 10 | 53.33 | 0 |
| Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialIntake_20260607/SourceCleaned | 10 | 53.33 | 0 |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET | 5 | 53.33 | 5 |
| Assets/_Project/Art/TEXTURES | 12 | 52.02 | 3 |
| Assets/_Project/Art/TEXTURES/Sky | 7 | 37.33 | 0 |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT | 3 | 32.00 | 3 |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/blue_metal_plate | 5 | 26.67 | 0 |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/box_profile_metal_sheet | 5 | 26.67 | 0 |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/container_side | 5 | 26.67 | 0 |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/corrugated_iron | 5 | 26.67 | 0 |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/corrugated_iron_02 | 5 | 26.67 | 0 |

## Top Runtime-Candidate Texture Directories

| Directory | Count | BC7 full mip MiB | VRAM crime rows |
|---|---:|---:|---:|
| Assets/ScifiFacility/Textures | 76 | 525.00 | 11 |
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY) | 12 | 56.00 | 0 |
| Assets/_Project/Art/TEXTURES/Generated/GeminiBiomeMaterialIntake_20260607/SourceCleaned | 10 | 53.33 | 0 |
| Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialIntake_20260607/SourceCleaned | 10 | 53.33 | 0 |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET | 5 | 53.33 | 5 |
| Assets/_Project/Art/TEXTURES | 12 | 52.02 | 3 |
| Assets/_Project/Art/TEXTURES/Sky | 7 | 37.33 | 0 |
| Assets/Feel/MMTools/Tools/MMVFX/MMBloomDirt | 4 | 34.28 | 4 |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT | 3 | 32.00 | 3 |
| Assets/Feel/MMTools/Tools/MMPrototypeTextures/Textures/MMProtoTextures | 24 | 32.00 | 0 |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/blue_metal_plate | 5 | 26.67 | 0 |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/box_profile_metal_sheet | 5 | 26.67 | 0 |

## Runtime Texture Extension Pressure

| Extension | Count | BC7 full mip MiB | VRAM crime rows | Container risk rows |
|---|---:|---:|---:|---:|
| .png | 1475 | 1256.65 | 723 | 0 |
| .jpg | 360 | 840.89 | 2 | 0 |
| .tga | 10 | 38.67 | 1 | 10 |
| .hdr | 1 | 2.67 | 0 | 1 |
| .psd | 2 | 1.67 | 0 | 2 |
| .jpeg | 1 | 1.33 | 0 | 0 |
| .gif | 1 | 0.88 | 0 | 1 |
| .exr | 2 | 0.25 | 0 | 2 |
| .tif | 2 | 0.08 | 0 | 2 |
| .bmp | 1 | 0.02 | 0 | 1 |

## Runtime Mesh Extension Pressure

| Extension | Count | Known triangles | Triangle-unreadable rows | Geometry MiB | Flagged rows |
|---|---:|---:|---:|---:|---:|
| .fbx | 300 | 321633 | 0 | 47.85 | 18 |
| .glb | 1 | 1298 | 0 | 0.19 | 0 |

## RenderTexture Static Assets

| Path | Size | Estimate MiB | Color | Depth | AA | Flags |
|---|---:|---:|---:|---:|---:|---|
| Assets/_Project/Art/TEXTURES/RT_HUD_Display.renderTexture | 1280x720 | 7.03 | 8 | 94 | 1 | RENDER_TEXTURE_DEPTH_STENCIL_PRESENT_STATIC_SUSPECT |

## Runtime RenderTexture Source Hotspots

| Path | Line | Pattern | Editor-only | Static evidence |
|---|---:|---|---:|---|
| Assets/_Project/Scripts/Core/HectonXRManager.cs | 14 | RenderTextureDescriptor | false | private static RenderTextureDescriptor _cachedEyeDescriptor; |
| Assets/_Project/Scripts/Core/HectonXRManager.cs | 20 | RenderTextureDescriptor | false | public static RenderTextureDescriptor EyeRenderTextureDescriptor |
| Assets/_Project/Scripts/Core/HectonXRManager.cs | 39 | RenderTextureDescriptor | false | public static RenderTextureDescriptor RefreshEyeDescriptor() |
| Assets/_Project/Scripts/Core/HectonXRManager.cs | 41 | RenderTextureDescriptor | false | RenderTextureDescriptor descriptor = ResolveUnityEyeDescriptor(); |
| Assets/_Project/Scripts/Core/HectonXRManager.cs | 60 | RenderTextureDescriptor | false | private static RenderTextureDescriptor ResolveUnityEyeDescriptor() |
| Assets/_Project/Scripts/Core/HectonXRManager.cs | 64 | RenderTextureDescriptor | false | RenderTextureDescriptor descriptor = XRSettings.eyeTextureDesc; |
| Assets/_Project/Scripts/Core/HectonXRManager.cs | 69 | RenderTextureDescriptor | false | return new RenderTextureDescriptor( |
| Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs | 107 | RenderTextureDescriptor | false | private RenderTextureDescriptor _eyeDescriptorCold; |
| Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs | 541 | RenderTextureDescriptor | false | RenderTextureDescriptor eyeDescriptor = _eyeDescriptorCold; |
| Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs | 821 | RenderTextureDescriptor | false | private void ReportHardwareState(bool applied, float appliedLevel, RenderTextureDescriptor eyeDescriptor) |
| Assets/_Project/Scripts/HectonCelestialEngine.cs | 4429 | new RenderTexture | false | _bakedStarCubemap = new RenderTexture( |
| Assets/_Project/Scripts/HectonCelestialEngine.cs | 4460 | new RenderTexture | false | _atmosphereScatteringLutTexture = new RenderTexture( |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 5110 | RTHandles.Alloc | false | _emptyFluidAdvectionTextureHandle = RTHandles.Alloc(_emptyFluidAdvectionTexture); |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 5123 | RTHandles.Alloc | false | _emptyFluidAdvectionTextureHandle = RTHandles.Alloc(_emptyFluidAdvectionTexture); |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 5163 | RTHandles.Alloc | false | _gpuAbyssalFlowTextureAHandle = RTHandles.Alloc(_gpuAbyssalFlowTextureA); |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 5176 | RTHandles.Alloc | false | _gpuAbyssalFlowTextureBHandle = RTHandles.Alloc(_gpuAbyssalFlowTextureB); |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 5189 | RTHandles.Alloc | false | _cachedFluidAdvectionFlowHandle = RTHandles.Alloc(texture); |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 5207 | RTHandles.Alloc | false | _cachedFluidAdvectionSdfHandle = RTHandles.Alloc(texture); |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 8071 | RTHandles.Alloc | false | _gpuAbyssalFlowTextureAHandle = RTHandles.Alloc(_gpuAbyssalFlowTextureA); |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 8074 | RTHandles.Alloc | false | _gpuAbyssalFlowTextureBHandle = RTHandles.Alloc(_gpuAbyssalFlowTextureB); |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 8134 | RenderTextureDescriptor | false | RenderTextureDescriptor descriptor = new RenderTextureDescriptor( |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 8149 | new RenderTexture | false | RenderTexture texture = new RenderTexture(descriptor) |
| Assets/_Project/Scripts/HectonUnderwaterVisuals.cs | 6009 | RenderTextureDescriptor | false | RenderTextureDescriptor descriptor = new RenderTextureDescriptor(1, 1) |
| Assets/_Project/Scripts/HectonUnderwaterVisuals.cs | 6021 | new RenderTexture | false | _hudFogLuminanceTexture = new RenderTexture(descriptor) |
| Assets/_Project/Scripts/HectonUnderwaterVisuals.cs | 6458 | RenderTextureDescriptor | false | RenderTextureDescriptor descriptor = new RenderTextureDescriptor( |
| Assets/_Project/Scripts/HectonUnderwaterVisuals.cs | 6472 | new RenderTexture | false | RenderTexture texture = new RenderTexture(descriptor) |
| Assets/_Project/Scripts/Optimization/RenderTexturePool.cs | 181 | new RenderTexture | false | RenderTexture newRT = new RenderTexture(safeWidth, safeHeight, safeDepthBits, format); |
| Assets/_Project/Scripts/Rendering/BilateralDrs/HectonBilateralDrsUpscalerFeature.cs | 1057 | RenderTextureDescriptor | false | RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor; |
| Assets/_Project/Scripts/Rendering/BilateralDrs/HectonBilateralDrsUpscalerFeature.cs | 1147 | RenderTextureDescriptor | false | private static bool IsUnsupportedRenderTargetDescriptor(RenderTextureDescriptor descriptor, bool supports2DArrayTextures) |
| Assets/_Project/Scripts/SaveThumbnailCaptureFeature.cs | 136 | RTHandles.Alloc | false | _captureTexture = RTHandles.Alloc( |
| Assets/_Project/Scripts/UI/DiegeticPanelController.cs | 1428 | RenderTextureDescriptor | false | RenderTextureDescriptor descriptor = new RenderTextureDescriptor(requiredResolution.x, requiredResolution.y) |
| Assets/_Project/Scripts/UI/DiegeticPanelController.cs | 1440 | new RenderTexture | false | _panelRenderTexture = new RenderTexture(descriptor) |
| Assets/_Project/Scripts/UI/DiegeticPanelController.cs | 1535 | RenderTextureDescriptor | false | RenderTextureDescriptor descriptor = _panelRenderTexture.descriptor; |
| Assets/_Project/Scripts/UI/DiegeticPanelController.cs | 1584 | RenderTextureDescriptor | false | private static RenderTexture CreatePhosphorTexture(RenderTextureDescriptor descriptor, string textureName) |
| Assets/_Project/Scripts/UI/DiegeticPanelController.cs | 1586 | new RenderTexture | false | RenderTexture texture = new RenderTexture(descriptor) |
| Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs | 1994 | RenderTextureDescriptor | false | RenderTextureDescriptor descriptor = new RenderTextureDescriptor(resolution, resolution, GraphicsFormat.R8G8B8A8_UNorm, 0) |
| Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs | 2005 | new RenderTexture | false | _terminalTextureArray = new RenderTexture(descriptor) |
| Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs | 1675 | new RenderTexture | false | RenderTexture rt = new RenderTexture(math.max(16, width), math.max(16, height), 16, format) |
| Assets/_Project/Scripts/UI/WristHologramHudRuntime_PdaScreenProjector.cs | 681 | RTHandles.Alloc | false | _pdaProjectionAtlasHandle = RTHandles.Alloc(source); // COLD ALLOC: RTHandle[atlas] - cached PDA atlas import handle for RenderGraph declaration - owner: WristHologramHudRuntime |
| Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs | 5065 | new RenderTexture | false | _sonarGlowTexture = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.RInt, RenderTextureReadWrite.Linear) |
| Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs | 5119 | new RenderTexture | false | _fogDensityTexture = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.RInt, RenderTextureReadWrite.Linear) |
| Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamGpuRuntime.cs | 845 | RTHandles.Alloc | false | return RTHandles.Alloc( |
| Assets/_Project/Scripts/Visor/DeferredDecalPass.cs | 380 | RTHandles.Alloc | false | _decalAtlasHandle = RTHandles.Alloc(atlas); |
| Assets/_Project/Scripts/Visor/HectonDryVolumeFeature.cs | 279 | RenderTextureDescriptor | false | public void EnsureTarget(RenderTextureDescriptor descriptor) |
| Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs | 417 | RTHandles.Alloc | false | _historyRead = RTHandles.Alloc( |
| Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs | 428 | RTHandles.Alloc | false | _historyWrite = RTHandles.Alloc( |
| Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs | 470 | RTHandles.Alloc | false | _worldHistoryRead = RTHandles.Alloc( |
| Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs | 481 | RTHandles.Alloc | false | _worldHistoryWrite = RTHandles.Alloc( |
| Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs | 739 | RenderTextureDescriptor | false | RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor; |
| Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs | 680 | RTHandles.Alloc | false | handle = RTHandles.Alloc(texture); |
| Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs | 2362 | RenderTextureDescriptor | false | RenderTextureDescriptor descriptor = new RenderTextureDescriptor(1, 1) |
| Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs | 2372 | new RenderTexture | false | _emptyFogDensityTexture = new RenderTexture(descriptor) |
| Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs | 2417 | RTHandles.Alloc | false | handle = RTHandles.Alloc(texture); |
| Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs | 2465 | RTHandles.Alloc | false | handleA = RTHandles.Alloc(texture); |
| Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs | 2472 | RTHandles.Alloc | false | handleB = RTHandles.Alloc(texture); |
| Assets/_Project/Scripts/Visor/VisorHUDController.cs | 2369 | new RenderTexture | false | RenderTexture rt = new RenderTexture( |
| Assets/_Project/Scripts/Visor/VolumetricLightFeature.cs | 794 | RTHandles.Alloc | false | handle = RTHandles.Alloc(texture); |
| Assets/_Project/Scripts/World/Biolum/HectonBiolumDiffusionVolume.cs | 448 | RenderTextureDescriptor | false | RenderTextureDescriptor descriptor = new RenderTextureDescriptor(resolution, resolution) |
| Assets/_Project/Scripts/World/Biolum/HectonBiolumDiffusionVolume.cs | 461 | new RenderTexture | false | RenderTexture texture = new RenderTexture(descriptor) |
| Assets/_Project/Scripts/World/FloraInteractionManager.cs | 8939 | new RenderTexture | false | RenderTexture texture = new RenderTexture(_wakeTrailRuntimeResolution, _wakeTrailRuntimeResolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) |
| Assets/_Project/Scripts/World/GPUScatterDirector.cs | 1771 | new RenderTexture | false | _depthPyramidTexture = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear) |
| Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs | 3328 | new RenderTexture | false | _depthPyramidTexture = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear) |
| Assets/_Project/Scripts/World/SargassumCrestDampingController.cs | 709 | new RenderTexture | false | texture = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear) |
| Assets/_Project/Scripts/World/SargassumCutManager.cs | 1977 | new RenderTexture | false | RenderTexture texture = new RenderTexture(runtimeResolution, runtimeResolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) |
| Assets/_Project/Scripts/World/SargassumCutManager.cs | 2680 | new RenderTexture | false | RenderTexture texture = new RenderTexture(_maskRuntimeResolution, _maskRuntimeResolution, 0, format, RenderTextureReadWrite.Linear) |
| Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureControlMapBaker.cs | 200 | RenderTextureDescriptor | true | RenderTextureDescriptor descriptor = new RenderTextureDescriptor(resolution, resolution, GraphicsFormat.R8G8B8A8_UNorm, 0) |
| Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureControlMapBaker.cs | 207 | new RenderTexture | true | readbackTexture = new RenderTexture(descriptor) |
| Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureControlMapBaker.cs | 216 | RenderTextureDescriptor | true | RenderTextureDescriptor supersampleDescriptor = descriptor; |
| Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureControlMapBaker.cs | 219 | new RenderTexture | true | drawTexture = new RenderTexture(supersampleDescriptor) |
| Assets/_Project/Scripts/Editor/HectonArtOptimizationTools.cs | 283 | RenderTexture.GetTemporary | true | RenderTexture temp = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); |
| Assets/_Project/Scripts/Editor/HectonArtOptimizationTools.cs | 701 | RenderTexture.GetTemporary | true | RenderTexture temp = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); |
| Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs | 748 | RenderTextureDescriptor | true | RenderTextureDescriptor descriptor = new RenderTextureDescriptor(width, height) |
| Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs | 759 | new RenderTexture | true | RenderTexture texture = new RenderTexture(descriptor) |
| Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs | 770 | RenderTextureDescriptor | true | RenderTextureDescriptor descriptor = new RenderTextureDescriptor(width, height) |
| Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs | 781 | new RenderTexture | true | RenderTexture texture = new RenderTexture(descriptor) |
| Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForgeSupport.cs | 629 | RenderTexture.GetTemporary | true | RenderTexture temp = RenderTexture.GetTemporary(rect.Width, rect.Height, 0, RenderTextureFormat.ARGB32, ResolveReadWrite(linear)); |
| Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForgeSupport.cs | 644 | RenderTexture.GetTemporary | true | RenderTexture temp = RenderTexture.GetTemporary(rect.Width, rect.Height, 0, RenderTextureFormat.ARGB32, ResolveReadWrite(linear)); |
| Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForgeSupport.cs | 683 | RenderTexture.GetTemporary | true | RenderTexture temp = RenderTexture.GetTemporary(atlas.width, atlas.height, 0, RenderTextureFormat.ARGB32, ResolveReadWrite(linear)); |
| Assets/_Project/Scripts/Editor/QuestVrOptimizationValidator1406.cs | 523 | RTHandles.Alloc | true | "RTHandles.Alloc(", |
| Assets/_Project/Scripts/Editor/QuestVrOptimizationValidator1406.cs | 614 | RTHandles.Alloc | true | AssertNotContains(feature, "RTHandles.Alloc(oceanCameraColorTexture);", DryVolumeFeaturePath, "dry volume must not allocate texture wrappers from LateFrameTick"); |

## Top Runtime Texture Costs

| Path | Size | BC7 full mip MiB | Flags |
|---|---:|---:|---|
| Assets/MapMagic/Map_Graph/New Gen/heightmap.png | 4096x4096 | 21.33 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;VRAM CRIME: UNCOMPRESSED_RGBA32_STATIC_SUSPECT;STREAMING_MIPMAPS_OFF_LARGE;READ_WRITE_ENABLED_LARGE_STATIC_SUSPECT |
| Assets/ScifiFacility/Textures/Base_02_dirt_roughness.png | 4096x4096 | 21.33 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/ScifiFacility/Textures/Base_dirt_roughness.png | 4096x4096 | 21.33 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/ScifiFacility/Textures/Base_normal.png | 4096x4096 | 21.33 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/ScifiFacility/Textures/BrushedMetal_dirt_roughness.png | 4096x4096 | 21.33 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/ScifiFacility/Textures/DetailSheet_mask.png | 4096x4096 | 21.33 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/ScifiFacility/Textures/DetailSheet_normal.png | 4096x4096 | 21.33 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/ScifiFacility/Textures/plane_2x2_DefaultMaterial_Normal.png | 4096x4096 | 21.33 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/ScifiFacility/Textures/Transparent_basecolor.png | 4096x4096 | 21.33 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/ScifiFacility/Textures/Transparent_normal.png | 4096x4096 | 21.33 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/Art/Models/Rocks/Rock 7/Materials/2.jpg | 4000x4000 | 20.35 | VRAM CRIME: TEXTURE_GT_2048 |
| Assets/Plugins/Sirenix/Odin Inspector/Assets/Editor/SdfIconAtlas.png | 3072x3072 | 12.00 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;VRAM CRIME: UNCOMPRESSED_RGBA32_STATIC_SUSPECT;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/Aegir_storms.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/clouds.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/clouds0_diff.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/clouds0_diff.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_bump.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_diff.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_norm.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_spec.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/Art/TEXTURES/Aegir_storms.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048 |
| Assets/_Project/Art/TEXTURES/clouds.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048 |
| Assets/_Project/Art/TEXTURES/clouds0_diff.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048 |
| Assets/ScifiFacility/Textures/sphere_basecolor.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/Feel/MMTools/Tools/MMVFX/MMBloomDirt/MMBloomDirt1.png | 3840x2160 | 10.55 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |

## Mesh Redlines

| Path | File MiB | Triangles | Geometry MiB | LOD | Readable | Compression | BlendShapes | Flags |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| Assets/Feel/MMTools/Demos/MMGhostCamera/Models/MMGhostCameraCity.fbx | 2.20 | 127645 | 18.99 | false | 0 | 0 | 1 | MESH_GEOMETRY_ESTIMATE_GT_16MIB_STATIC;MESH_GT_80K_ABSOLUTE_STATIC;MESH_REDLINE_GT_50K_NO_LOD;MESH_COMPRESSION_OFF_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/_Project/Art/Materials/Meshy_AI_Alien_barnacles_clust_0301230506_texture.fbx | 1.90 | 10000 | 1.49 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT |
| Assets/_Project/Art/Models/Rocks/Rock 7/SAMMPLE.fbx | 2.59 | 6519 | 0.97 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT |
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx | 0.23 | 5000 | 0.74 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT |
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Forest_Rock_Shelf_wgpqfjl_Mid.fbx | 0.18 | 4038 | 0.60 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT |
| Assets/_Project/Art/Models/Rocks/Rock 6/rock6/Mossy_Forest_Rock_vimrfjsaw_Mid.fbx | 0.12 | 3539 | 0.53 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT |
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Mossy_Forest_Rock_vimrfjsaw_Mid.fbx | 0.12 | 3539 | 0.53 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT |
| Assets/Shapes/Models/shapes_primitives.fbx | 0.09 | 3222 | 0.48 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT |
| Assets/_Project/Art/Models/Rocks/Rock 5/orig/River_Rock_FBX.fbx | 0.11 | 3054 | 0.45 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT |
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Nordic_Beach_Rock_Formation_vd4iecjva_Low.fbx | 0.08 | 2100 | 0.31 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT |
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Nordic_Beach_Rock_uknoehp_Mid.fbx | 0.05 | 1218 | 0.18 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT |
| Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx | 0.07 | 782 | 0.12 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT |
| Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_c.fbx | 0.07 | 742 | 0.11 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT |
| Assets/_Project/_PROLOGUE_CONTENT/Models/floor_05.fbx | 0.09 | 670 | 0.10 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT |
| Assets/_Project/_PROLOGUE_CONTENT/Models/wall_04_3x6_d.fbx | 0.04 | 586 | 0.09 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT |
| Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_2x3_a.fbx | 0.06 | 530 | 0.08 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT |
| Assets/_Project/_PROLOGUE_CONTENT/Models/ceiling_10.fbx | 0.02 | 108 | 0.02 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT |
| Assets/_Project/_PROLOGUE_CONTENT/Models/floor_large_8x8.fbx | 0.02 | 2 | 0.00 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT |

## Atlas Suggestions

| Group | Count | Combined BC7 MiB | Members |
|---|---:|---:|---|
| Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SplitAtlasCandidates_20260608/decal_atlas/B34-3424 | 36 | 9.00 | TX_B34-3424_island_00.png, TX_B34-3424_island_01.png, TX_B34-3424_island_02.png, TX_B34-3424_island_03.png, TX_B34-3424_island_04.png, TX_B34-3424_island_05.png, TX_B34-3424_island_06.png, TX_B34-3424_island_07.png |
| Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SplitAtlasCandidates_20260608/uv_atlas/B34-3444 | 31 | 7.75 | TX_B34-3444_island_00.png, TX_B34-3444_island_01.png, TX_B34-3444_island_02.png, TX_B34-3444_island_03.png, TX_B34-3444_island_04.png, TX_B34-3444_island_05.png, TX_B34-3444_island_06.png, TX_B34-3444_island_07.png |
| Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SplitAtlasCandidates_20260608/uv_atlas/B34-3440 | 28 | 9.25 | TX_B34-3440_island_00.png, TX_B34-3440_island_01.png, TX_B34-3440_island_02.png, TX_B34-3440_island_03.png, TX_B34-3440_island_04.png, TX_B34-3440_island_05.png, TX_B34-3440_island_06.png, TX_B34-3440_island_07.png |
| Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SplitAtlasCandidates_20260608/uv_atlas/B34-3447 | 28 | 7.00 | TX_B34-3447_island_00.png, TX_B34-3447_island_01.png, TX_B34-3447_island_02.png, TX_B34-3447_island_03.png, TX_B34-3447_island_04.png, TX_B34-3447_island_05.png, TX_B34-3447_island_06.png, TX_B34-3447_island_07.png |
| Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SplitAtlasCandidates_20260608/uv_atlas/B34-3438 | 17 | 8.00 | TX_B34-3438_island_00.png, TX_B34-3438_island_01.png, TX_B34-3438_island_02.png, TX_B34-3438_island_03.png, TX_B34-3438_island_04.png, TX_B34-3438_island_05.png, TX_B34-3438_island_06.png, TX_B34-3438_island_07.png |

## Low-Tier Halving Candidates

| Path | Source | Est. full-mip MiB saved by halving | Rationale |
|---|---:|---:|---|
| Assets/MapMagic/Map_Graph/New Gen/heightmap.png | 4096x4096 | 16.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/ScifiFacility/Textures/Base_02_dirt_roughness.png | 4096x4096 | 16.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/ScifiFacility/Textures/Base_dirt_roughness.png | 4096x4096 | 16.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/ScifiFacility/Textures/Base_normal.png | 4096x4096 | 16.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/ScifiFacility/Textures/BrushedMetal_dirt_roughness.png | 4096x4096 | 16.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/ScifiFacility/Textures/DetailSheet_mask.png | 4096x4096 | 16.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/ScifiFacility/Textures/DetailSheet_normal.png | 4096x4096 | 16.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/ScifiFacility/Textures/plane_2x2_DefaultMaterial_Normal.png | 4096x4096 | 16.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/ScifiFacility/Textures/Transparent_basecolor.png | 4096x4096 | 16.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/ScifiFacility/Textures/Transparent_normal.png | 4096x4096 | 16.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/_Project/Art/Models/Rocks/Rock 7/Materials/2.jpg | 4000x4000 | 15.26 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/Plugins/Sirenix/Odin Inspector/Assets/Editor/SdfIconAtlas.png | 3072x3072 | 9.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/Aegir_storms.png | 4096x2048 | 8.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/clouds.png | 4096x2048 | 8.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/clouds0_diff.png | 4096x2048 | 8.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/clouds0_diff.png | 4096x2048 | 8.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_bump.png | 4096x2048 | 8.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_diff.png | 4096x2048 | 8.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_norm.png | 4096x2048 | 8.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_spec.png | 4096x2048 | 8.00 | Preserve Noir value with mip bias or authored detail normals; do not keep full source on MX350 without hero proof. |

## link.xml Check

- Assets/_Project/Scripts/Global/Generated/link.xml assemblies=4 types=114 preserve_all=117
- Assets/link.xml assemblies=2 types=16 preserve_all=16
- Assets/Plugins/Sirenix/Assemblies/link.xml assemblies=4 types=0 preserve_all=4

## Evidence Boundary

- STATIC_SOURCE: file dimensions, file sizes, source metadata, and parser-readable mesh triangle counts.
- Static geometry estimate assumes 48 byte vertices plus 4 byte indices and no vertex sharing; Unity imported geometry must be verified in Memory Profiler.
- Static RenderTexture estimates use YAML dimensions, MSAA, mip flag, color format, and depth-stencil format; transient and code-created RTs still require Unity runtime capture.
- Runtime RenderTexture source hotspots are static code evidence only; dimensions and residency require Unity profiler capture.
- Scan excludes generated/scratch directories by name: .codex-artifacts, .codex-build, .git, .vs, Build, Builds, Library, Obj, Temp.
- PENDING VERIFICATION: Unity importer compression, actual texture residency, mesh import settings, Memory Profiler VRAM, scene wiring, player-build behavior.
