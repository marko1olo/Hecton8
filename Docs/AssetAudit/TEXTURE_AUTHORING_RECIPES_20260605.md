# Texture Authoring Recipes - 2026-06-05

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_DOC` + `STATIC_IMAGE_QA`.
Scope: non-Unity authoring recipe for texture owners. No files under `Assets` were changed.

Read before execution:

- `Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.md`
- `Docs/AssetAudit/TEXTURE_VISUAL_REVIEW_20260605.md`
- `Docs/AssetAudit/TEXTURE_MATERIAL_USAGE_REVIEW_20260605.md`
- `Docs/GeneratedAssets/AssetSystem_20260605/TEXTURE_AUTHORING_MANIFEST_3212_20260605.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `rendering.md`
- `streaming.md`

Mandates followed:

- `QA_Evidence_Text_Filter_Audit`
- `STRM_Async_Asset_Upload_Texture_Settings`
- `REND_URP_Graphics_HotPath_Optimization_HLOD`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory`

## P0 Foam / Contact Mask Recipe

Problem:

- `Assets/Crest/Crest/Textures/foam.png` is visually rejected as visible route waterline art.
- It is still serialized-reachable through active world/ocean users, so replacement priority is P0.

Source inputs:

- `Assets/Crest/Crest/Textures/foam.png`: reference only; do not direct-promote.
- `Assets/_Project/Art/TEXTURES/Detali/visor droplet mask.png`: bubble/droplet reference only.
- `Assets/_Project/Art/TEXTURES/Detali/visor runoff normal.png`: streak normal reference only.
- Existing shoreline/basalt/sand source sheets from `Docs/GeneratedAssets/Batch31_LocalPBR`.

Required generated pack:

- `TX_H8_FoamContact_Albedo_Source_YYYYMMDD.png`: low-contrast white/blue-gray foam residue, no turquoise pool pattern.
- `TX_H8_FoamContact_Normal_Source_YYYYMMDD.png`: shallow micro-ripple and residue breakup, no raised plastic web.
- `TX_H8_FoamContact_MRAO_Source_YYYYMMDD.png`: R metallic 0, G AO/contact residue, B smoothness variation, A optional emission/sparkle mask kept dark unless proven.
- `TX_H8_FoamContact_MaskRGBA_Source_YYYYMMDD.png`: R salt rim, G wet edge, B bubble breakup, A shoreline residue.

Visual rules:

- Foam must read as ocean/shore contact, not swimming-pool tiling.
- Mask must support thin edge breakup and wide wetness separately.
- Compact tier may reduce resolution/mip residency, not turn waterline into flat cyan noise.
- High/Ultra should spend saved cost on richer micro-breakup and wet edge material response.

Hard rejections:

- Do not use `foam.png` as visible final.
- Do not write Crest wrapper code or clone Crest materials.
- Do not create a material variant by raw YAML patch.
- Do not use fog, bloom, or darkness to hide foam weakness.

Proof gate:

- Contact sheet under `Docs/GeneratedAssets/...`.
- Manifest with channel packing and source references.
- Unity readback after gate clears: active material slot, scene user, import settings, screenshot, Frame Debugger/Stats.

Current source prototype:

- Folder: `Docs/GeneratedAssets/AssetSystem_20260605/FoamContactPrototype_20260605/`.
- Review: `Docs/AssetAudit/SOURCE_PROTOTYPE_REVIEW_20260605.md`.
- Status: source-only. Masks need cleanup before any import attempt.

Current cleanup pass:

- Folder: `Docs/GeneratedAssets/AssetSystem_20260605/CleanupPass_20260605/`.
- Manifest: `Docs/GeneratedAssets/AssetSystem_20260605/CleanupPass_20260605/CleanupPass_MANIFEST_20260605.md`.
- Review: `Docs/AssetAudit/SOURCE_PROTOTYPE_CLEANUP_REVIEW_20260605.md`.
- Status: source-only. Direction improved, but MRAO/RGBA remain too broad/false-color for direct import.

## P1 Aegir / Sky Cloud Composition Recipe

Problem:

- `TX_H8AegirGasGiantBakedDisc_1428.png` is serialized-reachable but visually prototype-only.
- Stronger ingredients exist but shader-slot proof is absent.

Source inputs:

- `Assets/_Project/Art/TEXTURES/clouds0_diff.png`: primary band/cloud ingredient.
- `Assets/_Project/Art/TEXTURES/Sky/bo3.png`: storm/vortex band ingredient.
- `Assets/_Project/Art/TEXTURES/Sky/oblakajip.png`: high-detail gas/cloud ingredient.
- `Assets/_Project/Art/TEXTURES/Aegir_storms.png`: storm mask/detail only.
- `Assets/_Project/Art/TEXTURES/TX_H8AegirGasGiantBakedDisc_1428.png`: reference/prototype only.
- `Assets/_Project/Art/TEXTURES/Sky/oblaka!.png`: sky cloud candidate, readback-blocked.

Required generated pack:

- `TX_H8_AegirBand_Albedo_Source_YYYYMMDD.png`: broad cloud bands with depth, no toy disc softness.
- `TX_H8_AegirStorm_Mask_Source_YYYYMMDD.png`: authored storm belts/vortices, not sparse black specks.
- `TX_H8_AegirCloud_Detail_Source_YYYYMMDD.png`: high-frequency detail for shader layering.
- Optional `TX_H8_AegirTerminator_LUT_Source_YYYYMMDD.png`: 1D or 2D lookup for cheap cinematic terminator/fake depth.

Visual rules:

- Aegir is a first-viewport signal for surface/orbit moments. It cannot look like a low-res marble.
- Keep surface/sky bright and legible. Noir darkness belongs to depth, storms, interiors, and temporary events.
- Low tier uses fewer samples and lower mips, not a flat gray/blue planet.
- Ultra tier should add richer band depth, storm detail, and terminator color response without changing gameplay truth.

Hard rejections:

- Do not use the baked disc as final hero planet.
- Do not accept `_HighCloudTex`/`_MainCloudAtlas` from stale YAML evidence.
- Do not raw-patch sky materials.

Proof gate:

- Generated contact sheet and manifest.
- Unity readback of `Mat_HectonSky`, Aegir material slots, and scene skybox refs.
- Bright surface screenshot. No acceptance from isolated texture preview.

Current source prototype:

- Folder: `Docs/GeneratedAssets/AssetSystem_20260605/AegirCloudPrototype_20260605/`.
- Review: `Docs/AssetAudit/SOURCE_PROTOTYPE_REVIEW_20260605.md`.
- Status: source-only. Storm/channel palette needs cleanup before any import attempt.

Current cleanup pass:

- Folder: `Docs/GeneratedAssets/AssetSystem_20260605/CleanupPass_20260605/`.
- Manifest: `Docs/GeneratedAssets/AssetSystem_20260605/CleanupPass_20260605/CleanupPass_MANIFEST_20260605.md`.
- Review: `Docs/AssetAudit/SOURCE_PROTOTYPE_CLEANUP_REVIEW_20260605.md`.
- Status: source-only. Band/detail direction improved, but storm mask remains too blob-like and needs Unity shader-slot proof before promotion.

## P1 Wet Basalt / Shell Sand Terrain PBR Recipe

Problem:

- Wet basalt/shell/sand source pools exist but are source-only or direct-scan candidates.
- Generated sources show baked-light, repetition, watermark/source-family sameness, or naive channel output risks.

Source inputs:

- `Docs/GeneratedAssets/Batch31_LocalPBR/*`: source references only.
- `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/Rock031_1K-JPG_Color.jpg`
- `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/Rock031_1K-JPG_NormalGL.jpg`
- `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/TX_H8_WetBasaltShoreline_Albedo_1428.png`
- `Assets/_Project/Art/TEXTURES/Terrain Textures/sand/Ground079S_1K-PNG_Color.png`
- `Assets/_Project/Art/TEXTURES/Terrain Textures/sand +green/Ground074_1K-JPG_Color.jpg`

Required generated pack:

- `TX_H8_WetBasaltRoute_Albedo_Source_YYYYMMDD.png`: no baked directional lighting, no obvious periodic ridges.
- `TX_H8_WetBasaltRoute_Normal_Source_YYYYMMDD.png`: believable wet rock relief, no overcranked generated normal.
- `TX_H8_WetBasaltRoute_MRAO_Source_YYYYMMDD.png`: wet smoothness variation and occlusion without dirty-gray flattening.
- `TX_H8_ShellSandRoute_Albedo_Source_YYYYMMDD.png`: shell/sand grain with route readability and no baked shadows.
- `TX_H8_ShellSandRoute_Normal_Source_YYYYMMDD.png`
- `TX_H8_ShellSandRoute_MRAO_Source_YYYYMMDD.png`

Visual rules:

- Terrain must support bright photic route readability.
- Tiling breakup is mandatory; one 1K scan repeated broad-field is rejected.
- Detail normals/decals can buy close-camera quality without bloating base texture residency.
- Low tier keeps material identity and silhouettes. High/Ultra adds richer detail normals, masks, and longer LOD residency.

Hard rejections:

- Do not direct-import `Docs/GeneratedAssets` outputs as final.
- Do not use terrain scans as random route art without named material family.
- Do not promote `terrain.mat` or `Mat_TriplanarRock.mat` stale refs without readback.

Proof gate:

- Contact sheet showing tile repetition at 1x/2x/4x.
- Channel manifest.
- Unity terrain/material readback and bright route screenshot.

## UI Sprite Role Recipe

Problem:

- `oxygen-tank.png` is a black silhouette/mask, not a colored final oxygen icon.
- `Assets/_Project/Art/Sprites/ui/OXYGEN.png` is the detailed candidate.

Rules:

- Treat `oxygen-tank.png` as mask/source only unless a UI owner explicitly assigns it that role.
- Use `ui/OXYGEN.png` for colored oxygen inventory/HUD icon candidate.
- Do not claim UI readiness until atlas/import/runtime UI proof exists.

Proof gate:

- UI atlas/import readback.
- In-HUD screenshot with legible icon at target scale.
- No text or icon clipping in compact layout.

Final status: `PENDING_VERIFICATION`.
