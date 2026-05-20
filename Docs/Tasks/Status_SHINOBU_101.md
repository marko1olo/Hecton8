# Status_SHINOBU_101

Agent: SHINOBU_101
Role: ADDRESSABLES_HEAP_DEFRAGMENTER
Domain: ECHELON 1: CORE & MEMORY INFRASTRUCTURE
Task Count: 20
Status: PENDING VERIFICATION

## Source Of Truth

- Active `Docs/Tasks/CURRENT_BATCH.md` no longer contains `<AGENT_PROMPT id="SHINOBU_101">`.
- Durable prior state was recovered from `Docs/Archive/Batch010/Tasks/Status_SHINOBU_101.md` and `Docs/Archive/Batch010/AgentLogs/Rationale_SHINOBU_101.md`.
- Current user continuation explicitly targets the same SHINOBU_101 Addressables heap/release-gate work.

## Task Matrix

- [x] Task 01 MANAGED_DICTIONARY_ERADICATION | Archived Batch010 pass remains current: hot Addressables registry/queue/list paths moved to fixed storage and Vault map.
- [x] Task 02 DEFERRED_RELEASE_QUEUE_PURGE | Archived Batch010 pass remains current: normal release drains through blind/panic gate.
- [x] Task 03 CS1612_ENCAPSULATION_PURGE | Archived Batch010 pass remains current: hot DTO mutation uses fields/ref access, not property copies.
- [x] Task 04 ARM64_PADDING_RECONSTRUCTION | Archived Batch010 pass remains current: primary DTOs are explicit 64-byte layouts.
- [x] Task 05 EMERGENCY_MOCK_CACHE_PROFILES | Archived Batch010 pass remains current: deterministic fallback cache profiles exist.
- [x] Task 06 VAULT_OPEN_ADDRESS_HASH_TABLE | Archived Batch010 pass remains current: fixed open-address handle map is Vault-owned.
- [x] Task 07 BURST_TTL_EVALUATION_KERNEL | Archived Batch010 pass remains current: Burst TTL job uses `[NoAlias]` and map-entry iteration.
- [x] Task 08 SAFE_FRAME_RELEASE_GATE | R24 strengthened: project-wide raw `Addressables.Release(` scan reports one line inside `AssetLifecycleGovernor.TryExecuteOrDeferBlindFrameRelease`.
- [x] Task 09 THE_DEAR_LIE_IMPOSTOR_MESH | Archived Batch010 pass remains current: checkerboard/cube facade hides async Addressables latency.
- [x] Task 10 VRAM_PANIC_EVICTION_ROUTING | Archived Batch010 pass remains current: panic eviction uses bounded furthest unreferenced candidates.
- [x] Task 11 CONTINUOUS_SCALABILITY_CACHE_SIZING | Archived Batch010 pass remains current: TTL is driven by continuous `GlobalQualityWeight`.
- [x] Task 12 ATOMIC_REFERENCE_COUNTING | Archived Batch010 pass remains current: native ref ownership uses interlocked/compare-exchange guards.
- [x] Task 13 AUP_PRECISION_EVICTION_SCORING | Archived Batch010 pass remains current: scoring subtracts AUP before local float math.
- [x] Task 14 ASSET_BUNDLE_FRAGMENTATION_DEFRAG | Archived Batch010 pass remains current: bundle prefix sharing inflates TTL and map compaction rebuilds tombstones.
- [x] Task 15 NARRATIVE_PINNING_LOCK | Archived Batch010 pass remains current: pinned handles skip TTL/panic release.
- [x] Task 16 ZERO_INIT_OVERHEAD_BYPASS | Archived Batch010 pass remains current: Vault buffers use uninitialized allocation plus cold clear.
- [x] Task 17 TELEMETRY_HEAP_RECORDER | Archived Batch010 pass remains current: 300-frame 64B telemetry ring and dumps exist.
- [x] Task 18 MEMORY_TUNER_EDITOR_WINDOW | Archived Batch010 pass remains current: UI Toolkit facade replaces IMGUI.
- [x] Task 19 CSV_OVERRIDE_INGESTOR | Archived Batch010 pass remains current: CSV scratch buffer is Vault-owned and parsed with spans.
- [x] Task 20 LIVE_LEAK_DETECTOR_GIZMO | Archived Batch010 pass remains current: editor leak banner scans native map.

