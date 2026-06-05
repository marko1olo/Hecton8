# Unity Owner Handoff 2026-06-04 - 1472 Foam/Caustics/Underwater Route

Target thread: `Продолжить работу по логам`
Mode: active-run handoff / evidence packet.

1472 remains NOT accepted.

Evidence reviewed:
- `Docs/Screenshots/MCP/h8_1472_surface_coast_aegir_ui_off.png`
- `Docs/Screenshots/MCP/h8_1472_shoreline_close_1m.png`
- `Docs/Screenshots/MCP/h8_1472_underwater_0_5m.png`
- `Docs/Screenshots/MCP/h8_1472_underwater_20_50m_route.png`
- `Docs/Screenshots/MCP/h8_1472_regression_low_oblique.png`
- Static sidecar inventory from `019e92b6-6093-7981-b1f0-d26bcb3269b8`
- Active-scene offender list from `019e92b0-905c-7f01-a3b6-9e23ea7444d2`

## Visual Reject

- Surface color is improved compared to 1466/1468, but still poor and flat.
- There is no credible shoreline foam.
- There is no visible caustics richness.
- Underwater is too transparent and empty.
- Underwater proof is still broken by a large yellow/white overhead plane or sheet.
- Terrain/shoreline still reads as a shell with weak authored breakup, not premium coastal geology.

## Strong Suspect For Yellow/White Underwater Plane

Inspect first:
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:79014` / `H8_UnderwaterSurfaceSheet_1455`
- `Assets/_Project/Art/Shaders/H8_UnderwaterSurfaceSheet_1455.shader`

Reason:
- Sidecar static inventory says the object is inactive in source, but its shader/material route has bright green-white values, `_Opacity`, and fixed `_SeaLevel`.
- If any 1469/1470/1471/1472 runtime-created or re-enabled variant is active or mismatched to camera sea level, it directly explains the yellow/white overhead plane.

Unity owner action:
1. Inspect active renderer bounds/material of `H8_UnderwaterSurfaceSheet_1455` and any clone/variant.
2. If it is the plane, either disable it for the proof path or retune it into a subtle water-surface read. It must not cover the underwater view as a solid sheet.
3. Do not replace the underwater plane with black/noir fog. Fix the route.

## Foam/Caustics/Haze Existing Routes To Inspect

Foam candidates:
- `02_HECTON_WORLD.unity:62124` / `H8_ShorelineFoamOrganic_1446`
- `02_HECTON_WORLD.unity:11874` / `H8_SurfaceFoamVertex_1437`
- `02_HECTON_WORLD.unity:53971` / `H8_SurfaceFoamLace_1453`
- `Assets/_Project/Art/Materials/World/MAT_H8SurfaceShoreFoam_1428.mat`
- `Assets/_Project/Scripts/VFX/JacobianFoam/HectonJacobianFoamRenderFeature.cs`
- `Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamGpuRuntime.cs`

Caustics candidates:
- `02_HECTON_WORLD.unity:93951` / `H8_FloorCausticPatches_1438`
- `Assets/_Project/Data/PC_Renderer.asset` / active deferred caustics feature
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` / `TryEnsureDeferredCausticsRegistered`
- `Assets/_Project/Scripts/Rendering/AbyssalCaustics/HectonDeferredCausticsFeature.cs`
- `Assets/_Project/Scripts/Rendering/AbyssalCaustics/AbyssalDeferredCausticsRuntime.cs`

Underwater richness candidates:
- `02_HECTON_WORLD.unity:93473` / `H8_UnderwaterHazeCurtain_1454`
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`
- `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs`
- `Assets/_Project/Scripts/Rendering/WaterOptics/WaterOpticsRuntime.cs`

## Active Scene Slab/Plane Offenders To Inspect

From `2104` static validator, prioritize these exact object hints:
- `BLACK_WATER_PLANE`
- `BASALT_SEABED`
- `ABYSS_SURFACE_CEILING`
- `ABYSS_BLACKWATER_CEILING_1428`
- `Water_Mass_Far_1428`
- `Water_Mass_Mid_1428`
- `H8_WORLD_LOW_WATER_OCCLUSION_00_1428`
- `H8_WORLD_LOW_WATER_OCCLUSION_01_1428`
- `H8_WORLD_LOW_WATER_OCCLUSION_02_1428`
- `H8_WORLD_LOW_WATER_OCCLUSION_03_1428`
- `NOIR_FAR_WATER_CURTAIN_A`
- `NOIR_FAR_WATER_CURTAIN_B`
- `NOIR_MIDWATER_VEIL_A`
- `NOIR_MIDWATER_VEIL_B`
- `NOIR_LEFT_VIGNETTE_SLAB`
- `NOIR_RIGHT_VIGNETTE_SLAB`

Safe action:
- Inspect renderer bounds and camera visibility.
- Disable only proven debug/proxy/occlusion slabs visible in current proof path.
- Do not delete assets or production candidates without reference proof.

## Immediate Proof Gate

Next proof packet must show:
- surface coast/Aegir with credible water color and no slab/horizon artifacts;
- shoreline close with visible, subtle, organic foam/contact breakup;
- underwater 0-5 m with depth tint, caustics, haze/motes or marine snow, and no overhead yellow sheet;
- underwater 20-50 m with readable but richer water volume, caustics/particles/depth falloff, not air-clear emptiness;
- regression low oblique;
- clean console/runtime proof, including material/property errors.

Do not claim acceptance until foam, caustics/depth richness, and underwater plane failure are all closed by screenshots.
