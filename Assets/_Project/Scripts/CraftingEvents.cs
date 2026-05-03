// ============================================================================
// HECTON-8 - CraftingEvents.cs
// NativeQueue-backed crafting event lane flushed by SystemDispatcher.LateUpdate.
// ============================================================================

using System.Runtime.InteropServices;
using Hecton.Localization;
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
        public uint FabricatorHashId;
        public uint RecipeHashId;
        public uint ResultItemHashId;
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
        private const int PendingEventCapacity = 128;
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
        // COLD ALLOC: bool[128] - reference slot occupancy map prevents wrap overwrite before deferred flush - owner: CraftingEvents
        private static readonly bool[] _referenceSlotOccupied = new bool[ReferenceSlotCapacity];
        // COLD ALLOC: int[128] - reference slots released only after LateUpdate dispatch resolves listeners - owner: CraftingEvents
        private static readonly int[] _referenceSlotsPendingRelease = new int[ReferenceSlotCapacity];
        private static NativeQueue<CraftingEventPayload> _pendingEvents;
        private static NativeQueue<CraftingEventPayload> _nextFrameEvents;
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
                NativeMemorySentinel.UnregisterNativeQueue(nameof(CraftingEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(CraftingEvents), nameof(_nextFrameEvents));
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

            int releaseCount = 0;
            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                {
                    ReleaseProcessedReferenceSlots(releaseCount);
                    return;
                }

                if (!_pendingEvents.TryDequeue(out CraftingEventPayload payload))
                {
                    ReleaseProcessedReferenceSlots(releaseCount);
                    return;
                }

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                ICraftingEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        ICraftingEventListener listener = rawArray[i];
                        if (listener != null)
                            listener.OnCraftingEvent(in payload);
                    }
                }
                finally
                {
                    _isDispatching = false;
                }

                if (IsValidReferenceSlot(payload.ReferenceSlot) && releaseCount < ReferenceSlotCapacity)
                {
                    _referenceSlotsPendingRelease[releaseCount] = payload.ReferenceSlot;
                    releaseCount++;
                }
            }

            ReleaseProcessedReferenceSlots(releaseCount);
            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
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
                FabricatorHashId = ComputeFabricatorHash(fabricator),
                RecipeHashId = 0u,
                ResultItemHashId = 0u,
                ReferenceSlot = referenceSlot,
                EventType = (ushort)CraftingEventType.FabricatorOpened
            });
        }

        /// <summary>
        /// Enqueues a fabricator-closed event.
        /// </summary>
        public static void RaiseFabricatorClosed()
        {
            Enqueue(new CraftingEventPayload
            {
                ReferenceSlot = -1,
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
                FabricatorHashId = 0u,
                RecipeHashId = ComputeRecipeHash(recipe),
                ResultItemHashId = ComputeItemHash(recipe != null ? recipe.resultItem : null),
                ReferenceSlot = referenceSlot,
                EventType = (ushort)CraftingEventType.CraftStarted
            });
        }

        /// <summary>
        /// Enqueues a crafting-progress update.
        /// </summary>
        public static void RaiseCraftProgressUpdated(float progress01)
        {
            Enqueue(new CraftingEventPayload
            {
                Progress01 = progress01,
                ReferenceSlot = -1,
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
                FabricatorHashId = 0u,
                RecipeHashId = 0u,
                ResultItemHashId = ComputeItemHash(resultItem),
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
                FabricatorHashId = 0u,
                RecipeHashId = 0u,
                ResultItemHashId = ComputeItemHash(synthesisEvent.Item),
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
            Enqueue(new CraftingEventPayload
            {
                ReferenceSlot = -1,
                EventType = (ushort)CraftingEventType.CraftCancelled
            });
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<CraftingEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<CraftingEventPayload>[128] - deferred crafting event lane flushed by SystemDispatcher LateUpdate - owner: CraftingEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(CraftingEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<CraftingEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<CraftingEventPayload>[128] - next-frame crafting event lane prevents same-frame reentrant dispatch - owner: CraftingEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(CraftingEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
            }
        }

        private static void Enqueue(in CraftingEventPayload payload)
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

        private static void ReleaseProcessedReferenceSlots(int releaseCount)
        {
            for (int i = 0; i < releaseCount; i++)
            {
                int referenceSlot = _referenceSlotsPendingRelease[i];
                _referenceSlotsPendingRelease[i] = -1;
                ReleaseReferenceSlot(referenceSlot);
            }
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
            ref NativeQueue<CraftingEventPayload> queue,
            ref int pendingCount)
        {
            int scanBudget = pendingCount > 0 ? pendingCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !queue.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!queue.TryDequeue(out CraftingEventPayload payload))
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

            NativeQueue<CraftingEventPayload> swap = _pendingEvents;
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
                _referenceSlotsPendingRelease[i] = -1;
            }
        }

        private static uint ComputeFabricatorHash(Fabricator fabricator)
        {
            return fabricator != null
                ? unchecked((uint)EntityId.ToULong(fabricator.GetEntityId()))
                : 0u;
        }

        private static uint ComputeRecipeHash(RecipeData recipe)
        {
            return recipe != null && !string.IsNullOrWhiteSpace(recipe.name)
                ? unchecked((uint)LocHash.Compute(recipe.name))
                : 0u;
        }

        private static uint ComputeItemHash(ItemData item)
        {
            return item != null && !string.IsNullOrWhiteSpace(item.PersistentId)
                ? unchecked((uint)LocHash.Compute(item.PersistentId))
                : 0u;
        }
    }
}
