# COMPUTE LIVE DELTA 2026-05-16

Status: AUDIT COMPLETE
Snapshot: 2026-05-16T05:19+04:00
Source: `C:\Users\danat\.codex\state_5.sqlite`
Boundary: SQLite live-tail only. This does not recalculate input/cached/output; costs use the latest full JSONL `gpt-5.5` blended rates.

## Method

1. Read all `threads.tokens_used`.
2. Wait 30 seconds.
3. Read all `threads.tokens_used` again.
4. Attribute positive deltas by thread ID.

Blended rate from the 2026-05-16 full JSONL scan:

| Scenario | USD / 1M tokens |
|---|---:|
| `gpt-5.5` cache-aware blended | 0.765893 |
| `gpt-5.5` no-cache blended | 5.079664 |

## Live 30-Second Sample

| Metric | Value |
|---|---:|
| Sample start | 2026-05-16T05:18:34.716+04:00 |
| Sample end | 2026-05-16T05:19:04.858+04:00 |
| Elapsed | 30.14228 sec |
| Start SQLite tokens | 47,802,606,335 |
| End SQLite tokens | 47,804,795,352 |
| Delta | 2,189,017 tokens |
| Tokens/sec | 72,622.81 |
| Tokens/min | 4,357,368.45 |
| Tokens/hour | 261,442,107.23 |
| Tokens/day equivalent | 6,274,610,573.59 |
| Cache-aware sample cost | USD 1.68 |
| No-cache sample equivalent | USD 11.12 |
| Cache-aware rate | USD 3.34/min; USD 200.24/hour; USD 4,805.68/day |
| No-cache rate | USD 22.13/min; USD 1,328.04/hour; USD 31,872.91/day |
| Active threads | 10 |

This is a short live pulse, not a stable daily forecast. It is still high enough to matter.

## Delta Since Full 03:56 Snapshot

Previous full-audit SQLite snapshot: 2026-05-16T03:56:25.453+04:00, `47,465,726,066` tokens.

| Metric | Value |
|---|---:|
| End SQLite tokens | 47,804,795,352 |
| Delta since 03:56 snapshot | 339,069,286 tokens |
| Elapsed since 03:56 snapshot | 4,959.405 sec |
| Average tokens/sec | 68,368.94 |
| Cache-aware estimated cost | USD 259.69 |
| No-cache equivalent | USD 1,722.36 |
| Cache-aware rate | USD 3.14/min; USD 188.51/hour; USD 4,524.19/day |

## Active Thread Deltas

| Rank | Delta tokens | Share | Model | Thread title |
|---:|---:|---:|---|---|
| 1 | 430,495 | 19.67% | `gpt-5.5` | Automate H8Memory lifecycle |
| 2 | 334,305 | 15.27% | `gpt-5.5` | Build ballast PID |
| 3 | 323,029 | 14.76% | `gpt-5.5` | Add Verlet tow cable physics |
| 4 | 212,820 | 9.72% | `gpt-5.5` | Sync flora bioluminescence pulses |
| 5 | 193,467 | 8.84% | `gpt-5.5` | Add indirect flora drawing |
| 6 | 177,517 | 8.11% | `gpt-5.5` | Build marine snow advection |
| 7 | 176,395 | 8.06% | `gpt-5.5` | Add Burst funnel smoothing |
| 8 | 161,194 | 7.36% | `gpt-5.5` | Build jaw and tentacle IK |
| 9 | 132,148 | 6.04% | `gpt-5.5` | AUDIO_IMPORT_RESIDENCY_GUARD |
| 10 | 47,647 | 2.18% | `gpt-5.5` | Build hull repair engine |

Top 3 threads account for 49.70% of the 30-second live burn.

## Verdict

The burn did not cool down after the full 03:56 audit. The live 05:18-05:19 sample ran hotter than the previous rolling 24h average: 72.6K tokens/sec live vs 37.5K tokens/sec rolling 24h.

This remains a high-concurrency, high-cache-reuse workload. Cache makes it affordable relative to no-cache pricing. It does not make it disciplined.

STATUS: AUDIT COMPLETE.
