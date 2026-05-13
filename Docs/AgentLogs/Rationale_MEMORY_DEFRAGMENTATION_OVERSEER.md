# Rationale_MEMORY_DEFRAGMENTATION_OVERSEER

Status: PENDING VERIFICATION

## Decision 000 - Scope Boundary

Problem: The prompt requests native heap defragmentation, but Unity/C++ heap allocations made by independent `UnsafeUtility.Malloc` calls cannot be safely compacted from managed code by shifting arbitrary addresses. Blind pointer movement would invalidate cached `NativeArray` views and corrupt live jobs.

Solution: Implement compaction inside the owned `GlobalDataVault` arena, with `H8Memory` maintaining the block map. Only vault-owned blocks can move, and the vault pointer registry is updated immediately after `UnsafeUtility.MemMove`.

Rejected Alternatives: A process-wide Unity heap compactor was rejected because the engine does not expose relocation handles for arbitrary native allocations. A copy-to-new-malloc approach was rejected for the defrag path because it does not consolidate adjacent free space and recreates fragmentation.

Scalability potential: Low runs gap analysis aggressively at 1s cadence to prevent OOM. Middle and High run at 5s cadence. Ultra gains stability headroom and can spend saved memory pressure on richer HLOD/impostor residency instead of emergency eviction.

Hardware Impact: On i3/MX350, expected gain is lower OOM risk over long sessions and fewer emergency release spikes. CPU target remains under 1.0ms per compaction slice; one move is capped at 5MB.
