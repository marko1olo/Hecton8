# AGENT 01 Graphics Log

Generated: `2026-04-25`
Status: `PENDING VERIFICATION`

Mandates followed:
- `GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `PROJECT_LTS_Compatibility_Layer.txt`
- `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`

## What Was Wrong

- `Hecton_NoirAutoExposure.compute` declared a `groupshared` local, which breaks shader compilation.
- `Hecton_ScooterVolumetricShafts.shader` called `ResolveVolumetricLightDistanceFade` before a usable definition was visible to the compiler.
- `HectonIndirectVegetationRenderer` could enter BRG setup with no valid material and fail immediately.
- `SargassumMicroFaunaBoids.compute` referenced spatial-grid frame data aliases that were never defined.
- `Assets/Bakery/emptyLightingData.asset` was unreferenced and emitted the incompatible lighting-data warning.
- `Hecton_AegirHazeOverlay.shader` still used alpha blending in the haze overlay path.

## What Changed

- Reworked `Hecton_NoirAutoExposure.compute` exposure resolve to remove the illegal `groupshared` local reduction and replaced it with a scalar resolve path.
- Injected finite-value guards into `Hecton_NoirAutoExposure.compute`, `Hecton_VoxelSSAO.compute`, `Hecton_BiolumSSGI.compute`, and `FloraCulling.compute` so NaN/INF inputs collapse to safe defaults instead of poisoning exposure, lighting, or culling.
- Moved `ResolveVolumetricLightDistanceFade` and `ResolveSpotConeAttenuation` to active definitions early inside `Hecton_ScooterVolumetricShafts.shader`.
- Added resolve-stage blue-noise dithering to `Hecton_ScooterVolumetricShafts.shader` to break low-light banding without adding texture bandwidth-heavy taps.
- Converted `Hecton_AegirHazeOverlay.shader` from `Blend SrcAlpha OneMinusSrcAlpha` to dithered cutout coverage using `_BlueNoiseTex` when available and interleaved-noise fallback otherwise.
- Hardened `HectonIndirectVegetationRenderer.cs` so missing shared material authoring now falls back through shader resolution and a hidden runtime material before BRG setup continues.
- Removed the unreferenced incompatible Bakery lighting asset: `Assets/Bakery/emptyLightingData.asset`.

## MX350 Baseline

- Keep scooter shafts half-resolution. Do not raise the shaft pass to full resolution on MX350.
- Keep depth priming disabled and MSAA at `1` for Crest/depth stability.
- Keep volumetric marching at the existing low step count and lean on blue-noise jitter instead of extra samples.
- Keep Aegir haze in dithered coverage mode instead of alpha blending.
- Prefer ALU noise and analytic phase terms over extra lookup textures in custom underwater post.

## RTX Scale-Up

- Raise shaft render scale only after frame-time proof.
- Increase raymarch distance or blur radius before increasing step count.
- Re-enable richer haze density and lens ghost intensity only after SetPass and frame-time checks stay inside budget.
- Keep the NaN guards; they are correctness guards, not quality-tier features.

## Depth / Water Notes

- `HectonUrpTextureRequirementsGuard.cs` already forces `supportsCameraDepthTexture = true`, `supportsCameraOpaqueTexture = true`, `msaaSampleCount = 1`, and `DepthPrimingMode.Disabled`.
- `HectonAbyssalSsdoFeature.cs` samples camera depth and normals but does not clear or overwrite `_CameraDepthTexture` in the feature owner.

## Regression Model

- CPU: shader-side only except for cold-path BRG material fallback and asset deletion. No hot-path C# loops added.
- GC: no new managed allocations in per-frame gameplay paths. New material fallback is cold-path only.
- Memory: NaN guards add ALU, not persistent memory. Bakery lighting warning removal reduces dead asset surface area.
- Cadence: volumetric resolve dithering adds one blue-noise sample/fallback hash at composite time; this is cheaper than increasing sample count.
- Correctness: safe-value clamps can suppress catastrophic black-screen corruption at the cost of zeroing invalid samples.

## Hot Path Impact

- `Hecton_ScooterVolumetricShafts.shader`: minor ALU increase from resolve dithering, no extra full-resolution passes.
- `Hecton_AegirHazeOverlay.shader`: removed alpha blending; coverage now resolves through dither clip.
- Compute guards: branch cost added only around invalid-data handling paths.

## Failure Modes

- If `_BlueNoiseTex` is not bound globally, `Hecton_AegirHazeOverlay.shader` falls back to interleaved gradient noise. Visual result is stable but less ideal than true blue noise.
- If a required vegetation shader asset is missing and `Shader.Find` also fails, BRG still cannot render. The fallback prevents null material startup, not missing-shader authoring.
- Unity MCP transport was unstable during domain reloads. Live screenshot verification is incomplete until the editor websocket remains attached through compile.

## Verification

- Code review proof:
  - `dotnet build Hecton8.Core.csproj -nologo` reached `0 Error(s)` after restoring unrelated broken runtime structs that blocked Unity compile.
  - `PC_Renderer.asset`, `PC_High_Renderer.asset`, and `Mobile_Renderer.asset` already report `m_DepthPrimingMode: 0`.
  - `HectonUrpTextureRequirementsGuard.cs` enforces MSAA `1` and depth texture support in code.
- Live Unity proof:
  - `PENDING VERIFICATION` because the Unity MCP websocket repeatedly disconnected during domain reload, preventing a final clean console capture and runtime screenshot pass.

## Iteration 15 - HDRP Purge / Crest Import Repair

### What Was Wrong

- `Assets/MapMagic/Preview/Editor/Shaders/TerrainPreviewHDRP.shader` hard-referenced `Hidden/HDRP/TerrainLit_Basemap` and `Hidden/HDRP/TerrainLit_BasemapGen` inside a URP project.
- `Assets/MapMagic/Preview/Editor/MatrixPreview.cs` still selected `MapMagic/TerrainPreviewHDRP` when it detected an HD-style terrain material or pipeline name.
- `Assets/MapMagic/Core/MapMagicObject.cs` still fell back to `Shader.Find("HDRP/TerrainLit")` when resolving the default terrain material.
- `Assets/Crest/Crest/Scripts/LodData/RegisterLodDataInput.cs` exposed only generic/alternate partial `Unity.Object`-derived types, so Unity's MonoScript importer could not anchor the file name to a matching class.
- `Hecton_NoirAutoExposure.compute` still relied on `isfinite()`, which D3D11 warned could be optimized away under `/Gis`.

### What Changed

- Deleted `Assets/MapMagic/Preview/Editor/Shaders/TerrainPreviewHDRP.shader` and its `.meta` so the URP project no longer imports any MapMagic HDRP terrain preview shader.
- Removed the HDRP selection branch from `Assets/MapMagic/Preview/Editor/MatrixPreview.cs`; SRP preview routing now resolves `MapMagic/TerrainPreviewURP` or falls back to the non-SRP preview only.
- Replaced the `HDRP/TerrainLit` fallback in `Assets/MapMagic/Core/MapMagicObject.cs` with `Universal Render Pipeline/Terrain/Lit`.
- Added a non-generic `RegisterLodDataInput` MonoScript anchor type inside `Assets/Crest/Crest/Scripts/LodData/RegisterLodDataInput.cs` so Unity has one filename-matching `Unity.Object`-derived partial type in the file.
- Replaced `isfinite()`-based guards in `Assets/_Project/Art/Shaders/Hecton_NoirAutoExposure.compute` with bit-mask finite checks using `asuint(value) & 0x7FFFFFFF` and clamped EV bounds, preventing zero/NaN exposure multiplier output under D3D11 optimization.

### Water Depth Notes

- `HectonScooterVolumetricShaftsFeature.cs`, `HectonAbyssalSsdoFeature.cs`, `HectonBiolumSSGIFeature.cs`, `HectonVoxelSsaoFeature.cs`, and `HectonSonarPointCloudFeature.cs` all read `resourceData.cameraDepthTexture` as an input texture; the feature owners do not clear the camera depth texture.
- Crest URP underwater paths still declare `ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth)`.
- `Packages/com.waveharmonic.crest/Runtime/Scripts/WaterRenderer.Universal.cs` configures the copy-depth path with `shouldClear: false`.
- Legacy Crest `Assets/Crest/Crest/Scripts/Underwater/UnderwaterEffectPassURP.cs` binds the existing camera depth target when stencil mode is not used. Its temporary depth-stencil copy is local to Crest's effect path, not a global `_CameraDepthTexture` clear.

### Status

- `PENDING VERIFICATION`. Source-level HDRP leakage under `Assets/MapMagic` was removed, but live Unity render proof is still required after a clean domain reload.

### Live Editor Proof Collected After Refresh

- Cleared the Unity console, forced `refresh_unity(mode=force, scope=all, compile=request)`, then re-read the console.
- Fresh post-refresh console sample contained 2 warnings only:
  - `ProceduralWreckGenerator.cs(601,39): warning CS0414`
  - `MCP-FOR-UNITY` websocket warning
- Fresh post-refresh console sample contained no `TerrainPreviewHDRP`, no `Hidden/HDRP/TerrainLit_Basemap`, no `Hecton_NoirAutoExposure`, and no `RegisterLodDataInput.cs` partial-class importer error.
- Scene-view capture: `Assets/Screenshots/hecton_iteration15_sceneview.png`
- Game-view capture: `Assets/Screenshots/hecton_iteration15_gameview.png`

## Iteration 16 - Water Depth / Render Pipeline

### Mandates Followed

- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `PROJECT_LTS_Compatibility_Layer.txt`
- `REND_Abyssal_Lighting_Voxel_Occlusion_Shadows.txt`
- `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`

### What Was Wrong

- The active PC URP assets already had `Depth Texture = On`, `Opaque Texture = On`, `Opaque Downsampling = None`, `MSAA = 1`, and renderer `Depth Priming = Disabled`, so the transparent water fault was not caused by those asset toggles.
- The active first-party renderer features were reading `resourceData.cameraDepthTexture`; they were not clearing the global `_CameraDepthTexture`.
- `HectonDryVolumeFeature.cs` is the only first-party feature that binds `activeDepthTexture` as `AccessFlags.ReadWrite`, but there is no scene or renderer asset reference wiring it into the active renderers.
- The player base camera prefab still serialized `m_RequiresOpaqueTextureOption: 2` (`UsePipelineSettings`) and left `m_RequiresDepthTexture` / `m_RequiresColorTexture` at `0`, so there was no scene-level hard guarantee that Crest's underwater camera would force the URP copies even when the project asset was correct.
- The active Crest ocean material asset on `OceanRenderer` was `Assets/Crest/Crest/Materials/Ocean.mat`, and that asset still lacked `_UNDERWATER_ON`, kept `_Underwater: 0`, and used `_CullMode: Back`. Crest's own validator marks that as invalid for underwater rendering.
- The world scene contains `OceanDepthCache` owners, so the failure model is not "no sea-floor cache exists". The broken chain was camera/material enforcement, not total absence of depth-cache ownership.

### What Changed

- Extended `Assets/_Project/Scripts/Core/HectonUrpTextureRequirementsGuard.cs` so scene load now enforces `requiresDepthOption = On` and `requiresColorOption = On` for base cameras that render the Crest ocean layer or carry `Crest.UnderwaterRenderer`.
- Hardened the player main camera authoring in `Assets/_Project/Prefabs/Player.prefab`:
  - `m_RequiresDepthTextureOption: 1`
  - `m_RequiresOpaqueTextureOption: 1`
  - `m_RequiresDepthTexture: 1`
  - `m_RequiresColorTexture: 1`
- Corrected the Crest surface material asset in `Assets/Crest/Crest/Materials/Ocean.mat`:
  - added `_UNDERWATER_ON`
  - set `_Underwater: 1`
  - set `_CullMode: 0`
- Added `Assets/_Project/Editor/HectonRenderPipelineValidator.cs`. It now:
  - auto-scans the active `UniversalRenderPipelineAsset`
  - forcefully sets `supportsCameraDepthTexture = true`
  - forcefully sets `supportsCameraOpaqueTexture = true`
  - forcefully sets `msaaSampleCount = 1`
  - forcefully disables `DepthPrimingMode`
  - emits severe warnings for active custom renderer features injected before opaques when their source contains depth-mutation patterns
  - validates active Crest underwater cameras, ocean materials, and `OceanDepthCache` objects in the open scene

### Depth Chain Forensics

- `Assets/Crest/Crest/Scripts/OceanRenderer.RenderGraph.cs` still requests `ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth`.
- `Assets/Crest/Crest/Scripts/Underwater/UnderwaterEffectPassURP.cs` still requests `ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth` and binds the existing `_depthTarget` when not using the temporary stencil copy.
- `Assets/Crest/Crest/Scripts/Underwater/UnderwaterMaskPassURP.cs` clears only Crest's local `_underwaterRenderer._depthTarget`; it does not clear the global `_CameraDepthTexture`.
- `Assets/_Project/Scripts/Visor/HectonAbyssalSsdoFeature.cs`, `HectonScooterVolumetricShaftsFeature.cs`, `HectonBiolumSSGIFeature.cs`, and `HectonVoxelSsaoFeature.cs` read `resourceData.cameraDepthTexture`.
- `Assets/_Project/Scripts/Visor/HectonDryVolumeFeature.cs` is the only first-party feature found using `resourceData.activeDepthTexture` as `AccessFlags.ReadWrite`, but grep found no renderer asset / scene reference to its GUID `1d026629ab20b2f4cba2b148d18ec9ff`.

### Regression Model

- CPU: one cold-path scene camera sweep on scene load. No new per-frame loops.
- GC: one cold-path `FindObjectsByType<UniversalAdditionalCameraData>()` allocation on scene load inside the runtime guard. No new hot-path allocations.
- Memory: no persistent render target or texture additions. Material asset change reuses existing Crest shader/material infrastructure.
- Cadence: editor validator runs on delayed editor load and by menu only. Runtime guard executes before scene load and on scene load callbacks only.
- Correctness: scene cameras now explicitly request URP depth/color copies for Crest, and the shared ocean material no longer starts in an underwater-invalid cull/keyword state.

### Verification

- `dotnet build Hecton8.Core.csproj -nologo -v minimal` succeeded with `0 Error(s)`.
- `dotnet build Hecton8.Editor.csproj -nologo -v minimal` succeeded with `0 Error(s)`.
- Remaining build warnings were external Unity test-runner reference resolution warnings in generated editor project files, not compile errors in the modified HECTON-8 source.
- Live Unity render proof remains `PENDING VERIFICATION` because the MCP websocket is unstable and no fresh runtime frame capture was available after this pass.

## Iteration 17 - Bakery / LODGroup / Water Depth

### Mandates Followed

- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `PROJECT_LTS_Compatibility_Layer.txt`
- `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

