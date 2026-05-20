# Rationale_SHINOBU_202

## Decision 001 - Non-breaking kernel insertion first
Problem: The repository already has 1700+ references to VaultBufferHandle<T> compatibility fields and pointer-centric methods. A blind ABI rewrite would likely break unrelated systems before the generation kernel exists.
Solution: Insert a generation-safe API and flat metadata path inside GlobalDataVault first, then migrate consumers against a compiling core.
Rejected Alternatives: Immediate repo-wide deletion of pointer fields would create a compile wall from unrelated domains and block verification.
Scalability potential: Low tier gets one integer generation compare; middle/high/ultra can enable editor stress relocation and X-Ray diagnostics without shipping those costs.
Hardware Impact: i3/MX350 avoids hash lookup in the new resolve path; expected hot resolve saving is sub-micro per thousands of resolves depending on cache locality.

## Decision 002 - Flat metadata mirror, not managed dictionary
Problem: Existing metadata is stored in UnsafeHashMap keyed by BufferID, which is native but still not the direct BufferID indexed array required for the new handle contract.
Solution: Add a NativeArray<VaultBufferMeta> mirror sized for 100000 potential handles and update it whenever metadata mutates.
Rejected Alternatives: Managed Dictionary<int, ptr> is forbidden and would add GC/object indirection. Replacing the entire map in one pass risks breaking current allocator bookkeeping.
Scalability potential: Low uses direct O(1) metadata loads; high/ultra keeps defrag visibility and churn telemetry.
Hardware Impact: About 6.4 MB metadata at 64 bytes per entry; acceptable for core safety and cheaper than UAF crash recovery. i3/MX350 gains predictable cache access versus hash probes.

## Decision 003 - Metadata never enters rollback truth
Problem: Generation counters are local memory safety state and cannot be rewound by gameplay rollback.
Solution: Keep VaultBufferMeta in GlobalDataVault runtime memory only and do not expose it to StateRingBuffer/Merkle payload hashing.
Rejected Alternatives: Serializing generation into netcode snapshots would invalidate monotonic safety after resimulation.
Scalability potential: Same behavior across low, middle, high, ultra; no hardware fork.
Hardware Impact: Saves snapshot bytes and avoids rollback-time metadata churn on low-end CPU.

## Decision 004 - Legacy pointer handle quarantined instead of ABI demolition
Problem: Existing codebase stores and passes VaultBufferHandle<T> with pointer/length/stride fields in thousands of call sites, including jobs and editor utilities.
Solution: Add VaultGenerationHandle<T> as the 16-byte pointer-free ABI and route new safety work through it while preserving legacy handle compatibility for compile continuity.
Rejected Alternatives: Replacing VaultBufferHandle<T> in one pass would create an uncontrolled compile wall across unrelated domains.
Scalability potential: Low uses pointer-free descriptor resolve. Middle/high/ultra can migrate consumers incrementally and activate force-defrag stress without global breakage.
Hardware Impact: i3/MX350 gets predictable metadata loads on new path; legacy path remains quarantined until consumer migration.

## Decision 005 - Release validation is generation-only
Problem: Full validation of SystemID/type/bounds on every resolve adds ALU and branch cost.
Solution: Gate SystemID, type hash, and bounds checks behind ENABLE_UNITY_COLLECTIONS_CHECKS. Release path performs flat metadata load and generation compare.
Rejected Alternatives: Hardware-tier validation branches violate the no binary quality switch rule and make behavior nondeterministic by device.
Scalability potential: Low through ultra receive identical safety semantics; development builds buy forensic depth with editor-only cost.
Hardware Impact: Low-end CPU avoids hash/type checks in player builds; expected hot path is one indexed load plus one compare.

## Decision 006 - Writer authority stored in metadata
Problem: Global mutation guard masks do not expose which system owns a specific buffer write.
Solution: Add ActiveWriterSystemID to VaultBufferMeta and claim it with Interlocked.CompareExchange before resolving a write view.
Rejected Alternatives: Trusting scheduling convention only leaves data races invisible.
Scalability potential: Low fails fast. High/ultra can layer X-Ray and fault visualization over the same field.
Hardware Impact: Atomic cost occurs only on write lock acquisition, not read resolve.

## Decision 007 - Mock relocation uses metadata generation churn
Problem: Forcing real random memory movement inside a live arena can corrupt unrelated systems before consumer migration is complete.
Solution: Add PRE_SIMULATION-fenced GenerateMockVaultRelocationForValidation that deterministically bumps generations through a Burst job.
Rejected Alternatives: Unfenced random MemMove during simulation would be a deliberate UAF injector.
Scalability potential: Low runs few mutations; ultra can run larger mutation counts under the same continuous maxMutations parameter.
Hardware Impact: i3/MX350 can stress stale-handle rejection without moving megabytes of payload.

## Decision 008 - SHINOBU_202 dump is written on blocked UAF
Problem: Access violation prevention is useless without postmortem state.
Solution: On generation mismatch, record the current 300-frame ring and write Docs/AgentLogs/Dump_SHINOBU_202.bin.
Rejected Alternatives: Logging only a single stale handle loses pre-fault memory pressure and relocation context.
Scalability potential: Same dump format across all devices.
Hardware Impact: No steady-state cost; IO occurs only on a blocked fault.

## Decision 009 - Legacy pointer API is quarantined at resolve call, not demolished in one edit
Problem: Static scan now shows 1802 `VaultBufferHandle<T>` references and 270 raw pointer lease routes. A forced 16-byte rename of the legacy struct in one pass would create a repo-wide compile wall outside the Core memory lane.
Solution: Mark the legacy handle type as obsolete, add legacy bridge overloads that convert `VaultBufferHandle<T>` into pointer-free generation descriptors internally, and make legacy `.Resolve(...)`, `ResolvePointer(...)`, ref accessors, and tombstone helpers go through `IDataVault.TryResolveHandle(...)` before deriving any transient pointer.
Rejected Alternatives: Trusting `handle.ptr` during `.Resolve(...)` was rejected because it preserves UAF risk after relocation. Immediate global consumer rewrite was rejected because the blast radius crosses AI, Physics, UI, VFX, Construction, Rendering, and Modding in the same batch.
Scalability potential: Low keeps one indexed metadata load and generation compare. Middle/high/ultra can enable strict editor audits and force-defrag stress without shipping managed scanner cost.
Hardware Impact: i3/MX350 removes cached-pointer trust from the most common legacy `.Resolve(...)` call path without adding hash-map probes; expected steady cost remains one L1 metadata load plus one integer compare.

