using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class ModRegistryEventsOverflowEditTests
    {
        [Test]
        public void QueueOverflowCoalescesDroppedRegistryChangeForRetry()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModRegistryEvents.cs");
            string enqueue = ExtractMethodBody(source, "private static void Enqueue(");
            string replay = ExtractMethodBody(source, "private static void ReplayOverflowedEvents()");
            string tryReplay = ExtractMethodBody(source, "private static void TryReplayOverflowedEvent(");
            string markOverflowed = ExtractMethodBody(source, "private static void MarkOverflowedIfNotAlreadyQueued(");
            string flush = ExtractMethodBody(source, "internal static void FlushPending()");
            string drain = ExtractMethodBody(source, "private static void DrainPendingEventsWithoutDispatch()");
            string reset = ExtractMethodBody(source, "internal static void ResetStaticState()");

            StringAssert.Contains("internal static int DroppedEventCount => _droppedEventCount;", source);
            StringAssert.Contains("private static bool _runtimeRegistryChangeOverflowed;", source);
            Assert.IsTrue(ContainsTokensInOrder(
                enqueue,
                "if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)",
                "MarkOverflowedIfNotAlreadyQueued(eventType);",
                "ReportQueueOverflow(eventType);",
                "return;",
                "if (!TryMarkQueued(eventType))"));
            Assert.IsTrue(ContainsTokensInOrder(
                markOverflowed,
                "if (IsQueued(eventType))",
                "return;",
                "case ModRegistryEventType.RuntimeRegistryChanged:",
                "_runtimeRegistryChangeOverflowed = true;"));
            Assert.IsTrue(ContainsTokensInOrder(
                replay,
                "TryReplayOverflowedEvent(ModRegistryEventType.RuntimeRegistryChanged, ref _runtimeRegistryChangeOverflowed);",
                "TryReplayOverflowedEvent(ModRegistryEventType.SettingsRegistryChanged, ref _settingsRegistryChangeOverflowed);",
                "TryReplayOverflowedEvent(ModRegistryEventType.RecipeRegistryChanged, ref _recipeRegistryChangeOverflowed);",
                "TryReplayOverflowedEvent(ModRegistryEventType.BuildableRegistryChanged, ref _buildableRegistryChangeOverflowed);"));
            Assert.IsTrue(ContainsTokensInOrder(
                tryReplay,
                "if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)",
                "return;",
                "if (IsQueued(eventType))",
                "overflowed = false;",
                "Enqueue(eventType, 0u, 0u, 1);"));
            Assert.IsTrue(ContainsTokensInOrder(
                flush,
                "_isDispatching = false;",
                "ReplayOverflowedEvents();",
                "PromoteNextFrameEventsIfFrontEmpty();",
                "ReplayOverflowedEvents();"));
            StringAssert.Contains("ClearOverflowedFlags();", drain);
            StringAssert.Contains("_droppedEventCount = 0;", reset);
            StringAssert.Contains("_lastQueueOverflowTelemetryFrame = -1;", reset);
            StringAssert.Contains("ClearOverflowedFlags();", reset);
        }

        [Test]
        public void QueueOverflowPublishesFrameLimitedTelemetry()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModRegistryEvents.cs");
            string report = ExtractMethodBody(source, "private static void ReportQueueOverflow(");
            string publish = ExtractMethodBody(source, "private static void PublishPerformanceWarningBestEffort(");

            StringAssert.Contains("RegistryEventQueueOverflowWarningHash = 0x4D524F46u", source);
            StringAssert.Contains("RegistryEventQueueContextHash = 0x4D524551u", source);
            Assert.IsTrue(ContainsTokensInOrder(
                report,
                "_droppedEventCount++;",
                "int frame = ResolveCurrentFrameIndexSafe();",
                "if (_lastQueueOverflowTelemetryFrame == frame)",
                "return;",
                "_lastQueueOverflowTelemetryFrame = frame;",
                "PublishPerformanceWarningBestEffort(",
                "RegistryEventQueueOverflowWarningHash",
                "RegistryEventQueueContextHash ^ ((uint)eventType << 24)",
                "_droppedEventCount);"));
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, value);", publish);
            StringAssert.Contains("catch (Exception exception)", publish);
            StringAssert.Contains("H8Debug.LogWarning(\"[ModRegistryEvents] telemetry failed: \" + exception.Message);", publish);
        }

        [Test]
        public void ListenerOverflowPublishesFrameLimitedTelemetry()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModRegistryEvents.cs");
            string registerPublic = ExtractMethodBody(source, "internal static bool Register(IModRegistryEventListener listener)");
            string register = ExtractMethodBody(source, "private static bool RegisterImmediate(");
            string report = ExtractMethodBody(source, "private static void ReportListenerRegistrationOverflow()");

            StringAssert.Contains("internal static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;", source);
            StringAssert.Contains("RegistryEventListenerOverflowWarningHash = 0x4D524C46u", source);
            StringAssert.Contains("RegistryEventListenerContextHash = 0x4D524C51u", source);
            Assert.IsTrue(ContainsTokensInOrder(
                registerPublic,
                "if (listener == null)",
                "return false;",
                "EnsureInitialized();",
                "return RegisterImmediate(listener);"));
            Assert.IsTrue(ContainsTokensInOrder(
                register,
                "if (ReferenceEquals(_listeners[i].Listener, listener))",
                "return true;",
                "if (_listenerCount >= ListenerCapacity)",
                "ReportListenerRegistrationOverflow();",
                "return false;",
                "_listeners[_listenerCount++].Listener = listener;",
                "return true;"));
            Assert.IsTrue(ContainsTokensInOrder(
                report,
                "_droppedListenerRegistrationCount++;",
                "int frame = ResolveCurrentFrameIndexSafe();",
                "if (_lastListenerOverflowTelemetryFrame == frame)",
                "return;",
                "_lastListenerOverflowTelemetryFrame = frame;",
                "PublishPerformanceWarningBestEffort(",
                "RegistryEventListenerOverflowWarningHash",
                "RegistryEventListenerContextHash",
                "_droppedListenerRegistrationCount);"));
        }

        [Test]
        public void ListenerMutationDuringDispatchUsesStableSnapshot()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModRegistryEvents.cs");
            string flush = ExtractMethodBody(source, "internal static void FlushPending()");
            string capture = ExtractMethodBody(source, "private static void CaptureDispatchSnapshot(");
            string clear = ExtractMethodBody(source, "private static void ClearDispatchSnapshot(");
            string reset = ExtractMethodBody(source, "internal static void ResetStaticState()");

            StringAssert.Contains("private static readonly IModRegistryEventListener[] _dispatchListeners", source);
            Assert.IsTrue(ContainsTokensInOrder(
                flush,
                "int count = _listenerCount;",
                "CaptureDispatchSnapshot(count);",
                "_isDispatching = true;",
                "IModRegistryEventListener listener = _dispatchListeners[i];",
                "listener.OnModRegistryEvent(in payload);",
                "finally",
                "ClearDispatchSnapshot(count);",
                "_isDispatching = false;"));
            Assert.IsTrue(ContainsTokensInOrder(
                capture,
                "int safeCount = Mathf.Clamp(count, 0, ListenerCapacity);",
                "for (int i = 0; i < safeCount; i++)",
                "_dispatchListeners[i] = _listeners[i].Listener;"));
            Assert.IsTrue(ContainsTokensInOrder(
                clear,
                "int safeCount = Mathf.Clamp(count, 0, ListenerCapacity);",
                "for (int i = 0; i < safeCount; i++)",
                "_dispatchListeners[i] = null;"));
            StringAssert.Contains("ClearDispatchSnapshot(ListenerCapacity);", reset);
        }

        [Test]
        public void ListenerExceptionDuringDispatchIsTelemetryIsolated()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModRegistryEvents.cs");
            string flush = ExtractMethodBody(source, "internal static void FlushPending()");
            string report = ExtractMethodBody(source, "private static void ReportListenerDispatchException(");
            string reset = ExtractMethodBody(source, "internal static void ResetStaticState()");

            StringAssert.Contains("internal static int ListenerExceptionCount => _listenerExceptionCount;", source);
            StringAssert.Contains("RegistryEventListenerExceptionWarningHash = 0x4D524558u", source);
            StringAssert.Contains("RegistryEventListenerExceptionContextHash = 0x4D524543u", source);
            Assert.IsTrue(ContainsTokensInOrder(
                flush,
                "try",
                "listener.OnModRegistryEvent(in payload);",
                "catch (Exception exception)",
                "ReportListenerDispatchException(payload.EventType, exception);"));
            Assert.IsTrue(ContainsTokensInOrder(
                report,
                "_listenerExceptionCount++;",
                "int frame = ResolveCurrentFrameIndexSafe();",
                "if (_lastListenerExceptionTelemetryFrame != frame)",
                "_lastListenerExceptionTelemetryFrame = frame;",
                "PublishPerformanceWarningBestEffort(",
                "RegistryEventListenerExceptionWarningHash",
                "RegistryEventListenerExceptionContextHash ^ ((uint)eventType << 24)",
                "_listenerExceptionCount);",
                "H8Debug.LogWarning(\"[ModRegistryEvents] listener failed: \" + exception.Message);"));
            StringAssert.Contains("_listenerExceptionCount = 0;", reset);
            StringAssert.Contains("_lastListenerExceptionTelemetryFrame = -1;", reset);
        }

        private static string ReadProjectFile(string relativePath)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(root, relativePath));
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

        private static bool ContainsTokensInOrder(string text, params string[] tokens)
        {
            int index = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                int found = text.IndexOf(tokens[i], index, StringComparison.Ordinal);
                if (found < 0)
                    return false;

                index = found + tokens[i].Length;
            }

            return true;
        }
    }
}