### What Was Wrong

- `Assets/_Project/Scripts/Editor/HectonFBXPostprocessor.cs` still honored the `HECTON_POSTPROCESS_TANGENTS` escape hatch inside `Assets/ScifiFacility`, which forced the two known crash FBXs into `tangentImportMode: None` even though Bakery needs stable normals/tangents before `ftModelPostProcessor.OnPostprocessModel`.
- `Assets/ScifiFacility/Models/structural/walls/wall_01_6x3_door.fbx.meta` and `wall_01_6x3_door_b.fbx.meta` still serialized `tangentImportMode: 2` with `userData: HECTON_POSTPROCESS_TANGENTS`. The internal zero-vertex planes (`Plane.481`, `Plane.492`, `Plane.498`) were the reason that escape hatch existed.
- `Assets/_Project/Prefabs/Nature/ГОТОВЫЕ ПРЕФАБЫ КАМНЕЙ/ENV_ Скала2.prefab` serialized a null `LOD0` renderer in its root `LODGroup`.
- `Assets/_Project/Scenes/XXX_SANDBOX.unity` compensated by overriding `ENV_ Скала2` LOD0 to use `ENV_ Болдер 2`'s stripped `LOD0` renderer, which is the direct source of the duplicate-LODGroup registration conflict.
- `Assets/Feel/MMTools/Tools/MMShaders/MMRipple.shader` still fell back to the removed built-in `Particle/AlphaBlended` shader.
- `Assets/_Project/Editor/HectonRenderPipelineValidator.cs` was menu-only, so it was not auto-running after reload to keep URP/Crest depth enforcement alive when the editor transport dropped.

