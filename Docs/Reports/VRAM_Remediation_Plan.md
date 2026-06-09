# VRAM Remediation Plan

Generated: 2026-06-10T00:47:21
Evidence class: STATIC_SOURCE / FILESYSTEM / PY_UNIT_TEST. No asset/import mutation performed.

## Gate Status

- Runtime-candidate full-mip BC7: 2143.12 MiB
- First-party production full-mip BC7: 1429.63 MiB
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
- CI behavior: `python Tools/MemoryBudgetCheck.py --root . --ci` must fail until redlines are resolved or explicitly suppressed by future policy.

## Priority 1 - Quarantine Non-Production Runtime Payloads

| Directory | Count | BC7 full mip MiB | VRAM crime rows | Required action |
|---|---:|---:|---:|---|
| Assets/ScifiFacility/Textures | 76 | 525.00 | 11 | Prove production use, move to editor-only/demo quarantine, or exclude from Addressables/build payload. |
| Assets/Feel/MMTools/Tools/MMVFX/MMBloomDirt | 4 | 34.28 | 4 | Prove production use, move to editor-only/demo quarantine, or exclude from Addressables/build payload. |
| Assets/Feel/MMTools/Tools/MMPrototypeTextures/Textures/MMProtoTextures | 24 | 32.00 | 0 | Prove production use, move to editor-only/demo quarantine, or exclude from Addressables/build payload. |
| Assets/MapMagic/Map_Graph/New Gen | 1 | 21.33 | 1 | Prove production use, move to editor-only/demo quarantine, or exclude from Addressables/build payload. |
| Assets/Feel/MMTools/Tools/MMPrototypeTextures/Textures/MMPlastic | 3 | 16.00 | 0 | Prove production use, move to editor-only/demo quarantine, or exclude from Addressables/build payload. |
| Packages/com.unity.shadergraph/GraphTemplates/Cross Pipeline | 7 | 15.23 | 2 | Prove production use, move to editor-only/demo quarantine, or exclude from Addressables/build payload. |
| Assets/Plugins/Sirenix/Odin Inspector/Assets/Editor | 1 | 12.00 | 1 | Prove production use, move to editor-only/demo quarantine, or exclude from Addressables/build payload. |
| Assets/Feel/MMTools/Tools/MMVFX/MMNoise | 13 | 9.33 | 0 | Prove production use, move to editor-only/demo quarantine, or exclude from Addressables/build payload. |
| Assets/Feel/MMTools/Demos/MMTween/Textures | 1 | 5.33 | 1 | Prove production use, move to editor-only/demo quarantine, or exclude from Addressables/build payload. |
| Assets/Feel/MMTools/Tools/MMVFX/MMParticles | 16 | 5.33 | 0 | Prove production use, move to editor-only/demo quarantine, or exclude from Addressables/build payload. |
| Assets/Screenshots | 6 | 4.42 | 0 | Prove production use, move to editor-only/demo quarantine, or exclude from Addressables/build payload. |
| Assets/Feel/MMTools/Tools/MMSceneLoading/Sprites | 4 | 4.33 | 0 | Prove production use, move to editor-only/demo quarantine, or exclude from Addressables/build payload. |

## Priority 2 - Convert Risky Texture Source Containers

| Extension | Runtime count | BC7 full mip MiB | VRAM crime rows | Container risk rows | Required action |
|---|---:|---:|---:|---:|---|
| .tga | 10 | 38.67 | 1 | 10 | Convert/quarantine source container or prove importer compression and residency. |
| .hdr | 1 | 2.67 | 0 | 1 | Convert/quarantine source container or prove importer compression and residency. |
| .psd | 2 | 1.67 | 0 | 2 | Convert/quarantine source container or prove importer compression and residency. |
| .gif | 1 | 0.88 | 0 | 1 | Convert/quarantine source container or prove importer compression and residency. |
| .exr | 2 | 0.25 | 0 | 2 | Convert/quarantine source container or prove importer compression and residency. |
| .tif | 2 | 0.08 | 0 | 2 | Convert/quarantine source container or prove importer compression and residency. |
| .bmp | 1 | 0.02 | 0 | 1 | Convert/quarantine source container or prove importer compression and residency. |

