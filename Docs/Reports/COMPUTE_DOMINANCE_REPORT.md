# COMPUTE DOMINANCE REPORT

Status: AUDIT COMPLETE
Agent: COMPUTE_LOGISTICS_AUDITOR
Domain: Echelon 9 / Meta, Audit, Reporting, Evidence Accounting
Audit timestamp: 2026-05-15T01:49:02+04:00
Evidence class: FILESYSTEM / STATIC_DOC / SQLITE / JSONL / WEB_OFFICIAL / CALC. No Unity runtime, profiler, GCMonitor, or billing export proof.

## Executive Verdict

The current first-party script surface is not 1.63M meaningful LOC. It is 775,435 meaningful LOC under `Assets/_Project/Scripts`, 946,341 physical script LOC, and 1,581,522 physical C# LOC under all `Assets`. The 1.63M claim is close to all-Assets physical C# plus drift, not meaningful first-party logic.

The `.codex` ledger is the economic anomaly: 764 thread rows in `state_5.sqlite`, 43.436B recorded `tokens_used`, and 765 JSONL session files occupying about 8.0GB. Final JSONL session totals cross-check at 43.423B total tokens. The raw sticker shadow bill at the supplied GPT-5.5 Spud rates is about USD 437,166.04. If cached input is treated as a zero-cost lower bound, the floor is still about USD 21,733.53.

Continuation reprice on 2026-05-15T02:55+04:00 supersedes the prompt-constant bill for current market pricing. Latest JSONL final usage is 43,778,987,916 total tokens: 43,630,634,851 input, 41,886,807,040 cached input, and 148,094,665 output. Current standard API-rate cache-aware estimate is USD 29,135.37. No-cache equivalent is USD 191,832.08. Cache avoided USD 162,696.72, or 84.812%. Effective blended price is USD 0.6655 per 1M total tokens. This is still not an invoice.

The prompt cadence is pathological by normal production standards: `.codex` shows peak user-message bursts of 13 prompts/sec, 40 prompts/min, 202 prompts/hour, 748 prompts/day, and 2,565 prompts/week. The last observed six-hour window contained 183 user prompts, or 30.5/hour.

H-Phi correlation with token burn is NOT PROVEN. H-Phi values exist in `HECTON_PHI_REPORT.md`; token burn exists in `.codex` SQLite/JSONL; no valid join key maps token spend to H-Phi delta. Any stronger claim is metric theater.

No named active agent is convicted as a "Compute Thief" from current Status/LOG/Rationale docs. `.codex` does contain 716 threads over 1M tokens and 30 threads over 250M tokens. They are high-burn candidates pending diff/LOC/H-Phi attribution.

## Dashboard

| Metric | Value | Evidence | Residual risk |
|---|---:|---|---|
| Active agent IDs in current Status/LOG/Rationale files | 57 | FILESYSTEM | File naming only, not proof of live processes |
| `Assets/_Project/Scripts/**/*.cs` files | 1,501 | FILESYSTEM | Current dirty workspace only |
| Script physical LOC | 946,341 | FILESYSTEM | Comment stripping custom scanner, not `cloc` |
| Script meaningful LOC | 775,435 | FILESYSTEM | Comment-only lines subtracted; inline comments kept |
| Logic density | 81.94% | FILESYSTEM | Static source metric only |
| All `Assets/**/*.cs` physical LOC | 1,581,522 | FILESYSTEM | Includes third-party/vendor surfaces |
| `Packages/**/*.cs` physical LOC | 142,887 | FILESYSTEM | Package code, not first-party ownership |
| `.codex` threads | 764 | SQLITE | `state_5.sqlite` thread table |
| `.codex` JSONL sessions | 765 | FILESYSTEM/JSONL | One archived session included |
| `.codex` JSONL bytes | 7,995,089,133 | FILESYSTEM | Not a token count |
| `.codex` state token sum | 43,436,372,807 | SQLITE | Internal Codex accounting, not invoice |
| JSONL final session token sum | 43,423,314,989 | JSONL | Final `total_token_usage` per session |
| Latest JSONL final session token sum | 43,778,987,916 | JSONL | Continuation pass; ledger is live |
| Raw shadow bill | USD 437,166.04 | CALC | Supplied rates, no real billing export |
| Cached-input lower-bound bill | USD 21,733.53 | CALC | Assumes cached input free; prompt did not authorize that discount |
| Cache-aware current API estimate | USD 29,135.37 | CALC | Standard model rates with cached-input pricing; not invoice |
| No-cache current API equivalent | USD 191,832.08 | CALC | Same tokens, cached input priced as normal input |
| Cache discount avoided | USD 162,696.72 | CALC | 84.812% reduction from no-cache equivalent |
| Effective blended token price | USD 0.6655 / 1M tokens | CALC | Total cost divided by all latest JSONL final tokens |
| Latest live SQLite token mass | 43,998,578,833 | SQLITE | 2026-05-15T03:21+04 snapshot; ledger continues moving |
| Energy estimate | 2,171.17 MWh | CALC | Uses supplied 0.05 kWh/1K tokens, not OpenAI telemetry |
| Peak `.codex` prompt burst | 13/sec | JSONL | User-message events only |
| Last six hours prompt rate | 30.5/hour | JSONL | Latest observed `.codex` timestamp window |
| 14-day meaningful LOC velocity | 2,307.84 LOC/hour | CALC | Compression model, not git-proven creation time |
| Human-year compression, meaningful LOC | 176.24-352.47 years | CALC | 10-20 LOC/day, 220 workdays/year |
| Midpoint replacement cost, meaningful LOC | USD 58.75M | CALC | Assumes USD 250k fully loaded senior/year |
| Midpoint replacement cost, all Assets physical C# | USD 119.81M | CALC | Includes vendor/third-party physical code |

## Near-Root Audit Files

These files preserve the short operational view outside the long report:

| File | Purpose |
|---|---|
| `COMPUTE_AUDIT_INDEX.md` | Read-order index and hard evidence boundaries |
| `COMPUTE_AUDIT_BRIEF.md` | Root hard-number snapshot and evidence rules |
| `COMPUTE_THREAD_TRIAGE.md` | Top-heavy `.codex` thread queue by token concentration |
| `COMPUTE_THREAD_ATTRIBUTION.md` | Top-30 rollout JSONL work-trace attribution |
| `COMPUTE_COLLISION_RISK.md` | Current dirty-tree intersection with hot attribution targets |
| `COMPUTE_VALIDATION_FORENSICS.md` | Top-30 validation command/output forensic scan |
| `COMPUTE_FILE_BURN_ATTRIBUTION.md` | Weighted token burn attribution by patch target |

Latest attribution pass parsed top-30 rollout JSONL files: 490,220 events, 14,015 `apply_patch` calls, 86,616 `shell_command` calls, 1,647 unique patch targets, and patch churn of +354,203/-75,895 lines. This is work-trace evidence, not final value proof.

Current collision snapshot observed 45 dirty/untracked paths and 10 dirty `Assets/_Project/Scripts/*` paths. Hot-target intersection: `Assets/_Project/Scripts/SpatialAudioManager.cs`.

Validation forensics scanned 17,885 validation-relevant outputs in top-30 rollout JSONL: 15,510 exit-code-zero outputs, 2,374 non-zero outputs, 1,297 outputs with `error CS####`, 935 compile-fail signals, 746 build-success signals, and 0 reliable test-success signals. This proves validation effort existed. It does not prove final correctness.

File burn attribution distributed top-30 thread tokens across 1,647 patch targets by per-thread patch-hit share. Weighted class split: code 7,411,317,235 tokens (78.073%), docs 1,143,348,625 (12.044%), assets 496,931,949 (5.235%), other 431,588,269 (4.546%), packages 9,607,022 (0.101%). Top weighted file: `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` at 261,401,331 weighted tokens.

## Continuation Addendum - Live Ledger Recheck

Recheck timestamp: 2026-05-15 during continuation pass.

The `.codex` ledger is live. After the first report write, `state_5.sqlite` moved from 43,436,372,807 to 43,651,684,909 thread tokens. Delta: +215,312,102 tokens, about +0.496%. This is not a correction of the first capture. It is new churn while the machine kept working.

### Token Concentration

| Top N threads | Tokens | Share of current SQLite token mass |
|---:|---:|---:|
| 1 | 518,697,166 | 1.188% |
| 5 | 2,315,069,669 | 5.304% |
| 10 | 3,958,374,314 | 9.068% |
| 30 | 9,492,793,103 | 21.747% |
| 50 | 13,854,323,252 | 31.738% |
| 100 | 21,929,194,190 | 50.237% |
| 123 | 24,574,190,128 | 56.296% |
| 250 | 33,560,370,660 | 76.882% |

