# Rationale_CONSOLE_MEDIC

## Decision 001

Problem: Active `Status_CONSOLE_MEDIC.md` and `Rationale_CONSOLE_MEDIC.md` were missing because another process moved the prior loop into `Docs/Archive/Batch004`.
Solution: Read the archived files, recreate active state files, and continue from the archived evidence instead of restarting from memory.
Rejected Alternatives: Reverting the archive move; ignoring the missing active state; writing only to chat.
Scalability potential: Low/Middle/High/Ultra unaffected directly; process continuity prevents duplicate or contradictory fixes.
Hardware Impact: 0 us runtime; process-only guard.

## Decision 002

Problem: New `CURRENT_BATCH.md` exists and may contain fresh assignments, but no `CONSOLE_MEDIC` block was present.
Solution: Treat the user's repeated request as a direct integration/console interrupt and avoid neighboring agents' prompts.
Rejected Alternatives: Hijacking active batch roles such as QUALITY or SIMULATION agents; parsing Polish Mandate before core work.
Scalability potential: Low/Middle/High/Ultra unaffected directly; protects domain boundaries under parallel execution.
Hardware Impact: 0 us runtime; process-only guard.

## Decision 003

Problem: Stale Unity log warnings listed obsolete `GetInstanceID`, `FindFirstObjectByType`, and internal dispatcher time usage, but previous source edits may already have resolved them.
Solution: Search current touched source before editing; no matching stale patterns remained in the touched files.
Rejected Alternatives: Editing line numbers from stale log text; broad project-wide deprecation churn.
Scalability potential: Low/Middle/High/Ultra unaffected until a current defect is confirmed.
Hardware Impact: 0 us runtime; prevents unnecessary reimport/diff churn.
