# 2104 Primitive Null Default Static Validator

## Evidence Boundary

- Evidence class: `STATIC_SOURCE`.
- Visual acceptance: `PENDING VERIFICATION`.
- This report is static text/YAML evidence only.
- It does not prove runtime binding, import state, prefab override application, route visuals, frame cost, player safety, or build safety.
- Do not close visual debt from this report alone.

## Scope

- Scanned files: `939`.
- Total findings: `3008`.
- Active scene findings: `346`.
- CSV detail: `Docs/Reports/Batch31/PrimitiveNullDefaultStaticValidator_batch31.csv`.
- JSON detail: `Docs/Reports/Batch31/PrimitiveNullDefaultStaticValidator_batch31.json`.

## Check Matrix

| Check | Evidence | Closure rule |
| --- | --- | --- |
| Built-in primitive mesh refs | `m_Mesh` source references to Unity built-in primitive fileIDs | Replace with authored mesh/prefab, then Unity-owner route proof |
| Null renderer material slots | `m_Materials` source entries with `fileID: 0` | Bind authored material or remove renderer slot, then Unity-owner route proof |
| Default/package/proxy materials | Renderer or material asset paths with default/vendor/proxy tokens | Replace with route-owned authored material, then inspect active scene overrides |
| Unresolved material GUIDs | Renderer material GUID absent from scanned meta index | Resolve import/meta state in Unity owner pass |
| Unresolved texture GUIDs | Material texture GUID absent from scanned meta index | Resolve source texture/import state in Unity owner pass |
| Empty base texture slots | Base/albedo texture property is null | Confirm material design intent or bind authored texture before route acceptance |

## Summary

- By severity: `{"CRITICAL": 1947, "HIGH": 875, "LOW": 7, "MEDIUM": 179}`.
- By issue type: `{"BUILTIN_PRIMITIVE_MESH_REF": 1097, "EMPTY_BASE_TEXTURE_SLOT": 402, "NULL_RENDERER_MATERIAL_SLOT": 26, "PLACEHOLDER_OR_PROXY_MATERIAL_ASSET": 83, "PLACEHOLDER_OR_PROXY_MATERIAL_REF": 1376, "UNRESOLVED_MATERIAL_GUID": 20, "UNRESOLVED_TEXTURE_GUID": 4}`.
- By route band: `{"diagnostic_only_candidate": 7, "placeholder_proxy_candidate": 92, "product_face": 782, "surface_sky_photic_medium_product_face": 1735, "unknown_candidate": 392}`.

## Top Findings