Half the token mass is concentrated in the top 100 threads. That is the actual target for any future audit. Broad moralizing over 764 threads is low-yield.

### Model Split - Current SQLite

| Model | Threads | Tokens | Share |
|---|---:|---:|---:|
| `gpt-5.5` | 483 | 31,767,489,763 | 72.775% |
| `gpt-5.4` | 244 | 11,591,437,853 | 26.554% |
| `gpt-5.4-mini` | 25 | 192,533,099 | 0.441% |
| `gpt-5.2-codex` | 3 | 85,512,992 | 0.196% |
| `gpt-5.1-codex-mini` | 3 | 13,472,930 | 0.031% |
| `gpt-5.3-codex` | 3 | 1,096,113 | 0.003% |
| `gpt-5.2` | 3 | 142,159 | ~0% |

The cost center is not "agents" in the abstract. It is `gpt-5.5` long-context work.

### Working Directory Split - Current SQLite

| CWD | Threads | Tokens | Share |
|---|---:|---:|---:|
| `\\?\C:\hades` | 440 | 25,660,018,300 | 58.784% |
| `\\?\C:\hades\Hecton8` | 291 | 17,755,525,588 | 40.675% |
| `c:\hades\Hecton8` | 29 | 198,567,084 | 0.455% |
| `\\?\C:\Users\danat\Downloads` | 3 | 37,573,937 | 0.086% |
| `c:\hades` | 1 | 0 | 0% |

Root-level `C:\hades` sessions now carry more token mass than direct `Hecton8` sessions. That matters because root context usually drags more unrelated filesystem and doc surface.

### Updated-Day Proxy

This table assigns each thread's total tokens to the day it was last updated. It is not true daily spend; it is a triage proxy.

| Day | Threads updated | Tokens assigned | Share |
|---|---:|---:|---:|
| 2026-05-09 | 37 | 6,975,131,407 | 15.979% |
| 2026-05-03 | 20 | 4,031,099,628 | 9.235% |
| 2026-05-14 | 60 | 3,708,474,086 | 8.496% |
| 2026-05-11 | 98 | 3,397,025,439 | 7.782% |
| 2026-05-13 | 78 | 2,906,507,916 | 6.658% |
| 2026-04-29 | 27 | 2,286,427,061 | 5.238% |
| 2026-05-01 | 14 | 2,040,759,482 | 4.675% |
| 2026-05-12 | 57 | 1,966,316,084 | 4.505% |
| 2026-05-05 | 30 | 1,866,271,582 | 4.275% |
| 2026-05-10 | 38 | 1,861,990,920 | 4.266% |

### JSONL File-Write Weight

This table uses session file LastWriteTime and bytes, not tokens. It is useful for locating fat transcript days.

| Day | JSONL files | Bytes |
|---|---:|---:|
| 2026-05-09 | 37 | 1,150,105,051 |
| 2026-05-03 | 20 | 702,656,978 |
| 2026-05-13 | 102 | 524,517,453 |
| 2026-05-11 | 77 | 431,369,326 |
| 2026-05-01 | 14 | 391,252,089 |
| 2026-05-06 | 37 | 383,762,078 |
| 2026-04-09 | 5 | 289,502,450 |
| 2026-05-15 | 41 | 287,557,060 |
| 2026-05-12 | 61 | 283,022,474 |
| 2026-04-29 | 18 | 249,664,931 |

### Current Agent-Doc Top Surfaces

Current Status/LOG/Rationale docs remain small compared with `.codex` thread burn. Top active agent-doc surfaces:

| Agent ID | Files | Estimated doc tokens |
|---|---:|---:|
| `ARCHITECTURAL_AUP_INTEGRITY_AUDITOR` | 3 | 34,595 |
| `PROLOGUE_SEQUENCE_DIRECTOR` | 3 | 28,481 |
| `MARAUDER_OUTPOST_ARCHITECT` | 3 | 28,455 |
| `MACRO_WFC_PERSISTENCE_SYNC` | 3 | 25,122 |
| `VR_COCKPIT_MANUAL_OVERRIDE` | 3 | 25,084 |
| `VOLUMETRIC_PRESSURE_SOLVER` | 3 | 24,790 |
| `KINETIC_IMPACT_ACOUSTICS` | 3 | 24,252 |
| `PROCEDURAL_BIOME_BAKER_SHALLOWS` | 3 | 21,580 |
| `PLATINUM_DATA_VAULT_WARDEN` | 3 | 19,639 |
| `VFX_SDF_CARVE_DEBRIS` | 3 | 19,473 |

Still no active named agent crosses a 1M estimated doc-token threshold. The thief hunt belongs in `.codex` thread attribution, not Markdown size.

### `logs_2.sqlite` Boundary

`logs_2.sqlite` exists and was previously schema-counted at 478,371 rows and 3,203,743,744 bytes. A full grouping pass over log targets timed out twice at 120 seconds. That means the log DB is heavy enough to require an indexed/offline extraction pass before it can be used as precise forensic evidence. This report does not pretend otherwise.

## Throughput And Cache-Aware Reprice

Recheck timestamp: 2026-05-15T02:55+04:00.

Method: JSONL pass over `.codex` sessions, counting positive deltas of `total_token_usage` per session and retaining final session usage by model. Runtime: 348.7 seconds. Token events parsed: 345,347. Usage events parsed: 344,535. Parse errors: 0. Negative deltas observed: 8.

Price source: OpenAI standard API pricing verified on 2026-05-15. GPT-5.5, GPT-5.4, and GPT-5.4 mini rates are from `https://openai.com/api/pricing/`. GPT-5.3-Codex, GPT-5.2-Codex, GPT-5.1-Codex-mini, and GPT-5.2 rates are from official OpenAI model pages under `https://developers.openai.com/api/docs/models/`. Batch, priority, enterprise, taxes, and subscription entitlements are excluded.

### Latest JSONL Token Ledger

| Metric | Value |
|---|---:|
| Final input tokens | 43,630,634,851 |
| Cached input tokens | 41,886,807,040 |
| Non-cached input tokens | 1,743,827,811 |
| Output tokens | 148,094,665 |
| Reasoning output tokens | 51,467,734 |
| Final total tokens | 43,778,987,916 |
| Positive-delta token sum | 43,790,642,941 |
| Delta method vs final total | 100.0266% |
| Cached-input ratio | 96.0032% |
| Output/input ratio | 0.3394% |

Reasoning output is treated as a subset of output, not an extra billable add-on. Adding it again would double-count.

### Token Flow Rates

Observation window: 2026-04-03T17:10:40.595Z to 2026-05-14T22:49:18.114Z, 3,562,717.519 seconds.

| Window | Tokens | Tokens/sec | Tokens/min | Tokens/hour | Tokens/day equivalent |
|---|---:|---:|---:|---:|---:|
| Whole observed period | 43,790,642,941 | 12,291.36 | 737,481.59 | 44,248,895.33 | 1,061,973,487.91 |
| Last 1h | 334,082,492 | 92,800.69 | 5,568,041.53 | 334,082,492.00 | 8,017,979,808.00 |
| Last 6h | 1,331,534,247 | 61,645.10 | 3,698,706.24 | 221,922,374.50 | 5,326,136,988.00 |
| Last 24h | 2,422,466,534 | 28,037.81 | 1,682,268.43 | 100,936,105.58 | 2,422,466,534.00 |
| Last 7d | 18,498,354,270 | 30,585.90 | 1,835,154.19 | 110,109,251.61 | 2,642,622,038.57 |
| Last 14d | 28,935,776,912 | 23,921.77 | 1,435,306.39 | 86,118,383.67 | 2,066,841,208.00 |

### Peak Token Buckets

These are token-accounting buckets, not raw request-arrival buckets. A one-second spike can include consolidated telemetry from a large turn.

| Bucket | Peak label | Tokens | Equivalent rate |
|---|---|---:|---:|
| Second | 2026-04-11 19:12:43 UTC | 23,433,405 | 23,433,405 tokens/sec |
| Minute | 2026-04-13 14:59 UTC | 36,323,325 | 605,388.75 tokens/sec |
| Hour | 2026-05-14 21 UTC | 385,964,924 | 107,212.48 tokens/sec |
| Day | 2026-05-13 | 3,951,756,366 | 45,737.92 tokens/sec |
| Week | 2026-W19 | 14,308,828,640 | 23,658.78 tokens/sec |

