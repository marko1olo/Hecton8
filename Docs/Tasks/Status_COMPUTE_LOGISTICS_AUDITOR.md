# Status_COMPUTE_LOGISTICS_AUDITOR

Status: AUDIT COMPLETE
Snapshot: 2026-05-16T03:56+04:00
Scope: HECTON-8 compute/token accounting. Timaert excluded.

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

## Current Evidence

Primary output:

- `COMPUTE_AUDIT_BRIEF.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_AUDIT_INDEX.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_TOKEN_BURN_RATE_LEDGER.md`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_LIVE_DELTA_20260516.md`
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

