using System;
using System.Runtime.CompilerServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Unity.Collections;
using UnityEngine;
using ScalabilityChangedEvent = Hecton8.Core.Contracts.Signals.ScalabilityChangedEvent;

namespace Hecton8.Core
{
    internal static class ScalabilityTierRuntime
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static HectonQualityTier ToQualityTier(byte tier)
        {
            return ScalabilityTierProfiles.Normalize(tier) == ScalabilityTierProfiles.LowMx350
                ? HectonQualityTier.Mx350
                : HectonQualityTier.High;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static byte FromQualityTier(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.High || tier == HectonQualityTier.Ultra
                ? ScalabilityTierProfiles.HighRtx
                : ScalabilityTierProfiles.LowMx350;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static MathPrecisionLevel ToMathPrecisionLevel(byte tier)
        {
            return ScalabilityTierProfiles.Normalize(tier) == ScalabilityTierProfiles.LowMx350
                ? MathPrecisionLevel.Low
                : MathPrecisionLevel.High;
        }
    }

    /// <summary>
    /// Listener contract for platform scalability profile changes.
    /// </summary>
    public interface IScalabilityChangedEventListener
    {
        /// <summary>Receives one scalability profile change on the dispatcher event lane.</summary>
        /// <param name="payload">Profile transition payload.</param>
        void OnScalabilityChanged(in ScalabilityChangedEvent payload);
    }

    /// <summary>
    /// NativeQueue-backed scalability event lane drained by <see cref="SystemDispatcher"/>.
    /// </summary>
    public static class ScalabilityEvents
    {
        private const int ListenerCapacity = 32;
        private const int PendingEventCapacity = 4;

        // COLD ALLOC: RegistryBucket<IScalabilityChangedEventListener>[32] - platform scalability listeners drained by SystemDispatcher - owner: ScalabilityEvents
        private static readonly RegistryBucket<IScalabilityChangedEventListener> _listeners =
            new RegistryBucket<IScalabilityChangedEventListener>(ListenerCapacity);
        // COLD ALLOC: IScalabilityChangedEventListener[32] - listener additions deferred during scalability dispatch - owner: ScalabilityEvents
        private static readonly IScalabilityChangedEventListener[] _deferredRegisterListeners =
            new IScalabilityChangedEventListener[ListenerCapacity];
        // COLD ALLOC: IScalabilityChangedEventListener[32] - listener removals deferred during scalability dispatch - owner: ScalabilityEvents
        private static readonly IScalabilityChangedEventListener[] _deferredUnregisterListeners =
            new IScalabilityChangedEventListener[ListenerCapacity];

        private static NativeQueue<ScalabilityChangedEvent> _pendingEvents;
        private static NativeQueue<ScalabilityChangedEvent> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedEventCount;
        private static bool _isDispatching;
        private static bool _typedSignalLaneConfigured;

        /// <summary>Number of queued scalability events waiting for dispatcher flush.</summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        internal static int DroppedEventCount => _droppedEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ScalabilityEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ScalabilityEvents), nameof(_nextFrameEvents));
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
            _isDispatching = false;
            _typedSignalLaneConfigured = false;
        }

        /// <summary>Registers a listener for dispatcher-flushed scalability events.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void Register(IScalabilityChangedEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (_isDispatching)
            {
                QueueDeferredRegister(listener);
                return;
            }

            _listeners.TryRegister(listener);
        }

        /// <summary>Unregisters a scalability listener.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void Unregister(IScalabilityChangedEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredUnregister(listener);
                return;
            }

            _listeners.TryUnregister(listener);
            if (_listeners.Count <= 0)
                DropQueuedEvents();
        }

        /// <summary>Queues one scalability change event.</summary>
        /// <param name="payload">Profile transition payload.</param>
        public static void Raise(in ScalabilityChangedEvent payload)
        {
            EnsureTypedSignalLaneConfigured();
            global::Hecton8.Core.Contracts.Signals.SignalBus<ScalabilityChangedEvent>.Push(in payload);

            if (_listeners.Count <= 0)
                return;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                _droppedEventCount++;
                return;
            }

            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
                return;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
        }

        /// <summary>Flushes queued scalability events to listeners on the main dispatcher lane.</summary>
        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            if (_listeners.Count <= 0)
            {
                DropQueuedEvents();
                return;
            }

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out ScalabilityChangedEvent payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                IScalabilityChangedEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IScalabilityChangedEventListener listener = rawArray[i];
                        if (listener == null || IsDeferredUnregisterPending(listener))
                            continue;

                        listener.OnScalabilityChanged(in payload);
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

        private static void EnsureInitialized()
        {
            EnsureTypedSignalLaneConfigured();

            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<ScalabilityChangedEvent>(Allocator.Persistent); // COLD ALLOC: NativeQueue<ScalabilityChangedEvent>[4] - deferred scalability lane - owner: ScalabilityEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(ScalabilityEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<ScalabilityChangedEvent>(Allocator.Persistent); // COLD ALLOC: NativeQueue<ScalabilityChangedEvent>[4] - next-frame scalability lane prevents reentrant dispatch - owner: ScalabilityEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(ScalabilityEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
            }
        }

        private static void EnsureTypedSignalLaneConfigured()
        {
            if (_typedSignalLaneConfigured)
                return;

            GlobalSignals.InitializeAllQueues();
            global::Hecton8.Core.Contracts.Signals.SignalBus<ScalabilityChangedEvent>.EnsureInitialized();
            _typedSignalLaneConfigured = true;
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

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!_pendingEvents.IsCreated ||
                !_nextFrameEvents.IsCreated ||
                !_pendingEvents.IsEmpty() ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            NativeQueue<ScalabilityChangedEvent> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        private static void DropQueuedEvents()
        {
            if (_pendingEvents.IsCreated)
            {
                while (_pendingEvents.TryDequeue(out _))
                {
                }
            }

            if (_nextFrameEvents.IsCreated)
            {
                while (_nextFrameEvents.TryDequeue(out _))
                {
                }
            }

            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
        }

        private static void QueueDeferredRegister(IScalabilityChangedEventListener listener)
        {
            if (_deferredRegisterCount >= ListenerCapacity)
                return;

            _deferredRegisterListeners[_deferredRegisterCount++] = listener;
        }

        private static void QueueDeferredUnregister(IScalabilityChangedEventListener listener)
        {
            if (_deferredUnregisterCount >= ListenerCapacity)
                return;

            _deferredUnregisterListeners[_deferredUnregisterCount++] = listener;
        }

        private static bool IsDeferredUnregisterPending(IScalabilityChangedEventListener listener)
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
                IScalabilityChangedEventListener listener = _deferredUnregisterListeners[i];
                _deferredUnregisterListeners[i] = null;
                _listeners.TryUnregister(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                IScalabilityChangedEventListener listener = _deferredRegisterListeners[i];
                _deferredRegisterListeners[i] = null;
                if (listener != null)
                    _listeners.TryRegister(listener);
            }

            _deferredRegisterCount = 0;
        }
    }
}
