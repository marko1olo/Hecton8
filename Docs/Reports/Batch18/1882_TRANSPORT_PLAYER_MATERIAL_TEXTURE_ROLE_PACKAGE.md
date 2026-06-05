# 1882 Transport + Player Material Texture Role Package

Date: 2026-06-04
Agent: 1882
Evidence class: STATIC_SOURCE / STATIC_DOC
Unity/build/runtime: NOT RUN

## Scope

Report-only material/texture source audit for:

- Transport: `CargoSled`, `ExosuitFrame`, `MicroSub`, `ScoutGlider`
- Player suit/visor roles: `PLAYER_FP_GLOVES_FOREARMS`, `PLAYER_TORSO_PELVIS_LEGS_FINS`, `PLAYER_HELMET_VISOR_HOUSING`, `PLAYER_VISOR_GLASS_RIM`, `PLAYER_LABELS_LATCHES_INSTRUMENT_TRIMS`

Owned outputs:

- `Docs/Reports/Batch18/1882_TRANSPORT_PLAYER_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1882_TRANSPORT_PLAYER_MATERIAL_TEXTURE_MATRIX.csv`
- `Docs/Tasks/Status_1882.md`
- `Docs/AgentLogs/Rationale_1882.md`
- `Docs/AgentLogs/LOG_1882.md`

No source, prefab, Unity asset, scene, `.meta`, binary, generated mesh, Unity menu, import, bake, PlayMode, profiler, dotnet build, or Data Monolith operation was run.

## Authorities Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `player.md`
- `tools.md`
- `survival.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/MATH_AUP_Determinism_Sync.txt`
- `Docs/Reports/Batch18/1871_TRANSPORT_VISUAL_SOURCE_PACKAGE.md`
- `Docs/Reports/Batch18/1872_PLAYER_BODY_VISUAL_SOURCE_PACKAGE.md`
- `Docs/Reports/Batch18/1876_TRANSPORT_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md`
- `Docs/Reports/Batch18/1879_PRODUCT_FACE_RELINK_AND_PROOF_CONTRACT.md`

`Docs/Actual Domains of Project.txt` is absent. Narrow domain used: product-face transport/player material and texture role audit.

## Static Baseline

Current product-face visuals are not material-ready:

- `CargoSled`, `ExosuitFrame`, `MicroSub`, and `ScoutGlider` transport prefabs still use built-in cube mesh `m_Mesh: {fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}` at line `49` in each transport prefab, with unresolved/default material GUID `31321ba15b8f8eb4c954353edc038b1d` at line `96`.
- `Player.prefab` still contains active built-in cube body MeshFilters and primitive `Suit_Visor` sphere mesh `m_Mesh: {fileID: 10207, guid: 0000000000000000e000000000000000, type: 0}` at line `2819`.
- Multiple player body parts still reference unresolved/default material GUID `31321ba15b8f8eb4c954353edc038b1d`.
- `MAT_PlayerSwimBlockout` is a known blockout material, not a product material source.

Static evidence cannot prove visual quality, Unity import health, SRP Batcher state, material render response, compact-tier readability, or profiler cost. All material acceptance remains `PENDING VERIFICATION`.

## Credible Candidate Pool

### Project-Owned Candidate Materials

Transport hull/metal/composite candidate paths:

- `Assets/_Project/Art/RuntimeShell1428/MAT_H8Shell_PressureHull.mat`
- `Assets/_Project/Art/RuntimeShell1428/H8_Shell_Submarine_WetSteel.mat`
- `Assets/_Project/Art/RuntimeShell1428/H8_Shell_1428_WetGraphite.mat`
- `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_WetPressureMetal.mat`
- `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_WetEdgeSteel.mat`
- `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_BlackHullNoir.mat`

Transport/player glass candidate paths:

- `Assets/_Project/Art/Materials/Mat_Visor_Glass.mat`
- `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_DirtyPressureGlass.mat`
- `Assets/_Project/Art/RuntimeShell1428/H8_Shell_1428_BlackGlass.mat`
- `Assets/_Project/Art/RuntimeShell1428/H8_Shell_1428_HazeGlass.mat`

Player suit/trim candidate paths:

- `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_PlayerSuitGraphiteNoir.mat`
- `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_PlayerSuitCyanEdge.mat`
- `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_PlayerSuitAmberLatch.mat`
- `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_WornLabel.mat`
- `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_WornLabelWhite.mat`
- `Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_ReadableWornLabelWhite.mat`

Construction/ocean-adjacent support candidates:

- `Assets/_Project/Art/Materials/Construction/MAT_Equipment_Atlas.mat`
- `Assets/_Project/Art/Materials/Construction/Mat_LeakWetSheen.mat`
- `Assets/_Project/Art/Materials/World/MAT_SurfaceDropPodCharredHull_1428.mat`
- `Assets/_Project/Art/Materials/World/MAT_H8SurfaceWetBasaltReal_1428.mat`
- `Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat`

