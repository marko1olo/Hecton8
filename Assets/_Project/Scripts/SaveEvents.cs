using System;
using System.Diagnostics;
using Hecton8.Core;
using Unity.Collections;
using Unity.Mathematics;

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
        EmergencyBackupRestoreRequested = 6,
        MappedWriteStarted = 7
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
        private const int ListenerCapacity = 16;
        public const int ManualSlotCount = 3;
        private const string Slot0Name = "slot_0";
        private const string Slot1Name = "slot_1";
        private const string Slot2Name = "slot_2";
        private const string UnknownSlotName = "slot_unknown";
        private const string Slot0Number = "1";
        private const string Slot1Number = "2";
        private const string Slot2Number = "3";
        private const string UnknownSlotNumber = "?";
        private const uint SaveEventOverflowWarningHash = 0x5345564Fu; // SEVO
        private const uint SaveEventQueueContextHash = 0x53455651u; // SEVQ
        private const uint SaveEventListenerOverflowWarningHash = 0x5345564Cu; // SEVL
        private const uint SaveEventListenerContextHash = 0x53455652u; // SEVR
        private const uint SaveEventListenerExceptionWarningHash = 0x53455645u; // SEVE
        private const uint SaveEventListenerExceptionContextHash = 0x53455658u; // SEVX
        private const uint SaveEventPayloadTruncatedWarningHash = 0x53455654u; // SEVT
        private const uint SaveEventSlotTruncatedContextHash = 0x5345534Cu; // SESL
        private const uint SaveEventMessageTruncatedContextHash = 0x53454D53u; // SEMS

        // COLD ALLOC: RegistryBucket<ISaveEventListener>[16] — save event listener registry drained on dispatcher LateUpdate — owner: SaveEvents
        private static readonly RegistryBucket<ISaveEventListener> _listeners = new RegistryBucket<ISaveEventListener>(ListenerCapacity);
        // COLD ALLOC: ISaveEventListener[16] — listener additions deferred while dispatching save events — owner: SaveEvents
        private static readonly ISaveEventListener[] _deferredRegisterListeners = new ISaveEventListener[ListenerCapacity];
        // COLD ALLOC: ISaveEventListener[16] — listener removals deferred while dispatching save events — owner: SaveEvents
        private static readonly ISaveEventListener[] _deferredUnregisterListeners = new ISaveEventListener[ListenerCapacity];
        private static NativeQueue<SaveEventPayload> _pendingEvents;
        private static NativeQueue<SaveEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedEventCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _truncatedPayloadCount;
        private static int _lastOverflowTelemetryFrame = -1;
        private static int _lastListenerOverflowTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static int _lastPayloadTruncationTelemetryFrame = -1;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        public static int DroppedEventCount => _droppedEventCount;

        public static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;

        public static int ListenerExceptionCount => _listenerExceptionCount;

        public static int TruncatedPayloadCount => _truncatedPayloadCount;

        public static string ResolveManualSlotName(int slotIndex)
        {
            switch (slotIndex)
            {
                case 0:
                    return Slot0Name;
                case 1:
                    return Slot1Name;
                case 2:
                    return Slot2Name;
                default:
                    return UnknownSlotName;
            }
        }

        public static bool IsKnownManualSlotName(string slotName)
        {
            return ResolveKnownSlotIndex(slotName) >= 0;
        }

        public static string ResolveSlotName(in FixedString64Bytes slotName)
        {
            if (slotName.Length <= 0)
                return string.Empty;

            return TryResolveKnownSlotName(in slotName, out string resolvedSlotName)
                ? resolvedSlotName
                : UnknownSlotName;
        }

        public static bool TryResolveKnownSlotName(in FixedString64Bytes slotName, out string resolvedSlotName)
        {
            resolvedSlotName = string.Empty;
            if (slotName.Length <= 0)
                return false;

            if (IsFixedStringEqual(in slotName, Slot0Name))
            {
                resolvedSlotName = Slot0Name;
                return true;
            }

            if (IsFixedStringEqual(in slotName, Slot1Name))
            {
                resolvedSlotName = Slot1Name;
                return true;
            }

            if (IsFixedStringEqual(in slotName, Slot2Name))
            {
                resolvedSlotName = Slot2Name;
                return true;
            }

            return false;
        }

        public static string ResolveSlotNumber(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
                return string.Empty;

            if (string.Equals(slotName, Slot0Name, System.StringComparison.Ordinal))
                return Slot0Number;

            if (string.Equals(slotName, Slot1Name, System.StringComparison.Ordinal))
                return Slot1Number;

            if (string.Equals(slotName, Slot2Name, System.StringComparison.Ordinal))
                return Slot2Number;

            return UnknownSlotNumber;
        }

        public static int ResolveKnownSlotIndex(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
                return -1;

            if (string.Equals(slotName, Slot0Name, System.StringComparison.Ordinal))
                return 0;

            if (string.Equals(slotName, Slot1Name, System.StringComparison.Ordinal))
                return 1;

            if (string.Equals(slotName, Slot2Name, System.StringComparison.Ordinal))
                return 2;

            return -1;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticState()
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
            Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterCount);
            _deferredRegisterCount = 0;
            Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterCount);
            _deferredUnregisterCount = 0;
            _droppedEventCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _truncatedPayloadCount = 0;
            _lastOverflowTelemetryFrame = -1;
            _lastListenerOverflowTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _lastPayloadTruncationTelemetryFrame = -1;
            _isDispatching = false;
        }

        public static void Register(ISaveEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (_isDispatching)
            {
                QueueDeferredRegister(listener);
                return;
            }

            RegisterImmediate(listener);
        }

        public static void Unregister(ISaveEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredUnregister(listener);
                return;
            }

            _listeners.TryUnregister(listener);
        }

        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated || _listeners.Count <= 0)
            {
                // No callbacks will run here; silent stale-event cleanup must not steal shared LateFrame dispatch budget.
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

        public static void RaiseMappedWriteStarted(string slot)
        {
            Enqueue(SaveEventType.MappedWriteStarted, slot, default);
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

        private static void DispatchToListener(ISaveEventListener listener, in SaveEventPayload payload)
        {
            try
            {
                listener.OnSaveEvent(in payload);
            }
            catch (Exception exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        private static void LogListenerDispatchException(Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.LogException(exception);
#endif
        }

        private static void QueueDeferredUnregister(ISaveEventListener listener)
        {
            if (CancelDeferredRegister(listener))
                return;

            if (!_listeners.Contains(listener))
                return;

            if (IsDeferredUnregisterPending(listener))
                return;

            if (_deferredUnregisterCount >= _deferredUnregisterListeners.Length)
            {
                ReportListenerRegistrationOverflow();
                return;
            }

            _deferredUnregisterListeners[_deferredUnregisterCount++] = listener;
        }

        private static void QueueDeferredRegister(ISaveEventListener listener)
        {
            if (_listeners.Contains(listener))
            {
                CancelDeferredUnregister(listener);
                return;
            }

            if (IsDeferredRegisterPending(listener))
                return;

            if (_deferredRegisterCount >= _deferredRegisterListeners.Length)
            {
                ReportListenerRegistrationOverflow();
                return;
            }

            _deferredRegisterListeners[_deferredRegisterCount++] = listener;
        }

        private static bool CancelDeferredRegister(ISaveEventListener listener)
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

        private static void CancelDeferredUnregister(ISaveEventListener listener)
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

        private static bool IsDeferredRegisterPending(ISaveEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i], listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(ISaveEventListener listener)
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
            ApplyDeferredUnregisters();
            ApplyDeferredRegisters();
        }

        private static void ApplyDeferredRegisters()
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                ISaveEventListener listener = _deferredRegisterListeners[i];
                _deferredRegisterListeners[i] = null;
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;
        }

        private static void ApplyDeferredUnregisters()
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                ISaveEventListener listener = _deferredUnregisterListeners[i];
                _deferredUnregisterListeners[i] = null;
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterCount = 0;
        }

        private static void RegisterImmediate(ISaveEventListener listener)
        {
            if (_listeners.Contains(listener))
                return;

            if (!_listeners.TryRegister(listener))
                ReportListenerRegistrationOverflow();
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<SaveEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SaveEventPayload>[16] — deferred save event lane flushed by SystemDispatcher LateUpdate — owner: SaveEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(SaveEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<SaveEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SaveEventPayload>[16] — next-frame save event lane prevents same-frame reentrant dispatch — owner: SaveEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(SaveEvents),
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

        private static void Enqueue(SaveEventType type, string slot, string message)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportOverflow(type);
                return;
            }

            SaveEventPayload payload = new SaveEventPayload
            {
                Type = type,
                TimestampTicks = unchecked((ulong)Stopwatch.GetTimestamp()),
                SlotName = CopySlotName(slot),
                Message = CopyMessage(message)
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

        private static void ReportOverflow(SaveEventType type)
        {
            _droppedEventCount++;
            int frame = UnityEngine.Time.frameCount;
            if (_lastOverflowTelemetryFrame == frame)
                return;

            _lastOverflowTelemetryFrame = frame;
            uint contextHash = SaveEventQueueContextHash ^ ((uint)type << 24);
            GlobalTelemetryBus.PublishPerformanceWarning(
                SaveEventOverflowWarningHash,
                contextHash,
                math.max(1, _droppedEventCount));
        }

        private static void ReportListenerRegistrationOverflow()
        {
            _droppedListenerRegistrationCount++;
            int frame = UnityEngine.Time.frameCount;
            if (_lastListenerOverflowTelemetryFrame == frame)
                return;

            _lastListenerOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                SaveEventListenerOverflowWarningHash,
                SaveEventListenerContextHash,
                math.max(1, _droppedListenerRegistrationCount));
        }

        private static void ReportListenerDispatchException()
        {
            _listenerExceptionCount++;
            int frame = UnityEngine.Time.frameCount;
            if (_lastListenerExceptionTelemetryFrame == frame)
                return;

            _lastListenerExceptionTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                SaveEventListenerExceptionWarningHash,
                SaveEventListenerExceptionContextHash,
                math.max(1, _listenerExceptionCount));
        }

        private static void ReportPayloadTruncated(uint contextHash)
        {
            _truncatedPayloadCount++;
            int frame = UnityEngine.Time.frameCount;
            if (_lastPayloadTruncationTelemetryFrame == frame)
                return;

            _lastPayloadTruncationTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                SaveEventPayloadTruncatedWarningHash,
                contextHash,
                math.max(1, _truncatedPayloadCount));
        }

        private static FixedString64Bytes CopySlotName(string slot)
        {
            FixedString64Bytes value = default;
            if (string.IsNullOrEmpty(slot))
                return value;

            if (value.CopyFromTruncated(slot) != CopyError.None)
                ReportPayloadTruncated(SaveEventSlotTruncatedContextHash);

            return value;
        }

        private static FixedString128Bytes CopyMessage(string message)
        {
            FixedString128Bytes value = default;
            if (string.IsNullOrEmpty(message))
                return value;

            if (value.CopyFromTruncated(message) != CopyError.None)
                ReportPayloadTruncated(SaveEventMessageTruncatedContextHash);

            return value;
        }

        private static bool IsFixedStringEqual(in FixedString64Bytes value, string expected)
        {
            if (string.IsNullOrEmpty(expected) || value.Length != expected.Length)
                return false;

            for (int i = 0; i < expected.Length; i++)
            {
                if (value[i] != (byte)expected[i])
                    return false;
            }

            return true;
        }

        private static void DrainWithoutDispatch()
        {
            if (!_pendingEvents.IsCreated)
                return;

            DrainQueueWithoutBudget(ref _pendingEvents, ref _pendingEventCount);

            if (_pendingEvents.IsEmpty())
                PromoteNextFrameEventsIfFrontEmpty();

            if (_pendingEventCount > 0)
                DrainQueueWithoutBudget(ref _pendingEvents, ref _pendingEventCount);

            if (_nextFrameEvents.IsCreated)
                DrainQueueWithoutBudget(ref _nextFrameEvents, ref _nextFrameEventCount);
        }

        private static void DrainQueueWithoutBudget(
            ref NativeQueue<SaveEventPayload> queue,
            ref int pendingCount)
        {
            int scanBudget = pendingCount > 0 ? pendingCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !queue.IsEmpty())
            {
                if (!queue.TryDequeue(out _))
                    break;

                if (pendingCount > 0)
                    pendingCount--;
            }

            if (queue.IsEmpty())
                pendingCount = 0;
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
