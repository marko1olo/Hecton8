# TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604_Gemini_155742 Manifest

Target: `TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604`  
Source file: `Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604_Gemini_155742.png`  
SHA-256: `ED65B4399BD6E868CF7E587D80411523E81C837AA6B8B39114FA569B8E92A5C2`

## Status

`SOURCE_REFERENCE_ONLY / STATIC_REJECTED / UNITY_MATERIAL_BLOCKED`

## Generation Context

Generated in Gemini browser from the `2102` photic seabed substrate prompt.

The candidate is visually useful as a reference for bright photic seabed color, shell/calcite chips, silt, and small algae/biofilm marks. It is not acceptable as a production tile.

## Audit

Audit tool:

`Tools/GeminiTextureIntakeAudit.py`

Output:

`Docs/GeneratedAssets/Gemini/Audit/Batch21/GeminiTextureIntakeAudit.csv`

Result:

- Verdict: `REJECT`
- Issue: `top_bottom_band_mismatch`
- Warnings: `left_right_edge_warning`, `top_bottom_edge_warning`, `left_right_band_warning`
- `seam_lr_mean`: `15.702`
- `seam_tb_mean`: `17.115`
- `seam_lr_band_mean`: `19.358`
- `seam_tb_band_mean`: `23.681`
- `luminance_mean`: `169.267`

2x2 preview:

`Docs/GeneratedAssets/Gemini/Audit/Batch21/tile_previews/TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604_Gemini_155742_tile2x2.png`

## Visual Rejection Notes

- Large diagonal dune/ripple bands repeat as obvious hero shapes in the 2x2 preview.
- The source is brighter and more useful than the flat current Unity seabed, but tile repetition would be visible in route-scale terrain.
- Do not import or bind this texture to Unity materials.

## Next Prompt Correction

Ask for a revised seamless square texture with:

- no large diagonal bands;
- no recognizable repeated hero shapes;
- more isotropic stochastic small/medium scale variation;
- invisible left/right and top/bottom edges in 2x2 and 3x3 tiled previews;
- same bright photic seabed material identity.
