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

## 2026-05-17 Midnight Live Rebase And H-Phi Gate Attempt

What was wrong: The 23:14 live token pulse and H-Phi report were already stale. Also, the H-Phi report had a potential ambiguity: score improvement was proven, strict old-budget compliance was not.

What was done:

- Ran a 45-second SQLite live token sample.
- Re-scanned current first-party script LOC.
- Attempted the strict old-budget H-Phi gate.
- Documented that the strict gate timed out after 244 seconds and produced no completed artifact.
- Wrote `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_BUDGET_GATE_ATTEMPT_20260517.md`.
- Updated root brief, index, live delta, token ledger, H-Phi correlation report, status, rationale, and this log.

Cinematic cheats used: None. This is accounting/static architecture measurement.

Exact microseconds saved: 0 runtime us. No runtime system changed.

Key numbers:

- Current SQLite tokens: 49,903,844,533.
- 45-second burn: 4,829,772 tokens.
- Live rate: 107,328.27 tokens/sec; 6,439,696 tokens/min.
- Estimated current cache-aware total: USD 33,882.25.
- Energy estimate: 2,495.19 MWh.
- Meaningful first-party LOC: 836,249.
- Tokens per meaningful LOC: 59,675.82.
- Strict H-Phi old-budget gate: timed out, no artifact.

STATUS: AUDIT COMPLETE.

## 2026-05-17 Recent JSONL Rate Audit 00:52

What was wrong: The attempted full all-history JSONL pass was interrupted and could not be treated as evidence. The audit also needed a fresh denominator for token/LOC and token/byte after concurrent source edits.

What was done:

- Re-read status and rationale from disk.
- Checked for obvious leftover Python/PowerShell process evidence before continuing.
- Ran a bounded recent-file JSONL pass over files modified in the last 30 hours.
- Computed timestamp-windowed 1h, 6h, and 24h token/cost cadence from `last_token_usage`.
- Counted peak token second/minute/hour and prompt minute/hour.
- Ran a 30-second SQLite live-tail sample at 00:52 local.
- Re-scanned `Assets/_Project/Scripts/**/*.cs` for current LOC and bytes.
- Wrote `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_RECENT_JSONL_RATE_AUDIT_20260517.md`.
- Updated the root brief, audit index, token ledger, dominance report, status, rationale, and this log.

Cinematic cheats used: None. This is accounting.

Exact microseconds saved: Full 8+ GB historical rescan avoided after interruption. Bounded JSONL pass read 991,426,469 bytes in about 35 seconds and produced current rate evidence without touching Unity runtime.

Key numbers:

- Last 24h JSONL tokens: 5,364,091,619.
- Last 24h cache-aware cost: USD 4,123.40.
- Last 24h no-cache equivalent: USD 27,223.44.
- Last 24h cache ratio: 96.211%.
- Long-context surcharge events over 272K input: 0 in the bounded pass.
- SQLite current tokens at 00:52: 50,027,664,742.
- 30-second live burn: 2,659,344 tokens.
- Live rate: 88,644.80 tokens/sec; 5,318,688 tokens/min.
- Current meaningful LOC: 836,910.
- Tokens per meaningful LOC: 59,776.64.
- Tokens per script byte: 1,121.40.
- Current energy estimate: 2,501.38 MWh.

STATUS: AUDIT COMPLETE.

## 2026-05-17 Active Thread Burners 01:39

What was wrong: Aggregate live burn proved the system was still consuming tokens, but did not identify current rate contributors.

What was done:

- Ran a 20-second per-thread SQLite delta.
- Listed only threads with positive token delta.
- Wrote the burner table to `COMPUTE_RECENT_JSONL_RATE_AUDIT_20260517.md`, root brief, dominance report, status, and rationale.

Cinematic cheats used: None. This is accounting.

Exact microseconds saved: No full scan. Two read-only SQLite snapshots plus 20-second wait.

Key numbers:

- Total delta: 828,509 tokens.
- Live rate: 41,425.45 tokens/sec; 2,485,527 tokens/min.
- Cache-aware rate: USD 1.90/min; USD 114.22/hour; USD 2,741.25/day.
- Top burners: Build hull repair engine 196,707; Standardize SignalBus lanes 177,518; Build STP dynamic resolution adapter 172,571; Build ballast PID 139,980; CORE_TICK_DILATION 112,168.

