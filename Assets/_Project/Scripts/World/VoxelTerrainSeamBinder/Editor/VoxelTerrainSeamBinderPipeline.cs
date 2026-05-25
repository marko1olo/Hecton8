#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Hecton8.World.VoxelTerrainSeamBinder;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World.VoxelTerrainSeamBinder.Editor
{
    public struct VoxelTerrainSeamBindRequest
    {
        public string AssetName;
        public Mesh TerrainLod0;
        public Mesh TerrainLod1;
        public Mesh TerrainLod2;
        public Mesh VoxelLod0;
        public Mesh VoxelLod1;
        public Mesh VoxelLod2;
        public double3 TerrainRootAup;
        public double3 VoxelRootAup;
        public bool PublishPreview;
    }

    public struct VoxelTerrainSeamBindResult
    {
        public int ProcessedLods;
        public int SnappedVertices;
        public int MissingLods;
        public float MaxDistanceErrorMeters;
        public double BurstMicroseconds;
        public uint WarningFlags;
        public string ReportPath;
        public string LastTerrainMeshPath;
        public string LastVoxelMeshPath;
    }

    public static class VoxelTerrainSeamBinderPipeline
    {
        private const string OutputFolder = "Assets/_Project/BakedGeometry/Stitched";
        private const string ReportPath = "Docs/Reports/SEAM_STITCH_REPORT.json";
        private const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_246.bin";
        private const string RollbackFenceSuffix = "_RollbackExcluded.bytes";
        private const int IndexCopyTileSize = 384;
        private static readonly Encoding TextEncoding = new UTF8Encoding(false);

        public static SeamBindingProfileDTO BuildDefaultProfile()
        {
            SeamBindingProfileDTO profile = default;
            profile.ProfileHash = VoxelTerrainSeamMath.HashAscii("dear_lie_default");
            profile.GlobalQualityWeight = 0.65f;
            profile.SnapRadiusMeters = 2.0f;
            profile.NormalBlendDistanceMeters = 3.5f;
            profile.TextureGradientFalloffMeters = 4.0f;
            profile.SpatialCellSizeMeters = 2.0f;
            profile.LodContinuityBias = 0.5f;
            profile.PreviewLineColor = new float3(1f, 0.08f, 0.04f);
            return profile;
        }

        public static VoxelTerrainSeamBindResult Stitch(in VoxelTerrainSeamBindRequest request, SeamBindingProfileDTO profile)
        {
            Directory.CreateDirectory(OutputFolder);
            Directory.CreateDirectory("Docs/Reports");
            Directory.CreateDirectory("Docs/AgentLogs");
            profile = SanitizeProfile(profile);
            string safeName = SanitizeFileName(string.IsNullOrEmpty(request.AssetName) ? "VoxelTerrainSeam" : request.AssetName);
            Stopwatch totalStopwatch = Stopwatch.StartNew();
            VoxelTerrainSeamBindResult result = new VoxelTerrainSeamBindResult
            {
                ReportPath = ReportPath,
                MaxDistanceErrorMeters = 0f
            };

            NativeArray<SeamBindTelemetryEntry> telemetry = default;
            StringBuilder lodRows = new StringBuilder(4096);
            int telemetryCursor = 0;
            try
            {
                telemetry = new NativeArray<SeamBindTelemetryEntry>(VoxelTerrainSeamConstants.TelemetryFrames, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                InitializeTelemetry(telemetry);
                RecordTelemetry(telemetry, ref telemetryCursor, request.TerrainRootAup, default, 1u, 0u);
                TryStitchLod(request.TerrainLod0, request.VoxelLod0, 0, safeName, in profile, request.TerrainRootAup, request.VoxelRootAup, request.PublishPreview, telemetry, ref telemetryCursor, ref result, lodRows);
                TryStitchLod(request.TerrainLod1, request.VoxelLod1, 1, safeName, in profile, request.TerrainRootAup, request.VoxelRootAup, false, telemetry, ref telemetryCursor, ref result, lodRows);
                TryStitchLod(request.TerrainLod2, request.VoxelLod2, 2, safeName, in profile, request.TerrainRootAup, request.VoxelRootAup, false, telemetry, ref telemetryCursor, ref result, lodRows);

                if (result.ProcessedLods <= 0)
                    result.WarningFlags |= VoxelTerrainSeamConstants.WarningLodMissing;
                if (result.SnappedVertices <= 0)
                    result.WarningFlags |= VoxelTerrainSeamConstants.WarningNoSnaps;

                WriteReport(in request, in profile, in result, lodRows, totalStopwatch.Elapsed.TotalMilliseconds);
                return result;
            }
            catch
            {
                if (telemetry.IsCreated)
                    DumpBlackBox(telemetry, 1u);
                throw;
            }
            finally
            {
                if (telemetry.IsCreated)
                    telemetry.Dispose();
            }
        }

        public static SeamBindCounters64 RunMockBenchmark(SeamBindingProfileDTO profile)
        {
            profile = SanitizeProfile(profile);
            int resolution = VoxelTerrainSeamConstants.MockResolution;
            int vertexCount = resolution * resolution;
            int quadCount = (resolution - 1) * (resolution - 1);
            int indexCount = quadCount * 6;
            NativeArray<SeamBindVertex32> terrainVertices = default;
            NativeArray<SeamBindVertex32> voxelVertices = default;
            NativeArray<int> terrainIndices = default;
            NativeArray<int> voxelIndices = default;
            try
            {
                terrainVertices = new NativeArray<SeamBindVertex32>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                voxelVertices = new NativeArray<SeamBindVertex32>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                terrainIndices = new NativeArray<int>(indexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                voxelIndices = new NativeArray<int>(indexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                JobHandle mockHandle = new GenerateMockSeamJob
                {
                    TerrainVertices = terrainVertices,
                    VoxelVertices = voxelVertices,
                    ResolutionX = resolution,
                    ResolutionZ = resolution,
                    CellSizeMeters = 0.35f,
                    NoiseAmplitudeMeters = 0.04f,
                    VoxelVerticalBiasMeters = 0.08f,
                    Seed = 0x246246u
                }.Schedule(vertexCount, 128);
                JobHandle terrainIndexHandle = new GenerateMockPlaneIndicesJob
                {
                    Output = terrainIndices,
                    ResolutionX = resolution,
                    ResolutionZ = resolution
                }.Schedule(quadCount, 128, mockHandle);
                JobHandle voxelIndexHandle = new GenerateMockPlaneIndicesJob
                {
                    Output = voxelIndices,
                    ResolutionX = resolution,
                    ResolutionZ = resolution
                }.Schedule(quadCount, 128, mockHandle);
                JobHandle.CombineDependencies(terrainIndexHandle, voxelIndexHandle).Complete();

                SeamBindingProfileDTO lodProfile = ResolveLodProfile(in profile, 0);
                int telemetryCursor = 0;
                StitchBuffers(
                    ref terrainVertices,
                    terrainIndices,
                    ref voxelVertices,
                    voxelIndices,
                    in lodProfile,
                    double3.zero,
                    new double3(0.042d, 0.0d, -0.028d),
                    false,
                    default,
                    ref telemetryCursor,
                    out SeamBindCounters64 counters);
                return counters;
            }
            finally
            {
                if (voxelIndices.IsCreated)
                    voxelIndices.Dispose();
                if (terrainIndices.IsCreated)
                    terrainIndices.Dispose();
                if (voxelVertices.IsCreated)
                    voxelVertices.Dispose();
                if (terrainVertices.IsCreated)
                    terrainVertices.Dispose();
            }
        }

        public static bool PreviewLod0(in VoxelTerrainSeamBindRequest request, SeamBindingProfileDTO profile, out SeamBindCounters64 counters)
        {
            counters = default;
            if (request.TerrainLod0 == null || request.VoxelLod0 == null)
                return false;

            profile = SanitizeProfile(profile);
            NativeArray<SeamBindVertex32> terrainVertices = default;
            NativeArray<SeamBindVertex32> voxelVertices = default;
            NativeArray<int> terrainIndices = default;
            NativeArray<int> voxelIndices = default;
            try
            {
                if (!TryBuildBaseBuffers(request.TerrainLod0, out terrainVertices, out terrainIndices) ||
                    !TryBuildBaseBuffers(request.VoxelLod0, out voxelVertices, out voxelIndices))
                    return false;

                int telemetryCursor = 0;
                SeamBindingProfileDTO lodProfile = ResolveLodProfile(in profile, 0);
                StitchBuffers(
                    ref terrainVertices,
                    terrainIndices,
                    ref voxelVertices,
                    voxelIndices,
                    in lodProfile,
                    request.TerrainRootAup,
                    request.VoxelRootAup,
                    true,
                    default,
                    ref telemetryCursor,
                    out counters);
                return true;
            }
            finally
            {
                if (voxelIndices.IsCreated)
                    voxelIndices.Dispose();
                if (terrainIndices.IsCreated)
                    terrainIndices.Dispose();
                if (voxelVertices.IsCreated)
                    voxelVertices.Dispose();
                if (terrainVertices.IsCreated)
                    terrainVertices.Dispose();
            }
        }

        private static void TryStitchLod(
            Mesh terrainMesh,
            Mesh voxelMesh,
            int lod,
            string safeName,
            in SeamBindingProfileDTO profile,
            double3 terrainRootAup,
            double3 voxelRootAup,
            bool publishPreview,
            NativeArray<SeamBindTelemetryEntry> telemetry,
            ref int telemetryCursor,
            ref VoxelTerrainSeamBindResult result,
            StringBuilder lodRows)
        {
            if (terrainMesh == null || voxelMesh == null)
            {
                result.MissingLods++;
                result.WarningFlags |= VoxelTerrainSeamConstants.WarningLodMissing;
                AppendLodRow(lodRows, lod, "MISSING", string.Empty, string.Empty, default);
                RecordTelemetry(telemetry, ref telemetryCursor, terrainRootAup, default, (uint)(10 + lod), VoxelTerrainSeamConstants.WarningLodMissing);
                return;
            }

            NativeArray<SeamBindVertex32> terrainVertices = default;
            NativeArray<SeamBindVertex32> voxelVertices = default;
            NativeArray<int> terrainIndices = default;
            NativeArray<int> voxelIndices = default;
            try
            {
                if (!TryBuildBaseBuffers(terrainMesh, out terrainVertices, out terrainIndices) ||
                    !TryBuildBaseBuffers(voxelMesh, out voxelVertices, out voxelIndices))
                {
                    result.WarningFlags |= VoxelTerrainSeamConstants.WarningMissingBoundary;
                    AppendLodRow(lodRows, lod, "INVALID_SOURCE", string.Empty, string.Empty, default);
                    RecordTelemetry(telemetry, ref telemetryCursor, terrainRootAup, default, (uint)(20 + lod), VoxelTerrainSeamConstants.WarningMissingBoundary);
                    return;
                }

                SeamBindingProfileDTO lodProfile = ResolveLodProfile(in profile, lod);
                StitchBuffers(
                    ref terrainVertices,
                    terrainIndices,
                    ref voxelVertices,
                    voxelIndices,
                    in lodProfile,
                    terrainRootAup,
                    voxelRootAup,
                    publishPreview,
                    telemetry,
                    ref telemetryCursor,
                    out SeamBindCounters64 counters);

                string terrainName = BuildGeneratedMeshName(safeName, "Terrain", lod);
                string voxelName = BuildGeneratedMeshName(safeName, "Voxel", lod);
                string terrainPath = SaveOrReplaceMesh(CreateMesh(terrainName, terrainVertices, terrainIndices), BuildGeneratedMeshPath(safeName, "Terrain", lod));
                string voxelPath = SaveOrReplaceMesh(CreateMesh(voxelName, voxelVertices, voxelIndices), BuildGeneratedMeshPath(safeName, "Voxel", lod));
                WriteRollbackFence(safeName, lod, terrainMesh, voxelMesh, terrainPath, voxelPath);
                result.ProcessedLods++;
                result.SnappedVertices += counters.SnappedVertexCount;
                result.MaxDistanceErrorMeters = math.max(result.MaxDistanceErrorMeters, counters.MaxDistanceErrorMeters);
                result.BurstMicroseconds += counters.BurstMicroseconds;
                result.WarningFlags |= counters.WarningFlags;
                result.LastTerrainMeshPath = terrainPath;
                result.LastVoxelMeshPath = voxelPath;
                AppendLodRow(lodRows, lod, "STITCHED", terrainPath, voxelPath, counters);
                RecordTelemetry(telemetry, ref telemetryCursor, terrainRootAup, counters, (uint)(30 + lod), counters.WarningFlags);
            }
            finally
            {
                if (voxelIndices.IsCreated)
                    voxelIndices.Dispose();
                if (terrainIndices.IsCreated)
                    terrainIndices.Dispose();
                if (voxelVertices.IsCreated)
                    voxelVertices.Dispose();
                if (terrainVertices.IsCreated)
                    terrainVertices.Dispose();
            }
        }

        private static void StitchBuffers(
            ref NativeArray<SeamBindVertex32> terrainVertices,
            NativeArray<int> terrainIndices,
            ref NativeArray<SeamBindVertex32> voxelVertices,
            NativeArray<int> voxelIndices,
            in SeamBindingProfileDTO profile,
            double3 terrainRootAup,
            double3 voxelRootAup,
            bool publishPreview,
            NativeArray<SeamBindTelemetryEntry> telemetry,
            ref int telemetryCursor,
            out SeamBindCounters64 counters)
        {
            counters = default;
            NativeArray<SeamEdgeDTO> voxelEdges = default;
            NativeArray<byte> boundaryMask = default;
            NativeParallelMultiHashMap<ulong, int> edgeMap = default;
            NativeParallelMultiHashMap<long, SeamBoundaryVertex64> boundaryHash = default;
            NativeArray<SeamSnapResult64> snapResults = default;
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                int voxelTriCount = voxelIndices.Length / 3;
                int edgeCount = voxelTriCount * 3;
                voxelEdges = new NativeArray<SeamEdgeDTO>(math.max(edgeCount, 1), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                boundaryMask = new NativeArray<byte>(math.max(voxelVertices.Length, 1), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                edgeMap = new NativeParallelMultiHashMap<ulong, int>(math.max(edgeCount, 1), Allocator.TempJob);
                boundaryHash = new NativeParallelMultiHashMap<long, SeamBoundaryVertex64>(math.max(voxelVertices.Length * 2, 1), Allocator.TempJob);
                snapResults = new NativeArray<SeamSnapResult64>(math.max(terrainVertices.Length, 1), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                JobHandle clearHandle = new ClearBoundaryMaskJob { BoundaryMask = boundaryMask }.Schedule(boundaryMask.Length, 128);
                JobHandle edgeHandle = new BuildTriangleEdgeMapJob
                {
                    Indices = voxelIndices,
                    Edges = voxelEdges,
                    EdgeMap = edgeMap.AsParallelWriter(),
                    VertexCount = voxelVertices.Length
                }.Schedule(voxelTriCount, 64, clearHandle);
                JobHandle markHandle = new MarkBoundaryVerticesJob
                {
                    Edges = voxelEdges,
                    EdgeMap = edgeMap,
                    BoundaryMask = boundaryMask
                }.Schedule(edgeHandle);
                JobHandle hashHandle = new ConstructBoundarySpatialHashJob
                {
                    VoxelVertices = voxelVertices,
                    BoundaryMask = boundaryMask,
                    BoundaryHash = boundaryHash.AsParallelWriter(),
                    VoxelRootAup = voxelRootAup,
                    CellSizeMeters = profile.SpatialCellSizeMeters
                }.Schedule(voxelVertices.Length, 64, markHandle);
                JobHandle snapHandle = new EvaluateSeamSnappingJob
                {
                    TerrainVertices = terrainVertices,
                    BoundaryHash = boundaryHash,
                    SnapResults = snapResults,
                    TerrainRootAup = terrainRootAup,
                    CellSizeMeters = profile.SpatialCellSizeMeters,
                    SnapRadiusMeters = profile.SnapRadiusMeters,
                    NormalBlendDistanceMeters = profile.NormalBlendDistanceMeters,
                    GlobalQualityWeight = profile.GlobalQualityWeight
                }.Schedule(terrainVertices.Length, 64, hashHandle);
                JobHandle normalHandle = new BlendSeamNormalsJob
                {
                    VoxelVertices = voxelVertices,
                    SnapResults = snapResults
                }.Schedule(snapHandle);
                JobHandle terrainColorHandle = new BakeSeamTransitionColorsJob
                {
                    Vertices = terrainVertices,
                    BoundaryHash = boundaryHash,
                    RootAup = terrainRootAup,
                    CellSizeMeters = profile.SpatialCellSizeMeters,
                    TextureGradientFalloffMeters = profile.TextureGradientFalloffMeters,
                    GlobalQualityWeight = profile.GlobalQualityWeight
                }.Schedule(terrainVertices.Length, 64, normalHandle);
                JobHandle voxelColorHandle = new BakeSeamTransitionColorsJob
                {
                    Vertices = voxelVertices,
                    BoundaryHash = boundaryHash,
                    RootAup = voxelRootAup,
                    CellSizeMeters = profile.SpatialCellSizeMeters,
                    TextureGradientFalloffMeters = profile.TextureGradientFalloffMeters,
                    GlobalQualityWeight = profile.GlobalQualityWeight
                }.Schedule(voxelVertices.Length, 64, normalHandle);
                JobHandle.CombineDependencies(terrainColorHandle, voxelColorHandle).Complete();
                stopwatch.Stop();

                int snapped = 0;
                float maxDistance = 0f;
                uint warningFlags = 0u;
                for (int i = 0; i < snapResults.Length && i < terrainVertices.Length; i++)
                {
                    SeamSnapResult64 snap = snapResults[i];
                    if (snap.VoxelVertexIndex >= 0)
                    {
                        snapped++;
                        if (math.isfinite(snap.DistanceMeters))
                            maxDistance = math.max(maxDistance, snap.DistanceMeters);
                    }
                }

                if (snapped <= 0)
                    warningFlags |= VoxelTerrainSeamConstants.WarningNoSnaps;
                if (HasNonFinite(terrainVertices) || HasNonFinite(voxelVertices))
                    warningFlags |= VoxelTerrainSeamConstants.WarningNonFiniteFallback;

                counters.TerrainVertexCount = terrainVertices.Length;
                counters.VoxelVertexCount = voxelVertices.Length;
                counters.TerrainIndexCount = terrainIndices.Length;
                counters.VoxelIndexCount = voxelIndices.Length;
                counters.SnappedVertexCount = snapped;
                counters.MissingBoundaryCount = snapped <= 0 ? terrainVertices.Length : 0;
                counters.MaxDistanceErrorMeters = maxDistance;
                counters.BurstMicroseconds = (float)(stopwatch.Elapsed.TotalMilliseconds * 1000.0d);
                counters.WarningFlags = warningFlags;
                counters.CriticalWarning = (warningFlags & VoxelTerrainSeamConstants.WarningNoSnaps) != 0u ? 1u : 0u;

                RecordTelemetry(telemetry, ref telemetryCursor, terrainRootAup, counters, 5u, 0u);

                if (publishPreview)
                    VoxelTerrainSeamPreviewStore.Set(snapResults);
            }
            catch
            {
                if (telemetry.IsCreated)
                    DumpBlackBox(telemetry, 2u);
                throw;
            }
            finally
            {
                if (snapResults.IsCreated)
                    snapResults.Dispose();
                if (boundaryHash.IsCreated)
                    boundaryHash.Dispose();
                if (edgeMap.IsCreated)
                    edgeMap.Dispose();
                if (boundaryMask.IsCreated)
                    boundaryMask.Dispose();
                if (voxelEdges.IsCreated)
                    voxelEdges.Dispose();
            }
        }

        private static bool TryBuildBaseBuffers(Mesh source, out NativeArray<SeamBindVertex32> vertices, out NativeArray<int> indices)
        {
            vertices = default;
            indices = default;
            if (source == null || source.vertexCount <= 0 || source.subMeshCount <= 0)
                return false;

            NativeArray<SeamSubMeshIndexRangeDTO> ranges = default;
            try
            {
                using Mesh.MeshDataArray readOnly = Mesh.AcquireReadOnlyMeshData(source);
                Mesh.MeshData sourceData = readOnly[0];
                int vertexCount = sourceData.vertexCount;
                int indexCount = BuildTriangleSubMeshRanges(sourceData, out ranges, out int rangeCount);
                if (vertexCount <= 0 || indexCount <= 0 || rangeCount <= 0 || !HasFloatAttribute(sourceData, VertexAttribute.Position, 3))
                    return false;

                vertices = new NativeArray<SeamBindVertex32>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                indices = new NativeArray<int>(indexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                JobHandle handle = ScheduleExtractBaseVertices(sourceData, vertices);
                handle = ScheduleCopyIndices(sourceData, indices, ranges, rangeCount, handle);
                handle.Complete();
                return true;
            }
            catch
            {
                if (indices.IsCreated)
                    indices.Dispose();
                if (vertices.IsCreated)
                    vertices.Dispose();
                indices = default;
                vertices = default;
                return false;
            }
            finally
            {
                if (ranges.IsCreated)
                    ranges.Dispose();
            }
        }

        private static JobHandle ScheduleExtractBaseVertices(Mesh.MeshData sourceData, NativeArray<SeamBindVertex32> vertices)
        {
            int positionStream = sourceData.GetVertexAttributeStream(VertexAttribute.Position);
            int normalStream = sourceData.HasVertexAttribute(VertexAttribute.Normal) ? sourceData.GetVertexAttributeStream(VertexAttribute.Normal) : -1;
            int uvStream = sourceData.HasVertexAttribute(VertexAttribute.TexCoord0) ? sourceData.GetVertexAttributeStream(VertexAttribute.TexCoord0) : -1;
            int colorStream = sourceData.HasVertexAttribute(VertexAttribute.Color) ? sourceData.GetVertexAttributeStream(VertexAttribute.Color) : -1;
            bool hasNormal = normalStream >= 0 && HasFloatAttribute(sourceData, VertexAttribute.Normal, 3);
            bool hasUv = uvStream >= 0 && HasFloatAttribute(sourceData, VertexAttribute.TexCoord0, 2);
            bool hasColor = colorStream >= 0 &&
                            sourceData.GetVertexAttributeFormat(VertexAttribute.Color) == VertexAttributeFormat.UNorm8 &&
                            sourceData.GetVertexAttributeDimension(VertexAttribute.Color) >= 4;

            ExtractSeamVerticesJob job = new ExtractSeamVerticesJob
            {
                PositionBytes = sourceData.GetVertexData<byte>(positionStream),
                NormalBytes = hasNormal ? sourceData.GetVertexData<byte>(normalStream) : default,
                UvBytes = hasUv ? sourceData.GetVertexData<byte>(uvStream) : default,
                ColorBytes = hasColor ? sourceData.GetVertexData<byte>(colorStream) : default,
                Output = vertices,
                PositionOffset = sourceData.GetVertexAttributeOffset(VertexAttribute.Position),
                PositionStride = sourceData.GetVertexBufferStride(positionStream),
                NormalOffset = hasNormal ? sourceData.GetVertexAttributeOffset(VertexAttribute.Normal) : 0,
                NormalStride = hasNormal ? sourceData.GetVertexBufferStride(normalStream) : 0,
                UvOffset = hasUv ? sourceData.GetVertexAttributeOffset(VertexAttribute.TexCoord0) : 0,
                UvStride = hasUv ? sourceData.GetVertexBufferStride(uvStream) : 0,
                ColorOffset = hasColor ? sourceData.GetVertexAttributeOffset(VertexAttribute.Color) : 0,
                ColorStride = hasColor ? sourceData.GetVertexBufferStride(colorStream) : 0,
                HasNormal = hasNormal ? 1 : 0,
                HasUv = hasUv ? 1 : 0,
                HasColor = hasColor ? 1 : 0
            };
            return job.Schedule(vertices.Length, 64);
        }

        private static JobHandle ScheduleCopyIndices(
            Mesh.MeshData sourceData,
            NativeArray<int> indices,
            NativeArray<SeamSubMeshIndexRangeDTO> ranges,
            int rangeCount,
            JobHandle dependency)
        {
            if (sourceData.indexFormat == IndexFormat.UInt16)
            {
                return new CopySeamIndex16RangesJob
                {
                    Source = sourceData.GetIndexData<ushort>(),
                    Ranges = ranges,
                    Output = indices
                }.Schedule(rangeCount, 1, dependency);
            }

            return new CopySeamIndex32RangesJob
            {
                Source = sourceData.GetIndexData<uint>(),
                Ranges = ranges,
                Output = indices
            }.Schedule(rangeCount, 1, dependency);
        }

        private static int BuildTriangleSubMeshRanges(
            Mesh.MeshData sourceData,
            out NativeArray<SeamSubMeshIndexRangeDTO> ranges,
            out int rangeCount)
        {
            ranges = default;
            rangeCount = 0;
            int subMeshCount = sourceData.subMeshCount;
            if (subMeshCount <= 0)
                return 0;

            int sourceIndexCapacity = sourceData.indexFormat == IndexFormat.UInt16
                ? sourceData.GetIndexData<ushort>().Length
                : sourceData.GetIndexData<uint>().Length;
            if (sourceIndexCapacity <= 0)
                return 0;

            int totalIndexCount = 0;
            int tileCapacity = 0;
            for (int i = 0; i < subMeshCount; i++)
            {
                SubMeshDescriptor descriptor = sourceData.GetSubMesh(i);
                if (descriptor.topology != MeshTopology.Triangles)
                    continue;

                int sourceStart = math.clamp(descriptor.indexStart, 0, sourceIndexCapacity);
                int available = math.max(sourceIndexCapacity - sourceStart, 0);
                int indexCount = math.min(descriptor.indexCount, available);
                indexCount -= indexCount % 3;
                if (indexCount <= 0)
                    continue;

                totalIndexCount += indexCount;
                tileCapacity += (indexCount + IndexCopyTileSize - 1) / IndexCopyTileSize;
            }

            if (tileCapacity <= 0 || totalIndexCount <= 0)
                return 0;

            ranges = new NativeArray<SeamSubMeshIndexRangeDTO>(tileCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            int destinationStart = 0;
            for (int i = 0; i < subMeshCount; i++)
            {
                SubMeshDescriptor descriptor = sourceData.GetSubMesh(i);
                if (descriptor.topology != MeshTopology.Triangles)
                    continue;

                int sourceStart = math.clamp(descriptor.indexStart, 0, sourceIndexCapacity);
                int available = math.max(sourceIndexCapacity - sourceStart, 0);
                int remaining = math.min(descriptor.indexCount, available);
                remaining -= remaining % 3;
                if (remaining <= 0)
                    continue;

                while (remaining > 0)
                {
                    int chunk = math.min(remaining, IndexCopyTileSize);
                    ranges[rangeCount] = new SeamSubMeshIndexRangeDTO
                    {
                        SourceIndexStart = sourceStart,
                        IndexCount = chunk,
                        DestinationIndexStart = destinationStart,
                        BaseVertex = descriptor.baseVertex
                    };
                    sourceStart += chunk;
                    destinationStart += chunk;
                    remaining -= chunk;
                    rangeCount++;
                }
            }

            return destinationStart;
        }

        private static bool HasFloatAttribute(Mesh.MeshData sourceData, VertexAttribute attribute, int minDimension)
        {
            return sourceData.HasVertexAttribute(attribute) &&
                   sourceData.GetVertexAttributeFormat(attribute) == VertexAttributeFormat.Float32 &&
                   sourceData.GetVertexAttributeDimension(attribute) >= minDimension;
        }

        private static Mesh CreateMesh(string name, NativeArray<SeamBindVertex32> vertices, NativeArray<int> indices)
        {
            Mesh mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            const MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers;
            Bounds bounds = ComputeBounds(vertices);
            mesh.SetVertexBufferParams(
                vertices.Length,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 0),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.UNorm16, 2, 0));
            mesh.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
            mesh.SetVertexBufferData(vertices, 0, 0, vertices.Length, 0, flags);
            mesh.SetIndexBufferData(indices, 0, 0, indices.Length, flags);
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length, MeshTopology.Triangles)
            {
                bounds = bounds,
                vertexCount = vertices.Length
            }, flags);
            mesh.bounds = bounds;
            mesh.UploadMeshData(false);
            return mesh;
        }

        private static Bounds ComputeBounds(NativeArray<SeamBindVertex32> vertices)
        {
            if (!vertices.IsCreated || vertices.Length <= 0)
                return new Bounds(Vector3.zero, Vector3.one);

            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);
            for (int i = 0; i < vertices.Length; i++)
            {
                float3 p = vertices[i].Position;
                if (!math.all(math.isfinite(p)))
                    continue;

                min = math.min(min, p);
                max = math.max(max, p);
            }

            if (!math.all(math.isfinite(min)) || !math.all(math.isfinite(max)) || math.any(max < min))
                return new Bounds(Vector3.zero, Vector3.one);

            float3 center = (min + max) * 0.5f;
            float3 size = math.max(max - min, new float3(0.01f));
            return new Bounds(new Vector3(center.x, center.y, center.z), new Vector3(size.x, size.y, size.z));
        }

        private static string SaveOrReplaceMesh(Mesh mesh, string assetPath)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, assetPath);
                return assetPath;
            }

            EditorUtility.CopySerialized(mesh, existing);
            existing.name = mesh.name;
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            return assetPath;
        }

        private static bool HasNonFinite(NativeArray<SeamBindVertex32> vertices)
        {
            for (int i = 0; i < vertices.Length; i++)
            {
                SeamBindVertex32 vertex = vertices[i];
                if (!math.all(math.isfinite(vertex.Position)) || !math.all(math.isfinite(vertex.Normal)))
                    return true;
            }

            return false;
        }

        private static SeamBindingProfileDTO SanitizeProfile(SeamBindingProfileDTO profile)
        {
            if (profile.ProfileHash == 0u)
                profile.ProfileHash = VoxelTerrainSeamMath.HashAscii("dear_lie_default");
            profile.GlobalQualityWeight = math.saturate(math.isfinite(profile.GlobalQualityWeight) ? profile.GlobalQualityWeight : 0.5f);
            profile.SnapRadiusMeters = math.clamp(math.isfinite(profile.SnapRadiusMeters) ? profile.SnapRadiusMeters : 2f, 0.02f, 20f);
            profile.NormalBlendDistanceMeters = math.clamp(math.isfinite(profile.NormalBlendDistanceMeters) ? profile.NormalBlendDistanceMeters : 3.5f, 0.02f, 40f);
            profile.TextureGradientFalloffMeters = math.clamp(math.isfinite(profile.TextureGradientFalloffMeters) ? profile.TextureGradientFalloffMeters : 4f, 0.02f, 64f);
            profile.SpatialCellSizeMeters = math.clamp(math.isfinite(profile.SpatialCellSizeMeters) ? profile.SpatialCellSizeMeters : profile.SnapRadiusMeters, 0.02f, 64f);
            profile.LodContinuityBias = math.saturate(math.isfinite(profile.LodContinuityBias) ? profile.LodContinuityBias : profile.GlobalQualityWeight);
            if (!math.all(math.isfinite(profile.PreviewLineColor)))
                profile.PreviewLineColor = new float3(1f, 0.08f, 0.04f);
            return profile;
        }

        private static SeamBindingProfileDTO ResolveLodProfile(in SeamBindingProfileDTO profile, int lod)
        {
            SeamBindingProfileDTO resolved = profile;
            float quality = math.saturate(math.isfinite(profile.GlobalQualityWeight) ? profile.GlobalQualityWeight : 0.5f);
            float lod01 = math.saturate(lod * 0.5f);
            float continuity = math.saturate(math.isfinite(profile.LodContinuityBias) ? profile.LodContinuityBias : quality);
            float expansion = 1f + (lod01 * math.lerp(0.45f, 0.12f, quality) * math.lerp(1f, 0.45f, continuity));
            resolved.SnapRadiusMeters = math.clamp(profile.SnapRadiusMeters * expansion, 0.02f, 64f);
            resolved.NormalBlendDistanceMeters = math.clamp(profile.NormalBlendDistanceMeters * expansion, 0.02f, 96f);
            resolved.TextureGradientFalloffMeters = math.clamp(profile.TextureGradientFalloffMeters * expansion, 0.02f, 128f);
            resolved.SpatialCellSizeMeters = math.clamp(math.max(profile.SpatialCellSizeMeters, resolved.SnapRadiusMeters * math.lerp(0.75f, 0.5f, quality)), 0.02f, 128f);
            return resolved;
        }

        private static void InitializeTelemetry(NativeArray<SeamBindTelemetryEntry> telemetry)
        {
            SeamBindTelemetryEntry empty = default;
            for (int i = 0; i < telemetry.Length; i++)
                telemetry[i] = empty;
        }

        private static void RecordTelemetry(
            NativeArray<SeamBindTelemetryEntry> telemetry,
            ref int cursor,
            double3 terrainRootAup,
            SeamBindCounters64 counters,
            uint stage,
            uint dumpReason)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            int index = cursor % telemetry.Length;
            SeamBindTelemetryEntry entry = BuildTelemetry(terrainRootAup, counters, (uint)cursor, stage, dumpReason);
            telemetry[index] = entry;
            cursor++;
        }

        private static SeamBindTelemetryEntry BuildTelemetry(double3 terrainRootAup, SeamBindCounters64 counters, uint frame, uint stage, uint dumpReason)
        {
            SeamBindTelemetryEntry entry = default;
            entry.TerrainRootAup = terrainRootAup;
            entry.Frame = frame;
            entry.TerrainVertexCount = counters.TerrainVertexCount;
            entry.VoxelVertexCount = counters.VoxelVertexCount;
            entry.SnappedVertexCount = counters.SnappedVertexCount;
            entry.MaxDistanceErrorMeters = counters.MaxDistanceErrorMeters;
            entry.BurstMicroseconds = counters.BurstMicroseconds;
            entry.WarningFlags = counters.WarningFlags;
            entry.StateHash = VoxelTerrainSeamMath.Hash((uint)counters.SnappedVertexCount ^ ((uint)counters.TerrainVertexCount << 1) ^ ((uint)counters.VoxelVertexCount << 2));
            entry.Stage = stage;
            entry.DumpReason = dumpReason;
            return entry;
        }

        private static void WriteReport(
            in VoxelTerrainSeamBindRequest request,
            in SeamBindingProfileDTO profile,
            in VoxelTerrainSeamBindResult result,
            StringBuilder lodRows,
            double totalMs)
        {
            StringBuilder json = new StringBuilder(8192);
            json.Append("{\n");
            json.Append("  \"agent\": \"SHINOBU_246\",\n");
            json.Append("  \"status\": \"PENDING_VERIFICATION\",\n");
            json.Append("  \"version\": ").Append(VoxelTerrainSeamConstants.ReportVersion).Append(",\n");
            json.Append("  \"assetName\": \"");
            AppendEscaped(json, request.AssetName ?? string.Empty);
            json.Append("\",\n");
            json.Append("  \"profileHash\": ").Append(profile.ProfileHash).Append(",\n");
            json.Append("  \"globalQualityWeight\": ").Append(profile.GlobalQualityWeight.ToString("0.###", CultureInfo.InvariantCulture)).Append(",\n");
            json.Append("  \"processedLods\": ").Append(result.ProcessedLods).Append(",\n");
            json.Append("  \"snappedVertices\": ").Append(result.SnappedVertices).Append(",\n");
            json.Append("  \"maxDistanceErrorMeters\": ").Append(result.MaxDistanceErrorMeters.ToString("0.####", CultureInfo.InvariantCulture)).Append(",\n");
            json.Append("  \"burstMicroseconds\": ").Append(result.BurstMicroseconds.ToString("0.###", CultureInfo.InvariantCulture)).Append(",\n");
            json.Append("  \"totalMilliseconds\": ").Append(totalMs.ToString("0.###", CultureInfo.InvariantCulture)).Append(",\n");
            json.Append("  \"warningFlags\": ").Append(result.WarningFlags).Append(",\n");
            json.Append("  \"criticalWarning\": \"").Append((result.WarningFlags & VoxelTerrainSeamConstants.WarningNoSnaps) != 0u ? "CRITICAL_WARNING" : "NONE").Append("\",\n");
            json.Append("  \"rollbackNetcodeExcluded\": true,\n");
            json.Append("  \"lods\": [\n");
            json.Append(lodRows);
            json.Append("\n  ]\n");
            json.Append("}\n");
            WriteTextAtomic(ReportPath, json.ToString());
        }

        private static void AppendLodRow(StringBuilder builder, int lod, string status, string terrainPath, string voxelPath, SeamBindCounters64 counters)
        {
            if (builder.Length > 0)
                builder.Append(",\n");

            builder.Append("    { \"lod\": ").Append(lod);
            builder.Append(", \"status\": \"").Append(status).Append("\"");
            builder.Append(", \"terrainMesh\": \"");
            AppendEscaped(builder, terrainPath ?? string.Empty);
            builder.Append("\", \"voxelMesh\": \"");
            AppendEscaped(builder, voxelPath ?? string.Empty);
            builder.Append("\", \"snapped\": ").Append(counters.SnappedVertexCount);
            builder.Append(", \"maxError\": ").Append(counters.MaxDistanceErrorMeters.ToString("0.####", CultureInfo.InvariantCulture));
            builder.Append(", \"burstMicroseconds\": ").Append(counters.BurstMicroseconds.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(", \"warnings\": ").Append(counters.WarningFlags);
            builder.Append(" }");
        }

        private static void WriteRollbackFence(string safeName, int lod, Mesh terrainMesh, Mesh voxelMesh, string terrainPath, string voxelPath)
        {
            SeamMeshRollbackFenceDTO fence = default;
            fence.TerrainMeshHash = VoxelTerrainSeamMath.HashAscii(terrainMesh != null ? terrainMesh.name : terrainPath);
            fence.VoxelMeshHash = VoxelTerrainSeamMath.HashAscii(voxelMesh != null ? voxelMesh.name : voxelPath);
            fence.StitchedMeshHash = VoxelTerrainSeamMath.Hash(fence.TerrainMeshHash ^ (fence.VoxelMeshHash * 16777619u));
            fence.RollbackExcluded = VoxelTerrainSeamConstants.RollbackExcludedTrue;
            fence.Magic = VoxelTerrainSeamConstants.RollbackFenceMagic;
            fence.Version = VoxelTerrainSeamConstants.RollbackFenceVersion;
            fence.EndianMarker = VoxelTerrainSeamConstants.LittleEndianMarker;
            fence.Reserved = 0u;
            Span<byte> bytes = stackalloc byte[32];
            WriteUInt32(bytes, 0, fence.TerrainMeshHash);
            WriteUInt32(bytes, 4, fence.VoxelMeshHash);
            WriteUInt32(bytes, 8, fence.StitchedMeshHash);
            WriteUInt32(bytes, 12, fence.RollbackExcluded);
            WriteUInt32(bytes, 16, fence.Magic);
            WriteUInt32(bytes, 20, fence.Version);
            WriteUInt32(bytes, 24, fence.EndianMarker);
            WriteUInt32(bytes, 28, fence.Reserved);
            string path = BuildRollbackFencePath(safeName, lod);
            WriteBytesAtomic(path, bytes);
            AssetDatabase.ImportAsset(path);
        }

        private static string BuildGeneratedMeshName(string safeName, string meshKind, int lod)
        {
            StringBuilder builder = new StringBuilder(64 + safeName.Length + meshKind.Length);
            builder.Append("GEN_");
            builder.Append(safeName);
            builder.Append('_');
            builder.Append(meshKind);
            builder.Append("_STITCHED_LOD");
            builder.Append(lod);
            return builder.ToString();
        }

        private static string BuildGeneratedMeshPath(string safeName, string meshKind, int lod)
        {
            StringBuilder builder = new StringBuilder(96 + safeName.Length + meshKind.Length);
            builder.Append(OutputFolder);
            builder.Append("/GEN_");
            builder.Append(safeName);
            builder.Append('_');
            builder.Append(meshKind);
            builder.Append("_STITCHED_LOD");
            builder.Append(lod);
            builder.Append(".asset");
            return builder.ToString();
        }

        private static string BuildRollbackFencePath(string safeName, int lod)
        {
            StringBuilder builder = new StringBuilder(96 + safeName.Length);
            builder.Append(OutputFolder);
            builder.Append("/GEN_");
            builder.Append(safeName);
            builder.Append("_LOD");
            builder.Append(lod);
            builder.Append(RollbackFenceSuffix);
            return builder.ToString();
        }

        private static void DumpBlackBox(NativeArray<SeamBindTelemetryEntry> telemetry, uint reason)
        {
            using (FileStream stream = new FileStream(DumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(0x53454D32u);
                writer.Write(telemetry.Length);
                for (int i = 0; i < telemetry.Length; i++)
                {
                    SeamBindTelemetryEntry entry = telemetry[i];
                    writer.Write(entry.TerrainRootAup.x);
                    writer.Write(entry.TerrainRootAup.y);
                    writer.Write(entry.TerrainRootAup.z);
                    writer.Write(entry.Frame);
                    writer.Write(entry.TerrainVertexCount);
                    writer.Write(entry.VoxelVertexCount);
                    writer.Write(entry.SnappedVertexCount);
                    writer.Write(entry.MaxDistanceErrorMeters);
                    writer.Write(entry.BurstMicroseconds);
                    writer.Write(entry.WarningFlags);
                    writer.Write(entry.StateHash);
                    writer.Write(entry.Stage);
                    writer.Write(i == 0 && entry.DumpReason == 0u ? reason : entry.DumpReason);
                }
            }
        }

        private static string SanitizeFileName(string value)
        {
            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                builder.Append(IsInvalidFileNameChar(c) ? '_' : c);
            }

            return builder.Length == 0 ? "VoxelTerrainSeam" : builder.ToString();
        }

        private static bool IsInvalidFileNameChar(char value)
        {
            return value < 32 ||
                   value == '<' ||
                   value == '>' ||
                   value == ':' ||
                   value == '"' ||
                   value == '/' ||
                   value == '\\' ||
                   value == '|' ||
                   value == '?' ||
                   value == '*';
        }

        private static void AppendEscaped(StringBuilder builder, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' || c == '"')
                    builder.Append('\\');
                builder.Append(c);
            }
        }

        private static void WriteTextAtomic(string path, string text)
        {
            string fullPath = FullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temp = BuildTempPath(fullPath);
            File.WriteAllText(temp, text, TextEncoding);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
            File.Move(temp, fullPath);
        }

        private static void WriteBytesAtomic(string path, ReadOnlySpan<byte> bytes)
        {
            string fullPath = FullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temp = BuildTempPath(fullPath);
            using (FileStream stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                stream.Write(bytes);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
            File.Move(temp, fullPath);
        }

        private static string BuildTempPath(string fullPath)
        {
            StringBuilder builder = new StringBuilder(fullPath.Length + 4);
            builder.Append(fullPath);
            builder.Append(".tmp");
            return builder.ToString();
        }

        private static string FullPath(string projectRelativePath)
        {
            return Path.Combine(ProjectRoot(), projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ProjectRoot()
        {
            return Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length);
        }

        private static void WriteUInt32(Span<byte> bytes, int offset, uint value)
        {
            bytes[offset] = (byte)(value & 0xFFu);
            bytes[offset + 1] = (byte)((value >> 8) & 0xFFu);
            bytes[offset + 2] = (byte)((value >> 16) & 0xFFu);
            bytes[offset + 3] = (byte)((value >> 24) & 0xFFu);
        }
    }
}
#endif
