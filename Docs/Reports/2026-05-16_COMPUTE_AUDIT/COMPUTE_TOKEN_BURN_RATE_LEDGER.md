# COMPUTE TOKEN BURN RATE LEDGER

Status: AUDIT COMPLETE
Snapshot: 2026-05-16T03:56+04:00
Agent: COMPUTE_LOGISTICS_AUDITOR
Source: `C:\Users\danat\.codex\sessions/**/*.jsonl`, `C:\Users\danat\.codex\state_5.sqlite`, `Assets/_Project/Scripts/**/*.cs`
Pricing reference: official OpenAI API pricing, `https://openai.com/api/pricing/`, checked during this audit.
Search keywords: H-Phi; HPhi; hphi; ash-fi; ash_phi; ASh-Fi; HФ; Аш-Фи; integration-metric; architecture-integration; token-H-Phi-ROI; compute-H-Phi.

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

## Continuation Rebase - 2026-05-16T14:57+04:00

This section does not replace the full 03:56 JSONL scan. It rebases the live total with SQLite and recalculates code ratios after the source tree moved.

| Metric | 03:56 full scan | 14:57 live rebase | Delta |
|---|---:|---:|---:|
| Token total used for current estimate | 47,456,271,437 | 48,761,315,725 | +1,305,044,288 |
| First-party script files | 1,538 | 1,561 | +23 |
| Physical script LOC | 985,864 | 1,006,323 | +20,459 |
| Meaningful script LOC | 809,871 | 827,838 | +17,967 |
| Script bytes | 43,119,776 | 43,651,495 | +531,719 |
| Logic density | 82.15% | 82.26% | +0.11 pp |
| Tokens per meaningful LOC | 58,597.32 | 58,902.00 | +304.68 |
| Burn tokens per script byte | 1,100.57 | 1,117.06 | +16.49 |
| Cache-aware estimate | USD 32,007.67 | USD 33,007.19 | +USD 999.52 |
| No-cache equivalent | USD 210,561.57 | USD 217,190.76 | +USD 6,629.19 |
| Energy | 2,372.81 MWh | 2,438.07 MWh | +65.25 MWh |

Current code distribution:

| Domain | Files | Physical LOC | Meaningful LOC |
|---|---:|---:|---:|
| `(root)` | 337 | 307,870 | 252,679 |
| `World` | 177 | 136,020 | 113,155 |
| `Editor` | 210 | 88,202 | 76,192 |
| `Gameplay` | 139 | 83,870 | 66,366 |
| `UI` | 108 | 72,068 | 59,157 |
| `Core` | 143 | 74,353 | 58,353 |
| `Construction` | 37 | 26,172 | 21,898 |
| `Audio` | 27 | 22,311 | 18,950 |
| `Fauna` | 22 | 22,026 | 18,887 |
| `Visor` | 34 | 19,657 | 16,494 |

Current boilerplate split:

| Bucket | Files | Physical LOC | Meaningful LOC | Share of meaningful LOC |
|---|---:|---:|---:|---:|
| Contracts/interfaces/protocol-like files | 117 | 30,376 | 22,037 | 2.66% |
| Implementation | 1,444 | 975,947 | 805,801 | 97.34% |

The contract ratio changed because the current scanner classifies `I*.cs`, `*Interface*`, and `*Contract*` names as contract-like. That is broader than the 03:56 pass and should not be treated as a perfect semantic boundary.

STATUS: AUDIT COMPLETE.

## Live Rebase - 2026-05-16T23:14+04:00

This is a SQLite live rebase, not a new full JSONL rescan.

| Metric | Value |
|---|---:|
| Current SQLite thread tokens | 49,767,593,348 |
| Delta vs 14:57 SQLite rebase | +1,006,277,623 |
| 30-second live delta | 3,815,200 |
| Live rate | 127,173.33 tokens/sec |
| Live minute equivalent | 7,630,400 tokens/min |
| Live day equivalent | 10,987,776,000 tokens/day |
| Estimated current cache-aware total | USD 33,777.90 |
| Delta cost since 14:57, cache-aware blended | USD 770.70 |
| Current energy estimate | 2,488.38 MWh |

The 23:14 pulse is hotter than both previous same-day continuation samples. It should be treated as burst evidence, not a stable daily forecast.

STATUS: AUDIT COMPLETE.

## Midnight Live Rebase - 2026-05-17T00:00+04:00

This is a SQLite live rebase and current source LOC scan, not a new full JSONL rescan.

