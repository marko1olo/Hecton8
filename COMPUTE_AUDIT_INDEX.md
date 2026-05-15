# COMPUTE AUDIT INDEX

Status: AUDIT COMPLETE
Snapshot: 2026-05-15T16:39+04:00
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
| 8 | `COMPUTE_TOKEN_BURN_RATE_LEDGER.md` | Current rolling token burn, cost/min, cost/hour, cost/day, model/cache split |
| 9 | `COMPUTE_MODEL_BUCKET_RECONCILIATION.md` | Corrected model attribution using path-or-UUID matching |
| 10 | `COMPUTE_LIVE_BURN_SOURCES.md` | Current short-window active thread token deltas |
| 11 | `COMPUTE_RATE_EFFICIENCY_AUDIT.md` | Previous detailed token rates, cache economics, and token/code ratios |
| 12 | `COMPUTE_CODEX_DIALOGUE_AUDIT.md` | `.codex` dialogue/log topology and `logs_2.sqlite` boundaries |
| 13 | `COMPUTE_COLLISION_RISK.md` | Current dirty-tree collision gate |

## Current Hard Boundaries

| Claim | Status |
|---|---|
| HECTON-8 first-party meaningful LOC | 788,619 script LOC |
| Latest JSONL final tokens | 45,652,088,834 |
| Latest live SQLite token mass observed | 45,644,663,325 |
| Model-aware cache-aware corrected estimate | USD 30,613.26 |
| Model-aware no-cache equivalent | USD 201,374.74 |
| All-GPT-5.5 standard cache-aware scenario | USD 35,582.98 |
| All-GPT-5.5 standard no-cache scenario | USD 232,137.91 |
| Unknown final-usage model bucket | 0 tokens after UUID reconciliation |
| Latest last-24h token flow | 3,236,618,901 tokens; USD 1,039.59 cache-aware |
| Latest post-scan SQLite tail delta | +102,151,525 tokens; 27,749.37 tokens/sec |
| Latest 90s active-source sample | 2,725,800 tokens; 30,099.39 tokens/sec; 11 active threads |
| Top-100 thread share | 49.752% at 03:42 snapshot |
| Top-30 validation non-zero outputs | 2,374 |
| Reliable test-success evidence in top-30 validation scan | 0 |
| Top weighted file target | `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` |
| Current dirty hot intersections | `SargassumMicroFaunaBoids.cs`, `HabitatGraphManager.cs` |
| C++ transfer evidence in top-100 patch targets | 0 hits; NOT VERIFIED / NO PATCH EVIDENCE |
| Latest last-6h token flow | 24,429.23 tokens/sec |
| Latest tokens per meaningful script LOC | 57,888.65 |
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
