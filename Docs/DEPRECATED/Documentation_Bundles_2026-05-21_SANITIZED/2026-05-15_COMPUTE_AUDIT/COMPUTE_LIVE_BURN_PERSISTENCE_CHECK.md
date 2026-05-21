# COMPUTE LIVE BURN PERSISTENCE CHECK

Status: AUDIT COMPLETE / LIVE LEDGER MOVING
Snapshot: 2026-05-15T18:45:33+04:00
Agent: COMPUTE_LOGISTICS_AUDITOR
Source: `C:\Users\danat\.codex\state_5.sqlite`, table `threads`

## Boundary

This is a live SQLite delta sample. It is not an invoice. SQLite exposes cumulative `tokens_used` by thread, not input/cached-input/output split for each interval. Costs use the corrected global blended rates from `COMPUTE_CORRECTED_ROLLING_RATES.md`.

Official OpenAI pricing page was rechecked on 2026-05-15 before this pass.

| Rate | Value |
|---|---:|
| Cache-aware blended rate | USD 0.670788 / 1M tokens |
| No-cache blended equivalent | USD 4.413807 / 1M tokens |

## Current Snapshot

| Metric | Value |
|---|---:|
| Snapshot local time | 2026-05-15T18:45:33.644193+04:00 |
| SQLite thread count | 767 |
| Current SQLite total tokens | 46,052,861,781 |
| Delta since 18:31 snapshot | 55,333,600 |
| Seconds since 18:31 snapshot | 830.042947 |
| Rate since 18:31 snapshot | 66,663.54 tokens/sec |
| Cache-aware cost since 18:31 | USD 37.12 |
| Cache-aware USD/min since 18:31 | USD 2.683 |
| Delta beyond corrected JSONL final | 281,362,665 |
| Estimated live cost beyond corrected JSONL final | USD 188.73 |
| Current live cost estimate | USD 30,893.09 |
| Prompt-constant energy equivalent | 2,302.64 MWh |
| Live tokens per meaningful script LOC | 58,396.85 |
| Live tokens per script source byte | 1,094.728 |

Current cumulative model split:

| Model | Tokens | Share |
|---|---:|---:|
| `gpt-5.5` | 34,168,666,635 | 74.19% |
| `gpt-5.4` | 11,591,437,853 | 25.17% |
| Other known models | 292,757,293 | 0.64% |

## Persistence Sample

This sample was taken after the previous cooldown check ended with a low final minute. It checks whether that low state persisted.

| Metric | Value |
|---|---:|
| Sample start UTC | 2026-05-15T14:43:02.798501Z |
| Sample finish UTC | 2026-05-15T14:45:33.644193Z |
| Duration | 150.845692 sec |
| Sample token delta | 10,933,623 |
| Tokens/sec | 72,482.17 |
| Tokens/min | 4,348,930.16 |
| Tokens/hour equivalent | 260,935,809.82 |
| Tokens/day equivalent | 6,262,459,435.70 |
| Cache-aware sample cost | USD 7.33 |
| Cache-aware USD/min | USD 2.917 |
| Cache-aware USD/hour | USD 175.03 |
| Cache-aware USD/day equivalent | USD 4,200.78 |
| No-cache sample cost | USD 48.26 |
| No-cache USD/min | USD 19.195 |
| No-cache USD/hour | USD 1,151.72 |
| No-cache USD/day equivalent | USD 27,641.29 |
| Active threads | 22 |
| Active model bucket | `gpt-5.5` only |
| Active CWD bucket | `C:/hades` only |

## Thirty-Second Intervals

| Interval | Tokens | Tokens/sec | Cache-aware USD/min | No-cache USD/min | Active threads |
|---:|---:|---:|---:|---:|---:|
| 1 | 2,929,199 | 97,058.97 | USD 3.906 | USD 25.704 | 15 |
| 2 | 2,615,475 | 85,902.14 | USD 3.457 | USD 22.749 | 14 |
| 3 | 758,929 | 25,239.26 | USD 1.016 | USD 6.684 | 7 |
| 4 | 2,037,954 | 67,841.81 | USD 2.730 | USD 17.966 | 8 |
| 5 | 2,592,066 | 86,087.14 | USD 3.465 | USD 22.798 | 12 |

Verdict: the prior low minute did not persist. The sample rebounded immediately to 97.06k and 85.90k tokens/sec, dipped once, then returned above 67k and 86k tokens/sec.

## Concentration

| Slice | Tokens | Share |
|---|---:|---:|
| Top 1 thread | 1,515,599 | 13.86% |
| Top 2 threads | 2,982,556 | 27.28% |
| Top 5 threads | 5,475,000 | 50.07% |
| Top 10 threads | 8,194,707 | 74.95% |
| Top 12 threads | 9,061,692 | 82.88% |

Top active sources:

| Rank | Thread ID | Delta tokens | Title label |
|---:|---|---:|---|
| 1 | `019e2803-e942-74a2-b53e-ad02112a585a` | 1,515,599 | Open batch and find tag |
| 2 | `019e2321-2f60-7fd3-8cd6-31ccbca84ce9` | 1,466,957 | Build Race Condition Hunter |
| 3 | `019e27d6-a009-70e3-8335-8d260d6d1000` | 1,143,677 | Git conflict / push repair |
| 4 | `019e2592-efa1-7562-93d6-f671ff937574` | 708,996 | Implement base hibernation |
| 5 | `019e2310-3a80-7962-849b-5f9327a7141f` | 639,771 | Build outpost save delta sync |
| 6 | `019e2099-6961-71b3-abdc-f8fdb0c1576c` | 611,066 | Improve bot memory and CRM |
| 7 | `019e230e-0e12-7be2-8eb9-39df3a774cc6` | 580,717 | Forge SignalLanes |
| 8 | `019e2098-4883-7440-9d71-44971d6192fd` | 554,625 | Check bot and documentation |
| 9 | `019e2804-348c-75e2-b279-944011058d14` | 503,631 | Find own AGENT_PROMPT |
| 10 | `019e230d-8959-72f2-a88b-5e6576683819` | 469,668 | Contextual UX prompter |
| 11 | `019e2c13-4dd1-7d63-bb08-d78132efdc31` | 438,869 | Sort docs files |
| 12 | `019e285e-2f6d-7313-a7e7-6a9e3a3d670a` | 428,116 | Fix assembly compile wall |

## Verdict

The cooldown was not stable. The honest current status is renewed volatile high burn:

- 66.66k tokens/sec since the 18:31 snapshot.
- 72.48k tokens/sec in the 150-second persistence sample.
- USD 2.917/min cache-aware in the sample.
- USD 19.195/min no-cache equivalent in the sample.
- Top 10 active threads carried 74.95% of the sample.

No waste conviction is made here. The file proves persistence failure and current spend rate only.
