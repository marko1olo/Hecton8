# Runtime Preclassification - Persistence, Streaming, Release, Platform, Modding, Testing

Status: HEURISTIC FIRST PASS - MANUAL REVIEW STILL REQUIRED
Date: 2026-06-02

This file groups static runtime suspects by a conservative heuristic. It can reduce review time, but it cannot prove a line is legal or illegal without reading the containing method and owner phase.

Total runtime suspects: 126.

## Summary

- LEGAL_EDITOR_OR_DEV_GUARDED: 75
- LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH: 34
- REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED: 16
- REVIEW_LOG_GUARD_REQUIRED: 1

## LEGAL_EDITOR_OR_DEV_GUARDED (75)

- Runtime debug logging | Assets\_Project\Scripts\SaveSystem\H8BinaryWorldPager.cs:827:            Hecton8.Core.H8Debug.LogWarning("H8BinaryWorldPager disabled page IO after initialization fault. reason=" + reason);
- Runtime debug logging | Assets\_Project\Scripts\Optimization\CameraRTManager.cs:222:            Hecton8.Core.H8Debug.LogWarning(_reportBuilder.ToString(), this);
- Runtime debug logging | Assets\_Project\Scripts\Optimization\PostFXRTManager.cs:220:            Hecton8.Core.H8Debug.LogWarning(_reportBuilder.ToString(), this);
- Runtime debug logging | Assets\_Project\Scripts\Optimization\UIRTManager.cs:213:            Hecton8.Core.H8Debug.LogWarning("[UIRTManager] BUDGET EXCEEDED", this);
- Runtime debug logging | Assets\_Project\Scripts\Optimization\AssetLifecycleGovernor.cs:243:                Hecton8.Core.H8Debug.LogError("[AssetLifecycleGovernor] AssetHandleMapEntryDTO must remain 64 bytes.", this);
- Runtime debug logging | Assets\_Project\Scripts\Optimization\AssetLifecycleGovernor.cs:245:                Hecton8.Core.H8Debug.LogError("[AssetLifecycleGovernor] AssetTrackerDTO must remain 64 bytes.", this);
- Runtime debug logging | Assets\_Project\Scripts\Optimization\AssetLifecycleGovernor.cs:891:                Hecton8.Core.H8Debug.LogError("[AssetLifecycleGovernor] Double release detected.", this);
- Runtime debug logging | Assets\_Project\Scripts\Optimization\AssetLifecycleGovernor.cs:993:            Hecton8.Core.H8Debug.LogError("[AssetLifecycleGovernor] Asset load failed.", this);
- Runtime debug logging | Assets\_Project\Scripts\Optimization\AssetLifecycleGovernor.cs:5248:            Hecton8.Core.H8Debug.LogError("[AssetLifecycleGovernor] Asset key collision.");
- Runtime debug logging | Assets\_Project\Scripts\Optimization\VisorRTManager.cs:213:            Hecton8.Core.H8Debug.LogWarning("[VisorRTManager] BUDGET EXCEEDED", this);
- Runtime debug logging | Assets\_Project\Scripts\Optimization\RenderTexturePool.cs:205:                Hecton8.Core.H8Debug.LogWarning("[RTPool] Return called with null RenderTexture");
- Runtime debug logging | Assets\_Project\Scripts\Optimization\VRAMMonitor.cs:573:            Hecton8.Core.H8Debug.LogWarning(_reportBuilder.ToString(), this);
- Runtime debug logging | Assets\_Project\Scripts\Optimization\RenderTextureLifecycleTracker.cs:141:                Hecton8.Core.H8Debug.LogError("[LifecycleTracker] RegisterAllocation called with null RenderTexture");
- Runtime debug logging | Assets\_Project\Scripts\Optimization\RenderTextureLifecycleTracker.cs:149:                Hecton8.Core.H8Debug.LogError("[LifecycleTracker] RegisterAllocation called with null owner");
- Runtime debug logging | Assets\_Project\Scripts\Optimization\RenderTextureLifecycleTracker.cs:159:                Hecton8.Core.H8Debug.LogWarning(
- Runtime debug logging | Assets\_Project\Scripts\Optimization\RenderTextureLifecycleTracker.cs:394:                    Hecton8.Core.H8Debug.LogError(
- Runtime debug logging | Assets\_Project\Scripts\QA\Headless\HeadlessStressFractureBot.cs:523:            Hecton8.Core.H8Debug.LogWarning(FormatStaticHPhiLog(
- Runtime debug logging | Assets\_Project\Scripts\QA\Headless\HeadlessStressFractureBot.cs:935:            Hecton8.Core.H8Debug.LogError(status);
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\HectonEventBus.cs:181:                Hecton8.Core.H8Debug.LogError("[HectonEventBus] Cannot subscribe a null handler.");
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\HectonEventBus.cs:231:                Hecton8.Core.H8Debug.LogError("[HectonEventBus] Cannot subscribe a null native payload handler.");
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\HectonEventBus.cs:256:                Hecton8.Core.H8Debug.LogError("[HectonEventBus] Cannot subscribe a null projected event handler.");
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\HectonEventBus.cs:308:                Hecton8.Core.H8Debug.LogError("[HectonEventBus] Cannot publish a null event instance.");
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\HectonEventBus.cs:427:            Hecton8.Core.H8Debug.LogError(RecursiveCascadeCriticalMessage);
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\HectonEventBus.cs:466:            Hecton8.Core.H8Debug.LogWarning(ModStallWarningMessage);
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\HectonEventBus.cs:601:                            Hecton8.Core.H8Debug.LogError("[HectonEventBus] Unmanaged subscriber threw during payload dispatch.");
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\HectonEventBus.cs:764:                            Hecton8.Core.H8Debug.LogError("[HectonEventBus] Native subscriber threw during payload dispatch.");
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\HectonEventBus.cs:936:                            Hecton8.Core.H8Debug.LogError("[HectonEventBus] Subscriber threw during managed payload dispatch.");
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModAssetManager.cs:108:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] SECURITY_VIOLATION: mod '", modId, "' attempted to load unauthorized prefab reference '", assetName, "'."));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModAssetManager.cs:120:            Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Asset '", assetName, "' was not found in bundle for mod '", modId, "'."));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModAssetManager.cs:140:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Failed to load AssetBundle '", bundlePath, "' for mod '", modId, "'."));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModAssetManager.cs:179:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Rejected inaccessible raw texture '", filePath, "': ", exception.Message));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModAssetManager.cs:184:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Failed to read raw texture '", filePath, "': ", exception.Message));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModAssetManager.cs:189:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Rejected invalid raw texture read '", filePath, "': ", exception.Message));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModAssetManager.cs:201:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] PNG decode failed for '", filePath, "'."));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModAssetManager.cs:208:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Raw texture '", filePath, "' exceeded ", MaxRawTextureDimensionLabel, "px dimension cap."));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModAssetManager.cs:227:                    Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Raw texture '", filePath, "' exceeded ", MaxRawTextureBytesLabel, " byte cap."));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModAssetManager.cs:233:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Failed to inspect raw texture '", filePath, "': ", exception.Message));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModAssetManager.cs:238:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Rejected invalid raw texture '", filePath, "': ", exception.Message));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModAssetManager.cs:319:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Failed to scan project content ledger for mod allowlist: ", exception.Message));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModAssetManager.cs:430:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModAssetManager] Rejected invalid raw texture path '", relativePath, "': ", exception.Message));
- Additional lines omitted here: 35. Use `../_scans/10_persistence_streaming_release_platform_runtime_risks.txt` for the full list.

