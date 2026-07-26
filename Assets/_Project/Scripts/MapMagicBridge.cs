using System;
using System.Runtime.InteropServices;
using Hecton8.Environment;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    public interface IMapMagicBiomeEventListener
    {
        void OnMapMagicBiomeChanged(int biomeId);
    }

    public static class MapMagicBiomeEvents
    {
        private const int ExpectedPendingBiomeEventCapacity = 8;
        private const int ListenerCapacity = 8;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;

        private struct ListenerSlot
        {
            public IMapMagicBiomeEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        // COLD ALLOC: ListenerSlot[8] - MapMagic biome listeners drained by SystemDispatcher without interface array dispatch - owner: MapMagicBiomeEvents
        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity];
        private static NativeQueue<int> _pendingBiomeIds;
        private static NativeQueue<int> _nextFrameBiomeIds;
        private static int _pendingBiomeIdsSentinelId;
        private static int _nextFrameBiomeIdsSentinelId;
        private static int _listenerCount;
        private static int _pendingBiomeIdCount;
        private static int _nextFrameBiomeIdCount;
        private static int _droppedBiomeIdCount;
        private static bool _isDispatching;

        public static int PendingCount => _pendingBiomeIdCount + _nextFrameBiomeIdCount;
        public static int DroppedCount => _droppedBiomeIdCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ReleaseNativeQueues();

            _pendingBiomeIdCount = 0;
            _nextFrameBiomeIdCount = 0;
            _droppedBiomeIdCount = 0;
            _isDispatching = false;
            for (int i = 0; i < _listenerCount; i++)
                _listeners[i].Clear();

            _listenerCount = 0;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterEditorPlayModeTeardown()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ResetStaticState;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ResetStaticState;
            UnityEditor.EditorApplication.quitting -= ResetStaticState;
            UnityEditor.EditorApplication.quitting += ResetStaticState;
        }

        private static void HandleEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange change)
        {
            if (change == UnityEditor.PlayModeStateChange.ExitingPlayMode ||
                change == UnityEditor.PlayModeStateChange.EnteredEditMode)
            {
                ResetStaticState();
            }
        }
#endif

        public static void Register(IMapMagicBiomeEventListener listener)
        {
            if (listener != null)
                RegisterImmediate(listener);
        }

        public static void Unregister(IMapMagicBiomeEventListener listener)
        {
            if (listener != null)
                TryUnregisterImmediate(listener);
        }

        [Obsolete("Use TryRaiseBiomeChanged(int) so bounded enqueue refusal is visible.", true)]
        public static void RaiseBiomeChanged(int biomeId)
        {
            TryRaiseBiomeChanged(biomeId);
        }

        public static bool TryRaiseBiomeChanged(int biomeId)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return false;
