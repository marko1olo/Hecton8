# Rationale: ProceduralOreSpawner DataVault Lock Flattening

Problem: Spawn job path acquired write locks for ResourceNodes, OrePositions, OreTypes, OreMatrices, SpawnCounts, MockTerrainSdf, CandidateSlots, and IndirectArgs before scheduling worker jobs. This held multiple GlobalDataVault writer locks across an async job lifetime.
Solution: Stage spawn-job outputs in owner-local cold native buffers, then copy each finished output back to DataVault under one write lock at a time.
Rejected Alternatives: Keeping locks until job completion violates lock flattening. Converting the whole spawner to a new DataVault route would exceed the prompt scope. Scheduling separate jobs per output would multiply scheduler overhead and complicate determinism.
Scalability potential: Low uses same deterministic output with no lock convoy; Middle/High/Ultra spend saved contention budget in existing visual-cluster and VISUAL_SYNC render lanes, not gameplay truth divergence.
Hardware Impact: Expected i3/MX350 gain is reduced main-thread lock contention and relocation deadlock risk; CPU microsecond gain is pending source-only validation, no runtime profiler proof.
