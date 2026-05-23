# Rationale_SHINOBU_335

Status: IMPLEMENTED / POLISH HARDENED / [BLOCKED BY DEPENDENCY]
Evidence class: STATIC_SOURCE plus three dotnet build attempts; runtime Unity import/profiler still blocked by unrelated compile walls.

## Decision 001 - Integration Surface

Problem: The requested path `Assets/_Project/Scripts/Vehicles/Drones/` does not exist, while drone fleet logic is already implemented in `Construction/DroneFleetManager.cs` and launched by `RepairDroneHub.cs`.
Solution: Use the existing fleet manager as authority and add isolated transaction code through partial-extension files if the class can be made partial safely.
Rejected Alternatives: A new `HectonDroneTransactionManager` would duplicate fleet ownership, create bootstrap/register order risk, and violate the prompt's partial integration mandate.
Scalability potential: Low/Middle/High/Ultra all keep one owner route; visual load scales through signals, not new GameObjects.
Hardware Impact: Avoids extra manager polling and scene lookup; estimated low-end saving is 4-12 us/frame versus an additional managed dispatcher path.

## Decision 002 - Signal Route

Problem: Repair completion already has an existing `HullRepairedSignal` lane; adding a drone-specific repair signal would fragment consumers.
Solution: Reuse `SignalBus<HullRepairedSignal>` for completed hull repair and add only a welding VFX request if no existing VFX spark lane can carry the payload.
Rejected Alternatives: `DroneFixedLeakSignal` or C# events would create new global route surface without fan-out proof.
Scalability potential: Low emits sparse VFX signals; Middle emits normal cadence; High/Ultra spend saved CPU on richer VFX consumers.
Hardware Impact: Avoids LineRenderer/ParticleSystem object work; estimated low-end saving is 20-80 us/event plus zero hierarchy churn.

## Decision 003 - Data Shape

Problem: Drone task state must be job-readable and ARM64-safe while other agents may be changing navigation.
Solution: Define a 32-byte explicit `DroneTaskDTO` with raw fields and padding exactly matching the prompt layout; keep navigation state separate and read only AUP/target buffers.
Rejected Alternatives: C# properties or object task classes would cause copies, boxing risk, and cache-hostile pointer chasing.
Scalability potential: Low processes fixed arrays cheaply; High/Ultra can increase visual telemetry without changing transaction truth layout.
Hardware Impact: 32-byte DTO gives two tasks per 64-byte cache line; 50 drones fit in 1600 bytes, expected sub-1 us Burst traversal on i3/MX350 class CPU pending measurement.

## Decision 004 - Navigation DTO Collision

Problem: The existing navigation kernel already used the name `DroneTaskDTO` for a 64-byte assignment record, while the assignment explicitly requires `DroneTaskDTO` to be a 32-byte destination transaction DTO.
Solution: Rename the old assignment record to `DroneAssignmentTaskDTO` and reserve `DroneTaskDTO` for SHINOBU_335's 32-byte atomic transaction lane.
Rejected Alternatives: Keeping two incompatible `DroneTaskDTO` meanings would break ABI validation and produce hidden cache layout bugs. Adding a namespace split would not help inside the same construction assembly.
Scalability potential: Low/Middle keep small transaction traversal; High/Ultra can add debug snapshots without bloating truth DTOs.
Hardware Impact: Avoids traversing 64-byte assignment records for destination work. Estimated L1 traffic reduction for 50 active transactions: 1600 bytes/frame.

## Decision 005 - Atomic Repair Bridge