## Decision 010 - Pointer-retention scanner is editor/CI gated
Problem: "Do not keep long-lived pointers in managers" needs a repeatable source gate, not a one-time grep pasted into chat.
Solution: Add `VaultPointerRetentionScanner` under Editor. It writes `Docs/AgentLogs/VaultPointerAudit_SHINOBU_202.md` and can hard-fail editor load only when `HECTON_VAULT_POINTER_AUDIT_STRICT=1` is set.
Rejected Alternatives: Throwing by default on editor load would currently brick the workspace because the existing static debt is still above zero. A runtime scanner was rejected because managed filesystem IO and source parsing are forbidden in player hot paths.
Scalability potential: Runtime cost is zero across low, middle, high, ultra. CI can enforce the gate while player builds remain untouched.
Hardware Impact: No player hardware impact. Editor-only scan cost is O(source bytes) and is manually/CI triggered.

## Decision 011 - Memory pressure dump is tied to the existing heartbeat
Problem: Task 16 required dumping the 300-frame ring when memory exceeds 90% capacity, but the previous implementation dumped only on blocked UAF.
Solution: Add a heartbeat-side integer pressure check: `_allocatedBytes * 10 >= _arenaBytes * 9`, then reuse `DumpShinobu202BlackBox()`.
Rejected Alternatives: Floating-point `CapacityPressure01` in the branch was rejected to avoid extra conversion and property use inside the heartbeat.
Scalability potential: Same threshold across devices; arena size already scales continuously by requested limit and existing vault configuration.
Hardware Impact: One integer multiply/compare per heartbeat; no steady IO unless the 90% threshold is crossed.

## Decision 012 - Orphan sweep consumes an unmanaged live-owner table
Problem: Task 14 requires reclaiming Vault buffers whose owners died without ReleaseBuffer, but scanning Unity scenes or managed services inside the Vault would violate Global Authority and inject reflection/managed traversal into Core memory.
Solution: Add `SweepOrphanedHandles(...)` and Burst `SweepOrphanedHandlesJob`. Callers supply a `NativeArray<SystemID>` live-owner table during PRE_SIMULATION. The job marks scene-owned metadata rows missing from that table with `VaultMetaFlagOrphanCandidate`; the main Vault maintenance section then reclaims unlocked candidates, increments generations through tombstone/free, and packs candidate/reclaim counts into the 300-frame blackbox `Reserved32`.
Rejected Alternatives: Direct Unity scene traversal from GlobalDataVault was rejected because Core memory must not own scene hierarchy knowledge. Reclaiming locked or external-view buffers was rejected because a live job or bridge still has an alias.
Scalability potential: Low devices run the sweep rarely with a short live-owner table; high/ultra/editor can schedule broader owner audits during maintenance windows. Runtime resolve hot path is unchanged.
Hardware Impact: Sweep cost is O(metadata capacity + liveOwnerCount * activeRows) during PRE_SIMULATION only; zero player-frame pointer validation cost. On i3/MX350 the scan is maintenance-only and avoids UAF crash recovery.

## Decision 013 - X-Ray waterfall uses fixed UI Toolkit columns
Problem: The X-Ray facade exposed current block layout but did not show the 300-frame fault/pressure trend requested by Task 17.
Solution: Add a fixed 64-column `VaultTelemetryWaterfallElement` fed from `TryGetVaultTelemetrySnapshot(age, ...)`. Columns encode arena pressure by height/color and generation-mismatch deltas as red pulses.
Rejected Alternatives: Allocating textures or rebuilding VisualElements every refresh was rejected because editor diagnostics must not hide allocation churn. IMGUI repaint-only drawing was rejected because the task required UI Toolkit.
Scalability potential: Runtime cost is zero. Editor high-end machines get richer forensic visualization; low-end runtime players do not pay for it.
Hardware Impact: Editor-only fixed 64 VisualElements and fixed arrays; no player CPU/GPU impact.

## Decision 014 - Released BufferIDs keep a generation tombstone
Problem: Removing metadata on final release erased the generation epoch. A later allocation with the same BufferID could start at generation 1 and theoretically validate a stale generation-1 handle.
Solution: Preserve a tombstone generation in the flat metadata slot when metadata is removed. New allocations call `ResolveInitialGenerationForAllocation(key)` and start at the next epoch instead of resetting to 1.
Rejected Alternatives: Keeping a freed buffer in the active metadata map was rejected because it would look live to ownership scans. A managed per-BufferID dictionary was rejected because the flat native table already owns this fact.
Scalability potential: Low through ultra get identical safety. The hot resolve path stays one indexed metadata load plus generation compare; only allocation/release maintenance pays the epoch update.
Hardware Impact: One flat metadata read during allocation and one tombstone write during release. On i3/MX350 this is negligible compared with avoiding a stale handle access violation.

## Decision 015 - First Core manager migrated to pointer-free descriptors
Problem: The source audit found persistent legacy `VaultBufferHandle<T>` fields in managers. Leaving every manager on the pointer-bearing bridge would keep the enforcement story theoretical.
Solution: Migrate `BurstTokenBucketJobAdmissionService` from five persistent `VaultBufferHandle<T>` fields to five `VaultGenerationHandle<T>` descriptors. Each accessor resolves through `IDataVault.TryResolveHandle(...)` into a phase-local `NativeArray<T>`, and teardown calls `ReleaseBuffer` before clearing descriptors.
Rejected Alternatives: Rewriting all 1800+ legacy references in one pass was rejected as a cross-domain compile wall. Keeping this Core service on obsolete handles was rejected because it is small enough to migrate safely.
Scalability potential: Low devices remove cached pointer fields from a scheduling service. High/ultra/editor keep the same diagnostics because the underlying Vault telemetry is unchanged.
Hardware Impact: Hot path remains one descriptor BufferID guard plus the Vault generation compare. Teardown adds five cold-path release calls and prevents leaked references.

## Decision 016 - Data Monolith arena stops caching NativeArray views
Problem: `H8StaticDataArena` kept a static `NativeArray<byte>` view and three legacy Vault handles, then telemetry used `ResolvePointer`. A Data Monolith payload can be relocated by Vault maintenance; caching the native view defeats generation invalidation.
Solution: Store only `VaultGenerationHandle<byte>`, `VaultGenerationHandle<H8DataMonolithTelemetryEntry>`, and `VaultGenerationHandle<int>`. Every section, localization, hash, file-read, and telemetry route resolves a method-local `NativeArray<T>` through `TryResolveHandle`. Shutdown releases all three descriptors through `ReleaseBuffer`.
Rejected Alternatives: Keeping `_arena` and refreshing it opportunistically was rejected because it remains a long-lived alias. Migrating the entire Data Monolith compiler/editor was rejected because it is editor-owned static-data tooling and outside this Core runtime alias surface.
Scalability potential: Low devices keep the same zero-copy static-data reads with generation validation. High/ultra/editor retain Data Monolith telemetry dumps without raw pointer leases.
Hardware Impact: Runtime reads add one flat metadata generation compare when a public static-data route resolves the arena; this is cheaper than UAF recovery and does not allocate. Shutdown adds three cold release calls.

