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
        /// True once this lane has published at least one signal in this session.
        ///
        /// This is a PUBLISH COUNTER, not a provider census, and it must not be used to decide whether to
        /// honour the readiness gate. An inactive lane is the INITIAL state of every run, not a "no terrain
        /// provider" state, so a consumer that reads `!IsLaneActive` as "nothing will ever publish, skip the
        /// gate" holds the gate wide open across the entire window between scene load and the first tile
        /// apply — precisely the window the Kinematic Arrest Gate exists to cover. Ask
        /// <see cref="IsProviderRegistered"/> instead.
        ///
        /// The property stays because it is still correct for the question it can answer: telemetry that must
        /// separate "the streaming route never ran at all this session" from "it ran and the point failed".
        /// </summary>
        public static bool IsLaneActive => _publishedCount > 0;

        /// <summary>
        /// True when a world provider that publishes on this lane EXISTS in the active scene, whether or not
        /// it has produced anything yet. This is the fact a readiness gate must branch on: it is established
        /// at provider `OnEnable` — `MapMagicRuntimeBridge.cs`:385 registers the terrain provider,
        /// `HectonVoxelEngine.cs`:1234 registers the voxel engine — so it answers correctly from the first
        /// frame, long before either has a baked chunk to broadcast.
        ///
        /// Deliberately `ReferenceEquals` rather than `!= null`. `GlobalRegistry.VoxelEngine` is a
        /// MonoBehaviour, so `!= null` would invoke Unity's overloaded operator and report a DESTROYED but
        /// still-registered provider as ABSENT — opening the gate on a scene that demonstrably has a
        /// provider. Reference identity fails CLOSED instead: a provider that died without unregistering
        /// keeps the gate shut and the consumer's own degradation path resolves it. Normal teardown is clean
        /// either way, because both providers unregister in `OnDisable` (`MapMagicRuntimeBridge.cs`:403,
        /// `HectonVoxelEngine.cs`:3261).
        ///
        /// Pure read per `AGENTS.md`:222 — two static field reads and two reference compares. No allocation,
        /// no scene search, no publish, no registry mutation. This is cold identity, not hot polling: the
        /// spawn gate asks it on a bounded retry cadence, never from a tick or physics path.
        /// </summary>
        public static bool IsProviderRegistered =>
            !ReferenceEquals(GlobalRegistry.Terrain, null) ||
            !ReferenceEquals(GlobalRegistry.VoxelEngine, null);

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

        /// <summary>
        /// Reads one latched chunk state by index, for a caller that must inspect what this lane has actually
        /// proven instead of asking about a point it already committed to. Valid indices are
        /// 0 .. <see cref="LatchedChunkCount"/>-1. Order is insertion order, not spatial order, and the ring
        /// overwrite in <see cref="Latch"/> means an index does not identify the same chunk across calls.
        ///
        /// Allocation-free: a 64-byte struct copied out of the fixed latch array.
        /// </summary>
        public static bool TryGetLatchedChunk(int index, out WorldChunkPhysicsBakedSignal signal)
        {
            if ((uint)index >= (uint)_latchCount)
            {
                signal = default;
                return false;
            }

            signal = _latch[index];
            return true;
        }

        /// <summary>
        /// Names the PROVEN-BAKED chunk whose footprint centre lies closest to the given point, so a degraded
        /// spawn can land on ground with collider proof instead of at an arbitrary coordinate.
        ///
        /// Written because `HectonPlayerSpawner.cs`:1885 `ForceFallbackSpawn` teleports to the world-centre
        /// water level with no bake proof of any kind — the one release path in that class not behind the
        /// gate — and its own comment at :1917 names the missing accessor as the reason it cannot do better:
        /// this lane exposed point queries only, so a caller could ask "is THIS point baked" but never
        /// "which point IS baked".
        ///
        /// Acceptance matches <see cref="IsWorldPointPhysicsBaked"/> exactly — collider active, no terminal
        /// failure — so the returned centre satisfies that predicate rather than merely resembling it.
        /// `center.y` is the chunk's terrain BASE height
        /// (<see cref="WorldChunkPhysicsBakedSignal.TerrainPosition"/>), NOT a ground height at the centre:
        /// vertical placement stays with the caller, which owns the character's height offset.
        ///
        /// Allocation-free: one pass over the fixed latch, squared distances, no sqrt, no sort, no collection.
        /// </summary>
        public static bool TryGetNearestPhysicsBakedChunkCenter(float originX, float originZ, out Vector3 center)
        {
            center = default;
            float bestSqrDistance = 0f;
            bool found = false;
            int count = _latchCount;
            for (int i = 0; i < count; i++)
            {
                uint flags = _latch[i].Flags;
                if ((flags & WorldChunkPhysicsBakedSignal.FlagColliderActive) == 0u ||
                    (flags & WorldChunkPhysicsBakedSignal.FlagBakeFailed) != 0u)
                {
                    continue;
                }

                float centerX = _latch[i].TerrainPosition.x + _latch[i].TerrainSize.x * 0.5f;
                float centerZ = _latch[i].TerrainPosition.z + _latch[i].TerrainSize.z * 0.5f;
                float deltaX = centerX - originX;
                float deltaZ = centerZ - originZ;
                float sqrDistance = deltaX * deltaX + deltaZ * deltaZ;
                if (found && sqrDistance >= bestSqrDistance)
                    continue;

                bestSqrDistance = sqrDistance;
                center.Set(centerX, _latch[i].TerrainPosition.y, centerZ);
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
