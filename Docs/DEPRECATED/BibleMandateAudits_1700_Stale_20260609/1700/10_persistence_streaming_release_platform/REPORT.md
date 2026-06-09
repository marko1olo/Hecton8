# Persistence, Streaming, Release, Platform, Modding, Testing

Status: STATIC BIBLE/MANDATE/CODEBASE AUDIT - RUNTIME PROOF NOT RUN
Date: 2026-06-02
Verdict: YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING

## Scope

This report compares the current root bible routes and selected mandate registry files against static codebase evidence. It does not prove Unity import health, Play Mode behavior, profiler cost, memory use, visual quality, or device performance.

## Bibles Checked

- OK persistence.md - 145 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK streaming.md - 124 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK release.md - 203 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK platform.md - 218 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK modding.md - 192 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK networking.md - 186 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK testing.md - 110 lines; GlobalQualityWeight, proof, acceptance, rejection.
- OK authoring.md - 148 lines; GlobalQualityWeight, proof, acceptance, rejection.

## Mandates Matched

- .agents-skills\DATA_Save_Persistence_Binary_Delta_Checksum.txt
- .agents-skills\NET_Logistics_Quantum.txt
- .agents-skills\NET_Logistics_Sync_BitPacking_Reconciliation.txt
- .agents-skills\PROJECT_LTS_Compatibility_Layer.txt
- .agents-skills\QA_Evidence_Text_Filter_Audit.txt
- .agents-skills\STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt
- .agents-skills\STRM_Async_Asset_Upload_Texture_Settings.txt
- .agents-skills\STRM_Async_Standard.txt
- .agents-skills\STRM_DirectStorage_Reality_Check.txt
- .agents-skills\STRM_ModuleDTO_LZ4_Dictionary.txt
- .agents-skills\STRM_Persistent_Object_Registry.txt
- .agents-skills\STRM_World_Streaming_Residency_Chunk_Management.txt
- .agents-skills\TOOL_Designer_Facades_CSV_Binary_Bridge.txt

## Code/Asset Roots

- OK Assets\_Project\Scripts\SaveSystem
- OK Assets\_Project\Scripts\Optimization
- OK Assets\_Project\Scripts\QA
- OK Assets\_Project\Scripts\Build
- OK Assets\_Project\Scripts\ModdingAPI
- OK Assets\_Project\Scripts\Networking
- OK Assets\_Project\Tests
- OK ProjectSettings
- OK Packages
- OK Tools

## Static Evidence Found

Total matching files: 631. Showing first 80. Full list: _scans/10_persistence_streaming_release_platform_evidence_files.txt.

