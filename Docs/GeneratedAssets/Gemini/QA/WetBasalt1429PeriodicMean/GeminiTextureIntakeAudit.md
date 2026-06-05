# Gemini Texture Intake Audit

Evidence class: STATIC_IMAGE_QA.
Unity was not run. No Assets were edited.

Scanned root: `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_mean.png`
Images scanned: 1
PASS_STATIC: 0
REVIEW: 0
REJECT: 1

## Rules

- `REJECT` means at least one hard static issue exists: non-square source, severe seam/band mismatch, too-dark albedo, clipped albedo, or saturated channel data.
- `REVIEW` means no hard static issue, but source is lossy, low-res, not power-of-two, has moderate seams, or has suspicious luminance/channel behavior.
- `PASS_STATIC` is still not Unity acceptance. It only means this intake gate found no static blocker.
- Every accepted candidate still needs PBR channel manifest, import settings, material binding, 2x2 visual review, and Unity screenshot proof.

## Findings

| Verdict | Role | Size | LR seam | TB seam | LR band | TB band | Lum mean | Clip 0/255 | Sat ch | Path | Preview |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---|
| REJECT | Albedo | 1024x1024 | 68.255 | 84.430 | 69.583 | 75.465 | 85.771 | 16.73%/3.50% | 21.90% | `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_mean.png` | `Docs/GeneratedAssets/Gemini/QA/WetBasalt1429PeriodicMean/tile_previews/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_mean_tile2x2.png` |
