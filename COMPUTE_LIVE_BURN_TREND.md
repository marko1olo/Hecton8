# COMPUTE LIVE BURN TREND

Status: AUDIT COMPLETE
Snapshot: 2026-05-15T17:29:14+04:00
Agent: COMPUTE_LOGISTICS_AUDITOR
Source: `C:\Users\danat\.codex\state_5.sqlite`, table `threads`

## Boundary

This is a live SQLite trend sample, not a full JSONL accounting pass and not an invoice.

Method:
- Start from the corrected rolling-rate snapshot at 2026-05-15T13:18:23.574685Z.
- Query current SQLite `threads.tokens_used`.
- Take three consecutive 60-second thread-delta samples.
- Price live deltas with corrected `gpt-5.5` blended rates from `COMPUTE_CORRECTED_ROLLING_RATES.md`.

## Since Corrected Snapshot

| Metric | Value |
|---|---:|
| Base corrected snapshot UTC | 2026-05-15T13:18:23.574685Z |
| Base SQLite tokens | 45,758,254,570 |
| Current SQLite tokens | 45,817,071,457 |
| Delta since corrected snapshot | 58,816,887 |
| Elapsed since corrected snapshot | 650.720049 seconds |
| Tokens/sec since corrected snapshot | 90,387.39 |
| Tokens/min since corrected snapshot | 5,423,243.41 |
| Tokens/hour since corrected snapshot | 325,394,604.83 |
| Tokens/day equivalent | 7,809,470,515.95 |

This delta is not model-costed directly because the baseline per-thread row snapshot was not preserved. The live three-minute sample below is model-costed.

## Three-Minute Trend

| Interval | Elapsed | Active threads | Tokens | Tokens/sec | Tokens/min | Day equiv tokens | Cache-aware cost | USD/min | USD/hour | USD/day equiv |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 60.240077s | 13 | 2,884,767 | 47,887.84 | 2,873,270.23 | 4,137,509,133.66 | USD 2.21 | USD 2.20 | USD 131.97 | USD 3,167.31 |
| 2 | 60.058366s | 16 | 4,529,639 | 75,420.62 | 4,525,237.00 | 6,516,341,280.41 | USD 3.47 | USD 3.46 | USD 207.85 | USD 4,988.33 |
| 3 | 60.285675s | 11 | 2,819,497 | 46,768.94 | 2,806,136.28 | 4,040,836,248.41 | USD 2.16 | USD 2.15 | USD 128.89 | USD 3,093.30 |
| Total | 180.584118s | 19 | 10,233,903 | 56,671.11 | 3,400,266.79 | 4,896,384,183.69 | USD 7.83 | USD 2.60 | USD 156.18 | USD 3,748.23 |

All observed live sample deltas were `gpt-5.5`. All observed live sample deltas were under `\\?\C:\hades`.

## Concentration

| Slice | Tokens | Share |
|---|---:|---:|
| Top 1 thread | 1,821,461 | 17.80% |
| Top 2 threads | 3,388,464 | 33.11% |
| Top 5 threads | 6,536,418 | 63.87% |
| Top 10 threads | 8,745,897 | 85.46% |
| All 19 active threads | 10,233,903 | 100.00% |

The live burn is not evenly distributed. The top five threads produced almost two thirds of the three-minute sample.

## Top Active Threads

| Rank | Thread ID | Delta tokens | Tokens/sec | Cache cost | Title label |
|---:|---|---:|---:|---:|---|
| 1 | `019e27d6-a009-70e3-8335-8d260d6d1000` | 1,821,461 | 10,086.50 | USD 1.394 | Git conflict / push repair |
| 2 | `019e230d-8959-72f2-a88b-5e6576683819` | 1,567,003 | 8,677.41 | USD 1.200 | Contextual UX prompter |
| 3 | `019e2099-6961-71b3-abdc-f8fdb0c1576c` | 1,133,940 | 6,279.29 | USD 0.868 | Improve bot memory and CRM |
| 4 | `019e2310-3a80-7962-849b-5f9327a7141f` | 1,024,282 | 5,672.05 | USD 0.784 | Build outpost save delta sync |
| 5 | `019e2098-4883-7440-9d71-44971d6192fd` | 989,732 | 5,480.73 | USD 0.758 | Check bot and documentation |
| 6 | `019e230e-0e12-7be2-8eb9-39df3a774cc6` | 597,991 | 3,311.43 | USD 0.458 | Forge SignalLanes |
| 7 | `019e2802-6cfe-7ed1-8f84-6c466293f707` | 454,724 | 2,518.07 | USD 0.348 | Timaert terrain batch |
| 8 | `019e27db-3780-7b80-900a-0aeb9a23f4de` | 391,107 | 2,165.79 | USD 0.299 | Form 10 agent prompts |
| 9 | `019e20d5-88cc-7260-b940-a986d7db8ec5` | 390,823 | 2,164.22 | USD 0.299 | Introduce H-Phi metric |
| 10 | `019e17f5-c75f-7870-a623-4edfef2022a9` | 374,834 | 2,075.68 | USD 0.287 | Check internet through CLI |

## Verdict

The live burn did not cool down. It accelerated in the middle minute and averaged 56.7k tokens/sec across the three-minute sample.

Short-window equivalent burn is about 4.90B tokens/day and USD 3.75k/day cache-aware. That is a live extrapolation, not a stable daily invoice.

STATUS: AUDIT COMPLETE.