Problem: The prompt requests Interlocked repair against base integrity, but live `BaseModule.CurrentIntegrity` is a managed owner field, not a shared `NativeArray<IntegrityStateDTO>` route available inside this domain.
Solution: Stage repair in `DroneTransactionIntegrityDTO` as fixed-point milli-integrity, mutate it inside Burst with `Interlocked.CompareExchange`, then apply the validated delta through the BaseModule owner phase and publish `HullRepairedSignal`.
Rejected Alternatives: Writing managed `BaseModule` fields inside Burst is impossible and unsafe. Creating a parallel integrity owner would split truth from the habitat deformation owner.
Scalability potential: Low devices still run a single CAS and sparse VFX; Ultra can increase spark density without changing repair truth.
Hardware Impact: One CAS retry loop per repair command; avoids lock contention and managed coroutine waits. Estimated saving versus managed wait/object route: 10-40 us/frame for a 50-drone fleet, pending profiler.

## Decision 006 - SoA Inventory Injection

Problem: Mining results must enter the Agent 316 SoA inventory without `inventory.Add()` and without object allocation.
Solution: Bind `SoaInventoryQueryEngine` vault handles during cold bootstrap, pass hash/quantity/durability/active-count arrays to `EvaluateDroneTransactionsJob`, then use CAS/Interlocked to claim or increment slots.
Rejected Alternatives: Managed inventory commands allocate and serialize late. A private drone inventory would create a second owner for resources.
Scalability potential: Low searches existing flat arrays; Middle/High/Ultra can raise storage capacity through vault configuration without changing transaction math.
Hardware Impact: Matching stack path is one linear scan and one CAS. Empty slot path is one hash CAS plus quantity CAS. No hierarchy updates, no GC, no string item lookup.
Superseded: Decision 019 rejects direct mutation of `SoaInventoryQueryEngine` mirror buffers after forensic review proved `PlayerInventory` owns the canonical SoA state.

## Decision 007 - AUP Distance Gate

Problem: At 100 km scale, casting absolute positions to float before subtracting causes false drone arrival and remote repair/mining.
Solution: Mirror `DroneStateDTO.CurrentAUP` and `DroneTargetDTO.TargetAUP`, subtract in `double3`, validate finite delta, then cast the local delta to `float3` for radius squared comparison.
Rejected Alternatives: Reusing local `HeadlessDroneState.Position` alone ignores origin shifts and is not rollback-safe at far coordinates.
Scalability potential: The same math works on weak and high-end machines; quality only changes visual cadence, not arrival truth.
Hardware Impact: Three double subtracts per active transaction are cheaper than any physics raycast. Expected cost below 0.5 us for 50 drones on i3/MX350 pending profiler.

## Decision 008 - Welding Dear Lie

Problem: Physical laser beams/particle objects from each repair drone would break batching and turn repair progress into a rendering-thread problem.
Solution: Generate only deterministic VFX intent flags in the job, then publish `DebrisSpawnSignal` and `VfxSparkRequestSignal` from the owner phase. Spark chance is `lerp(0.08, 1.0, GlobalQualityWeight^2)`.
Rejected Alternatives: `LineRenderer`, `ParticleSystem`, `Instantiate`, or runtime mesh sparks all create object churn and draw-call instability.
Scalability potential: Low emits sparse flashes; Middle emits readable cadence; High/Ultra emits full spark density. Repair rate never changes.
Hardware Impact: Low-tier VFX signal count drops to about 8% of ultra. Saved CPU is spent on GPU-side visual overkill only when quality weight allows it.

## Decision 009 - Build Wall Handling

Problem: `dotnet build Hecton8.Core.csproj` failed before SHINOBU_335 verification on `ConstructionManager` references to SHINOBU_336 deconstruction DTOs because `HabitatDeconstructionTransactionKernel.cs` existed but was not compiled.
Solution: Add a guarded Core compile include for `Assets/_Project/Scripts/Construction/HabitatDeconstructionTransactionKernel.cs` in `Directory.Build.targets`, matching the existing project include pattern.
Rejected Alternatives: Editing `ConstructionManager` to remove another agent's feature would be sabotage. Rebuilding repeatedly while CPU/dotnet gates were active would violate the hardware protection rule.
Scalability potential: No runtime behavior change; compile surface now sees the intended SHINOBU_336 kernel.
Hardware Impact: Cold build-only fix. Runtime impact: 0 us.