| Severity | Issue | Path:Line | Hint | Slot/Property | Detail |
| --- | --- | --- | --- | --- | --- |
| CRITICAL | UNRESOLVED_TEXTURE_GUID | `Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat:52` | MAT_H8_SurfaceCrestOcean_1428 | `_MainTex` | Texture GUID is not present in the scanned Assets/Packages meta index. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/MAT_H8_SurfaceFoamRibbons_1428.mat:45` | MAT_H8_SurfaceFoamRibbons_1428 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/MAT_H8_SurfaceFoamRibbons_1428.mat:49` | MAT_H8_SurfaceFoamRibbons_1428 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/MAT_H8WorldWaterMassVeil_1428.mat:45` | MAT_H8WorldWaterMassVeil_1428 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/MAT_H8WorldWaterMassVeil_1428.mat:49` | MAT_H8WorldWaterMassVeil_1428 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/Mat_HectonSky.mat:45` | Mat_HectonSky | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/Mat_HectonSky.mat:85` | Mat_HectonSky | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/Mat_HectonSky_CloudOverlay.mat:45` | Mat_HectonSky_CloudOverlay | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/Mat_HectonSky_CloudOverlay.mat:85` | Mat_HectonSky_CloudOverlay | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/Mat_TriplanarRock.mat:45` | Mat_TriplanarRock | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/Mat_TriplanarRock.mat:69` | Mat_TriplanarRock | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_AbyssalKelpSilhouette.mat:29` | MAT_RuntimeVisualProof_AbyssalKelpSilhouette | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_AbyssalKelpSilhouette.mat:53` | MAT_RuntimeVisualProof_AbyssalKelpSilhouette | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_BasaltWet.mat:29` | MAT_RuntimeVisualProof_BasaltWet | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_BasaltWet.mat:53` | MAT_RuntimeVisualProof_BasaltWet | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_ReadableWetBasalt.mat:29` | MAT_RuntimeVisualProof_ReadableWetBasalt | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_ReadableWetBasalt.mat:53` | MAT_RuntimeVisualProof_ReadableWetBasalt | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/Skybox.mat:59` | Skybox | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/terrain 1.mat:29` | terrain 1 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/terrain 1.mat:53` | terrain 1 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/terrain 2.mat:29` | terrain 2 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/terrain 2.mat:53` | terrain 2 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | UNRESOLVED_TEXTURE_GUID | `Assets/_Project/Art/Materials/terrain.mat:41` | terrain | `_BaseMap` | Texture GUID is not present in the scanned Assets/Packages meta index. |
| CRITICAL | UNRESOLVED_TEXTURE_GUID | `Assets/_Project/Art/Materials/terrain.mat:65` | terrain | `_MainTex` | Texture GUID is not present in the scanned Assets/Packages meta index. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/MAT_SurfaceDropPodCharredHull_1428.mat:29` | MAT_SurfaceDropPodCharredHull_1428 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/MAT_SurfaceDropPodCharredHull_1428.mat:53` | MAT_SurfaceDropPodCharredHull_1428 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/MAT_SurfaceEmergencyAmber_1428.mat:42` | MAT_SurfaceEmergencyAmber_1428 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/MAT_SurfaceEmergencyAmber_1428.mat:46` | MAT_SurfaceEmergencyAmber_1428 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/MAT_SurfaceHorizonSaltHaze_1428.mat:32` | MAT_SurfaceHorizonSaltHaze_1428 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/MAT_SurfaceHorizonSaltHaze_1428.mat:36` | MAT_SurfaceHorizonSaltHaze_1428 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/MAT_SurfaceMoonCold_1428.mat:30` | MAT_SurfaceMoonCold_1428 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/MAT_SurfaceMoonCold_1428.mat:34` | MAT_SurfaceMoonCold_1428 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/MAT_SurfaceSplashFoamDirty_1428.mat:48` | MAT_SurfaceSplashFoamDirty_1428 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/MAT_SurfaceSunDisc_1428.mat:42` | MAT_SurfaceSunDisc_1428 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/MAT_SurfaceSunDisc_1428.mat:46` | MAT_SurfaceSunDisc_1428 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/MAT_WorldReadableWaterVeil_1428.mat:45` | MAT_WorldReadableWaterVeil_1428 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/MAT_WorldReadableWaterVeil_1428.mat:49` | MAT_WorldReadableWaterVeil_1428 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_MutedCoralAccent_1447.mat:29` | MAT_H8_MutedCoralAccent_1447 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_MutedCoralAccent_1447.mat:53` | MAT_H8_MutedCoralAccent_1447 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_MutedKelpAccent_1447.mat:29` | MAT_H8_MutedKelpAccent_1447 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_MutedKelpAccent_1447.mat:53` | MAT_H8_MutedKelpAccent_1447 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticBackReefRidge_1432.mat:42` | MAT_H8_PhoticBackReefRidge_1432 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticBackReefRidge_1432.mat:66` | MAT_H8_PhoticBackReefRidge_1432 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticBranchCoralNatural_1429.mat:29` | MAT_H8_PhoticBranchCoralNatural_1429 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticBranchCoralNatural_1429.mat:53` | MAT_H8_PhoticBranchCoralNatural_1429 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticCeilingCausticRibbons_1429.mat:27` | MAT_H8_PhoticCeilingCausticRibbons_1429 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticCoralBranching_1428.mat:59` | MAT_H8_PhoticCoralBranching_1428 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticCoralLow_1428.mat:59` | MAT_H8_PhoticCoralLow_1428 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticCoralMassive_1428.mat:59` | MAT_H8_PhoticCoralMassive_1428 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticFanCoralNatural_1429.mat:42` | MAT_H8_PhoticFanCoralNatural_1429 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticFanCoralNatural_1429.mat:66` | MAT_H8_PhoticFanCoralNatural_1429 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticFishSilhouette_1430.mat:45` | MAT_H8_PhoticFishSilhouette_1430 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticFishSilhouette_1430.mat:49` | MAT_H8_PhoticFishSilhouette_1430 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticMotes_1428.mat:42` | MAT_H8_PhoticMotes_1428 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticMutedRubble_1447.mat:29` | MAT_H8_PhoticMutedRubble_1447 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticMutedRubble_1447.mat:53` | MAT_H8_PhoticMutedRubble_1447 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticTubeSpongeNatural_1429.mat:29` | MAT_H8_PhoticTubeSpongeNatural_1429 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticTubeSpongeNatural_1429.mat:53` | MAT_H8_PhoticTubeSpongeNatural_1429 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticWarmBasaltRubble_1429.mat:42` | MAT_H8_PhoticWarmBasaltRubble_1429 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticWarmBasaltRubble_1429.mat:66` | MAT_H8_PhoticWarmBasaltRubble_1429 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_SurfaceDropPodPanel_1430.mat:29` | MAT_H8_SurfaceDropPodPanel_1430 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_SurfaceDropPodPanel_1430.mat:53` | MAT_H8_SurfaceDropPodPanel_1430 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_SurfaceFoamRing_1432.mat:27` | MAT_H8_SurfaceFoamRing_1432 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_VisibleFoamUnlit_1436.mat:32` | MAT_H8_VisibleFoamUnlit_1436 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_VisibleFoamUnlit_1436.mat:36` | MAT_H8_VisibleFoamUnlit_1436 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1453/MAT_H8_HeroTubeCoralAmber_1453.mat:29` | MAT_H8_HeroTubeCoralAmber_1453 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1453/MAT_H8_HeroTubeCoralAmber_1453.mat:53` | MAT_H8_HeroTubeCoralAmber_1453 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1453/MAT_H8_HeroTubeCoralCyan_1453.mat:29` | MAT_H8_HeroTubeCoralCyan_1453 | `_BaseMap` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1453/MAT_H8_HeroTubeCoralCyan_1453.mat:53` | MAT_H8_HeroTubeCoralCyan_1453 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1457/MAT_H8_PhoticCoralBranching_1457.mat:72` | MAT_H8_PhoticCoralBranching_1457 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1457/MAT_H8_PhoticCoralBrittle_1457.mat:56` | MAT_H8_PhoticCoralBrittle_1457 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1457/MAT_H8_PhoticCoralLow_1457.mat:59` | MAT_H8_PhoticCoralLow_1457 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1457/MAT_H8_PhoticCoralMassive_1457.mat:72` | MAT_H8_PhoticCoralMassive_1457 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1457/MAT_H8_PhoticCoralPlate_1457.mat:72` | MAT_H8_PhoticCoralPlate_1457 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1457/MAT_H8_PhoticKelpPatch_1457.mat:69` | MAT_H8_PhoticKelpPatch_1457 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/World/Photic1457/MAT_H8_PhoticKelpTall_1457.mat:56` | MAT_H8_PhoticKelpTall_1457 | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat:72` | MAT_family_coral_branching | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_brittle.mat:56` | MAT_family_coral_brittle | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_low.mat:59` | MAT_family_coral_low | `_MainTex` | Primary/base texture property is empty in material source. |
| CRITICAL | EMPTY_BASE_TEXTURE_SLOT | `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_massive.mat:72` | MAT_family_coral_massive | `_MainTex` | Primary/base texture property is empty in material source. |
| INFO | TRUNCATED | Full CSV |  |  | 2928 additional rows omitted from Markdown table. |

