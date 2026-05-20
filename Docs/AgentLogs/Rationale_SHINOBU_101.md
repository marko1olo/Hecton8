# Rationale_SHINOBU_101

Status: PENDING VERIFICATION

## Active Re-Entry R24

Problem: The active SHINOBU_101 status/rationale/log paths were missing after Batch010 archive rotation, while the user explicitly continued the same Addressables heap/release-gate mandate. R23 also left a hidden `GlobalRegistry.AssetLifecycle` fallback pattern inside runtime release/acquire helpers, which violates cold-service caching and can mask boot-order defects.

Solution: Rehydrate active status/rationale/log files from archived Batch010 authority and patch runtime owners to use cached service fields only. `ContentRuntimeServices.OnEnable` and `Start` now call `CacheDependencies()` before release-capable work. `ContentRuntimeServices` release helpers consume `_assetLifecycle` only. `WorldChunkResidencyManager` acquire/mark/release helpers consume `_assetLifecycleGovernor` only. If the cold cache is missing, the code fails closed and retains the handle where possible instead of polling the registry during tick/release cadence.

Rejected Alternatives: Keeping fallback `GlobalRegistry.AssetLifecycle` reads was rejected because it is a hidden service-locator dependency in runtime cadence. Running `dotnet build` again was rejected because the latest valid compile probe already aborts before SHINOBU code on missing external Construction source. Creating a second release queue or new registry slot was rejected because R23 already established the single governor release route.

Scalability potential: Low devices avoid visible-frame release stalls by preserving the single blind/panic release bridge. Middle/high/ultra keep the same continuous TTL/residency curve and can spend saved CPU on richer resident content instead of direct unload hitches.

Hardware Impact: Static only. Removing runtime registry fallback avoids per-release service-locator reads and prevents accidental direct release bypass recovery paths. Expected gain is hitch avoidance, not a measured microsecond claim. Unity import/profiler proof remains pending behind the external compile wall.

## R24 Verification

- `rg -n "ResolveAssetLifecycleGovernor|GlobalRegistry\\.AssetLifecycle" Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs`
  - `ContentRuntimeServices.cs`: one cold `CacheDependencies()` assignment.
  - `WorldChunkResidencyManager.cs`: one cold `RefreshColdServiceCache()` assignment.
- `rg -n "Addressables\\.Release\\(" Assets/_Project/Scripts`
  - one raw release line: `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:4218`.
- `rg -n "Addressables\\.ReleaseInstance\\(" Assets/_Project/Scripts`
  - one UI instance teardown line: `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2274`.
- `git diff --check` on touched runtime owners reports only LF-to-CRLF warnings.

## Compile Boundary R24

Problem: Full compile verification is still blocked by an external project-file/source mismatch.

Solution: Do not rerun build until the external owner restores or removes `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` from `Hecton8.Core.csproj`.

Rejected Alternatives: Fabricating a Construction source file or editing the project include was rejected as cross-domain sabotage.

Scalability potential: No runtime tier change.

Hardware Impact: Avoids wasting local IO/CPU on a known pre-SHINOBU compiler abort.

## Optimization-Lane Cold-DI Cleanup R25

Problem: R24 removed fallback `GlobalRegistry.AssetLifecycle` reads from Core Content and World, but the same release/pressure lane still had hot or cadence-bound registry reads in Optimization-owned services. `VRAMPressureMonitor.SampleAndRespond()` pulled `VRAMMonitor` and `AssetLifecycle` through the registry during pressure sampling, `RunPressureEviction()` pulled player inventory and render-texture pool services during emergency pressure handling, and `AssetLoadDispatcher` pulled `AssetLifecycle`, `VRAMMonitor`, and `VRAMPressure` during forced release drain, UI mip gating, and load budget resolution.

Solution: Cache these services during `OnEnable`, `Start`, and registration. Add `IGlobalRegistryHotSwapListener` to both `VRAMPressureMonitor` and `AssetLoadDispatcher` so service replacement refreshes cached fields without reintroducing tick-time registry polling. Runtime pressure math, release drain, UI mip bias gate, and dispatch concurrency now consume cached fields only.

Rejected Alternatives: Leaving a sampled 90-frame registry read was rejected because the global authority rule forbids turning cadence into a live query bus. A permanent fail-closed cache without hot-swap was rejected because player inventory and render texture services can arrive after the pressure monitor. A new global route was rejected; the existing hot-swap listener bucket is the established rebind mechanism.

Scalability potential: Low devices avoid service-locator churn in pressure response while preserving emergency evictions. Middle/high/ultra keep the same residency and load-budget math; the change is route hygiene, not a visual feature change.

Hardware Impact: Static estimate only. Removes several registry property chains from pressure sampling and dispatch budget cadence. Expected gain is small per call, but it removes central-service jitter from exactly the path that runs under memory pressure. No measured microseconds claimed without Unity profiler.

## R25 Verification

- `rg -n "GlobalRegistry\\.(AssetLifecycle|VRAMMonitor|VRAMPressure|PlayerInventory|RenderTexturePool)" Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs Assets/_Project/Scripts/Optimization/VRAMPressureMonitor.cs`
  - Remaining hits are service registration guards or `CacheDependencies()` assignments.
- `rg -n "Addressables\\.Release\\(" Assets/_Project/Scripts`
  - one raw release line remains: `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:4218`.
- `git diff --check` on R25 touched files reports LF-to-CRLF warnings only.

## Compile Boundary R25

Problem: Full compile remains blocked by the previously proven external Construction missing source.

Solution: No build launched. Keep SHINOBU verification static until the external compile item is restored or removed by its owner.

Rejected Alternatives: Running another known-failing build was rejected by command discipline.

Scalability potential: No runtime tier change.

Hardware Impact: Avoids needless local CPU/IO burn.

