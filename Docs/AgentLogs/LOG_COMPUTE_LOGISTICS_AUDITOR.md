# LOG_COMPUTE_LOGISTICS_AUDITOR

## 2026-05-15 - Compute Dominance Audit

What was wrong:
- The project had economic claims around 1.63M LOC and 43+ agent workflows without a current local token/LOC reconciliation.
- `.codex` JSONL contains repeated `last_token_usage` telemetry; naive summing would overcount.
- H-Phi and token burn are stored in different evidence surfaces with no valid join key.

What was done:
- Scanned `Assets/_Project/Scripts/**/*.cs`: 1,501 files, 946,341 physical LOC, 775,435 meaningful LOC, 81.94% logic density.
- Scanned all `Assets/**/*.cs`: 4,112 files, 1,581,522 physical LOC.
- Scanned `Docs/AgentLogs` and `Docs/Tasks`: 6,539,206 estimated AgentLogs tokens and 174,381 estimated Tasks tokens.
- Parsed `.codex` JSONL final per-session `total_token_usage`: 43,423,314,989 total tokens.
- Queried `.codex/state_5.sqlite`: 764 threads, 43,436,372,807 `tokens_used`, 34 spawn edges.
- Calculated raw shadow bill: USD 437,166.04. Cached-input lower-bound bill: USD 21,733.53.
- Calculated energy estimate from supplied constant: 2,171.17 MWh.
- Calculated cadence: peak `.codex` user-message burst 13/sec, 40/min, 202/hour, 748/day, 2,565/week; last 6h 183 prompts, 30.5/hour.
- Calculated R&D compression: 176.24-352.47 human-years for meaningful script LOC, midpoint 234.98 years.
- Generated final report: `Docs/Reports/COMPUTE_DOMINANCE_REPORT.md`.

Cinematic Cheats used:
- None. This was forensic accounting, not physical simulation or presentation work.

Exact microseconds saved:
- Runtime: 0 us. No C# source, shader, prefab, scene, or project setting was changed.
- Process: not claimed. The report identifies audit waste but no profiler measures management time.

Verification:
- `<POLISH_MANDATE>` lookup in active `Docs/Tasks/CURRENT_BATCH.md`: tag not found.
- Report readback confirmed `AUDIT COMPLETE` markers and no `IN_PROGRESS` residue.
- `git diff --check` on touched files passed with LF-to-CRLF warnings only.
- Compile was not launched because an existing `dotnet` process was active and this audit changed markdown only.

STATUS: AUDIT COMPLETE.

## 2026-05-15 - Continuation Addendum

What was wrong:
- The first report correctly captured `.codex` at one point in time, but `.codex/state_5.sqlite` is live and changed during continued work.
- A raw total without concentration hides the real audit target.

What was done:
- Re-read status/rationale/report before continuing.
- Queried current `.codex/state_5.sqlite` concentration: top 100 threads hold 50.237% of current token mass; top 250 hold 76.882%.
- Rechecked model split: `gpt-5.5` holds 72.775% of thread tokens, `gpt-5.4` holds 26.554%.
- Rechecked CWD split: `\\?\C:\hades` holds 58.784% of thread tokens, `\\?\C:\hades\Hecton8` holds 40.675%.
- Added a continuation addendum to `Docs/Reports/COMPUTE_DOMINANCE_REPORT.md`.
- Attempted `logs_2.sqlite` grouping; it timed out twice at 120 seconds and remains partial evidence only.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Runtime: 0 us.
- Process: not claimed.

Verification:
- Report addendum written by `apply_patch`.
- No runtime/code compile run; markdown-only continuation.

STATUS: AUDIT COMPLETE.
