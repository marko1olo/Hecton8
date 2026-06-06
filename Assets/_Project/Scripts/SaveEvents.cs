using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Hecton.Localization;
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

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct SaveEventPayload
    {
        [FieldOffset(0)] public ulong TimestampTicks;
        [FieldOffset(8)] public uint SlotHash;
        [FieldOffset(12)] public uint MessageHash;
        [FieldOffset(16)] public int MessageSlot;
        [FieldOffset(20)] public SaveEventType Type;
    }

    public interface ISaveEventListener
    {
        void OnSaveEvent(in SaveEventPayload payload);
    }

    public static class SaveEvents
    {
        private const int PendingEventCapacity = 16;
        private const int ListenerCapacity = 16;
        private const int MessageSlotCapacity = 16;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
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
        private static readonly uint Slot0Hash = ComputeHash(Slot0Name);
        private static readonly uint Slot1Hash = ComputeHash(Slot1Name);
        private static readonly uint Slot2Hash = ComputeHash(Slot2Name);
        private static readonly uint UnknownSlotHash = ComputeHash(UnknownSlotName);

        private struct MessageSlot
        {
            public uint MessageHash;
            public string Message;
            public byte IsValid;

            public void Clear()
            {
                MessageHash = 0u;
                Message = null;
                IsValid = 0;
            }
        }

        private struct ListenerSlot
        {
            public ISaveEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct SaveListenerRegistry
        {
            private readonly ListenerSlot[] _slots;
            private int _count;

            public SaveListenerRegistry(int capacity)
            {
                _slots = new ListenerSlot[capacity]; // COLD ALLOC: ListenerSlot[16] — fixed save listener slots drained on dispatcher LateUpdate — owner: SaveEvents
                _count = 0;
            }

            public int Count => _count;

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                    _slots[i].Clear();

                _count = 0;
            }

            public bool Contains(ISaveEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(ISaveEventListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public void TryUnregister(ISaveEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (!ReferenceEquals(_slots[i].Listener, listener))
                        continue;

                    _count--;
                    _slots[i] = _slots[_count];
                    _slots[_count].Clear();
                    return;
                }
            }

            public ISaveEventListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        private static SaveListenerRegistry _listeners = new SaveListenerRegistry(ListenerCapacity);
        // COLD ALLOC: ListenerSlot[16] — listener additions deferred while dispatching save events — owner: SaveEvents
        private static readonly ListenerSlot[] _deferredRegisterListeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: ListenerSlot[16] — listener removals deferred while dispatching save events — owner: SaveEvents
        private static readonly ListenerSlot[] _deferredUnregisterListeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: MessageSlot[16] - fixed save UI message sidecar; queued DTO carries only hashes/slot index - owner: SaveEvents
        private static readonly MessageSlot[] _messageSlots = new MessageSlot[MessageSlotCapacity];
        private static NativeQueue<SaveEventPayload> _pendingEvents;
        private static NativeQueue<SaveEventPayload> _nextFrameEvents;
        private static int _messageSlotWriteIndex;
        private static int _messageSlotPendingCount;
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

        public static uint ResolveManualSlotHash(int slotIndex)
        {
            switch (slotIndex)
            {
                case 0:
                    return Slot0Hash;
                case 1:
                    return Slot1Hash;
                case 2:
                    return Slot2Hash;
                default:
                    return UnknownSlotHash;
            }
        }

        public static bool IsKnownManualSlotName(string slotName)
        {
            return ResolveKnownSlotIndex(slotName) >= 0;
        }

        public static string ResolveSlotName(uint slotHash)
        {
            if (slotHash == 0u)
                return string.Empty;

            return TryResolveKnownSlotName(slotHash, out string resolvedSlotName)
                ? resolvedSlotName
                : UnknownSlotName;
        }

        public static bool TryResolveKnownSlotName(uint slotHash, out string resolvedSlotName)
        {
            if (slotHash == Slot0Hash)
            {
                resolvedSlotName = Slot0Name;
                return true;
            }

            if (slotHash == Slot1Hash)
            {
                resolvedSlotName = Slot1Name;
                return true;
            }

            if (slotHash == Slot2Hash)
            {
                resolvedSlotName = Slot2Name;
                return true;
            }

            resolvedSlotName = string.Empty;
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

        public static string ResolveSlotNumber(uint slotHash)
        {
            if (slotHash == Slot0Hash)
                return Slot0Number;
            if (slotHash == Slot1Hash)
                return Slot1Number;
            if (slotHash == Slot2Hash)
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

        public static uint ComputeSlotHash(string slotName)
        {
            return ComputeHash(slotName);
        }

        public static uint ComputeMessageHash(string message)
        {
            return ComputeHash(message);
        }

        public static string ResolveMessage(in SaveEventPayload payload)
        {
            if (!TryResolveMessage(in payload, out string message))
                return string.Empty;

            return message;
        }

        public static bool TryResolveMessage(in SaveEventPayload payload, out string message)
        {
            message = string.Empty;
            int slot = payload.MessageSlot;
            if ((uint)slot >= MessageSlotCapacity)
                return false;

            ref MessageSlot messageSlot = ref _messageSlots[slot];
            if (messageSlot.IsValid == 0 ||
                messageSlot.MessageHash != payload.MessageHash ||
                string.IsNullOrEmpty(messageSlot.Message))
            {
                return false;
            }

            message = messageSlot.Message;
            return true;
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
            ClearMessageSlots();
            _messageSlotWriteIndex = 0;
            _messageSlotPendingCount = 0;
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

        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void PrewarmRuntimeQueues()
        {
            EnsureInitialized();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorTeardownHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ResetStaticState;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ResetStaticState;
            UnityEditor.EditorApplication.quitting -= ResetStaticState;
            UnityEditor.EditorApplication.quitting += ResetStaticState;
        }
#endif

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
                {
                    _pendingEventCount = 0;
                    break;
                }

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        ISaveEventListener listener = _listeners.GetAt(i);
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

                ReleaseMessageSlot(payload.MessageSlot);
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        public static bool TryRaiseSaveStarted(uint slotHash)
        {
            return TryEnqueue(SaveEventType.SaveStarted, slotHash, 0u, null);
        }

        [Obsolete("Save event payloads must use precomputed slot hashes; use TryRaiseSaveStarted(uint).", true)]
        public static void RaiseSaveStarted(string slot)
        {
            TryRaiseSaveStarted(ComputeSlotHash(slot));
        }

        public static bool TryRaiseSaveCompleted(uint slotHash)
        {
            return TryEnqueue(SaveEventType.SaveCompleted, slotHash, 0u, null);
        }

        [Obsolete("Save event payloads must use precomputed slot hashes; use TryRaiseSaveCompleted(uint).", true)]
        public static void RaiseSaveCompleted(string slot)
        {
            TryRaiseSaveCompleted(ComputeSlotHash(slot));
        }

        public static bool TryRaiseSaveFailed(uint slotHash, uint errorHash, string errorMessage)
        {
            return TryEnqueue(SaveEventType.SaveFailed, slotHash, errorHash, errorMessage);
        }

        [Obsolete("Save event payloads must use precomputed hashes; use TryRaiseSaveFailed(uint,uint,string).", true)]
        public static void RaiseSaveFailed(string slot, string error)
        {
            TryRaiseSaveFailed(ComputeSlotHash(slot), ComputeHash(error), error);
        }

        public static bool TryRaiseMappedWriteStarted(uint slotHash)
        {
            return TryEnqueue(SaveEventType.MappedWriteStarted, slotHash, 0u, null);
        }

        [Obsolete("Save event payloads must use precomputed slot hashes; use TryRaiseMappedWriteStarted(uint).", true)]
        public static void RaiseMappedWriteStarted(string slot)
        {
            TryRaiseMappedWriteStarted(ComputeSlotHash(slot));
        }

        public static bool TryRaiseLoadStarted(uint slotHash)
        {
            return TryEnqueue(SaveEventType.LoadStarted, slotHash, 0u, null);
        }

        [Obsolete("Save event payloads must use precomputed slot hashes; use TryRaiseLoadStarted(uint).", true)]
        public static void RaiseLoadStarted(string slot)
        {
            TryRaiseLoadStarted(ComputeSlotHash(slot));
        }

        public static bool TryRaiseLoadCompleted(uint slotHash)
        {
            return TryEnqueue(SaveEventType.LoadCompleted, slotHash, 0u, null);
        }

        [Obsolete("Save event payloads must use precomputed slot hashes; use TryRaiseLoadCompleted(uint).", true)]
        public static void RaiseLoadCompleted(string slot)
        {
            TryRaiseLoadCompleted(ComputeSlotHash(slot));
        }

        public static bool TryRaiseLoadFailed(uint slotHash, uint errorHash, string errorMessage)
        {
            return TryEnqueue(SaveEventType.LoadFailed, slotHash, errorHash, errorMessage);
        }

        [Obsolete("Save event payloads must use precomputed hashes; use TryRaiseLoadFailed(uint,uint,string).", true)]
        public static void RaiseLoadFailed(string slot, string error)
        {
            TryRaiseLoadFailed(ComputeSlotHash(slot), ComputeHash(error), error);
        }

        public static bool TryRaiseEmergencyBackupRestoreRequested(uint slotHash)
        {
            return TryEnqueue(SaveEventType.EmergencyBackupRestoreRequested, slotHash, 0u, null);
        }

        [Obsolete("Save event payloads must use precomputed slot hashes; use TryRaiseEmergencyBackupRestoreRequested(uint).", true)]
        public static void RaiseEmergencyBackupRestoreRequested(string slot)
        {
            TryRaiseEmergencyBackupRestoreRequested(ComputeSlotHash(slot));
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
            Hecton8.Core.H8Debug.LogException(exception);
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

            _deferredUnregisterListeners[_deferredUnregisterCount++].Listener = listener;
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

            _deferredRegisterListeners[_deferredRegisterCount++].Listener = listener;
        }

        private static bool CancelDeferredRegister(ISaveEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (!ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    continue;

                _deferredRegisterCount--;
                _deferredRegisterListeners[i] = _deferredRegisterListeners[_deferredRegisterCount];
                _deferredRegisterListeners[_deferredRegisterCount].Clear();
                return true;
            }

            return false;
        }

        private static void CancelDeferredUnregister(ISaveEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (!ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    continue;

                _deferredUnregisterCount--;
                _deferredUnregisterListeners[i] = _deferredUnregisterListeners[_deferredUnregisterCount];
                _deferredUnregisterListeners[_deferredUnregisterCount].Clear();
                return;
            }
        }

        private static bool IsDeferredRegisterPending(ISaveEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(ISaveEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
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
                ISaveEventListener listener = _deferredRegisterListeners[i].Listener;
                _deferredRegisterListeners[i].Clear();
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;
        }

        private static void ApplyDeferredUnregisters()
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                ISaveEventListener listener = _deferredUnregisterListeners[i].Listener;
                _deferredUnregisterListeners[i].Clear();
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
                _pendingEvents = new NativeQueue<SaveEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<SaveEventPayload>[16] — deferred save event lane flushed by SystemDispatcher LateUpdate — owner: SaveEvents
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
                _nextFrameEvents = new NativeQueue<SaveEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<SaveEventPayload>[16] — next-frame save event lane prevents same-frame reentrant dispatch — owner: SaveEvents
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

        private static bool TryEnqueue(SaveEventType type, uint slotHash, uint messageHash, string message)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportOverflow(type);
                return false;
            }

            if (slotHash == 0u)
                slotHash = UnknownSlotHash;

            int messageSlot = -1;
            if (!string.IsNullOrEmpty(message))
            {
                if (messageHash == 0u)
                    messageHash = ComputeHash(message);

                if (!TryReserveMessageSlot(messageHash, message, out messageSlot))
                {
                    ReportPayloadTruncated(SaveEventMessageTruncatedContextHash);
                    return false;
                }
            }

            SaveEventPayload payload = new SaveEventPayload
            {
                Type = type,
                TimestampTicks = unchecked((ulong)Stopwatch.GetTimestamp()),
                SlotHash = slotHash,
                MessageHash = messageHash,
                MessageSlot = messageSlot
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

        private static void ReportOverflow(SaveEventType type)
        {
            _droppedEventCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
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
            int frame = SystemDispatcher.CurrentFrameIndex;
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
            int frame = SystemDispatcher.CurrentFrameIndex;
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
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastPayloadTruncationTelemetryFrame == frame)
                return;

            _lastPayloadTruncationTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                SaveEventPayloadTruncatedWarningHash,
                contextHash,
                math.max(1, _truncatedPayloadCount));
        }

        private static bool TryReserveMessageSlot(uint messageHash, string message, out int slot)
        {
            slot = -1;
            if (messageHash == 0u || string.IsNullOrEmpty(message))
                return true;

            if (_messageSlotPendingCount >= MessageSlotCapacity)
                return false;

            for (int probe = 0; probe < MessageSlotCapacity; probe++)
            {
                int candidate = _messageSlotWriteIndex;
                _messageSlotWriteIndex++;
                if (_messageSlotWriteIndex >= MessageSlotCapacity)
                    _messageSlotWriteIndex = 0;

                if (_messageSlots[candidate].IsValid != 0)
                    continue;

                _messageSlots[candidate].MessageHash = messageHash;
                _messageSlots[candidate].Message = message;
                _messageSlots[candidate].IsValid = 1;
                _messageSlotPendingCount++;
                slot = candidate;
                return true;
            }

            return false;
        }

        private static void ReleaseMessageSlot(int slot)
        {
            if ((uint)slot >= MessageSlotCapacity)
                return;

            if (_messageSlots[slot].IsValid == 0)
                return;

            _messageSlots[slot].Clear();
            if (_messageSlotPendingCount > 0)
                _messageSlotPendingCount--;
        }

        private static void ClearMessageSlots()
        {
            for (int i = 0; i < MessageSlotCapacity; i++)
                _messageSlots[i].Clear();
        }

        private static uint ComputeHash(string value)
        {
            return string.IsNullOrEmpty(value)
                ? 0u
                : unchecked((uint)LocHash.Compute(value));
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
                if (!queue.TryDequeue(out SaveEventPayload payload))
                {
                    pendingCount = 0;
                    break;
                }

                if (pendingCount > 0)
                    pendingCount--;

                ReleaseMessageSlot(payload.MessageSlot);
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
