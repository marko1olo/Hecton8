# COMPUTE TOKEN BURN RATE LEDGER

Status: AUDIT COMPLETE
Snapshot: 2026-05-15T15:03+04:00
Agent: COMPUTE_LOGISTICS_AUDITOR
Source: `C:\Users\danat\.codex\sessions/**/*.jsonl` + `C:\Users\danat\.codex\state_5.sqlite` + `Assets/_Project/Scripts/**/*.cs`

## Boundary

This is not an OpenAI invoice. It is a local forensic estimate from Codex token telemetry.

Pricing reference checked on 2026-05-15: official OpenAI API pricing at `https://openai.com/api/pricing/` and `https://developers.openai.com/api/docs/pricing`.

Pricing assumptions used here:

| Model bucket | Input / 1M | Cached input / 1M | Output / 1M | Note |
|---|---:|---:|---:|---|
| `gpt-5.5` | USD 5.00 | USD 0.50 | USD 30.00 | Current standard scenario |
| `gpt-5.5-long` | USD 10.00 | USD 1.00 | USD 45.00 | Long-context scenario only |
| `gpt-5.4` | USD 2.50 | USD 0.25 | USD 15.00 | Current standard scenario |
| `gpt-5.4-mini` | USD 0.75 | USD 0.075 | USD 4.50 | Current mini scenario |
| `gpt-5.3-codex` / `gpt-5.2` | USD 1.75 | USD 0.175 | USD 14.00 | Codex proxy where public SKU mapping is ambiguous |
| `gpt-5.1-codex-mini` | USD 0.25 | USD 0.025 | USD 2.00 | Mini Codex proxy |
| `unknown` | USD 1.75 | USD 0.175 | USD 14.00 | Conservative proxy for 41 unmatched JSONL files |

Do not hide the `unknown` bucket: 40 final-usage sessions, 4.242B final tokens, 9.333% of JSONL final tokens. Cost precision stops there.

## Current Ledger

| Metric | Value |
|---|---:|
| JSONL session files | 765 |
| JSONL files with final usage | 747 |
| Parsed token-count rows | 364,838 |
| Parse errors | 0 |
| Observation start UTC | 2026-04-03T17:10:34.949Z |
| Latest token timestamp UTC | 2026-05-15T11:02:34.235Z |
| JSONL final total tokens | 45,453,534,197 |
| Positive-delta token flow | 45,443,684,518 |
| SQLite `threads.tokens_used` | 45,426,630,057 |
| JSONL minus SQLite | 26,904,140 |
| JSONL/SQLite drift | 0.0592% |
| Input tokens | 45,298,799,461 |
| Cached input tokens | 43,488,107,392 |
| Non-cached input tokens | 1,810,692,069 |
| Output tokens | 154,476,336 |
| Reasoning output tokens | 53,416,102 |
| Cached-input ratio | 96.00278% |
| Cache-miss ratio | 3.99722% |
| Output/input ratio | 0.34102% |
| Energy by prompt constant | 2,272.68 MWh |

The ledger is live. This file is a timestamped capture, not eternal truth.

## Cost

| Scenario | Cache-aware cost | No-cache equivalent | Cache avoided | Avoided share |
|---|---:|---:|---:|---:|
| Model-aware local estimate | USD 28,362.44 | USD 186,377.89 | USD 158,015.45 | 84.78% |
| All tokens as GPT-5.5 standard | USD 35,431.80 | USD 231,128.29 | USD 195,696.48 | 84.67% |
| All tokens as GPT-5.5 long-context | USD 68,546.46 | USD 459,939.43 | USD 391,392.97 | 85.10% |

The model-aware row is the best current local estimate. It is lower than the GPT-5.5 scenario because part of the history ran on cheaper model buckets.

## Model Split

