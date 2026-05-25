using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace Hecton8.World
{
    internal static class HectonSpatialHashLayout
    {
        public const int Long3StrideBytes = 24;
        public const int SpatialEntryStrideBytes = 112;
        public const int QueryStatsStrideBytes = 24;
        public const int TransientEventRecordStrideBytes = 80;
    }

    /// <summary>
    /// Native AUP-aware broadphase storing cell occupancy in 64-bit world space instead of presentation-space Unity transforms.
    /// </summary>
    internal sealed class HectonSpatialHash : IDisposable
    {
        [StructLayout(LayoutKind.Explicit, Size = HectonSpatialHashLayout.Long3StrideBytes)]
        internal struct Long3 : IEquatable<Long3>
        {
            [FieldOffset(0)] public long X;
            [FieldOffset(8)] public long Y;
            [FieldOffset(16)] public long Z;

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

        [StructLayout(LayoutKind.Explicit, Size = HectonSpatialHashLayout.SpatialEntryStrideBytes)]
        internal struct SpatialEntry
        {
            [FieldOffset(0)] public double3 AbsoluteCenter;
            [FieldOffset(24)] public float3 HalfExtents;
            [FieldOffset(36)] private uint _pad0;
            [FieldOffset(40)] public Long3 MinCell;
            [FieldOffset(64)] public Long3 MaxCell;
            [FieldOffset(88)] public int KindMask;
            [FieldOffset(92)] private uint _pad1;
            [FieldOffset(96)] public ulong EntityFlags;
            [FieldOffset(104)] public int PayloadId;
            [FieldOffset(108)] private uint _pad2;
        }

        [StructLayout(LayoutKind.Explicit, Size = HectonSpatialHashLayout.QueryStatsStrideBytes)]
        internal readonly struct QueryStats
        {
            [FieldOffset(0)] public readonly int Mode;
            [FieldOffset(4)] public readonly int VisitedCellCount;
            [FieldOffset(8)] public readonly int CandidateHandleCount;
            [FieldOffset(12)] public readonly int DedupeHandleCount;
            [FieldOffset(16)] public readonly int ResultHandleCount;
            [FieldOffset(20)] public readonly byte Saturated;
            [FieldOffset(21)] private readonly byte _pad0;
            [FieldOffset(22)] private readonly ushort _pad1;

            public QueryStats(
                int mode,
                int visitedCellCount,
                int candidateHandleCount,
                int dedupeHandleCount,
                int resultHandleCount,
                byte saturated)
            {
                Mode = mode;
                VisitedCellCount = visitedCellCount;
                CandidateHandleCount = candidateHandleCount;
                DedupeHandleCount = dedupeHandleCount;
                ResultHandleCount = resultHandleCount;
                Saturated = saturated;
                _pad0 = 0;
                _pad1 = 0;
            }

            public bool IsSaturated => Saturated != 0;
        }

        [StructLayout(LayoutKind.Explicit, Size = HectonSpatialHashLayout.TransientEventRecordStrideBytes)]
        internal struct TransientEventRecord
        {
            [FieldOffset(0)] public uint EventId;
            [FieldOffset(4)] private uint _pad0;
            [FieldOffset(8)] public double3 AbsoluteCenter;
            [FieldOffset(32)] public float RadiusMeters;
            [FieldOffset(36)] public float Intensity;
            [FieldOffset(40)] public float Temperature;
            [FieldOffset(44)] private uint _pad1;
            [FieldOffset(48)] public double ExpirationTimestamp;
            [FieldOffset(56)] public uint EventTypeMask;
            [FieldOffset(60)] private uint _pad2;
            [FieldOffset(64)] public ulong EventFlags;
            [FieldOffset(72)] public uint SourceKey;
            [FieldOffset(76)] private uint _pad3;
        }

        private struct QueryScratchArena : IDisposable
        {
            public NativeList<int> Handles;
            public NativeParallelHashSet<int> Dedup;
            private readonly string _sentinelOwner;
            private readonly NativeAllocationLifetime _lifetime;

            public QueryScratchArena(int initialCapacity, string sentinelOwner, NativeAllocationLifetime lifetime)
            {
                int safeCapacity = math.max(1, initialCapacity);
                _sentinelOwner = sentinelOwner;
                _lifetime = lifetime;
                // COLD ALLOC: NativeList<int>[safeCapacity] — persistent query result staging arena for AUP spatial overlap queries — owner: HectonSpatialHash
                Handles = new NativeList<int>(safeCapacity, DataVaultExemptSpatialQueryScratchAllocator);
                NativeMemorySentinel.RegisterNativeList(
                    Handles,
                    _sentinelOwner,
                    QueryScratchHandlesLabel,
                    _lifetime);
                // COLD ALLOC: NativeParallelHashSet<int>[safeCapacity] — persistent dedupe arena for multi-cell overlap queries — owner: HectonSpatialHash
                Dedup = new NativeParallelHashSet<int>(safeCapacity, DataVaultExemptSpatialQueryScratchAllocator);
                NativeMemorySentinel.RegisterNativeParallelHashSet(
                    Dedup,
                    _sentinelOwner,
                    QueryScratchDedupLabel,
                    _lifetime);
            }

            public void Reset()
            {
                Handles.Clear();
                Dedup.Clear();
            }

            public void Dispose()
            {
                if (Handles.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeList(_sentinelOwner, QueryScratchHandlesLabel);
                    Handles.Dispose();
                }

                if (Dedup.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeParallelHashSet(_sentinelOwner, QueryScratchDedupLabel);
                    Dedup.Dispose();
                }
            }

            public JobHandle Dispose(JobHandle dependency)
            {
                JobHandle disposeHandle = dependency;

                if (Handles.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeList(_sentinelOwner, QueryScratchHandlesLabel);
                    disposeHandle = Handles.Dispose(disposeHandle);
                    Handles = default;
                }

                if (Dedup.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeParallelHashSet(_sentinelOwner, QueryScratchDedupLabel);
                    disposeHandle = Dedup.Dispose(disposeHandle);
                    Dedup = default;
                }

                return disposeHandle;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct RebuildCellOccupancyJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<int> Handles;
            [ReadOnly, NoAlias] public NativeArray<SpatialEntry> Entries;
            public int Count;
            public NativeParallelMultiHashMap<Long3, int> BackBuffer;

            public void Execute()
            {
                int safeCount = math.min(Count, math.min(Handles.Length, Entries.Length));
                for (int i = 0; i < safeCount; i++)
                {
                    SpatialEntry entry = Entries[i];
                    AddEntryCells(Handles[i], in entry, BackBuffer);
                }
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
        private const int DefaultCompactionCapacityFloor = 512;
        private const int MaxRegisteredEntryCellSpan = 4096;
        private const int MaxTransientEventCellSpan = 8192;
        private const int MaxSphereQueryCellSpan = 8192;
        private const long MinSafeCellIndex = long.MinValue + 2L;
        private const long MaxSafeCellIndex = long.MaxValue - 2L;
        private const uint AcousticImpulseEventMask = 1u << 0;
        private const uint ChemicalScentEventMask = 1u << 1;
        private const uint DisturbanceEventMask = 1u << 3;
        private const float CascadeIntensityThreshold = 0.8f;
        private const string QueryScratchHandlesLabel = "_queryScratch.Handles";
        private const string QueryScratchDedupLabel = "_queryScratch.Dedup";
        private const int QueryModeSphere = 1;
        private const int QueryModeAdjacent = 2;
        private const Allocator DataVaultExemptSpatialQueryScratchAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptSpatialEntryAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptSpatialCellAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptTransientEventAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptSpatialCompactionAllocator = Allocator.Persistent;

        private static readonly ProfilerMarker _registerProfilerMarker = new ProfilerMarker("H8.World.AupSpatialHash.Register");
        private static readonly ProfilerMarker _updateProfilerMarker = new ProfilerMarker("H8.World.AupSpatialHash.Update");
        private static readonly ProfilerMarker _queryProfilerMarker = new ProfilerMarker("H8.World.AupSpatialHash.QuerySphere");
        private static readonly ProfilerMarker _queryAdjacentProfilerMarker = new ProfilerMarker("H8.World.AupSpatialHash.QueryAdjacentCells");
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
        private NativeParallelMultiHashMap<Long3, int> _cellOccupancyScratch;
        private NativeParallelMultiHashMap<uint, TransientEventRecord> _transientEvents;
        private NativeParallelMultiHashMap<uint, TransientEventRecord> _transientEventsScratch;
        private NativeParallelHashSet<uint> _transientCellKeySet;
        private NativeParallelHashSet<uint> _transientCellKeySetScratch;
        private NativeParallelHashSet<uint> _transientQueryDedupe;
        private NativeList<uint> _transientCellKeys;
        private NativeList<uint> _transientCellKeysScratch;
        private NativeList<int> _compactionHandleSnapshot;
        private NativeList<SpatialEntry> _compactionEntrySnapshot;
        private QueryScratchArena _queryScratch;
        private static int _nextSentinelInstanceId;
        private readonly string _sentinelOwner;
        private readonly NativeAllocationLifetime _allocationLifetime;
        private JobHandle _cellCompactionHandle;
        private JobHandle _readerFence;
        private uint _nextSlot;
        private uint _nextTransientEventId;
        private bool _cellCompactionScheduled;
        private int _pendingCellCompactionTargetCapacity;
        private uint _mutationVersion;
        private uint _compactionMutationVersion;
        private QueryStats _lastQueryStats;
        public HectonSpatialHash(
            int entryCapacity = 128,
            int cellCapacity = 512,
            double cellSizeMeters = DefaultCellSizeMeters,
            NativeAllocationLifetime allocationLifetime = NativeAllocationLifetime.Scene)
        {
            int safeEntryCapacity = math.max(1, entryCapacity);
            int safeCellCapacity = math.max(safeEntryCapacity, cellCapacity);
            _cellSizeMeters = math.max(0.5d, cellSizeMeters);
            _sentinelOwner = string.Concat(nameof(HectonSpatialHash), "_", ++_nextSentinelInstanceId);
            _allocationLifetime = allocationLifetime;
            // COLD ALLOC: NativeParallelHashMap<int,SpatialEntry>[safeEntryCapacity] — AUP spatial registry records — owner: HectonSpatialHash
            _entries = new NativeParallelHashMap<int, SpatialEntry>(safeEntryCapacity, DataVaultExemptSpatialEntryAllocator);
            NativeMemorySentinel.RegisterNativeParallelHashMap(_entries, _sentinelOwner, nameof(_entries), _allocationLifetime);
            // COLD ALLOC: NativeList<int>[safeEntryCapacity] - dense active-handle list for zero-alloc hash rebuilds - owner: HectonSpatialHash
            _entryHandles = new NativeList<int>(safeEntryCapacity, DataVaultExemptSpatialEntryAllocator);
            NativeMemorySentinel.RegisterNativeList(_entryHandles, _sentinelOwner, nameof(_entryHandles), _allocationLifetime);
            // COLD ALLOC: NativeQueue<uint>[safeEntryCapacity] - generation-counted free handle queue - owner: HectonSpatialHash
            _freeHandles = new NativeQueue<uint>(DataVaultExemptSpatialEntryAllocator);
            NativeMemorySentinel.RegisterNativeQueue(
                _freeHandles,
                safeEntryCapacity,
                _sentinelOwner,
                nameof(_freeHandles),
                _allocationLifetime);
            PrewarmFreeHandleQueue(ref _freeHandles, safeEntryCapacity);
            // COLD ALLOC: NativeParallelHashSet<uint>[safeEntryCapacity] - duplicate queued-handle guard - owner: HectonSpatialHash
            _queuedFreeHandles = new NativeParallelHashSet<uint>(safeEntryCapacity, DataVaultExemptSpatialEntryAllocator);
            NativeMemorySentinel.RegisterNativeParallelHashSet(_queuedFreeHandles, _sentinelOwner, nameof(_queuedFreeHandles), _allocationLifetime);
            // COLD ALLOC: NativeParallelHashMap<uint,uint>[safeEntryCapacity] - current generation per spatial handle slot - owner: HectonSpatialHash
            _slotGenerations = new NativeParallelHashMap<uint, uint>(safeEntryCapacity, DataVaultExemptSpatialEntryAllocator);
            NativeMemorySentinel.RegisterNativeParallelHashMap(_slotGenerations, _sentinelOwner, nameof(_slotGenerations), _allocationLifetime);
            // COLD ALLOC: NativeParallelMultiHashMap<long3,int>[safeCellCapacity] — AUP cell occupancy buckets — owner: HectonSpatialHash
            _cellOccupancy = new NativeParallelMultiHashMap<Long3, int>(safeCellCapacity, DataVaultExemptSpatialCellAllocator);
            NativeMemorySentinel.RegisterNativeParallelMultiHashMap(_cellOccupancy, _sentinelOwner, nameof(_cellOccupancy), _allocationLifetime);
            // COLD ALLOC: NativeParallelMultiHashMap<long3,int>[safeCellCapacity] - spatial bucket compaction scratch - owner: HectonSpatialHash
            _cellOccupancyScratch = new NativeParallelMultiHashMap<Long3, int>(safeCellCapacity, DataVaultExemptSpatialCellAllocator);
            NativeMemorySentinel.RegisterNativeParallelMultiHashMap(_cellOccupancyScratch, _sentinelOwner, nameof(_cellOccupancyScratch), _allocationLifetime);
            int safeTransientCellCapacity = math.max(DefaultTransientCellCapacity, safeCellCapacity);
            // COLD ALLOC: NativeParallelMultiHashMap<uint,TransientEventRecord>[safeTransientCellCapacity] - transient acoustic/chemical event buckets - owner: HectonSpatialHash
            _transientEvents = new NativeParallelMultiHashMap<uint, TransientEventRecord>(safeTransientCellCapacity, DataVaultExemptTransientEventAllocator);
            NativeMemorySentinel.RegisterNativeParallelMultiHashMap(_transientEvents, _sentinelOwner, nameof(_transientEvents), _allocationLifetime);
            // COLD ALLOC: NativeParallelMultiHashMap<uint,TransientEventRecord>[safeTransientCellCapacity] - expired-event prune scratch buckets - owner: HectonSpatialHash
            _transientEventsScratch = new NativeParallelMultiHashMap<uint, TransientEventRecord>(safeTransientCellCapacity, DataVaultExemptTransientEventAllocator);
            NativeMemorySentinel.RegisterNativeParallelMultiHashMap(_transientEventsScratch, _sentinelOwner, nameof(_transientEventsScratch), _allocationLifetime);
            // COLD ALLOC: NativeParallelHashSet<uint>[safeTransientCellCapacity] - unique transient cell keys - owner: HectonSpatialHash
            _transientCellKeySet = new NativeParallelHashSet<uint>(safeTransientCellCapacity, DataVaultExemptTransientEventAllocator);
            NativeMemorySentinel.RegisterNativeParallelHashSet(_transientCellKeySet, _sentinelOwner, nameof(_transientCellKeySet), _allocationLifetime);
            // COLD ALLOC: NativeParallelHashSet<uint>[safeTransientCellCapacity] - transient prune scratch key set - owner: HectonSpatialHash
            _transientCellKeySetScratch = new NativeParallelHashSet<uint>(safeTransientCellCapacity, DataVaultExemptTransientEventAllocator);
            NativeMemorySentinel.RegisterNativeParallelHashSet(_transientCellKeySetScratch, _sentinelOwner, nameof(_transientCellKeySetScratch), _allocationLifetime);
            // COLD ALLOC: NativeParallelHashSet<uint>[safeTransientCellCapacity] - transient event id dedupe for multi-cell queries - owner: HectonSpatialHash
            _transientQueryDedupe = new NativeParallelHashSet<uint>(safeTransientCellCapacity, DataVaultExemptTransientEventAllocator);
            NativeMemorySentinel.RegisterNativeParallelHashSet(_transientQueryDedupe, _sentinelOwner, nameof(_transientQueryDedupe), _allocationLifetime);
            // COLD ALLOC: NativeList<uint>[safeTransientCellCapacity] - active transient cell-key traversal list - owner: HectonSpatialHash
            _transientCellKeys = new NativeList<uint>(safeTransientCellCapacity, DataVaultExemptTransientEventAllocator);
            NativeMemorySentinel.RegisterNativeList(_transientCellKeys, _sentinelOwner, nameof(_transientCellKeys), _allocationLifetime);
            // COLD ALLOC: NativeList<uint>[safeTransientCellCapacity] - transient prune scratch cell-key traversal list - owner: HectonSpatialHash
            _transientCellKeysScratch = new NativeList<uint>(safeTransientCellCapacity, DataVaultExemptTransientEventAllocator);
            NativeMemorySentinel.RegisterNativeList(_transientCellKeysScratch, _sentinelOwner, nameof(_transientCellKeysScratch), _allocationLifetime);
            // COLD ALLOC: NativeList<int>[safeEntryCapacity] - immutable handle snapshot for async occupancy compaction - owner: HectonSpatialHash
            _compactionHandleSnapshot = new NativeList<int>(safeEntryCapacity, DataVaultExemptSpatialCompactionAllocator);
            NativeMemorySentinel.RegisterNativeList(_compactionHandleSnapshot, _sentinelOwner, nameof(_compactionHandleSnapshot), _allocationLifetime);
            // COLD ALLOC: NativeList<SpatialEntry>[safeEntryCapacity] - immutable entry snapshot for async occupancy compaction - owner: HectonSpatialHash
            _compactionEntrySnapshot = new NativeList<SpatialEntry>(safeEntryCapacity, DataVaultExemptSpatialCompactionAllocator);
            NativeMemorySentinel.RegisterNativeList(_compactionEntrySnapshot, _sentinelOwner, nameof(_compactionEntrySnapshot), _allocationLifetime);
            _queryScratch = new QueryScratchArena(safeEntryCapacity, _sentinelOwner, _allocationLifetime);
            _nextSlot = 1u;
            _nextTransientEventId = 1u;
        }

        public int EntryCount => _entryHandles.IsCreated ? _entryHandles.Length : 0;
        public QueryStats LastQueryStats => _lastQueryStats;

        public void ClearLastQueryStats()
        {
            _lastQueryStats = default;
        }

        public int Register(in AbsoluteUniversePosition position, float3 halfExtents, int kindMask, ulong entityFlags, int payloadId)
        {
            if (!IsFiniteAup(in position) || !IsFiniteFloat3(halfExtents) || kindMask == 0)
                return 0;

            using (_registerProfilerMarker.Auto())
            {
                int handle = AllocateHandle();
                if (handle <= 0)
                    return 0;

                if (!UpsertInternal(handle, in position, halfExtents, kindMask, entityFlags, payloadId, true))
                {
                    RecycleHandle(handle);
                    return 0;
                }

                return handle;
            }
        }

        public int Register(in AbsoluteUniversePosition position, float3 halfExtents, int kindMask, ulong entityFlags)
        {
            return Register(in position, halfExtents, kindMask, entityFlags, 0);
        }

        public int Register(in AbsoluteUniversePosition position, float3 halfExtents, int kindMask, int payloadId)
        {
            return Register(in position, halfExtents, kindMask, 0UL, payloadId);
        }

        public bool TryUpdateEntry(int handle, in AbsoluteUniversePosition position, float3 halfExtents, int kindMask, ulong entityFlags, int payloadId)
        {
            if (handle <= 0 ||
                !IsHandleCurrent(handle) ||
                !IsFiniteAup(in position) ||
                !IsFiniteFloat3(halfExtents) ||
                kindMask == 0)
                return false;

            using (_updateProfilerMarker.Auto())
            {
                bool appendHandle = !_entries.ContainsKey(handle);
                return UpsertInternal(handle, in position, halfExtents, kindMask, entityFlags, payloadId, appendHandle);
            }
        }

        public void UpdateEntry(int handle, in AbsoluteUniversePosition position, float3 halfExtents, int kindMask, ulong entityFlags, int payloadId)
        {
            TryUpdateEntry(handle, in position, halfExtents, kindMask, entityFlags, payloadId);
        }

        public void UpdateEntry(int handle, in AbsoluteUniversePosition position, float3 halfExtents, int kindMask, ulong entityFlags)
        {
            UpdateEntry(handle, in position, halfExtents, kindMask, entityFlags, 0);
        }

        public void UpdateEntry(int handle, in AbsoluteUniversePosition position, float3 halfExtents, int kindMask, int payloadId)
        {
            UpdateEntry(handle, in position, halfExtents, kindMask, 0UL, payloadId);
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

            _mutationVersion++;
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
            ulong eventFlags,
            double currentTimestamp,
            uint sourceKey = 0u,
            float temperature = 0f)
        {
            if (!IsFiniteAup(in position) ||
                !math.isfinite(radiusMeters) ||
                !math.isfinite(intensity) ||
                !math.isfinite(expirationTimestamp) ||
                !math.isfinite(currentTimestamp) ||
                radiusMeters <= 0f ||
                intensity <= 0f ||
                eventTypeMask == 0u ||
                IsTransientExpired(currentTimestamp, expirationTimestamp))
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
                    Temperature = math.isfinite(temperature) ? temperature : 0f,
                    ExpirationTimestamp = expirationTimestamp,
                    EventTypeMask = eventTypeMask,
                    EventFlags = eventFlags,
                    SourceKey = sourceKey
                };

                Long3 minCell = ToCell(absoluteCenter - new double3(safeRadius, safeRadius, safeRadius));
                Long3 maxCell = ToCell(absoluteCenter + new double3(safeRadius, safeRadius, safeRadius));
                int cellSpan = EstimateCellSpan(minCell, maxCell);
                if (cellSpan > MaxTransientEventCellSpan)
                    return;

                EnsureTransientCapacity(cellSpan);
                AddTransientRecordToCells(in record, minCell, maxCell, _transientEvents, _transientCellKeySet, _transientCellKeys);
                TryEmitDisturbanceCascade(in record, minCell, maxCell, currentTimestamp, cellSpan);
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
                if (!IsFiniteAup(in origin) ||
                    !math.isfinite(radiusMeters) ||
                    !math.isfinite(currentTimestamp) ||
                    radiusMeters <= 0f ||
                    eventTypeMask == 0u ||
                    !_transientEvents.IsCreated ||
                    _transientCellKeys.Length == 0)
                    return 0;

                _transientQueryDedupe.Clear();
                double3 absoluteCenter = origin.ToAbsoluteDouble3();
                double radius = math.max(0.001d, radiusMeters);
                Long3 minCell = ToCell(absoluteCenter - new double3(radius, radius, radius));
                Long3 maxCell = ToCell(absoluteCenter + new double3(radius, radius, radius));
                if (EstimateCellSpan(minCell, maxCell) > MaxSphereQueryCellSpan)
                    return 0;

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

                                if (IsTransientExpired(currentTimestamp, record.ExpirationTimestamp))
                                    continue;

                                if ((record.EventTypeMask & eventTypeMask) == 0u)
                                    continue;

                                double combinedRadius = radius + record.RadiusMeters;
                                double3 delta = absoluteCenter - record.AbsoluteCenter;
                                if (math.lengthsq(delta) > combinedRadius * combinedRadius)
                                    continue;

                                if (results.Length < results.Capacity)
                                    results.AddNoResize(record);
                            }
                            while (_transientEvents.TryGetNextValue(out record, ref iterator));
                        }
                    }
                }

                return results.Length;
            }
        }

        public bool QueryTemperatureGradient(
            in AbsoluteUniversePosition origin,
            float radiusMeters,
            double currentTimestamp,
            out float temperatureDelta,
            out double3 gradient)
        {
            temperatureDelta = 0f;
            gradient = double3.zero;
            if (!IsFiniteAup(in origin) ||
                !math.isfinite(radiusMeters) ||
                !math.isfinite(currentTimestamp) ||
                radiusMeters <= 0f ||
                !_transientEvents.IsCreated ||
                _transientCellKeys.Length == 0)
                return false;

            using (_transientQueryProfilerMarker.Auto())
            {
                _transientQueryDedupe.Clear();
                double3 absoluteCenter = origin.ToAbsoluteDouble3();
                double radius = math.max(0.001d, radiusMeters);
                Long3 minCell = ToCell(absoluteCenter - new double3(radius, radius, radius));
                Long3 maxCell = ToCell(absoluteCenter + new double3(radius, radius, radius));
                if (EstimateCellSpan(minCell, maxCell) > MaxSphereQueryCellSpan)
                    return false;

                double accumulatedWeight = 0d;
                double3 accumulatedGradient = double3.zero;

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

                                if (IsTransientExpired(currentTimestamp, record.ExpirationTimestamp) || record.Temperature == 0f)
                                    continue;

                                double3 delta = record.AbsoluteCenter - absoluteCenter;
                                double distanceSq = math.lengthsq(delta);
                                double influenceRadius = radius + record.RadiusMeters;
                                double influenceRadiusSq = influenceRadius * influenceRadius;
                                if (distanceSq > influenceRadiusSq)
                                    continue;

                                double inverseInfluenceRadius = 1d / math.max(0.001d, influenceRadius);
                                double falloff = 1d - math.saturate(distanceSq / math.max(0.000001d, influenceRadiusSq));
                                double weight = falloff * math.max(0f, record.Intensity);
                                if (weight <= 0d)
                                    continue;

                                temperatureDelta += (float)(record.Temperature * weight);
                                accumulatedWeight += weight;
                                if (distanceSq > 0.00000001d)
                                    accumulatedGradient += (delta * inverseInfluenceRadius) * (record.Temperature * weight);
                            }
                            while (_transientEvents.TryGetNextValue(out record, ref iterator));
                        }
                    }
                }

                if (accumulatedWeight <= 0d)
                    return false;

                gradient = accumulatedGradient / accumulatedWeight;
                return true;
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
            if (!densityMap.IsCreated ||
                !IsFiniteAup(in origin) ||
                !math.isfinite(radiusMeters) ||
                !math.isfinite(currentTimestamp) ||
                radiusMeters <= 0f ||
                acousticEventTypeMask == 0u)
                return;

            if (dimensions.x <= 0 || dimensions.y <= 0 || dimensions.z <= 0)
                return;

            if (!TryResolveDensityGridLayout(dimensions, densityMap.Length, out int cellCount, out int strideZ))
                return;

            for (int i = 0; i < cellCount; i++)
                densityMap[i] = 0f;

            if (!_transientEvents.IsCreated || _transientCellKeys.Length == 0 || cellCount <= 0)
                return;

            _transientQueryDedupe.Clear();
            double3 center = origin.ToAbsoluteDouble3();
            double radius = math.max(0.001d, radiusMeters);
            double diameter = radius * 2d;
            double3 minBounds = center - new double3(radius, radius, radius);
            double3 cellSize = new double3(
                diameter / dimensions.x,
                diameter / dimensions.y,
                diameter / dimensions.z);
            Long3 minCell = ToCell(minBounds);
            Long3 maxCell = ToCell(center + new double3(radius, radius, radius));
            if (EstimateCellSpan(minCell, maxCell) > MaxSphereQueryCellSpan)
                return;

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

                            if (IsTransientExpired(currentTimestamp, record.ExpirationTimestamp) || (record.EventTypeMask & acousticEventTypeMask) == 0u)
                                continue;

                            double3 local = record.AbsoluteCenter - minBounds;
                            if (local.x < 0d || local.y < 0d || local.z < 0d ||
                                local.x >= diameter || local.y >= diameter || local.z >= diameter)
                            {
                                continue;
                            }

                            int ix = (int)(local.x / cellSize.x);
                            int iy = (int)(local.y / cellSize.y);
                            int iz = (int)(local.z / cellSize.z);
                            if ((uint)ix >= (uint)dimensions.x || (uint)iy >= (uint)dimensions.y || (uint)iz >= (uint)dimensions.z)
                                continue;

                            int index = ToDensityMapIndex(ix, iy, iz, dimensions.x, strideZ, cellCount);
                            if (index < 0)
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
            if (!math.isfinite(currentTimestamp) || !_transientEvents.IsCreated || _transientCellKeys.Length == 0)
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
                        if (IsTransientExpired(currentTimestamp, record.ExpirationTimestamp))
                            continue;

                        _transientEventsScratch.Add(cellKey, record);
                        if (_transientCellKeySetScratch.Add(cellKey))
                            _transientCellKeysScratch.AddNoResize(cellKey);
                    }
                    while (_transientEvents.TryGetNextValue(out record, ref iterator));
                }

                SwapTransientScratch();
            }
        }

        public void DecayTransientEvents(double currentTimestamp, float deltaTime, uint eventTypeMask, float decayScale, float minimumIntensity)
        {
            if (!math.isfinite(currentTimestamp) ||
                !math.isfinite(deltaTime) ||
                !math.isfinite(decayScale) ||
                !math.isfinite(minimumIntensity) ||
                !_transientEvents.IsCreated ||
                _transientCellKeys.Length == 0 ||
                deltaTime <= 0f)
                return;

            using (_transientPruneProfilerMarker.Auto())
            {
                float decayFactor = math.clamp(decayScale * deltaTime, 0f, 1f);
                float safeMinimumIntensity = math.max(0f, minimumIntensity);
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
                        if (IsTransientExpired(currentTimestamp, record.ExpirationTimestamp))
                            continue;

                        if ((record.EventTypeMask & eventTypeMask) != 0u)
                        {
                            record.Intensity *= decayFactor;
                            if (record.Intensity < safeMinimumIntensity)
                                continue;
                        }

                        _transientEventsScratch.Add(cellKey, record);
                        if (_transientCellKeySetScratch.Add(cellKey))
                            _transientCellKeysScratch.AddNoResize(cellKey);
                    }
                    while (_transientEvents.TryGetNextValue(out record, ref iterator));
                }

                SwapTransientScratch();
            }
        }

        public bool CompactIfOverCapacity(int capacityThreshold, int targetCapacityFloor, double currentTimestamp)
        {
            bool swapped = TrySwapCompletedCompaction();
            bool scheduled = ScheduleCompactionIfOverCapacity(capacityThreshold, targetCapacityFloor, currentTimestamp);
            return swapped || scheduled;
        }

        public bool ScheduleCompactionIfOverCapacity(int capacityThreshold, int targetCapacityFloor, double currentTimestamp)
        {
            if (!math.isfinite(currentTimestamp))
                return TrySwapCompletedCompaction();

            bool compacted = false;
            int safeThreshold = math.max(DefaultCompactionCapacityFloor, capacityThreshold);
            int safeFloor = math.max(DefaultCompactionCapacityFloor, targetCapacityFloor);

            if (_cellOccupancy.IsCreated && _cellOccupancy.Capacity > safeThreshold)
            {
                int targetCapacity = math.max(safeFloor, EstimateActiveCellOccupancyCapacity());
                if (targetCapacity < _cellOccupancy.Capacity && TryScheduleCellOccupancyCompaction(targetCapacity))
                {
                    compacted = true;
                }
            }

            if (_transientEvents.IsCreated && _transientEvents.Capacity > safeThreshold)
            {
                PruneExpiredTransientEvents(currentTimestamp);
                int liveTransientCount = math.max(1, _transientEvents.Count());
                int targetCapacity = math.max(safeFloor, liveTransientCount);
                if (targetCapacity < _transientEvents.Capacity)
                {
                    _transientEventsScratch.Clear();
                    _transientCellKeySetScratch.Clear();
                    _transientCellKeysScratch.Clear();
                    _transientEventsScratch.Capacity = targetCapacity;
                    NativeMemorySentinel.RefreshNativeParallelMultiHashMap(_transientEventsScratch, _sentinelOwner, nameof(_transientEventsScratch));

                    for (int i = 0; i < _transientCellKeys.Length; i++)
                    {
                        uint cellKey = _transientCellKeys[i];
                        if (!_transientEvents.TryGetFirstValue(cellKey, out TransientEventRecord record, out NativeParallelMultiHashMapIterator<uint> iterator))
                            continue;

                        do
                        {
                            _transientEventsScratch.Add(cellKey, record);
                            if (_transientCellKeySetScratch.Add(cellKey))
                                _transientCellKeysScratch.AddNoResize(cellKey);
                        }
                        while (_transientEvents.TryGetNextValue(out record, ref iterator));
                    }

                    SwapTransientScratch();
                    _transientEventsScratch.Clear();
                    _transientEventsScratch.Capacity = targetCapacity;
                    NativeMemorySentinel.RefreshNativeParallelMultiHashMap(_transientEventsScratch, _sentinelOwner, nameof(_transientEventsScratch));
                    compacted = true;
                }
            }

            return compacted;
        }

        public bool TrySwapCompletedCompaction()
        {
            if (!_cellCompactionScheduled)
                return false;

            if (!_cellCompactionHandle.IsCompleted || !_readerFence.IsCompleted)
                return false;

            if (!DispatcherJobSwap.TryComplete(ref _readerFence, forceComplete: false) ||
                !DispatcherJobSwap.TryComplete(ref _cellCompactionHandle, forceComplete: false))
            {
                return false;
            }

            if (_compactionMutationVersion != _mutationVersion)
            {
                _cellOccupancyScratch.Clear();
                _cellCompactionScheduled = false;
                _pendingCellCompactionTargetCapacity = 0;
                _compactionMutationVersion = 0u;
                _readerFence = default;
                _cellCompactionHandle = default;
                return false;
            }

            NativeParallelMultiHashMap<Long3, int> occupancySwap = _cellOccupancy;
            _cellOccupancy = _cellOccupancyScratch;
            _cellOccupancyScratch = occupancySwap;
            RefreshCellOccupancySentinelCapacities();
            _cellOccupancyScratch.Clear();
            if (_pendingCellCompactionTargetCapacity > 0 && _cellOccupancyScratch.Capacity != _pendingCellCompactionTargetCapacity)
            {
                _cellOccupancyScratch.Capacity = _pendingCellCompactionTargetCapacity;
                RefreshCellOccupancySentinelCapacities();
            }

            _cellCompactionScheduled = false;
            _pendingCellCompactionTargetCapacity = 0;
            _compactionMutationVersion = 0u;
            _readerFence = default;
            _cellCompactionHandle = default;
            return true;
        }

        public void RegisterReaderFence(JobHandle readerFence)
        {
            _readerFence = JobHandle.CombineDependencies(_readerFence, readerFence);
        }

        public void ClearTransientEvents(uint eventTypeMask, uint sourceKey, double currentTimestamp)
        {
            if (eventTypeMask == 0u ||
                sourceKey == 0u ||
                !math.isfinite(currentTimestamp) ||
                !_transientEvents.IsCreated ||
                _transientCellKeys.Length == 0)
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
                        if (IsTransientExpired(currentTimestamp, record.ExpirationTimestamp))
                            continue;

                        if (record.SourceKey == sourceKey && (record.EventTypeMask & eventTypeMask) != 0u)
                            continue;

                        _transientEventsScratch.Add(cellKey, record);
                        if (_transientCellKeySetScratch.Add(cellKey))
                            _transientCellKeysScratch.AddNoResize(cellKey);
                    }
                    while (_transientEvents.TryGetNextValue(out record, ref iterator));
                }

                SwapTransientScratch();
            }
        }

        public int CollectSphere(in AbsoluteUniversePosition origin, float radiusMeters, int requiredKindMask, ulong interactionFilter, NativeList<int> resultHandles)
        {
            if (!resultHandles.IsCreated)
            {
                _lastQueryStats = default;
                return 0;
            }

            using (_queryProfilerMarker.Auto())
            {
                resultHandles.Clear();
                if (!IsFiniteAup(in origin) ||
                    !math.isfinite(radiusMeters) ||
                    radiusMeters <= 0f ||
                    !_cellOccupancy.IsCreated ||
                    _entryHandles.Length == 0)
                {
                    _lastQueryStats = default;
                    return 0;
                }

                double3 absoluteCenter = origin.ToAbsoluteDouble3();
                double radius = math.max(0.001d, radiusMeters);
                Long3 minCell = ToCell(new double3(absoluteCenter.x - radius, absoluteCenter.y - radius, absoluteCenter.z - radius));
                Long3 maxCell = ToCell(new double3(absoluteCenter.x + radius, absoluteCenter.y + radius, absoluteCenter.z + radius));
                int cellSpan = EstimateCellSpan(minCell, maxCell);
                if (cellSpan > MaxSphereQueryCellSpan)
                {
                    _lastQueryStats = new QueryStats(QueryModeSphere, 0, 0, 0, 0, 1);
                    return 0;
                }

                ulong resolvedInteractionFilter = interactionFilter;

                _queryScratch.Reset();
                int dedupeCount = 0;
                int dedupeCapacity = _queryScratch.Dedup.Capacity;
                int resultCapacity = _queryScratch.Handles.Capacity;
                int visitedCellCount = 0;
                int candidateHandleCount = 0;

                for (long z = minCell.Z; z <= maxCell.Z && _queryScratch.Handles.Length < resultCapacity && dedupeCount < dedupeCapacity; z++)
                {
                    for (long y = minCell.Y; y <= maxCell.Y && _queryScratch.Handles.Length < resultCapacity && dedupeCount < dedupeCapacity; y++)
                    {
                        for (long x = minCell.X; x <= maxCell.X && _queryScratch.Handles.Length < resultCapacity && dedupeCount < dedupeCapacity; x++)
                        {
                            visitedCellCount++;
                            Long3 cellKey = new Long3(x, y, z);
                            if (!_cellOccupancy.TryGetFirstValue(cellKey, out int handle, out NativeParallelMultiHashMapIterator<Long3> iterator))
                                continue;

                            do
                            {
                                candidateHandleCount++;
                                if (dedupeCount >= dedupeCapacity)
                                    continue;

                                if (!_queryScratch.Dedup.Add(handle))
                                    continue;
                                dedupeCount++;

                                if (!_entries.TryGetValue(handle, out SpatialEntry entry))
                                    continue;

                                if (requiredKindMask != 0 && (entry.KindMask & requiredKindMask) == 0)
                                    continue;

                                if (resolvedInteractionFilter != 0UL && (entry.EntityFlags & resolvedInteractionFilter) != resolvedInteractionFilter)
                                    continue;

                                if (!SphereOverlapsEntry(absoluteCenter, radius * radius, in entry))
                                    continue;

                                if (_queryScratch.Handles.Length >= resultCapacity)
                                    continue;

                                _queryScratch.Handles.AddNoResize(handle);
                            }
                            while (_queryScratch.Handles.Length < resultCapacity &&
                                   dedupeCount < dedupeCapacity &&
                                   _cellOccupancy.TryGetNextValue(out handle, ref iterator));
                        }
                    }
                }

                int copyCount = math.min(_queryScratch.Handles.Length, resultHandles.Capacity);
                resultHandles.ResizeUninitialized(copyCount);
                NativeArray<int>.Copy(_queryScratch.Handles.AsArray(), resultHandles.AsArray(), copyCount);
                byte saturated = (_queryScratch.Handles.Length >= resultCapacity ||
                                  dedupeCount >= dedupeCapacity ||
                                  copyCount < _queryScratch.Handles.Length)
                    ? (byte)1
                    : (byte)0;
                _lastQueryStats = new QueryStats(
                    QueryModeSphere,
                    visitedCellCount,
                    candidateHandleCount,
                    dedupeCount,
                    copyCount,
                    saturated);
                return copyCount;
            }
        }

        public int CollectSphere(in AbsoluteUniversePosition origin, float radiusMeters, int requiredKindMask, uint interactionFilter, NativeList<int> resultHandles)
        {
            return CollectSphere(in origin, radiusMeters, requiredKindMask, (ulong)interactionFilter, resultHandles);
        }

        public int CollectSphere(in AbsoluteUniversePosition origin, float radiusMeters, int requiredKindMask, NativeList<int> resultHandles)
        {
            return CollectSphere(in origin, radiusMeters, requiredKindMask, 0UL, resultHandles);
        }

        public int CollectAdjacentCells(in AbsoluteUniversePosition origin, int requiredKindMask, ulong interactionFilter, NativeList<int> resultHandles)
        {
            if (!resultHandles.IsCreated)
            {
                _lastQueryStats = default;
                return 0;
            }

            using (_queryAdjacentProfilerMarker.Auto())
            {
                resultHandles.Clear();
                if (!IsFiniteAup(in origin) || !_cellOccupancy.IsCreated || _entryHandles.Length == 0)
                {
                    _lastQueryStats = default;
                    return 0;
                }

                Long3 centerCell = ToCell(origin.ToAbsoluteDouble3());
                ulong resolvedInteractionFilter = interactionFilter;

                _queryScratch.Reset();
                int dedupeCount = 0;
                int dedupeCapacity = _queryScratch.Dedup.Capacity;
                int resultCapacity = _queryScratch.Handles.Capacity;
                int visitedCellCount = 0;
                int candidateHandleCount = 0;

                for (long z = centerCell.Z - 1L; z <= centerCell.Z + 1L && _queryScratch.Handles.Length < resultCapacity && dedupeCount < dedupeCapacity; z++)
                {
                    for (long y = centerCell.Y - 1L; y <= centerCell.Y + 1L && _queryScratch.Handles.Length < resultCapacity && dedupeCount < dedupeCapacity; y++)
                    {
                        for (long x = centerCell.X - 1L; x <= centerCell.X + 1L && _queryScratch.Handles.Length < resultCapacity && dedupeCount < dedupeCapacity; x++)
                        {
                            visitedCellCount++;
                            Long3 cellKey = new Long3(x, y, z);
                            if (!_cellOccupancy.TryGetFirstValue(cellKey, out int handle, out NativeParallelMultiHashMapIterator<Long3> iterator))
                                continue;

                            do
                            {
                                candidateHandleCount++;
                                if (dedupeCount >= dedupeCapacity)
                                    continue;

                                if (!_queryScratch.Dedup.Add(handle))
                                    continue;
                                dedupeCount++;

                                if (!_entries.TryGetValue(handle, out SpatialEntry entry))
                                    continue;

                                if (requiredKindMask != 0 && (entry.KindMask & requiredKindMask) == 0)
                                    continue;

                                if (resolvedInteractionFilter != 0UL && (entry.EntityFlags & resolvedInteractionFilter) != resolvedInteractionFilter)
                                    continue;

                                if (_queryScratch.Handles.Length >= resultCapacity)
                                    continue;

                                _queryScratch.Handles.AddNoResize(handle);
                            }
                            while (_queryScratch.Handles.Length < resultCapacity &&
                                   dedupeCount < dedupeCapacity &&
                                   _cellOccupancy.TryGetNextValue(out handle, ref iterator));
                        }
                    }
                }

                int copyCount = math.min(_queryScratch.Handles.Length, resultHandles.Capacity);
                resultHandles.ResizeUninitialized(copyCount);
                NativeArray<int>.Copy(_queryScratch.Handles.AsArray(), resultHandles.AsArray(), copyCount);
                byte saturated = (_queryScratch.Handles.Length >= resultCapacity ||
                                  dedupeCount >= dedupeCapacity ||
                                  copyCount < _queryScratch.Handles.Length)
                    ? (byte)1
                    : (byte)0;
                _lastQueryStats = new QueryStats(
                    QueryModeAdjacent,
                    visitedCellCount,
                    candidateHandleCount,
                    dedupeCount,
                    copyCount,
                    saturated);
                return copyCount;
            }
        }

        public int CollectAdjacentCells(in AbsoluteUniversePosition origin, int requiredKindMask, NativeList<int> resultHandles)
        {
            return CollectAdjacentCells(in origin, requiredKindMask, 0UL, resultHandles);
        }

        public void Dispose()
        {
            JobHandle disposeHandle = CancelPendingJobsForTeardown();

            if (_entries.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelHashMap(_sentinelOwner, nameof(_entries));
                disposeHandle = _entries.Dispose(disposeHandle);
                _entries = default;
            }

            if (_entryHandles.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(_sentinelOwner, nameof(_entryHandles));
                disposeHandle = _entryHandles.Dispose(disposeHandle);
                _entryHandles = default;
            }

            if (_freeHandles.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(_sentinelOwner, nameof(_freeHandles));
                disposeHandle = _freeHandles.Dispose(disposeHandle);
                _freeHandles = default;
            }

            if (_queuedFreeHandles.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelHashSet(_sentinelOwner, nameof(_queuedFreeHandles));
                disposeHandle = _queuedFreeHandles.Dispose(disposeHandle);
                _queuedFreeHandles = default;
            }

            if (_slotGenerations.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelHashMap(_sentinelOwner, nameof(_slotGenerations));
                disposeHandle = _slotGenerations.Dispose(disposeHandle);
                _slotGenerations = default;
            }

            if (_cellOccupancy.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelMultiHashMap(_sentinelOwner, nameof(_cellOccupancy));
                disposeHandle = _cellOccupancy.Dispose(disposeHandle);
                _cellOccupancy = default;
            }

            if (_cellOccupancyScratch.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelMultiHashMap(_sentinelOwner, nameof(_cellOccupancyScratch));
                disposeHandle = _cellOccupancyScratch.Dispose(disposeHandle);
                _cellOccupancyScratch = default;
            }

            if (_transientEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelMultiHashMap(_sentinelOwner, nameof(_transientEvents));
                disposeHandle = _transientEvents.Dispose(disposeHandle);
                _transientEvents = default;
            }

            if (_transientEventsScratch.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelMultiHashMap(_sentinelOwner, nameof(_transientEventsScratch));
                disposeHandle = _transientEventsScratch.Dispose(disposeHandle);
                _transientEventsScratch = default;
            }

            if (_transientCellKeySet.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelHashSet(_sentinelOwner, nameof(_transientCellKeySet));
                disposeHandle = _transientCellKeySet.Dispose(disposeHandle);
                _transientCellKeySet = default;
            }

            if (_transientCellKeySetScratch.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelHashSet(_sentinelOwner, nameof(_transientCellKeySetScratch));
                disposeHandle = _transientCellKeySetScratch.Dispose(disposeHandle);
                _transientCellKeySetScratch = default;
            }

            if (_transientQueryDedupe.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelHashSet(_sentinelOwner, nameof(_transientQueryDedupe));
                disposeHandle = _transientQueryDedupe.Dispose(disposeHandle);
                _transientQueryDedupe = default;
            }

            if (_transientCellKeys.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(_sentinelOwner, nameof(_transientCellKeys));
                disposeHandle = _transientCellKeys.Dispose(disposeHandle);
                _transientCellKeys = default;
            }

            if (_transientCellKeysScratch.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(_sentinelOwner, nameof(_transientCellKeysScratch));
                disposeHandle = _transientCellKeysScratch.Dispose(disposeHandle);
                _transientCellKeysScratch = default;
            }

            if (_compactionHandleSnapshot.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(_sentinelOwner, nameof(_compactionHandleSnapshot));
                disposeHandle = _compactionHandleSnapshot.Dispose(disposeHandle);
                _compactionHandleSnapshot = default;
            }

            if (_compactionEntrySnapshot.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(_sentinelOwner, nameof(_compactionEntrySnapshot));
                disposeHandle = _compactionEntrySnapshot.Dispose(disposeHandle);
                _compactionEntrySnapshot = default;
            }

            disposeHandle = _queryScratch.Dispose(disposeHandle);
            JobHandle.ScheduleBatchedJobs();
        }

        private JobHandle CancelPendingJobsForTeardown()
        {
            JobHandle dependency = JobHandle.CombineDependencies(_cellCompactionHandle, _readerFence);
            _cellCompactionHandle = default;
            _readerFence = default;
            _cellCompactionScheduled = false;
            _pendingCellCompactionTargetCapacity = 0;
            _compactionMutationVersion = 0u;
            return dependency;
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

        private static void PrewarmFreeHandleQueue(ref NativeQueue<uint> queue, int capacity)
        {
            if (!queue.IsCreated)
                return;

            int safeCapacity = math.max(0, capacity);
            for (int i = 0; i < safeCapacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }

        private bool UpsertInternal(int handle, in AbsoluteUniversePosition position, float3 halfExtents, int kindMask, ulong entityFlags, int payloadId, bool appendHandle)
        {
            Long3 minCell;
            Long3 maxCell;
            double3 absoluteCenter = position.ToAbsoluteDouble3();
            ResolveCellRange(absoluteCenter, halfExtents, out minCell, out maxCell);
            if (EstimateCellSpan(minCell, maxCell) > MaxRegisteredEntryCellSpan)
                return false;

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
                _entryHandles.AddNoResize(handle);
            AddEntryCells(handle, in entry);
            _mutationVersion++;
            return true;
        }

        private void EnsureCapacityForEntry(Long3 minCell, Long3 maxCell, int additionalEntries)
        {
            int requiredEntryCapacity = AddCapacitySaturating(_entryHandles.Length, math.max(0, additionalEntries));
            if (_entries.Capacity < requiredEntryCapacity)
            {
                _entries.Capacity = GrowCapacity(_entries.Capacity, requiredEntryCapacity);
                NativeMemorySentinel.RefreshNativeParallelHashMap(_entries, _sentinelOwner, nameof(_entries));
            }

            if (_entryHandles.Capacity < requiredEntryCapacity)
            {
                _entryHandles.Capacity = GrowCapacity(_entryHandles.Capacity, requiredEntryCapacity);
                NativeMemorySentinel.RefreshNativeList(_entryHandles, _sentinelOwner, nameof(_entryHandles));
            }

            if (_queuedFreeHandles.Capacity < requiredEntryCapacity)
            {
                _queuedFreeHandles.Capacity = GrowCapacity(_queuedFreeHandles.Capacity, requiredEntryCapacity);
                NativeMemorySentinel.RefreshNativeParallelHashSet(_queuedFreeHandles, _sentinelOwner, nameof(_queuedFreeHandles));
            }

            if (_slotGenerations.Capacity < requiredEntryCapacity)
            {
                _slotGenerations.Capacity = GrowCapacity(_slotGenerations.Capacity, requiredEntryCapacity);
                NativeMemorySentinel.RefreshNativeParallelHashMap(_slotGenerations, _sentinelOwner, nameof(_slotGenerations));
            }

            int cellSpan = EstimateCellSpan(minCell, maxCell);
            int requiredCellCapacity = AddCapacitySaturating(_cellOccupancy.Count(), cellSpan);
            if (_cellOccupancy.Capacity < requiredCellCapacity)
            {
                _cellOccupancy.Capacity = GrowCapacity(_cellOccupancy.Capacity, requiredCellCapacity);
                NativeMemorySentinel.RefreshNativeParallelMultiHashMap(_cellOccupancy, _sentinelOwner, nameof(_cellOccupancy));
            }
        }

        private void EnsureTransientCapacity(int additionalCells)
        {
            int requiredCapacity = AddCapacitySaturating(_transientEvents.Count(), math.max(1, additionalCells));
            if (_transientEvents.Capacity < requiredCapacity)
            {
                _transientEvents.Capacity = GrowCapacity(_transientEvents.Capacity, requiredCapacity);
                NativeMemorySentinel.RefreshNativeParallelMultiHashMap(_transientEvents, _sentinelOwner, nameof(_transientEvents));
            }

            if (_transientEventsScratch.Capacity < requiredCapacity)
            {
                _transientEventsScratch.Capacity = GrowCapacity(_transientEventsScratch.Capacity, requiredCapacity);
                NativeMemorySentinel.RefreshNativeParallelMultiHashMap(_transientEventsScratch, _sentinelOwner, nameof(_transientEventsScratch));
            }

            int requiredKeyCapacity = AddCapacitySaturating(_transientCellKeys.Length, math.max(1, additionalCells));
            if (_transientCellKeySet.Capacity < requiredKeyCapacity)
            {
                _transientCellKeySet.Capacity = GrowCapacity(_transientCellKeySet.Capacity, requiredKeyCapacity);
                NativeMemorySentinel.RefreshNativeParallelHashSet(_transientCellKeySet, _sentinelOwner, nameof(_transientCellKeySet));
            }

            if (_transientCellKeySetScratch.Capacity < requiredKeyCapacity)
            {
                _transientCellKeySetScratch.Capacity = GrowCapacity(_transientCellKeySetScratch.Capacity, requiredKeyCapacity);
                NativeMemorySentinel.RefreshNativeParallelHashSet(_transientCellKeySetScratch, _sentinelOwner, nameof(_transientCellKeySetScratch));
            }

            if (_transientQueryDedupe.Capacity < requiredCapacity)
            {
                _transientQueryDedupe.Capacity = GrowCapacity(_transientQueryDedupe.Capacity, requiredCapacity);
                NativeMemorySentinel.RefreshNativeParallelHashSet(_transientQueryDedupe, _sentinelOwner, nameof(_transientQueryDedupe));
            }

            if (_transientCellKeys.Capacity < requiredKeyCapacity)
            {
                _transientCellKeys.Capacity = GrowCapacity(_transientCellKeys.Capacity, requiredKeyCapacity);
                NativeMemorySentinel.RefreshNativeList(_transientCellKeys, _sentinelOwner, nameof(_transientCellKeys));
            }

            if (_transientCellKeysScratch.Capacity < requiredKeyCapacity)
            {
                _transientCellKeysScratch.Capacity = GrowCapacity(_transientCellKeysScratch.Capacity, requiredKeyCapacity);
                NativeMemorySentinel.RefreshNativeList(_transientCellKeysScratch, _sentinelOwner, nameof(_transientCellKeysScratch));
            }
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
                            targetKeyList.AddNoResize(cellKey);
                    }
                }
            }
        }

        private bool TryScheduleCellOccupancyCompaction(int targetCapacity)
        {
            if (_cellCompactionScheduled || !_cellOccupancyScratch.IsCreated)
                return false;

            int entryCount = _entryHandles.Length;
            if (entryCount <= 0)
                return false;

            if (_cellOccupancyScratch.Capacity != targetCapacity)
            {
                _cellOccupancyScratch.Capacity = targetCapacity;
                NativeMemorySentinel.RefreshNativeParallelMultiHashMap(_cellOccupancyScratch, _sentinelOwner, nameof(_cellOccupancyScratch));
            }

            _cellOccupancyScratch.Clear();
            if (_compactionHandleSnapshot.Capacity < entryCount)
            {
                _compactionHandleSnapshot.Capacity = entryCount;
                NativeMemorySentinel.RefreshNativeList(_compactionHandleSnapshot, _sentinelOwner, nameof(_compactionHandleSnapshot));
            }
            if (_compactionEntrySnapshot.Capacity < entryCount)
            {
                _compactionEntrySnapshot.Capacity = entryCount;
                NativeMemorySentinel.RefreshNativeList(_compactionEntrySnapshot, _sentinelOwner, nameof(_compactionEntrySnapshot));
            }

            _compactionHandleSnapshot.ResizeUninitialized(entryCount);
            _compactionEntrySnapshot.ResizeUninitialized(entryCount);
            int writeIndex = 0;
            for (int i = 0; i < entryCount; i++)
            {
                int handle = _entryHandles[i];
                if (!_entries.TryGetValue(handle, out SpatialEntry entry))
                    continue;

                _compactionHandleSnapshot[writeIndex] = handle;
                _compactionEntrySnapshot[writeIndex] = entry;
                writeIndex++;
            }

            if (writeIndex <= 0)
                return false;

            _cellCompactionHandle = new RebuildCellOccupancyJob
            {
                Handles = _compactionHandleSnapshot.AsArray(),
                Entries = _compactionEntrySnapshot.AsArray(),
                Count = writeIndex,
                BackBuffer = _cellOccupancyScratch
            }.Schedule();
            _cellCompactionScheduled = true;
            _pendingCellCompactionTargetCapacity = targetCapacity;
            _compactionMutationVersion = _mutationVersion;
            return true;
        }

        private void TryEmitDisturbanceCascade(
            in TransientEventRecord triggerRecord,
            Long3 minCell,
            Long3 maxCell,
            double currentTimestamp,
            int cellSpan)
        {
            if ((triggerRecord.EventTypeMask & AcousticImpulseEventMask) == 0u ||
                triggerRecord.Intensity <= CascadeIntensityThreshold ||
                (triggerRecord.EventTypeMask & DisturbanceEventMask) != 0u)
            {
                return;
            }

            if (!HasHighIntensityChemicalInCells(minCell, maxCell, currentTimestamp))
                return;

            TransientEventRecord disturbanceRecord = new TransientEventRecord
            {
                EventId = AllocateTransientEventId(),
                AbsoluteCenter = triggerRecord.AbsoluteCenter,
                RadiusMeters = triggerRecord.RadiusMeters,
                Intensity = math.saturate(triggerRecord.Intensity * 0.75f),
                Temperature = triggerRecord.Temperature,
                ExpirationTimestamp = triggerRecord.ExpirationTimestamp,
                EventTypeMask = DisturbanceEventMask,
                EventFlags = triggerRecord.EventFlags,
                SourceKey = triggerRecord.SourceKey
            };

            EnsureTransientCapacity(cellSpan);
            AddTransientRecordToCells(in disturbanceRecord, minCell, maxCell, _transientEvents, _transientCellKeySet, _transientCellKeys);
        }

        private bool HasHighIntensityChemicalInCells(Long3 minCell, Long3 maxCell, double currentTimestamp)
        {
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
                            if (IsTransientExpired(currentTimestamp, record.ExpirationTimestamp))
                                continue;

                            if ((record.EventTypeMask & ChemicalScentEventMask) != 0u &&
                                record.Intensity > CascadeIntensityThreshold)
                            {
                                return true;
                            }
                        }
                        while (_transientEvents.TryGetNextValue(out record, ref iterator));
                    }
                }
            }

            return false;
        }

        private void AddEntryCells(int handle, in SpatialEntry entry)
        {
            AddEntryCells(handle, in entry, _cellOccupancy);
        }

        private static void AddEntryCells(int handle, in SpatialEntry entry, NativeParallelMultiHashMap<Long3, int> targetMap)
        {
            for (long z = entry.MinCell.Z; z <= entry.MaxCell.Z; z++)
            {
                for (long y = entry.MinCell.Y; y <= entry.MaxCell.Y; y++)
                {
                    for (long x = entry.MinCell.X; x <= entry.MaxCell.X; x++)
                    {
                        targetMap.Add(new Long3(x, y, z), handle);
                    }
                }
            }
        }

        private int EstimateActiveCellOccupancyCapacity()
        {
            int capacity = 1;
            for (int i = 0; i < _entryHandles.Length; i++)
            {
                int handle = _entryHandles[i];
                if (!_entries.TryGetValue(handle, out SpatialEntry entry))
                    continue;

                capacity = AddCapacitySaturating(capacity, EstimateCellSpan(entry.MinCell, entry.MaxCell));
            }

            return capacity;
        }

        private void SwapTransientScratch()
        {
            NativeParallelMultiHashMap<uint, TransientEventRecord> eventSwap = _transientEvents;
            _transientEvents = _transientEventsScratch;
            _transientEventsScratch = eventSwap;

            NativeParallelHashSet<uint> keySetSwap = _transientCellKeySet;
            _transientCellKeySet = _transientCellKeySetScratch;
            _transientCellKeySetScratch = keySetSwap;

            NativeList<uint> keyListSwap = _transientCellKeys;
            _transientCellKeys = _transientCellKeysScratch;
            _transientCellKeysScratch = keyListSwap;
            RefreshTransientSentinelCapacities();
        }

        private void RefreshCellOccupancySentinelCapacities()
        {
            NativeMemorySentinel.RefreshNativeParallelMultiHashMap(_cellOccupancy, _sentinelOwner, nameof(_cellOccupancy));
            NativeMemorySentinel.RefreshNativeParallelMultiHashMap(_cellOccupancyScratch, _sentinelOwner, nameof(_cellOccupancyScratch));
        }

        private void RefreshTransientSentinelCapacities()
        {
            NativeMemorySentinel.RefreshNativeParallelMultiHashMap(_transientEvents, _sentinelOwner, nameof(_transientEvents));
            NativeMemorySentinel.RefreshNativeParallelMultiHashMap(_transientEventsScratch, _sentinelOwner, nameof(_transientEventsScratch));
            NativeMemorySentinel.RefreshNativeParallelHashSet(_transientCellKeySet, _sentinelOwner, nameof(_transientCellKeySet));
            NativeMemorySentinel.RefreshNativeParallelHashSet(_transientCellKeySetScratch, _sentinelOwner, nameof(_transientCellKeySetScratch));
            NativeMemorySentinel.RefreshNativeList(_transientCellKeys, _sentinelOwner, nameof(_transientCellKeys));
            NativeMemorySentinel.RefreshNativeList(_transientCellKeysScratch, _sentinelOwner, nameof(_transientCellKeysScratch));
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
                ToCellIndex(absolutePosition.x * invCellSize),
                ToCellIndex(absolutePosition.y * invCellSize),
                ToCellIndex(absolutePosition.z * invCellSize));
        }

        private static long ToCellIndex(double scaledPosition)
        {
            if (!math.isfinite(scaledPosition))
                return 0L;

            double floored = math.floor(scaledPosition);
            if (floored <= MinSafeCellIndex)
                return MinSafeCellIndex;
            if (floored >= MaxSafeCellIndex)
                return MaxSafeCellIndex;
            return (long)floored;
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

        private static bool IsTransientExpired(double currentTimestamp, double expirationTimestamp)
        {
            return !(currentTimestamp < expirationTimestamp);
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
            double spanX = math.max(1d, ((double)maxCell.X - minCell.X) + 1d);
            double spanY = math.max(1d, ((double)maxCell.Y - minCell.Y) + 1d);
            double spanZ = math.max(1d, ((double)maxCell.Z - minCell.Z) + 1d);
            double total = spanX * spanY * spanZ;
            return total >= int.MaxValue ? int.MaxValue : (int)total;
        }

        private static bool TryResolveDensityGridLayout(int3 dimensions, int bufferLength, out int cellCount, out int strideZ)
        {
            cellCount = 0;
            strideZ = 0;
            if (bufferLength <= 0 || dimensions.x <= 0 || dimensions.y <= 0 || dimensions.z <= 0)
                return false;

            long xyStride = (long)dimensions.x * dimensions.y;
            long totalCells = xyStride * dimensions.z;
            if (xyStride <= 0L || xyStride > int.MaxValue || totalCells <= 0L || totalCells > int.MaxValue)
                return false;

            strideZ = (int)xyStride;
            cellCount = bufferLength < totalCells ? bufferLength : (int)totalCells;
            return cellCount > 0;
        }

        private static int ToDensityMapIndex(int x, int y, int z, int strideY, int strideZ, int cellCount)
        {
            long index = x + ((long)y * strideY) + ((long)z * strideZ);
            return index >= 0L && index < cellCount && index <= int.MaxValue ? (int)index : -1;
        }

        private static int AddCapacitySaturating(int current, int additional)
        {
            int safeAdditional = math.max(0, additional);
            return current >= int.MaxValue - safeAdditional ? int.MaxValue : current + safeAdditional;
        }

        private static int GrowCapacity(int currentCapacity, int requiredCapacity)
        {
            int safeCurrent = math.max(1, currentCapacity);
            int doubled = safeCurrent >= (int.MaxValue >> 1) ? int.MaxValue : safeCurrent << 1;
            return math.max(requiredCapacity, doubled);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.all(math.isfinite(new float3(position.LocalX, position.LocalY, position.LocalZ)));
        }

        private static bool IsFiniteFloat3(float3 value)
        {
            return math.all(math.isfinite(value));
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