## Priority 3 - RenderTexture Static Assets

| Path | Size | Estimate MiB | Color | Depth | AA | Flags | Required action |
|---|---:|---:|---:|---:|---:|---|---|
| Assets/_Project/Art/TEXTURES/RT_HUD_Display.renderTexture | 1280x720 | 7.03 | 8 | 94 | 1 | RENDER_TEXTURE_DEPTH_STENCIL_PRESENT_STATIC_SUSPECT | Verify RT necessity in Unity; remove depth/MSAA/mips/random-write unless required and keep RT+Depth under 320 MiB. |

## Priority 4 - Runtime RenderTexture Source Hotspots

| Path | Line | Pattern | Editor-only | Required action |
|---|---:|---|---:|---|
| Assets/_Project/Scripts/Core/HectonXRManager.cs | 14 | RenderTextureDescriptor | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Core/HectonXRManager.cs | 20 | RenderTextureDescriptor | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Core/HectonXRManager.cs | 39 | RenderTextureDescriptor | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Core/HectonXRManager.cs | 41 | RenderTextureDescriptor | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Core/HectonXRManager.cs | 60 | RenderTextureDescriptor | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Core/HectonXRManager.cs | 64 | RenderTextureDescriptor | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Core/HectonXRManager.cs | 69 | RenderTextureDescriptor | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs | 107 | RenderTextureDescriptor | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs | 541 | RenderTextureDescriptor | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Graphics/VR/FoveatedRenderCommander.cs | 821 | RenderTextureDescriptor | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/HectonCelestialEngine.cs | 4429 | new RenderTexture | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/HectonCelestialEngine.cs | 4460 | new RenderTexture | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 5110 | RTHandles.Alloc | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 5123 | RTHandles.Alloc | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 5163 | RTHandles.Alloc | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 5176 | RTHandles.Alloc | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 5189 | RTHandles.Alloc | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 5207 | RTHandles.Alloc | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 8071 | RTHandles.Alloc | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 8074 | RTHandles.Alloc | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 8134 | RenderTextureDescriptor | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/HectonFluidEngine.cs | 8149 | new RenderTexture | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/HectonUnderwaterVisuals.cs | 6009 | RenderTextureDescriptor | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/HectonUnderwaterVisuals.cs | 6021 | new RenderTexture | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/HectonUnderwaterVisuals.cs | 6458 | RenderTextureDescriptor | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/HectonUnderwaterVisuals.cs | 6472 | new RenderTexture | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Optimization/RenderTexturePool.cs | 181 | new RenderTexture | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Rendering/BilateralDrs/HectonBilateralDrsUpscalerFeature.cs | 1057 | RenderTextureDescriptor | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Rendering/BilateralDrs/HectonBilateralDrsUpscalerFeature.cs | 1147 | RenderTextureDescriptor | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/SaveThumbnailCaptureFeature.cs | 136 | RTHandles.Alloc | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/UI/DiegeticPanelController.cs | 1428 | RenderTextureDescriptor | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/UI/DiegeticPanelController.cs | 1440 | new RenderTexture | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/UI/DiegeticPanelController.cs | 1535 | RenderTextureDescriptor | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/UI/DiegeticPanelController.cs | 1584 | RenderTextureDescriptor | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/UI/DiegeticPanelController.cs | 1586 | new RenderTexture | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs | 1994 | RenderTextureDescriptor | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs | 2005 | new RenderTexture | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/UI/VehicleSubOsCockpitRuntime.cs | 1675 | new RenderTexture | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/UI/WristHologramHudRuntime_PdaScreenProjector.cs | 681 | RTHandles.Alloc | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs | 5065 | new RenderTexture | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs | 5119 | new RenderTexture | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamGpuRuntime.cs | 845 | RTHandles.Alloc | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Visor/DeferredDecalPass.cs | 380 | RTHandles.Alloc | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Visor/HectonDryVolumeFeature.cs | 279 | RenderTextureDescriptor | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs | 417 | RTHandles.Alloc | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs | 428 | RTHandles.Alloc | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs | 470 | RTHandles.Alloc | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs | 481 | RTHandles.Alloc | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Visor/HectonSonarPointCloudFeature.cs | 739 | RenderTextureDescriptor | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs | 680 | RTHandles.Alloc | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs | 2362 | RenderTextureDescriptor | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs | 2372 | new RenderTexture | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs | 2417 | RTHandles.Alloc | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs | 2465 | RTHandles.Alloc | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs | 2472 | RTHandles.Alloc | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Visor/VisorHUDController.cs | 2369 | new RenderTexture | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Visor/VolumetricLightFeature.cs | 794 | RTHandles.Alloc | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/World/Biolum/HectonBiolumDiffusionVolume.cs | 448 | RenderTextureDescriptor | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/World/Biolum/HectonBiolumDiffusionVolume.cs | 461 | new RenderTexture | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/World/FloraInteractionManager.cs | 8939 | new RenderTexture | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/World/GPUScatterDirector.cs | 1771 | new RenderTexture | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs | 3328 | new RenderTexture | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/World/SargassumCrestDampingController.cs | 709 | new RenderTexture | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/World/SargassumCutManager.cs | 1977 | new RenderTexture | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/World/SargassumCutManager.cs | 2680 | new RenderTexture | false | Profiler capture and lifecycle proof required; static scan cannot estimate dynamic dimensions safely. |
| Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureControlMapBaker.cs | 200 | RenderTextureDescriptor | true | Editor-only; keep out of player build and ignore for runtime budget unless referenced by player assembly. |
| Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureControlMapBaker.cs | 207 | new RenderTexture | true | Editor-only; keep out of player build and ignore for runtime budget unless referenced by player assembly. |
| Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureControlMapBaker.cs | 216 | RenderTextureDescriptor | true | Editor-only; keep out of player build and ignore for runtime budget unless referenced by player assembly. |
| Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureControlMapBaker.cs | 219 | new RenderTexture | true | Editor-only; keep out of player build and ignore for runtime budget unless referenced by player assembly. |
| Assets/_Project/Scripts/Editor/HectonArtOptimizationTools.cs | 283 | RenderTexture.GetTemporary | true | Editor-only; keep out of player build and ignore for runtime budget unless referenced by player assembly. |
| Assets/_Project/Scripts/Editor/HectonArtOptimizationTools.cs | 701 | RenderTexture.GetTemporary | true | Editor-only; keep out of player build and ignore for runtime budget unless referenced by player assembly. |
| Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs | 748 | RenderTextureDescriptor | true | Editor-only; keep out of player build and ignore for runtime budget unless referenced by player assembly. |
| Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs | 759 | new RenderTexture | true | Editor-only; keep out of player build and ignore for runtime budget unless referenced by player assembly. |
| Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs | 770 | RenderTextureDescriptor | true | Editor-only; keep out of player build and ignore for runtime budget unless referenced by player assembly. |
| Assets/_Project/Scripts/Editor/HectonOctahedralImpostorBaker.cs | 781 | new RenderTexture | true | Editor-only; keep out of player build and ignore for runtime budget unless referenced by player assembly. |
| Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForgeSupport.cs | 629 | RenderTexture.GetTemporary | true | Editor-only; keep out of player build and ignore for runtime budget unless referenced by player assembly. |
| Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForgeSupport.cs | 644 | RenderTexture.GetTemporary | true | Editor-only; keep out of player build and ignore for runtime budget unless referenced by player assembly. |
| Assets/_Project/Scripts/Editor/OfflineGeometryBaker/InteriorClutterForgeSupport.cs | 683 | RenderTexture.GetTemporary | true | Editor-only; keep out of player build and ignore for runtime budget unless referenced by player assembly. |
| Assets/_Project/Scripts/Editor/QuestVrOptimizationValidator1406.cs | 523 | RTHandles.Alloc | true | Editor-only; keep out of player build and ignore for runtime budget unless referenced by player assembly. |
| Assets/_Project/Scripts/Editor/QuestVrOptimizationValidator1406.cs | 614 | RTHandles.Alloc | true | Editor-only; keep out of player build and ignore for runtime budget unless referenced by player assembly. |

