# COMPUTE AUDIT INDEX

Status: AUDIT COMPLETE
Snapshot: 2026-05-15T04:00+04:00
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
| 8 | `COMPUTE_RATE_EFFICIENCY_AUDIT.md` | Latest token rates, cache economics, and token/code ratios |
| 9 | `COMPUTE_COLLISION_RISK.md` | Current dirty-tree collision gate |

## Current Hard Boundaries

| Claim | Status |
|---|---|
| HECTON-8 first-party meaningful LOC | 775,435 script LOC |
| Latest JSONL final tokens | 44,590,504,461 |
| Latest live SQLite token mass observed | 44,567,638,432 |
| Model-aware cache-aware lower-bound estimate | USD 28,860.62 |
| All-GPT-5.5 standard cache-aware scenario | USD 34,755.89 |
| All-GPT-5.5 standard no-cache scenario | USD 226,732.30 |
| Top-100 thread share | 49.752% at 03:42 snapshot |
| Top-30 validation non-zero outputs | 2,374 |
| Reliable test-success evidence in top-30 validation scan | 0 |
| Top weighted file target | `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` |
| Current dirty hot intersections | `SargassumMicroFaunaBoids.cs`, `HabitatGraphManager.cs` |
| C++ transfer evidence in top-100 patch targets | 0 hits; NOT VERIFIED / NO PATCH EVIDENCE |
| Latest last-6h token flow | 97,652.24 tokens/sec |
| Latest tokens per meaningful script LOC | 57,503.86 |

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
