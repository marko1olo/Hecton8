# Rationale_1305 - MEMORY_SOVEREIGN_WORLD_PAGING_EXORCIST

## Phase 0 Decisions

Problem: The prompt names `Assets/Project/Scripts/World/Streaming`, but the repository uses `Assets/_Project` and the canonical strict folder contains only an asmdef plus assembly marker.
Solution: Treat `Assets/_Project/Scripts/World/Streaming` as the literal first-party path equivalent and run strict Roslyn proof there first. Separately inspect streaming-adjacent source under `Assets/_Project/Scripts/World` without mutating it until a critical domain-placement justification exists.
Rejected Alternatives: Blindly editing all of `Assets/_Project/Scripts/World` would violate the strict folder mandate. Pretending the missing `Assets/Project` path exists would produce a fake report.
Scalability potential: Low/Middle/High/Ultra unchanged until real resident buffers are migrated; no runtime method is changed by this decision.
Hardware Impact: 0 us runtime. Prevents wrong-file surgery on i3/MX350 and top-tier hardware alike.

Problem: Native alias detection by text search cannot distinguish job parameters, locals, core memory owners, and persistent class fields.
Solution: Use the existing `Tools/VaultNativeAliasRoslynAudit` AST scanner as the primary proof artifact, then narrow findings to strict folder and streaming-adjacent files. Cross-check the result with two read-only subagent audits.
Rejected Alternatives: `rg NativeArray<` as proof; it produces false positives for job structs and method-local resolved views.
Scalability potential: Static-only; protects all quality weights because memory ownership must not vary by tier.
Hardware Impact: 0 us runtime. Removes stale-pointer risk only after offenders are migrated.

Problem: The strict folder has zero runtime owner files, while active world paging/residency code lives in `Assets/_Project/Scripts/World`.
Solution: Stop Phase 0 at evidence collection and log the mismatch as an implementation blocker. Do not move or rewrite broad world files until the next phase explicitly accepts the domain exception.
Rejected Alternatives: Creating a new manager in the empty strict folder would duplicate authority. Editing adjacent files under an implicit assumption would violate domain boundary enforcement.
Scalability potential: Low tier keeps current behavior; Middle/High/Ultra unchanged. No fake architecture split is introduced.
Hardware Impact: 0 us runtime.

Problem: `TerrainChunkPagerRuntime` keeps 19 raw DataVault-derived pointers as class fields and locks vault buffers for the lifetime of the runtime.
Solution: Classify the pager migration as a contract refactor: remove persistent raw pointer fields, resolve phase-local `NativeArray<T>` views, redesign the worker thread staging/request/result/dump leases, and convert raw pointer Burst job signatures in `TerrainChunkPagerTypes.cs`.
Rejected Alternatives: Replacing pointer fields with `NativeArray<T>` fields would still be persistent aliasing. Unlocking buffers without worker lease redesign risks stale pointers and data races.
Scalability potential: Low uses cheapest phase-local views and minimal copy windows; Middle keeps normal cadence; High/Ultra spend saved memory safety margin on larger residency windows and richer telemetry without changing truth ownership.
Hardware Impact: Estimated 0 us saved until implemented. Expected low-end gain after implementation is reduced stall/crash risk, not deterministic frame-time savings.

Problem: `WorldChunkResidencyManager` stores vault-resolved `NativeArray<T>` views as long-lived fields and also owns direct persistent `NativeParallelHashMap`, `NativeQueue`, and `NativeList` fields.
Solution: Phase 1 must retain `VaultGenerationHandle<T>` fields for existing BufferID-backed arrays, add scoped resolve helpers, and migrate direct maps/lists/queues to DTO arrays/ring buffers only after public consumers and job contracts are wrapped.
Rejected Alternatives: Removing all native fields in one pass would break jobs, HLOD renderer binding, PDA map reads, and load queue semantics. Keeping direct collections as undocumented exceptions violates the ownership doctrine.
Scalability potential: Low uses compact state arrays and bounded queues; Middle uses normal budgets; High/Ultra scale capacity and telemetry density by `GlobalQualityWeight` while keeping DTO layout and authority route fixed.
Hardware Impact: Estimated 0 us saved in Phase 0. Future i3/MX350 impact is lower allocator pressure and fewer relocation hazards during sector transitions.

