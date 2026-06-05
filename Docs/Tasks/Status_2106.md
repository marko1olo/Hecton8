# Status 2106

Batch: batch21_art_replacement_wave
Task: PRODUCTFACE_RESOURCE_TOOL_PICKUP_MATERIAL_MESH_SOURCE_PACKAGE
Evidence class: STATIC_DOC / STATIC_SOURCE_REVIEW

## Checklist

- [x] Read `AGENTS.md`.
- [x] Read required root bibles and relevant UI/construction edge bibles.
- [x] Loaded 7 relevant mandates from `.agents-skills`.
- [x] Read 2022 Gemini texture queue decision.
- [x] Targeted Batch18 product-face/tool/pickup evidence search completed under `Docs/Reports/Batch18`.
- [x] Read relevant Batch18 evidence files and extracted blocker rows.
- [x] Separated tool/product-face source scope from broad inventory/crafting redesign.
- [x] Wrote final source package report.
- [x] Wrote prompt pack.
- [x] Wrote Rationale and LOG.

## Authorities Read

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `PROJECT_BIBLES.md`
- `quality.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `3dmodel.md`
- `3DMODEL_EQUIPMENT_PROPS.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `3DMODEL_HERO_REALISM_OVERKILL.md`
- `tools.md`
- `inventory.md`
- `construction.md`
- `ui.md`
- `UI_DIEGETIC_HUD_STANDARDS.md`
- `rendering.md`
- `shaders.md`
- `Docs/Reports/Batch21/2022_GEMINI_TEXTURE_BUDGET_AND_PROMPT_QUEUE.md`

Mandates:

- `CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `QA_Evidence_Text_Filter_Audit.txt`

## Batch18 Evidence Read

- `Docs/Reports/Batch18/1864_PRODUCT_FACE_PRIMITIVE_REPLACEMENT_QUEUE.md`
- `Docs/Reports/Batch18/1864_PRODUCT_FACE_REPLACEMENT_MATRIX.csv`
- `Docs/Reports/Batch18/1869_TOOL_VISUAL_SOURCE_PACKAGE.md`
- `Docs/Reports/Batch18/1870_RESOURCE_PICKUP_VISUAL_SOURCE_PACKAGE.md`
- `Docs/Reports/Batch18/1874_TOOL_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md`
- `Docs/Reports/Batch18/1875_RESOURCE_PICKUP_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md`
- `Docs/Reports/Batch18/1880_TOOL_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1881_RESOURCE_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1888_PRODUCT_FACE_TEXTURE_CHANNEL_MANIFEST_AND_SHADER_AUDIT.md`
- `Docs/Reports/Batch18/1896_TOOLSCREEN_DIEGETIC_SHADER_CHANNEL_AUDIT.md`

## Files Created

- `Docs/Reports/Batch21/2106_PRODUCTFACE_RESOURCE_TOOL_PICKUP_SOURCE_PACKAGE.md`
- `Docs/GeneratedAssets/Gemini/Prompts/Batch21/2106_productface_tool_resource_prompts_20260604.md`
- `Docs/Tasks/Status_2106.md`
- `Docs/AgentLogs/Rationale_2106.md`
- `Docs/AgentLogs/LOG_2106.md`

## CANDIDATE Unresolved Evidence

- Scanner/tool casing material source: CANDIDATE. No generated image.
- Resource ore pickup material source: CANDIDATE. No generated image.
- ToolScreenDiegetic premium screen wear/glass channel: BLOCKED until material/shader channel extension and proof.
- ProductFace tool mesh authoring route from 1874: CANDIDATE until Unity execution and generated mesh asset proof.
- Resource pickup mesh authoring route from 1875: CANDIDATE until Unity execution and generated mesh asset proof.
- `Item_Titanium` canonical/quarantine state: CANDIDATE until future production-reference scan and Unity-owner relink/quarantine proof.

## Verification State

STATIC VERIFIED:

- 2022 evidence read.
- Relevant Batch18 evidence found and read.
- Source constraints and QA gates written.
- Prompt pack written.

PENDING VERIFICATION:

- image generation;
- mesh generation;
- Unity import;
- prefab binding;
- scene/tool/pickup captures;
- interaction proof;
- profiler, GC, memory, and VRAM proof.
