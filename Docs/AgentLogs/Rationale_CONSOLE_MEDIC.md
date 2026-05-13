# Rationale_CONSOLE_MEDIC

## Decision 001

Problem: User requested Unity Console diagnosis without a matching `<AGENT_PROMPT id="...">` in the active batch.
Solution: Use isolated `CONSOLE_MEDIC` identity and integration/console domain; do not steal neighboring batch prompts.
Rejected Alternatives: Using `PIPE_LOGISTICS_ARCHITECT` because an IDE tab referenced a missing status file; using any active batch prompt ID without explicit user assignment.
Scalability potential: Low/Middle/High/Ultra unaffected until real code changes exist.
Hardware Impact: 0 us runtime; process-only guard.

## Decision 002

Problem: Console fixes can become fake reports if based on static search or partial console reads.
Solution: Apply `QA_Evidence_Text_Filter_Audit` and treat Unity Console data as the primary evidence class for this task.
Rejected Alternatives: `rg`-only diagnosis; dotnet-only proof; speculative success language.
Scalability potential: Low/Middle/High/Ultra unaffected until real code changes exist.
Hardware Impact: 0 us runtime; prevents wrong edits.
