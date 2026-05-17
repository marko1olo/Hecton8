# Status_COMPUTE_LOGISTICS_AUDITOR

Status: AUDIT COMPLETE
Snapshot: 2026-05-16T03:56+04:00
Scope: HECTON-8 compute/token accounting. Timaert excluded.
Search keywords: H-Phi; HPhi; hphi; ash-fi; ash_phi; ASh-Fi; HФ; Аш-Фи; integration-metric; architecture-integration; token-H-Phi-ROI; compute-H-Phi.

## Checklist

- [x] Re-read active HECTON-8 `AGENTS.md`.
- [x] Locate historical compute audit bundle under `Docs/Reports/2026-05-15_COMPUTE_AUDIT/`.
- [x] Scan `Assets/_Project/Scripts/**/*.cs` for physical LOC, comment/blank stripping, meaningful LOC, domain weights, and contract/implementation ratio.
- [x] Scan `Docs/AgentLogs`, `Docs/Tasks`, and `Docs/Reports` for file/byte/token-proxy mass.
- [x] Scan `C:\Users\danat\.codex\state_5.sqlite` for thread totals, model split, cwd split, and live tail.
- [x] Scan `C:\Users\danat\.codex\sessions/**/*.jsonl` for final input/cache/output tokens and rolling burn windows.
- [x] Calculate cache-aware cost, no-cache equivalent, rolling cost/min-hour-day, token/code ratios, and energy equivalents.
- [x] Write current root brief and 2026-05-16 report bundle.
- [x] Run post-audit SQLite live-tail sample and write `COMPUTE_LIVE_DELTA_20260516.md`.
- [x] Run lightweight `logs_2.sqlite` metadata/latest-sample audit and write `COMPUTE_LOG_DB_AUDIT.md`.
- [x] Run continuation 60-second SQLite live-tail sample at 14:57 local and append updated burn rates.
- [x] Re-scan current first-party script LOC after concurrent agent changes and append updated code ratios.
- [x] Correct `logs_2.sqlite` tail query to use actual `ts`/`ts_nanos` schema and append corrected latest sample.
- [x] Run last-6h JSONL token/prompt cadence pass and write `COMPUTE_LAST6H_PROMPT_TOKEN_AUDIT.md`.
- [x] Run current H-Phi static source scan and write H-Phi/token correlation report.
- [x] Re-parse historical H-Phi artifacts with UTF-8/UTF-16 autodetection and compute token correlation.
- [x] Run latest 30-second SQLite live pulse at 23:14 local.
- [x] Run 45-second SQLite live pulse at 23:59 local and append midnight rebase.
- [x] Re-scan current first-party LOC at midnight rebase.
- [x] Attempt strict H-Phi baseline budget gate and document timeout/no-artifact boundary.
- [x] Run bounded recent JSONL rate audit after interrupted full pass and write `COMPUTE_RECENT_JSONL_RATE_AUDIT_20260517.md`.
- [x] Run 30-second SQLite live pulse at 00:52 local and re-scan first-party LOC/code-byte ratios.
- [x] Run 20-second per-thread SQLite live burner attribution at 01:39 local and append results.
- [x] Run current H-Phi summary scan at 02:17 local and write `COMPUTE_HPHI_LIVE_REBASE_20260517_0217.md`.
- [x] Compute token/cost window between 17:18 and 02:17 H-Phi artifacts.
- [x] Compute cumulative H-Phi ROI since 2026-05-15 baseline.
- [x] Run 03:04 per-thread burn spike sample and append results.
- [x] Run current H-Phi summary scan at 04:12 local and write `COMPUTE_HPHI_LIVE_REBASE_20260517_0412.md`.
- [x] Compute token/cost window between 02:17 and 04:12 H-Phi artifacts.
- [x] Run post-04:12 bounded JSONL token window and write `COMPUTE_TOKEN_LIVE_REBASE_20260517_0446.md`.
- [x] Run 04:46 per-thread SQLite live burner attribution and refresh current code ratios.
- [x] Run 05:34 SQLite live pulse and write `COMPUTE_LIVE_PULSE_20260517_0534.md`.
- [x] Detect large source drift after 04:12 and rerun H-Phi scan at 11:42.
- [x] Compute token/cost window between 04:12 and 11:42 H-Phi artifacts.
- [x] Create H-Phi / ash-fi search index and add keyword aliases to active H-Phi compute docs.
- [x] Detect source drift after 11:42 and rerun H-Phi scan at 13:37.
- [x] Compute token/cost window between 11:42 and 13:37 H-Phi artifacts.
- [x] Update H-Phi / ash-fi keyword coverage notes and stable authority links.
- [x] Detect source drift after 13:37 and rerun final H-Phi scan at 15:39.
- [x] Correct 13:37-15:39 JSONL token accounting from cumulative deltas and reject naive row-sum overcount.
- [x] Update final compute audit bundle and stop measurement loop.

