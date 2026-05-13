# Rationale_INVENTORY_DEFRAG_ALGORITHM

Status: PENDING VERIFICATION

## Decision 0 - Scope Gate
Problem: Inventory sort prompt requires replacing managed singleton/comparer sorting without crossing domain boundaries.
Solution: Treat Echelon 4 / S.O.A. Inventory System as authoritative scope; use native arrays, Burst jobs, typed signals/interfaces already present in the codebase.
Rejected Alternatives: Direct concrete references to UI/save/audio systems are rejected because batch agents run concurrently and AGENTS requires `EventBus` or `GlobalRegistry` boundaries.
Scalability potential: Low = insertion sort for small inventories; Middle = same Burst path with larger slot count; High = optional deferred/sliced flow for base lockers; Ultra = saved CPU budget can drive richer inventory UI/audio feedback without changing data layout.
Hardware Impact: i3/MX350 target expects removal of 10 ms managed-sort spike; exact measured gain is PENDING VERIFICATION until compile/profiler evidence exists.

## Decision 1 - Mandate Selection
Problem: The prompt touches inventory data, native memory, telemetry, save readability, UI sync, and decoupled command routing.
Solution: Loaded inventory SOA, zero-GC, native collection/job, registry/event, telemetry, save delta, and UI data streaming mandates before code.
Rejected Alternatives: Starting from `InventorySorter` implementation alone is rejected because singleton removal can silently break UI/save/audio command paths.
Scalability potential: Low/Middle/High/Ultra tiers remain data-layout compatible; sorting cost stays predictable rather than object-graph dependent.
Hardware Impact: Static mandate alignment avoids managed allocations and cache-miss-heavy object sorting on low-end silicon.
