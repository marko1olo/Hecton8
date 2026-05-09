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
        private const string InventoryFullWriteMmfRelativePath = "CodexArtifacts/persistence-ux-inventory-full-write.mmf";
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
            string suitHud = ReadProjectFile("Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs");
            string dataRecPulseShader = ReadProjectFile("Assets/_Project/Shaders/UI/Hecton_DataRecPulse.shader");
            string playerInventory = ReadProjectFile("Assets/_Project/Scripts/PlayerInventory.cs");
            string saveBinaryPayloadCodec = ReadProjectFile("Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs");
            string saveBinaryStorage = ReadProjectFile("Assets/_Project/Scripts/SaveBinaryStorage.cs");
            string persistentWorldRegistry = ReadProjectFile("Assets/_Project/Scripts/World/PersistentWorldRegistry.cs");
            string unsafeMemoryCopyGuard = ReadProjectFile("Assets/_Project/Scripts/Core/UnsafeMemoryCopyGuard.cs");
            string saveEvents = ReadProjectFile("Assets/_Project/Scripts/SaveEvents.cs");
            string hectonEventBus = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs");
            string codexPlayModeLauncher = ReadProjectFile("Assets/_Project/Scripts/Editor/CodexPlayModeLauncher.cs");
            string sceneRuntimeService = ReadProjectFile("Assets/_Project/Scripts/Core/SceneRuntimeService.cs");
            string mainMenuController = ReadProjectFile("Assets/_Project/Scripts/MainMenuController.cs");
            string pauseMenuController = ReadProjectFile("Assets/_Project/Scripts/UI/PauseMenuController.cs");
            string saveSlotMaintenanceRecord = ReadProjectFile("Assets/_Project/Scripts/SaveSlotMaintenanceRecord.cs");
            string saveStation = ReadProjectFile("Assets/_Project/Scripts/Interaction/SaveStation.cs");
            string saveSidecarStorage = ReadProjectFile("Assets/_Project/Scripts/SaveSidecarStorage.cs");
            string saveSlotThumbnail = ReadProjectFile("Assets/_Project/Scripts/UI/SaveSlotThumbnail.cs");

            bool asyncThumbnailPass =
                ContainsAll(thumbnailSystem, "Extension = \".jpg\"", "EncodeNativeArrayToJPG", "Awaitable.BackgroundThreadAsync", "NativeMemorySentinel.RegisterNativeArray") &&
                ContainsAll(thumbnailSystem, "MinPoseCaptureDistanceMeters = 5f", "MinPoseCaptureAngleDegrees = 5f", "MinPoseCaptureQuaternionDot", "HasCapturePoseChanged", "delta.sqrMagnitude > MinPoseCaptureDistanceSq", "Quaternion.Dot") &&
                ContainsAll(captureFeature, "RequestAsyncReadback", "SaveThumbnailSystem.ReadbackCompletedCallback") &&
                SourceIndex(saveManager, "SaveThumbnailSystem.CaptureThumbnail(slotName);") <
                SourceIndex(saveManager, "SaveEvents.RaiseSaveStarted(slotName);");

            bool loadingStagePass =
                ContainsAll(loadingScreen, "LoadingPipelineStage", "Paging Sectors...", "Hydrating Entities...", "Building NavGrid...", "CharBufferPool.TryAcquire", "SetCharArray", "WritePercent") &&
                ContainsAll(saveManager, "ReportLoadPipelineStage(LoadingPipelineStage.PagingSectors", "ReportLoadPipelineStage(LoadingPipelineStage.HydratingEntities", "ReportLoadPipelineStage(LoadingPipelineStage.BuildingNavGrid");

            bool safeAupSnapPass =
                ContainsAll(saveManager, "TryApplySafeAupSnapOnLoad(data)", "Physics.SphereCastNonAlloc", "AbsoluteUniversePosition.FromRuntimePosition", "HectonFloatingOrigin.BeginSafeTeleportProtocol");

            bool savingHudPass =
                ContainsAll(saveManager, "SaveEvents.RaiseMappedWriteStarted(slotName);") &&
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
                ContainsAll(saveManager, "CriticalSectorCorruptionMessage", "NotificationEvents.PushCritical(CriticalSectorCorruptionMessage)");

            bool seedConsistencyPass =
                ContainsAll(saveManager, "GeologicalAnomalyDetectedMessage", "WorldGenerationVersionId", "RuntimeWorldGenerationVersionId") &&
                ContainsAll(ReadProjectFile("Assets/_Project/Scripts/SaveData.cs"), "worldGenerationVersionId", "CurrentVersion =") &&
                ContainsAll(ReadProjectFile("Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs"), "RuntimeWorldGenerationVersionId") &&
                ContainsAll(ReadProjectFile("Assets/_Project/Scripts/HectonWorldGenerator.cs"), "WorldGenerationAlgorithmVersionId");

            bool inventoryFullWritePass = RunInventoryFullWriteMmfAssert(out int rewrittenOffset, out int rewrittenLength);
            bool unsafeMappedWritePass =
                ContainsAll(saveBinaryStorage, "MemoryMappedFile.CreateFromFile", "UnsafeMemoryCopyGuard.SafeCopy") &&
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
                ContainsAll(saveManager, "LoadApplyFrameBudgetTicks = Math.Max(1L, Stopwatch.Frequency / 333L)", "await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: destroyCancellationToken);") &&
                ContainsAll(persistentWorldRegistry, "HydrationFrameBudgetTicks = Math.Max(1L, Stopwatch.Frequency / 333L)", "TryProcessHydrationBurst", "await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(cancellationToken: destroyCancellationToken);") &&
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

            bool writeAllBytesPurgedPass = !ProjectSourceContains("File." + "WriteAllBytes");

            bool saveEventOverflowTelemetryPass =
                ContainsAll(saveEvents, "DroppedEventCount", "ReportOverflow(type);", "GlobalTelemetryBus.PublishPerformanceWarning", "SaveEventOverflowWarningHash", "SaveEventQueueContextHash", "ResolveKnownSlotIndex", "UnknownSlotNumber") &&
                ContainsAll(saveEvents, "DroppedListenerRegistrationCount", "ReportListenerRegistrationOverflow", "SaveEventListenerOverflowWarningHash", "TryRegister(listener)") &&
                ContainsAll(saveEvents, "TruncatedPayloadCount", "ReportPayloadTruncated", "SaveEventPayloadTruncatedWarningHash", "CopyFromTruncated", "CopySlotName(slot)", "CopyMessage(message)") &&
                ContainsAll(saveEvents, "ManualSlotCount = 3", "ResolveManualSlotName", "TryResolveKnownSlotName", "if (slotName.Length <= 0)", "resolvedSlotName = Slot0Name", "resolvedSlotName = Slot2Name", "return false;") &&
                ContainsAll(saveEvents, "private static void DrainQueueWithoutBudget", "silent stale-event cleanup must not steal shared LateFrame dispatch budget") &&
                ContainsAll(saveEvents, "NativeMemorySentinel.RegisterNativeQueue", "NativeAllocationLifetime.Session") &&
                ContainsAll(pauseMenuController, "_cachedUpperSlotDisplayNames", "ClearUpperSlotDisplayCache", "SaveEvents.ResolveKnownSlotIndex(slotName)") &&
                ContainsAll(pauseMenuController, "NormalizeSaveSlots()", "saveSlots = { \"slot_0\", \"slot_1\", \"slot_2\" }", "new string[SaveEvents.ManualSlotCount]") &&
                ContainsAll(pauseMenuController, "CopyFixedStringUpperAsciiToBuffer(in error, buffer, ref cursor)", "CopyStringToBuffer(_cachedUnknownErrorStatus, buffer, cursor)") &&
                ContainsAll(mainMenuController, "SlotCount = SaveEvents.ManualSlotCount", "SaveEvents.ResolveManualSlotName(0)", "SaveEvents.ResolveManualSlotName(1)", "SaveEvents.ResolveManualSlotName(2)") &&
                ContainsAll(ReadProjectFile("Assets/_Project/Scripts/UI/SaveThumbnailCapture.cs"), "SaveEvents.TryResolveKnownSlotName(in payload.SlotName, out string slotName)", "SaveThumbnailSystem.CaptureThumbnail(slotName, captureCamera);") &&
                SourceIndex(saveEvents, "Slot3Name") == int.MaxValue &&
                SourceIndex(saveEvents, "\"slot_3\"") == int.MaxValue &&
                SourceIndex(mainMenuController, "\"slot_3\"") == int.MaxValue &&
                SourceIndex(pauseMenuController, "\"slot_3\"") == int.MaxValue &&
                SourceIndex(pauseMenuController, "CachedToUpperInvariant(error.ToString())") == int.MaxValue &&
                SourceIndex(saveEvents, ".ToString()") == int.MaxValue &&
                SourceIndex(saveEvents, "Substring(") == int.MaxValue &&
                SourceIndex(saveEvents, "SlotName = string.IsNullOrEmpty(slot)") == int.MaxValue &&
                SourceIndex(saveEvents, "Message = string.IsNullOrEmpty(message)") == int.MaxValue;

            bool saveEventDispatchMutationPass =
                ContainsAll(saveEvents, "ListenerCapacity = 16", "_deferredRegisterListeners", "_deferredUnregisterListeners") &&
                ContainsAll(saveEvents, "QueueDeferredRegister(listener);", "QueueDeferredUnregister(listener);", "ApplyDeferredListenerMutations();") &&
                ContainsAll(saveEvents, "private static void RegisterImmediate", "CancelDeferredRegister(listener)", "CancelDeferredUnregister(listener)") &&
                ContainsAll(saveEvents, "ListenerExceptionCount", "ReportListenerDispatchException", "SaveEventListenerExceptionWarningHash", "SaveEventListenerExceptionContextHash") &&
                ContainsAll(saveEvents, "listener == null || IsDeferredUnregisterPending(listener)", "_listeners.TryUnregister(listener)") &&
                SourceIndex(saveEvents, "if (_isDispatching)\r\n                CancelDeferredUnregister(listener)") == int.MaxValue;

            bool eventBusThrowableAllocationTelemetryPass =
                ContainsAll(hectonEventBus, "MaxEventDispatchDepth = 5", "GlobalTelemetryBus.PublishCatastrophicCascadePrevented") &&
                ContainsAll(hectonEventBus, "GC.GetAllocatedBytesForCurrentThread() - allocationBefore", "ModCommandDispatcher.ReportModManagedAllocation(entry.SubscriberHash, allocationDelta);", "ModCallbackExceptionDisableReason") &&
                CountOccurrences(hectonEventBus, "ModLoader.DisableManagedMod(entry.SubscriberId, ModCallbackExceptionDisableReason);") == 3 &&
                SourceIndex(hectonEventBus, "ex.Message") == int.MaxValue;

            bool sceneActivationContractPass =
                ContainsAll(sceneRuntimeService, "TransitionDissolveSeconds = 3f", "_pendingSceneLoadOperation.allowSceneActivation = false", "ReleaseSceneActivation(_pendingSceneLoadOperation);") &&
                ContainsAll(sceneRuntimeService, "HasMainMenuDissolveReachedActivationTime(_cinematicTransitionElapsed)", "return elapsedSeconds == TransitionDissolveSeconds;") &&
                ContainsAll(sceneRuntimeService, "IServiceShutdown", "ShutdownServiceState()", "_sceneActivationReleased = false;") &&
                ContainsAll(mainMenuController, "SceneRuntimeService.EnsureRuntimeInstance", "sceneService.LoadScene(targetSceneName);") &&
                ContainsAll(pauseMenuController, "SceneRuntimeService.EnsureRuntimeInstance", "sceneService.LoadScene(mainMenuSceneName);") &&
                SourceIndex(sceneRuntimeService, "_pendingSceneLoadOperation.allowSceneActivation = true") == int.MaxValue &&
                SourceIndex(sceneRuntimeService, "_cinematicTransitionElapsed >= TransitionDissolveSeconds") == int.MaxValue &&
                SourceIndex(mainMenuController, "allowSceneActivation") == int.MaxValue &&
                SourceIndex(mainMenuController, "SceneManager.LoadSceneAsync") == int.MaxValue &&
                SourceIndex(pauseMenuController, "allowSceneActivation") == int.MaxValue &&
                SourceIndex(pauseMenuController, "SceneManager.LoadSceneAsync") == int.MaxValue;

            bool playModeSentinelAsyncIoPass =
                ContainsAll(codexPlayModeLauncher, "TryWriteAutoRunFlagAsync", "FileOptions.Asynchronous | FileOptions.WriteThrough", "await stream.WriteAsync(AutoRunFlagPayload, 0, AutoRunFlagPayload.Length);", "await stream.FlushAsync();") &&
                ContainsAll(codexPlayModeLauncher, "RequestMetricsWriteAndCleanup", "WriteMetricsAndCleanupAsync", "await WriteMetricsAsync();", "Metrics pipeline failed before cleanup", "CleanupAndExitIfBatch();");

            bool saveSlotPathGuardPass =
                ContainsAll(saveManager, "MaxSaveSlotNameLength = 48", "InvalidSlotNameReason = \"Invalid save slot name.\"", "InvalidSlotFileStem = \"slot_invalid\"") &&
                ContainsAll(saveManager, "TryResolveSafeSlotName", "ResolveSafeSlotFileStem", "IsReservedManualSlotPattern", "StringComparison.OrdinalIgnoreCase", "!SaveEvents.IsKnownManualSlotName(slotName)", "safeSlotName = slotName;") &&
                ContainsAll(saveManager, "SaveEvents.RaiseSaveFailed(string.Empty, InvalidSlotNameReason);", "SaveEvents.RaiseLoadFailed(string.Empty, InvalidSlotNameReason);") &&
                ContainsAll(saveManager, "GetPrimarySaveFilePath(string slotName) => $\"{ResolveSafeSlotFileStem(slotName)}.sav\"", "GetTempSaveFilePath(string slotName) => $\"{ResolveSafeSlotFileStem(slotName)}.sav.tmp\"", "GetDiagnosticSaveFilePath(string slotName) => $\"{ResolveSafeSlotFileStem(slotName)}.diag\"") &&
                ContainsAll(saveManager, "return TryResolveSafeSlotName(slotName, out slotName);", "BuildSaveSlotInfoInternal(string slotName)") &&
                CountOccurrences(saveManager, "if (!TryResolveSafeSlotName(slotName, out slotName))") >= 7 &&
                ContainsAll(saveSlotMaintenanceRecord, "SaveManager.GetDiagnosticSaveFilePath(slotName)", "SaveManager.TryResolveSafeSlotName(SlotName, out string safeSlotName)", "SaveManager.TryResolveSafeSlotName(slotName, out string safeSlotName)", "SaveSlotInfo.ToStorageString(SaveSlotIntegrityState.Empty)") &&
                ContainsAll(saveStation, "SaveManager.IsSafeSlotName(_saveSlot)", "Save slot rejected by SaveManager slot-name guard.", "Debug.LogError(\"[SaveStation] SaveManager instance not found.\", this);") &&
                SourceIndex(saveStation, "#if UNITY_EDITOR || DEVELOPMENT_BUILD") < SourceIndex(saveStation, "Debug.LogError(\"[SaveStation] SaveManager instance not found.\", this);") &&
                SourceIndex(saveManager, "slotName.Trim(") == int.MaxValue &&
                SourceIndex(saveManager, "$\"{slotName}.sav\"") == int.MaxValue &&
                SourceIndex(saveManager, "$\"{slotName}.sav.tmp\"") == int.MaxValue &&
                SourceIndex(saveManager, "$\"{slotName}.diag\"") == int.MaxValue &&
                SourceIndex(saveSlotMaintenanceRecord, "$\"{slotName}.diag\"") == int.MaxValue &&
                SourceIndex(saveSlotMaintenanceRecord, "SaveSlotIntegrityState.Empty.ToString()") == int.MaxValue;

            bool saveThumbnailSidecarGuardPass =
                ContainsAll(thumbnailSystem, "ResolveThumbnailFileStem", "SaveManager.ResolveSafeSlotFileStem(slotName)", "Path.Combine(Application.persistentDataPath, ResolveThumbnailFileStem(slotName) + Extension)", "Path.Combine(Application.persistentDataPath, ResolveThumbnailFileStem(slotName) + LegacyExtension)") &&
                CountOccurrences(thumbnailSystem, "SaveManager.TryResolveSafeSlotName(slotName, out slotName)") >= 4 &&
                ContainsAll(thumbnailSystem, "AsyncWriteManager.WriteAll(tempPath, dataPtr, encodedJpg.Length, out string writeError)", "throw new IOException(writeError);", "bool encodedJpgRegistered = false", "encodedJpgRegistered = true", "File.Move(tempPath, path);", "await Awaitable.MainThreadAsync();") &&
                ContainsAll(saveSidecarStorage, "NativeTempMemoryLifetime = NativeAllocationLifetime.Temp", "RegisterTempBuffer(buffer, \"metadataWriteBuffer\")", "RegisterTempBuffer(buffer, \"metadataReadBuffer\")", "RegisterTempBuffer(buffer, \"maintenanceWriteBuffer\")", "RegisterTempBuffer(buffer, \"maintenanceReadBuffer\")", "NativeMemorySentinel.RegisterNativeArray(buffer, NativeMemoryOwner, label, NativeTempMemoryLifetime)", "NativeMemorySentinel.UnregisterNativeArray(buffer)") &&
                ContainsAll(saveSlotThumbnail, "SaveManager.IsSafeSlotName(slotName)", "SaveThumbnailSystem.CaptureThumbnail(slotName, captureCamera);", "SaveThumbnailSystem.LoadThumbnail(slotName)") &&
                SourceIndex(thumbnailSystem, "slotName + Extension") == int.MaxValue &&
                SourceIndex(thumbnailSystem, "slotName + LegacyExtension") == int.MaxValue &&
                SourceIndex(thumbnailSystem, "new FileStream(tempPath") == int.MaxValue &&
                CountOccurrences(saveSidecarStorage, "DisposeTempBuffer(ref buffer);") == 4 &&
                CountOccurrences(saveSidecarStorage, "buffer.Dispose();") == 1;

            bool pass = asyncThumbnailPass &&
                        loadingStagePass &&
                        safeAupSnapPass &&
                        savingHudPass &&
                        savingHudShaderPulsePass &&
                        corruptionDialogPass &&
                        seedConsistencyPass &&
                        inventoryFullWritePass &&
                        unsafeMappedWritePass &&
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
                        eventBusThrowableAllocationTelemetryPass &&
                        sceneActivationContractPass &&
                        playModeSentinelAsyncIoPass &&
                        saveSlotPathGuardPass &&
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
                unsafeMappedWritePass,
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
                eventBusThrowableAllocationTelemetryPass,
                sceneActivationContractPass,
                playModeSentinelAsyncIoPass,
                saveSlotPathGuardPass,
                saveThumbnailSidecarGuardPass,
                rewrittenOffset,
                rewrittenLength);

            if (pass)
                Debug.Log("[PersistenceUxSmokeTester] PASS artifact=" + ArtifactRelativePath);
            else
                Debug.LogError("[PersistenceUxSmokeTester] FAIL artifact=" + ArtifactRelativePath);

            return pass;
        }

        private static bool RunInventoryFullWriteMmfAssert(out int rewrittenOffset, out int rewrittenLength)
        {
            byte[] before = new byte[SectorSizeBytes]; // COLD ALLOC: byte[16KB] — editor-only inventory full-write sector fixture — owner: PersistenceUxSmokeTester
            byte[] after = new byte[SectorSizeBytes]; // COLD ALLOC: byte[16KB] — editor-only inventory full-write sector fixture — owner: PersistenceUxSmokeTester
            byte[] observed = new byte[SectorSizeBytes]; // COLD ALLOC: byte[16KB] — editor-only MMF full-write verification readback — owner: PersistenceUxSmokeTester
            for (int slot = 0; slot < InventorySlotCount; slot++)
            {
                int offset = slot * InventorySlotStrideBytes;
                WriteInventorySlot(before, offset, unchecked((uint)(0xA0000000u + slot)), (ushort)(slot + 1), (ushort)0);
                WriteInventorySlot(after, offset, unchecked((uint)(0xA0000000u + slot)), (ushort)(slot + 1), (ushort)0);
            }

            int changedSlot = 17;
            int changedSlotOffset = changedSlot * InventorySlotStrideBytes;
            WriteInventorySlot(after, changedSlotOffset, 0u, (ushort)0, (ushort)1);

            string mmfPath = Path.Combine(System.Environment.CurrentDirectory, InventoryFullWriteMmfRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(mmfPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            WriteBytes(mmfPath, before);
            using (FileStream stream = new FileStream(mmfPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                stream.Position = 0L;
                stream.Write(after, 0, SectorSizeBytes);
                stream.Flush(true);
            }

            using (FileStream stream = new FileStream(mmfPath, FileMode.Open, FileAccess.Read, FileShare.Read))
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

        private static bool ProjectSourceContains(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            string sourceRoot = Path.Combine(System.Environment.CurrentDirectory, "Assets/_Project/Scripts");
            if (!Directory.Exists(sourceRoot))
                return false;

            string[] files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                if (File.ReadAllText(files[i]).IndexOf(value, StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
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
            bool unsafeMappedWritePass,
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
            bool eventBusThrowableAllocationTelemetryPass,
            bool sceneActivationContractPass,
            bool playModeSentinelAsyncIoPass,
            bool saveSlotPathGuardPass,
            bool saveThumbnailSidecarGuardPass,
            int inventoryRewriteOffset,
            int inventoryRewriteLength)
        {
            string artifactPath = Path.Combine(System.Environment.CurrentDirectory, ArtifactRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(artifactPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            StringBuilder builder = new StringBuilder(1248); // COLD ALLOC: StringBuilder[1248] — editor smoke JSON artifact — owner: PersistenceUxSmokeTester
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
                .Append("\"unsafeMappedWritePass\":").Append(unsafeMappedWritePass ? "true" : "false").Append(',')
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
                .Append("\"eventBusThrowableAllocationTelemetryPass\":").Append(eventBusThrowableAllocationTelemetryPass ? "true" : "false").Append(',')
                .Append("\"sceneActivationContractPass\":").Append(sceneActivationContractPass ? "true" : "false").Append(',')
                .Append("\"playModeSentinelAsyncIoPass\":").Append(playModeSentinelAsyncIoPass ? "true" : "false").Append(',')
                .Append("\"saveSlotPathGuardPass\":").Append(saveSlotPathGuardPass ? "true" : "false").Append(',')
                .Append("\"saveThumbnailSidecarGuardPass\":").Append(saveThumbnailSidecarGuardPass ? "true" : "false").Append(',')
                .Append("\"inventoryRewriteOffset\":").Append(inventoryRewriteOffset).Append(',')
                .Append("\"inventoryRewriteLength\":").Append(inventoryRewriteLength)
                .Append('}');

            File.WriteAllText(artifactPath, builder.ToString());
        }
    }
}
#endif
