// ============================================================================
// HECTON-8 - InteractionEvents.cs
// NativeQueue-backed interaction event lane flushed by SystemDispatcher.LateUpdate.
// ============================================================================

namespace Hecton8.Interaction
{
    using System.Runtime.InteropServices;
    using Hecton.Localization;
    using Hecton8.Core;
    using Hecton8.Items;
    using Unity.Collections;
    using UnityEngine;

    /// <summary>
    /// Interaction event discriminator for <see cref="InteractionEventPayload"/>.
    /// </summary>
    public enum InteractionEventType : byte
    {
        ItemCollected = 0,
        InteractionStarted = 1,
        HoverChanged = 2
    }

    /// <summary>
    /// Unmanaged event payload carried by the native interaction queue.
    /// Managed Unity references are resolved through the event lane sidecar during dispatch only.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct InteractionEventPayload
    {
        public uint ItemHashId;
        public uint TargetHashId;
        public uint InteractorHashId;
        public int ReferenceSlot;
        public int Quantity;
        public ushort EventType;
        public ushort Reserved;
    }

    /// <summary>
    /// Listener contract for interaction events drained from <see cref="SystemDispatcher"/>.
    /// </summary>
    public interface IInteractionEventListener
    {
        void OnInteractionEvent(in InteractionEventPayload payload);
    }

    /// <summary>
    /// Queue-backed global interaction event bus.
    /// </summary>
    public static class InteractionEvents
    {
        private const int ListenerCapacity = 32;
        private const int PendingEventCapacity = 128;
        private const int ReferenceSlotCapacity = 128;

        private struct InteractionReferenceSlot
        {
            public ItemData Item;
            public IInteractable Target;
            public Transform Interactor;

            public void Clear()
            {
                Item = null;
                Target = null;
                Interactor = null;
            }
        }

        // COLD ALLOC: RegistryBucket<IInteractionEventListener>[32] - interaction listeners drained by SystemDispatcher LateUpdate - owner: InteractionEvents
        private static readonly RegistryBucket<IInteractionEventListener> _listeners = new RegistryBucket<IInteractionEventListener>(ListenerCapacity);
        // COLD ALLOC: InteractionReferenceSlot[128] - managed reference sidecar for unmanaged interaction payloads - owner: InteractionEvents
        private static readonly InteractionReferenceSlot[] _referenceSlots = new InteractionReferenceSlot[ReferenceSlotCapacity];
        // COLD ALLOC: bool[128] - reference slot occupancy map prevents wrap overwrite before deferred flush - owner: InteractionEvents
        private static readonly bool[] _referenceSlotOccupied = new bool[ReferenceSlotCapacity];
        private static NativeQueue<InteractionEventPayload> _pendingEvents;
        private static NativeQueue<InteractionEventPayload> _nextFrameEvents;
        private static int _referenceWriteIndex;
        private static int _referencePendingCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(InteractionEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(InteractionEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            ClearReferenceSlots();
            _referenceWriteIndex = 0;
            _referencePendingCount = 0;
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
        }

        /// <summary>
        /// Registers a listener for deferred interaction events.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Register(IInteractionEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        /// <summary>
        /// Unregisters a listener from deferred interaction events.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Unregister(IInteractionEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        /// <summary>
        /// Flushes queued interaction events to registered listeners.
        /// Called by <see cref="SystemDispatcher"/> from LateUpdate.
        /// </summary>
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

                if (!_pendingEvents.TryDequeue(out InteractionEventPayload payload))
                    break;

                IInteractionEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IInteractionEventListener listener = rawArray[i];
                        if (listener != null)
                            listener.OnInteractionEvent(in payload);
                    }
                }
                finally
                {
                    _isDispatching = false;
                }

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                ReleaseReferenceSlot(payload.ReferenceSlot);
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        /// <summary>
        /// Resolves the collected item reference attached to a queued interaction event.
        /// Valid only during listener dispatch.
        /// </summary>
        public static bool TryResolveItem(in InteractionEventPayload payload, out ItemData item)
        {
            item = null;
            if (!IsValidReferenceSlot(payload.ReferenceSlot))
                return false;

            item = _referenceSlots[payload.ReferenceSlot].Item;
            return item != null;
        }

        /// <summary>
        /// Resolves the target interactable reference attached to a queued interaction event.
        /// Valid only during listener dispatch.
        /// </summary>
        public static bool TryResolveTarget(in InteractionEventPayload payload, out IInteractable target)
        {
            target = null;
            if (!IsValidReferenceSlot(payload.ReferenceSlot))
                return false;

            target = _referenceSlots[payload.ReferenceSlot].Target;
            return target != null;
        }

        /// <summary>
        /// Resolves the interactor transform reference attached to a queued interaction event.
        /// Valid only during listener dispatch.
        /// </summary>
        public static bool TryResolveInteractor(in InteractionEventPayload payload, out Transform interactor)
        {
            interactor = null;
            if (!IsValidReferenceSlot(payload.ReferenceSlot))
                return false;

            interactor = _referenceSlots[payload.ReferenceSlot].Interactor;
            return interactor != null;
        }