## R24 Re-Entry Review

- [x] Read active/archived status and rationale before coding.
- [x] Rechecked active `CURRENT_BATCH.md`; SHINOBU_101 prompt is absent from active batch, archived Batch010 files are the current durable assignment record.
- [x] Read AGENTS, binary payload ledger, global authority boundaries, domain file, and seven relevant `.agents-skills` mandates.
- [x] Removed hidden runtime fallback reads of `GlobalRegistry.AssetLifecycle` from `ContentRuntimeServices` and `WorldChunkResidencyManager` Addressables release/acquire helpers.
- [x] `ContentRuntimeServices.OnEnable` and `Start` now cold-cache dependencies before release-capable paths.
- [x] `WorldChunkResidencyManager` Addressables acquire/loaded/release paths now consume cached `_assetLifecycleGovernor` only and fail closed if unavailable.
- [x] Static scan: `ResolveAssetLifecycleGovernor|GlobalRegistry.AssetLifecycle` in touched owners reports only cold cache methods.
- [x] Static scan: `Addressables.Release(` under `Assets/_Project/Scripts` reports only `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:4218`.
- [x] Static scan: `Addressables.ReleaseInstance(` reports only `GameBootstrapper.cs:2274`; this is UI prefab instance teardown, not a raw asset/dependency handle.
- [ ] Compile verification R24 | PENDING VERIFICATION: not launched. R22 proved `Hecton8.Core.csproj` currently aborts before SHINOBU verification on missing external `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`; rerunning would waste IO and violate command discipline until that compile item is restored or removed by its owner.

## R25 Optimization-Lane Cold-DI Review

- [x] Identified additional runtime service-locator reads in `VRAMPressureMonitor.SampleAndRespond`, `VRAMPressureMonitor.RunPressureEviction`, `AssetLoadDispatcher.ForceDrainDeferredReleases`, `AssetLoadDispatcher.EvaluateUiMipBiasGate`, and `AssetLoadDispatcher.ResolveAllowedConcurrentLoads`.
- [x] `VRAMPressureMonitor` now cold-caches `VRAMMonitor`, `AssetLifecycleGovernor`, `IPlayerInventoryService`, and `RenderTexturePool`.
- [x] `AssetLoadDispatcher` now cold-caches `VRAMMonitor`, `VRAMPressureMonitor`, and `AssetLifecycleGovernor`.
- [x] Both classes implement `IGlobalRegistryHotSwapListener` so late service replacement updates cached fields without per-tick registry polling.
- [x] Static scan: `GlobalRegistry.(AssetLifecycle|VRAMMonitor|VRAMPressure|PlayerInventory|RenderTexturePool)` in the two patched Optimization files now reports only service registration checks and cache/rebind setup, not pressure sample or dispatch budget math.
- [x] Static scan: `Addressables.Release(` under `Assets/_Project/Scripts` still reports only `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:4218`.
- [ ] Compile verification R25 | PENDING VERIFICATION: not launched. Known external missing Construction source still blocks `Hecton8.Core.csproj` before SHINOBU verification.

## R26 Hot-Swap Lifecycle Closure

