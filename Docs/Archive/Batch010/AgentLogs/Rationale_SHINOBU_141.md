# Rationale_SHINOBU_141

Status: STATIC IMPLEMENTATION PATCHED / LEGACY SYNC CONTRACT HARDENED / RUNTIME ASMDEF ISOLATED / UNITY COMPILE PENDING

## Initial Mandate Selection
Problem: SOA inventory routing crosses runtime DTO layout, native jobs, AUP, typed signal, and telemetry domains.
Solution: Read and bind implementation to DATA_Inventory_Resources_Items_SOA_Layout, DATA_Runtime_Struct_Layout_ARM64, OPT_Zero_GC_Policy_AllocFree_Mandate, OPT_Native_Memory_Collections_JobSystem_Protocol, ARCH_Global_Registry_ServiceLocator_DI_Init, ARCH_Signal_Lane_Segregation, MATH_AUP_Determinism_Sync, DBG_Telemetry_Crash_Reporting_PostMortem.
Rejected Alternatives: Starting from PlayerInventory only would miss DataVault/generation/disposal boundaries and would likely create another local native heap.
Scalability potential: Low uses bounded time-sliced resource scans; middle increases per-frame slot window; high/ultra spend saved CPU on presentation signals and richer editor telemetry, not gameplay truth bloat.
Hardware Impact: Expected low-end i3/MX350 gain comes from replacing object/list scans with contiguous 32-byte records and Burst linear reads. Static estimate before profiling: removes managed GC spikes and converts 100k-slot scan from pointer-chasing to streaming memory access.

## Global Authority Constraint
Problem: Task requests GlobalDataVault ownership, but adding or changing global routes requires owner/phase/cadence/failure-mode evidence.
Solution: Reuse existing vault/signal APIs if present; if missing, implement owner-local compile-safe inventory infrastructure and document the absent integration instead of inventing cross-domain surface.
Rejected Alternatives: Adding new GlobalRegistry slots or catch-all EventBus routes would violate authority boundaries and create merge conflicts with 20+ agents.
Scalability potential: Owner-local buffers can later be promoted to vault handles without changing DTO layout or job kernels.
Hardware Impact: Avoids extra indirection and registry polling in hot paths on weak CPUs.

## First 20 Minutes Route Constraint
Problem: A pure SOA inventory architecture win is not sufficient unless it serves the selected route: boot -> world load -> swim -> find resource -> tool interaction -> craft/repair/build -> hazard response -> save/load.
Solution: Bind the inventory router to the resource and craft/repair/build moments. The SOA query/index path supports fast Copper Wire/fabricator/base-support availability checks; the container-window bridge is the safe boundary required before legacy lockers can publish into route truth.
Rejected Alternatives: Directly replacing `BaseLogisticsNetwork`/`StorageCrate` calls now was rejected because no stable container hash/AUP/reservation authority is supplied by that owner yet.
Scalability potential: Low uses dirty-window publishes and sliced queries; middle increases slice windows; high/ultra can keep more route resources indexed and spend saved CPU on transfer presentation while preserving the same save/load truth.
Hardware Impact: Expected low-end benefit is eliminating repeated object scans during recipe UI refresh. Measured microseconds require Unity profiler proof on the Copper Wire route, so no measured claim is recorded.
First 20 Minutes moment: resource -> craft/repair/build -> save/load.
Route impact: Faster and safer resource availability/reservation path for the selected craft route.
Proof required: Unity import/Console, Play Mode Copper Wire route, fabricator query stress, 0B GC hot-path capture, profiler frame sample, save directory diff, and reload same-state verification.
Parked work rejected: cross-domain `StorageCrate` mutation until owner-provided stable identity/AUP exists.