## Current Evidence

Primary output:

- `COMPUTE_AUDIT_BRIEF.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_AUDIT_INDEX.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_TOKEN_BURN_RATE_LEDGER.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_LIVE_DELTA_20260516.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_RECENT_JSONL_RATE_AUDIT_20260517.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_TOKEN_LIVE_REBASE_20260517_0446.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_LIVE_PULSE_20260517_0534.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_SEARCH_INDEX_20260517.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_KEYWORD_COVERAGE_20260517.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_LIVE_REBASE_20260517_1539.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_LIVE_REBASE_20260517_1337.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_LIVE_REBASE_20260517_1142.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_LIVE_REBASE_20260517_0412.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_LIVE_REBASE_20260517_0217.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_BUDGET_GATE_ATTEMPT_20260517.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_LOG_DB_AUDIT.md`
- `Docs/Reports/COMPUTE_DOMINANCE_REPORT.md`

No Unity compile/run was required. This task is accounting, not runtime validation.

## Continuation Snapshot

2026-05-16T14:57+04:00:

- SQLite thread tokens: 48,761,315,725.
- 60-second live burn: 5,591,521 tokens; 93,192.02 tokens/sec.
- Active threads: 29.
- First-party meaningful LOC: 827,838.
- Estimated cache-aware total: USD 33,007.19.
- Energy: 2,438.07 MWh.
- Last 6h JSONL tokens: 757,394,868; USD 607.01 cache-aware.

2026-05-16T23:14+04:00:

- SQLite thread tokens: 49,767,593,348.
- 30-second live burn: 3,815,200 tokens; 127,173.33 tokens/sec.
- Runtime H-Phi risk: 0.004164939.
- Runtime H-Phi narrow: 0.060806118.
- H-Phi/token artifact correlation: risk r=0.522; narrow r=0.493.

2026-05-17T00:00+04:00:

- SQLite thread tokens: 49,903,844,533.
- 45-second live burn: 4,829,772 tokens; 107,328.27 tokens/sec.
- First-party meaningful LOC: 836,249.
- Estimated cache-aware total: USD 33,882.25.
- Strict H-Phi baseline gate attempt timed out after 244 seconds with no completed artifact.

2026-05-17T00:52+04:00:

- Bounded recent JSONL pass: 81 files, 991,426,469 bytes, 85,425 usable usage rows, 0 parse errors.
- Last 24h JSONL tokens: 5,364,091,619.
- Last 24h cache-aware cost: USD 4,123.40; no-cache equivalent: USD 27,223.44.
- Last 24h cache ratio: 96.211%.
- SQLite thread tokens: 50,027,664,742.
- 30-second live burn: 2,659,344 tokens; 88,644.80 tokens/sec.
- First-party meaningful LOC: 836,910.
- Tokens per meaningful LOC: 59,776.64.

2026-05-17T01:39+04:00:

- Per-thread 20-second SQLite delta: 828,509 tokens; 41,425.45 tokens/sec.
- Active delta threads: 6.
- Top burners: Build hull repair engine, Standardize SignalBus lanes, Build STP dynamic resolution adapter, Build ballast PID, CORE_TICK_DILATION.

2026-05-17T02:17+04:00:

- Runtime H-Phi risk: 0.004847023.
- Runtime H-Phi narrow: 0.070058393.
- Data sovereignty: 0.131794933.
- Memory alignment: 0.531571219.
- Delta vs 17:18 H-Phi: +0.000682084 risk, +0.009252275 narrow, +160 DataVault refs, -123 owner-blocked NativeArray refs.
- Token window between H-Phi artifacts: 2,183,475,652 tokens; USD 1,640.89 cache-aware.
- SQLite thread tokens: 50,313,194,499.
- First-party meaningful LOC: 837,628.
- Tokens per meaningful LOC: 60,066.28.

2026-05-17T03:15+04:00:

- 03:04 per-thread 20-second delta: 3,079,626 tokens; 153,981.30 tokens/sec.
- Active delta threads: 20.
- SQLite thread tokens: 50,453,850,790.
- Estimated cache-aware total: USD 34,303.50.
- Energy estimate: 2,522.69 MWh.
- Tokens per meaningful LOC: 60,234.20.

2026-05-17T04:12+04:00:

- Runtime H-Phi risk: 0.004858813.
- Runtime H-Phi narrow: 0.070286230.
- Data sovereignty: 0.132223543.
- Delta vs 02:17 H-Phi: +0.000011790 risk, +0.000227837 narrow, +4 DataVault refs, -20 owner-blocked NativeArray refs, +2 PrimaryManagedRuntimeRisk.
- Token window between H-Phi artifacts: 418,677,551 tokens; USD 326.77 cache-aware.
- SQLite thread tokens: 50,526,148,304.
- First-party meaningful LOC: 838,223.
- Tokens per meaningful LOC: 60,277.69.

