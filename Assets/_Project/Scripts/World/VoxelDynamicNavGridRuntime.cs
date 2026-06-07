using System.Runtime.InteropServices;
using Hecton8.Bootstrap;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.World
{
    internal static class VoxelDynamicNavGridRuntimeLayout
    {
        public const int DirtyVolumeRequestStrideBytes = 16;
        public const int DynamicObstacleClearRequestStrideBytes = 32;
        public const int PortalNodeStrideBytes = 32;
        public const int RouteNodeStateStrideBytes = 16;
        public const int NavObstaclePrimitiveStrideBytes = 32;
        public const int HybridNavigationSampleStrideBytes = 32;
    }

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
        private const int MaxObstacleSnapshotLeaseCount = 64;
        private const int MaxObstacleSnapshotPrimitiveCount = 1024;
        private const int MaxPortalFaceScratchCells = 4096;
        private const int MaxPortalGraphNodeCapacity = 4096;
        private const float PersistentObstacleMergeDistanceMeters = 2f;
        private const SystemID NavGridVaultOwner = SystemID.WorldStreaming;
        private const int NavGridVaultBufferBase = (int)BufferID.VoxelDynamicNavGridRecordBufferBase;
        private const int NavGridVaultBufferEnd = (int)BufferID.VoxelDynamicNavGridRecordBufferEnd;
        private const int NavGridVaultBufferStride = 6;
        private const int NavGridLaneCurrent = 0;
        private const int NavGridLaneNext = 1;
        private const int NavGridLaneBaseCurrent = 2;
        private const int NavGridLaneBaseNext = 3;
        private const int NavGridLaneCurrentDistance = 4;
        private const int NavGridLaneNextDistance = 5;
        private const int NavGridTelemetryFrameCount = 300;
        private const int NavGridTelemetryEntryStrideBytes = 64;
        private const ushort NavGridFailureVaultMissing = 1;
        private const ushort NavGridFailureCompactionFence = 2;
        private const ushort NavGridFailureInvalidBufferId = 3;
        private const ushort NavGridFailureHandleResolve = 4;
        private const ushort NavGridFailureWriteLock = 5;
        private const ushort NavGridFailureCapacity = 6;
        private const ushort NavGridFailureBudget = 7;
        private const ushort NavGridPhaseBuild = 1;
        private const ushort NavGridPhaseDynamicUpdate = 2;
        private const ushort NavGridPhaseVault = 3;
        private const ushort NavGridPhaseLifecycle = 4;
        private const uint NavGridFlagFailClosed = 1u << 0;
        private const uint NavGridFlagCompaction = 1u << 1;
        private const uint NavGridFlagContention = 1u << 2;
        private const uint NavGridFlagOverBudget = 1u << 3;
        private const ulong NavGridTelemetryStateHash = 0x313331365F4E4156UL;
        private const string DynamicClearanceBudgetWarningMessage = "[VoxelDynamicNavGridRuntime] Partial clearance dilation exceeded 1ms; next destroyed-flora clear uses reduced clearance radius.";

        // COLD ALLOC: fixed managed slots - capped voxel navgrid snapshots keyed by runtime volume instance ID - owner: VoxelDynamicNavGridRuntime
        private static readonly int[] _recordKeys = new int[MaxTrackedVolumeRecords];
        private static readonly VolumeRecord[] _records = CreateRecordPool();
        // COLD ALLOC: byte[512] - stable DataVault buffer-slot occupancy for voxel navgrid records - owner: VoxelDynamicNavGridRuntime
        private static readonly byte[] _recordBufferSlots = new byte[MaxTrackedVolumeRecords];
        // COLD ALLOC: fixed managed spill buffer while consuming queue entries - owner: VoxelDynamicNavGridRuntime
        private static readonly DirtyVolumeRequest[] _dirtyRequestSpill = new DirtyVolumeRequest[DirtyVolumeQueueCapacity];
        // COLD ALLOC: fixed managed delayed voxel nav rebuild markers for chthonic pillar volumes - owner: VoxelDynamicNavGridRuntime
        private static readonly DeferredDirtyVolumeRequest[] _deferredDirtyVolumes = new DeferredDirtyVolumeRequest[DeferredDirtyVolumeQueueCapacity];
        // COLD ALLOC: fixed managed macro portal graph nodes spanning active navgrid chunks - owner: VoxelDynamicNavGridRuntime
        private static readonly PortalNode[] _portalGraphNodes = new PortalNode[MaxPortalGraphNodeCapacity];
        // COLD ALLOC: fixed shared portal flood-fill scratch - one owner-phase graph rebuild at a time - owner: VoxelDynamicNavGridRuntime
        private static readonly int[] _portalFaceVisitScratch = new int[MaxPortalFaceScratchCells];
        private static readonly int[] _portalFaceQueueScratch = new int[MaxPortalFaceScratchCells];
        // COLD ALLOC: fixed managed portal A* node state scratch - owner: VoxelDynamicNavGridRuntime
        private static readonly RouteNodeState[] _routeNodeScratch = new RouteNodeState[MaxPortalGraphNodeCapacity];
        // COLD ALLOC: fixed managed portal A* open-set scratch - owner: VoxelDynamicNavGridRuntime
        private static readonly int[] _routeOpenSetScratch = new int[MaxPortalGraphNodeCapacity];
        // COLD ALLOC: fixed managed portal route reconstruction scratch - owner: VoxelDynamicNavGridRuntime
        private static readonly int[] _routePathScratch = new int[MaxPortalGraphNodeCapacity];
        // COLD ALLOC: fixed managed record-key removal scratch - owner: VoxelDynamicNavGridRuntime
        private static readonly int[] _recordRemovalScratch = new int[MaxTrackedVolumeRecords];
        // COLD ALLOC: fixed managed habitat obstacle collider sources - owner: VoxelDynamicNavGridRuntime
        private static readonly int[] _registeredObstacleKeys = new int[MaxRegisteredObstacleRecords];
        private static readonly ObstacleRegistration[] _registeredObstacles = new ObstacleRegistration[MaxRegisteredObstacleRecords];
        private static readonly ProfilerMarker _partialClearanceDilationScheduleMarker = new ProfilerMarker("H8/NavGrid/PartialClearanceDilationJob.Schedule");
        private static readonly ProfilerMarker _partialClearanceDilationCompleteMarker = new ProfilerMarker("H8/NavGrid/PartialClearanceDilationJob.Complete");
        private static readonly DirtyVolumeRequest[] _dirtyVolumes = new DirtyVolumeRequest[DirtyVolumeQueueCapacity];
        private static readonly DynamicObstacleClearRequest[] _pendingObstacleClears = new DynamicObstacleClearRequest[PendingObstacleClearQueueCapacity];
        private static readonly NavObstaclePrimitive[] _persistentDynamicObstacles = new NavObstaclePrimitive[MaxPersistentDynamicObstacleCount];
        private static readonly NativeArray<NavObstaclePrimitive>[] _obstacleSnapshotPool = new NativeArray<NavObstaclePrimitive>[MaxObstacleSnapshotLeaseCount];
        private static readonly byte[] _obstacleSnapshotLeaseStates = new byte[MaxObstacleSnapshotLeaseCount];
        private static VoxelDynamicNavGridRuntimeLifecycle _lifecycleOwner;
        private static IDataVault _dataVault;
        private static HectonMapMagicVegetationBridge _vegetationBridge;
        private static VaultGenerationHandle<NavGridTelemetryEntry> _navGridTelemetryRingHandle;
        private static VaultGenerationHandle<int> _navGridTelemetryCursorHandle;
        private static uint _navGridTelemetrySequence;
        private static bool _portalGraphDirty = true;
        private static bool _teardownPending;
        private static bool _clearRuntimeContainersWhenTeardownCompletes;
        private static int _dirtyVolumeQueueHead;
        private static int _dirtyVolumeQueueTail;
        private static int _pendingObstacleClearQueueHead;
        private static int _pendingObstacleClearQueueTail;
        private static int _persistentDynamicObstacleWriteCursor;
        private static int _persistentDynamicObstacleCount;
        private static int _dynamicClearanceFallbackSchedulesRemaining;
        private static int _dirtyVolumeQueueCount;
        private static int _pendingObstacleClearQueueCount;
        private static int _recordCount;
        private static int _dirtyRequestSpillCount;
        private static int _deferredDirtyVolumeCount;
        private static int _portalGraphNodeCount;
        private static int _portalFaceVisitStamp;
        private static int _routeNodeScratchCount;
        private static int _routeOpenSetCount;
        private static int _routePathCount;
        private static int _recordRemovalScratchCount;
        private static int _registeredObstacleCount;
        private static bool _obstacleSnapshotPoolReady;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static float _nextDynamicClearanceWarningTime = float.NegativeInfinity;
#endif

        internal enum HybridNavigationMode : byte
        {
            OpenWaterHeightmap = 0,
            CaveVoxel = 1,
            SolidVoxel = 2,
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct PassabilityBuildJob : Unity.Jobs.IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<float> DensityField;
            [WriteOnly, NoAlias] public NativeArray<byte> Output;
            public float SolidThreshold;

            public void Execute(int index)
            {
                Output[index] = DensityField[index] < SolidThreshold ? OpenCell : SolidCell;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct UpdateNavCellsJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<float> DensityField;
            [NoAlias] public NativeArray<byte> Passability;
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct PureVoidBlockScanJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<byte> Passability;
            [ReadOnly, NoAlias] public NativeArray<ushort> DistanceMap;
            [WriteOnly, NoAlias] public NativeArray<int> BlockFlags;
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct ClearanceDilationJob : Unity.Jobs.IJob
        {
            [NoAlias] public NativeArray<byte> Passability;
            [NoAlias] public NativeArray<ushort> DistanceMap;
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct ObstacleStampJob : Unity.Jobs.IJobParallelFor
        {
            [NoAlias] public NativeArray<byte> Passability;
            [ReadOnly, NoAlias] public NativeArray<NavObstaclePrimitive> Obstacles;
            public int ObstacleCount;
            public int3 Dimensions;
            public float3 Origin;
            public float CellSize;

            public void Execute(int index)
            {
                if (!Passability.IsCreated ||
                    !Obstacles.IsCreated ||
                    index < 0 ||
                    index >= Passability.Length)
                {
                    return;
                }

                int obstacleLimit = math.min(ObstacleCount, Obstacles.Length);
                if (obstacleLimit <= 0)
                    return;

                int slice = Dimensions.x * Dimensions.y;
                int z = index / slice;
                int y = (index - (z * slice)) / Dimensions.x;
                int x = index - (z * slice) - (y * Dimensions.x);
                float3 samplePoint = Origin + new float3(x * CellSize, y * CellSize, z * CellSize);

                for (int obstacleIndex = 0; obstacleIndex < obstacleLimit; obstacleIndex++)
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

                    Passability[index] = SolidCell;
                    return;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct CopyByteBufferJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<byte> Source;
            [WriteOnly, NoAlias] public NativeArray<byte> Destination;

            public void Execute(int index)
            {
                if (!Source.IsCreated || !Destination.IsCreated || index < 0 || index >= Source.Length || index >= Destination.Length)
                    return;

                Destination[index] = Source[index];
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct CopyUShortBufferJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<ushort> Source;
            [WriteOnly, NoAlias] public NativeArray<ushort> Destination;

            public void Execute(int index)
            {
                if (!Source.IsCreated || !Destination.IsCreated || index < 0 || index >= Source.Length || index >= Destination.Length)
                    return;

                Destination[index] = Source[index];
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct PartialObstacleResetAndStampJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<byte> BasePassability;
            [NoAlias] public NativeArray<byte> Passability;
            [ReadOnly, NoAlias] public NativeArray<NavObstaclePrimitive> Obstacles;
            public int ObstacleCount;
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
                int obstacleLimit = Obstacles.IsCreated
                    ? math.min(ObstacleCount, Obstacles.Length)
                    : 0;
                if (obstacleLimit > 0)
                {
                    float3 samplePoint = Origin + new float3(globalX * CellSize, globalY * CellSize, globalZ * CellSize);
                    for (int obstacleIndex = 0; obstacleIndex < obstacleLimit; obstacleIndex++)
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

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct PartialClearanceDilationJob : IJob
        {
            [NoAlias] public NativeArray<byte> Passability;
            [ReadOnly, NoAlias] public NativeArray<ushort> ReferenceDistanceMap;
            [NoAlias] public NativeArray<ushort> DistanceMap;
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

        [StructLayout(LayoutKind.Explicit, Size = VoxelDynamicNavGridRuntimeLayout.DirtyVolumeRequestStrideBytes)]
        private struct DirtyVolumeRequest
        {
            [FieldOffset(0)] public int VolumeInstanceId;
            [FieldOffset(4)] public int RuntimeStamp;
            [FieldOffset(8)] private ulong _pad0;
        }

        private struct DeferredDirtyVolumeRequest
        {
            public HectonVoxelVolume Volume;
            public int RuntimeStamp;
            public int RemainingSlowTicks;
        }

        [StructLayout(LayoutKind.Explicit, Size = VoxelDynamicNavGridRuntimeLayout.DynamicObstacleClearRequestStrideBytes)]
        private struct DynamicObstacleClearRequest
        {
            [FieldOffset(0)] public float3 Center;
            [FieldOffset(12)] public float3 Extents;
            [FieldOffset(24)] private ulong _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = VoxelDynamicNavGridRuntimeLayout.PortalNodeStrideBytes)]
        private struct PortalNode
        {
            [FieldOffset(0)] public uint ChunkId;
            [FieldOffset(4)] public float3 Centroid;
            [FieldOffset(16)] public float Radius;
            [FieldOffset(20)] public int ConnectedPortalIndex;
            [FieldOffset(24)] public byte Face;
            [FieldOffset(25)] private byte _pad0;
            [FieldOffset(26)] private ushort _pad1;
            [FieldOffset(28)] private uint _pad2;
        }

        [StructLayout(LayoutKind.Explicit, Size = VoxelDynamicNavGridRuntimeLayout.RouteNodeStateStrideBytes)]
        private struct RouteNodeState
        {
            [FieldOffset(0)] public float GScore;
            [FieldOffset(4)] public float FScore;
            [FieldOffset(8)] public int ParentIndex;
            [FieldOffset(12)] public byte Flags;
            [FieldOffset(13)] private byte _pad0;
            [FieldOffset(14)] private ushort _pad1;
        }

        [StructLayout(LayoutKind.Explicit, Size = VoxelDynamicNavGridRuntimeLayout.NavObstaclePrimitiveStrideBytes)]
        internal struct NavObstaclePrimitive
        {
            [FieldOffset(0)] public float3 Center;
            [FieldOffset(12)] public float3 Extents;
            [FieldOffset(24)] private ulong _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = VoxelDynamicNavGridRuntimeLayout.HybridNavigationSampleStrideBytes)]
        internal struct HybridNavigationSample
        {
            [FieldOffset(0)] public HybridNavigationMode Mode;
            [FieldOffset(1)] public byte Passability;
            [FieldOffset(2)] private ushort _pad0;
            [FieldOffset(4)] public float CellSize;
            [FieldOffset(8)] public float3 CellOrigin;
            [FieldOffset(20)] public float TerrainHeight;
            [FieldOffset(24)] public float FloorBoundaryY;
            [FieldOffset(28)] public byte HasTerrainHeight;
            [FieldOffset(29)] private byte _pad1;
            [FieldOffset(30)] private ushort _pad2;
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
            public int BufferSlot;
            public VaultGenerationHandle<byte> CurrentHandle;
            public VaultGenerationHandle<byte> NextHandle;
            public VaultGenerationHandle<byte> BaseCurrentHandle;
            public VaultGenerationHandle<byte> BaseNextHandle;
            public VaultGenerationHandle<ushort> CurrentDistanceHandle;
            public VaultGenerationHandle<ushort> NextDistanceHandle;
            public JobHandle PendingDynamicUpdateHandle;
            public VaultGenerationHandle<byte> PendingDynamicNextHandle;
            public VaultGenerationHandle<ushort> PendingDynamicNextDistanceHandle;
            public int PendingBuildRuntimeStamp;
            public int PendingBuildObstacleSnapshotLease;
            public int PendingBuildObstacleSnapshotCount;
            public int PendingDynamicObstacleSnapshotLease;
            public int PendingDynamicObstacleSnapshotCount;
            public ulong PendingBuildMutationGuardMask;
            public ulong PendingDynamicMutationGuardMask;
            public IDataVault PendingBuildMutationGuardVault;
            public IDataVault PendingDynamicMutationGuardVault;
            public long PendingDynamicScheduleTimestamp;
            public bool HasPendingDynamicUpdate;
            public bool PendingRemoval;
            public bool PortalsReady;
            public int3 PendingRegionMin;
            public int3 PendingRegionMax;
            public int PortalCount;

            public VolumeRecord()
            {
                ResetForReuse();
            }

            public bool TryDisposeCompleted()
            {
                if (HasPendingDynamicUpdate)
                {
                    if (!DispatcherJobSwap.TryComplete(ref PendingDynamicUpdateHandle, forceComplete: false))
                        return false;

                    VoxelDynamicNavGridRuntime.CompleteDynamicObstacleUpdate(this);
                }

                VoxelDynamicNavGridRuntime.ReleaseVoxelBuffers(this);
                VoxelDynamicNavGridRuntime.ReleaseRecordBufferSlot(BufferSlot);
                ResetForReuse();

                return true;
            }

            public void ResetForReuse()
            {
                BufferSlot = -1;
                ChunkId = 0u;
                CurrentHandle = default;
                NextHandle = default;
                BaseCurrentHandle = default;
                BaseNextHandle = default;
                CurrentDistanceHandle = default;
                NextDistanceHandle = default;
                PendingDynamicUpdateHandle = default;
                PendingDynamicNextHandle = default;
                PendingDynamicNextDistanceHandle = default;
                PendingBuildRuntimeStamp = 0;
                PendingBuildObstacleSnapshotLease = -1;
                PendingBuildObstacleSnapshotCount = 0;
                PendingDynamicObstacleSnapshotLease = -1;
                PendingDynamicObstacleSnapshotCount = 0;
                PendingBuildMutationGuardMask = 0UL;
                PendingDynamicMutationGuardMask = 0UL;
                PendingBuildMutationGuardVault = null;
                PendingDynamicMutationGuardVault = null;
                PendingDynamicScheduleTimestamp = 0L;
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
                PortalCount = 0;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = NavGridTelemetryEntryStrideBytes)]
        private struct NavGridTelemetryEntry
        {
            [FieldOffset(0)] public ulong StateHash;
            [FieldOffset(8)] public uint BufferId;
            [FieldOffset(12)] public uint Generation;
            [FieldOffset(16)] public uint Frame;
            [FieldOffset(20)] public int ExpectedLength;
            [FieldOffset(24)] public int ActualLength;
            [FieldOffset(28)] public int RecordSlot;
            [FieldOffset(32)] public float JobMicroseconds;
            [FieldOffset(36)] public float QualityWeight;
            [FieldOffset(40)] public uint Flags;
            [FieldOffset(44)] public uint Sequence;
            [FieldOffset(48)] public float3 Position;
            [FieldOffset(60)] public ushort FailureCode;
            [FieldOffset(62)] public ushort Phase;
        }

        private struct ObstacleRegistration
        {
            public BoxCollider[] Boxes;
            public CapsuleCollider[] Capsules;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime()
        {
            DisposeAll();
        }

        private static VolumeRecord[] CreateRecordPool()
        {
            VolumeRecord[] records = new VolumeRecord[MaxTrackedVolumeRecords]; // COLD ALLOC: fixed record shell pool - owner: VoxelDynamicNavGridRuntime
            for (int i = 0; i < records.Length; i++)
                records[i] = new VolumeRecord(); // COLD ALLOC: class shell only; portal scratch is shared and bounded.

            return records;
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
            _ = TryEnqueueDirtyVolume(new DirtyVolumeRequest
            {
                VolumeInstanceId = volumeInstanceId,
                RuntimeStamp = volume.RuntimeStamp
            });
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
            for (int i = 0; i < _deferredDirtyVolumeCount; i++)
            {
                DeferredDirtyVolumeRequest request = _deferredDirtyVolumes[i];
                if (!ReferenceEquals(request.Volume, volume))
                    continue;

                request.RuntimeStamp = runtimeStamp;
                request.RemainingSlowTicks = math.max(request.RemainingSlowTicks, slowTickDelay);
                _deferredDirtyVolumes[i] = request;
                return;
            }

            if (_deferredDirtyVolumeCount >= DeferredDirtyVolumeQueueCapacity)
            {
                QueueDirtyVolume(volume);
                return;
            }

            _deferredDirtyVolumes[_deferredDirtyVolumeCount] = new DeferredDirtyVolumeRequest
            {
                Volume = volume,
                RuntimeStamp = runtimeStamp,
                RemainingSlowTicks = slowTickDelay
            };
            _deferredDirtyVolumeCount++;
        }

        internal static void TickDeferredDirtyVolumes()
        {
            for (int i = _deferredDirtyVolumeCount - 1; i >= 0; i--)
            {
                DeferredDirtyVolumeRequest request = _deferredDirtyVolumes[i];
                HectonVoxelVolume volume = request.Volume;
                if (volume == null || !volume.MatchesRuntimeStamp(request.RuntimeStamp))
                {
                    RemoveDeferredDirtyVolumeAt(i);
                    continue;
                }

                request.RemainingSlowTicks--;
                if (request.RemainingSlowTicks > 0)
                {
                    _deferredDirtyVolumes[i] = request;
                    continue;
                }

                RemoveDeferredDirtyVolumeAt(i);
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
            if (!TryGetRecord(volumeInstanceId, out VolumeRecord record) ||
                !HasValidRecordBounds(record))
            {
                QueueDirtyVolume(volume);
                return;
            }

            double safeVoxelSize = math.max(0.0001d, (double)voxelSize);
            double3 minAup = new double3(minAbsoluteCell.x, minAbsoluteCell.y, minAbsoluteCell.z) * safeVoxelSize;
            double3 maxAup = (new double3(maxAbsoluteCell.x, maxAbsoluteCell.y, maxAbsoluteCell.z) + 1d) * safeVoxelSize;
            double3 centerAup = (minAup + maxAup) * 0.5d;
            double3 extentDouble = math.max((maxAup - minAup) * 0.5d, new double3(safeVoxelSize));
            float3 extents = new float3((float)extentDouble.x, (float)extentDouble.y, (float)extentDouble.z);
            if (!math.all(math.isfinite(centerAup)) ||
                !math.all(math.isfinite(extents)))
            {
                QueueDirtyVolume(volume);
                return;
            }

            Vector3 runtimeCenter = HectonFloatingOrigin.ToRuntimePosition(centerAup);
            TryEnqueueDynamicObstacleClear(new DynamicObstacleClearRequest
            {
                Center = new float3(runtimeCenter.x, runtimeCenter.y, runtimeCenter.z),
                Extents = extents
            });
        }

        internal static bool TryScheduleBuild(
            HectonVoxelVolume volume,
            int runtimeStamp,
            int3 dimensions,
            float3 origin,
            float cellSize,
            int pointCount,
            NativeArray<float> densityField,
            int jobBatch,
            JobHandle dependency,
            out JobHandle scheduledHandle)
        {
            scheduledHandle = dependency;
            if (!densityField.IsCreated ||
                densityField.Length < pointCount ||
                pointCount <= 0)
            {
                return false;
            }

            if (!TryPrepareBuild(
                    volume,
                    runtimeStamp,
                    dimensions,
                    origin,
                    cellSize,
                    pointCount,
                    out VolumeRecord record))
            {
                return false;
            }

            NativeArray<byte> outputBuffer = default;
            NativeArray<byte> baseOutputBuffer = default;
            NativeArray<ushort> distanceBuffer = default;
            NativeArray<NavObstaclePrimitive> obstacleSnapshot = default;
            int obstacleSnapshotLease = -1;
            int obstacleSnapshotCount = 0;
            IDataVault guardVault = null;
            ulong guardMask = 0UL;
            try
            {
                if (!TryAcquireNavGridMutationGuard(
                        in record.NextHandle,
                        in record.BaseNextHandle,
                        in record.NextDistanceHandle,
                        out guardMask,
                        out guardVault))
                {
                    return false;
                }

                if (!TryResolveNavGridMutable(in record.NextHandle, pointCount, out outputBuffer) ||
                    !TryResolveNavGridMutable(in record.BaseNextHandle, pointCount, out baseOutputBuffer) ||
                    !TryResolveNavGridMutable(in record.NextDistanceHandle, pointCount, out distanceBuffer))
                {
                    return false;
                }

                IDataVault vault = _dataVault;
                if (vault == null || vault.IsCompactionFenceActive)
                    return false;

                int safeBatch = math.max(1, jobBatch);
                JobHandle passabilityHandle = new PassabilityBuildJob
                {
                    DensityField = densityField,
                    Output = outputBuffer,
                    SolidThreshold = 0f
                }.Schedule(pointCount, safeBatch, dependency);

                JobHandle baseCopyHandle = new CopyByteBufferJob
                {
                    Source = outputBuffer,
                    Destination = baseOutputBuffer
                }.Schedule(pointCount, safeBatch, passabilityHandle);

                obstacleSnapshotCount = CountObstacleSnapshotPrimitives();
                if (obstacleSnapshotCount > 0 &&
                    TryAcquireObstacleSnapshotLease(out obstacleSnapshot, out obstacleSnapshotLease))
                {
                    if (!TryFillObstacleSnapshot(obstacleSnapshot, out obstacleSnapshotCount))
                    {
                        ReleaseObstacleSnapshotLease(obstacleSnapshotLease);
                        obstacleSnapshot = default;
                        obstacleSnapshotLease = -1;
                        obstacleSnapshotCount = 0;
                    }
                }
                else
                {
                    obstacleSnapshotCount = 0;
                }

                if (obstacleSnapshot.IsCreated && obstacleSnapshotCount > 0)
                {
                    baseCopyHandle = new ObstacleStampJob
                    {
                        Passability = outputBuffer,
                        Obstacles = obstacleSnapshot,
                        ObstacleCount = obstacleSnapshotCount,
                        Dimensions = dimensions,
                        Origin = origin,
                        CellSize = cellSize
                    }.Schedule(pointCount, safeBatch, baseCopyHandle);
                }

                scheduledHandle = new ClearanceDilationJob
                {
                    Passability = outputBuffer,
                    DistanceMap = distanceBuffer,
                    Dimensions = dimensions,
                    AgentRadiusCells = ResolveClearanceRadiusCells(cellSize)
                }.Schedule(baseCopyHandle);

                record.PendingBuildRuntimeStamp = runtimeStamp;
                record.PendingBuildObstacleSnapshotLease = obstacleSnapshotLease;
                record.PendingBuildObstacleSnapshotCount = obstacleSnapshotCount;
                record.PendingBuildMutationGuardMask = guardMask;
                record.PendingBuildMutationGuardVault = guardVault;
                obstacleSnapshotLease = -1;
                guardMask = 0UL;
                return true;
            }
            finally
            {
                if (obstacleSnapshotLease >= 0)
                    ReleaseObstacleSnapshotLease(obstacleSnapshotLease);
                if (guardMask != 0UL)
                    ReleaseNavGridMutationGuard(guardVault, guardMask);
            }
        }

        private static bool TryPrepareBuild(
            HectonVoxelVolume volume,
            int runtimeStamp,
            int3 dimensions,
            float3 origin,
            float cellSize,
            int pointCount,
            out VolumeRecord record)
        {
            record = null;
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
            record = GetOrCreateRecord(volumeInstanceId);
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
                               !HasCompleteDynamicUpdateBuffers(record)) ||
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

            if (!TryEnsureRecordBuffers(record, pointCount))
                return false;

            if (!EnsurePortalWorkCapacity(record))
            {
                record.PortalCount = 0;
                record.PortalsReady = false;
            }

            record.IsPureVoid = false;
            record.PortalsReady = false;
            return true;
        }

        internal static void CommitBuild(HectonVoxelVolume volume, int runtimeStamp)
        {
            if (volume == null)
                return;

            int volumeInstanceId = GetStableVolumeEntityId(volume);
            if (!TryGetRecord(volumeInstanceId, out VolumeRecord record))
                return;

            if (record.RuntimeStamp != runtimeStamp)
            {
                if (record.PendingBuildRuntimeStamp == runtimeStamp)
                {
                    ReleaseNavGridMutationGuard(record.PendingBuildMutationGuardVault, record.PendingBuildMutationGuardMask);
                    ReleaseObstacleSnapshotLease(record.PendingBuildObstacleSnapshotLease);
                    record.PendingBuildRuntimeStamp = 0;
                    record.PendingBuildObstacleSnapshotLease = -1;
                    record.PendingBuildObstacleSnapshotCount = 0;
                    record.PendingBuildMutationGuardMask = 0UL;
                    record.PendingBuildMutationGuardVault = null;
                    record.IsDirty = true;
                }

                return;
            }

            try
            {
                SwapHandles(ref record.CurrentHandle, ref record.NextHandle);
                SwapHandles(ref record.BaseCurrentHandle, ref record.BaseNextHandle);
                SwapHandles(ref record.CurrentDistanceHandle, ref record.NextDistanceHandle);
                EvaluatePureVoidState(record);
                if (record.IsPureVoid)
                {
                    record.PortalsReady = true;
                    _portalGraphDirty = true;
                    return;
                }

                RebuildPortals(record);
                _portalGraphDirty = true;
            }
            finally
            {
                ReleaseNavGridMutationGuard(record.PendingBuildMutationGuardVault, record.PendingBuildMutationGuardMask);
                ReleaseObstacleSnapshotLease(record.PendingBuildObstacleSnapshotLease);
                record.PendingBuildRuntimeStamp = 0;
                record.PendingBuildObstacleSnapshotLease = -1;
                record.PendingBuildObstacleSnapshotCount = 0;
                record.PendingBuildMutationGuardMask = 0UL;
                record.PendingBuildMutationGuardVault = null;
            }
        }

        internal static void RegisterModuleObstacle(int obstacleId, BoxCollider[] boxes, CapsuleCollider[] capsules)
        {
            if (obstacleId == 0)
                return;

            int registrationIndex = FindObstacleRegistrationIndex(obstacleId);
            if (registrationIndex < 0)
            {
                if (_registeredObstacleCount >= MaxRegisteredObstacleRecords)
                    return;

                registrationIndex = _registeredObstacleCount;
                _registeredObstacleKeys[registrationIndex] = obstacleId;
                _registeredObstacleCount++;
            }

            _registeredObstacles[registrationIndex].Boxes = boxes ?? System.Array.Empty<BoxCollider>();
            _registeredObstacles[registrationIndex].Capsules = capsules ?? System.Array.Empty<CapsuleCollider>();
            MarkAllVolumesDirty();
        }

        internal static void UnregisterModuleObstacle(int obstacleId)
        {
            if (obstacleId == 0)
                return;

            if (RemoveObstacleRegistration(obstacleId))
                MarkAllVolumesDirty();
        }

        internal static int CountObstacleSnapshotPrimitives()
        {
            int obstacleCount = 0;
            for (int obstacleIndex = 0; obstacleIndex < _registeredObstacleCount; obstacleIndex++)
            {
                ObstacleRegistration registration = _registeredObstacles[obstacleIndex];
                obstacleCount += CountLiveColliders(registration.Boxes);
                obstacleCount += CountLiveColliders(registration.Capsules);
            }

            HectonMapMagicVegetationBridge activeBridge = _vegetationBridge;
            obstacleCount += CountMacroFloraObstacles(activeBridge);
            obstacleCount += CountPersistentDynamicObstacles();

            return obstacleCount;
        }

        internal static bool TryFillObstacleSnapshot(
            NativeArray<NavObstaclePrimitive> snapshot,
            out int writtenCount)
        {
            writtenCount = 0;
            if (!snapshot.IsCreated || snapshot.Length <= 0)
                return false;

            int writeIndex = 0;
            for (int obstacleIndex = 0; obstacleIndex < _registeredObstacleCount; obstacleIndex++)
            {
                ObstacleRegistration registration = _registeredObstacles[obstacleIndex];
                WriteColliderBounds(registration.Boxes, ref snapshot, ref writeIndex);
                WriteColliderBounds(registration.Capsules, ref snapshot, ref writeIndex);
            }

            HectonMapMagicVegetationBridge activeBridge = _vegetationBridge;
            WriteMacroFloraObstacles(activeBridge, ref snapshot, ref writeIndex);
            WritePersistentDynamicObstacles(ref snapshot, ref writeIndex);
            writtenCount = math.min(writeIndex, snapshot.Length);
            return writtenCount > 0;
        }

        private static bool EnsureObstacleSnapshotPoolCold()
        {
            if (_obstacleSnapshotPoolReady)
                return true;

            for (int i = 0; i < _obstacleSnapshotPool.Length; i++)
            {
                if (!_obstacleSnapshotPool[i].IsCreated)
                {
                    _obstacleSnapshotPool[i] = H8Memory.Allocate<NavObstaclePrimitive>(
                        MaxObstacleSnapshotPrimitiveCount,
                        NavGridVaultOwner,
                        Allocator.Persistent,
                        NativeArrayOptions.UninitializedMemory);
                    if (!_obstacleSnapshotPool[i].IsCreated)
                        return false;
                }

                _obstacleSnapshotLeaseStates[i] = 0;
            }

            _obstacleSnapshotPoolReady = true;
            return true;
        }

        private static bool TryAcquireObstacleSnapshotLease(
            out NativeArray<NavObstaclePrimitive> snapshot,
            out int leaseIndex)
        {
            snapshot = default;
            leaseIndex = -1;
            if (!_obstacleSnapshotPoolReady)
                return false;

            for (int i = 0; i < _obstacleSnapshotPool.Length; i++)
            {
                if (_obstacleSnapshotLeaseStates[i] != 0)
                    continue;

                NativeArray<NavObstaclePrimitive> candidate = _obstacleSnapshotPool[i];
                if (!candidate.IsCreated)
                    continue;

                _obstacleSnapshotLeaseStates[i] = 1;
                snapshot = candidate;
                leaseIndex = i;
                return true;
            }

            return false;
        }

        private static void ReleaseObstacleSnapshotLease(int leaseIndex)
        {
            if ((uint)leaseIndex >= (uint)_obstacleSnapshotLeaseStates.Length)
                return;

            _obstacleSnapshotLeaseStates[leaseIndex] = 0;
        }

        private static void DisposeObstacleSnapshotPool()
        {
            for (int i = 0; i < _obstacleSnapshotPool.Length; i++)
            {
                _obstacleSnapshotLeaseStates[i] = 0;
                if (!_obstacleSnapshotPool[i].IsCreated)
                    continue;

                H8Memory.Release(ref _obstacleSnapshotPool[i], NavGridVaultOwner);
            }

            _obstacleSnapshotPoolReady = false;
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

        internal static void EnqueueDestroyedOrganicEvents(System.ReadOnlySpan<DestroyedOrganicEvent> destroyedEvents)
        {
            if (destroyedEvents.Length <= 0)
                return;

            EnsureInitialized();
            for (int i = 0; i < destroyedEvents.Length; i++)
            {
                DestroyedOrganicEvent destroyedEvent = destroyedEvents[i];
                EnqueueDestroyedOrganicEvent(in destroyedEvent);
            }
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
            for (int recordIndex = 0; recordIndex < _recordCount; recordIndex++)
            {
                VolumeRecord record = _records[recordIndex];
                if (record == null || !record.HasPendingDynamicUpdate)
                    continue;

                if (!DispatcherJobSwap.TryComplete(ref record.PendingDynamicUpdateHandle, forceComplete: false))
                    continue;

                CompleteDynamicObstacleUpdate(record);
            }

            if (_teardownPending)
                DisposePendingCompletedRecords(false);
        }

        private static void CompleteDynamicObstacleUpdate(VolumeRecord record)
        {
            if (record == null)
                return;

            try
            {
                EvaluateDynamicClearanceBudget(record.PendingDynamicScheduleTimestamp);
                record.PendingDynamicNextHandle = default;
                record.PendingDynamicNextDistanceHandle = default;
                record.PendingDynamicScheduleTimestamp = 0L;
                record.HasPendingDynamicUpdate = false;
                SwapHandles(ref record.CurrentHandle, ref record.NextHandle);
                SwapHandles(ref record.CurrentDistanceHandle, ref record.NextDistanceHandle);
                record.PendingRegionMin = int3.zero;
                record.PendingRegionMax = int3.zero;
                EvaluatePureVoidState(record);
                if (!record.IsPureVoid)
                    RebuildPortals(record);
                _portalGraphDirty = true;
            }
            finally
            {
                ReleaseNavGridMutationGuard(record.PendingDynamicMutationGuardVault, record.PendingDynamicMutationGuardMask);
                ReleaseObstacleSnapshotLease(record.PendingDynamicObstacleSnapshotLease);
                record.PendingDynamicObstacleSnapshotLease = -1;
                record.PendingDynamicObstacleSnapshotCount = 0;
                record.PendingDynamicMutationGuardMask = 0UL;
                record.PendingDynamicMutationGuardVault = null;
            }
        }

        internal static void SchedulePendingDynamicObstacleUpdates()
        {
            if (_pendingObstacleClearQueueCount <= 0)
                return;

            if (!TryDequeueValidDynamicClearRequest(out DynamicObstacleClearRequest clearRequest))
                return;

            bool useReducedClearance = _dynamicClearanceFallbackSchedulesRemaining > 0;
            bool scheduledAnyRecord = false;
            for (int recordIndex = 0; recordIndex < _recordCount; recordIndex++)
            {
                VolumeRecord record = _records[recordIndex];
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

                int3 regionSize = regionMax - regionMin + 1;
                long regionPointCountLong = (long)regionSize.x * regionSize.y * regionSize.z;
                if (regionPointCountLong <= 0L || regionPointCountLong > int.MaxValue)
                    continue;

                int regionPointCount = (int)regionPointCountLong;
                NativeArray<byte> current = default;
                NativeArray<byte> next = default;
                NativeArray<byte> baseCurrent = default;
                NativeArray<ushort> currentDistance = default;
                NativeArray<ushort> nextDistance = default;
                NativeArray<NavObstaclePrimitive> obstacleSnapshot = default;
                int obstacleSnapshotLease = -1;
                int obstacleSnapshotCount = 0;
                VaultGenerationHandle<byte> lockedNextHandle = record.NextHandle;
                VaultGenerationHandle<ushort> lockedNextDistanceHandle = record.NextDistanceHandle;
                IDataVault guardVault = null;
                ulong guardMask = 0UL;
                try
                {
                    if (!TryResolveNavGridRead(in record.CurrentHandle, requiredCellCount, out current) ||
                        !TryResolveNavGridRead(in record.BaseCurrentHandle, requiredCellCount, out baseCurrent) ||
                        !TryResolveNavGridRead(in record.CurrentDistanceHandle, requiredCellCount, out currentDistance))
                    {
                        continue;
                    }

                    if (!TryAcquireNavGridMutationGuard(
                            in record.NextHandle,
                            in record.NextDistanceHandle,
                            out guardMask,
                            out guardVault))
                    {
                        continue;
                    }

                    if (!TryResolveNavGridMutable(in record.NextHandle, requiredCellCount, out next) ||
                        !TryResolveNavGridMutable(in record.NextDistanceHandle, requiredCellCount, out nextDistance))
                    {
                        continue;
                    }

                    IDataVault vault = _dataVault;
                    if (vault == null || vault.IsCompactionFenceActive)
                        continue;

                    JobHandle passabilityCopyHandle = new CopyByteBufferJob
                    {
                        Source = current,
                        Destination = next
                    }.Schedule(requiredCellCount, 64);
                    JobHandle distanceCopyHandle = new CopyUShortBufferJob
                    {
                        Source = currentDistance,
                        Destination = nextDistance
                    }.Schedule(requiredCellCount, 64);
                    JobHandle resetDependency = JobHandle.CombineDependencies(passabilityCopyHandle, distanceCopyHandle);

                    obstacleSnapshotCount = CountObstacleSnapshotPrimitives();
                    if (obstacleSnapshotCount > 0 &&
                        TryAcquireObstacleSnapshotLease(out obstacleSnapshot, out obstacleSnapshotLease))
                    {
                        if (!TryFillObstacleSnapshot(obstacleSnapshot, out obstacleSnapshotCount))
                        {
                            ReleaseObstacleSnapshotLease(obstacleSnapshotLease);
                            obstacleSnapshot = default;
                            obstacleSnapshotLease = -1;
                            obstacleSnapshotCount = 0;
                        }
                    }
                    else
                    {
                        obstacleSnapshotCount = 0;
                    }

                    record.PendingRegionMin = regionMin;
                    record.PendingRegionMax = regionMax;

                    int clearanceRadiusCells = ResolveDynamicClearanceRadiusCells(record.CellSize, useReducedClearance);
                    JobHandle stampHandle = new PartialObstacleResetAndStampJob
                    {
                        BasePassability = baseCurrent,
                        Passability = next,
                        Obstacles = obstacleSnapshot,
                        ObstacleCount = obstacleSnapshotCount,
                        Dimensions = record.Dimensions,
                        RegionMin = regionMin,
                        RegionMax = regionMax,
                        Origin = record.Origin,
                        CellSize = record.CellSize
                    }.Schedule(regionPointCount, 64, resetDependency);

                    long completionStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                    JobHandle updateHandle;
                    using (_partialClearanceDilationScheduleMarker.Auto())
                    {
                        updateHandle = new PartialClearanceDilationJob
                        {
                            Passability = next,
                            ReferenceDistanceMap = currentDistance,
                            DistanceMap = nextDistance,
                            Dimensions = record.Dimensions,
                            RegionMin = regionMin,
                            RegionMax = regionMax,
                            AgentRadiusCells = clearanceRadiusCells
                        }.Schedule(stampHandle);
                    }

                    record.PendingDynamicUpdateHandle = updateHandle;
                    record.PendingDynamicNextHandle = lockedNextHandle;
                    record.PendingDynamicNextDistanceHandle = lockedNextDistanceHandle;
                    record.PendingDynamicObstacleSnapshotLease = obstacleSnapshotLease;
                    record.PendingDynamicObstacleSnapshotCount = obstacleSnapshotCount;
                    record.PendingDynamicMutationGuardMask = guardMask;
                    record.PendingDynamicMutationGuardVault = guardVault;
                    record.PendingDynamicScheduleTimestamp = completionStartTimestamp;
                    record.HasPendingDynamicUpdate = true;
                    obstacleSnapshot = default;
                    obstacleSnapshotLease = -1;
                    guardMask = 0UL;
                    scheduledAnyRecord = true;
                }
                finally
                {
                    if (obstacleSnapshotLease >= 0)
                        ReleaseObstacleSnapshotLease(obstacleSnapshotLease);
                    if (guardMask != 0UL)
                        ReleaseNavGridMutationGuard(guardVault, guardMask);
                }
            }

            if (scheduledAnyRecord && useReducedClearance)
                _dynamicClearanceFallbackSchedulesRemaining--;
        }

        private static bool HasPendingDynamicObstacleUpdate()
        {
            for (int recordIndex = 0; recordIndex < _recordCount; recordIndex++)
            {
                VolumeRecord record = _records[recordIndex];
                if (record != null && record.HasPendingDynamicUpdate)
                    return true;
            }

            return false;
        }

        private static bool IsPortalRouteReady(VolumeRecord startRecord, VolumeRecord endRecord)
        {
            return startRecord != null &&
                   endRecord != null &&
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
                   TryDequeuePendingObstacleClear(out DynamicObstacleClearRequest candidate))
            {
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
            out NativeArray<byte>.ReadOnly passability,
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
            if (!TryGetRecord(volumeInstanceId, out VolumeRecord record) ||
                !HasValidRecordBounds(record))
            {
                return false;
            }

            if (!TryResolveVoxelCellCount(record.Dimensions, out int requiredCellCount) ||
                !TryReadOnlyNavGrid(in record.CurrentHandle, requiredCellCount, out passability))
            {
                return false;
            }

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
            if (_portalGraphNodeCount <= 0 ||
                startRecord.PortalCount <= 0 ||
                endRecord.PortalCount <= 0 ||
                !TrySolvePortalRoute(startRecord, endRecord, startWorldPosition, endWorldPosition))
            {
                return false;
            }

            if (!CanEmitPortalRoutePath(outputWaypoints.Length))
                return false;

            outputWaypoints[waypointCount++] = new Vector3(startWorldPosition.x, startWorldPosition.y, startWorldPosition.z);
            for (int i = _routePathCount - 1; i >= 0; i--)
            {
                PortalNode node = _portalGraphNodes[_routePathScratch[i]];
                outputWaypoints[waypointCount++] = new Vector3(node.Centroid.x, node.Centroid.y, node.Centroid.z);
            }

            outputWaypoints[waypointCount++] = new Vector3(endWorldPosition.x, endWorldPosition.y, endWorldPosition.z);
            return waypointCount >= 2;
        }

        internal static bool TryGetContainingPassabilityPayload(
            float3 worldPosition,
            out NativeArray<byte>.ReadOnly passability,
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

            if (!TryResolveVoxelCellCount(record.Dimensions, out int requiredCellCount) ||
                !TryReadOnlyNavGrid(in record.CurrentHandle, requiredCellCount, out passability))
            {
                return false;
            }

            dimensions = record.Dimensions;
            origin = record.Origin;
            cellSize = record.CellSize;
            return true;
        }

        internal static bool TryGetNearestPassabilityPayload(
            float3 worldPosition,
            out NativeArray<byte>.ReadOnly passability,
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
            for (int recordIndex = 0; recordIndex < _recordCount; recordIndex++)
            {
                VolumeRecord candidate = _records[recordIndex];
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

            if (!TryResolveVoxelCellCount(nearestRecord.Dimensions, out int requiredCellCount) ||
                !TryReadOnlyNavGrid(in nearestRecord.CurrentHandle, requiredCellCount, out passability))
            {
                return false;
            }

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
            for (int recordIndex = 0; recordIndex < _recordCount; recordIndex++)
            {
                VolumeRecord record = _records[recordIndex];
                if (!VoxelDynamicNavGridRuntime.HasValidRecordBounds(record))
                {
                    continue;
                }

                if (!TryResolveVoxelCellCount(record.Dimensions, out int requiredCellCount) ||
                    !TryReadOnlyNavGrid(in record.CurrentHandle, requiredCellCount, out NativeArray<byte>.ReadOnly current))
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
                                    flatIndex >= current.Length ||
                                    current[flatIndex] != OpenCell)
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
            HectonMapMagicVegetationBridge activeBridge = _vegetationBridge;
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
            if (_portalGraphNodeCount <= 0 ||
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
            for (int i = _routePathCount - 1; i >= 0; i--)
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
            if (TryGetRecord(volumeInstanceId, out VolumeRecord record))
            {
                record.PendingRemoval = true;
                if (record.TryDisposeCompleted())
                {
                    RemoveRecord(volumeInstanceId);
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
            _recordRemovalScratchCount = 0;

            for (int recordIndex = 0; recordIndex < _recordCount; recordIndex++)
            {
                int recordKey = _recordKeys[recordIndex];
                VolumeRecord record = _records[recordIndex];
                if (record == null)
                {
                    AddRecordRemovalScratch(recordKey);
                    continue;
                }

                if (markAllRecordsForRemoval)
                    record.PendingRemoval = true;

                if (!record.PendingRemoval)
                    continue;

                if (record.TryDisposeCompleted())
                {
                    AddRecordRemovalScratch(recordKey);
                    continue;
                }

                blockedByPendingJob = true;
            }

            for (int i = 0; i < _recordRemovalScratchCount; i++)
                RemoveRecord(_recordRemovalScratch[i]);

            _recordRemovalScratchCount = 0;
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
            ClearRecordStorage();
            ReleaseNavGridTelemetryBuffers(_dataVault);
            DisposeObstacleSnapshotPool();
            ClearDeferredDirtyVolumes();
            ClearObstacleRegistrations();
            _dirtyRequestSpillCount = 0;
            _portalGraphNodeCount = 0;
            _portalFaceVisitStamp = 0;
            System.Array.Clear(_portalFaceVisitScratch, 0, _portalFaceVisitScratch.Length);
            _routeNodeScratchCount = 0;
            _routeOpenSetCount = 0;
            _routePathCount = 0;
            _recordRemovalScratchCount = 0;
            _portalGraphDirty = true;
            _teardownPending = false;
            _clearRuntimeContainersWhenTeardownCompletes = false;
            _dirtyVolumeQueueCount = 0;
            _pendingObstacleClearQueueCount = 0;
            _dirtyVolumeQueueHead = 0;
            _dirtyVolumeQueueTail = 0;
            _pendingObstacleClearQueueHead = 0;
            _pendingObstacleClearQueueTail = 0;
            _persistentDynamicObstacleWriteCursor = 0;
            _persistentDynamicObstacleCount = 0;
            System.Array.Clear(_persistentDynamicObstacles, 0, _persistentDynamicObstacles.Length);
        }

        private static void EnsureInitialized()
        {
            EnsureLifecycleOwner();
            if (_teardownPending)
                DisposePendingCompletedRecords(false);

        }

        private static bool TryEnqueueDirtyVolume(DirtyVolumeRequest request)
        {
            if (_dirtyVolumeQueueCount >= DirtyVolumeQueueCapacity)
                return false;

            _dirtyVolumes[_dirtyVolumeQueueTail] = request;
            _dirtyVolumeQueueTail++;
            if (_dirtyVolumeQueueTail >= DirtyVolumeQueueCapacity)
                _dirtyVolumeQueueTail = 0;
            _dirtyVolumeQueueCount++;
            return true;
        }

        private static bool TryDequeueDirtyVolume(out DirtyVolumeRequest request)
        {
            request = default;
            if (_dirtyVolumeQueueCount <= 0)
                return false;

            request = _dirtyVolumes[_dirtyVolumeQueueHead];
            _dirtyVolumes[_dirtyVolumeQueueHead] = default;
            _dirtyVolumeQueueHead++;
            if (_dirtyVolumeQueueHead >= DirtyVolumeQueueCapacity)
                _dirtyVolumeQueueHead = 0;
            _dirtyVolumeQueueCount--;
            return true;
        }

        private static bool TryEnqueuePendingObstacleClear(DynamicObstacleClearRequest request)
        {
            if (_pendingObstacleClearQueueCount >= PendingObstacleClearQueueCapacity)
                return false;

            _pendingObstacleClears[_pendingObstacleClearQueueTail] = request;
            _pendingObstacleClearQueueTail++;
            if (_pendingObstacleClearQueueTail >= PendingObstacleClearQueueCapacity)
                _pendingObstacleClearQueueTail = 0;
            _pendingObstacleClearQueueCount++;
            return true;
        }

        private static bool TryDequeuePendingObstacleClear(out DynamicObstacleClearRequest request)
        {
            request = default;
            if (_pendingObstacleClearQueueCount <= 0)
                return false;

            request = _pendingObstacleClears[_pendingObstacleClearQueueHead];
            _pendingObstacleClears[_pendingObstacleClearQueueHead] = default;
            _pendingObstacleClearQueueHead++;
            if (_pendingObstacleClearQueueHead >= PendingObstacleClearQueueCapacity)
                _pendingObstacleClearQueueHead = 0;
            _pendingObstacleClearQueueCount--;
            return true;
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
            for (int recordIndex = 0; recordIndex < _recordCount; recordIndex++)
            {
                VolumeRecord record = _records[recordIndex];
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

            RecordNavGridTelemetry(
                0u,
                0u,
                0,
                0,
                -1,
                NavGridFailureBudget,
                NavGridPhaseDynamicUpdate,
                NavGridFlagOverBudget,
                (float)(elapsedMilliseconds * 1000.0d),
                float3.zero);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            float now = (float)Hecton8.Core.SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (now >= _nextDynamicClearanceWarningTime)
            {
                _nextDynamicClearanceWarningTime = now + DynamicClearanceWarningCooldownSeconds;
                Hecton8.Core.H8Debug.LogWarning(DynamicClearanceBudgetWarningMessage);
            }
#endif
        }

        private static int GetStableVolumeEntityId(HectonVoxelVolume volume)
        {
            return volume != null
                ? unchecked((int)EntityId.ToULong(volume.GetEntityId()))
                : 0;
        }

        private static bool TryGetRecord(int volumeInstanceId, out VolumeRecord record)
        {
            int index = FindRecordIndex(volumeInstanceId);
            if (index < 0)
            {
                record = null;
                return false;
            }

            record = _records[index];
            return record != null;
        }

        private static int FindRecordIndex(int volumeInstanceId)
        {
            for (int i = 0; i < _recordCount; i++)
            {
                if (_recordKeys[i] == volumeInstanceId)
                    return i;
            }

            return -1;
        }

        private static bool RemoveRecord(int volumeInstanceId)
        {
            int index = FindRecordIndex(volumeInstanceId);
            if (index < 0)
                return false;

            VolumeRecord removedRecord = _records[index];
            if (removedRecord != null)
            {
                ReleaseVoxelBuffers(removedRecord);
                ReleaseRecordBufferSlot(removedRecord.BufferSlot);
                removedRecord.ResetForReuse();
            }

            int lastIndex = _recordCount - 1;
            if (index != lastIndex)
            {
                VolumeRecord lastRecord = _records[lastIndex];
                _recordKeys[index] = _recordKeys[lastIndex];
                _records[index] = lastRecord;
                _records[lastIndex] = removedRecord;
            }

            _recordKeys[lastIndex] = 0;
            _recordCount--;
            return true;
        }

        private static void ClearRecordStorage()
        {
            for (int i = 0; i < _recordCount; i++)
            {
                VolumeRecord record = _records[i];
                if (record != null)
                {
                    ReleaseVoxelBuffers(record);
                    ReleaseRecordBufferSlot(record.BufferSlot);
                    record.ResetForReuse();
                }

                _recordKeys[i] = 0;
            }

            _recordCount = 0;
        }

        private static void AddRecordRemovalScratch(int recordKey)
        {
            if (_recordRemovalScratchCount >= _recordRemovalScratch.Length)
                return;

            _recordRemovalScratch[_recordRemovalScratchCount] = recordKey;
            _recordRemovalScratchCount++;
        }

        private static void RemoveDeferredDirtyVolumeAt(int index)
        {
            if ((uint)index >= (uint)_deferredDirtyVolumeCount)
                return;

            int lastIndex = _deferredDirtyVolumeCount - 1;
            if (index != lastIndex)
                _deferredDirtyVolumes[index] = _deferredDirtyVolumes[lastIndex];

            _deferredDirtyVolumes[lastIndex] = default;
            _deferredDirtyVolumeCount--;
        }

        private static void ClearDeferredDirtyVolumes()
        {
            for (int i = 0; i < _deferredDirtyVolumeCount; i++)
                _deferredDirtyVolumes[i] = default;

            _deferredDirtyVolumeCount = 0;
        }

        private static int FindObstacleRegistrationIndex(int obstacleId)
        {
            for (int i = 0; i < _registeredObstacleCount; i++)
            {
                if (_registeredObstacleKeys[i] == obstacleId)
                    return i;
            }

            return -1;
        }

        private static bool RemoveObstacleRegistration(int obstacleId)
        {
            int index = FindObstacleRegistrationIndex(obstacleId);
            if (index < 0)
                return false;

            int lastIndex = _registeredObstacleCount - 1;
            if (index != lastIndex)
            {
                _registeredObstacleKeys[index] = _registeredObstacleKeys[lastIndex];
                _registeredObstacles[index] = _registeredObstacles[lastIndex];
            }

            _registeredObstacleKeys[lastIndex] = 0;
            _registeredObstacles[lastIndex] = default;
            _registeredObstacleCount--;
            return true;
        }

        private static void ClearObstacleRegistrations()
        {
            for (int i = 0; i < _registeredObstacleCount; i++)
            {
                _registeredObstacleKeys[i] = 0;
                _registeredObstacles[i] = default;
            }

            _registeredObstacleCount = 0;
        }

        private static VolumeRecord GetOrCreateRecord(int volumeInstanceId)
        {
            if (!TryGetRecord(volumeInstanceId, out VolumeRecord record))
            {
                if (_recordCount >= MaxTrackedVolumeRecords)
                    return null;

                int bufferSlot = AllocateRecordBufferSlot();
                if (bufferSlot < 0)
                    return null;

                record = _records[_recordCount];
                if (record == null)
                {
                    ReleaseRecordBufferSlot(bufferSlot);
                    return null;
                }

                record.ResetForReuse();
                record.BufferSlot = bufferSlot;
                _recordKeys[_recordCount] = volumeInstanceId;
                _recordCount++;
            }

            return record;
        }

        private static bool ConsumeDirtyMarker(int volumeInstanceId, int runtimeStamp)
        {
            if (_dirtyVolumeQueueCount <= 0)
                return false;

            bool found = false;
            _dirtyRequestSpillCount = 0;
            int scanBudget = _dirtyVolumeQueueCount > 0
                ? _dirtyVolumeQueueCount
                : DirtyVolumeQueueCapacity;
            while (scanBudget-- > 0 &&
                   TryDequeueDirtyVolume(out DirtyVolumeRequest request))
            {
                if (!found &&
                    request.VolumeInstanceId == volumeInstanceId &&
                    request.RuntimeStamp == runtimeStamp)
                {
                    found = true;
                    continue;
                }

                if (_dirtyRequestSpillCount < _dirtyRequestSpill.Length)
                {
                    _dirtyRequestSpill[_dirtyRequestSpillCount] = request;
                    _dirtyRequestSpillCount++;
                }
            }

            _dirtyVolumeQueueCount = 0;
            _dirtyVolumeQueueHead = 0;
            _dirtyVolumeQueueTail = 0;
            for (int i = 0; i < _dirtyRequestSpillCount; i++)
            {
                if (!TryEnqueueDirtyVolume(_dirtyRequestSpill[i]))
                    break;
            }

            _dirtyRequestSpillCount = 0;
            return found;
        }

        private static void TryEnqueueDynamicObstacleClear(DynamicObstacleClearRequest request)
        {
            if (!IsValidDynamicObstacleBounds(request.Center, request.Extents))
            {
                return;
            }

            if (!TryEnqueuePendingObstacleClear(request))
            {
                MarkAllVolumesDirty();
                return;
            }
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

        internal static void SetDataVault(IDataVault dataVault)
        {
            IDataVault previousVault = _dataVault;
            if (ReferenceEquals(previousVault, dataVault))
                return;

            if (previousVault != null)
                ReleaseNavGridTelemetryBuffers(previousVault);

            _dataVault = dataVault;
            if (dataVault != null)
            {
                EnsureNavGridTelemetryBuffers(dataVault);
                EnsureObstacleSnapshotPoolCold();
            }
        }

        internal static void SetVegetationBridge(HectonMapMagicVegetationBridge vegetationBridge)
        {
            _vegetationBridge = vegetationBridge;
        }

        internal static bool IsTeardownPending()
        {
            return _teardownPending;
        }

        private static BufferID ResolveNavGridBufferId(int slot, int lane)
        {
            if ((uint)slot >= (uint)MaxTrackedVolumeRecords ||
                (uint)lane >= (uint)NavGridVaultBufferStride)
            {
                return BufferID.Unknown;
            }

            int bufferId = NavGridVaultBufferBase + (slot * NavGridVaultBufferStride) + lane;
            if (bufferId < NavGridVaultBufferBase || bufferId > NavGridVaultBufferEnd)
                return BufferID.Unknown;

            return (BufferID)bufferId;
        }

        private static int AllocateRecordBufferSlot()
        {
            for (int i = 0; i < _recordBufferSlots.Length; i++)
            {
                if (_recordBufferSlots[i] != 0)
                    continue;

                _recordBufferSlots[i] = 1;
                return i;
            }

            return -1;
        }

        private static void ReleaseRecordBufferSlot(int slot)
        {
            if ((uint)slot >= (uint)_recordBufferSlots.Length)
                return;

            _recordBufferSlots[slot] = 0;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID != 0u &&
                   handle.SystemID == (uint)NavGridVaultOwner &&
                   handle.Generation != 0u;
        }

        private static bool HasNavGridBufferLength<T>(in VaultGenerationHandle<T> handle, int requiredLength)
            where T : struct
        {
            if (requiredLength <= 0 || !IsVaultHandleCreated(in handle))
                return false;

            IDataVault vault = _dataVault;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength &&
                   !vault.IsCompactionFenceActive;
        }

        private static bool EnsureNavGridBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            int slot,
            int lane,
            int requiredLength,
            NativeArrayOptions options)
            where T : struct
        {
            if ((uint)slot >= (uint)MaxTrackedVolumeRecords || requiredLength <= 0)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
            {
                RecordNavGridTelemetry(0u, 0u, requiredLength, 0, slot, NavGridFailureVaultMissing, NavGridPhaseVault, NavGridFlagFailClosed, 0f, float3.zero);
                return false;
            }

            if (vault.IsCompactionFenceActive)
            {
                RecordNavGridTelemetry(0u, 0u, requiredLength, 0, slot, NavGridFailureCompactionFence, NavGridPhaseVault, NavGridFlagFailClosed | NavGridFlagCompaction, 0f, float3.zero);
                return false;
            }

            BufferID bufferId = ResolveNavGridBufferId(slot, lane);
            if (bufferId == BufferID.Unknown)
            {
                RecordNavGridTelemetry(0u, 0u, requiredLength, 0, slot, NavGridFailureInvalidBufferId, NavGridPhaseVault, NavGridFlagFailClosed, 0f, float3.zero);
                return false;
            }

            if (handle.BufferID != (uint)bufferId ||
                handle.SystemID != (uint)NavGridVaultOwner ||
                !HasNavGridBufferLength(in handle, requiredLength))
            {
                handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, NavGridVaultOwner, options);
            }

            bool valid = handle.BufferID == (uint)bufferId &&
                         handle.SystemID == (uint)NavGridVaultOwner &&
                         HasNavGridBufferLength(in handle, requiredLength);
            if (!valid)
            {
                RecordNavGridTelemetry(
                    unchecked((uint)(int)bufferId),
                    handle.Generation,
                    requiredLength,
                    0,
                    slot,
                    NavGridFailureHandleResolve,
                    NavGridPhaseVault,
                    NavGridFlagFailClosed,
                    0f,
                    float3.zero);
            }

            return valid;
        }

        private static bool TryEnsureRecordBuffers(VolumeRecord record, int requiredCellCount)
        {
            return record != null &&
                   EnsureNavGridBuffer(ref record.CurrentHandle, record.BufferSlot, NavGridLaneCurrent, requiredCellCount, NativeArrayOptions.ClearMemory) &&
                   EnsureNavGridBuffer(ref record.NextHandle, record.BufferSlot, NavGridLaneNext, requiredCellCount, NativeArrayOptions.ClearMemory) &&
                   EnsureNavGridBuffer(ref record.BaseCurrentHandle, record.BufferSlot, NavGridLaneBaseCurrent, requiredCellCount, NativeArrayOptions.ClearMemory) &&
                   EnsureNavGridBuffer(ref record.BaseNextHandle, record.BufferSlot, NavGridLaneBaseNext, requiredCellCount, NativeArrayOptions.ClearMemory) &&
                   EnsureNavGridBuffer(ref record.CurrentDistanceHandle, record.BufferSlot, NavGridLaneCurrentDistance, requiredCellCount, NativeArrayOptions.ClearMemory) &&
                   EnsureNavGridBuffer(ref record.NextDistanceHandle, record.BufferSlot, NavGridLaneNextDistance, requiredCellCount, NativeArrayOptions.ClearMemory);
        }

        private static bool IsNavGridHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)NavGridVaultOwner &&
                   handle.Generation != 0u;
        }

        private static bool EnsureNavGridTelemetryBuffers(IDataVault vault)
        {
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!IsNavGridHandle(in _navGridTelemetryRingHandle, BufferID.VoxelDynamicNavGridTelemetryRing) ||
                !HasNavGridBufferLength(in _navGridTelemetryRingHandle, NavGridTelemetryFrameCount))
            {
                _navGridTelemetryRingHandle = vault.EnsureGenerationHandle<NavGridTelemetryEntry>(
                    BufferID.VoxelDynamicNavGridTelemetryRing,
                    NavGridTelemetryFrameCount,
                    NavGridVaultOwner,
                    NativeArrayOptions.ClearMemory);
            }

            if (!IsNavGridHandle(in _navGridTelemetryCursorHandle, BufferID.VoxelDynamicNavGridTelemetryCursor) ||
                !HasNavGridBufferLength(in _navGridTelemetryCursorHandle, 1))
            {
                _navGridTelemetryCursorHandle = vault.EnsureGenerationHandle<int>(
                    BufferID.VoxelDynamicNavGridTelemetryCursor,
                    1,
                    NavGridVaultOwner,
                    NativeArrayOptions.ClearMemory);
            }

            return IsNavGridHandle(in _navGridTelemetryRingHandle, BufferID.VoxelDynamicNavGridTelemetryRing) &&
                   IsNavGridHandle(in _navGridTelemetryCursorHandle, BufferID.VoxelDynamicNavGridTelemetryCursor) &&
                   HasNavGridBufferLength(in _navGridTelemetryRingHandle, NavGridTelemetryFrameCount) &&
                   HasNavGridBufferLength(in _navGridTelemetryCursorHandle, 1);
        }

        private static void ReleaseNavGridTelemetryBuffers(IDataVault vault)
        {
            if (vault != null)
            {
                if (IsNavGridHandle(in _navGridTelemetryRingHandle, BufferID.VoxelDynamicNavGridTelemetryRing))
                    vault.ReleaseBuffer(in _navGridTelemetryRingHandle);
                if (IsNavGridHandle(in _navGridTelemetryCursorHandle, BufferID.VoxelDynamicNavGridTelemetryCursor))
                    vault.ReleaseBuffer(in _navGridTelemetryCursorHandle);
            }

            _navGridTelemetryRingHandle = default;
            _navGridTelemetryCursorHandle = default;
            _navGridTelemetrySequence = 0u;
        }

        private static void RecordNavGridTelemetry(
            uint bufferId,
            uint generation,
            int expectedLength,
            int actualLength,
            int recordSlot,
            ushort failureCode,
            ushort phase,
            uint flags,
            float jobMicroseconds,
            float3 position)
        {
            IDataVault vault = _dataVault;
            if (!EnsureNavGridTelemetryBuffers(vault))
                return;

            if (!TryAdvanceNavGridTelemetryCursor(vault, out int slot, out uint sequence))
                return;

            NavGridTelemetryEntry entry = new NavGridTelemetryEntry
            {
                StateHash = NavGridTelemetryStateHash,
                BufferId = bufferId,
                Generation = generation,
                Frame = (uint)math.max(0, Time.frameCount),
                ExpectedLength = expectedLength,
                ActualLength = actualLength,
                RecordSlot = recordSlot,
                JobMicroseconds = jobMicroseconds,
                QualityWeight = ResolveNavGridQualityWeight01(),
                FailureCode = failureCode,
                Phase = phase,
                Flags = flags,
                Position = position,
                Sequence = sequence
            };

            TryWriteNavGridTelemetryRing(vault, slot, in entry);
        }

        private static bool TryAdvanceNavGridTelemetryCursor(IDataVault vault, out int slot, out uint sequence)
        {
            slot = 0;
            sequence = 0u;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(in _navGridTelemetryCursorHandle, NavGridVaultOwner, out NativeArray<int> cursor))
            {
                return false;
            }

            try
            {
                if (!cursor.IsCreated || cursor.Length <= 0)
                    return false;

                slot = cursor[0];
                if ((uint)slot >= (uint)NavGridTelemetryFrameCount)
                    slot = 0;

                int nextSlot = slot + 1;
                if (nextSlot >= NavGridTelemetryFrameCount)
                    nextSlot = 0;

                cursor[0] = nextSlot;

                sequence = _navGridTelemetrySequence + 1u;
                _navGridTelemetrySequence = sequence;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _navGridTelemetryCursorHandle, NavGridVaultOwner);
            }
        }

        private static bool TryWriteNavGridTelemetryRing(
            IDataVault vault,
            int slot,
            in NavGridTelemetryEntry entry)
        {
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(in _navGridTelemetryRingHandle, NavGridVaultOwner, out NativeArray<NavGridTelemetryEntry> ring))
            {
                return false;
            }

            try
            {
                if (!ring.IsCreated || ring.Length <= 0)
                    return false;

                int ringSlot = slot;
                if ((uint)ringSlot >= (uint)ring.Length)
                    ringSlot = 0;

                ring[ringSlot] = entry;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _navGridTelemetryRingHandle, NavGridVaultOwner);
            }
        }

        private static float ResolveNavGridQualityWeight01()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, weight, math.isfinite(weight)));
        }

        private static bool TryAcquireNavGridMutationGuard<TA, TB>(
            in VaultGenerationHandle<TA> first,
            in VaultGenerationHandle<TB> second,
            out ulong guardMask,
            out IDataVault guardVault)
            where TA : struct
            where TB : struct
        {
            guardMask = NavGridMutationGuardBit(in first) |
                        NavGridMutationGuardBit(in second);
            return TryAcquireNavGridMutationGuard(guardMask, out guardVault);
        }

        private static bool TryAcquireNavGridMutationGuard<TA, TB, TC>(
            in VaultGenerationHandle<TA> first,
            in VaultGenerationHandle<TB> second,
            in VaultGenerationHandle<TC> third,
            out ulong guardMask,
            out IDataVault guardVault)
            where TA : struct
            where TB : struct
            where TC : struct
        {
            guardMask = NavGridMutationGuardBit(in first) |
                        NavGridMutationGuardBit(in second) |
                        NavGridMutationGuardBit(in third);
            return TryAcquireNavGridMutationGuard(guardMask, out guardVault);
        }

        private static bool TryAcquireNavGridMutationGuard(ulong guardMask, out IDataVault guardVault)
        {
            guardVault = _dataVault;
            if (guardVault == null || guardMask == 0UL || guardVault.IsCompactionFenceActive)
                return false;

            if (!guardVault.TryAcquireMutationGuard(guardMask))
            {
                guardVault = null;
                return false;
            }

            bool keepGuard = false;
            try
            {
                keepGuard = true;
                return true;
            }
            finally
            {
                if (!keepGuard)
                {
                    guardVault.ReleaseMutationGuard(guardMask);
                    guardVault = null;
                }
            }
        }

        private static void ReleaseNavGridMutationGuard(IDataVault vault, ulong guardMask)
        {
            if (vault != null && guardMask != 0UL)
                vault.ReleaseMutationGuard(guardMask);
        }

        private static ulong NavGridMutationGuardBit<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID == (uint)BufferID.Unknown
                ? 0UL
                : 1UL << (unchecked((int)handle.BufferID) & 31);
        }

        private static bool TryResolveNavGridMutable<T>(
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            return TryResolveNavGridRead(in handle, requiredLength, out buffer);
        }

        private static bool TryResolveNavGridRead<T>(
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null || requiredLength <= 0)
                return false;

            if (vault.IsCompactionFenceActive)
                return false;

            if (!IsVaultHandleCreated(in handle))
                return false;

            if (!vault.TryResolveHandle(in handle, out NativeArray<T> resolved) ||
                !resolved.IsCreated ||
                resolved.Length < requiredLength ||
                vault.IsCompactionFenceActive)
            {
                return false;
            }

            buffer = resolved;
            return true;
        }

        private static bool TryReadOnlyNavGrid<T>(
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   requiredLength > 0 &&
                   !vault.IsCompactionFenceActive &&
                   IsVaultHandleCreated(in handle) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength &&
                   !vault.IsCompactionFenceActive;
        }

        private static void ReleaseNavGridBuffer<T>(ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault != null && IsVaultHandleCreated(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static void SwapHandles<T>(ref VaultGenerationHandle<T> first, ref VaultGenerationHandle<T> second)
            where T : struct
        {
            VaultGenerationHandle<T> swap = first;
            first = second;
            second = swap;
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

            record.IsPureVoid = true;
            record.PortalsReady = true;
            record.PortalCount = 0;
        }

        private static bool IsPureVoidSnapshot(VolumeRecord record)
        {
            if (!HasValidRecordBounds(record))
            {
                return false;
            }

            if (!TryResolveVoxelCellCount(record.Dimensions, out int requiredCellCount))
                return false;

            if (!TryReadOnlyNavGrid(in record.CurrentHandle, requiredCellCount, out NativeArray<byte>.ReadOnly current) ||
                !TryReadOnlyNavGrid(in record.CurrentDistanceHandle, requiredCellCount, out NativeArray<ushort>.ReadOnly currentDistance))
            {
                return false;
            }

            for (int i = 0; i < requiredCellCount; i++)
            {
                if (current[i] != OpenCell ||
                    currentDistance[i] != ushort.MaxValue)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ReleaseVoxelBuffers(VolumeRecord record)
        {
            if (record.HasPendingDynamicUpdate)
                return false;

            ReleaseNavGridMutationGuard(record.PendingBuildMutationGuardVault, record.PendingBuildMutationGuardMask);
            ReleaseNavGridMutationGuard(record.PendingDynamicMutationGuardVault, record.PendingDynamicMutationGuardMask);
            ReleaseObstacleSnapshotLease(record.PendingBuildObstacleSnapshotLease);
            ReleaseObstacleSnapshotLease(record.PendingDynamicObstacleSnapshotLease);
            record.PendingBuildRuntimeStamp = 0;
            record.PendingBuildObstacleSnapshotLease = -1;
            record.PendingBuildObstacleSnapshotCount = 0;
            record.PendingDynamicObstacleSnapshotLease = -1;
            record.PendingDynamicObstacleSnapshotCount = 0;
            record.PendingBuildMutationGuardMask = 0UL;
            record.PendingDynamicMutationGuardMask = 0UL;
            record.PendingBuildMutationGuardVault = null;
            record.PendingDynamicMutationGuardVault = null;
            ReleaseNavGridBuffer(ref record.CurrentHandle);
            ReleaseNavGridBuffer(ref record.NextHandle);
            ReleaseNavGridBuffer(ref record.BaseCurrentHandle);
            ReleaseNavGridBuffer(ref record.BaseNextHandle);
            ReleaseNavGridBuffer(ref record.CurrentDistanceHandle);
            ReleaseNavGridBuffer(ref record.NextDistanceHandle);
            return true;
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
                    out NativeArray<int>.ReadOnly underwaterSemanticTypes,
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
                    out NativeArray<int>.ReadOnly surfaceSemanticTypes,
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
            NativeArray<int>.ReadOnly semanticTypes,
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
                vegetationBridge.TryGetActiveUnderwaterSemanticPayload(out NativeArray<int>.ReadOnly underwaterSemanticTypes, out _, out int underwaterSemanticCount))
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
                vegetationBridge.TryGetActiveSurfaceSemanticPayload(out NativeArray<int>.ReadOnly surfaceSemanticTypes, out _, out int surfaceSemanticCount))
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
            if (!snapshot.IsCreated)
                return;

            int capacity = snapshot.Length;
            for (int i = 0; i < _persistentDynamicObstacleCount && writeIndex < capacity; i++)
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
            if (_persistentDynamicObstacleCount <= 0)
                return 0;

            int count = 0;
            for (int i = 0; i < _persistentDynamicObstacleCount; i++)
            {
                NavObstaclePrimitive obstacle = _persistentDynamicObstacles[i];
                if (IsValidDynamicObstacleBounds(obstacle.Center, obstacle.Extents))
                    count++;
            }

            return count;
        }

        private static void ClearInvalidObstacleTail(NativeArray<NavObstaclePrimitive> snapshot, int startIndex)
        {
            if (!snapshot.IsCreated)
                return;

            int first = math.clamp(startIndex, 0, snapshot.Length);
            for (int i = first; i < snapshot.Length; i++)
            {
                snapshot[i] = new NavObstaclePrimitive
                {
                    Center = float3.zero,
                    Extents = new float3(-1f, -1f, -1f)
                };
            }
        }

        private static void RegisterPersistentDynamicObstacle(float3 center, float3 extents)
        {
            if (!IsValidDynamicObstacleBounds(center, extents))
            {
                return;
            }

            float mergeDistanceSq = PersistentObstacleMergeDistanceMeters * PersistentObstacleMergeDistanceMeters;
            for (int i = 0; i < _persistentDynamicObstacleCount; i++)
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

            if (_persistentDynamicObstacleCount < MaxPersistentDynamicObstacleCount)
            {
                _persistentDynamicObstacles[_persistentDynamicObstacleCount] = new NavObstaclePrimitive
                {
                    Center = center,
                    Extents = extents
                };
                _persistentDynamicObstacleCount++;
                return;
            }

            int writeIndex = math.clamp(_persistentDynamicObstacleWriteCursor, 0, MaxPersistentDynamicObstacleCount - 1);
            _persistentDynamicObstacles[writeIndex] = new NavObstaclePrimitive
            {
                Center = center,
                Extents = extents
            };
            int nextWriteIndex = writeIndex + 1;
            _persistentDynamicObstacleWriteCursor = nextWriteIndex >= MaxPersistentDynamicObstacleCount
                ? 0
                : nextWriteIndex;
        }

        private static void RemovePersistentDynamicObstacles(float3 center, float3 extents)
        {
            if (_persistentDynamicObstacleCount <= 0 ||
                !IsValidDynamicObstacleBounds(center, extents))
            {
                return;
            }

            float removeRadius = math.max(
                PersistentObstacleMergeDistanceMeters,
                math.max(extents.x, math.max(extents.y, extents.z)) + PersistentObstacleMergeDistanceMeters);
            float removeRadiusSq = removeRadius * removeRadius;
            for (int i = _persistentDynamicObstacleCount - 1; i >= 0; i--)
            {
                NavObstaclePrimitive obstacle = _persistentDynamicObstacles[i];
                if (!IsValidDynamicObstacleBounds(obstacle.Center, obstacle.Extents))
                {
                    RemovePersistentDynamicObstacleAt(i);
                    continue;
                }

                if (math.lengthsq(obstacle.Center - center) > removeRadiusSq)
                    continue;

                RemovePersistentDynamicObstacleAt(i);
            }
        }

        private static void RemovePersistentDynamicObstacleAt(int index)
        {
            if ((uint)index >= (uint)_persistentDynamicObstacleCount)
                return;

            int lastIndex = _persistentDynamicObstacleCount - 1;
            _persistentDynamicObstacles[index] = _persistentDynamicObstacles[lastIndex];
            _persistentDynamicObstacles[lastIndex] = default;
            _persistentDynamicObstacleCount = lastIndex;
            if (_persistentDynamicObstacleWriteCursor >= _persistentDynamicObstacleCount)
                _persistentDynamicObstacleWriteCursor = 0;
        }

        private static void WriteMacroFloraObstacles(
            NativeArray<Matrix4x4> matrices,
            NativeArray<HectonVegetationInstanceData> metadata,
            NativeArray<int> types,
            NativeArray<int>.ReadOnly semanticTypes,
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

            return TryResolveVoxelCellCount(record.Dimensions, out int requiredCellCount) &&
                   HasNavGridBufferLength(in record.CurrentHandle, requiredCellCount);
        }

        private static bool HasCompleteDynamicUpdateBuffers(VolumeRecord record)
        {
            if (!HasValidRecordBounds(record))
            {
                return false;
            }

            if (!TryResolveVoxelCellCount(record.Dimensions, out int requiredCellCount))
                return false;

            return HasNavGridBufferLength(in record.NextHandle, requiredCellCount) &&
                   HasNavGridBufferLength(in record.BaseCurrentHandle, requiredCellCount) &&
                   HasNavGridBufferLength(in record.CurrentDistanceHandle, requiredCellCount) &&
                   HasNavGridBufferLength(in record.NextDistanceHandle, requiredCellCount);
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
            int routeNodeCount = _routePathCount;
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
                if (nodeIndex < 0 || nodeIndex >= _portalGraphNodeCount)
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

            _portalGraphNodeCount = 0;
            for (int recordIndex = 0; recordIndex < _recordCount && _portalGraphNodeCount < MaxPortalGraphNodeCapacity; recordIndex++)
            {
                VolumeRecord record = _records[recordIndex];
                if (record != null)
                    record.PortalCount = 0;

                AppendRecordPortalsToGraph(record);
            }

            for (int portalIndex = 0; portalIndex < _portalGraphNodeCount; portalIndex++)
            {
                PortalNode current = _portalGraphNodes[portalIndex];
                if (!IsValidPortalNode(in current))
                    continue;

                int bestMatchIndex = InvalidPortalIndex;
                float bestMatchScore = float.MaxValue;
                for (int candidateIndex = 0; candidateIndex < _portalGraphNodeCount; candidateIndex++)
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

        private static void AppendRecordPortalsToGraph(VolumeRecord record)
        {
            if (!HasValidRecordBounds(record) ||
                !record.PortalsReady ||
                record.IsPureVoid ||
                record.Dimensions.x <= 1 ||
                record.Dimensions.y <= 1 ||
                record.Dimensions.z <= 1 ||
                _portalGraphNodeCount >= MaxPortalGraphNodeCapacity ||
                !EnsurePortalWorkCapacity(record) ||
                !TryResolveVoxelCellCount(record.Dimensions, out int requiredCellCount) ||
                !TryReadOnlyNavGrid(in record.CurrentHandle, requiredCellCount, out NativeArray<byte>.ReadOnly current))
            {
                return;
            }

            for (byte face = 0; face < FaceCount && _portalGraphNodeCount < MaxPortalGraphNodeCapacity; face++)
            {
                GetFaceDimensions(record.Dimensions, face, out int width, out int height);
                int faceCellCount = width * height;
                if (faceCellCount <= 0 || faceCellCount > _portalFaceVisitScratch.Length)
                    continue;

                int visitStamp = NextPortalFaceVisitStamp();
                for (int faceIndex = 0; faceIndex < faceCellCount && _portalGraphNodeCount < MaxPortalGraphNodeCapacity; faceIndex++)
                {
                    if (_portalFaceVisitScratch[faceIndex] == visitStamp ||
                        !IsFaceCellOpen(record, current, face, faceIndex, width))
                    {
                        continue;
                    }

                    PortalNode portal = ExtractFacePortal(record, current, face, faceIndex, width, height, visitStamp);
                    if (!IsValidPortalNode(in portal))
                        continue;

                    portal.ConnectedPortalIndex = InvalidPortalIndex;
                    _portalGraphNodes[_portalGraphNodeCount] = portal;
                    _portalGraphNodeCount++;
                    record.PortalCount++;
                }
            }
        }

        private static int NextPortalFaceVisitStamp()
        {
            _portalFaceVisitStamp++;
            if (_portalFaceVisitStamp == int.MaxValue)
            {
                System.Array.Clear(_portalFaceVisitScratch, 0, _portalFaceVisitScratch.Length);
                _portalFaceVisitStamp = 1;
            }

            return _portalFaceVisitStamp;
        }

        private static bool TryResolveRecord(float3 worldPosition, out VolumeRecord record)
        {
            record = null;
            if (!math.all(math.isfinite(worldPosition)))
                return false;

            float nearestDistanceSq = float.MaxValue;
            for (int recordIndex = 0; recordIndex < _recordCount; recordIndex++)
            {
                VolumeRecord candidate = _records[recordIndex];
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

            for (int recordIndex = 0; recordIndex < _recordCount; recordIndex++)
            {
                VolumeRecord candidate = _records[recordIndex];
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

            record.PortalCount = 0;
            if (!HasValidRecordBounds(record) ||
                record.Dimensions.x <= 1 ||
                record.Dimensions.y <= 1 ||
                record.Dimensions.z <= 1)
            {
                record.PortalsReady = false;
                return;
            }

            if (!EnsurePortalWorkCapacity(record))
            {
                record.PortalsReady = false;
                return;
            }

            record.PortalsReady = true;
            _portalGraphDirty = true;
        }

        private static PortalNode ExtractFacePortal(
            VolumeRecord record,
            NativeArray<byte>.ReadOnly current,
            byte face,
            int seedFaceIndex,
            int width,
            int height,
            int visitStamp)
        {
            int queueHead = 0;
            int queueTail = 0;
            _portalFaceQueueScratch[queueTail++] = seedFaceIndex;
            _portalFaceVisitScratch[seedFaceIndex] = visitStamp;

            float3 sum = float3.zero;
            int cellCount = 0;
            int minU = int.MaxValue;
            int minV = int.MaxValue;
            int maxU = int.MinValue;
            int maxV = int.MinValue;
            while (queueHead < queueTail)
            {
                int faceIndex = _portalFaceQueueScratch[queueHead++];
                int u = faceIndex % width;
                int v = faceIndex / width;
                int3 voxel = ResolveFaceVoxel(record.Dimensions, face, u, v);
                sum += record.Origin + (new float3(voxel.x, voxel.y, voxel.z) * record.CellSize);
                cellCount++;
                minU = math.min(minU, u);
                minV = math.min(minV, v);
                maxU = math.max(maxU, u);
                maxV = math.max(maxV, v);

                QueueFaceNeighbor(record, current, face, u - 1, v, width, height, visitStamp, ref queueTail);
                QueueFaceNeighbor(record, current, face, u + 1, v, width, height, visitStamp, ref queueTail);
                QueueFaceNeighbor(record, current, face, u, v - 1, width, height, visitStamp, ref queueTail);
                QueueFaceNeighbor(record, current, face, u, v + 1, width, height, visitStamp, ref queueTail);
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

        private static void QueueFaceNeighbor(
            VolumeRecord record,
            NativeArray<byte>.ReadOnly current,
            byte face,
            int u,
            int v,
            int width,
            int height,
            int visitStamp,
            ref int queueTail)
        {
            if (u < 0 || v < 0 || u >= width || v >= height)
                return;

            int faceIndex = u + (v * width);
            if (_portalFaceVisitScratch[faceIndex] == visitStamp ||
                !IsFaceCellOpen(record, current, face, faceIndex, width))
            {
                return;
            }

            if (queueTail >= _portalFaceQueueScratch.Length)
                return;

            _portalFaceVisitScratch[faceIndex] = visitStamp;
            _portalFaceQueueScratch[queueTail++] = faceIndex;
        }

        private static bool TrySolvePortalRoute(VolumeRecord startRecord, VolumeRecord endRecord, float3 startWorldPosition, float3 endWorldPosition)
        {
            int nodeCount = _portalGraphNodeCount;
            if (nodeCount <= 0 ||
                !HasValidRecordBounds(startRecord) ||
                !HasValidRecordBounds(endRecord) ||
                !math.all(math.isfinite(startWorldPosition)) ||
                !math.all(math.isfinite(endWorldPosition)) ||
                !EnsureRouteNodeCapacity(nodeCount))
            {
                return false;
            }

            _routeOpenSetCount = 0;
            _routePathCount = 0;
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
                if (_routeOpenSetCount >= _routeOpenSetScratch.Length)
                    return false;

                _routeOpenSetScratch[_routeOpenSetCount] = nodeIndex;
                _routeOpenSetCount++;
            }

            while (_routeOpenSetCount > 0)
            {
                int currentNodeIndex = PopLowestCostOpenNode();
                if (currentNodeIndex < 0 || currentNodeIndex >= _portalGraphNodeCount)
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
                currentNodeIndex >= _portalGraphNodeCount ||
                !math.isfinite(currentGScore))
            {
                return;
            }

            PortalNode currentNode = _portalGraphNodes[currentNodeIndex];
            if (!IsValidPortalNode(in currentNode))
                return;

            if (currentNode.ConnectedPortalIndex >= 0)
                RelaxPortalEdge(currentNodeIndex, currentNode.ConnectedPortalIndex, currentGScore, endWorldPosition);

            for (int candidateIndex = 0; candidateIndex < _portalGraphNodeCount; candidateIndex++)
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
                currentNodeIndex >= _portalGraphNodeCount ||
                candidateIndex >= _portalGraphNodeCount ||
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
                if (_routeOpenSetCount >= _routeOpenSetScratch.Length)
                    return;

                candidateState.Flags |= 1;
                _routeOpenSetScratch[_routeOpenSetCount] = candidateIndex;
                _routeOpenSetCount++;
            }

            _routeNodeScratch[candidateIndex] = candidateState;
        }

        private static int PopLowestCostOpenNode()
        {
            int bestListIndex = InvalidPortalIndex;
            float bestScore = float.MaxValue;
            for (int listIndex = 0; listIndex < _routeOpenSetCount; listIndex++)
            {
                int nodeIndex = _routeOpenSetScratch[listIndex];
                if (nodeIndex < 0 || nodeIndex >= _routeNodeScratchCount)
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
                _routeOpenSetCount = 0;
                return InvalidPortalIndex;
            }

            int selectedNodeIndex = _routeOpenSetScratch[bestListIndex];
            int lastListIndex = _routeOpenSetCount - 1;
            _routeOpenSetScratch[bestListIndex] = _routeOpenSetScratch[lastListIndex];
            _routeOpenSetCount--;
            return selectedNodeIndex;
        }

        private static bool ReconstructRoute(int endNodeIndex)
        {
            _routePathCount = 0;
            int currentIndex = endNodeIndex;
            int iterationCount = 0;
            while (currentIndex >= 0 &&
                   currentIndex < _routeNodeScratchCount &&
                   _routePathCount < _routePathScratch.Length &&
                   iterationCount < MaxPortalGraphNodeCapacity)
            {
                if (currentIndex >= _portalGraphNodeCount)
                {
                    _routePathCount = 0;
                    return false;
                }

                PortalNode node = _portalGraphNodes[currentIndex];
                if (!IsValidPortalNode(in node))
                {
                    _routePathCount = 0;
                    return false;
                }

                _routePathScratch[_routePathCount] = currentIndex;
                _routePathCount++;
                currentIndex = _routeNodeScratch[currentIndex].ParentIndex;
                iterationCount++;
            }

            if (currentIndex >= 0 || _routePathCount <= 0)
            {
                _routePathCount = 0;
                return false;
            }

            return true;
        }

        private static bool IsFaceCellOpen(
            VolumeRecord record,
            NativeArray<byte>.ReadOnly current,
            byte face,
            int faceIndex,
            int width)
        {
            int u = faceIndex % width;
            int v = faceIndex / width;
            int3 voxel = ResolveFaceVoxel(record.Dimensions, face, u, v);
            int flatIndex = voxel.x + (voxel.y * record.Dimensions.x) + (voxel.z * record.Dimensions.x * record.Dimensions.y);
            return flatIndex >= 0 &&
                   flatIndex < current.Length &&
                   current[flatIndex] == OpenCell;
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

            if (!TryResolveVoxelCellCount(record.Dimensions, out int requiredCellCount) ||
                !TryReadOnlyNavGrid(in record.CurrentHandle, requiredCellCount, out NativeArray<byte>.ReadOnly current))
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
            if (flatIndex < 0 || flatIndex >= current.Length)
                return false;

            voxel = candidate;
            passability = current[flatIndex];
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
                record.Dimensions.z <= 1)
            {
                return false;
            }

            if (!TryResolveMaxFaceCells(record.Dimensions, out int maxFaceCells))
                return false;

            return maxFaceCells > 0 &&
                   maxFaceCells <= _portalFaceVisitScratch.Length &&
                   maxFaceCells <= _portalFaceQueueScratch.Length &&
                   _portalGraphNodes.Length > 0;
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
            if (radiusMeters <= 0f || _recordCount <= 0)
                return;

            float radiusSq = radiusMeters * radiusMeters;
            float3 playerPosition = new float3(playerRuntimePosition.x, playerRuntimePosition.y, playerRuntimePosition.z);
            Color previousColor = Gizmos.color;
            Gizmos.color = new Color(0.1f, 0.65f, 0.95f, 0.45f);

            int drawnCells = 0;
            for (int recordIndex = 0; recordIndex < _recordCount && drawnCells < 2048; recordIndex++)
            {
                VolumeRecord record = _records[recordIndex];
                if (record == null ||
                    record.IsPureVoid ||
                    record.Dimensions.x <= 0 ||
                    record.Dimensions.y <= 0 ||
                    record.Dimensions.z <= 0)
                {
                    continue;
                }

                if (!TryResolveVoxelCellCount(record.Dimensions, out int requiredCellCount) ||
                    !TryReadOnlyNavGrid(in record.CurrentHandle, requiredCellCount, out NativeArray<byte>.ReadOnly current))
                {
                    continue;
                }

                int pointCount = math.min(current.Length, record.Dimensions.x * record.Dimensions.y * record.Dimensions.z);
                float cellSize = math.max(record.CellSize, 0.05f);
                Vector3 wireSize = Vector3.one * math.min(cellSize * 0.32f, 0.32f);
                int width = record.Dimensions.x;
                int slice = record.Dimensions.x * record.Dimensions.y;
                for (int flatIndex = 0; flatIndex < pointCount && drawnCells < 2048; flatIndex++)
                {
                    if (current[flatIndex] != OpenCell)
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
            if (requiredCount < 0 || requiredCount > _routeNodeScratch.Length)
                return false;

            while (_routeNodeScratchCount < requiredCount)
            {
                _routeNodeScratch[_routeNodeScratchCount] = default;
                _routeNodeScratchCount++;
            }

            return true;
        }
    }

}
