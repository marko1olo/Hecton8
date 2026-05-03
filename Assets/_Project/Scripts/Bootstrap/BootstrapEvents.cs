using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;
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
    [StructLayout(LayoutKind.Sequential)]
    public struct BootstrapEventPayload
    {
        public uint Frame;
        public ushort EventType;
        public ushort StatusBits;

        public bool IsComplete => EventType == (ushort)BootstrapEventType.Complete;
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
    /// Queue-backed bootstrap event lane. Replaces legacy direct static bootstrap callbacks.
    /// </summary>
    public static class BootstrapEvents
    {
        private const int ListenerCapacity = 16;
        private const int PendingEventCapacity = 4;

        // COLD ALLOC: RegistryBucket<IBootstrapEventListener>[16] - bootstrap completion listeners drained by SystemDispatcher - owner: BootstrapEvents
        private static readonly RegistryBucket<IBootstrapEventListener> _listeners = new RegistryBucket<IBootstrapEventListener>(ListenerCapacity);
        private static NativeQueue<BootstrapEventPayload> _pendingEvents;
        private static NativeQueue<BootstrapEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;

        /// <summary>
        /// Pending payload count in the bootstrap event lane.
        /// </summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(BootstrapEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(BootstrapEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
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
            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        /// <summary>
        /// Unregisters a bootstrap event listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Unregister(IBootstrapEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        /// <summary>
        /// Enqueues the bootstrap-complete event.
        /// </summary>
        public static void NotifyBootstrapComplete()
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

            BootstrapEventPayload payload = new BootstrapEventPayload
            {
                Frame = unchecked((uint)Mathf.Max(0, Time.frameCount)),
                EventType = (ushort)BootstrapEventType.Complete,
                StatusBits = 0
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

        /// <summary>
        /// Flushes pending bootstrap events under the dispatcher late-frame budget.
        /// </summary>
        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            if (_listeners.Count <= 0)
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

                if (!_pendingEvents.TryDequeue(out BootstrapEventPayload payload))
                    return;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                IBootstrapEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                        rawArray[i].OnBootstrapEvent(in payload);
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

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<BootstrapEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<BootstrapEventPayload>[4] - deferred bootstrap event lane flushed by SystemDispatcher LateUpdate - owner: BootstrapEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(BootstrapEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<BootstrapEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<BootstrapEventPayload>[4] - next-frame bootstrap event lane prevents same-frame reentrant dispatch - owner: BootstrapEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(BootstrapEvents),
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
            ref NativeQueue<BootstrapEventPayload> queue,
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

            NativeQueue<BootstrapEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }
}
