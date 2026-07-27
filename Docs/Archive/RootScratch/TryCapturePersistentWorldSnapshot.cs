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
