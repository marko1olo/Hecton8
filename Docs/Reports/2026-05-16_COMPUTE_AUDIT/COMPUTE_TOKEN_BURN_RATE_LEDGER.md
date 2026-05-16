# COMPUTE TOKEN BURN RATE LEDGER

Status: AUDIT COMPLETE
Snapshot: 2026-05-16T03:56+04:00
Agent: COMPUTE_LOGISTICS_AUDITOR
Source: `C:\Users\danat\.codex\sessions/**/*.jsonl`, `C:\Users\danat\.codex\state_5.sqlite`, `Assets/_Project/Scripts/**/*.cs`
Pricing reference: official OpenAI API pricing, `https://openai.com/api/pricing/`, checked during this audit.

## Boundary

This is local forensic accounting. It is not an OpenAI invoice, not a contractual billing export, and not proof that every local Codex token is billable API traffic.

Pricing buckets used:

| Model bucket | Input / 1M | Cached input / 1M | Output / 1M | Note |
|---|---:|---:|---:|---|
| `gpt-5.5` | USD 5.00 | USD 0.50 | USD 30.00 | Current standard scenario |
| `gpt-5.4` | USD 2.50 | USD 0.25 | USD 15.00 | Current standard scenario |
| `gpt-5.4-mini` | USD 0.75 | USD 0.075 | USD 4.50 | Current mini scenario |
| `gpt-5.3-codex` / `gpt-5.2-codex` / `gpt-5.2` | USD 1.75 | USD 0.175 | USD 14.00 | Proxy bucket for old/ambiguous local model IDs |
| `gpt-5.1-codex-mini` | USD 0.25 | USD 0.025 | USD 2.00 | Proxy mini bucket |

Long-context scenario: for `gpt-5.5` per-request delta input above 272K, input and cached input are doubled and output is multiplied by 1.5. Current scan found 1 such delta event, so the surcharge scenario changes little.

## Current Ledger

| Metric | Value |
|---|---:|
| JSONL session files | 809 |
| JSONL bytes | 8,493,635,444 |
| JSONL files with final usage | 791 |
| Parsed token-count rows | 386,446 |
| Token regex misses | 194 |
| Negative cumulative deltas skipped | 8 |
| Observation start UTC | 2026-04-03T17:10:34.947Z |
| Latest token timestamp UTC | 2026-05-15T23:50:49.162Z |
| JSONL final total tokens | 47,456,271,437 |
| Positive-delta token flow | 47,467,926,462 |
| SQLite `threads.tokens_used`, 03:56 local | 47,465,726,066 |
| JSONL final vs SQLite tail | SQLite +9,454,629 tokens |
| Input tokens | 47,294,226,243 |
| Cached input tokens | 45,410,520,576 |
| Non-cached input tokens | 1,883,705,667 |
| Output tokens | 161,786,794 |
| Reasoning output tokens | 55,602,954 |
| Cached-input ratio | 96.017% |
| Cache-miss ratio | 3.983% |
| Output/input ratio | 0.342% |

## Cost

| Scenario | Cache-aware cost | No-cache equivalent | Cache avoided | Avoided share |
|---|---:|---:|---:|---:|
| Model-aware local estimate | USD 32,007.67 | USD 210,561.57 | USD 178,553.90 | 84.80% |
| Model-aware with `gpt-5.5` long-context surcharge scenario | USD 32,015.49 | USD 210,595.75 | USD 178,580.26 | 84.80% |

Direct current answer: the last rolling 24h burned 3.240B tokens. Cache-aware estimated cost is USD 2,525.79. No-cache equivalent is USD 16,500.45. Cache avoided about USD 13,974.66 in that 24h window.

## Model Split

