using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Hecton8.Interaction;
using Hecton8.Items;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hecton8.Tests.Editor
{
    public sealed class InteractionEventsBackpressureEditTests
    {
        [Test]
        public void InteractionEventPayloadLayoutKeepsReservedGenerationSlot()
        {
            StructLayoutAttribute layout = typeof(InteractionEventPayload).StructLayoutAttribute;
            Assert.IsNotNull(layout);
            Assert.AreEqual(LayoutKind.Explicit, layout.Value);
            Assert.AreEqual(32, UnsafeUtility.SizeOf<InteractionEventPayload>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<InteractionEventPayload>() & 7);
            Assert.AreEqual(0, (int)Marshal.OffsetOf<InteractionEventPayload>(nameof(InteractionEventPayload.ItemHashId)));
            Assert.AreEqual(4, (int)Marshal.OffsetOf<InteractionEventPayload>(nameof(InteractionEventPayload.TargetHashId)));
            Assert.AreEqual(8, (int)Marshal.OffsetOf<InteractionEventPayload>(nameof(InteractionEventPayload.InteractorHashId)));
            Assert.AreEqual(12, (int)Marshal.OffsetOf<InteractionEventPayload>(nameof(InteractionEventPayload.ReferenceSlot)));
            Assert.AreEqual(16, (int)Marshal.OffsetOf<InteractionEventPayload>(nameof(InteractionEventPayload.Quantity)));
            Assert.AreEqual(20, (int)Marshal.OffsetOf<InteractionEventPayload>(nameof(InteractionEventPayload.EventType)));
            Assert.AreEqual(22, (int)Marshal.OffsetOf<InteractionEventPayload>(nameof(InteractionEventPayload.Reserved)));
        }

        [Test]
        public void FlushPendingDrainsReferenceSlotsWhenNoListenersRegistered()
        {
            InvokeResetStaticState();
            try
            {
                DummyInteractable target = new DummyInteractable();

                Assert.IsTrue(InteractionEvents.TryRaiseInteractionStarted(target, null));
                Assert.AreEqual(1, InteractionEvents.PendingCount);
                Assert.AreEqual(1, GetPrivateStaticInt("_referencePendingCount"));

                InteractionEvents.FlushPending();

                Assert.AreEqual(0, InteractionEvents.PendingCount);
                Assert.AreEqual(0, GetPrivateStaticInt("_referencePendingCount"));
            }
            finally
            {
                InvokeResetStaticState();
            }
        }

        [Test]
        public void QueueBackpressureReleasesRejectedReferenceSlot()
        {
            InvokeResetStaticState();
            try
            {
                int capacity = ResolvePrivateConstInt("PendingEventCapacity");
                for (int i = 0; i < capacity; i++)
                    Assert.IsTrue(InteractionEvents.TryRaiseHoverChanged(null));

                Assert.AreEqual(capacity, InteractionEvents.PendingCount);
                Assert.AreEqual(0, GetPrivateStaticInt("_referencePendingCount"));

                Assert.IsFalse(InteractionEvents.TryRaiseInteractionStarted(new DummyInteractable(), null));

                Assert.AreEqual(capacity, InteractionEvents.PendingCount);
                Assert.AreEqual(0, GetPrivateStaticInt("_referencePendingCount"));
                AssertNoOccupiedReferenceSlots();
                Assert.AreEqual(1, InteractionEvents.DroppedEventCount);
                Assert.AreEqual(0, InteractionEvents.DroppedReferenceSlotCount);
            }
            finally
            {
                InvokeResetStaticState();
            }
        }

        [Test]
        public void ItemEventProducersRejectInvalidPayloadsBeforeReservingReferenceSlots()
        {
            InvokeResetStaticState();
            try
            {
                Assert.IsFalse(InteractionEvents.TryRaiseItemCollected(null, 1, null));
                Assert.IsFalse(InteractionEvents.TryRaiseItemCollected(null, 0, null));
                Assert.IsFalse(InteractionEvents.TryRaiseItemLost(null, 1, null));
                Assert.IsFalse(InteractionEvents.TryRaiseItemLost(null, 0, null));

                Assert.AreEqual(0, InteractionEvents.PendingCount);
                Assert.AreEqual(0, GetPrivateStaticInt("_referencePendingCount"));
                Assert.AreEqual(0, InteractionEvents.DroppedEventCount);
                Assert.AreEqual(4, InteractionEvents.DroppedInvalidItemEventCount);
                Assert.AreEqual(0, InteractionEvents.DroppedReferenceSlotCount);
            }
            finally
            {
                InvokeResetStaticState();
            }
        }

        [Test]
        public void ValidItemCollectedEnqueuesAndResolvesItemSidecar()
        {
            InvokeResetStaticState();
            ItemData item = null;
            try
            {
                item = CreateItemData("InteractionEventsBackpressureEditTests.ValidResource");
                item.category = ItemCategory.Material;
                item.isRawResource = true;

                RecordingInteractionEventListener recorder = new RecordingInteractionEventListener();
                InteractionEvents.Register(recorder);

                Assert.IsTrue(InteractionEvents.TryRaiseItemCollected(item, 3, null));
                Assert.AreEqual(1, InteractionEvents.PendingCount);
                Assert.AreEqual(1, GetPrivateStaticInt("_referencePendingCount"));
                Assert.AreEqual(0, InteractionEvents.DroppedInvalidItemEventCount);
                Assert.AreEqual(0, InteractionEvents.DroppedReferenceSlotCount);

                InteractionEvents.FlushPending();

                Assert.AreEqual(1, recorder.ReceivedCount);
                Assert.AreEqual((ushort)InteractionEventType.ItemCollected, recorder.LastEventType);
                Assert.AreSame(item, recorder.LastItem);
                Assert.AreEqual(3, recorder.LastQuantity);
                Assert.AreEqual(0, InteractionEvents.PendingCount);
                Assert.AreEqual(0, GetPrivateStaticInt("_referencePendingCount"));
                Assert.AreEqual(0, InteractionEvents.DroppedEventCount);
                Assert.AreEqual(0, InteractionEvents.DroppedInvalidItemEventCount);
                Assert.AreEqual(0, InteractionEvents.DroppedReferenceSlotCount);
            }
            finally
            {
                if (item != null)
                    UnityEngine.Object.DestroyImmediate(item);

                InvokeResetStaticState();
            }
        }

        [Test]
        public void ReleasedPayloadDoesNotResolveAfterReferenceSlotReuse()
        {
            InvokeResetStaticState();
            ItemData first = null;
            ItemData second = null;
            try
            {
                first = CreateItemData("InteractionEventsBackpressureEditTests.FirstResource");
                second = CreateItemData("InteractionEventsBackpressureEditTests.SecondResource");
                RecordingInteractionEventListener recorder = new RecordingInteractionEventListener();
                InteractionEvents.Register(recorder);

                Assert.IsTrue(InteractionEvents.TryRaiseItemCollected(first, 1, null));
                InteractionEvents.FlushPending();

                InteractionEventPayload stalePayload = recorder.LastPayload;
                int staleSlot = stalePayload.ReferenceSlot;
                Assert.IsFalse(InteractionEvents.TryResolveItem(in stalePayload, out ItemData releasedItem));
                Assert.IsNull(releasedItem);

                SetPrivateStaticInt("_referenceWriteIndex", staleSlot);
                Assert.IsTrue(InteractionEvents.TryRaiseItemCollected(second, 2, null));

                Assert.IsFalse(InteractionEvents.TryResolveItem(in stalePayload, out ItemData reusedItem));
                Assert.IsNull(reusedItem);

                InteractionEvents.FlushPending();

                Assert.AreSame(second, recorder.LastItem);
                Assert.AreEqual(2, recorder.LastQuantity);
                Assert.AreEqual(0, InteractionEvents.PendingCount);
                Assert.AreEqual(0, GetPrivateStaticInt("_referencePendingCount"));
                AssertNoOccupiedReferenceSlots();
            }
            finally
            {
                if (first != null)
                    UnityEngine.Object.DestroyImmediate(first);

                if (second != null)
                    UnityEngine.Object.DestroyImmediate(second);

                InvokeResetStaticState();
            }
        }

        [Test]
        public void ItemEventProducersValidateItemHashBeforeReservingReferenceSlot()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Interaction/InteractionEvents.cs"));
            string collected = ExtractMethodBody(source, "public static bool TryRaiseItemCollected(");
            string lost = ExtractMethodBody(source, "public static bool TryRaiseItemLost(");
            string invalidItemReport = ExtractMethodBody(source, "private static void ReportInvalidItemEvent(");
            string reset = ExtractMethodBody(source, "internal static void ResetStaticState()");

            AssertItemProducerValidatesBeforeReserve(collected);
            AssertItemProducerValidatesBeforeReserve(lost);
            StringAssert.Contains("private static readonly ushort[] _referenceSlotGenerations", source);
            StringAssert.Contains("private static bool IsReferenceSlotPayloadCurrent(", source);
            StringAssert.Contains("payload.Reserved != 0", source);
            StringAssert.Contains("_droppedInvalidItemEventCount++;", invalidItemReport);
            StringAssert.Contains("InteractionInvalidItemEventWarningHash", invalidItemReport);
            StringAssert.Contains("InteractionInvalidItemEventContextHash", invalidItemReport);
            StringAssert.Contains("_lastInvalidItemEventTelemetryFrame = -1;", reset);
        }

        [Test]
        public void ReferenceSlotScanExhaustionReportsDroppedSlotWhenOccupancyMapIsStale()
        {
            InvokeResetStaticState();
            try
            {
                bool[] occupied = GetPrivateStaticBoolArray("_referenceSlotOccupied");
                for (int i = 0; i < occupied.Length; i++)
                    occupied[i] = true;

                Assert.AreEqual(0, GetPrivateStaticInt("_referencePendingCount"));
                Assert.IsFalse(InteractionEvents.TryRaiseInteractionStarted(new DummyInteractable(), null));

                Assert.AreEqual(0, InteractionEvents.PendingCount);
                Assert.AreEqual(0, GetPrivateStaticInt("_referencePendingCount"));
                Assert.AreEqual(0, InteractionEvents.DroppedEventCount);
                Assert.AreEqual(1, InteractionEvents.DroppedReferenceSlotCount);
            }
            finally
            {
                InvokeResetStaticState();
            }
        }

        [Test]
        public void FlushPendingContinuesAndReleasesReferenceSlotWhenListenerThrows()
        {
            InvokeResetStaticState();
            try
            {
                RecordingInteractionEventListener recorder = new RecordingInteractionEventListener();
                ThrowingInteractionEventListener throwing = new ThrowingInteractionEventListener();
                DummyInteractable target = new DummyInteractable();

                InteractionEvents.Register(recorder);
                InteractionEvents.Register(throwing);

                Assert.IsTrue(InteractionEvents.TryRaiseInteractionStarted(target, null));
                Assert.AreEqual(1, InteractionEvents.PendingCount);
                Assert.AreEqual(1, GetPrivateStaticInt("_referencePendingCount"));

                LogAssert.Expect(
                    LogType.Exception,
                    new Regex("InvalidOperationException: simulated interaction listener failure"));
                InteractionEvents.FlushPending();

                Assert.AreEqual(1, recorder.ReceivedCount);
                Assert.AreEqual((ushort)InteractionEventType.InteractionStarted, recorder.LastEventType);
                Assert.AreSame(target, recorder.LastTarget);
                Assert.AreEqual(0, InteractionEvents.PendingCount);
                Assert.AreEqual(0, GetPrivateStaticInt("_referencePendingCount"));
                Assert.AreEqual(1, InteractionEvents.ListenerExceptionCount);
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
                RecordingInteractionEventListener recorder = new RecordingInteractionEventListener();
                SelfUnregisteringInteractionEventListener selfUnregistering = new SelfUnregisteringInteractionEventListener();

                InteractionEvents.Register(recorder);
                InteractionEvents.Register(selfUnregistering);

                Assert.IsTrue(InteractionEvents.TryRaiseInteractionStarted(new DummyInteractable(), null));
                Assert.IsTrue(InteractionEvents.TryRaiseInteractionStarted(new DummyInteractable(), null));

                InteractionEvents.FlushPending();

                Assert.AreEqual(2, recorder.ReceivedCount);
                Assert.AreEqual(1, selfUnregistering.ReceivedCount);
                Assert.AreEqual(0, InteractionEvents.PendingCount);
                Assert.AreEqual(0, GetPrivateStaticInt("_referencePendingCount"));
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
                RecordingInteractionEventListener deferredRecorder = new RecordingInteractionEventListener();
                RegisteringInteractionEventListener registering = new RegisteringInteractionEventListener(deferredRecorder);

                InteractionEvents.Register(registering);

                Assert.IsTrue(InteractionEvents.TryRaiseInteractionStarted(new DummyInteractable(), null));
                Assert.IsTrue(InteractionEvents.TryRaiseInteractionStarted(new DummyInteractable(), null));

                InteractionEvents.FlushPending();

                Assert.AreEqual(2, registering.ReceivedCount);
                Assert.AreEqual(1, deferredRecorder.ReceivedCount);
                Assert.AreEqual((ushort)InteractionEventType.InteractionStarted, deferredRecorder.LastEventType);
                Assert.AreEqual(0, InteractionEvents.PendingCount);
                Assert.AreEqual(0, GetPrivateStaticInt("_referencePendingCount"));
            }
            finally
            {
                InvokeResetStaticState();
            }
        }

        [Test]
        public void TryRaiseAndPrewarmFailClosedWhenQueueInitializationFails()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Interaction/InteractionEvents.cs"));
            string prewarm = ExtractMethodBody(source, "internal static void PrewarmCold()");
            string enqueue = ExtractMethodBody(source, "private static bool Enqueue(");
            string report = ExtractMethodBody(source, "private static void ReportQueueInitializationFailure(");
            string publishBestEffort = ExtractMethodBody(source, "private static void PublishPerformanceWarningBestEffort(");
            string log = ExtractMethodBody(source, "private static void LogQueueInitializationException(");

            StringAssert.Contains("try", prewarm);
            StringAssert.Contains("EnsureInitialized();", prewarm);
            StringAssert.Contains("catch (Exception exception)", prewarm);
            StringAssert.Contains("ReportQueueInitializationFailure((ushort)InteractionEventType.InteractionStarted, exception);", prewarm);
            StringAssert.Contains("try", enqueue);
            StringAssert.Contains("EnsureInitialized();", enqueue);
            StringAssert.Contains("catch (Exception exception)", enqueue);
            StringAssert.Contains("ReleaseReferenceSlotForPayload(in payload);", enqueue);
            StringAssert.Contains("ReportQueueInitializationFailure(payload.EventType, exception);", enqueue);
            StringAssert.Contains("return false;", enqueue);
            StringAssert.Contains("InteractionQueueInitializationFailureWarningHash", report);
            StringAssert.Contains("_droppedEventCount++;", report);
            StringAssert.Contains("int frame = ResolveCurrentFrameIndexSafe();", report);
            StringAssert.Contains("PublishPerformanceWarningBestEffort(", report);
            StringAssert.Contains("try", publishBestEffort);
            StringAssert.Contains("catch (Exception telemetryException)", publishBestEffort);
            StringAssert.Contains("try", log);
            StringAssert.Contains("catch", log);
        }

        [Test]
        public void OverflowAndListenerDiagnosticsCannotBreakInteractionEventFlow()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Interaction/InteractionEvents.cs"));
            string queueOverflow = ExtractMethodBody(source, "private static void ReportQueueOverflow(");
            string referenceSlotExhausted = ExtractMethodBody(source, "private static void ReportReferenceSlotExhausted(");
            string listenerOverflow = ExtractMethodBody(source, "private static void ReportListenerRegistrationOverflow()");
            string listenerDispatch = ExtractMethodBody(source, "private static void ReportListenerDispatchException()");
            string logDispatch = ExtractMethodBody(source, "private static void LogListenerDispatchException(");

            StringAssert.Contains("int frame = ResolveCurrentFrameIndexSafe();", queueOverflow);
            StringAssert.Contains("PublishPerformanceWarningBestEffort(", queueOverflow);
            StringAssert.Contains("int frame = ResolveCurrentFrameIndexSafe();", referenceSlotExhausted);
            StringAssert.Contains("PublishPerformanceWarningBestEffort(", referenceSlotExhausted);
            StringAssert.Contains("int frame = ResolveCurrentFrameIndexSafe();", listenerOverflow);
            StringAssert.Contains("PublishPerformanceWarningBestEffort(", listenerOverflow);
            StringAssert.Contains("int frame = ResolveCurrentFrameIndexSafe();", listenerDispatch);
            StringAssert.Contains("PublishPerformanceWarningBestEffort(", listenerDispatch);
            StringAssert.Contains("try", logDispatch);
            StringAssert.Contains("Hecton8.Core.H8Debug.LogException(exception);", logDispatch);
            StringAssert.Contains("catch", logDispatch);
            StringAssert.DoesNotContain("GlobalTelemetryBus.PublishPerformanceWarning(", queueOverflow);
            StringAssert.DoesNotContain("GlobalTelemetryBus.PublishPerformanceWarning(", referenceSlotExhausted);
            StringAssert.DoesNotContain("GlobalTelemetryBus.PublishPerformanceWarning(", listenerOverflow);
            StringAssert.DoesNotContain("GlobalTelemetryBus.PublishPerformanceWarning(", listenerDispatch);
        }

        [Test]
        public void ResetStaticStateReleasesNativeQueuesBestEffortBeforeClearingState()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Interaction/InteractionEvents.cs"));
            string reset = ExtractMethodBody(source, "internal static void ResetStaticState()");
            string ensureInitialized = ExtractMethodBody(source, "private static void EnsureInitialized()");
            string releaseBestEffort = ExtractMethodBody(source, "private static Exception ReleaseNativeQueuesBestEffort()");
            string releaseQueueBestEffort = ExtractMethodBody(source, "private static void ReleaseNativeQueueBestEffort<T>");
            string releaseQueue = ExtractMethodBody(source, "private static void ReleaseNativeQueue<T>");
            string reportRelease = ExtractMethodBody(source, "private static void ReportQueueReleaseFailure(");

            int releaseIndex = reset.IndexOf("Exception releaseException = ReleaseNativeQueuesBestEffort();", StringComparison.Ordinal);
            int clearIndex = reset.IndexOf("_listeners.Clear();", StringComparison.Ordinal);
            int reportIndex = reset.IndexOf("ReportQueueReleaseFailure(releaseException);", StringComparison.Ordinal);

            Assert.GreaterOrEqual(releaseIndex, 0);
            Assert.Greater(clearIndex, releaseIndex);
            Assert.Greater(reportIndex, clearIndex);
            StringAssert.Contains("Exception releaseException = ReleaseNativeQueuesBestEffort();", ensureInitialized);
            StringAssert.Contains("ReportQueueReleaseFailure(releaseException);", ensureInitialized);
            StringAssert.Contains("ReleaseNativeQueueBestEffort(ref _pendingEvents, ref _pendingEventsSentinelId, ref firstException);", releaseBestEffort);
            StringAssert.Contains("ReleaseNativeQueueBestEffort(ref _nextFrameEvents, ref _nextFrameEventsSentinelId, ref firstException);", releaseBestEffort);
            StringAssert.Contains("catch (Exception exception)", releaseQueueBestEffort);
            StringAssert.Contains("if (firstException == null)", releaseQueueBestEffort);
            StringAssert.Contains("Exception firstException = null;", releaseQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseQueue);
            StringAssert.DoesNotContain("disposed = true;", releaseQueue);
            StringAssert.Contains("catch (Exception exception)", releaseQueue);
            StringAssert.DoesNotContain("if (disposed)", releaseQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseQueue);
            StringAssert.Contains("queue.Dispose();", releaseQueue);
            StringAssert.Contains("sentinelId = 0;", releaseQueue);
            StringAssert.Contains("queue = default;", releaseQueue);
            StringAssert.Contains("if (firstException != null)", releaseQueue);
            StringAssert.Contains("throw firstException;", releaseQueue);
            StringAssert.DoesNotContain("queueReleased", releaseQueue);
            Assert.Less(
                releaseQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                releaseQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseQueue.IndexOf("sentinelId = 0;", StringComparison.Ordinal));
            StringAssert.Contains("InteractionQueueReleaseFailureWarningHash", reportRelease);
            StringAssert.Contains("PublishPerformanceWarningBestEffort(", reportRelease);
            StringAssert.Contains("LogQueueInitializationException(exception);", reportRelease);
        }

        private sealed class DummyInteractable : IInteractable
        {
            public void OnHoverStart()
            {
            }

            public void OnHoverEnd()
            {
            }

            public void Interact(Transform interactor)
            {
            }

            public string GetInteractText()
            {
                return "DUMMY";
            }
        }

        private sealed class RecordingInteractionEventListener : IInteractionEventListener
        {
            public int ReceivedCount;
            public ushort LastEventType;
            public IInteractable LastTarget;
            public ItemData LastItem;
            public int LastQuantity;
            public InteractionEventPayload LastPayload;

            public void OnInteractionEvent(in InteractionEventPayload payload)
            {
                ReceivedCount++;
                LastEventType = payload.EventType;
                LastQuantity = payload.Quantity;
                LastPayload = payload;
                InteractionEvents.TryResolveItem(in payload, out LastItem);
                InteractionEvents.TryResolveTarget(in payload, out LastTarget);
            }
        }

        private sealed class ThrowingInteractionEventListener : IInteractionEventListener
        {
            public void OnInteractionEvent(in InteractionEventPayload payload)
            {
                throw new InvalidOperationException("simulated interaction listener failure");
            }
        }

        private sealed class SelfUnregisteringInteractionEventListener : IInteractionEventListener
        {
            public int ReceivedCount;

            public void OnInteractionEvent(in InteractionEventPayload payload)
            {
                ReceivedCount++;
                InteractionEvents.Unregister(this);
            }
        }

        private sealed class RegisteringInteractionEventListener : IInteractionEventListener
        {
            private readonly IInteractionEventListener _listenerToRegister;
            private bool _registered;

            public int ReceivedCount;

            public RegisteringInteractionEventListener(IInteractionEventListener listenerToRegister)
            {
                _listenerToRegister = listenerToRegister;
            }

            public void OnInteractionEvent(in InteractionEventPayload payload)
            {
                ReceivedCount++;
                if (_registered)
                    return;

                _registered = true;
                InteractionEvents.Register(_listenerToRegister);
            }
        }

        private static int ResolvePrivateConstInt(string fieldName)
        {
            FieldInfo field = typeof(InteractionEvents).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Missing InteractionEvents const: " + fieldName);
            return (int)field.GetRawConstantValue();
        }

        private static int GetPrivateStaticInt(string fieldName)
        {
            FieldInfo field = typeof(InteractionEvents).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Missing InteractionEvents field: " + fieldName);
            return (int)field.GetValue(null);
        }

        private static void SetPrivateStaticInt(string fieldName, int value)
        {
            FieldInfo field = typeof(InteractionEvents).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Missing InteractionEvents field: " + fieldName);
            field.SetValue(null, value);
        }

        private static bool[] GetPrivateStaticBoolArray(string fieldName)
        {
            FieldInfo field = typeof(InteractionEvents).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Missing InteractionEvents field: " + fieldName);
            return (bool[])field.GetValue(null);
        }

        private static void AssertNoOccupiedReferenceSlots()
        {
            bool[] occupied = GetPrivateStaticBoolArray("_referenceSlotOccupied");
            for (int i = 0; i < occupied.Length; i++)
                Assert.IsFalse(occupied[i], "Reference slot remained occupied: " + i);
        }

        private static ItemData CreateItemData(string stableId)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.name = stableId;
            SetPrivateInstanceField(item, "stableId", stableId);
            InvokePrivateInstanceMethod(item, "RefreshPersistentHash");
            return item;
        }

        private static void SetPrivateInstanceField<TValue>(object target, string fieldName, TValue value)
        {
            Assert.IsNotNull(target);
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private static void InvokePrivateInstanceMethod(object target, string methodName)
        {
            Assert.IsNotNull(target);
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            method.Invoke(target, null);
        }

        private static void InvokeResetStaticState()
        {
            MethodInfo reset = typeof(InteractionEvents).GetMethod("ResetStaticState", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(reset, "Missing InteractionEvents.ResetStaticState");
            reset.Invoke(null, null);
        }

        private static void AssertItemProducerValidatesBeforeReserve(string methodBody)
        {
            int hashIndex = methodBody.IndexOf("uint itemHashId = ComputeItemHash(item);", StringComparison.Ordinal);
            int guardIndex = methodBody.IndexOf("if (quantity <= 0 || itemHashId == 0u)", StringComparison.Ordinal);
            int reportIndex = methodBody.IndexOf("ReportInvalidItemEvent(", StringComparison.Ordinal);
            int reserveIndex = methodBody.IndexOf("TryReserveReferenceSlot(", StringComparison.Ordinal);
            Assert.GreaterOrEqual(hashIndex, 0, methodBody);
            Assert.Greater(guardIndex, hashIndex, methodBody);
            Assert.Greater(reportIndex, guardIndex, methodBody);
            Assert.Greater(reserveIndex, reportIndex, methodBody);
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