### What Changed

- Hardened `HectonFBXPostprocessor.cs` so `Assets/ScifiFacility` now always resolves tangents as `ModelImporterTangents.CalculateMikk`, strips the `HECTON_POSTPROCESS_TANGENTS` marker for that root, and exposes a dedicated `Reimport FBX - ScifiFacility` menu action.
- Patched the two known ScifiFacility offender metas immediately:
  - `wall_01_6x3_door.fbx.meta`: `tangentImportMode: 3`, cleared `userData`
  - `wall_01_6x3_door_b.fbx.meta`: `tangentImportMode: 3`, cleared `userData`
- Fixed `ENV_ Скала2.prefab` so the root `LODGroup` now owns its own `LOD0` renderer instead of a null slot.
- Removed the bad cross-prefab scene override from `XXX_SANDBOX.unity`; `ENV_ Скала2` no longer steals `ENV_ Болдер 2`'s `LOD0` renderer.
- Added `Assets/_Project/Editor/HectonLodGroupConflictResolver.cs` to repair the named rock prefabs and loaded scene instances if renderer ownership drifts again.
- Changed `MMRipple.shader` fallback to `Universal Render Pipeline/Particles/Unlit`.
- Restored auto-execution in `HectonRenderPipelineValidator.cs` via `InitializeOnLoad`, `EditorApplication.projectChanged`, and `EditorSceneManager.sceneOpened`, and narrowed its hard warnings to depth-mutation patterns rather than simple depth reads.
- Extended `HectonMaterialChannelPackValidator.cs` with an enforcement pass that fixes packed mask importers to `TextureImporterType.Default` + linear sampling and reports any material that still relies on loose metallic / AO / emission textures.

