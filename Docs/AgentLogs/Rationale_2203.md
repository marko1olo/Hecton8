# Rationale 2203

Evidence class: STATIC VERIFIED.

## Decisions

- Wet basalt and sand/shell were placed first in the queue because current evidence names shoreline, coast, terrain, and seabed material identity as weak, and existing sources for those targets are static-rejected.
- Foam/salt and caustic masks were placed before coral because they directly improve shoreline/waterline and photic readability with cheap visual-fake presentation routes.
- Existing rejected sand/shell art remains reference only. Direct PBR derivation from it is blocked because the audit shows top-bottom band mismatch and visual notes identify repeated shell/stone clusters.
- Existing wet basalt 1429 periodic mean is not treated as a seam fix because strict audit reports worse band mismatch, clipping, and channel saturation.
- No edit was made to `Tools/GeminiTextureIntakeAudit.py` because its behavior matches task needs: edge/band seam checks, luminance/clipping/saturation checks, CSV/Markdown output, and 2x2 preview generation.

## Scaling Consequence

- Compact: use compressed 512-1024 imported world textures and small caustic/decal masks; preserve material identity through clean sources and masks.
- Middle: allow 1024-2048 key photic materials and stronger decal/roughness variation.
- High: use 2048 hero surfaces and richer normal/wetness/foam layers.
- Ultra: keep 4096 source/bake archives and hero-only overkill after streaming/profiler proof; do not change gameplay or material truth.
