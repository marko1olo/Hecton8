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

## Decision 045 - Root Construction Deconstruction Vault Slice
Problem: APEX re-audit proved the earlier folder-focused scan missed `Assets/_Project/Scripts/ConstructionManager.cs`, which is still in `Hecton8.Construction` and owns module deconstruction. It kept persistent DFS scratch, transaction, fallback-cost, refund, loot-cache, and black-box native aliases plus sentinel lifecycle.
Solution: Remove the ten physical native aliases and keep only `VaultGenerationHandle<T>` descriptors. Add local deconstruction scratch BufferIDs `72140..72144` and reuse existing SHINOBU_336 transaction/refund/loot/counter lanes. DFS now uses bounded `NativeArray<int>` stack and `NativeArray<byte>` visited lanes. Transaction, telemetry, black-box, and fallback-cost paths acquire local Vault write views and release by explicit acquisition count or `finally`.
Rejected Alternatives: Keeping root `ConstructionManager.cs` outside the migration scope was rejected because domain ownership is by namespace/system responsibility, not folder shape. Wrapping `NativeList`/`NativeParallelHashSet` inside another type was rejected because it preserves stale physical aliases. Using managed `List`/`HashSet` for DFS was rejected as GC-positive. Running dotnet/build for reassurance was rejected by user order and by the local CPU/build policy.
Scalability potential: Low tier can fail closed by rejecting one deconstruction request or skipping one black-box write when Vault locks are contended. Middle keeps bounded array DFS and single transaction. High/Ultra can spend budget on visual debris/refund feedback; DTO layout and deconstruction authority route remain unchanged.
Hardware Impact: Removes ten persistent native aliases and sentinel lifecycle from root construction deconstruction. Adds 3 DFS lock pairs and 5 transaction lock pairs only on player-triggered deconstruction, plus cold black-box/telemetry locks. Estimated below 0.05 ms on i3/MX350 for the cold path; no profiler timing was collected.

## Decision 046 - Construction AUP Local Delta Explicitness
Problem: `ConstructionManager.cs` used `AbsoluteUniversePosition.ToRuntimeFloat3()` in deconstruction validation and save-load respawn. The shared implementation already subtracts runtime origin in double, but the call site hid the proof and could resolve the origin separately for ray origin and target.
Solution: Add `TryResolveRuntimeFloat3AupDelta()` in `ConstructionManager`: it validates both AUPs, computes `double3 localDelta = position.ToAbsoluteDouble3() - originAup.ToAbsoluteDouble3()`, then casts only that local delta to `float3`. Deconstruction probe resolves one runtime origin per validation call; graph save-load resolves one origin per load loop.
Rejected Alternatives: Leaving `ToRuntimeFloat3()` was rejected because the APEX verification requires visible local-delta proof at the domain site. Casting absolute coordinates directly was rejected. Calling into internal `AUPMath` was rejected to avoid assembly-boundary risk and to keep the formula auditable in this file.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged except that invalid origin/AUP now fails closed instead of silently producing a zero runtime position during graph respawn.
Hardware Impact: Runtime cost is two double3 subtractions in deconstruction validation and one per graph-node load. This is cold/control-path work, estimated below 5 us for the validation call on i3/MX350. No profiler timing was collected.

## Decision 047 - Preview Job-Lifetime Vault Locks
Problem: `VRPipeBlueprintPreview` and `HectonBlueprintPreviewBatch` scheduled ghost-state jobs using Vault-resolved arrays, but the state/visual/indirect-args views were not explicitly held by write locks for the whole scheduled job and GPU upload window.
Solution: Acquire the preview state, visual, and indirect-args lanes through `TryAcquireWriteLock()` before scheduling. Store the lock vault/count on the component, read the same locked buffers during finalization/upload, and release in `finally` or teardown. Hecton batch telemetry writes now acquire a short telemetry write lock.
Rejected Alternatives: Resolving buffers again after the job was rejected because it can mix generations after compaction. Holding cached `NativeArray<T>` fields was rejected because it recreates the stale alias defect. Completing jobs synchronously was rejected because it hides frame cost.
Scalability potential: Low tier skips a preview refresh when locks are contended; Middle/High/Ultra keep the same ghost data and can spend visual budget on denser preview effects without changing gameplay truth.
Hardware Impact: Adds 3 write-lock pairs around preview job windows and 1 short telemetry lock in the batch path; estimated below 0.01 ms on i3/MX350. No profiler timing was collected.

## Decision 048 - Preview And Drone AUP Local Delta Proof
Problem: Preview and drone visual/docking paths still depended on hidden `ToRuntimeFloat3()` conversion, which obscured whether absolute AUP coordinates were cast to float before subtracting runtime origin.
Solution: Add explicit local-delta helpers in `VRPipeBlueprintPreview` and `DroneFleetManager`. Each helper validates object AUP and runtime origin, computes `double3 localDelta = position.ToAbsoluteDouble3() - originAup.ToAbsoluteDouble3()`, checks finiteness, and casts only the local delta to `float3`.
Rejected Alternatives: Trusting the shared helper was rejected because the verification requirement is call-site proof. Direct float casts of absolute AUP were rejected. Per-axis duplicated formulas at each call site were rejected because a single helper is easier to scan.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged except invalid AUP/origin now fails closed for preview/VFX instead of creating unstable runtime positions.
Hardware Impact: Adds one double3 subtraction in visual/docking conversion paths; estimated below 5 us per call on i3/MX350. No profiler timing was collected.

## Decision 049 - Sump Pump Residual Write Contract
Problem: `SumpPumpPipeGridRuntime` correctly held job buffers for the main solver, but cold tuning init and idle telemetry heartbeat still used mutable views without their own write windows, and the post-solver wall-time stamp had to remain inside the held solver lock window.
Solution: Keep `StampSolverWallTime()` before `UnlockJobBuffers()` inside `LateFrameTick()`. Lock frame-summary/cursor/telemetry lanes for heartbeat. Lock tuning during `InitializeTuningIfNeeded()`. Convert visual upload/debug paths to read-only `Read()` handles where they do not mutate data.
Rejected Alternatives: Treating these as harmless because they are cold/diagnostic was rejected because Vault relocation safety is a contract, not a frequency guess. Reusing the solver lock for idle heartbeat was rejected because no solver window exists in that path.
Scalability potential: Low tier may skip one heartbeat or tuning sanitation under Vault contention. Middle/High/Ultra keep the same fixed-pass drainage solver and visual flow upload.
Hardware Impact: Adds three short telemetry locks for heartbeat and one cold tuning lock; expected below 0.01 ms on i3/MX350 outside file I/O. No profiler timing was collected.

## Decision 050 - Docking Telemetry Dual-Lock Repair
Problem: `VehicleDockingModule` wrote the dock telemetry ring and cursor through resolved handles without write locks. Dumping on invalid position/AUP also sanitized the cursor through an unlocked view.
Solution: Replace `TryResolveDockTelemetry()` with `TryAcquireDockTelemetryWrite()`, acquiring the ring and cursor write locks with explicit `ringLocked`/`cursorLocked` cleanup. `RecordDockTelemetry()` and `DumpDockTelemetry()` operate on the locked views and release in `finally`; invalid-path dump uses `DumpDockTelemetryLocked()` to avoid nested acquisition.
Rejected Alternatives: Leaving diagnostics unlocked was rejected because the cursor is mutable state. Locking only the ring was rejected because the cursor is mutated on every write/dump. Copying the ring into managed storage for dump was rejected as cold GC churn and less accurate forensic data.
Scalability potential: Low tier can skip one telemetry entry if locks are contended; Middle/High/Ultra keep the same 300-frame forensic history and docking gameplay behavior.
Hardware Impact: Adds two write locks around docking telemetry writes and dumps; expected below 5 us on i3/MX350 for normal telemetry. File I/O remains cold and not counted as frame work.

