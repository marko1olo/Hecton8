# VRAM Budget Audit Summary

Generated: 2026-05-15T23:53:10
Evidence class: STATIC_SOURCE / FILESYSTEM. Runtime residency is PENDING VERIFICATION.
Scan roots: Assets, Packages, Data. Non-import roots such as Docs/AgentLogs are excluded from asset residency totals.

## Summary

- Texture files scanned: 1652
- Mesh files scanned: 302
- RenderTexture assets scanned: 1
- Total BC7 no-mip estimate: 973.99 MiB
- Total BC7 full-mip estimate: 1298.65 MiB
- Runtime-candidate BC7 full-mip estimate: 1298.65 MiB
- First-party production BC7 full-mip estimate: 505.62 MiB
- MX350 texture budget: 900 MiB
- Critical overflow trigger: 1228.8 MiB
- [CRITICAL_VRAM_OVERFLOW] All scanned textures exceed 1.2GB static full-mip BC7 threshold.
- [CRITICAL_VRAM_OVERFLOW] Runtime-candidate textures exceed 1.2GB static full-mip BC7 threshold.
- Texture VRAM crime rows: 801
- Texture source-container risk rows: 23
- First-party texture source-container risk rows: 2
- Static mesh geometry estimate: 48.05 MiB / 200 MiB geometry budget
- First-party static mesh geometry estimate: 6.51 MiB
- Mesh single-asset geometry estimate redlines: 1
- Mesh redline/risk rows: 293
- Mesh importer risk rows: 293
- First-party mesh importer risk rows: 16
- Static RenderTexture estimate: 7.03 MiB / 320 MiB RT+Depth budget
- RenderTexture redline/risk rows: 1
- Runtime RenderTexture source hotspots: 53
- First-party large textures with streaming mips off: 50
- link.xml status: LINK_XML_PRESENT_STATIC_ONLY

## Top First-Party Texture Directories

| Directory | Count | BC7 full mip MiB | VRAM crime rows |
|---|---:|---:|---:|
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY) | 12 | 56.00 | 0 |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET | 5 | 53.33 | 5 |
| Assets/_Project/Art/TEXTURES | 11 | 50.69 | 3 |
| Assets/_Project/Art/TEXTURES/Sky | 7 | 37.33 | 0 |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT | 3 | 32.00 | 3 |
| Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.branching | 4 | 21.33 | 0 |
| Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.low | 4 | 21.33 | 0 |
| Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.abyssal | 4 | 21.33 | 0 |
| Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.canopy | 4 | 21.33 | 0 |
| Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.patch.dense | 4 | 21.33 | 0 |
| Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.tall | 4 | 21.33 | 0 |
| Assets/_Project/Art/Models/Rocks/Rock 7/Materials | 1 | 20.35 | 1 |

## Top Runtime-Candidate Texture Directories

| Directory | Count | BC7 full mip MiB | VRAM crime rows |
|---|---:|---:|---:|
| Assets/ScifiFacility/Textures | 76 | 525.00 | 11 |
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY) | 12 | 56.00 | 0 |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET | 5 | 53.33 | 5 |
| Assets/_Project/Art/TEXTURES | 11 | 50.69 | 3 |
| Assets/Screenshots | 100 | 43.62 | 0 |
| Assets/_Project/Art/TEXTURES/Sky | 7 | 37.33 | 0 |
| Assets/Feel/MMTools/Tools/MMVFX/MMBloomDirt | 4 | 34.28 | 4 |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT | 3 | 32.00 | 3 |
| Assets/Feel/MMTools/Tools/MMPrototypeTextures/Textures/MMProtoTextures | 24 | 32.00 | 0 |
| Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.branching | 4 | 21.33 | 0 |
| Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.low | 4 | 21.33 | 0 |
| Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.abyssal | 4 | 21.33 | 0 |

## Runtime Texture Extension Pressure

| Extension | Count | BC7 full mip MiB | VRAM crime rows | Container risk rows |
|---|---:|---:|---:|---:|
| .png | 1592 | 1132.12 | 799 | 0 |
| .jpg | 37 | 119.12 | 1 | 0 |
| .tga | 10 | 38.67 | 1 | 10 |
| .hdr | 2 | 5.33 | 0 | 2 |
| .psd | 5 | 2.17 | 0 | 5 |
| .gif | 1 | 0.88 | 0 | 1 |
| .exr | 2 | 0.25 | 0 | 2 |
| .tif | 2 | 0.08 | 0 | 2 |
| .bmp | 1 | 0.02 | 0 | 1 |