STATUS: AUDIT COMPLETE.

## 2026-05-17 H-Phi Live Rebase 02:17

What was wrong: The latest H-Phi report still pointed at the 2026-05-16T17:18 artifact while token burn and source files continued moving.

What was done:

- Re-read status and rationale from disk.
- Ran `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`.
- Wrote `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_CURRENT_20260517_021429.json`.
- Compared current H-Phi scores and counters against the 17:18 artifact.
- Parsed JSONL usage rows between the two H-Phi timestamps.
- Ran a 30-second SQLite live pulse and a current first-party script LOC scan.
- Wrote `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_LIVE_REBASE_20260517_0217.md`.
- Updated root brief, audit index, dominance report, status, rationale, and this log.

Cinematic cheats used: None. This is static architecture/token accounting.

Exact microseconds saved: Strict budget gate was not rerun after the prior timeout. Summary-only H-Phi scan took 157,042 ms and produced a valid artifact.

Key numbers:

- Runtime H-Phi risk: 0.004847023.
- Runtime H-Phi narrow: 0.070058393.
- Data sovereignty: 0.131794933.
- Memory alignment: 0.531571219.
- Delta vs 17:18: +0.000682084 risk, +0.009252275 narrow, +160 DataVault refs, -123 owner-blocked NativeArray refs.
- Token window between H-Phi artifacts: 2,183,475,652 tokens.
- Cache-aware cost between H-Phi artifacts: USD 1,640.89.
- No-cache equivalent: USD 11,075.08.
- Marginal cost: 3,201,182,922 tokens per +0.001 Runtime H-Phi risk.
- SQLite current tokens: 50,313,194,499.
- Meaningful LOC: 837,628.
- Tokens per meaningful LOC: 60,066.28.

STATUS: AUDIT COMPLETE.

## 2026-05-17 H-Phi ROI And Burn Spike 03:15

What was wrong: The 02:17 H-Phi rebase showed marginal cost but did not yet separate cumulative ROI from marginal ROI, and live burn spiked again after the H-Phi scan.

What was done:

- Calculated cumulative H-Phi ROI from the 2026-05-15 baseline to the 02:17 artifact.
- Ran another 20-second per-thread SQLite burn sample at 03:04.
- Queried current SQLite total at 03:15.
- Updated `COMPUTE_HPHI_LIVE_REBASE_20260517_0217.md`, root brief, dominance report, status, rationale, and this log.

Cinematic cheats used: None. This is accounting.

Exact microseconds saved: Avoided full JSONL re-scan for the 03:04 pulse; used SQLite deltas for live burner attribution.

Key numbers:

- Cumulative H-Phi token spend since baseline: 4,647,730,001.
- Cumulative cache-aware H-Phi cost since baseline: USD 3,588.59.
- Cumulative Runtime H-Phi risk delta: +0.004210932.
- Cumulative tokens per +0.001 Runtime H-Phi risk: 1,103,729,531.
- 03:04 live delta: 3,079,626 tokens in 20 seconds.
- 03:04 live rate: 153,981.30 tokens/sec; 9,238,878 tokens/min.
- 03:04 blended cache-aware rate: USD 7.08/min; USD 10,189.43/day.
- 03:15 SQLite total: 50,453,850,790.
- Estimated current cache-aware total: USD 34,303.50.
- Current energy estimate: 2,522.69 MWh.

STATUS: AUDIT COMPLETE.

## 2026-05-17 H-Phi Live Rebase 04:12

What was wrong: The 02:17 H-Phi rebase was already stale, and the latest score movement needed to be separated from cumulative ROI.

What was done:

- Ran `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`.
- Wrote `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_CURRENT_20260517_040910.json`.
- Compared 04:12 H-Phi against the 02:17 artifact.
- Parsed JSONL usage rows between the two H-Phi timestamps.
- Ran a 30-second SQLite live pulse and current first-party script LOC scan.
- Wrote `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_LIVE_REBASE_20260517_0412.md`.
- Updated root brief, audit index, dominance report, status, rationale, and this log.

Cinematic cheats used: None. This is static architecture/token accounting.

Exact microseconds saved: Strict gate was not rerun. Summary-only H-Phi scan took 170,338 ms and produced a valid artifact.

Key numbers:

