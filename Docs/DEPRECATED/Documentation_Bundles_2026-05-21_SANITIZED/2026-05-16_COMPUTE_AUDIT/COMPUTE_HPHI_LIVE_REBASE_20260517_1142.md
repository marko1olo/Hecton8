# COMPUTE H-PHI LIVE REBASE 2026-05-17 11:42

Status: AUDIT COMPLETE
Scope: HECTON-8 only. Timaert excluded.
Evidence class: STATIC_SOURCE + JSONL window + SQLite live pulse.
Invoice status: NOT AN INVOICE.
Search keywords: H-Phi; HPhi; hphi; ash-fi; ash_phi; ASh-Fi; HФ; Аш-Фи; integration-metric; architecture-integration; token-H-Phi-ROI; compute-H-Phi.

Pricing basis: official OpenAI API pricing page checked during this pass. Current audit rates remain `gpt-5.5` USD 5.00/M input, USD 0.50/M cached input, USD 30.00/M output.

## Source Drift Gate

Reason to rerun H-Phi: source drift after the 04:12 H-Phi artifact was not small.

| Metric | Value |
|---|---:|
| C# files modified after 04:12 | 113 |
| Modified file bytes after 04:12 | 10,799,862 |
| Current first-party script files | 1,585 |
| Current physical LOC | 1,035,315 |
| Current blank lines | 137,980 |
| Current comment-only lines | 42,392 |
| Current meaningful LOC | 854,943 |
| Current script bytes | 46,232,512 |
| Current logic density | 82.5781% |

Net movement since the 04:46 LOC denominator: +4 files, +16,194 physical LOC, +15,874 meaningful LOC, +1,486,386 script bytes.

## H-Phi Score Delta

Current artifact: `COMPUTE_HPHI_CURRENT_20260517_1138.json`.

Scan time: 181,218 ms.
Artifact timestamp: 2026-05-17 11:41:52 +04:00.

| Metric | 04:12 | 11:42 | Delta |
|---|---:|---:|---:|
| Runtime H-Phi risk | 0.004858813 | 0.005378664 | +0.000519851 |
| Runtime H-Phi narrow | 0.070286230 | 0.075881112 | +0.005594882 |
| All-source H-Phi risk | 0.004020452 | 0.004467625 | +0.000447173 |
| All-source H-Phi narrow | 0.063124846 | 0.068247520 | +0.005122674 |
| Data sovereignty | 0.132223543 | 0.141543476 | +0.009319933 |
| Memory alignment | 0.531571219 | 0.536097561 | +0.004526342 |
| Binary-safe ratio | 0.021536955 | 0.021463415 | -0.000073540 |

## Counter Delta

| Counter | 04:12 | 11:42 | Delta |
|---|---:|---:|---:|
| Runtime files | 1,344 | 1,349 | +5 |
| Runtime lines | 921,714 | 938,693 | +16,979 |
| SignalBus pushes | 423 | 436 | +13 |
| GlobalRegistry surface | 5,315 | 5,335 | +20 |
| DataVault refs | 1,112 | 1,216 | +104 |
| NativeArray refs | 7,298 | 7,375 | +77 |
| Struct declarations | 2,043 | 2,050 | +7 |
| StructLayout attributes | 1,086 | 1,099 | +13 |
| GetComponent calls | 322 | 321 | -1 |
| Dispose calls | 1,071 | 1,060 | -11 |
| Managed format surface | 543 | 563 | +20 |
| JobComplete surface | 73 | 73 | 0 |
| Primary managed runtime risk | 157 | 177 | +20 |
| Primary job complete risk | 55 | 55 | 0 |
| Owner-blocked NativeArray refs | 5,123 | 4,961 | -162 |
| Owner-blocked Dispose calls | 746 | 722 | -24 |
| Native ownership risk | 6,615 | 6,405 | -210 |
| Primary owner-blocked NativeArray refs | 4,539 | 4,404 | -135 |
| Primary owner-blocked Dispose calls | 621 | 601 | -20 |
| Primary native ownership risk | 5,781 | 5,606 | -175 |

Verdict on counters: architecture ownership moved in the right direction. DataVault refs, StructLayout attrs, and owner-blocked native ownership improved. Managed runtime debt also worsened by +20 PrimaryManagedRuntimeRisk and +20 ManagedFormatSurface. This is not a clean win.

## Token Window 04:12 To 11:42

Window: 2026-05-17T04:11:59+04:00 to 2026-05-17T11:41:52+04:00.

