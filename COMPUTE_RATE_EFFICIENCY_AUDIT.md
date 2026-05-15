# COMPUTE RATE EFFICIENCY AUDIT

Status: AUDIT COMPLETE
Snapshot: 2026-05-15T04:47+04:00
Agent: COMPUTE_LOGISTICS_AUDITOR
Source: `.codex` JSONL + `state_5.sqlite` + source byte scan

## Boundary

This is not an invoice. It is local token-ledger accounting.

Pricing reference: official OpenAI API pricing checked on 2026-05-15 at `https://openai.com/api/pricing/`. Legacy local Codex SKUs that are absent from the current public table are treated as proxy estimates. The exact bill requires a billing export.

## Latest Ledger

| Metric | Value |
|---|---:|
| JSONL sessions with final usage | 756 |
| JSONL final total tokens | 44,590,504,461 |
| JSONL input tokens | 44,439,003,137 |
| JSONL cached input tokens | 42,661,425,024 |
| JSONL non-cached input tokens | 1,777,578,113 |
| JSONL output tokens | 151,242,924 |
| JSONL reasoning output tokens | 52,468,010 |
| SQLite threads | 765 |
| SQLite token sum | 44,567,638,432 |
| JSONL minus SQLite | 22,866,029 tokens |
| JSONL/SQLite drift | 0.0513% |
| Cached-input ratio | 95.99996% |
| Cache-miss ratio | 4.00004% |
| Cached tokens per non-cached token | 23.9997 |
| Output/input ratio | 0.34034% |
| Energy by prompt constant | 2,229.53 MWh |

The ledger is still moving. Older snapshots remain valid captures at their timestamps, not current truth.

## Cache-Aware Cost Scenarios

| Scenario | Cache-aware cost | No-cache equivalent | Cache avoided | Avoided share | Blended cost / 1M total tokens | Tokens / USD |
|---|---:|---:|---:|---:|---:|---:|
| Model-aware local ledger lower bound | USD 28,860.62 | USD 189,914.82 | USD 161,054.20 | 84.803% | USD 0.647237 | 1,545,029 |
| All tokens as GPT-5.5 standard | USD 34,755.89 | USD 226,732.30 | USD 191,976.41 | 84.671% | USD 0.779446 | 1,282,962 |
| All tokens as GPT-5.5 long-context | USD 67,243.14 | USD 451,195.96 | USD 383,952.83 | 85.097% | USD 1.508015 | 663,123 |

Use the model-aware row as a lower-bound local estimate. Use the all-GPT-5.5 rows when the question is "what would this cost if everything were re-priced as current GPT-5.5".

## Model-Aware Split

| Model | Sessions | Input | Cached input | Non-cached input | Output | Total | Cache-aware cost |
|---|---:|---:|---:|---:|---:|---:|---:|
| `gpt-5.5` | 456 | 30,774,084,098 | 29,596,346,624 | 1,177,737,474 | 95,769,770 | 30,870,112,268 | USD 23,559.95 |
| `gpt-5.4` | 244 | 11,546,273,790 | 11,051,701,632 | 494,572,158 | 46,453,047 | 11,592,726,837 | USD 4,696.15 |
| `unknown` | 20 | 1,827,978,390 | 1,753,426,432 | 74,551,958 | 7,086,776 | 1,835,065,166 | USD 536.53 |
| `gpt-5.4-mini` | 25 | 191,173,213 | 167,098,752 | 24,074,461 | 1,359,886 | 192,533,099 | USD 36.71 |
| `gpt-5.2-codex` | 3 | 85,044,900 | 79,787,648 | 5,257,252 | 468,092 | 85,512,992 | USD 29.72 |
| `gpt-5.1-codex-mini` | 2 | 13,218,484 | 12,128,000 | 1,090,484 | 97,343 | 13,315,827 | USD 0.77 |
| `gpt-5.3-codex` | 3 | 1,088,533 | 879,744 | 208,789 | 7,580 | 1,096,113 | USD 0.63 |
| `gpt-5.2` | 3 | 141,729 | 56,192 | 85,537 | 430 | 142,159 | USD 0.17 |

The `unknown` bucket is 4.117% of final JSONL tokens. Do not hide it. Do not over-claim exact billing until those sessions are mapped.

## Token Flow

Observation window: 2026-04-03T17:10:34.949Z to 2026-05-15T00:46:25.021Z.

| Window | Tokens | Tokens/sec | Tokens/min | Tokens/hour | Tokens/day equivalent |
|---|---:|---:|---:|---:|---:|
| Whole observed period | 44,590,504,461 | 12,491.21 | 749,472.71 | 44,968,362.72 | 1,079,240,705.29 |
| Last 1h | 405,171,652 | 112,547.68 | 6,752,860.87 | 405,171,652.00 | 9,724,119,648.00 |
| Last 6h | 2,109,288,480 | 97,652.24 | 5,859,134.67 | 351,548,080.00 | 8,437,153,920.00 |
| Last 24h | 2,858,505,035 | 33,084.55 | 1,985,072.94 | 119,104,376.46 | 2,858,505,035.00 |
| Last 7d | 19,239,318,742 | 31,811.04 | 1,908,662.57 | 114,519,754.42 | 2,748,474,106.00 |
| Last 14d | 29,622,111,514 | 24,489.18 | 1,469,350.77 | 88,161,046.17 | 2,115,865,108.14 |
| Last 30d | 41,728,778,036 | 16,099.07 | 965,943.94 | 57,956,636.16 | 1,390,959,267.87 |