### Project-Owned Candidate Textures

Visor glass texture candidates:

- `Assets/_Project/Art/TEXTURES/Detali/visor runoff normal.png`
- `Assets/_Project/Art/TEXTURES/Detali/visor droplet mask.png`

Other material support textures:

- `Assets/_Project/Art/TEXTURES/Detali/Mineral Seep Mask - second try.png`
- `Assets/_Project/Art/TEXTURES/Detali/mineral seep mask - looks seamless.png`
- `Assets/_Project/Art/TEXTURES/H8_RustDetailAtlas.asset`
- `Assets/_Project/Art/TEXTURES/TX_H8_SurfaceWaterNormals_1428.asset`
- `Assets/_Project/Art/TEXTURES/TX_SurfaceBasaltWetStrata_1428.asset`
- `Assets/_Project/Art/TEXTURES/TX_H8SurfaceBasaltWetSediment_1428.asset`

The visor path is the strongest static texture match: `Mat_Visor_Glass.mat` binds `SuitVisor.shader`, `_WaterDropletMaskTex` resolves to `visor droplet mask.png`, and `_WaterRunoffNormalTex` resolves to `visor runoff normal.png`.

## Candidate Limits

Most inspected `RuntimeVisualProof` and `RuntimeShell1428` materials are semantic color/material-slot candidates, not final PBR texture sets:

- `MAT_RuntimeVisualProof_WetPressureMetal.mat`, `MAT_RuntimeVisualProof_PlayerSuitGraphiteNoir.mat`, `MAT_RuntimeVisualProof_PlayerSuitCyanEdge.mat`, `MAT_RuntimeVisualProof_PlayerSuitAmberLatch.mat`, `MAT_H8Shell_PressureHull.mat`, and `H8_Shell_Submarine_WetSteel.mat` have no assigned `_BaseMap`, `_BumpMap`, `_MetallicGlossMap`, or `_OcclusionMap` in inspected YAML.
- `MAT_RuntimeVisualProof_DirtyPressureGlass.mat` is a transparent glass candidate but has no bound scratch/droplet/normal texture set.
- `MAT_Equipment_Atlas.mat` declares `_BaseMap`, `_MaskMap`, `_BumpMap`, rust/detail/array slots, and instancing, but inspected YAML shows those texture slots unassigned. It is a shader/material shell, not proven equipment atlas content.
- `Mat_LeakWetSheen.mat` uses third-party ScifiFacility shader/normal dependencies plus a first-party seep mask. It is support reference only, not a first-party transport/player final material source.

## Role Matrix Summary

Detailed row-level mapping is in:

`Docs/Reports/Batch18/1882_TRANSPORT_PLAYER_MATERIAL_TEXTURE_MATRIX.csv`

Result by required role:

- `TRANSPORT_CargoSled_*`: `MISSING_SOURCE_REQUIRED`. Existing hull/label candidates are usable only as semantic slot references. Rubber/grip source is missing.
- `TRANSPORT_ExosuitFrame_*`: `MISSING_SOURCE_REQUIRED`. Metal and suit trim candidates exist. Hydraulic/rubber/seal maps are missing.
- `TRANSPORT_MicroSub_*`: `MISSING_SOURCE_REQUIRED`. Wet steel and glass candidates exist. Micro-sub viewport scratch/droplet and hull panel PBR maps are missing.
- `TRANSPORT_ScoutGlider_*`: `MISSING_SOURCE_REQUIRED`. Graphite/hull/glass/signal candidates exist. Glider body/fins/grips/lens texture maps are missing.
- `PLAYER_FP_GLOVES_FOREARMS`: `MISSING_SOURCE_REQUIRED`. Suit graphite/cyan/amber materials exist but no glove/forearm texture set exists.
- `PLAYER_TORSO_PELVIS_LEGS_FINS`: `MISSING_SOURCE_REQUIRED`. No full-body suit material atlas exists.
- `PLAYER_HELMET_VISOR_HOUSING`: `MISSING_SOURCE_REQUIRED`. No helmet housing/rim texture set exists.
- `PLAYER_VISOR_GLASS_RIM`: `PARTIAL_SOURCE_STATIC`. `Mat_Visor_Glass`, `SuitVisor.shader`, `visor runoff normal.png`, and `visor droplet mask.png` are credible static sources, but scratch/fingerprint/grime maps and mesh replacement are missing.
- `PLAYER_LABELS_LATCHES_INSTRUMENT_TRIMS`: `MISSING_SOURCE_REQUIRED`. Color/emission candidates exist, but no decal/label/trim atlas or packed emission/wear masks are proven.

## Rejected Sources

Rejected for product-face material source use:

