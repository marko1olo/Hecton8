# 14MU Log

Date: 2026-05-28
Status: PENDING VERIFICATION

What was wrong -> No `14MU` XML batch prompt exists in `Docs/Tasks/CURRENT_BATCH.md`; status/rationale/log files were absent.
What was done -> Created `Status_14MU.md`, `Rationale_14MU.md`, and `LOG_14MU.md`; locked active scope to user-provided platform adaptation domain.
Cinematic Cheats used -> None; setup only.
Exact Microseconds saved -> 0 runtime us; prevents cross-domain mis-edits.

---

Date: 2026-05-28
Status: PENDING VERIFICATION

What was wrong -> Content visual budgets used hard VRAM forks (`<=2048`, `>4096`) instead of continuous runtime pressure. Predictive world streaming ignored VRAM abort entirely on GPUs above 2048 MB.
What was done -> `ContentTieredGroupPolicy` now derives budget/download/feature intensity from continuous `GlobalQualityWeight` + smoothed hardware capacity + XR/tier ceiling. `WorldChunkResidencyManager` now uses quality-scaled predictive VRAM abort/resume thresholds with shared-memory handling and hysteresis. Added edit-mode source guards.
Cinematic Cheats used -> Low budget keeps 1D LUT, triangle-noise, dot-product visual fakes; expensive silt/raymarch/POM/hull dent layers are progressively admitted as visual budget rises.
Exact Microseconds saved -> Pending profiler. Static expectation: no new allocations; predictive-stream abort prevents future pressure hitches, but no player/profiler artifact exists.
Verification -> `python Tools/PlatformPortabilityProofAudit.py` returned `PASS_WITH_WARNINGS`. Source-pattern checks found removed binary forks absent from changed files. `dotnet build` not launched because CPU load measured 100%.
