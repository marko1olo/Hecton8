// ============================================================================
// HECTON-8 - CraftingEvents.cs
// NativeQueue-backed crafting event lane flushed by SystemDispatcher.LateUpdate.
// ============================================================================

using System;
using System.Diagnostics;
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
        CraftCancelled = 6,
        CraftFailed = 7
    }

    /// <summary>
    /// Legacy synthesis payload retained for callers that produce physical crafted outputs.
    /// </summary>
    public readonly struct CraftedItemSynthesisEvent
    {
        public readonly ItemData Item;
        public readonly int Quantity;
        public readonly Vector3 SpawnPosition;
        public readonly Vector3 VelocityChange;

        public CraftedItemSynthesisEvent(ItemData item, int quantity, Vector3 spawnPosition, Vector3 velocityChange)
        {
            Item = item;
            Quantity = quantity;
            SpawnPosition = spawnPosition;
            VelocityChange = velocityChange;
        }
    }

    /// <summary>
    /// Unmanaged crafting event payload carried by the native queue.
    /// Managed references are resolved through sidecar slots during dispatch only.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct CraftingEventPayload
    {
        [FieldOffset(0)] public Vector3 SpawnPosition;
        [FieldOffset(12)] public Vector3 VelocityChange;
        [FieldOffset(24)] public uint FabricatorHashId;
        [FieldOffset(28)] public uint RecipeHashId;
        [FieldOffset(32)] public uint ResultItemHashId;
        [FieldOffset(36)] public float Progress01;
        [FieldOffset(40)] public int Quantity;
        [FieldOffset(44)] public int ReferenceSlot;
        [FieldOffset(48)] public ushort EventType;
        [FieldOffset(50)] public ushort Reserved;
        [FieldOffset(52)] private uint _pad0;
        [FieldOffset(56)] private ulong _pad1;
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
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
        private const uint CraftingListenerOverflowWarningHash = 0x4345564Cu; // CEVL
        private const uint CraftingListenerContextHash = 0x43455652u; // CEVR
        private const uint CraftingListenerExceptionWarningHash = 0x43455645u; // CEVE
        private const uint CraftingListenerExceptionContextHash = 0x43455658u; // CEVX
        private const uint CraftingQueueOverflowWarningHash = 0x43455651u; // CEVQ
        private const uint CraftingQueueContextHash = 0x43455650u; // CEVP
        private const uint CraftingReferenceSlotExhaustedWarningHash = 0x43524653u; // CRFS
        private const uint CraftingReferenceSlotContextHash = 0x43524643u; // CRFC

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

        private struct ListenerSlot
        {
            public ICraftingEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct CraftingListenerRegistry
        {
            private readonly ListenerSlot[] _slots;
            private int _count;

            public CraftingListenerRegistry(int capacity)
            {
                _slots = new ListenerSlot[capacity]; // COLD ALLOC: ListenerSlot[32] - fixed crafting listener slots drained by SystemDispatcher LateUpdate - owner: CraftingEvents
                _count = 0;
            }

            public int Count => _count;

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                    _slots[i].Clear();

                _count = 0;
            }

            public bool Contains(ICraftingEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(ICraftingEventListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public void TryUnregister(ICraftingEventListener listener)
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

            public ICraftingEventListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        private static CraftingListenerRegistry _listeners = new CraftingListenerRegistry(ListenerCapacity);
        // COLD ALLOC: ListenerSlot[32] - listener additions deferred while dispatching crafting events - owner: CraftingEvents
        private static readonly ListenerSlot[] _deferredRegisterListeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: ListenerSlot[32] - listener removals deferred while dispatching crafting events - owner: CraftingEvents
        private static readonly ListenerSlot[] _deferredUnregisterListeners = new ListenerSlot[ListenerCapacity];
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
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedEventCount;
        private static int _droppedReferenceSlotCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _lastQueueOverflowTelemetryFrame = -1;
        private static int _lastReferenceSlotTelemetryFrame = -1;
        private static int _lastListenerOverflowTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        public static int DroppedEventCount => _droppedEventCount;

        public static int DroppedReferenceSlotCount => _droppedReferenceSlotCount;

        public static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;

        public static int ListenerExceptionCount => _listenerExceptionCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticState()
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
            Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterCount);
            Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterCount);
            ClearReferenceSlots();
            _referenceWriteIndex = 0;
            _referencePendingCount = 0;
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedEventCount = 0;
            _droppedReferenceSlotCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _lastQueueOverflowTelemetryFrame = -1;
            _lastReferenceSlotTelemetryFrame = -1;
            _lastListenerOverflowTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _isDispatching = false;
        }

        /// <summary>
        /// Registers a listener for deferred crafting events.
        /// </summary>
        public static void Register(ICraftingEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredRegister(listener);
                return;
            }

            RegisterImmediate(listener);
        }

        /// <summary>
        /// Unregisters a listener from deferred crafting events.
        /// </summary>
        public static void Unregister(ICraftingEventListener listener)
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
                    _pendingEventCount = 0;
                    ReleaseProcessedReferenceSlots(releaseCount);
                    return;
                }

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        ICraftingEventListener listener = _listeners.GetAt(i);
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
        [Obsolete("Use TryRaiseFabricatorOpened(Fabricator) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseFabricatorOpened(Fabricator fabricator)
        {
            TryRaiseFabricatorOpened(fabricator);
        }

        public static bool TryRaiseFabricatorOpened(Fabricator fabricator)
        {
            if (!TryReserveReferenceSlot(CraftingEventType.FabricatorOpened, out int referenceSlot))
                return false;

            _referenceSlots[referenceSlot].Fabricator = fabricator;
            _referenceSlots[referenceSlot].Recipe = null;
            _referenceSlots[referenceSlot].Item = null;

            return Enqueue(new CraftingEventPayload
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
        [Obsolete("Use TryRaiseFabricatorClosed() so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseFabricatorClosed()
        {
            TryRaiseFabricatorClosed();
        }

        public static bool TryRaiseFabricatorClosed()
        {
            return Enqueue(new CraftingEventPayload
            {
                ReferenceSlot = -1,
                EventType = (ushort)CraftingEventType.FabricatorClosed
            });
        }

        /// <summary>
        /// Enqueues a crafting-started event.
        /// </summary>
        [Obsolete("Use TryRaiseCraftStarted(RecipeData) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseCraftStarted(RecipeData recipe)
        {
            TryRaiseCraftStarted(recipe);
        }

        public static bool TryRaiseCraftStarted(RecipeData recipe)
        {
            if (!TryReserveReferenceSlot(CraftingEventType.CraftStarted, out int referenceSlot))
                return false;

            _referenceSlots[referenceSlot].Fabricator = null;
            _referenceSlots[referenceSlot].Recipe = recipe;
            _referenceSlots[referenceSlot].Item = null;

            return Enqueue(new CraftingEventPayload
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
        [Obsolete("Use TryRaiseCraftProgressUpdated(float) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseCraftProgressUpdated(float progress01)
        {
            TryRaiseCraftProgressUpdated(progress01);
        }

        public static bool TryRaiseCraftProgressUpdated(float progress01)
        {
            return Enqueue(new CraftingEventPayload
            {
                Progress01 = progress01,
                ReferenceSlot = -1,
                EventType = (ushort)CraftingEventType.CraftProgressUpdated
            });
        }

        /// <summary>
        /// Enqueues a crafting-completed event and publishes item-crafted telemetry.
        /// </summary>
        [Obsolete("Use TryRaiseCraftCompleted(ItemData) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseCraftCompleted(ItemData resultItem)
        {
            TryRaiseCraftCompleted(resultItem);
        }

        public static bool TryRaiseCraftCompleted(ItemData resultItem)
        {
            uint resultItemHash = ComputeItemHash(resultItem);
            if (resultItemHash != 0u)
                GlobalTelemetryBus.PublishItemCrafted(resultItemHash);

            if (!TryReserveReferenceSlot(CraftingEventType.CraftCompleted, out int referenceSlot))
                return false;

            _referenceSlots[referenceSlot].Fabricator = null;
            _referenceSlots[referenceSlot].Recipe = null;
            _referenceSlots[referenceSlot].Item = resultItem;

            return Enqueue(new CraftingEventPayload
            {
                FabricatorHashId = 0u,
                RecipeHashId = 0u,
                ResultItemHashId = resultItemHash,
                ReferenceSlot = referenceSlot,
                EventType = (ushort)CraftingEventType.CraftCompleted
            });
        }

        /// <summary>
        /// Enqueues a physical crafted-output synthesis event.
        /// </summary>
        [Obsolete("Use TryRaiseCraftOutputSynthesized(CraftedItemSynthesisEvent) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseCraftOutputSynthesized(CraftedItemSynthesisEvent synthesisEvent)
        {
            TryRaiseCraftOutputSynthesized(synthesisEvent);
        }

        public static bool TryRaiseCraftOutputSynthesized(CraftedItemSynthesisEvent synthesisEvent)
        {
            if (!TryReserveReferenceSlot(CraftingEventType.CraftOutputSynthesized, out int referenceSlot))
                return false;

            _referenceSlots[referenceSlot].Fabricator = null;
            _referenceSlots[referenceSlot].Recipe = null;
            _referenceSlots[referenceSlot].Item = synthesisEvent.Item;

            return Enqueue(new CraftingEventPayload
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
        [Obsolete("Use TryRaiseCraftCancelled() so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseCraftCancelled()
        {
            TryRaiseCraftCancelled();
        }

        public static bool TryRaiseCraftCancelled()
        {
            return Enqueue(new CraftingEventPayload
            {
                ReferenceSlot = -1,
                EventType = (ushort)CraftingEventType.CraftCancelled
            });
        }

        /// <summary>
        /// Enqueues a diegetic crafting-failure event for panel shake and local feedback.
        /// </summary>
        [Obsolete("Use TryRaiseCraftFailed(Fabricator) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseCraftFailed(Fabricator fabricator)
        {
            TryRaiseCraftFailed(fabricator);
        }

        public static bool TryRaiseCraftFailed(Fabricator fabricator)
        {
            if (!TryReserveReferenceSlot(CraftingEventType.CraftFailed, out int referenceSlot))
                return false;

            _referenceSlots[referenceSlot].Fabricator = fabricator;
            _referenceSlots[referenceSlot].Recipe = null;
            _referenceSlots[referenceSlot].Item = null;

            return Enqueue(new CraftingEventPayload
            {
                FabricatorHashId = ComputeFabricatorHash(fabricator),
                ReferenceSlot = referenceSlot,
                EventType = (ushort)CraftingEventType.CraftFailed
            });
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<CraftingEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<CraftingEventPayload>[128] - deferred crafting event lane flushed by SystemDispatcher LateUpdate - owner: CraftingEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(CraftingEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<CraftingEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<CraftingEventPayload>[128] - next-frame crafting event lane prevents same-frame reentrant dispatch - owner: CraftingEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(CraftingEvents),
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

        private static bool Enqueue(in CraftingEventPayload payload)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportQueueOverflow(payload.EventType);
                ReleaseReferenceSlot(payload.ReferenceSlot);
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

        private static bool TryReserveReferenceSlot(CraftingEventType eventType, out int referenceSlot)
        {
            referenceSlot = -1;
            if (_referencePendingCount >= ReferenceSlotCapacity)
            {
                ReportReferenceSlotExhausted((ushort)eventType);
                return false;
            }

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

            NativeQueue<CraftingEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        private static void DispatchToListener(ICraftingEventListener listener, in CraftingEventPayload payload)
        {
            try
            {
                listener.OnCraftingEvent(in payload);
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

        private static void QueueDeferredRegister(ICraftingEventListener listener)
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

        private static void QueueDeferredUnregister(ICraftingEventListener listener)
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

        private static bool CancelDeferredRegister(ICraftingEventListener listener)
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

        private static void CancelDeferredUnregister(ICraftingEventListener listener)
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

        private static bool IsDeferredRegisterPending(ICraftingEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(ICraftingEventListener listener)
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
                ICraftingEventListener listener = _deferredUnregisterListeners[i].Listener;
                _deferredUnregisterListeners[i].Clear();
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                ICraftingEventListener listener = _deferredRegisterListeners[i].Listener;
                _deferredRegisterListeners[i].Clear();
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;
        }

        private static void RegisterImmediate(ICraftingEventListener listener)
        {
            if (_listeners.Contains(listener))
                return;

            if (!_listeners.TryRegister(listener))
                ReportListenerRegistrationOverflow();
        }

        private static void ReportQueueOverflow(ushort eventType)
        {
            _droppedEventCount++;
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastQueueOverflowTelemetryFrame == frame)
                return;

            _lastQueueOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                CraftingQueueOverflowWarningHash,
                CraftingQueueContextHash ^ ((uint)eventType << 24),
                Mathf.Max(1, _droppedEventCount));
        }

        private static void ReportReferenceSlotExhausted(ushort eventType)
        {
            _droppedReferenceSlotCount++;
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastReferenceSlotTelemetryFrame == frame)
                return;

            _lastReferenceSlotTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                CraftingReferenceSlotExhaustedWarningHash,
                CraftingReferenceSlotContextHash ^ ((uint)eventType << 24),
                Mathf.Max(1, _droppedReferenceSlotCount));
        }

        private static void ReportListenerRegistrationOverflow()
        {
            _droppedListenerRegistrationCount++;
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastListenerOverflowTelemetryFrame == frame)
                return;

            _lastListenerOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                CraftingListenerOverflowWarningHash,
                CraftingListenerContextHash,
                Mathf.Max(1, _droppedListenerRegistrationCount));
        }

        private static void ReportListenerDispatchException()
        {
            _listenerExceptionCount++;
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastListenerExceptionTelemetryFrame == frame)
                return;

            _lastListenerExceptionTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                CraftingListenerExceptionWarningHash,
                CraftingListenerExceptionContextHash,
                Mathf.Max(1, _listenerExceptionCount));
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
            return recipe != null ? recipe.RuntimeRecipeHash : 0u;
        }

        private static uint ComputeItemHash(ItemData item)
        {
            return item != null ? unchecked((uint)item.PersistentHashId) : 0u;
        }
    }
}