## Priority 5 - Clamp First-Party Large Textures

| Path | Source | Est. full-mip MiB saved by halving | Current flags | Required action |
|---|---:|---:|---|---|
| Assets/_Project/Art/Models/Rocks/Rock 7/Materials/2.jpg | 4000x4000 | 15.26 | VRAM CRIME: TEXTURE_GT_2048 | Low tier import cap 1024 or lower; keep higher variant only behind streaming/tier proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/Aegir_storms.png | 4096x2048 | 8.00 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE | Low tier import cap 1024 or lower; keep higher variant only behind streaming/tier proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/clouds.png | 4096x2048 | 8.00 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE | Low tier import cap 1024 or lower; keep higher variant only behind streaming/tier proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/clouds0_diff.png | 4096x2048 | 8.00 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE | Low tier import cap 1024 or lower; keep higher variant only behind streaming/tier proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/clouds0_diff.png | 4096x2048 | 8.00 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE | Low tier import cap 1024 or lower; keep higher variant only behind streaming/tier proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_bump.png | 4096x2048 | 8.00 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE | Low tier import cap 1024 or lower; keep higher variant only behind streaming/tier proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_diff.png | 4096x2048 | 8.00 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE | Low tier import cap 1024 or lower; keep higher variant only behind streaming/tier proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_norm.png | 4096x2048 | 8.00 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE | Low tier import cap 1024 or lower; keep higher variant only behind streaming/tier proof. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_spec.png | 4096x2048 | 8.00 | VRAM CRIME: TEXTURE_GT_2048;STREAMING_MIPMAPS_OFF_LARGE | Low tier import cap 1024 or lower; keep higher variant only behind streaming/tier proof. |
| Assets/_Project/Art/TEXTURES/Aegir_storms.png | 4096x2048 | 8.00 | VRAM CRIME: TEXTURE_GT_2048 | Low tier import cap 1024 or lower; keep higher variant only behind streaming/tier proof. |
| Assets/_Project/Art/TEXTURES/clouds.png | 4096x2048 | 8.00 | VRAM CRIME: TEXTURE_GT_2048 | Low tier import cap 1024 or lower; keep higher variant only behind streaming/tier proof. |
| Assets/_Project/Art/TEXTURES/clouds0_diff.png | 4096x2048 | 8.00 | VRAM CRIME: TEXTURE_GT_2048 | Low tier import cap 1024 or lower; keep higher variant only behind streaming/tier proof. |

