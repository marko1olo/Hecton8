# Foam Contact Prototype Manifest - 2026-06-05

Status: `SOURCE_ONLY_NOT_IMPORTED` / `PENDING_VERIFICATION`.
Evidence class: `STATIC_SOURCE` + `STATIC_IMAGE_QA`.

No Unity import, no material binding, no runtime proof, no final visual acceptance.

## Outputs

- `TX_H8_FoamContact_Albedo_SourcePreview_20260605.png`
- `TX_H8_FoamContact_DetailNormal_SourcePreview_20260605.png`
- `TX_H8_FoamContact_MRAO_SourcePreview_20260605.png`
- `TX_H8_FoamContact_MaskRGBA_SourcePreview_20260605.png`
- `FoamContact_ChannelContactSheet_SOURCE_ONLY_20260605.png`
- `FoamContact_SourceReferenceSheet_SOURCE_ONLY_20260605.png`

## Source Inputs

- `Assets/Crest/Crest/Textures/foam.png`: rejected visible foam reference only.
- Crest/reference foam texture from existing project sources.
- `Assets/_Project/Art/TEXTURES/Detali/mineral seep mask - looks seamless.png`
- `Assets/_Project/Art/TEXTURES/Detali/Mineral Seep Mask - second try.png`
- `Assets/_Project/Art/TEXTURES/Detali/Soft Plume Noise - second try.png`
- `Assets/_Project/Art/TEXTURES/Detali/visor droplet mask.png`
- `Assets/_Project/Art/TEXTURES/Detali/visor runoff normal.png`

## Channel Intent

- Albedo: low-saturation ocean contact residue preview.
- Detail normal: source preview for shallow streak/foam relief.
- MRAO: packed preview, not final import format proof.
- MaskRGBA: source preview for salt rim, wet edge, bubble breakup, and residue.

## Manual Review

- Better source direction than flat turquoise `foam.png`.
- Still too high-contrast/blocky in several mask channels because mineral seep sources dominate.
- Useful for authoring direction only.
- Not acceptable for direct import.

## Proof Required

- Cleaned channel authoring pass with softened/block-reduced masks.
- Unity import readback: linear masks, normal type, compression, mipmaps, streaming mips.
- Crest material readback without wrapper or material clone.
- Bright shoreline screenshot and Frame Debugger/Stats proof.

