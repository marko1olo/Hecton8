# Rationale_HECTON_PHI_MONITOR

Status: PENDING VERIFICATION
Evidence Class: STATIC_SOURCE / STATIC_DOC only until Unity artifacts exist.

## Decision 1

Problem: The assignment prompt is present in chat but absent from `Docs/Tasks/CURRENT_BATCH.md`; strict batch extraction returned `[PROMPT_NOT_FOUND]`.
Solution: Treat the in-chat XML as the assignment source, record extraction failure as evidence debt, and avoid reading neighboring batch prompts.
Rejected Alternatives: Reading archived batches would violate fresh-context hygiene; inventing a batch path would create false authority.
Scalability potential: Low/Middle/High/Ultra all benefit from audit repeatability; no runtime behavior changed.
Hardware Impact: 0 us runtime. Process-only gain.

## Decision 2

Problem: H-Phi is a meta-audit metric, not a gameplay runtime system.
Solution: Keep work read-only over source plus documentation artifact writes; no script, prefab, scene, material, or project-setting mutation.
Rejected Alternatives: Adding runtime monitor code without explicit integration owner would risk cross-domain sabotage and compile churn.
Scalability potential: Low tier avoids new overhead; high/ultra can later consume the metric in dashboards if authorized.
Hardware Impact: 0 us runtime. Static CLI scan cost is offline only.
