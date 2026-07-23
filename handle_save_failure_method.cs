        private void HandleSaveFailure(
            string slotName,
            byte slotIndex,
            uint operationId,
            string reason,
            string logReason,
            uint failureCode,
            long elapsedMilliseconds,
            ref Exception cleanupException,
            ref bool snapshotPauseActive)
        {
            if (snapshotPauseActive)
            {
                ReleaseSnapshotPauseBestEffort(operationId, ref cleanupException);
                snapshotPauseActive = false;
            }

            ReportPersistenceCleanupFailure("save", cleanupException);
            RecordAsyncPersistenceTelemetry(operationId, slotName, elapsedMilliseconds, 0L, 0, 0, failureCode);
            PublishSaveCompletedForSlotName(slotIndex, slotName, new PublishSaveCompletedArgs(operationId: operationId, durationMs: elapsedMilliseconds, compressedSizeBytes: 0L, succeeded: false));
            PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Failed, 1f, failureCode);
            DumpSaveBlackBox();
            RecordFailure(slotName, "save", reason);
            LastOperationError = reason;
            LogError(logReason);
            SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
        }