| Model bucket | Sessions | Final tokens | Cache ratio | Cache-aware cost | No-cache cost | Blended cache cost / 1M tokens |
|---|---:|---:|---:|---:|---:|---:|
| `gpt-5.5` | 435 | 29,326,222,059 | 96.158% | USD 22,382.94 | USD 148,888.40 | USD 0.7632 |
| `gpt-5.4` | 237 | 11,592,726,837 | 95.717% | USD 4,696.15 | USD 29,562.48 | USD 0.4051 |
| `unknown` | 40 | 4,241,985,111 | 96.164% | USD 1,215.37 | USD 7,616.37 | USD 0.2865 |
| `gpt-5.4-mini` | 24 | 192,533,099 | 87.407% | USD 36.71 | USD 149.50 | USD 0.1907 |
| `gpt-5.2` / Codex proxy | 6 | 85,655,151 | 93.728% | USD 29.88 | USD 155.64 | USD 0.3489 |
| `gpt-5.1-codex-mini` | 2 | 13,315,827 | 91.750% | USD 0.77 | USD 3.50 | USD 0.0579 |
| `gpt-5.3-codex` | 3 | 1,096,113 | 80.819% | USD 0.63 | USD 2.01 | USD 0.5706 |

## Burn Rate By Window

Cost rates use each model bucket's observed blended cache-aware cost per token. This is not exact per-turn billing, because positive-delta rows do not carry a full input/cache/output split.

| Window | Tokens | Tokens/sec | Tokens/min | Tokens/hour | Tokens/day equiv | Cost | USD/min | USD/hour | USD/day equiv | No-cache cost |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Last 1h | 238,176,241 | 66,160.07 | 3,969,604.02 | 238,176,241.00 | 5,716,229,784.00 | USD 71.56 | USD 1.19 | USD 71.56 | USD 1,717.40 | USD 450.48 |
| Last 6h | 527,671,396 | 24,429.23 | 1,465,753.88 | 87,945,232.67 | 2,110,685,584.00 | USD 156.18 | USD 0.43 | USD 26.03 | USD 624.73 | USD 981.84 |
| Last 24h | 3,236,618,901 | 37,460.87 | 2,247,652.01 | 134,859,120.88 | 3,236,618,901.00 | USD 1,039.59 | USD 0.72 | USD 43.32 | USD 1,039.59 | USD 6,584.06 |
| Last 7d | 19,978,482,276 | 33,033.20 | 1,981,992.29 | 118,919,537.36 | 2,854,068,896.57 | USD 13,226.50 | USD 1.31 | USD 78.73 | USD 1,889.50 | USD 87,512.99 |
| Last 14d | 29,928,430,817 | 24,742.42 | 1,484,545.18 | 89,072,710.76 | 2,137,745,058.36 | USD 20,820.70 | USD 1.03 | USD 61.97 | USD 1,487.19 | USD 138,028.60 |
| Last 30d | 42,503,434,268 | 16,397.93 | 983,875.79 | 59,032,547.59 | 1,416,781,142.27 | USD 27,209.39 | USD 0.63 | USD 37.79 | USD 906.98 | USD 179,232.09 |
| Whole observed | 45,443,684,518 | 12,599.73 | 755,983.72 | 45,359,023.34 | 1,088,616,560.10 | USD 28,354.91 | USD 0.47 | USD 28.30 | USD 679.25 | USD 186,328.20 |

Direct answer for the latest full rolling day: 3.237B tokens burned in 24h, cache-aware estimated cost USD 1,039.59, no-cache equivalent USD 6,584.06, cache avoided USD 5,544.47.

## Live SQLite Tail Check

This is a light check after the full JSONL scan. It uses SQLite `threads.tokens_used`, so it is faster but less detailed than the JSONL pass.

| Metric | Value |
|---|---:|
| Tail check UTC | 2026-05-15T12:03:55.455Z |
| Tail check local | 2026-05-15T16:03:55+04:00 |
| SQLite tokens at full scan | 45,426,630,057 |
| SQLite tokens at tail check | 45,528,781,582 |
| Delta after full scan | 102,151,525 |
| Delta model bucket | `gpt-5.5` |
| Delta elapsed time | 3,681.22 seconds |
| Delta tokens/sec | 27,749.37 |
| Delta tokens/min | 1,664,962.08 |
| Delta tokens/hour | 99,897,725.04 |
| Delta tokens/day equivalent | 2,397,545,401.05 |
| Delta cache-aware estimated cost | USD 77.97 |
| Delta no-cache equivalent | USD 518.62 |
| Delta average cost/min | USD 1.27 |
| Delta average cost/hour | USD 76.25 |
| Delta average cost/day equivalent | USD 1,829.90 |