Problem: Existing telemetry does not match the requested single 64B streaming black-box route.
Solution: Record exact state: pager has `PagerTelemetryEntry` at 64B x 300, residency has `ChunkResidencyTelemetryEntry` at 72B x 300, and dump routing must target `Docs/AgentLogs/Dump_1305_Streaming.bin` in the implementation phase.
Rejected Alternatives: Allocating a private telemetry ring in a MonoBehaviour or relying on `Debug.Log` on crash.
Scalability potential: Low records minimal flags/hashes at fixed capacity; Middle records normal state; High/Ultra may add richer optional telemetry through separate vault-owned lanes without changing the critical ring layout.
Hardware Impact: New 64B x 300 ring cost would be 19.2 KB. Runtime savings in Phase 0: 0 us.

Problem: Compile verification is mandated, but project rules forbid `dotnet build` when CPU load is above 50 percent or compiler/build processes are active.
Solution: Sample CPU/build state before build. CPU was 83 percent and no build processes were active; compile verification remains pending.
Rejected Alternatives: Running a build under prohibited CPU load, or claiming a compile pass without running it.
Scalability potential: Not runtime-relevant.
Hardware Impact: 0 us runtime.

Problem: The original scanner missed `NativeParallelMultiHashMap`, leaving `_chunkSpatialLookup` invisible to the automated report.
Solution: Add `NativeParallelMultiHashMap` to `Tools/VaultNativeAliasRoslynAudit/Program.cs` native collection coverage. This is a scanner source correction only; no rebuild/rerun was performed in this pass.
Rejected Alternatives: Continuing to document `_chunkSpatialLookup` as a permanent blind spot after identifying the exact scanner gap.
Scalability potential: Static-only. Improves future proof quality for all tiers.
Hardware Impact: 0 us runtime.

Problem: The terrain pager blackbox dump path still used a legacy `Dump_SHINOBU_245.bin` route and version.
Solution: Change `TerrainChunkPagerRuntime.cs` dump constants/comments to `Docs/AgentLogs/Dump_1305_Streaming.bin`, version `1305`, and `BLACKBOX_DUMP_1305_STREAMING`.
Rejected Alternatives: Leaving crash dumps under another agent identity, or claiming blackbox compliance in docs only.
Scalability potential: Low/Middle/High/Ultra unchanged; crash evidence route is identity/forensics, not fidelity.
Hardware Impact: 0 us runtime. Only path/version constants changed.

Problem: AUP audit found a direct absolute-to-float fallback in HLOD payload creation.
Solution: Record `WorldChunkResidencyManager.cs:5066-5068` as an active defect. Do not patch blindly because `StreamingHlodImpostorPoint.Center` is a Core contract and public layout change requires owner review.
Rejected Alternatives: Changing a Core contract layout from this agent's domain, or ignoring the direct float cast.
Scalability potential: Low suffers precision loss first at distance; High/Ultra visual-overkill cannot rescue corrupted authority. Correct fix is camera/player-relative local delta before float presentation.
Hardware Impact: 0 us runtime until fixed.

Problem: Residency fail-closed dumps still used three legacy file identities while the agent prompt requires `Docs/AgentLogs/Dump_1305_Streaming.bin`.
Solution: Point `DumpRelativePath`, `BackpressureDumpRelativePath`, and `HlodDumpRelativePath` in `WorldChunkResidencyManager.cs` to the single 1305 streaming dump artifact. This keeps post-mortem evidence under the correct owner without changing hot-path behavior.
Rejected Alternatives: Leaving split legacy dump names, or documenting compliance while fail branches still write to old paths.
Scalability potential: Low/Middle/High/Ultra unchanged. Dump route identity is forensic, not quality-tier behavior.
Hardware Impact: 0 us runtime in normal frames; only fail-path path constants changed.