## Decision 017 - StaticDataStore telemetry stops leasing Vault pointers
Problem: `StaticDataStore` still resolved static-data and B-Tree telemetry rings through pointer-bearing Vault handles. Those rings are shared forensic buffers; a relocated Vault allocation would leave stale ring/cursor pointers in a manager.
Solution: Replace telemetry fields with `VaultGenerationHandle<T>` and add `TryResolveBlackBox` / `TryResolveBTreeTelemetry` helpers. Dump and record paths resolve method-local `NativeArray<T>` views immediately before use, then take read-only pointers only for synchronous dump writers.
Rejected Alternatives: Releasing the shared telemetry BufferIDs from `StaticDataStore.Shutdown` was rejected because `BabelDictionaryStore` and static helper contracts also use the same IDs; refcount ownership must be split before destructive release. Keeping `ResolvePointer` was rejected because the whole fault class is cached-pointer trust after relocation.
Scalability potential: Low devices pay one flat generation compare on cold telemetry paths. Middle/high/ultra keep 300-frame B-Tree and static-data forensic rings without persistent manager aliases.
Hardware Impact: Removed five pointer-bearing telemetry descriptors from the store. Runtime hot lookup remains B-Tree dominated; telemetry write/dump adds O(1) generation validation and no managed allocation.

## Decision 018 - Babel telemetry and fallback aliases are quarantined
Problem: `BabelDictionaryStore` had five telemetry `VaultBufferHandle<T>` fields, a Vault-backed `ERROR` span pointer, and a padded dictionary fallback pointer derived through `ResolvePointer`. The telemetry/error routes could be migrated safely; the padded dictionary has active lore read jobs and must not be moved while a raw pointer job reads it.
Solution: Convert telemetry and `BabelErrorUtf8` to `VaultGenerationHandle<T>` with local `TryResolveHandle` calls. Convert the padded dictionary fallback to `GetBuffer<byte>` so GlobalDataVault marks it as an external view and live defrag refuses to relocate that block while the raw pointer job path exists.
Rejected Alternatives: A partial generation handle around the padded dictionary was rejected because it would still leave `_basePointer` and scheduled pointer jobs stale after a generation bump. Rewriting the B-Tree/lore jobs to pure `NativeArray<byte>` in this pass was rejected as SHINOBU_207 ownership and too large for the Vault ABI migration lane.
Scalability potential: Low devices keep zero-copy Babel reads and a defrag-blocked fallback blob. High/ultra retain B-Tree telemetry and lore job throughput; the next owner pass can replace pointer jobs with NativeArray jobs for relocatable overkill.
Hardware Impact: Removed all legacy `VaultBufferHandle<T>` / `ResolvePointer` routes from `BabelDictionaryStore`. The padded fallback may block compaction for one dictionary blob, but avoids UAF on i3/MX350 and Quest-class hardware until the lore job is rewritten.

## Decision 019 - Core memory diagnostics stop exporting legacy handles
Problem: `VaultMemoryContracts`, `VaultLegacyBinaryArchaeology`, and `VaultProbeUtility` still exposed or consumed obsolete pointer-bearing Vault handle routes inside the Core memory lane. Even if the bridge resolves safely, leaving those APIs in diagnostics trains future code to persist `VaultBufferHandle<T>`.
Solution: Convert the sovereignty telemetry ring to `VaultGenerationHandle<T>`, convert memory-layout config hydration/writes to generation descriptors plus local `TryResolveHandle`, and replace the public diagnostic `TryGetHandle` helper with `TryGetGenerationHandle`.
Rejected Alternatives: Keeping an obsolete diagnostic wrapper was rejected because no callers existed and the API would reintroduce pointer-bearing manager state. A repo-wide signature purge was rejected because unrelated domains still own large legacy debt and require staged migration.
Scalability potential: Low devices keep the same flat generation compare. Middle/high/ultra editor diagnostics retain probe ability without exporting stale pointer descriptors.
Hardware Impact: Hot player paths are unchanged. Cold telemetry/config/diagnostic routes add no GC and avoid future cached-pointer misuse; i3/MX350 pays only the existing O(1) Vault metadata resolve when these routes execute.

## Decision 020 - Hardware thermal blackbox uses generation descriptors
Problem: `HardwareThermalService` persisted two `VaultBufferHandle<T>` fields and refreshed them through `ResolveBuffer`. This kept a Core runtime manager on pointer-bearing descriptors even though its thermal severity byte and blackbox ring are owned Vault buffers.
Solution: Replace both fields with `VaultGenerationHandle<T>`, resolve local `NativeArray<T>` views through `TryResolveHandle`, allocate through `GetGenerationHandle<T>`, and release descriptors through `ReleaseBuffer` during teardown or DataVault hot-swap.
Rejected Alternatives: Leaving this service on the legacy bridge was rejected because it is a small Core-owned manager with clear Vault ownership. Rewriting hardware policy/scalability behavior was rejected because the task is pointer safety, not thermal policy redesign.
Scalability potential: Low devices keep the same thermal sampling cadence and blackbox size. Middle/high/ultra preserve thermal diagnostics without cached pointer state; no binary quality branch was introduced.
Hardware Impact: i3/MX350 pays one O(1) generation compare when the severity byte or 300-frame blackbox is touched. Cold teardown now returns two Vault references instead of leaking them across service reloads.

## Decision 021 - SignalBus frame snapshots are phase-local views
Problem: `SignalBus<T>` kept a static `NativeArray<T> _frameSnapshot` alias and a legacy `VaultBufferHandle<T>`. Because every closed signal lane can survive across frames, this was a generic long-lived Vault alias at Core signal scale.
Solution: Remove the static `NativeArray<T>` field, convert `_frameSnapshotHandle` to `VaultGenerationHandle<T>`, resolve a local snapshot view in each read/flush/filter/sort path, refresh the descriptor with `TryGetGenerationHandle<T>` after generation churn, and release the descriptor on lane disposal.
Rejected Alternatives: Keeping a cached view for speed was rejected because the task is specifically to prevent stale relocatable aliases. Rewriting SignalBus policy, lane lists, or signal DTO layouts was rejected because those changes are outside the pointer-safety lane and already have unrelated working-tree edits.
Scalability potential: Low devices pay only the flat generation compare when signal snapshots are consumed or flushed. High/ultra keep deterministic snapshot sorting/coalescing and can tolerate Vault relocation stress without stale alias reuse.
Hardware Impact: One O(1) metadata compare replaces a long-lived alias hazard across all typed signal lanes. No GC is added; no interface array hot dispatch is introduced by this pass.

## Decision 022 - Alignment telemetry proves faults without legacy handles
Problem: `Arm64AlignmentTelemetry` used a static legacy Vault handle and three `.Resolve(vault)` routes for the diagnostic ring that records alignment faults. A stale diagnostic ring would weaken the exact evidence path used to prove ARM64 layout violations.
Solution: Convert the ring to `VaultGenerationHandle<AlignmentTelemetryEntry>`, resolve local views for record/read/dump, and release the old descriptor if the cached Vault instance changes before reacquiring the ring.
Rejected Alternatives: Leaving SHINOBU_204 diagnostic code untouched was rejected because it is a Core memory telemetry route and small enough to migrate safely. Adding a copied managed ring was rejected because diagnostics must remain Vault-backed and zero-GC in record/read paths.
Scalability potential: Low devices keep the same 300-entry ring and cold dump path. High/ultra/editor retain richer alignment fault proof without cached pointer handles.
Hardware Impact: Diagnostic record/read paths pay one flat generation compare and no GC. Vault hot-swap no longer leaks the previous alignment telemetry descriptor.