## Peak Buckets

These are token-accounting buckets, not raw API request arrival rates. A spike can include a large turn checkpoint.

| Bucket | Peak label | Tokens | Equivalent tokens/sec |
|---|---|---:|---:|
| Second | 2026-04-11 19:12:43 UTC | 23,433,405 | 23,433,405.00 |
| Minute | 2026-04-13 14:59 UTC | 36,323,325 | 605,388.75 |
| Hour | 2026-05-14 23 UTC | 402,375,981 | 111,771.11 |
| Day | 2026-05-13 | 3,951,756,366 | 45,737.92 |
| Week | 2026-W19 | 14,308,828,640 | 23,658.78 |

## Prompt/Event Cadence

This count uses JSONL `response_item` user-message rows. It is broader than "human typed once" because transcript replay/imported user rows can appear as user messages.

| Bucket | Peak label | User-message rows |
|---|---|---:|
| Second | 2026-04-13 14:59:32 UTC | 35 |
| Minute | 2026-04-13 14:59 UTC | 132 |
| Hour | 2026-04-13 14 UTC | 470 |
| Day | 2026-05-08 | 824 |
| Week | 2026-W19 | 3,023 |

## Code Byte Ratios

| Scope | Files | Bytes | Physical LOC | Tokens/source byte | Tokens/physical LOC |
|---|---:|---:|---:|---:|---:|
| `Assets/_Project/Scripts` | 1,501 | 41,654,805 | 953,631 | 1,070.477 | 46,758.66 |
| `Assets/_Project` | 1,549 | 42,366,273 | 971,342 | 1,052.500 | 45,906.08 |
| `Assets` | 4,112 | 66,640,354 | 1,588,813 | 669.122 | 28,065.29 |
| `Packages` | 984 | 5,549,590 | 142,887 | 8,034.919 | 312,068.31 |

Using the earlier verified meaningful script LOC of 775,435:

| Ratio | Value |
|---|---:|
| Tokens per meaningful script LOC | 57,503.86 |
| Tokens per earlier physical script LOC | 47,118.86 |
| Model-aware lower-bound cost per meaningful LOC | USD 0.03722 |
| All-GPT-5.5 standard cost per meaningful LOC | USD 0.04482 |
| Context amplification vs 50-token/LOC heuristic | 1,150.08x |

## Worst File Burn Per LOC

Source: `COMPUTE_FILE_BURN_ATTRIBUTION.md`; weighted tokens divided by current LOC from that attribution pass.

| Rank | File | Class | Weighted tokens | Current LOC | Tokens/LOC | Dirty |
|---:|---|---|---:|---:|---:|---|
| 1 | `BUILD_PLAYTEST_ISSUES.md` | docs | 81,870,163 | 1,082 | 75,665.59 | No |
| 2 | `Assets/_Project/Scripts/CrashTelemetryBuffer.cs` | code | 193,185,153 | 3,472 | 55,640.89 | No |
| 3 | `MASTER_RELEASE_WORK_PLAN.md` | docs | 116,894,969 | 2,333 | 50,105.00 | No |
| 4 | `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` | code | 261,401,331 | 6,894 | 37,917.22 | No |
| 5 | `Docs/Reports/2026-05-03_FOUNDATION_HARDENING_CONTINUATION.md` | docs | 51,741,688 | 1,479 | 34,984.24 | No |
| 6 | `Assets/_Project/Scripts/BaseModule.cs` | code | 176,763,826 | 5,459 | 32,380.26 | No |
| 7 | `Assets/_Project/Scripts/Editor/WorldProceduralSeaweedMeshBuilder.cs` | code | 77,221,028 | 2,542 | 30,378.06 | No |
| 8 | `Assets/_Project/Scripts/HectonFabricatorUI.cs` | code | 51,479,020 | 1,731 | 29,739.47 | No |
| 9 | `Assets/_Project/Art/Shaders/SargassumMicroFaunaBoids.compute` | assets | 55,821,413 | 2,094 | 26,657.79 | No |
| 10 | `Assets/_Project/Scripts/Fauna/FaunaBrain.cs` | code | 158,398,777 | 6,712 | 23,599.34 | No |

Interpretation: high tokens/LOC means audit pressure, retries, and shared-surface complexity. It does not prove the file is bad. It proves the file is expensive to keep dragging through long-context agents.

## Verdict

The cache is carrying the economy. Roughly 96% of input tokens are cached, so the project can survive the bill. But 57,504 tokens per meaningful LOC is not a clean engineering pipeline. It is context recursion with enough cache discount to stay solvent.

STATUS: AUDIT COMPLETE.
