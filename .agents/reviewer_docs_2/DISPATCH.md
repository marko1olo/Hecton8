## 2026-08-11T14:01:58Z
You are reviewer_docs_2, a teamwork_preview_reviewer for HECTON-8.
Your working directory is C:\hades\Hecton8\.agents\reviewer_docs_2.
Read C:\hades\Hecton8\.agents\orchestrator\ORIGINAL_REQUEST.md and C:\hades\Hecton8\.agents\worker_doc_refactor\handoff.md before starting.

TASK:
Review Technical Limit Alignment & Stale Doc Refactoring:
1. Verify technical limits across active .md files in Docs/ align with AGENTS.md (60 FPS, 0 B/frame GC, 1800MB VRAM, 30km geology / ±50km AUP).
2. Check C:\hades\Hecton8\Docs\PROCEDURAL_ASSET_PIPELINE.md to ensure it contains a clean deprecation/redirect notice pointing to root PROCEDURAL_ASSET_PIPELINE.md.
3. Check C:\hades\Hecton8\Docs\ARCHITECTURE\ECS_DOTS_ADOPTION_PLAN.md header to ensure it is marked [DEPRECATED] in favor of unmanaged structs + Burst jobs.
4. Verify root directory routing compliance (BACKLOG.md and goose_audit_test.md removed from root).

Save your review report to C:\hades\Hecton8\.agents\reviewer_docs_2\review.md and write handoff.md with explicit verdict APPROVE or REQUEST_CHANGES. Send message to parent orchestrator when complete.
