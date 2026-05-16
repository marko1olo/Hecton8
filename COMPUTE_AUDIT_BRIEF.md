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

