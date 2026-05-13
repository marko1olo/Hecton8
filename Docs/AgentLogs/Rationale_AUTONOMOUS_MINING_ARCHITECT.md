# Rationale_AUTONOMOUS_MINING_ARCHITECT

Status: PENDING VERIFICATION

## Decision 0 - Domain Gate

Problem: Deployable SDF Drills cross gameplay tools, voxel carving, logistics power, inventory, audio threat, and persistence. Direct ownership of those systems would violate domain boundaries and create dependencies on agents working in parallel.
Solution: Scope implementation to mining-owned runtime contracts and adapters. Use GlobalRegistry/service interfaces or typed signal packets for cross-domain traffic. Keep SDF mutation as a `VoxelCarveEvent`, acoustic threat as `AcousticPingSignal`, and power as a scalar query/consumer interface where existing contracts permit it.
Rejected Alternatives: Direct calls into concrete voxel, fauna, logistics, or audio managers; Unity trigger callbacks; AudioSource event strings; per-frame SDF edits.
Scalability potential: Low uses deterministic background resource accrual and skips visible carve rebuilds. Middle emits sparse 60 s carve events. High/Ultra can spend saved cycles on stronger crater/debris/acoustic presentation through owning systems.
Hardware Impact: i3/MX350 avoids per-frame mining simulation, managed allocations, trigger callbacks, and repeated mesh churn; expected savings are in milliseconds during multi-drill scenes compared with direct Unity physics/presentation loops.