## Decision 010 - Compile Wall Stop Point

Problem: After fixing the SHINOBU_336 include and the SHINOBU_335 `BaseModule` namespace import, the third compile attempt still fails on unrelated partial-class gaps in submarine gyro, ballast, VR somatic comfort, metabolism contract, combat status effect, and construction loot-cache routing.
Solution: Stop after the third failed compile attempt, record `[BLOCKED BY DEPENDENCY]`, and do not edit cross-domain systems outside the drone transaction assignment.
Rejected Alternatives: Patching submarine/VR/metabolism systems from this agent would cross domain boundaries and risk architectural sabotage. Re-running dotnet without code changes would violate the no-spam build rule.
Scalability potential: SHINOBU_335 runtime path remains isolated; unrelated dependency walls do not change transaction math.
Hardware Impact: Runtime impact: 0 us. Developer hardware protected by stopping after three attempts.

## Decision 011 - Dispatcher Completion Discipline

Problem: The first transaction path scheduled `EvaluateDroneTransactionsJob` and immediately called `DispatcherJobSwap.TryComplete(..., true)`, creating a same-frame schedule/readback loop under load.
Solution: Store the scheduled `JobHandle`, attempt non-blocking completion at the next service-drain owner phase, and apply results only after the dispatcher wrapper reports completion. Force completion is kept only in shutdown/release.
Rejected Alternatives: Immediate `.Complete()` or forced readback would serialize the main thread. A private background queue would invent a second dispatcher.
Scalability potential: Low devices can let the legacy service path cover frames where the transaction buffer is still busy; Middle/High/Ultra complete more often within the natural frame gap without changing truth layout.
Hardware Impact: Removes a main-thread fence. Estimated low-end saving is 15-120 us on congested frames, with zero change to repair/inventory authority.

## Decision 012 - False-Sharing Counter Padding

Problem: Seven adjacent `int` counters were mutated by parallel workers through `Interlocked.Add`; all counters fit in one cache line and could fight through MESI invalidation.
Solution: Replace `NativeArray<int>` counters with explicit 64-byte `DroneTransactionCounterDTO` rows and atomically mutate only `Value@0`.
Rejected Alternatives: Per-thread NativeArrays add aggregation work and private allocation pressure. Plain `int[7]` is cache-hostile under parallel mining/repair bursts.
Scalability potential: Low/Middle get stable atomic cost under bursts; High/Ultra can raise transaction count without counter-line ping-pong.
Hardware Impact: Wastes 420 bytes of padding to protect L1 lines. Expected gain on i3/MX350-class CPUs: 3-20 us during 50-drone mixed bursts.

## Decision 013 - Vault-Only Transaction Buffers And Command Snapshots

Problem: Transaction buffers used the general drone fallback allocator and the Burst job could read `DroneServiceCommand` queue rows that the owner later clears/reuses.
Solution: Resolve SHINOBU_335 buffers only through `GlobalDataVault` (`12873350..12873357`) and snapshot service metadata plus AUP rows into 64-byte transaction DTOs before scheduling. The initial `70278..70284` candidate was rejected after exact scan because SavePersistence already owns it.
Rejected Alternatives: Retaining H8Memory fallback would keep private memory ownership for cross-domain transaction rows. Reading service queue rows after scheduling risks stale command data.
Scalability potential: Low through Ultra keep the same buffer identity and rollback-compatible row layout; capacity/cadence can scale without changing DTO ABI.
Hardware Impact: Adds 98 KB command snapshot capacity for 1536 commands, but removes queue-read hazards and avoids allocator fragmentation.

## Decision 014 - Async Stale-Result Fence

Problem: Once completion is deferred, a drone can retarget or lose its module/source before an old result is applied.
Solution: Validate `TargetEntityHash` against the current `BaseModule` runtime id for repair and the current mining source hash before applying owner-phase mutations.
Rejected Alternatives: Blindly applying stale repair/inventory deltas would violate one-fact ownership. Cancelling all pending transactions on any retarget would throw away valid completed work.
Scalability potential: Quality affects VFX only; target fences protect gameplay truth across all device tiers.
Hardware Impact: One integer hash compare per completed result. Estimated cost below 0.05 us for 50 results, cheaper than a scene/object lookup.

