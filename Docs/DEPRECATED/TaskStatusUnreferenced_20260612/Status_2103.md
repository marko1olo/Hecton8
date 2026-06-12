# Status 2103

ID: 2103  
Role: CORAL_REEF_FLORA_FINAL_READY_SOURCE_PACKAGE_CONSTRAINTS  
Batch: batch21_art_replacement_wave  
Status: STATIC VERIFIED for source constraints. Runtime/source generation/Unity proof is PENDING VERIFICATION.

## No-Unity Acknowledgment

Unity, MCP, Play Mode, profiler, imports, dotnet build, csc, and project build were not run.

## Authority And Evidence Read

STATIC VERIFIED:

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `PROJECT_BIBLES.md`
- `quality.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `3dmodel.md`
- `3DMODEL_FLORA_CORAL.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `3DMODEL_HERO_REALISM_OVERKILL.md`
- `world.md`
- `terrain.md`
- `water.md`
- `rendering.md`
- `shaders.md`
- `.agents-skills/REND_Instanced_Flora_Physics.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `Docs/Reports/Batch20/2019_PRIMITIVE_PROXY_ART_DEBT_ELIMINATION_PLAN.md`
- `Docs/Reports/Batch20/2019_PROXY_DEBT_QUEUE.csv`
- `Docs/Reports/Batch20/2019_GENERATION_ROUTE_MATRIX.csv`
- `Docs/Reports/Batch21/2022_GEMINI_TEXTURE_BUDGET_AND_PROMPT_QUEUE.md`
- `Docs/Reports/Batch21/2022_gemini_texture_generation_queue.csv`
- `Docs/GeneratedAssets/Gemini/Prompts/Batch21/2022_priority_texture_prompts_20260604.md`
- `Docs/Reports/Batch20/2004_BIOFORGE_FLORA_CORAL_SOURCE_PACKAGE.md`
- `Docs/Reports/Batch20/2004_FLORA_CORAL_VARIANT_MATRIX.csv`
- `Docs/Reports/Batch20/2004_TEXTURE_CHANNEL_CONTRACTS.csv`
- `Docs/Reports/Batch20/2004_GENERATION_HANDOFF_CHECKLIST.md`
- `Docs/Reports/Batch20/2004_PROMPT_PACKS.md`

`Docs/Actual Domains of Project.txt` was missing. Narrow domain inferred from task: flora/coral/reef source package constraints and QA gates only.

## Discovered Evidence

STATIC VERIFIED:

| Evidence | Result |
| --- | --- |
| `2019-Q006` | Dry-land kelp blocker open. |
| `2019-Q007` | Dry-land coral blocker open. |
| `2019-Q009` | Kelp proxy finals blocker open. |
| `2019-Q010` | Coral proxy finals blocker open. |
| `2019-Q014` | BioForge/default material fallback blocker open. |
| `2019-G004` | Kelp tall hero planned, not imported. |
| `2019-G005` | Kelp patch/canopy planned, not imported. |
| `2019-G006` | Intertidal shoreline flora source missing. |
| `2019-G007` | Branching coral planned, not imported. |
| `2019-G008` | Massive/plate/low coral planned, not imported. |
| `2019-G009` | Soft reef fan source missing or new family. |
| `2022` rank 4 | Shallow branching coral source candidate exists as prompt queue item. |
| `2022` rank 8 | Kelp blade/holdfast source candidate exists as prompt queue item. |
| `2004` optional files | Present and integrated as static evidence. |

No flora/coral evidence absence blocker. Task proceeded.

## Active Constraints

STATIC VERIFIED:

- Accepted families: tall kelp, kelp patch/canopy, intertidal shoreline flora, branching coral, massive coral, plate coral, low sponge/floor coral, soft reef fan, anchor debris encrustation.
- Dry-land kelp/coral is rejected. Shoreline flora is separate intertidal/coastal family.
- Primitive stalks, ribbon-only blades, smooth tube bouquets, smooth blobs, paper-thin plates, flat/noisy carpets, alpha-only fans, starter material finals, and proxy fallback finals are rejected.
- Vertex colors must keep R=sway, G=biolum/mask, B=AO/cavity, A=family/wear/thickness/damage/wetness/variant meaning.
- `_BaseMap`, `_DetailMap`, `_NormalMap`, `_MaskMap`, optional thickness/subsurface, and BioForge ORMA exception are documented.
- Low/Middle/High/Ultra consequences are continuous `GlobalQualityWeight` documentation checkpoints, not binary quality switches.
- Static constraints are not visual proof.

## Files Created

STATIC VERIFIED:

- `Docs/Reports/Batch21/2103_CORAL_REEF_FLORA_SOURCE_PACKAGE_CONSTRAINTS.md`
- `Docs/GeneratedAssets/Gemini/Prompts/Batch21/2103_coral_reef_flora_prompt_requirements.md`
- `Docs/Tasks/Status_2103.md`
- `Docs/AgentLogs/Rationale_2103.md`
- `Docs/AgentLogs/LOG_2103.md`

## Pending Proof

PENDING VERIFICATION:

- source image generation;
- source intake QA on actual images;
- PBR derivation;
- mesh generation;
- Unity import;
- material binding;
- placement rule edits;
- route captures;
- overdraw/profiler;
- GC/memory/VRAM;
- player/build proof.
