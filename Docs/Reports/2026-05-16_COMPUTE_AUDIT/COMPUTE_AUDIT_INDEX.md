# 2026-05-16 COMPUTE AUDIT INDEX

Status: AUDIT COMPLETE
Snapshot: 2026-05-16T03:56+04:00
Agent: COMPUTE_LOGISTICS_AUDITOR
Scope: HECTON-8 local telemetry and source/docs size.

## Read Order

1. `COMPUTE_AUDIT_BRIEF.md` at repo root - shortest current snapshot.
2. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_TOKEN_BURN_RATE_LEDGER.md` - current token, cost, cadence, LOC, and energy ledger.
3. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_LIVE_DELTA_20260516.md` - post-audit SQLite live burn sample.
4. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_LAST6H_PROMPT_TOKEN_AUDIT.md` - recent six-hour JSONL token/prompt cadence.
5. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_RECENT_JSONL_RATE_AUDIT_20260517.md` - bounded 30h/24h/6h/1h rate audit and 00:52 live pulse.
6. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_LIVE_PULSE_20260517_0534.md` - latest SQLite live pulse and active burners.
7. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_TOKEN_LIVE_REBASE_20260517_0446.md` - post-H-Phi token/live-rate rebase.
8. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_LIVE_REBASE_20260517_0412.md` - latest H-Phi score/counter/token rebase.
9. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_LIVE_REBASE_20260517_0217.md` - prior H-Phi score/counter/token rebase.
10. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_TOKEN_CORRELATION_20260516.md` - H-Phi source score and token correlation.
11. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_BUDGET_GATE_ATTEMPT_20260517.md` - strict H-Phi baseline gate attempt and timeout boundary.
12. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_LOG_DB_AUDIT.md` - Codex log DB size/noise audit.
13. `Docs/Reports/COMPUTE_DOMINANCE_REPORT.md` - historical report plus appended continuation addenda.

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
- H-Phi/token correlation is now measured from local artifacts, but causality is NOT PROVEN. Artifact timestamps are sparse and repeated budget reruns can bias the series.
- "Compute Thief" conviction is still NOT PROVEN. High-burn candidates need diff, LOC delta, compile/test result, and value delta.

## Continuation Notes

2026-05-16T14:57+04:00 live rebase was appended to the brief, live delta, token ledger, and log DB audit.

Current live total: 48,761,315,725 SQLite thread tokens. Current first-party script surface: 827,838 meaningful LOC. Current estimated cache-aware total: USD 33,007.19. Current energy estimate: 2,438.07 MWh.

Last 6h JSONL check: 757,394,868 tokens, 95.599% cached input, USD 607.01 cache-aware, USD 3,853.78 no-cache equivalent, peak minute 15,133,220 tokens.

H-Phi continuation: current Runtime H-Phi risk 0.004164939, Runtime H-Phi narrow 0.060806118, Data sovereignty 0.114950891. Versus the 2026-05-15T22:46 baseline, token spend was 2,464,254,349 and cache-aware cost was USD 1,947.70. Local artifact correlation: tokens vs Runtime H-Phi risk r=0.522.

Midnight continuation: 49,903,844,533 SQLite thread tokens at 2026-05-16T23:59+04:00. Latest 45-second pulse: 4,829,772 tokens, 107,328.27 tokens/sec. Current first-party script surface: 836,249 meaningful LOC. Strict H-Phi baseline budget command timed out after 244 seconds and produced no completed gate artifact.

Recent 2026-05-17T00:52+04:00 audit: bounded 30h JSONL pass over 81 files / 991,426,469 bytes found 5,364,091,619 tokens in the last 24h, USD 4,123.40 cache-aware, USD 27,223.44 no-cache, 96.211% cached input, and 0 long-context surcharge events over 272K input. SQLite live pulse at 00:52: 50,027,664,742 total tokens, 2,659,344 tokens in 30 seconds, 88,644.80 tokens/sec. Current first-party script surface: 836,910 meaningful LOC, 59,776.64 tokens per meaningful LOC.

H-Phi live rebase 2026-05-17T02:17+04:00: Runtime H-Phi risk 0.004847023, Runtime H-Phi narrow 0.070058393, Data sovereignty 0.131794933, Memory alignment 0.531571219. Versus 17:18 artifact: +0.000682084 risk, +0.009252275 narrow, +160 DataVault refs, -123 owner-blocked NativeArray refs. Token window between H-Phi artifacts: 2,183,475,652 tokens, USD 1,640.89 cache-aware, USD 11,075.08 no-cache. Current SQLite live total: 50,313,194,499 tokens. Current meaningful LOC: 837,628; tokens per meaningful LOC: 60,066.28.

H-Phi live rebase 2026-05-17T04:12+04:00: Runtime H-Phi risk 0.004858813, Runtime H-Phi narrow 0.070286230, Data sovereignty 0.132223543. Versus 02:17 artifact: +0.000011790 risk, +0.000227837 narrow, +4 DataVault refs, -20 owner-blocked NativeArray refs, +2 PrimaryManagedRuntimeRisk. Token window: 418,677,551 tokens, USD 326.77 cache-aware, USD 2,122.89 no-cache. Current SQLite live total: 50,526,148,304 tokens. Current meaningful LOC: 838,223; tokens per meaningful LOC: 60,277.69.

Token live rebase 2026-05-17T04:46+04:00: no fresh H-Phi scan. Post-04:12 JSONL window found 190,381,072 tokens over 1,793.884547 seconds, USD 173.29 cache-aware, USD 966.82 no-cache, 93.009% cached input, 106,127.83 tokens/sec average, and a 17,679,821-token peak minute at 04:41. SQLite total at 04:45:54: 50,636,429,732 tokens. Current meaningful LOC: 839,069; tokens per meaningful LOC: 60,348.35.

Live pulse 2026-05-17T05:34+04:00: SQLite total 50,953,580,001 tokens. 30-second delta 1,648,101 tokens, 54,919.99 tokens/sec, 3,295,199.27 tokens/min, 4,745,086,950.04 tokens/day equivalent. Cache-aware rate range: USD 2.52-3.00/min; no-cache scenario USD 16.73/min. Current energy estimate: 2,547.68 MWh. Current token/code ratio: 60,726.33 tokens per meaningful LOC.

