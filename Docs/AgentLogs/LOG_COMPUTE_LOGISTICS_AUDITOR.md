# LOG_COMPUTE_LOGISTICS_AUDITOR

## 2026-05-16 Compute Continuation

What was wrong: The previous compute audit was stale by about 1.685B JSONL final tokens. The user redirected away from Timaert back to HECTON-8 token/cost accounting.

What was done:

- Re-read `AGENTS.md`.
- Re-scanned first-party script LOC.
- Re-scanned `Docs/AgentLogs`, `Docs/Tasks`, and `Docs/Reports`.
- Re-scanned `.codex` SQLite and full JSONL session ledger.
- Recomputed cache-aware cost, no-cache equivalent, rolling rates, prompt cadence, tokens/LOC, tokens/code-byte, and energy equivalents.
- Wrote `COMPUTE_AUDIT_BRIEF.md`.
- Wrote `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_AUDIT_INDEX.md`.
- Wrote `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_TOKEN_BURN_RATE_LEDGER.md`.
- Appended the 2026-05-16 addendum to `Docs/Reports/COMPUTE_DOMINANCE_REPORT.md`.

Cinematic cheats used: None. This is accounting.

Exact microseconds saved: Not applicable. The useful saving is analytical: SQLite-only cost estimation was rejected because it would lose cache/output split; full JSONL scan avoided a fake cost model.

Key numbers:

- JSONL final tokens: 47,456,271,437.
- Cached input ratio: 96.017%.
- Cache-aware estimate: USD 32,007.67.
- No-cache equivalent: USD 210,561.57.
- Last 24h: 3,240,421,310 tokens; USD 2,525.79 cache-aware.
- Meaningful script LOC: 809,871.
- Tokens per meaningful LOC: 58,597.32.
- Energy: 2,372.81 MWh.

STATUS: AUDIT COMPLETE.

## 2026-05-16 H-Phi Token Correlation

What was wrong: H-Phi correlation was previously marked not proven. The user explicitly requested H-Phi measurement, so the missing evidence had to be produced instead of guessed.

What was done:

- Ran `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json` on current disk.
- Saved current artifact as `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_CURRENT_20260516_171857.json`.
- Re-parsed historical H-Phi artifacts with UTF-8/UTF-16 autodetection.
- Built `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_TIMESERIES_EXTRACT_20260516.json`.
- Wrote `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_TOKEN_CORRELATION_20260516.md`.
- Ran a fresh 30-second SQLite live token pulse at 23:14 local.

Cinematic cheats used: None. This is accounting/static architecture measurement.

Exact microseconds saved: No runtime microseconds claimed. The H-Phi scan took about 57,022 ms wall-clock.

Key numbers:

- Runtime H-Phi risk: 0.004164939.
- Runtime H-Phi narrow: 0.060806118.
- Data sovereignty: 0.114950891.
- Baseline-to-current H-Phi token spend: 2,464,254,349 tokens.
- Cache-aware cost between H-Phi artifacts: USD 1,947.70.
- Pearson token correlation: risk r=0.522; narrow r=0.493; data sovereignty r=0.492.
- Latest SQLite total: 49,767,593,348 tokens.
- Latest 30-second live burn: 3,815,200 tokens; 127,173.33 tokens/sec.

STATUS: AUDIT COMPLETE.

## 2026-05-16 Last 6H JSONL Cadence

What was wrong: The 14:57 SQLite pulse was useful but too short to represent a stable six-hour average, and SQLite does not carry input/cache/output billing split.

What was done:

- Scanned 47 recent JSONL files, 335,002,125 bytes.
- Parsed 8,388 token rows inside the last-six-hour window.
- Used cumulative usage deltas with pre-window baseline; no negative deltas; two rows used `last_token_usage` fallback.
- Counted prompt cadence rows separately from token deltas.
- Wrote `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_LAST6H_PROMPT_TOKEN_AUDIT.md`.

Cinematic cheats used: None. This is accounting.

Exact microseconds saved: Full 8.49GB historical JSONL rescan avoided. Recent-file scan preserved cache/output split for the active window.

Key numbers:

