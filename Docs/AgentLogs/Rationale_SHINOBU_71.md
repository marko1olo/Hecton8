# SHINOBU_71 Rationale

Status: PENDING VERIFICATION

## Decision 001 - Attach DRS To Existing Adapter

Problem: The repository already contains `ThermalDynamicResolutionAdapter` and `DynamicResolutionScaler`; adding a third scaler would create competing render-scale writers.
Solution: Keep ownership in the existing graphics scalability adapter and patch only the missing SHINOBU_71 acceptance gaps.
Rejected Alternatives: A new MonoBehaviour would race the existing adapter and duplicate `DynamicResolutionHandler` state. Direct `Screen.SetResolution` is forbidden because it reallocates display buffers.
Scalability potential: Low keeps scale near survival floor with sharpened reconstruction; Middle/High recover smoothly; Ultra spends headroom on visual overkill shader globals.
Hardware Impact: i3/MX350 avoids display-buffer reallocations and keeps DRS work scalar-only; estimated hot-path cost remains below 100 microseconds, pending profiler proof.