### Top Token Days

| Rank | Day | Positive-delta tokens |
|---:|---|---:|
| 1 | 2026-05-13 | 3,951,756,366 |
| 2 | 2026-05-08 | 3,187,635,852 |
| 3 | 2026-05-11 | 2,610,546,071 |
| 4 | 2026-05-12 | 2,415,939,777 |
| 5 | 2026-04-29 | 2,403,482,481 |
| 6 | 2026-05-09 | 2,353,206,029 |
| 7 | 2026-05-14 | 2,215,708,707 |
| 8 | 2026-05-07 | 2,196,754,809 |
| 9 | 2026-05-05 | 1,915,455,339 |
| 10 | 2026-05-01 | 1,815,908,075 |

### Cache-Aware Bill By Model

| Model | Input tokens | Cached input | Output tokens | Cache-aware cost | No-cache equivalent |
|---|---:|---:|---:|---:|---:|
| `gpt-5.5` | 31,793,537,853 | 30,575,045,120 | 99,707,533 | USD 24,371.21 | USD 161,958.92 |
| `gpt-5.4` | 11,546,273,790 | 11,051,701,632 | 46,453,047 | USD 4,696.15 | USD 29,562.48 |
| `gpt-5.4-mini` | 191,173,213 | 167,098,752 | 1,359,886 | USD 36.71 | USD 149.50 |
| `gpt-5.2-codex` | 85,044,900 | 79,787,648 | 468,092 | USD 29.72 | USD 155.38 |
| `gpt-5.1-codex-mini` | 13,374,833 | 12,237,952 | 98,097 | USD 0.79 | USD 3.54 |
| `gpt-5.3-codex` | 1,088,533 | 879,744 | 7,580 | USD 0.63 | USD 2.01 |
| `gpt-5.2` | 141,729 | 56,192 | 430 | USD 0.17 | USD 0.25 |
| Total | 43,630,634,851 | 41,886,807,040 | 148,094,665 | USD 29,135.37 | USD 191,832.08 |

Cash verdict: prompt-cache pricing avoided USD 162,696.72 against the no-cache equivalent, an 84.812% reduction. The blended effective price is USD 0.6655 per 1M total tokens, or 1,502,606 total tokens per USD.

### Tokens Per Code Byte

Live source-byte scan grew by one script file during continuation. LOC ratios below still use the earlier verified 775,435 meaningful LOC and 946,341 physical script LOC; LOC was not recomputed in this pass.

| Scope | Files | Bytes | Tokens/source byte |
|---|---:|---:|---:|
| `Assets/_Project/Scripts/**/*.cs` | 1,502 | 41,479,641 | 1,055.433 |
| `Assets/_Project/**/*.cs` | 1,550 | 42,191,109 | 1,037.635 |
| `Assets/**/*.cs` | 4,113 | 66,465,190 | 658.675 |
| `Packages/**/*.cs` | 984 | 5,549,590 | Not used for first-party ratio |

| Ratio | Value |
|---|---:|
| Tokens per meaningful script LOC | 56,457.33 |
| Tokens per physical script LOC | 46,261.32 |
| Output tokens per meaningful script LOC | 190.98 |
| Tokens per script source KiB | 1,080,763.54 |
| Tokens per script source MiB | 1,106,701,864.49 |
| Cache-aware cost per meaningful LOC | USD 0.037573 |
| Cache-aware cost per physical script LOC | USD 0.030787 |
| Cache-aware cost per script source byte | USD 0.0007024 |
| Cache-aware cost per script source KiB | USD 0.7193 |
| Cache-aware cost per script source MiB | USD 736.52 |
| Context amplification vs 50-token/LOC heuristic | 1,129.15x |

The interesting number is not output tokens per LOC. It is the 56,457 total tokens burned per meaningful LOC. That is the audit signature of long-context recursion.

## Code Metrics

### Script LOC

| Scope | Files | Physical | Blank | Comment-only | Meaningful | Logic density |
|---|---:|---:|---:|---:|---:|---:|
| `Assets/_Project/Scripts/**/*.cs` | 1,501 | 946,341 | 129,393 | 41,513 | 775,435 | 81.94% |

### Wider C# Surface

| Scope | Files | Physical LOC | Meaning |
|---|---:|---:|---|
| `Assets/_Project/**/*.cs` | 1,549 | 964,052 | First-party project C# physical surface |
| `Assets/**/*.cs` | 4,112 | 1,581,522 | Project plus vendor/third-party asset C# |
| `Packages/**/*.cs` | 984 | 142,887 | Package code surface |

### Boilerplate / Contract Ratio

| Category | Files | Meaningful LOC | Physical LOC | Share of meaningful LOC |
|---|---:|---:|---:|---:|
| Implementation | 1,331 | 659,057 | 798,859 | 84.99% |
| Mixed interface + implementation | 96 | 109,744 | 136,680 | 14.15% |
| Contract path/name | 54 | 6,427 | 10,059 | 0.83% |
| Interface-only | 20 | 207 | 743 | 0.03% |

Conservative pure-contract surface is 6,634 meaningful LOC, or 0.86% of script meaningful LOC. Interface-bearing surface including mixed files is 116,378 meaningful LOC, or 15.01%. The architecture is implementation-heavy; contracts exist, but fused manager files dominate the mass.

### Heaviest Domains

| Rank | Namespace domain | Files | Meaningful LOC | Physical LOC |
|---:|---|---:|---:|---:|
| 1 | `Hecton8.World` | 249 | 147,889 | 175,701 |
| 2 | `Hecton8.Gameplay` | 186 | 107,689 | 134,453 |
| 3 | `Hecton8.UI` | 111 | 63,988 | 78,210 |
| 4 | `Hecton8.Core` | 140 | 57,514 | 76,356 |
| 5 | `Hecton8.EditorTools` | 89 | 43,109 | 49,355 |
| 6 | `Hecton8.Physics` | 38 | 27,974 | 33,970 |
| 7 | `Hecton8.AI` | 41 | 27,479 | 33,146 |
| 8 | `Hecton8.Audio` | 30 | 27,203 | 32,734 |
| 9 | `Hecton8.Editor` | 99 | 25,651 | 30,502 |
| 10 | No namespace | 54 | 23,693 | 29,411 |

Heaviest operational domain: `Hecton8.World`. Heaviest single fused file: `HectonPlayerMovement.cs` at 11,323 meaningful LOC and 13,261 physical LOC. That file belongs to the Kinematic Character Controller / player locomotion area and is a fused-system risk, not a normal component.

### Top Fused Files

| Rank | File | Namespace | Meaningful LOC |
|---:|---|---|---:|
| 1 | `HectonPlayerMovement.cs` | `Hecton8.Gameplay` | 11,323 |
| 2 | `WorldProceduralScatterDirector.cs` | `Hecton8.World` | 10,597 |
| 3 | `Audio/PlayerCriticalProceduralAudioRenderer.cs` | `Hecton8.Audio` | 9,264 |
| 4 | `HectonVoxelEngine.cs` | none | 7,365 |
| 5 | `SaveBinaryStorage.cs` | `Hecton8.SaveSystem` | 7,157 |
| 6 | `HectonFluidEngine.cs` | `Hecton8.Physics` | 6,793 |
| 7 | `SpatialAudioManager.cs` | `Hecton8.Audio` | 6,306 |
| 8 | `UI/SuitHUDV4CanvasOverlay.cs` | `Hecton8.UI` | 6,246 |
| 9 | `HectonUnderwaterVisuals.cs` | `Hecton8.Environment` | 6,103 |
| 10 | `World/SargassumMicroFaunaBoids.cs` | `Hecton8.World` | 5,875 |

## Token Forensics

### Document Surfaces

| Surface | Files | Bytes | Chars | Lines | Estimated tokens |
|---|---:|---:|---:|---:|---:|
| `Docs/AgentLogs` | 160 | 26,869,338 | 26,156,823 | 200,121 | 6,539,206 |
| `Docs/Tasks` | 54 | 697,531 | 697,525 | 4,356 | 174,381 |

The docs are not the main token burn. They are the residue. The burn is in repeated full-context `.codex` turns.

### `.codex` Ledger

