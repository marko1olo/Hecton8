using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using CoreTetherFiredSignal = Hecton8.Core.Contracts.Signals.TetherFiredSignal;

namespace Hecton8.Core.Contracts.Signals
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 144)]
    public struct TetherTensionSignal : ISignal
    {
        public AbsoluteUniversePosition AnchorAup;
        public AbsoluteUniversePosition PayloadAup;
        public float3 DirectionToPayload;
        public uint TetherId;
        public uint FrameIndex;
        public float TensionForce;
        public float SnapThreshold;
        public float Tension01;
        public float ReactiveVfx01;
        public ushort NodeCount;
        public byte Flags;
        public byte Reserved;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 72)]
    public struct TetherSnappedSignal : ISignal
    {
        public AbsoluteUniversePosition SnapAup;
        public uint TetherId;
        public uint FrameIndex;
        public float PeakTension;
        public float SnapThreshold;
        public float Severity01;
        public ushort NodeCount;
        public byte Reason;
        public byte Flags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 40)]
    public struct TetherFiredSignal : ISignal
    {
        public int ManagerInstanceId;
        public int OwnerInstanceId;
        public int PayloadBodyInstanceId;
        public int PayloadColliderInstanceId;
        public int RequestSlot;
        public uint RequestVersion;
        public uint FrameIndex;
        public float InitialDistance;
        public uint Flags;
        public uint Reserved;
    }

}

namespace Hecton8.Physics
{
    public static class TetherSignals
    {
        private const int FireSignalCapacity = 16;
        private const uint FireSignalMaxAgeFrames = 8u;
        // COLD ALLOC: TetherFireRequest[16] - managed resolver sidecar for fire signals - owner: TetherSignals
        private static readonly TetherFireRequest[] _fireRequests = new TetherFireRequest[FireSignalCapacity];
        private static int _fireRequestCount;
        private static int _nextFireRequestSlot;
        private static uint _nextFireRequestVersion;
        private static int _snapSnapshotReadFrame = -1;
        private static int _snapSnapshotReadCursor;
        private static bool _initialized;

        internal struct TetherFireRequest
        {
            public TetherManager Manager;
            public HeavyTowWinch Owner;
            public HectonPlayerMotor PlayerMotor;
            public Rigidbody PlayerBody;
            public Rigidbody PayloadBody;
            public Collider PayloadCollider;
            public float InitialDistance;
            public uint Version;
            public uint FrameIndex;
            public bool Active;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForDomainReload()
        {
            for (int i = 0; i < _fireRequests.Length; i++)
                _fireRequests[i] = default;

            _fireRequestCount = 0;
            _nextFireRequestSlot = 0;
            _nextFireRequestVersion = 0u;
            _snapSnapshotReadFrame = -1;
            _snapSnapshotReadCursor = 0;
            _initialized = false;
        }

        public static void EnsureInitialized()
        {
            if (_initialized)
                return;

            GlobalSignals.InitializeAllQueues();
            SignalBus<CoreTetherFiredSignal>.EnsureInitialized();
            _initialized = true;
        }

        public static bool PublishFire(
            TetherManager manager,
            HeavyTowWinch owner,
            HectonPlayerMotor playerMotor,
            Rigidbody playerBody,
            Rigidbody payloadBody,
            Collider payloadCollider,
            float initialDistance)
        {
            if (manager == null || owner == null || playerBody == null || payloadBody == null || payloadCollider == null)
                return false;

            EnsureInitialized();
            uint currentFrame = (uint)Time.frameCount;
            PruneExpiredFireRequests(currentFrame);
            if (_fireRequestCount >= FireSignalCapacity)
                return false;

            int slot = ReserveFireRequestSlot();
            if (slot < 0)
                return false;

            uint version = ++_nextFireRequestVersion;
            if (version == 0u)
                version = ++_nextFireRequestVersion;

            _fireRequests[slot] = new TetherFireRequest
            {
                Manager = manager,
                Owner = owner,
                PlayerMotor = playerMotor,
                PlayerBody = playerBody,
                PayloadBody = payloadBody,
                PayloadCollider = payloadCollider,
                InitialDistance = initialDistance,
                Version = version,
                FrameIndex = currentFrame,
                Active = true
            };

            CoreTetherFiredSignal signal = new CoreTetherFiredSignal
            {
                ManagerInstanceId = ResolveStableObjectId(manager),
                OwnerInstanceId = ResolveStableObjectId(owner),
                PayloadBodyInstanceId = ResolveStableObjectId(payloadBody),
                PayloadColliderInstanceId = ResolveStableObjectId(payloadCollider),
                RequestSlot = slot,
                RequestVersion = version,
                FrameIndex = currentFrame,
                InitialDistance = initialDistance,
                Flags = 0
            };

            SignalBus<CoreTetherFiredSignal>.Push(in signal);
            _fireRequestCount++;
            return true;
        }

        public static bool PublishSnap(in TetherSnappedSignal signal)
        {
            EnsureInitialized();
            SignalBus<TetherSnappedSignal>.Push(in signal);
            return true;
        }

        public static void PublishTension(in TetherTensionSignal signal)
        {
            EnsureInitialized();
            SignalBus<TetherTensionSignal>.Push(in signal);
        }

