# Rationale_1306 - MEMORY_SOVEREIGN_CONSTRUCTION_EXORCIST

Date: 2026-05-25
Domain: Assets/Project/Scripts/Construction
Evidence: STATIC_SOURCE_PENDING

## Decision 000 - Phase 0 Evidence Path
Problem: Task requires eradication of persistent native aliases without false positives from local job-scoped native views.
Solution: Use a Roslyn syntax scan for FieldDeclarationSyntax inside Construction, then perform targeted code reads for lifecycle and consumers. This follows DOD ownership proof: one owner, one route, one proof artifact.
Rejected Alternatives: Regex-only scan is too noisy; direct edits before owner mapping can break consumers and race fences.
Scalability potential: Low tier gets safer relocation and zero hidden native leaks; Middle/High/Ultra can spend saved stability margin on richer base visuals without bloating gameplay truth.
Hardware Impact: Static scan cost is offline. Runtime target is no extra hot-path work on i3/MX350; expected gain cannot be measured until offenders are known.

## Decision 001 - Mandate Set
Problem: Construction memory touches native ownership, ARM64 DTO layout, telemetry, power/pipe logistics, flooding, registry access, zero-GC, and fake-first simulation.
Solution: Loaded eight mandates before coding: native memory/job protocol, ARM64 layout law, postmortem telemetry, logistics networks, fluid incursion, zero-GC, global registry DI, cinematic cheat protocol.
Rejected Alternatives: Reading unrelated AI/render/audio mandates would inflate context and increase off-domain drift.
Scalability potential: Low/Middle/High/Ultra decisions must remain continuous through GlobalQualityWeight, not binary quality switches.
Hardware Impact: No runtime impact; reduces design error risk before touching MX350-sensitive systems.

## Decision 002 - Domain Path Correction
Problem: Prompt names `Assets/Project/Scripts/Construction`, but that path does not exist. A scan there would prove nothing.
Solution: Use `Assets/_Project/Scripts/Construction`, the active first-party Construction folder under the project structure declared by `AGENTS.md`.
Rejected Alternatives: Creating the missing prompt path would be architectural sabotage. Reporting zero violations from a nonexistent path would be a false report.
Scalability potential: Correct path exposes real hot-path native aliases that can affect weak and high-end devices through relocation crashes.
Hardware Impact: Offline-only. Prevents a false clean report that would leave i3/MX350 crash risk untouched.

## Decision 003 - Roslyn Tool Reuse
Problem: Task 01 requires field-level AST separation of local native views from persistent native fields.
Solution: Reused existing `Tools/VaultNativeAliasRoslynAudit` net10 binary against the Construction folder and emitted `Docs/Reports/VAULT_EXORCISM_REPORT_1306.json`.
Rejected Alternatives: Regex-only report would misclassify locals and job parameters; launching a rebuild while CPU was above 50 percent violates AGENTS.
Scalability potential: Offline proof enables targeted migration instead of broad refactor churn.
Hardware Impact: 0 runtime us. Static result exposes 155 candidate persistent aliases.

## Decision 004 - BufferID Planning Without Code Mutation
Problem: Several offenders already have established BufferIDs; others have no exact enum lanes and cannot be migrated safely in Phase 0 without route-card review.
Solution: Map existing drone/catalog/socket/foundation lanes to current IDs and reserve planned construction memory-sovereignty range `12876000..12876070` for missing FluidPipe/HabitatGraph/LogisticsScheduler lanes.
Rejected Alternatives: Reusing unrelated existing IDs risks type identity break; adding enum values during Phase 0 would exceed archaeology scope.
Scalability potential: Low tier gets stable relocation; high/ultra can increase visual proxy richness after state ownership is safe.
Hardware Impact: 0 runtime us in Phase 0. Later migration removes stale-pointer crash class.

## Decision 005 - Telemetry DTO Naming Conflict
Problem: `ConstructionTelemetryEntry` already exists as a 64-byte validation telemetry DTO, but Phase 0 needs memory-sovereignty telemetry fields.
Solution: Phase 0 report records the conflict and plans either reviewed ABI migration of the existing DTO or a new `ConstructionMemoryTelemetryEntry` naming exception.
Rejected Alternatives: Adding a duplicate `ConstructionTelemetryEntry` would not compile; overloading existing fields without documentation would corrupt validation telemetry meaning.
Scalability potential: Low/Middle/High/Ultra telemetry remains fixed 300 frames and 64B entries; no gameplay-truth bloat.
Hardware Impact: Planned ring is 19.2 KB. Runtime write target remains sub-0.05 ms when implemented.

## Decision 006 - Build Verification Deferred By Protocol
Problem: Phase 0 produced docs/report files only, but AGENTS asks for verification. CPU sampled at 90 percent.
Solution: Do not launch dotnet build. Record static Roslyn parse result as current proof and mark compile as blocked by CPU protocol.
Rejected Alternatives: Violating the >50 percent CPU build ban to satisfy a checklist would create contention with other agents.
Scalability potential: No device-tier impact.
Hardware Impact: Avoided adding build load during active multi-agent work.

## Decision 007 - Repair Drone Acoustic Lane Vault Migration
Problem: `RepairDroneTorchAcousticEvents` owned two persistent `NativeQueue<RepairDroneTorchAcousticPayload>` fields. Those queues are physical native aliases and can survive across GlobalDataVault relocation with no generation validation.
Solution: Replace the queues with two 16-byte `VaultGenerationHandle<RepairDroneTorchAcousticPayload>` descriptors using `(BufferID)12876071` and `(BufferID)12876072`. Writers acquire a vault write lock and release it in `finally`; dispatch reads through `NativeArray<T>.ReadOnly` phase views and index cursors.
Rejected Alternatives: Keeping `NativeQueue` and registering it with `NativeMemorySentinel` only documents the fault; it does not make the alias relocatable. Moving `AudioClip` references into the DTO is invalid because the DTO must stay unmanaged.
Scalability potential: Low tier avoids a stale pointer crash during repair-drone weld bursts; Middle/High/Ultra can increase repair VFX/audio density without adding native ownership islands.
Hardware Impact: Removes two persistent native queue owners. Expected hot-path cost stays below 0.01 ms for 32 events because drain is linear over a fixed array; no per-frame managed allocation is introduced beyond the existing preallocated managed audio sidecar.