## Severity Rules

- `CRITICAL`: active scene debt, or surface/sky/ocean/photic/medium/product-face route debt that can violate the visual floor.
- `HIGH`: first-party product-face, placeholder/proxy, or unresolved source debt outside the active scene.
- `MEDIUM`: first-party prefab/material debt without enough route tokens for higher routing.
- `LOW`: diagnostic/editor/test candidates only.

## Scalability Consequences

- Low: remove primitives/nulls first; use authored low-cost meshes/materials that still preserve the route visual floor.
- Middle: bind stable PBR roles and first-pass LODs so debt does not reappear through scene overrides.
- High: upgrade surface/photic/medium route assets with richer material response after static debt is cleared.
- Ultra: spend recovered cost on premium route detail only after authored assets and runtime proof exist.

## Excluded Checks

- No Unity Editor execution.
- No import, Play Mode, profiler, scene mutation, prefab mutation, material mutation, or build command.
- No screenshot, capture, or visual quality acceptance.
- No claim that a path exists in source equals a bound runtime asset.

## Unity Owner Handoff

- Address `CRITICAL` active scene rows first.
- Inspect scene overrides because source prefabs can pass while active scene instances still contain primitives or null/default slots.
- Use the CSV as the queue; keep rows open until the Unity owner produces real scene/import/visual/profiler evidence.
