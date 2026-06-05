# Codex Chat Launch Queue 2026-06-04

Purpose: queue for separate visible Codex chats, not `spawn_agent` subagents.

Use separate Codex chats when the work should be durable, visible in the VS Code Codex tab list, steerable by the controller, and capable of continuing after a report. Use `spawn_agent` only for narrow support research.

Owner name override: `Продолжить работу по логам`. If an older owner line in this file shows mojibake, ignore it and use this override.

## Current Unity Constraint

Unity owner: `Продолжить работу по логам`.

Do not launch a second Unity-heavy chat until that owner hands over the Unity slot or clearly finishes. Task `2001` is the only Batch20 Unity-slot task.

## Launch First As Separate Codex Chats

1. `2002_STATIC_SURFACE_SHALLOW_SCENE_REPAIR_LEDGER`
   - Why: high-level static ledger for all surface/photic blockers.
   - Safe while Unity is busy: yes.
   - Must not run Unity/build/MCP.

2. `2003_KELP_ROCK_DRY_LAND_PLACEMENT_REPAIR_SPEC`
   - Why: prevents repeat of land kelp, proxy coral, direct LOD0 shoreline dressing.
   - Safe while Unity is busy: yes.
   - Must not edit rule assets until a data/code owner explicitly takes the slot.

3. `2005_GEOLOGY_FORGE_SHORELINE_ROCK_SOURCE_PACKAGE`
   - Why: coastline currently reads grey/procedural; direct flattened LOD0 rocks are not enough.
   - Safe while Unity is busy: yes.
   - Output should make later GeologyForge/Unity execution concrete.

4. `2007_OCEAN_SHORELINE_WATERLINE_RENDER_PROOF_PACKET`
   - Why: waterline/ocean is still a visible blocker; current capture reads dark green and flat.
   - Safe while Unity is busy: yes.
   - Output should define material/source/proof gates.

5. `2012_SCENE_REPAIR_INTEGRATION_BACKLOG_AND_OWNER_HANDOFF`
   - Why: once 2002/2003/2005/2007 reports exist, this becomes the practical repair queue for the Unity owner.
   - Safe while Unity is busy: yes.
   - It must treat missing sibling outputs as `PENDING`, not fake completion.

## Launch Later

1. `2001_UNITY_SLOT_VISUAL_PROOF_CAPTURE_AND_TRIAGE`
   - Launch only after Unity handoff.
   - Must capture Game/Scene matching, shoreline closeup, underwater 0-5m, underwater 20-50m, Aegir long/crop, 360 sky, Low/Middle/High/Ultra.
   - Must save proof outside `Assets`.

2. `2004_BIOFORGE_FLORA_CORAL_SOURCE_PACKAGE`
   - Can run now, but it benefits from 2003 scatter constraints and 1908 atlas package.

3. `2006_SKY_AEGIR_MOONS_CLOUD_SOURCE_AND_VALIDATOR_PACKET`
   - Partially covered by `SKY_AEGIR_MOONS_SOURCE_ROLE_PACKAGE_20260604.md`; launch as separate chat if more source/validator detail is needed.

4. `2008_PRODUCTFACE_RELINK_AND_CHANNEL_CONTRACT_UNITY_HANDOFF`
   - Partially covered by `PRODUCT_FACE_SOURCE_MANIFEST_PLAN_20260604.md`; launch as separate chat if relink checklist needs expansion.

5. `2009_GEMINI_PROMPT_PACKS_FOR_SURFACE_SHALLOWS`
   - Use when a GUI/Gemini operator is ready to generate actual image candidates.

6. `2010_QUALITY_SCALABILITY_MATRIX_AND_PROOF_CHECKLIST`
   - Launch once enough domain packets exist, or sooner if agents start weakening low-tier visuals.

7. `2011_STATIC_VALIDATOR_RUNBOOK_FOR_VISUAL_DEBT`
   - Launch when the controller needs a repeatable static gate for primitive/default/material debt.

## Copy-Paste Prompt Template For Separate Chat

```
You are a HECTON-8 Codex implementation/planning agent in a separate visible Codex chat.

Workspace: C:\hades\Hecton8
Task file: C:\hades\Hecton8\taskslocal\batch20_unity_slot_visual_proof_and_scene_repair\<TASK_FILE>.txt

Read the full task file and execute it.

You are not alone in the codebase. Other agents and the Unity owner `Продолжить работу по логам` may be active.

Hard rules:
- Obey `C:\hades\Hecton8\AGENTS.md`, `TASTE.md`, `VISION_LOCKS.md`, `PROJECT_BIBLES.md`, and relevant domain docs/mandates.
- Do not run Unity, MCP, Play Mode, profiler, imports, or builds unless the task file explicitly marks you as the Unity-slot owner and the current Unity owner has handed off.
- Do not edit active `Assets` scene/material/prefab/rule/script files unless the task explicitly grants that scope.
- Do not fake visual proof from static YAML, old screenshots, or path existence.
- Surface/sky/Aegir/coast/ocean/photic shallows must be bright, beautiful, readable, Subnautica-level or better.
- Primitive rocks, land kelp, proxy coral, package/default materials, and placeholder-looking assets are rejected.

Create/update:
- `Docs/Tasks/Status_<ID>.md`
- `Docs/AgentLogs/Rationale_<ID>.md`
- `Docs/AgentLogs/LOG_<ID>.md`

Keep logs concise and factual. Final report must list files changed, proof produced, blockers, and whether Unity/build was not run.
```
