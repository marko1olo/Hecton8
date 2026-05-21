# COMPUTE LIVE BURN COOLDOWN CHECK

Status: AUDIT COMPLETE / LIVE LEDGER MOVING
Snapshot: 2026-05-15T18:31:43+04:00
Agent: COMPUTE_LOGISTICS_AUDITOR
Source: `C:\Users\danat\.codex\state_5.sqlite`, table `threads`

## Boundary

This is a short live SQLite delta sample. It is not a billing export. SQLite gives cumulative `tokens_used` by thread, not input/cached-input/output split per interval. Costs use the corrected global blended rates from `COMPUTE_CORRECTED_ROLLING_RATES.md`.

Official OpenAI pricing was rechecked on 2026-05-15 before this pass.

| Rate | Value |
|---|---:|
| Cache-aware blended rate | USD 0.670788 / 1M tokens |
| No-cache blended equivalent | USD 4.413807 / 1M tokens |

## Current Snapshot

| Metric | Value |
|---|---:|
| Snapshot local time | 2026-05-15T18:31:43.601246+04:00 |
| Current SQLite total tokens | 45,997,528,181 |
| Delta since 18:16 snapshot | 50,961,239 |
| Seconds since 18:16 snapshot | 900.672733 |
| Rate since 18:16 snapshot | 56,581.31 tokens/sec |
| Cache-aware cost since 18:16 | USD 34.18 |
| Cache-aware USD/min since 18:16 | USD 2.277 |
| No-cache USD/min since 18:16 | USD 14.984 |
| Delta beyond corrected JSONL final | 226,029,065 |
| Estimated live cost beyond corrected JSONL final | USD 151.62 |
| Current live cost estimate | USD 30,855.98 |
| Prompt-constant energy equivalent | 2,299.88 MWh |
| Live tokens per meaningful script LOC | 58,326.68 |
| Live tokens per script source byte | 1,093.413 |

## Three-Minute Cooldown Sample

| Metric | Value |
|---|---:|
| Sample start UTC | 2026-05-15T14:28:43.063342Z |
| Sample finish UTC | 2026-05-15T14:31:43.601246Z |
| Duration | 180.537904 sec |
| Sample token delta | 13,464,191 |
| Tokens/sec | 74,578.19 |
| Tokens/min | 4,474,691.70 |
| Tokens/hour equivalent | 268,481,501.81 |
| Tokens/day equivalent | 6,443,556,043.50 |
| Cache-aware sample cost | USD 9.03 |
| Cache-aware USD/min | USD 3.002 |
| Cache-aware USD/hour | USD 180.09 |
| Cache-aware USD/day equivalent | USD 4,322.26 |
| No-cache sample cost | USD 59.43 |
| No-cache USD/min | USD 19.750 |
| No-cache USD/hour | USD 1,185.03 |
| No-cache USD/day equivalent | USD 28,440.61 |
| Active threads | 19 |
| Active model bucket | `gpt-5.5` only |
| Active CWD bucket | `C:/hades` only |

## Interval Volatility

| Interval | Tokens | Tokens/sec | Cache-aware USD/min | No-cache USD/min | Active threads |
|---:|---:|---:|---:|---:|---:|
| 1 | 5,934,314 | 98,859.36 | USD 3.979 | USD 26.181 | 14 |
| 2 | 6,978,887 | 115,654.38 | USD 4.655 | USD 30.629 | 16 |
| 3 | 550,990 | 9,157.61 | USD 0.369 | USD 2.425 | 3 |

Verdict: the window is not smooth. The first two minutes were spike-level. The third minute collapsed to 9.16k tokens/sec and only 3 active threads. The honest description is volatile cooldown, not stable low burn.

## Concentration

| Slice | Tokens | Share |
|---|---:|---:|
| Top 1 thread | 1,445,360 | 10.73% |
| Top 5 threads | 6,267,156 | 46.55% |
| Top 10 threads | 10,468,364 | 77.75% |
| Top 12 threads | 11,622,496 | 86.32% |

Top active sources:

| Rank | Thread ID | Delta tokens | Title label |
|---:|---|---:|---|
| 1 | `019e2321-2f60-7fd3-8cd6-31ccbca84ce9` | 1,445,360 | Build Race Condition Hunter |
| 2 | `019e2592-efa1-7562-93d6-f671ff937574` | 1,356,030 | Implement base hibernation |
| 3 | `019e2804-6d3c-7712-a927-0839fac1cc5e` | 1,200,230 | Read batch prompt |
| 4 | `019e28dc-001a-7d83-9c7e-f2caae5752bd` | 1,185,514 | Timaert documentation verification prompt under `C:/hades` |
| 5 | `019e2310-3a80-7962-849b-5f9327a7141f` | 1,080,022 | Build outpost save delta sync |
| 6 | `019e1dfe-8ab5-7970-bba2-f7b283b05d7b` | 960,294 | Check and update documentation |
| 7 | `019e2802-6cfe-7ed1-8f84-6c466293f707` | 899,501 | TMA road/river terrain prompt |
| 8 | `019e2805-5171-7393-9a26-2291c246bd72` | 834,022 | Read own AGENT_PROMPT |
| 9 | `019e230d-8959-72f2-a88b-5e6576683819` | 817,788 | Contextual UX prompter |
| 10 | `019e27db-3780-7b80-900a-0aeb9a23f4de` | 689,603 | Form 10 agent prompts |
| 11 | `019e27d6-a009-70e3-8335-8d260d6d1000` | 618,292 | Git conflict / push repair |
| 12 | `019e2593-7f04-7c50-b5c8-3c16f805188f` | 535,840 | Fix compile wall |

## Verdict

The previous post-forecast tail was not a permanent cooldown. Between 18:16 and 18:31, the ledger added 50.96M tokens at 56.58k tokens/sec. The last minute of the sample cooled sharply, but the three-minute mean was higher than the 17:18 -> 18:16 combined mean.

No waste conviction is made. This file only proves current volatility, concentration, and spend rate.
