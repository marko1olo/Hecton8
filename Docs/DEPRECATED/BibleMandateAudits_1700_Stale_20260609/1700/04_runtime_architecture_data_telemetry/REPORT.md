# Runtime Architecture, Data, Bootstrap, Telemetry, Performance

Status: STATIC BIBLE/MANDATE/CODEBASE AUDIT - RUNTIME PROOF NOT RUN
Date: 2026-06-02
Verdict: YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING

## Scope

This report compares the current root bible routes and selected mandate registry files against static codebase evidence. It does not prove Unity import health, Play Mode behavior, profiler cost, memory use, visual quality, or device performance.

## Bibles Checked

- OK systems.md - 99 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK data.md - 130 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK bootstrap.md - 106 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK telemetry.md - 178 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK performance.md - 157 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK math.md - 243 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK authoring.md - 148 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK quality.md - 228 lines; GlobalQualityWeight, proof, acceptance, rejection.

## Mandates Matched

- .agents-skills\ARCH_Execution_Phases.txt
- .agents-skills\ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- .agents-skills\ARCH_Pentarchy_Audit.txt
- .agents-skills\ARCH_Project_Bootstrap_Sequence_Init_Safety.txt
- .agents-skills\ARCH_Signal_Lane_Segregation.txt
- .agents-skills\CI_MATH_VIOLATIONS_Gate.txt
- .agents-skills\DATA_Runtime_Struct_Layout_ARM64.txt
- .agents-skills\DBG_Telemetry_Crash_Reporting_PostMortem.txt
- .agents-skills\OPT_HectonArenaAllocator_2_0.txt
- .agents-skills\OPT_Native_Memory_Collections_JobSystem_Protocol.txt

## Code/Asset Roots

- OK Assets\_Project\Scripts\Core
- OK Assets\_Project\Scripts\Bootstrap
- OK Assets\_Project\Scripts\Global
- OK Assets\_Project\Scripts\Data
- OK Assets\_Project\Scripts\Optimization
- OK Tools
- OK Docs\ARCHITECTURE

## Static Evidence Found

Total matching files: 388. Showing first 80. Full list: _scans/04_runtime_architecture_data_telemetry_evidence_files.txt.

