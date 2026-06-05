# Gemini Texture Intake Audit

Evidence class: STATIC_IMAGE_QA.
Unity was not run. No Assets were edited.

Scanned root: `Docs/GeneratedAssets/Gemini/Outputs/Batch21`
Images scanned: 2
PASS_STATIC: 0
REVIEW: 0
REJECT: 2

## Rules

- `REJECT` means at least one hard static issue exists: non-square source, severe seam/band mismatch, too-dark albedo, clipped albedo, or saturated channel data.
- `REVIEW` means no hard static issue, but source is lossy, low-res, not power-of-two, has moderate seams, or has suspicious luminance/channel behavior.
- `PASS_STATIC` is still not Unity acceptance. It only means this intake gate found no static blocker.
- Every accepted candidate still needs PBR channel manifest, import settings, material binding, 2x2 visual review, and Unity screenshot proof.

## Findings

| Verdict | Role | Size | LR seam | TB seam | LR band | TB band | Lum mean | Clip 0/255 | Sat ch | Path | Preview |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---|
| REJECT | Albedo | 1024x1024 | 15.702 | 17.115 | 19.358 | 23.681 | 169.267 | 0.00%/0.03% | 0.06% | `Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604_Gemini_155742.png` | `Docs/GeneratedAssets/Gemini/Audit/Batch21/tile_previews/TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604_Gemini_155742_tile2x2.png` |
| REJECT | Albedo | 1024x1024 | 16.978 | 17.091 | 19.845 | 23.251 | 172.502 | 0.00%/0.03% | 0.07% | `Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticShellSandSubstrate_AlbedoSource_20260604_Gemini_164642.png` | `Docs/GeneratedAssets/Gemini/Audit/Batch21/tile_previews/TX_B21_PhoticShellSandSubstrate_AlbedoSource_20260604_Gemini_164642_tile2x2.png` |
