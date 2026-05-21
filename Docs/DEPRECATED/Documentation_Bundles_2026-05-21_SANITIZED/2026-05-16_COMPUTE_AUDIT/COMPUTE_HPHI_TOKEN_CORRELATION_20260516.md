# COMPUTE H-PHI TOKEN CORRELATION 2026-05-16

Status: AUDIT COMPLETE
Scope: HECTON-8 only. Timaert excluded.
Evidence class: static H-Phi source scan + local `.codex` token telemetry.
Invoice status: NOT AN INVOICE.
Search keywords: H-Phi; HPhi; hphi; ash-fi; ash_phi; ASh-Fi; HФ; Аш-Фи; integration-metric; architecture-integration; token-H-Phi-ROI; compute-H-Phi.

## Boundary

H-Phi is a static architecture hygiene score. It is not a Unity runtime proof, not a profiler result, not a GC proof, and not a build result.

Authoritative metric contract: `Docs/ARCHITECTURE/HECTON_PHI_STATIC_METRIC.md`.
Authoritative tool: `Tools/Architecture/HectonPhiAudit.ps1`.

Current raw artifact:

- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_CURRENT_20260516_171857.json`
- `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_TIMESERIES_EXTRACT_20260516.json`

PowerShell JSON warning: many historical `HPhi*.json` files are UTF-16. A UTF-8-only parser falsely marks them as broken. The timeseries extractor used UTF-8/UTF-16 autodetection.

## Capture-Time H-Phi

Snapshot: 2026-05-16 17:18:57 +04:00.

| Metric | Value |
|---|---:|
| Runtime H-Phi risk | 0.004164939 |
| Runtime H-Phi narrow | 0.060806118 |
| All-source H-Phi risk | 0.003430876 |
| All-source H-Phi narrow | 0.054608110 |
| Risk integration | 0.068495401 |
| Architectural purity | 1.000000000 |
| Data sovereignty | 0.114950891 |
| Memory alignment | 0.528974740 |
| Binary-safe ratio | 0.021792967 |
| AUP precision integrity | 1.000000000 |

Counts:

| Counter | Value |
|---|---:|
| Runtime files | 1,325 |
| Runtime lines | 913,046 |
| SignalBus push surface | 417 |
| GlobalRegistry surface | 5,291 |
| Legacy event publish surface | 26 |
| Unity update debt methods | 0 |
| DataVault refs | 948 |
| NativeArray refs | 7,299 |
| Struct declarations | 2,019 |
| StructLayout attributes | 1,068 |
| GetComponent calls | 321 |
| Owner-blocked NativeArray refs | 5,266 |
| Primary owner-blocked NativeArray refs | 4,682 |
| Managed format surface | 535 |
| JobComplete surface | 73 |

## Baseline Delta

Baseline: `Docs/AgentLogs/HPhi_INTEGRATION_ASSEMBLY_SURGEON_20260515_224426_CurrentDiskBudgetGate22.json`, timestamp 2026-05-15 22:46:22 +04:00.

| Metric | Baseline | Current | Delta | Ratio |
|---|---:|---:|---:|---:|
| Runtime H-Phi risk | 0.000636091 | 0.004164939 | +0.003528848 | 6.548x |
| Runtime H-Phi narrow | 0.010787439 | 0.060806118 | +0.050018679 | 5.637x |
| Data sovereignty | 0.021306032 | 0.114950891 | +0.093644859 | 5.395x |
| Memory alignment | 0.506309148 | 0.528974740 | +0.022665592 | 1.045x |
| Runtime files | 1,278 | 1,325 | +47 | 1.037x |
| Runtime lines | 872,669 | 913,046 | +40,377 | 1.046x |
| SignalBus push surface | 341 | 417 | +76 | 1.223x |
| GlobalRegistry surface | 5,060 | 5,291 | +231 | 1.046x |
| DataVault refs | 154 | 948 | +794 | 6.156x |
| NativeArray refs | 7,074 | 7,299 | +225 | 1.032x |
| StructLayout attributes | 963 | 1,068 | +105 | 1.109x |
| Owner-blocked NativeArray refs | 6,262 | 5,266 | -996 | 0.841x |

Verdict: H-Phi improved mainly because DataVault visibility exploded from 154 to 948 refs and owner-blocked NativeArray refs fell by 996. This is a real static-score movement. It is not runtime proof.

## Budget Gate Reality

The score improved, but old absolute budget gates are not all green.

| Gate | Current | Old limit | Pass |
|---|---:|---:|---:|
| GlobalRegistry surface max | 5,291 | 5,060 | no |
| NativeArray refs max | 7,299 | 7,074 | no |
| ManagedFormat surface max | 535 | 534 | no |
| JobComplete surface max | 73 | 58 | no |
| PrimaryManagedRuntimeRisk max | 148 | 147 | no |
| DataSovereignty min | 0.114950891 | 0.021306 | yes |
| MemoryAlignment min | 0.528974740 | 0.506309 | yes |
| RuntimeHPhiRisk min | 0.004164939 | 0.000636 | yes |

This is the core contradiction: the integrated score is much better, while several raw debt counters regressed. Do not sell this as pure green.

## Token Spend Between H-Phi Artifacts

Token window: baseline H-Phi artifact to current H-Phi artifact.

| Metric | Value |
|---|---:|
| Total token delta | 2,464,254,349 |
| Input token delta | 2,455,768,697 |
| Cached input delta | 2,352,380,928 |
| Non-cached input delta | 103,387,769 |
| Output token delta | 8,485,652 |
| Cache ratio | 95.790% |
| Cache-aware cost estimate | USD 1,947.70 |
| No-cache equivalent | USD 12,533.41 |

Rates used for this slice: `gpt-5.5` input USD 5.00/M, cached input USD 0.50/M, output USD 30.00/M. This inherits the same pricing assumption used by the compute audit ledger.

## H-Phi Efficiency

| Ratio | Value |
|---|---:|
| Tokens per +0.001 Runtime H-Phi risk | 698,316,943 |
| Cache-aware USD per +0.001 Runtime H-Phi risk | USD 551.94 |
| No-cache USD per +0.001 Runtime H-Phi risk | USD 3,551.70 |
| Tokens per +0.01 Runtime H-Phi narrow | 492,666,819 |
| Cache-aware USD per +0.01 Runtime H-Phi narrow | USD 389.39 |
| Tokens per +0.01 Data sovereignty | 263,148,920 |
| Cache-aware USD per +0.01 Data sovereignty | USD 207.99 |
| Tokens per +0.01 Memory alignment | 1,087,222,583 |
| Cache-aware USD per +0.01 Memory alignment | USD 859.32 |

This is not cheap. The score moved, but it moved through billions of tokens.

## Timeseries Correlation

Extractor input:

| Metric | Value |
|---|---:|
| Valid H-Phi artifacts | 76 |
| Invalid/empty/non-score H-Phi artifacts | 20 |
| JSONL files scanned for token timeline | 112 |
| JSONL rows scanned | 493,054 |
| Token rows used | 71,134 |
| Token parse errors | 0 |
| Token window total | 6,245,045,718 |

Pearson correlation against cumulative tokens since the local window start:

| Pair | Pearson r |
|---|---:|
| Tokens vs Runtime H-Phi risk | 0.522 |
| Tokens vs Runtime H-Phi narrow | 0.493 |
| Tokens vs Data sovereignty | 0.492 |

Verdict: within local artifacts, higher token burn is moderately associated with higher H-Phi. This is not causal proof. The dataset has many repeated budget reruns and one major step-change at the current artifact, so the honest statement is: correlation exists in the observed audit trail, but causation is not proven.

## Current Backlog

Top owner-blocked DataVault candidates:

| File | Owner-blocked refs | Primary owner-blocked refs | NativeArray refs | DataVault refs | Native ownership risk |
|---|---:|---:|---:|---:|---:|
| `HectonVoxelEngine.cs` | 277 | 277 | 277 | 0 | 315 |
| `SaveBinaryStorage.cs` | 132 | 0 | 132 | 0 | 238 |
| `PlayerInventory.cs` | 198 | 198 | 198 | 0 | 204 |
| `World/HectonMapMagicVegetationBridge.cs` | 166 | 166 | 166 | 0 | 188 |
| `Power/LogisticsNetworkGraph.cs` | 145 | 145 | 145 | 0 | 161 |
| `SubmarineAtmosphereSystem.cs` | 132 | 132 | 132 | 0 | 142 |
| `World/DestructibleOrganicManager.cs` | 125 | 125 | 125 | 0 | 139 |
| `VoxelDeltaProcessor.cs` | 92 | 92 | 92 | 0 | 118 |
| `World/ProceduralWreckGenerator.cs` | 66 | 66 | 66 | 0 | 116 |
| `World/VegetationFlowFieldIntegrator.cs` | 107 | 107 | 107 | 0 | 107 |

Domain backlog:

| Domain | Owner-blocked NativeArray refs | Native ownership risk |
|---|---:|---:|
| `World` | 1,592 | 2,128 |
| `Gameplay` | 350 | 438 |
| `Construction` | 291 | 337 |
| `HectonVoxelEngine.cs` | 277 | 315 |
| `PlayerInventory.cs` | 198 | 204 |
| `Core` | 194 | 274 |
| `Power` | 188 | 210 |
| `SaveBinaryStorage.cs` | 132 | 238 |

## Latest Token Pulse

SQLite live sample: 2026-05-16T23:14:21+04:00 to 2026-05-16T23:14:51+04:00.

| Metric | Value |
|---|---:|
| Start SQLite tokens | 49,763,778,148 |
| End SQLite tokens | 49,767,593,348 |
| 30-second delta | 3,815,200 |
| Tokens/sec | 127,173.33 |
| Tokens/min | 7,630,400 |
| Cache-aware USD/min, blended | USD 5.84 |
| Cache-aware USD/hour, blended | USD 350.64 |
| Cache-aware USD/day, blended | USD 8,415.46 |
| Active threads | 25 |

Current SQLite-based total estimate:

| Metric | Value |
|---|---:|
| Current SQLite thread tokens | 49,767,593,348 |
| Estimated current cache-aware total | USD 33,777.90 |
| Energy estimate | 2,488.38 MWh |
| 30 kWh/day household equivalent | 82,946 home-days |

## Strict Budget Gate Attempt

Timestamp: 2026-05-17T00:00+04:00.

The full strict H-Phi command with the old baseline absolute budgets timed out after 244 seconds. No completed `COMPUTE_HPHI_BUDGET_GATE_*.json` artifact was produced.

Therefore:

| Claim | Status |
|---|---|
| Capture-time H-Phi score improved | proven by `COMPUTE_HPHI_CURRENT_20260516_171857.json` |
| Old absolute budget gates are all green | false by current counters |
| Fresh strict gate command produced an `EXIT=0`/`EXIT=1` artifact | not proven; command timed out |

The honest operating state is: H-Phi score improved, but strict old budget compliance remains not clean.

STATUS: AUDIT COMPLETE.

## 2026-05-17 02:17 Superseding H-Phi Rebase

This file's "Capture-Time H-Phi" section was current at 2026-05-16T17:18+04:00. A later static H-Phi scan now supersedes it for current-state reporting:

`Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_LIVE_REBASE_20260517_0217.md`

Latest current values:

| Metric | Value |
|---|---:|
| Runtime H-Phi risk | 0.004847023 |
| Runtime H-Phi narrow | 0.070058393 |
| Data sovereignty | 0.131794933 |
| Memory alignment | 0.531571219 |
| DataVault refs | 1,108 |
| Owner-blocked NativeArray refs | 5,143 |
| Managed format surface | 541 |
| Primary managed runtime risk | 155 |

Delta from the 17:18 artifact: +0.000682084 Runtime H-Phi risk, +0.009252275 Runtime H-Phi narrow, +160 DataVault refs, -123 owner-blocked NativeArray refs.

Token spend between the 17:18 and 02:17 artifacts: 2,183,475,652 tokens, USD 1,640.89 cache-aware, USD 11,075.08 no-cache. Marginal Runtime H-Phi risk efficiency: 3,201,182,922 tokens per +0.001.

STATUS: AUDIT COMPLETE.