- Runtime H-Phi risk: 0.004858813.
- Runtime H-Phi narrow: 0.070286230.
- Data sovereignty: 0.132223543.
- Delta vs 02:17: +0.000011790 risk, +0.000227837 narrow, +4 DataVault refs, -20 owner-blocked NativeArray refs.
- Regressions in same interval: +2 ManagedFormatSurface, +2 PrimaryManagedRuntimeRisk.
- Token window between H-Phi artifacts: 418,677,551 tokens.
- Cache-aware cost between H-Phi artifacts: USD 326.77.
- Marginal efficiency: 35,511,242,663 tokens per +0.001 Runtime H-Phi risk.
- SQLite current tokens: 50,526,148,304.
- Meaningful LOC: 838,223.
- Tokens per meaningful LOC: 60,277.69.

STATUS: AUDIT COMPLETE.

## 2026-05-17 Token Live Rebase 04:46

What was wrong: The 04:12 H-Phi report was current for architecture score, but token burn continued after it. A quiet 04:38 SQLite pulse alone would understate the later 04:41 burst.

What was done:

- Kept 04:12 as the current H-Phi boundary instead of rerunning a 170-second static scan immediately.
- Parsed a bounded JSONL window from 04:11:59 to 04:41:52.884.
- Queried current SQLite total at 04:45:54.
- Re-scanned `Assets/_Project/Scripts/**/*.cs` for current LOC/byte denominators.
- Ran a 20-second per-thread SQLite burner sample at 04:46.
- Updated root brief, audit index, dominance report, status, rationale, and this log.

Cinematic cheats used: None. This is static accounting.

Exact microseconds saved: Avoided a repeat H-Phi scan that previously took 170,338 ms; no Unity runtime/import/build was touched.

Key numbers:

- Post-04:12 JSONL tokens: 190,381,072.
- Post-04:12 cache-aware cost: USD 173.29.
- Post-04:12 no-cache equivalent: USD 966.82.
- Post-04:12 average rate: 106,127.83 tokens/sec; 6,367,669.72 tokens/min.
- Peak token minute: 17,679,821 at 2026-05-17T04:41+04:00.
- SQLite current tokens: 50,636,429,732.
- Estimated current cache-aware total: USD 34,443.33.
- Energy estimate: 2,531.82 MWh.
- Meaningful LOC: 839,069.
- Tokens per meaningful LOC: 60,348.35.
- 04:46 live delta: 497,906 tokens in 20 seconds; 24,895.30 tokens/sec.
- Top live burners: `Add modulo time slicer`, `AUDIO_IMPORT_RESIDENCY_GUARD`, `Add indirect flora drawing`.

STATUS: AUDIT COMPLETE.

## 2026-05-17 Live Pulse 05:34

What was wrong: The 04:46 rebase was already stale for live burn rate, and the user asked to keep counting. SQLite can update burn rate quickly but cannot expose input/cache/output split.

What was done:

- Ran a 30-second read-only SQLite pulse from 05:34:08 to 05:34:38.
- Calculated tokens/sec, tokens/min/hour/day, energy, and token/code ratios.
- Priced the pulse as a range: historical cache-aware blend, latest post-04:12 cache-aware blend, and no-cache scenario.
- Wrote `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_LIVE_PULSE_20260517_0534.md`.
- Updated root brief, audit index, token ledger, dominance report, status, rationale, and this log.

Cinematic cheats used: None. This is live accounting.

Exact microseconds saved: Avoided heavy H-Phi/JSONL full rescan; used a 30-second SQLite delta.

Key numbers:

- Current SQLite tokens: 50,953,580,001.
- 30-second delta: 1,648,101 tokens.
- Live rate: 54,919.99 tokens/sec; 3,295,199.27 tokens/min.
- Day equivalent: 4,745,086,950.04 tokens/day.
- Cache-aware rate range: USD 2.52-3.00/min; USD 151.43-179.96/hour.
- No-cache scenario: USD 16.73/min; USD 1,004.05/hour.
- Current energy estimate: 2,547.68 MWh.
- Tokens per meaningful LOC: 60,726.33.
- Active delta threads: 5.
- Top live burners: `Enforce DataVault statelessness`, `CONTENT_AUTHORITY_DICTATOR`, `Move reports to batch006`, `Build ballast PID`, `Improve bot memory and CRM`.

STATUS: AUDIT COMPLETE.