## Decision 023 - Simulation bucketer tables use generation descriptors
Problem: `ModuloSimulationBucketer` persisted eight legacy `VaultBufferHandle<T>` fields for front/work bucket tables, EWMA cost/load tables, rebalance scratch, frame state, and the 300-frame blackbox. Re-init/dispose cleared the handles locally without returning Vault references.
Solution: Convert all eight fields to `VaultGenerationHandle<T>`, allocate through `GetGenerationHandle`, resolve method-local `NativeArray<T>` views through `TryResolveHandle`, and release all non-zero descriptors through `ReleaseBuffer` after the pending rebalance job is completed.
Rejected Alternatives: Keeping the bucketer on the legacy bridge was rejected because this service sits in the Core dispatcher cadence path and is small enough to migrate safely. Rewriting bucketing math or the unrelated existing quality-curve work was rejected because the current pass is pointer safety.
Scalability potential: Low devices keep the same continuous bucketing cadence and survival distribution behavior. Middle/high/ultra retain rebalance and blackbox telemetry while removing stale descriptor state; no binary hardware branch was added.
Hardware Impact: i3/MX350 pays one flat generation compare when a bucketing table is touched. Cold re-init/teardown now performs eight release calls and prevents Vault refcount leaks.

## Decision 024 - Lockstep hash sources validate resolved views, not cached ptr
Problem: `LockstepStateValidator.TryGetHashSourceBuffer` acquired a local `VaultBufferHandle<T>`, checked `handle.ptr` for alignment, then called `handle.Resolve(vault)`. That route proved the cached pointer, not the generation-validated transient view actually hashed for determinism.
Solution: Use `TryGetGenerationHandle<T>` and `TryResolveHandle` to obtain a local `NativeArray<T>` view first, then validate alignment through `buffer.GetUnsafeReadOnlyPtr()` before hashing.
Rejected Alternatives: Keeping the local legacy handle was rejected because rollback hash proof should not depend on any cached pointer field. Persisting a hash-source descriptor was rejected because this method only needs a phase-local view.
Scalability potential: The lockstep hash cadence remains governed by the existing continuous quality/stress curve. Low through ultra keep identical pointer safety semantics with no tier branch.
Hardware Impact: i3/MX350 pays one flat generation compare only when the hash-source buffer is requested; no GC and no additional persistent native state.

## Decision 025 - Input bridge facade writes through resolved NativeArray
Problem: `H8InputMappingFacade` wrote `BridgeInputFacadeBindings` through local `VaultBufferHandle<T>` descriptors and `ResolvePointer`, leaving bridge hydration on the cached-pointer route.
Solution: Allocate or acquire the buffer with `GetGenerationHandle`, resolve a local `NativeArray<H8InputFacadeBindingEntry>` through `TryResolveHandle`, clear via a transient `GetUnsafePtr()` on that view, and write entries through indexed `NativeArray` access.
Rejected Alternatives: Keeping raw pointer writes was rejected because ScriptableObject bridge sync can run in Play Mode after Vault relocation. Introducing a persistent facade-owned handle was rejected because the mapping source is the serialized facade list, not a runtime buffer owner.
Scalability potential: Low devices keep the same bridge sync behavior; high/ultra get the same data route without cached pointer state. No binary tier branch was added.
Hardware Impact: Sync path pays one flat generation compare and one transient MemClear; no additional GC beyond the existing serialized editor list.

## Decision 026 - Prefab registry binder hydrates bridge DTOs through local views
Problem: `H8PrefabRegistryRuntimeBinder` hydrated prefab mapping and lore link buffers through local `VaultBufferHandle<T>` descriptors and `ResolvePointer` in both bind and clear paths.
Solution: Acquire `BridgePrefabMapping` and `BridgePrefabLoreLinks` with `GetGenerationHandle`, resolve local `NativeArray<T>` views through `TryResolveHandle`, clear through transient `GetUnsafePtr()` on those views, and write DTO entries by index.
Rejected Alternatives: Keeping raw pointer hydration was rejected because prefab registry binding can run during Play Mode after Vault relocation. Persisting static binder handles was rejected because the registry asset is the authoring source and the binder does not own a runtime lifetime.
Scalability potential: Low devices keep the same registry bind route. Middle/high/ultra retain runtime acoustic/lore signal publication without cached Vault pointer state.
Hardware Impact: Bind path pays two flat generation compares and two transient MemClear calls; no added managed allocation.

## Decision 027 - Design bridge runtime avoids local pointer-bearing descriptors
Problem: `H8BridgeFacadeRuntime` used local `VaultBufferHandle<T>` descriptors and `ResolvePointer` for design value bytes, macro header persistence, and the 300-frame facade telemetry dump ring. These are local variables, but they still route bridge live-tuning writes through the obsolete cached-pointer API.
Solution: Convert each route to `GetGenerationHandle` or `TryGetGenerationHandle`, then resolve a method-local `NativeArray<T>` through `TryResolveHandle`. Value writes use a transient pointer derived from the resolved byte view only for the aligned float store; telemetry hash/dump now reads entries through the resolved `NativeArray<T>`.
Rejected Alternatives: Keeping the legacy bridge because the handles were local was rejected; the source audit policy is to prevent new code from learning `ResolvePointer`. Persisting bridge handles was rejected because the bridge facade asset, not the runtime, owns the authoring fact.
Scalability potential: Low devices pay one flat generation compare per bridge buffer touched. Middle/high/ultra keep live tuning, macro persistence, and blackbox dumps without cached Vault pointer state; no binary quality branch was added.
Hardware Impact: i3/MX350 avoids stale pointer trust in live tuning and blackbox dump routes. The extra O(1) generation compare is colder than the existing facade iteration, signal publication, and dump IO.

## Decision 028 - Content authority ledgers use generation descriptors
Problem: `ContentRuntimeServices` persisted six `VaultBufferHandle<T>` fields across the bundle ref counter and content runtime for resident bundle state, telemetry, and pending-load queues. These managers can survive DataVault replacement and were resolving raw pointers from pointer-bearing descriptors.
Solution: Convert all six fields to `VaultGenerationHandle<T>`, add local resolve-or-acquire helpers that return `NativeArray<T>` views, derive pointers only from those views inside the current method, and release descriptors on ref-counter vault rebind, runtime teardown, and DataVault hot-swap.
Rejected Alternatives: Ignoring content because it had unrelated Addressables/hot-swap changes was rejected; the Vault handle edits are separable. Rewriting Addressables ownership was rejected because that is existing SHINOBU_203/ContentAuthority work and outside this pointer-safety pass.
Scalability potential: Low devices keep the same pending-load and telemetry cadence with one flat generation compare per access. Middle/high/ultra retain content blackbox evidence and live residency accounting without stale Vault aliases; no binary quality branch was added.
Hardware Impact: i3/MX350 loses persistent pointer-bearing descriptor fields in the content manager. Teardown/hot-swap now returns six Vault references instead of leaking or leaving invalid descriptors after service replacement.

