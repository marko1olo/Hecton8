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

## 2026-05-23 Token Usage Refresh 16:11 Europe/Samara

What was wrong:

- The 15:05 token ledger no longer included current 2026-05-23 Codex session growth and six pushed runtime-fix commits.

What was done:

- Re-scanned 2,624 Codex JSONL files across backup/current roots.
- Counted 97,306,917,423 total Codex tokens from 2,599 sessions with token usage.
- Counted first-party C# under `Assets/_Project`: 2,446 files and 1,707,768 physical lines.
- Counted broader first-party source under `Assets/_Project` plus `Tools`, excluding JSON: 3,047 files and 1,866,086 physical lines.
- Counted Git history: 744 commits on `HEAD`/`origin/main`, 756 commits across all refs.
- Updated `Docs/TOKEN_USAGE_LEDGER.md`, `Docs/Reports/2026-05-23_TOKEN_USAGE_CODEBASE_AND_COMMIT_COUNTERS.md`, `Docs/DOC_GOVERNANCE.md`, and `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`.

Cinematic Cheats used:

- None. Audit/documentation only.

Exact microseconds saved:

- 0 us measured. No runtime code changed.

Verification:

- JSON parse/read errors: 0.
- Duplicate records removed from token total: 0.
- `sqlite3` was unavailable in the shell; JSONL final per-session telemetry remained the accounting source.
- Unity compile/import/profiler not run because only documentation changed.


## 2026-05-25 TOKEN_USAGE_AUDIT process/token refresh

What was wrong -> VS Code was responsive, but Unity batch compiles left orphan VBCSCompiler dotnet processes after terminal compile failures. First compile wall was FaunaSensorSuite.maxRayLength after a rename; second wall was HazardZoneManager wrapper compatibility.
What was done -> Stopped only orphan compiler servers after parent death/log completion; current Fauna source now routes the legacy serialized value to maxProbeLength, HazardVaultArray exposes the missing wrapper surfaces, and token ledger was refreshed from 2,741 JSONL files across current and backup roots.
Cinematic Cheats used -> None; audit/process hygiene only.
Exact Microseconds saved -> 0 us game runtime. Workstation contention reduced by terminating orphan compiler servers; no profiler sample, so no runtime timing claim.
Token report -> Docs/Reports/TOKEN_USAGE_AUDIT_2026-05-25.md and .json. Total tokens 95,707,766,654; gpt-5.3-codex standard API-equivalent $27,238.18.
Evidence -> STATIC_LOCAL_CODEX_JSONL_AND_FILESYSTEM plus Unity compile log tails from Temp/X002_UnityDataMonolithProbe_Rerun_FINAL*.log. Runtime/Unity PlayMode proof absent.

Final process pass -> Unity/dotnet/csc/MSBuild/VBCSCompiler count 0. VS Code process tree responsive. CPU still 96 percent from live VS Code/node workload; active `dental-crm` node dev servers were not stopped because they were not orphaned. Guarded build skipped by project rule: CPU >50 percent.


## 2026-05-25 TOKEN_USAGE_AUDIT model-price/statistics refresh

What was wrong -> Prior token report priced broad scenarios but did not separate structurally observed model labels from known-but-unpriced model labels.
What was done -> Added model attribution, model-cost bounds, cache-savings, Pareto/Gini/session/day/context-window/LOC-cost diagnostics, and refreshed ledger/report from 2,745 JSONL files.
Cinematic Cheats used -> None; audit/process hygiene only.
Exact Microseconds saved -> 0 us game runtime. Static telemetry and docs only.
Token report -> Docs/Reports/TOKEN_USAGE_AUDIT_2026-05-25.md and .json. Total tokens 95,853,026,051; all-as-gpt-5.3-codex standard API-equivalent $27,275.85; model-bound known+unpriced-as-gpt-5.5 standard $69,925.03.
Evidence -> STATIC_LOCAL_CODEX_JSONL_AND_FILESYSTEM plus official OpenAI pricing pages. Runtime/Unity PlayMode proof absent.
