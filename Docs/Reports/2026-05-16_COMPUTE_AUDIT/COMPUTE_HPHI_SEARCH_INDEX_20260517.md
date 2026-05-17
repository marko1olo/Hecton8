# COMPUTE H-PHI SEARCH INDEX 2026-05-17

Status: AUDIT COMPLETE
Scope: HECTON-8 only. Timaert excluded.
Evidence class: STATIC_SOURCE artifacts + JSONL/SQLite accounting reports.

Search keywords: H-Phi; HPhi; hphi; ash-fi; ash_phi; ASh-Fi; HФ; Аш-Фи; integration-metric; architecture-integration; token-H-Phi-ROI; compute-H-Phi.

Purpose: one index for finding the H-Phi / ash-fi integration metric and its token cost. H-Phi is the local static architecture integration indicator, not runtime proof.

## Canonical Artifacts

| Artifact | Meaning |
|---|---|
| `COMPUTE_HPHI_TOKEN_CORRELATION_20260516.md` | Historical artifact correlation and first live H-Phi scan in this compute audit. |
| `COMPUTE_HPHI_LIVE_REBASE_20260517_0217.md` | Rebase from 17:18 to 02:17. |
| `COMPUTE_HPHI_LIVE_REBASE_20260517_0412.md` | Plateau interval from 02:17 to 04:12. |
| `COMPUTE_HPHI_LIVE_REBASE_20260517_1142.md` | Rebase after large source drift. |
| `COMPUTE_HPHI_LIVE_REBASE_20260517_1337.md` | Latest rebase after second source-drift gate. |
| `COMPUTE_HPHI_CURRENT_20260516_171857.json` | Raw static-source H-Phi artifact. |
| `COMPUTE_HPHI_CURRENT_20260517_021429.json` | Raw static-source H-Phi artifact. |
| `COMPUTE_HPHI_CURRENT_20260517_040910.json` | Raw static-source H-Phi artifact. |
| `COMPUTE_HPHI_CURRENT_20260517_1138.json` | Raw static-source H-Phi artifact. |
| `COMPUTE_HPHI_CURRENT_20260517_1327.json` | Raw static-source H-Phi artifact. |

## Score Timeline

| Timestamp | Runtime risk | Runtime narrow | Data sovereignty | Memory alignment | DataVault refs | Owner-blocked NativeArray refs | Primary managed risk |
|---|---:|---:|---:|---:|---:|---:|---:|
| 2026-05-15 22:46 | 0.000636091 | 0.010787439 | 0.021306032 | 0.506309148 | 154 | 6,262 | n/a |
| 2026-05-16 17:18 | 0.004164939 | 0.060806118 | 0.114950891 | 0.528974740 | 948 | 5,266 | 148 |
| 2026-05-17 02:17 | 0.004847023 | 0.070058393 | 0.131794933 | 0.531571219 | 1,108 | 5,143 | 155 |
| 2026-05-17 04:12 | 0.004858813 | 0.070286230 | 0.132223543 | 0.531571219 | 1,112 | 5,123 | 157 |
| 2026-05-17 11:42 | 0.005378664 | 0.075881112 | 0.141543476 | 0.536097561 | 1,216 | 4,961 | 177 |
| 2026-05-17 13:37 | 0.005525762 | 0.077385732 | 0.144331092 | 0.536168133 | 1,245 | 4,941 | 183 |

## Token ROI By Interval

| Interval | Tokens | Cache-aware USD | Risk delta | Tokens / +0.001 risk | USD / +0.001 risk | Narrow delta | Tokens / +0.001 narrow |
|---|---:|---:|---:|---:|---:|---:|---:|
| 2026-05-15 22:46 -> 2026-05-16 17:18 | 2,464,254,349 | USD 1,947.70 | +0.003528848 | 698,316,943 | USD 551.94 | +0.050018679 | 49,266,682 |
| 2026-05-16 17:18 -> 2026-05-17 02:17 | 2,183,475,652 | USD 1,640.89 | +0.000682084 | 3,201,182,922 | USD 2,405.70 | +0.009252275 | 235,993,380 |
| 2026-05-17 02:17 -> 2026-05-17 04:12 | 418,677,551 | USD 326.77 | +0.000011790 | 35,511,242,663 | USD 27,715.86 | +0.000227837 | 1,837,618,784 |
| 2026-05-17 04:12 -> 2026-05-17 11:42 | 501,495,243 | USD 397.22 | +0.000519851 | 964,690,350 | USD 764.11 | +0.005594882 | 89,634,642 |
| 2026-05-17 11:42 -> 2026-05-17 13:37 | 304,562,532 | USD 236.42 | +0.000147098 | 2,070,473,643 | USD 1,607.26 | +0.001504620 | 202,418,240 |
| Cumulative 2026-05-15 22:46 -> 2026-05-17 13:37 | 5,872,465,327 | USD 4,549.01 | +0.004889671 | 1,200,993,958 | USD 930.33 | +0.066598293 | 88,177,415 |

## Current Read

Latest H-Phi integration metric:

| Metric | Value |
|---|---:|
| Runtime H-Phi risk | 0.005525762 |
| Runtime H-Phi narrow | 0.077385732 |
| Data sovereignty | 0.144331092 |
| Memory alignment | 0.536168133 |
| Binary-safe ratio | 0.021994135 |
| Current meaningful LOC | 856,940 |
| Current SQLite tokens | 51,372,184,781 |
| Current energy estimate | 2,568.61 MWh |

Interpretation:

- The project is integrating: Runtime H-Phi risk rose from 0.000636091 to 0.005525762.
- The strongest architectural gain is DataVault adoption: 154 refs at baseline to 1,245 refs current.
- Native ownership debt improved: owner-blocked NativeArray refs dropped from 6,262 to 4,941.
- The score is not clean: PrimaryManagedRuntimeRisk reached 183, and managed format surface reached 569 in the latest artifact.
- The 02:17-04:12 interval was the worst ROI plateau. The 11:42-13:37 interval improved the score again but was less token-efficient than the 04:12-11:42 recovery.

STATUS: AUDIT COMPLETE.
