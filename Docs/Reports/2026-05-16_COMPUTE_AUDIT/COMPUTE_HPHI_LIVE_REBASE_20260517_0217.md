# COMPUTE H-PHI LIVE REBASE 2026-05-17 02:17

Status: AUDIT COMPLETE
Scope: HECTON-8 only. Timaert excluded.
Evidence class: static H-Phi source scan + bounded JSONL token window + SQLite live pulse.
Invoice status: NOT AN INVOICE.
Search keywords: H-Phi; HPhi; hphi; ash-fi; ash_phi; ASh-Fi; HФ; Аш-Фи; integration-metric; architecture-integration; token-H-Phi-ROI; compute-H-Phi.

## Artifacts

| Artifact | Value |
|---|---|
| Previous H-Phi artifact | `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_CURRENT_20260516_171857.json` |
| Current H-Phi artifact | `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_CURRENT_20260517_021429.json` |
| Previous H-Phi timestamp | 2026-05-16 17:18:57 +04:00 |
| Current H-Phi timestamp | 2026-05-17 02:17:05 +04:00 |
| Current scan wall time | 157,042 ms |
| Current artifact size | 153,080 bytes |

No strict budget gate was run in this pass. The prior strict old-budget command timed out; this pass measures score/counter drift only.

## H-Phi Score Delta

| Metric | Previous | Current | Delta | Ratio |
|---|---:|---:|---:|---:|
| Runtime H-Phi risk | 0.004164939 | 0.004847023 | +0.000682084 | 1.164x |
| Runtime H-Phi narrow | 0.060806118 | 0.070058393 | +0.009252275 | 1.152x |
| All-source H-Phi risk | 0.003430876 | 0.004003970 | +0.000573094 | 1.167x |
| All-source H-Phi narrow | 0.054608110 | 0.062947667 | +0.008339557 | 1.153x |
| Risk integration | 0.068495401 | 0.069185476 | +0.000690075 | 1.010x |
| Architectural purity | 1.000000000 | 1.000000000 | 0.000000000 | 1.000x |
| Data sovereignty | 0.114950891 | 0.131794933 | +0.016844042 | 1.147x |
| Memory alignment | 0.528974740 | 0.531571219 | +0.002596479 | 1.005x |
| Binary-safe ratio | 0.021792967 | 0.021536955 | -0.000256012 | 0.988x |
| AUP precision integrity | 1.000000000 | 1.000000000 | 0.000000000 | 1.000x |

## Counter Delta

| Counter | Previous | Current | Delta |
|---|---:|---:|---:|
| Runtime files | 1,325 | 1,344 | +19 |
| Runtime lines | 913,046 | 921,103 | +8,057 |
| SignalBus push surface | 417 | 423 | +6 |
| GlobalRegistry surface | 5,291 | 5,311 | +20 |
| Legacy event publish surface | 26 | 26 | 0 |
| Unity update raw methods | 2 | 0 | -2 |
| Unity loop shell methods | 2 | 0 | -2 |
| Unity update debt methods | 0 | 0 | 0 |
| DataVault refs | 948 | 1,108 | +160 |
| NativeArray refs | 7,299 | 7,299 | 0 |
| Struct declarations | 2,019 | 2,043 | +24 |
| StructLayout attributes | 1,068 | 1,086 | +18 |
| FindObject calls | 0 | 0 | 0 |
| GetComponent calls | 321 | 321 | 0 |
| Dispose calls | 1,085 | 1,071 | -14 |
| Owner-blocked NativeArray refs | 5,266 | 5,143 | -123 |
| Primary owner-blocked NativeArray refs | 4,682 | 4,559 | -123 |
| Managed format surface | 535 | 541 | +6 |
| JobComplete surface | 73 | 73 | 0 |
| Primary managed runtime risk | 148 | 155 | +7 |

## Token Window Between H-Phi Artifacts

Window: 2026-05-16T17:18:57+04:00 to 2026-05-17T02:17:05+04:00. Source: `.codex/sessions` JSONL `last_token_usage` rows in the timestamp window.