Static halving relief if every runtime-candidate >1024 texture is halved: 1133.38 MiB full-mip BC7.

## Priority 6 - Enable Streaming Mipmaps On Large First-Party Textures

| Path | Source | Streaming metadata | Required action |
|---|---:|---|---|
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/Aegir_storms.png | 4096x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/clouds.png | 4096x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/clouds0_diff.png | 4096x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/clouds0_diff.png | 4096x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_bump.png | 4096x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_diff.png | 4096x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_norm.png | 4096x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_spec.png | 4096x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/blue_metal_plate/TX_PH_blue_metal_plate_ARM_AO_Rough_Metal_2k.jpg | 2048x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/blue_metal_plate/TX_PH_blue_metal_plate_BaseColor_2k.jpg | 2048x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/blue_metal_plate/TX_PH_blue_metal_plate_Height_2k.jpg | 2048x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/blue_metal_plate/TX_PH_blue_metal_plate_MaskMap_UnityURP_2k.png | 2048x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/blue_metal_plate/TX_PH_blue_metal_plate_NormalGL_2k.jpg | 2048x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/box_profile_metal_sheet/TX_PH_box_profile_metal_sheet_ARM_AO_Rough_Metal_2k.jpg | 2048x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/box_profile_metal_sheet/TX_PH_box_profile_metal_sheet_BaseColor_2k.jpg | 2048x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/box_profile_metal_sheet/TX_PH_box_profile_metal_sheet_Height_2k.jpg | 2048x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/box_profile_metal_sheet/TX_PH_box_profile_metal_sheet_MaskMap_UnityURP_2k.png | 2048x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/box_profile_metal_sheet/TX_PH_box_profile_metal_sheet_NormalGL_2k.jpg | 2048x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/container_side/TX_PH_container_side_ARM_AO_Rough_Metal_2k.jpg | 2048x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/container_side/TX_PH_container_side_BaseColor_2k.jpg | 2048x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/container_side/TX_PH_container_side_Height_2k.jpg | 2048x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/container_side/TX_PH_container_side_MaskMap_UnityURP_2k.png | 2048x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/container_side/TX_PH_container_side_NormalGL_2k.jpg | 2048x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/corrugated_iron/TX_PH_corrugated_iron_ARM_AO_Rough_Metal_2k.jpg | 2048x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |
| Assets/_Project/Art/TEXTURES/Generated/ExternalPBR_20260607/PolyHaven/corrugated_iron/TX_PH_corrugated_iron_BaseColor_2k.jpg | 2048x2048 | 0 | Enable streaming mips unless UI/non-mipped proof exists. |

