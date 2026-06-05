# Rationale 2017

Decision: fail production acceptance for the Gemini wet-basalt pipeline.

Reason:

- HECTON-8 texture law requires full PBR family, import settings, mip/compression behavior, URP lighting preview, and material proof before Unity production use.
- Current artifacts are albedo-only static QA outputs.
- `TextureSeamPeriodicRefiner.py` can force exact outer pixels to match; `GeminiTextureIntakeAudit.py` then reports `0.000` seams while adjacent inner bands remain discontinuous.
- Visual preview still shows broad repeated rock forms and baked-light-looking highlights, violating surface/coast/photic material floor.

Accepted limited use:

- Source/reference.
- Small masked decal.
- Macro-blended diagnostic layer only after cleanup and PBR proof.

Rejected use:

- Direct TerrainLayer replacement.
- Naked broad shoreline tile.
- Any production-ready PBR/material/import claim.