## Decision 051 - Shinobu Socket Mock Seed Vault Locks
Problem: `ShinobuSocketConstructionData.GenerateMockBaseConstructionGrid()` used an `Interlocked` module fence but wrote modules, socket states, socket AUPs, counters, and CSR lanes through `TryResolveHandle()` without DataVault write locks. `InitializeVault()` also reset counters/tuning without locks.
Solution: Add `TryAcquireWriteLane()` and `ReleaseGenerateMockWriteLocks()`. Mock-grid generation now locks module/socket/AUP/counter/CSR lanes before writes and releases every partial acquisition in `finally`. Counter reset and tuning initialization in `InitializeVault()` use short write-lock windows.
Rejected Alternatives: Relying on the existing `Interlocked` fence was rejected because it does not pin Vault memory against compaction. Keeping CSR optional after core writes was rejected because a partial mock topology is harder to reason about than a fail-closed false return.
Scalability potential: Low tier can fail closed by skipping mock-grid regeneration under Vault contention. Middle/High/Ultra keep the same deterministic mock layout and socket CSR table.
Hardware Impact: Adds six write-lock pairs for cold mock-grid generation and two short init locks. This is cold/editor/bootstrap work, estimated below 0.03 ms on i3/MX350 when invoked. No profiler timing was collected.

## Decision 052 - Base Module Catalog Lease Gate
Problem: `BaseModuleCatalogRuntime` exposed scheduled catalog mutation APIs that returned a `JobHandle` and mutable `ModuleCatalogViews` without any release object. It also recorded telemetry and filled hydration bytes through resolved mutable Vault views with no write-lock window.
Solution: Add `ModuleCatalogWriteLease` and lease-based `ScheduleMockCatalog`/`ScheduleHydrateCatalog` overloads. The old overloads now fail closed because they cannot prove a release point. Catalog targets, telemetry state/ring, and sync hydration bytes are acquired through `TryAcquireWriteLock()` and released in `finally`. Async byte loading is disabled fail-closed because a background writer cannot safely hold or release a Vault lock through the old API shape.
Rejected Alternatives: Completing catalog jobs synchronously was rejected because it hides frame cost and changes caller scheduling. Leaving old overloads active was rejected because no caller could release the locks. Returning unlocked `ReadOnly` bytes for async hydration was rejected because Vault compaction can invalidate the view while the background writer is still running.
Scalability potential: Low tier can skip catalog generation/hydration while Vault lanes are contended instead of corrupting relocated memory. Middle/High/Ultra keep the same table/LUT catalog model and can increase module richness later without changing DTO ownership.
Hardware Impact: Adds six write-lock pairs around catalog generation/hydration jobs and two short locks for telemetry writes. Sync byte hydration adds one cold byte-lane lock. Estimated below 0.03 ms on i3/MX350 for cold catalog paths; no profiler timing was collected.

## Decision 053 - Foundation Cold Config Write Locks
Problem: `FoundationSnappingCalculatorData.InitializeVault()` and `TryApplyEditorTuning()` wrote telemetry cursor, tuning, and SDF config lanes with `TryResolveHandle()` instead of write locks. These are cold paths, but relocation safety cannot depend on frequency.
Solution: Add `TryAcquireWriteLane<T>()` and lock cursor/tuning/SDF config around every mutation. `TryApplyEditorTuning()` now locks the tuning lane before writing the editor-updated DTO.
Rejected Alternatives: Keeping raw resolves because the path is editor/cold was rejected; cold code can still race DataVault compaction or leave stale state during bootstrap. Copying the config into managed static-only state was rejected because the GPU/job consumers read the Vault DTO lanes.
Scalability potential: Low tier may fail closed during one foundation bootstrap/tuning update if a Vault compaction is active. Middle/High/Ultra keep continuous GlobalQualityWeight-driven ray/SDF budgets.
Hardware Impact: Adds three cold write-lock pairs during foundation initialization and one editor tuning lock. Normal GPU pylon frame cost is unchanged; no profiler timing was collected.

## Decision 054 - Bulkhead Job Lifetime Pins
Problem: Bulkhead and hatch jobs consumed Vault-resolved arrays across scheduled job windows, while profile/tuning/telemetry paths still had raw mutable resolve windows. A DataVault relocation or external writer could invalidate job pointers or corrupt forensic cursors.
Solution: Add explicit bulkhead job pin bits and hold `TryLockBuffer` pins from schedule through `TryFinalizeBulkheadJobsNoWait()`. Profile CSV, tuning, mock fluid, telemetry cursor/ring, and dump copy paths now use write locks or pins. External hatch fluid/structural inputs are optional pins and fail closed to mock/no structural feed if unavailable.
Rejected Alternatives: Completing the jobs immediately was rejected because it hides frame cost. Resolving the arrays again after schedule was rejected because it can mix generations. Keeping raw telemetry resolves because they are diagnostic was rejected because cursor corruption destroys crash evidence.
Scalability potential: Low tier can skip one bulkhead/hatch simulation window under Vault contention. Middle/High/Ultra keep the same bounded cinematic seal/flood model and can spend visual budget on shader feedback, not a heavier physics solver.
Hardware Impact: Adds bounded pin/unpin calls around bulkhead and hatch job windows plus short write locks for cold CSV/tuning/telemetry paths. Expected under 0.05 ms on i3/MX350 on affected frames; no profiler timing was collected.

## Decision 055 - Airlock Intent Bridge Write Ownership
Problem: `BulkheadContainmentIntentBus.TryWriteAirlockBulkheadIntent()` wrote Construction-owned intent ring/control lanes without Vault write locks, while Construction consumption now expects locked ownership.
Solution: Acquire intent ring and control through `TryAcquireWriteLock()` and release both in `finally`. Partial acquisition releases the ring before returning false.
Rejected Alternatives: Treating this as an Airlock-owned direct bridge was rejected because the ring route is Construction-owned and consumed by Bulkhead. Moving to managed event bus was rejected as hot-path GC and wrong authority route.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged except a contended frame drops one intent safely instead of corrupting the intent cursor.
Hardware Impact: Adds two short write-lock calls per airlock intent write. Estimated below 5 us on i3/MX350; no profiler timing was collected.

## Decision 056 - Habitat Validation Graph Full Pin Mask
Problem: `HabitatConstructionManager` built the validation graph before locking and only pinned six of ten buffers. Degree scratch, write scratch, connection, and socket lookup lanes were written without protection and could be relocated before or during the validation job window.
Solution: Ensure capacity first, lock all ten validation graph lanes, build the graph while pinned, schedule the job, and release only after completion/teardown. Cache invalidation clears socket lookup under the held pin or a short socket lookup write lock.
Rejected Alternatives: Locking only the six job-read buffers was rejected because graph construction mutates the other four lanes. Rebuilding through managed collections was rejected as GC-positive and slower. Removing the cache was rejected because candidate sockets are not inserted into the cache, so the existing cache can stay correct once the write window is fixed.
Scalability potential: Low tier may skip one placement validation when Vault pins are contended. Middle/High/Ultra keep the same bounded graph validation and can spend saved stability margin on preview fidelity.
Hardware Impact: Adds four more buffer pins and moves the lock window earlier. Estimated under 0.02 ms on i3/MX350 for the validation frame; no profiler timing was collected.

## Decision 057 - Modular Validator Write Locks
Problem: `ModularBaseConstructionValidator` wrote tuning, bounds overrides, emergency mock bounds, and telemetry ring lanes through ensure/resolve helpers without write locks. The dump route also pointed to a non-1306 forensic file.
Solution: Add `TryAcquireValidationWriteBuffer<T>()` and route all mutable tuning/bounds/telemetry writes through `TryAcquireWriteLock()` with `finally` release. Read helpers now use `TryReadHandle()`. The validation dump path is `Dump_1306_ConstructionValidation.bin`.
Rejected Alternatives: Leaving writes unlocked because some callers are editor/cold was rejected; editor and bootstrap still share the same Vault. Copying bounds into managed arrays was rejected as GC-positive and unnecessary.
Scalability potential: Low tier can skip one tuning/bounds/telemetry update under Vault contention. Middle/High/Ultra keep the same table-driven validator and mock bounds model.
Hardware Impact: Adds one write lock for tuning, one for telemetry writes, and one for bounds import/seed. Cold/editor cost is expected below 0.02 ms on i3/MX350; no profiler timing was collected.

