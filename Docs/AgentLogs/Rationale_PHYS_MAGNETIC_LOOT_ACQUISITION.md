# PHYS_MAGNETIC_LOOT_ACQUISITION Rationale

Status: PENDING VERIFICATION

## Decision 0 - Execution Boundary

Problem: Existing loot magnet implementation is unknown. Prompt demands Burst spatial pull, but AGENTS.md forbids guessing APIs and direct dependencies across parallel agents.

Solution: Scan current loot, inventory, AUP, vault, signal, audio, and VFX contracts before code. Implement only against existing contracts or create narrow local contracts under gameplay loot isolation where no contract exists.

Rejected Alternatives: Directly inventing `GlobalDataVault`, `ItemAcquiredSignal`, or `WAKE_TURBULENCE_COMPUTE` shapes would compile-risk the batch and create false dependencies.

Scalability potential: Low tier will prefer 10Hz snap or coarse pull; Middle uses scheduled spatial pull; High and Ultra can use stronger VFX/audio event density while preserving same gameplay truth.

Hardware Impact: Expected gain on i3/MX350 comes from removing PhysX trigger stay work and replacing it with contiguous SoA iteration. Exact microsecond proof is pending profiler/compile evidence.

## Decision 1 - Mandate Selection

Problem: Loot magnet touches physics avoidance, AUP math, Native jobs, inventory state, signal dispatch, debug telemetry, and presentation fakes.

Solution: Read the eight task-relevant mandate files recorded in Status before code.

Rejected Alternatives: Reading the full registry would create context noise; reading only physics would miss inventory/vault sovereignty and AUP shift safety.

Scalability potential: The selected mandates define low/middle/high/ultra behavior, especially Math LOD and fake-first VFX/audio.

Hardware Impact: Keeps MX350 path bounded by cadence and flat NativeArray access instead of Unity trigger overhead.
