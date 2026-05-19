# SHINOBU_100 Rationale

Date: 2026-05-19
Agent: SHINOBU_100
Evidence class: STATIC_SOURCE until Unity compile/import/profiler proof exists.

## Decision 00 - Scope Gate

Problem: Batch target requests global elimination of owner-blocked native arrays, but project architecture forbids converting local scratch into global heap without real cross-domain/save/replay/telemetry ownership.

Solution: Apply GlobalDataVault only to persistent AI/Physics state that crosses rollback/save/telemetry/job-owner boundaries. Keep cold local scratch local unless source proves it survives across frames as authority.

Rejected Alternatives: Rejected blind replacement of every `NativeArray` token because `GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md` explicitly rejects DataVault buffers for local scratch. Rejected new global surface for absent systems because `GLOBAL_AUTHORITY_BOUNDARIES.md` marks it as merge blocker.

Scalability potential: Low tier avoids DataVault bloat from scratch buffers; Middle tier gets stable generation-checked authoritative buffers; High tier can add denser telemetry and presentation consumers; Ultra tier spends saved simulation cost in `VISUAL_SYNC` without changing gameplay truth.

Hardware Impact: Estimated gain on i3/MX350 is avoiding allocator churn and rollback snapshot misses; expected per-frame saving cannot be claimed without profiler/GCMonitor. Static target is sub-100 microseconds per migrated buffer family by removing stale local ownership and generation misses.

## Decision 01 - Mandate Selection

Problem: SHINOBU_100 touches memory topology, DTO layout, rollback determinism, execution phases, signals, and crash telemetry.

Solution: Read eight mandates before code: native memory/job protocol, ARM64 layout, zero-GC, AUP determinism, deterministic RNG, execution phases, signal lane segregation, and post-mortem telemetry.

Rejected Alternatives: Rejected reading only AGENTS.md because task-specific registry files define current runtime DTO/Vault rules. Rejected broad documentation crawl before source scan because batch requires evidence-based implementation, not report inflation.

Scalability potential: Low/Middle/High/Ultra behavior must be continuous through `GlobalQualityWeight`, with authority invariant across tiers.

Hardware Impact: No runtime gain yet. Prevents compile/runtime regressions caused by Pack=1 DTOs, unmanaged lane drift, or hidden hot-path allocations.

## Decision 02 - VehicleMotor Vault Migration

Problem: `VehicleMotor` held three local persistent `NativeArray` allocations for headless submarine state and deferred capsule sweep commands/results. This state participates in physics, presentation, and rollback-adjacent movement, but the Vault could not snapshot or relocate it.

Solution: Added `BufferID.VehicleMotorSubmarineStates`, `VehicleMotorSweepCommands`, and `VehicleMotorSweepResults`; replaced local allocations with `VaultBufferHandle<T>` resolution and DataVault buffer locks while the scheduled sweep job owns pointers. Teardown now tombstones local handles and clears the motor slot instead of freeing owner-local native memory.

Rejected Alternatives: Rejected one shared single-element Vault buffer because multiple registered motors would alias the same state. Rejected retaining `Dispose(JobHandle)` because DataVault owns lifecycle after migration. Rejected creating per-instance enum IDs because BufferID is a global ABI and per-instance enum growth is unbounded.

Scalability potential: Low uses same authority buffer with one command/result slice per registered motor; Middle/High/Ultra can increase visual interpolation and telemetry consumers without adding new authority arrays.

Hardware Impact: Removes three owner-local persistent arrays per active vehicle. On i3/MX350 the direct allocation saving is scene-lifetime, not per-frame; expected hot-path gain is stale-handle/rollback safety, not measurable CPU until profiler proof. Estimated avoided allocator/registration overhead per vehicle boot: 80-150 microseconds static estimate.

## Decision 03 - DTO Layout And NoAlias

Problem: `VehicleMotor.SubmarineState` used `Pack=1`, and fauna parasite attach DTOs had implicit layout. Burst job arrays in fauna/submarine jobs lacked explicit NoAlias metadata after consolidation into Vault-backed memory.

Solution: Rebuilt `VehicleMotor.SubmarineState` as explicit 112-byte layout, rebuilt `ScheduledSweepState` as explicit 48-byte non-native helper, rebuilt fauna parasite input/result as explicit 80/64-byte DTOs, and added `[NoAlias]` to migrated NativeArray job fields.

