using Hecton8.Core;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.World
{
    public static class TerrainChunkGeneratedEvents
    {
        private const int Capacity = 32;
        private const string NativeOwner = nameof(TerrainChunkGeneratedEvents);
        private const string QueueLabel = "TerrainChunkGeneratedSignalQueue";

        private static NativeQueue<TerrainChunkGeneratedSignal> _events;
        private static int _pendingCount;
        private static int _droppedCount;
        private static int _rejectedCount;
        private static uint _lastRejectedTerrainHash;

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
            if (_pendingCount >= Capacity && _events.TryDequeue(out _))
            {
                _pendingCount--;
                _droppedCount++;
            }

            if (_pendingCount >= Capacity)
            {
                _rejectedCount++;
                _lastRejectedTerrainHash = signal.TerrainEntityHash;
                return false;
            }

            _events.Enqueue(signal);
            _pendingCount++;
            return true;
        }

        public static bool TryDequeue(out TerrainChunkGeneratedSignal signal)
        {
            if (!_events.IsCreated)
            {
                signal = default;
                return false;
            }

            bool dequeued = _events.TryDequeue(out signal);
            if (dequeued && _pendingCount > 0)
                _pendingCount--;

            return dequeued;
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
            if (_events.IsCreated)
                return;

            _events = new NativeQueue<TerrainChunkGeneratedSignal>(Allocator.Persistent);
            NativeMemorySentinel.RegisterNativeQueue(
                _events,
                Capacity,
                NativeOwner,
                QueueLabel,
                NativeAllocationLifetime.Session);
            PrewarmQueue(ref _events, Capacity);
            _pendingCount = 0;
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

        private static void DisposeAll()
        {
            if (_events.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(NativeOwner, QueueLabel);
                _events.Dispose();
            }

            _events = default;
            _pendingCount = 0;
            _droppedCount = 0;
            _rejectedCount = 0;
            _lastRejectedTerrainHash = 0u;
        }
    }
}
