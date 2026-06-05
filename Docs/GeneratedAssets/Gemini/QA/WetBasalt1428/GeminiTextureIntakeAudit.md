# Gemini Texture Intake Audit

Evidence class: STATIC_IMAGE_QA.
Unity was not run. No Assets were edited.

Scanned root: `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/TX_H8_WetBasaltShoreline_Albedo_1428.png`
Images scanned: 1
PASS_STATIC: 0
REVIEW: 0
REJECT: 1

## Rules

- `REJECT` means at least one hard static issue exists, usually non-square, severe edge mismatch, or too-dark albedo for surface/shallows.
- `REVIEW` means no hard static issue, but source is lossy, low-res, not power-of-two, has moderate seams, or has suspicious luminance/channel behavior.
- `PASS_STATIC` is still not Unity acceptance. It only means this intake gate found no static blocker.
- Every accepted candidate still needs PBR channel manifest, import settings, material binding, 2x2 visual review, and Unity screenshot proof.

## Findings

| Verdict | Role | Size | LR seam | TB seam | Lum mean | Path | Preview |
|---|---|---:|---:|---:|---:|---|---|
| REJECT | Albedo | 1024x1024 | 30.780 | 33.396 | 85.971 | `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/TX_H8_WetBasaltShoreline_Albedo_1428.png` | `Docs/GeneratedAssets/Gemini/QA/WetBasalt1428/tile_previews/TX_H8_WetBasaltShoreline_Albedo_1428_tile2x2.png` |
