using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using UnityEngine;

namespace Hecton8.Bootstrap
{
    /// <summary>
    /// Bootstrap event discriminator for <see cref="BootstrapEventPayload"/>.
    /// </summary>
    public enum BootstrapEventType : ushort
    {
        Complete = 1
    }

    /// <summary>
    /// Deferred unmanaged bootstrap event payload flushed by <see cref="SystemDispatcher"/>.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct BootstrapEventPayload : ISignal
    {
        public const int ExpectedCapacity = 4;
        public const int MaxFrameSignals = 4;
        public const int LowTierFrameSignals = 4;
        public const uint LaneHash = 0x42545650u; // BTVP

        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public ushort EventType;
        [FieldOffset(6)] public ushort StatusBits;
        [FieldOffset(8)] private ulong _pad0;

        public static bool IsCompleteEvent(in BootstrapEventPayload payload)
        {
            return payload.EventType == (ushort)BootstrapEventType.Complete;
        }
    }

    /// <summary>
    /// Listener contract for deferred bootstrap events.
    /// </summary>
    public interface IBootstrapEventListener
    {
        /// <summary>
        /// Called during the dispatcher late-frame event flush.
        /// </summary>
        /// <param name="payload">Unmanaged bootstrap payload.</param>
        void OnBootstrapEvent(in BootstrapEventPayload payload);
    }

    /// <summary>
    /// SignalBus-backed bootstrap event lane. Replaces legacy direct static bootstrap callbacks.
    /// </summary>
    public static class BootstrapEvents
    {
        private const int ListenerCapacity = 16;
        private const int PendingEventCapacity = 4;
        private const uint BootstrapListenerOverflowWarningHash = 0x4254564Cu; // BTVL
        private const uint BootstrapListenerContextHash = 0x42545652u; // BTVR

        private struct ListenerSlot
        {
            public IBootstrapEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct BootstrapListenerRegistry
        {
            private readonly ListenerSlot[] _slots;
            private int _count;

            public BootstrapListenerRegistry(int capacity)
            {
                _slots = new ListenerSlot[capacity]; // COLD ALLOC: ListenerSlot[16] - fixed bootstrap listener slots drained by SystemDispatcher - owner: BootstrapEvents
                _count = 0;
            }

            public int Count => _count;

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                    _slots[i].Clear();

                _count = 0;
            }

            public bool Contains(IBootstrapEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(IBootstrapEventListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public void TryUnregister(IBootstrapEventListener listener)
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

            public IBootstrapEventListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        private static BootstrapListenerRegistry _listeners = new BootstrapListenerRegistry(ListenerCapacity);
        // COLD ALLOC: ListenerSlot[16] - listener additions deferred while dispatching bootstrap events - owner: BootstrapEvents
        private static readonly ListenerSlot[] _deferredRegisterListeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: ListenerSlot[16] - listener removals deferred while dispatching bootstrap events - owner: BootstrapEvents
        private static readonly ListenerSlot[] _deferredUnregisterListeners = new ListenerSlot[ListenerCapacity];
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedListenerRegistrationCount;
        private static int _droppedBootstrapEventCount;
        private static int _lastListenerOverflowTelemetryFrame = -1;
        private static bool _isDispatching;

        /// <summary>
        /// Pending payload count in the bootstrap event lane.
        /// </summary>
        public static int PendingCount => SignalBus<BootstrapEventPayload>.SnapshotCount;

        public static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;

        public static int DroppedBootstrapEventCount => _droppedBootstrapEventCount;

        public static int ListenerExceptionCount => 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticState()
        {
            _listeners.Clear();
            Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterCount);
            Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterCount);
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedListenerRegistrationCount = 0;
            _droppedBootstrapEventCount = 0;
            _lastListenerOverflowTelemetryFrame = -1;
            _isDispatching = false;
            ConfigureSignalLane();
        }

        /// <summary>
        /// Registers a bootstrap event listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Register(IBootstrapEventListener listener)
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

        /// <summary>
        /// Unregisters a bootstrap event listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Unregister(IBootstrapEventListener listener)
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

        public static bool TryNotifyBootstrapComplete()
        {
            EnsureInitialized();
            BootstrapEventPayload payload = new BootstrapEventPayload
            {
                Frame = SystemDispatcher.CurrentFrameId,
                EventType = (ushort)BootstrapEventType.Complete,
                StatusBits = 0
            };

            return SignalBus<BootstrapEventPayload>.TryPushTracked(in payload, ref _droppedBootstrapEventCount);
        }

        /// <summary>
        /// Flushes pending bootstrap events under the dispatcher late-frame budget.
        /// </summary>
        public static void FlushPending()
        {
            ReadOnlySpan<BootstrapEventPayload> events = SignalBus<BootstrapEventPayload>.GetFrameSnapshot();
            int eventCount = events.Length;
            if (eventCount <= 0)
                return;

            if (_listeners.Count <= 0)
                return;

            int scanBudget = eventCount;
            for (int eventIndex = 0; eventIndex < eventCount && scanBudget-- > 0; eventIndex++)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                BootstrapEventPayload payload = events[eventIndex];
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IBootstrapEventListener listener = _listeners.GetAt(i);
                        if (listener == null || IsDeferredUnregisterPending(listener))
                            continue;

                        listener.OnBootstrapEvent(in payload);
                    }
                }
                finally
                {
                    _isDispatching = false;
                    ApplyDeferredListenerMutations();
                }
            }
        }

        private static void EnsureInitialized()
        {
            ConfigureSignalLane();
            SignalBus<BootstrapEventPayload>.EnsureInitialized();
        }

        private static void ConfigureSignalLane()
        {
            SignalBus<BootstrapEventPayload>.Configure(
                BootstrapEventPayload.ExpectedCapacity,
                BootstrapEventPayload.MaxFrameSignals,
                BootstrapEventPayload.LowTierFrameSignals,
                BootstrapEventPayload.LaneHash);
        }

        private static void QueueDeferredRegister(IBootstrapEventListener listener)
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

        private static void QueueDeferredUnregister(IBootstrapEventListener listener)
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

        private static bool CancelDeferredRegister(IBootstrapEventListener listener)
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

        private static void CancelDeferredUnregister(IBootstrapEventListener listener)
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

        private static bool IsDeferredRegisterPending(IBootstrapEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(IBootstrapEventListener listener)
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
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                IBootstrapEventListener listener = _deferredUnregisterListeners[i].Listener;
                _deferredUnregisterListeners[i].Clear();
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                IBootstrapEventListener listener = _deferredRegisterListeners[i].Listener;
                _deferredRegisterListeners[i].Clear();
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;
        }

        private static void RegisterImmediate(IBootstrapEventListener listener)
        {
            if (_listeners.Contains(listener))
                return;

            if (!_listeners.TryRegister(listener))
                ReportListenerRegistrationOverflow();
        }

        private static void ReportListenerRegistrationOverflow()
        {
            _droppedListenerRegistrationCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastListenerOverflowTelemetryFrame == frame)
                return;

            _lastListenerOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                BootstrapListenerOverflowWarningHash,
                BootstrapListenerContextHash,
                Mathf.Max(1, _droppedListenerRegistrationCount));
        }
    }
}