## Decision 008 - AUP Hash Precision Correction
Problem: `HabitatDeconstructionTransactionKernel.HashTransaction` hashed `OriginalAUP` with `(float)` casts, throwing away absolute-position precision before the deterministic state hash.
Solution: Hash the raw double bits through `math.aslong(double)` and fold low/high 32-bit halves into the same FNV-style state.
Rejected Alternatives: Keeping the float cast would make two far-sector AUPs collide after truncation. Converting through `ToRuntimeFloat3()` would bind transaction identity to the current floating origin, which is not valid for persistent transaction hashes.
Scalability potential: Low/Middle/High/Ultra all get stable teardown identity independent of sector distance and origin shifts.
Hardware Impact: Adds three extra 32-bit hash folds per transaction. Estimated cost is below 0.5 us for the low-tier frame cap of five teardown transactions and below 5 us for the ultra cap of fifty.

## Decision 009 - ARM64 DTO Offset Repair
Problem: `DeconstructionTransactionDTO` placed a `double3` at offset 8 after two `uint` fields, and `TeardownTelemetryEntry` placed a `double` at offset 48 after 4-byte fields. Size was 8-aligned, but field order violated the ARM64 largest-first mandate.
Solution: Move `DeconstructionTransactionDTO.OriginalAUP` to offset 0 and move `TeardownTelemetryEntry.AupLocalMagnitude` to offset 0. Keep struct sizes fixed at 32 and 64 bytes and extend `RuntimeLayoutValid()` offset guards.
Rejected Alternatives: Relying only on `StructLayout(Size=...)` would hide suboptimal memory layout. Reordering semantic names without updating offset guards would create false validation.
Scalability potential: Weak Quest-class ARM64 devices avoid avoidable unaligned access patterns; high-tier devices keep the same data density.
Hardware Impact: No extra runtime memory. Layout guard adds cold validation only; job payload size is unchanged.

## Decision 010 - Drone Fleet Snapshot BufferID Collision
Problem: `DroneFleetManager` used `(BufferID)70271/70272` for snapshot lanes, colliding with `SaveMerkleNodeBack/SaveMerkleLeafDescriptors` and Physiology metabolism constants. That breaks one fact -> one owner -> one route.
Solution: Move snapshot event lanes to `(BufferID)12870271` and `(BufferID)12870272`, which are free in source scan and fit the existing drone-local high-ID range near `12870276..12870278`. Update `Docs/ARCHITECTURE/DRONE_FLEET_PROTOCOL.md`.
Rejected Alternatives: Leaving the collision for a later batch risks cross-domain buffer resolution returning the wrong element type. Adding enum members was not required because the local code already uses casted BufferIDs for this lane.
Scalability potential: Low-tier saves crash risk under vault reuse; high/ultra can keep event density without corrupting save or physiology lanes.
Hardware Impact: Runtime cost is zero; this is identity routing only.

## Decision 011 - Repair Drone Partial Drain Compaction
Problem: The first vault migration used a read cursor but did not reclaim consumed front slots after `SystemDispatcher.TryConsumeLateFrameEventDispatch()` cut the drain short. A full pending lane could then reject new events even though part of the lane had already been consumed.
Solution: Add `CompactPendingEvents()` to copy live payloads down to index 0 under a vault write lock, then reset `_pendingEventReadIndex`. If compaction cannot acquire a valid buffer, fail closed by dropping queued payloads and releasing managed clip sidecars.
Rejected Alternatives: A circular ring would add more cursor state and risk reentrant listener bugs in this hotfix. Leaving holes until full drain would create false overflow on weak devices under tight late-frame budgets.
Scalability potential: Low tier can run with stricter event dispatch budgets without losing the entire repair-drone audio lane; high/ultra can dispatch more pulses without changing data ownership.
Hardware Impact: Worst case copy is 31 structs * 32 B = 992 B after a partial drain, only when dispatch budget cuts off. Estimated below 2 us on i3/MX350 and normally zero.

## Decision 012 - Drone Fleet Snapshot Event Lane Vault Migration
Problem: `HectonDroneFleetEvents` still owned physical pending and next-frame `NativeArray<HectonDroneFleetSnapshotPayload>` aliases, which breaks GlobalDataVault defragmentation safety and kept a legacy H8Memory fallback path alive for the event bridge.
Solution: Remove the event-lane physical arrays and vault-backed booleans. Keep only two `VaultGenerationHandle<HectonDroneFleetSnapshotPayload>` descriptors, resolve read-only views during dispatch, and write payloads under `TryAcquireWriteLock` with `ReleaseWriteLock` in `finally`.
Rejected Alternatives: Keeping `NativeArray` fields because they were already "vault backed" is invalid; the manager still held the physical alias. Reusing `NativeQueue` would make relocation safety worse.
Scalability potential: Low tier drops stale or overflowing fleet snapshot events instead of corrupting relocated memory; Middle/High/Ultra can raise snapshot event density without introducing another native owner.
Hardware Impact: Removes two physical event arrays from static ownership. Write cost is one bounded vault lock and one 48-byte copy per snapshot. Partial compaction worst case is 63 * 48 B = 3024 B, only when late-frame event budget cuts off.

## Decision 013 - Byte-Explicit Padding And Layout Guards
Problem: Some migrated DTOs were size-aligned but used trailing `ulong` or `uint` padding fields, which is ambiguous against the ARM64 mandate's byte-padding proof requirement.
Solution: Convert padding in `RefundCommandDTO`, `LootCacheDTO`, `TeardownTelemetryEntry`, `RefundProfileDTO`, and `HectonDroneFleetSnapshotPayload` to explicit byte fields and add offset guards for every padding byte.
Rejected Alternatives: Relying on `[StructLayout(Size=...)]` alone proves total size but not field-level discipline. Keeping wide padding fields after byte fields creates an avoidable audit objection.
Scalability potential: All quality tiers get deterministic DTO ABI. Low/Middle avoid ARM64 layout drift; High/Ultra keep the same compact buffer footprint.
Hardware Impact: Runtime buffer sizes unchanged: 32, 64, 64, 32, and 48 bytes respectively. Validation remains cold; hot path cost is 0 us.

## Decision 014 - Residual Release Blockers Are Real
Problem: The latest text scan still reports 176 Construction Native collection field-like matches and one legacy `Allocator.Persistent` fallback in the changed `DroneFleetManager.cs`. Pretending this is zero would be a false release report.
Solution: Mark the state as not release-clean and record exact residuals. The two event lanes were fixed, but large manager-owned NativeArray families in `DroneFleetManager`, `DroneFleetManager_Transactions`, `FluidPipeGraphRuntime`, `HabitatGraphManager`, logistics scratch/scheduler, socket/foundation data remain outside this partial remediation.
Rejected Alternatives: Hiding behind the old Roslyn Phase 0 report or claiming job `NativeQueue<T>.ParallelWriter` as a persistent manager alias would both be inaccurate. Removing the legacy fallback without migrating all static arrays would risk default buffers and broad behavioral breakage.
Scalability potential: Current fixes reduce crash surface in event lanes only. Full low-to-ultra scalability still requires systematic vault-handle migration for the remaining owners.
Hardware Impact: No measurable runtime savings can be claimed for the whole Construction domain yet. Event-lane improvement is expected sub-0.01 ms normal path and prevents stale-pointer failure under compaction.