| Metric | Value |
|---|---:|
| Duration | 32,288 sec |
| Prefiltered JSONL files | 50 |
| Prefiltered bytes | 543,939,025 |
| Rows read | 195,415 |
| Token rows in window | 15,364 |
| Usable usage rows | 15,363 |
| Prompt rows in window | 185 |
| Parse errors | 0 |
| Model bucket | `gpt-5.5` only |
| Input tokens | 2,171,308,607 |
| Cached input tokens | 2,096,486,400 |
| Output tokens | 7,284,530 |
| Reasoning output tokens | 2,168,036 |
| Total tokens | 2,183,475,652 |
| Cached input ratio | 96.554% |
| Cache-aware cost | USD 1,640.89 |
| No-cache equivalent | USD 11,075.08 |
| Long-context surcharge events over 272K input | 0 |
| Average tokens/sec | 67,624.99 |
| Average tokens/min | 4,057,499.35 |
| Average tokens/hour | 243,449,961.20 |

Peak cadence inside the H-Phi window:

| Peak | Value |
|---|---:|
| Token peak second | 2,144,552 at 2026-05-17T02:08:16+04:00 |
| Token peak minute | 19,685,298 at 2026-05-16T17:20+04:00 |
| Token peak hour | 419,756,106 at 2026-05-16T18:00+04:00 |
| Prompt peak minute | 3 rows at 2026-05-17T02:12+04:00 |

## Marginal H-Phi Efficiency

| Ratio | Value |
|---|---:|
| Tokens per +0.001 Runtime H-Phi risk | 3,201,182,922 |
| Cache-aware USD per +0.001 Runtime H-Phi risk | USD 2,405.70 |
| Tokens per +0.01 Runtime H-Phi narrow | 2,359,933,802 |
| Cache-aware USD per +0.01 Runtime H-Phi narrow | USD 1,773.50 |
| Tokens per +0.01 Data sovereignty | 1,296,289,603 |
| Cache-aware USD per +0.01 Data sovereignty | USD 974.17 |
| Tokens per +0.01 Memory alignment | 8,409,371,507 |
| Cache-aware USD per +0.01 Memory alignment | USD 6,319.67 |

Interpretation: marginal H-Phi gains are now much more expensive than the previous baseline-to-17:18 jump. The easy DataVault migration lift is being consumed. Further improvement is running into harder owner-blocked NativeArray and managed runtime debt.

## Cumulative H-Phi ROI Since 2026-05-15 Baseline

Baseline artifact: `Docs/AgentLogs/HPhi_INTEGRATION_ASSEMBLY_SURGEON_20260515_224426_CurrentDiskBudgetGate22.json`.

This combines the prior baseline-to-17:18 token slice with the new 17:18-to-02:17 slice.

| Metric | Value |
|---|---:|
| Cumulative token spend | 4,647,730,001 |
| Cumulative cache-aware cost | USD 3,588.59 |
| Runtime H-Phi risk delta | +0.004210932 |
| Runtime H-Phi narrow delta | +0.059270954 |
| Data sovereignty delta | +0.110488901 |
| Memory alignment delta | +0.025262071 |
| Tokens per +0.001 Runtime H-Phi risk | 1,103,729,531 |
| USD per +0.001 Runtime H-Phi risk | USD 852.21 |
| Tokens per +0.01 Runtime H-Phi narrow | 784,149,687 |
| USD per +0.01 Runtime H-Phi narrow | USD 605.46 |
| Tokens per +0.01 Data sovereignty | 420,651,302 |
| USD per +0.01 Data sovereignty | USD 324.79 |
| Tokens per +0.01 Memory alignment | 1,839,805,613 |
| USD per +0.01 Memory alignment | USD 1,420.54 |

Cumulative ROI still looks better than the latest interval because the first large DataVault migration jump was cheaper. The marginal curve is worsening.

## Budget Reality

Old absolute budget status inferred from current counters:

| Gate | Current | Old limit | Pass |
|---|---:|---:|---:|
| GlobalRegistry surface max | 5,311 | 5,060 | no |
| NativeArray refs max | 7,299 | 7,074 | no |
| ManagedFormat surface max | 541 | 534 | no |
| JobComplete surface max | 73 | 58 | no |
| PrimaryManagedRuntimeRisk max | 155 | 147 | no |
| DataSovereignty min | 0.131794933 | 0.021306 | yes |
| MemoryAlignment min | 0.531571219 | 0.506309 | yes |
| RuntimeHPhiRisk min | 0.004847023 | 0.000636 | yes |

The score improved. The old absolute budgets are still not clean. Reporting this as green would be false.

## Current Code And Token Rebase

