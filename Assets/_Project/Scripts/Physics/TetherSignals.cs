using System;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;
using UnityEngine;
using CoreTetherFiredSignal = Hecton8.Core.Contracts.Signals.TetherFiredSignal;

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

        [Obsolete("Use TryPublishFire(...) so SignalBus enqueue refusal is visible.", true)]
        public static bool PublishFire(
            int managerInstanceId,
            int ownerInstanceId,
            int payloadBodyInstanceId,
            int payloadColliderInstanceId,
            float initialDistance,
            uint frameIndex)
        {
            return TryPublishFire(
                managerInstanceId,
                ownerInstanceId,
                payloadBodyInstanceId,
                payloadColliderInstanceId,
                initialDistance,
                frameIndex);
        }

        public static bool TryPublishFire(
            int managerInstanceId,
            int ownerInstanceId,
            int payloadBodyInstanceId,
            int payloadColliderInstanceId,
            float initialDistance,
            uint frameIndex)
        {
            if (managerInstanceId == 0 || ownerInstanceId == 0 || payloadBodyInstanceId == 0 || payloadColliderInstanceId == 0)
                return false;

            if (!math.isfinite(initialDistance) || initialDistance < 0f)
                return false;

            EnsureInitialized();

            CoreTetherFiredSignal signal = new CoreTetherFiredSignal
            {
                ManagerInstanceId = managerInstanceId,
                OwnerInstanceId = ownerInstanceId,
                PayloadBodyInstanceId = payloadBodyInstanceId,
                PayloadColliderInstanceId = payloadColliderInstanceId,
                RequestSlot = -1,
                RequestVersion = 0u,
                FrameIndex = frameIndex,
                InitialDistance = math.max(0f, initialDistance),
                Flags = 0
            };

            return SignalBus<CoreTetherFiredSignal>.TryPush(in signal);
        }

        [Obsolete("Use TryPublishSnap(in TetherSnappedSignal) so SignalBus enqueue refusal is visible.", true)]
        public static bool PublishSnap(in TetherSnappedSignal signal)
        {
            return TryPublishSnap(in signal);
        }

        public static bool TryPublishSnap(in TetherSnappedSignal signal)
        {
            EnsureInitialized();
            return SignalBus<TetherSnappedSignal>.TryPush(in signal);
        }

        [Obsolete("Use TryPublishTension(in TetherTensionSignal) so SignalBus enqueue refusal is visible.", true)]
        public static void PublishTension(in TetherTensionSignal signal)
        {
            TryPublishTension(in signal);
        }

        public static bool TryPublishTension(in TetherTensionSignal signal)
        {
            EnsureInitialized();
            return SignalBus<TetherTensionSignal>.TryPush(in signal);
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
    }
}