## Runtime Mesh Extension Pressure

| Extension | Count | Known triangles | Triangle-unreadable rows | Geometry MiB | Flagged rows |
|---|---:|---:|---:|---:|---:|
| .fbx | 300 | 321633 | 0 | 47.85 | 292 |
| .glb | 1 | 1298 | 0 | 0.19 | 0 |
| .obj | 1 | 12 | 0 | 0.00 | 1 |

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
| Assets/_Project/Scripts/Core/OculusFfrEnforcer.cs | 290 | RenderTextureDescriptor | false | RenderTextureDescriptor eyeDescriptor = HectonXRManager.RefreshEyeDescriptor(); |
| Assets/_Project/Scripts/Graphics/Caustics/AnalyticalCausticsService.cs | 334 | RenderTextureDescriptor | false | RenderTextureDescriptor descriptor = new RenderTextureDescriptor(CausticsResolution, CausticsResolution, GraphicsFormat.R8G8B8A8_UNorm, 0) |
| Assets/_Project/Scripts/Graphics/Caustics/AnalyticalCausticsService.cs | 342 | new RenderTexture | false | _causticsMap = new RenderTexture(descriptor) |
| Assets/_Project/Scripts/HectonCelestialEngine.cs | 3057 | new RenderTexture | false | _bakedStarCubemap = new RenderTexture( |
| Assets/_Project/Scripts/HectonCelestialEngine.cs | 3088 | new RenderTexture | false | _atmosphereScatteringLutTexture = new RenderTexture( |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 3839 | RTHandles.Alloc | false | _emptyFluidAdvectionTextureHandle = RTHandles.Alloc(_emptyFluidAdvectionTexture); |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 3852 | RTHandles.Alloc | false | _emptyFluidAdvectionTextureHandle = RTHandles.Alloc(_emptyFluidAdvectionTexture); |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 3863 | RTHandles.Alloc | false | _gpuAbyssalFlowTextureAHandle = RTHandles.Alloc(_gpuAbyssalFlowTextureA); |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 3870 | RTHandles.Alloc | false | _gpuAbyssalFlowTextureBHandle = RTHandles.Alloc(_gpuAbyssalFlowTextureB); |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 3878 | RTHandles.Alloc | false | _cachedFluidAdvectionFlowHandle = RTHandles.Alloc(texture); |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 3893 | RTHandles.Alloc | false | _cachedFluidAdvectionSdfHandle = RTHandles.Alloc(texture); |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 5746 | RTHandles.Alloc | false | _gpuAbyssalFlowTextureAHandle = RTHandles.Alloc(_gpuAbyssalFlowTextureA); |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 5749 | RTHandles.Alloc | false | _gpuAbyssalFlowTextureBHandle = RTHandles.Alloc(_gpuAbyssalFlowTextureB); |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 5772 | RenderTextureDescriptor | false | RenderTextureDescriptor descriptor = new RenderTextureDescriptor( |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 5787 | new RenderTexture | false | RenderTexture texture = new RenderTexture(descriptor) |
| Assets/_Project/Scripts/HectonUnderwaterVisuals.cs | 5464 | RenderTextureDescriptor | false | RenderTextureDescriptor descriptor = new RenderTextureDescriptor(1, 1) |
| Assets/_Project/Scripts/HectonUnderwaterVisuals.cs | 5476 | new RenderTexture | false | _hudFogLuminanceTexture = new RenderTexture(descriptor) |
| Assets/_Project/Scripts/HectonUnderwaterVisuals.cs | 5670 | RenderTextureDescriptor | false | RenderTextureDescriptor descriptor = new RenderTextureDescriptor( |
| Assets/_Project/Scripts/HectonUnderwaterVisuals.cs | 5684 | new RenderTexture | false | RenderTexture texture = new RenderTexture(descriptor) |
| Assets/_Project/Scripts/Optimization/RenderTexturePool.cs | 159 | new RenderTexture | false | RenderTexture newRT = new RenderTexture(safeWidth, safeHeight, safeDepthBits, format); |
| Assets/_Project/Scripts/SaveThumbnailCaptureFeature.cs | 119 | RTHandles.Alloc | false | _captureTexture = RTHandles.Alloc( |
| Assets/_Project/Scripts/UI/DiegeticPanelController.cs | 1215 | RenderTextureDescriptor | false | RenderTextureDescriptor descriptor = new RenderTextureDescriptor(requiredResolution.x, requiredResolution.y) |
| Assets/_Project/Scripts/UI/DiegeticPanelController.cs | 1229 | new RenderTexture | false | _panelRenderTexture = new RenderTexture(descriptor) |
| Assets/_Project/Scripts/UI/DiegeticPanelController.cs | 1321 | RenderTextureDescriptor | false | RenderTextureDescriptor descriptor = _panelRenderTexture.descriptor; |
| Assets/_Project/Scripts/UI/DiegeticPanelController.cs | 1367 | RenderTextureDescriptor | false | private static RenderTexture CreatePhosphorTexture(RenderTextureDescriptor descriptor, string textureName) |
| Assets/_Project/Scripts/UI/DiegeticPanelController.cs | 1369 | new RenderTexture | false | RenderTexture texture = new RenderTexture(descriptor) |
| Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs | 1002 | new RenderTexture | false | RenderTexture rt = new RenderTexture(math.max(16, width), math.max(16, height), 16, format) |
| Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs | 1696 | new RenderTexture | false | _sonarGlowTexture = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.RInt, RenderTextureReadWrite.Linear) |
| Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs | 1744 | new RenderTexture | false | _fogDensityTexture = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.RInt, RenderTextureReadWrite.Linear) |
| Assets/_Project/Scripts/Visor/HectonBiolumSSGIFeature.cs | 267 | RTHandles.Alloc | false | _gatherTexture = RTHandles.Alloc( |
| Assets/_Project/Scripts/Visor/HectonBiolumSSGIFeature.cs | 278 | RTHandles.Alloc | false | _giTexture = RTHandles.Alloc( |
| Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs | 226 | RTHandles.Alloc | false | _historyRead = RTHandles.Alloc( |
| Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs | 237 | RTHandles.Alloc | false | _historyWrite = RTHandles.Alloc( |
| Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs | 271 | RTHandles.Alloc | false | _worldHistoryRead = RTHandles.Alloc( |
| Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs | 282 | RTHandles.Alloc | false | _worldHistoryWrite = RTHandles.Alloc( |
| Assets/_Project/Scripts/Visor/HectonVoxelSsaoFeature.cs | 181 | RTHandles.Alloc | false | _aoTexture = RTHandles.Alloc( |
| Assets/_Project/Scripts/Visor/VolumetricLightFeature.cs | 422 | RTHandles.Alloc | false | _halfTexture = RTHandles.Alloc( |
| Assets/_Project/Scripts/Visor/VolumetricLightFeature.cs | 435 | RTHandles.Alloc | false | _compositeTexture = RTHandles.Alloc( |
| Assets/_Project/Scripts/World/Biolum/HectonBiolumDiffusionVolume.cs | 295 | RenderTextureDescriptor | false | RenderTextureDescriptor descriptor = new RenderTextureDescriptor(resolution, resolution) |
| Assets/_Project/Scripts/World/Biolum/HectonBiolumDiffusionVolume.cs | 308 | new RenderTexture | false | RenderTexture texture = new RenderTexture(descriptor) |
| Assets/_Project/Scripts/World/FloraInteractionManager.cs | 5258 | new RenderTexture | false | RenderTexture texture = new RenderTexture(_wakeTrailRuntimeResolution, _wakeTrailRuntimeResolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) |
| Assets/_Project/Scripts/World/GPUScatterDirector.cs | 1379 | new RenderTexture | false | _depthPyramidTexture = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear) |
| Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs | 1906 | new RenderTexture | false | _depthPyramidTexture = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear) |
| Assets/_Project/Scripts/World/SargassumCrestDampingController.cs | 350 | new RenderTexture | false | texture = new RenderTexture(width, height, 0, format, RenderTextureReadWrite.Linear) |
| Assets/_Project/Scripts/World/SargassumCutManager.cs | 995 | new RenderTexture | false | RenderTexture texture = new RenderTexture(damageVolumeResolution, damageVolumeResolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) |
| Assets/_Project/Scripts/World/SargassumCutManager.cs | 1329 | new RenderTexture | false | RenderTexture texture = new RenderTexture(_maskRuntimeResolution, _maskRuntimeResolution, 0, format, RenderTextureReadWrite.Linear) |
| Assets/_Project/Scripts/Editor/HectonArtOptimizationTools.cs | 283 | RenderTexture.GetTemporary | true | RenderTexture temp = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); |
| Assets/_Project/Scripts/Editor/HectonArtOptimizationTools.cs | 664 | RenderTexture.GetTemporary | true | RenderTexture temp = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); |
| Assets/_Project/Scripts/Editor/HectonMaskChannelPacker.cs | 130 | RenderTexture.GetTemporary | true | RenderTexture temp = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); |
| Assets/_Project/Scripts/Editor/HectonMaterialChannelPackValidator.cs | 345 | RenderTexture.GetTemporary | true | RenderTexture tempRt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); |
| Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs | 92 | RenderTexture.GetTemporary | true | tileRt = RenderTexture.GetTemporary(TileWidth, TileHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default); |
| Assets/_Project/Scripts/Optimization/Editor/RenderTextureFormatOptimizer.cs | 140 | RenderTexture.GetTemporary | true | var tempRT = RenderTexture.GetTemporary(rt.width, rt.height, 0, newFormat); |
| Assets/_Project/Scripts/Optimization/Editor/RenderTextureFormatOptimizer.cs | 199 | RenderTexture.GetTemporary | true | var tempRT = RenderTexture.GetTemporary(rt.width, rt.height, 0, newFormat); |
| Assets/_Project/Scripts/Optimization/Editor/RenderTextureResolutionAnalyzer.cs | 147 | RenderTexture.GetTemporary | true | var tempRT = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32); |

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
| Assets/_Project/Art/TEXTURES/Aegir_storms.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/Art/TEXTURES/clouds.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/_Project/Art/TEXTURES/clouds0_diff.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/ScifiFacility/Textures/sphere_basecolor.png | 4096x2048 | 10.67 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |
| Assets/Feel/MMTools/Tools/MMVFX/MMBloomDirt/MMBloomDirt1.png | 3840x2160 | 10.55 | VRAM CRIME: TEXTURE_GT_2048;VRAM CRIME: IMPORT_MAX_GT_2048;STREAMING_MIPMAPS_OFF_LARGE |

## Mesh Redlines

| Path | File MiB | Triangles | Geometry MiB | LOD | Readable | Compression | BlendShapes | Flags |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| Assets/Feel/MMTools/Demos/MMGhostCamera/Models/MMGhostCameraCity.fbx | 2.20 | 127645 | 18.99 | false | 0 | 0 | 1 | MESH_GEOMETRY_ESTIMATE_GT_16MIB_STATIC;MESH_GT_80K_ABSOLUTE_STATIC;MESH_REDLINE_GT_50K_NO_LOD;MESH_COMPRESSION_OFF_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/structural/walls/viewing_deck.fbx | 0.45 | 12778 | 1.90 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/_Project/Art/Materials/Meshy_AI_Alien_barnacles_clust_0301230506_texture.fbx | 1.90 | 10000 | 1.49 | false | 0 | 0 | 1 | MESH_COMPRESSION_OFF_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/details/technical/detail_04_b.fbx | 0.75 | 7377 | 1.10 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/_Project/Art/Models/Rocks/Rock 7/SAMMPLE.fbx | 2.59 | 6519 | 0.97 | false | 0 | 0 | 1 | MESH_COMPRESSION_OFF_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/structural/walls/wall_01_4x3_h.fbx | 0.29 | 5388 | 0.80 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/prop_14.fbx | 0.22 | 5189 | 0.77 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx | 0.23 | 5000 | 0.74 | false | 0 | 0 | 1 | MESH_COMPRESSION_OFF_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Forest_Rock_Shelf_wgpqfjl_Mid.fbx | 0.18 | 4038 | 0.60 | false | 0 | 0 | 1 | MESH_COMPRESSION_OFF_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/prop_10_base.fbx | 0.11 | 3952 | 0.59 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/structural/walls/wall_01_6x3_door.fbx | 0.19 | 3540 | 0.53 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/_Project/Art/Models/Rocks/Rock 6/rock6/Mossy_Forest_Rock_vimrfjsaw_Mid.fbx | 0.12 | 3539 | 0.53 | false | 0 | 0 | 1 | MESH_COMPRESSION_OFF_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Mossy_Forest_Rock_vimrfjsaw_Mid.fbx | 0.12 | 3539 | 0.53 | false | 0 | 0 | 1 | MESH_COMPRESSION_OFF_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/structural/walls/wall_01_6x3_door_b.fbx | 0.18 | 3468 | 0.52 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/Shapes/Models/shapes_primitives.fbx | 0.09 | 3222 | 0.48 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/prop_11.fbx | 0.11 | 3090 | 0.46 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/_Project/Art/Models/Rocks/Rock 5/orig/River_Rock_FBX.fbx | 0.11 | 3054 | 0.45 | false | 0 | 0 | 1 | MESH_COMPRESSION_OFF_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/prop_15.fbx | 0.18 | 2999 | 0.45 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/details/technical/detail_02.fbx | 0.11 | 2688 | 0.40 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/furniture/chair_01.fbx | 0.15 | 2548 | 0.38 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/furniture/bed_02.fbx | 0.13 | 2243 | 0.33 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/furniture/chair_02.fbx | 0.14 | 2234 | 0.33 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/structural/rails+scaffolds+stairs/walk_02.fbx | 0.09 | 2228 | 0.33 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/structural/rails+scaffolds+stairs/stairs_01.fbx | 0.11 | 2176 | 0.32 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Nordic_Beach_Rock_Formation_vd4iecjva_Low.fbx | 0.08 | 2100 | 0.31 | false | 0 | 0 | 1 | MESH_COMPRESSION_OFF_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/structural/walls/column_03.fbx | 0.11 | 2000 | 0.30 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/prop_13.fbx | 0.09 | 1992 | 0.30 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/details/technical/detail_03_b.fbx | 0.15 | 1964 | 0.29 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/prop_11_base.fbx | 0.09 | 1920 | 0.29 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/structural/rails+scaffolds+stairs/walk_01.fbx | 0.09 | 1880 | 0.28 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/prop_10.fbx | 0.09 | 1858 | 0.28 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/details/technical/detail_04_c.fbx | 0.15 | 1788 | 0.27 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/structural/walls/column_04.fbx | 0.09 | 1594 | 0.24 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/prop_05.fbx | 0.22 | 1582 | 0.24 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/structural/walls/column_01.fbx | 0.33 | 1535 | 0.23 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/details/technical/detail_04_d.fbx | 0.09 | 1518 | 0.23 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/structural/walls/wall_01_4x3_door.fbx | 0.25 | 1407 | 0.21 | false | 1 | 2 | 0 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/tubes/tube_03.fbx | 0.12 | 1337 | 0.20 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/keyboard_b.fbx | 0.05 | 1314 | 0.20 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |
| Assets/ScifiFacility/Models/props/keyboard.fbx | 0.05 | 1314 | 0.20 | false | 1 | 2 | 1 | MESH_READ_WRITE_ENABLED_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT |

## Atlas Suggestions

| Group | Count | Combined BC7 MiB | Members |
|---|---:|---:|---|
| Assets/_Project/Art/TEXTURES/Detali | 7 | 7.00 | bubble vent atlas - bad - redo.png, mineral seep mask - looks seamless.png, Mineral Seep Mask - second try.png, Soft Plume Noise - second try.png, soft_plume_noise_-_kakoy_to_seryy_nu_norm.png, visor droplet mask.png, visor runoff normal.png |
| Assets/_Project/Art/Sprites/ui | 6 | 6.00 | BATTERY.png, COPPER.png, CUTTER.png, MICRO.png, OXYGEN.png, TITANIUM.png |
| Assets/_Project/Art/TEXTURES | 4 | 4.00 | FLOOR.png, FLOOR1.png, ORGANIC.png, terrain.png |
| Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.branching.v2 | 4 | 4.00 | albedo___family.coral.branching.v2.png, detail___family.coral.branching.v2.png, mask___family.coral.branching.v2.png, normal___family.coral.branching.v2.png |
| Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.brittle | 4 | 4.00 | albedo___family.coral.brittle.png, detail___family.coral.brittle.png, mask___family.coral.brittle.png, normal___family.coral.brittle.png |

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

- Assets/AstarPathfindingProject/link.xml assemblies=1 types=18 preserve_all=18
- Assets/link.xml assemblies=2 types=10 preserve_all=10
- Assets/Plugins/Sirenix/Assemblies/link.xml assemblies=4 types=0 preserve_all=4

## Evidence Boundary

- STATIC_SOURCE: file dimensions, file sizes, source metadata, and parser-readable mesh triangle counts.
- Static geometry estimate assumes 48 byte vertices plus 4 byte indices and no vertex sharing; Unity imported geometry must be verified in Memory Profiler.
- Static RenderTexture estimates use YAML dimensions, MSAA, mip flag, color format, and depth-stencil format; transient and code-created RTs still require Unity runtime capture.
- Runtime RenderTexture source hotspots are static code evidence only; dimensions and residency require Unity profiler capture.
- Scan excludes generated/scratch directories by name: .codex-artifacts, .codex-build, .git, .vs, Build, Builds, Library, Obj, Temp.
- PENDING VERIFICATION: Unity importer compression, actual texture residency, mesh import settings, Memory Profiler VRAM, scene wiring, player-build behavior.