## Decision 058 - Runtime Forensic Route Cleanup And Dead Queue Removal
Problem: Several Construction black-box paths still wrote to `Dump_SHINOBU_*`, and `DroneFleetNavigationKernel` retained an unused `NativeQueue<DroneAssignmentTaskDTO>` mock producer job. Both created false ownership/reporting artifacts in 1306 verification.
Solution: Rename runtime dump route values to `Dump_1306_Construction*` without changing binary formats. Update editor scanner strings to match. Remove the unused queue job; the active mock task path is the existing array-backed `GenerateMockDroneTasksJob`.
Rejected Alternatives: Keeping compatibility SHINOBU dump names was rejected because the current agent report must be traceable by ID. Leaving an unused queue job was rejected because it preserves a dead native queue contract in runtime code.
Scalability potential: No gameplay behavior change. Low/Middle/High/Ultra keep identical task generation through the array-backed lane and get cleaner forensic routing.
Hardware Impact: Removes one dead Burst job type and all `NativeQueue<DroneAssignmentTaskDTO>` source hits. Runtime cost is unchanged; verification noise is reduced. No profiler timing was collected.

## Decision 059 - Explicit AUP Local Delta In Remaining Habitat Paths
Problem: The previous AUP pass removed `ToRuntimeFloat3()` but left two weaker patterns: `BaseDegradationSystem` cached rupture absolute coordinates as `Vector3`, and habitat snap/socket conversion depended on helper routing instead of showing the required double-local formula at the call site.
Solution: Store rupture node coordinates only as `double3 AbsoluteUniversePositionDouble`. Remove the absolute `Vector3` field and double3-to-Vector3 converters. In `HabitatConstructionManager` and `HabitatGraphManager`, resolve the runtime origin AUP once, compute `double3 localDelta = objectAup - originAup`, validate finite/range, then cast only the local delta to `float3`/`Vector3`.
Rejected Alternatives: Keeping a `Vector3` rupture cache was rejected because it stores absolute AUP in float precision. Trusting helper conversions was rejected because the verifier requires call-site proof. Running dotnet/build for reassurance was rejected by current user CPU/build policy.
Scalability potential: Low/Middle/High/Ultra gameplay truth is unchanged. Invalid AUP/origin now fails closed earlier; visual fidelity can still scale independently through existing quality paths.
Hardware Impact: Removes one float absolute-coordinate cache and replaces hidden conversions with one double3 subtraction per affected cold/control path. Estimated below 5 us on i3/MX350 for these paths. No profiler timing was collected.

## Decision 060 - Base Degradation Managed Growth Caps
Problem: `BaseDegradationSystem` used managed dictionaries/lists in runtime state caches. Constructors had capacities, but new insertions could still grow managed backing storage under rupture, pressure, parasite, integrity, or spore hazard churn.
Solution: Add explicit capacity constants for each cache and guard new-key writes with `CanWriteDictionarySlot()`. Guard stale-node list insertion. Route module rupture writes through `TryWriteModuleRuptureState()`. If a cache is saturated, the system fails closed by skipping the new visual/rupture side effect instead of allocating.
Rejected Alternatives: Replacing the whole system with new native containers was rejected in this pass because it would expand scope and risk new ownership defects. Allowing managed growth was rejected because Zero-GC runtime is the mandate.
Scalability potential: Low tier drops excess degradation side effects under pathological churn. Middle/High/Ultra keep the same table/visual model and can raise capacities later through a documented owner change if profiling proves need.
Hardware Impact: Prevents dictionary/list backing-array growth in the degradation hot path. Added cost is one `ContainsKey`/count check on new inserts; estimated below 5 us per new event on i3/MX350. No profiler timing was collected.

## Decision 061 - Construction DataVault Cache Ownership Cleanup
Problem: `DroneFleetManager_Transactions.cs` still contained direct DataVault registry reads in helper paths, and `HabitatGraphManager` could poll `GlobalRegistry.DataVault` internally instead of receiving the cached owner dependency from `ConstructionManager`.
Solution: Transaction helpers now prefer `s_CachedDataVault` and only use the existing cold resolver for allocation/bootstrap. `HabitatGraphManager` receives `IDataVault` by constructor injection, exposes `SetDataVault()`, and `ConstructionManager` refreshes it during storage setup and DataVault hot-swap.
Rejected Alternatives: Leaving graph-manager registry polling was rejected because owner-phase DI belongs to `ConstructionManager`. Registering the graph manager itself as a hot-swap listener was rejected because it is not a `MonoBehaviour` runtime owner and is already owned by ConstructionManager.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged except contended/missing Vault routes now fail through the owner cache rather than hidden registry polling.
Hardware Impact: Removes direct registry reads from transaction helpers and graph manager. Runtime cost is one cached reference read; no profiler timing was collected.

## Decision 062 - 1306 Forensic Route Closure
Problem: Several Construction black-box paths still wrote to legacy `Dump_*` files that did not include the 1306 owner tag. This broke traceability during post-mortem analysis and made audit evidence ambiguous.
Solution: Route the remaining drone, docking, foundation, and habitat integrity/module-stress dumps to `Docs/AgentLogs/Dump_1306_Construction*` paths without changing binary formats.
Rejected Alternatives: Keeping old agent-compatible file names was rejected because forensic ownership must be exact. Writing both old and new paths was rejected as extra file I/O and duplicate crash artifacts.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; only crash/post-mortem routing changes.
Hardware Impact: No frame cost. Crash dump path cost is unchanged file I/O with clearer ownership.

## Decision 063 - Fixed Runtime Registration Caps
Problem: Construction registries used managed `List<T>` instances with cold capacities but no hard runtime caps. Endpoint, pump, pipe-node, hub, and placed-module registration could grow managed backing arrays during gameplay.
Solution: Add fixed capacity constants and count guards before `Add` in `BaseLogisticsNetwork`, `WaterPumpModule`, `LogisticsPipeTransportScheduler`, `RepairDroneHub`, and `ConstructionManager.RegisterModule()`. Overflow fails closed by skipping registration instead of allocating.
Rejected Alternatives: Migrating every registry to new native containers was rejected in this pass because these lists expose managed Unity object references and require owner-specific API changes. Leaving capacity hints only was rejected because hints are not a Zero-GC guarantee.
Scalability potential: Low tier drops excess endpoints/modules once configured caps are exhausted. Middle/High/Ultra can raise serialized/cold capacities before runtime without changing authority routes.
Hardware Impact: Adds one or two count comparisons per registration. Expected below 5 us on i3/MX350. Prevents worst-case managed resize spikes.

## Decision 064 - Docked Cargo Non-Alloc Traversal
Problem: `VehicleDockingModule` used `GetComponentsInChildren(true, List<StorageCrate>)` and stored connected crates in a managed list. A transport with more child crates than the list capacity could allocate during docking.
Solution: Replace both lists with fixed arrays: `StorageCrate[16]` for connected cargo and `Transform[64]` for traversal. Manual transform traversal uses `TryGetComponent`, duplicate checks, external power-grid rejection, and `finally` cleanup for stale transform references.
Rejected Alternatives: Raising list capacity was rejected because it still leaves growth semantics. Allocating a temporary native traversal buffer was rejected because the dock owns managed scene object references and the operation is bounded by scene hierarchy depth.
Scalability potential: Low tier gets a hard 16-crate bridge and 64-transform traversal. Middle/High/Ultra can raise constants after profiling without changing the algorithm.
Hardware Impact: Removes managed list growth and Unity component-list fill cost from docking. Traversal is bounded; expected below 15 us for normal docked vehicles on i3/MX350.

## Decision 065 - Habitat Graph Managed Growth Clamp
Problem: `HabitatGraphManager` still wrote `List.Capacity` at runtime and could expand module, rupture, and visual-link buffers during graph rebuilds. Socket/edge/dictionary insert paths also assumed capacity would be enough.
Solution: Remove runtime `Capacity` writes. Keep cold-allocated capacities as hard caps. Guard module, dictionary, socket, edge, rupture, emitted-VFX, and visual-link insertions. Overflow records habitat black-box overflow/topology flags and degrades graph/visual output instead of allocating.
Rejected Alternatives: Allowing resize during deformation was rejected because graph rebuild is exactly where frame spikes hurt. Failing the whole manager on the first overflow was rejected because partial visual degradation is safer than losing all habitat telemetry.
Scalability potential: Low tier keeps fixed graph budgets. Middle/High/Ultra can increase initial module capacity and edge capacities in cold configuration to buy larger bases and denser visual links.
Hardware Impact: Adds bounded count checks to rebuild paths, expected below 20 us on i3/MX350. Prevents managed resize spikes in base deformation/rebuild frames.

