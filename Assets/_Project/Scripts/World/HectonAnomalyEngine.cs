using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Configuration for closed basin detection on a 2D heightmap.
    /// </summary>
    public struct AnomalyBasinDetectionSettings
    {
        /// <summary>Heightmap width in samples.</summary>
        public int Width;

        /// <summary>Heightmap height in samples.</summary>
        public int Height;

        /// <summary>Heightmap cell size in meters.</summary>
        public float CellSizeMeters;

        /// <summary>Minimum basin depth required before a basin is accepted.</summary>
        public float MinimumDepthMeters;

        /// <summary>Maximum number of cells a candidate flood may visit.</summary>
        public int MaxFloodCells;

        /// <summary>Height epsilon used for plateau and lip comparisons.</summary>
        public float EqualHeightEpsilon;

        /// <summary>Returns a bounded copy of the settings.</summary>
        public AnomalyBasinDetectionSettings Sanitized()
        {
            return new AnomalyBasinDetectionSettings
            {
                Width = math.max(1, Width),
                Height = math.max(1, Height),
                CellSizeMeters = math.max(0.001f, CellSizeMeters),
                MinimumDepthMeters = math.max(0f, MinimumDepthMeters),
                MaxFloodCells = math.max(8, MaxFloodCells),
                EqualHeightEpsilon = math.max(0.000001f, EqualHeightEpsilon)
            };
        }
    }

    /// <summary>
    /// Closed basin record emitted by the anomaly detector.
    /// </summary>
    public struct AnomalyBasinRecord
    {
        /// <summary>One-based basin identifier. Zero means no valid basin.</summary>
        public int BasinId;

        /// <summary>Flat heightmap index of the deepest point.</summary>
        public int DeepestIndex;

        /// <summary>Deepest point X sample.</summary>
        public int DeepestX;

        /// <summary>Deepest point Z sample.</summary>
        public int DeepestZ;

        /// <summary>Inclusive minimum X sample in the basin mask.</summary>
        public int MinX;

        /// <summary>Inclusive minimum Z sample in the basin mask.</summary>
        public int MinZ;

        /// <summary>Inclusive maximum X sample in the basin mask.</summary>
        public int MaxX;

        /// <summary>Inclusive maximum Z sample in the basin mask.</summary>
        public int MaxZ;

        /// <summary>Number of masked cells in the basin.</summary>
        public int CellCount;

        /// <summary>Height at the deepest point in meters.</summary>
        public float DeepestHeight;

        /// <summary>Spill lip height in meters.</summary>
        public float LipHeight;

        /// <summary>Approximate basin area in square meters.</summary>
        public float AreaMetersSq;

        /// <summary>One when the record is valid.</summary>
        public byte Valid;
    }

    /// <summary>
    /// Standalone anomaly and SDF processors. All hot-path storage is caller-owned.
    /// </summary>
    public static class HectonAnomalyEngine
    {
        /// <summary>
        /// Schedules closed basin detection against a heightmap.
        /// </summary>
        public static JobHandle ScheduleClosedBasinDetection(
            NativeArray<float> heightmap,
            NativeArray<byte> basinMask,
            NativeArray<AnomalyBasinRecord> basinRecords,
            NativeArray<byte> candidateMask,
            NativeArray<int> floodHeap,
            NativeArray<int> visitedStamp,
            NativeArray<int> acceptedCells,
            AnomalyBasinDetectionSettings settings,
            JobHandle dependency = default)
        {
            AnomalyBasinDetectionSettings safeSettings = settings.Sanitized();
            int cellCount = checked(safeSettings.Width * safeSettings.Height);
            if (heightmap.Length < cellCount)
                throw new ArgumentException("Heightmap length is smaller than Width * Height.", nameof(heightmap));
            if (basinMask.Length < cellCount)
                throw new ArgumentException("Basin mask length is smaller than Width * Height.", nameof(basinMask));
            if (basinRecords.Length < cellCount)
                throw new ArgumentException("Basin records length is smaller than Width * Height.", nameof(basinRecords));
            if (candidateMask.Length < cellCount)
                throw new ArgumentException("Candidate mask length is smaller than Width * Height.", nameof(candidateMask));
            if (floodHeap.Length < cellCount)
                throw new ArgumentException("Flood heap length is smaller than Width * Height.", nameof(floodHeap));
            if (visitedStamp.Length < cellCount)
                throw new ArgumentException("Visited stamp length is smaller than Width * Height.", nameof(visitedStamp));
            if (acceptedCells.Length < cellCount)
                throw new ArgumentException("Accepted cells length is smaller than Width * Height.", nameof(acceptedCells));

            var scanJob = new ClosedBasinDetectionJob
            {
                Heightmap = heightmap,
                CandidateMask = candidateMask,
                BasinMask = basinMask,
                BasinRecords = basinRecords,
                Settings = safeSettings
            };

            JobHandle scanHandle = scanJob.Schedule(cellCount, 64, dependency);
            var floodJob = new ClosedBasinFloodFillJob
            {
                Heightmap = heightmap,
                CandidateMask = candidateMask,
                BasinMask = basinMask,
                BasinRecords = basinRecords,
                FloodHeap = floodHeap,
                VisitedStamp = visitedStamp,
                AcceptedCells = acceptedCells,
                Settings = safeSettings
            };

            return floodJob.Schedule(scanHandle);
        }

        /// <summary>
        /// Schedules exact terrain-to-SDF top-surface snapping.
        /// </summary>
        public static JobHandle SnapSDFToTerrain(
            NativeArray<float> terrainHeights,
            int terrainWidth,
            int terrainDepth,
            float terrainCellSizeMeters,
            double3 terrainOriginAup,
            NativeArray<float> sdf,
            int sdfWidth,
            int sdfHeight,
            int sdfDepth,
            float voxelSizeMeters,
            double3 sdfOriginAup,
            JobHandle dependency = default)
        {
            ValidateTerrainSdfBuffers(terrainHeights, terrainWidth, terrainDepth, sdf, sdfWidth, sdfHeight, sdfDepth);

            var job = new SnapSDFToTerrainJob
            {
                TerrainHeights = terrainHeights,
                TerrainWidth = math.max(1, terrainWidth),
                TerrainDepth = math.max(1, terrainDepth),
                TerrainCellSizeMeters = math.max(0.001f, terrainCellSizeMeters),
                TerrainOriginAup = terrainOriginAup,
                Sdf = sdf,
                SdfWidth = math.max(1, sdfWidth),
                SdfHeight = math.max(1, sdfHeight),
                SdfDepth = math.max(1, sdfDepth),
                VoxelSizeMeters = math.max(0.001f, voxelSizeMeters),
                SdfOriginAup = sdfOriginAup
            };

            return job.Schedule(sdfWidth * sdfHeight * sdfDepth, 64, dependency);
        }

        /// <summary>
        /// Schedules injection of a solid chthonic pillar into a signed density field.
        /// </summary>
        public static JobHandle InjectMegaPillarSDF(
            NativeArray<float> sdf,
            int sdfWidth,
            int sdfHeight,
            int sdfDepth,
            float voxelSizeMeters,
            double3 sdfOriginAup,
            double3 pillarBaseAup,
            float radiusMeters,
            float heightMeters = 1000f,
            float edgeWarpMeters = 24f,
            float noiseFrequency = 0.004f,
            JobHandle dependency = default)
        {
            ValidateSdfBuffer(sdf, sdfWidth, sdfHeight, sdfDepth);

            var job = new InjectMegaPillarSDFJob
            {
                Sdf = sdf,
                SdfWidth = math.max(1, sdfWidth),
                SdfHeight = math.max(1, sdfHeight),
                SdfDepth = math.max(1, sdfDepth),
                VoxelSizeMeters = math.max(0.001f, voxelSizeMeters),
                SdfOriginAup = sdfOriginAup,
                PillarBaseAup = pillarBaseAup,
                RadiusMeters = math.max(0.001f, radiusMeters),
                HeightMeters = math.max(0.001f, heightMeters),
                EdgeWarpMeters = math.max(0f, edgeWarpMeters),
                NoiseFrequency = math.max(0.000001f, noiseFrequency)
            };

            return job.Schedule(sdfWidth * sdfHeight * sdfDepth, 64, dependency);
        }

        /// <summary>
        /// Schedules lateral SDF displacement that turns steep stitched slopes into overhangs.
        /// </summary>
        public static JobHandle ApplyVoxelCliffOverhangNoise(
            NativeArray<float> inputSdf,
            NativeArray<float> outputSdf,
            int sdfWidth,
            int sdfHeight,
            int sdfDepth,
            float voxelSizeMeters,
            float slopeThreshold,
            float lateralAmplitudeMeters,
            float noiseFrequency,
            float strength,
            JobHandle dependency = default)
        {
            ValidateSdfBuffer(inputSdf, sdfWidth, sdfHeight, sdfDepth);
            ValidateSdfBuffer(outputSdf, sdfWidth, sdfHeight, sdfDepth);

            var job = new VoxelCliffOverhangNoiseJob
            {
                InputSdf = inputSdf,
                OutputSdf = outputSdf,
                SdfWidth = math.max(1, sdfWidth),
                SdfHeight = math.max(1, sdfHeight),
                SdfDepth = math.max(1, sdfDepth),
                VoxelSizeMeters = math.max(0.001f, voxelSizeMeters),
                SlopeThreshold = math.max(0f, slopeThreshold),
                LateralAmplitudeMeters = math.max(0f, lateralAmplitudeMeters),
                NoiseFrequency = math.max(0.000001f, noiseFrequency),
                Strength = math.saturate(strength)
            };

            return job.Schedule(sdfWidth * sdfHeight * sdfDepth, 64, dependency);
        }

        private static void ValidateTerrainSdfBuffers(
            NativeArray<float> terrainHeights,
            int terrainWidth,
            int terrainDepth,
            NativeArray<float> sdf,
            int sdfWidth,
            int sdfHeight,
            int sdfDepth)
        {
            int terrainCount = checked(math.max(1, terrainWidth) * math.max(1, terrainDepth));
            if (terrainHeights.Length < terrainCount)
                throw new ArgumentException("Terrain height array is smaller than terrainWidth * terrainDepth.", nameof(terrainHeights));

            ValidateSdfBuffer(sdf, sdfWidth, sdfHeight, sdfDepth);
        }

        private static void ValidateSdfBuffer(NativeArray<float> sdf, int sdfWidth, int sdfHeight, int sdfDepth)
        {
            int count = checked(math.max(1, sdfWidth) * math.max(1, sdfHeight) * math.max(1, sdfDepth));
            if (sdf.Length < count)
                throw new ArgumentException("SDF array is smaller than sdfWidth * sdfHeight * sdfDepth.", nameof(sdf));
        }
    }

    /// <summary>
    /// Burst parallel kernel that scans the heightmap, clears outputs, and marks local minimum candidates.
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Deterministic, CompileSynchronously = true)]
    public struct ClosedBasinDetectionJob : IJobParallelFor
    {
        /// <summary>Input heightmap in meters.</summary>
        [ReadOnly] public NativeArray<float> Heightmap;

        /// <summary>Candidate minima mask. One means the cell is a flood-fill seed.</summary>
        public NativeArray<byte> CandidateMask;

        /// <summary>Basin extent mask. One means the cell belongs to an accepted basin.</summary>
        public NativeArray<byte> BasinMask;

        /// <summary>Record array indexed by candidate cell.</summary>
        public NativeArray<AnomalyBasinRecord> BasinRecords;

        /// <summary>Detection settings.</summary>
        public AnomalyBasinDetectionSettings Settings;

        /// <inheritdoc />
        public void Execute(int index)
        {
            CandidateMask[index] = 0;
            BasinMask[index] = 0;
            BasinRecords[index] = default;

            int width = Settings.Width;
            int height = Settings.Height;
            int x = index % width;
            int z = index / width;
            if (x <= 0 || z <= 0 || x >= width - 1 || z >= height - 1)
                return;

            float center = Heightmap[index];
            if (!math.isfinite(center))
                return;

            float epsilon = Settings.EqualHeightEpsilon;
            bool hasHigherNeighbor = false;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0)
                        continue;

                    int neighborIndex = index + dx + dz * width;
                    float neighbor = Heightmap[neighborIndex];
                    if (!math.isfinite(neighbor))
                        return;

                    if (neighbor < center - epsilon)
                        return;

                    if (math.abs(neighbor - center) <= epsilon && neighborIndex < index)
                        return;

                    if (neighbor > center + epsilon)
                        hasHigherNeighbor = true;
                }
            }

            if (!hasHigherNeighbor)
                return;

            CandidateMask[index] = 1;
        }
    }

    /// <summary>
    /// Burst flood-fill kernel that expands candidate minima to their spill lip and writes basin extents.
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Deterministic, CompileSynchronously = true)]
    public struct ClosedBasinFloodFillJob : IJob
    {
        /// <summary>Input heightmap in meters.</summary>
        [ReadOnly] public NativeArray<float> Heightmap;

        /// <summary>Candidate minima mask.</summary>
        [ReadOnly] public NativeArray<byte> CandidateMask;

        /// <summary>Output basin mask.</summary>
        public NativeArray<byte> BasinMask;

        /// <summary>Output records indexed by candidate cell.</summary>
        public NativeArray<AnomalyBasinRecord> BasinRecords;

        /// <summary>Binary min-heap scratch. Caller owns storage.</summary>
        public NativeArray<int> FloodHeap;

        /// <summary>Visited stamp scratch. Caller owns storage.</summary>
        public NativeArray<int> VisitedStamp;

        /// <summary>Accepted cell scratch. Caller owns storage.</summary>
        public NativeArray<int> AcceptedCells;

        /// <summary>Detection settings.</summary>
        public AnomalyBasinDetectionSettings Settings;

        /// <inheritdoc />
        public void Execute()
        {
            int cellCount = Settings.Width * Settings.Height;
            int stamp = 1;
            int basinId = 1;

            for (int index = 0; index < cellCount; index++)
            {
                if (CandidateMask[index] == 0 || BasinMask[index] != 0)
                    continue;

                AnomalyBasinRecord record = ResolveCandidate(index, stamp, basinId, out int nextStamp);
                stamp = nextStamp;
                if (record.Valid == 0)
                {
                    BasinRecords[index] = default;
                    continue;
                }

                basinId++;
                BasinRecords[index] = record;
            }
        }

        private AnomalyBasinRecord ResolveCandidate(int seedIndex, int stamp, int basinId, out int nextStamp)
        {
            int width = Settings.Width;
            int height = Settings.Height;
            int cellCount = width * height;
            int maxFloodCells = math.min(Settings.MaxFloodCells, cellCount);
            float epsilon = Settings.EqualHeightEpsilon;
            float deepestHeight = Heightmap[seedIndex];

            if (!NextStamp(ref stamp))
            {
                ClearVisited(cellCount);
                stamp = 1;
            }

            int heapCount = 0;
            int acceptedCount = 0;
            float lipHeight = deepestHeight;
            bool foundSpill = false;

            MarkVisited(seedIndex, stamp);
            HeapPush(ref heapCount, seedIndex);

            while (heapCount > 0 && acceptedCount < maxFloodCells)
            {
                int cellIndex = HeapPop(ref heapCount);
                float cellHeight = Heightmap[cellIndex];
                AcceptedCells[acceptedCount++] = cellIndex;
                lipHeight = math.max(lipHeight, cellHeight);

                if (cellHeight > deepestHeight + epsilon && HasUnvisitedLowerNeighbor(cellIndex, cellHeight, stamp, epsilon))
                {
                    lipHeight = cellHeight;
                    foundSpill = true;
                    break;
                }

                int x = cellIndex % width;
                int z = cellIndex / width;
                for (int dz = -1; dz <= 1; dz++)
                {
                    int nz = z + dz;
                    if (nz < 0 || nz >= height)
                        continue;

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0)
                            continue;

                        int nx = x + dx;
                        if (nx < 0 || nx >= width)
                            continue;

                        int neighborIndex = nx + nz * width;
                        if (VisitedStamp[neighborIndex] == stamp || BasinMask[neighborIndex] != 0)
                            continue;

                        float neighborHeight = Heightmap[neighborIndex];
                        if (!math.isfinite(neighborHeight))
                            continue;

                        MarkVisited(neighborIndex, stamp);
                        HeapPush(ref heapCount, neighborIndex);
                    }
                }
            }

            nextStamp = stamp;
            if (acceptedCount <= 0 || acceptedCount >= maxFloodCells)
                return default;

            float depth = lipHeight - deepestHeight;
            if (depth + epsilon < Settings.MinimumDepthMeters)
                return default;

            int deepestIndex = seedIndex;
            int deepestX = seedIndex % width;
            int deepestZ = seedIndex / width;
            int minX = width;
            int minZ = height;
            int maxX = 0;
            int maxZ = 0;
            int maskedCount = 0;
            float maskThreshold = foundSpill ? lipHeight - epsilon : lipHeight + epsilon;

            for (int i = 0; i < acceptedCount; i++)
            {
                int cellIndex = AcceptedCells[i];
                float cellHeight = Heightmap[cellIndex];
                if (cellHeight > lipHeight + epsilon || cellHeight >= maskThreshold)
                    continue;

                int x = cellIndex % width;
                int z = cellIndex / width;
                BasinMask[cellIndex] = 1;
                minX = math.min(minX, x);
                minZ = math.min(minZ, z);
                maxX = math.max(maxX, x);
                maxZ = math.max(maxZ, z);
                maskedCount++;

                if (cellHeight < deepestHeight - epsilon)
                {
                    deepestHeight = cellHeight;
                    deepestIndex = cellIndex;
                    deepestX = x;
                    deepestZ = z;
                }
            }

            if (maskedCount <= 0)
                return default;

            float cellSize = Settings.CellSizeMeters;
            return new AnomalyBasinRecord
            {
                BasinId = basinId,
                DeepestIndex = deepestIndex,
                DeepestX = deepestX,
                DeepestZ = deepestZ,
                MinX = minX,
                MinZ = minZ,
                MaxX = maxX,
                MaxZ = maxZ,
                CellCount = maskedCount,
                DeepestHeight = deepestHeight,
                LipHeight = lipHeight,
                AreaMetersSq = maskedCount * cellSize * cellSize,
                Valid = 1
            };
        }

        private bool HasUnvisitedLowerNeighbor(int cellIndex, float cellHeight, int stamp, float epsilon)
        {
            int width = Settings.Width;
            int height = Settings.Height;
            int x = cellIndex % width;
            int z = cellIndex / width;

            for (int dz = -1; dz <= 1; dz++)
            {
                int nz = z + dz;
                if (nz < 0 || nz >= height)
                    continue;

                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0)
                        continue;

                    int nx = x + dx;
                    if (nx < 0 || nx >= width)
                        continue;

                    int neighborIndex = nx + nz * width;
                    if (VisitedStamp[neighborIndex] == stamp)
                        continue;

                    float neighborHeight = Heightmap[neighborIndex];
                    if (math.isfinite(neighborHeight) && neighborHeight < cellHeight - epsilon)
                        return true;
                }
            }

            return false;
        }

        private void HeapPush(ref int heapCount, int index)
        {
            int heapIndex = heapCount++;
            FloodHeap[heapIndex] = index;

            while (heapIndex > 0)
            {
                int parent = (heapIndex - 1) >> 1;
                if (Heightmap[FloodHeap[parent]] <= Heightmap[index])
                    break;

                FloodHeap[heapIndex] = FloodHeap[parent];
                heapIndex = parent;
            }

            FloodHeap[heapIndex] = index;
        }

        private int HeapPop(ref int heapCount)
        {
            int result = FloodHeap[0];
            int last = FloodHeap[--heapCount];
            if (heapCount <= 0)
                return result;

            int heapIndex = 0;
            while (true)
            {
                int left = heapIndex * 2 + 1;
                if (left >= heapCount)
                    break;

                int right = left + 1;
                int child = right < heapCount && Heightmap[FloodHeap[right]] < Heightmap[FloodHeap[left]] ? right : left;
                if (Heightmap[FloodHeap[child]] >= Heightmap[last])
                    break;

                FloodHeap[heapIndex] = FloodHeap[child];
                heapIndex = child;
            }

            FloodHeap[heapIndex] = last;
            return result;
        }

        private void MarkVisited(int index, int stamp)
        {
            VisitedStamp[index] = stamp;
        }

        private static bool NextStamp(ref int stamp)
        {
            if (stamp == int.MaxValue)
                return false;

            stamp++;
            return true;
        }

        private void ClearVisited(int cellCount)
        {
            for (int i = 0; i < cellCount; i++)
                VisitedStamp[i] = 0;
        }
    }

}
