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
| `COMPUTE_HPHI_LIVE_REBASE_20260517_1337.md` | Rebase after second source-drift gate. |
| `COMPUTE_HPHI_LIVE_REBASE_20260517_1539.md` | Latest final rebase after third source-drift gate. |
| `COMPUTE_HPHI_CURRENT_20260516_171857.json` | Raw static-source H-Phi artifact. |
| `COMPUTE_HPHI_CURRENT_20260517_021429.json` | Raw static-source H-Phi artifact. |
| `COMPUTE_HPHI_CURRENT_20260517_040910.json` | Raw static-source H-Phi artifact. |
| `COMPUTE_HPHI_CURRENT_20260517_1138.json` | Raw static-source H-Phi artifact. |
| `COMPUTE_HPHI_CURRENT_20260517_1327.json` | Raw static-source H-Phi artifact. |
| `COMPUTE_HPHI_CURRENT_20260517_1539.json` | Raw static-source H-Phi artifact. |

## Score Timeline

| Timestamp | Runtime risk | Runtime narrow | Data sovereignty | Memory alignment | DataVault refs | Owner-blocked NativeArray refs | Primary managed risk |
|---|---:|---:|---:|---:|---:|---:|---:|
| 2026-05-15 22:46 | 0.000636091 | 0.010787439 | 0.021306032 | 0.506309148 | 154 | 6,262 | n/a |
| 2026-05-16 17:18 | 0.004164939 | 0.060806118 | 0.114950891 | 0.528974740 | 948 | 5,266 | 148 |
| 2026-05-17 02:17 | 0.004847023 | 0.070058393 | 0.131794933 | 0.531571219 | 1,108 | 5,143 | 155 |
| 2026-05-17 04:12 | 0.004858813 | 0.070286230 | 0.132223543 | 0.531571219 | 1,112 | 5,123 | 157 |
| 2026-05-17 11:42 | 0.005378664 | 0.075881112 | 0.141543476 | 0.536097561 | 1,216 | 4,961 | 177 |
| 2026-05-17 13:37 | 0.005525762 | 0.077385732 | 0.144331092 | 0.536168133 | 1,245 | 4,941 | 183 |
| 2026-05-17 15:39 | 0.005580503 | 0.077988159 | 0.145138727 | 0.537335286 | 1,245 | 4,902 | 183 |

## Token ROI By Interval

| Interval | Tokens | Cache-aware USD | Risk delta | Tokens / +0.001 risk | USD / +0.001 risk | Narrow delta | Tokens / +0.001 narrow |
|---|---:|---:|---:|---:|---:|---:|---:|
| 2026-05-15 22:46 -> 2026-05-16 17:18 | 2,464,254,349 | USD 1,947.70 | +0.003528848 | 698,316,943 | USD 551.94 | +0.050018679 | 49,266,682 |
| 2026-05-16 17:18 -> 2026-05-17 02:17 | 2,183,475,652 | USD 1,640.89 | +0.000682084 | 3,201,182,922 | USD 2,405.70 | +0.009252275 | 235,993,380 |
| 2026-05-17 02:17 -> 2026-05-17 04:12 | 418,677,551 | USD 326.77 | +0.000011790 | 35,511,242,663 | USD 27,715.86 | +0.000227837 | 1,837,618,784 |
| 2026-05-17 04:12 -> 2026-05-17 11:42 | 501,495,243 | USD 397.22 | +0.000519851 | 964,690,350 | USD 764.11 | +0.005594882 | 89,634,642 |
| 2026-05-17 11:42 -> 2026-05-17 13:37 | 304,562,532 | USD 236.42 | +0.000147098 | 2,070,473,643 | USD 1,607.26 | +0.001504620 | 202,418,240 |
| 2026-05-17 13:37 -> 2026-05-17 15:39 | 213,121,363 | USD 145.30 | +0.000054741 | 3,893,267,624 | USD 2,654.31 | +0.000602427 | 353,771,267 |
| Cumulative 2026-05-15 22:46 -> 2026-05-17 15:39 | 6,085,586,690 | USD 4,694.31 | +0.004944412 | 1,230,800,890 | USD 949.42 | +0.067200720 | 90,558,355 |

## Current Read

Latest H-Phi integration metric:

| Metric | Value |
|---|---:|
| Runtime H-Phi risk | 0.005580503 |
| Runtime H-Phi narrow | 0.077988159 |
| Data sovereignty | 0.145138727 |
| Memory alignment | 0.537335286 |
| Binary-safe ratio | 0.021961933 |
| Current meaningful LOC | 857,227 |
| Current SQLite tokens | 51,586,452,098 |
| Current energy estimate | 2,579.32 MWh |

Interpretation:

- The project is integrating: Runtime H-Phi risk rose from 0.000636091 to 0.005580503.
- The strongest architectural gain is DataVault adoption: 154 refs at baseline to 1,245 refs current.
- Native ownership debt improved: owner-blocked NativeArray refs dropped from 6,262 to 4,902.
- The score is not clean: PrimaryManagedRuntimeRisk reached 183, and managed format surface reached 569 in the latest artifact.
- The 02:17-04:12 interval was the worst ROI plateau. The 13:37-15:39 interval is a new plateau warning: cleaner counters, weak score-per-token movement.

STATUS: AUDIT COMPLETE.