Rejected Alternatives: Rejected `Pack=1` because ARM64 runtime structs must not trade bytes for misaligned loads. Rejected property wrappers because CS1612 risks stack-copy mutations. Rejected relying on Burst alias analysis because Vault memory co-location makes conservative alias assumptions plausible.

Scalability potential: Low gains predictable aligned loads; Middle/High get SIMD-friendly job fields; Ultra can add richer presentation data outside these authoritative DTOs.

Hardware Impact: Expected gain is reduced copy/alias penalty in Burst jobs. No profiler proof yet. Static estimate: 5-25 microseconds per 5k fauna job batch depending on vectorization and cache pressure.

## Decision 04 - Continuous Quality Striding

Problem: `Submarine6DIntegratorJob` used a thermal binary low-LOD branch and hard `Frame & 3` cadence. Batch requires continuous `GlobalQualityWeight` consumption.

Solution: Added `GlobalQualityWeight` to the job and mapped it to stride 1-4 continuously. Skipped frames run deterministic dead reckoning from last velocity/rotation, keeping presentation motion coherent while reducing expensive solver passes.

Rejected Alternatives: Rejected `if (isLowEndHardware)` because binary quality switches are prohibited. Rejected skipping writes completely because renderer/audio would observe frozen truth. Rejected Unity random or wall-clock staggering; phase uses deterministic `Frame + index`.

Scalability potential: Low quality weight approaches stride 4; Middle operates around stride 2-3 under pressure; High/Ultra converge to stride 1 and can spend the saved budget in VISUAL_SYNC effects.

Hardware Impact: At quality 0.1, heavy submarine solve cadence drops toward 25% for affected vehicles while dead reckoning remains cheap. Static estimate: saves 10-40 microseconds per 16-vehicle batch depending on active forces; measured proof absent.

## Decision 05 - Signal Bridges Without Owner-Local Queues

Problem: SubmarineDynamics owned three persistent `NativeQueue` bridges and AcousticEcho owned one persistent tap queue. These allocations were outside the Vault/signal authority and were invisible to rollback/state audit.

Solution: Moved submarine mock flood, mock impact, and cavitation payloads to typed `SignalBus<T>` lanes. Moved AcousticEcho pending taps into `BufferID.AcousticEchoPendingTaps` as a Vault buffer because the frame tap buffer is read asynchronously by `EchoTrackingJob` and cannot be reused as a producer staging area.

Rejected Alternatives: Rejected writing AcousticEcho producers directly into `AcousticEchoFrameTaps` because it races the scheduled tracking job. Rejected keeping owner-local queues because they are not snapshot-owned. Rejected introducing a new managed list or event because it violates Zero-GC.

Scalability potential: Low tier sheds through SignalBus low-tier capacity and uses one Vault pending tap buffer; Middle tier preserves normal echo density; High/Ultra can raise signal lane limits or add richer acoustic presentation without changing authoritative AI state ownership.

Hardware Impact: Removes four owner-local persistent queues from AI/vehicle physics source. Static runtime gain is allocator/lifecycle safety, not measured frame time. Expected i3/MX350 gain is reduced session memory fragmentation and no per-owner queue drain overhead beyond fixed SignalBus flush.

## Decision 06 - Rollback Layout Fence

Problem: Sequential submarine/acoustic DTO layouts left netcode memcpy offsets implicit and platform-dependent enough to fail the ARM64 layout mandate.

Solution: Converted changed hot DTOs to `[StructLayout(LayoutKind.Explicit, Size = ...)]` with exact `[FieldOffset]` declarations. Submarine state/control/mass/PID/force/config/telemetry and acoustic tap/hunt/trail/blackbox rows now have stable byte contracts. Submarine and acoustic jobs use deterministic Burst float mode.

Rejected Alternatives: Rejected relying on current C# sequential order because future field insertion would silently break rollback snapshots. Rejected `Pack=1` because ARM64 unaligned reads are forbidden.

Scalability potential: Low/Middle/High/Ultra all share identical authoritative state bytes; only cadence/math changes with `GlobalQualityWeight`, so scalability cannot alter rollback truth.

