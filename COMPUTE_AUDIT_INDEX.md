# COMPUTE AUDIT INDEX

Status: AUDIT COMPLETE
Snapshot: 2026-05-15T17:43+04:00
Agent: COMPUTE_LOGISTICS_AUDITOR

## Read Order

| Order | File | Purpose |
|---:|---|---|
| 1 | `COMPUTE_AUDIT_BRIEF.md` | Short hard-number snapshot and evidence rules |
| 2 | `Docs/Reports/COMPUTE_DOMINANCE_REPORT.md` | Full detailed report |
| 3 | `COMPUTE_THREAD_TRIAGE.md` | Top-heavy thread concentration and top-30 queue |
| 4 | `COMPUTE_THREAD_ATTRIBUTION.md` | Top-30 visible patch/tool attribution |
| 5 | `COMPUTE_VALIDATION_FORENSICS.md` | Top-30 historical validation attempts and failure signals |
| 6 | `COMPUTE_THREAD_VALUE_AUDIT.md` | Top-100 work-trace/value/collision/C++ evidence audit |
| 7 | `COMPUTE_FILE_BURN_ATTRIBUTION.md` | Weighted token burn by patch target |
| 8 | `COMPUTE_CORRECTED_ROLLING_RATES.md` | Corrected rolling token burn, cost/min, cost/hour, cost/day with UUID model matching |
| 9 | `COMPUTE_TOKEN_BURN_RATE_LEDGER.md` | Previous rolling token burn ledger; superseded for window costs |
| 10 | `COMPUTE_MODEL_BUCKET_RECONCILIATION.md` | Corrected model attribution using path-or-UUID matching |
| 11 | `COMPUTE_LIVE_BURN_5MIN_FORECAST.md` | Current five-minute live burn, stop-loss projection, and concentration |
| 12 | `COMPUTE_LIVE_BURN_TREND.md` | Previous three-minute live burn trend and concentration |
| 13 | `COMPUTE_LIVE_BURN_SOURCES.md` | Previous short-window active thread token deltas |
| 14 | `COMPUTE_RATE_EFFICIENCY_AUDIT.md` | Previous detailed token rates, cache economics, and token/code ratios |
| 15 | `COMPUTE_CODEX_DIALOGUE_AUDIT.md` | `.codex` dialogue/log topology and `logs_2.sqlite` boundaries |
| 16 | `COMPUTE_COLLISION_RISK.md` | Current dirty-tree collision gate |

## Current Hard Boundaries

| Claim | Status |
|---|---|
| HECTON-8 first-party meaningful LOC | 788,619 script LOC |
| Latest JSONL final tokens | 45,771,499,116 |
| Latest live SQLite token mass observed | 45,857,878,991 |
| Model-aware cache-aware corrected estimate | USD 30,704.36 |
| Model-aware no-cache equivalent | USD 201,983.02 |
| All-GPT-5.5 standard cache-aware scenario | USD 35,674.08 |
| All-GPT-5.5 standard no-cache scenario | USD 232,746.18 |
| Unknown final-usage model bucket | 0 tokens after UUID reconciliation |
| Latest corrected last-24h token flow | 3,398,780,549 tokens; USD 2,601.80 cache-aware |
| Latest post-scan SQLite tail delta | +102,151,525 tokens; 27,749.37 tokens/sec |
| Latest 90s active-source sample | 2,725,800 tokens; 30,099.39 tokens/sec; 11 active threads |
| Latest 3-minute live trend | 10,233,903 tokens; 56,671.11 tokens/sec; USD 2.60/min |
| Latest 5-minute live trend | 16,694,405 tokens; 55,562.22 tokens/sec; USD 2.236/min |
| Latest 5-minute no-cache equivalent | USD 14.714/min; USD 21,188.82/day |
| Latest 5-minute top-10 concentration | 12,444,535 tokens; 74.54% |
| Top-100 thread share | 49.752% at 03:42 snapshot |
| Top-30 validation non-zero outputs | 2,374 |
| Reliable test-success evidence in top-30 validation scan | 0 |
| Top weighted file target | `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` |
| Current dirty hot intersections | `SargassumMicroFaunaBoids.cs`, `HabitatGraphManager.cs` |
| C++ transfer evidence in top-100 patch targets | 0 hits; NOT VERIFIED / NO PATCH EVIDENCE |
| Latest corrected last-6h token flow | 39,149.01 tokens/sec |
| Latest tokens per meaningful script LOC | 58,040.07 |
| `.codex` JSONL dialogue lines | 2,410,138 |
| `.codex` user role markers | 14,473 |
| `.codex` function-call markers | 518,303 |
| `.codex/logs_2.sqlite` rows | 474,415 |
| `logs_2.sqlite` exact-1000-row thread cap hits | 298 threads |

## Do Not Misstate

- Do not call any high-burn thread `waste` without final diff, meaningful LOC delta, compile/test result, and quality delta.
- Do not call any thread `verified value`; current evidence is work trace plus partial validation history.
- Do not claim current compile status from historical rollout logs.
- Do not claim C++ migration progress from token volume. The top-100 patch target scan found zero C++ targets.
- Do not revert dirty runtime files from this audit agent.

## Next Honest Gate

Wait for runtime agents to pause, then run one integration compile/test pass and bind every failure to:
1. file;
2. owning agent/status file;
3. last relevant thread ID;
4. current diff;
5. expected fix path.