        /// <summary>
        /// Enqueues a world item collection event.
        /// </summary>
        public static void RaiseItemCollected(ItemData item, int quantity, Transform interactor)
        {
            if (!TryReserveReferenceSlot(out int referenceSlot))
                return;

            _referenceSlots[referenceSlot].Item = item;
            _referenceSlots[referenceSlot].Target = null;
            _referenceSlots[referenceSlot].Interactor = interactor;

            Enqueue(new InteractionEventPayload
            {
                ItemHashId = ComputeItemHash(item),
                TargetHashId = 0u,
                InteractorHashId = ComputeTransformHash(interactor),
                ReferenceSlot = referenceSlot,
                Quantity = quantity,
                EventType = (ushort)InteractionEventType.ItemCollected,
                Reserved = 0
            });
        }

        /// <summary>
        /// Enqueues an interaction-started event.
        /// </summary>
        public static void RaiseInteractionStarted(IInteractable target, Transform interactor)
        {
            if (!TryReserveReferenceSlot(out int referenceSlot))
                return;

            _referenceSlots[referenceSlot].Item = null;
            _referenceSlots[referenceSlot].Target = target;
            _referenceSlots[referenceSlot].Interactor = interactor;

            Enqueue(new InteractionEventPayload
            {
                ItemHashId = 0u,
                TargetHashId = ComputeInteractableHash(target),
                InteractorHashId = ComputeTransformHash(interactor),
                ReferenceSlot = referenceSlot,
                Quantity = 0,
                EventType = (ushort)InteractionEventType.InteractionStarted,
                Reserved = 0
            });
        }

        /// <summary>
        /// Enqueues a hover-target change event.
        /// </summary>
        public static void RaiseHoverChanged(IInteractable target)
        {
            int referenceSlot = -1;
            if (target != null)
            {
                if (!TryReserveReferenceSlot(out referenceSlot))
                    return;

                _referenceSlots[referenceSlot].Item = null;
                _referenceSlots[referenceSlot].Target = target;
                _referenceSlots[referenceSlot].Interactor = null;
            }

            Enqueue(new InteractionEventPayload
            {
                ItemHashId = 0u,
                TargetHashId = ComputeInteractableHash(target),
                InteractorHashId = 0u,
                ReferenceSlot = referenceSlot,
                Quantity = 0,
                EventType = (ushort)InteractionEventType.HoverChanged,
                Reserved = 0
            });
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<InteractionEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<InteractionEventPayload>[128] - deferred interaction event lane flushed by SystemDispatcher LateUpdate - owner: InteractionEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(InteractionEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<InteractionEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<InteractionEventPayload>[128] - next-frame interaction event lane prevents same-frame reentrant dispatch - owner: InteractionEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(InteractionEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
            }
        }

        private static bool Enqueue(in InteractionEventPayload payload)
        {
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReleaseReferenceSlot(payload.ReferenceSlot);
                return false;
            }

            EnsureInitialized();
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

        private static bool TryReserveReferenceSlot(out int referenceSlot)
        {
            referenceSlot = -1;
            if (_referencePendingCount >= ReferenceSlotCapacity)
                return false;

            for (int probe = 0; probe < ReferenceSlotCapacity; probe++)
            {
                int candidateSlot = _referenceWriteIndex;
                _referenceWriteIndex++;
                if (_referenceWriteIndex >= ReferenceSlotCapacity)
                    _referenceWriteIndex = 0;

                if (_referenceSlotOccupied[candidateSlot])
                    continue;

                referenceSlot = candidateSlot;
                _referenceSlotOccupied[referenceSlot] = true;
                _referencePendingCount++;
                return true;
            }

            return false;
        }

        private static void ReleaseReferenceSlot(int referenceSlot)
        {
            if (!IsValidReferenceSlot(referenceSlot))
                return;

            if (!_referenceSlotOccupied[referenceSlot])
                return;

            _referenceSlots[referenceSlot].Clear();
            _referenceSlotOccupied[referenceSlot] = false;
            if (_referencePendingCount > 0)
                _referencePendingCount--;
        }

        private static bool IsValidReferenceSlot(int referenceSlot)
        {
            return (uint)referenceSlot < ReferenceSlotCapacity;
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
            ref NativeQueue<InteractionEventPayload> queue,
            ref int pendingCount)
        {
            int scanBudget = pendingCount > 0 ? pendingCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !queue.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!queue.TryDequeue(out InteractionEventPayload payload))
                    break;

                if (pendingCount > 0)
                    pendingCount--;

                ReleaseReferenceSlot(payload.ReferenceSlot);
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

            NativeQueue<InteractionEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        private static void ClearReferenceSlots()
        {
            for (int i = 0; i < ReferenceSlotCapacity; i++)
            {
                _referenceSlots[i].Clear();
                _referenceSlotOccupied[i] = false;
            }
        }

        private static uint ComputeItemHash(ItemData item)
        {
            return item != null && !string.IsNullOrWhiteSpace(item.PersistentId)
                ? unchecked((uint)LocHash.Compute(item.PersistentId))
                : 0u;
        }

        private static uint ComputeInteractableHash(IInteractable target)
        {
            return target is UnityEngine.Object targetObject
                ? unchecked((uint)EntityId.ToULong(targetObject.GetEntityId()))
                : 0u;
        }

        private static uint ComputeTransformHash(Transform transform)
        {
            return transform != null
                ? unchecked((uint)EntityId.ToULong(transform.GetEntityId()))
                : 0u;
        }
    }
}
