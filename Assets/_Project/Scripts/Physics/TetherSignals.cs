using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Physics
{
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 72)]
    public struct TetherSnappedSignal
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

    public static class TetherSignals
    {
        private const int SnapSignalCapacity = 64;
        private const int FireSignalCapacity = 16;
        private const string NativeMemoryOwner = nameof(TetherSignals);
        // COLD ALLOC: TetherFireRequest[16] - managed resolver sidecar for fire signals - owner: TetherSignals
        private static readonly TetherFireRequest[] _fireRequests = new TetherFireRequest[FireSignalCapacity];
        private static NativeQueue<TetherSnappedSignal> _snappedSignals;
        private static NativeQueue<TetherFiredSignal> _firedSignals;
        private static int _fireSignalCount;
        private static int _nextFireRequestSlot;
        private static uint _nextFireRequestVersion;
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
            public bool Active;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForDomainReload()
        {
            if (_snappedSignals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(NativeMemoryOwner, nameof(_snappedSignals));
                _snappedSignals.Dispose();
            }

            if (_firedSignals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(NativeMemoryOwner, nameof(_firedSignals));
                _firedSignals.Dispose();
            }

            _snappedSignals = default;
            _firedSignals = default;
            for (int i = 0; i < _fireRequests.Length; i++)
                _fireRequests[i] = default;

            _fireSignalCount = 0;
            _nextFireRequestSlot = 0;
            _nextFireRequestVersion = 0u;
            _initialized = false;
        }

        public static void EnsureInitialized()
        {
            if (_initialized && _snappedSignals.IsCreated)
                return;

            if (!_snappedSignals.IsCreated)
            {
                _snappedSignals = new NativeQueue<TetherSnappedSignal>(Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeQueue(
                    _snappedSignals,
                    SnapSignalCapacity,
                    NativeMemoryOwner,
                    nameof(_snappedSignals),
                    NativeAllocationLifetime.Session);
            }

            if (!_firedSignals.IsCreated)
            {
                _firedSignals = new NativeQueue<TetherFiredSignal>(Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeQueue(
                    _firedSignals,
                    FireSignalCapacity,
                    NativeMemoryOwner,
                    nameof(_firedSignals),
                    NativeAllocationLifetime.Session);
            }

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
            if (_fireSignalCount >= FireSignalCapacity)
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
                Active = true
            };

            TetherFiredSignal signal = new TetherFiredSignal
            {
                ManagerInstanceId = ResolveStableObjectId(manager),
                OwnerInstanceId = ResolveStableObjectId(owner),
                PayloadBodyInstanceId = ResolveStableObjectId(payloadBody),
                PayloadColliderInstanceId = ResolveStableObjectId(payloadCollider),
                RequestSlot = slot,
                RequestVersion = version,
                FrameIndex = (uint)Time.frameCount,
                InitialDistance = initialDistance,
                Flags = 0
            };

            _firedSignals.Enqueue(signal);
            _fireSignalCount++;
            return true;
        }

        public static void PublishSnap(in TetherSnappedSignal signal)
        {
            EnsureInitialized();
            _snappedSignals.Enqueue(signal);
        }

        public static bool TryDequeueSnap(out TetherSnappedSignal signal)
        {
            EnsureInitialized();
            return _snappedSignals.TryDequeue(out signal);
        }

        internal static bool TryConsumeFireForManager(TetherManager manager, out TetherFireRequest request)
        {
            request = default;
            if (manager == null || _fireSignalCount <= 0)
                return false;

            EnsureInitialized();
            int managerId = ResolveStableObjectId(manager);
            int scanCount = _fireSignalCount;
            for (int i = 0; i < scanCount; i++)
            {
                if (!_firedSignals.TryDequeue(out TetherFiredSignal signal))
                    break;

                _fireSignalCount--;
                if (signal.ManagerInstanceId == managerId)
                {
                    if (TryConsumeFireRequest(in signal, manager, out request))
                        return true;

                    continue;
                }

                _firedSignals.Enqueue(signal);
                _fireSignalCount++;
            }

            return false;
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

        private static bool TryConsumeFireRequest(
            in TetherFiredSignal signal,
            TetherManager manager,
            out TetherFireRequest request)
        {
            request = default;
            int slot = signal.RequestSlot;
            if ((uint)slot >= (uint)_fireRequests.Length)
                return false;

            TetherFireRequest candidate = _fireRequests[slot];
            if (!candidate.Active ||
                candidate.Version != signal.RequestVersion ||
                !ReferenceEquals(candidate.Manager, manager))
            {
                return false;
            }

            request = candidate;
            _fireRequests[slot] = default;
            return true;
        }
    }
}
