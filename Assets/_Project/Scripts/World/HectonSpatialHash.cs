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
            public int PayloadId;
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

        private static readonly ProfilerMarker _registerProfilerMarker = new ProfilerMarker("H8.World.AupSpatialHash.Register");
        private static readonly ProfilerMarker _updateProfilerMarker = new ProfilerMarker("H8.World.AupSpatialHash.Update");
        private static readonly ProfilerMarker _queryProfilerMarker = new ProfilerMarker("H8.World.AupSpatialHash.QuerySphere");

        private readonly double _cellSizeMeters;
        private NativeParallelHashMap<int, SpatialEntry> _entries;
        private NativeList<int> _entryHandles;
        private NativeList<int> _releasedHandles;
        private NativeParallelHashSet<int> _releasedHandleSet;
        private NativeParallelMultiHashMap<Long3, int> _cellOccupancy;
        private QueryScratchArena _queryScratch;
        private int _nextHandle;
        public HectonSpatialHash(int entryCapacity = 128, int cellCapacity = 512, double cellSizeMeters = DefaultCellSizeMeters)
        {
            int safeEntryCapacity = math.max(1, entryCapacity);
            int safeCellCapacity = math.max(safeEntryCapacity, cellCapacity);
            _cellSizeMeters = math.max(0.5d, cellSizeMeters);
            // COLD ALLOC: NativeParallelHashMap<int,SpatialEntry>[safeEntryCapacity] — AUP spatial registry records — owner: HectonSpatialHash
            _entries = new NativeParallelHashMap<int, SpatialEntry>(safeEntryCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeList<int>[safeEntryCapacity] — dense active-handle list for zero-alloc hash rebuilds — owner: HectonSpatialHash
            _entryHandles = new NativeList<int>(safeEntryCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeList<int>[safeEntryCapacity] — reusable released handle stack for bounded spatial registry ids — owner: HectonSpatialHash
            _releasedHandles = new NativeList<int>(safeEntryCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeParallelHashSet<int>[safeEntryCapacity] — duplicate-release guard for reusable spatial handles — owner: HectonSpatialHash
            _releasedHandleSet = new NativeParallelHashSet<int>(safeEntryCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeParallelMultiHashMap<long3,int>[safeCellCapacity] — AUP cell occupancy buckets — owner: HectonSpatialHash
            _cellOccupancy = new NativeParallelMultiHashMap<Long3, int>(safeCellCapacity, Allocator.Persistent);
            _queryScratch = new QueryScratchArena(safeEntryCapacity);
            _nextHandle = 1;
        }

        public int EntryCount => _entryHandles.IsCreated ? _entryHandles.Length : 0;

        public int Register(in AbsoluteUniversePosition position, float3 halfExtents, int kindMask, int payloadId)
        {
            using (_registerProfilerMarker.Auto())
            {
                int handle = AllocateHandle();
                if (handle <= 0)
                    return 0;

                UpsertInternal(handle, in position, halfExtents, kindMask, payloadId, appendHandle: true);
                return handle;
            }
        }

        public void UpdateEntry(int handle, in AbsoluteUniversePosition position, float3 halfExtents, int kindMask, int payloadId)
        {
            if (handle <= 0)
                return;

            using (_updateProfilerMarker.Auto())
            {
                bool appendHandle = !_entries.ContainsKey(handle);
                UpsertInternal(handle, in position, halfExtents, kindMask, payloadId, appendHandle);
            }
        }

        public void Unregister(int handle)
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
        }

        public void ReleaseHandle(int handle)
        {
            if (handle <= 0 || _entries.ContainsKey(handle))
                return;

            if (!_releasedHandleSet.Add(handle))
                return;

            _releasedHandles.Add(handle);
        }

        public bool TryGetEntry(int handle, out SpatialEntry entry)
        {
            return _entries.TryGetValue(handle, out entry);
        }

        public int CollectSphere(in AbsoluteUniversePosition origin, float radiusMeters, int requiredKindMask, NativeList<int> resultHandles)
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

        public void Dispose()
        {
            if (_entries.IsCreated)
                _entries.Dispose();

            if (_entryHandles.IsCreated)
                _entryHandles.Dispose();

            if (_releasedHandles.IsCreated)
                _releasedHandles.Dispose();

            if (_releasedHandleSet.IsCreated)
                _releasedHandleSet.Dispose();

            if (_cellOccupancy.IsCreated)
                _cellOccupancy.Dispose();

            _queryScratch.Dispose();
        }

        private int AllocateHandle()
        {
            int releasedCount = _releasedHandles.Length;
            if (releasedCount > 0)
            {
                int lastIndex = releasedCount - 1;
                int handle = _releasedHandles[lastIndex];
                _releasedHandles.RemoveAt(lastIndex);
                _releasedHandleSet.Remove(handle);
                return handle;
            }

            if (_nextHandle == int.MaxValue)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.LogError("[HectonSpatialHash] Handle allocator exhausted.");
#endif
                return 0;
            }

            return _nextHandle++;
        }

        private void UpsertInternal(int handle, in AbsoluteUniversePosition position, float3 halfExtents, int kindMask, int payloadId, bool appendHandle)
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

            if (_releasedHandles.Capacity < requiredEntryCapacity)
                _releasedHandles.Capacity = math.max(requiredEntryCapacity, _releasedHandles.Capacity << 1);

            if (_releasedHandleSet.Capacity < requiredEntryCapacity)
                _releasedHandleSet.Capacity = math.max(requiredEntryCapacity, _releasedHandleSet.Capacity << 1);

            int cellSpan = EstimateCellSpan(minCell, maxCell);
            int requiredCellCapacity = _cellOccupancy.Count() + cellSpan;
            if (_cellOccupancy.Capacity < requiredCellCapacity)
                _cellOccupancy.Capacity = math.max(requiredCellCapacity, _cellOccupancy.Capacity << 1);
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
    }
}
