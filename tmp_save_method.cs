        private async Awaitable SaveGameAsyncInternal(string slotName, byte slotIndex, uint operationId)
        {
            CachePersistentDataPathRoot();
            LastOperationSucceeded = false;
            LastOperationError = string.Empty;
            LastOperationSlot = string.Empty;
            LastLoadUsedBackup = false;
            LastLoadBackupGeneration = 0;
            LastLoadSelfRepaired = false;
            LastLoadUsedLegacyCompression = false;

            if (_runtimeOwnerAborted || !_serviceRegistered)
            {
                uint unavailableSlotHash = ResolveUnavailableSlotContext(slotName, slotIndex, out string unavailableSlotName);
                LastOperationError = SaveServiceUnavailableReason;
                LastOperationSlot = unavailableSlotName;
                SaveEvents.TryRaiseSaveFailed(unavailableSlotHash, SaveEvents.ComputeMessageHash(SaveServiceUnavailableReason), SaveServiceUnavailableReason);
                PublishSaveStatus(unavailableSlotHash, operationId, SaveStatusSignal.Rejected, 0f, 1u);
                return;
            }

            if (!TryResolveSafeSlotName(slotName, out slotName))
            {
                LastOperationError = InvalidSlotNameReason;
                LogWarning("[SaveManager] Ignored save request: invalid slot name.");
                SaveEvents.TryRaiseSaveFailed(0u, SaveEvents.ComputeMessageHash(InvalidSlotNameReason), InvalidSlotNameReason);
                PublishSaveStatus(slotIndex, operationId, SaveStatusSignal.Rejected, 0f, 1u);
                return;
            }

            LastOperationSlot = slotName;

            if (_isBusy)
            {
                const string reason = "Save already in progress.";
                LastOperationError = reason;
                LogWarning($"[SaveManager] Ignored save request for '{slotName}': {reason}");
                SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Rejected, 0f, 1u);
                return;
            }

            if (TryRejectSaveDuringRespawnReconciliation(slotIndex, operationId, slotName))
                return;

            if (HectonFloatingOrigin.IsShiftInProgress || HectonFloatingOrigin.IsPhysicsPausedForShift)
            {
                const string reason = "Save blocked during floating-origin shift.";
                LastOperationError = reason;
                LogWarning($"[SaveManager] Ignored save request for '{slotName}': {reason}");
                SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Rejected, 0f, 1u);
                return;
            }

            _isBusy = true;
            Exception startupException = null;
            NotifyMacroDatabasePersistenceGateBestEffort(true, ref startupException);
            if (startupException != null)
            {
                const string reason = "Save persistence gate request failed.";
                LastOperationError = reason;
                LogWarningBestEffort($"[SaveManager] Save failed for '{slotName}': {reason}");
                _isBusy = false;
                NotifyMacroDatabasePersistenceGateBestEffort(false, ref startupException);
                ReportPersistenceCleanupFailure("save", startupException);
                SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, 1u);
                return;
            }

            SaveThumbnailSystem.CaptureTicket thumbnailTicket = default;
            var totalTimer = Stopwatch.StartNew();
            var snapshotTimer = Stopwatch.StartNew();
            bool snapshotPauseActive = false;
            double playTime = ResolveCurrentPlayTimeSeconds();
            SaveData data = SaveData.CreateNew(playTime);
            PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            NativeArray<PersistentWorldDeltaRecord>.ReadOnly persistentWorldDeltaSnapshot = default;
            NativeArray<PersistentWorldDeltaRecord> persistentWorldDeltaSnapshotOwner = default;
            NativeArray<EcosystemSectorSaveRecord>.ReadOnly ecosystemSectorSnapshot = default;
            NativeArray<EcosystemSectorSaveRecord> ecosystemSectorSnapshotOwner = default;
            NativeArray<uint> packedQuestStateSnapshot = default;
            QuestSaveHeader packedQuestSaveHeader = default;
            NativeArray<byte> voxelDeltaSnapshot = default;
            bool ownsVoxelDeltaSnapshot = false;
            VoxelDeltaProcessor borrowedVoxelDeltaSnapshotOwner = null;

            try
            {
                SaveEvents.TryRaiseSaveStarted(SaveEvents.ComputeSlotHash(slotName));
                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.InProgress, 0.05f, 0u);
                EnsureSaveWorkingBuffers();
                RequestSnapshotPause(operationId);
                snapshotPauseActive = true;
                await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(destroyCancellationToken);
                if (TryRejectSaveDuringRespawnReconciliation(
                        slotIndex,
                        operationId,
                        slotName,
                        activeSaveStarted: true,
                        elapsedMs: totalTimer.ElapsedMilliseconds))
                {
                    return;
                }

                thumbnailTicket = SaveThumbnailSystem.CaptureThumbnailForSave(slotName, slotIndex, operationId);
                ThreadSafeCommandQueue.PrepareStorageReservationCommitBridgeForPersistenceSnapshot();
                snapshotTimer.Restart();
                SortRegistryIfDirty(SavePriorityComparer);
                for (int i = 0; i < _saveableCount; i++)
                {
                    ISaveable saveable = _saveables[i];
                    if (!IsAlive(saveable))
                        continue;

                    if (saveable is VoxelDeltaProcessor voxelDeltaProcessor)
                    {
                        Exception cleanupException = null;
                        if (borrowedVoxelDeltaSnapshotOwner != null)
                        {
                            ReleaseBorrowedVoxelDeltaSnapshotBestEffort(borrowedVoxelDeltaSnapshotOwner, ref cleanupException);
                            borrowedVoxelDeltaSnapshotOwner = null;
                        }

                        if (voxelDeltaSnapshot.IsCreated && ownsVoxelDeltaSnapshot)
                            DisposeTransientNativeArrayBestEffort(ref voxelDeltaSnapshot, ref cleanupException, sentinelLabel: "voxelDeltaSnapshot");
                        else
                            voxelDeltaSnapshot = default;

                        ReportPersistenceCleanupFailure("save", cleanupException);
                        ownsVoxelDeltaSnapshot = false;
                        if (!voxelDeltaProcessor.TryCopyNativeSnapshotToBorrowedScratch(
                                out voxelDeltaSnapshot,
                                out int voxelDeltaSnapshotByteCount) ||
                            voxelDeltaSnapshotByteCount <= 0)
                        {
                            if (voxelDeltaSnapshotByteCount > 0)
                            {
                                const string reason = "Voxel delta native snapshot copy failed.";
                                const string logReason = "[SaveManager] Save failed: voxel delta native snapshot copy failed.";
                                const uint failureCode = 3u;
                                voxelDeltaSnapshot = default;
                                if (snapshotPauseActive)
                                {
                                    ReleaseSnapshotPauseBestEffort(operationId, ref cleanupException);
                                    snapshotPauseActive = false;
                                }

                                ReportPersistenceCleanupFailure("save", cleanupException);
                                RecordAsyncPersistenceTelemetry(operationId, slotName, totalTimer.ElapsedMilliseconds, 0L, 0, 0, failureCode);
                                PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: totalTimer.ElapsedMilliseconds, compressedSizeBytes: 0L, succeeded: false));
                                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, failureCode);
                                DumpSaveBlackBox();
                                RecordFailure(slotName, "save", reason);
                                LastOperationError = reason;
                                LogError(logReason);
                                SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
                                return;
                            }

                            voxelDeltaSnapshot = default;
                        }
                        else
                        {
                            borrowedVoxelDeltaSnapshotOwner = voxelDeltaProcessor;
                        }

                    }

                    saveable.PopulateSaveData(data);
                }

                StampRuntimeWorldSeed(data);
                StampProceduralTerrainIdentity(data);
                ModSaveStateStore.PopulateSaveData(data);
                Stopwatch divergenceSnapshotTimer = Stopwatch.StartNew();
                if (persistentWorldRegistry != null)
                {
                    if (!persistentWorldRegistry.CaptureSaveSnapshot())
                    {
                        const string reason = "Persistent world save snapshot capture failed.";
                        const string logReason = "[SaveManager] Save failed: persistent world save snapshot capture failed.";
                        const uint failureCode = 3u;
                        Exception cleanupException = null;
                        if (snapshotPauseActive)
                        {
                            ReleaseSnapshotPauseBestEffort(operationId, ref cleanupException);
                            snapshotPauseActive = false;
                        }

                        ReportPersistenceCleanupFailure("save", cleanupException);
                        RecordAsyncPersistenceTelemetry(operationId, slotName, totalTimer.ElapsedMilliseconds, 0L, 0, 0, failureCode);
                        PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: totalTimer.ElapsedMilliseconds, compressedSizeBytes: 0L, succeeded: false));
                        PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, failureCode);
                        DumpSaveBlackBox();
                        RecordFailure(slotName, "save", reason);
                        LastOperationError = reason;
                        LogError(logReason);
                        SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
                        return;
                    }

                    int persistentWorldSnapshotCapacity = persistentWorldRegistry.SaveSnapshotCapacity;
                    if (persistentWorldSnapshotCapacity > 0)
                    {
                        persistentWorldDeltaSnapshotOwner = CreateTransientNativeArray<PersistentWorldDeltaRecord>(
                            persistentWorldSnapshotCapacity,
                            Allocator.Persistent,
                            NativeArrayOptions.UninitializedMemory,
                            "persistentWorldDeltaSnapshotOwner");

                        if (!persistentWorldRegistry.TryCopySaveSnapshotDeltas(
                            persistentWorldDeltaSnapshotOwner,
                            persistentWorldSnapshotCapacity,
                            out int copiedPersistentWorldDeltas))
                        {
                            Exception cleanupException = null;
                            DisposeTransientNativeArrayBestEffort(ref persistentWorldDeltaSnapshotOwner, ref cleanupException, sentinelLabel: "persistentWorldDeltaSnapshotOwner");
                            const string reason = "Persistent world save snapshot copy failed.";
                            const string logReason = "[SaveManager] Save failed: persistent world save snapshot copy failed.";
                            const uint failureCode = 3u;
                            if (snapshotPauseActive)
                            {
                                ReleaseSnapshotPauseBestEffort(operationId, ref cleanupException);
                                snapshotPauseActive = false;
                            }

                            ReportPersistenceCleanupFailure("save", cleanupException);
                            RecordAsyncPersistenceTelemetry(operationId, slotName, totalTimer.ElapsedMilliseconds, 0L, 0, 0, failureCode);
                            PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: totalTimer.ElapsedMilliseconds, compressedSizeBytes: 0L, succeeded: false));
                            PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, failureCode);
                            DumpSaveBlackBox();
                            RecordFailure(slotName, "save", reason);
                            LastOperationError = reason;
                            LogError(logReason);
                            SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
                            return;
                        }

                        if (copiedPersistentWorldDeltas > 0)
                        {
                            NativeArray<PersistentWorldDeltaRecord> copiedView = copiedPersistentWorldDeltas < persistentWorldDeltaSnapshotOwner.Length
                                ? persistentWorldDeltaSnapshotOwner.GetSubArray(0, copiedPersistentWorldDeltas)
                                : persistentWorldDeltaSnapshotOwner;
                            persistentWorldDeltaSnapshot = copiedView.AsReadOnly();
                        }
                    }
                }

                EcosystemDirector ecosystemDirector = GlobalRegistry.EcosystemDirector as EcosystemDirector;
                if (ecosystemDirector != null)
                {
                    ecosystemDirector.CaptureSaveSnapshot();
                    NativeArray<EcosystemSectorSaveRecord>.ReadOnly ecosystemView = ecosystemDirector.GetSaveSnapshotArray(out int ecosystemRecordCount);
                    if (ecosystemView.IsCreated && ecosystemRecordCount > 0)
                    {
                        ecosystemSectorSnapshotOwner = CreateTransientNativeArray<EcosystemSectorSaveRecord>(
                            ecosystemRecordCount,
                            Allocator.Persistent,
                            NativeArrayOptions.UninitializedMemory,
                            "ecosystemSectorSnapshotOwner");

                        for (int i = 0; i < ecosystemRecordCount; i++)
                            ecosystemSectorSnapshotOwner[i] = ecosystemView[i];

                        ecosystemSectorSnapshot = ecosystemSectorSnapshotOwner.AsReadOnly();
                    }
                }

                divergenceSnapshotTimer.Stop();
