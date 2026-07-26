using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// R99: lane owner for <see cref="WorldChunkPhysicsBakedSignal"/>.
    ///
    /// A drain-once signal bus alone cannot implement a readiness GATE: the spawner routinely starts asking
    /// after the chunk under it was already baked, and a consumed signal is gone. So this lane keeps a
    /// bounded LATCH of the most recent per-chunk bake states alongside the reactive queue. The latch is a
    /// fixed array — no allocation, no GC, safe to poll from a spawn loop.
    ///
    /// Queries are by world-space XZ, not by chunk index, so a caller cannot disagree with the publisher
    /// about chunk identity (see the note in the signal contract about the three coordinate conventions).
    /// </summary>
    public static class WorldChunkPhysicsBakedEvents
    {
        private static int s_x001DirectSignalPushDropCount_WorldChunkPhysicsBakedEvents;

        private const int Capacity = 32;
        private const int SurvivalCapacity = 4;
        private const uint LaneHash = 0x57435042u; // WCPB

        /// <summary>Bounded latch of recent chunk bake states. Sized well above the residency ring.</summary>
        private const int LatchCapacity = 64;
        private static readonly WorldChunkPhysicsBakedSignal[] _latch = new WorldChunkPhysicsBakedSignal[LatchCapacity];
        private static int _latchCount;
        private static int _latchCursor;

        private static int _pendingCount;
        private static int _droppedCount;
        private static int _rejectedCount;
        private static int _publishedCount;
        private static int _failedCount;
        private static uint _lastRejectedTerrainHash;
        private static bool _configured;

        public static int PendingCount => _pendingCount;
        public static int DebugCapacity => Capacity;
        public static int DebugDroppedCount => _droppedCount;
        public static int DebugRejectedCount => _rejectedCount;
        public static int DebugFailedCount => _failedCount;
        public static uint DebugLastRejectedTerrainHash => _lastRejectedTerrainHash;
        public static int LatchedChunkCount => _latchCount;

        /// <summary>
        /// True once this lane has published at least one signal in this session. Consumers use it to tell
        /// "the physics-bake route is live, so honour the gate" from "no terrain provider in this scene, so
        /// do not block on a signal that will never come".
        /// </summary>
        public static bool IsLaneActive => _publishedCount > 0;

        public static bool TryPublish(in WorldChunkPhysicsBakedSignal signal)
        {
            if (!WorldChunkPhysicsBakedSignal.IsValid(in signal))
            {
                _rejectedCount++;
                _lastRejectedTerrainHash = signal.TerrainEntityHash;
                return false;
            }

            EnsureInitialized();

            // Latch FIRST. The latch is the gate's source of truth and must survive a full reactive queue,
            // otherwise a burst of tile applies would drop exactly the readiness the spawner is waiting on.
            Latch(in signal);
            _publishedCount++;
            if ((signal.Flags & WorldChunkPhysicsBakedSignal.FlagBakeFailed) != 0u)
                _failedCount++;

            if (_pendingCount >= Capacity)
            {
                _droppedCount++;
                return false;
            }

            if (!SignalBus<WorldChunkPhysicsBakedSignal>.TryPushTracked(in signal, ref s_x001DirectSignalPushDropCount_WorldChunkPhysicsBakedEvents))
            {
                _droppedCount++;
                return false;
            }

            _pendingCount++;
            return true;
        }

        public static bool TryDequeue(out WorldChunkPhysicsBakedSignal signal)
        {
            EnsureInitialized();
            bool dequeued = SignalBus<WorldChunkPhysicsBakedSignal>.TryConsumeFrame(out signal);
            if (dequeued)
            {
                if (_pendingCount > 0)
                    _pendingCount--;

                return true;
            }

            _pendingCount = 0;
            return false;
        }

        /// <summary>
        /// Physics readiness for a world-space XZ point. Returns false when no chunk covering the point has
        /// reported yet — the caller must keep waiting, not assume ground exists.
        /// </summary>
        public static bool IsWorldPointPhysicsBaked(float worldX, float worldZ)
        {
            return TryGetWorldPointBakeFlags(worldX, worldZ, out uint flags) &&
                   (flags & WorldChunkPhysicsBakedSignal.FlagColliderActive) != 0u &&
                   (flags & WorldChunkPhysicsBakedSignal.FlagBakeFailed) == 0u;
        }

        /// <summary>
        /// True when a chunk covering the point has reported a TERMINAL FAILURE. The gate must release on
        /// this (degraded spawn) instead of waiting forever.
        /// </summary>
        public static bool IsWorldPointBakeFailed(float worldX, float worldZ)
        {
            return TryGetWorldPointBakeFlags(worldX, worldZ, out uint flags) &&
                   (flags & WorldChunkPhysicsBakedSignal.FlagBakeFailed) != 0u;
        }

        /// <summary>Raw latched flags for the newest chunk covering the point. False when nothing covers it.</summary>
        public static bool TryGetWorldPointBakeFlags(float worldX, float worldZ, out uint flags)
        {
            flags = 0u;
            uint newestFrame = 0u;
            bool found = false;
            int count = _latchCount;
            for (int i = 0; i < count; i++)
            {
                if (!WorldChunkPhysicsBakedSignal.ContainsWorldXZ(in _latch[i], worldX, worldZ))
                    continue;

                // Same footprint can be re-reported (tile move / regenerate); newest frame wins.
                if (found && _latch[i].Frame < newestFrame)
                    continue;

                newestFrame = _latch[i].Frame;
                flags = _latch[i].Flags;
                found = true;
            }

            return found;
        }

        private static void Latch(in WorldChunkPhysicsBakedSignal signal)
        {
            for (int i = 0; i < _latchCount; i++)
            {
                if (_latch[i].TerrainEntityHash != signal.TerrainEntityHash)
                    continue;

                _latch[i] = signal;
                return;
            }

            if (_latchCount < LatchCapacity)
            {
                _latch[_latchCount++] = signal;
                return;
            }

            // Bounded ring overwrite. Losing the oldest entry is correct: it belongs to a chunk that has
            // long since left residency, and the gate only ever asks about the chunk under the player.
            _latch[_latchCursor] = signal;
            _latchCursor++;
            if (_latchCursor >= LatchCapacity)
                _latchCursor = 0;
        }

        /// <summary>Clears latched readiness. Call on world unload/regeneration so stale bakes cannot pass the gate.</summary>
        public static void ClearLatch()
        {
            for (int i = 0; i < LatchCapacity; i++)
                _latch[i] = default;

            _latchCount = 0;
            _latchCursor = 0;
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
                SignalBus<WorldChunkPhysicsBakedSignal>.Configure(
                    Capacity,
                    maxFrameSignals: Capacity,
                    lowTierFrameSignals: SurvivalCapacity,
                    laneHash: LaneHash);
                SignalBus<WorldChunkPhysicsBakedSignal>.EnsureInitialized();
                _configured = true;
            }
        }

        private static void DisposeAll()
        {
            SignalBus<WorldChunkPhysicsBakedSignal>.Dispose();
            ClearLatch();
            _pendingCount = 0;
            _droppedCount = 0;
            _rejectedCount = 0;
            _publishedCount = 0;
            _failedCount = 0;
            _lastRejectedTerrainHash = 0u;
            _configured = false;
        }
    }
}
