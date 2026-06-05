# TX_H8_WetBasaltShoreline 1429 Manifest

Status: SOURCE_REFERENCE_ONLY / STATIC_REJECTED / UNITY_MATERIAL_BLOCKED

## Source

- Generator: Gemini / Nano Banana 2 through Edge browser.
- Dialogue route: old wet-basalt Gemini thread, no new browser tab required.
- Prompt class: correction pass for true seamless square tileable albedo.
- Subject: alien wet basalt shoreline rock, black-grey volcanic stone, subtle teal mineral staining, salt-water erosion, pores, cracks, small mineral/barnacle speckles.

## Downloaded Candidate

- `Docs/GeneratedAssets/Gemini/TX_H8_WetBasaltShoreline_Albedo_1429.png`
- Download source: browser download from Gemini image result.
- Unity import state: not imported into `Assets/**`.

## Static QA: Raw Gemini Download

- Report: `Docs/GeneratedAssets/Gemini/QA/WetBasalt1429/GeminiTextureIntakeAudit.md`
- Preview: `Docs/GeneratedAssets/Gemini/QA/WetBasalt1429/tile_previews/TX_H8_WetBasaltShoreline_Albedo_1429_tile2x2.png`
- Verdict: `REJECT`
- Metrics:
  - left/right seam: `30.611`
  - top/bottom seam: `34.508`
  - luminance mean: `82.999`
- Reason: raw Gemini output still has hard edge mismatch and visible large repeated forms.

## Local Seam Refinement Attempt

- Tool: `Tools/TextureSeamPeriodicRefiner.py`
- Important: exact edge pinning is diagnostic-only and must not be treated as seam proof.
- Refined candidate:
  - `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_mean.png`
- QA report:
  - `Docs/GeneratedAssets/Gemini/QA/WetBasalt1429PeriodicMean/GeminiTextureIntakeAudit.md`
- 2x2 preview:
  - `Docs/GeneratedAssets/Gemini/QA/WetBasalt1429PeriodicMean/tile_previews/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_mean_tile2x2.png`
- Verdict after strict audit: `REJECT`
- Metrics:
  - left/right seam: `68.255`
  - top/bottom seam: `84.430`
  - left/right band: `69.583`
  - top/bottom band: `75.465`
  - luminance mean: `85.771`
  - clipped black pixels: `16.733%`
  - clipped white pixels: `3.495%`
  - saturated channels: `21.901%`
- Rejection: strict audit catches inner wrap-band mismatch and baked/clipped albedo that exact edge pinning hid.

## Visual Review

- Raw `1429` is better than `1428` as source material identity, but still not seamless enough for production terrain.
- `1429_periodic_mean` is not a valid seam fix. It can make edge-only metrics look better in diagnostic mode, but strict band analysis still rejects it.
- Large repeated rock forms remain visible in a 2x2 preview.
- Some black crevices and white highlights look baked into albedo; this must be cleaned or diluted through macro blending before production use.

## Permitted Use

- Source/reference for wet basalt shoreline material.
- Small masked shoreline detail decal.
- Possible reference for a future Gemini correction prompt.
- Basis for manual paintover/source study only after albedo range cleanup. Do not derive final normal/MRAO from this rejected albedo.

## Forbidden Use

- Do not replace active basalt TerrainLayer directly.
- Do not use as naked broad terrain tile.
- Do not use `1429_periodic_mean` as a seam-fixed source candidate.
- Do not claim production-ready PBR material: normal, MRAO, wetness/foam-waterline channels, import settings, material binding, and Unity visual proof are missing.
- Do not darken/noir-grade surface or photic shallows to hide repetition.

## Next Required Gates

1. Produce an albedo cleanup variant with reduced baked lighting and less visible large-form repetition.
2. Generate or derive normal and MRAO/wetness masks from a cleaned candidate.
3. Build a candidate material/terrain layer only during the Unity-owner slot.
4. Capture surface, waterline, and 0-100 m photic screenshots before any acceptance claim.
