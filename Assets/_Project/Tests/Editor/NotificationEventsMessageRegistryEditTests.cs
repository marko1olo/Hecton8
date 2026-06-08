using System;
using System.IO;
using System.Reflection;
using Hecton8.UI;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class NotificationEventsMessageRegistryEditTests
    {
        [Test]
        public void RegisterMessageCopiesCallerSpanAndStringResolverReturnsText()
        {
            InvokeResetStaticState();
            try
            {
                char[] callerBuffer = "HULL BREACH".ToCharArray();
                uint messageHash = NotificationEvents.RegisterMessage(callerBuffer.AsSpan());
                Assert.AreNotEqual(0u, messageHash);

                callerBuffer[0] = 'N';
                callerBuffer[1] = 'O';

                Assert.IsTrue(NotificationEvents.TryResolveMessageSpan(messageHash, out ReadOnlySpan<char> resolvedSpan));
                Assert.AreEqual("HULL BREACH", resolvedSpan.ToString());

                Assert.IsTrue(NotificationEvents.TryResolveMessage(messageHash, out string resolvedString));
                Assert.AreEqual("HULL BREACH", resolvedString);
            }
            finally
            {
                InvokeResetStaticState();
            }
        }

        [Test]
        public void RegisterMessageRejectsBadDataAndResetClearsResolver()
        {
            InvokeResetStaticState();
            try
            {
                Assert.AreEqual(0u, NotificationEvents.RegisterMessage("   "));
                Assert.AreEqual(0u, NotificationEvents.RegisterMessage(new string('A', 513)));
                Assert.IsFalse(NotificationEvents.TryResolveMessage(0u, out string zeroHashMessage));
                Assert.AreEqual(string.Empty, zeroHashMessage);

                uint messageHash = NotificationEvents.RegisterMessage("OXYGEN LOW");
                Assert.AreNotEqual(0u, messageHash);
                Assert.IsTrue(NotificationEvents.TryResolveMessage(messageHash, out string resolved));
                Assert.AreEqual("OXYGEN LOW", resolved);

                InvokeResetStaticState();
                Assert.IsFalse(NotificationEvents.TryResolveMessage(messageHash, out string resetMessage));
                Assert.AreEqual(string.Empty, resetMessage);
            }
            finally
            {
                InvokeResetStaticState();
            }
        }

        [Test]
        public void RegisteredPushFailsClosedAndCountsMissingMessageHash()
        {
            InvokeResetStaticState();
            try
            {
                Assert.AreEqual(0, NotificationEvents.RegisteredMessageMissCount);
                Assert.IsFalse(InvokeTryPushRegisteredWarning(0x4D495353u));
                Assert.AreEqual(1, NotificationEvents.RegisteredMessageMissCount);

                uint messageHash = NotificationEvents.RegisterMessage("VALID MESSAGE");
                Assert.AreNotEqual(0u, messageHash);
                Assert.IsTrue(InvokeTryPushRegisteredWarning(messageHash));
                Assert.AreEqual(1, NotificationEvents.RegisteredMessageMissCount);
            }
            finally
            {
                InvokeResetStaticState();
            }
        }

        [Test]
        public void DeferredListenerMutationsApplyOnceAndSkipPendingUnregisters()
        {
            InvokeResetStaticState();
            try
            {
                uint messageHash = NotificationEvents.RegisterMessage("LIFECYCLE");
                Assert.AreNotEqual(0u, messageHash);

                RecordingListener lateListener = new RecordingListener();
                RecordingListener mutatingListener = new RecordingListener
                {
                    OnEvent = () =>
                    {
                        NotificationEvents.Register(lateListener);
                        NotificationEvents.Register(lateListener);
                        NotificationEvents.Unregister(lateListener);
                        NotificationEvents.Register(lateListener);
                    }
                };

                NotificationEvents.Register(mutatingListener);
                Assert.IsTrue(InvokeTryPushRegisteredWarning(messageHash));
                NotificationEvents.FlushPending();

                Assert.AreEqual(1, mutatingListener.ReceivedCount);
                Assert.AreEqual(0, lateListener.ReceivedCount);

                Assert.IsTrue(InvokeTryPushRegisteredWarning(messageHash));
                NotificationEvents.FlushPending();

                Assert.AreEqual(2, mutatingListener.ReceivedCount);
                Assert.AreEqual(1, lateListener.ReceivedCount);

                RecordingListener skippedTarget = new RecordingListener();
                RecordingListener unregisteringListener = new RecordingListener
                {
                    OnEvent = () => NotificationEvents.Unregister(skippedTarget)
                };

                NotificationEvents.Register(skippedTarget);
                NotificationEvents.Register(unregisteringListener);
                Assert.IsTrue(InvokeTryPushRegisteredWarning(messageHash));
                NotificationEvents.FlushPending();

                Assert.AreEqual(1, unregisteringListener.ReceivedCount);
                Assert.AreEqual(0, skippedTarget.ReceivedCount);
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
                NotificationEventOrderLog orderLog = new NotificationEventOrderLog();
                OrderRecordingListener recorder = new OrderRecordingListener(orderLog, "recorder");
                ReentrantFlushListener reentrant = new ReentrantFlushListener(orderLog, "reentrant");
                uint firstHash = NotificationEvents.ComputeMessageHash("FIRST NOTICE");
                uint secondHash = NotificationEvents.ComputeMessageHash("SECOND NOTICE");

                NotificationEvents.Register(recorder);
                NotificationEvents.Register(reentrant);

                Assert.IsTrue(NotificationEvents.TryPushInfo("FIRST NOTICE"));
                Assert.IsTrue(NotificationEvents.TryPushWarning("SECOND NOTICE"));

                NotificationEvents.FlushPending();

                Assert.AreEqual(4, orderLog.Count);
                Assert.AreEqual("reentrant", orderLog.ListenerAt(0));
                Assert.AreEqual(firstHash, orderLog.HashAt(0));
                Assert.AreEqual(NotificationEventSeverity.Info, orderLog.SeverityAt(0));
                Assert.AreEqual("recorder", orderLog.ListenerAt(1));
                Assert.AreEqual(firstHash, orderLog.HashAt(1));
                Assert.AreEqual(NotificationEventSeverity.Info, orderLog.SeverityAt(1));
                Assert.AreEqual("reentrant", orderLog.ListenerAt(2));
                Assert.AreEqual(secondHash, orderLog.HashAt(2));
                Assert.AreEqual(NotificationEventSeverity.Warning, orderLog.SeverityAt(2));
                Assert.AreEqual("recorder", orderLog.ListenerAt(3));
                Assert.AreEqual(secondHash, orderLog.HashAt(3));
                Assert.AreEqual(NotificationEventSeverity.Warning, orderLog.SeverityAt(3));
                Assert.AreEqual(0, NotificationEvents.PendingCount);
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
                "Assets/_Project/Scripts/UI/NotificationEvents.cs"));
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
        public void NoListenerDrainDoesNotConsumeLateFrameDispatchBudget()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/UI/NotificationEvents.cs"));
            string flush = ExtractMethodBody(source, "public static void FlushPending()");
            string drain = ExtractMethodBody(source, "private static void DrainWithoutDispatch()");
            string drainQueue = ExtractMethodBody(source, "private static void DrainQueueWithoutBudget(");

            StringAssert.Contains("silent stale-event cleanup must not steal shared LateFrame dispatch budget", flush);
            StringAssert.Contains("DrainQueueWithoutBudget(ref _pendingEvents, ref _pendingEventCount);", drain);
            StringAssert.Contains("if (_nextFrameEvents.IsCreated)", drain);
            StringAssert.Contains("DrainQueueWithoutBudget(ref _nextFrameEvents, ref _nextFrameEventCount);", drain);
            StringAssert.Contains("queue.TryDequeue(out _)", drainQueue);
            StringAssert.DoesNotContain("SystemDispatcher.TryConsumeLateFrameEventDispatch()", drainQueue);
        }

        private sealed class RecordingListener : INotificationEventListener
        {
            public int ReceivedCount;
            public Action OnEvent;

            public void OnNotificationEvent(in NotificationEventPayload payload)
            {
                ReceivedCount++;
                OnEvent?.Invoke();
            }
        }

        private sealed class OrderRecordingListener : INotificationEventListener
        {
            private readonly NotificationEventOrderLog _orderLog;
            private readonly string _name;

            public OrderRecordingListener(NotificationEventOrderLog orderLog, string name)
            {
                _orderLog = orderLog;
                _name = name;
            }

            public void OnNotificationEvent(in NotificationEventPayload payload)
            {
                _orderLog.Record(_name, in payload);
            }
        }

        private sealed class ReentrantFlushListener : INotificationEventListener
        {
            private readonly NotificationEventOrderLog _orderLog;
            private readonly string _name;

            public ReentrantFlushListener(NotificationEventOrderLog orderLog, string name)
            {
                _orderLog = orderLog;
                _name = name;
            }

            public void OnNotificationEvent(in NotificationEventPayload payload)
            {
                _orderLog.Record(_name, in payload);
                NotificationEvents.FlushPending();
            }
        }

        private sealed class NotificationEventOrderLog
        {
            private readonly string[] _listeners = new string[8];
            private readonly uint[] _hashes = new uint[8];
            private readonly NotificationEventSeverity[] _severities = new NotificationEventSeverity[8];

            public int Count { get; private set; }

            public void Record(string listenerName, in NotificationEventPayload payload)
            {
                Assert.Less(Count, _listeners.Length);
                _listeners[Count] = listenerName;
                _hashes[Count] = payload.MessageHash;
                _severities[Count] = (NotificationEventSeverity)payload.Severity;
                Count++;
            }

            public string ListenerAt(int index)
            {
                return _listeners[index];
            }

            public uint HashAt(int index)
            {
                return _hashes[index];
            }

            public NotificationEventSeverity SeverityAt(int index)
            {
                return _severities[index];
            }
        }

        private static bool InvokeTryPushRegisteredWarning(uint messageHash)
        {
            MethodInfo method = typeof(NotificationEvents).GetMethod(
                "TryPushRegisteredWarning",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "Missing NotificationEvents.TryPushRegisteredWarning");
            return (bool)method.Invoke(null, new object[] { messageHash });
        }

        private static void InvokeResetStaticState()
        {
            MethodInfo reset = typeof(NotificationEvents).GetMethod(
                "ResetStaticState",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(reset, "Missing NotificationEvents.ResetStaticState");
            reset.Invoke(null, null);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, $"Missing method signature: {signature}");

            int bodyStart = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(bodyStart, 0, $"Missing method body for: {signature}");

            int depth = 0;
            for (int i = bodyStart; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(bodyStart, i - bodyStart + 1);
                }
            }

            Assert.Fail($"Unclosed method body for: {signature}");
            return string.Empty;
        }
    }
}
