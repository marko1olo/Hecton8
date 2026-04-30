using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;

namespace Hecton8.World
{
    /// <summary>
    /// Native AUP-aware broadphase storing cell occupancy in 64-bit world space instead of presentation-space Unity transforms.
    /// </summary>
    internal sealed class HectonSpatialHash : IDisposable
    {
        internal struct Long3 : IEquatable<Long3>
        {
            public long X;
            public long Y;
            public long Z;

            public Long3(long x, long y, long z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public bool Equals(Long3 other)
            {
                return X == other.X && Y == other.Y && Z == other.Z;
            }

            public override bool Equals(object obj)
            {
                return obj is Long3 other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = X.GetHashCode();
                    hash = (hash * 397) ^ Y.GetHashCode();
                    hash = (hash * 397) ^ Z.GetHashCode();
                    return hash;
                }
            }
        }

        internal struct SpatialEntry
        {
            public double3 AbsoluteCenter;
            public float3 HalfExtents;
            public Long3 MinCell;
            public Long3 MaxCell;
            public int KindMask;
            public uint EntityFlags;
            public int PayloadId;
        }

        internal struct TransientEventRecord
        {
            public uint EventId;
            public double3 AbsoluteCenter;
            public float RadiusMeters;
            public float Intensity;
            public double ExpirationTimestamp;
            public uint EventTypeMask;
            public uint EventFlags;
            public uint SourceKey;
        }

        private struct QueryScratchArena : IDisposable
        {
            public NativeList<int> Handles;
            public NativeParallelHashSet<int> Dedup;

            public QueryScratchArena(int initialCapacity)
            {
                int safeCapacity = math.max(1, initialCapacity);
                // COLD ALLOC: NativeList<int>[safeCapacity] — persistent query result staging arena for AUP spatial overlap queries — owner: HectonSpatialHash
                Handles = new NativeList<int>(safeCapacity, Allocator.Persistent);
                // COLD ALLOC: NativeParallelHashSet<int>[safeCapacity] — persistent dedupe arena for multi-cell overlap queries — owner: HectonSpatialHash
                Dedup = new NativeParallelHashSet<int>(safeCapacity, Allocator.Persistent);
            }

            public void EnsureCapacity(int requiredCapacity)
            {
                int safeCapacity = math.max(1, requiredCapacity);
                if (Handles.Capacity < safeCapacity)
                    Handles.Capacity = safeCapacity;

                if (Dedup.Capacity < safeCapacity)
                    Dedup.Capacity = safeCapacity;
            }

            public void Reset()
            {
                Handles.Clear();
                Dedup.Clear();
            }

            public void Dispose()
            {
                if (Handles.IsCreated)
                    Handles.Dispose();

                if (Dedup.IsCreated)
                    Dedup.Dispose();
            }
        }

        private const double DefaultCellSizeMeters = 8d;
        private const int HandleSlotBits = 20;
        private const uint HandleSlotMask = (1u << HandleSlotBits) - 1u;
        private const uint HandleGenerationShift = HandleSlotBits;
        private const uint MaxHandleSlot = HandleSlotMask;
        private const uint MaxHandleGeneration = (1u << (31 - HandleSlotBits)) - 1u;
        private const uint InitialHandleGeneration = 1u;
        private const int DefaultTransientCellCapacity = 512;

        private static readonly ProfilerMarker _registerProfilerMarker = new ProfilerMarker("H8.World.AupSpatialHash.Register");
        private static readonly ProfilerMarker _updateProfilerMarker = new ProfilerMarker("H8.World.AupSpatialHash.Update");
        private static readonly ProfilerMarker _queryProfilerMarker = new ProfilerMarker("H8.World.AupSpatialHash.QuerySphere");
        private static readonly ProfilerMarker _transientRegisterProfilerMarker = new ProfilerMarker("H8.World.AupSpatialHash.TransientRegister");
        private static readonly ProfilerMarker _transientQueryProfilerMarker = new ProfilerMarker("H8.World.AupSpatialHash.TransientQuery");
        private static readonly ProfilerMarker _transientPruneProfilerMarker = new ProfilerMarker("H8.World.AupSpatialHash.TransientPrune");

