using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;

namespace Hecton8.Quest
{
    public readonly struct QuestRevertRequest
    {
        public QuestRevertRequest(uint questHash, uint itemHash, uint respawnEventHash, int questIndex)
        {
            QuestHash = questHash;
            ItemHash = itemHash;
            RespawnEventHash = respawnEventHash;
            QuestIndex = questIndex;
        }

        public uint QuestHash { get; }
        public uint ItemHash { get; }
        public uint RespawnEventHash { get; }
        public int QuestIndex { get; }
    }

    public enum QuestEventType : byte
    {
        Activated = 0,
        Completed = 1,
        Failed = 2,
        RevertRequested = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct QuestEventPayload
    {
        public uint QuestHashID;
        public ushort EventType;
        public ushort Reserved;
    }

    public interface IQuestEventListener
    {
        void OnQuestEvent(in QuestEventPayload payload);
    }

    public static class QuestEvents
    {
        private const int PendingEventCapacity = 16;

        // COLD ALLOC: RegistryBucket<IQuestEventListener>[16] - quest event listener registry drained on dispatcher LateUpdate - owner: QuestEvents
        private static readonly RegistryBucket<IQuestEventListener> _listeners = new RegistryBucket<IQuestEventListener>(16);
        private static NativeQueue<QuestEventPayload> _pendingEvents;
        private static NativeQueue<QuestEventPayload> _nextFrameEvents;
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
                NativeMemorySentinel.UnregisterNativeQueue(nameof(QuestEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(QuestEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
        }

        public static void Register(IQuestEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        public static void Unregister(IQuestEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        public static void FlushPending()
        {
            QuestGraphEvaluator.FlushPendingSignals();

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

                if (!_pendingEvents.TryDequeue(out QuestEventPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                IQuestEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IQuestEventListener listener = rawArray[i];
                        if (listener != null)
                            listener.OnQuestEvent(in payload);
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

        public static void RaiseActivated(uint questHash)
        {
            Enqueue(QuestEventType.Activated, questHash);
        }

        public static void RaiseCompleted(uint questHash)
        {
            Enqueue(QuestEventType.Completed, questHash);
        }

        public static void RaiseFailed(uint questHash)
        {
            Enqueue(QuestEventType.Failed, questHash);
        }

        public static void RaiseRevertRequested(in QuestRevertRequest request)
        {
            Enqueue(QuestEventType.RevertRequested, request.QuestHash);
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<QuestEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<QuestEventPayload>[16] - deferred quest event lane flushed by SystemDispatcher LateUpdate - owner: QuestEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(QuestEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<QuestEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<QuestEventPayload>[16] - next-frame quest event lane prevents same-frame reentrant dispatch - owner: QuestEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(QuestEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
            }
        }

        private static void Enqueue(QuestEventType type, uint questHash)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

            QuestEventPayload payload = new QuestEventPayload
            {
                QuestHashID = questHash,
                EventType = (ushort)type,
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
            ref NativeQueue<QuestEventPayload> queue,
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

            NativeQueue<QuestEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }
}