## SOA Vault Route
Problem: Legacy storage queries iterate Unity objects and cannot scale across hundreds of lockers without L1 misses and managed-object pressure.
Solution: Added `InventorySlotDTO` as a 32-byte explicit-layout vault record and `InventoryRoutingNetwork.EnsureBuffers()` to request all persistent memory through `GlobalDataVault` handles. Query truth is `BufferID.ShinobuInventorySlots`, not a locker object graph.
Rejected Alternatives: Rewriting `StorageCrate` directly was rejected because it owns player interaction and scene serialization, not global routing truth; changing it blindly would create cross-domain breakage with base/power ownership.
Scalability potential: Low uses sliced scans over the active dense prefix; middle increases slice size; high and ultra keep more query counters live and spend saved CPU on richer presentation signals/heatmap telemetry.
Hardware Impact: 32-byte stride streams cleanly through ARM64/AVX cache lines; static estimate: 100k slots is 3.2 MB sequential memory, replacing scattered object references and repeated crate loops.

## BufferID Collision Repair
Problem: Initial draft used `70142..70149`, colliding with existing VRSomatic IDs. A later audit found `71340..71350` were already occupied by `AbyssalShadowBufferIds`, even though most of that range was not represented in the `BufferID` enum.
Solution: Moved SOA inventory IDs to `73120..73132` and recorded the route in `H8Memory.cs` plus the architecture card.
Rejected Alternatives: Reusing `ShinobuInventorySignalScratch`/`DumpScratch` for counters/cursor or sharing the graphics culling range was rejected because mixed payload types under one BufferID can corrupt vault ownership.
Scalability potential: Dedicated IDs allow low/middle/high/ultra buffer capacities without cross-system type collisions.
Hardware Impact: Prevents catastrophic memory aliasing on all hardware; no runtime cost.

## Aggregation And Hash Map
Problem: Unity `NativeParallelHashMap` does not expose a safe ref-value atomic increment API for `ParallelWriter`, yet the task requires concurrent resource totals and caller-owned hash-map lookup.
Solution: Use `InventoryAtomicCounter64[]` as the parallel atomic accumulation surface, then `FlushPaddedTotalsToHashMapJob` writes a caller-owned `NativeParallelHashMap<uint,int>` for O(1) item lookup.
Rejected Alternatives: Direct duplicate-key `ParallelWriter.TryAdd` was rejected because it loses aggregation semantics; managed `Dictionary<string,int>` was rejected because it reintroduces GC/rehash risk.
Scalability potential: Low runs smaller slot windows; middle/high/ultra use the same counters and only change slice size. The lookup remains O(1) after each flush.
Hardware Impact: 64-byte counters avoid false sharing on multi-core ARM64/desktop; expected low-end gain is reduced cache invalidation during parallel query writes.

## Atomic Transaction And Dear Lie
Problem: Concurrent drones/fabricators can duplicate items if source/destination slots mutate without a deterministic lock protocol.
Solution: `InventoryTransactionJob` acquires two slot locks with `Interlocked.CompareExchange` on `ReservedLock`, mutates integers instantly, and emits `LogisticsTransferSignal` for presentation.
Rejected Alternatives: `lock`, `Monitor`, coroutine transfer, or physical item-in-pipe simulation were rejected as non-Burst and CPU-wasteful.
Scalability potential: Low can drop or coalesce presentation signals while preserving integer truth; high/ultra can render stronger pipe UV/shader effects from the same signal payload.
Hardware Impact: On i3/MX350 the transaction path stays a few atomic operations and two struct writes; the rejected physical transfer path would allocate/transforms/physics-update per moved item.

## AUP And Rollback Fence
Problem: 100km logistics range makes absolute float distance invalid and object serialization too slow for rollback.
Solution: `ContainerAUPHash` reconstructs double3; aggregation subtracts query AUP in double first, then casts local delta to float3. `InventoryRollbackSnapshotJob` memcopies contiguous DTO bytes into caller-owned rollback pages.
Rejected Alternatives: Absolute world floats and per-container serialization were rejected.
Scalability potential: Low/middle/high/ultra use identical deterministic truth; only scan cadence changes.
Hardware Impact: Prevents precision jitter near world edges and keeps rollback memory bandwidth linear.