2026-05-17T04:46+04:00:

- No new H-Phi scan; latest H-Phi artifact remained 04:12 and cost 170,338 ms to create.
- Post-04:12 JSONL window tokens: 190,381,072 over 1,793.884547 seconds.
- Post-04:12 average rate: 106,127.83 tokens/sec; 6,367,669.72 tokens/min.
- Post-04:12 cost: USD 173.29 cache-aware; USD 966.82 no-cache.
- SQLite thread tokens: 50,636,429,732.
- Current energy estimate: 2,531.82 MWh.
- First-party meaningful LOC: 839,069.
- Tokens per meaningful LOC: 60,348.35.
- 04:46 per-thread delta: 497,906 tokens in 20 seconds; top burners `Add modulo time slicer`, `AUDIO_IMPORT_RESIDENCY_GUARD`, `Add indirect flora drawing`.

2026-05-17T05:34+04:00:

- SQLite thread tokens: 50,953,580,001.
- 30-second live burn: 1,648,101 tokens; 54,919.99 tokens/sec.
- Live burn rate: 3,295,199.27 tokens/min; 4,745,086,950.04 tokens/day equivalent.
- Cache-aware rate range: USD 2.52-3.00/min; no-cache scenario: USD 16.73/min.
- Energy estimate: 2,547.68 MWh.
- Tokens per meaningful LOC: 60,726.33.
- Active delta threads: 5; top burners `Enforce DataVault statelessness`, `CONTENT_AUTHORITY_DICTATOR`, `Move reports to batch006`, `Build ballast PID`, `Improve bot memory and CRM`.

2026-05-17T11:42+04:00:

- H-Phi scan justified by source drift: 113 C# files changed after 04:12, 10,799,862 bytes touched.
- Runtime H-Phi risk: 0.005378664.
- Runtime H-Phi narrow: 0.075881112.
- Data sovereignty: 0.141543476.
- Delta vs 04:12 H-Phi: +0.000519851 risk, +0.005594882 narrow, +104 DataVault refs, -162 owner-blocked NativeArray refs, -175 PrimaryNativeOwnershipRisk, +20 PrimaryManagedRuntimeRisk.
- Token window between H-Phi artifacts: 501,495,243 tokens; USD 397.22 cache-aware; USD 2,548.92 no-cache.
- SQLite thread tokens: 51,066,572,323.
- 11:38 live burn: 3,001,335 tokens; 99,715.11 tokens/sec.
- First-party meaningful LOC: 854,943.
- Tokens per meaningful LOC: 59,730.97.
- Energy estimate: 2,553.33 MWh.

2026-05-17T13:37+04:00:

- H-Phi scan justified by source drift: 102 C# files changed after 11:42, 8,481,368 bytes touched.
- Runtime H-Phi risk: 0.005525762.
- Runtime H-Phi narrow: 0.077385732.
- Data sovereignty: 0.144331092.
- Delta vs 11:42 H-Phi: +0.000147098 risk, +0.001504620 narrow, +29 DataVault refs, -20 owner-blocked NativeArray refs, -28 PrimaryNativeOwnershipRisk, +6 PrimaryManagedRuntimeRisk.
- Token window between H-Phi artifacts: 304,562,532 tokens; USD 236.42 cache-aware; USD 1,546.69 no-cache.
- SQLite thread tokens: 51,372,184,781.
- 13:36 live burn: 741,683 tokens; 24,665.28 tokens/sec.
- First-party meaningful LOC: 856,940.
- Tokens per meaningful LOC: 59,948.40.
- Energy estimate: 2,568.61 MWh.

2026-05-17T15:39+04:00 FINAL:

- H-Phi scan justified by source drift: 46 C# files changed after 13:37, 3,210,412 bytes touched.
- Runtime H-Phi risk: 0.005580503.
- Runtime H-Phi narrow: 0.077988159.
- Data sovereignty: 0.145138727.
- Delta vs 13:37 H-Phi: +0.000054741 risk, +0.000602427 narrow, -48 NativeArray refs, -39 owner-blocked NativeArray refs, -41 PrimaryNativeOwnershipRisk, 0 PrimaryManagedRuntimeRisk.
- Corrected token window between H-Phi artifacts: 213,121,363 tokens; USD 145.30 cache-aware; USD 1,080.48 no-cache.
- Rejected naive `last_token_usage` row sum: 466,464,890 tokens.
- SQLite thread tokens: 51,586,452,098.
- 15:38 live burn: 111,779 tokens; 3,724.30 tokens/sec.
- First-party meaningful LOC: 857,227.
- Tokens per meaningful LOC: 60,178.29.
- Energy estimate: 2,579.32 MWh.