## Priority 7 - Atlas Small First-Party Texture Families

| Group | Count | Combined BC7 MiB | Required action |
|---|---:|---:|---|
| Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SplitAtlasCandidates_20260608/decal_atlas/B34-3424 | 36 | 9.00 | Build one atlas/material family or justify separate residency. |
| Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SplitAtlasCandidates_20260608/uv_atlas/B34-3444 | 31 | 7.75 | Build one atlas/material family or justify separate residency. |
| Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SplitAtlasCandidates_20260608/uv_atlas/B34-3440 | 28 | 9.25 | Build one atlas/material family or justify separate residency. |
| Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SplitAtlasCandidates_20260608/uv_atlas/B34-3447 | 28 | 7.00 | Build one atlas/material family or justify separate residency. |
| Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SplitAtlasCandidates_20260608/uv_atlas/B34-3438 | 17 | 8.00 | Build one atlas/material family or justify separate residency. |

## Priority 8 - Mesh LOD And Importer Redlines

| Path | Triangles | Geometry MiB | LOD detected | Readable | Compression | BlendShapes | Flags | Required action |
|---|---:|---:|---:|---:|---:|---:|---|---|
| Assets/Feel/MMTools/Demos/MMGhostCamera/Models/MMGhostCameraCity.fbx | 127645 | 18.99 | false | 0 | 0 | 1 | MESH_GEOMETRY_ESTIMATE_GT_16MIB_STATIC;MESH_GT_80K_ABSOLUTE_STATIC;MESH_REDLINE_GT_50K_NO_LOD;MESH_COMPRESSION_OFF_STATIC_SUSPECT;MESH_BLENDSHAPES_IMPORT_ENABLED_STATIC_SUSPECT | Add LOD0/LOD1/LOD2 or cull/impostor path before production visibility beyond 20m. |
| Assets/_Project/Art/Materials/Meshy_AI_Alien_barnacles_clust_0301230506_texture.fbx | 10000 | 1.49 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT | Fix ModelImporter risk: disable Read/Write, BlendShapes, colliders, or compression-off unless CPU/hero proof exists. |
| Assets/_Project/Art/Models/Rocks/Rock 7/SAMMPLE.fbx | 6519 | 0.97 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT | Fix ModelImporter risk: disable Read/Write, BlendShapes, colliders, or compression-off unless CPU/hero proof exists. |
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx | 5000 | 0.74 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT | Fix ModelImporter risk: disable Read/Write, BlendShapes, colliders, or compression-off unless CPU/hero proof exists. |
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Forest_Rock_Shelf_wgpqfjl_Mid.fbx | 4038 | 0.60 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT | Fix ModelImporter risk: disable Read/Write, BlendShapes, colliders, or compression-off unless CPU/hero proof exists. |
| Assets/_Project/Art/Models/Rocks/Rock 6/rock6/Mossy_Forest_Rock_vimrfjsaw_Mid.fbx | 3539 | 0.53 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT | Fix ModelImporter risk: disable Read/Write, BlendShapes, colliders, or compression-off unless CPU/hero proof exists. |
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Mossy_Forest_Rock_vimrfjsaw_Mid.fbx | 3539 | 0.53 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT | Fix ModelImporter risk: disable Read/Write, BlendShapes, colliders, or compression-off unless CPU/hero proof exists. |
| Assets/Shapes/Models/shapes_primitives.fbx | 3222 | 0.48 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT | Fix ModelImporter risk: disable Read/Write, BlendShapes, colliders, or compression-off unless CPU/hero proof exists. |
| Assets/_Project/Art/Models/Rocks/Rock 5/orig/River_Rock_FBX.fbx | 3054 | 0.45 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT | Fix ModelImporter risk: disable Read/Write, BlendShapes, colliders, or compression-off unless CPU/hero proof exists. |
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Nordic_Beach_Rock_Formation_vd4iecjva_Low.fbx | 2100 | 0.31 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT | Fix ModelImporter risk: disable Read/Write, BlendShapes, colliders, or compression-off unless CPU/hero proof exists. |
| Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Nordic_Beach_Rock_uknoehp_Mid.fbx | 1218 | 0.18 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT | Fix ModelImporter risk: disable Read/Write, BlendShapes, colliders, or compression-off unless CPU/hero proof exists. |
| Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx | 782 | 0.12 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT | Fix ModelImporter risk: disable Read/Write, BlendShapes, colliders, or compression-off unless CPU/hero proof exists. |
| Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_c.fbx | 742 | 0.11 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT | Fix ModelImporter risk: disable Read/Write, BlendShapes, colliders, or compression-off unless CPU/hero proof exists. |
| Assets/_Project/_PROLOGUE_CONTENT/Models/floor_05.fbx | 670 | 0.10 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT | Fix ModelImporter risk: disable Read/Write, BlendShapes, colliders, or compression-off unless CPU/hero proof exists. |
| Assets/_Project/_PROLOGUE_CONTENT/Models/wall_04_3x6_d.fbx | 586 | 0.09 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT | Fix ModelImporter risk: disable Read/Write, BlendShapes, colliders, or compression-off unless CPU/hero proof exists. |
| Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_2x3_a.fbx | 530 | 0.08 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT | Fix ModelImporter risk: disable Read/Write, BlendShapes, colliders, or compression-off unless CPU/hero proof exists. |
| Assets/_Project/_PROLOGUE_CONTENT/Models/ceiling_10.fbx | 108 | 0.02 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT | Fix ModelImporter risk: disable Read/Write, BlendShapes, colliders, or compression-off unless CPU/hero proof exists. |
| Assets/_Project/_PROLOGUE_CONTENT/Models/floor_large_8x8.fbx | 2 | 0.00 | false | 0 | 0 | 0 | MESH_COMPRESSION_OFF_STATIC_SUSPECT | Fix ModelImporter risk: disable Read/Write, BlendShapes, colliders, or compression-off unless CPU/hero proof exists. |

## Verification Required After Asset Fixes

- Rerun `python Tools/MemoryBudgetCheck.py --root . --ci`.
- Open Unity and verify importer settings for every changed texture/mesh.
- Capture Memory Profiler texture memory and graphics memory in target scene.
- Run MX350/LOW profile and prove frame-time/VRAM with player or profiler artifact.
- Do not mark runtime VRAM solved from this static plan alone.