## Decision 015 - Remove Drone Fleet H8Memory Fallback
Problem: `DroneFleetManager.ResolveDroneVaultBuffer<T>()` still allocated `H8Memory.Allocate(... Allocator.Persistent)` when GlobalDataVault was unavailable, directly violating the master prompt's no-private-native-owner rule.
Solution: Delete the fallback allocation path and let cold boot fail closed. `AllocateHeadlessNativeMemory()` validates all required vault-backed arrays before creating managed sidecars; if any required buffer is missing, it releases partial state and returns.
Rejected Alternatives: Keeping fallback as an emergency bridge preserves the stale-pointer class being eliminated. Throwing a managed exception would satisfy visibility but violate fail-closed runtime behavior.
Scalability potential: Low-tier devices avoid hidden persistent native memory islands during bootstrap pressure. Middle/High/Ultra retain the same handle route and can scale visual density only after the vault route is valid.
Hardware Impact: Removes one persistent allocation branch. Normal hot-path cost is unchanged; failure path saves the cost of allocating and later releasing fallback arrays.

## Decision 016 - Transaction Partial Handle-Only Conversion
Problem: `DroneFleetManager_Transactions.cs` held eight static `NativeArray<T>` transaction fields even after it had handles, so GlobalDataVault relocation could still invalidate cached physical aliases.
Solution: Remove the fields and vault-backed booleans. Store only eight `VaultGenerationHandle<T>` descriptors. Resolve transaction write views inside `TryAcquireDroneTransactionWriteBuffers()`, pass those views to jobs/mutation helpers, and release all write locks in `finally`. Read/debug accessors resolve read-only views on demand.
Rejected Alternatives: A wrapper struct containing NativeArrays would make the regex report look cleaner but still create field-level native aliases inside a struct. Keeping physical aliases because they were "vault backed" is not relocation-safe.
Scalability potential: Low tier can drop a transaction frame when locks cannot be acquired instead of corrupting relocated memory. Middle/High/Ultra can increase transaction count under the same continuous `GlobalQualityWeight` math without adding another owner.
Hardware Impact: Removes eight static NativeArray fields. The added lock/unlock work is eight bounded vault calls on transaction schedule/completion; estimated under 0.03 ms for i3/MX350 in the transaction frame, zero when no service transaction is scheduled.

## Decision 017 - Transaction DTO Byte Padding
Problem: Transaction DTOs were explicit-size but used `uint`, `int`, or `ulong` padding fields. That proves total size but not byte-level padding discipline demanded by the ARM64 mandate.
Solution: Convert transaction DTO padding to explicit private byte fields and add offset guards in `ValidateDroneTransactionLayouts()`. `DroneTaskDTO` padding was also converted to byte fields and its layout sentinel offsets were updated.
Rejected Alternatives: Relying on `[StructLayout(Size=...)]` hides sub-field drift. Leaving `ulong` padding after a 4-byte counter makes the byte map harder to audit and contradicts the prompt's requested proof shape.
Scalability potential: All tiers keep fixed DTO footprints: DroneTaskDTO 32B, transaction command/AUP/result/counter/telemetry 64B, integrity 32B.
Hardware Impact: Runtime memory footprint is unchanged. Validation is cold-path only; hot-path cost is 0 us.

## Decision 018 - Correct 1306 Blackbox Route
Problem: The transaction blackbox dump code still used a SHINOBU-specific dump route inherited from adjacent work, so Task 15 proof would land in the wrong forensic file.
Solution: Rename the constant and route `TryWriteDroneTransactionBlackBoxFile()` to `Docs/AgentLogs/Dump_1306_Construction.bin`.
Rejected Alternatives: Keeping the old dump name and documenting the mismatch is invalid because the CTO/integrator reads disk artifacts by agent ID.
Scalability potential: No quality-tier gameplay change. Low/Middle/High/Ultra all get one deterministic forensic path for transaction failure data.
Hardware Impact: 0 hot-path cost. Dump path is cold failure I/O only; normal frame cost remains unchanged.

## Decision 019 - Drone DTO Byte-Pad Closure
Problem: A second pass found wide `uint`/`ulong` pad fields still present in touched drone DTOs outside the transaction kernel: `DroneStateDTO`, `DroneChassisSpecDTO`, `PathWaypointDTO`, `DroneTransactionTelemetrySnapshot`, `DroneTransactionDebugTask`, `MockDroneSDFHeader`, and `DroneAStarPersistentState`.
Solution: Convert the padding to explicit private byte fields while preserving every public semantic offset and total struct size. Extend `DroneFleetLayoutSentinel` checks for the DTOs already covered by that sentinel. Remove direct writes to `DroneChassisSpecDTO._pad*` because private byte pads default to zero inside the value type.
Rejected Alternatives: Leaving wide pads because the total size was correct would fail the user's requested byte-level audit. Making the pads public would keep ABI noise and invite non-semantic writes.
Scalability potential: All tiers keep the same buffer footprints and same cache density; only layout proof improves.
Hardware Impact: 0 runtime us. This is ABI-preserving source hardening; validation remains cold only.

## Decision 020 - Flat Metadata BufferID Cap Correction
Problem: My prior local IDs `(BufferID)12870271..12870278`, `(BufferID)12873350..12873357`, and `(BufferID)12876071..12876072` were below `int.MaxValue` but above `GlobalDataVault` flat metadata capacity `100000`. `EnsureGenerationHandle` could create dictionary metadata, but `TryResolveHandle` and `TryAcquireWriteLock` read flat metadata and would fail for those handles.
Solution: Move every 1306-created high local lane to the free Construction range `72032..72053`. Logistics route scratch uses `72032..72038`; repair-drone acoustic lanes use `72039..72040`; fleet snapshot lanes use `72041..72042`; drone chassis/csv/AStar lanes use `72043..72045`; drone transaction lanes use `72046..72053`.
Rejected Alternatives: Raising `MaxGenerationHandleCapacity` would be a core allocator change outside this task and would inflate vault metadata by orders of magnitude. Returning to `70271/70272` reintroduces proven Save/Physiology collisions.
Scalability potential: Low/Middle/High/Ultra all get resolvable generation handles; no quality tier changes DTO layout or gameplay authority route.
Hardware Impact: 0 hot-path math cost. Fix removes a fail-closed no-op path where event and transaction write locks could never be acquired.