## Decision 066 - DataVault Bridge Cache Ownership
Problem: Logistics route scratch, pipe scheduler scratch, drone snapshot bridge, repair-drone acoustic bridge, and fluid pipe runtime still had paths that could resolve `GlobalRegistry.DataVault` from inside helper/bridge accessors rather than from the Construction owner cache.
Solution: Bind the cached `IDataVault` from `ConstructionManager` into logistics and scheduler bridge state, bind drone/repair event bridges from the drone manager's cached Vault, and make `FluidPipeGraphRuntime.ResolveDataVault()` return only its cached reference after cold boot/hot-swap.
Rejected Alternatives: Keeping direct registry reads in helper accessors was rejected because `GlobalRegistry` is cold identity/DI, not a hot polling route. Passing `GlobalRegistry.DataVault` through every individual call was rejected as noisier and easier to regress than owner-phase binding.
Scalability potential: Low tier avoids hidden service-locator cost during pipe/logistics/event frames. Middle/High/Ultra keep the same route capacity and can raise visual density without adding registry dependency noise.
Hardware Impact: Removes hot registry reads from affected bridge paths. Expected win is small but deterministic: one cached reference read instead of service-locator lookup per route scratch/event bridge access; no profiler timing was collected.

## Decision 067 - Fluid Pipe Write-Lock Closure
Problem: `FluidPipeGraphRuntime` had no persistent native owner fields left, but public mutation APIs still used raw mutable Vault views through `TryResolveBuffer()` for node registration, connection writes, pipe injection, rate/flag updates, oxygen demand clearing, and visual flow cache writes.
Solution: Remove `TryResolveBuffer()` and route all mutable writes through `TryAcquireSolveWriteBuffer()` with explicit lock-mask bits and `finally` release. Add lock bits for AUP and last-visual-flow lanes. Read-only room-exchange output now uses `TryReadOnlyBuffer()`.
Rejected Alternatives: Trusting `_solveScheduled` alone was rejected because it does not pin Vault memory against compaction or cross-domain writes. Completing the solve job just to mutate one field was rejected because it hides frame cost and violates dispatcher-owned completion windows.
Scalability potential: Low tier may fail closed on one pipe mutation if the Vault lane is contended. Middle/High/Ultra keep the same pipe graph model and can scale visuals through `GlobalQualityWeight`; no heavier fluid solver was introduced.
Hardware Impact: Adds short write-lock pairs to registration/mutation frames, estimated below 20 us on i3/MX350 for normal edits. Removes stale mutable-view risk under DataVault relocation. No profiler timing was collected.

## Decision 068 - Construction Save/Load Static Diagnostics
Problem: Root `ConstructionManager` save/load diagnostics used string interpolation and variable concatenation in development/editor branches. They are not release hot paths, but they violated the zero-GC static scan and can allocate during save/load verification.
Solution: Replace variable-rich diagnostic strings with static messages. Keep fail-closed behavior and counters unchanged; only diagnostic detail was reduced.
Rejected Alternatives: Keeping rich logs behind `UNITY_EDITOR || DEVELOPMENT_BUILD` was rejected because the current audit explicitly scans source, not only player builds. Introducing a custom fixed-char diagnostic formatter was rejected as unnecessary for these non-authority warnings.
Scalability potential: Low/Middle/High/Ultra gameplay behavior is unchanged. Developer builds avoid avoidable save/load diagnostic string allocation.
Hardware Impact: Saves only diagnostic-path allocations; release runtime cost is unchanged. No profiler timing was collected.

## Decision 069 - Construction Save DTO Preallocation Contract
Problem: `ConstructionManager.PopulateSaveData()` called `ConstructionDTO.EnsureCapacity()`, which can allocate five managed arrays from a Construction save callback if the DTO arrives cold or malformed. That violates the domain rule that Construction runtime callbacks write to owned storage instead of creating persistence buffers.
Solution: Move the Construction DTO array allocation to `SaveData.CreateNew()` by adding `ConstructionDTO.CreatePreallocated()`. `PopulateSaveData()` now checks the DTO array capacity, clears all Construction counts, logs a static warning in development/editor builds, and returns fail-closed when the SaveData contract is missing.
Rejected Alternatives: Keeping `EnsureCapacity()` in `PopulateSaveData()` was rejected because it hides managed allocations in a save callback. Editing every save caller was rejected because `SaveData` is the single persistence owner. Returning partial writes into undersized arrays was rejected because it risks corrupting save topology.
Scalability potential: Low tier avoids save-frame managed resize spikes inside Construction. Middle/High/Ultra keep the same fixed 256-module/1536-edge persistence budget; larger bases require a cold SaveData contract change, not runtime growth.
Hardware Impact: Removes up to five managed array allocations from the Construction save callback. Cold save creation now pays the fixed persistence allocation once under SaveData ownership; no profiler timing was collected.

## Decision 070 - Blueprint Preview Cold Read Handles
Problem: Blueprint preview buffer bootstrap checked existing Vault lanes with raw `TryResolveHandle()` before deciding whether to allocate/ensure handles. The path is cold, but raw validation can still observe relocatable memory without the read route.
Solution: Change `VRPipeBlueprintPreview.EnsureBuffersCold()` and `HectonBlueprintPreviewBatch.EnsureBuffersCold()` to use `TryReadHandle()` for state, visual, telemetry, and indirect-args lane validation. Existing `TryResolveHandle()` calls remain only in helpers that read buffers while the same system already holds pending write locks.
Rejected Alternatives: Treating cold validation as harmless was rejected because DataVault relocation safety must not depend on call frequency. Forcing write locks just to validate capacity was rejected as unnecessary; read handles express the intent.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. Under contention, preview bootstrap can fail closed or re-ensure later instead of touching weakly validated lanes.
Hardware Impact: Cold/bootstrap only; expected cost is one read-handle validation per preview lane and no measurable frame impact. No profiler timing was collected.

## Decision 071 - Cold Validation Read Route Sweep
Problem: Multiple Construction helpers used raw `TryResolveHandle()` only to validate that cold-created Vault lanes still existed and had enough capacity. This was not a managed allocation bug, but it made DataVault relocation safety harder to prove because validation and locked mutable access used the same raw primitive.
Solution: Change cold validation in `ConstructionManager`, `DroneFleetManager`, `HabitatGraphManager`, `FluidPipeGraphRuntime`, `FoundationSnappingCalculatorData`, `LogisticsPipeTransportScheduler`, `LogisticsRouteScratchMemory`, `ShinobuSocketConstructionData`, `VehicleDockingModule`, and `HabitatConstructionManager` to use `TryReadHandle()`. Add a separate `Read<T>()` helper in `BulkheadContainmentRuntime` for refresh/shader/gizmo paths while keeping raw `Resolve<T>()` for pinned job-write windows.
Rejected Alternatives: Leaving raw resolves because they were cold or already non-allocating was rejected; the contract should communicate read validation versus mutable locked access. Replacing all raw resolves was rejected because some remaining calls are intentionally inside held write-lock/pin windows.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. Under Vault contention or relocation, cold validation now fails closed through the read route while hot job writes still use pinned views.
Hardware Impact: Cold/read validation only; no measurable frame cost expected. No profiler timing was collected.

## Decision 072 - SumpPump Native View Proof Cleanup
Problem: `SumpPumpPipeGridRuntime` no longer owned persistent native fields, but two private helper signatures still returned `NativeArray<T>`. Static proof scanners could not distinguish those local handle views from persistent native ownership, so the release ledger still had noisy residuals.
Solution: Replace `BorrowMutable<T>()` and `Read<T>()` with boolean `TryBorrowMutable<T>(..., out NativeArray<T>)` and `TryRead<T>(..., out NativeArray<T>)`. Rewire all mutable/read call sites to explicit fail-closed checks. Keep raw `TryResolveHandle()` only inside locked mutable borrow helpers, and use `TryReadHandle()` for validation/read-only views.
Rejected Alternatives: Suppressing the scanner or documenting the helper return as a false positive was rejected because the code can express the ownership contract more clearly. Replacing SumpPump simulation math was rejected because this pass found proof-contract noise, not a physics-overcost defect.
Scalability potential: Low/Middle/High/Ultra simulation behavior is unchanged. The same GlobalQualityWeight cadence/fidelity route remains; the proof path is now cleaner for future capacity scaling.
Hardware Impact: No measurable runtime win claimed. Branch shape is equivalent to previous helper-default checks; expected delta is below noise on i3/MX350. No profiler timing was collected.