| Metric | Value |
|---|---:|
| Current SQLite thread tokens | 49,903,844,533 |
| Delta vs 23:14 SQLite sample | +136,251,185 |
| Delta vs 14:57 SQLite rebase | +1,142,528,808 |
| 45-second live delta | 4,829,772 |
| Live rate | 107,328.27 tokens/sec |
| Live minute equivalent | 6,439,696 tokens/min |
| Live day equivalent | 9,273,162,240 tokens/day |
| Estimated current cache-aware total | USD 33,882.25 |
| Delta cost since 23:14, cache-aware blended | USD 104.35 |
| Delta cost since 14:57, cache-aware blended | USD 875.05 |
| Current energy estimate | 2,495.19 MWh |

Current source ratios:

| Metric | Value |
|---|---:|
| First-party script files | 1,580 |
| Physical script LOC | 1,015,982 |
| Meaningful script LOC | 836,249 |
| Script bytes | 44,057,472 |
| Logic density | 82.31% |
| Tokens per meaningful LOC | 59,675.82 |
| Tokens per physical LOC | 49,118.83 |
| Burn tokens per script byte | 1,132.70 |

STATUS: AUDIT COMPLETE.

## Recent JSONL Rate Audit - 2026-05-17T00:52+04:00

This is a bounded recent-file JSONL pass plus a 30-second SQLite live pulse. It supersedes short-pulse-only estimates for 1h/6h/24h cadence, but it is still not a billing export.

| Metric | Value |
|---|---:|
| JSONL files scanned | 81 |
| JSONL bytes scanned | 991,426,469 |
| Usable `last_token_usage` rows | 85,425 |
| Parse errors | 0 |
| Fresh model bucket | `gpt-5.5` |
| Max per-event input delta | 266,037 |
| Long-context surcharge events over 272K input | 0 |

Rolling windows:

| Window | Tokens | Tokens/sec | Cache-aware USD | USD/min | No-cache USD |
|---|---:|---:|---:|---:|---:|
| Last 1h | 390,025,115 | 108,340.31 | 283.72 | 4.73 | 1,978.29 |
| Last 6h | 1,088,865,736 | 50,410.45 | 827.11 | 2.30 | 5,524.96 |
| Last 24h | 5,364,091,619 | 62,084.39 | 4,123.40 | 2.86 | 27,223.44 |

Last 24h cache ratio: 96.211%. Last 24h cache avoided: USD 23,100.04.

Peak cadence:

| Peak | Value |
|---|---:|
| Token peak second | 2,780,390 at 2026-05-16T16:54:28+04:00 |
| Token peak minute | 25,820,127 at 2026-05-16T05:44+04:00 |
| Token peak hour | 452,526,419 at 2026-05-16T16:00+04:00 |
| Prompt peak minute | 21 user-message rows at 2026-05-16T09:10+04:00 |
| Prompt peak hour | 99 user-message rows at 2026-05-16T14:00+04:00 |

SQLite 30-second live pulse:

| Metric | Value |
|---|---:|
| Current SQLite tokens | 50,027,664,742 |
| 30-second delta | 2,659,344 |
| Tokens/sec | 88,644.80 |
| Tokens/min | 5,318,688 |
| Tokens/hour | 319,121,280 |
| Tokens/day equivalent | 7,658,910,720 |
| Live cache-aware rate | USD 4.07/min; USD 244.41/hour; USD 5,865.91/day |
| Estimated current cache-aware total | USD 33,977.08 |
| Current energy estimate | 2,501.38 MWh |

Current source ratios:

| Metric | Value |
|---|---:|
| First-party script files | 1,580 |
| Physical script LOC | 1,016,698 |
| Meaningful script LOC | 836,910 |
| Script bytes | 44,611,915 |
| Logic density | 82.3165% |
| Tokens per meaningful LOC | 59,776.64 |
| Tokens per physical LOC | 49,206.02 |
| Burn tokens per script byte | 1,121.40 |
| Burn / source-text proxy ratio | 4,485.59x |

STATUS: AUDIT COMPLETE.

## Token Live Rebase - 2026-05-17T04:46+04:00

This is a post-H-Phi token rebase. It does not replace the 04:12 H-Phi score artifact.

Post-04:12 JSONL window:

| Metric | Value |
|---|---:|
| Window | 2026-05-17T04:11:59+04:00 to 2026-05-17T04:41:52.884+04:00 |
| JSONL files scanned | 45 |
| JSONL bytes scanned | 525,293,697 |
| Usable usage rows | 1,212 |
| Parse errors | 0 |
| Total tokens | 190,381,072 |
| Cached-input ratio | 93.009% |
| Cache-aware cost | USD 173.29 |
| No-cache equivalent | USD 966.82 |
| Average rate | 106,127.83 tokens/sec |
| Average minute | 6,367,669.72 tokens/min |
| Cache-aware rate | USD 5.80/min; USD 347.75/hour |
| Peak token minute | 17,679,821 at 2026-05-17T04:41+04:00 |

SQLite/code rebase:

| Metric | Value |
|---|---:|
| Current SQLite tokens | 50,636,429,732 |
| Delta since 04:09 SQLite total | +110,281,428 |
| Estimated current cache-aware total | USD 34,443.33 |
| Current energy estimate | 2,531.82 MWh |
| First-party script files | 1,581 |
| Physical script LOC | 1,019,121 |
| Meaningful script LOC | 839,069 |
| Script bytes | 44,746,126 |
| Logic density | 82.3326% |
| Tokens per meaningful LOC | 60,348.35 |
| Tokens per physical LOC | 49,686.38 |
| Burn tokens per script byte | 1,131.64 |

04:46 live burner sample:

| Metric | Value |
|---|---:|
| Sample duration | 20 sec |
| Active delta threads | 3 |
| Total delta | 497,906 |
| Tokens/sec | 24,895.30 |
| Tokens/min | 1,493,718 |
| Tokens/day equivalent | 2,150,953,920 |
| Cache-aware rate, blended | USD 1.14/min; USD 68.64/hour; USD 1,647.40/day |

Top burners: `Add modulo time slicer` 193,366; `AUDIO_IMPORT_RESIDENCY_GUARD` 169,754; `Add indirect flora drawing` 134,786.

STATUS: AUDIT COMPLETE.

## Live Pulse - 2026-05-17T05:34+04:00

This is a SQLite live pulse only. It uses the latest 04:46 LOC denominator and does not update H-Phi.

| Metric | Value |
|---|---:|
| Start | 2026-05-17T05:34:08+04:00 |
| End | 2026-05-17T05:34:38+04:00 |
| Duration | 30.009129 sec |
| Current SQLite tokens | 50,953,580,001 |
| 30-second delta | 1,648,101 |
| Tokens/sec | 54,919.99 |
| Tokens/min | 3,295,199.27 |
| Tokens/hour | 197,711,956.25 |
| Tokens/day equivalent | 4,745,086,950.04 |
| Cache-aware rate range | USD 2.52-3.00/min; USD 151.43-179.96/hour |
| No-cache scenario rate | USD 16.73/min; USD 1,004.05/hour |
| Current energy estimate | 2,547.68 MWh |
| Tokens per meaningful LOC | 60,726.33 |
| Tokens per script byte | 1,138.73 |

Top burners: `Enforce DataVault statelessness` 460,086; `CONTENT_AUTHORITY_DICTATOR` 404,169; `Move reports to batch006` 328,033; `Build ballast PID` 284,567; `Improve bot memory and CRM` 171,246.

STATUS: AUDIT COMPLETE.

## H-Phi Live Rebase - 2026-05-17T11:42+04:00

This section adds the latest H-Phi/token window after the large post-04:12 source drift.

| Metric | Value |
|---|---:|
| C# files modified after 04:12 | 113 |
| Current meaningful LOC | 854,943 |
| Current script bytes | 46,232,512 |
| Runtime H-Phi risk | 0.005378664 |
| Runtime H-Phi narrow | 0.075881112 |
| Data sovereignty | 0.141543476 |
| Delta vs 04:12 Runtime risk | +0.000519851 |
| Delta vs 04:12 Runtime narrow | +0.005594882 |
| Token window total | 501,495,243 |
| Token window cache-aware cost | USD 397.22 |
| Token window no-cache equivalent | USD 2,548.92 |
| Token window average | 18,578.71 tokens/sec |
| 11:38 SQLite total | 51,066,572,323 |
| 11:38 live pulse | 3,001,335 tokens in 30.099099 sec |
| 11:38 live rate | 99,715.11 tokens/sec |
| Current energy estimate | 2,553.33 MWh |
| Tokens per meaningful LOC | 59,730.97 |
| Tokens per script byte | 1,104.56 |

STATUS: AUDIT COMPLETE.
