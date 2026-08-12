## 2026-08-11T18:00:10Z

You are worker_doc_refactor, a teamwork_preview_worker for HECTON-8.
Your working directory is C:\hades\Hecton8\.agents\worker_doc_refactor.
Read C:\hades\Hecton8\.agents\orchestrator\ORIGINAL_REQUEST.md and the explorer findings in C:\hades\Hecton8\.agents\explorer_doc_audit\handoff.md, C:\hades\Hecton8\.agents\explorer_mandates\handoff.md, and C:\hades\Hecton8\.agents\explorer_codebase_alignment\handoff.md before starting.

TASK SCOPE:
Execute documentation refactoring, archiving, formatting fixes, and Knowledge Graph generation for HECTON-8.

1. Mandate Header Standardization:
   - Edit C:\hades\Hecton8\.agents-skills\AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt so line 1 starts with `# AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC` header formatting.

2. Root Documentation Routing Fix:
   - Move unauthorized root files `BACKLOG.md` and `goose_audit_test.md` from C:\hades\Hecton8\ into C:\hades\Hecton8\Docs\Archive\.

3. Task Log Archiving:
   - Create directory C:\hades\Hecton8\Docs\Archive\Batch014_LegacyTasks\ if it does not exist.
   - Move all 86 completed `Status_*.md` files from C:\hades\Hecton8\Docs\Tasks\ into C:\hades\Hecton8\Docs\Archive\Batch014_LegacyTasks\.

4. Stale/Duplicate Docs Refactoring & Technical Limit Alignment:
   - In C:\hades\Hecton8\Docs\PROCEDURAL_ASSET_PIPELINE.md, replace contents with a deprecation/redirect notice pointing to supreme authority root C:\hades\Hecton8\PROCEDURAL_ASSET_PIPELINE.md.
   - Move temporary playtest logs in `Docs/V0_Playtest/` and outdated static scan reports in `Docs/Reports/DocumentationCompleteness_20260605/` to `Docs/Archive/`.
   - Update `Docs/ARCHITECTURE/ECS_DOTS_ADOPTION_PLAN.md` header marking it DEPRECATED in favor of unmanaged C# Structs + Burst Jobs (AGENTS.md & systems.md).
   - Ensure all active .md files in Docs/ align with canonical limits in AGENTS.md / PROJECT_BIBLES.md (60 FPS target, 0 B/frame GC, 1800MB VRAM, 30km geology / ±50km AUP bounds).

5. Knowledge Graph Deliverable (Docs/HECTON8_KNOWLEDGE_GRAPH.md):
   - Create C:\hades\Hecton8\Docs\HECTON8_KNOWLEDGE_GRAPH.md as a comprehensive, well-structured Markdown knowledge graph and navigation hub for HECTON-8.
   - Map 7 primary domains.

6. Execution & Verification:
   - Run `python Tools/Docs/TestMandateRegistry.py --strict` (MUST exit 0 with 0 errors and 0 warnings).
   - Run `python Tools/Docs/TestAgentRuleRouting.py` (MUST exit 0).
   - Run `git diff --check` (MUST exit 0 with no trailing whitespace or merge conflict markers).
