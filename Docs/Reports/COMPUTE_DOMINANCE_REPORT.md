# COMPUTE DOMINANCE REPORT

Status: AUDIT COMPLETE
Agent: COMPUTE_LOGISTICS_AUDITOR
Domain: Echelon 9 / Meta, Audit, Reporting, Evidence Accounting
Audit timestamp: 2026-05-15T01:49:02+04:00
Evidence class: FILESYSTEM / STATIC_DOC / SQLITE / JSONL. No Unity runtime, profiler, GCMonitor, or billing export proof.

## Executive Verdict

The current first-party script surface is not 1.63M meaningful LOC. It is 775,435 meaningful LOC under `Assets/_Project/Scripts`, 946,341 physical script LOC, and 1,581,522 physical C# LOC under all `Assets`. The 1.63M claim is close to all-Assets physical C# plus drift, not meaningful first-party logic.

The `.codex` ledger is the economic anomaly: 764 thread rows in `state_5.sqlite`, 43.436B recorded `tokens_used`, and 765 JSONL session files occupying about 8.0GB. Final JSONL session totals cross-check at 43.423B total tokens. The raw sticker shadow bill at the supplied GPT-5.5 Spud rates is about USD 437,166.04. If cached input is treated as a zero-cost lower bound, the floor is still about USD 21,733.53.

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
| Raw shadow bill | USD 437,166.04 | CALC | Supplied rates, no real billing export |
| Cached-input lower-bound bill | USD 21,733.53 | CALC | Assumes cached input free; prompt did not authorize that discount |
| Energy estimate | 2,171.17 MWh | CALC | Uses supplied 0.05 kWh/1K tokens, not OpenAI telemetry |
| Peak `.codex` prompt burst | 13/sec | JSONL | User-message events only |
| Last six hours prompt rate | 30.5/hour | JSONL | Latest observed `.codex` timestamp window |
| 14-day meaningful LOC velocity | 2,307.84 LOC/hour | CALC | Compression model, not git-proven creation time |
| Human-year compression, meaningful LOC | 176.24-352.47 years | CALC | 10-20 LOC/day, 220 workdays/year |
| Midpoint replacement cost, meaningful LOC | USD 58.75M | CALC | Assumes USD 250k fully loaded senior/year |
| Midpoint replacement cost, all Assets physical C# | USD 119.81M | CALC | Includes vendor/third-party physical code |

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
