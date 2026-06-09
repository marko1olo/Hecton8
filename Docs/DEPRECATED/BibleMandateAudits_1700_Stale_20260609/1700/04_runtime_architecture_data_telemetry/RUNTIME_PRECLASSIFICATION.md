# Runtime Preclassification - Runtime Architecture, Data, Bootstrap, Telemetry, Performance

Status: HEURISTIC FIRST PASS - MANUAL REVIEW STILL REQUIRED
Date: 2026-06-02

This file groups static runtime suspects by a conservative heuristic. It can reduce review time, but it cannot prove a line is legal or illegal without reading the containing method and owner phase.

Total runtime suspects: 275.

## Summary

- LEGAL_EDITOR_OR_DEV_GUARDED: 124
- REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 65
- LIKELY_LEGAL_COLD_OR_DIAGNOSTIC_PATH: 46
- LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH: 16
- REVIEW_LOG_GUARD_REQUIRED: 8
- LIKELY_LEGAL_COLD_LOOKUP: 5
- REVIEW_CACHE_OR_INJECTION_REQUIRED: 5
- REVIEW_JOB_FENCE_REQUIRED: 3
- REVIEW_UNCLASSIFIED_STATIC_RISK: 1
- REVIEW_HOT_PHASE_METHOD: 1
- REVIEW_RUNTIME_MESH_MATERIAL_PATH: 1

## LEGAL_EDITOR_OR_DEV_GUARDED (124)

- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentLoreBinaryProvider.cs:133:                Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] Babel dictionary missing.", this);
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentLoreBinaryProvider.cs:156:                Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] Failed to open Babel dictionary.", this);
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentLoreBinaryProvider.cs:400:            Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] Rejected zero hash lore read.", this);
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentLoreBinaryProvider.cs:407:            Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] Missing lore block.", this);
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentLoreBinaryProvider.cs:414:            Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] Unreadable lore block.", this);
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentLoreBinaryProvider.cs:421:            Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] Destination span too small for lore.", this);
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentLoreBinaryProvider.cs:428:            Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] No readable Babel dictionary stream.", this);
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentLoreBinaryProvider.cs:435:            Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] Partial lore read.", this);
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentAssetHashMap.cs:358:            Hecton8.Core.H8Debug.LogError("[ContentAssetHashMap] Required-hash copy rejected destinationLength=" +
- Runtime debug logging | Assets\_Project\Scripts\Core\Data\H8DataBaker.cs:166:                Hecton8.Core.H8Debug.Log("[H8DataBaker] Static data bake complete. Records=" + result.RecordCount.ToString(CultureInfo.InvariantCulture));
- Runtime debug logging | Assets\_Project\Scripts\Core\Data\H8DataBaker.cs:170:                Hecton8.Core.H8Debug.LogError("[H8DataBaker] " + result.Message);
- Runtime debug logging | Assets\_Project\Scripts\Core\Data\H8DataBaker.cs:1339:                Hecton8.Core.H8Debug.LogError("[H8DataHotReload] " + result.Message);
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:584:            Hecton8.Core.H8Debug.LogError("[ContentBundleReferenceCounter] Invalid ref-count transition.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:591:            Hecton8.Core.H8Debug.LogError("[ContentBundleReferenceCounter] Invalid acquire metadata.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:598:            Hecton8.Core.H8Debug.LogError("[ContentBundleReferenceCounter] Refused to remove active bundle.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:605:            Hecton8.Core.H8Debug.LogError("[ContentBundleReferenceCounter] Vault unavailable.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:612:            Hecton8.Core.H8Debug.LogError("[ContentBundleReferenceCounter] Bundle ref ledger full.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:619:            Hecton8.Core.H8Debug.LogError("[ContentBundleReferenceCounter] Vault ledger count exceeded fixed capacity; cleared residency ledger.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:1100:                Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Hologram proxy mesh/material missing.", this);
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:1542:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Bundle handle table exhausted.", this);
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2210:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Asset hash map missing.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2217:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] No content registry entry.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2224:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] DataVault dependency unavailable on runtime content route.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2231:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Failed to track Addressables bundle handle.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2238:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] No tracked Addressables bundle handle during release.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2245:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Invalid Addressables bundle handle.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2252:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Rejected async load tracking.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2259:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Pending-load vault unavailable.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2266:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Pending-load ledger full.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2273:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Async load completion had no pending entry.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2280:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Invalid VFX prewarm Addressables reference.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2287:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] VFX prewarm handle ledger full.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2294:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] VFX prewarm returned invalid Addressables handle.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2301:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Resident VFX handle ledger full; releasing completed prewarm handle.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2308:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] VFX prewarm handle failed; releasing Addressables handle.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2315:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Hologram proxy unavailable.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2322:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Hologram proxy pool exhausted; pending asset will remain invisible until a proxy frees.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2329:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Pending load vault count exceeded fixed capacity; cleared pending-load ledger.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2336:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Failed to write content blackbox dump.");
- Runtime debug logging | Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2343:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Failed to resolve content blackbox dump path.");
- Additional lines omitted here: 84. Use `../_scans/04_runtime_architecture_data_telemetry_runtime_risks.txt` for the full list.

## REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED (65)

- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\ConnectionSplineBatchRenderer.cs:974:            array = new NativeArray<SplineDescriptor>(requiredLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\ConnectionSplineBatchRenderer.cs:989:            array = new NativeArray<FlexiblePipeInstanceGpuData>(requiredLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\BurstCallback.cs:83:            _events = new NativeQueue<int>(Allocator.Persistent);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\BurstCallback.cs:87:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Data\StaticDataStore.cs:133:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Data\StaticDataStore.cs:408:                H8Memory.FreeRaw(_ownedFallbackPointer, Allocator.Persistent, SystemID.CoreDataVault);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\DodReplayRecorder.cs:974:            NativeArray<T> array = new NativeArray<T>(length, Allocator.Persistent, (NativeArrayOptions)options);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\HectonArenaAllocator.cs:175:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\HectonArenaAllocator.cs:378:            H8Memory.FreeRaw(_basePtr, Allocator.Persistent, SystemID.H8Memory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\NativeRingBuffer.cs:34:            _buffer = new NativeArray<T>(capacity, allocator, (NativeArrayOptions)options);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\NativeMemorySentinel.cs:1429:                        ? Allocator.Persistent
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\NativeMemorySentinel.cs:1969:                    return Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Signals\SignalStormConcurrencyFuzzer1311.cs:71:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:714:                _buffers = new UnsafeHashMap<int, IntPtr>(safeCapacity, Allocator.Persistent);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:715:                _metadata = new UnsafeHashMap<int, VaultBufferMeta>(safeCapacity, Allocator.Persistent);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:716:                _metadataGenerationByBufferId = new UnsafeHashMap<int, uint>(safeCapacity, Allocator.Persistent);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:720:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:734:                _keys = new NativeList<int>(safeCapacity, Allocator.Persistent);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:735:                _blocks = new NativeList<VaultArenaBlock>(blockCapacity, Allocator.Persistent);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:739:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:750:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:761:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:772:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:789:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:800:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:808:                _macroDatabasePayloadCache = new NativeParallelHashMap<ulong, MacroDatabasePayloadCacheEntry>(safeCapacity, Allocator.Persistent);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:809:                _macroDatabasePayloadAccessTicks = new NativeParallelHashMap<ulong, uint>(safeCapacity, Allocator.Persistent);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:810:                _macroDatabasePayloadKeys = new NativeList<ulong>(safeCapacity, Allocator.Persistent);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:823:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:1391:                meta.Allocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:3551:                _macroDatabasePayloadCache = new NativeParallelHashMap<ulong, MacroDatabasePayloadCacheEntry>(safeCapacity, Allocator.Persistent);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:3553:                _macroDatabasePayloadAccessTicks = new NativeParallelHashMap<ulong, uint>(safeCapacity, Allocator.Persistent);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:3555:                _macroDatabasePayloadKeys = new NativeList<ulong>(safeCapacity, Allocator.Persistent);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:3611:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:3619:                H8Memory.FreeRaw(payloadPointer, Allocator.Persistent, SystemID.CoreDataVault);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:3642:                    H8Memory.FreeRaw(existing.Pointer.ToPointer(), Allocator.Persistent, SystemID.CoreDataVault);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:3651:                    H8Memory.FreeRaw(payloadPointer, Allocator.Persistent, SystemID.CoreDataVault);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:3659:                    H8Memory.FreeRaw(payloadPointer, Allocator.Persistent, SystemID.CoreDataVault);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:3783:                H8Memory.FreeRaw(entry.Pointer.ToPointer(), Allocator.Persistent, SystemID.CoreDataVault);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs:3862:                H8Memory.FreeRaw(_arenaBase, Allocator.Persistent, SystemID.CoreDataVault);
- Additional lines omitted here: 25. Use `../_scans/04_runtime_architecture_data_telemetry_runtime_risks.txt` for the full list.

## LIKELY_LEGAL_COLD_OR_DIAGNOSTIC_PATH (46)

- Runtime debug logging | Assets\_Project\Scripts\Core\BootstrapContracts\BootstrapStatus.cs:338:            Debug.LogError(SafeHaltMessage);
- Runtime debug logging | Assets\_Project\Scripts\Core\GlobalRegistry.cs:3786:                Debug.LogError("[GlobalRegistry] Cannot register null as IBabelLocalization.");
- Runtime debug logging | Assets\_Project\Scripts\Core\GlobalRegistry.cs:5617:                Debug.LogWarning("[GlobalRegistry] Unregister mismatch for IBabelLocalization.");
- Runtime debug logging | Assets\_Project\Scripts\Core\GlobalRegistry.cs:6975:                Debug.LogError("[GlobalRegistry] SystemDispatcher is not registered. Bootstrap must create and register it before runtime tick registration.");
- Runtime debug logging | Assets\_Project\Scripts\Core\GlobalRegistry.cs:7035:                Debug.LogError("[GlobalRegistry] Get<T>() during Registering is forbidden. requested=" + typeof(T).Name);
- Runtime debug logging | Assets\_Project\Scripts\Core\GlobalRegistry.cs:7341:                Debug.LogWarning("[GlobalRegistry] Unregister mismatch for " + typeof(T).Name + ".");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:681:            Debug.LogError(
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:1572:                    Debug.LogError("[GameBootstrapper] SERVICE_HEARTBEAT_FREEZE");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:2083:            Debug.Log("[GameBootstrapper] Completed bootstrap handoff loading pending target scene '" + gameplaySceneName + "'.");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:2355:                    Debug.LogError("[GameBootstrapper] Data Monolith boot validation failed. status=" + _lastDataMonolithBootstrapStatus);
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:3010:                    Debug.LogError(
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:3120:            Debug.LogError($"[GameBootstrapper] Scene load watchdog tripped during {stageName}. progress={progress:0.000} frames={waitFrames} elapsed={elapsedSeconds:0.000}s target={targetSceneName}.");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:3347:                    Debug.LogError("[GameBootstrapper] UI addressable prefab failed during bootstrap UI gate.");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:3390:                    Debug.LogWarning("[GameBootstrapper] Tier Addressables prewarm timed out; continuing bootstrap. label=" + label + " elapsed=" + elapsedSeconds.ToString("0.000"));
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:3404:                Debug.LogWarning("[GameBootstrapper] Tier Addressables prewarm failed; continuing bootstrap. label=" + label);
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:5148:                Debug.LogError(
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:5980:            Debug.LogWarning($"[GameBootstrapper] {message}");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:5987:            Debug.LogError($"[GameBootstrapper] Bootstrap dependency graph invalid. phase={phase}");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:5994:            Debug.LogError($"[GameBootstrapper] Bootstrap dependency failed. phase={phase} node={node}");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:6002:            Debug.LogError("[GameBootstrapper] CoreServices substep failed. substep=" + substep);
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:6014:            Debug.LogError($"[GameBootstrapper] Service heartbeat timeout. node={node} frames={waitFrames} elapsed={elapsedSeconds:0.000}s");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:6028:            Debug.LogError($"[GameBootstrapper] Bootstrap phase failed. phase={phase}");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:6451:                Debug.LogError("[GameBootstrapper] Foreign DontDestroyOnLoad root destroyed. name=" + root.name);
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:6809:                Debug.LogError("[GameBootstrapper] SystemDispatcher not found.");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:6815:                Debug.LogError("[GameBootstrapper] ObjectPoolManager not found.");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:6821:                Debug.LogError("[GameBootstrapper] PrefabRegistry not found.");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:6827:                Debug.LogError("[GameBootstrapper] SaveManager not found.");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:6832:                Debug.LogWarning("[GameBootstrapper] WorldStateManager not found.");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:6835:                Debug.LogWarning("[GameBootstrapper] ConstructionManager not found.");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:6924:                Debug.LogError("[GameBootstrapper] Save load failed.");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:6959:                    Debug.LogWarning("[GameBootstrapper] World-ready queue stalled. Continuing bootstrap.");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:6991:            Debug.LogWarning("[GameBootstrapper] Ground-ready timed out. Activating player without collider confirmation.");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:7101:            Debug.LogWarning("[GameBootstrapper] No player spawner or owned player reference is available.");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:7390:            Debug.LogError("[GameBootstrapper] " + error);
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:7418:                Debug.LogError("[GameBootstrapper] Dirty editor scene rejected, but scene has no disk path.");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:7422:            Debug.LogError("[GameBootstrapper] Dirty editor scene rejected; reloading from disk: " + scenePath);
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:7524:                        Debug.LogError("[GameBootstrapper] Background domain handshake invalid telemetry path.");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:7527:                        Debug.LogError("[GameBootstrapper] Background domain handshake IO failure.");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:7530:                        Debug.LogError("[GameBootstrapper] Background domain handshake unauthorized.");
- Runtime debug logging | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:7533:                        Debug.LogError("[GameBootstrapper] Background domain handshake unsupported path.");
- Additional lines omitted here: 6. Use `../_scans/04_runtime_architecture_data_telemetry_runtime_risks.txt` for the full list.

## LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH (16)

- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\UIStateStore.cs:156:            _states = new NativeArray<UIStateData>(StateCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<UIStateData>[StateCount] - headless UI simulation state - owner: UIStateStore
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\UIStateStore.cs:157:            _valueSlots = new NativeArray<UIValueSlot>(ValueSlotCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<UIValueSlot>[ValueSlotCount] - headless numeric UI value bridge - owner: UIStateStore
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\UIStateStore.cs:158:            _historyStates = new NativeArray<UIStateData>(UIStateHistoryFrames, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<UIStateData>[UIStateHistoryFrames] - PDA UI rollback snapshot ring - owner: UIStateStore
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\UIStateStore.cs:159:            _pdaLogEventHashes = new NativeArray<uint>(MaxPdaLogEvents, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<uint>[MaxPdaLogEvents] - PDA event-sourced log history - owner: UIStateStore
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\UIStateStore.cs:160:            _pdaLogEventTimestamps = new NativeArray<float>(MaxPdaLogEvents, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[MaxPdaLogEvents] - PDA event-sourced log timestamps - owner: UIStateStore
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\GlobalTelemetryBus.cs:749:                    _ringBuffer = new NativeRingBuffer<TelemetryEvent>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeRingBuffer<TelemetryEvent>[1024] — power-of-two black-box ring retaining the last 1000 telemetry frames — owner: GlobalTelemetryBus
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\GlobalTelemetryBus.cs:762:                    _snapshotBuffer = new NativeArray<TelemetryEvent>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<TelemetryEvent>[1024] — telemetry export snapshot staging buffer — owner: GlobalTelemetryBus
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\GlobalTelemetryBus.cs:773:                    _exportScratch = new NativeArray<byte>(exportScratchBytes, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[65552] — unmanaged binary telemetry export scratch — owner: GlobalTelemetryBus
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\ThreadSafeCommandQueue.cs:274:                    _pendingCommands = new NativeQueue<EntityCommand>(Allocator.Persistent); // COLD ALLOC: NativeQueue<EntityCommand>(Persistent) - structural command ingress drained by SystemDispatcher LateUpdate - owner: ThreadSafeCommandQueue
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\ThreadSafeCommandQueue.cs:882:            _pendingStorageReservationCommitResolved = new NativeQueue<StorageReservationCommitResolvedPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<StorageReservationCommitResolvedPayload>[64] - deferred storage reservation acknowledgements - owner: ThreadSafeCommandQueue
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\FrameTimeWatchdog.cs:274:            _frameTimeSamples = new NativeRingBuffer<float>(FrameTimeSampleCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeRingBuffer<float>[64] - fixed frame pacing average, no managed List/array growth - owner: FrameTimeWatchdog
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\JobFenceManager.cs:26:            Handles = new NativeArray<JobHandle>(Capacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\GlobalRegistry.cs:7542:                _pendingServiceRebounds = new NativeQueue<RegistryEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<RegistryEventPayload>[64] - service rebound event lane - owner: GlobalRegistry
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\GlobalRegistry.cs:7549:                _nextFrameServiceRebounds = new NativeQueue<RegistryEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<RegistryEventPayload>[64] - next-frame service rebound event lane - owner: GlobalRegistry
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Core\Contracts\CoreLowLevelUtilities.cs:314:            _buffer = new NativeArray<T>(capacity, allocator, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Optimization\AssetLifecycleGovernor.cs:3712:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

## REVIEW_LOG_GUARD_REQUIRED (8)

- Runtime debug logging | Assets\_Project\Scripts\Core\GlobalRegistry.cs:6961:            Debug.LogError(report);
- Runtime debug logging | Assets\_Project\Scripts\Core\GlobalRegistry.cs:7108:                Debug.LogError("[GlobalRegistry] Ready-locked registry rejected registration: " + typeof(T).Name);
- Runtime debug logging | Assets\_Project\Scripts\Core\GlobalRegistry.cs:7196:                Debug.LogError(
- Runtime debug logging | Assets\_Project\Scripts\Core\GlobalRegistry.cs:7251:                Debug.LogError(
- Runtime debug logging | Assets\_Project\Scripts\Core\GlobalRegistry.cs:7366:            Debug.LogError("[FATAL LEAK PREVENTED] H8Memory reaped native allocations for " + serviceSlot + ".");
- Runtime debug logging | Assets\_Project\Scripts\Core\GlobalRegistry.cs:7488:                    Debug.LogError("[GlobalRegistry] Service rebound queue overflow. Increase MaxPendingServiceRebounds.");
- Runtime debug logging | Assets\_Project\Scripts\Core\GlobalRegistry.cs:7502:                    Debug.LogError("[GlobalRegistry] Service rebound queue overflow. Increase MaxPendingServiceRebounds.");
- Runtime debug logging | Assets\_Project\Scripts\Core\GlobalRegistry.cs:7903:                Debug.LogException(exception);

## LIKELY_LEGAL_COLD_LOOKUP (5)

- Unity scene lookup | Assets\_Project\Scripts\Bootstrap\HectonLoreSystemsRoot.cs:251:                if (!existingChild.TryGetComponent<T>(out _))
- Unity scene lookup | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:1821:            Camera camera = cameraObject.GetComponent<Camera>();
- Unity scene lookup | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:1837:            Light key = keyLight.GetComponent<Light>();
- Unity scene lookup | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:1847:            Light fill = fillLight.GetComponent<Light>();
- Unity scene lookup | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:1973:            TextMeshPro text = textObject.GetComponent<TextMeshPro>();

## REVIEW_CACHE_OR_INJECTION_REQUIRED (5)

- Unity scene lookup | Assets\_Project\Scripts\Core\Bridge\H8PrefabRegistry.cs:471:                prefab.GetComponentsInChildren(true, s_RendererScratch);
- Unity scene lookup | Assets\_Project\Scripts\Core\HectonUrpTextureRequirementsGuard.cs:180:                root.GetComponentsInChildren(true, s_cameraScratch);
- Unity scene lookup | Assets\_Project\Scripts\Core\PlayerRuntimeContextService.cs:1099:                _playerTransform.GetComponentsInChildren(true, _visorResolveBuffer);
- Unity scene lookup | Assets\_Project\Scripts\Core\PlayerSensoryManager.cs:353:                        _playerTransform.GetComponentsInChildren(true, _visorResolveBuffer);
- Unity scene lookup | Assets\_Project\Scripts\Core\SceneRuntimeService.cs:1169:                root.GetComponentsInChildren(false, _cameraSearchBuffer);

## REVIEW_JOB_FENCE_REQUIRED (3)

- Job fence / sync wait | Assets\_Project\Scripts\Core\Memory\H8Memory.cs:3737:            ownerHandle.Complete();
- Job fence / sync wait | Assets\_Project\Scripts\Core\Contracts\CoreLowLevelUtilities.cs:279:            handle.Complete();
- Job fence / sync wait | Assets\_Project\Scripts\Core\Contracts\CoreLowLevelUtilities.cs:290:            handle.Complete();

## REVIEW_UNCLASSIFIED_STATIC_RISK (1)

- Uncategorized | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:1974:            text.text = textValue;

## REVIEW_HOT_PHASE_METHOD (1)

- Hot Unity phase method | Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs:1490:        private void Update()

## REVIEW_RUNTIME_MESH_MATERIAL_PATH (1)

- Runtime mesh/material mutation | Assets\_Project\Scripts\Core\SceneRuntimeService.cs:1077:                image.material = _transitionDitherMaterial;

