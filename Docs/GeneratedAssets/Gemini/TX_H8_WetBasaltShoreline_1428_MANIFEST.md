# TX_H8_WetBasaltShoreline 1428 Manifest

Status: SOURCE_REVIEW_ONLY / UNITY_MATERIAL_BLOCKED / VISUAL_PROOF_PENDING

## Source

- Generator: Gemini / Nano Banana 2 through Edge browser
- Prompt class: seamless square tileable PBR albedo texture
- Subject: alien wet basalt shoreline rock, black-grey volcanic stone, teal mineral staining, salt-water erosion, pores/cracks/mineral speckles
- Browser workflow documented in:
  - `Docs/Orchestration/GEMINI_BROWSER_IMAGE_ASSET_WORKFLOW.md`

## Canonical File

- `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/TX_H8_WetBasaltShoreline_Albedo_1428.png`

## QA Artifacts

- `Docs/GeneratedAssets/Gemini/QA/Gemini_Downloaded_PNG_preview.png`
- `Docs/GeneratedAssets/Gemini/QA/TX_Gemini_WetBasaltShoreline_Albedo_20260604_tile2x2.jpg`
- `Docs/GeneratedAssets/Gemini/QA/WetBasalt1428/GeminiTextureIntakeAudit.md`
- `Docs/GeneratedAssets/Gemini/QA/WetBasalt1428/tile_previews/TX_H8_WetBasaltShoreline_Albedo_1428_tile2x2.png`

## Current QA Verdict

- `Tools/GeminiTextureIntakeAudit.py` verdict: `REJECT` as a production tile.
- Static metrics:
  - left/right edge mismatch: `30.78`
  - top/bottom edge mismatch: `33.396`
  - luminance range: `0..255`
- Visual 2x2 review: material identity is useful, but the large diagonal teal vein repeats too obviously. It is not accepted as a naked large terrain tile.
- Permitted use before seam-fix: source/reference, small masked decal, or heavily blended detail under macro masks.
- Forbidden use before seam-fix: direct replacement for active basalt TerrainLayer or broad unbroken shoreline terrain material.

## Known Current State

- The texture is albedo only.
- Unity has created/imported a `.meta` for the canonical PNG.
- It is not currently wired into the active basalt TerrainLayer.
- It must not replace `L_Basalt.terrainlayer` while the active Unity owner is working.
- Existing `L_Basalt.terrainlayer` still uses:
  - `Rock031_1K-JPG_Color.jpg`
  - `Rock031_1K-JPG_NormalGL.jpg`
- The old Rock031 texture is shared outside pure terrain routes, so GUID replacement is forbidden.

## Required Family Before Production Use

- `TX_H8_WetBasaltShoreline_Albedo_1428.png` - present
- `TX_H8_WetBasaltShoreline_Normal_1428.png` - pending
- `TX_H8_WetBasaltShoreline_MRAO_1428.png` - pending
- `H8_TerrainLayer_WetBasaltShoreline_1428.terrainlayer` - pending Unity/editor slot
- `MAT_H8WetBasaltShoreline_1428.mat` - pending Unity/editor slot

## Intended Use

- Surface/coastline wet basalt variation.
- Waterline/coastal rock breakup.
- Photic-shallow terrain transition material candidate.

## Rejection Conditions

- Do not use as a huge unbroken surface without macro variation.
- Do not use as final PBR material without normal and packed mask/wetness route.
- Do not use as a final seamless tile until edge mismatch and large-form repetition are fixed and re-audited.
- Do not claim in-game quality without Unity screenshot proof.
- Do not darken/noir-grade this material for surface or 0-100 m photic zones.
