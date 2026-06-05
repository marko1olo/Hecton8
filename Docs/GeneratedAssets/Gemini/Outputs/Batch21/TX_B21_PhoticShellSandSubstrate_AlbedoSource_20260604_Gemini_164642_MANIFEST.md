# TX_B21_PhoticShellSandSubstrate_AlbedoSource_20260604_Gemini_164642

Status: SOURCE_REFERENCE_ONLY / STATIC_REJECTED / UNITY_MATERIAL_BLOCKED
Evidence class: STATIC_IMAGE_QA

## Source

- File: `Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticShellSandSubstrate_AlbedoSource_20260604_Gemini_164642.png`
- SHA-256: `5BC241A044CBF1817458AF01B7893A9BF62D50D02D3D31AE0B1B571A28851462`
- Origin: Gemini browser generation downloaded by user, moved from Downloads into the project documentation asset intake area.
- Intended role: photic shallow shell/sand/calcite substrate albedo source/reference.

## Static QA

- Audit command:
  - `python Tools/GeminiTextureIntakeAudit.py --root Docs/GeneratedAssets/Gemini/Outputs/Batch21 --out-dir Docs/GeneratedAssets/Gemini/Audit/Batch21 --max-tile-preview 512`
- Audit report:
  - `Docs/GeneratedAssets/Gemini/Audit/Batch21/GeminiTextureIntakeAudit.csv`
  - `Docs/GeneratedAssets/Gemini/Audit/Batch21/GeminiTextureIntakeAudit.md`
- 2x2 preview:
  - `Docs/GeneratedAssets/Gemini/Audit/Batch21/tile_previews/TX_B21_PhoticShellSandSubstrate_AlbedoSource_20260604_Gemini_164642_tile2x2.png`

Result:
- Role: `Albedo`
- Verdict: `REJECT`
- Issues: `top_bottom_band_mismatch`
- Warnings: `left_right_edge_warning; top_bottom_edge_warning; left_right_band_warning; possible_crushed_range_or_baked_lighting`
- LR seam mean: `16.978`
- TB seam mean: `17.091`
- LR band mean: `19.845`
- TB band mean: `23.251`
- Luminance mean/min/max: `172.502 / 0 / 255`

## Visual Notes

The image is visually useful as a bright photic sand/shell reference. It has believable small shells, pebbles, pale substrate, and small algae/marine growth flecks.

It is not accepted for direct Unity import because the 2x2 preview repeats recognizable shell/stone clusters and the top-bottom band gate fails. Large seabed use would expose tiling.

## Next Required Work

Use this candidate as reference for a corrected source or offline cleanup pass:
- reduce repeated hero shell/stone clusters;
- keep shell/pebble distribution smaller and more stochastic;
- remove large directional bands;
- preserve bright photic readability without baked lighting;
- produce normal, height, roughness/AO/wetness, and contact variation channels;
- rerun static intake;
- only then create material/TerrainLayer and Unity proof.

Do not bind this file directly into runtime materials or terrain layers.
