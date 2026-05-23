# LOG_TOKEN_USAGE_AUDIT

## 2026-05-23 Token Usage, Code Lines, Commit Count

What was wrong:

- Prior token ledger stopped at 2026-05-18 and did not include the May 21 cleanup backup plus current May 21-23 sessions.
- Stable docs still carried R51 source-line counters from 2026-05-21.

What was done:

- Scanned 2,616 Codex JSONL session files across backup/current roots.
- Deduped to 2,522 unique session/path keys.
- Counted 87,322,244,824 total Codex tokens from 2,497 sessions with token usage.
- Counted first-party C# under `Assets/_Project`: 2,422 files and 1,701,001 physical lines.
- Counted broader first-party source under `Assets/_Project` plus `Tools`, excluding JSON: 3,015 files and 1,859,225 physical lines.
- Counted Git history: 735 commits on `HEAD`/`origin/main`, 747 commits across all refs.
- Added `Docs/TOKEN_USAGE_LEDGER.md` and `Docs/Reports/2026-05-23_TOKEN_USAGE_CODEBASE_AND_COMMIT_COUNTERS.md`.

Cinematic Cheats used:

- None. Audit/documentation only.

Exact microseconds saved:

- 0 us measured. No runtime code changed.

Verification:

- JSON parse/read errors: 0.
- Duplicate records removed from token total: 94.
- SQLite inspected for table presence/counts only; JSONL remained primary accounting source.
- Unity compile/import/profiler not run because only documentation changed.
