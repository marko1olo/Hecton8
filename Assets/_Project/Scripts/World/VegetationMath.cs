using Hecton8.Environment;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Burst-safe deterministic math used by the MapMagic vegetation bridge and downstream vegetation jobs.
    /// </summary>
    internal static class VegetationMath
    {
        public const int DensityGridResolution = 8;

        public static float BuildJitteredCoordinate(float min, float step, int index, float jitterFraction, uint seed)
        {
            float basePosition = min + ((index + 0.5f) * step);
            float jitter = ((Hash01(seed) * 2f) - 1f) * step * jitterFraction;
            return basePosition + jitter;
        }

        public static bool TryEvaluateFloatingLabyrinth(
            float worldX,
            float worldZ,
            uint seed,
            float floatingPatchThreshold,
            float floatingPatchNoiseScale,
            float floatingCellSize,
            float floatingSecondaryCellSize,
            float floatingWallWidth,
            float floatingWarpMeters,
            float2 floatingFlowDirection,
            float floatingFlowAnisotropy,
            out float occupancy)
        {
            float2 world = new float2(worldX, worldZ);
            float2 flowDirection = NormalizeSafe(floatingFlowDirection, new float2(1f, 0f));
            float2 crossFlow = new float2(-flowDirection.y, flowDirection.x);
            float2 flowSpace = new float2(
                math.dot(world, flowDirection),
                math.dot(world, crossFlow) * floatingFlowAnisotropy);

            float2 warp = SampleFloatingWarp(world, floatingPatchNoiseScale, floatingWarpMeters);
            float primaryEdgeDistance = EvaluateVoronoiEdgeDistance(
                flowSpace + warp,
                floatingCellSize,
                HectonVegetationConstants.PrimaryVoronoiSalt,
                out float primaryVariation);
            float secondaryEdgeDistance = EvaluateVoronoiEdgeDistance(
                world + (warp * 0.65f),
                floatingSecondaryCellSize,
                HectonVegetationConstants.SecondaryVoronoiSalt,
                out float secondaryVariation);
            float primaryWall = 1f - math.saturate(primaryEdgeDistance / math.max(0.01f, floatingWallWidth));
            float secondaryWidth = math.max(0.75f, floatingWallWidth * 0.8f);
            float secondaryWall = 1f - math.saturate(secondaryEdgeDistance / secondaryWidth);
            float combinedWall = math.saturate((primaryWall * 0.72f) + (secondaryWall * 0.4f));
            float cellVariation = math.lerp(primaryVariation, secondaryVariation, 0.35f);
            occupancy = combinedWall * math.lerp(0.82f, 1.14f, cellVariation);
            occupancy *= math.lerp(0.92f, 1.08f, Hash01(seed ^ HectonVegetationConstants.OccupancyVariationSalt));
            return occupancy > floatingPatchThreshold;
        }

        public static float2 SampleFloatingWarp(float2 world, float floatingPatchNoiseScale, float floatingWarpMeters)
        {
            float sampleX = world.x * floatingPatchNoiseScale;
            float sampleZ = world.y * floatingPatchNoiseScale;
            float warpX = ((SampleValueNoise(sampleX + 11.37f, sampleZ + 47.13f, HectonVegetationConstants.WarpXSalt) * 2f) - 1f) * floatingWarpMeters;
            float warpZ = ((SampleValueNoise(sampleX + 29.61f, sampleZ + 73.77f, HectonVegetationConstants.WarpZSalt) * 2f) - 1f) * floatingWarpMeters;
            return new float2(warpX, warpZ);
        }

        public static float EvaluateVoronoiEdgeDistance(float2 position, float cellSize, uint salt, out float variation)
        {
            float inverseCellSize = 1f / math.max(0.01f, cellSize);
            float2 scaled = position * inverseCellSize;
            int baseX = (int)math.floor(scaled.x);
            int baseZ = (int)math.floor(scaled.y);
            float nearestDistanceSqr = float.PositiveInfinity;
            float secondDistanceSqr = float.PositiveInfinity;
            uint nearestSeed = 0u;

            for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    int cellX = baseX + offsetX;
                    int cellZ = baseZ + offsetZ;
                    uint cellSeed = BuildCellSeed(cellX, cellZ, salt);
                    float2 featurePoint = new float2(
                        cellX + Hash01(cellSeed),
                        cellZ + Hash01(cellSeed ^ HectonVegetationConstants.SecondaryFeatureSalt));
                    float2 delta = featurePoint - scaled;
                    float distanceSqr = math.lengthsq(delta);
                    if (distanceSqr < nearestDistanceSqr)
                    {
                        secondDistanceSqr = nearestDistanceSqr;
                        nearestDistanceSqr = distanceSqr;
                        nearestSeed = cellSeed;
                    }
                    else if (distanceSqr < secondDistanceSqr)
                    {
                        secondDistanceSqr = distanceSqr;
                    }
                }
            }

            float nearestDistance = math.sqrt(nearestDistanceSqr);
            float secondDistance = math.sqrt(secondDistanceSqr);
            variation = Hash01(nearestSeed ^ HectonVegetationConstants.PrimaryVariationSalt);
            return math.max(0f, (secondDistance - nearestDistance) * cellSize * 0.5f);
        }

        public static float SampleValueNoise(float x, float z, uint salt)
        {
            int minX = (int)math.floor(x);
            int minZ = (int)math.floor(z);
            float fracX = x - minX;
            float fracZ = z - minZ;
            float smoothX = fracX * fracX * (3f - (2f * fracX));
            float smoothZ = fracZ * fracZ * (3f - (2f * fracZ));
            float bottomLeft = Hash01(BuildCellSeed(minX, minZ, salt));
            float bottomRight = Hash01(BuildCellSeed(minX + 1, minZ, salt));
            float topLeft = Hash01(BuildCellSeed(minX, minZ + 1, salt));
            float topRight = Hash01(BuildCellSeed(minX + 1, minZ + 1, salt));
            float bottom = math.lerp(bottomLeft, bottomRight, smoothX);
            float top = math.lerp(topLeft, topRight, smoothX);
            return math.lerp(bottom, top, smoothZ);
        }

        public static float EvaluateVisibilityModifier(
            float worldY,
            float3 densityChannels,
            float grassWeight,
            float kelpWeight,
            float sargassumWeight,
            float localWaterLevel,
            float localFloatingSurfaceOffset,
            float localSargassumVisibilityBand)
        {
            float grassCover = math.saturate(densityChannels.x * grassWeight);
            float kelpCover = math.saturate(densityChannels.y * kelpWeight);
            float verticalConcealment = EvaluateSargassumVerticalConcealment(
                worldY,
                localWaterLevel,
                localFloatingSurfaceOffset,
                localSargassumVisibilityBand);
            float sargassumCover = math.saturate(densityChannels.z * sargassumWeight * verticalConcealment);
            float combinedDensity = grassCover + kelpCover + sargassumCover;
            return ApproximateOneMinusExpNegPositive(combinedDensity);
        }

        public static float EvaluateSargassumVerticalConcealment(
            float worldY,
            float localWaterLevel,
            float localFloatingSurfaceOffset,
            float localSargassumVisibilityBand)
        {
            float canopyY = localWaterLevel + localFloatingSurfaceOffset;
            if (worldY > canopyY)
                return 0.12f;

            float band = math.max(0.25f, localSargassumVisibilityBand);
            float canopyDepth = canopyY - worldY;
            return math.saturate(1f - (canopyDepth / band));
        }

        public static float3 ResolveDensityChannel(int type, float densityWeight)
        {
            switch ((HectonVegetationInstanceType)type)
            {
                case HectonVegetationInstanceType.Grass:
                    return new float3(densityWeight, 0f, 0f);
                case HectonVegetationInstanceType.GiantKelp:
                    return new float3(0f, densityWeight, 0f);
                case HectonVegetationInstanceType.Sargassum:
                    return new float3(0f, 0f, densityWeight);
                default:
                    return float3.zero;
            }
        }

        public static float2 ResolveThreatAttractorChannel(int semanticType, float densityWeight)
        {
            HectonMapMagicVegetationBridge.VegetationSemanticType resolvedSemanticType =
                (HectonMapMagicVegetationBridge.VegetationSemanticType)semanticType;
            switch (resolvedSemanticType)
            {
                case HectonMapMagicVegetationBridge.VegetationSemanticType.ColonyCable:
                case HectonMapMagicVegetationBridge.VegetationSemanticType.ColonyHullPlating:
                case HectonMapMagicVegetationBridge.VegetationSemanticType.ColonySupportBeam:
                    return new float2(0f, densityWeight);
                case HectonMapMagicVegetationBridge.VegetationSemanticType.FloatingSargassum:
                    return new float2(densityWeight, 0f);
                case HectonMapMagicVegetationBridge.VegetationSemanticType.DeadZoneMassiveStructure:
                    return new float2(0f, densityWeight * 0.35f);
                default:
                    return float2.zero;
            }
        }

        public static float SampleDensityAtPosition(
            float3 position,
            int typeMask,
            NativeArray<HectonMapMagicVegetationBridge.VegetationDensityChunkRecord> chunks,
            NativeArray<float3> densityGrid,
            int chunkCount)
        {
            return ApplyDensityTypeMask(SampleDensityChannelsAtPosition(position, chunks, densityGrid, chunkCount), typeMask);
        }

        public static float3 SampleDensityChannelsAtPosition(
            float3 position,
            NativeArray<HectonMapMagicVegetationBridge.VegetationDensityChunkRecord> chunks,
            NativeArray<float3> densityGrid,
            int chunkCount)
        {
            float3 density = float3.zero;
            for (int i = 0; i < chunkCount; i++)
            {
                HectonMapMagicVegetationBridge.VegetationDensityChunkRecord chunk = chunks[i];
                if (position.x < chunk.MinX || position.x > chunk.MaxX || position.z < chunk.MinZ || position.z > chunk.MaxZ)
                    continue;

                density += SampleChunkDensityChannels(position.x, position.z, chunk, densityGrid);
            }

            return density;
        }

        public static float3 SampleDensityChannelsAtPositionHashed(
            float3 position,
            NativeArray<HectonMapMagicVegetationBridge.VegetationDensityChunkRecord> chunks,
            NativeArray<float3> densityGrid,
            NativeParallelMultiHashMap<int, int> chunkHash,
            float3 gridCenter,
            float cellSize,
            int gridResolution,
            int chunkCount)
        {
            if (!chunkHash.IsCreated)
                return SampleDensityChannelsAtPosition(position, chunks, densityGrid, chunkCount);

            int cellIndex = ComputeThreatGridCellIndex(position, gridCenter, cellSize, gridResolution);
            if (cellIndex < 0)
                return float3.zero;

            float3 density = float3.zero;
            NativeParallelMultiHashMapIterator<int> iterator;
            int chunkIndex;
            if (!chunkHash.TryGetFirstValue(cellIndex, out chunkIndex, out iterator))
                return density;

            do
            {
                if (chunkIndex < 0 || chunkIndex >= chunkCount || chunkIndex >= chunks.Length)
                    continue;

                HectonMapMagicVegetationBridge.VegetationDensityChunkRecord chunk = chunks[chunkIndex];
                if (position.x < chunk.MinX || position.x > chunk.MaxX || position.z < chunk.MinZ || position.z > chunk.MaxZ)
                    continue;

                density += SampleChunkDensityChannels(position.x, position.z, chunk, densityGrid);
            }
            while (chunkHash.TryGetNextValue(out chunkIndex, ref iterator));

            return density;
        }

        public static float2 SampleThreatAttractorAtPosition(
            float3 position,
            NativeArray<HectonMapMagicVegetationBridge.VegetationDensityChunkRecord> chunks,
            NativeArray<float2> attractorGrid,
            int chunkCount)
        {
            float2 attractor = float2.zero;
            for (int i = 0; i < chunkCount; i++)
            {
                HectonMapMagicVegetationBridge.VegetationDensityChunkRecord chunk = chunks[i];
                if (position.x < chunk.MinX || position.x > chunk.MaxX || position.z < chunk.MinZ || position.z > chunk.MaxZ)
                    continue;

                attractor += SampleThreatAttractorChunk(position.x, position.z, chunk, attractorGrid);
            }

            return attractor;
        }

        public static float2 SampleThreatAttractorAtPositionHashed(
            float3 position,
            NativeArray<HectonMapMagicVegetationBridge.VegetationDensityChunkRecord> chunks,
            NativeArray<float2> attractorGrid,
            NativeParallelMultiHashMap<int, int> chunkHash,
            float3 gridCenter,
            float cellSize,
            int gridResolution,
            int chunkCount)
        {
            if (!chunkHash.IsCreated)
                return SampleThreatAttractorAtPosition(position, chunks, attractorGrid, chunkCount);

            int cellIndex = ComputeThreatGridCellIndex(position, gridCenter, cellSize, gridResolution);
            if (cellIndex < 0)
                return float2.zero;

            float2 attractor = float2.zero;
            NativeParallelMultiHashMapIterator<int> iterator;
            int chunkIndex;
            if (!chunkHash.TryGetFirstValue(cellIndex, out chunkIndex, out iterator))
                return attractor;

            do
            {
                if (chunkIndex < 0 || chunkIndex >= chunkCount || chunkIndex >= chunks.Length)
                    continue;

                HectonMapMagicVegetationBridge.VegetationDensityChunkRecord chunk = chunks[chunkIndex];
                if (position.x < chunk.MinX || position.x > chunk.MaxX || position.z < chunk.MinZ || position.z > chunk.MaxZ)
                    continue;

                attractor += SampleThreatAttractorChunk(position.x, position.z, chunk, attractorGrid);
            }
            while (chunkHash.TryGetNextValue(out chunkIndex, ref iterator));

            return attractor;
        }

        public static float3 SampleChunkDensityChannels(
            float worldX,
            float worldZ,
            HectonMapMagicVegetationBridge.VegetationDensityChunkRecord chunk,
            NativeArray<float3> densityGrid)
        {
            float width = math.max(0.01f, chunk.MaxX - chunk.MinX);
            float depth = math.max(0.01f, chunk.MaxZ - chunk.MinZ);
            float normalizedX = math.saturate((worldX - chunk.MinX) / width) * (DensityGridResolution - 1);
            float normalizedZ = math.saturate((worldZ - chunk.MinZ) / depth) * (DensityGridResolution - 1);
            int cellX = math.clamp((int)math.floor(normalizedX), 0, DensityGridResolution - 1);
            int cellZ = math.clamp((int)math.floor(normalizedZ), 0, DensityGridResolution - 1);
            int nextCellX = math.min(cellX + 1, DensityGridResolution - 1);
            int nextCellZ = math.min(cellZ + 1, DensityGridResolution - 1);
            float fracX = normalizedX - cellX;
            float fracZ = normalizedZ - cellZ;

            float3 sample00 = densityGrid[chunk.GridOffset + (cellZ * DensityGridResolution) + cellX];
            float3 sample10 = densityGrid[chunk.GridOffset + (cellZ * DensityGridResolution) + nextCellX];
            float3 sample01 = densityGrid[chunk.GridOffset + (nextCellZ * DensityGridResolution) + cellX];
            float3 sample11 = densityGrid[chunk.GridOffset + (nextCellZ * DensityGridResolution) + nextCellX];
            float3 bottom = math.lerp(sample00, sample10, fracX);
            float3 top = math.lerp(sample01, sample11, fracX);
            return math.lerp(bottom, top, fracZ);
        }

        public static float ApplyDensityTypeMask(float3 sample, int typeMask)
        {
            float density = 0f;
            if ((typeMask & HectonMapMagicVegetationBridge.DensityTypeMaskGrass) != 0)
                density += sample.x;
            if ((typeMask & HectonMapMagicVegetationBridge.DensityTypeMaskKelp) != 0)
                density += sample.y;
            if ((typeMask & HectonMapMagicVegetationBridge.DensityTypeMaskSargassum) != 0)
                density += sample.z;

            return density;
        }

        public static int ComputeThreatGridCellIndex(float3 position, float3 gridCenter, float cellSize, int resolution)
        {
            if (resolution <= 0 || cellSize <= 0f)
                return -1;

            float halfExtent = (resolution - 1) * 0.5f * cellSize;
            float localX = position.x - (gridCenter.x - halfExtent);
            float localZ = position.z - (gridCenter.z - halfExtent);
            if (localX < 0f || localZ < 0f || localX > halfExtent * 2f || localZ > halfExtent * 2f)
                return -1;

            int cellX = math.clamp((int)math.floor(localX / cellSize), 0, resolution - 1);
            int cellZ = math.clamp((int)math.floor(localZ / cellSize), 0, resolution - 1);
            return (cellZ * resolution) + cellX;
        }

        public static byte EncodeThreatByte(float threat)
        {
            return (byte)math.clamp((int)math.round(math.saturate(threat) * 255f), 0, 255);
        }

        public static float DecodeThreatByte(byte encoded)
        {
            return encoded * (1f / 255f);
        }

        public static void ClearByteGrid(NativeArray<byte> destination, int count)
        {
            if (!destination.IsCreated || count <= 0)
                return;

            int end = math.min(count, destination.Length);
            for (int i = 0; i < end; i++)
                destination[i] = 0;
        }

        public static void ClearFloatGrid(NativeArray<float> destination, int count)
        {
            if (!destination.IsCreated || count <= 0)
                return;

            int end = math.min(count, destination.Length);
            for (int i = 0; i < end; i++)
                destination[i] = 0f;
        }

        public static int PositiveModulo(int value, int length)
        {
            if (length <= 0)
                return 0;

            int wrapped = value % length;
            return wrapped < 0 ? wrapped + length : wrapped;
        }

        public static uint BuildCellSeed(int cellX, int cellZ, uint salt)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)cellX) * 16777619u;
                hash = (hash ^ (uint)cellZ) * 16777619u;
                hash = (hash ^ salt) * 16777619u;
                return hash;
            }
        }

        public static byte PackMask01(float value)
        {
            return (byte)math.clamp((int)math.round(math.saturate(value) * 255f), 0, 255);
        }

        public static float Hash01(uint seed)
        {
            unchecked
            {
                seed ^= seed >> 16;
                seed *= 0x7FEB352Du;
                seed ^= seed >> 15;
                seed *= 0x846CA68Bu;
                seed ^= seed >> 16;
                return (seed & 0x00FFFFFFu) / 16777215f;
            }
        }

        private static float2 SampleThreatAttractorChunk(
            float worldX,
            float worldZ,
            HectonMapMagicVegetationBridge.VegetationDensityChunkRecord chunk,
            NativeArray<float2> attractorGrid)
        {
            float width = math.max(0.01f, chunk.MaxX - chunk.MinX);
            float depth = math.max(0.01f, chunk.MaxZ - chunk.MinZ);
            float normalizedX = math.saturate((worldX - chunk.MinX) / width) * (DensityGridResolution - 1);
            float normalizedZ = math.saturate((worldZ - chunk.MinZ) / depth) * (DensityGridResolution - 1);
            int cellX = math.clamp((int)math.floor(normalizedX), 0, DensityGridResolution - 1);
            int cellZ = math.clamp((int)math.floor(normalizedZ), 0, DensityGridResolution - 1);
            int nextCellX = math.min(cellX + 1, DensityGridResolution - 1);
            int nextCellZ = math.min(cellZ + 1, DensityGridResolution - 1);
            float fracX = normalizedX - cellX;
            float fracZ = normalizedZ - cellZ;

            float2 sample00 = attractorGrid[chunk.GridOffset + (cellZ * DensityGridResolution) + cellX];
            float2 sample10 = attractorGrid[chunk.GridOffset + (cellZ * DensityGridResolution) + nextCellX];
            float2 sample01 = attractorGrid[chunk.GridOffset + (nextCellZ * DensityGridResolution) + cellX];
            float2 sample11 = attractorGrid[chunk.GridOffset + (nextCellZ * DensityGridResolution) + nextCellX];
            float2 sampleX0 = math.lerp(sample00, sample10, fracX);
            float2 sampleX1 = math.lerp(sample01, sample11, fracX);
            return math.lerp(sampleX0, sampleX1, fracZ);
        }

        private static float2 NormalizeSafe(float2 value, float2 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq > 0.000001f
                ? value * math.rsqrt(lengthSq)
                : fallback;
        }

        private static float ApproximateOneMinusExpNegPositive(float x)
        {
            return math.saturate(1f - ApproximateExpNegPositive(x));
        }

        private static float ApproximateExpNegPositive(float x)
        {
            float clamped = math.clamp(x, 0f, 8f);
            float x2 = clamped * clamped;
            float x3 = x2 * clamped;
            float numerator = 120f - (60f * clamped) + (12f * x2) - x3;
            float denominator = 120f + (60f * clamped) + (12f * x2) + x3;
            return math.saturate(numerator / math.max(denominator, 0.0001f));
        }
    }
}