## Decision 021 - Logistics Route Scratch Vault Views
Problem: `LogisticsRouteScratchMemory` held seven static physical `NativeArray<T>` aliases for CSR route BFS: edge offsets, destinations, write cursor, storage flags, visited flags, queue, and result index.
Solution: Replace all seven static arrays with `VaultGenerationHandle<T>` descriptors and resolve write views only through `TryAcquireWriteBuffers()`. `BaseLogisticsNetwork.TryResolveNearestStorageEndpoint()` now acquires local views, builds CSR, runs `LogisticsPipeRoutingKernel.ExecuteRouteBfs()`, and releases every write lock in `finally`.
Rejected Alternatives: Keeping the arrays as scene scratch with `NativeMemorySentinel` keeps stale-pointer risk under vault relocation. Adding a managed list/queue fallback would satisfy neither Zero-GC nor deterministic routing.
Scalability potential: Low tier can fail closed when the vault or locks are unavailable; Middle/High/Ultra can raise topology sizes through the same capacities without private native owners.
Hardware Impact: Removes seven persistent native aliases. Normal route BFS still uses the same O(nodes+edges) linear work; added cost is seven bounded vault lock/unlock pairs, estimated below 0.02 ms on i3/MX350 for cold logistics path.

## Decision 022 - No Build Relaunch For APEX Addendum
Problem: The user explicitly ordered rare dotnet/build usage, and the current work is a static source/doc correction around BufferIDs and native ownership rather than a broad compile-fix pass.
Solution: Use prompt extraction, `rg`, targeted `Select-String`, brace/diff checks, and line-level source evidence. Do not launch `dotnet build`, Unity compile, or the Roslyn audit binary again in this addendum.
Rejected Alternatives: Re-running build/Roslyn after every patch would violate the user's explicit instruction and compete with other active agents. Claiming compile proof without running it would be a false report.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged by the verification choice; the corrected BufferIDs make every tier's vault handles resolvable.
Hardware Impact: 0 runtime cost. Avoided local workstation contention; no frame-time measurement is claimed.

## Decision 023 - Logistics Pipe Scheduler Scratch Vault Migration
Problem: `LogisticsPipeTransportScheduler` held ten static persistent scratch arrays for topological replay and cycle repair. Three of those arrays existed only to repair invalid cyclic pipe graphs with a synchronous suppression pass.
Solution: Keep only seven `VaultGenerationHandle<int>` descriptors for CSR/topological sort lanes `(BufferID)72054..72060`. `ScheduleNextOrder()` resolves write views through `GlobalDataVault`, schedules the Burst topological order job, and holds the writer locks until `CompletePendingSort()` observes job completion. Invalid cyclic graphs now fail closed to registration-order replay with a cadence-limited development warning instead of running a second suppression algorithm.
Rejected Alternatives: Keeping static `NativeArray<int>` fields preserves the stale-pointer relocation risk. Migrating the cycle-repair suppression arrays to Vault would keep unnecessary complexity for an invalid authoring case. Forcing same-frame completion would violate the job mandate.
Scalability potential: Low tier avoids seven to ten private persistent aliases and uses registration-order fallback when graph authoring is cyclic; Middle/High/Ultra keep the async topological sort when the DAG is valid without changing gameplay truth.
Hardware Impact: Removes ten static native aliases and four cold persistent allocation sites. The cycle fallback saves an exceptional O(V+E) synchronous repair pass; normal DAG path adds seven vault write-lock releases after job completion and keeps the same sort complexity.

## Decision 024 - Fluid Pipe Runtime Vault Migration
Problem: `FluidPipeGraphRuntime` owned 18 persistent `NativeArray<T>` fields, one `NativeParallelMultiHashMap<int,int>`, and one `NativeQueue<FluidPipeRuptureRecord>`. Those physical aliases were registered with `NativeMemorySentinel`, but still remained stale-pointer risks under GlobalDataVault relocation or defragmentation.
Solution: Replace every runtime pipe buffer with `VaultGenerationHandle<T>` descriptors on BufferIDs `72080..72100`. Resolve mutable views only inside schedule/write phases, pass those views into `FluidPipePressureSolveJob`, and keep the write locks until the dispatcher reports the scheduled job complete. Replace the hash-map adjacency with flat connection source/destination arrays and replace the rupture queue with a bounded rupture dispatch array plus a 3-int budget.
Rejected Alternatives: Migrating `NativeParallelMultiHashMap` as-is would keep a private native container outside the array-based vault route. Keeping `NativeQueue` would preserve unmanaged queue ownership and relocation risk. Releasing solve locks immediately after `Schedule()` would expose raw job views to relocation before completion. Forcing same-frame completion would violate the job mandate and hide a synchronization cost in the hot path.
Scalability potential: Low tier can fail closed when vault buffers or locks are unavailable and keep deterministic registration/pipe state instead of corrupting memory. Middle/High/Ultra can raise node, connection, telemetry, and visual-flow capacities through the same handle route without changing DTO layout or gameplay truth ownership.
Hardware Impact: Removes 20 private native runtime owners from the fluid pipe manager. Flat edge traversal is sequential and should be more cache-friendly than multi-hash-map iteration for typical pipe graphs. No profiler timing was collected; conservative expected gain is risk removal plus sub-0.05 ms improvement on i3/MX350 for about 1k directed edges, not a measured claim.

## Decision 025 - Residual Owner Blind Rewrite Rejected
Problem: After the FluidPipe and logistics migrations, the remaining persistent native owners are concentrated in `DroneFleetManager` and `HabitatGraphManager`. Static count is 822 drone identifier references across the manager and transaction partial, and 523 habitat identifier references in the graph manager. A declaration-only rewrite would touch live simulation, render upload, flood propagation, pathfinding, blackbox, and transaction paths.
Solution: Stop before a blind mechanical rewrite and record the exact residual front. The correct next step is a scoped owner-by-owner conversion: service-command lanes, drone double-buffer render state, drone AStar scratch, habitat flood CSR arrays, then habitat room connection map replacement. Each sub-slice must resolve phase-local vault views at the caller and pass them through jobs/mutators explicitly.
Rejected Alternatives: Wrapping `NativeArray<T>` fields inside another struct would hide the regex hit while keeping the same stale physical aliases. Replacing all identifiers with a property that resolves `GlobalRegistry.DataVault` would create hot global polling and impure reads. Removing the fields without converting 1300+ call sites would break runtime behavior.
Scalability potential: Low/Middle/High/Ultra still benefit from the completed event, logistics, transaction, and fluid migrations. Full scalability remains blocked until the two residual owners are converted with real phase-local view routing.
Hardware Impact: 0 runtime gain from this triage. It prevents a high-risk patch that would likely introduce undefined simulation state or hidden sync. No timing claim is made.