Problem: HLOD payload creation converted absolute chunk AUP to `float3`, causing precision loss and violating the local-delta rule.
Solution: Require `_lastPlayerAup`, compute `originX/Y/Z` from it in double, then write `center = absolute - origin` before the final float cast. If no player origin exists, fail closed and dump HLOD telemetry.
Rejected Alternatives: Direct float cast of absolute coordinates; changing the public `StreamingHlodImpostorPoint` layout from this domain; silently emitting a zero center.
Scalability potential: Low devices avoid jitter from far-origin float loss; Middle/High/Ultra can spend stable local presentation on richer HLOD/impostor visuals without changing truth ownership.
Hardware Impact: Estimated low-end cost is three double subtractions per HLOD resolve on a non-core hot path; expected frame impact below measurement noise, with precision correctness recovered.

Problem: The previous HLOD correction still allowed authoring `Vector3 absoluteCenterMeters` to act as the payload center source before subtracting origin.
Solution: Force HLOD payload center to come from `_chunkCenters[index]` (`AbsoluteUniversePositionBlit`) and verify `_chunkIds[index] == chunkId` before reading it. `HLOD_ImpostorDTO.CenterXZ` now stores `centerAup.LocalX/Z`, not authoring absolute X/Z.
Rejected Alternatives: Treating a serialized `Vector3` as authoritative AUP, or falling back to zero/absolute center when native state is missing.
Scalability potential: Low devices get stable local impostor positions near far sector boundaries; High/Ultra can increase impostor density without amplifying float-origin jitter.
Hardware Impact: Fail path adds bounds/hash checks only during payload/initialization routes. Normal frame impact is below measurable threshold without profiler proof.

Problem: Prefab activation spawned chunk GameObjects at `definition.absoluteCenterMeters`, directly pushing authoring absolute float coordinates into scene space.
Solution: Add `TryResolveChunkScenePosition`, resolve `_chunkCenters[index] - _lastPlayerAup` in double, cast only the local delta to `Vector3`, and fail closed with telemetry dump if native center/origin state is invalid.
Rejected Alternatives: Continuing absolute float spawn; broad floating-origin subsystem rewrite from this task; spawning at default origin on failure.
Scalability potential: Low/Middle avoid precision damage during high-speed sector transitions; High/Ultra can use larger residency windows with the same local projection rule.
Hardware Impact: Three double subtractions per prefab activation. Activation is already async/sliced; no Tick cost added.

Problem: The Roslyn proof artifact was stale after adding `NativeParallelMultiHashMap` coverage.
Solution: Run the lightweight audit tool once for strict and world roots. Strict post-scan remains 0 offenders; world post-scan now reports 445 forbidden candidates and includes `_chunkSpatialLookup`.
Rejected Alternatives: Leaving Task 20 as a stale pre-patch report, or launching a full Unity/solution build against the user's explicit build-throttling instruction.
Scalability potential: Static-only; improves evidence quality for all tiers.
Hardware Impact: 0 us runtime. The audit spawned `VBCSCompiler`; no further build/dotnet commands are allowed while CPU remains high.

Problem: Runtime spatial methods still read serialized `definition.absoluteCenterMeters` for biome fallback depth, active biota containment, and additive-scene hydration distance.
Solution: Route those reads through `_chunkCenters[index]` as `AbsoluteUniversePositionBlit`, then compute depth or deltas in double precision. Add `TryReadNativeChunkCenter` helpers that validate native center availability without writing duplicate dumps; callers own the specific fail-closed telemetry route.
Rejected Alternatives: Treating authoring `Vector3` as runtime authority, broad migration of all chunk definitions in one pass, or silently falling back to authoring data when native center state is missing.
Scalability potential: Low devices avoid far-origin float drift during streaming decisions; Middle/High/Ultra can expand residency and visual proxy budgets without changing truth ownership.
Hardware Impact: Three double axis reads/subtractions in non-core streaming decision routes; no Tick loop allocation and no collection growth.

