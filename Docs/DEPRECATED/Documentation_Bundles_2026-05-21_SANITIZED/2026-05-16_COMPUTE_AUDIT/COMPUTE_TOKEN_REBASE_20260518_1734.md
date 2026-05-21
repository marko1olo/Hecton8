# COMPUTE TOKEN REBASE 2026-05-18 17:34

Status: AUDIT COMPLETE
Scope: HECTON/Hades local Codex telemetry. `C:\Users\danat\Downloads` excluded from HECTON totals and listed only as non-project drift.
Evidence class: LOCAL_SQLITE + LOCAL_JSONL + STATIC_SOURCE_SIZE.
Invoice status: NOT AN INVOICE. This is local telemetry accounting.

## Method

This pass used the same anti-overcount rule as the prior compute audit:

- `C:\Users\danat\.codex\state_5.sqlite` for fast current `threads.tokens_used`.
- `C:\Users\danat\.codex\sessions/**/*.jsonl` for final per-session `total_token_usage` and input/cache/output split.
- Rolling windows use per-thread cumulative `total_token_usage` deltas with pre-window baselines.
- Repeated `last_token_usage` rows were not summed.
- `reasoning_output_tokens` is treated as a subset of output tokens, not an extra billable line.

## Current Totals

Primary current total is SQLite because it includes live thread tail at the end of the audit.

| Metric | Value |
|---|---:|
| SQLite HECTON/Hades tokens at 2026-05-18T17:35:14+04:00 | 54,517,775,171 |
| SQLite all-local tokens at 2026-05-18T17:35:14+04:00 | 54,555,349,108 |
| Excluded non-project SQLite tokens | 37,573,937 |
| JSONL HECTON/Hades final tokens at 2026-05-18T17:34:06+04:00 | 54,468,241,841 |
| JSONL all-local final tokens | 54,507,104,762 |
| SQLite live tail over JSONL HECTON final | +49,533,330 |
| Delta vs 2026-05-17T15:39 SQLite total | +2,931,323,073 |

## JSONL Scan Surface

| Metric | Value |
|---|---:|
| JSONL files scanned | 1,002 |
| JSONL bytes scanned | 9,580,317,579 |
| Sessions seen | 1,002 |
| JSONL files with final usage | 984 |
| HECTON/Hades files with final usage | 981 |
| Token rows seen | 468,382 |
| Usable token rows | 468,382 |
| Parse errors | 0 |
| Negative cumulative deltas observed | 8 |
| Earliest token timestamp UTC | 2026-04-03T17:10:40.595Z |
| Latest token timestamp UTC | 2026-05-18T13:34:06.955Z |
| Latest token timestamp local | 2026-05-18T17:34:06+04:00 |

## HECTON Token Split

| Metric | Value |
|---|---:|
| Input tokens | 54,281,061,389 |
| Cached input tokens | 52,113,735,040 |
| Non-cached input tokens | 2,167,326,349 |
| Output tokens | 186,922,052 |
| Reasoning output tokens | 63,548,475 |
| Cached-input ratio | 96.007% |
| Output/input ratio | 0.344% |

## Cost Estimate

Pricing uses the same model buckets as the 2026-05-16 compute ledger.

| Scope | Cache-aware estimate | No-cache equivalent |
|---|---:|---:|
| JSONL HECTON/Hades final | USD 37,575.94 | USD 246,487.42 |
| SQLite HECTON/Hades live total | USD 37,610.11 | USD 246,711.58 |
| SQLite all-local live total | USD 37,632.25 | USD 246,806.22 |

Cache avoided for JSONL HECTON/Hades final: USD 208,911.48.

## Rolling HECTON Windows

| Window | Tokens | Tokens/sec | Tokens/min | Tokens/hour | Tokens/day equiv | Cache-aware cost | No-cache equivalent | Cached-input ratio |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Last 1h | 185,331,409 | 51,480.95 | 3,088,856.82 | 185,331,409.00 | 4,447,953,816.00 | USD 127.85 | USD 838.69 | 95.060% |
| Last 6h | 519,532,296 | 24,052.42 | 1,443,145.27 | 86,588,716.00 | 2,078,129,184.00 | USD 358.41 | USD 2,351.06 | 94.696% |
| Last 24h | 2,862,892,706 | 33,135.33 | 1,988,119.93 | 119,287,196.08 | 2,862,892,706.00 | USD 1,975.02 | USD 12,955.57 | 95.832% |
| Last 7d | 20,584,216,692 | 34,034.75 | 2,042,084.99 | 122,525,099.36 | 2,940,602,384.57 | USD 14,200.41 | USD 93,150.62 | 96.179% |
| Last 14d | 35,876,843,205 | 29,660.09 | 1,779,605.32 | 106,776,319.06 | 2,562,631,657.50 | USD 24,750.32 | USD 162,354.98 | 96.055% |
| Last 30d | 49,819,568,589 | 19,220.51 | 1,153,230.75 | 69,193,845.26 | 1,660,652,286.30 | USD 34,368.97 | USD 225,450.58 | 96.082% |

