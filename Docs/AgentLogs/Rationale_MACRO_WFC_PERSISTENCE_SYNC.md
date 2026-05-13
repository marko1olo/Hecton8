# MACRO_WFC_PERSISTENCE_SYNC Rationale

Status: PENDING VERIFICATION

## Decision 0: Task Scope Identification
Problem: WFC-generated outpost mutable state disappears on reload because generated topology and player mutations are not mapped into the binary delta persistence path.
Solution: Inspect existing save/database/WFC/signal contracts first, then add the thinnest interface-backed persistence bridge that stores mutable bits by absolute sector hash.
Rejected Alternatives: Direct concrete WFC-to-database reference; JSON save sidecar; full grid blob writes every state change. These violate decoupling, SaveManager contract, and microSD write budget.
Scalability potential: Low tier stores exact mutable truth with the smallest bitmask. Middle tier may batch more sectors per write. High/Ultra can spend saved IO on richer restored presentation state without changing persistence truth.
Hardware Impact: Expected i3/MX350 gain versus full 500-byte mutable byte-grid writes is sub-0.01 ms CPU and fewer microSD writes; measured proof absent.

## Decision 1: Contract Surface
Problem: WFC persistence needs to be callable before solver execution without taking a concrete outpost generator dependency.
Solution: Extend `IAsyncPersistenceService` with `TryPersistWfcOutpostStateSnapshot` and `TryApplyWfcOutpostStateOverride`, both using `NativeArray<byte>` and `WfcOutpostPersistenceStatus`.
Rejected Alternatives: SaveManager singleton lookup from WFC runtime; direct `H8MacroDatabaseService` calls from outpost code. Both would hard-couple generation to storage and violate registry-owned service boundaries.
Scalability potential: Low tier calls the exact same contract with a 500-cell grid. Middle/High/Ultra can add richer solver state later without changing the persistence call site.
Hardware Impact: Interface dispatch only; expected cost below 1 microsecond on i3/MX350.

## Decision 2: DataVault Grid And Bit Packing
Problem: A 10x10x5 outpost byte grid is cheap but still wasteful to write as a blob for each door/power/loot mutation.
Solution: Reserve `BufferID.WfcOutpostGrid` and pack four mutable planes into 32 `ulong` words through a Burst `IJob`.
Rejected Alternatives: Per-cell byte payload, JSON DTO, or managed `BitArray`. They increase IO, allocate managed memory, or make Burst validation impossible.
Scalability potential: Low tier keeps exact bit truth at 256 raw bytes before RLE. Ultra can spend saved IO on restored visual state while this truth layer stays unchanged.
Hardware Impact: Expected packing cost is under 10 microseconds for 500 cells on i3/MX350; RLE can reduce disk bytes to the 32-byte header plus short runs for mostly-default bases.

## Decision 3: Compile Boundary
Problem: Full `Hecton8.Core` compile is blocked by unrelated missing assembly references from concurrent agents and stale generated project files.
Solution: Verify the changed contract/database/memory/paging dependencies with Unity Roslyn response files where possible, then mark the full Core compile check as dependency-blocked instead of hiding it.
Rejected Alternatives: Killing the active Unity editor owned by another agent; rewriting generated `.csproj` files; fixing unrelated Audio/AI/Outpost generation references inside this WFC persistence pass.
Scalability potential: No runtime impact. This keeps the persistence patch isolated until the integration owner clears the project-wide compile wall.
Hardware Impact: Zero runtime effect; prevents false compile reporting.

## Decision 4: Dirty And Hydration Path
Problem: MacroDB writes must not happen for no-op interactions, and restore must happen before the generator treats a sector as fresh.
Solution: Drain `WfcOutpostStateChangedSignal` snapshots, compare previous/current mutable bits, and call `MarkDirty` only after a real bit transition. Drain bounded `SectorHydratedSignal` snapshots and silently attempt WFC payload decode into the DataVault grid.
Rejected Alternatives: Marking every interaction dirty; destructive signal reads that could steal hydration packets from other systems; polling MacroDB every frame. All would waste IO or create cross-system side effects.
Scalability potential: Low = at most 8 mutation signals and 4 hydration probes per tick. Middle/High/Ultra can raise these caps without changing payload semantics.
Hardware Impact: Expected hot-frame cost is under 20 microseconds for the capped mutation drain on i3/MX350; no managed allocation.

## Decision 5: Absolute Sector Key And RLE Codec
Problem: AUP shifts make local coordinates unsafe as save keys, and raw 500-byte grids waste MicroSD write bandwidth.
Solution: Use the incoming absolute `sectorHash` directly for `IMacroDatabaseService.MarkDirty` / `TryGetPayload`; encode the 32-word bitmask through `SaveBinaryPayloadCodec` with a 32-byte validation header and byte-RLE payload.
Rejected Alternatives: Derived world-pager payload hashes; local grid coordinate keys; bespoke sidecar RLE class. Derived keys break the prompt, local keys are not AUP-safe, and a sidecar codec fragments save validation.
Scalability potential: Low = exact bitmask and compact RLE. High/Ultra can add new bit planes by increasing versioned header fields while preserving the absolute key.
Hardware Impact: Worst raw WFC payload is 288 bytes including header; unchanged snapshots skip MacroDB dirty writes entirely.

