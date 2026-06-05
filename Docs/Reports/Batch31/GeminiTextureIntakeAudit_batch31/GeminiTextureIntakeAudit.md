# Gemini Texture Intake Audit

Evidence class: STATIC_IMAGE_QA.
Unity was not run. No Assets were edited.

Scanned root: `Docs/GeneratedAssets`
Images scanned: 9
PASS_STATIC: 0
REVIEW: 2
REJECT: 7

## Rules

- `REJECT` means at least one hard static issue exists: non-square source, severe seam/band mismatch, too-dark albedo, clipped albedo, or saturated channel data.
- `REVIEW` means no hard static issue, but source is lossy, low-res, not power-of-two, has moderate seams, or has suspicious luminance/channel behavior.
- `PASS_STATIC` is still not Unity acceptance. It only means this intake gate found no static blocker.
- Every accepted candidate still needs PBR channel manifest, import settings, material binding, 2x2 visual review, and Unity screenshot proof.

## Findings

| Verdict | Role | Size | LR seam | TB seam | LR band | TB band | Lum mean | Clip 0/255 | Sat ch | Path | Preview |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---|
| REJECT | Albedo | 536x268 | 0.000 | 0.000 | 3.272 | 3.165 | 157.783 | 0.00%/0.00% | 0.00% | `Docs/GeneratedAssets/Gemini/Audit/Batch21/GeminiTextureIntake_contact_sheet.png` | `Docs/Reports/Batch31/GeminiTextureIntakeAudit_batch31/tile_previews/GeminiTextureIntake_contact_sheet_tile2x2.png` |
| REVIEW | Albedo | 1024x1024 | 12.087 | 17.242 | 15.784 | 20.958 | 169.266 | 0.00%/0.06% | 0.06% | `Docs/GeneratedAssets/Gemini/Audit/Batch21/tile_previews/TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604_Gemini_155742_tile2x2.png` | `Docs/Reports/Batch31/GeminiTextureIntakeAudit_batch31/tile_previews/TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604_Gemini_155742_tile2x2_tile2x2.png` |
| REVIEW | Albedo | 1024x1024 | 13.042 | 17.408 | 16.157 | 20.327 | 172.495 | 0.01%/0.07% | 0.08% | `Docs/GeneratedAssets/Gemini/Audit/Batch21/tile_previews/TX_B21_PhoticShellSandSubstrate_AlbedoSource_20260604_Gemini_164642_tile2x2.png` | `Docs/Reports/Batch31/GeminiTextureIntakeAudit_batch31/tile_previews/TX_B21_PhoticShellSandSubstrate_AlbedoSource_20260604_Gemini_164642_tile2x2_tile2x2.png` |
| REJECT | Albedo | 1024x1024 | 15.702 | 17.115 | 19.358 | 23.681 | 169.267 | 0.00%/0.03% | 0.06% | `Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604_Gemini_155742.png` | `Docs/Reports/Batch31/GeminiTextureIntakeAudit_batch31/tile_previews/TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604_Gemini_155742_tile2x2.png` |
| REJECT | Albedo | 1024x1024 | 16.978 | 17.091 | 19.845 | 23.251 | 172.502 | 0.00%/0.03% | 0.07% | `Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticShellSandSubstrate_AlbedoSource_20260604_Gemini_164642.png` | `Docs/Reports/Batch31/GeminiTextureIntakeAudit_batch31/tile_previews/TX_B21_PhoticShellSandSubstrate_AlbedoSource_20260604_Gemini_164642_tile2x2.png` |
| REJECT | Albedo | 1024x1024 | 0.000 | 0.000 | 57.797 | 62.653 | 156.559 | 0.12%/12.54% | 14.22% | `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic.png` | `Docs/Reports/Batch31/GeminiTextureIntakeAudit_batch31/tile_previews/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_tile2x2.png` |
| REJECT | Albedo | 1024x1024 | 0.000 | 0.000 | 57.797 | 62.653 | 156.559 | 0.12%/12.54% | 14.22% | `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_darkpreserve.png` | `Docs/Reports/Batch31/GeminiTextureIntakeAudit_batch31/tile_previews/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_darkpreserve_tile2x2.png` |
| REJECT | Albedo | 1024x1024 | 68.255 | 84.430 | 69.583 | 75.465 | 85.771 | 16.73%/3.50% | 21.90% | `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_mean.png` | `Docs/Reports/Batch31/GeminiTextureIntakeAudit_batch31/tile_previews/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_mean_tile2x2.png` |
| REJECT | Albedo | 1024x1024 | 30.611 | 34.508 | 37.609 | 40.462 | 82.999 | 0.12%/0.00% | 0.31% | `Docs/GeneratedAssets/Gemini/TX_H8_WetBasaltShoreline_Albedo_1429.png` | `Docs/Reports/Batch31/GeminiTextureIntakeAudit_batch31/tile_previews/TX_H8_WetBasaltShoreline_Albedo_1429_tile2x2.png` |