## LIKELY_LEGAL_COLD_OR_OWNER_LIFETIME_PATH (34)

- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore_SHINOBU357.cs:204:                    payloadOwner = new NativeArray<byte>(payloadBytes, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore_SHINOBU357.cs:215:                    corruptWalOwner = new NativeArray<byte>(payloadBytes, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore_SHINOBU357.cs:226:                    stateOwner = new NativeArray<WalFuzzStateDTO>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore_SHINOBU357.cs:237:                    telemetryOwner = new NativeArray<WalFuzzTelemetryEntry>(Shinobu357TelemetryCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore_SHINOBU357.cs:439:                    hashScratchOwner = new NativeArray<byte>(backupByteCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore_SHINOBU357.cs:501:                    fileHandleStatusOwner = new NativeArray<WalFuzzFileHandleStatusDTO>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:178:            NativeArray<WalFuzzerProfileDTO> profiles = new NativeArray<WalFuzzerProfileDTO>(4, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:221:                payload = new NativeArray<byte>(payloadBytes, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:222:                recovered = new NativeArray<byte>(payloadBytes, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:340:            NativeArray<byte> bytes = new NativeArray<byte>((int)info.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:719:                buffers.CurrentTree = new NativeArray<MerkleNodeDTO>(SaveStateMerkleTree.RequiredNodeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:720:                buffers.PreviousTree = new NativeArray<MerkleNodeDTO>(SaveStateMerkleTree.RequiredNodeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:721:                buffers.LeafDescriptors = new NativeArray<StateLeafDescriptor>(SaveStateMerkleTree.LeafCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:722:                buffers.DeltaRecords = new NativeArray<StateDeltaRecordDTO>(SaveStateMerkleTree.LeafCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:723:                buffers.DeltaBytes = new NativeArray<byte>(deltaCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:724:                buffers.PrunedDeltaBytes = new NativeArray<byte>(deltaCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:725:                buffers.CompressedBytes = new NativeArray<byte>(compressedCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:726:                buffers.Lz4BlockHeaders = new NativeArray<Lz4SubBlockHeader>(blockHeaderCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:729:                buffers.Lz4HashTable = new NativeArray<int>(SaveStateMerkleTree.HashTableSlots, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:730:                replayedDeltaBytes = new NativeArray<byte>(deltaCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:1110:                payload = new NativeArray<byte>(payloadBytes, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:1111:                readback = new NativeArray<byte>(payloadBytes, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Optimization\AssetLifecycleGovernor.cs:3712:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\QA\Headless\JacobiStressFuzzer\PowerGridJacobiStressFuzzer.cs:334:                scratch = new NativeArray<byte>(PowerJacobiStressFuzzerConstants.CsvScratchBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\IModResourceProxy.cs:126:                _resourceIndexByHash = new NativeHashMap<uint, int>(ResourceCapacity, Allocator.Persistent); // COLD ALLOC: NativeHashMap<uint,int>[256] - O(1) resource hash to sidecar index - owner: ModResourceRegistry
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\ModEventProjectionBridge.cs:219:            _cullTelemetry = new NativeArray<ModCullTelemetryEntry>(BlackboxCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ModCullTelemetryEntry>[300] - culled mod hash blackbox ring, local bridge-owned memory to avoid DataVault hot writes - owner: ModEventProjectionBridge
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\FutureCommandSandboxValidator.cs:2466:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\FutureCommandSandboxValidator.cs:2475:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\FutureCommandSandboxValidator.cs:2484:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\FutureCommandSandboxValidator.cs:2493:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\FutureCommandSandboxValidator.cs:2502:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\FutureCommandSandboxValidator.cs:2874:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\FutureCommandSandboxValidator.cs:2926:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Networking\HectonRollbackNetcodeRuntime.cs:1550:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

## REVIEW_NATIVE_LIFETIME_OR_ALLOCATOR_PROOF_REQUIRED (16)

- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\EntityDeltaCompressionArchitecture.cs:1349:                NativeArray<byte> payload = new NativeArray<byte>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\H8BinaryWorldPager.cs:3174:                array = new NativeArray<T>(length, Allocator.Persistent, options);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore_SHINOBU357.cs:242:                legacyTelemetry = new NativeArray<WalFuzzerTelemetryEntry>(TelemetryCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:223:                jobResult = new NativeArray<WalFuzzerResultDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:224:                telemetry = new NativeArray<WalFuzzerTelemetryEntry>(TelemetryCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:727:                buffers.TelemetryRing = new NativeArray<SaveMerkleTelemetryEntry>(SaveStateMerkleTree.TelemetryRingFrames, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:728:                buffers.Counters = new NativeArray<int>(MerkleCounterCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:731:                replayCounters = new NativeArray<int>(MerkleCounterCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Optimization\PreInitAssetIdMap.cs:66:            _guidRecords = new NativeArray<AssetGuidIdRecord>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\Optimization\PreInitAssetIdMap.cs:68:                Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\ModEventProjectionBridge.cs:27:        private const Allocator SignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\ModCommandDispatcher.cs:226:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\ModCommandDispatcher.cs:227:        private const Allocator DataVaultExemptOwnerIndexAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\ModRegistryEvents.cs:65:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\ModRuntimeState.cs:307:                payloadBytes = new NativeArray<byte>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\ModRuntimeState.cs:372:                payloadBytes = new NativeArray<byte>(

## REVIEW_LOG_GUARD_REQUIRED (1)

- Runtime debug logging | Assets\_Project\Scripts\QA\QA_WatchdogBot.cs:5:// Hot path rules: no strings, no LINQ, no scene searches, no Debug.Log, no