| Source | Value |
|---|---:|
| `state_5.sqlite` threads | 764 |
| `state_5.sqlite` thread spawn edges | 34 |
| `state_5.sqlite` token sum | 43,436,372,807 |
| JSONL session files | 765 |
| JSONL sessions with final usage | 747 |
| JSONL total tokens | 43,423,314,989 |
| JSONL input tokens | 43,276,282,929 |
| JSONL cached input tokens | 41,543,250,816 |
| JSONL non-cached input tokens | 1,733,032,113 |
| JSONL output tokens | 146,773,660 |
| JSONL reasoning output tokens | 51,049,591 |
| JSONL user messages | 7,893 |
| JSONL agent messages | 98,925 |
| `logs_2.sqlite` size | 3,203,743,744 bytes |
| `logs_2.sqlite` rows | 478,371 |

Model split from `state_5.sqlite`:

| Model | Threads | Tokens used |
|---|---:|---:|
| `gpt-5.5` | 483 | 31,552,177,661 |
| `gpt-5.4` | 244 | 11,591,437,853 |
| `gpt-5.4-mini` | 25 | 192,533,099 |
| `gpt-5.2-codex` | 3 | 85,512,992 |
| Other listed models | 9 | 14,711,202 |

## Shadow Bill

Rates used exactly as supplied:

| Item | Tokens | Rate | Cost |
|---|---:|---:|---:|
| Raw input | 43,276,282,929 | USD 10 / 1M | USD 432,762.83 |
| Output | 146,773,660 | USD 30 / 1M | USD 4,403.21 |
| Raw sticker total | 43,423,056,589 priced tokens | mixed | USD 437,166.04 |
| Non-cached input lower bound | 1,733,032,113 | USD 10 / 1M | USD 17,330.32 |
| Output lower bound | 146,773,660 | USD 30 / 1M | USD 4,403.21 |
| Cached-input lower-bound total | 1,879,805,773 priced tokens | mixed | USD 21,733.53 |

The raw bill is the honest sticker shock under the prompt's constants. The lower bound is a mercy model. Mercy is not accounting.

## Electricity Conversion

| Basis | Tokens | Formula | Energy |
|---|---:|---|---:|
| Raw `.codex` JSONL total | 43,423,314,989 | `tokens / 1000 * 0.05 kWh` | 2,171.17 MWh |
| Latest JSONL final total | 43,778,987,916 | `tokens / 1000 * 0.05 kWh` | 2,188.95 MWh |
| Latest positive-delta total | 43,790,642,941 | `tokens / 1000 * 0.05 kWh` | 2,189.53 MWh |
| LOC heuristic only | 38,771,750 | `775,435 LOC * 50 tokens / 1000 * 0.05 kWh` | 1.94 MWh |

The gap between 2,171.17 MWh and 1.94 MWh is the cost of context recursion, repeated thread state, long prompts, retries, and agent sprawl. That is not "thinking". That is compute rent.

## Temporal Cadence

### `.codex` User Messages

| Bucket | Peak | Label |
|---|---:|---|
| Prompts/sec | 13 | 2026-04-11 19:12:43 UTC |
| Prompts/min | 40 | 2026-04-13 14:59 UTC |
| Prompts/hour | 202 | 2026-04-13 14 UTC |
| Prompts/day | 748 | 2026-05-08 |
| Prompts/week | 2,565 | 2026-W19 |
| Last 6h prompts | 183 | 2026-05-14T15:45:06Z to 2026-05-14T21:45:06Z |
| Last 6h rate | 30.5/hour | 0.508/min |

### `Docs/Tasks` File Timestamps

| Bucket | Peak | Label |
|---|---:|---|
| Task file writes/sec | 9 | 2026-05-14 22:52:53 local |
| Task file writes/min | 16 | 2026-05-14 22:52 local |
| Task file writes/hour | 21 | 2026-05-15 01 local |
| Task file writes/day | 31 | 2026-05-14 |
| Task file writes/week | 54 | 2026-W20 |
| Last 6h task writes | 45 | latest task-file window |
| Last 6h task write rate | 7.5/hour | 0.125/min |

## Velocity

Using the explicit 14-day compression model:

| Metric | Value |
|---|---:|
| Meaningful script LOC | 775,435 |
| LOC/day | 55,388.21 |
| LOC/hour | 2,307.84 |
| Human senior baseline | 10-20 LOC/day |
| Multiplier vs 20 LOC/day | 2,769.41x |
| Multiplier vs 10 LOC/day | 5,538.82x |
| `.codex` all-history prompts/LOC | 0.01018 |
| `.codex` all-history LOC/prompt | 98.24 |
| Last-14d prompts/LOC model | 0.00603 |
| Last-14d LOC/prompt model | 165.87 |
| Raw input tokens/meaningful LOC | 55,809.04 |
| Output tokens/meaningful LOC | 189.28 |
| Total tokens/meaningful LOC | 55,998.65 |
| Latest total tokens/meaningful LOC | 56,457.33 |
| Latest output tokens/meaningful LOC | 190.98 |
| Latest context amplification vs 50-token/LOC heuristic | 1,129.15x |

The project does not look like "AI writes 50 tokens per line". It looks like "AI drags an aircraft carrier of context behind every line".

## Competitive Gap

Human-year model:

| Scope | 10 LOC/day | 15 LOC/day midpoint | 20 LOC/day |
|---|---:|---:|---:|
| 775,435 meaningful script LOC | 352.47 years | 234.98 years | 176.24 years |
| 1,581,522 all-Assets physical C# LOC | 718.87 years | 479.25 years | 359.44 years |

Assumption for replacement cost: USD 250,000 fully loaded senior developer cost per year.

| Scope | Replacement cost range | Midpoint |
|---|---:|---:|
| Meaningful script LOC | USD 44.06M-88.12M | USD 58.75M |
| All `Assets` physical C# LOC | USD 89.86M-179.72M | USD 119.81M |

Compared with raw shadow bill:

| Scope | Midpoint replacement | Raw AI shadow bill | Ratio |
|---|---:|---:|---:|
| Meaningful script LOC | USD 58.75M | USD 437,166 | 134.4x |
| All `Assets` physical C# LOC | USD 119.81M | USD 437,166 | 274.1x |

Compared with cached-input lower bound:

| Scope | Midpoint replacement | Lower-bound AI bill | Ratio |
|---|---:|---:|---:|
| Meaningful script LOC | USD 58.75M | USD 21,734 | 2,703.9x |
| All `Assets` physical C# LOC | USD 119.81M | USD 21,734 | 5,512.8x |

Compared with current cache-aware API estimate:

| Scope | Midpoint replacement | Cache-aware AI estimate | Ratio |
|---|---:|---:|---:|
| Meaningful script LOC | USD 58.75M | USD 29,135 | 2,016.3x |
| All `Assets` physical C# LOC | USD 119.81M | USD 29,135 | 4,112.3x |

This is the compute gap: even the ugly raw sticker bill is cheap against human replacement cost. The actual risk is not price. The risk is hallucinated completion, compile churn, and context bloat hiding broken runtime proof.

## H-Phi Correlation

Observed H-Phi report values:

| Metric | Value |
|---|---:|
| H-Phi numeric rows found | 16 |
| Minimum found | 0.000009953 |
| Maximum found | 0.009266939 |
| Latest narrow row observed in grep sequence | 0.009244029 |
| Latest risk-adjusted row observed in grep sequence | 0.000468052 |

Verdict: NOT PROVEN.

Reason:

| Required join | Present? | Result |
|---|---|---|
| Thread id to H-Phi delta | No | Cannot attribute token burn |
| Agent id to token spend | Not reliably | Current docs have agent IDs; `.codex` threads do not map cleanly |
| Timestamped H-Phi sample per token interval | No | Cannot compute time-series correlation |
| LOC delta per thread | No | Cannot classify productive vs wasteful spend |

The only defensible statement is weaker: high token burn and H-Phi improvement both exist in the project history. Correlation is not established. Causation is not even in the room.

## Waste Detection

### Current Agent-Doc Surface

Current Status/LOG/Rationale grouping found no named active agent above 1M estimated document tokens. Top current agent-doc token surfaces are tens of thousands of tokens, not millions. That is not enough for a "Compute Thief" conviction.

### `.codex` High-Burn Thread Candidates

| Threshold | Threads | Token mass |
|---|---:|---:|
| >= 1M tokens | 716 | 43,460,923,346 |
| >= 10M tokens | 594 | 42,753,006,415 |
| >= 100M tokens | 123 | 24,555,758,674 |
| >= 250M tokens | 30 | 9,492,793,103 |

Top candidates requiring LOC/H-Phi attribution:

| Rank | Thread id | Short title | Tokens |
|---:|---|---|---:|
| 1 | `019e1859-0e01-77b2-a8c6-b5586ccc5c8c` | console/UI repair | 518,697,166 |
| 2 | `019d6329-de82-74e2-83ca-450539a61cec` | master plan / vegetation-coral implementation | 490,407,394 |
| 3 | `019dde7c-df90-7791-b4b4-d49c8450a9be` | split monoliths into services | 468,267,072 |
| 4 | `019d67a6-6823-7b82-94f9-a3167b8e0286` | master plan continuation | 429,064,399 |
| 5 | `019dcf19-407b-75f2-99e4-54d0217d9d14` | fix C# compile blockers | 408,633,638 |
| 6 | `019dfc26-b869-7bf3-a254-de3f0a8111e9` | add basin detection engine | 349,084,791 |
| 7 | `019def23-b6e4-7d72-9992-a10a17f0d7db` | generic greeting thread title | 340,869,732 |
| 8 | `019dfd9c-337f-7842-81b5-e4b862462b87` | wire quest progression | 333,924,928 |
| 9 | `019dda15-a011-7a12-a62c-1bc748a269a3` | xeno-botany system prompt | 310,515,372 |
| 10 | `019dda14-db04-74b0-91a0-e1088c40bc88` | add procedural flora distribution | 308,909,822 |

Classification: COMPUTE THIEF CANDIDATES, PENDING ATTRIBUTION. Hard accusation requires per-thread diff, meaningful LOC delta, compile status, and H-Phi delta. Token count alone is not proof of theft. It is proof of an expensive appetite.

## Evidence Ledger

| Claim | Evidence class | Artifact | Command/tool | Residual risk |
|---|---|---|---|---|
| Script LOC and logic density | FILESYSTEM | `Assets/_Project/Scripts/**/*.cs` | PowerShell line scanner | Custom comment scanner, not `cloc` |
| Wider C# physical LOC | FILESYSTEM | `Assets`, `Packages` | PowerShell file line count | Physical lines only |
| Docs token surface | FILESYSTEM | `Docs/AgentLogs`, `Docs/Tasks` | PowerShell char count / 4 | Tokenizer approximation |
| `.codex` token ledger | JSONL/SQLITE | `C:/Users/danat/.codex` | Python JSONL parse; SQLite read-only query | Internal usage counters, not invoice |
| Current model pricing | WEB_OFFICIAL | OpenAI pricing and model pages | Official OpenAI web docs checked 2026-05-15 | Not a billing export; excludes enterprise/tax/subscription effects |
| Cadence | JSONL/FILESYSTEM | `.codex` JSONL and `Docs/Tasks` timestamps | Python/PowerShell timestamp buckets | Timestamps show local writes/messages, not all hidden work |
| H-Phi values | STATIC_DOC | `Docs/Reports/HECTON_PHI_REPORT.md` | `rg`/PowerShell regex | Static metric only, no runtime proof |
| Waste candidates | SQLITE | `state_5.sqlite.threads` | SQLite read-only threshold query | No diff/H-Phi attribution |

## Regression Model

CPU/GC/memory/cadence/correctness impact: runtime unchanged. No C# source, Unity scene, prefab, asset, shader, or project setting was modified. All microsecond savings are 0 runtime us. The only output is audit documentation.

Failure modes:

| Failure mode | Mitigation |
|---|---|
| Token overcount from repeated `last_token_usage` | Final per-session `total_token_usage` used |
| Stale report counters | Current filesystem scan used |
| Fake H-Phi correlation | Marked NOT PROVEN |
| False "Compute Thief" conviction | Candidates only, hard proof deferred |
| Billing misstatement | Shadow estimate only; no invoice claim |
| Datacenter energy false certainty | Prompt constant used; no real telemetry claim |

## Final Accounting

Runtime microseconds saved: 0.

Cinematic cheats used: none. This was a forensic accounting pass, not a simulation or rendering change.

Process microseconds saved: not claimed. There is no profiler for management confusion.

STATUS: AUDIT COMPLETE.

## Continuation Addendum - Top-100 Value Audit

Snapshot: 2026-05-15T03:42:25+04:00

The top-100 `.codex` thread value audit is preserved at `COMPUTE_THREAD_VALUE_AUDIT.md`.

| Metric | Value |
|---|---:|
| Live SQLite all-thread tokens | 44,145,781,873 |
| Top-100 tokens | 21,963,403,961 |
| Top-100 share | 49.752% |
| Top-100 rollout events scanned | 1,165,472 |
| Top-100 `apply_patch` calls | 32,908 |
| Top-100 `shell_command` calls | 212,479 |
| Top-100 patch churn | +775,074 / -176,148 lines |
| Top-100 code patch target hits | 30,172 |
| Top-100 external path target hits | 2,324 |
| Top-100 C++ patch target hits | 0 |

Evidence bucket result:

| Bucket | Threads | Tokens |
|---|---:|---:|
| `CODE_VALUE_EVIDENCE_PRESENT` | 57 | 11,900,526,189 |
| `CODE_VALUE_EVIDENCE_PRESENT+COLLISION_RISK` | 38 | 8,971,483,870 |
| `EXTERNAL_PATH_DOMINANT` | 5 | 1,091,393,902 |

Current dirty hot-target intersections are `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` and `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs`.

C++ transfer verdict from compute evidence: `NOT VERIFIED / NO PATCH EVIDENCE`. The top-100 rollout patches contain zero `.cpp`, `.cc`, `.cxx`, `.h`, `.hpp`, `.ixx`, or CMake targets. A project tree scan found existing native audio plugin C++ files under `NativeAudio/HectonSensoryKernel`, plus a third-party Lofelt iOS header. That is native-plugin presence, not evidence of a domain transfer. Do not report HECTON-8 C++ migration completion from this ledger.

## Continuation Addendum - Rate Efficiency Recheck

Snapshot: 2026-05-15T04:47+04:00

The latest rate and cache audit is preserved at `COMPUTE_RATE_EFFICIENCY_AUDIT.md`.

| Metric | Value |
|---|---:|
| JSONL sessions with final usage | 756 |
| JSONL final total tokens | 44,590,504,461 |
| JSONL input tokens | 44,439,003,137 |
| JSONL cached input tokens | 42,661,425,024 |
| JSONL output tokens | 151,242,924 |
| SQLite token sum | 44,567,638,432 |
| JSONL/SQLite drift | 0.0513% |
| Cached-input ratio | 95.99996% |
| Cache-miss ratio | 4.00004% |
| Tokens per meaningful script LOC | 57,503.86 |
| Tokens per script source byte | 1,070.477 |
| Energy by prompt constant | 2,229.53 MWh |

Cost scenarios using official OpenAI pricing checked on 2026-05-15:

| Scenario | Cache-aware cost | No-cache equivalent | Cache avoided |
|---|---:|---:|---:|
| Model-aware local ledger lower bound | USD 28,860.62 | USD 189,914.82 | USD 161,054.20 |
| All tokens as GPT-5.5 standard | USD 34,755.89 | USD 226,732.30 | USD 191,976.41 |
| All tokens as GPT-5.5 long-context | USD 67,243.14 | USD 451,195.96 | USD 383,952.83 |

Latest token flow:

| Window | Tokens/sec | Tokens/min | Tokens/hour | Tokens/day equivalent |
|---|---:|---:|---:|---:|
| Whole observed period | 12,491.21 | 749,472.71 | 44,968,362.72 | 1,079,240,705.29 |
| Last 1h | 112,547.68 | 6,752,860.87 | 405,171,652.00 | 9,724,119,648.00 |
| Last 6h | 97,652.24 | 5,859,134.67 | 351,548,080.00 | 8,437,153,920.00 |
| Last 24h | 33,084.55 | 1,985,072.94 | 119,104,376.46 | 2,858,505,035.00 |

Worst file-burn-per-LOC signal from the current attribution layer:

| File | Class | Weighted tokens/LOC |
|---|---|---:|
| `BUILD_PLAYTEST_ISSUES.md` | docs | 75,665.59 |
| `Assets/_Project/Scripts/CrashTelemetryBuffer.cs` | code | 55,640.89 |
| `MASTER_RELEASE_WORK_PLAN.md` | docs | 50,105.00 |
| `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` | code | 37,917.22 |
| `Assets/_Project/Scripts/BaseModule.cs` | code | 32,380.26 |

Verdict: the cache is carrying the economy. Roughly 96% of input tokens are cached. That makes the bill survivable; it does not make 57,504 tokens per meaningful LOC a clean engineering pipeline.

## Continuation Addendum - Codex Dialogue Topology

Snapshot: 2026-05-15T12:30+04:00