Hardware Impact: Runtime speedup is not claimed without Burst disassembly/profiler. The concrete gain is lower desync risk and aligned, explicit DTO surfaces for blind `UnsafeUtility.MemCpy`.

## Decision 07 - Vault Sovereignty Telemetry Ring

Problem: `VaultSovereigntyTelemetryRing` BufferID existed after migration, but no 300-frame SHINOBU_100 ring or dump path wrote evidence for Vault bytes/stride/faults.

Solution: Added explicit 64-byte `VaultSovereigntyTelemetryEntry` and static `VaultSovereigntyTelemetry` writer. `SubmarineDynamicsRuntime` records Vault allocated bytes, arena bytes, block count, stride multiplier, quality weight, frame, source hash, and fault flag. Fatal submarine state also triggers `Docs/AgentLogs/Dump_SHINOBU_100.bin`.

Rejected Alternatives: Rejected reporting profiler timings without profiler data. `GenerationMisses` and `MaxMemoryJobUs` are present in the ABI but currently written as zero until dispatcher/Vault miss counters are routed.

Scalability potential: Low tier records stride inflation and capacity pressure; Middle/High/Ultra can correlate richer memory jobs and visual overkill with the same ring without changing file format.

Hardware Impact: Static estimate under 2 microseconds per record due one Vault array write. Fault dump is cold-path IO only.

## Decision 08 - Culling Allocation Boundary (Superseded By Decision 17)

Problem: Static scan still finds `GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs` owning one `NativeParallelMultiHashMap` spatial hash and two `NativeQueue` lanes.

Solution: Do not rewrite the SHINOBU_37 culling spatial hash inside this pass without replacing its query contract and parallel changed-index writer. Mark as remaining dependency boundary for Task 15 instead of pretending the global scan is clean.

Rejected Alternatives: Rejected blind replacement with `SignalBus` for changed indices because Burst jobs require a parallel writer and the downstream culling dispatch currently drains queue semantics. Rejected an O(n) linear spatial scan as it would buy memory purity by burning frame time.

Scalability potential: Low tier needs a cheaper spatial representation, likely Vault bucket arrays plus fixed head/next-index SoA. High/Ultra can keep denser wake candidates, but it still needs one owner and one compaction route.

Hardware Impact: Superseded. Loop 4 migrated the three culling containers into Vault-backed SoA/counter buffers; see Decision 17.

## Decision 09 - UI Toolkit Vault Facade

Problem: The existing Vault X-Ray was IMGUI-only and did not expose the `GlobalQualityWeight` stride relationship requested by Task 18.

Solution: Rebuilt `VaultXRayWindow` on UI Toolkit with a fixed 256-bar heatmap, allocation/arena/generation labels, fragmentation ratio, and continuous quality-derived stride display. It reuses the existing window/menu route instead of adding a duplicate editor tool.

Rejected Alternatives: Rejected a second `VaultSovereigntyWindow` because it would split debugging authority. Rejected per-refresh child allocation by pre-creating heatmap bars once in `CreateGUI`.

Scalability potential: Low tier pressure is visible through stride/fragmentation; Middle/High/Ultra can inspect larger active block maps without changing runtime systems.

Hardware Impact: Editor-only. Runtime impact is 0 microseconds.

## Decision 10 - AUP Sector Local32 Compaction

Problem: Vault AUP rows preserved 64-bit sector/local truth, but hot physics and debug consumers still needed a stable 32-bit local row so they do not perform broad-world double math in every tight loop.

Solution: Added explicit 64-byte `VaultAupSectorLocal32` and deterministic `VaultAupPrecisionDeltaCompactionJob`. The job wraps local offsets across the 5000m sector boundary, increments/decrements 64-bit sector IDs, writes local `float3` into hot rows, and keeps rollback truth blit-safe.

Rejected Alternatives: Rejected using absolute world floats because 100km coordinates amplify jitter. Rejected running physics directly on `double3` every frame because it burns bandwidth and ALU for data that should be localized once.

Scalability potential: Low tier gets one compact sector-local row per entity with minimal math; Middle/High/Ultra can use the same row for denser rendering/debug overlays without changing authoritative AUP storage.

Hardware Impact: Static estimate 6-18 microseconds per 1k active rows on i3/MX350 depending on cache residency. No profiler proof; build blocked by CPU gate.