## Decision 073 - Origin-Shift Joint Recovery Non-Alloc Traversal
Problem: `ConstructionManager.RecoverHabitatJointsAfterOriginShift()` used `GetComponentsInChildren(true, List<Joint>)`. The list was cold-created with capacity, but Unity can grow the backing array if a module hierarchy has more joints than expected, creating managed allocation during AUP origin-shift recovery.
Solution: Replace the joint list with a cold fixed `Transform[]` traversal stack. Traverse module hierarchies manually, probe standard Unity joint components with `TryGetComponent<TJoint>()`, reuse the existing fixed Rigidbody velocity arrays, and fail closed by recording capacity overflow when the transform stack is exhausted.
Rejected Alternatives: Keeping the list and trusting initial capacity was rejected because capacity is not a hard runtime guarantee. Calling `GetComponentsInChildren<Joint>()` was rejected because it allocates a result array. Adding a larger list was rejected because it only moves the resize threshold.
Scalability potential: Low tier bounds origin-shift recovery by the configured stack and degrades by skipping excess child transforms with a development warning. Middle/High/Ultra can raise `initialCapacity`/stack capacity cold to support denser module prefabs without changing runtime behavior.
Hardware Impact: Removes a potential managed resize spike from origin-shift frames. Manual transform traversal adds simple child iteration and typed component probes; expected below 20 us for normal module hierarchies on i3/MX350. No profiler timing was collected.

## Decision 074 - Construction Save Nested Array Ownership
Problem: The save callback preallocation fix stopped top-level Construction arrays from allocating, but `ModuleDTO` nested arrays were still lost by `new ModuleDTO()` in `ConstructionManager.PopulateSaveData()`. `LogisticsSorterModule.PopulateSaveData()` and `CultivationManager.PopulateSaveData()` then allocated fresh nested arrays for every saved module.
Solution: Make `ConstructionDTO.EnsureCapacity()` preallocate nested sorter/cultivation arrays for all `ModuleDTO` slots. Add `ModuleDTO.ResetForConstructionSave()` to preserve and clear those arrays while resetting scalar fields. `ConstructionManager` now starts from `dto.modules[moduleIndex]` instead of a new module struct, and sorter/cultivation write into preowned arrays after capacity checks.
Rejected Alternatives: Allocating nested arrays lazily in Construction save was rejected because save callbacks are still runtime domain code. Moving sorter/cultivation payloads to separate global save arrays was rejected in this pass because it would be a save-format migration, not a contained ownership fix. Leaving stale nested arrays without clearing was rejected because old JSON/XML serializers may inspect full arrays, not only count fields.
Scalability potential: Low tier pays fixed cold persistence storage for 256 modules and never grows during save. Middle/High/Ultra keep the same save schema; larger bases require changing `ConstructionDTO.MaxModules` and cold preallocation, not runtime resize.
Hardware Impact: Removes up to six managed array allocations per saved Construction module from the save callback (`string[]`, `int[]`, `string[]`, `ulong[]`, `float[]`, `float[]`). Cold SaveData creation pays the deterministic nested-array budget. No profiler timing was collected.

## Decision 075 - Foundation Pylon DataVault Hot-Swap Rebind
Problem: `FoundationPylonGpuBatch` implemented `IGlobalRegistryHotSwapListener` but handled only the Player slot. If DataVault was registered late or replaced, the batch could keep a stale `_vault`, stale encoded SDF handle, and pending Vault locks tied to the wrong owner instance.
Solution: Add explicit `GlobalRegistryServiceSlot.DataVault` handling. The rebind path completes pending jobs, releases profile/socket fences and Vault locks through the old cached Vault, binds `currentService as IDataVault`, clears cached handle/upload state, and reruns cold buffer initialization when the component is active.
Rejected Alternatives: Relying on enable-time `GlobalRegistry.DataVault` lookup was rejected because late bootstrap/hot-swap is already part of the registry contract. Polling `GlobalRegistry.DataVault` in `LateFrameTick()` was rejected because registry reads do not belong in hot presentation loops. Holding old handles across service replacement was rejected as stale-handle risk.
Scalability potential: Low/Middle/High/Ultra visual behavior is unchanged. Under Vault replacement, the pylon preview fails closed for that frame and rebinds without scheduling against stale memory.
Hardware Impact: No steady-frame cost. Rebind work happens only on service replacement and includes one forced pending-job completion if a pylon build is active. No profiler timing was collected.

## Decision 076 - Disabled Owner DataVault Stale-Handle Closure
Problem: Several Construction owners handled active DataVault hot-swap but still had a disabled-owner gap. `VRPipeBlueprintPreview`, `HectonBlueprintPreviewBatch`, and `FoundationPylonGpuBatch` could retain stale `_vault` and generation handles across disable/re-enable. `SumpPumpPipeGridRuntime` ignored DataVault replacement when `currentService == null`. `AutonomousExtractorSystem` and `FluidPipeGraphRuntime` could reuse an old cached Vault on cold re-enable after a service replacement that happened while their listeners were unregistered.
Solution: Clear preview/pylon Vault descriptors on disable/destroy and on every DataVault replacement, including null replacement. Make SumpPump set `_buffersReady=false` when the replacement Vault is null. Add cold-enable rebinding for Autonomous Extractor and Fluid Pipe: compare the cached Vault with `GlobalRegistry.DataVault`, release old handles only when the service instance changed, bind the current service, and force cold reinitialization.
Rejected Alternatives: Leaving disabled owners to keep handles was rejected because listeners are unregistered while disabled. Polling `GlobalRegistry.DataVault` every hot tick was rejected because registry access is cold DI only. Releasing buffers on every disable was rejected for Autonomous/Fluid because normal temporary disable should not discard owner state unless the Vault service actually changed.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. Under DataVault teardown/replacement, affected systems fail closed or reinitialize from current owner storage rather than scheduling against stale memory.
Hardware Impact: No steady-frame cost. Work is limited to service replacement or cold enable. The only forced completion is existing pending-job teardown before descriptor invalidation; no profiler timing was collected.

## Decision 077 - Drone DataVault Rebind Without Managed Churn
Problem: `DroneFleetManager` reacted to DataVault replacement by rebinding event bridges but initially did not release headless and transaction generation handles through the old Vault. The first correction used full `ReleaseHeadlessNativeMemory()`, which fixed stale handles but also nulled managed lookup arrays and forced cold array reallocation during a service-event rebind.
Solution: Split teardown into two paths. `ReleaseHeadlessVaultHandles(IDataVault)` releases service-command locks, headless scratch locks, all headless Vault handles, and transaction Vault handles against the old cached Vault, then clears managed state in place. `ReleaseHeadlessNativeMemory()` remains the full subsystem reset path and drops managed arrays after handle release. `AllocateHeadlessNativeMemory()` now calls `EnsureHeadlessManagedMemory()` so arrays are created only when missing or wrong-sized, then clears them in place before seeding new Vault lanes.
Rejected Alternatives: Releasing through `s_CachedDataVault` after assigning the new service was rejected because old handles must be returned to the old owner. Keeping the full managed teardown for hot-swap was rejected because service replacement is not a license to allocate managed arrays. Leaving stale managed references when the new Vault is null was rejected because fail-closed teardown must not retain old drone/task object links.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. Under DataVault hot-swap, weak devices avoid managed array churn; high-tier devices keep the same fleet capacity and visual path without hidden stale-handle risk.
Hardware Impact: No steady-frame cost. Service-event path removes avoidable `RepairDroneHub[]`, `int[]`, `bool[]`, `BaseModule[]`, `HectonVoxelVolume[]`, `Vector3[]`, and `PendingDroneLaunch[]` reallocations when arrays already match configured capacities. No profiler timing was collected.

