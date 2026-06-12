using System;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Acoustic payload raised by repair drones while their weld torch is active.
    /// </summary>
    public readonly struct RepairDroneTorchAcousticEvent
    {
        public readonly Vector3 Position;
        public readonly AudioClip Clip;
        public readonly float Volume;
        public readonly float Pitch;

        public RepairDroneTorchAcousticEvent(Vector3 position, AudioClip clip, float volume, float pitch)
        {
            Position = position;
            Clip = clip;
            Volume = volume;
            Pitch = pitch;
        }
    }

    /// <summary>
    /// Unmanaged repair-drone torch acoustic payload carried by the deferred event lane.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct RepairDroneTorchAcousticPayload
    {
        [FieldOffset(0)]
        public float3 Position;
        [FieldOffset(12)]
        public float Volume;
        [FieldOffset(16)]
        public float Pitch;
        [FieldOffset(20)]
        public uint ClipHashId;
        [FieldOffset(24)]
        public int ReferenceSlot;
        [FieldOffset(28)]
        public ushort EventType;
        [FieldOffset(30)]
        public ushort Reserved;
    }

    /// <summary>
    /// Listener for deferred repair-drone torch acoustic pulses.
    /// </summary>
    public interface IRepairDroneTorchAcousticListener
    {
        void OnRepairDroneTorchAcoustic(in RepairDroneTorchAcousticEvent acousticEvent);
    }

    /// <summary>
    /// Vault-backed event bridge that lets the audio owner consume repair-torch pulses without scene scans.
    /// </summary>
    public static class RepairDroneTorchAcousticEvents
    {
        private const int ListenerCapacity = 8;
        private const int PendingEventCapacity = 32;
        private const int ReferenceSlotCapacity = 32;
        private const ushort TorchAcousticEventType = 1;
        private const BufferID PendingEventBufferId = BufferID.RepairDroneTorchAcousticEvents_PendingEventBufferId;
        private const BufferID NextFrameEventBufferId = BufferID.RepairDroneTorchAcousticEvents_NextFrameEventBufferId;
        private static readonly uint _overflowWarningHash = unchecked((uint)LocHash.Compute("RepairDroneTorchAcousticEvents.Overflow"));
        private static readonly uint _queueHash = unchecked((uint)LocHash.Compute("RepairDroneTorchAcousticEvents"));
        private static readonly uint _duplicateListenerWarningHash = unchecked((uint)LocHash.Compute("RepairDroneTorchAcousticEvents.DuplicateListener"));
        private static readonly uint _listenerRejectedWarningHash = unchecked((uint)LocHash.Compute("RepairDroneTorchAcousticEvents.ListenerRejected"));
        private static readonly uint _listenerExceptionWarningHash = unchecked((uint)LocHash.Compute("RepairDroneTorchAcousticEvents.ListenerException"));
        private static readonly uint _listenerHash = unchecked((uint)LocHash.Compute("RepairDroneTorchAcousticEvents.Listener"));

        private struct ListenerSlot
        {
            public IRepairDroneTorchAcousticListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        // COLD ALLOC: ListenerSlot[8] - repair drone torch acoustic listeners drained by SystemDispatcher LateUpdate - owner: RepairDroneTorchAcousticEvents
        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: ListenerSlot[8] - listener additions deferred during acoustic dispatch - owner: RepairDroneTorchAcousticEvents
        private static readonly ListenerSlot[] _deferredRegisterListeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: ListenerSlot[8] - listener removals deferred during acoustic dispatch - owner: RepairDroneTorchAcousticEvents
        private static readonly ListenerSlot[] _deferredUnregisterListeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: AudioClip[32] - managed clip sidecar for deferred repair drone torch acoustic payloads - owner: RepairDroneTorchAcousticEvents
        private static readonly AudioClip[] _clipReferenceSlots = new AudioClip[ReferenceSlotCapacity];
        // COLD ALLOC: bool[32] - clip sidecar occupancy map prevents wrap overwrite before deferred flush - owner: RepairDroneTorchAcousticEvents
        private static readonly bool[] _referenceSlotOccupied = new bool[ReferenceSlotCapacity];
        // COLD ALLOC: ushort[32] - generation tokens reject stale clip sidecar payloads after release/reuse - owner: RepairDroneTorchAcousticEvents
        private static readonly ushort[] _referenceSlotGenerations = new ushort[ReferenceSlotCapacity];
        private static IDataVault _vault;
        private static VaultGenerationHandle<RepairDroneTorchAcousticPayload> _pendingEventsHandle;
        private static VaultGenerationHandle<RepairDroneTorchAcousticPayload> _nextFrameEventsHandle;
        private static int _listenerCount;
        private static int _pendingEventCount;
        private static int _pendingEventReadIndex;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedEventCount;
        private static int _duplicateListenerRegistrationCount;
        private static int _listenerRejectCount;
        private static int _listenerExceptionCount;
        private static int _referenceWriteIndex;
        private static int _referencePendingCount;
        private static int _lastOverflowWarningFrame = -1;
        private static int _lastDuplicateListenerWarningFrame = -1;
        private static int _lastListenerRejectedWarningFrame = -1;
        private static int _lastListenerExceptionWarningFrame = -1;

        /// <summary>Number of repair drone torch acoustic payloads waiting for late-frame dispatch.</summary>
        public static int PendingCount => math.max(0, _pendingEventCount - _pendingEventReadIndex) + _nextFrameEventCount;
        public static int DroppedEventCount => _droppedEventCount;
        public static int DuplicateListenerRegistrationCount => _duplicateListenerRegistrationCount;
        public static int ListenerRejectCount => _listenerRejectCount;
        public static int ListenerExceptionCount => _listenerExceptionCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ReleaseVaultBuffer(ref _pendingEventsHandle);
            ReleaseVaultBuffer(ref _nextFrameEventsHandle);
            _vault = null;

            for (int i = 0; i < _listenerCount; i++)
                _listeners[i].Clear();

            for (int i = 0; i < _deferredRegisterCount; i++)
                _deferredRegisterListeners[i].Clear();

            for (int i = 0; i < _deferredUnregisterCount; i++)
                _deferredUnregisterListeners[i].Clear();

            _listenerCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedEventCount = 0;
            _duplicateListenerRegistrationCount = 0;
            _listenerRejectCount = 0;
            _listenerExceptionCount = 0;
            ClearReferenceSlots();
            _pendingEventCount = 0;
            _pendingEventReadIndex = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
            _referenceWriteIndex = 0;
            _referencePendingCount = 0;
            _lastOverflowWarningFrame = -1;
            _lastDuplicateListenerWarningFrame = -1;
            _lastListenerRejectedWarningFrame = -1;
            _lastListenerExceptionWarningFrame = -1;
        }

        internal static void BindDataVault(IDataVault vault)
        {
            if (ReferenceEquals(_vault, vault))
                return;

            DropQueuedPayloads();
            ReleaseVaultBuffer(ref _pendingEventsHandle);
            ReleaseVaultBuffer(ref _nextFrameEventsHandle);
            _vault = vault;
            if (_listenerCount > 0)
                TryEnsureInitialized();
        }

        /// <summary>Registers one deferred repair-drone torch acoustic listener.</summary>
        public static void Register(IRepairDroneTorchAcousticListener listener)
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

        /// <summary>Unregisters one deferred repair-drone torch acoustic listener.</summary>
        public static void Unregister(IRepairDroneTorchAcousticListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredUnregister(listener);
                return;
            }

            TryUnregisterImmediate(listener);
            if (_listenerCount <= 0)
                DropQueuedPayloads();
        }

        private static void RegisterImmediate(IRepairDroneTorchAcousticListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                {
                    ReportDuplicateListenerRegistration();
                    return;
                }
            }

            if (_listenerCount >= ListenerCapacity)
            {
                ReportListenerRejected();
                return;
            }

            _listeners[_listenerCount++].Listener = listener;
            if (_listenerCount == 1)
                TryEnsureInitialized();
        }

        private static bool TryUnregisterImmediate(IRepairDroneTorchAcousticListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i].Listener, listener))
                    continue;

                int lastIndex = --_listenerCount;
                if (i != lastIndex)
                    _listeners[i].Listener = _listeners[lastIndex].Listener;

                _listeners[lastIndex].Clear();
                return true;
            }

            return false;
        }

        /// <summary>Flushes queued repair-drone torch acoustic payloads.</summary>
        public static void FlushPending()
        {
            if (!IsPayloadVaultHandle(in _pendingEventsHandle))
                return;

            if (_listenerCount <= 0)
            {
                DropQueuedPayloads();
                return;
            }

            PromoteNextFrameEventsIfFrontEmpty();
            if (!TryReadPayloadBuffer(in _pendingEventsHandle, out NativeArray<RepairDroneTorchAcousticPayload>.ReadOnly pendingEvents))
            {
                DropQueuedPayloads();
                return;
            }

            int scanBudget = math.max(0, _pendingEventCount - _pendingEventReadIndex);
            while (scanBudget-- > 0 && _pendingEventReadIndex < _pendingEventCount)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                {
                    if (!CompactPendingEvents())
                        DropQueuedPayloads();

                    return;
                }

                RepairDroneTorchAcousticPayload payload = pendingEvents[_pendingEventReadIndex++];

                Dispatch(in payload);
                ReleaseReferenceSlotForPayload(in payload);
                if (_listenerCount <= 0)
                {
                    DropQueuedPayloads();
                    return;
                }
            }

            if (_pendingEventReadIndex >= _pendingEventCount)
            {
                _pendingEventCount = 0;
                _pendingEventReadIndex = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
            else if (!CompactPendingEvents())
            {
                DropQueuedPayloads();
            }
        }

        /// <summary>Queues one repair-drone torch acoustic pulse.</summary>
        [Obsolete("Use TryNotify(in RepairDroneTorchAcousticEvent) so bounded queue rejection stays visible at the producer.", true)]
        public static void Notify(in RepairDroneTorchAcousticEvent acousticEvent)
        {
            TryNotify(in acousticEvent);
        }

        public static bool TryNotify(in RepairDroneTorchAcousticEvent acousticEvent)
        {
            if (_listenerCount <= 0)
                return false;

            if (acousticEvent.Clip == null)
            {
                RecordDroppedEvent();
                return false;
            }

            if (!TryReserveReferenceSlot(out int referenceSlot, out ushort referenceGeneration))
            {
                RecordDroppedEvent();
                ReportOverflowOncePerFrame();
                return false;
            }

            _clipReferenceSlots[referenceSlot] = acousticEvent.Clip;
            RepairDroneTorchAcousticPayload payload = default;
            payload.Position = math.float3(acousticEvent.Position.x, acousticEvent.Position.y, acousticEvent.Position.z);
            payload.Volume = acousticEvent.Volume;
            payload.Pitch = acousticEvent.Pitch;
            payload.ClipHashId = unchecked((uint)EntityId.ToULong(acousticEvent.Clip.GetEntityId()));
            payload.ReferenceSlot = referenceSlot;
            payload.EventType = TorchAcousticEventType;
            payload.Reserved = referenceGeneration;
            return Enqueue(in payload);
        }

        private static bool TryEnsureInitialized()
        {
            if (!RuntimeLayoutValid())
                return false;

            IDataVault vault = _vault;
            if (vault == null)
                return false;

            return TryEnsurePayloadBuffer(vault, ref _pendingEventsHandle, PendingEventBufferId) &&
                   TryEnsurePayloadBuffer(vault, ref _nextFrameEventsHandle, NextFrameEventBufferId);
        }

        public static bool RuntimeLayoutValid()
        {
            if (UnsafeUtility.SizeOf<RepairDroneTorchAcousticPayload>() != 32)
                return false;

#if UNITY_EDITOR
            return Marshal.OffsetOf<RepairDroneTorchAcousticPayload>(nameof(RepairDroneTorchAcousticPayload.Position)).ToInt32() == 0 &&
                   Marshal.OffsetOf<RepairDroneTorchAcousticPayload>(nameof(RepairDroneTorchAcousticPayload.Volume)).ToInt32() == 12 &&
                   Marshal.OffsetOf<RepairDroneTorchAcousticPayload>(nameof(RepairDroneTorchAcousticPayload.Pitch)).ToInt32() == 16 &&
                   Marshal.OffsetOf<RepairDroneTorchAcousticPayload>(nameof(RepairDroneTorchAcousticPayload.ClipHashId)).ToInt32() == 20 &&
                   Marshal.OffsetOf<RepairDroneTorchAcousticPayload>(nameof(RepairDroneTorchAcousticPayload.ReferenceSlot)).ToInt32() == 24 &&
                   Marshal.OffsetOf<RepairDroneTorchAcousticPayload>(nameof(RepairDroneTorchAcousticPayload.EventType)).ToInt32() == 28 &&
                   Marshal.OffsetOf<RepairDroneTorchAcousticPayload>(nameof(RepairDroneTorchAcousticPayload.Reserved)).ToInt32() == 30;
#else
            return true;
#endif
        }

        private static bool Enqueue(in RepairDroneTorchAcousticPayload payload)
        {
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReleaseReferenceSlotForPayload(in payload);
                RecordDroppedEvent();
                ReportOverflowOncePerFrame();
                return false;
            }

            if (!TryEnsureInitialized())
            {
                ReleaseReferenceSlotForPayload(in payload);
                RecordDroppedEvent();
                ReportOverflowOncePerFrame();
                return false;
            }

            if (_isDispatching)
            {
                if (!TryWritePayload(ref _nextFrameEventsHandle, _nextFrameEventCount, in payload))
                {
                    ReleaseReferenceSlotForPayload(in payload);
                    RecordDroppedEvent();
                    ReportOverflowOncePerFrame();
                    return false;
                }

                _nextFrameEventCount++;
                return true;
            }

            if (!TryWritePayload(ref _pendingEventsHandle, _pendingEventCount, in payload))
            {
                ReleaseReferenceSlotForPayload(in payload);
                RecordDroppedEvent();
                ReportOverflowOncePerFrame();
                return false;
            }

            _pendingEventCount++;
            return true;
        }

        private static void Dispatch(in RepairDroneTorchAcousticPayload payload)
        {
            if (payload.EventType != TorchAcousticEventType ||
                !IsReferenceSlotPayloadCurrent(in payload))
            {
                return;
            }

            AudioClip clip = _clipReferenceSlots[payload.ReferenceSlot];
            if (clip == null)
                return;

            RepairDroneTorchAcousticEvent acousticEvent = new RepairDroneTorchAcousticEvent(
                new Vector3(payload.Position.x, payload.Position.y, payload.Position.z),
                clip,
                payload.Volume,
                payload.Pitch);

            int count = _listenerCount;
            _isDispatching = true;
            try
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    IRepairDroneTorchAcousticListener listener = _listeners[i].Listener;
                    if (listener != null)
                        DispatchToListener(listener, in acousticEvent);
                }
            }
            finally
            {
                _isDispatching = false;
                ApplyDeferredListenerMutations();
            }
        }

        private static void DispatchToListener(
            IRepairDroneTorchAcousticListener listener,
            in RepairDroneTorchAcousticEvent acousticEvent)
        {
            try
            {
                listener.OnRepairDroneTorchAcoustic(in acousticEvent);
            }
            catch (Exception exception)
            {
                ReportListenerDispatchException(exception);
            }
        }

        private static void QueueDeferredRegister(IRepairDroneTorchAcousticListener listener)
        {
            if (CancelDeferredUnregister(listener))
                return;

            if (ContainsImmediate(listener) || IsDeferredRegisterPending(listener))
            {
                ReportDuplicateListenerRegistration();
                return;
            }

            if (_deferredRegisterCount >= ListenerCapacity)
            {
                ReportListenerRejected();
                return;
            }

            _deferredRegisterListeners[_deferredRegisterCount++].Listener = listener;
        }

        private static void QueueDeferredUnregister(IRepairDroneTorchAcousticListener listener)
        {
            if (CancelDeferredRegister(listener))
                return;

            if (!ContainsImmediate(listener) || IsDeferredUnregisterPending(listener))
                return;

            if (_deferredUnregisterCount >= ListenerCapacity)
            {
                ReportListenerRejected();
                return;
            }

            _deferredUnregisterListeners[_deferredUnregisterCount++].Listener = listener;
        }

        private static bool CancelDeferredRegister(IRepairDroneTorchAcousticListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (!ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    continue;

                int lastIndex = --_deferredRegisterCount;
                if (i != lastIndex)
                    _deferredRegisterListeners[i].Listener = _deferredRegisterListeners[lastIndex].Listener;

                _deferredRegisterListeners[lastIndex].Clear();
                return true;
            }

            return false;
        }

        private static bool CancelDeferredUnregister(IRepairDroneTorchAcousticListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (!ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    continue;

                int lastIndex = --_deferredUnregisterCount;
                if (i != lastIndex)
                    _deferredUnregisterListeners[i].Listener = _deferredUnregisterListeners[lastIndex].Listener;

                _deferredUnregisterListeners[lastIndex].Clear();
                return true;
            }

            return false;
        }

        private static bool ContainsImmediate(IRepairDroneTorchAcousticListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredRegisterPending(IRepairDroneTorchAcousticListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(IRepairDroneTorchAcousticListener listener)
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
            int unregisterCount = _deferredUnregisterCount;
            _deferredUnregisterCount = 0;
            for (int i = 0; i < unregisterCount; i++)
            {
                IRepairDroneTorchAcousticListener listener = _deferredUnregisterListeners[i].Listener;
                _deferredUnregisterListeners[i].Clear();
                if (listener != null)
                    TryUnregisterImmediate(listener);
            }

            int registerCount = _deferredRegisterCount;
            _deferredRegisterCount = 0;
            for (int i = 0; i < registerCount; i++)
            {
                IRepairDroneTorchAcousticListener listener = _deferredRegisterListeners[i].Listener;
                _deferredRegisterListeners[i].Clear();
                if (listener != null)
                    RegisterImmediate(listener);
            }

            if (_listenerCount <= 0)
                DropQueuedPayloads();
        }

        private static bool TryReserveReferenceSlot(out int referenceSlot, out ushort referenceGeneration)
        {
            referenceSlot = -1;
            referenceGeneration = 0;
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
                referenceGeneration = AdvanceReferenceSlotGeneration(referenceSlot);
                _referenceSlotOccupied[referenceSlot] = true;
                _referencePendingCount++;
                return true;
            }

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

        private static void ReleaseReferenceSlotForPayload(in RepairDroneTorchAcousticPayload payload)
        {
            if (IsReferenceSlotPayloadCurrent(in payload))
                ReleaseReferenceSlot(payload.ReferenceSlot);
        }

        private static void ReleaseReferenceSlot(int referenceSlot)
        {
            if (!IsValidReferenceSlot(referenceSlot) || !_referenceSlotOccupied[referenceSlot])
                return;

            _clipReferenceSlots[referenceSlot] = null;
            _referenceSlotOccupied[referenceSlot] = false;
            if (_referencePendingCount > 0)
                _referencePendingCount--;
        }

        private static bool IsValidReferenceSlot(int referenceSlot)
        {
            return (uint)referenceSlot < ReferenceSlotCapacity;
        }

        private static bool IsReferenceSlotPayloadCurrent(in RepairDroneTorchAcousticPayload payload)
        {
            int referenceSlot = payload.ReferenceSlot;
            return IsValidReferenceSlot(referenceSlot) &&
                   _referenceSlotOccupied[referenceSlot] &&
                   payload.Reserved != 0 &&
                   _referenceSlotGenerations[referenceSlot] == payload.Reserved;
        }

        private static void ClearReferenceSlots()
        {
            for (int i = 0; i < ReferenceSlotCapacity; i++)
            {
                _clipReferenceSlots[i] = null;
                _referenceSlotOccupied[i] = false;
                AdvanceReferenceSlotGeneration(i);
            }
        }

        private static void DropQueuedPayloads()
        {
            _pendingEventCount = 0;
            _pendingEventReadIndex = 0;
            _nextFrameEventCount = 0;
            ClearReferenceSlots();
            _referenceWriteIndex = 0;
            _referencePendingCount = 0;
        }

        private static void RecordDroppedEvent()
        {
            _droppedEventCount = SaturatingIncrement(_droppedEventCount);
        }

        private static void ReportDuplicateListenerRegistration()
        {
            _duplicateListenerRegistrationCount = SaturatingIncrement(_duplicateListenerRegistrationCount);
            PublishWarningOncePerFrame(
                _duplicateListenerWarningHash,
                _listenerHash,
                _duplicateListenerRegistrationCount,
                ref _lastDuplicateListenerWarningFrame);
        }

        private static void ReportListenerRejected()
        {
            _listenerRejectCount = SaturatingIncrement(_listenerRejectCount);
            PublishWarningOncePerFrame(
                _listenerRejectedWarningHash,
                _listenerHash,
                _listenerRejectCount,
                ref _lastListenerRejectedWarningFrame);
        }

        private static void ReportListenerDispatchException(Exception exception)
        {
            _listenerExceptionCount = SaturatingIncrement(_listenerExceptionCount);
            PublishWarningOncePerFrame(
                _listenerExceptionWarningHash,
                _listenerHash,
                _listenerExceptionCount,
                ref _lastListenerExceptionWarningFrame);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.LogException(exception);
#endif
        }

        private static void ReportOverflowOncePerFrame()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastOverflowWarningFrame == frame)
                return;

            _lastOverflowWarningFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(_overflowWarningHash, _queueHash, PendingEventCapacity);
        }

        private static void PublishWarningOncePerFrame(
            uint warningHash,
            uint contextHash,
            int count,
            ref int lastFrame)
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (lastFrame == frame)
                return;

            lastFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, math.max(1, count));
        }

        private static int SaturatingIncrement(int value)
        {
            return value == int.MaxValue ? int.MaxValue : value + 1;
        }

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!IsPayloadVaultHandle(in _pendingEventsHandle) ||
                !IsPayloadVaultHandle(in _nextFrameEventsHandle) ||
                _pendingEventCount > 0 ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            VaultGenerationHandle<RepairDroneTorchAcousticPayload> swap = _pendingEventsHandle;
            _pendingEventsHandle = _nextFrameEventsHandle;
            _nextFrameEventsHandle = swap;
            _pendingEventCount = _nextFrameEventCount;
            _pendingEventReadIndex = 0;
            _nextFrameEventCount = 0;
        }

        private static bool CompactPendingEvents()
        {
            if (_pendingEventReadIndex <= 0)
                return true;

            int liveCount = math.max(0, _pendingEventCount - _pendingEventReadIndex);
            if (liveCount <= 0)
            {
                _pendingEventCount = 0;
                _pendingEventReadIndex = 0;
                return true;
            }

            IDataVault vault = _vault;
            if (vault == null ||
                !IsPayloadVaultHandle(in _pendingEventsHandle) ||
                !vault.TryAcquireWriteLock(in _pendingEventsHandle, SystemID.Construction, out NativeArray<RepairDroneTorchAcousticPayload> buffer))
            {
                return false;
            }

            try
            {
                if (!buffer.IsCreated || _pendingEventCount > buffer.Length)
                    return false;

                for (int i = 0; i < liveCount; i++)
                    buffer[i] = buffer[_pendingEventReadIndex + i];

                _pendingEventCount = liveCount;
                _pendingEventReadIndex = 0;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _pendingEventsHandle, SystemID.Construction);
            }
        }

        private static bool TryEnsurePayloadBuffer(
            IDataVault vault,
            ref VaultGenerationHandle<RepairDroneTorchAcousticPayload> handle,
            BufferID defaultBufferId)
        {
            if (vault == null)
                return false;

            if (TryReadPayloadBuffer(vault, in handle, out _))
                return true;

            BufferID bufferId = ResolvePayloadBufferId(in handle, defaultBufferId);
            if (vault.TryGetGenerationHandle<RepairDroneTorchAcousticPayload>(bufferId, out VaultGenerationHandle<RepairDroneTorchAcousticPayload> existingHandle) &&
                IsPayloadVaultHandle(in existingHandle))
            {
                handle = existingHandle;
                if (TryReadPayloadBuffer(vault, in handle, out _))
                    return true;
            }

            handle = vault.EnsureGenerationHandle<RepairDroneTorchAcousticPayload>(
                bufferId,
                PendingEventCapacity,
                SystemID.Construction,
                NativeArrayOptions.ClearMemory);

            return TryReadPayloadBuffer(vault, in handle, out _);
        }

        private static bool TryWritePayload(
            ref VaultGenerationHandle<RepairDroneTorchAcousticPayload> handle,
            int index,
            in RepairDroneTorchAcousticPayload payload)
        {
            IDataVault vault = _vault;
            if (vault == null ||
                !IsPayloadVaultHandle(in handle) ||
                index < 0 ||
                !vault.TryAcquireWriteLock(in handle, SystemID.Construction, out NativeArray<RepairDroneTorchAcousticPayload> buffer))
            {
                return false;
            }

            try
            {
                if (!buffer.IsCreated || index >= buffer.Length)
                    return false;

                buffer[index] = payload;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.Construction);
            }
        }

        private static bool TryReadPayloadBuffer(
            in VaultGenerationHandle<RepairDroneTorchAcousticPayload> handle,
            out NativeArray<RepairDroneTorchAcousticPayload>.ReadOnly buffer)
        {
            return TryReadPayloadBuffer(_vault, in handle, out buffer);
        }

        private static bool TryReadPayloadBuffer(
            IDataVault vault,
            in VaultGenerationHandle<RepairDroneTorchAcousticPayload> handle,
            out NativeArray<RepairDroneTorchAcousticPayload>.ReadOnly buffer)
        {
            buffer = default;
            return vault != null &&
                   IsPayloadVaultHandle(in handle) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.Length >= PendingEventCapacity;
        }

        private static void ReleaseVaultBuffer(ref VaultGenerationHandle<RepairDroneTorchAcousticPayload> handle)
        {
            IDataVault vault = _vault;
            if (vault != null && IsPayloadVaultHandle(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static BufferID ResolvePayloadBufferId(
            in VaultGenerationHandle<RepairDroneTorchAcousticPayload> handle,
            BufferID defaultBufferId)
        {
            if (IsPayloadVaultHandle(in handle))
                return (BufferID)(int)handle.BufferID;

            return defaultBufferId;
        }

        private static bool IsPayloadVaultHandle(in VaultGenerationHandle<RepairDroneTorchAcousticPayload> handle)
        {
            return (handle.BufferID == unchecked((uint)(int)PendingEventBufferId) ||
                    handle.BufferID == unchecked((uint)(int)NextFrameEventBufferId)) &&
                   handle.SystemID == (uint)SystemID.Construction &&
                   handle.Generation != 0u;
        }
    }
}
