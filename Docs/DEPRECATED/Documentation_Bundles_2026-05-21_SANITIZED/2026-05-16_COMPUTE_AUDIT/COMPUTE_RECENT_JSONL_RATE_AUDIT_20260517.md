# COMPUTE RECENT JSONL RATE AUDIT 2026-05-17

Status: AUDIT COMPLETE
Scope: HECTON-8 only. Timaert excluded.
Evidence class: bounded `.codex/sessions` JSONL usage pass + SQLite live-tail + static script LOC scan.
Generated local: 2026-05-17T00:51-00:52+04:00.

This is not a billing export. It is local telemetry priced with the current audit rate table: `gpt-5.5` input USD 5.00/M, cached input USD 0.50/M, output USD 30.00/M. Legacy/non-current model IDs remain proxy-priced in the main ledger. The official public pricing reference is `https://openai.com/api/pricing/`.

## Bounded JSONL Pass

Input set: JSONL files modified in the last 30 hours. This avoids another full 8+ GB historical read after the interrupted full pass.

| Metric | Value |
|---|---:|
| Files scanned | 81 |
| Bytes scanned | 991,426,469 |
| JSONL rows read | 397,027 |
| `event_msg.token_count` rows | 85,509 |
| Rows with usable `last_token_usage` | 85,425 |
| Parse errors | 0 |
| Earliest usage event in scanned files | 2026-05-11T20:55:04.732+04:00 |
| Latest usage event | 2026-05-17T00:50:59.379+04:00 |
| Model bucket in this fresh window | `gpt-5.5` only |
| Maximum per-event input delta | 266,037 tokens |
| Long-context surcharge events over 272K input | 0 |

The mtime-bounded file set contains older events because active session files can keep growing. Window tables below are timestamp-windowed against the latest event, not file mtime.

## Rolling Token Windows

| Window | Tokens | Tokens/sec | Tokens/min | Tokens/hour | Cache-aware USD | USD/min | USD/hour | No-cache USD |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Last 1h | 390,025,115 | 108,340.31 | 6,500,418.58 | 390,025,115.00 | 283.72 | 4.73 | 283.72 | 1,978.29 |
| Last 6h | 1,088,865,736 | 50,410.45 | 3,024,627.04 | 181,477,622.67 | 827.11 | 2.30 | 137.85 | 5,524.96 |
| Last 24h | 5,364,091,619 | 62,084.39 | 3,725,063.62 | 223,503,817.46 | 4,123.40 | 2.86 | 171.81 | 27,223.44 |

Last 24h cache ratio: 96.211% (`5,133,342,976 / 5,335,497,946` input tokens). Cache avoided about USD 23,100.04 in the 24h window alone.

## Peak Cadence

| Peak | Value |
|---|---:|
| Token peak second | 2,780,390 tokens at 2026-05-16T16:54:28+04:00 |
| Token peak minute | 25,820,127 tokens at 2026-05-16T05:44+04:00 |
| Token peak hour | 452,526,419 tokens at 2026-05-16T16:00+04:00 |
| Token peak local day in scanned set | 5,081,003,643 tokens on 2026-05-16 |
| Prompt peak minute | 21 user-message rows at 2026-05-16T09:10+04:00 |
| Prompt peak hour | 99 user-message rows at 2026-05-16T14:00+04:00 |

## SQLite Live Pulse 00:52

Source: `C:\Users\danat\.codex\state_5.sqlite`, HECTON/Hades cwd rows, 30-second sample from 2026-05-17T00:51:36+04:00 to 2026-05-17T00:52:06+04:00.

| Metric | Value |
|---|---:|
| Start SQLite tokens | 50,025,005,398 |
| End SQLite tokens | 50,027,664,742 |
| Delta | 2,659,344 tokens |
| Tokens/sec | 88,644.80 |
| Tokens/min | 5,318,688 |
| Tokens/hour | 319,121,280 |
| Tokens/day equivalent | 7,658,910,720 |
| Active threads updated in last hour | 47 |
| Live cache-aware rate, blended `gpt-5.5` | USD 4.07/min; USD 244.41/hour; USD 5,865.91/day |

SQLite model split at sample end:

| Model | Threads | Tokens |
|---|---:|---:|
| `gpt-5.5` | 535 | 38,181,043,533 |
| `gpt-5.4` | 241 | 11,553,863,916 |
| `gpt-5.4-mini` | 25 | 192,533,099 |
| `gpt-5.2-codex` | 3 | 85,512,992 |
| `gpt-5.1-codex-mini` | 3 | 13,472,930 |
| `gpt-5.3-codex` | 3 | 1,096,113 |
| `gpt-5.2` | 3 | 142,159 |

Estimated current cache-aware total from prior midnight rebase plus live delta: USD 33,977.08. This is still not invoice-grade because SQLite does not expose current input/cache/output split.

## Code Ratio Rebase

Static scan: `Assets/_Project/Scripts/**/*.cs`.

| Metric | Value |
|---|---:|
| Script files | 1,580 |
| Physical LOC | 1,016,698 |
| Blank lines | 137,324 |
| Comment lines | 42,464 |
| Meaningful LOC | 836,910 |
| Script bytes | 44,611,915 |
| Logic density | 82.3165% |
| SQLite tokens / meaningful LOC | 59,776.64 |
| SQLite tokens / physical LOC | 49,206.02 |
| SQLite tokens / script byte | 1,121.40 |
| Source text proxy tokens at bytes/4 | 11,152,979 |
| Burn / source-text proxy ratio | 4,485.59x |

Energy at the project constant `0.05 kWh / 1K tokens`: 2,501.38 MWh, or 2.501 GWh. At 30 kWh/day household usage, that is 83,379.44 household-days.

## Verdict

The last 24h was not a short burst: 5.364B JSONL usage tokens, 96.211% cached input, and USD 4.123K cache-aware under the audit price model. The latest 30-second SQLite pulse is hotter than the 24h average: 88.6K tokens/sec versus 62.1K tokens/sec.

Cache is the only reason this is not a five-figure-per-day public-price event. The engineering smell remains context recursion: about 59.8K local ledger tokens per meaningful first-party C# line.

STATUS: AUDIT COMPLETE.

## Active Thread Burners 2026-05-17T01:39+04:00

Source: 20-second SQLite per-thread delta. This identifies live burners, not value delivered. It is not a "compute thief" conviction because no per-thread LOC/H-Phi delta was joined in this pass.

| Metric | Value |
|---|---:|
| Sample window | 2026-05-17T01:39:27 to 01:39:47+04:00 |
| Active delta threads | 6 |
| Total delta | 828,509 tokens |
| Tokens/sec | 41,425.45 |
| Tokens/min | 2,485,527 |
| Cache-aware rate, blended | USD 1.90/min; USD 114.22/hour; USD 2,741.25/day |

| Rank | Thread title | Model | Delta tokens |
|---:|---|---:|---:|
| 1 | Build hull repair engine | `gpt-5.5` | 196,707 |
| 2 | Standardize SignalBus lanes | `gpt-5.5` | 177,518 |
| 3 | Build STP dynamic resolution adapter | `gpt-5.5` | 172,571 |
| 4 | Build ballast PID | `gpt-5.5` | 139,980 |
| 5 | CORE_TICK_DILATION prompt thread | `gpt-5.5` | 112,168 |
| 6 | Add sensory input to boid shader | `gpt-5.5` | 29,565 |

Interpretation: live burn cooled from the 00:52 pulse but stayed economically material. The burn is distributed across implementation agents rather than one runaway accounting thread.

STATUS: AUDIT COMPLETE.
