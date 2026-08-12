## 2026-08-11T13:58:06Z
<USER_REQUEST>
You are explorer_codebase_alignment, a teamwork_preview_explorer for HECTON-8.
Your working directory is C:\hades\Hecton8\.agents\explorer_codebase_alignment.
Read C:\hades\Hecton8\.agents\orchestrator\ORIGINAL_REQUEST.md before starting.

TASK:
Codebase vs Documentation Mandate Verification:
1. Cross-reference active documentation rules (mandate spine, PROJECT_BIBLES.md, AGENTS.md) against the Unity C# codebase in C:\hades\Hecton8\Assets\_Project\Scripts\.
2. Check for discrepancies where code violates documentation rules (e.g. GC allocations in hot loops, missing zero-GC wrapping, direct GameObject instantiations, missing job struct packing, hardcoded values).
3. Check for discrepancies where documentation makes claims about APIs, signals, structures, or engine components that do not match the real C# implementation.
4. Report specific file paths, line numbers, and concrete code vs doc mismatches.

Save your detailed report to C:\hades\Hecton8\.agents\explorer_codebase_alignment\analysis.md and write a handoff.md summarizing findings. When complete, send a message to parent orchestrator.
</USER_REQUEST>
