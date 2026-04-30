// ============================================================================
// HECTON-8 - InteractionEvents.cs
// NativeQueue-backed interaction event lane flushed by SystemDispatcher.LateUpdate.
// ============================================================================

namespace Hecton8.Interaction
{
    using System.Runtime.InteropServices;
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
        private static NativeQueue<InteractionEventPayload> _pendingEvents;
        private static int _referenceWriteIndex;
        private static int _referencePendingCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            _listeners.Clear();
            ClearReferenceSlots();
            _referenceWriteIndex = 0;
            _referencePendingCount = 0;
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

            while (_pendingEvents.TryDequeue(out InteractionEventPayload payload))
            {
                IInteractionEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                    rawArray[i].OnInteractionEvent(in payload);

                ReleaseReferenceSlot(payload.ReferenceSlot);
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
            if (!TryReserveReferenceSlot(out int referenceSlot))
                return;

            _referenceSlots[referenceSlot].Item = null;
            _referenceSlots[referenceSlot].Target = target;
            _referenceSlots[referenceSlot].Interactor = null;

            Enqueue(new InteractionEventPayload
            {
                ReferenceSlot = referenceSlot,
                Quantity = 0,
                EventType = (ushort)InteractionEventType.HoverChanged,
                Reserved = 0
            });
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
                _pendingEvents = new NativeQueue<InteractionEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<InteractionEventPayload>[128] - deferred interaction event lane flushed by SystemDispatcher LateUpdate - owner: InteractionEvents
        }

        private static void Enqueue(in InteractionEventPayload payload)
        {
            EnsureInitialized();
            _pendingEvents.Enqueue(payload);
        }

        private static bool TryReserveReferenceSlot(out int referenceSlot)
        {
            referenceSlot = -1;
            if (_referencePendingCount >= ReferenceSlotCapacity)
                return false;

            referenceSlot = _referenceWriteIndex;
            _referenceWriteIndex++;
            if (_referenceWriteIndex >= ReferenceSlotCapacity)
                _referenceWriteIndex = 0;

            _referencePendingCount++;
            return true;
        }

        private static void ReleaseReferenceSlot(int referenceSlot)
        {
            if (!IsValidReferenceSlot(referenceSlot))
                return;

            _referenceSlots[referenceSlot].Clear();
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

            while (_pendingEvents.TryDequeue(out InteractionEventPayload payload))
                ReleaseReferenceSlot(payload.ReferenceSlot);
        }

        private static void ClearReferenceSlots()
        {
            for (int i = 0; i < ReferenceSlotCapacity; i++)
                _referenceSlots[i].Clear();
        }
    }
}
