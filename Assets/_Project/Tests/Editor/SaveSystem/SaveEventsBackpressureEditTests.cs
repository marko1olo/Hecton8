using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Hecton8.SaveSystem;
using NUnit.Framework;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public sealed class SaveEventsBackpressureEditTests
    {
        [Test]
        public void FailureEventSurvivesMessageSlotBackpressure()
        {
            InvokeResetStaticState();
            try
            {
                SetPrivateStaticInt("_messageSlotPendingCount", ResolvePrivateConstInt("MessageSlotCapacity"));

                bool raised = SaveEvents.TryRaiseSaveFailed(
                    SaveEvents.ComputeSlotHash("slot_0"),
                    0x45525221u,
                    "simulated message sidecar backpressure");

                Assert.IsTrue(raised);
                Assert.AreEqual(1, SaveEvents.PendingCount);
            }
            finally
            {
                InvokeResetStaticState();
            }
        }

        [Test]
        public void FailureSnapshotResolvesUiMessageWhenMessageSlotBackpressureDropsSidecar()
        {
            InvokeResetStaticState();
            try
            {
                SetPrivateStaticInt("_messageSlotPendingCount", ResolvePrivateConstInt("MessageSlotCapacity"));

                SnapshotFallbackSaveEventListener listener = new SnapshotFallbackSaveEventListener();
                SaveEvents.Register(listener);

                string failureMessage = "message sidecar overflow kept only the terminal payload";
                bool raised = SaveEvents.TryRaiseSaveFailed(
                    SaveEvents.ComputeSlotHash("slot_0"),
                    SaveEvents.ComputeMessageHash(failureMessage),
                    failureMessage);

                Assert.IsTrue(raised);
                Assert.AreEqual(1, SaveEvents.PendingCount);

                SaveEvents.FlushPending();

                Assert.AreEqual(1, listener.ReceivedCount);
                Assert.IsFalse(listener.ResolvedFromMessageSlot);
                Assert.IsTrue(listener.ResolvedFromFailureSnapshot);
                Assert.AreEqual(failureMessage, listener.FailureMessage);
                Assert.AreEqual(0, SaveEvents.PendingCount);
            }
            finally
            {
                InvokeResetStaticState();
            }
        }

        [Test]
        public void MatchingFailureSnapshotSurvivesLaterStartedEventWhenSidecarDropped()
        {
            InvokeResetStaticState();
            try
            {
                SetPrivateStaticInt("_messageSlotPendingCount", ResolvePrivateConstInt("MessageSlotCapacity"));

                FailureOnlySnapshotFallbackSaveEventListener listener = new FailureOnlySnapshotFallbackSaveEventListener();
                SaveEvents.Register(listener);

                uint slotHash = SaveEvents.ComputeSlotHash("slot_0");
                string failureMessage = "queued failure sidecar was dropped before a later save start";
                Assert.IsTrue(SaveEvents.TryRaiseSaveFailed(
                    slotHash,
                    SaveEvents.ComputeMessageHash(failureMessage),
                    failureMessage));
                Assert.IsTrue(SaveEvents.TryRaiseSaveStarted(slotHash));

                uint lastSeenSequence = 0u;
                Assert.IsFalse(SaveEvents.TryConsumeLatestFailureSnapshotForUi(
                    ref lastSeenSequence,
                    out _,
                    out _));

                SaveEvents.FlushPending();

                Assert.AreEqual(1, listener.FailureCount);
                Assert.IsFalse(listener.ResolvedFromMessageSlot);
                Assert.IsTrue(listener.ResolvedFromFailureSnapshot);
                Assert.AreEqual(failureMessage, listener.FailureMessage);
                Assert.AreEqual(0, SaveEvents.PendingCount);
            }
            finally
            {
                InvokeResetStaticState();
            }
        }

        [Test]
        public void TerminalFailureEventSurvivesQueueBackpressure()
        {
            InvokeResetStaticState();
            try
            {
                int capacity = ResolvePrivateConstInt("PendingEventCapacity");
                uint slotHash = SaveEvents.ComputeSlotHash("slot_0");
                for (int i = 0; i < capacity; i++)
                    Assert.IsTrue(SaveEvents.TryRaiseSaveStarted(slotHash));

                Assert.AreEqual(capacity, SaveEvents.PendingCount);

                bool raised = SaveEvents.TryRaiseSaveFailed(
                    slotHash,
                    0x51554521u,
                    "terminal failure must evict stale queued save event");

                Assert.IsTrue(raised);
                Assert.AreEqual(capacity, SaveEvents.PendingCount);
                Assert.AreEqual(1, SaveEvents.DroppedEventCount);
            }
            finally
            {
                InvokeResetStaticState();
            }
        }

        [Test]
        public void CompletionBackpressureDoesNotEvictQueuedFailureEvent()
        {
            InvokeResetStaticState();
            try
            {
                int capacity = ResolvePrivateConstInt("PendingEventCapacity");
                uint slotHash = SaveEvents.ComputeSlotHash("slot_0");
                for (int i = 0; i < capacity; i++)
                {
                    string failureMessage = "queued failure must survive completion pressure " + i.ToString();
                    Assert.IsTrue(SaveEvents.TryRaiseSaveFailed(
                        slotHash,
                        SaveEvents.ComputeMessageHash(failureMessage),
                        failureMessage));
                }

                Assert.AreEqual(capacity, SaveEvents.PendingCount);
                Assert.IsFalse(SaveEvents.TryRaiseSaveCompleted(slotHash));
                Assert.AreEqual(capacity, SaveEvents.PendingCount);
                Assert.AreEqual(1, SaveEvents.DroppedEventCount);

                FailureOnlySnapshotFallbackSaveEventListener listener = new FailureOnlySnapshotFallbackSaveEventListener();
                SaveEvents.Register(listener);
                SaveEvents.FlushPending();

                Assert.AreEqual(capacity, listener.FailureCount);
                Assert.AreEqual(0, SaveEvents.PendingCount);
            }
            finally
            {
                InvokeResetStaticState();
            }
        }

        [Test]
        public void CompletionBackpressureEvictsOldestNonFailureBehindQueuedFailure()
        {
            InvokeResetStaticState();
            try
            {
                int capacity = ResolvePrivateConstInt("PendingEventCapacity");
                uint slotHash = SaveEvents.ComputeSlotHash("slot_0");
                string failureMessage = "queued failure must survive completion pressure";
                Assert.IsTrue(SaveEvents.TryRaiseSaveFailed(
                    slotHash,
                    SaveEvents.ComputeMessageHash(failureMessage),
                    failureMessage));

                for (int i = 1; i < capacity; i++)
                    Assert.IsTrue(SaveEvents.TryRaiseSaveStarted(slotHash));

                Assert.AreEqual(capacity, SaveEvents.PendingCount);
                Assert.IsTrue(SaveEvents.TryRaiseSaveCompleted(slotHash));
                Assert.AreEqual(capacity, SaveEvents.PendingCount);
                Assert.AreEqual(1, SaveEvents.DroppedEventCount);

                CountingSaveEventListener listener = new CountingSaveEventListener();
                SaveEvents.Register(listener);
                SaveEvents.FlushPending();

                Assert.AreEqual(1, listener.FailureCount);
                Assert.AreEqual(1, listener.CompletedCount);
                Assert.AreEqual(capacity - 2, listener.StartedCount);
                Assert.AreEqual(SaveEventType.SaveFailed, listener.FirstType);
                Assert.AreEqual(SaveEventType.SaveCompleted, listener.LastType);
                Assert.AreEqual(0, SaveEvents.PendingCount);
            }
            finally
            {
                InvokeResetStaticState();
            }
        }

        [Test]
        public void DroppedNonTerminalEventDoesNotHideLatestFailureSnapshot()
        {
            InvokeResetStaticState();
            try
            {
                int capacity = ResolvePrivateConstInt("PendingEventCapacity");
                uint slotHash = SaveEvents.ComputeSlotHash("slot_0");
                for (int i = 0; i < capacity; i++)
                    Assert.IsTrue(SaveEvents.TryRaiseSaveStarted(slotHash));

                string failureMessage = "latest failure must stay visible when a later start event is dropped";
                Assert.IsTrue(SaveEvents.TryRaiseLoadFailed(
                    slotHash,
                    SaveEvents.ComputeMessageHash(failureMessage),
                    failureMessage));
                Assert.AreEqual(capacity, SaveEvents.PendingCount);

                Assert.IsFalse(SaveEvents.TryRaiseSaveStarted(slotHash));

                uint lastSeenSequence = 0u;
                Assert.IsTrue(SaveEvents.TryConsumeLatestFailureSnapshotForUi(
                    ref lastSeenSequence,
                    out SaveEventPayload snapshot,
                    out string snapshotMessage));
                Assert.AreEqual(SaveEventType.LoadFailed, snapshot.Type);
                Assert.AreEqual(failureMessage, snapshotMessage);
            }
            finally
            {
                InvokeResetStaticState();
            }
        }

        [Test]
        public void FlushPendingContinuesAndReleasesMessageSlotWhenListenerThrows()
        {
            InvokeResetStaticState();
            try
            {
                RecordingSaveEventListener recorder = new RecordingSaveEventListener();
                ThrowingSaveEventListener throwing = new ThrowingSaveEventListener();
                uint slotHash = SaveEvents.ComputeSlotHash("slot_0");
                string errorMessage = "listener exception must not strand message slot";

                SaveEvents.Register(recorder);
                SaveEvents.Register(throwing);

                Assert.IsTrue(SaveEvents.TryRaiseSaveFailed(
                    slotHash,
                    SaveEvents.ComputeMessageHash(errorMessage),
                    errorMessage));
                Assert.AreEqual(1, SaveEvents.PendingCount);
                Assert.AreEqual(1, GetPrivateStaticInt("_messageSlotPendingCount"));

                UnityEngine.TestTools.LogAssert.Expect(
                    UnityEngine.LogType.Exception,
                    new Regex("InvalidOperationException: simulated save listener failure"));
                SaveEvents.FlushPending();

                Assert.AreEqual(1, recorder.ReceivedCount);
                Assert.AreEqual(SaveEventType.SaveFailed, recorder.LastType);
                Assert.AreEqual(errorMessage, recorder.LastMessage);
                Assert.AreEqual(0, SaveEvents.PendingCount);
                Assert.AreEqual(0, GetPrivateStaticInt("_messageSlotPendingCount"));
            }
            finally
            {
                InvokeResetStaticState();
            }
        }

        [Test]
        public void LateUiFailureSnapshotSurvivesDrainWithoutListenerReplay()
        {
            InvokeResetStaticState();
            try
            {
                uint slotHash = SaveEvents.ComputeSlotHash("slot_1");
                string errorMessage = "load failed while player UI was between scenes";

                Assert.IsTrue(SaveEvents.TryRaiseLoadFailed(
                    slotHash,
                    SaveEvents.ComputeMessageHash(errorMessage),
                    errorMessage));
                Assert.AreEqual(1, SaveEvents.PendingCount);
                Assert.AreEqual(1, GetPrivateStaticInt("_messageSlotPendingCount"));

                SaveEvents.FlushPending();

                Assert.AreEqual(0, SaveEvents.PendingCount);
                Assert.AreEqual(0, GetPrivateStaticInt("_messageSlotPendingCount"));

                uint lastSeenSequence = 0u;
                Assert.IsTrue(SaveEvents.TryConsumeLatestFailureSnapshotForUi(
                    ref lastSeenSequence,
                    out SaveEventPayload snapshot,
                    out string snapshotMessage));
                Assert.AreEqual(SaveEventType.LoadFailed, snapshot.Type);
                Assert.AreEqual(slotHash, snapshot.SlotHash);
                Assert.AreEqual(errorMessage, snapshotMessage);
                Assert.AreEqual(-1, snapshot.MessageSlot);
                Assert.IsFalse(SaveEvents.TryResolveMessage(in snapshot, out _));
                Assert.IsFalse(SaveEvents.TryConsumeLatestFailureSnapshotForUi(
                    ref lastSeenSequence,
                    out _,
                    out _));

                Assert.IsTrue(SaveEvents.TryRaiseLoadStarted(slotHash));
                Assert.IsFalse(SaveEvents.TryConsumeLatestFailureSnapshotForUi(
                    ref lastSeenSequence,
                    out _,
                    out _));
            }
            finally
            {
                InvokeResetStaticState();
            }
        }

        [Test]
        public void FlushPendingAppliesDeferredUnregisterBeforeNextPayload()
        {
            InvokeResetStaticState();
            try
            {
                RecordingSaveEventListener recorder = new RecordingSaveEventListener();
                SelfUnregisteringSaveEventListener selfUnregistering = new SelfUnregisteringSaveEventListener();
                uint slotHash = SaveEvents.ComputeSlotHash("slot_0");

                SaveEvents.Register(recorder);
                SaveEvents.Register(selfUnregistering);

                Assert.IsTrue(SaveEvents.TryRaiseSaveStarted(slotHash));
                Assert.IsTrue(SaveEvents.TryRaiseSaveCompleted(slotHash));

                SaveEvents.FlushPending();

                Assert.AreEqual(2, recorder.ReceivedCount);
                Assert.AreEqual(1, selfUnregistering.ReceivedCount);
                Assert.AreEqual(0, SaveEvents.PendingCount);
            }
            finally
            {
                InvokeResetStaticState();
            }
        }

        [Test]
        public void FlushPendingAppliesDeferredRegisterAfterCurrentPayload()
        {
            InvokeResetStaticState();
            try
            {
                RecordingSaveEventListener deferredRecorder = new RecordingSaveEventListener();
                RegisteringSaveEventListener registering = new RegisteringSaveEventListener(deferredRecorder);
                uint slotHash = SaveEvents.ComputeSlotHash("slot_0");

                SaveEvents.Register(registering);

                Assert.IsTrue(SaveEvents.TryRaiseSaveStarted(slotHash));
                Assert.IsTrue(SaveEvents.TryRaiseSaveCompleted(slotHash));

                SaveEvents.FlushPending();

                Assert.AreEqual(2, registering.ReceivedCount);
                Assert.AreEqual(1, deferredRecorder.ReceivedCount);
                Assert.AreEqual(SaveEventType.SaveCompleted, deferredRecorder.LastType);
                Assert.AreEqual(0, SaveEvents.PendingCount);
            }
            finally
            {
                InvokeResetStaticState();
            }
        }

        [Test]
        public void FlushPendingDoesNotReenterDispatchFromListenerCallback()
        {
            InvokeResetStaticState();
            try
            {
                SaveEventOrderLog orderLog = new SaveEventOrderLog();
                OrderRecordingSaveEventListener recorder = new OrderRecordingSaveEventListener(orderLog, "recorder");
                ReentrantFlushSaveEventListener reentrant = new ReentrantFlushSaveEventListener(orderLog, "reentrant");
                uint slotHash = SaveEvents.ComputeSlotHash("slot_0");

                SaveEvents.Register(recorder);
                SaveEvents.Register(reentrant);

                Assert.IsTrue(SaveEvents.TryRaiseSaveStarted(slotHash));
                Assert.IsTrue(SaveEvents.TryRaiseSaveCompleted(slotHash));

                SaveEvents.FlushPending();

                Assert.AreEqual(4, orderLog.Count);
                Assert.AreEqual("reentrant", orderLog.ListenerAt(0));
                Assert.AreEqual(SaveEventType.SaveStarted, orderLog.TypeAt(0));
                Assert.AreEqual("recorder", orderLog.ListenerAt(1));
                Assert.AreEqual(SaveEventType.SaveStarted, orderLog.TypeAt(1));
                Assert.AreEqual("reentrant", orderLog.ListenerAt(2));
                Assert.AreEqual(SaveEventType.SaveCompleted, orderLog.TypeAt(2));
                Assert.AreEqual("recorder", orderLog.ListenerAt(3));
                Assert.AreEqual(SaveEventType.SaveCompleted, orderLog.TypeAt(3));
                Assert.AreEqual(0, SaveEvents.PendingCount);
            }
            finally
            {
                InvokeResetStaticState();
            }
        }

        [Test]
        public void FlushPendingReturnsBeforeDrainOrBudgetWhenDispatchIsActive()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/SaveEvents.cs"));
            string flush = ExtractMethodBody(source, "public static void FlushPending()");

            int dispatchGuardIndex = flush.IndexOf("if (_isDispatching)", StringComparison.Ordinal);
            int guardReturnIndex = flush.IndexOf("return;", dispatchGuardIndex, StringComparison.Ordinal);
            int drainIndex = flush.IndexOf("DrainWithoutDispatch();", StringComparison.Ordinal);
            int budgetIndex = flush.IndexOf("SystemDispatcher.TryConsumeLateFrameEventDispatch()", StringComparison.Ordinal);

            Assert.GreaterOrEqual(dispatchGuardIndex, 0);
            Assert.Greater(guardReturnIndex, dispatchGuardIndex);
            Assert.Greater(drainIndex, guardReturnIndex);
            Assert.Greater(budgetIndex, guardReturnIndex);
        }

        [Test]
        public void TryRaiseFailsClosedWhenQueueInitializationFails()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/SaveEvents.cs"));
            string tryEnqueue = ExtractMethodBody(source, "private static bool TryEnqueue(");
            string reportInitializationFailure = ExtractMethodBody(source, "private static void ReportQueueInitializationFailure(");

            StringAssert.Contains("UpdateUiFailureSnapshot(type, slotHash, messageHash, message, timestampTicks);", tryEnqueue);
            StringAssert.Contains("try", tryEnqueue);
            StringAssert.Contains("EnsureInitialized();", tryEnqueue);
            StringAssert.Contains("catch (Exception exception)", tryEnqueue);
            StringAssert.Contains("ReportQueueInitializationFailure(type, exception);", tryEnqueue);
            StringAssert.Contains("return false;", tryEnqueue);
            StringAssert.Contains("SaveEventQueueInitializationFailureWarningHash", reportInitializationFailure);
            StringAssert.Contains("int frame = ResolveCurrentFrameIndexSafe();", reportInitializationFailure);
            StringAssert.Contains("PublishPerformanceWarningBestEffort(", reportInitializationFailure);
            StringAssert.Contains("LogQueueInitializationException(exception);", reportInitializationFailure);
        }

        [Test]
        public void ResetStaticStateReleasesNativeQueuesBestEffortBeforeClearingState()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/SaveEvents.cs"));
            string reset = ExtractMethodBody(source, "internal static void ResetStaticState()");
            string releaseBestEffort = ExtractMethodBody(source, "private static Exception ReleaseNativeQueuesBestEffort()");
            string registerNativeQueue = ExtractMethodBody(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueue = ExtractMethodBody(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethodBody(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");
            string reportReleaseFailure = ExtractMethodBody(source, "private static void ReportQueueReleaseFailure(Exception exception)");

            int releaseIndex = reset.IndexOf("Exception releaseException = ReleaseNativeQueuesBestEffort();", StringComparison.Ordinal);
            int clearIndex = reset.IndexOf("_listeners.Clear();", StringComparison.Ordinal);
            int reportIndex = reset.IndexOf("ReportQueueReleaseFailure(releaseException);", StringComparison.Ordinal);

            Assert.GreaterOrEqual(releaseIndex, 0);
            Assert.Greater(clearIndex, releaseIndex);
            Assert.Greater(reportIndex, clearIndex);
            StringAssert.Contains("ReleaseNativeQueueBestEffort(ref _pendingEvents", releaseBestEffort);
            StringAssert.Contains("ReleaseNativeQueueBestEffort(ref _nextFrameEvents", releaseBestEffort);
            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", source);
            StringAssert.Contains("out _nextFrameEventsSentinelId", source);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref queue, ref sentinelId);", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueueBestEffort(ref _pendingEvents, ref _pendingEventsSentinelId, ref firstException);", releaseBestEffort);
            StringAssert.Contains("ReleaseNativeQueueBestEffort(ref _nextFrameEvents, ref _nextFrameEventsSentinelId, ref firstException);", releaseBestEffort);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.DoesNotContain("disposed = true;", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("if (firstException != null)", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("int sentinelIdSwap = _pendingEventsSentinelId;", promote);
            StringAssert.Contains("_pendingEventsSentinelId = _nextFrameEventsSentinelId;", promote);
            StringAssert.Contains("_nextFrameEventsSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(SaveEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("sentinelId = 0;", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameEvents = swap;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingEventsSentinelId;", StringComparison.Ordinal));
            StringAssert.Contains("SaveEventQueueReleaseFailureWarningHash", reportReleaseFailure);
            StringAssert.Contains("int frame = ResolveCurrentFrameIndexSafe();", reportReleaseFailure);
            StringAssert.Contains("PublishPerformanceWarningBestEffort(", reportReleaseFailure);
        }

        [Test]
        public void ListenerRegistrationAndPrewarmFailClosedWhenQueueInitializationFails()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/SaveEvents.cs"));
            string prewarm = ExtractMethodBody(source, "internal static void PrewarmRuntimeQueues()");
            string register = ExtractMethodBody(source, "public static void Register(ISaveEventListener listener)");
            string reportListenerFailure = ExtractMethodBody(source, "private static void ReportListenerInitializationFailure(Exception exception)");

            StringAssert.Contains("try", prewarm);
            StringAssert.Contains("EnsureInitialized();", prewarm);
            StringAssert.Contains("catch (Exception exception)", prewarm);
            StringAssert.Contains("ReportQueueInitializationFailure(SaveEventType.SaveStarted, exception);", prewarm);

            StringAssert.Contains("try", register);
            StringAssert.Contains("EnsureInitialized();", register);
            StringAssert.Contains("catch (Exception exception)", register);
            StringAssert.Contains("ReportListenerInitializationFailure(exception);", register);
            StringAssert.Contains("return;", register);

            StringAssert.Contains("SaveEventListenerInitializationFailureWarningHash", reportListenerFailure);
            StringAssert.Contains("_droppedListenerRegistrationCount++;", reportListenerFailure);
            StringAssert.Contains("int frame = ResolveCurrentFrameIndexSafe();", reportListenerFailure);
            StringAssert.Contains("PublishPerformanceWarningBestEffort(", reportListenerFailure);
            StringAssert.Contains("private static int ResolveCurrentFrameIndexSafe()", source);
            StringAssert.Contains("try", ExtractMethodBody(source, "private static void LogQueueInitializationException(Exception exception)"));
        }

        [Test]
        public void ListenerDispatchDiagnosticsCannotBreakFlushPending()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/SaveEvents.cs"));
            string dispatch = ExtractMethodBody(source, "private static void DispatchToListener(");
            string reportDispatch = ExtractMethodBody(source, "private static void ReportListenerDispatchException()");
            string logDispatch = ExtractMethodBody(source, "private static void LogListenerDispatchException(Exception exception)");
            string publishBestEffort = ExtractMethodBody(source, "private static void PublishPerformanceWarningBestEffort(");

            StringAssert.Contains("catch (Exception exception)", dispatch);
            StringAssert.Contains("ReportListenerDispatchException();", dispatch);
            StringAssert.Contains("LogListenerDispatchException(exception);", dispatch);
            StringAssert.Contains("int frame = ResolveCurrentFrameIndexSafe();", reportDispatch);
            StringAssert.Contains("PublishPerformanceWarningBestEffort(", reportDispatch);
            StringAssert.Contains("try", logDispatch);
            StringAssert.Contains("Hecton8.Core.H8Debug.LogException(exception);", logDispatch);
            StringAssert.Contains("catch", logDispatch);
            StringAssert.Contains("catch (Exception telemetryException)", publishBestEffort);
        }

        [Test]
        public void UiFailureSnapshotIsConsumedExplicitlyAndNotReplayedThroughRegister()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/SaveEvents.cs"));
            string consumeSnapshot = ExtractMethodBody(source, "public static bool TryConsumeLatestFailureSnapshotForUi(");
            string consumeMatchingSnapshot = ExtractMethodBody(source, "public static bool TryConsumeMatchingFailureSnapshotForUi(");
            string register = ExtractMethodBody(source, "public static void Register(ISaveEventListener listener)");
            string captureSnapshot = ExtractMethodBody(source, "private static void CaptureUiFailureSnapshot(");
            string clearSnapshot = ExtractMethodBody(source, "private static void ClearUiFailureSnapshot()");
            string doesFailureSnapshotMatch = ExtractMethodBody(source, "private static bool DoesFailureSnapshotMatch(");
            string tryEnqueue = ExtractMethodBody(source, "private static bool TryEnqueue(");

            StringAssert.Contains("ref uint lastSeenSequence", consumeSnapshot);
            StringAssert.Contains("!_lastUiFailureVisible", consumeSnapshot);
            StringAssert.Contains("!IsFailureEvent(payload.Type)", consumeSnapshot);
            StringAssert.Contains("lastSeenSequence = sequence;", consumeSnapshot);
            StringAssert.Contains("in SaveEventPayload expectedPayload", consumeMatchingSnapshot);
            StringAssert.Contains("DoesFailureSnapshotMatch(in expectedPayload, in payload)", consumeMatchingSnapshot);
            StringAssert.Contains("message = _lastUiFailureMessage ?? string.Empty;", consumeMatchingSnapshot);
            StringAssert.Contains("lastSeenSequence = sequence;", consumeMatchingSnapshot);
            StringAssert.Contains("expectedPayload.TimestampTicks == snapshotPayload.TimestampTicks", doesFailureSnapshotMatch);
            StringAssert.DoesNotContain("TryConsumeLatestFailureSnapshotForUi", register);
            StringAssert.DoesNotContain("TryConsumeMatchingFailureSnapshotForUi", register);
            StringAssert.DoesNotContain("OnSaveEvent", register);
            StringAssert.Contains("MessageSlot = -1", captureSnapshot);
            StringAssert.Contains("_lastUiFailureSequence++", captureSnapshot);
            StringAssert.Contains("_lastUiFailureVisible = true;", captureSnapshot);
            StringAssert.Contains("_lastUiFailureMessage = string.Empty;", clearSnapshot);
            StringAssert.Contains("_lastUiFailureVisible = false;", clearSnapshot);
            StringAssert.Contains("bool hideFailureSnapshotAfterEnqueue", tryEnqueue);
            StringAssert.Contains("if (isFailureEvent)", tryEnqueue);
            StringAssert.Contains("UpdateUiFailureSnapshot(type, slotHash, messageHash, message, timestampTicks);", tryEnqueue);
            Assert.Greater(
                tryEnqueue.LastIndexOf("HideUiFailureSnapshotFromLateReplay();", StringComparison.Ordinal),
                tryEnqueue.IndexOf("_pendingEvents.Enqueue(payload);", StringComparison.Ordinal));
        }

        private sealed class RecordingSaveEventListener : ISaveEventListener
        {
            public int ReceivedCount;
            public SaveEventType LastType;
            public string LastMessage = string.Empty;

            public void OnSaveEvent(in SaveEventPayload payload)
            {
                ReceivedCount++;
                LastType = payload.Type;
                LastMessage = SaveEvents.ResolveMessage(in payload);
            }
        }

        private sealed class CountingSaveEventListener : ISaveEventListener
        {
            public int StartedCount;
            public int CompletedCount;
            public int FailureCount;
            public SaveEventType FirstType;
            public SaveEventType LastType;
            private bool _hasFirstType;

            public void OnSaveEvent(in SaveEventPayload payload)
            {
                if (!_hasFirstType)
                {
                    FirstType = payload.Type;
                    _hasFirstType = true;
                }

                LastType = payload.Type;
                switch (payload.Type)
                {
                    case SaveEventType.SaveStarted:
                        StartedCount++;
                        return;

                    case SaveEventType.SaveCompleted:
                        CompletedCount++;
                        return;

                    case SaveEventType.SaveFailed:
                    case SaveEventType.LoadFailed:
                        FailureCount++;
                        return;
                }
            }
        }

        private sealed class SnapshotFallbackSaveEventListener : ISaveEventListener
        {
            public int ReceivedCount;
            public bool ResolvedFromMessageSlot;
            public bool ResolvedFromFailureSnapshot;
            public string FailureMessage = string.Empty;
            private uint _lastSeenFailureSnapshotSequence;

            public void OnSaveEvent(in SaveEventPayload payload)
            {
                ReceivedCount++;
                ResolvedFromMessageSlot = SaveEvents.TryResolveMessage(in payload, out string slotMessage);
                ResolvedFromFailureSnapshot = SaveEvents.TryConsumeMatchingFailureSnapshotForUi(
                    ref _lastSeenFailureSnapshotSequence,
                    in payload,
                    out string snapshotMessage);
                FailureMessage = ResolvedFromMessageSlot ? slotMessage : snapshotMessage;
            }
        }

        private sealed class FailureOnlySnapshotFallbackSaveEventListener : ISaveEventListener
        {
            public int FailureCount;
            public bool ResolvedFromMessageSlot;
            public bool ResolvedFromFailureSnapshot;
            public string FailureMessage = string.Empty;
            private uint _lastSeenFailureSnapshotSequence;

            public void OnSaveEvent(in SaveEventPayload payload)
            {
                if (payload.Type != SaveEventType.SaveFailed &&
                    payload.Type != SaveEventType.LoadFailed)
                {
                    return;
                }

                FailureCount++;
                ResolvedFromMessageSlot = SaveEvents.TryResolveMessage(in payload, out string slotMessage);
                ResolvedFromFailureSnapshot = SaveEvents.TryConsumeMatchingFailureSnapshotForUi(
                    ref _lastSeenFailureSnapshotSequence,
                    in payload,
                    out string snapshotMessage);
                FailureMessage = ResolvedFromMessageSlot ? slotMessage : snapshotMessage;
            }
        }

        private sealed class ThrowingSaveEventListener : ISaveEventListener
        {
            public void OnSaveEvent(in SaveEventPayload payload)
            {
                throw new InvalidOperationException("simulated save listener failure");
            }
        }

        private sealed class SelfUnregisteringSaveEventListener : ISaveEventListener
        {
            public int ReceivedCount;

            public void OnSaveEvent(in SaveEventPayload payload)
            {
                ReceivedCount++;
                SaveEvents.Unregister(this);
            }
        }

        private sealed class RegisteringSaveEventListener : ISaveEventListener
        {
            private readonly ISaveEventListener _listenerToRegister;
            private bool _registered;

            public int ReceivedCount;

            public RegisteringSaveEventListener(ISaveEventListener listenerToRegister)
            {
                _listenerToRegister = listenerToRegister;
            }

            public void OnSaveEvent(in SaveEventPayload payload)
            {
                ReceivedCount++;
                if (_registered)
                    return;

                _registered = true;
                SaveEvents.Register(_listenerToRegister);
            }
        }

        private sealed class SaveEventOrderLog
        {
            private readonly SaveEventType[] _types = new SaveEventType[8];
            private readonly string[] _listeners = new string[8];

            public int Count { get; private set; }

            public void Add(string listenerName, SaveEventType type)
            {
                Assert.Less(Count, _types.Length);
                _listeners[Count] = listenerName;
                _types[Count] = type;
                Count++;
            }

            public string ListenerAt(int index) => _listeners[index];

            public SaveEventType TypeAt(int index) => _types[index];
        }

        private sealed class OrderRecordingSaveEventListener : ISaveEventListener
        {
            private readonly SaveEventOrderLog _orderLog;
            private readonly string _listenerName;

            public OrderRecordingSaveEventListener(SaveEventOrderLog orderLog, string listenerName)
            {
                _orderLog = orderLog;
                _listenerName = listenerName;
            }

            public void OnSaveEvent(in SaveEventPayload payload)
            {
                _orderLog.Add(_listenerName, payload.Type);
            }
        }

        private sealed class ReentrantFlushSaveEventListener : ISaveEventListener
        {
            private readonly SaveEventOrderLog _orderLog;
            private readonly string _listenerName;
            private bool _flushed;

            public ReentrantFlushSaveEventListener(SaveEventOrderLog orderLog, string listenerName)
            {
                _orderLog = orderLog;
                _listenerName = listenerName;
            }

            public void OnSaveEvent(in SaveEventPayload payload)
            {
                _orderLog.Add(_listenerName, payload.Type);
                if (_flushed)
                    return;

                _flushed = true;
                SaveEvents.FlushPending();
            }
        }

        private static int ResolvePrivateConstInt(string fieldName)
        {
            FieldInfo field = typeof(SaveEvents).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Missing SaveEvents const: " + fieldName);
            return (int)field.GetRawConstantValue();
        }

        private static int GetPrivateStaticInt(string fieldName)
        {
            FieldInfo field = typeof(SaveEvents).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Missing SaveEvents field: " + fieldName);
            return (int)field.GetValue(null);
        }

        private static void SetPrivateStaticInt(string fieldName, int value)
        {
            FieldInfo field = typeof(SaveEvents).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Missing SaveEvents field: " + fieldName);
            field.SetValue(null, value);
        }

        private static void InvokeResetStaticState()
        {
            MethodInfo reset = typeof(SaveEvents).GetMethod("ResetStaticState", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(reset, "Missing SaveEvents.ResetStaticState");
            reset.Invoke(null, null);
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
    }
}
