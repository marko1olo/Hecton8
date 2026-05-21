# COMPUTE H-PHI LIVE REBASE 2026-05-17 15:39

Status: AUDIT COMPLETE
Agent: COMPUTE_LOGISTICS_AUDITOR
Scope: HECTON-8 only. Timaert excluded.
Evidence class: STATIC_SOURCE + JSONL + SQLITE + CALC. No Unity runtime, profiler, GCMonitor, billing export, or playmode proof.
Search keywords: H-Phi; HPhi; hphi; ash-fi; ash_phi; ASh-Fi; HФ; Аш-Фи; integration-metric; architecture-integration; token-H-Phi-ROI; compute-H-Phi.

## Why This Rebase Exists

The 13:37 H-Phi artifact became stale after another source drift: 46 C# files changed after 13:37 and 3,210,412 bytes moved. A fresh scan was justified. This was not a blind rerun.

Raw artifact: `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_CURRENT_20260517_1539.json`.

Scan timestamp: `2026-05-17 15:39:53 +04:00`.

H-Phi scan time: 52,698 ms.

## Current Source Surface

| Metric | Value |
|---|---:|
| First-party C# files | 1,585 |
| Physical LOC | 1,037,942 |
| Blank LOC | 138,308 |
| Comment-only LOC | 42,407 |
| Meaningful LOC | 857,227 |
| Script bytes | 46,338,967 |
| Logic density | 82.5891% |

## H-Phi Scores

| Metric | 13:37 | 15:39 | Delta |
|---|---:|---:|---:|
| Runtime H-Phi risk | 0.005525762 | 0.005580503 | +0.000054741 |
| Runtime H-Phi narrow | 0.077385732 | 0.077988159 | +0.000602427 |
| All-source H-Phi risk | 0.004599421 | 0.004643708 | +0.000044287 |
| All-source H-Phi narrow | 0.069576516 | 0.070100340 | +0.000523824 |
| Risk integration | 0.071405440 | 0.071555771 | +0.000150331 |
| Architectural purity | 1.000000000 | 1.000000000 | 0 |
| Data sovereignty | 0.144331092 | 0.145138727 | +0.000807635 |
| Memory alignment | 0.536168133 | 0.537335286 | +0.001167153 |
| Binary-safe ratio | 0.021994135 | 0.021961933 | -0.000032202 |
| AUP precision integrity | 1.000000000 | 1.000000000 | 0 |

## Counter Deltas Vs 13:37

| Counter | Delta |
|---|---:|
| Runtime files | +1 |
| Runtime lines | +285 |
| SignalBus push surface | +1 |
| GlobalRegistry surface | 0 |
| Event publish surface | 0 |
| DataVault refs | 0 |
| NativeArray refs | -48 |
| Struct declarations | +3 |
| StructLayout attributes | +4 |
| GetComponent calls | 0 |
| Dispose calls | -8 |
| LINQ surface | 0 |
| Managed format surface | 0 |
| Job complete surface | 0 |
| Primary managed runtime risk | 0 |
| Primary job-complete risk | 0 |
| Owner-blocked NativeArray refs | -39 |
| Owner-blocked Dispose calls | -7 |
| Native ownership risk | -53 |
| Primary owner-blocked NativeArray refs | -31 |
| Primary owner-blocked Dispose calls | -5 |
| Primary native ownership risk | -41 |

Interpretation: this interval is cleaner than 13:37. Managed format and primary managed runtime risk did not grow. Native ownership risk improved. The score lift is small.

## Corrected Token Window 13:37 To 15:39

Window: `2026-05-17T13:37:29+04:00` to `2026-05-17T15:39:53+04:00`.

The first parser pass summed `last_token_usage` rows and produced 466,464,890 tokens. That overcounts local cumulative telemetry. The accepted number below uses per-thread cumulative `total_token_usage` deltas with the pre-window baseline, which cross-checks against SQLite movement.

| Metric | Value |
|---|---:|
| Duration | 7,344 sec |
| JSONL files scanned | 36 |
| JSONL bytes scanned | 543,013,394 |
| JSONL rows scanned | 195,608 |
| Token rows in window | 2,964 |
| Usable token delta rows | 2,964 |
| User prompt rows | 26 |
| Parse errors | 0 |
| Dominant model | `gpt-5.5` |
| Input tokens | 212,526,539 |
| Cached input tokens | 207,817,344 |
| Output tokens | 594,824 |
| Reasoning output tokens | 149,203 |
| Total tokens | 213,121,363 |
| Cached-input ratio | 97.7842% |
| Cache-aware cost | USD 145.30 |
| No-cache equivalent | USD 1,080.48 |
| Cache avoided | USD 935.18 |
| Long-context events over 272K input | 0 |

## Cadence

