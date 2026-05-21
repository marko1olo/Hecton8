# COMPUTE BURN TRAJECTORY LEDGER

Status: AUDIT COMPLETE / LIVE LEDGER MOVING
Snapshot: 2026-05-15T18:16:42+04:00
Agent: COMPUTE_LOGISTICS_AUDITOR
Source: `C:\Users\danat\.codex\state_5.sqlite`, table `threads`

## Boundary

This file does not replace `COMPUTE_CORRECTED_ROLLING_RATES.md`. It tracks short local trajectory between fixed audit snapshots.

SQLite stores cumulative `tokens_used` by thread. It does not expose input/cached-input/output split for the delta. Segment costs use the corrected global blends from the UUID-reconciled JSONL scan:

| Rate | Value |
|---|---:|
| Cache-aware blended rate | USD 0.670788 / 1M tokens |
| No-cache blended equivalent | USD 4.413807 / 1M tokens |

Official OpenAI pricing page was rechecked on 2026-05-15 before this pass. This is still not a billing export.

## Current Snapshot

| Metric | Value |
|---|---:|
| Snapshot local time | 2026-05-15T18:16:42.928513+04:00 |
| SQLite thread count | 766 |
| Current SQLite total tokens | 45,946,566,942 |
| Corrected JSONL final tokens, 17:18 scan | 45,771,499,116 |
| Delta beyond corrected JSONL final | 175,067,826 |
| Estimated live cost beyond corrected JSONL final | USD 117.43 |
| Current live cost estimate | USD 30,821.79 |
| Prompt-constant energy equivalent | 2,297.33 MWh |
| Live tokens per meaningful script LOC | 58,262.06 |
| Live tokens per script source byte | 1,092.202 |

Current cumulative model split in SQLite:

| Model | Tokens | Share |
|---|---:|---:|
| `gpt-5.5` | 34,062,371,796 | 74.14% |
| `gpt-5.4` | 11,591,437,853 | 25.23% |
| `gpt-5.4-mini` | 192,533,099 | 0.42% |
| Other known models | 100,224,194 | 0.22% |

## Fixed Snapshot Trajectory

| Segment | Seconds | Delta tokens | Tokens/sec | Tokens/min | Tokens/hour equiv | Tokens/day equiv | Cache-aware cost | USD/min | No-cache cost | No-cache USD/min |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 17:18 corrected -> 17:29 live3 | 650.425315 | 58,816,887 | 90,428.35 | 5,425,700.90 | 325,542,054.28 | 7,813,009,302.69 | USD 39.45 | USD 3.639 | USD 259.61 | USD 23.948 |
| 17:29 live3 -> 17:43 live5 | 836.616907 | 40,807,534 | 48,776.85 | 2,926,610.76 | 175,596,645.45 | 4,214,319,490.92 | USD 27.37 | USD 1.963 | USD 180.12 | USD 12.917 |
| 17:43 live5 -> 18:16 instant | 2,012.311606 | 88,687,951 | 44,072.67 | 2,644,360.37 | 158,661,622.11 | 3,807,878,930.66 | USD 59.49 | USD 1.774 | USD 391.45 | USD 11.672 |
| 17:18 -> 18:16 combined | 3,499.353828 | 188,312,372 | 53,813.47 | 3,228,808.20 | 193,728,491.75 | 4,649,483,802.01 | USD 126.32 | USD 2.166 | USD 831.17 | USD 14.251 |

## Interpretation

The live tail cooled from the 17:18 -> 17:29 spike, but it did not stop.

| Window | Rate |
|---|---:|
| Spike window | 90,428.35 tokens/sec |
| Five-minute window | 55,562.22 tokens/sec |
| Post-forecast tail | 44,072.67 tokens/sec |
| Combined 58.32-minute window | 53,813.47 tokens/sec |

The short-window trend moved downward, but the combined rate still projects to 4.65B tokens/day if sustained.

## Current Absolute Heavy Threads

These are cumulative heavy threads, not live deltas.

| Rank | Thread ID | Tokens | Model | Title label |
|---:|---|---:|---|---|
| 1 | `019e1859-0e01-77b2-a8c6-b5586ccc5c8c` | 518,697,166 | `gpt-5.5` | Fix console and UI |
| 2 | `019d6329-de82-74e2-83ca-450539a61cec` | 490,407,394 | `gpt-5.4` | Hecton8 master plan / build-playtest work |
| 3 | `019dde7c-df90-7791-b4b4-d49c8450a9be` | 468,267,072 | `gpt-5.5` | Split monoliths into services |
| 4 | `019d67a6-6823-7b82-94f9-a3167b8e0286` | 429,064,399 | `gpt-5.4` | Continue Hecton8 work |
| 5 | `019dcf19-407b-75f2-99e4-54d0217d9d14` | 408,633,638 | `gpt-5.4` | Fix C# compile blockers |
| 6 | `019dfc26-b869-7bf3-a254-de3f0a8111e9` | 349,084,791 | `gpt-5.5` | Add basin detection engine |
| 7 | `019def23-b6e4-7d72-9992-a10a17f0d7db` | 340,869,732 | `gpt-5.5` | Greeting thread |
| 8 | `019dfd9c-337f-7842-81b5-e4b862462b87` | 333,924,928 | `gpt-5.5` | Wire quest progression |
| 9 | `019dda15-a011-7a12-a62c-1bc748a269a3` | 310,515,372 | `gpt-5.5` | Xeno-botany assets prompt |
| 10 | `019dda14-db04-74b0-91a0-e1088c40bc88` | 308,909,822 | `gpt-5.5` | Add procedural flora distribution |

These cumulative top threads are the historical mass center. They are not automatically the current live burners.

## Verdict

The ledger is still moving at industrial scale. The current honest number is not "runaway spike"; it is "sustained high tail":

- 44.07k tokens/sec after the five-minute forecast window.
- 53.81k tokens/sec across the last 58.32 minutes.
- USD 1.774/min cache-aware in the post-forecast tail.
- USD 11.672/min no-cache equivalent in the post-forecast tail.

No waste conviction is made here. This file only proves trajectory and spend rate.