## Decision 11 - Frost Swap-Pop Sweep And 64B Mutation Signal

Problem: Destroyed/despawned Vault hot rows can leave authoritative holes. Presentation systems also need to know when compaction relocates an entity index without querying the memory manager.

Solution: Added `VaultSovereigntyActiveEntityCount` and `VaultOrphanedPointerSweepJob`. The job scans a `GlobalQualityWeight`/`stride_aggressiveness` budget, treats zero EntityId or non-finite vectors as tombstones, performs O(1) swap-pop, and emits expanded 64-byte `MemoryAddressShiftSignal` records with old index, new index, entity id, frame, source hash, and compacted count.

Rejected Alternatives: Rejected O(n) render-side remaps because presentation must react to signals, not inspect Vault internals. Rejected rewriting SHINOBU_37 culling containers in this pass because that would alter another agent's spatial query contract.

Scalability potential: Low tier collapses toward a 64-row minimum Frost budget; Middle scans a larger fraction; High/Ultra approaches full active-row sweep. The curve is continuous, not a low-end boolean.

Hardware Impact: Static estimate 8-35 microseconds per Frost sweep window on i3/MX350 for common active counts; one queue enqueue per moved entity.

## Decision 12 - Dispatcher Telemetry Heartbeat

Problem: The telemetry ABI existed, but generation-handle misses and memory job cost were not routed from the dispatcher, and the batch requires a 300-frame black box that can survive post-mortem analysis.

Solution: Added `IDataVault.GenerationHandleMissCount`, incremented refresh misses inside `GlobalDataVault.ResolveBuffer`, and routed dispatcher POST_SIMULATION heartbeats into `VaultSovereigntyTelemetry.TryRecord`. Frost memory maintenance updates the last measured memory-job microseconds and flags; the post-sim heartbeat writes those values every frame. Fatal submarine state still dumps `Docs/AgentLogs/Dump_SHINOBU_100.bin`.

Rejected Alternatives: Rejected writing fake profiler numbers. Rejected only recording from subsystem runtimes because generation misses belong to the Vault/dispatcher boundary.

Scalability potential: Low/Middle/High/Ultra all write the same 64-byte row. Stride values make thermal memory shedding visible without altering authoritative bytes.

Hardware Impact: Static estimate under 2 microseconds per heartbeat. Dump path is cold IO only.

## Decision 13 - Native Scratch CSV Memory Profile

Problem: Designers need to tune Vault budgets and stride curves in Play Mode, but managed full-file reads and `string.Split` would violate the Zero-GC hot-path policy.

Solution: Reworked `VaultLegacyBinaryArchaeology.TryApplyMemoryOverridesCsv` to request `VaultMemoryProfileCsvScratch` from the DataVault, stream the file into native byte spans, hash lower-case ASCII keys with FNV-1a, and mutate `VaultMemoryLayoutConfig` fields including `StrideAggressiveness`. `SystemDispatcher` polls `memory_overrides.csv` on the Frost cadence in editor/development builds.

Rejected Alternatives: Rejected `File.ReadAllBytes`, regex, `string.Split`, and managed `List<>` staging. Rejected OSHINO-only regeneration because the task explicitly asks for live human tuning.

Scalability potential: Low can raise `stride_aggressiveness` to shrink maintenance budgets aggressively; Middle/High can use moderate values; Ultra can hold it low and scan more rows while preserving the same data route.

Hardware Impact: Runtime hot path impact is 0 microseconds. Frost/editor polling is bounded by a 4096-byte Vault scratch buffer and only parses on changed file ticks.

## Decision 14 - Deterministic Editor Visualization

Problem: Memory compaction is invisible unless a developer can see contiguous rows and recent swap-pop moves without attaching a profiler or mutating simulation state.

Solution: Added editor-only `VaultMemoryGizmoVisualizer`. It reads Vault AUP/hot buffers, reconstructs runtime positions from sector/local coordinates minus floating origin, draws active rows green, and flashes rows yellow when a matching `MemoryAddressShiftSignal` snapshot reports a swap-pop relocation. `VaultXRayWindow` also moved from managed `List<>` snapshots to a fixed 256-row array.