## Editor Isolation And Human Control
Problem: A tuner in the shared `Editor` folder with a broad asmdef would pull unrelated editor scripts into a new assembly and damage compile-wall isolation.
Solution: Moved the tuner under `Assets/_Project/Scripts/Editor/InventoryRouting/` with `Hecton8.InventoryRouting.Editor.asmdef` referencing the inventory routing runtime assembly, `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, and Unity packages. Added an `InitializeOnLoadMethod` layout guard that calls `InventoryRoutingNetwork.ValidateRuntimeLayoutOrThrow()` during editor import.
Rejected Alternatives: Root editor asmdef, runtime MonoBehaviour tuner, and passive layout warning text were rejected.
Scalability potential: Designers can tune radius, slice batch, and decay multiplier without C# edits; low/middle/high/ultra behavior remains continuous.
Hardware Impact: Editor-only; no player runtime cost.

## Compile Wall Status
Problem: The generated `.csproj` files currently do not include the new InventoryRouting source files, so `dotnet build Hecton8.Core.csproj` would not validate this code and would produce false evidence.
Solution: Per user instruction, no dotnet build was launched. CPU/dotnet gate was checked before the first build decision: CPU 29 percent, no `dotnet` or `csc` process. A later gate check reported CPU 62.2 percent with active `dotnet` and `csc`, so build remained forbidden. After route-binding docs, CPU reported 100 percent with no `dotnet`/`csc`; the CPU gate alone still forbids build. The runtime source was then moved out of the root `Hecton8.Core` asmdef into `Hecton8.Inventory.Routing.Runtime` so inventory changes no longer force a root Core recompile.
Rejected Alternatives: Editing generated `.csproj`, leaving the runtime under root Core, or launching a meaningless build was rejected.
Scalability potential: Preserves developer iteration time and avoids fake proof.
Hardware Impact: Avoids unnecessary CPU load during multi-agent work.

## Polish Re-Audit: Slice Accumulation And Hash Index
Problem: A one-frame aggregation flush gives partial results under thermal slicing, and the first padded-counter path still scans requested hashes per active slot.
Solution: Added cumulative slice controls: padded counters can skip clear after the first frame, and `ScheduleResourceHashIndexLookup` builds caller-owned open-addressed key/total arrays before O(1) requested-hash lookup.
Rejected Alternatives: Maintaining a managed `Dictionary<string,int>` mirror or forcing every query to rescan every locker object was rejected. Direct concurrent `NativeParallelHashMap` value atomics remain rejected because the Unity container does not expose a safe ref-value atomic add surface.
Scalability potential: Low processes smaller slot windows over multiple frames without losing prior slice totals; middle/high/ultra increase slice size and can rebuild the open-address index more aggressively for responsive fabrication UI.
Hardware Impact: On i3/MX350, expected benefit is bounded per-frame memory bandwidth and fewer repeated requested-hash scans. On desktop, the same index path trades one streaming O(N) pass for O(1) repeated recipe lookups.

## Polish Re-Audit: NaN And AUP Presentation Fence
Problem: `GlobalQualityWeight` and distance inputs can become non-finite, and the original transfer visual midpoint cast an absolute AUP midpoint to `float3`.
Solution: Added finite sanitizers for quality and non-negative distances. Distance gates fail closed on non-finite local deltas. `LogisticsTransferSignal.VisualMidpoint` now carries a local midpoint offset from source to destination while AUP hashes carry authoritative endpoints.
Rejected Alternatives: Trusting Homeostasis or passing absolute float world positions was rejected because 100 km map edges amplify float precision loss.
Scalability potential: All quality bands use identical deterministic truth. Low/middle/high/ultra differ in cadence and presentation budget, not in unsafe coordinate math.
Hardware Impact: Prevents rare NaN propagation and avoids far-origin float jitter; cost is one finite check in the route gate.

## Polish Re-Audit: Unity Asset Metadata
Problem: New Unity assets without `.meta` files would get editor-generated GUIDs, creating nondeterministic source-control churn and possible reference drift.
Solution: Added stable `.meta` files for `Inventory/Routing`, `InventoryRoutingNetwork.cs`, `Hecton8.Inventory.Routing.Runtime.asmdef`, `Editor/InventoryRouting`, `InventoryRoutingNetworkTunerWindow.cs`, and `Hecton8.InventoryRouting.Editor.asmdef`. GUID uniqueness was checked by static `rg`.
Rejected Alternatives: Waiting for Unity import to create GUIDs was rejected because this batch is multi-agent and source identity must be stable before import.
Scalability potential: No runtime scalability effect; protects compile/import determinism for all hardware targets.
Hardware Impact: No runtime cost.

## Polish Re-Audit: Runtime Assembly Isolation
Problem: The first static patch placed `InventoryRoutingNetwork.cs` under the root `Hecton8.Core` asmdef. That would make every inventory-routing edit dirty the broad Core assembly and weaken the Compile Wall.
Solution: Moved the runtime source to `Assets/_Project/Scripts/Inventory/Routing/` and added `Hecton8.Inventory.Routing.Runtime.asmdef`. The asmdef references `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, and Unity packages only. The editor asmdef now references the runtime assembly explicitly.
Rejected Alternatives: Leaving the file under root Core, widening the existing inventory root assembly to include unrelated item/corrosion files, or referencing scene-facing storage/logistics assemblies.
Scalability potential: Runtime behavior is unchanged; developer iteration scales better because SHINOBU_141 code no longer invalidates the largest project assembly on every edit.
Hardware Impact: Build-time/iteration impact only. Runtime CPU and memory behavior unchanged.