Problem: Several private helpers named `Resolve*`/`TryResolve*` mutated sequence counters, allocated or ensured buffers, consumed per-frame budgets, or wrote telemetry. That violates the project read-accessor doctrine even when the methods are private.
Solution: Rename mutating methods to `AdvancePagerReadRequestId`, `EnsureStreamingLedgerBuffers`, and `ConsumeLoadDispatchBudget`; rename snapshot/build/select helpers to `TryCapture*`, `TryBuild*`, and `TrySelect*`. This is semantic hardening only; no public contract was changed.
Rejected Alternatives: Leaving false accessor names as "private enough"; renaming public pager accessors without worker/thread lease migration.
Scalability potential: Low/Middle/High/Ultra unchanged. The gain is route clarity: read methods remain auditable and mutation points become explicit before the real vault migration.
Hardware Impact: 0 us runtime. Symbol names only.

Problem: The active diff still contained direct Unity-time and concrete save-manager fallback reads in `WorldChunkResidencyManager`, creating hidden horizontal/cold dependencies inside streaming code.
Solution: Route frame cadence through `SystemDispatcher.CurrentFrameIndex`, route time reads through `RuntimeNowSeconds()` backed by `SystemDispatcher.CurrentUnscaledTimeSeconds`, and use `GlobalRegistry.Save as IAsyncPersistenceService` instead of `Hecton8.SaveSystem.SaveManager.ActiveRuntimeInstance`.
Rejected Alternatives: Keeping direct `Time.frameCount`/`Time.unscaledTimeAsDouble` and concrete save runtime fallback; adding a new streaming-owned save dependency.
Scalability potential: Low/Middle/High/Ultra unchanged. Continuous quality budgets still scale through existing tuning, not binary switches.
Hardware Impact: 0 us expected; removes Unity API/cold concrete dependency from the audited runtime path.

Problem: Forensic comments still referenced `SHINOBU_245` after dump paths had been moved to agent 1305. That corrupts post-mortem ownership.
Solution: Rename stale comments to `BACKGROUND_WORKER_IO_1305_STREAMING`, `COLD_BOOT_CONFIG_READ_1305_STREAMING`, and `BLACKBOX_DUMP_1305_STREAMING`; rerun stale-label scan with 0 hits.
Rejected Alternatives: Leaving a mixed-owner crash trail; changing binary dump schema again without need.
Scalability potential: No fidelity effect on any tier. This is evidence routing only.
Hardware Impact: 0 us runtime.

Problem: `TerrainChunkPagerRuntime.TryReadTuning`, `TryReadCounters`, and `TryGetDebugCell` were public read accessors but read through persistent `_tuningPtr`, `_countersPtr`, `_metadataPtr`, and `_sectorCoordsPtr` aliases. `TryWriteTuning` wrote through `_tuningPtr` without a vault write lease.
Solution: Route the read accessors through `IDataVault.TryReadOnlyHandle` via `TryReadOnlyArray`. Route `TryWriteTuning` through `TryAcquireWriteLock` via `TryAcquireWriteArray` and release in `finally` through `ReleaseWriteArray`.
Rejected Alternatives: Leaving public diagnostics pointer-backed because the hot path still has deeper pointer debt; replacing all pager aliases in one text patch without redesigning worker queues and job fences.
Scalability potential: Low/Middle/High/Ultra unchanged. Tuning still preserves continuous `MaxQueuedLoads` and commit budget math; no binary quality switch was added.
Hardware Impact: 0 us expected in normal frames. Tuning writes are diagnostic/editor-style mutations and now pay one vault write-lock acquisition instead of a raw pointer store.