### Water Depth Notes

- Current first-party active-depth evidence is unchanged: `HectonAbyssalSsdoFeature.cs` and `HectonScooterVolumetricShaftsFeature.cs` read `resourceData.cameraDepthTexture`; they do not clear `_CameraDepthTexture`.
- `HectonRenderPipelineValidator.cs` now auto-runs after reload and scene open, so URP asset requirements and Crest camera/material checks are re-applied even when the MCP websocket is unstable.
- `HectonDryVolumeFeature.cs` remains the only first-party depth read/write feature in source, and grep still finds no active asset or scene reference wiring it into the project renderers.

### Regression Model

- CPU: editor/import-time only. No gameplay hot-path cost added.
- GC: editor menu and validation only. No runtime frame allocations introduced.
- Memory: no new runtime render targets or resident textures. Channel-pack enforcement only normalizes existing texture import settings.
- Cadence: validator now runs on editor reload / project change / scene open instead of menu-only, which increases editor validation cadence but not player runtime cost.
- Correctness: ScifiFacility models now stop advertising the tangents-postprocess escape hatch that was destabilizing Bakery, and the named rock assets no longer serialize cross-prefab LOD ownership.

### Verification

- Source evidence:
  - `AGENT_06_TECHART_LOG.md` identified `wall_01_6x3_door*.fbx` as the exact files forced into postprocess tangents because of zero-vertex planes.
  - `ENV_ Скала2.prefab` root `LODGroup` now points `LOD0` to its own `MeshRenderer`.
  - `XXX_SANDBOX.unity` no longer contains the `m_LODs.Array.data[0].renderers.Array.data[0].renderer` override for `ENV_ Скала2`.
  - `MMRipple.shader` no longer references `Particle/AlphaBlended`.