## Decision 078 - Multiplatform Construction Dump Paths
Problem: `FoundationSnappingCalculatorData`, `ModularBaseConstructionValidator`, and `ShinobuSocketConstructionData` still exposed absolute Windows-only default dump paths rooted at `C:\hades\Hecton8`. Those paths fail in CI, macOS/Linux editor sessions, alternate local worktrees, and packaged diagnostic runs.
Solution: Replace the absolute constants with relative `Docs/AgentLogs/...` defaults. Add cold `ResolveDumpPath()` helpers that preserve caller-supplied rooted paths but resolve relative defaults from `Application.dataPath` to the project root before file creation. Binary dump payloads and DTO layouts are unchanged.
Rejected Alternatives: Keeping absolute paths was rejected as non-portable and hostile to parallel agent worktrees. Resolving through `Directory.GetCurrentDirectory()` alone was rejected because Unity editor/player working directories are less explicit than `Application.dataPath`. Moving all dump routing into a shared core helper was rejected in this pass to avoid cross-domain dependency churn.
Scalability potential: Low/Middle/High/Ultra gameplay is unchanged. Diagnostics now work across developer machines and CI agents without per-user path edits.
Hardware Impact: No steady-frame cost. Path resolution is only in crash/telemetry dump calls; no profiler timing was collected.

## Decision 079 - AUP Double-To-Float Bridge Range Gates
Problem: Several Construction runtime bridges subtracted the AUP origin in double precision but only checked finiteness after casting the local `double3` delta to `float3`/`Vector3`. That leaves an overflow window for corrupt AUP, bad rebase data, or huge sector deltas before fail-closed validation sees the value.
Solution: Add explicit `math.isfinite(double3)` and `abs(delta) <= float.MaxValue` gates before every touched bridge cast. Unsafe values now return `false`, propagate `float.NaN` into existing invalid-path checks, set `BuilderGhostValidationFlags.NonFinite`, or write an invisible tiny fallback matrix instead of creating an infinite runtime-space vector.
Rejected Alternatives: Clamping huge AUP deltas was rejected because it lies about position and can create false snap/target success. Keeping post-cast `math.isfinite(float3)` was rejected because the conversion itself is the unsafe bridge. Replacing the local presentation math with a heavier spatial solver was rejected because this is a guard defect, not a need for more simulation.
Scalability potential: Low/Middle/High/Ultra behavior remains the same for valid data. Weak devices fail closed before bad matrices or drone distance vectors poison a frame; high-tier devices retain the same visual fidelity and can still use continuous quality scaling elsewhere.
Hardware Impact: Adds one finite/range branch at selected AUP bridge points. No profiler timing was collected; expected steady-frame delta is below noise on i3/MX350. The gain is reliability: no Inf/NaN runtime vectors from overflowed local AUP casts.

## Decision 080 - Schedule-Failure Vault Lock Guards
Problem: Several Construction paths acquired Vault write locks for preview, validation, or flood propagation buffers and then called Unity Job `Schedule()` without a local failure guard. Normal player builds should not throw there, but editor/development safety paths can fail before pending-state is fully recorded. That creates either leaked Vault locks or, if naively fixed, lock release while a partially scheduled job still owns the native views.
Solution: Wrap the schedule transition in `try/finally`. For single-job paths, release write locks only when scheduling never succeeded. For preview paths that can schedule a chain of builder jobs before scheduling indirect args, preserve the already scheduled handle as a pending discard job so finalization waits for the fence and releases locks through the existing teardown path. `HabitatGraphManager` now records flood pending state before active-job registration, so registration failure does not lose ownership of a scheduled job.
Rejected Alternatives: Assuming `Schedule()` cannot fail was rejected because Unity safety validation can fail in editor/development sessions. Catching and swallowing exceptions was rejected because it hides the root fault and adds managed exception behavior to runtime paths. Releasing locks unconditionally in `finally` was rejected because a job may already be scheduled over those buffers.
Scalability potential: Low/Middle/High/Ultra valid-frame behavior is unchanged. Failure-path behavior is safer: weak devices or editor safety failures drop the visual/validation result instead of leaking a lock or exposing relocated memory to a live job.
Hardware Impact: Adds only a branch and pending-discard assignment around schedule transitions. Steady-frame cost is 0 us in the common path by design; no profiler timing was collected. The value is lock/fence correctness under rare schedule failure.

## Decision 081 - Stack-Only Habitat Vault View Containers
Problem: `HabitatGraphManager` used `HabitatGraphWriteViews` and `HabitatFloodGraphJobViews` as local view bundles over DataVault-owned graph buffers. They held `NativeArray<T>` views but were declared as ordinary structs, which leaves a future path for accidental persistence in a field or container even though current use is local/ref/out only.
Solution: Convert both view bundles to `private ref struct`. This makes the ownership contract mechanical: the compiler keeps the graph/flood Vault views on the stack and blocks capture, boxing, heap fields, async storage, and normal generic container storage.
Rejected Alternatives: Keeping ordinary structs with a comment was rejected because it depends on reviewer discipline. Replacing all method signatures with individual `NativeArray<T>` parameters was rejected because it increases call-site noise and does not improve lifetime safety as clearly as `ref struct`.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. This is a proof-strengthening change around graph rebuild/flood propagation write windows, not a simulation or quality change.
Hardware Impact: 0 runtime us expected. `ref struct` changes compile-time lifetime restrictions only; no generated runtime work is intended. No profiler timing was collected.

## Decision 082 - Bulkhead Multi-Job Fence Ownership
Problem: Bulkhead pre-simulation, simulation, hatch-lock, mock-data, and telemetry paths schedule job chains while DataVault pins are held. Several chains recorded `_simulationScheduled` or `_preSimulationScheduled` only after the final job. If a later `Schedule()` failed after an earlier job was already scheduled, cleanup could release pins under a live job or lose the handle needed for no-wait finalization.
Solution: Record the latest successfully scheduled bulkhead simulation handle immediately through `TrackScheduledSimulationJob()`. Pre-simulation override jobs now set `_preSimulationHandle` and `_preSimulationScheduled` before collision scheduling. The main simulation scheduling body has a `finally` release guard that releases pins only when no pre/simulation job was recorded.
Rejected Alternatives: Recording only the final telemetry job was rejected because it leaves partial chains unowned. Unconditional pin release in a method-level `finally` was rejected because it is unsafe after any job has been scheduled. Catching and swallowing schedule exceptions was rejected because runtime managed exception handling hides the safety fault.
Scalability potential: Low/Middle/High/Ultra valid-frame behavior is unchanged. Under editor/development safety failure, weak devices and CI keep memory ownership deterministic instead of leaking or exposing moved Vault lanes to scheduled jobs.
Hardware Impact: Adds field assignments after successful schedule calls and one final release guard. Steady-frame cost is below measurement noise and no profiler timing was collected. This is a correctness fix for rare schedule-failure windows.

## Decision 083 - Bulkhead Partial Fence Active-Job Ledger Registration
Problem: The bulkhead partial-fence fix still had a global ownership gap. Partial schedules could set `_simulationScheduled` or `_preSimulationScheduled` and retain DataVault pins locally, but the global `H8Memory` owner ledger only saw selected final handles. A teardown/diagnostic owner fence could therefore miss a live partial bulkhead job.
Solution: Move `H8Memory.RegisterActiveJob(SystemID.Construction, handle)` into `TrackScheduledSimulationJob()` and add `TrackScheduledPreSimulationJob()` for pre-simulation handles. Every successful bulkhead schedule call now goes through one of those helpers immediately after `Schedule()`.
Rejected Alternatives: Keeping explicit one-off registrations near final telemetry jobs was rejected because it repeats the same omission class. Registering only when the whole chain completes scheduling was rejected because schedule failure after a partial job is exactly the defect class being closed.
Scalability potential: Low/Middle/High/Ultra valid-frame behavior is unchanged. Under editor/development schedule failure or forced teardown, the owner ledger now sees partial bulkhead fences instead of relying on local fields only.
Hardware Impact: Adds one owner-ledger `JobHandle.CombineDependencies` path per already scheduled bulkhead job. No profiler timing was collected; expected cost is below measurement noise compared to the scheduled jobs themselves. No dotnet/build was launched by user order.