Peak rolling-window cadence:

| Peak | Value |
|---|---:|
| Last 1h peak minute | 6,665,944 at 2026-05-18T12:36Z |
| Last 24h peak minute | 13,384,196 at 2026-05-17T21:57Z |
| Last 24h peak hour | 328,586,709 at 2026-05-17T21Z |
| Last 30d peak minute | 16,123,293 at 2026-05-08T21:56Z |
| Last 30d peak hour | 402,375,981 at 2026-05-14T23Z |

## Model Split

| Model | Sessions | Final tokens | Input | Cached input | Output | Cache ratio |
|---|---:|---:|---:|---:|---:|---:|
| `gpt-5.5` | 712 | 42,668,089,559 | 42,528,850,591 | 40,881,321,600 | 138,980,568 | 96.126% |
| `gpt-5.4` | 235 | 11,518,937,929 | 11,472,935,319 | 10,982,986,112 | 46,002,610 | 95.730% |
| `gpt-5.4-mini` | 22 | 182,545,249 | 181,255,633 | 158,516,352 | 1,289,616 | 87.455% |
| `gpt-5.2-codex` | 4 | 84,115,005 | 83,571,100 | 77,847,040 | 543,905 | 93.151% |
| `gpt-5.1-codex-mini` | 2 | 13,315,827 | 13,218,484 | 12,128,000 | 97,343 | 91.750% |
| `gpt-5.3-codex` | 3 | 1,096,113 | 1,088,533 | 879,744 | 7,580 | 80.819% |
| `gpt-5.2` | 3 | 142,159 | 141,729 | 56,192 | 430 | 39.647% |

## SQLite CWD Split

| CWD | Threads | Tokens | Scope |
|---|---:|---:|---|
| `\\?\C:\hades` | 660 | 36,520,368,690 | HECTON/Hades |
| `\\?\C:\hades\Hecton8` | 291 | 17,755,525,588 | HECTON/Hades |
| `c:\hades\Hecton8` | 29 | 198,567,084 | HECTON/Hades |
| `c:\hades` | 19 | 37,815,861 | HECTON/Hades |
| `\\?\C:\Users\danat\Downloads` | 3 | 37,573,937 | Excluded |

## Source And Docs Ratio

| Metric | Value |
|---|---:|
| First-party script files | 1,698 |
| Physical script LOC | 1,127,464 |
| Meaningful script LOC | 934,997 |
| Script bytes | 50,031,325 |
| Tokens per meaningful LOC, SQLite HECTON | 58,302.09 |
| Tokens per script byte, SQLite HECTON | 1,089.56 |
| Energy estimate, SQLite HECTON | 2,725.61 MWh |
| Energy estimate, SQLite all-local | 2,727.49 MWh |

Heaviest current source domains:

| Domain | Files | Physical LOC | Meaningful LOC |
|---|---:|---:|---:|
| `(root)` | 337 | 311,983 | 256,404 |
| `World` | 185 | 146,781 | 122,540 |
| `Editor` | 234 | 94,312 | 81,504 |
| `Core` | 180 | 96,227 | 77,265 |
| `UI` | 115 | 91,014 | 77,264 |
| `Gameplay` | 141 | 88,037 | 70,165 |
| `Construction` | 39 | 29,466 | 24,808 |
| `Audio` | 30 | 25,076 | 21,295 |
| `Fauna` | 22 | 23,067 | 19,841 |
| `Visor` | 34 | 19,799 | 16,598 |

Active docs token proxy:

| Folder | Files | Bytes | Estimated text tokens, bytes/4 |
|---|---:|---:|---:|
| `Docs/AgentLogs` | 63 | 4,690,445 | 1,172,611 |
| `Docs/Tasks` | 31 | 714,373 | 178,593 |
| `Docs/Reports` | 316 | 19,730,152 | 4,932,538 |

## Verdict

The current HECTON/Hades local token total is 54.518B by SQLite live state. The detailed JSONL split trails by 49.5M tokens because active sessions keep moving while the scan runs.

Since the prior 2026-05-17T15:39 checkpoint, the HECTON/Hades SQLite total increased by 2.931B tokens. The last 24h window alone burned 2.863B tokens. This is still long-context multi-agent recursion, not normal single-thread coding traffic.

STATUS: AUDIT COMPLETE.