- Live Unity proof:
  - `PENDING VERIFICATION`. MCP transport remains unstable and no post-import Bakery / water runtime capture was available in this pass.

## Iteration 18 - Screenshot Import Choke / Camera Depth / Crest Partial Class

### Mandates Followed

- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `PROJECT_LTS_Compatibility_Layer.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

### What Was Wrong

- `Assets/Screenshots` still existed on disk and the live screenshot writers (`MMScreenshot`, `HectonDevToolsMenu`, `WorldProceduralFloraFinalStatusReport`) still targeted that folder, forcing Unity to reimport PNG dumps during refresh.
- `Player.prefab` main camera was already authored correctly, but the gameplay camera enforcement code only forced depth/color and did not force post-processing on Crest-owned runtime cameras. `SpaceCamera` in the same prefab still serialized all depth/color/post flags off.
- `Assets/Crest/Crest/Scripts/LodData/RegisterLodDataInput.cs` still used `partial` on Unity.Object-derived classes. That is the importer-pattern Unity was complaining about.
- `HectonFBXPostprocessor.cs` still early-returned on zero-vertex meshes without normal/tangent array repair, leaving Bakery exposed to null-length mesh channels even though importer normals were forced.

### What Changed

- Moved `Assets/Screenshots` out of the import domain to project-root `Screenshots/` and removed `Assets/Screenshots.meta`.
- Redirected screenshot output to `Application.dataPath/../Screenshots` in:
  - `Assets/Feel/MMTools/Tools/MMUtilities/MMScreenshot.cs`
  - `Assets/_Project/Editor/HectonDevToolsMenu.cs`
  - `Assets/_Project/Scripts/Editor/WorldProceduralFloraFinalStatusReport.cs`
- Extended `HectonUrpTextureRequirementsGuard.cs` to force:
  - `requiresDepthOption = On`
  - `requiresColorOption = On`
  - `requiresDepthTexture = true`
  - `requiresColorTexture = true`
  - `renderPostProcessing = true`
  for base cameras that render the ocean or own a Crest `UnderwaterRenderer`.
- Extended `HectonUnderwaterVisuals.cs` to apply the same enforcement at runtime camera ownership points and to re-apply it immediately after restoring the Crest underwater renderer.
- Serialized `Player.prefab` `SpaceCamera` to request depth/color and post-processing so authoring-time Game View composition no longer starts from an invalid base camera.
- Repaired Crest by removing `partial` from `RegisterLodDataInputBase`, `RegisterLodDataInput<T>`, and `RegisterLodDataInputWithSplineSupport<,>` and folding their editor-only validation fragments back into the primary class bodies. This keeps the original `RegisterLodDataInput.cs` path intact while removing the Unity.Object partial-class importer trigger.
- Hardened `HectonFBXPostprocessor.cs` so zero-vertex meshes now write explicit empty normal/tangent arrays instead of returning untouched.

### Regression Model

- CPU: screenshot changes are editor-only path redirects. Runtime camera enforcement is still cold-path only.
- GC: no new per-frame allocations added. The only path changes are editor filesystem writes and scene-load camera sweeps already present in the guard.
- Memory: removing `Assets/Screenshots` from the import domain lowers asset-database churn and avoids imported PNG texture residency. Runtime camera enforcement does not allocate new render targets.
- Cadence: screenshot writers now emit to project-root storage and stop triggering asset refreshes for captures. Crest class validation remains source-only until Unity refreshes.
- Correctness: runtime water cameras now explicitly request the URP buffers Crest needs, and the Crest LOD input base file no longer relies on the partial Unity.Object pattern that Unity was rejecting.

### Verification

- Filesystem:
  - `Assets/Screenshots` no longer exists.
  - `Assets/Screenshots.meta` no longer exists.
  - `Screenshots/` exists at the project root.
- Build:
  - `dotnet build MoreMountains.Tools.csproj -nologo -v minimal /m:1 /p:BuildProjectReferences=false` succeeded with `0 Error(s)`.
  - `dotnet build Crest.csproj -nologo -v minimal /m:1 /p:BuildProjectReferences=false` succeeded with `0 Error(s)` after the Crest partial-class repair.
  - `Hecton8.Core.csproj` and `Hecton8.Editor.csproj` remain `PENDING VERIFICATION` because the local generated-project graph is broken by missing `Unity.ShaderGraph.Editor` / `WaveHarmonic.Crest.Shared.Editor` metadata and package-cache content unrelated to the edited first-party files.
- Live Unity render proof:
  - `PENDING VERIFICATION`. No fresh Game View frame or live editor import log was available after this pass.

## Iteration 20 - Ocean Depth Cache / Temporal R2 Dithering Restart

### Mandates Followed

- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `PROJECT_LTS_Compatibility_Layer.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