## Verification: Runtime Assembly Isolation Static Pass
Problem: After relocation, stale generated project files and source caches can make path checks lie about what Unity will import.
Solution: Ran focused static checks on the new path. Old runtime path is absent; new runtime path and runtime/editor asmdefs are present. `InventoryRoutingNetwork.cs` remains brace-balanced at 194/194 with 19 `*Job` structs and 19 deterministic Burst attributes after the fixed-stride repair. Forbidden hot-path grep found no `foreach`, LINQ Select/Where, `UnityEngine.Random`, new native containers, direct `StorageCrate`/`BaseLogisticsNetwork`/`PowerGrid` references, or hot DTO auto-properties. GUID grep found exactly one hit each for the routing folder, runtime asmdef, and preserved runtime file GUID. Generated `.csproj` search still finds none of the new routing/editor entries. CPU gate reports 100 percent and no `dotnet`/`csc`; build remains forbidden and still not authoritative until Unity regenerates projects.
Rejected Alternatives: Treating the source move as compile proof or editing generated project files by hand.
Scalability potential: No runtime algorithm change; compile-wall isolation improves future iteration under multi-agent source churn.
Hardware Impact: No frame-time impact; avoids unnecessary local build pressure while CPU is saturated.

## Polish Re-Audit: Legacy Storage Bridge Boundary
Problem: Static audit proved `BaseLogisticsNetwork.CountAccessibleItem/TryReserveResources` still walks `StorageCrate` arrays, but direct replacement would create two authority planes: legacy `_reservedSlotIds` inside `StorageCrate` and SOA `ReservedLock` inside `InventorySlotDTO`.
Solution: Added an inventory-owned, data-only container sync contract instead of editing construction/gameplay. `InventoryContainerRangeDTO` records `ContainerHash`, `ContainerAUPHash`, fixed slot window, active count, and flags. `PublishInventoryContainerSnapshotJob` now claims/resolves a window and rewrites it from caller-owned native hash/quantity/lock arrays inside one job; `ClearInventoryContainerRangeJob` clears and releases the range on unregister. `ContainerSyncResult` is diagnostic output only and no longer sits between claim and publish state mutation.
Rejected Alternatives: Referencing `StorageCrate` from the inventory domain, caching managed crate objects in a dictionary, or swapping `BaseLogisticsNetwork` counts to SOA without mirrored reservation/commit semantics. All three would violate owner-local authority or create double-spend risk.
Scalability potential: Low/middle devices can publish only dirty container windows and query the SOA array in sliced chunks. High/ultra can keep more container windows hot and spend the saved CPU on richer pipe-flow presentation signals while maintaining the same integer truth.
Hardware Impact: Future bridge path converts repeated object scans into fixed-window contiguous writes plus SOA reads. Static estimate: avoids hundreds of `StorageCrate.CountItemByHash` loops during fabricator UI refresh, but measured microseconds remain pending Unity/profiler proof.