Rejected Alternatives: Rejected debug GameObject probes because they create scene state and managed churn. Rejected console logging because it is not spatial and can allocate.

Scalability potential: Low-tier runtime pays nothing because this is `#if UNITY_EDITOR`; High/Ultra developers can raise draw counts for denser inspection without touching runtime systems.

Hardware Impact: Player runtime impact is 0 microseconds. Editor gizmo cost is proportional to drawn rows, capped by the serialized `maxDrawn`.

## Decision 15 - Cached Vault Dependency Boundary

Problem: A DataVault migration that still resolves services from `GlobalRegistry` inside Tick/FixedTick would trade native-memory purity for service-locator stalls and hot-path branch noise.

Solution: Kept migrated runtime files on cached `IDataVault` fields for hot access and verified the target Fauna, VehicleMotor, SubmarineDynamics, and AcousticEcho files have no `GlobalRegistry.DataVault/Get/TryGet` calls. Cold fallback now uses `GlobalDataVault.TryGetLatestCreated`, matching the Vault-owned dependency boundary.

Rejected Alternatives: Rejected passing `GlobalRegistry.DataVault` through every ref helper because it hides an unbounded service lookup behind memory access. Rejected broad service rewrites outside SHINOBU_100 files.

Scalability potential: Low tier avoids repeated dependency lookup overhead; Middle/High/Ultra gain the same stable memory route and can layer denser telemetry without changing service ownership.

Hardware Impact: Static estimate 1-2 microseconds avoided per dense hot ref-access burst. No profiler proof due CPU-gated build.

## Decision 16 - Core.Memory Compile Wall And BufferID Collision Fix

Problem: The Frost sweep initially wrote `MemoryAddressShiftSignal` directly through `SignalBus` from `Hecton8.Core.Memory`. `SignalBus` and `MemoryAddressShiftSignal` are compiled in `Hecton8.Core`, while `Hecton8.Core` already references `Hecton8.Core.Memory`; adding a reverse reference would create a compile-wall violation. The same pass also placed late SHINOBU BufferIDs on 560-565, colliding with existing WristHud IDs.

Solution: Replaced the direct SignalBus writer with a 64-byte `VaultMemoryAddressShiftRecord` plus a Vault-owned count buffer. `VaultOrphanedPointerSweepJob` writes records only; `SystemDispatcher` maps those records into `MemoryAddressShiftSignal` and publishes from the Core boundary where the dependency is legal. Moved `VaultXRayWindow` to the Editor asmdef and `VaultMemoryGizmoVisualizer` to Core diagnostics so editor/debug code can legally use Core-only types. Relocated late SHINOBU BufferIDs to 636-641 and changed `VaultBufferContract.OwnsBufferId` from range math to an explicit owner switch.

Rejected Alternatives: Rejected adding `Hecton8.Core` as an asmdef reference to `Hecton8.Core.Memory`; that would create a circular architectural dependency. Rejected leaving duplicate BufferID values because it corrupts one-fact ownership. Rejected moving WristHud IDs because that is another domain's authority.

Scalability potential: Low tier still writes a bounded record buffer sized by the same continuous Frost sweep budget; Middle/High/Ultra publish denser relocation records without changing the data route. Editor visuals remain zero runtime cost.

Hardware Impact: Removes a compile-wall risk rather than a direct frame-time cost. The per-move path is one 64-byte Vault record write and one main-thread signal publish; static cost remains below the previous queue enqueue class, with deterministic ownership and no reverse assembly edge.

## Decision 17 - SHINOBU_37 Physics Culling Vault Closure

Problem: Static audit still found three persistent owner-local native containers in `GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs`: `NativeParallelMultiHashMap<int,int>` for spatial culling, `NativeQueue<int>` for changed-state indices, and `NativeQueue<PhysicsCullingTargetWakeRequestSignal>` for targeted wake requests. Keeping them as session allocations violated the GlobalDataVault sovereignty target and left rollback-adjacent culling state outside Vault handle generation.

