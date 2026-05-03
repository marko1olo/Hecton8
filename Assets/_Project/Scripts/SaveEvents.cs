using System.Diagnostics;
using Hecton8.Core;
using Unity.Collections;

namespace Hecton8.SaveSystem
{
    public enum SaveEventType : byte
    {
        SaveStarted = 0,
        SaveCompleted = 1,
        SaveFailed = 2,
        LoadStarted = 3,
        LoadCompleted = 4,
        LoadFailed = 5,
        EmergencyBackupRestoreRequested = 6
    }

    public struct SaveEventPayload
    {
        public SaveEventType Type;
        public ulong TimestampTicks;
        public FixedString64Bytes SlotName;
        public FixedString128Bytes Message;
    }

    public interface ISaveEventListener
    {
        void OnSaveEvent(in SaveEventPayload payload);
    }

    public static class SaveEvents
    {
        private const int PendingEventCapacity = 16;

        // COLD ALLOC: RegistryBucket<ISaveEventListener>[16] - save event listener registry drained on dispatcher LateUpdate - owner: SaveEvents
        private static readonly RegistryBucket<ISaveEventListener> _listeners = new RegistryBucket<ISaveEventListener>(16);
        private static NativeQueue<SaveEventPayload> _pendingEvents;
        private static NativeQueue<SaveEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SaveEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SaveEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
        }

        public static void Register(ISaveEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        public static void Unregister(ISaveEventListener listener)
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

                if (!_pendingEvents.TryDequeue(out SaveEventPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                ISaveEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        ISaveEventListener listener = rawArray[i];
                        if (listener != null)
                            listener.OnSaveEvent(in payload);
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

        public static void RaiseSaveStarted(string slot)
        {
            Enqueue(SaveEventType.SaveStarted, slot, default);
        }

        public static void RaiseSaveCompleted(string slot)
        {
            Enqueue(SaveEventType.SaveCompleted, slot, default);
        }

        public static void RaiseSaveFailed(string slot, string error)
        {
            Enqueue(SaveEventType.SaveFailed, slot, error);
        }

        public static void RaiseLoadStarted(string slot)
        {
            Enqueue(SaveEventType.LoadStarted, slot, default);
        }

        public static void RaiseLoadCompleted(string slot)
        {
            Enqueue(SaveEventType.LoadCompleted, slot, default);
        }

        public static void RaiseLoadFailed(string slot, string error)
        {
            Enqueue(SaveEventType.LoadFailed, slot, error);
        }

        public static void RaiseEmergencyBackupRestoreRequested(string slot)
        {
            Enqueue(SaveEventType.EmergencyBackupRestoreRequested, slot, default);
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<SaveEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SaveEventPayload>[16] - deferred save event lane flushed by SystemDispatcher LateUpdate - owner: SaveEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(SaveEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<SaveEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SaveEventPayload>[16] - next-frame save event lane prevents same-frame reentrant dispatch - owner: SaveEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(SaveEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
            }
        }

        private static void Enqueue(SaveEventType type, string slot, string message)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

            SaveEventPayload payload = new SaveEventPayload
            {
                Type = type,
                TimestampTicks = unchecked((ulong)Stopwatch.GetTimestamp()),
                SlotName = string.IsNullOrEmpty(slot) ? default : slot,
                Message = string.IsNullOrEmpty(message) ? default : message
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
            ref NativeQueue<SaveEventPayload> queue,
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

            NativeQueue<SaveEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }
}