## Decision 015 - BufferID Collision Repair

Problem: Focused scan proved `70278..70284` is already owned by SavePersistence (`SaveMerkle*` and `SaveVoxelDeltaSchemaBytes`) in `H8Memory.BufferID`.
Solution: Move SHINOBU_335 local transaction lanes to unused numeric range `12873350..12873357` and update ledger/report/log references.
Rejected Alternatives: Keeping colliding IDs would corrupt Vault ownership. Editing SavePersistence enum or reusing SHINOBU_334 local `70265..70275` would widen cross-domain blast radius.
Scalability potential: Buffer identity is stable across Low/Middle/High/Ultra; quality only affects VFX signal admission.
Hardware Impact: Runtime cost: 0 us. Prevents data aliasing between save persistence and drone transaction buffers.

## Decision 016 - Inventory Cold Bind And Repair No-Op Fence

Problem: `TryResolveDroneInventoryTransactionBuffers` could call the cold `SoaInventoryQueryEngine.EnsureVaultBuffers` path from the service-drain transaction route if inventory handles were not already bound. Repair no-op results also collapsed "already complete" and "not at target yet" into the same return-to-hub behavior.
Solution: Cache the `IDataVault` pointer during cold `TryBindDroneInventoryVaultHandles`, make hot inventory resolve use only cached handles and fail closed without allocation or local route-state mutation, and accept mining transactions only when the SoA inventory route is already available. Repair no-op now returns to hub only when `FlagCompleted` is also present; an AUP distance miss leaves the drone state untouched for the next owner tick.
Rejected Alternatives: Rebinding inventory vault handles in the hot service-drain path would violate GlobalRegistry/DataVault cold-route law. Blindly sending every no-op repair drone to hub loses valid work when target AUP is temporarily stale or not yet reached. Forcing mining into the transaction job with default inventory arrays would consume commands without a valid SoA mutation route.
Scalability potential: Low devices avoid late allocation/rebind spikes and keep mining on the existing owner route until SoA is ready. Middle/High/Ultra keep the same gameplay truth route; richer VFX admission remains quality-driven and independent from inventory availability.
Hardware Impact: Prevents one cold vault allocation/resolve branch from entering service-drain frames and avoids wasted repair-drone round trips. Estimated low-end saving is 2-15 us on first mining-service frames with missing SoA handles, plus correctness protection for AUP no-op frames.
Superseded: Decision 019 keeps the cached resolve helper for telemetry only and routes mining commits through the PlayerInventory owner signal.

## Decision 017 - Late Result Clamp

Problem: Once SHINOBU_335 results are applied asynchronously, the owner state may have advanced through legacy fallback work before an old result is consumed. A staged repair row could over-apply against the live `BaseModule`, and a staged mining progress row could regress `HeadlessDroneState.TransactionProgress`.
Solution: In owner-phase repair apply, clamp `RepairAppliedMilli` against the current `BaseModule.CurrentIntegrity` and `MaxRecoverableIntegrity` before applying, publishing VFX, or consuming solder. In mining apply, non-completing result progress is accepted only if it is ahead of the current owner progress; completed inventory results still return the drone after target-hash validation.
Rejected Alternatives: Trusting the Burst-staged `NextIntegrityMilli` as current truth would split ownership from `BaseModule`. Blocking for job completion to preserve order would reintroduce a main-thread fence. Cancelling every pending result after any fallback frame would discard valid completed work and increase service jitter.
Scalability potential: Low devices can tolerate longer job completion latency without corrupting repair/inventory truth. Middle/High/Ultra still use the same DTO/authority route; quality continues to affect only VFX admission and debug/telemetry cost.
Hardware Impact: Per completed repair result adds two clamps and one min, below 0.05 us for 50 results on target low-end CPU. It prevents over-repair, wasted solder consumption, and extra VFX signals from stale rows.

