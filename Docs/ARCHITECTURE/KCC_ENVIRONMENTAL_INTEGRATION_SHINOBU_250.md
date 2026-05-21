# KCC Environmental Integration - SHINOBU_250

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Owner: Physics KCC.

Route:
- `ApplyEnvironmentalForcesJob` runs in fixed simulation before `BuildCapsuleCastCommandsJob`.
- Pointer/ref mutation in `ApplyEnvironmentalForcesJob` fails closed before pointer arithmetic if mandatory `States` or `ProposedVelocities` lanes are absent or shorter than the scheduled row index.
- It consumes KCC-owned DataVault staging buffers for 3D current and SDF mud distance.
- The same pre-capsule job samples the SDF at capsule-foot AUP, derives a central-difference gradient normal, and injects over-limit wall-slide velocity before the capsule command is built.
- Metabolism penalties read the shared `Hecton8.Core.Contracts.Physiology` `MetabolicStateDTO` ABI from SHINOBU_145 buffer `70238` only when that published lane exists, is long enough, and is not actively locked; otherwise the KCC-owned mock metabolism lane `71764` supplies deterministic fallback rows.
- Physiology remains the metabolism owner. The required cross-domain edit is limited to Physiology consuming the same Core.Contracts DTO and constants (`ShinobuMetabolismData.cs`, `ShinobuMetabolismJobs.cs`, `ShinobuMetabolismRuntime.cs`) so the Vault payload type identity is one route rather than duplicate shape-compatible structs.
- Profile CSV ingestion writes profile rows to `71768`, linear-probe buckets to `71769`, and FNV-1a hash keys to `71770`, so bucket collisions are verified before a profile can become the active row.
- `EvaluateSlopeFrictionJob` runs after capsule hit extraction and before kinematic resolution.
- `KinematicResolutionJob` similarly guards mandatory state, previous-AUP, proposed-velocity, debug, and fault lanes before pointer/ref row mutation.
- `KccEnvironmentTelemetryAggregateJob` writes the last 300 frames to `ShinobuKccEnvironmentTelemetryRing`.
- KCC editor-only scanner/tuner code is isolated under `Hecton8.Physics.KCC.Editor.asmdef` with explicit Roslyn precompiled references, so scanner source changes stop widening the `Hecton8.Core` compile wall after Unity regenerates Bee/project files.

Rules:
- No `OnTriggerStay`, `Rigidbody.AddForce`, `CharacterController.slopeLimit`, or downward raycast slope authority owns KCC environmental movement.
- AUP positions are localized by subtracting the environment grid origin before float sampling.
- `GlobalQualityWeight` is continuous: low weight biases nearest sampling and flatter SDF-gradient anticipation; high weight biases trilinear sampling and stronger pre-contact slope response.
- Core/KCC does not reference the `Hecton8.Physiology` runtime assembly; physiology DTO sharing is routed through `Hecton8.Core.Contracts.Physiology`.
- `70265` remains documented DroneFleet state space; KCC/Physiology metabolism uses `70238` to avoid a DataVault BufferID alias.
- `Environment_Trigger_Scanner` writes the full SHINOBU_250 report to `PHYSICS_OPTIMIZATION_REPORT_SHINOBU_250.json` and merges only the top-level `shinobu250KccEnvironmentScanner` block into the shared canonical report to avoid deleting neighboring agents' report sections.
- Planned/generated-on-fault dump target: `Docs/AgentLogs/Dump_SHINOBU_250.bin`; no existing dump artifact is implied without command, timestamp, environment, trigger, and output.