## Decision 029 - Base homeostasis buffers use generation descriptors
Problem: `HomeostasisBrain.cs` persisted three legacy handles for global hardware metrics, frame-time samples, and its 300-frame blackbox. This authority controls quality pressure and survives registry DataVault replacement, so stale handles here can poison global throttling.
Solution: Store `VaultGenerationHandle<T>` descriptors, resolve or reacquire method-local `NativeArray<T>` views through `TryResolveHandle`, avoid clearing existing moved buffers after generation churn by probing `TryGetGenerationHandle` before allocation, and release descriptors during shutdown or DataVault hot-swap.
Rejected Alternatives: Migrating the larger `HomeostasisBrain.ScalabilityDictator.cs` in the same pass was rejected because it owns additional job-fenced state and deserves a separate review. Keeping the base file on legacy handles was rejected because the base migration is compact and directly protects the global quality authority.
Scalability potential: Low devices keep the same continuous `GlobalQualityWeight` path with one flat generation compare per buffer. Middle/high/ultra retain homeostasis blackbox evidence and visual-overkill pressure policy without cached Vault aliases.
Hardware Impact: i3/MX350 removes three persistent pointer-bearing descriptors from the global pressure manager. Shutdown and hot-swap now return three Vault refs instead of clearing static fields only.

## Decision 030 - Scalability dictator buffers use generation descriptors
Problem: `HomeostasisBrain.ScalabilityDictator.cs` still persisted seven `VaultBufferHandle<T>` descriptors and used `.Resolve(vault)` / `GetElementAsRef` for health, quality state, tuning, mock load, mock terrain proof, CSV scratch, and the 300-frame oscilloscope. This kept the highest-level quality authority on pointer-bearing routes after the base file was migrated.
Solution: Convert all seven buffers to `VaultGenerationHandle<T>`, reuse the base `TryResolveOrAcquire` helper, and resolve only method-local `NativeArray<T>` views through `IDataVault.TryResolveHandle`. Editor/test facades copy structs out of the resolved view, sanitize/mutate, and write the row back by index. DataVault hot-swap now releases scalability descriptors against the previous Vault before `_dataVault` is replaced, after completing the pending mock terrain sampler job.
Rejected Alternatives: Keeping the dictator on the legacy bridge was rejected because it is Core-owned and controls `GlobalQualityWeight`. Holding `ref` rows from `GetElementAsRef` was rejected because ref access is derived from a cached-pointer compatibility handle. Rewriting the continuous scalability math was rejected because the existing math already uses smooth weights and this pass is memory safety.
Scalability potential: Low devices retain cheap mock terrain proof and quality telemetry with one generation compare per buffer touch. Middle/high/ultra keep the visual-overkill policy, oscilloscope, CSV tuning bridge, and shader-quality globals without any persistent Vault alias.
Hardware Impact: i3/MX350 removes seven pointer-bearing descriptors from the global pressure dictator. Hot tick writes pay one flat metadata compare for state and telemetry views; teardown/hot-swap now returns seven Vault refs instead of leaking or clearing local descriptors only.

## Decision 031 - AUP origin-shift lanes use generation descriptors
Problem: `AupOriginShiftCoordinator` persisted eight `VaultBufferHandle<T>` descriptors and resolved AUP states, velocities, history, telemetry, runtime state, mock camera, CSV scratch, and the padded counter through `.Resolve(vault)`. These buffers are direct targets for origin-shift and defrag relocation, so stale descriptors here undermine the AUP precision contract.
Solution: Convert all eight descriptors to `VaultGenerationHandle<T>`, add a local `TryResolveOrAcquire` helper, and route tick/rebase/editor/CSV paths through method-local `NativeArray<T>` views from `IDataVault.TryResolveHandle`. On cached Vault replacement, release all non-zero descriptors against the previous Vault before clearing local state.
Rejected Alternatives: Keeping origin shift on the legacy bridge was rejected because this file moves `double3` AUP payloads and can run during the same maintenance windows that relocate Vault buffers. Rewriting origin-shift algorithms was rejected because the existing AUP math already subtracts in double precision before float-local work; this pass is descriptor safety.
Scalability potential: Low devices retain time-sliced rebase batches from `GlobalQualityWeight`; middle/high/ultra keep larger batches and telemetry without cached Vault aliases. No binary tier branch was introduced.
Hardware Impact: i3/MX350 pays one flat generation compare when AUP lanes are resolved. The 64-byte padded counter remains false-sharing isolated; hot rebase jobs still receive phase-local pointers derived from validated `NativeArray<T>` views.

## Decision 032 - Crash blackbox no longer retains Vault aliases
Problem: `GlobalTelemetryBus.Blackbox.cs` persisted eleven legacy `VaultBufferHandle<T>` descriptors and eleven static Vault-backed `NativeArray<T>` aliases. The buffers were lifetime-locked, but a Core manager retaining native views still violates the generation-handle contract and hides release/refcount leaks during crash-path shutdown.
Solution: Convert all blackbox lanes to `VaultGenerationHandle<T>`, add a local `TryResolveBlackboxBuffer` helper, and resolve method-local `NativeArray<T>` views in event push, source registration, frame commit, dump writing, MMF flushing, watchdog probing, and editor copy routes. Binding failure and teardown now release every non-zero descriptor after unlocking the relocation fences.
Rejected Alternatives: Relying on `TryLockBuffer` alone was rejected because it prevents relocation instead of proving the manager can survive relocation-era handles. Keeping cached arrays only for speed was rejected because this is forensic infrastructure, not a sub-0.1 ms gameplay solver.
Scalability potential: Low devices keep the same 300-frame blackbox and MMF cadence with descriptor validation only on touched lanes. Middle/high/ultra retain richer crash forensics and watchdog coverage without persistent Vault views. No binary quality branch was introduced.
Hardware Impact: i3/MX350 pays one flat generation compare when a blackbox lane is touched; shutdown now returns eleven Vault refs instead of leaking them. Long-lived manager-side `NativeArray<T>` aliases are removed from this file.

