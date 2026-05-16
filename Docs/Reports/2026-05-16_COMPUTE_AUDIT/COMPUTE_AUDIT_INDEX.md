# 2026-05-16 COMPUTE AUDIT INDEX

Status: AUDIT COMPLETE
Snapshot: 2026-05-16T03:56+04:00
Agent: COMPUTE_LOGISTICS_AUDITOR
Scope: HECTON-8 local telemetry and source/docs size.

## Read Order

1. `COMPUTE_AUDIT_BRIEF.md` at repo root - shortest current snapshot.
2. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_TOKEN_BURN_RATE_LEDGER.md` - current token, cost, cadence, LOC, and energy ledger.
3. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_LIVE_DELTA_20260516.md` - post-audit SQLite live burn sample.
4. `Docs/Reports/COMPUTE_DOMINANCE_REPORT.md` - historical report plus appended 2026-05-16 addendum.

## Evidence Boundaries

| Evidence | Used | Meaning |
|---|---:|---|
| `Assets/_Project/Scripts/**/*.cs` | yes | First-party script LOC surface |
| `Docs/AgentLogs`, `Docs/Tasks`, `Docs/Reports` | yes | Local documentation/token proxy surface |
| `C:\Users\danat\.codex\state_5.sqlite` | yes | Fast thread token total and model/cwd split |
| `C:\Users\danat\.codex\sessions/**/*.jsonl` | yes | Final input/cache/output token ledger and rolling rates |
| OpenAI public pricing page | yes | Pricing model assumptions |
| Billing export | no | No invoice-grade proof |
| Unity profiler/runtime validation | no | Not part of compute accounting |

## Non-Negotiable Caveats

- JSONL `token_count` rows repeat cumulative totals. The valid totals here use final per-session usage and positive deltas for rolling windows.
- `reasoning_output_tokens` is treated as a subset of output, not an extra billable line.
- Model pricing for old Codex-specific model IDs remains a proxy bucket where public SKU mapping is ambiguous.
- Long-context surcharge is reported as a scenario. Local session-level totals are not enough to prove every billable request boundary.
- H-Phi/token correlation is still NOT PROVEN. There is no valid join key from local token burn to H-Phi delta.
- "Compute Thief" conviction is still NOT PROVEN. High-burn candidates need diff, LOC delta, compile/test result, and value delta.