## Decision 084 - Bulkhead Collision AUP Float-Range Gate
Problem: `ProcessPlayerBulkheadCollisionJob` correctly computed player-to-bulkhead deltas as `double3 PlayerAup - plane.CenterAup`, but it cast those local deltas to `float3` before proving the double values fit in float range. A corrupt AUP or stale plane center could create an Inf/NaN vector during the bridge step.
Solution: Add `BulkheadContainmentMath.CanCastLocalDeltaToFloat3(double3)` and call it before both collision delta casts. Invalid deltas set `BulkheadCollisionFlags.NonFinite` and skip the candidate bulkhead instead of clamping or lying about position.
Rejected Alternatives: Keeping the post-cast `math.isfinite(float3)` check was rejected because the unsafe conversion already happened. Clamping oversized deltas was rejected because it can create false collision acceptance. Moving this to a heavier spatial solver was rejected because the local-plane fake is correct; only the bridge guard was missing.
Scalability potential: Low/Middle/High/Ultra valid behavior is unchanged. Weak devices fail closed on corrupt far-sector data; high-tier devices keep the same cheap collision fake and can spend cycles elsewhere.
Hardware Impact: Adds two double3 finite/range checks per active closed bulkhead candidate in the player collision job. No profiler timing was collected; expected cost is below noise on i3/MX350 relative to the existing plane collision loop.

## Decision 085 - Construction DTO Private Byte Padding Sweep
Problem: A broad Construction DTO scan still found padding exposed as public fields or represented as coarse `uint`/`ulong` lanes. Some jobs and cold builders wrote those `_pad*` fields only to satisfy struct definite-assignment, which made padding look like payload and weakened the ARM64 byte-offset proof.
Solution: Convert the safe Construction padding clusters to private byte fields at exact offsets. The changed DTOs are hatch state, bulkhead state, construction signals, foundation counters/ray/warning signal, habitat integrity scratch records, modular validation DTOs, and Shinobu socket/ghost DTOs. DTO locals that previously wrote padding now start from `default`, then assign real payload fields only.
Rejected Alternatives: Leaving public padding with a report-only waiver was rejected because the prompt requires byte-level private padding proof. Reordering public signal payload fields was rejected because `ConstructionPreviewSignal` and `FloraExclusionSignal` are cross-domain ABI packets; offsets must not move without a versioned migration. Adding runtime memset jobs was rejected because default initialization is cheaper and does not introduce job/fence complexity.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. Weak devices get the same binary layouts with clearer ARM64 alignment proof; high-tier devices keep the same visual/validation fidelity and signal capacity.
Hardware Impact: 0 steady-frame us claimed. This is ABI/proof hardening and removal of meaningless padding writes. No profiler timing was collected; no dotnet/build was launched by user order.

## Decision 086 - Editor-Only DTO Offset Probes
Problem: Several Construction layout validators still executed `Marshal.OffsetOf` or reflection-backed field-offset probes from runtime-callable guard paths. The validators were cold, but still player/runtime code: drone fleet boot, transaction allocation, fluid pipe buffer init, foundation/socket/validator initialization, repair-drone acoustic queue setup, deconstruction layout checks, and bulkhead layout guards could pay managed metadata work to prove offsets already fixed by explicit layout declarations.
Solution: Split every touched validator into two phases. Player/runtime path checks only `UnsafeUtility.SizeOf<T>()` against the explicit size constants and fails closed on mismatch. `UNITY_EDITOR` keeps the exact byte-offset map through `Marshal.OffsetOf` or `UnsafeUtility.GetFieldOffset()` for authoring proof. This preserves the ARM64 offset evidence without pulling managed layout probes into player validation.
Rejected Alternatives: Keeping reflection in cold runtime guards was rejected because the task requires zero managed leakage in runtime code paths. Removing offset checks entirely was rejected because editor authoring still needs byte-map proof. Replacing all checks with comments was rejected because comments do not catch ABI drift.
Scalability potential: Low/Middle/High/Ultra gameplay behavior is unchanged. Weak devices avoid metadata/reflection work during cold initialization and fail closed on size mismatch; high-tier/editor keeps exact byte-map proof.
Hardware Impact: Removes managed offset-probe work from player/runtime validation paths in `DroneFleetNavigationKernel`, `DroneFleetManager_Transactions`, `FluidPipeGraphTypes`, `FoundationSnappingCalculatorData`, `ModularBaseConstructionValidator`, `ShinobuSocketConstructionData`, `RepairDroneEntity`, `HabitatDeconstructionTransactionKernel`, and `BulkheadContainmentContracts`. No profiler timing was collected; dotnet/build was not launched by user order.

## Decision 087 - Stack-Only Public Vault View Bundles
Problem: `ModuleCatalogViews`, `ModuleCatalogWriteLease`, `FoundationSnappingVaultViews`, and `ConstructionSocketVaultViews` carried `NativeArray<T>` views or write-lock lease state but were ordinary public structs. Current call sites used them as locals/out parameters, but ordinary structs can later be stored in fields, boxed indirectly through generic containers, or captured, weakening the transient DataVault view contract.
Solution: Convert the four public view/lease containers to `ref struct`. This makes the lifetime rule mechanical: these Vault views and catalog write leases stay on the stack, cannot be stored in class fields, cannot be captured by async/lambda state, and cannot outlive the dispatcher phase by ordinary managed storage.
Rejected Alternatives: Leaving them as structs with comments was rejected because future reviewers could still persist them accidentally. Replacing the bundles with long parameter lists was rejected because it increases call-site error risk and does not strengthen lifetime proof as cleanly.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. This is ownership-proof hardening only; weak and high-tier devices run the same math and visuals.
Hardware Impact: 0 runtime us expected. `ref struct` is a compile-time lifetime restriction for transient Vault views and write leases. No profiler timing was collected; dotnet/build was not launched by user order.

## Decision 088 - Static Runtime DataVault Owner Instance Binding
Problem: Foundation and socket static runtime helpers stored Vault handles and seed/dump state outside any MonoBehaviour owner. A DataVault replacement can reuse generation values from zero while `FoundationSnappingCalculatorRuntime` still believes its telemetry cursor was seeded. `ShinobuSocketConstructionRuntime.ShouldResetCounterLane()` trusted counter ranges alone, so a fresh uninitialized int lane could look valid by chance and poison socket topology counts.
Solution: Add explicit bound-owner tracking to both static runtimes. On `IDataVault` instance change, reset static handles and dump/seed flags. Foundation editor tuning/profile writes now use the bound Vault instead of polling `GlobalRegistry.DataVault`; `FoundationPylonGpuBatch.ClearVaultCacheCold()` unbinds the old owner. Socket counters now reserve slot 7 as magic `0x534B5431`; initialization and mock grid generation stamp it, and validation rejects missing/bad magic before trusting module/socket counts.
Rejected Alternatives: Trusting generation alone was rejected because generation is scoped to a Vault instance, not globally monotonic across service replacement. Trusting counter ranges was rejected because uninitialized memory can accidentally satisfy bounds. Polling `GlobalRegistry.DataVault` from static editor-write helpers was rejected because it bypasses the active Construction owner route.
Scalability potential: Low/Middle/High/Ultra valid behavior is unchanged. On weak devices or editor hot-swap, foundation/socket systems fail closed or reinitialize deterministic counters instead of reading stale descriptors or random native memory. High-tier visuals keep the same foundation pylon and socket holography paths.
Hardware Impact: 0 steady-frame us expected. Work occurs on cold initialization or DataVault owner replacement only; adds one reference compare and descriptor reset path. No profiler timing was collected; dotnet/build was not launched by user order.

## Decision 089 - Validation and Logistics Static Descriptor Owner Binding
Problem: `ModularBaseConstructionValidator` still kept static validation `VaultGenerationHandle<T>` descriptors for tuning, telemetry, bounds, and occupancy without tracking which `IDataVault` instance created them. A replacement Vault can reuse generation values, so read/write helpers could trust stale descriptors or preserve the old telemetry dump latch. `LogisticsPipeTransportScheduler.ReleaseVaultHandles()` also assumed a non-null Vault owner during descriptor release, leaving an avoidable stale-field state if emergency teardown reached it with no owner.
Solution: Add `s_BoundVault` and `BindValidationVault(IDataVault)` to the validator. Every Vault-backed validation read/write helper now binds the current owner first; on owner change, old descriptors release through the old Vault and static handles reset. Validator subsystem registration also clears last validation state and descriptors. Logistics scheduler release now defaults every descriptor when no owner is available and only calls `ReleaseBuffer` when BufferID and Generation are nonzero.
Rejected Alternatives: Trusting caller order after DataVault service replacement was rejected because PlayerBuilder/editor tools can call validator helpers independently. Trusting generation-only descriptors was rejected because generation is scoped to the Vault instance. Leaving scheduler handles untouched when `vault == null` was rejected because emergency teardown paths must fail closed, not preserve impossible state.
Scalability potential: Low/Middle/High/Ultra gameplay and quality scaling are unchanged. Weak devices avoid stale validation buffers after service replacement; high-tier devices keep the same builder validation and logistics scheduler behavior.
Hardware Impact: 0 steady-frame us expected. Validator adds reference compare work in cold/helper paths; logistics change is teardown-only. No profiler timing was collected; dotnet/build was not launched by user order.

