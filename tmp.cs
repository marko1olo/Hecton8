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
        private bool TryPassPreflightChecks(ref string slotName, byte slotIndex, uint operationId)
        {
            if (_runtimeOwnerAborted || !_serviceRegistered)
            {
                uint unavailableSlotHash = ResolveUnavailableSlotContext(slotName, slotIndex, out string unavailableSlotName);
                LastOperationError = SaveServiceUnavailableReason;
                LastOperationSlot = unavailableSlotName;
                SaveEvents.TryRaiseSaveFailed(unavailableSlotHash, SaveEvents.ComputeMessageHash(SaveServiceUnavailableReason), SaveServiceUnavailableReason);
                PublishSaveStatus(unavailableSlotHash, operationId, SaveStatusSignal.Rejected, 0f, 1u);
                return false;
            }

            if (!TryResolveSafeSlotName(slotName, out slotName))
            {
                LastOperationError = InvalidSlotNameReason;
                LogWarning("[SaveManager] Ignored save request: invalid slot name.");
                SaveEvents.TryRaiseSaveFailed(0u, SaveEvents.ComputeMessageHash(InvalidSlotNameReason), InvalidSlotNameReason);
                PublishSaveStatus(slotIndex, operationId, SaveStatusSignal.Rejected, 0f, 1u);
                return false;
            }

            LastOperationSlot = slotName;

            if (_isBusy)
            {
                const string reason = "Save already in progress.";
                LastOperationError = reason;
                LogWarning($"[SaveManager] Ignored save request for '{slotName}': {reason}");
                SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Rejected, 0f, 1u);
                return false;
            }

            if (TryRejectSaveDuringRespawnReconciliation(slotIndex, operationId, slotName))
                return false;

            if (HectonFloatingOrigin.IsShiftInProgress || HectonFloatingOrigin.IsPhysicsPausedForShift)
            {
                const string reason = "Save blocked during floating-origin shift.";
                LastOperationError = reason;
                LogWarning($"[SaveManager] Ignored save request for '{slotName}': {reason}");
                SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);
                PublishSaveStatusForSlotName(slotIndex, slotName, operationId, SaveStatusSignal.Rejected, 0f, 1u);
                return false;
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
                return false;
            }

            return true;
        }