- Assets\_Project\Scripts\Bootstrap\BootstrapEvents.cs
- Assets\_Project\Scripts\Bootstrap\BootstrapRegistryCycleValidator.cs
- Assets\_Project\Scripts\Bootstrap\GameBootstrapper.cs
- Assets\_Project\Scripts\Bootstrap\SceneInstantiationGate.cs
- Assets\_Project\Scripts\Core\Arm64AlignmentFaultGizmo.cs
- Assets\_Project\Scripts\Core\BinaryLayoutManifest.cs
- Assets\_Project\Scripts\Core\BlackBoxHeartbeatThread.cs
- Assets\_Project\Scripts\Core\BootstrapContracts\BootstrapStatus.cs
- Assets\_Project\Scripts\Core\BootstrapContracts\InputBindingServiceContracts.cs
- Assets\_Project\Scripts\Core\Bridge\Editor\H8BridgeFacadeEditors.cs
- Assets\_Project\Scripts\Core\Bridge\Editor\H8PrefabRegistryWindow.cs
- Assets\_Project\Scripts\Core\Bridge\H8BridgeBinaryLayoutVerifier.cs
- Assets\_Project\Scripts\Core\Bridge\H8BridgeContracts.cs
- Assets\_Project\Scripts\Core\Bridge\H8BridgeFacadeRuntime.cs
- Assets\_Project\Scripts\Core\Bridge\H8BridgeLiveSyncScheduler.cs
- Assets\_Project\Scripts\Core\Bridge\H8DesignDataFacade.cs
- Assets\_Project\Scripts\Core\Bridge\H8InputMappingFacade.cs
- Assets\_Project\Scripts\Core\Bridge\H8PrefabRegistry.cs
- Assets\_Project\Scripts\Core\Bridge\H8PrefabRegistryRuntimeBinder.cs
- Assets\_Project\Scripts\Core\Bucketing\ModuloSimulationBucketer.cs
- Assets\_Project\Scripts\Core\BulkheadContainmentIntentBus.cs
- Assets\_Project\Scripts\Core\BurstCallback.cs
- Assets\_Project\Scripts\Core\CameraJuiceSignals.cs
- Assets\_Project\Scripts\Core\ConnectionSplineBatchRenderer.cs
- Assets\_Project\Scripts\Core\Content\ContentAssetHashMap.cs
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs
- Assets\_Project\Scripts\Core\Content\Editor\ContentAuthorityBuildValidators.cs
- Assets\_Project\Scripts\Core\Contracts\AI\AlphaLeviathanCognitionContracts.cs
- Assets\_Project\Scripts\Core\Contracts\AI\AlphaLeviathanStalkContracts.cs
- Assets\_Project\Scripts\Core\Contracts\AupPrecisionContracts.cs
- Assets\_Project\Scripts\Core\Contracts\BabelLocalizationContract.cs
- Assets\_Project\Scripts\Core\Contracts\CoreContractsAssemblyMarker.cs
- Assets\_Project\Scripts\Core\Contracts\CoreLowLevelUtilities.cs
- Assets\_Project\Scripts\Core\Contracts\DrsContracts.cs
- Assets\_Project\Scripts\Core\Contracts\ExosuitKinematicsContracts.cs
- Assets\_Project\Scripts\Core\Contracts\Fluids\FluidAnalyticalContracts.cs
- Assets\_Project\Scripts\Core\Contracts\GroundRadarContracts.cs
- Assets\_Project\Scripts\Core\Contracts\HectonContractVersion.cs
- Assets\_Project\Scripts\Core\Contracts\HectonDataSovereigntyContract.cs
- Assets\_Project\Scripts\Core\Contracts\HectonEcologyContract.cs
- Assets\_Project\Scripts\Core\Contracts\HectonLoreContract.cs
- Assets\_Project\Scripts\Core\Contracts\HectonPlatformContract.cs
- Assets\_Project\Scripts\Core\Contracts\HectonSignalLaneContract.cs
- Assets\_Project\Scripts\Core\Contracts\HectonVaultOffsetContract.cs
- Assets\_Project\Scripts\Core\Contracts\IHectonOceanKinematics.cs
- Assets\_Project\Scripts\Core\Contracts\InertialNavigationContracts.cs
- Assets\_Project\Scripts\Core\Contracts\JobAdmissionContracts.cs
- Assets\_Project\Scripts\Core\Contracts\MacroDatabaseContracts.cs
- Assets\_Project\Scripts\Core\Contracts\MacroEcosystemVaultContract.cs
- Assets\_Project\Scripts\Core\Contracts\MemorySentinelSignals.cs
- Assets\_Project\Scripts\Core\Contracts\NativeMemoryTrackingBridge.cs
- Assets\_Project\Scripts\Core\Contracts\PersistencePagingContracts.cs
- Assets\_Project\Scripts\Core\Contracts\Physics\HabitatFluidIncursionContracts.cs
- Assets\_Project\Scripts\Core\Contracts\Physics\ReplayDeterminismContracts.cs
- Assets\_Project\Scripts\Core\Contracts\Physics\SeaglidePropulsionContracts.cs
- Assets\_Project\Scripts\Core\Contracts\PlayerHandIkContracts.cs
- Assets\_Project\Scripts\Core\Contracts\PrologueSequenceContracts.cs
- Assets\_Project\Scripts\Core\Contracts\ScalabilityContract.cs
- Assets\_Project\Scripts\Core\Contracts\Signals\DynamicMusicScalarSignal.cs
- Assets\_Project\Scripts\Core\Contracts\SimulationBucketingContracts.cs
- Assets\_Project\Scripts\Core\Contracts\VRInteractionBridgeContracts.cs
- Assets\_Project\Scripts\Core\Data\BabelDictionaryStore.cs
- Assets\_Project\Scripts\Core\Data\Editor\CacheBTreeTopologyXRayWindow.cs
- Assets\_Project\Scripts\Core\Data\H8DataBaker.cs
- Assets\_Project\Scripts\Core\Data\H8StaticDataContracts.cs
- Assets\_Project\Scripts\Core\Data\H8StaticDataSanity.cs
- Assets\_Project\Scripts\Core\Data\StaticDataStore.cs
- Assets\_Project\Scripts\Core\Database\H8MacroDatabaseService.cs
- Assets\_Project\Scripts\Core\Determinism\LockstepStateValidator.cs
- Assets\_Project\Scripts\Core\Diagnostics\AsynchronousTelemetryExporter.cs
- Assets\_Project\Scripts\Core\Diagnostics\Visuals\ArchitectEyeDebugSignal.cs
- Assets\_Project\Scripts\Core\Diagnostics\Visuals\ArchitectEyeVisualizer.cs
- Assets\_Project\Scripts\Core\Diagnostics\Visuals\Editor\ArchitectEyeBlackBoxTimelineViewer.cs
- Assets\_Project\Scripts\Core\Diagnostics\Visuals\VaultMemoryGizmoVisualizer.cs
- Assets\_Project\Scripts\Core\Diagnostics\Visuals\VaultProbeUtility.cs
- Assets\_Project\Scripts\Core\DistanceMath.cs
- Assets\_Project\Scripts\Core\DodReplayRecorder.cs
- Assets\_Project\Scripts\Core\Editor\HapticRumbleDebugGizmo.cs
- Assets\_Project\Scripts\Core\Editor\InputCurveHapticsTunerWindow.cs
- Assets\_Project\Scripts\Core\Editor\TactileSynthesisTunerWindow.cs

## Static Risk Suspects

These are raw static suspects, not confirmed defects. Current manual or line-level review files are the authority for classification where present; editor/tool suspects remain legal only if they cannot execute in gameplay/player hot paths.