SQLite live sample at 2026-05-17T02:14:29 to 02:14:59+04:00:

| Metric | Value |
|---|---:|
| Start SQLite tokens | 50,311,752,031 |
| End SQLite tokens | 50,313,194,499 |
| 30-second delta | 1,442,468 |
| Tokens/sec | 48,082.27 |
| Tokens/min | 2,884,936 |
| Tokens/hour | 173,096,160 |
| Tokens/day equivalent | 4,154,307,840 |
| Active threads updated in last hour | 49 |

Static source scan:

| Metric | Value |
|---|---:|
| Script files | 1,580 |
| Physical LOC | 1,017,445 |
| Blank lines | 137,383 |
| Comment lines | 42,434 |
| Meaningful LOC | 837,628 |
| Script bytes | 44,642,915 |
| Logic density | 82.3266% |
| SQLite tokens / meaningful LOC | 60,066.28 |
| SQLite tokens / physical LOC | 49,450.53 |
| SQLite tokens / script byte | 1,127.01 |
| Burn / source-text proxy ratio | 4,508.06x |

Estimated cache-aware total from prior 00:52 estimate plus SQLite delta: USD 34,195.77.
Energy at `0.05 kWh / 1K tokens`: 2,515.66 MWh.

## 2026-05-17 03:04 Burn Spike

Source: 20-second SQLite per-thread delta over Hades cwd rows. This is burn attribution, not proof of project value and not a compute-thief conviction.

| Metric | Value |
|---|---:|
| Sample window | 2026-05-17T03:04:00 to 03:04:20+04:00 |
| Active delta threads | 20 |
| Total delta | 3,079,626 tokens |
| Tokens/sec | 153,981.30 |
| Tokens/min | 9,238,878 |
| Tokens/day equivalent | 13,303,984,320 |
| Cache-aware rate, blended | USD 7.08/min; USD 424.56/hour; USD 10,189.43/day |

| Rank | Thread title | Delta tokens |
|---:|---|---:|
| 1 | Build loot magnet system | 255,901 |
| 2 | Build memory visualizer | 232,292 |
| 3 | Improve bot memory and CRM | 228,341 |
| 4 | Implement Beer-Lambert shader | 221,344 |
| 5 | Build marine snow advection | 206,849 |
| 6 | ARCHITECT_SPATIAL_PROBE prompt thread | 191,593 |
| 7 | Automate H8Memory lifecycle | 179,491 |
| 8 | CORE_TICK_DILATION prompt thread | 176,381 |
| 9 | Manage biota spawning pool | 168,554 |
| 10 | Build CSV balance pipeline | 155,171 |

SQLite total at 2026-05-17T03:15:49+04:00: 50,453,850,790 tokens.

| Metric | Value |
|---|---:|
| Delta since 02:15 SQLite total | +140,656,291 |
| Estimated cache-aware cost delta | USD 107.73 |
| Estimated current cache-aware total | USD 34,303.50 |
| Current energy estimate | 2,522.69 MWh |
| Tokens per meaningful LOC | 60,234.20 |
| Tokens per script byte | 1,130.16 |

## Current Backlog

Top owner-blocked DataVault candidates remain:

| File | Owner-blocked refs | Primary owner-blocked refs | Native ownership risk |
|---|---:|---:|---:|
| `HectonVoxelEngine.cs` | 277 | 277 | 315 |
| `SaveBinaryStorage.cs` | 132 | 0 | 238 |
| `PlayerInventory.cs` | 198 | 198 | 204 |
| `World/HectonMapMagicVegetationBridge.cs` | 166 | 166 | 188 |
| `Power/LogisticsNetworkGraph.cs` | 145 | 145 | 161 |
| `SubmarineAtmosphereSystem.cs` | 132 | 132 | 142 |
| `World/DestructibleOrganicManager.cs` | 125 | 125 | 139 |
| `VoxelDeltaProcessor.cs` | 92 | 92 | 118 |
| `World/ProceduralWreckGenerator.cs` | 66 | 66 | 116 |
| `World/VegetationFlowFieldIntegrator.cs` | 107 | 107 | 107 |

Top managed runtime risk is now mostly instrumentation/persistence:

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
| `FieldToolRuntimeSmokeTester.cs` | 14 | 14 |
| `RuntimeDiagnosticsTrace.cs` | 13 | 13 |

STATUS: AUDIT COMPLETE.