## Decision 018 - Black-Box Heartbeat And Zero-GC Histogram

Problem: The telemetry ring only advanced on completed transaction jobs, leaving idle and fallback-only owner frames absent from the 300-frame black box. The editor tuner histogram also rebuilt managed strings every `EditorApplication.update`, which violated the Task 16 zero-GC histogram intent.
Solution: Add `RecordDroneTransactionOwnerFrame` at the end of the service-drain owner phase. It skips live jobs, suppresses duplicate same-frame writes with `s_DroneTransactionLastTelemetryFrame`, clears counters, and records a zero-transaction heartbeat row with active inventory slot count when available. Replace the editor histogram label/string builder with precreated UI Toolkit bar elements whose widths are mutated at a throttled 4 Hz refresh.
Rejected Alternatives: Recording only on job completion makes post-mortem analysis blind during idle/fallback frames. Calling a blocking completion just to fill telemetry would violate dispatcher discipline. A text histogram via `StringBuilder` or string concatenation is acceptable for one-shot reports but not for the live tuner histogram requirement.
Scalability potential: Low devices get cheap one-row heartbeat coverage without job fences; Middle/High/Ultra keep the same truth route while the editor facade can show more frequent visual inspection without runtime cost. Quality weight remains telemetry and VFX admission data only, never gameplay authority.
Hardware Impact: Runtime heartbeat cost is one 64-byte ring write plus seven 64-byte counter clears only when no transaction job is live; estimated below 2 us on i3/MX350-class hardware. Editor allocation churn from histogram rebuilds is removed; summary text remains throttled and editor-only.

## Decision 019 - Forensic Race Repair And Inventory Owner Route

Problem: Sidecar review found four correctness defects: the transaction job could read live `s_DroneStateDtos/s_DroneTargetDtos` while the next headless frame wrote them, mining directly mutated `SoaInventoryQueryEngine` mirror buffers owned by `PlayerInventory`, parallel empty-slot inventory CAS was nondeterministic, and missing AUP data failed open.
Solution: Add transaction-owned `DroneTransactionAupSnapshotDTO` rows in Vault ID `12873357`; copy current/target AUP in the owner phase before scheduling; make `IsDroneAtTarget` fail closed without a valid snapshot and matching target hash; remove direct inventory mirror mutation from the Burst job; publish mining completion through `ItemAcquiredSignal.SourceKind=DroneMining`; and make `PlayerInventory` consume that source through its existing owner add path. Also defer repair/mining fallback while a prior transaction job is still live to prevent duplicate owner-phase work.
Rejected Alternatives: Chaining the transaction job directly into every future headless writer would require broad dispatcher surgery outside this domain. Keeping mirror CAS violates one-fact ownership and can be overwritten by the next inventory snapshot. Letting fallback run during a pending transaction can double-apply repair or mining. Treating missing AUP as "arrived" authorizes remote work at 100 km scale.
Scalability potential: Low devices may defer a service frame while a previous transaction job is still live; Middle/High/Ultra naturally complete inside the frame gap. Quality remains a continuous VFX/telemetry scalar only and does not alter BufferIDs, item identity, repair authority, or PlayerInventory ownership.
Hardware Impact: Adds one 64-byte AUP snapshot row per admitted command. Removes direct contention on inventory mirror cells and prevents undefined job safety races; estimated low-end cost is below 1 us for 50 snapshots, with correctness replacing an unbounded data-race failure mode.

## Decision 020 - Active-Slot Telemetry Fence And Zero-Budget Repair No-Op