- Last 6h tokens: 757,394,868.
- Cache ratio: 95.599%.
- Average rate: 35,064.58 tokens/sec; 2,103,874.63 tokens/min.
- Peak minute: 15,133,220 tokens at 2026-05-16T10:13+04:00.
- Cache-aware cost: USD 607.01.
- No-cache equivalent: USD 3,853.78.
- Explicit user-message rows: 146; peak prompt minute: 15 rows.

STATUS: AUDIT COMPLETE.

## 2026-05-16 Continuation Rebase 14:57

What was wrong: The audit state was stale again. `.codex` token totals kept increasing and first-party source changed under concurrent HECTON agents.

What was done:

- Re-read status, rationale, root brief, and live delta from disk.
- Ran a new 60-second read-only SQLite live-tail sample.
- Re-scanned `Assets/_Project/Scripts/**/*.cs` for current physical LOC, meaningful LOC, bytes, domain weights, and contract-like split.
- Inspected `logs_2.sqlite` schema after a failed continuation query and reran the tail sample against `ts`/`ts_nanos`.
- Appended updated numbers to the root brief, live delta, token ledger, log DB audit, index, status, rationale, and this log.

Cinematic cheats used: None. This is accounting.

Exact microseconds saved: Full JSONL rescan avoided for this continuation pulse. The accepted tradeoff is explicit: post-03:56 deltas use SQLite totals with the previous JSONL blended `gpt-5.5` rates.

Key numbers:

- Current SQLite tokens: 48,761,315,725.
- 60-second burn: 5,591,521 tokens.
- Live rate: 93,192.02 tokens/sec; 5,591,521 tokens/min.
- Cache-aware live rate: USD 4.28/min; USD 256.95/hour; USD 6,166.81/day.
- No-cache live rate: USD 28.40/min; USD 1,704.18/hour; USD 40,900.39/day.
- First-party meaningful LOC: 827,838.
- Tokens per meaningful LOC: 58,902.00.
- Estimated current cache-aware total: USD 33,007.19.
- Current energy estimate: 2,438.07 MWh.

STATUS: AUDIT COMPLETE.

## 2026-05-16 Log DB Audit

What was wrong: `logs_2.sqlite` was being referenced as forensic evidence, but full grouping over the DB is too heavy for an interactive pass and it is not a token ledger.

What was done:

- Checked `PRAGMA quick_check`: ok.
- Recorded DB size, WAL size, row count, timestamp range, and estimated byte sum.
- Ran a latest-5,000-row sample and grouped it by level, target, module, and thread.
- Wrote `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_LOG_DB_AUDIT.md`.

Cinematic cheats used: None. This is accounting.

Exact microseconds saved: Avoided another timed-out global grouping pass; used bounded sample instead.

Key numbers:

- `logs_2.sqlite`: 3,569,434,624 bytes.
- WAL: 406,367,992 bytes.
- Rows: 486,917.
- `sum(estimated_bytes)`: 2,970,778,869.
- Latest 5,000-row sample: 8 ERROR rows, 1,972 TRACE rows, 2,277 INFO rows.

STATUS: AUDIT COMPLETE.

## 2026-05-16 Live Tail Continuation

What was wrong: The full JSONL snapshot was already stale because `.codex` continued writing after 03:56 local.

What was done:

- Ran a 30-second SQLite tail sample from 05:18:34 to 05:19:04 local.
- Measured +2,189,017 tokens over 30.14228 seconds.
- Identified 10 active threads, all `gpt-5.5`.
- Wrote `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_LIVE_DELTA_20260516.md`.
- Updated root `COMPUTE_AUDIT_BRIEF.md`, index, status, and rationale.

Cinematic cheats used: None. This is accounting.

Exact microseconds saved: Full JSONL rescan avoided for a live-tail check; SQLite tail gave the current burn signal without another multi-minute 8.49GB pass.

Key numbers:

- Live 30-second delta: 2,189,017 tokens.
- Live rate: 72,622.81 tokens/sec.
- Cache-aware rate: USD 3.34/min; USD 200.24/hour; USD 4,805.68/day.
- Delta since full 03:56 snapshot: +339,069,286 tokens, about USD 259.69 cache-aware.

STATUS: AUDIT COMPLETE.

