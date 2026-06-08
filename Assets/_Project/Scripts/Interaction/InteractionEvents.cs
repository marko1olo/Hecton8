// ============================================================================
// HECTON-8 - InteractionEvents.cs
// NativeQueue-backed interaction event lane flushed by SystemDispatcher.LateUpdate.
// ============================================================================

namespace Hecton8.Interaction
{
    using System;
    using System.Diagnostics;
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
        HoverChanged = 2,
        ItemLost = 3
    }

    /// <summary>
    /// Unmanaged event payload carried by the native interaction queue.
    /// Managed Unity references are resolved through the event lane sidecar during dispatch only.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InteractionEventPayload
    {
        [FieldOffset(0)] public uint ItemHashId;
        [FieldOffset(4)] public uint TargetHashId;
        [FieldOffset(8)] public uint InteractorHashId;
        [FieldOffset(12)] public int ReferenceSlot;
        [FieldOffset(16)] public int Quantity;
        [FieldOffset(20)] public ushort EventType;
        [FieldOffset(22)] public ushort Reserved;
        [FieldOffset(24)] private ulong _pad0;
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
        private const uint InteractionListenerOverflowWarningHash = 0x4945564Cu; // IEVL
        private const uint InteractionListenerContextHash = 0x49455652u; // IEVR
        private const uint InteractionListenerExceptionWarningHash = 0x49455645u; // IEVE
        private const uint InteractionListenerExceptionContextHash = 0x49455658u; // IEVX
        private const uint InteractionQueueOverflowWarningHash = 0x49455651u; // IEVQ
        private const uint InteractionQueueContextHash = 0x49455650u; // IEVP
        private const uint InteractionInvalidItemEventWarningHash = 0x4945564Du; // IEVM
        private const uint InteractionInvalidItemEventContextHash = 0x4945564Fu; // IEVO
        private const uint InteractionQueueInitializationFailureWarningHash = 0x49455649u; // IEVI
        private const uint InteractionQueueReleaseFailureWarningHash = 0x49455644u; // IEVD
        private const uint InteractionReferenceSlotExhaustedWarningHash = 0x49524653u; // IRFS
        private const uint InteractionReferenceSlotContextHash = 0x49524643u; // IRFC
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;

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

        // Fixed listener storage avoids interface-container array exposure during dispatcher drain.
        private struct ListenerSlot
        {
            public IInteractionEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct InteractionListenerRegistry
        {
            private readonly ListenerSlot[] _slots;
            private int _count;

            public InteractionListenerRegistry(int capacity)
            {
                _slots = new ListenerSlot[capacity]; // COLD ALLOC: ListenerSlot[32] - fixed interaction listener slots drained by SystemDispatcher LateUpdate - owner: InteractionEvents
                _count = 0;
            }

            public int Count => _count;

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                    _slots[i].Clear();

                _count = 0;
            }

            public bool Contains(IInteractionEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(IInteractionEventListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public void TryUnregister(IInteractionEventListener listener)
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

            public IInteractionEventListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        private static InteractionListenerRegistry _listeners = new InteractionListenerRegistry(ListenerCapacity);
        // COLD ALLOC: ListenerSlot[32] - listener additions deferred while dispatching interaction events - owner: InteractionEvents
        private static readonly ListenerSlot[] _deferredRegisterListeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: ListenerSlot[32] - listener removals deferred while dispatching interaction events - owner: InteractionEvents
        private static readonly ListenerSlot[] _deferredUnregisterListeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: InteractionReferenceSlot[128] — managed reference sidecar for unmanaged interaction payloads — owner: InteractionEvents
        private static readonly InteractionReferenceSlot[] _referenceSlots = new InteractionReferenceSlot[ReferenceSlotCapacity];
        // COLD ALLOC: bool[128] — reference slot occupancy map prevents wrap overwrite before deferred flush — owner: InteractionEvents
        private static readonly bool[] _referenceSlotOccupied = new bool[ReferenceSlotCapacity];
        // COLD ALLOC: ushort[128] - reference slot generations invalidate stale payload handles after sidecar reuse - owner: InteractionEvents
        private static readonly ushort[] _referenceSlotGenerations = new ushort[ReferenceSlotCapacity];
        private static NativeQueue<InteractionEventPayload> _pendingEvents;
        private static NativeQueue<InteractionEventPayload> _nextFrameEvents;
        private static int _pendingEventsSentinelId;
        private static int _nextFrameEventsSentinelId;
        private static int _referenceWriteIndex;
        private static int _referencePendingCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedEventCount;
        private static int _droppedInvalidItemEventCount;
        private static int _droppedReferenceSlotCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _lastQueueOverflowTelemetryFrame = -1;
        private static int _lastInvalidItemEventTelemetryFrame = -1;
        private static int _lastQueueInitializationFailureTelemetryFrame = -1;
        private static int _lastQueueReleaseFailureTelemetryFrame = -1;
        private static int _lastReferenceSlotTelemetryFrame = -1;
        private static int _lastListenerOverflowTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        public static int DroppedEventCount => _droppedEventCount;

        public static int DroppedInvalidItemEventCount => _droppedInvalidItemEventCount;

        public static int DroppedReferenceSlotCount => _droppedReferenceSlotCount;

        public static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;

        public static int ListenerExceptionCount => _listenerExceptionCount;

        internal static void PrewarmCold()
        {
            try
            {
                EnsureInitialized();
            }
            catch (Exception exception)
            {
                ReportQueueInitializationFailure((ushort)InteractionEventType.InteractionStarted, exception);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticState()
        {
            Exception releaseException = ReleaseNativeQueuesBestEffort();

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
            _droppedInvalidItemEventCount = 0;
            _droppedReferenceSlotCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _lastQueueOverflowTelemetryFrame = -1;
            _lastInvalidItemEventTelemetryFrame = -1;
            _lastQueueInitializationFailureTelemetryFrame = -1;
            _lastQueueReleaseFailureTelemetryFrame = -1;
            _lastReferenceSlotTelemetryFrame = -1;
            _lastListenerOverflowTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _isDispatching = false;

            ReportQueueReleaseFailure(releaseException);
        }

        /// <summary>
        /// Registers a listener for deferred interaction events.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Register(IInteractionEventListener listener)
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
        /// Unregisters a listener from deferred interaction events.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Unregister(IInteractionEventListener listener)
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
                {
                    _pendingEventCount = 0;
                    break;
                }

                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IInteractionEventListener listener = _listeners.GetAt(i);
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
            if (!IsReferenceSlotPayloadCurrent(in payload))
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
            if (!IsReferenceSlotPayloadCurrent(in payload))
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
            if (!IsReferenceSlotPayloadCurrent(in payload))
                return false;

            interactor = _referenceSlots[payload.ReferenceSlot].Interactor;
            return interactor != null;
        }

        /// <summary>
        /// Enqueues a world item collection event.
        /// </summary>
        [Obsolete("Use TryRaiseItemCollected(ItemData,int,Transform) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseItemCollected(ItemData item, int quantity, Transform interactor)
        {
            TryRaiseItemCollected(item, quantity, interactor);
        }

        public static bool TryRaiseItemCollected(ItemData item, int quantity, Transform interactor)
        {
            uint itemHashId = ComputeItemHash(item);
            if (quantity <= 0 || itemHashId == 0u)
            {
                ReportInvalidItemEvent((ushort)InteractionEventType.ItemCollected, itemHashId, quantity);
                return false;
            }

            if (!TryReserveReferenceSlot(InteractionEventType.ItemCollected, out int referenceSlot, out ushort referenceGeneration))
                return false;

            _referenceSlots[referenceSlot].Item = item;
            _referenceSlots[referenceSlot].Target = null;
            _referenceSlots[referenceSlot].Interactor = interactor;

            return Enqueue(new InteractionEventPayload
            {
                ItemHashId = itemHashId,
                TargetHashId = 0u,
                InteractorHashId = ComputeTransformHash(interactor),
                ReferenceSlot = referenceSlot,
                Quantity = quantity,
                EventType = (ushort)InteractionEventType.ItemCollected,
                Reserved = referenceGeneration
            });
        }

        /// <summary>
        /// Enqueues a lost/discarded item event for quest deadlock recovery and other native listeners.
        /// </summary>
        [Obsolete("Use TryRaiseItemLost(ItemData,int,Transform) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseItemLost(ItemData item, int quantity, Transform interactor)
        {
            TryRaiseItemLost(item, quantity, interactor);
        }

        public static bool TryRaiseItemLost(ItemData item, int quantity, Transform interactor)
        {
            uint itemHashId = ComputeItemHash(item);
            if (quantity <= 0 || itemHashId == 0u)
            {
                ReportInvalidItemEvent((ushort)InteractionEventType.ItemLost, itemHashId, quantity);
                return false;
            }

            if (!TryReserveReferenceSlot(InteractionEventType.ItemLost, out int referenceSlot, out ushort referenceGeneration))
                return false;

            _referenceSlots[referenceSlot].Item = item;
            _referenceSlots[referenceSlot].Target = null;
            _referenceSlots[referenceSlot].Interactor = interactor;

            return Enqueue(new InteractionEventPayload
            {
                ItemHashId = itemHashId,
                TargetHashId = 0u,
                InteractorHashId = ComputeTransformHash(interactor),
                ReferenceSlot = referenceSlot,
                Quantity = quantity,
                EventType = (ushort)InteractionEventType.ItemLost,
                Reserved = referenceGeneration
            });
        }

        /// <summary>
        /// Enqueues an interaction-started event.
        /// </summary>
        [Obsolete("Use TryRaiseInteractionStarted(IInteractable,Transform) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseInteractionStarted(IInteractable target, Transform interactor)
        {
            TryRaiseInteractionStarted(target, interactor);
        }

        public static bool TryRaiseInteractionStarted(IInteractable target, Transform interactor)
        {
            if (!TryReserveReferenceSlot(InteractionEventType.InteractionStarted, out int referenceSlot, out ushort referenceGeneration))
                return false;

            _referenceSlots[referenceSlot].Item = null;
            _referenceSlots[referenceSlot].Target = target;
            _referenceSlots[referenceSlot].Interactor = interactor;

            return Enqueue(new InteractionEventPayload
            {
                ItemHashId = 0u,
                TargetHashId = ComputeInteractableHash(target),
                InteractorHashId = ComputeTransformHash(interactor),
                ReferenceSlot = referenceSlot,
                Quantity = 0,
                EventType = (ushort)InteractionEventType.InteractionStarted,
                Reserved = referenceGeneration
            });
        }

        /// <summary>
        /// Enqueues a hover-target change event.
        /// </summary>
        [Obsolete("Use TryRaiseHoverChanged(IInteractable) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseHoverChanged(IInteractable target)
        {
            TryRaiseHoverChanged(target);
        }

        public static bool TryRaiseHoverChanged(IInteractable target)
        {
            int referenceSlot = -1;
            ushort referenceGeneration = 0;
            if (target != null)
            {
                if (!TryReserveReferenceSlot(InteractionEventType.HoverChanged, out referenceSlot, out referenceGeneration))
                    return false;

                _referenceSlots[referenceSlot].Item = null;
                _referenceSlots[referenceSlot].Target = target;
                _referenceSlots[referenceSlot].Interactor = null;
            }

            return Enqueue(new InteractionEventPayload
            {
                ItemHashId = 0u,
                TargetHashId = ComputeInteractableHash(target),
                InteractorHashId = 0u,
                ReferenceSlot = referenceSlot,
                Quantity = 0,
                EventType = (ushort)InteractionEventType.HoverChanged,
                Reserved = referenceGeneration
            });
        }

        private static void EnsureInitialized()
        {
            try
            {
                if (!_pendingEvents.IsCreated)
                {
                    _pendingEvents = new NativeQueue<InteractionEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<InteractionEventPayload>[128] — deferred interaction event lane flushed by SystemDispatcher LateUpdate — owner: InteractionEvents
                    RegisterNativeQueue(ref _pendingEvents, PendingEventCapacity, nameof(_pendingEvents), out _pendingEventsSentinelId);
                    PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
                }

                if (!_nextFrameEvents.IsCreated)
                {
                    _nextFrameEvents = new NativeQueue<InteractionEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<InteractionEventPayload>[128] — next-frame interaction event lane prevents same-frame reentrant dispatch — owner: InteractionEvents
                    RegisterNativeQueue(ref _nextFrameEvents, PendingEventCapacity, nameof(_nextFrameEvents), out _nextFrameEventsSentinelId);
                    PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
                }
            }
            catch
            {
                Exception releaseException = ReleaseNativeQueuesBestEffort();
                ClearReferenceSlots();
                _pendingEventCount = 0;
                _nextFrameEventCount = 0;
                ReportQueueReleaseFailure(releaseException);
                throw;
            }
        }

        private static void RegisterNativeQueue<T>(
            ref NativeQueue<T> queue,
            int capacity,
            string label,
            out int sentinelId)
            where T : unmanaged
        {
            sentinelId = 0;
            sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(
                queue,
                capacity,
                nameof(InteractionEvents),
                label,
                NativeAllocationLifetime.Session);
            if (sentinelId > 0)
                return;

            ReleaseNativeQueue(ref queue, ref sentinelId);
            throw new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static void ReleaseNativeQueues()
        {
            ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);
            ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);
        }

        private static Exception ReleaseNativeQueuesBestEffort()
        {
            Exception firstException = ReleaseNativeQueueBestEffort(ref _pendingEvents, ref _pendingEventsSentinelId);
            Exception nextFrameException = ReleaseNativeQueueBestEffort(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);
            return firstException ?? nextFrameException;
        }

        private static void ReleaseNativeQueue<T>(ref NativeQueue<T> queue, ref int sentinelId)
            where T : unmanaged
        {
            Exception releaseException = ReleaseNativeQueueBestEffort(ref queue, ref sentinelId);
            if (releaseException != null)
                throw releaseException;
        }

        private static Exception ReleaseNativeQueueBestEffort<T>(ref NativeQueue<T> queue, ref int sentinelId)
            where T : unmanaged
        {
            Exception firstException = null;

            if (sentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(sentinelId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    sentinelId = 0;
                }
            }

            if (queue.IsCreated)
            {
                try
                {
                    queue.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    queue = default;
                }
            }
            else
            {
                queue = default;
            }

            return firstException;
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

        private static bool Enqueue(in InteractionEventPayload payload)
        {
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportQueueOverflow(payload.EventType);
                ReleaseReferenceSlot(payload.ReferenceSlot);
                return false;
            }

            try
            {
                EnsureInitialized();
            }
            catch (Exception exception)
            {
                ReleaseReferenceSlot(payload.ReferenceSlot);
                ReportQueueInitializationFailure(payload.EventType, exception);
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

        private static bool TryReserveReferenceSlot(
            InteractionEventType eventType,
            out int referenceSlot,
            out ushort referenceGeneration)
        {
            referenceSlot = -1;
            referenceGeneration = 0;
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
                referenceGeneration = AdvanceReferenceSlotGeneration(referenceSlot);
                _referenceSlotOccupied[referenceSlot] = true;
                _referencePendingCount++;
                return true;
            }

            ReportReferenceSlotExhausted((ushort)eventType);
            return false;
        }

        private static ushort AdvanceReferenceSlotGeneration(int referenceSlot)
        {
            ushort generation = unchecked((ushort)(_referenceSlotGenerations[referenceSlot] + 1));
            if (generation == 0)
                generation = 1;

            _referenceSlotGenerations[referenceSlot] = generation;
            return generation;
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

        private static bool IsReferenceSlotPayloadCurrent(in InteractionEventPayload payload)
        {
            int referenceSlot = payload.ReferenceSlot;
            return IsValidReferenceSlot(referenceSlot) &&
                   _referenceSlotOccupied[referenceSlot] &&
                   payload.Reserved != 0 &&
                   _referenceSlotGenerations[referenceSlot] == payload.Reserved;
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

            NativeQueue<InteractionEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            int sentinelIdSwap = _pendingEventsSentinelId;
            _pendingEventsSentinelId = _nextFrameEventsSentinelId;
            _nextFrameEventsSentinelId = sentinelIdSwap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        private static void DispatchToListener(IInteractionEventListener listener, in InteractionEventPayload payload)
        {
            try
            {
                listener.OnInteractionEvent(in payload);
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
            try
            {
                Hecton8.Core.H8Debug.LogException(exception);
            }
            catch
            {
            }
#endif
        }

        private static void QueueDeferredRegister(IInteractionEventListener listener)
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

        private static void QueueDeferredUnregister(IInteractionEventListener listener)
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

        private static bool CancelDeferredRegister(IInteractionEventListener listener)
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

        private static void CancelDeferredUnregister(IInteractionEventListener listener)
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

        private static bool IsDeferredRegisterPending(IInteractionEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(IInteractionEventListener listener)
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
                IInteractionEventListener listener = _deferredUnregisterListeners[i].Listener;
                _deferredUnregisterListeners[i].Clear();
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                IInteractionEventListener listener = _deferredRegisterListeners[i].Listener;
                _deferredRegisterListeners[i].Clear();
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;
        }

        private static void RegisterImmediate(IInteractionEventListener listener)
        {
            if (_listeners.Contains(listener))
                return;

            if (!_listeners.TryRegister(listener))
                ReportListenerRegistrationOverflow();
        }

        private static void ReportQueueOverflow(ushort eventType)
        {
            _droppedEventCount++;
            int frame = ResolveCurrentFrameIndexSafe();
            if (_lastQueueOverflowTelemetryFrame == frame)
                return;

            _lastQueueOverflowTelemetryFrame = frame;
            PublishPerformanceWarningBestEffort(
                InteractionQueueOverflowWarningHash,
                InteractionQueueContextHash ^ ((uint)eventType << 24),
                Mathf.Max(1, _droppedEventCount));
        }

        private static void ReportInvalidItemEvent(ushort eventType, uint itemHashId, int quantity)
        {
            _droppedInvalidItemEventCount++;
            int frame = ResolveCurrentFrameIndexSafe();
            if (_lastInvalidItemEventTelemetryFrame == frame)
                return;

            _lastInvalidItemEventTelemetryFrame = frame;
            uint contextHash = InteractionInvalidItemEventContextHash ^ ((uint)eventType << 24);
            if (itemHashId == 0u)
                contextHash ^= 1u;
            if (quantity <= 0)
                contextHash ^= 2u;

            PublishPerformanceWarningBestEffort(
                InteractionInvalidItemEventWarningHash,
                contextHash,
                Mathf.Max(1, _droppedInvalidItemEventCount));
        }

        private static void ReportQueueInitializationFailure(ushort eventType, Exception exception)
        {
            _droppedEventCount++;
            int frame = ResolveCurrentFrameIndexSafe();
            if (_lastQueueInitializationFailureTelemetryFrame != frame)
            {
                _lastQueueInitializationFailureTelemetryFrame = frame;
                PublishPerformanceWarningBestEffort(
                    InteractionQueueInitializationFailureWarningHash,
                    InteractionQueueContextHash ^ ((uint)eventType << 24),
                    Mathf.Max(1, _droppedEventCount));
            }

            LogQueueInitializationException(exception);
        }

        private static void ReportQueueReleaseFailure(Exception exception)
        {
            if (exception == null)
                return;

            int frame = ResolveCurrentFrameIndexSafe();
            if (_lastQueueReleaseFailureTelemetryFrame != frame)
            {
                _lastQueueReleaseFailureTelemetryFrame = frame;
                PublishPerformanceWarningBestEffort(
                    InteractionQueueReleaseFailureWarningHash,
                    InteractionQueueContextHash,
                    1f);
            }

            LogQueueInitializationException(exception);
        }

        private static void ReportReferenceSlotExhausted(ushort eventType)
        {
            _droppedReferenceSlotCount++;
            int frame = ResolveCurrentFrameIndexSafe();
            if (_lastReferenceSlotTelemetryFrame == frame)
                return;

            _lastReferenceSlotTelemetryFrame = frame;
            PublishPerformanceWarningBestEffort(
                InteractionReferenceSlotExhaustedWarningHash,
                InteractionReferenceSlotContextHash ^ ((uint)eventType << 24),
                Mathf.Max(1, _droppedReferenceSlotCount));
        }

        private static void ReportListenerRegistrationOverflow()
        {
            _droppedListenerRegistrationCount++;
            int frame = ResolveCurrentFrameIndexSafe();
            if (_lastListenerOverflowTelemetryFrame == frame)
                return;

            _lastListenerOverflowTelemetryFrame = frame;
            PublishPerformanceWarningBestEffort(
                InteractionListenerOverflowWarningHash,
                InteractionListenerContextHash,
                Mathf.Max(1, _droppedListenerRegistrationCount));
        }

        private static void ReportListenerDispatchException()
        {
            _listenerExceptionCount++;
            int frame = ResolveCurrentFrameIndexSafe();
            if (_lastListenerExceptionTelemetryFrame == frame)
                return;

            _lastListenerExceptionTelemetryFrame = frame;
            PublishPerformanceWarningBestEffort(
                InteractionListenerExceptionWarningHash,
                InteractionListenerExceptionContextHash,
                Mathf.Max(1, _listenerExceptionCount));
        }

        private static int ResolveCurrentFrameIndexSafe()
        {
            try
            {
                return SystemDispatcher.CurrentFrameIndex;
            }
            catch
            {
                return -1;
            }
        }

        private static void PublishPerformanceWarningBestEffort(uint warningHash, uint contextHash, float value)
        {
            try
            {
                GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, value);
            }
            catch (Exception telemetryException)
            {
                LogQueueInitializationException(telemetryException);
            }
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        private static void LogQueueInitializationException(Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                Hecton8.Core.H8Debug.LogException(exception);
            }
            catch
            {
            }
#endif
        }

        private static void ClearReferenceSlots()
        {
            for (int i = 0; i < ReferenceSlotCapacity; i++)
            {
                _referenceSlots[i].Clear();
                _referenceSlotOccupied[i] = false;
                AdvanceReferenceSlotGeneration(i);
            }
        }

        private static uint ComputeItemHash(ItemData item)
        {
            return unchecked((uint)ItemData.ResolvePersistentHashId(item));
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
