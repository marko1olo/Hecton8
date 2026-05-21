# COMPUTE H-PHI LIVE REBASE 2026-05-17 13:37

Status: AUDIT COMPLETE
Agent: COMPUTE_LOGISTICS_AUDITOR
Scope: HECTON-8 only. Timaert excluded.
Evidence class: STATIC_SOURCE + JSONL + SQLITE + CALC. No Unity runtime, profiler, GCMonitor, billing export, or playmode proof.
Pricing reference: official OpenAI API pricing page, `https://openai.com/api/pricing/`, checked for cached-input/current-model accounting.
Search keywords: H-Phi; HPhi; hphi; ash-fi; ash_phi; ASh-Fi; HФ; Аш-Фи; integration-metric; architecture-integration; token-H-Phi-ROI; compute-H-Phi.

## Why This Rebase Exists

The 11:42 H-Phi artifact became stale quickly. Source drift after 11:42 touched 102 C# files and 8,481,368 bytes. A new static H-Phi scan was justified by source movement, not by prompt pressure.

Raw artifact: `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_CURRENT_20260517_1327.json`.

Scan timestamp: `2026-05-17 13:37:29 +04:00`.

H-Phi scan time: 82,422 ms.

## Current Source Surface

| Metric | Value |
|---|---:|
| First-party C# files | 1,585 |
| Physical LOC | 1,037,644 |
| Blank LOC | 138,278 |
| Comment-only LOC | 42,426 |
| Meaningful LOC | 856,940 |
| Script bytes | 46,331,767 |
| Logic density | 82.5852% |

## H-Phi Scores

| Metric | 11:42 | 13:37 | Delta |
|---|---:|---:|---:|
| Runtime H-Phi risk | 0.005378664 | 0.005525762 | +0.000147098 |
| Runtime H-Phi narrow | 0.075881112 | 0.077385732 | +0.001504620 |
| All-source H-Phi risk | 0.004467625 | 0.004599421 | +0.000131796 |
| All-source H-Phi narrow | 0.068247520 | 0.069576516 | +0.001328996 |
| Data sovereignty | 0.141543476 | 0.144331092 | +0.002787616 |
| Memory alignment | 0.536097561 | 0.536168133 | +0.000070572 |
| Binary-safe ratio | 0.021463415 | 0.021994135 | +0.000530720 |
| Risk integration | n/a | 0.071405440 | n/a |
| Architectural purity | n/a | 1.000000000 | n/a |
| AUP precision integrity | n/a | 1.000000000 | n/a |

## Counter Deltas Vs 11:42

| Counter | Delta |
|---|---:|
| Runtime files | -1 |
| Runtime lines | +1,843 |
| SignalBus push surface | +5 |
| GlobalRegistry surface | +21 |
| Event publish surface | -1 |
| DataVault refs | +29 |
| NativeArray refs | +6 |
| Struct declarations | -4 |
| StructLayout attributes | -2 |
| GetComponent calls | 0 |
| Dispose calls | -9 |
| LINQ surface | 0 |
| Managed format surface | +6 |
| Job complete surface | 0 |
| Primary managed runtime risk | +6 |
| Primary job-complete risk | 0 |
| Owner-blocked NativeArray refs | -20 |
| Owner-blocked Dispose calls | -7 |
| Native ownership risk | -34 |
| Primary owner-blocked NativeArray refs | -16 |
| Primary owner-blocked Dispose calls | -6 |
| Primary native ownership risk | -28 |

Interpretation: the score improved, but not cleanly. DataVault and ownership debt moved in the right direction. GlobalRegistry surface and managed format debt still grew. Composite improvement is not a free pass.

## Token Window 11:42 To 13:37

Window: `2026-05-17T11:41:52+04:00` to `2026-05-17T13:37:29+04:00`.

| Metric | Value |
|---|---:|
| Duration | 6,937 sec |
| JSONL files scanned | 45 |
| JSONL bytes scanned | 614,361,607 |
| JSONL rows scanned | 220,091 |
| Usable token rows | 1,956 |
| User prompt rows | 57 |
| Parse errors | 0 |
| Dominant model | `gpt-5.5` |
| Input tokens | 303,607,513 |
| Cached input tokens | 291,169,792 |
| Output tokens | 955,019 |
| Reasoning output tokens | 283,542 |
| Total tokens | 304,562,532 |
| Cached-input ratio | 95.9034% |
| Cache-aware cost | USD 236.42 |
| No-cache equivalent | USD 1,546.69 |
| Cache avoided | USD 1,310.26 |
| Long-context events over 272K input | 0 |

## Cadence

| Metric | Value |
|---|---:|
| Average tokens/sec | 43,904.07 |
| Average tokens/min | 2,634,244.19 |
| Average tokens/hour | 158,054,651.17 |
| Cache-aware USD/sec | USD 0.0341 |
| Cache-aware USD/min | USD 2.04 |
| Cache-aware USD/hour | USD 122.69 |
| Cache-aware USD/day equivalent | USD 2,944.65 |
| No-cache USD/min | USD 13.38 |
| No-cache USD/hour | USD 802.66 |
| No-cache USD/day equivalent | USD 19,263.93 |
| Peak token second | 1,086,803 at 2026-05-17T11:48:43+04:00 |
| Peak token minute | 11,257,896 at 2026-05-17T11:52+04:00 |
| Peak token hour | 196,510,763 at 2026-05-17T12:00+04:00 |
| Prompt peak minute | 3 at 2026-05-17T13:36+04:00 |
| Prompt peak hour | 23 at 2026-05-17T12:00+04:00 |

