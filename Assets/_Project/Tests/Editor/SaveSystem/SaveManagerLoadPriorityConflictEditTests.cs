using System;
using System.IO;
using System.Reflection;
using Hecton8.SaveSystem;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public sealed class SaveManagerLoadPriorityConflictEditTests
    {
        [Test]
        public void LoadPriorityComparersTieBreakEqualPriorityByTypeName()
        {
            MethodInfo compareLoad = GetPrivateStaticMethod("CompareLoadPriority");
            MethodInfo compareSave = GetPrivateStaticMethod("CompareSavePriority");
            ISaveable alpha = new AlphaPriorityOwner();
            ISaveable beta = new BetaPriorityOwner();

            int loadForward = (int)compareLoad.Invoke(null, new object[] { alpha, beta });
            int loadReverse = (int)compareLoad.Invoke(null, new object[] { beta, alpha });
            int saveForward = (int)compareSave.Invoke(null, new object[] { alpha, beta });
            int saveReverse = (int)compareSave.Invoke(null, new object[] { beta, alpha });

            Assert.AreNotEqual(0, loadForward);
            Assert.AreEqual(Math.Sign(loadForward), -Math.Sign(loadReverse));
            Assert.AreNotEqual(0, saveForward);
            Assert.AreEqual(Math.Sign(saveForward), -Math.Sign(saveReverse));
        }

        [Test]
        public void LoadGameReportsDuplicateOwnerConflictsBeforeApplyingSaveables()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string loadBody = ExtractMethodBody(source, "public async Awaitable LoadGameAsync(string slotName)");
            string reportBody = ExtractMethodBody(source, "private void ReportLoadPriorityConflictsForLoad(string slotName)");
            string publishBestEffort = ExtractMethodBody(source, "private static void PublishPerformanceWarningBestEffort(");

            int sortIndex = loadBody.IndexOf("SortRegistryIfDirty(LoadPriorityComparer);", StringComparison.Ordinal);
            int reportIndex = loadBody.IndexOf("ReportLoadPriorityConflictsForLoad(slotName);", StringComparison.Ordinal);
            int applyIndex = loadBody.IndexOf("VoxelDeltaProcessor voxelDeltaProcessor = null;", StringComparison.Ordinal);

            Assert.GreaterOrEqual(sortIndex, 0);
            Assert.Greater(reportIndex, sortIndex);
            Assert.Greater(applyIndex, reportIndex);
            StringAssert.Contains("PublishPerformanceWarningBestEffort(LoadPriorityConflictTelemetryHash", reportBody);
            StringAssert.Contains("Type ownerType = first.GetType();", reportBody);
            StringAssert.Contains("Type candidateType = candidate.GetType();", reportBody);
            StringAssert.Contains("BuildLoadPriorityConflictContextHash(slotName, priority, groupCount, ownerType)", reportBody);
            StringAssert.Contains("catch (Exception telemetryException)", publishBestEffort);
            StringAssert.Contains("LogErrorBestEffort(", publishBestEffort);
            StringAssert.Contains("LogWarningBestEffort(", reportBody);
            StringAssert.DoesNotContain("SaveEvents.TryRaise", reportBody);
            StringAssert.DoesNotContain("SignalBus<", reportBody);
        }

        [Test]
        public void LoadPriorityConflictStateIsClearedWithSaveManagerShutdown()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string shutdownBody = ExtractMethodBody(source, "private void ShutdownServiceState()");

            StringAssert.Contains("_saveableCount = 0;", shutdownBody);
            StringAssert.Contains("_saveableCapacityWarningLogged = false;", shutdownBody);
            StringAssert.Contains("_lastLoadPriorityConflictCount = 0;", shutdownBody);
            StringAssert.Contains("_lastLoadPriorityConflictFrame = 0;", shutdownBody);
        }

        [Test]
        public void SaveManagerWorldValidationNotificationsReportQueueRefusalsWithoutGatingWorldRecovery()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string validateSeed = ExtractMethodBody(source, "private static void ValidateRuntimeWorldSeed(");
            string criticalDialog = ExtractMethodBody(source, "private static void ReportCriticalSectorCorruptionDialog()");
            string geologicalPush = ExtractMethodBody(source, "private static void TryPushGeologicalAnomalyNotification()");
            string criticalPush = ExtractMethodBody(source, "private static void TryPushCriticalSectorCorruptionNotification()");
            string report = ExtractMethodBody(source, "private static void ReportSaveNotificationMiss(");
            string clear = ExtractMethodBody(source, "private static void ClearSaveNotificationDiagnostics()");
            string shutdownBody = ExtractMethodBody(source, "private void ShutdownServiceState()");

            StringAssert.Contains("public static int GeologicalAnomalyNotificationMissCount =>", source);
            StringAssert.Contains("public static int CriticalSectorCorruptionNotificationMissCount =>", source);
            StringAssert.Contains("GeologicalAnomalyNotificationMissTelemetryHash", source);
            StringAssert.Contains("CriticalSectorCorruptionNotificationMissTelemetryHash", source);

            int geologicalNotificationIndex = validateSeed.IndexOf("TryPushGeologicalAnomalyNotification();", StringComparison.Ordinal);
            int traumaIndex = validateSeed.IndexOf("PlayerSignalEvents.TryRaiseTraumaHudSignal", StringComparison.Ordinal);
            Assert.GreaterOrEqual(geologicalNotificationIndex, 0);
            Assert.Greater(traumaIndex, geologicalNotificationIndex);
            StringAssert.DoesNotContain("NotificationEvents.TryPushWarning(GeologicalAnomalyDetectedMessage.AsSpan());", validateSeed);

            int criticalNotificationIndex = criticalDialog.IndexOf("TryPushCriticalSectorCorruptionNotification();", StringComparison.Ordinal);
            int criticalLogIndex = criticalDialog.IndexOf("LogWarning($\"[SaveManager] {CriticalSectorCorruptionMessage}\");", StringComparison.Ordinal);
            Assert.GreaterOrEqual(criticalNotificationIndex, 0);
            Assert.Greater(criticalLogIndex, criticalNotificationIndex);
            StringAssert.DoesNotContain("NotificationEvents.TryPushCritical(CriticalSectorCorruptionMessage.AsSpan());", criticalDialog);

            StringAssert.Contains("if (NotificationEvents.TryPushWarning(GeologicalAnomalyDetectedMessage.AsSpan()))", geologicalPush);
            StringAssert.Contains("ReportSaveNotificationMiss(", geologicalPush);
            StringAssert.Contains("GeologicalAnomalyNotificationMissTelemetryHash", geologicalPush);
            StringAssert.Contains("ref s_geologicalAnomalyNotificationMissCount", geologicalPush);
            StringAssert.Contains("if (NotificationEvents.TryPushCritical(CriticalSectorCorruptionMessage.AsSpan()))", criticalPush);
            StringAssert.Contains("ReportSaveNotificationMiss(", criticalPush);
            StringAssert.Contains("CriticalSectorCorruptionNotificationMissTelemetryHash", criticalPush);
            StringAssert.Contains("ref s_criticalSectorCorruptionNotificationMissCount", criticalPush);
            StringAssert.Contains("PublishPerformanceWarningBestEffort(", report);
            StringAssert.Contains("SaveManagerNotificationContextHash ^ contextHash", report);
            StringAssert.Contains("math.max(1, missCount)", report);
            StringAssert.Contains("s_geologicalAnomalyNotificationMissCount = 0;", clear);
            StringAssert.Contains("s_criticalSectorCorruptionNotificationMissCount = 0;", clear);
            StringAssert.Contains("ClearSaveNotificationDiagnostics();", shutdownBody);
        }

        [Test]
        public void SaveableRegistryRefusesOverflowOwnerAndKeepsDenseArrayIntact()
        {
            // Replaces a source-text test that pinned the ORDER of statements inside
            // SaveManager.Register (SaveManager.cs:4483) with IndexOf offsets. That test proved the
            // capacity branch was WRITTEN above the append, not that an owner past capacity is
            // actually refused - it stayed green for any rewrite that kept the same literals and any
            // off-by-one in the bound itself. This drives the real method and reads the real registry.
            //
            // BOUNDARY, stated rather than faked: the telemetry publish that precedes the drop goes to
            // GlobalTelemetryBus and is not readable from this assembly, and the paired editor-only
            // Debug.LogError (SaveManager.cs:4507) is suppressed here rather than asserted, because
            // GlobalTelemetryBus may itself log from the best-effort catch in EditMode and that second
            // message would fail the fixture for the wrong reason. What is proven below is the
            // observable contract: capacity is honoured, the refused owner never lands in the array,
            // duplicates are rejected, and a freed slot is reusable.
            int maxRegisteredSaveables = ReadPrivateConstInt("MaxRegisteredSaveables");
            Assert.Greater(maxRegisteredSaveables, 1, "MaxRegisteredSaveables must leave room for an overflow case.");

            GameObject host = new GameObject("SaveManagerLoadPriorityConflictEditTests.RegistryOverflow");
            bool previousIgnoreFailingMessages = UnityEngine.TestTools.LogAssert.ignoreFailingMessages;
            try
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

                SaveManager manager = host.AddComponent<SaveManager>();
                SetPrivateInstanceField(manager, "_runtimeOwnerAborted", false);
                SetPrivateInstanceField(manager, "_serviceRegistered", true);

                AlphaPriorityOwner[] owners = new AlphaPriorityOwner[maxRegisteredSaveables];
                for (int i = 0; i < owners.Length; i++)
                {
                    owners[i] = new AlphaPriorityOwner();
                    manager.Register(owners[i]);
                }

                Assert.AreEqual(
                    maxRegisteredSaveables,
                    ReadRegisteredSaveableCount(manager),
                    "Registering exactly MaxRegisteredSaveables live owners must fill the registry.");

                BetaPriorityOwner overflowOwner = new BetaPriorityOwner();
                manager.Register(overflowOwner);

                Assert.AreEqual(
                    maxRegisteredSaveables,
                    ReadRegisteredSaveableCount(manager),
                    "An owner registered past capacity must be dropped, not appended.");
                Assert.IsFalse(
                    IsRegistered(manager, overflowOwner),
                    "The refused owner is still in the dense array, so the capacity branch did not drop it.");

                manager.Register(owners[0]);
                Assert.AreEqual(
                    maxRegisteredSaveables,
                    ReadRegisteredSaveableCount(manager),
                    "Re-registering an owner that is already present must not grow the registry.");

                manager.Unregister(owners[0]);
                Assert.AreEqual(maxRegisteredSaveables - 1, ReadRegisteredSaveableCount(manager));
                Assert.IsFalse(IsRegistered(manager, owners[0]));
                AssertNoNullEntriesBelowCount(manager);

                // Swap-with-last removal must leave the freed tail slot genuinely reusable.
                manager.Register(overflowOwner);
                Assert.AreEqual(maxRegisteredSaveables, ReadRegisteredSaveableCount(manager));
                Assert.IsTrue(
                    IsRegistered(manager, overflowOwner),
                    "A slot freed by Unregister was not reusable, so capacity accounting drifted.");
                AssertNoNullEntriesBelowCount(manager);
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SaveableRegistryRejectsDestroyedUnityOwnerInsteadOfStoringAStaleHandle()
        {
            // The dropped source-text test also asserted the literal
            // "!IsAlive(saveable)" guard in Register. IsAlive (SaveManager.cs:5082) exists so that a
            // destroyed UnityEngine.Object never enters the dense array; that is observable.
            GameObject host = new GameObject("SaveManagerLoadPriorityConflictEditTests.DeadOwner");
            UnityObjectSaveableOwner unityOwner = null;
            bool previousIgnoreFailingMessages = UnityEngine.TestTools.LogAssert.ignoreFailingMessages;
            try
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

                SaveManager manager = host.AddComponent<SaveManager>();
                SetPrivateInstanceField(manager, "_runtimeOwnerAborted", false);
                SetPrivateInstanceField(manager, "_serviceRegistered", true);

                unityOwner = ScriptableObject.CreateInstance<UnityObjectSaveableOwner>();
                unityOwner.name = "SaveManagerLoadPriorityConflictEditTests.DeadOwner.Owner";

                manager.Register(unityOwner);
                Assert.AreEqual(1, ReadRegisteredSaveableCount(manager));
                Assert.IsTrue(IsRegistered(manager, unityOwner));

                manager.Unregister(unityOwner);
                Assert.AreEqual(0, ReadRegisteredSaveableCount(manager));

                UnityEngine.Object.DestroyImmediate(unityOwner);

                manager.Register(unityOwner);
                Assert.AreEqual(
                    0,
                    ReadRegisteredSaveableCount(manager),
                    "A destroyed UnityEngine.Object owner was accepted, so the registry now holds a fake-null handle.");
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
                if (unityOwner != null)
                    UnityEngine.Object.DestroyImmediate(unityOwner);

                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static int ReadPrivateConstInt(string fieldName)
        {
            FieldInfo field = typeof(SaveManager).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Missing SaveManager constant: " + fieldName);
            return (int)field.GetRawConstantValue();
        }

        private static void SetPrivateInstanceField(SaveManager manager, string fieldName, object value)
        {
            FieldInfo field = typeof(SaveManager).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "Missing SaveManager field: " + fieldName);
            field.SetValue(manager, value);
        }

        private static ISaveable[] ReadRegisteredSaveables(SaveManager manager)
        {
            FieldInfo field = typeof(SaveManager).GetField("_saveables", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "Missing SaveManager field: _saveables");
            ISaveable[] saveables = field.GetValue(manager) as ISaveable[];
            Assert.IsNotNull(saveables, "SaveManager._saveables is not an ISaveable[]");
            return saveables;
        }

        private static int ReadRegisteredSaveableCount(SaveManager manager)
        {
            FieldInfo field = typeof(SaveManager).GetField("_saveableCount", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "Missing SaveManager field: _saveableCount");
            return (int)field.GetValue(manager);
        }

        private static bool IsRegistered(SaveManager manager, ISaveable candidate)
        {
            ISaveable[] saveables = ReadRegisteredSaveables(manager);
            int count = ReadRegisteredSaveableCount(manager);
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(saveables[i], candidate))
                    return true;
            }

            return false;
        }

        private static void AssertNoNullEntriesBelowCount(SaveManager manager)
        {
            ISaveable[] saveables = ReadRegisteredSaveables(manager);
            int count = ReadRegisteredSaveableCount(manager);
            for (int i = 0; i < count; i++)
            {
                Assert.IsNotNull(
                    saveables[i],
                    "Dense registry has a hole at index " + i + " below live count " + count + ".");
            }
        }

        [Test]
        public void MappedInventoryCommitCallbacksSkipStaleSaveableHandles()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string resolveBody = ExtractMethodBody(source, "private T ResolveRegisteredSaveable<T>() where T : class, ISaveable");
            string notifyBody = ExtractMethodBody(source, "private void NotifyMappedInventoryWritesCommitted()");

            AssertStaleSaveableGuardBeforeUse(resolveBody, "if (saveable is T typed)");
            AssertStaleSaveableGuardBeforeUse(notifyBody, "sink.NotifyMappedInventoryWriteCommitted();");
            StringAssert.Contains("saveable is IMappedInventoryWriteCommitSink sink", notifyBody);
        }

        [Test]
        public void MacroDatabaseCompactionBusyGateIsBestEffort()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string requestBody = ExtractMethodBody(source, "public bool TryRequestMacroDatabaseCompaction(");
            string completeBody = ExtractMethodBody(source, "public bool TryCompleteMacroDatabaseCompaction(");
            string blockBody = ExtractMethodBody(source, "private void BlockMacroDatabaseCompactionForActivePersistence()");
            string reportCleanupFailure = ExtractMethodBody(source, "private static void ReportPersistenceCleanupFailure(string operationName, Exception exception)");

            StringAssert.Contains("BlockMacroDatabaseCompactionForActivePersistence();", requestBody);
            StringAssert.Contains("BlockMacroDatabaseCompactionForActivePersistence();", completeBody);
            StringAssert.DoesNotContain("NotifyMacroDatabasePersistenceGate(true);", requestBody);
            StringAssert.DoesNotContain("NotifyMacroDatabasePersistenceGate(true);", completeBody);
            StringAssert.Contains("NotifyMacroDatabasePersistenceGateBestEffort(true, ref gateException);", blockBody);
            StringAssert.Contains("ReportPersistenceCleanupFailure(\"gate\", gateException);", blockBody);
            StringAssert.Contains("PersistenceCleanupGateContextHash", source);
            StringAssert.Contains("string.Equals(operationName, \"gate\", StringComparison.Ordinal)", reportCleanupFailure);
        }

        [Test]
        public void SaveStatusAndCompletionSignalBridgeIsBestEffort()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string statusBody = ExtractMethodBody(source, "private static void PublishSaveStatus(");
            string statusByHashBody = ExtractMethodBody(source, "private static void PublishSaveStatus(uint slotHash");
            string completedBody = ExtractMethodBody(source, "private static void PublishSaveCompleted(");
            string completedByHashBody = ExtractMethodBody(source, "private static void PublishSaveCompleted(uint slotHash");
            string reportBody = ExtractMethodBody(source, "private static void ReportPersistenceSignalBridgeFailure(Exception exception)");
            string pushBestEffort = ExtractMethodBody(source, "private static void TryPushSignalTrackedBestEffort");
            string recoveredNotification = ExtractMethodBody(source, "private static void PublishSaveRecoveredNotification(");
            string synchronizedNotification = ExtractMethodBody(source, "private static void PublishSaveSynchronizedNotification(");

            StringAssert.Contains("try", statusBody);
            StringAssert.Contains("TryPushSignalTrackedBestEffort(in status);", statusBody);
            StringAssert.Contains("TryPushSignalTrackedBestEffort(in lifecycle);", statusBody);
            StringAssert.Contains("catch (Exception exception)", statusBody);
            StringAssert.Contains("ReportPersistenceSignalBridgeFailure(exception);", statusBody);
            StringAssert.Contains("try", statusByHashBody);
            StringAssert.Contains("SlotHash = slotHash", statusByHashBody);
            StringAssert.Contains("TryPushSignalTrackedBestEffort(in status);", statusByHashBody);
            StringAssert.Contains("TryPushSignalTrackedBestEffort(in lifecycle);", statusByHashBody);
            StringAssert.Contains("catch (Exception exception)", statusByHashBody);
            StringAssert.Contains("ReportPersistenceSignalBridgeFailure(exception);", statusByHashBody);
            StringAssert.Contains("try", completedBody);
            StringAssert.Contains("TryPushSignalTrackedBestEffort(in completed);", completedBody);
            StringAssert.Contains("catch (Exception exception)", completedBody);
            StringAssert.Contains("ReportPersistenceSignalBridgeFailure(exception);", completedBody);
            StringAssert.Contains("try", completedByHashBody);
            StringAssert.Contains("SlotHash = slotHash", completedByHashBody);
            StringAssert.Contains("TryPushSignalTrackedBestEffort(in completed);", completedByHashBody);
            StringAssert.Contains("catch (Exception exception)", completedByHashBody);
            StringAssert.Contains("ReportPersistenceSignalBridgeFailure(exception);", completedByHashBody);
            StringAssert.Contains("SignalBus<TSignal>.TryPushTracked", pushBestEffort);
            StringAssert.Contains("catch (Exception exception)", pushBestEffort);
            StringAssert.Contains("ReportPersistenceSignalBridgeFailure(exception);", pushBestEffort);
            StringAssert.Contains("where TSignal : unmanaged, ISignal", source);
            StringAssert.Contains("TryPushSignalTrackedBestEffort(in notification);", recoveredNotification);
            StringAssert.Contains("TryPushSignalTrackedBestEffort(in notification);", synchronizedNotification);
            StringAssert.Contains("PersistenceSignalBridgeContextHash", source);
            StringAssert.Contains("PublishPerformanceWarningBestEffort(", reportBody);
            StringAssert.Contains("LogErrorBestEffort(\"[SaveManager] signal bridge failed: \" + exception);", reportBody);
        }

        [Test]
        public void TryRequestSaveRejectsPublishSaveEventAndStatusTogether()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string requestBody = ExtractMethodBody(source, "public bool TryRequestSave(byte slotIndex, uint sourceHash, uint operationId = 0u)");
            string processBody = ExtractMethodBody(source, "private void ProcessSaveRequest(in SaveRequestSignal signal)");

            int operationIdIndex = requestBody.IndexOf("uint resolvedOperationId = ResolveOperationId(operationId);", StringComparison.Ordinal);
            int invalidIndex = requestBody.IndexOf("if (slotIndex >= SaveEvents.ManualSlotCount)", StringComparison.Ordinal);
            int invalidErrorIndex = requestBody.IndexOf("LastOperationError = InvalidSlotNameReason;", invalidIndex, StringComparison.Ordinal);
            int invalidEventIndex = requestBody.IndexOf("SaveEvents.TryRaiseSaveFailed(0u, SaveEvents.ComputeMessageHash(InvalidSlotNameReason), InvalidSlotNameReason);", invalidIndex, StringComparison.Ordinal);
            int invalidStatusIndex = requestBody.IndexOf("PublishSaveStatus(slotIndex, new SaveStatusParams(resolvedOperationId, SaveStatusSignal.Rejected, 0f, 1u));", invalidIndex, StringComparison.Ordinal);
            int busyIndex = requestBody.IndexOf("if (_isBusy)", StringComparison.Ordinal);
            int busyReasonIndex = requestBody.IndexOf("const string reason = \"Save already in progress.\";", busyIndex, StringComparison.Ordinal);
            int busyErrorIndex = requestBody.IndexOf("LastOperationError = reason;", busyIndex, StringComparison.Ordinal);
            int busySlotIndex = requestBody.IndexOf("LastOperationSlot = slotName;", busyIndex, StringComparison.Ordinal);
            int busyEventIndex = requestBody.IndexOf("SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);", busyIndex, StringComparison.Ordinal);
            int busyStatusIndex = requestBody.IndexOf("PublishSaveStatus(slotIndex, new SaveStatusParams(resolvedOperationId, SaveStatusSignal.Rejected, 0f, 1u));", busyIndex, StringComparison.Ordinal);
            int signalIndex = requestBody.IndexOf("SaveRequestSignal signal = new SaveRequestSignal", StringComparison.Ordinal);

            Assert.GreaterOrEqual(operationIdIndex, 0);
            Assert.Greater(invalidIndex, operationIdIndex);
            Assert.Greater(invalidErrorIndex, invalidIndex);
            Assert.Greater(invalidEventIndex, invalidErrorIndex);
            Assert.Greater(invalidStatusIndex, invalidEventIndex);
            Assert.Greater(busyIndex, invalidStatusIndex);
            Assert.Greater(busyReasonIndex, busyIndex);
            Assert.Greater(busyErrorIndex, busyReasonIndex);
            Assert.Greater(busySlotIndex, busyErrorIndex);
            Assert.Greater(busyEventIndex, busySlotIndex);
            Assert.Greater(busyStatusIndex, busyEventIndex);
            Assert.Greater(signalIndex, busyStatusIndex);
            StringAssert.Contains("OperationId = resolvedOperationId,", requestBody);
            StringAssert.Contains("PublishSaveStatus(slotIndex, new SaveStatusParams(signal.OperationId, SaveStatusSignal.Queued, 0f, 0u));", requestBody);

            int processInvalidIndex = processBody.IndexOf("if (slotIndex >= SaveEvents.ManualSlotCount)", StringComparison.Ordinal);
            int processInvalidErrorIndex = processBody.IndexOf("LastOperationError = InvalidSlotNameReason;", processInvalidIndex, StringComparison.Ordinal);
            int processInvalidSlotIndex = processBody.IndexOf("LastOperationSlot = string.Empty;", processInvalidIndex, StringComparison.Ordinal);
            int processInvalidEventIndex = processBody.IndexOf("SaveEvents.TryRaiseSaveFailed(0u, SaveEvents.ComputeMessageHash(InvalidSlotNameReason), InvalidSlotNameReason);", processInvalidIndex, StringComparison.Ordinal);
            int processInvalidStatusIndex = processBody.IndexOf("PublishSaveStatus(slotIndex, new SaveStatusParams(operationId, SaveStatusSignal.Rejected, 0f, 1u));", processInvalidIndex, StringComparison.Ordinal);
            int processBusyIndex = processBody.IndexOf("if (_isBusy)", StringComparison.Ordinal);
            int processBusyReasonIndex = processBody.IndexOf("const string reason = \"Save already in progress.\";", processBusyIndex, StringComparison.Ordinal);
            int processBusyErrorIndex = processBody.IndexOf("LastOperationError = reason;", processBusyIndex, StringComparison.Ordinal);
            int processBusySlotIndex = processBody.IndexOf("LastOperationSlot = slotName;", processBusyIndex, StringComparison.Ordinal);
            int processBusyEventIndex = processBody.IndexOf("SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);", processBusyIndex, StringComparison.Ordinal);
            int processBusyStatusIndex = processBody.IndexOf("PublishSaveStatus(slotIndex, new SaveStatusParams(operationId, SaveStatusSignal.Rejected, 0f, 1u));", processBusyIndex, StringComparison.Ordinal);

            Assert.GreaterOrEqual(processInvalidIndex, 0);
            Assert.Greater(processInvalidErrorIndex, processInvalidIndex);
            Assert.Greater(processInvalidSlotIndex, processInvalidErrorIndex);
            Assert.Greater(processInvalidEventIndex, processInvalidSlotIndex);
            Assert.Greater(processInvalidStatusIndex, processInvalidEventIndex);
            Assert.Greater(processBusyIndex, processInvalidStatusIndex);
            Assert.Greater(processBusyReasonIndex, processBusyIndex);
            Assert.Greater(processBusyErrorIndex, processBusyReasonIndex);
            Assert.Greater(processBusySlotIndex, processBusyErrorIndex);
            Assert.Greater(processBusyEventIndex, processBusySlotIndex);
            Assert.Greater(processBusyStatusIndex, processBusyEventIndex);
        }

        [Test]
        public void SaveRequestsRejectDuringRespawnReconciliationBeforeSnapshot()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string survivalSource = ReadProjectFile("Assets/_Project/Scripts/HectonSurvivalSystem.cs");
            string healthSource = ReadProjectFile("Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs");
            string requestBody = ExtractMethodBody(source, "public bool TryRequestSave(byte slotIndex, uint sourceHash, uint operationId = 0u)");
            string processBody = ExtractMethodBody(source, "private void ProcessSaveRequest(in SaveRequestSignal signal)");
            string saveBody = ExtractMethodBody(source, "private async Awaitable SaveGameAsyncInternal(string slotName, byte slotIndex, uint operationId)");
            string rejectBody = ExtractMethodBody(source, "private bool TryRejectSaveDuringRespawnReconciliation(");
            string gateBody = ExtractMethodBody(source, "private bool HasPendingRespawnReconciliationSaveGate()");

            StringAssert.Contains("private const string RespawnReconciliationInProgressReason = \"Save blocked during respawn reconciliation.\";", source);
            StringAssert.Contains("internal bool RespawnReconciliationPending => _pendingRespawnReconciliationSequence != 0u;", survivalSource);
            StringAssert.Contains("internal bool RespawnReconciliationPending => _pendingRespawnReconciliationSequence != 0u;", healthSource);
            StringAssert.Contains("saveable is HectonSurvivalSystem survival && survival.RespawnReconciliationPending", gateBody);
            StringAssert.Contains("saveable is HectonPlayerHealth health && health.RespawnReconciliationPending", gateBody);
            StringAssert.Contains("LastOperationError = RespawnReconciliationInProgressReason;", rejectBody);
            StringAssert.Contains("LastOperationSlot = slotName ?? string.Empty;", rejectBody);
            StringAssert.Contains("SaveEvents.TryRaiseSaveFailed(", rejectBody);
            StringAssert.Contains("SaveEvents.ComputeMessageHash(RespawnReconciliationInProgressReason)", rejectBody);
            StringAssert.Contains("PublishSaveStatusForSlotName(slotIndex, slotName, new SaveStatusParams(operationId, SaveStatusSignal.Rejected, 0f, 1u));", rejectBody);
            StringAssert.Contains("RecordAsyncPersistenceTelemetry(operationId, LastOperationSlot, elapsedMs, 0L, 0, 0, 1u);", rejectBody);
            StringAssert.Contains("PublishSaveCompletedForSlotName(slotIndex, slotName, operationId, elapsedMs, 0L, succeeded: false);", rejectBody);
            StringAssert.Contains("PublishSaveStatusForSlotName(slotIndex, slotName, new SaveStatusParams(operationId, SaveStatusSignal.Failed, 1f, 1u));", rejectBody);

            int requestGateIndex = requestBody.IndexOf("if (TryRejectSaveDuringRespawnReconciliation(slotIndex, resolvedOperationId, slotName))", StringComparison.Ordinal);
            int requestSignalIndex = requestBody.IndexOf("SaveRequestSignal signal = new SaveRequestSignal", StringComparison.Ordinal);
            Assert.GreaterOrEqual(requestGateIndex, 0);
            Assert.Greater(requestSignalIndex, requestGateIndex);

            int processGateIndex = processBody.IndexOf("if (TryRejectSaveDuringRespawnReconciliation(slotIndex, operationId, slotName))", StringComparison.Ordinal);
            int processStartIndex = processBody.IndexOf("_ = SaveGameAsyncInternal(slotName, slotIndex, operationId);", StringComparison.Ordinal);
            Assert.GreaterOrEqual(processGateIndex, 0);
            Assert.Greater(processStartIndex, processGateIndex);

            int directGateIndex = saveBody.IndexOf("if (TryRejectSaveDuringRespawnReconciliation(slotIndex, operationId, slotName))", StringComparison.Ordinal);
            int floatingOriginIndex = saveBody.IndexOf("if (HectonFloatingOrigin.IsShiftInProgress || HectonFloatingOrigin.IsPhysicsPausedForShift)", StringComparison.Ordinal);
            int busySetIndex = saveBody.IndexOf("_isBusy = true;", StringComparison.Ordinal);
            int nextFrameIndex = saveBody.IndexOf("await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(destroyCancellationToken);", StringComparison.Ordinal);
            int activeGateIndex = saveBody.IndexOf("activeSaveStarted: true", nextFrameIndex, StringComparison.Ordinal);
            int thumbnailIndex = saveBody.IndexOf("thumbnailTicket = SaveThumbnailSystem.CaptureThumbnailForSave(slotName, slotIndex, operationId);", StringComparison.Ordinal);
            Assert.GreaterOrEqual(directGateIndex, 0);
            Assert.Greater(floatingOriginIndex, directGateIndex);
            Assert.Greater(busySetIndex, directGateIndex);
            Assert.GreaterOrEqual(nextFrameIndex, 0);
            Assert.Greater(activeGateIndex, nextFrameIndex);
            Assert.Greater(thumbnailIndex, activeGateIndex);
        }

        [Test]
        public void DirectSaveInvalidSlotPublishesSaveEventAndStatusTogether()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");

            // The invalid-slot rejection is no longer inside SaveGameAsyncInternal. SaveManager
            // extracted every pre-flight rejection into TryPassPreflightChecks (SaveManager.cs:5329),
            // which SaveGameAsyncInternal (SaveManager.cs:5394) now calls, and the slot-name branch sits
            // at SaveManager.cs:5341-5347. Reading the old method's body found the anchor literal
            // nowhere, produced startIndex -1, and the next IndexOf threw ArgumentOutOfRangeException
            // before a single assertion ran - so this test could not fail for its own reason, and could
            // not pass either.
            //
            // BOUNDARY: this is still a guard on the literal text of a .cs file. It proves the four
            // statements are WRITTEN in this order, not that they EXECUTE in it. Proving the execution
            // order needs the async save path driven with a real SaveEvents sink observing the emitted
            // (event, status) pair - SaveGameAsyncInternal is a private async Awaitable on a
            // MonoBehaviour, so that is a PlayMode test, not reachable from this EditMode assembly.
            const string preflightSignature =
                "private bool TryPassPreflightChecks(ref string slotName, byte slotIndex, uint operationId)";
            const string invalidSlotAnchor = "if (!TryResolveSafeSlotName(slotName, out slotName))";
            string preflightBody = ExtractMethodBody(source, preflightSignature);

            int invalidIndex = preflightBody.IndexOf(invalidSlotAnchor, StringComparison.Ordinal);
            int invalidErrorIndex = IndexOfAfterAnchor(
                preflightBody,
                "LastOperationError = InvalidSlotNameReason;",
                invalidIndex,
                invalidSlotAnchor);
            int invalidEventIndex = IndexOfAfterAnchor(
                preflightBody,
                "SaveEvents.TryRaiseSaveFailed(0u, SaveEvents.ComputeMessageHash(InvalidSlotNameReason), InvalidSlotNameReason);",
                invalidIndex,
                invalidSlotAnchor);
            int invalidStatusIndex = IndexOfAfterAnchor(
                preflightBody,
                "PublishSaveStatus(slotIndex, operationId, SaveStatusSignal.Rejected, 0f, 1u);",
                invalidIndex,
                invalidSlotAnchor);
            int invalidRejectIndex = IndexOfAfterAnchor(
                preflightBody,
                "return false;",
                invalidIndex,
                invalidSlotAnchor);

            Assert.GreaterOrEqual(invalidIndex, 0, invalidSlotAnchor);
            Assert.Greater(invalidErrorIndex, invalidIndex);
            Assert.Greater(invalidEventIndex, invalidErrorIndex);
            Assert.Greater(invalidStatusIndex, invalidEventIndex);
            Assert.Greater(invalidRejectIndex, invalidStatusIndex);
        }

        [Test]
        public void DirectLoadRejectedPathsPublishLoadEventAndStatusTogether()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string signalContract = ReadProjectFile("Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs");
            string loadBody = ExtractMethodBody(source, "public async Awaitable LoadGameAsync(string slotName)");

            StringAssert.Contains("public const byte FailureFlag = 1 << 0;", signalContract);
            StringAssert.Contains("public const byte LoadOperationFlag = 1 << 2;", signalContract);
            StringAssert.Contains("private const uint LoadStatusFlags = SaveStatusSignal.LoadOperationFlag;", source);
            StringAssert.Contains("private const uint LoadFailureStatusFlags = SaveStatusSignal.FailureFlag | SaveStatusSignal.LoadOperationFlag;", source);

            int operationIdIndex = loadBody.IndexOf("uint operationId = ResolveOperationId(0u);", StringComparison.Ordinal);
            int initialSlotIndex = loadBody.IndexOf("byte slotIndex = ResolveManualSlotIndex(slotName);", operationIdIndex, StringComparison.Ordinal);
            int invalidIndex = loadBody.IndexOf("if (!TryResolveSafeSlotName(slotName, out slotName))", StringComparison.Ordinal);
            int invalidErrorIndex = loadBody.IndexOf("LastOperationError = InvalidSlotNameReason;", invalidIndex, StringComparison.Ordinal);
            int invalidEventIndex = loadBody.IndexOf("SaveEvents.TryRaiseLoadFailed(0u, SaveEvents.ComputeMessageHash(InvalidSlotNameReason), InvalidSlotNameReason);", invalidIndex, StringComparison.Ordinal);
            int invalidStatusIndex = loadBody.IndexOf("PublishSaveStatus(slotIndex, new SaveStatusParams(operationId, SaveStatusSignal.Rejected, 0f, LoadFailureStatusFlags));", invalidIndex, StringComparison.Ordinal);
            int validatedSlotIndex = loadBody.IndexOf("slotIndex = ResolveManualSlotIndex(slotName);", invalidIndex, StringComparison.Ordinal);
            int busyIndex = loadBody.IndexOf("if (_isBusy)", validatedSlotIndex, StringComparison.Ordinal);
            int busyEventIndex = loadBody.IndexOf("SaveEvents.TryRaiseLoadFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);", busyIndex, StringComparison.Ordinal);
            int busyStatusIndex = loadBody.IndexOf("PublishSaveStatusForSlotName(slotIndex, slotName, new SaveStatusParams(operationId, SaveStatusSignal.Rejected, 0f, LoadFailureStatusFlags));", busyIndex, StringComparison.Ordinal);
            int missingSaveIndex = loadBody.IndexOf("if (!SaveExists(slotName))", busyStatusIndex, StringComparison.Ordinal);
            int missingSaveEventIndex = loadBody.IndexOf("SaveEvents.TryRaiseLoadFailed(SaveEvents.ComputeSlotHash(slotName), SaveEvents.ComputeMessageHash(reason), reason);", missingSaveIndex, StringComparison.Ordinal);
            int missingSaveStatusIndex = loadBody.IndexOf("PublishSaveStatusForSlotName(slotIndex, slotName, new SaveStatusParams(operationId, SaveStatusSignal.Rejected, 0f, LoadFailureStatusFlags));", missingSaveIndex, StringComparison.Ordinal);
            int startedEventIndex = loadBody.IndexOf("SaveEvents.TryRaiseLoadStarted(SaveEvents.ComputeSlotHash(slotName));", missingSaveStatusIndex, StringComparison.Ordinal);
            int startedStatusIndex = loadBody.IndexOf("PublishSaveStatusForSlotName(slotIndex, slotName, new SaveStatusParams(operationId, SaveStatusSignal.InProgress, 0.08f, LoadStatusFlags));", startedEventIndex, StringComparison.Ordinal);
            int startedStageIndex = loadBody.IndexOf("ReportLoadPipelineStage(LoadingPipelineStage.PagingSectors, 0.08f);", startedStatusIndex, StringComparison.Ordinal);
            int completedSignalIndex = loadBody.IndexOf("PublishSaveCompletedForSlotName(slotIndex, slotName, operationId, totalTimer.ElapsedMilliseconds, 0L, succeeded: true);", startedStageIndex, StringComparison.Ordinal);
            int completedStatusIndex = loadBody.IndexOf("PublishSaveStatusForSlotName(slotIndex, slotName, new SaveStatusParams(operationId, SaveStatusSignal.Completed, 1f, LoadStatusFlags));", completedSignalIndex, StringComparison.Ordinal);
            int completedEventIndex = loadBody.IndexOf("RaiseLoadCompletedWithBackpressureRecovery(SaveEvents.ComputeSlotHash(slotName));", completedStatusIndex, StringComparison.Ordinal);

            Assert.GreaterOrEqual(operationIdIndex, 0);
            Assert.Greater(initialSlotIndex, operationIdIndex);
            Assert.Greater(invalidIndex, initialSlotIndex);
            Assert.Greater(invalidErrorIndex, invalidIndex);
            Assert.Greater(invalidEventIndex, invalidErrorIndex);
            Assert.Greater(invalidStatusIndex, invalidEventIndex);
            Assert.Greater(validatedSlotIndex, invalidStatusIndex);
            Assert.Greater(busyIndex, validatedSlotIndex);
            Assert.Greater(busyEventIndex, busyIndex);
            Assert.Greater(busyStatusIndex, busyEventIndex);
            Assert.Greater(missingSaveIndex, busyStatusIndex);
            Assert.Greater(missingSaveEventIndex, missingSaveIndex);
            Assert.Greater(missingSaveStatusIndex, missingSaveEventIndex);
            Assert.Greater(startedEventIndex, missingSaveStatusIndex);
            Assert.Greater(startedStatusIndex, startedEventIndex);
            Assert.Greater(startedStageIndex, startedStatusIndex);
            Assert.Greater(completedSignalIndex, startedStageIndex);
            Assert.Greater(completedStatusIndex, completedSignalIndex);
            Assert.Greater(completedEventIndex, completedStatusIndex);
        }

        [Test]
        public void SuccessfulSaveLoadTerminalEventsRetryAfterBackpressureFlush()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string saveBody = ExtractMethodBody(source, "private async Awaitable SaveGameAsyncInternal(string slotName, byte slotIndex, uint operationId)");
            string loadBody = ExtractMethodBody(source, "public async Awaitable LoadGameAsync(string slotName)");
            string saveHelper = ExtractMethodBody(source, "private static void RaiseSaveCompletedWithBackpressureRecovery(");
            string loadHelper = ExtractMethodBody(source, "private static void RaiseLoadCompletedWithBackpressureRecovery(");

            int saveStatusIndex = saveBody.IndexOf("PublishSaveStatusForSlotName(slotIndex, slotName, new SaveStatusParams(operationId, SaveStatusSignal.Completed, 1f, 0u));", StringComparison.Ordinal);
            int saveEventIndex = saveBody.IndexOf("RaiseSaveCompletedWithBackpressureRecovery(SaveEvents.ComputeSlotHash(slotName));", saveStatusIndex, StringComparison.Ordinal);
            int saveNotifyIndex = saveBody.IndexOf("PublishSaveSynchronizedNotification(slotName);", saveEventIndex, StringComparison.Ordinal);
            int loadStatusIndex = loadBody.IndexOf("PublishSaveStatusForSlotName(slotIndex, slotName, new SaveStatusParams(operationId, SaveStatusSignal.Completed, 1f, LoadStatusFlags));", StringComparison.Ordinal);
            int loadEventIndex = loadBody.IndexOf("RaiseLoadCompletedWithBackpressureRecovery(SaveEvents.ComputeSlotHash(slotName));", loadStatusIndex, StringComparison.Ordinal);

            Assert.GreaterOrEqual(saveStatusIndex, 0);
            Assert.Greater(saveEventIndex, saveStatusIndex);
            Assert.Greater(saveNotifyIndex, saveEventIndex);
            Assert.GreaterOrEqual(loadStatusIndex, 0);
            Assert.Greater(loadEventIndex, loadStatusIndex);
            StringAssert.Contains("if (SaveEvents.TryRaiseSaveCompleted(slotHash))", saveHelper);
            StringAssert.Contains("SaveEvents.FlushPending();", saveHelper);
            StringAssert.Contains("SaveEvents.TryRaiseSaveCompleted(slotHash);", saveHelper);
            StringAssert.Contains("if (SaveEvents.TryRaiseLoadCompleted(slotHash))", loadHelper);
            StringAssert.Contains("SaveEvents.FlushPending();", loadHelper);
            StringAssert.Contains("SaveEvents.TryRaiseLoadCompleted(slotHash);", loadHelper);
            StringAssert.DoesNotContain("SaveEvents.TryRaiseSaveCompleted(SaveEvents.ComputeSlotHash(slotName));", saveBody);
            StringAssert.DoesNotContain("SaveEvents.TryRaiseLoadCompleted(SaveEvents.ComputeSlotHash(slotName));", loadBody);
        }

        [Test]
        public void ServiceUnavailableSaveAndLoadPublishTerminalEvents()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string requestBody = ExtractMethodBody(source, "public bool TryRequestSave(byte slotIndex, uint sourceHash, uint operationId = 0u)");
            string saveBody = ExtractMethodBody(source, "private async Awaitable SaveGameAsyncInternal(string slotName, byte slotIndex, uint operationId)");
            string loadBody = ExtractMethodBody(source, "public async Awaitable LoadGameAsync(string slotName)");
            string unavailableSlotContext = ExtractMethodBody(source, "private static uint ResolveUnavailableSlotContext(");

            StringAssert.Contains("private const string SaveServiceUnavailableReason = \"Save service unavailable.\";", source);
            StringAssert.Contains("TryResolveSafeSlotName(slotName, out safeSlotName)", unavailableSlotContext);
            StringAssert.Contains("return ComputeSlotHash(safeSlotName);", unavailableSlotContext);
            StringAssert.Contains("safeSlotName = string.Empty;", unavailableSlotContext);
            StringAssert.Contains("return ResolveSlotHash(slotIndex);", unavailableSlotContext);
            AssertUnavailableRequestSaveBlockPublishesEventAndStatus(requestBody, "resolvedOperationId");
            AssertUnavailableDirectSaveBlockPublishesSlotContextEventAndStatus(saveBody, "operationId");
            AssertUnavailableDirectLoadBlockPublishesSlotContextEventAndStatus(loadBody);
        }

        private static void AssertUnavailableRequestSaveBlockPublishesEventAndStatus(string methodBody, string operationIdName)
        {
            int unavailableIndex = methodBody.IndexOf("if (_runtimeOwnerAborted || !_serviceRegistered)", StringComparison.Ordinal);
            int errorIndex = methodBody.IndexOf("LastOperationError = SaveServiceUnavailableReason;", unavailableIndex, StringComparison.Ordinal);
            int slotIndex = methodBody.IndexOf("LastOperationSlot = slotIndex < SaveEvents.ManualSlotCount", unavailableIndex, StringComparison.Ordinal);
            int slotNameIndex = methodBody.IndexOf("SaveEvents.ResolveManualSlotName(slotIndex)", slotIndex, StringComparison.Ordinal);
            int eventIndex = methodBody.IndexOf("SaveEvents.TryRaiseSaveFailed(ResolveSlotHash(slotIndex), SaveEvents.ComputeMessageHash(SaveServiceUnavailableReason), SaveServiceUnavailableReason);", unavailableIndex, StringComparison.Ordinal);
            int statusIndex = methodBody.IndexOf($"PublishSaveStatus(slotIndex, new SaveStatusParams({operationIdName}, SaveStatusSignal.Rejected, 0f, 1u));", unavailableIndex, StringComparison.Ordinal);
            int returnIndex = methodBody.IndexOf("return", unavailableIndex, StringComparison.Ordinal);

            Assert.GreaterOrEqual(unavailableIndex, 0);
            Assert.Greater(errorIndex, unavailableIndex);
            Assert.Greater(slotIndex, errorIndex);
            Assert.Greater(slotNameIndex, slotIndex);
            Assert.Greater(eventIndex, slotNameIndex);
            Assert.Greater(statusIndex, eventIndex);
            Assert.Greater(returnIndex, statusIndex);
        }

        private static void AssertUnavailableDirectSaveBlockPublishesSlotContextEventAndStatus(string methodBody, string operationIdName)
        {
            int unavailableIndex = methodBody.IndexOf("if (_runtimeOwnerAborted || !_serviceRegistered)", StringComparison.Ordinal);
            int contextIndex = methodBody.IndexOf("uint unavailableSlotHash = ResolveUnavailableSlotContext(slotName, slotIndex, out string unavailableSlotName);", unavailableIndex, StringComparison.Ordinal);
            int errorIndex = methodBody.IndexOf("LastOperationError = SaveServiceUnavailableReason;", unavailableIndex, StringComparison.Ordinal);
            int slotIndex = methodBody.IndexOf("LastOperationSlot = unavailableSlotName;", unavailableIndex, StringComparison.Ordinal);
            int eventIndex = methodBody.IndexOf("SaveEvents.TryRaiseSaveFailed(unavailableSlotHash, SaveEvents.ComputeMessageHash(SaveServiceUnavailableReason), SaveServiceUnavailableReason);", unavailableIndex, StringComparison.Ordinal);
            int statusIndex = methodBody.IndexOf($"PublishSaveStatus(unavailableSlotHash, new SaveStatusParams({operationIdName}, SaveStatusSignal.Rejected, 0f, 1u));", unavailableIndex, StringComparison.Ordinal);
            int returnIndex = methodBody.IndexOf("return", unavailableIndex, StringComparison.Ordinal);

            Assert.GreaterOrEqual(unavailableIndex, 0);
            Assert.Greater(contextIndex, unavailableIndex);
            Assert.Greater(errorIndex, contextIndex);
            Assert.Greater(slotIndex, errorIndex);
            Assert.Greater(eventIndex, slotIndex);
            Assert.Greater(statusIndex, eventIndex);
            Assert.Greater(returnIndex, statusIndex);
        }

        private static void AssertUnavailableDirectLoadBlockPublishesSlotContextEventAndStatus(string methodBody)
        {
            int unavailableIndex = methodBody.IndexOf("if (_runtimeOwnerAborted || !_serviceRegistered)", StringComparison.Ordinal);
            int contextIndex = methodBody.IndexOf("uint unavailableSlotHash = ResolveUnavailableSlotContext(slotName, byte.MaxValue, out string unavailableSlotName);", unavailableIndex, StringComparison.Ordinal);
            int errorIndex = methodBody.IndexOf("LastOperationError = SaveServiceUnavailableReason;", unavailableIndex, StringComparison.Ordinal);
            int slotIndex = methodBody.IndexOf("LastOperationSlot = unavailableSlotName;", unavailableIndex, StringComparison.Ordinal);
            int eventIndex = methodBody.IndexOf("SaveEvents.TryRaiseLoadFailed(unavailableSlotHash, SaveEvents.ComputeMessageHash(SaveServiceUnavailableReason), SaveServiceUnavailableReason);", unavailableIndex, StringComparison.Ordinal);
            int statusIndex = methodBody.IndexOf("PublishSaveStatus(unavailableSlotHash, new SaveStatusParams(operationId, SaveStatusSignal.Rejected, 0f, LoadFailureStatusFlags));", unavailableIndex, StringComparison.Ordinal);
            int returnIndex = methodBody.IndexOf("return;", unavailableIndex, StringComparison.Ordinal);

            Assert.GreaterOrEqual(unavailableIndex, 0);
            Assert.Greater(contextIndex, unavailableIndex);
            Assert.Greater(errorIndex, contextIndex);
            Assert.Greater(slotIndex, errorIndex);
            Assert.Greater(eventIndex, slotIndex);
            Assert.Greater(statusIndex, eventIndex);
            Assert.Greater(returnIndex, statusIndex);
        }

        [Test]
        public void DirectSaveStatusAndCompletionSignalsUseResolvedSlotNameAfterValidation()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string saveBody = ExtractMethodBody(source, "private async Awaitable SaveGameAsyncInternal(string slotName, byte slotIndex, uint operationId)");
            string resolveSlotHash = ExtractMethodBody(source, "private static uint ResolveSlotHash(byte slotIndex, string slotName)");
            string publishStatusForSlotName = ExtractMethodBody(source, "private static void PublishSaveStatusForSlotName(");
            string publishCompletedForSlotName = ExtractMethodBody(source, "private static void PublishSaveCompletedForSlotName(");

            int validatedIndex = saveBody.IndexOf("LastOperationSlot = slotName;", StringComparison.Ordinal);
            int busyStatusIndex = saveBody.IndexOf("PublishSaveStatusForSlotName(slotIndex, slotName, new SaveStatusParams(operationId, SaveStatusSignal.Rejected, 0f, 1u));", validatedIndex, StringComparison.Ordinal);
            int startedStatusIndex = saveBody.IndexOf("PublishSaveStatusForSlotName(slotIndex, slotName, new SaveStatusParams(operationId, SaveStatusSignal.InProgress, 0.05f, 0u));", validatedIndex, StringComparison.Ordinal);
            int failedCompletionIndex = saveBody.IndexOf("PublishSaveCompletedForSlotName(slotIndex, slotName, operationId, totalTimer.ElapsedMilliseconds, 0L, succeeded: false);", validatedIndex, StringComparison.Ordinal);
            int successfulCompletionIndex = saveBody.IndexOf("PublishSaveCompletedForSlotName(slotIndex, slotName, operationId, totalTimer.ElapsedMilliseconds, compressedSizeBytes, succeeded: true);", validatedIndex, StringComparison.Ordinal);
            int successfulStatusIndex = saveBody.IndexOf("PublishSaveStatusForSlotName(slotIndex, slotName, new SaveStatusParams(operationId, SaveStatusSignal.Completed, 1f, 0u));", successfulCompletionIndex, StringComparison.Ordinal);

            Assert.GreaterOrEqual(validatedIndex, 0);
            Assert.Greater(busyStatusIndex, validatedIndex);
            Assert.Greater(startedStatusIndex, busyStatusIndex);
            Assert.Greater(failedCompletionIndex, startedStatusIndex);
            Assert.Greater(successfulCompletionIndex, failedCompletionIndex);
            Assert.Greater(successfulStatusIndex, successfulCompletionIndex);
            StringAssert.Contains("if (slotIndex < SaveEvents.ManualSlotCount)", resolveSlotHash);
            StringAssert.Contains("return ResolveSlotHash(slotIndex);", resolveSlotHash);
            StringAssert.Contains("return string.IsNullOrEmpty(slotName) ? 0u : ComputeSlotHash(slotName);", resolveSlotHash);
            StringAssert.Contains("PublishSaveStatus(ResolveSlotHash(slotIndex, slotName), in statusParams);", publishStatusForSlotName);
            StringAssert.Contains("PublishSaveCompleted(ResolveSlotHash(slotIndex, slotName), operationId, durationMs, compressedSizeBytes, succeeded);", publishCompletedForSlotName);
        }

        [Test]
        public void LoadingScreenCacheRejectsStaleRegistryReplacementDuringLoad()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string serviceReplaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string resolveLoadingScreen = ExtractMethodBody(source, "private LoadingScreenController ResolveLoadingScreenController()");
            string cacheLoadingScreen = ExtractMethodBody(source, "private void CacheLoadingScreenController(");

            StringAssert.Contains("GlobalRegistryServiceSlot.LoadingScreenRuntime", serviceReplaced);
            StringAssert.Contains("CacheLoadingScreenController(currentService as LoadingScreenController);", serviceReplaced);
            StringAssert.Contains("ReferenceEquals(loadingScreen, GlobalRegistry.LoadingScreen)", resolveLoadingScreen);
            StringAssert.Contains("_cachedLoadingScreenController = null;", resolveLoadingScreen);
            StringAssert.Contains("CacheLoadingScreenController(loadingScreen);", resolveLoadingScreen);
            StringAssert.Contains("_cachedLoadingScreenController = IsLoadingScreenControllerUsable(loadingScreen) ? loadingScreen : null;", cacheLoadingScreen);
            StringAssert.DoesNotContain("_cachedLoadingScreenController = GlobalRegistry.LoadingScreen", source);
        }

        private static void AssertStaleSaveableGuardBeforeUse(string methodBody, string useNeedle)
        {
            int aliveIndex = methodBody.IndexOf("if (!IsAlive(saveable))", StringComparison.Ordinal);
            int dirtyIndex = methodBody.IndexOf("_registryDirty = true;", StringComparison.Ordinal);
            int useIndex = methodBody.IndexOf(useNeedle, StringComparison.Ordinal);

            Assert.GreaterOrEqual(aliveIndex, 0);
            Assert.Greater(dirtyIndex, aliveIndex);
            Assert.Greater(useIndex, dirtyIndex);
            StringAssert.Contains("ISaveable saveable = _saveables[i];", methodBody);
        }

        [Test]
        public void SaveAndLoadCleanupFaultsCannotLeavePersistenceGateBlocked()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string saveBody = ExtractMethodBody(source, "private async Awaitable SaveGameAsyncInternal(string slotName, byte slotIndex, uint operationId)");
            string loadBody = ExtractMethodBody(source, "public async Awaitable LoadGameAsync(string slotName)");
            string disposeTransientBestEffort = ExtractMethodBody(source, "private static void DisposeTransientNativeArrayBestEffort<T>(");
            string releaseWriteBuffersBestEffort = ExtractMethodBody(source, "private static void ReleaseWriteBuffersBestEffort(");
            string reportCleanupFailure = ExtractMethodBody(source, "private static void ReportPersistenceCleanupFailure(string operationName, Exception exception)");

            StringAssert.Contains("PersistenceCleanupFailureTelemetryHash", source);
            StringAssert.Contains("NativeTransientMemoryLifetime", disposeTransientBestEffort);
            StringAssert.Contains("ReleaseWriteBuffers(rawBuffer, ownsRawBuffer, compressedBuffer, ownsCompressedBuffer);", releaseWriteBuffersBestEffort);
            StringAssert.Contains("CaptureFirstCleanupException(ref firstException, exception);", releaseWriteBuffersBestEffort);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", reportCleanupFailure);
            StringAssert.Contains("catch (Exception telemetryException)", reportCleanupFailure);
            StringAssert.Contains("cleanup telemetry failed", reportCleanupFailure);
            StringAssert.Contains("LogError(\"[SaveManager] \" + operationName + \" cleanup failed: \" + exception);", reportCleanupFailure);

            StringAssert.DoesNotContain("ReleaseSnapshotPause(operationId);", saveBody);
            StringAssert.Contains("ReleaseSnapshotPauseBestEffort(operationId, ref cleanupException);", saveBody);
            StringAssert.Contains("ReleaseSnapshotPauseBestEffort(operationId, ref snapshotPauseReleaseException);", saveBody);
            StringAssert.Contains("ReportPersistenceCleanupFailure(\"save\", snapshotPauseReleaseException);", saveBody);
            AssertOperationCleanupBlock(saveBody, "save");
            AssertOperationCleanupBlock(loadBody, "load");
            AssertOperationStartupGateFailureBlock(
                saveBody,
                "save",
                "Save persistence gate request failed.",
                "SaveEvents.TryRaiseSaveFailed(SaveEvents.ComputeSlotHash(slotName)",
                "PublishSaveStatusForSlotName(slotIndex, slotName, new SaveStatusParams(operationId, SaveStatusSignal.Failed", requireThumbnailAfterGate: true);
            AssertOperationStartupGateFailureBlock(
                loadBody, "load",
                "Load persistence gate request failed.",
                "SaveEvents.TryRaiseLoadFailed(SaveEvents.ComputeSlotHash(slotName)",
                "PublishSaveStatusForSlotName(slotIndex, slotName, new SaveStatusParams(operationId, SaveStatusSignal.Failed", requireThumbnailAfterGate: false);
        }

        private static MethodInfo GetPrivateStaticMethod(string methodName)
        {
            MethodInfo method = typeof(SaveManager).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "Missing SaveManager method: " + methodName);
            return method;
        }

        private static string ReadProjectFile(string relativePath)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(root, relativePath));
        }

        /// <summary>
        /// Ordinal <c>IndexOf</c> measured from an anchor index this fixture has already located.
        /// <para>
        /// The bare <c>body.IndexOf(literal, anchorIndex, StringComparison.Ordinal)</c> chains this
        /// fixture is built from throw <see cref="ArgumentOutOfRangeException"/> the moment the ANCHOR
        /// literal stops matching, because -1 is not a legal <c>startIndex</c>. The throw lands on the
        /// lookup line, several lines above the <c>Assert.GreaterOrEqual(anchorIndex, 0)</c> written
        /// specifically to name the missing literal - so the run reports "Index was out of range" and
        /// says nothing about which part of SaveManager.cs moved. Four tests in this fixture failed
        /// exactly that way in all three recorded batchmode runs. Anchoring through here fails on the
        /// anchor, and names it.
        /// </para>
        /// </summary>
        private static int IndexOfAfterAnchor(string body, string literal, int anchorIndex, string anchorLabel)
        {
            Assert.IsNotNull(body);
            Assert.GreaterOrEqual(
                anchorIndex,
                0,
                "Anchor literal is missing from the extracted method body, so nothing ordered after it can be located: " + anchorLabel);
            return body.IndexOf(literal, anchorIndex, StringComparison.Ordinal);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, "Missing method signature: " + signature);
            int open = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(open, 0, "Missing method open brace: " + signature);

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(open, i - open + 1);
                }
            }

            Assert.Fail("Missing method close brace: " + signature);
            return string.Empty;
        }

        private static void AssertOperationCleanupBlock(string methodBody, string operationName)
        {
            int cleanupIndex = methodBody.LastIndexOf("Exception cleanupException = null;", StringComparison.Ordinal);
            Assert.GreaterOrEqual(cleanupIndex, 0, operationName);

            int disposeIndex = methodBody.IndexOf("DisposeTransientNativeArrayBestEffort(", cleanupIndex, StringComparison.Ordinal);
            int busyIndex = methodBody.IndexOf("_isBusy = false;", cleanupIndex, StringComparison.Ordinal);
            int gateIndex = methodBody.IndexOf("NotifyMacroDatabasePersistenceGateBestEffort(false, ref cleanupException);", cleanupIndex, StringComparison.Ordinal);
            int reportIndex = methodBody.IndexOf("ReportPersistenceCleanupFailure(\"" + operationName + "\", cleanupException);", cleanupIndex, StringComparison.Ordinal);

            Assert.Greater(disposeIndex, cleanupIndex, operationName);
            Assert.Greater(busyIndex, cleanupIndex, operationName);
            Assert.Greater(gateIndex, busyIndex, operationName);
            Assert.Greater(reportIndex, gateIndex, operationName);

            string cleanupBlock = methodBody.Substring(cleanupIndex);
            StringAssert.DoesNotContain("ThrowFirstDisposeException(cleanupException)", cleanupBlock);
            StringAssert.DoesNotContain("NotifyMacroDatabasePersistenceGate(false);", cleanupBlock);
        }

        private static void AssertOperationStartupGateFailureBlock(
            string methodBody,
            string operationName,
            string reason,
            string failureEventNeedle,
            string statusNeedle,
            bool requireThumbnailAfterGate)
        {
            int busyIndex = methodBody.IndexOf("_isBusy = true;", StringComparison.Ordinal);
            int startupIndex = methodBody.IndexOf("Exception startupException = null;", busyIndex, StringComparison.Ordinal);
            int gateIndex = methodBody.IndexOf("NotifyMacroDatabasePersistenceGateBestEffort(true, ref startupException);", startupIndex, StringComparison.Ordinal);
            int failureBlockIndex = methodBody.IndexOf("if (startupException != null)", gateIndex, StringComparison.Ordinal);
            int reasonIndex = methodBody.IndexOf(reason, failureBlockIndex, StringComparison.Ordinal);
            int warningIndex = methodBody.IndexOf("LogWarningBestEffort(", reasonIndex, StringComparison.Ordinal);
            int eventIndex = methodBody.IndexOf(failureEventNeedle, failureBlockIndex, StringComparison.Ordinal);
            int releaseBusyIndex = methodBody.IndexOf("_isBusy = false;", failureBlockIndex, StringComparison.Ordinal);
            int releaseGateIndex = methodBody.IndexOf("NotifyMacroDatabasePersistenceGateBestEffort(false, ref startupException);", failureBlockIndex, StringComparison.Ordinal);
            int reportIndex = methodBody.IndexOf("ReportPersistenceCleanupFailure(\"" + operationName + "\", startupException);", failureBlockIndex, StringComparison.Ordinal);
            int returnIndex = methodBody.IndexOf("return;", reportIndex, StringComparison.Ordinal);
            int tryIndex = methodBody.IndexOf("try", returnIndex, StringComparison.Ordinal);

            Assert.GreaterOrEqual(startupIndex, 0, operationName);
            Assert.Greater(gateIndex, startupIndex, operationName);
            Assert.Greater(failureBlockIndex, gateIndex, operationName);
            Assert.Greater(reasonIndex, failureBlockIndex, operationName);
            Assert.Greater(warningIndex, reasonIndex, operationName);
            Assert.Greater(releaseBusyIndex, warningIndex, operationName);
            Assert.Greater(releaseGateIndex, releaseBusyIndex, operationName);
            Assert.Greater(reportIndex, releaseGateIndex, operationName);
            Assert.Greater(eventIndex, reportIndex, operationName);
            if (statusNeedle != null)
            {
                int statusIndex = methodBody.IndexOf(statusNeedle, eventIndex, StringComparison.Ordinal);
                Assert.Greater(statusIndex, eventIndex, operationName);
                Assert.Greater(returnIndex, statusIndex, operationName);
            }
            else
            {
                Assert.Greater(returnIndex, eventIndex, operationName);
            }

            Assert.Greater(returnIndex, reportIndex, operationName);
            Assert.Greater(tryIndex, returnIndex, operationName);

            if (requireThumbnailAfterGate)
            {
                int startedIndex = methodBody.IndexOf("SaveEvents.TryRaiseSaveStarted(SaveEvents.ComputeSlotHash(slotName));", returnIndex, StringComparison.Ordinal);
                int thumbnailIndex = methodBody.IndexOf("SaveThumbnailSystem.CaptureThumbnailForSave(slotName, slotIndex, operationId)", startedIndex, StringComparison.Ordinal);
                Assert.Greater(startedIndex, tryIndex, operationName);
                Assert.Greater(thumbnailIndex, startedIndex, operationName);
            }
            else
            {
                int startedIndex = methodBody.IndexOf("SaveEvents.TryRaiseLoadStarted(SaveEvents.ComputeSlotHash(slotName));", tryIndex, StringComparison.Ordinal);
                int stageIndex = methodBody.IndexOf("ReportLoadPipelineStage(LoadingPipelineStage.PagingSectors", startedIndex, StringComparison.Ordinal);
                Assert.Greater(startedIndex, tryIndex, operationName);
                Assert.Greater(stageIndex, startedIndex, operationName);
            }
        }

        private sealed class AlphaPriorityOwner : ISaveable
        {
            public int SavePriority => 50;
            public int LoadPriority => 50;
            public void PopulateSaveData(SaveData data) { }
            public void LoadFromSaveData(SaveData data) { }
        }

        private sealed class BetaPriorityOwner : ISaveable
        {
            public int SavePriority => 50;
            public int LoadPriority => 50;
            public void PopulateSaveData(SaveData data) { }
            public void LoadFromSaveData(SaveData data) { }
        }

    }

    /// <summary>
    /// Save owner backed by a real <see cref="UnityEngine.Object"/> so that
    /// <c>SaveManager.IsAlive</c> (SaveManager.cs:5082) can be exercised against Unity's overloaded
    /// fake-null equality rather than against plain managed null.
    /// <para>
    /// Declared at namespace scope rather than nested inside the fixture: Unity instantiates
    /// <see cref="ScriptableObject"/> types through its own object factory, and a nested type is the
    /// kind of shape that has no reason to be risked here for zero benefit.
    /// </para>
    /// </summary>
    internal sealed class UnityObjectSaveableOwner : ScriptableObject, ISaveable
    {
        public int SavePriority => 50;
        public int LoadPriority => 50;
        public void PopulateSaveData(SaveData data) { }
        public void LoadFromSaveData(SaveData data) { }
    }
}
