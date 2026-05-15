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
