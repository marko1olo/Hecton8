# Rationale_SHINOBU_144

Date: 2026-05-19
Status: PENDING VERIFICATION

## Decision 00 - Scope Boundary

Problem: Dense topographical sonar via PhysX raycasts and GameObject point markers would violate the 0.1 ms suspicion threshold, heap purity, and batching rules.
Solution: Limit authority to Echelon 8 presentation sonar. Use Voxel SDF samples and GPU/Native buffers; avoid gameplay truth mutation and exclude point clouds from rollback hashes.
Rejected Alternatives: Physics.Raycast fan and instantiated spheres were rejected because they route visual scanning through PhysX and the hierarchy.
Scalability potential: Low uses sparse rays and cheap SDF stepping; Middle increases ray density; High adds richer color sampling and smoother fade; Ultra spends saved CPU on denser glowing point clouds.
Hardware Impact: Expected low-end i3/MX350 gain is removal of thousands of PhysX calls and GameObject transforms; exact microseconds remain PENDING VERIFICATION until profiler/GCMonitor logs exist.