### What Was Wrong

- `Ocean_Crest.prefab` had `_createSeaFloorDepthData: 1` on `Crest.OceanRenderer`, but first-party prefabs and scenes still contained no serialized `OceanDepthCache` and no `RegisterSeaFloorDepthInput`. Crest therefore had sea-floor depth simulation enabled with no producer.
- `Hecton_AegirHazeOverlay.shader` still used static blue-noise UVs, so its dither pattern screen-doored in place instead of temporally decorrelating for TAA.
- `Hecton_ScooterVolumetricShafts.shader` already had a temporal offset, but it was hard-coded inline against a fixed `64x64` assumption instead of an explicit R2 helper keyed to the actual blue-noise texture texel size.
- `Hecton_CoreLit.hlsl` already respected the 24-step cap, but the raymarch loop did not expose that cap as an explicit loop constant for audit evidence.
- `HectonFBXPostprocessor.cs` still left Unity secondary UV unwrap settings implicit when `generateSecondaryUV` was enabled elsewhere, which is not deterministic enough for future Bakery recovery passes.

### What Changed

- Added `HectonCrestOceanDepthCacheBootstrap.cs` under `Assets/_Project/Scripts/World/`.
  - The bootstrap resolves `OceanRenderer`, `MapMagicBridge`, and `OceanDepthCache`.
  - It fits the cache transform to aggregated `Terrain.activeTerrains` bounds, locks X/Z scale to a square Crest-compatible footprint, aligns the cache Y to the authored water level, and calls `PopulateCache(updateComponents: true)`.
  - It registers as `ISlowTickable` and repopulates only when streamed terrain coverage or water level changes, keeping the fix out of the frame hot path.
- Serialized a real `OceanDepthCache` child into `Assets/_Project/Prefabs/Ocean_Crest.prefab`.
  - `OceanDepthCache` now exists as a prefab child instead of relying on null fallback.
  - The cache is configured as realtime `OnDemand`, with capture layers `Default | Terrain`, resolution `512`, and camera max terrain height `256`.
  - `HectonCrestOceanDepthCacheBootstrap` is attached to the prefab root and references both the `OceanRenderer` and the serialized `OceanDepthCache`.
- Replaced static/inline blue-noise offsets with explicit R2 sequence helpers.
  - `Hecton_ScooterVolumetricShafts.shader` now resolves a shared `ResolveTemporalR2Offset()` and samples `_BlueNoiseTex` using the imported texel size instead of a hard-coded `64`.
  - `Hecton_AegirHazeOverlay.shader` now uses the same R2 offset and falls back to `_Time.y * 60` if `_HectonFrameCount` is not available on that pass.
- Tightened the flashlight voxel-SDF audit path in `Hecton_CoreLit.hlsl` by hoisting `HECTON_FLASHLIGHT_SDF_SHADOW_MAX_STEPS` into an explicit `maxVoxelShadowSteps` constant used by both the clamp and the loop bound.
- Hardened `HectonFBXPostprocessor.cs` so any importer that enables `generateSecondaryUV` is forced onto deterministic unwrap settings:
  - `secondaryUVHardAngle = 88`
  - `secondaryUVAngleDistortion = 8`
  - `secondaryUVAreaDistortion = 15`
  - `secondaryUVPackMargin = 4`

### Regression Model