Solution: Replaced the multi-hash map with Vault SoA buffers: `ShinobuPhysicsCullingSpatialBucketHeads` (4096 bucket heads), `ShinobuPhysicsCullingSpatialNext` (per-body chain next), and `ShinobuPhysicsCullingSpatialCellHashes` (exact full cell hash, so bucket collisions do not become false spatial hits). Replaced changed-index queue with `ShinobuPhysicsCullingChangedIndices` plus a 64-byte `PhysicsCullingCounter64` at `ShinobuPhysicsCullingChangedCount`; Burst writers use `Interlocked.Increment` through a `NativeArray<PhysicsCullingCounter64>` pointer. Replaced targeted wake queue with the existing `ShinobuPhysicsCullingWakeRequestMirror` plus `ShinobuPhysicsCullingWakeRequestCount`. The local `VaultBufferBinding<T>` now caches `IDataVault` and uses `GlobalDataVault.TryGetLatestCreated` as the cold fallback, removing hot `GlobalRegistry.DataVault` service lookup from physics culling buffer access.

Rejected Alternatives: Rejected keeping the queues under `NativeMemorySentinel` because that preserves owner-local persistent native allocations. Rejected `SignalBus` for changed indices because the culling jobs need a parallel write target and dispatcher drains indexed body mutations, not fire-and-forget event semantics. Rejected a bucket-only hash because it would wake/sleep bodies from unrelated cells on collisions. Rejected reusing 70609-70614 after source scan showed those IDs are reserved by `ShinobuApexBrainVault` constants; the physics extension moved to 70630-70635.

Scalability potential: Low devices keep the same fixed SoA memory and benefit from continuous quality radius shrinkage via `HomeostasisBrain.GlobalQualityWeight`; Middle uses moderate radii and the same O(N + C) candidate path; High/Ultra expand activation radius smoothly and spend the saved CPU on denser visual systems. The route has no binary tier branch for the culling radius.

Hardware Impact: Removes three owner-local persistent native allocations from the physics culling partial. Rebuild remains O(N) over active bodies and query is O(C) over nine neighbor buckets plus exact hash checks. Static i3/MX350 gain is reduced allocator fragmentation and queue container overhead; frame-time gain is not claimed without profiler proof. Counter false sharing is addressed by 64-byte explicit layout.

## Decision 18 - Boot Prewarm And Test Contract Repair

Problem: SHINOBU_100 maintenance buffers were created lazily when Frost maintenance first ran, and `VaultSurgeryEditTests` still asserted the pre-expansion VaultBufferContract values. That created a boot/runtime ownership ambiguity and stale CI contract.

Solution: Added `VaultSovereigntyMaintenance.PrewarmBuffers(IDataVault, int)` and called it from `GameBootstrapper.PreallocateDataVaultPrimaryBuffers` using `VaultMemoryLayoutConfig.HotEntityCapacity` with a 1024 fallback. The prewarm path requests active count, address shift count/records, CSV scratch, telemetry ring, and AUP local32 from the DataVault before gameplay maintenance. Editor tests now assert the expanded BufferID contract and the 8-byte alignment of `VaultAupSectorLocal32`, `VaultSovereigntyTelemetryEntry`, and `VaultMemoryAddressShiftRecord`.

Rejected Alternatives: Rejected relying on first Frost tick allocation because it violates the boot-phase DataVault request rule. Rejected broad bootstrap refactor because the existing preallocation method is the narrow owner route. Rejected leaving tests stale because future agents would receive false-negative ABI failures unrelated to their changes.

Scalability potential: Low devices receive bounded default capacities and avoid a first-frame allocation spike; Middle/High/Ultra can increase `HotEntityCapacity` through layout config without new C# code.

Hardware Impact: Boot-only allocation route. Expected runtime gain is avoiding first-maintenance allocator stalls; exact microseconds are not claimed without Unity Profiler proof.

## Decision 19 - Core Memory Explicit ABI Closure

Problem: A post-polish ABI scan still found `LayoutKind.Sequential` on non-generic Core.Memory records and dispatcher black-box DTOs in files touched by the Vault route. Even when the current sequential order happened to produce aligned sizes, it left future field insertion able to silently alter rollback/black-box byte offsets.

