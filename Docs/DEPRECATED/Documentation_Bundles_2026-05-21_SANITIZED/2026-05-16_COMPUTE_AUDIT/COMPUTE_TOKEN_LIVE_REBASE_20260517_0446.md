# COMPUTE TOKEN LIVE REBASE 2026-05-17 04:46

Status: AUDIT COMPLETE
Scope: HECTON-8 only. Timaert excluded.
Evidence class: SQLite live pulse + bounded JSONL post-H-Phi window + static script LOC scan.
Invoice status: NOT AN INVOICE.

No new H-Phi scan was run in this pass. The latest H-Phi scan finished at 2026-05-17T04:11:59+04:00 and took 170,338 ms. Re-running it 25 minutes later would be wasteful without evidence of a large source movement. This pass measures token drift after that H-Phi artifact.

## Post-04:12 JSONL Window

Window: 2026-05-17T04:11:59+04:00 to 2026-05-17T04:41:52.884+04:00.

| Metric | Value |
|---|---:|
| Duration | 1,793.884547 sec |
| Prefiltered JSONL files | 45 |
| Prefiltered bytes | 525,293,697 |
| Rows read | 190,097 |
| Token rows in window | 1,212 |
| Usable usage rows | 1,212 |
| Prompt rows in window | 37 |
| Parse errors | 0 |
| Model bucket | `gpt-5.5` only |
| Input tokens | 189,593,548 |
| Cached input tokens | 176,339,968 |
| Output tokens | 628,293 |
| Reasoning output tokens | 177,380 |
| Total tokens | 190,381,072 |
| Cache ratio | 93.009% |
| Cache-aware cost | USD 173.29 |
| No-cache equivalent | USD 966.82 |
| Long-context surcharge events over 272K input | 0 |
| Average tokens/sec | 106,127.83 |
| Average tokens/min | 6,367,669.72 |
| Average tokens/hour | 382,060,183.50 |
| Cache-aware USD/min | USD 5.80 |
| Cache-aware USD/hour | USD 347.75 |

Peak cadence:

| Peak | Value |
|---|---:|
| Token peak second | 1,171,462 at 2026-05-17T04:41:08+04:00 |
| Token peak minute | 17,679,821 at 2026-05-17T04:41+04:00 |
| Prompt peak minute | 4 user-message rows at 2026-05-17T04:38+04:00 |

## SQLite Live State

SQLite 30-second pulse at 2026-05-17T04:38:25 to 04:38:55+04:00:

| Metric | Value |
|---|---:|
| Start tokens | 50,603,827,937 |
| End tokens | 50,603,827,937 |
| 30-second delta | 0 |
| Tokens/sec | 0 |

That pulse was quiet, but it did not describe the whole post-04:12 window. SQLite total later moved.

SQLite summary at 2026-05-17T04:45:54+04:00:

| Metric | Value |
|---|---:|
| Current SQLite tokens | 50,636,429,732 |
| Delta since 04:09 SQLite total | +110,281,428 |
| Estimated cache-aware delta since 04:09 | USD 84.46 |
| Estimated current cache-aware total | USD 34,443.33 |
| Current energy estimate | 2,531.82 MWh |

Model split:

| Model | Threads | Tokens |
|---|---:|---:|
| `gpt-5.5` | 536 | 38,789,808,523 |
| `gpt-5.4` | 241 | 11,553,863,916 |
| `gpt-5.4-mini` | 25 | 192,533,099 |
| `gpt-5.2-codex` | 3 | 85,512,992 |
| `gpt-5.1-codex-mini` | 3 | 13,472,930 |
| `gpt-5.3-codex` | 3 | 1,096,113 |
| `gpt-5.2` | 3 | 142,159 |

## Current Code Ratio

Static scan: `Assets/_Project/Scripts/**/*.cs`.

| Metric | Value |
|---|---:|
| Script files | 1,581 |
| Physical LOC | 1,019,121 |
| Blank lines | 137,610 |
| Comment lines | 42,442 |
| Meaningful LOC | 839,069 |
| Script bytes | 44,746,126 |
| Logic density | 82.3326% |
| SQLite tokens / meaningful LOC | 60,348.35 |
| SQLite tokens / physical LOC | 49,686.38 |
| SQLite tokens / script byte | 1,131.64 |
| Burn / source-text proxy ratio | 4,526.55x |

## Active Thread Burners 04:46

20-second per-thread SQLite delta at 2026-05-17T04:46:23 to 04:46:43+04:00.

| Metric | Value |
|---|---:|
| Active delta threads | 3 |
| Total delta | 497,906 |
| Tokens/sec | 24,895.30 |
| Tokens/min | 1,493,718 |
| Tokens/day equivalent | 2,150,953,920 |
| Cache-aware rate, blended | USD 1.14/min; USD 68.64/hour; USD 1,647.40/day |

| Rank | Thread title | Delta tokens |
|---:|---|---:|
| 1 | Add modulo time slicer | 193,366 |
| 2 | AUDIO_IMPORT_RESIDENCY_GUARD prompt thread | 169,754 |
| 3 | Add indirect flora drawing | 134,786 |

Verdict: after the 04:12 H-Phi scan the system did not stop. It had a quiet 30-second SQLite pulse, then a JSONL-visible 04:41 burst, then a cooler but still active 04:46 per-thread burn. No fresh H-Phi score should be inferred from this token movement until another static scan is run.

STATUS: AUDIT COMPLETE.
