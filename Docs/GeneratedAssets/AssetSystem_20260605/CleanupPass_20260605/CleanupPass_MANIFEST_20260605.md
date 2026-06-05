# Texture Cleanup Pass Manifest - 2026-06-05

Status: `SOURCE_ONLY_NOT_IMPORTED / PENDING_VERIFICATION`.
Evidence class: `STATIC_IMAGE_QA`.
Scope: source-only cleanup outputs under `Docs/GeneratedAssets`. No files under `Assets` were edited or imported.

## Authority

Mandates followed:

- `QA_Evidence_Text_Filter_Audit`
- `STRM_Async_Asset_Upload_Texture_Settings`
- `REND_URP_Graphics_HotPath_Optimization_HLOD`
- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC` for asset-front audio separation awareness; no audio files changed.

Root locks used:

- `TASTE.md`
- `VISION_LOCKS.md`
- `TEXTURE_AUTHORING_RECIPES_20260605.md`
- `SOURCE_PROTOTYPE_REVIEW_20260605.md`

## Generated Files

Foam/contact source cleanup:

- `TX_H8_FoamContact_CleanedSource_Albedo_20260605.png` - 1024x1024 RGBA.
- `TX_H8_FoamContact_CleanedSource_DetailNormal_20260605.png` - 1024x1024 RGB.
- `TX_H8_FoamContact_CleanedSource_MRAO_20260605.png` - 1024x1024 RGBA.
- `TX_H8_FoamContact_CleanedSource_MaskRGBA_20260605.png` - 1024x1024 RGBA.
- `FoamContact_CleanupPass_ContactSheet_SOURCE_ONLY_20260605.png` - 1280x794 RGB.

Aegir/cloud source cleanup:

- `TX_H8_AegirCloud_CleanedSource_BandAlbedo_20260605.png` - 1024x512 RGB.
- `TX_H8_AegirCloud_CleanedSource_StormMaskRGBA_20260605.png` - 1024x512 RGBA.
- `TX_H8_AegirCloud_CleanedSource_Detail_20260605.png` - 1024x512 RGB.
- `AegirCloud_CleanupPass_ContactSheet_SOURCE_ONLY_20260605.png` - 1280x794 RGB.

All generated PNGs include metadata:

- `H8_Status = SOURCE_ONLY_NOT_IMPORTED / PENDING_VERIFICATION`

## Static Metrics

Foam/contact cleanup:

- Albedo mean RGBA: `0.6099,0.6742,0.6815,0.3431`.
- Detail normal mean RGBA: `0.5027,0.5029,0.9269,1.0000`.
- Mask RGBA mean: `0.2860,0.3750,0.1516,0.3482`.
- MRAO mean: `0.0000,0.7342,0.3536,0.3460`.

Aegir/cloud cleanup:

- Band albedo mean RGBA: `0.2484,0.3311,0.4251,1.0000`.
- Detail mean RGBA: `0.3876,0.3876,0.3876,1.0000`.
- Storm mask mean RGBA: `0.0559,0.2276,0.2975,0.2951`.

## Visual Review

Foam/contact:

- Better than the rejected turquoise Crest `foam.png`.
- Albedo avoids pool-cyan read and can serve as a source reference.
- Normal is softened enough for a source pass.
- RGBA and MRAO are still too broad and false-color in contact-sheet preview; wet edge and residue channels need tighter art direction before Unity import.

Disposition: `SOURCE_ONLY_USEFUL / NOT_IMPORT_READY`.

Aegir/cloud:

- Band albedo is a stronger direction than the old baked disc.
- Detail sheet keeps readable storm/band structure.
- Storm mask is less chaotic than the previous prototype but still too blob-like and preview-colored; channel semantics need shader-slot proof before promotion.

Disposition: `SOURCE_ONLY_USEFUL / NOT_IMPORT_READY`.

## Required Next Proof

- Unity material readback of sky/Aegir and Crest/ocean slots.
- Import settings proof: compression, mipmaps, streaming, max size, sRGB/linear by role.
- Bright surface/sky/ocean screenshot after binding, with Stats/Frame Debugger proof.
- No Crest wrapper, material clone, or raw YAML patch.

Final status: `PENDING_VERIFICATION`.