| Metric | Value |
|---|---:|
| Duration | 26,993 sec |
| JSONL files scanned | 50 |
| JSONL bytes scanned | 631,964,960 |
| Rows read | 226,027 |
| Usable usage rows | 3,317 |
| Prompt rows | 106 |
| Parse errors | 0 |
| Model bucket | `gpt-5.5` |
| Input tokens | 499,837,590 |
| Cached input tokens | 478,154,112 |
| Output tokens | 1,657,653 |
| Reasoning output tokens | 476,569 |
| Total tokens | 501,495,243 |
| Cached-input ratio | 95.6619% |
| Cache-aware cost | USD 397.22 |
| No-cache equivalent | USD 2,548.92 |
| Average tokens/sec | 18,578.71 |
| Average tokens/min | 1,114,722.88 |
| Average tokens/hour | 66,883,372.53 |
| Long-context events over 272K input | 0 |

Peak cadence:

| Peak | Value |
|---|---:|
| Token peak second | 1,068,485 at 2026-05-17T05:10:56+04:00 |
| Token peak minute | 12,973,587 at 2026-05-17T05:07+04:00 |
| Token peak hour | 282,149,550 at 2026-05-17T05+04:00 |
| Prompt peak minute | 5 user-message rows at 2026-05-17T11:27+04:00 |
| Prompt peak hour | 55 user-message rows at 2026-05-17T04+04:00 |

Top token-window threads:

| Rank | Thread title | Tokens |
|---:|---|---:|
| 1 | CONTENT_AUTHORITY_DICTATOR prompt thread | 32,705,956 |
| 2 | Enforce DataVault statelessness | 29,879,387 |
| 3 | Improve bot memory and CRM | 29,439,825 |
| 4 | Move reports to batch006 | 25,232,293 |
| 5 | Build ballast PID | 17,818,206 |
| 6 | Fix jitter with double3 math | 17,404,466 |
| 7 | Add prefab registry facade | 15,952,838 |
| 8 | CONTRACT_AUTHORITY_SURGEON prompt thread | 15,842,036 |
| 9 | Add Burst funnel smoothing | 15,231,152 |
| 10 | Sync flora bioluminescence pulses | 14,524,018 |

## Marginal H-Phi ROI

| Metric | Value |
|---|---:|
| Tokens per +0.001 Runtime H-Phi risk | 964,690,350 |
| USD per +0.001 Runtime H-Phi risk | USD 764.11 |
| Tokens per +0.001 Runtime H-Phi narrow | 89,634,642 |
| USD per +0.001 Runtime H-Phi narrow | USD 71.00 |

This is materially better than the 02:17-04:12 plateau interval, where Runtime H-Phi risk cost 35.51B tokens per +0.001. The improvement came with real source movement and real ownership migration, not just report churn.

## SQLite Live Pulse 11:38

Source: `C:\Users\danat\.codex\state_5.sqlite`.

| Metric | Value |
|---|---:|
| Start | 2026-05-17T11:38:53+04:00 |
| End | 2026-05-17T11:39:24+04:00 |
| Duration | 30.099099 sec |
| Start total tokens | 51,063,570,988 |
| End total tokens | 51,066,572,323 |
| Delta tokens | 3,001,335 |
| Tokens/sec | 99,715.11 |
| Tokens/min | 5,982,906.66 |
| Tokens/hour | 358,974,399.86 |
| Tokens/day equivalent | 8,615,385,596.76 |
| Active delta threads | 12 |
| Cache-aware pulse cost | USD 2.38 |
| Cache-aware USD/min | USD 4.74 |
| Cache-aware USD/hour | USD 284.34 |
| No-cache USD/min scenario | USD 30.41 |

Top live burners:

| Rank | Thread title | Delta tokens |
|---:|---|---:|
| 1 | CONTRACT_AUTHORITY_SURGEON prompt thread | 561,138 |
| 2 | Build CSV balance pipeline | 436,301 |
| 3 | Sync flora bioluminescence pulses | 324,299 |
| 4 | CORE_TICK_DILATION prompt thread | 318,307 |
| 5 | Add acoustic echo navigation | 254,567 |
| 6 | Fix ASMDEF graph | 227,229 |
| 7 | Move reports to batch006 | 210,422 |
| 8 | MEMORY_DEFRAGMENTATION_OVERSEER prompt thread | 191,478 |
| 9 | Automate H8Memory lifecycle | 148,701 |
| 10 | AUDIO_IMPORT_RESIDENCY_GUARD prompt thread | 144,614 |

## Current Totals

| Metric | Value |
|---|---:|
| Current SQLite tokens | 51,066,572,323 |
| Estimated current cache-aware total | USD 34,756.09 |
| Current energy estimate | 2,553.33 MWh |
| Tokens per meaningful LOC | 59,730.97 |
| Tokens per script byte | 1,104.56 |

Verdict: H-Phi improved again after a real source surge, but the runtime debt ledger is mixed. Token efficiency is no longer the catastrophic 04:12 plateau, yet the live 11:38 pulse is hot at almost 100K tokens/sec. Continue treating high-burn threads as suspects, not convicted waste, until joined against diffs and compile/runtime evidence.

STATUS: AUDIT COMPLETE.