Problem: Pager Burst jobs accepted raw `void*` / `byte*` fields with `[NativeDisableUnsafePtrRestriction]`, making the job boundary incompatible with the vault view doctrine even after scheduling resolved `NativeArray<T>` buffers.
Solution: Convert `EvaluateChunkResidencyJob`, `EvictStaleChunksJob`, `CommitStagedChunkJob`, and `GenerateMockDiskLoadJob` to direct `NativeArray<T>` fields with `[NoAlias]` and `[ReadOnly]` where applicable. Runtime scheduling now resolves metadata and sector coordinate arrays immediately before scheduling and passes those views into the jobs.
Rejected Alternatives: Passing `VaultGenerationHandle<T>` into Burst jobs; leaving raw pointer fields until the full worker migration; deleting the unused commit/mock job types instead of making their signatures compliant.
Scalability potential: Low uses the same ring radius and queue budgets; Middle/High/Ultra retain continuous `GlobalQualityWeight` tuning. No binary tier switch was added.
Hardware Impact: 0 us expected for active evaluate/evict jobs; NativeArray indexers should Burst to direct memory access. `CommitStagedChunkJob` now loops byte copy if ever used, but current runtime does not instantiate that job.

Problem: Pager hot/counter paths still used direct pointer element access for tuning, counters, metadata, job load requests, sector coordinates, and telemetry writes after the job signature patch.
Solution: Route frame tuning, dispatch, worker result drain, counter reset, counter increments, telemetry write, cold profile tuning write, and editor gizmos through scoped vault views. Add `ClearFirstValue` and a NativeArray overload of `TerrainChunkPagerMath.HashMetadata`.
Rejected Alternatives: Removing pointer fields before redesigning the worker SPSC queues and byte slab IO; replacing the worker queue with managed collections; running a full build after every patch against the user's explicit throttle.
Scalability potential: Low/Middle/High/Ultra preserve the same continuous `GlobalQualityWeight` queue/ring-radius math. This pass changes access route only.
Hardware Impact: 0 us claimed savings. Expected cost is extra vault handle resolution and write-lock calls around counters/tuning/telemetry; active chunk math remains bounded and allocation-free.

Problem: After moving many pager routes to scoped vault views, dead pointer fields remained in class state and readiness checks, preserving false-positive native alias debt.
Solution: Delete persistent aliases for job load requests/counts, stale slots/counts, telemetry ring, tuning, counters, freed slots/count, and hardware profiles. Cache now keeps only lengths for those buffers; telemetry snapshot copies from a locally resolved telemetry array into the existing dump snapshot pointer.
Rejected Alternatives: Keeping dead pointer fields for convenience; removing active worker/byte-slab pointers before a queue and IO lease redesign exists.
Scalability potential: Low/Middle/High/Ultra unchanged. This reduces stale alias surface without changing fidelity budgets.
Hardware Impact: 0 us runtime savings claimed. It removes 10 persistent pointer aliases and associated readiness checks; active runtime behavior is otherwise unchanged.

Problem: Pager runtime still held active persistent `byte*` and worker queue pointer fields after pass 9, so the class could still retain physical DataVault addresses across defragmentation windows.
Solution: Remove the remaining pointer fields from `TerrainChunkPagerRuntime`; resolve worker request/result queues, staging bytes, compressed scratch bytes, CSV scratch, metadata/sector/active arrays, and telemetry dump snapshot bytes as transient `NativeArray<T>` views at the use site. Worker byte offsets now check slot and slab bounds before pointer arithmetic.
Rejected Alternatives: Replacing SPSC queues with managed collections; leaving worker IO pointers cached because the worker thread is inconvenient; running dotnet/Unity build after every patch despite explicit throttle.
Scalability potential: Low/Middle/High/Ultra preserve continuous `GlobalQualityWeight` queue and radius math. This pass changes memory alias lifetime only, not gameplay truth or fidelity tiers.
Hardware Impact: 0 us savings claimed. Expected cost is extra vault handle resolution in worker and dump routes; expected benefit is removal of all persistent raw pointer fields from the pager runtime.