## Decision 090 - Read-Accessor Purity and Owner-Bound Presentation Services
Problem: `HabitatGraphManager` still had read-style `Resolve*` methods that lazily polled `GlobalRegistry` for atmosphere, ambient current, audio, and fluid decal presentation services, then mutated local fields. `BaseDegradationSystem` also pulled construction parasite graph and fluid decal services directly from `GlobalRegistry` during rupture effects. Several private helpers named `Resolve*` performed component capture through `TryGetComponent`/parent component lookups, weakening the project rule that read accessors are pure.
Solution: Move presentation service ownership to `ConstructionManager`: cold cache reads happen in the owner, hot-swap updates push interfaces into `HabitatGraphManager`, and disable/shutdown clears them. `HabitatGraphManager` now uses cached getters only. `BaseDegradationSystem` receives parasite graph and fluid decal sinks through `BindRuntimeServices()`. Component-capturing helpers were renamed to `Capture*` or `Find*`; drone DataVault cold refresh was renamed from `ResolveDroneDataVaultForColdPath()` to `RefreshDroneDataVaultForColdPath()` because it can rebind owner state.
Rejected Alternatives: Leaving lazy `GlobalRegistry` reads inside graph accessors was rejected because read accessors must not mutate state or poll global services. Adding more fallback registry reads in `HabitatGraphManager` was rejected because it hides dependency ownership. Keeping `Resolve*` names for component capture was rejected because it makes static review classify non-pure work as read access.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged for valid services. Weak devices avoid hidden hot registry reads during graph rebuild/audio/fluid presentation, and high-tier devices keep the same analytical stress, rupture VFX, and ambient current fidelity through owner-bound service routes.
Hardware Impact: 0 steady-frame us claimed from measurement. Expected benefit is removal of hidden registry polls from graph presentation paths; remaining work is direct interface reads. No profiler timing was collected; dotnet/build was not launched by user order.

## Decision 091 - Fail-Closed Runtime Service Routes for Physics, Hazards, and Extractors
Problem: Construction still had three service-route defects after the accessor cleanup. `ConstructionManager.RestoreCapturedJointBodyVelocities()` read `GlobalRegistry.Physics` during AUP origin-shift recovery instead of using owner-bound service state. `CultivationManager` unregistered hazard zones through the current global hazard runtime, which can miss zones registered in a previous service after hot-swap, and its register path could call `HazardZoneManager.EnsureRuntimeInstance()` from slow-tick botany logic. `AutonomousExtractorModule.TryRegister()` could create `[AutonomousExtractorSystem]` through `EnsureRuntimeInstance()` on module enable, leaving a hidden managed `GameObject`/`AddComponent` allocation path.
Solution: Cache `IPhysicsService` in `ConstructionManager` and refresh it on `GlobalRegistryServiceSlot.Physics`. Cache `HazardZoneManager` in `CultivationManager`, unregister existing toxic/rot zones against the previous service on `HazardZoneRuntime` replacement, and fail closed when no hazard runtime is bound. Extractor modules now register only with an already active `AutonomousExtractorSystem`; the unused public ensure/allocator API was removed.
Rejected Alternatives: Polling `GlobalRegistry.Physics` inside origin-shift recovery was rejected because origin shift is a sensitive AUP path and should use cached owner routes. Calling `HazardZoneManager.EnsureRuntimeInstance()` from cultivation slow tick was rejected because it can bootstrap environment runtime from gameplay code. Keeping extractor auto-spawn was rejected because module enable/pool activation must not allocate service roots.
Scalability potential: Low/Middle/High/Ultra behavior remains deterministic for valid services. Weak devices avoid surprise runtime object creation and service-locator work; high-tier devices keep the same joint velocity restoration, toxic botany hazard, and extractor behavior when the owning runtimes are present.
Hardware Impact: 0 measured us. Expected effect is removal of hidden service polling and elimination of one possible `GameObject` plus component allocation from extractor module enable. No profiler timing was collected; dotnet/build was not launched by user order.

## Decision 092 - Release Fabrication Closure and Bounded Parent Capture
Problem: Construction still had release-runtime escape hatches that could fabricate scene objects from damaged authoring data. `ConstructionRuntimeProxyFactory` created proxy `GameObject`, component, mesh, and material instances; `ConstructionManager.RegisterModule()` auto-added `ModuleMarker`; load/restore could create a proxy when `BuildableData.finalPrefab` was missing. A separate runtime scan found direct `GetComponentInParent` use in extractor, botany, cultivation, water pump, repair hub, and vehicle docking paths, plus missing-component null risks in docking and pipe nodes.
Solution: Gate `ConstructionRuntimeProxyFactory` behind `UNITY_EDITOR || DEVELOPMENT_BUILD`. In player builds, markerless modules are retired fail-closed and missing `finalPrefab` records are skipped instead of proxy-spawned. `VehicleDockingModule` and `LogisticsPipeNode` now bind optional components with `TryGetComponent`; docking trigger checks require a cached collider. Added `ConstructionParentLookup.TryCaptureSelfOrParent()` with a 32-parent cap and replaced every direct Construction `GetComponentInParent` call with bounded owner-local capture.
Rejected Alternatives: Shipping proxy fabrication was rejected because it hides authoring defects behind managed allocations and changes release behavior. Keeping `AddComponent<ModuleMarker>` in player builds was rejected because prefab contracts must be authored, not repaired at runtime. Leaving `GetComponentInParent` was rejected because it is unbounded scene traversal in release code and cannot express a fail-closed depth limit. Broad component caches were rejected because several captures are one-shot or event-driven and do not justify new persistent managed state.
Scalability potential: Low tier skips broken module records instead of spending CPU and heap budget on debug visuals. Middle/high/ultra tiers keep real authored prefabs and docking/extractor behavior; visual overkill remains tied to actual authored assets, not emergency proxies. The bounded parent capture path has identical truth ownership but deterministic traversal cost.
Hardware Impact: 0 measured us. Expected gain is removal of player-load `GameObject`/component/mesh/material allocation paths and replacement of unbounded parent traversal with a capped scan. Bounded capture cost is at most 32 `TryGetComponent` probes per capture, used in cold or event-triggered paths, not a per-frame solver.

## Decision 093 - Hidden Parent Service Traversal Closure
Problem: Loop 69 removed direct `GetComponentInParent` calls, but five Construction systems still called `ComponentReferenceUtility.ResolveParentService<T>()`. That Core helper walks parents and calls `GetComponent(typeof(T))`, so it preserved the same unbounded scene traversal behind a neutral utility name.
Solution: Replace the six remaining Construction call sites with `ConstructionParentLookup.TryCaptureSelfOrParent()` in `BatteryBankModule`, `FluidPipeGraphRuntime`, `LogisticsPipeNode`, `WaterPumpModule`, and `VehicleDockingModule`. The capture stays owner-local, deterministic, and capped at 32 parent probes.
Rejected Alternatives: Keeping the Core utility was rejected because the call site no longer exposes traversal cost or fail-closed depth. Adding persistent service fields beyond the existing caches was rejected because the lookups are cold/event captures and do not justify more managed state. Routing through GlobalRegistry was rejected because these are local parent ownership relationships, not global service authority.
Scalability potential: Low/Middle/High/Ultra gameplay behavior is unchanged for valid authored hierarchies. Weak devices avoid an unbounded parent walk if a hierarchy is malformed; high-tier devices keep the same docking, atmosphere, logistics, and pump behavior through bounded capture.
Hardware Impact: 0 measured us. Cost is capped at 32 `TryGetComponent` probes only when the systems capture parent services during cold/event paths. No dotnet/build or profiler run was performed by user order.