| Model | Sessions with final usage | Final tokens | Input | Cached input | Output | Cache ratio |
|---|---:|---:|---:|---:|---:|---:|
| `gpt-5.5` | 519 | 35,570,944,410 | 35,457,285,594 | 34,098,868,608 | 113,400,416 | 96.17% |
| `gpt-5.4` | 237 | 11,592,726,837 | 11,546,273,790 | 11,051,701,632 | 46,453,047 | 95.72% |
| `gpt-5.4-mini` | 24 | 192,533,099 | 191,173,213 | 167,098,752 | 1,359,886 | 87.41% |
| `gpt-5.2-codex` | 3 | 85,512,992 | 85,044,900 | 79,787,648 | 468,092 | 93.82% |
| `gpt-5.1-codex-mini` | 2 | 13,315,827 | 13,218,484 | 12,128,000 | 97,343 | 91.75% |
| `gpt-5.3-codex` | 3 | 1,096,113 | 1,088,533 | 879,744 | 7,580 | 80.82% |
| `gpt-5.2` | 3 | 142,159 | 141,729 | 56,192 | 430 | 39.65% |

## Burn Rate Windows

| Window | Tokens | Tokens/sec | Tokens/min | Tokens/hour | Tokens/day equiv | Cache-aware cost | USD/min | USD/hour | USD/day equiv | No-cache cost |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Last 1h | 193,709,191 | 53,808.11 | 3,228,486.52 | 193,709,191.00 | 4,649,020,584 | USD 144.30 | USD 2.40 | USD 144.30 | USD 3,463.13 | USD 985.46 |
| Last 6h | 844,680,537 | 39,105.58 | 2,346,334.82 | 140,780,089.50 | 3,378,722,148 | USD 667.69 | USD 1.85 | USD 111.28 | USD 2,670.76 | USD 4,302.47 |
| Last 24h | 3,240,421,310 | 37,504.88 | 2,250,292.58 | 135,017,554.58 | 3,240,421,310 | USD 2,525.79 | USD 1.75 | USD 105.24 | USD 2,525.79 | USD 16,500.45 |
| Last 7d | 18,963,121,006 | 31,354.37 | 1,881,262.00 | 112,875,720.27 | 2,709,017,286.57 | USD 14,642.67 | USD 1.45 | USD 87.16 | USD 2,091.81 | USD 96,425.35 |
| Last 14d | 30,671,970,415 | 25,357.12 | 1,521,427.10 | 91,285,626.24 | 2,190,855,029.64 | USD 23,627.72 | USD 1.17 | USD 70.32 | USD 1,687.69 | USD 155,845.79 |
| Last 30d | 44,013,354,604 | 16,980.46 | 1,018,827.65 | 61,129,659.17 | 1,467,111,820.13 | USD 30,658.23 | USD 0.71 | USD 42.58 | USD 1,021.94 | USD 202,213.24 |

## Recent UTC Days

| UTC day | Tokens | Cache-aware cost | No-cache cost | Dominant model |
|---|---:|---:|---:|---|
| 2026-05-08 | 3,187,635,852 | USD 2,451.01 | USD 16,208.05 | `gpt-5.5` |
| 2026-05-09 | 2,353,206,029 | USD 1,809.15 | USD 11,962.95 | `gpt-5.5` |
| 2026-05-10 | 1,692,852,292 | USD 1,377.46 | USD 8,612.38 | `gpt-5.5` |
| 2026-05-11 | 2,610,546,071 | USD 2,042.10 | USD 13,274.09 | `gpt-5.5` |
| 2026-05-12 | 2,415,939,777 | USD 1,873.95 | USD 12,278.28 | `gpt-5.5` |
| 2026-05-13 | 3,951,756,366 | USD 2,889.92 | USD 20,055.72 | `gpt-5.5` |
| 2026-05-14 | 2,693,098,678 | USD 2,122.10 | USD 13,714.88 | `gpt-5.5` |
| 2026-05-15 | 3,200,050,653 | USD 2,495.83 | USD 16,295.03 | `gpt-5.5` |

Calendar-day buckets are UTC and can be partial at edges.

## Prompt Cadence

This uses JSONL `role:user` rows. Transcript replay and imported prompt rows can inflate the count, so this is cadence evidence, not a clean human keystroke counter.

