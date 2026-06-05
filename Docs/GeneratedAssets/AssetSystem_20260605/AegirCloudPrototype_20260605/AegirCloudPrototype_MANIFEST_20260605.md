# Aegir Cloud Prototype Manifest - 2026-06-05

Status: `SOURCE_ONLY_NOT_IMPORTED` / `PENDING_VERIFICATION`.
Evidence class: `STATIC_SOURCE` + `STATIC_IMAGE_QA`.

No Unity import, no material binding, no runtime proof, no final visual acceptance.

## Outputs

- `TX_H8_AegirBand_Albedo_SOURCE_ONLY_NOT_IMPORTED_PENDING_VERIFICATION_20260605.png`
- `TX_H8_AegirStorm_MaskRGBA_SOURCE_ONLY_NOT_IMPORTED_PENDING_VERIFICATION_20260605.png`
- `TX_H8_AegirCloud_Detail_SOURCE_ONLY_NOT_IMPORTED_PENDING_VERIFICATION_20260605.png`
- `TX_H8_AegirCloudPrototype_ContactSheet_SOURCE_ONLY_NOT_IMPORTED_PENDING_VERIFICATION_20260605.png`

## Source Inputs

- `Assets/_Project/Art/TEXTURES/clouds0_diff.png`
- `Assets/_Project/Art/TEXTURES/Sky/bo3.png`
- `Assets/_Project/Art/TEXTURES/Sky/oblakajip.png`
- `Assets/_Project/Art/TEXTURES/Aegir_storms.png`
- `Assets/_Project/Art/TEXTURES/TX_H8AegirGasGiantBakedDisc_1428.png`: rejected final, reference only.

## Channel Intent

- Band albedo: broad Aegir cloud-band source preview.
- Storm mask RGBA: source preview for storm cells, turbulence, rim/limb breakup, opacity/detail blend.
- Cloud detail: source preview for shader detail layer.

## Manual Review

- Prototype is richer than the baked disc and confirms better source direction.
- Storm mask contact sheet is false-color and oversaturated; it is not final material art.
- Useful for authoring direction and shader-slot discussion only.
- Not acceptable for direct import.

## Proof Required

- Cleaned Aegir/cloud channel pass with final palette discipline.
- Unity readback of `Mat_HectonSky`, Aegir material slots, and scene skybox refs.
- Bright surface screenshot proving Aegir is premium and not toy-like.
- Import settings proof and texture memory proof.