## Polish Re-Audit: Pinned Range Compaction
Problem: Fixed container windows and dense global compaction conflict; a normal swap-and-pop compaction would invalidate the owner-published slot window for a container.
Solution: Added `ConditionContainerRangePinned`. `PublishInventoryContainerSnapshotJob` marks every window slot pinned, including empty slots, and `CompactInventoryArrayJob` preserves pinned windows while compacting non-pinned SOA slots around them.
Rejected Alternatives: Disabling compaction globally or allowing compaction to move legacy-published slots. The first leaves fragmentation; the second corrupts range ownership.
Scalability potential: Mock/global SOA slots still compact into dense memory. Owner-published legacy windows remain stable until the storage owner migrates fully to SOA mutation.
Hardware Impact: Minimal extra branch in the FrostTick compaction job; prevents far larger cache/correctness loss from stale mappings.

## Polish Re-Audit: Container Range CAS Boundary
Problem: The new range claim path could overwrite a range after seeing the same hash from a concurrent publisher, and clear could mutate slot payload or default a range even when hash->zero CAS failed.
Solution: Claim now writes a new `InventoryContainerRangeDTO` only after winning the zero->containerHash CAS. Clear now clears the slot window and defaults the range only when the expected container hash was actually exchanged to zero.
Rejected Alternatives: Relying on the current single-owner/chained lane as the only protection was rejected because future callers can still schedule parallel publishers incorrectly.
Scalability potential: Low/middle/high/ultra use the same atomic ownership rule; increasing publisher count later requires result-lane expansion, not weakening the CAS boundary.
Hardware Impact: No measurable hot-path cost; it removes a correctness race before profiler work.

## Polish Re-Audit: Container Window Stride
Problem: The first container bridge computed `SlotStart` as `rangeIndex * requestedSlotCapacity`. If one container requested 128 slots and a later container requested 64, their ranges could overlap and create cross-container slot corruption.
Solution: Added a fixed `DefaultContainerSlotStride` of 64 slots. `PublishInventoryContainerSnapshotJob` computes starts as `rangeIndex * DefaultContainerSlotStride` and fails with `ContainerRangeCapacityExceeded` when a request exceeds that stride. Existing ranges with unaligned `SlotStart` are rejected. Range write-back uses the fixed stride instead of dividing by variable `SlotCapacity`.
Rejected Alternatives: Variable-width window starts without a separate atomic slot allocator, or silently truncating oversized containers.
Scalability potential: Low/middle/high/ultra keep identical range ownership; larger containers require an explicit future owner contract and a separate slot allocator rather than accidental overlap.
Hardware Impact: No measurable hot-path cost; prevents corrupted SOA windows that would invalidate query and save/load proof.

## Polish Re-Audit: Transaction Pin Preservation
Problem: `InventoryTransactionJob` could empty a pinned container-window source slot and clear all condition flags, or fill an empty pinned destination and overwrite its pin bit with the source flags.
Solution: Preserve `ConditionContainerRangePinned` separately from item condition state. Emptying a pinned source leaves the pin bit active. Filling an empty pinned destination ORs the destination pin bit into the moved condition flags.
Rejected Alternatives: Treating pin state as regular item condition state was rejected because compaction would later move owner-published windows and corrupt container-to-slot mapping.
Scalability potential: Low/middle/high/ultra keep identical reservation truth; dirty-window publishing remains stable across transaction churn.
Hardware Impact: Two bitwise masks in the transaction success path; no measured runtime claim.

