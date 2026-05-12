using System.Runtime.InteropServices;
using Hecton8.Core;
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
        private const string NativeMemoryOwner = nameof(TetherSignals);
        private static NativeQueue<TetherSnappedSignal> _snappedSignals;
        private static bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForDomainReload()
        {
            if (_snappedSignals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(NativeMemoryOwner, nameof(_snappedSignals));
                _snappedSignals.Dispose();
            }

            _snappedSignals = default;
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

            _initialized = true;
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
    }
}