Problem: Several streaming DTO layouts were explicit and 8-byte sized but not ordered by ARM64 access priority: byte flags preceded 2-byte padding in some structs, and `AddressablesRequestDTO` placed `ulong HandlePtr` after 4-byte fields.
Solution: Reorder explicit `FieldOffset` maps without changing struct sizes: `ChunkLoadRequest`, `AddressablesRequestDTO`, `ChunkResidencyDTO`, `ChunkHydrationApplyRecord`, `MockAssetHandle`, `MockAupShiftSignal`, and `WorldStreamingRuntimeTuning` now place 8-byte fields first, then 4-byte fields, then 2-byte fields, then byte flags/padding.
Rejected Alternatives: `Pack=1`; relying on C# declaration order despite explicit layout; changing public DTO names or buffer strides.
Scalability potential: Low devices avoid avoidable unaligned access penalties; Middle/High/Ultra retain the same buffer capacities and can spend saved stability margin on larger visual residency windows.
Hardware Impact: 0 us directly measured. Expected low-end gain is lower alignment-risk on ARM64/Quest-class CPUs, not a guaranteed frame-time reduction.

Problem: `WorldChunkResidencyManager` still held `_pagerReadTickets` as a persistent `NativeArray<H8WorldPageReadTicket>` field even though the buffer already had a fixed `BufferID`, fixed capacity, and a narrow request/retire access surface.
Solution: Replace `_pagerReadTickets` with `VaultGenerationHandle<H8WorldPageReadTicket>`, allocate it through `EnsureWorldStreamingVaultBuffer`, resolve a local `NativeArray` only inside `RequestAsyncPagerRead` and `RetireAsyncPagerReadTickets`, and release the handle through the existing vault-handle release path.
Rejected Alternatives: Keeping the field as a "small harmless" native array; moving pager tickets into a managed queue; rewriting the full residency manager in one risky pass.
Scalability potential: Low/Middle/High/Ultra unchanged. Pager retire budget still scales continuously via `ResolveSmoothGlobalQualityWeight01`.
Hardware Impact: 0 us saved. One extra handle resolution in request/retire paths; persistent native-field debt reduced by one without changing public contracts.

Problem: `WorldChunkResidencyManager` still carried small native fields that were either unused (`_chunkSpatialLookup`) or already had fixed BufferIDs and narrow single-purpose access surfaces (`_macroDatabaseEvictionScratch`, `_hydrationApplyRecords`, `_dehydrationMetadataPayload`).
Solution: Delete `_chunkSpatialLookup` and its cold fill/dispose code because no consumer reads it. Replace the three fixed buffers with `VaultGenerationHandle<T>` fields and resolve local views only in macro eviction, hydration record write, and dehydration metadata write paths.
Rejected Alternatives: Creating a vault-backed replacement for an unused spatial lookup; keeping small persistent arrays as exceptions; moving metadata payloads to managed byte arrays.
Scalability potential: Low/Middle/High/Ultra unchanged. Macro eviction tier, hydration copy budget, and threat retention behavior keep their existing continuous budget math.
Hardware Impact: 0 us saved. Removes four persistent native fields and one dead cold index build; adds local vault resolve calls in three non-core paths.

Problem: `WorldChunkResidencyManager` still held fixed BufferID-backed `NativeArray` fields for residency telemetry, Addressables load timing, HLOD impostor SoA, and chunk id/center SoA. These were stale-alias risks even though the data already had deterministic vault identities.
Solution: Replace those arrays with `VaultGenerationHandle<T>` descriptors, register them through `EnsureWorldStreamingVaultBuffer`, resolve local views at each read/write/job boundary, and remove `AcquireWorldStreamingArray` fallback ownership. Direct native maps/queue/lists remain because they require a separate DTO/ring redesign.
Rejected Alternatives: Keeping HLOD arrays as exceptions because public accessors use them; preserving fallback `H8Memory.Allocate<T>` arrays after a vault miss; rewriting maps/queue/list contracts in the same patch without compile proof.
Scalability potential: Low resolves the same fixed-capacity views and keeps current quality-weight budgets; Middle/High/Ultra can still scale HLOD and residency windows via existing continuous tuning without changing DTO layout or truth ownership.
Hardware Impact: 0 us saved claimed. Extra vault resolves occur in HLOD, telemetry, and residency scan paths; stale alias surface drops from 21 to 6 persistent native fields.

