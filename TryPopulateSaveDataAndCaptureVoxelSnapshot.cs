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
