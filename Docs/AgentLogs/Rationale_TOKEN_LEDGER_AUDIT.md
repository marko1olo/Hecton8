# Rationale_TOKEN_LEDGER_AUDIT

Date: 2026-05-18
Status: AUDIT COMPLETE / LOCAL TELEMETRY ONLY

## Decision 1

Problem: User requested updated token counts, and existing counts already lived in compute-audit docs.
Solution: Preserve the established accounting format under `Docs/Reports/2026-05-16_COMPUTE_AUDIT` and add a dated rebase file.
Rejected Alternatives: A chat-only answer was rejected because the project rules require durable logs. A new unrelated ledger was rejected because it would split the source of truth.
Scalability potential: Low/Middle/High/Ultra device tiers are not touched; this is documentation telemetry only.
Hardware Impact: 0 us runtime gain on i3/MX350; no Unity runtime path changed.

## Decision 2

Problem: `.codex` JSONL repeats token telemetry rows, and naive summing overcounts usage.
Solution: Use final per-session `total_token_usage` for lifetime totals and per-thread cumulative deltas with pre-window baselines for rolling windows.
Rejected Alternatives: Summing `last_token_usage` was rejected because earlier compute audits already proved it double-counts repeated snapshots. Estimating from file bytes was rejected because JSONL exposes token counters.
Scalability potential: Report remains useful across cheap and top-tier machines because it measures workflow burn, not runtime rendering quality.
Hardware Impact: 0 us runtime gain; audit CPU time was offline local filesystem work.

## Decision 3

Problem: Windows long-path CWD values initially caused `\\?\C:\hades\Hecton8` to be excluded from HECTON totals.
Solution: Normalize `\\?\` prefixes and treat both `C:\hades` and `C:\hades\Hecton8` as HECTON/Hades scope.
Rejected Alternatives: Reporting the first 36.5B-token HECTON subtotal was rejected because it undercounted by roughly 17.95B tokens. Including `C:\Users\danat\Downloads` in HECTON totals was rejected because it is non-project drift.
Scalability potential: Continuous scope normalization avoids future project-root drift when agents run from either repository root or workspace root.
Hardware Impact: 0 us runtime gain; prevents accounting error only.

## Decision 4

Problem: Existing top-level tables are historical 2026-05-16 snapshots, but user asked for current numbers.
Solution: Add append-only current sections and a separate `COMPUTE_TOKEN_REBASE_20260518_1734.md`; do not erase historical snapshots.
Rejected Alternatives: Rewriting old snapshot tables was rejected because it destroys audit chronology. Leaving old docs untouched was rejected because the user explicitly asked to update counts.
Scalability potential: Low/Middle/High/Ultra runtime tiers unaffected; documentation chronology remains stable for future audits.
Hardware Impact: 0 us runtime gain; no compile or Unity import required.

