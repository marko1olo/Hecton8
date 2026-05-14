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

## 2026-05-15 - Top Thread Triage

What was wrong:
- The root brief identified top-100 `.codex` threads as the next audit target, but the concise evidence was not yet preserved near root.
- The prior report listed high-burn candidates but did not isolate the top-heavy distribution as a separate working queue.

What was done:
- Queried `C:\Users\danat\.codex\state_5.sqlite` read-only.
- Created `COMPUTE_THREAD_TRIAGE.md` at project root.
- Recorded current live snapshot: 764 threads, 43,912,005,185 tokens.
- Recorded concentration: top 30 = 9,492,793,103 tokens, 21.618%; top 100 = 21,944,967,637 tokens, 49.975%; top 250 = 33,698,725,001 tokens, 76.741%.
- Recorded top-100 shape: 78.96% `gpt-5.5`, 21.04% `gpt-5.4`; 55.18% root `C:\hades` CWD; 44.82% `C:\hades\Hecton8` CWD.
- Added top-30 thread queue with IDs, token counts, model, updated timestamp, CWD, and title hints.
- Updated `COMPUTE_AUDIT_BRIEF.md` with a link and evidence warning.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Runtime: 0 us.
- Process: not claimed.

Verification:
- Markdown-only triage. Compile not run.

STATUS: AUDIT COMPLETE.

## 2026-05-15 - Root Brief Preservation

What was wrong:
- The complete compute report was detailed but buried under `Docs/Reports`.
- The user requested concise hard facts closer to the root so future agents do not lose the audit state.

What was done:
- Created `COMPUTE_AUDIT_BRIEF.md` at project root.
- Recorded the latest hard numbers: LOC, total tokens, cached input, cache-aware cost, no-cache equivalent, token flow rates, tokens per LOC, tokens per byte, and energy estimate.
- Added evidence rules: no `last_token_usage` overcount, no double-counted reasoning output, no invoice claim, no proven H-Phi/token correlation, no proven Compute Thief convictions.
- Linked the full report, status, rationale, and log files.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Runtime: 0 us.
- Process: not claimed.

Verification:
- Markdown-only root preservation. Compile not run.

STATUS: AUDIT COMPLETE.

## 2026-05-15 - Cache-Aware Throughput Reprice

What was wrong:
- The earlier bill used the prompt's synthetic GPT-5.5 Spud constants and a zero-cost cached-input floor.
- The user requested actual token flow rates and cache-aware pricing.
- Repeated JSONL telemetry snapshots are still a trap: summing every usage event overcounts.

What was done:
- Re-read Status/Rationale before continuing.
- Verified current OpenAI standard API pricing from official OpenAI pricing/model pages.
- Ran a 348.7s JSONL positive-delta pass over `.codex`.
- Latest JSONL final usage: 43,778,987,916 total tokens; 43,630,634,851 input; 41,886,807,040 cached input; 148,094,665 output.
- Measured throughput: whole-period 12,291.36 tokens/sec, 737,481.59/min, 44,248,895.33/hour, 1,061,973,487.91/day.
- Measured recent throughput: last 1h 92,800.69 tokens/sec; last 6h 61,645.10 tokens/sec; last 24h 28,037.81 tokens/sec.
- Calculated cache-aware cost: USD 29,135.37. No-cache equivalent: USD 191,832.08. Cache avoided: USD 162,696.72, or 84.812%.
- Calculated code ratios: 1,055.433 tokens per script source byte, 56,457.33 tokens per meaningful LOC, USD 0.037573 per meaningful LOC, USD 736.52 per script source MiB.
- Updated `Docs/Reports/COMPUTE_DOMINANCE_REPORT.md`, `Docs/Tasks/Status_COMPUTE_LOGISTICS_AUDITOR.md`, and `Docs/AgentLogs/Rationale_COMPUTE_LOGISTICS_AUDITOR.md`.

Cinematic Cheats used:
- None.

Exact microseconds saved:
- Runtime: 0 us.
- Process: not claimed.

Verification:
- Markdown report updated by `apply_patch`.
- Compile not run; markdown-only audit continuation.

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