## Hot-Swap Lifecycle Closure R26

Problem: R25 established cold service fields and hot-swap rebinding for release/pressure cadence, but `WorldChunkResidencyManager.DisposeInternal()` still relied on `OnDisable()` to unregister tick/backpressure routes and the hot-swap listener. External `Dispose()` or an abnormal destroy path could leave a disposed instance subscribed to the registry listener bucket. `ClearColdServiceCache()` also left `_ambientBiotaService` populated after shutdown.

Solution: `DisposeInternal()` now calls `TryUnregister()` and `TryUnregisterHotSwap()` before job teardown and native disposal. `ClearColdServiceCache()` clears `_ambientBiotaService`. The four touched owners were statically rescanned to confirm registry reads now sit at registration, cold-cache, or hot-swap rebind boundaries rather than release/pressure cadence.

Rejected Alternatives: Depending on Unity lifecycle ordering alone was rejected because explicit `Dispose()` exists and must be safe. Keeping `_ambientBiotaService` across disable was rejected because it is a stale owner pointer. Running a build was rejected because the known external missing Construction source would abort before these changes.

Scalability potential: Low devices avoid stale listener callbacks and service-locator jitter in exactly the memory-pressure path where CPU time is least available. Middle/high/ultra keep the same continuous residency math and can use the saved stability budget on richer resident content.

Hardware Impact: Static estimate only. The gain is not an ALU micro-benchmark; it removes failure modes: no stale hot-swap callback into disposed world streaming state, no runtime registry fallback on pressure/release cadence, and no additional visible-frame Addressables release route. Profiler microseconds remain pending until compile/import is unblocked.

## R26 Verification

- `rg -n "GlobalRegistry\\.(AssetLifecycle|VRAMMonitor|VRAMPressure|PlayerInventory|RenderTexturePool|DataVault|JobAdmission|MacroDatabase|ObjectPool|AmbientBiota|SaveRuntime|AsyncPersistence)"` on the four touched owners:
  - Remaining hits are service registration guards, cold-cache setup, or hot-swap listener registration/unregistration.
- `rg -n "Addressables\\.Release\\(|Addressables\\.ReleaseInstance\\(" Assets/_Project/Scripts`
  - `Addressables.Release(` remains only at `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:4218`.
  - `Addressables.ReleaseInstance(` remains only at `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2275`.
- `git diff --check` on the four runtime files reports LF-to-CRLF warnings only.

## Compile Boundary R26

