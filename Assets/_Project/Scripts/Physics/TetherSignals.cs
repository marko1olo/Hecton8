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

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 80)]
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
        public ulong ReservedPadding;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]
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
        public ulong ReservedPadding;
    }

}

namespace Hecton8.Physics
{
    public static class TetherSignals
    {
        private static uint _snapSnapshotReadFrameIndex;
        private static int _snapSnapshotReadLength;
        private static int _snapSnapshotReadCursor;
        private static bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForDomainReload()
        {
            _snapSnapshotReadFrameIndex = 0u;
            _snapSnapshotReadLength = 0;
            _snapSnapshotReadCursor = 0;
            _initialized = false;
        }

        public static void EnsureInitialized()
        {
            if (_initialized)
                return;

            SignalBus<TetherSnappedSignal>.EnsureInitialized();
            SignalBus<TetherTensionSignal>.EnsureInitialized();
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
            float initialDistance,
            uint frameIndex)
        {
            if (manager == null || owner == null || playerBody == null || payloadBody == null || payloadCollider == null)
                return false;

            EnsureInitialized();

            CoreTetherFiredSignal signal = new CoreTetherFiredSignal
            {
                ManagerInstanceId = ResolveStableObjectId(manager),
                OwnerInstanceId = ResolveStableObjectId(owner),
                PayloadBodyInstanceId = ResolveStableObjectId(payloadBody),
                PayloadColliderInstanceId = ResolveStableObjectId(payloadCollider),
                RequestSlot = -1,
                RequestVersion = 0u,
                FrameIndex = frameIndex,
                InitialDistance = initialDistance,
                Flags = 0
            };

            SignalBus<CoreTetherFiredSignal>.Push(in signal);
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
            ReadOnlySpan<TetherSnappedSignal> snapshot = SignalBus<TetherSnappedSignal>.GetFrameSnapshot();
            if (snapshot.Length <= 0)
            {
                _snapSnapshotReadLength = 0;
                _snapSnapshotReadCursor = 0;
                signal = default;
                return false;
            }

            uint snapshotFrameIndex = snapshot[0].FrameIndex;
            if (_snapSnapshotReadFrameIndex != snapshotFrameIndex || _snapSnapshotReadLength != snapshot.Length)
            {
                _snapSnapshotReadFrameIndex = snapshotFrameIndex;
                _snapSnapshotReadLength = snapshot.Length;
                _snapSnapshotReadCursor = 0;
            }

            if (_snapSnapshotReadCursor >= snapshot.Length)
            {
                signal = default;
                return false;
            }

            signal = snapshot[_snapSnapshotReadCursor++];
            return true;
        }

        private static int ResolveStableObjectId(UnityEngine.Object unityObject)
        {
            return unityObject != null ? unchecked((int)EntityId.ToULong(unityObject.GetEntityId())) : 0;
        }
    }
}
