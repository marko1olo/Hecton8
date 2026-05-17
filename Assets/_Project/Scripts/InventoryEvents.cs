// ============================================================================
// HECTON-8 - InventoryEvents.cs
// NativeQueue-backed inventory event lane flushed by SystemDispatcher.LateUpdate.
// ============================================================================

using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Items;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Inventory
{
    /// <summary>
    /// Inventory event discriminator for <see cref="InventoryEventPayload"/>.
    /// </summary>
    public enum InventoryEventType : byte
    {
        InventoryFull = 0,
        InventoryChanged = 1,
        EncumbranceChanged = 2
    }

    /// <summary>
    /// Carry-load change payload consumed by movement penalties without inventory polling.
    /// </summary>
    public readonly struct EncumbranceChangedEvent
    {
        public EncumbranceChangedEvent(
            PlayerInventory inventory,
            float totalMassKg,
            float carryCapacityKg,
            float load01)
        {
            Inventory = inventory;
            TotalMassKg = totalMassKg;
            CarryCapacityKg = carryCapacityKg;
            Load01 = load01;
        }

        public readonly PlayerInventory Inventory;
        public readonly float TotalMassKg;
        public readonly float CarryCapacityKg;
        public readonly float Load01;
    }

    /// <summary>
    /// Unmanaged inventory payload carried by the native event queue.
    /// Managed references are resolved through the sidecar slot table during LateUpdate dispatch.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 24)]
    public struct InventoryEventPayload
    {
        public float TotalMassKg;
        public float CarryCapacityKg;
        public float Load01;
        public uint ItemHashId;
        public int ReferenceSlot;
        public ushort EventType;
        public ushort Reserved;
    }

    /// <summary>
    /// Unmanaged physical-drop request emitted by inventory owners after persistence accepts the drop.
    /// World/presentation layers own hydration and prefab visuals.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]
    public struct InventoryPhysicalDropRequestPayload
    {
        public Vector3 RuntimePosition;
        public Vector3 InitialImpulse;
        public ulong GeneticsMask;
        public uint ItemHashId;
        public int Quantity;
        public ushort QualityMilli;
        public ushort Reserved;
    }

    /// <summary>
    /// Listener contract for inventory events drained from <see cref="SystemDispatcher"/>.
    /// </summary>
    public interface IInventoryEventListener
    {
        /// <summary>
        /// Consumes one queue-drained inventory event.
        /// </summary>
        /// <param name="payload">Unmanaged inventory event payload.</param>
        void OnInventoryEvent(in InventoryEventPayload payload);
    }

    /// <summary>
    /// Queue-backed global inventory event bus.
    /// </summary>
    public static class InventoryEvents
    {
        private const int ListenerCapacity = 16;
        private const int PendingEventCapacity = 64;
        private const int ReferenceSlotCapacity = 64;
        private const int EventDedupCapacity = 128;
        private const uint PlayerInventorySourceId = 1u;

        private struct InventoryReferenceSlot
        {
            public ItemData Item;
            public PlayerInventory Inventory;

            public void Clear()
            {
                Item = null;
                Inventory = null;
            }
        }

        // COLD ALLOC: RegistryBucket<IInventoryEventListener>[16] — inventory listeners drained by SystemDispatcher LateUpdate — owner: InventoryEvents
        private static readonly RegistryBucket<IInventoryEventListener> _listeners = new RegistryBucket<IInventoryEventListener>(ListenerCapacity);
        // COLD ALLOC: InventoryReferenceSlot[64] — managed reference sidecar for unmanaged inventory payloads — owner: InventoryEvents
        private static readonly InventoryReferenceSlot[] _referenceSlots = new InventoryReferenceSlot[ReferenceSlotCapacity];
        // COLD ALLOC: bool[64] — reference slot occupancy map prevents wrap overwrite before deferred flush — owner: InventoryEvents
        private static readonly bool[] _referenceSlotOccupied = new bool[ReferenceSlotCapacity];
        private static NativeQueue<InventoryEventPayload> _pendingEvents;
        private static NativeQueue<InventoryEventPayload> _nextFrameEvents;
        private static NativeParallelHashSet<ulong> _queuedEventKeys;
        private static int _referenceWriteIndex;
        private static int _referencePendingCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _dedupFrame = -1;
        private static bool _isDispatching;

        /// <summary>
        /// Number of queued inventory events awaiting LateUpdate dispatch.
        /// </summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(InventoryEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(InventoryEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            if (_queuedEventKeys.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelHashSet(nameof(InventoryEvents), nameof(_queuedEventKeys));
                _queuedEventKeys.Dispose();
                _queuedEventKeys = default;
            }

            _listeners.Clear();
            ClearReferenceSlots();
            _referenceWriteIndex = 0;
            _referencePendingCount = 0;
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _dedupFrame = -1;
            _isDispatching = false;
        }

        /// <summary>
        /// Registers a listener for deferred inventory events.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Register(IInventoryEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        /// <summary>
        /// Unregisters a listener from deferred inventory events.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Unregister(IInventoryEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        /// <summary>
        /// Flushes queued inventory events to registered listeners.
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

                if (!_pendingEvents.TryDequeue(out InventoryEventPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                IInventoryEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IInventoryEventListener listener = rawArray[i];
                        if (listener != null)
                            listener.OnInventoryEvent(in payload);
                    }
                }
                finally
                {
                    _isDispatching = false;
                }

                ReleaseReferenceSlot(payload.ReferenceSlot);
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        /// <summary>
        /// Resolves the item attached to an inventory event payload.
        /// Valid only during listener dispatch.
        /// </summary>
        /// <param name="payload">Inventory payload.</param>
        /// <param name="item">Resolved item reference.</param>
        /// <returns>True when an item reference is available.</returns>
        public static bool TryResolveItem(in InventoryEventPayload payload, out ItemData item)
        {
            item = null;
            if (!IsValidReferenceSlot(payload.ReferenceSlot))
                return false;

            item = _referenceSlots[payload.ReferenceSlot].Item;
            return item != null;
        }

        /// <summary>
        /// Builds the managed encumbrance event view for a queue payload.
        /// Valid only during listener dispatch.
        /// </summary>
        /// <param name="payload">Inventory payload.</param>
        /// <param name="encumbranceEvent">Resolved encumbrance event.</param>
        /// <returns>True when the event contains an inventory source.</returns>
        public static bool TryBuildEncumbranceChangedEvent(
            in InventoryEventPayload payload,
            out EncumbranceChangedEvent encumbranceEvent)
        {
            encumbranceEvent = default;
            if (!IsValidReferenceSlot(payload.ReferenceSlot))
                return false;

            PlayerInventory inventory = _referenceSlots[payload.ReferenceSlot].Inventory;
            if (inventory == null)
                return false;

            encumbranceEvent = new EncumbranceChangedEvent(
                inventory,
                payload.TotalMassKg,
                payload.CarryCapacityKg,
                payload.Load01);
            return true;
        }

        /// <summary>
        /// Enqueues an item pickup failure caused by full inventory.
        /// </summary>
        /// <param name="item">Rejected item data.</param>
        public static void NotifyInventoryFull(ItemData item)
        {
            uint sourceId = item != null
                ? unchecked((uint)EntityId.ToULong(item.GetEntityId()))
                : 0u;
            NotifyInventoryFull(0u, sourceId, item);
        }

        /// <summary>
        /// Enqueues an item pickup failure caused by full inventory.
        /// </summary>
        /// <param name="itemHashId">Rejected item numeric hash.</param>
        public static void NotifyInventoryFull(int itemHashId)
        {
            NotifyInventoryFull(unchecked((uint)itemHashId), unchecked((uint)itemHashId), null);
        }

        /// <summary>
        /// Enqueues an item pickup failure caused by full inventory.
        /// </summary>
        /// <param name="itemHashId">Rejected item numeric hash.</param>
        /// <param name="item">Rejected item data.</param>
        public static void NotifyInventoryFull(uint itemHashId, ItemData item)
        {
            uint sourceId = itemHashId != 0u
                ? itemHashId
                : (item != null ? unchecked((uint)EntityId.ToULong(item.GetEntityId())) : 0u);
            NotifyInventoryFull(itemHashId, sourceId, item);
        }

        private static void NotifyInventoryFull(uint itemHashId, uint sourceId, ItemData item)
        {
            if (_listeners.Count <= 0)
                return;

            if (!TryRegisterFrameEventKey(InventoryEventType.InventoryFull, sourceId))
                return;

            if (!TryReserveReferenceSlot(out int referenceSlot))
                return;

            _referenceSlots[referenceSlot].Item = item;
            _referenceSlots[referenceSlot].Inventory = null;

            Enqueue(new InventoryEventPayload
            {
                TotalMassKg = 0f,
                CarryCapacityKg = 0f,
                Load01 = 0f,
                ItemHashId = itemHashId,
                ReferenceSlot = referenceSlot,
                EventType = (ushort)InventoryEventType.InventoryFull,
                Reserved = 0
            });
        }

        /// <summary>
        /// Enqueues a coarse inventory contents changed event.
        /// </summary>
        public static void NotifyInventoryChanged()
        {
            if (_listeners.Count <= 0)
                return;

            if (!TryRegisterFrameEventKey(InventoryEventType.InventoryChanged, 0u))
                return;

            Enqueue(new InventoryEventPayload
            {
                TotalMassKg = 0f,
                CarryCapacityKg = 0f,
                Load01 = 0f,
                ItemHashId = 0u,
                ReferenceSlot = -1,
                EventType = (ushort)InventoryEventType.InventoryChanged,
                Reserved = 0
            });
        }

        /// <summary>
        /// Enqueues a derived carry-load change.
        /// </summary>
        /// <param name="payload">Managed producer payload.</param>
        public static void NotifyEncumbranceChanged(EncumbranceChangedEvent payload)
        {
            if (_listeners.Count <= 0)
                return;

            uint inventorySourceId = payload.Inventory != null ? PlayerInventorySourceId : 0u;
            if (!TryRegisterFrameEventKey(InventoryEventType.EncumbranceChanged, inventorySourceId))
                return;

            if (!TryReserveReferenceSlot(out int referenceSlot))
                return;

            _referenceSlots[referenceSlot].Item = null;
            _referenceSlots[referenceSlot].Inventory = payload.Inventory;

            Enqueue(new InventoryEventPayload
            {
                TotalMassKg = payload.TotalMassKg,
                CarryCapacityKg = payload.CarryCapacityKg,
                Load01 = payload.Load01,
                ItemHashId = 0u,
                ReferenceSlot = referenceSlot,
                EventType = (ushort)InventoryEventType.EncumbranceChanged,
                Reserved = 0
            });
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<InventoryEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<InventoryEventPayload>[64] — deferred inventory event lane flushed by SystemDispatcher LateUpdate — owner: InventoryEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(InventoryEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<InventoryEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<InventoryEventPayload>[64] — next-frame inventory event lane prevents same-frame reentrant dispatch — owner: InventoryEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(InventoryEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
            }

            if (!_queuedEventKeys.IsCreated)
            {
                _queuedEventKeys = new NativeParallelHashSet<ulong>(EventDedupCapacity, Allocator.Persistent); // COLD ALLOC: NativeParallelHashSet<ulong>[128] - per-frame inventory duplicate suppression keys - owner: InventoryEvents
                NativeMemorySentinel.RegisterNativeParallelHashSet(
                    _queuedEventKeys,
                    nameof(InventoryEvents),
                    nameof(_queuedEventKeys),
                    NativeAllocationLifetime.Session);
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

        private static void Enqueue(in InventoryEventPayload payload)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReleaseReferenceSlot(payload.ReferenceSlot);
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

        private static bool TryRegisterFrameEventKey(InventoryEventType eventType, uint sourceId)
        {
            EnsureInitialized();
            PrepareDedupFrame();
            if (!_queuedEventKeys.IsCreated)
                return true;

            if (_queuedEventKeys.Count() >= _queuedEventKeys.Capacity)
                return true;

            ulong key = ((ulong)sourceId << 32) | ((uint)eventType + 1u);
            return _queuedEventKeys.Add(key);
        }

        private static void PrepareDedupFrame()
        {
            int frame = Time.frameCount;
            if (_dedupFrame == frame)
                return;

            if (_queuedEventKeys.IsCreated)
                _queuedEventKeys.Clear();

            _dedupFrame = frame;
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
            ref NativeQueue<InventoryEventPayload> queue,
            ref int pendingCount)
        {
            int scanBudget = pendingCount > 0 ? pendingCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !queue.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!queue.TryDequeue(out InventoryEventPayload payload))
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

            NativeQueue<InventoryEventPayload> swap = _pendingEvents;
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

    }
}
