# 2026-05-16 COMPUTE AUDIT INDEX

Status: AUDIT COMPLETE
Snapshot: 2026-05-16T03:56+04:00
Agent: COMPUTE_LOGISTICS_AUDITOR
Scope: HECTON-8 local telemetry and source/docs size.
Search keywords: H-Phi; HPhi; hphi; ash-fi; ash_phi; ASh-Fi; HФ; Аш-Фи; integration-metric; architecture-integration; token-H-Phi-ROI; compute-H-Phi.

## Read Order

1. `COMPUTE_AUDIT_BRIEF.md` in this folder - shortest current snapshot. It was moved out of repo root during the 2026-05-17 R3 documentation integration pass.
2. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_TOKEN_BURN_RATE_LEDGER.md` - current token, cost, cadence, LOC, and energy ledger.
3. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_TOKEN_REBASE_20260518_1734.md` - latest full JSONL/SQLite token rebase.
4. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_LIVE_DELTA_20260516.md` - post-audit SQLite live burn sample.
5. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_LAST6H_PROMPT_TOKEN_AUDIT.md` - recent six-hour JSONL token/prompt cadence.
6. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_RECENT_JSONL_RATE_AUDIT_20260517.md` - bounded 30h/24h/6h/1h rate audit and 00:52 live pulse.
7. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_SEARCH_INDEX_20260517.md` - H-Phi / ash-fi search aliases and score timeline.
8. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_KEYWORD_COVERAGE_20260517.md` - H-Phi / ash-fi keyword coverage boundary.
9. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_LIVE_REBASE_20260517_1539.md` - latest H-Phi score/counter/token rebase.
10. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_LIVE_REBASE_20260517_1337.md` - previous H-Phi score/counter/token rebase.
11. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_LIVE_REBASE_20260517_1142.md` - earlier H-Phi score/counter/token rebase.
12. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_LIVE_PULSE_20260517_0534.md` - SQLite live pulse and active burners.
13. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_TOKEN_LIVE_REBASE_20260517_0446.md` - post-H-Phi token/live-rate rebase.
14. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_LIVE_REBASE_20260517_0412.md` - prior H-Phi score/counter/token rebase.
15. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_LIVE_REBASE_20260517_0217.md` - earlier H-Phi score/counter/token rebase.
16. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_TOKEN_CORRELATION_20260516.md` - H-Phi source score and token correlation.
17. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_BUDGET_GATE_ATTEMPT_20260517.md` - strict H-Phi baseline gate attempt and timeout boundary.
18. `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_LOG_DB_AUDIT.md` - Codex log DB size/noise audit.
19. `Docs/Reports/COMPUTE_DOMINANCE_REPORT.md` - historical report plus appended continuation addenda.

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

H-Phi live rebase 2026-05-17T11:42+04:00: source drift after 04:12 justified a new scan: 113 C# files changed, 10,799,862 changed bytes, current 854,943 meaningful LOC. Runtime H-Phi risk 0.005378664, narrow 0.075881112, Data sovereignty 0.141543476. Versus 04:12: +0.000519851 risk, +0.005594882 narrow, +104 DataVault refs, -162 owner-blocked NativeArray refs, but +20 PrimaryManagedRuntimeRisk. Token window 04:12-11:42: 501,495,243 tokens, USD 397.22 cache-aware, USD 2,548.92 no-cache. SQLite total at 11:39: 51,066,572,323 tokens; current energy 2,553.33 MWh.

H-Phi live rebase 2026-05-17T13:37+04:00: source drift after 11:42 justified another scan: 102 C# files changed, 8,481,368 changed bytes, current 856,940 meaningful LOC. Runtime H-Phi risk 0.005525762, narrow 0.077385732, Data sovereignty 0.144331092. Versus 11:42: +0.000147098 risk, +0.001504620 narrow, +29 DataVault refs, -20 owner-blocked NativeArray refs, but +6 PrimaryManagedRuntimeRisk and +21 GlobalRegistry surface. Token window 11:42-13:37: 304,562,532 tokens, USD 236.42 cache-aware, USD 1,546.69 no-cache. SQLite total at 13:36: 51,372,184,781 tokens; current energy 2,568.61 MWh.

H-Phi live rebase 2026-05-17T15:39+04:00: source drift after 13:37 justified the final scan: 46 C# files changed, 3,210,412 changed bytes, current 857,227 meaningful LOC. Runtime H-Phi risk 0.005580503, narrow 0.077988159, Data sovereignty 0.145138727. Versus 13:37: +0.000054741 risk, +0.000602427 narrow, -48 NativeArray refs, -39 owner-blocked NativeArray refs, -41 PrimaryNativeOwnershipRisk, 0 PrimaryManagedRuntimeRisk growth. Corrected token window 13:37-15:39: 213,121,363 tokens, USD 145.30 cache-aware, USD 1,080.48 no-cache. SQLite total at 15:39: 51,586,452,098 tokens; current energy 2,579.32 MWh.

Token rebase 2026-05-18T17:34+04:00: full JSONL pass over 1,002 files / 9,580,317,579 bytes plus SQLite live tail. Current HECTON/Hades SQLite total at 17:35:14 is 54,517,775,171 tokens; JSONL final HECTON/Hades split is 54,468,241,841 tokens, 54,281,061,389 input, 52,113,735,040 cached input, 186,922,052 output, 96.007% cached-input ratio. Delta vs 2026-05-17T15:39 SQLite is +2,931,323,073 tokens. Last 24h HECTON/Hades window is 2,862,892,706 tokens. Current SQLite HECTON/Hades cache-aware estimate is USD 37,610.11; no-cache equivalent is USD 246,711.58. Current first-party script surface is 934,997 meaningful LOC, 58,302.09 tokens per meaningful LOC, and 2,725.61 MWh energy-equivalent under the legacy 0.05 kWh/1K-token formula.