#endif

            EnsureInitialized();
            if (_pendingBiomeIdCount + _nextFrameBiomeIdCount >= ExpectedPendingBiomeEventCapacity)
            {
                _droppedBiomeIdCount++;
                return false;
            }

            if (_isDispatching)
            {
                _nextFrameBiomeIds.Enqueue(biomeId);
                _nextFrameBiomeIdCount++;
                return true;
            }

            _pendingBiomeIds.Enqueue(biomeId);
            _pendingBiomeIdCount++;
            return true;
        }

        public static void FlushPending()
        {
            if (!_pendingBiomeIds.IsCreated)
                return;

            PromoteNextFrameBiomeIdsIfFrontEmpty();
            int scanBudget = _pendingBiomeIdCount > 0 ? _pendingBiomeIdCount : ExpectedPendingBiomeEventCapacity;
            while (scanBudget > 0 && !_pendingBiomeIds.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingBiomeIds.TryDequeue(out int biomeId))
                {
                    _pendingBiomeIdCount = 0;
                    break;
                }

                if (_pendingBiomeIdCount > 0)
                    _pendingBiomeIdCount--;

                scanBudget--;
                int count = _listenerCount;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IMapMagicBiomeEventListener listener = _listeners[i].Listener;
                        if (listener != null)
                            listener.OnMapMagicBiomeChanged(biomeId);
                    }
                }
                finally
                {
                    _isDispatching = false;
                }
            }

            if (_pendingBiomeIds.IsEmpty())
            {
                _pendingBiomeIdCount = 0;
                PromoteNextFrameBiomeIdsIfFrontEmpty();
            }
        }

        private static void EnsureInitialized()
        {
            try
            {
                if (!_pendingBiomeIds.IsCreated)
                {
                    _pendingBiomeIds = new NativeQueue<int>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<int>[8] - deferred MapMagic biome events flushed by SystemDispatcher - owner: MapMagicBiomeEvents
                    RegisterNativeQueue(ref _pendingBiomeIds, ExpectedPendingBiomeEventCapacity, nameof(_pendingBiomeIds), out _pendingBiomeIdsSentinelId);
                    PrewarmQueue(ref _pendingBiomeIds, ExpectedPendingBiomeEventCapacity);
                }

                if (!_nextFrameBiomeIds.IsCreated)
                {
                    _nextFrameBiomeIds = new NativeQueue<int>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<int>[8] - next-frame MapMagic biome event lane prevents same-frame reentrant dispatch - owner: MapMagicBiomeEvents
                    RegisterNativeQueue(ref _nextFrameBiomeIds, ExpectedPendingBiomeEventCapacity, nameof(_nextFrameBiomeIds), out _nextFrameBiomeIdsSentinelId);
                    PrewarmQueue(ref _nextFrameBiomeIds, ExpectedPendingBiomeEventCapacity);
                }
            }
            catch
            {
                ReleaseNativeQueues();
                _pendingBiomeIdCount = 0;
                _nextFrameBiomeIdCount = 0;
                throw;
            }
        }

        private static void RegisterNativeQueue<T>(
            ref NativeQueue<T> queue,
            int capacity,
            string label,
            out int sentinelId)
            where T : unmanaged
        {
            sentinelId = 0;
            sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(
                queue,
                capacity,
                nameof(MapMagicBiomeEvents),
                label,
                NativeAllocationLifetime.Session);
            if (sentinelId > 0)
                return;

            ReleaseNativeQueue(ref queue, ref sentinelId);
            throw new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static void ReleaseNativeQueues()
        {
            ReleaseNativeQueue(ref _pendingBiomeIds, ref _pendingBiomeIdsSentinelId);
            ReleaseNativeQueue(ref _nextFrameBiomeIds, ref _nextFrameBiomeIdsSentinelId);
        }

        private static void ReleaseNativeQueue<T>(ref NativeQueue<T> queue, ref int sentinelId)
            where T : unmanaged
        {
            Exception firstException = null;

            if (sentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(sentinelId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    sentinelId = 0;
                }
            }

            if (queue.IsCreated)
            {
                try
                {
                    queue.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    queue = default;
                }
            }
            else
            {
                queue = default;
            }

            if (firstException != null)
                throw firstException;
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

        private static void PromoteNextFrameBiomeIdsIfFrontEmpty()
        {
            if (!_pendingBiomeIds.IsCreated ||
                !_nextFrameBiomeIds.IsCreated ||
                !_pendingBiomeIds.IsEmpty() ||
                _nextFrameBiomeIdCount <= 0)
            {
                return;
            }

            NativeQueue<int> swap = _pendingBiomeIds;
            _pendingBiomeIds = _nextFrameBiomeIds;
            _nextFrameBiomeIds = swap;
            int sentinelIdSwap = _pendingBiomeIdsSentinelId;
            _pendingBiomeIdsSentinelId = _nextFrameBiomeIdsSentinelId;
            _nextFrameBiomeIdsSentinelId = sentinelIdSwap;
            _pendingBiomeIdCount = _nextFrameBiomeIdCount;
            _nextFrameBiomeIdCount = 0;
        }

        private static void RegisterImmediate(IMapMagicBiomeEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return;
            }

            if (_listenerCount >= ListenerCapacity)
                return;

            _listeners[_listenerCount++].Listener = listener;
        }

        private static bool TryUnregisterImmediate(IMapMagicBiomeEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i].Listener, listener))
                    continue;

                _listenerCount--;
                _listeners[i] = _listeners[_listenerCount];
                _listeners[_listenerCount].Clear();
                return true;
            }

            return false;
        }
    }

    public readonly struct MapMagicTerrainTileSnapshot
    {
        public MapMagicTerrainTileSnapshot(MapMagicBridge provider, int tileX, int tileZ, Terrain terrain)
        {
            Provider = provider;
            TileX = tileX;
            TileZ = tileZ;
            Terrain = terrain;
        }

        public readonly MapMagicBridge Provider;
        public readonly int TileX;
        public readonly int TileZ;
        public readonly Terrain Terrain;

        public bool IsValid => Terrain != null && Terrain.terrainData != null;
    }

    public interface IMapMagicTerrainTileEventListener
    {
        void OnMapMagicTerrainTileApplied(in MapMagicTerrainTileSnapshot snapshot);

        void OnMapMagicTerrainTileMoved(in MapMagicTerrainTileSnapshot snapshot);
    }

    public static class MapMagicTerrainTileEvents
    {
        private const int ListenerCapacity = 8;
        private const int PendingEventCapacity = 16;
        private const int SnapshotSlotCapacity = 16;
        private const byte TileAppliedEventType = 1;
        private const byte TileMovedEventType = 2;
        private const byte TerrainChunkGeneratedFlagTileApplied = 1;
        private const byte TerrainChunkGeneratedFlagHeightPayloadResolved = 1 << 1;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;

        [StructLayout(LayoutKind.Explicit, Size = 8)]
        private struct MapMagicTerrainTileEventPayload
        {
            [FieldOffset(0)] public byte EventType;
            [FieldOffset(1)] private byte _pad0;
            [FieldOffset(2)] private ushort _pad1;
            [FieldOffset(4)] public int SnapshotSlot;
        }

        private struct ListenerSlot
        {
            public IMapMagicTerrainTileEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        // COLD ALLOC: ListenerSlot[8] - MapMagic terrain tile listeners without interface array dispatch - owner: MapMagicTerrainTileEvents
        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: MapMagicTerrainTileSnapshot[16] - fixed sidecar for managed MapMagic tile references during deferred dispatch - owner: MapMagicTerrainTileEvents
        private static readonly MapMagicTerrainTileSnapshot[] _snapshotSlots = new MapMagicTerrainTileSnapshot[SnapshotSlotCapacity];
        // COLD ALLOC: bool[16] - sidecar occupancy map for deferred tile snapshots - owner: MapMagicTerrainTileEvents
        private static readonly bool[] _snapshotSlotOccupied = new bool[SnapshotSlotCapacity];
        private static NativeQueue<MapMagicTerrainTileEventPayload> _pendingEvents;
        private static NativeQueue<MapMagicTerrainTileEventPayload> _nextFrameEvents;
        private static int _pendingEventsSentinelId;
        private static int _nextFrameEventsSentinelId;
        private static int _listenerCount;
        private static int _snapshotWriteIndex;
        private static int _snapshotPendingCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _droppedEventCount;
        private static int _droppedSnapshotSlotCount;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;
        public static int DroppedEventCount => _droppedEventCount;
        public static int DroppedSnapshotSlotCount => _droppedSnapshotSlotCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ReleaseNativeQueues();

            for (int i = 0; i < _listenerCount; i++)
                _listeners[i].Clear();

            _listenerCount = 0;
            ClearSnapshotSlots();
            _snapshotWriteIndex = 0;
            _snapshotPendingCount = 0;
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _droppedEventCount = 0;
            _droppedSnapshotSlotCount = 0;
            _isDispatching = false;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterEditorPlayModeTeardown()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ResetStaticState;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ResetStaticState;
            UnityEditor.EditorApplication.quitting -= ResetStaticState;
            UnityEditor.EditorApplication.quitting += ResetStaticState;
        }

        private static void HandleEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange change)
        {
            if (change == UnityEditor.PlayModeStateChange.ExitingPlayMode ||
                change == UnityEditor.PlayModeStateChange.EnteredEditMode)
            {
                ResetStaticState();
            }
        }
