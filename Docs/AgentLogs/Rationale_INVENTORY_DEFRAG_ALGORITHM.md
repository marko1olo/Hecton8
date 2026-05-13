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

## Decision 2 - Algorithm Assembly Split
Problem: Sort logic inside `PlayerInventory` kept the algorithm in the monolithic Core assembly and used per-sort TempJob buffers.
Solution: Added `Hecton8.Inventory.Algorithms` with `InventoryDefragJob` in a child asmdef that references contracts and Unity native packages only.
Rejected Alternatives: Keeping the nested radix job was rejected because it required TempJob allocations per command and hid the reusable algorithm behind a player component.
Scalability potential: Low = insertion sort under 256 slots; Middle = same job for player/backpack; High = command lane can defer base lockers; Ultra = saved CPU budget can be spent on richer PDA feedback and acoustic polish.
Hardware Impact: Removes three per-sort native TempJob allocations and managed comparer risk on i3/MX350; exact microseconds PENDING VERIFICATION.

## Decision 3 - Signal Migration Without UI Breakage
Problem: PDA button was a direct call, but prompt requires `InventoryCommandSignal(Sort)` consumption and `InventoryChangedSignal` emission.
Solution: Added typed command/change signals; `RequestSortInventory` pushes the command for external observers and executes immediate local sort so current PDA refresh cadence stays valid; `LateFrameTick` consumes external sort commands.
Rejected Alternatives: Delaying PDA sort until next signal flush was rejected because current UI immediately refreshes after the click and would show stale inventory for one frame.
Scalability potential: Low = direct player sort; Middle/High/Ultra = external systems can enqueue commands without concrete references.
Hardware Impact: No frame-time savings claimed; architecture removes direct cross-domain dependency pressure.

## Decision 4 - Defrag Algorithm Shape
Problem: Prompt requires stack merge, gap compaction, and ordered sort without managed heap allocation or temporary native arrays inside the job.
Solution: Implemented one Burst `IJob` over SOA lanes: merge equivalent stack variants first, compact non-empty slots forward, then insertion sort by category/hash/count. Lane copy helpers keep hash/count/category/metadata aligned.
Rejected Alternatives: Radix sort with temporary buckets was rejected for player inventory because grids are small and extra lane buffers would either allocate per sort or permanently multiply memory. Hash-only merge was rejected because state/genetics/quality variants would corrupt item identity.
Scalability potential: Low = same in-place insertion path on toaster hardware; Middle = same path for normal backpacks; High = command lane can route larger lockers to a future sliced runner; Ultra = saved sort budget can be spent on richer PDA feedback and item affordance polish.
Hardware Impact: i3/MX350 gains come from avoiding managed comparer/list sorting and per-sort native temp allocation. Unity produced `Hecton8.Inventory.Algorithms.dll`; global compile remains blocked by unrelated domains, so exact microseconds are PENDING PROFILER.

## Decision 5 - UI/Save/Telemetry Boundary
Problem: Sort completion must update UI, save delta state, telemetry, and audio without direct cross-domain dependencies or prefab churn.
Solution: `PlayerInventory.NotifyInventoryChanged` refreshes SOA mirrors/shadow payloads, emits `InventoryChangedSignal`, and preserves the existing event for PDA tab refresh. `PDAInventoryTab.RefreshGrid` reuses prebuilt arrays and reads native read-only views. Sort timing is written to telemetry and the 300-frame black-box ring; acoustic feedback is an event-bus signal.
Rejected Alternatives: Direct PDA refresh from the Burst job was rejected because it couples algorithm code to UI. Direct save compressor calls from sorting were rejected because save owns byte blitting and checksum semantics.
Scalability potential: Low = no prefab churn on weak hardware; Middle = deterministic UI refresh from native mirrors; High = external systems can consume `InventoryChangedSignal`; Ultra = richer audio/UI response can be layered onto the same signal.
Hardware Impact: i3/MX350 avoids managed UI rebuild churn and keeps save reads as native byte copies. Exact microseconds are PENDING PROFILER because the global project compile is blocked by unrelated domains.

## Decision 6 - Massive Locker Awaitable Gate
Problem: Task 17 requests frame-sliced Awaitable sort only if a massive base locker over 1000 items exists.
Solution: Scanned construction, gameplay, and inventory domains. Visible storage types are `StorageCrate` content arrays and `PressurizedContainer` protection wrappers; no 1000+ base-locker inventory owner or async sort integration point exists.
Rejected Alternatives: Inventing a locker API or binding to speculative future code was rejected under the concurrent-agent dependency rule.
Scalability potential: Low/Middle = player inventory uses in-place Burst sort. High/Ultra = future locker system should enqueue `InventoryCommandSignal(Sort)` and own its sliced runner behind a registry/event contract.
Hardware Impact: No current code path. Avoids adding uncalled async machinery and continuation latency debt on low-end silicon.
