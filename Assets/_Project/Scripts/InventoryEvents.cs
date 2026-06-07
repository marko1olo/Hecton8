// ============================================================================
// HECTON-8 - InventoryEvents.cs
// NativeQueue-backed inventory event lane flushed by SystemDispatcher.LateUpdate.
// ============================================================================

using System;
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
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InventoryEventPayload
    {
        [FieldOffset(0)] public float TotalMassKg;
        [FieldOffset(4)] public float CarryCapacityKg;
        [FieldOffset(8)] public float Load01;
        [FieldOffset(12)] public uint ItemHashId;
        [FieldOffset(16)] public int ReferenceSlot;
        [FieldOffset(20)] public ushort EventType;
        [FieldOffset(22)] public ushort Reserved;
        [FieldOffset(24)] private ulong _pad0;
    }

    /// <summary>
    /// Unmanaged physical-drop request emitted by inventory owners after persistence accepts the drop.
    /// World/presentation layers own hydration and prefab visuals.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct InventoryPhysicalDropRequestPayload
    {
        [FieldOffset(0)] public Vector3 RuntimePosition;
        [FieldOffset(12)] public Vector3 InitialImpulse;
        [FieldOffset(24)] public ulong GeneticsMask;
        [FieldOffset(32)] public uint ItemHashId;
        [FieldOffset(36)] public int Quantity;
        [FieldOffset(40)] public ushort QualityMilli;
        [FieldOffset(42)] public ushort Reserved;
        [FieldOffset(44)] public uint _pad0;
        [FieldOffset(48)] private ulong _pad1;
        [FieldOffset(56)] private ulong _pad2;
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
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptOwnerIndexAllocator = Allocator.Persistent;

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

        private struct ListenerSlot
        {
            public IInventoryEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct InventoryListenerRegistry
        {
            private readonly ListenerSlot[] _slots;
            private int _count;

            public InventoryListenerRegistry(int capacity)
            {
                _slots = new ListenerSlot[capacity]; // COLD ALLOC: ListenerSlot[16] - fixed inventory listener slots drained by SystemDispatcher LateUpdate - owner: InventoryEvents
                _count = 0;
            }

            public int Count => _count;

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                    _slots[i].Clear();

                _count = 0;
            }

            public bool Contains(IInventoryEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(IInventoryEventListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public void Unregister(IInventoryEventListener listener)
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

            public IInventoryEventListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        private static InventoryListenerRegistry _listeners = new InventoryListenerRegistry(ListenerCapacity);
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
        private static int _droppedEventCount;
        private static int _dedupFrame = -1;
        private static bool _isDispatching;

        /// <summary>
        /// Number of queued inventory events awaiting LateUpdate dispatch.
        /// </summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        /// <summary>
        /// Number of inventory payloads rejected by fixed queue, sidecar, or dedup capacity.
        /// </summary>
        public static int DroppedEventCount => _droppedEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ReleaseNativeState();

            _listeners.Clear();
            ClearReferenceSlots();
            _referenceWriteIndex = 0;
            _referencePendingCount = 0;
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _droppedEventCount = 0;
            _dedupFrame = -1;
            _isDispatching = false;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorTeardownHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ResetStaticState;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ResetStaticState;
            UnityEditor.EditorApplication.quitting -= ResetStaticState;
            UnityEditor.EditorApplication.quitting += ResetStaticState;
            UnityEditor.EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
        }

        private static void HandleEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                ResetStaticState();
        }
#endif

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
                _listeners.TryRegister(listener);
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
                        IInventoryEventListener listener = _listeners.GetAt(i);
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
        [Obsolete("Use TryNotifyInventoryFull(ItemData) so bounded queue refusal stays visible at the producer.", true)]
        public static void NotifyInventoryFull(ItemData item)
        {
            TryNotifyInventoryFull(item);
        }

        public static bool TryNotifyInventoryFull(ItemData item)
        {
            uint sourceId = item != null
                ? unchecked((uint)EntityId.ToULong(item.GetEntityId()))
                : 0u;
            return TryNotifyInventoryFull(0u, sourceId, item);
        }

        /// <summary>
        /// Enqueues an item pickup failure caused by full inventory.
        /// </summary>
        /// <param name="itemHashId">Rejected item numeric hash.</param>
        [Obsolete("Use TryNotifyInventoryFull(int) so bounded queue refusal stays visible at the producer.", true)]
        public static void NotifyInventoryFull(int itemHashId)
        {
            TryNotifyInventoryFull(itemHashId);
        }

        public static bool TryNotifyInventoryFull(int itemHashId)
        {
            return TryNotifyInventoryFull(unchecked((uint)itemHashId), unchecked((uint)itemHashId), null);
        }

        /// <summary>
        /// Enqueues an item pickup failure caused by full inventory.
        /// </summary>
        /// <param name="itemHashId">Rejected item numeric hash.</param>
        /// <param name="item">Rejected item data.</param>
        [Obsolete("Use TryNotifyInventoryFull(uint,ItemData) so bounded queue refusal stays visible at the producer.", true)]
        public static void NotifyInventoryFull(uint itemHashId, ItemData item)
        {
            TryNotifyInventoryFull(itemHashId, item);
        }

        public static bool TryNotifyInventoryFull(uint itemHashId, ItemData item)
        {
            uint sourceId = itemHashId != 0u
                ? itemHashId
                : (item != null ? unchecked((uint)EntityId.ToULong(item.GetEntityId())) : 0u);
            return TryNotifyInventoryFull(itemHashId, sourceId, item);
        }

        private static bool TryNotifyInventoryFull(uint itemHashId, uint sourceId, ItemData item)
        {
            if (_listeners.Count <= 0)
                return false;

            if (!TryRegisterFrameEventKey(InventoryEventType.InventoryFull, sourceId))
            {
                _droppedEventCount++;
                return false;
            }

            if (!TryReserveReferenceSlot(out int referenceSlot))
            {
                _droppedEventCount++;
                return false;
            }

            _referenceSlots[referenceSlot].Item = item;
            _referenceSlots[referenceSlot].Inventory = null;

            return Enqueue(new InventoryEventPayload
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
        [Obsolete("Use TryNotifyInventoryChanged() so bounded queue refusal stays visible at the producer.", true)]
        public static void NotifyInventoryChanged()
        {
            TryNotifyInventoryChanged();
        }

        public static bool TryNotifyInventoryChanged()
        {
            if (_listeners.Count <= 0)
                return false;

            if (!TryRegisterFrameEventKey(InventoryEventType.InventoryChanged, 0u))
            {
                _droppedEventCount++;
                return false;
            }

            return Enqueue(new InventoryEventPayload
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
        [Obsolete("Use TryNotifyEncumbranceChanged(EncumbranceChangedEvent) so bounded queue refusal stays visible at the producer.", true)]
        public static void NotifyEncumbranceChanged(EncumbranceChangedEvent payload)
        {
            TryNotifyEncumbranceChanged(payload);
        }

        public static bool TryNotifyEncumbranceChanged(EncumbranceChangedEvent payload)
        {
            if (_listeners.Count <= 0)
                return false;

            uint inventorySourceId = payload.Inventory != null ? PlayerInventorySourceId : 0u;
            if (!TryRegisterFrameEventKey(InventoryEventType.EncumbranceChanged, inventorySourceId))
            {
                _droppedEventCount++;
                return false;
            }

            if (!TryReserveReferenceSlot(out int referenceSlot))
            {
                _droppedEventCount++;
                return false;
            }

            _referenceSlots[referenceSlot].Item = null;
            _referenceSlots[referenceSlot].Inventory = payload.Inventory;

            return Enqueue(new InventoryEventPayload
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
            if (!Application.isPlaying)
                return;

            try
            {
                if (!_pendingEvents.IsCreated)
                {
                    _pendingEvents = new NativeQueue<InventoryEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<InventoryEventPayload>[64] — deferred inventory event lane flushed by SystemDispatcher LateUpdate — owner: InventoryEvents
                    RegisterNativeQueue(ref _pendingEvents, PendingEventCapacity, nameof(_pendingEvents));
                    PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
                }

                if (!_nextFrameEvents.IsCreated)
                {
                    _nextFrameEvents = new NativeQueue<InventoryEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<InventoryEventPayload>[64] — next-frame inventory event lane prevents same-frame reentrant dispatch — owner: InventoryEvents
                    RegisterNativeQueue(ref _nextFrameEvents, PendingEventCapacity, nameof(_nextFrameEvents));
                    PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
                }

                if (!_queuedEventKeys.IsCreated)
                {
                    _queuedEventKeys = new NativeParallelHashSet<ulong>(EventDedupCapacity, DataVaultExemptOwnerIndexAllocator); // COLD ALLOC: NativeParallelHashSet<ulong>[128] - per-frame inventory duplicate suppression keys - owner: InventoryEvents
                    RegisterNativeHashSet(ref _queuedEventKeys, nameof(_queuedEventKeys));
                }
            }
            catch
            {
                ReleaseNativeState();
                ClearReferenceSlots();
                _pendingEventCount = 0;
                _nextFrameEventCount = 0;
                _dedupFrame = -1;
                throw;
            }
        }

        private static void RegisterNativeQueue<T>(
            ref NativeQueue<T> queue,
            int capacity,
            string label)
            where T : unmanaged
        {
            int sentinelId = NativeMemorySentinel.RegisterNativeQueue(
                queue,
                capacity,
                nameof(InventoryEvents),
                label,
                NativeAllocationLifetime.Session);
            if (sentinelId > 0)
                return;

            ReleaseNativeQueue(ref queue, label);
            throw new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static void RegisterNativeHashSet<T>(ref NativeParallelHashSet<T> hashSet, string label)
            where T : unmanaged
        {
            int sentinelId = NativeMemorySentinel.RegisterNativeParallelHashSet(
                hashSet,
                nameof(InventoryEvents),
                label,
                NativeAllocationLifetime.Session);
            if (sentinelId > 0)
                return;

            ReleaseNativeHashSet(ref hashSet, label);
            throw new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static void ReleaseNativeState()
        {
            ReleaseNativeQueue(ref _pendingEvents, nameof(_pendingEvents));
            ReleaseNativeQueue(ref _nextFrameEvents, nameof(_nextFrameEvents));
            ReleaseNativeHashSet(ref _queuedEventKeys, nameof(_queuedEventKeys));
        }

        private static void ReleaseNativeQueue<T>(ref NativeQueue<T> queue, string label)
            where T : unmanaged
        {
            if (!queue.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeQueue(nameof(InventoryEvents), label);
            queue.Dispose();
            queue = default;
        }

        private static void ReleaseNativeHashSet<T>(ref NativeParallelHashSet<T> hashSet, string label)
            where T : unmanaged
        {
            if (!hashSet.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeParallelHashSet(nameof(InventoryEvents), label);
            hashSet.Dispose();
            hashSet = default;
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

        private static bool Enqueue(in InventoryEventPayload payload)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReleaseReferenceSlot(payload.ReferenceSlot);
                _droppedEventCount++;
                return false;
            }

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

        private static bool TryRegisterFrameEventKey(InventoryEventType eventType, uint sourceId)
        {
            EnsureInitialized();
            PrepareDedupFrame();
            if (!_queuedEventKeys.IsCreated)
                return true;

            if (_queuedEventKeys.Count() >= _queuedEventKeys.Capacity)
                return false;

            ulong key = ((ulong)sourceId << 32) | ((uint)eventType + 1u);
            return _queuedEventKeys.Add(key);
        }

        private static void PrepareDedupFrame()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
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
                {
                    pendingCount = 0;
                    break;
                }

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
