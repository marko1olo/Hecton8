# KCC Environmental Integration - SHINOBU_250

Status: STATIC_ROUTE_DOC / RUNTIME_PROOF_PENDING
Evidence class: STATIC_DOC / STATIC_SOURCE
Owner domain: Physics KCC environmental integration
Review disposition: YELLOW / STATIC_DOC_ONLY until compile/import/runtime/profiler/player proof exists.

Owner: Physics KCC.

Route:

- `ApplyEnvironmentalForcesJob` runs in fixed simulation before `BuildCapsuleCastCommandsJob`.
- Pointer/ref mutation in `ApplyEnvironmentalForcesJob` fails closed before pointer arithmetic if mandatory `States` or `ProposedVelocities` lanes are absent or shorter than the scheduled row index.
- It consumes KCC-owned DataVault staging buffers for 3D current and SDF mud distance.
- The same pre-capsule job samples the SDF at capsule-foot AUP, derives a central-difference gradient normal, and injects over-limit wall-slide velocity before the capsule command is built.
- Metabolism penalties read shared `Hecton8.Core.Contracts.Physiology` `MetabolicStateDTO`.
- Source lane: SHINOBU_145 buffer `70238`.
- Preconditions: lane exists, length is sufficient, no active lock.
- Fallback: KCC-owned mock metabolism lane `71764`.
- Physiology remains the metabolism owner.
- Cross-domain edit is limited to Physiology consuming the same Core.Contracts DTO/constants.
- Files: `ShinobuMetabolismData.cs`, `ShinobuMetabolismJobs.cs`, `ShinobuMetabolismRuntime.cs`.
- Vault payload type identity must stay one route.
- Profile CSV ingestion writes rows to `71768`, linear-probe buckets to `71769`, FNV-1a keys to `71770`; bucket collisions are checked before active row selection.
- `EvaluateSlopeFrictionJob` runs after capsule hit extraction and before kinematic resolution.
- `KinematicResolutionJob` similarly guards mandatory state, previous-AUP, proposed-velocity, debug, and fault lanes before pointer/ref row mutation.
- `KccEnvironmentTelemetryAggregateJob` writes the last 300 frames to `ShinobuKccEnvironmentTelemetryRing`.
- KCC editor-only scanner/tuner code is isolated under `Hecton8.Physics.KCC.Editor.asmdef` with Roslyn precompiled references, so scanner source changes stop widening the `Hecton8.Core` compile wall after Unity regenerates Bee/project files.

Rules:

- No `OnTriggerStay`, `Rigidbody.AddForce`, `CharacterController.slopeLimit`, or downward raycast slope authority owns KCC environmental movement.
- AUP positions are localized by subtracting the environment grid origin before float sampling.
- `GlobalQualityWeight` is continuous: low weight biases nearest sampling and flatter SDF-gradient anticipation; high weight biases trilinear sampling and stronger pre-contact slope response.
- Core/KCC does not reference the `Hecton8.Physiology` runtime assembly; physiology DTO sharing is routed through `Hecton8.Core.Contracts.Physiology`.
- `70265` remains documented DroneFleet state space; KCC/Physiology metabolism uses `70238` to avoid a DataVault BufferID alias.
- `Environment_Trigger_Scanner` writes the full SHINOBU_250 report to `PHYSICS_OPTIMIZATION_REPORT_SHINOBU_250.json` and merges only the top-level `shinobu250KccEnvironmentScanner` block into the shared canonical report to avoid deleting neighboring agents' report sections.
- Planned/generated-on-fault dump target: `Docs/AgentLogs/Dump_SHINOBU_250.bin`; no existing dump artifact is implied without command, timestamp, environment, trigger, and output.
