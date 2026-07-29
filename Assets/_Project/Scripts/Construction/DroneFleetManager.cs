using Hecton.Localization;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Generated;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Power;
using Hecton8.SaveSystem;
using Hecton8.Vehicles.Automation;
using Hecton8.World;
using System;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Construction
{
    internal enum DroneFleetTaskKind : byte
    {
        None = 0,
        RepairModule = 1,
        CutParasite = 2,
        MineNode = 3
    }

    internal readonly struct DroneFleetTask
    {
        public readonly DroneFleetTaskKind Kind;
        public readonly BaseModule Module;
        public readonly Vector3 Position;
        public readonly float Radius;

        public DroneFleetTask(DroneFleetTaskKind kind, BaseModule module, Vector3 position, float radius)
        {
            Kind = kind;
            Module = module;
            Position = position;
            Radius = radius;
        }

        public bool IsValid()
        {
            return Kind != DroneFleetTaskKind.None && Module != null;
        }
    }

    /// <summary>
    /// Read-only fleet snapshot consumed by diagnostics owners such as the submarine OS.
    /// </summary>
    public readonly struct HectonDroneFleetSnapshot
    {
        public readonly int ActiveHubCount;
        public readonly int ActiveDroneCount;
        public readonly int AssignedTaskCount;
        public readonly int DockedStasisSlotCount;
        public readonly int DestroyedDroneCount;
        public readonly byte EmergencyOverclockActive;
        public readonly SubmarineEmergencyLevel EmergencyLevel;
        public readonly float AverageBatteryPercent;
        public readonly int SolderReserve;
        public readonly int HostileDroneCount;
        public readonly int LogicLeechHijackCount;

        public HectonDroneFleetSnapshot(
            int activeHubCount,
            int activeDroneCount,
            int assignedTaskCount,
            int dockedStasisSlotCount,
            int destroyedDroneCount,
            bool emergencyOverclockActive,
            SubmarineEmergencyLevel emergencyLevel,
            float averageBatteryPercent,
            int solderReserve,
            int hostileDroneCount,
            int logicLeechHijackCount)
        {
            ActiveHubCount = activeHubCount;
            ActiveDroneCount = activeDroneCount;
            AssignedTaskCount = assignedTaskCount;
            DockedStasisSlotCount = dockedStasisSlotCount;
            DestroyedDroneCount = destroyedDroneCount;
            EmergencyOverclockActive = emergencyOverclockActive ? (byte)1 : (byte)0;
            EmergencyLevel = emergencyLevel;
            AverageBatteryPercent = averageBatteryPercent;
            SolderReserve = solderReserve;
            HostileDroneCount = hostileDroneCount;
            LogicLeechHijackCount = logicLeechHijackCount;
        }
    }

    /// <summary>
    /// Burst-accumulated fleet status payload published to the global telemetry ring and OS bridge.
    /// </summary>
    public readonly struct FleetStatusSnapshot
    {
        public readonly int TotalActive;
        public readonly float AverageBattery;
        public readonly int SolderReserve;
        public readonly int LostUnits;
        public readonly int HostileUnits;

        public FleetStatusSnapshot(int totalActive, float averageBattery, int solderReserve, int lostUnits, int hostileUnits)
        {
            TotalActive = totalActive;
            AverageBattery = averageBattery;
            SolderReserve = solderReserve;
            LostUnits = lostUnits;
            HostileUnits = hostileUnits;
        }
    }

    /// <summary>
    /// Fleet telemetry bridge. The submarine OS and any diegetic diagnostics can subscribe without scene scans.
    /// </summary>
    public interface IDroneFleetSnapshotEventListener
    {
        /// <summary>
        /// Receives one late-frame drone fleet snapshot update.
        /// </summary>
        /// <param name="snapshot">Read-only fleet snapshot.</param>
        void OnDroneFleetSnapshotUpdated(in HectonDroneFleetSnapshot snapshot);
    }

    /// <summary>
    /// Blittable snapshot payload queued before dispatch to fleet snapshot listeners.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct HectonDroneFleetSnapshotPayload
    {
        [FieldOffset(0)]
        public int ActiveHubCount;
        [FieldOffset(4)]
        public int ActiveDroneCount;
        [FieldOffset(8)]
        public int AssignedTaskCount;
        [FieldOffset(12)]
        public int DockedStasisSlotCount;
        [FieldOffset(16)]
        public int DestroyedDroneCount;
        [FieldOffset(20)]
        public int EmergencyLevel;
        [FieldOffset(24)]
        public float AverageBatteryPercent;
        [FieldOffset(28)]
        public int SolderReserve;
        [FieldOffset(32)]
        public int HostileDroneCount;
        [FieldOffset(36)]
        public int LogicLeechHijackCount;
        [FieldOffset(40)]
        public byte EmergencyOverclockActive;
        [FieldOffset(41)]
        private byte _padding0;
        [FieldOffset(42)]
        private byte _padding1;
        [FieldOffset(43)]
        private byte _padding2;
        [FieldOffset(44)]
        private byte _padding3;
        [FieldOffset(45)]
        private byte _padding4;
        [FieldOffset(46)]
        private byte _padding5;
        [FieldOffset(47)]
        private byte _padding6;
    }

    /// <summary>
    /// Vault-array-backed fleet telemetry bridge drained by <see cref="SystemDispatcher"/>.
    /// </summary>
    public static class HectonDroneFleetEvents
    {
        private const int ListenerCapacity = 8;
        private const int PendingEventCapacity = 64;
        private const BufferID PendingEventBufferId = BufferID.DroneFleetManager_PendingEventBufferId;
        private const BufferID NextFrameEventBufferId = BufferID.DroneFleetManager_NextFrameEventBufferId;
        private static readonly ulong PendingEventMutationGuardMask = SnapshotMutationGuardBit(PendingEventBufferId);
        private static readonly ulong NextFrameEventMutationGuardMask = SnapshotMutationGuardBit(NextFrameEventBufferId);

        private static readonly uint _overflowWarningHash = unchecked((uint)LocHash.Compute("HectonDroneFleetEvents.Overflow"));
        private static readonly uint _queueHash = unchecked((uint)LocHash.Compute("HectonDroneFleetEvents"));

        private struct ListenerSlot
        {
            public IDroneFleetSnapshotEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        // COLD ALLOC: ListenerSlot[8] - fleet snapshot listeners drained by SystemDispatcher LateUpdate - owner: HectonDroneFleetEvents
        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity];

        private static IDataVault _vault;
        private static VaultGenerationHandle<HectonDroneFleetSnapshotPayload> _pendingEventsHandle;
        private static VaultGenerationHandle<HectonDroneFleetSnapshotPayload> _nextFrameEventsHandle;
        private static int _listenerCount;
        private static int _pendingEventCount;
        private static int _pendingEventReadIndex;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;
        private static int _lastOverflowWarningFrame = -1;

        /// <summary>
        /// Number of pending fleet snapshot payloads waiting for late-frame dispatch.
        /// </summary>
        public static int PendingCount => math.max(0, _pendingEventCount - _pendingEventReadIndex) + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ReleaseSnapshotVaultBuffer(ref _pendingEventsHandle);
            ReleaseSnapshotVaultBuffer(ref _nextFrameEventsHandle);
            _vault = null;

            for (int i = 0; i < _listenerCount; i++)
                _listeners[i].Clear();

            _listenerCount = 0;
            _pendingEventCount = 0;
            _pendingEventReadIndex = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
            _lastOverflowWarningFrame = -1;
        }

        internal static void BindDataVault(IDataVault vault)
        {
            if (ReferenceEquals(_vault, vault))
                return;

            DropQueuedPayloads();
            ReleaseSnapshotVaultBuffer(ref _pendingEventsHandle);
            ReleaseSnapshotVaultBuffer(ref _nextFrameEventsHandle);
            _vault = vault;
            if (_listenerCount > 0)
                TryEnsureInitialized();
        }

        /// <summary>
        /// Registers a fleet snapshot listener.
        /// </summary>
        public static void Register(IDroneFleetSnapshotEventListener listener)
        {
            if (listener == null)
                return;

            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return;
            }

            if (_listenerCount >= ListenerCapacity)
                return;

            _listeners[_listenerCount++].Listener = listener;
            if (_listenerCount == 1)
                TryEnsureInitialized();
        }

        /// <summary>
        /// Unregisters a fleet snapshot listener.
        /// </summary>
        public static void Unregister(IDroneFleetSnapshotEventListener listener)
        {
            if (listener == null)
                return;

            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i].Listener, listener))
                    continue;

                int lastIndex = --_listenerCount;
                if (i != lastIndex)
                    _listeners[i].Listener = _listeners[lastIndex].Listener;

                _listeners[lastIndex].Clear();
                return;
            }
        }

        [Obsolete("Use TryRaiseSnapshotUpdated(in HectonDroneFleetSnapshot) so bounded event refusal stays visible at the producer.", true)]
        internal static void RaiseSnapshotUpdated(in HectonDroneFleetSnapshot snapshot)
        {
            TryRaiseSnapshotUpdated(in snapshot);
        }

        internal static bool TryRaiseSnapshotUpdated(in HectonDroneFleetSnapshot snapshot)
        {
            if (_listenerCount <= 0)
                return false;

            HectonDroneFleetSnapshotPayload payload = default;
            payload.ActiveHubCount = snapshot.ActiveHubCount;
            payload.ActiveDroneCount = snapshot.ActiveDroneCount;
            payload.AssignedTaskCount = snapshot.AssignedTaskCount;
            payload.DockedStasisSlotCount = snapshot.DockedStasisSlotCount;
            payload.DestroyedDroneCount = snapshot.DestroyedDroneCount;
            payload.EmergencyLevel = (int)snapshot.EmergencyLevel;
            payload.AverageBatteryPercent = snapshot.AverageBatteryPercent;
            payload.SolderReserve = snapshot.SolderReserve;
            payload.HostileDroneCount = snapshot.HostileDroneCount;
            payload.LogicLeechHijackCount = snapshot.LogicLeechHijackCount;
            payload.EmergencyOverclockActive = snapshot.EmergencyOverclockActive != 0 ? (byte)1 : (byte)0;
            return Enqueue(in payload);
        }

        /// <summary>
        /// Flushes pending fleet snapshots to registered listeners.
        /// </summary>
        public static void FlushPending()
        {
            if (!IsSnapshotVaultHandle(in _pendingEventsHandle))
                return;

            if (_listenerCount <= 0)
            {
                DropQueuedPayloads();
                return;
            }

            PromoteNextFrameEventsIfFrontEmpty();
            if (!TryReadPayloadBuffer(in _pendingEventsHandle, out NativeArray<HectonDroneFleetSnapshotPayload>.ReadOnly pendingEvents))
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

                HectonDroneFleetSnapshotPayload payload = pendingEvents[_pendingEventReadIndex++];

                HectonDroneFleetSnapshot snapshot = new HectonDroneFleetSnapshot(
                    payload.ActiveHubCount,
                    payload.ActiveDroneCount,
                    payload.AssignedTaskCount,
                    payload.DockedStasisSlotCount,
                    payload.DestroyedDroneCount,
                    payload.EmergencyOverclockActive != 0,
                    (SubmarineEmergencyLevel)payload.EmergencyLevel,
                    payload.AverageBatteryPercent,
                    payload.SolderReserve,
                    payload.HostileDroneCount,
                    payload.LogicLeechHijackCount);

                int count = _listenerCount;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IDroneFleetSnapshotEventListener listener = _listeners[i].Listener;
                        if (listener != null)
                            listener.OnDroneFleetSnapshotUpdated(in snapshot);
                    }
                }
                finally
                {
                    _isDispatching = false;
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

        private static bool TryEnsureInitialized()
        {
            IDataVault vault = _vault;
            if (vault == null)
                return false;

            return TryEnsurePayloadBuffer(vault, ref _pendingEventsHandle, PendingEventBufferId) &&
                   TryEnsurePayloadBuffer(vault, ref _nextFrameEventsHandle, NextFrameEventBufferId);
        }

        private static bool PayloadBuffersReady()
        {
            IDataVault vault = _vault;
            return vault != null &&
                   TryReadPayloadBuffer(vault, in _pendingEventsHandle, out _) &&
                   TryReadPayloadBuffer(vault, in _nextFrameEventsHandle, out _);
        }

        private static bool Enqueue(in HectonDroneFleetSnapshotPayload payload)
        {
            if (_listenerCount <= 0)
                return false;

            if (PendingCount >= PendingEventCapacity)
            {
                ReportOverflowOncePerFrame();
                return false;
            }

            if (!PayloadBuffersReady())
            {
                ReportOverflowOncePerFrame();
                return false;
            }

            if (_isDispatching)
            {
                if (_nextFrameEventCount >= PendingEventCapacity ||
                    !TryWritePayload(ref _nextFrameEventsHandle, _nextFrameEventCount, in payload))
                {
                    ReportOverflowOncePerFrame();
                    return false;
                }

                _nextFrameEventCount++;
                return true;
            }

            if (!CompactPendingEvents())
            {
                DropQueuedPayloads();
                ReportOverflowOncePerFrame();
                return false;
            }

            if (_pendingEventCount >= PendingEventCapacity ||
                !TryWritePayload(ref _pendingEventsHandle, _pendingEventCount, in payload))
            {
                ReportOverflowOncePerFrame();
                return false;
            }

            _pendingEventCount++;
            return true;
        }

        private static void ReportOverflowOncePerFrame()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastOverflowWarningFrame == frame)
                return;

            _lastOverflowWarningFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(_overflowWarningHash, _queueHash, PendingEventCapacity);
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

            if (!TryAcquirePayloadMutationBuffer(
                    in _pendingEventsHandle,
                    _pendingEventCount,
                    out NativeArray<HectonDroneFleetSnapshotPayload> buffer,
                    out IDataVault guardedVault,
                    out ulong mutationGuardMask))
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
                ReleaseSnapshotPayloadMutationGuard(guardedVault, mutationGuardMask);
            }
        }

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!IsSnapshotVaultHandle(in _pendingEventsHandle) ||
                !IsSnapshotVaultHandle(in _nextFrameEventsHandle) ||
                _pendingEventReadIndex < _pendingEventCount ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            VaultGenerationHandle<HectonDroneFleetSnapshotPayload> swap = _pendingEventsHandle;
            _pendingEventsHandle = _nextFrameEventsHandle;
            _nextFrameEventsHandle = swap;
            _pendingEventCount = _nextFrameEventCount;
            _pendingEventReadIndex = 0;
            _nextFrameEventCount = 0;
        }

        private static bool TryEnsurePayloadBuffer(
            IDataVault vault,
            ref VaultGenerationHandle<HectonDroneFleetSnapshotPayload> handle,
            BufferID defaultBufferId)
        {
            if (vault == null)
                return false;

            if (TryReadPayloadBuffer(vault, in handle, out _))
                return true;

            BufferID bufferId = ResolveSnapshotBufferId(in handle, defaultBufferId);
            if (vault.TryGetGenerationHandle<HectonDroneFleetSnapshotPayload>(bufferId, out VaultGenerationHandle<HectonDroneFleetSnapshotPayload> existingHandle) &&
                IsSnapshotVaultHandle(in existingHandle))
            {
                handle = existingHandle;
                if (TryReadPayloadBuffer(vault, in handle, out _))
                    return true;
            }

            handle = vault.EnsureGenerationHandle<HectonDroneFleetSnapshotPayload>(
                bufferId,
                PendingEventCapacity,
                SystemID.Construction,
                NativeArrayOptions.ClearMemory);

            return TryReadPayloadBuffer(vault, in handle, out _);
        }

        private static bool TryWritePayload(
            ref VaultGenerationHandle<HectonDroneFleetSnapshotPayload> handle,
            int index,
            in HectonDroneFleetSnapshotPayload payload)
        {
            if (index < 0 ||
                !TryAcquirePayloadMutationBuffer(
                    in handle,
                    index + 1,
                    out NativeArray<HectonDroneFleetSnapshotPayload> buffer,
                    out IDataVault guardedVault,
                    out ulong mutationGuardMask))
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
                ReleaseSnapshotPayloadMutationGuard(guardedVault, mutationGuardMask);
            }
        }

        private static bool TryAcquirePayloadMutationBuffer(
            in VaultGenerationHandle<HectonDroneFleetSnapshotPayload> handle,
            int requiredLength,
            out NativeArray<HectonDroneFleetSnapshotPayload> buffer,
            out IDataVault guardedVault,
            out ulong mutationGuardMask)
        {
            buffer = default;
            guardedVault = null;
            mutationGuardMask = 0UL;

            IDataVault vault = _vault;
            if (vault == null ||
                requiredLength <= 0 ||
                vault.IsCompactionFenceActive ||
                !TryResolveSnapshotMutationGuardMask(in handle, out mutationGuardMask) ||
                !vault.TryAcquireMutationGuard(mutationGuardMask))
            {
                return false;
            }

            bool releaseOnExit = true;
            try
            {
                if (vault.IsCompactionFenceActive ||
                    !vault.TryReadHandle(in handle, out buffer) ||
                    vault.IsCompactionFenceActive ||
                    !buffer.IsCreated ||
                    buffer.Length < requiredLength)
                {
                    buffer = default;
                    return false;
                }

                guardedVault = vault;
                releaseOnExit = false;
                return true;
            }
            finally
            {
                if (releaseOnExit)
                    vault.ReleaseMutationGuard(mutationGuardMask);
            }
        }

        private static bool TryReadPayloadBuffer(
            in VaultGenerationHandle<HectonDroneFleetSnapshotPayload> handle,
            out NativeArray<HectonDroneFleetSnapshotPayload>.ReadOnly buffer)
        {
            return TryReadPayloadBuffer(_vault, in handle, out buffer);
        }

        private static bool TryReadPayloadBuffer(
            IDataVault vault,
            in VaultGenerationHandle<HectonDroneFleetSnapshotPayload> handle,
            out NativeArray<HectonDroneFleetSnapshotPayload>.ReadOnly buffer)
        {
            buffer = default;
            return vault != null &&
                   IsSnapshotVaultHandle(in handle) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.Length >= PendingEventCapacity;
        }

        private static void DropQueuedPayloads()
        {
            _pendingEventCount = 0;
            _pendingEventReadIndex = 0;
            _nextFrameEventCount = 0;
        }

        private static void ReleaseSnapshotVaultBuffer(ref VaultGenerationHandle<HectonDroneFleetSnapshotPayload> handle)
        {
            IDataVault vault = _vault;
            if (vault != null && IsSnapshotVaultHandle(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static void ReleaseSnapshotPayloadMutationGuard(
            IDataVault vault,
            ulong mutationGuardMask)
        {
            if (vault != null && mutationGuardMask != 0UL)
                vault.ReleaseMutationGuard(mutationGuardMask);
        }

        private static BufferID ResolveSnapshotBufferId(
            in VaultGenerationHandle<HectonDroneFleetSnapshotPayload> handle,
            BufferID defaultBufferId)
        {
            if (IsSnapshotVaultHandle(in handle))
                return (BufferID)(int)handle.BufferID;

            return defaultBufferId;
        }

        private static bool IsSnapshotVaultHandle(in VaultGenerationHandle<HectonDroneFleetSnapshotPayload> handle)
        {
            return (handle.BufferID == unchecked((uint)(int)PendingEventBufferId) ||
                    handle.BufferID == unchecked((uint)(int)NextFrameEventBufferId)) &&
                   handle.SystemID == (uint)SystemID.Construction &&
                   handle.Generation != 0u;
        }

        private static bool TryResolveSnapshotMutationGuardMask(
            in VaultGenerationHandle<HectonDroneFleetSnapshotPayload> handle,
            out ulong mutationGuardMask)
        {
            if (IsSnapshotVaultHandle(in handle))
            {
                if (handle.BufferID == unchecked((uint)(int)PendingEventBufferId))
                {
                    mutationGuardMask = PendingEventMutationGuardMask;
                    return true;
                }

                if (handle.BufferID == unchecked((uint)(int)NextFrameEventBufferId))
                {
                    mutationGuardMask = NextFrameEventMutationGuardMask;
                    return true;
                }
            }

            mutationGuardMask = 0UL;
            return false;
        }

        private static ulong SnapshotMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }
    }

    /// <summary>
    /// Central zero-alloc fleet arbitration owner for repair drones.
    /// Runtime drone bodies are stored in native state arrays and rendered indirectly.
    /// </summary>
    internal static partial class DroneFleetManager
    {
        private static int s_SignalPushDropCount;
        internal static int SignalPushDropCount => System.Threading.Volatile.Read(ref s_SignalPushDropCount);
        private static int s_DockingCompleteSignalDropCount;
        private static int s_DockingFailedSignalDropCount;
        private static int s_ItemAcquiredSignalDropCount;
        private static int s_InventoryTransactionSignalDropCount;
        private static int s_InventoryCommandSignalDropCount;
        private static int s_DroneMiningCommitFailureCount;
        private static int s_LastDroneMiningCommitFailureReason;
        private static int s_StorageReservationStaleAckCount;
        private static int s_StorageReservationMismatchAckCount;
        private static readonly uint s_StorageReservationStaleAckWarningHash = unchecked((uint)LocHash.Compute("DroneFleet.StorageReservationStaleAck"));
        private static readonly uint s_StorageReservationMismatchAckWarningHash = unchecked((uint)LocHash.Compute("DroneFleet.StorageReservationMismatchAck"));
        private static readonly uint s_StorageReservationAckContextHash = unchecked((uint)LocHash.Compute("DroneFleet.StorageReservationAck"));
        internal static int DockingCompleteSignalDropCount => System.Threading.Volatile.Read(ref s_DockingCompleteSignalDropCount);
        internal static int DockingFailedSignalDropCount => System.Threading.Volatile.Read(ref s_DockingFailedSignalDropCount);
        internal static int ItemAcquiredSignalDropCount => System.Threading.Volatile.Read(ref s_ItemAcquiredSignalDropCount);
        internal static int InventoryTransactionSignalDropCount => System.Threading.Volatile.Read(ref s_InventoryTransactionSignalDropCount);
        internal static int InventoryCommandSignalDropCount => System.Threading.Volatile.Read(ref s_InventoryCommandSignalDropCount);
        internal static int DroneMiningCommitFailureCount => System.Threading.Volatile.Read(ref s_DroneMiningCommitFailureCount);
        internal static int LastDroneMiningCommitFailureReason => System.Threading.Volatile.Read(ref s_LastDroneMiningCommitFailureReason);
        internal static int StorageReservationStaleAckCount => System.Threading.Volatile.Read(ref s_StorageReservationStaleAckCount);
        internal static int StorageReservationMismatchAckCount => System.Threading.Volatile.Read(ref s_StorageReservationMismatchAckCount);
        private const int InitialTaskCapacity = 64;
        private const int MaxOperationalDroneCount = 500;
        private const int HeadlessDroneCapacity = 512;
        private const int DroneJobBatchSize = 64;
        private const int PhantomDroneCount = 500;
        private const int SurvivalPhantomDroneCount = 0;
        private const int StandardPhantomDroneCount = 192;
        private const int HighFidelityPhantomDroneCount = 384;
        private const uint PortableMaxComputeThreadsPerGroup = 256u;
        private const int HeadlessTaskCapacity = 64;
        private const int HeadlessPendingLaunchCapacity = HeadlessDroneCapacity;
        private const int DroneServiceCommandCapacity = HeadlessDroneCapacity * 3;
        private const int DroneSpatialBucketCapacity = 2048;
        private const int DroneAStarGridSide = 8;
        private const int DroneAStarNodeCapacity = DroneAStarGridSide * DroneAStarGridSide * DroneAStarGridSide;
        private const int DroneAStarScratchNodeCapacity = DroneAStarNodeCapacity * HeadlessDroneCapacity;
        private const int DroneAStarTelemetryCapacity = 1;
        private const int DroneAStarRouteNodeStride = 8;
        private const int DroneAStarRouteNodeCapacity = HeadlessDroneCapacity * DroneAStarRouteNodeStride;
        private const int DroneAStarRouteDebugPointCount = 4;
        private const int DockingObstacleProbeMaxSegments = 3;
        private const int DroneFleetBlackBoxFrameCapacity = 300;
        private const int MaxMainThreadTaskScanCount = 64;
        private const int MaxMainThreadHubScanCount = 8;
        private const int DefaultMaxClaimsPerTarget = 2;
        private const int InvalidHubId = 0;
        private const int EmptyTaskIndex = -1;
        private const string DroneFleetBlackBoxDumpPath = "Docs/AgentLogs/Dump_1306_Construction_FleetCommander.bin";
        private const string DroneFleetLegacyBlackBoxDumpPath = "Docs/AgentLogs/Dump_1306_Construction_DroneFleetLegacy.bin";
        private const string DroneFleetShinobu334BlackBoxDumpPath = "Docs/AgentLogs/Dump_1306_Construction_DroneFleet.bin";
        private const string DroneFleetBlackBoxH8DumpPath = "Docs/AgentLogs/Dump_1306_Construction_DroneFleet.h8dump";
        private const int DroneFleetBlackBoxFlagNonFiniteState = 1;
        private const int DroneFleetBlackBoxFlagAStarFailure = 2;
        private const int DroneFleetBlackBoxFlagDockingCompleteSignalRejected = 4;
        private const int DroneFleetBlackBoxFlagDockingFailedSignalRejected = 8;
        private const int DroneFleetBlackBoxFlagInventoryCommandSignalRejected = 16;
        private const int DroneFleetBlackBoxFlagInventoryTransactionSignalRejected = 32;
        private const int DroneFleetBlackBoxFlagItemAcquiredSignalRejected = 64;
        private const int DroneFleetBlackBoxFlagMiningCommitFailed = 128;
        private const byte DroneMiningCommitFailureNone = 0;
        private const byte DroneMiningCommitFailureInvalidPayload = 1;
        private const byte DroneMiningCommitFailureItemCatalogUnavailable = 2;
        private const byte DroneMiningCommitFailureItemMissing = 3;
        private const byte DroneMiningCommitFailureHubMissing = 4;
        private const byte DroneMiningCommitFailureHubUnpowered = 5;
        private const byte DroneMiningCommitFailureGridMissing = 6;
        private const byte DroneMiningCommitFailureStorageFull = 7;
        private const byte DroneMiningCommitFailureDuplicateHub = 8;
#if UNITY_EDITOR
        private const string DroneNavigationProfilesCsvFileName = "drone_navigation_profiles.csv";
        private const string DroneHardwareProfilesCsvFileName = "drone_hardware_profiles.csv";
        private const string DroneSpecsCsvFileName = "drone_chassis_specs.csv";
        private const string DroneSpecsCsvLegacyFileName = "drone_specs.csv";
        private const int DroneSpecsCsvMaxBytes = 16 * 1024;
#endif
        private const int DroneChassisSpecCapacity = 8;
        private static readonly ulong TaskAssignmentMutationGuardMask =
            DroneMutationGuardBit(BufferID.ShinobuDroneFleetTaskClaimCounts) |
            DroneMutationGuardBit(BufferID.ShinobuDroneFleetTaskPriorityHeap);
        private static readonly ulong DroneServiceCommandMutationGuardMask =
            DroneMutationGuardBit(DroneFleetServiceCommandsBufferId) |
            DroneMutationGuardBit(DroneFleetServiceCommandCursorBufferId);
        private static readonly ulong DroneCoreMutationGuardMask =
            DroneMutationGuardBit(BufferID.ShinobuDroneFleetStates) |
            DroneMutationGuardBit(BufferID.ShinobuDroneFleetStateBackBuffer) |
            DroneMutationGuardBit(BufferID.ShinobuDroneFleetRenderMatrices) |
            DroneMutationGuardBit(BufferID.ShinobuDroneFleetRenderMatrixBackBuffer);
        private static readonly ulong DroneMirrorMutationGuardMask =
            DroneMutationGuardBit(BufferID.ShinobuDroneFleetPositionsSoA) |
            DroneMutationGuardBit(BufferID.ShinobuDroneFleetStateBytes) |
            DroneMutationGuardBit(DroneFleetStateDtoBufferId) |
            DroneMutationGuardBit(DroneFleetTargetDtoBufferId);
        private static readonly ulong DroneCoreMirrorMutationGuardMask =
            DroneCoreMutationGuardMask |
            DroneMirrorMutationGuardMask;
        private static readonly ulong DroneOriginShiftMutationGuardMask =
            DroneCoreMutationGuardMask |
            DroneMutationGuardBit(BufferID.ShinobuDroneFleetPositionsSoA);
        private static ulong DroneHeadlessJobMutationGuardMask =>
            DroneCoreMirrorMutationGuardMask |
            DroneServiceCommandMutationGuardMask |
            DroneTransactionMutationGuardMask |
            DroneMutationGuardBit(BufferID.ShinobuDroneFleetTaskClaimOwners) |
            DroneMutationGuardBit(BufferID.ShinobuDroneFleetTelemetryAccumulator) |
            DroneMutationGuardBit(DroneFleetAssignmentTasksBufferId) |
            DroneMutationGuardBit(DroneFleetSpatialBucketHeadsBufferId) |
            DroneMutationGuardBit(DroneFleetSpatialNextIndicesBufferId) |
            DroneMutationGuardBit(DroneFleetSpatialKeysBufferId) |
            DroneMutationGuardBit(BufferID.ShinobuDroneFleetMacroWaypoints) |
            DroneMutationGuardBit(BufferID.ShinobuDroneFleetMacroWaypointStates) |
            DroneMutationGuardBit(BufferID.ShinobuDroneFleetAStarOpenHeap) |
            DroneMutationGuardBit(BufferID.ShinobuDroneFleetAStarGCosts) |
            DroneMutationGuardBit(BufferID.ShinobuDroneFleetAStarCameFrom) |
            DroneMutationGuardBit(BufferID.ShinobuDroneFleetAStarNodeStates) |
            DroneMutationGuardBit(BufferID.ShinobuDroneFleetMacroRouteNodes) |
            DroneMutationGuardBit(BufferID.ShinobuDroneFleetMacroRouteCounts) |
            DroneMutationGuardBit(BufferID.ShinobuDroneFleetAStarTelemetry) |
            DroneMutationGuardBit(DroneFleetAStarPersistentStatesBufferId) |
            DroneMutationGuardBit(DroneFleetProceduralArgsBufferId) |
            DroneMutationGuardBit(BufferID.ShinobuDroneFleetBlackBox);
        private const float DefaultDroneClearanceRadiusMeters = 0.75f;
        private const float RepairDroneClearanceRadiusMeters = 0.35f;
        private const float MiningDroneClearanceRadiusMeters = 2.0f;
        private const float CombatDroneClearanceRadiusMeters = 0.8f;
        private const uint DroneChassisSpecValidFlag = 1u;
        private const uint DroneChassisRepairHash = 0x29520BB4u;
        private const uint DroneChassisMiningHash = 0x2FF741A1u;
        private const uint DroneChassisCombatHash = 0x1CE36E21u;
        private const uint DroneChassisCutParasiteHash = 0x64C86046u;
        private const uint DroneChassisHeavyMinerHash = 0x7E031634u;
        private const uint DroneChassisMicroWelderHash = 0x5F08629Bu;
        private const uint DroneNavigationSignalSourceHash = 0x53333334u;
        private const byte DronePathFailureGlitchReason = 34;
        private const byte DroneChassisUnavailableGlitchReason = 35;
        private const float MinimumScoreDistanceMeters = 0.75f;
        private const float MinimumScoreDistanceMetersSq = MinimumScoreDistanceMeters * MinimumScoreDistanceMeters;
        private const float RuptureCriticalityBonus = 2.5f;
        private const float FloodCriticalityBonus = 2f;
        private const float BreachCriticalityBonus = 3f;
        private const float CascadeCriticalityBonus = 1.5f;
        private const float ParasiteCriticalityBonus = 4f;
        private const float AirReserveCriticalityScale = 1.5f;
        private const float EmergencyCriticalityScale = 1.35f;
        private const float SeparationDistanceEpsilon = 0.0001f;
        private const float HeadlessTaskRebuildIntervalSeconds = 0.5f;
        private const float HeadlessDefaultSpeedMetersPerSecond = 6.5f;
        private const float HeadlessBatteryDrainPercentPerSecond = 2.5f;
        private const float HeadlessServiceRadiusMeters = 1f;
        private const float HeadlessWeldPowerNormalized = 0.75f;
        private const float HeadlessWeldRangeMeters = 1.25f;
        private const uint DroneRepairSparksSignalHash = 0x44525350u;
        private const int DroneInventoryCopperHash = unchecked((int)H8Hashes.Items.DataCopperHash);
        private const byte DroneRepairSparkDebrisKind = 1;
        private const float DefaultRepairTorchAcousticVolume = 0.32f;
        private const float DefaultRepairTorchAcousticPitch = 1f;
        private const float SolderIntegrityUnitsPerBundle = 10f;
        private const float OrphanWanderDistanceMeters = 4f;
        private const float DroneFlowDragCoefficient = 0.85f;
        private const float DockingObstacleProbeEndpointTrimMeters = 0.35f;
        private const float DockingMinimumProbeDistanceMeters = 0.25f;
        private const int FleetTelemetryPublishFrameInterval = 60;
        private const string DroneCullingComputeAssetPath = "Assets/_Project/Art/Shaders/DroneCulling.compute";
        private const string PhantomDronesComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_PhantomDrones.compute";
        private const uint DroneProceduralVerticesPerInstance = 36u;
        private const float DroneProceduralScaleMeters = 0.28f;
        private const BufferID DroneFleetStateDtoBufferId = BufferID.DroneFleetManager_DroneFleetStateDtoBufferId;
        private const BufferID DroneFleetTargetDtoBufferId = BufferID.DroneFleetManager_DroneFleetTargetDtoBufferId;
        private const BufferID DroneFleetAssignmentTasksBufferId = BufferID.DroneFleetManager_DroneFleetAssignmentTasksBufferId;
        private const BufferID DroneFleetProceduralArgsBufferId = BufferID.DroneFleetManager_DroneFleetProceduralArgsBufferId;
        private const BufferID DroneFleetServiceCommandsBufferId = BufferID.DroneFleetManager_DroneFleetServiceCommandsBufferId;
        private const BufferID DroneFleetServiceCommandCursorBufferId = BufferID.SaveMerkleNodeFront;
        private const BufferID DroneFleetSpatialBucketHeadsBufferId = BufferID.SaveMerkleDeltaRecords;
        private const BufferID DroneFleetSpatialNextIndicesBufferId = BufferID.SaveMerkleDeltaBytes;
        private const BufferID DroneFleetSpatialKeysBufferId = BufferID.SaveMerkleCompressedBytes;
        private const BufferID DroneFleetChassisSpecsBufferId = BufferID.DroneFleetManager_DroneFleetChassisSpecsBufferId;
        private const BufferID DroneFleetAStarPersistentStatesBufferId = BufferID.DroneFleetManager_DroneFleetAStarPersistentStatesBufferId;
        private const int DroneBoneJointTableCapacity = 64;
        private const int DroneAttachmentTableCapacity = 16;
        private const float DroneCullRadiusMeters = 1.25f;
        private const float SurvivalDroneRenderDistanceMeters = 50f;
        private const float StandardDroneRenderDistanceMeters = 100f;
        private const float HighFidelityDroneRenderDistanceMeters = 150f;
        private const float PhantomDroneOrbitRadiusMeters = 20f;
        private const float PhantomDroneVerticalAmplitudeMeters = 4.5f;
        private const float PhantomDroneScaleMeters = 0.18f;
        private const float PhantomDroneBoundsDiameterMeters = 64f;
        private const float PhantomDronePhaseWrapSeconds = 60f;
        private const float HeadlessSimulationClockMaxSeconds = 16777215f;
        private const float DroneRelaySubmarineDistanceMeters = 100f;
        private const float DroneRelayScanRadiusMeters = 160f;
        private const float DroneRelayPingRadiusMeters = 220f;
        private const float DroneRelayPingLifetimeSeconds = 4f;
        private const int MaxDroneRelayContacts = 16;

        [StructLayout(LayoutKind.Explicit, Size = 80)]
        private struct DroneRenderInstance
        {
            [FieldOffset(0)]
            public float4x4 Matrix;
            [FieldOffset(64)]
            public float TransactionProgress;
            [FieldOffset(68)]
            public float3 Padding;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct DroneCullingStateGpu
        {
            [FieldOffset(0)]
            public float3 Position;
            [FieldOffset(12)]
            public uint PackedStateFactionCorridor;
        }

        private struct RepairTaskCandidate
        {
            public DroneFleetTaskKind Kind;
            public BaseModule Module;
            public int ModuleIndex;
            public Vector3 Position;
            public float Radius;
            public float Score;
            public float CriticalityWeight;
        }

        private struct PendingDroneLaunch
        {
            public byte Active;
            public int DroneSlot;
            public int DroneId;
            public RepairDroneHub Hub;
            public DroneFleetTask Task;
            public Vector3 HomePosition;
            public Quaternion HomeRotation;
            public float RepairRatePerSecond;
            public int LoadedSolderUnits;
        }

        [StructLayout(LayoutKind.Explicit, Size = 80)]
        private struct DroneFleetBlackBoxEntry
        {
            [FieldOffset(0)]
            public int Frame;
            [FieldOffset(4)]
            public int ActiveCount;
            [FieldOffset(8)]
            public int StateHash;
            [FieldOffset(12)]
            public int Flags;
            [FieldOffset(16)]
            public float DeltaTime;
            [FieldOffset(20)]
            public int DockingAborts;
            [FieldOffset(24)]
            public int PathSolves;
            [FieldOffset(28)]
            public int PathFailures;
            [FieldOffset(32)]
            public int PathIterations;
            [FieldOffset(36)]
            public float AveragePathfindingTimeMs;
            [FieldOffset(40)]
            public int TasksCompleted;
            [FieldOffset(44)]
            public float3 FirstPosition;
            [FieldOffset(56)]
            public float3 BoundsCenter;
            [FieldOffset(68)]
            public float3 BoundsExtents;
        }

        private sealed class HeadlessFleetDriver : IUpdatable, ILateFrameTickable, IRenderable, IGlobalRegistryHotSwapListener
        {
            public void Tick(float deltaTime)
            {
                ScheduleHeadlessSimulation(deltaTime);
            }

            public void LateFrameTick()
            {
                CompleteHeadlessSimulationAndApply();
            }

            public void Render(float deltaTime)
            {
                RenderHeadlessFleet(deltaTime);
            }

            public void OnGlobalRegistryServiceReplaced(
                GlobalRegistryServiceSlot serviceSlot,
                object previousService,
                object currentService)
            {
                CacheRuntimeRegistryService(serviceSlot, currentService);
            }
        }

        // COLD ALLOC: HeadlessFleetDriver[1] - registry adapter for headless drone simulation and rendering - owner: DroneFleetManager
        private static readonly HeadlessFleetDriver s_HeadlessDriver = new HeadlessFleetDriver();
        private static VaultGenerationHandle<int> s_TaskClaimCountsHandle;
        private static VaultGenerationHandle<HeadlessDroneState> s_DroneStatesHandle;
        private static VaultGenerationHandle<HeadlessDroneState> s_DroneStateBackBufferHandle;
        private static VaultGenerationHandle<float4x4> s_DroneRenderMatricesHandle;
        private static VaultGenerationHandle<float4x4> s_DroneRenderMatrixBackBufferHandle;
        private static VaultGenerationHandle<DroneRenderInstance> s_DroneRenderInstancesHandle;
        private static VaultGenerationHandle<DroneCullingStateGpu> s_DroneCullingStatesHandle;
        private static VaultGenerationHandle<float3> s_DronePositionsSoAHandle;
        private static VaultGenerationHandle<byte> s_DroneStateBytesHandle;
        private static VaultGenerationHandle<DroneFleetBlackBoxEntry> s_DroneBlackBoxHandle;
        private static VaultGenerationHandle<DroneFleetTuningConstants> s_DroneTuningConstantsHandle;
        private static VaultGenerationHandle<PathWaypointDTO> s_DroneMacroWaypointsHandle;
        private static VaultGenerationHandle<byte> s_DroneMacroWaypointStatesHandle;
        private static VaultGenerationHandle<DroneNativeMinHeapNode> s_DroneAStarOpenHeapHandle;
        private static VaultGenerationHandle<float> s_DroneAStarGCostsHandle;
        private static VaultGenerationHandle<int> s_DroneAStarCameFromHandle;
        private static VaultGenerationHandle<byte> s_DroneAStarNodeStatesHandle;
        private static VaultGenerationHandle<int> s_DroneMacroRouteNodesHandle;
        private static VaultGenerationHandle<byte> s_DroneMacroRouteCountsHandle;
        private static VaultGenerationHandle<DroneAStarTelemetry> s_DroneAStarTelemetryHandle;
        private static VaultGenerationHandle<DroneAStarPersistentState> s_DroneAStarPersistentStatesHandle;
        private static VaultGenerationHandle<int> s_HeadlessTaskClaimOwnersHandle;
        private static VaultGenerationHandle<int> s_FleetTelemetryAccumulatorHandle;
        private static VaultGenerationHandle<DroneAssignmentTaskDTO> s_DroneTaskPriorityHeapHandle;
        private static VaultGenerationHandle<DroneStateDTO> s_DroneStateDtosHandle;
        private static VaultGenerationHandle<DroneTargetDTO> s_DroneTargetDtosHandle;
        private static VaultGenerationHandle<DroneAssignmentTaskDTO> s_DroneAssignmentTasksHandle;
        private static VaultGenerationHandle<DroneProceduralIndirectArgsDTO> s_DroneProceduralArgsHandle;
        private static VaultGenerationHandle<DroneServiceCommand> s_DroneServiceCommandsHandle;
        private static VaultGenerationHandle<DroneServiceCommandCursor> s_DroneServiceCommandCursorHandle;
        private static VaultGenerationHandle<int> s_DroneSpatialBucketHeadsHandle;
        private static VaultGenerationHandle<int> s_DroneSpatialNextIndicesHandle;
        private static VaultGenerationHandle<int> s_DroneSpatialKeysHandle;
        private static VaultGenerationHandle<DroneChassisSpecDTO> s_DroneChassisSpecsHandle;
        private static RepairDroneHub[] s_DroneHubs;
        private static int[] s_DroneSlotDroneIds;
        private static bool[] s_DroneSlotDestroyed;
        private static bool[] s_PendingAbortBySlot;
        private static bool[] s_PendingReleaseBySlot;
        private static bool[] s_PendingHostileBySlot;
        private static bool[] s_PendingResupplyGrantBySlot;
        private static bool[] s_PendingResupplyFailureBySlot;
        private static int[] s_PendingResupplyReservationIdsBySlot;
        private static byte[] s_DroneMiningCommitFailureReasonsBySlot;
        private static BaseModule[] s_TargetModulesByDroneSlot;
        private static HectonVoxelVolume[] s_TargetVoxelVolumesByDroneSlot;
        private static DroneFleetTaskKind[] s_DroneTaskKindsBySlot;
        private static Vector3[] s_DronePositions;
        private static BaseModule[] s_TaskModuleRefs;
        private static HectonVoxelVolume[] s_TaskVoxelVolumeRefs;
        private static DroneFleetTaskKind[] s_TaskKinds;
        private static PendingDroneLaunch[] s_PendingLaunches;
        private static int s_PendingLaunchCount;
        private static int s_DroneChassisSpecCount;
        private static int s_HeadlessTaskCount;
        private static int s_HeadlessDroneIdSequence;
        private static int s_HeadlessStasisSlotCount;
        private static bool s_Initialized;
        private static int s_StorageReservationCommitResolvedListenerGeneration = -1;
        private static bool s_RuntimeRegistryCacheInitialized;
        private static ILogisticsService s_CachedLogisticsService;
        private static IPlayerRuntimeContext s_CachedPlayerRuntime;
        private static IPlayerInventoryService s_CachedPlayerInventoryService;
        private static ISubmarineRuntimeContext s_CachedSubmarineRuntime;
        private static IFluidSurfaceCurrentReadModel s_CachedFluidRuntime;
        private static FloraInteractionManager s_CachedFloraInteractionManager;
        private static HectonMapMagicVegetationBridge s_CachedVegetationBridge;
        private static IDataVault s_CachedDataVault;
        private static IVoxelSonarSdfReadLeaseModel s_CachedVoxelSdfReadLeaseModel;
        private static bool s_DockingSignalLanesConfigured;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Latch for the DroneFleetMiningServiceSignal no-producer advisory in BuildHeadlessTaskMap. One bool
        // read per fleet tick after the first announce; compiled out of release entirely. Reset in
        // ResetStaticState so a domain reload re-announces.
        private static bool s_UnpublishedMiningServiceLaneWarned;
#endif
        private static bool s_HeadlessDriverRegistered;
        private static bool s_HeadlessUpdateRegistered;
        private static bool s_HeadlessLateFrameRegistered;
        private static bool s_HeadlessRenderRegistered;
        private static bool s_HeadlessHotSwapRegistered;
        private static bool s_HeadlessJobScheduled;
        private static JobHandle s_HeadlessJobHandle;
        private static bool s_DroneServiceCommandMutationGuardHeld;
        private static IDataVault s_DroneServiceCommandMutationGuardVault;
        private static bool s_DroneServiceCommandMutationGuardCoveredByHeadlessJob;
        private static bool s_DroneHeadlessJobMutationGuardHeld;
        private static IDataVault s_DroneHeadlessJobMutationGuardVault;
        private static IVoxelSonarSdfReadLeaseModel s_HeadlessSdfReadLeaseModel;
        private static VoxelSonarSdfReadLease s_HeadlessSdfReadLease;
        private static bool s_HeadlessSdfReadLeaseLocked;
        private static DroneFleetTuningConstants s_HeadlessFrameTuning;
        private static bool s_HeadlessFrameTuningValid;
        private static bool s_FleetSacrificeRequested;
        private static int s_DestroyedDroneCount;
        private static SubmarineEmergencyLevel s_EmergencyLevel;
        private static HectonDroneFleetSnapshot s_LastSnapshot;
        private static FleetStatusSnapshot s_LastFleetStatusSnapshot;
        private static Material s_DroneProceduralMaterial;
        private static AudioClip s_RepairTorchAcousticClip;
        private static GraphicsBuffer s_DroneMatrixBuffer;
        private static GraphicsBuffer s_DroneMatrixBufferBackBuffer;
        private static GraphicsBuffer s_DroneStateGpuBuffer;
        private static GraphicsBuffer s_DroneRenderInstanceBuffer;
        private static GraphicsBuffer s_DroneVisibleMatrixBuffer;
        private static GraphicsBuffer s_DroneVisibleInstanceBuffer;
        private static GraphicsBuffer s_DroneVisibleIndexBuffer;
        private static GraphicsBuffer s_DroneProceduralArgsBuffer;
        private static GraphicsBuffer s_DroneProceduralArgsUploadBuffer;
        private static GraphicsBuffer s_DroneDefaultColorBuffer;
        private static ComputeShader s_DroneCullingCompute;
        private static ComputeShader s_PhantomDronesCompute;
        private static GraphicsBuffer s_PhantomDroneMatrixBuffer;
        private static GraphicsBuffer s_PhantomDroneColorBuffer;
        private static GraphicsBuffer s_PhantomDroneArgsBuffer;
        private static Bounds s_DroneDrawBounds = new Bounds(Vector3.zero, new Vector3(2048f, 2048f, 2048f));
        private static Bounds s_PhantomDroneDrawBounds = new Bounds(Vector3.zero, new Vector3(PhantomDroneBoundsDiameterMeters, PhantomDroneBoundsDiameterMeters, PhantomDroneBoundsDiameterMeters));
        private static int s_DroneRenderLayer;
        private static float s_HeadlessTaskRebuildTimer;
        private static float s_LastHeadlessDeltaTime;
        private static float s_HeadlessSimulationClockSeconds;
        private static float s_PhantomDronePhaseSeconds;
        private static int s_PhantomDroneLastDrawCount;
        private static int s_DroneMatrixUploadBufferIndex;
        private static int s_LastDroneMatrixUploadFrame = -1;
        private static int s_FleetTelemetryFrameCounter;
        private static int s_LogicLeechHijackCount;
        private static int s_DockingAbortCount;
        private static int s_DroneBlackBoxCursor;
        private static int s_LastDroneBlackBoxDumpFrame;
        private static bool s_DroneBlackBoxDumpPending;
        private static int s_DroneFrameIndex;
        private static int s_DroneAStarSolvedCount;
        private static int s_DroneAStarFailureCount;
        private static int s_DroneAStarIterationCount;
        private static int s_LastDroneAStarStatus;
        private static float s_LastDroneAStarAveragePathfindingTimeMs;
        private static int s_LastDronePathFailureSignalFrame = -1;
        private static int s_DroneTasksCompletedCount;
        private static float s_RepairTorchAcousticVolume = DefaultRepairTorchAcousticVolume;
        private static float s_RepairTorchAcousticPitch = DefaultRepairTorchAcousticPitch;
        private static int s_LastDroneSteeringTickModulo = 1;
        private static DroneFleetFormationMode s_FleetFormationMode;
        private static bool s_DroneCullingKernelsResolved;
        private static bool s_PhantomDroneKernelResolved;
        private static bool s_DroneProceduralMaterialRuntimeOwned;
        private static int s_DroneCullKernel;
        private static int s_DroneClearArgsKernel;
        private static int s_PhantomDroneKernel;
        private static int s_DroneCullThreadGroupSizeX;
        private static int s_PhantomDroneThreadGroupSizeX;

        private static int s_DroneMatricesPropertyId;
        private static int s_InstanceMatricesPropertyId;
        private static int s_DroneStatesPropertyId;
        private static int s_DroneRenderInstancesPropertyId;
        private static int s_DroneVisibleInstancesPropertyId;
        private static int s_DroneVisibleIndicesPropertyId;
        private static int s_IndirectArgsBufferPropertyId;
        private static int s_CameraFrustumPlanesPropertyId;
        private static int s_DroneCountPropertyId;
        private static int s_DroneCullRadiusPropertyId;
        private static int s_CameraPositionPropertyId;
        private static int s_DroneRenderDistanceSqPropertyId;
        private static int s_PhantomMatricesPropertyId;
        private static int s_PhantomColorsPropertyId;
        private static int s_PhantomAnchorPropertyId;
        private static int s_PhantomTimePropertyId;
        private static int s_PhantomCountPropertyId;
        private static int s_PhantomBaseRadiusPropertyId;
        private static int s_PhantomVerticalAmplitudePropertyId;
        private static int s_PhantomScalePropertyId;
        private static int s_PhantomCapacityPropertyId;
        private static int s_DroneProceduralCameraOriginPropertyId;
        private static int s_UsePhantomColorsPropertyId;
        private static bool s_DroneShaderPropertyIdsInitialized;
        private static int s_ConfiguredDroneBoneJointCount;
        private static uint s_ConfiguredDroneBoneDroneId;
        private static uint s_ConfiguredDroneBoneBakeHash;
        private static float s_ConfiguredDroneBoneQualityWeight;
        private static int s_ConfiguredDroneAttachmentCount;
        private static uint s_ConfiguredDroneAttachmentDroneId;
        private static uint s_ConfiguredDroneAttachmentBakeHash;
        private static float s_ConfiguredDroneAttachmentQualityWeight;
        // COLD ALLOC: Plane[6] - reusable camera frustum plane scratch for GPU drone culling upload - owner: DroneFleetManager
        private static readonly Plane[] s_CullingPlanes = new Plane[6];
        // COLD ALLOC: Vector4[6] - reusable camera frustum plane vector scratch for GPU drone culling upload - owner: DroneFleetManager
        private static readonly Vector4[] s_CullingPlaneVectors = new Vector4[6];
        // COLD ALLOC: SpatialQueryHit[16] - drone acoustic relay contact scratch buffer - owner: DroneFleetManager
        private static readonly SpatialQueryHit[] s_DroneRelayContacts = new SpatialQueryHit[MaxDroneRelayContacts];
        // COLD ALLOC: DroneBoneJointRuntimeData[64] - cold-cached prefab IK joint table copied to caller-owned NativeArray - owner: DroneFleetManager
        private static readonly DroneBoneJointRuntimeData[] s_ConfiguredDroneBoneJointTable = new DroneBoneJointRuntimeData[DroneBoneJointTableCapacity];
        // COLD ALLOC: DroneAttachmentRuntimeData[16] - cold-cached prefab socket/VFX table copied to caller-owned NativeArray - owner: DroneFleetManager
        private static readonly DroneAttachmentRuntimeData[] s_ConfiguredDroneAttachmentTable = new DroneAttachmentRuntimeData[DroneAttachmentTableCapacity];
        // COLD ALLOC: SubmarineOsEventBridge[1] - static fleet bridge into deferred submarine OS payloads - owner: DroneFleetManager
        private static readonly SubmarineOsEventBridge s_SubmarineOsEventBridge = new SubmarineOsEventBridge();
        // COLD ALLOC: StorageReservationCommitResolvedBridge[1] - static fleet bridge into deferred command queue acknowledgements - owner: DroneFleetManager
        private static readonly StorageReservationCommitResolvedBridge s_StorageReservationCommitResolvedBridge = new StorageReservationCommitResolvedBridge();

        private sealed class SubmarineOsEventBridge : ISubmarineOsEventListener
        {
            public void OnSubmarineOsEvent(in SubmarineOsEventPayload payload)
            {
                if (HectonSubmarineOsEvents.TryBuildSnapshot(in payload, out HectonSubmarineOsSnapshot snapshot))
                    HandleSubmarineSnapshotUpdated(in snapshot);
            }
        }

        private sealed class StorageReservationCommitResolvedBridge : ThreadSafeCommandQueue.IStorageReservationCommitResolvedListener
        {
            public void OnStorageReservationCommitResolved(in ThreadSafeCommandQueue.StorageReservationCommitResolvedPayload payload)
            {
                HandleStorageReservationCommitResolved(
                    payload.RequesterId,
                    payload.ReservationId,
                    payload.Committed != 0);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (s_Initialized)
            {
                HectonSubmarineOsEvents.Unregister(s_SubmarineOsEventBridge);
                ThreadSafeCommandQueue.Unregister(s_StorageReservationCommitResolvedBridge);
            }

            TryUnregisterHeadlessDriver();
            CompletePendingHeadlessJobForReset();
            ReleaseHeadlessSdfReadLease();
            ReleaseHeadlessNativeMemory();
            ReleaseRenderBuffers();
            ReleasePhantomRenderResources();

            s_PendingLaunchCount = 0;
            s_HeadlessTaskCount = 0;
            s_HeadlessDroneIdSequence = 0;
            s_SignalPushDropCount = 0;
            s_DockingCompleteSignalDropCount = 0;
            s_DockingFailedSignalDropCount = 0;
            s_ItemAcquiredSignalDropCount = 0;
            s_InventoryTransactionSignalDropCount = 0;
            s_InventoryCommandSignalDropCount = 0;
            s_DroneMiningCommitFailureCount = 0;
            s_LastDroneMiningCommitFailureReason = DroneMiningCommitFailureNone;
            s_StorageReservationStaleAckCount = 0;
            s_StorageReservationMismatchAckCount = 0;
            s_HeadlessStasisSlotCount = 0;
            s_FleetSacrificeRequested = false;
            s_DestroyedDroneCount = 0;
            s_EmergencyLevel = SubmarineEmergencyLevel.Nominal;
            s_LastSnapshot = default;
            s_LastFleetStatusSnapshot = default;
            s_Initialized = false;
            s_StorageReservationCommitResolvedListenerGeneration = -1;
            s_RuntimeRegistryCacheInitialized = false;
            s_CachedLogisticsService = null;
            s_CachedPlayerRuntime = null;
            s_CachedPlayerInventoryService = null;
            s_CachedSubmarineRuntime = null;
            s_CachedFluidRuntime = null;
            s_CachedFloraInteractionManager = null;
            s_CachedVegetationBridge = null;
            HectonDroneFleetEvents.BindDataVault(null);
            RepairDroneTorchAcousticEvents.BindDataVault(null);
            s_CachedDataVault = null;
            s_CachedVoxelSdfReadLeaseModel = null;
            s_DockingSignalLanesConfigured = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            s_UnpublishedMiningServiceLaneWarned = false;
#endif
            s_HeadlessHotSwapRegistered = false;
            s_HeadlessJobScheduled = false;
            s_HeadlessSdfReadLease = default;
            s_HeadlessSdfReadLeaseModel = null;
            s_HeadlessSdfReadLeaseLocked = false;
            s_HeadlessFrameTuning = default;
            s_HeadlessFrameTuningValid = false;
            s_DroneProceduralMaterial = null;
            s_RepairTorchAcousticClip = null;
            s_PhantomDronesCompute = null;
            s_DroneRenderLayer = 0;
            s_HeadlessTaskRebuildTimer = 0f;
            s_LastHeadlessDeltaTime = 0f;
            s_HeadlessSimulationClockSeconds = 0f;
            s_PhantomDronePhaseSeconds = 0f;
            s_PhantomDroneLastDrawCount = -1;
            s_DroneMatrixUploadBufferIndex = 0;
            s_LastDroneMatrixUploadFrame = -1;
            s_FleetTelemetryFrameCounter = 0;
            s_LogicLeechHijackCount = 0;
            s_DockingAbortCount = 0;
            s_DroneBlackBoxCursor = 0;
            s_LastDroneBlackBoxDumpFrame = -1;
            s_DroneBlackBoxDumpPending = false;
            s_DroneFrameIndex = 0;
            s_DroneAStarSolvedCount = 0;
            s_DroneAStarFailureCount = 0;
            s_DroneAStarIterationCount = 0;
            s_LastDroneAStarStatus = 0;
            s_LastDroneAStarAveragePathfindingTimeMs = 0f;
            s_LastDronePathFailureSignalFrame = -1;
            s_DroneTasksCompletedCount = 0;
            s_RepairTorchAcousticVolume = DefaultRepairTorchAcousticVolume;
            s_RepairTorchAcousticPitch = DefaultRepairTorchAcousticPitch;
            s_FleetFormationMode = DroneFleetFormationMode.Repair;
            s_DroneCullingCompute = null;
            s_DroneCullingKernelsResolved = false;
            s_PhantomDroneKernelResolved = false;
            s_DroneProceduralMaterialRuntimeOwned = false;
            s_DroneCullKernel = -1;
            s_DroneClearArgsKernel = -1;
            s_PhantomDroneKernel = -1;
            s_DroneCullThreadGroupSizeX = 0;
            s_PhantomDroneThreadGroupSizeX = 0;
            s_DroneMatricesPropertyId = 0;
            s_InstanceMatricesPropertyId = 0;
            s_DroneStatesPropertyId = 0;
            s_DroneRenderInstancesPropertyId = 0;
            s_DroneVisibleInstancesPropertyId = 0;
            s_DroneVisibleIndicesPropertyId = 0;
            s_IndirectArgsBufferPropertyId = 0;
            s_CameraFrustumPlanesPropertyId = 0;
            s_DroneCountPropertyId = 0;
            s_DroneCullRadiusPropertyId = 0;
            s_CameraPositionPropertyId = 0;
            s_DroneRenderDistanceSqPropertyId = 0;
            ClearConfiguredDroneBoneTable();
            ClearConfiguredDroneAttachmentTable();
            s_PhantomMatricesPropertyId = 0;
            s_PhantomColorsPropertyId = 0;
            s_PhantomAnchorPropertyId = 0;
            s_PhantomTimePropertyId = 0;
            s_PhantomCountPropertyId = 0;
            s_PhantomBaseRadiusPropertyId = 0;
            s_PhantomVerticalAmplitudePropertyId = 0;
            s_PhantomScalePropertyId = 0;
            s_PhantomCapacityPropertyId = 0;
            s_DroneProceduralCameraOriginPropertyId = 0;
            s_UsePhantomColorsPropertyId = 0;
            s_DroneShaderPropertyIdsInitialized = false;

            ReleaseDroneVaultHandle(ref s_TaskClaimCountsHandle, BufferID.ShinobuDroneFleetTaskClaimCounts);
        }

        internal static HectonDroneFleetSnapshot CurrentSnapshot
        {
            get
            {
                return s_LastSnapshot;
            }
        }

        internal static bool IsEmergencyOverclockActive
        {
            get
            {
                return s_EmergencyLevel == SubmarineEmergencyLevel.Evacuate;
            }
        }

        public static void RequestFleetSacrifice()
        {
            EnsureInitialized();
            s_FleetSacrificeRequested = true;
            PublishSnapshot();
        }

        /// <summary>
        /// Requests a tactical formation mode for idle drones without interrupting active repair/resupply sorties.
        /// </summary>
        public static void RequestFleetFormation(DroneFleetFormationMode formationMode)
        {
            EnsureInitialized();
            s_FleetFormationMode = formationMode;
        }

        /// <summary>
        /// Supplies the GPU culling compute shader used by the headless indirect renderer.
        /// </summary>
        public static void ConfigureHeadlessCulling(ComputeShader cullingCompute)
        {
            EnsureInitialized();
            s_DroneCullingCompute = cullingCompute;
            s_DroneCullKernel = -1;
            s_DroneClearArgsKernel = -1;
            s_DroneCullThreadGroupSizeX = 0;
            s_DroneCullingKernelsResolved = false;
            ResolveDroneCullingKernels();
        }

        internal static void ConfigureHeadlessRenderSource(GameObject dronePrefab)
        {
            if (dronePrefab == null)
                return;

            EnsureInitialized();

            s_DroneRenderLayer = dronePrefab.layer;
            s_PhantomDroneLastDrawCount = -1;
            CacheConfiguredDroneBoneTable(dronePrefab);
            CacheConfiguredDroneAttachmentTable(dronePrefab);
            EnsureRenderBuffers();
        }

        internal static void ConfigurePhantomSwarm(ComputeShader phantomCompute, Material phantomMaterial)
        {
            EnsureInitialized();

            if (phantomCompute != null)
            {
                s_PhantomDronesCompute = phantomCompute;
                s_PhantomDroneKernel = -1;
                s_PhantomDroneThreadGroupSizeX = 0;
                s_PhantomDroneKernelResolved = false;
            }

            ResolvePhantomDroneKernel();
            EnsurePhantomRenderResources();
        }

        internal static void ConfigureRepairTorchAcoustic(AudioClip clip, float volume, float pitch)
        {
            if (clip == null)
                return;

            EnsureInitialized();
            s_RepairTorchAcousticClip = clip;
            s_RepairTorchAcousticVolume = Mathf.Clamp01(volume);
            s_RepairTorchAcousticPitch = Mathf.Clamp(pitch, 0.25f, 2f);
        }

        internal static void ClearRepairTorchAcousticBinding()
        {
            s_RepairTorchAcousticClip = null;
            s_RepairTorchAcousticVolume = DefaultRepairTorchAcousticVolume;
            s_RepairTorchAcousticPitch = DefaultRepairTorchAcousticPitch;
        }

        internal static bool TryLaunchHeadlessDrone(
            RepairDroneHub hub,
            in DroneFleetTask task,
            Vector3 homePosition,
            float repairRatePerSecond,
            int loadedSolderUnits,
            out int droneId)
        {
            droneId = 0;
            if (hub == null || !task.IsValid())
                return false;

            EnsureInitialized();
            TryRegisterHeadlessDriver();

            if (CountManagedHeadlessDrones() >= MaxOperationalDroneCount)
                return false;

            int slot = FindFreeHeadlessSlot();
            if (slot < 0 || s_PendingLaunchCount >= s_PendingLaunches.Length)
                return false;

            droneId = ++s_HeadlessDroneIdSequence;
            if (droneId <= 0)
                droneId = ++s_HeadlessDroneIdSequence;

            s_DroneSlotDroneIds[slot] = droneId;
            s_PendingReleaseBySlot[slot] = false;
            s_PendingAbortBySlot[slot] = false;
            s_PendingHostileBySlot[slot] = false;
            ClearDroneMiningCommitFailureLatch(slot);
            s_PendingLaunches[s_PendingLaunchCount++] = new PendingDroneLaunch
            {
                Active = 1,
                DroneSlot = slot,
                DroneId = droneId,
                Hub = hub,
                Task = task,
                HomePosition = homePosition,
                HomeRotation = hub.DockRotation,
                RepairRatePerSecond = Mathf.Max(0.1f, repairRatePerSecond),
                LoadedSolderUnits = Mathf.Max(0, loadedSolderUnits)
            };

            PublishSnapshot();
            return true;
        }

        internal static bool IsHeadlessDroneActive(int droneId)
        {
            if (droneId <= 0 || s_DroneSlotDroneIds == null)
                return false;

            for (int i = 0; i < s_DroneSlotDroneIds.Length; i++)
            {
                if (s_DroneSlotDroneIds[i] == droneId && !s_PendingReleaseBySlot[i])
                    return true;
            }

            return false;
        }

        internal static void AbortHeadlessDrone(int droneId)
        {
            int slot = ResolveHeadlessSlot(droneId);
            if (slot < 0)
                return;

            s_PendingAbortBySlot[slot] = true;
        }

        internal static void ReleaseHeadlessDrone(int droneId)
        {
            int slot = ResolveHeadlessSlot(droneId);
            if (slot < 0)
                return;

            s_PendingReleaseBySlot[slot] = true;
        }

        internal static bool ReportLogicLeechContact(Vector3 contactPosition, float radiusMeters)
        {
            return TryHijackNearestDrone(contactPosition, radiusMeters);
        }

        internal static bool TryHijackNearestDrone(Vector3 contactPosition, float radiusMeters)
        {
            EnsureInitialized();
            if (s_DroneSlotDroneIds == null || radiusMeters <= 0.0001f)
                return false;

            float radiusSq = radiusMeters * radiusMeters;
            int bestSlot = -1;
            float bestDistanceSq = float.MaxValue;
            for (int i = 0; i < s_DroneSlotDroneIds.Length; i++)
            {
                if (s_DroneSlotDroneIds[i] <= 0 || s_PendingReleaseBySlot[i])
                    continue;

                float distanceSq = (s_DronePositions[i] - contactPosition).sqrMagnitude;
                if (distanceSq > radiusSq || distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestSlot = i;
            }

            if (bestSlot < 0)
                return false;

            s_PendingHostileBySlot[bestSlot] = true;
            PublishSnapshot();
            return true;
        }

        internal static void ReportDroneDestroyed()
        {
            EnsureInitialized();
            s_DestroyedDroneCount++;
            PublishSnapshot();
        }

        [Obsolete("Use TryNotifyFleetStateChanged() so fleet snapshot enqueue rejection stays visible.", true)]
        internal static void NotifyFleetStateChanged()
        {
            TryNotifyFleetStateChanged();
        }

        internal static bool TryNotifyFleetStateChanged()
        {
            EnsureInitialized();
            TryRegisterHeadlessDriver();
            return TryPublishSnapshot();
        }

        internal static bool TryAssignRepairTask(
            RepairDroneHub hub,
            float dispatchIntegrityThreshold,
            out BaseModule target,
            out float assignmentScore,
            out float criticalityWeight)
        {
            target = null;
            if (!TryAssignFleetTask(hub, dispatchIntegrityThreshold, out DroneFleetTask task, out assignmentScore, out criticalityWeight))
                return false;

            target = task.Module;
            return target != null;
        }

        internal static bool TryAssignFleetTask(
            RepairDroneHub hub,
            float dispatchIntegrityThreshold,
            out DroneFleetTask task,
            out float assignmentScore,
            out float criticalityWeight)
        {
            task = default;
            assignmentScore = 0f;
            criticalityWeight = 0f;

            if (hub == null)
                return false;

            EnsureInitialized();

            ILogisticsService manager = s_CachedLogisticsService;
            int moduleCount = manager != null ? manager.SpawnedBaseModuleCount : 0;
            if (moduleCount == 0)
                return false;

            int scanModuleCount = Mathf.Min(moduleCount, MaxMainThreadTaskScanCount);
            if (!TryAcquireTaskAssignmentMutationViews(
                    scanModuleCount,
                    out NativeArray<int> taskClaimCounts,
                    out NativeArray<DroneAssignmentTaskDTO> taskPriorityHeap,
                    out IDataVault taskAssignmentVault))
            {
                return false;
            }

            bool assignedTask = false;
            try
            {
                ClearClaimCounts(scanModuleCount, taskClaimCounts);
                RebuildActiveClaimCounts(manager, scanModuleCount, taskClaimCounts);

                Vector3 hubPosition = hub.DockPosition;
                PowerGrid hubGrid = hub.CurrentGrid;
                FloraInteractionManager floraInteractionManager = s_CachedFloraInteractionManager;
                RepairTaskCandidate bestTask = default;
                bool hasBestTask = false;
                DroneTaskNativeMinHeap taskHeap = new DroneTaskNativeMinHeap
                {
                    Nodes = taskPriorityHeap,
                    Count = 0
                };

                for (int moduleIndex = 0; moduleIndex < scanModuleCount; moduleIndex++)
                {
                    BaseModule module = manager.GetSpawnedBaseModuleAt(moduleIndex);
                    if (module == null || !module.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    if (IsEligibleRepairTarget(hubGrid, module, dispatchIntegrityThreshold))
                    {
                        Vector3 modulePosition = module.transform.position;
                        float distanceSq = (hubPosition - modulePosition).sqrMagnitude;
                        float taskCriticality = ResolveCriticalityWeight(module);
                        float taskScore = ComputeTaskAssignmentScoreFromDistanceSq(distanceSq, taskCriticality);
                        RepairTaskCandidate candidate = new RepairTaskCandidate
                        {
                            Kind = DroneFleetTaskKind.RepairModule,
                            Module = module,
                            ModuleIndex = moduleIndex,
                            Position = modulePosition,
                            Radius = 0f,
                            Score = taskScore,
                            CriticalityWeight = taskCriticality
                        };
                        TryPushTaskPriorityCandidate(ref taskHeap, in candidate, taskClaimCounts);
                        ConsiderTaskCandidate(in candidate, taskClaimCounts, ref bestTask, ref hasBestTask);
                    }

                    if (floraInteractionManager == null ||
                        module.ParasiteInfectionLevel <= 0.0001f ||
                        IsDifferentGrid(hubGrid, module) ||
                        !floraInteractionManager.TryResolveNearestModuleParasite(module, hubPosition, out FloraInteractionManager.ModuleParasiteTarget parasiteTarget))
                    {
                        continue;
                    }

                    float parasiteDistanceSq = (hubPosition - parasiteTarget.Position).sqrMagnitude;
                    float parasiteCriticality = ResolveParasiteCriticalityWeight(module, in parasiteTarget);
                    float parasiteScore = ComputeTaskAssignmentScoreFromDistanceSq(parasiteDistanceSq, parasiteCriticality);
                    RepairTaskCandidate parasiteCandidate = new RepairTaskCandidate
                    {
                        Kind = DroneFleetTaskKind.CutParasite,
                        Module = module,
                        ModuleIndex = moduleIndex,
                        Position = parasiteTarget.Position,
                        Radius = parasiteTarget.Radius,
                        Score = parasiteScore,
                        CriticalityWeight = parasiteCriticality
                    };
                    TryPushTaskPriorityCandidate(ref taskHeap, in parasiteCandidate, taskClaimCounts);
                    ConsiderTaskCandidate(in parasiteCandidate, taskClaimCounts, ref bestTask, ref hasBestTask);
                }

                if (TryResolvePriorityHeapTask(ref taskHeap, manager, taskClaimCounts, out RepairTaskCandidate heapTask))
                {
                    bestTask = heapTask;
                    hasBestTask = true;
                }

                if (hasBestTask)
                {
                    taskClaimCounts[bestTask.ModuleIndex] = taskClaimCounts[bestTask.ModuleIndex] + 1;
                    task = new DroneFleetTask(
                        bestTask.Kind,
                        bestTask.Module,
                        bestTask.Position,
                        bestTask.Radius);
                    assignmentScore = bestTask.Score;
                    criticalityWeight = bestTask.CriticalityWeight;
                    assignedTask = true;
                }
            }
            finally
            {
                ReleaseDroneMutationGuard(taskAssignmentVault, TaskAssignmentMutationGuardMask);
            }

            PublishSnapshot();
            return assignedTask;
        }

        public static float ComputeTaskAssignmentScore(float distanceMeters, float criticalityWeight)
        {
            float clampedDistance = Mathf.Max(MinimumScoreDistanceMeters, distanceMeters);
            return math.rcp(clampedDistance) * Mathf.Max(0.1f, criticalityWeight);
        }

        private static float ComputeTaskAssignmentScoreFromDistanceSq(float distanceSq, float criticalityWeight)
        {
            float inverseDistance = math.rsqrt(math.max(MinimumScoreDistanceMetersSq, distanceSq));
            return inverseDistance * math.max(0.1f, criticalityWeight);
        }

        private static void EnsureInitialized()
        {
            EnsureDockingSignalLanes();
            EnsureRuntimeRegistryCache();
            EnsureDroneShaderPropertyIds();
            if (!TryOpenDroneVaultBuffer(
                    s_CachedDataVault,
                    in s_DroneStatesHandle,
                    BufferID.ShinobuDroneFleetStates,
                    HeadlessDroneCapacity,
                    out NativeArray<HeadlessDroneState> _))
            {
                AllocateHeadlessNativeMemory();
            }

            if (!s_Initialized)
            {
                HectonSubmarineOsEvents.Unregister(s_SubmarineOsEventBridge);
                HectonSubmarineOsEvents.Register(s_SubmarineOsEventBridge);
                s_Initialized = true;
            }

            EnsureStorageReservationCommitResolvedBridge();
            TryRegisterHeadlessDriver();
        }

        private static void EnsureStorageReservationCommitResolvedBridge()
        {
            int listenerGeneration = ThreadSafeCommandQueue.StorageReservationCommitListenerGeneration;
            if (s_StorageReservationCommitResolvedListenerGeneration == listenerGeneration)
                return;

            ThreadSafeCommandQueue.Unregister(s_StorageReservationCommitResolvedBridge);
            if (ThreadSafeCommandQueue.Register(s_StorageReservationCommitResolvedBridge))
            {
                s_StorageReservationCommitResolvedListenerGeneration = ThreadSafeCommandQueue.StorageReservationCommitListenerGeneration;
                return;
            }

            s_StorageReservationCommitResolvedListenerGeneration = -1;
        }

        private static void EnsureRuntimeRegistryCache()
        {
            if (s_RuntimeRegistryCacheInitialized)
                return;

            s_CachedLogisticsService = GlobalRegistry.Logistics;
            s_CachedPlayerRuntime = GlobalRegistry.Player;
            s_CachedPlayerInventoryService = GlobalRegistry.PlayerInventory;
            s_CachedSubmarineRuntime = GlobalRegistry.Submarine;
            s_CachedFluidRuntime = GlobalRegistry.FluidSurfaceCurrent;
            WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref s_CachedVegetationBridge);
            s_CachedDataVault = GlobalRegistry.DataVault;
            s_CachedVoxelSdfReadLeaseModel = GlobalRegistry.VoxelSonarSdf as IVoxelSonarSdfReadLeaseModel;
            HectonDroneFleetEvents.BindDataVault(s_CachedDataVault);
            RepairDroneTorchAcousticEvents.BindDataVault(s_CachedDataVault);
            s_RuntimeRegistryCacheInitialized = true;
        }

        private static void CacheRuntimeRegistryService(
            GlobalRegistryServiceSlot serviceSlot,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Logistics:
                    s_CachedLogisticsService = currentService as ILogisticsService;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    s_CachedPlayerRuntime = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.PlayerInventory:
                    s_CachedPlayerInventoryService = currentService as IPlayerInventoryService;
                    break;
                case GlobalRegistryServiceSlot.Submarine:
                    s_CachedSubmarineRuntime = currentService as ISubmarineRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.FluidRuntime:
                    s_CachedFluidRuntime = currentService as IFluidSurfaceCurrentReadModel;
                    break;
                case GlobalRegistryServiceSlot.MapMagicVegetationRuntime:
                    s_CachedVegetationBridge = currentService as HectonMapMagicVegetationBridge;
                    WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref s_CachedVegetationBridge);
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    RebindDroneDataVault(currentService is IDataVault currentVault ? currentVault : null);
                    break;
                case GlobalRegistryServiceSlot.VoxelEngineRuntime:
                    s_CachedVoxelSdfReadLeaseModel = currentService as IVoxelSonarSdfReadLeaseModel;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterHeadlessDriverLanes();
                    s_HeadlessDriverRegistered = false;
                    if (currentService != null)
                        TryRegisterHeadlessDriver();
                    break;
            }
        }

        internal static void BindFloraInteractionManager(FloraInteractionManager source)
        {
            if (source != null)
                s_CachedFloraInteractionManager = source;
        }

        internal static void ClearFloraInteractionManager(FloraInteractionManager source)
        {
            if (source == null || ReferenceEquals(s_CachedFloraInteractionManager, source))
                s_CachedFloraInteractionManager = null;
        }

        internal static void BindVegetationBridge(HectonMapMagicVegetationBridge source)
        {
            if (source != null)
                s_CachedVegetationBridge = source;
        }

        internal static void ClearVegetationBridge(HectonMapMagicVegetationBridge source)
        {
            if (source == null || ReferenceEquals(s_CachedVegetationBridge, source))
                s_CachedVegetationBridge = null;
        }

        private static void EnsureDroneShaderPropertyIds()
        {
            if (s_DroneShaderPropertyIdsInitialized)
                return;

            s_DroneMatricesPropertyId = Shader.PropertyToID("_DroneMatrices");
            s_InstanceMatricesPropertyId = Shader.PropertyToID("_InstanceMatrices");
            s_DroneStatesPropertyId = Shader.PropertyToID("_DroneStates");
            s_DroneRenderInstancesPropertyId = Shader.PropertyToID("_DroneRenderInstances");
            s_DroneVisibleInstancesPropertyId = Shader.PropertyToID("_DroneVisibleInstances");
            s_DroneVisibleIndicesPropertyId = Shader.PropertyToID("_DroneVisibleIndices");
            s_IndirectArgsBufferPropertyId = Shader.PropertyToID("_IndirectArgsBuffer");
            s_CameraFrustumPlanesPropertyId = Shader.PropertyToID("_CameraFrustumPlanes");
            s_DroneCountPropertyId = Shader.PropertyToID("_DroneCount");
            s_DroneCullRadiusPropertyId = Shader.PropertyToID("_DroneCullRadius");
            s_CameraPositionPropertyId = Shader.PropertyToID("_CameraPositionWS");
            s_DroneRenderDistanceSqPropertyId = Shader.PropertyToID("_DroneRenderDistanceSq");
            s_PhantomMatricesPropertyId = Shader.PropertyToID("_PhantomMatrices");
            s_PhantomColorsPropertyId = Shader.PropertyToID("_PhantomColors");
            s_PhantomAnchorPropertyId = Shader.PropertyToID("_PhantomAnchorWS");
            s_PhantomTimePropertyId = Shader.PropertyToID("_PhantomTime");
            s_PhantomCountPropertyId = Shader.PropertyToID("_PhantomCount");
            s_PhantomBaseRadiusPropertyId = Shader.PropertyToID("_PhantomBaseRadius");
            s_PhantomVerticalAmplitudePropertyId = Shader.PropertyToID("_PhantomVerticalAmplitude");
            s_PhantomScalePropertyId = Shader.PropertyToID("_PhantomScale");
            s_PhantomCapacityPropertyId = Shader.PropertyToID("_PhantomCapacity");
            s_DroneProceduralCameraOriginPropertyId = Shader.PropertyToID("_DroneCameraOriginWS");
            s_UsePhantomColorsPropertyId = Shader.PropertyToID("_UsePhantomColors");
            s_DroneShaderPropertyIdsInitialized = true;
        }

        private static void EnsureDockingSignalLanes()
        {
            if (s_DockingSignalLanesConfigured)
                return;

            SignalCorridorRuntime.EnsureInitialized();
            SignalBus<DroneFleetRepairServiceSignal>.Configure(64, maxFrameSignals: 64, lowTierFrameSignals: 16, laneHash: 0x44524D52u);
            SignalBus<DroneFleetRepairServiceSignal>.EnsureInitialized();
            SignalBus<DroneFleetMiningServiceSignal>.Configure(64, maxFrameSignals: 64, lowTierFrameSignals: 16, laneHash: 0x44524D4Eu);
            SignalBus<DroneFleetMiningServiceSignal>.EnsureInitialized();
            SignalBus<DroneFleetInventoryTransactionSignal>.Configure(64, maxFrameSignals: 64, lowTierFrameSignals: 16, laneHash: 0x4452494Eu);
            SignalBus<DroneFleetInventoryTransactionSignal>.EnsureInitialized();
            SignalBus<ItemAcquiredSignal>.EnsureInitialized();
            SignalBus<SystemGlitchSignal>.EnsureInitialized();
            s_DockingSignalLanesConfigured = true;
        }

        private static void AllocateHeadlessNativeMemory()
        {
            if (!ValidateDroneFleetDtoLayouts())
                return;

            EnsureDroneVaultBuffer<HeadlessDroneState>(BufferID.ShinobuDroneFleetStates, HeadlessDroneCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneStatesHandle);
            EnsureDroneVaultBuffer<HeadlessDroneState>(BufferID.ShinobuDroneFleetStateBackBuffer, HeadlessDroneCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneStateBackBufferHandle);
            EnsureDroneVaultBuffer<float4x4>(BufferID.ShinobuDroneFleetRenderMatrices, HeadlessDroneCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneRenderMatricesHandle);
            EnsureDroneVaultBuffer<float4x4>(BufferID.ShinobuDroneFleetRenderMatrixBackBuffer, HeadlessDroneCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneRenderMatrixBackBufferHandle);
            EnsureDroneVaultBuffer<DroneRenderInstance>(BufferID.ShinobuDroneFleetRenderInstances, HeadlessDroneCapacity, NativeArrayOptions.ClearMemory, ref s_DroneRenderInstancesHandle);
            EnsureDroneVaultBuffer<DroneCullingStateGpu>(BufferID.DroneFleetCullingStates, HeadlessDroneCapacity, NativeArrayOptions.ClearMemory, ref s_DroneCullingStatesHandle);
            EnsureDroneVaultBuffer<float3>(BufferID.ShinobuDroneFleetPositionsSoA, HeadlessDroneCapacity, NativeArrayOptions.ClearMemory, ref s_DronePositionsSoAHandle);
            EnsureDroneVaultBuffer<byte>(BufferID.ShinobuDroneFleetStateBytes, HeadlessDroneCapacity, NativeArrayOptions.ClearMemory, ref s_DroneStateBytesHandle);
            EnsureDroneVaultBuffer<DroneFleetBlackBoxEntry>(BufferID.ShinobuDroneFleetBlackBox, DroneFleetBlackBoxFrameCapacity, NativeArrayOptions.ClearMemory, ref s_DroneBlackBoxHandle);
            EnsureDroneVaultBuffer<DroneFleetTuningConstants>(BufferID.ShinobuDroneFleetTuningConstants, 1, NativeArrayOptions.ClearMemory, ref s_DroneTuningConstantsHandle);
            EnsureDroneVaultBuffer<PathWaypointDTO>(BufferID.ShinobuDroneFleetMacroWaypoints, HeadlessDroneCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneMacroWaypointsHandle);
            EnsureDroneVaultBuffer<byte>(BufferID.ShinobuDroneFleetMacroWaypointStates, HeadlessDroneCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneMacroWaypointStatesHandle);
            EnsureDroneVaultBuffer<DroneNativeMinHeapNode>(BufferID.ShinobuDroneFleetAStarOpenHeap, DroneAStarScratchNodeCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneAStarOpenHeapHandle);
            EnsureDroneVaultBuffer<float>(BufferID.ShinobuDroneFleetAStarGCosts, DroneAStarScratchNodeCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneAStarGCostsHandle);
            EnsureDroneVaultBuffer<int>(BufferID.ShinobuDroneFleetAStarCameFrom, DroneAStarScratchNodeCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneAStarCameFromHandle);
            EnsureDroneVaultBuffer<byte>(BufferID.ShinobuDroneFleetAStarNodeStates, DroneAStarScratchNodeCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneAStarNodeStatesHandle);
            EnsureDroneVaultBuffer<int>(BufferID.ShinobuDroneFleetMacroRouteNodes, DroneAStarRouteNodeCapacity, NativeArrayOptions.ClearMemory, ref s_DroneMacroRouteNodesHandle);
            EnsureDroneVaultBuffer<byte>(BufferID.ShinobuDroneFleetMacroRouteCounts, HeadlessDroneCapacity, NativeArrayOptions.ClearMemory, ref s_DroneMacroRouteCountsHandle);
            EnsureDroneVaultBuffer<DroneAStarTelemetry>(BufferID.ShinobuDroneFleetAStarTelemetry, DroneAStarTelemetryCapacity, NativeArrayOptions.ClearMemory, ref s_DroneAStarTelemetryHandle);
            EnsureDroneVaultBuffer<DroneAStarPersistentState>(DroneFleetAStarPersistentStatesBufferId, HeadlessDroneCapacity, NativeArrayOptions.ClearMemory, ref s_DroneAStarPersistentStatesHandle);
            EnsureDroneVaultBuffer<int>(BufferID.ShinobuDroneFleetTaskClaimOwners, HeadlessTaskCapacity, NativeArrayOptions.ClearMemory, ref s_HeadlessTaskClaimOwnersHandle);
            EnsureDroneVaultBuffer<int>(BufferID.ShinobuDroneFleetTelemetryAccumulator, (int)DroneFleetTelemetryAccumulatorSlot.Count, NativeArrayOptions.ClearMemory, ref s_FleetTelemetryAccumulatorHandle);
            EnsureDroneVaultBuffer<DroneAssignmentTaskDTO>(BufferID.ShinobuDroneFleetTaskPriorityHeap, HeadlessTaskCapacity, NativeArrayOptions.ClearMemory, ref s_DroneTaskPriorityHeapHandle);
            EnsureDroneVaultBuffer<DroneStateDTO>(DroneFleetStateDtoBufferId, HeadlessDroneCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneStateDtosHandle);
            EnsureDroneVaultBuffer<DroneTargetDTO>(DroneFleetTargetDtoBufferId, HeadlessDroneCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneTargetDtosHandle);
            EnsureDroneVaultBuffer<DroneAssignmentTaskDTO>(DroneFleetAssignmentTasksBufferId, HeadlessTaskCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneAssignmentTasksHandle);
            EnsureDroneVaultBuffer<DroneProceduralIndirectArgsDTO>(DroneFleetProceduralArgsBufferId, 1, NativeArrayOptions.UninitializedMemory, ref s_DroneProceduralArgsHandle);
            EnsureDroneVaultBuffer<DroneServiceCommand>(DroneFleetServiceCommandsBufferId, DroneServiceCommandCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneServiceCommandsHandle);
            EnsureDroneVaultBuffer<DroneServiceCommandCursor>(DroneFleetServiceCommandCursorBufferId, 1, NativeArrayOptions.ClearMemory, ref s_DroneServiceCommandCursorHandle);
            EnsureDroneVaultBuffer<int>(DroneFleetSpatialBucketHeadsBufferId, DroneSpatialBucketCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneSpatialBucketHeadsHandle);
            EnsureDroneVaultBuffer<int>(DroneFleetSpatialNextIndicesBufferId, HeadlessDroneCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneSpatialNextIndicesHandle);
            EnsureDroneVaultBuffer<int>(DroneFleetSpatialKeysBufferId, HeadlessDroneCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneSpatialKeysHandle);
            EnsureDroneVaultBuffer<DroneChassisSpecDTO>(DroneFleetChassisSpecsBufferId, DroneChassisSpecCapacity, NativeArrayOptions.ClearMemory, ref s_DroneChassisSpecsHandle);
            AllocateDroneTransactionMemory();
            if (!HeadlessNativeBuffersCreated())
            {
                ReleaseHeadlessNativeMemory();
                return;
            }

            EnsureHeadlessManagedMemory();
            ClearHeadlessManagedState();
            ClearAllHeadlessSlots();
            WriteDefaultDroneTuningConstants();
            ClearDroneChassisSpecs();
            ClearDroneMacroWaypointStates();
        }

        private static bool ValidateDroneFleetDtoLayouts()
        {
            return DroneFleetLayoutSentinel.ValidateDroneStateDTO() &&
                   DroneFleetLayoutSentinel.ValidateDroneTargetDTO() &&
                   DroneFleetLayoutSentinel.ValidateDroneTaskDTO() &&
                   DroneFleetLayoutSentinel.ValidateDroneAssignmentTaskDTO() &&
                   DroneFleetLayoutSentinel.ValidateDroneChassisSpecDTO() &&
                   DroneFleetLayoutSentinel.ValidateDroneSnapshotPayload() &&
                   ValidateDroneRenderUploadLayouts();
        }

        private static bool ValidateDroneRenderUploadLayouts()
        {
            int renderInstanceStride = UnsafeUtility.SizeOf<DroneRenderInstance>();
            int cullingStateStride = UnsafeUtility.SizeOf<DroneCullingStateGpu>();
            return renderInstanceStride == 80 &&
                   (renderInstanceStride & 7) == 0 &&
                   cullingStateStride == 16 &&
                   (cullingStateStride & 7) == 0;
        }

        private static bool HeadlessNativeBuffersCreated()
        {
            return TryOpenDroneCoreBuffers(
                       out NativeArray<HeadlessDroneState> _,
                       out NativeArray<HeadlessDroneState> _,
                       out NativeArray<float4x4> _,
                       out NativeArray<float4x4> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneRenderInstancesHandle,
                       BufferID.ShinobuDroneFleetRenderInstances,
                       HeadlessDroneCapacity,
                       out NativeArray<DroneRenderInstance> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneCullingStatesHandle,
                       BufferID.DroneFleetCullingStates,
                       HeadlessDroneCapacity,
                       out NativeArray<DroneCullingStateGpu> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DronePositionsSoAHandle,
                       BufferID.ShinobuDroneFleetPositionsSoA,
                       HeadlessDroneCapacity,
                       out NativeArray<float3> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneStateBytesHandle,
                       BufferID.ShinobuDroneFleetStateBytes,
                       HeadlessDroneCapacity,
                       out NativeArray<byte> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneBlackBoxHandle,
                       BufferID.ShinobuDroneFleetBlackBox,
                       DroneFleetBlackBoxFrameCapacity,
                       out NativeArray<DroneFleetBlackBoxEntry> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneTuningConstantsHandle,
                       BufferID.ShinobuDroneFleetTuningConstants,
                       1,
                       out NativeArray<DroneFleetTuningConstants> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneMacroWaypointsHandle,
                       BufferID.ShinobuDroneFleetMacroWaypoints,
                       HeadlessDroneCapacity,
                       out NativeArray<PathWaypointDTO> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneMacroWaypointStatesHandle,
                       BufferID.ShinobuDroneFleetMacroWaypointStates,
                       HeadlessDroneCapacity,
                       out NativeArray<byte> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneAStarOpenHeapHandle,
                       BufferID.ShinobuDroneFleetAStarOpenHeap,
                       DroneAStarScratchNodeCapacity,
                       out NativeArray<DroneNativeMinHeapNode> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneAStarGCostsHandle,
                       BufferID.ShinobuDroneFleetAStarGCosts,
                       DroneAStarScratchNodeCapacity,
                       out NativeArray<float> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneAStarCameFromHandle,
                       BufferID.ShinobuDroneFleetAStarCameFrom,
                       DroneAStarScratchNodeCapacity,
                       out NativeArray<int> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneAStarNodeStatesHandle,
                       BufferID.ShinobuDroneFleetAStarNodeStates,
                       DroneAStarScratchNodeCapacity,
                       out NativeArray<byte> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneMacroRouteNodesHandle,
                       BufferID.ShinobuDroneFleetMacroRouteNodes,
                       DroneAStarRouteNodeCapacity,
                       out NativeArray<int> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneMacroRouteCountsHandle,
                       BufferID.ShinobuDroneFleetMacroRouteCounts,
                       HeadlessDroneCapacity,
                       out NativeArray<byte> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneAStarTelemetryHandle,
                       BufferID.ShinobuDroneFleetAStarTelemetry,
                       DroneAStarTelemetryCapacity,
                       out NativeArray<DroneAStarTelemetry> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneAStarPersistentStatesHandle,
                       DroneFleetAStarPersistentStatesBufferId,
                       HeadlessDroneCapacity,
                       out NativeArray<DroneAStarPersistentState> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_HeadlessTaskClaimOwnersHandle,
                       BufferID.ShinobuDroneFleetTaskClaimOwners,
                       HeadlessTaskCapacity,
                       out NativeArray<int> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_FleetTelemetryAccumulatorHandle,
                       BufferID.ShinobuDroneFleetTelemetryAccumulator,
                       (int)DroneFleetTelemetryAccumulatorSlot.Count,
                       out NativeArray<int> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneTaskPriorityHeapHandle,
                       BufferID.ShinobuDroneFleetTaskPriorityHeap,
                       HeadlessTaskCapacity,
                       out NativeArray<DroneAssignmentTaskDTO> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneStateDtosHandle,
                       DroneFleetStateDtoBufferId,
                       HeadlessDroneCapacity,
                       out NativeArray<DroneStateDTO> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneTargetDtosHandle,
                       DroneFleetTargetDtoBufferId,
                       HeadlessDroneCapacity,
                       out NativeArray<DroneTargetDTO> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneAssignmentTasksHandle,
                       DroneFleetAssignmentTasksBufferId,
                       HeadlessTaskCapacity,
                       out NativeArray<DroneAssignmentTaskDTO> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneProceduralArgsHandle,
                       DroneFleetProceduralArgsBufferId,
                       1,
                       out NativeArray<DroneProceduralIndirectArgsDTO> _) &&
                   TryResolveDroneServiceCommandBuffers(out _, out _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneSpatialBucketHeadsHandle,
                       DroneFleetSpatialBucketHeadsBufferId,
                       DroneSpatialBucketCapacity,
                       out NativeArray<int> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneSpatialNextIndicesHandle,
                       DroneFleetSpatialNextIndicesBufferId,
                       HeadlessDroneCapacity,
                       out NativeArray<int> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneSpatialKeysHandle,
                       DroneFleetSpatialKeysBufferId,
                       HeadlessDroneCapacity,
                       out NativeArray<int> _) &&
                   TryOpenDroneVaultBuffer(
                       s_CachedDataVault,
                       in s_DroneChassisSpecsHandle,
                       DroneFleetChassisSpecsBufferId,
                       DroneChassisSpecCapacity,
                       out NativeArray<DroneChassisSpecDTO> _)
                    ;
        }

        private static void ReleaseHeadlessNativeMemory()
        {
            ReleaseHeadlessVaultHandles(s_CachedDataVault);
            DropHeadlessManagedMemory();
        }

        internal static void ClearSceneTransitionRuntimeState()
        {
            CompletePendingHeadlessJobForReset();
            ReleaseHeadlessSdfReadLease();
            ClearAllHeadlessSlots();
            ClearHeadlessManagedState();
            s_HeadlessStasisSlotCount = 0;
            s_LastSnapshot = default;
            s_LastFleetStatusSnapshot = default;
        }

        internal static int ConfiguredDroneBoneJointCount => s_ConfiguredDroneBoneJointCount;

        internal static int ConfiguredDroneAttachmentCount => s_ConfiguredDroneAttachmentCount;

        internal static bool TryGetConfiguredDroneBoneTableInfo(
            out uint droneId,
            out uint bakeHash,
            out float qualityWeight,
            out int jointCount)
        {
            droneId = s_ConfiguredDroneBoneDroneId;
            bakeHash = s_ConfiguredDroneBoneBakeHash;
            qualityWeight = s_ConfiguredDroneBoneQualityWeight;
            jointCount = s_ConfiguredDroneBoneJointCount;
            return jointCount > 0 && DroneBoneMetadata.ValidateStaticLayout();
        }

        internal static bool TryGetConfiguredDroneAttachmentTableInfo(
            out uint droneId,
            out uint bakeHash,
            out float qualityWeight,
            out int attachmentCount)
        {
            droneId = s_ConfiguredDroneAttachmentDroneId;
            bakeHash = s_ConfiguredDroneAttachmentBakeHash;
            qualityWeight = s_ConfiguredDroneAttachmentQualityWeight;
            attachmentCount = s_ConfiguredDroneAttachmentCount;
            return attachmentCount > 0 && DroneAttachmentMetadata.ValidateStaticLayout();
        }

        internal static bool TryCopyConfiguredDroneBoneJointTable(
            NativeArray<DroneBoneJointRuntimeData> destination,
            out int jointCount)
        {
            jointCount = 0;
            if (!destination.IsCreated ||
                s_ConfiguredDroneBoneJointCount <= 0 ||
                destination.Length < s_ConfiguredDroneBoneJointCount ||
                !DroneBoneMetadata.ValidateStaticLayout())
            {
                return false;
            }

            int count = s_ConfiguredDroneBoneJointCount;
            for (int i = 0; i < count; i++)
                destination[i] = s_ConfiguredDroneBoneJointTable[i];

            jointCount = count;
            return true;
        }

        internal static bool TryCopyConfiguredDroneAttachmentTable(
            NativeArray<DroneAttachmentRuntimeData> destination,
            out int attachmentCount)
        {
            attachmentCount = 0;
            if (!destination.IsCreated ||
                s_ConfiguredDroneAttachmentCount <= 0 ||
                destination.Length < s_ConfiguredDroneAttachmentCount ||
                !DroneAttachmentMetadata.ValidateStaticLayout())
            {
                return false;
            }

            int count = s_ConfiguredDroneAttachmentCount;
            for (int i = 0; i < count; i++)
                destination[i] = s_ConfiguredDroneAttachmentTable[i];

            attachmentCount = count;
            return true;
        }

        private static void CacheConfiguredDroneBoneTable(GameObject dronePrefab)
        {
            ClearConfiguredDroneBoneTable();
            if (dronePrefab == null || !DroneBoneMetadata.ValidateStaticLayout())
                return;

            if (!dronePrefab.TryGetComponent(out DroneBoneMetadata metadata) || metadata == null)
                return;

            int jointCount = metadata.JointCount;
            if (jointCount <= 0 || jointCount > s_ConfiguredDroneBoneJointTable.Length)
                return;

            for (int i = 0; i < jointCount; i++)
            {
                if (!metadata.TryExportRuntimeJoint(i, out s_ConfiguredDroneBoneJointTable[i]))
                {
                    ClearConfiguredDroneBoneTable();
                    return;
                }
            }

            s_ConfiguredDroneBoneJointCount = jointCount;
            s_ConfiguredDroneBoneDroneId = metadata.DroneId;
            s_ConfiguredDroneBoneBakeHash = metadata.BakeHash;
            s_ConfiguredDroneBoneQualityWeight = metadata.AuthoredQualityWeight;
        }

        private static void CacheConfiguredDroneAttachmentTable(GameObject dronePrefab)
        {
            ClearConfiguredDroneAttachmentTable();
            if (dronePrefab == null || !DroneAttachmentMetadata.ValidateStaticLayout())
                return;

            if (!dronePrefab.TryGetComponent(out DroneAttachmentMetadata metadata) || metadata == null)
                return;

            int attachmentCount = metadata.DescriptorCount;
            if (attachmentCount <= 0 || attachmentCount > s_ConfiguredDroneAttachmentTable.Length)
                return;

            for (int i = 0; i < attachmentCount; i++)
            {
                if (!metadata.TryExportRuntimeAttachment(i, out s_ConfiguredDroneAttachmentTable[i]))
                {
                    ClearConfiguredDroneAttachmentTable();
                    return;
                }
            }

            s_ConfiguredDroneAttachmentCount = attachmentCount;
            s_ConfiguredDroneAttachmentDroneId = metadata.DroneId;
            s_ConfiguredDroneAttachmentBakeHash = metadata.BakeHash;
            s_ConfiguredDroneAttachmentQualityWeight = metadata.AuthoredQualityWeight;
        }

        private static void ClearConfiguredDroneBoneTable()
        {
            int count = math.min(s_ConfiguredDroneBoneJointCount, s_ConfiguredDroneBoneJointTable.Length);
            for (int i = 0; i < count; i++)
                s_ConfiguredDroneBoneJointTable[i] = default;

            s_ConfiguredDroneBoneJointCount = 0;
            s_ConfiguredDroneBoneDroneId = 0u;
            s_ConfiguredDroneBoneBakeHash = 0u;
            s_ConfiguredDroneBoneQualityWeight = 0f;
        }

        private static void ClearConfiguredDroneAttachmentTable()
        {
            int count = math.min(s_ConfiguredDroneAttachmentCount, s_ConfiguredDroneAttachmentTable.Length);
            for (int i = 0; i < count; i++)
                s_ConfiguredDroneAttachmentTable[i] = default;

            s_ConfiguredDroneAttachmentCount = 0;
            s_ConfiguredDroneAttachmentDroneId = 0u;
            s_ConfiguredDroneAttachmentBakeHash = 0u;
            s_ConfiguredDroneAttachmentQualityWeight = 0f;
        }

        private static void ReleaseHeadlessVaultHandles(IDataVault vault)
        {
            ReleaseDroneServiceCommandMutationGuard();
            ReleaseDroneHeadlessJobMutationGuard();
            ReleaseDroneVaultHandle(vault, ref s_DroneStatesHandle, BufferID.ShinobuDroneFleetStates);
            ReleaseDroneVaultHandle(vault, ref s_DroneStateBackBufferHandle, BufferID.ShinobuDroneFleetStateBackBuffer);
            ReleaseDroneVaultHandle(vault, ref s_DroneRenderMatricesHandle, BufferID.ShinobuDroneFleetRenderMatrices);
            ReleaseDroneVaultHandle(vault, ref s_DroneRenderMatrixBackBufferHandle, BufferID.ShinobuDroneFleetRenderMatrixBackBuffer);
            ReleaseDroneVaultHandle(vault, ref s_DroneRenderInstancesHandle, BufferID.ShinobuDroneFleetRenderInstances);
            ReleaseDroneVaultHandle(vault, ref s_DroneCullingStatesHandle, BufferID.DroneFleetCullingStates);
            ReleaseDroneVaultHandle(vault, ref s_DronePositionsSoAHandle, BufferID.ShinobuDroneFleetPositionsSoA);
            ReleaseDroneVaultHandle(vault, ref s_DroneStateBytesHandle, BufferID.ShinobuDroneFleetStateBytes);
            ReleaseDroneVaultHandle(vault, ref s_DroneBlackBoxHandle, BufferID.ShinobuDroneFleetBlackBox);
            ReleaseDroneVaultHandle(vault, ref s_DroneTuningConstantsHandle, BufferID.ShinobuDroneFleetTuningConstants);
            ReleaseDroneVaultHandle(vault, ref s_DroneMacroWaypointsHandle, BufferID.ShinobuDroneFleetMacroWaypoints);
            ReleaseDroneVaultHandle(vault, ref s_DroneMacroWaypointStatesHandle, BufferID.ShinobuDroneFleetMacroWaypointStates);
            ReleaseDroneVaultHandle(vault, ref s_DroneAStarOpenHeapHandle, BufferID.ShinobuDroneFleetAStarOpenHeap);
            ReleaseDroneVaultHandle(vault, ref s_DroneAStarGCostsHandle, BufferID.ShinobuDroneFleetAStarGCosts);
            ReleaseDroneVaultHandle(vault, ref s_DroneAStarCameFromHandle, BufferID.ShinobuDroneFleetAStarCameFrom);
            ReleaseDroneVaultHandle(vault, ref s_DroneAStarNodeStatesHandle, BufferID.ShinobuDroneFleetAStarNodeStates);
            ReleaseDroneVaultHandle(vault, ref s_DroneMacroRouteNodesHandle, BufferID.ShinobuDroneFleetMacroRouteNodes);
            ReleaseDroneVaultHandle(vault, ref s_DroneMacroRouteCountsHandle, BufferID.ShinobuDroneFleetMacroRouteCounts);
            ReleaseDroneVaultHandle(vault, ref s_DroneAStarTelemetryHandle, BufferID.ShinobuDroneFleetAStarTelemetry);
            ReleaseDroneVaultHandle(vault, ref s_DroneAStarPersistentStatesHandle, DroneFleetAStarPersistentStatesBufferId);
            ReleaseDroneVaultHandle(vault, ref s_HeadlessTaskClaimOwnersHandle, BufferID.ShinobuDroneFleetTaskClaimOwners);
            ReleaseDroneVaultHandle(vault, ref s_FleetTelemetryAccumulatorHandle, BufferID.ShinobuDroneFleetTelemetryAccumulator);
            ReleaseDroneVaultHandle(vault, ref s_DroneTaskPriorityHeapHandle, BufferID.ShinobuDroneFleetTaskPriorityHeap);
            ReleaseDroneVaultHandle(vault, ref s_DroneStateDtosHandle, DroneFleetStateDtoBufferId);
            ReleaseDroneVaultHandle(vault, ref s_DroneTargetDtosHandle, DroneFleetTargetDtoBufferId);
            ReleaseDroneVaultHandle(vault, ref s_DroneAssignmentTasksHandle, DroneFleetAssignmentTasksBufferId);
            ReleaseDroneVaultHandle(vault, ref s_DroneProceduralArgsHandle, DroneFleetProceduralArgsBufferId);
            ReleaseDroneVaultHandle(vault, ref s_DroneServiceCommandsHandle, DroneFleetServiceCommandsBufferId);
            ReleaseDroneVaultHandle(vault, ref s_DroneServiceCommandCursorHandle, DroneFleetServiceCommandCursorBufferId);
            ReleaseDroneVaultHandle(vault, ref s_DroneSpatialBucketHeadsHandle, DroneFleetSpatialBucketHeadsBufferId);
            ReleaseDroneVaultHandle(vault, ref s_DroneSpatialNextIndicesHandle, DroneFleetSpatialNextIndicesBufferId);
            ReleaseDroneVaultHandle(vault, ref s_DroneSpatialKeysHandle, DroneFleetSpatialKeysBufferId);
            ReleaseDroneVaultHandle(vault, ref s_DroneChassisSpecsHandle, DroneFleetChassisSpecsBufferId);
            ReleaseDroneTransactionMemory(vault);
            ClearHeadlessManagedState();
        }

        private static void EnsureHeadlessManagedMemory()
        {
            if (s_DroneHubs == null || s_DroneHubs.Length != HeadlessDroneCapacity)
                s_DroneHubs = new RepairDroneHub[HeadlessDroneCapacity]; // COLD ALLOC: RepairDroneHub[512] - managed hub owner lookup for late-frame service commits - owner: DroneFleetManager
            if (s_DroneSlotDroneIds == null || s_DroneSlotDroneIds.Length != HeadlessDroneCapacity)
                s_DroneSlotDroneIds = new int[HeadlessDroneCapacity]; // COLD ALLOC: int[512] - managed active drone id slots safe during job execution - owner: DroneFleetManager
            if (s_DroneSlotDestroyed == null || s_DroneSlotDestroyed.Length != HeadlessDroneCapacity)
                s_DroneSlotDestroyed = new bool[HeadlessDroneCapacity]; // COLD ALLOC: bool[512] - permanently consumed suicide-weld slots - owner: DroneFleetManager
            if (s_PendingAbortBySlot == null || s_PendingAbortBySlot.Length != HeadlessDroneCapacity)
                s_PendingAbortBySlot = new bool[HeadlessDroneCapacity]; // COLD ALLOC: bool[512] - deferred abort control flags - owner: DroneFleetManager
            if (s_PendingReleaseBySlot == null || s_PendingReleaseBySlot.Length != HeadlessDroneCapacity)
                s_PendingReleaseBySlot = new bool[HeadlessDroneCapacity]; // COLD ALLOC: bool[512] - deferred release control flags - owner: DroneFleetManager
            if (s_PendingHostileBySlot == null || s_PendingHostileBySlot.Length != HeadlessDroneCapacity)
                s_PendingHostileBySlot = new bool[HeadlessDroneCapacity]; // COLD ALLOC: bool[512] - deferred Logic-Leech hijack flags - owner: DroneFleetManager
            if (s_PendingResupplyGrantBySlot == null || s_PendingResupplyGrantBySlot.Length != HeadlessDroneCapacity)
                s_PendingResupplyGrantBySlot = new bool[HeadlessDroneCapacity]; // COLD ALLOC: bool[512] - command-queue storage commit success acks - owner: DroneFleetManager
            if (s_PendingResupplyFailureBySlot == null || s_PendingResupplyFailureBySlot.Length != HeadlessDroneCapacity)
                s_PendingResupplyFailureBySlot = new bool[HeadlessDroneCapacity]; // COLD ALLOC: bool[512] - command-queue storage commit failure acks - owner: DroneFleetManager
            if (s_PendingResupplyReservationIdsBySlot == null || s_PendingResupplyReservationIdsBySlot.Length != HeadlessDroneCapacity)
                s_PendingResupplyReservationIdsBySlot = new int[HeadlessDroneCapacity]; // COLD ALLOC: int[512] - expected storage reservation ids for deferred resupply acks - owner: DroneFleetManager
            if (s_DroneMiningCommitFailureReasonsBySlot == null || s_DroneMiningCommitFailureReasonsBySlot.Length != HeadlessDroneCapacity)
                s_DroneMiningCommitFailureReasonsBySlot = new byte[HeadlessDroneCapacity]; // COLD ALLOC: byte[512] - per-slot mining commit failure latch - owner: DroneFleetManager
            if (s_TargetModulesByDroneSlot == null || s_TargetModulesByDroneSlot.Length != HeadlessDroneCapacity)
                s_TargetModulesByDroneSlot = new BaseModule[HeadlessDroneCapacity]; // COLD ALLOC: BaseModule[512] - managed target lookup for late-frame repair application - owner: DroneFleetManager
            if (s_TargetVoxelVolumesByDroneSlot == null || s_TargetVoxelVolumesByDroneSlot.Length != HeadlessDroneCapacity)
                s_TargetVoxelVolumesByDroneSlot = new HectonVoxelVolume[HeadlessDroneCapacity]; // COLD ALLOC: HectonVoxelVolume[512] - managed voxel target lookup for weld/carve commits - owner: DroneFleetManager
            if (s_DroneTaskKindsBySlot == null || s_DroneTaskKindsBySlot.Length != HeadlessDroneCapacity)
                s_DroneTaskKindsBySlot = new DroneFleetTaskKind[HeadlessDroneCapacity]; // COLD ALLOC: DroneFleetTaskKind[512] - managed task kind mirror for service application - owner: DroneFleetManager
            if (s_DronePositions == null || s_DronePositions.Length != HeadlessDroneCapacity)
                s_DronePositions = new Vector3[HeadlessDroneCapacity]; // COLD ALLOC: Vector3[512] - last completed drone positions for non-job contact queries - owner: DroneFleetManager
            if (s_TaskModuleRefs == null || s_TaskModuleRefs.Length != HeadlessTaskCapacity)
                s_TaskModuleRefs = new BaseModule[HeadlessTaskCapacity]; // COLD ALLOC: BaseModule[64] - native task index to managed module lookup - owner: DroneFleetManager
            if (s_TaskVoxelVolumeRefs == null || s_TaskVoxelVolumeRefs.Length != HeadlessTaskCapacity)
                s_TaskVoxelVolumeRefs = new HectonVoxelVolume[HeadlessTaskCapacity]; // COLD ALLOC: HectonVoxelVolume[64] - native task index to managed voxel lookup - owner: DroneFleetManager
            if (s_TaskKinds == null || s_TaskKinds.Length != HeadlessTaskCapacity)
                s_TaskKinds = new DroneFleetTaskKind[HeadlessTaskCapacity]; // COLD ALLOC: DroneFleetTaskKind[64] - native task index to managed task kind lookup - owner: DroneFleetManager
            if (s_PendingLaunches == null || s_PendingLaunches.Length != HeadlessPendingLaunchCapacity)
                s_PendingLaunches = new PendingDroneLaunch[HeadlessPendingLaunchCapacity]; // COLD ALLOC: PendingDroneLaunch[512] - slow-tick launch queue applied after job completion - owner: DroneFleetManager
        }

        private static void ClearHeadlessManagedState()
        {
            s_PendingLaunchCount = 0;
            s_HeadlessTaskCount = 0;
            s_DroneChassisSpecCount = 0;

            if (s_DroneSlotDroneIds != null &&
                s_DroneHubs != null &&
                s_DroneSlotDestroyed != null &&
                s_PendingAbortBySlot != null &&
                s_PendingReleaseBySlot != null &&
                s_PendingHostileBySlot != null &&
                s_PendingResupplyGrantBySlot != null &&
                s_PendingResupplyFailureBySlot != null &&
                s_PendingResupplyReservationIdsBySlot != null &&
                s_DroneMiningCommitFailureReasonsBySlot != null &&
                s_TargetModulesByDroneSlot != null &&
                s_TargetVoxelVolumesByDroneSlot != null &&
                s_DroneTaskKindsBySlot != null &&
                s_DronePositions != null)
            {
                int slotCount = s_DroneSlotDroneIds.Length;
                if (s_DroneHubs.Length < slotCount)
                    slotCount = s_DroneHubs.Length;
                if (s_DroneSlotDestroyed.Length < slotCount)
                    slotCount = s_DroneSlotDestroyed.Length;
                if (s_PendingAbortBySlot.Length < slotCount)
                    slotCount = s_PendingAbortBySlot.Length;
                if (s_PendingReleaseBySlot.Length < slotCount)
                    slotCount = s_PendingReleaseBySlot.Length;
                if (s_PendingHostileBySlot.Length < slotCount)
                    slotCount = s_PendingHostileBySlot.Length;
                if (s_PendingResupplyGrantBySlot.Length < slotCount)
                    slotCount = s_PendingResupplyGrantBySlot.Length;
                if (s_PendingResupplyFailureBySlot.Length < slotCount)
                    slotCount = s_PendingResupplyFailureBySlot.Length;
                if (s_PendingResupplyReservationIdsBySlot.Length < slotCount)
                    slotCount = s_PendingResupplyReservationIdsBySlot.Length;
                if (s_DroneMiningCommitFailureReasonsBySlot.Length < slotCount)
                    slotCount = s_DroneMiningCommitFailureReasonsBySlot.Length;
                if (s_TargetModulesByDroneSlot.Length < slotCount)
                    slotCount = s_TargetModulesByDroneSlot.Length;
                if (s_TargetVoxelVolumesByDroneSlot.Length < slotCount)
                    slotCount = s_TargetVoxelVolumesByDroneSlot.Length;
                if (s_DroneTaskKindsBySlot.Length < slotCount)
                    slotCount = s_DroneTaskKindsBySlot.Length;
                if (s_DronePositions.Length < slotCount)
                    slotCount = s_DronePositions.Length;

                for (int slot = 0; slot < slotCount; slot++)
                {
                    s_DroneSlotDroneIds[slot] = 0;
                    s_DroneHubs[slot] = null;
                    s_DroneSlotDestroyed[slot] = false;
                    s_PendingAbortBySlot[slot] = false;
                    s_PendingReleaseBySlot[slot] = false;
                    s_PendingHostileBySlot[slot] = false;
                    s_PendingResupplyGrantBySlot[slot] = false;
                    s_PendingResupplyFailureBySlot[slot] = false;
                    s_PendingResupplyReservationIdsBySlot[slot] = 0;
                    s_DroneMiningCommitFailureReasonsBySlot[slot] = DroneMiningCommitFailureNone;
                    s_TargetModulesByDroneSlot[slot] = null;
                    s_TargetVoxelVolumesByDroneSlot[slot] = null;
                    s_DroneTaskKindsBySlot[slot] = DroneFleetTaskKind.None;
                    s_DronePositions[slot] = Vector3.zero;
                }
            }

            if (s_TaskModuleRefs != null &&
                s_TaskVoxelVolumeRefs != null &&
                s_TaskKinds != null)
            {
                int taskCount = s_TaskModuleRefs.Length;
                if (s_TaskVoxelVolumeRefs.Length < taskCount)
                    taskCount = s_TaskVoxelVolumeRefs.Length;
                if (s_TaskKinds.Length < taskCount)
                    taskCount = s_TaskKinds.Length;

                for (int i = 0; i < taskCount; i++)
                {
                    s_TaskModuleRefs[i] = null;
                    s_TaskVoxelVolumeRefs[i] = null;
                    s_TaskKinds[i] = DroneFleetTaskKind.None;
                }
            }

            if (s_PendingLaunches != null)
            {
                for (int i = 0; i < s_PendingLaunches.Length; i++)
                    s_PendingLaunches[i] = default;
            }
        }

        private static void DropHeadlessManagedMemory()
        {
            s_DroneHubs = null;
            s_DroneSlotDroneIds = null;
            s_DroneSlotDestroyed = null;
            s_PendingAbortBySlot = null;
            s_PendingReleaseBySlot = null;
            s_PendingHostileBySlot = null;
            s_PendingResupplyGrantBySlot = null;
            s_PendingResupplyFailureBySlot = null;
            s_PendingResupplyReservationIdsBySlot = null;
            s_DroneMiningCommitFailureReasonsBySlot = null;
            s_TargetModulesByDroneSlot = null;
            s_TargetVoxelVolumesByDroneSlot = null;
            s_DroneTaskKindsBySlot = null;
            s_DronePositions = null;
            s_TaskModuleRefs = null;
            s_TaskVoxelVolumeRefs = null;
            s_TaskKinds = null;
            s_PendingLaunches = null;
        }

        private static void RebindDroneDataVault(IDataVault currentVault)
        {
            if (ReferenceEquals(s_CachedDataVault, currentVault))
            {
                HectonDroneFleetEvents.BindDataVault(s_CachedDataVault);
                RepairDroneTorchAcousticEvents.BindDataVault(s_CachedDataVault);
                return;
            }

            if (s_CachedDataVault != null)
            {
                CompletePendingHeadlessJobForReset();
                ReleaseHeadlessVaultHandles(s_CachedDataVault);
            }

            s_CachedDataVault = currentVault;
            HectonDroneFleetEvents.BindDataVault(s_CachedDataVault);
            RepairDroneTorchAcousticEvents.BindDataVault(s_CachedDataVault);

            if (s_CachedDataVault != null)
                AllocateHeadlessNativeMemory();
        }

        private static bool EnsureDroneVaultBuffer<T>(
            BufferID bufferId,
            int length,
            NativeArrayOptions allocationNativeArrayOptions,
            ref VaultGenerationHandle<T> handle) where T : struct
        {
            IDataVault vault = RefreshDroneDataVaultForColdPath();

            if (vault != null)
            {
                if (TryOpenDroneVaultBuffer(vault, in handle, bufferId, length, out NativeArray<T> buffer))
                    return true;

                if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existingHandle))
                {
                    handle = existingHandle;
                    if (TryOpenDroneVaultBuffer(vault, in handle, bufferId, length, out buffer))
                        return true;
                }

                handle = vault.EnsureGenerationHandle<T>(
                    bufferId,
                    length,
                    SystemID.Construction,
                    allocationNativeArrayOptions);
                if (TryOpenDroneVaultBuffer(vault, in handle, bufferId, length, out buffer))
                    return true;

                handle = default;
            }

            handle = default;
            return false;
        }

        private static IDataVault RefreshDroneDataVaultForColdPath()
        {
            IDataVault currentVault = GlobalRegistry.DataVault;
            if (!ReferenceEquals(s_CachedDataVault, currentVault))
                RebindDroneDataVault(currentVault);

            return s_CachedDataVault;
        }

        private static bool TryResolveDroneServiceCommandBuffers(
            out NativeArray<DroneServiceCommand> commands,
            out NativeArray<DroneServiceCommandCursor> cursor)
        {
            commands = default;
            cursor = default;
            IDataVault vault = s_CachedDataVault;
            return TryOpenDroneVaultBuffer(
                       vault,
                       in s_DroneServiceCommandsHandle,
                       DroneFleetServiceCommandsBufferId,
                       DroneServiceCommandCapacity,
                       out commands) &&
                   TryOpenDroneVaultBuffer(
                       vault,
                       in s_DroneServiceCommandCursorHandle,
                       DroneFleetServiceCommandCursorBufferId,
                       1,
                       out cursor);
        }

        private static bool TryAcquireDroneServiceCommandMutationViews(
            out NativeArray<DroneServiceCommand> commands,
            out NativeArray<DroneServiceCommandCursor> cursor,
            out IDataVault guardedVault)
        {
            commands = default;
            cursor = default;
            guardedVault = null;

            IDataVault vault = s_CachedDataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !TryOpenDroneVaultBuffer(
                    vault,
                    in s_DroneServiceCommandsHandle,
                    DroneFleetServiceCommandsBufferId,
                    DroneServiceCommandCapacity,
                    out NativeArray<DroneServiceCommand> _) ||
                !TryOpenDroneVaultBuffer(
                    vault,
                    in s_DroneServiceCommandCursorHandle,
                    DroneFleetServiceCommandCursorBufferId,
                    1,
                    out NativeArray<DroneServiceCommandCursor> _) ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(DroneServiceCommandMutationGuardMask))
            {
                return false;
            }

            bool releaseOnExit = true;
            try
            {
                if (vault.IsCompactionFenceActive ||
                    !vault.TryReadHandle(in s_DroneServiceCommandsHandle, out commands) ||
                    !vault.TryReadHandle(in s_DroneServiceCommandCursorHandle, out cursor) ||
                    vault.IsCompactionFenceActive ||
                    !commands.IsCreated ||
                    commands.Length < DroneServiceCommandCapacity ||
                    !cursor.IsCreated ||
                    cursor.Length < 1)
                {
                    commands = default;
                    cursor = default;
                    return false;
                }

                guardedVault = vault;
                releaseOnExit = false;
                return true;
            }
            finally
            {
                if (releaseOnExit)
                    vault.ReleaseMutationGuard(DroneServiceCommandMutationGuardMask);
            }
        }

        private static bool TryResolveDroneServiceCommandMutationViewsFromHeadlessGuard(
            out NativeArray<DroneServiceCommand> commands,
            out NativeArray<DroneServiceCommandCursor> cursor,
            out IDataVault guardedVault)
        {
            commands = default;
            cursor = default;
            guardedVault = null;

            IDataVault vault = s_DroneHeadlessJobMutationGuardVault ?? s_CachedDataVault;
            if (!s_DroneHeadlessJobMutationGuardHeld ||
                vault == null ||
                vault.IsCompactionFenceActive ||
                !TryResolveDroneMutationBuffer(
                    vault,
                    in s_DroneServiceCommandsHandle,
                    DroneFleetServiceCommandsBufferId,
                    DroneServiceCommandCapacity,
                    out commands) ||
                !TryResolveDroneMutationBuffer(
                    vault,
                    in s_DroneServiceCommandCursorHandle,
                    DroneFleetServiceCommandCursorBufferId,
                    1,
                    out cursor) ||
                vault.IsCompactionFenceActive)
            {
                commands = default;
                cursor = default;
                return false;
            }

            guardedVault = vault;
            return true;
        }

        private static void ReleaseDroneServiceCommandMutationGuard()
        {
            if (s_DroneServiceCommandMutationGuardCoveredByHeadlessJob)
            {
                s_DroneServiceCommandMutationGuardCoveredByHeadlessJob = false;
                s_DroneServiceCommandMutationGuardVault = null;
                s_DroneServiceCommandMutationGuardHeld = false;
                return;
            }

            if (!s_DroneServiceCommandMutationGuardHeld)
                return;

            ReleaseDroneMutationGuard(s_DroneServiceCommandMutationGuardVault, DroneServiceCommandMutationGuardMask);

            s_DroneServiceCommandMutationGuardVault = null;
            s_DroneServiceCommandMutationGuardHeld = false;
        }

        private static void ReleaseDroneVaultHandle<T>(ref VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            ReleaseDroneVaultHandle(s_CachedDataVault, ref handle, bufferId);
        }

        private static void ReleaseDroneVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            if (vault != null && IsDroneVaultHandle(in handle, bufferId))
            {
                vault.ReleaseBuffer(in handle);
            }

            handle = default;
        }

        private static bool TryOpenDroneVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsDroneVaultHandle(in handle, bufferId))
            {
                return false;
            }

            if (!vault.TryReadHandle(in handle, out buffer) || !buffer.IsCreated || buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool TryReadDroneVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsDroneVaultHandle(in handle, bufferId))
            {
                return false;
            }

            if (!vault.TryReadOnlyHandle(in handle, out buffer) || buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool TryAcquireDroneVaultWriteBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (!TryOpenDroneVaultBuffer(vault, in handle, bufferId, requiredLength, out NativeArray<T> _))
                return false;

            bool locked = vault.TryAcquireWriteLock(in handle, SystemID.Construction, out buffer);
            if (!locked)
            {
                return false;
            }

            bool releaseOnFailure = true;
            try
            {
                if (buffer.IsCreated && buffer.Length >= requiredLength)
                {
                    releaseOnFailure = false;
                    return true;
                }

                buffer = default;
                return false;
            }
            finally
            {
                if (releaseOnFailure)
                    vault.ReleaseWriteLock(in handle, SystemID.Construction);
            }
        }

        private static void ReleaseDroneMutationGuard(IDataVault vault, ulong mutationGuardMask)
        {
            if (vault != null && mutationGuardMask != 0UL)
                vault.ReleaseMutationGuard(mutationGuardMask);
        }

        private static bool IsDroneVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)SystemID.Construction &&
                   handle.Generation != 0u;
        }

        private static ulong DroneMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private static bool TryOpenDroneCoreBuffers(
            out NativeArray<HeadlessDroneState> droneStates,
            out NativeArray<HeadlessDroneState> droneStateBackBuffer,
            out NativeArray<float4x4> droneRenderMatrices,
            out NativeArray<float4x4> droneRenderMatrixBackBuffer)
        {
            droneStates = default;
            droneStateBackBuffer = default;
            droneRenderMatrices = default;
            droneRenderMatrixBackBuffer = default;
            IDataVault vault = s_CachedDataVault;
            return TryOpenDroneVaultBuffer(
                       vault,
                       in s_DroneStatesHandle,
                       BufferID.ShinobuDroneFleetStates,
                       HeadlessDroneCapacity,
                       out droneStates) &&
                   TryOpenDroneVaultBuffer(
                       vault,
                       in s_DroneStateBackBufferHandle,
                       BufferID.ShinobuDroneFleetStateBackBuffer,
                       HeadlessDroneCapacity,
                       out droneStateBackBuffer) &&
                   TryOpenDroneVaultBuffer(
                       vault,
                       in s_DroneRenderMatricesHandle,
                       BufferID.ShinobuDroneFleetRenderMatrices,
                       HeadlessDroneCapacity,
                       out droneRenderMatrices) &&
                   TryOpenDroneVaultBuffer(
                       vault,
                       in s_DroneRenderMatrixBackBufferHandle,
                       BufferID.ShinobuDroneFleetRenderMatrixBackBuffer,
                       HeadlessDroneCapacity,
                       out droneRenderMatrixBackBuffer);
        }

        private static bool TryReadDroneStates(out NativeArray<HeadlessDroneState>.ReadOnly droneStates)
        {
            return TryReadDroneVaultBuffer(
                s_CachedDataVault,
                in s_DroneStatesHandle,
                BufferID.ShinobuDroneFleetStates,
                HeadlessDroneCapacity,
                out droneStates);
        }

        private static bool TryOpenDroneRenderMatrices(out NativeArray<float4x4> droneRenderMatrices)
        {
            return TryOpenDroneVaultBuffer(
                s_CachedDataVault,
                in s_DroneRenderMatricesHandle,
                BufferID.ShinobuDroneFleetRenderMatrices,
                HeadlessDroneCapacity,
                out droneRenderMatrices);
        }

        private static bool TryResolveDroneMutationBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsDroneVaultHandle(in handle, bufferId) ||
                !vault.TryReadHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool TryAcquireDroneOriginShiftMutationViews(
            out NativeArray<HeadlessDroneState> droneStates,
            out NativeArray<HeadlessDroneState> droneStateBackBuffer,
            out NativeArray<float4x4> droneRenderMatrices,
            out NativeArray<float4x4> droneRenderMatrixBackBuffer,
            out NativeArray<float3> positionsSoA,
            out IDataVault guardedVault)
        {
            droneStates = default;
            droneStateBackBuffer = default;
            droneRenderMatrices = default;
            droneRenderMatrixBackBuffer = default;
            positionsSoA = default;
            guardedVault = null;

            IDataVault vault = s_CachedDataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(DroneOriginShiftMutationGuardMask))
            {
                return false;
            }

            bool releaseOnExit = true;
            try
            {
                if (vault.IsCompactionFenceActive ||
                    !TryResolveDroneMutationBuffer(
                        vault,
                        in s_DroneStatesHandle,
                        BufferID.ShinobuDroneFleetStates,
                        HeadlessDroneCapacity,
                        out droneStates) ||
                    !TryResolveDroneMutationBuffer(
                        vault,
                        in s_DroneStateBackBufferHandle,
                        BufferID.ShinobuDroneFleetStateBackBuffer,
                        HeadlessDroneCapacity,
                        out droneStateBackBuffer) ||
                    !TryResolveDroneMutationBuffer(
                        vault,
                        in s_DroneRenderMatricesHandle,
                        BufferID.ShinobuDroneFleetRenderMatrices,
                        HeadlessDroneCapacity,
                        out droneRenderMatrices) ||
                    !TryResolveDroneMutationBuffer(
                        vault,
                        in s_DroneRenderMatrixBackBufferHandle,
                        BufferID.ShinobuDroneFleetRenderMatrixBackBuffer,
                        HeadlessDroneCapacity,
                        out droneRenderMatrixBackBuffer) ||
                    !TryResolveDroneMutationBuffer(
                        vault,
                        in s_DronePositionsSoAHandle,
                        BufferID.ShinobuDroneFleetPositionsSoA,
                        HeadlessDroneCapacity,
                        out positionsSoA) ||
                    vault.IsCompactionFenceActive)
                {
                    droneStates = default;
                    droneStateBackBuffer = default;
                    droneRenderMatrices = default;
                    droneRenderMatrixBackBuffer = default;
                    positionsSoA = default;
                    return false;
                }

                guardedVault = vault;
                releaseOnExit = false;
                return true;
            }
            finally
            {
                if (releaseOnExit)
                    vault.ReleaseMutationGuard(DroneOriginShiftMutationGuardMask);
            }
        }

        private static bool TryAcquireDroneChassisSpecs(
            out NativeArray<DroneChassisSpecDTO> chassisSpecs,
            out IDataVault vault)
        {
            vault = s_CachedDataVault;
            return TryAcquireDroneVaultWriteBuffer(
                vault,
                in s_DroneChassisSpecsHandle,
                DroneFleetChassisSpecsBufferId,
                DroneChassisSpecCapacity,
                out chassisSpecs);
        }

        private static bool TryAcquireDroneTuningConstants(
            out NativeArray<DroneFleetTuningConstants> tuningConstants,
            out IDataVault vault)
        {
            vault = s_CachedDataVault;
            return TryAcquireDroneVaultWriteBuffer(
                vault,
                in s_DroneTuningConstantsHandle,
                BufferID.ShinobuDroneFleetTuningConstants,
                1,
                out tuningConstants);
        }

        private static bool TryAcquireDroneBlackBox(
            out NativeArray<DroneFleetBlackBoxEntry> blackBox,
            out IDataVault vault)
        {
            vault = s_CachedDataVault;
            return TryAcquireDroneVaultWriteBuffer(
                vault,
                in s_DroneBlackBoxHandle,
                BufferID.ShinobuDroneFleetBlackBox,
                DroneFleetBlackBoxFrameCapacity,
                out blackBox);
        }

        private static bool TryPrepareAndUploadDroneRenderInstances(
            NativeArray<float4x4> droneRenderMatrices,
            NativeArray<HeadlessDroneState>.ReadOnly droneStates)
        {
            if (s_DroneRenderInstanceBuffer == null)
                return true;
            if (!droneRenderMatrices.IsCreated ||
                droneRenderMatrices.Length < HeadlessDroneCapacity ||
                droneStates.Length < HeadlessDroneCapacity)
            {
                return false;
            }

            IDataVault vault = s_CachedDataVault;
            if (vault == null)
                return false;

            bool gpuLocked = false;
            bool success = false;
            int writtenCount = 0;
            try
            {
                NativeArray<DroneRenderInstance> gpuWindow = s_DroneRenderInstanceBuffer.LockBufferForWrite<DroneRenderInstance>(0, HeadlessDroneCapacity);
                gpuLocked = true;
                writtenCount = HeadlessDroneCapacity;
                if (!PrepareDroneRenderInstances(gpuWindow, droneRenderMatrices, droneStates))
                    return false;

                if (!TryAcquireDroneVaultWriteBuffer(
                    vault,
                    in s_DroneRenderInstancesHandle,
                    BufferID.ShinobuDroneFleetRenderInstances,
                    HeadlessDroneCapacity,
                    out NativeArray<DroneRenderInstance> renderInstances))
                {
                    return false;
                }

                try
                {
                    CopyDroneRenderInstances(gpuWindow, renderInstances);
                }
                finally
                {
                    vault.ReleaseWriteLock(in s_DroneRenderInstancesHandle, SystemID.Construction);
                }

                success = true;
            }
            catch (System.Exception)
            {
                success = false;
            }
            finally
            {
                if (gpuLocked && !TryUnlockDroneGpuBufferAfterWrite<DroneRenderInstance>(s_DroneRenderInstanceBuffer, writtenCount))
                    success = false;
            }

            return success;
        }

        private static bool TryUnlockDroneGpuBufferAfterWrite<T>(GraphicsBuffer buffer, int count)
            where T : struct
        {
            try
            {
                buffer.UnlockBufferAfterWrite<T>(count);
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private static bool TryPrepareAndUploadDroneCullingStates(NativeArray<HeadlessDroneState>.ReadOnly droneStates)
        {
            if (s_DroneStateGpuBuffer == null)
                return true;
            if (droneStates.Length < HeadlessDroneCapacity)
                return false;

            IDataVault vault = s_CachedDataVault;
            if (vault == null)
                return false;

            bool gpuLocked = false;
            bool success = false;
            int writtenCount = 0;
            try
            {
                NativeArray<DroneCullingStateGpu> gpuWindow = s_DroneStateGpuBuffer.LockBufferForWrite<DroneCullingStateGpu>(0, HeadlessDroneCapacity);
                gpuLocked = true;
                writtenCount = HeadlessDroneCapacity;
                if (!PrepareDroneCullingStates(gpuWindow, droneStates))
                    return false;

                if (!TryAcquireDroneVaultWriteBuffer(
                    vault,
                    in s_DroneCullingStatesHandle,
                    BufferID.DroneFleetCullingStates,
                    HeadlessDroneCapacity,
                    out NativeArray<DroneCullingStateGpu> cullingStates))
                {
                    return false;
                }

                try
                {
                    CopyDroneCullingStates(gpuWindow, cullingStates);
                }
                finally
                {
                    vault.ReleaseWriteLock(in s_DroneCullingStatesHandle, SystemID.Construction);
                }

                success = true;
            }
            catch (System.Exception)
            {
                success = false;
            }
            finally
            {
                if (gpuLocked && !TryUnlockDroneGpuBufferAfterWrite<DroneCullingStateGpu>(s_DroneStateGpuBuffer, writtenCount))
                    success = false;
            }

            return success;
        }

        private static bool TryResolveDroneProceduralArgsForUpload(
            out NativeArray<DroneProceduralIndirectArgsDTO> proceduralArgs)
        {
            return TryOpenDroneVaultBuffer(
                s_CachedDataVault,
                in s_DroneProceduralArgsHandle,
                DroneFleetProceduralArgsBufferId,
                1,
                out proceduralArgs);
        }

        private static bool TryReadDroneTuningConstants(out NativeArray<DroneFleetTuningConstants>.ReadOnly tuningConstants)
        {
            return TryReadDroneVaultBuffer(
                s_CachedDataVault,
                in s_DroneTuningConstantsHandle,
                BufferID.ShinobuDroneFleetTuningConstants,
                1,
                out tuningConstants);
        }

        private static bool TryReadDroneMacroWaypointBuffers(
            out NativeArray<PathWaypointDTO>.ReadOnly macroWaypoints,
            out NativeArray<byte>.ReadOnly macroWaypointStates)
        {
            macroWaypoints = default;
            macroWaypointStates = default;
            IDataVault vault = s_CachedDataVault;
            return TryReadDroneVaultBuffer(
                       vault,
                       in s_DroneMacroWaypointsHandle,
                       BufferID.ShinobuDroneFleetMacroWaypoints,
                       HeadlessDroneCapacity,
                       out macroWaypoints) &&
                   TryReadDroneVaultBuffer(
                       vault,
                       in s_DroneMacroWaypointStatesHandle,
                       BufferID.ShinobuDroneFleetMacroWaypointStates,
                       HeadlessDroneCapacity,
                       out macroWaypointStates);
        }

        private static bool TryReadDroneMacroRouteBuffers(
            out NativeArray<int>.ReadOnly macroRouteNodes,
            out NativeArray<byte>.ReadOnly macroRouteCounts)
        {
            macroRouteNodes = default;
            macroRouteCounts = default;
            IDataVault vault = s_CachedDataVault;
            return TryReadDroneVaultBuffer(
                       vault,
                       in s_DroneMacroRouteNodesHandle,
                       BufferID.ShinobuDroneFleetMacroRouteNodes,
                       DroneAStarRouteNodeCapacity,
                       out macroRouteNodes) &&
                   TryReadDroneVaultBuffer(
                       vault,
                       in s_DroneMacroRouteCountsHandle,
                       BufferID.ShinobuDroneFleetMacroRouteCounts,
                       HeadlessDroneCapacity,
                       out macroRouteCounts);
        }

        private static bool TryReadDroneAStarNodeStates(out NativeArray<byte>.ReadOnly aStarNodeStates)
        {
            return TryReadDroneVaultBuffer(
                s_CachedDataVault,
                in s_DroneAStarNodeStatesHandle,
                BufferID.ShinobuDroneFleetAStarNodeStates,
                DroneAStarScratchNodeCapacity,
                out aStarNodeStates);
        }

        private static void ClearDroneMacroWaypointStates()
        {
            IDataVault vault = s_CachedDataVault;
            if (!TryAcquireDroneVaultWriteBuffer(
                vault,
                in s_DroneMacroWaypointStatesHandle,
                BufferID.ShinobuDroneFleetMacroWaypointStates,
                HeadlessDroneCapacity,
                out NativeArray<byte> macroWaypointStates))
            {
                return;
            }

            try
            {
                for (int i = 0; i < macroWaypointStates.Length; i++)
                    macroWaypointStates[i] = 0;
            }
            finally
            {
                vault.ReleaseWriteLock(in s_DroneMacroWaypointStatesHandle, SystemID.Construction);
            }
        }

        private static bool TryOpenDroneMirrorBuffers(
            out NativeArray<float3> positionsSoA,
            out NativeArray<byte> stateBytes,
            out NativeArray<DroneStateDTO> stateDtos,
            out NativeArray<DroneTargetDTO> targetDtos)
        {
            positionsSoA = default;
            stateBytes = default;
            stateDtos = default;
            targetDtos = default;
            IDataVault vault = s_CachedDataVault;
            return TryOpenDroneVaultBuffer(
                       vault,
                       in s_DronePositionsSoAHandle,
                       BufferID.ShinobuDroneFleetPositionsSoA,
                       HeadlessDroneCapacity,
                       out positionsSoA) &&
                   TryOpenDroneVaultBuffer(
                       vault,
                       in s_DroneStateBytesHandle,
                       BufferID.ShinobuDroneFleetStateBytes,
                       HeadlessDroneCapacity,
                       out stateBytes) &&
                   TryOpenDroneVaultBuffer(
                       vault,
                       in s_DroneStateDtosHandle,
                       DroneFleetStateDtoBufferId,
                       HeadlessDroneCapacity,
                       out stateDtos) &&
                   TryOpenDroneVaultBuffer(
                       vault,
                       in s_DroneTargetDtosHandle,
                       DroneFleetTargetDtoBufferId,
                       HeadlessDroneCapacity,
                       out targetDtos);
        }

        private static bool TryAcquireDroneCoreMirrorMutationViews(
            out NativeArray<HeadlessDroneState> droneStates,
            out NativeArray<HeadlessDroneState> droneStateBackBuffer,
            out NativeArray<float4x4> droneRenderMatrices,
            out NativeArray<float4x4> droneRenderMatrixBackBuffer,
            out NativeArray<float3> positionsSoA,
            out NativeArray<byte> stateBytes,
            out NativeArray<DroneStateDTO> stateDtos,
            out NativeArray<DroneTargetDTO> targetDtos,
            out IDataVault guardedVault)
        {
            droneStates = default;
            droneStateBackBuffer = default;
            droneRenderMatrices = default;
            droneRenderMatrixBackBuffer = default;
            positionsSoA = default;
            stateBytes = default;
            stateDtos = default;
            targetDtos = default;
            guardedVault = null;

            IDataVault vault = s_CachedDataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(DroneCoreMirrorMutationGuardMask))
            {
                return false;
            }

            bool releaseOnExit = true;
            try
            {
                if (vault.IsCompactionFenceActive ||
                    !TryResolveDroneMutationBuffer(
                        vault,
                        in s_DroneStatesHandle,
                        BufferID.ShinobuDroneFleetStates,
                        HeadlessDroneCapacity,
                        out droneStates) ||
                    !TryResolveDroneMutationBuffer(
                        vault,
                        in s_DroneStateBackBufferHandle,
                        BufferID.ShinobuDroneFleetStateBackBuffer,
                        HeadlessDroneCapacity,
                        out droneStateBackBuffer) ||
                    !TryResolveDroneMutationBuffer(
                        vault,
                        in s_DroneRenderMatricesHandle,
                        BufferID.ShinobuDroneFleetRenderMatrices,
                        HeadlessDroneCapacity,
                        out droneRenderMatrices) ||
                    !TryResolveDroneMutationBuffer(
                        vault,
                        in s_DroneRenderMatrixBackBufferHandle,
                        BufferID.ShinobuDroneFleetRenderMatrixBackBuffer,
                        HeadlessDroneCapacity,
                        out droneRenderMatrixBackBuffer) ||
                    !TryResolveDroneMutationBuffer(
                        vault,
                        in s_DronePositionsSoAHandle,
                        BufferID.ShinobuDroneFleetPositionsSoA,
                        HeadlessDroneCapacity,
                        out positionsSoA) ||
                    !TryResolveDroneMutationBuffer(
                        vault,
                        in s_DroneStateBytesHandle,
                        BufferID.ShinobuDroneFleetStateBytes,
                        HeadlessDroneCapacity,
                        out stateBytes) ||
                    !TryResolveDroneMutationBuffer(
                        vault,
                        in s_DroneStateDtosHandle,
                        DroneFleetStateDtoBufferId,
                        HeadlessDroneCapacity,
                        out stateDtos) ||
                    !TryResolveDroneMutationBuffer(
                        vault,
                        in s_DroneTargetDtosHandle,
                        DroneFleetTargetDtoBufferId,
                        HeadlessDroneCapacity,
                        out targetDtos) ||
                    vault.IsCompactionFenceActive)
                {
                    droneStates = default;
                    droneStateBackBuffer = default;
                    droneRenderMatrices = default;
                    droneRenderMatrixBackBuffer = default;
                    positionsSoA = default;
                    stateBytes = default;
                    stateDtos = default;
                    targetDtos = default;
                    return false;
                }

                guardedVault = vault;
                releaseOnExit = false;
                return true;
            }
            finally
            {
                if (releaseOnExit)
                    vault.ReleaseMutationGuard(DroneCoreMirrorMutationGuardMask);
            }
        }

        private static bool TryAcquireHeadlessJobScratchBuffers(
            out NativeArray<HeadlessDroneState> droneStates,
            out NativeArray<HeadlessDroneState> droneStateBackBuffer,
            out NativeArray<float4x4> droneRenderMatrices,
            out NativeArray<float4x4> droneRenderMatrixBackBuffer,
            out NativeArray<int> taskClaimOwners,
            out NativeArray<int> telemetryAccumulator,
            out NativeArray<DroneAssignmentTaskDTO> assignmentTasks,
            out NativeArray<int> spatialBucketHeads,
            out NativeArray<int> spatialNextIndices,
            out NativeArray<int> spatialKeys,
            out NativeArray<PathWaypointDTO> macroWaypoints,
            out NativeArray<byte> macroWaypointStates,
            out NativeArray<DroneNativeMinHeapNode> aStarOpenHeap,
            out NativeArray<float> aStarGCosts,
            out NativeArray<int> aStarCameFrom,
            out NativeArray<byte> aStarNodeStates,
            out NativeArray<int> macroRouteNodes,
            out NativeArray<byte> macroRouteCounts,
            out NativeArray<DroneAStarTelemetry> aStarTelemetry,
            out NativeArray<DroneAStarPersistentState> aStarPersistentStates,
            out NativeArray<float3> positionsSoA,
            out NativeArray<byte> stateBytes,
            out NativeArray<DroneStateDTO> stateDtos,
            out NativeArray<DroneTargetDTO> targetDtos,
            out NativeArray<DroneProceduralIndirectArgsDTO> proceduralArgs,
            out bool proceduralArgsReady,
            out IDataVault guardedVault)
        {
            droneStates = default;
            droneStateBackBuffer = default;
            droneRenderMatrices = default;
            droneRenderMatrixBackBuffer = default;
            taskClaimOwners = default;
            telemetryAccumulator = default;
            assignmentTasks = default;
            spatialBucketHeads = default;
            spatialNextIndices = default;
            spatialKeys = default;
            macroWaypoints = default;
            macroWaypointStates = default;
            aStarOpenHeap = default;
            aStarGCosts = default;
            aStarCameFrom = default;
            aStarNodeStates = default;
            macroRouteNodes = default;
            macroRouteCounts = default;
            aStarTelemetry = default;
            aStarPersistentStates = default;
            positionsSoA = default;
            stateBytes = default;
            stateDtos = default;
            targetDtos = default;
            proceduralArgs = default;
            proceduralArgsReady = false;
            guardedVault = null;

            IDataVault vault = s_CachedDataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(DroneHeadlessJobMutationGuardMask))
            {
                return false;
            }

            bool releaseOnExit = true;
            try
            {
                if (vault.IsCompactionFenceActive ||
                    !TryResolveDroneMutationBuffer(vault, in s_DroneStatesHandle, BufferID.ShinobuDroneFleetStates, HeadlessDroneCapacity, out droneStates) ||
                    !TryResolveDroneMutationBuffer(vault, in s_DroneStateBackBufferHandle, BufferID.ShinobuDroneFleetStateBackBuffer, HeadlessDroneCapacity, out droneStateBackBuffer) ||
                    !TryResolveDroneMutationBuffer(vault, in s_DroneRenderMatricesHandle, BufferID.ShinobuDroneFleetRenderMatrices, HeadlessDroneCapacity, out droneRenderMatrices) ||
                    !TryResolveDroneMutationBuffer(vault, in s_DroneRenderMatrixBackBufferHandle, BufferID.ShinobuDroneFleetRenderMatrixBackBuffer, HeadlessDroneCapacity, out droneRenderMatrixBackBuffer) ||
                    !TryResolveDroneMutationBuffer(vault, in s_HeadlessTaskClaimOwnersHandle, BufferID.ShinobuDroneFleetTaskClaimOwners, HeadlessTaskCapacity, out taskClaimOwners) ||
                    !TryResolveDroneMutationBuffer(vault, in s_FleetTelemetryAccumulatorHandle, BufferID.ShinobuDroneFleetTelemetryAccumulator, (int)DroneFleetTelemetryAccumulatorSlot.Count, out telemetryAccumulator) ||
                    !TryResolveDroneMutationBuffer(vault, in s_DroneAssignmentTasksHandle, DroneFleetAssignmentTasksBufferId, HeadlessTaskCapacity, out assignmentTasks) ||
                    !TryResolveDroneMutationBuffer(vault, in s_DroneSpatialBucketHeadsHandle, DroneFleetSpatialBucketHeadsBufferId, DroneSpatialBucketCapacity, out spatialBucketHeads) ||
                    !TryResolveDroneMutationBuffer(vault, in s_DroneSpatialNextIndicesHandle, DroneFleetSpatialNextIndicesBufferId, HeadlessDroneCapacity, out spatialNextIndices) ||
                    !TryResolveDroneMutationBuffer(vault, in s_DroneSpatialKeysHandle, DroneFleetSpatialKeysBufferId, HeadlessDroneCapacity, out spatialKeys) ||
                    !TryResolveDroneMutationBuffer(vault, in s_DroneMacroWaypointsHandle, BufferID.ShinobuDroneFleetMacroWaypoints, HeadlessDroneCapacity, out macroWaypoints) ||
                    !TryResolveDroneMutationBuffer(vault, in s_DroneMacroWaypointStatesHandle, BufferID.ShinobuDroneFleetMacroWaypointStates, HeadlessDroneCapacity, out macroWaypointStates) ||
                    !TryResolveDroneMutationBuffer(vault, in s_DroneAStarOpenHeapHandle, BufferID.ShinobuDroneFleetAStarOpenHeap, DroneAStarScratchNodeCapacity, out aStarOpenHeap) ||
                    !TryResolveDroneMutationBuffer(vault, in s_DroneAStarGCostsHandle, BufferID.ShinobuDroneFleetAStarGCosts, DroneAStarScratchNodeCapacity, out aStarGCosts) ||
                    !TryResolveDroneMutationBuffer(vault, in s_DroneAStarCameFromHandle, BufferID.ShinobuDroneFleetAStarCameFrom, DroneAStarScratchNodeCapacity, out aStarCameFrom) ||
                    !TryResolveDroneMutationBuffer(vault, in s_DroneAStarNodeStatesHandle, BufferID.ShinobuDroneFleetAStarNodeStates, DroneAStarScratchNodeCapacity, out aStarNodeStates) ||
                    !TryResolveDroneMutationBuffer(vault, in s_DroneMacroRouteNodesHandle, BufferID.ShinobuDroneFleetMacroRouteNodes, DroneAStarRouteNodeCapacity, out macroRouteNodes) ||
                    !TryResolveDroneMutationBuffer(vault, in s_DroneMacroRouteCountsHandle, BufferID.ShinobuDroneFleetMacroRouteCounts, HeadlessDroneCapacity, out macroRouteCounts) ||
                    !TryResolveDroneMutationBuffer(vault, in s_DroneAStarTelemetryHandle, BufferID.ShinobuDroneFleetAStarTelemetry, DroneAStarTelemetryCapacity, out aStarTelemetry) ||
                    !TryResolveDroneMutationBuffer(vault, in s_DroneAStarPersistentStatesHandle, DroneFleetAStarPersistentStatesBufferId, HeadlessDroneCapacity, out aStarPersistentStates) ||
                    !TryResolveDroneMutationBuffer(vault, in s_DronePositionsSoAHandle, BufferID.ShinobuDroneFleetPositionsSoA, HeadlessDroneCapacity, out positionsSoA) ||
                    !TryResolveDroneMutationBuffer(vault, in s_DroneStateBytesHandle, BufferID.ShinobuDroneFleetStateBytes, HeadlessDroneCapacity, out stateBytes) ||
                    !TryResolveDroneMutationBuffer(vault, in s_DroneStateDtosHandle, DroneFleetStateDtoBufferId, HeadlessDroneCapacity, out stateDtos) ||
                    !TryResolveDroneMutationBuffer(vault, in s_DroneTargetDtosHandle, DroneFleetTargetDtoBufferId, HeadlessDroneCapacity, out targetDtos) ||
                    vault.IsCompactionFenceActive)
                {
                    droneStates = default;
                    droneStateBackBuffer = default;
                    droneRenderMatrices = default;
                    droneRenderMatrixBackBuffer = default;
                    taskClaimOwners = default;
                    telemetryAccumulator = default;
                    assignmentTasks = default;
                    spatialBucketHeads = default;
                    spatialNextIndices = default;
                    spatialKeys = default;
                    macroWaypoints = default;
                    macroWaypointStates = default;
                    aStarOpenHeap = default;
                    aStarGCosts = default;
                    aStarCameFrom = default;
                    aStarNodeStates = default;
                    macroRouteNodes = default;
                    macroRouteCounts = default;
                    aStarTelemetry = default;
                    aStarPersistentStates = default;
                    positionsSoA = default;
                    stateBytes = default;
                    stateDtos = default;
                    targetDtos = default;
                    return false;
                }

                proceduralArgsReady = TryResolveDroneMutationBuffer(
                    vault,
                    in s_DroneProceduralArgsHandle,
                    DroneFleetProceduralArgsBufferId,
                    1,
                    out proceduralArgs);

                if (vault.IsCompactionFenceActive)
                {
                    proceduralArgs = default;
                    proceduralArgsReady = false;
                    return false;
                }

                guardedVault = vault;
                releaseOnExit = false;
                return true;
            }
            finally
            {
                if (releaseOnExit)
                    vault.ReleaseMutationGuard(DroneHeadlessJobMutationGuardMask);
            }
        }

        private static void ReleaseDroneHeadlessJobMutationGuard()
        {
            if (!s_DroneHeadlessJobMutationGuardHeld)
                return;

            ReleaseDroneMutationGuard(s_DroneHeadlessJobMutationGuardVault, DroneHeadlessJobMutationGuardMask);
            s_DroneHeadlessJobMutationGuardVault = null;
            s_DroneHeadlessJobMutationGuardHeld = false;
            s_HeadlessFrameTuning = default;
            s_HeadlessFrameTuningValid = false;
        }

        private static void TryRegisterHeadlessDriver()
        {
            if (s_HeadlessDriverRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            s_HeadlessUpdateRegistered = GlobalRegistry.TryRegisterUpdatable(s_HeadlessDriver, PriorityLayer.Environment);
            s_HeadlessLateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(s_HeadlessDriver, PriorityLayer.Environment);
            s_HeadlessRenderRegistered = GlobalRegistry.Renderables.TryRegister(s_HeadlessDriver);

            if (!s_HeadlessUpdateRegistered || !s_HeadlessLateFrameRegistered || !s_HeadlessRenderRegistered)
            {
                TryUnregisterHeadlessDriverLanes();
                return;
            }

            s_HeadlessDriverRegistered = true;
            TryRegisterHeadlessHotSwapListener();
        }

        private static void TryUnregisterHeadlessDriver()
        {
            TryUnregisterHeadlessHotSwapListener();

            if (!s_HeadlessDriverRegistered)
                return;

            TryUnregisterHeadlessDriverLanes();
            s_HeadlessDriverRegistered = false;
        }

        private static void TryUnregisterHeadlessDriverLanes()
        {
            if (s_HeadlessRenderRegistered)
            {
                GlobalRegistry.Renderables.TryUnregister(s_HeadlessDriver);
                s_HeadlessRenderRegistered = false;
            }

            if (s_HeadlessLateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(s_HeadlessDriver, PriorityLayer.Environment);
                s_HeadlessLateFrameRegistered = false;
            }

            if (s_HeadlessUpdateRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(s_HeadlessDriver, PriorityLayer.Environment);
                s_HeadlessUpdateRegistered = false;
            }
        }

        private static void TryRegisterHeadlessHotSwapListener()
        {
            if (s_HeadlessHotSwapRegistered || !Application.isPlaying)
                return;

            s_HeadlessHotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(s_HeadlessDriver);
        }

        private static void TryUnregisterHeadlessHotSwapListener()
        {
            if (!s_HeadlessHotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(s_HeadlessDriver);
            s_HeadlessHotSwapRegistered = false;
        }

        private static void ScheduleHeadlessSimulation(float deltaTime)
        {
            if (s_HeadlessJobScheduled || CountManagedHeadlessDrones() <= 0)
                return;

            s_LastHeadlessDeltaTime = SanitizeHeadlessDeltaTime(deltaTime);
            AdvanceHeadlessSimulationClock(s_LastHeadlessDeltaTime);
            if (!TryAcquireHeadlessJobScratchBuffers(
                    out NativeArray<HeadlessDroneState> droneStates,
                    out NativeArray<HeadlessDroneState> droneStateBackBuffer,
                    out NativeArray<float4x4> droneRenderMatrices,
                    out NativeArray<float4x4> droneRenderMatrixBackBuffer,
                    out NativeArray<int> taskClaimOwners,
                    out NativeArray<int> telemetryAccumulator,
                    out NativeArray<DroneAssignmentTaskDTO> assignmentTasks,
                    out NativeArray<int> spatialBucketHeads,
                    out NativeArray<int> spatialNextIndices,
                    out NativeArray<int> spatialKeys,
                    out NativeArray<PathWaypointDTO> macroWaypoints,
                    out NativeArray<byte> macroWaypointStates,
                    out NativeArray<DroneNativeMinHeapNode> aStarOpenHeap,
                    out NativeArray<float> aStarGCosts,
                    out NativeArray<int> aStarCameFrom,
                    out NativeArray<byte> aStarNodeStates,
                    out NativeArray<int> macroRouteNodes,
                    out NativeArray<byte> macroRouteCounts,
                    out NativeArray<DroneAStarTelemetry> aStarTelemetry,
                    out NativeArray<DroneAStarPersistentState> aStarPersistentStates,
                    out NativeArray<float3> positionsSoA,
                    out NativeArray<byte> stateBytes,
                    out NativeArray<DroneStateDTO> stateDtos,
                    out NativeArray<DroneTargetDTO> targetDtos,
                    out NativeArray<DroneProceduralIndirectArgsDTO> proceduralArgs,
                    out bool proceduralArgsReady,
                    out IDataVault headlessScratchVault))
            {
                return;
            }

            s_DroneHeadlessJobMutationGuardHeld = true;
            s_DroneHeadlessJobMutationGuardVault = headlessScratchVault;
            bool headlessJobScheduled = false;
            try
            {
            BuildHeadlessTaskMap(s_LastHeadlessDeltaTime, assignmentTasks);
            BuildHeadlessSpatialHash(droneStates, spatialBucketHeads, spatialNextIndices, spatialKeys);
            ClearHeadlessTaskClaims(droneStates, taskClaimOwners);
            ClearFleetTelemetryAccumulator(telemetryAccumulator);
            ApplyDockingRequestSignals(droneStates, droneStateBackBuffer, positionsSoA, stateBytes, stateDtos, targetDtos);

            DroneFleetTuningConstants tuning = ResolveDroneTuning();
            s_HeadlessFrameTuning = tuning;
            s_HeadlessFrameTuningValid = true;
            if (!TryAcquireHeadlessDroneSdfGrid(in tuning, droneStates, out DroneSdfGrid sdfGrid))
            {
                PublishDroneSdfFailClosedSignal(CountManagedHeadlessDrones());
                return;
            }

            ResolveDockingObstacleAborts(droneStates, droneStateBackBuffer, telemetryAccumulator, positionsSoA, stateBytes, stateDtos, targetDtos, in sdfGrid, in tuning);

            bool hasPlayer = TryResolvePlayerPosition(out Vector3 playerPosition);
            bool hasFormationAnchor = TryResolveFormationAnchor(out Vector3 formationAnchorPosition);
            bool hasAbyssalFlow = TryResolveAbyssalFlowVolumePayload(
                out NativeArray<float3>.ReadOnly abyssalFlowVolume,
                out Vector3 abyssalFlowCenter,
                out int abyssalFlowResolutionXZ,
                out int abyssalFlowResolutionY,
                out int abyssalFlowRingOffsetX,
                out int abyssalFlowRingOffsetY,
                out int abyssalFlowRingOffsetZ,
                out float abyssalFlowHorizontalCellSize,
                out float abyssalFlowVerticalCellSize,
                out float abyssalFlowSurfaceY,
                out float abyssalFlowDepthMeters);
            ResolveFluidCurrentSnapshot(
                out Vector3 baseFlowVelocity,
                out bool phantomFlowEnabled,
                out float phantomFlowNoiseScale,
                out float phantomFlowTimeScale,
                out float phantomFlowStrength,
                out float phantomFlowVerticalFactor);

            int frameIndex = s_DroneFrameIndex++;
            int steeringTickModulo = ResolveDroneSteeringTickModulo(in tuning);
            int aStarSolveBudget = ResolveDroneAStarSolveBudget(in tuning);
            JobHandle macroAStarHandle = ScheduleDroneMacroAStar(
                frameIndex,
                aStarSolveBudget,
                in tuning,
                in sdfGrid,
                macroWaypoints,
                macroWaypointStates,
                aStarOpenHeap,
                aStarGCosts,
                aStarCameFrom,
                aStarNodeStates,
                macroRouteNodes,
                macroRouteCounts,
                aStarTelemetry,
                aStarPersistentStates,
                droneStates);
            JobHandle assignmentHandle = macroAStarHandle;
            if (s_FleetFormationMode == DroneFleetFormationMode.Repair &&
                assignmentTasks.IsCreated &&
                stateDtos.IsCreated &&
                targetDtos.IsCreated &&
                taskClaimOwners.IsCreated)
            {
                DroneTaskAssignmentJob assignmentJob = new DroneTaskAssignmentJob
                {
                    Drones = droneStates,
                    DroneStatesDto = stateDtos,
                    DroneTargets = targetDtos,
                    Tasks = assignmentTasks,
                    TaskClaimOwners = taskClaimOwners,
                    TaskCount = s_HeadlessTaskCount,
                    EmergencyOverclock = IsEmergencyOverclockActive ? 1 : 0
                };
                assignmentHandle = assignmentJob.Schedule(HeadlessDroneCapacity, DroneJobBatchSize, macroAStarHandle);
            }
            s_LastDroneSteeringTickModulo = steeringTickModulo;
            bool serviceQueueEnabled = TryResolveDroneServiceCommandMutationViewsFromHeadlessGuard(
                out NativeArray<DroneServiceCommand> serviceCommands,
                out NativeArray<DroneServiceCommandCursor> serviceCommandCursor,
                out IDataVault serviceCommandVault);
            if (serviceQueueEnabled)
            {
                s_DroneServiceCommandMutationGuardHeld = true;
                s_DroneServiceCommandMutationGuardVault = serviceCommandVault;
                s_DroneServiceCommandMutationGuardCoveredByHeadlessJob = true;
                serviceCommandCursor[0] = default;
            }

            DroneCognitionJob job = default;
            job.ReadDrones = droneStates;
            job.Drones = droneStateBackBuffer;
            job.DroneStatesDto = stateDtos;
            job.DroneTargets = targetDtos;
            job.RenderMatrices = droneRenderMatrixBackBuffer;
            job.DronePositions = positionsSoA;
            job.DroneStates = stateBytes;
            job.DroneSpatialBucketHeads = spatialBucketHeads;
            job.DroneSpatialNextIndices = spatialNextIndices;
            job.DroneSpatialKeys = spatialKeys;
            job.AbyssalFlowVolume = abyssalFlowVolume;
            job.MacroWaypoints = macroWaypoints;
            job.MacroWaypointStates = macroWaypointStates;
            job.TaskClaimOwners = taskClaimOwners;
            job.TelemetryAccumulator = telemetryAccumulator;
            job.ServiceCommands = serviceCommands;
            job.ServiceCommandCursor = serviceCommandCursor;
            job.ServiceCommandCapacity = DroneServiceCommandCapacity;
            job.DeltaTime = s_LastHeadlessDeltaTime;
            job.ServiceQueueEnabled = serviceQueueEnabled ? 1 : 0;
            job.PlayerPosition = (float3)(playerPosition);
            job.PlayerPositionValid = hasPlayer ? 1 : 0;
            job.EmergencyOverclock = IsEmergencyOverclockActive ? 1 : 0;
            job.FormationMode = (int)s_FleetFormationMode;
            job.DroneSpatialBucketMask = DroneSpatialBucketCapacity - 1;
            job.FormationAnchorPosition = (float3)(formationAnchorPosition);
            job.FormationAnchorValid = hasFormationAnchor ? 1 : 0;
            job.AbyssalFlowVolumeValid = hasAbyssalFlow ? 1 : 0;
            job.AbyssalFlowResolutionXZ = abyssalFlowResolutionXZ;
            job.AbyssalFlowResolutionY = abyssalFlowResolutionY;
            job.AbyssalFlowRingOffsetX = abyssalFlowRingOffsetX;
            job.AbyssalFlowRingOffsetY = abyssalFlowRingOffsetY;
            job.AbyssalFlowRingOffsetZ = abyssalFlowRingOffsetZ;
            job.AbyssalFlowCenter = (float3)(abyssalFlowCenter);
            job.AbyssalFlowHorizontalCellSize = abyssalFlowHorizontalCellSize;
            job.AbyssalFlowVerticalCellSize = abyssalFlowVerticalCellSize;
            job.AbyssalFlowWaterLevel = abyssalFlowSurfaceY;
            job.AbyssalFlowDepthMeters = abyssalFlowDepthMeters;
            job.BaseFlowVelocity = (float3)(baseFlowVelocity);
            job.PhantomFlowTime = ResolveHeadlessSimulationClockSeconds();
            job.PhantomFlowNoiseScale = phantomFlowNoiseScale;
            job.PhantomFlowTimeScale = phantomFlowTimeScale;
            job.PhantomFlowStrength = phantomFlowStrength;
            job.PhantomFlowVerticalFactor = phantomFlowVerticalFactor;
            job.PhantomFlowEnabled = phantomFlowEnabled ? 1 : 0;
            job.FlowDragCoefficient = DroneFlowDragCoefficient;
            job.CrossCurrentVisualSlipWeight = ResolveGlobalQualityWeight();
            job.SdfGrid = sdfGrid;
            job.FrameIndex = frameIndex;
            job.SteeringTickModulo = steeringTickModulo;
            job.SdfRepulsionStrength = tuning.SdfRepulsionStrength;
            JobHandle cognitionHandle = job.Schedule(HeadlessDroneCapacity, DroneJobBatchSize, assignmentHandle);
            DroneMetabolismJob metabolismJob = new DroneMetabolismJob
            {
                Drones = droneStateBackBuffer,
                DroneStatesDto = stateDtos,
                DroneTargets = targetDtos,
                DeltaTime = s_LastHeadlessDeltaTime,
                EmergencyOverclock = IsEmergencyOverclockActive ? 1 : 0
            };
            JobHandle metabolismHandle = metabolismJob.Schedule(HeadlessDroneCapacity, DroneJobBatchSize, cognitionHandle);
            ExtractDroneMatricesJob matrixJob = new ExtractDroneMatricesJob
            {
                Drones = droneStateBackBuffer,
                DroneStatesDto = stateDtos,
                Matrices = droneRenderMatrixBackBuffer,
                CameraAup = ResolveDroneRenderReferenceAup(),
                ScaleMeters = DroneProceduralScaleMeters
            };
            JobHandle matrixHandle = matrixJob.Schedule(HeadlessDroneCapacity, DroneJobBatchSize, metabolismHandle);
            if (proceduralArgsReady && proceduralArgs.IsCreated)
            {
                BuildDroneProceduralArgsJob argsJob = new BuildDroneProceduralArgsJob
                {
                    Args = proceduralArgs,
                    VertexCountPerInstance = DroneProceduralVerticesPerInstance,
                    InstanceCount = (uint)HeadlessDroneCapacity
                };
                s_HeadlessJobHandle = argsJob.Schedule(matrixHandle);
            }
            else
            {
                s_HeadlessJobHandle = matrixHandle;
            }
            H8Memory.RegisterActiveJob(SystemID.Construction, s_HeadlessJobHandle);
            s_HeadlessJobScheduled = true;
            headlessJobScheduled = true;
            }
            finally
            {
                if (!headlessJobScheduled)
                {
                    s_HeadlessFrameTuning = default;
                    s_HeadlessFrameTuningValid = false;
                    ReleaseHeadlessSdfReadLease();
                    ReleaseDroneServiceCommandMutationGuard();
                    ReleaseDroneHeadlessJobMutationGuard();
                }
            }
        }

        private static float SanitizeHeadlessDeltaTime(float deltaTime)
        {
            return math.isfinite(deltaTime) ? math.max(0f, deltaTime) : 0f;
        }

        private static void AdvanceHeadlessSimulationClock(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            s_HeadlessSimulationClockSeconds = math.min(
                HeadlessSimulationClockMaxSeconds,
                s_HeadlessSimulationClockSeconds + deltaTime);
        }

        private static float ResolveHeadlessSimulationClockSeconds()
        {
            return s_HeadlessSimulationClockSeconds;
        }

        private static JobHandle ScheduleDroneMacroAStar(
            int frameIndex,
            int solveBudget,
            in DroneFleetTuningConstants tuning,
            in DroneSdfGrid sdfGrid,
            NativeArray<PathWaypointDTO> macroWaypoints,
            NativeArray<byte> macroWaypointStates,
            NativeArray<DroneNativeMinHeapNode> aStarOpenHeap,
            NativeArray<float> aStarGCosts,
            NativeArray<int> aStarCameFrom,
            NativeArray<byte> aStarNodeStates,
            NativeArray<int> macroRouteNodes,
            NativeArray<byte> macroRouteCounts,
            NativeArray<DroneAStarTelemetry> aStarTelemetry,
            NativeArray<DroneAStarPersistentState> aStarPersistentStates,
            NativeArray<HeadlessDroneState> droneStates)
        {
            if (!droneStates.IsCreated ||
                !macroWaypoints.IsCreated ||
                !macroWaypointStates.IsCreated ||
                !aStarOpenHeap.IsCreated ||
                !aStarGCosts.IsCreated ||
                !aStarCameFrom.IsCreated ||
                !aStarNodeStates.IsCreated ||
                !macroRouteNodes.IsCreated ||
                !macroRouteCounts.IsCreated ||
                !aStarTelemetry.IsCreated ||
                !aStarPersistentStates.IsCreated)
            {
                return default;
            }

            DroneMacroAStarJob job = new DroneMacroAStarJob
            {
                Drones = droneStates,
                Waypoints = macroWaypoints,
                WaypointStates = macroWaypointStates,
                OpenHeap = aStarOpenHeap,
                GCosts = aStarGCosts,
                CameFrom = aStarCameFrom,
                NodeStates = aStarNodeStates,
                RouteNodes = macroRouteNodes,
                RouteNodeCounts = macroRouteCounts,
                Telemetry = aStarTelemetry,
                SearchStates = aStarPersistentStates,
                SdfGrid = sdfGrid,
                FrameIndex = frameIndex,
                MaxSolves = solveBudget,
                RouteNodeStride = DroneAStarRouteNodeStride,
                CellSize = tuning.AStarCellSize,
                MaxNodesExpandedPerDrone = ResolveDroneAStarNodeBudget(in tuning),
                HeuristicWeight = ResolveDroneAStarHeuristicWeight(in tuning),
                RequiredDroneRadius = ResolveDroneRequiredRadius(in tuning)
            };

            return job.Schedule();
        }

        private static DroneFleetTuningConstants ResolveDroneTuning()
        {
            if (TryReadDroneTuningConstants(out NativeArray<DroneFleetTuningConstants>.ReadOnly tuningConstants) &&
                tuningConstants.Length > 0)
            {
                return SanitizeDroneTuning(tuningConstants[0]);
            }

            return DroneFleetTuningConstants.CreateDefault();
        }

        private static DroneFleetTuningConstants SanitizeDroneTuning(DroneFleetTuningConstants tuning)
        {
            DroneFleetTuningConstants fallback = DroneFleetTuningConstants.CreateDefault();
            if (tuning.MaxDroneSpeed <= 0f)
                tuning.MaxDroneSpeed = fallback.MaxDroneSpeed;
            if (tuning.BatteryDrainRate <= 0f)
                tuning.BatteryDrainRate = fallback.BatteryDrainRate;
            if (tuning.RepairSpeed <= 0f)
                tuning.RepairSpeed = fallback.RepairSpeed;
            if (tuning.CargoCapacity <= 0f)
                tuning.CargoCapacity = fallback.CargoCapacity;
            if (tuning.MiningHoldSeconds <= 0f)
                tuning.MiningHoldSeconds = fallback.MiningHoldSeconds;
            if (tuning.SurvivalSteeringHz <= 0f)
                tuning.SurvivalSteeringHz = fallback.SurvivalSteeringHz;
            if (tuning.StandardSteeringHz <= 0f)
                tuning.StandardSteeringHz = fallback.StandardSteeringHz;
            if (tuning.HighFidelitySteeringHz <= 0f)
                tuning.HighFidelitySteeringHz = fallback.HighFidelitySteeringHz;
            if (tuning.OverkillSteeringHz <= 0f)
                tuning.OverkillSteeringHz = fallback.OverkillSteeringHz;
            if (tuning.AStarCellSize <= 0f)
                tuning.AStarCellSize = fallback.AStarCellSize;
            if (tuning.SurvivalSolveBudget <= 0f)
                tuning.SurvivalSolveBudget = fallback.SurvivalSolveBudget;
            if (tuning.StandardSolveBudget <= 0f)
                tuning.StandardSolveBudget = fallback.StandardSolveBudget;
            if (tuning.HighFidelitySolveBudget <= 0f)
                tuning.HighFidelitySolveBudget = fallback.HighFidelitySolveBudget;
            if (tuning.OverkillSolveBudget <= 0f)
                tuning.OverkillSolveBudget = fallback.OverkillSolveBudget;

            tuning.MaxDroneSpeed = Mathf.Clamp(tuning.MaxDroneSpeed, 0.5f, 24f);
            tuning.BatteryDrainRate = Mathf.Clamp(tuning.BatteryDrainRate, 0.01f, 25f);
            tuning.SdfRepulsionStrength = Mathf.Clamp(tuning.SdfRepulsionStrength, 0f, 24f);
            tuning.RepairSpeed = Mathf.Clamp(tuning.RepairSpeed, 0.05f, 8f);
            tuning.CargoCapacity = Mathf.Clamp(tuning.CargoCapacity, 1f, 64f);
            tuning.MiningHoldSeconds = Mathf.Clamp(tuning.MiningHoldSeconds, 0.01f, 5f);
            tuning.SurvivalSteeringHz = Mathf.Clamp(tuning.SurvivalSteeringHz, 5f, 60f);
            tuning.StandardSteeringHz = Mathf.Clamp(tuning.StandardSteeringHz, 10f, 60f);
            tuning.HighFidelitySteeringHz = Mathf.Clamp(tuning.HighFidelitySteeringHz, 15f, 120f);
            tuning.OverkillSteeringHz = Mathf.Clamp(tuning.OverkillSteeringHz, 15f, 120f);
            tuning.AStarCellSize = Mathf.Clamp(tuning.AStarCellSize, 1f, 12f);
            tuning.SurvivalSolveBudget = Mathf.Clamp(tuning.SurvivalSolveBudget, 1f, HeadlessDroneCapacity);
            tuning.StandardSolveBudget = Mathf.Clamp(tuning.StandardSolveBudget, 1f, HeadlessDroneCapacity);
            tuning.HighFidelitySolveBudget = Mathf.Clamp(tuning.HighFidelitySolveBudget, 1f, HeadlessDroneCapacity);
            tuning.OverkillSolveBudget = Mathf.Clamp(tuning.OverkillSolveBudget, 1f, HeadlessDroneCapacity);
            tuning.Reserved0 = Mathf.Clamp(tuning.Reserved0, 0f, 4f);
            return tuning;
        }

        private static void ClearDroneChassisSpecs()
        {
            s_DroneChassisSpecCount = 0;
            if (!TryAcquireDroneChassisSpecs(
                    out NativeArray<DroneChassisSpecDTO> chassisSpecs,
                    out IDataVault vault))
            {
                return;
            }

            try
            {
                for (int i = 0; i < chassisSpecs.Length; i++)
                    chassisSpecs[i] = default;
            }
            finally
            {
                vault.ReleaseWriteLock(in s_DroneChassisSpecsHandle, SystemID.Construction);
            }
        }

        private static bool TryCreateDroneChassisAuthoringSeed(
            uint typeHash,
            in DroneFleetTuningConstants tuning,
            out DroneChassisSpecDTO spec)
        {
            spec = default;
            if (typeHash == 0u)
                return false;

            float speedScale = 1f;
            float drainScale = 1f;
            float repairScale = 1f;
            float cargoScale = 1f;
            float miningHoldScale = 1f;
            float clearanceRadius = DefaultDroneClearanceRadiusMeters;

            if (typeHash == DroneChassisMiningHash)
            {
                speedScale = 0.85f;
                drainScale = 0.9f;
                cargoScale = 1.5f;
                miningHoldScale = 0.85f;
                clearanceRadius = MiningDroneClearanceRadiusMeters;
            }
            else if (typeHash == DroneChassisHeavyMinerHash)
            {
                speedScale = 0.75f;
                drainScale = 0.95f;
                cargoScale = 2.0f;
                miningHoldScale = 0.75f;
                clearanceRadius = MiningDroneClearanceRadiusMeters;
            }
            else if (typeHash == DroneChassisMicroWelderHash)
            {
                speedScale = 1.2f;
                drainScale = 0.8f;
                repairScale = 1.15f;
                clearanceRadius = RepairDroneClearanceRadiusMeters;
            }
            else if (typeHash == DroneChassisCombatHash || typeHash == DroneChassisCutParasiteHash)
            {
                speedScale = 1.15f;
                drainScale = 1.25f;
                repairScale = 0.75f;
                cargoScale = 0.5f;
                clearanceRadius = CombatDroneClearanceRadiusMeters;
            }

            spec = new DroneChassisSpecDTO
            {
                TypeHash = typeHash,
                Flags = DroneChassisSpecValidFlag,
                MaxSpeed = tuning.MaxDroneSpeed * speedScale,
                BatteryCapacity = 100f,
                BatteryDrainRate = tuning.BatteryDrainRate * drainScale,
                RepairSpeed = tuning.RepairSpeed * repairScale,
                CargoCapacity = tuning.CargoCapacity * cargoScale,
                MiningHoldSeconds = tuning.MiningHoldSeconds * miningHoldScale,
                SdfRepulsionScale = 1f,
                ClearanceRadiusMeters = clearanceRadius
            };
            spec = SanitizeDroneChassisSpec(spec, in tuning);
            return true;
        }

        private static DroneChassisSpecDTO SanitizeDroneChassisSpec(DroneChassisSpecDTO spec, in DroneFleetTuningConstants tuning)
        {
            if (spec.TypeHash == 0u)
                spec.TypeHash = DroneChassisRepairHash;

            if (spec.MaxSpeed <= 0f)
                spec.MaxSpeed = tuning.MaxDroneSpeed;
            if (spec.BatteryCapacity <= 0f)
                spec.BatteryCapacity = 100f;
            if (spec.BatteryDrainRate <= 0f)
                spec.BatteryDrainRate = tuning.BatteryDrainRate;
            if (spec.RepairSpeed <= 0f)
                spec.RepairSpeed = tuning.RepairSpeed;
            if (spec.CargoCapacity <= 0f)
                spec.CargoCapacity = tuning.CargoCapacity;
            if (spec.MiningHoldSeconds <= 0f)
                spec.MiningHoldSeconds = tuning.MiningHoldSeconds;
            if (spec.SdfRepulsionScale <= 0f)
                spec.SdfRepulsionScale = 1f;
            if (spec.ClearanceRadiusMeters <= 0f)
                spec.ClearanceRadiusMeters = DefaultDroneClearanceRadiusMeters;

            spec.MaxSpeed = Mathf.Clamp(spec.MaxSpeed, 0.5f, 24f);
            spec.BatteryCapacity = Mathf.Clamp(spec.BatteryCapacity, 1f, 100f);
            spec.BatteryDrainRate = Mathf.Clamp(spec.BatteryDrainRate, 0.01f, 25f);
            spec.RepairSpeed = Mathf.Clamp(spec.RepairSpeed, 0.05f, 8f);
            spec.CargoCapacity = Mathf.Clamp(spec.CargoCapacity, 1f, 64f);
            spec.MiningHoldSeconds = Mathf.Clamp(spec.MiningHoldSeconds, 0.01f, 5f);
            spec.SdfRepulsionScale = Mathf.Clamp(spec.SdfRepulsionScale, 0.1f, 4f);
            spec.ClearanceRadiusMeters = Mathf.Clamp(spec.ClearanceRadiusMeters, 0.2f, 2.0f);
            spec.Flags |= DroneChassisSpecValidFlag;
            return spec;
        }

        private static void CommitDroneChassisSpecs(ReadOnlySpan<DroneChassisSpecDTO> stagedSpecs, int stagedCount)
        {
            if (stagedCount <= 0 ||
                !TryAcquireDroneChassisSpecs(
                    out NativeArray<DroneChassisSpecDTO> chassisSpecs,
                    out IDataVault vault))
            {
                return;
            }

            try
            {
                for (int i = 0; i < chassisSpecs.Length; i++)
                    chassisSpecs[i] = default;

                int count = Mathf.Min(stagedCount, chassisSpecs.Length);
                for (int i = 0; i < count; i++)
                    chassisSpecs[i] = stagedSpecs[i];

                s_DroneChassisSpecCount = count;
            }
            finally
            {
                vault.ReleaseWriteLock(in s_DroneChassisSpecsHandle, SystemID.Construction);
            }
        }

        private static bool TryUpsertStagedDroneChassisSpec(
            DroneChassisSpecDTO spec,
            in DroneFleetTuningConstants tuning,
            Span<DroneChassisSpecDTO> stagedSpecs,
            ref int stagedCount)
        {
            if (stagedSpecs.Length <= 0)
                return false;

            spec = SanitizeDroneChassisSpec(spec, in tuning);
            int count = Mathf.Clamp(stagedCount, 0, stagedSpecs.Length);
            stagedCount = count;
            for (int i = 0; i < count; i++)
            {
                if ((stagedSpecs[i].Flags & DroneChassisSpecValidFlag) == 0u ||
                    stagedSpecs[i].TypeHash != spec.TypeHash)
                {
                    continue;
                }

                stagedSpecs[i] = spec;
                return true;
            }

            if (count >= stagedSpecs.Length)
                return false;

            stagedSpecs[count] = spec;
            stagedCount = count + 1;
            return true;
        }

        private static bool TryResolveDroneChassisSpec(uint typeHash, out DroneChassisSpecDTO spec)
        {
            spec = default;
            if (s_DroneChassisSpecCount <= 0 ||
                !TryReadDroneVaultBuffer(
                    s_CachedDataVault,
                    in s_DroneChassisSpecsHandle,
                    DroneFleetChassisSpecsBufferId,
                    DroneChassisSpecCapacity,
                    out NativeArray<DroneChassisSpecDTO>.ReadOnly chassisSpecs))
            {
                return false;
            }

            int count = Mathf.Min(s_DroneChassisSpecCount, chassisSpecs.Length);
            for (int i = 0; i < count; i++)
            {
                DroneChassisSpecDTO candidate = chassisSpecs[i];
                if ((candidate.Flags & DroneChassisSpecValidFlag) == 0u || candidate.TypeHash != typeHash)
                    continue;

                spec = candidate;
                return true;
            }

            return false;
        }

        private static uint ResolveDroneChassisHash(DroneFleetTaskKind kind)
        {
            if (kind == DroneFleetTaskKind.MineNode)
                return DroneChassisMiningHash;

            if (kind == DroneFleetTaskKind.CutParasite)
                return DroneChassisCombatHash;

            return DroneChassisRepairHash;
        }

        private static bool TryResolveLaunchDroneChassisSpec(
            DroneFleetTaskKind kind,
            in DroneFleetTuningConstants tuning,
            out DroneChassisSpecDTO spec)
        {
            spec = default;
            if (kind == DroneFleetTaskKind.MineNode &&
                TryResolveDroneChassisSpec(DroneChassisHeavyMinerHash, out DroneChassisSpecDTO aliasSpec))
            {
                spec = SanitizeDroneChassisSpec(aliasSpec, in tuning);
                return true;
            }

            if (kind == DroneFleetTaskKind.RepairModule &&
                TryResolveDroneChassisSpec(DroneChassisMicroWelderHash, out aliasSpec))
            {
                spec = SanitizeDroneChassisSpec(aliasSpec, in tuning);
                return true;
            }

            uint typeHash = ResolveDroneChassisHash(kind);
            if (TryResolveDroneChassisSpec(typeHash, out spec))
            {
                spec = SanitizeDroneChassisSpec(spec, in tuning);
                return true;
            }

            if (kind == DroneFleetTaskKind.CutParasite &&
                TryResolveDroneChassisSpec(DroneChassisCutParasiteHash, out spec))
            {
                spec = SanitizeDroneChassisSpec(spec, in tuning);
                return true;
            }

            return false;
        }

        private static int ResolveDroneSteeringTickModulo(in DroneFleetTuningConstants tuning)
        {
            float quality = ResolveDroneSimulationQualityWeight();
            float lowHz = Mathf.Max(1f, tuning.SurvivalSteeringHz);
            float highHz = Mathf.Max(lowHz, tuning.OverkillSteeringHz);
            float targetHz = math.lerp(lowHz, highHz, quality);
            return Mathf.Clamp(Mathf.RoundToInt(60f / Mathf.Max(1f, targetHz)), 1, 12);
        }

        private static int ResolveDroneAStarSolveBudget(in DroneFleetTuningConstants tuning)
        {
            float quality = ResolveDroneSimulationQualityWeight();
            float smoothedQuality = quality * quality * (3f - (2f * quality));
            float budget = math.lerp(
                Mathf.Max(1f, tuning.SurvivalSolveBudget),
                Mathf.Max(1f, tuning.OverkillSolveBudget),
                smoothedQuality);
            return Mathf.Clamp(Mathf.RoundToInt(budget), 1, HeadlessDroneCapacity);
        }

        private static int ResolveDroneAStarNodeBudget(in DroneFleetTuningConstants tuning)
        {
            float quality = ResolveDroneSimulationQualityWeight();
            float smoothedQuality = quality * quality * (3f - (2f * quality));
            float lowBudget = math.max(48f, tuning.SurvivalSolveBudget * 24f);
            float highBudget = math.max(lowBudget, tuning.OverkillSolveBudget * 48f);
            float budget = math.lerp(lowBudget, highBudget, smoothedQuality);
            return Mathf.Clamp(Mathf.RoundToInt(budget), 16, DroneAStarNodeCapacity);
        }

        private static float ResolveDroneAStarHeuristicWeight(in DroneFleetTuningConstants tuning)
        {
            if (tuning.Reserved0 > 0f)
                return math.clamp(tuning.Reserved0, 1f, 4f);

            float quality = ResolveDroneSimulationQualityWeight();
            return math.lerp(2.25f, 1.05f, quality);
        }

        private static float ResolveDroneRequiredRadius(in DroneFleetTuningConstants tuning)
        {
            return Mathf.Clamp(tuning.AStarCellSize * 0.125f, 0.2f, 2f);
        }

        private static int ResolveDroneFramesBetweenUpdates()
        {
            float quality = ResolveDroneSimulationQualityWeight();
            return Mathf.Clamp((int)math.lerp(5f, 60f, 1f - quality), 5, 60);
        }

        private static float ResolveDroneTaskRebuildIntervalSeconds()
        {
            return ResolveDroneFramesBetweenUpdates() * (1f / 60f);
        }

        private static float ResolveGlobalQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private static float ResolveDroneSimulationQualityWeight()
        {
            return ResolveGlobalQualityWeight();
        }

        private static bool TryAcquireHeadlessDroneSdfGrid(
            in DroneFleetTuningConstants tuning,
            NativeArray<HeadlessDroneState> droneStates,
            out DroneSdfGrid grid)
        {
            ReleaseHeadlessSdfReadLease();
            float3 runtimeOrigin = ResolveDroneSdfQueryOrigin(droneStates);
            if (!TryAcquireDroneSdfGrid(
                    in tuning,
                    runtimeOrigin,
                    out grid,
                    out VoxelSonarSdfReadLease lease,
                    out IVoxelSonarSdfReadLeaseModel leaseModel))
            {
                return false;
            }

            s_HeadlessSdfReadLease = lease;
            s_HeadlessSdfReadLeaseModel = leaseModel;
            s_HeadlessSdfReadLeaseLocked = true;
            return true;
        }

        private static bool TryAcquireDroneSdfGrid(
            in DroneFleetTuningConstants tuning,
            float3 runtimeOrigin,
            out DroneSdfGrid grid,
            out VoxelSonarSdfReadLease lease,
            out IVoxelSonarSdfReadLeaseModel leaseModel)
        {
            grid = default;
            lease = default;
            leaseModel = s_CachedVoxelSdfReadLeaseModel;
            if (leaseModel == null || !IsFiniteFloat3(runtimeOrigin))
                return false;

            if (!leaseModel.TryAcquireNearestSonarSdfReadLease(
                    runtimeOrigin,
                    out NativeArray<byte>.ReadOnly encodedSdf,
                    out int3 dimensions,
                    out float3 volumeOrigin,
                    out float3 cellSize,
                    out float sdfRange,
                    out VoxelSonarSdfReadLease acquiredLease))
            {
                return false;
            }

            bool accepted = false;
            try
            {
                float repulsionDistance = Mathf.Max(0.5f, tuning.AStarCellSize * 0.65f);
                if (!acquiredLease.IsValid ||
                    !DroneSdfGrid.TryCreate(
                        encodedSdf,
                        dimensions,
                        volumeOrigin,
                        cellSize,
                        sdfRange,
                        repulsionDistance,
                        acquiredLease.Version,
                        out grid))
                {
                    return false;
                }

                lease = acquiredLease;
                accepted = true;
                return true;
            }
            finally
            {
                if (!accepted)
                    leaseModel.ReleaseNearestSonarSdfReadLease(in acquiredLease);
            }
        }

        private static float3 ResolveDroneSdfQueryOrigin(NativeArray<HeadlessDroneState> droneStates)
        {
            if (droneStates.IsCreated)
            {
                int limit = math.min(droneStates.Length, HeadlessDroneCapacity);
                for (int i = 0; i < limit; i++)
                {
                    HeadlessDroneState drone = droneStates[i];
                    if (drone.State == (byte)HeadlessDroneRuntimeState.Empty ||
                        drone.State == (byte)HeadlessDroneRuntimeState.Sacrificed ||
                        drone.State == (byte)HeadlessDroneRuntimeState.Completed ||
                        !IsFiniteFloat3(drone.Position))
                    {
                        continue;
                    }

                    return drone.Position;
                }
            }

            return float3.zero;
        }

        private static float3 ResolveDroneSdfQueryOrigin(NativeArray<HeadlessDroneState>.ReadOnly droneStates)
        {
            if (droneStates.IsCreated)
            {
                int limit = math.min(droneStates.Length, HeadlessDroneCapacity);
                for (int i = 0; i < limit; i++)
                {
                    HeadlessDroneState drone = droneStates[i];
                    if (drone.State == (byte)HeadlessDroneRuntimeState.Empty ||
                        drone.State == (byte)HeadlessDroneRuntimeState.Sacrificed ||
                        drone.State == (byte)HeadlessDroneRuntimeState.Completed ||
                        !IsFiniteFloat3(drone.Position))
                    {
                        continue;
                    }

                    return drone.Position;
                }
            }

            return float3.zero;
        }

        private static void ReleaseHeadlessSdfReadLease()
        {
            if (!s_HeadlessSdfReadLeaseLocked)
                return;

            IVoxelSonarSdfReadLeaseModel leaseModel = s_HeadlessSdfReadLeaseModel;
            VoxelSonarSdfReadLease lease = s_HeadlessSdfReadLease;
            s_HeadlessSdfReadLeaseLocked = false;
            s_HeadlessSdfReadLeaseModel = null;
            s_HeadlessSdfReadLease = default;
            if (leaseModel != null && lease.IsValid)
                leaseModel.ReleaseNearestSonarSdfReadLease(in lease);
        }

        private static void PublishDroneSdfFailClosedSignal(int activeDroneCount)
        {
            int failedCount = math.max(1, activeDroneCount);
            s_DroneAStarFailureCount += failedCount;
            s_LastDroneAStarStatus = 3;
            DroneAStarTelemetry telemetry = default;
            telemetry.FailedCount = failedCount;
            telemetry.LastStatus = 3;
            telemetry.ActiveCandidateCount = failedCount;
            PublishDronePathFailureSignal(in telemetry);
        }

        private static void PublishDroneChassisFailClosedSignal(int activeDroneCount)
        {
            int failedCount = math.max(1, activeDroneCount);
            s_DroneAStarFailureCount += failedCount;
            s_LastDroneAStarStatus = 4;
            DroneAStarTelemetry telemetry = default;
            telemetry.FailedCount = failedCount;
            telemetry.LastStatus = 4;
            telemetry.ActiveCandidateCount = failedCount;
            PublishDronePathFailureSignal(in telemetry, DroneChassisUnavailableGlitchReason);
        }

        private static void ReadDroneAStarTelemetry()
        {
            if (!TryOpenDroneVaultBuffer(
                    s_CachedDataVault,
                    in s_DroneAStarTelemetryHandle,
                    BufferID.ShinobuDroneFleetAStarTelemetry,
                    DroneAStarTelemetryCapacity,
                    out NativeArray<DroneAStarTelemetry> aStarTelemetry) ||
                aStarTelemetry.Length <= 0)
            {
                return;
            }

            DroneAStarTelemetry telemetry = aStarTelemetry[0];
            s_DroneAStarSolvedCount += telemetry.SolvedCount;
            s_DroneAStarFailureCount += telemetry.FailedCount;
            s_DroneAStarIterationCount += telemetry.IterationCount;
            s_LastDroneAStarStatus = telemetry.LastStatus;
            int attemptCount = telemetry.SolvedCount + telemetry.FailedCount;
            s_LastDroneAStarAveragePathfindingTimeMs = EstimateAStarAveragePathfindingTimeMs(telemetry.IterationCount, attemptCount);
            if (telemetry.FailedCount > 0 || telemetry.LastStatus == 2)
                PublishDronePathFailureSignal(in telemetry);
        }

        private static float EstimateAStarAveragePathfindingTimeMs(int iterationCount, int attemptCount)
        {
            if (attemptCount <= 0 || iterationCount <= 0)
                return 0f;

            float averageIterations = iterationCount * math.rcp(math.max(1f, attemptCount));
            return averageIterations * 0.000045f;
        }

        private static void PublishDronePathFailureSignal(in DroneAStarTelemetry telemetry)
        {
            PublishDronePathFailureSignal(in telemetry, DronePathFailureGlitchReason);
        }

        private static void PublishDronePathFailureSignal(in DroneAStarTelemetry telemetry, byte reason)
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (s_LastDronePathFailureSignalFrame == frame)
                return;

            s_LastDronePathFailureSignalFrame = frame;
            SystemGlitchSignal signal = default;
            signal.Frame = (uint)math.max(0, frame);
            signal.SourceId = DroneNavigationSignalSourceHash;
            signal.LocalHash = math.hash(new uint4(
                (uint)math.max(0, s_DroneAStarFailureCount),
                (uint)math.max(0, telemetry.IterationCount),
                (uint)math.max(0, telemetry.ActiveCandidateCount),
                (uint)math.max(0, telemetry.LastStatus)));
            signal.ExpectedHash = 0u;
            signal.Intensity01 = math.saturate(telemetry.FailedCount * 0.25f);
            signal.DurationSeconds = 0.5f;
            signal.Reason = reason;
            signal.Flags = 2;
            SignalBus<SystemGlitchSignal>.TryPushTracked(in signal, ref s_SignalPushDropCount);
        }

        private static void CompleteHeadlessSimulationAndApply()
        {
            bool publishSnapshotAfterGuardRelease = false;
            bool publishTelemetryAfterGuardRelease = false;
            if (!TryOpenDroneVaultBuffer(
                    s_CachedDataVault,
                    in s_DroneStatesHandle,
                    BufferID.ShinobuDroneFleetStates,
                    HeadlessDroneCapacity,
                    out NativeArray<HeadlessDroneState> _))
            {
                if (!s_HeadlessJobScheduled)
                {
                    ReleaseHeadlessSdfReadLease();
                    ReleaseDroneServiceCommandMutationGuard();
                    ReleaseDroneHeadlessJobMutationGuard();
                }
                return;
            }

            if (s_HeadlessJobScheduled)
            {
                if (!DispatcherJobSwap.TryComplete(ref s_HeadlessJobHandle, false))
                    return;

                s_HeadlessJobScheduled = false;
                ReleaseHeadlessSdfReadLease();
                VaultGenerationHandle<HeadlessDroneState> stateHandleSwap = s_DroneStatesHandle;
                s_DroneStatesHandle = s_DroneStateBackBufferHandle;
                s_DroneStateBackBufferHandle = stateHandleSwap;
                VaultGenerationHandle<float4x4> matrixHandleSwap = s_DroneRenderMatricesHandle;
                s_DroneRenderMatricesHandle = s_DroneRenderMatrixBackBufferHandle;
                s_DroneRenderMatrixBackBufferHandle = matrixHandleSwap;
                ReadDroneAStarTelemetry();
            }

            DroneFleetTuningConstants tuning = ResolveHeadlessCompletionTuning();
            bool coreMirrorGuardAcquired = false;
            IDataVault coreMirrorVault = null;
            NativeArray<HeadlessDroneState> droneStates = default;
            NativeArray<HeadlessDroneState> droneStateBackBuffer = default;
            NativeArray<float4x4> droneRenderMatrices = default;
            NativeArray<float4x4> droneRenderMatrixBackBuffer = default;
            NativeArray<float3> positionsSoA = default;
            NativeArray<byte> stateBytes = default;
            NativeArray<DroneStateDTO> stateDtos = default;
            NativeArray<DroneTargetDTO> targetDtos = default;
            bool coreReady = false;
            bool mirrorReady = false;
            if (s_DroneHeadlessJobMutationGuardHeld)
            {
                coreReady = TryOpenDroneCoreBuffers(
                    out droneStates,
                    out droneStateBackBuffer,
                    out droneRenderMatrices,
                    out droneRenderMatrixBackBuffer);

                mirrorReady = coreReady &&
                    TryOpenDroneMirrorBuffers(
                        out positionsSoA,
                        out stateBytes,
                        out stateDtos,
                        out targetDtos);
            }
            else if (TryAcquireDroneCoreMirrorMutationViews(
                    out droneStates,
                    out droneStateBackBuffer,
                    out droneRenderMatrices,
                    out droneRenderMatrixBackBuffer,
                    out positionsSoA,
                    out stateBytes,
                    out stateDtos,
                    out targetDtos,
                    out coreMirrorVault))
            {
                coreReady = true;
                mirrorReady = true;
                coreMirrorGuardAcquired = true;
            }

            if (!coreReady || !mirrorReady)
            {
                ReleaseDroneServiceCommandMutationGuard();
                ReleaseDroneHeadlessJobMutationGuard();
                return;
            }
            try
            {
                ApplyPendingControls(droneStates, droneStateBackBuffer, droneRenderMatrices, droneRenderMatrixBackBuffer, positionsSoA, stateBytes, stateDtos, targetDtos);
                ApplyCompletedHeadlessServices(droneStates, droneStateBackBuffer, droneRenderMatrices, droneRenderMatrixBackBuffer, positionsSoA, stateBytes, stateDtos, targetDtos);
                DrainDroneServiceCommandQueue(droneStates, droneStateBackBuffer, positionsSoA, stateBytes, stateDtos, targetDtos, in tuning);
                ReleaseDroneServiceCommandMutationGuard();
                ApplyPendingLaunches(droneStates, droneStateBackBuffer, droneRenderMatrices, droneRenderMatrixBackBuffer, positionsSoA, stateBytes, stateDtos, targetDtos, in tuning);
                RefreshHeadlessCounters(droneStates);
                UpdateDrawBounds();
                CaptureFleetBlackBoxFrame(droneStates);
                publishSnapshotAfterGuardRelease = true;
                publishTelemetryAfterGuardRelease = true;
            }
            finally
            {
                if (coreMirrorGuardAcquired)
                    ReleaseDroneMutationGuard(coreMirrorVault, DroneCoreMirrorMutationGuardMask);
                ReleaseDroneHeadlessJobMutationGuard();
            }

            if (publishSnapshotAfterGuardRelease)
                PublishSnapshot();

            if (publishTelemetryAfterGuardRelease)
                PublishFleetTelemetryIfDue();
        }

        private static DroneFleetTuningConstants ResolveHeadlessCompletionTuning()
        {
            return s_HeadlessFrameTuningValid
                ? s_HeadlessFrameTuning
                : ResolveDroneTuning();
        }

        private static void CompletePendingHeadlessJobForReset()
        {
            if (!s_HeadlessJobScheduled)
                return;

            // RESET SYNC BOUNDARY: SubsystemRegistration/disable can release Vault buffers immediately after this call.
            // The forced wait prevents worker threads from writing into released drone lanes. This path is cold and
            // outside gameplay cadence; normal fleet simulation uses DispatcherJobSwap.TryComplete(..., false).
            DispatcherJobSwap.TryComplete(ref s_HeadlessJobHandle, true);
            s_HeadlessJobScheduled = false;
            ReleaseHeadlessSdfReadLease();
            ReleaseDroneServiceCommandMutationGuard();
            ReleaseDroneHeadlessJobMutationGuard();
        }

        internal static void ApplyOriginShift(Vector3 shiftOffset)
        {
            if (!IsFiniteVector(shiftOffset) || shiftOffset.sqrMagnitude <= 0.000001f)
                return;

            EnsureInitialized();
            CompletePendingHeadlessJobForReset();
            if (!TryAcquireDroneOriginShiftMutationViews(
                    out NativeArray<HeadlessDroneState> droneStates,
                    out NativeArray<HeadlessDroneState> droneStateBackBuffer,
                    out NativeArray<float4x4> droneRenderMatrices,
                    out NativeArray<float4x4> droneRenderMatrixBackBuffer,
                    out NativeArray<float3> positionsSoA,
                    out IDataVault originShiftVault))
            {
                return;
            }

            float3 runtimeOffset = -(float3)(shiftOffset);
            DroneFleetOriginShiftJob job = new DroneFleetOriginShiftJob
            {
                DroneStates = droneStates,
                DroneStateBackBuffer = droneStateBackBuffer,
                RenderMatrices = droneRenderMatrices,
                RenderMatrixBackBuffer = droneRenderMatrixBackBuffer,
                DronePositions = positionsSoA,
                RuntimeOffset = runtimeOffset
            };
            try
            {
                JobHandle handle = job.Schedule(HeadlessDroneCapacity, DroneJobBatchSize);
                // ORIGIN-SHIFT SYNC BOUNDARY: the world rebase contract requires all drone runtime-space rows to be
                // shifted before the next owner phase reads managed mirrors or render bounds. This is a rare rebase
                // window, not the steady-state pathing loop, and must remain documented until dispatcher rebase phases
                // expose a non-blocking owner swap handle.
                DispatcherJobSwap.TryComplete(ref handle, true);
            }
            finally
            {
                ReleaseDroneMutationGuard(originShiftVault, DroneOriginShiftMutationGuardMask);
            }

            if (s_DronePositions != null)
            {
                Vector3 managedOffset = new Vector3(runtimeOffset.x, runtimeOffset.y, runtimeOffset.z);
                for (int slot = 0; slot < s_DronePositions.Length; slot++)
                    s_DronePositions[slot] += managedOffset;

                for (int launchIndex = 0; launchIndex < s_PendingLaunchCount && launchIndex < s_PendingLaunches.Length; launchIndex++)
                {
                    PendingDroneLaunch launch = s_PendingLaunches[launchIndex];
                    if (launch.Active == 0)
                        continue;

                    launch.HomePosition += managedOffset;
                    DroneFleetTask task = launch.Task;
                    task = new DroneFleetTask(task.Kind, task.Module, task.Position + managedOffset, task.Radius);
                    launch.Task = task;
                    s_PendingLaunches[launchIndex] = launch;
                }
            }

            UpdateDrawBounds();
        }

        private static void ApplyPendingControls(
            NativeArray<HeadlessDroneState> droneStates,
            NativeArray<HeadlessDroneState> droneStateBackBuffer,
            NativeArray<float4x4> droneRenderMatrices,
            NativeArray<float4x4> droneRenderMatrixBackBuffer,
            NativeArray<float3> positionsSoA,
            NativeArray<byte> stateBytes,
            NativeArray<DroneStateDTO> stateDtos,
            NativeArray<DroneTargetDTO> targetDtos)
        {
            if (s_DroneSlotDroneIds == null)
                return;

            for (int slot = 0; slot < s_DroneSlotDroneIds.Length; slot++)
            {
                if (s_PendingReleaseBySlot[slot])
                {
                    ClearHeadlessSlot(slot, true, droneStates, droneStateBackBuffer, droneRenderMatrices, droneRenderMatrixBackBuffer, positionsSoA, stateBytes, stateDtos, targetDtos);
                    s_PendingReleaseBySlot[slot] = false;
                    s_PendingAbortBySlot[slot] = false;
                    s_PendingHostileBySlot[slot] = false;
                    continue;
                }

                int droneId = s_DroneSlotDroneIds[slot];
                if (droneId <= 0)
                {
                    s_PendingAbortBySlot[slot] = false;
                    s_PendingHostileBySlot[slot] = false;
                    s_PendingResupplyGrantBySlot[slot] = false;
                    s_PendingResupplyFailureBySlot[slot] = false;
                    s_PendingResupplyReservationIdsBySlot[slot] = 0;
                    continue;
                }

                HeadlessDroneState drone = droneStates[slot];
                bool droneMutated = false;
                if (s_PendingResupplyGrantBySlot[slot])
                {
                    if (TryConsumeResolvedResupplyCommitAck(slot, true, ref drone, out bool resupplyDroneChanged))
                        droneMutated |= resupplyDroneChanged;
                }

                if (s_PendingResupplyFailureBySlot[slot])
                {
                    if (TryConsumeResolvedResupplyCommitAck(slot, false, ref drone, out bool resupplyDroneChanged))
                        droneMutated |= resupplyDroneChanged;
                }

                if (drone.State != (byte)HeadlessDroneRuntimeState.ResupplyCommitPending)
                    ClearPendingResupplyCommitAck(slot);

                if (s_PendingHostileBySlot[slot])
                {
                    s_LogicLeechHijackCount++;
                    drone.FactionBit = (byte)HeadlessDroneFactionBit.Hostile;
                    if (TryResolvePlayerPosition(out Vector3 playerPosition) &&
                        TryResolvePlayerAup(out double3 playerAup))
                    {
                        drone.TargetPosition = (float3)(playerPosition);
                        drone.TargetAup = playerAup;
                        drone.State = (byte)HeadlessDroneRuntimeState.Travel;
                        droneMutated = true;
                    }
                }

                if (s_PendingAbortBySlot[slot] && drone.State != (byte)HeadlessDroneRuntimeState.Empty)
                {
                    drone.TargetTaskIndex = EmptyTaskIndex;
                    drone.TargetPosition = drone.HomePosition;
                    drone.TargetAup = drone.HomeAup;
                    drone.State = (byte)HeadlessDroneRuntimeState.Return;
                    droneMutated = true;
                }

                s_PendingAbortBySlot[slot] = false;
                s_PendingHostileBySlot[slot] = false;
                if (droneMutated)
                {
                    if (droneStateBackBuffer.IsCreated && (uint)slot < (uint)droneStateBackBuffer.Length)
                        droneStateBackBuffer[slot] = drone;

                    float4x4 renderMatrix = float4x4.TRS(drone.Position, drone.Rotation, new float3(1f, 1f, 1f));
                    if (droneRenderMatrices.IsCreated && (uint)slot < (uint)droneRenderMatrices.Length)
                        droneRenderMatrices[slot] = renderMatrix;
                    if (droneRenderMatrixBackBuffer.IsCreated && (uint)slot < (uint)droneRenderMatrixBackBuffer.Length)
                        droneRenderMatrixBackBuffer[slot] = renderMatrix;

                    MirrorDroneSoA(slot, in drone, positionsSoA, stateBytes, stateDtos, targetDtos);
                }

                droneStates[slot] = drone;
            }
        }

        private static bool TryApplyResolvedResupplyCommitToLiveSlot(int slot, bool committed)
        {
            bool consumedAck = false;
            bool publishSnapshotAfterGuardRelease = false;
            if (!TryAcquireDroneCoreMirrorMutationViews(
                    out NativeArray<HeadlessDroneState> droneStates,
                    out NativeArray<HeadlessDroneState> droneStateBackBuffer,
                    out NativeArray<float4x4> droneRenderMatrices,
                    out NativeArray<float4x4> droneRenderMatrixBackBuffer,
                    out NativeArray<float3> positionsSoA,
                    out NativeArray<byte> stateBytes,
                    out NativeArray<DroneStateDTO> stateDtos,
                    out NativeArray<DroneTargetDTO> targetDtos,
                    out IDataVault coreMirrorVault))
            {
                return false;
            }

            try
            {
                if ((uint)slot >= (uint)HeadlessDroneCapacity ||
                    s_DroneSlotDroneIds == null ||
                    s_DroneSlotDroneIds[slot] <= 0)
                {
                    return consumedAck;
                }

                HeadlessDroneState drone = droneStates[slot];
                if (!TryConsumeResolvedResupplyCommitAck(slot, committed, ref drone, out bool droneChanged))
                    return consumedAck;

                consumedAck = true;

                if (droneChanged)
                {
                    droneStates[slot] = drone;
                    if (droneStateBackBuffer.IsCreated && (uint)slot < (uint)droneStateBackBuffer.Length)
                        droneStateBackBuffer[slot] = drone;

                    float4x4 renderMatrix = float4x4.TRS(drone.Position, drone.Rotation, new float3(1f, 1f, 1f));
                    if (droneRenderMatrices.IsCreated && (uint)slot < (uint)droneRenderMatrices.Length)
                        droneRenderMatrices[slot] = renderMatrix;
                    if (droneRenderMatrixBackBuffer.IsCreated && (uint)slot < (uint)droneRenderMatrixBackBuffer.Length)
                        droneRenderMatrixBackBuffer[slot] = renderMatrix;

                    MirrorDroneSoA(slot, in drone, positionsSoA, stateBytes, stateDtos, targetDtos);
                    RefreshHeadlessCounters(droneStates);
                    RefreshFleetStatusSnapshotFromDroneStates(droneStates);
                    UpdateDrawBounds();
                    publishSnapshotAfterGuardRelease = true;
                }
            }
            finally
            {
                ReleaseDroneMutationGuard(coreMirrorVault, DroneCoreMirrorMutationGuardMask);
            }

            if (publishSnapshotAfterGuardRelease)
                PublishSnapshot();

            return consumedAck;
        }

        private static bool TryConsumeResolvedResupplyCommitAck(
            int slot,
            bool committed,
            ref HeadlessDroneState drone,
            out bool droneChanged)
        {
            droneChanged = false;
            if (!HasPendingResupplyCommitAckSlot(slot))
                return false;

            if (drone.State != (byte)HeadlessDroneRuntimeState.ResupplyCommitPending)
            {
                ClearPendingResupplyCommitAck(slot);
                return true;
            }

            if (committed)
            {
                GrantDroneResupply(ref drone, 1);
            }
            else
            {
                drone.SolderUnits = 0;
                drone.TransactionProgress = 0f;
                ReturnDroneToHub(ref drone);
            }

            ClearPendingResupplyCommitAck(slot);
            droneChanged = true;
            return true;
        }

        private static bool HasPendingResupplyCommitAckSlot(int slot)
        {
            return slot >= 0 &&
                   s_PendingResupplyGrantBySlot != null &&
                   s_PendingResupplyFailureBySlot != null &&
                   s_PendingResupplyReservationIdsBySlot != null &&
                   slot < s_PendingResupplyGrantBySlot.Length &&
                   slot < s_PendingResupplyFailureBySlot.Length &&
                   slot < s_PendingResupplyReservationIdsBySlot.Length;
        }

        private static void ClearPendingResupplyCommitAck(int slot)
        {
            if (!HasPendingResupplyCommitAckSlot(slot))
                return;

            s_PendingResupplyGrantBySlot[slot] = false;
            s_PendingResupplyFailureBySlot[slot] = false;
            s_PendingResupplyReservationIdsBySlot[slot] = 0;
        }

        private static void ApplyDockingRequestSignals(
            NativeArray<HeadlessDroneState> droneStates,
            NativeArray<HeadlessDroneState> droneStateBackBuffer,
            NativeArray<float3> positionsSoA,
            NativeArray<byte> stateBytes,
            NativeArray<DroneStateDTO> stateDtos,
            NativeArray<DroneTargetDTO> targetDtos)
        {
            if (!droneStates.IsCreated || s_DroneSlotDroneIds == null)
                return;

            // VESTIGIAL LANE, NOT A BROKEN ONE. SignalBus<DockingRequestSignal> has no producer anywhere in
            // Assets/_Project/Scripts: no "new DockingRequestSignal" and no Push/TryPush/TryPushTracked exists.
            // Its only other occurrences are the struct (Core/Signals/GlobalSignalPayloads.CoreFoundation.cs:800),
            // the lane registration and size check (Core/Signals/GlobalSignals.RuntimeLifecycle.cs:955 and :289),
            // the reserved contract id HectonSignalLaneContract.cs:411, and the SanitizeDockingRequestSignal
            // guard in Core/Signals/SignalBusRuntime.cs:4819. So this loop has never executed.
            // Do NOT read that as "drones never dock" - docking is fully live on a DIRECT-CALL path that does not
            // touch this lane:
            //   - autonomous return-and-dock: DroneCognitionJob.cs:442-444 calls BeginDocking itself once a drone
            //     in Return state is inside DockingStartDistanceSq (also DroneCognitionJob.cs:703).
            //   - Return state is entered by ReturnDroneToHub, by DroneFleetNavigationKernel.cs:598, and by the
            //     abort path here, which RepairDroneHub.cs:897 drives through AbortHeadlessDrone.
            //   - the dock-pose retarget this handler performs (HomePosition/HomeAup/HomeRotation) is also done
            //     internally by TryAttachToAlternateHub when a drone is orphaned.
            //   - the ack half of the triad is live regardless: PublishDockingComplete/PublishDockingFailed fire
            //     for autonomous docks too, carrying drone.DockingRequestId == 0, and Power/ShinobuLogisticsRouter.cs:1267
            //     consumes both lanes (it filters SourceKind == VehicleDockingModule, so it ignores fleet acks).
            // What this lane uniquely adds is an EXTERNAL command to dock a named drone at an ARBITRARY AUP pose
            // with a correlated RequestId - a contracted extension point with sanitizer support and 52 bytes of
            // ReservedTail, for which no caller has ever been written. Treat it as a dormant command entry point.
            // Read is GetFrameSnapshot (non-destructive): a future producer can be added without disturbing this
            // reader, and this reader does not consume signals out from under anyone else.
            System.ReadOnlySpan<DockingRequestSignal> requests = SignalBus<DockingRequestSignal>.GetFrameSnapshot();
            for (int i = 0; i < requests.Length; i++)
            {
                DockingRequestSignal request = requests[i];
                int slot = ResolveHeadlessSlot(request.DroneId);
                if (slot < 0)
                {
                    PublishDockingFailedForMissingDrone(in request);
                    continue;
                }

                HeadlessDroneState drone = droneStates[slot];
                if (drone.State == (byte)HeadlessDroneRuntimeState.Empty ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Sacrificed ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Completed)
                {
                    PublishDockingFailed(slot, in drone, ToVector3(drone.Position), request.RequestId, DockingFailureReason.InvalidRequest);
                    continue;
                }

                if (request.HubGridId != 0 && request.HubGridId != drone.HubGridId)
                {
                    PublishDockingFailed(slot, in drone, ToVector3(drone.Position), request.RequestId, DockingFailureReason.InvalidRequest);
                    continue;
                }

                AbsoluteUniversePosition dockAup = request.DockAup.ToAup();
                if (!TryResolveRuntimeFloat3AupDelta(in dockAup, out float3 dockRuntime))
                {
                    PublishDockingFailed(slot, in drone, ToVector3(drone.Position), request.RequestId, DockingFailureReason.InvalidRequest);
                    continue;
                }

                float3 dockForward = NormalizeOrFallback(request.DockForward, ResolveForward(drone.HomeRotation));
                drone.HomePosition = dockRuntime;
                drone.HomeAup = dockAup.ToAbsoluteDouble3();
                drone.HomeRotation = quaternion.LookRotationSafe(dockForward, math.up());
                drone.TargetTaskIndex = EmptyTaskIndex;
                drone.TargetModuleId = 0;
                drone.TargetPosition = dockRuntime;
                drone.TargetAup = drone.HomeAup;
                drone.DockingRequestId = request.RequestId;
                DroneCognitionJob.BeginDocking(ref drone);

                droneStates[slot] = drone;
                if (droneStateBackBuffer.IsCreated)
                    droneStateBackBuffer[slot] = drone;

                if (s_DronePositions != null)
                    s_DronePositions[slot] = ToVector3(drone.Position);

                MirrorDroneSoA(slot, in drone, positionsSoA, stateBytes, stateDtos, targetDtos);
            }
        }

        private static void ResolveDockingObstacleAborts(
            NativeArray<HeadlessDroneState> droneStates,
            NativeArray<HeadlessDroneState> droneStateBackBuffer,
            NativeArray<int> telemetryAccumulator,
            NativeArray<float3> positionsSoA,
            NativeArray<byte> stateBytes,
            NativeArray<DroneStateDTO> stateDtos,
            NativeArray<DroneTargetDTO> targetDtos,
            in DroneSdfGrid sdfGrid,
            in DroneFleetTuningConstants tuning)
        {
            if (!droneStates.IsCreated ||
                s_DroneSlotDroneIds == null ||
                s_PendingReleaseBySlot == null)
            {
                return;
            }
            float clearanceRadius = ResolveDroneRequiredRadius(in tuning);
            int segmentCount = ResolveDockingObstacleSegmentCount();
            float invSegmentCount = math.rcp((float)segmentCount);
            for (int slot = 0; slot < HeadlessDroneCapacity; slot++)
            {
                if (s_DroneSlotDroneIds[slot] <= 0 || s_PendingReleaseBySlot[slot])
                    continue;

                HeadlessDroneState drone = droneStates[slot];
                if (drone.State != (byte)HeadlessDroneRuntimeState.Docking)
                    continue;

                float3 p0 = IsFiniteDouble3(drone.DockControlP0) ? (float3)(drone.DockControlP0) : drone.Position;
                float3 p1 = IsFiniteDouble3(drone.DockControlP1) ? (float3)(drone.DockControlP1) : p0;
                float3 p2 = IsFiniteDouble3(drone.DockControlP2) ? (float3)(drone.DockControlP2) : drone.HomePosition;
                float3 p3 = IsFiniteDouble3(drone.DockControlP3) ? (float3)(drone.DockControlP3) : drone.HomePosition;
                if (!IsFiniteFloat3(p0) || !IsFiniteFloat3(p1) || !IsFiniteFloat3(p2) || !IsFiniteFloat3(p3))
                    continue;

                float startT = math.saturate(drone.DockingElapsed);
                if (startT >= 1f)
                    continue;

                float3 segmentStart = IsFiniteFloat3(drone.Position)
                    ? drone.Position
                    : EvaluateDockingObstacleBezier(p0, p1, p2, p3, startT);
                for (int segment = 1; segment <= segmentCount; segment++)
                {
                    float segmentT = startT + ((1f - startT) * (segment * invSegmentCount));
                    float3 segmentEnd = EvaluateDockingObstacleBezier(p0, p1, p2, p3, segmentT);
                    if (TryResolveDockingSdfBlock(
                        segmentStart,
                        segmentEnd,
                        segment == segmentCount,
                        in sdfGrid,
                        clearanceRadius,
                        out float3 blockedPoint))
                    {
                        AbortDockingForObstacle(slot, ref drone, ToVector3(blockedPoint), droneStates, droneStateBackBuffer, telemetryAccumulator, positionsSoA, stateBytes, stateDtos, targetDtos);
                        break;
                    }

                    segmentStart = segmentEnd;
                }
            }
        }

        private static float3 EvaluateDockingObstacleBezier(float3 p0, float3 p1, float3 p2, float3 p3, float t)
        {
            float clampedT = math.saturate(t);
            float oneMinusT = 1f - clampedT;
            float oneMinusT2 = oneMinusT * oneMinusT;
            float t2 = clampedT * clampedT;
            return
                (oneMinusT2 * oneMinusT * p0) +
                (3f * oneMinusT2 * clampedT * p1) +
                (3f * oneMinusT * t2 * p2) +
                (t2 * clampedT * p3);
        }

        private static int ResolveDockingObstacleSegmentCount()
        {
            float quality = ResolveDroneSimulationQualityWeight();
            return Mathf.Clamp(1 + Mathf.RoundToInt(quality * (DockingObstacleProbeMaxSegments - 1)), 1, DockingObstacleProbeMaxSegments);
        }

        private static bool TryResolveDockingSdfBlock(
            float3 segmentStart,
            float3 segmentEnd,
            bool isLastSegment,
            in DroneSdfGrid sdfGrid,
            float clearanceRadius,
            out float3 blockedPoint)
        {
            blockedPoint = segmentEnd;
            float3 delta = segmentEnd - segmentStart;
            float lengthSq = math.lengthsq(delta);
            if (!IsFiniteFloat3(delta) ||
                !math.isfinite(lengthSq) ||
                lengthSq <= DockingMinimumProbeDistanceMeters * DockingMinimumProbeDistanceMeters)
            {
                return false;
            }

            float lengthInv = math.rsqrt(lengthSq);
            float length = lengthSq * lengthInv;
            float probeDistance = length - (isLastSegment ? DockingObstacleProbeEndpointTrimMeters : 0f);
            if (!math.isfinite(probeDistance) || probeDistance <= DockingMinimumProbeDistanceMeters)
                return false;

            int samples = Mathf.Clamp(Mathf.CeilToInt(probeDistance / Mathf.Max(0.25f, clearanceRadius)), 1, 8);
            float3 direction = delta * lengthInv;
            for (int i = 1; i <= samples; i++)
            {
                float distance = probeDistance * (i * math.rcp((float)samples + 1f));
                float3 point = segmentStart + (direction * distance);
                if (sdfGrid.IsBlockedForRadius(point, clearanceRadius))
                {
                    blockedPoint = point;
                    return true;
                }
            }

            return false;
        }

        private static void AbortDockingForObstacle(
            int slot,
            ref HeadlessDroneState drone,
            Vector3 hitPoint,
            NativeArray<HeadlessDroneState> droneStates,
            NativeArray<HeadlessDroneState> droneStateBackBuffer,
            NativeArray<int> telemetryAccumulator,
            NativeArray<float3> positionsSoA,
            NativeArray<byte> stateBytes,
            NativeArray<DroneStateDTO> stateDtos,
            NativeArray<DroneTargetDTO> targetDtos)
        {
            drone.TargetTaskIndex = EmptyTaskIndex;
            drone.TargetModuleId = 0;
            drone.TargetPosition = ResolveOrphanWanderTarget(slot, drone.Position);
            drone.TargetAup = drone.PositionAup + global::Hecton8.World.AUPMath.ToDouble3(drone.TargetPosition - drone.Position);
            drone.DockingElapsed = 0f;
            drone.DockingFlags = 0;
            drone.DockingPathLengthMeters = 0f;
            drone.Velocity = float3.zero;
            drone.State = (byte)HeadlessDroneRuntimeState.Wander;
            droneStates[slot] = drone;

            if (droneStateBackBuffer.IsCreated)
                droneStateBackBuffer[slot] = drone;

            if (s_DronePositions != null)
                s_DronePositions[slot] = ToVector3(drone.Position);

            IncrementDockingAbortTelemetry(telemetryAccumulator);
            MirrorDroneSoA(slot, in drone, positionsSoA, stateBytes, stateDtos, targetDtos);
            PublishDockingFailed(slot, in drone, hitPoint, DockingFailureReason.ObstacleBlocked);
        }

        private static void IncrementDockingAbortTelemetry(NativeArray<int> telemetryAccumulator)
        {
            s_DockingAbortCount++;
            if (telemetryAccumulator.IsCreated &&
                telemetryAccumulator.Length > (int)DroneFleetTelemetryAccumulatorSlot.DockingAborts)
            {
                telemetryAccumulator[(int)DroneFleetTelemetryAccumulatorSlot.DockingAborts]++;
            }
        }

        private static void PublishDockingHatchOpen(int slot)
        {
            if (s_DroneHubs == null || slot < 0 || slot >= s_DroneHubs.Length)
                return;

            BaseAirlock airlock = s_DroneHubs[slot] != null ? s_DroneHubs[slot].DockingAirlock : null;
            if (airlock != null)
                BaseAirlockEvents.TryRaiseCycleStarted(airlock, null);
        }

        private static void PublishPendingDockingHatchOpen(int slot, ref HeadlessDroneState drone)
        {
            if ((drone.DockingFlags & DroneCognitionJob.DockingFlagHatchOpenQueued) == 0 ||
                (drone.DockingFlags & DroneCognitionJob.DockingFlagHatchOpenPublished) != 0)
            {
                return;
            }

            PublishDockingHatchOpen(slot);
            drone.DockingFlags |= DroneCognitionJob.DockingFlagHatchOpenPublished;
        }

        private static void PublishDockingComplete(in HeadlessDroneState drone)
        {
            Vector3 dockRuntime = IsFiniteFloat3(drone.HomePosition)
                ? ToVector3(drone.HomePosition)
                : ToVector3(drone.Position);
            AbsoluteUniversePosition dockAup;
            if (IsFiniteDouble3(drone.HomeAup))
                dockAup = AbsoluteUniversePosition.FromAbsolutePosition(drone.HomeAup);
            else if (IsFiniteDouble3(drone.PositionAup))
                dockAup = AbsoluteUniversePosition.FromAbsolutePosition(drone.PositionAup);
            else if (!TryResolveAbsoluteAupFromRuntimeOrigin(dockRuntime, out dockAup))
                return;

            float3 dockForward = ResolveForward(drone.HomeRotation);

            DockingCompleteSignal signal = new DockingCompleteSignal
            {
                DroneId = drone.DroneId,
                HubGridId = drone.HubGridId,
                DockAup = AbsoluteUniversePositionBlit.FromAup(in dockAup),
                DockForward = dockForward,
                RequestId = drone.DockingRequestId,
                Flags = drone.DockingFlags,
                SourceKind = DockingSignalSourceKinds.DroneFleet,
                Reserved1 = 0,
                Reserved2 = 0,
                ReservedTail = 0u
            };
            if (!SignalBus<DockingCompleteSignal>.TryPushTracked(in signal, ref s_SignalPushDropCount))
                RecordDockingCompleteSignalRejected();
        }

        private static void PublishDockingFailed(int slot, in HeadlessDroneState drone, Vector3 hitPoint, DockingFailureReason reason)
        {
            PublishDockingFailed(slot, in drone, hitPoint, drone.DockingRequestId, reason);
        }

        private static void PublishDockingFailed(int slot, in HeadlessDroneState drone, Vector3 hitPoint, uint requestId, DockingFailureReason reason)
        {
            AbsoluteUniversePosition lastAup;
            if (IsFiniteDouble3(drone.PositionAup))
                lastAup = AbsoluteUniversePosition.FromAbsolutePosition(drone.PositionAup);
            else if (!TryResolveAbsoluteAupFromRuntimeOrigin(drone.Position, out lastAup))
                return;

            Vector3 failureVector = hitPoint - ToVector3(drone.Position);
            float3 finiteFailureVector = IsFiniteVector(failureVector)
                ? (float3)(failureVector)
                : float3.zero;
            DockingFailedSignal signal = new DockingFailedSignal
            {
                DroneId = drone.DroneId,
                HubGridId = drone.HubGridId,
                LastAup = AbsoluteUniversePositionBlit.FromAup(in lastAup),
                FailureVector = finiteFailureVector,
                RequestId = requestId,
                Reason = (byte)reason,
                Flags = 0,
                SourceKind = DockingSignalSourceKinds.DroneFleet,
                Reserved1 = 0,
                ReservedTail = 0u
            };
            if (!SignalBus<DockingFailedSignal>.TryPushTracked(in signal, ref s_SignalPushDropCount))
                RecordDockingFailedSignalRejected();
        }

        private static void PublishDockingFailedForMissingDrone(in DockingRequestSignal request)
        {
            DockingFailedSignal signal = new DockingFailedSignal
            {
                DroneId = request.DroneId,
                HubGridId = request.HubGridId,
                LastAup = request.DockAup,
                FailureVector = float3.zero,
                RequestId = request.RequestId,
                Reason = (byte)DockingFailureReason.InvalidRequest,
                Flags = 0,
                SourceKind = DockingSignalSourceKinds.DroneFleet,
                Reserved1 = 0,
                ReservedTail = 0u
            };
            if (!SignalBus<DockingFailedSignal>.TryPushTracked(in signal, ref s_SignalPushDropCount))
                RecordDockingFailedSignalRejected();
        }

        private static void RecordDockingCompleteSignalRejected()
        {
            System.Threading.Interlocked.Increment(ref s_DockingCompleteSignalDropCount);
        }

        private static void RecordDockingFailedSignalRejected()
        {
            System.Threading.Interlocked.Increment(ref s_DockingFailedSignalDropCount);
        }

        private static void RecordInventoryCommandSignalRejected()
        {
            System.Threading.Interlocked.Increment(ref s_InventoryCommandSignalDropCount);
        }

        private static void RecordItemAcquiredSignalRejected()
        {
            System.Threading.Interlocked.Increment(ref s_ItemAcquiredSignalDropCount);
        }

        private static void RecordInventoryTransactionSignalRejected()
        {
            System.Threading.Interlocked.Increment(ref s_InventoryTransactionSignalDropCount);
        }

        private static void RecordDroneMiningCommitFailed(int slot, byte reason)
        {
            byte safeReason = reason != DroneMiningCommitFailureNone
                ? reason
                : DroneMiningCommitFailureInvalidPayload;
            if ((uint)slot < (uint)HeadlessDroneCapacity &&
                s_DroneMiningCommitFailureReasonsBySlot != null &&
                slot < s_DroneMiningCommitFailureReasonsBySlot.Length)
            {
                if (s_DroneMiningCommitFailureReasonsBySlot[slot] == safeReason)
                    return;

                s_DroneMiningCommitFailureReasonsBySlot[slot] = safeReason;
            }

            System.Threading.Interlocked.Increment(ref s_DroneMiningCommitFailureCount);
            System.Threading.Volatile.Write(ref s_LastDroneMiningCommitFailureReason, safeReason);
            RecordInventoryTransactionSignalRejected();
        }

        private static void ClearDroneMiningCommitFailureLatch(int slot)
        {
            if ((uint)slot >= (uint)HeadlessDroneCapacity ||
                s_DroneMiningCommitFailureReasonsBySlot == null ||
                slot >= s_DroneMiningCommitFailureReasonsBySlot.Length)
            {
                return;
            }

            s_DroneMiningCommitFailureReasonsBySlot[slot] = DroneMiningCommitFailureNone;
        }

        private static void ReportStorageReservationStaleAck(int requesterId)
        {
            System.Threading.Interlocked.Increment(ref s_StorageReservationStaleAckCount);
            PublishStorageReservationAckWarningBestEffort(
                s_StorageReservationStaleAckWarningHash,
                math.max(0, requesterId));
        }

        private static void ReportStorageReservationMismatchAck(int reservationId)
        {
            System.Threading.Interlocked.Increment(ref s_StorageReservationMismatchAckCount);
            PublishStorageReservationAckWarningBestEffort(
                s_StorageReservationMismatchAckWarningHash,
                math.max(0, reservationId));
        }

        private static void PublishStorageReservationAckWarningBestEffort(uint warningHash, float value)
        {
            try
            {
                GlobalTelemetryBus.PublishPerformanceWarning(warningHash, s_StorageReservationAckContextHash, value);
            }
            catch (System.Exception exception) when (!(exception is FatalArchitectureException))
            {
                LogStorageReservationAckTelemetryException(exception);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogStorageReservationAckTelemetryException(System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.LogException(exception);
#endif
        }

        private static void ApplyCompletedHeadlessServices(
            NativeArray<HeadlessDroneState> droneStates,
            NativeArray<HeadlessDroneState> droneStateBackBuffer,
            NativeArray<float4x4> droneRenderMatrices,
            NativeArray<float4x4> droneRenderMatrixBackBuffer,
            NativeArray<float3> positionsSoA,
            NativeArray<byte> stateBytes,
            NativeArray<DroneStateDTO> stateDtos,
            NativeArray<DroneTargetDTO> targetDtos)
        {
            for (int slot = 0; slot < HeadlessDroneCapacity; slot++)
            {
                int droneId = s_DroneSlotDroneIds[slot];
                if (droneId <= 0)
                    continue;

                HeadlessDroneState drone = droneStates[slot];
                if (drone.State == (byte)HeadlessDroneRuntimeState.Empty)
                    continue;

                s_DronePositions[slot] = ToVector3(drone.Position);
                SyncManagedTaskReference(slot, ref drone);

                if (drone.State == (byte)HeadlessDroneRuntimeState.Completed)
                {
                    s_DroneTasksCompletedCount++;
                    PublishPendingDockingHatchOpen(slot, ref drone);
                    PublishDockingComplete(in drone);
                    ClearHeadlessSlot(slot, true, droneStates, droneStateBackBuffer, droneRenderMatrices, droneRenderMatrixBackBuffer, positionsSoA, stateBytes, stateDtos, targetDtos);
                    continue;
                }

                if (drone.State == (byte)HeadlessDroneRuntimeState.Sacrificed)
                {
                    ClearHeadlessSlot(slot, true, droneStates, droneStateBackBuffer, droneRenderMatrices, droneRenderMatrixBackBuffer, positionsSoA, stateBytes, stateDtos, targetDtos);
                    continue;
                }

                if (TryResolveHubOrphan(slot, ref drone))
                {
                    droneStates[slot] = drone;
                    continue;
                }

                if (drone.State == (byte)HeadlessDroneRuntimeState.ResupplyDocked)
                {
                    ApplyHeadlessResupply(slot, ref drone);
                    droneStates[slot] = drone;
                    continue;
                }

                if (drone.State == (byte)HeadlessDroneRuntimeState.Stasis)
                {
                    TryQueueStasisWakeRequest(slot, ref drone);
                    droneStates[slot] = drone;
                    continue;
                }

                if (drone.State == (byte)HeadlessDroneRuntimeState.Repair ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Attack)
                {
                    if (TryBeginHijackRebootIfSourceGone(slot, ref drone))
                    {
                        droneStates[slot] = drone;
                        MirrorDroneSoA(slot, in drone, positionsSoA, stateBytes, stateDtos, targetDtos);
                        continue;
                    }

                    droneStates[slot] = drone;
                    MirrorDroneSoA(slot, in drone, positionsSoA, stateBytes, stateDtos, targetDtos);
                }
            }
        }

        private static void DrainDroneServiceCommandQueue(
            NativeArray<HeadlessDroneState> droneStates,
            NativeArray<HeadlessDroneState> droneStateBackBuffer,
            NativeArray<float3> positionsSoA,
            NativeArray<byte> stateBytes,
            NativeArray<DroneStateDTO> stateDtos,
            NativeArray<DroneTargetDTO> targetDtos,
            in DroneFleetTuningConstants tuning)
        {
            if (!s_DroneServiceCommandMutationGuardHeld ||
                !TryResolveDroneServiceCommandBuffers(
                    out NativeArray<DroneServiceCommand> serviceCommands,
                    out NativeArray<DroneServiceCommandCursor> serviceCommandCursor) ||
                serviceCommandCursor.Length <= 0)
            {
                return;
            }

            bool transactionBuffersAvailable = CompleteScheduledDroneServiceTransactionBatch(
                false,
                droneStates,
                positionsSoA,
                stateBytes,
                stateDtos,
                targetDtos,
                in tuning);
            s_DroneTransactionConsumedMaskCurrent = false;
            int commandCount = Mathf.Clamp(serviceCommandCursor[0].Count, 0, Mathf.Min(DroneServiceCommandCapacity, serviceCommands.Length));
            if (transactionBuffersAvailable)
            {
                ExecuteDroneServiceTransactionBatch(
                    commandCount,
                    serviceCommands,
                    droneStates,
                    positionsSoA,
                    stateBytes,
                    stateDtos,
                    targetDtos,
                    in tuning);
            }
            for (int commandIndex = 0; commandIndex < commandCount; commandIndex++)
            {
                if (IsDroneServiceTransactionCommandConsumed(commandIndex))
                    continue;

                DroneServiceCommand command = serviceCommands[commandIndex];
                if (!transactionBuffersAvailable && ShouldDeferDroneServiceWhileTransactionPending(in command))
                    continue;

                int slot = command.Slot;
                if (slot < 0 || slot >= HeadlessDroneCapacity || s_DroneSlotDroneIds == null)
                    continue;

                if (command.DroneId <= 0 || s_DroneSlotDroneIds[slot] != command.DroneId)
                    continue;

                HeadlessDroneState drone = droneStates[slot];
                if (command.Kind == (byte)DroneServiceCommandKind.DockingHatchOpen)
                {
                    PublishPendingDockingHatchOpen(slot, ref drone);
                    droneStates[slot] = drone;

                    if (droneStateBackBuffer.IsCreated)
                        droneStateBackBuffer[slot] = drone;

                    continue;
                }

                if (drone.DroneId != command.DroneId ||
                    (drone.State != (byte)HeadlessDroneRuntimeState.Repair &&
                     drone.State != (byte)HeadlessDroneRuntimeState.Attack))
                {
                    continue;
                }

                if (TryBeginHijackRebootIfSourceGone(slot, ref drone))
                {
                    droneStates[slot] = drone;
                    MirrorDroneSoA(slot, in drone, positionsSoA, stateBytes, stateDtos, targetDtos);
                    continue;
                }

                float serviceDt = Mathf.Max(0f, command.DeltaTime);
                if (command.Kind == (byte)DroneServiceCommandKind.Attack ||
                    drone.FactionBit == (byte)HeadlessDroneFactionBit.Hostile)
                {
                    ApplyHostileHijackService(slot, ref drone, serviceDt);
                }
                else if (s_DroneTaskKindsBySlot[slot] == DroneFleetTaskKind.MineNode)
                {
                    ApplyMiningService(slot, ref drone, serviceDt, in tuning);
                }
                else if (s_DroneTaskKindsBySlot[slot] == DroneFleetTaskKind.CutParasite)
                {
                    ApplyParasiteAttackService(slot, ref drone, serviceDt);
                }
                else
                {
                    ApplyFriendlyRepairService(slot, ref drone, serviceDt);
                }

                droneStates[slot] = drone;
                MirrorDroneSoA(slot, in drone, positionsSoA, stateBytes, stateDtos, targetDtos);
            }

            RecordDroneTransactionOwnerFrame(commandCount);
            serviceCommandCursor[0] = default;
            s_DroneTransactionConsumedMaskCurrent = false;
        }

        private static void ApplyHeadlessResupply(int slot, ref HeadlessDroneState drone)
        {
            RepairDroneHub hub = s_DroneHubs[slot];
            if (hub == null || !hub.TryQueueDroneResupplyCommit(1, drone.DroneId, out bool committedImmediately, out int queuedReservationId))
            {
                drone.State = (byte)HeadlessDroneRuntimeState.Stasis;
                drone.Velocity = float3.zero;
                drone.TransactionProgress = 0f;
                s_PendingResupplyReservationIdsBySlot[slot] = 0;
                return;
            }

            if (committedImmediately)
            {
                s_PendingResupplyReservationIdsBySlot[slot] = 0;
                GrantDroneResupply(ref drone, 1);
                return;
            }

            s_PendingResupplyReservationIdsBySlot[slot] = queuedReservationId;
            drone.State = (byte)HeadlessDroneRuntimeState.ResupplyCommitPending;
            drone.Velocity = float3.zero;
            drone.TransactionProgress = 0.5f;
        }

        private static void GrantDroneResupply(ref HeadlessDroneState drone, int grantedUnits)
        {
            int units = Mathf.Max(1, grantedUnits);
            drone.SolderUnits += units;
            drone.LoadedSolderCapacity = Mathf.Max(drone.LoadedSolderCapacity, drone.SolderUnits);
            drone.TransactionProgress = 1f;
            drone.State = drone.TargetTaskIndex >= 0
                ? (byte)HeadlessDroneRuntimeState.Travel
                : (byte)HeadlessDroneRuntimeState.Idle;
            drone.Velocity = float3.zero;
            DroneFleetInventoryTransactionSignal signal = new DroneFleetInventoryTransactionSignal
            {
                DroneId = drone.DroneId,
                SourceId = drone.HubGridId,
                DestinationId = drone.DroneId,
                ItemHash = (int)DroneRepairSparksSignalHash,
                Quantity = units,
                Position = drone.Position,
                Flags = 1u,
                Reserved0 = 0u
            };
            if (!SignalBus<DroneFleetInventoryTransactionSignal>.TryPushTracked(in signal, ref s_SignalPushDropCount))
                RecordInventoryTransactionSignalRejected();
        }

        private static void TryQueueStasisWakeRequest(int slot, ref HeadlessDroneState drone)
        {
            RepairDroneHub hub = s_DroneHubs[slot];
            if (hub == null)
                return;

            if (!hub.TryResolveNearestSupplyEndpoint(ToVector3(drone.Position), out Vector3 endpointPosition))
                return;

            if (!TryResolveAupDoubleFromRuntimeOrigin(endpointPosition, out double3 endpointAup))
                return;

            drone.SupplyPosition = (float3)(endpointPosition);
            drone.SupplyAup = endpointAup;
            drone.State = (byte)HeadlessDroneRuntimeState.ResupplyTravel;
            drone.Velocity = float3.zero;
        }

        private static bool TryBeginHijackRebootIfSourceGone(int slot, ref HeadlessDroneState drone)
        {
            if (drone.FactionBit != (byte)HeadlessDroneFactionBit.Hostile)
                return false;

            if (s_DroneTaskKindsBySlot[slot] != DroneFleetTaskKind.CutParasite)
                return false;

            BaseModule target = s_TargetModulesByDroneSlot[slot];
            if (target != null && target.ParasiteInfectionLevel > 0.0001f)
                return false;

            drone.FactionBit = (byte)HeadlessDroneFactionBit.Friendly;
            drone.TargetTaskIndex = EmptyTaskIndex;
            drone.RebootElapsed = 0f;
            drone.Velocity = float3.zero;
            drone.State = (byte)HeadlessDroneRuntimeState.Reboot;
            return true;
        }

        private static bool TryResolveHubOrphan(int slot, ref HeadlessDroneState drone)
        {
            RepairDroneHub currentHub = s_DroneHubs[slot];
            if (currentHub != null && currentHub.isActiveAndEnabled)
                return false;

            drone.HubGridId = InvalidHubId;
            s_DroneHubs[slot] = null;
            if (TryAttachToAlternateHub(slot, ref drone))
                return true;

            drone.TargetTaskIndex = EmptyTaskIndex;
            drone.TargetPosition = ResolveOrphanWanderTarget(slot, drone.Position);
            drone.TargetAup = drone.PositionAup + global::Hecton8.World.AUPMath.ToDouble3(drone.TargetPosition - drone.Position);
            drone.State = (byte)HeadlessDroneRuntimeState.Wander;
            drone.Velocity = float3.zero;
            return true;
        }

        private static bool TryAttachToAlternateHub(int slot, ref HeadlessDroneState drone)
        {
            int hubCount = RepairDroneHub.ActiveHubCount;
            BaseModule target = s_TargetModulesByDroneSlot[slot];
            RepairDroneHub bestHub = null;
            float bestDistanceSq = float.MaxValue;
            Vector3 dronePosition = ToVector3(drone.Position);

            int scanHubCount = Mathf.Min(hubCount, MaxMainThreadHubScanCount);
            for (int i = 0; i < scanHubCount; i++)
            {
                RepairDroneHub candidate = RepairDroneHub.GetActiveHubAt(i);
                if (candidate == null || !candidate.isActiveAndEnabled || !candidate.HasOperationalPower)
                    continue;

                if (target != null && IsDifferentGrid(candidate.CurrentGrid, target))
                    continue;

                Vector3 candidateDock = candidate.DockPosition;
                float distanceSq = (candidateDock - dronePosition).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestHub = candidate;
            }

            if (bestHub == null || !bestHub.TryAttachOrphanedDrone(drone.DroneId))
                return false;

            s_DroneHubs[slot] = bestHub;
            drone.HubGridId = ResolveHubTaskKey(bestHub);
            AbsoluteUniversePosition hubDockAup = bestHub.DockAup;
            if (!hubDockAup.IsFinite())
                return false;

            drone.HomePosition = (float3)(bestHub.DockPosition);
            drone.HomeAup = hubDockAup.ToAbsoluteDouble3();
            drone.HomeRotation = ToQuaternion(bestHub.DockRotation);
            drone.TargetTaskIndex = EmptyTaskIndex;
            drone.TargetPosition = drone.HomePosition;
            drone.TargetAup = drone.HomeAup;
            drone.State = (byte)HeadlessDroneRuntimeState.Return;
            drone.Velocity = float3.zero;
            return true;
        }

        private static float3 ResolveOrphanWanderTarget(int slot, float3 position)
        {
            float angle = (slot * 2.3999631f) + 0.7853982f;
            return position + new float3(
                CinematicMath.FastCos(angle) * OrphanWanderDistanceMeters,
                0f,
                CinematicMath.FastSin(angle) * OrphanWanderDistanceMeters);
        }

        private static void ApplyFriendlyRepairService(int slot, ref HeadlessDroneState drone, float dt)
        {
            BaseModule target = s_TargetModulesByDroneSlot[slot];
            if (target == null)
            {
                ReturnDroneToHub(ref drone);
                return;
            }

            if (s_FleetSacrificeRequested && IsSacrificeEligible(target))
            {
                ExecuteSacrifice(slot, ref drone, target);
                return;
            }

            if (drone.SolderUnits <= 0)
            {
                RouteDroneToSupplyOrStasis(slot, ref drone);
                return;
            }

            float recoverableIntegrity = Mathf.Max(1f, target.MaxRecoverableIntegrity);
            if (target.CurrentIntegrity >= recoverableIntegrity && !target.IsFlooded && !target.HasCascadeFailure)
            {
                ReturnDroneToHub(ref drone);
                return;
            }

            float repairAmount = Mathf.Max(0f, drone.RepairRatePerSecond * dt);
            if (repairAmount <= 0f)
                return;

            DroneFleetRepairServiceSignal repairSignal = new DroneFleetRepairServiceSignal
            {
                DroneId = drone.DroneId,
                TargetModuleId = GetRuntimeId(target),
                RepairUnits = repairAmount,
                Position = drone.Position,
                Flags = 0u,
                Reserved0 = 0u
            };
            SignalBus<DroneFleetRepairServiceSignal>.TryPushTracked(in repairSignal, ref s_SignalPushDropCount);
            PublishHullRepairedByDrone(slot, in drone, target, repairAmount);
            DispatchRepairWeld(slot, in drone, target);
            ConsumeSolderByWork(ref drone, repairAmount, SolderIntegrityUnitsPerBundle);
        }

        private static void ApplyMiningService(int slot, ref HeadlessDroneState drone, float dt, in DroneFleetTuningConstants tuning)
        {
            if (!TryResolveLaunchDroneChassisSpec(s_DroneTaskKindsBySlot[slot], in tuning, out DroneChassisSpecDTO chassis))
            {
                FailCloseDroneForUnavailableChassis(ref drone);
                return;
            }

            float holdSeconds = Mathf.Max(0.01f, chassis.MiningHoldSeconds);
            drone.RepairAccumulator = Mathf.Min(holdSeconds, drone.RepairAccumulator + Mathf.Max(0f, dt));
            drone.TransactionProgress = Mathf.Clamp01(drone.RepairAccumulator / holdSeconds);
            if (drone.RepairAccumulator < holdSeconds)
                return;

            int sourceId = drone.TargetModuleId != 0
                ? drone.TargetModuleId
                : unchecked((int)math.hash(drone.TargetPosition));
            int depositedQuantity;
            if (!TryCommitDroneMiningOutputToHub(
                    in drone,
                    unchecked((uint)DroneInventoryCopperHash),
                    1,
                    out depositedQuantity,
                    out byte commitFailureReason))
            {
                RecordDroneMiningCommitFailed(slot, commitFailureReason);
                drone.RepairAccumulator = holdSeconds;
                drone.TransactionProgress = 1f;
                return;
            }

            if (depositedQuantity > 0)
            {
                PublishDroneMiningItemAcquiredSignal(in drone, unchecked((uint)DroneInventoryCopperHash), depositedQuantity, sourceId);
                DroneFleetInventoryTransactionSignal signal = new DroneFleetInventoryTransactionSignal
                {
                    DroneId = drone.DroneId,
                    SourceId = sourceId,
                    DestinationId = drone.HubGridId,
                    ItemHash = DroneInventoryCopperHash,
                    Quantity = depositedQuantity,
                    Position = drone.Position,
                    Flags = 2u,
                    Reserved0 = 0u
                };
                if (!SignalBus<DroneFleetInventoryTransactionSignal>.TryPushTracked(in signal, ref s_SignalPushDropCount))
                    RecordInventoryTransactionSignalRejected();

                InventoryCommandSignal inventoryCommand = new InventoryCommandSignal
                {
                    InventoryHash = (uint)Mathf.Max(0, drone.HubGridId),
                    Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                    Sequence = (uint)Mathf.Max(0, drone.DroneId),
                    Command = InventoryCommandSignalCommands.Sort,
                    Flags = 2
                };
                if (!SignalBus<InventoryCommandSignal>.TryPushTracked(in inventoryCommand, ref s_SignalPushDropCount))
                    RecordInventoryCommandSignalRejected();
            }

            drone.RepairAccumulator = 0f;
            drone.TransactionProgress = 1f;
            ClearDroneMiningCommitFailureLatch(slot);
            ReturnDroneToHub(ref drone);
        }

        private static bool TryCommitDroneMiningOutputToHub(
            in HeadlessDroneState drone,
            uint itemHash,
            int requestedQuantity,
            out int depositedQuantity,
            out byte failureReason)
        {
            depositedQuantity = 0;
            failureReason = DroneMiningCommitFailureNone;
            if (itemHash == 0u || requestedQuantity <= 0)
            {
                failureReason = DroneMiningCommitFailureInvalidPayload;
                return false;
            }

            if (!TryResolveDroneMiningItem(itemHash, out ItemData item, out failureReason))
                return false;

            if (!TryResolveDroneHubByKey(drone.HubGridId, out RepairDroneHub hub, out failureReason))
                return false;

            if (!hub.HasOperationalPower)
            {
                failureReason = hub.CurrentGrid == null
                    ? DroneMiningCommitFailureGridMissing
                    : DroneMiningCommitFailureHubUnpowered;
                return false;
            }

            PowerGrid grid = hub.CurrentGrid;
            if (grid == null)
            {
                failureReason = DroneMiningCommitFailureGridMissing;
                return false;
            }

            int safeQuantity = Mathf.Max(1, requestedQuantity);
            if (!BaseLogisticsNetwork.TryDepositItem(grid, item, safeQuantity, out int routedQuantity))
            {
                failureReason = DroneMiningCommitFailureStorageFull;
                return false;
            }

            depositedQuantity = Mathf.Clamp(routedQuantity, 0, safeQuantity);
            if (depositedQuantity <= 0)
            {
                failureReason = DroneMiningCommitFailureStorageFull;
                return false;
            }

            return true;
        }

        private static bool TryResolveDroneMiningItem(uint itemHash, out ItemData item, out byte failureReason)
        {
            item = null;
            failureReason = DroneMiningCommitFailureNone;
            IPlayerInventoryService inventoryService = s_CachedPlayerInventoryService;
            PlayerInventory inventory = inventoryService != null && inventoryService.IsInitialized
                ? inventoryService.Inventory
                : null;
            ItemCatalog catalog = inventory != null ? inventory.ItemCatalog : null;
            if (catalog == null)
            {
                failureReason = DroneMiningCommitFailureItemCatalogUnavailable;
                return false;
            }

            item = catalog.FindByHash(unchecked((int)itemHash));
            if (item == null)
            {
                failureReason = DroneMiningCommitFailureItemMissing;
                return false;
            }

            return true;
        }

        private static bool TryResolveDroneHubByKey(int hubKey, out RepairDroneHub hub, out byte failureReason)
        {
            hub = null;
            failureReason = DroneMiningCommitFailureNone;
            if (hubKey == 0)
            {
                failureReason = DroneMiningCommitFailureHubMissing;
                return false;
            }

            int scanHubCount = RepairDroneHub.ActiveHubCount;
            for (int i = 0; i < scanHubCount; i++)
            {
                RepairDroneHub candidate = RepairDroneHub.GetActiveHubAt(i);
                if (candidate == null || !candidate.isActiveAndEnabled)
                    continue;

                if (ResolveHubTaskKey(candidate) != hubKey)
                    continue;

                if (hub != null)
                {
                    hub = null;
                    failureReason = DroneMiningCommitFailureDuplicateHub;
                    return false;
                }

                hub = candidate;
            }

            if (hub != null)
                return true;

            failureReason = DroneMiningCommitFailureHubMissing;
            return false;
        }

        private static void ApplyParasiteAttackService(int slot, ref HeadlessDroneState drone, float dt)
        {
            BaseModule target = s_TargetModulesByDroneSlot[slot];
            if (target == null || target.ParasiteInfectionLevel <= 0.0001f)
            {
                ReturnDroneToHub(ref drone);
                return;
            }

            if (drone.SolderUnits <= 0)
            {
                RouteDroneToSupplyOrStasis(slot, ref drone);
                return;
            }

            FloraInteractionManager floraInteractionManager = s_CachedFloraInteractionManager;
            if (floraInteractionManager == null)
            {
                ReturnDroneToHub(ref drone);
                return;
            }

            Vector3 hitPoint = ToVector3(drone.TargetPosition);
            Vector3 dronePosition = ToVector3(drone.Position);
            Vector3 direction = hitPoint - dronePosition;
            float directionDistanceSq = direction.sqrMagnitude;
            float deliveredDamage = Mathf.Max(0.1f, drone.RepairRatePerSecond * dt);
            floraInteractionManager.TryApplyDroneParasiteCut(
                hitPoint,
                directionDistanceSq > SeparationDistanceEpsilon ? direction * math.rsqrt(directionDistanceSq) : Vector3.down,
                deliveredDamage,
                drone.WeldPowerNormalized);

            ConsumeSolderByWork(ref drone, deliveredDamage, SolderIntegrityUnitsPerBundle);
        }

        private static void ApplyHostileHijackService(int slot, ref HeadlessDroneState drone, float dt)
        {
            BaseModule target = s_TargetModulesByDroneSlot[slot];
            if (target == null)
            {
                if (TryResolvePlayerPosition(out Vector3 playerPosition) &&
                    TryResolvePlayerAup(out double3 playerAup))
                {
                    drone.TargetPosition = (float3)(playerPosition);
                    drone.TargetAup = playerAup;
                }
                return;
            }

            float damage = Mathf.Max(0.1f, drone.RepairRatePerSecond * dt);
            target.ApplyDamage(damage);
            DispatchPlasmaCut(slot, in drone, target);
            drone.State = (byte)HeadlessDroneRuntimeState.Attack;
        }

        private static void PublishHullRepairedByDrone(int slot, in HeadlessDroneState drone, BaseModule target, float repairUnits)
        {
            if (target == null || repairUnits <= 0f)
                return;

            if (!TryResolveRepairHitAup(in drone, target, out AbsoluteUniversePosition hitAup))
                return;

            HullRepairedSignal signal = new HullRepairedSignal
            {
                HitAup = hitAup,
                RoomId = 0,
                SourceHash = ComputeDroneTaskHash(s_DroneTaskKindsBySlot[slot], drone.DroneId, GetRuntimeId(target)),
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                DentIndex = 0,
                DentsRepairedCount = 1,
                QualityTier = ResolveDroneRepairQualityWeightByte(),
                Flags = HullRepairedSignal.CompletedFlag
            };
            SignalBus<HullRepairedSignal>.TryPushTracked(in signal, ref s_SignalPushDropCount);
        }

        private static byte ResolveDroneRepairQualityWeightByte()
        {
            float quality = ResolveDroneSimulationQualityWeight();
            return (byte)math.clamp((int)math.round(quality * 255f), 0, 255);
        }

        private static bool TryResolveRepairHitAup(in HeadlessDroneState drone, BaseModule target, out AbsoluteUniversePosition hitAup)
        {
            hitAup = default;
            if (IsFiniteDouble3(drone.TargetAup))
            {
                hitAup = AbsoluteUniversePosition.FromAbsolutePosition(drone.TargetAup);
                return hitAup.IsFinite();
            }

            if (target == null)
                return false;

            Vector3 targetPosition = target.transform.position;
            if (!IsFiniteVector(targetPosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            hitAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(targetPosition.x, targetPosition.y, targetPosition.z));
            return hitAup.IsFinite();
        }

        private static void ExecuteSacrifice(int slot, ref HeadlessDroneState drone, BaseModule target)
        {
            float recoverableIntegrity = Mathf.Max(1f, target.MaxRecoverableIntegrity);
            float requestedRepair = Mathf.Max(0f, recoverableIntegrity - target.CurrentIntegrity);
            if (requestedRepair > 0f || target.IsFlooded)
                PublishHullRepairedByDrone(slot, in drone, target, Mathf.Max(1f, requestedRepair));

            s_FleetSacrificeRequested = false;
            s_DroneSlotDestroyed[slot] = true;
            s_DestroyedDroneCount++;
            drone.State = (byte)HeadlessDroneRuntimeState.Sacrificed;
            drone.Velocity = float3.zero;
        }

        private static bool IsSacrificeEligible(BaseModule module)
        {
            if (module == null)
                return false;

            return module.IsBreached || module.FloodLevel01 >= 0.8f || (module.IsFlooded && module.FloodLevel01 <= 0.001f);
        }

        private static void ConsumeSolderByWork(ref HeadlessDroneState drone, float workAmount, float unitsPerSolder)
        {
            if (workAmount <= 0f || drone.SolderUnits <= 0)
                return;

            drone.RepairAccumulator += workAmount;
            float safeUnitsPerSolder = Mathf.Max(1f, unitsPerSolder);
            float safeUnitsPerSolderInv = math.rcp(safeUnitsPerSolder);
            int consumedUnits = Mathf.Min(
                drone.SolderUnits,
                Mathf.FloorToInt(drone.RepairAccumulator * safeUnitsPerSolderInv));
            if (consumedUnits <= 0)
                return;

            drone.RepairAccumulator -= safeUnitsPerSolder * consumedUnits;
            drone.SolderUnits -= consumedUnits;
        }

        private static void RouteDroneToSupplyOrStasis(int slot, ref HeadlessDroneState drone)
        {
            RepairDroneHub hub = s_DroneHubs[slot];
            if (hub != null && hub.TryResolveNearestSupplyEndpoint(ToVector3(drone.Position), out Vector3 endpointPosition))
            {
                if (!TryResolveAupDoubleFromRuntimeOrigin(endpointPosition, out double3 endpointAup))
                    return;

                drone.SupplyPosition = (float3)(endpointPosition);
                drone.SupplyAup = endpointAup;
                drone.State = (byte)HeadlessDroneRuntimeState.ResupplyTravel;
                return;
            }

            drone.State = (byte)HeadlessDroneRuntimeState.Stasis;
            drone.Velocity = float3.zero;
        }

        private static void ReturnDroneToHub(ref HeadlessDroneState drone)
        {
            drone.TargetTaskIndex = EmptyTaskIndex;
            drone.TargetPosition = drone.HomePosition;
            drone.TargetAup = drone.HomeAup;
            drone.DockingElapsed = 0f;
            drone.DockingFlags = 0;
            drone.DockingPathLengthMeters = 0f;
            drone.State = (byte)HeadlessDroneRuntimeState.Return;
        }

        private static void FailCloseDroneForUnavailableChassis(ref HeadlessDroneState drone)
        {
            drone.TargetTaskIndex = EmptyTaskIndex;
            drone.TargetModuleId = 0;
            drone.TargetPosition = drone.HomePosition;
            drone.TargetAup = drone.HomeAup;
            drone.Velocity = float3.zero;
            drone.RepairAccumulator = 0f;
            drone.TransactionProgress = 0f;
            drone.State = (byte)HeadlessDroneRuntimeState.Stasis;
            PublishDroneChassisFailClosedSignal(1);
        }

        private static void DispatchRepairWeld(int slot, in HeadlessDroneState drone, BaseModule target)
        {
            HectonVoxelVolume volume = s_TargetVoxelVolumesByDroneSlot[slot];
            if (volume == null || target == null)
                return;

            if (!IsFiniteDouble3(drone.PositionAup) ||
                !TryResolveDroneTargetAup(in drone, target, out double3 targetAup))
            {
                return;
            }

            double3 weldDeltaDouble = targetAup - drone.PositionAup;
            float3 weldDirectionLocal = (float3)(weldDeltaDouble);
            float weldDistanceSq = math.lengthsq(weldDirectionLocal);
            if (weldDistanceSq <= SeparationDistanceEpsilon)
                return;

            float3 normalizedWeldLocal = weldDirectionLocal * math.rsqrt(weldDistanceSq);
            Vector3 normalizedWeldDirection = ToVector3(normalizedWeldLocal);
            double3 absoluteHitPoint = drone.PositionAup + (new double3(normalizedWeldLocal.x, normalizedWeldLocal.y, normalizedWeldLocal.z) * 0.35d);
            volume.ApplyRepairWeldDda(
                absoluteHitPoint,
                normalizedWeldDirection,
                drone.WeldPowerNormalized,
                drone.WeldRangeMeters);
            PublishDroneRepairSparks(absoluteHitPoint, drone.DroneId, drone.WeldPowerNormalized);
        }

        private static void DispatchPlasmaCut(int slot, in HeadlessDroneState drone, BaseModule target)
        {
            HectonVoxelVolume volume = s_TargetVoxelVolumesByDroneSlot[slot];
            if (volume == null || target == null)
                return;

            if (!IsFiniteDouble3(drone.PositionAup) ||
                !TryResolveDroneTargetAup(in drone, target, out double3 targetAup))
            {
                return;
            }

            double3 cutDeltaDouble = targetAup - drone.PositionAup;
            float3 cutDirectionLocal = (float3)(cutDeltaDouble);
            float cutDistanceSq = math.lengthsq(cutDirectionLocal);
            if (cutDistanceSq <= SeparationDistanceEpsilon)
                return;

            float3 normalizedCutLocal = cutDirectionLocal * math.rsqrt(cutDistanceSq);
            Vector3 normalizedCutDirection = ToVector3(normalizedCutLocal);
            double3 absoluteHitPoint = drone.PositionAup + (new double3(normalizedCutLocal.x, normalizedCutLocal.y, normalizedCutLocal.z) * 0.35d);
            volume.ApplyPlasmaCutDda(
                absoluteHitPoint,
                normalizedCutDirection,
                drone.WeldPowerNormalized,
                drone.WeldRangeMeters);
        }

        private static void PublishDroneRepairSparks(double3 absoluteHitPoint, int droneId, float intensity01)
        {
            float safeIntensity = Mathf.Clamp01(intensity01);
            AbsoluteUniversePosition hitAup = AbsoluteUniversePosition.FromAbsolutePosition(absoluteHitPoint);
            DebrisSpawnSignal signal = new DebrisSpawnSignal
            {
                PositionAup = hitAup,
                SpeciesHash = DroneRepairSparksSignalHash,
                SourceEntityId = (uint)Mathf.Max(0, droneId),
                Intensity01 = safeIntensity,
                DebrisKind = DebrisSpawnSignal.DebrisKindSparks,
                Flags = DebrisSpawnSignal.FlagToolSparks | DebrisSpawnSignal.FlagComputeShard
            };
            SignalBus<DebrisSpawnSignal>.TryPushTracked(in signal, ref s_SignalPushDropCount);

            if (!TryResolveRuntimeFloat3AupDelta(in hitAup, out float3 hitRuntime))
                return;

            Hecton8.Tools.ToolKinematics.Contracts.VfxSparkRequestSignal spark = new Hecton8.Tools.ToolKinematics.Contracts.VfxSparkRequestSignal
            {
                HitPoint = hitRuntime,
                Normal = new float3(0f, 1f, 0f),
                MaterialHash = DroneRepairSparksSignalHash,
                ToolHash = DroneRepairSparksSignalHash,
                Intensity01 = safeIntensity,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId
            };
            SignalBus<Hecton8.Tools.ToolKinematics.Contracts.VfxSparkRequestSignal>.TryPushTracked(in spark, ref s_SignalPushDropCount);
            PublishDroneRepairTorchAcoustic(ToVector3(hitRuntime), safeIntensity);
        }

        private static void PublishDroneRepairTorchAcoustic(Vector3 runtimePosition, float intensity01)
        {
            AudioClip clip = s_RepairTorchAcousticClip;
            if (clip == null)
                return;

            float safeIntensity = Mathf.Clamp01(intensity01);
            RepairDroneTorchAcousticEvent acousticEvent = new RepairDroneTorchAcousticEvent(
                runtimePosition,
                clip,
                Mathf.Clamp01(s_RepairTorchAcousticVolume * Mathf.Lerp(0.6f, 1f, safeIntensity)),
                s_RepairTorchAcousticPitch);
            if (!RepairDroneTorchAcousticEvents.TryNotify(in acousticEvent) && s_SignalPushDropCount < int.MaxValue)
                s_SignalPushDropCount++;
        }

        private static void ApplyPendingLaunches(
            NativeArray<HeadlessDroneState> droneStates,
            NativeArray<HeadlessDroneState> droneStateBackBuffer,
            NativeArray<float4x4> droneRenderMatrices,
            NativeArray<float4x4> droneRenderMatrixBackBuffer,
            NativeArray<float3> positionsSoA,
            NativeArray<byte> stateBytes,
            NativeArray<DroneStateDTO> stateDtos,
            NativeArray<DroneTargetDTO> targetDtos,
            in DroneFleetTuningConstants tuning)
        {
            if (s_PendingLaunchCount <= 0)
                return;

            for (int i = 0; i < s_PendingLaunchCount; i++)
            {
                PendingDroneLaunch launch = s_PendingLaunches[i];
                s_PendingLaunches[i] = default;
                if (launch.Active == 0)
                    continue;

                int slot = launch.DroneSlot;
                if (slot < 0 ||
                    slot >= HeadlessDroneCapacity ||
                    s_DroneSlotDroneIds[slot] != launch.DroneId ||
                    s_PendingReleaseBySlot[slot] ||
                    s_DroneSlotDestroyed[slot])
                {
                    ClearHeadlessSlot(slot, true, droneStates, droneStateBackBuffer, droneRenderMatrices, droneRenderMatrixBackBuffer, positionsSoA, stateBytes, stateDtos, targetDtos);
                    continue;
                }

                BaseModule target = launch.Task.Module;
                HectonVoxelVolume targetVolume = TryResolveTargetVoxelVolume(target);
                s_DroneHubs[slot] = launch.Hub;
                s_TargetModulesByDroneSlot[slot] = target;
                s_TargetVoxelVolumesByDroneSlot[slot] = targetVolume;
                s_DroneTaskKindsBySlot[slot] = launch.Task.Kind;
                s_DronePositions[slot] = launch.HomePosition;
                quaternion homeRotation = ToQuaternion(launch.HomeRotation);
                if (!TryResolveAupDoubleFromRuntimeOrigin(launch.HomePosition, out double3 homeAup) ||
                    !TryResolveAupDoubleFromRuntimeOrigin(launch.Task.Position, out double3 targetAup))
                {
                    ClearHeadlessSlot(slot, true, droneStates, droneStateBackBuffer, droneRenderMatrices, droneRenderMatrixBackBuffer, positionsSoA, stateBytes, stateDtos, targetDtos);
                    continue;
                }

                uint launchTaskHash = ComputeDroneTaskHash(launch.Task.Kind, launch.DroneId, GetRuntimeId(target));
                if (!TryResolveLaunchDroneChassisSpec(launch.Task.Kind, in tuning, out DroneChassisSpecDTO chassis))
                {
                    PublishDroneChassisFailClosedSignal(1);
                    ClearHeadlessSlot(slot, true, droneStates, droneStateBackBuffer, droneRenderMatrices, droneRenderMatrixBackBuffer, positionsSoA, stateBytes, stateDtos, targetDtos);
                    continue;
                }

                int tunedCargoCapacity = Mathf.Max(1, Mathf.RoundToInt(chassis.CargoCapacity));

                HeadlessDroneState state = new HeadlessDroneState
                {
                    DroneId = launch.DroneId,
                    HubGridId = ResolveHubTaskKey(launch.Hub),
                    HubSlot = slot,
                    TargetTaskIndex = launch.DroneId,
                    TargetModuleId = GetRuntimeId(target),
                    SolderUnits = Mathf.Max(0, launch.LoadedSolderUnits),
                    LoadedSolderCapacity = Mathf.Max(tunedCargoCapacity, launch.LoadedSolderUnits),
                    State = (byte)HeadlessDroneRuntimeState.Travel,
                    FactionBit = (byte)HeadlessDroneFactionBit.Friendly,
                    CorridorTight = ResolveCorridorFlag(launch.HomePosition),
                    BatteryPercent = chassis.BatteryCapacity,
                    RepairAccumulator = 0f,
                    DockingElapsed = 0f,
                    RebootElapsed = 0f,
                    AvoidanceHysteresisSeconds = 0f,
                    TransactionProgress = 0f,
                    ServiceRadius = Mathf.Max(HeadlessServiceRadiusMeters, launch.Task.Radius),
                    MaxSpeed = chassis.MaxSpeed,
                    BatteryDrainPerSecond = chassis.BatteryDrainRate,
                    RepairRatePerSecond = Mathf.Max(0.01f, launch.RepairRatePerSecond * chassis.RepairSpeed),
                    WeldPowerNormalized = HeadlessWeldPowerNormalized,
                    WeldRangeMeters = HeadlessWeldRangeMeters,
                    Position = (float3)(launch.HomePosition),
                    Velocity = float3.zero,
                    HomePosition = (float3)(launch.HomePosition),
                    TargetPosition = (float3)(launch.Task.Position),
                    SupplyPosition = (float3)(launch.HomePosition),
                    DockStartPosition = (float3)(launch.HomePosition),
                    Rotation = homeRotation,
                    HomeRotation = homeRotation,
                    DockStartRotation = homeRotation,
                    DockingPathLengthMeters = 0f,
                    DockingRequestId = 0u,
                    DockingFlags = 0,
                    DockControlP0 = global::Hecton8.World.AUPMath.ToDouble3(launch.HomePosition),
                    DockControlP1 = global::Hecton8.World.AUPMath.ToDouble3(launch.HomePosition),
                    DockControlP2 = global::Hecton8.World.AUPMath.ToDouble3(launch.HomePosition),
                    DockControlP3 = global::Hecton8.World.AUPMath.ToDouble3(launch.HomePosition),
                    PositionAup = homeAup,
                    HomeAup = homeAup,
                    TargetAup = targetAup,
                    SupplyAup = homeAup,
                    ReservedTail0 = math.asuint(chassis.ClearanceRadiusMeters)
                };
                droneStates[slot] = state;
                droneStateBackBuffer[slot] = state;
                if (stateDtos.IsCreated && (uint)slot < (uint)stateDtos.Length)
                {
                    stateDtos[slot] = new DroneStateDTO
                    {
                        CurrentAUP = homeAup,
                        Velocity = float3.zero,
                        CurrentTargetHashID = launchTaskHash,
                        TaskStateFlags = ((uint)state.State) | ((uint)state.FactionBit << 8) | ((uint)state.CorridorTight << 16),
                        BatteryLevel = chassis.BatteryCapacity,
                    };
                }

                if (targetDtos.IsCreated && (uint)slot < (uint)targetDtos.Length)
                {
                    targetDtos[slot] = new DroneTargetDTO
                    {
                        TargetAUP = targetAup,
                        LocalPosition = state.TargetPosition,
                        TaskHash = launchTaskHash,
                        TaskIndex = state.TargetTaskIndex,
                        TargetModuleId = state.TargetModuleId,
                        Radius = state.ServiceRadius,
                        TaskKind = (uint)launch.Task.Kind,
                        Flags = 1u,
                        Reserved0 = 0u
                    };
                }

                droneRenderMatrices[slot] = float4x4.TRS(state.Position, state.Rotation, new float3(1f, 1f, 1f));
                droneRenderMatrixBackBuffer[slot] = droneRenderMatrices[slot];
                MirrorDroneSoA(slot, in state, positionsSoA, stateBytes, stateDtos, targetDtos);
            }

            s_PendingLaunchCount = 0;
        }

        private static void SyncManagedTaskReference(int slot, ref HeadlessDroneState drone)
        {
            int taskIndex = drone.TargetTaskIndex;
            if (taskIndex < 0 || taskIndex >= s_HeadlessTaskCount || taskIndex >= s_TaskModuleRefs.Length)
                return;

            s_DroneTaskKindsBySlot[slot] = s_TaskKinds[taskIndex];
            BaseModule module = s_TaskModuleRefs[taskIndex];
            if (module == null)
            {
                s_TargetModulesByDroneSlot[slot] = null;
                s_TargetVoxelVolumesByDroneSlot[slot] = null;
                drone.TargetModuleId = 0;
                return;
            }

            s_TargetModulesByDroneSlot[slot] = module;
            s_TargetVoxelVolumesByDroneSlot[slot] = s_TaskVoxelVolumeRefs[taskIndex];
            drone.TargetModuleId = GetRuntimeId(module);
        }

        private static void ClearAllHeadlessSlots()
        {
            if (!TryAcquireDroneCoreMirrorMutationViews(
                    out NativeArray<HeadlessDroneState> droneStates,
                    out NativeArray<HeadlessDroneState> droneStateBackBuffer,
                    out NativeArray<float4x4> droneRenderMatrices,
                    out NativeArray<float4x4> droneRenderMatrixBackBuffer,
                    out NativeArray<float3> positionsSoA,
                    out NativeArray<byte> stateBytes,
                    out NativeArray<DroneStateDTO> stateDtos,
                    out NativeArray<DroneTargetDTO> targetDtos,
                    out IDataVault coreMirrorVault))
            {
                return;
            }

            try
            {
                for (int slot = 0; slot < HeadlessDroneCapacity; slot++)
                    ClearHeadlessSlot(slot, false, droneStates, droneStateBackBuffer, droneRenderMatrices, droneRenderMatrixBackBuffer, positionsSoA, stateBytes, stateDtos, targetDtos);
            }
            finally
            {
                ReleaseDroneMutationGuard(coreMirrorVault, DroneCoreMirrorMutationGuardMask);
            }
        }

        private static void ClearHeadlessSlot(int slot, bool notifyHub)
        {
            if (!TryAcquireDroneCoreMirrorMutationViews(
                    out NativeArray<HeadlessDroneState> droneStates,
                    out NativeArray<HeadlessDroneState> droneStateBackBuffer,
                    out NativeArray<float4x4> droneRenderMatrices,
                    out NativeArray<float4x4> droneRenderMatrixBackBuffer,
                    out NativeArray<float3> positionsSoA,
                    out NativeArray<byte> stateBytes,
                    out NativeArray<DroneStateDTO> stateDtos,
                    out NativeArray<DroneTargetDTO> targetDtos,
                    out IDataVault coreMirrorVault))
            {
                return;
            }

            try
            {
                ClearHeadlessSlot(slot, notifyHub, droneStates, droneStateBackBuffer, droneRenderMatrices, droneRenderMatrixBackBuffer, positionsSoA, stateBytes, stateDtos, targetDtos);
            }
            finally
            {
                ReleaseDroneMutationGuard(coreMirrorVault, DroneCoreMirrorMutationGuardMask);
            }
        }

        private static void ClearHeadlessSlot(
            int slot,
            bool notifyHub,
            NativeArray<HeadlessDroneState> droneStates,
            NativeArray<HeadlessDroneState> droneStateBackBuffer,
            NativeArray<float4x4> droneRenderMatrices,
            NativeArray<float4x4> droneRenderMatrixBackBuffer,
            NativeArray<float3> positionsSoA,
            NativeArray<byte> stateBytes,
            NativeArray<DroneStateDTO> stateDtos,
            NativeArray<DroneTargetDTO> targetDtos)
        {
            if (slot < 0 || slot >= HeadlessDroneCapacity || s_DroneSlotDroneIds == null)
                return;

            int droneId = s_DroneSlotDroneIds[slot];
            RepairDroneHub hub = s_DroneHubs[slot];
            s_DroneSlotDroneIds[slot] = 0;
            s_DroneHubs[slot] = null;
            s_TargetModulesByDroneSlot[slot] = null;
            s_TargetVoxelVolumesByDroneSlot[slot] = null;
            s_DroneTaskKindsBySlot[slot] = DroneFleetTaskKind.None;
            s_DronePositions[slot] = Vector3.zero;
            HeadlessDroneState clearedState = default;
            droneStates[slot] = clearedState;
            MirrorDroneSoA(slot, in clearedState, positionsSoA, stateBytes, stateDtos, targetDtos);
            if (droneStateBackBuffer.IsCreated)
                droneStateBackBuffer[slot] = default;
            droneRenderMatrices[slot] = float4x4.zero;
            if (droneRenderMatrixBackBuffer.IsCreated)
                droneRenderMatrixBackBuffer[slot] = float4x4.zero;
            if (stateDtos.IsCreated && (uint)slot < (uint)stateDtos.Length)
                stateDtos[slot] = default;
            if (targetDtos.IsCreated && (uint)slot < (uint)targetDtos.Length)
                targetDtos[slot] = default;
            s_PendingAbortBySlot[slot] = false;
            s_PendingReleaseBySlot[slot] = false;
            s_PendingHostileBySlot[slot] = false;
            s_PendingResupplyGrantBySlot[slot] = false;
            s_PendingResupplyFailureBySlot[slot] = false;
            s_PendingResupplyReservationIdsBySlot[slot] = 0;
            ClearDroneMiningCommitFailureLatch(slot);

            if (notifyHub && hub != null && droneId > 0)
                hub.NotifyHeadlessDroneReturned(droneId);
        }

        private static int FindFreeHeadlessSlot()
        {
            for (int i = 0; i < MaxOperationalDroneCount; i++)
            {
                if (s_DroneSlotDestroyed[i])
                    continue;

                if (s_DroneSlotDroneIds[i] <= 0)
                    return i;
            }

            return -1;
        }

        private static void MirrorDroneSoA(
            int slot,
            in HeadlessDroneState drone,
            NativeArray<float3> positionsSoA,
            NativeArray<byte> stateBytes,
            NativeArray<DroneStateDTO> stateDtos,
            NativeArray<DroneTargetDTO> targetDtos)
        {
            if (slot < 0 || slot >= HeadlessDroneCapacity)
                return;

            if (positionsSoA.IsCreated && (uint)slot < (uint)positionsSoA.Length)
                positionsSoA[slot] = drone.Position;

            if (stateBytes.IsCreated && (uint)slot < (uint)stateBytes.Length)
            {
                stateBytes[slot] = s_DroneTaskKindsBySlot != null &&
                    s_DroneTaskKindsBySlot[slot] == DroneFleetTaskKind.MineNode &&
                    drone.State == (byte)HeadlessDroneRuntimeState.Repair
                    ? (byte)DroneFleetSoaState.Mining
                    : ResolveDroneSoAState(in drone);
            }

            DroneFleetTaskKind kind = s_DroneTaskKindsBySlot != null ? s_DroneTaskKindsBySlot[slot] : DroneFleetTaskKind.None;
            uint taskHash = ResolveTransactionTaskHash(kind);
            if (stateDtos.IsCreated && (uint)slot < (uint)stateDtos.Length)
            {
                stateDtos[slot] = new DroneStateDTO
                {
                    CurrentAUP = drone.PositionAup,
                    Velocity = drone.Velocity,
                    CurrentTargetHashID = taskHash,
                    TaskStateFlags = ((uint)drone.State) | ((uint)drone.FactionBit << 8) | ((uint)drone.CorridorTight << 16),
                    BatteryLevel = drone.BatteryPercent
                };
            }

            if (targetDtos.IsCreated && (uint)slot < (uint)targetDtos.Length)
            {
                targetDtos[slot] = new DroneTargetDTO
                {
                    TargetAUP = drone.TargetAup,
                    LocalPosition = drone.TargetPosition,
                    TaskHash = taskHash,
                    TaskIndex = drone.TargetTaskIndex,
                    TargetModuleId = drone.TargetModuleId,
                    Radius = drone.ServiceRadius,
                    TaskKind = (uint)kind,
                    Flags = drone.State == (byte)HeadlessDroneRuntimeState.Empty ? 0u : 1u,
                    Reserved0 = 0u
                };
            }
        }

        private static uint ResolveTransactionTaskHash(DroneFleetTaskKind kind)
        {
            if (kind == DroneFleetTaskKind.MineNode)
                return DroneMiningTaskTypeHash;

            if (kind == DroneFleetTaskKind.RepairModule)
                return DroneRepairTaskTypeHash;

            return 0u;
        }

        private static byte ResolveDroneSoAState(in HeadlessDroneState drone)
        {
            if (drone.State == (byte)HeadlessDroneRuntimeState.Repair ||
                drone.State == (byte)HeadlessDroneRuntimeState.Attack)
            {
                return (byte)DroneFleetSoaState.Repairing;
            }

            if (drone.State == (byte)HeadlessDroneRuntimeState.Return ||
                drone.State == (byte)HeadlessDroneRuntimeState.Docking ||
                drone.State == (byte)HeadlessDroneRuntimeState.ResupplyTravel ||
                drone.State == (byte)HeadlessDroneRuntimeState.ResupplyDocked ||
                drone.State == (byte)HeadlessDroneRuntimeState.ResupplyCommitPending)
            {
                return (byte)DroneFleetSoaState.Returning;
            }

            return (byte)DroneFleetSoaState.Idle;
        }

        private static int ResolveHeadlessSlot(int droneId)
        {
            if (droneId <= 0 || s_DroneSlotDroneIds == null)
                return -1;

            for (int i = 0; i < s_DroneSlotDroneIds.Length; i++)
            {
                if (s_DroneSlotDroneIds[i] == droneId)
                    return i;
            }

            return -1;
        }

        private static int CountManagedHeadlessDrones()
        {
            if (s_DroneSlotDroneIds == null)
                return 0;

            int count = 0;
            for (int i = 0; i < s_DroneSlotDroneIds.Length; i++)
            {
                if (s_DroneSlotDroneIds[i] > 0 && !s_PendingReleaseBySlot[i])
                    count++;
            }

            return count;
        }

        private static void RefreshHeadlessCounters(NativeArray<HeadlessDroneState> droneStates)
        {
            s_HeadlessStasisSlotCount = 0;
            if (s_DroneSlotDroneIds == null)
                return;

            for (int i = 0; i < s_DroneSlotDroneIds.Length; i++)
            {
                if (s_DroneSlotDroneIds[i] <= 0)
                    continue;

                HeadlessDroneState drone = droneStates[i];
                if (drone.State == (byte)HeadlessDroneRuntimeState.Stasis)
                    s_HeadlessStasisSlotCount++;

                s_DronePositions[i] = ToVector3(drone.Position);
            }
        }

        private static void RefreshFleetStatusSnapshotFromDroneStates(NativeArray<HeadlessDroneState> droneStates)
        {
            if (s_DroneSlotDroneIds == null ||
                !droneStates.IsCreated)
            {
                return;
            }

            int limit = math.min(HeadlessDroneCapacity, math.min(s_DroneSlotDroneIds.Length, droneStates.Length));
            int activeCount = 0;
            int batteryMilliPercent = 0;
            int solderReserve = 0;
            int hostileCount = 0;
            for (int slot = 0; slot < limit; slot++)
            {
                if (s_DroneSlotDroneIds[slot] <= 0)
                    continue;

                HeadlessDroneState drone = droneStates[slot];
                if (drone.State == (byte)HeadlessDroneRuntimeState.Empty ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Sacrificed ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Completed)
                {
                    continue;
                }

                activeCount++;
                batteryMilliPercent += (int)math.round(math.clamp(drone.BatteryPercent, 0f, 100f) * 1000f);
                solderReserve += math.max(0, drone.SolderUnits);
                if (drone.FactionBit == (byte)HeadlessDroneFactionBit.Hostile)
                    hostileCount++;
            }

            float averageBattery = activeCount > 0
                ? Mathf.Clamp(batteryMilliPercent * math.rcp(activeCount * 1000f), 0f, 100f)
                : 0f;
            s_LastFleetStatusSnapshot = new FleetStatusSnapshot(
                activeCount,
                averageBattery,
                solderReserve,
                s_DestroyedDroneCount,
                hostileCount);
        }

        private static void BuildHeadlessTaskMap(
            float deltaTime,
            NativeArray<DroneAssignmentTaskDTO> assignmentTasks)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Announced ahead of the rebuild-timer and moduleCount guards on purpose: both can return for the
            // whole session, and an advisory placed next to the SignalBus<DroneFleetMiningServiceSignal> read
            // below would then be dead code itself. See the dead-lane block at that read site.
            if (!s_UnpublishedMiningServiceLaneWarned)
            {
                s_UnpublishedMiningServiceLaneWarned = true;
                H8Debug.LogWarning(
                    "[DroneFleetManager] DEAD SIGNAL LANE: BuildHeadlessTaskMap drains SignalBus<DroneFleetMiningServiceSignal>, but nothing in Assets/_Project/Scripts ever constructs or pushes that signal - the type occurs only at this drain, at its struct declaration (Construction/DroneFleetNavigationKernel.cs:934), and in the EnsureDockingSignalLanes Configure/EnsureInitialized pair. The two sibling lanes configured in that same block ARE published by this very file (DroneFleetRepairServiceSignal from ApplyFriendlyRepairService, DroneFleetInventoryTransactionSignal from ApplyMiningService and DroneFleetManager_Transactions.cs:1302), so the asymmetry is a missing producer rather than a design choice. Consequence: AppendMiningServiceTasksForHub always returns on an empty span, so DroneFleetTaskKind.MineNode is never written - AppendHeadlessMiningServiceTask is its ONLY assignment site, and the managed launch path (TryAssignFleetTask) can only produce RepairModule or CutParasite. No drone slot ever reaches MineNode, ApplyMiningService and PrepareMiningTransaction (DroneFleetManager_Transactions.cs:995) never execute, and NO DRONE EVER MINES OR DEPOSITS ORE. Repair and CutParasite tasking are unaffected. Do NOT paper over this by publishing a synthetic signal: which system owns mining work orders (resource-node discovery vs. hub scan) is an owner decision. Note also that the drain sits after the moduleCount == 0 early return, so even with a producer, mining tasks would additionally require at least one spawned base module.");
            }
#endif
            s_HeadlessTaskRebuildTimer -= Mathf.Max(0f, deltaTime);
            if (s_HeadlessTaskRebuildTimer > 0f && s_HeadlessTaskCount > 0)
                return;

            s_HeadlessTaskRebuildTimer = ResolveDroneTaskRebuildIntervalSeconds();
            s_HeadlessTaskCount = 0;
            ClearManagedTaskRefs(assignmentTasks);

            ILogisticsService manager = s_CachedLogisticsService;
            int moduleCount = manager != null ? manager.SpawnedBaseModuleCount : 0;
            if (moduleCount == 0)
                return;

            int hubCount = Mathf.Min(RepairDroneHub.ActiveHubCount, MaxMainThreadHubScanCount);
            FloraInteractionManager floraInteractionManager = s_CachedFloraInteractionManager;
            // DEAD LANE - THIS SNAPSHOT HAS ALWAYS BEEN EMPTY. SignalBus<DroneFleetMiningServiceSignal> has no
            // producer anywhere in Assets/_Project/Scripts. Verified producers-of set is empty:
            //   - no "new DroneFleetMiningServiceSignal" and no SignalBus<DroneFleetMiningServiceSignal>.Push/
            //     TryPush/TryPushTracked exists in the tree; the type's only other occurrences are the struct
            //     declaration (Construction/DroneFleetNavigationKernel.cs:934) and the Configure/
            //     EnsureInitialized pair in EnsureDockingSignalLanes above.
            //   - the lane is NOT registered in Core/Signals/GlobalSignals.RuntimeLifecycle.cs like the other
            //     first-party lanes; it is configured locally here with a hand-written laneHash 0x44524D4E.
            //   - both siblings configured beside it ARE pushed by this assembly: DroneFleetRepairServiceSignal
            //     in ApplyFriendlyRepairService, DroneFleetInventoryTransactionSignal in ApplyMiningService and
            //     in DroneFleetManager_Transactions.cs:1302. Only the mining work-order lane was never wired.
            // This lane is the SOLE input to the fleet's mining capability, so the whole capability is inert:
            // AppendHeadlessMiningServiceTask is the only site that ever assigns DroneFleetTaskKind.MineNode,
            // and it is reachable only from this span. The managed launch path cannot substitute - the only
            // RepairTaskCandidate.Kind literals in TryAssignFleetTask are RepairModule and CutParasite, so
            // launch.Task.Kind in ApplyPendingLaunches can never be MineNode either. Net effect: ApplyMiningService,
            // PrepareMiningTransaction (DroneFleetManager_Transactions.cs:995), the MineNode chassis spec with
            // MiningHoldSeconds, the copper deposit path and the mining commit-failure latch are all fully
            // implemented and completely unreachable. Nothing logs it; the drain below just early-returns.
            // Read is GetFrameSnapshot (non-destructive), so a future producer can be added alongside this
            // reader without stealing signals from another consumer. Adding that producer is an owner decision.
            System.ReadOnlySpan<DroneFleetMiningServiceSignal> miningSignals = SignalBus<DroneFleetMiningServiceSignal>.GetFrameSnapshot();
            int remainingModuleScans = MaxMainThreadTaskScanCount;
            for (int hubIndex = 0; hubIndex < hubCount; hubIndex++)
            {
                RepairDroneHub hub = RepairDroneHub.GetActiveHubAt(hubIndex);
                if (hub == null || !hub.isActiveAndEnabled)
                    continue;

                int hubKey = ResolveHubTaskKey(hub);
                PowerGrid hubGrid = hub.CurrentGrid;
                Vector3 hubPosition = hub.DockPosition;
                for (int moduleIndex = 0; moduleIndex < moduleCount && remainingModuleScans > 0 && s_HeadlessTaskCount < HeadlessTaskCapacity; moduleIndex++, remainingModuleScans--)
                {
                    BaseModule module = manager.GetSpawnedBaseModuleAt(moduleIndex);
                    if (module == null || !module.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    if (IsEligibleRepairTarget(hubGrid, module, 0.98f))
                    {
                        AppendHeadlessTask(
                            assignmentTasks,
                            hubKey,
                            DroneFleetTaskKind.RepairModule,
                            module,
                            module.transform.position,
                            0f,
                            ResolveCriticalityWeight(module));
                    }

                    if (floraInteractionManager == null ||
                        module.ParasiteInfectionLevel <= 0.0001f ||
                        IsDifferentGrid(hubGrid, module) ||
                        !floraInteractionManager.TryResolveNearestModuleParasite(module, hubPosition, out FloraInteractionManager.ModuleParasiteTarget parasiteTarget))
                    {
                        continue;
                    }

                    AppendHeadlessTask(
                        assignmentTasks,
                        hubKey,
                        DroneFleetTaskKind.CutParasite,
                        module,
                        parasiteTarget.Position,
                        parasiteTarget.Radius,
                        ResolveParasiteCriticalityWeight(module, in parasiteTarget));
                }

                if (remainingModuleScans <= 0)
                    break;

                AppendMiningServiceTasksForHub(assignmentTasks, hubIndex, hubCount, hubKey, miningSignals);
            }
        }

        private static void AppendMiningServiceTasksForHub(
            NativeArray<DroneAssignmentTaskDTO> assignmentTasks,
            int hubIndex,
            int hubCount,
            int hubKey,
            System.ReadOnlySpan<DroneFleetMiningServiceSignal> miningSignals)
        {
            if (miningSignals.Length <= 0)
                return;

            for (int i = 0; i < miningSignals.Length && s_HeadlessTaskCount < HeadlessTaskCapacity; i++)
            {
                DroneFleetMiningServiceSignal signal = miningSignals[i];
                if (!IsFiniteFloat3(signal.Position) ||
                    ResolveNearestHubIndex(signal.Position, hubCount) != hubIndex)
                {
                    continue;
                }

                AppendHeadlessMiningServiceTask(assignmentTasks, hubKey, in signal);
            }
        }

        private static int ResolveNearestHubIndex(float3 position, int hubCount)
        {
            int bestIndex = -1;
            float bestDistanceSq = float.MaxValue;
            Vector3 targetPosition = ToVector3(position);
            for (int i = 0; i < hubCount; i++)
            {
                RepairDroneHub hub = RepairDroneHub.GetActiveHubAt(i);
                if (hub == null || !hub.isActiveAndEnabled)
                    continue;

                float distanceSq = (hub.DockPosition - targetPosition).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestIndex = i;
            }

            return bestIndex;
        }

        private static void AppendHeadlessTask(
            NativeArray<DroneAssignmentTaskDTO> assignmentTasks,
            int hubKey,
            DroneFleetTaskKind kind,
            BaseModule module,
            Vector3 position,
            float radius,
            float criticalityWeight)
        {
            int taskIndex = s_HeadlessTaskCount;
            if (taskIndex < 0 || taskIndex >= HeadlessTaskCapacity || module == null)
                return;

            s_TaskModuleRefs[taskIndex] = module;
            s_TaskVoxelVolumeRefs[taskIndex] = TryResolveTargetVoxelVolume(module);
            s_TaskKinds[taskIndex] = kind;
            if (assignmentTasks.IsCreated && taskIndex < assignmentTasks.Length)
            {
                if (!TryResolveAupDoubleFromRuntimeOrigin(position, out double3 targetAup))
                    return;

                assignmentTasks[taskIndex] = new DroneAssignmentTaskDTO
                {
                    TargetAup = targetAup,
                    LocalPosition = (float3)(position),
                    Priority = 1f,
                    Score = 0f,
                    CriticalityWeight = Mathf.Max(0.1f, criticalityWeight),
                    Radius = Mathf.Max(HeadlessServiceRadiusMeters, radius),
                    ModuleIndex = taskIndex,
                    TaskKind = (int)kind,
                    Reserved0 = (uint)Mathf.Max(0, hubKey)
                };
            }
            s_HeadlessTaskCount++;
        }

        private static void AppendHeadlessMiningServiceTask(
            NativeArray<DroneAssignmentTaskDTO> assignmentTasks,
            int hubKey,
            in DroneFleetMiningServiceSignal signal)
        {
            int taskIndex = s_HeadlessTaskCount;
            if (taskIndex < 0 || taskIndex >= HeadlessTaskCapacity)
                return;

            s_TaskModuleRefs[taskIndex] = null;
            s_TaskVoxelVolumeRefs[taskIndex] = null;
            s_TaskKinds[taskIndex] = DroneFleetTaskKind.MineNode;
            if (assignmentTasks.IsCreated && taskIndex < assignmentTasks.Length)
            {
                Vector3 targetPosition = ToVector3(signal.Position);
                if (!TryResolveAupDoubleFromRuntimeOrigin(targetPosition, out double3 targetAup))
                    return;

                assignmentTasks[taskIndex] = new DroneAssignmentTaskDTO
                {
                    TargetAup = targetAup,
                    LocalPosition = signal.Position,
                    Priority = 0.25f,
                    Score = 0f,
                    CriticalityWeight = 0.1f,
                    Radius = HeadlessServiceRadiusMeters,
                    ModuleIndex = taskIndex,
                    TaskKind = (int)DroneFleetTaskKind.MineNode,
                    Reserved0 = (uint)Mathf.Max(0, hubKey)
                };
            }
            s_HeadlessTaskCount++;
        }

        private static void BuildHeadlessSpatialHash(
            NativeArray<HeadlessDroneState> droneStates,
            NativeArray<int> spatialBucketHeads,
            NativeArray<int> spatialNextIndices,
            NativeArray<int> spatialKeys)
        {
            if (!spatialBucketHeads.IsCreated ||
                !spatialNextIndices.IsCreated ||
                !spatialKeys.IsCreated)
            {
                return;
            }

            for (int i = 0; i < spatialBucketHeads.Length; i++)
                spatialBucketHeads[i] = -1;

            for (int i = 0; i < HeadlessDroneCapacity; i++)
            {
                if (s_DroneSlotDroneIds[i] <= 0)
                    continue;

                HeadlessDroneState drone = droneStates[i];
                if (drone.State == (byte)HeadlessDroneRuntimeState.Empty ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Sacrificed ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Completed)
                {
                    continue;
                }

                drone.CorridorTight = ResolveCorridorFlag(ToVector3(drone.Position));
                droneStates[i] = drone;
                int key = DroneCognitionJob.PackSpatialKey(drone.Position);
                int bucket = ResolveDroneSpatialBucket(key);
                spatialKeys[i] = key;
                spatialNextIndices[i] = spatialBucketHeads[bucket];
                spatialBucketHeads[bucket] = i;
            }
        }

        private static int ResolveDroneSpatialBucket(int key)
        {
            uint hash = (uint)key;
            hash ^= hash >> 16;
            hash *= 0x7feb352du;
            hash ^= hash >> 15;
            return (int)(hash & (uint)(DroneSpatialBucketCapacity - 1));
        }

        private static void ClearHeadlessTaskClaims(
            NativeArray<HeadlessDroneState> droneStates,
            NativeArray<int> taskClaimOwners)
        {
            if (!taskClaimOwners.IsCreated)
                return;

            for (int i = 0; i < taskClaimOwners.Length; i++)
                taskClaimOwners[i] = 0;

            for (int slot = 0; slot < HeadlessDroneCapacity; slot++)
            {
                if (s_DroneSlotDroneIds[slot] <= 0)
                    continue;

                HeadlessDroneState drone = droneStates[slot];
                int taskIndex = drone.TargetTaskIndex;
                if (taskIndex < 0 || taskIndex >= s_HeadlessTaskCount || taskIndex >= taskClaimOwners.Length)
                    continue;

                if (taskClaimOwners[taskIndex] == 0)
                    taskClaimOwners[taskIndex] = drone.DroneId;
            }
        }

        private static void ClearFleetTelemetryAccumulator(NativeArray<int> telemetryAccumulator)
        {
            if (!telemetryAccumulator.IsCreated)
                return;

            for (int i = 0; i < telemetryAccumulator.Length; i++)
                telemetryAccumulator[i] = 0;

            if (telemetryAccumulator.Length > (int)DroneFleetTelemetryAccumulatorSlot.LostToHijack)
                telemetryAccumulator[(int)DroneFleetTelemetryAccumulatorSlot.LostToHijack] = s_LogicLeechHijackCount;
        }

        private static void PublishFleetTelemetryIfDue()
        {
            if (!TryReadDroneVaultBuffer(
                    s_CachedDataVault,
                    in s_FleetTelemetryAccumulatorHandle,
                    BufferID.ShinobuDroneFleetTelemetryAccumulator,
                    (int)DroneFleetTelemetryAccumulatorSlot.Count,
                    out NativeArray<int>.ReadOnly telemetryAccumulator))
            {
                return;
            }

            s_FleetTelemetryFrameCounter++;
            if (s_FleetTelemetryFrameCounter < FleetTelemetryPublishFrameInterval)
                return;

            s_FleetTelemetryFrameCounter = 0;
            int activeCount = telemetryAccumulator[(int)DroneFleetTelemetryAccumulatorSlot.ActiveCount];
            int batteryMilliPercent = telemetryAccumulator[(int)DroneFleetTelemetryAccumulatorSlot.BatteryMilliPercent];
            float averageBattery = activeCount > 0
                ? Mathf.Clamp(batteryMilliPercent * math.rcp(activeCount * 1000f), 0f, 100f)
                : 0f;
            FleetStatusSnapshot snapshot = new FleetStatusSnapshot(
                activeCount,
                averageBattery,
                telemetryAccumulator[(int)DroneFleetTelemetryAccumulatorSlot.SolderReserve],
                s_DestroyedDroneCount,
                telemetryAccumulator[(int)DroneFleetTelemetryAccumulatorSlot.HostileCount]);

            s_LastFleetStatusSnapshot = snapshot;
            GlobalTelemetryBus.PublishDroneFleetStatus(
                snapshot.TotalActive,
                snapshot.AverageBattery,
                snapshot.SolderReserve,
                snapshot.LostUnits,
                snapshot.HostileUnits);
            PublishDominantAxisDroneTelemetryIfPresent();
            TryRelayLeviathanPing();
            PublishSnapshot();
        }

        private static void ClearManagedTaskRefs(NativeArray<DroneAssignmentTaskDTO> assignmentTasks)
        {
            for (int i = 0; i < s_TaskModuleRefs.Length; i++)
            {
                s_TaskModuleRefs[i] = null;
                s_TaskVoxelVolumeRefs[i] = null;
                s_TaskKinds[i] = DroneFleetTaskKind.None;
                if (assignmentTasks.IsCreated && i < assignmentTasks.Length)
                    assignmentTasks[i] = default;
            }
        }

        private static int ResolveHubTaskKey(RepairDroneHub hub)
        {
            return GetRuntimeId(hub);
        }

        private static bool TryResolvePlayerPosition(out Vector3 position)
        {
            position = Vector3.zero;
            IPlayerRuntimeContext playerContext = s_CachedPlayerRuntime;
            if (playerContext == null)
                return false;

            if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                (snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                math.all(math.isfinite(snapshot.RuntimePosition)) &&
                snapshot.Aup.IsFinite())
            {
                position = new Vector3(snapshot.RuntimePosition.x, snapshot.RuntimePosition.y, snapshot.RuntimePosition.z);
                return true;
            }

            if (!playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) ||
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u ||
                !movementState.PredictedAup.IsFinite() ||
                !math.all(math.isfinite(movementState.WorldPosition)))
            {
                return false;
            }

            position = new Vector3(
                movementState.WorldPosition.x,
                movementState.WorldPosition.y,
                movementState.WorldPosition.z);
            return true;
        }

        private static bool TryResolveFormationAnchor(out Vector3 position)
        {
            position = Vector3.zero;
            ISubmarineRuntimeContext submarine = s_CachedSubmarineRuntime;
            Transform platformTransform = submarine != null ? submarine.PlatformTransform : null;
            if (platformTransform == null)
                return false;

            position = platformTransform.position;
            return true;
        }

        private static void TryRelayLeviathanPing()
        {
            if (s_DroneSlotDroneIds == null ||
                !TryReadDroneStates(out NativeArray<HeadlessDroneState>.ReadOnly droneStates) ||
                !TryResolveFormationAnchor(out Vector3 submarinePosition))
            {
                return;
            }

            float relayDistanceSq = DroneRelaySubmarineDistanceMeters * DroneRelaySubmarineDistanceMeters;
            for (int slot = 0; slot < s_DroneSlotDroneIds.Length; slot++)
            {
                if (s_DroneSlotDroneIds[slot] <= 0)
                    continue;

                HeadlessDroneState drone = droneStates[slot];
                if (drone.State == (byte)HeadlessDroneRuntimeState.Empty ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Sacrificed ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Completed ||
                    drone.FactionBit == (byte)HeadlessDroneFactionBit.Hostile)
                {
                    continue;
                }

                Vector3 dronePosition = ToVector3(drone.Position);
                if ((dronePosition - submarinePosition).sqrMagnitude <= relayDistanceSq)
                    continue;

                if (!TryResolveRelayLeviathan(dronePosition, out SpatialQueryHit hit))
                    continue;

                PhysicsEventPayload payload = new PhysicsEventPayload
                {
                    RuntimePosition = hit.Position,
                    Direction = default,
                    ForceVector = default,
                    ImpulseVector = default,
                    RadiusMeters = DroneRelayPingRadiusMeters,
                    Scalar0 = 1f,
                    Scalar1 = DroneRelayPingLifetimeSeconds,
                    Scalar2 = DroneRelayPingRadiusMeters * 48f,
                    PrimaryId = hit.SpeciesId,
                    DataHash = 0u,
                    StatusBits = unchecked((uint)FieldTargetRole.BioformAggressive),
                    EventType = (ushort)PhysicsEventType.AcousticPing,
                    Reserved = 0
                };
                SignalBus<PhysicsEventPayload>.TryPushTracked(in payload, ref s_SignalPushDropCount);
                return;
            }
        }

        private static void PublishDominantAxisDroneTelemetryIfPresent()
        {
            if (s_DroneSlotDroneIds == null ||
                !TryReadDroneStates(out NativeArray<HeadlessDroneState>.ReadOnly droneStates) ||
                !TryResolveFormationAnchor(out Vector3 anchorPosition))
            {
                return;
            }

            float3 anchor = (float3)(anchorPosition);
            float quality = ResolveGlobalQualityWeight();
            float precisionWeight = quality * quality * (3f - (2f * quality));
            for (int slot = 0; slot < s_DroneSlotDroneIds.Length; slot++)
            {
                int droneId = s_DroneSlotDroneIds[slot];
                if (droneId <= 0)
                    continue;

                HeadlessDroneState drone = droneStates[slot];
                if (drone.State == (byte)HeadlessDroneRuntimeState.Empty ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Sacrificed ||
                    drone.State == (byte)HeadlessDroneRuntimeState.Completed)
                {
                    continue;
                }

                float3 droneAnchorDelta = drone.Position - anchor;
                float exactDistanceSq = math.lengthsq(droneAnchorDelta);
                float dominantAxisSq = DominantAxisMagnitudeSq(droneAnchorDelta);
                float distanceMetricSq = math.lerp(dominantAxisSq, exactDistanceSq, precisionWeight);
                GlobalTelemetryBus.PublishDominantAxisTelemetry(
                    unchecked((uint)droneId),
                    distanceMetricSq,
                    precisionWeight < 0.999f);
            }
        }

        private static float DominantAxisMagnitudeSq(float3 value)
        {
            if (!math.all(math.isfinite(value)))
                return 0f;

            float3 absValue = math.abs(value);
            float dominantMagnitude = math.cmax(absValue);
            return dominantMagnitude * dominantMagnitude;
        }

        private static bool TryResolveRelayLeviathan(Vector3 dronePosition, out SpatialQueryHit hit)
        {
            hit = default;
            int count = WorldSpatialHashGrid.CollectContactsNonAlloc(
                dronePosition,
                DroneRelayScanRadiusMeters,
                SpatialTargetKind.Bioform,
                s_DroneRelayContacts);

            float bestDistanceSq = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                SpatialQueryHit candidate = s_DroneRelayContacts[i];
                if (!(candidate.Owner is IFaunaSpatialContact faunaContact) ||
                    !faunaContact.IsLeviathanContact)
                {
                    continue;
                }

                Vector3 targetPosition = candidate.Position;
                Vector3 delta = targetPosition - dronePosition;
                float distanceSq = delta.sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                hit = candidate;
            }

            return bestDistanceSq < float.MaxValue;
        }

        private static bool TryResolveAbyssalFlowVolumePayload(
            out NativeArray<float3>.ReadOnly flowVolume,
            out Vector3 center,
            out int resolutionXZ,
            out int resolutionY,
            out int ringOffsetX,
            out int ringOffsetY,
            out int ringOffsetZ,
            out float horizontalCellSize,
            out float verticalCellSize,
            out float surfaceY,
            out float depthMeters)
        {
            HectonMapMagicVegetationBridge bridge = s_CachedVegetationBridge;
            if (bridge != null &&
                bridge.TryGetAbyssalFlowVolumePayload(
                    out flowVolume,
                    out center,
                    out resolutionXZ,
                    out resolutionY,
                    out ringOffsetX,
                    out ringOffsetY,
                    out ringOffsetZ,
                    out horizontalCellSize,
                    out verticalCellSize,
                    out surfaceY,
                    out depthMeters) &&
                math.isfinite(surfaceY) &&
                math.abs(surfaceY) <= 1000f &&
                math.isfinite(depthMeters) &&
                depthMeters > 0f &&
                horizontalCellSize > 0f &&
                verticalCellSize > 0f)
            {
                return true;
            }

            flowVolume = default;
            center = Vector3.zero;
            resolutionXZ = 0;
            resolutionY = 0;
            ringOffsetX = 0;
            ringOffsetY = 0;
            ringOffsetZ = 0;
            horizontalCellSize = 0f;
            verticalCellSize = 0f;
            surfaceY = 0f;
            depthMeters = 0f;
            return false;
        }

        private static void ResolveFluidCurrentSnapshot(
            out Vector3 baseFlowVelocity,
            out bool phantomFlowEnabled,
            out float phantomFlowNoiseScale,
            out float phantomFlowTimeScale,
            out float phantomFlowStrength,
            out float phantomFlowVerticalFactor)
        {
            IFluidSurfaceCurrentReadModel fluidSurface = s_CachedFluidRuntime;
            if (fluidSurface == null)
            {
                baseFlowVelocity = Vector3.zero;
                phantomFlowEnabled = false;
                phantomFlowNoiseScale = 0f;
                phantomFlowTimeScale = 0f;
                phantomFlowStrength = 0f;
                phantomFlowVerticalFactor = 0f;
                return;
            }

            baseFlowVelocity = fluidSurface.CurrentVector * Mathf.Max(0f, fluidSurface.CurrentStrength);
            phantomFlowEnabled = fluidSurface.EnablePhantomCurrent;
            phantomFlowNoiseScale = Mathf.Max(0f, fluidSurface.CurrentNoiseScale);
            phantomFlowTimeScale = Mathf.Max(0f, fluidSurface.CurrentTimeScale);
            phantomFlowStrength = Mathf.Max(0f, fluidSurface.PhantomCurrentStrength);
            phantomFlowVerticalFactor = Mathf.Max(0f, fluidSurface.CurrentVerticalFactor);
        }

        private static byte ResolveCorridorFlag(Vector3 position)
        {
            return VoxelDynamicNavGridRuntime.TrySampleHybridNavigation(position, out VoxelDynamicNavGridRuntime.HybridNavigationSample sample) &&
                   sample.Mode == VoxelDynamicNavGridRuntime.HybridNavigationMode.CaveVoxel
                ? (byte)1
                : (byte)0;
        }

        private static HectonVoxelVolume TryResolveTargetVoxelVolume(BaseModule target)
        {
            return target != null ? target.CachedVoxelVolume : null;
        }

        private static void EnsureRenderBuffers()
        {
            EnsureDroneShaderPropertyIds();
            EnsureDroneProceduralMaterial();
            if (s_DroneProceduralMaterial == null)
                return;

            if (s_DroneMatrixBuffer == null)
                s_DroneMatrixBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4x4>(HeadlessDroneCapacity); // COLD ALLOC: GraphicsBuffer[512] - real headless drone matrix upload buffer - owner: DroneFleetManager

            if (s_DroneMatrixBufferBackBuffer == null)
                s_DroneMatrixBufferBackBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4x4>(HeadlessDroneCapacity); // COLD ALLOC: GraphicsBuffer[512] - alternate real drone matrix upload buffer for GPU/CPU double-buffering - owner: DroneFleetManager

            if (s_DroneStateGpuBuffer == null)
                s_DroneStateGpuBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<DroneCullingStateGpu>(HeadlessDroneCapacity); // COLD ALLOC: GraphicsBuffer[512] - compact real drone culling upload buffer for GPU culling - owner: DroneFleetManager

            if (s_DroneRenderInstanceBuffer == null)
                s_DroneRenderInstanceBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<DroneRenderInstance>(HeadlessDroneCapacity); // COLD ALLOC: GraphicsBuffer[512] - real drone render instance upload buffer for VAT transaction parameters - owner: DroneFleetManager

            if (s_DroneVisibleMatrixBuffer == null)
                s_DroneVisibleMatrixBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, HeadlessDroneCapacity, UnsafeUtility.SizeOf<float4x4>()); // COLD ALLOC: GraphicsBuffer[512] - GPU-compacted visible real drone matrices - owner: DroneFleetManager

            if (s_DroneVisibleInstanceBuffer == null)
                s_DroneVisibleInstanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, HeadlessDroneCapacity, UnsafeUtility.SizeOf<DroneRenderInstance>()); // COLD ALLOC: GraphicsBuffer[512] - GPU-compacted visible real drone VAT instance data - owner: DroneFleetManager

            if (s_DroneVisibleIndexBuffer == null)
                s_DroneVisibleIndexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Append, HeadlessDroneCapacity, sizeof(int)); // COLD ALLOC: GraphicsBuffer[512] - visible real drone index append buffer for shader indirection/debug - owner: DroneFleetManager

            if (s_DroneProceduralArgsBuffer == null)
                s_DroneProceduralArgsBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.CopyDestination,
                    1,
                    UnsafeUtility.SizeOf<DroneProceduralIndirectArgsDTO>()); // COLD ALLOC: GraphicsBuffer[1] - headless drone procedural indirect draw arguments with mapped CPU upload - owner: DroneFleetManager
            if (s_DroneProceduralArgsUploadBuffer == null)
                s_DroneProceduralArgsUploadBuffer = GraphicsBufferUploadUtility.CreateRawIndirectUploadStagingBuffer(
                    1,
                    UnsafeUtility.SizeOf<DroneProceduralIndirectArgsDTO>()); // COLD ALLOC: GraphicsBuffer[1] - CPU-visible drone procedural args staging, GPU copy source only - owner: DroneFleetManager

            EnsureDroneDefaultColorBuffer();
            ResolveDroneCullingKernels();
        }

        private static void EnsureDroneProceduralMaterial()
        {
            if (s_DroneProceduralMaterial != null)
                return;

            if (!RuntimeShaderReferenceCatalog.TryGetDroneFleetProceduralMaterial(out Material material) || material == null)
                return;

            s_DroneProceduralMaterial = material;
            s_DroneProceduralMaterialRuntimeOwned = false;
        }

        private static void EnsureDroneDefaultColorBuffer()
        {
            if (s_DroneDefaultColorBuffer != null)
                return;

            s_DroneDefaultColorBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(1); // COLD ALLOC: GraphicsBuffer[1] - default white procedural color binding for real drones - owner: DroneFleetManager
            NativeArray<float4> mappedColor = s_DroneDefaultColorBuffer.LockBufferForWrite<float4>(0, 1);
            try
            {
                mappedColor[0] = new float4(1f, 1f, 1f, 1f);
            }
            finally
            {
                s_DroneDefaultColorBuffer.UnlockBufferAfterWrite<float4>(1);
            }
        }

        private static void ReleaseRenderBuffers()
        {
            if (s_DroneMatrixBuffer != null)
            {
                s_DroneMatrixBuffer.Release();
                s_DroneMatrixBuffer = null;
            }

            if (s_DroneMatrixBufferBackBuffer != null)
            {
                s_DroneMatrixBufferBackBuffer.Release();
                s_DroneMatrixBufferBackBuffer = null;
            }

            if (s_DroneStateGpuBuffer != null)
            {
                s_DroneStateGpuBuffer.Release();
                s_DroneStateGpuBuffer = null;
            }

            if (s_DroneRenderInstanceBuffer != null)
            {
                s_DroneRenderInstanceBuffer.Release();
                s_DroneRenderInstanceBuffer = null;
            }

            if (s_DroneVisibleMatrixBuffer != null)
            {
                s_DroneVisibleMatrixBuffer.Release();
                s_DroneVisibleMatrixBuffer = null;
            }

            if (s_DroneVisibleInstanceBuffer != null)
            {
                s_DroneVisibleInstanceBuffer.Release();
                s_DroneVisibleInstanceBuffer = null;
            }

            if (s_DroneVisibleIndexBuffer != null)
            {
                s_DroneVisibleIndexBuffer.Release();
                s_DroneVisibleIndexBuffer = null;
            }

            if (s_DroneProceduralArgsBuffer != null)
            {
                s_DroneProceduralArgsBuffer.Release();
                s_DroneProceduralArgsBuffer = null;
            }

            if (s_DroneProceduralArgsUploadBuffer != null)
            {
                s_DroneProceduralArgsUploadBuffer.Release();
                s_DroneProceduralArgsUploadBuffer = null;
            }

            if (s_DroneDefaultColorBuffer != null)
            {
                s_DroneDefaultColorBuffer.Release();
                s_DroneDefaultColorBuffer = null;
            }

            if (s_DroneProceduralMaterialRuntimeOwned && s_DroneProceduralMaterial != null)
                DestroyRuntimeObject(s_DroneProceduralMaterial);

            s_DroneProceduralMaterial = null;
            s_DroneProceduralMaterialRuntimeOwned = false;
        }

        private static bool EnsurePhantomRenderResources()
        {
            ResolvePhantomDroneKernel();
            if (s_PhantomDronesCompute == null || !s_PhantomDroneKernelResolved)
                return false;

            EnsureDroneProceduralMaterial();
            if (s_DroneProceduralMaterial == null)
                return false;

            if (s_PhantomDroneMatrixBuffer == null)
                s_PhantomDroneMatrixBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, PhantomDroneCount, UnsafeUtility.SizeOf<float4x4>()); // COLD ALLOC: GraphicsBuffer[500] - GPU-authored phantom drone matrices - owner: DroneFleetManager

            if (s_PhantomDroneColorBuffer == null)
                s_PhantomDroneColorBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, PhantomDroneCount, UnsafeUtility.SizeOf<float4>()); // COLD ALLOC: GraphicsBuffer[500] - GPU-authored phantom drone emissive colors - owner: DroneFleetManager

            if (s_PhantomDroneArgsBuffer == null)
            {
                s_PhantomDroneArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, UnsafeUtility.SizeOf<DroneProceduralIndirectArgsDTO>()); // COLD ALLOC: GraphicsBuffer[1] - phantom drone procedural indirect draw arguments - owner: DroneFleetManager
                s_PhantomDroneLastDrawCount = -1;
            }

            return true;
        }

        private static void ReleasePhantomRenderResources()
        {
            if (s_PhantomDroneMatrixBuffer != null)
            {
                s_PhantomDroneMatrixBuffer.Release();
                s_PhantomDroneMatrixBuffer = null;
            }

            if (s_PhantomDroneColorBuffer != null)
            {
                s_PhantomDroneColorBuffer.Release();
                s_PhantomDroneColorBuffer = null;
            }

            if (s_PhantomDroneArgsBuffer != null)
            {
                s_PhantomDroneArgsBuffer.Release();
                s_PhantomDroneArgsBuffer = null;
            }

            s_PhantomDroneLastDrawCount = -1;
        }

        private static int ResolvePhantomDroneDrawCount()
        {
            float quality = ResolveGlobalQualityWeight();
            return Mathf.Clamp(Mathf.RoundToInt(math.lerp(SurvivalPhantomDroneCount, PhantomDroneCount, quality)), 0, PhantomDroneCount);
        }

        private static int ResolveKernelThreadGroupSizeX(ComputeShader compute, int kernel)
        {
            if (compute == null || kernel < 0)
                return 0;

            uint sizeX;
            uint sizeY;
            uint sizeZ;
            try
            {
                if (!compute.IsSupported(kernel))
                    return 0;

                compute.GetKernelThreadGroupSizes(kernel, out sizeX, out sizeY, out sizeZ);
            }
            catch (System.ObjectDisposedException)
            {
                return 0;
            }
            catch (System.InvalidOperationException)
            {
                return 0;
            }
            catch (System.ArgumentException)
            {
                return 0;
            }
            catch (UnityEngine.MissingReferenceException)
            {
                return 0;
            }
            catch (UnityEngine.UnityException)
            {
                return 0;
            }
            if (sizeX == 0u || sizeY != 1u || sizeZ != 1u || sizeX > int.MaxValue)
                return 0;

            ulong totalThreads = sizeX * (ulong)sizeY * sizeZ;
            return totalThreads <= PortableMaxComputeThreadsPerGroup ? (int)sizeX : 0;
        }

        private static int CeilDividePositive(int value, int divisor)
        {
            const int MaxDispatchGroupsPerDimension = 65535;
            if (value <= 0 || divisor <= 0)
                return 0;

            long groups = ((long)value + divisor - 1L) / divisor;
            return groups <= MaxDispatchGroupsPerDimension ? (int)groups : 0;
        }

        private static void UpdatePhantomDroneArgs(int phantomDrawCount)
        {
            if (s_PhantomDroneArgsBuffer == null)
                return;

            phantomDrawCount = Mathf.Clamp(phantomDrawCount, 0, PhantomDroneCount);
            if (s_PhantomDroneLastDrawCount == phantomDrawCount)
                return;

            NativeArray<DroneProceduralIndirectArgsDTO> mappedArgs = s_PhantomDroneArgsBuffer.LockBufferForWrite<DroneProceduralIndirectArgsDTO>(0, 1);
            try
            {
                mappedArgs[0] = new DroneProceduralIndirectArgsDTO
                {
                    VertexCountPerInstance = DroneProceduralVerticesPerInstance,
                    InstanceCount = (uint)phantomDrawCount,
                    StartVertex = 0u,
                    StartInstance = 0u
                };
            }
            finally
            {
                s_PhantomDroneArgsBuffer.UnlockBufferAfterWrite<DroneProceduralIndirectArgsDTO>(1);
            }
            s_PhantomDroneLastDrawCount = phantomDrawCount;
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(target);
                return;
            }
#endif

            UnityEngine.Object.Destroy(target);
        }

        private static void ResolveDroneCullingKernels()
        {
            if (s_DroneCullingKernelsResolved)
                return;

            s_DroneCullKernel = -1;
            s_DroneClearArgsKernel = -1;
            s_DroneCullThreadGroupSizeX = 0;

#if UNITY_EDITOR
            if (s_DroneCullingCompute == null)
                s_DroneCullingCompute = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(DroneCullingComputeAssetPath);
#endif

            if (s_DroneCullingCompute == null)
                return;

            if (!TryFindKernel(s_DroneCullingCompute, "CS_ClearArgs", out s_DroneClearArgsKernel) &&
                !TryFindKernel(s_DroneCullingCompute, "ClearIndirectArgs", out s_DroneClearArgsKernel))
                s_DroneClearArgsKernel = -1;

            if (!TryFindKernel(s_DroneCullingCompute, "CS_CullDrones", out s_DroneCullKernel) &&
                !TryFindKernel(s_DroneCullingCompute, "CullDrones", out s_DroneCullKernel))
                s_DroneCullKernel = -1;

            if (s_DroneCullKernel < 0 ||
                s_DroneClearArgsKernel < 0 ||
                !IsKernelSupported(s_DroneCullingCompute, s_DroneCullKernel) ||
                !IsKernelSupported(s_DroneCullingCompute, s_DroneClearArgsKernel))
            {
                s_DroneCullKernel = -1;
                s_DroneClearArgsKernel = -1;
                return;
            }

            s_DroneCullThreadGroupSizeX = ResolveKernelThreadGroupSizeX(s_DroneCullingCompute, s_DroneCullKernel);
            s_DroneCullingKernelsResolved = s_DroneCullThreadGroupSizeX > 0;
        }

        private static void ResolvePhantomDroneKernel()
        {
            if (s_PhantomDroneKernelResolved)
                return;

            s_PhantomDroneKernel = -1;
            s_PhantomDroneThreadGroupSizeX = 0;

#if UNITY_EDITOR
            if (s_PhantomDronesCompute == null)
                s_PhantomDronesCompute = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(PhantomDronesComputeAssetPath);
#endif

            if (s_PhantomDronesCompute == null)
                return;

            if (!TryFindKernel(s_PhantomDronesCompute, "CS_UpdatePhantomDrones", out s_PhantomDroneKernel) &&
                !TryFindKernel(s_PhantomDronesCompute, "UpdatePhantomDrones", out s_PhantomDroneKernel))
                s_PhantomDroneKernel = -1;

            if (s_PhantomDroneKernel < 0 || !IsKernelSupported(s_PhantomDronesCompute, s_PhantomDroneKernel))
            {
                s_PhantomDroneKernel = -1;
                s_PhantomDroneThreadGroupSizeX = 0;
                s_PhantomDroneKernelResolved = false;
                return;
            }

            s_PhantomDroneThreadGroupSizeX = ResolveKernelThreadGroupSizeX(s_PhantomDronesCompute, s_PhantomDroneKernel);
            s_PhantomDroneKernelResolved = s_PhantomDroneThreadGroupSizeX > 0;
        }

        private static bool IsKernelSupported(ComputeShader compute, int kernel)
        {
            if (compute == null || kernel < 0)
                return false;

            try
            {
                return compute.IsSupported(kernel);
            }
            catch (System.ObjectDisposedException)
            {
                return false;
            }
            catch (System.InvalidOperationException)
            {
                return false;
            }
            catch (System.ArgumentException)
            {
                return false;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
            catch (UnityException)
            {
                return false;
            }
        }

        private static bool TryFindKernel(ComputeShader compute, string kernelName, out int kernel)
        {
            kernel = -1;
            if (compute == null)
                return false;

            try
            {
                if (!compute.HasKernel(kernelName))
                    return false;

                kernel = compute.FindKernel(kernelName);
                return kernel >= 0;
            }
            catch (System.ObjectDisposedException)
            {
                kernel = -1;
                return false;
            }
            catch (System.InvalidOperationException)
            {
                kernel = -1;
                return false;
            }
            catch (System.ArgumentException)
            {
                kernel = -1;
                return false;
            }
            catch (MissingReferenceException)
            {
                kernel = -1;
                return false;
            }
            catch (UnityException)
            {
                kernel = -1;
                return false;
            }
        }

        private static void RenderHeadlessFleet(float deltaTime)
        {
            RenderRealHeadlessFleet();
            RenderPhantomSwarm(deltaTime);
        }

        private static void RenderRealHeadlessFleet()
        {
            if (CountManagedHeadlessDrones() <= 0)
                return;

            EnsureRenderBuffers();
            int frame = SystemDispatcher.CurrentFrameIndex;
            int uploadModulo = ResolveDroneVisualMatrixUploadModulo();
            bool uploadMatrices = s_LastDroneMatrixUploadFrame < 0 ||
                                  uploadModulo <= 1 ||
                                  frame - s_LastDroneMatrixUploadFrame >= uploadModulo;
            GraphicsBuffer matrixBuffer = uploadMatrices
                ? (s_DroneMatrixUploadBufferIndex == 0 ? s_DroneMatrixBuffer : s_DroneMatrixBufferBackBuffer)
                : (s_DroneMatrixUploadBufferIndex == 0 ? s_DroneMatrixBufferBackBuffer : s_DroneMatrixBuffer);

            if (matrixBuffer == null ||
                s_DroneProceduralArgsBuffer == null ||
                s_DroneDefaultColorBuffer == null ||
                s_DroneProceduralMaterial == null ||
                !TryOpenDroneRenderMatrices(out NativeArray<float4x4> droneRenderMatrices) ||
                !TryReadDroneStates(out NativeArray<HeadlessDroneState>.ReadOnly droneStates) ||
                !TryResolveDroneProceduralArgsForUpload(
                    out NativeArray<DroneProceduralIndirectArgsDTO> proceduralArgs))
            {
                return;
            }

            if (uploadMatrices)
            {
                GraphicsBufferUploadUtility.UploadNativeArray(matrixBuffer, droneRenderMatrices, HeadlessDroneCapacity);
                if (!TryPrepareAndUploadDroneRenderInstances(droneRenderMatrices, droneStates) ||
                    !TryPrepareAndUploadDroneCullingStates(droneStates))
                {
                    return;
                }

                if (proceduralArgs.IsCreated && proceduralArgs.Length > 0)
                {
                    DroneProceduralIndirectArgsDTO args = proceduralArgs[0];
                    if (args.VertexCountPerInstance == 0u ||
                        args.InstanceCount == 0u)
                    {
                        return;
                    }
                }
                else
                    return;

                GraphicsBufferUploadUtility.UploadNativeArrayAndCopyWholeBuffer(
                    s_DroneProceduralArgsUploadBuffer,
                    s_DroneProceduralArgsBuffer,
                    proceduralArgs,
                    1);
                s_LastDroneMatrixUploadFrame = frame;
            }

            if (TryRenderGpuCulledFleet(matrixBuffer))
            {
                if (uploadMatrices)
                    s_DroneMatrixUploadBufferIndex ^= 1;
                return;
            }

            s_DroneProceduralMaterial.SetBuffer(s_DroneMatricesPropertyId, matrixBuffer);
            s_DroneProceduralMaterial.SetBuffer(s_InstanceMatricesPropertyId, matrixBuffer);
            s_DroneProceduralMaterial.SetBuffer(s_PhantomColorsPropertyId, s_DroneDefaultColorBuffer);
            if (s_DroneRenderInstanceBuffer != null)
                s_DroneProceduralMaterial.SetBuffer(s_DroneRenderInstancesPropertyId, s_DroneRenderInstanceBuffer);

            Vector3 origin = ResolveDroneRenderReferencePosition();
            s_DroneProceduralMaterial.SetVector(s_DroneProceduralCameraOriginPropertyId, new Vector4(origin.x, origin.y, origin.z, 0f));
            s_DroneProceduralMaterial.SetInt(s_UsePhantomColorsPropertyId, 0);

            UnityEngine.Graphics.DrawProceduralIndirect(
                s_DroneProceduralMaterial,
                s_DroneDrawBounds,
                MeshTopology.Triangles,
                s_DroneProceduralArgsBuffer,
                0,
                null,
                null,
                ShadowCastingMode.Off,
                false,
                s_DroneRenderLayer);
            if (uploadMatrices)
                s_DroneMatrixUploadBufferIndex ^= 1;
        }

        private static void RenderPhantomSwarm(float deltaTime)
        {
            int phantomDrawCount = ResolvePhantomDroneDrawCount();
            if (phantomDrawCount <= 0)
                return;

            if (!TryResolvePhantomAnchor(out Vector3 anchor) || !EnsurePhantomRenderResources())
                return;

            Vector3 phantomOrigin = ResolveDroneRenderReferencePosition();
            Vector3 phantomAnchorLocal = anchor - phantomOrigin;
            if (!IsFiniteVector(phantomOrigin) || !IsFiniteVector(phantomAnchorLocal))
                return;

            phantomDrawCount = Mathf.Min(phantomDrawCount, PhantomDroneCount);
            int phantomDispatchGroups = CeilDividePositive(phantomDrawCount, s_PhantomDroneThreadGroupSizeX);
            if (phantomDispatchGroups <= 0)
                return;

            UpdatePhantomDroneArgs(phantomDrawCount);
            s_PhantomDronePhaseSeconds += Mathf.Max(0f, deltaTime);
            if (s_PhantomDronePhaseSeconds >= PhantomDronePhaseWrapSeconds)
                s_PhantomDronePhaseSeconds = Mathf.Repeat(s_PhantomDronePhaseSeconds, PhantomDronePhaseWrapSeconds);

            s_PhantomDroneDrawBounds = new Bounds(
                anchor,
                new Vector3(
                    PhantomDroneBoundsDiameterMeters,
                    PhantomDroneBoundsDiameterMeters,
                    PhantomDroneBoundsDiameterMeters));

            s_PhantomDronesCompute.SetInt(s_PhantomCountPropertyId, phantomDrawCount);
            s_PhantomDronesCompute.SetVector(s_PhantomAnchorPropertyId, new Vector4(phantomAnchorLocal.x, phantomAnchorLocal.y, phantomAnchorLocal.z, 0f));
            s_PhantomDronesCompute.SetFloat(s_PhantomTimePropertyId, s_PhantomDronePhaseSeconds);
            s_PhantomDronesCompute.SetFloat(s_PhantomBaseRadiusPropertyId, PhantomDroneOrbitRadiusMeters);
            s_PhantomDronesCompute.SetFloat(s_PhantomVerticalAmplitudePropertyId, PhantomDroneVerticalAmplitudeMeters);
            s_PhantomDronesCompute.SetFloat(s_PhantomScalePropertyId, PhantomDroneScaleMeters);
            s_PhantomDronesCompute.SetInt(s_PhantomCapacityPropertyId, phantomDrawCount);
            s_PhantomDronesCompute.SetBuffer(s_PhantomDroneKernel, s_PhantomMatricesPropertyId, s_PhantomDroneMatrixBuffer);
            s_PhantomDronesCompute.SetBuffer(s_PhantomDroneKernel, s_PhantomColorsPropertyId, s_PhantomDroneColorBuffer);
            s_PhantomDronesCompute.Dispatch(
                s_PhantomDroneKernel,
                phantomDispatchGroups,
                1,
                1);

            s_DroneProceduralMaterial.SetBuffer(s_DroneMatricesPropertyId, s_PhantomDroneMatrixBuffer);
            s_DroneProceduralMaterial.SetBuffer(s_InstanceMatricesPropertyId, s_PhantomDroneMatrixBuffer);
            s_DroneProceduralMaterial.SetBuffer(s_PhantomColorsPropertyId, s_PhantomDroneColorBuffer);
            s_DroneProceduralMaterial.SetVector(s_DroneProceduralCameraOriginPropertyId, new Vector4(phantomOrigin.x, phantomOrigin.y, phantomOrigin.z, 0f));
            s_DroneProceduralMaterial.SetInt(s_UsePhantomColorsPropertyId, 1);

            UnityEngine.Graphics.DrawProceduralIndirect(
                s_DroneProceduralMaterial,
                s_PhantomDroneDrawBounds,
                MeshTopology.Triangles,
                s_PhantomDroneArgsBuffer,
                0,
                null,
                null,
                ShadowCastingMode.Off,
                false,
                s_DroneRenderLayer);
        }

        private static bool TryResolvePhantomAnchor(out Vector3 position)
        {
            if (TryResolveFormationAnchor(out position))
                return true;

            RepairDroneHub hub = RepairDroneHub.GetActiveHubAt(0);
            if (hub != null)
            {
                position = hub.DockPosition;
                return true;
            }

            return TryResolvePlayerPosition(out position);
        }

        private static bool PrepareDroneRenderInstances(
            NativeArray<DroneRenderInstance> renderInstances,
            NativeArray<float4x4> droneRenderMatrices,
            NativeArray<HeadlessDroneState>.ReadOnly droneStates)
        {
            if (!renderInstances.IsCreated ||
                !droneRenderMatrices.IsCreated ||
                renderInstances.Length < HeadlessDroneCapacity ||
                droneRenderMatrices.Length < HeadlessDroneCapacity ||
                droneStates.Length < HeadlessDroneCapacity)
            {
                return false;
            }

            for (int i = 0; i < HeadlessDroneCapacity; i++)
            {
                float transactionProgress = 0f;
                HeadlessDroneState drone = droneStates[i];
                transactionProgress = Mathf.Clamp01(drone.TransactionProgress);

                renderInstances[i] = new DroneRenderInstance
                {
                    Matrix = droneRenderMatrices[i],
                    TransactionProgress = transactionProgress,
                    Padding = float3.zero
                };
            }

            return true;
        }

        private static void CopyDroneRenderInstances(
            NativeArray<DroneRenderInstance> source,
            NativeArray<DroneRenderInstance> destination)
        {
            int count = HeadlessDroneCapacity;
            if (!source.IsCreated || !destination.IsCreated || source.Length < count || destination.Length < count)
                return;

            for (int i = 0; i < count; i++)
                destination[i] = source[i];
        }

        private static bool PrepareDroneCullingStates(
            NativeArray<DroneCullingStateGpu> cullingStates,
            NativeArray<HeadlessDroneState>.ReadOnly droneStates)
        {
            if (!cullingStates.IsCreated ||
                cullingStates.Length < HeadlessDroneCapacity ||
                droneStates.Length < HeadlessDroneCapacity)
            {
                return false;
            }

            for (int i = 0; i < HeadlessDroneCapacity; i++)
            {
                HeadlessDroneState drone = droneStates[i];
                cullingStates[i] = new DroneCullingStateGpu
                {
                    Position = drone.Position,
                    PackedStateFactionCorridor = PackStateFactionCorridor(in drone)
                };
            }

            return true;
        }

        private static void CopyDroneCullingStates(
            NativeArray<DroneCullingStateGpu> source,
            NativeArray<DroneCullingStateGpu> destination)
        {
            int count = HeadlessDroneCapacity;
            if (!source.IsCreated || !destination.IsCreated || source.Length < count || destination.Length < count)
                return;

            for (int i = 0; i < count; i++)
                destination[i] = source[i];
        }

        private static uint PackStateFactionCorridor(in HeadlessDroneState drone)
        {
            return ((uint)drone.State) |
                   ((uint)drone.FactionBit << 8) |
                   ((uint)drone.CorridorTight << 16);
        }

        private static bool TryRenderGpuCulledFleet(GraphicsBuffer matrixBuffer)
        {
            ResolveDroneCullingKernels();
            if (!s_DroneCullingKernelsResolved ||
                matrixBuffer == null ||
                s_DroneStateGpuBuffer == null ||
                s_DroneRenderInstanceBuffer == null ||
                s_DroneVisibleMatrixBuffer == null ||
                s_DroneVisibleInstanceBuffer == null ||
                s_DroneVisibleIndexBuffer == null ||
                s_DroneProceduralArgsBuffer == null ||
                s_DroneProceduralMaterial == null)
            {
                return false;
            }

            int cullDispatchGroups = CeilDividePositive(HeadlessDroneCapacity, s_DroneCullThreadGroupSizeX);
            if (cullDispatchGroups <= 0)
                return false;

            Camera camera = Camera.current;
            if (camera == null)
                return false;

            GeometryUtility.CalculateFrustumPlanes(camera, s_CullingPlanes);
            for (int i = 0; i < s_CullingPlaneVectors.Length; i++)
            {
                Plane plane = s_CullingPlanes[i];
                Vector3 normal = plane.normal;
                s_CullingPlaneVectors[i] = new Vector4(normal.x, normal.y, normal.z, plane.distance);
            }

            Vector3 cameraPosition = camera.transform.position;
            float renderDistance = ResolveDroneRenderDistanceMeters();

            s_DroneVisibleMatrixBuffer.SetCounterValue(0u);
            s_DroneVisibleInstanceBuffer.SetCounterValue(0u);
            s_DroneVisibleIndexBuffer.SetCounterValue(0u);

            s_DroneCullingCompute.SetBuffer(s_DroneCullKernel, s_DroneStatesPropertyId, s_DroneStateGpuBuffer);
            s_DroneCullingCompute.SetBuffer(s_DroneCullKernel, s_DroneMatricesPropertyId, matrixBuffer);
            s_DroneCullingCompute.SetBuffer(s_DroneCullKernel, s_DroneRenderInstancesPropertyId, s_DroneRenderInstanceBuffer);
            s_DroneCullingCompute.SetBuffer(s_DroneCullKernel, s_InstanceMatricesPropertyId, s_DroneVisibleMatrixBuffer);
            s_DroneCullingCompute.SetBuffer(s_DroneCullKernel, s_DroneVisibleInstancesPropertyId, s_DroneVisibleInstanceBuffer);
            s_DroneCullingCompute.SetBuffer(s_DroneCullKernel, s_DroneVisibleIndicesPropertyId, s_DroneVisibleIndexBuffer);
            s_DroneCullingCompute.SetVectorArray(s_CameraFrustumPlanesPropertyId, s_CullingPlaneVectors);
            s_DroneCullingCompute.SetInt(s_DroneCountPropertyId, HeadlessDroneCapacity);
            s_DroneCullingCompute.SetFloat(s_DroneCullRadiusPropertyId, DroneProceduralScaleMeters * 2.5f);
            s_DroneCullingCompute.SetVector(s_CameraPositionPropertyId, new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 0f));
            s_DroneCullingCompute.SetFloat(s_DroneRenderDistanceSqPropertyId, renderDistance * renderDistance);
            s_DroneCullingCompute.Dispatch(
                s_DroneCullKernel,
                cullDispatchGroups,
                1,
                1);
            GraphicsBuffer.CopyCount(s_DroneVisibleMatrixBuffer, s_DroneProceduralArgsBuffer, 4);

            s_DroneProceduralMaterial.SetBuffer(s_DroneMatricesPropertyId, s_DroneVisibleMatrixBuffer);
            s_DroneProceduralMaterial.SetBuffer(s_InstanceMatricesPropertyId, s_DroneVisibleMatrixBuffer);
            s_DroneProceduralMaterial.SetBuffer(s_PhantomColorsPropertyId, s_DroneDefaultColorBuffer);
            s_DroneProceduralMaterial.SetBuffer(s_DroneRenderInstancesPropertyId, s_DroneVisibleInstanceBuffer);
            s_DroneProceduralMaterial.SetVector(s_DroneProceduralCameraOriginPropertyId, new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 0f));
            s_DroneProceduralMaterial.SetInt(s_UsePhantomColorsPropertyId, 0);

            UnityEngine.Graphics.DrawProceduralIndirect(
                s_DroneProceduralMaterial,
                s_DroneDrawBounds,
                MeshTopology.Triangles,
                s_DroneProceduralArgsBuffer,
                0,
                null,
                null,
                ShadowCastingMode.Off,
                false,
                s_DroneRenderLayer);
            return true;
        }

        private static float ResolveDroneRenderDistanceMeters()
        {
            float quality = ResolveGlobalQualityWeight();
            return math.lerp(SurvivalDroneRenderDistanceMeters, HighFidelityDroneRenderDistanceMeters, quality);
        }

        private static int ResolveDroneVisualMatrixUploadModulo()
        {
            float quality = ResolveGlobalQualityWeight();
            return Mathf.Clamp(Mathf.RoundToInt(math.lerp(4f, 1f, quality)), 1, 4);
        }

        private static void UpdateDrawBounds()
        {
            if (s_DroneSlotDroneIds == null)
                return;

            bool found = false;
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;
            for (int i = 0; i < s_DroneSlotDroneIds.Length; i++)
            {
                if (s_DroneSlotDroneIds[i] <= 0)
                    continue;

                Vector3 position = s_DronePositions[i];
                if (!found)
                {
                    min = position;
                    max = position;
                    found = true;
                    continue;
                }

                min = Vector3.Min(min, position);
                max = Vector3.Max(max, position);
            }

            if (!found)
                return;

            Vector3 center = (min + max) * 0.5f;
            Vector3 size = (max - min) + new Vector3(16f, 16f, 16f);
            s_DroneDrawBounds = new Bounds(center, size);
        }

        private static void CaptureFleetBlackBoxFrame(NativeArray<HeadlessDroneState> droneStates)
        {
            if (s_DroneSlotDroneIds == null ||
                s_DroneSlotDroneIds.Length < HeadlessDroneCapacity ||
                !droneStates.IsCreated ||
                droneStates.Length < HeadlessDroneCapacity)
            {
                return;
            }

            DroneFleetBlackBoxEntry entry = BuildFleetBlackBoxEntry(droneStates);
            bool dumpRequested = (entry.Flags & 1) != 0;
            if (s_DroneHeadlessJobMutationGuardHeld)
            {
                if (TryResolveDroneMutationBuffer(
                        s_DroneHeadlessJobMutationGuardVault ?? s_CachedDataVault,
                        in s_DroneBlackBoxHandle,
                        BufferID.ShinobuDroneFleetBlackBox,
                        DroneFleetBlackBoxFrameCapacity,
                        out NativeArray<DroneFleetBlackBoxEntry> guardedBlackBox))
                {
                    WriteFleetBlackBoxEntry(in entry, guardedBlackBox);
                }

                if (dumpRequested)
                    RequestDroneBlackBoxDump();
                return;
            }

            if (!TryAcquireDroneBlackBox(
                    out NativeArray<DroneFleetBlackBoxEntry> blackBox,
                    out IDataVault vault))
            {
                return;
            }

            try
            {
                WriteFleetBlackBoxEntry(in entry, blackBox);
            }
            finally
            {
                vault.ReleaseWriteLock(in s_DroneBlackBoxHandle, SystemID.Construction);
            }

            if (dumpRequested)
                RequestDroneBlackBoxDump();
        }

        private static DroneFleetBlackBoxEntry BuildFleetBlackBoxEntry(NativeArray<HeadlessDroneState> droneStates)
        {
            int activeCount = 0;
            int stateHash = 17;
            int flags = 0;
            float3 firstPosition = float3.zero;
            for (int slot = 0; slot < HeadlessDroneCapacity; slot++)
            {
                int droneId = s_DroneSlotDroneIds[slot];
                if (droneId <= 0)
                    continue;

                HeadlessDroneState drone = droneStates[slot];
                if (activeCount == 0)
                    firstPosition = drone.Position;

                activeCount++;
                stateHash = unchecked((stateHash * 31) ^ droneId);
                stateHash = unchecked((stateHash * 31) ^ drone.State);
                stateHash = unchecked((stateHash * 31) ^ (int)math.hash(drone.Position));
                stateHash = unchecked((stateHash * 31) ^ (int)math.hash(drone.TargetPosition));

                if (!IsFiniteFloat3(drone.Position) ||
                    !IsFiniteFloat3(drone.TargetPosition) ||
                    !IsFiniteFloat3(drone.Velocity))
                {
                    flags |= DroneFleetBlackBoxFlagNonFiniteState;
                }
            }

            if (s_LastDroneAStarStatus == 2)
                flags |= DroneFleetBlackBoxFlagAStarFailure;

            int completeSignalDropCount = DockingCompleteSignalDropCount;
            int failedSignalDropCount = DockingFailedSignalDropCount;
            int itemAcquiredDropCount = ItemAcquiredSignalDropCount;
            int inventoryTransactionDropCount = InventoryTransactionSignalDropCount;
            int inventoryCommandDropCount = InventoryCommandSignalDropCount;
            int miningCommitFailureCount = DroneMiningCommitFailureCount;
            int miningCommitFailureReason = LastDroneMiningCommitFailureReason;
            if (completeSignalDropCount > 0)
                flags |= DroneFleetBlackBoxFlagDockingCompleteSignalRejected;
            if (failedSignalDropCount > 0)
                flags |= DroneFleetBlackBoxFlagDockingFailedSignalRejected;
            if (itemAcquiredDropCount > 0)
                flags |= DroneFleetBlackBoxFlagItemAcquiredSignalRejected;
            if (inventoryTransactionDropCount > 0)
                flags |= DroneFleetBlackBoxFlagInventoryTransactionSignalRejected;
            if (inventoryCommandDropCount > 0)
                flags |= DroneFleetBlackBoxFlagInventoryCommandSignalRejected;
            if (miningCommitFailureCount > 0)
                flags |= DroneFleetBlackBoxFlagMiningCommitFailed;
            stateHash = unchecked((stateHash * 31) ^ completeSignalDropCount);
            stateHash = unchecked((stateHash * 31) ^ failedSignalDropCount);
            stateHash = unchecked((stateHash * 31) ^ itemAcquiredDropCount);
            stateHash = unchecked((stateHash * 31) ^ inventoryTransactionDropCount);
            stateHash = unchecked((stateHash * 31) ^ inventoryCommandDropCount);
            stateHash = unchecked((stateHash * 31) ^ miningCommitFailureCount);
            stateHash = unchecked((stateHash * 31) ^ miningCommitFailureReason);

            return new DroneFleetBlackBoxEntry
            {
                Frame = unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId),
                ActiveCount = activeCount,
                StateHash = stateHash,
                Flags = flags,
                DeltaTime = s_LastHeadlessDeltaTime,
                DockingAborts = s_DockingAbortCount,
                PathSolves = s_DroneAStarSolvedCount,
                PathFailures = s_DroneAStarFailureCount,
                PathIterations = s_DroneAStarIterationCount,
                AveragePathfindingTimeMs = s_LastDroneAStarAveragePathfindingTimeMs,
                TasksCompleted = s_DroneTasksCompletedCount,
                FirstPosition = firstPosition,
                BoundsCenter = (float3)(s_DroneDrawBounds.center),
                BoundsExtents = (float3)(s_DroneDrawBounds.extents)
            };
        }

        private static bool WriteFleetBlackBoxEntry(
            in DroneFleetBlackBoxEntry entry,
            NativeArray<DroneFleetBlackBoxEntry> blackBox)
        {
            if (!blackBox.IsCreated || blackBox.Length <= 0)
                return false;

            int index = s_DroneBlackBoxCursor;
            if ((uint)index >= (uint)blackBox.Length)
                index = 0;

            blackBox[index] = entry;
            s_DroneBlackBoxCursor = (index + 1) % blackBox.Length;
            return true;
        }

        private static void RequestDroneBlackBoxDump()
        {
            s_DroneBlackBoxDumpPending = true;
            FlushPendingDroneBlackBoxDump();
        }

        private static void FlushPendingDroneBlackBoxDump()
        {
            if (!s_DroneBlackBoxDumpPending)
                return;

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (s_LastDroneBlackBoxDumpFrame == frame)
            {
                s_DroneBlackBoxDumpPending = false;
                return;
            }

            if (!TryReadDroneVaultBuffer(
                    s_CachedDataVault,
                    in s_DroneBlackBoxHandle,
                    BufferID.ShinobuDroneFleetBlackBox,
                    DroneFleetBlackBoxFrameCapacity,
                    out NativeArray<DroneFleetBlackBoxEntry>.ReadOnly blackBox))
            {
                return;
            }

            s_LastDroneBlackBoxDumpFrame = frame;
            s_DroneBlackBoxDumpPending = false;
            TryDumpDroneBlackBox(blackBox);
        }

        private static void TryDumpDroneBlackBox(NativeArray<DroneFleetBlackBoxEntry>.ReadOnly blackBox)
        {
            if (blackBox.Length <= 0)
                return;

            TryWriteDroneBlackBoxFile(DroneFleetBlackBoxDumpPath, blackBox);
            TryWriteDroneBlackBoxFile(DroneFleetLegacyBlackBoxDumpPath, blackBox);
            TryWriteDroneBlackBoxFile(DroneFleetShinobu334BlackBoxDumpPath, blackBox);
            TryWriteDroneBlackBoxFile(DroneFleetBlackBoxH8DumpPath, blackBox);
        }

        private static unsafe void TryWriteDroneBlackBoxFile(string relativePath, NativeArray<DroneFleetBlackBoxEntry>.ReadOnly blackBox)
        {
            try
            {
                const int headerBytes = 8;
                const int expectedEntryBytes = 80;
                int entryBytes = UnsafeUtility.SizeOf<DroneFleetBlackBoxEntry>();
                if (entryBytes != expectedEntryBytes)
                    return;

                int byteCount = headerBytes + blackBox.Length * entryBytes;
                NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(DroneFleetManager),
                    "DroneFleetBlackBoxDumpPayload",
                    NativeArrayOptions.ClearMemory);
                try
                {
                    byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                    Span<byte> header = new Span<byte>(destination, headerBytes);
                    WriteInt32LittleEndian(header, 0, DroneFleetBlackBoxFrameCapacity);
                    WriteInt32LittleEndian(header, 4, s_DroneBlackBoxCursor);
                    UnsafeUtility.MemCpy(destination + headerBytes, NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(blackBox), blackBox.Length * entryBytes);
                    NativeFaultDumpWriter.TryWriteAll(relativePath, payload, byteCount);
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref payload,
                        nameof(DroneFleetManager),
                        "DroneFleetBlackBoxDumpPayload");
                }
            }
            catch (System.Exception)
            {
            }
        }

        private static void WriteInt32LittleEndian(Span<byte> destination, int offset, int value)
        {
            uint bits = unchecked((uint)value);
            destination[offset] = (byte)bits;
            destination[offset + 1] = (byte)(bits >> 8);
            destination[offset + 2] = (byte)(bits >> 16);
            destination[offset + 3] = (byte)(bits >> 24);
        }

        private static void WriteDefaultDroneTuningConstants()
        {
            if (!TryAcquireDroneTuningConstants(
                    out NativeArray<DroneFleetTuningConstants> tuningConstants,
                    out IDataVault vault))
            {
                return;
            }

            try
            {
                tuningConstants[0] = DroneFleetTuningConstants.CreateDefault();
            }
            finally
            {
                vault.ReleaseWriteLock(in s_DroneTuningConstantsHandle, SystemID.Construction);
            }
        }

        internal static bool TryGetDroneFleetTuningConstants(out DroneFleetTuningConstants constants)
        {
            bool hasTuningConstants = TryReadDroneTuningConstants(
                out NativeArray<DroneFleetTuningConstants>.ReadOnly tuningConstants);
            constants = hasTuningConstants && tuningConstants.Length > 0
                ? SanitizeDroneTuning(tuningConstants[0])
                : DroneFleetTuningConstants.CreateDefault();
            return hasTuningConstants;
        }

        internal static void ApplyDroneFleetTuningConstants(in DroneFleetTuningConstants constants)
        {
            EnsureInitialized();
            if (!TryAcquireDroneTuningConstants(
                    out NativeArray<DroneFleetTuningConstants> tuningConstants,
                    out IDataVault vault))
            {
                return;
            }

            try
            {
                unsafe
                {
                    DroneFleetTuningConstants* tuningPtr = (DroneFleetTuningConstants*)tuningConstants.GetUnsafePtr();
                    ref DroneFleetTuningConstants tuning = ref UnsafeUtility.AsRef<DroneFleetTuningConstants>(tuningPtr);
                    tuning = SanitizeDroneTuning(constants);
                }
            }
            finally
            {
                vault.ReleaseWriteLock(in s_DroneTuningConstantsHandle, SystemID.Construction);
            }
        }

        internal static bool TryGetDroneFleetAutomationStats(out DroneFleetAutomationStats stats)
        {
            DroneFleetTuningConstants tuning = ResolveDroneTuning();
            stats = new DroneFleetAutomationStats
            {
                ActiveDrones = CountManagedHeadlessDrones(),
                PathSolves = s_DroneAStarSolvedCount,
                PathFailures = s_DroneAStarFailureCount,
                PathIterations = s_DroneAStarIterationCount,
                TasksCompleted = s_DroneTasksCompletedCount,
                LastAStarStatus = s_LastDroneAStarStatus,
                SteeringTickModulo = s_LastDroneSteeringTickModulo,
                ChassisSpecCount = s_DroneChassisSpecCount,
                AveragePathfindingTimeMs = s_LastDroneAStarAveragePathfindingTimeMs,
                SdfRepulsionStrength = tuning.SdfRepulsionStrength,
                AStarCellSize = tuning.AStarCellSize,
                AverageBatteryPercent = s_LastFleetStatusSnapshot.AverageBattery
            };

            return TryReadDroneStates(out NativeArray<HeadlessDroneState>.ReadOnly _);
        }

        internal static int CopyDroneFleetDebugRoutes(DroneFleetDebugRoute[] buffer)
        {
            if (buffer == null ||
                buffer.Length <= 0 ||
                !TryReadDroneStates(out NativeArray<HeadlessDroneState>.ReadOnly droneStates) ||
                s_DroneSlotDroneIds == null ||
                s_HeadlessJobScheduled)
            {
                return 0;
            }

            DroneFleetTuningConstants tuning = ResolveDroneTuning();
            bool sdfGridReady = TryAcquireDroneSdfGrid(
                in tuning,
                ResolveDroneSdfQueryOrigin(droneStates),
                out DroneSdfGrid sdfGrid,
                out VoxelSonarSdfReadLease sdfLease,
                out IVoxelSonarSdfReadLeaseModel sdfLeaseModel);
            bool macroWaypointBuffersReady = TryReadDroneMacroWaypointBuffers(
                out NativeArray<PathWaypointDTO>.ReadOnly macroWaypoints,
                out NativeArray<byte>.ReadOnly macroWaypointStates);
            bool macroRouteBuffersReady = TryReadDroneMacroRouteBuffers(
                out NativeArray<int>.ReadOnly macroRouteNodes,
                out NativeArray<byte>.ReadOnly macroRouteCounts);
            bool aStarNodeStatesReady = TryReadDroneAStarNodeStates(out NativeArray<byte>.ReadOnly aStarNodeStates);
            int count = 0;
            try
            {
                int limit = Mathf.Min(buffer.Length, HeadlessDroneCapacity);
                for (int slot = 0; slot < HeadlessDroneCapacity && count < limit; slot++)
                {
                    if (s_DroneSlotDroneIds[slot] <= 0)
                        continue;

                    HeadlessDroneState drone = droneStates[slot];
                    if (drone.State == (byte)HeadlessDroneRuntimeState.Empty ||
                        drone.State == (byte)HeadlessDroneRuntimeState.Sacrificed ||
                        drone.State == (byte)HeadlessDroneRuntimeState.Completed)
                    {
                        continue;
                    }

                    float3 waypoint = drone.TargetPosition;
                    int pathStatus = 0;
                    if (macroWaypointBuffersReady &&
                        slot < macroWaypoints.Length &&
                        slot < macroWaypointStates.Length &&
                        macroWaypointStates[slot] != 0)
                    {
                        waypoint = macroWaypoints[slot].LocalPosition;
                        pathStatus = macroWaypointStates[slot];
                    }

                    float3 sdfNormal = float3.zero;
                    byte flags = 0;
                    if (sdfGridReady && sdfGrid.TrySampleRepulsion(drone.Position, out sdfNormal, out _))
                        flags |= 1;

                    int routePointCount = ResolveDebugRoutePoints(
                        macroRouteNodes,
                        macroRouteCounts,
                        macroRouteBuffersReady,
                        slot,
                        drone.Position,
                        tuning.AStarCellSize,
                        out float3 routePoint0,
                        out float3 routePoint1,
                        out float3 routePoint2,
                        out float3 routePoint3);
                    int closedPointCount = ResolveDebugClosedSetPoints(
                        aStarNodeStates,
                        aStarNodeStatesReady,
                        slot,
                        drone.Position,
                        tuning.AStarCellSize,
                        out float3 closedPoint0,
                        out float3 closedPoint1,
                        out float3 closedPoint2,
                        out float3 closedPoint3);
                    buffer[count++] = new DroneFleetDebugRoute
                    {
                        Position = drone.Position,
                        Target = drone.TargetPosition,
                        Waypoint = waypoint,
                        SdfNormal = sdfNormal,
                        Velocity = drone.Velocity,
                        RoutePoint0 = routePoint0,
                        RoutePoint1 = routePoint1,
                        RoutePoint2 = routePoint2,
                        RoutePoint3 = routePoint3,
                        RoutePointCount = routePointCount,
                        ClosedPoint0 = closedPoint0,
                        ClosedPoint1 = closedPoint1,
                        ClosedPoint2 = closedPoint2,
                        ClosedPoint3 = closedPoint3,
                        DroneId = drone.DroneId,
                        PathStatus = pathStatus,
                        BatteryPercent = drone.BatteryPercent,
                        State = drone.State,
                        Flags = flags,
                        Reserved0 = (ushort)math.min(closedPointCount, ushort.MaxValue),
                        Reserved1 = 0u,
                        Reserved2 = 0u,
                        Reserved3 = 0u
                    };
                }
            }
            finally
            {
                if (sdfGridReady && sdfLeaseModel != null && sdfLease.IsValid)
                    sdfLeaseModel.ReleaseNearestSonarSdfReadLease(in sdfLease);
            }

            return count;
        }

        private static int ResolveDebugRoutePoints(
            NativeArray<int>.ReadOnly macroRouteNodes,
            NativeArray<byte>.ReadOnly macroRouteCounts,
            bool routeBuffersReady,
            int slot,
            float3 origin,
            float cellSize,
            out float3 routePoint0,
            out float3 routePoint1,
            out float3 routePoint2,
            out float3 routePoint3)
        {
            routePoint0 = origin;
            routePoint1 = origin;
            routePoint2 = origin;
            routePoint3 = origin;
            if (!routeBuffersReady ||
                slot < 0 ||
                slot >= macroRouteCounts.Length)
            {
                return 0;
            }

            int nodeCount = math.min(macroRouteCounts[slot], DroneAStarRouteNodeStride);
            int pointCount = math.min(nodeCount, DroneAStarRouteDebugPointCount);
            int offset = slot * DroneAStarRouteNodeStride;
            if (nodeCount <= 0 || offset < 0 || offset >= macroRouteNodes.Length)
                return 0;

            float cell = Mathf.Max(0.5f, cellSize);
            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                int sourceIndex = nodeCount - 1 - pointIndex;
                int packedNode = macroRouteNodes[offset + sourceIndex];
                float3 routePoint = ResolveAStarRoutePoint(packedNode, origin, cell);
                if (pointIndex == 0)
                    routePoint0 = routePoint;
                else if (pointIndex == 1)
                    routePoint1 = routePoint;
                else if (pointIndex == 2)
                    routePoint2 = routePoint;
                else
                    routePoint3 = routePoint;
            }

            return pointCount;
        }

        private static int ResolveDebugClosedSetPoints(
            NativeArray<byte>.ReadOnly aStarNodeStates,
            bool nodeStatesReady,
            int slot,
            float3 origin,
            float cellSize,
            out float3 closedPoint0,
            out float3 closedPoint1,
            out float3 closedPoint2,
            out float3 closedPoint3)
        {
            closedPoint0 = origin;
            closedPoint1 = origin;
            closedPoint2 = origin;
            closedPoint3 = origin;
            if (!nodeStatesReady || slot < 0)
            {
                return 0;
            }

            int nodeBase = slot * DroneAStarNodeCapacity;
            if (nodeBase < 0 || nodeBase + DroneAStarNodeCapacity > aStarNodeStates.Length)
                return 0;

            float cell = Mathf.Max(0.5f, cellSize);
            int count = 0;
            for (int node = 0; node < DroneAStarNodeCapacity && count < DroneAStarRouteDebugPointCount; node++)
            {
                if (aStarNodeStates[nodeBase + node] != 2)
                    continue;

                float3 point = ResolveAStarRoutePoint(node, origin, cell);
                if (count == 0)
                    closedPoint0 = point;
                else if (count == 1)
                    closedPoint1 = point;
                else if (count == 2)
                    closedPoint2 = point;
                else
                    closedPoint3 = point;

                count++;
            }

            return count;
        }

        private static float3 ResolveAStarRoutePoint(int packedNode, float3 origin, float cell)
        {
            int z = packedNode / (DroneAStarGridSide * DroneAStarGridSide);
            int remainder = packedNode - (z * DroneAStarGridSide * DroneAStarGridSide);
            int y = remainder / DroneAStarGridSide;
            int x = remainder - (y * DroneAStarGridSide);
            float3 coord = new float3(x, y, z) - new float3(DroneAStarGridSide >> 1, DroneAStarGridSide >> 1, DroneAStarGridSide >> 1);
            return origin + (coord * cell);
        }

#if UNITY_EDITOR
        internal static bool TryAutoApplyDroneSpecsCsv(out int keysApplied)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string navigationProfilesPath = Path.Combine(projectRoot, DroneNavigationProfilesCsvFileName);
            if (TryApplyDroneSpecsCsv(navigationProfilesPath, out keysApplied))
                return true;

            string hardwareProfilesPath = Path.Combine(projectRoot, DroneHardwareProfilesCsvFileName);
            if (TryApplyDroneSpecsCsv(hardwareProfilesPath, out keysApplied))
                return true;

            string primaryPath = Path.Combine(projectRoot, DroneSpecsCsvFileName);
            if (TryApplyDroneSpecsCsv(primaryPath, out keysApplied))
                return true;

            string legacyPath = Path.Combine(projectRoot, DroneSpecsCsvLegacyFileName);
            return TryApplyDroneSpecsCsv(legacyPath, out keysApplied);
        }

        internal static bool TryApplyDroneSpecsCsv(string path, out int keysApplied)
        {
            keysApplied = 0;
            string resolvedPath = string.IsNullOrWhiteSpace(path)
                ? Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), DroneNavigationProfilesCsvFileName)
                : path;

            if (!File.Exists(resolvedPath))
                return false;

            EnsureInitialized();

            Span<byte> csvScratch = stackalloc byte[DroneSpecsCsvMaxBytes];
            int bytesRead;
            using (FileStream stream = File.OpenRead(resolvedPath))
            {
                long fileLength = stream.Length;
                if (fileLength <= 0L)
                    return false;

                int bytesToRead = fileLength > DroneSpecsCsvMaxBytes
                    ? DroneSpecsCsvMaxBytes
                    : (int)fileLength;
                bytesRead = stream.Read(csvScratch.Slice(0, bytesToRead));
            }

            if (bytesRead <= 0)
                return false;

            DroneFleetTuningConstants tuning = ResolveDroneTuning();
            Span<DroneChassisSpecDTO> stagedChassisSpecs = stackalloc DroneChassisSpecDTO[DroneChassisSpecCapacity];
            int stagedChassisSpecCount = 0;
            ReadOnlySpan<byte> bytes = csvScratch.Slice(0, bytesRead);
            int lineStart = 0;
            for (int i = 0; i <= bytesRead; i++)
            {
                if (i < bytesRead && bytes[i] != (byte)'\n')
                    continue;

                if (TryApplyDroneSpecLine(bytes, lineStart, i, ref tuning))
                    keysApplied++;
                lineStart = i + 1;
            }

            tuning = SanitizeDroneTuning(tuning);
            lineStart = 0;
            for (int i = 0; i <= bytesRead; i++)
            {
                if (i < bytesRead && bytes[i] != (byte)'\n')
                    continue;

                if (TryApplyDroneChassisSpecLine(bytes, lineStart, i, in tuning, stagedChassisSpecs, ref stagedChassisSpecCount))
                    keysApplied++;
                lineStart = i + 1;
            }

            if (keysApplied <= 0)
                return false;

            ApplyDroneFleetTuningConstants(in tuning);
            if (stagedChassisSpecCount > 0)
                CommitDroneChassisSpecs(stagedChassisSpecs, stagedChassisSpecCount);
            return true;
        }

        private static bool TryApplyDroneSpecLine(ReadOnlySpan<byte> bytes, int lineStart, int lineEnd, ref DroneFleetTuningConstants tuning)
        {
            int start = TrimAsciiLeft(bytes, lineStart, lineEnd);
            int end = TrimAsciiRight(bytes, start, lineEnd);
            if (start >= end || bytes[start] == (byte)'#')
                return false;

            int separator = FindKeyValueSeparator(bytes, start, end);

            if (separator <= start || separator >= end - 1)
                return false;

            int secondSeparator = FindCsvSeparator(bytes, separator + 1, end);
            if (secondSeparator > separator)
                return false;

            int keyStart = TrimAsciiLeft(bytes, start, separator);
            int keyEnd = TrimAsciiRight(bytes, keyStart, separator);
            int valueStart = TrimAsciiLeft(bytes, separator + 1, end);
            int valueEnd = TrimAsciiRight(bytes, valueStart, end);
            if (keyStart >= keyEnd || !TryParseAsciiFloat(bytes, valueStart, valueEnd, out float value))
                return false;

            return TryApplyDroneSpecKey(bytes, keyStart, keyEnd, value, ref tuning);
        }

        private static bool TryApplyDroneChassisSpecLine(
            ReadOnlySpan<byte> bytes,
            int lineStart,
            int lineEnd,
            in DroneFleetTuningConstants tuning,
            Span<DroneChassisSpecDTO> stagedSpecs,
            ref int stagedCount)
        {
            int typeEnd = FindCsvSeparator(bytes, lineStart, lineEnd);
            if (typeEnd <= lineStart)
                return false;

            int secondSeparator = FindCsvSeparator(bytes, typeEnd + 1, lineEnd);
            if (secondSeparator <= typeEnd)
                return false;

            int typeStart = TrimAsciiLeft(bytes, lineStart, typeEnd);
            int trimmedTypeEnd = TrimAsciiRight(bytes, typeStart, typeEnd);
            if (typeStart >= trimmedTypeEnd ||
                AsciiEqualsIgnoreCase(bytes, typeStart, trimmedTypeEnd, "Type") ||
                AsciiEqualsIgnoreCase(bytes, typeStart, trimmedTypeEnd, "DroneType") ||
                AsciiEqualsIgnoreCase(bytes, typeStart, trimmedTypeEnd, "Chassis"))
            {
                return false;
            }

            if (!IsKnownDroneChassisName(bytes, typeStart, trimmedTypeEnd) &&
                IsReservedDroneSpecKeyName(bytes, typeStart, trimmedTypeEnd))
            {
                return false;
            }

            uint typeHash = ComputeAsciiFnv1aLower(bytes, typeStart, trimmedTypeEnd);
            if (!TryCreateDroneChassisAuthoringSeed(typeHash, in tuning, out DroneChassisSpecDTO spec))
                return false;

            int cursor = typeEnd + 1;
            bool parsedAnyValue = false;

            if (TryReadDelimitedFloat(bytes, ref cursor, lineEnd, out float maxSpeed))
            {
                spec.MaxSpeed = maxSpeed;
                parsedAnyValue = true;
            }

            if (TryReadDelimitedFloat(bytes, ref cursor, lineEnd, out float batteryCapacity))
            {
                spec.BatteryCapacity = batteryCapacity;
                parsedAnyValue = true;
            }

            if (TryReadDelimitedFloat(bytes, ref cursor, lineEnd, out float batteryDrainRate))
            {
                spec.BatteryDrainRate = batteryDrainRate;
                parsedAnyValue = true;
            }

            if (TryReadDelimitedFloat(bytes, ref cursor, lineEnd, out float repairSpeed))
            {
                spec.RepairSpeed = repairSpeed;
                parsedAnyValue = true;
            }

            if (TryReadDelimitedFloat(bytes, ref cursor, lineEnd, out float cargoCapacity))
            {
                spec.CargoCapacity = cargoCapacity;
                parsedAnyValue = true;
            }

            if (TryReadDelimitedFloat(bytes, ref cursor, lineEnd, out float miningHoldSeconds))
            {
                spec.MiningHoldSeconds = miningHoldSeconds;
                parsedAnyValue = true;
            }

            if (TryReadDelimitedFloat(bytes, ref cursor, lineEnd, out float sdfRepulsionScale))
            {
                spec.SdfRepulsionScale = sdfRepulsionScale;
                parsedAnyValue = true;
            }

            if (TryReadDelimitedFloat(bytes, ref cursor, lineEnd, out float clearanceRadiusMeters))
            {
                spec.ClearanceRadiusMeters = clearanceRadiusMeters;
                parsedAnyValue = true;
            }

            if (!parsedAnyValue)
                return false;

            return TryUpsertStagedDroneChassisSpec(spec, in tuning, stagedSpecs, ref stagedCount);
        }

        private static bool IsKnownDroneChassisName(ReadOnlySpan<byte> bytes, int start, int end)
        {
            return AsciiEqualsIgnoreCase(bytes, start, end, "Repair") ||
                AsciiEqualsIgnoreCase(bytes, start, end, "Mining") ||
                AsciiEqualsIgnoreCase(bytes, start, end, "Combat") ||
                AsciiEqualsIgnoreCase(bytes, start, end, "CutParasite");
        }

        private static bool IsReservedDroneSpecKeyName(ReadOnlySpan<byte> bytes, int start, int end)
        {
            return AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.MaxDroneSpeed)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, "Speed") ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.BatteryDrainRate)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, "BatteryDrain") ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.SdfRepulsionStrength)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, "SDF") ||
                AsciiEqualsIgnoreCase(bytes, start, end, "SeparationForce") ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.RepairSpeed)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, "Repair") ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.CargoCapacity)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, "Cargo") ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.MiningHoldSeconds)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.SurvivalSteeringHz)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, "LowTierSteeringHz") ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.StandardSteeringHz)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, "MidTierSteeringHz") ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.HighFidelitySteeringHz)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, "HighTierSteeringHz") ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.OverkillSteeringHz)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, "UltraTierSteeringHz") ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.AStarCellSize)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.SurvivalSolveBudget)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, "LowTierSolveBudget") ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.StandardSolveBudget)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, "MidTierSolveBudget") ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.HighFidelitySolveBudget)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, "HighTierSolveBudget") ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.OverkillSolveBudget)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, "UltraTierSolveBudget") ||
                AsciiEqualsIgnoreCase(bytes, start, end, "MaxNodesExpandedPerFrame") ||
                AsciiEqualsIgnoreCase(bytes, start, end, nameof(DroneFleetTuningConstants.Reserved0)) ||
                AsciiEqualsIgnoreCase(bytes, start, end, "HeuristicWeight");
        }

        private static bool TryApplyDroneSpecKey(ReadOnlySpan<byte> bytes, int keyStart, int keyEnd, float value, ref DroneFleetTuningConstants tuning)
        {
            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.MaxDroneSpeed)) ||
                AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, "Speed"))
            {
                tuning.MaxDroneSpeed = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.BatteryDrainRate)) ||
                AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, "BatteryDrain"))
            {
                tuning.BatteryDrainRate = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.SdfRepulsionStrength)) ||
                AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, "SDF") ||
                AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, "SeparationForce"))
            {
                tuning.SdfRepulsionStrength = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.RepairSpeed)) ||
                AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, "Repair"))
            {
                tuning.RepairSpeed = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.CargoCapacity)) ||
                AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, "Cargo"))
            {
                tuning.CargoCapacity = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.MiningHoldSeconds)))
            {
                tuning.MiningHoldSeconds = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.SurvivalSteeringHz)) ||
                AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, "LowTierSteeringHz"))
            {
                tuning.SurvivalSteeringHz = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.StandardSteeringHz)) ||
                AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, "MidTierSteeringHz"))
            {
                tuning.StandardSteeringHz = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.HighFidelitySteeringHz)) ||
                AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, "HighTierSteeringHz"))
            {
                tuning.HighFidelitySteeringHz = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.OverkillSteeringHz)) ||
                AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, "UltraTierSteeringHz"))
            {
                tuning.OverkillSteeringHz = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.AStarCellSize)))
            {
                tuning.AStarCellSize = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.SurvivalSolveBudget)) ||
                AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, "LowTierSolveBudget"))
            {
                tuning.SurvivalSolveBudget = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.StandardSolveBudget)) ||
                AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, "MidTierSolveBudget"))
            {
                tuning.StandardSolveBudget = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.HighFidelitySolveBudget)) ||
                AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, "HighTierSolveBudget"))
            {
                tuning.HighFidelitySolveBudget = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.OverkillSolveBudget)) ||
                AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, "UltraTierSolveBudget"))
            {
                tuning.OverkillSolveBudget = value;
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, "MaxNodesExpandedPerFrame"))
            {
                tuning.OverkillSolveBudget = math.max(1f, value * (1f / 48f));
                return true;
            }

            if (AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, nameof(DroneFleetTuningConstants.Reserved0)) ||
                AsciiEqualsIgnoreCase(bytes, keyStart, keyEnd, "HeuristicWeight"))
            {
                tuning.Reserved0 = value;
                return true;
            }

            return false;
        }

        private static int FindKeyValueSeparator(ReadOnlySpan<byte> bytes, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                byte token = bytes[i];
                if (token == (byte)',' || token == (byte)'=' || token == (byte)';' || token == (byte)'\t')
                    return i;
            }

            return -1;
        }

        private static int FindCsvSeparator(ReadOnlySpan<byte> bytes, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                byte token = bytes[i];
                if (token == (byte)',' || token == (byte)';' || token == (byte)'\t')
                    return i;
            }

            return -1;
        }

        private static bool TryReadDelimitedFloat(ReadOnlySpan<byte> bytes, ref int cursor, int end, out float value)
        {
            value = 0f;
            if (cursor >= end)
                return false;

            int fieldEnd = FindCsvSeparator(bytes, cursor, end);
            if (fieldEnd < 0)
                fieldEnd = end;

            int valueStart = TrimAsciiLeft(bytes, cursor, fieldEnd);
            int valueEnd = TrimAsciiRight(bytes, valueStart, fieldEnd);
            cursor = fieldEnd < end ? fieldEnd + 1 : end;
            return valueStart < valueEnd && TryParseAsciiFloat(bytes, valueStart, valueEnd, out value);
        }

        private static uint ComputeAsciiFnv1aLower(ReadOnlySpan<byte> bytes, int start, int end)
        {
            uint hash = 2166136261u;
            for (int i = start; i < end; i++)
            {
                hash ^= ToAsciiLower(bytes[i]);
                hash *= 16777619u;
            }

            return hash;
        }

        private static int TrimAsciiLeft(ReadOnlySpan<byte> bytes, int start, int end)
        {
            while (start < end && IsAsciiWhitespace(bytes[start]))
                start++;

            return start;
        }

        private static int TrimAsciiRight(ReadOnlySpan<byte> bytes, int start, int end)
        {
            while (end > start && IsAsciiWhitespace(bytes[end - 1]))
                end--;

            return end;
        }

        private static bool TryParseAsciiFloat(ReadOnlySpan<byte> bytes, int start, int end, out float value)
        {
            value = 0f;
            if (start >= end)
                return false;

            int i = start;
            bool negative = false;
            if (bytes[i] == (byte)'+' || bytes[i] == (byte)'-')
            {
                negative = bytes[i] == (byte)'-';
                i++;
            }

            float result = 0f;
            bool hasDigit = false;
            while (i < end && IsAsciiDigit(bytes[i]))
            {
                result = (result * 10f) + (bytes[i] - (byte)'0');
                hasDigit = true;
                i++;
            }

            if (i < end && bytes[i] == (byte)'.')
            {
                i++;
                float scale = 0.1f;
                while (i < end && IsAsciiDigit(bytes[i]))
                {
                    result += (bytes[i] - (byte)'0') * scale;
                    scale *= 0.1f;
                    hasDigit = true;
                    i++;
                }
            }

            int exponent = 0;
            if (hasDigit && i < end && (bytes[i] == (byte)'e' || bytes[i] == (byte)'E'))
            {
                i++;
                bool exponentNegative = false;
                if (i < end && (bytes[i] == (byte)'+' || bytes[i] == (byte)'-'))
                {
                    exponentNegative = bytes[i] == (byte)'-';
                    i++;
                }

                bool hasExponentDigit = false;
                while (i < end && IsAsciiDigit(bytes[i]))
                {
                    exponent = math.min(38, (exponent * 10) + (bytes[i] - (byte)'0'));
                    hasExponentDigit = true;
                    i++;
                }

                if (!hasExponentDigit)
                    return false;

                if (exponentNegative)
                    exponent = -exponent;
            }

            if (!hasDigit || i != end)
                return false;

            if (exponent != 0)
                result *= ResolvePow10(exponent);

            value = negative ? -result : result;
            return math.isfinite(value);
        }

        private static float ResolvePow10(int exponent)
        {
            int steps = math.min(38, math.abs(exponent));
            float scale = 1f;
            for (int i = 0; i < steps; i++)
                scale *= 10f;

            return exponent < 0 ? math.rcp(scale) : scale;
        }

        private static bool AsciiEqualsIgnoreCase(ReadOnlySpan<byte> bytes, int start, int end, string expected)
        {
            int length = end - start;
            if (length != expected.Length)
                return false;

            for (int i = 0; i < length; i++)
            {
                byte actual = ToAsciiLower(bytes[start + i]);
                byte target = ToAsciiLower((byte)expected[i]);
                if (actual != target)
                    return false;
            }

            return true;
        }

        private static bool IsAsciiWhitespace(byte value)
        {
            return value == (byte)' ' ||
                   value == (byte)'\t' ||
                   value == (byte)'\r' ||
                   value == (byte)'\n';
        }

        private static bool IsAsciiDigit(byte value)
        {
            return value >= (byte)'0' && value <= (byte)'9';
        }

        private static byte ToAsciiLower(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }
#endif

        private static void HandleSubmarineSnapshotUpdated(in HectonSubmarineOsSnapshot snapshot)
        {
            s_EmergencyLevel = snapshot.EmergencyLevel;
            PublishSnapshot();
        }

        private static void HandleStorageReservationCommitResolved(int requesterId, int reservationId, bool committed)
        {
            if (requesterId <= 0 ||
                s_DroneSlotDroneIds == null ||
                s_PendingResupplyGrantBySlot == null ||
                s_PendingResupplyFailureBySlot == null ||
                s_PendingResupplyReservationIdsBySlot == null)
                return;

            int slot = ResolveHeadlessSlot(requesterId);
            if (slot < 0 ||
                slot >= s_PendingResupplyGrantBySlot.Length ||
                slot >= s_PendingResupplyFailureBySlot.Length ||
                slot >= s_PendingResupplyReservationIdsBySlot.Length)
            {
                ReportStorageReservationStaleAck(requesterId);
                return;
            }

            int expectedReservationId = s_PendingResupplyReservationIdsBySlot[slot];
            if (expectedReservationId <= 0)
            {
                ReportStorageReservationStaleAck(requesterId);
                return;
            }

            if (reservationId != expectedReservationId)
            {
                ReportStorageReservationMismatchAck(reservationId);
                return;
            }

            bool commitSucceeded = committed && reservationId > 0;
            if (TryApplyResolvedResupplyCommitToLiveSlot(slot, commitSucceeded))
                return;

            if (commitSucceeded)
            {
                s_PendingResupplyGrantBySlot[slot] = true;
                s_PendingResupplyFailureBySlot[slot] = false;
            }
            else
            {
                if (s_PendingResupplyGrantBySlot[slot])
                    return;

                s_PendingResupplyGrantBySlot[slot] = false;
                s_PendingResupplyFailureBySlot[slot] = true;
            }
        }

        private static bool IsEligibleRepairTarget(PowerGrid hubGrid, BaseModule module, float dispatchIntegrityThreshold)
        {
            if (module == null)
                return false;

            float recoverableIntegrity = Mathf.Max(1f, module.MaxRecoverableIntegrity);
            float integrity01 = Mathf.Clamp01(module.CurrentIntegrity * math.rcp(recoverableIntegrity));
            bool graphRuptured = BaseDegradationSystem.IsModuleRuptured(module);
            bool belowThreshold = integrity01 < dispatchIntegrityThreshold;

            if (!belowThreshold && !module.IsFlooded && !module.HasCascadeFailure && !graphRuptured)
                return false;

            if (IsDifferentGrid(hubGrid, module))
                return false;

            return module.CurrentIntegrity < recoverableIntegrity || module.IsFlooded || module.HasCascadeFailure || graphRuptured;
        }

        private static bool IsDifferentGrid(PowerGrid hubGrid, BaseModule module)
        {
            if (hubGrid == null || module == null)
                return false;

            PowerGrid moduleGrid = module.CachedPowerGrid;
            if (moduleGrid == null)
                return false;

            return !ReferenceEquals(moduleGrid, hubGrid);
        }

        private static float ResolveCriticalityWeight(BaseModule module)
        {
            float recoverableIntegrity = Mathf.Max(1f, module.MaxRecoverableIntegrity);
            float integrity01 = Mathf.Clamp01(module.CurrentIntegrity * math.rcp(recoverableIntegrity));
            float integrityDeficit01 = 1f - integrity01;
            float weight = 1f + (integrityDeficit01 * 4f);

            if (module.IsFlooded)
                weight += FloodCriticalityBonus;

            if (module.IsBreached)
                weight += BreachCriticalityBonus;

            if (module.HasCascadeFailure)
                weight += CascadeCriticalityBonus;

            if (BaseDegradationSystem.IsModuleRuptured(module))
                weight += RuptureCriticalityBonus;

            weight += (1f - Mathf.Clamp01(module.AirReserveNormalized)) * AirReserveCriticalityScale;

            if (s_EmergencyLevel == SubmarineEmergencyLevel.Evacuate)
                weight *= EmergencyCriticalityScale;

            return weight;
        }

        private static float ResolveParasiteCriticalityWeight(BaseModule module, in FloraInteractionManager.ModuleParasiteTarget parasiteTarget)
        {
            float moduleAirRisk = module != null ? 1f - Mathf.Clamp01(module.AirReserveNormalized) : 0f;
            float infection = Mathf.Clamp01(parasiteTarget.InfectionLevel);
            float weight = ParasiteCriticalityBonus + (infection * 6f) + (moduleAirRisk * AirReserveCriticalityScale);
            if (module != null && module.HasCascadeFailure)
                weight += CascadeCriticalityBonus;

            if (s_EmergencyLevel == SubmarineEmergencyLevel.Evacuate)
                weight *= EmergencyCriticalityScale;

            return weight;
        }

        private static bool TryAcquireTaskAssignmentMutationViews(
            int requiredCount,
            out NativeArray<int> taskClaimCounts,
            out NativeArray<DroneAssignmentTaskDTO> taskPriorityHeap,
            out IDataVault guardedVault)
        {
            taskClaimCounts = default;
            taskPriorityHeap = default;
            guardedVault = null;

            IDataVault vault = s_CachedDataVault;
            if (vault == null ||
                requiredCount <= 0 ||
                vault.IsCompactionFenceActive ||
                !TryEnsureTaskClaimCountsBuffer(vault, requiredCount) ||
                !TryEnsureTaskPriorityHeapBuffer(vault) ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(TaskAssignmentMutationGuardMask))
            {
                return false;
            }

            bool releaseOnExit = true;
            try
            {
                if (vault.IsCompactionFenceActive ||
                    !vault.TryReadHandle(in s_TaskClaimCountsHandle, out taskClaimCounts) ||
                    !vault.TryReadHandle(in s_DroneTaskPriorityHeapHandle, out taskPriorityHeap) ||
                    vault.IsCompactionFenceActive ||
                    !taskClaimCounts.IsCreated ||
                    taskClaimCounts.Length < requiredCount ||
                    !taskPriorityHeap.IsCreated ||
                    taskPriorityHeap.Length < HeadlessTaskCapacity)
                {
                    taskClaimCounts = default;
                    taskPriorityHeap = default;
                    return false;
                }

                guardedVault = vault;
                releaseOnExit = false;
                return true;
            }
            finally
            {
                if (releaseOnExit)
                    vault.ReleaseMutationGuard(TaskAssignmentMutationGuardMask);
            }
        }

        private static bool TryEnsureTaskClaimCountsBuffer(IDataVault vault, int requiredCount)
        {
            if (vault == null || requiredCount <= 0)
                return false;

            if (!TryOpenDroneVaultBuffer(
                    vault,
                    in s_TaskClaimCountsHandle,
                    BufferID.ShinobuDroneFleetTaskClaimCounts,
                    requiredCount,
                    out NativeArray<int> _))
            {
                if (vault.TryGetGenerationHandle<int>(
                        BufferID.ShinobuDroneFleetTaskClaimCounts,
                        out VaultGenerationHandle<int> existingHandle))
                {
                    s_TaskClaimCountsHandle = existingHandle;
                }

                if (!TryOpenDroneVaultBuffer(
                        vault,
                        in s_TaskClaimCountsHandle,
                        BufferID.ShinobuDroneFleetTaskClaimCounts,
                        requiredCount,
                        out _))
                {
                    int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(requiredCount, InitialTaskCapacity));
                    s_TaskClaimCountsHandle = vault.EnsureGenerationHandle<int>(
                        BufferID.ShinobuDroneFleetTaskClaimCounts,
                        nextCapacity,
                        SystemID.Construction,
                        NativeArrayOptions.ClearMemory);
                }
            }

            if (TryOpenDroneVaultBuffer(
                    vault,
                    in s_TaskClaimCountsHandle,
                    BufferID.ShinobuDroneFleetTaskClaimCounts,
                    requiredCount,
                    out _))
            {
                return true;
            }

            s_TaskClaimCountsHandle = default;
            return false;
        }

        private static bool TryEnsureTaskPriorityHeapBuffer(IDataVault vault)
        {
            if (vault == null)
                return false;

            if (!TryOpenDroneVaultBuffer(
                    vault,
                    in s_DroneTaskPriorityHeapHandle,
                    BufferID.ShinobuDroneFleetTaskPriorityHeap,
                    HeadlessTaskCapacity,
                    out NativeArray<DroneAssignmentTaskDTO> _))
            {
                if (vault.TryGetGenerationHandle<DroneAssignmentTaskDTO>(
                        BufferID.ShinobuDroneFleetTaskPriorityHeap,
                        out VaultGenerationHandle<DroneAssignmentTaskDTO> existingHandle))
                {
                    s_DroneTaskPriorityHeapHandle = existingHandle;
                }

                if (!TryOpenDroneVaultBuffer(
                        vault,
                        in s_DroneTaskPriorityHeapHandle,
                        BufferID.ShinobuDroneFleetTaskPriorityHeap,
                        HeadlessTaskCapacity,
                        out _))
                {
                    s_DroneTaskPriorityHeapHandle = vault.EnsureGenerationHandle<DroneAssignmentTaskDTO>(
                        BufferID.ShinobuDroneFleetTaskPriorityHeap,
                        HeadlessTaskCapacity,
                        SystemID.Construction,
                        NativeArrayOptions.ClearMemory);
                }
            }

            if (TryOpenDroneVaultBuffer(
                    vault,
                    in s_DroneTaskPriorityHeapHandle,
                    BufferID.ShinobuDroneFleetTaskPriorityHeap,
                    HeadlessTaskCapacity,
                    out _))
            {
                return true;
            }

            s_DroneTaskPriorityHeapHandle = default;
            return false;
        }

        private static void ConsiderTaskCandidate(
            in RepairTaskCandidate candidate,
            NativeArray<int> taskClaimCounts,
            ref RepairTaskCandidate bestTask,
            ref bool hasBestTask)
        {
            if (candidate.Module == null ||
                candidate.ModuleIndex < 0 ||
                !taskClaimCounts.IsCreated ||
                candidate.ModuleIndex >= taskClaimCounts.Length ||
                taskClaimCounts[candidate.ModuleIndex] >= DefaultMaxClaimsPerTarget)
            {
                return;
            }

            if (hasBestTask && candidate.Score <= bestTask.Score)
                return;

            bestTask = candidate;
            hasBestTask = true;
        }

        private static void TryPushTaskPriorityCandidate(
            ref DroneTaskNativeMinHeap heap,
            in RepairTaskCandidate candidate,
            NativeArray<int> taskClaimCounts)
        {
            if (!heap.Nodes.IsCreated ||
                candidate.Module == null ||
                candidate.ModuleIndex < 0 ||
                !taskClaimCounts.IsCreated ||
                candidate.ModuleIndex >= taskClaimCounts.Length ||
                taskClaimCounts[candidate.ModuleIndex] >= DefaultMaxClaimsPerTarget)
            {
                return;
            }

            if (!TryResolveAupDoubleFromRuntimeOrigin(candidate.Position, out double3 targetAup))
                return;

            DroneAssignmentTaskDTO dto = new DroneAssignmentTaskDTO
            {
                TargetAup = targetAup,
                LocalPosition = (float3)(candidate.Position),
                Priority = ResolveTaskPriority(candidate.Kind),
                Score = candidate.Score,
                CriticalityWeight = candidate.CriticalityWeight,
                Radius = candidate.Radius,
                ModuleIndex = candidate.ModuleIndex,
                TaskKind = (int)candidate.Kind,
                Reserved0 = 0u
            };
            if (!heap.TryPush(in dto) && s_SignalPushDropCount < int.MaxValue)
                s_SignalPushDropCount++;
        }

        private static bool TryResolvePriorityHeapTask(
            ref DroneTaskNativeMinHeap heap,
            ILogisticsService manager,
            NativeArray<int> taskClaimCounts,
            out RepairTaskCandidate candidate)
        {
            candidate = default;
            if (manager == null)
                return false;

            while (heap.TryPop(out DroneAssignmentTaskDTO dto))
            {
                if (dto.ModuleIndex < 0 ||
                    !taskClaimCounts.IsCreated ||
                    dto.ModuleIndex >= taskClaimCounts.Length ||
                    taskClaimCounts[dto.ModuleIndex] >= DefaultMaxClaimsPerTarget)
                {
                    continue;
                }

                BaseModule module = manager.GetSpawnedBaseModuleAt(dto.ModuleIndex);
                if (module == null || !module.gameObject.activeInHierarchy)
                    continue;

                DroneFleetTaskKind kind = (DroneFleetTaskKind)dto.TaskKind;
                if (kind == DroneFleetTaskKind.None)
                    continue;

                candidate = new RepairTaskCandidate
                {
                    Kind = kind,
                    Module = module,
                    ModuleIndex = dto.ModuleIndex,
                    Position = ToVector3(dto.LocalPosition),
                    Radius = dto.Radius,
                    Score = dto.Score,
                    CriticalityWeight = dto.CriticalityWeight
                };
                return true;
            }

            return false;
        }

        private static float ResolveTaskPriority(DroneFleetTaskKind kind)
        {
            if (kind == DroneFleetTaskKind.RepairModule)
                return 1f;

            if (kind == DroneFleetTaskKind.CutParasite)
                return 10f;

            if (kind == DroneFleetTaskKind.MineNode)
                return 10f;

            return 1024f;
        }

        private static void ClearClaimCounts(
            int moduleCount,
            NativeArray<int> taskClaimCounts)
        {
            for (int i = 0; i < moduleCount; i++)
                taskClaimCounts[i] = 0;
        }

        private static void RebuildActiveClaimCounts(
            ILogisticsService manager,
            int moduleCount,
            NativeArray<int> taskClaimCounts)
        {
            if (manager == null)
                return;

            if (s_DroneSlotDroneIds != null)
            {
                for (int slot = 0; slot < s_DroneSlotDroneIds.Length; slot++)
                {
                    if (s_DroneSlotDroneIds[slot] <= 0)
                        continue;

                    IncrementClaimForTarget(manager, moduleCount, s_TargetModulesByDroneSlot[slot], taskClaimCounts);
                }
            }

            if (s_PendingLaunches != null)
            {
                for (int i = 0; i < s_PendingLaunchCount; i++)
                {
                    if (s_PendingLaunches[i].Active == 0)
                        continue;

                    IncrementClaimForTarget(manager, moduleCount, s_PendingLaunches[i].Task.Module, taskClaimCounts);
                }
            }
        }

        private static void IncrementClaimForTarget(
            ILogisticsService manager,
            int moduleCount,
            BaseModule target,
            NativeArray<int> taskClaimCounts)
        {
            if (manager == null || target == null)
                return;

            for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
            {
                BaseModule module = manager.GetSpawnedBaseModuleAt(moduleIndex);
                if (module == null || !ReferenceEquals(module, target))
                {
                    continue;
                }

                taskClaimCounts[moduleIndex] = taskClaimCounts[moduleIndex] + 1;
                break;
            }
        }

        private static void PublishSnapshot()
        {
            TryPublishSnapshot();
        }

        private static bool TryPublishSnapshot()
        {
            int activeHubCount = 0;
            int dockedStasisSlotCount = s_HeadlessStasisSlotCount;
            int hubCount = Mathf.Min(RepairDroneHub.ActiveHubCount, MaxMainThreadHubScanCount);
            for (int i = 0; i < hubCount; i++)
            {
                RepairDroneHub hub = RepairDroneHub.GetActiveHubAt(i);
                if (hub == null || !hub.isActiveAndEnabled)
                    continue;

                activeHubCount++;
                dockedStasisSlotCount += hub.ResolveDockedStasisSlotCount();
            }

            int activeDroneCount = CountManagedHeadlessDrones();
            int assignedTaskCount = activeDroneCount;

            HectonDroneFleetSnapshot nextSnapshot = new HectonDroneFleetSnapshot(
                activeHubCount,
                activeDroneCount,
                assignedTaskCount,
                dockedStasisSlotCount,
                s_DestroyedDroneCount,
                IsEmergencyOverclockActive,
                s_EmergencyLevel,
                s_LastFleetStatusSnapshot.AverageBattery,
                s_LastFleetStatusSnapshot.SolderReserve,
                s_LastFleetStatusSnapshot.HostileUnits,
                s_LogicLeechHijackCount);

            if (AreSnapshotsEqual(in s_LastSnapshot, in nextSnapshot))
                return true;

            s_LastSnapshot = nextSnapshot;
            return HectonDroneFleetEvents.TryRaiseSnapshotUpdated(in nextSnapshot);
        }

        private static bool AreSnapshotsEqual(in HectonDroneFleetSnapshot a, in HectonDroneFleetSnapshot b)
        {
            return a.ActiveHubCount == b.ActiveHubCount &&
                   a.ActiveDroneCount == b.ActiveDroneCount &&
                   a.AssignedTaskCount == b.AssignedTaskCount &&
                   a.DockedStasisSlotCount == b.DockedStasisSlotCount &&
                   a.DestroyedDroneCount == b.DestroyedDroneCount &&
                   a.EmergencyOverclockActive == b.EmergencyOverclockActive &&
                   a.EmergencyLevel == b.EmergencyLevel &&
                   Mathf.Approximately(a.AverageBatteryPercent, b.AverageBatteryPercent) &&
                   a.SolderReserve == b.SolderReserve &&
                   a.HostileDroneCount == b.HostileDroneCount &&
                   a.LogicLeechHijackCount == b.LogicLeechHijackCount;
        }

        private static int GetRuntimeId(Component component)
        {
            return component == null
                ? 0
                : unchecked((int)EntityId.ToULong(component.GetEntityId()));
        }

        private static uint ComputeDroneTaskHash(DroneFleetTaskKind kind, int primaryId, int secondaryId)
        {
            return math.hash(new uint3(
                (uint)math.max(0, (int)kind),
                (uint)math.max(0, primaryId),
                (uint)math.max(0, secondaryId)));
        }

        private static double3 ResolveDroneRenderReferenceAup()
        {
            if (TryResolvePlayerAup(out double3 playerAup))
                return playerAup;

            if (TryResolveFormationAnchor(out Vector3 formationAnchor) &&
                TryResolveAupDoubleFromRuntimeOrigin(formationAnchor, out double3 formationAup))
            {
                return formationAup;
            }

            RepairDroneHub hub = RepairDroneHub.GetActiveHubAt(0);
            if (hub != null)
            {
                AbsoluteUniversePosition dockAup = hub.DockAup;
                if (dockAup.IsFinite())
                    return dockAup.ToAbsoluteDouble3();
            }

            if (s_CachedPlayerRuntime != null)
                return double3.zero;

            return RuntimeOriginRoute.CurrentRuntimeOriginAup().ToAbsoluteDouble3();
        }

        private static bool TryResolvePlayerAup(out double3 playerAup)
        {
            playerAup = default;
            IPlayerRuntimeContext playerContext = s_CachedPlayerRuntime;
            if (playerContext == null)
                return false;

            if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                (snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                snapshot.Aup.IsFinite())
            {
                playerAup = snapshot.Aup.ToAbsoluteDouble3();
                return math.all(math.isfinite(playerAup));
            }

            if (!playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) ||
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u ||
                !movementState.PredictedAup.IsFinite())
            {
                return false;
            }

            playerAup = movementState.PredictedAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(playerAup));
        }

        private static bool TryResolveDroneTargetAup(in HeadlessDroneState drone, BaseModule target, out double3 targetAup)
        {
            targetAup = default;
            if (IsFiniteDouble3(drone.TargetAup))
            {
                targetAup = drone.TargetAup;
                return true;
            }

            if (target == null)
                return false;

            return TryResolveAupDoubleFromRuntimeOrigin(target.transform.position, out targetAup);
        }

        private static bool TryResolveAbsoluteAupFromRuntimeOrigin(float3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            return TryResolveAbsoluteAupFromRuntimeOrigin(ToVector3(runtimePosition), out aup);
        }

        private static bool TryResolveAbsoluteAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!IsFiniteVector(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return aup.IsFinite();
        }

        private static bool TryResolveAupDoubleFromRuntimeOrigin(float3 runtimePosition, out double3 aup)
        {
            aup = default;
            if (!IsFiniteFloat3(runtimePosition))
                return false;

            return TryResolveAupDoubleFromRuntimeOrigin(ToVector3(runtimePosition), out aup);
        }

        private static bool TryResolveAupDoubleFromRuntimeOrigin(Vector3 runtimePosition, out double3 aup)
        {
            aup = default;
            if (!TryResolveAbsoluteAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition absoluteAup))
                return false;

            aup = absoluteAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(aup));
        }

        private static Vector3 ResolveDroneRenderReferencePosition()
        {
            Camera camera = Camera.current;
            if (camera != null)
                return camera.transform.position;

            if (TryResolvePlayerPosition(out Vector3 playerPosition))
                return playerPosition;

            if (TryResolveFormationAnchor(out Vector3 formationAnchor))
                return formationAnchor;

            RepairDroneHub hub = RepairDroneHub.GetActiveHubAt(0);
            return hub != null ? hub.DockPosition : Vector3.zero;
        }

        private static float3 SafeCastToFloat3(double3 value)
        {
            if (!IsFiniteDouble3(value) || math.any(math.abs(value) > (double)float.MaxValue))
                return new float3(float.NaN);

            return new float3((float)value.x, (float)value.y, (float)value.z);
        }

        private static bool TryResolveRuntimeFloat3AupDelta(in AbsoluteUniversePosition position, out float3 runtimePosition)
        {
            runtimePosition = default;
            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!AbsoluteUniversePosition.IsFinite(in position) ||
                !originAup.IsFinite())
            {
                return false;
            }

            double3 localDelta = position.ToAbsoluteDouble3() - originAup.ToAbsoluteDouble3();
            if (!math.all(math.isfinite(localDelta)))
                return false;

            runtimePosition = SafeCastToFloat3(localDelta);
            return IsFiniteFloat3(runtimePosition);
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        private static bool IsFiniteFloat3(float3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        private static bool IsFiniteDouble3(double3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        private static float3 ResolveForward(quaternion rotation)
        {
            return NormalizeOrFallback(math.mul(rotation, new float3(0f, 0f, 1f)), new float3(0f, 0f, 1f));
        }

        private static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.0001f)
            {
                float fallbackLengthSq = math.lengthsq(fallback);
                return IsFiniteFloat3(fallback) && math.isfinite(fallbackLengthSq) && fallbackLengthSq > 0.0001f
                    ? fallback * math.rsqrt(fallbackLengthSq)
                    : new float3(0f, 0f, 1f);
            }

            return value * math.rsqrt(lengthSq);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static quaternion ToQuaternion(Quaternion value)
        {
            return new quaternion(value.x, value.y, value.z, value.w);
        }
    }

    public static partial class DroneFleetAutomationFacade
    {
        public const int MaxDebugRoutes = 64;

        public static bool TryGetTuningConstants(out DroneFleetTuningConstants constants)
        {
            return DroneFleetManager.TryGetDroneFleetTuningConstants(out constants);
        }

        public static void ApplyTuningConstants(in DroneFleetTuningConstants constants)
        {
            DroneFleetManager.ApplyDroneFleetTuningConstants(in constants);
        }

        public static bool TryGetStats(out DroneFleetAutomationStats stats)
        {
            return DroneFleetManager.TryGetDroneFleetAutomationStats(out stats);
        }

        public static int CopyDebugRoutes(DroneFleetDebugRoute[] buffer)
        {
            return DroneFleetManager.CopyDroneFleetDebugRoutes(buffer);
        }

        public static bool TryGetConfiguredDroneBoneTableInfo(
            out uint droneId,
            out uint bakeHash,
            out float qualityWeight,
            out int jointCount)
        {
            return DroneFleetManager.TryGetConfiguredDroneBoneTableInfo(
                out droneId,
                out bakeHash,
                out qualityWeight,
                out jointCount);
        }

        public static bool TryCopyConfiguredDroneBoneJointTable(
            NativeArray<DroneBoneJointRuntimeData> destination,
            out int jointCount)
        {
            return DroneFleetManager.TryCopyConfiguredDroneBoneJointTable(destination, out jointCount);
        }

        public static bool TryGetConfiguredDroneAttachmentTableInfo(
            out uint droneId,
            out uint bakeHash,
            out float qualityWeight,
            out int attachmentCount)
        {
            return DroneFleetManager.TryGetConfiguredDroneAttachmentTableInfo(
                out droneId,
                out bakeHash,
                out qualityWeight,
                out attachmentCount);
        }

        public static bool TryCopyConfiguredDroneAttachmentTable(
            NativeArray<DroneAttachmentRuntimeData> destination,
            out int attachmentCount)
        {
            return DroneFleetManager.TryCopyConfiguredDroneAttachmentTable(destination, out attachmentCount);
        }

#if UNITY_EDITOR
        public static bool TryApplyDroneSpecsCsv(string path, out int keysApplied)
        {
            return DroneFleetManager.TryApplyDroneSpecsCsv(path, out keysApplied);
        }

        public static bool TryAutoApplyDroneSpecsCsv(out int keysApplied)
        {
            return DroneFleetManager.TryAutoApplyDroneSpecsCsv(out keysApplied);
        }
#endif
    }
}
