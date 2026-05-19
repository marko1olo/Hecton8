# Rationale_SHINOBU_138

Status: PENDING VERIFICATION

## Initial Scope

Problem: Predator scent tracking request targets replacement of object-trigger scent logic with a mathematical 3D chemical field.
Solution: Use DataVault-owned flat buffers, explicit 16-byte `ChemicalCellDTO`, AUP-local mapping, Burst jobs, front/back Jacobi buffers, and telemetry ring.
Rejected Alternatives: Unity trigger volumes, scent particle GameObjects, `Vector3.Distance` scans, local unmanaged persistent allocations outside Vault.
Scalability potential: Low uses lower grid resolution and 1 solver iteration; Middle increases resolution/iteration cadence; High uses stronger advection/occlusion fidelity; Ultra spends saved cycles on richer sensory debug and visual fog response without changing gameplay truth.
Hardware Impact: On i3/MX350, replacing PhysX broadphase scent checks and O(M*N) distance scans with O(1) sampling and O(N) flat-array solver targets stable cache-linear work; measured proof absent.

## Mandate Selection

Problem: Chemical field crosses AI, AUP, Vault, jobs, telemetry, and editor debug surfaces.
Solution: Read eight targeted mandates before coding: GlobalRegistry DI, Signal Lane Segregation, AUP Determinism, Floating Origin Precision, Zero GC, Native Memory Jobs, ARM64 Struct Layout, Crash Telemetry.
Rejected Alternatives: Reading unrelated rendering/UI mandates first or starting from invented architecture.
Scalability potential: Mandates force continuous quality weight, bounded snapshots, and hot-path allocation rejection across weak/mid/high/ultra devices.
Hardware Impact: Reduces risk of MX350 frame spikes from managed allocations, sync job completion, misaligned DTO reads, or trigger broadphase churn.
