# Status 2306 - PHOTIC_TEXTURE_INTAKE_AND_MATERIAL_BINDING_PLANNER

Status: COMPLETE_STATIC_PLAN - PENDING UNITY OWNER EXECUTION
Evidence class: STATIC FILE REVIEW ONLY
Unity: NOT RUN
Assets edited: NONE

## Mandates Loaded

- AGENTS.md: surface/photic visuals must stay bright, readable, premium; source-only images cannot bypass audit; no Unity/build/import work for this task.
- PROJECT_BIBLES.md: domain bibles, not old batch notes, define material/route proof.
- TASTE.md: surface, coast, ocean skin, wet rock, and photic shallows must meet or exceed Subnautica-level readability; darkness cannot hide weak texture work.
- VISION_LOCKS.md: `GlobalQualityWeight = 0.0` is not ugly mode; wet rock, ocean color, foam, and route cues are non-negotiable.
- 3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md: generated/AI textures are source only until albedo, normal, MRAO, optional height/detail, seam, mips, compression, and preview proof exist.
- 3DMODEL_TEXTURES_MATERIALS.md: shipped route uses imported compressed assets, documented texture roles, MRAO packing, SRP-batchable material assets, no runtime texture generation.
- terrain.md: photic terrain needs wet geology, sediment, foam contact, route silhouettes, and material breakup across compact/high lanes.
- water.md and vfx.md: foam, caustics, wetness, and silt are controlled fakes/consequences with named owners, not unbounded simulation or decoration.
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt: use baked/source masks, shader fakes, and offline derivation before any runtime simulation.
- REND_Terrain_VirtualTexturing.txt / STRM_Async_Asset_Upload_Texture_Settings.txt / OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt / REND_Shader_Noir_Aesthetics_Dithering_Fog.txt: texture variety must respect binding count, streaming, compact VRAM, caustic depth reason, and quality-scaled import sizes.

## Task Result

- Current Gemini/source candidates inventoried.
- No source candidate is approved for Unity import, derivation, or material binding.
- User-downloaded sand/shell candidate path verified: `Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticShellSandSubstrate_AlbedoSource_20260604_Gemini_164642.png`.
- Binding plan written: `Docs/Reports/Batch23/2306_PHOTIC_TEXTURE_BINDING_PLAN.md`.
- Candidate matrix written: `Docs/Reports/Batch23/2306_TEXTURE_CANDIDATE_MATRIX.csv`.
- Log appended: `Docs/AgentLogs/LOG_2306.md`.

## Blocking Facts

- `Docs/GeneratedAssets/Gemini/Outputs/Batch22/` is absent/empty in static scan.
- Batch21 audit reports `PASS_STATIC: 0`, `REVIEW: 0`, `REJECT: 2`.
- Root 1428 surface material files are modified in git status; `Photic14xx` material folders are untracked. Treat them as active parallel-agent/Unity-owner work until controller confirms ownership.
- `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/TX_H8_WetBasaltShoreline_Albedo_1428.png` exists as an untracked Unity-side texture, but Batch22 evidence classifies the 1428 wet basalt source as rejected/source-only. Do not bind or promote without manifest and fresh audit.

## Next Required Actions

1. Generate corrected wet basalt shoreline albedo source.
2. Generate corrected sand/shell substrate albedo source.
3. Generate shoreline foam/salt RGBA mask source.
4. Run static intake audit and human 2x2/3x3 seam review before any derivation or Unity material binding.