- Assets\_Project\Scripts\Build\BuildInfo.cs
- Assets\_Project\Scripts\Build\BuildInfoHudPresenter.cs
- Assets\_Project\Scripts\ModdingAPI\Editor\ModApiSandboxTunerWindow.cs
- Assets\_Project\Scripts\ModdingAPI\Editor\ModKernelInspectorWindow.cs
- Assets\_Project\Scripts\ModdingAPI\FutureCommandSandboxValidator.cs
- Assets\_Project\Scripts\ModdingAPI\HectonAPI.cs
- Assets\_Project\Scripts\ModdingAPI\HectonEventBus.cs
- Assets\_Project\Scripts\ModdingAPI\HectonGameEvents.cs
- Assets\_Project\Scripts\ModdingAPI\IHectonMod.cs
- Assets\_Project\Scripts\ModdingAPI\IllegalContractException.cs
- Assets\_Project\Scripts\ModdingAPI\IModResourceProxy.cs
- Assets\_Project\Scripts\ModdingAPI\ModAssetManager.cs
- Assets\_Project\Scripts\ModdingAPI\ModCommandDispatcher.cs
- Assets\_Project\Scripts\ModdingAPI\ModEventContracts.cs
- Assets\_Project\Scripts\ModdingAPI\ModEventProjectionBridge.cs
- Assets\_Project\Scripts\ModdingAPI\ModLoader.cs
- Assets\_Project\Scripts\ModdingAPI\ModLocalizationBridge.cs
- Assets\_Project\Scripts\ModdingAPI\ModMenuModEntryView.cs
- Assets\_Project\Scripts\ModdingAPI\ModMenuSettingSliderView.cs
- Assets\_Project\Scripts\ModdingAPI\ModMenuSettingToggleView.cs
- Assets\_Project\Scripts\ModdingAPI\ModMenuUIController.cs
- Assets\_Project\Scripts\ModdingAPI\ModMetadata.cs
- Assets\_Project\Scripts\ModdingAPI\ModRegistryEvents.cs
- Assets\_Project\Scripts\ModdingAPI\ModRuntimeInfo.cs
- Assets\_Project\Scripts\ModdingAPI\ModRuntimeState.cs
- Assets\_Project\Scripts\ModdingAPI\ModSettingsRegistry.cs
- Assets\_Project\Scripts\ModdingAPI\ModSpatialContracts.cs
- Assets\_Project\Scripts\ModdingAPI\ModWorldPersistenceManager.cs
- Assets\_Project\Scripts\Networking\HectonNetworkManager.cs
- Assets\_Project\Scripts\Networking\HectonRollbackNetcodeRuntime.cs
- Assets\_Project\Scripts\Networking\RollbackNetcodeContracts.cs
- Assets\_Project\Scripts\Optimization\ARCHITECTURE.md
- Assets\_Project\Scripts\Optimization\AssetLifecycleGovernor.cs
- Assets\_Project\Scripts\Optimization\AssetLoadDispatcher.cs
- Assets\_Project\Scripts\Optimization\AssetRecord.cs
- Assets\_Project\Scripts\Optimization\CameraRTManager.cs
- Assets\_Project\Scripts\Optimization\CHANGELOG.md
- Assets\_Project\Scripts\Optimization\Editor\FormatOptimizationRecommendation.cs
- Assets\_Project\Scripts\Optimization\Editor\HeapSanitizerTunerWindow.cs
- Assets\_Project\Scripts\Optimization\Editor\Hecton8.Optimization.Editor.asmdef
- Assets\_Project\Scripts\Optimization\Editor\HectonTransparentOverdrawBuildGuard.cs
- Assets\_Project\Scripts\Optimization\Editor\RenderTextureFormatOptimizer.cs
- Assets\_Project\Scripts\Optimization\Editor\RenderTextureLifecycleWindow.cs
- Assets\_Project\Scripts\Optimization\Editor\RenderTextureOptimizationWindow.cs
- Assets\_Project\Scripts\Optimization\Editor\RenderTextureResolutionAnalyzer.cs
- Assets\_Project\Scripts\Optimization\Editor\ResolutionOptimizationRecommendation.cs
- Assets\_Project\Scripts\Optimization\Editor\VRAMDiagnosticReport.cs
- Assets\_Project\Scripts\Optimization\Editor\VRAMIntegratorVerifier1617.cs
- Assets\_Project\Scripts\Optimization\Editor\VRAMStreamingStaticAssertions1617.cs
- Assets\_Project\Scripts\Optimization\Editor\VRAMTextureFootprintScanner1617.cs
- Assets\_Project\Scripts\Optimization\Editor\VRAMValidator.cs
- Assets\_Project\Scripts\Optimization\HardwareProfiler.cs
- Assets\_Project\Scripts\Optimization\IMPLEMENTATION_SUMMARY.md
- Assets\_Project\Scripts\Optimization\INTEGRATION_VERIFICATION.md
- Assets\_Project\Scripts\Optimization\PostFXRTManager.cs
- Assets\_Project\Scripts\Optimization\PreInitAssetIdMap.cs
- Assets\_Project\Scripts\Optimization\README.md
- Assets\_Project\Scripts\Optimization\RenderTextureLifecycleTracker.cs
- Assets\_Project\Scripts\Optimization\RenderTexturePool.cs
- Assets\_Project\Scripts\Optimization\UIRTManager.cs
- Assets\_Project\Scripts\Optimization\VisorRTManager.cs
- Assets\_Project\Scripts\Optimization\VRAMBudgetThresholds.cs
- Assets\_Project\Scripts\Optimization\VRAMEnforcer.cs
- Assets\_Project\Scripts\Optimization\VRAMMonitor.cs
- Assets\_Project\Scripts\Optimization\VRAMPressureMonitor.cs
- Assets\_Project\Scripts\QA\Editor\Hecton8.QA.Editor.asmdef
- Assets\_Project\Scripts\QA\Editor\QAEnduranceBatchRunner.cs
- Assets\_Project\Scripts\QA\Editor\QAWatchdogBatchRunner1524.cs
- Assets\_Project\Scripts\QA\Editor\QAWatchdogGcAllocationFuzzer1424.cs
- Assets\_Project\Scripts\QA\Editor\QAWatchdogGcAllocationFuzzer1524Menu.cs
- Assets\_Project\Scripts\QA\Headless\Editor\HeadlessSimulationBatchRunner.cs
- Assets\_Project\Scripts\QA\Headless\Editor\HeadlessStressFractureBatchRunner.cs
- Assets\_Project\Scripts\QA\Headless\Editor\Hecton8.QA.Headless.Editor.asmdef
- Assets\_Project\Scripts\QA\Headless\Editor\JacobiStressFuzzer\JacobiStressFuzzerWindow.cs
- Assets\_Project\Scripts\QA\Headless\Editor\Shinobu38QaWatchdogCommanderWindow.cs
- Assets\_Project\Scripts\QA\Headless\HeadlessSimulationRunner.cs
- Assets\_Project\Scripts\QA\Headless\HeadlessStressFractureBot.cs
- Assets\_Project\Scripts\QA\Headless\Hecton8.QA.Headless.asmdef
- Assets\_Project\Scripts\QA\Headless\JacobiStressFuzzer\PowerGridJacobiStressFuzzer.cs
- Assets\_Project\Scripts\QA\Headless\Shinobu38QaWatchdogRuntime.cs