Runtime suspects:
Total runtime suspects: 275. Showing first 80. Full list: _scans/04_runtime_architecture_data_telemetry_runtime_risks.txt.

- Assets\_Project\Scripts\Core\BootstrapContracts\BootstrapStatus.cs:338:            Debug.LogError(SafeHaltMessage);
- Assets\_Project\Scripts\Core\ConnectionSplineBatchRenderer.cs:974:            array = new NativeArray<SplineDescriptor>(requiredLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Core\ConnectionSplineBatchRenderer.cs:989:            array = new NativeArray<FlexiblePipeInstanceGpuData>(requiredLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\Core\BurstCallback.cs:83:            _events = new NativeQueue<int>(Allocator.Persistent);
- Assets\_Project\Scripts\Core\BurstCallback.cs:87:                Allocator.Persistent,
- Assets\_Project\Scripts\Core\Bridge\H8PrefabRegistry.cs:471:                prefab.GetComponentsInChildren(true, s_RendererScratch);
- Assets\_Project\Scripts\Core\Data\StaticDataStore.cs:133:                Allocator.Persistent,
- Assets\_Project\Scripts\Core\Data\StaticDataStore.cs:408:                H8Memory.FreeRaw(_ownedFallbackPointer, Allocator.Persistent, SystemID.CoreDataVault);
- Assets\_Project\Scripts\Core\Content\ContentLoreBinaryProvider.cs:133:                Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] Babel dictionary missing.", this);
- Assets\_Project\Scripts\Core\Content\ContentLoreBinaryProvider.cs:156:                Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] Failed to open Babel dictionary.", this);
- Assets\_Project\Scripts\Core\Content\ContentLoreBinaryProvider.cs:400:            Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] Rejected zero hash lore read.", this);
- Assets\_Project\Scripts\Core\Content\ContentLoreBinaryProvider.cs:407:            Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] Missing lore block.", this);
- Assets\_Project\Scripts\Core\Content\ContentLoreBinaryProvider.cs:414:            Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] Unreadable lore block.", this);
- Assets\_Project\Scripts\Core\Content\ContentLoreBinaryProvider.cs:421:            Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] Destination span too small for lore.", this);
- Assets\_Project\Scripts\Core\Content\ContentLoreBinaryProvider.cs:428:            Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] No readable Babel dictionary stream.", this);
- Assets\_Project\Scripts\Core\Content\ContentLoreBinaryProvider.cs:435:            Hecton8.Core.H8Debug.LogError("[ContentLoreBinaryProvider] Partial lore read.", this);
- Assets\_Project\Scripts\Core\Content\ContentAssetHashMap.cs:358:            Hecton8.Core.H8Debug.LogError("[ContentAssetHashMap] Required-hash copy rejected destinationLength=" +
- Assets\_Project\Scripts\Core\Data\H8DataBaker.cs:166:                Hecton8.Core.H8Debug.Log("[H8DataBaker] Static data bake complete. Records=" + result.RecordCount.ToString(CultureInfo.InvariantCulture));
- Assets\_Project\Scripts\Core\Data\H8DataBaker.cs:170:                Hecton8.Core.H8Debug.LogError("[H8DataBaker] " + result.Message);
- Assets\_Project\Scripts\Core\Data\H8DataBaker.cs:1339:                Hecton8.Core.H8Debug.LogError("[H8DataHotReload] " + result.Message);
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:584:            Hecton8.Core.H8Debug.LogError("[ContentBundleReferenceCounter] Invalid ref-count transition.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:591:            Hecton8.Core.H8Debug.LogError("[ContentBundleReferenceCounter] Invalid acquire metadata.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:598:            Hecton8.Core.H8Debug.LogError("[ContentBundleReferenceCounter] Refused to remove active bundle.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:605:            Hecton8.Core.H8Debug.LogError("[ContentBundleReferenceCounter] Vault unavailable.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:612:            Hecton8.Core.H8Debug.LogError("[ContentBundleReferenceCounter] Bundle ref ledger full.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:619:            Hecton8.Core.H8Debug.LogError("[ContentBundleReferenceCounter] Vault ledger count exceeded fixed capacity; cleared residency ledger.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:1100:                Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Hologram proxy mesh/material missing.", this);
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:1542:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Bundle handle table exhausted.", this);
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2210:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Asset hash map missing.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2217:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] No content registry entry.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2224:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] DataVault dependency unavailable on runtime content route.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2231:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Failed to track Addressables bundle handle.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2238:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] No tracked Addressables bundle handle during release.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2245:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Invalid Addressables bundle handle.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2252:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Rejected async load tracking.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2259:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Pending-load vault unavailable.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2266:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Pending-load ledger full.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2273:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Async load completion had no pending entry.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2280:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Invalid VFX prewarm Addressables reference.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2287:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] VFX prewarm handle ledger full.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2294:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] VFX prewarm returned invalid Addressables handle.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2301:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Resident VFX handle ledger full; releasing completed prewarm handle.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2308:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] VFX prewarm handle failed; releasing Addressables handle.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2315:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Hologram proxy unavailable.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2322:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Hologram proxy pool exhausted; pending asset will remain invisible until a proxy frees.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2329:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Pending load vault count exceeded fixed capacity; cleared pending-load ledger.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2336:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Failed to write content blackbox dump.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2343:            Hecton8.Core.H8Debug.LogError("[ContentAuthorityRuntime] Failed to resolve content blackbox dump path.");
- Assets\_Project\Scripts\Core\Content\ContentRuntimeServices.cs:2680:            Hecton8.Core.H8Debug.LogError("[ContentTieredGroupPolicy] Invalid content tier value.");
- Assets\_Project\Scripts\Core\Determinism\LockstepStateValidator.cs:1962:            Hecton8.Core.H8Debug.LogException(ex);
- Assets\_Project\Scripts\Core\UIStateStore.cs:156:            _states = new NativeArray<UIStateData>(StateCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<UIStateData>[StateCount] - headless UI simulation state - owner: UIStateStore
- Assets\_Project\Scripts\Core\UIStateStore.cs:157:            _valueSlots = new NativeArray<UIValueSlot>(ValueSlotCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<UIValueSlot>[ValueSlotCount] - headless numeric UI value bridge - owner: UIStateStore
- Assets\_Project\Scripts\Core\UIStateStore.cs:158:            _historyStates = new NativeArray<UIStateData>(UIStateHistoryFrames, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<UIStateData>[UIStateHistoryFrames] - PDA UI rollback snapshot ring - owner: UIStateStore
- Assets\_Project\Scripts\Core\UIStateStore.cs:159:            _pdaLogEventHashes = new NativeArray<uint>(MaxPdaLogEvents, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<uint>[MaxPdaLogEvents] - PDA event-sourced log history - owner: UIStateStore
- Assets\_Project\Scripts\Core\UIStateStore.cs:160:            _pdaLogEventTimestamps = new NativeArray<float>(MaxPdaLogEvents, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[MaxPdaLogEvents] - PDA event-sourced log timestamps - owner: UIStateStore
- Assets\_Project\Scripts\Core\HectonUrpTextureRequirementsGuard.cs:180:                root.GetComponentsInChildren(true, s_cameraScratch);
- Assets\_Project\Scripts\Core\HectonUrpTextureRequirementsGuard.cs:273:            Hecton8.Core.H8Debug.LogWarning($"[HectonUrpTextureRequirementsGuard] {message}");
- Assets\_Project\Scripts\Core\DodReplayRecorder.cs:974:            NativeArray<T> array = new NativeArray<T>(length, Allocator.Persistent, (NativeArrayOptions)options);
- Assets\_Project\Scripts\Core\GlobalTelemetryBus.cs:749:                    _ringBuffer = new NativeRingBuffer<TelemetryEvent>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeRingBuffer<TelemetryEvent>[1024] — power-of-two black-box ring retaining the last 1000 telemetry frames — owner: GlobalTelemetryBus
- Assets\_Project\Scripts\Core\GlobalTelemetryBus.cs:762:                    _snapshotBuffer = new NativeArray<TelemetryEvent>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<TelemetryEvent>[1024] — telemetry export snapshot staging buffer — owner: GlobalTelemetryBus
- Assets\_Project\Scripts\Core\GlobalTelemetryBus.cs:773:                    _exportScratch = new NativeArray<byte>(exportScratchBytes, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[65552] — unmanaged binary telemetry export scratch — owner: GlobalTelemetryBus
- Assets\_Project\Scripts\Core\ThreadSafeCommandQueue.cs:274:                    _pendingCommands = new NativeQueue<EntityCommand>(Allocator.Persistent); // COLD ALLOC: NativeQueue<EntityCommand>(Persistent) - structural command ingress drained by SystemDispatcher LateUpdate - owner: ThreadSafeCommandQueue
- Assets\_Project\Scripts\Core\ThreadSafeCommandQueue.cs:882:            _pendingStorageReservationCommitResolved = new NativeQueue<StorageReservationCommitResolvedPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<StorageReservationCommitResolvedPayload>[64] - deferred storage reservation acknowledgements - owner: ThreadSafeCommandQueue
- Assets\_Project\Scripts\Core\ThreadSafeCommandQueue.cs:968:            Hecton8.Core.H8Debug.LogError("[ThreadSafeCommandQueue] Storage reservation commit listener capacity exceeded. capacity=" +
- Assets\_Project\Scripts\Core\HectonInputRuntime_HapticSynth.cs:606:                Hecton8.Core.H8Debug.LogError("[InputDispatcher] Haptic synthesis ABI violation.");
- Assets\_Project\Scripts\Core\FrameTimeWatchdog.cs:274:            _frameTimeSamples = new NativeRingBuffer<float>(FrameTimeSampleCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeRingBuffer<float>[64] - fixed frame pacing average, no managed List/array growth - owner: FrameTimeWatchdog
- Assets\_Project\Scripts\Core\HectonArenaAllocator.cs:175:                    Allocator.Persistent,
- Assets\_Project\Scripts\Core\HectonArenaAllocator.cs:378:            H8Memory.FreeRaw(_basePtr, Allocator.Persistent, SystemID.H8Memory);
- Assets\_Project\Scripts\Core\OceanKinematicsRuntimeService.cs:308:            Hecton8.Core.H8Debug.LogError("[OceanKinematicsRuntimeService] Provider capacity exceeded. capacity=" + ProviderCapacity);
- Assets\_Project\Scripts\Core\NativeRingBuffer.cs:34:            _buffer = new NativeArray<T>(capacity, allocator, (NativeArrayOptions)options);
- Assets\_Project\Scripts\Core\H8Debug.cs:21:            Debug.Log(message);
- Assets\_Project\Scripts\Core\H8Debug.cs:33:            Debug.Log(message, context);
- Assets\_Project\Scripts\Core\H8Debug.cs:44:            Debug.LogWarning(message);
- Assets\_Project\Scripts\Core\H8Debug.cs:56:            Debug.LogWarning(message, context);
- Assets\_Project\Scripts\Core\H8Debug.cs:67:            Debug.LogError(message);
- Assets\_Project\Scripts\Core\H8Debug.cs:79:            Debug.LogError(message, context);
- Assets\_Project\Scripts\Core\H8Debug.cs:90:            Debug.LogException(exception);
- Assets\_Project\Scripts\Core\H8Debug.cs:102:            Debug.LogException(exception, context);
- Assets\_Project\Scripts\Core\NativeMemorySentinel.cs:839:                    Hecton8.Core.H8Debug.LogError(CriticalMemoryViolationRegistryCapacityMessage);
- Assets\_Project\Scripts\Core\NativeMemorySentinel.cs:1174:                    Hecton8.Core.H8Debug.LogError(CriticalMemoryViolationUnsafeLeakMessage);

Editor/tool/static suspects:
Total editor/tool/static suspects: 123. Showing first 80. Full list: _scans/04_runtime_architecture_data_telemetry_editor_tool_risks.txt.

- Assets\_Project\Scripts\Core\Bridge\Editor\H8PrefabRegistryWindow.cs:104:                entriesLabel.text = "Entries: 0";
- Assets\_Project\Scripts\Core\Bridge\Editor\H8PrefabRegistryWindow.cs:105:                vramLabel.text = "VRAM Estimate MB: 0";
- Assets\_Project\Scripts\Core\Bridge\Editor\H8PrefabRegistryWindow.cs:106:                validationLabel.text = "Validation: no registry";
- Assets\_Project\Scripts\Core\Bridge\Editor\H8PrefabRegistryWindow.cs:110:            entriesLabel.text = "Entries: " + registry.EntryCount.ToString(CultureInfo.InvariantCulture);
- Assets\_Project\Scripts\Core\Bridge\Editor\H8PrefabRegistryWindow.cs:111:            vramLabel.text = "VRAM Estimate MB: " + (registry.EstimateTotalVramBytes() >> 20).ToString(CultureInfo.InvariantCulture);
- Assets\_Project\Scripts\Core\Bridge\Editor\H8PrefabRegistryWindow.cs:112:            validationLabel.text = BuildValidationSummary(registry);
- Assets\_Project\Scripts\Core\Bridge\Editor\H8PrefabRegistryWindow.cs:188:                Debug.LogError("[H8Bridge] Prefab registry bind failed. Fix duplicate prefab hashes or wait for DataVault allocation fences to clear.");
- Assets\_Project\Scripts\Core\Bridge\Editor\H8PrefabRegistryWindow.cs:278:                Debug.LogError("[H8Bridge] Prefab registry bind failed. Fix duplicate prefab hashes or wait for DataVault allocation fences to clear.");
- Assets\_Project\Scripts\Core\Bridge\Editor\H8BridgeFacadeEditors.cs:55:                Debug.LogError("[H8Bridge] Design DataVault sync failed. Fix duplicate field hashes or wait for DataVault allocation fences to clear.");
- Assets\_Project\Scripts\Core\Bridge\Editor\H8BridgeFacadeEditors.cs:154:                Debug.LogError("[H8Bridge] Input map sync failed. Fix duplicate action hashes or wait for DataVault allocation fences to clear.");
- Assets\_Project\Scripts\Core\Bridge\Editor\H8BridgeContractGenerator.cs:37:                    Debug.LogError("[H8Bridge] Contract generation skipped duplicate design field hashes in " + path);
- Assets\_Project\Scripts\Core\Bridge\Editor\H8BridgeContractGenerator.cs:64:            Debug.Log("[H8Bridge] Design facade contracts generated.");
- Assets\_Project\Scripts\Core\Content\Editor\ContentAuthorityBuildValidators.cs:81:            Debug.Log("[ContentAuthority] Validation passed.");
- Assets\_Project\Scripts\Core\Content\Editor\ContentAuthorityBuildValidators.cs:788:                ContentLoreBinaryProvider[] providers = prefab.GetComponentsInChildren<ContentLoreBinaryProvider>(true);
- Assets\_Project\Scripts\Core\Content\Editor\ContentAuthorityBuildValidators.cs:999:                ContentAuthorityRuntime[] runtimes = prefab.GetComponentsInChildren<ContentAuthorityRuntime>(true);
- Assets\_Project\Scripts\Core\Content\Editor\ContentAuthorityAssetPostprocessor.cs:22:                StripMeshColliders(root);
- Assets\_Project\Scripts\Core\Content\Editor\ContentAuthorityAssetPostprocessor.cs:51:        private static void StripMeshColliders(GameObject root)
- Assets\_Project\Scripts\Core\Content\Editor\ContentAuthorityAssetPostprocessor.cs:53:            MeshCollider[] colliders = root.GetComponentsInChildren<MeshCollider>(true);
- Assets\_Project\Scripts\Core\Content\Editor\ContentAuthorityAssetPostprocessor.cs:60:            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
- Assets\_Project\Scripts\Core\Content\Editor\ContentAuthorityAssetPostprocessor.cs:91:            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
- Assets\_Project\Scripts\Core\Content\Editor\ContentAuthorityAssetPostprocessor.cs:126:            BoxCollider[] boxes = root.GetComponentsInChildren<BoxCollider>(true);
- Assets\_Project\Scripts\Core\Content\Editor\ContentAuthorityAssetPostprocessor.cs:129:                Debug.LogError("[ContentPhysicsProxyBaker] Bake rejected for " + root.name + ": at least two BoxColliders are required.");
- Assets\_Project\Scripts\Core\Content\Editor\ContentAuthorityAssetPostprocessor.cs:138:                    Debug.LogError("[ContentPhysicsProxyBaker] Bake rejected for " + root.name + ": non-finite BoxCollider bounds.");
- Assets\_Project\Scripts\Core\Content\Editor\ContentAuthorityAssetPostprocessor.cs:147:                Debug.LogError("[ContentPhysicsProxyBaker] Bake rejected for " + root.name + ": invalid convex hull bounds.");
- Assets\_Project\Scripts\Core\Content\Editor\ContentAuthorityAssetPostprocessor.cs:165:            MeshCollider hull = proxy.AddComponent<MeshCollider>();
- Assets\_Project\Scripts\Core\Content\Editor\ContentAuthorityAssetPostprocessor.cs:209:            mesh.RecalculateNormals();
- Assets\_Project\Scripts\Core\Editor\OOP_Gamepad_Scanner.cs:51:            Debug.Log("[OOP_Gamepad_Scanner] Report written: " + sharedReportPath);
- Assets\_Project\Scripts\Core\Data\Editor\CacheBTreeTopologyXRayWindow.cs:467:            using (NativeArray<byte> bytes = new NativeArray<byte>(_fileBytes, Allocator.TempJob))
- Assets\_Project\Scripts\Core\Data\Editor\CacheBTreeTopologyXRayWindow.cs:468:            using (NativeArray<DataOffsetLengthDTO> output = new NativeArray<DataOffsetLengthDTO>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
- Assets\_Project\Scripts\Core\Data\Editor\CacheBTreeTopologyXRayWindow.cs:469:            using (NativeArray<uint> trace = new NativeArray<uint>(MaxTraceNodes, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
- Assets\_Project\Scripts\Core\Data\Editor\CacheBTreeTopologyXRayWindow.cs:517:            _summaryLabel.text = _formatName + " | nodes " + _nodeSnapshotCount +
- Assets\_Project\Scripts\Core\Data\Editor\CacheBTreeTopologyXRayWindow.cs:521:            _traceLabel.text = "Live key hash 0x" + _lastSearchHash.ToString("X8") +
- Assets\_Project\Scripts\Core\Data\Editor\CacheBTreeTopologyXRayWindow.cs:537:            _telemetryLabel.text = "Telemetry searches " + last.SearchCount +
- Assets\_Project\Scripts\Core\Diagnostics\Visuals\Editor\ArchitectEyeBlackBoxTimelineViewer.cs:157:                _pathLabel.text = $"Path: {_loadedPath}";
- Assets\_Project\Scripts\Core\Diagnostics\Visuals\Editor\ArchitectEyeBlackBoxTimelineViewer.cs:237:                    _detailLabels[i].text = string.Empty;
- Assets\_Project\Scripts\Core\Diagnostics\Visuals\Editor\ArchitectEyeBlackBoxTimelineViewer.cs:243:            _detailLabels[0].text = $"Frame: {frame.Frame}";
- Assets\_Project\Scripts\Core\Diagnostics\Visuals\Editor\ArchitectEyeBlackBoxTimelineViewer.cs:244:            _detailLabels[1].text = $"Quads: {frame.QuadCount}";
- Assets\_Project\Scripts\Core\Diagnostics\Visuals\Editor\ArchitectEyeBlackBoxTimelineViewer.cs:245:            _detailLabels[2].text = $"Signal Lanes: {frame.SignalLaneCount}";
- Assets\_Project\Scripts\Core\Diagnostics\Visuals\Editor\ArchitectEyeBlackBoxTimelineViewer.cs:246:            _detailLabels[3].text = $"Signal Pressure: {frame.SignalPressure01:0.000}";
- Assets\_Project\Scripts\Core\Diagnostics\Visuals\Editor\ArchitectEyeBlackBoxTimelineViewer.cs:247:            _detailLabels[4].text = $"Vault Pressure: {frame.VaultPressure01:0.000}";
- Assets\_Project\Scripts\Core\Diagnostics\Visuals\Editor\ArchitectEyeBlackBoxTimelineViewer.cs:248:            _detailLabels[5].text = $"Memory Fragmentation: {frame.MemoryFragmentation01:0.000}";
- Assets\_Project\Scripts\Core\Diagnostics\Visuals\Editor\ArchitectEyeBlackBoxTimelineViewer.cs:249:            _detailLabels[6].text = $"Health: {frame.SystemHealth01:0.000}";
- Assets\_Project\Scripts\Core\Diagnostics\Visuals\Editor\ArchitectEyeBlackBoxTimelineViewer.cs:250:            _detailLabels[7].text = $"Frame Time Ms: {frame.FrameTimeMs:0.000}";
- Assets\_Project\Scripts\Core\Diagnostics\Visuals\Editor\ArchitectEyeBlackBoxTimelineViewer.cs:251:            _detailLabels[8].text = $"Non-Finite: {frame.NonFiniteCount}";
- Assets\_Project\Scripts\Core\Diagnostics\Visuals\Editor\ArchitectEyeBlackBoxTimelineViewer.cs:252:            _detailLabels[9].text = $"Kill Switch Mask: 0x{frame.KillSwitchMask:X8}";
- Assets\_Project\Scripts\Core\Diagnostics\Visuals\Editor\ArchitectEyeBlackBoxTimelineViewer.cs:368:            Vector3 point = UnityEngine.Physics.Raycast(ray, out RaycastHit hit, 10000f)
- Assets\_Project\Scripts\Core\Diagnostics\Visuals\Editor\ArchitectEyeBlackBoxTimelineViewer.cs:416:            Vector3 point = UnityEngine.Physics.Raycast(ray, out RaycastHit hit, 10000f)
- Assets\_Project\Scripts\Core\Memory\Editor\OOP_MemorySentryConcurrentRelocationFuzzer.cs:95:            Debug.Log("DataVault compaction fuzzer completed. Iterations=" + result.TotalOperations + " Report=" + ReportPath);
- Assets\_Project\Scripts\Core\Memory\Editor\OOP_MemorySentryConcurrentRelocationFuzzer.cs:105:            Debug.Log("DataVault false-positive corruption probe caught expected corruption. Report=" + ReportPath);
- Assets\_Project\Scripts\Core\Memory\Editor\OOP_MemorySentryConcurrentRelocationFuzzer.cs:166:                    Allocator.Persistent,
- Assets\_Project\Scripts\Core\Memory\Editor\OOP_MemorySentryConcurrentRelocationFuzzer.cs:171:                    Allocator.Persistent,
- Assets\_Project\Scripts\Core\Memory\Editor\OOP_MemorySentryConcurrentRelocationFuzzer.cs:647:                    handle.Complete();
- Assets\_Project\Scripts\Core\Signals\Editor\SignalBusEditorTeardown1428.cs:49:                Debug.LogError("[SignalBusEditorTeardown1428] Signal lane teardown failed: " + exception.Message);
- Assets\_Project\Scripts\Global\FutureSeams\Editor\FutureSystemSeamStaticValidator.cs:44:                Debug.Log("[H8 FutureSeams] PASS: dormant reservation records, binary writer, public API closure, survival envelope, and blackbox ring validated.");
- Assets\_Project\Scripts\Global\FutureSeams\Editor\FutureSystemSeamStaticValidator.cs:52:            Debug.LogError("[H8 FutureSeams] FAIL: dormant future-seam contract validation rejected the current reservation set.");
- Assets\_Project\Scripts\Optimization\Editor\HeapSanitizerTunerWindow.cs:218:                _activeLabel.text = "Active: " + active.ToString();
- Assets\_Project\Scripts\Optimization\Editor\HeapSanitizerTunerWindow.cs:224:                _hitsLabel.text = "Hits: " + hits.ToString();
- Assets\_Project\Scripts\Optimization\Editor\HeapSanitizerTunerWindow.cs:230:                _missesLabel.text = "Misses: " + misses.ToString();
- Assets\_Project\Scripts\Optimization\Editor\HeapSanitizerTunerWindow.cs:236:                _releasedLabel.text = "Released: " + released.ToString();
- Assets\_Project\Scripts\Optimization\Editor\HeapSanitizerTunerWindow.cs:291:                    _leakBanner.text = "LEAK SUSPECT  asset=0x" + hash.ToString("X8") +
- Assets\_Project\Scripts\Optimization\Editor\HeapSanitizerTunerWindow.cs:323:                    _trackerRows[i].text = "0x" + tracker.AssetHash.ToString("X8") +
- Assets\_Project\Scripts\Optimization\Editor\HeapSanitizerTunerWindow.cs:346:            _statusLabel.text = registered
- Assets\_Project\Scripts\Optimization\Editor\HectonTransparentOverdrawBuildGuard.cs:50:            Debug.Log(
- Assets\_Project\Scripts\Optimization\Editor\RenderTextureResolutionAnalyzer.cs:58:                Debug.LogWarning("[ResolutionAnalyzer] RenderTextureLifecycleTracker not available. Enter Play Mode first.");
- Assets\_Project\Scripts\Optimization\Editor\RenderTextureResolutionAnalyzer.cs:132:                Debug.LogWarning("[ResolutionAnalyzer] Cannot capture screenshot: RenderTexture is null");
- Assets\_Project\Scripts\Optimization\Editor\RenderTextureResolutionAnalyzer.cs:177:                    Debug.Log($"[ResolutionAnalyzer] Screenshot saved: {fullPath}");
- Assets\_Project\Scripts\Optimization\Editor\VRAMStreamingStaticAssertions1617.cs:19:            Debug.Log("[VRAMStreamingStaticAssertions1617] Static VRAM streaming assertions passed.");
- Assets\_Project\Scripts\Optimization\Editor\RenderTextureOptimizationWindow.cs:166:                Debug.Log($"[FormatOptimizer] Applied format optimization to {rec.RenderTexture.name}: {rec.CurrentFormat} → {rec.RecommendedFormat}");
- Assets\_Project\Scripts\Optimization\Editor\RenderTextureOptimizationWindow.cs:254:                Debug.Log($"[ResolutionAnalyzer] Applied resolution optimization to {rec.RenderTexture.name}: {rec.CurrentWidth}x{rec.CurrentHeight} → {rec.RecommendedWidth}x{rec.RecommendedHeight}");
- Assets\_Project\Scripts\Optimization\Editor\VRAMTextureFootprintScanner1617.cs:27:            Debug.Log(
- Assets\_Project\Scripts\Optimization\Editor\VRAMIntegratorVerifier1617.cs:45:            Debug.Log("Agent 1617 APEX integrator verification passed.");
- Assets\_Project\Scripts\Optimization\Editor\RenderTextureFormatOptimizer.cs:70:                Debug.LogWarning("[FormatOptimizer] RenderTextureLifecycleTracker not available. Enter Play Mode first.");
- Assets\_Project\Scripts\Optimization\Editor\VRAMDiagnosticReport.cs:66:            Debug.Log($"[VRAMDiagnostic] Report generated: {filepath}");
- Assets\_Project\Scripts\Optimization\Editor\VRAMValidator.cs:45:            Debug.Log("[VRAMValidator] Texture budget gate passed.");
- Assets\_Project\Scripts\Optimization\Editor\VRAMValidator.cs:52:            Debug.Log(
- Tools\DataMonolithBakeCli\DataMonolithSourceInventoryProbe.cs:340:                .Where(s => s.PayloadBytes > 0UL && ids.Any(id => s.SectionId == (uint)id))
- Tools\DataMonolithBakeCli\DataMonolithSourceInventoryProbe.cs:342:                .ToList();
- Tools\DataMonolithBakeCli\DataMonolithSourceInventoryProbe.cs:356:                Sections = selected.Select(s => s.Name).ToArray()
- Tools\PresentationDecouplingAudit\Program.cs:678:            (left.EndsWith(".color", StringComparison.Ordinal) || left.EndsWith(".material", StringComparison.Ordinal) || left.EndsWith(".materials", StringComparison.Ordinal)))
- Tools\VaultNativeAliasRoslynAudit\Program.cs:130:            string attributes = string.Join(" ", field.AttributeLists.Select(static list => list.ToString()));

## Exists / Missing / Required Proof

- Exists: bible routes exist and static implementation evidence was found.
- Partial: all 275 runtime static suspect lines have method-level classification in `LINE_LEVEL_CLASSIFICATION.md`; runtime/profiler/player proof is still missing.
- Editor/tool: static suspects exist but may be legal if editor-only or cold-path.
- Required proof: Owner phase map, route cards, SignalBus/DataVault payload layouts, profiler markers, 300-frame black-box dump, no hot registry polling scan.

## Next Audit Action

Use `LINE_LEVEL_CLASSIFICATION.md`, close DataVault/H8Memory, debug, bootstrap, prewarm, transition, player-rebind, ContentAuthority, profiler, player-build, and device proof gates before any green/release claim.