- CPU: the new ocean cache bootstrap runs on startup and SlowTick only. It does not add any per-frame Update work.
- GC: no new managed allocations were introduced in gameplay hot paths. The only cold allocation added is the missing-cache child recovery path, and that executes only if prefab authoring data is broken.
- Memory: the fix adds one Crest sea-floor depth cache render target, which is already part of Crest's intended sea-floor simulation path. Resolution is capped at `512` for the MX350 baseline.
- Cadence: terrain-bound reconfiguration only occurs when terrain coverage or water level changes, not every frame.
- Correctness: the prefab now contains a real `OceanDepthCache` source of truth, and the bootstrap explicitly populates it from terrain bounds instead of leaving Crest to bind null sea-floor depth data.

### Verification

- Source evidence:
  - `Ocean_Crest.prefab` now contains a serialized `OceanDepthCache` child and a `HectonCrestOceanDepthCacheBootstrap` root component.
  - `HectonCrestOceanDepthCacheBootstrap.cs` computes world terrain bounds from `Terrain.activeTerrains`, squares the cache footprint for Crest, aligns it to water level, and calls `PopulateCache(updateComponents: true)`.
  - `Hecton_ScooterVolumetricShafts.shader` and `Hecton_AegirHazeOverlay.shader` both now carry explicit `ResolveTemporalR2Offset()` helpers.
  - `Hecton_CoreLit.hlsl` now exposes the 24-step cap through `maxVoxelShadowSteps`.
  - `HectonFBXPostprocessor.cs` now pins secondary UV unwrap settings when importer-driven UV2 generation is enabled.
- Build:
  - `Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj` remain `PENDING VERIFICATION` because the generated Unity project graph in this workspace is missing required metadata assemblies from `Temp/bin/Debug` and several Unity package references. The failure mode is external metadata resolution, not a confirmed syntax error inside the edited files.
- Live Unity proof:
  - `PENDING VERIFICATION`. No live editor render or runtime cache-population log was available in this pass, so water opacity restoration is still awaiting user-side Unity confirmation.

## Iteration 21 - Crest Cache Alignment / Render Ordering / Cave Ambient AO

### Mandates Followed

- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `PROJECT_LTS_Compatibility_Layer.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

### What Was Wrong

- `HectonCrestOceanDepthCacheBootstrap.cs` still treated `MapMagicBridge.WaterSurfaceLevel` as the preferred Y source and still left Crest's private `_cameraMaxTerrainHeight` at the serialized placeholder value. Crest positions the cache camera at `transform.position + Vector3.up * _cameraMaxTerrainHeight`, so a stale height field leaves the depth camera below or too close to the actual terrain peak.
- `HectonScooterVolumetricShaftsFeature` still defaulted to `BeforeRenderingPostProcessing`, and every serialized URP renderer asset still stored `injectionPoint: 550`. That places the shaft composite too late in the pipeline for Crest water and camera-space overlays.
- The project still had no first-party owner for cave ambient/reflection darkening. There is no world-space global voxel density texture in the repo. The only exported `_VoxelDensityTex` owner in source is the flashlight-local shadow volume.

### What Changed

- Added `HectonCrestOceanDepthCacheRuntimeBridge.cs` as a first-party partial extension on `Crest.OceanDepthCache`.
  - This exposes a controlled bridge for `_cameraMaxTerrainHeight`, `_refreshMode`, `_layers`, `_resolution`, and `_relative` without reflection and without wrapping Crest runtime materials.
- Tightened `HectonCrestOceanDepthCacheBootstrap.cs`.
  - Water level now resolves from `OceanRenderer.SeaLevel` first, with `MapMagicBridge.WaterSurfaceLevel` only as fallback.
  - Terrain aggregation now carries `terrainTopY` and derives Crest camera height as `terrainTopY - seaLevel`, clamped to a minimum safety margin.
  - `ApplyDepthCacheSettings()` and populate both call the Crest partial bridge so `_cameraMaxTerrainHeight` is no longer left at the static prefab placeholder.
  - Added a cold recovery fallback to `Resources.FindObjectsOfTypeAll<Terrain>()` when Unity's `Terrain.activeTerrains` cache is empty during authoring/runtime recovery.
- Moved volumetrics to `BeforeRenderingTransparents`.
  - `HectonScooterVolumetricShaftsFeature.cs` default injection/fallback now uses `RenderPassEvent.BeforeRenderingTransparents`.
  - `PC_Renderer.asset`, `PC_High_Renderer.asset`, and `Mobile_Renderer.asset` all now serialize `injectionPoint: 450` for the shafts feature.
  - `HectonRenderPipelineValidator.cs` now emits a hard error if either `HectonScooterVolumetricShaftsFeature` or `HectonAbyssalSsdoFeature` drifts away from `BeforeRenderingTransparents`.
- Added `HectonCaveVoxelAmbientOcclusionController.cs` and attached it to `Ocean_Crest.prefab`.
  - This is a bounded first-party cave owner, not a fake reuse of the flashlight-local `_VoxelDensityTex`.
  - Production path now pulls active `HectonVoxelVolume` instances from `WorldCaveDirector`'s runtime registry. `FindObjectsByType<HectonVoxelVolume>()` remains only as a recovery/editor fallback when the cave director is absent.
  - It resolves viewer position from player/camera ownership, computes cave occlusion from voxel-volume local bounds depth, and darkens `RenderSettings.ambientIntensity` / `RenderSettings.reflectionIntensity` with zero per-frame allocations.
- Extended `RenderSettingsLifecycleGuard.cs` to capture and restore `RenderSettings.reflectionIntensity` so the new cave AO owner does not leak reflection dimming after release.

### Regression Model

- CPU: cache alignment remains startup/SlowTick only. Cave AO adds one tick-time scalar blend and one SlowTick cave-volume refresh through `WorldCaveDirector`. No new heavy per-frame loops or GPU passes were added.
- GC: no new managed allocations were introduced in hot paths. The only new allocations are cold-path terrain recovery (`Resources.FindObjectsOfTypeAll<Terrain>()`) and the cave-volume fallback scan (`FindObjectsByType<HectonVoxelVolume>()`) used only when `WorldCaveDirector` is unavailable.
- Memory: no new persistent render targets were added beyond the already-intended Crest cache. Cave AO stores only a cached `HectonVoxelVolume[]`.
- Cadence: volumetric ordering is now fixed at the renderer-asset level, preventing late post-process composition from bleeding over water/UI. Cave AO re-evaluates volume ownership on SlowTick and blends on Tick.
- Correctness: the Crest depth cache now aligns to `OceanRenderer.SeaLevel` and a derived terrain-top camera height instead of a hardcoded placeholder. The cave AO implementation is intentionally bounded to `HectonVoxelVolume` ownership because no world-space voxel-density texture exists in the current repo.

### Verification

- Source evidence:
  - `HectonCrestOceanDepthCacheBootstrap.cs` now derives `cameraMaxTerrainHeight` from aggregated terrain top Y and `OceanRenderer.SeaLevel`.
  - `PC_Renderer.asset`, `PC_High_Renderer.asset`, and `Mobile_Renderer.asset` now serialize `HectonScooterVolumetricShaftsFeature.settings.injectionPoint: 450`.
  - `HectonRenderPipelineValidator.cs` now errors if the volumetric or SSDO features drift away from `BeforeRenderingTransparents`.
  - `Ocean_Crest.prefab` now carries `HectonCaveVoxelAmbientOcclusionController`.
- Live Unity proof:
  - `PENDING VERIFICATION`. No fresh runtime frame or editor-side cache-populate log was available in this pass.

## Iteration 23 - Crest Init Order / Screenshot Import Domain / Self-Intersecting FBX Import Flags

### Mandates Followed

- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `PROJECT_LTS_Compatibility_Layer.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

### Source Changes

- `Assets/_Project/Scripts/World/HectonCrestOceanDepthCacheBootstrap.cs`
  - Removed all `ResolveWaterLevel()` / cache-generation work from `Awake()`.
  - `Start()` now early-outs if `Crest.OceanRenderer.Instance == null`.
  - `SlowTick()` now early-outs while Crest is uninitialized and keeps diagnostics on the fallback water level.
  - Added `ResolveFallbackWaterLevel()` so diagnostics and deferred cache setup do not touch `OceanRenderer.SeaLevel` before Crest bootstraps.
- `Assets/Feel/MMTools/Editor/MMUtilities/MMScreenshotEditor.cs`
  - Screenshot path now resolves to `Application.dataPath/../Screenshots` explicitly instead of relying on the editor working directory.
- `Assets/_Project/Scripts/Editor/HectonFBXPostprocessor.cs`
  - Added a bounded offender list derived from import logs.
  - For those files, importer policy now forces `ModelImporterMeshCompression.Off` and `optimizeMeshPolygons = false`.

### Import-Log Inputs Used

- `.iter17c_unity_batch.log:5316-5323`
  - `Assets/ScifiFacility/Models/props/prop_15.fbx`
- `.iter17c_unity_batch.log:11926-11941`
  - `Assets/ScifiFacility/Models/structural/walls/viewing_deck.fbx`

### Verification

- Filesystem:
  - `Assets/Screenshots=False`
  - `Assets/Screenshots.meta=False`
  - `Screenshots=True`
- Build:
  - `Assembly-CSharp.csproj` -> `0 Error(s)`
  - `Hecton8.Editor.csproj` -> `0 Error(s)`
  - `MoreMountains.Tools.Editor.csproj` -> `0 Error(s)`
- Live Unity proof:
  - `PENDING VERIFICATION`
