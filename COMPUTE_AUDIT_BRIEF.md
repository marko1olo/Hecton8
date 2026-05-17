# COMPUTE AUDIT BRIEF

Status: AUDIT COMPLETE
Snapshot: 2026-05-16T03:56+04:00
Scope: HECTON-8 only. Timaert ignored.
Evidence: local filesystem, `.codex` SQLite, `.codex` JSONL, static LOC scanner, official OpenAI pricing page.
Invoice status: NOT AN INVOICE. This is local telemetry accounting.

## Current Hard Numbers

| Metric | Value |
|---|---:|
| `Assets/_Project/Scripts/**/*.cs` files | 1,538 |
| Script physical LOC | 985,864 |
| Script meaningful LOC | 809,871 |
| Logic density | 82.15% |
| All `Assets/**/*.cs` physical LOC | 1,543,550 |
| `Packages/**/*.cs` physical LOC | 140,868 |
| `.codex` JSONL files | 809 |
| `.codex` JSONL bytes | 8,493,635,444 |
| JSONL files with final usage | 791 |
| JSONL final total tokens | 47,456,271,437 |
| JSONL input tokens | 47,294,226,243 |
| JSONL cached input tokens | 45,410,520,576 |
| JSONL output tokens | 161,786,794 |
| JSONL reasoning output tokens | 55,602,954 |
| Cached-input ratio | 96.017% |
| SQLite thread tokens, 03:56 local | 47,465,726,066 |
| JSONL vs SQLite drift | ~9.45M tokens live tail |
| Model-aware cache-aware estimate | USD 32,007.67 |
| Model-aware no-cache equivalent | USD 210,561.57 |
| Cache avoided | USD 178,553.90 |
| Long-context surcharge scenario | USD 32,015.49 |
| Last 24h tokens | 3,240,421,310 |
| Last 24h cache-aware cost | USD 2,525.79 |
| Last 24h no-cache equivalent | USD 16,500.45 |
| Last 24h average | 37,504.88 tokens/sec |
| Tokens per meaningful LOC | 58,597.32 |
| Historical burn per script byte | 1,100.57 tokens/byte |
| Script text proxy tokens | ~10.78M tokens at bytes/4 |
| Energy at 0.05 kWh / 1K tokens | 2,372.81 MWh |
| Energy in common units | 2.373 GWh; 2,372,814 kWh; 79,094 home-days at 30 kWh/day |

## Current Verdict

The old "1.63M LOC" claim is still not meaningful first-party logic. Current first-party script surface is 809,871 meaningful LOC. The broader all-Assets C# physical count is 1.544M LOC and includes non-first-party/vendor surface.

The economic anomaly is not raw output volume. It is repeated long-context recursion: 47.456B local ledger tokens against 809,871 meaningful script LOC, or 58.6K tokens burned per meaningful line.

Cache is carrying the bill. At current model-aware public-price assumptions, cache reduces the local estimate from USD 210.56K to USD 32.01K. That is an 84.8% avoided-cost effect. It is still not clean engineering economics.

## Canonical Files

- Detailed 2026-05-16 ledger: `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_TOKEN_BURN_RATE_LEDGER.md`
- Live delta: `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_LIVE_DELTA_20260516.md`
- Recent 2026-05-17 JSONL rate audit: `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_RECENT_JSONL_RATE_AUDIT_20260517.md`
- Post-04:12 token live rebase: `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_TOKEN_LIVE_REBASE_20260517_0446.md`
- 05:34 SQLite live pulse: `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_LIVE_PULSE_20260517_0534.md`
- Log DB audit: `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_LOG_DB_AUDIT.md`
- Index: `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_AUDIT_INDEX.md`
- Historical long report with addendum: `Docs/Reports/COMPUTE_DOMINANCE_REPORT.md`

## Live Tail After Snapshot

SQLite live sample at 2026-05-16T05:18-05:19+04:00:

| Metric | Value |
|---|---:|
| 30-second delta | 2,189,017 tokens |
| Live rate | 72,622.81 tokens/sec |
| Live cache-aware rate | USD 3.34/min; USD 200.24/hour; USD 4,805.68/day |
| Active threads | 10, all `gpt-5.5` |
| Delta since 03:56 full snapshot | +339,069,286 tokens; USD 259.69 cache-aware |

## Log DB Tail

`C:\Users\danat\.codex\logs_2.sqlite` is operational telemetry, not billing. Current metadata:

| Metric | Value |
|---|---:|
| `logs_2.sqlite` file size | 3,569,434,624 bytes |
| WAL size | 406,367,992 bytes |
| Rows in `logs` | 486,917 |
| `sum(estimated_bytes)` | 2,970,778,869 |
| Latest 5,000-row sample window | 2026-05-16T06:01:18+04:00 to 06:04:21+04:00 |
| Latest sample ERROR rows | 8 |

## Continuation Pulse 2026-05-16T14:57+04:00

Source: `C:\Users\danat\.codex\state_5.sqlite` live tail plus current `Assets/_Project/Scripts/**/*.cs` LOC scan.

| Metric | Value |
|---|---:|
| Current SQLite thread tokens | 48,761,315,725 |
| Delta vs 03:56 SQLite snapshot | +1,295,589,659 tokens |
| Delta vs 05:19 live sample end | +956,520,373 tokens |
| 60-second sample delta | 5,591,521 tokens |
| 60-second sample rate | 93,192.02 tokens/sec |
| 60-second sample rate | 5,591,521 tokens/min |
| 60-second day-equivalent | 8,051,790,240 tokens/day |
| Cache-aware 60-second cost | USD 4.28 |
| Cache-aware rate | USD 4.28/min; USD 256.95/hour; USD 6,166.81/day |
| No-cache rate | USD 28.40/min; USD 1,704.18/hour; USD 40,900.39/day |
| Estimated current cache-aware total | USD 33,007.19 |
| Estimated current no-cache total | USD 217,190.76 |
| Current first-party files | 1,561 |
| Current physical script LOC | 1,006,323 |
| Current meaningful script LOC | 827,838 |
| Current logic density | 82.26% |
| Current tokens per meaningful LOC | 58,902.00 |
| Current burn per script byte | 1,117.06 tokens/byte |
| Current energy estimate | 2,438.07 MWh |

This pulse is SQLite-only for the post-03:56 delta. It inherits the latest full JSONL blended `gpt-5.5` cache-aware/no-cache rates. It is not invoice-grade.

## Last 6H JSONL Check

Window: 2026-05-16T09:55:54+04:00 to 2026-05-16T15:55:54+04:00. Source: recent `.codex\sessions` JSONL token deltas, not SQLite.

| Metric | Value |
|---|---:|
| Last 6h total tokens | 757,394,868 |
| Cached-input ratio | 95.599% |
| Tokens/sec | 35,064.58 |
| Tokens/min | 2,103,874.63 |
| Tokens/hour | 126,232,478.00 |
| Day equivalent | 3,029,579,472 tokens/day |
| Cache-aware 6h cost | USD 607.01 |
| No-cache 6h equivalent | USD 3,853.78 |
| Cache-aware average | USD 1.69/min; USD 101.17/hour; USD 2,428.03/day |
| Peak minute | 15,133,220 tokens at 2026-05-16T10:13+04:00 |

Prompt cadence nearby: 146 explicit `event_msg.user_message` rows over six hours; peak minute 15 rows at 2026-05-16T14:24+04:00. Detailed file: `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_LAST6H_PROMPT_TOKEN_AUDIT.md`.

## H-Phi Continuation

Current H-Phi source scan at 2026-05-16T17:18:57+04:00:

| Metric | Value |
|---|---:|
| Runtime H-Phi risk | 0.004164939 |
| Runtime H-Phi narrow | 0.060806118 |
| Data sovereignty | 0.114950891 |
| Memory alignment | 0.528974740 |
| DataVault refs | 948 |
| NativeArray refs | 7,299 |
| Owner-blocked NativeArray refs | 5,266 |

