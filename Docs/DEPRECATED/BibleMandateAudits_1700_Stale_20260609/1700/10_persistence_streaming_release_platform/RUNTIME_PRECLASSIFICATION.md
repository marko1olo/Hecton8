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
- Runtime debug logging | Assets\_Project\Scripts\Optimization\AssetLifecycleGovernor.cs:243:                Hecton8.Core.H8Debug.LogError("[AssetLifecycleGovernor] AssetHandleMapEntryDTO must remain 64 bytes.", this);
- Runtime debug logging | Assets\_Project\Scripts\Optimization\AssetLifecycleGovernor.cs:245:                Hecton8.Core.H8Debug.LogError("[AssetLifecycleGovernor] AssetTrackerDTO must remain 64 bytes.", this);
- Runtime debug logging | Assets\_Project\Scripts\Optimization\AssetLifecycleGovernor.cs:891:                Hecton8.Core.H8Debug.LogError("[AssetLifecycleGovernor] Double release detected.", this);
- Runtime debug logging | Assets\_Project\Scripts\Optimization\AssetLifecycleGovernor.cs:993:            Hecton8.Core.H8Debug.LogError("[AssetLifecycleGovernor] Asset load failed.", this);
- Runtime debug logging | Assets\_Project\Scripts\Optimization\AssetLifecycleGovernor.cs:5248:            Hecton8.Core.H8Debug.LogError("[AssetLifecycleGovernor] Asset key collision.");
- Runtime debug logging | Assets\_Project\Scripts\Optimization\PostFXRTManager.cs:220:            Hecton8.Core.H8Debug.LogWarning(_reportBuilder.ToString(), this);
- Runtime debug logging | Assets\_Project\Scripts\Optimization\RenderTexturePool.cs:205:                Hecton8.Core.H8Debug.LogWarning("[RTPool] Return called with null RenderTexture");
- Runtime debug logging | Assets\_Project\Scripts\Optimization\VisorRTManager.cs:213:            Hecton8.Core.H8Debug.LogWarning("[VisorRTManager] BUDGET EXCEEDED", this);
- Runtime debug logging | Assets\_Project\Scripts\Optimization\UIRTManager.cs:213:            Hecton8.Core.H8Debug.LogWarning("[UIRTManager] BUDGET EXCEEDED", this);
- Runtime debug logging | Assets\_Project\Scripts\Optimization\RenderTextureLifecycleTracker.cs:141:                Hecton8.Core.H8Debug.LogError("[LifecycleTracker] RegisterAllocation called with null RenderTexture");
- Runtime debug logging | Assets\_Project\Scripts\Optimization\RenderTextureLifecycleTracker.cs:149:                Hecton8.Core.H8Debug.LogError("[LifecycleTracker] RegisterAllocation called with null owner");
- Runtime debug logging | Assets\_Project\Scripts\Optimization\RenderTextureLifecycleTracker.cs:159:                Hecton8.Core.H8Debug.LogWarning(
- Runtime debug logging | Assets\_Project\Scripts\Optimization\RenderTextureLifecycleTracker.cs:394:                    Hecton8.Core.H8Debug.LogError(
- Runtime debug logging | Assets\_Project\Scripts\Optimization\VRAMMonitor.cs:573:            Hecton8.Core.H8Debug.LogWarning(_reportBuilder.ToString(), this);
- Runtime debug logging | Assets\_Project\Scripts\QA\Headless\HeadlessStressFractureBot.cs:523:            Hecton8.Core.H8Debug.LogWarning(FormatStaticHPhiLog(
- Runtime debug logging | Assets\_Project\Scripts\QA\Headless\HeadlessStressFractureBot.cs:935:            Hecton8.Core.H8Debug.LogError(status);
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModWorldPersistenceManager.cs:196:                Hecton8.Core.H8Debug.LogWarning($"[ModWorldPersistenceManager] Prefab '{assetName}' for mod '{modId}' could not be resolved.");
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModWorldPersistenceManager.cs:205:                Hecton8.Core.H8Debug.LogWarning("[ModWorldPersistenceManager] GlobalRegistry.ObjectPoolService is unavailable. Persistent mod spawn was rejected.");
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModWorldPersistenceManager.cs:309:                Hecton8.Core.H8Debug.LogWarning($"[ModWorldPersistenceManager] Failed to parse mod world payload: {exception.Message}");
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModWorldPersistenceManager.cs:387:                    Hecton8.Core.H8Debug.LogWarning(
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModSettingsRegistry.cs:305:                Hecton8.Core.H8Debug.LogWarning("[ModSettingsRegistry] Refused to register a setting with an empty modId or settingName.");
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModSettingsRegistry.cs:360:                Hecton8.Core.H8Debug.LogWarning($"[ModSettingsRegistry] Toggle callback failed for mod '{modId}': {exception}");
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModSettingsRegistry.cs:378:                Hecton8.Core.H8Debug.LogWarning($"[ModSettingsRegistry] Slider callback failed for mod '{modId}': {exception}");
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:138:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] LoadMods failed: ", ex.Message));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:231:            Hecton8.Core.H8Debug.LogWarning("[ModLoader] WARNING: External managed code mods require a Mono scripting backend. IL2CPP builds cannot load runtime assemblies dynamically.");
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:294:                        Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Manifest discovery capped at ", MaxDiscoveredManifestCountLabel, " packages under '", modsRoot, "'."));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:303:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Manifest discovery skipped inaccessible path under '", modsRoot, "': ", exception.Message));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:307:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Manifest discovery failed under '", modsRoot, "': ", exception.Message));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:311:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Manifest discovery aborted under '", modsRoot, "': ", exception.Message));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:329:                    Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Skipped manifest '", manifestPath, "': invalid Id. ", modIdError));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:392:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Failed to read manifest '", manifestPath, "': ", ex.Message));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:469:                    Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Skipped manifest '", manifestPath, "': manifest file is missing or empty."));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:475:                    Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Skipped manifest '", manifestPath, "': manifest exceeds ", MaxManifestBytesLabel, " byte cap."));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:481:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Failed to inspect manifest '", manifestPath, "': ", exception.Message));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:486:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Rejected invalid manifest path '", manifestPath, "': ", exception.Message));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:894:                        Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Top-level ", fileKind, " discovery capped at ", maxCountLabel, " files under '", directory, "'."));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:904:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Top-level ", fileKind, " discovery skipped inaccessible path under '", directory, "': ", exception.Message));
- Runtime debug logging | Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:910:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Top-level ", fileKind, " discovery failed under '", directory, "': ", exception.Message));
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
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\ModEventProjectionBridge.cs:219:            _cullTelemetry = new NativeArray<ModCullTelemetryEntry>(BlackboxCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ModCullTelemetryEntry>[300] - culled mod hash blackbox ring, local bridge-owned memory to avoid DataVault hot writes - owner: ModEventProjectionBridge
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\IModResourceProxy.cs:126:                _resourceIndexByHash = new NativeHashMap<uint, int>(ResourceCapacity, Allocator.Persistent); // COLD ALLOC: NativeHashMap<uint,int>[256] - O(1) resource hash to sidecar index - owner: ModResourceRegistry
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\FutureCommandSandboxValidator.cs:2483:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\FutureCommandSandboxValidator.cs:2492:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\FutureCommandSandboxValidator.cs:2501:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\FutureCommandSandboxValidator.cs:2510:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\FutureCommandSandboxValidator.cs:2519:                    Allocator.Persistent,
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\FutureCommandSandboxValidator.cs:2921:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\FutureCommandSandboxValidator.cs:2973:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
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
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\ModRuntimeState.cs:307:                payloadBytes = new NativeArray<byte>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\ModRuntimeState.cs:372:                payloadBytes = new NativeArray<byte>(
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\ModRegistryEvents.cs:65:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\ModCommandDispatcher.cs:226:        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
- Native allocation or persistent lifetime | Assets\_Project\Scripts\ModdingAPI\ModCommandDispatcher.cs:227:        private const Allocator DataVaultExemptOwnerIndexAllocator = Allocator.Persistent;

## REVIEW_LOG_GUARD_REQUIRED (1)

- Runtime debug logging | Assets\_Project\Scripts\QA\QA_WatchdogBot.cs:5:// Hot path rules: no strings, no LINQ, no scene searches, no Debug.Log, no