Problem: Loop 13 source read found two remaining precision defects. First, an already-complete repair task staged with `RepairBudgetMilli=0` was marked `InvalidInput` in `EvaluateRepair`, so the owner could leave a finished drone at the module instead of returning it to the hub. Second, SHINOBU_335 still cold-bound the full `SoaInventoryQueryEngine` lane set for telemetry, which was broader than needed after mining commits moved to the `ItemAcquiredSignal.SourceKind=DroneMining` owner route.
Solution: Treat zero repair budget as a read-only CAS probe: if the staged integrity is already at cap, emit `FlagNoop | FlagCompleted`; otherwise emit invalid input. Narrow inventory telemetry binding to the existing `ShinobuInventoryActiveSlotCount` handle only during cold transaction allocation, using `TryGetGenerationHandle`; hot telemetry reads use only cached `IDataVault.TryReadHandle` and fail closed if the optional handle was not cold-bound. SHINOBU_335 no longer calls `SoaInventoryQueryEngine.EnsureVaultBuffers`, `TryResolveVaultBuffers`, or opens item hash/quantity/durability arrays. `SignalBus<ItemAcquiredSignal>` is now cold-prewarmed in `EnsureDockingSignalLanes` to prevent the first drone mining award from allocating lane storage from the owner apply path.
Rejected Alternatives: Leaving zero-budget repair as invalid would corrupt service behavior for already-restored modules. Opening full inventory SoA views for a single telemetry scalar would expand cross-domain read scope without authority value. Calling `SignalBus<ItemAcquiredSignal>.Push` without cold lane prewarm can still work, but it risks lazy lane initialization at the first gameplay award.
Scalability potential: Low devices avoid unnecessary inventory lane resolution and first-award signal-lane allocation; Middle/High/Ultra keep the same owner route and can still consume richer telemetry through PlayerInventory's own black-box lanes. Quality remains VFX/telemetry-only and does not change repair/inventory truth.
Hardware Impact: Removes three large inventory array handle opens from SHINOBU_335 telemetry reads and replaces them with one 4-byte active-count read. Estimated low-end saving is 1-5 us on telemetry frames plus avoided cold allocation hitch on the first drone mining award.

## Decision 021 - Service Command Freshness And AUP Source Fence

Problem: Sidecar review found three post-polish gaps. A service command generated before `CompleteScheduledDroneServiceTransactionBatch(false)` could be consumed after the previous transaction returned the drone to hub. `WriteDroneTransactionAupSnapshot` copied `TargetEntityHash` from the same transaction task it later compared against, so the AUP snapshot did not prove current DTO ownership. Fallback mining published only logistics telemetry and an inventory sort command, leaving the PlayerInventory owner without the `ItemAcquiredSignal.SourceKind=DroneMining` award route used by Burst completion.
Solution: `PrepareDroneServiceTransactions` now re-reads the current `HeadlessDroneState` after previous transaction completion and admits only matching drone ids still in `HeadlessDroneRuntimeState.Repair`. `WriteDroneTransactionAupSnapshot` resolves expected task kind from the transaction type hash, validates `DroneStateDTO.CurrentTargetHashID`, `DroneTargetDTO.TaskHash`, `TaskKind`, current owner state, target task index, and a target hash derived from the DTO before setting `FlagValid`; invalid rows fail closed in Burst. Fallback mining now calls `PublishDroneMiningItemAcquiredSignal` before its logistics telemetry signal, so transaction and fallback paths share the same PlayerInventory authority route.
Rejected Alternatives: Trusting the service queue after applying old results would consume stale work. Self-assigning the snapshot target hash from the task row proves only that the task row matches itself. Making fallback mining mutate inventory directly would reintroduce the rejected second inventory owner and mirror-race class.
Scalability potential: Low devices may skip one transaction frame when DTO/source ownership is stale, but no gameplay fact is duplicated or lost. Middle/High/Ultra use the same fences; quality still affects VFX admission and telemetry only, never inventory truth or repair ownership.
Hardware Impact: Adds one native state read and a handful of integer/hash checks per admitted command; estimated below 0.5 us for 50 service commands on i3/MX350-class hardware. It prevents stale repair/mining application and restores fallback mining awards without any GC or scene/object work.

