# Rationale_COMPUTE_LOGISTICS_AUDITOR

## Decision 0 - Evidence Boundary

Problem: The task asks for economic and token accounting over active code, docs, agent logs, and `.codex` history. These are filesystem/static-document facts, not Unity runtime facts.

Solution: Use CLI filesystem scans, byte counts, timestamps, and JSON/JSONL transcript parsing where available. Mark evidence classes explicitly in the final report.

Rejected Alternatives: Treating historical report text as verified truth was rejected because QA_Evidence_Text_Filter_Audit forbids stale proof and unsupported verification language.

Scalability potential: Low devices gain no runtime change. Middle/High/Ultra tiers gain process clarity only by identifying compute waste and report bloat.

Hardware Impact: Runtime gain on i3/MX350 is 0 microseconds because no gameplay code is changed. Process savings are counted separately as avoided audit rework.

## Decision 1 - Prompt Source

Problem: Batch protocol requires extraction from CURRENT_BATCH.md, but the active prompt was supplied inline in chat.

Solution: CLI checked `Docs/Tasks/CURRENT_BATCH.md`; the ID was not found. The inline XML block is therefore the operative assignment and the missing batch entry is recorded as evidence debt.

Rejected Alternatives: Searching archive batches as active authority was rejected because AGENTS.md forbids reading previous-batch logs unless explicitly ordered.

Scalability potential: Keeps the audit bounded to the current task and prevents stale neighboring prompts from polluting metrics.

Hardware Impact: 0 runtime microseconds. Avoids human review time lost to wrong-agent task bleed.

## Decision 2 - LOC Method

Problem: The task requested `cloc`, but `cloc` is not installed in PATH.

Solution: Used a PowerShell CLI scanner over `Assets/_Project/Scripts/**/*.cs`, streaming files line-by-line and subtracting blank plus comment-only lines. Inline comments on code lines were kept as meaningful code lines because they still carry executable source.

Rejected Alternatives: Stale May 13 report counters were rejected because current filesystem churn changed script counts. Pure `wc -l` was rejected because it cannot subtract comments and blanks.

Scalability potential: Low/Middle/High/Ultra runtime tiers are unaffected. Process scalability improves because the audit can be rerun without installing extra tooling.

Hardware Impact: 0 runtime microseconds on i3/MX350. The only gain is audit reproducibility.

## Decision 3 - Domain Weight Classification

Problem: The 85-domain authority map is semantic, while the filesystem is namespace/folder/file based and includes fused legacy hubs.

Solution: Report both namespace-domain weight and top-file outliers. `Hecton8.World` is the heaviest namespace domain; `HectonPlayerMovement.cs` is the heaviest single fused file.

Rejected Alternatives: Hard-mapping every file into one of 85 domains by keyword would create fake precision and pollute the report.

Scalability potential: Low tier benefits from identifying large fused systems that are harder to budget. High/Ultra tiers can use the same map to target visual-overkill domains without bloating core execution.

Hardware Impact: 0 direct runtime microseconds. Indirectly identifies risk surfaces where later profiling may recover frame time.