#endif

        public static void Register(IMapMagicTerrainTileEventListener listener)
        {
            if (listener != null)
                RegisterImmediate(listener);
        }

        public static void Unregister(IMapMagicTerrainTileEventListener listener)
        {
            if (listener != null)
                TryUnregisterImmediate(listener);
        }

        [Obsolete("Use TryRaiseTileApplied(in MapMagicTerrainTileSnapshot) so deferred bounded enqueue refusal is visible.", true)]
        public static void RaiseTileApplied(in MapMagicTerrainTileSnapshot snapshot)
        {
            TryRaiseTileApplied(in snapshot);
        }

        public static bool TryRaiseTileApplied(in MapMagicTerrainTileSnapshot snapshot)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return false;
#endif

            if (!snapshot.IsValid)
                return false;

            bool signalQueued = TryPublishTerrainChunkGenerated(in snapshot);

            // R99: heightmap-applied is NOT physics-ready. Publish the collider truth on its own lane so the
            // spawner's Kinematic Arrest Gate has something real to wait on (AGENTS.md requires the gate and
            // bans time-based loading timeouts; the signal simply did not exist before this).
            TryPublishWorldChunkPhysicsBaked(in snapshot);

            if (_listenerCount <= 0)
                return signalQueued;

            return Enqueue(TileAppliedEventType, in snapshot) && signalQueued;
        }

        [Obsolete("Use TryRaiseTileMoved(in MapMagicTerrainTileSnapshot) so deferred bounded enqueue refusal is visible.", true)]
        public static void RaiseTileMoved(in MapMagicTerrainTileSnapshot snapshot)
        {
            TryRaiseTileMoved(in snapshot);
        }

        public static bool TryRaiseTileMoved(in MapMagicTerrainTileSnapshot snapshot)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return false;
