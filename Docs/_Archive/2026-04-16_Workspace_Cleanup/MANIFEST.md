**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# Archive Manifest

Date: `2026-04-17`
Status: `PENDING VERIFICATION`

Purpose: traceability summary for the workspace cleanup archive.

## Archive Scope

The archive contains documentation removed from active root or active `Docs` because it was judged to be:
- stale session output
- one-shot findings/reporting
- agent handoff material
- prompt/chat payload
- superseded audit bundle
- old helper pack not suitable as active source of truth

## Major Moves

### Root -> Archive

- feature migration pack: `implementation_plan.md`, `task.md`, `walkthrough.md`
- findings/reporting: `MY_FINDINGS.md`, `review_issues.md`, `RUNTIME_PROBLEM_REPORT_AND_FILES.txt`, `SUMMARY_OF_WORK_COMPLETED.md`, `oshibka-delete.txt`
- prompt/chat payload: `_AI_RULES.md`, `README (2).md`, `CLAUDE_KELP_SHADER_MASTER_PROMPT.md`, `PROMPT 8 MARTA UTRO.txt`, `DAMP HUYNI IZ ChATA NOVOSTI YuNITI.txt`
- shell completion summaries: `01_MAIN_MENU_PRODUCTION_READINESS.md`, `SETTINGS_IMPLEMENTATION_SUMMARY.md`
- stale procedural snapshots and transfer ledgers
- old flora session docs

### Whole Folders -> Archive

- `Old Md files by Ai agent`
- `GEMINI_ANALYSIS`
- `AI_AGENT_WORK`
- `Work Completed Report`
- `Docs/2026-04-13_Final_Audit`

### Findings Folders Reclassified

- `Ai findings/Project Analysis/Gemini Review` -> archived
- `Ai findings/PostFX_Visual_Stack_Audit_2026-04-12` -> archived
- `Ai findings/2026-04-15_Underwater_Recovery` -> promoted into active `Docs`

## Active Promotion

The following bundle was moved out of mixed findings storage into active `Docs`:

- `Docs/2026-04-15_Underwater_Recovery/HECTON8_UNDERWATER_LOD_PERF_RECOVERY_MASTER_PLAN.md`
- `Docs/2026-04-15_Underwater_Recovery/HECTON8_UNDERWATER_TEXTURE_PROMPTS.md`

The following former root references were also normalized into active `Docs`:

- `Docs/AI_Fauna/AI_CREATURE_ROSTER_ENTERPRISE.md`
- `Docs/AI_Fauna/AI_FAUNA_WORLD_INTEGRATION_REPORT.md`
- `Docs/Legacy_World_Reference/TERRAIN_108_BIOMES_VISION.md`
- `Docs/Legacy_World_Reference/terrain_description.txt`
- `Docs/Legacy_Backlog/beklog.txt`
- `Docs/Legacy_Backlog/spetsifikatsii.txt`
- `Docs/Legacy_Backlog/Polnyy_spisok_nedorabotok_i_plan_razrabotki_—_Project_Submerge_(HECTON-8) - ChITAT, ryadom sozdat dokument - chto i kak isparvlyaem, dianmichno obnovlyat.md`
- `Docs/Legacy_Backlog/Chto_i_kak_ispravlyaem_—_zhivoy_plan.md`

## Cleanup Notes

- empty directories `Design` and `ProfilerCaptures` were removed locally
- empty `Compliance Analysis` directory tree was removed locally
- empty `Ai findings` shell directories were removed locally after active/archive promotion
- root legacy reference docs were moved into active `Docs` bundles
- archived `AI_AGENT_WORK` and `PostFX` folders were flattened after an initial nested move

## Counts After Cleanup Snapshot

- active root text docs: `3`
- active `Docs` text docs: `44`
- archived docs in this cleanup bundle: `128`

Counts are point-in-time and may drift as the repo changes.