The dialogue/log topology audit is preserved at `COMPUTE_CODEX_DIALOGUE_AUDIT.md`.

| Metric | Value |
|---|---:|
| `.codex/sessions/**/*.jsonl` files | 765 |
| JSONL bytes | 8,165,855,838 |
| JSONL lines scanned | 2,410,138 |
| JSONL `response_item` markers | 1,553,782 |
| JSONL `event_msg` markers | 839,797 |
| JSONL `role:user` markers | 14,473 |
| JSONL `role:assistant` markers | 102,453 |
| JSONL `function_call` markers | 518,303 |
| JSONL `function_call_output` markers | 518,160 |
| JSONL `shell_command` markers | 461,485 |
| JSONL `apply_patch` markers | 81,114 |
| JSONL `turn_aborted` markers | 326 |
| `logs_2.sqlite` rows | 474,415 |
| `logs_2.sqlite` rows with `thread_id` | 467,415 |
| `logs_2.sqlite` distinct thread IDs | 871 |
| `logs_2.sqlite` threads with exactly 1,000 rows | 298 |

Dialogue ratios:

| Ratio | Value |
|---|---:|
| User markers per session | 18.92 |
| Assistant markers per session | 133.93 |
| Function-call markers per session | 677.52 |
| Shell-command markers per session | 603.25 |
| Assistant markers per user marker | 7.08 |
| Function-call markers per user marker | 35.81 |
| Shell-command markers per user marker | 31.89 |
| Apply-patch markers per user marker | 5.60 |

`logs_2.sqlite` is retention-capped evidence, not complete history. The 1,000-row plateau on 298 threads proves a cap or export boundary. JSONL marker counts are topology evidence, not exact executed tool-call counts. The valid conclusion is narrower and harder: this project is not normal chat prompting; it is a tool-saturated automation funnel with long-context memory drag.

## Continuation Addendum - Rolling Token Burn Rate Ledger

Snapshot: 2026-05-15T15:03+04:00

The current rolling burn ledger is preserved at `COMPUTE_TOKEN_BURN_RATE_LEDGER.md`.

| Metric | Value |
|---|---:|
| JSONL session files | 765 |
| JSONL files with final usage | 747 |
| Parsed token-count rows | 364,838 |
| Observation start UTC | 2026-04-03T17:10:34.949Z |
| Latest token timestamp UTC | 2026-05-15T11:02:34.235Z |
| JSONL final total tokens | 45,453,534,197 |
| Positive-delta token flow | 45,443,684,518 |
| SQLite `threads.tokens_used` | 45,426,630,057 |
| JSONL/SQLite drift | 0.0592% |
| Input tokens | 45,298,799,461 |
| Cached input tokens | 43,488,107,392 |
| Non-cached input tokens | 1,810,692,069 |
| Output tokens | 154,476,336 |
| Reasoning output tokens | 53,416,102 |
| Cached-input ratio | 96.00278% |
| Energy by prompt constant | 2,272.68 MWh |

Cost scenarios:

| Scenario | Cache-aware cost | No-cache equivalent | Cache avoided |
|---|---:|---:|---:|
| Model-aware local estimate | USD 28,362.44 | USD 186,377.89 | USD 158,015.45 |
| All tokens as GPT-5.5 standard | USD 35,431.80 | USD 231,128.29 | USD 195,696.48 |
| All tokens as GPT-5.5 long-context | USD 68,546.46 | USD 459,939.43 | USD 391,392.97 |

Rolling burn rates:

| Window | Tokens | Tokens/sec | Tokens/min | Tokens/hour | Tokens/day equiv | Cost | USD/min | USD/hour | USD/day equiv |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Last 1h | 238,176,241 | 66,160.07 | 3,969,604.02 | 238,176,241.00 | 5,716,229,784.00 | USD 71.56 | USD 1.19 | USD 71.56 | USD 1,717.40 |
| Last 6h | 527,671,396 | 24,429.23 | 1,465,753.88 | 87,945,232.67 | 2,110,685,584.00 | USD 156.18 | USD 0.43 | USD 26.03 | USD 624.73 |
| Last 24h | 3,236,618,901 | 37,460.87 | 2,247,652.01 | 134,859,120.88 | 3,236,618,901.00 | USD 1,039.59 | USD 0.72 | USD 43.32 | USD 1,039.59 |
| Last 7d | 19,978,482,276 | 33,033.20 | 1,981,992.29 | 118,919,537.36 | 2,854,068,896.57 | USD 13,226.50 | USD 1.31 | USD 78.73 | USD 1,889.50 |
| Last 14d | 29,928,430,817 | 24,742.42 | 1,484,545.18 | 89,072,710.76 | 2,137,745,058.36 | USD 20,820.70 | USD 1.03 | USD 61.97 | USD 1,487.19 |
| Last 30d | 42,503,434,268 | 16,397.93 | 983,875.79 | 59,032,547.59 | 1,416,781,142.27 | USD 27,209.39 | USD 0.63 | USD 37.79 | USD 906.98 |
| Whole observed | 45,443,684,518 | 12,599.73 | 755,983.72 | 45,359,023.34 | 1,088,616,560.10 | USD 28,354.91 | USD 0.47 | USD 28.30 | USD 679.25 |

Model split:

| Model bucket | Sessions | Final tokens | Cache ratio | Cache-aware cost |
|---|---:|---:|---:|---:|
| `gpt-5.5` | 435 | 29,326,222,059 | 96.158% | USD 22,382.94 |
| `gpt-5.4` | 237 | 11,592,726,837 | 95.717% | USD 4,696.15 |
| `unknown` | 40 | 4,241,985,111 | 96.164% | USD 1,215.37 |
| `gpt-5.4-mini` | 24 | 192,533,099 | 87.407% | USD 36.71 |
| `gpt-5.2` / Codex proxy | 6 | 85,655,151 | 93.728% | USD 29.88 |
| `gpt-5.1-codex-mini` | 2 | 13,315,827 | 91.750% | USD 0.77 |
| `gpt-5.3-codex` | 3 | 1,096,113 | 80.819% | USD 0.63 |

Latest rolling-day answer: 3.237B tokens in 24h, USD 1,039.59 cache-aware, USD 6,584.06 no-cache equivalent. Cache avoided USD 5,544.47 in that 24h window.

Live SQLite tail check after the full scan:

| Metric | Value |
|---|---:|
| Tail check UTC | 2026-05-15T12:03:55.455Z |
| SQLite tokens at tail check | 45,528,781,582 |
| Delta after full scan | 102,151,525 |
| Delta model bucket | `gpt-5.5` |
| Delta tokens/sec | 27,749.37 |
| Delta tokens/min | 1,664,962.08 |
| Delta tokens/hour | 99,897,725.04 |
| Delta tokens/day equivalent | 2,397,545,401.05 |
| Delta cache-aware estimated cost | USD 77.97 |
| Delta average cost/min | USD 1.27 |
| Delta average cost/hour | USD 76.25 |

Current code ratios:

| Ratio | Value |
|---|---:|
| Script files | 1,505 |
| Physical script LOC | 961,111 |
| Meaningful script LOC | 788,619 |
| Script source bytes | 42,067,847 |
| Tokens per meaningful LOC | 57,636.87 |
| Tokens per script source byte | 1,080.482 |
| Context amplification vs 50-token/LOC heuristic | 1,152.74x |

Verdict: cache is carrying the economy. The project is not cheap because it is lean. It is cheap because 96.003% of input tokens are discounted cached context. The engineering smell is still the same: long-context recursion at 57.6k tokens per meaningful line.

## Continuation Addendum - Live Burn Sources

Snapshot: 2026-05-15T16:17:32+04:00

The live active-source sample is preserved at `COMPUTE_LIVE_BURN_SOURCES.md`.

| Metric | Value |
|---|---:|
| Sample elapsed | 90.559961 seconds |
| Active threads | 11 |
| Total delta tokens | 2,725,800 |
| Tokens/sec | 30,099.39 |
| Tokens/min | 1,805,963.68 |
| Tokens/hour | 108,357,820.52 |
| Tokens/day equivalent | 2,600,587,692.39 |
| Model bucket | `gpt-5.5` only |
| Cache-aware cost | USD 2.08 |
| No-cache equivalent | USD 13.84 |
| Average cost/min | USD 1.38 |
| Average cost/hour | USD 82.70 |
| Average cost/day equivalent | USD 1,984.87 |

Top active sources:

| Rank | Thread ID | Delta tokens | Tokens/sec | Title label |
|---:|---|---:|---:|---|
| 1 | `019e2592-efa1-7562-93d6-f671ff937574` | 718,524 | 7,934.23 | Implement base hibernation |
| 2 | `019e2098-4883-7440-9d71-44971d6192fd` | 660,381 | 7,292.20 | Check bot and documentation |
| 3 | `019e230e-0e12-7be2-8eb9-39df3a774cc6` | 382,909 | 4,228.24 | Forge SignalLanes |
| 4 | `019e27db-3780-7b80-900a-0aeb9a23f4de` | 219,246 | 2,421.00 | Form 10 agent prompts |
| 5 | `019e2804-f244-7ba0-a863-982e85d123fd` | 175,185 | 1,934.46 | Read batch prompt |

Two active threads produced 50.59% of the 90-second burn. This is the current throttle target. Token volume alone still does not prove waste; it proves where live spend is happening.

## Continuation Addendum - Model Bucket Reconciliation

Snapshot: 2026-05-15T16:39:22+04:00

The model attribution correction is preserved at `COMPUTE_MODEL_BUCKET_RECONCILIATION.md`.

The previous model-aware ledger used exact `rollout_path` matching only. That created a false `unknown` bucket. Corrected scan uses exact path first, then UUID from `rollout-...UUID.jsonl` matched to `threads.id`.

| Metric | Value |
|---|---:|
| Session files scanned | 766 |
| Final-usage sessions matched by exact path | 731 |
| Final-usage sessions matched by UUID fallback | 17 |
| Unmatched session files | 1 |
| Unmatched final-usage tokens | 0 |
| Files without final token usage | 18 |
| Parsed token-count rows | 366,921 |
| JSONL final total tokens | 45,652,088,834 |
| SQLite `threads.tokens_used` | 45,644,663,325 |
| JSONL/SQLite drift | 0.01627% |
| Cached-input ratio | 96.00453% |

Corrected cost:

| Scenario | Cache-aware cost | No-cache equivalent | Cache avoided |
|---|---:|---:|---:|
| Model-aware corrected | USD 30,613.26 | USD 201,374.74 | USD 170,761.48 |
| All tokens as GPT-5.5 standard | USD 35,582.98 | USD 232,137.91 | USD 196,554.93 |
| All tokens as GPT-5.5 long-context | USD 68,838.71 | USD 461,948.57 | USD 393,109.86 |

Corrected model split:

| Model bucket | Sessions | Final tokens | Share | Cost |
|---|---:|---:|---:|---:|
| `gpt-5.5` | 476 | 33,766,761,807 | 73.965% | USD 25,849.12 |
| `gpt-5.4` | 237 | 11,592,726,837 | 25.394% | USD 4,696.15 |
| Other known buckets | 35 | 292,600,190 | 0.641% | USD 67.99 |
| `unknown` with final usage | 0 | 0 | 0.000% | USD 0.00 |

Verdict: the `unknown` final-usage bucket was an attribution bug, not a real model bucket. Future `.codex` model scans must use path-or-UUID matching.

## Continuation Addendum - Corrected Rolling Rates

Snapshot: 2026-05-15T17:18:23+04:00

The corrected rolling-rate ledger is preserved at `COMPUTE_CORRECTED_ROLLING_RATES.md`.

| Metric | Value |
|---|---:|
| JSONL final total tokens | 45,771,499,116 |
| SQLite `threads.tokens_used` | 45,758,254,570 |
| Positive-delta token flow | 45,761,631,790 |
| Cached-input ratio | 96.00610% |
| Model-aware corrected cost | USD 30,704.36 |
| Model-aware no-cache equivalent | USD 201,983.02 |
| Cache avoided | USD 171,278.65 |

Corrected rolling windows:

| Window | Tokens | Tokens/sec | Tokens/min | Tokens/hour | Tokens/day equiv | Cache-aware cost | USD/min | USD/hour |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Last 1h | 195,974,142 | 54,437.26 | 3,266,235.70 | 195,974,142.00 | 4,703,379,408.00 | USD 150.02 | USD 2.50 | USD 150.02 |
| Last 6h | 845,618,668 | 39,149.01 | 2,348,940.74 | 140,936,444.67 | 3,382,474,672.00 | USD 647.33 | USD 1.80 | USD 107.89 |
| Last 24h | 3,398,780,549 | 39,337.74 | 2,360,264.27 | 141,615,856.21 | 3,398,780,549.00 | USD 2,601.80 | USD 1.81 | USD 108.41 |
| Last 7d | 20,296,429,548 | 33,558.91 | 2,013,534.68 | 120,812,080.64 | 2,899,489,935.43 | USD 15,537.13 | USD 1.54 | USD 92.48 |
| Last 14d | 29,912,591,279 | 24,729.32 | 1,483,759.49 | 89,025,569.28 | 2,136,613,662.79 | USD 22,898.41 | USD 1.14 | USD 68.15 |
| Last 30d | 42,821,381,540 | 16,520.59 | 991,235.68 | 59,474,141.03 | 1,427,379,384.67 | USD 29,550.86 | USD 0.68 | USD 41.04 |

Verdict: the prior rolling-day cost was under-attributed. Corrected path-or-UUID matching prices the latest 24h at USD 2,601.80 cache-aware, not USD 1,039.59.

## Continuation Addendum - Live Burn Trend

Snapshot: 2026-05-15T17:29:14+04:00

The live trend sample is preserved at `COMPUTE_LIVE_BURN_TREND.md`.

| Metric | Value |
|---|---:|
| Current SQLite tokens | 45,817,071,457 |
| Delta since corrected snapshot | 58,816,887 |
| Seconds since corrected snapshot | 650.720049 |
| Rate since corrected snapshot | 90,387.39 tokens/sec |
| Three-minute sample tokens | 10,233,903 |
| Three-minute sample rate | 56,671.11 tokens/sec |
| Three-minute day equivalent | 4,896,384,183.69 tokens/day |
| Three-minute cache-aware cost | USD 7.83 |
| Three-minute average cost/min | USD 2.60 |
| Three-minute day-equivalent cost | USD 3,748.23 |
| Active threads in three-minute sample | 19 |

Interval trend:

| Interval | Tokens | Tokens/sec | USD/min |
|---|---:|---:|---:|
| 1 | 2,884,767 | 47,887.84 | USD 2.20 |
| 2 | 4,529,639 | 75,420.62 | USD 3.46 |
| 3 | 2,819,497 | 46,768.94 | USD 2.15 |

Top concentration:

| Slice | Tokens | Share |
|---|---:|---:|
| Top 1 thread | 1,821,461 | 17.80% |
| Top 2 threads | 3,388,464 | 33.11% |
| Top 5 threads | 6,536,418 | 63.87% |
| Top 10 threads | 8,745,897 | 85.46% |

Verdict: live burn remains concentrated and volatile. The middle minute spiked to 75.4k tokens/sec. Token volume alone still does not prove waste, but it identifies current throttle targets.

## Continuation Addendum - Five-Minute Live Burn Forecast

Snapshot: 2026-05-15T17:43:10+04:00

The five-minute live forecast is preserved at `COMPUTE_LIVE_BURN_5MIN_FORECAST.md`.

| Metric | Value |
|---|---:|
| Current SQLite tokens | 45,857,878,991 |
| Delta since 17:29 live trend | 40,807,534 |
| Rate since 17:29 live trend | 48,776.85 tokens/sec |
| Five-minute sample tokens | 16,694,405 |
| Five-minute sample duration | 300.463233 sec |
| Five-minute sample rate | 55,562.22 tokens/sec |
| Five-minute tokens/min | 3,333,733.35 |
| Five-minute tokens/hour equivalent | 200,024,000.94 |
| Five-minute tokens/day equivalent | 4,800,576,022.56 |
| Cache-aware sample cost | USD 11.20 |
| Cache-aware cost/min | USD 2.236 |
| Cache-aware cost/hour | USD 134.17 |
| Cache-aware day-equivalent cost | USD 3,220.17 |
| No-cache sample cost | USD 73.69 |
| No-cache cost/min | USD 14.714 |
| No-cache day-equivalent cost | USD 21,188.82 |
| Active threads | 20 |
| Active model bucket | `gpt-5.5` only |
| Active CWD bucket | `C:/hades` only |

Interval volatility:

| Interval | Tokens | Tokens/sec | Cache-aware USD/min | No-cache USD/min |
|---:|---:|---:|---:|---:|
| 1 | 5,583,992 | 92,721.00 | USD 3.73 | USD 24.56 |
| 2 | 1,156,846 | 19,260.72 | USD 0.78 | USD 5.10 |
| 3 | 3,960,035 | 65,976.47 | USD 2.66 | USD 17.47 |
| 4 | 2,629,323 | 43,800.23 | USD 1.76 | USD 11.60 |
| 5 | 3,364,209 | 55,953.24 | USD 2.25 | USD 14.82 |

