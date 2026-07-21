        private bool TryPopulateSaveDataAndCaptureVoxelSnapshot(
            string slotName,
            byte slotIndex,
            uint operationId,
            SaveData data,
            long elapsedMilliseconds,
            ref NativeArray<byte> voxelDeltaSnapshot,
            ref bool ownsVoxelDeltaSnapshot,
            ref VoxelDeltaProcessor borrowedVoxelDeltaSnapshotOwner,
            ref bool snapshotPauseActive)
        {
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
                            HandleSaveFailure(slotName, slotIndex, operationId, reason, logReason, failureCode, elapsedMilliseconds, ref cleanupException, ref snapshotPauseActive);
                            return false;
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
            return true;
        }
        private bool TryCapturePersistentWorldSnapshot(
            string slotName,
            byte slotIndex,
            uint operationId,
            PersistentWorldRegistry persistentWorldRegistry,
            long elapsedMilliseconds,
            ref NativeArray<PersistentWorldDeltaRecord>.ReadOnly persistentWorldDeltaSnapshot,
            ref NativeArray<PersistentWorldDeltaRecord> persistentWorldDeltaSnapshotOwner,
            ref bool snapshotPauseActive)
        {
            if (persistentWorldRegistry != null)
            {
                if (!persistentWorldRegistry.CaptureSaveSnapshot())
                {
                    const string reason = "Persistent world save snapshot capture failed.";
                    const string logReason = "[SaveManager] Save failed: persistent world save snapshot capture failed.";
                    const uint failureCode = 3u;
                    Exception cleanupException = null;
                    HandleSaveFailure(slotName, slotIndex, operationId, reason, logReason, failureCode, elapsedMilliseconds, ref cleanupException, ref snapshotPauseActive);
                    return false;
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
                        HandleSaveFailure(slotName, slotIndex, operationId, reason, logReason, failureCode, elapsedMilliseconds, ref cleanupException, ref snapshotPauseActive);
                        return false;
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
            return true;
        }
        private void CaptureEcosystemSnapshot(
            ref NativeArray<EcosystemSectorSaveRecord>.ReadOnly ecosystemSectorSnapshot,
            ref NativeArray<EcosystemSectorSaveRecord> ecosystemSectorSnapshotOwner)
        {
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
        }
        private void CaptureQuestSnapshot(
            long saveTimestampTicks,
            ref NativeArray<uint> packedQuestStateSnapshot,
            ref QuestSaveHeader packedQuestSaveHeader)
        {
            QuestManager questManager = GlobalRegistry.Quest;
            if (questManager != null)
            {
                int packedQuestWordCount = questManager.PackedStateWordCount;
                if (packedQuestWordCount > 0)
                {
                    packedQuestStateSnapshot = CreateTransientNativeArray<uint>(
                        packedQuestWordCount,
                        Allocator.Persistent,
                        NativeArrayOptions.ClearMemory,
                        "packedQuestStateSnapshot");

                    bool copiedQuestState;
                    unsafe
                    {
                        void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(packedQuestStateSnapshot);
                        copiedQuestState = questManager.TryCopyPackedStateSnapshot(
                            destinationPtr,
                            packedQuestStateSnapshot.Length,
                            out packedQuestSaveHeader,
                            saveTimestampTicks);
                    }

                    if (!copiedQuestState)
                        DisposeTransientNativeArrayBestEffortAndReport(ref packedQuestStateSnapshot, "save", "packedQuestStateSnapshot");
                }
            }
        }