Solution: Converted non-generic Core.Memory records to explicit layouts with unchanged sizes: `BlockDescriptor` 40B, `H8AllocationRecord` 48B, `H8MemoryTelemetryEntry` 64B, `VaultRelocationRecord` 32B, `VaultBufferMeta` 48B, `VaultMemoryBlockSnapshot` 48B, `VaultArenaBlock` 32B, and `MemoryDefragTelemetryEntry` 128B. Also converted touched dispatcher DTOs `CriticalMemoryPressureEvent` 32B and `DispatcherBlackBoxEntry` 64B. Kept `VaultBufferHandle<T>` and `VaultBufferSlice<T>` sequential because generic explicit-layout value types are a CLR/IL2CPP compile-wall risk; both retain exact `UnsafeUtility.SizeOf` assertions and are handle/view metadata rather than hot authoritative DTO rows.

Rejected Alternatives: Rejected forcing explicit layout onto generic handle/view structs because a compile-risk in the core memory API would be worse than a documented handle-layout exception. Rejected expanding field sizes or changing enum underlying types because the goal is ABI freezing, not a public contract change. Rejected touching AI pathfinding `Pack=1` DTOs inside this decision because those are separate AI ownership lanes with their own binary contracts.

Scalability potential: Low tier gets deterministic, cache-aligned crash/relocation metadata without extra bytes; Middle/High/Ultra can add richer telemetry only by explicit versioned fields, not accidental sequential drift.

Hardware Impact: Runtime microseconds are not claimed. The direct value is ARM64-safe documented offsets for memory authority records and prevention of future unaligned insertions. Existing size assertions remain the compile/import guard.

## Decision 20 - Dispatcher Vault Dependency Cache Closure

Problem: `SystemDispatcher` still had two `GlobalRegistry.DataVault` fallbacks in its memory heartbeat/cache path. They were cold fallbacks, but their presence made static hot-path audits unable to prove that SHINOBU-touched Vault route uses cached dependencies only.

Solution: Replaced both direct fallbacks with cached `_dataVault` plus `GlobalDataVault.TryGetLatestCreated`. This keeps the route inside the Vault ownership boundary and matches the migrated VehicleMotor/Physics/Acoustic/Fauna cold fallback pattern.

Rejected Alternatives: Rejected retaining registry fallback with comments because the audit target is objective source proof, not intent. Rejected a broad dispatcher dependency-injection refactor because that would alter unrelated service bootstrap contracts.

Scalability potential: Low tier avoids service-locator branch noise during memory heartbeat; Middle/High/Ultra keep the same route while increasing telemetry density or Vault capacities.

Hardware Impact: Static estimate is small, under 1-2 microseconds in cache-miss recovery paths. The larger value is compile-wall and audit determinism; no profiler measurement was taken.

## Decision 21 - Physics Culling Scratch Vault Closure

Problem: After the native container migration, `GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs` still held two managed byte scratch arrays for CSV and legacy binary hydration plus one managed `int3[]` neighbor-cell scratch. They were cold paths, but they weakened the DataVault sovereignty proof and kept configuration IO outside the same Vault scratch model used by SHINOBU memory ingestion.

Solution: Added BufferIDs `ShinobuPhysicsCullingCsvScratch` (70636) and `ShinobuPhysicsCullingLegacyRadiiScratch` (70637). The CSV and legacy `.h8bin` readers now stream directly into Vault-backed `NativeArray<byte>` spans and parse via `ReadOnlySpan<byte>`. The 3x3 neighbor scratch array was deleted; spatial candidate gathering now computes the nine neighbor cell hashes directly in nested loops.

Rejected Alternatives: Rejected leaving the managed byte arrays with comments because source proof matters more than intent. Rejected storing the neighbor cells in another Vault buffer because a fixed nine-element loop is cheaper and has no persistent authority. Rejected replacing `Plane[6]` frustum scratch because Unity's `GeometryUtility.CalculateFrustumPlanes(Camera, Plane[])` requires a managed array and this scratch is pre-existing API interop, not rollback state.

Scalability potential: Low tier keeps the same fixed 4096B/64B scratch route with no managed array staging; Middle/High can reload tuning without heap churn; Ultra can raise culling richness through existing continuous quality radii without changing the scratch ABI.

Hardware Impact: Measured frame savings are not claimed. Static gain is removal of two managed scratch arrays and one candidate-cell array from the culling partial, plus direct native-span file hydration. Expected i3/MX350 value is lower GC-audit risk and less cold-path copy pressure; hot-path gain from deleting the 9-cell array is negligible but removes a persistent managed field.
