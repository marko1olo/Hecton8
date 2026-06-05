# Rationale 1909

## Evidence Boundary

Static reports, CSVs, YAML-derived claims, paths, screenshots, and QA previews were treated as static evidence only. Anything requiring Unity import, active scene binding, material slot state, render pass ordering, visual acceptance, profiler, GC, memory, VRAM, PlayMode, or player capture was marked `PENDING_UNITY_OWNER` or assigned to `PROFILER_OWNER`.

## Classification Decisions

- Old screenshots were used only as old static image evidence. They can prove prior visual failure or prior route state, not current pass.
- `Docs/Screenshots/1428_current_world_surface_mismatch.png`, `1428_bad_foam_disabled_retry_game.png`, `1428_surface_aegir_impostor_waterline.png`, `1428_gameview_h8_crest_material_pass18.png`, and `1428_crest_restored_real_surface_game.png` were classified as failure/regression evidence because they show black/muddy surface, flat/hard foam, grey procedural coast, neon/flat water, or weak Aegir integration.
- 1905 was included as optional Batch19 evidence because its report exists. It remains source-planning only: 24 prompts, zero images, no QA previews.
- 1906 wet basalt QA was included because files exist and were inspected. It remains `PENDING_CHANNEL_QA` because the metrics report left/right and top/bottom seam diffs of 30.78 and 33.40 and says generated maps are QA-only previews.
- 1907 was included as prompt-only evidence. It cannot clear source, channel, import, or proof debt.
- 1908 was represented as absent/empty because no prompt files were found in its folder at audit time.
- ProductFace material assignment debt was ranked high because 1893 gives concrete static counts: 61 material rows, 55 blocked, 17 package/default Lit, 23 tool placeholders, 8 resource flat color, 6 player blockout.
- ProductFace channel rows were `BLOCKED_BY_CONTRACT` when the exact shader channel semantics were not present. Names such as ARM, ORM, MRAO, Mask, and Packed were not accepted as contracts.
- Flora/coral/geology proof debt was ranked high because 1858 and 1901 show broad missing manifests, named proof gaps, source-only packages, and all 46 priority proof rows still `PENDING UNITY`.
- Missing `Docs/Screenshots/GeneratedAssets` and `Docs/Reports/GeneratedAssets` were recorded as proof-artifact gaps, not as runtime failures.

## Ranking Basis

Priority followed first-hour route credibility:

1. coastline, waterline, foam, wet basalt, and ocean surface;
2. Aegir, sky, moons, and photic-shallow clarity;
3. flora/coral/kelp/geology proof wave;
4. product-face default/placeholder materials;
5. channel contract and proof-folder hygiene.

## Continuous Quality Consequence Rule

Compact means minimum survival readability, not ugly mode. Middle adds clarity and density. High and Ultra add sensory richness after the same route truth, material semantics, collision identity, save identity, and owner route remain stable.

## No Dependency Decision

Sibling Batch19 outputs were treated as optional. No future owner queue row requires 1905, 1906, 1907, or 1908 as a hard dependency. Rows say "can consume if useful" where applicable.