## Decision 033 - Memory sentinel validates targets under generation locks
Problem: `MemorySentinelRuntime` persisted ten `VaultBufferHandle<T>` descriptors and used `TryGetBufferHandle` / `ResolvePointer` to watch external targets. It also unlocked target buffers before consuming validation results and rollback copies, leaving a relocation window around stored target pointers.
Solution: Convert owned sentinel lanes to `VaultGenerationHandle<T>` with `TryResolveOrAcquire`, resolve all owned buffers through local `NativeArray<T>` views, and acquire external target pointers from `TryGetGenerationHandle` plus `TryResolveHandle`. `CompleteValidationJob` now keeps locks active through `ConsumeResults` and releases them in `finally` after rollback/correction work.
Rejected Alternatives: Keeping `ResolvePointer` in a memory sentinel was rejected because it audits stale pointers while using a stale-pointer API. Removing target pointers from `MemorySentinelTargetDTO` in this pass was rejected because the scheduled validation job still needs locked phase-local pointers; the lock fence now covers their full use window.
Scalability potential: Low devices keep the existing `GlobalQualityWeight` cadence collapse for validation frequency. Middle/high/ultra can validate more frequently without stale descriptor debt. No binary quality branch was introduced.
Hardware Impact: i3/MX350 pays one flat generation compare for each sentinel lane or watched target view. The rollback copy no longer races against Vault defrag after target unlock.

## Decision 034 - Input haptics tuner stops teaching legacy pointer APIs
Problem: `InputCurveHapticsTunerWindow.cs` was editor-only, but it still demonstrated `GetBufferHandle`, `VaultBufferHandle<T>`, and `GetElementAsRef` for deterministic input profile/state buffers. That keeps obsolete pointer-bearing access patterns alive in designer-facing code and makes future runtime copy-paste defects more likely.
Solution: Replace the local profile/state handles with `VaultGenerationHandle<T>`, resolve `NativeArray<T>` views through `IDataVault.TryResolveHandle` inside `OnGUI`, read rows by index, mutate the profile value locally, and write row zero back by index after `EditorGUI.EndChangeCheck`.
Rejected Alternatives: Ignoring editor code was rejected because Task 17/18 facades are part of the SHINOBU_202 human-control surface. Releasing the descriptors from the window was rejected because this window is not the owner of `ShinobuInputProfile` or `ShinobuInputCurrentDto`; it only observes or edits owner-created buffers through the existing Vault route.
Scalability potential: Low devices see no runtime cost because this is editor-only. Middle/high/ultra editor sessions still get live curve/haptics visualization while resolving through the same generation descriptor path as runtime managers.
Hardware Impact: i3/MX350 runtime unchanged. Editor repaint pays two flat generation compares and copies 88 bytes of DTO data, removing all obsolete pointer-bearing source hits from this facade.

## Decision 035 - Input dispatcher resolves Vault views per phase
Problem: `InputDispatcher.cs` persisted twelve `VaultBufferHandle<T>` descriptors and used `.Resolve`, `GetElementAsRef`, `GetElementAsReadOnlyRef`, and a background-thread `_inputReplaySnapshotHandle.ptr` path. Deterministic input, haptic commands, XR state, and replay snapshot buffers cross frame boundaries, so cached Vault pointers here can produce UAF during defrag or DataVault replacement.
Solution: Convert all input-owned Vault descriptors to `VaultGenerationHandle<T>`, add resolve/acquire helpers that return method-local `NativeArray<T>` views, replace ref access with row index copies, release descriptors on shutdown and DataVault hot-swap, and move replay snapshot copying into `StageInputReplaySnapshot` while the local view is valid. The replay worker now flushes the MMF only and does not dereference Vault memory.
Rejected Alternatives: Keeping the replay worker on a cached Vault pointer was rejected because it is the clearest stale-pointer race in the file. Removing the replay snapshot lane outright was rejected because the deterministic bridge still expects a Vault-backed snapshot audit row; the safer minimal change is generation resolution plus phase-local copy.
Scalability potential: Low devices pay flat generation comparisons for input/haptic/XR lanes and keep the existing continuous haptic throttle behavior under pressure. Middle/high/ultra preserve deterministic replay, richer haptic composition, and XR look-at state without storing Vault aliases.
Hardware Impact: i3/MX350 removes twelve pointer-bearing descriptors and one background stale-pointer path. The replay path still copies the same 12 KB payload at its existing cadence; hot input writes add only metadata generation checks.

## Decision 036 - System dispatcher owns phase fences through generation descriptors
Problem: `SystemDispatcher.cs` persisted legacy Vault handles for H8 time, dispatcher blackbox, master job/fence telemetry, presentation suppression, and static raycast command/hit buffers. The dispatcher is also the system that defines when Vault relocation is legal, so cached pointer-bearing descriptors in this file can invalidate the entire "defrag only between phases" safety model.
Solution: Convert all dispatcher-owned descriptors to `VaultGenerationHandle<T>`, add a single generation resolve/acquire helper that returns method-local `NativeArray<T>` views, and release old descriptors during shutdown or DataVault service replacement. Scheduled raycast buffers keep their existing Vault lock fence while the scheduled `RaycastCommand` job owns local views; the handles themselves are pointer-free.
Rejected Alternatives: Keeping `ResolveBuffer` around the domain fence array was rejected because AUP hard-fence cleanup can run during relocation pressure. Keeping static raycast handles as legacy pointers was rejected because raycast scheduling spans frames and can outlive a compaction window. Rewriting the dispatcher scheduler was rejected because this pass is descriptor safety, not a topology refactor.
Scalability potential: Low devices keep the same continuous `GlobalQualityWeight` behavior for frame pacing, temporal compression, and presentation suppression; middle/high/ultra keep richer master telemetry and raycast scheduling without cached Vault aliases. No binary quality branch was introduced.
Hardware Impact: i3/MX350 pays flat generation comparisons when dispatcher lanes are touched. The expensive work remains job dependency combining, raycast scheduling, and ring writes; stale pointer access during Vault defrag is removed from the phase owner.

## Decision 037 - Analytics exporter worker resolves locked buffers by generation
Problem: `AsynchronousTelemetryExporter.cs` persisted seventeen `VaultBufferHandle<T>` descriptors and the `H8_Analytics_IO` worker created `NativeArray<T>` views directly from cached `handle.ptr` values. The exporter also wrote hot ingress events through `_ingressCursorHandle.ptr` and a selected ingress handle pointer, leaving both main-thread and background-thread UAF paths.
Solution: Convert every exporter-owned descriptor to `VaultGenerationHandle<T>`, resolve main-thread and worker views through `IDataVault.TryResolveHandle`, and retain the existing owner-tagged Vault locks only as the compaction fence while the worker is alive. Shutdown releases descriptors only after `StopWorker()` succeeds and worker locks are removed.
Rejected Alternatives: Keeping worker-owned buffers on `VaultBufferHandle<T>.ptr` was rejected because a lock is a relocation fence, not permission for managers to persist pointer metadata. Releasing descriptors when `StopWorker()` fails was rejected because the worker can still be alive and resolving handoff/dump buffers; preserving locks is the safer failure mode.
Scalability potential: Low devices still shed routine analytics continuously through `GlobalQualityWeight`, backlog pressure, and deterministic AUP hashing. Middle/high/ultra keep richer route samples, heatmap debug, network batch size, and disk replay without a cached Vault pointer route.
Hardware Impact: i3/MX350 pays O(1) generation checks for event ingress and worker scratch resolution. The expensive worker path remains RLE/network/disk I/O; stale Vault pointer dereference is removed from both main and background threads.