## Polish Re-Audit: Subagent Concurrency Burn-Down
Problem: Read-only subagent audit found that `ContainerSyncResult[0]` was a shared scratch slot between range claim and publish. Parallel snapshot calls could overwrite the scratch range before the second job consumed it, and the claim job published `ContainerHash` before the rest of the range was visible.
Solution: Removed `ClaimInventoryContainerRangeJob` from the state path. `ScheduleContainerSnapshotPublish` now schedules a single `PublishInventoryContainerSnapshotJob` that claims/resolves the range and writes the slot window in one job. New ranges reserve `StateFlags` with `ContainerRangeMutating`, write non-hash fields, then publish `ContainerHash` with an atomic exchange. Existing ranges must acquire the same mutating flag before any slot rewrite. `ContainerSyncResult` is now diagnostic result output only, not a state dependency.
Rejected Alternatives: Keeping a single global scratch result, relying on caller discipline for all future parallel publishers, or publishing a final hash before the range payload was coherent.
Scalability potential: Low devices still publish dirty container windows only; middle/high/ultra can run more publishers without corrupting range ownership. Same-container concurrent writes fail closed and must be rescheduled by the owner.
Hardware Impact: Adds a few atomic operations per container publish, outside the per-slot query hot path. Prevents rare corruption that would otherwise invalidate O(1) lookup truth and save/load proof.

## Polish Re-Audit: Atomic Reads And Safety Exceptions
Problem: The open-addressed hash index read `IndexKeys` through a naked load while other workers inserted with CAS. `TransferSignalWriter` used `NativeDisableContainerSafetyRestriction` without the mandated written invariant. `PackAupHash` cast non-finite doubles to long.
Solution: Replaced the hash-key naked load with an atomic `Interlocked.CompareExchange(ref keyRef, 0, 0)` read, documented the NativeQueue safety exception in three paragraphs, exposed `ScheduleTransactions()` so transfer emission sources its writer from `SignalBus<LogisticsTransferSignal>`, and collapsed non-finite AUP components to the zero-AUP hash before quantization.
Rejected Alternatives: `Volatile.Read` was rejected to avoid Burst support ambiguity; managed queues and deleting transfer signals were rejected because they break zero-GC and the Dear Lie presentation lane.
Scalability potential: The O(1) index path remains unchanged across low/middle/high/ultra. The visual transfer signal can still be downsampled under low `GlobalQualityWeight` without weakening integer truth.
Hardware Impact: Atomic reads remove ARM/Burst data race risk. Non-finite AUP guards prevent poison hashes. Queue comment has no runtime cost.

## Polish Re-Audit: Editor Assembly And External BufferID Finding
Problem: The editor tuner used `IDataVault` but its asmdef relied on transitive references. Subagent also reported a P0 duplicate between `SaveWorldPagerWriteArena` and `ConstructionBuilderOccupancy`.
Solution: Added direct editor asmdef references to `Hecton8.Core.Contracts` and `Hecton8.Core.Memory`, and updated `Docs/DEPENDENCY_GRAPH.md`. Rechecked the current `H8Memory.BufferID` enum: `SaveWorldPagerWriteArena=70200`, `ConstructionBuilderOccupancy=70319`, and no duplicate BufferID values remain in the enum range.
Rejected Alternatives: Depending on transitive asmdef references, or blindly renumbering save/construction IDs without confirming current source state.
Scalability potential: Editor-only import correctness; no runtime algorithm change.
Hardware Impact: Prevents Unity import failure and vault ID aliasing. No frame-time cost.