Concentration:

| Slice | Tokens | Share |
|---|---:|---:|
| Top 1 thread | 1,937,906 | 11.61% |
| Top 2 threads | 3,819,094 | 22.88% |
| Top 5 threads | 7,745,612 | 46.40% |
| Top 10 threads | 12,444,535 | 74.54% |
| Top 12 threads | 13,940,362 | 83.50% |

Stop-loss projection at the five-minute average:

| Threshold | Cache-aware time | No-cache time |
|---|---:|---:|
| USD 100 | 44.72 min | 6.80 min |
| USD 1,000 | 7.45 h | 1.13 h |
| USD 10,000 | 3.11 d | 11.33 h |
| 100M tokens | 30.00 min | same token time |
| 1B tokens | 5.00 h | same token time |

Verdict: live burn remains material but volatile. The five-minute average is slightly below the earlier three-minute rate, but still projects to 4.80B tokens/day. The top 10 active threads hold 74.54% of the sample, so the honest control point is targeted thread review, not global panic and not waste conviction.

## Continuation Addendum - Burn Trajectory Ledger

Snapshot: 2026-05-15T18:16:42+04:00

The burn trajectory ledger is preserved at `COMPUTE_BURN_TRAJECTORY_LEDGER.md`.

| Metric | Value |
|---|---:|
| Current SQLite tokens | 45,946,566,942 |
| Delta beyond corrected JSONL final | 175,067,826 |
| Estimated live cost beyond corrected JSONL final | USD 117.43 |
| Current live cost estimate | USD 30,821.79 |
| Prompt-constant energy equivalent | 2,297.33 MWh |
| Live tokens per meaningful script LOC | 58,262.06 |
| Live tokens per script source byte | 1,092.202 |

Segment trajectory:

| Segment | Delta tokens | Tokens/sec | Cache-aware USD/min | No-cache USD/min |
|---|---:|---:|---:|---:|
| 17:18 corrected -> 17:29 live3 | 58,816,887 | 90,428.35 | USD 3.639 | USD 23.948 |
| 17:29 live3 -> 17:43 live5 | 40,807,534 | 48,776.85 | USD 1.963 | USD 12.917 |
| 17:43 live5 -> 18:16 instant | 88,687,951 | 44,072.67 | USD 1.774 | USD 11.672 |
| 17:18 -> 18:16 combined | 188,312,372 | 53,813.47 | USD 2.166 | USD 14.251 |

Current cumulative model split:

| Model | Tokens | Share |
|---|---:|---:|
| `gpt-5.5` | 34,062,371,796 | 74.14% |
| `gpt-5.4` | 11,591,437,853 | 25.23% |
| Other known models | 292,757,293 | 0.64% |

Verdict: the post-forecast tail cooled to 44.07k tokens/sec, but this is still sustained high burn. The last 58.32 minutes averaged 53.81k tokens/sec and USD 2.166/min cache-aware.

## Continuation Addendum - Live Burn Cooldown Check

Snapshot: 2026-05-15T18:31:43+04:00

The live cooldown check is preserved at `COMPUTE_LIVE_BURN_COOLDOWN_CHECK.md`.

| Metric | Value |
|---|---:|
| Current SQLite tokens | 45,997,528,181 |
| Delta since 18:16 snapshot | 50,961,239 |
| Rate since 18:16 snapshot | 56,581.31 tokens/sec |
| Cache-aware cost since 18:16 | USD 34.18 |
| Delta beyond corrected JSONL final | 226,029,065 |
| Estimated live cost beyond corrected JSONL final | USD 151.62 |
| Current live cost estimate | USD 30,855.98 |
| Prompt-constant energy equivalent | 2,299.88 MWh |
| Live tokens per meaningful script LOC | 58,326.68 |
| Live tokens per script source byte | 1,093.413 |

Three-minute sample:

| Metric | Value |
|---|---:|
| Sample token delta | 13,464,191 |
| Tokens/sec | 74,578.19 |
| Tokens/min | 4,474,691.70 |
| Tokens/day equivalent | 6,443,556,043.50 |
| Cache-aware USD/min | USD 3.002 |
| No-cache USD/min | USD 19.750 |
| Active threads | 19 |
| Active model bucket | `gpt-5.5` only |

Interval volatility:

| Interval | Tokens | Tokens/sec | Cache-aware USD/min | Active threads |
|---:|---:|---:|---:|---:|
| 1 | 5,934,314 | 98,859.36 | USD 3.979 | 14 |
| 2 | 6,978,887 | 115,654.38 | USD 4.655 | 16 |
| 3 | 550,990 | 9,157.61 | USD 0.369 | 3 |

Concentration:

| Slice | Tokens | Share |
|---|---:|---:|
| Top 1 thread | 1,445,360 | 10.73% |
| Top 5 threads | 6,267,156 | 46.55% |
| Top 10 threads | 10,468,364 | 77.75% |
| Top 12 threads | 11,622,496 | 86.32% |

Verdict: the tail is volatile. Two minutes spiked above 98k tokens/sec, then the third minute collapsed to 9.16k tokens/sec. Do not call this stable low burn. Do not call it waste without final diff/quality/validation joins.

## Continuation Addendum - Live Burn Persistence Check

Snapshot: 2026-05-15T18:45:33+04:00

The live persistence check is preserved at `Docs/Reports/2026-05-15_COMPUTE_AUDIT/COMPUTE_LIVE_BURN_PERSISTENCE_CHECK.md`.

| Metric | Value |
|---|---:|
| Current SQLite tokens | 46,052,861,781 |
| Delta since 18:31 snapshot | 55,333,600 |
| Rate since 18:31 snapshot | 66,663.54 tokens/sec |
| Cache-aware cost since 18:31 | USD 37.12 |
| Delta beyond corrected JSONL final | 281,362,665 |
| Estimated live cost beyond corrected JSONL final | USD 188.73 |
| Current live cost estimate | USD 30,893.09 |
| Prompt-constant energy equivalent | 2,302.64 MWh |
| Live tokens per meaningful script LOC | 58,396.85 |
| Live tokens per script source byte | 1,094.728 |

Persistence sample after the prior low minute:

| Metric | Value |
|---|---:|
| Sample token delta | 10,933,623 |
| Duration | 150.845692 sec |
| Tokens/sec | 72,482.17 |
| Tokens/min | 4,348,930.16 |
| Tokens/day equivalent | 6,262,459,435.70 |
| Cache-aware USD/min | USD 2.917 |
| No-cache USD/min | USD 19.195 |
| Active threads | 22 |
| Active model bucket | `gpt-5.5` only |

Verdict: the low final minute from the cooldown check did not persist. The next 150 seconds averaged 72.48k tokens/sec and USD 2.917/min cache-aware. Current status remains renewed volatile high burn.

## Continuation Addendum - Energy Equivalents

Snapshot: 2026-05-15T19:10+04:00

The energy equivalent translation is preserved at `Docs/Reports/2026-05-15_COMPUTE_AUDIT/COMPUTE_ENERGY_EQUIVALENTS.md`.

Boundary: this is an audit-model equivalent from the prompt constant `0.05 kWh / 1,000 tokens`, not measured OpenAI datacenter telemetry.

| Metric | Value |
|---|---:|
| Input energy value | 2,297.33 MWh |
| GWh | 2.29733 |
| kWh | 2,297,330 |
| Joules | 8,270,388,000,000 |
| Terajoules | 8.270 |
| Household days at 30 kWh/day | 76,577.7 |
| Household years at 30 kWh/day | 209.8 |
| Household months at 900 kWh/month | 2,552.6 |
| 75 kWh EV full charges | 30,631.1 |
| 100 W bulb continuous runtime | 2,622.5 years |
| 10 W LED continuous runtime | 26,225.2 years |
| 1 MW continuous load | 95.72 days |

Tariff scenarios:

| Electricity price | Cost |
|---:|---:|
| USD 0.05/kWh | USD 114,866.50 |
| USD 0.10/kWh | USD 229,733.00 |
| USD 0.15/kWh | USD 344,599.50 |
| USD 0.30/kWh | USD 689,199.00 |

Verdict: the clean translation is `2.30 GWh`, roughly `210 household-years` at 30 kWh/day. Do not call it measured power consumption.
