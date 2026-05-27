using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
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
    /// AUP-aware broadphase storing entry bounds in 64-bit world space instead of presentation-space Unity transforms.
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
            [FieldOffset(24)] public Long3 MinCell;
            [FieldOffset(48)] public Long3 MaxCell;
            [FieldOffset(72)] public ulong EntityFlags;
            [FieldOffset(80)] public float3 HalfExtents;
            [FieldOffset(92)] public int KindMask;
            [FieldOffset(96)] public int PayloadId;
            [FieldOffset(100)] private uint _pad0;
            [FieldOffset(104)] private uint _pad1;
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
            [FieldOffset(20)] private readonly ushort _pad0;
            [FieldOffset(22)] public readonly byte Saturated;
            [FieldOffset(23)] private readonly byte _pad1;

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
            [FieldOffset(0)] public double3 AbsoluteCenter;
            [FieldOffset(24)] public double ExpirationTimestamp;
            [FieldOffset(32)] public ulong EventFlags;
            [FieldOffset(40)] public uint EventId;
            [FieldOffset(44)] public float RadiusMeters;
            [FieldOffset(48)] public float Intensity;
            [FieldOffset(52)] public float Temperature;
            [FieldOffset(56)] public uint EventTypeMask;
            [FieldOffset(60)] public uint SourceKey;
            [FieldOffset(64)] private uint _pad0;
            [FieldOffset(68)] private uint _pad1;
            [FieldOffset(72)] private uint _pad2;
            [FieldOffset(76)] private uint _pad3;
        }

        private struct QueryScratchArena : IDisposable
        {
            public int[] Handles;
            public int[] Dedup;
            public int HandleCount;
            public int DedupCount;

            public QueryScratchArena(int initialCapacity, string sentinelOwner, NativeAllocationLifetime lifetime)
            {
                int safeCapacity = math.max(1, initialCapacity);
                _ = sentinelOwner;
                _ = lifetime;
                // COLD ALLOC: int[safeCapacity] - managed query result staging arena for AUP spatial overlap queries - owner: HectonSpatialHash
                Handles = new int[safeCapacity];
                // COLD ALLOC: int[safeCapacity] - managed dedupe arena for multi-cell overlap queries - owner: HectonSpatialHash
                Dedup = new int[safeCapacity];
                HandleCount = 0;
                DedupCount = 0;
            }

            public int Capacity => Handles != null ? Handles.Length : 0;

            public bool TryAddDedupe(int handle)
            {
                int count = DedupCount;
                for (int i = 0; i < count; i++)
                {
                    if (Dedup[i] == handle)
                        return false;
                }

                if (count >= Capacity)
                    return false;

                Dedup[count] = handle;
                DedupCount = count + 1;
                return true;
            }

            public bool TryAddHandle(int handle)
            {
                int count = HandleCount;
                if (count >= Capacity)
                    return false;

                Handles[count] = handle;
                HandleCount = count + 1;
                return true;
            }

            public void Reset()
            {
                HandleCount = 0;
                DedupCount = 0;
            }

            public void Dispose()
            {
                Handles = null;
                Dedup = null;
                HandleCount = 0;
                DedupCount = 0;
            }

            public JobHandle Dispose(JobHandle dependency)
            {
                Dispose();
                return dependency;
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
        private const int MaxRegisteredEntryCellSpan = 4096;
        private const int MaxTransientEventCellSpan = 8192;
        private const int MaxSphereQueryCellSpan = 8192;
        private const long MinSafeCellIndex = long.MinValue + 2L;
        private const long MaxSafeCellIndex = long.MaxValue - 2L;
        private const uint AcousticImpulseEventMask = 1u << 0;
        private const uint ChemicalScentEventMask = 1u << 1;
        private const uint DisturbanceEventMask = 1u << 3;
        private const float CascadeIntensityThreshold = 0.8f;
        private const int QueryModeSphere = 1;
        private const int QueryModeAdjacent = 2;
        private static readonly ProfilerMarker _registerProfilerMarker = new ProfilerMarker("H8.World.AupSpatialHash.Register");
        private static readonly ProfilerMarker _updateProfilerMarker = new ProfilerMarker("H8.World.AupSpatialHash.Update");
        private static readonly ProfilerMarker _queryProfilerMarker = new ProfilerMarker("H8.World.AupSpatialHash.QuerySphere");
        private static readonly ProfilerMarker _queryAdjacentProfilerMarker = new ProfilerMarker("H8.World.AupSpatialHash.QueryAdjacentCells");
        private static readonly ProfilerMarker _transientRegisterProfilerMarker = new ProfilerMarker("H8.World.AupSpatialHash.TransientRegister");
        private static readonly ProfilerMarker _transientQueryProfilerMarker = new ProfilerMarker("H8.World.AupSpatialHash.TransientQuery");
        private static readonly ProfilerMarker _transientPruneProfilerMarker = new ProfilerMarker("H8.World.AupSpatialHash.TransientPrune");

        private readonly double _cellSizeMeters;
        private TransientEventRecord[] _transientEvents;
        private SpatialEntry[] _entries;
        private byte[] _entryOccupied;
        private int[] _entryHandles;
        private uint[] _slotGenerations;
        private QueryScratchArena _queryScratch;
        private readonly uint[] _freeHandles;
        private static int _nextSentinelInstanceId;
        private readonly string _sentinelOwner;
        private readonly NativeAllocationLifetime _allocationLifetime;
        private uint _nextSlot;
        private uint _nextTransientEventId;
        private uint _mutationVersion;
        private int _entryHandleCount;
        private int _transientEventCount;
        private int _freeHandleHead;
        private int _freeHandleTail;
        private int _freeHandleCount;
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
            int safeGenerationCapacity = safeEntryCapacity >= int.MaxValue ? int.MaxValue : safeEntryCapacity + 1;
            // COLD ALLOC: SpatialEntry[safeEntryCapacity + 1] - managed slot-indexed AUP spatial registry records - owner: HectonSpatialHash
            _entries = new SpatialEntry[safeGenerationCapacity];
            // COLD ALLOC: byte[safeEntryCapacity + 1] - managed spatial slot occupancy flags - owner: HectonSpatialHash
            _entryOccupied = new byte[safeGenerationCapacity];
            // COLD ALLOC: int[safeEntryCapacity] - managed dense active-handle list for zero-alloc hash rebuilds - owner: HectonSpatialHash
            _entryHandles = new int[safeEntryCapacity];
            // COLD ALLOC: uint[safeEntryCapacity + 1] - managed current generation per spatial handle slot; slot zero is invalid - owner: HectonSpatialHash
            _slotGenerations = new uint[safeGenerationCapacity];
            int safeTransientEventCapacity = math.max(DefaultTransientCellCapacity, safeCellCapacity);
            // COLD ALLOC: TransientEventRecord[safeTransientEventCapacity] - managed transient visual/acoustic event records - owner: HectonSpatialHash
            _transientEvents = new TransientEventRecord[safeTransientEventCapacity];
            _queryScratch = new QueryScratchArena(safeEntryCapacity, _sentinelOwner, _allocationLifetime);
            // COLD ALLOC: uint[safeEntryCapacity] - managed generation-counted free-handle ring for long-session spatial churn - owner: HectonSpatialHash
            _freeHandles = new uint[safeEntryCapacity];
            _nextSlot = 1u;
            _nextTransientEventId = 1u;
        }

        public int EntryCount => _entryHandleCount;
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
                bool appendHandle = !ContainsEntry(handle);
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
            if (handle <= 0 || !TryGetEntryByHandle(handle, out SpatialEntry existingEntry))
                return;

            _ = existingEntry;
            ClearEntry(handle);

            RemoveEntryHandle(handle);

            if (recycleHandle)
                RecycleHandle(handle);

            _mutationVersion++;
        }

        public bool TryGetEntry(int handle, out SpatialEntry entry)
        {
            return TryGetEntryByHandle(handle, out entry);
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

                if (!TryAppendTransientEvent(in record))
                    return;

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
                    _transientEventCount == 0)
                    return 0;

                double3 absoluteCenter = origin.ToAbsoluteDouble3();
                double radius = math.max(0.001d, radiusMeters);
                Long3 minCell = ToCell(absoluteCenter - new double3(radius, radius, radius));
                Long3 maxCell = ToCell(absoluteCenter + new double3(radius, radius, radius));
                if (EstimateCellSpan(minCell, maxCell) > MaxSphereQueryCellSpan)
                    return 0;

                int eventCount = math.min(math.max(0, _transientEventCount), _transientEvents != null ? _transientEvents.Length : 0);
                for (int i = 0; i < eventCount; i++)
                {
                    TransientEventRecord record = _transientEvents[i];
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
                _transientEventCount == 0)
                return false;

            using (_transientQueryProfilerMarker.Auto())
            {
                double3 absoluteCenter = origin.ToAbsoluteDouble3();
                double radius = math.max(0.001d, radiusMeters);
                Long3 minCell = ToCell(absoluteCenter - new double3(radius, radius, radius));
                Long3 maxCell = ToCell(absoluteCenter + new double3(radius, radius, radius));
                if (EstimateCellSpan(minCell, maxCell) > MaxSphereQueryCellSpan)
                    return false;

                double accumulatedWeight = 0d;
                double3 accumulatedGradient = double3.zero;

                int eventCount = math.min(math.max(0, _transientEventCount), _transientEvents != null ? _transientEvents.Length : 0);
                for (int i = 0; i < eventCount; i++)
                {
                    TransientEventRecord record = _transientEvents[i];
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

            if (_transientEventCount == 0 || cellCount <= 0)
                return;

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

            int eventCount = math.min(math.max(0, _transientEventCount), _transientEvents != null ? _transientEvents.Length : 0);
            for (int i = 0; i < eventCount; i++)
            {
                TransientEventRecord record = _transientEvents[i];
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
        }

        public void BuildAcousticDensityMap(
            in AbsoluteUniversePosition origin,
            float radiusMeters,
            double currentTimestamp,
            float[] densityMap,
            int3 dimensions,
            uint acousticEventTypeMask)
        {
            if (densityMap == null ||
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

            if (_transientEventCount == 0 || cellCount <= 0)
                return;

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

            int eventCount = math.min(math.max(0, _transientEventCount), _transientEvents != null ? _transientEvents.Length : 0);
            for (int i = 0; i < eventCount; i++)
            {
                TransientEventRecord record = _transientEvents[i];
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
        }

        public void PruneExpiredTransientEvents(double currentTimestamp)
        {
            if (!math.isfinite(currentTimestamp) || _transientEventCount == 0)
                return;

            using (_transientPruneProfilerMarker.Auto())
            {
                int eventCount = math.min(math.max(0, _transientEventCount), _transientEvents != null ? _transientEvents.Length : 0);
                int writeIndex = 0;
                for (int i = 0; i < eventCount; i++)
                {
                    TransientEventRecord record = _transientEvents[i];
                    if (IsTransientExpired(currentTimestamp, record.ExpirationTimestamp))
                        continue;

                    _transientEvents[writeIndex++] = record;
                }

                ClearTransientTail(writeIndex, eventCount);
                _transientEventCount = writeIndex;
            }
        }

        public void DecayTransientEvents(double currentTimestamp, float deltaTime, uint eventTypeMask, float decayScale, float minimumIntensity)
        {
            if (!math.isfinite(currentTimestamp) ||
                !math.isfinite(deltaTime) ||
                !math.isfinite(decayScale) ||
                !math.isfinite(minimumIntensity) ||
                _transientEventCount == 0 ||
                deltaTime <= 0f)
                return;

            using (_transientPruneProfilerMarker.Auto())
            {
                float decayFactor = math.clamp(decayScale * deltaTime, 0f, 1f);
                float safeMinimumIntensity = math.max(0f, minimumIntensity);
                int eventCount = math.min(math.max(0, _transientEventCount), _transientEvents != null ? _transientEvents.Length : 0);
                int writeIndex = 0;
                for (int i = 0; i < eventCount; i++)
                {
                    TransientEventRecord record = _transientEvents[i];
                    if (IsTransientExpired(currentTimestamp, record.ExpirationTimestamp))
                        continue;

                    if ((record.EventTypeMask & eventTypeMask) != 0u)
                    {
                        record.Intensity *= decayFactor;
                        if (record.Intensity < safeMinimumIntensity)
                            continue;
                    }

                    _transientEvents[writeIndex++] = record;
                }

                ClearTransientTail(writeIndex, eventCount);
                _transientEventCount = writeIndex;
            }
        }

        public bool CompactIfOverCapacity(int capacityThreshold, int targetCapacityFloor, double currentTimestamp)
        {
            bool scheduled = ScheduleCompactionIfOverCapacity(capacityThreshold, targetCapacityFloor, currentTimestamp);
            return scheduled;
        }

        public bool ScheduleCompactionIfOverCapacity(int capacityThreshold, int targetCapacityFloor, double currentTimestamp)
        {
            if (!math.isfinite(currentTimestamp))
                return false;

            _ = capacityThreshold;
            _ = targetCapacityFloor;
            PruneExpiredTransientEvents(currentTimestamp);
            return false;
        }

        public bool TrySwapCompletedCompaction()
        {
            return false;
        }

        public void RegisterReaderFence(JobHandle readerFence)
        {
            _ = readerFence;
        }

        public void ClearTransientEvents(uint eventTypeMask, uint sourceKey, double currentTimestamp)
        {
            if (eventTypeMask == 0u ||
                sourceKey == 0u ||
                !math.isfinite(currentTimestamp) ||
                _transientEventCount == 0)
                return;

            using (_transientPruneProfilerMarker.Auto())
            {
                int eventCount = math.min(math.max(0, _transientEventCount), _transientEvents != null ? _transientEvents.Length : 0);
                int writeIndex = 0;
                for (int i = 0; i < eventCount; i++)
                {
                    TransientEventRecord record = _transientEvents[i];
                    if (IsTransientExpired(currentTimestamp, record.ExpirationTimestamp))
                        continue;

                    if (record.SourceKey == sourceKey && (record.EventTypeMask & eventTypeMask) != 0u)
                        continue;

                    _transientEvents[writeIndex++] = record;
                }

                ClearTransientTail(writeIndex, eventCount);
                _transientEventCount = writeIndex;
            }
        }

        private int CollectSphereIntoScratch(
            in AbsoluteUniversePosition origin,
            float radiusMeters,
            int requiredKindMask,
            ulong interactionFilter,
            out int visitedCellCount,
            out int candidateHandleCount,
            out int dedupeCount,
            out byte saturated)
        {
            visitedCellCount = 0;
            candidateHandleCount = 0;
            dedupeCount = 0;
            saturated = 0;
            _queryScratch.Reset();

            double3 absoluteCenter = origin.ToAbsoluteDouble3();
            double radius = math.max(0.001d, radiusMeters);
            Long3 minCell = ToCell(new double3(absoluteCenter.x - radius, absoluteCenter.y - radius, absoluteCenter.z - radius));
            Long3 maxCell = ToCell(new double3(absoluteCenter.x + radius, absoluteCenter.y + radius, absoluteCenter.z + radius));
            int cellSpan = EstimateCellSpan(minCell, maxCell);
            if (cellSpan > MaxSphereQueryCellSpan)
            {
                saturated = 1;
                return 0;
            }

            visitedCellCount = cellSpan;
            int resultCapacity = _queryScratch.Capacity;
            int entryCapacity = _entryHandles != null ? _entryHandles.Length : 0;
            int entryCount = math.min(math.max(0, _entryHandleCount), entryCapacity);
            double radiusSq = radius * radius;
            for (int i = 0; i < entryCount && _queryScratch.HandleCount < resultCapacity; i++)
            {
                int handle = _entryHandles[i];
                if (!TryGetEntryByHandle(handle, out SpatialEntry entry))
                    continue;

                candidateHandleCount++;
                dedupeCount++;
                if (requiredKindMask != 0 && (entry.KindMask & requiredKindMask) == 0)
                    continue;

                if (interactionFilter != 0UL && (entry.EntityFlags & interactionFilter) != interactionFilter)
                    continue;

                if (!SphereOverlapsEntry(absoluteCenter, radiusSq, in entry))
                    continue;

                _queryScratch.TryAddHandle(handle);
            }

            if (_queryScratch.HandleCount >= resultCapacity && candidateHandleCount < entryCount)
                saturated = 1;

            return _queryScratch.HandleCount;
        }

        private int CollectAdjacentCellsIntoScratch(
            in AbsoluteUniversePosition origin,
            int requiredKindMask,
            ulong interactionFilter,
            out int visitedCellCount,
            out int candidateHandleCount,
            out int dedupeCount,
            out byte saturated)
        {
            visitedCellCount = 27;
            candidateHandleCount = 0;
            dedupeCount = 0;
            saturated = 0;
            _queryScratch.Reset();

            Long3 centerCell = ToCell(origin.ToAbsoluteDouble3());
            Long3 minCell = new Long3(centerCell.X - 1L, centerCell.Y - 1L, centerCell.Z - 1L);
            Long3 maxCell = new Long3(centerCell.X + 1L, centerCell.Y + 1L, centerCell.Z + 1L);
            int resultCapacity = _queryScratch.Capacity;
            int entryCapacity = _entryHandles != null ? _entryHandles.Length : 0;
            int entryCount = math.min(math.max(0, _entryHandleCount), entryCapacity);
            for (int i = 0; i < entryCount && _queryScratch.HandleCount < resultCapacity; i++)
            {
                int handle = _entryHandles[i];
                if (!TryGetEntryByHandle(handle, out SpatialEntry entry))
                    continue;

                candidateHandleCount++;
                dedupeCount++;
                if (!CellRangesOverlap(entry.MinCell, entry.MaxCell, minCell, maxCell))
                    continue;

                if (requiredKindMask != 0 && (entry.KindMask & requiredKindMask) == 0)
                    continue;

                if (interactionFilter != 0UL && (entry.EntityFlags & interactionFilter) != interactionFilter)
                    continue;

                _queryScratch.TryAddHandle(handle);
            }

            if (_queryScratch.HandleCount >= resultCapacity && candidateHandleCount < entryCount)
                saturated = 1;

            return _queryScratch.HandleCount;
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
                    _entryHandleCount == 0)
                {
                    _lastQueryStats = default;
                    return 0;
                }

                int foundCount = CollectSphereIntoScratch(
                    in origin,
                    radiusMeters,
                    requiredKindMask,
                    interactionFilter,
                    out int visitedCellCount,
                    out int candidateHandleCount,
                    out int dedupeCount,
                    out byte saturated);

                int copyCount = math.min(foundCount, resultHandles.Capacity);
                resultHandles.ResizeUninitialized(copyCount);
                for (int i = 0; i < copyCount; i++)
                    resultHandles[i] = _queryScratch.Handles[i];
                if (copyCount < foundCount)
                    saturated = 1;
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

        public int CollectSphere(in AbsoluteUniversePosition origin, float radiusMeters, int requiredKindMask, ulong interactionFilter, NativeArray<int> resultHandles)
        {
            if (!resultHandles.IsCreated)
            {
                _lastQueryStats = default;
                return 0;
            }

            using (_queryProfilerMarker.Auto())
            {
                if (!IsFiniteAup(in origin) ||
                    !math.isfinite(radiusMeters) ||
                    radiusMeters <= 0f ||
                    _entryHandleCount == 0)
                {
                    _lastQueryStats = default;
                    return 0;
                }

                int foundCount = CollectSphereIntoScratch(
                    in origin,
                    radiusMeters,
                    requiredKindMask,
                    interactionFilter,
                    out int visitedCellCount,
                    out int candidateHandleCount,
                    out int dedupeCount,
                    out byte saturated);

                int copyCount = math.min(foundCount, resultHandles.Length);
                for (int i = 0; i < copyCount; i++)
                    resultHandles[i] = _queryScratch.Handles[i];
                if (copyCount < foundCount)
                    saturated = 1;
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

        public int CollectSphere(in AbsoluteUniversePosition origin, float radiusMeters, int requiredKindMask, uint interactionFilter, NativeArray<int> resultHandles)
        {
            return CollectSphere(in origin, radiusMeters, requiredKindMask, (ulong)interactionFilter, resultHandles);
        }

        public int CollectSphere(in AbsoluteUniversePosition origin, float radiusMeters, int requiredKindMask, NativeArray<int> resultHandles)
        {
            return CollectSphere(in origin, radiusMeters, requiredKindMask, 0UL, resultHandles);
        }

        public int CollectSphere(in AbsoluteUniversePosition origin, float radiusMeters, int requiredKindMask, ulong interactionFilter, int[] resultHandles)
        {
            if (resultHandles == null || resultHandles.Length == 0)
            {
                _lastQueryStats = default;
                return 0;
            }

            using (_queryProfilerMarker.Auto())
            {
                if (!IsFiniteAup(in origin) ||
                    !math.isfinite(radiusMeters) ||
                    radiusMeters <= 0f ||
                    _entryHandleCount == 0)
                {
                    _lastQueryStats = default;
                    return 0;
                }

                int foundCount = CollectSphereIntoScratch(
                    in origin,
                    radiusMeters,
                    requiredKindMask,
                    interactionFilter,
                    out int visitedCellCount,
                    out int candidateHandleCount,
                    out int dedupeCount,
                    out byte saturated);

                int copyCount = math.min(foundCount, resultHandles.Length);
                for (int i = 0; i < copyCount; i++)
                    resultHandles[i] = _queryScratch.Handles[i];

                if (copyCount < foundCount)
                    saturated = 1;
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

        public int CollectSphere(in AbsoluteUniversePosition origin, float radiusMeters, int requiredKindMask, int[] resultHandles)
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
                if (!IsFiniteAup(in origin) || _entryHandleCount == 0)
                {
                    _lastQueryStats = default;
                    return 0;
                }

                int foundCount = CollectAdjacentCellsIntoScratch(
                    in origin,
                    requiredKindMask,
                    interactionFilter,
                    out int visitedCellCount,
                    out int candidateHandleCount,
                    out int dedupeCount,
                    out byte saturated);

                int copyCount = math.min(foundCount, resultHandles.Capacity);
                resultHandles.ResizeUninitialized(copyCount);
                for (int i = 0; i < copyCount; i++)
                    resultHandles[i] = _queryScratch.Handles[i];
                if (copyCount < foundCount)
                    saturated = 1;
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

            _entries = null;
            _entryOccupied = null;
            _entryHandles = null;
            _slotGenerations = null;
            _entryHandleCount = 0;

            _transientEvents = null;
            _transientEventCount = 0;

            disposeHandle = _queryScratch.Dispose(disposeHandle);
            _freeHandleHead = 0;
            _freeHandleTail = 0;
            _freeHandleCount = 0;
            JobHandle.ScheduleBatchedJobs();
        }

        private JobHandle CancelPendingJobsForTeardown()
        {
            return default;
        }

        private bool ContainsEntry(int handle)
        {
            return TryGetEntryByHandle(handle, out _);
        }

        private bool TryGetEntryByHandle(int handle, out SpatialEntry entry)
        {
            entry = default;
            if (!TryResolveEntrySlot(handle, out int slotIndex))
                return false;

            if (_entryOccupied == null || slotIndex >= _entryOccupied.Length || _entryOccupied[slotIndex] == 0)
                return false;

            entry = _entries[slotIndex];
            return true;
        }

        private bool TrySetEntry(int handle, in SpatialEntry entry)
        {
            if (!TryResolveEntrySlot(handle, out int slotIndex))
                return false;

            _entries[slotIndex] = entry;
            _entryOccupied[slotIndex] = 1;
            return true;
        }

        private void ClearEntry(int handle)
        {
            if (!TryDecodeHandle(handle, out uint slot, out _) ||
                _entries == null ||
                _entryOccupied == null ||
                slot >= (uint)_entries.Length ||
                slot >= (uint)_entryOccupied.Length)
            {
                return;
            }

            int slotIndex = (int)slot;
            _entries[slotIndex] = default;
            _entryOccupied[slotIndex] = 0;
        }

        private bool TryResolveEntrySlot(int handle, out int slotIndex)
        {
            slotIndex = 0;
            if (!TryDecodeHandle(handle, out uint slot, out uint generation) ||
                _entries == null ||
                _entryOccupied == null ||
                slot >= (uint)_entries.Length ||
                slot >= (uint)_entryOccupied.Length ||
                !TryGetSlotGeneration(slot, out uint currentGeneration) ||
                currentGeneration != generation)
            {
                return false;
            }

            slotIndex = (int)slot;
            return true;
        }

        private int AllocateHandle()
        {
            if (!HasEntryHandleCapacity(1))
                return 0;

            while (TryDequeueFreeHandle(out uint queuedHandle))
            {
                if (IsQueuedFreeHandleCurrent(queuedHandle))
                    return (int)queuedHandle;
            }

            int generationCapacity = _slotGenerations != null ? _slotGenerations.Length : 0;
            if (_nextSlot > MaxHandleSlot || _nextSlot >= (uint)generationCapacity)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[HectonSpatialHash] Handle allocator exhausted.");
#endif
                return 0;
            }

            uint slot = _nextSlot++;
            _slotGenerations[(int)slot] = InitialHandleGeneration;
            return EncodeHandle(slot, InitialHandleGeneration);
        }

        private bool HasEntryHandleCapacity(int additionalHandles)
        {
            int capacity = _entryHandles != null ? _entryHandles.Length : 0;
            int required = AddCapacitySaturating(_entryHandleCount, math.max(0, additionalHandles));
            return required <= capacity;
        }

        private bool TryAppendEntryHandle(int handle)
        {
            int capacity = _entryHandles != null ? _entryHandles.Length : 0;
            if (handle <= 0 || _entryHandleCount >= capacity)
                return false;

            _entryHandles[_entryHandleCount] = handle;
            _entryHandleCount++;
            return true;
        }

        private void RemoveEntryHandle(int handle)
        {
            int capacity = _entryHandles != null ? _entryHandles.Length : 0;
            int count = math.min(math.max(0, _entryHandleCount), capacity);
            for (int i = 0; i < count; i++)
            {
                if (_entryHandles[i] != handle)
                    continue;

                int lastIndex = count - 1;
                _entryHandles[i] = _entryHandles[lastIndex];
                _entryHandles[lastIndex] = 0;
                _entryHandleCount = lastIndex;
                return;
            }

            _entryHandleCount = count;
        }

        private bool TryGetSlotGeneration(uint slot, out uint generation)
        {
            generation = 0u;
            if (slot == 0u || _slotGenerations == null || slot >= (uint)_slotGenerations.Length)
                return false;

            generation = _slotGenerations[(int)slot];
            return generation != 0u;
        }

        private bool TrySetSlotGeneration(uint slot, uint generation)
        {
            if (slot == 0u || generation == 0u || _slotGenerations == null || slot >= (uint)_slotGenerations.Length)
                return false;

            _slotGenerations[(int)slot] = generation;
            return true;
        }

        private void ClearSlotGeneration(uint slot)
        {
            if (slot == 0u || _slotGenerations == null || slot >= (uint)_slotGenerations.Length)
                return;

            _slotGenerations[(int)slot] = 0u;
        }

        private bool TryDequeueFreeHandle(out uint handle)
        {
            handle = 0u;
            int capacity = _freeHandles != null ? _freeHandles.Length : 0;
            if (capacity == 0 || _freeHandleCount <= 0)
                return false;

            handle = _freeHandles[_freeHandleHead];
            _freeHandles[_freeHandleHead] = 0u;
            _freeHandleHead++;
            if (_freeHandleHead >= capacity)
                _freeHandleHead = 0;

            _freeHandleCount--;
            return handle != 0u;
        }

        private bool TryEnqueueFreeHandle(uint handle)
        {
            int capacity = _freeHandles != null ? _freeHandles.Length : 0;
            if (handle == 0u || capacity == 0 || _freeHandleCount >= capacity)
                return false;

            _freeHandles[_freeHandleTail] = handle;
            _freeHandleTail++;
            if (_freeHandleTail >= capacity)
                _freeHandleTail = 0;

            _freeHandleCount++;
            return true;
        }

        private bool UpsertInternal(int handle, in AbsoluteUniversePosition position, float3 halfExtents, int kindMask, ulong entityFlags, int payloadId, bool appendHandle)
        {
            Long3 minCell;
            Long3 maxCell;
            double3 absoluteCenter = position.ToAbsoluteDouble3();
            ResolveCellRange(absoluteCenter, halfExtents, out minCell, out maxCell);
            if (EstimateCellSpan(minCell, maxCell) > MaxRegisteredEntryCellSpan)
                return false;

            if (appendHandle && !HasEntryHandleCapacity(1))
                return false;

            EnsureCapacityForEntry(appendHandle ? 1 : 0);

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

            if (!TrySetEntry(handle, in entry))
                return false;
            if (appendHandle && !TryAppendEntryHandle(handle))
            {
                ClearEntry(handle);
                return false;
            }

            _mutationVersion++;
            return true;
        }

        private void EnsureCapacityForEntry(int additionalEntries)
        {
            int requiredEntryCapacity = AddCapacitySaturating(_entryHandleCount, math.max(0, additionalEntries));
            _ = requiredEntryCapacity;
        }

        private bool TryAppendTransientEvent(in TransientEventRecord record)
        {
            int capacity = _transientEvents != null ? _transientEvents.Length : 0;
            int count = math.min(math.max(0, _transientEventCount), capacity);
            if (count >= capacity)
                return false;

            _transientEvents[count] = record;
            _transientEventCount = count + 1;
            return true;
        }

        private void ClearTransientTail(int startIndex, int endIndex)
        {
            int capacity = _transientEvents != null ? _transientEvents.Length : 0;
            int safeStart = math.clamp(startIndex, 0, capacity);
            int safeEnd = math.clamp(endIndex, safeStart, capacity);
            for (int i = safeStart; i < safeEnd; i++)
                _transientEvents[i] = default;
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

            _ = cellSpan;
            TryAppendTransientEvent(in disturbanceRecord);
        }

        private bool HasHighIntensityChemicalInCells(Long3 minCell, Long3 maxCell, double currentTimestamp)
        {
            int eventCount = math.min(math.max(0, _transientEventCount), _transientEvents != null ? _transientEvents.Length : 0);
            for (int i = 0; i < eventCount; i++)
            {
                TransientEventRecord record = _transientEvents[i];
                if (IsTransientExpired(currentTimestamp, record.ExpirationTimestamp))
                    continue;

                if ((record.EventTypeMask & ChemicalScentEventMask) == 0u ||
                    record.Intensity <= CascadeIntensityThreshold)
                {
                    continue;
                }

                Long3 recordMinCell = ToCell(record.AbsoluteCenter - new double3(record.RadiusMeters, record.RadiusMeters, record.RadiusMeters));
                Long3 recordMaxCell = ToCell(record.AbsoluteCenter + new double3(record.RadiusMeters, record.RadiusMeters, record.RadiusMeters));
                if (CellRangesOverlap(recordMinCell, recordMaxCell, minCell, maxCell))
                    return true;
            }

            return false;
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

        private static bool CellRangesOverlap(Long3 aMin, Long3 aMax, Long3 bMin, Long3 bMax)
        {
            return aMin.X <= bMax.X && aMax.X >= bMin.X &&
                   aMin.Y <= bMax.Y && aMax.Y >= bMin.Y &&
                   aMin.Z <= bMax.Z && aMax.Z >= bMin.Z;
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
            if (handle <= 0 || ContainsEntry(handle))
                return;

            if (!TryDecodeHandle(handle, out uint slot, out uint generation))
                return;

            if (!TryGetSlotGeneration(slot, out uint currentGeneration) || currentGeneration != generation)
                return;

            if (generation >= MaxHandleGeneration)
            {
                ClearSlotGeneration(slot);
                return;
            }

            uint nextGeneration = generation + 1u;
            if (!TrySetSlotGeneration(slot, nextGeneration))
                return;

            TryEnqueueFreeHandle((uint)EncodeHandle(slot, nextGeneration));
        }

        private bool IsHandleCurrent(int handle)
        {
            if (!TryDecodeHandle(handle, out uint slot, out uint generation))
                return false;

            return TryGetSlotGeneration(slot, out uint currentGeneration) &&
                   currentGeneration == generation &&
                   ContainsEntry(handle);
        }

        private bool IsQueuedFreeHandleCurrent(uint queuedHandle)
        {
            if (!TryDecodeHandle((int)queuedHandle, out uint slot, out uint generation))
                return false;

            return TryGetSlotGeneration(slot, out uint currentGeneration) &&
                   currentGeneration == generation &&
                   !ContainsEntry((int)queuedHandle);
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
