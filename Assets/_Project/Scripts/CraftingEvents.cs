// ============================================================================
// HECTON-8 - CraftingEvents.cs
// NativeQueue-backed crafting event lane flushed by SystemDispatcher.LateUpdate.
// ============================================================================

using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Items;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Crafting
{
    /// <summary>
    /// Blittable crafting event discriminator for <see cref="CraftingEventPayload"/>.
    /// </summary>
    public enum CraftingEventType : byte
    {
        FabricatorOpened = 0,
        FabricatorClosed = 1,
        CraftStarted = 2,
        CraftProgressUpdated = 3,
        CraftCompleted = 4,
        CraftOutputSynthesized = 5,
        CraftCancelled = 6
    }

    /// <summary>
    /// Legacy synthesis payload retained for callers that produce physical crafted outputs.
    /// </summary>
    public readonly struct CraftedItemSynthesisEvent
    {
        public CraftedItemSynthesisEvent(ItemData item, int quantity, Vector3 spawnPosition, Vector3 velocityChange)
        {
            Item = item;
            Quantity = quantity;
            SpawnPosition = spawnPosition;
            VelocityChange = velocityChange;
        }

        public ItemData Item { get; }
        public int Quantity { get; }
        public Vector3 SpawnPosition { get; }
        public Vector3 VelocityChange { get; }
    }

    /// <summary>
    /// Unmanaged crafting event payload carried by the native queue.
    /// Managed references are resolved through sidecar slots during dispatch only.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CraftingEventPayload
    {
        public Vector3 SpawnPosition;
        public Vector3 VelocityChange;
        public float Progress01;
        public int Quantity;
        public int ReferenceSlot;
        public ushort EventType;
        public ushort Reserved;
    }

    /// <summary>
    /// Listener contract for crafting events drained from <see cref="SystemDispatcher"/>.
    /// </summary>
    public interface ICraftingEventListener
    {
        void OnCraftingEvent(in CraftingEventPayload payload);
    }

    /// <summary>
    /// Queue-backed global crafting event bus.
    /// </summary>
    public static class CraftingEvents
    {
        private const int ListenerCapacity = 32;
        private const int ReferenceSlotCapacity = 128;

        private struct CraftingReferenceSlot
        {
            public Fabricator Fabricator;
            public RecipeData Recipe;
            public ItemData Item;

            public void Clear()
            {
                Fabricator = null;
                Recipe = null;
                Item = null;
            }
        }

        // COLD ALLOC: RegistryBucket<ICraftingEventListener>[32] - crafting listeners drained by SystemDispatcher LateUpdate - owner: CraftingEvents
        private static readonly RegistryBucket<ICraftingEventListener> _listeners = new RegistryBucket<ICraftingEventListener>(ListenerCapacity);
        // COLD ALLOC: CraftingReferenceSlot[128] - managed reference sidecar for unmanaged crafting payloads - owner: CraftingEvents
        private static readonly CraftingReferenceSlot[] _referenceSlots = new CraftingReferenceSlot[ReferenceSlotCapacity];
        private static NativeQueue<CraftingEventPayload> _pendingEvents;
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
        /// Registers a listener for deferred crafting events.
        /// </summary>
        public static void Register(ICraftingEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        /// <summary>
        /// Unregisters a listener from deferred crafting events.
        /// </summary>
        public static void Unregister(ICraftingEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        /// <summary>
        /// Flushes queued crafting events to registered listeners.
        /// Called by <see cref="SystemDispatcher"/> from LateUpdate.
        /// </summary>
        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated || _listeners.Count <= 0)
            {
                DrainWithoutDispatch();
                return;
            }

            while (_pendingEvents.TryDequeue(out CraftingEventPayload payload))
            {
                ICraftingEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                    rawArray[i].OnCraftingEvent(in payload);

                ReleaseReferenceSlot(payload.ReferenceSlot);
            }
        }

        /// <summary>
        /// Resolves a fabricator reference for an event payload.
        /// Valid only during listener dispatch.
        /// </summary>
        public static bool TryResolveFabricator(in CraftingEventPayload payload, out Fabricator fabricator)
        {
            fabricator = null;
            if (!IsValidReferenceSlot(payload.ReferenceSlot))
                return false;

            fabricator = _referenceSlots[payload.ReferenceSlot].Fabricator;
            return fabricator != null;
        }

        /// <summary>
        /// Resolves a recipe reference for an event payload.
        /// Valid only during listener dispatch.
        /// </summary>
        public static bool TryResolveRecipe(in CraftingEventPayload payload, out RecipeData recipe)
        {
            recipe = null;
            if (!IsValidReferenceSlot(payload.ReferenceSlot))
                return false;

            recipe = _referenceSlots[payload.ReferenceSlot].Recipe;
            return recipe != null;
        }

        /// <summary>
        /// Resolves an item reference for an event payload.
        /// Valid only during listener dispatch.
        /// </summary>
        public static bool TryResolveItem(in CraftingEventPayload payload, out ItemData item)
        {
            item = null;
            if (!IsValidReferenceSlot(payload.ReferenceSlot))
                return false;

            item = _referenceSlots[payload.ReferenceSlot].Item;
            return item != null;
        }

        /// <summary>
        /// Enqueues a fabricator-opened event.
        /// </summary>
        public static void RaiseFabricatorOpened(Fabricator fabricator)
        {
            if (!TryReserveReferenceSlot(out int referenceSlot))
                return;

            _referenceSlots[referenceSlot].Fabricator = fabricator;
            _referenceSlots[referenceSlot].Recipe = null;
            _referenceSlots[referenceSlot].Item = null;

            Enqueue(new CraftingEventPayload
            {
                ReferenceSlot = referenceSlot,
                EventType = (ushort)CraftingEventType.FabricatorOpened
            });
        }

        /// <summary>
        /// Enqueues a fabricator-closed event.
        /// </summary>
        public static void RaiseFabricatorClosed()
        {
            if (!TryReserveReferenceSlot(out int referenceSlot))
                return;

            Enqueue(new CraftingEventPayload
            {
                ReferenceSlot = referenceSlot,
                EventType = (ushort)CraftingEventType.FabricatorClosed
            });
        }

        /// <summary>
        /// Enqueues a crafting-started event.
        /// </summary>
        public static void RaiseCraftStarted(RecipeData recipe)
        {
            if (!TryReserveReferenceSlot(out int referenceSlot))
                return;

            _referenceSlots[referenceSlot].Fabricator = null;
            _referenceSlots[referenceSlot].Recipe = recipe;
            _referenceSlots[referenceSlot].Item = null;

            Enqueue(new CraftingEventPayload
            {
                ReferenceSlot = referenceSlot,
                EventType = (ushort)CraftingEventType.CraftStarted
            });
        }

        /// <summary>
        /// Enqueues a crafting-progress update.
        /// </summary>
        public static void RaiseCraftProgressUpdated(float progress01)
        {
            if (!TryReserveReferenceSlot(out int referenceSlot))
                return;

            Enqueue(new CraftingEventPayload
            {
                Progress01 = progress01,
                ReferenceSlot = referenceSlot,
                EventType = (ushort)CraftingEventType.CraftProgressUpdated
            });
        }

        /// <summary>
        /// Enqueues a crafting-completed event and publishes item-crafted telemetry.
        /// </summary>
        public static void RaiseCraftCompleted(ItemData resultItem)
        {
            if (resultItem != null)
                GlobalTelemetryBus.PublishItemCrafted(resultItem.PersistentId);

            if (!TryReserveReferenceSlot(out int referenceSlot))
                return;

            _referenceSlots[referenceSlot].Fabricator = null;
            _referenceSlots[referenceSlot].Recipe = null;
            _referenceSlots[referenceSlot].Item = resultItem;

            Enqueue(new CraftingEventPayload
            {
                ReferenceSlot = referenceSlot,
                EventType = (ushort)CraftingEventType.CraftCompleted
            });
        }

        /// <summary>
        /// Enqueues a physical crafted-output synthesis event.
        /// </summary>
        public static void RaiseCraftOutputSynthesized(CraftedItemSynthesisEvent synthesisEvent)
        {
            if (!TryReserveReferenceSlot(out int referenceSlot))
                return;

            _referenceSlots[referenceSlot].Fabricator = null;
            _referenceSlots[referenceSlot].Recipe = null;
            _referenceSlots[referenceSlot].Item = synthesisEvent.Item;

            Enqueue(new CraftingEventPayload
            {
                SpawnPosition = synthesisEvent.SpawnPosition,
                VelocityChange = synthesisEvent.VelocityChange,
                Quantity = synthesisEvent.Quantity,
                ReferenceSlot = referenceSlot,
                EventType = (ushort)CraftingEventType.CraftOutputSynthesized
            });
        }

        /// <summary>
        /// Enqueues a crafting-cancelled event.
        /// </summary>
        public static void RaiseCraftCancelled()
        {
            if (!TryReserveReferenceSlot(out int referenceSlot))
                return;

            Enqueue(new CraftingEventPayload
            {
                ReferenceSlot = referenceSlot,
                EventType = (ushort)CraftingEventType.CraftCancelled
            });
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
                _pendingEvents = new NativeQueue<CraftingEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<CraftingEventPayload>[128] - deferred crafting event lane flushed by SystemDispatcher LateUpdate - owner: CraftingEvents
        }

        private static void Enqueue(in CraftingEventPayload payload)
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

            while (_pendingEvents.TryDequeue(out CraftingEventPayload payload))
                ReleaseReferenceSlot(payload.ReferenceSlot);
        }

        private static void ClearReferenceSlots()
        {
            for (int i = 0; i < ReferenceSlotCapacity; i++)
                _referenceSlots[i].Clear();
        }
    }
}
