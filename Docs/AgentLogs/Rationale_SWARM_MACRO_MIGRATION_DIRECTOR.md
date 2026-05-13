# Rationale - SWARM_MACRO_MIGRATION_DIRECTOR

Status: PENDING VERIFICATION

## Initial Boundaries

Problem: Existing biomass equations do not move abstract fish biomass between unloaded sectors.
Solution: Add a macro migration layer behind existing ecology contracts, not direct concrete cross-system references.
Rejected Alternatives: Directly moving boid GameObjects or calling world streamer concrete classes would violate parallel-agent isolation and create load-order dependencies.
Scalability potential: Low caps active macro swarms and uses coarse FrostTick diffusion; Middle raises capacity; High adds richer radar/readout data; Ultra spends saved cycles on denser migration telemetry and more visible fuzzy radar blobs.
Hardware Impact: MX350/i3 target gets O(n) native-array passes on FrostTick, capped at 32 swarms on Low; expected hot-frame managed GC impact is 0 B, CPU impact pending measurement.

Problem: AUP sector data must survive origin shifts.
Solution: Store macro swarm authority in absolute sector coordinates and shift only visual/hydration presentation.
Rejected Alternatives: Storing migration in `Transform.position` or shifted world floats would corrupt unloaded-sector authority after origin rebase.
Scalability potential: Low uses integer-sector travel and sparse samples; High/Ultra can increase path interpolation fidelity without changing authority.
Hardware Impact: Avoids per-frame Transform work and scene object churn; expected low-end gain is removal of GameObject migration simulation cost, exact microseconds pending profiler.
