using System.Collections.Generic;
using Hecton8.Caves;
using Hecton8.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
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

        // COLD ALLOC: Dictionary<int, VolumeRecord>(16) - voxel navgrid snapshots keyed by runtime volume instance ID - owner: VoxelDynamicNavGridRuntime
        private static readonly Dictionary<int, VolumeRecord> _records = new Dictionary<int, VolumeRecord>(16);
        // COLD ALLOC: List<DirtyVolumeRequest>(32) - temporary dirty-volume spill buffer while consuming queue entries - owner: VoxelDynamicNavGridRuntime
        private static readonly List<DirtyVolumeRequest> _dirtyRequestSpill = new List<DirtyVolumeRequest>(32);
        // COLD ALLOC: List<PortalNode>(128) - rebuilt macro portal graph nodes spanning all active navgrid chunks - owner: VoxelDynamicNavGridRuntime
        private static readonly List<PortalNode> _portalGraphNodes = new List<PortalNode>(128);
        // COLD ALLOC: List<RouteNodeState>(128) - reusable portal A* node state scratch - owner: VoxelDynamicNavGridRuntime
        private static readonly List<RouteNodeState> _routeNodeScratch = new List<RouteNodeState>(128);
        // COLD ALLOC: List<int>(128) - reusable portal A* open-set scratch - owner: VoxelDynamicNavGridRuntime
        private static readonly List<int> _routeOpenSetScratch = new List<int>(128);
        // COLD ALLOC: List<int>(128) - reusable portal route reconstruction scratch - owner: VoxelDynamicNavGridRuntime
        private static readonly List<int> _routePathScratch = new List<int>(128);
        // COLD ALLOC: Dictionary<int,ObstacleRegistration>(64) - registered habitat obstacle collider sources - owner: VoxelDynamicNavGridRuntime
        private static readonly Dictionary<int, ObstacleRegistration> _registeredObstacles = new Dictionary<int, ObstacleRegistration>(64);

        private static NativeQueue<DirtyVolumeRequest> _dirtyVolumes;
        private static bool _portalGraphDirty = true;

        internal enum HybridNavigationMode : byte
        {
            OpenWaterHeightmap = 0,
            CaveVoxel = 1,
            SolidVoxel = 2,
        }

        [BurstCompile(FloatMode = FloatMode.Fast)]
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

        [BurstCompile(FloatMode = FloatMode.Fast)]
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

        [BurstCompile(FloatMode = FloatMode.Fast)]
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

        private struct DirtyVolumeRequest
        {
            public int VolumeInstanceId;
            public int RuntimeStamp;
        }

        private struct PortalNode
        {
            public uint ChunkId;
            public float3 Centroid;
            public float Radius;
            public int ConnectedPortalIndex;
            public byte Face;
        }

        private struct RouteNodeState
        {
            public float GScore;
            public float FScore;
            public int ParentIndex;
            public byte Flags;
        }

        internal struct NavObstaclePrimitive
        {
            public float3 Center;
            public float3 Extents;
        }

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
            public NativeArray<byte> Current;
            public NativeArray<byte> Next;
            public PortalNode[] Portals = System.Array.Empty<PortalNode>();
            public int PortalCount;
            public int[] FaceVisitScratch = System.Array.Empty<int>();
            public int[] FaceQueueScratch = System.Array.Empty<int>();
            public int FaceVisitStamp;

            public void Dispose()
            {
                if (Current.IsCreated)
                    Current.Dispose();

                if (Next.IsCreated)
                    Next.Dispose();

                Current = default;
                Next = default;
                IsDirty = false;
                RuntimeStamp = 0;
                Dimensions = int3.zero;
                Origin = float3.zero;
                Max = float3.zero;
                CellSize = 0f;
                PortalCount = 0;
                FaceVisitStamp = 0;
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
            record.IsDirty = true;
            record.RuntimeStamp = volume.RuntimeStamp;
            _dirtyVolumes.Enqueue(new DirtyVolumeRequest
            {
                VolumeInstanceId = volumeInstanceId,
                RuntimeStamp = volume.RuntimeStamp
            });
        }

        internal static bool TryPrepareBuild(
            HectonVoxelVolume volume,
            int runtimeStamp,
            int3 dimensions,
            float3 origin,
            float cellSize,
            int pointCount,
            out NativeArray<byte> outputBuffer)
        {
            outputBuffer = default;
            if (volume == null ||
                pointCount <= 0 ||
                dimensions.x <= 0 ||
                dimensions.y <= 0 ||
                dimensions.z <= 0)
            {
                return false;
            }

            EnsureInitialized();
            int volumeInstanceId = GetStableVolumeEntityId(volume);
            VolumeRecord record = GetOrCreateRecord(volumeInstanceId);
            bool consumedDirtyMarker = ConsumeDirtyMarker(volumeInstanceId, runtimeStamp);
            bool dimensionsChanged = !math.all(record.Dimensions == dimensions);
            bool originChanged = math.lengthsq(record.Origin - origin) > 0.0001f;
            bool cellSizeChanged = math.abs(record.CellSize - cellSize) > 0.0001f;
            bool needsBuild = consumedDirtyMarker ||
                              record.IsDirty ||
                              !record.Current.IsCreated ||
                              !record.Next.IsCreated ||
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

            EnsureBuffer(ref record.Current, pointCount);
            EnsureBuffer(ref record.Next, pointCount);

            if (!needsBuild)
                return false;

            outputBuffer = record.Next;
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
            RebuildPortals(record);
            _portalGraphDirty = true;
        }

        internal static void RegisterModuleObstacle(int obstacleId, BoxCollider[] boxes, CapsuleCollider[] capsules)
        {
            if (obstacleId == 0)
                return;

            ObstacleRegistration registration = null;
            if (!_registeredObstacles.TryGetValue(obstacleId, out registration))
            {
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

            if (obstacleCount <= 0)
                return default;

            NativeArray<NavObstaclePrimitive> snapshot = new NativeArray<NavObstaclePrimitive>(obstacleCount, allocator, NativeArrayOptions.UninitializedMemory);
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

            return snapshot;
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
                !record.Current.IsCreated)
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
                startRecord == endRecord)
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

            int requiredWaypointCount = _routePathScratch.Count + 2;
            if (requiredWaypointCount > outputWaypoints.Length)
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
                record == null ||
                !record.Current.IsCreated)
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

            bool foundContainingRecord = false;
            float nearestDistanceSq = float.MaxValue;
            VolumeRecord nearestRecord = null;
            Dictionary<int, VolumeRecord>.Enumerator enumerator = _records.GetEnumerator();
            while (enumerator.MoveNext())
            {
                VolumeRecord candidate = enumerator.Current.Value;
                if (candidate == null ||
                    !candidate.Current.IsCreated ||
                    candidate.Dimensions.x <= 0 ||
                    candidate.Dimensions.y <= 0 ||
                    candidate.Dimensions.z <= 0 ||
                    candidate.CellSize <= 0f)
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

            if (nearestRecord == null)
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
            sample.FloorBoundaryY = worldPosition.y;
            HectonMapMagicVegetationBridge activeBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (activeBridge != null &&
                activeBridge.TryGetCachedTerrainHeight(worldPosition.x, worldPosition.z, out float terrainHeight))
            {
                sample.TerrainHeight = terrainHeight;
                sample.FloorBoundaryY = terrainHeight;
                sample.HasTerrainHeight = 1;
            }

            if (!TryResolveContainingRecord(worldPosition, out VolumeRecord record) ||
                record == null ||
                !record.Current.IsCreated)
            {
                return sample.HasTerrainHeight != 0;
            }

            if (!TrySamplePassabilityCell(record, worldPosition, out int3 voxel, out byte passability))
                return false;

            sample.Passability = passability;
            sample.CellSize = record.CellSize;
            sample.CellOrigin = record.Origin + (new float3(voxel.x, voxel.y, voxel.z) * record.CellSize);
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
                startRecord == endRecord)
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

            outputWaypoints.Clear();
            outputWaypoints.Add(new Vector3(startWorldPosition.x, startWorldPosition.y, startWorldPosition.z));
            for (int i = _routePathScratch.Count - 1; i >= 0; i--)
            {
                PortalNode node = _portalGraphNodes[_routePathScratch[i]];
                outputWaypoints.Add(new Vector3(node.Centroid.x, node.Centroid.y, node.Centroid.z));
            }

            outputWaypoints.Add(new Vector3(endWorldPosition.x, endWorldPosition.y, endWorldPosition.z));
            return outputWaypoints.Length >= 2;
        }

        internal static void UnregisterVolume(HectonVoxelVolume volume)
        {
            if (volume == null)
                return;

            int volumeInstanceId = GetStableVolumeEntityId(volume);
            if (_records.TryGetValue(volumeInstanceId, out VolumeRecord record))
            {
                record.Dispose();
                _records.Remove(volumeInstanceId);
                _portalGraphDirty = true;
            }
        }

        internal static void DisposeAll()
        {
            foreach (KeyValuePair<int, VolumeRecord> pair in _records)
            {
                pair.Value.Dispose();
            }

            _records.Clear();
            _dirtyRequestSpill.Clear();
            _portalGraphNodes.Clear();
            _routeNodeScratch.Clear();
            _routeOpenSetScratch.Clear();
            _routePathScratch.Clear();
            _registeredObstacles.Clear();
            _portalGraphDirty = true;
            if (_dirtyVolumes.IsCreated)
            {
                _dirtyVolumes.Dispose();
                _dirtyVolumes = default;
            }
        }

        private static void EnsureInitialized()
        {
            if (_dirtyVolumes.IsCreated)
                return;

            _dirtyVolumes = new NativeQueue<DirtyVolumeRequest>(Allocator.Persistent); // COLD ALLOC: NativeQueue<DirtyVolumeRequest>[32] - dirty voxel volume rebuild requests - owner: VoxelDynamicNavGridRuntime
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
            return math.max(1, (int)math.ceil(DefaultPredatorClearanceRadiusMeters / math.max(cellSize, 0.0001f)));
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
            while (_dirtyVolumes.TryDequeue(out DirtyVolumeRequest request))
            {
                if (!found &&
                    request.VolumeInstanceId == volumeInstanceId &&
                    request.RuntimeStamp == runtimeStamp)
                {
                    found = true;
                    continue;
                }

                _dirtyRequestSpill.Add(request);
            }

            for (int i = 0; i < _dirtyRequestSpill.Count; i++)
            {
                _dirtyVolumes.Enqueue(_dirtyRequestSpill[i]);
            }

            _dirtyRequestSpill.Clear();
            return found;
        }

        private static void EnsureBuffer(ref NativeArray<byte> buffer, int length)
        {
            if (buffer.IsCreated && buffer.Length == length)
                return;

            if (buffer.IsCreated)
                buffer.Dispose();

            buffer = new NativeArray<byte>(length, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[pointCount] - double-buffered voxel passability snapshot - owner: VoxelDynamicNavGridRuntime
        }

        private static int CountLiveColliders<T>(T[] colliders)
            where T : Collider
        {
            if (colliders == null || colliders.Length <= 0)
                return 0;

            int count = 0;
            for (int i = 0; i < colliders.Length; i++)
            {
                T collider = colliders[i];
                if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
                    count++;
            }

            return count;
        }

        private static void WriteColliderBounds<T>(T[] colliders, ref NativeArray<NavObstaclePrimitive> snapshot, ref int writeIndex)
            where T : Collider
        {
            if (colliders == null || colliders.Length <= 0)
                return;

            for (int i = 0; i < colliders.Length; i++)
            {
                T collider = colliders[i];
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                    continue;

                Bounds bounds = collider.bounds;
                snapshot[writeIndex] = new NavObstaclePrimitive
                {
                    Center = bounds.center,
                    Extents = bounds.extents
                };
                writeIndex++;
            }
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

        private static void EnsurePortalGraphBuilt()
        {
            if (!_portalGraphDirty)
                return;

            _portalGraphNodes.Clear();
            Dictionary<int, VolumeRecord>.Enumerator enumerator = _records.GetEnumerator();
            while (enumerator.MoveNext())
            {
                VolumeRecord record = enumerator.Current.Value;
                if (record == null || record.PortalCount <= 0)
                    continue;

                for (int portalIndex = 0; portalIndex < record.PortalCount; portalIndex++)
                {
                    PortalNode portal = record.Portals[portalIndex];
                    portal.ConnectedPortalIndex = InvalidPortalIndex;
                    record.Portals[portalIndex] = portal;
                    _portalGraphNodes.Add(portal);
                }
            }

            for (int portalIndex = 0; portalIndex < _portalGraphNodes.Count; portalIndex++)
            {
                PortalNode current = _portalGraphNodes[portalIndex];
                int bestMatchIndex = InvalidPortalIndex;
                float bestMatchScore = float.MaxValue;
                for (int candidateIndex = 0; candidateIndex < _portalGraphNodes.Count; candidateIndex++)
                {
                    if (candidateIndex == portalIndex)
                        continue;

                    PortalNode candidate = _portalGraphNodes[candidateIndex];
                    if (candidate.ChunkId == current.ChunkId || !AreOppositeFaces(current.Face, candidate.Face))
                        continue;

                    float centroidDistanceSq = math.lengthsq(current.Centroid - candidate.Centroid);
                    float maxJoinDistance = math.max(current.Radius + candidate.Radius + BoundsMatchEpsilon, BoundsMatchEpsilon);
                    if (centroidDistanceSq > maxJoinDistance * maxJoinDistance)
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
            float nearestDistanceSq = float.MaxValue;
            Dictionary<int, VolumeRecord>.Enumerator enumerator = _records.GetEnumerator();
            while (enumerator.MoveNext())
            {
                VolumeRecord candidate = enumerator.Current.Value;
                if (candidate == null || !candidate.Current.IsCreated)
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

            return record != null && nearestDistanceSq <= math.max(record.CellSize * record.CellSize, 1f);
        }

        private static bool TryResolveContainingRecord(float3 worldPosition, out VolumeRecord record)
        {
            record = null;
            Dictionary<int, VolumeRecord>.Enumerator enumerator = _records.GetEnumerator();
            while (enumerator.MoveNext())
            {
                VolumeRecord candidate = enumerator.Current.Value;
                if (candidate == null ||
                    !candidate.Current.IsCreated ||
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
            if (record == null ||
                !record.Current.IsCreated ||
                record.Dimensions.x <= 1 ||
                record.Dimensions.y <= 1 ||
                record.Dimensions.z <= 1)
            {
                record.PortalCount = 0;
                return;
            }

            int maxFaceCells = math.max(
                record.Dimensions.x * record.Dimensions.y,
                math.max(record.Dimensions.x * record.Dimensions.z, record.Dimensions.y * record.Dimensions.z));
            EnsureManagedBuffer(ref record.FaceVisitScratch, maxFaceCells);
            EnsureManagedBuffer(ref record.FaceQueueScratch, maxFaceCells);
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
                    if (portal.Radius <= 0f)
                        continue;

                    EnsurePortalCapacity(record, record.PortalCount + 1);
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
            return new PortalNode
            {
                ChunkId = record.ChunkId,
                Centroid = sum / cellCount,
                Radius = math.max(record.CellSize * 0.5f, math.max(faceSpanU, faceSpanV) * 0.5f),
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

            record.FaceVisitScratch[faceIndex] = record.FaceVisitStamp;
            record.FaceQueueScratch[queueTail++] = faceIndex;
        }

        private static bool TrySolvePortalRoute(VolumeRecord startRecord, VolumeRecord endRecord, float3 startWorldPosition, float3 endWorldPosition)
        {
            int nodeCount = _portalGraphNodes.Count;
            EnsureRouteNodeCapacity(nodeCount);
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
                if (node.ChunkId != startRecord.ChunkId)
                    continue;

                RouteNodeState state = _routeNodeScratch[nodeIndex];
                state.GScore = math.distance(startWorldPosition, node.Centroid);
                state.FScore = state.GScore + math.distance(node.Centroid, endWorldPosition);
                state.ParentIndex = InvalidPortalIndex;
                state.Flags = 1;
                _routeNodeScratch[nodeIndex] = state;
                _routeOpenSetScratch.Add(nodeIndex);
            }

            while (_routeOpenSetScratch.Count > 0)
            {
                int currentNodeIndex = PopLowestCostOpenNode();
                RouteNodeState currentState = _routeNodeScratch[currentNodeIndex];
                currentState.Flags = 2;
                _routeNodeScratch[currentNodeIndex] = currentState;

                PortalNode currentNode = _portalGraphNodes[currentNodeIndex];
                if (currentNode.ChunkId == endRecord.ChunkId)
                {
                    ReconstructRoute(currentNodeIndex);
                    return _routePathScratch.Count > 0;
                }

                RelaxPortalNeighbors(currentNodeIndex, currentState.GScore, endWorldPosition);
            }

            return false;
        }

        private static void RelaxPortalNeighbors(int currentNodeIndex, float currentGScore, float3 endWorldPosition)
        {
            PortalNode currentNode = _portalGraphNodes[currentNodeIndex];
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
            RouteNodeState candidateState = _routeNodeScratch[candidateIndex];
            if ((candidateState.Flags & 2) != 0)
                return;

            float edgeCost = math.distance(_portalGraphNodes[currentNodeIndex].Centroid, _portalGraphNodes[candidateIndex].Centroid);
            float tentativeG = currentGScore + edgeCost;
            if (tentativeG >= candidateState.GScore)
                return;

            candidateState.GScore = tentativeG;
            candidateState.FScore = tentativeG + math.distance(_portalGraphNodes[candidateIndex].Centroid, endWorldPosition);
            candidateState.ParentIndex = currentNodeIndex;
            if ((candidateState.Flags & 1) == 0)
            {
                candidateState.Flags |= 1;
                _routeOpenSetScratch.Add(candidateIndex);
            }

            _routeNodeScratch[candidateIndex] = candidateState;
        }

        private static int PopLowestCostOpenNode()
        {
            int bestListIndex = 0;
            float bestScore = float.MaxValue;
            for (int listIndex = 0; listIndex < _routeOpenSetScratch.Count; listIndex++)
            {
                int nodeIndex = _routeOpenSetScratch[listIndex];
                float score = _routeNodeScratch[nodeIndex].FScore;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestListIndex = listIndex;
                }
            }

            int selectedNodeIndex = _routeOpenSetScratch[bestListIndex];
            int lastListIndex = _routeOpenSetScratch.Count - 1;
            _routeOpenSetScratch[bestListIndex] = _routeOpenSetScratch[lastListIndex];
            _routeOpenSetScratch.RemoveAt(lastListIndex);
            return selectedNodeIndex;
        }

        private static void ReconstructRoute(int endNodeIndex)
        {
            _routePathScratch.Clear();
            int currentIndex = endNodeIndex;
            while (currentIndex >= 0)
            {
                _routePathScratch.Add(currentIndex);
                currentIndex = _routeNodeScratch[currentIndex].ParentIndex;
            }
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
            if (record == null ||
                !record.Current.IsCreated ||
                record.CellSize <= 0f)
            {
                return false;
            }

            float invCellSize = 1f / math.max(record.CellSize, 0.0001f);
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
            float safeCellSize = math.max(cellSize, 0.001f);
            float chunkSpan = safeCellSize * math.max(1, math.max(dimensions.x - 1, math.max(dimensions.y - 1, dimensions.z - 1)));
            int chunkX = math.clamp((int)math.floor(origin.x / chunkSpan) + ChunkIdAxisBias, 0, 1023);
            int chunkY = math.clamp((int)math.floor(origin.y / chunkSpan) + ChunkIdAxisBias, 0, 1023);
            int chunkZ = math.clamp((int)math.floor(origin.z / chunkSpan) + ChunkIdAxisBias, 0, 1023);
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

        private static void EnsureManagedBuffer<T>(ref T[] buffer, int length)
        {
            if (buffer != null && buffer.Length >= length)
                return;

            buffer = new T[length];
        }

        private static void EnsurePortalCapacity(VolumeRecord record, int requiredCount)
        {
            if (record.Portals != null && record.Portals.Length >= requiredCount)
                return;

            int newCapacity = math.max(requiredCount, math.max(4, (record.Portals?.Length ?? 0) * 2));
            PortalNode[] replacement = new PortalNode[newCapacity];
            if (record.Portals != null && record.PortalCount > 0)
                System.Array.Copy(record.Portals, replacement, record.PortalCount);

            record.Portals = replacement;
        }

        private static void EnsureRouteNodeCapacity(int requiredCount)
        {
            while (_routeNodeScratch.Count < requiredCount)
            {
                _routeNodeScratch.Add(default);
            }
        }
    }
}
