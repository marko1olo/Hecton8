#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Rendering.Scatter;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.Editor.Generators.World
{
    internal struct AbyssalScatterBakeResult
    {
        public string OutputPath;
        public string MetadataPath;
        public string PrefabPath;
        public string MapMagicSourceFolder;
        public string CullingDatasetFolder;
        public int SourceRuleCount;
        public int SourcePrefabCount;
        public int MapMagicAssetCount;
        public bool SourceFoldersValid;
        public int ImportedCullingBoundsCount;
        public int MockCullingBoundsCount;
        public bool ImportedCullingBoundsTruncated;
        public int InstanceCount;
        public int CullingBoundsCount;
        public int CulledCount;
        public int NonFiniteCount;
        public long FileBytes;
        public uint ContentHash;
        public float AlignmentMilliseconds;
        public float CullingMilliseconds;
        public float SerializationMilliseconds;
        public float TotalMilliseconds;
        public bool LayoutValid;
        public bool PrefabHasRendererController;
    }

    internal sealed class AbyssalScatterBrgMetadataAsset : ScriptableObject
    {
        public string binaryAssetPath;
        public int matrixCount;
        public int metadataCount;
        public int qualityIndexCount;
        public int matrixStrideBytes;
        public int metadataStrideBytes;
        public int qualityIndexStrideBytes;
        public uint matrixOffsetBytes;
        public uint metadataOffsetBytes;
        public uint qualityIndexOffsetBytes;
        public uint contentHash;
        public uint headerHash;
        public uint chunkHash;
        public long binaryFileBytes;
        public Bounds bakedDrawBounds;
        public float globalQualityWeightAtBake;
        public float lowDeviceDrawFraction;
        public float middleDeviceDrawFraction;
        public float highDeviceDrawFraction;
        public float ultraDeviceDrawFraction;
    }

    internal static class AbyssalScatterPolisherPipeline
    {
        public const string OutputFolder = "Assets/StreamingAssets/Hecton8/Scatter";
        public const string MetadataFolder = "Assets/_Project/Data/World/ScatterMetadata";
        public const string PrefabFolder = "Assets/_Project/Prefabs/WorldSupport/GeneratedScatter";
        public const string DefaultAssetName = "scatter_1614_mock_chunk.brgdata";
        public const string DefaultMapMagicOutputFolder = "Assets/_Project/Data/World/Sandbox";
        public const string DefaultCullingDatasetFolder = "Assets/_Project/Prefabs/Construction";
        public const int MaxCullingBounds = 4096;
        public const int MaxCullingGridReferences = 1048576;
        public const int MockForcedInsideCount = 50;
        private const string ProceduralRuleFolder = "Assets/_Project/Data/World/ProceduralPlacementRules";
        private const string FloraProxyPrefabFolder = "Assets/_Project/Data/Flora/GeneratedProxies/Prefabs";
        private const string FloraNaturePrefabFolder = "Assets/_Project/Prefabs/Nature/Flora";
        private const string FloraWorldProxyPrefabFolder = "Assets/_Project/Prefabs/WorldProceduralProxy";
        private const string MapMagicPackageFolder = "Assets/MapMagic";
        private const int DefaultMockInstanceCount = 100000;
        private const int DefaultMockBoundsCount = 500;
        private const int ScheduleBatchSize = 64;
        private const int QualityBucketCount = 16;
        private const double DefaultSectorOriginX = -50000.0d;
        private const double DefaultSectorOriginY = -1800.0d;
        private const double DefaultSectorOriginZ = -50000.0d;

        public static ScatterPolishConfigDTO DefaultConfig(int instanceCount, float globalQualityWeight)
        {
            int normalResolution = AbyssalScatterPolisherConstants.DefaultTerrainNormalResolution;
            int sanitizedCount = math.clamp(instanceCount, 1, AbyssalScatterPolisherConstants.MaxGraphicsBufferElements);
            return new ScatterPolishConfigDTO
            {
                SectorOriginAup = new double3(DefaultSectorOriginX, DefaultSectorOriginY, DefaultSectorOriginZ),
                InstanceCount = sanitizedCount,
                TerrainNormalWidth = normalResolution,
                TerrainNormalHeight = normalResolution,
                TerrainCellSizeMeters = AbyssalScatterPolisherConstants.DefaultCellSizeMeters,
                TerrainOriginXZ = new float2(-normalResolution * AbyssalScatterPolisherConstants.DefaultCellSizeMeters * 0.5f),
                DefaultGroundPenetrationMeters = AbyssalScatterPolisherConstants.DefaultGroundPenetrationMeters,
                ScaleMultiplier = 1f,
                GlobalQualityWeight = math.saturate(globalQualityWeight),
                MinimumScale = 0.001f,
                CullingGridResolutionX = 32,
                CullingGridResolutionY = 8,
                CullingGridResolutionZ = 32,
                CullingCellSizeMeters = 24f,
                CullingGridOrigin = new float3(-384f, -192f, -384f),
                QualityPermutationStride = ResolvePermutationStride(sanitizedCount),
                Seed = 0x16141614u,
                Flags = 0u
            };
        }

        [MenuItem("HECTON-8/World Scatter/1614 Bake Mock BRG Scatter")]
        public static void BakeMockMenu()
        {
            if (!BakeMockScatterChunk(DefaultMockInstanceCount, DefaultMockBoundsCount, 1f, DefaultAssetName, out AbyssalScatterBakeResult result))
            {
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                return;
            }

            AssetDatabase.Refresh();
            Debug.Log("[1614] BRG scatter bake complete. path=" + result.OutputPath + " instances=" + result.InstanceCount + " culled=" + result.CulledCount + " bytes=" + result.FileBytes);
        }

        [MenuItem("HECTON-8/World Scatter/1614 Scan Scatter Sources")]
        public static void ScanSourcesMenu()
        {
            AbyssalScatterBakeResult result = default;
            ScanScatterSources(ref result, DefaultMapMagicOutputFolder, DefaultCullingDatasetFolder);
            Debug.Log("[1614] Scatter source scan complete. rules=" + result.SourceRuleCount + " prefabs=" + result.SourcePrefabCount + " mapMagicAssets=" + result.MapMagicAssetCount + " mapMagicFolder=" + result.MapMagicSourceFolder + " cullingFolder=" + result.CullingDatasetFolder);
        }

        public static bool BakeMockScatterChunk(
            int instanceCount,
            int boundsCount,
            float globalQualityWeight,
            string assetName,
            out AbyssalScatterBakeResult result)
        {
            return BakeMockScatterChunk(instanceCount, boundsCount, globalQualityWeight, assetName, DefaultMapMagicOutputFolder, DefaultCullingDatasetFolder, out result);
        }

        public static bool BakeMockScatterChunk(
            int instanceCount,
            int boundsCount,
            float globalQualityWeight,
            string assetName,
            string mapMagicOutputFolder,
            string cullingDatasetFolder,
            out AbyssalScatterBakeResult result)
        {
            result = default;
            try
            {
                result = BakeMockScatterChunkBlocking(instanceCount, boundsCount, globalQualityWeight, assetName, mapMagicOutputFolder, cullingDatasetFolder);
                return result.LayoutValid && result.FileBytes > 0L;
            }
            catch (Exception exception)
            {
                result = default;
                Debug.LogError("[1614] Scatter bake failed: " + exception.GetType().Name + " " + exception.Message);
                return false;
            }
        }

        private static AbyssalScatterBakeResult BakeMockScatterChunkBlocking(
            int instanceCount,
            int boundsCount,
            float globalQualityWeight,
            string assetName,
            string mapMagicOutputFolder,
            string cullingDatasetFolder)
        {
            if (instanceCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(instanceCount), "Instance count must be positive.");
            if (boundsCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(boundsCount), "Bounds count must be positive.");
            if (!IsCullingBoundsCountWithinBakeCap(boundsCount))
            {
                Debug.LogError("Culling Bounds Count Exceeds Bake Limits - Aborting Bake");
                return default;
            }

            if (instanceCount > AbyssalScatterPolisherConstants.MaxGraphicsBufferElements)
            {
                Debug.LogError("Instance Count Exceeds GraphicsBuffer Limits - Aborting Bake");
                return default;
            }

            ValidateLayoutsOrThrow();
            Directory.CreateDirectory(OutputFolder);
            Directory.CreateDirectory(MetadataFolder);
            Directory.CreateDirectory(PrefabFolder);

            AbyssalScatterBakeResult result = default;
            ScanScatterSources(ref result, mapMagicOutputFolder, cullingDatasetFolder);

            ScatterPolishConfigDTO config = DefaultConfig(instanceCount, globalQualityWeight);
            result.InstanceCount = config.InstanceCount;
            result.LayoutValid = true;

            NativeArray<ScatterInstanceDTO> instances = default;
            NativeArray<CullingBoundsDTO> bounds = default;
            NativeArray<CullingBoundsDTO> importedBounds = default;
            NativeArray<float4> terrainNormalHeight = default;
            NativeArray<float3> localPositions = default;
            NativeArray<float3> surfaceNormals = default;
            NativeArray<float4x4> matrices = default;
            NativeArray<BrgInstanceMetadataDTO> metadata = default;
            NativeArray<int> qualityIndices = default;
            NativeArray<int2> cellRanges = default;
            NativeArray<int> boundIndices = default;
            NativeArray<byte> culledMask = default;
            NativeArray<byte> nonFiniteMask = default;
            NativeArray<ScatterPolisherTelemetryEntry> telemetry = default;

            Stopwatch totalTimer = Stopwatch.StartNew();
            Stopwatch alignmentTimer = new Stopwatch();
            Stopwatch cullingTimer = new Stopwatch();
            Stopwatch serializationTimer = new Stopwatch();

            try
            {
                int terrainCellCount = checked(config.TerrainNormalWidth * config.TerrainNormalHeight);
                int gridCellCount = checked(config.CullingGridResolutionX * config.CullingGridResolutionY * config.CullingGridResolutionZ);
                instances = new NativeArray<ScatterInstanceDTO>(config.InstanceCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                importedBounds = new NativeArray<CullingBoundsDTO>(boundsCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                int importedBoundCount = WritePrefabCullingBounds(result.CullingDatasetFolder, config, importedBounds, out bool importedBoundsTruncated);
                bool useImportedBounds = importedBoundCount > 0;
                int effectiveBoundsCount = useImportedBounds ? importedBoundCount : boundsCount;
                bounds = new NativeArray<CullingBoundsDTO>(effectiveBoundsCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                if (useImportedBounds)
                {
                    for (int i = 0; i < importedBoundCount; i++)
                        bounds[i] = importedBounds[i];
                    result.ImportedCullingBoundsCount = importedBoundCount;
                    result.ImportedCullingBoundsTruncated = importedBoundsTruncated;
                }
                else
                {
                    result.MockCullingBoundsCount = boundsCount;
                }

                result.CullingBoundsCount = effectiveBoundsCount;
                terrainNormalHeight = new NativeArray<float4>(terrainCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                localPositions = new NativeArray<float3>(config.InstanceCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                surfaceNormals = new NativeArray<float3>(config.InstanceCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                matrices = new NativeArray<float4x4>(config.InstanceCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                metadata = new NativeArray<BrgInstanceMetadataDTO>(config.InstanceCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                qualityIndices = new NativeArray<int>(config.InstanceCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                culledMask = new NativeArray<byte>(config.InstanceCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                nonFiniteMask = new NativeArray<byte>(config.InstanceCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                telemetry = new NativeArray<ScatterPolisherTelemetryEntry>(AbyssalScatterPolisherConstants.TelemetryFrames, Allocator.Persistent, NativeArrayOptions.ClearMemory);

                alignmentTimer.Start();
                JobHandle inputHandle = new GenerateMockScatterInputJob
                {
                    Instances = instances,
                    Config = config,
                    ForcedInsideCount = MockForcedInsideCount
                }.Schedule(config.InstanceCount, ScheduleBatchSize);
                JobHandle terrainHandle = new GenerateMockTerrainNormalsJob
                {
                    TerrainNormalHeight = terrainNormalHeight,
                    Config = config
                }.Schedule(terrainCellCount, ScheduleBatchSize);
                JobHandle boundsHandle = default;
                if (!useImportedBounds)
                {
                    boundsHandle = new GenerateMockCullingBoundsJob
                    {
                        Bounds = bounds,
                        Config = config
                    }.Schedule(bounds.Length, ScheduleBatchSize);
                }
                JobHandle preAlignHandle = JobHandle.CombineDependencies(inputHandle, terrainHandle);
                JobHandle offsetHandle = new ApplyGroundPenetrationOffsetJob
                {
                    Instances = instances,
                    TerrainNormalHeight = terrainNormalHeight,
                    LocalPositions = localPositions,
                    SurfaceNormals = surfaceNormals,
                    NonFiniteMask = nonFiniteMask,
                    Config = config
                }.Schedule(config.InstanceCount, ScheduleBatchSize, preAlignHandle);
                JobHandle alignHandle = new AlignScatterToTerrainNormalJob
                {
                    Instances = instances,
                    LocalPositions = localPositions,
                    SurfaceNormals = surfaceNormals,
                    Matrices = matrices,
                    Metadata = metadata,
                    NonFiniteMask = nonFiniteMask,
                    Config = config
                }.Schedule(config.InstanceCount, ScheduleBatchSize, offsetHandle);

                // EDITOR BLOCKING SYNC POINT: required before building the cold spatial grid from generated bounds.
                JobHandle.CombineDependencies(alignHandle, boundsHandle).Complete();
                alignmentTimer.Stop();

                BuildCullingGrid(config, bounds, gridCellCount, out cellRanges, out boundIndices);

                cullingTimer.Start();
                JobHandle cullHandle = new CullScatterInsideBoundsJob
                {
                    LocalPositions = localPositions,
                    Bounds = bounds,
                    CellRanges = cellRanges,
                    BoundIndices = boundIndices,
                    Matrices = matrices,
                    CulledMask = culledMask,
                    Config = config
                }.Schedule(config.InstanceCount, ScheduleBatchSize);
                // EDITOR BLOCKING SYNC POINT: culling must finish before atomic file serialization.
                cullHandle.Complete();
                BuildQualityDeductionMap(config, instances, qualityIndices);
                ValidateQualityDeductionMap(qualityIndices, config.InstanceCount);
                cullingTimer.Stop();

                result.CulledCount = CountCulled(culledMask);
                result.NonFiniteCount = CountMarked(nonFiniteMask);
                result.AlignmentMilliseconds = (float)alignmentTimer.Elapsed.TotalMilliseconds;
                result.CullingMilliseconds = (float)cullingTimer.Elapsed.TotalMilliseconds;
                result.ContentHash = HashContent(matrices, metadata, qualityIndices, result.CulledCount);

                string outputPath = CombineAssetPath(OutputFolder, SanitizeBrgAssetName(assetName));
                serializationTimer.Start();
                WriteBrgDataAtomic(outputPath, matrices, metadata, qualityIndices, result.ContentHash, out BrgDataHeaderDTO header);
                serializationTimer.Stop();

                result.OutputPath = outputPath;
                ValidateBakedBinaryPayloadOrThrow(outputPath, header);
                result.FileBytes = new FileInfo(AssetPathToFileSystemPath(outputPath)).Length;
                result.SerializationMilliseconds = (float)serializationTimer.Elapsed.TotalMilliseconds;

                result.MetadataPath = WriteMetadataAsset(result, header, config);
                result.PrefabPath = WriteScatterPrefab(result.MetadataPath, result.OutputPath, out bool prefabHasRendererController);
                result.PrefabHasRendererController = prefabHasRendererController;
                result.TotalMilliseconds = (float)totalTimer.Elapsed.TotalMilliseconds;

                RecordTelemetry(telemetry, 0, 0x414C4947u, result, config);
                return result;
            }
            finally
            {
                totalTimer.Stop();
                DisposeIfCreated(ref instances);
                DisposeIfCreated(ref bounds);
                DisposeIfCreated(ref importedBounds);
                DisposeIfCreated(ref terrainNormalHeight);
                DisposeIfCreated(ref localPositions);
                DisposeIfCreated(ref surfaceNormals);
                DisposeIfCreated(ref matrices);
                DisposeIfCreated(ref metadata);
                DisposeIfCreated(ref qualityIndices);
                DisposeIfCreated(ref cellRanges);
                DisposeIfCreated(ref boundIndices);
                DisposeIfCreated(ref culledMask);
                DisposeIfCreated(ref nonFiniteMask);
                DisposeIfCreated(ref telemetry);
            }
        }

        internal static bool IsCullingBoundsCountWithinBakeCap(int boundsCount)
        {
            return boundsCount > 0 && boundsCount <= MaxCullingBounds;
        }

        private static void BuildCullingGrid(
            ScatterPolishConfigDTO config,
            NativeArray<CullingBoundsDTO> bounds,
            int gridCellCount,
            out NativeArray<int2> cellRanges,
            out NativeArray<int> boundIndices)
        {
            NativeArray<int> counts = default;
            NativeArray<int> offsets = default;
            NativeArray<int> cursors = default;
            NativeArray<int> indices = default;
            try
            {
                counts = new NativeArray<int>(gridCellCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
                for (int i = 0; i < bounds.Length; i++)
                {
                    CullingBoundsDTO bound = bounds[i];
                    ValidateCullingBoundOrThrow(bound, i);
                    ResolveBoundsCellRange(config, bound, out int3 minCell, out int3 maxCell);
                    for (int z = minCell.z; z <= maxCell.z; z++)
                    for (int y = minCell.y; y <= maxCell.y; y++)
                    for (int x = minCell.x; x <= maxCell.x; x++)
                    {
                        int cellIndex = FlattenCell(config, x, y, z);
                        counts[cellIndex]++;
                    }
                }

                long totalRefs = 0L;
                for (int i = 0; i < counts.Length; i++)
                    totalRefs += counts[i];

                if (totalRefs > MaxCullingGridReferences)
                    throw new InvalidOperationException("Culling grid reference count exceeds bake limits.");

                offsets = new NativeArray<int>(gridCellCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                int running = 0;
                for (int i = 0; i < counts.Length; i++)
                {
                    offsets[i] = running;
                    running += counts[i];
                }

                cursors = new NativeArray<int>(gridCellCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
                indices = new NativeArray<int>(math.max(1, (int)totalRefs), Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < bounds.Length; i++)
                {
                    CullingBoundsDTO bound = bounds[i];
                    ResolveBoundsCellRange(config, bound, out int3 minCell, out int3 maxCell);
                    for (int z = minCell.z; z <= maxCell.z; z++)
                    for (int y = minCell.y; y <= maxCell.y; y++)
                    for (int x = minCell.x; x <= maxCell.x; x++)
                    {
                        int cellIndex = FlattenCell(config, x, y, z);
                        int writeIndex = offsets[cellIndex] + cursors[cellIndex];
                        indices[writeIndex] = i;
                        cursors[cellIndex]++;
                    }
                }

                cellRanges = new NativeArray<int2>(gridCellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                boundIndices = new NativeArray<int>(indices.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < gridCellCount; i++)
                    cellRanges[i] = new int2(offsets[i], offsets[i] + counts[i]);
                for (int i = 0; i < indices.Length; i++)
                    boundIndices[i] = indices[i];
            }
            finally
            {
                DisposeIfCreated(ref counts);
                DisposeIfCreated(ref offsets);
                DisposeIfCreated(ref cursors);
                DisposeIfCreated(ref indices);
            }
        }

        private static void ValidateCullingBoundOrThrow(CullingBoundsDTO bound, int index)
        {
            if (!math.all(math.isfinite(bound.CenterAup)) ||
                !math.all(math.isfinite(bound.Extents)) ||
                !math.isfinite(bound.PaddingMeters) ||
                bound.Extents.x < 0f ||
                bound.Extents.y < 0f ||
                bound.Extents.z < 0f)
            {
                throw new InvalidOperationException("Culling bound contains non-finite or negative values at index " + index);
            }
        }

        private static void ResolveBoundsCellRange(
            ScatterPolishConfigDTO config,
            CullingBoundsDTO bound,
            out int3 minCell,
            out int3 maxCell)
        {
            float3 center = (float3)(bound.CenterAup - config.SectorOriginAup);
            float3 extents = math.max(bound.Extents + new float3(math.max(0f, bound.PaddingMeters)), new float3(0.01f));
            float3 min = center - extents;
            float3 max = center + extents;
            minCell = (int3)math.floor((min - config.CullingGridOrigin) / config.CullingCellSizeMeters);
            maxCell = (int3)math.floor((max - config.CullingGridOrigin) / config.CullingCellSizeMeters);
            minCell = math.clamp(minCell, int3.zero, new int3(config.CullingGridResolutionX - 1, config.CullingGridResolutionY - 1, config.CullingGridResolutionZ - 1));
            maxCell = math.clamp(maxCell, int3.zero, new int3(config.CullingGridResolutionX - 1, config.CullingGridResolutionY - 1, config.CullingGridResolutionZ - 1));
        }

        private static int FlattenCell(ScatterPolishConfigDTO config, int x, int y, int z)
        {
            return (z * config.CullingGridResolutionY + y) * config.CullingGridResolutionX + x;
        }

        private static int CountCulled(NativeArray<byte> culledMask)
        {
            return CountMarked(culledMask);
        }

        private static int CountMarked(NativeArray<byte> mask)
        {
            int count = 0;
            for (int i = 0; i < mask.Length; i++)
                count += mask[i] != 0 ? 1 : 0;
            return count;
        }

        private static void BuildQualityDeductionMap(
            ScatterPolishConfigDTO config,
            NativeArray<ScatterInstanceDTO> instances,
            NativeArray<int> qualityIndices)
        {
            int count = math.min(instances.Length, qualityIndices.Length);
            if (count <= 0)
                return;

            NativeArray<int> bucketCounts = default;
            NativeArray<int> bucketOffsets = default;
            NativeArray<int> bucketCursors = default;
            try
            {
                bucketCounts = new NativeArray<int>(QualityBucketCount, Allocator.Temp, NativeArrayOptions.ClearMemory);
                bucketOffsets = new NativeArray<int>(QualityBucketCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                bucketCursors = new NativeArray<int>(QualityBucketCount, Allocator.Temp, NativeArrayOptions.ClearMemory);

                for (int i = 0; i < count; i++)
                {
                    int bucket = ResolveQualityBucket(instances[i].Importance);
                    bucketCounts[bucket]++;
                }

                int running = 0;
                for (int bucket = QualityBucketCount - 1; bucket >= 0; bucket--)
                {
                    bucketOffsets[bucket] = running;
                    running += bucketCounts[bucket];
                }

                int stride = ResolvePermutationStride(count);
                int seedOffset = (int)(config.Seed % (uint)count);
                for (int rank = 0; rank < count; rank++)
                {
                    int instanceIndex = (int)(((long)rank * stride + seedOffset) % count);
                    int bucket = ResolveQualityBucket(instances[instanceIndex].Importance);
                    int writeIndex = bucketOffsets[bucket] + bucketCursors[bucket];
                    qualityIndices[writeIndex] = instanceIndex;
                    bucketCursors[bucket] = bucketCursors[bucket] + 1;
                }
            }
            finally
            {
                DisposeIfCreated(ref bucketCounts);
                DisposeIfCreated(ref bucketOffsets);
                DisposeIfCreated(ref bucketCursors);
            }
        }

        private static void ValidateQualityDeductionMap(NativeArray<int> qualityIndices, int instanceCount)
        {
            int count = math.min(qualityIndices.Length, instanceCount);
            if (count <= 0)
                return;

            NativeArray<byte> seen = default;
            try
            {
                seen = new NativeArray<byte>(count, Allocator.Temp, NativeArrayOptions.ClearMemory);
                for (int i = 0; i < count; i++)
                {
                    int instanceIndex = qualityIndices[i];
                    if ((uint)instanceIndex >= (uint)count)
                        throw new InvalidOperationException("Quality deduction map contains out-of-range index " + instanceIndex);
                    if (seen[instanceIndex] != 0)
                        throw new InvalidOperationException("Quality deduction map contains duplicate index " + instanceIndex);
                    seen[instanceIndex] = 1;
                }
            }
            finally
            {
                DisposeIfCreated(ref seen);
            }
        }

        private static int ResolveQualityBucket(float importance)
        {
            return math.clamp((int)math.floor(math.saturate(importance) * QualityBucketCount), 0, QualityBucketCount - 1);
        }

        private static Bounds ResolveBakedDrawBounds(ScatterPolishConfigDTO config)
        {
            float cellSize = math.max(0.01f, config.CullingCellSizeMeters);
            Vector3 size = new Vector3(
                math.max(1, config.CullingGridResolutionX) * cellSize,
                math.max(1, config.CullingGridResolutionY) * cellSize,
                math.max(1, config.CullingGridResolutionZ) * cellSize);
            Vector3 origin = new Vector3(
                config.CullingGridOrigin.x,
                config.CullingGridOrigin.y,
                config.CullingGridOrigin.z);
            return new Bounds(origin + size * 0.5f, size);
        }

        private static string WriteMetadataAsset(AbyssalScatterBakeResult result, BrgDataHeaderDTO header, ScatterPolishConfigDTO config)
        {
            string metadataPath = CombineAssetPath(MetadataFolder, ResolveMetadataAssetName(result.OutputPath));
            AbyssalScatterBrgMetadataAsset asset = AssetDatabase.LoadAssetAtPath<AbyssalScatterBrgMetadataAsset>(metadataPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<AbyssalScatterBrgMetadataAsset>();
                AssetDatabase.CreateAsset(asset, metadataPath);
            }

            asset.binaryAssetPath = result.OutputPath;
            asset.matrixCount = header.MatrixCount;
            asset.metadataCount = header.MetadataCount;
            asset.qualityIndexCount = header.QualityIndexCount;
            asset.matrixStrideBytes = header.MatrixStrideBytes;
            asset.metadataStrideBytes = header.MetadataStrideBytes;
            asset.qualityIndexStrideBytes = header.QualityIndexStrideBytes;
            asset.matrixOffsetBytes = header.MatrixOffsetBytes;
            asset.metadataOffsetBytes = header.MetadataOffsetBytes;
            asset.qualityIndexOffsetBytes = header.QualityIndexOffsetBytes;
            asset.contentHash = header.ContentHash;
            asset.headerHash = header.HeaderHash;
            asset.chunkHash = header.ChunkHash;
            asset.binaryFileBytes = result.FileBytes;
            asset.bakedDrawBounds = ResolveBakedDrawBounds(config);
            asset.globalQualityWeightAtBake = config.GlobalQualityWeight;
            asset.lowDeviceDrawFraction = ResolveDrawFraction(0.18f, config.GlobalQualityWeight);
            asset.middleDeviceDrawFraction = ResolveDrawFraction(0.45f, config.GlobalQualityWeight);
            asset.highDeviceDrawFraction = ResolveDrawFraction(0.76f, config.GlobalQualityWeight);
            asset.ultraDeviceDrawFraction = ResolveDrawFraction(1.0f, config.GlobalQualityWeight);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return metadataPath;
        }

        private static string WriteScatterPrefab(string metadataPath, string binaryPath, out bool hasRendererController)
        {
            string binaryFile = AssetPathToFileSystemPath(binaryPath);
            if (!File.Exists(binaryFile))
                throw new FileNotFoundException("Cannot create scatter prefab without baked binary payload.", binaryFile);

            string prefabPath = CombineAssetPath(PrefabFolder, ResolvePrefabAssetName(binaryPath));
            GameObject root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                AbyssalScatterBrgMetadataAsset metadata = AssetDatabase.LoadAssetAtPath<AbyssalScatterBrgMetadataAsset>(metadataPath);
                if (metadata == null)
                    throw new FileNotFoundException("Cannot create scatter prefab without metadata asset.", metadataPath);

                GpuScatterLodManager controller = root.AddComponent<GpuScatterLodManager>();
                hasRendererController = controller != null;
                if (controller != null)
                {
                    ConfigureRendererController(controller, metadataPath, binaryPath);

                    AbyssalScatterBrgDataVaultBootstrap bootstrap = root.AddComponent<AbyssalScatterBrgDataVaultBootstrap>();
                    bootstrap.ConfigureCold(
                        controller,
                        binaryPath,
                        metadata.contentHash,
                        metadata.headerHash,
                        metadata.matrixCount,
                        metadata.metadataCount,
                        metadata.qualityIndexCount,
                        metadata.bakedDrawBounds);
                    EditorUtility.SetDirty(bootstrap);
                }

                root.SetActive(true);
                EditorUtility.SetDirty(metadata);

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (savedPrefab == null)
                {
                    hasRendererController = false;
                    throw new InvalidOperationException("PrefabUtility.SaveAsPrefabAsset returned null for " + prefabPath);
                }

                return prefabPath;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ConfigureRendererController(GpuScatterLodManager controller, string metadataPath, string binaryPath)
        {
            AbyssalScatterBrgMetadataAsset metadata = AssetDatabase.LoadAssetAtPath<AbyssalScatterBrgMetadataAsset>(metadataPath);
            int capacity = metadata == null ? 100000 : math.max(1, metadata.matrixCount);
            Bounds bakedBounds = metadata == null || metadata.bakedDrawBounds.size == Vector3.zero
                ? new Bounds(Vector3.zero, new Vector3(820f, 260f, 820f))
                : metadata.bakedDrawBounds;
            SerializedObject serialized = new SerializedObject(controller);
            SetSerializedInt(serialized, "instanceCapacity", capacity);
            SetSerializedInt(serialized, "initialActiveInstanceCount", capacity);
            SetSerializedFloat(serialized, "lowTierCullDistanceMeters", 120f);
            SetSerializedFloat(serialized, "midTierCullDistanceMeters", 320f);
            SetSerializedFloat(serialized, "highTierCullDistanceMeters", 640f);
            SetSerializedFloat(serialized, "lodCrossfadeRangeMeters", 18f);
            SetSerializedFloat(serialized, "swayMotionStrength", 0.045f);
            SetSerializedFloat(serialized, "lowTierAnisotropicSssStrength", 0.42f);
            SetSerializedFloat(serialized, "highTierAnisotropicSssStrength", 1.25f);
            SetSerializedFloat(serialized, "lowTierOrganicSssScale", 0.62f);
            SetSerializedFloat(serialized, "highTierOrganicSssScale", 1.72f);
            SetSerializedFloat(serialized, "lowTierEdgeBloomStrength", 0.22f);
            SetSerializedFloat(serialized, "highTierEdgeBloomStrength", 0.96f);
            SetSerializedFloat(serialized, "lowTierLocalCausticStrength", 0.06f);
            SetSerializedFloat(serialized, "highTierLocalCausticStrength", 0.38f);
            SetSerializedBool(serialized, "receiveShadows", false);
            SetSerializedBool(serialized, "enableBurstCullAudit", false);
            SetSerializedBool(serialized, "enableVisibleCountReadback", false);
            SetSerializedEnum(serialized, "shadowCastingMode", (int)UnityEngine.Rendering.ShadowCastingMode.Off);
            SerializedProperty drawBounds = RequireSerializedProperty(serialized, "fallbackDrawBounds");
            drawBounds.boundsValue = bakedBounds;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            controller.name = "BRG Scatter LOD Manager 1614 " + Path.GetFileNameWithoutExtension(binaryPath);
        }

        private static void SetSerializedInt(SerializedObject serialized, string propertyName, int value)
        {
            RequireSerializedProperty(serialized, propertyName).intValue = value;
        }

        private static void SetSerializedFloat(SerializedObject serialized, string propertyName, float value)
        {
            RequireSerializedProperty(serialized, propertyName).floatValue = value;
        }

        private static void SetSerializedBool(SerializedObject serialized, string propertyName, bool value)
        {
            RequireSerializedProperty(serialized, propertyName).boolValue = value;
        }

        private static void SetSerializedEnum(SerializedObject serialized, string propertyName, int value)
        {
            RequireSerializedProperty(serialized, propertyName).enumValueIndex = value;
        }

        private static SerializedProperty RequireSerializedProperty(SerializedObject serialized, string propertyName)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                string typeName = serialized.targetObject == null ? "null" : serialized.targetObject.GetType().FullName;
                throw new MissingFieldException(typeName, propertyName);
            }

            return property;
        }

        private static void WriteBrgDataAtomic(
            string assetPath,
            NativeArray<float4x4> matrices,
            NativeArray<BrgInstanceMetadataDTO> metadata,
            NativeArray<int> qualityIndices,
            uint contentHash,
            out BrgDataHeaderDTO header)
        {
            ValidatePayloadArrayLengthsOrThrow(matrices.Length, metadata.Length, qualityIndices.Length);
            string filePath = AssetPathToFileSystemPath(assetPath);
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            uint matrixOffset = AbyssalScatterPolisherConstants.HeaderSizeBytes;
            uint metadataOffset = checked(matrixOffset + (uint)((long)matrices.Length * AbyssalScatterPolisherConstants.MatrixStrideBytes));
            uint qualityOffset = checked(metadataOffset + (uint)((long)metadata.Length * AbyssalScatterPolisherConstants.MetadataStrideBytes));
            header = new BrgDataHeaderDTO
            {
                Magic = AbyssalScatterPolisherConstants.FileMagic,
                Version = AbyssalScatterPolisherConstants.FileVersion,
                HeaderBytes = AbyssalScatterPolisherConstants.HeaderSizeBytes,
                Flags = AbyssalScatterPolisherConstants.FileFlagHasMetadata | AbyssalScatterPolisherConstants.FileFlagHasQualityIndex,
                MatrixCount = matrices.Length,
                MetadataCount = metadata.Length,
                QualityIndexCount = qualityIndices.Length,
                MatrixStrideBytes = AbyssalScatterPolisherConstants.MatrixStrideBytes,
                MetadataStrideBytes = AbyssalScatterPolisherConstants.MetadataStrideBytes,
                QualityIndexStrideBytes = AbyssalScatterPolisherConstants.QualityIndexStrideBytes,
                MatrixOffsetBytes = matrixOffset,
                MetadataOffsetBytes = metadataOffset,
                QualityIndexOffsetBytes = qualityOffset,
                ChunkHash = HashPath(assetPath),
                ContentHash = contentHash,
                HeaderHash = HashHeaderSeed(matrices.Length, metadata.Length, qualityIndices.Length, contentHash)
            };

            string tempPath = filePath + ".tmp";
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 65536, FileOptions.WriteThrough))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                WriteHeader(writer, header);
                WriteMatrices(writer, matrices);
                WriteMetadata(writer, metadata);
                WriteQualityIndices(writer, qualityIndices);
                writer.Flush();
            }

            if (File.Exists(filePath))
                File.Replace(tempPath, filePath, null);
            else
                File.Move(tempPath, filePath);
        }

        private static void ValidatePayloadArrayLengthsOrThrow(int matrixCount, int metadataCount, int qualityIndexCount)
        {
            if (matrixCount <= 0 ||
                metadataCount != matrixCount ||
                qualityIndexCount != matrixCount)
            {
                throw new InvalidOperationException("BRG payload block lengths must be positive and identical before serialization.");
            }
        }

        private static void ValidateBakedBinaryPayloadOrThrow(string assetPath, BrgDataHeaderDTO expected)
        {
            string filePath = AssetPathToFileSystemPath(assetPath);
            FileInfo info = new FileInfo(filePath);
            if (!info.Exists)
                throw new FileNotFoundException("BRG payload was not written.", filePath);

            long expectedBytes = ComputeExpectedBrgByteLength(expected);
            if (info.Length != expectedBytes)
                throw new InvalidOperationException("BRG payload length mismatch. expected=" + expectedBytes + " actual=" + info.Length);

            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                BrgDataHeaderDTO actual = ReadHeader(reader);
                if (actual.Magic != expected.Magic ||
                    actual.Version != expected.Version ||
                    actual.HeaderBytes != expected.HeaderBytes ||
                    actual.Flags != expected.Flags ||
                    actual.MatrixCount != expected.MatrixCount ||
                    actual.MetadataCount != expected.MetadataCount ||
                    actual.QualityIndexCount != expected.QualityIndexCount ||
                    actual.MatrixStrideBytes != expected.MatrixStrideBytes ||
                    actual.MetadataStrideBytes != expected.MetadataStrideBytes ||
                    actual.QualityIndexStrideBytes != expected.QualityIndexStrideBytes ||
                    actual.MatrixOffsetBytes != expected.MatrixOffsetBytes ||
                    actual.MetadataOffsetBytes != expected.MetadataOffsetBytes ||
                    actual.QualityIndexOffsetBytes != expected.QualityIndexOffsetBytes ||
                    actual.ChunkHash != expected.ChunkHash ||
                    actual.ContentHash != expected.ContentHash ||
                    actual.HeaderHash != expected.HeaderHash)
                {
                    throw new InvalidOperationException("BRG payload header verification failed for " + assetPath);
                }
            }
        }

        private static BrgDataHeaderDTO ReadHeader(BinaryReader reader)
        {
            return new BrgDataHeaderDTO
            {
                Magic = reader.ReadUInt32(),
                Version = reader.ReadUInt32(),
                HeaderBytes = reader.ReadUInt32(),
                Flags = reader.ReadUInt32(),
                MatrixCount = reader.ReadInt32(),
                MetadataCount = reader.ReadInt32(),
                QualityIndexCount = reader.ReadInt32(),
                MatrixStrideBytes = reader.ReadInt32(),
                MetadataStrideBytes = reader.ReadInt32(),
                QualityIndexStrideBytes = reader.ReadInt32(),
                MatrixOffsetBytes = reader.ReadUInt32(),
                MetadataOffsetBytes = reader.ReadUInt32(),
                QualityIndexOffsetBytes = reader.ReadUInt32(),
                ChunkHash = reader.ReadUInt32(),
                ContentHash = reader.ReadUInt32(),
                HeaderHash = reader.ReadUInt32()
            };
        }

        private static long ComputeExpectedBrgByteLength(BrgDataHeaderDTO header)
        {
            return checked((long)header.QualityIndexOffsetBytes + (long)header.QualityIndexCount * header.QualityIndexStrideBytes);
        }

        private static void WriteHeader(BinaryWriter writer, BrgDataHeaderDTO header)
        {
            writer.Write(header.Magic);
            writer.Write(header.Version);
            writer.Write(header.HeaderBytes);
            writer.Write(header.Flags);
            writer.Write(header.MatrixCount);
            writer.Write(header.MetadataCount);
            writer.Write(header.QualityIndexCount);
            writer.Write(header.MatrixStrideBytes);
            writer.Write(header.MetadataStrideBytes);
            writer.Write(header.QualityIndexStrideBytes);
            writer.Write(header.MatrixOffsetBytes);
            writer.Write(header.MetadataOffsetBytes);
            writer.Write(header.QualityIndexOffsetBytes);
            writer.Write(header.ChunkHash);
            writer.Write(header.ContentHash);
            writer.Write(header.HeaderHash);
        }

        private static void WriteMatrices(BinaryWriter writer, NativeArray<float4x4> matrices)
        {
            for (int i = 0; i < matrices.Length; i++)
            {
                float4x4 m = matrices[i];
                WriteFloat4(writer, m.c0);
                WriteFloat4(writer, m.c1);
                WriteFloat4(writer, m.c2);
                WriteFloat4(writer, m.c3);
            }
        }

        private static void WriteMetadata(BinaryWriter writer, NativeArray<BrgInstanceMetadataDTO> metadata)
        {
            for (int i = 0; i < metadata.Length; i++)
            {
                BrgInstanceMetadataDTO m = metadata[i];
                writer.Write(m.Type);
                writer.Write(m.HeightScale);
                writer.Write(m.WidthScale);
                writer.Write(m.Variation);
                writer.Write(m.TemplateIndex);
                writer.Write(m.RuntimeState);
                writer.Write(m.RuntimeFlags);
                writer.Write(m.PulseFrequency);
                WriteFloat4(writer, m.BioluminescenceColor);
                writer.Write(m.SwaySpeed);
                writer.Write(m.BendAmplitude);
                writer.Write(m.HealthNormalized);
                writer.Write(m.Reserved0);
            }
        }

        private static void WriteQualityIndices(BinaryWriter writer, NativeArray<int> qualityIndices)
        {
            for (int i = 0; i < qualityIndices.Length; i++)
                writer.Write(qualityIndices[i]);
        }

        private static void WriteFloat4(BinaryWriter writer, float4 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
            writer.Write(value.w);
        }

        private static uint HashContent(
            NativeArray<float4x4> matrices,
            NativeArray<BrgInstanceMetadataDTO> metadata,
            NativeArray<int> qualityIndices,
            int culledCount)
        {
            uint hash = 2166136261u;
            hash = HashCombine(hash, (uint)matrices.Length);
            hash = HashCombine(hash, (uint)metadata.Length);
            hash = HashCombine(hash, (uint)qualityIndices.Length);
            hash = HashCombine(hash, (uint)culledCount);
            int stride = math.max(1, matrices.Length / 1024);
            for (int i = 0; i < matrices.Length; i += stride)
            {
                float4x4 m = matrices[i];
                hash = HashCombine(hash, math.asuint(m.c3.x));
                hash = HashCombine(hash, math.asuint(m.c3.y));
                hash = HashCombine(hash, math.asuint(m.c3.z));
                hash = HashCombine(hash, math.asuint(metadata[i].Reserved0));
                hash = HashCombine(hash, (uint)qualityIndices[i]);
            }

            return hash;
        }

        private static uint HashCombine(uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
            return hash;
        }

        private static uint HashPath(string path)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < path.Length; i++)
            {
                hash ^= path[i];
                hash *= 16777619u;
            }

            return hash;
        }

        private static uint HashHeaderSeed(int matrixCount, int metadataCount, int qualityCount, uint contentHash)
        {
            uint hash = 2166136261u;
            hash = HashCombine(hash, (uint)matrixCount);
            hash = HashCombine(hash, (uint)metadataCount);
            hash = HashCombine(hash, (uint)qualityCount);
            hash = HashCombine(hash, contentHash);
            return hash;
        }

        internal static void ScanScatterSourcesForFolders(string mapMagicOutputFolder, string cullingDatasetFolder, out AbyssalScatterBakeResult result)
        {
            result = default;
            ScanScatterSources(ref result, mapMagicOutputFolder, cullingDatasetFolder);
        }

        private static void ScanScatterSources(ref AbyssalScatterBakeResult result, string mapMagicOutputFolder, string cullingDatasetFolder)
        {
            bool mapMagicFolderValid = TryResolveAssetFolderOrDefault(mapMagicOutputFolder, DefaultMapMagicOutputFolder, out string resolvedMapMagicFolder);
            bool cullingFolderValid = TryResolveAssetFolderOrDefault(cullingDatasetFolder, DefaultCullingDatasetFolder, out string resolvedCullingFolder);

            string[] ruleFolders = BuildValidSearchFolders(ProceduralRuleFolder, string.Empty, string.Empty, string.Empty);
            string[] floraFolders = BuildValidSearchFolders(resolvedCullingFolder, FloraProxyPrefabFolder, FloraNaturePrefabFolder, FloraWorldProxyPrefabFolder);
            string[] mapMagicFolders = BuildValidSearchFolders(resolvedMapMagicFolder, MapMagicPackageFolder, string.Empty, string.Empty);

            result.MapMagicSourceFolder = resolvedMapMagicFolder;
            result.CullingDatasetFolder = resolvedCullingFolder;
            result.SourceRuleCount = CountAssets("ProceduralRule", ruleFolders);
            result.SourcePrefabCount = CountAssets("t:Prefab", floraFolders);
            result.MapMagicAssetCount = CountAssets("MapMagic", mapMagicFolders);
            result.SourceFoldersValid = mapMagicFolderValid && cullingFolderValid;
        }

        private static bool TryResolveAssetFolderOrDefault(string folder, string fallbackFolder, out string resolvedFolder)
        {
            string normalized = NormalizeAssetFolder(folder);
            if (normalized.Length > 0 && AssetDatabase.IsValidFolder(normalized))
            {
                resolvedFolder = normalized;
                return true;
            }

            resolvedFolder = AssetDatabase.IsValidFolder(fallbackFolder) ? fallbackFolder : string.Empty;
            return false;
        }

        private static string NormalizeAssetFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder))
                return string.Empty;

            string normalized = folder.Trim().Replace('\\', '/');
            if (normalized.Length == 0)
                return string.Empty;

            while (normalized.Length > "Assets".Length && normalized.EndsWith("/", StringComparison.Ordinal))
                normalized = normalized.Substring(0, normalized.Length - 1);

            if (normalized == "Assets" || normalized.StartsWith("Assets/", StringComparison.Ordinal))
                return normalized;

            return string.Empty;
        }

        private static string[] BuildValidSearchFolders(string first, string second, string third, string fourth)
        {
            string[] candidates = { first, second, third, fourth };
            string[] valid = new string[candidates.Length];
            int count = 0;
            for (int i = 0; i < candidates.Length; i++)
            {
                string normalized = NormalizeAssetFolder(candidates[i]);
                if (normalized.Length == 0 || !AssetDatabase.IsValidFolder(normalized))
                    continue;

                bool duplicate = false;
                for (int j = 0; j < count; j++)
                {
                    if (valid[j] == normalized)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                    valid[count++] = normalized;
            }

            if (count == valid.Length)
                return valid;

            string[] trimmed = new string[count];
            Array.Copy(valid, trimmed, count);
            return trimmed;
        }

        private static int CountAssets(string filter, string[] searchFolders)
        {
            if (searchFolders == null || searchFolders.Length == 0)
                return 0;

            string[] guids = AssetDatabase.FindAssets(filter, searchFolders);
            return guids == null ? 0 : guids.Length;
        }

        private static int WritePrefabCullingBounds(
            string cullingDatasetFolder,
            ScatterPolishConfigDTO config,
            NativeArray<CullingBoundsDTO> destination,
            out bool truncated)
        {
            truncated = false;
            if (!destination.IsCreated || destination.Length == 0 || !AssetDatabase.IsValidFolder(cullingDatasetFolder))
                return 0;

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { cullingDatasetFolder });
            if (prefabGuids == null || prefabGuids.Length == 0)
                return 0;

            int writeCount = 0;
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (string.IsNullOrEmpty(prefabPath))
                    continue;

                writeCount += WritePrefabCullingBoundsFromPath(prefabPath, config, destination, writeCount, out bool pathTruncated);
                if (pathTruncated || writeCount >= destination.Length)
                {
                    truncated = true;
                    break;
                }
            }

            return writeCount;
        }

        private static int WritePrefabCullingBoundsFromPath(
            string prefabPath,
            ScatterPolishConfigDTO config,
            NativeArray<CullingBoundsDTO> destination,
            int startIndex,
            out bool truncated)
        {
            truncated = false;
            if ((uint)startIndex >= (uint)destination.Length)
            {
                truncated = true;
                return 0;
            }

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                if (root == null)
                    return 0;

                Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
                int written = WriteColliderCullingBounds(prefabPath, config, colliders, destination, startIndex, out truncated);
                if (written > 0 || truncated)
                    return written;

                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                return WriteRendererCullingBounds(prefabPath, config, renderers, destination, startIndex, out truncated);
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static int WriteColliderCullingBounds(
            string prefabPath,
            ScatterPolishConfigDTO config,
            Collider[] colliders,
            NativeArray<CullingBoundsDTO> destination,
            int startIndex,
            out bool truncated)
        {
            truncated = false;
            if (colliders == null || colliders.Length == 0)
                return 0;

            int written = 0;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !collider.enabled)
                    continue;

                if (!TryWriteUnityBounds(prefabPath, i, collider.bounds, config, destination, startIndex + written))
                {
                    if ((uint)(startIndex + written) >= (uint)destination.Length)
                    {
                        truncated = true;
                        return written;
                    }

                    continue;
                }

                written++;
            }

            return written;
        }

        private static int WriteRendererCullingBounds(
            string prefabPath,
            ScatterPolishConfigDTO config,
            Renderer[] renderers,
            NativeArray<CullingBoundsDTO> destination,
            int startIndex,
            out bool truncated)
        {
            truncated = false;
            if (renderers == null || renderers.Length == 0)
                return 0;

            int written = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                    continue;

                if (!TryWriteUnityBounds(prefabPath, i, renderer.bounds, config, destination, startIndex + written))
                {
                    if ((uint)(startIndex + written) >= (uint)destination.Length)
                    {
                        truncated = true;
                        return written;
                    }

                    continue;
                }

                written++;
            }

            return written;
        }

        private static bool TryWriteUnityBounds(
            string prefabPath,
            int componentIndex,
            Bounds sourceBounds,
            ScatterPolishConfigDTO config,
            NativeArray<CullingBoundsDTO> destination,
            int writeIndex)
        {
            if ((uint)writeIndex >= (uint)destination.Length)
                return false;

            Vector3 center = sourceBounds.center;
            Vector3 extents = sourceBounds.extents;
            if (!IsFinite(center) ||
                !IsFinite(extents) ||
                extents.x < 0.01f ||
                extents.y < 0.01f ||
                extents.z < 0.01f)
            {
                return false;
            }

            destination[writeIndex] = new CullingBoundsDTO
            {
                CenterAup = config.SectorOriginAup + new double3(center.x, center.y, center.z),
                Extents = new float3(extents.x, extents.y, extents.z),
                BoundsHash = HashCombine(HashPath(prefabPath), (uint)componentIndex),
                Flags = 1u,
                PaddingMeters = 0.35f
            };
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static void ValidateLayoutsOrThrow()
        {
            AssertSize<ScatterInstanceDTO>(AbyssalScatterPolisherConstants.ScatterInstanceStrideBytes, nameof(ScatterInstanceDTO));
            AssertSize<CullingBoundsDTO>(AbyssalScatterPolisherConstants.CullingBoundsStrideBytes, nameof(CullingBoundsDTO));
            AssertSize<ScatterPolishConfigDTO>(AbyssalScatterPolisherConstants.ConfigStrideBytes, nameof(ScatterPolishConfigDTO));
            AssertSize<BrgDataHeaderDTO>(AbyssalScatterPolisherConstants.HeaderSizeBytes, nameof(BrgDataHeaderDTO));
            AssertSize<BrgInstanceMetadataDTO>(AbyssalScatterPolisherConstants.MetadataStrideBytes, nameof(BrgInstanceMetadataDTO));
            AssertSize<GpuScatterFloraInstanceData>(AbyssalScatterPolisherConstants.MetadataStrideBytes, nameof(GpuScatterFloraInstanceData));
            AssertSize<ScatterPolisherTelemetryEntry>(64, nameof(ScatterPolisherTelemetryEntry));

            AssertOffset<ScatterInstanceDTO>(nameof(ScatterInstanceDTO.WorldPositionAup), 0);
            AssertOffset<ScatterInstanceDTO>(nameof(ScatterInstanceDTO.FallbackNormal), 24);
            AssertOffset<ScatterInstanceDTO>(nameof(ScatterInstanceDTO.SpeciesHash), 48);
            AssertOffset<CullingBoundsDTO>(nameof(CullingBoundsDTO.CenterAup), 0);
            AssertOffset<CullingBoundsDTO>(nameof(CullingBoundsDTO.Extents), 24);
            AssertOffset<BrgDataHeaderDTO>(nameof(BrgDataHeaderDTO.QualityIndexOffsetBytes), 48);
            AssertOffset<BrgInstanceMetadataDTO>(nameof(BrgInstanceMetadataDTO.BioluminescenceColor), 32);
            AssertOffset<BrgInstanceMetadataDTO>(nameof(BrgInstanceMetadataDTO.Reserved0), 60);
            AssertOffset<GpuScatterFloraInstanceData>(nameof(GpuScatterFloraInstanceData.Type), 0);
            AssertOffset<GpuScatterFloraInstanceData>(nameof(GpuScatterFloraInstanceData.HeightScale), 4);
            AssertOffset<GpuScatterFloraInstanceData>(nameof(GpuScatterFloraInstanceData.WidthScale), 8);
            AssertOffset<GpuScatterFloraInstanceData>(nameof(GpuScatterFloraInstanceData.Variation), 12);
            AssertOffset<GpuScatterFloraInstanceData>(nameof(GpuScatterFloraInstanceData.TemplateIndex), 16);
            AssertOffset<GpuScatterFloraInstanceData>(nameof(GpuScatterFloraInstanceData.RuntimeState), 20);
            AssertOffset<GpuScatterFloraInstanceData>(nameof(GpuScatterFloraInstanceData.RuntimeFlags), 24);
            AssertOffset<GpuScatterFloraInstanceData>(nameof(GpuScatterFloraInstanceData.PulseFrequency), 28);
            AssertOffset<GpuScatterFloraInstanceData>(nameof(GpuScatterFloraInstanceData.BioluminescenceColor), 32);
            AssertOffset<GpuScatterFloraInstanceData>(nameof(GpuScatterFloraInstanceData.SwaySpeed), 48);
            AssertOffset<GpuScatterFloraInstanceData>(nameof(GpuScatterFloraInstanceData.BendAmplitude), 52);
            AssertOffset<GpuScatterFloraInstanceData>(nameof(GpuScatterFloraInstanceData.HealthNormalized), 56);
            AssertOffset<GpuScatterFloraInstanceData>(nameof(GpuScatterFloraInstanceData.Reserved0), 60);
        }

        internal static bool ValidateMatrixDeterminant(float4x4 matrix, float expectedScale, float tolerance)
        {
            float determinant = math.determinant(matrix);
            float expected = expectedScale * expectedScale * expectedScale;
            return math.isfinite(determinant) && math.abs(determinant - expected) <= tolerance;
        }

        internal static bool RunFuzzerForKnownInsideBounds(out int culledCount)
        {
            bool ok = BakeMockScatterChunk(2048, 32, 1f, "scatter_1614_fuzzer.brgdata", out AbyssalScatterBakeResult result);
            culledCount = result.CulledCount;
            return ok && culledCount >= MockForcedInsideCount;
        }

        private static void RecordTelemetry(
            NativeArray<ScatterPolisherTelemetryEntry> telemetry,
            int index,
            uint stage,
            AbyssalScatterBakeResult result,
            ScatterPolishConfigDTO config)
        {
            if (!telemetry.IsCreated || (uint)index >= (uint)telemetry.Length)
                return;

            telemetry[index] = new ScatterPolisherTelemetryEntry
            {
                Stage = stage,
                StateHash = result.ContentHash,
                WarningFlags = result.NonFiniteCount > 0 ? 1u : 0u,
                InstanceCount = result.InstanceCount,
                CulledCount = result.CulledCount,
                NonFiniteCount = result.NonFiniteCount,
                JobMilliseconds = result.AlignmentMilliseconds,
                CullingMilliseconds = result.CullingMilliseconds,
                SectorOriginX = config.SectorOriginAup.x,
                SectorOriginY = config.SectorOriginAup.y,
                SectorOriginZ = config.SectorOriginAup.z,
                ContentHash = result.ContentHash
            };
        }

        private static float ResolveDrawFraction(float deviceWeight, float bakeWeight)
        {
            float x = math.saturate(deviceWeight) * math.saturate(bakeWeight);
            return math.saturate(0.08f + x * x * (3f - 2f * x) * 0.92f);
        }

        private static int ResolvePermutationStride(int instanceCount)
        {
            int stride = math.max(1, instanceCount / 97);
            if ((stride & 1) == 0)
                stride++;

            while (GreatestCommonDivisor(stride, instanceCount) != 1)
                stride += 2;

            return stride;
        }

        private static int GreatestCommonDivisor(int a, int b)
        {
            a = math.abs(a);
            b = math.abs(b);
            while (b != 0)
            {
                int t = a % b;
                a = b;
                b = t;
            }

            return math.max(1, a);
        }

        private static string SanitizeBrgAssetName(string assetName)
        {
            string value = string.IsNullOrEmpty(assetName) ? DefaultAssetName : assetName.Replace('\\', '/');
            string fileName = Path.GetFileName(value);
            string stem = ResolveAssetStem(fileName);
            return stem + ".brgdata";
        }

        private static string ResolveMetadataAssetName(string binaryAssetPath)
        {
            return "AbyssalScatterMetadata_" + ResolveAssetStem(binaryAssetPath) + ".asset";
        }

        private static string ResolvePrefabAssetName(string binaryAssetPath)
        {
            return "PFB_WorldScatterChunk_" + ResolveAssetStem(binaryAssetPath) + ".prefab";
        }

        private static string ResolveAssetStem(string assetPath)
        {
            string stem = Path.GetFileNameWithoutExtension(assetPath);
            if (string.IsNullOrEmpty(stem))
                stem = "scatter_1614_chunk";

            char[] chars = stem.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!IsAsciiAssetStemChar(chars[i]))
                    chars[i] = '_';
            }

            return new string(chars);
        }

        private static bool IsAsciiAssetStemChar(char c)
        {
            return (c >= 'a' && c <= 'z') ||
                   (c >= 'A' && c <= 'Z') ||
                   (c >= '0' && c <= '9') ||
                   c == '_' ||
                   c == '-';
        }

        private static string CombineAssetPath(string folder, string fileName)
        {
            return (folder.TrimEnd('/') + "/" + fileName).Replace("\\", "/");
        }

        private static string AssetPathToFileSystemPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        }

        private static void AssertSize<T>(int expectedBytes, string label) where T : struct
        {
            int actualBytes = Marshal.SizeOf<T>();
            if (actualBytes != expectedBytes)
                throw new InvalidOperationException(label + " stride mismatch. expected=" + expectedBytes + " actual=" + actualBytes);
        }

        private static void AssertOffset<T>(string fieldName, int expectedOffset) where T : struct
        {
            int actualOffset;
            try
            {
                actualOffset = Marshal.OffsetOf<T>(fieldName).ToInt32();
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(typeof(T).Name + "." + fieldName + " field missing for ABI validation.", exception);
            }

            if (actualOffset != expectedOffset)
                throw new InvalidOperationException(typeof(T).Name + "." + fieldName + " offset mismatch. expected=" + expectedOffset + " actual=" + actualOffset);
        }

        private static void DisposeIfCreated<T>(ref NativeArray<T> array) where T : struct
        {
            if (array.IsCreated)
                array.Dispose();
            array = default;
        }
    }
}
#endif