Versus 2026-05-15T22:46:22+04:00 baseline:

| Metric | Delta |
|---|---:|
| Runtime H-Phi risk | +0.003528848; 6.548x |
| Runtime H-Phi narrow | +0.050018679; 5.637x |
| Data sovereignty | +0.093644859; 5.395x |
| Token spend between H-Phi artifacts | 2,464,254,349 |
| Cache-aware cost between artifacts | USD 1,947.70 |
| No-cache equivalent | USD 12,533.41 |

Correlation across 76 valid H-Phi artifacts: tokens vs Runtime H-Phi risk `r=0.522`; tokens vs Runtime H-Phi narrow `r=0.493`; tokens vs Data sovereignty `r=0.492`. This proves local association, not causality.

Latest SQLite pulse at 2026-05-16T23:14+04:00: current total `49,767,593,348` tokens; 30-second burn `3,815,200`; rate `127,173.33 tokens/sec`; blended cache-aware rate `USD 5.84/min`.

Detailed file: `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_TOKEN_CORRELATION_20260516.md`.

## Midnight Continuation 2026-05-17T00:00+04:00

SQLite live sample: 2026-05-16T23:58:34+04:00 to 2026-05-16T23:59:20+04:00.

| Metric | Value |
|---|---:|
| Current SQLite thread tokens | 49,903,844,533 |
| Delta vs 23:14 sample | +136,251,185 tokens |
| 45-second sample delta | 4,829,772 tokens |
| 45-second rate | 107,328.27 tokens/sec |
| Tokens/min | 6,439,696 |
| Tokens/day equivalent | 9,273,162,240 |
| Cache-aware rate, blended | USD 4.93/min; USD 295.93/hour; USD 7,102.25/day |
| Estimated current cache-aware total | USD 33,882.25 |
| Current energy estimate | 2,495.19 MWh |
| Current first-party files | 1,580 |
| Current physical script LOC | 1,015,982 |
| Current meaningful script LOC | 836,249 |
| Current tokens per meaningful LOC | 59,675.82 |
| Current burn per script byte | 1,132.70 tokens/byte |

Strict H-Phi budget attempt with old baseline gates timed out after 244 seconds and produced no completed gate artifact. Current scan still proves score improvement, but strict old absolute budgets remain inferred red for several counters. Detailed file: `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_BUDGET_GATE_ATTEMPT_20260517.md`.

## Recent Rate Audit 2026-05-17T00:52+04:00

Bounded JSONL pass: 81 session files modified in the last 30 hours, 991,426,469 bytes, 85,425 usable `last_token_usage` rows, 0 parse errors. Timestamp windows below are measured against latest usage event `2026-05-17T00:50:59.379+04:00`.

| Metric | Value |
|---|---:|
| Last 1h tokens | 390,025,115 |
| Last 1h cache-aware cost | USD 283.72 |
| Last 1h rate | 108,340.31 tokens/sec; USD 4.73/min |
| Last 6h tokens | 1,088,865,736 |
| Last 6h cache-aware cost | USD 827.11 |
| Last 24h tokens | 5,364,091,619 |
| Last 24h cache-aware cost | USD 4,123.40 |
| Last 24h no-cache equivalent | USD 27,223.44 |
| Last 24h cache ratio | 96.211% |
| Last 24h cache avoided | USD 23,100.04 |
| Peak token second | 2,780,390 at 2026-05-16T16:54:28+04:00 |
| Peak token minute | 25,820,127 at 2026-05-16T05:44+04:00 |
| Peak prompt minute | 21 user-message rows at 2026-05-16T09:10+04:00 |
| Long-context surcharge events over 272K input | 0 in the bounded pass |

SQLite live sample at 2026-05-17T00:51:36-00:52:06+04:00:

| Metric | Value |
|---|---:|
| Current SQLite tokens | 50,027,664,742 |
| 30-second delta | 2,659,344 |
| Live rate | 88,644.80 tokens/sec |
| Live rate | 5,318,688 tokens/min |
| Live cache-aware rate | USD 4.07/min; USD 244.41/hour; USD 5,865.91/day |
| Estimated current cache-aware total | USD 33,977.08 |
| Current energy estimate | 2,501.38 MWh |
| Current meaningful LOC | 836,910 |
| Tokens per meaningful LOC | 59,776.64 |
| Tokens per script byte | 1,121.40 |

Detailed file: `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_RECENT_JSONL_RATE_AUDIT_20260517.md`.

## Active Thread Burners 2026-05-17T01:39+04:00

20-second SQLite per-thread delta:

| Metric | Value |
|---|---:|
| Active delta threads | 6 |
| Total delta | 828,509 tokens |
| Live rate | 41,425.45 tokens/sec |
| Live rate | 2,485,527 tokens/min |
| Cache-aware rate | USD 1.90/min; USD 114.22/hour; USD 2,741.25/day |

Top deltas: `Build hull repair engine` 196,707; `Standardize SignalBus lanes` 177,518; `Build STP dynamic resolution adapter` 172,571; `Build ballast PID` 139,980; `CORE_TICK_DILATION` 112,168; `Add sensory input to boid shader` 29,565.

## H-Phi Rebase 2026-05-17T02:17+04:00

Current artifact: `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_CURRENT_20260517_021429.json`.

| Metric | Value |
|---|---:|
| Runtime H-Phi risk | 0.004847023 |
| Runtime H-Phi narrow | 0.070058393 |
| Data sovereignty | 0.131794933 |
| Memory alignment | 0.531571219 |
| DataVault refs | 1,108 |
| Owner-blocked NativeArray refs | 5,143 |
| Managed format surface | 541 |
| Primary managed runtime risk | 155 |

Delta vs 2026-05-16T17:18+04:00: Runtime H-Phi risk `+0.000682084`, Runtime H-Phi narrow `+0.009252275`, Data sovereignty `+0.016844042`, owner-blocked NativeArray refs `-123`, DataVault refs `+160`.

Token window between H-Phi artifacts: `2,183,475,652` tokens, `USD 1,640.89` cache-aware, `USD 11,075.08` no-cache, `96.554%` cached input. Marginal efficiency: `3,201,182,922` tokens per `+0.001` Runtime H-Phi risk. Old absolute budgets are still not clean.

SQLite live sample at 2026-05-17T02:14-02:15+04:00: `50,313,194,499` current tokens, 30-second burn `1,442,468`, rate `48,082.27 tokens/sec`, estimated current cache-aware total `USD 34,195.77`, energy `2,515.66 MWh`, meaningful LOC `837,628`, tokens per meaningful LOC `60,066.28`.

Detailed file: `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_LIVE_REBASE_20260517_0217.md`.

03:04 burn spike: 20 active delta threads, `3,079,626` tokens in 20 seconds, `153,981.30 tokens/sec`, `9,238,878 tokens/min`, blended cache-aware rate `USD 7.08/min` / `USD 10,189.43/day`. SQLite total at 03:15:49: `50,453,850,790` tokens; estimated cache-aware total `USD 34,303.50`; energy `2,522.69 MWh`; tokens per meaningful LOC `60,234.20`.

## H-Phi Rebase 2026-05-17T04:12+04:00

Current artifact: `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_CURRENT_20260517_040910.json`.

| Metric | Value |
|---|---:|
| Runtime H-Phi risk | 0.004858813 |
| Runtime H-Phi narrow | 0.070286230 |
| Data sovereignty | 0.132223543 |
| Memory alignment | 0.531571219 |
| DataVault refs | 1,112 |
| Owner-blocked NativeArray refs | 5,123 |
| Managed format surface | 543 |
| Primary managed runtime risk | 157 |