| Metric | Value |
|---|---:|
| User-role rows | 15,215 |
| Peak user rows/sec | 36 at 2026-04-13T14:59:32Z |
| Peak user rows/min | 136 at 2026-04-13T14:59Z |
| Peak user rows/hour | 494 at 2026-04-13T14Z |
| Peak user rows/day | 1,132 on 2026-05-08 UTC |
| Last 6h user rows | 318 |
| Last 24h user rows | 940 |

## Code Ratios

| Metric | Value |
|---|---:|
| `Assets/_Project/Scripts/**/*.cs` files | 1,538 |
| Physical script LOC | 985,864 |
| Meaningful script LOC | 809,871 |
| Script source bytes | 43,119,776 |
| Script text proxy tokens at bytes/4 | ~10,779,944 |
| Tokens per meaningful LOC | 58,597.32 |
| Tokens per physical LOC | 48,136.73 |
| Historical burn per script byte | 1,100.57 tokens/byte |
| Historical burn per KiB of script source | 1,126,982 tokens/KiB |
| Source text proxy tokens per byte | ~0.25 tokens/byte |
| Model-aware cost per meaningful LOC | USD 0.03952 |
| Context amplification vs 50-token/LOC heuristic | 1,171.95x |

The "tokens per byte" number is intentionally split. The code text itself is roughly 0.25 tokenizer tokens per byte under a bytes/4 proxy. The historical burn is 1,100.57 tokens per current script byte because the same context was re-read, cached, summarized, patched, and re-validated many times.

## LOC And Domain Weight

| Domain | Files | Physical LOC | Meaningful LOC |
|---|---:|---:|---:|
| `(root)` | 337 | 306,253 | 251,259 |
| `World` | 177 | 135,530 | 112,723 |
| `Editor` | 210 | 88,037 | 76,053 |
| `Gameplay` | 139 | 83,449 | 65,974 |
| `UI` | 107 | 71,650 | 58,837 |
| `Core` | 123 | 66,097 | 51,183 |

Heaviest named domain: `World` at 112,723 meaningful LOC. The physical root bucket is larger, which is a filing/architecture smell, not a domain.

Boilerplate ratio:

| Bucket | Files | Physical LOC | Meaningful LOC | Share of meaningful LOC |
|---|---:|---:|---:|---:|
| Contracts | 57 | 10,776 | 6,937 | 0.86% |
| Implementation | 1,481 | 975,088 | 802,934 | 99.14% |

Implementation outweighs contract surface by ~115.75x meaningful LOC.

## Docs Token Proxy

| Folder | Files | Bytes | Estimated tokens, chars/4 |
|---|---:|---:|---:|
| `Docs/AgentLogs` | 137 | 36,668,556 | 9,167,120 |
| `Docs/Tasks` | 47 | 493,350 | 123,281 |
| `Docs/Reports` | 140 | 4,135,904 | 1,032,517 |

Agent Markdown is not the cost center. The `.codex` JSONL ledger is ~47.456B tokens; current agent/task/report docs are only ~10.323M estimated text tokens by chars/4.

## Energy Conversion

Formula: `tokens / 1000 * 0.05 kWh`.

| Unit | Value |
|---|---:|
| kWh | 2,372,813.57 |
| MWh | 2,372.81 |
| GWh | 2.3728 |
| 30 kWh/day household | 79,094 home-days |
| 30 kWh/day household years | 216.7 home-years |
| 75 kWh EV full charges | 31,637.5 |
| 1 MW continuous load | 98.87 days |
| Electricity at USD 0.10/kWh | USD 237,281.36 |

The earlier 2,297.33 MWh number was correct for an older token total. Current token total moves the estimate to 2,372.81 MWh.

## Verdict

The burn increased from the 2026-05-15 brief's 45.771B final tokens to 47.456B final tokens. Delta: +1.685B final JSONL tokens.

Last rolling 24h: 3.240B tokens, USD 2.526K cache-aware, USD 16.500K no-cache equivalent.

The root cause is not "too many lines of code." The root cause is long-context agent concurrency with very high cache reuse. Cache hides most of the sticker price, but the work pattern still burns billions of tokens per day.

STATUS: AUDIT COMPLETE.