        private readonly double _cellSizeMeters;
        private NativeParallelHashMap<int, SpatialEntry> _entries;
        private NativeList<int> _entryHandles;
        private NativeQueue<uint> _freeHandles;
        private NativeParallelHashSet<uint> _queuedFreeHandles;
        private NativeParallelHashMap<uint, uint> _slotGenerations;
        private NativeParallelMultiHashMap<Long3, int> _cellOccupancy;
        private NativeParallelMultiHashMap<uint, TransientEventRecord> _transientEvents;
        private NativeParallelMultiHashMap<uint, TransientEventRecord> _transientEventsScratch;
        private NativeParallelHashSet<uint> _transientCellKeySet;
        private NativeParallelHashSet<uint> _transientCellKeySetScratch;
        private NativeParallelHashSet<uint> _transientQueryDedupe;
        private NativeList<uint> _transientCellKeys;
        private NativeList<uint> _transientCellKeysScratch;
        private QueryScratchArena _queryScratch;
        private uint _nextSlot;
        private uint _nextTransientEventId;
        public HectonSpatialHash(int entryCapacity = 128, int cellCapacity = 512, double cellSizeMeters = DefaultCellSizeMeters)
        {
            int safeEntryCapacity = math.max(1, entryCapacity);
            int safeCellCapacity = math.max(safeEntryCapacity, cellCapacity);
            _cellSizeMeters = math.max(0.5d, cellSizeMeters);
            // COLD ALLOC: NativeParallelHashMap<int,SpatialEntry>[safeEntryCapacity] — AUP spatial registry records — owner: HectonSpatialHash
            _entries = new NativeParallelHashMap<int, SpatialEntry>(safeEntryCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeList<int>[safeEntryCapacity] - dense active-handle list for zero-alloc hash rebuilds - owner: HectonSpatialHash
            _entryHandles = new NativeList<int>(safeEntryCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeQueue<uint>[safeEntryCapacity] - generation-counted free handle queue - owner: HectonSpatialHash
            _freeHandles = new NativeQueue<uint>(Allocator.Persistent);
            // COLD ALLOC: NativeParallelHashSet<uint>[safeEntryCapacity] - duplicate queued-handle guard - owner: HectonSpatialHash
            _queuedFreeHandles = new NativeParallelHashSet<uint>(safeEntryCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeParallelHashMap<uint,uint>[safeEntryCapacity] - current generation per spatial handle slot - owner: HectonSpatialHash
            _slotGenerations = new NativeParallelHashMap<uint, uint>(safeEntryCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeParallelMultiHashMap<long3,int>[safeCellCapacity] — AUP cell occupancy buckets — owner: HectonSpatialHash
            _cellOccupancy = new NativeParallelMultiHashMap<Long3, int>(safeCellCapacity, Allocator.Persistent);
            int safeTransientCellCapacity = math.max(DefaultTransientCellCapacity, safeCellCapacity);
            // COLD ALLOC: NativeParallelMultiHashMap<uint,TransientEventRecord>[safeTransientCellCapacity] - transient acoustic/chemical event buckets - owner: HectonSpatialHash
            _transientEvents = new NativeParallelMultiHashMap<uint, TransientEventRecord>(safeTransientCellCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeParallelMultiHashMap<uint,TransientEventRecord>[safeTransientCellCapacity] - expired-event prune scratch buckets - owner: HectonSpatialHash
            _transientEventsScratch = new NativeParallelMultiHashMap<uint, TransientEventRecord>(safeTransientCellCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeParallelHashSet<uint>[safeTransientCellCapacity] - unique transient cell keys - owner: HectonSpatialHash
            _transientCellKeySet = new NativeParallelHashSet<uint>(safeTransientCellCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeParallelHashSet<uint>[safeTransientCellCapacity] - transient prune scratch key set - owner: HectonSpatialHash
            _transientCellKeySetScratch = new NativeParallelHashSet<uint>(safeTransientCellCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeParallelHashSet<uint>[safeTransientCellCapacity] - transient event id dedupe for multi-cell queries - owner: HectonSpatialHash
            _transientQueryDedupe = new NativeParallelHashSet<uint>(safeTransientCellCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeList<uint>[safeTransientCellCapacity] - active transient cell-key traversal list - owner: HectonSpatialHash
            _transientCellKeys = new NativeList<uint>(safeTransientCellCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeList<uint>[safeTransientCellCapacity] - transient prune scratch cell-key traversal list - owner: HectonSpatialHash
            _transientCellKeysScratch = new NativeList<uint>(safeTransientCellCapacity, Allocator.Persistent);
            _queryScratch = new QueryScratchArena(safeEntryCapacity);
            _nextSlot = 1u;
            _nextTransientEventId = 1u;
        }

        public int EntryCount => _entryHandles.IsCreated ? _entryHandles.Length : 0;

        public int Register(in AbsoluteUniversePosition position, float3 halfExtents, int kindMask, uint entityFlags, int payloadId)
        {
            using (_registerProfilerMarker.Auto())
            {
                int handle = AllocateHandle();
                if (handle <= 0)
                    return 0;

                UpsertInternal(handle, in position, halfExtents, kindMask, entityFlags, payloadId, true);
                return handle;
            }
        }

        public int Register(in AbsoluteUniversePosition position, float3 halfExtents, int kindMask, uint entityFlags)
        {
            return Register(in position, halfExtents, kindMask, entityFlags, 0);
        }

        public int Register(in AbsoluteUniversePosition position, float3 halfExtents, int kindMask, int payloadId)
        {
            return Register(in position, halfExtents, kindMask, 0u, payloadId);
        }

        public void UpdateEntry(int handle, in AbsoluteUniversePosition position, float3 halfExtents, int kindMask, uint entityFlags, int payloadId)
        {
            if (handle <= 0 || !IsHandleCurrent(handle))
                return;

            using (_updateProfilerMarker.Auto())
            {
                bool appendHandle = !_entries.ContainsKey(handle);
                UpsertInternal(handle, in position, halfExtents, kindMask, entityFlags, payloadId, appendHandle);
            }
        }

        public void UpdateEntry(int handle, in AbsoluteUniversePosition position, float3 halfExtents, int kindMask, uint entityFlags)
        {
            UpdateEntry(handle, in position, halfExtents, kindMask, entityFlags, 0);
        }

        public void UpdateEntry(int handle, in AbsoluteUniversePosition position, float3 halfExtents, int kindMask, int payloadId)
        {
            UpdateEntry(handle, in position, halfExtents, kindMask, 0u, payloadId);
        }

        public void Unregister(int handle)
        {
            RemoveEntry(handle, recycleHandle: true);
        }

        public void Evict(int handle)
        {
            RemoveEntry(handle, recycleHandle: false);
        }

        public void ReleaseHandle(int handle)
        {
            RecycleHandle(handle);
        }

        private void RemoveEntry(int handle, bool recycleHandle)
        {
            if (handle <= 0 || !_entries.TryGetValue(handle, out SpatialEntry existingEntry))
                return;

            RemoveEntryCells(handle, in existingEntry);
            _entries.Remove(handle);

            for (int i = 0; i < _entryHandles.Length; i++)
            {
                if (_entryHandles[i] != handle)
                    continue;

                int lastIndex = _entryHandles.Length - 1;
                _entryHandles[i] = _entryHandles[lastIndex];
                _entryHandles.RemoveAt(lastIndex);
                break;
            }

            if (recycleHandle)
                RecycleHandle(handle);
        }

        public bool TryGetEntry(int handle, out SpatialEntry entry)
        {
            return _entries.TryGetValue(handle, out entry);
        }

        public bool IsCurrentHandle(int handle)
        {
            return IsHandleCurrent(handle);
        }

        public void RegisterTransientEvent(
            in AbsoluteUniversePosition position,
            float radiusMeters,
            float intensity,
            double expirationTimestamp,
            uint eventTypeMask,
            uint eventFlags,
            double currentTimestamp,
            uint sourceKey = 0u)
        {
            if (radiusMeters <= 0f || intensity <= 0f || eventTypeMask == 0u || expirationTimestamp <= currentTimestamp)
                return;

            using (_transientRegisterProfilerMarker.Auto())
            {
                PruneExpiredTransientEvents(currentTimestamp);

                double3 absoluteCenter = position.ToAbsoluteDouble3();
                float safeRadius = math.max(0.001f, radiusMeters);
                TransientEventRecord record = new TransientEventRecord
                {
                    EventId = AllocateTransientEventId(),
                    AbsoluteCenter = absoluteCenter,
                    RadiusMeters = safeRadius,
                    Intensity = math.max(0f, intensity),
                    ExpirationTimestamp = expirationTimestamp,
                    EventTypeMask = eventTypeMask,
                    EventFlags = eventFlags,
                    SourceKey = sourceKey
                };

                Long3 minCell = ToCell(absoluteCenter - new double3(safeRadius, safeRadius, safeRadius));
                Long3 maxCell = ToCell(absoluteCenter + new double3(safeRadius, safeRadius, safeRadius));
                EnsureTransientCapacity(EstimateCellSpan(minCell, maxCell));
                AddTransientRecordToCells(in record, minCell, maxCell, _transientEvents, _transientCellKeySet, _transientCellKeys);
            }
        }

        public int CollectTransientSphere(
            in AbsoluteUniversePosition origin,
            float radiusMeters,
            uint eventTypeMask,
            double currentTimestamp,
            NativeList<TransientEventRecord> results)
        {
            if (!results.IsCreated)
                return 0;

            using (_transientQueryProfilerMarker.Auto())
            {
                results.Clear();
                if (radiusMeters <= 0f || eventTypeMask == 0u || !_transientEvents.IsCreated || _transientCellKeys.Length == 0)
                    return 0;

                _transientQueryDedupe.Clear();
                double3 absoluteCenter = origin.ToAbsoluteDouble3();
                double radius = math.max(0.001d, radiusMeters);
                Long3 minCell = ToCell(absoluteCenter - new double3(radius, radius, radius));
                Long3 maxCell = ToCell(absoluteCenter + new double3(radius, radius, radius));

                for (long z = minCell.Z; z <= maxCell.Z; z++)
                {
                    for (long y = minCell.Y; y <= maxCell.Y; y++)
                    {
                        for (long x = minCell.X; x <= maxCell.X; x++)
                        {
                            uint cellKey = HashCell(new Long3(x, y, z));
                            if (!_transientEvents.TryGetFirstValue(cellKey, out TransientEventRecord record, out NativeParallelMultiHashMapIterator<uint> iterator))
                                continue;

                            do
                            {
                                if (!_transientQueryDedupe.Add(record.EventId))
                                    continue;

                                if (currentTimestamp >= record.ExpirationTimestamp)
                                    continue;

                                if ((record.EventTypeMask & eventTypeMask) == 0u)
                                    continue;

                                double combinedRadius = radius + record.RadiusMeters;
                                double3 delta = absoluteCenter - record.AbsoluteCenter;
                                if (math.lengthsq(delta) > combinedRadius * combinedRadius)
                                    continue;

                                results.Add(record);
                            }
                            while (_transientEvents.TryGetNextValue(out record, ref iterator));
                        }
                    }
                }

                return results.Length;
            }
        }

        public void BuildAcousticDensityMap(
            in AbsoluteUniversePosition origin,
            float radiusMeters,
            double currentTimestamp,
            NativeArray<float> densityMap,
            int3 dimensions,
            uint acousticEventTypeMask)
        {
            if (!densityMap.IsCreated || radiusMeters <= 0f || acousticEventTypeMask == 0u)
                return;

            int cellCount = math.max(0, math.min(densityMap.Length, dimensions.x * dimensions.y * dimensions.z));
            for (int i = 0; i < cellCount; i++)
                densityMap[i] = 0f;

            if (!_transientEvents.IsCreated || _transientCellKeys.Length == 0 || cellCount <= 0)
                return;

            _transientQueryDedupe.Clear();
            double3 center = origin.ToAbsoluteDouble3();
            double radius = math.max(0.001d, radiusMeters);
            double invDiameter = 1d / (radius * 2d);
            double3 minBounds = center - new double3(radius, radius, radius);
            Long3 minCell = ToCell(minBounds);
            Long3 maxCell = ToCell(center + new double3(radius, radius, radius));

            for (long z = minCell.Z; z <= maxCell.Z; z++)
            {
                for (long y = minCell.Y; y <= maxCell.Y; y++)
                {
                    for (long x = minCell.X; x <= maxCell.X; x++)
                    {
                        uint cellKey = HashCell(new Long3(x, y, z));
                        if (!_transientEvents.TryGetFirstValue(cellKey, out TransientEventRecord record, out NativeParallelMultiHashMapIterator<uint> iterator))
                            continue;

                        do
                        {
                            if (!_transientQueryDedupe.Add(record.EventId))
                                continue;

                            if (currentTimestamp >= record.ExpirationTimestamp || (record.EventTypeMask & acousticEventTypeMask) == 0u)
                                continue;

                            double3 normalized = (record.AbsoluteCenter - minBounds) * invDiameter;
                            int ix = math.clamp((int)math.floor(normalized.x * dimensions.x), 0, dimensions.x - 1);
                            int iy = math.clamp((int)math.floor(normalized.y * dimensions.y), 0, dimensions.y - 1);
                            int iz = math.clamp((int)math.floor(normalized.z * dimensions.z), 0, dimensions.z - 1);
                            int index = ix + (iy * dimensions.x) + (iz * dimensions.x * dimensions.y);
                            if (index < 0 || index >= cellCount)
                                continue;

                            densityMap[index] = math.min(1f, densityMap[index] + record.Intensity);
                        }
                        while (_transientEvents.TryGetNextValue(out record, ref iterator));
                    }
                }
            }
        }

        public void PruneExpiredTransientEvents(double currentTimestamp)
        {
            if (!_transientEvents.IsCreated || _transientCellKeys.Length == 0)
                return;

            using (_transientPruneProfilerMarker.Auto())
            {
                _transientEventsScratch.Clear();
                _transientCellKeySetScratch.Clear();
                _transientCellKeysScratch.Clear();

                for (int i = 0; i < _transientCellKeys.Length; i++)
                {
                    uint cellKey = _transientCellKeys[i];
                    if (!_transientEvents.TryGetFirstValue(cellKey, out TransientEventRecord record, out NativeParallelMultiHashMapIterator<uint> iterator))
                        continue;

                    do
                    {
                        if (currentTimestamp >= record.ExpirationTimestamp)
                            continue;

                        _transientEventsScratch.Add(cellKey, record);
                        if (_transientCellKeySetScratch.Add(cellKey))
                            _transientCellKeysScratch.Add(cellKey);
                    }
                    while (_transientEvents.TryGetNextValue(out record, ref iterator));
                }

                NativeParallelMultiHashMap<uint, TransientEventRecord> eventSwap = _transientEvents;
                _transientEvents = _transientEventsScratch;
                _transientEventsScratch = eventSwap;

                NativeParallelHashSet<uint> keySetSwap = _transientCellKeySet;
                _transientCellKeySet = _transientCellKeySetScratch;
                _transientCellKeySetScratch = keySetSwap;

                NativeList<uint> keyListSwap = _transientCellKeys;
                _transientCellKeys = _transientCellKeysScratch;
                _transientCellKeysScratch = keyListSwap;
            }
        }

        public void ClearTransientEvents(uint eventTypeMask, uint sourceKey, double currentTimestamp)
        {
            if (eventTypeMask == 0u || sourceKey == 0u || !_transientEvents.IsCreated || _transientCellKeys.Length == 0)
                return;

            using (_transientPruneProfilerMarker.Auto())
            {
                _transientEventsScratch.Clear();
                _transientCellKeySetScratch.Clear();
                _transientCellKeysScratch.Clear();

                for (int i = 0; i < _transientCellKeys.Length; i++)
                {
                    uint cellKey = _transientCellKeys[i];
                    if (!_transientEvents.TryGetFirstValue(cellKey, out TransientEventRecord record, out NativeParallelMultiHashMapIterator<uint> iterator))
                        continue;

                    do
                    {
                        if (currentTimestamp >= record.ExpirationTimestamp)
                            continue;

                        if (record.SourceKey == sourceKey && (record.EventTypeMask & eventTypeMask) != 0u)
                            continue;

                        _transientEventsScratch.Add(cellKey, record);
                        if (_transientCellKeySetScratch.Add(cellKey))
                            _transientCellKeysScratch.Add(cellKey);
                    }
                    while (_transientEvents.TryGetNextValue(out record, ref iterator));
                }

                NativeParallelMultiHashMap<uint, TransientEventRecord> eventSwap = _transientEvents;
                _transientEvents = _transientEventsScratch;
                _transientEventsScratch = eventSwap;

                NativeParallelHashSet<uint> keySetSwap = _transientCellKeySet;
                _transientCellKeySet = _transientCellKeySetScratch;
                _transientCellKeySetScratch = keySetSwap;

                NativeList<uint> keyListSwap = _transientCellKeys;
                _transientCellKeys = _transientCellKeysScratch;
                _transientCellKeysScratch = keyListSwap;
            }
        }

        public int CollectSphere(in AbsoluteUniversePosition origin, float radiusMeters, int requiredKindMask, uint interactionFilter, NativeList<int> resultHandles)
        {
            if (!resultHandles.IsCreated)
                return 0;

            using (_queryProfilerMarker.Auto())
            {
                resultHandles.Clear();
                if (radiusMeters <= 0f || !_cellOccupancy.IsCreated || _entryHandles.Length == 0)
                    return 0;

                double3 absoluteCenter = origin.ToAbsoluteDouble3();
                double radius = math.max(0.001d, radiusMeters);
                Long3 minCell = ToCell(new double3(absoluteCenter.x - radius, absoluteCenter.y - radius, absoluteCenter.z - radius));
                Long3 maxCell = ToCell(new double3(absoluteCenter.x + radius, absoluteCenter.y + radius, absoluteCenter.z + radius));

                int estimatedHandleCapacity = EstimateCellSpan(minCell, maxCell) * 4;
                _queryScratch.EnsureCapacity(math.max(estimatedHandleCapacity, _entryHandles.Length));
                _queryScratch.Reset();

                for (long z = minCell.Z; z <= maxCell.Z; z++)
                {
                    for (long y = minCell.Y; y <= maxCell.Y; y++)
                    {
                        for (long x = minCell.X; x <= maxCell.X; x++)
                        {
                            Long3 cellKey = new Long3(x, y, z);
                            if (!_cellOccupancy.TryGetFirstValue(cellKey, out int handle, out NativeParallelMultiHashMapIterator<Long3> iterator))
                                continue;

                            do
                            {
                                if (!_queryScratch.Dedup.Add(handle))
                                    continue;

                                if (!_entries.TryGetValue(handle, out SpatialEntry entry))
                                    continue;

                                if (requiredKindMask != 0 && (entry.KindMask & requiredKindMask) == 0)
                                    continue;

                                if (interactionFilter != 0u && (entry.EntityFlags & interactionFilter) == 0u)
                                    continue;

                                if (!SphereOverlapsEntry(absoluteCenter, radius * radius, in entry))
                                    continue;

                                _queryScratch.Handles.Add(handle);
                            }
                            while (_cellOccupancy.TryGetNextValue(out handle, ref iterator));
                        }
                    }
                }

                resultHandles.ResizeUninitialized(_queryScratch.Handles.Length);
                NativeArray<int>.Copy(_queryScratch.Handles.AsArray(), resultHandles.AsArray(), _queryScratch.Handles.Length);
                return _queryScratch.Handles.Length;
            }
        }

        public int CollectSphere(in AbsoluteUniversePosition origin, float radiusMeters, int requiredKindMask, NativeList<int> resultHandles)
        {
            return CollectSphere(in origin, radiusMeters, requiredKindMask, 0u, resultHandles);
        }

        public void Dispose()
        {
            if (_entries.IsCreated)
                _entries.Dispose();

            if (_entryHandles.IsCreated)
                _entryHandles.Dispose();

            if (_freeHandles.IsCreated)
                _freeHandles.Dispose();

            if (_queuedFreeHandles.IsCreated)
                _queuedFreeHandles.Dispose();

            if (_slotGenerations.IsCreated)
                _slotGenerations.Dispose();

            if (_cellOccupancy.IsCreated)
                _cellOccupancy.Dispose();

            if (_transientEvents.IsCreated)
                _transientEvents.Dispose();

            if (_transientEventsScratch.IsCreated)
                _transientEventsScratch.Dispose();

            if (_transientCellKeySet.IsCreated)
                _transientCellKeySet.Dispose();

            if (_transientCellKeySetScratch.IsCreated)
                _transientCellKeySetScratch.Dispose();

            if (_transientQueryDedupe.IsCreated)
                _transientQueryDedupe.Dispose();

            if (_transientCellKeys.IsCreated)
                _transientCellKeys.Dispose();

            if (_transientCellKeysScratch.IsCreated)
                _transientCellKeysScratch.Dispose();

            _queryScratch.Dispose();
        }

        private int AllocateHandle()
        {
            while (_freeHandles.TryDequeue(out uint queuedHandle))
            {
                _queuedFreeHandles.Remove(queuedHandle);
                if (IsQueuedFreeHandleCurrent(queuedHandle))
                    return (int)queuedHandle;
            }

            if (_nextSlot > MaxHandleSlot)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.LogError("[HectonSpatialHash] Handle allocator exhausted.");
#endif
                return 0;
            }

            uint slot = _nextSlot++;
            _slotGenerations.TryAdd(slot, InitialHandleGeneration);
            return EncodeHandle(slot, InitialHandleGeneration);
        }

        private void UpsertInternal(int handle, in AbsoluteUniversePosition position, float3 halfExtents, int kindMask, uint entityFlags, int payloadId, bool appendHandle)
        {
            Long3 minCell;
            Long3 maxCell;
            double3 absoluteCenter = position.ToAbsoluteDouble3();
            ResolveCellRange(absoluteCenter, halfExtents, out minCell, out maxCell);
            EnsureCapacityForEntry(minCell, maxCell, appendHandle ? 1 : 0);

            bool hadExistingEntry = _entries.TryGetValue(handle, out SpatialEntry previousEntry);
            if (hadExistingEntry)
                RemoveEntryCells(handle, in previousEntry);

            SpatialEntry entry = new SpatialEntry
            {
                AbsoluteCenter = absoluteCenter,
                HalfExtents = math.max(halfExtents, 0f),
                MinCell = minCell,
                MaxCell = maxCell,
                KindMask = kindMask,
                EntityFlags = entityFlags,
                PayloadId = payloadId
            };

            _entries[handle] = entry;
            if (appendHandle)
                _entryHandles.Add(handle);
            AddEntryCells(handle, in entry);
        }

        private void EnsureCapacityForEntry(Long3 minCell, Long3 maxCell, int additionalEntries)
        {
            int requiredEntryCapacity = _entryHandles.Length + math.max(0, additionalEntries);
            if (_entries.Capacity < requiredEntryCapacity)
                _entries.Capacity = math.max(requiredEntryCapacity, _entries.Capacity << 1);

            if (_entryHandles.Capacity < requiredEntryCapacity)
                _entryHandles.Capacity = math.max(requiredEntryCapacity, _entryHandles.Capacity << 1);

            if (_queuedFreeHandles.Capacity < requiredEntryCapacity)
                _queuedFreeHandles.Capacity = math.max(requiredEntryCapacity, _queuedFreeHandles.Capacity << 1);

            if (_slotGenerations.Capacity < requiredEntryCapacity)
                _slotGenerations.Capacity = math.max(requiredEntryCapacity, _slotGenerations.Capacity << 1);

            int cellSpan = EstimateCellSpan(minCell, maxCell);
            int requiredCellCapacity = _cellOccupancy.Count() + cellSpan;
            if (_cellOccupancy.Capacity < requiredCellCapacity)
                _cellOccupancy.Capacity = math.max(requiredCellCapacity, _cellOccupancy.Capacity << 1);
        }

        private void EnsureTransientCapacity(int additionalCells)
        {
            int requiredCapacity = _transientEvents.Count() + math.max(1, additionalCells);
            if (_transientEvents.Capacity < requiredCapacity)
                _transientEvents.Capacity = math.max(requiredCapacity, _transientEvents.Capacity << 1);

            if (_transientEventsScratch.Capacity < requiredCapacity)
                _transientEventsScratch.Capacity = math.max(requiredCapacity, _transientEventsScratch.Capacity << 1);

            int requiredKeyCapacity = _transientCellKeys.Length + math.max(1, additionalCells);
            if (_transientCellKeySet.Capacity < requiredKeyCapacity)
                _transientCellKeySet.Capacity = math.max(requiredKeyCapacity, _transientCellKeySet.Capacity << 1);

            if (_transientCellKeySetScratch.Capacity < requiredKeyCapacity)
                _transientCellKeySetScratch.Capacity = math.max(requiredKeyCapacity, _transientCellKeySetScratch.Capacity << 1);

            if (_transientQueryDedupe.Capacity < requiredCapacity)
                _transientQueryDedupe.Capacity = math.max(requiredCapacity, _transientQueryDedupe.Capacity << 1);

            if (_transientCellKeys.Capacity < requiredKeyCapacity)
                _transientCellKeys.Capacity = math.max(requiredKeyCapacity, _transientCellKeys.Capacity << 1);

            if (_transientCellKeysScratch.Capacity < requiredKeyCapacity)
                _transientCellKeysScratch.Capacity = math.max(requiredKeyCapacity, _transientCellKeysScratch.Capacity << 1);
        }

        private void AddTransientRecordToCells(
            in TransientEventRecord record,
            Long3 minCell,
            Long3 maxCell,
            NativeParallelMultiHashMap<uint, TransientEventRecord> targetMap,
            NativeParallelHashSet<uint> targetKeySet,
            NativeList<uint> targetKeyList)
        {
            for (long z = minCell.Z; z <= maxCell.Z; z++)
            {
                for (long y = minCell.Y; y <= maxCell.Y; y++)
                {
                    for (long x = minCell.X; x <= maxCell.X; x++)
                    {
                        uint cellKey = HashCell(new Long3(x, y, z));
                        targetMap.Add(cellKey, record);
                        if (targetKeySet.Add(cellKey))
                            targetKeyList.Add(cellKey);
                    }
                }
            }
        }

        private void AddEntryCells(int handle, in SpatialEntry entry)
        {
            for (long z = entry.MinCell.Z; z <= entry.MaxCell.Z; z++)
            {
                for (long y = entry.MinCell.Y; y <= entry.MaxCell.Y; y++)
                {
                    for (long x = entry.MinCell.X; x <= entry.MaxCell.X; x++)
                    {
                        _cellOccupancy.Add(new Long3(x, y, z), handle);
                    }
                }
            }
        }

        private void RemoveEntryCells(int handle, in SpatialEntry entry)
        {
            for (long z = entry.MinCell.Z; z <= entry.MaxCell.Z; z++)
            {
                for (long y = entry.MinCell.Y; y <= entry.MaxCell.Y; y++)
                {
                    for (long x = entry.MinCell.X; x <= entry.MaxCell.X; x++)
                    {
                        Long3 cellKey = new Long3(x, y, z);
                        while (_cellOccupancy.TryGetFirstValue(cellKey, out int existingHandle, out NativeParallelMultiHashMapIterator<Long3> iterator))
                        {
                            bool removedCurrentHandle = false;
                            do
                            {
                                if (existingHandle != handle)
                                    continue;

                                _cellOccupancy.Remove(iterator);
                                removedCurrentHandle = true;
                                break;
                            }
                            while (_cellOccupancy.TryGetNextValue(out existingHandle, ref iterator));

                            if (!removedCurrentHandle)
                                break;
                        }
                    }
                }
            }
        }

        private void ResolveCellRange(double3 absoluteCenter, float3 halfExtents, out Long3 minCell, out Long3 maxCell)
        {
            double3 absoluteMin = absoluteCenter - (double3)math.max(halfExtents, 0f);
            double3 absoluteMax = absoluteCenter + (double3)math.max(halfExtents, 0f);
            minCell = ToCell(absoluteMin);
            maxCell = ToCell(absoluteMax);
        }

        private Long3 ToCell(double3 absolutePosition)
        {
            double invCellSize = 1d / _cellSizeMeters;
            return new Long3(
                (long)math.floor(absolutePosition.x * invCellSize),
                (long)math.floor(absolutePosition.y * invCellSize),
                (long)math.floor(absolutePosition.z * invCellSize));
        }

        private static uint HashCell(Long3 cell)
        {
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                hash = (hash ^ (ulong)cell.X) * 1099511628211UL;
                hash = (hash ^ (ulong)cell.Y) * 1099511628211UL;
                hash = (hash ^ (ulong)cell.Z) * 1099511628211UL;
                hash ^= hash >> 32;
                return (uint)hash;
            }
        }

        private uint AllocateTransientEventId()
        {
            uint eventId = _nextTransientEventId++;
            if (_nextTransientEventId == 0u)
                _nextTransientEventId = 1u;
            return eventId;
        }

        private static bool SphereOverlapsEntry(double3 absoluteCenter, double radiusSq, in SpatialEntry entry)
        {
            double3 absoluteMin = entry.AbsoluteCenter - (double3)entry.HalfExtents;
            double3 absoluteMax = entry.AbsoluteCenter + (double3)entry.HalfExtents;
            double3 clampedPoint = math.clamp(absoluteCenter, absoluteMin, absoluteMax);
            double3 delta = absoluteCenter - clampedPoint;
            return math.lengthsq(delta) <= radiusSq;
        }

        private static int EstimateCellSpan(Long3 minCell, Long3 maxCell)
        {
            long spanX = math.max(1L, (maxCell.X - minCell.X) + 1L);
            long spanY = math.max(1L, (maxCell.Y - minCell.Y) + 1L);
            long spanZ = math.max(1L, (maxCell.Z - minCell.Z) + 1L);
            long total = spanX * spanY * spanZ;
            return (int)math.min(total, int.MaxValue);
        }

        private void RecycleHandle(int handle)
        {
            if (handle <= 0 || _entries.ContainsKey(handle))
                return;

            if (_queuedFreeHandles.Contains((uint)handle))
                return;

            if (!TryDecodeHandle(handle, out uint slot, out uint generation))
                return;

            if (!_slotGenerations.TryGetValue(slot, out uint currentGeneration) || currentGeneration != generation)
                return;

            if (generation >= MaxHandleGeneration)
            {
                _slotGenerations.Remove(slot);
                return;
            }

            uint nextGeneration = generation + 1u;
            _slotGenerations[slot] = nextGeneration;
            uint recycledHandle = (uint)EncodeHandle(slot, nextGeneration);
            if (_queuedFreeHandles.Add(recycledHandle))
                _freeHandles.Enqueue(recycledHandle);
        }

        private bool IsHandleCurrent(int handle)
        {
            if (!TryDecodeHandle(handle, out uint slot, out uint generation))
                return false;

            return _slotGenerations.TryGetValue(slot, out uint currentGeneration) &&
                   currentGeneration == generation &&
                   !_queuedFreeHandles.Contains((uint)handle);
        }

        private bool IsQueuedFreeHandleCurrent(uint queuedHandle)
        {
            if (!TryDecodeHandle((int)queuedHandle, out uint slot, out uint generation))
                return false;

            return _slotGenerations.TryGetValue(slot, out uint currentGeneration) &&
                   currentGeneration == generation &&
                   !_entries.ContainsKey((int)queuedHandle);
        }

        private static int EncodeHandle(uint slot, uint generation)
        {
            return (int)((generation << (int)HandleGenerationShift) | slot);
        }

        private static bool TryDecodeHandle(int handle, out uint slot, out uint generation)
        {
            uint encoded = (uint)handle;
            slot = encoded & HandleSlotMask;
            generation = encoded >> (int)HandleGenerationShift;
            return handle > 0 &&
                   slot > 0u &&
                   slot <= MaxHandleSlot &&
                   generation > 0u &&
                   generation <= MaxHandleGeneration;
        }
    }
}
