#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Dev
{
    public static class PersistenceUxSmokeTester
    {
        private const string ArtifactRelativePath = "CodexArtifacts/persistence-ux-smoke.json";
        private const string InventoryFullWriteFileRelativePath = "CodexArtifacts/persistence-ux-inventory-full-write.bin";
        private const int SectorSizeBytes = 16 * 1024;
        private const int InventorySlotStrideBytes = 16;
        private const int InventorySlotCount = 64;

        [MenuItem("Hecton8/Dev/Run Persistence UX Smoke")]
        private static void RunMenuSmokeTest()
        {
            RunSmokeAndWriteArtifact();
        }

        public static void RunBatchModeSmokeTest()
        {
            bool pass = RunSmokeAndWriteArtifact();
            if (Application.isBatchMode)
                EditorApplication.Exit(pass ? 0 : 1);
        }

        private static bool RunSmokeAndWriteArtifact()
        {
            string saveManager = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string thumbnailSystem = ReadProjectFile("Assets/_Project/Scripts/SaveThumbnailSystem.cs");
            string captureFeature = ReadProjectFile("Assets/_Project/Scripts/SaveThumbnailCaptureFeature.cs");
            string loadingScreen = ReadProjectFile("Assets/_Project/Scripts/UI/LoadingScreenController.cs");
            string gameBootstrapper = ReadProjectFile("Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs");
            string suitHud = ReadProjectFile("Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs");
            string dataRecPulseShader = ReadProjectFile("Assets/_Project/Shaders/UI/Hecton_DataRecPulse.shader");
            string playerInventory = ReadProjectFile("Assets/_Project/Scripts/PlayerInventory.cs");
            string saveData = ReadProjectFile("Assets/_Project/Scripts/SaveData.cs");
            string saveBinaryPayloadCodec = ReadProjectFile("Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs");
            string saveDataMigration = ReadProjectFile("Assets/_Project/Scripts/SaveDataMigration.cs");
            string saveBinaryStorage = ReadProjectFile("Assets/_Project/Scripts/SaveBinaryStorage.cs");
            string constructionManager = ReadProjectFile("Assets/_Project/Scripts/ConstructionManager.cs");
            string baseModule = ReadProjectFile("Assets/_Project/Scripts/BaseModule.cs");
            string baseLogisticsNetwork = ReadProjectFile("Assets/_Project/Scripts/Construction/BaseLogisticsNetwork.cs");
            string deepDrill = ReadProjectFile("Assets/_Project/Scripts/Construction/DeepDrillModule.cs");
            string logisticsSorter = ReadProjectFile("Assets/_Project/Scripts/Construction/LogisticsSorterModule.cs");
            string logisticsPipe = ReadProjectFile("Assets/_Project/Scripts/Construction/LogisticsPipeNode.cs");
            string cultivationManager = ReadProjectFile("Assets/_Project/Scripts/Construction/CultivationManager.cs");
            string storageCrate = ReadProjectFile("Assets/_Project/Scripts/Gameplay/StorageCrate.cs");
            string persistentWorldRegistry = ReadProjectFile("Assets/_Project/Scripts/World/PersistentWorldRegistry.cs");
            string globalRegistryContracts = ReadProjectFile("Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs");
            string harvestableOutcrop = ReadProjectFile("Assets/_Project/Scripts/Gameplay/HarvestableOutcrop.cs");
            string unsafeMemoryCopyGuard = ReadProjectFile("Assets/_Project/Scripts/Core/UnsafeMemoryCopyGuard.cs");
            string saveEvents = ReadProjectFile("Assets/_Project/Scripts/SaveEvents.cs");
            string interactionEvents = ReadProjectFile("Assets/_Project/Scripts/Interaction/InteractionEvents.cs");
            string hectonEventBus = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs");
            string playModeSentinel = ReadProjectFile("Assets/_Project/Scripts/Editor/H8PlayModeSentinel.cs");
            string sceneRuntimeService = ReadProjectFile("Assets/_Project/Scripts/Core/SceneRuntimeService.cs");
            string mainMenuController = ReadProjectFile("Assets/_Project/Scripts/MainMenuController.cs");
            string pauseMenuController = ReadProjectFile("Assets/_Project/Scripts/UI/PauseMenuController.cs");
            string hudSaveNotificationLink = ReadProjectFile("Assets/_Project/Scripts/UI/HUDSaveNotificationLink.cs");
            string saveSlotHoverPreview = ReadProjectFile("Assets/_Project/Scripts/UI/SaveSlotHoverPreview.cs");
            string saveSlotMaintenanceRecord = ReadProjectFile("Assets/_Project/Scripts/SaveSlotMaintenanceRecord.cs");
            string saveStation = ReadProjectFile("Assets/_Project/Scripts/Interaction/SaveStation.cs");
            string interactableContract = ReadProjectFile("Assets/_Project/Scripts/Interaction/IInteractable.cs");
            string playerInteraction = ReadProjectFile("Assets/_Project/Scripts/Interaction/PlayerInteraction.cs");
            string fabricator = ReadProjectFile("Assets/_Project/Scripts/Fabricator.cs");
            string repairDroneHub = ReadProjectFile("Assets/_Project/Scripts/Construction/RepairDroneHub.cs");
            string droneFleetManager = ReadProjectFile("Assets/_Project/Scripts/Construction/DroneFleetManager.cs");
            string maintenanceStationModule = ReadProjectFile("Assets/_Project/Scripts/Construction/MaintenanceStationModule.cs");
            string threadSafeCommandQueue = ReadProjectFile("Assets/_Project/Scripts/Core/ThreadSafeCommandQueue.cs");
            string faunaGeneticsManager = ReadProjectFile("Assets/_Project/Scripts/Ecosystem/FaunaGeneticsManager.cs");
            string emergencyServiceRelay = ReadProjectFile("Assets/_Project/Scripts/World/EmergencyServiceRelay.cs");
            string saveSidecarStorage = ReadProjectFile("Assets/_Project/Scripts/SaveSidecarStorage.cs");
            string saveSlotThumbnail = ReadProjectFile("Assets/_Project/Scripts/UI/SaveSlotThumbnail.cs");
            string saveThumbnailCapture = ReadProjectFile("Assets/_Project/Scripts/UI/SaveThumbnailCapture.cs");
            string englishLocalization = ReadProjectFile("Assets/_Project/Scripts/English.json");
            string locKeysGenerated = ReadProjectFile("Assets/_Project/Scripts/LocKeys.Generated.cs");
            string h8Hashes = ReadProjectFile("Assets/_Project/Scripts/Core/Generated/H8Hashes.cs");
            string modWorldPersistenceManager = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModWorldPersistenceManager.cs");
            string modRuntimeState = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs");
            string modAssetManager = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModAssetManager.cs");
            string modLoader = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModLoader.cs");
            string modRegistryEvents = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModRegistryEvents.cs");
            string modResourceProxy = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/IModResourceProxy.cs");
            string modSettingsRegistry = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModSettingsRegistry.cs");
            string modMenuController = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModMenuUIController.cs");
            string recyclingRegistry = ReadProjectFile("Assets/_Project/Scripts/Economy/RecyclingRegistry.cs");
            string scrapManager = ReadProjectFile("Assets/_Project/Scripts/Economy/ScrapManager.cs");
            string resourceRecycler = ReadProjectFile("Assets/_Project/Scripts/Economy/ResourceRecyclerModule.cs");
            string itemCatalog = ReadProjectFile("Assets/_Project/Scripts/ItemCatalog.cs");
            string moduleCatalog = ReadProjectFile("Assets/_Project/Scripts/ModuleCatalog.cs");
            string playerBuilder = ReadProjectFile("Assets/_Project/Scripts/PlayerBuilder.cs");
            string pdaConstructionTab = ReadProjectFile("Assets/_Project/Scripts/UI/PDAConstructionTab.cs");

            bool asyncThumbnailPass =
                ContainsAll(thumbnailSystem, "Extension = \".jpg\"", "EncodeNativeArrayToJPG", "Awaitable.BackgroundThreadAsync", "NativeMemorySentinel.RegisterNativeArray") &&
                ContainsAll(thumbnailSystem, "MinPoseCaptureDistanceMeters = 5f", "MinPoseCaptureAngleDegrees = 5f", "MinPoseCaptureQuaternionDot", "HasCapturePoseChanged", "delta.sqrMagnitude > MinPoseCaptureDistanceSq", "Quaternion.Dot") &&
                ContainsAll(thumbnailSystem, "s_completionHistory", "TryGetCompletion(ticket.SequenceId", "if (completion.OperationId != 0u)", "ticket.ByteLength", "catch (OperationCanceledException)", "ReleaseWriteInProgress()") &&
                SourceIndex(thumbnailSystem, "ReleaseWriteInProgress();") < SourceIndex(thumbnailSystem, "CompleteRequest(completion);") &&
                ContainsAll(captureFeature, "RequestAsyncReadback", "SaveThumbnailSystem.ReadbackCompletedCallback") &&
                SourceIndex(saveManager, "SaveThumbnailSystem.CaptureThumbnailForSave(slotName, slotIndex, operationId)") <
                SourceIndex(saveManager, "SaveEvents.TryRaiseSaveStarted(SaveEvents.ComputeSlotHash(slotName));");

            bool loadingStagePass =
                ContainsAll(loadingScreen, "LoadingPipelineStage", "Paging Sectors...", "Hydrating Entities...", "Building NavGrid...", "CharBufferPool.TryAcquire", "SetCharArray", "WritePercent") &&
                ContainsAll(saveManager, "ReportLoadPipelineStage(LoadingPipelineStage.PagingSectors", "ReportLoadPipelineStage(LoadingPipelineStage.HydratingEntities", "ReportLoadPipelineStage(LoadingPipelineStage.BuildingNavGrid") &&
                ContainsAll(saveManager, "LoadingScreenController loadingScreen = ResolveLoadingScreenController()", "IsLoadingScreenControllerUsable", "_cachedLoadingScreenController = null", "CacheLoadingScreenController(loadingScreen)", "ReferenceEquals(loadingScreen, GlobalRegistry.LoadingScreen)", "GlobalRegistryServiceSlot.LoadingScreenRuntime", "CacheLoadingScreenController(currentService as LoadingScreenController)") &&
                ContainsAll(loadingScreen, "TryAbortForUsableExistingRuntime", "IsLoadingScreenRuntimeUsable", "GlobalRegistry.UnregisterLoadingScreenRuntime(current);", "ReferenceEquals(current, null)", "ReferenceEquals(current, this)") &&
                SourceIndex(loadingScreen, "if (TryAbortForUsableExistingRuntime())") < SourceIndex(loadingScreen, "GlobalRegistry.RegisterLoadingScreenRuntime(this);") &&
                SourceIndex(loadingScreen, "current != null && current != this") == int.MaxValue &&
                SourceIndex(saveManager, "_cachedLoadingScreenController = GlobalRegistry.LoadingScreen") == int.MaxValue;

            bool safeAupSnapPass =
                ContainsAll(saveManager, "TryApplySafeAupSnapOnLoad(data)", "AbsoluteUniversePosition.FromRuntimePosition", "HectonFloatingOrigin.BeginSafeTeleportProtocol");

            bool savingHudPass =
                ContainsAll(saveManager, "SaveEvents.TryRaiseMappedWriteStarted(SaveEvents.ComputeSlotHash(slotName));") &&
                ContainsAll(suitHud, "ISaveEventListener", "SavingProgressRoot", "SaveEventType.MappedWriteStarted", "SaveEventType.SaveCompleted", "_savingProgressTargetAlpha", "DataRecPulseShaderName") &&
                ContainsAll(dataRecPulseShader, "Shader \"Hecton8/UI/DataRecPulse\"", "sin(_Time.y * _PulseSpeed)") &&
                ContainsAll(suitHud, "SavingProgressMinimumVisibleSeconds", "_savingProgressHidePending", "BeginSavingProgressMappedWrite", "EmitSavingProgressHapticPulse", "ToolHapticsRuntime.EnqueueSinusoidalCommand", "RequestSavingProgressHide");

            bool savingHudShaderPulsePass =
                ContainsAll(dataRecPulseShader, "_SweepIntensity", "sincos(phase", "rsqrt(radiusSq)", "dot(dir, sweepDir)") &&
                ContainsAll(suitHud, "_savingProgressDataNeedle.material = _savingProgressDataPulseMaterial") &&
                !ContainsAll(suitHud, "SavingProgressSpinDegreesPerSecond", "_savingProgressIconRoot.localEulerAngles") &&
                SourceIndex(dataRecPulseShader, "atan2(") == int.MaxValue;

            bool corruptionDialogPass =
                ContainsAll(saveBinaryStorage, "ConsumeIndexedSectorQuarantineFlag", "ReportIndexedSectorQuarantine", "TryResetIndexedPersistentWorldSectorToPristine") &&
                ContainsAll(saveBinaryStorage, "ReportIndexedSectorBackupRecovery", "CopyAndClearIndexedSectorBackupRecoveryHashes", "TryRestoreIndexedPersistentWorldSectorFromBackup", "refreshBackupBeforeCommit: false") &&
                ContainsAll(persistentWorldRegistry, "ConsumeIndexedSectorBackupRecoveryFlag", "RestoreBackupRecoveredIndexedSectorsFromBackup") &&
                ContainsAll(saveManager, "CriticalSectorCorruptionMessage", "NotificationEvents.PushCritical(CriticalSectorCorruptionMessage)");

            bool seedConsistencyPass =
                ContainsAll(saveManager, "GeologicalAnomalyDetectedMessage", "WorldGenerationVersionId", "RuntimeWorldGenerationVersionId") &&
                ContainsAll(ReadProjectFile("Assets/_Project/Scripts/SaveData.cs"), "worldGenerationVersionId", "CurrentVersion =") &&
                ContainsAll(ReadProjectFile("Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs"), "RuntimeWorldGenerationVersionId") &&
                ContainsAll(ReadProjectFile("Assets/_Project/Scripts/HectonWorldGenerator.cs"), "WorldGenerationAlgorithmVersionId");

            bool inventoryFullWritePass = RunInventoryFullWriteFileStreamAssert(out int rewrittenOffset, out int rewrittenLength);
            bool portableFileStreamWritePass =
                ContainsAll(saveBinaryStorage, "FileStream", "NativeArray<byte>", "UnsafeMemoryCopyGuard.SafeCopy") &&
                SourceIndex(saveBinaryStorage, "MemoryMappedFile.CreateFromFile") == int.MaxValue &&
                ContainsAll(unsafeMemoryCopyGuard, "UnsafeUtility.MemCpy");

            bool inventoryShadowBufferPass =
                ContainsAll(playerInventory, "_inventoryShadowBuffer", "RefreshInventoryShadowBufferFromRuntime", "Fnv1a32Offset", "CommitCurrentInventoryShadowHash") &&
                ContainsAll(saveBinaryPayloadCodec, "WriteNativeBytes", "NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr", "data.hasInventoryShadowPayload");

            bool tombstoneLoadOrderPass =
                ContainsAll(saveManager, "persistentWorldRegistryForLoad?.PreloadTombstonesFromLoadedRecords(loadedWorldDeltas);") &&
                SourceIndex(saveManager, "PreloadTombstonesFromLoadedRecords(loadedWorldDeltas);") <
                SourceIndex(saveManager, "saveable.LoadFromSaveData(data);") &&
                ContainsAll(persistentWorldRegistry, "PreloadTombstonesFromLoadedRecords", "UpsertDeletedTombstone", "RegisterResourceNodeTombstone");

            bool modPayloadSidecarPass =
                ContainsAll(saveBinaryStorage, "ModPayloadSectorPrefix = 0x4D50000000000000UL", "ModPayloadSubBlockSizeBytes", "ModPayloadMagic = 0x50444F4Du") &&
                ContainsAll(saveBinaryStorage, "payloadLength & 1", "Mod payload rejected: odd byte length.", "PayloadLength & 1");

            bool hydrationTimeSlicePass =
                ContainsAll(saveManager, "LoadApplyFrameBudgetTicks = HydrationScheduler.FrameBudgetTicks", "await HydrationScheduler.NextFrameAsync(destroyCancellationToken);", "loadApplyDeadlineTicks = HydrationScheduler.CreateDeadlineTicks();") &&
                ContainsAll(persistentWorldRegistry, "HydrationFrameBudgetTicks = HydrationScheduler.FrameBudgetTicks", "TryProcessHydrationBurst", "await HydrationScheduler.NextFrameAsync(destroyCancellationToken);") &&
                ContainsAll(persistentWorldRegistry, "HydrationPerformanceWarningBudgetTicks = Math.Max(1L, Stopwatch.Frequency / 5000L)", "PublishHydrationBudgetWarningIfNeeded", "GlobalTelemetryBus.PublishPerformanceWarning");

            bool hydrationGcPurgePass =
                ContainsAll(persistentWorldRegistry, "ComputePersistentIdHash(in record.ItemPersistentId)", "ComputePersistentIdHash(in FixedString128Bytes value)") &&
                !ContainsAll(persistentWorldRegistry, "TryResolveItemData(in PersistentWorldItemRecord record", "ItemPersistentId.ToString()");

            bool registryMigrationRsqrtPass =
                ContainsAll(persistentWorldRegistry, "float invDistance = math.rsqrt(distanceSq);", "float moveScalar = math.min(stepMeters * invDistance, 1f);") &&
                SourceIndex(persistentWorldRegistry, "Mathf.Sqrt(distanceSq)") == int.MaxValue;

            bool deterministicScatterCheapRadiusPass =
                ContainsAll(persistentWorldRegistry, "float radius = NextScatter01(ref state) * DropScatterRadiusMeters;") &&
                SourceIndex(persistentWorldRegistry, "math.sqrt(NextScatter01(ref state))") == int.MaxValue;

            bool asyncDehydrationPipelinePass =
                ContainsAll(saveBinaryStorage, "BuildSectorEntityStateSortEntriesJob", "CompressSectorEntityStateJob", "BurstCompile", "xxHash3");

            bool writeAllBytesPurgedPass = !ProjectRuntimeSourceContains("File." + "WriteAllBytes");

            bool saveEventOverflowTelemetryPass =
                ContainsAll(saveEvents, "DroppedEventCount", "ReportOverflow(type);", "GlobalTelemetryBus.PublishPerformanceWarning", "SaveEventOverflowWarningHash", "SaveEventQueueContextHash", "ResolveKnownSlotIndex", "UnknownSlotNumber") &&
                ContainsAll(saveEvents, "DroppedListenerRegistrationCount", "ReportListenerRegistrationOverflow", "SaveEventListenerOverflowWarningHash", "TryRegister(listener)") &&
                ContainsAll(saveEvents, "TruncatedPayloadCount", "ReportPayloadTruncated", "SaveEventPayloadTruncatedWarningHash", "TryReserveMessageSlot", "MessageSlotCapacity", "MessageSlot") &&
                ContainsAll(saveEvents, "_eventEvictionScratch", "bool preserveFailureEvents = !IsFailureEvent(type);", "int evictedIndex = -1;", "if (evictedIndex < 0 && !IsFailureEvent(candidate.Type))", "TryEvictQueueHead(ref queue, ref pendingCount);") &&
                ContainsAll(saveEvents, "TryConsumeLatestFailureSnapshotForUi", "TryConsumeMatchingFailureSnapshotForUi", "UpdateUiFailureSnapshot(type, slotHash, messageHash, message, timestampTicks);", "DoesFailureSnapshotMatch", "MessageSlot = -1") &&
                ContainsAll(saveEvents, "_lastUiFailureSequence = 0u;", "ClearUiFailureSnapshot();", "_lastUiFailureVisible = false;") &&
                ContainsAll(saveEvents, "ManualSlotCount = 3", "ResolveManualSlotName", "ResolveManualSlotHash", "TryResolveKnownSlotName", "slotHash == Slot0Hash", "resolvedSlotName = Slot2Name", "return false;") &&
                ContainsAll(saveEvents, "private static void DrainQueueWithoutBudget", "silent stale-event cleanup must not steal shared LateFrame dispatch budget") &&
                ContainsAll(saveEvents, "NativeMemorySentinel.RegisterNativeQueue", "NativeAllocationLifetime.Session") &&
                ContainsAll(saveEvents, "private static void ReleaseNativeQueue<T>(ref NativeQueue<T> queue, ref int sentinelId)", "Exception firstException = null;", "if (sentinelId > 0)", "NativeMemorySentinel.Unregister(sentinelId);", "finally", "sentinelId = 0;", "if (queue.IsCreated)", "queue.Dispose();", "queue = default;", "if (firstException != null)", "throw firstException;") &&
                SourceIndex(saveEvents, "NativeMemorySentinel.Unregister(sentinelId);") < SourceIndex(saveEvents, "queue.Dispose();") &&
                ContainsAll(pauseMenuController, "ResolveConfiguredSaveSlotName", "ResolveSlotDisplayName", "SaveEvents.ResolveKnownSlotIndex(slotName)") &&
                ContainsAll(pauseMenuController, "NormalizeSaveSlots()", "saveSlots = { \"slot_0\", \"slot_1\", \"slot_2\" }", "ResolveConfiguredSaveSlotName(i)") &&
                ContainsAll(pauseMenuController, "CopyUpperAsciiStringToBuffer(error, buffer, ref cursor)", "CopyStringToBuffer(_cachedUnknownErrorStatus, buffer, cursor)") &&
                ContainsAll(mainMenuController, "SlotCount = SaveEvents.ManualSlotCount", "SaveEvents.ResolveManualSlotName(0)", "SaveEvents.ResolveManualSlotName(1)", "SaveEvents.ResolveManualSlotName(2)") &&
                ContainsAll(saveThumbnailCapture, "public void CaptureThumbnail(string slotName)", "SaveManager.TryResolveSafeSlotName(slotName, out string safeSlotName)", "SaveThumbnailSystem.CaptureThumbnail(safeSlotName, captureCamera);") &&
                SourceIndex(saveThumbnailCapture, "SaveEvents.Register(this)") == int.MaxValue &&
                SourceIndex(saveThumbnailCapture, "ISaveEventListener") == int.MaxValue &&
                SourceIndex(saveEvents, "Slot3Name") == int.MaxValue &&
                SourceIndex(saveEvents, "\"slot_3\"") == int.MaxValue &&
                SourceIndex(mainMenuController, "\"slot_3\"") == int.MaxValue &&
                SourceIndex(pauseMenuController, "\"slot_3\"") == int.MaxValue &&
                SourceIndex(pauseMenuController, "CachedToUpperInvariant(error.ToString())") == int.MaxValue &&
                SourceIndex(saveEvents, ".ToString()") == int.MaxValue &&
                SourceIndex(saveEvents, "Substring(") == int.MaxValue &&
                SourceIndex(saveEvents, "SlotName = string.IsNullOrEmpty(slot)") == int.MaxValue &&
                SourceIndex(saveEvents, "Message = string.IsNullOrEmpty(message)") == int.MaxValue &&
                SourceIndex(saveEvents, "FixedString64Bytes SlotName") == int.MaxValue &&
                SourceIndex(saveEvents, "FixedString128Bytes Message") == int.MaxValue;

            bool saveEventDispatchMutationPass =
                ContainsAll(saveEvents, "ListenerCapacity = 16", "_deferredRegisterListeners", "_deferredUnregisterListeners") &&
                ContainsAll(saveEvents, "public static void FlushPending()", "if (_isDispatching)", "return;") &&
                ContainsAll(saveEvents, "QueueDeferredRegister(listener);", "QueueDeferredUnregister(listener);", "ApplyDeferredListenerMutations();") &&
                ContainsAll(saveEvents, "private static void RegisterImmediate", "CancelDeferredRegister(listener)", "CancelDeferredUnregister(listener)") &&
                ContainsAll(saveEvents, "ListenerExceptionCount", "ReportListenerDispatchException", "SaveEventListenerExceptionWarningHash", "SaveEventListenerExceptionContextHash") &&
                ContainsAll(saveEvents, "listener == null || IsDeferredUnregisterPending(listener)", "_listeners.TryUnregister(listener)") &&
                SourceIndex(saveEvents, "if (_isDispatching)\r\n                CancelDeferredUnregister(listener)") == int.MaxValue;

            bool saveLoadFailureUiBridgePass =
                ContainsAll(saveManager, "SaveEvents.TryRaiseLoadStarted(SaveEvents.ComputeSlotHash(slotName));", "RaiseLoadCompletedWithBackpressureRecovery(SaveEvents.ComputeSlotHash(slotName));", "SaveEvents.TryRaiseLoadFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(loadFailure), loadFailure);") &&
                ContainsAll(saveManager, "SaveServiceUnavailableReason = \"Save service unavailable.\"", "SaveEvents.TryRaiseSaveFailed(ResolveSlotHash(slotIndex), SaveEvents.ComputeMessageHash(SaveServiceUnavailableReason), SaveServiceUnavailableReason);", "ResolveUnavailableSlotContext(slotName, byte.MaxValue, out string unavailableSlotName)", "SaveEvents.TryRaiseLoadFailed(unavailableSlotHash, SaveEvents.ComputeMessageHash(SaveServiceUnavailableReason), SaveServiceUnavailableReason);") &&
                ContainsAll(saveManager, "uint operationId = ResolveOperationId(0u);", "PublishSaveStatus(unavailableSlotHash, operationId, SaveStatusSignal.Rejected, 0f, LoadFailureStatusFlags);", "PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.InProgress, 0.08f, LoadStatusFlags);", "PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: totalTimer.ElapsedMilliseconds, compressedSizeBytes: 0L, succeeded: true));", "PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Completed, 1f, LoadStatusFlags);", "private static void RaiseSaveCompletedWithBackpressureRecovery(", "private static void RaiseLoadCompletedWithBackpressureRecovery(", "SaveEvents.FlushPending();") &&
                ContainsAll(saveEvents, "type == SaveEventType.LoadFailed", "SaveEventListenerInitializationFailureWarningHash", "ReportListenerInitializationFailure(exception);", "return false;") &&
                ContainsAll(mainMenuController, "case SaveEventType.LoadStarted:", "case SaveEventType.LoadCompleted:", "case SaveEventType.LoadFailed:", "_isSaveLoadBusy = false;", "SetSaveLoadButtonsInteractable(true);", "if (!SaveEvents.IsKnownManualSlotName(slotName))", "Ignored unknown save slot click.", "private Action CacheStartGameAction(string slotName)", "_pendingStartSlotName = string.Empty;", "return null;", "bool canRetry = SaveEvents.IsKnownManualSlotName(slotName);", "canRetry ? CacheStartGameAction(slotName) : null", "canRetry ? _returnSaveLoadToMainMenuAction : null", "canRetry ? \"Retry\" : \"OK\"", "canRetry ? \"Return to Menu\" : null") &&
                ContainsAll(mainMenuController, "private static bool IsSaveManagerUsable(SaveManager saveManager)", "return saveManager != null && saveManager.IsInitialized;", "RefreshSaveLoadSlotViewsFromCachedManager();", "private void ApplyUnavailableSaveSlotViews()", "_slotButtonAvailability[i] = false;", "slotUI.Init(slotName, false, string.Empty, 0f, OnSlotClicked);") &&
                ContainsAll(mainMenuController, "if (!IsSaveManagerUsable(saveManager))", "saveManager.TryGetSaveSlotInfo(slotName, out SaveSlotInfo slotInfo)", "string safeSlotName = string.Empty;", "if (!SaveManager.TryResolveSafeSlotName(slotName, out safeSlotName))", "if (!saveManager.SaveExists(safeSlotName))", "slotName = safeSlotName;", "ShowLoadRecoveryModal(in payload, saveManager);", "saveManager.LastLoadUsedBackup", "saveManager.LastLoadSelfRepaired", "LocalizationKeys.WARNING_BACKUP_USED_TITLE", "LocalizationKeys.WARNING_SAVE_REPAIRED_MESSAGE", "LocalizationKeys.WARNING_SAVE_REPAIRED_TITLE", "SetSaveLoadButtonsInteractable(!_isSaveLoadBusy && !_isSceneLoadInFlight);") &&
                ContainsAll(englishLocalization, "\"WARNING_BACKUP_USED_MESSAGE\": \"Primary save file was corrupt. Loaded from backup.\"", "\"WARNING_BACKUP_USED_TITLE\": \"BACKUP LOADED\"", "\"WARNING_SAVE_REPAIRED_MESSAGE\": \"Primary save file was repaired before loading.\"", "\"WARNING_SAVE_REPAIRED_TITLE\": \"SAVE REPAIRED\"") &&
                EveryRootLocalizationJsonContains("\"WARNING_SAVE_REPAIRED_MESSAGE\": \"Primary save file was repaired before loading.\"") &&
                EveryRootLocalizationJsonContains("\"WARNING_SAVE_REPAIRED_TITLE\": \"SAVE REPAIRED\"") &&
                ContainsAll(locKeysGenerated, "WARNING_SAVE_REPAIRED_MESSAGE = LocHash.Compute(\"WARNING_SAVE_REPAIRED_MESSAGE\")", "WARNING_SAVE_REPAIRED_TITLE = LocHash.Compute(\"WARNING_SAVE_REPAIRED_TITLE\")") &&
                ContainsAll(h8Hashes, "WARNINGSAVEREPAIREDMESSAGEId = \"WARNING_SAVE_REPAIRED_MESSAGE\"", "WARNINGSAVEREPAIREDMESSAGEHash = 2818600358u") &&
                ContainsAll(mainMenuController, "TryShowLatestFailureSnapshot", "SaveEvents.TryConsumeLatestFailureSnapshotForUi(", "ResolveSaveFailureMessage(in payload)", "SaveEvents.TryConsumeMatchingFailureSnapshotForUi(", "ref _lastConsumedFailureSnapshotSequence") &&
                ContainsAll(mainMenuController, "IsDuplicateFailureNotification(in payload)", "RememberFailureNotification(in payload);", "BuildFailureNotificationSignature(in payload)", "_lastFailureNotificationSignature", "payload.TimestampTicks") &&
                ContainsAll(pauseMenuController, "case SaveEventType.LoadStarted:", "case SaveEventType.LoadCompleted:", "case SaveEventType.LoadFailed:", "private void SaveSlot(string slotName)", "if (!SaveEvents.IsKnownManualSlotName(slotName))", "const string reason = \"Invalid save slot.\"", "ApplySaveFailedStatusText(slotName, reason);", "private void HandleSaveFailed(string slotName, string error)", "bool canRetry = SaveEvents.IsKnownManualSlotName(slotName);", "canRetry ? CacheRetrySaveSlot(slotName) : null", "canRetry ? \"Retry\" : \"OK\"", "canRetry ? \"Cancel\" : null", "private void HandleLoadFailed(string slotName, string error)", "_saveOperationInFlight = false;", "SetSaveButtonsInteractable(true);", "ApplyLoadFailedStatusText(slotName, error);", "ErrorLoadFailedMessageKeyHash") &&
                ContainsAll(pauseMenuController, "private async Awaitable SaveSlotAsync(string slotName)", "if (saveService == null || !saveService.IsInitialized)", "ApplySaveStatusLiteral(_cachedSaveServiceUnavailable);", "if (saveService.IsBusy)", "ApplySaveStatusText(_cachedWriting, upperSlotName, \"...\")", "await saveService.SaveGameAsync(slotName);") &&
                ContainsAll(pauseMenuController, "ResolveSaveFailureMessage(in payload)", "SaveEvents.TryConsumeMatchingFailureSnapshotForUi(", "ref _lastConsumedFailureSnapshotSequence") &&
                ContainsAll(pauseMenuController, "private void ShowLoadRecoveryModal(string slotName)", "_cachedSaveService is SaveManager saveManager", "saveManager.LastLoadUsedBackup", "saveManager.LastLoadSelfRepaired", "WarningBackupUsedMessageKeyHash", "WarningSaveRepairedMessageKeyHash", "WarningBackupUsedTitleKeyHash", "WarningSaveRepairedTitleKeyHash", "appendRetryPrompt: false", "BuildLocalizedModalTitle", "_modalTitleBuffer") &&
                ContainsAll(saveSlotHoverPreview, "private bool TryPopulatePreviewMetadata()", "!IsSaveManagerUsable(saveManager)", "!saveManager.TryGetSaveSlotInfo(_currentSlotId, out SaveSlotInfo slotInfo)", "!slotInfo.HasAnySaveData", "private void RefreshVisiblePreviewFromCachedSaveManager()", "bool hasPreviewData = TryPopulatePreviewMetadata();", "previewThumbnail.ClearThumbnail();") &&
                ContainsAll(saveSlotHoverPreview, "private static bool IsSaveManagerUsable(SaveManager saveManager)", "return saveManager != null && saveManager.IsInitialized;", "case GlobalRegistryServiceSlot.Save:", "RefreshVisiblePreviewFromCachedSaveManager();") &&
                ContainsAll(gameBootstrapper, "private static bool IsSaveManagerUsable(SaveManager saveManager)", "return saveManager != null && saveManager.IsInitialized;", "if (!IsSaveManagerUsable(GlobalRegistry.Save as SaveManager))", "SaveManager not found or not initialized.", "if (!IsSaveManagerUsable(save))", "if (!SaveManager.TryResolveSafeSlotName(context.TargetSaveSlot, out string targetSaveSlot))", "if (!save.SaveExists(targetSaveSlot))", "await save.LoadGameAsync(targetSaveSlot);", "private void StartNewGameFromRejectedLoadContext()", "GameStartContextHolder.Current = GameStartContext.CreateNewGame();") &&
                ContainsAll(hudSaveNotificationLink, "case SaveEventType.LoadFailed:", "notificationSystem.ShowCritical(in _messageBuffer);", "LoadFailedKeyHash", "AppendLocalized(ref _messageBuffer, LoadFailedKeyHash, \"LOAD FAILED\".AsSpan())") &&
                ContainsAll(hudSaveNotificationLink, "TryShowLatestFailureSnapshot", "SaveEvents.TryConsumeLatestFailureSnapshotForUi(", "ResolveFailureMessageOverride(in payload)", "SaveEvents.TryConsumeMatchingFailureSnapshotForUi(", "IsDuplicateFailureNotification", "RememberFailureNotification(in payload);") &&
                ContainsAll(hudSaveNotificationLink, "private void OnDestroy()", "TryUnregisterHotSwapListener();", "LocalizationEvents.UnregisterCorruptionVisualStateListener(this);", "LocalizationEvents.UnregisterLanguageListener(this);", "SaveEvents.Unregister(this);", "ClearMessageCache();") &&
                SourceIndex(mainMenuController, "_saveManager == null") == int.MaxValue &&
                SourceIndex(mainMenuController, "_saveManager != null") == int.MaxValue &&
                SourceIndex(mainMenuController, "saveManager.SaveExists(slotName)") == int.MaxValue &&
                SourceIndex(gameBootstrapper, "if (save == null)") == int.MaxValue &&
                SourceIndex(gameBootstrapper, "(GlobalRegistry.Save as SaveManager) == null") == int.MaxValue &&
                SourceIndex(gameBootstrapper, "save.SaveExists(context.TargetSaveSlot)") == int.MaxValue &&
                SourceIndex(gameBootstrapper, "save.LoadGameAsync(context.TargetSaveSlot)") == int.MaxValue &&
                SourceIndex(mainMenuController, "OnLoadFailed(SaveEvents.ResolveSlotName(payload.SlotHash), SaveEvents.ResolveMessage(in payload));") == int.MaxValue &&
                SourceIndex(pauseMenuController, "HandleLoadFailed(SaveEvents.ResolveSlotName(payload.SlotHash), SaveEvents.ResolveMessage(in payload));") == int.MaxValue;

            bool modWorldLoadRollbackPass =
                ContainsAll(modWorldPersistenceManager, "_loadRollbackRecords", "_loadApplyPending", "CaptureLoadRollbackSnapshot();", "private void RollbackLoadApplyIfPending()", "private void CommitLoadApply()") &&
                ContainsAll(modWorldPersistenceManager, "case SaveEventType.LoadCompleted:", "CommitLoadApply();", "_restorePending = _records.Count > 0;", "case SaveEventType.LoadFailed:", "RollbackLoadApplyIfPending();", "_restorePending = false;") &&
                ContainsAll(modWorldPersistenceManager, "catch (Exception exception)", "Failed to parse mod world payload", "RollbackLoadApplyIfPending();", "return;") &&
                ContainsAll(modWorldPersistenceManager, "AddOrReplaceRecord(_loadRollbackRecords[i]);", "_nextSpawnSequence = Mathf.Max(1, _loadRollbackNextSpawnSequence);", "RebuildLiveEntityLookupFromScene();", "UnityEngine.Object.FindObjectsByType<ModSpawnedEntity>") &&
                SourceIndex(modWorldPersistenceManager, "case SaveEventType.LoadStarted:\r\n                    RollbackLoadApplyIfPending();") == int.MaxValue &&
                SourceIndex(modWorldPersistenceManager, "case SaveEventType.LoadStarted:\n                    RollbackLoadApplyIfPending();") == int.MaxValue;

            bool modWorldObjectPoolRetryPass =
                ContainsAll(modWorldPersistenceManager, "if (serviceSlot == GlobalRegistryServiceSlot.ObjectPool)", "CacheObjectPoolService(currentService as ObjectPoolManager);", "_restorePending &&", "TryResolveCachedObjectPool(out pool)", "RestoreActiveSceneRecords();") &&
                ContainsAll(modWorldPersistenceManager, "if (!TryResolveCachedObjectPool(out IObjectPoolService pool))", "_restorePending = _records.Count > 0;", "pool.Spawn(prefab") &&
                SourceIndex(modWorldPersistenceManager, "if (pool == null)\r\n                    continue;") == int.MaxValue &&
                SourceIndex(modWorldPersistenceManager, "if (pool == null)\n                    continue;") == int.MaxValue;

            bool modWorldRegistrySceneRetryPass =
                ContainsAll(modWorldPersistenceManager, "IModRegistryEventListener", "TryRegisterModRegistryListener();", "TryUnregisterModRegistryListener();", "_modRegistryListenerRegistered = ModRegistryEvents.Register(this);", "ModRegistryEvents.Unregister(this);") &&
                ContainsAll(modRegistryEvents, "internal static bool Register(IModRegistryEventListener listener)", "return false;", "return RegisterImmediate(listener);", "private static bool RegisterImmediate(IModRegistryEventListener listener)", "ReportListenerRegistrationOverflow();", "return false;", "return true;") &&
                ContainsAll(modWorldPersistenceManager, "private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)", "_liveEntitiesByHash.Clear();", "_restorePending = _records.Count > 0;", "RestoreSceneRecords(SaveMetadata.NormalizeSceneName(scene.name));") &&
                ContainsAll(modWorldPersistenceManager, "ModRegistryEventType.RuntimeRegistryChanged", "if (!_restorePending || _serviceShuttingDown || !isActiveAndEnabled)", "RestoreActiveSceneRecords();") &&
                ContainsAll(modWorldPersistenceManager, "private void RestoreActiveSceneRecords()", "TryRegisterModRegistryListener();", "string activeSceneName = SaveMetadata.NormalizeSceneName(SceneManager.GetActiveScene().name);", "RestoreSceneRecords(activeSceneName);") &&
                ContainsAll(modWorldPersistenceManager, "string activeSceneName = SaveMetadata.NormalizeSceneName(sceneName);", "ModCommandDispatcher.ComputeModHash(activeSceneName)", "string.Equals(record.SceneName, activeSceneName, StringComparison.Ordinal)", "ModAssetManager.LoadPrefab(record.ModId, record.AssetName)", "restoreStillPending = true;", "_restorePending = restoreStillPending;");

            bool modRegistryOverflowRetryPass =
                ContainsAll(modRegistryEvents, "internal static int DroppedEventCount => _droppedEventCount;", "internal static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;", "RegistryEventQueueOverflowWarningHash = 0x4D524F46u", "RegistryEventListenerOverflowWarningHash = 0x4D524C46u", "MarkOverflowedIfNotAlreadyQueued(eventType);", "ReportQueueOverflow(eventType);") &&
                ContainsAll(modRegistryEvents, "RecycleRegistryChanged = 5", "private const int PendingEventCapacity = 5;", "private static bool _recycleRegistryChangeQueued;", "private static bool _recycleRegistryChangeOverflowed;", "internal static void NotifyRecycleRegistryChanged()", "Enqueue(ModRegistryEventType.RecycleRegistryChanged, 0u, 0u, 0);") &&
                ContainsAll(modRegistryEvents, "private static void ReplayOverflowedEvents()", "TryReplayOverflowedEvent(ModRegistryEventType.RuntimeRegistryChanged, ref _runtimeRegistryChangeOverflowed);", "TryReplayOverflowedEvent(ModRegistryEventType.BuildableRegistryChanged, ref _buildableRegistryChangeOverflowed);", "TryReplayOverflowedEvent(ModRegistryEventType.RecycleRegistryChanged, ref _recycleRegistryChangeOverflowed);") &&
                ContainsAll(modRegistryEvents, "private static void TryReplayOverflowedEvent(ModRegistryEventType eventType, ref bool overflowed)", "if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)", "if (IsQueued(eventType))", "overflowed = false;", "Enqueue(eventType, 0u, 0u, 1);") &&
                ContainsAll(modRegistryEvents, "case ModRegistryEventType.RecycleRegistryChanged:", "_recycleRegistryChangeOverflowed = true;", "_recycleRegistryChangeQueued = true;", "_recycleRegistryChangeQueued = false;") &&
                ContainsAll(modRegistryEvents, "if (_listenerCount >= ListenerCapacity)", "ReportListenerRegistrationOverflow();", "private static void ReportListenerRegistrationOverflow()", "_droppedListenerRegistrationCount++;", "RegistryEventListenerContextHash", "_droppedListenerRegistrationCount);") &&
                ContainsAll(modRegistryEvents, "_dispatchListeners", "CaptureDispatchSnapshot(count);", "IModRegistryEventListener listener = _dispatchListeners[i];", "ClearDispatchSnapshot(count);", "ClearDispatchSnapshot(ListenerCapacity);") &&
                ContainsAll(modRegistryEvents, "internal static int ListenerExceptionCount => _listenerExceptionCount;", "catch (Exception exception)", "ReportListenerDispatchException(payload.EventType, exception);", "RegistryEventListenerExceptionWarningHash = 0x4D524558u", "RegistryEventListenerExceptionContextHash ^ ((uint)eventType << 24)") &&
                ContainsAll(modRegistryEvents, "_isDispatching = false;", "ReplayOverflowedEvents();", "PromoteNextFrameEventsIfFrontEmpty();", "ClearOverflowedFlags();", "PublishPerformanceWarningBestEffort(", "GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, value);");

            bool modWorldStaleHandleRestorePass =
                ContainsAll(modWorldPersistenceManager, "_liveEntitiesByHash.TryGetValue(record.SpawnHash, out ModSpawnedEntity liveMarker)", "if (liveMarker != null)", "_liveEntitiesByHash.Remove(record.SpawnHash);", "GameObject prefab = ModAssetManager.LoadPrefab(record.ModId, record.AssetName);") &&
                SourceIndex(modWorldPersistenceManager, "_liveEntitiesByHash.TryGetValue(record.SpawnHash, out ModSpawnedEntity liveMarker)") <
                SourceIndex(modWorldPersistenceManager, "GameObject prefab = ModAssetManager.LoadPrefab(record.ModId, record.AssetName);") &&
                SourceIndex(modWorldPersistenceManager, "if (_liveEntitiesByHash.ContainsKey(record.SpawnHash))") == int.MaxValue;

            bool modWorldSpawnHashSanitizerPass =
                ContainsAll(modWorldPersistenceManager, "uint spawnHash = ModCommandDispatcher.ComputeModHash(record.SpawnId);", "if (record.SpawnHash != spawnHash)", "record.SpawnHash = spawnHash;", "record.SceneName = SaveMetadata.NormalizeSceneName(record.SceneName);", "uint sceneHash = ModCommandDispatcher.ComputeModHash(record.SceneName);") &&
                SourceIndex(modWorldPersistenceManager, "record.SpawnHash == 0u && !string.IsNullOrWhiteSpace(record.SpawnId)") == int.MaxValue;

            bool modAssetBindingLifecyclePass =
                ContainsAll(modAssetManager, "internal static void UnregisterBundlePath(string modId)", "private static void UnloadModAssets(uint modHash)", "private static void UnloadBundle(uint modHash)", "private static void UnloadRawTexturesForMod(uint modHash)") &&
                ContainsAll(modAssetManager, "_bundlePaths.Remove(modHash);", "UnloadModAssets(modHash);", "_loadedBundles.Remove(modHash);", "bundle.Unload(false);") &&
                ContainsAll(modAssetManager, "if (_bundlePaths.ContainsKey(modHash))", "_bundlePaths[modHash] = bundlePath;", "UnloadRawTexturesForMod(modHash);") &&
                ContainsAll(modAssetManager, "_rawTextureModHashes", "uint cacheKey = ComputeAssetCacheHash(modHash, filePath);", "_rawTextureModHashes[cacheKey] = modHash;", "_rawTextureModHashes.Remove(cacheKey);", "_rawTextureModHashes.Clear();") &&
                ContainsAll(modResourceProxy, "internal static void UnregisterModResources(string modId)", "private static void RemoveRecordAt(int index)", "RemoveResourceIndex(moved);", "AddResourceIndex(moved, index);", "internal static int DroppedResourceRegistrationCount => _droppedResourceRegistrationCount;", "private static void ReportResourceRegistrationOverflow(", "GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, value);") &&
                ContainsAll(modSettingsRegistry, "internal static void UnregisterModSettings(string modId)", "private static void RemoveEntryAt(int index)", "_entryIndexByHash.Remove(removed.KeyHash);", "_entryIndexByHash[moved.KeyHash] = index;", "ModRegistryEvents.NotifySettingsRegistryChanged(modHash, 0u);", "ModSettingCallbackExceptionWarningHash = 0x4D534346u", "private static void ReportSettingCallbackException(", "GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, value);", "ModLoader.DisableManagedMod(modId, ModSettingCallbackExceptionDisableReason);") &&
                ContainsAll(itemCatalog, "_runtimeItemOwnerByPersistentId", "internal bool TryRegisterRuntimeItem(ItemData item, string ownerId, out string error)", "if (ContainsRuntimeItem(item))", "RecordRuntimeItemOwnerIfUnownedOrSameOwner(persistentId, ownerId);", "internal bool TryPromoteRuntimeItemOwnerIfPresent(ItemData item, string ownerId)", "!ContainsRuntimeItem(item)", "private void RecordRuntimeItemOwnerIfUnownedOrSameOwner(string persistentId, string ownerId)", "!string.Equals(registeredOwner, ownerId, StringComparison.Ordinal)", "internal bool UnregisterRuntimeItemsForOwner(string ownerId)", "_runtimeItemOwnerByPersistentId.Remove(persistentId);", "_runtimeItems.RemoveAt(i);", "RebuildLookup();") &&
                ContainsAll(moduleCatalog, "_runtimeModuleOwnerByPersistentId", "internal bool TryRegisterRuntimeModule(BuildableData data, string customCategory, string ownerId, out string error)", "if (ContainsRuntimeModule(data))", "RecordRuntimeModuleOwnerIfUnownedOrSameOwner(persistentId, ownerId);", "internal bool TryPromoteRuntimeModuleOwnerIfPresent(BuildableData data, string customCategory, string ownerId)", "!ContainsRuntimeModule(data)", "if (RecordRuntimeModuleOwnerIfUnownedOrSameOwner(persistentId, ownerId))", "_runtimeCategoryByPersistentId[persistentId] = NormalizeRuntimeCategory(customCategory);", "_combinedModulesDirty = true;", "private bool RecordRuntimeModuleOwnerIfUnownedOrSameOwner(string persistentId, string ownerId)", "!string.Equals(registeredOwner, ownerId, StringComparison.Ordinal)", "internal bool UnregisterRuntimeModulesForOwner(string ownerId)", "_runtimeModuleOwnerByPersistentId.Remove(persistentId);", "_runtimeCategoryByPersistentId?.Remove(persistentId);", "_runtimeModules.RemoveAt(i);", "RebuildLookup();") &&
                ContainsAll(modRuntimeState, "_liveItems", "_liveItemCatalogs", "ReplayLiveRegistrationsToActiveCatalog();", "AddOrReplaceLiveItemRegistration(itemData, modId, modHash);", "AddOrReplaceLiveItemRegistration(registration.Data, registration.ModId, registration.ModHash);", "TrackLiveCatalog(catalog);", "TryFindLiveItem(itemData, out existingLiveItemIndex)", "PromoteItemRegistrationOwnerIfUnownedOrSameMod(_liveItems, existingLiveItemIndex);", "PromoteKnownItemCatalogOwnersIfUnownedOrSameMod(itemData);", "catalog.TryPromoteRuntimeItemOwnerIfPresent(itemData, modId);", "TryFindPendingItem(itemData, out existingPendingItemIndex)", "PromoteItemRegistrationOwnerIfUnownedOrSameMod(_pendingItems, existingPendingItemIndex);", "RemoveLiveItemRegistrationsForMod(modId)", "UnregisterRuntimeItemsFromKnownCatalogs(modId)", "catalog.TryRegisterRuntimeItem(itemData, modId, out error);", "!ContainsKnownLiveCatalog(catalog) && catalog.UnregisterRuntimeItemsForOwner(modId)", "catalog.TryRegisterRuntimeItem(itemData, registration.ModId, out string error)", "ModRegistryEvents.NotifyRuntimeRegistryChanged(ModCommandDispatcher.ComputeModHash(modId));", "ModRegistryEvents.NotifyRuntimeRegistryChanged(0u);") &&
                ContainsAll(modRuntimeState, "_liveBuildables", "_liveModuleCatalogs", "AddOrReplaceLiveBuildableRegistration(buildableData, normalizedCategory, modId, modHash);", "AddOrReplaceLiveBuildableRegistration(registration.Data, registration.CustomCategory, registration.ModId, registration.ModHash);", "TrackLiveCatalog(catalog);", "TryFindLiveBuildable(buildableData, out existingLiveBuildableIndex)", "PromoteBuildableRegistrationOwnerIfUnownedOrSameMod(_liveBuildables, existingLiveBuildableIndex, customCategory);", "PromoteKnownModuleCatalogOwnersIfUnownedOrSameMod(buildableData, customCategory);", "catalog.TryPromoteRuntimeModuleOwnerIfPresent(buildableData, normalizedCategory, modId);", "TryFindPendingBuildable(buildableData, out existingPendingBuildableIndex)", "PromoteBuildableRegistrationOwnerIfUnownedOrSameMod(_pendingBuildables, existingPendingBuildableIndex, customCategory);", "RemoveLiveBuildableRegistrationsForMod(modId)", "UnregisterRuntimeBuildablesFromKnownCatalogs(modId)", "catalog.TryRegisterRuntimeModule(buildableData, normalizedCategory, modId, out error);", "!ContainsKnownLiveCatalog(catalog) && catalog.UnregisterRuntimeModulesForOwner(modId)", "catalog.TryRegisterRuntimeModule(registration.Data, registration.CustomCategory, registration.ModId, out string error)") &&
                ContainsAll(playerInventory, "itemCatalog.TryGetRuntimeDescriptor(itemHashId, out runtimeDescriptor)", "itemCatalog.FindByHash(itemHashId)") &&
                ContainsAll(playerBuilder, "_buildCatalog.GetViewableCount(_cachedQuestSystem)", "_buildCatalog.GetViewableAt(index, _cachedQuestSystem)") &&
                ContainsAll(pdaConstructionTab, "BuildableData data = catalog.GetViewableAt(i, _cachedQuestSystem);") &&
                ContainsAll(modRuntimeState, "private struct RuntimeRecipeRegistration", "internal static void UnregisterModRecipes(string modId)", "bool removedStaleOwnerRecipes = RemoveStaleOwnerRecipes();", "TryFindRecipeReference(recipeData, out existingRecipeIndex)", "PromoteRuntimeRecipeOwnerIfUnownedOrSameMod(existingRecipeIndex);", "private static bool TryFindRecipeReference(RecipeData recipeData, out int index)", "private static void PromoteRuntimeRecipeOwnerIfUnownedOrSameMod(int index)", "private static bool RemoveStaleOwnerRecipes()", "private static bool IsRuntimeOwnerStillRegistered(uint modHash)", "return _runtimeRecipes[index].Data;", "ReferenceEquals(_runtimeRecipes[i].Data, recipeData)", "_runtimeRecipes[index] = registration;", "ModRegistryEvents.NotifyRecipeRegistryChanged();") &&
                ContainsAll(recyclingRegistry, "using Hecton8.Modding;", "_customYieldOwnerById", "_customYieldOwnerByHash", "_stableIdRemovalScratch", "_hashRemovalScratch", "internal static bool ClearOwner(string ownerId)", "internal static bool TryGetYield(uint targetHashId, out ResourceStack[] yield, out uint ownerHash)", "internal static uint ComputeStableItemHash(string legacyItemId)", "internal static uint RegistryRevision => _registryRevision;", "_registryRevision = 0u;", "private static string ResolveActiveOwnerId()", "private static void RecordOwner<TKey>", "private static bool TryResolveOwnerHash<TKey>", "private static void NotifyRecycleRegistryChanged()", "return ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModId : string.Empty;", "ownerHash = ModCommandDispatcher.ComputeModHash(ownerId);", "_registryRevision++;", "if (_registryRevision == 0u)", "_registryRevision = 1u;", "ModRegistryEvents.NotifyRecycleRegistryChanged();") &&
                ContainsAll(recyclingRegistry, "NotifyRecycleRegistryChanged();", "if (removed)", "NotifyRecycleRegistryChanged();", "return removed;") &&
                ContainsAll(scrapManager, "uint unusedOverlayOwnerHash;", "return TryBuildRecycleYieldSnapshot(sourceItem, destination, out resolvedCount, out unusedOverlayOwnerHash);", "out uint overlayOwnerHash", "out bool usedRegisteredOverlay", "usedRegisteredOverlay = false;", "RecyclingRegistry.TryGetYield(", "out overlayOwnerHash", "usedRegisteredOverlay = true;", "return CopyYieldSnapshotNonAlloc(registeredYield, destination, out resolvedCount);", "RecipeData recipe;") &&
                ContainsAll(modRuntimeState, "return RecyclingRegistry.TryRegister(itemId, yield, out error);", "RecyclingRegistry.ClearOwner(modId);") &&
                ContainsAll(resourceRecycler, "using Hecton8.Modding;", "ActiveModuleRegistrationOverflowWarningHash = 0x5252434Fu", "ActiveModuleRegistrationOverflowContextHash = 0x52524D4Fu", "private static int s_DroppedActiveModuleRegistrationCount;", "internal static int DroppedActiveModuleRegistrationCount => s_DroppedActiveModuleRegistrationCount;", "s_DroppedActiveModuleRegistrationCount = 0;", "private static bool s_ModRegistryEventRegistered;", "private static ModRegistryEventAdapter s_ModRegistryEventAdapter;", "s_ModRegistryEventRegistered = false;", "s_ModRegistryEventAdapter = null;", "_pendingRecycleOverlayOwnerHash", "_pendingRecycleSubjectHash", "_pendingRecycleRegistryRevision", "_pendingRecycleUsesOverlay", "_pendingRecycleSnapshotInvalidated", "TryRegisterModRegistryListener();", "TryUnregisterModRegistryListenerIfNoActiveModules();", "s_ModRegistryEventRegistered = ModRegistryEvents.Register(GetModRegistryEventAdapter());", "ModRegistryEvents.Unregister(s_ModRegistryEventAdapter);", "private sealed class ModRegistryEventAdapter : IModRegistryEventListener") &&
                ContainsAll(resourceRecycler, "private static void HandleModRegistryEvent(in ModRegistryEventPayload payload)", "ModRegistryEventType.RecycleRegistryChanged", "for (int i = s_ActiveModuleCount - 1; i >= 0; i--)", "ResourceRecyclerModule module = s_ActiveModules[i];", "if (module == null || !module.isActiveAndEnabled)", "module.MarkPendingRecycleSnapshotDirtyIfAffected(payload.ModHash, payload.SubjectHash);", "private void MarkPendingRecycleSnapshotDirtyIfAffected(uint modHash, uint sourceItemHash)", "!_pendingRecycleUsesOverlay || _activeSourceItem == null", "modHash != 0u && modHash != _pendingRecycleOverlayOwnerHash", "sourceItemHash != 0u && sourceItemHash != _pendingRecycleSubjectHash", "_pendingRecycleSnapshotInvalidated = true;") &&
                ContainsAll(resourceRecycler, "private void RegisterModuleInstance()", "if (s_ActiveModuleCount >= s_ActiveModules.Length)", "ReportActiveModuleRegistrationOverflow();", "private static void ReportActiveModuleRegistrationOverflow()", "s_DroppedActiveModuleRegistrationCount++;", "PublishPerformanceWarningBestEffort(", "GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, value);", "catch (System.Exception)") &&
                ContainsAll(baseLogisticsNetwork, "StorageEndpointRegistrationOverflowWarningHash = 0x424C534Fu", "FabricatorEndpointRegistrationOverflowWarningHash = 0x424C464Fu", "ReservationPoolExhaustedWarningHash = 0x424C5258u", "ReservationPoolInvalidSlotWarningHash = 0x424C524Eu", "ReservationPoolReturnOverflowWarningHash = 0x424C5252u", "private static int s_DroppedStorageEndpointRegistrationCount;", "private static int s_DroppedFabricatorEndpointRegistrationCount;", "private static int s_ReservationPoolExhaustionCount;", "private static int s_ReservationPoolInvalidSlotCount;", "private static int s_ReservationPoolReturnOverflowCount;", "internal static int DroppedStorageEndpointRegistrationCount => s_DroppedStorageEndpointRegistrationCount;", "internal static int DroppedFabricatorEndpointRegistrationCount => s_DroppedFabricatorEndpointRegistrationCount;", "internal static int ReservationPoolExhaustionCount => s_ReservationPoolExhaustionCount;", "internal static int ReservationPoolInvalidSlotCount => s_ReservationPoolInvalidSlotCount;", "internal static int ReservationPoolReturnOverflowCount => s_ReservationPoolReturnOverflowCount;", "s_DroppedStorageEndpointRegistrationCount = 0;", "s_DroppedFabricatorEndpointRegistrationCount = 0;", "s_ReservationPoolExhaustionCount = 0;", "s_ReservationPoolInvalidSlotCount = 0;", "s_ReservationPoolReturnOverflowCount = 0;", "if (s_StorageEndpointCount >= StorageEndpointCapacity)", "ReportStorageEndpointRegistrationOverflow();", "if (s_FabricatorEndpointCount >= FabricatorEndpointCapacity)", "ReportFabricatorEndpointRegistrationOverflow();", "if (s_ReservationPoolCount <= 0)", "ReportReservationPoolExhausted();", "ReportReservationPoolInvalidSlot();", "ReportReservationPoolReturnOverflow();", "private static void ReportStorageEndpointRegistrationOverflow()", "private static void ReportFabricatorEndpointRegistrationOverflow()", "private static void ReportReservationPoolExhausted()", "private static void ReportReservationPoolInvalidSlot()", "private static void ReportReservationPoolReturnOverflow()", "bool reservedAnyCost = false;", "if (!reservedAnyCost)", "GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, value);", "catch (System.Exception exception) when (!(exception is FatalArchitectureException))", "LogPerformanceWarningTelemetryException(exception);") &&
                ContainsAll(fabricator, "BaseLogisticsNetwork.TryReserveResources(", "out _networkReservation", "BaseLogisticsNetwork.RollbackReserved(_networkReservation);", "BaseLogisticsNetwork.CommitReserved(_networkReservation);") &&
                ContainsAll(repairDroneHub, "internal bool TryQueueDroneResupplyCommit(int requestedUnits, int droneId, out bool committedImmediately, out int queuedReservationId)", "int safeRequestedUnits = 1;", "BaseLogisticsNetwork.TryReserveResources(grid, _repairSupplyHashIds, _repairSupplyAmounts, 1, out BaseLogisticsNetwork.LogisticsReservation reservation)", "queuedReservationId = reservation.ReservationId;", "BaseLogisticsNetwork.TryCommitReservedViaCommandQueue(reservation, requesterId, out committedImmediately)", "if (committedImmediately)", "queuedReservationId = 0;", "BaseLogisticsNetwork.CommitReserved(reservation);") &&
                ContainsAll(threadSafeCommandQueue, "public interface IStorageReservationCommitResolvedListener", "StorageReservationCommitResolvedPayload", "StorageReservationCommitListenerGeneration", "AdvanceStorageReservationCommitListenerGeneration();", "void ReleaseReservation(int reservationId);", "PrepareStorageReservationCommitBridgeForPersistenceSnapshot();", "_persistenceSnapshotCommandBuffer", "DrainPendingStorageReservationCommitsForPersistenceSnapshot();", "DrainAbandonedPendingCommands(dispatchStorageReservationFailures: false);", "DrainAbandonedPendingCommands(dispatchStorageReservationFailures: true);", "DrainAbandonedStorageReservationCommitResolvedEvents(dispatchPendingEvents: false);", "DrainAbandonedStorageReservationCommitResolvedEvents(dispatchPendingEvents: true);", "target.ReleaseReservation(command.IntValue);", "DispatchStorageReservationCommitResolvedFailure(command.SecondaryToken, command.IntValue);", "RaiseStorageReservationCommitResolved(command.SecondaryToken, command.IntValue, committed);", "EnqueueStorageReservationCommitResolved(in payload)", "Volatile.Write(ref readyFlag, 0);", "ReportStorageReservationCommitOverflowOncePerFrame();", "StorageReservationCommitOverflowCount", "_storageReservationCommitOverflowCount++", "_storageCommitListenerCapacityWarningHash", "StorageReservationCommitListenerCapacityExceededCount", "_storageReservationCommitListenerCapacityExceededCount = 0;", "ReportStorageReservationCommitListenerCapacityExceeded();", "IncrementStorageReservationCommitListenerCapacityExceededCount();", "Interlocked.CompareExchange(", "PublishQueuePerformanceWarningBestEffort(", "_storageCommitListenerCapacityWarningHash", "StorageReservationCommitListenerCapacity)") &&
                ContainsAll(saveManager, "await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(destroyCancellationToken);", "ThreadSafeCommandQueue.PrepareStorageReservationCommitBridgeForPersistenceSnapshot();", "SortRegistryIfDirty(SavePriorityComparer);") &&
                ContainsAll(droneFleetManager, "hub.TryQueueDroneResupplyCommit(1, drone.DroneId, out bool committedImmediately, out int queuedReservationId)", "StorageReservationStaleAckCount", "StorageReservationMismatchAckCount", "s_StorageReservationStaleAckWarningHash", "s_StorageReservationMismatchAckWarningHash", "s_StorageReservationAckContextHash", "s_StorageReservationStaleAckCount = 0;", "s_StorageReservationMismatchAckCount = 0;", "s_StorageReservationCommitResolvedListenerGeneration = -1;", "EnsureStorageReservationCommitResolvedBridge();", "ThreadSafeCommandQueue.StorageReservationCommitListenerGeneration", "ThreadSafeCommandQueue.Register(s_StorageReservationCommitResolvedBridge)", "HandleStorageReservationCommitResolved(", "int slot = ResolveHeadlessSlot(requesterId);", "slot >= s_PendingResupplyReservationIdsBySlot.Length)", "ReportStorageReservationStaleAck(requesterId);", "int expectedReservationId = s_PendingResupplyReservationIdsBySlot[slot];", "if (expectedReservationId <= 0)", "if (reservationId != expectedReservationId)", "ReportStorageReservationMismatchAck(reservationId);", "TryApplyResolvedResupplyCommitToLiveSlot(slot, commitSucceeded)", "TryConsumeResolvedResupplyCommitAck(slot, committed, ref drone, out bool droneChanged)", "ClearPendingResupplyCommitAck(slot);", "MirrorDroneSoA(slot, in drone, positionsSoA, stateBytes, stateDtos, targetDtos);", "RefreshHeadlessCounters(droneStates);", "RefreshFleetStatusSnapshotFromDroneStates(droneStates);", "UpdateDrawBounds();", "PublishSnapshot();", "s_LastFleetStatusSnapshot = new FleetStatusSnapshot(", "solderReserve += math.max(0, drone.SolderUnits);", "GlobalTelemetryBus.PublishPerformanceWarning(warningHash, s_StorageReservationAckContextHash, value);", "catch (System.Exception exception) when (!(exception is FatalArchitectureException))", "LogStorageReservationAckTelemetryException(exception);") &&
                ContainsAll(maintenanceStationModule, "BaseLogisticsNetwork.TryReserveResources(", "out _activeReservation", "BaseLogisticsNetwork.CommitReserved(_activeReservation);", "_activeReservation = null;", "_repairTargetDurability = maxDurability;", "BaseLogisticsNetwork.RollbackReserved(_activeReservation);") &&
                SourceIndex(resourceRecycler, "using Hecton8.Construction;") == int.MaxValue &&
                SourceIndex(resourceRecycler, "BaseLogisticsNetwork.RegisterRecycler") == int.MaxValue &&
                SourceIndex(resourceRecycler, "BaseLogisticsNetwork.UnregisterRecycler") == int.MaxValue &&
                SourceIndex(baseLogisticsNetwork, "RecyclerEndpoint") == int.MaxValue &&
                ContainsAll(resourceRecycler, "ScrapManager.TryBuildRecycleYieldSnapshot(", "out uint overlayOwnerHash", "out bool usedRegisteredOverlay", "_pendingRecycleOverlayOwnerHash = overlayOwnerHash;", "_pendingRecycleRegistryRevision = RecyclingRegistry.RegistryRevision;", "_pendingRecycleUsesOverlay = usedRegisteredOverlay;", "if (_pendingRecycleUsesOverlay &&", "_pendingRecycleRegistryRevision != RecyclingRegistry.RegistryRevision", "_pendingRecycleSnapshotInvalidated = true;", "if (_pendingRecycleSnapshotInvalidated && !TryRefreshInvalidatedPendingYield())", "private bool TryRefreshInvalidatedPendingYield()", "ScrapManager.ClearYieldScratch(_pendingYieldScratch, _pendingYieldCount);", "_pendingYield = null;", "_pendingRecycleSnapshotInvalidated = false;", "if (TryBufferItem(sourceItem))", "ClearPendingOutput();") &&
                ContainsAll(logisticsSorter, "using Hecton8.World;", "PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;", "int persistentWorldDropCandidateCount = 0;", "persistentWorldRegistry.CanRegisterDroppedItem(item, quantity, dropPosition)", "persistentWorldDropCandidateCount++;", "persistentWorldRegistry.CanRegisterDroppedItemBatch(persistentWorldDropCandidateCount)") &&
                ContainsAll(deepDrill, "internal void RestoreFromSaveData(ModuleDTO dto, ItemCatalog itemCatalog)", "float restoredCycleTimer = Mathf.Clamp(", "bool hasSavedBufferedOutput =", "dto.drillBufferedAmount > 0", "!string.IsNullOrWhiteSpace(dto.drillBufferedItemId);", "if (!hasSavedBufferedOutput)", "ClearBufferedOutputState();", "_cycleTimer = restoredCycleTimer;", "if (itemCatalog == null)", "ItemData item = itemCatalog.FindById(dto.drillBufferedItemId);", "if (item == null)", "ClearBufferedOutputState();", "_bufferedItem = item;") &&
                ContainsAll(resourceRecycler, "using Hecton8.SaveSystem;", "using Hecton8.Gameplay;", "using Hecton8.World;", "internal void PopulateSaveData(ref ModuleDTO dto)", "dto.recyclerBufferedSlotCount = 0;", "dto.recyclerActiveSourceItemId = string.Empty;", "AppendRecyclerBufferedSaveSlot(ref dto, item, quantity);", "AppendRecyclerBufferedSaveSlot(ref dto, _activeSourceItem, 1);", "dto.recyclerPendingYieldItemIds[slot] = stack.Item.PersistentId;", "internal void RestoreFromSaveData(ModuleDTO dto, ItemCatalog itemCatalog)", "bool hasSavedRecyclerState = HasSavedRecyclerState(in dto);", "if (!CanResolveRecyclerRestoreState(in dto, itemCatalog))", "ClearRecyclerRuntimeStateForRestore();", "private static bool HasSavedRecyclerState(in ModuleDTO dto)", "private bool CanResolveRecyclerRestoreState(in ModuleDTO dto, ItemCatalog itemCatalog)", "_bufferItems[i] = item;", "_bufferQuantities[i] = quantity;", "_pendingYieldScratch[restoredYieldCount] = new ResourceStack", "_hasPendingOutput = true;", "PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;", "int persistentWorldDropCandidateCount = 0;", "persistentWorldDropCandidateCount += CountPersistentWorldDropCandidate(", "owner,", "pool,", "dropPosition,", "persistentWorldRegistry.CanRegisterDroppedItemBatch(persistentWorldDropCandidateCount)", "private static int CountPersistentWorldDropCandidate(", "persistentWorldRegistry.CanRegisterDroppedItem(item, quantity, dropPosition)") &&
                ContainsAll(fabricator, "internal void PopulateSaveData(ref ModuleDTO dto)", "dto.fabricatorPendingOutputItemId = string.Empty;", "dto.fabricatorPendingOutputQuantity = 0;", "if (!HasPendingCraftOutput)", "ItemData result = _pendingCraftOutputItem;", "dto.fabricatorPendingOutputItemId = persistentId;", "dto.fabricatorPendingOutputQuantity = quantity;", "dto.fabricatorPendingOutputTotalQuantity = math.max(quantity, _pendingCraftOutputTotalQuantity);", "internal void RestoreFromSaveData(ModuleDTO dto, ItemCatalog itemCatalog)", "int quantity = math.max(0, dto.fabricatorPendingOutputQuantity);", "if (itemCatalog == null || string.IsNullOrWhiteSpace(itemId))", "ItemData result = itemCatalog.FindById(itemId);", "ClearPendingCraftOutput();", "_pendingCraftOutputItem = result;", "_pendingCraftOutputQuantity = quantity;") &&
                ContainsAll(fabricator, "internal bool CanEjectPendingCraftOutput(PlayerInventory inventory, Vector3 dropPosition)", "if (!HasPendingCraftOutput)", "inventory.CanAcceptItemQuantity(itemHashId, quantity)", "PersistentWorldRegistry registry = _persistentWorldRegistry;", "registry.CanRegisterDroppedItem(result, quantity, dropPosition)", "internal bool EjectPendingCraftOutput(PlayerInventory inventory, ref Vector3 dropPosition)", "inventory.TryAddItem(itemHashId, quantity)", "ClearPendingCraftOutput();", "registry.TryRegisterDroppedItem(result, quantity, dropPosition)", "dropPosition.x += 0.3f;") &&
                ContainsAll(cultivationManager, "public void PopulateSaveData(ref ModuleDTO moduleDto, ItemCatalog itemCatalog)", "int[] seedHashIds = moduleDto.cultivationSeedItemHashIds;", "seedIds[writeIndex] = item != null && !string.IsNullOrWhiteSpace(item.PersistentId)", "seedHashIds[writeIndex] = slot.SeedItemHashId;", "public void RestoreFromSaveData(ModuleDTO moduleDto, ItemCatalog itemCatalog)", "int safeCount = ResolveCultivationRestoreCount(in moduleDto);", "if (!CanResolveCultivationRestoreState(in moduleDto, itemCatalog, safeCount))", "ClearSlots();", "ResolveSavedCultivationSeedHashId(in moduleDto, itemCatalog, i, persistentId)", "return moduleDto.cultivationSeedItemHashIds[slotIndex];", "internal bool CanEjectCultivationContents(BaseModule owner, PlayerInventory inventory, Vector3 dropPosition)", "Span<int> itemHashIds = stackalloc int[MaxCultivationSlots];", "Span<ulong> geneticsMasks = stackalloc ulong[MaxCultivationSlots];", "Span<ushort> qualityMillis = stackalloc ushort[MaxCultivationSlots];", "inventory.CanAcceptItemWithStateBatch(itemHashIds, geneticsMasks, qualityMillis, occupiedCount)", "PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;", "persistentWorldRegistry.CanRegisterDroppedItem(item, quantities[i], dropPosition)", "persistentWorldRegistry.CanRegisterDroppedItemBatch(occupiedCount)", "internal bool EjectCultivationContents(BaseModule owner, PlayerInventory inventory, ref Vector3 dropPosition)", "inventory.TryAddItemWithState(slot.SeedItemHashId, new PlayerInventory.ItemState(geneticsMask, qualityMilli))", "persistentWorldRegistry.TryRegisterDroppedItemWithState(", "private int BuildCultivationEjectionBatch(", "geneticsMasks[count] = SanitizeGeneticsMask(slot.GeneticsMask);", "qualityMillis[count] = ResolveCultivationQualityMilli(slot.Quality01);", "private static ushort ResolveCultivationQualityMilli(float quality01)", "private static bool IsFiniteRuntimePosition(Vector3 position)") &&
                ContainsAll(playerInventory, "public bool CanAcceptItemWithStateBatch(", "private bool CanAcceptQuantityWithStateBatch(", "CopyNativeArray(_stackCounts, _scavengeSimStackCounts);", "_grid.CopyOccupiedMask(_simulationOccupiedCells);", "byte compressedGenetics = CompressItemGenetics(geneticsMasks[groupIndex]);", "ushort resolvedQualityMilli = NormalizeQualityMilli(qualityMillis[groupIndex]);", "!CanStackStatefulItemAt(anchorIndex, resolvedStateFlags, compressedGenetics, resolvedQualityMilli)", "TryReservePlacementInSimulation(in descriptor)", "private bool CanStackStatefulItemAt(", "_itemStateFlags[anchorIndex] == itemStateFlags", "_itemGenetics[anchorIndex] == geneticsMask", "NormalizeQualityMilli(_qualityMilli[anchorIndex]) == qualityMilli") &&
                ContainsAll(resourceRecycler, "internal bool EjectBufferedContents(BaseModule owner, PlayerInventory inventory, IObjectPoolService pool, ref Vector3 dropPosition)", "bool stoppedProcessingAfterSourceEject = false;", "bool allDelivered = true;", "int delivered = DropItemDataQuantity(owner, _bufferItems[i], quantity, inventory, pool, ref dropPosition);", "_bufferQuantities[i] = quantity - safeDelivered;", "_bufferedItemCount = Mathf.Max(0, _bufferedItemCount - safeDelivered);", "allDelivered = false;", "int delivered = DropItemDataQuantity(owner, _activeSourceItem, 1, inventory, pool, ref dropPosition);", "_activeSourceItem = null;", "_isProcessing = false;", "_debugIsProcessing = false;", "stoppedProcessingAfterSourceEject = true;", "int delivered = DropItemDataQuantity(owner, stack.Item, stack.Amount, inventory, pool, ref dropPosition);", "stack.Amount -= Mathf.Max(0, delivered);", "if (allDelivered)", "ClearBufferedInputState();", "ClearPendingOutput();", "if (wasProcessing && (allDelivered || stoppedProcessingAfterSourceEject))", "return allDelivered;", "private void AppendRecyclerBufferedSaveSlot(ref ModuleDTO dto, ItemData item, int quantity)", "dto.recyclerBufferedQuantities[i] += quantity;", "private void ClearBufferedInputState()") &&
                ContainsAll(saveData, "ResourceRecyclerModulePersistenceVersion = 79", "StorageCrateModulePersistenceVersion = 80", "FabricatorPendingOutputPersistenceVersion = 81", "CultivationSeedHashPersistenceVersion = 82", "ProceduralTerrainIdentityPersistenceVersion = 83", "CelestialLightPhasePersistenceVersion = 84", "CurrentVersion = CelestialLightPhasePersistenceVersion", "celestialLightPhaseSerialized", "celestialLightTimeOfDay01", "MaxRecyclerBufferedSlots = 8", "MaxRecyclerPendingYieldSlots = 16", "MaxStorageCrateSlots = 32", "recyclerBufferedSlotCount", "recyclerActiveSourceItemId", "recyclerPendingYieldSlotCount", "fabricatorPendingOutputItemId", "fabricatorPendingOutputQuantity", "fabricatorPendingOutputTotalQuantity", "cultivationSeedItemHashIds", "storageCrateContentsSerialized", "storageCrateSlotCount", "HasRecyclerSaveCapacity()", "HasStorageCrateSaveCapacity()", "HasCultivationSaveCapacity()", "ResolveRecyclerBufferPersistenceSlotCount", "ResolveRecyclerPendingYieldPersistenceSlotCount", "ResolveStorageCratePersistenceSlotCount", "SanitizeRecyclerBufferedQuantitiesCopyOnWrite", "SanitizeRecyclerPendingYieldQuantitiesInPlace", "SanitizeStorageCrateQuantitiesCopyOnWrite", "SanitizeStorageCrateQuantitiesInPlace") &&
                ContainsAll(saveBinaryPayloadCodec, "ResourceRecyclerModuleSaveVersion = SaveData.ResourceRecyclerModulePersistenceVersion", "StorageCrateModuleSaveVersion = SaveData.StorageCrateModulePersistenceVersion", "FabricatorPendingOutputSaveVersion = SaveData.FabricatorPendingOutputPersistenceVersion", "CultivationSeedHashSaveVersion = SaveData.CultivationSeedHashPersistenceVersion", "ProceduralTerrainIdentitySaveVersion = SaveData.ProceduralTerrainIdentityPersistenceVersion", "CelestialLightPhaseSaveVersion = SaveData.CelestialLightPhasePersistenceVersion", "WriteCelestialLightPhase", "ReadCelestialLightPhase", "SanitizeCelestialLightPhase", "WriteProceduralTerrainIdentity", "ReadProceduralTerrainIdentity", "ModuleRecyclerBufferSlotMax = 8", "ModuleRecyclerPendingYieldSlotMax = 16", "ModuleStorageCrateSlotMax = 32", "safeValue.recyclerBufferedSlotCount", "writer.WriteString(safeValue.recyclerActiveSourceItemId)", "safeValue.recyclerPendingYieldItemIds", "WriteCultivationSeedHashArraySlice(", "safeValue.fabricatorPendingOutputItemId", "safeValue.fabricatorPendingOutputQuantity", "safeValue.storageCrateContentsSerialized", "safeValue.storageCrateItemIds", "if (version >= ResourceRecyclerModuleSaveVersion)", "if (version >= StorageCrateModuleSaveVersion)", "if (version >= CultivationSeedHashSaveVersion)", "out value.cultivationSeedItemHashIds", "if (version >= FabricatorPendingOutputSaveVersion", "reader.ReadString(out value.fabricatorPendingOutputItemId)", "reader.ReadString(out value.recyclerActiveSourceItemId)", "reader.ReadBool(out value.storageCrateContentsSerialized)", "value.recyclerActiveSourceItemId = string.Empty;", "value.storageCrateContentsSerialized = false;", "ok = reader.ReadFloat(out value.posX)") &&
                ContainsAll(saveDataMigration, "module.recyclerBufferedItemIds.Length == ModuleDTO.MaxRecyclerBufferedSlots", "module.recyclerPendingYieldQuantities.Length == ModuleDTO.MaxRecyclerPendingYieldSlots", "module.storageCrateItemIds.Length == ModuleDTO.MaxStorageCrateSlots", "module.storageCrateQuantities.Length == ModuleDTO.MaxStorageCrateSlots", "module.cultivationSeedItemHashIds.Length == ModuleDTO.MaxCultivationSlots", "private static bool BackfillCultivationSeedHashIds(ref ModuleDTO module)", "module.cultivationSeedItemHashIds[i] = seedHashId;", "EnsureCelestialLightPhase(data, sourceVersion, steps)", "celestial light phase state repaired", "construction cultivation seed hashes repaired") &&
                ContainsAll(storageCrate, "using Hecton8.SaveSystem;", "using Hecton8.World;", "internal void PopulateSaveData(ref ModuleDTO dto)", "dto.storageCrateContentsSerialized = true;", "EnsureReservationCapacity();", "if (IsReservedSlot(i))", "TryAppendStorageCrateSaveEntry", "internal void RestoreFromSaveData(ModuleDTO dto, ItemCatalog itemCatalog)", "if (!CanResolveStorageCrateRestoreState(in dto, itemCatalog))", "internal void ClearRuntimeContentsForLegacyLoad()", "CountStorageCrateRestoreSlots", "private static bool CanResolveStorageCrateRestoreState(in ModuleDTO dto, ItemCatalog itemCatalog)", "dto.storageCrateItemIds.Length < entryCount", "if (itemCatalog == null || itemCatalog.FindById(itemId.Trim()) == null)", "Mathf.Clamp(", "dto.storageCrateSlotCount", "Mathf.Clamp(dto.storageCrateQuantities", "itemCatalog.FindById(itemId)", "ClearContainedItemsForRestore", "internal bool CanEjectContainedContents(", "Vector3 dropPosition", "PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;", "int persistentWorldDropCandidateCount = 0;", "owner.CanDropItemQuantityToInventoryOrWorld(itemHashId, 1, inventory, pool, dropPosition)", "persistentWorldRegistry.CanRegisterDroppedItem(item, 1, dropPosition)", "persistentWorldDropCandidateCount++;", "persistentWorldRegistry.CanRegisterDroppedItemBatch(persistentWorldDropCandidateCount)", "internal bool EjectContainedContents(", "bool allDelivered = true;", "if (IsReservedSlot(i))", "continue;", "owner.DropItemQuantityToInventoryOrWorld(itemHashId, 1, inventory, pool, ref dropPosition) != 1", "return allDelivered;", "public bool TakeItemToInventory(int itemIndex, PlayerInventory playerInventory)", "if (IsReservedSlot(itemIndex)) return false;", "SetContainedItemHash(itemIndex, null);") &&
                ContainsAll(logisticsPipe, "internal void RestoreFromSaveData(ModuleDTO dto, ItemCatalog itemCatalog)", "float restoredExportTimer = math.clamp(", "bool hasSavedInFlightItem =", "dto.pipeInFlightAmount > 0", "!string.IsNullOrWhiteSpace(dto.pipeInFlightItemId);", "if (!hasSavedInFlightItem)", "ClearInFlightState();", "_exportTimer = restoredExportTimer;", "if (itemCatalog == null)", "ItemData item = itemCatalog.FindById(dto.pipeInFlightItemId);", "if (item == null)", "ClearInFlightState();", "_inFlightItem = item;", "private void ResolveInFlightLossToWorldOrRollback(Vector3 spillPosition)", "if (TrySpillInFlightItemToWorld(spillPosition))", "if (TryReturnCommittedInFlightItemToSource())", "private bool TryReturnCommittedInFlightItemToSource()", "_activeReservationId > 0", "!sourceCrate.HasAutomatedCapacity()", "sourceCrate.TryAddAutomatedItem(_inFlightItem)", "NotifyGridBalanceChanged();") &&
                ContainsAll(constructionManager, "using Hecton8.Crafting;", "using Hecton8.Economy;", "resourceRecycler.PopulateSaveData(ref moduleDto);", "fabricator.PopulateSaveData(ref moduleDto);", "resourceRecycler.RestoreFromSaveData(moduleDto, itemCatalog);", "fabricator.RestoreFromSaveData(moduleDto, itemCatalog);", "cultivationManager.RestoreFromSaveData(moduleDto, itemCatalog);", "storageCrate.PopulateSaveData(ref moduleDto);", "if (!CanResolveConstructionItemReferencesForLoad(in dto, data.version, count, itemCatalog))", "private static bool ModuleRequiresItemCatalogForLoad(in ModuleDTO dto, int version)", "private static bool HasCultivationSeedItemsRequiringCatalog(in ModuleDTO dto, int version)", "private static bool CanResolveCultivationSeedItems(ItemCatalog itemCatalog, in ModuleDTO dto, int version)", "private static bool HasSavedCultivationSeedHashId(in ModuleDTO dto, int slotIndex, int version)", "dto.cultivationSeedItemHashIds[slotIndex] != 0;", "private static bool HasOptionalItemId(string itemId, int quantity)", "return quantity > 0;", "private static bool CanResolveOptionalItemId(ItemCatalog itemCatalog, string itemId, int quantity)", "return quantity <= 0 || (!string.IsNullOrWhiteSpace(itemId) && CanResolveOptionalItemId(itemCatalog, itemId));", "!CanResolveOptionalItemId(itemCatalog, dto.pipeInFlightItemId, dto.pipeInFlightAmount)", "!CanResolveOptionalItemId(itemCatalog, dto.fabricatorPendingOutputItemId, dto.fabricatorPendingOutputQuantity)", "dto.storageCrateContentsSerialized &&", "HasSavedItemArrayEntries(", "dto.storageCrateItemIds", "private static bool CanResolveSavedItemArray(", "if (quantities[i] > 0)", "if (string.IsNullOrWhiteSpace(itemId))", "return false;", "PlayerInventory hostedContentInventory = null;", "module.CanEjectHostedContentsForDeconstruction(hostedContentInventory, pool)", "ExecuteDeconstructionTransaction(", "hostedContentInventory,", "pool,", "int refundCommandCount = counters[DeconstructionRefundCommandCountIndex];", "!module.EjectHostedContentsForDeconstruction(hostedContentInventory, pool)", "int returnedCount = ApplyRefundCommandsOrOverflow(in request, inventory, refundCommandCount, refundCommands, lootCaches, counters);", "int publishedOverflowLootCacheCount = PublishOverflowLootCaches(lootCaches, counters);", "int rejectedOverflowLootCacheCount = math.max(0, overflowLootCacheCount - publishedOverflowLootCacheCount);", "if (rejectedOverflowLootCaches > 0)", "entry.FaultFlags |= HabitatDeconstructionTransactionKernel.FaultRefundOverflow;", "MarkDeconstructionEdgesSevered(targetNodeIndex)", "PublishDeconstructionVfx(in request);", "module.PrepareForDeconstructionPoolReturn();", "data.version >= SaveData.StorageCrateModulePersistenceVersion", "storageCrate.RestoreFromSaveData(moduleDto, itemCatalog);", "storageCrate.ClearRuntimeContentsForLegacyLoad();") &&
                ContainsAll(persistentWorldRegistry, "internal bool CanRegisterDroppedItem(ItemData itemData, int quantity)", "CanRegisterDroppedItemData(itemData, quantity, out string persistentId)", "CanAppendDroppedItemState()", "internal bool CanRegisterDroppedItem(ItemData itemData, int quantity, Vector3 runtimePosition)", "CanResolveDroppedItemRuntimePosition(runtimePosition)", "internal bool CanRegisterDroppedItemBatch(int recordCount)", "public int Count => ReadCount();", "private bool TryRegisterDroppedItemStateful(", "!CanRegisterDroppedItemData(itemData, quantity, out string persistentId)", "!CanAppendDroppedItemState()", "private bool CanAppendDroppedItemState(int recordCount)", "long requiredRecordCount = nextRecordIndex + recordCount;", "CanGenerateDroppedItemInstanceUidBatch(recordCount)", "requiredRecordCount <= _records.Capacity", "(long)_recordsByChunk.Count + recordCount <= _recordsByChunk.Capacity", "(long)_deltaRecords.Length + recordCount <= _deltaRecords.Capacity", "(long)_deltaRecordIndexByEntityId.Count + recordCount <= _deltaRecordIndexByEntityId.Capacity", "(long)_guidToPoolIndex.Count + recordCount <= _guidToPoolIndex.Capacity", "(long)_entityStateByInstanceUid.Count + recordCount <= _entityStateByInstanceUid.Capacity", "private static bool CanGenerateDroppedItemInstanceUidBatch(int recordCount)", "int counterSnapshot = Volatile.Read(ref _nextInstanceUidCounter);", "long requiredSequence = (long)counterSnapshot + recordCount;", "requiredSequence <= InstanceUidCounterMask", "private bool CanResolveDroppedItemScatterEnvelope(Vector3 runtimePosition)", "ResolveScatterPlanarDirection(directionIndex << 29)", "DropScatterMinLiftMeters", "DropScatterMaxLiftMeters", "DropScatterRadiusMeters", "private bool CanResolveDroppedItemRuntimePositionSample(Vector3 runtimePosition)", "AbsoluteUniversePosition.ResolveChunkId(in position, chunkSizeMeters)", "AbsoluteUniversePosition.IsValidChunkId(chunkId)", "internal bool CanRegisterDroppedItem(int itemHashId, ItemCatalog itemCatalog, int quantity)", "return CanRegisterDroppedItem(itemCatalog.FindByHash(itemHashId), quantity);", "internal bool CanRegisterDroppedItem(int itemHashId, ItemCatalog itemCatalog, int quantity, Vector3 runtimePosition)") &&
                ContainsAll(globalRegistryContracts, "bool CanRegisterDroppedItem(ItemData itemData, int quantity, Vector3 runtimePosition);", "bool CanRegisterDroppedItem(int itemHashId, ItemCatalog itemCatalog, int quantity, Vector3 runtimePosition);") &&
                ContainsAll(persistentWorldRegistry, "bool IPersistentDroppedItemRegistry.CanRegisterDroppedItem(ItemData itemData, int quantity, Vector3 runtimePosition)", "bool IPersistentDroppedItemRegistry.CanRegisterDroppedItem(int itemHashId, ItemCatalog itemCatalog, int quantity, Vector3 runtimePosition)") &&
                ContainsAll(harvestableOutcrop, "if (!CanDispatchYield(toolPower, hitPoint))", "_currentHealth = math.max(_currentHealth, MinimumToolPower);", "_isBroken = true;", "private bool CanDispatchYield(float toolPower, Vector3 dropPoint)", "playerInventory.CanAcceptItemQuantity(itemHashId, quantity)", "registry.CanRegisterDroppedItem(item, quantity, dropPoint)", "private static void ReportYieldDeliveryBlocked(int itemHashId, int quantity)", "GlobalTelemetryBus.PublishPerformanceWarning(") &&
                ContainsAll(baseModule, "using Hecton8.Economy;", "using Hecton8.Crafting;", "internal bool CanEjectHostedContentsForDeconstruction(PlayerInventory playerInventory, IObjectPoolService pool)", "ResolveHostedContentsDropPosition()", "private bool CanEjectHostedModuleContents(PlayerInventory playerInventory, IObjectPoolService pool, Vector3 dropPosition)", "internal bool CanDropItemQuantityToInventoryOrWorld(", "Vector3 dropPosition", "playerInventory.CanAcceptItemQuantity(itemHashId, quantity)", "if (!IsFiniteRuntimePosition(dropPosition))", "PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;", "persistentWorldRegistry.CanRegisterDroppedItem(itemData, quantity, dropPosition)", "return true;", "return false;", "internal bool CanSpawnPooledWorldItemFallback(", "itemCatalog.FindByHash(itemHashId) != null", "worldItemPrefab.TryGetComponent(out HectonItem _)", "internal int DropItemQuantityToInventoryOrWorld(", "int remainingQuantity = quantity - delivered;", "TryRegisterPersistentDroppedItemQuantity(itemHashId, remainingQuantity, dropPosition, playerInventory)", "delivered += remainingQuantity;", "private bool TryRegisterPersistentDroppedItemQuantity(", "persistentWorldRegistry.TryRegisterDroppedItem(itemHashId, itemCatalog, quantity, position)", "private bool SpawnPooledWorldItem(", "if (!IsFiniteRuntimePosition(position))", "private bool EjectHostedModuleContents(PlayerInventory playerInventory, IObjectPoolService pool, ref Vector3 dropPosition)", "allDelivered &= sorterModule.EjectBufferedContents(this, playerInventory, pool, ref dropPosition);", "allDelivered &= recyclerModule.EjectBufferedContents(this, playerInventory, pool, ref dropPosition);", "allDelivered &= fabricator.EjectPendingCraftOutput(playerInventory, ref dropPosition);", "allDelivered &= cultivationManager.EjectCultivationContents(this, playerInventory, ref dropPosition);", "allDelivered &= storageCrate.EjectContainedContents(this, playerInventory, pool, ref dropPosition);", "return allDelivered;") &&
                ContainsAll(modRuntimeState, "private struct RuntimeBiomeMutationRegistration", "internal static void UnregisterModBiomeMutations(string modId)", "TryFindMatchingDefinition(definition, out existingMutationIndex)", "PromoteRuntimeMutationOwnerIfUnownedOrSameMod(existingMutationIndex);", "private static bool TryFindMatchingDefinition(FaunaBiomeMutationDefinition definition, out int index)", "private static void PromoteRuntimeMutationOwnerIfUnownedOrSameMod(int index)", "private static void RemoveStaleOwnerMutations()", "MaxRuntimeMutationCount", "return _runtimeMutations[index].Data;", "FaunaBiomeMutationDefinition existing = _runtimeMutations[i].Data;", "_runtimeMutations[index] = registration;") &&
                ContainsAll(faunaGeneticsManager, "for (int i = 0; i < ModEcosystemRegistry.Count; i++)", "FaunaBiomeMutationDefinition definition = ModEcosystemRegistry.GetAt(i);", "scale *= overlayScale;", "speed *= definition.SpeedMultiplier;", "health *= definition.HealthMultiplier;") &&
                ContainsAll(modMenuController, "private bool _modRegistryEventRegistered;", "TryRegisterModRegistryListener();", "public void RefreshView()", "_modRegistryEventRegistered = ModRegistryEvents.Register(GetModRegistryEventAdapter());", "if (_modRegistryEventRegistered && _modRegistryEventAdapter != null)", "_modRegistryEventRegistered = false;") &&
                ContainsAll(fabricator, "private bool _modRegistryEventRegistered;", "private void EnsureRecipeCache()", "TryRegisterModRegistryListener();", "_modRegistryEventRegistered = ModRegistryEvents.Register(GetModRegistryEventAdapter());", "if (_modRegistryEventRegistered && _modRegistryEventAdapter != null)", "_modRegistryEventRegistered = false;") &&
                ContainsAll(modLoader, "ModAssetManager.UnregisterBundlePath(candidate.Metadata.Id);", "ModResourceRegistry.UnregisterModResources(candidate.Metadata.Id);", "ModSettingsRegistry.UnregisterModSettings(candidate.Metadata.Id);", "ModItemRegistry.UnregisterModItems(candidate.Metadata.Id);", "ModRecipeRegistry.UnregisterModRecipes(candidate.Metadata.Id);", "ModRecycleRegistry.UnregisterModRecycleYields(candidate.Metadata.Id);", "ModEcosystemRegistry.UnregisterModBiomeMutations(candidate.Metadata.Id);", "ModBuildableRegistry.UnregisterModBuildables(candidate.Metadata.Id);", "ModAssetManager.UnregisterBundlePath(modId);", "ModResourceRegistry.UnregisterModResources(modId);", "ModSettingsRegistry.UnregisterModSettings(modId);", "ModItemRegistry.UnregisterModItems(modId);", "ModRecipeRegistry.UnregisterModRecipes(modId);", "ModRecycleRegistry.UnregisterModRecycleYields(modId);", "ModEcosystemRegistry.UnregisterModBiomeMutations(modId);", "ModBuildableRegistry.UnregisterModBuildables(modId);", "ModAssetManager.UnregisterBundlePath(_loadedMods[i].Metadata.Id);", "ModResourceRegistry.UnregisterModResources(_loadedMods[i].Metadata.Id);", "ModSettingsRegistry.UnregisterModSettings(_loadedMods[i].Metadata.Id);", "ModItemRegistry.UnregisterModItems(_loadedMods[i].Metadata.Id);", "ModRecipeRegistry.UnregisterModRecipes(_loadedMods[i].Metadata.Id);", "ModRecycleRegistry.UnregisterModRecycleYields(_loadedMods[i].Metadata.Id);", "ModEcosystemRegistry.UnregisterModBiomeMutations(_loadedMods[i].Metadata.Id);", "ModBuildableRegistry.UnregisterModBuildables(_loadedMods[i].Metadata.Id);") &&
                ContainsAll(modLoader, "if (!ExecuteModCallback(loadedMod.Metadata.Id, loadedMod.Instance.OnLoad, \"OnLoad\"))", "ModCommandDispatcher.UnregisterMod(loadedMod.Metadata.Id);", "DisableCandidate(candidate, \"OnLoad failed.\");", "_loadedMods.Add(loadedMod);");

            bool modSaveStateMmfRollbackPass =
                ContainsAll(modRuntimeState, "_mmfLoadRollbackData", "CaptureMmfLoadRollbackSnapshot();", "private static void RestoreMmfLoadRollbackSnapshot()", "private static void RebuildCustomModIndex()") &&
                ContainsAll(modRuntimeState, "private static void AddOrReplaceLoadedSaveEntry(", "AddOrReplaceLoadedSaveEntry(", "_customModData[existingIndex] = new ModSaveEntry", "_customModIndexByHash[compoundHash] = _customModData.Count;") &&
                ContainsAll(modRuntimeState, "bool keepLoadedPayloads = false;", "if (!loaded)", "RestoreMmfLoadRollbackSnapshot();", "return false;", "keepLoadedPayloads = true;", "catch", "throw;") &&
                ContainsAll(modRuntimeState, "DisposeTempNativeArrayBuffer(ref payloadBytes, ModPayloadReadBufferLabel);", "if (keepLoadedPayloads)", "DiscardMmfLoadRollbackSnapshot();") &&
                ContainsAll(modRuntimeState, "_customModData.Add(_mmfLoadRollbackData[i]);", "_customModIndexByHash[compoundHash] = i;") &&
                ContainsAll(modRuntimeState, "private static string BuildModPayloadTempOverridePath(", "string tempOverridePath = BuildModPayloadTempOverridePath(absoluteSavePath, entry.ModHash, entry.KeyHash);", "if (payloadLength > SaveBinaryStorage.ModPayloadMaxBytes)", "SaveBinaryStorage.TryCommitModPayloadSubSector(") &&
                ContainsAll(saveBinaryStorage, "if (!TryDeleteFileIfExists(tempOverridePath, out string staleTempDeleteError))", "if (!AsyncWriteManager.WriteAll(tempOverridePath, filePtr, fileCursor, out error))", "if (!AsyncWriteManager.FlushCriticalSavePath(tempOverridePath, fileCursor, out error))", "if (TryCommitIndexedPersistentWorldSectorOverride(absoluteSavePath, tempOverridePath, out error))", "_ = TryDeleteFileIfExists(tempOverridePath, out _);") &&
                ContainsAll(saveManager, "ModPayloadLoadFallbackTelemetryHash = 0x4D504C46u", "private static void ReportModPayloadLoadFailure(string slotName, string error)", "string message = string.IsNullOrEmpty(error)", "PublishPerformanceWarningBestEffort(ModPayloadLoadFallbackTelemetryHash, ComputeSlotHash(slotName), 1f);", "LogWarning($\"[SaveManager] Mod payload load warning for '{slotName}': {message}\");") &&
                ContainsAll(saveManager, "if (!ModSaveStateStore.TryCommitMmfPayloads(absoluteTempPath, out string modPayloadCommitError) ||", "ReportModPayloadCommitFailure(slotName, modPayloadCommitError);", "error = string.IsNullOrEmpty(modPayloadCommitError)", "return false;", "return TryCommitTempSaveToPrimary(slotName, tempPath, finalPath, backupRetentionCount, out error);");

            bool eventBusThrowableAllocationTelemetryPass =
                ContainsAll(hectonEventBus, "MaxEventDispatchDepth = 5", "GlobalTelemetryBus.PublishCatastrophicCascadePrevented") &&
                ContainsAll(hectonEventBus, "GC.GetAllocatedBytesForCurrentThread() - allocationBefore", "ModCommandDispatcher.ReportModManagedAllocation(entry.SubscriberHash, allocationDelta);", "ModCallbackExceptionDisableReason") &&
                CountOccurrences(hectonEventBus, "ModLoader.DisableManagedMod(entry.SubscriberId, ModCallbackExceptionDisableReason);") == 3 &&
                SourceIndex(hectonEventBus, "ex.Message") == int.MaxValue;

            bool sceneActivationContractPass =
                ContainsAll(sceneRuntimeService, "TransitionDissolveSeconds = 3f", "_pendingSceneLoadOperation.allowSceneActivation = false", "ReleaseSceneActivation(_pendingSceneLoadOperation);") &&
                ContainsAll(sceneRuntimeService, "HasMainMenuDissolveReachedActivationTime(_cinematicTransitionElapsed)", "return elapsedSeconds == TransitionDissolveSeconds;") &&
                ContainsAll(sceneRuntimeService, "IServiceShutdown", "ShutdownServiceState()", "_sceneActivationReleased = false;") &&
                ContainsAll(mainMenuController, "SceneRuntimeService.EnsureRuntimeInstance", "ResolveStartSceneName(isNewGame)", "sceneService.LoadScene(sceneName);", "newGameTargetSceneName = DefaultGameplaySceneName") &&
                ContainsAll(pauseMenuController, "SceneRuntimeService.EnsureRuntimeInstance", "ResolveMainMenuSceneName(mainMenuSceneName)", "sceneService.LoadScene(resolvedMainMenuSceneName);") &&
                SourceIndex(sceneRuntimeService, "_pendingSceneLoadOperation.allowSceneActivation = true") == int.MaxValue &&
                SourceIndex(sceneRuntimeService, "_cinematicTransitionElapsed >= TransitionDissolveSeconds") == int.MaxValue &&
                SourceIndex(mainMenuController, "allowSceneActivation") == int.MaxValue &&
                SourceIndex(mainMenuController, "SceneManager.LoadSceneAsync") == int.MaxValue &&
                SourceIndex(pauseMenuController, "allowSceneActivation") == int.MaxValue &&
                SourceIndex(pauseMenuController, "SceneManager.LoadSceneAsync") == int.MaxValue;

            bool playModeSentinelAsyncIoPass =
                ContainsAll(playModeSentinel, "TryWriteAutoRunFlagAsync", "FileOptions.Asynchronous | FileOptions.WriteThrough", "await stream.WriteAsync(AutoRunFlagPayload, 0, AutoRunFlagPayload.Length);", "await stream.FlushAsync();") &&
                ContainsAll(playModeSentinel, "RequestMetricsWriteAndCleanup", "WriteMetricsAndCleanupAsync", "await WriteMetricsAsync();", "Metrics pipeline failed before cleanup", "CleanupAndExitIfBatch();");

            bool saveSlotPathGuardPass =
                ContainsAll(saveManager, "MaxSaveSlotNameLength = 48", "InvalidSlotNameReason = \"Invalid save slot name.\"", "InvalidSlotFileStem = \"slot_invalid\"") &&
                ContainsAll(saveManager, "TryResolveSafeSlotName", "ResolveSafeSlotFileStem", "IsReservedManualSlotPattern", "StringComparison.OrdinalIgnoreCase", "!SaveEvents.IsKnownManualSlotName(slotName)", "safeSlotName = slotName;") &&
                ContainsAll(saveManager, "SaveEvents.TryRaiseSaveFailed(0u, SaveEvents.ComputeMessageHash(InvalidSlotNameReason), InvalidSlotNameReason);", "SaveEvents.TryRaiseLoadFailed(0u, SaveEvents.ComputeMessageHash(InvalidSlotNameReason), InvalidSlotNameReason);") &&
                ContainsAll(saveManager, "public Awaitable SaveGameAsync(string slotName)", "private async Awaitable SaveGameAsyncInternal(string slotName, byte slotIndex, uint operationId)", "PublishSaveStatus(slotIndex, operationId, SaveStatusSignal.Rejected, 0f, 1u);", "PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Rejected, 0f, 1u);") &&
                CountOccurrences(saveManager, "PublishSaveStatus(slotIndex, operationId, SaveStatusSignal.Rejected, 0f, 1u);") +
                CountOccurrences(saveManager, "PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Rejected, 0f, 1u);") >= 6 &&
                ContainsAll(saveManager, "LastOperationSlot = slotIndex < SaveEvents.ManualSlotCount", "SaveEvents.ResolveManualSlotName(slotIndex)", "ResolveUnavailableSlotContext(slotName, slotIndex, out string unavailableSlotName)", "ResolveUnavailableSlotContext(slotName, byte.MaxValue, out string unavailableSlotName)", "LastOperationSlot = unavailableSlotName;", "SaveEvents.TryRaiseLoadFailed(unavailableSlotHash") &&
                ContainsAll(saveManager, "GetPrimarySaveFilePath(string slotName) => $\"{ResolveSafeSlotFileStem(slotName)}.sav\"", "GetTempSaveFilePath(string slotName) => $\"{ResolveSafeSlotFileStem(slotName)}.sav.tmp\"", "GetDiagnosticSaveFilePath(string slotName) => $\"{ResolveSafeSlotFileStem(slotName)}.diag\"") &&
                ContainsAll(saveManager, "return TryResolveSafeSlotName(slotName, out slotName);", "BuildSaveSlotInfoInternal(string slotName)") &&
                CountOccurrences(saveManager, "if (!TryResolveSafeSlotName(slotName, out slotName))") >= 7 &&
                ContainsAll(saveSlotMaintenanceRecord, "SaveManager.GetDiagnosticSaveFilePath(slotName)", "SaveManager.TryResolveSafeSlotName(SlotName, out string safeSlotName)", "SaveManager.TryResolveSafeSlotName(slotName, out string safeSlotName)", "SaveSlotInfo.ToStorageString(SaveSlotIntegrityState.Empty)") &&
                ContainsAll(saveStation, "SaveManager.IsSafeSlotName(_saveSlot)", "Save slot rejected by SaveManager slot-name guard.", "if (saveService == null || !saveService.IsInitialized)", "Debug.LogError(\"[SaveStation] Save service is unavailable.\", this);") &&
                ContainsAll(saveStation, "IInteractionStartedEventOwner", "TryRequestManualSlotSave(saveService, interactor)", "saveService as IAsyncPersistenceService", "SaveEvents.ResolveKnownSlotIndex(_saveSlot)", "bool accepted = asyncPersistence.TryRequestSave((byte)slotIndex, SaveStationSourceHash);", "if (!accepted)", "return true;", "PlayInteractionSound();") &&
                ContainsAll(interactableContract, "public interface IInteractionStartedEventOwner", "PlayerInteraction skips its default attempt", "generic confirm feedback") &&
                ContainsAll(playerInteraction, "IInteractable target = _currentHovered;", "TryRaiseDefaultInteractionStarted(target, transform)", "QueueDefaultInteractionFeedback(target)", "private static bool TryRaiseDefaultInteractionStarted(IInteractable target, Transform interactor)", "private void QueueDefaultInteractionFeedback(IInteractable target)", "private static bool IsInteractionStartedEventOwner(IInteractable target)", "return target is IInteractionStartedEventOwner;", "return InteractionEvents.TryRaiseInteractionStarted(target, interactor);") &&
                ContainsAll(fabricator, "IInteractionStartedEventOwner", "InteractionEvents.TryRaiseInteractionStarted(this, interactor);") &&
                ContainsAll(emergencyServiceRelay, "IInteractionStartedEventOwner", "InteractionEvents.TryRaiseInteractionStarted(this, interactor);") &&
                SourceIndex(saveStation, "ShowManualSaveRequestRejected") == int.MaxValue &&
                SourceIndex(saveStation, "Save request rejected by async persistence lane.") == int.MaxValue &&
                SourceIndex(saveStation, "#if UNITY_EDITOR || DEVELOPMENT_BUILD") < SourceIndex(saveStation, "Debug.LogError(\"[SaveStation] Save service is unavailable.\", this);") &&
                SourceIndex(saveManager, "slotName.Trim(") == int.MaxValue &&
                SourceIndex(saveManager, "$\"{slotName}.sav\"") == int.MaxValue &&
                SourceIndex(saveManager, "$\"{slotName}.sav.tmp\"") == int.MaxValue &&
                SourceIndex(saveManager, "$\"{slotName}.diag\"") == int.MaxValue &&
                SourceIndex(saveSlotMaintenanceRecord, "$\"{slotName}.diag\"") == int.MaxValue &&
                SourceIndex(saveSlotMaintenanceRecord, "SaveSlotIntegrityState.Empty.ToString()") == int.MaxValue;

            bool interactionEventRuntimeBridgePass =
                ContainsAll(interactionEvents, "InteractionQueueInitializationFailureWarningHash", "InteractionQueueReleaseFailureWarningHash") &&
                ContainsAll(interactionEvents, "internal static void PrewarmCold()", "catch (Exception exception)", "ReportQueueInitializationFailure((ushort)InteractionEventType.InteractionStarted, exception);") &&
                ContainsAll(interactionEvents, "private static bool Enqueue(in InteractionEventPayload payload)", "ReleaseReferenceSlotForPayload(in payload);", "ReportQueueInitializationFailure(payload.EventType, exception);", "return false;") &&
                ContainsAll(interactionEvents, "private static readonly ushort[] _referenceSlotGenerations", "private static bool IsReferenceSlotPayloadCurrent(in InteractionEventPayload payload)", "payload.Reserved != 0", "_referenceSlotGenerations[referenceSlot] == payload.Reserved") &&
                ContainsAll(interactionEvents, "out ushort referenceGeneration", "referenceGeneration = AdvanceReferenceSlotGeneration(referenceSlot)", "Reserved = referenceGeneration") &&
                ContainsAll(interactionEvents, "internal static void ResetStaticState()", "Exception releaseException = ReleaseNativeQueuesBestEffort();", "ReportQueueReleaseFailure(releaseException);") &&
                ContainsAll(interactionEvents, "private static Exception ReleaseNativeQueuesBestEffort()", "ReleaseNativeQueueBestEffort(ref _pendingEvents, ref _pendingEventsSentinelId, ref firstException);", "ReleaseNativeQueueBestEffort(ref _nextFrameEvents, ref _nextFrameEventsSentinelId, ref firstException);") &&
                ContainsAll(interactionEvents, "private static void ReleaseNativeQueue<T>(ref NativeQueue<T> queue, ref int sentinelId)", "Exception firstException = null;", "if (sentinelId > 0)", "NativeMemorySentinel.Unregister(sentinelId);", "finally", "sentinelId = 0;", "if (queue.IsCreated)", "queue.Dispose();", "queue = default;", "if (firstException != null)", "throw firstException;") &&
                ContainsAll(interactionEvents, "private static void PublishPerformanceWarningBestEffort(uint warningHash, uint contextHash, float value)", "GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, value);", "catch (Exception telemetryException)", "LogQueueInitializationException(telemetryException);") &&
                ContainsAll(interactionEvents, "private static void ReportQueueReleaseFailure(Exception exception)", "InteractionQueueReleaseFailureWarningHash", "PublishPerformanceWarningBestEffort(", "LogQueueInitializationException(exception);") &&
                ContainsAll(ReadProjectFile("Assets/_Project/Tests/Editor/InteractionEventsBackpressureEditTests.cs"), "QueueBackpressureReleasesRejectedReferenceSlot", "FlushPendingContinuesAndReleasesReferenceSlotWhenListenerThrows", "ResetStaticStateReleasesNativeQueuesBestEffortBeforeClearingState", "ReleasedPayloadDoesNotResolveAfterReferenceSlotReuse", "InteractionEventPayloadLayoutKeepsReservedGenerationSlot") &&
                SourceIndex(interactionEvents, "queueReleased") == int.MaxValue &&
                SourceIndex(interactionEvents, "NativeMemorySentinel.Unregister(sentinelId);") < SourceIndex(interactionEvents, "queue.Dispose();");

            bool saveThumbnailSidecarGuardPass =
                ContainsAll(thumbnailSystem, "ResolveThumbnailFileStem", "SaveManager.ResolveSafeSlotFileStem(slotName)", "HectonPersistentPathPolicy.CombineFile(ResolveThumbnailFileStem(slotName) + Extension)", "HectonPersistentPathPolicy.CombineFile(ResolveThumbnailFileStem(slotName) + LegacyExtension)") &&
                CountOccurrences(thumbnailSystem, "SaveManager.TryResolveSafeSlotName(slotName, out slotName)") >= 4 &&
                ContainsAll(thumbnailSystem, "AsyncWriteManager.WriteAll(tempPath, dataPtr, encodedJpg.Length, out string writeError)", "throw new IOException(writeError);", "int encodedJpgSentinelId = 0", "encodedJpgSentinelId = NativeMemorySentinel.RegisterNativeArray(", "encodedJpg.Dispose();", "NativeMemorySentinel.Unregister(encodedJpgSentinelId);", "encodedJpgSentinelId = 0;", "AsyncWriteManager.TryGetFileLength(tempPath, out long tempThumbnailBytes, out string tempLengthError)", "tempThumbnailBytes != encodedByteLength", "AsyncWriteManager.FlushCriticalSavePath(tempPath, tempThumbnailBytes, out string tempFlushError)", "File.Replace(tempPath, path, null)", "File.Move(tempPath, path);", "AsyncWriteManager.TryGetFileLength(path, out long persistedThumbnailBytes, out string lengthError)", "persistedThumbnailBytes != encodedByteLength", "AsyncWriteManager.FlushCriticalSavePath(path, persistedThumbnailBytes, out string flushError)", "await Awaitable.MainThreadAsync();", "ClearCacheEntry(slotName);") &&
                SourceIndex(thumbnailSystem, "NativeMemorySentinel.Unregister(encodedJpgSentinelId);") < SourceIndex(thumbnailSystem, "encodedJpgSentinelId = 0;") &&
                SourceIndex(thumbnailSystem, "NativeMemorySentinel.Unregister(encodedJpgSentinelId);") < SourceIndex(thumbnailSystem, "encodedJpg.Dispose();") &&
                CountOccurrences(thumbnailSystem, "AsyncWriteManager.InvalidateCachedReadWindows(path);") >= 2 &&
                ContainsAll(saveSidecarStorage, "NativeArrayOwnerSystem = SystemID.SavePersistence", "H8Memory.Allocate<byte>(", "Allocator.Temp", "if (!buffer.IsCreated || buffer.Length != length)", "H8Memory.Release(ref buffer, NativeArrayOwnerSystem);", "if (buffer.IsCreated)", "NativeMemoryReleaseFailureMessage") &&
                ContainsAll(saveSidecarStorage, "private static bool WriteSidecarAtomically(string absolutePath, void* bufferPtr, int byteCount, string sidecarName, out string error)", "AsyncWriteManager.WriteAll(tempPath, bufferPtr, byteCount, out error)", "AsyncWriteManager.TryGetFileLength(tempPath, out long tempBytes, out string lengthError)", "AsyncWriteManager.FlushCriticalSavePath(tempPath, tempBytes, out string flushError)", "File.Replace(tempPath, absolutePath, null)", "File.Move(tempPath, absolutePath);", "AsyncWriteManager.TryGetFileLength(absolutePath, out long promotedBytes, out lengthError)", "AsyncWriteManager.FlushCriticalSavePath(absolutePath, promotedBytes, out flushError)") &&
                CountOccurrences(saveSidecarStorage, "AsyncWriteManager.InvalidateCachedReadWindows(absolutePath);") >= 2 &&
                ContainsAll(saveSlotThumbnail, "SaveManager.IsSafeSlotName(slotName)", "SaveThumbnailSystem.CaptureThumbnail(slotName, captureCamera);", "SaveThumbnailSystem.LoadThumbnailTextureAsync(slotName, destroyCancellationToken)", "AdvanceLoadSequence()") &&
                SourceIndex(thumbnailSystem, "slotName + Extension") == int.MaxValue &&
                SourceIndex(thumbnailSystem, "slotName + LegacyExtension") == int.MaxValue &&
                SourceIndex(thumbnailSystem, "new FileStream(tempPath") == int.MaxValue &&
                CountOccurrences(saveSidecarStorage, "DisposeTempNativeArrayBuffer(ref buffer,") == 4 &&
                CountOccurrences(saveSidecarStorage, "H8Memory.Release(ref buffer, NativeArrayOwnerSystem);") == 1;

            bool pass = asyncThumbnailPass &&
                        loadingStagePass &&
                        safeAupSnapPass &&
                        savingHudPass &&
                        savingHudShaderPulsePass &&
                        corruptionDialogPass &&
                        seedConsistencyPass &&
                        inventoryFullWritePass &&
                        portableFileStreamWritePass &&
                        inventoryShadowBufferPass &&
                        tombstoneLoadOrderPass &&
                        modPayloadSidecarPass &&
                        hydrationTimeSlicePass &&
                        hydrationGcPurgePass &&
                        registryMigrationRsqrtPass &&
                        deterministicScatterCheapRadiusPass &&
                        asyncDehydrationPipelinePass &&
                        writeAllBytesPurgedPass &&
                        saveEventOverflowTelemetryPass &&
                        saveEventDispatchMutationPass &&
                        saveLoadFailureUiBridgePass &&
                        modWorldLoadRollbackPass &&
                        modWorldObjectPoolRetryPass &&
                        modWorldRegistrySceneRetryPass &&
                        modRegistryOverflowRetryPass &&
                        modWorldStaleHandleRestorePass &&
                        modWorldSpawnHashSanitizerPass &&
                        modAssetBindingLifecyclePass &&
                        modSaveStateMmfRollbackPass &&
                        eventBusThrowableAllocationTelemetryPass &&
                        sceneActivationContractPass &&
                        playModeSentinelAsyncIoPass &&
                        saveSlotPathGuardPass &&
                        interactionEventRuntimeBridgePass &&
                        saveThumbnailSidecarGuardPass;

            WriteArtifact(
                pass,
                asyncThumbnailPass,
                loadingStagePass,
                safeAupSnapPass,
                savingHudPass,
                savingHudShaderPulsePass,
                corruptionDialogPass,
                seedConsistencyPass,
                inventoryFullWritePass,
                portableFileStreamWritePass,
                inventoryShadowBufferPass,
                tombstoneLoadOrderPass,
                modPayloadSidecarPass,
                hydrationTimeSlicePass,
                hydrationGcPurgePass,
                registryMigrationRsqrtPass,
                deterministicScatterCheapRadiusPass,
                asyncDehydrationPipelinePass,
                writeAllBytesPurgedPass,
                saveEventOverflowTelemetryPass,
                saveEventDispatchMutationPass,
                saveLoadFailureUiBridgePass,
                modWorldLoadRollbackPass,
                modWorldObjectPoolRetryPass,
                modWorldRegistrySceneRetryPass,
                modRegistryOverflowRetryPass,
                modWorldStaleHandleRestorePass,
                modWorldSpawnHashSanitizerPass,
                modAssetBindingLifecyclePass,
                modSaveStateMmfRollbackPass,
                eventBusThrowableAllocationTelemetryPass,
                sceneActivationContractPass,
                playModeSentinelAsyncIoPass,
                saveSlotPathGuardPass,
                interactionEventRuntimeBridgePass,
                saveThumbnailSidecarGuardPass,
                rewrittenOffset,
                rewrittenLength);

            if (pass)
                Debug.Log("[PersistenceUxSmokeTester] PASS artifact=" + ArtifactRelativePath);
            else
                Debug.LogError("[PersistenceUxSmokeTester] FAIL artifact=" + ArtifactRelativePath);

            return pass;
        }

        private static bool RunInventoryFullWriteFileStreamAssert(out int rewrittenOffset, out int rewrittenLength)
        {
            byte[] before = new byte[SectorSizeBytes]; // COLD ALLOC: byte[16KB] — editor-only inventory full-write sector fixture — owner: PersistenceUxSmokeTester
            byte[] after = new byte[SectorSizeBytes]; // COLD ALLOC: byte[16KB] — editor-only inventory full-write sector fixture — owner: PersistenceUxSmokeTester
            byte[] observed = new byte[SectorSizeBytes]; // COLD ALLOC: byte[16KB] — editor-only FileStream full-write verification readback — owner: PersistenceUxSmokeTester
            for (int slot = 0; slot < InventorySlotCount; slot++)
            {
                int offset = slot * InventorySlotStrideBytes;
                WriteInventorySlot(before, offset, unchecked((uint)(0xA0000000u + slot)), (ushort)(slot + 1), (ushort)0);
                WriteInventorySlot(after, offset, unchecked((uint)(0xA0000000u + slot)), (ushort)(slot + 1), (ushort)0);
            }

            int changedSlot = 17;
            int changedSlotOffset = changedSlot * InventorySlotStrideBytes;
            WriteInventorySlot(after, changedSlotOffset, 0u, (ushort)0, (ushort)1);

            string filePath = Path.Combine(System.Environment.CurrentDirectory, InventoryFullWriteFileRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            WriteBytes(filePath, before);
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                stream.Position = 0L;
                stream.Write(after, 0, SectorSizeBytes);
                stream.Flush(true);
            }

            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int read = stream.Read(observed, 0, observed.Length);
                if (read != observed.Length)
                {
                    rewrittenOffset = -1;
                    rewrittenLength = 0;
                    return false;
                }
            }

            int changedOffset = -1;
            int lastChangedOffset = -1;
            for (int i = 0; i < observed.Length; i++)
            {
                if (before[i] == observed[i])
                    continue;

                if (changedOffset < 0)
                    changedOffset = i;
                lastChangedOffset = i;
            }

            int changedLength = changedOffset >= 0 ? lastChangedOffset - changedOffset + 1 : 0;
            rewrittenOffset = 0;
            rewrittenLength = SectorSizeBytes;
            return changedOffset >= changedSlotOffset &&
                   changedOffset + changedLength <= changedSlotOffset + InventorySlotStrideBytes &&
                   changedLength > 0 &&
                   changedLength < InventorySlotStrideBytes &&
                   observed[changedSlotOffset] == after[changedSlotOffset] &&
                   observed[changedSlotOffset + 6] == after[changedSlotOffset + 6];
        }

        private static void WriteInventorySlot(byte[] bytes, int offset, uint itemHash, ushort stackCount, ushort flags)
        {
            bytes[offset + 0] = (byte)itemHash;
            bytes[offset + 1] = (byte)(itemHash >> 8);
            bytes[offset + 2] = (byte)(itemHash >> 16);
            bytes[offset + 3] = (byte)(itemHash >> 24);
            bytes[offset + 4] = (byte)stackCount;
            bytes[offset + 5] = (byte)(stackCount >> 8);
            bytes[offset + 6] = (byte)flags;
            bytes[offset + 7] = (byte)(flags >> 8);
        }

        private static int SourceIndex(string source, string value)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value))
                return int.MaxValue;

            int index = source.IndexOf(value, StringComparison.Ordinal);
            return index < 0 ? int.MaxValue : index;
        }

        private static int CountOccurrences(string source, string value)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value))
                return 0;

            int count = 0;
            int searchIndex = 0;
            while (searchIndex < source.Length)
            {
                int foundIndex = source.IndexOf(value, searchIndex, StringComparison.Ordinal);
                if (foundIndex < 0)
                    break;

                count++;
                searchIndex = foundIndex + value.Length;
            }

            return count;
        }

        private static bool ContainsAll(string source, params string[] values)
        {
            if (string.IsNullOrEmpty(source) || values == null)
                return false;

            for (int i = 0; i < values.Length; i++)
            {
                if (source.IndexOf(values[i], StringComparison.Ordinal) < 0)
                    return false;
            }

            return true;
        }

        private static string ReadProjectFile(string relativePath)
        {
            string path = Path.Combine(System.Environment.CurrentDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }

        private static bool ProjectRuntimeSourceContains(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            string sourceRoot = Path.Combine(System.Environment.CurrentDirectory, "Assets/_Project/Scripts");
            if (!Directory.Exists(sourceRoot))
                return false;

            foreach (string file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (IsEditorSourcePath(file))
                    continue;

                if (File.ReadAllText(file).IndexOf(value, StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }

        private static bool EveryRootLocalizationJsonContains(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            string sourceRoot = Path.Combine(System.Environment.CurrentDirectory, "Assets/_Project/Scripts");
            if (!Directory.Exists(sourceRoot))
                return false;

            int checkedFileCount = 0;
            foreach (string file in Directory.EnumerateFiles(sourceRoot, "*.json", SearchOption.TopDirectoryOnly))
            {
                checkedFileCount++;
                if (File.ReadAllText(file).IndexOf(value, StringComparison.Ordinal) < 0)
                    return false;
            }

            return checkedFileCount >= 17;
        }

        private static bool IsEditorSourcePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            string normalizedPath = path.Replace('\\', '/');
            return normalizedPath.IndexOf("/Editor/", StringComparison.Ordinal) >= 0;
        }

        private static void WriteBytes(string path, byte[] bytes)
        {
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }

        private static void WriteArtifact(
            bool pass,
            bool asyncThumbnailPass,
            bool loadingStagePass,
            bool safeAupSnapPass,
            bool savingHudPass,
            bool savingHudShaderPulsePass,
            bool corruptionDialogPass,
            bool seedConsistencyPass,
            bool inventoryFullWritePass,
            bool portableFileStreamWritePass,
            bool inventoryShadowBufferPass,
            bool tombstoneLoadOrderPass,
            bool modPayloadSidecarPass,
            bool hydrationTimeSlicePass,
            bool hydrationGcPurgePass,
            bool registryMigrationRsqrtPass,
            bool deterministicScatterCheapRadiusPass,
            bool asyncDehydrationPipelinePass,
            bool writeAllBytesPurgedPass,
            bool saveEventOverflowTelemetryPass,
            bool saveEventDispatchMutationPass,
            bool saveLoadFailureUiBridgePass,
            bool modWorldLoadRollbackPass,
            bool modWorldObjectPoolRetryPass,
            bool modWorldRegistrySceneRetryPass,
            bool modRegistryOverflowRetryPass,
            bool modWorldStaleHandleRestorePass,
            bool modWorldSpawnHashSanitizerPass,
            bool modAssetBindingLifecyclePass,
            bool modSaveStateMmfRollbackPass,
            bool eventBusThrowableAllocationTelemetryPass,
            bool sceneActivationContractPass,
            bool playModeSentinelAsyncIoPass,
            bool saveSlotPathGuardPass,
            bool interactionEventRuntimeBridgePass,
            bool saveThumbnailSidecarGuardPass,
            int inventoryRewriteOffset,
            int inventoryRewriteLength)
        {
            string artifactPath = Path.Combine(System.Environment.CurrentDirectory, ArtifactRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(artifactPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            StringBuilder builder = new StringBuilder(1792); // COLD ALLOC: StringBuilder[1792] — editor smoke JSON artifact — owner: PersistenceUxSmokeTester
            builder.Append('{')
                .Append("\"tester\":\"PersistenceUxSmokeTester\",")
                .Append("\"pass\":").Append(pass ? "true" : "false").Append(',')
                .Append("\"asyncThumbnailPass\":").Append(asyncThumbnailPass ? "true" : "false").Append(',')
                .Append("\"loadingStagePass\":").Append(loadingStagePass ? "true" : "false").Append(',')
                .Append("\"safeAupSnapPass\":").Append(safeAupSnapPass ? "true" : "false").Append(',')
                .Append("\"savingHudPass\":").Append(savingHudPass ? "true" : "false").Append(',')
                .Append("\"savingHudShaderPulsePass\":").Append(savingHudShaderPulsePass ? "true" : "false").Append(',')
                .Append("\"corruptionDialogPass\":").Append(corruptionDialogPass ? "true" : "false").Append(',')
                .Append("\"seedConsistencyPass\":").Append(seedConsistencyPass ? "true" : "false").Append(',')
                .Append("\"inventoryFullWritePass\":").Append(inventoryFullWritePass ? "true" : "false").Append(',')
                .Append("\"portableFileStreamWritePass\":").Append(portableFileStreamWritePass ? "true" : "false").Append(',')
                .Append("\"inventoryShadowBufferPass\":").Append(inventoryShadowBufferPass ? "true" : "false").Append(',')
                .Append("\"tombstoneLoadOrderPass\":").Append(tombstoneLoadOrderPass ? "true" : "false").Append(',')
                .Append("\"modPayloadSidecarPass\":").Append(modPayloadSidecarPass ? "true" : "false").Append(',')
                .Append("\"hydrationTimeSlicePass\":").Append(hydrationTimeSlicePass ? "true" : "false").Append(',')
                .Append("\"hydrationGcPurgePass\":").Append(hydrationGcPurgePass ? "true" : "false").Append(',')
                .Append("\"registryMigrationRsqrtPass\":").Append(registryMigrationRsqrtPass ? "true" : "false").Append(',')
                .Append("\"deterministicScatterCheapRadiusPass\":").Append(deterministicScatterCheapRadiusPass ? "true" : "false").Append(',')
                .Append("\"asyncDehydrationPipelinePass\":").Append(asyncDehydrationPipelinePass ? "true" : "false").Append(',')
                .Append("\"writeAllBytesPurgedPass\":").Append(writeAllBytesPurgedPass ? "true" : "false").Append(',')
                .Append("\"saveEventOverflowTelemetryPass\":").Append(saveEventOverflowTelemetryPass ? "true" : "false").Append(',')
                .Append("\"saveEventDispatchMutationPass\":").Append(saveEventDispatchMutationPass ? "true" : "false").Append(',')
                .Append("\"saveLoadFailureUiBridgePass\":").Append(saveLoadFailureUiBridgePass ? "true" : "false").Append(',')
                .Append("\"modWorldLoadRollbackPass\":").Append(modWorldLoadRollbackPass ? "true" : "false").Append(',')
                .Append("\"modWorldObjectPoolRetryPass\":").Append(modWorldObjectPoolRetryPass ? "true" : "false").Append(',')
                .Append("\"modWorldRegistrySceneRetryPass\":").Append(modWorldRegistrySceneRetryPass ? "true" : "false").Append(',')
                .Append("\"modRegistryOverflowRetryPass\":").Append(modRegistryOverflowRetryPass ? "true" : "false").Append(',')
                .Append("\"modWorldStaleHandleRestorePass\":").Append(modWorldStaleHandleRestorePass ? "true" : "false").Append(',')
                .Append("\"modWorldSpawnHashSanitizerPass\":").Append(modWorldSpawnHashSanitizerPass ? "true" : "false").Append(',')
                .Append("\"modAssetBindingLifecyclePass\":").Append(modAssetBindingLifecyclePass ? "true" : "false").Append(',')
                .Append("\"modSaveStateMmfRollbackPass\":").Append(modSaveStateMmfRollbackPass ? "true" : "false").Append(',')
                .Append("\"eventBusThrowableAllocationTelemetryPass\":").Append(eventBusThrowableAllocationTelemetryPass ? "true" : "false").Append(',')
                .Append("\"sceneActivationContractPass\":").Append(sceneActivationContractPass ? "true" : "false").Append(',')
                .Append("\"playModeSentinelAsyncIoPass\":").Append(playModeSentinelAsyncIoPass ? "true" : "false").Append(',')
                .Append("\"saveSlotPathGuardPass\":").Append(saveSlotPathGuardPass ? "true" : "false").Append(',')
                .Append("\"interactionEventRuntimeBridgePass\":").Append(interactionEventRuntimeBridgePass ? "true" : "false").Append(',')
                .Append("\"saveThumbnailSidecarGuardPass\":").Append(saveThumbnailSidecarGuardPass ? "true" : "false").Append(',')
                .Append("\"inventoryRewriteOffset\":").Append(inventoryRewriteOffset).Append(',')
                .Append("\"inventoryRewriteLength\":").Append(inventoryRewriteLength)
                .Append('}');

            File.WriteAllText(artifactPath, builder.ToString());
        }
    }
}
#endif
