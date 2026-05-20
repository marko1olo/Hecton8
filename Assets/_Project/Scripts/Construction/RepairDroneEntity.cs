using System;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Acoustic payload raised by repair drones while their weld torch is active.
    /// </summary>
    public readonly struct RepairDroneTorchAcousticEvent
    {
        public RepairDroneTorchAcousticEvent(Vector3 position, AudioClip clip, float volume, float pitch)
        {
            Position = position;
            Clip = clip;
            Volume = volume;
            Pitch = pitch;
        }

        public Vector3 Position { get; }
        public AudioClip Clip { get; }
        public float Volume { get; }
        public float Pitch { get; }
    }

    /// <summary>
    /// Unmanaged repair-drone torch acoustic payload carried by the deferred event lane.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct RepairDroneTorchAcousticPayload
    {
        [FieldOffset(0)]
        public Vector3 Position;
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
    /// NativeQueue-backed event bridge that lets the audio owner consume repair-torch pulses without scene scans.
    /// </summary>
    public static class RepairDroneTorchAcousticEvents
    {
        private const int ListenerCapacity = 8;
        private const int PendingEventCapacity = 32;
        private const int ReferenceSlotCapacity = 32;
        private const ushort TorchAcousticEventType = 1;
        private static readonly uint _overflowWarningHash = unchecked((uint)LocHash.Compute("RepairDroneTorchAcousticEvents.Overflow"));
        private static readonly uint _queueHash = unchecked((uint)LocHash.Compute("RepairDroneTorchAcousticEvents"));

        // COLD ALLOC: RegistryBucket<IRepairDroneTorchAcousticListener>[8] - repair drone torch acoustic listeners drained by SystemDispatcher LateUpdate - owner: RepairDroneTorchAcousticEvents
        private static readonly RegistryBucket<IRepairDroneTorchAcousticListener> _listeners = new RegistryBucket<IRepairDroneTorchAcousticListener>(ListenerCapacity);
        // COLD ALLOC: AudioClip[32] - managed clip sidecar for deferred repair drone torch acoustic payloads - owner: RepairDroneTorchAcousticEvents
        private static readonly AudioClip[] _clipReferenceSlots = new AudioClip[ReferenceSlotCapacity];
        // COLD ALLOC: bool[32] - clip sidecar occupancy map prevents wrap overwrite before deferred flush - owner: RepairDroneTorchAcousticEvents
        private static readonly bool[] _referenceSlotOccupied = new bool[ReferenceSlotCapacity];
        private static NativeQueue<RepairDroneTorchAcousticPayload> _pendingEvents;
        private static NativeQueue<RepairDroneTorchAcousticPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;
        private static int _referenceWriteIndex;
        private static int _referencePendingCount;
        private static int _lastOverflowWarningFrame = -1;

        /// <summary>Number of repair drone torch acoustic payloads waiting for late-frame dispatch.</summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(RepairDroneTorchAcousticEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(RepairDroneTorchAcousticEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            ClearReferenceSlots();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
            _referenceWriteIndex = 0;
            _referencePendingCount = 0;
            _lastOverflowWarningFrame = -1;
        }

        /// <summary>Registers one deferred repair-drone torch acoustic listener.</summary>
        public static void Register(IRepairDroneTorchAcousticListener listener)
        {
            if (listener == null)
                return;

            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        /// <summary>Unregisters one deferred repair-drone torch acoustic listener.</summary>
        public static void Unregister(IRepairDroneTorchAcousticListener listener)
        {
            if (listener == null)
                return;

            if (!_listeners.Contains(listener))
                return;

            _listeners.Unregister(listener);
            if (_listeners.Count <= 0)
                DropQueuedPayloads();
        }

        /// <summary>Flushes queued repair-drone torch acoustic payloads.</summary>
        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            if (_listeners.Count <= 0)
            {
                DropQueuedPayloads();
                return;
            }

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out RepairDroneTorchAcousticPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                Dispatch(in payload);
                ReleaseReferenceSlot(payload.ReferenceSlot);
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        /// <summary>Queues one repair-drone torch acoustic pulse.</summary>
        public static void Notify(in RepairDroneTorchAcousticEvent acousticEvent)
        {
            if (_listeners.Count <= 0 || acousticEvent.Clip == null)
                return;

            if (!TryReserveReferenceSlot(out int referenceSlot))
            {
                ReportOverflowOncePerFrame();
                return;
            }

            _clipReferenceSlots[referenceSlot] = acousticEvent.Clip;
            Enqueue(new RepairDroneTorchAcousticPayload
            {
                Position = acousticEvent.Position,
                Volume = acousticEvent.Volume,
                Pitch = acousticEvent.Pitch,
                ClipHashId = unchecked((uint)EntityId.ToULong(acousticEvent.Clip.GetEntityId())),
                ReferenceSlot = referenceSlot,
                EventType = TorchAcousticEventType,
                Reserved = 0
            });
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<RepairDroneTorchAcousticPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<RepairDroneTorchAcousticPayload>[32] - deferred repair drone torch acoustic lane flushed by SystemDispatcher LateUpdate - owner: RepairDroneTorchAcousticEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(RepairDroneTorchAcousticEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<RepairDroneTorchAcousticPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<RepairDroneTorchAcousticPayload>[32] - next-frame repair drone torch acoustic lane prevents same-frame reentrant dispatch - owner: RepairDroneTorchAcousticEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(RepairDroneTorchAcousticEvents),
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

        private static bool Enqueue(in RepairDroneTorchAcousticPayload payload)
        {
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReleaseReferenceSlot(payload.ReferenceSlot);
                ReportOverflowOncePerFrame();
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

        private static void Dispatch(in RepairDroneTorchAcousticPayload payload)
        {
            if (payload.EventType != TorchAcousticEventType ||
                !IsValidReferenceSlot(payload.ReferenceSlot))
            {
                return;
            }

            AudioClip clip = _clipReferenceSlots[payload.ReferenceSlot];
            if (clip == null)
                return;

            RepairDroneTorchAcousticEvent acousticEvent = new RepairDroneTorchAcousticEvent(
                payload.Position,
                clip,
                payload.Volume,
                payload.Pitch);

            IRepairDroneTorchAcousticListener[] rawArray = _listeners.RawArray;
            int count = _listeners.Count;
            _isDispatching = true;
            try
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    IRepairDroneTorchAcousticListener listener = rawArray[i];
                    if (listener != null)
                        listener.OnRepairDroneTorchAcoustic(in acousticEvent);
                }
            }
            finally
            {
                _isDispatching = false;
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

        private static void ClearReferenceSlots()
        {
            for (int i = 0; i < ReferenceSlotCapacity; i++)
            {
                _clipReferenceSlots[i] = null;
                _referenceSlotOccupied[i] = false;
            }
        }

        private static void DropQueuedPayloads()
        {
            if (_pendingEvents.IsCreated)
            {
                while (_pendingEvents.TryDequeue(out _))
                {
                }
            }

            if (_nextFrameEvents.IsCreated)
            {
                while (_nextFrameEvents.TryDequeue(out _))
                {
                }
            }

            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            ClearReferenceSlots();
            _referenceWriteIndex = 0;
            _referencePendingCount = 0;
        }

        private static void ReportOverflowOncePerFrame()
        {
            int frame = Time.frameCount;
            if (_lastOverflowWarningFrame == frame)
                return;

            _lastOverflowWarningFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(_overflowWarningHash, _queueHash, PendingEventCapacity);
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

            NativeQueue<RepairDroneTorchAcousticPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }

    /// <summary>
    /// Retired source-name marker. Runtime drones now live exclusively in DroneFleetManager native state.
    /// </summary>
    [Obsolete("RepairDroneEntity MonoBehaviour is retired. Use DroneFleetManager headless native state.", true)]
    public sealed class RepairDroneEntity
    {
        private RepairDroneEntity()
        {
        }
    }
}