Problem: `WorldChunkResidencyManager` still owned six direct native containers: two hash maps for state/index lookup, one native load queue, two load/unload NativeLists, and one sort NativeList. These containers were outside `GlobalDataVault` and could not survive relocation/defrag as sovereign state.
Solution: Replace the maps with a 24B `ChunkStateSlotDTO` vault buffer keyed by streaming-table slot and scanned only up to `_chunkCount`. Replace the native queue with a fixed vault-backed `ChunkLoadRequest` ring plus scalar read/write cursors. Replace load/unload lists and the sort scratch with a 16B `ResidencyDecisionDTO` per-slot output buffer; dispatch now selects the highest-priority/nearest queued request by bounded ring scan instead of scheduling a separate `NativeList` sort job.
Rejected Alternatives: Managed `Dictionary`/`Queue`; leaving direct containers as documented exceptions; keeping the `NativeList` sort job after the output could be represented as a per-slot decision table. The sort job was deleted because a bounded queue scan is simpler, deterministic, and keeps priority without persistent list ownership.
Scalability potential: Low keeps the same fixed capacities and avoids extra allocator pressure; Middle keeps existing continuous load budget tokens; High/Ultra can increase serialized queue/chunk capacities and still use the same DTO layouts and vault route. No binary quality switch was added.
Hardware Impact: 0 us saved claimed without profiler. Expected low-end cost is bounded linear slot scans for state lookup/load selection; expected stability gain is removal of the last six non-vault native containers from residency. Load dispatch remains capped by `ResolveLoadDispatchBudgetPerFrame()` and concurrent-load caps.

Problem: `PredictiveChunkResidencyJob` in `ShinobuStreamingRuntime` still exposed `NativeList<int>.ParallelWriter` output lanes even though no call site used the job. This was not a persistent field, but it violated the current no-list static surface the user requested for the touched streaming runtime.
Solution: Replace hydration/dehydration list writers with a `NativeArray<ResidencyDecisionDTO>` output lane and write one deterministic per-index action record. The AUP formula remains `double3 deltaD = chunk.AUP_Center - CameraAup; float3 delta = (float3)deltaD`, so absolute AUP is never cast before the double subtraction.
Rejected Alternatives: Deleting the unused job; leaving list writers because they were not class fields; adding a managed request list. Keeping the job with a per-slot DTO preserves compile surface and zero-GC intent.
Scalability potential: Low/Middle/High/Ultra unchanged. Consumers can compact or scan decisions under the same continuous budgets.
Hardware Impact: 0 us measured. Expected cost is one indexed DTO write per chunk; allocator/list-growth risk is removed.

Problem: `TerrainChunkPagerRuntime` no longer stored persistent raw pointers, but it still called `TryLockBuffer` for every pager buffer during cold allocation and held those GlobalDataVault pins until teardown. That kept the defragmenter blocked for the full runtime.
Solution: Remove `LockVaultBuffers`, `UnlockVaultBuffers`, `TryLock`, `FailLock`, `UnlockLockedBuffers`, `_lockedVaultBuffers`, and `_lockedVaultMask`. Readiness now depends on `_validatedVaultBuffers` plus resolved buffer lengths from `CacheUnsafePointers`, which no longer caches physical pointers.
Rejected Alternatives: Keeping lifetime locks as a "safe" exception; removing locks and claiming worker proof. The worker still resolves views from a background thread, so phase/fuzzer proof remains explicitly pending.
Scalability potential: Low/Middle/High/Ultra unchanged in quality math. The change releases GlobalDataVault compaction eligibility instead of changing chunk cadence.
Hardware Impact: 0 us saved claimed. Removes runtime-long lock contention against DataVault defrag; adds no new hot allocation. Residual risk remains around worker thread view lifetime until a fuzzer proves or redesigns that lease.
