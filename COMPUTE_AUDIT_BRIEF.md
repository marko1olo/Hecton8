# COMPUTE AUDIT BRIEF

Status: AUDIT COMPLETE
Snapshot: 2026-05-15T18:16+04:00
Agent: COMPUTE_LOGISTICS_AUDITOR
Index: `COMPUTE_AUDIT_INDEX.md`
Full report: `Docs/Reports/COMPUTE_DOMINANCE_REPORT.md`
Thread triage: `COMPUTE_THREAD_TRIAGE.md`
Thread attribution: `COMPUTE_THREAD_ATTRIBUTION.md`
Validation forensics: `COMPUTE_VALIDATION_FORENSICS.md`
Thread value audit: `COMPUTE_THREAD_VALUE_AUDIT.md`
Collision risk: `COMPUTE_COLLISION_RISK.md`
File burn attribution: `COMPUTE_FILE_BURN_ATTRIBUTION.md`
Token burn rate ledger: `COMPUTE_TOKEN_BURN_RATE_LEDGER.md`
Corrected rolling rates: `COMPUTE_CORRECTED_ROLLING_RATES.md`
Model bucket reconciliation: `COMPUTE_MODEL_BUCKET_RECONCILIATION.md`
Live burn trend: `COMPUTE_LIVE_BURN_TREND.md`
Live 5min burn forecast: `COMPUTE_LIVE_BURN_5MIN_FORECAST.md`
Burn trajectory ledger: `COMPUTE_BURN_TRAJECTORY_LEDGER.md`
Live burn sources: `COMPUTE_LIVE_BURN_SOURCES.md`
Rate efficiency audit: `COMPUTE_RATE_EFFICIENCY_AUDIT.md`
Codex dialogue audit: `COMPUTE_CODEX_DIALOGUE_AUDIT.md`
Status/Rationale/Log:
- `Docs/Tasks/Status_COMPUTE_LOGISTICS_AUDITOR.md`
- `Docs/AgentLogs/Rationale_COMPUTE_LOGISTICS_AUDITOR.md`
- `Docs/AgentLogs/LOG_COMPUTE_LOGISTICS_AUDITOR.md`

## Hard Numbers

