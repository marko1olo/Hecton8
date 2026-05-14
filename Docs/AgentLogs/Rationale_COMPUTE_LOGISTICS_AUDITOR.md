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
