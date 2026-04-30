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
        // COLD ALLOC: RegistryBucket<INotificationEventListener>[8] - HUD notification listeners drained on dispatcher LateUpdate - owner: NotificationEvents
        private static readonly RegistryBucket<INotificationEventListener> _listeners = new RegistryBucket<INotificationEventListener>(8);
        // COLD ALLOC: Dictionary<uint,string>[64] - notification message registry keyed by stable FNV-1a hash for cold-path UI resolution - owner: NotificationEvents
        private static readonly Dictionary<uint, string> _messagesByHash = new Dictionary<uint, string>(64);
        private static NativeQueue<NotificationEventPayload> _pendingEvents;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            _listeners.Clear();
            _messagesByHash.Clear();
        }

        public static void Register(INotificationEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            _listeners.Register(listener);
        }

        public static void Unregister(INotificationEventListener listener)
        {
            if (listener == null)
                return;

            _listeners.Unregister(listener);
        }

        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated || _listeners.Count <= 0)
            {
                DrainWithoutDispatch();
                return;
            }

            while (!_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out NotificationEventPayload payload))
                    return;

                INotificationEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                    rawArray[i].OnNotificationEvent(in payload);
            }
        }

        public static uint ComputeMessageHash(string message)
        {
            return string.IsNullOrWhiteSpace(message)
                ? 0u
                : unchecked((uint)LocHash.Compute(message));
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

        private static void Publish(string message, NotificationEventSeverity severity)
        {
            uint messageHash = ComputeMessageHash(message);
            if (messageHash == 0u)
                return;

            if (!_messagesByHash.ContainsKey(messageHash))
                _messagesByHash.Add(messageHash, message);

            EnsureInitialized();
            _pendingEvents.Enqueue(new NotificationEventPayload
            {
                MessageHash = messageHash,
                Severity = (ushort)severity,
                Reserved = 0
            });
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<NotificationEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<NotificationEventPayload>[8] - deferred notification lane flushed by SystemDispatcher LateUpdate - owner: NotificationEvents
            }
        }

        private static void DrainWithoutDispatch()
        {
            if (!_pendingEvents.IsCreated)
                return;

            while (_pendingEvents.TryDequeue(out _))
            {
            }
        }
    }
}