## Recent Calendar Days

Calendar-day buckets are UTC and can be partial at the edges.

| Day UTC | Tokens | Cache-aware cost | No-cache cost | Cache avoided | Dominant model bucket |
|---|---:|---:|---:|---:|---|
| 2026-05-06 | 1,713,970,292 | USD 1,308.17 | USD 8,701.78 | USD 7,393.61 | `gpt-5.5` |
| 2026-05-07 | 2,196,054,409 | USD 1,676.12 | USD 11,149.31 | USD 9,473.19 | `gpt-5.5` |
| 2026-05-08 | 3,187,620,273 | USD 2,432.92 | USD 16,183.46 | USD 13,750.54 | `gpt-5.5` |
| 2026-05-09 | 2,352,879,610 | USD 1,795.81 | USD 11,945.50 | USD 10,149.69 | `gpt-5.5` |
| 2026-05-10 | 1,692,316,233 | USD 1,291.64 | USD 8,591.84 | USD 7,300.20 | `gpt-5.5` |
| 2026-05-11 | 2,608,576,016 | USD 1,977.51 | USD 13,151.01 | USD 11,173.51 | `gpt-5.5` |
| 2026-05-12 | 2,414,507,314 | USD 1,804.55 | USD 11,994.79 | USD 10,190.24 | `gpt-5.5` |
| 2026-05-13 | 3,948,640,246 | USD 2,642.91 | USD 17,494.46 | USD 14,851.55 | `gpt-5.5` |
| 2026-05-14 | 2,692,719,493 | USD 1,004.31 | USD 6,437.33 | USD 5,433.01 | `unknown` |
| 2026-05-15 partial | 1,197,313,413 | USD 365.45 | USD 2,303.99 | USD 1,938.54 | `unknown` |

Peak observed UTC hour by token flow: 2026-05-14T23:00Z, 402,358,823 tokens, USD 126.95 cache-aware, USD 802.77 no-cache.

## Prompt/Event Cadence

This uses JSONL `role:user` rows. It is broader than "a human typed once"; transcript replay and imported prompt rows can appear here.

| Metric | Value |
|---|---:|
| Peak user-message rows/sec | 36 at 2026-04-13T14:59:32Z |
| Peak user-message rows/min | 136 at 2026-04-13T14:59Z |
| Peak user-message rows/hour | 494 at 2026-04-13T14Z |
| Peak user-message rows/day | 1,132 on 2026-05-08 |
| Last 6h user-message rows | 166 |
| Last 24h user-message rows | 981 |

## Code Ratios

| Metric | Value |
|---|---:|
| `Assets/_Project/Scripts/**/*.cs` files | 1,505 |
| Physical script LOC | 961,111 |
| Meaningful script LOC | 788,619 |
| Script source bytes | 42,067,847 |
| Tokens per meaningful LOC | 57,636.87 |
| Tokens per physical LOC | 47,292.70 |
| Tokens per script source byte | 1,080.482 |
| Script bytes per token | 0.0009255 |
| Tokens per KiB of script source | 1,106,413 |
| Model-aware cost per meaningful LOC | USD 0.03596 |
| All-GPT-5.5 standard cost per meaningful LOC | USD 0.04493 |
| Context amplification vs 50-token/LOC heuristic | 1,152.74x |

These are historical-context ratios against the current code surface. They are not tokenizer ratios for the code text itself.

## Verdict

The last rolling 24h burned 3.237B tokens. The current cache-aware local cost estimate for that day is about USD 1.04k. Without cache, the same local telemetry would price at about USD 6.58k.

The whole local ledger now sits at 45.454B final tokens. Cache is carrying the economy: 96.003% of input tokens are cached. That does not make the workflow clean. It means the project is converting long-context recursion into a discounted burn instead of a full-price burn.

Live source attribution is preserved at `COMPUTE_LIVE_BURN_SOURCES.md`. Latest 90-second SQLite sample: 2,725,800 tokens, 30,099.39 tokens/sec, USD 1.38/min cache-aware, 11 active threads, all `gpt-5.5`.

STATUS: AUDIT COMPLETE.
