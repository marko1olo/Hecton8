# Rationale_CAUSTICS_PROJECTION_ENGINEER

Status: PENDING VERIFICATION

## Session Init
Problem: Existing caustics directive reports texture/projector-style caustics that ignore wave data, depth, shadows, AUP shifts, and quality tiers.
Solution: Build an analytical GPU-owned caustics subsystem with GlobalRegistry service registration, VISUAL_SYNC execution, AUP-safe projection, low-tier shader fallback, depth gate, and telemetry state.
Rejected Alternatives: Unity Projector/DecalProjector and per-object material overrides burn fill-rate or break batching; CPU ray-style simulation violates the 0.1 ms suspicion threshold.
Scalability potential: Low disables compute and uses fragment Voronoi; Middle uses 512 map with cheap derivatives; High increases visual response through chromatic split and stronger shadow/depth masking; Ultra can raise dispatch cadence/detail if verified.
Hardware Impact: Expected low-end gain is from deleting projector fill-rate and disabling compute on MX350/i3; exact gain remains PENDING VERIFICATION until Unity profiler data exists.
