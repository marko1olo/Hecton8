using System;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Unity.Collections;
using UnityEngine;

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
        private const int ListenerCapacity = 16;
        private const int PendingEventCapacity = 16;
        private static readonly uint _QueueOverflowWarningHash = unchecked((uint)LocHash.Compute("QuestEvents.QueueOverflow"));
        private static readonly uint _DuplicateListenerWarningHash = unchecked((uint)LocHash.Compute("QuestEvents.DuplicateListener"));
        private static readonly uint _ListenerRejectedWarningHash = unchecked((uint)LocHash.Compute("QuestEvents.ListenerRejected"));
        private static readonly uint _ListenerExceptionWarningHash = unchecked((uint)LocHash.Compute("QuestEvents.ListenerException"));
        private static readonly uint _UnregisterMissWarningHash = unchecked((uint)LocHash.Compute("QuestEvents.UnregisterMiss"));
        private static readonly uint _QueueContextHash = unchecked((uint)LocHash.Compute("QuestEvents.PendingQueue"));
        private static readonly uint _ListenerContextHash = unchecked((uint)LocHash.Compute("QuestEvents.Listeners"));

        // COLD ALLOC: RegistryBucket<IQuestEventListener>[16] — quest event listener registry drained on dispatcher LateUpdate — owner: QuestEvents
        private static readonly RegistryBucket<IQuestEventListener> _listeners = new RegistryBucket<IQuestEventListener>(ListenerCapacity);
        // COLD ALLOC: IQuestEventListener[16] — listener additions deferred while dispatching quest events — owner: QuestEvents
        private static readonly IQuestEventListener[] _deferredRegisterListeners = new IQuestEventListener[ListenerCapacity];
        // COLD ALLOC: IQuestEventListener[16] — listener removals deferred while dispatching quest events — owner: QuestEvents
        private static readonly IQuestEventListener[] _deferredUnregisterListeners = new IQuestEventListener[ListenerCapacity];
        private static NativeQueue<QuestEventPayload> _pendingEvents;
        private static NativeQueue<QuestEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedEventCount;
        private static int _duplicateRegistrationCount;
        private static int _listenerRejectCount;
        private static int _listenerExceptionCount;
        private static int _unregisterMissCount;
        private static int _lastOverflowTelemetryFrame = -1;
        private static int _lastDuplicateTelemetryFrame = -1;
        private static int _lastListenerRejectedTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static int _lastUnregisterMissTelemetryFrame = -1;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;
        public static int DroppedEventCount => _droppedEventCount;
        public static int DuplicateRegistrationCount => _duplicateRegistrationCount;
        public static int ListenerRejectCount => _listenerRejectCount;
        public static int ListenerExceptionCount => _listenerExceptionCount;
        public static int UnregisterMissCount => _unregisterMissCount;

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
            Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterCount);
            Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterCount);
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedEventCount = 0;
            _duplicateRegistrationCount = 0;
            _listenerRejectCount = 0;
            _listenerExceptionCount = 0;
            _unregisterMissCount = 0;
            _lastOverflowTelemetryFrame = -1;
            _lastDuplicateTelemetryFrame = -1;
            _lastListenerRejectedTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _lastUnregisterMissTelemetryFrame = -1;
            _isDispatching = false;
        }

        public static void Register(IQuestEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredRegister(listener);
                return;
            }

            RegisterImmediate(listener);
        }

        public static void Unregister(IQuestEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredUnregister(listener);
                return;
            }

            if (!_listeners.TryUnregister(listener))
                ReportUnregisterMiss();
        }

        internal static bool IsRegistered(IQuestEventListener listener)
        {
            return listener != null &&
                (_listeners.Contains(listener) || IsDeferredRegisterPending(listener)) &&
                !IsDeferredUnregisterPending(listener);
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
                        if (listener == null || IsDeferredUnregisterPending(listener))
                            continue;

                        DispatchToListener(listener, in payload);
                    }
                }
                finally
                {
                    _isDispatching = false;
                    ApplyDeferredListenerMutations();
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
                _pendingEvents = new NativeQueue<QuestEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<QuestEventPayload>[16] — deferred quest event lane flushed by SystemDispatcher LateUpdate — owner: QuestEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(QuestEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<QuestEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<QuestEventPayload>[16] — next-frame quest event lane prevents same-frame reentrant dispatch — owner: QuestEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(QuestEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
            }
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }

        private static bool Enqueue(QuestEventType type, uint questHash)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportQueueOverflow((ushort)type);
                return false;
            }

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
                return true;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
            return true;
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

        private static void RegisterImmediate(IQuestEventListener listener)
        {
            if (_listeners.Contains(listener))
            {
                ReportDuplicateListenerRegistration();
                return;
            }

            if (!_listeners.TryRegister(listener))
                ReportListenerRejected();
        }

        private static void DispatchToListener(IQuestEventListener listener, in QuestEventPayload payload)
        {
            try
            {
                listener.OnQuestEvent(in payload);
            }
            catch (Exception exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogListenerDispatchException(Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogException(exception);
#endif
        }

        private static void QueueDeferredRegister(IQuestEventListener listener)
        {
            if (_listeners.Contains(listener))
            {
                CancelDeferredUnregister(listener);
                return;
            }

            if (IsDeferredRegisterPending(listener))
                return;

            if (_deferredRegisterCount >= ListenerCapacity)
            {
                ReportListenerRejected();
                return;
            }

            _deferredRegisterListeners[_deferredRegisterCount++] = listener;
        }

        private static void QueueDeferredUnregister(IQuestEventListener listener)
        {
            if (CancelDeferredRegister(listener))
                return;

            if (!_listeners.Contains(listener))
                return;

            if (IsDeferredUnregisterPending(listener))
                return;

            if (_deferredUnregisterCount >= ListenerCapacity)
            {
                ReportListenerRejected();
                return;
            }

            _deferredUnregisterListeners[_deferredUnregisterCount++] = listener;
        }

        private static bool CancelDeferredRegister(IQuestEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (!ReferenceEquals(_deferredRegisterListeners[i], listener))
                    continue;

                _deferredRegisterCount--;
                _deferredRegisterListeners[i] = _deferredRegisterListeners[_deferredRegisterCount];
                _deferredRegisterListeners[_deferredRegisterCount] = null;
                return true;
            }

            return false;
        }

        private static void CancelDeferredUnregister(IQuestEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (!ReferenceEquals(_deferredUnregisterListeners[i], listener))
                    continue;

                _deferredUnregisterCount--;
                _deferredUnregisterListeners[i] = _deferredUnregisterListeners[_deferredUnregisterCount];
                _deferredUnregisterListeners[_deferredUnregisterCount] = null;
                return;
            }
        }

        private static bool IsDeferredRegisterPending(IQuestEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i], listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(IQuestEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (ReferenceEquals(_deferredUnregisterListeners[i], listener))
                    return true;
            }

            return false;
        }

        private static void ApplyDeferredListenerMutations()
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                IQuestEventListener listener = _deferredUnregisterListeners[i];
                _deferredUnregisterListeners[i] = null;
                if (listener != null && !_listeners.TryUnregister(listener))
                    ReportUnregisterMiss();
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                IQuestEventListener listener = _deferredRegisterListeners[i];
                _deferredRegisterListeners[i] = null;
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;
        }

        private static void ReportQueueOverflow(ushort eventType)
        {
            _droppedEventCount++;
            int frame = Time.frameCount;
            if (_lastOverflowTelemetryFrame == frame)
                return;

            _lastOverflowTelemetryFrame = frame;
            uint contextHash = _QueueContextHash ^ ((uint)eventType << 24);
            GlobalTelemetryBus.PublishPerformanceWarning(
                _QueueOverflowWarningHash,
                contextHash,
                _droppedEventCount);
        }

        private static void ReportDuplicateListenerRegistration()
        {
            _duplicateRegistrationCount++;
            int frame = Time.frameCount;
            if (_lastDuplicateTelemetryFrame == frame)
                return;

            _lastDuplicateTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _DuplicateListenerWarningHash,
                _ListenerContextHash,
                _duplicateRegistrationCount);
        }

        private static void ReportListenerRejected()
        {
            _listenerRejectCount++;
            int frame = Time.frameCount;
            if (_lastListenerRejectedTelemetryFrame == frame)
                return;

            _lastListenerRejectedTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _ListenerRejectedWarningHash,
                _ListenerContextHash,
                _listenerRejectCount);
        }

        private static void ReportListenerDispatchException()
        {
            _listenerExceptionCount++;
            int frame = Time.frameCount;
            if (_lastListenerExceptionTelemetryFrame == frame)
                return;

            _lastListenerExceptionTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _ListenerExceptionWarningHash,
                _ListenerContextHash,
                _listenerExceptionCount > 0 ? _listenerExceptionCount : 1);
        }

        private static void ReportUnregisterMiss()
        {
            _unregisterMissCount++;
            int frame = Time.frameCount;
            if (_lastUnregisterMissTelemetryFrame == frame)
                return;

            _lastUnregisterMissTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _UnregisterMissWarningHash,
                _ListenerContextHash,
                _unregisterMissCount);
        }
    }
}