        public static bool TryDequeueSnap(out TetherSnappedSignal signal)
        {
            EnsureInitialized();
            int currentFrame = Time.frameCount;
            if (_snapSnapshotReadFrame != currentFrame)
            {
                _snapSnapshotReadFrame = currentFrame;
                _snapSnapshotReadCursor = 0;
            }

            ReadOnlySpan<TetherSnappedSignal> snapshot = SignalBus<TetherSnappedSignal>.GetFrameSnapshot();
            if (_snapSnapshotReadCursor >= snapshot.Length)
            {
                signal = default;
                return false;
            }

            signal = snapshot[_snapSnapshotReadCursor++];
            return true;
        }

        internal static bool TryConsumeFireForManager(TetherManager manager, out TetherFireRequest request)
        {
            request = default;
            if (manager == null || _fireRequestCount <= 0)
                return false;

            EnsureInitialized();
            uint currentFrame = (uint)Time.frameCount;
            PruneExpiredFireRequests(currentFrame);
            if (_fireRequestCount <= 0)
                return false;

            int managerId = ResolveStableObjectId(manager);
            if (TryConsumeFireFromSnapshot(manager, managerId, out request))
                return true;

            return TryConsumeImmediateFireRequest(manager, managerId, currentFrame, out request);
        }

        private static int ResolveStableObjectId(UnityEngine.Object unityObject)
        {
            return unityObject != null ? unchecked((int)EntityId.ToULong(unityObject.GetEntityId())) : 0;
        }

        private static int ReserveFireRequestSlot()
        {
            for (int i = 0; i < FireSignalCapacity; i++)
            {
                int slot = (_nextFireRequestSlot + i) % FireSignalCapacity;
                if (_fireRequests[slot].Active)
                    continue;

                _nextFireRequestSlot = (slot + 1) % FireSignalCapacity;
                return slot;
            }

            return -1;
        }

        private static void PruneExpiredFireRequests(uint currentFrame)
        {
            if (_fireRequestCount <= 0)
                return;

            for (int i = 0; i < _fireRequests.Length; i++)
            {
                TetherFireRequest request = _fireRequests[i];
                if (request.Active && !IsFireRequestLive(in request, currentFrame))
                    ClearFireRequestSlot(i);
            }
        }

        private static bool IsFireRequestLive(in TetherFireRequest request, uint currentFrame)
        {
            return request.Active &&
                   currentFrame - request.FrameIndex <= FireSignalMaxAgeFrames;
        }

        private static bool IsFireSignalLive(in CoreTetherFiredSignal signal, uint currentFrame)
        {
            int slot = signal.RequestSlot;
            if ((uint)slot >= (uint)_fireRequests.Length)
                return false;

            TetherFireRequest request = _fireRequests[slot];
            return request.Active &&
                   request.Version == signal.RequestVersion &&
                   currentFrame - signal.FrameIndex <= FireSignalMaxAgeFrames;
        }

        private static void ClearFireRequestSlot(int slot)
        {
            if ((uint)slot >= (uint)_fireRequests.Length)
                return;

            if (_fireRequests[slot].Active && _fireRequestCount > 0)
                _fireRequestCount--;

            _fireRequests[slot] = default;
        }

        private static bool TryConsumeFireFromSnapshot(
            TetherManager manager,
            int managerId,
            out TetherFireRequest request)
        {
            request = default;
            ReadOnlySpan<CoreTetherFiredSignal> snapshot = SignalBus<CoreTetherFiredSignal>.GetFrameSnapshot();
            uint currentFrame = (uint)Time.frameCount;
            for (int i = 0; i < snapshot.Length; i++)
            {
                CoreTetherFiredSignal signal = snapshot[i];
                if (signal.ManagerInstanceId != managerId || !IsFireSignalLive(in signal, currentFrame))
                    continue;

                if (TryConsumeFireRequest(in signal, manager, out request))
                    return true;
            }

            return false;
        }

        private static bool TryConsumeImmediateFireRequest(
            TetherManager manager,
            int managerId,
            uint currentFrame,
            out TetherFireRequest request)
        {
            request = default;
            for (int slot = 0; slot < _fireRequests.Length; slot++)
            {
                TetherFireRequest candidate = _fireRequests[slot];
                if (!candidate.Active ||
                    !ReferenceEquals(candidate.Manager, manager) ||
                    currentFrame - candidate.FrameIndex > FireSignalMaxAgeFrames)
                {
                    continue;
                }

                CoreTetherFiredSignal signal = new CoreTetherFiredSignal
                {
                    ManagerInstanceId = managerId,
                    OwnerInstanceId = ResolveStableObjectId(candidate.Owner),
                    PayloadBodyInstanceId = ResolveStableObjectId(candidate.PayloadBody),
                    PayloadColliderInstanceId = ResolveStableObjectId(candidate.PayloadCollider),
                    RequestSlot = slot,
                    RequestVersion = candidate.Version,
                    FrameIndex = candidate.FrameIndex,
                    InitialDistance = candidate.InitialDistance,
                    Flags = 0
                };

                if (TryConsumeFireRequest(in signal, manager, out request))
                    return true;
            }

            return false;
        }

        private static bool TryConsumeFireRequest(
            in CoreTetherFiredSignal signal,
            TetherManager manager,
            out TetherFireRequest request)
        {
            request = default;
            int slot = signal.RequestSlot;
            if ((uint)slot >= (uint)_fireRequests.Length)
                return false;

            TetherFireRequest candidate = _fireRequests[slot];
            if (!candidate.Active || candidate.Version != signal.RequestVersion)
                return false;

            if (!ReferenceEquals(candidate.Manager, manager))
            {
                ClearFireRequestSlot(slot);
                return false;
            }

            request = candidate;
            ClearFireRequestSlot(slot);
            return true;
        }
    }
}