## Decision 038 - Acoustic echo static queue uses pointer-free Vault descriptors
Problem: `AcousticEchoLocationRuntime.cs` persisted four static `VaultBufferHandle<T>` descriptors and resolved pending taps, frame taps, job results, and the 300-frame blackbox through cached pointer metadata. Echo tap queues can cross frame/defrag windows, so the static queue was a UAF candidate even though the Burst tracking job itself only needs phase-local views.
Solution: Convert the four AISensory lanes to `VaultGenerationHandle<T>`, add resolve/acquire/release helpers, and keep all queue drain/drop, job schedule, blackbox, and fault dump access on method-local `NativeArray<T>` views returned by `IDataVault.TryResolveHandle`. Dispose and DataVault replacement release only the descriptors owned by this static runtime; an active tracking fence is completed before old descriptors are released.
Rejected Alternatives: Keeping `ReleaseOwnerBuffers(SystemID.AISensory)` was rejected because it can free unrelated AISensory buffers owned by neighboring static services. Replacing the tracking job or acoustic scoring model was rejected because this pass is pointer safety; the existing quality-weight scoring already gives the continuous cheap/expensive behavior.
Scalability potential: Low devices keep direct echo breadcrumb scoring and quality-byte attenuation; middle/high/ultra preserve richer portal/movement/sonar tap density without pointer retention. No binary quality branch was introduced.
Hardware Impact: i3/MX350 pays one generation compare when enqueueing, draining, scheduling, or writing blackbox rows. The expensive path remains the existing O(n) tap scan capped at 32 taps; stale Vault pointer metadata is removed from the static sensory queue.

## Decision 039 - Path funnel navmesh resolves WFC invalidation lanes by generation
Problem: `PathFunnelNavmeshRuntime.cs` persisted five `VaultBufferHandle<T>` descriptors, relied on legacy handle `Length` and `GenerationID`, and resolved mutation/telemetry/runtime-state views through `.Resolve(...)`. The fast tick also used `TryGetBuffer` for WFC grid flags, bypassing the generation descriptor route used by migrated consumers.
Solution: Convert path-funnel owned lanes to `VaultGenerationHandle<T>`, derive actual lengths from transient resolved views, and route every active-path, cell-mask, invalidation, telemetry, runtime-state, and WFC-grid access through `IDataVault.TryResolveHandle`. Disable/hot-swap releases only this component's owned descriptors.
Rejected Alternatives: Keeping `TryGetBuffer` for the external WFC grid was rejected because it normalizes direct Vault views outside the generation descriptor contract. Releasing all `SystemID.AIPathfinding` buffers was rejected because other pathfinding systems can share the owner ID.
Scalability potential: Low devices still use bounded 500-bit masks and ring cursors; middle/high/ultra can increase active path and invalidation capacities while retaining phase-local descriptor resolution. No binary quality branch was introduced.
Hardware Impact: i3/MX350 pays flat generation checks when path APIs or tick phases touch Vault lanes. The dominant cost remains the bounded scan of active corridors against WFC cell masks; stale pointer metadata is removed from path invalidation state.

## Decision 040 - WFC laser cut tool writes Vault progress through phase-local arrays
Problem: `WfcLaserCutRuntime.cs` persisted two static `VaultBufferHandle<T>` descriptors and converted their cached `.ptr` fields into raw `float*` progress and telemetry pointers. The same file used `GlobalRegistry.ScalabilityTier` for visual overkill, producing a hard tier branch in a user-facing VFX path.
Solution: Convert cut progress and blackbox lanes to `VaultGenerationHandle<T>`, resolve local `NativeArray<T>` views per cut attempt, and rewrite progress clear/telemetry/dump paths to index those views instead of pointers. Replace tier branching with a continuous smoothstep over `HomeostasisBrain.GlobalQualityWeight`, modulated by system stress headroom.
Rejected Alternatives: Keeping raw pointers as "short-lived" locals was rejected because they were derived from persistent pointer-bearing descriptors and used in gameplay truth writes. Keeping the tier switch was rejected because quality-weight already supplies the required continuous control scalar.
Scalability potential: Low devices collapse molten/spark overkill smoothly as quality/stress drops; middle/high/ultra regain richer cut visuals continuously without a feature pop. No binary quality branch remains in this file.
Hardware Impact: i3/MX350 pays two generation checks per active cut attempt and a few scalar ops for the visual curve. The expensive work remains shader feedback and signal fanout; stale Vault pointer writes are removed from the tool path.

## Decision 041 - Ladder climb IK schedules only generation-resolved views
Problem: `ProceduralLadderClimbRuntime.cs` persisted five `VaultBufferHandle<T>` descriptors and staged its IK solve job through `.Resolve(_dataVault)` views. Disable/destroy only cleared handles, leaving Vault refcount/generation state untouched.
Solution: Convert IK input/output, ladder AUP, telemetry ring, and telemetry cursor descriptors to `VaultGenerationHandle<T>`, resolve method-local views before reads/writes/scheduling, and release the five owned descriptors after completing any outstanding IK job during teardown or DataVault replacement/loss.
Rejected Alternatives: Keeping legacy handles because this is presentation IK was rejected; the job consumes Vault-backed AUP and telemetry data and can race relocation if the manager keeps pointer metadata. Owner-wide release was rejected in favor of exact descriptor release.
Scalability potential: Low devices keep the same cheap slide/grip solve cadence; middle/high/ultra retain richer IK targets and telemetry without pointer retention. No binary quality branch was added.
Hardware Impact: i3/MX350 pays five generation checks when staging or reading IK views. The dominant cost remains the Burst IK solve; stale Vault handle metadata is removed from animation locomotion.

## Decision 042 - Tool haptics front/back lanes stop refreshing legacy handles
Problem: `ToolHapticsRuntime.cs` persisted front/back `VaultBufferHandle<HapticCommand>` descriptors and called `ResolveBuffer(ref handle)` before every haptic buffer access, keeping pointer-bearing metadata alive in the manager.
Solution: Convert both lanes to `VaultGenerationHandle<HapticCommand>`, cache the active `IDataVault`, resolve local `NativeArray<HapticCommand>` views per haptic operation, and release old descriptors on DataVault loss/replacement or teardown.
Rejected Alternatives: Keeping `ReadOnlySpan` snapshots as the only change was rejected because the stale descriptor route would still exist before the span is created. Owner-wide release was rejected because exact front/back descriptors are known.
Scalability potential: Low devices still compact and decay a 16-command buffer; middle/high/ultra keep richer command blending and sinusoidal feedback without pointer retention. No binary quality branch was introduced.
Hardware Impact: i3/MX350 pays one generation compare per front/back lane access. The dominant haptic work remains bounded command compaction/merge; stale Vault metadata is removed from tool rumble.

