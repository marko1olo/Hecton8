// ============================================================================
// HECTON-8 - BaseAirlockEvents.cs
// NativeQueue-backed airlock transition lane flushed by SystemDispatcher.
// ============================================================================

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Airlock event discriminator for <see cref="BaseAirlockEventPayload"/>.
    /// </summary>
    public enum BaseAirlockEventType : byte
    {
        CycleStarted = 0,
        CycleCompleted = 1,
        EnvironmentChanged = 2,
        EmergencyLockdownChanged = 3,
        ManualOverrideBlockedChanged = 4,
        ManualOverrideCompleted = 5
    }

    /// <summary>
    /// Unmanaged payload emitted by <see cref="BaseAirlockEvents"/>.
    /// Managed references are available only through sidecar resolution during dispatch.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BaseAirlockEventPayload
    {
        public const uint EventTypeMask = 0x000000FFu;
        public const uint DryFlag = 1u << 8;
        public const uint LockedDownFlag = 1u << 9;
        public const uint OverrideBlockedFlag = 1u << 10;

        [FieldOffset(0)] public uint AirlockHashId;
        [FieldOffset(4)] public uint InteractorHashId;
        [FieldOffset(8)] public float WeldProgress01;
        [FieldOffset(12)] public int ReferenceSlot;
        [FieldOffset(16)] public uint StatusFlags;
        [FieldOffset(20)] public uint Reserved0;
        [FieldOffset(24)] public uint Reserved1;
        [FieldOffset(28)] public uint Reserved2;

        public static BaseAirlockEventType GetEventType(uint statusFlags)
        {
            return (BaseAirlockEventType)(statusFlags & EventTypeMask);
        }

        public static bool IsDry(uint statusFlags)
        {
            return (statusFlags & DryFlag) != 0u;
        }

        public static bool IsLockedDown(uint statusFlags)
        {
            return (statusFlags & LockedDownFlag) != 0u;
        }

        public static bool IsOverrideBlocked(uint statusFlags)
        {
            return (statusFlags & OverrideBlockedFlag) != 0u;
        }

        public static uint BuildStatusFlags(BaseAirlockEventType eventType, bool isDry, bool lockedDown, bool overrideBlocked)
        {
            uint flags = (uint)eventType & EventTypeMask;
            if (isDry)
                flags |= DryFlag;
            if (lockedDown)
                flags |= LockedDownFlag;
            if (overrideBlocked)
                flags |= OverrideBlockedFlag;

            return flags;
        }
    }

    /// <summary>
    /// Listener contract for deferred base-airlock events.
    /// </summary>
    public interface IBaseAirlockEventListener
    {
        /// <summary>
        /// Receives one airlock event during <see cref="SystemDispatcher"/> late-frame dispatch.
        /// </summary>
        /// <param name="payload">Blittable airlock payload.</param>
        void OnBaseAirlockEvent(in BaseAirlockEventPayload payload);
    }

    /// <summary>
    /// Queue-backed bus for base-airlock transition, lockdown, and override state.
    /// </summary>
    public static class BaseAirlockEvents
    {
        private const int ListenerCapacity = 16;
        private const int PendingEventCapacity = 32;
        private const int ReferenceSlotCapacity = 32;

        private struct AirlockReferenceSlot
        {
            public BaseAirlock Airlock;
            public Transform Interactor;

            public void Clear()
            {
                Airlock = null;
                Interactor = null;
            }
        }

        // COLD ALLOC: RegistryBucket<IBaseAirlockEventListener>[16] - airlock listeners drained by SystemDispatcher LateUpdate - owner: BaseAirlockEvents
        private static readonly RegistryBucket<IBaseAirlockEventListener> _listeners = new RegistryBucket<IBaseAirlockEventListener>(ListenerCapacity);
        // COLD ALLOC: AirlockReferenceSlot[32] - managed airlock/interactor sidecar for unmanaged payloads - owner: BaseAirlockEvents
        private static readonly AirlockReferenceSlot[] _referenceSlots = new AirlockReferenceSlot[ReferenceSlotCapacity];
        // COLD ALLOC: bool[32] - sidecar occupancy map prevents wrap overwrite before deferred dispatch - owner: BaseAirlockEvents
        private static readonly bool[] _referenceSlotOccupied = new bool[ReferenceSlotCapacity];

        private static NativeQueue<BaseAirlockEventPayload> _pendingEvents;
        private static NativeQueue<BaseAirlockEventPayload> _nextFrameEvents;
        private static int _referenceWriteIndex;
        private static int _referencePendingCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;

        /// <summary>
        /// Pending airlock payload count across front and next-frame queues.
        /// </summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        /// <summary>
        /// Allocates and primes native queue storage during dispatcher initialization.
        /// </summary>
        public static void Prewarm()
        {
            EnsureInitialized();
            PrimeQueueStorage(ref _pendingEvents);
            PrimeQueueStorage(ref _nextFrameEvents);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(BaseAirlockEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(BaseAirlockEvents), nameof(_nextFrameEvents));
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
        /// Registers one listener for deferred airlock events.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Register(IBaseAirlockEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        /// <summary>
        /// Unregisters one listener from deferred airlock events.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Unregister(IBaseAirlockEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        /// <summary>
        /// Reports an editor/development error if a listener remains registered after teardown.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        /// <param name="ownerName">Human-readable owner name.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void AssertUnregistered(IBaseAirlockEventListener listener, string ownerName)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (listener == null || !_listeners.Contains(listener))
                return;

            Debug.LogError($"[BaseAirlockEvents] {ownerName} was destroyed while still registered as an IBaseAirlockEventListener.");
#endif
        }

        /// <summary>
        /// Queues an airlock cycle-started payload.
        /// </summary>
        public static void RaiseCycleStarted(BaseAirlock airlock, Transform interactor)
        {
            Enqueue(BaseAirlockEventType.CycleStarted, airlock, interactor);
        }

        /// <summary>
        /// Queues an airlock cycle-completed payload.
        /// </summary>
        public static void RaiseCycleCompleted(BaseAirlock airlock, Transform interactor)
        {
            Enqueue(BaseAirlockEventType.CycleCompleted, airlock, interactor);
        }

        /// <summary>
        /// Queues an airlock dry/wet environment-change payload.
        /// </summary>
        public static void RaiseEnvironmentChanged(BaseAirlock airlock, Transform interactor)
        {
            Enqueue(BaseAirlockEventType.EnvironmentChanged, airlock, interactor);
        }

        /// <summary>
        /// Queues a lockdown-state change payload.
        /// </summary>
        public static void RaiseEmergencyLockdownChanged(BaseAirlock airlock)
        {
            Enqueue(BaseAirlockEventType.EmergencyLockdownChanged, airlock, null);
        }

        /// <summary>
        /// Queues a manual override blocked/unblocked payload.
        /// </summary>
        public static void RaiseManualOverrideBlockedChanged(BaseAirlock airlock)
        {
            Enqueue(BaseAirlockEventType.ManualOverrideBlockedChanged, airlock, null);
        }

        /// <summary>
        /// Queues a completed emergency manual-override payload.
        /// </summary>
        public static void RaiseManualOverrideCompleted(BaseAirlock airlock)
        {
            Enqueue(BaseAirlockEventType.ManualOverrideCompleted, airlock, null);
        }

        /// <summary>
        /// Flushes pending airlock events to registered listeners.
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

                if (!_pendingEvents.TryDequeue(out BaseAirlockEventPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                IBaseAirlockEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IBaseAirlockEventListener listener = rawArray[i];
                        if (listener != null)
                            listener.OnBaseAirlockEvent(in payload);
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
        /// Resolves the airlock reference attached to a queued payload.
        /// Valid only during listener dispatch.
        /// </summary>
        public static bool TryResolveAirlock(in BaseAirlockEventPayload payload, out BaseAirlock airlock)
        {
            airlock = null;
            if (!IsValidReferenceSlot(payload.ReferenceSlot))
                return false;

            airlock = _referenceSlots[payload.ReferenceSlot].Airlock;
            return airlock != null;
        }

        /// <summary>
        /// Resolves the interactor reference attached to a queued payload.
        /// Valid only during listener dispatch.
        /// </summary>
        public static bool TryResolveInteractor(in BaseAirlockEventPayload payload, out Transform interactor)
        {
            interactor = null;
            if (!IsValidReferenceSlot(payload.ReferenceSlot))
                return false;

            interactor = _referenceSlots[payload.ReferenceSlot].Interactor;
            return interactor != null;
        }

        private static void Enqueue(BaseAirlockEventType eventType, BaseAirlock airlock, Transform interactor)
        {
            if (airlock == null)
                return;

            if (!TryReserveReferenceSlot(out int referenceSlot))
                return;

            _referenceSlots[referenceSlot].Airlock = airlock;
            _referenceSlots[referenceSlot].Interactor = interactor;

            Enqueue(new BaseAirlockEventPayload
            {
                AirlockHashId = ComputeReferenceHash(airlock),
                InteractorHashId = ComputeReferenceHash(interactor),
                WeldProgress01 = airlock.WeldOverrideProgress01,
                ReferenceSlot = referenceSlot,
                StatusFlags = BaseAirlockEventPayload.BuildStatusFlags(
                    eventType,
                    airlock.IsPlayerInside,
                    airlock.IsEmergencyLockedDown,
                    airlock.IsManualOverrideBlocked),
                Reserved0 = 0u,
                Reserved1 = 0u,
                Reserved2 = 0u
            });
        }

        private static void Enqueue(in BaseAirlockEventPayload payload)
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

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<BaseAirlockEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<BaseAirlockEventPayload>[32] - deferred airlock event lane flushed by SystemDispatcher LateUpdate - owner: BaseAirlockEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(BaseAirlockEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<BaseAirlockEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<BaseAirlockEventPayload>[32] - next-frame airlock event lane prevents same-frame reentrant dispatch - owner: BaseAirlockEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(BaseAirlockEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
            }
        }

        private static void PrimeQueueStorage(ref NativeQueue<BaseAirlockEventPayload> queue)
        {
            if (!queue.IsCreated || !queue.IsEmpty())
                return;

            for (int i = 0; i < PendingEventCapacity; i++)
                queue.Enqueue(default);

            for (int i = 0; i < PendingEventCapacity; i++)
            {
                if (!queue.TryDequeue(out BaseAirlockEventPayload ignored))
                    break;
            }
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

        private static void ClearReferenceSlots()
        {
            for (int i = 0; i < ReferenceSlotCapacity; i++)
            {
                _referenceSlots[i].Clear();
                _referenceSlotOccupied[i] = false;
            }
        }

        private static void DrainWithoutDispatch()
        {
            if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                return;

            if (_pendingEventCount <= 0)
            {
                PromoteNextFrameEventsIfFrontEmpty();
                if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                    return;
            }

            if (_nextFrameEvents.IsCreated)
                DrainQueueWithoutDispatch(ref _nextFrameEvents, ref _nextFrameEventCount);
        }

        private static bool DrainQueueWithoutDispatch(
            ref NativeQueue<BaseAirlockEventPayload> queue,
            ref int pendingCount)
        {
            if (!queue.IsCreated)
                return true;

            int scanBudget = pendingCount > 0 ? pendingCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !queue.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!queue.TryDequeue(out BaseAirlockEventPayload payload))
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
                _pendingEventCount > 0 ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            NativeQueue<BaseAirlockEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        private static uint ComputeReferenceHash(object reference)
        {
            return reference != null ? unchecked((uint)RuntimeHelpers.GetHashCode(reference)) : 0u;
        }
    }
}