## Decision 6: Solver Injection Boundary
Problem: The concrete outpost runtime is owned by the World domain and is still incomplete/blocked in the current compile graph.
Solution: Expose `TryApplyWfcOutpostStateOverride` on the core persistence service and hydrate the DataVault WFC grid from MacroDB; the World-owned generator can call it before scheduling `MarauderOutpostSolveJob` without a database dependency.
Rejected Alternatives: Editing World outpost jobs from the backend persistence pass; directly referencing `SaveManager` from WFC; inventing a new outpost runtime. Those violate the domain boundary and concurrent-agent protocol.
Scalability potential: Low/Middle/High/Ultra all consume the same exact mutable override. Visual tiering can remain in the outpost renderer.
Hardware Impact: Restore decode is bounded to 288 bytes and 500 cells; expected under 20 microseconds on i3/MX350 when invoked.

## Decision 7: Exact Persistence, Not Math LOD
Problem: WFC mutable save truth cannot degrade by distance or hardware tier without corrupting player-authored state.
Solution: Keep persistence exact for every tier and reserve Math LOD for presentation systems that consume restored state.
Rejected Alternatives: Lower-tier probabilistic restore, nearest-cell approximation, or visual-only fake state. These save negligible CPU and break deterministic reload correctness.
Scalability potential: Low uses the same exact bitmask with cheapest IO. Middle can batch more dirty sectors. High/Ultra can use saved CPU/IO to restore extra visual dressing while the truth bitmask remains invariant.
Hardware Impact: No branch cost; expected i3/MX350 benefit is correctness without extra simulation.

## Decision 8: Background Append And Zero-GC Audit
Problem: MacroDB append can touch disk/MMF and must not stall the SaveManager hot tick while WFC mutations arrive.
Solution: `MarkDirty` copies the bounded payload, then `TryAppendDirtyPayload` runs after `Awaitable.BackgroundThreadAsync()` and returns to main thread before telemetry. Packing uses fixed `NativeArray` buffers, for-loops, and a Burst `IJob`.
Rejected Alternatives: Synchronous Tick-time append; managed `BitArray`; managed byte arrays; LINQ; Coroutine disk flush. These either allocate or risk frame spikes.
Scalability potential: Low keeps append off main thread and compresses to <=288 bytes. High/Ultra can raise signal drain caps or attach richer restored visual states without changing disk truth.
Hardware Impact: Hot path queues the append only; expected main-thread saving versus synchronous append is workload-dependent and unmeasured. Packing remains bounded under 10 microseconds target on i3/MX350.

## Decision 9: Telemetry And Compile Honesty
Problem: The task demands `WfcBytesSaved` telemetry and Burst compile proof, but Unity/Burst import is unavailable and full Core compile is blocked by unrelated assemblies.
Solution: Emit `GlobalTelemetryBus.PublishModTelemetry(WFCP, WFBS, savedBytes)` on successful persist and record compile verification as dependency-blocked instead of fabricating a green result.
Rejected Alternatives: Debug logging telemetry; claiming Burst verification from static code; fixing unrelated Audio/AI/World compile errors in the backend persistence pass.
Scalability potential: Telemetry lets later tier tuning track compression effectiveness on weak storage and stronger machines.
Hardware Impact: Telemetry enqueue is expected below 1 microsecond; measured proof absent until Unity profiling is available.

## OMEGA POLISH CHANGES
Problem: The first WFC packer used four branch tests per cell and a written-only async append counter existed after the background append path was made fire-and-forget.
Solution: Replaced the four mutable-state branches with masked `ulong` OR writes inside `PackWfcOutpostMutableStateJob`; removed the unused `_wfcOutpostAppendActive` counter and `System.Threading` dependency; verified the active `SaveManager` compaction API has one method set and one `FrostTick`.
Rejected Alternatives: Keeping the readable branch form; reintroducing an append counter just for diagnostics; editing World WFC generator ownership from the backend pass. Branch readability costs unnecessary instructions, the counter had no consumer, and World call-site wiring belongs to the World domain.
Scalability potential: Low/MX350 keeps exact bit truth with the cheapest branchless pack path and <=288 byte payload. Middle raises batching only. High/Ultra spend the saved CPU/IO on restored visual dressing, richer outpost powered-state presentation, and denser loot feedback without changing persistence truth.
Hardware Impact: Expected i3/MX350 gain is small but real: four branch tests per 500 cells removed from the pack pass, plus one unused interlocked counter path removed. Measured microseconds absent; target remains under 10 microseconds for the 500-cell pack.
Cinematic Cheats Used: Exact mutable-state bitmask instead of resimulating outpost internals; byte-RLE over the bitmask instead of full grid blob; dirty-on-transition snapshot hash instead of always-writing; restore injection into DataVault grid instead of rerunning loot/power history.
Final Git Diff: Key changed files are `Assets/_Project/Scripts/SaveManager.cs`, `Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs`, `Assets/_Project/Scripts/Core/Contracts/PersistencePagingContracts.cs`, `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`, `Assets/_Project/Scripts/Core/GlobalSignals.cs`, `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`, `Docs/Tasks/Status_MACRO_WFC_PERSISTENCE_SYNC.md`, and this rationale log. Full diff remains in the worktree for integrator review.