Problem: Full compile verification remains blocked by `Hecton8.Core.csproj` referencing missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`.

Solution: No build launched in R26. Static verification only.

Rejected Alternatives: Creating a fake Construction file, editing the project include, or rerunning the known-failing build was rejected as cross-domain sabotage and command-discipline failure.

Scalability potential: No runtime tier change.

Hardware Impact: Avoids wasting local CPU/IO on a compiler abort that cannot reach SHINOBU code.

## Governor Cold-DI Closure R27

Problem: R26 still left release/streaming-adjacent runtime helpers with hidden registry access. `AssetLifecycleGovernor` still acknowledged dispatch requests, sampled player AUP for eviction, toggled scanner-interference UI, and released distant chunk prefabs through direct `GlobalRegistry` reads. `AssetLoadDispatcher` static helper methods still pulled `GlobalRegistry.AssetLoadDispatcher`. `ItemCatalog` world-prefab streaming still queued dispatch, consumed tickets, acknowledged dispatch requests, sampled player AUP, and released Addressable world-prefab handles through direct registry lookups. `ItemCatalog` also lazy-allocated queue/set/scratch containers in release/dispatch methods.

Solution: Move these routes behind cold cached fields and hot-swap rebinding. `AssetLifecycleGovernor` now caches dispatcher, VRAM pressure, player context, player inventory, and scanner UI; runtime cadence uses those fields. `AssetLoadDispatcher` static helpers now resolve the owner-local `s_registeredInstance`. `ItemCatalog` implements `IGlobalRegistryHotSwapListener`, caches the governor/dispatcher/player context, converts world-prefab helper methods from static registry access to instance cached access, and allocates world-prefab release/dispatch managed containers during catalog rebuild instead of first release/dispatch.

Rejected Alternatives: Leaving `ItemCatalog` outside the patch was rejected because it owns real Addressables queue/consume/release cadence. Adding a new registry slot or direct sibling dependency was rejected; the existing Core hot-swap listener contract is the route. Running a build was rejected because the known external Construction source mismatch still aborts before SHINOBU code. Editing `HectonFloatingOrigin` or `GameBootstrapper` was rejected for R27 because the observed calls are non-SHINOBU owner/boot or blind-frame cleanup paths and do not create a second raw Addressables release route.

Scalability potential: Low devices avoid service-locator reads and first-use managed allocations inside world-prefab streaming and memory-pressure release cadence. Middle/high/ultra retain the same continuous TTL/residency behavior and can spend saved stability budget on richer resident content instead of stutter recovery. No low/high binary switch was introduced.

Hardware Impact: Static estimate only. R27 removes direct registry reads from hot release/dispatch acknowledgement paths and prevents first-release managed queue/list allocations in `ItemCatalog`. Expected gain is reduced jitter under pressure, not a claimed profiler number. Measured microseconds remain blocked until Unity import/compile reaches these files.

## R27 Verification

- `rg -n "Addressables\\.Release\\(|Addressables\\.ReleaseInstance\\(" Assets/_Project/Scripts`
  - `Addressables.Release(` remains only at `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:4303`.
  - `Addressables.ReleaseInstance(` remains only at `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2275`.
- `rg -n "GlobalRegistry\\.(AssetLoadDispatcher|AssetLifecycle|Player)|Hecton8\\.Core\\.GlobalRegistry\\.AssetLoadDispatcher" Assets/_Project/Scripts/ItemCatalog.cs`
  - Remaining hits are only `CacheRuntimeServices()` assignments.
- `rg -n "private static (bool TryAcquireWorldPrefabHandle|void MarkWorldPrefabLoaded|void CancelPendingWorldPrefabDispatch|void CompleteWorldPrefabDispatch|void CaptureCurrentPlayerAup|bool TryCaptureCurrentPlayerAup)" Assets/_Project/Scripts/ItemCatalog.cs`
  - no results; world-prefab helpers now use instance cached services.
- `git diff --check` on R27 runtime files reports LF-to-CRLF warnings only.

## Compile Boundary R27

Problem: Full compile verification remains blocked by `Hecton8.Core.csproj` referencing missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`.

Solution: No build launched in R27. Keep verification static until the external Construction owner restores or removes the missing project-file item.

Rejected Alternatives: Creating a fake Construction source file, editing the `.csproj`, or rerunning the known-failing build was rejected as cross-domain sabotage and command-discipline failure.

Scalability potential: No runtime tier change.

Hardware Impact: Avoids burning local CPU/IO on a compile abort that cannot verify SHINOBU changes.

## ItemCatalog Fixed Scratch Closure R28

Problem: R27 moved `ItemCatalog` world-prefab release/dispatch allocation out of first-use runtime methods, but the bridge still mutated managed `Queue<int>`, `HashSet<int>`, and `List<int>` containers during release drain and dispatch ticket pumping. The release drain also had the same structural risk as the previous queue path: a failed governor stage could be requeued and retried repeatedly when `maxReleaseCount <= 0`.

Solution: Replace the deferred world-prefab release queue/set with a fixed `int[]` ring allocated during catalog rebuild, with linear dedupe across a bounded buffer. Replace dispatch scratch `List<int>` with a fixed `int[]` and explicit count. `DrainDeferredWorldPrefabReleases()` now snapshots the starting pending count and processes each entry at most once per drain call; a failed release is requeued for a future blind/panic gate instead of spinning in the same frame.

Rejected Alternatives: Leaving managed containers in the bridge was rejected because it preserves GC-facing mutation on an Addressables release cadence. Expanding the fix into a full `ItemCatalog` Vault migration was rejected for R28 because the catalog is a managed ScriptableObject authoring/runtime bridge, not SHINOBU's rollback/Burst DTO owner, and converting its dictionaries would be a broad inventory/save-system change outside this polish pass. Direct immediate release when the ring is full was rejected because the single owner route remains the governor release gate.

Scalability potential: Low devices avoid managed container churn and same-frame release retry loops during world-prefab memory pressure. Middle/high/ultra keep the same continuous residency behavior while avoiding dispatch/release jitter; no low/high binary switch was introduced.

Hardware Impact: Static estimate only. R28 removes three managed mutable containers from the ItemCatalog Addressables release/dispatch bridge and bounds retry work to O(N_initial) per drain call. No profiler microseconds are claimed until Unity import/compile reaches SHINOBU code.

## R28 Verification

- `rg -n "_pendingWorldPrefabReleaseQueue|_pendingWorldPrefabReleaseSet|new Queue<int>|new HashSet<int>|new List<int>\\(32\\)|_worldPrefabDispatchScratch\\.Clear\\(|_worldPrefabDispatchScratch\\.Add\\(|_worldPrefabDispatchScratch\\.Count" Assets/_Project/Scripts/ItemCatalog.cs`
  - no results.
- `rg -n "Addressables\\.Release\\(|Addressables\\.ReleaseInstance\\(" Assets/_Project/Scripts`
  - `Addressables.Release(` remains only at `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:4303`.
  - `Addressables.ReleaseInstance(` remains only at `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2275`.
- `rg -n "GlobalRegistry\\.(AssetLoadDispatcher|AssetLifecycle|Player)|Hecton8\\.Core\\.GlobalRegistry\\.AssetLoadDispatcher" Assets/_Project/Scripts/ItemCatalog.cs Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs`
  - remaining hits are service registration guards, owner-local publication checks, cold-cache assignments, or hot-swap setup.
- `git diff --check` on SHINOBU runtime files reports LF-to-CRLF warnings only.

## Compile Boundary R28

Problem: Full compile verification is still blocked by the known external missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` item in `Hecton8.Core.csproj`.

Solution: No build launched in R28. Static verification only.

Rejected Alternatives: Rerunning a known pre-SHINOBU abort was rejected by user command discipline and AGENTS build discipline.

Scalability potential: No runtime tier change.

Hardware Impact: Avoids local CPU/IO burn while the compile wall remains external.

## ItemCatalog Runtime Rebuild Guard R29

Problem: R28 removed managed queue/set/list mutation from the world-prefab bridge, but `QueueWorldPrefabPrewarm()` and `TryGetLoadedWorldPrefab()` could still call `RebuildWorldPrefabLookup()` when lookup fields were null. That rebuild creates dictionaries and fixed arrays. It is acceptable during ScriptableObject `OnEnable` or editor validation, but not acceptable from gameplay request cadence. The direct fallback also used `FindByHash()`, which can call `RebuildLookup()` if the hash lookup is missing.

Solution: Add `TryEnsureWorldPrefabLookupReady()`. It returns true only when all world-prefab lookup/scratch fields are already initialized. It may rebuild outside Play Mode for editor/cold setup, but fails closed during Play Mode. Runtime direct prefab fallback now uses a linear no-allocation scan over `allItems` and `_runtimeItems` when `_hashLookup` is absent, avoiding lazy dictionary allocation. `OnDisable()` now queues and immediately drains catalog world-prefab handles through the governor route before cached services are cleared.

Rejected Alternatives: Leaving lazy rebuilds in gameplay callers was rejected because it preserves managed allocation in the Addressables cadence. Making a full Vault-backed catalog lookup was rejected as cross-domain inventory/save-system surgery beyond the SHINOBU addressable release bridge. Adding raw `Addressables.Release` in `OnDisable()` was rejected because the single owner route is the governor.

Scalability potential: Low devices avoid hidden dictionary/scratch allocation in world-prefab request paths and avoid leaked handles on catalog teardown. Middle/high/ultra retain continuous residency behavior and can spend stable frame time on richer streamed presentation. No binary quality branch was introduced.

Hardware Impact: Static estimate only. R29 removes a lazy dictionary/fixed-buffer rebuild from Play Mode addressable request calls and bounds teardown through the existing release governor. No profiler microseconds are claimed until Unity import/compile reaches SHINOBU code.

## R29 Verification

- `rg -n "RebuildWorldPrefabLookup\\(|TryEnsureWorldPrefabLookupReady|TryGetDirectWorldPrefabFallbackLinear|ReleaseAllWorldPrefabHandles\\(|DrainDeferredWorldPrefabReleases\\(0\\)|MatchesPersistentHash\\(hashId\\)" Assets/_Project/Scripts/ItemCatalog.cs`
  - rebuild call sites are `OnEnable`, editor `OnValidate`, and the non-playing branch of `TryEnsureWorldPrefabLookupReady()`.
- `rg -n "_pendingWorldPrefabReleaseQueue|_pendingWorldPrefabReleaseSet|new Queue<int>|new HashSet<int>|new List<int>\\(32\\)|_worldPrefabDispatchScratch\\.Clear\\(|_worldPrefabDispatchScratch\\.Add\\(|_worldPrefabDispatchScratch\\.Count|item\\.HashId" Assets/_Project/Scripts/ItemCatalog.cs`
  - no results.
- `rg -n "Addressables\\.Release\\(|Addressables\\.ReleaseInstance\\(" Assets/_Project/Scripts`
  - `Addressables.Release(` remains only at `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:4303`.
  - `Addressables.ReleaseInstance(` remains only at `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2275`.
- `git diff --check -- Assets/_Project/Scripts/ItemCatalog.cs`
  - LF-to-CRLF warning only.

## Compile Boundary R29

Problem: Full compile verification remains blocked by the known external missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` item in `Hecton8.Core.csproj`.

Solution: No build launched in R29. Static verification only.

Rejected Alternatives: Rerunning a known pre-SHINOBU abort was rejected by user command discipline and AGENTS build discipline.

Scalability potential: No runtime tier change.

Hardware Impact: Avoids local CPU/IO burn while the compile wall remains external.

## AssetLoadDispatcher Fixed Buffer Rewrite R30

Problem: `AssetLoadDispatcher` still used managed `List<T>` containers for queued requests, ready tickets, and inflight requests. Cold construction avoided first-use allocation, but `List<T>` is still a growable managed container. A pressure burst could force resize allocation or leave hot dispatch work dependent on mutable managed list state.

Solution: Replace queued, ready, and inflight lanes with fixed arrays and explicit counts: 128 queued requests, 32 ready tickets, and 64 inflight requests. Clamp the serialized ready-ticket limit to the fixed ticket buffer. `Enqueue()` now fails closed when buffers are saturated. `DispatchWithinBudget()` refuses dispatch when the ready ticket limit is zero/full or the inflight buffer is full. Typed swap-back removers clear vacated slots.

Rejected Alternatives: Increasing `List<T>` capacity was rejected because it still preserves growable managed storage. Moving these exact lanes into Vault buffers was rejected for R30 because the dispatcher queues are owner-local main-thread scheduling scratch, not rollback-critical shared state. Immediate raw release or synchronous load fallback was rejected because it would violate the single governor route and visible-frame budget.

Scalability potential: Low devices avoid resize allocation risk and bounded-list churn during pressure. Middle/high/ultra keep the same continuous pressure, ticket, and concurrency math; fixed capacities bound scheduling memory without binary hardware switches.

Hardware Impact: Static estimate only. R30 removes three growable managed lists from dispatch cadence and bounds request/ticket/inflight memory to 128/32/64 slots. No profiler microseconds are claimed until Unity import/compile reaches SHINOBU code.

## R30 Verification

- `rg -n "List<|_queuedRequests\\.Count|_readyTickets\\.Count|_inflightRequests\\.Count|\\.Add\\(|RemoveAt\\(|RemoveAtSwapBack|using System.Collections.Generic" Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs`
  - no results.
- `rg -n "_queuedRequestCount|_readyTicketCount|_inflightRequestCount|ResolveReadyTicketLimit|RemoveQueuedRequestAtSwapBack|ClearDispatchBuffers|QueuedRequestCapacity|ReadyTicketCapacity|InflightRequestCapacity" Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs`
  - expected fixed-buffer counters, limits, and removal helpers only.
- `git diff --check -- Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs`
  - LF-to-CRLF warning only.

## Compile Boundary R30

Problem: Full compile verification remains blocked by the known external missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` item in `Hecton8.Core.csproj`.

Solution: No build launched in R30. Static verification only.

Rejected Alternatives: Rerunning a known pre-SHINOBU abort was rejected by user command discipline and AGENTS build discipline.

Scalability potential: No runtime tier change.

Hardware Impact: Avoids local CPU/IO burn while the compile wall remains external.

## AssetLoadDispatcher Native Group Map Eviction R31

Problem: `AssetLoadDispatcher` still owned `_addressableGroupMap` as a private persistent `NativeParallelHashMap<uint, byte>`. That map was not Vault-owned, not cross-domain state, and not feeding a Burst job. It existed only to classify UI icon requests for the mip-bias gate, so the private persistent native allocation violated the SHINOBU H-Phi/Vault law without providing SIMD value.

Solution: Replace `_addressableGroupMap` with fixed owner-local `uint[512]` and `byte[512]` arrays plus `_addressableGroupCount`. Registration is bounded: update existing entry, append while capacity remains, ignore non-UI classifications when saturated, and preserve UI icon classifications by replacing a non-UI entry before deterministic hash-slot replacement. Query is a bounded linear scan over at most 512 entries.

Rejected Alternatives: Keeping `NativeParallelHashMap` was rejected because it is private persistent native memory outside the Vault. Adding new `BufferID` fields to core Vault headers was rejected because the group cache is not rollback-critical and touching core headers for a UI mip gate would widen the compile surface. A managed `Dictionary<uint, byte>` was rejected because it would reintroduce growable managed storage and hash-table allocation. Dropping UI classification entirely was rejected because the mip gate is the low-VRAM visual-protection path.

Scalability potential: Low devices preserve UI mip protection with no private native allocation and no growable managed map. Middle/high/ultra retain the same gate semantics; high-memory devices still exit early by graphics memory threshold, while constrained devices get bounded O(512) classification.

Hardware Impact: Static estimate only. R31 removes one private persistent native hash map and its sentinel registration/disposal from dispatcher lifetime. Query cost changes from hash lookup to bounded linear scan; at 512 entries this is acceptable because the path is main-thread request/gate cadence and removes native allocation ownership risk. No profiler microseconds are claimed until Unity import/compile reaches SHINOBU code.

## R31 Verification

- `rg -n "Unity\\.Collections|NativeParallelHashMap|Allocator\\.Persistent|NativeMemorySentinel|_addressableGroupMap|EnsureAddressableGroupMap" Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs`
  - no results.
- `rg -n "_addressableGroupKeys|_addressableGroupValues|_addressableGroupCount|ClearAddressableGroupMap|RegisterAddressableGroupInternal|IsUiIconGroup" Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs`
  - expected fixed cache fields and register/query/clear paths only.
- `git diff --check -- Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs`
  - LF-to-CRLF warning only.

## Compile Boundary R31

Problem: Full compile verification remains blocked by the known external missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` item in `Hecton8.Core.csproj`.

Solution: No build launched in R31. Static verification only.

Rejected Alternatives: Rerunning a known pre-SHINOBU abort was rejected by user command discipline and AGENTS build discipline.

Scalability potential: No runtime tier change.

Hardware Impact: Avoids local CPU/IO burn while the compile wall remains external.

## AssetLifecycleGovernor DataVault Cold-Cache Guard R32

Problem: `AssetLifecycleGovernor.TryResolveHeapSanitizerVaultBuffers()` still used `_dataVault != null ? _dataVault : GlobalRegistry.DataVault`. That resolver is reachable through tracker/cache/telemetry view helpers and cold tick scheduling, so the fallback was a hidden registry poll outside the explicit dependency cache boundary.

Solution: Make `TryResolveHeapSanitizerVaultBuffers()` consume `_dataVault` only. Cache dependencies before native storage resolution in `Awake()`, `OnEnable()`, and `Start()`. `Start()` retries native storage only if earlier lifecycle calls failed before the Vault became available. Add `GlobalRegistryServiceSlot.DataVault` hot-swap handling that completes any scheduled TTL job against the old vault, swaps the cached vault, invalidates stale handle descriptors, and reacquires storage only when a new vault exists.

Rejected Alternatives: Leaving the fallback was rejected because it hides a global lookup in a resolver used by runtime view paths. Adding a new global route was rejected because DataVault is already the route. Forcing a raw addressable release during Vault rebound was rejected because release authority remains the governor's blind/panic gate and DataVault rebound should not become a visible-frame unload cascade.

Scalability potential: Low devices avoid service-locator churn and hidden failed lookup retries in cold tick memory paths. Middle/high/ultra retain the same continuous TTL/residency behavior; this is authority-route hardening, not a visual tier branch.

Hardware Impact: Static estimate only. R32 removes one runtime fallback registry lookup from every Vault-buffer resolution attempt after the cold cache is established. No profiler microseconds are claimed until Unity import/compile reaches SHINOBU code.

## R32 Verification

- `rg -n "GlobalRegistry\\.DataVault" Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs`
  - one hit: cold `CacheDependencies()` assignment.
- `rg -n "private bool TryResolveHeapSanitizerVaultBuffers|IDataVault vault = _dataVault|GlobalRegistryServiceSlot\\.DataVault|CompleteTtlEvaluationForTeardown\\(\\)|InvalidateVaultHandleDescriptors\\(\\)" Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs`
  - expected resolver, cached-vault assignment, and hot-swap invalidation paths only.
- `rg -n "Addressables\\.Release\\(|Addressables\\.ReleaseInstance\\(" Assets/_Project/Scripts`
  - `Addressables.Release(` remains only at `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:4332`.
  - `Addressables.ReleaseInstance(` remains only at `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2275`.

## Compile Boundary R32

Problem: Full compile verification remains blocked by the known external missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` item in `Hecton8.Core.csproj`.

Solution: No build launched in R32. Static verification only.

Rejected Alternatives: Rerunning a known pre-SHINOBU abort was rejected by user command discipline and AGENTS build discipline.

Scalability potential: No runtime tier change.

Hardware Impact: Avoids local CPU/IO burn while the compile wall remains external.

## AssetLoadDispatcher Continuous Quality Slot Curve R33

Problem: `AssetLoadDispatcher.ResolveAllowedConcurrentLoads()` still used discrete RAM-pressure cliffs. Tier 3/4 loads switched at `ramPressure > 0.85f`, tier 5/6 loads switched at `ramPressure > 0.75f`, and the dispatcher did not consume `HomeostasisBrain.GlobalQualityWeight`. That violated the active systemic mandate against binary quality switches and could cause abrupt background streaming starvation/restoration under thermal pressure.

Solution: Add scalar `Unity.Mathematics` quality math and route each priority band through `ResolveContinuousLoadSlots()`. The helper blends pressure collapse and quality collapse with `math.smoothstep`, `math.lerp`, `math.max`, and `math.saturate`, then rounds only at the final discrete dispatch-permit boundary. Critical bands retain minimum permits; noncritical bands can collapse to zero permits as pressure rises or quality falls.

Rejected Alternatives: Keeping the existing RAM thresholds was rejected because it is a visible binary cliff. Adding an `IsLowEndHardware` branch was rejected by mandate. Dispatching every queued load and relying on Addressables internals was rejected because it would move stutter to the backend. Adding a new registry/service route for quality was rejected because `HomeostasisBrain.GlobalQualityWeight` is the existing global scalar.

Scalability potential: Low devices: background tier 5/6 streaming slides toward zero permits while critical tier 0/1 keeps a reduced minimum, preserving control and UI asset readiness. Middle devices: slot counts interpolate between max/min rather than flipping. High devices: pressure stays low and quality near 1.0, so near-critical and background loads retain higher permits. Ultra devices: the dispatcher feeds more ready tickets without changing release authority, buying richer residency and fewer fallback impostors.

Hardware Impact: Static estimate only. R33 does not claim microseconds saved. It replaces two binary threshold branches with O(1) scalar math and prevents bursty load-slot oscillation under thermal pressure. Runtime frame/GC proof remains blocked until Unity compile/import reaches SHINOBU code.

## R33 Verification

- `rg -n "ramPressure >|PressureFactor >|IsLowEndHardware|if \\(.*Quality|GlobalRegistry\\.(AssetLifecycle|VRAMMonitor|VRAMPressure|DataVault)|List<|NativeParallelHashMap|Allocator\\.Persistent|Addressables\\.Release\\(" Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs`
  - Remaining hits are `QualitySettings.globalTextureMipmapLimit` comparison and cold `CacheDependencies()` assignments.
- `rg -n "ResolveAllowedConcurrentLoads|ResolveContinuousLoadSlots|math\\.smoothstep|math\\.lerp|Tier34CriticalSlots|Tier56WarningSlots" Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs`
  - Expected continuous load-slot resolver and priority-band min/max constants only.
- `rg -n "Addressables\\.Release\\(|Addressables\\.ReleaseInstance\\(" Assets/_Project/Scripts`
  - `Addressables.Release(` remains only at `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:4332`.
  - `Addressables.ReleaseInstance(` remains only at `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2275`.
- `git diff --check -- Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs`
  - LF-to-CRLF warning only.

## Compile Boundary R33

Problem: Full compile verification remains blocked by the known external missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` item in `Hecton8.Core.csproj`.

Solution: No build launched in R33. Static verification only.

Rejected Alternatives: Rerunning a known pre-SHINOBU abort was rejected by user command discipline and AGENTS build discipline.

Scalability potential: No extra runtime tier change beyond the dispatcher quality curve.

Hardware Impact: Avoids local CPU/IO burn while the compile wall remains external.

## VRAMPressureMonitor Quality-Weighted Pressure Response R34

Problem: After R33, `VRAMPressureMonitor` still had hard warning/emergency cliffs. Soft pressure, emergency eviction, RAM warning, and LOD aggression could flip around fixed fractions without consuming `GlobalQualityWeight`. That preserved abrupt behavior in the system that directly controls mip bias, LOD bias, release draining, and emergency eviction.

Solution: Add `Unity.Mathematics` scalar helpers. `ResolveQualityAdjustedFraction()` maps low-quality and high-quality threshold fractions through `math.smoothstep(0.15f, 0.85f, GlobalQualityWeight)`. Soft and emergency pressure now resolve to continuous response values through `math.smoothstep`. Release-drain and eviction counts are derived from `ResolveBudgetedPressureCount()`. LOD aggression now lerps `QualitySettings.lodBias` and `BrgLodDistanceScalar` from 1.0 toward 0.5, while hard red-zone pressure still forces full collapse.

Rejected Alternatives: Keeping fixed warning/emergency thresholds was rejected because it keeps binary thermal behavior. Removing red-zone safety was rejected because actual budget exhaustion must remain fail-fast. Creating a new thermal service was rejected because `HomeostasisBrain.GlobalQualityWeight` is the existing quality scalar. Increasing eviction counts unconditionally was rejected because it would cause visible churn on high-end hardware under transient pressure.

Scalability potential: Low devices start pressure response earlier and drain/evict progressively, preserving UI/control assets while shedding distant HLOD/world-prefab residency. Middle devices get partial release-drain and partial LOD bias instead of a cliff. High devices remain near authored thresholds. Ultra devices hold richer residency longer unless actual pressure rises, preserving visual overkill without changing the hard red-zone safety path.

Hardware Impact: Static estimate only. R34 does not claim measured microseconds. It replaces warning/emergency branch cliffs with O(1) scalar response math and bounds release/eviction work to response-scaled counts. Runtime frame/GC proof remains blocked until Unity compile/import reaches SHINOBU code.

## R34 Verification

- `rg -n "ResolveSoftVramPressureThresholdBytes|ResolveLodAggressionThresholdBytes|VramPressureFactor >= emergencyVramFraction|RamPressureFactor >= RamEmergencyFraction|RamPressureFactor >= RamWarningFraction|VramPressureFactor >= warningVramFraction" Assets/_Project/Scripts/Optimization/VRAMPressureMonitor.cs`
  - no results.
- `rg -n "ResolveQualityAdjustedFraction|ResolveSoftPressureResponse|ResolveEmergencyPressureResponse|ResolveBudgetedPressureCount|HomeostasisBrain\\.GlobalQualityWeight|VramPressureFactor >= 1f" Assets/_Project/Scripts/Optimization/VRAMPressureMonitor.cs`
  - expected quality response helpers and one red-zone fail-safe only.
- `rg -n "GlobalRegistry\\.(VRAMMonitor|AssetLifecycle|PlayerInventory|RenderTexturePool|VRAMPressure)|Addressables\\.Release\\(|NativeParallelHashMap|Allocator\\.Persistent|List<|Dictionary<|HashSet<|Queue<" Assets/_Project/Scripts/Optimization/VRAMPressureMonitor.cs`
  - remaining hits are service registration and cold-cache assignments only.
- `git diff --check -- Assets/_Project/Scripts/Optimization/VRAMPressureMonitor.cs`
  - LF-to-CRLF warning only.

## Compile Boundary R34

Problem: Full compile verification remains blocked by the known external missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` item in `Hecton8.Core.csproj`.

Solution: No build launched in R34. Static verification only.

Rejected Alternatives: Rerunning a known pre-SHINOBU abort was rejected by user command discipline and AGENTS build discipline.

Scalability potential: No extra runtime tier change beyond the monitor response curve.

Hardware Impact: Avoids local CPU/IO burn while the compile wall remains external.

## VRAMPressureMonitor Continuous Mip Bias Closure R35

Problem: R34 replaced most hard pressure gates, but `ApplyMipBias()` still had a discrete mip branch. Any positive soft-pressure response or forced-mip byte threshold crossing produced the same single-step downgrade through `Mathf.Max(_baselineMipLimit, 1)`. That preserved a visible quality cliff and could restore an already downgraded mip limit as soon as pressure dropped below the old byte threshold instead of the restore band.

Solution: Replace the boolean/byte-threshold mip path with scalar response math. `ApplyMipBias()` now computes `softPressureResponse`, `forcedMipResponse`, and final `mipPressureResponse`, all quality-adjusted through `HomeostasisBrain.GlobalQualityWeight` and `math.smoothstep`. `ResolveMipLimitDelta()` converts that scalar into the final integer Unity mip-limit delta at the last possible boundary. Red-zone pressure forces a two-step collapse; small nonzero response values hold the active mip limit instead of restoring early.

Rejected Alternatives: Keeping `IsSoftVramPressureActive()` was rejected because it made any soft pressure binary. Keeping `ResolveForcedMipDropThresholdBytes()` was rejected because it hid another branch cliff in a byte comparison. Driving per-texture mips directly was rejected for this pass because the global monitor already owns the active `QualitySettings.globalTextureMipmapLimit`, and per-texture migration would require a wider texture-ledger route card.

Scalability potential: Low devices enter mip downgrade progressively as quality falls and pressure rises, with red-zone forcing a stronger two-step collapse. Middle devices hold intermediate global mips without flickering at the soft boundary. High devices keep authored baseline longer because quality-adjusted fractions stay near authored thresholds. Ultra devices retain full mip residency unless real pressure rises, preserving visual overkill.

Hardware Impact: Static estimate only. R35 removes one boolean soft-pressure route and one forced-mip byte cliff from the 90-frame pressure sample path. Claimed measured savings: 0 microseconds because no Unity Profiler/GCMonitor proof is available behind the compile wall. Expected impact is lower mip-thrash and fewer abrupt texture residency changes under MX350/Quest-class pressure.

## R35 Verification

- `rg -n "softVramPressure|forcedMipThresholdBytes|ResolveForcedMipDropThresholdBytes|IsSoftVramPressureActive|Mathf.Max\\(_baselineMipLimit, 1\\)|LastUsedVramBytes >= forced" Assets/_Project/Scripts/Optimization/VRAMPressureMonitor.cs`
  - no results.
- `rg -n "ResolveForcedMipResponse|ResolveMipLimitDelta|mipPressureResponse|math\\.lerp\\(0f, 2f|ResolveQualityAdjustedFraction|VramPressureFactor >= 1f" Assets/_Project/Scripts/Optimization/VRAMPressureMonitor.cs`
  - expected continuous mip-response helpers and one red-zone fail-safe only.
- `git diff --check -- Assets/_Project/Scripts/Optimization/VRAMPressureMonitor.cs`
  - LF-to-CRLF warning only.

## Compile Boundary R35

Problem: Full compile verification remains blocked by the known external missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` item in `Hecton8.Core.csproj`.

Solution: No build launched in R35. Static verification only.

Rejected Alternatives: Rerunning a known pre-SHINOBU abort was rejected by user command discipline and AGENTS build discipline.

Scalability potential: No extra runtime tier change beyond the continuous mip-bias closure.

Hardware Impact: Avoids local CPU/IO burn while the compile wall remains external.

## Dispatcher UI Mip Gate Ownership Collapse R36

Problem: R35 closed the monitor mip cliff, but R36 scan found that `AssetLoadDispatcher` still wrote `QualitySettings.globalTextureMipmapLimit` directly. That made global texture mip state a two-owner fact between dispatcher and monitor. The dispatcher also kept private baseline/active mip state and a binary low-VRAM device gate from the old route.

Solution: Remove dispatcher-owned mip baseline/active state and route the UI mip response into `VRAMPressureMonitor.SetExternalMipPressureResponse(...)`. Dispatcher now computes only a scalar `gateResponse` from current VRAM pressure, graphics budget, and continuous `GlobalQualityWeight`. `VRAMPressureMonitor` owns `_externalMipPressureResponse`, refreshes current VRAM pressure from the dispatcher-fed byte count, combines external/soft/forced/red-zone pressure in `ApplyMipBias()`, and remains the writer for `QualitySettings.globalTextureMipmapLimit`.

Rejected Alternatives: Leaving direct dispatcher writes was rejected because it violates one fact -> one owner. Moving all UI gate math into the monitor was rejected because dispatcher owns the UI group classification cache and request-context telemetry. Keeping the binary `LowVramDeviceThresholdMb` early return was rejected because it is an explicit low-end hardware switch. Removing the UI gate entirely was rejected because it protects UI icons under pressure without raw release churn.

Scalability potential: Low devices now feed a larger scalar external pressure into the monitor and collapse mips via the same global response path as normal pressure. Middle devices get hysteresis through active external response plus restore fraction instead of byte cliffs. High/ultra devices keep full UI texture residency until actual pressure fraction and quality curve demand shedding.

Hardware Impact: Static estimate only. R36 removes two direct `QualitySettings.globalTextureMipmapLimit` writes and three dispatcher mip-state fields, replacing them with one scalar call into the monitor. Claimed measured savings: 0 microseconds because no Unity Profiler/GCMonitor proof is available behind the compile wall. Expected impact is authority correctness and less mip tug-of-war, not a measured CPU gain.

## R36 Verification

- `rg -n "QualitySettings\\.globalTextureMipmapLimit|_baselineGlobalTextureMipLimit|_activeGlobalTextureMipLimit|_mipGateInitialized|CaptureMipBiasBaseline|UiMipDowngradeThresholdBytes|UiMipRestoreThresholdBytes|LowVramDeviceThresholdMb|totalVramBytes >= UiMip|totalVramBytes <= UiMip" Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs`
  - no results.
- `rg -n "SetExternalMipPressureResponse|_externalMipPressureResponse|VramPressureFactor = _runtimeTotalVramBudgetBytes|QualitySettings\\.globalTextureMipmapLimit" Assets/_Project/Scripts/Optimization/VRAMPressureMonitor.cs`
  - expected external pressure field/method and monitor-owned global mip write only.
- `git diff --check -- Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs Assets/_Project/Scripts/Optimization/VRAMPressureMonitor.cs`
  - LF-to-CRLF warnings only.

## Compile Boundary R36

Problem: Full compile verification remains blocked by the known external missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` item in `Hecton8.Core.csproj`.

Solution: No build launched in R36. Static verification only.

Rejected Alternatives: Rerunning a known pre-SHINOBU abort was rejected by user command discipline and AGENTS build discipline.

Scalability potential: No extra runtime tier change beyond the external UI pressure route.

Hardware Impact: Avoids local CPU/IO burn while the compile wall remains external.

## VRAMEnforcer Continuous Bootstrap Budget R37

Problem: `VRAMEnforcer` still encoded a hard MX350/shared-memory split: `DetectedGraphicsMemoryMb <= 2048` activated clamps, boid population used fixed low/shared scale constants, and texture mip clamp selected half vs shared-memory mip limits with branch logic. This was cold/bootstrap code, but it still trained downstream systems to reason in binary hardware classes.

Solution: Replace the binary budget selection with scalar weight math. `ResolveHardwareBudgetWeight()` uses `math.smoothstep(1024 MB, 8192 MB, detectedGraphicsMemoryMb)` and branchless `math.select` for shared-memory ceiling. `ApplyBoidPopulationBudget()` combines hardware scale and `HomeostasisBrain.GlobalQualityWeight` scale through `math.lerp` and `math.min`. Bootstrap mip clamp now derives an integer mip minimum only after a continuous `math.lerp(2, 0, usableWeight)` response.

Rejected Alternatives: Removing `VRAMEnforcer` entirely was rejected because `GameBootstrapper` and `SargassumMicroFaunaBoids` still call it as a cold budget guard. Leaving the boolean low-VRAM return was rejected because it violated the no binary hardware gate rule. Moving boid budgeting into the biota domain was rejected because the current call site already depends on this optimization facade and cross-domain surgery would widen the batch.

Scalability potential: Low devices and UMA devices receive stronger bootstrap mip/boid clamp from low hardware weight and quality curve. Middle devices land between 0.4 and 1.0 boid scale instead of jumping. High/ultra devices with quality near 1.0 resolve to scale 1.0 and mip minimum 0, preserving visual overkill.

Hardware Impact: Static estimate only. R37 replaces one binary hardware threshold and two fixed low/shared population scales with O(1) scalar math. Claimed measured savings: 0 microseconds because no Unity Profiler/GCMonitor proof is available behind the compile wall. Expected impact is smoother asset/fauna budget behavior across MX350 -> midrange -> RTX hardware.

## R37 Verification

- `rg -n "LowVramGraphicsMemoryMbThreshold|HalfResolutionTextureMipLimit|SharedMemoryTextureMipLimit|LowVramBoidPopulationScale|SharedMemoryBoidPopulationScale|DetectedGraphicsMemoryMb > 0 &&|\\? SharedMemory|graphicsMemoryMb > 0 \\?|if \\(!_lowVramBudgetActive\\)|<= LowVram" Assets/_Project/Scripts/Optimization/VRAMEnforcer.cs`
  - no results.
- `rg -n "ResolveHardwareBudgetWeight|ResolveQualityCurve|math\\.smoothstep|math\\.lerp|math\\.select|HomeostasisBrain\\.GlobalQualityWeight|QualitySettings\\.globalTextureMipmapLimit" Assets/_Project/Scripts/Optimization/VRAMEnforcer.cs`
  - expected continuous budget helpers and bootstrap/editor `QualitySettings` clamps only.
- `git diff --check -- Assets/_Project/Scripts/Optimization/VRAMEnforcer.cs`
  - LF-to-CRLF warning only.

## Compile Boundary R37

Problem: Full compile verification remains blocked by the known external missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` item in `Hecton8.Core.csproj`.

Solution: No build launched in R37. Static verification only.

Rejected Alternatives: Rerunning a known pre-SHINOBU abort was rejected by user command discipline and AGENTS build discipline.

Scalability potential: No extra runtime tier change beyond continuous bootstrap hardware/quality weighting.

Hardware Impact: Avoids local CPU/IO burn while the compile wall remains external.
