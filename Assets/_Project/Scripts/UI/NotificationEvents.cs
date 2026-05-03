using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Unity.Collections;

namespace Hecton8.UI
{
    public enum NotificationEventSeverity : byte
    {
        Info = 0,
        Warning = 1,
        Critical = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NotificationEventPayload
    {
        public uint MessageHash;
        public ushort Severity;
        public ushort Reserved;
    }

    public interface INotificationEventListener
    {
        void OnNotificationEvent(in NotificationEventPayload payload);
    }

    public static class NotificationEvents
    {
        private const int PendingEventCapacity = 8;

        // COLD ALLOC: RegistryBucket<INotificationEventListener>[8] - HUD notification listeners drained on dispatcher LateUpdate - owner: NotificationEvents
        private static readonly RegistryBucket<INotificationEventListener> _listeners = new RegistryBucket<INotificationEventListener>(8);
        // COLD ALLOC: Dictionary<uint,string>[64] - notification message registry keyed by stable FNV-1a hash for cold-path UI resolution - owner: NotificationEvents
        private static readonly Dictionary<uint, string> _messagesByHash = new Dictionary<uint, string>(64);
        private static NativeQueue<NotificationEventPayload> _pendingEvents;
        private static NativeQueue<NotificationEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(NotificationEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(NotificationEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            _messagesByHash.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
        }

        public static void Register(INotificationEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        public static void Unregister(INotificationEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated || _listeners.Count <= 0)
            {
                DrainWithoutDispatch();
                return;
            }

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out NotificationEventPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                INotificationEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        INotificationEventListener listener = rawArray[i];
                        if (listener != null)
                            listener.OnNotificationEvent(in payload);
                    }
                }
                finally
                {
                    _isDispatching = false;
                }
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        public static uint ComputeMessageHash(string message)
        {
            return string.IsNullOrWhiteSpace(message)
                ? 0u
                : unchecked((uint)LocHash.Compute(message));
        }

        public static uint RegisterMessage(string message)
        {
            uint messageHash = ComputeMessageHash(message);
            if (messageHash == 0u)
                return 0u;

            if (!_messagesByHash.ContainsKey(messageHash))
                _messagesByHash.Add(messageHash, message);

            return messageHash;
        }

        public static bool TryResolveMessage(uint messageHash, out string message)
        {
            return _messagesByHash.TryGetValue(messageHash, out message);
        }

        public static void PushInfo(string message)
        {
            Publish(message, NotificationEventSeverity.Info);
        }

        public static void PushWarning(string message)
        {
            Publish(message, NotificationEventSeverity.Warning);
        }

        public static void PushCritical(string message)
        {
            Publish(message, NotificationEventSeverity.Critical);
        }

        internal static void PushRegisteredInfo(uint messageHash)
        {
            PublishRegistered(messageHash, NotificationEventSeverity.Info);
        }

        internal static void PushRegisteredWarning(uint messageHash)
        {
            PublishRegistered(messageHash, NotificationEventSeverity.Warning);
        }

        internal static void PushRegisteredCritical(uint messageHash)
        {
            PublishRegistered(messageHash, NotificationEventSeverity.Critical);
        }

        private static void Publish(string message, NotificationEventSeverity severity)
        {
            uint messageHash = ComputeMessageHash(message);
            if (messageHash == 0u)
                return;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

            if (!_messagesByHash.ContainsKey(messageHash))
                _messagesByHash.Add(messageHash, message);

            NotificationEventPayload payload = new NotificationEventPayload
            {
                MessageHash = messageHash,
                Severity = (ushort)severity,
                Reserved = 0
            };

            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
                return;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
        }

        private static void PublishRegistered(uint messageHash, NotificationEventSeverity severity)
        {
            if (messageHash == 0u || !_messagesByHash.ContainsKey(messageHash))
                return;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

            NotificationEventPayload payload = new NotificationEventPayload
            {
                MessageHash = messageHash,
                Severity = (ushort)severity,
                Reserved = 0
            };

            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
                return;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<NotificationEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<NotificationEventPayload>[8] - deferred notification lane flushed by SystemDispatcher LateUpdate - owner: NotificationEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(NotificationEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<NotificationEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<NotificationEventPayload>[8] - next-frame notification lane prevents same-frame reentrant dispatch - owner: NotificationEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(NotificationEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
            }
        }

        private static void DrainWithoutDispatch()
        {
            if (!_pendingEvents.IsCreated)
                return;

            if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                return;

            if (_pendingEvents.IsEmpty())
                PromoteNextFrameEventsIfFrontEmpty();

            if (_pendingEventCount > 0 &&
                !DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
            {
                return;
            }

            if (_nextFrameEvents.IsCreated)
                DrainQueueWithoutDispatch(ref _nextFrameEvents, ref _nextFrameEventCount);
        }

        private static bool DrainQueueWithoutDispatch(
            ref NativeQueue<NotificationEventPayload> queue,
            ref int pendingCount)
        {
            int scanBudget = pendingCount > 0 ? pendingCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !queue.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!queue.TryDequeue(out _))
                    break;

                if (pendingCount > 0)
                    pendingCount--;
            }

            if (queue.IsEmpty())
                pendingCount = 0;

            return true;
        }

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!_pendingEvents.IsCreated ||
                !_nextFrameEvents.IsCreated ||
                !_pendingEvents.IsEmpty() ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            NativeQueue<NotificationEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }
}