## Decision 026 - Drone Service Command Handle-Only Slice
Problem: `DroneFleetManager` still held cached static physical aliases for `DroneServiceCommand` and `DroneServiceCommandCursor`. The same file also retained a dead fallback bridge through `NativeMemorySentinel`/`H8Memory`, so a failed or stale GlobalDataVault route could still leave private native ownership in the drone manager.
Solution: Remove the two service-command `NativeArray<T>` fields and their vault-backed booleans. Keep only `VaultGenerationHandle<T>` descriptors, acquire local write views in `ScheduleHeadlessSimulation()`, pass those views into `DroneCognitionJob`, drain through local views, and release both write locks after `DrainDroneServiceCommandQueue()` or before reset/native release. `CompleteHeadlessSimulationAndApply()` now releases the locks even on the absent-state early return. Partial acquire failure releases by explicit acquisition booleans instead of relying on `NativeArray.IsCreated`.
Rejected Alternatives: A property that resolves the vault on every read would create hot `GlobalRegistry.DataVault` polling and impure read accessors. Keeping fallback `H8Memory.Allocate` would preserve the private native-owner class being removed. Releasing the write locks immediately after scheduling would allow GlobalDataVault relocation while a job still owns raw `NativeArray` views.
Scalability potential: Low tier can fail closed by disabling the service queue for a frame when locks cannot be acquired; Middle/High/Ultra keep the same command capacity and can spend saved stability budget on richer repair/drone visuals without changing command DTO layout or authority route.
Hardware Impact: Removes two cached static native aliases and the drone fallback sentinel bridge. Added work is two vault write-lock acquisitions at schedule and two releases after completion; estimated below 0.01 ms on i3/MX350 for the service-command frame. No profiler timing was collected.

## Decision 027 - Drone Task Selection Scratch Handle-Only Slice
Problem: `DroneFleetManager` still cached `s_TaskClaimCounts` and `s_DroneTaskPriorityHeap` as static physical `NativeArray<T>` fields for main-thread task selection. These are scratch lanes, not durable truth, and they should not survive as manager-owned aliases across Vault relocation.
Solution: Remove both physical fields and vault-backed booleans. Keep only `VaultGenerationHandle<int>` and `VaultGenerationHandle<DroneAssignmentTaskDTO>`. `TryAssignFleetTask()` now acquires local write views for claim counts and priority heap, passes those views through helper call chains, and releases both locks before publishing the fleet snapshot. The helpers resolve existing handles first and only call `EnsureGenerationHandle` when missing or undersized.
Rejected Alternatives: Reusing the existing cached fields would keep stale-pointer risk. Calling `EnsureGenerationHandle` on every task request would turn a task selection path into repeated cold-registration work. Publishing while locks are held was rejected because listeners or snapshot side effects should not run inside a scratch-buffer write window.
Scalability potential: Low tier can fail closed by denying a task assignment if the Vault is compacting or the scratch locks are unavailable. Middle/High/Ultra keep the same bounded scan and heap capacity; no gameplay authority or DTO layout changes.
Hardware Impact: Removes two more cached native aliases from `DroneFleetManager`. Runtime cost changes from direct cached-array access to two write-lock pairs around main-thread task selection; expected below 0.01 ms on i3/MX350 for the scan window. No profiler timing was collected.

## Decision 028 - Drone Chassis Specs And CSV Scratch Handle-Only Slice
Problem: `DroneFleetManager` still cached `s_DroneChassisSpecs` and editor-only `s_DroneSpecsCsvScratch` as static physical `NativeArray<T>` fields. Chassis specs are read during launch decisions, and CSV scratch was a cold editor bridge, but both were still manager-owned aliases outside the Vault descriptor model.
Solution: Remove both physical fields and vault-backed booleans. Keep only `VaultGenerationHandle<DroneChassisSpecDTO>` and `VaultGenerationHandle<byte>`. Chassis clear/commit paths acquire local write views and release in `finally`; chassis reads use `TryReadOnlyHandle` into a local read-only view. CSV import acquires the scratch buffer for the import window and releases it in `finally`.
Rejected Alternatives: Keeping chassis specs as a cached physical read buffer would preserve stale-pointer risk. Converting the editor CSV scratch to a managed byte array would reduce native residuals but violate the DataVault route already assigned to BufferID `72044`. Resolving `GlobalRegistry.DataVault` inside every chassis read was rejected as hot registry polling, so the manager now caches the vault service through the existing registry cache/hot-swap path.
Scalability potential: Low tier falls back to deterministic default chassis specs if the Vault is compacting or the chassis view is unavailable; Middle/High/Ultra keep the same eight-row profile capacity and continuous tuning math. This slice changes ownership only, not drone launch authority or quality-tier solver budgets.
Hardware Impact: Removes two cached native aliases from `DroneFleetManager`. Chassis spec reads add one read-only handle resolution for launch-spec lookup; estimated below 5 us on i3/MX350 because capacity is eight rows. CSV scratch cost is editor/cold only. No profiler timing was collected.

## Decision 029 - Drone Headless Scratch And Tuning Handle-Only Slice
Problem: `DroneFleetManager` still cached `s_DroneTuningConstants`, `s_HeadlessTaskClaimOwners`, and `s_FleetTelemetryAccumulator` as static physical `NativeArray<T>` fields. The task-claim and telemetry lanes are written by scheduled drone jobs, so a declaration-only removal would either leak a write view into worker lifetime or force unsafe same-frame completion.
Solution: Remove the three physical fields and their vault-backed booleans. Tuning reads use `TryReadOnlyHandle`; tuning writes acquire one write lock and release in `finally`. Headless task-claim and telemetry buffers are acquired as local write views before scheduling, passed into the docking abort path and Burst jobs, then released only after `DispatcherJobSwap` completion, reset completion, or native release.
Rejected Alternatives: Keeping the fields because they were already Vault-backed preserves stale physical aliases. Acquiring/releasing task-claim and telemetry locks around only the pre-schedule clear would leave jobs writing into unpinned Vault memory. Running the drone jobs synchronously to avoid lock lifetime would violate the job-system mandate and hide frame cost.
Scalability potential: Low tier can fail closed by skipping one drone simulation schedule if scratch write locks are unavailable during compaction. Middle/High/Ultra keep the same bounded task and telemetry capacities, with continuous `GlobalQualityWeight` still controlling cadence and solver budget.
Hardware Impact: Removes three cached native aliases. Added work is two write-lock acquisitions on schedule and two releases after job completion, plus one read-only tuning resolution when tuning is requested; estimated under 0.02 ms on i3/MX350. No profiler timing was collected.

