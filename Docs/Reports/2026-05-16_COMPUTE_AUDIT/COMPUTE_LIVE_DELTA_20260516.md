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

## Continuation Sample 4 - 2026-05-16T23:59+04:00

Source: `C:\Users\danat\.codex\state_5.sqlite`.
Boundary: SQLite live-tail only. Costs use the latest full JSONL blended `gpt-5.5` cache-aware/no-cache rates.

| Metric | Value |
|---|---:|
| Sample start | 2026-05-16T23:58:34+04:00 |
| Sample end | 2026-05-16T23:59:20+04:00 |
| Elapsed | 45 sec |
| Start SQLite tokens | 49,899,014,761 |
| End SQLite tokens | 49,903,844,533 |
| Delta | 4,829,772 tokens |
| Tokens/sec | 107,328.27 |
| Tokens/min | 6,439,696.00 |
| Tokens/hour | 386,381,760.00 |
| Tokens/day equivalent | 9,273,162,240.00 |
| Cache-aware rate, blended | USD 4.93/min; USD 295.93/hour; USD 7,102.25/day |
| Active threads | 29 |

Delta since the 23:14 live sample:

| Metric | Value |
|---|---:|
| 23:14 SQLite tokens | 49,767,593,348 |
| 23:59 SQLite tokens | 49,903,844,533 |
| Delta | 136,251,185 tokens |
| Cache-aware estimated cost, blended | USD 104.35 |

Top live threads in this sample:

| Rank | Delta tokens | Model | Thread title |
|---:|---:|---|---|
| 1 | 400,840 | `gpt-5.5` | ARCHITECT_SPATIAL_PROBE prompt thread |
| 2 | 397,216 | `gpt-5.5` | Study Subnautica and project |
| 3 | 274,984 | `gpt-5.5` | Add Burst funnel smoothing |
| 4 | 227,974 | `gpt-5.5` | Fix ASMDEF graph |
| 5 | 222,845 | `gpt-5.5` | Add modulo time slicer |
| 6 | 221,776 | `gpt-5.5` | Automate H8Memory lifecycle |
| 7 | 217,172 | `gpt-5.5` | Implement SDF gap traversal |
| 8 | 216,741 | `gpt-5.5` | CONTENT_AUTHORITY_DICTATOR prompt thread |
| 9 | 210,578 | `gpt-5.5` | Add VR foveated rendering |
| 10 | 207,765 | `gpt-5.5` | Build ballast PID |

The pulse cooled from the 23:14 short peak but remains above 100K tokens/sec.

STATUS: AUDIT COMPLETE.

## Continuation Sample 3 - 2026-05-16T23:14+04:00

Source: `C:\Users\danat\.codex\state_5.sqlite`.
Boundary: SQLite live-tail only. Costs use the latest full JSONL blended `gpt-5.5` cache-aware/no-cache rates.

| Metric | Value |
|---|---:|
| Sample start | 2026-05-16T23:14:21+04:00 |
| Sample end | 2026-05-16T23:14:51+04:00 |
| Elapsed | 30.0 sec |
| Start SQLite tokens | 49,763,778,148 |
| End SQLite tokens | 49,767,593,348 |
| Delta | 3,815,200 tokens |
| Tokens/sec | 127,173.33 |
| Tokens/min | 7,630,400.00 |
| Tokens/hour | 457,824,000.00 |
| Tokens/day equivalent | 10,987,776,000.00 |
| Cache-aware rate, blended | USD 5.84/min; USD 350.64/hour; USD 8,415.46/day |
| Active threads | 25 |

Delta since the 14:57 live rebase:

| Metric | Value |
|---|---:|
| 14:57 SQLite tokens | 48,761,315,725 |
| 23:14 SQLite tokens | 49,767,593,348 |
| Delta | 1,006,277,623 tokens |
| Cache-aware estimated cost, blended | USD 770.70 |

Top live threads in this sample:

| Rank | Delta tokens | Model | Thread title |
|---:|---:|---|---|
| 1 | 236,257 | `gpt-5.5` | Add GPU-only debris chips |
| 2 | 235,551 | `gpt-5.5` | Add predator headlight reaction |
| 3 | 233,851 | `gpt-5.5` | Add wake displacement |
| 4 | 232,932 | `gpt-5.5` | Add VR hand grabbing |
| 5 | 230,541 | `gpt-5.5` | Build CSV balance pipeline |
| 6 | 212,146 | `gpt-5.5` | Integrate caustics rust fog shader |
| 7 | 210,420 | `gpt-5.5` | Add WFC laser clipping |
| 8 | 197,750 | `gpt-5.5` | CONTENT_AUTHORITY_DICTATOR prompt thread |
| 9 | 192,565 | `gpt-5.5` | Build loot magnet system |
| 10 | 185,637 | `gpt-5.5` | Sync flora bioluminescence pulses |

This is the hottest short pulse recorded in this 2026-05-16 continuation set: 127.2K tokens/sec versus 93.2K at 14:57 and 72.6K at 05:19.

STATUS: AUDIT COMPLETE.

## Continuation Sample 2 - 2026-05-16T14:57+04:00

Source: `C:\Users\danat\.codex\state_5.sqlite`.
Boundary: SQLite live-tail only. Input/cache/output split is inherited from the latest full JSONL scan as a blended `gpt-5.5` rate.

| Metric | Value |
|---|---:|
| Sample start | 2026-05-16T14:56:12.600+04:00 |
| Sample end | 2026-05-16T14:57:12.747+04:00 |
| Measured interval used for rates | 60.0 sec |
| Start SQLite tokens | 48,755,724,204 |
| End SQLite tokens | 48,761,315,725 |
| Delta | 5,591,521 tokens |
| Tokens/sec | 93,192.02 |
| Tokens/min | 5,591,521.00 |
| Tokens/hour | 335,491,260.00 |
| Tokens/day equivalent | 8,051,790,240.00 |
| Cache-aware sample cost | USD 4.28 |
| No-cache sample equivalent | USD 28.40 |
| Cache-aware rate | USD 4.28/min; USD 256.95/hour; USD 6,166.81/day |
| No-cache rate | USD 28.40/min; USD 1,704.18/hour; USD 40,900.39/day |
| Active threads | 29 |

Delta since previous 05:19 live sample end:

| Metric | Value |
|---|---:|
| Previous live sample end tokens | 47,804,795,352 |
| Current end tokens | 48,761,315,725 |
| Delta | 956,520,373 tokens |
| Cache-aware estimated cost | USD 732.59 |
| No-cache equivalent | USD 4,858.80 |

Delta since 03:56 SQLite snapshot:

| Metric | Value |
|---|---:|
| 03:56 SQLite tokens | 47,465,726,066 |
| Current end tokens | 48,761,315,725 |
| Delta | 1,295,589,659 tokens |
| Cache-aware estimated cost | USD 992.28 |
| No-cache equivalent | USD 6,581.16 |

### Active Thread Deltas, Sample 2

| Rank | Delta tokens | Share | Model | Thread title |
|---:|---:|---:|---|---|
| 1 | 445,466 | 7.97% | `gpt-5.5` | Sync flora bioluminescence pulses |
| 2 | 426,988 | 7.64% | `gpt-5.5` | Implement SDF gap traversal |
| 3 | 421,393 | 7.54% | `gpt-5.5` | Bridge macro-swarms into simulation |
| 4 | 398,644 | 7.13% | `gpt-5.5` | Build loot magnet system |
| 5 | 350,609 | 6.27% | `gpt-5.5` | Add spline docking autopilot |
| 6 | 317,015 | 5.67% | `gpt-5.5` | Manage biota spawning pool |
| 7 | 253,946 | 4.54% | `gpt-5.5` | Add Leviathan stalking AI |
| 8 | 225,480 | 4.03% | `gpt-5.5` | Add visor Snell refraction |
| 9 | 220,704 | 3.95% | `gpt-5.5` | Move reports to batch006 |
| 10 | 219,595 | 3.93% | `gpt-5.5` | Implement 300-frame state hashing |

Top 10 threads account for 58.67% of this 60-second live burn. The concurrency widened from 10 active threads at 05:19 to 29 active threads at 14:57.

STATUS: AUDIT COMPLETE.