## Static Risk Suspects

These are raw static suspects, not confirmed defects. Current manual or line-level review files are the authority for classification where present; editor/tool suspects remain legal only if they cannot execute in gameplay/player hot paths.

Runtime suspects:
Total runtime suspects: 126. Showing first 80. Full list: _scans/10_persistence_streaming_release_platform_runtime_risks.txt.

- Assets\_Project\Scripts\SaveSystem\EntityDeltaCompressionArchitecture.cs:1349:                NativeArray<byte> payload = new NativeArray<byte>(
- Assets\_Project\Scripts\SaveSystem\H8BinaryWorldPager.cs:827:            Hecton8.Core.H8Debug.LogWarning("H8BinaryWorldPager disabled page IO after initialization fault. reason=" + reason);
- Assets\_Project\Scripts\SaveSystem\H8BinaryWorldPager.cs:3174:                array = new NativeArray<T>(length, Allocator.Persistent, options);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore_SHINOBU357.cs:204:                    payloadOwner = new NativeArray<byte>(payloadBytes, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore_SHINOBU357.cs:215:                    corruptWalOwner = new NativeArray<byte>(payloadBytes, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore_SHINOBU357.cs:226:                    stateOwner = new NativeArray<WalFuzzStateDTO>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore_SHINOBU357.cs:237:                    telemetryOwner = new NativeArray<WalFuzzTelemetryEntry>(Shinobu357TelemetryCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore_SHINOBU357.cs:242:                legacyTelemetry = new NativeArray<WalFuzzerTelemetryEntry>(TelemetryCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore_SHINOBU357.cs:439:                    hashScratchOwner = new NativeArray<byte>(backupByteCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore_SHINOBU357.cs:501:                    fileHandleStatusOwner = new NativeArray<WalFuzzFileHandleStatusDTO>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:178:            NativeArray<WalFuzzerProfileDTO> profiles = new NativeArray<WalFuzzerProfileDTO>(4, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:221:                payload = new NativeArray<byte>(payloadBytes, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:222:                recovered = new NativeArray<byte>(payloadBytes, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:223:                jobResult = new NativeArray<WalFuzzerResultDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:224:                telemetry = new NativeArray<WalFuzzerTelemetryEntry>(TelemetryCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:340:            NativeArray<byte> bytes = new NativeArray<byte>((int)info.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:719:                buffers.CurrentTree = new NativeArray<MerkleNodeDTO>(SaveStateMerkleTree.RequiredNodeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:720:                buffers.PreviousTree = new NativeArray<MerkleNodeDTO>(SaveStateMerkleTree.RequiredNodeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:721:                buffers.LeafDescriptors = new NativeArray<StateLeafDescriptor>(SaveStateMerkleTree.LeafCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:722:                buffers.DeltaRecords = new NativeArray<StateDeltaRecordDTO>(SaveStateMerkleTree.LeafCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:723:                buffers.DeltaBytes = new NativeArray<byte>(deltaCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:724:                buffers.PrunedDeltaBytes = new NativeArray<byte>(deltaCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:725:                buffers.CompressedBytes = new NativeArray<byte>(compressedCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:726:                buffers.Lz4BlockHeaders = new NativeArray<Lz4SubBlockHeader>(blockHeaderCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:727:                buffers.TelemetryRing = new NativeArray<SaveMerkleTelemetryEntry>(SaveStateMerkleTree.TelemetryRingFrames, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:728:                buffers.Counters = new NativeArray<int>(MerkleCounterCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:729:                buffers.Lz4HashTable = new NativeArray<int>(SaveStateMerkleTree.HashTableSlots, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:730:                replayedDeltaBytes = new NativeArray<byte>(deltaCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:731:                replayCounters = new NativeArray<int>(MerkleCounterCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:1110:                payload = new NativeArray<byte>(payloadBytes, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\SaveSystem\WalIntegrityFuzzerCore.cs:1111:                readback = new NativeArray<byte>(payloadBytes, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\Optimization\CameraRTManager.cs:222:            Hecton8.Core.H8Debug.LogWarning(_reportBuilder.ToString(), this);
- Assets\_Project\Scripts\Optimization\AssetLifecycleGovernor.cs:243:                Hecton8.Core.H8Debug.LogError("[AssetLifecycleGovernor] AssetHandleMapEntryDTO must remain 64 bytes.", this);
- Assets\_Project\Scripts\Optimization\AssetLifecycleGovernor.cs:245:                Hecton8.Core.H8Debug.LogError("[AssetLifecycleGovernor] AssetTrackerDTO must remain 64 bytes.", this);
- Assets\_Project\Scripts\Optimization\AssetLifecycleGovernor.cs:891:                Hecton8.Core.H8Debug.LogError("[AssetLifecycleGovernor] Double release detected.", this);
- Assets\_Project\Scripts\Optimization\AssetLifecycleGovernor.cs:993:            Hecton8.Core.H8Debug.LogError("[AssetLifecycleGovernor] Asset load failed.", this);
- Assets\_Project\Scripts\Optimization\AssetLifecycleGovernor.cs:3712:                payload = new NativeArray<byte>(byteCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\Optimization\AssetLifecycleGovernor.cs:5248:            Hecton8.Core.H8Debug.LogError("[AssetLifecycleGovernor] Asset key collision.");
- Assets\_Project\Scripts\Optimization\PostFXRTManager.cs:220:            Hecton8.Core.H8Debug.LogWarning(_reportBuilder.ToString(), this);
- Assets\_Project\Scripts\Optimization\RenderTexturePool.cs:205:                Hecton8.Core.H8Debug.LogWarning("[RTPool] Return called with null RenderTexture");
- Assets\_Project\Scripts\Optimization\VisorRTManager.cs:213:            Hecton8.Core.H8Debug.LogWarning("[VisorRTManager] BUDGET EXCEEDED", this);
- Assets\_Project\Scripts\Optimization\UIRTManager.cs:213:            Hecton8.Core.H8Debug.LogWarning("[UIRTManager] BUDGET EXCEEDED", this);
- Assets\_Project\Scripts\Optimization\RenderTextureLifecycleTracker.cs:141:                Hecton8.Core.H8Debug.LogError("[LifecycleTracker] RegisterAllocation called with null RenderTexture");
- Assets\_Project\Scripts\Optimization\RenderTextureLifecycleTracker.cs:149:                Hecton8.Core.H8Debug.LogError("[LifecycleTracker] RegisterAllocation called with null owner");
- Assets\_Project\Scripts\Optimization\RenderTextureLifecycleTracker.cs:159:                Hecton8.Core.H8Debug.LogWarning(
- Assets\_Project\Scripts\Optimization\RenderTextureLifecycleTracker.cs:394:                    Hecton8.Core.H8Debug.LogError(
- Assets\_Project\Scripts\Optimization\VRAMMonitor.cs:573:            Hecton8.Core.H8Debug.LogWarning(_reportBuilder.ToString(), this);
- Assets\_Project\Scripts\Optimization\PreInitAssetIdMap.cs:66:            _guidRecords = new NativeArray<AssetGuidIdRecord>(
- Assets\_Project\Scripts\Optimization\PreInitAssetIdMap.cs:68:                Allocator.Persistent,
- Assets\_Project\Scripts\QA\Headless\HeadlessStressFractureBot.cs:523:            Hecton8.Core.H8Debug.LogWarning(FormatStaticHPhiLog(
- Assets\_Project\Scripts\QA\Headless\HeadlessStressFractureBot.cs:935:            Hecton8.Core.H8Debug.LogError(status);
- Assets\_Project\Scripts\QA\QA_WatchdogBot.cs:5:// Hot path rules: no strings, no LINQ, no scene searches, no Debug.Log, no
- Assets\_Project\Scripts\QA\Headless\JacobiStressFuzzer\PowerGridJacobiStressFuzzer.cs:334:                scratch = new NativeArray<byte>(PowerJacobiStressFuzzerConstants.CsvScratchBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Scripts\ModdingAPI\ModWorldPersistenceManager.cs:196:                Hecton8.Core.H8Debug.LogWarning($"[ModWorldPersistenceManager] Prefab '{assetName}' for mod '{modId}' could not be resolved.");
- Assets\_Project\Scripts\ModdingAPI\ModWorldPersistenceManager.cs:205:                Hecton8.Core.H8Debug.LogWarning("[ModWorldPersistenceManager] GlobalRegistry.ObjectPoolService is unavailable. Persistent mod spawn was rejected.");
- Assets\_Project\Scripts\ModdingAPI\ModWorldPersistenceManager.cs:309:                Hecton8.Core.H8Debug.LogWarning($"[ModWorldPersistenceManager] Failed to parse mod world payload: {exception.Message}");
- Assets\_Project\Scripts\ModdingAPI\ModWorldPersistenceManager.cs:387:                    Hecton8.Core.H8Debug.LogWarning(
- Assets\_Project\Scripts\ModdingAPI\ModSettingsRegistry.cs:305:                Hecton8.Core.H8Debug.LogWarning("[ModSettingsRegistry] Refused to register a setting with an empty modId or settingName.");
- Assets\_Project\Scripts\ModdingAPI\ModSettingsRegistry.cs:360:                Hecton8.Core.H8Debug.LogWarning($"[ModSettingsRegistry] Toggle callback failed for mod '{modId}': {exception}");
- Assets\_Project\Scripts\ModdingAPI\ModSettingsRegistry.cs:378:                Hecton8.Core.H8Debug.LogWarning($"[ModSettingsRegistry] Slider callback failed for mod '{modId}': {exception}");
- Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:138:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] LoadMods failed: ", ex.Message));
- Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:231:            Hecton8.Core.H8Debug.LogWarning("[ModLoader] WARNING: External managed code mods require a Mono scripting backend. IL2CPP builds cannot load runtime assemblies dynamically.");
- Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:294:                        Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Manifest discovery capped at ", MaxDiscoveredManifestCountLabel, " packages under '", modsRoot, "'."));
- Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:303:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Manifest discovery skipped inaccessible path under '", modsRoot, "': ", exception.Message));
- Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:307:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Manifest discovery failed under '", modsRoot, "': ", exception.Message));
- Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:311:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Manifest discovery aborted under '", modsRoot, "': ", exception.Message));
- Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:329:                    Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Skipped manifest '", manifestPath, "': invalid Id. ", modIdError));
- Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:392:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Failed to read manifest '", manifestPath, "': ", ex.Message));
- Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:469:                    Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Skipped manifest '", manifestPath, "': manifest file is missing or empty."));
- Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:475:                    Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Skipped manifest '", manifestPath, "': manifest exceeds ", MaxManifestBytesLabel, " byte cap."));
- Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:481:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Failed to inspect manifest '", manifestPath, "': ", exception.Message));
- Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:486:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Rejected invalid manifest path '", manifestPath, "': ", exception.Message));
- Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:894:                        Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Top-level ", fileKind, " discovery capped at ", maxCountLabel, " files under '", directory, "'."));
- Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:904:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Top-level ", fileKind, " discovery skipped inaccessible path under '", directory, "': ", exception.Message));
- Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:910:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Top-level ", fileKind, " discovery failed under '", directory, "': ", exception.Message));
- Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:916:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Top-level ", fileKind, " discovery aborted under '", directory, "': ", exception.Message));
- Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:1004:                    Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Disabled mod '", candidate.Metadata.Id, "': dependency cycle or unresolved ordering deadlock."));
- Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:1027:                Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Disabled mod '", candidate.Metadata.Id, "': missing dependency '", dependencyId, "'."));
- Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:1168:            Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Disabled mod '", candidate.Metadata.Id, "': ", reason));
- Assets\_Project\Scripts\ModdingAPI\ModLoader.cs:1212:                        Hecton8.Core.H8Debug.LogWarning(string.Concat("[ModLoader] Disabled mod '", modId, "' threw during isolation unload: ", unloadException));

Editor/tool/static suspects:
Total editor/tool/static suspects: 1345. Showing first 80. Full list: _scans/10_persistence_streaming_release_platform_editor_tool_risks.txt.

- Assets\_Project\Scripts\SaveSystem\Editor\EntitySaveTunerWindow.cs:108:        private void Update()
- Assets\_Project\Scripts\SaveSystem\Editor\EntitySaveTunerWindow.cs:332:                _summary.text = text;
- Assets\_Project\Scripts\SaveSystem\Editor\OOP_VoxelPagingFuzzer1312.cs:23:            UnityEngine.Debug.Log($"[OOP 1312] Voxel paging fuzzer report written: {report}");
- Assets\_Project\Scripts\SaveSystem\Editor\WalSaveFuzzerWindow_SHINOBU357.cs:109:        private void Update()
- Assets\_Project\Scripts\SaveSystem\Editor\WalSaveFuzzerWindow_SHINOBU357.cs:268:                _summary.text = text;
- Assets\_Project\Scripts\SaveSystem\Editor\VoxelSaveTunerWindow.cs:81:        private void Update()
- Assets\_Project\Scripts\SaveSystem\Editor\VoxelSaveTunerWindow.cs:115:                    _summary.text = "GlobalDataVault is not registered.";
- Assets\_Project\Scripts\SaveSystem\Editor\VoxelSaveTunerWindow.cs:205:                _summary.text = "Voxel delta WAL telemetry ring is empty.";
- Assets\_Project\Scripts\SaveSystem\Editor\VoxelSaveTunerWindow.cs:215:            _summary.text = "Last sector: " + entry.SectorHash.ToString("X16") +
- Assets\_Project\Scripts\Optimization\Editor\RenderTextureFormatOptimizer.cs:70:                Debug.LogWarning("[FormatOptimizer] RenderTextureLifecycleTracker not available. Enter Play Mode first.");
- Assets\_Project\Scripts\Optimization\Editor\VRAMValidator.cs:45:            Debug.Log("[VRAMValidator] Texture budget gate passed.");
- Assets\_Project\Scripts\Optimization\Editor\VRAMValidator.cs:52:            Debug.Log(
- Assets\_Project\Scripts\Optimization\Editor\RenderTextureOptimizationWindow.cs:166:                Debug.Log($"[FormatOptimizer] Applied format optimization to {rec.RenderTexture.name}: {rec.CurrentFormat} → {rec.RecommendedFormat}");
- Assets\_Project\Scripts\Optimization\Editor\RenderTextureOptimizationWindow.cs:254:                Debug.Log($"[ResolutionAnalyzer] Applied resolution optimization to {rec.RenderTexture.name}: {rec.CurrentWidth}x{rec.CurrentHeight} → {rec.RecommendedWidth}x{rec.RecommendedHeight}");
- Assets\_Project\Scripts\Optimization\Editor\VRAMDiagnosticReport.cs:66:            Debug.Log($"[VRAMDiagnostic] Report generated: {filepath}");
- Assets\_Project\Scripts\Optimization\Editor\HectonTransparentOverdrawBuildGuard.cs:50:            Debug.Log(
- Assets\_Project\Scripts\Optimization\Editor\VRAMStreamingStaticAssertions1617.cs:19:            Debug.Log("[VRAMStreamingStaticAssertions1617] Static VRAM streaming assertions passed.");
- Assets\_Project\Scripts\Optimization\Editor\RenderTextureResolutionAnalyzer.cs:58:                Debug.LogWarning("[ResolutionAnalyzer] RenderTextureLifecycleTracker not available. Enter Play Mode first.");
- Assets\_Project\Scripts\Optimization\Editor\RenderTextureResolutionAnalyzer.cs:132:                Debug.LogWarning("[ResolutionAnalyzer] Cannot capture screenshot: RenderTexture is null");
- Assets\_Project\Scripts\Optimization\Editor\RenderTextureResolutionAnalyzer.cs:177:                    Debug.Log($"[ResolutionAnalyzer] Screenshot saved: {fullPath}");
- Assets\_Project\Scripts\Optimization\Editor\VRAMIntegratorVerifier1617.cs:45:            Debug.Log("Agent 1617 APEX integrator verification passed.");
- Assets\_Project\Scripts\Optimization\Editor\HeapSanitizerTunerWindow.cs:218:                _activeLabel.text = "Active: " + active.ToString();
- Assets\_Project\Scripts\Optimization\Editor\HeapSanitizerTunerWindow.cs:224:                _hitsLabel.text = "Hits: " + hits.ToString();
- Assets\_Project\Scripts\Optimization\Editor\HeapSanitizerTunerWindow.cs:230:                _missesLabel.text = "Misses: " + misses.ToString();
- Assets\_Project\Scripts\Optimization\Editor\HeapSanitizerTunerWindow.cs:236:                _releasedLabel.text = "Released: " + released.ToString();
- Assets\_Project\Scripts\Optimization\Editor\HeapSanitizerTunerWindow.cs:291:                    _leakBanner.text = "LEAK SUSPECT  asset=0x" + hash.ToString("X8") +
- Assets\_Project\Scripts\Optimization\Editor\HeapSanitizerTunerWindow.cs:323:                    _trackerRows[i].text = "0x" + tracker.AssetHash.ToString("X8") +
- Assets\_Project\Scripts\Optimization\Editor\HeapSanitizerTunerWindow.cs:346:            _statusLabel.text = registered
- Assets\_Project\Scripts\Optimization\Editor\VRAMTextureFootprintScanner1617.cs:27:            Debug.Log(
- Assets\_Project\Scripts\QA\Headless\Editor\JacobiStressFuzzer\JacobiStressFuzzerWindow.cs:81:            _stateLabel.text = "RUNNING";
- Assets\_Project\Scripts\QA\Headless\Editor\JacobiStressFuzzer\JacobiStressFuzzerWindow.cs:82:            _flagsLabel.text = "scheduled background Burst chain";
- Assets\_Project\Scripts\QA\Headless\Editor\JacobiStressFuzzer\JacobiStressFuzzerWindow.cs:116:                run.Complete(out result);
- Assets\_Project\Scripts\QA\Headless\Editor\JacobiStressFuzzer\JacobiStressFuzzerWindow.cs:123:                Debug.LogException(exception);
- Assets\_Project\Scripts\QA\Headless\Editor\JacobiStressFuzzer\JacobiStressFuzzerWindow.cs:168:            _stateLabel.text = passed ? "PASS" : "FAIL";
- Assets\_Project\Scripts\QA\Headless\Editor\JacobiStressFuzzer\JacobiStressFuzzerWindow.cs:170:            _flagsLabel.text = "failure flags: " + result.FailureFlags + "  node: " + result.FirstFailureNodeHash;
- Assets\_Project\Scripts\QA\Headless\Editor\JacobiStressFuzzer\JacobiStressFuzzerWindow.cs:171:            _residualLabel.text = "final residual: " + result.FinalResidual.ToString("0.000000") +
- Assets\_Project\Scripts\QA\Headless\Editor\JacobiStressFuzzer\JacobiStressFuzzerWindow.cs:173:            _perfLabel.text = "solver/chain us: " + result.AverageSolverMicroseconds.ToString("0.000") +
- Assets\_Project\Scripts\QA\Headless\Editor\JacobiStressFuzzer\JacobiStressFuzzerWindow.cs:283:            Debug.Log("OOP Fuzz scanner wrote " + RunScan());
- Assets\_Project\Scripts\ModdingAPI\Editor\ModKernelInspectorWindow.cs:102:            _statusLabel.text =
- Assets\_Project\Scripts\ModdingAPI\Editor\ModKernelInspectorWindow.cs:106:            _shedLabel.text = $"survival {survival} haptic {haptic} subtitle {subtitle} shed {shedTotal} rejected {rejected}";
- Assets\_Project\Tests\Editor\BootstrapShaderWarmupEditTests.cs:187:            Assert.That(source, Does.Not.Contain("StartCoroutine"));
- Assets\_Project\Tests\Editor\AsynchronousTelemetryExporterEditTests.cs:43:            NativeArray<byte> source = new NativeArray<byte>(32, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Tests\Editor\AsynchronousTelemetryExporterEditTests.cs:44:            NativeArray<byte> destination = new NativeArray<byte>(96, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Tests\Editor\BiomeTransitionPolisher1628EditTests.cs:98:            Assert.That(publishBlock, Does.Not.Contain(".material"));
- Assets\_Project\Tests\Editor\BiomeTransitionPolisher1628EditTests.cs:156:            Assert.That(fastTick, Does.Not.Contain(".Complete("));
- Assets\_Project\Tests\Editor\BiomeTransitionPolisher1628EditTests.cs:277:            Assert.That(source, Does.Not.Contain("GameObject.Find"));
- Assets\_Project\Tests\Editor\BiomeTransitionPolisher1628EditTests.cs:279:            Assert.That(source, Does.Not.Contain("Camera.main"));
- Assets\_Project\Tests\Editor\ArenaAllocatorSentinel1414EditTests.cs:366:            void* pointer = UnsafeUtility.Malloc(64, 16, Allocator.Persistent);
- Assets\_Project\Tests\Editor\ArenaAllocatorSentinel1414EditTests.cs:389:                    UnsafeUtility.Free(pointer, Allocator.Persistent);
- Assets\_Project\Tests\Editor\ArenaAllocatorSentinel1414EditTests.cs:460:            Assert.IsFalse(block.Contains("GetComponent<"));
- Assets\_Project\Tests\Editor\ArenaAllocatorSentinel1414EditTests.cs:471:            Assert.IsFalse(block.Contains(".Select("));
- Assets\_Project\Tests\Editor\ArenaAllocatorSentinel1414EditTests.cs:472:            Assert.IsFalse(block.Contains(".Where("));
- Assets\_Project\Tests\Editor\ArenaAllocatorSentinel1414EditTests.cs:474:            Assert.IsFalse(block.Contains(".ToList("));
- Assets\_Project\Tests\Editor\ContinuousPhysicsQuality1409EditTests.cs:92:            NativeArray<BallastTankDTO> tanks = new NativeArray<BallastTankDTO>(1, Allocator.TempJob);
- Assets\_Project\Tests\Editor\ContinuousPhysicsQuality1409EditTests.cs:93:            NativeArray<SubmarineBallastFluidSampleDTO> samples = new NativeArray<SubmarineBallastFluidSampleDTO>(1, Allocator.TempJob);
- Assets\_Project\Tests\Editor\ContinuousPhysicsQuality1409EditTests.cs:94:            NativeArray<SubmarineBallastForcePacketDTO> packets = new NativeArray<SubmarineBallastForcePacketDTO>(1, Allocator.TempJob);
- Assets\_Project\Tests\Editor\ContinuousPhysicsQuality1409EditTests.cs:95:            NativeArray<SubmarineBallastTelemetryEntry> telemetry = new NativeArray<SubmarineBallastTelemetryEntry>(SubmarineBallastConstants.TelemetryCapacity, Allocator.TempJob);
- Assets\_Project\Tests\Editor\DispatcherPhaseAlignment1410EditTests.cs:16:            NativeArray<int> buffer = new NativeArray<int>(1, Allocator.Temp);
- Assets\_Project\Tests\Editor\ColliderOptimization1609EditTests.cs:26:            StringAssert.Contains("MeshColliderFatalTriangleLimit = 500", source);
- Assets\_Project\Tests\Editor\ColliderOptimization1609EditTests.cs:36:            StringAssert.Contains("GetComponentsInChildren(true, s_MeshColliderScratch)", source);
- Assets\_Project\Tests\Editor\ColliderOptimization1609EditTests.cs:39:            Assert.IsFalse(source.Contains("GetComponentsInChildren<MeshCollider>(true)", StringComparison.Ordinal));
- Assets\_Project\Tests\Editor\ColliderOptimization1609EditTests.cs:56:            StringAssert.Contains("MeshColliderCookingOptions", source);
- Assets\_Project\Tests\Editor\ColliderOptimization1609EditTests.cs:176:            StringAssert.Contains("ExpandMeshColliderRootBounds(meshCollider, rootTransform, ref fallbackMin, ref fallbackMax, ref hasFallbackBounds)", source);
- Assets\_Project\Tests\Editor\ColliderOptimization1609EditTests.cs:261:                    MeshCollider collider = gameObject.AddComponent<MeshCollider>();
- Assets\_Project\Tests\Editor\ColliderOptimization1609EditTests.cs:282:                Assert.IsTrue(ColliderOptimizationEngine1609.ValidatePrefabMeshColliderBudget(prefab, out string failure), failure);
- Assets\_Project\Tests\Editor\ColliderOptimization1609EditTests.cs:426:            mesh.RecalculateNormals();
- Assets\_Project\Tests\Editor\ColliderOptimizer1716EditTests.cs:22:            StringAssert.Contains("MeshColliderFatalTriangleLimit = 500", source);
- Assets\_Project\Tests\Editor\ColliderOptimizer1716EditTests.cs:27:            StringAssert.Contains("IsPrimaryVisualMeshCollider", source);
- Assets\_Project\Tests\Editor\ColliderOptimizer1716EditTests.cs:66:            StringAssert.Contains("job.Schedule(directionCount, 4).Complete()", source);
- Assets\_Project\Tests\Editor\Bakers\ProceduralTextureBaker1605EditTests.cs:554:            NativeArray<float2> uvs = new NativeArray<float2>(4, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
- Assets\_Project\Tests\Editor\AcousticPortalPropagationTests.cs:41:            NativeArray<AcousticPortalNode> nodes = new NativeArray<AcousticPortalNode>(3, Allocator.TempJob);
- Assets\_Project\Tests\Editor\AcousticPortalPropagationTests.cs:42:            NativeArray<AcousticPortalEdge> edges = new NativeArray<AcousticPortalEdge>(4, Allocator.TempJob);
- Assets\_Project\Tests\Editor\AcousticPortalPropagationTests.cs:43:            NativeArray<AcousticPathResult> results = new NativeArray<AcousticPathResult>(1, Allocator.TempJob);
- Assets\_Project\Tests\Editor\AcousticPortalPropagationTests.cs:44:            NativeArray<float> costs = new NativeArray<float>(3, Allocator.TempJob);
- Assets\_Project\Tests\Editor\AcousticPortalPropagationTests.cs:45:            NativeArray<int> cameFrom = new NativeArray<int>(3, Allocator.TempJob);
- Assets\_Project\Tests\Editor\AcousticPortalPropagationTests.cs:46:            NativeArray<byte> states = new NativeArray<byte>(3, Allocator.TempJob);
- Assets\_Project\Tests\Editor\DropPodStaticAudit1602EditTests.cs:40:            Assert.IsFalse(body.Contains(".text =", StringComparison.Ordinal));
- Assets\_Project\Tests\Editor\DropPodStaticAudit1602EditTests.cs:101:            Assert.IsFalse(body.Contains(".Select(", StringComparison.Ordinal));
- Assets\_Project\Tests\Editor\DropPodStaticAudit1602EditTests.cs:102:            Assert.IsFalse(body.Contains(".Where(", StringComparison.Ordinal));
- Assets\_Project\Tests\Editor\DropPodStaticAudit1602EditTests.cs:104:            Assert.IsFalse(body.Contains(".ToList(", StringComparison.Ordinal));

## Exists / Missing / Required Proof

- Exists: bible routes exist and static implementation evidence was found.
- Partial: all 126 runtime static suspect lines have method-level classification in `LINE_LEVEL_CLASSIFICATION.md`; runtime/profiler/player proof is still missing.
- Editor/tool: static suspects exist but may be legal if editor-only or cold-path.
- Required proof: Save/load binary proof, Addressables/residency proof, build/import/player proof, platform device proof, mod envelope proof, testing evidence class proof.

## Next Audit Action

Use `LINE_LEVEL_CLASSIFICATION.md`, then collect save/load, Addressables, streaming, mod-envelope, legacy quarantine, prewarm, GC/memory, player-build, and device proof before any green/release claim.