## Decision 030 - Drone Black Box Handle-Only Ring
Problem: `s_DroneBlackBox` was still a cached physical `NativeArray<DroneFleetBlackBoxEntry>`. The black box is a forensic ring and must survive NaN/failure dump paths, but keeping the physical alias violates the DataVault relocation rule.
Solution: Remove the physical field and vault-backed boolean. `CaptureFleetBlackBoxFrame()` now acquires a local write view through `TryAcquireDroneBlackBox()`, writes the ring entry, and releases the lock in `finally`. The failure dump functions receive the local view as an argument and write the existing binary dump files before the lock is released.
Rejected Alternatives: Copying the 300-frame ring into a managed array before file I/O would allocate on the failure path and make the forensic path less truthful. Releasing the lock before dumping would expose a relocatable view during crash analysis. Keeping the cached field because the route is failure-only would leave a stale pointer in the exact system meant to explain failures.
Scalability potential: Low/Middle/High/Ultra keep the same 300-frame forensic capacity and dump routes. Quality settings do not alter black-box ABI or authority.
Hardware Impact: Removes one cached native alias. Normal frame adds one black-box write lock/release around a 300-entry ring owner update; estimated below 5 us on i3/MX350. Failure dump I/O remains cold and not claimed as frame-time work.

## Decision 031 - Drone Procedural Args Handle-Only Lane
Problem: `s_DroneProceduralArgs` cached a one-row indirect draw args `NativeArray<DroneProceduralIndirectArgsDTO>` even though the lane is already represented by `VaultGenerationHandle<DroneProceduralIndirectArgsDTO>`. The row is written by `BuildDroneProceduralArgsJob`, so removing the field without holding a write lock through job completion would expose relocation risk.
Solution: Remove the physical field and vault-backed boolean. `ScheduleHeadlessSimulation()` optionally acquires a local procedural args write view, schedules `BuildDroneProceduralArgsJob` only when the view is locked, and releases that lock with the existing headless scratch completion path. Render resolves a local current-phase Vault view for the one-row GPU upload.
Rejected Alternatives: Keeping the cached args field because it is one row preserves a stale pointer. Failing the entire simulation when procedural args lock is unavailable would sacrifice gameplay truth for a visual draw lane. Copying args into a managed staging object for render would add a managed allocation path.
Scalability potential: Low tier can simulate without refreshing procedural draw args for one frame if the Vault is compacting. Middle/High/Ultra keep the same indirect draw lane and can scale visual density through existing instance count and render distance knobs.
Hardware Impact: Removes one cached native alias. Optional write-lock cost is one lock/release pair on frames where the args job is scheduled; estimated below 3 us on i3/MX350. Render upload still copies one 16-byte DTO.

## Decision 032 - Drone Render Upload Handle-Only Slice
Problem: `s_DroneRenderInstances` and `s_DroneCullingStates` were still cached static physical `NativeArray<T>` fields used only as render/GPU upload staging lanes. Keeping them as manager fields leaves stale physical aliases even though the lanes already have Vault handles.
Solution: Remove both physical fields and vault-backed booleans. Cold boot now ensures the two Vault handles only. `RenderRealHeadlessFleet()` acquires local write views through `TryAcquireDroneRenderUploadBuffers()`, fills render/culling payloads through `PrepareDroneRenderInstances(renderInstances, cullingStates)`, uploads from those local views, and releases both write locks in `finally`.
Rejected Alternatives: Leaving the cached fields because they are presentation-only would still violate GlobalDataVault relocation safety. Releasing the locks before `GraphicsBufferUploadUtility.UploadNativeArray()` would allow the upload source view to become stale during the copy. Failing the simulation when render staging locks are unavailable was rejected because render staging is visual, not gameplay truth.
Scalability potential: Low tier can skip the render upload for a contended frame without changing drone gameplay state. Middle/High/Ultra retain the same instance capacity and can scale visual density through existing render distance and count settings.
Hardware Impact: Removes two cached native aliases from `DroneFleetManager`. Added work is two write-lock acquisitions and releases around render staging/upload; expected below 0.01 ms on i3/MX350. No profiler timing was collected.

## Decision 033 - Drone Spatial Hash Scratch Handle-Only Slice
Problem: The drone spatial hash lanes `s_DroneSpatialBucketHeads`, `s_DroneSpatialNextIndices`, and `s_DroneSpatialKeys` were cached static physical `NativeArray<int>` fields. They are scratch inputs for `DroneCognitionJob`, not durable truth, and must not exist as long-lived manager aliases.
Solution: Remove the three physical fields and their vault-backed booleans. Acquire all three write views in `TryAcquireHeadlessJobScratchBuffers()`, build the hash through local parameters, pass those views into `DroneCognitionJob`, and release the locks with the headless job completion path.
Rejected Alternatives: Rebuilding the hash into managed arrays would violate Zero-GC and Burst input requirements. Releasing the locks before scheduling cognition would leave the job reading relocation-unsafe views.
Scalability potential: Low tier can skip one headless simulation frame during Vault contention; Middle/High/Ultra keep the same spatial bucket capacity and continuous quality-driven steering budget.
Hardware Impact: Removes three cached native aliases. Adds three lock/release pairs per scheduled headless drone frame; expected below 0.01 ms on i3/MX350. No profiler timing was collected.

## Decision 034 - Drone Assignment Task Lane Handle-Only Slice
Problem: `s_DroneAssignmentTasks` was still a static physical `NativeArray<DroneAssignmentTaskDTO>` written by task-map construction and read by `DroneTaskAssignmentJob`. Building the map before acquiring a Vault lock left a stale-pointer risk window.
Solution: Remove the physical field and vault-backed boolean. Acquire the assignment-task lane with the headless scratch set before rebuilding the task map, pass the local view to task append/clear helpers and `DroneTaskAssignmentJob`, and release the lock after the headless job fence completes.
Rejected Alternatives: Locking only during task-map writes would release the array before the assignment job reads it. Keeping a cached field for convenience preserves the exact class of GlobalDataVault relocation fault being removed.
Scalability potential: Low tier can preserve prior managed task references or skip a scheduling frame if the Vault is contended. Middle/High/Ultra keep the same task capacity and assignment scoring.
Hardware Impact: Removes one cached native alias. Adds one lock/release pair in the scheduled headless frame; expected below 5 us on i3/MX350. No profiler timing was collected.

