using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using UnityEngine;

namespace Hecton8.World
{
    public static class TerrainChunkGeneratedEvents
    {
        private const int Capacity = 32;
        private const int SurvivalCapacity = 4;
        private const uint LaneHash = 0x54434753u; // TCGS

        private static int _pendingCount;
        private static int _droppedCount;
        private static int _rejectedCount;
        private static uint _lastRejectedTerrainHash;
        private static bool _configured;

        public static int PendingCount => _pendingCount;
        public static int DebugCapacity => Capacity;
        public static int DebugDroppedCount => _droppedCount;
        public static int DebugRejectedCount => _rejectedCount;
        public static uint DebugLastRejectedTerrainHash => _lastRejectedTerrainHash;

        public static bool TryPublish(in TerrainChunkGeneratedSignal signal)
        {
            if (!TerrainChunkGeneratedSignal.IsValid(in signal))
            {
                _rejectedCount++;
                _lastRejectedTerrainHash = signal.TerrainEntityHash;
                return false;
            }

            EnsureInitialized();
            if (_pendingCount >= Capacity)
            {
                _droppedCount++;
                _rejectedCount++;
                _lastRejectedTerrainHash = signal.TerrainEntityHash;
                return false;
            }

            if (!SignalBus<TerrainChunkGeneratedSignal>.TryPush(in signal))
            {
                _rejectedCount++;
                _lastRejectedTerrainHash = signal.TerrainEntityHash;
                return false;
            }

            _pendingCount++;
            return true;
        }

        public static bool TryDequeue(out TerrainChunkGeneratedSignal signal)
        {
            EnsureInitialized();
            bool dequeued = SignalBus<TerrainChunkGeneratedSignal>.TryConsumeFrame(out signal);
            if (dequeued)
            {
                if (_pendingCount > 0)
                    _pendingCount--;

                return true;
            }

            _pendingCount = 0;
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            DisposeAll();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterQuitHook()
        {
            Application.quitting -= DisposeAll;
            Application.quitting += DisposeAll;
        }

        private static void EnsureInitialized()
        {
            if (!_configured)
            {
                SignalBus<TerrainChunkGeneratedSignal>.Configure(
                    Capacity,
                    maxFrameSignals: Capacity,
                    lowTierFrameSignals: SurvivalCapacity,
                    laneHash: LaneHash);
                SignalBus<TerrainChunkGeneratedSignal>.EnsureInitialized();
                _configured = true;
            }
        }

        private static void DisposeAll()
        {
            SignalBus<TerrainChunkGeneratedSignal>.Dispose();
            _pendingCount = 0;
            _droppedCount = 0;
            _rejectedCount = 0;
            _lastRejectedTerrainHash = 0u;
            _configured = false;
        }
    }
}
