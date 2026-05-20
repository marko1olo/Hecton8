using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.Bootstrap;
using Hecton8.Caves;
using Hecton8.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Runtime double-buffered voxel passability snapshots built directly from the cave SDF density field.
    /// Owner: voxel rebuild pipeline. Eviction: explicit unregister or domain reset.
    /// </summary>
    internal static class VoxelDynamicNavGridRuntime
    {
        internal const byte OpenCell = 0x00;
        internal const byte SolidCell = 0xFF;
        private const int FaceCount = 6;
        private const int ChunkIdAxisBias = 512;
        private const float BoundsMatchEpsilon = 0.05f;
        private const float DefaultPredatorClearanceRadiusMeters = 2f;
        private const int InvalidPortalIndex = -1;
        private const float MinimumKelpObstacleRadiusMeters = 0.9f;
        private const float MaximumKelpObstacleRadiusMeters = 2.4f;
        private const float MinimumSargassumObstacleRadiusMeters = 1.5f;
        private const float MaximumSargassumObstacleRadiusMeters = 4.5f;
        private const float MinimumSargassumObstacleHalfHeightMeters = 0.75f;
        private const float MaximumSargassumObstacleHalfHeightMeters = 1.8f;
        private const float MinimumCoralObstacleRadiusMeters = 0.9f;
        private const float MaximumCoralObstacleRadiusMeters = 4.8f;
        private const float MinimumCoralObstacleHalfHeightMeters = 0.8f;
        private const float MaximumCoralObstacleHalfHeightMeters = 5.5f;
        private const byte FloraRuntimeFlagDead = 1 << 6;
        private const float DynamicObstacleChunkSizeMeters = 16f;
        private const double PartialClearanceDilationBudgetMilliseconds = 1.0d;
        private const int NavGridIndexMask = 0x0FFF;
        private const int ToxicBodySafeNodeSearchRadiusCells = 4;
        private const int DynamicClearanceFallbackScheduleCount = 1;
        private const float DynamicClearanceWarningCooldownSeconds = 5f;
        private const int MaxPersistentDynamicObstacleCount = 512;
        private const int DirtyVolumeQueueCapacity = 32;
        private const int DeferredDirtyVolumeQueueCapacity = 16;
        private const int PendingObstacleClearQueueCapacity = 16;
        private const int PureVoidScanBlockSize = 64;
        private const int PureVoidScanBlockShift = 6;
        private const int MaxTrackedVolumeRecords = 512;
        private const int MaxRegisteredObstacleRecords = 512;
        private const int MaxPortalFaceScratchCells = 4096;
        private const int MaxPortalsPerVolume = 4096;
        private const int MaxPortalGraphNodeCapacity = 4096;
        private const float PersistentObstacleMergeDistanceMeters = 2f;
        private const string NativeMemoryOwner = nameof(VoxelDynamicNavGridRuntime);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        private const string ObstacleSnapshotNativeMemoryLabel = "VoxelDynamicNavGridRuntime.ObstacleSnapshot";
        private const string DynamicClearanceBudgetWarningMessage = "[VoxelDynamicNavGridRuntime] Partial clearance dilation exceeded 1ms; next destroyed-flora clear uses reduced clearance radius.";

        // COLD ALLOC: Dictionary<int, VolumeRecord>(512) - capped voxel navgrid snapshots keyed by runtime volume instance ID - owner: VoxelDynamicNavGridRuntime
        private static readonly Dictionary<int, VolumeRecord> _records = new Dictionary<int, VolumeRecord>(MaxTrackedVolumeRecords);
        // COLD ALLOC: List<DirtyVolumeRequest>(32) - temporary dirty-volume spill buffer while consuming queue entries - owner: VoxelDynamicNavGridRuntime
        private static readonly List<DirtyVolumeRequest> _dirtyRequestSpill = new List<DirtyVolumeRequest>(32);
        // COLD ALLOC: List<DeferredDirtyVolumeRequest>(16) - slow-tick delayed voxel nav rebuild markers for chthonic pillar volumes - owner: VoxelDynamicNavGridRuntime
        private static readonly List<DeferredDirtyVolumeRequest> _deferredDirtyVolumes = new List<DeferredDirtyVolumeRequest>(16);
        // COLD ALLOC: List<PortalNode>(4096) - capped macro portal graph nodes spanning active navgrid chunks - owner: VoxelDynamicNavGridRuntime
        private static readonly List<PortalNode> _portalGraphNodes = new List<PortalNode>(MaxPortalGraphNodeCapacity);
        // COLD ALLOC: List<RouteNodeState>(4096) - capped portal A* node state scratch - owner: VoxelDynamicNavGridRuntime
        private static readonly List<RouteNodeState> _routeNodeScratch = new List<RouteNodeState>(MaxPortalGraphNodeCapacity);
        // COLD ALLOC: List<int>(4096) - capped portal A* open-set scratch - owner: VoxelDynamicNavGridRuntime
        private static readonly List<int> _routeOpenSetScratch = new List<int>(MaxPortalGraphNodeCapacity);
        // COLD ALLOC: List<int>(4096) - capped portal route reconstruction scratch - owner: VoxelDynamicNavGridRuntime
        private static readonly List<int> _routePathScratch = new List<int>(MaxPortalGraphNodeCapacity);
        // COLD ALLOC: List<int>(512) - record keys pending safe native-container disposal after dynamic jobs complete - owner: VoxelDynamicNavGridRuntime
        private static readonly List<int> _recordRemovalScratch = new List<int>(MaxTrackedVolumeRecords);
        // COLD ALLOC: Dictionary<int,ObstacleRegistration>(512) - capped registered habitat obstacle collider sources - owner: VoxelDynamicNavGridRuntime
        private static readonly Dictionary<int, ObstacleRegistration> _registeredObstacles = new Dictionary<int, ObstacleRegistration>(MaxRegisteredObstacleRecords);
        private static readonly ProfilerMarker _partialClearanceDilationScheduleMarker = new ProfilerMarker("H8/NavGrid/PartialClearanceDilationJob.Schedule");
        private static readonly ProfilerMarker _partialClearanceDilationCompleteMarker = new ProfilerMarker("H8/NavGrid/PartialClearanceDilationJob.Complete");
        private static NativeQueue<DirtyVolumeRequest> _dirtyVolumes;
        private static NativeQueue<DynamicObstacleClearRequest> _pendingObstacleClears;
        private static NativeList<NavObstaclePrimitive> _persistentDynamicObstacles;
        private static VoxelDynamicNavGridRuntimeLifecycle _lifecycleOwner;
        private static bool _portalGraphDirty = true;
        private static bool _teardownPending;
        private static bool _clearRuntimeContainersWhenTeardownCompletes;
        private static int _persistentDynamicObstacleWriteCursor;
        private static int _dynamicClearanceFallbackSchedulesRemaining;
        private static int _dirtyVolumeQueueCount;
        private static int _pendingObstacleClearQueueCount;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static float _nextDynamicClearanceWarningTime = float.NegativeInfinity;
#endif

        internal enum HybridNavigationMode : byte
        {
            OpenWaterHeightmap = 0,
            CaveVoxel = 1,
            SolidVoxel = 2,
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct PassabilityBuildJob : Unity.Jobs.IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> DensityField;
            [WriteOnly] public NativeArray<byte> Output;
            public float SolidThreshold;

            public void Execute(int index)
            {
                Output[index] = DensityField[index] < SolidThreshold ? OpenCell : SolidCell;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct UpdateNavCellsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> DensityField;
            public NativeArray<byte> Passability;
            public int3 Dimensions;
            public int3 RegionMin;
            public int3 RegionSize;
            public float SolidThreshold;

            public void Execute(int index)
            {
                int regionXY = RegionSize.x * RegionSize.y;
                if (!DensityField.IsCreated ||
                    !Passability.IsCreated ||
                    regionXY <= 0 ||
                    RegionSize.z <= 0)
                {
                    return;
                }

                int z = index / regionXY;
                int remainder = index - z * regionXY;
                int y = remainder / RegionSize.x;
                int x = remainder - y * RegionSize.x;
                int3 cell = RegionMin + new int3(x, y, z);
                if (cell.x < 0 || cell.y < 0 || cell.z < 0 ||
                    cell.x >= Dimensions.x || cell.y >= Dimensions.y || cell.z >= Dimensions.z)
                {
                    return;
                }

                int flatIndex = cell.x + cell.y * Dimensions.x + cell.z * Dimensions.x * Dimensions.y;
                if (flatIndex < 0 || flatIndex >= DensityField.Length || flatIndex >= Passability.Length)
                    return;

                Passability[flatIndex] = DensityField[flatIndex] < SolidThreshold ? OpenCell : SolidCell;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct PureVoidBlockScanJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> Passability;
            [ReadOnly] public NativeArray<ushort> DistanceMap;
            [WriteOnly] public NativeArray<int> BlockFlags;
            public int PointCount;

            public void Execute(int blockIndex)
            {
                if (!BlockFlags.IsCreated ||
                    (uint)blockIndex >= (uint)BlockFlags.Length)
                {
                    return;
                }

                int pure = 1;
                if (!Passability.IsCreated ||
                    !DistanceMap.IsCreated ||
                    PointCount <= 0 ||
                    Passability.Length < PointCount ||
                    DistanceMap.Length < PointCount)
                {
                    pure = 0;
                }
                else
                {
                    long startLong = (long)blockIndex * PureVoidScanBlockSize;
                    if (startLong < 0L || startLong >= PointCount)
                    {
                        BlockFlags[blockIndex] = 0;
                        return;
                    }

                    int start = (int)startLong;
                    long endLong = startLong + PureVoidScanBlockSize;
                    if (endLong > PointCount)
                        endLong = PointCount;

                    int end = (int)endLong;
                    for (int i = start; i < end; i++)
                    {
                        if (Passability[i] == OpenCell && DistanceMap[i] == ushort.MaxValue)
                            continue;

                        pure = 0;
                        break;
                    }
                }

                BlockFlags[blockIndex] = pure;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct ClearanceDilationJob : Unity.Jobs.IJob
        {
            public NativeArray<byte> Passability;
            public NativeArray<ushort> DistanceMap;
            public int3 Dimensions;
            public int AgentRadiusCells;

            public void Execute()
            {
                int width = Dimensions.x;
                int height = Dimensions.y;
                int depth = Dimensions.z;
                if (!Passability.IsCreated ||
                    !DistanceMap.IsCreated ||
                    width <= 0 ||
                    height <= 0 ||
                    depth <= 0 ||
                    AgentRadiusCells <= 0)
                {
                    return;
                }

                int slice = width * height;
                int pointCount = slice * depth;
                if (Passability.Length != pointCount || DistanceMap.Length != pointCount)
                    return;

                const int MaxDistance = ushort.MaxValue;
                for (int flatIndex = 0; flatIndex < pointCount; flatIndex++)
                {
                    int z = flatIndex / slice;
                    int y = (flatIndex - (z * slice)) / width;
                    int x = flatIndex - (z * slice) - (y * width);
                    if (Passability[flatIndex] == SolidCell)
                    {
                        DistanceMap[flatIndex] = 0;
                        continue;
                    }

                    int distance = MaxDistance;
                    if (x > 0)
                        distance = math.min(distance, DistanceMap[flatIndex - 1] + 1);
                    if (y > 0)
                        distance = math.min(distance, DistanceMap[flatIndex - width] + 1);
                    if (z > 0)
                        distance = math.min(distance, DistanceMap[flatIndex - slice] + 1);

                    DistanceMap[flatIndex] = (ushort)math.min(distance, MaxDistance);
                }

                for (int flatIndex = pointCount - 1; flatIndex >= 0; flatIndex--)
                {
                    int z = flatIndex / slice;
                    int y = (flatIndex - (z * slice)) / width;
                    int x = flatIndex - (z * slice) - (y * width);
                    int distance = DistanceMap[flatIndex];
                    if (x + 1 < width)
                        distance = math.min(distance, DistanceMap[flatIndex + 1] + 1);
                    if (y + 1 < height)
                        distance = math.min(distance, DistanceMap[flatIndex + width] + 1);
                    if (z + 1 < depth)
                        distance = math.min(distance, DistanceMap[flatIndex + slice] + 1);

                    ushort resolvedDistance = (ushort)math.min(distance, MaxDistance);
                    DistanceMap[flatIndex] = resolvedDistance;
                    Passability[flatIndex] = resolvedDistance <= AgentRadiusCells
                        ? SolidCell
                        : OpenCell;
                }
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct ObstacleStampJob : Unity.Jobs.IJobParallelFor
        {
            public NativeArray<byte> Passability;
            [ReadOnly] public NativeArray<NavObstaclePrimitive> Obstacles;
            public int3 Dimensions;
            public float3 Origin;
            public float CellSize;

            public void Execute(int index)
            {
                if (!Passability.IsCreated ||
                    !Obstacles.IsCreated ||
                    Obstacles.Length <= 0 ||
                    index < 0 ||
                    index >= Passability.Length)
                {
                    return;
                }

                int slice = Dimensions.x * Dimensions.y;
                int z = index / slice;
                int y = (index - (z * slice)) / Dimensions.x;
                int x = index - (z * slice) - (y * Dimensions.x);
                float3 samplePoint = Origin + new float3(x * CellSize, y * CellSize, z * CellSize);

                for (int obstacleIndex = 0; obstacleIndex < Obstacles.Length; obstacleIndex++)
                {
                    NavObstaclePrimitive obstacle = Obstacles[obstacleIndex];
                    float3 min = obstacle.Center - obstacle.Extents;
                    float3 max = obstacle.Center + obstacle.Extents;
                    if (samplePoint.x < min.x || samplePoint.x > max.x ||
                        samplePoint.y < min.y || samplePoint.y > max.y ||
                        samplePoint.z < min.z || samplePoint.z > max.z)
                    {
                        continue;
                    }

                    Passability[index] = SolidCell;
                    return;
                }
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct CopyByteBufferJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> Source;
            [WriteOnly] public NativeArray<byte> Destination;

            public void Execute(int index)
            {
                if (!Source.IsCreated || !Destination.IsCreated || index < 0 || index >= Source.Length || index >= Destination.Length)
                    return;

                Destination[index] = Source[index];
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct PartialObstacleResetAndStampJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<byte> BasePassability;
            public NativeArray<byte> Passability;
            [ReadOnly] public NativeArray<NavObstaclePrimitive> Obstacles;
            public int3 Dimensions;
            public int3 RegionMin;
            public int3 RegionMax;
            public float3 Origin;
            public float CellSize;

            public void Execute(int index)
            {
                if (!BasePassability.IsCreated ||
                    !Passability.IsCreated ||
                    index < 0)
                {
                    return;
                }

                int3 regionSize = RegionMax - RegionMin + 1;
                if (regionSize.x <= 0 || regionSize.y <= 0 || regionSize.z <= 0)
                    return;

                int regionSlice = regionSize.x * regionSize.y;
                int regionPointCount = regionSlice * regionSize.z;
                if (index >= regionPointCount)
                    return;

                int localZ = index / regionSlice;
                int localY = (index - (localZ * regionSlice)) / regionSize.x;
                int localX = index - (localZ * regionSlice) - (localY * regionSize.x);
                int globalX = RegionMin.x + localX;
                int globalY = RegionMin.y + localY;
                int globalZ = RegionMin.z + localZ;
                int globalIndex = globalX + (globalY * Dimensions.x) + (globalZ * Dimensions.x * Dimensions.y);
                if (globalIndex < 0 || globalIndex >= Passability.Length || globalIndex >= BasePassability.Length)
                    return;

                byte resolvedPassability = BasePassability[globalIndex];
                if (Obstacles.IsCreated && Obstacles.Length > 0)
                {
                    float3 samplePoint = Origin + new float3(globalX * CellSize, globalY * CellSize, globalZ * CellSize);
                    for (int obstacleIndex = 0; obstacleIndex < Obstacles.Length; obstacleIndex++)
                    {
                        NavObstaclePrimitive obstacle = Obstacles[obstacleIndex];
                        if (!math.all(math.isfinite(obstacle.Center)) ||
                            !math.all(math.isfinite(obstacle.Extents)) ||
                            obstacle.Extents.x <= 0.0001f ||
                            obstacle.Extents.y <= 0.0001f ||
                            obstacle.Extents.z <= 0.0001f)
                        {
                            continue;
                        }

                        float3 min = obstacle.Center - obstacle.Extents;
                        float3 max = obstacle.Center + obstacle.Extents;
                        if (!math.all(math.isfinite(min)) || !math.all(math.isfinite(max)))
                            continue;

                        if (samplePoint.x < min.x || samplePoint.x > max.x ||
                            samplePoint.y < min.y || samplePoint.y > max.y ||
                            samplePoint.z < min.z || samplePoint.z > max.z)
                        {
                            continue;
                        }

                        resolvedPassability = SolidCell;
                        break;
                    }
                }

                Passability[globalIndex] = resolvedPassability;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct PartialClearanceDilationJob : IJob
        {
            public NativeArray<byte> Passability;
            [ReadOnly] public NativeArray<ushort> ReferenceDistanceMap;
            public NativeArray<ushort> DistanceMap;
            public int3 Dimensions;
            public int3 RegionMin;
            public int3 RegionMax;
            public int AgentRadiusCells;

            public void Execute()
            {
                int width = Dimensions.x;
                int height = Dimensions.y;
                int depth = Dimensions.z;
                if (!Passability.IsCreated ||
                    !ReferenceDistanceMap.IsCreated ||
                    !DistanceMap.IsCreated ||
                    width <= 0 ||
                    height <= 0 ||
                    depth <= 0 ||
                    AgentRadiusCells <= 0)
                {
                    return;
                }

                const int MaxDistance = ushort.MaxValue;
                for (int z = RegionMin.z; z <= RegionMax.z; z++)
                {
                    for (int y = RegionMin.y; y <= RegionMax.y; y++)
                    {
                        for (int x = RegionMin.x; x <= RegionMax.x; x++)
                        {
                            int flatIndex = x + (y * Dimensions.x) + (z * Dimensions.x * Dimensions.y);
                            if (flatIndex < 0 || flatIndex >= Passability.Length || flatIndex >= DistanceMap.Length)
                                continue;

                            if (Passability[flatIndex] == SolidCell)
                            {
                                DistanceMap[flatIndex] = 0;
                                continue;
                            }

                            int distance = MaxDistance;
                            distance = math.min(distance, ReadNeighborDistance(x - 1, y, z) + 1);
                            distance = math.min(distance, ReadNeighborDistance(x, y - 1, z) + 1);
                            distance = math.min(distance, ReadNeighborDistance(x, y, z - 1) + 1);
                            DistanceMap[flatIndex] = (ushort)math.min(distance, MaxDistance);
                        }
                    }
                }

                for (int z = RegionMax.z; z >= RegionMin.z; z--)
                {
                    for (int y = RegionMax.y; y >= RegionMin.y; y--)
                    {
                        for (int x = RegionMax.x; x >= RegionMin.x; x--)
                        {
                            int flatIndex = x + (y * Dimensions.x) + (z * Dimensions.x * Dimensions.y);
                            if (flatIndex < 0 || flatIndex >= Passability.Length || flatIndex >= DistanceMap.Length)
                                continue;

                            int distance = DistanceMap[flatIndex];
                            distance = math.min(distance, ReadNeighborDistance(x + 1, y, z) + 1);
                            distance = math.min(distance, ReadNeighborDistance(x, y + 1, z) + 1);
                            distance = math.min(distance, ReadNeighborDistance(x, y, z + 1) + 1);

                            ushort resolvedDistance = (ushort)math.min(distance, MaxDistance);
                            DistanceMap[flatIndex] = resolvedDistance;
                            Passability[flatIndex] = resolvedDistance <= AgentRadiusCells
                                ? SolidCell
                                : OpenCell;
                        }
                    }
                }
            }

            private int ReadNeighborDistance(int x, int y, int z)
            {
                if (x < 0 || y < 0 || z < 0 || x >= Dimensions.x || y >= Dimensions.y || z >= Dimensions.z)
                    return ushort.MaxValue;

                int rawIndex = x + (y * Dimensions.x) + (z * Dimensions.x * Dimensions.y);
                int flatIndex = DistanceMap.Length <= NavGridIndexMask + 1
                    ? rawIndex & NavGridIndexMask
                    : rawIndex;
                if (x >= RegionMin.x && x <= RegionMax.x &&
                    y >= RegionMin.y && y <= RegionMax.y &&
                    z >= RegionMin.z && z <= RegionMax.z)
                {
                    if (flatIndex < 0 || flatIndex >= DistanceMap.Length)
                        return ushort.MaxValue;

                    return DistanceMap[flatIndex];
                }

                if (flatIndex < 0 || flatIndex >= ReferenceDistanceMap.Length)
                    return ushort.MaxValue;

                return ReferenceDistanceMap[flatIndex];
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DirtyVolumeRequest
        {
            public int VolumeInstanceId;
            public int RuntimeStamp;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DeferredDirtyVolumeRequest
        {
            public HectonVoxelVolume Volume;
            public int RuntimeStamp;
            public int RemainingSlowTicks;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DynamicObstacleClearRequest
        {
            public float3 Center;
            public float3 Extents;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PortalNode
        {
            public uint ChunkId;
            public float3 Centroid;
            public float Radius;
            public int ConnectedPortalIndex;
            public byte Face;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RouteNodeState
        {
            public float GScore;
            public float FScore;
            public int ParentIndex;
            public byte Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct NavObstaclePrimitive
        {
            public float3 Center;
            public float3 Extents;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HybridNavigationSample
        {
            public HybridNavigationMode Mode;
            public byte Passability;
            public float CellSize;
            public float3 CellOrigin;
            public float TerrainHeight;
            public float FloorBoundaryY;
            public byte HasTerrainHeight;
        }

        private sealed class VolumeRecord
        {
            public uint ChunkId;
            public int RuntimeStamp;
            public int3 Dimensions;
            public float3 Origin;
            public float3 Max;
            public float CellSize;
            public bool IsDirty;
            public bool IsPureVoid;
            public NativeArray<byte> Current;
            public NativeArray<byte> Next;
            public NativeArray<byte> BaseCurrent;
            public NativeArray<byte> BaseNext;
            public NativeArray<ushort> CurrentDistance;
            public NativeArray<ushort> NextDistance;
            public NativeArray<int> PureVoidBlockFlags;
            public NativeArray<NavObstaclePrimitive> PendingObstacleSnapshot;
            public JobHandle PendingDynamicUpdateHandle;
            public bool HasPendingDynamicUpdate;
            public bool PendingRemoval;
            public bool PortalsReady;
            public int3 PendingRegionMin;
            public int3 PendingRegionMax;
            public int PureVoidBlockCount;
            public PortalNode[] Portals;
            public int PortalCount;
            public int[] FaceVisitScratch;
            public int[] FaceQueueScratch;
            public int FaceVisitStamp;

            public VolumeRecord()
            {
                // COLD ALLOC: PortalNode[4096] - fixed macro-portal storage for one voxel volume record - owner: VoxelDynamicNavGridRuntime
                Portals = new PortalNode[MaxPortalsPerVolume];
                // COLD ALLOC: int[4096] - fixed face flood-fill visit stamps for portal rebuild - owner: VoxelDynamicNavGridRuntime
                FaceVisitScratch = new int[MaxPortalFaceScratchCells];
                // COLD ALLOC: int[4096] - fixed face flood-fill queue for portal rebuild - owner: VoxelDynamicNavGridRuntime
                FaceQueueScratch = new int[MaxPortalFaceScratchCells];
            }

            public bool TryDisposeCompleted()
            {
                if (HasPendingDynamicUpdate)
                {
                    if (!DispatcherJobSwap.TryComplete(ref PendingDynamicUpdateHandle, forceComplete: false))
                        return false;

                    HasPendingDynamicUpdate = false;
                }

                VoxelDynamicNavGridRuntime.DisposeTrackedNativeArray(ref Current);
                VoxelDynamicNavGridRuntime.DisposeTrackedNativeArray(ref Next);
                VoxelDynamicNavGridRuntime.DisposeTrackedNativeArray(ref BaseCurrent);
                VoxelDynamicNavGridRuntime.DisposeTrackedNativeArray(ref BaseNext);
                VoxelDynamicNavGridRuntime.DisposeTrackedNativeArray(ref CurrentDistance);
                VoxelDynamicNavGridRuntime.DisposeTrackedNativeArray(ref NextDistance);
                VoxelDynamicNavGridRuntime.DisposeTrackedNativeArray(ref PureVoidBlockFlags);

                VoxelDynamicNavGridRuntime.DisposeObstacleSnapshot(ref PendingObstacleSnapshot);

                Current = default;
                Next = default;
                BaseCurrent = default;
                BaseNext = default;
                CurrentDistance = default;
                NextDistance = default;
                PureVoidBlockFlags = default;
                PendingObstacleSnapshot = default;
                PendingDynamicUpdateHandle = default;
                HasPendingDynamicUpdate = false;
                PendingRemoval = false;
                PortalsReady = false;
                PendingRegionMin = int3.zero;
                PendingRegionMax = int3.zero;
                IsDirty = false;
                IsPureVoid = false;
                RuntimeStamp = 0;
                Dimensions = int3.zero;
                Origin = float3.zero;
                Max = float3.zero;
                CellSize = 0f;
                PureVoidBlockCount = 0;
                PortalCount = 0;
                FaceVisitStamp = 0;
                return true;
            }
        }

        private sealed class ObstacleRegistration
        {
            public BoxCollider[] Boxes = System.Array.Empty<BoxCollider>();
            public CapsuleCollider[] Capsules = System.Array.Empty<CapsuleCollider>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime()
        {
            DisposeAll();
        }

        internal static void QueueDirtyVolume(HectonVoxelVolume volume)
        {
            if (volume == null)
                return;

            EnsureInitialized();
            int volumeInstanceId = GetStableVolumeEntityId(volume);
            VolumeRecord record = GetOrCreateRecord(volumeInstanceId);
            if (record == null)
                return;

            if (record.IsDirty && record.RuntimeStamp == volume.RuntimeStamp)
                return;

            record.IsDirty = true;
            record.RuntimeStamp = volume.RuntimeStamp;
            if (_dirtyVolumeQueueCount >= DirtyVolumeQueueCapacity)
                return;

            _dirtyVolumes.Enqueue(new DirtyVolumeRequest
            {
                VolumeInstanceId = volumeInstanceId,
                RuntimeStamp = volume.RuntimeStamp
            });
            _dirtyVolumeQueueCount++;
        }

        internal static void QueueDeferredDirtyVolume(HectonVoxelVolume volume, int slowTickDelay)
        {
            if (volume == null)
                return;

            if (slowTickDelay <= 0)
            {
                QueueDirtyVolume(volume);
                return;
            }

            EnsureInitialized();
            int runtimeStamp = volume.RuntimeStamp;
            for (int i = 0; i < _deferredDirtyVolumes.Count; i++)
            {
                DeferredDirtyVolumeRequest request = _deferredDirtyVolumes[i];
                if (!ReferenceEquals(request.Volume, volume))
                    continue;

                request.RuntimeStamp = runtimeStamp;
                request.RemainingSlowTicks = math.max(request.RemainingSlowTicks, slowTickDelay);
                _deferredDirtyVolumes[i] = request;
                return;
            }

            if (_deferredDirtyVolumes.Count >= DeferredDirtyVolumeQueueCapacity)
            {
                QueueDirtyVolume(volume);
                return;
            }

            _deferredDirtyVolumes.Add(new DeferredDirtyVolumeRequest
            {
                Volume = volume,
                RuntimeStamp = runtimeStamp,
                RemainingSlowTicks = slowTickDelay
            });
        }

        internal static void TickDeferredDirtyVolumes()
        {
            for (int i = _deferredDirtyVolumes.Count - 1; i >= 0; i--)
            {
                DeferredDirtyVolumeRequest request = _deferredDirtyVolumes[i];
                HectonVoxelVolume volume = request.Volume;
                if (volume == null || !volume.MatchesRuntimeStamp(request.RuntimeStamp))
                {
                    _deferredDirtyVolumes.RemoveAt(i);
                    continue;
                }

                request.RemainingSlowTicks--;
                if (request.RemainingSlowTicks > 0)
                {
                    _deferredDirtyVolumes[i] = request;
                    continue;
                }

                _deferredDirtyVolumes.RemoveAt(i);
                QueueDirtyVolume(volume);
            }
        }

        internal static void QueueLocalizedSdfPatch(HectonVoxelVolume volume, int3 minAbsoluteCell, int3 maxAbsoluteCell, float voxelSize)
        {
            if (volume == null ||
                voxelSize <= 0f ||
                !math.isfinite(voxelSize))
                return;

            EnsureInitialized();
            int volumeInstanceId = GetStableVolumeEntityId(volume);
            if (!_records.TryGetValue(volumeInstanceId, out VolumeRecord record) ||
                !HasValidRecordBounds(record))
            {
                QueueDirtyVolume(volume);
                return;
            }

            float3 minAup = new float3(minAbsoluteCell.x, minAbsoluteCell.y, minAbsoluteCell.z) * voxelSize;
            float3 maxAup = (new float3(maxAbsoluteCell.x, maxAbsoluteCell.y, maxAbsoluteCell.z) + 1f) * voxelSize;
            float3 centerAup = (minAup + maxAup) * 0.5f;
            float3 extents = math.max((maxAup - minAup) * 0.5f, new float3(voxelSize));
            if (!math.all(math.isfinite(centerAup)) ||
                !math.all(math.isfinite(extents)))
            {
                QueueDirtyVolume(volume);
                return;
            }

            Vector3 runtimeCenter = HectonFloatingOrigin.ToRuntimePosition(new Vector3(centerAup.x, centerAup.y, centerAup.z));
            TryEnqueueDynamicObstacleClear(new DynamicObstacleClearRequest
            {
                Center = new float3(runtimeCenter.x, runtimeCenter.y, runtimeCenter.z),
                Extents = extents
            });
        }

        internal static bool TryPrepareBuild(
            HectonVoxelVolume volume,
            int runtimeStamp,
            int3 dimensions,
            float3 origin,
            float cellSize,
            int pointCount,
            out NativeArray<byte> outputBuffer,
            out NativeArray<byte> baseOutputBuffer,
            out NativeArray<ushort> distanceBuffer,
            out NativeArray<int> pureVoidBlockFlags)
        {
            outputBuffer = default;
            baseOutputBuffer = default;
            distanceBuffer = default;
            pureVoidBlockFlags = default;
            if (volume == null ||
                pointCount <= 0 ||
                dimensions.x <= 0 ||
                dimensions.y <= 0 ||
                dimensions.z <= 0 ||
                cellSize <= 0f ||
                !math.isfinite(cellSize) ||
                !math.all(math.isfinite(origin)) ||
                !HasCompleteVoxelCellCoverage(dimensions, pointCount))
            {
                return false;
            }

            EnsureInitialized();
            int volumeInstanceId = GetStableVolumeEntityId(volume);
            VolumeRecord record = GetOrCreateRecord(volumeInstanceId);
            if (record == null)
                return false;

            if (record.HasPendingDynamicUpdate)
            {
                record.IsDirty = true;
                return false;
            }

            bool consumedDirtyMarker = ConsumeDirtyMarker(volumeInstanceId, runtimeStamp);
            bool dimensionsChanged = !math.all(record.Dimensions == dimensions);
            bool recordMetadataInvalid = !math.all(math.isfinite(record.Origin)) ||
                                         !math.all(math.isfinite(record.Max)) ||
                                         !math.all(record.Max >= record.Origin) ||
                                         !math.isfinite(record.CellSize) ||
                                         record.CellSize <= 0f;
            bool originChanged = recordMetadataInvalid || math.lengthsq(record.Origin - origin) > 0.0001f;
            bool cellSizeChanged = recordMetadataInvalid || math.abs(record.CellSize - cellSize) > 0.0001f;
            bool needsBuild = consumedDirtyMarker ||
                              record.IsDirty ||
                              (!record.IsPureVoid &&
                               (!record.Current.IsCreated ||
                                !record.Next.IsCreated ||
                                !record.BaseCurrent.IsCreated ||
                                !record.BaseNext.IsCreated ||
                                !record.CurrentDistance.IsCreated ||
                                !record.NextDistance.IsCreated)) ||
                              record.RuntimeStamp != runtimeStamp ||
                              dimensionsChanged ||
                              originChanged ||
                              cellSizeChanged;

            record.RuntimeStamp = runtimeStamp;
            record.Dimensions = dimensions;
            record.Origin = origin;
            record.ChunkId = ComputeChunkId(origin, cellSize, dimensions);
            record.Max = origin + (new float3(
                math.max(0, dimensions.x - 1),
                math.max(0, dimensions.y - 1),
                math.max(0, dimensions.z - 1)) * cellSize);
            record.CellSize = cellSize;
            record.IsDirty = false;

            if (!needsBuild)
                return false;

            EnsureBuffer(ref record.Current, pointCount, nameof(VolumeRecord.Current));
            EnsureBuffer(ref record.Next, pointCount, nameof(VolumeRecord.Next));
            EnsureBuffer(ref record.BaseCurrent, pointCount, nameof(VolumeRecord.BaseCurrent));
            EnsureBuffer(ref record.BaseNext, pointCount, nameof(VolumeRecord.BaseNext));
            EnsureBuffer(ref record.CurrentDistance, pointCount, nameof(VolumeRecord.CurrentDistance));
            EnsureBuffer(ref record.NextDistance, pointCount, nameof(VolumeRecord.NextDistance));
            int pureVoidBlockCount = ResolvePureVoidBlockCount(pointCount);
            EnsureBuffer(ref record.PureVoidBlockFlags, pureVoidBlockCount, nameof(VolumeRecord.PureVoidBlockFlags));
            record.PureVoidBlockCount = pureVoidBlockCount;
            if (!EnsurePortalWorkCapacity(record))
            {
                record.PortalCount = 0;
                record.PortalsReady = false;
            }

            record.IsPureVoid = false;
            record.PortalsReady = false;

            outputBuffer = record.Next;
            baseOutputBuffer = record.BaseNext;
            distanceBuffer = record.NextDistance;
            pureVoidBlockFlags = record.PureVoidBlockFlags;
            return true;
        }

        internal static void CommitBuild(HectonVoxelVolume volume, int runtimeStamp)
        {
            if (volume == null)
                return;

            int volumeInstanceId = GetStableVolumeEntityId(volume);
            if (!_records.TryGetValue(volumeInstanceId, out VolumeRecord record) ||
                record.RuntimeStamp != runtimeStamp)
            {
                return;
            }

            NativeArray<byte> swap = record.Current;
            record.Current = record.Next;
            record.Next = swap;
            swap = record.BaseCurrent;
            record.BaseCurrent = record.BaseNext;
            record.BaseNext = swap;
            NativeArray<ushort> distanceSwap = record.CurrentDistance;
            record.CurrentDistance = record.NextDistance;
            record.NextDistance = distanceSwap;
            EvaluatePureVoidState(record);
            if (record.IsPureVoid)
            {
                record.PortalsReady = true;
                _portalGraphDirty = true;
                return;
            }

            RebuildPortals(record);
            record.PortalsReady = true;
            _portalGraphDirty = true;
        }

        internal static void RegisterModuleObstacle(int obstacleId, BoxCollider[] boxes, CapsuleCollider[] capsules)
        {
            if (obstacleId == 0)
                return;

            ObstacleRegistration registration = null;
            if (!_registeredObstacles.TryGetValue(obstacleId, out registration))
            {
                if (_registeredObstacles.Count >= MaxRegisteredObstacleRecords)
                    return;

                registration = new ObstacleRegistration();
                _registeredObstacles.Add(obstacleId, registration);
            }

            registration.Boxes = boxes ?? System.Array.Empty<BoxCollider>();
            registration.Capsules = capsules ?? System.Array.Empty<CapsuleCollider>();
            MarkAllVolumesDirty();
        }

        internal static void UnregisterModuleObstacle(int obstacleId)
        {
            if (obstacleId == 0)
                return;

            if (_registeredObstacles.Remove(obstacleId))
                MarkAllVolumesDirty();
        }

        internal static NativeArray<NavObstaclePrimitive> CreateObstacleSnapshot(Allocator allocator)
        {
            int obstacleCount = 0;
            Dictionary<int, ObstacleRegistration>.Enumerator countEnumerator = _registeredObstacles.GetEnumerator();
            while (countEnumerator.MoveNext())
            {
                ObstacleRegistration registration = countEnumerator.Current.Value;
                if (registration == null)
                    continue;

                obstacleCount += CountLiveColliders(registration.Boxes);
                obstacleCount += CountLiveColliders(registration.Capsules);
            }

            HectonMapMagicVegetationBridge activeBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            obstacleCount += CountMacroFloraObstacles(activeBridge);
            obstacleCount += CountPersistentDynamicObstacles();

            if (obstacleCount <= 0)
                return default;

            NativeArray<NavObstaclePrimitive> snapshot = new NativeArray<NavObstaclePrimitive>(obstacleCount, allocator, NativeArrayOptions.UninitializedMemory);
            NativeMemorySentinel.RegisterNativeArray(
                snapshot,
                NativeMemoryOwner,
                ObstacleSnapshotNativeMemoryLabel,
                ResolveObstacleSnapshotLifetime(allocator));
            int writeIndex = 0;
            Dictionary<int, ObstacleRegistration>.Enumerator writeEnumerator = _registeredObstacles.GetEnumerator();
            while (writeEnumerator.MoveNext())
            {
                ObstacleRegistration registration = writeEnumerator.Current.Value;
                if (registration == null)
                    continue;

                WriteColliderBounds(registration.Boxes, ref snapshot, ref writeIndex);
                WriteColliderBounds(registration.Capsules, ref snapshot, ref writeIndex);
            }

            WriteMacroFloraObstacles(activeBridge, ref snapshot, ref writeIndex);
            WritePersistentDynamicObstacles(ref snapshot, ref writeIndex);

            return snapshot;
        }

        internal static void DisposeObstacleSnapshot(ref NativeArray<NavObstaclePrimitive> snapshot)
        {
            if (!snapshot.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(snapshot);
            snapshot.Dispose();
            snapshot = default;
        }

        internal static JobHandle DisposeObstacleSnapshot(
            ref NativeArray<NavObstaclePrimitive> snapshot,
            JobHandle dependency)
        {
            if (!snapshot.IsCreated)
                return dependency;

            NativeMemorySentinel.UnregisterNativeArray(snapshot);
            JobHandle disposeHandle = snapshot.Dispose(dependency);
            snapshot = default;
            return disposeHandle;
        }

        private static NativeAllocationLifetime ResolveObstacleSnapshotLifetime(Allocator allocator)
        {
            switch (allocator)
            {
                case Allocator.Temp:
                    return NativeAllocationLifetime.Temp;
                case Allocator.TempJob:
                    return NativeAllocationLifetime.TempJob;
                case Allocator.Persistent:
                    return NativeMemoryLifetime;
                default:
                    return NativeAllocationLifetime.TransientArena;
            }
        }

        internal static void EnqueueDynamicObstacleGrowth(float3 center, float3 extents, float expansionMeters)
        {
            if (!math.all(math.isfinite(center)) ||
                !math.all(math.isfinite(extents)) ||
                !math.isfinite(expansionMeters) ||
                !HasPositiveObstacleExtents(extents))
            {
                return;
            }

            EnsureInitialized();
            float lateralExpansion = math.max(0f, expansionMeters);
            float3 expandedExtents = extents + new float3(lateralExpansion, math.max(0f, lateralExpansion * 0.25f), lateralExpansion);
            if (!IsValidDynamicObstacleBounds(center, expandedExtents))
                return;

            RegisterPersistentDynamicObstacle(center, expandedExtents);
            MarkAllVolumesDirty();
            TryEnqueueDynamicObstacleClear(new DynamicObstacleClearRequest
            {
                Center = center,
                Extents = expandedExtents
            });
        }

        internal static void EnqueueDestroyedOrganicEvents(NativeList<DestroyedOrganicEvent> destroyedEvents)
        {
            if (!destroyedEvents.IsCreated || destroyedEvents.Length <= 0)
                return;

            EnsureInitialized();
            for (int i = 0; i < destroyedEvents.Length; i++)
                EnqueueDestroyedOrganicEvent(destroyedEvents[i]);
        }

        internal static void EnqueueDestroyedOrganicEvents(NativeArray<DestroyedOrganicEvent> destroyedEvents, int count)
        {
            if (!destroyedEvents.IsCreated || count <= 0)
                return;

            EnsureInitialized();
            int safeCount = math.min(count, destroyedEvents.Length);
            for (int i = 0; i < safeCount; i++)
                EnqueueDestroyedOrganicEvent(destroyedEvents[i]);
        }

        private static void EnqueueDestroyedOrganicEvent(in DestroyedOrganicEvent destroyedEvent)
        {
            float3 center = destroyedEvent.NavObstacleCenter;
            float3 extents = destroyedEvent.NavObstacleExtents;
            if (!IsValidDynamicObstacleBounds(center, extents))
                return;

            RemovePersistentDynamicObstacles(center, extents);
            TryEnqueueDynamicObstacleClear(new DynamicObstacleClearRequest
            {
                Center = center,
                Extents = extents
            });
        }

        internal static void CompletePendingDynamicObstacleUpdates()
        {
            Dictionary<int, VolumeRecord>.Enumerator enumerator = _records.GetEnumerator();
            while (enumerator.MoveNext())
            {
                VolumeRecord record = enumerator.Current.Value;
                if (record == null || !record.HasPendingDynamicUpdate || !record.PendingDynamicUpdateHandle.IsCompleted)
                    continue;

                long completionStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                using (_partialClearanceDilationCompleteMarker.Auto())
                {
                    if (!DispatcherJobSwap.TryComplete(ref record.PendingDynamicUpdateHandle, forceComplete: false))
                        continue;
                }

                EvaluateDynamicClearanceBudget(completionStartTimestamp);
                record.HasPendingDynamicUpdate = false;

                NativeArray<byte> passabilitySwap = record.Current;
                record.Current = record.Next;
                record.Next = passabilitySwap;

                NativeArray<ushort> distanceSwap = record.CurrentDistance;
                record.CurrentDistance = record.NextDistance;
                record.NextDistance = distanceSwap;
                EvaluatePureVoidState(record);

                DisposeObstacleSnapshot(ref record.PendingObstacleSnapshot);

                record.PendingRegionMin = int3.zero;
                record.PendingRegionMax = int3.zero;
                if (!record.IsPureVoid)
                    RebuildPortals(record);
                record.PortalsReady = true;
                _portalGraphDirty = true;
            }

            if (_teardownPending)
                DisposePendingCompletedRecords(false);
        }

        internal static void SchedulePendingDynamicObstacleUpdates()
        {
            if (!_pendingObstacleClears.IsCreated)
                return;

            if (HasPendingDynamicObstacleUpdate())
                return;

            if (!TryDequeueValidDynamicClearRequest(out DynamicObstacleClearRequest clearRequest))
                return;

            bool useReducedClearance = _dynamicClearanceFallbackSchedulesRemaining > 0;
            bool scheduledAnyRecord = false;
            Dictionary<int, VolumeRecord>.Enumerator enumerator = _records.GetEnumerator();
            while (enumerator.MoveNext())
            {
                VolumeRecord record = enumerator.Current.Value;
                if (!HasValidRecordBounds(record) ||
                    record.HasPendingDynamicUpdate ||
                    !HasCompleteDynamicUpdateBuffers(record))
                {
                    continue;
                }

                if (!TryResolveDynamicUpdateRegion(record, clearRequest, out int3 regionMin, out int3 regionMax))
                    continue;

                if (!TryResolveVoxelCellCount(record.Dimensions, out int requiredCellCount))
                    continue;

                int requiredBlockCount = ResolvePureVoidBlockCount(requiredCellCount);
                if (requiredBlockCount <= 0 || record.PureVoidBlockFlags.Length < requiredBlockCount)
                    continue;

                int3 regionSize = regionMax - regionMin + 1;
                long regionPointCountLong = (long)regionSize.x * regionSize.y * regionSize.z;
                if (regionPointCountLong <= 0L || regionPointCountLong > int.MaxValue)
                    continue;

                record.PureVoidBlockCount = requiredBlockCount;
                int regionPointCount = (int)regionPointCountLong;
                NativeArray<byte>.Copy(record.Current, record.Next, requiredCellCount);
                NativeArray<ushort>.Copy(record.CurrentDistance, record.NextDistance, requiredCellCount);

                DisposeObstacleSnapshot(ref record.PendingObstacleSnapshot);

                record.PendingObstacleSnapshot = CreateObstacleSnapshot(Allocator.TempJob);
                record.PendingRegionMin = regionMin;
                record.PendingRegionMax = regionMax;

                int clearanceRadiusCells = ResolveDynamicClearanceRadiusCells(record.CellSize, useReducedClearance);
                JobHandle resetHandle = new PartialObstacleResetAndStampJob
                {
                    BasePassability = record.BaseCurrent,
                    Passability = record.Next,
                    Obstacles = record.PendingObstacleSnapshot,
                    Dimensions = record.Dimensions,
                    RegionMin = regionMin,
                    RegionMax = regionMax,
                    Origin = record.Origin,
                    CellSize = record.CellSize
                }.Schedule(regionPointCount, 64);

                JobHandle dilationHandle;
                using (_partialClearanceDilationScheduleMarker.Auto())
                {
                    dilationHandle = new PartialClearanceDilationJob
                    {
                        Passability = record.Next,
                        ReferenceDistanceMap = record.CurrentDistance,
                        DistanceMap = record.NextDistance,
                        Dimensions = record.Dimensions,
                        RegionMin = regionMin,
                        RegionMax = regionMax,
                        AgentRadiusCells = clearanceRadiusCells
                    }.Schedule(resetHandle);
                }

                record.PendingDynamicUpdateHandle = SchedulePureVoidScan(
                    record.Next,
                    record.NextDistance,
                    record.PureVoidBlockFlags,
                    requiredCellCount,
                    dilationHandle);

                record.HasPendingDynamicUpdate = true;
                record.PortalsReady = false;
                scheduledAnyRecord = true;
            }

            if (scheduledAnyRecord && useReducedClearance)
                _dynamicClearanceFallbackSchedulesRemaining--;
        }

        private static bool HasPendingDynamicObstacleUpdate()
        {
            Dictionary<int, VolumeRecord>.Enumerator enumerator = _records.GetEnumerator();
            while (enumerator.MoveNext())
            {
                VolumeRecord record = enumerator.Current.Value;
                if (record != null && record.HasPendingDynamicUpdate)
                    return true;
            }

            return false;
        }

        private static bool IsPortalRouteReady(VolumeRecord startRecord, VolumeRecord endRecord)
        {
            return startRecord != null &&
                   endRecord != null &&
                   !startRecord.HasPendingDynamicUpdate &&
                   !endRecord.HasPendingDynamicUpdate &&
                   startRecord.PortalsReady &&
                   endRecord.PortalsReady;
        }

        private static bool TryDequeueValidDynamicClearRequest(out DynamicObstacleClearRequest request)
        {
            request = default;
            int scanBudget = _pendingObstacleClearQueueCount > 0
                ? _pendingObstacleClearQueueCount
                : PendingObstacleClearQueueCapacity;
            while (scanBudget-- > 0 &&
                   _pendingObstacleClears.TryDequeue(out DynamicObstacleClearRequest candidate))
            {
                if (_pendingObstacleClearQueueCount > 0)
                    _pendingObstacleClearQueueCount--;

                if (!IsValidDynamicObstacleBounds(candidate.Center, candidate.Extents))
                    continue;

                request = candidate;
                return true;
            }

            return false;
        }

        internal static bool TryResolveMacroFloraObstacleWorldBounds(
            Matrix4x4 matrix,
            HectonVegetationInstanceData metadata,
            int typeId,
            int semanticType,
            out float3 center,
            out float3 extents)
        {
            center = float3.zero;
            extents = float3.zero;
            if (!TryResolveMacroFloraObstacle(metadata, typeId, semanticType, out float3 centerOffset, out extents))
                return false;

            double3 stableUniverseRoot = new double3(matrix.m03, matrix.m13, matrix.m23);
            Vector3 runtimeRoot = HectonMapMagicVegetationBridge.ToRuntimeSpace(stableUniverseRoot);
            float3 runtimeRoot3 = new float3(runtimeRoot.x, runtimeRoot.y, runtimeRoot.z);
            center = runtimeRoot3 + centerOffset;
            return math.all(math.isfinite(runtimeRoot3)) &&
                   math.all(math.isfinite(centerOffset)) &&
                   math.all(math.isfinite(center)) &&
                   math.all(math.isfinite(extents)) &&
                   extents.x > 0f &&
                   extents.y > 0f &&
                   extents.z > 0f;
        }

        internal static bool TryGetPassabilityPayload(
            HectonVoxelVolume volume,
            out NativeArray<byte> passability,
            out int3 dimensions,
            out float3 origin,
            out float cellSize)
        {
            passability = default;
            dimensions = int3.zero;
            origin = float3.zero;
            cellSize = 0f;
            if (volume == null)
                return false;

            int volumeInstanceId = GetStableVolumeEntityId(volume);
            if (!_records.TryGetValue(volumeInstanceId, out VolumeRecord record) ||
                !HasValidRecordBounds(record))
            {
                return false;
            }

            passability = record.Current;
            dimensions = record.Dimensions;
            origin = record.Origin;
            cellSize = record.CellSize;
            return true;
        }

        internal static bool TryBuildMacroPortalRouteNonAlloc(
            float3 startWorldPosition,
            float3 endWorldPosition,
            Vector3[] outputWaypoints,
            out int waypointCount)
        {
            waypointCount = 0;
            if (outputWaypoints == null || outputWaypoints.Length < 2)
                return false;

            if (!TryResolveRecord(startWorldPosition, out VolumeRecord startRecord) ||
                !TryResolveRecord(endWorldPosition, out VolumeRecord endRecord) ||
                startRecord == null ||
                endRecord == null ||
                startRecord == endRecord ||
                !IsPortalRouteReady(startRecord, endRecord))
            {
                return false;
            }

            EnsurePortalGraphBuilt();
            if (_portalGraphNodes.Count <= 0 ||
                startRecord.PortalCount <= 0 ||
                endRecord.PortalCount <= 0 ||
                !TrySolvePortalRoute(startRecord, endRecord, startWorldPosition, endWorldPosition))
            {
                return false;
            }

            if (!CanEmitPortalRoutePath(outputWaypoints.Length))
                return false;

            outputWaypoints[waypointCount++] = new Vector3(startWorldPosition.x, startWorldPosition.y, startWorldPosition.z);
            for (int i = _routePathScratch.Count - 1; i >= 0; i--)
            {
                PortalNode node = _portalGraphNodes[_routePathScratch[i]];
                outputWaypoints[waypointCount++] = new Vector3(node.Centroid.x, node.Centroid.y, node.Centroid.z);
            }

            outputWaypoints[waypointCount++] = new Vector3(endWorldPosition.x, endWorldPosition.y, endWorldPosition.z);
            return waypointCount >= 2;
        }

        internal static bool TryGetContainingPassabilityPayload(
            float3 worldPosition,
            out NativeArray<byte> passability,
            out int3 dimensions,
            out float3 origin,
            out float cellSize)
        {
            passability = default;
            dimensions = int3.zero;
            origin = float3.zero;
            cellSize = 0f;
            if (!TryResolveContainingRecord(worldPosition, out VolumeRecord record) ||
                !HasValidRecordBounds(record))
            {
                return false;
            }

            passability = record.Current;
            dimensions = record.Dimensions;
            origin = record.Origin;
            cellSize = record.CellSize;
            return true;
        }

        internal static bool TryGetNearestPassabilityPayload(
            float3 worldPosition,
            out NativeArray<byte> passability,
            out int3 dimensions,
            out float3 origin,
            out float cellSize)
        {
            passability = default;
            dimensions = int3.zero;
            origin = float3.zero;
            cellSize = 0f;
            if (!math.all(math.isfinite(worldPosition)))
                return false;

            bool foundContainingRecord = false;
            float nearestDistanceSq = float.MaxValue;
            VolumeRecord nearestRecord = null;
            Dictionary<int, VolumeRecord>.Enumerator enumerator = _records.GetEnumerator();
            while (enumerator.MoveNext())
            {
                VolumeRecord candidate = enumerator.Current.Value;
                if (!VoxelDynamicNavGridRuntime.HasValidRecordBounds(candidate))
                {
                    continue;
                }

                if (ContainsPoint(candidate, worldPosition))
                {
                    nearestRecord = candidate;
                    foundContainingRecord = true;
                    break;
                }

                float distanceSq = DistanceSqToBounds(candidate, worldPosition);
                if (distanceSq < nearestDistanceSq)
                {
                    nearestDistanceSq = distanceSq;
                    nearestRecord = candidate;
                }
            }

            if (nearestRecord == null ||
                !HasValidRecordBounds(nearestRecord))
                return false;

            // Prefer containing volumes, otherwise allow nearest-volume binding for edge probes near cave mouths.
            if (!foundContainingRecord && nearestDistanceSq > math.max(nearestRecord.CellSize * nearestRecord.CellSize, 1f))
                return false;

            passability = nearestRecord.Current;
            dimensions = nearestRecord.Dimensions;
            origin = nearestRecord.Origin;
            cellSize = nearestRecord.CellSize;
            return true;
        }

        internal static bool TryResolveNearestSafeNode(Vector3 runtimePosition, out Vector3 safeRuntimePosition)
        {
            safeRuntimePosition = default;
            float3 worldPosition = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(worldPosition)))
                return false;

            float bestDistanceSq = float.MaxValue;
            float3 bestPosition = float3.zero;
            bool found = false;
            Dictionary<int, VolumeRecord>.Enumerator enumerator = _records.GetEnumerator();
            while (enumerator.MoveNext())
            {
                VolumeRecord record = enumerator.Current.Value;
                if (!VoxelDynamicNavGridRuntime.HasValidRecordBounds(record))
                {
                    continue;
                }

                float invCellSize = math.rcp(record.CellSize);
                float3 local = (worldPosition - record.Origin) * invCellSize;
                int3 centerCell = new int3(
                    math.clamp((int)math.floor(local.x), 0, math.max(0, record.Dimensions.x - 1)),
                    math.clamp((int)math.floor(local.y), 0, math.max(0, record.Dimensions.y - 1)),
                    math.clamp((int)math.floor(local.z), 0, math.max(0, record.Dimensions.z - 1)));
                int searchRadius = math.min(
                    ToxicBodySafeNodeSearchRadiusCells,
                    math.max(record.Dimensions.x, math.max(record.Dimensions.y, record.Dimensions.z)));

                for (int radius = 0; radius <= searchRadius; radius++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        for (int y = -radius; y <= radius; y++)
                        {
                            for (int x = -radius; x <= radius; x++)
                            {
                                if (math.max(math.abs(x), math.max(math.abs(y), math.abs(z))) != radius)
                                    continue;

                                int3 candidate = centerCell + new int3(x, y, z);
                                if (candidate.x < 0 ||
                                    candidate.y < 0 ||
                                    candidate.z < 0 ||
                                    candidate.x >= record.Dimensions.x ||
                                    candidate.y >= record.Dimensions.y ||
                                    candidate.z >= record.Dimensions.z)
                                {
                                    continue;
                                }

                                int flatIndex = candidate.x +
                                                (candidate.y * record.Dimensions.x) +
                                                (candidate.z * record.Dimensions.x * record.Dimensions.y);
                                if (flatIndex < 0 ||
                                    flatIndex >= record.Current.Length ||
                                    record.Current[flatIndex] != OpenCell)
                                {
                                    continue;
                                }

                                float3 candidatePosition = record.Origin +
                                                           ((new float3(candidate.x, candidate.y, candidate.z) + 0.5f) * record.CellSize);
                                float distanceSq = math.lengthsq(candidatePosition - worldPosition);
                                if (distanceSq >= bestDistanceSq)
                                    continue;

                                bestDistanceSq = distanceSq;
                                bestPosition = candidatePosition;
                                found = true;
                            }
                        }
                    }

                    if (found)
                        break;
                }
            }

            if (!found || !math.all(math.isfinite(bestPosition)))
                return false;

            safeRuntimePosition = new Vector3(bestPosition.x, bestPosition.y, bestPosition.z);
            return true;
        }

        internal static HybridNavigationMode SampleHybridNavigationMode(float3 worldPosition)
        {
            return TrySampleHybridNavigation(worldPosition, out HybridNavigationSample sample)
                ? sample.Mode
                : HybridNavigationMode.OpenWaterHeightmap;
        }

        internal static bool TrySampleHybridNavigation(float3 worldPosition, out HybridNavigationSample sample)
        {
            sample = default;
            sample.Mode = HybridNavigationMode.OpenWaterHeightmap;
            if (!math.all(math.isfinite(worldPosition)))
                return false;

            sample.FloorBoundaryY = worldPosition.y;
            HectonMapMagicVegetationBridge activeBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (activeBridge != null &&
                activeBridge.TryGetCachedTerrainHeight(worldPosition.x, worldPosition.z, out float terrainHeight) &&
                math.isfinite(terrainHeight))
            {
                sample.TerrainHeight = terrainHeight;
                sample.FloorBoundaryY = terrainHeight;
                sample.HasTerrainHeight = 1;
            }

            if (!TryResolveContainingRecord(worldPosition, out VolumeRecord record) ||
                !HasValidRecordBounds(record))
            {
                return sample.HasTerrainHeight != 0;
            }

            if (!TrySamplePassabilityCell(record, worldPosition, out int3 voxel, out byte passability))
                return false;

            sample.Passability = passability;
            sample.CellSize = record.CellSize;
            sample.CellOrigin = record.Origin + (new float3(voxel.x, voxel.y, voxel.z) * record.CellSize);
            if (!math.all(math.isfinite(sample.CellOrigin)))
                return false;

            sample.FloorBoundaryY = sample.CellOrigin.y;
            sample.Mode = passability == OpenCell
                ? HybridNavigationMode.CaveVoxel
                : HybridNavigationMode.SolidVoxel;
            return true;
        }

        internal static bool TryBuildMacroPortalRoute(float3 startWorldPosition, float3 endWorldPosition, NativeList<Vector3> outputWaypoints)
        {
            if (!outputWaypoints.IsCreated)
                return false;

            if (!TryResolveRecord(startWorldPosition, out VolumeRecord startRecord) ||
                !TryResolveRecord(endWorldPosition, out VolumeRecord endRecord) ||
                startRecord == null ||
                endRecord == null ||
                startRecord == endRecord ||
                !IsPortalRouteReady(startRecord, endRecord))
            {
                return false;
            }

            EnsurePortalGraphBuilt();
            if (_portalGraphNodes.Count <= 0 ||
                startRecord.PortalCount <= 0 ||
                endRecord.PortalCount <= 0)
            {
                return false;
            }

            if (!TrySolvePortalRoute(startRecord, endRecord, startWorldPosition, endWorldPosition))
                return false;

            if (!CanEmitPortalRoutePath(outputWaypoints.Capacity))
                return false;

            outputWaypoints.Clear();
            outputWaypoints.AddNoResize(new Vector3(startWorldPosition.x, startWorldPosition.y, startWorldPosition.z));
            for (int i = _routePathScratch.Count - 1; i >= 0; i--)
            {
                PortalNode node = _portalGraphNodes[_routePathScratch[i]];
                outputWaypoints.AddNoResize(new Vector3(node.Centroid.x, node.Centroid.y, node.Centroid.z));
            }

            outputWaypoints.AddNoResize(new Vector3(endWorldPosition.x, endWorldPosition.y, endWorldPosition.z));
            return outputWaypoints.Length >= 2;
        }

        internal static void UnregisterVolume(HectonVoxelVolume volume)
        {
            if (volume == null)
                return;

            int volumeInstanceId = GetStableVolumeEntityId(volume);
            if (_records.TryGetValue(volumeInstanceId, out VolumeRecord record))
            {
                record.PendingRemoval = true;
                if (record.TryDisposeCompleted())
                {
                    _records.Remove(volumeInstanceId);
                    _portalGraphDirty = true;
                    return;
                }

                _teardownPending = true;
            }
        }

        internal static void DisposeAll()
        {
            if (!DisposePendingCompletedRecords(true))
            {
                _clearRuntimeContainersWhenTeardownCompletes = true;
                return;
            }

            ClearRuntimeContainers();
        }

        internal static void DisposeCompletedTeardownRecords()
        {
            DisposePendingCompletedRecords(false);
        }

        internal static void ClearLifecycleOwner(VoxelDynamicNavGridRuntimeLifecycle owner)
        {
            if (_lifecycleOwner == owner)
                _lifecycleOwner = null;
        }

        private static bool DisposePendingCompletedRecords(bool markAllRecordsForRemoval)
        {
            bool blockedByPendingJob = false;
            _recordRemovalScratch.Clear();

            Dictionary<int, VolumeRecord>.Enumerator enumerator = _records.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<int, VolumeRecord> pair = enumerator.Current;
                VolumeRecord record = pair.Value;
                if (record == null)
                {
                    _recordRemovalScratch.Add(pair.Key);
                    continue;
                }

                if (markAllRecordsForRemoval)
                    record.PendingRemoval = true;

                if (!record.PendingRemoval)
                    continue;

                if (record.TryDisposeCompleted())
                {
                    _recordRemovalScratch.Add(pair.Key);
                    continue;
                }

                blockedByPendingJob = true;
            }

            for (int i = 0; i < _recordRemovalScratch.Count; i++)
            {
                _records.Remove(_recordRemovalScratch[i]);
            }

            _recordRemovalScratch.Clear();
            _teardownPending = blockedByPendingJob;
            if (!blockedByPendingJob)
            {
                _portalGraphDirty = true;
                if (_clearRuntimeContainersWhenTeardownCompletes)
                {
                    _clearRuntimeContainersWhenTeardownCompletes = false;
                    ClearRuntimeContainers();
                }
            }

            return !blockedByPendingJob;
        }

        private static void ClearRuntimeContainers()
        {
            _records.Clear();
            _dirtyRequestSpill.Clear();
            _deferredDirtyVolumes.Clear();
            _portalGraphNodes.Clear();
            _routeNodeScratch.Clear();
            _routeOpenSetScratch.Clear();
            _routePathScratch.Clear();
            _recordRemovalScratch.Clear();
            _registeredObstacles.Clear();
            _portalGraphDirty = true;
            _teardownPending = false;
            _clearRuntimeContainersWhenTeardownCompletes = false;
            _dirtyVolumeQueueCount = 0;
            _pendingObstacleClearQueueCount = 0;
            if (_dirtyVolumes.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(VoxelDynamicNavGridRuntime), nameof(_dirtyVolumes));
                _dirtyVolumes.Dispose();
                _dirtyVolumes = default;
            }

            if (_pendingObstacleClears.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(VoxelDynamicNavGridRuntime), nameof(_pendingObstacleClears));
                _pendingObstacleClears.Dispose();
                _pendingObstacleClears = default;
            }

            if (_persistentDynamicObstacles.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(
                    nameof(VoxelDynamicNavGridRuntime),
                    nameof(_persistentDynamicObstacles));
                _persistentDynamicObstacles.Dispose();
                _persistentDynamicObstacles = default;
            }

            _persistentDynamicObstacleWriteCursor = 0;
        }

        private static void EnsureInitialized()
        {
            EnsureLifecycleOwner();
            if (_teardownPending)
                DisposePendingCompletedRecords(false);

            if (!_dirtyVolumes.IsCreated)
            {
                _dirtyVolumes = new NativeQueue<DirtyVolumeRequest>(Allocator.Persistent); // COLD ALLOC: NativeQueue<DirtyVolumeRequest>[32] - dirty voxel volume rebuild requests - owner: VoxelDynamicNavGridRuntime
                NativeMemorySentinel.RegisterNativeQueue(
                    _dirtyVolumes,
                    DirtyVolumeQueueCapacity,
                    nameof(VoxelDynamicNavGridRuntime),
                    nameof(_dirtyVolumes),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _dirtyVolumes, DirtyVolumeQueueCapacity);
            }

            if (!_pendingObstacleClears.IsCreated)
            {
                _pendingObstacleClears = new NativeQueue<DynamicObstacleClearRequest>(Allocator.Persistent); // COLD ALLOC: NativeQueue<DynamicObstacleClearRequest>[16] - destroyed-organic obstacle clear queue - owner: VoxelDynamicNavGridRuntime
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingObstacleClears,
                    PendingObstacleClearQueueCapacity,
                    nameof(VoxelDynamicNavGridRuntime),
                    nameof(_pendingObstacleClears),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingObstacleClears, PendingObstacleClearQueueCapacity);
            }

            if (!_persistentDynamicObstacles.IsCreated)
            {
                _persistentDynamicObstacles = new NativeList<NavObstaclePrimitive>(MaxPersistentDynamicObstacleCount, Allocator.Persistent); // COLD ALLOC: NativeList<NavObstaclePrimitive>[512] - overgrowth/vine dynamic obstacle snapshot lane - owner: VoxelDynamicNavGridRuntime
                NativeMemorySentinel.RegisterNativeList(
                    _persistentDynamicObstacles,
                    nameof(VoxelDynamicNavGridRuntime),
                    nameof(_persistentDynamicObstacles),
                NativeAllocationLifetime.Session);
            }
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

        private static void EnsureLifecycleOwner()
        {
            if (_lifecycleOwner != null || !Application.isPlaying)
                return;

            GameObject lifecycleRoot = new GameObject("[VoxelDynamicNavGridRuntime]"); // COLD ALLOC: GameObject[1] - static navgrid native-container lifecycle owner - owner: VoxelDynamicNavGridRuntime
            _lifecycleOwner = lifecycleRoot.AddComponent<VoxelDynamicNavGridRuntimeLifecycle>();
            GameBootstrapper.PersistRuntimeService(_lifecycleOwner);
        }

        internal static void MarkAllVolumesDirty()
        {
            Dictionary<int, VolumeRecord>.Enumerator enumerator = _records.GetEnumerator();
            while (enumerator.MoveNext())
            {
                VolumeRecord record = enumerator.Current.Value;
                if (record != null)
                    record.IsDirty = true;
            }
        }

        internal static int ResolveClearanceRadiusCells(float cellSize)
        {
            if (cellSize <= 0f || !math.isfinite(cellSize))
                return 1;

            return math.max(1, (int)math.ceil(DefaultPredatorClearanceRadiusMeters * math.rcp(cellSize)));
        }

        private static int ResolveDynamicClearanceRadiusCells(float cellSize, bool useReducedClearance)
        {
            int fullRadiusCells = ResolveClearanceRadiusCells(cellSize);
            return useReducedClearance
                ? math.max(1, (fullRadiusCells + 1) >> 1)
                : fullRadiusCells;
        }

        private static void EvaluateDynamicClearanceBudget(long completionStartTimestamp)
        {
            if (completionStartTimestamp <= 0L)
                return;

            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - completionStartTimestamp;
            double elapsedMilliseconds = elapsedTicks * 1000.0d * math.rcp((double)System.Diagnostics.Stopwatch.Frequency);
            if (elapsedMilliseconds <= PartialClearanceDilationBudgetMilliseconds)
                return;

            _dynamicClearanceFallbackSchedulesRemaining = math.max(
                _dynamicClearanceFallbackSchedulesRemaining,
                DynamicClearanceFallbackScheduleCount);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            float now = Time.unscaledTime;
            if (now >= _nextDynamicClearanceWarningTime)
            {
                _nextDynamicClearanceWarningTime = now + DynamicClearanceWarningCooldownSeconds;
                Debug.LogWarning(DynamicClearanceBudgetWarningMessage);
            }
#endif
        }

        private static int GetStableVolumeEntityId(HectonVoxelVolume volume)
        {
            return volume != null
                ? unchecked((int)EntityId.ToULong(volume.GetEntityId()))
                : 0;
        }

        private static VolumeRecord GetOrCreateRecord(int volumeInstanceId)
        {
            if (!_records.TryGetValue(volumeInstanceId, out VolumeRecord record))
            {
                if (_records.Count >= MaxTrackedVolumeRecords)
                    return null;

                record = new VolumeRecord();
                _records.Add(volumeInstanceId, record);
            }

            return record;
        }

        private static bool ConsumeDirtyMarker(int volumeInstanceId, int runtimeStamp)
        {
            if (!_dirtyVolumes.IsCreated)
                return false;

            bool found = false;
            _dirtyRequestSpill.Clear();
            int scanBudget = _dirtyVolumeQueueCount > 0
                ? _dirtyVolumeQueueCount
                : DirtyVolumeQueueCapacity;
            while (scanBudget-- > 0 &&
                   _dirtyVolumes.TryDequeue(out DirtyVolumeRequest request))
            {
                if (_dirtyVolumeQueueCount > 0)
                    _dirtyVolumeQueueCount--;

                if (!found &&
                    request.VolumeInstanceId == volumeInstanceId &&
                    request.RuntimeStamp == runtimeStamp)
                {
                    found = true;
                    continue;
                }

                _dirtyRequestSpill.Add(request);
            }

            _dirtyVolumeQueueCount = 0;
            for (int i = 0; i < _dirtyRequestSpill.Count; i++)
            {
                if (_dirtyVolumeQueueCount >= DirtyVolumeQueueCapacity)
                    break;

                _dirtyVolumes.Enqueue(_dirtyRequestSpill[i]);
                _dirtyVolumeQueueCount++;
            }

            _dirtyRequestSpill.Clear();
            return found;
        }

        private static void TryEnqueueDynamicObstacleClear(DynamicObstacleClearRequest request)
        {
            if (!_pendingObstacleClears.IsCreated ||
                !IsValidDynamicObstacleBounds(request.Center, request.Extents))
            {
                return;
            }

            if (_pendingObstacleClearQueueCount >= PendingObstacleClearQueueCapacity)
            {
                MarkAllVolumesDirty();
                return;
            }

            _pendingObstacleClears.Enqueue(request);
            _pendingObstacleClearQueueCount++;
        }

        private static bool IsValidDynamicObstacleBounds(float3 center, float3 extents)
        {
            return math.all(math.isfinite(center)) &&
                   math.all(math.isfinite(extents)) &&
                   HasPositiveObstacleExtents(extents);
        }

        private static bool HasPositiveObstacleExtents(float3 extents)
        {
            return extents.x > 0.0001f &&
                   extents.y > 0.0001f &&
                   extents.z > 0.0001f;
        }

        private static void EnsureBuffer(ref NativeArray<byte> buffer, int length, string label)
        {
            if (buffer.IsCreated && buffer.Length == length)
                return;

            if (buffer.IsCreated)
                DisposeTrackedNativeArray(ref buffer);

            buffer = new NativeArray<byte>(length, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[pointCount] - double-buffered voxel passability snapshot - owner: VoxelDynamicNavGridRuntime
            NativeMemorySentinel.RegisterNativeArray(buffer, NativeMemoryOwner, label, NativeMemoryLifetime);
        }

        private static void EnsureBuffer(ref NativeArray<ushort> buffer, int length, string label)
        {
            if (buffer.IsCreated && buffer.Length == length)
                return;

            if (buffer.IsCreated)
                DisposeTrackedNativeArray(ref buffer);

            buffer = new NativeArray<ushort>(length, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ushort>[pointCount] - double-buffered voxel clearance-distance snapshot - owner: VoxelDynamicNavGridRuntime
            NativeMemorySentinel.RegisterNativeArray(buffer, NativeMemoryOwner, label, NativeMemoryLifetime);
        }

        private static void EnsureBuffer(ref NativeArray<int> buffer, int length, string label)
        {
            if (buffer.IsCreated && buffer.Length == length)
                return;

            if (buffer.IsCreated)
                DisposeTrackedNativeArray(ref buffer);

            buffer = new NativeArray<int>(length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<int>[pureVoidBlockCount] - Burst pure-void block scan flags - owner: VoxelDynamicNavGridRuntime
            NativeMemorySentinel.RegisterNativeArray(buffer, NativeMemoryOwner, label, NativeMemoryLifetime);
        }

        internal static JobHandle SchedulePureVoidScan(
            NativeArray<byte> passability,
            NativeArray<ushort> distanceMap,
            NativeArray<int> blockFlags,
            int pointCount,
            JobHandle dependency)
        {
            if (!passability.IsCreated ||
                !distanceMap.IsCreated ||
                !blockFlags.IsCreated ||
                pointCount <= 0 ||
                passability.Length < pointCount ||
                distanceMap.Length < pointCount)
            {
                return dependency;
            }

            int requiredBlockCount = ResolvePureVoidBlockCount(pointCount);
            if (requiredBlockCount <= 0 || blockFlags.Length < requiredBlockCount)
                return dependency;

            return new PureVoidBlockScanJob
            {
                Passability = passability,
                DistanceMap = distanceMap,
                BlockFlags = blockFlags,
                PointCount = pointCount
            }.Schedule(requiredBlockCount, 32, dependency);
        }

        internal static int ResolvePureVoidBlockCount(int pointCount)
        {
            if (pointCount <= 0)
                return 1;

            long blockCount = ((long)pointCount + PureVoidScanBlockSize - 1L) >> PureVoidScanBlockShift;
            if (blockCount <= 0L)
                return 1;

            return blockCount > int.MaxValue
                ? int.MaxValue
                : (int)blockCount;
        }

        private static void EvaluatePureVoidState(VolumeRecord record)
        {
            if (record == null || !IsPureVoidSnapshot(record))
            {
                if (record != null)
                    record.IsPureVoid = false;
                return;
            }

            if (!ReleaseVoxelBuffers(record))
            {
                record.IsPureVoid = false;
                return;
            }

            record.IsPureVoid = true;
            record.PortalsReady = true;
            record.PortalCount = 0;
            record.FaceVisitStamp = 0;
        }

        private static bool IsPureVoidSnapshot(VolumeRecord record)
        {
            if (!HasValidRecordBounds(record) ||
                !record.CurrentDistance.IsCreated ||
                !record.PureVoidBlockFlags.IsCreated)
            {
                return false;
            }

            if (!TryResolveVoxelCellCount(record.Dimensions, out int requiredCellCount))
                return false;

            int requiredBlockCount = ResolvePureVoidBlockCount(requiredCellCount);
            if (requiredBlockCount <= 0 ||
                record.CurrentDistance.Length < requiredCellCount ||
                record.PureVoidBlockCount != requiredBlockCount ||
                record.PureVoidBlockFlags.Length < requiredBlockCount)
            {
                return false;
            }

            for (int i = 0; i < requiredBlockCount; i++)
            {
                if (record.PureVoidBlockFlags[i] == 0)
                    return false;
            }

            return true;
        }

        private static bool ReleaseVoxelBuffers(VolumeRecord record)
        {
            if (record.HasPendingDynamicUpdate)
                return false;

            DisposeTrackedNativeArray(ref record.Current);
            DisposeTrackedNativeArray(ref record.Next);
            DisposeTrackedNativeArray(ref record.BaseCurrent);
            DisposeTrackedNativeArray(ref record.BaseNext);
            DisposeTrackedNativeArray(ref record.CurrentDistance);
            DisposeTrackedNativeArray(ref record.NextDistance);
            DisposeTrackedNativeArray(ref record.PureVoidBlockFlags);
            record.PureVoidBlockCount = 0;
            return true;
        }

        private static void DisposeTrackedNativeArray<T>(ref NativeArray<T> buffer)
            where T : struct
        {
            if (!buffer.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(buffer);
            buffer.Dispose();
            buffer = default;
        }

        private static int FlattenIndex(int x, int y, int z, int3 dimensions)
        {
            if (x < 0 || y < 0 || z < 0 || x >= dimensions.x || y >= dimensions.y || z >= dimensions.z)
                return -1;

            return x + (y * dimensions.x) + (z * dimensions.x * dimensions.y);
        }

        private static int CountLiveColliders<T>(T[] colliders)
            where T : Collider
        {
            if (colliders == null || colliders.Length <= 0)
                return 0;

            int count = 0;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (TryResolveColliderObstacleBounds(colliders[i], out _, out _))
                    count++;
            }

            return count;
        }

        private static bool TryResolveColliderObstacleBounds<T>(T collider, out float3 center, out float3 extents)
            where T : Collider
        {
            center = float3.zero;
            extents = float3.zero;
            if (collider == null ||
                !collider.enabled ||
                !collider.gameObject.activeInHierarchy)
            {
                return false;
            }

            Bounds bounds = collider.bounds;
            center = bounds.center;
            extents = bounds.extents;
            return IsValidDynamicObstacleBounds(center, extents);
        }

        private static void WriteColliderBounds<T>(T[] colliders, ref NativeArray<NavObstaclePrimitive> snapshot, ref int writeIndex)
            where T : Collider
        {
            if (colliders == null ||
                colliders.Length <= 0 ||
                !snapshot.IsCreated ||
                writeIndex < 0)
            {
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (writeIndex >= snapshot.Length)
                    return;

                if (!TryResolveColliderObstacleBounds(colliders[i], out float3 center, out float3 extents))
                    continue;

                snapshot[writeIndex] = new NavObstaclePrimitive
                {
                    Center = center,
                    Extents = extents
                };
                writeIndex++;
            }
        }

        private static int CountMacroFloraObstacles(HectonMapMagicVegetationBridge vegetationBridge)
        {
            if (vegetationBridge == null)
                return 0;

            int obstacleCount = 0;
            if (vegetationBridge.TryGetActiveUnderwaterNativePayload(
                    out NativeArray<Matrix4x4> underwaterMatrices,
                    out NativeArray<HectonVegetationInstanceData> underwaterMetadata,
                    out NativeArray<int> underwaterTypes,
                    out int underwaterCount) &&
                vegetationBridge.TryGetActiveUnderwaterSemanticPayload(
                    out NativeArray<int> underwaterSemanticTypes,
                    out _,
                    out int underwaterSemanticCount))
            {
                obstacleCount += CountMacroFloraObstacles(
                    underwaterMatrices,
                    underwaterMetadata,
                    underwaterTypes,
                    underwaterSemanticTypes,
                    math.min(underwaterCount, underwaterSemanticCount));
            }

            if (vegetationBridge.TryGetActiveSurfaceNativePayload(
                    out NativeArray<Matrix4x4> surfaceMatrices,
                    out NativeArray<HectonVegetationInstanceData> surfaceMetadata,
                    out NativeArray<int> surfaceTypes,
                    out int surfaceCount) &&
                vegetationBridge.TryGetActiveSurfaceSemanticPayload(
                    out NativeArray<int> surfaceSemanticTypes,
                    out _,
                    out int surfaceSemanticCount))
            {
                obstacleCount += CountMacroFloraObstacles(
                    surfaceMatrices,
                    surfaceMetadata,
                    surfaceTypes,
                    surfaceSemanticTypes,
                    math.min(surfaceCount, surfaceSemanticCount));
            }

            return obstacleCount;
        }

        private static int CountMacroFloraObstacles(
            NativeArray<Matrix4x4> matrices,
            NativeArray<HectonVegetationInstanceData> metadata,
            NativeArray<int> types,
            NativeArray<int> semanticTypes,
            int count)
        {
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated ||
                !semanticTypes.IsCreated ||
                count <= 0)
            {
                return 0;
            }

            int safeCount = math.min(
                count,
                math.min(
                    matrices.Length,
                    math.min(metadata.Length, math.min(types.Length, semanticTypes.Length))));
            int obstacleCount = 0;
            for (int i = 0; i < safeCount; i++)
            {
                if (TryResolveMacroFloraObstacleWorldBounds(matrices[i], metadata[i], types[i], semanticTypes[i], out _, out _))
                    obstacleCount++;
            }

            return obstacleCount;
        }

        private static void WriteMacroFloraObstacles(
            HectonMapMagicVegetationBridge vegetationBridge,
            ref NativeArray<NavObstaclePrimitive> snapshot,
            ref int writeIndex)
        {
            if (vegetationBridge == null || !snapshot.IsCreated)
                return;

            if (vegetationBridge.TryGetActiveUnderwaterNativePayload(out NativeArray<Matrix4x4> underwaterMatrices, out NativeArray<HectonVegetationInstanceData> underwaterMetadata, out NativeArray<int> underwaterTypes, out int underwaterCount) &&
                vegetationBridge.TryGetActiveUnderwaterSemanticPayload(out NativeArray<int> underwaterSemanticTypes, out _, out int underwaterSemanticCount))
            {
                WriteMacroFloraObstacles(
                    underwaterMatrices,
                    underwaterMetadata,
                    underwaterTypes,
                    underwaterSemanticTypes,
                    math.min(underwaterCount, underwaterSemanticCount),
                    ref snapshot,
                    ref writeIndex);
            }

            if (vegetationBridge.TryGetActiveSurfaceNativePayload(out NativeArray<Matrix4x4> surfaceMatrices, out NativeArray<HectonVegetationInstanceData> surfaceMetadata, out NativeArray<int> surfaceTypes, out int surfaceCount) &&
                vegetationBridge.TryGetActiveSurfaceSemanticPayload(out NativeArray<int> surfaceSemanticTypes, out _, out int surfaceSemanticCount))
            {
                WriteMacroFloraObstacles(
                    surfaceMatrices,
                    surfaceMetadata,
                    surfaceTypes,
                    surfaceSemanticTypes,
                    math.min(surfaceCount, surfaceSemanticCount),
                    ref snapshot,
                    ref writeIndex);
            }
        }

        private static void WritePersistentDynamicObstacles(ref NativeArray<NavObstaclePrimitive> snapshot, ref int writeIndex)
        {
            if (!_persistentDynamicObstacles.IsCreated || !snapshot.IsCreated)
                return;

            int capacity = snapshot.Length;
            for (int i = 0; i < _persistentDynamicObstacles.Length && writeIndex < capacity; i++)
            {
                NavObstaclePrimitive obstacle = _persistentDynamicObstacles[i];
                if (!IsValidDynamicObstacleBounds(obstacle.Center, obstacle.Extents))
                    continue;

                snapshot[writeIndex] = obstacle;
                writeIndex++;
            }
        }

        private static int CountPersistentDynamicObstacles()
        {
            if (!_persistentDynamicObstacles.IsCreated || _persistentDynamicObstacles.Length <= 0)
                return 0;

            int count = 0;
            for (int i = 0; i < _persistentDynamicObstacles.Length; i++)
            {
                NavObstaclePrimitive obstacle = _persistentDynamicObstacles[i];
                if (IsValidDynamicObstacleBounds(obstacle.Center, obstacle.Extents))
                    count++;
            }

            return count;
        }

        private static void RegisterPersistentDynamicObstacle(float3 center, float3 extents)
        {
            if (!_persistentDynamicObstacles.IsCreated ||
                !IsValidDynamicObstacleBounds(center, extents))
            {
                return;
            }

            float mergeDistanceSq = PersistentObstacleMergeDistanceMeters * PersistentObstacleMergeDistanceMeters;
            for (int i = 0; i < _persistentDynamicObstacles.Length; i++)
            {
                NavObstaclePrimitive obstacle = _persistentDynamicObstacles[i];
                if (!IsValidDynamicObstacleBounds(obstacle.Center, obstacle.Extents))
                {
                    _persistentDynamicObstacles[i] = new NavObstaclePrimitive
                    {
                        Center = center,
                        Extents = extents
                    };
                    return;
                }

                if (math.lengthsq(obstacle.Center - center) > mergeDistanceSq)
                    continue;

                float3 mergedCenter = obstacle.Center + ((center - obstacle.Center) * 0.5f);
                float3 mergedExtents = math.max(obstacle.Extents, extents);
                if (!IsValidDynamicObstacleBounds(mergedCenter, mergedExtents))
                {
                    _persistentDynamicObstacles[i] = new NavObstaclePrimitive
                    {
                        Center = center,
                        Extents = extents
                    };
                    return;
                }

                obstacle.Center = mergedCenter;
                obstacle.Extents = mergedExtents;
                _persistentDynamicObstacles[i] = obstacle;
                return;
            }

            if (_persistentDynamicObstacles.Length < _persistentDynamicObstacles.Capacity)
            {
                _persistentDynamicObstacles.AddNoResize(new NavObstaclePrimitive
                {
                    Center = center,
                    Extents = extents
                });
                return;
            }

            int writeIndex = math.clamp(_persistentDynamicObstacleWriteCursor, 0, math.max(0, _persistentDynamicObstacles.Length - 1));
            _persistentDynamicObstacles[writeIndex] = new NavObstaclePrimitive
            {
                Center = center,
                Extents = extents
            };
            int nextWriteIndex = writeIndex + 1;
            _persistentDynamicObstacleWriteCursor = nextWriteIndex >= _persistentDynamicObstacles.Length
                ? 0
                : nextWriteIndex;
        }

        private static void RemovePersistentDynamicObstacles(float3 center, float3 extents)
        {
            if (!_persistentDynamicObstacles.IsCreated ||
                _persistentDynamicObstacles.Length <= 0 ||
                !IsValidDynamicObstacleBounds(center, extents))
            {
                return;
            }

            float removeRadius = math.max(
                PersistentObstacleMergeDistanceMeters,
                math.max(extents.x, math.max(extents.y, extents.z)) + PersistentObstacleMergeDistanceMeters);
            float removeRadiusSq = removeRadius * removeRadius;
            for (int i = _persistentDynamicObstacles.Length - 1; i >= 0; i--)
            {
                NavObstaclePrimitive obstacle = _persistentDynamicObstacles[i];
                if (!IsValidDynamicObstacleBounds(obstacle.Center, obstacle.Extents))
                {
                    _persistentDynamicObstacles.RemoveAtSwapBack(i);
                    continue;
                }

                if (math.lengthsq(obstacle.Center - center) > removeRadiusSq)
                    continue;

                _persistentDynamicObstacles.RemoveAtSwapBack(i);
            }
        }

        private static void WriteMacroFloraObstacles(
            NativeArray<Matrix4x4> matrices,
            NativeArray<HectonVegetationInstanceData> metadata,
            NativeArray<int> types,
            NativeArray<int> semanticTypes,
            int count,
            ref NativeArray<NavObstaclePrimitive> snapshot,
            ref int writeIndex)
        {
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated ||
                !semanticTypes.IsCreated ||
                !snapshot.IsCreated ||
                count <= 0)
            {
                return;
            }

            int remainingCapacity = snapshot.Length - writeIndex;
            if (remainingCapacity <= 0)
                return;

            int safeCount = math.min(
                count,
                math.min(
                    remainingCapacity,
                    math.min(
                        matrices.Length,
                        math.min(metadata.Length, math.min(types.Length, semanticTypes.Length)))));
            for (int i = 0; i < safeCount; i++)
            {
                if (!TryResolveMacroFloraObstacleWorldBounds(matrices[i], metadata[i], types[i], semanticTypes[i], out float3 center, out float3 extents))
                    continue;

                snapshot[writeIndex] = new NavObstaclePrimitive
                {
                    Center = center,
                    Extents = extents
                };
                writeIndex++;
            }
        }

        private static bool TryResolveMacroFloraObstacle(
            HectonVegetationInstanceData metadata,
            int typeId,
            int semanticType,
            out float3 centerOffset,
            out float3 extents)
        {
            centerOffset = float3.zero;
            extents = float3.zero;

            byte runtimeFlags = HectonVegetationRuntimeFlagEncoding.ExtractPackedFlags(metadata.RuntimeFlags);
            if ((runtimeFlags & FloraRuntimeFlagDead) != 0 ||
                metadata.RuntimeState >= HectonVegetationInstanceData.RuntimeStateDying - 0.01f ||
                metadata.HeightScale < 0f ||
                metadata.WidthScale < 0f)
            {
                return false;
            }

            HectonVegetationInstanceType vegetationType = (HectonVegetationInstanceType)typeId;
            HectonMapMagicVegetationBridge.VegetationSemanticType semantic = (HectonMapMagicVegetationBridge.VegetationSemanticType)semanticType;
            if (vegetationType == HectonVegetationInstanceType.GiantKelp ||
                semantic == HectonMapMagicVegetationBridge.VegetationSemanticType.OrganicKelp)
            {
                float height = math.lerp(10f, 20f, math.saturate(metadata.HeightScale));
                float radius = math.lerp(
                    MinimumKelpObstacleRadiusMeters,
                    MaximumKelpObstacleRadiusMeters,
                    math.saturate(math.abs(metadata.WidthScale)));
                centerOffset = new float3(0f, height * 0.5f, 0f);
                extents = new float3(radius, height * 0.5f, radius);
                return true;
            }

            if (vegetationType == HectonVegetationInstanceType.Sargassum ||
                semantic == HectonMapMagicVegetationBridge.VegetationSemanticType.FloatingSargassum)
            {
                float radius = math.lerp(
                    MinimumSargassumObstacleRadiusMeters,
                    MaximumSargassumObstacleRadiusMeters,
                    math.saturate(math.abs(metadata.WidthScale)));
                float halfHeight = math.lerp(
                    MinimumSargassumObstacleHalfHeightMeters,
                    MaximumSargassumObstacleHalfHeightMeters,
                    math.saturate(metadata.HeightScale));
                extents = new float3(radius, halfHeight, radius);
                return true;
            }

            if (HectonMapMagicVegetationBridge.IsColonyCoralSemanticType(semantic))
            {
                float semanticRadiusScale = semantic == HectonMapMagicVegetationBridge.VegetationSemanticType.ColonyCable
                    ? 0.58f
                    : (semantic == HectonMapMagicVegetationBridge.VegetationSemanticType.ColonySupportBeam ? 0.92f : 1f);
                float semanticHeightScale = semantic == HectonMapMagicVegetationBridge.VegetationSemanticType.ColonyCable
                    ? 0.78f
                    : (semantic == HectonMapMagicVegetationBridge.VegetationSemanticType.ColonySupportBeam ? 1.18f : 1f);
                float radius = math.max(
                    MinimumCoralObstacleRadiusMeters,
                    math.lerp(
                        MinimumCoralObstacleRadiusMeters,
                        MaximumCoralObstacleRadiusMeters,
                        math.saturate(math.abs(metadata.WidthScale))) * semanticRadiusScale);
                float halfHeight = math.max(
                    MinimumCoralObstacleHalfHeightMeters,
                    math.lerp(
                        MinimumCoralObstacleHalfHeightMeters,
                        MaximumCoralObstacleHalfHeightMeters,
                        math.saturate(math.abs(metadata.HeightScale))) * semanticHeightScale);
                centerOffset = new float3(0f, halfHeight, 0f);
                extents = new float3(radius, halfHeight, radius);
                return true;
            }

            return false;
        }

        private static bool TryResolveDynamicUpdateRegion(
            VolumeRecord record,
            DynamicObstacleClearRequest request,
            out int3 regionMin,
            out int3 regionMax)
        {
            regionMin = new int3(int.MaxValue, int.MaxValue, int.MaxValue);
            regionMax = new int3(int.MinValue, int.MinValue, int.MinValue);
            if (!HasValidRecordBounds(record) ||
                !math.all(math.isfinite(request.Center)) ||
                !math.all(math.isfinite(request.Extents)) ||
                request.Extents.x <= 0.0001f ||
                request.Extents.y <= 0.0001f ||
                request.Extents.z <= 0.0001f)
            {
                return false;
            }

            float invCellSize = math.rcp(record.CellSize);
            int chunkCells = math.max(1, (int)math.ceil(DynamicObstacleChunkSizeMeters * invCellSize));
            int clearanceCells = ResolveClearanceRadiusCells(record.CellSize);
            float3 requestMinWorld = request.Center - request.Extents;
            float3 requestMaxWorld = request.Center + request.Extents;
            if (!math.all(math.isfinite(requestMinWorld)) ||
                !math.all(math.isfinite(requestMaxWorld)))
            {
                return false;
            }

            if (!BoundsOverlapRecord(record, requestMinWorld, requestMaxWorld))
                return false;

            int3 requestMinVoxel = WorldToVoxel(record, requestMinWorld);
            int3 requestMaxVoxel = WorldToVoxel(record, requestMaxWorld);
            int3 chunkMin = new int3(
                (requestMinVoxel.x / chunkCells) * chunkCells,
                (requestMinVoxel.y / chunkCells) * chunkCells,
                (requestMinVoxel.z / chunkCells) * chunkCells);
            int3 chunkMax = new int3(
                (((requestMaxVoxel.x / chunkCells) + 1) * chunkCells) - 1,
                (((requestMaxVoxel.y / chunkCells) + 1) * chunkCells) - 1,
                (((requestMaxVoxel.z / chunkCells) + 1) * chunkCells) - 1);

            regionMin = math.max(int3.zero, chunkMin - clearanceCells);
            regionMax = math.min(record.Dimensions - 1, chunkMax + clearanceCells);
            return math.all(regionMin <= regionMax);
        }

        private static int3 WorldToVoxel(VolumeRecord record, float3 worldPosition)
        {
            float invCellSize = math.rcp(record.CellSize);
            float3 local = (worldPosition - record.Origin) * invCellSize;
            return new int3(
                math.clamp((int)math.floor(local.x), 0, math.max(0, record.Dimensions.x - 1)),
                math.clamp((int)math.floor(local.y), 0, math.max(0, record.Dimensions.y - 1)),
                math.clamp((int)math.floor(local.z), 0, math.max(0, record.Dimensions.z - 1)));
        }

        private static bool BoundsOverlapRecord(VolumeRecord record, float3 min, float3 max)
        {
            return max.x >= record.Origin.x &&
                   max.y >= record.Origin.y &&
                   max.z >= record.Origin.z &&
                   min.x <= record.Max.x &&
                   min.y <= record.Max.y &&
                   min.z <= record.Max.z;
        }

        private static bool HasValidRecordBounds(VolumeRecord record)
        {
            if (record == null ||
                !record.Current.IsCreated ||
                record.Dimensions.x <= 0 ||
                record.Dimensions.y <= 0 ||
                record.Dimensions.z <= 0 ||
                record.CellSize <= 0f ||
                !math.isfinite(record.CellSize) ||
                !math.all(math.isfinite(record.Origin)) ||
                !math.all(math.isfinite(record.Max)) ||
                !math.all(record.Max >= record.Origin))
            {
                return false;
            }

            return HasCompleteVoxelCellCoverage(record.Dimensions, record.Current.Length);
        }

        private static bool HasCompleteDynamicUpdateBuffers(VolumeRecord record)
        {
            if (!HasValidRecordBounds(record) ||
                !record.Next.IsCreated ||
                !record.BaseCurrent.IsCreated ||
                !record.CurrentDistance.IsCreated ||
                !record.NextDistance.IsCreated ||
                !record.PureVoidBlockFlags.IsCreated)
            {
                return false;
            }

            if (!TryResolveVoxelCellCount(record.Dimensions, out int requiredCellCount))
                return false;

            if (record.Next.Length < requiredCellCount ||
                record.BaseCurrent.Length < requiredCellCount ||
                record.CurrentDistance.Length < requiredCellCount ||
                record.NextDistance.Length < requiredCellCount)
            {
                return false;
            }

            int requiredBlockCount = ResolvePureVoidBlockCount(requiredCellCount);
            return requiredBlockCount > 0 &&
                   record.PureVoidBlockFlags.Length >= requiredBlockCount;
        }

        private static bool HasCompleteVoxelCellCoverage(int3 dimensions, int availableCellCount)
        {
            return TryResolveVoxelCellCount(dimensions, out int expectedCellCount) &&
                   availableCellCount >= expectedCellCount;
        }

        private static bool TryResolveVoxelCellCount(int3 dimensions, out int expectedCellCount)
        {
            expectedCellCount = 0;
            if (dimensions.x <= 0 ||
                dimensions.y <= 0 ||
                dimensions.z <= 0)
            {
                return false;
            }

            long xyCount = (long)dimensions.x * dimensions.y;
            if (xyCount <= 0L || xyCount > int.MaxValue)
                return false;

            long expectedCellCountLong = xyCount * dimensions.z;
            if (expectedCellCountLong <= 0L || expectedCellCountLong > int.MaxValue)
                return false;

            expectedCellCount = (int)expectedCellCountLong;
            return true;
        }

        private static bool IsValidPortalNode(in PortalNode node)
        {
            return node.Face < FaceCount &&
                   node.Radius > 0f &&
                   math.isfinite(node.Radius) &&
                   math.all(math.isfinite(node.Centroid));
        }

        private static bool CanEmitPortalRoutePath(int outputCapacity)
        {
            int routeNodeCount = _routePathScratch.Count;
            if (routeNodeCount <= 0 ||
                routeNodeCount > MaxPortalGraphNodeCapacity ||
                outputCapacity < 2 ||
                routeNodeCount + 2 > outputCapacity)
            {
                return false;
            }

            for (int routeIndex = 0; routeIndex < routeNodeCount; routeIndex++)
            {
                int nodeIndex = _routePathScratch[routeIndex];
                if (nodeIndex < 0 || nodeIndex >= _portalGraphNodes.Count)
                    return false;

                PortalNode node = _portalGraphNodes[nodeIndex];
                if (!IsValidPortalNode(in node))
                    return false;
            }

            return true;
        }

        private static bool ContainsPoint(VolumeRecord record, float3 worldPosition)
        {
            return worldPosition.x >= record.Origin.x &&
                   worldPosition.y >= record.Origin.y &&
                   worldPosition.z >= record.Origin.z &&
                   worldPosition.x <= record.Max.x &&
                   worldPosition.y <= record.Max.y &&
                   worldPosition.z <= record.Max.z;
        }

        private static float DistanceSqToBounds(VolumeRecord record, float3 worldPosition)
        {
            float3 clamped = math.clamp(worldPosition, record.Origin, record.Max);
            return math.lengthsq(worldPosition - clamped);
        }

        private static float EstimateLength3D(float3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float max = math.max(ax, math.max(ay, az));
            float min = math.min(ax, math.min(ay, az));
            float mid = ax + ay + az - max - min;
            return max + (mid * 0.375f) + (min * 0.25f);
        }

        private static void EnsurePortalGraphBuilt()
        {
            if (!_portalGraphDirty)
                return;

            _portalGraphNodes.Clear();
            Dictionary<int, VolumeRecord>.Enumerator enumerator = _records.GetEnumerator();
            while (enumerator.MoveNext() && _portalGraphNodes.Count < MaxPortalGraphNodeCapacity)
            {
                VolumeRecord record = enumerator.Current.Value;
                if (!HasValidRecordBounds(record) ||
                    !record.PortalsReady ||
                    record.PortalCount <= 0 ||
                    record.Portals == null ||
                    record.Portals.Length <= 0)
                {
                    continue;
                }

                int safePortalCount = math.min(record.PortalCount, record.Portals.Length);
                for (int portalIndex = 0; portalIndex < safePortalCount && _portalGraphNodes.Count < MaxPortalGraphNodeCapacity; portalIndex++)
                {
                    PortalNode portal = record.Portals[portalIndex];
                    if (!IsValidPortalNode(in portal))
                        continue;

                    portal.ConnectedPortalIndex = InvalidPortalIndex;
                    record.Portals[portalIndex] = portal;
                    _portalGraphNodes.Add(portal);
                }
            }

            for (int portalIndex = 0; portalIndex < _portalGraphNodes.Count; portalIndex++)
            {
                PortalNode current = _portalGraphNodes[portalIndex];
                if (!IsValidPortalNode(in current))
                    continue;

                int bestMatchIndex = InvalidPortalIndex;
                float bestMatchScore = float.MaxValue;
                for (int candidateIndex = 0; candidateIndex < _portalGraphNodes.Count; candidateIndex++)
                {
                    if (candidateIndex == portalIndex)
                        continue;

                    PortalNode candidate = _portalGraphNodes[candidateIndex];
                    if (!IsValidPortalNode(in candidate) ||
                        candidate.ChunkId == current.ChunkId ||
                        !AreOppositeFaces(current.Face, candidate.Face))
                        continue;

                    float centroidDistanceSq = math.lengthsq(current.Centroid - candidate.Centroid);
                    float maxJoinDistance = math.max(current.Radius + candidate.Radius + BoundsMatchEpsilon, BoundsMatchEpsilon);
                    if (!math.isfinite(centroidDistanceSq) ||
                        !math.isfinite(maxJoinDistance) ||
                        centroidDistanceSq > maxJoinDistance * maxJoinDistance)
                        continue;

                    if (centroidDistanceSq < bestMatchScore)
                    {
                        bestMatchScore = centroidDistanceSq;
                        bestMatchIndex = candidateIndex;
                    }
                }

                if (bestMatchIndex >= 0)
                {
                    current.ConnectedPortalIndex = bestMatchIndex;
                    _portalGraphNodes[portalIndex] = current;
                }
            }

            _portalGraphDirty = false;
        }

        private static bool TryResolveRecord(float3 worldPosition, out VolumeRecord record)
        {
            record = null;
            if (!math.all(math.isfinite(worldPosition)))
                return false;

            float nearestDistanceSq = float.MaxValue;
            Dictionary<int, VolumeRecord>.Enumerator enumerator = _records.GetEnumerator();
            while (enumerator.MoveNext())
            {
                VolumeRecord candidate = enumerator.Current.Value;
                if (!HasValidRecordBounds(candidate))
                    continue;

                if (ContainsPoint(candidate, worldPosition))
                {
                    record = candidate;
                    return true;
                }

                float distanceSq = DistanceSqToBounds(candidate, worldPosition);
                if (distanceSq < nearestDistanceSq)
                {
                    nearestDistanceSq = distanceSq;
                    record = candidate;
                }
            }

            return HasValidRecordBounds(record) &&
                   nearestDistanceSq <= math.max(record.CellSize * record.CellSize, 1f);
        }

        private static bool TryResolveContainingRecord(float3 worldPosition, out VolumeRecord record)
        {
            record = null;
            if (!math.all(math.isfinite(worldPosition)))
                return false;

            Dictionary<int, VolumeRecord>.Enumerator enumerator = _records.GetEnumerator();
            while (enumerator.MoveNext())
            {
                VolumeRecord candidate = enumerator.Current.Value;
                if (!HasValidRecordBounds(candidate) ||
                    !ContainsPoint(candidate, worldPosition))
                {
                    continue;
                }

                record = candidate;
                return true;
            }

            return false;
        }

        private static void RebuildPortals(VolumeRecord record)
        {
            if (record == null)
                return;

            if (!HasValidRecordBounds(record) ||
                record.Dimensions.x <= 1 ||
                record.Dimensions.y <= 1 ||
                record.Dimensions.z <= 1)
            {
                record.PortalCount = 0;
                return;
            }

            if (!EnsurePortalWorkCapacity(record))
            {
                record.PortalCount = 0;
                return;
            }

            record.PortalCount = 0;

            for (byte face = 0; face < FaceCount; face++)
            {
                GetFaceDimensions(record.Dimensions, face, out int width, out int height);
                int faceCellCount = width * height;
                if (faceCellCount <= 0)
                    continue;

                record.FaceVisitStamp++;
                if (record.FaceVisitStamp == int.MaxValue)
                {
                    System.Array.Clear(record.FaceVisitScratch, 0, record.FaceVisitScratch.Length);
                    record.FaceVisitStamp = 1;
                }

                for (int faceIndex = 0; faceIndex < faceCellCount; faceIndex++)
                {
                    if (record.FaceVisitScratch[faceIndex] == record.FaceVisitStamp ||
                        !IsFaceCellOpen(record, face, faceIndex, width))
                    {
                        continue;
                    }

                    PortalNode portal = ExtractFacePortal(record, face, faceIndex, width, height);
                    if (!IsValidPortalNode(in portal))
                        continue;

                    if (record.PortalCount >= record.Portals.Length)
                        return;

                    record.Portals[record.PortalCount] = portal;
                    record.PortalCount++;
                }
            }
        }

        private static PortalNode ExtractFacePortal(VolumeRecord record, byte face, int seedFaceIndex, int width, int height)
        {
            int queueHead = 0;
            int queueTail = 0;
            record.FaceQueueScratch[queueTail++] = seedFaceIndex;
            record.FaceVisitScratch[seedFaceIndex] = record.FaceVisitStamp;

            float3 sum = float3.zero;
            int cellCount = 0;
            int minU = int.MaxValue;
            int minV = int.MaxValue;
            int maxU = int.MinValue;
            int maxV = int.MinValue;
            while (queueHead < queueTail)
            {
                int faceIndex = record.FaceQueueScratch[queueHead++];
                int u = faceIndex % width;
                int v = faceIndex / width;
                int3 voxel = ResolveFaceVoxel(record.Dimensions, face, u, v);
                sum += record.Origin + (new float3(voxel.x, voxel.y, voxel.z) * record.CellSize);
                cellCount++;
                minU = math.min(minU, u);
                minV = math.min(minV, v);
                maxU = math.max(maxU, u);
                maxV = math.max(maxV, v);

                QueueFaceNeighbor(record, face, u - 1, v, width, height, ref queueTail);
                QueueFaceNeighbor(record, face, u + 1, v, width, height, ref queueTail);
                QueueFaceNeighbor(record, face, u, v - 1, width, height, ref queueTail);
                QueueFaceNeighbor(record, face, u, v + 1, width, height, ref queueTail);
            }

            if (cellCount <= 0)
                return default;

            float faceSpanU = (maxU - minU + 1) * record.CellSize;
            float faceSpanV = (maxV - minV + 1) * record.CellSize;
            float3 centroid = sum * math.rcp((float)cellCount);
            float radius = math.max(record.CellSize * 0.5f, math.max(faceSpanU, faceSpanV) * 0.5f);
            if (!math.all(math.isfinite(centroid)) ||
                !math.isfinite(radius) ||
                radius <= 0f)
            {
                return default;
            }

            return new PortalNode
            {
                ChunkId = record.ChunkId,
                Centroid = centroid,
                Radius = radius,
                ConnectedPortalIndex = InvalidPortalIndex,
                Face = face
            };
        }

        private static void QueueFaceNeighbor(VolumeRecord record, byte face, int u, int v, int width, int height, ref int queueTail)
        {
            if (u < 0 || v < 0 || u >= width || v >= height)
                return;

            int faceIndex = u + (v * width);
            if (record.FaceVisitScratch[faceIndex] == record.FaceVisitStamp ||
                !IsFaceCellOpen(record, face, faceIndex, width))
            {
                return;
            }

            if (queueTail >= record.FaceQueueScratch.Length)
                return;

            record.FaceVisitScratch[faceIndex] = record.FaceVisitStamp;
            record.FaceQueueScratch[queueTail++] = faceIndex;
        }

        private static bool TrySolvePortalRoute(VolumeRecord startRecord, VolumeRecord endRecord, float3 startWorldPosition, float3 endWorldPosition)
        {
            int nodeCount = _portalGraphNodes.Count;
            if (nodeCount <= 0 ||
                !HasValidRecordBounds(startRecord) ||
                !HasValidRecordBounds(endRecord) ||
                !math.all(math.isfinite(startWorldPosition)) ||
                !math.all(math.isfinite(endWorldPosition)) ||
                !EnsureRouteNodeCapacity(nodeCount))
            {
                return false;
            }

            _routeOpenSetScratch.Clear();
            _routePathScratch.Clear();
            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                RouteNodeState state = _routeNodeScratch[nodeIndex];
                state.GScore = float.MaxValue;
                state.FScore = float.MaxValue;
                state.ParentIndex = InvalidPortalIndex;
                state.Flags = 0;
                _routeNodeScratch[nodeIndex] = state;
            }

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                PortalNode node = _portalGraphNodes[nodeIndex];
                if (node.ChunkId != startRecord.ChunkId ||
                    !IsValidPortalNode(in node))
                    continue;

                RouteNodeState state = _routeNodeScratch[nodeIndex];
                state.GScore = EstimateLength3D(startWorldPosition - node.Centroid);
                state.FScore = state.GScore + EstimateLength3D(node.Centroid - endWorldPosition);
                if (!math.isfinite(state.GScore) || !math.isfinite(state.FScore))
                    continue;

                state.ParentIndex = InvalidPortalIndex;
                state.Flags = 1;
                _routeNodeScratch[nodeIndex] = state;
                if (_routeOpenSetScratch.Count >= _routeOpenSetScratch.Capacity)
                    return false;

                _routeOpenSetScratch.Add(nodeIndex);
            }

            while (_routeOpenSetScratch.Count > 0)
            {
                int currentNodeIndex = PopLowestCostOpenNode();
                if (currentNodeIndex < 0 || currentNodeIndex >= _portalGraphNodes.Count)
                    return false;

                RouteNodeState currentState = _routeNodeScratch[currentNodeIndex];
                if (!math.isfinite(currentState.GScore))
                    return false;

                currentState.Flags = 2;
                _routeNodeScratch[currentNodeIndex] = currentState;

                PortalNode currentNode = _portalGraphNodes[currentNodeIndex];
                if (currentNode.ChunkId == endRecord.ChunkId)
                {
                    return ReconstructRoute(currentNodeIndex);
                }

                RelaxPortalNeighbors(currentNodeIndex, currentState.GScore, endWorldPosition);
            }

            return false;
        }

        private static void RelaxPortalNeighbors(int currentNodeIndex, float currentGScore, float3 endWorldPosition)
        {
            if (currentNodeIndex < 0 ||
                currentNodeIndex >= _portalGraphNodes.Count ||
                !math.isfinite(currentGScore))
            {
                return;
            }

            PortalNode currentNode = _portalGraphNodes[currentNodeIndex];
            if (!IsValidPortalNode(in currentNode))
                return;

            if (currentNode.ConnectedPortalIndex >= 0)
                RelaxPortalEdge(currentNodeIndex, currentNode.ConnectedPortalIndex, currentGScore, endWorldPosition);

            for (int candidateIndex = 0; candidateIndex < _portalGraphNodes.Count; candidateIndex++)
            {
                if (candidateIndex == currentNodeIndex)
                    continue;

                PortalNode candidate = _portalGraphNodes[candidateIndex];
                if (candidate.ChunkId != currentNode.ChunkId)
                    continue;

                RelaxPortalEdge(currentNodeIndex, candidateIndex, currentGScore, endWorldPosition);
            }
        }

        private static void RelaxPortalEdge(int currentNodeIndex, int candidateIndex, float currentGScore, float3 endWorldPosition)
        {
            if (currentNodeIndex < 0 ||
                candidateIndex < 0 ||
                currentNodeIndex >= _portalGraphNodes.Count ||
                candidateIndex >= _portalGraphNodes.Count ||
                !math.isfinite(currentGScore))
            {
                return;
            }

            PortalNode currentNode = _portalGraphNodes[currentNodeIndex];
            PortalNode candidateNode = _portalGraphNodes[candidateIndex];
            if (!IsValidPortalNode(in currentNode) ||
                !IsValidPortalNode(in candidateNode))
            {
                return;
            }

            RouteNodeState candidateState = _routeNodeScratch[candidateIndex];
            if ((candidateState.Flags & 2) != 0)
                return;

            float edgeCost = EstimateLength3D(currentNode.Centroid - candidateNode.Centroid);
            float tentativeG = currentGScore + edgeCost;
            if (!math.isfinite(edgeCost) ||
                !math.isfinite(tentativeG) ||
                tentativeG >= candidateState.GScore)
                return;

            candidateState.GScore = tentativeG;
            candidateState.FScore = tentativeG + EstimateLength3D(candidateNode.Centroid - endWorldPosition);
            if (!math.isfinite(candidateState.FScore))
                return;

            candidateState.ParentIndex = currentNodeIndex;
            if ((candidateState.Flags & 1) == 0)
            {
                if (_routeOpenSetScratch.Count >= _routeOpenSetScratch.Capacity)
                    return;

                candidateState.Flags |= 1;
                _routeOpenSetScratch.Add(candidateIndex);
            }

            _routeNodeScratch[candidateIndex] = candidateState;
        }

        private static int PopLowestCostOpenNode()
        {
            int bestListIndex = InvalidPortalIndex;
            float bestScore = float.MaxValue;
            for (int listIndex = 0; listIndex < _routeOpenSetScratch.Count; listIndex++)
            {
                int nodeIndex = _routeOpenSetScratch[listIndex];
                if (nodeIndex < 0 || nodeIndex >= _routeNodeScratch.Count)
                    continue;

                float score = _routeNodeScratch[nodeIndex].FScore;
                if (math.isfinite(score) && score < bestScore)
                {
                    bestScore = score;
                    bestListIndex = listIndex;
                }
            }

            if (bestListIndex < 0 || !math.isfinite(bestScore))
            {
                _routeOpenSetScratch.Clear();
                return InvalidPortalIndex;
            }

            int selectedNodeIndex = _routeOpenSetScratch[bestListIndex];
            int lastListIndex = _routeOpenSetScratch.Count - 1;
            _routeOpenSetScratch[bestListIndex] = _routeOpenSetScratch[lastListIndex];
            _routeOpenSetScratch.RemoveAt(lastListIndex);
            return selectedNodeIndex;
        }

        private static bool ReconstructRoute(int endNodeIndex)
        {
            _routePathScratch.Clear();
            int currentIndex = endNodeIndex;
            int iterationCount = 0;
            while (currentIndex >= 0 &&
                   currentIndex < _routeNodeScratch.Count &&
                   _routePathScratch.Count < _routePathScratch.Capacity &&
                   iterationCount < MaxPortalGraphNodeCapacity)
            {
                if (currentIndex >= _portalGraphNodes.Count)
                {
                    _routePathScratch.Clear();
                    return false;
                }

                PortalNode node = _portalGraphNodes[currentIndex];
                if (!IsValidPortalNode(in node))
                {
                    _routePathScratch.Clear();
                    return false;
                }

                _routePathScratch.Add(currentIndex);
                currentIndex = _routeNodeScratch[currentIndex].ParentIndex;
                iterationCount++;
            }

            if (currentIndex >= 0 || _routePathScratch.Count <= 0)
            {
                _routePathScratch.Clear();
                return false;
            }

            return true;
        }

        private static bool IsFaceCellOpen(VolumeRecord record, byte face, int faceIndex, int width)
        {
            int u = faceIndex % width;
            int v = faceIndex / width;
            int3 voxel = ResolveFaceVoxel(record.Dimensions, face, u, v);
            int flatIndex = voxel.x + (voxel.y * record.Dimensions.x) + (voxel.z * record.Dimensions.x * record.Dimensions.y);
            return flatIndex >= 0 &&
                   flatIndex < record.Current.Length &&
                   record.Current[flatIndex] == OpenCell;
        }

        private static bool TrySamplePassabilityCell(VolumeRecord record, float3 worldPosition, out int3 voxel, out byte passability)
        {
            voxel = int3.zero;
            passability = SolidCell;
            if (!HasValidRecordBounds(record) ||
                !math.all(math.isfinite(worldPosition)))
            {
                return false;
            }

            float invCellSize = math.rcp(record.CellSize);
            float3 local = (worldPosition - record.Origin) * invCellSize;
            int3 candidate = new int3(
                math.clamp((int)math.floor(local.x), 0, math.max(0, record.Dimensions.x - 1)),
                math.clamp((int)math.floor(local.y), 0, math.max(0, record.Dimensions.y - 1)),
                math.clamp((int)math.floor(local.z), 0, math.max(0, record.Dimensions.z - 1)));
            int flatIndex = candidate.x +
                            (candidate.y * record.Dimensions.x) +
                            (candidate.z * record.Dimensions.x * record.Dimensions.y);
            if (flatIndex < 0 || flatIndex >= record.Current.Length)
                return false;

            voxel = candidate;
            passability = record.Current[flatIndex];
            return true;
        }

        private static int3 ResolveFaceVoxel(int3 dimensions, byte face, int u, int v)
        {
            switch (face)
            {
                case 0: return new int3(0, u, v);
                case 1: return new int3(math.max(0, dimensions.x - 1), u, v);
                case 2: return new int3(u, 0, v);
                case 3: return new int3(u, math.max(0, dimensions.y - 1), v);
                case 4: return new int3(u, v, 0);
                default: return new int3(u, v, math.max(0, dimensions.z - 1));
            }
        }

        private static void GetFaceDimensions(int3 dimensions, byte face, out int width, out int height)
        {
            switch (face)
            {
                case 0:
                case 1:
                    width = dimensions.y;
                    height = dimensions.z;
                    return;
                case 2:
                case 3:
                    width = dimensions.x;
                    height = dimensions.z;
                    return;
                default:
                    width = dimensions.x;
                    height = dimensions.y;
                    return;
            }
        }

        private static bool AreOppositeFaces(byte a, byte b)
        {
            return (a == 0 && b == 1) ||
                   (a == 1 && b == 0) ||
                   (a == 2 && b == 3) ||
                   (a == 3 && b == 2) ||
                   (a == 4 && b == 5) ||
                   (a == 5 && b == 4);
        }

        private static uint ComputeChunkId(float3 origin, float cellSize, int3 dimensions)
        {
            if (!math.all(math.isfinite(origin)) ||
                cellSize <= 0f ||
                !math.isfinite(cellSize) ||
                dimensions.x <= 0 ||
                dimensions.y <= 0 ||
                dimensions.z <= 0)
            {
                return 0u;
            }

            float chunkSpan = cellSize * math.max(1, math.max(dimensions.x - 1, math.max(dimensions.y - 1, dimensions.z - 1)));
            if (chunkSpan <= 0f || !math.isfinite(chunkSpan))
                return 0u;

            float invChunkSpan = math.rcp(chunkSpan);
            int chunkX = math.clamp((int)math.floor(origin.x * invChunkSpan) + ChunkIdAxisBias, 0, 1023);
            int chunkY = math.clamp((int)math.floor(origin.y * invChunkSpan) + ChunkIdAxisBias, 0, 1023);
            int chunkZ = math.clamp((int)math.floor(origin.z * invChunkSpan) + ChunkIdAxisBias, 0, 1023);
            return Part1By2((uint)chunkX) | (Part1By2((uint)chunkY) << 1) | (Part1By2((uint)chunkZ) << 2);
        }

        private static uint Part1By2(uint value)
        {
            uint compact = value & 0x000003ffu;
            compact = (compact | (compact << 16)) & 0x030000FFu;
            compact = (compact | (compact << 8)) & 0x0300F00Fu;
            compact = (compact | (compact << 4)) & 0x030C30C3u;
            compact = (compact | (compact << 2)) & 0x09249249u;
            return compact;
        }

        private static bool EnsurePortalWorkCapacity(VolumeRecord record)
        {
            if (record == null ||
                record.Dimensions.x <= 1 ||
                record.Dimensions.y <= 1 ||
                record.Dimensions.z <= 1 ||
                record.FaceVisitScratch == null ||
                record.FaceQueueScratch == null ||
                record.Portals == null)
            {
                return false;
            }

            if (!TryResolveMaxFaceCells(record.Dimensions, out int maxFaceCells))
                return false;

            return maxFaceCells > 0 &&
                   maxFaceCells <= record.FaceVisitScratch.Length &&
                   maxFaceCells <= record.FaceQueueScratch.Length &&
                   record.Portals.Length > 0;
        }

        private static bool TryResolveMaxFaceCells(int3 dimensions, out int maxFaceCells)
        {
            maxFaceCells = 0;
            if (dimensions.x <= 0 ||
                dimensions.y <= 0 ||
                dimensions.z <= 0)
            {
                return false;
            }

            long xy = (long)dimensions.x * dimensions.y;
            long xz = (long)dimensions.x * dimensions.z;
            long yz = (long)dimensions.y * dimensions.z;
            long maxFaceCellCount = xy;
            if (xz > maxFaceCellCount)
                maxFaceCellCount = xz;
            if (yz > maxFaceCellCount)
                maxFaceCellCount = yz;

            if (maxFaceCellCount <= 0L || maxFaceCellCount > int.MaxValue)
                return false;

            maxFaceCells = (int)maxFaceCellCount;
            return true;
        }

#if UNITY_EDITOR
        internal static void DrawEditorOpenCellGizmos(Vector3 playerRuntimePosition, float radiusMeters)
        {
            if (radiusMeters <= 0f || _records.Count <= 0)
                return;

            float radiusSq = radiusMeters * radiusMeters;
            float3 playerPosition = new float3(playerRuntimePosition.x, playerRuntimePosition.y, playerRuntimePosition.z);
            Color previousColor = Gizmos.color;
            Gizmos.color = new Color(0.1f, 0.65f, 0.95f, 0.45f);

            int drawnCells = 0;
            Dictionary<int, VolumeRecord>.Enumerator enumerator = _records.GetEnumerator();
            while (enumerator.MoveNext() && drawnCells < 2048)
            {
                VolumeRecord record = enumerator.Current.Value;
                if (record == null ||
                    record.IsPureVoid ||
                    !record.Current.IsCreated ||
                    record.Dimensions.x <= 0 ||
                    record.Dimensions.y <= 0 ||
                    record.Dimensions.z <= 0)
                {
                    continue;
                }

                int pointCount = math.min(record.Current.Length, record.Dimensions.x * record.Dimensions.y * record.Dimensions.z);
                float cellSize = math.max(record.CellSize, 0.05f);
                Vector3 wireSize = Vector3.one * math.min(cellSize * 0.32f, 0.32f);
                int width = record.Dimensions.x;
                int slice = record.Dimensions.x * record.Dimensions.y;
                for (int flatIndex = 0; flatIndex < pointCount && drawnCells < 2048; flatIndex++)
                {
                    if (record.Current[flatIndex] != OpenCell)
                        continue;

                    int z = flatIndex / slice;
                    int remainder = flatIndex - z * slice;
                    int y = remainder / width;
                    int x = remainder - y * width;
                    float3 cellCenter = record.Origin + (new float3(x, y, z) + 0.5f) * cellSize;
                    if (math.lengthsq(cellCenter - playerPosition) > radiusSq)
                        continue;

                    Gizmos.DrawWireCube(new Vector3(cellCenter.x, cellCenter.y, cellCenter.z), wireSize);
                    drawnCells++;
                }
            }

            Gizmos.color = previousColor;
        }
#endif

        private static bool EnsureRouteNodeCapacity(int requiredCount)
        {
            if (requiredCount < 0 || requiredCount > _routeNodeScratch.Capacity)
                return false;

            while (_routeNodeScratch.Count < requiredCount)
                _routeNodeScratch.Add(default);

            return true;
        }
    }

}