## Decision 035 - Drone AStar Macro Route Handle-Only Slice
Problem: `DroneFleetManager` still cached ten AStar/macro-route physical aliases: waypoints, waypoint states, AStar open heap, g-costs, came-from, node states, route nodes, route counts, telemetry, and persistent search states. Those buffers are scratch or diagnostic lanes and must not remain manager-owned physical pointers across Vault relocation.
Solution: Remove all ten physical fields and vault-backed booleans. Acquire every lane as part of `TryAcquireHeadlessJobScratchBuffers()`, pass local write views into `ScheduleDroneMacroAStar()` and `DroneCognitionJob`, hold all locks until the headless job fence completes, and resolve read/debug telemetry through local Vault views only.
Rejected Alternatives: Keeping the cached aliases because AStar is already bounded keeps stale-pointer risk. Releasing the AStar locks after scheduling would allow relocation while jobs still use raw views. Replacing AStar with a managed list or LINQ debug projection would violate Zero-GC.
Scalability potential: Low tier can skip one path-solve frame if Vault locks are unavailable; Middle/High/Ultra keep the same continuous quality-weight solve budget and can spend higher budgets on smoother macro routes without changing DTO layout.
Hardware Impact: Removes ten cached native aliases. Adds ten write-lock pairs during scheduled headless frames; expected below 0.03 ms on i3/MX350. No profiler timing was collected.

## Decision 036 - Habitat Latest Siege Static Alias Removal
Problem: `HabitatGraphManager` stored `_siegeTargets` and also published the same physical buffer through static `s_latestSiegeTargets`, creating an additional stale alias after owner teardown or future native relocation.
Solution: Remove `s_latestSiegeTargets`. The static getter now reads through `s_latestSiegeTargetOwner._siegeTargets`, clamps the published count to the current buffer length, and returns false with count zero when the owner/buffer is missing.
Rejected Alternatives: Keeping a second static NativeArray alias was rejected because it duplicates physical pointer state. Copying siege targets into managed static storage was rejected because it would allocate and break the native consumer route.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; this is ownership cleanup only. Siege target count remains capped and deterministic.
Hardware Impact: Removes one static native alias and adds one owner dereference plus one clamp in the read accessor; estimated below 1 us on i3/MX350. No profiler timing was collected.

## Decision 037 - Drone Mirror DTO Lanes Handle-Only Slice
Problem: `DroneFleetManager` still cached four mirror/DTO physical aliases: positions SoA, state bytes, `DroneStateDTO`, and `DroneTargetDTO`. These lanes are written by the headless job chain and cold control paths, so field-level aliases could become stale under Vault relocation.
Solution: Remove the four physical fields and their vault-backed booleans. Headless scheduling acquires the four write views with the scratch set and releases them at the same job fence. Cold paths use local write views: origin shift locks positions SoA; pending controls/services/launches and slot clearing receive explicit mirror views.
Rejected Alternatives: Resolving mutable views through a hot property on every mirror write was rejected as hidden GlobalRegistry/DataVault polling. Releasing mirror locks immediately after schedule was rejected because jobs still write those rows.
Scalability potential: Low tier can skip a frame or pending launch when mirror locks are unavailable; Middle/High/Ultra keep the same DTO layout and can scale visual/detail budget through existing continuous quality weights.
Hardware Impact: Removes four cached native aliases. Adds four write-lock pairs for scheduled headless frames and cold control windows; estimated below 0.015 ms on i3/MX350. No profiler timing was collected.

## Decision 038 - Headless Scratch Partial Acquire Cleanup
Problem: Superseded by Decision 040. This entry incorrectly described `acquiredCount - 1` as safe.
Solution: Invalidated. The corrected implementation releases `acquiredCount` and uses a 24-lane release map after core-buffer migration.
Rejected Alternatives: Keeping this as an accepted decision was rejected because it documents a lock leak.
Scalability potential: See Decision 040.
Hardware Impact: See Decision 040.

## Decision 039 - Drone Core State And Render Handle-Only Slice
Problem: `DroneFleetManager` still cached the authoritative drone state/front buffer, state back buffer, render matrix front buffer, and render matrix back buffer as static physical `NativeArray<T>` fields. These four buffers are the highest-risk aliases because the job chain reads/writes them across the dispatcher fence and completion swaps front/back ownership.
Solution: Remove the four physical fields and four vault-backed booleans. Keep only `VaultGenerationHandle<T>` descriptors. `TryAcquireHeadlessJobScratchBuffers()` now locks the four core buffers before all scratch lanes, passes local views into AStar, assignment, cognition, metabolism, matrix extraction, spatial hash, docking, controls, launch, clear, black-box, render prep, and debug paths, then releases all 24 acquired core/scratch/mirror locks after the job fence. Cold/manual paths use `TryAcquireDroneCoreWriteBuffers()` with `try/finally`; read-only public/debug paths use `TryReadDroneStates()` or a phase-local render matrix view.
Rejected Alternatives: Keeping the fields until Habitat migration was rejected because drone core state was already the last exact static native field hit in this manager. Opening views ad hoc in each helper was rejected because it would hide Vault resolution in hot helper calls and could create mixed-generation reads. Locking only the written back buffers was rejected because front buffers are read by jobs while the Vault may compact.
Scalability potential: Low tier can skip one scheduled drone simulation frame if any core/scratch lock is contended by compaction. Middle/High/Ultra keep the same continuous quality-weight steering/AStar/render budgets and no gameplay authority route changes.
Hardware Impact: Removes the final four cached static native aliases from `DroneFleetManager`. Adds four write-lock pairs to scheduled headless frames and cold mutation windows. Estimated added lock overhead is below 0.02 ms on i3/MX350; no profiler timing was collected.

## Decision 040 - Headless Scratch Cleanup Correction
Problem: Loop 24 rationale was wrong. The code increments `acquiredCount` after each successful lock, so at a failure site the variable already equals the number of lanes actually held. Releasing `acquiredCount - 1` leaks the last successfully acquired write lock.
Solution: Change every partial-failure cleanup in `TryAcquireHeadlessJobScratchBuffers()` to `ReleaseDroneHeadlessScratchWriteLocks(vault, acquiredCount)`. Extend the release map to 24 lanes after adding core state/render buffers.
Rejected Alternatives: Treating the leaked lock as harmless until reset was rejected because it can block later compaction or write acquisition. Reverting to a monolithic abstraction was rejected because explicit lane order is easier to audit during this migration.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged in successful frames. Contention failure now releases every held lane and fails closed without poisoning later frames.
Hardware Impact: 0 steady-state runtime cost. Fixes failure-path lock integrity; no profiler timing was collected.