| Metric | Value |
|---|---:|
| Script files, `Assets/_Project/Scripts/**/*.cs` | 1,505 |
| Script physical LOC | 961,111 |
| Script meaningful LOC | 788,619 |
| Script logic density | 82.05% |
| All `Assets/**/*.cs` physical LOC | 1,596,990 |
| Latest JSONL final tokens | 45,771,499,116 |
| Input tokens | 45,615,641,500 |
| Cached input tokens | 43,793,799,808 |
| Output tokens | 155,599,216 |
| Cached-input ratio | 96.00610% |
| Model-aware cache-aware corrected estimate | USD 30,704.36 |
| Model-aware no-cache equivalent | USD 201,983.02 |
| Model-aware cache avoided | USD 171,278.65 |
| All-GPT-5.5 standard cache-aware scenario | USD 35,674.08 |
| All-GPT-5.5 standard no-cache scenario | USD 232,746.18 |
| All-GPT-5.5 standard cache avoided | USD 197,072.10 |
| Unknown final-usage model bucket | 0 tokens after UUID reconciliation |
| Whole-period average | 12,659.39 tokens/sec |
| Whole-period minute rate | 759,563.17 tokens/min |
| Whole-period hour rate | 45,573,790.44 tokens/hour |
| Whole-period day rate | 1,093,770,970.49 tokens/day |
| Corrected last 1h rate | 54,437.26 tokens/sec |
| Corrected last 6h rate | 39,149.01 tokens/sec |
| Corrected last 24h tokens | 3,398,780,549 |
| Corrected last 24h cache-aware cost | USD 2,601.80 |
| Corrected last 24h no-cache equivalent | USD 17,262.61 |
| Corrected last 24h average cost | USD 1.81/min; USD 108.41/hour |
| Tokens per meaningful LOC | 58,040.07 |
| Tokens per script source byte | 1,088.040 |
| Model-aware cost per meaningful LOC | USD 0.03893 |
| All-GPT-5.5 standard cost per meaningful LOC | USD 0.04524 |
| Energy by prompt constant | 2,288.57 MWh |
| Live SQLite all-thread tokens, 17:29 snapshot | 45,817,071,457 |
| Live SQLite all-thread tokens, 17:43 snapshot | 45,857,878,991 |
| Live delta since 17:29 snapshot | +40,807,534 tokens; 48,776.85 tokens/sec |
| Live SQLite all-thread tokens, 18:16 snapshot | 45,946,566,942 |
| Live delta since 17:43 snapshot | +88,687,951 tokens; 44,072.67 tokens/sec |
| Live delta since corrected JSONL final | +175,067,826 tokens; USD 117.43 |
| UUID-resolved final-usage sessions | 17 |
| Tail delta after full scan | +102,151,525 tokens |
| Tail delta model bucket | `gpt-5.5` |
| Tail delta average | 27,749.37 tokens/sec; USD 1.27/min |
| Live 90s active-source sample | 2,725,800 tokens |
| Live 90s active-source rate | 30,099.39 tokens/sec; USD 1.38/min |
| Live active threads | 11, all `gpt-5.5` |
| Live 3-minute trend | 10,233,903 tokens |
| Live 3-minute trend rate | 56,671.11 tokens/sec; USD 2.60/min |
| Live 3-minute day equivalent | 4.896B tokens/day; USD 3,748.23/day |
| Live 5-minute trend | 16,694,405 tokens |
| Live 5-minute trend rate | 55,562.22 tokens/sec; USD 2.236/min |
| Live 5-minute day equivalent | 4.801B tokens/day; USD 3,220.17/day |
| Live 5-minute no-cache day equivalent | USD 21,188.82/day |
| Live 5-minute active threads | 20, all `gpt-5.5`, all `C:/hades` |
| Latest trajectory combined window | 188,312,372 tokens over 58.32 min |
| Latest trajectory combined rate | 53,813.47 tokens/sec; USD 2.166/min |
| Latest post-forecast tail rate | 44,072.67 tokens/sec; USD 1.774/min |
| Top-30 attribution patch calls | 14,015 |
| Top-30 attribution unique patch targets | 1,647 |
| Top-30 attribution patch churn | +354,203 / -75,895 lines |
| Top-100 value-audit tokens, 03:42 snapshot | 21,963,403,961 |
| Top-100 share, 03:42 snapshot | 49.752% |
| Top-100 `apply_patch` calls | 32,908 |
| Top-100 `shell_command` calls | 212,479 |
| Top-100 patch churn | +775,074 / -176,148 lines |
| Top-100 code patch target hits | 30,172 |
| Top-100 C++ patch target hits | 0 |
| Dirty paths observed, 03:42 snapshot | 15 |
| Dirty script paths observed | 3 |
| Hot attribution targets currently dirty | 2 |
| Top-30 validation outputs inspected | 17,885 |
| Top-30 non-zero validation outputs | 2,374 |
| Top-30 non-zero validation output rate | 13.274% |
| Top-30 outputs with `error CS####` | 1,297 |
| Top-30 compile-fail signals | 935 |
| Top-30 test-success signals | 0 |
| File burn attribution targets | 1,647 |
| Code target weighted-token share | 78.073% |
| Top weighted file target | `SargassumMicroFaunaBoids.cs`, 261,401,331 tokens |
| `.codex` JSONL dialogue lines | 2,410,138 |
| `.codex` user role markers | 14,473 |
| `.codex` assistant role markers | 102,453 |
| `.codex` function-call markers | 518,303 |
| `.codex` shell-command markers | 461,485 |
| `.codex/logs_2.sqlite` rows | 474,415 |
| `logs_2.sqlite` distinct thread IDs | 871 |
| `logs_2.sqlite` exact-1000-row thread cap hits | 298 |

## Evidence Rules

