# Gemini Texture Intake Audit

Evidence class: STATIC_IMAGE_QA.
Unity was not run. No Assets were edited.

Scanned root: `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic.png`
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
| REJECT | Albedo | 1024x1024 | 0.000 | 0.000 | 57.797 | 62.653 | 156.559 | 0.12%/12.54% | 14.22% | `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic.png` | `Docs/GeneratedAssets/Gemini/QA/WetBasalt1429Periodic/tile_previews/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_tile2x2.png` |