- [x] `ContentAuthorityRuntime`, `WorldChunkResidencyManager`, `AssetLoadDispatcher`, and `VRAMPressureMonitor` now implement hot-swap rebinding for the cached services they consume in release/pressure/runtime cadence.
- [x] `WorldChunkResidencyManager.DisposeInternal()` now unregisters tick/backpressure routes and the hot-swap listener, covering external `Dispose()` and `OnDestroy()` paths beyond the normal `OnDisable()` path.
- [x] `WorldChunkResidencyManager.ClearColdServiceCache()` now clears `_ambientBiotaService` so no disabled/disposed instance retains a stale owner pointer.
- [x] Static scan: `GlobalRegistry.(AssetLifecycle|VRAMMonitor|VRAMPressure|PlayerInventory|RenderTexturePool|DataVault|JobAdmission|MacroDatabase|ObjectPool|AmbientBiota|SaveRuntime|AsyncPersistence)` in the four touched owners reports only registration/cold-cache/rebind boundaries.
- [x] Static scan: `Addressables.Release(` remains single-route through `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:4218`; `Addressables.ReleaseInstance(` remains only Bootstrap UI instance teardown.
- [x] Static scan: `git diff --check` on the four runtime files reports LF-to-CRLF warnings only.
- [ ] Compile verification R26 | PENDING VERIFICATION: not launched. Known external missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` still blocks `Hecton8.Core.csproj` before SHINOBU verification.

## R27 Governor Cold-DI Closure

- [x] Re-read active SHINOBU status/rationale plus active architecture docs and relevant mandates before code edits.
- [x] `AssetLifecycleGovernor` now hot-swap caches dispatcher, VRAM pressure, player AUP source, player inventory, and scanner-interference UI. Live load completion, retry, release, hard-reaper UI, and distant chunk release paths consume cached fields only.
- [x] `AssetLoadDispatcher` static helper entrypoints now route through owner-local `s_registeredInstance`, not `GlobalRegistry.AssetLoadDispatcher`, removing static helper registry reads from UI mip and forced release paths.
- [x] `ItemCatalog` world-prefab Addressables path now cold-caches `AssetLifecycleGovernor`, `AssetLoadDispatcher`, and `IPlayerRuntimeContext` with hot-swap rebinding. Runtime queue/consume/ack/release/player-AUP helpers no longer query the registry directly.
- [x] `ItemCatalog` no longer lazy-allocates its world-prefab release queue, release set, or dispatch scratch list in release/dispatch methods; these managed containers are created/cleared during catalog rebuild.
- [x] Static scan: `Addressables.Release(` under `Assets/_Project/Scripts` reports only `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:4303`.
- [x] Static scan: `Addressables.ReleaseInstance(` reports only `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2275`; this remains UI instance teardown, not a dependency-handle release bypass.
- [x] Static scan: `GlobalRegistry.AssetLoadDispatcher` project hits are now bootstrap publication, optimization bootstrap resolution, registration/cold-cache boundaries, or governor/catalog cache setup; runtime ItemCatalog dispatch/release helpers no longer use it directly.
- [x] Static scan: `git diff --check` on R27 runtime files reports LF-to-CRLF warnings only.
- [ ] Compile verification R27 | PENDING VERIFICATION: not launched. Known external missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` still blocks `Hecton8.Core.csproj` before SHINOBU verification; rerunning would violate command discipline.

## R28 ItemCatalog Fixed Scratch Closure

- [x] Re-read active SHINOBU status/rationale before edits and rechecked active `CURRENT_BATCH.md`; SHINOBU_101 remains absent from the active batch, so archived/status authority remains current.
- [x] Replaced `ItemCatalog` world-prefab deferred release `Queue<int>` plus `HashSet<int>` with a fixed cold-allocated `int[]` ring sized from authored addressable entries plus GUID fallbacks.
- [x] Replaced `ItemCatalog` world-prefab dispatch `List<int>` scratch with a fixed cold-allocated `int[]` scratch buffer and explicit count.
- [x] `DrainDeferredWorldPrefabReleases()` now processes at most the initial pending count for the frame; a failed staged release is requeued once and cannot spin indefinitely in the same drain call.
- [x] `ReleaseWorldPrefabRuntimeRecord()` removes stale pending release entries through the fixed ring compaction helper, not through a managed set.
- [x] Static scan: old `_pendingWorldPrefabReleaseQueue`, `_pendingWorldPrefabReleaseSet`, `new Queue<int>`, `new HashSet<int>`, `new List<int>(32)`, and dispatch scratch `.Add/.Count/.Clear` usages in `ItemCatalog.cs` report no results.
- [x] Static scan: `Addressables.Release(` remains only at `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:4303`; `Addressables.ReleaseInstance(` remains only at `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2275`.
- [x] Static scan: `GlobalRegistry.(AssetLoadDispatcher|AssetLifecycle|Player)` in `ItemCatalog.cs` reports only cold `CacheRuntimeServices()` assignments.
- [x] Static scan: `git diff --check` on SHINOBU runtime files reports LF-to-CRLF warnings only.
- [ ] Compile verification R28 | PENDING VERIFICATION: not launched. Known external missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` still blocks `Hecton8.Core.csproj` before SHINOBU verification, and the user explicitly forbade needless build/rebuild runs.

## R29 ItemCatalog Runtime Rebuild Guard

- [x] Re-read active SHINOBU status/rationale, current batch marker, binary payload ledger, and relevant `.agents-skills` mandates before code edits.
- [x] `QueueWorldPrefabPrewarm()` and `TryGetLoadedWorldPrefab()` no longer call `RebuildWorldPrefabLookup()` from gameplay callers when world-prefab lookup fields are missing.
- [x] Added `TryEnsureWorldPrefabLookupReady()`: returns true for prebuilt lookup state, performs cold rebuild only outside Play Mode, and fails closed during Play Mode.
- [x] Direct world-prefab fallback now uses a no-allocation linear scan over `allItems` and `_runtimeItems` when `_hashLookup` is absent during Play Mode, avoiding `FindByHash()` -> `RebuildLookup()` allocation.
- [x] `ItemCatalog.OnDisable()` now queues and drains all world-prefab handles through the governor route before unregistering hot-swap and clearing cached services.
- [x] Static scan: `RebuildWorldPrefabLookup()` call sites are now `OnEnable`, editor `OnValidate`, and the non-playing branch of `TryEnsureWorldPrefabLookupReady()`.
- [x] Static scan: old queue/set/list release scratch patterns and accidental `item.HashId` usage report no results.
- [x] Static scan: `Addressables.Release(` remains only at `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:4303`; `Addressables.ReleaseInstance(` remains only at `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2275`.
- [x] Static scan: `git diff --check` on `ItemCatalog.cs` reports LF-to-CRLF warnings only.
- [ ] Compile verification R29 | PENDING VERIFICATION: not launched. Known external missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` still blocks `Hecton8.Core.csproj` before SHINOBU verification, and the user explicitly forbade needless build/rebuild runs.

## R30 AssetLoadDispatcher Fixed Buffer Rewrite

- [x] Re-scanned SHINOBU-touched files for lazy managed allocation markers and release-route escape hatches before edits.
- [x] Replaced `AssetLoadDispatcher` queued request `List<AssetDispatchRequest>` with fixed `AssetDispatchRequest[128]` plus `_queuedRequestCount`.
- [x] Replaced ready ticket `List<AssetDispatchTicket>` with fixed `AssetDispatchTicket[32]` plus `_readyTicketCount`; serialized `maxReadyTicketCount` is clamped to buffer length.
- [x] Replaced inflight request `List<AssetDispatchRequest>` with fixed `AssetDispatchRequest[64]` plus `_inflightRequestCount`.
- [x] Replaced generic `RemoveAtSwapBack(List<T>)` with typed fixed-array swap-back removal helpers that clear vacated slots.
- [x] `Enqueue()` now fails closed when the fixed queued buffer or ready ticket buffer is saturated; no resize allocation can occur under load.
- [x] `DispatchWithinBudget()` now refuses dispatch when ready ticket limit is zero, ready buffer is full, or inflight buffer is full.
- [x] Static scan: `List<`, `_queuedRequests.Count`, `_readyTickets.Count`, `_inflightRequests.Count`, `.Add(`, `RemoveAt(`, `RemoveAtSwapBack`, and `using System.Collections.Generic` in `AssetLoadDispatcher.cs` report no results.
- [x] Static scan: `git diff --check -- Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs` reports LF-to-CRLF warning only.
- [ ] Compile verification R30 | PENDING VERIFICATION: not launched. Known external missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` still blocks `Hecton8.Core.csproj` before SHINOBU verification, and the user explicitly forbade needless build/rebuild runs.

## R31 AssetLoadDispatcher Native Group Map Eviction

- [x] Re-scanned SHINOBU optimization/catalog files for persistent native containers after R30.
- [x] Identified `AssetLoadDispatcher._addressableGroupMap` as a private persistent `NativeParallelHashMap<uint, byte>` used only by the UI mip classification gate.
- [x] Replaced `_addressableGroupMap` with fixed owner-local `uint[512]`/`byte[512]` arrays and an explicit `_addressableGroupCount`.
- [x] Removed `Unity.Collections`, `Allocator.Persistent`, and `NativeMemorySentinel` usage from `AssetLoadDispatcher.cs`.
- [x] `RegisterAddressableGroupInternal()` now updates existing entries, appends while capacity remains, drops non-UI classifications when saturated, and preserves UI classifications by replacing non-UI entries before deterministic hash-slot replacement.
- [x] `IsUiIconGroup()` now uses a bounded linear scan over the fixed cache and allocates nothing.
- [x] `OnDestroy()` now clears the fixed group cache instead of disposing a private native container.
- [x] Static scan: `Unity.Collections`, `NativeParallelHashMap`, `Allocator.Persistent`, `NativeMemorySentinel`, `_addressableGroupMap`, and `EnsureAddressableGroupMap` in `AssetLoadDispatcher.cs` report no results.
- [x] Static scan: fixed group cache symbols resolve only to the expected field, register, query, and clear paths.
- [x] Static scan: `git diff --check -- Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs` reports LF-to-CRLF warning only.
- [ ] Compile verification R31 | PENDING VERIFICATION: not launched. Known external missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` still blocks `Hecton8.Core.csproj` before SHINOBU verification, and the user explicitly forbade needless build/rebuild runs.

## R32 AssetLifecycleGovernor DataVault Cold-Cache Guard

- [x] Re-scanned `AssetLifecycleGovernor` for runtime `GlobalRegistry.DataVault` fallback in Vault resolution.
- [x] `TryResolveHeapSanitizerVaultBuffers()` now consumes `_dataVault` only and fails closed when the cold cache/hot-swap cache is unavailable.
- [x] `Awake()`, `OnEnable()`, and `Start()` now cache dependencies before attempting native handle storage resolution.
- [x] `Start()` retries `EnsureNativeHandleStorage()` if early lifecycle resolution failed before DataVault became available.
- [x] Added `GlobalRegistryServiceSlot.DataVault` hot-swap handling: complete the TTL job against the old vault, swap the cached vault, invalidate stale Vault handle descriptors, and reacquire native storage only when the new vault exists.
- [x] Static scan: `GlobalRegistry.DataVault` in `AssetLifecycleGovernor.cs` now reports only the cold `CacheDependencies()` assignment.
- [x] Static scan: `TryResolveHeapSanitizerVaultBuffers()` contains `IDataVault vault = _dataVault;` and no registry fallback.
- [x] Static scan: `Addressables.Release(` remains only at `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:4332`; `Addressables.ReleaseInstance(` remains only at `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2275`.
- [x] Static scan: dispatcher native map markers remain absent after R31.
- [ ] Compile verification R32 | PENDING VERIFICATION: not launched. Known external missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` still blocks `Hecton8.Core.csproj` before SHINOBU verification, and the user explicitly forbade needless build/rebuild runs.

## R33 AssetLoadDispatcher Continuous Quality Slot Curve

- [x] Re-read active SHINOBU status/rationale and checked active `CURRENT_BATCH.md`; SHINOBU_101 remains absent from the active batch, so archived/status authority remains current.
- [x] Identified `ResolveAllowedConcurrentLoads()` as a remaining binary pressure gate: tier 3/4 switched at `ramPressure > 0.85f`, tier 5/6 switched at `ramPressure > 0.75f`, and the function did not consume `GlobalQualityWeight`.
- [x] Added `Unity.Mathematics` and replaced binary load-slot returns with `ResolveContinuousLoadSlots()`.
- [x] `ResolveAllowedConcurrentLoads()` now blends cached `VRAMPressureMonitor.PressureFactor` and `HomeostasisBrain.GlobalQualityWeight` through `math.smoothstep`, `math.lerp`, `math.max`, and `math.saturate`.
- [x] Critical tier 0/1 requests retain minimum slots under pressure; background tier 5/6 requests can continuously collapse to zero dispatch permits as quality/pressure worsens.
- [x] Static scan: `ramPressure >`, `PressureFactor >`, `IsLowEndHardware`, and quality `if` branches in `AssetLoadDispatcher.cs` report no dispatch-slot hits.
- [x] Static scan: `GlobalRegistry.(AssetLifecycle|VRAMMonitor|VRAMPressure|DataVault)` in `AssetLoadDispatcher.cs` reports only registration/cold-cache boundaries.
- [x] Static scan: `Addressables.Release(` remains only at `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:4332`; `Addressables.ReleaseInstance(` remains only at `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2275`.
- [x] Static scan: `git diff --check -- Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs` reports LF-to-CRLF warning only.
- [ ] Compile verification R33 | PENDING VERIFICATION: not launched. Known external missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` still blocks `Hecton8.Core.csproj` before SHINOBU verification, and the user explicitly forbade needless build/rebuild runs.

## R34 VRAMPressureMonitor Quality-Weighted Pressure Response

- [x] Re-read active SHINOBU status/rationale before patching the second pressure-cliff cluster.
- [x] Identified hard pressure gates in `VRAMPressureMonitor`: `VramPressureFactor >= emergencyVramFraction`, `RamPressureFactor >= RamEmergencyFraction`, `RamPressureFactor >= RamWarningFraction`, and warning-fraction soft pressure.
- [x] Added `Unity.Mathematics` and `HomeostasisBrain.GlobalQualityWeight` response helpers to the monitor.
- [x] Warning, emergency, forced-mip, restore, RAM, and LOD fractions now pass through `ResolveQualityAdjustedFraction()` using a smooth quality curve.
- [x] Soft and emergency pressure actions now use `ResolveSoftPressureResponse()` / `ResolveEmergencyPressureResponse()` and budget counts from `ResolveBudgetedPressureCount()`.
- [x] LOD aggression now lerps `QualitySettings.lodBias` and `BrgLodDistanceScalar` continuously instead of jumping directly to `LODAggressionMultiplier`; red-zone safety still forces full collapse.
- [x] Static scan: removed hard warning/emergency comparisons and dead threshold helpers from `VRAMPressureMonitor.cs`; only `VramPressureFactor >= 1f` red-zone safety remains.
- [x] Static scan: `GlobalRegistry.(VRAMMonitor|AssetLifecycle|PlayerInventory|RenderTexturePool|VRAMPressure)` in `VRAMPressureMonitor.cs` reports only registration and cold-cache boundaries.
- [x] Static scan: `git diff --check -- Assets/_Project/Scripts/Optimization/VRAMPressureMonitor.cs` reports LF-to-CRLF warning only.
- [ ] Compile verification R34 | PENDING VERIFICATION: not launched. Known external missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` still blocks `Hecton8.Core.csproj` before SHINOBU verification, and the user explicitly forbade needless build/rebuild runs.

## R35 VRAMPressureMonitor Continuous Mip Bias Closure

- [x] Re-read active SHINOBU status/rationale and R45 documentation boundary before code edits.
- [x] Identified the remaining mip-bias cliff: `ApplyMipBias()` still converted any soft pressure or forced-mip threshold crossing directly to one fixed mip downgrade.
- [x] Removed `IsSoftVramPressureActive()` and `ResolveForcedMipDropThresholdBytes()` from the mip path.
- [x] Added `ResolveForcedMipResponse()` so forced half-resolution pressure uses the same `GlobalQualityWeight` quality-adjusted fraction and `math.smoothstep` response as soft/emergency pressure.
- [x] Added `ResolveMipLimitDelta()` to convert the scalar response into the final Unity integer mip-limit delta only at the API boundary; red-zone pressure forces a two-step collapse.
- [x] `ApplyMipBias()` now holds the active mip limit through tiny nonzero response values instead of restoring early before the restore band, preventing threshold thrash.
- [x] Static scan: `softVramPressure`, `forcedMipThresholdBytes`, `ResolveForcedMipDropThresholdBytes`, `IsSoftVramPressureActive`, and `Mathf.Max(_baselineMipLimit, 1)` report no results in `VRAMPressureMonitor.cs`.
- [x] Static scan: continuous symbols `ResolveForcedMipResponse`, `ResolveMipLimitDelta`, and `mipPressureResponse` resolve to expected code paths only.
- [x] Static scan: `git diff --check -- Assets/_Project/Scripts/Optimization/VRAMPressureMonitor.cs` reports LF-to-CRLF warning only.
- [ ] Compile verification R35 | PENDING VERIFICATION: not launched. Known external missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` still blocks `Hecton8.Core.csproj` before SHINOBU verification, and the user explicitly forbade needless build/rebuild runs.

## R36 Dispatcher UI Mip Gate Ownership Collapse

- [x] Re-scanned dispatcher UI mip gate after R35 and identified a second issue: `AssetLoadDispatcher` still wrote `QualitySettings.globalTextureMipmapLimit`, creating a second writer for the same global mip fact.
- [x] Removed dispatcher-owned `_baselineGlobalTextureMipLimit`, `_activeGlobalTextureMipLimit`, `_mipGateInitialized`, and `CaptureMipBiasBaseline()`.
- [x] Dispatcher UI gate now computes `gateResponse` from current VRAM pressure and continuous `GlobalQualityWeight`, then calls `VRAMPressureMonitor.SetExternalMipPressureResponse(...)`.
- [x] `VRAMPressureMonitor` now owns `_externalMipPressureResponse`, combines it with soft/forced/red-zone pressure inside `ApplyMipBias()`, and remains the writer of `QualitySettings.globalTextureMipmapLimit`.
- [x] Dispatcher `OnDisable()` and `OnDestroy()` clear the external pressure response before unregistering/cold-cache clear, avoiding stale UI gate pressure.
- [x] External pressure updates also refresh monitor `LastUsedVramBytes`, `VramPressureFactor`, and aggregate `PressureFactor` before mip recompute, preventing stale 90-frame pressure from delaying restore.
- [x] Static scan: `QualitySettings.globalTextureMipmapLimit`, dispatcher baseline/active mip fields, `_mipGateInitialized`, `CaptureMipBiasBaseline`, old UI byte constants, and `LowVramDeviceThresholdMb` report no results in `AssetLoadDispatcher.cs`.
- [x] Static scan: `SetExternalMipPressureResponse`, `_externalMipPressureResponse`, and the sole pressure-lane `QualitySettings.globalTextureMipmapLimit` write resolve to expected `VRAMPressureMonitor.cs` paths.
- [x] Static scan: `git diff --check -- Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs Assets/_Project/Scripts/Optimization/VRAMPressureMonitor.cs` reports LF-to-CRLF warnings only.
- [ ] Compile verification R36 | PENDING VERIFICATION: not launched. Known external missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` still blocks `Hecton8.Core.csproj` before SHINOBU verification, and the user explicitly forbade needless build/rebuild runs.

## R37 VRAMEnforcer Continuous Bootstrap Budget

- [x] Inspected `VRAMEnforcer` after R36 and found explicit binary hardware gates: `DetectedGraphicsMemoryMb <= 2048`, low/shared boid scale constants, and shared-memory ternary texture clamp selection.
- [x] Added `Unity.Mathematics` scalar budget math.
- [x] Replaced the 2048 MB low-VRAM cliff with `ResolveHardwareBudgetWeight()` using `math.smoothstep(1024 MB, 8192 MB, detectedGraphicsMemoryMb)` and branchless shared-memory ceiling selection.
- [x] `ApplyBoidPopulationBudget()` now blends requested fauna counts through continuous hardware and `GlobalQualityWeight` curves instead of returning full count vs fixed low/shared scale.
- [x] Bootstrap texture mip clamp now resolves from `math.lerp(2, 0, min(hardwareWeight, qualityCurve))`, preserving a cold minimum clamp without a low/high hardware branch.
- [x] Static scan: old low/shared constants, `DetectedGraphicsMemoryMb > 0 &&`, shared-memory ternary scale, and `if (!_lowVramBudgetActive)` early return report no results in `VRAMEnforcer.cs`.
- [x] Static scan: `ResolveHardwareBudgetWeight`, `ResolveQualityCurve`, `math.smoothstep`, `math.lerp`, `math.select`, and `HomeostasisBrain.GlobalQualityWeight` resolve to expected code paths.
- [x] Static scan: `git diff --check -- Assets/_Project/Scripts/Optimization/VRAMEnforcer.cs` reports LF-to-CRLF warning only.
- [ ] Compile verification R37 | PENDING VERIFICATION: not launched. Known external missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` still blocks `Hecton8.Core.csproj` before SHINOBU verification, and the user explicitly forbade needless build/rebuild runs.

## R38 Bootstrap Default And Dispatcher Hardware Cache

- [x] Re-read active SHINOBU status/rationale, checked active batch authority, and re-read the binary payload ledger before code edits.
- [x] Found a cold-path regression in `VRAMEnforcer`: `_hardwareBudgetWeight` defaulted to `0f` before SubsystemRegistration/init, so non-playing editor/offline boid budget calls could collapse authored counts to the minimum scale.
- [x] `_hardwareBudgetWeight` now defaults to `1f`, and `ApplyBoidPopulationBudget()` returns the clamped authored count when the runtime budget has not initialized outside Play Mode.
- [x] Found a tick-path hardware query in `AssetLoadDispatcher.EvaluateUiMipBiasGate()`: `SystemInfo.graphicsMemorySize` was read during every UI mip gate evaluation.
- [x] Added `_graphicsBudgetBytes` plus `RefreshGraphicsBudgetBytes()` on `OnEnable()` and `Start()`; UI mip gate now uses the cached byte budget and only refreshes if the cache is invalid.
- [x] Static scan: `_hardwareBudgetWeight`, non-playing init guard, `RefreshGraphicsBudgetBytes`, and `_graphicsBudgetBytes` resolve to expected code paths.
- [x] Static scan: forbidden binary hardware and private native/managed collection markers across `AssetLoadDispatcher.cs`, `VRAMPressureMonitor.cs`, and `VRAMEnforcer.cs` report no results.
- [x] Static scan: `Addressables.Release(` remains only at `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs:4332`; `Addressables.ReleaseInstance(` remains only at `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2275`.
- [x] Static scan: `git diff --check -- Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs Assets/_Project/Scripts/Optimization/VRAMEnforcer.cs` reports LF-to-CRLF warnings only.
- [ ] Compile verification R38 | PENDING VERIFICATION: not launched. Known external missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` still blocks `Hecton8.Core.csproj` before SHINOBU verification, and the user explicitly forbade needless build/rebuild runs.
