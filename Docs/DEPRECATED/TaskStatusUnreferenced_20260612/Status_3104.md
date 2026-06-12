# Status_3104

ID: 3104
Role: SHORELINE_TERRAIN_ASSET_OWNER
Status: COMPLETE / STATIC VERIFIED / UNITY PROOF PENDING
Date: 2026-06-05

## Scope

Owned classification for shoreline terrain/material recovery:
- wet basalt source candidates;
- shell/sand and photic seabed candidates;
- foam/contact/caustic support requirements;
- first-party terrain/geology material route;
- broken/stale material route blockers;
- geology mesh replacement requirements.

No `Assets` imports. No material edits. No scene edits. No random asset intake.

## Mandates Followed

- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/TOOL_Procedural_Wreckage_Generator.txt`

## Evidence

- `STATIC_DOC`: AGENTS/TASTE/VISION_LOCKS/terrain/world/3dmodel/geology/textures/playbook/procedural/quality docs.
- `STATIC_SOURCE`: material YAML, shader source, texture import metas, screenshot metadata.
- `STATIC_IMAGE_QA`: Batch31 LocalPBR contact sheet and source manifests.
- `SCREENSHOT_REVIEW`: existing MCP screenshot metadata only.

## Output

- `Docs/Reports/Batch31/3104_SHORELINE_TERRAIN_ASSET_OWNER.md`
- `Docs/AgentLogs/Rationale_3104.md`
- `Docs/AgentLogs/LOG_3104.md`

## Disposition

Current wet basalt and shell/sand sources are useful source/prototype inputs, not production imports.

`terrain.mat` and `Mat_TriplanarRock.mat` are stale/broken routes. Do not patch their missing GUIDs. Replace assignments through Unity owner with current first-party material routes.

Valid static material/shader candidates exist, but they are incomplete until Unity readback, import settings, route screenshots, Frame Debugger/render proof, and PBR role completion are produced.

First-20-minutes impact: removes a surface/shoreline route blocker by separating usable material source debt from production import blockers and defining the controlled replacement path for readable wet shoreline geology.

## Pending

- Unity material readback.
- Scene assignment replacement through Unity API.
- Compact/middle/high/ultra screenshots.
- Frame Debugger / RenderGraph proof for shader route.
- Profiler/GC/VRAM proof after import or scene material changes.
- Geometry replacement proof for black primitive shoreline objects.
