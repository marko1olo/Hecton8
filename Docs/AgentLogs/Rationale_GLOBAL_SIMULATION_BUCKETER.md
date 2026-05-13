# Rationale_GLOBAL_SIMULATION_BUCKETER

Status: PENDING VERIFICATION

## Decision 0: Establish Source Of Truth
Problem: SlowTick clumping must be fixed without inventing direct dependencies across parallel agent work.
Solution: Use the extracted XML prompt, AGENTS.md, domain map, and task-relevant mandates as the only authority before code changes.
Rejected Alternatives: Directly editing suspected SlowTick code without registry/dispatcher audit was rejected because it would risk API invention and hidden dependencies.
Scalability potential: Low uses wider buckets and flat CPU load; Middle keeps stable cadence; High/Ultra can spend saved CPU on denser visual systems while preserving deterministic authority.
Hardware Impact: Expected gain on i3/MX350 is spike flattening, not total work elimination. Exact microseconds saved are PENDING PROFILER.

## Decision 1: Use Core Interface Boundary
Problem: A bucketer touches AI, voxel, thermodynamics, and dispatcher code. Direct concrete references would couple parallel agents.
Solution: Prefer an `ISimulationBucketer` service registered through `GlobalRegistry` and consumed through stable contract calls outside hot dependency lookup paths.
Rejected Alternatives: `BucketManager.Instance` and direct class references were rejected because AGENTS.md forbids singleton access and cross-domain concrete coupling.
Scalability potential: Low/MX350 can stretch cadence to reduce spikes; Ultra can process richer non-authoritative visual workload after authority buckets stay flat.
Hardware Impact: Avoids per-frame global searches and removes synchronous clump spikes. Exact microseconds saved are PENDING PROFILER.