## Decision 041 - Habitat Siege Target Snapshot Handle-Only Slice
Problem: After the drone purge, `HabitatGraphManager` still held `_siegeTargets` as an instance `NativeArray<HabitatSiegeTargetSnapshot>`. This was a real physical alias exposed through the static `TryGetLatestSiegeTargets()` read-only bridge for predator cognition, so GlobalDataVault relocation could leave Fauna copying from stale memory.
Solution: Add `HabitatSiegeTargetsBufferId=(BufferID)72122` and keep only `VaultGenerationHandle<HabitatSiegeTargetSnapshot> _siegeTargetsHandle`. `PublishSiegeTargetSnapshot()` and `ClearSiegeTargetSnapshot()` acquire local write views and release locks in `finally`. `TryGetLatestSiegeTargets()` resolves a read-only Vault view through the owner handle and fails closed if the handle is stale, missing, or too small.
Rejected Alternatives: Keeping `_siegeTargets` because the static alias had already been removed still left one manager-owned physical pointer. Copying siege targets into managed static storage would allocate and break the native Fauna consumer route. Changing the Fauna API was rejected because the existing read-only snapshot contract is correct; only the owner storage was wrong.
Scalability potential: Low tier can drop a predator siege snapshot frame when the Vault is compacting; Middle/High/Ultra keep the same capped 64-target feed with no authority or DTO layout change.
Hardware Impact: Removes one instance native alias. Publish/clear add one Vault write-lock pair around a 64-row copy/clear window; estimated below 5 us on i3/MX350. Read consumers add one read-only handle resolve. No profiler timing was collected.

## Decision 042 - Habitat Module Stress Handle-Only Slice
Problem: `HabitatGraphManager` still cached four per-module stress physical arrays for shader upload, acoustic deltas, impact spikes, and compromised hysteresis. These arrays are mutated every Habitat update and could become stale under GlobalDataVault relocation.
Solution: Add BufferIDs `72123..72126` and replace the four physical fields with `VaultGenerationHandle<T>` descriptors. `UpdateHabitatModuleStressMatrix()`, `ClearModuleStressState()`, `ResolveModuleStress01()`, `ConsumeModuleStressSignals()`, `InjectModuleStressSpike()`, `TryPublishBaseModuleCompromisedSignal()`, and `FlushModuleStressShader()` now work on phase-local Vault views and release write locks in `finally`.
Rejected Alternatives: Leaving the shader upload source as a cached `NativeArray<float>` was rejected because GPU staging convenience does not justify a stale native alias. Copying stress scalars to managed arrays was rejected as GC-positive and slower than the existing `GraphicsBufferUploadUtility.UploadNativeArray` path.
Scalability potential: Low tier can fail closed to zero module-stress shader publication for a contended frame. Middle/High/Ultra keep the same continuous quality-weight deformation model and can still spend visual budget on the existing stress buffer.
Hardware Impact: Removes four instance native aliases. Added cost is four Vault write locks during module-stress update/clear and one scalar lock during GPU upload; estimated below 0.02 ms on i3/MX350. No profiler timing was collected.

## Decision 043 - Habitat Room Flood Handle-Only Slice
Problem: Flood room state still used four cached physical arrays: water levels, volumes, propagation deltas, and room flags. The scheduled `HabitatFloodPropagationJob` read/wrote those arrays across a job fence, making relocation safety impossible with manager-owned aliases.
Solution: Add BufferIDs `72127..72130` and keep only room-state Vault handles. Room accessors resolve read-only views. `SyncFloodRoomStateSnapshot()` and `ClearFloodRoomStateSnapshot()` acquire local write views. `RunFloodPropagationJob()` acquires room write views before scheduling and records a held lock; `FinishFloodPropagationJob()` opens the held volume/delta views, applies deltas, and releases room locks in `finally`.
Rejected Alternatives: Resolving a read-only view for the job without a lock was rejected because the job can outlive the owner phase and the Vault may compact. Rebuilding flood deltas on the managed side was rejected as Zero-GC regression. Keeping flood state physical until graph CSR migration was rejected because this slice was separable.
Scalability potential: Low tier may skip one room-state sync or flood propagation schedule under Vault contention; Middle/High/Ultra retain the same bounded propagation budget and continuous graph flood node budget.
Hardware Impact: Removes four instance native aliases. Adds four room-state lock/release pairs on sync/clear and one lock set held across a scheduled flood job; estimated below 0.03 ms on i3/MX350 for 64 rooms. No profiler timing was collected.

## Decision 044 - Habitat Graph CSR Handle-Only Slice
Problem: The final Habitat release blockers were nine instance `NativeArray<T>` graph owner fields: nodes, CSR offsets, CSR destinations, edge resistance, CSR write cursor, anchor reachability, traversal visited, traversal queue, and edge flags. These lanes are used by rebuild, runtime rupture, flood propagation, deconstruction validation, siege target publication, and graph kernel export. Leaving them as physical aliases would keep the exact stale-pointer class the Vault migration is removing.
Solution: Add graph BufferIDs `72131..72139` and replace all nine physical fields with `VaultGenerationHandle<T>` descriptors. Rebuild and runtime topology publication acquire one `HabitatGraphWriteViews` window and pass local views through node build, CSR build, anchor BFS, component power, lockdown/edge flags, degradation sync, siege snapshot, and graph kernel export. Flood propagation acquires a narrow CSR/edge-flag job view and holds it across the scheduled job fence. Deconstruction CSR lanes are leased to the cold sync teardown job and released by `ConstructionManager.ReleaseDeconstructionCsrLanes()` in `finally`.
Rejected Alternatives: Keeping temporary cached aliases inside the manager was rejected because it merely hides the physical pointer under a different name. Resolving read-only CSR views for scheduled flood propagation was rejected because Vault compaction can invalidate a job-lifetime view. Rewriting deconstruction to copy CSR into managed arrays was rejected as GC-positive and slower than the existing bounded sync job.
Scalability potential: Low tier may skip one graph/flood/deconstruction window while Vault locks are contended; Middle tier keeps bounded CSR traversal; High/Ultra can spend saved cycles on visual siege/flood feedback without changing graph authority or DTO layout.
Hardware Impact: Removes nine more manager-owned native aliases and all Habitat graph `new NativeArray`/sentinel lifecycle. Added work is 4-9 Vault lock pairs around owner phases; estimated below 0.04 ms on i3/MX350 for 64 nodes/128 directed edges. No profiler timing was collected.