## Decision 022 - Mining DTO Source Synchronization Before Service Drain

Problem: The assignment job can write `DroneTargetDTO.TargetModuleId = bestTask.ModuleIndex` for `MineNode` assignment rows while the canonical mining source in `HeadlessDroneState` is the target-position hash unless a real source id exists. `SyncManagedTaskReference` previously returned on `module == null` without clearing stale `drone.TargetModuleId`, and no DTO mirror was forced before `DrainDroneServiceCommandQueue`. A valid mock-mining drone could therefore reach service state with a DTO placeholder target id, making the Loop 14 AUP/source fence reject the transaction every frame.
Solution: When a task has no managed `BaseModule`, `SyncManagedTaskReference` now clears `drone.TargetModuleId = 0`. During `ApplyCompletedHeadlessServices`, repair/attack service states now call `MirrorDroneSoA(slot, in drone)` immediately after the managed task reference sync and before service command drain. For mining, this overwrites the placeholder assignment DTO with the current owner task kind, target AUP, target position, and `TargetModuleId=0`, so `PrepareMiningTransaction` and `WriteDroneTransactionAupSnapshot` derive the same position-hash target.
Rejected Alternatives: Weakening the AUP source fence would re-open remote/stale mining writes. Teaching the Burst transaction job to accept assignment placeholder ids would let task indices masquerade as mining source identity. Adding a managed mining object/source owner would violate the current mock signal route and expand cross-domain coupling.
Scalability potential: Low devices pay one extra DTO mirror only for service-state drones; Middle/High/Ultra keep the same transaction math and VFX quality curve. Quality still does not change resource identity, DTO layout, repair truth, or PlayerInventory ownership.
Hardware Impact: The extra mirror writes two 64-byte DTO rows for drones already in repair/mining service state, estimated below 1 us for 50 active service drones. It removes an unbounded mining stall and avoids fallback-only awards under normal transaction availability.

## Decision 023 - Stale Result Fence And Deterministic VFX Seed

Problem: The AUP snapshot fence compared `DroneTargetDTO.TaskHash` and `DroneStateDTO.CurrentTargetHashID` to the transaction type hash, even though those fields are also written by navigation/assignment paths with launch or state hashes. That could reject valid owner-state service tasks after a legal DTO writer touched the row. Deferred result apply also accepted an old repair/mining result if the slot and drone id still matched, even after the current task kind or runtime state changed. Welding spark admission used `Time.frameCount` through the Burst job `Frame` field, making VFX flags vary across rollback/replay offsets.
Solution: `WriteDroneTransactionAupSnapshot` now validates the stable owner proof only: expected task kind, current `s_DroneTaskKindsBySlot`, owner drone id/state, non-empty owner target task index, matching `DroneTargetDTO.TaskIndex`, and a target hash derived from the current target DTO. `ApplyDroneTransactionResults` now requires the current drone to still be in `HeadlessDroneRuntimeState.Repair` and the current task kind to match the result type before owner mutation. `EvaluateDroneTransactionsJob` no longer consumes a Unity frame field for spark sampling; repair VFX seed is derived from the deterministic command snapshot (`StateHash`, target hash, task type hash, drone id) plus repair/cap result seed.
Rejected Alternatives: Keeping mixed hash comparisons would treat assignment-state metadata as transaction identity. Blind slot/drone result apply can mutate a retargeted drone. Using Unity frame count for spark sampling is acceptable for editor telemetry only, not for deterministic result flags that feed VFX signal counters.
Scalability potential: Low/Middle/High/Ultra keep identical repair and inventory truth. `GlobalQualityWeight` still continuously scales only spark admission and intensity; deterministic seed choice does not change the quality curve or gameplay authority.
Hardware Impact: Adds one state byte check and one task-kind check per completed result, below 0.05 us for 50 rows. Removes rollback-dirty VFX variability with no extra memory traffic; seed data already lives in the 64-byte command snapshot.