Delta vs 02:17: `+0.000011790` Runtime H-Phi risk, `+0.000227837` narrow, `+4` DataVault refs, `-20` owner-blocked NativeArray refs, `+2` PrimaryManagedRuntimeRisk.

Token window 02:17-04:12: `418,677,551` tokens, `USD 326.77` cache-aware, `USD 2,122.89` no-cache. Marginal efficiency collapsed to `35,511,242,663` tokens per `+0.001` Runtime H-Phi risk.

SQLite live sample 04:09: `50,526,148,304` tokens; 30-second burn `1,720,961`; rate `57,365.37 tokens/sec`; estimated cache-aware total `USD 34,358.87`; energy `2,526.31 MWh`; meaningful LOC `838,223`; tokens per meaningful LOC `60,277.69`.

Detailed file: `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_LIVE_REBASE_20260517_0412.md`.

## Token Live Rebase 2026-05-17T04:46+04:00

No new H-Phi scan was run in this pass. The latest H-Phi artifact was only about 25 minutes old and already cost 170,338 ms of static scan time. This section measures token movement after that artifact.

Post-04:12 JSONL window: 2026-05-17T04:11:59+04:00 to 2026-05-17T04:41:52.884+04:00.

| Metric | Value |
|---|---:|
| JSONL files scanned | 45 |
| JSONL bytes scanned | 525,293,697 |
| Usable usage rows | 1,212 |
| Parse errors | 0 |
| Total tokens | 190,381,072 |
| Cached-input ratio | 93.009% |
| Cache-aware cost | USD 173.29 |
| No-cache equivalent | USD 966.82 |
| Average rate | 106,127.83 tokens/sec; 6,367,669.72 tokens/min |
| Cache-aware rate | USD 5.80/min; USD 347.75/hour |
| Peak token second | 1,171,462 at 2026-05-17T04:41:08+04:00 |
| Peak token minute | 17,679,821 at 2026-05-17T04:41+04:00 |

SQLite summary at 2026-05-17T04:45:54+04:00:

| Metric | Value |
|---|---:|
| Current SQLite tokens | 50,636,429,732 |
| Delta since 04:09 SQLite total | +110,281,428 |
| Estimated current cache-aware total | USD 34,443.33 |
| Current energy estimate | 2,531.82 MWh |
| Current meaningful LOC | 839,069 |
| Tokens per meaningful LOC | 60,348.35 |
| Tokens per script byte | 1,131.64 |

04:46 per-thread burner pulse: 497,906 tokens in 20 seconds; 24,895.30 tokens/sec; 1,493,718 tokens/min; blended cache-aware rate USD 1.14/min. Top live burners: `Add modulo time slicer`, `AUDIO_IMPORT_RESIDENCY_GUARD`, `Add indirect flora drawing`.

Detailed file: `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_TOKEN_LIVE_REBASE_20260517_0446.md`.

## Live Pulse 2026-05-17T05:34+04:00

SQLite live sample: 2026-05-17T05:34:08+04:00 to 05:34:38+04:00.

| Metric | Value |
|---|---:|
| Current SQLite tokens | 50,953,580,001 |
| 30-second delta | 1,648,101 |
| Tokens/sec | 54,919.99 |
| Tokens/min | 3,295,199.27 |
| Tokens/day equivalent | 4,745,086,950.04 |
| Active delta threads | 5 |
| Cache-aware rate range | USD 2.52-3.00/min; USD 151.43-179.96/hour |
| No-cache scenario rate | USD 16.73/min; USD 1,004.05/hour |
| Current energy estimate | 2,547.68 MWh |
| Tokens per meaningful LOC | 60,726.33 |
| Tokens per script byte | 1,138.73 |

Top live burners: `Enforce DataVault statelessness` 460,086; `CONTENT_AUTHORITY_DICTATOR` 404,169; `Move reports to batch006` 328,033; `Build ballast PID` 284,567; `Improve bot memory and CRM` 171,246.

Detailed file: `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_LIVE_PULSE_20260517_0534.md`.