| Metric | Value |
|---|---:|
| Average tokens/sec | 29,019.79 |
| Average tokens/min | 1,741,187.61 |
| Average tokens/hour | 104,471,256.37 |
| Cache-aware USD/sec | USD 0.0198 |
| Cache-aware USD/min | USD 1.19 |
| Cache-aware USD/hour | USD 71.20 |
| Cache-aware USD/day equivalent | USD 1,710.09 |
| No-cache USD/min | USD 8.83 |
| No-cache USD/hour | USD 529.65 |
| No-cache USD/day equivalent | USD 12,711.69 |
| Peak token second | 853,637 at 2026-05-17T14:44:04+04:00 |
| Peak token minute | 8,310,130 at 2026-05-17T14:46+04:00 |
| Peak token hour | 130,050,540 at 2026-05-17T14:00+04:00 |
| Prompt peak minute | 4 at 2026-05-17T14:44+04:00 |
| Prompt peak hour | 10 at 2026-05-17T14:00+04:00 |

## SQLite Live Pulse 15:38

| Metric | Value |
|---|---:|
| Start | 2026-05-17T15:38:08+04:00 |
| End | 2026-05-17T15:38:38+04:00 |
| Duration | 30.013464 sec |
| Start total | 51,584,663,043 |
| End total | 51,584,774,822 |
| Delta | 111,779 |
| Tokens/sec | 3,724.30 |
| Tokens/min | 223,457.71 |
| Tokens/hour | 13,407,462.73 |
| Tokens/day equivalent | 321,779,105.54 |
| Active delta threads | 1 |
| Effective cache-aware USD/min, using 13:37-15:39 blend | USD 0.15 |
| Effective no-cache USD/min | USD 1.13 |

Top live burner:

| Rank | Thread title | Delta tokens |
|---:|---|---:|
| 1 | Архивируй BATCH007 и слей доки | 111,779 |

## Current Cumulative State

| Metric | Value |
|---|---:|
| SQLite total tokens | 51,586,452,098 |
| Delta vs 13:37 SQLite total | +214,267,317 |
| Estimated energy at 0.05 kWh / 1K tokens | 2,579.32 MWh |
| Energy in GWh | 2.5793 GWh |
| Tokens per meaningful LOC | 60,178.29 |
| Tokens per script byte | 1,113.24 |
| Current first-party meaningful LOC | 857,227 |
| Current script bytes | 46,338,967 |

## Marginal H-Phi ROI

| Interval | Tokens | Cache-aware USD | Risk delta | Tokens / +0.001 risk | USD / +0.001 risk | Narrow delta | Tokens / +0.001 narrow | USD / +0.001 narrow |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 13:37 -> 15:39 | 213,121,363 | USD 145.30 | +0.000054741 | 3,893,267,624 | USD 2,654.31 | +0.000602427 | 353,771,267 | USD 241.19 |
| Cumulative 2026-05-15 22:46 -> 15:39 | 6,085,586,690 | USD 4,694.31 | +0.004944412 | 1,230,800,890 | USD 949.42 | +0.067200720 | 90,558,355 | USD 69.86 |

The marginal risk-score ROI worsened again. The cleaner counter movement did not buy much score. That is a plateau warning, not a waste conviction.

## Top Token Threads In Window

| Rank | Thread title | Tokens |
|---:|---|---:|
| 1 | Add sensory input to boid shader | 17,943,143 |
| 2 | Build hull repair engine | 16,177,264 |
| 3 | Fix ASMDEF graph | 14,988,237 |
| 4 | Add acoustic echo navigation | 13,741,474 |
| 5 | Automate H8Memory lifecycle | 13,229,863 |
| 6 | Enforce DataVault statelessness | 11,021,954 |
| 7 | Add wake displacement | 10,126,942 |
| 8 | Sync flora bioluminescence pulses | 9,982,860 |
| 9 | Standardize SignalBus lanes | 9,743,107 |
| 10 | Implement SDF gap traversal | 9,216,807 |
| 11 | Improve bot memory and CRM | 8,057,809 |
| 12 | ARCHITECT_SPATIAL_PROBE prompt thread | 7,401,322 |
| 13 | Analyze compute token costs | 6,564,492 |
| 14 | CONTRACT_AUTHORITY_SURGEON prompt thread | 5,635,148 |
| 15 | Build drifting compass | 4,968,603 |

These are burn contributors, not waste convictions.

## Verdict

Latest Runtime H-Phi risk is 0.005580503. Latest Runtime H-Phi narrow is 0.077988159. Current local ledger is 51.586B SQLite tokens and 2,579.32 MWh by the audit constant.

The 15:39 interval is quieter than the 13:37 interval in average cost, cleaner in managed-risk counters, but worse in marginal H-Phi ROI. The integration metric is still moving; the score movement per token is now weak.

STATUS: AUDIT COMPLETE.