- Evidence classes used: FILESYSTEM, STATIC_DOC, SQLITE, JSONL, WEB_OFFICIAL, CALC.
- `.codex` is live. Old token snapshots are still valid captures, not eternal truth.
- JSONL `last_token_usage` events repeat. Do not sum every event. Use final `total_token_usage` per session or positive deltas.
- Reasoning output is treated as part of output, not charged twice.
- This is not an OpenAI invoice. It is local ledger accounting plus official public pricing.
- H-Phi/token correlation is NOT PROVEN. There is no valid join key from token burn to H-Phi delta.
- "Compute Thief" convictions are NOT PROVEN. High-burn `.codex` threads need diff, LOC delta, compile result, and H-Phi attribution.

## Current Verdict

The codebase is not 1.63M meaningful first-party LOC. It is 775,435 meaningful script LOC and 1.58M physical all-Assets C# LOC.

The economic anomaly is context recursion: 45.771B total tokens against 788,619 meaningful LOC equals 58,040 tokens per meaningful line. Cache pricing makes the bill survivable; it does not make the workflow clean.

The top-100 `.codex` thread audit has been executed. They hold about half of the recorded token mass. Broad scanning below that is lower yield than joining these threads to final diffs, compile/test results, and quality deltas.

Thread triage has been written to `COMPUTE_THREAD_TRIAGE.md`. The valid label is `HIGH-BURN CANDIDATE`, not `waste`, until a thread is joined to changed files, meaningful LOC delta, compile/test result, and H-Phi or equivalent quality delta.

Thread attribution has been written to `COMPUTE_THREAD_ATTRIBUTION.md`. It identifies visible patch targets from top-30 rollout JSONL. This is work-trace attribution, not final value proof.

Validation forensics has been written to `COMPUTE_VALIDATION_FORENSICS.md`. Top-30 threads show validation effort, but also 2,374 non-zero validation outputs, 1,297 visible `error CS####` outputs, and no reliable test-success signal in historical rollouts. That is not a clean pass.

Thread value audit has been written to `COMPUTE_THREAD_VALUE_AUDIT.md`. Top-100 threads show real work traces: 32,908 patch calls and 30,172 code patch target hits. They do not show HECTON-8 C++ transfer evidence: 0 C++ patch target hits. The only honest C++ status from this audit is `NOT VERIFIED / NO PATCH EVIDENCE`.

Collision snapshot has been refreshed. Current hot-target intersections are `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` and `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs`.

File burn attribution has been written to `COMPUTE_FILE_BURN_ATTRIBUTION.md`. It distributes top-30 thread tokens across patch targets by per-thread patch-hit share. This is probabilistic work-trace attribution, not final value proof.

Token burn rate ledger has been written to `COMPUTE_TOKEN_BURN_RATE_LEDGER.md`. It is now the current source for rolling 1h/6h/24h/7d/14d/30d burn rate, cost/min, cost/hour, cost/day, model split, and cache savings.

Corrected rolling rates have been written to `COMPUTE_CORRECTED_ROLLING_RATES.md`. They supersede earlier rolling-window costs because they use path-or-UUID model matching.

Live burn sources have been written to `COMPUTE_LIVE_BURN_SOURCES.md`. It identifies which active thread IDs generated token deltas during a 90-second sample.

Live burn trend has been written to `COMPUTE_LIVE_BURN_TREND.md`. It preserves three consecutive 60-second samples and shows live volatility plus concentration.

Live 5-minute burn forecast has been written to `COMPUTE_LIVE_BURN_5MIN_FORECAST.md`. It supersedes the three-minute sample for short-window throttle math: 16.694M tokens over 300.46 seconds, USD 2.236/min cache-aware, with top 10 active threads holding 74.54% of the live burn.

Burn trajectory ledger has been written to `COMPUTE_BURN_TRAJECTORY_LEDGER.md`. It binds the 17:18, 17:29, 17:43, and 18:16 snapshots into a segment ledger. The tail cooled from the spike, but still ran at 44,072.67 tokens/sec after 17:43.

Rate efficiency audit remains in `COMPUTE_RATE_EFFICIENCY_AUDIT.md` as the previous detailed rate snapshot.

Codex dialogue audit has been written to `COMPUTE_CODEX_DIALOGUE_AUDIT.md`. It preserves `logs_2.sqlite` schema/count boundaries and a marker-based JSONL dialogue topology. The valid interpretation is automation density, not exact executed tool-call count.