- Unresolved/default material GUID `31321ba15b8f8eb4c954353edc038b1d`.
- Unity built-in primitive materials and Unity/package-cache `Lit.mat` routes.
- `Assets/_Project/Art/Materials/Gameplay/MAT_PlayerSwimBlockout.mat`.
- `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/**`.
- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/**` material routes.
- `Assets/Shapes/**` generated/subtractive/debug materials.
- Third-party `Assets/ScifiFacility/**` materials/textures as final first-party sources without separate license/import/ownership proof and material-role rewrite.
- Flat color-only materials as final hull/suit/glass/rubber/decal sources.

## Required Shader And Texture Semantics

Every future transport/player material package must document:

- Albedo: color only; no baked lighting, no false AO shadows, no AI garbage illumination.
- Normal: BC5 where platform supports it; visor runoff/scratch normals must be separate or packed by an explicit shader contract.
- Packed MRAO: default HECTON-8 convention is R = Metallic, G = Roughness or Smoothness stated by shader contract, B = AO, A = Emission/Wetness/Family mask.
- Wetness: mask-driven, not whole-object gloss paint; must expose salt, waterline, droplet, edge-wear and grime differences.
- Glass/scratch/droplet: visor and transport viewports need glass base, scratch normal, droplet mask, runoff normal, grime/fingerprint mask where relevant, plus compact-safe readability fallback.
- Emissive trim: cyan/green instrument strips and amber warnings/latches must use masks. Emission may signal state but cannot invent gameplay truth.
- Decal/label/wear: service labels, latch marks, clamp scars, hazard paint, serial tags, worn seals, and tool-contact wear need atlas or trim-sheet source paths.

## Collider And Anchor Boundaries

This material package cannot change gameplay truth:

- Player root `CapsuleCollider` remains movement/body truth.
- `HandAnchor`, camera stack, `Suit_Diegetic_HUD_V4_Projection`, `Suit_Visor`, `VisorHUDController`, `SuitHUDPresentationController`, `PlayerToolManager.handAnchor`, `PlayerSwimPresentationController`, and `Swim_*Attachment` transforms must remain stable.
- Transport `RiderAnchor` and `DismountAnchor` must remain stable unless a future vehicle owner approves an anchor migration with runtime capture and proof.
- Transport occupancy, drive, mount/dismount, preset, AUP, kinematic, and camera-feel routes are not material concerns.
- No visual mesh may become movement or vehicle collision. Future colliders must be explicit `COL_*` primitives/proxies.

## Continuous Quality Scaling

`GlobalQualityWeight` affects material richness only. It must not change gameplay truth, collider identity, save identity, DTO layout, transport presets, anchors, or survival/tool/movement formulas.

- Compact `0.00-0.35`: strong silhouette, readable material families, baked AO/detail masks, reduced mip demand, no ugly flat blockout fallback, no transparent overdraw spam.
- Middle `0.35-0.65`: clearer grime/labels/gasket seams, stronger rubber/glass/metal distinction, longer near-field LOD residency where budget allows.
- High `0.65-0.90`: richer detail normals, wetness response, edge wear, visor scratches/droplets, clearer cyan/amber trim masks.
- Ultra `0.90-1.00`: micro bolts, secondary straps/cables, richer grime/wetness/scratch overlays, longer near-field material residency and optional extra decal density. No new gameplay truth.

## Future Unity Proof Steps

Do not claim acceptance until a future Unity owner runs proof from the same Unity state:

1. Import/author final `TX_*` albedo, normal, packed MRAO, wetness, glass scratch/droplet/runoff, decal/label/wear textures.
2. Bind final shared `MAT_*` assets to project-owned non-primitive meshes under the future player/transport visual source routes.
3. Prove no active built-in primitive body/transport MeshFilters remain on product-face visual roots.
4. Run product-face prefab validator and generated asset audit.
5. Capture screenshots:
   - first-person gloves/forearms with tool held;
   - visor glass close view with HUD/glass/rim readable;
   - third-person or external player body with torso/pelvis/legs/fins;
   - each transport near boarding view: `CargoSled`, `ExosuitFrame`, `MicroSub`, `ScoutGlider`;
   - compact and high tier material readability comparisons.
6. Capture Frame Debugger/RenderGraph/SRP Batcher proof if shaders/material paths change.
7. Capture Profiler/GC/memory/VRAM proof if runtime material logic, render features, transparency, VFX, or LOD behavior changes.

## Acceptance Boundary

This report proves only static text and asset-path facts. It does not prove:

- Unity import health.
- Render correctness.
- screenshot quality.
- compact-tier readability.
- SRP Batcher or instancing compatibility.
- runtime material cost.
- collider/anchor runtime behavior.
- replacement mesh existence or visual acceptance.

Status remains `PENDING VERIFICATION`.
