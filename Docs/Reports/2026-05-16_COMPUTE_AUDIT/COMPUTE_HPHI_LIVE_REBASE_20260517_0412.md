# COMPUTE H-PHI LIVE REBASE 2026-05-17 04:12

Status: AUDIT COMPLETE
Scope: HECTON-8 only. Timaert excluded.
Evidence class: static H-Phi source scan + bounded JSONL token window + SQLite live pulse.
Invoice status: NOT AN INVOICE.

## Artifacts

| Artifact | Value |
|---|---|
| Previous H-Phi artifact | `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_CURRENT_20260517_021429.json` |
| Current H-Phi artifact | `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_CURRENT_20260517_040910.json` |
| Previous H-Phi timestamp | 2026-05-17 02:17:05 +04:00 |
| Current H-Phi timestamp | 2026-05-17 04:11:59 +04:00 |
| Current scan wall time | 170,338 ms |
| Current artifact size | 153,081 bytes |

No strict budget gate was run. The prior strict old-budget gate timed out interactively; this pass measures score/counter drift only.

## H-Phi Score Delta

| Metric | Previous | Current | Delta | Ratio |
|---|---:|---:|---:|---:|
| Runtime H-Phi risk | 0.004847023 | 0.004858813 | +0.000011790 | 1.002x |
| Runtime H-Phi narrow | 0.070058393 | 0.070286230 | +0.000227837 | 1.003x |
| All-source H-Phi risk | 0.004003970 | 0.004020452 | +0.000016482 | 1.004x |
| All-source H-Phi narrow | 0.062947667 | 0.063124846 | +0.000177179 | 1.003x |
| Risk integration | 0.069185476 | 0.069128943 | -0.000056533 | 0.999x |
| Architectural purity | 1.000000000 | 1.000000000 | 0.000000000 | 1.000x |
| Data sovereignty | 0.131794933 | 0.132223543 | +0.000428610 | 1.003x |
| Memory alignment | 0.531571219 | 0.531571219 | 0.000000000 | 1.000x |
| Binary-safe ratio | 0.021536955 | 0.021536955 | 0.000000000 | 1.000x |
| AUP precision integrity | 1.000000000 | 1.000000000 | 0.000000000 | 1.000x |

## Counter Delta

| Counter | Previous | Current | Delta |
|---|---:|---:|---:|
| Runtime files | 1,344 | 1,344 | 0 |
| Runtime lines | 921,103 | 921,714 | +611 |
| SignalBus push surface | 423 | 423 | 0 |
| GlobalRegistry surface | 5,311 | 5,315 | +4 |
| Legacy event publish surface | 26 | 26 | 0 |
| Unity update debt methods | 0 | 0 | 0 |
| DataVault refs | 1,108 | 1,112 | +4 |
| NativeArray refs | 7,299 | 7,298 | -1 |
| Struct declarations | 2,043 | 2,043 | 0 |
| StructLayout attributes | 1,086 | 1,086 | 0 |
| FindObject calls | 0 | 0 | 0 |
| GetComponent calls | 321 | 322 | +1 |
| Dispose calls | 1,071 | 1,071 | 0 |
| Owner-blocked NativeArray refs | 5,143 | 5,123 | -20 |
| Primary owner-blocked NativeArray refs | 4,559 | 4,539 | -20 |
| Managed format surface | 541 | 543 | +2 |
| JobComplete surface | 73 | 73 | 0 |
| Primary managed runtime risk | 155 | 157 | +2 |

## Token Window Between H-Phi Artifacts

Window: 2026-05-17T02:17:05+04:00 to 2026-05-17T04:11:59+04:00.

| Metric | Value |
|---|---:|
| Duration | 6,894 sec |
| Prefiltered JSONL files | 50 |
| Prefiltered bytes | 561,363,073 |
| Rows read | 202,567 |
| Token rows in window | 3,022 |
| Usable usage rows | 3,021 |
| Prompt rows in window | 63 |
| Parse errors | 0 |
| Model bucket | `gpt-5.5` only |
| Input tokens | 416,269,447 |
| Cached input tokens | 399,138,816 |
| Output tokens | 1,384,824 |
| Reasoning output tokens | 398,068 |
| Total tokens | 418,677,551 |
| Cache ratio | 95.884% |
| Cache-aware cost | USD 326.77 |
| No-cache equivalent | USD 2,122.89 |
| Long-context surcharge events over 272K input | 0 |
| Average tokens/sec | 60,730.72 |
| Average tokens/min | 3,643,842.92 |
| Average tokens/hour | 218,630,574.93 |

Peak cadence:

| Peak | Value |
|---|---:|
| Token peak second | 3,255,783 at 2026-05-17T03:03:44+04:00 |
| Token peak minute | 18,611,193 at 2026-05-17T03:22+04:00 |
| Token peak hour | 192,003,736 at 2026-05-17T02:00+04:00 |
| Prompt peak minute | 7 user-message rows at 2026-05-17T03:55+04:00 |

## Marginal Efficiency

| Ratio | Value |
|---|---:|
| Tokens per +0.001 Runtime H-Phi risk | 35,511,242,663 |
| Cache-aware USD per +0.001 Runtime H-Phi risk | USD 27,715.63 |
| Tokens per +0.01 Runtime H-Phi narrow | 18,376,187,845 |
| Cache-aware USD per +0.01 Runtime H-Phi narrow | USD 14,342.15 |
| Tokens per +0.01 Data sovereignty | 9,768,263,713 |
| Cache-aware USD per +0.01 Data sovereignty | USD 7,623.88 |

Verdict: this interval is near-plateau. The project burned 418.7M tokens for a tiny H-Phi nudge. DataVault refs rose by only 4, owner-blocked NativeArray refs fell by 20, and managed runtime debt worsened by 2.

## Cumulative ROI Since 2026-05-15 Baseline

| Metric | Value |
|---|---:|
| Cumulative token spend | 5,066,407,552 |
| Cumulative cache-aware cost | USD 3,915.36 |
| Runtime H-Phi risk delta | +0.004222722 |
| Runtime H-Phi narrow delta | +0.059498791 |
| Data sovereignty delta | +0.110917511 |
| Memory alignment delta | +0.025262071 |
| Tokens per +0.001 Runtime H-Phi risk | 1,199,796,613 |
| USD per +0.001 Runtime H-Phi risk | USD 927.21 |
| Tokens per +0.01 Runtime H-Phi narrow | 851,514,370 |
| USD per +0.01 Runtime H-Phi narrow | USD 658.06 |
| Tokens per +0.01 Data sovereignty | 456,772,561 |
| USD per +0.01 Data sovereignty | USD 353.00 |
| Tokens per +0.01 Memory alignment | 2,005,539,273 |
| USD per +0.01 Memory alignment | USD 1,549.90 |

The cumulative average is still better than the latest interval because earlier DataVault migration work moved the score cheaply. The current marginal curve is poor.

## Current Live And Code Rebase

SQLite live sample at 2026-05-17T04:09:11 to 04:09:41+04:00:

| Metric | Value |
|---|---:|
| Start SQLite tokens | 50,524,427,343 |
| End SQLite tokens | 50,526,148,304 |
| 30-second delta | 1,720,961 |
| Tokens/sec | 57,365.37 |
| Tokens/min | 3,441,922 |
| Tokens/hour | 206,515,320 |
| Tokens/day equivalent | 4,956,367,680 |
| Active threads updated in last hour | 41 |

Static source scan:

| Metric | Value |
|---|---:|
| Script files | 1,580 |
| Physical LOC | 1,018,140 |
| Blank lines | 137,480 |
| Comment lines | 42,437 |
| Meaningful LOC | 838,223 |
| Script bytes | 44,672,504 |
| Logic density | 82.3289% |
| SQLite tokens / meaningful LOC | 60,277.69 |
| SQLite tokens / physical LOC | 49,625.93 |
| SQLite tokens / script byte | 1,131.03 |

Delta since 03:15 SQLite total: +72,297,514 tokens, about USD 55.37 cache-aware. Estimated current cache-aware total: USD 34,358.87. Energy estimate: 2,526.31 MWh.

## Current Backlog

Owner-blocked NativeArray backlog remains concentrated:

| Domain/File | Owner-blocked refs | Native ownership risk |
|---|---:|---:|
| `World` | 1,592 | 2,128 |
| `Gameplay` | 349 | 437 |
| `Construction` | 291 | 337 |
| `HectonVoxelEngine.cs` | 277 | 315 |
| `Core` | 194 | 274 |
| `SaveBinaryStorage.cs` | 132 | 238 |
| `Power` | 188 | 210 |
| `PlayerInventory.cs` | 198 | 204 |

Top managed runtime risk remains instrumentation/persistence:

| File | Managed format surface | Managed runtime risk |
|---|---:|---:|
| `RuntimePerformanceProfiler.cs` | 64 | 64 |
| `SaveBinaryStorage.cs` | 37 | 37 |
| `SaveManager.cs` | 37 | 37 |
| `ToolRuntimeSmokeTester.cs` | 24 | 24 |
| `BuilderRuntimeSmokeTester.cs` | 23 | 23 |
| `Dev/ShellVerificationRuntimeSmokeTester.cs` | 23 | 23 |
| `WorldGenerativeGeologyRuntimeSmokeTester.cs` | 19 | 19 |
| `Tools/PauseSystemVerifier.cs` | 14 | 14 |

STATUS: AUDIT COMPLETE.
