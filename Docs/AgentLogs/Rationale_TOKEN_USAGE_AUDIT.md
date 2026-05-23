# Rationale_TOKEN_USAGE_AUDIT
Date: 2026-05-23 16:11 Europe/Samara
Status: COMPLETE - STATIC LOCAL TELEMETRY REFRESHED

## Decision 1

Problem: User asked for all-time Codex tokens while old sessions were moved into a cleanup backup and current `.codex` still contains overlapping recent sessions.
Solution: Scan backup `old_sessions`, current `sessions`, and current `archived_sessions`; dedupe by `session_meta.id`; keep highest final `total_tokens` for duplicates.
Rejected Alternatives: Backup-only undercounts May 21-23. Current-only loses older moved sessions. Raw root-sum double-counts overlap.
Scalability potential: Low/Middle/High/Ultra runtime tiers unaffected; this is workflow telemetry.
Hardware Impact: 0 us runtime gain; offline filesystem scan only.

## Decision 2

Problem: Codex JSONL files contain repeated token telemetry snapshots.
Solution: Use final per-session `payload.info.total_token_usage` as the accounting row.
Rejected Alternatives: Summing `last_token_usage` or all `total_token_usage` rows was rejected because it counts the same session growth repeatedly.
Scalability potential: Same method remains valid as session count grows because each session contributes one final cumulative row.
Hardware Impact: 0 us runtime gain; prevents accounting error only.

## Decision 3

Problem: Existing compute token docs were moved into deprecated sanitized documentation bundles, while stable docs still pointed at stale compute-report paths.
Solution: Create `Docs/TOKEN_USAGE_LEDGER.md` as current stable token surface and link the dated report from governance/architecture docs.
Rejected Alternatives: Updating deprecated bundles as current authority was rejected because their path explicitly marks them historical. Chat-only answer was rejected because the user asked to update token documentation.
Scalability potential: Future audits have one active ledger instead of searching archived batches.
Hardware Impact: 0 us runtime gain.

## Decision 4

Problem: "Lines of code" can mean C# only, scripts only, non-test, or broad source including shaders/tools.
Solution: Report separate scopes and designate first-party C# under `Assets/_Project` as the primary answer.
Rejected Alternatives: Single broad count was rejected because it hides shader/tool/data inclusion. Comment-stripped SLOC was rejected because no project-local SLOC standard exists in current docs.
Scalability potential: Scope separation prevents future counter drift from being misread as runtime quality.
Hardware Impact: 0 us runtime gain.

## Decision 5

Problem: Runtime-fix work and active Codex sessions made the 15:05 token/LOC/commit snapshot stale on the same day.
Solution: Refresh JSONL token totals with shared-read streaming, refresh source-line scopes, and refresh Git commit counters before answering.
Rejected Alternatives: Reusing the 15:05 snapshot was rejected because current sessions and pushed commits changed. Querying billing/provider data was unavailable, so the evidence boundary remains local filesystem telemetry.
Scalability potential: Low/Middle/High/Ultra runtime tiers unaffected; this is workflow accounting.
Hardware Impact: 0 us runtime gain; offline filesystem/Git scan only.