#endif

            if (!snapshot.IsValid)
                return false;

            if (_listenerCount <= 0)
                return false;

            return Enqueue(TileMovedEventType, in snapshot);
        }

        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated || _listenerCount <= 0)
            {
                DropPendingAmbient();
                return;
            }

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out MapMagicTerrainTileEventPayload payload))
                {
                    _pendingEventCount = 0;
                    break;
                }

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                if (!TryResolveSnapshot(payload.SnapshotSlot, out MapMagicTerrainTileSnapshot snapshot))
                {
                    ReleaseSnapshotSlot(payload.SnapshotSlot);
                    continue;
                }

                _isDispatching = true;
                try
                {
                    Dispatch(payload.EventType, in snapshot);
                }
                finally
                {
                    _isDispatching = false;
                }

                ReleaseSnapshotSlot(payload.SnapshotSlot);
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        public static void DropPendingAmbient()
        {
            DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount);
            DrainQueueWithoutDispatch(ref _nextFrameEvents, ref _nextFrameEventCount);
        }

        private static bool TryPublishTerrainChunkGenerated(in MapMagicTerrainTileSnapshot snapshot)
        {
            Terrain terrain = snapshot.Terrain;
            TerrainData terrainData = terrain.terrainData;
            int heightmapResolution = terrainData.heightmapResolution;
            int cacheRevision = 0;
            byte flags = TerrainChunkGeneratedFlagTileApplied;
            if (TryResolveQuantizedPayloadForSnapshot(in snapshot, terrain, terrainData, out MapMagicBridge.QuantizedHeightmapPayload payload))
            {
                heightmapResolution = payload.HeightmapResolution;
                cacheRevision = payload.CacheRevision;
                flags |= TerrainChunkGeneratedFlagHeightPayloadResolved;
            }

            TerrainChunkGeneratedSignal signal = new TerrainChunkGeneratedSignal
            {
                ChunkX = snapshot.TileX,
                ChunkZ = snapshot.TileZ,
                TerrainEntityHash = unchecked((uint)EntityId.ToULong(terrain.GetEntityId())),
                HeightmapResolution = heightmapResolution,
                CacheRevision = cacheRevision,
                TerrainPosition = (float3)terrain.transform.position,
                TerrainSize = (float3)terrainData.size,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Flags = flags
            };

            return TerrainChunkGeneratedEvents.TryPublish(in signal);
        }

        /// <summary>
        /// R99: publishes the physics-collider truth for a freshly applied terrain tile.
        /// Success requires an ENABLED <see cref="TerrainCollider"/> whose terrainData is the same object the
        /// heightmap was written into — a collider component that exists but is disabled, or is still bound to
        /// a different TerrainData, does not stop a player. Every failure path publishes too, with
        /// <see cref="WorldChunkPhysicsBakedSignal.FlagBakeFailed"/>, so the gate always resolves.
        /// </summary>
        private static bool TryPublishWorldChunkPhysicsBaked(in MapMagicTerrainTileSnapshot snapshot)
        {
            Terrain terrain = snapshot.Terrain;
            if (terrain == null)
                return false;

            TerrainData terrainData = terrain.terrainData;
            if (terrainData == null)
                return false;

            Vector3 terrainSize = terrainData.size;
            if (!(terrainSize.x > 0f) || !(terrainSize.z > 0f))
                return false;

            uint flags = 0u;
            TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
            if (collider == null)
            {
                flags = WorldChunkPhysicsBakedSignal.FlagColliderMissing | WorldChunkPhysicsBakedSignal.FlagBakeFailed;
            }
            else if (!collider.enabled || collider.terrainData != terrainData)
            {
                flags = WorldChunkPhysicsBakedSignal.FlagColliderMissing | WorldChunkPhysicsBakedSignal.FlagBakeFailed;
            }
            else
            {
                flags = WorldChunkPhysicsBakedSignal.FlagColliderActive | WorldChunkPhysicsBakedSignal.FlagHeightmapSynced;
            }

            WorldChunkPhysicsBakedSignal signal = new WorldChunkPhysicsBakedSignal
            {
                ChunkX = snapshot.TileX,
                ChunkZ = snapshot.TileZ,
                TerrainEntityHash = unchecked((uint)EntityId.ToULong(terrain.GetEntityId())),
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                TerrainPosition = (float3)terrain.transform.position,
                TerrainSize = (float3)terrainSize,
                Flags = flags
            };

            return WorldChunkPhysicsBakedEvents.TryPublish(in signal);
        }

        private static bool TryResolveQuantizedPayloadForSnapshot(
            in MapMagicTerrainTileSnapshot snapshot,
            Terrain terrain,
            TerrainData terrainData,
            out MapMagicBridge.QuantizedHeightmapPayload payload)
        {
            payload = default;
            MapMagicBridge provider = snapshot.Provider;
            if (provider == null || terrain == null || terrainData == null)
                return false;

            Vector3 terrainSize = terrainData.size;
            if (terrainSize.x <= 0f || terrainSize.z <= 0f)
                return false;

            Vector3 terrainPosition = terrain.transform.position;
            float sampleX = terrainPosition.x + terrainSize.x * 0.5f;
            float sampleZ = terrainPosition.z + terrainSize.z * 0.5f;
            return provider.TryGetQuantizedHeightmapPayload(sampleX, sampleZ, out payload) &&
                   MapMagicBridge.QuantizedHeightmapPayload.IsValid(in payload);
        }

        private static bool Enqueue(byte eventType, in MapMagicTerrainTileSnapshot snapshot)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                _droppedEventCount++;
                return false;
            }

            if (!TryReserveSnapshotSlot(in snapshot, out int snapshotSlot))
            {
                _droppedSnapshotSlotCount++;
                return false;
            }

            MapMagicTerrainTileEventPayload payload = new MapMagicTerrainTileEventPayload
            {
                EventType = eventType,
                SnapshotSlot = snapshotSlot
            };

            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
                return true;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
            return true;
        }

        private static void EnsureInitialized()
        {
            try
            {
                if (!_pendingEvents.IsCreated)
                {
                    _pendingEvents = new NativeQueue<MapMagicTerrainTileEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<MapMagicTerrainTileEventPayload>[16] - deferred MapMagic tile events flushed by SystemDispatcher - owner: MapMagicTerrainTileEvents
                    RegisterNativeQueue(ref _pendingEvents, PendingEventCapacity, nameof(_pendingEvents), out _pendingEventsSentinelId);
                    PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
                }

                if (!_nextFrameEvents.IsCreated)
                {
                    _nextFrameEvents = new NativeQueue<MapMagicTerrainTileEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<MapMagicTerrainTileEventPayload>[16] - next-frame MapMagic tile events prevent same-frame reentrant dispatch - owner: MapMagicTerrainTileEvents
                    RegisterNativeQueue(ref _nextFrameEvents, PendingEventCapacity, nameof(_nextFrameEvents), out _nextFrameEventsSentinelId);
                    PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
                }
            }
            catch
            {
                ReleaseNativeQueues();
                ClearSnapshotSlots();
                _snapshotWriteIndex = 0;
                _snapshotPendingCount = 0;
                _pendingEventCount = 0;
                _nextFrameEventCount = 0;
                throw;
            }
        }

        private static void RegisterNativeQueue<T>(
            ref NativeQueue<T> queue,
            int capacity,
            string label,
            out int sentinelId)
            where T : unmanaged
        {
            sentinelId = 0;
            sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(
                queue,
                capacity,
                nameof(MapMagicTerrainTileEvents),
                label,
                NativeAllocationLifetime.Session);
            if (sentinelId > 0)
                return;

            ReleaseNativeQueue(ref queue, ref sentinelId);
            throw new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static void ReleaseNativeQueues()
        {
            ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);
            ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);
        }

        private static void ReleaseNativeQueue<T>(ref NativeQueue<T> queue, ref int sentinelId)
            where T : unmanaged
        {
            Exception firstException = null;

            if (sentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(sentinelId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    sentinelId = 0;
                }
            }

            if (queue.IsCreated)
            {
                try
                {
                    queue.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    queue = default;
                }
            }
            else
            {
                queue = default;
            }

            if (firstException != null)
                throw firstException;
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

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!_pendingEvents.IsCreated ||
                !_nextFrameEvents.IsCreated ||
                !_pendingEvents.IsEmpty() ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            NativeQueue<MapMagicTerrainTileEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            int sentinelIdSwap = _pendingEventsSentinelId;
            _pendingEventsSentinelId = _nextFrameEventsSentinelId;
            _nextFrameEventsSentinelId = sentinelIdSwap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        private static bool TryReserveSnapshotSlot(in MapMagicTerrainTileSnapshot snapshot, out int snapshotSlot)
        {
            snapshotSlot = -1;
            if (_snapshotPendingCount >= SnapshotSlotCapacity)
                return false;

            for (int probe = 0; probe < SnapshotSlotCapacity; probe++)
            {
                int candidateSlot = _snapshotWriteIndex;
                _snapshotWriteIndex++;
                if (_snapshotWriteIndex >= SnapshotSlotCapacity)
                    _snapshotWriteIndex = 0;

                if (_snapshotSlotOccupied[candidateSlot])
                    continue;

                snapshotSlot = candidateSlot;
                _snapshotSlotOccupied[snapshotSlot] = true;
                _snapshotSlots[snapshotSlot] = snapshot;
                _snapshotPendingCount++;
                return true;
            }

            return false;
        }

        private static bool TryResolveSnapshot(int snapshotSlot, out MapMagicTerrainTileSnapshot snapshot)
        {
            if ((uint)snapshotSlot >= SnapshotSlotCapacity || !_snapshotSlotOccupied[snapshotSlot])
            {
                snapshot = default;
                return false;
            }

            snapshot = _snapshotSlots[snapshotSlot];
            return snapshot.IsValid;
        }

        private static void ReleaseSnapshotSlot(int snapshotSlot)
        {
            if ((uint)snapshotSlot >= SnapshotSlotCapacity || !_snapshotSlotOccupied[snapshotSlot])
                return;

            _snapshotSlots[snapshotSlot] = default;
            _snapshotSlotOccupied[snapshotSlot] = false;
            if (_snapshotPendingCount > 0)
                _snapshotPendingCount--;
        }

        private static void ClearSnapshotSlots()
        {
            for (int i = 0; i < SnapshotSlotCapacity; i++)
            {
                _snapshotSlots[i] = default;
                _snapshotSlotOccupied[i] = false;
            }
        }

        private static void DrainQueueWithoutDispatch(
            ref NativeQueue<MapMagicTerrainTileEventPayload> queue,
            ref int pendingCount)
        {
            if (!queue.IsCreated)
            {
                pendingCount = 0;
                return;
            }

            int drainBudget = pendingCount > 0 ? pendingCount : PendingEventCapacity;
            while (drainBudget-- > 0 && queue.TryDequeue(out MapMagicTerrainTileEventPayload payload))
                ReleaseSnapshotSlot(payload.SnapshotSlot);

            if (queue.IsEmpty())
                pendingCount = 0;
        }

        private static void Dispatch(byte eventType, in MapMagicTerrainTileSnapshot snapshot)
        {
            int count = _listenerCount;
            for (int i = count - 1; i >= 0; i--)
            {
                IMapMagicTerrainTileEventListener listener = _listeners[i].Listener;
                if (listener == null)
                    continue;

                if (eventType == TileAppliedEventType)
                    listener.OnMapMagicTerrainTileApplied(in snapshot);
                else if (eventType == TileMovedEventType)
                    listener.OnMapMagicTerrainTileMoved(in snapshot);
            }
        }

        private static void RegisterImmediate(IMapMagicTerrainTileEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return;
            }

            if (_listenerCount >= ListenerCapacity)
                return;

            _listeners[_listenerCount++].Listener = listener;
        }

        private static bool TryUnregisterImmediate(IMapMagicTerrainTileEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i].Listener, listener))
                    continue;

                _listenerCount--;
                _listeners[i] = _listeners[_listenerCount];
                _listeners[_listenerCount].Clear();
                return true;
            }

            return false;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-7000)]
    public abstract class MapMagicBridge : MonoBehaviour, ISlowTickable, ITerrainProvider
    {
        private const int BiomeMatrixLayerCount = 108;
        private const string TectonicSpineFamilyId = "biome.family.tectonic_spine";
        private static MapMagicBridge s_activeRuntimeInstance;

        internal static MapMagicBridge ActiveRuntimeInstance => s_activeRuntimeInstance;

        public readonly ref struct QuantizedHeightmapPayload
        {
            public QuantizedHeightmapPayload(
                NativeArray<ushort> heightSamples,
                Vector3 terrainPosition,
                Vector3 terrainSize,
                int heightmapResolution,
                int cacheRevision)
            {
                HeightSamples = heightSamples;
                TerrainPosition = terrainPosition;
                TerrainSize = terrainSize;
                HeightmapResolution = heightmapResolution;
                CacheRevision = cacheRevision;
            }

            public readonly NativeArray<ushort> HeightSamples;
            public readonly Vector3 TerrainPosition;
            public readonly Vector3 TerrainSize;
            public readonly int HeightmapResolution;
            public readonly int CacheRevision;

            public static bool IsValid(in QuantizedHeightmapPayload payload)
            {
                return payload.HeightSamples.IsCreated &&
                       payload.HeightmapResolution > 1 &&
                       payload.HeightSamples.Length >= payload.HeightmapResolution * payload.HeightmapResolution;
            }
        }

        public static MapMagicBridge Instance
        {
            get
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    return null;
#endif
                return s_activeRuntimeInstance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveRuntimeInstance()
        {
            s_activeRuntimeInstance = null;
        }

        protected void PublishActiveRuntimeInstance()
        {
            s_activeRuntimeInstance = this;
        }

        protected void ClearActiveRuntimeInstance()
        {
            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;
        }

        public abstract float WaterSurfaceLevel { get; }
        public abstract bool IsAvailable { get; }
        public abstract Component RuntimeMapMagicObject { get; }
        public abstract bool SandboxProceduralTerrainOnly { get; }
        public abstract bool SandboxUseBiomeMatrixAlphamapLayers { get; }
        public abstract bool EnableSandboxThermalWeathering { get; }
        public abstract float SandboxThermalWeatheringStrength { get; }
        public abstract float SandboxThermalWeatheringTalusAngleDegrees { get; }
        public abstract bool EnableSandboxTectonicSpineDisplacement { get; }
        public abstract float SandboxTectonicSpineStrength { get; }
        public abstract float SandboxTectonicSpineFrequency { get; }
        public abstract float SandboxTectonicSpineRidgeSharpness { get; }
        public abstract uint SandboxTectonicSpineSeed { get; }
        public abstract bool EnableSandboxFakeCliffOverhangOffsets { get; }
        public abstract int CurrentBiomeID { get; }

        public virtual bool TryGetTerrainArtifactIdentity(out TerrainArtifactIdentityDTO identity)
        {
            IWorldSeedProvider seedProvider = GlobalRegistry.WorldSeedProvider;
            int runtimeSeed = 0;
            int worldGenerationVersionId = 0;
            if (seedProvider != null && seedProvider.IsInitialized)
            {
                runtimeSeed = seedProvider.RuntimeWorldSeed;
                worldGenerationVersionId = math.max(0, seedProvider.RuntimeWorldGenerationVersionId);
            }

            float chunkSizeMeters = WorldMacroGeologyFields.DefaultChunkSizeMeters;
            WorldMacroGeologyFields.ResolveMinimumChunkRange(
                chunkSizeMeters,
                out int chunkMinX,
                out int chunkMinZ,
                out int chunkMaxX,
                out int chunkMaxZ);

            identity = new TerrainArtifactIdentityDTO
            {
                AuthoringSeed = unchecked((uint)WorldMacroGeologyFields.DefaultAuthoringSeed),
                RuntimeSeed = runtimeSeed,
                WorldGenerationVersionId = worldGenerationVersionId,
                MacroArtifactVersion = WorldMacroGeologyFields.ArtifactVersion,
                ChunkSizeMeters = chunkSizeMeters,
                ChunkMinX = chunkMinX,
                ChunkMinZ = chunkMinZ,
                ChunkMaxX = chunkMaxX,
                ChunkMaxZ = chunkMaxZ,
                ChunkArtifactRangeHash = WorldMacroGeologyFields.BuildChunkArtifactRangeHash(
                    unchecked((uint)WorldMacroGeologyFields.DefaultAuthoringSeed),
                    runtimeSeed,
                    worldGenerationVersionId,
                    WorldMacroGeologyFields.ArtifactVersion,
                    chunkSizeMeters,
                    chunkMinX,
                    chunkMinZ,
                    chunkMaxX,
                    chunkMaxZ),
                Flags = TerrainArtifactIdentityDTO.FlagsMacroGeologyPresent |
                        TerrainArtifactIdentityDTO.FlagsDefaultChunkRange |
                        TerrainArtifactIdentityDTO.FlagsMapMagicProvider
            };

            if (TryGetActiveQuantizedHeightmapPayload(out QuantizedHeightmapPayload payload))
            {
                identity.CacheRevision = math.max(0, payload.CacheRevision);
                identity.Flags |= TerrainArtifactIdentityDTO.FlagsHeightPayloadPresent;
                Vector3 terrainCenter = payload.TerrainPosition + payload.TerrainSize * 0.5f;
                if (TryResolveTerrainAt(terrainCenter.x, terrainCenter.z, out Terrain terrain) && terrain != null)
                    identity.TerrainEntityHash = unchecked((uint)EntityId.ToULong(terrain.GetEntityId()));
            }

            return identity.HasMacroIdentity;
        }

        public abstract void SlowTick();
        public abstract bool TryGetHeight(float x, float z, out float height);
        public abstract bool TryGetNormal(float x, float z, float sampleDistance, out Vector3 normal);
        public abstract bool TryGetHeightAUP(Vector3 absoluteUniversePosition, out float height);
        public abstract bool TryGetNormalAUP(Vector3 absoluteUniversePosition, float sampleDistance, out Vector3 normal);

        public virtual bool TryGetHeightAUP(in AbsoluteUniversePosition absoluteUniversePosition, out float height)
        {
            height = 0f;
            return absoluteUniversePosition.IsFinite() &&
                   TryGetHeightAUP(ToVector3(absoluteUniversePosition.ToAbsoluteDouble3()), out height);
        }

        public virtual bool TryGetNormalAUP(in AbsoluteUniversePosition absoluteUniversePosition, float sampleDistance, out Vector3 normal)
        {
            normal = Vector3.up;
            return absoluteUniversePosition.IsFinite() &&
                   TryGetNormalAUP(ToVector3(absoluteUniversePosition.ToAbsoluteDouble3()), sampleDistance, out normal);
        }

        public virtual float GetHeightAt(float3 aup)
        {
            return TryGetHeightAUP(new Vector3(aup.x, aup.y, aup.z), out float height) ? height : 0f;
        }

        public abstract bool TryGetActiveQuantizedHeightmapPayload(out QuantizedHeightmapPayload payload);
        public abstract bool TryGetQuantizedHeightmapPayload(float x, float z, out QuantizedHeightmapPayload payload);
        public abstract bool TryGetQuantizedHeightmapPayloadAUP(Vector3 absoluteUniversePosition, out QuantizedHeightmapPayload payload);
        public abstract bool TryGetTerrainSplatColorAUP(Vector3 absoluteUniversePosition, out Color color, out float confidence);

        public virtual bool TryGetQuantizedHeightmapPayloadAUP(in AbsoluteUniversePosition absoluteUniversePosition, out QuantizedHeightmapPayload payload)
        {
            payload = default;
            return absoluteUniversePosition.IsFinite() &&
                   TryGetQuantizedHeightmapPayloadAUP(ToVector3(absoluteUniversePosition.ToAbsoluteDouble3()), out payload);
        }

        public virtual bool TryGetTerrainSplatColorAUP(in AbsoluteUniversePosition absoluteUniversePosition, out Color color, out float confidence)
        {
            color = Color.clear;
            confidence = 0f;
            return absoluteUniversePosition.IsFinite() &&
                   TryGetTerrainSplatColorAUP(ToVector3(absoluteUniversePosition.ToAbsoluteDouble3()), out color, out confidence);
        }

        public abstract bool TryGetTerrainSplatColor(float x, float z, out Color color, out float confidence);
        public abstract float SampleHeightAUP(Vector3 absoluteUniversePosition, float fallbackHeight = 0f);

        public virtual float SampleHeightAUP(in AbsoluteUniversePosition absoluteUniversePosition, float fallbackHeight = 0f)
        {
            return TryGetHeightAUP(in absoluteUniversePosition, out float height)
                ? height
                : fallbackHeight;
        }

        public abstract float GetHeight(float x, float z);
        public abstract bool TryResolveTerrainAt(float x, float z, out Terrain terrain);
        public abstract int CopyResolvedTerrainsTo(Terrain[] destination);
        public abstract int CopyTerrainTileSnapshotsTo(MapMagicTerrainTileSnapshot[] destination);
        public abstract bool IsUnderwater(float x, float y, float z);
        public abstract bool IsValidSpawnPoint(float x, float y, float z, out float bottomHeight);
        public abstract bool TryGetBiomeIndex(float x, float z, out int biomeIndex);
        public abstract bool TryGetMatrixBiomeId(float x, float z, out int matrixBiomeId);
        public abstract bool TryGetMatrixBiomeId(float x, float z, out int matrixBiomeId, out int alphamapLayer);
        public abstract bool TryGetMatrixBiomeInfluence(
            float x,
            float z,
            out int primaryBiomeId,
            out int secondaryBiomeId,
            out byte blend255,
            out int primaryAlphamapLayer,
            out int secondaryAlphamapLayer);
        public abstract bool TryGetMatrixBiomeId(
            float x,
            float z,
            HectonBiomeMatrixCatalog catalog,
            out int matrixBiomeId,
            out int alphamapLayer);
        public abstract int GetBiomeIndex(float x, float z);
        public abstract int GetCurrentBiome(float3 position);
        public abstract void SetPlayerTransform(Transform player);
        public abstract void SetMapMagicObject(UnityEngine.Object target);
        public abstract void SetWaterSurfaceLevel(float y);
        public abstract void SetSandboxProceduralTerrainOnly(bool enabled);
        public abstract void SetSandboxBiomeMatrixAlphamapLayers(bool enabled);
        public abstract JobHandle ScheduleSandboxThermalWeatheringPostProcess(
            NativeArray<float> inputHeights01,
            NativeArray<float> outputHeights01,
            int width,
            int height,
            float cellSizeMeters,
            float heightScaleMeters,
            JobHandle dependency = default);
        public abstract JobHandle ScheduleSandboxTectonicSpineDisplacementPostProcess(
            HectonBiomeMatrixProfile biomeProfile,
            NativeArray<float> inputHeights01,
            NativeArray<float> outputHeights01,
            int width,
            int height,
            float2 worldOriginXZ,
            float cellSizeMeters,
            JobHandle dependency = default);
        public abstract JobHandle ScheduleSandboxTectonicSpineDisplacementPostProcess(
            bool isTectonicSpineBiome,
            NativeArray<float> inputHeights01,
            NativeArray<float> outputHeights01,
            int width,
            int height,
            float2 worldOriginXZ,
            float cellSizeMeters,
            JobHandle dependency = default);
        public abstract JobHandle ScheduleSandboxFakeCliffOverhangOffsets(
            NativeArray<float> heights01,
            NativeArray<float2> horizontalOffsetsMeters,
            int width,
            int height,
            float cellSizeMeters,
            float heightScaleMeters,
            JobHandle dependency = default);
        public abstract bool SetRuntimeObjectsPerFrame(int objectsPerFrame);
        public abstract bool ConfigureRuntimeTerrainStreaming(
            bool draftsInPlaymode,
            int mainRange,
            int draftRange,
            int draftResolutionValue);
        public abstract bool ApplyRuntimeTerrainQuality(
            int pixelError,
            int baseMapDistance,
            float detailDistance,
            float detailDensity,
            int heightmapMaximumLod);
        public abstract void MaintainRuntimeTerrainDetailLevels(
            int mainRange,
            int teardownRange,
            int mainPixelError,
            int mainBaseMapDistance,
            int draftPixelError,
            int draftBaseMapDistance,
            float detailDistance,
            float detailDensity,
            int heightmapMaximumLod);

        public static bool TryResolveBiomeMatrixAlphamapLayer(int matrixBiomeId, out int alphamapLayer)
        {
            alphamapLayer = -1;
            if (matrixBiomeId < 1 || matrixBiomeId > BiomeMatrixLayerCount)
                return false;

            alphamapLayer = matrixBiomeId - 1;
            return true;
        }

        public static bool IsTectonicSpineMatrixBiome(HectonBiomeMatrixProfile profile)
        {
            if (profile == null)
                return false;

            if (IsTectonicSpineFamilyId(profile.familyId))
                return true;

            HectonBiomeFamilyProfile familyProfile = profile.familyProfile;
            return familyProfile != null && IsTectonicSpineFamilyId(familyProfile.familyId);
        }

        public static JobHandle ScheduleBrineBasinLipRidgeOverlay(
            NativeArray<byte> basinMask,
            NativeArray<float> lipOffsetMeters,
            int width,
            int height,
            int falloffCells,
            float lipHeightMeters,
            JobHandle dependency = default)
        {
            if (!basinMask.IsCreated ||
                !lipOffsetMeters.IsCreated ||
                width <= 2 ||
                height <= 2)
            {
                return dependency;
            }

            int cellCount = width * height;
            if (basinMask.Length < cellCount || lipOffsetMeters.Length < cellCount)
                return dependency;

            var job = new BrineBasinLipRidgeOverlayJob
            {
                BasinMask = basinMask,
                LipOffsetMeters = lipOffsetMeters,
                Width = width,
                Height = height,
                FalloffCells = math.max(1, falloffCells),
                LipHeightMeters = math.max(0f, lipHeightMeters)
            };

            int batchCount = math.max(1, math.min(64, cellCount / 16));
            return job.Schedule(cellCount, batchCount, dependency);
        }

        private static bool IsTectonicSpineFamilyId(string familyId)
        {
            return string.Equals(familyId, TectonicSpineFamilyId, StringComparison.OrdinalIgnoreCase);
        }

        private static Vector3 ToVector3(double3 value)
        {
            return new Vector3((float)value.x, (float)value.y, (float)value.z);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct BrineBasinLipRidgeOverlayJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> BasinMask;
            public NativeArray<float> LipOffsetMeters;
            public int Width;
            public int Height;
            public int FalloffCells;
            public float LipHeightMeters;

            public void Execute(int index)
            {
                if (BasinMask[index] != 0)
                {
                    LipOffsetMeters[index] = 0f;
                    return;
                }

                int x = index % Width;
                int z = index / Width;
                int radius = math.max(1, FalloffCells);
                float radiusSq = math.max(1f, radius * radius);
                float best = 0f;
                for (int dz = -radius; dz <= radius; dz++)
                {
                    int nz = z + dz;
                    if ((uint)nz >= (uint)Height)
                        continue;

                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (dx == 0 && dz == 0)
                            continue;

                        int nx = x + dx;
                        if ((uint)nx >= (uint)Width)
                            continue;

                        int neighbor = nx + nz * Width;
                        if (BasinMask[neighbor] == 0)
                            continue;

                        float distanceSq = (dx * dx) + (dz * dz);
                        float ridge = 1f - math.saturate((distanceSq - 1f) / radiusSq);
                        best = math.max(best, ridge);
                    }
                }

                LipOffsetMeters[index] = best * LipHeightMeters;
            }
        }
    }
}