## SQLite Live Pulse 13:36

| Metric | Value |
|---|---:|
| Start | 2026-05-17T13:36:08+04:00 |
| End | 2026-05-17T13:36:38+04:00 |
| Duration | 30.069926 sec |
| Start total | 51,371,443,098 |
| End total | 51,372,184,781 |
| Delta | 741,683 |
| Tokens/sec | 24,665.28 |
| Tokens/min | 1,479,916.51 |
| Tokens/hour | 88,794,990.72 |
| Tokens/day equivalent | 2,131,079,777.18 |
| Active delta threads | 5 |
| Effective cache-aware USD/min, using 11:42-13:37 blend | USD 1.15 |
| Effective cache-aware USD/hour | USD 68.93 |
| Effective cache-aware USD/day equivalent | USD 1,654.30 |

Top live burners:

| Rank | Thread title | Delta tokens |
|---:|---|---:|
| 1 | Integrate caustics rust fog shader | 198,305 |
| 2 | Build jaw and tentacle IK | 183,977 |
| 3 | Standardize SignalBus lanes | 132,695 |
| 4 | Fix ASMDEF graph | 126,771 |
| 5 | Implement SDF gap traversal | 99,935 |

## Current Cumulative State

| Metric | Value |
|---|---:|
| SQLite total tokens | 51,372,184,781 |
| Estimated energy at 0.05 kWh / 1K tokens | 2,568.61 MWh |
| Energy in GWh | 2.5686 GWh |
| Tokens per meaningful LOC | 59,948.40 |
| Tokens per script byte | 1,108.79 |
| Current first-party meaningful LOC | 856,940 |
| Current script bytes | 46,331,767 |

Model split at SQLite live end:

| Model | Threads | Tokens |
|---|---:|---:|
| `gpt-5.5` | 537 | 39,487,989,635 |
| `gpt-5.4` | 244 | 11,591,437,853 |
| `gpt-5.4-mini` | 25 | 192,533,099 |
| `gpt-5.2-codex` | 3 | 85,512,992 |
| `gpt-5.1-codex-mini` | 3 | 13,472,930 |
| `gpt-5.3-codex` | 3 | 1,096,113 |
| `gpt-5.2` | 3 | 142,159 |

## Marginal H-Phi ROI

| Interval | Tokens | Cache-aware USD | Risk delta | Tokens / +0.001 risk | USD / +0.001 risk | Narrow delta | Tokens / +0.001 narrow | USD / +0.001 narrow |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 11:42 -> 13:37 | 304,562,532 | USD 236.42 | +0.000147098 | 2,070,473,643 | USD 1,607.26 | +0.001504620 | 202,418,240 | USD 157.13 |
| Cumulative 2026-05-15 22:46 -> 13:37 | 5,872,465,327 | USD 4,549.01 | +0.004889671 | 1,200,993,958 | USD 930.33 | +0.066598293 | 88,177,415 | USD 68.31 |

The latest interval is better than the 02:17-04:12 plateau, worse than the 04:12-11:42 recovery, and still expensive. The score keeps moving because large source edits keep happening. The managed debt counters prevent a clean victory claim.

## Top Token Threads In Window

| Rank | Thread title | Tokens |
|---:|---|---:|
| 1 | Automate H8Memory lifecycle | 13,499,549 |
| 2 | CONTENT_AUTHORITY_DICTATOR prompt thread | 12,998,339 |
| 3 | Implement Beer-Lambert shader | 11,272,835 |
| 4 | Add spline docking autopilot | 10,985,331 |
| 5 | Add wake displacement | 10,808,468 |
| 6 | Build memory visualizer | 10,732,032 |
| 7 | Add modulo time slicer | 10,566,913 |
| 8 | Add sensory input to boid shader | 10,564,373 |
| 9 | ARCHITECT_SPATIAL_PROBE prompt thread | 10,104,892 |
| 10 | AUDIO_IMPORT_RESIDENCY_GUARD prompt thread | 10,007,315 |
| 11 | Implement 300-frame state hashing | 9,620,649 |
| 12 | Build jaw and tentacle IK | 9,114,142 |
| 13 | Fix jitter with double3 math | 9,003,978 |
| 14 | Standardize SignalBus lanes | 8,730,383 |
| 15 | Build thermal battery manager | 8,522,153 |

These are high-burn contributors, not waste convictions. A "Compute Thief" label still requires joined diff, LOC, H-Phi/value delta, and validation evidence.

## Verdict

H-Phi improved again: Runtime risk moved from 0.005378664 to 0.005525762, and Runtime narrow from 0.075881112 to 0.077385732. This is real static-source movement. It is also mixed movement: ownership counters improved while GlobalRegistry and managed format surfaces grew.

Token burn for the interval was 304.56M tokens and USD 236.42 cache-aware. Current cumulative local ledger is 51.372B tokens, 2,568.61 MWh by the audit constant, and 59,948 tokens per meaningful LOC.

STATUS: AUDIT COMPLETE.