## Decision 043 - Procedural bone blender schedules only generation-resolved views
Problem: `ProceduralBoneBlenderRuntime.cs` persisted eleven `VaultBufferHandle<T>` descriptors and used `.Resolve(vault)` in editor, CSV, mock rig, telemetry, GPU upload, and Burst scheduling paths. Disable/destroy and DataVault replacement cleared descriptors without releasing the owned Vault lanes.
Solution: Convert all fauna procedural bone lanes to `VaultGenerationHandle<T>`, add a shared resolve/acquire helper that uses `IDataVault.TryResolveHandle`, resolve `NativeArray<T>` views only inside the active method/phase, and release exact descriptors after completing outstanding solver jobs on disable, destroy, or DataVault replacement.
Rejected Alternatives: Keeping the legacy descriptors for editor-only routes was rejected because those same handles feed runtime solver and telemetry paths. Owner-wide release was rejected because exact buffer IDs are known and broader release would cross another owner boundary if future lanes share `SystemID.AnimationFauna`.
Scalability potential: Low devices still collapse update cadence through existing continuous `GlobalQualityWeight`; middle/high/ultra keep GPU matrix upload and richer procedural bone response without pointer retention. No binary quality branch was added.
Hardware Impact: i3/MX350 pays eleven generation compares when staging the fauna solve and a few compares on editor/telemetry reads. The dominant cost remains the Burst solve and GPU buffer copy; stale Vault metadata is removed from the animation path.

## Decision 044 - Kinetic character animator removes descriptor and direct-view debt
Problem: `KineticCharacterAnimatorRuntime.cs` persisted twelve `VaultBufferHandle<T>` descriptors, resolved them through `.Resolve(vault)`, and also fetched `PlayerKinematicState` / `VoxelSdfTexture3D` through direct `TryGetBuffer` calls. Teardown and DataVault replacement cleared descriptor fields without releasing the Vault lanes.
Solution: Convert all locomotion animation lanes to `VaultGenerationHandle<T>`, add generation-checked owned and external resolve helpers, route player and SDF reads through method-local `TryGetGenerationHandle` + `TryResolveHandle`, and release exact owned descriptors after solver completion on disable, destroy, and DataVault replacement.
Rejected Alternatives: Keeping `TryGetBuffer` for external read-only lanes was rejected because read-only does not make stale aliases safe after relocation. Releasing all `SystemID.AnimationLocomotion` buffers was rejected because adjacent locomotion runtimes can share the owner ID.
Scalability potential: Low devices retain existing continuous quality-weighted locomotion/SDF degradation; middle/high/ultra keep richer IK and GPU matrix output without persistent Vault views. No binary quality branch was added.
Hardware Impact: i3/MX350 pays twelve generation compares for owned staging and two local external descriptor resolves on player/SDF frames. The dominant cost remains Burst locomotion/matrix work; stale Vault descriptors and direct external Vault views are removed from the manager.

## Decision 045 - Laser cutter scalability read uses generation descriptor
Problem: `LaserCutterDodRuntime.cs` had one remaining `TryGetBufferHandle` route for `ShinobuScalabilityState`, leaving a legacy descriptor path inside an otherwise generation-descriptor tool runtime.
Solution: Replace the quality-weight read with `TryGetGenerationHandle<ScalabilityStateDTO>` and `TryResolveHandle`, keeping the resolved `NativeArray<ScalabilityStateDTO>` local to `ResolveGlobalQualityWeight()`.
Rejected Alternatives: Keeping the old route because it is read-only was rejected; relocation still invalidates pointer-bearing handle metadata regardless of write intent.
Scalability potential: Low/middle/high/ultra visuals continue to use the same continuous quality scalar; only the descriptor acquisition route changed.
Hardware Impact: i3/MX350 cost is one generation handle resolve on quality reads. It removes the final legacy handle API hit in this runtime without changing cut math.

## Decision 046 - Tool kinematics editor facade stops using legacy handles
Problem: `ToolKinematicsTunerWindow.cs` cached seven `VaultBufferHandle<T>` descriptors and used `ResolveBuffer(ref handle)` / `.Resolve(vault)` while Play Mode editor tuning can observe and mutate live Vault lanes.
Solution: Convert editor descriptors to `VaultGenerationHandle<T>`, resolve local editor views through `IDataVault.TryResolveHandle`, and release exactly the editor-acquired descriptors when the window closes or the Vault reference changes.
Rejected Alternatives: Treating editor windows as harmless was rejected because they run against Play Mode memory and can hold stale descriptors across defrag/rebind events.
Scalability potential: Runtime scalability is unchanged; the editor facade now observes the same quality-weighted tool kinematics state without preserving pointer-bearing handles.
Hardware Impact: No player-frame cost. Editor draws pay generation checks while the tuner window is open; stale Vault metadata is removed from the facade.

## Decision 047 - Tool kinematics runtime drops legacy handles and ref-return API
Problem: `ToolKinematicsRuntime.cs` persisted fifteen `VaultBufferHandle<T>` descriptors, resolved them through `ResolveBuffer(ref handle)` / `.Resolve(vault)`, and exposed an unused public `ToolKinematicsVaultAccess` class that returned refs via `GetElementAsRef`.
Solution: Convert all runtime lanes to `VaultGenerationHandle<T>`, resolve method-local `NativeArray<T>` views through `IDataVault.TryResolveHandle`, release exact descriptors on disable/destroy or Vault rebind, and remove the unused ref-return accessor class.
Rejected Alternatives: Keeping the ref-return accessor with generation handles was rejected because returning refs from transient views invites callers to keep byref access beyond a safe execution phase. Owner-wide release was rejected because exact ToolKinematics buffer IDs are known.
Scalability potential: Low devices keep existing continuous LOD hold and beam raymarch limits; middle/high/ultra keep richer tool beam/IK/spark output without persistent Vault descriptors. No binary quality branch was added.
Hardware Impact: i3/MX350 pays fifteen generation compares while staging tool jobs. The dominant cost remains Burst raymarch/beam jobs; stale Vault descriptor metadata and public byref mutation routes are removed.

## Decision 048 - Tool durability removes false-positive legacy naming
Problem: `ToolDurabilitySystem.cs` already used `VaultGenerationHandle<T>` and `TryResolveHandle`, but its local helper was named `TryResolveBuffer`, which caused broad legacy Vault scans to surface a false-positive ResolveBuffer-like route.
Solution: Rename the helper and callers to `TryResolveDurabilityView` without changing allocation, BufferID, DTO, or job behavior.
Rejected Alternatives: Leaving the name was rejected because audit noise slows detection of real stale pointer routes in tools and animation.
Scalability potential: Runtime scalability is unchanged; durability still uses the existing generation-descriptor state lanes.
Hardware Impact: No runtime change. The benefit is audit precision: broad scans now separate safe generation views from forbidden legacy resolve calls.
