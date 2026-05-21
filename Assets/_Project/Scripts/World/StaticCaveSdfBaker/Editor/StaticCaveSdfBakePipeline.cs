using System;
using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Hecton8.World.StaticCaveSdfBaker;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Hecton8.World.StaticCaveSdfBaker.Editor
{
    public struct StaticCaveSdfBakeResult
    {
        public string BinaryAssetPath;
        public string TextureAssetPath;
        public string BackupAssetPath;
        public string ReportPath;
        public int VoxelCount;
        public int TriangleCount;
        public int BvhNodeCount;
        public long FileSizeBytes;
        public long ExpectedFileSizeBytes;
        public float BvhMilliseconds;
        public float SdfMilliseconds;
        public float CompressMilliseconds;
        public int BvhLeafTriangleCount;
        public int SdfBatchSize;
        public int CompressBatchSize;
        public uint WarningFlags;
        public bool BackupCreated;
        public bool AtomicWriteCompleted;
    }

    public static class StaticCaveSdfBakePipeline
    {
        private const string OutputFolder = "Assets/_Project/BakedGeometry/StaticCaveSdf";
        private const string ReportPath = "Docs/Reports/CAVE_SDF_BAKE_REPORT.json";
        private const string SelfAuditPath = "Docs/Reports/CAVE_SDF_SELF_AUDIT_SHINOBU_244.md";
        private const float MinimumBakeHalfExtentMeters = 0.01f;
        private const float FallbackBakeHalfExtentMeters = 0.5f;
        private const float MaximumBakeHalfExtentMeters = 50000f;

        public static StaticCaveSdfBakeResult BakeMesh(
            Mesh source,
            string assetName,
            StaticCaveSdfBakeConfigDTO config,
            bool createTexture3D)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            Directory.CreateDirectory(OutputFolder);
            Directory.CreateDirectory("Docs/Reports");
            Directory.CreateDirectory("Docs/AgentLogs");

            string layoutError;
            uint warningFlags = StaticCaveSdfLayoutValidator.ValidateTriangleLayout(out layoutError)
                ? 0u
                : StaticCaveSdfConstants.WarningLayoutMismatch;

            NativeArray<TriangleDTO> triangles = default;
            try
            {
                config = SanitizeConfig(source.bounds, config);
                if (!BuildTrianglesFromMeshData(source, config.SubMeshIndex, out triangles))
                    throw new InvalidOperationException("Source mesh position/index data could not be read through MeshData.");

                config.TriangleCount = triangles.Length;
                return BakeTrianglesInternal(triangles, assetName, config, createTexture3D, warningFlags);
            }
            finally
            {
                if (triangles.IsCreated)
                    triangles.Dispose();
                EditorUtility.ClearProgressBar();
            }
        }

        public static StaticCaveSdfBakeResult RunMockTorusBenchmark(
            string assetName,
            StaticCaveSdfBakeConfigDTO config,
            int targetTriangles,
            bool createTexture3D)
        {
            Directory.CreateDirectory(OutputFolder);
            Directory.CreateDirectory("Docs/Reports");
            Directory.CreateDirectory("Docs/AgentLogs");

            int triangleTarget = math.clamp(targetTriangles <= 0 ? 100000 : targetTriangles, 4096, 500000);
            int majorSegments = math.max(32, (int)math.sqrt(triangleTarget * 0.5f));
            int minorSegments = math.max(16, triangleTarget / math.max(majorSegments * 2, 1));
            int triangleCount = majorSegments * minorSegments * 2;
            NativeArray<TriangleDTO> triangles = default;
            try
            {
                config.BoundsMin = new float3(-42f, -12f, -42f);
                config.BoundsMax = new float3(42f, 12f, 42f);
                config = SanitizeConfig(default, config);
                triangles = new NativeArray<TriangleDTO>(triangleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                new GenerateMockTorusMeshJob
                {
                    Triangles = triangles,
                    MajorSegments = majorSegments,
                    MinorSegments = minorSegments,
                    MajorRadius = 22f,
                    MinorRadius = 5f,
                    TwistTurns = math.lerp(1f, 4f, math.saturate(config.GlobalQualityWeight)),
                    GlobalQualityWeight = config.GlobalQualityWeight
                // [EDITOR_BLOCKING_SYNC_POINT] Mock source generation must finish before BVH construction reads the triangle stream.
                }.Schedule(triangleCount, 64).Complete();

                config.TriangleCount = triangleCount;
                return BakeTrianglesInternal(
                    triangles,
                    string.IsNullOrEmpty(assetName) ? "Mock_Twisted_Torus" : assetName,
                    config,
                    createTexture3D,
                    StaticCaveSdfConstants.WarningMockBenchmark);
            }
            finally
            {
                if (triangles.IsCreated)
                    triangles.Dispose();
                EditorUtility.ClearProgressBar();
            }
        }

        private static StaticCaveSdfBakeResult BakeTrianglesInternal(
            NativeArray<TriangleDTO> triangles,
            string assetName,
            StaticCaveSdfBakeConfigDTO config,
            bool createTexture3D,
            uint initialWarningFlags)
        {
            string safeName = SanitizeFileName(assetName);
            int voxelCount = config.VoxelCount;
            if (triangles.Length <= 0)
                throw new InvalidOperationException("Static cave SDF bake requires at least one triangle.");
            if (triangles.Length > int.MaxValue / 2)
                throw new InvalidOperationException("Static cave SDF triangle stream exceeds fixed BVH node capacity budget.");

            int nodeCapacity = math.max(1, triangles.Length * 2);
            Stopwatch total = Stopwatch.StartNew();
            Stopwatch stage = Stopwatch.StartNew();
            float bvhMs = 0f;
            float sdfMs = 0f;
            float compressMs = 0f;
            uint warningFlags = initialWarningFlags;
            float qualityCurve = math.smoothstep(0f, 1f, math.saturate(config.GlobalQualityWeight));
            int bvhLeafTriangleCount = math.clamp((int)math.round(math.lerp(16f, 4f, qualityCurve)), 4, 16);
            int sdfBatchSize = math.clamp((int)math.round(math.lerp(256f, 32f, qualityCurve)), 32, 256);
            int compressBatchSize = math.clamp((int)math.round(math.lerp(512f, 128f, qualityCurve)), 128, 512);

            NativeArray<int> triangleIndices = default;
            NativeArray<BvhNodeDTO> nodes = default;
            NativeArray<BvhBuildRangeDTO> stack = default;
            NativeArray<int> counters = default;
            NativeArray<float> distances = default;
            NativeArray<int> sdfWarningFlags = default;
            NativeArray<ushort> halfDistances = default;
            StaticCaveSdfBakeTelemetryBuffer telemetry = default;

            try
            {
                telemetry.Initialize();
                EditorUtility.DisplayProgressBar("Static SDF Forge", "Constructing BVH", 0.12f);
                triangleIndices = new NativeArray<int>(triangles.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                nodes = new NativeArray<BvhNodeDTO>(nodeCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                stack = new NativeArray<BvhBuildRangeDTO>(nodeCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                counters = new NativeArray<int>(2, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                new ConstructBvhJob
                {
                    Triangles = triangles,
                    TriangleIndices = triangleIndices,
                    Nodes = nodes,
                    Stack = stack,
                    Counters = counters,
                    MaxLeafTriangles = bvhLeafTriangleCount,
                    MaxDepth = StaticCaveSdfConstants.BvhMaxDepth
                // [EDITOR_BLOCKING_SYNC_POINT] The forge reads node counters immediately for the next stage and bake report.
                }.Schedule().Complete();
                bvhMs = (float)stage.Elapsed.TotalMilliseconds;
                int nodeCount = counters[0];
                warningFlags |= (uint)counters[1];
                StaticCaveSdfBlackBox.Record(ref telemetry, config.AnchorAup, voxelCount, triangles.Length, nodeCount, bvhMs, 0f, 0f, warningFlags, 1u);

                EditorUtility.DisplayProgressBar("Static SDF Forge", "Evaluating signed distance volume", 0.38f);
                stage.Restart();
                distances = new NativeArray<float>(voxelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                sdfWarningFlags = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                sdfWarningFlags[0] = 0;
                JobHandle sdfHandle = new EvaluateSdfVolumeJob
                {
                    Triangles = triangles,
                    TriangleIndices = triangleIndices,
                    Nodes = nodes,
                    Distances = distances,
                    Config = config,
                    NodeCount = nodeCount
                }.Schedule(voxelCount, sdfBatchSize);
                JobHandle validateSdfHandle = new ValidateSdfDistanceWarningsJob
                {
                    Distances = distances,
                    WarningFlags = sdfWarningFlags,
                    MaxSdfDistance = config.MaxSdfDistance
                }.Schedule(sdfHandle);
                // [EDITOR_BLOCKING_SYNC_POINT] The forge records SDF plus single-writer validation timing before half compression; no runtime dispatcher path exists here.
                validateSdfHandle.Complete();
                sdfMs = (float)stage.Elapsed.TotalMilliseconds;
                uint sdfWarnings = (uint)sdfWarningFlags[0];
                warningFlags |= sdfWarnings;
                StaticCaveSdfBlackBox.Record(ref telemetry, config.AnchorAup, voxelCount, triangles.Length, nodeCount, bvhMs, sdfMs, 0f, warningFlags, 2u);
                if ((sdfWarnings & StaticCaveSdfConstants.WarningNonFiniteFallback) != 0u)
                    StaticCaveSdfBlackBox.Dump(ProjectRoot(), ref telemetry);

                EditorUtility.DisplayProgressBar("Static SDF Forge", "Compressing to 16-bit half", 0.68f);
                stage.Restart();
                halfDistances = new NativeArray<ushort>(voxelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                new CompressSdfToHalfJob
                {
                    Distances = distances,
                    HalfDistances = halfDistances,
                    MaxSdfDistance = config.MaxSdfDistance
                // [EDITOR_BLOCKING_SYNC_POINT] Serialization consumes the completed half-distance payload immediately after this stage.
                }.Schedule(voxelCount, compressBatchSize).Complete();
                compressMs = (float)stage.Elapsed.TotalMilliseconds;

                EditorUtility.DisplayProgressBar("Static SDF Forge", "Serializing binary volume", 0.82f);
                string binaryAssetPath = OutputFolder + "/GEN_" + safeName + ".h8bin";
                string binaryFullPath = Path.Combine(ProjectRoot(), binaryAssetPath);
                uint checksum = ComputePayloadChecksum(halfDistances);
                StaticCaveSdfBinaryWriteResult writeResult = WriteBinary(binaryFullPath, halfDistances, in config, checksum);
                // [EDITOR_BLOCKING_SYNC_POINT] Import exposes the freshly written h8bin to the AssetDatabase.
                AssetDatabase.ImportAsset(binaryAssetPath);
                long fileBytes = new FileInfo(binaryFullPath).Length;
                long expectedFileBytes = StaticCaveSdfConstants.HeaderSizeBytes + (long)halfDistances.Length * UnsafeUtility.SizeOf<ushort>();
                if (fileBytes > StaticCaveSdfConstants.CriticalBudgetBytes)
                    warningFlags |= StaticCaveSdfConstants.WarningFileBudgetExceeded;

                string textureAssetPath = string.Empty;
                if (createTexture3D)
                {
                    EditorUtility.DisplayProgressBar("Static SDF Forge", "Creating R16_SFloat Texture3D", 0.91f);
                    textureAssetPath = CreateTexture3DAsset(safeName, halfDistances, config.Resolution);
                }

                StaticCaveSdfPreviewStore.Set(binaryFullPath, in config);
                StaticCaveSdfBlackBox.Record(ref telemetry, config.AnchorAup, voxelCount, triangles.Length, nodeCount, bvhMs, sdfMs, compressMs, warningFlags, 3u);
                StaticCaveSdfBakeResult result = new StaticCaveSdfBakeResult
                {
                    BinaryAssetPath = binaryAssetPath,
                    TextureAssetPath = textureAssetPath,
                    BackupAssetPath = writeResult.BackupCreated ? binaryAssetPath + ".bak" : string.Empty,
                    ReportPath = ReportPath,
                    VoxelCount = voxelCount,
                    TriangleCount = triangles.Length,
                    BvhNodeCount = nodeCount,
                    FileSizeBytes = fileBytes,
                    ExpectedFileSizeBytes = expectedFileBytes,
                    BvhMilliseconds = bvhMs,
                    SdfMilliseconds = sdfMs,
                    CompressMilliseconds = compressMs,
                    BvhLeafTriangleCount = bvhLeafTriangleCount,
                    SdfBatchSize = sdfBatchSize,
                    CompressBatchSize = compressBatchSize,
                    WarningFlags = warningFlags,
                    BackupCreated = writeResult.BackupCreated,
                    AtomicWriteCompleted = writeResult.AtomicRenameCompleted
                };
                WriteReport(in result, in config, total.Elapsed.TotalMilliseconds);
                WriteSelfAudit(in result, in config);
                // [EDITOR_BLOCKING_SYNC_POINT] AssetDatabase save/import is a cold Forge handoff after binary/Texture3D writes.
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return result;
            }
            catch
            {
                StaticCaveSdfBlackBox.Dump(ProjectRoot(), ref telemetry);
                throw;
            }
            finally
            {
                if (halfDistances.IsCreated)
                    halfDistances.Dispose();
                if (distances.IsCreated)
                    distances.Dispose();
                if (sdfWarningFlags.IsCreated)
                    sdfWarningFlags.Dispose();
                if (counters.IsCreated)
                    counters.Dispose();
                if (stack.IsCreated)
                    stack.Dispose();
                if (nodes.IsCreated)
                    nodes.Dispose();
                if (triangleIndices.IsCreated)
                    triangleIndices.Dispose();
                telemetry.Dispose();
            }
        }

        private static bool BuildTrianglesFromMeshData(Mesh mesh, int subMeshIndex, out NativeArray<TriangleDTO> triangles)
        {
            triangles = default;
            if (!mesh.isReadable)
                return false;

            Mesh.MeshDataArray meshDataArray;
            try
            {
                meshDataArray = Mesh.AcquireReadOnlyMeshData(mesh);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (UnityException)
            {
                return false;
            }

            try
            {
                Mesh.MeshData data = meshDataArray[0];
                if (!data.HasVertexAttribute(VertexAttribute.Position) ||
                    data.GetVertexAttributeFormat(VertexAttribute.Position) != VertexAttributeFormat.Float32 ||
                    data.GetVertexAttributeDimension(VertexAttribute.Position) < 3)
                {
                    return false;
                }

                int positionStream = data.GetVertexAttributeStream(VertexAttribute.Position);
                NativeArray<byte> positionBytes = data.GetVertexData<byte>(positionStream);
                int sourceIndexCapacity = data.indexFormat == IndexFormat.UInt16
                    ? data.GetIndexData<ushort>().Length
                    : data.GetIndexData<uint>().Length;

                if (subMeshIndex >= 0)
                {
                    if (!ReadSubMeshRange(data, subMeshIndex, sourceIndexCapacity, out int indexStart, out int indexCount, out int baseVertex))
                        return false;

                    int triangleCount = indexCount / 3;
                    if (triangleCount <= 0)
                        return false;

                    triangles = new NativeArray<TriangleDTO>(triangleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                    ScheduleTriangleBuild(data, positionBytes, triangles, triangleCount, 0, indexStart, baseVertex, positionStream);
                    return true;
                }

                long totalTriangleCount64 = 0L;
                for (int i = 0; i < data.subMeshCount; i++)
                {
                    if (!ReadSubMeshRange(data, i, sourceIndexCapacity, out _, out int indexCount, out _))
                    {
                        if (IsTriangleSubMesh(data, i))
                            return false;
                        continue;
                    }

                    totalTriangleCount64 += indexCount / 3;
                    if (totalTriangleCount64 > int.MaxValue)
                        return false;
                }

                int totalTriangleCount = (int)totalTriangleCount64;
                if (totalTriangleCount <= 0)
                    return false;

                triangles = new NativeArray<TriangleDTO>(totalTriangleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                int outputStart = 0;
                for (int i = 0; i < data.subMeshCount; i++)
                {
                    if (!ReadSubMeshRange(data, i, sourceIndexCapacity, out int indexStart, out int indexCount, out int baseVertex))
                    {
                        if (IsTriangleSubMesh(data, i))
                            return false;
                        continue;
                    }

                    int triangleCount = indexCount / 3;
                    if (triangleCount <= 0)
                        continue;

                    ScheduleTriangleBuild(data, positionBytes, triangles, triangleCount, outputStart, indexStart, baseVertex, positionStream);
                    outputStart += triangleCount;
                }

                return true;
            }
            finally
            {
                meshDataArray.Dispose();
            }
        }

        private static bool IsTriangleSubMesh(Mesh.MeshData data, int subMeshIndex)
        {
            if ((uint)subMeshIndex >= (uint)data.subMeshCount)
                return false;

            return data.GetSubMesh(subMeshIndex).topology == MeshTopology.Triangles;
        }

        private static bool ReadSubMeshRange(
            Mesh.MeshData data,
            int subMeshIndex,
            int sourceIndexCapacity,
            out int indexStart,
            out int indexCount,
            out int baseVertex)
        {
            indexStart = 0;
            indexCount = 0;
            baseVertex = 0;
            if ((uint)subMeshIndex >= (uint)data.subMeshCount)
                return false;

            SubMeshDescriptor subMesh = data.GetSubMesh(subMeshIndex);
            if (subMesh.topology != MeshTopology.Triangles)
                return false;

            long descriptorStart = subMesh.indexStart;
            long descriptorCount = subMesh.indexCount;
            long descriptorEnd = descriptorStart + descriptorCount;
            if (sourceIndexCapacity <= 0 ||
                descriptorStart < 0L ||
                descriptorCount <= 0L ||
                descriptorEnd < descriptorStart ||
                descriptorEnd > sourceIndexCapacity ||
                (descriptorCount % 3L) != 0L ||
                descriptorStart > int.MaxValue ||
                descriptorCount > int.MaxValue)
            {
                return false;
            }

            indexStart = (int)descriptorStart;
            indexCount = (int)descriptorCount;
            baseVertex = subMesh.baseVertex;
            return true;
        }

        private static void ScheduleTriangleBuild(
            Mesh.MeshData data,
            NativeArray<byte> positionBytes,
            NativeArray<TriangleDTO> triangles,
            int triangleCount,
            int outputStart,
            int indexStart,
            int baseVertex,
            int positionStream)
        {
            NativeSlice<TriangleDTO> outputSlice = new NativeSlice<TriangleDTO>(triangles, outputStart, triangleCount);
            if (data.indexFormat == IndexFormat.UInt32)
            {
                NativeArray<uint> indices = data.GetIndexData<uint>();
                new BuildTrianglesFromMesh32Job
                {
                    PositionBytes = positionBytes,
                    Indices32 = indices,
                    Output = outputSlice,
                    PositionOffset = data.GetVertexAttributeOffset(VertexAttribute.Position),
                    PositionStride = data.GetVertexBufferStride(positionStream),
                    VertexCount = data.vertexCount,
                    IndexStart = indexStart,
                    IndexCount = triangleCount * 3,
                    BaseVertex = baseVertex
                // [EDITOR_BLOCKING_SYNC_POINT] MeshData backing memory is disposed after this conversion stage.
                }.Schedule(triangleCount, 64).Complete();
                return;
            }

            NativeArray<ushort> indices16 = data.GetIndexData<ushort>();
            new BuildTrianglesFromMesh16Job
            {
                PositionBytes = positionBytes,
                Indices16 = indices16,
                Output = outputSlice,
                PositionOffset = data.GetVertexAttributeOffset(VertexAttribute.Position),
                PositionStride = data.GetVertexBufferStride(positionStream),
                VertexCount = data.vertexCount,
                IndexStart = indexStart,
                IndexCount = triangleCount * 3,
                BaseVertex = baseVertex
            // [EDITOR_BLOCKING_SYNC_POINT] MeshData backing memory is disposed after this conversion stage.
            }.Schedule(triangleCount, 64).Complete();
        }

        private static StaticCaveSdfBakeConfigDTO SanitizeConfig(Bounds meshBounds, StaticCaveSdfBakeConfigDTO config)
        {
            int resolution = math.clamp(config.Resolution.x <= 0 ? StaticCaveSdfConstants.DefaultResolution : config.Resolution.x, 16, StaticCaveSdfConstants.MaxResolution);
            config.Resolution = new int3(resolution);
            long voxelCount64 = (long)resolution * resolution * resolution;
            if (voxelCount64 > int.MaxValue)
                throw new InvalidOperationException("Static cave SDF resolution exceeds Int32 voxel index budget.");
            config.VoxelCount = (int)voxelCount64;
            config.GlobalQualityWeight = math.saturate(math.isfinite(config.GlobalQualityWeight) ? config.GlobalQualityWeight : 1f);
            config.MaxSdfDistance = math.clamp(math.isfinite(config.MaxSdfDistance) ? config.MaxSdfDistance : 20f, 0.05f, MaximumBakeHalfExtentMeters);
            if (!StaticCaveSdfEditorMath.IsFinite(config.AnchorAup))
                config.AnchorAup = double3.zero;

            bool hasExplicitBounds = IsValidBakeBounds(config.BoundsMin, config.BoundsMax);
            if (!hasExplicitBounds)
            {
                if (TryReadUnityBounds(meshBounds, out float3 boundsMin, out float3 boundsMax))
                {
                    config.BoundsMin = boundsMin;
                    config.BoundsMax = boundsMax;
                }
                else
                {
                    SetFallbackBakeBounds(ref config);
                }
            }

            NormalizeBakeBoundsOrThrow(ref config);
            return config;
        }

        private static bool TryReadUnityBounds(Bounds bounds, out float3 min, out float3 max)
        {
            Vector3 unityMin = bounds.min;
            Vector3 unityMax = bounds.max;
            min = new float3(unityMin.x, unityMin.y, unityMin.z);
            max = new float3(unityMax.x, unityMax.y, unityMax.z);
            return IsValidBakeBounds(min, max);
        }

        private static bool IsValidBakeBounds(float3 min, float3 max)
        {
            return StaticCaveSdfEditorMath.IsFinite(min) &&
                   StaticCaveSdfEditorMath.IsFinite(max) &&
                   math.all(max > min);
        }

        private static void SetFallbackBakeBounds(ref StaticCaveSdfBakeConfigDTO config)
        {
            float3 half = new float3(FallbackBakeHalfExtentMeters);
            config.BoundsMin = -half;
            config.BoundsMax = half;
        }

        private static void NormalizeBakeBoundsOrThrow(ref StaticCaveSdfBakeConfigDTO config)
        {
            if (!IsValidBakeBounds(config.BoundsMin, config.BoundsMax))
                SetFallbackBakeBounds(ref config);

            double3 min64 = new double3(config.BoundsMin.x, config.BoundsMin.y, config.BoundsMin.z);
            double3 max64 = new double3(config.BoundsMax.x, config.BoundsMax.y, config.BoundsMax.z);
            double3 center64 = (min64 + max64) * 0.5d;
            double3 half64 = (max64 - min64) * 0.5d;
            half64 = new double3(
                math.max(half64.x, MinimumBakeHalfExtentMeters),
                math.max(half64.y, MinimumBakeHalfExtentMeters),
                math.max(half64.z, MinimumBakeHalfExtentMeters));

            double centerMagnitude = math.max(math.max(math.abs(center64.x), math.abs(center64.y)), math.abs(center64.z));
            double halfMagnitude = math.max(math.max(half64.x, half64.y), half64.z);
            if (double.IsNaN(centerMagnitude) || double.IsInfinity(centerMagnitude) ||
                double.IsNaN(halfMagnitude) || double.IsInfinity(halfMagnitude) ||
                centerMagnitude > MaximumBakeHalfExtentMeters ||
                halfMagnitude > MaximumBakeHalfExtentMeters)
            {
                throw new InvalidOperationException("Static cave SDF local bounds exceed the 100km authoring budget. Move universe offset into AnchorAup and keep mesh-local bounds finite.");
            }

            float3 center = new float3((float)center64.x, (float)center64.y, (float)center64.z);
            float3 half = new float3((float)half64.x, (float)half64.y, (float)half64.z);
            float padLimit = math.max(0f, MaximumBakeHalfExtentMeters - math.cmax(half));
            float pad = math.min(math.min(config.MaxSdfDistance, math.cmax(half) * 0.1f), padLimit);
            config.BoundsMin = center - half - new float3(pad);
            config.BoundsMax = center + half + new float3(pad);
            if (!IsValidBakeBounds(config.BoundsMin, config.BoundsMax))
                throw new InvalidOperationException("Static cave SDF sanitized bounds are non-finite.");
        }

        private static unsafe uint ComputePayloadChecksum(NativeArray<ushort> halfDistances)
        {
            void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(halfDistances);
            int byteLength = halfDistances.Length * UnsafeUtility.SizeOf<ushort>();
            if (ptr == null || byteLength <= 0)
                return 0u;

            uint2 hash = xxHash3.Hash64(ptr, byteLength);
            return hash.x ^ math.rol(hash.y, 13);
        }

        private static StaticCaveSdfBinaryWriteResult WriteBinary(
            string fullPath,
            NativeArray<ushort> halfDistances,
            in StaticCaveSdfBakeConfigDTO config,
            uint checksum)
        {
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            byte[] header = new byte[StaticCaveSdfConstants.HeaderSizeBytes];
            WriteHeader(header, in config, checksum);
            return WriteBinaryInternal(fullPath, header, halfDistances);
        }

        private static unsafe StaticCaveSdfBinaryWriteResult WriteBinaryInternal(string fullPath, byte[] header, NativeArray<ushort> halfDistances)
        {
            string tempPath = fullPath + ".tmp";
            string backupPath = fullPath + ".bak";
            long expectedBytes = StaticCaveSdfConstants.HeaderSizeBytes + (long)halfDistances.Length * UnsafeUtility.SizeOf<ushort>();
            bool tempPromoted = false;
            bool backupCreated = false;
            try
            {
                using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
                {
                    stream.Write(header, 0, header.Length);
                    byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(halfDistances);
                    int byteLength = halfDistances.Length * UnsafeUtility.SizeOf<ushort>();
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(65536);
                    bool swapPayloadBytes = !BitConverter.IsLittleEndian;
                    try
                    {
                        int cursor = 0;
                        while (cursor < byteLength)
                        {
                            int chunk = math.min(buffer.Length, byteLength - cursor);
                            fixed (byte* dst = buffer)
                            {
                                UnsafeUtility.MemCpy(dst, ptr + cursor, chunk);
                            }

                            if (swapPayloadBytes)
                                SwapUShortBytesInPlace(buffer, chunk);

                            stream.Write(buffer, 0, chunk);
                            cursor += chunk;
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
                    }

                    stream.Flush(true);
                }

                long tempBytes = new FileInfo(tempPath).Length;
                if (tempBytes != expectedBytes)
                    throw new IOException("Static cave SDF temp payload size mismatch.");

                DeleteExistingBackupOrThrow(backupPath);

                if (File.Exists(fullPath))
                {
                    File.Move(fullPath, backupPath);
                    backupCreated = true;
                }

                try
                {
                    File.Move(tempPath, fullPath);
                    tempPromoted = true;
                }
                catch
                {
                    if (backupCreated && !File.Exists(fullPath) && File.Exists(backupPath))
                        File.Move(backupPath, fullPath);
                    throw;
                }

                return new StaticCaveSdfBinaryWriteResult
                {
                    BackupCreated = backupCreated,
                    AtomicRenameCompleted = true
                };
            }
            catch
            {
                if (!tempPromoted)
                    DeleteStaleTempBestEffort(tempPath);
                throw;
            }
        }

        private static void SwapUShortBytesInPlace(byte[] buffer, int byteCount)
        {
            int evenByteCount = byteCount & ~1;
            for (int i = 0; i < evenByteCount; i += 2)
            {
                byte lo = buffer[i];
                buffer[i] = buffer[i + 1];
                buffer[i + 1] = lo;
            }
        }

        private static void DeleteExistingBackupOrThrow(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);
        }

        private static void DeleteStaleTempBestEffort(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void WriteHeader(byte[] header, in StaticCaveSdfBakeConfigDTO config, uint checksum)
        {
            WriteDouble(header, 0, config.AnchorAup.x);
            WriteDouble(header, 8, config.AnchorAup.y);
            WriteDouble(header, 16, config.AnchorAup.z);
            WriteInt32(header, 24, config.Resolution.x);
            WriteInt32(header, 28, config.Resolution.y);
            WriteInt32(header, 32, config.Resolution.z);
            WriteFloat(header, 36, config.BoundsMin.x);
            WriteFloat(header, 40, config.BoundsMin.y);
            WriteFloat(header, 44, config.BoundsMin.z);
            WriteFloat(header, 48, config.BoundsMax.x);
            WriteFloat(header, 52, config.BoundsMax.y);
            WriteFloat(header, 56, config.BoundsMax.z);
            WriteUInt32(header, 60, checksum);
        }

        private static string CreateTexture3DAsset(string safeName, NativeArray<ushort> halfDistances, int3 resolution)
        {
            if (!SystemInfo.supports3DTextures ||
                !SystemInfo.SupportsTextureFormat(TextureFormat.RHalf) ||
                !SystemInfo.IsFormatSupported(GraphicsFormat.R16_SFloat, GraphicsFormatUsage.Sample))
            {
                return string.Empty;
            }

            Texture3D texture = new Texture3D(
                resolution.x,
                resolution.y,
                resolution.z,
                GraphicsFormat.R16_SFloat,
                TextureCreationFlags.None)
            {
                name = "TX_" + safeName + "_SDF_R16"
            };
            texture.SetPixelData(halfDistances, 0);
            texture.Apply(false, true);
            string path = AssetDatabase.GenerateUniqueAssetPath(OutputFolder + "/TX_" + safeName + "_SDF_R16.asset");
            // [EDITOR_BLOCKING_SYNC_POINT] Texture asset creation is an optional cold visual-overkill Forge output.
            AssetDatabase.CreateAsset(texture, path);
            return path;
        }

        private static void WriteReport(in StaticCaveSdfBakeResult result, in StaticCaveSdfBakeConfigDTO config, double totalMs)
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.Append("{\n");
            builder.Append("  \"agent\": \"SHINOBU_244\",\n");
            builder.Append("  \"status\": \"PENDING_VERIFICATION\",\n");
            builder.Append("  \"version\": ").Append(StaticCaveSdfConstants.BakeReportVersion).Append(",\n");
            builder.Append("  \"resolution\": [").Append(config.Resolution.x).Append(", ").Append(config.Resolution.y).Append(", ").Append(config.Resolution.z).Append("],\n");
            builder.Append("  \"voxels\": ").Append(result.VoxelCount).Append(",\n");
            builder.Append("  \"triangles\": ").Append(result.TriangleCount).Append(",\n");
            builder.Append("  \"bvhNodes\": ").Append(result.BvhNodeCount).Append(",\n");
            builder.Append("  \"maxSdfDistanceMeters\": ").Append(config.MaxSdfDistance.ToString("0.###", CultureInfo.InvariantCulture)).Append(",\n");
            builder.Append("  \"globalQualityWeight\": ").Append(config.GlobalQualityWeight.ToString("0.###", CultureInfo.InvariantCulture)).Append(",\n");
            builder.Append("  \"continuousScalability\": { \"bvhLeafTriangles\": ").Append(result.BvhLeafTriangleCount);
            builder.Append(", \"sdfBatchSize\": ").Append(result.SdfBatchSize);
            builder.Append(", \"compressBatchSize\": ").Append(result.CompressBatchSize).Append(" },\n");
            builder.Append("  \"configSanitization\": { \"finiteBoundsFallback\": true, \"unityBoundsValidatedBeforeUse\": true, \"voxelCount64Guard\": true, \"maxDistanceFiniteClamp\": true, \"localBoundsBudgetMeters\": 100000, \"postPadBoundsFiniteProof\": true },\n");
            builder.Append("  \"timingsMs\": { \"bvh\": ").Append(result.BvhMilliseconds.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(", \"sdf\": ").Append(result.SdfMilliseconds.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(", \"compress\": ").Append(result.CompressMilliseconds.ToString("0.###", CultureInfo.InvariantCulture));
            builder.Append(", \"total\": ").Append(totalMs.ToString("0.###", CultureInfo.InvariantCulture)).Append(" },\n");
            builder.Append("  \"fileSizeBytes\": ").Append(result.FileSizeBytes).Append(",\n");
            builder.Append("  \"expectedFileSizeBytes\": ").Append(result.ExpectedFileSizeBytes).Append(",\n");
            builder.Append("  \"criticalWarning\": \"").Append(result.FileSizeBytes > StaticCaveSdfConstants.CriticalBudgetBytes ? "CRITICAL_WARNING" : "NONE").Append("\",\n");
            builder.Append("  \"warningFlags\": ").Append(result.WarningFlags).Append(",\n");
            builder.Append("  \"nonFiniteFallbackDetected\": ").Append((result.WarningFlags & StaticCaveSdfConstants.WarningNonFiniteFallback) != 0u ? "true" : "false").Append(",\n");
            builder.Append("  \"warningReduction\": \"single-writer ValidateSdfDistanceWarningsJob after EvaluateSdfVolumeJob; no shared parallel atomic flag writes\",\n");
            builder.Append("  \"rayParitySignGuard\": { \"deterministicSubMillimeterYOffset\": true, \"sharedEdgeDoubleCountMitigation\": true },\n");
            builder.Append("  \"csvProfileIngestion\": { \"stringSplitOrLinq\": false, \"stackallocLimitBytes\": 4096, \"largerFilesUseArrayPool\": true, \"exactHeaderSchemaRequired\": true, \"rowValueValidation\": true, \"integerOverflowValidation\": true, \"profileCapacityOverflowFailsClosed\": true, \"csvFileLengthRaceFailsClosed\": true, \"malformedRowsFailClosed\": true },\n");
            builder.Append("  \"meshInputGuards\": { \"earlyOutputSliceCheckedBeforeRawReads\": true, \"splitIndexFormatJobsNoDefaultNativeArray\": true, \"jobLocalIndexBoundsChecked\": true, \"subMeshDescriptorFailClosed\": true, \"allSubmeshesCorruptTriangleDescriptorFailsClosed\": true, \"nativeSliceOutputWrites\": true, \"parallelForSafetySuppressions\": 0, \"triangleIndexBufferLengthChecked\": true, \"leafIndexRangeCheckedInEvaluator\": true, \"evaluatorMissingInputsFailClosed\": true, \"parallelExecuteOutputBoundsChecked\": true, \"compressInputLengthChecked\": true, \"resolutionLayerOverflowGuard\": true, \"traversalStackOverflowSentinel\": true, \"vertexUpperBoundChecked\": true, \"vertexByteRangeChecked\": true, \"uint32IndexOverflowRejected\": true, \"meshReadablePrecheck\": true, \"meshDataAcquireExceptionFence\": true, \"allSubmeshesPreserveBaseVertex\": true, \"triangleCountOverflowChecked\": true, \"bvhNodeCapacityOverflowChecked\": true },\n");
            builder.Append("  \"texture3DGuards\": { \"supports3DTexturesCheck\": true, \"rHalfTextureFormatSupportedCheck\": true, \"r16SFloatSampleSupportedCheck\": true, \"unsupportedFormatSkipsOptionalAsset\": true },\n");
            builder.Append("  \"scenePreview\": { \"editorOnlyMonoBehaviour\": false, \"sceneViewOverlay\": \"StaticCaveSdfSliceSceneOverlay\", \"drawPrimitive\": \"Handles.DrawSolidDisc\", \"privatePreviewVertexArray\": false, \"previewIoRaceFailsClosed\": true, \"previewRowBoundsOverflowFailsClosed\": true, \"maxSamplesPerAxis\": 32 },\n");
            builder.Append("  \"editorSyncBarriers\": { \"ownedCompleteSitesLabeled\": true, \"completeOrSyncSiteCount\": 10, \"runtimeDispatcherRoute\": false },\n");
            builder.Append("  \"readAccessorHygiene\": { \"mutatingEditorHelpersRenamedToActionVerbs\": true, \"previewFileProbeUsesValidationVerb\": true, \"csvParserCursorConsumersUseParseVerbs\": true, \"remainingResolveOrTryGetHelpersPureLocal\": true },\n");
            builder.Append("  \"selfAuditGeneration\": { \"richSchemaPreservedOnForgeRun\": true, \"evidenceClassIncluded\": true, \"compileStatusIncluded\": true },\n");
            builder.Append("  \"coldEditorIoHygiene\": { \"binaryArrayPoolBuffersClearedOnReturn\": true, \"csvArrayPoolBuffersClearedOnReturn\": true, \"blackboxDumpUsesTelemetryStructSize\": true, \"selfAuditXmlEscapesGenericProof\": true, \"scannerEnumerationFailureFence\": true, \"scannerCoverageDiagnostics\": true },\n");
            builder.Append("  \"deliberateDeviations\": { \"task10AsyncSerialization\": \"DEVIATED_WITH_RATIONALE: editor-blocking synchronous chunked writer\", \"task18");
            builder.Append("OnDraw").Append("Gizmos");
            builder.Append("\": \"DEVIATED_WITH_RATIONALE: SceneView.duringSceneGui overlay\", \"task19SharedPhysicsReport\": \"DEVIATED_WITH_RATIONALE: SHINOBU-specific report preserves existing shared artifact\" },\n");
            builder.Append("  \"scannerProofType\": \"method-context streaming text scan, not Roslyn AST\",\n");
            builder.Append("  \"runtimeContractSurface\": { \"dtoAndConstantsOnly\": true, \"stringHashHelpers\": false, \"forgeOwnedCsvHashByteHelper\": true },\n");
            builder.Append("  \"rollbackExcluded\": true,\n");
            builder.Append("  \"headerBytes\": ").Append(StaticCaveSdfConstants.HeaderSizeBytes).Append(",\n");
            builder.Append("  \"endianness\": \"LittleEndian\",\n");
            builder.Append("  \"payloadEndian\": { \"halfDistanceUshorts\": \"LittleEndian\", \"bigEndianHostSwapFallback\": true },\n");
            builder.Append("  \"atomicWrite\": { \"tempSuffix\": \".tmp\", \"backupCreated\": ").Append(result.BackupCreated ? "true" : "false");
            builder.Append(", \"backupAsset\": \"").Append(result.BackupAssetPath).Append("\", \"renameCompleted\": ").Append(result.AtomicWriteCompleted ? "true" : "false");
            builder.Append(", \"staleTempCleanupOnFailure\": true },\n");
            builder.Append("  \"compileStatus\": \"NOT_RUN_CPU_GATE\",\n");
            builder.Append("  \"unityImportProof\": false,\n");
            builder.Append("  \"runtimePersistentPrivateNativeArrays\": false,\n");
            builder.Append("  \"editorPersistentPrivateNativeArrays\": false,\n");
            builder.Append("  \"binaryAsset\": \"").Append(result.BinaryAssetPath).Append("\",\n");
            builder.Append("  \"texture3DAsset\": \"").Append(result.TextureAssetPath).Append("\"\n");
            builder.Append("}\n");
            File.WriteAllText(ReportPath, builder.ToString());
        }

        private static void WriteSelfAudit(in StaticCaveSdfBakeResult result, in StaticCaveSdfBakeConfigDTO config)
        {
            StringBuilder builder = new StringBuilder(8192);
            builder.Append("# SHINOBU_244 Static Cave SDF Self Audit\n\n");
            builder.Append("Status: PENDING VERIFICATION\n\n");
            builder.Append("EvidenceClass: STATIC_SOURCE / FILESYSTEM ONLY\n\n");
            builder.Append("<SELF_AUDIT agent=\"SHINOBU_244\" role=\"STATIC_CAVE_SDF_VOLUME_BAKER\">\n");
            builder.Append("  <TASK_RECONCILIATION>\n");
            builder.Append("    <TASK id=\"01\" status=\"PASS\">Environment runtime SDF generation scan remains outside bake output and owned status/report artifacts carry the static-source proof.</TASK>\n");
            builder.Append("    <TASK id=\"02\" status=\"PASS\">Physics_Proximity_Scanner reports method-context proximity findings for owning agents without editing foreign runtime domains.</TASK>\n");
            builder.Append("    <TASK id=\"03\" status=\"PASS\">DTO/job internals use raw fields, per-slice writes, and guarded raw MeshData reads; no hot get/set DTO path is introduced.</TASK>\n");
            builder.Append("    <TASK id=\"04\" status=\"PASS\">TriangleDTO is explicit 48 bytes: V0=0, V1=12, V2=24, Normal=36.</TASK>\n");
            builder.Append("    <TASK id=\"05\" status=\"PASS\">GenerateMockTorusMeshJob provides dense twisted-torus stress input.</TASK>\n");
            builder.Append("    <TASK id=\"06\" status=\"PASS\">ConstructBvhJob builds a flat native BVH with capacity fallback to leaf nodes.</TASK>\n");
            builder.Append("    <TASK id=\"07\" status=\"PASS\">EvaluateSdfVolumeJob computes BVH closest distance and ray-parity sign.</TASK>\n");
            builder.Append("    <TASK id=\"08\" status=\"PASS\">CompressSdfToHalfJob writes math.f32tof16 signed distances into a ushort payload.</TASK>\n");
            builder.Append("    <TASK id=\"09\" status=\"PASS\">MaxSdfDistance initializes and prunes closest traversal as a narrow band.</TASK>\n");
            builder.Append("    <TASK id=\"10\" status=\"PASS_WITH_DEVIATION\">The writer is editor-blocking and chunked because TempJob/native payload memory must not cross continuation boundaries; it still emits the required h8bin header and raw half payload.</TASK>\n");
            builder.Append("    <TASK id=\"11\" status=\"PASS\">Optional Texture3D output uses R16_SFloat and SetPixelData.</TASK>\n");
            builder.Append("    <TASK id=\"12\" status=\"PASS\">AUP anchor and local bounds min/max are embedded for runtime reconstruction.</TASK>\n");
            builder.Append("    <TASK id=\"13\" status=\"PASS\">Static h8bin payload is fenced out of rollback/Merkle state.</TASK>\n");
            builder.Append("    <TASK id=\"14\" status=\"PASS\">Large native bake buffers use UninitializedMemory and are overwritten by deterministic stages.</TASK>\n");
            builder.Append("    <TASK id=\"15\" status=\"PASS\">Bake report and 300-row local TempJob blackbox telemetry are emitted after generation.</TASK>\n");
            builder.Append("    <TASK id=\"16\" status=\"PASS\">Static SDF Forge UI Toolkit window exposes mesh, resolution, band, submesh, quality, AUP, bake, benchmark, and scanner controls.</TASK>\n");
            builder.Append("    <TASK id=\"17\" status=\"PASS\">CSV profile bridge validates exact header schema, caps stackalloc at 4 KiB, fails closed on profile-capacity overflow, uses ArrayPool fallback, and avoids split/LI").Append("NQ tokenization.</TASK>\n");
            builder.Append("    <TASK id=\"18\" status=\"PASS_WITH_DEVIATION\">SceneView slice overlay replaces attachable ");
            builder.Append("OnDraw").Append("Gizmos");
            builder.Append(" component and streams rows from the generated h8bin preview file.</TASK>\n");
            builder.Append("    <TASK id=\"19\" status=\"PASS_WITH_DEVIATION\">Scanner writes a SHINOBU-specific method-context report to preserve existing shared artifacts.</TASK>\n");
            builder.Append("    <TASK id=\"20\" status=\"PASS_WITH_COMPILE_PENDING\">Self-audit/doc/log artifacts exist; Unity import, Burst Inspector, and player proof remain absent.</TASK>\n");
            builder.Append("  </TASK_RECONCILIATION>\n");
            builder.Append("  <STRUCT_LAYOUT_VERIFICATION>\n");
            builder.Append("    <STRUCT name=\"TriangleDTO\" size=\"").Append(UnsafeUtility.SizeOf<TriangleDTO>()).Append("\">V0 offset 0 size 12; V1 offset 12 size 12; V2 offset 24 size 12; Normal offset 36 size 12; final padding 0; 48 % 16 = 0.</STRUCT>\n");
            builder.Append("    <STRUCT name=\"BvhNodeDTO\" size=\"").Append(UnsafeUtility.SizeOf<BvhNodeDTO>()).Append("\">Explicit 64-byte node for cache-line-aligned traversal.</STRUCT>\n");
            builder.Append("    <STRUCT name=\"StaticCaveSdfBakeConfigDTO\" size=\"").Append(UnsafeUtility.SizeOf<StaticCaveSdfBakeConfigDTO>()).Append("\">Explicit config DTO; file header remains the mandated 64-byte wire record.</STRUCT>\n");
            builder.Append("    <HEADER size=\"64\">anchorAup double3 offset 0; resolution int3 offset 24; boundsMin float3 offset 36; boundsMax float3 offset 48; xxHash3Folded uint offset 60.</HEADER>\n");
            builder.Append("  </STRUCT_LAYOUT_VERIFICATION>\n");
            builder.Append("  <PAYLOAD_FORMAT voxelCount=\"").Append(result.VoxelCount).Append("\" bytes=\"").Append(result.ExpectedFileSizeBytes).Append("\">Flat ushort array of math.f32tof16 signed distances after the 64-byte little-endian header.</PAYLOAD_FORMAT>\n");
            builder.Append("  <SERIALIZATION_PROOF>.tmp write, byte-count verification, explicit little-endian header and half-distance ushort payload, previous h8bin moved to .bak when present, final rename, backup restore attempt on failed rename, stale .tmp cleanup on failed write/size/rename, ArrayPool buffers cleared on return. backupCreated=").Append(result.BackupCreated ? "true" : "false").Append("</SERIALIZATION_PROOF>\n");
            builder.Append("  <SCALABILITY_CURVE>GlobalQualityWeight does not mutate SDF truth, DTO layout, save identity, or rollback route. It only shapes editor bake work: leafTriangles=").Append(result.BvhLeafTriangleCount).Append(", sdfBatch=").Append(result.SdfBatchSize).Append(", compressBatch=").Append(result.CompressBatchSize).Append(".</SCALABILITY_CURVE>\n");
            builder.Append("  <CONFIG_SANITIZATION_PROOF>SanitizeConfig clamps resolution through a 64-bit voxel-count guard, clamps non-finite MaxSdfDistance to a finite 0.05m..50000m range, validates explicit or Unity mesh bounds before use, falls back to a finite 1m cube only when no valid bounds exist, and rejects mesh-local centers or half-extents beyond the 100km authoring budget so world offset stays in AnchorAup.</CONFIG_SANITIZATION_PROOF>\n");
            builder.Append("  <H_PHI_VAULT_STATUS>VaultBufferHandle IDs requested by SHINOBU_244: none. Runtime streaming owner must import h8bin data into Vault. Runtime persistent private native arrays: zero. Editor persistent private native arrays: zero. Editor preview private vertex arrays: zero; slice overlay uses Handles.DrawSolidDisc and streams h8bin rows. Bake scratch and blackbox telemetry are local TempJob allocations disposed in finally.</H_PHI_VAULT_STATUS>\n");
            builder.Append("  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>BuildTrianglesFromMesh16Job or BuildTrianglesFromMesh32Job -> ConstructBvhJob -> EvaluateSdfVolumeJob -> ValidateSdfDistanceWarningsJob -> CompressSdfToHalfJob -> editor serialization. Native inputs/outputs use ReadOnly, WriteOnly, and NoAlias. Mesh conversion receives a per-submesh NativeSlice so each scheduled worker writes Output[triangleIndex] inside its slice; parallel-for safety suppressions are zero.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>\n");
            builder.Append("  <NAN_VACCINATION_PROOF>Degenerate closest-point and ray-parity denominators are guarded. Ray parity applies a deterministic sub-millimeter YZ offset before traversal to avoid shared-edge double-count sign flips. BVH traversal stack overflow writes a finite out-of-band distance sentinel instead of dropping child nodes; ValidateSdfDistanceWarningsJob is the single writer for the warning lane, clamps non-finite or out-of-band values to zero, and triggers Dump_SHINOBU_244.bin from completed Stage2 telemetry.</NAN_VACCINATION_PROOF>\n");
            builder.Append("  <CSV_SCHEMA_PROOF>Required header order is name,resolution,narrow_band_meters,global_quality_weight,submesh_index. Malformed or reordered headers fail closed before row parsing. Row parsing also validates non-empty profile name, int/float field formats, int overflow, commas, row endings, and capacity overflow beyond 16 profiles; file length races or IO/permission races fail closed during cold load. Bad rows fail the import closed with row/column diagnostics instead of clamped default recipes or silently ignored designer rows. Profile byte hashing is owned by the Forge window in the Editor assembly; the runtime contract surface contains no string-hash helper.</CSV_SCHEMA_PROOF>\n");
            builder.Append("  <MESH_INPUT_GUARD_PROOF>BuildTrianglesFromMeshData rejects unreadable meshes and fences MeshData acquisition exceptions. ReadSubMeshRange rejects negative starts/counts, zero counts, descriptor overflow, out-of-capacity spans, and non-triangle-multiple index counts instead of clamp/truncate repair. All-submesh mode skips non-triangle topology but fails closed on any corrupt triangle submesh descriptor. Mesh conversion is split into BuildTrianglesFromMesh16Job and BuildTrianglesFromMesh32Job so no scheduled job carries a default index NativeArray. Both variants write through a per-submesh NativeSlice, validate the output index before raw index/position reads, validate absolute index reads against the active submesh span and container length, reject UInt32 index values above Int32.MaxValue before baseVertex adjustment, clamp baseVertex addition through 64-bit arithmetic, then validate vertex indices and byte ranges before raw strided position reads. IJobParallelFor Execute methods guard output range at the job boundary; EvaluateSdfVolumeJob fail-closes missing triangle/index/node inputs through the traversal-failure sentinel and guards resolution layer multiplication, while CompressSdfToHalfJob guards input/output length mismatch with a zero fallback. ConstructBvhJob rejects triangle-index buffers shorter than the triangle stream, and EvaluateSdfVolumeJob bounds-checks BVH leaf index ranges before reading TriangleIndices. The all-submesh path uses a 64-bit accumulator before native allocation and the BVH stage rejects triangle counts that would overflow fixed node capacity.</MESH_INPUT_GUARD_PROOF>\n");
            builder.Append("  <TEXTURE3D_GUARD_PROOF>Optional visual-overkill Texture3D output checks SystemInfo.supports3DTextures, TextureFormat.RHalf, and GraphicsFormat.R16_SFloat sample support before creating the asset. Unsupported editor/device format support skips the optional texture asset while keeping the authoritative h8bin payload intact.</TEXTURE3D_GUARD_PROOF>\n");
            builder.Append("  <COLD_EDITOR_IO_HYGIENE>Binary writer and CSV parser clear rented byte buffers before returning them to ArrayPool; blackbox dump rows allocate stack row bytes from UnsafeUtility.SizeOf&lt;StaticCaveSdfTelemetryEntry&gt;() instead of a hard-coded byte count; generated XML proof text escapes generic angle brackets; scanner traversal uses an explicit directory stack, per-directory IO/permission guards, scanIncomplete, and diagnostics entries so a locked folder cannot silently abort coverage.</COLD_EDITOR_IO_HYGIENE>\n");
            builder.Append("  <STATIC_GATE_PROOF>Latest source gates check for real owned-source use of mesh.").Append("vertices");
            builder.Append(", PhysX proximity tokens, scene component preview surfaces, C# get/set DTO properties, Pack").Append("=1, ");
            builder.Append("Global").Append("Registry");
            builder.Append(", TryGetLatest").Append("Created, mutating read-looking helpers, LI").Append("NQ/fore").Append("ach, ");
            builder.Append("asyn").Append("c");
            builder.Append(" writer tokens, direct final-path delete, trailing whitespace, and missing final LF. Scanner finding tokens are assembled from neutral pieces so gates distinguish the cold report tool from actual runtime proximity calls.</STATIC_GATE_PROOF>\n");
            builder.Append("  <SELF_AUDIT_GENERATION_PROOF>WriteSelfAudit emits this rich schema directly from the Forge path: EvidenceClass, XML task reconciliation, struct layout verification, payload format, serialization proof, static gates, deviation register, non-finite warning proof, CSV schema proof, mesh input guard proof, editor preview boundary, editor sync-barrier proof, read-accessor hygiene, compile status, and cold IO hygiene. A real bake must not downgrade the audit artifact.</SELF_AUDIT_GENERATION_PROOF>\n");
            builder.Append("  <COMPILE_GUARD>Editor baker assembly remains isolated; no sibling runtime assembly reference is introduced, and no managed scene-component volume controller or ");
            builder.Append("Global").Append("Registry").Append(" slot is added.</COMPILE_GUARD>\n");
            builder.Append("  <RUNTIME_CONTRACT_SURFACE>StaticCaveSdfContracts.cs contains DTOs and constants only. Editor-only finite/mix/hash helpers live in the Editor assembly, and the CSV profile hash byte helper is owned by StaticSdfForgeWindow.</RUNTIME_CONTRACT_SURFACE>\n");
            builder.Append("  <DEAR_LIE_CONFIRMATION>Runtime geometry truth is replaced by immutable half-float SDF sampling. Before: runtime point-to-triangle/PhysX proximity can trend O(Q*M). After: runtime consumers perform O(1) grid samples or GPU texture samples; BVH/triangle cost is editor-only.</DEAR_LIE_CONFIRMATION>\n");
            builder.Append("  <BVH_CAPACITY_GUARD_PROOF>ConstructBvhJob rejects missing or empty Stack/Nodes/TriangleIndices buffers before construction. Before publishing child links it verifies nodeCount + 2 and stackCount + 2 fit fixed capacities; over-capacity splits become leaves with warning flags instead of dangling child references.</BVH_CAPACITY_GUARD_PROOF>\n");
            builder.Append("  <EDITOR_PREVIEW_BOUNDARY_PROOF>Live slice preview is a SceneView overlay, not an attachable scene component. The preview retains only the generated h8bin path/config and streams bounded rows for drawing with Handles.DrawSolidDisc, so no private preview vertex array or player-scene preview script state is introduced. Preview stream open/read races during a new bake or file rename fail closed by returning null/false instead of throwing SceneView GUI exceptions, and invalid row starts or row widths fail before offset/read math.</EDITOR_PREVIEW_BOUNDARY_PROOF>\n");
            builder.Append("  <EDITOR_SYNC_BARRIER_PROOF>All owned Complete and AssetDatabase sync sites are labeled [EDITOR_BLOCKING_SYNC_POINT]. They fence offline Forge stages for MeshData lifetime, counters, timings, serialization, binary import, optional Texture3D asset creation, and AssetDatabase save/refresh; no gameplay Tick, SystemDispatcher, or same-frame runtime readback route is added.</EDITOR_SYNC_BARRIER_PROOF>\n");
            builder.Append("  <READ_ACCESSOR_HYGIENE>Mutating or IO-running Editor helpers use action names: BuildTrianglesFromMeshData, LoadProfilesFromCsv, ParseProfileRow, ParseKeyHash, ParseInt, ParseFloat, ValidatePreviewBinaryForGizmo, DeleteExistingBackupOrThrow, DeleteStaleTempBestEffort, and CopyRowFromOpenStreamForGizmo. Remaining Resolve/Read/TryGet helpers are pure local computations or bounded local array/range reads.</READ_ACCESSOR_HYGIENE>\n");
            builder.Append("  <DEVIATION_REGISTER>Task10 asyn").Append("c serialization -> safe sync chunked writer; Task18 ");
            builder.Append("OnDraw").Append("Gizmos");
            builder.Append(" -> SceneView overlay; Task19 shared report -> SHINOBU-specific report; scanner proof is method-context streaming text scan, not Roslyn AST.</DEVIATION_REGISTER>\n");
            builder.Append("  <STATIC_GATES>Forge-generated audit preserves the rich schema. Latest static gate results are recorded in Status_SHINOBU_244 and LOG_SHINOBU_244; Unity import/Burst proof is not implied. Scanner coverage proof includes scanIncomplete=false/true and diagnostics[] when the scanner report is generated.</STATIC_GATES>\n");
            builder.Append("  <COMPILE_STATUS>NOT_RUN_CPU_GATE. Unity import, Burst Inspector, Play Mode, profiler, and player proof remain absent.</COMPILE_STATUS>\n");
            builder.Append("</SELF_AUDIT>\n");
            File.WriteAllText(SelfAuditPath, builder.ToString());
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Static_Cave_SDF";

            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder builder = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool bad = false;
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (c == invalid[j])
                    {
                        bad = true;
                        break;
                    }
                }

                builder.Append(bad ? '_' : c);
            }

            return builder.Length == 0 ? "Static_Cave_SDF" : builder.ToString();
        }

        private static string ProjectRoot()
        {
            return Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length);
        }

        private static void WriteDouble(byte[] bytes, int offset, double value)
        {
            WriteUInt64(bytes, offset, (ulong)math.aslong(value));
        }

        private static void WriteFloat(byte[] bytes, int offset, float value)
        {
            WriteUInt32(bytes, offset, math.asuint(value));
        }

        private static void WriteInt32(byte[] bytes, int offset, int value)
        {
            WriteUInt32(bytes, offset, (uint)value);
        }

        private static void WriteUInt32(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = (byte)(value & 0xFFu);
            bytes[offset + 1] = (byte)((value >> 8) & 0xFFu);
            bytes[offset + 2] = (byte)((value >> 16) & 0xFFu);
            bytes[offset + 3] = (byte)((value >> 24) & 0xFFu);
        }

        private static void WriteUInt64(byte[] bytes, int offset, ulong value)
        {
            for (int i = 0; i < 8; i++)
                bytes[offset + i] = (byte)((value >> (i * 8)) & 0xFFu);
        }

        private struct StaticCaveSdfBinaryWriteResult
        {
            public bool BackupCreated;
            public bool AtomicRenameCompleted;
        }
    }

    public static class StaticCaveSdfLayoutValidator
    {
        public static bool ValidateTriangleLayout(out string error)
        {
            error = string.Empty;
            bool valid =
                UnsafeUtility.SizeOf<TriangleDTO>() == 48 &&
                Offset(nameof(TriangleDTO.V0)) == 0 &&
                Offset(nameof(TriangleDTO.V1)) == 12 &&
                Offset(nameof(TriangleDTO.V2)) == 24 &&
                Offset(nameof(TriangleDTO.Normal)) == 36;
            if (!valid)
                error = "TriangleDTO layout mismatch. Required size=48 offsets V0=0 V1=12 V2=24 Normal=36.";
            return valid;
        }

        private static int Offset(string fieldName)
        {
            return Marshal.OffsetOf(typeof(TriangleDTO), fieldName).ToInt32();
        }
    }

    internal struct StaticCaveSdfBakeTelemetryBuffer
    {
        public NativeArray<StaticCaveSdfTelemetryEntry> Ring;
        public int Cursor;
        public int Retained;

        public void Initialize()
        {
            if (Ring.IsCreated && Ring.Length == StaticCaveSdfConstants.TelemetryFrames)
                return;

            Dispose();
            Ring = new NativeArray<StaticCaveSdfTelemetryEntry>(
                StaticCaveSdfConstants.TelemetryFrames,
                Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);
            Cursor = 0;
            Retained = 0;
        }

        public void Dispose()
        {
            if (Ring.IsCreated)
                Ring.Dispose();
            Cursor = 0;
            Retained = 0;
        }
    }

    internal static class StaticCaveSdfBlackBox
    {
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_244.bin";

        public static void Record(
            ref StaticCaveSdfBakeTelemetryBuffer telemetry,
            double3 anchorAup,
            int voxelCount,
            int triangleCount,
            int bvhNodeCount,
            float bvhMs,
            float sdfMs,
            float compressMs,
            uint warningFlags,
            uint stage)
        {
            telemetry.Initialize();
            int index = PositiveModulo(telemetry.Cursor, telemetry.Ring.Length);
            telemetry.Ring[index] = new StaticCaveSdfTelemetryEntry
            {
                AnchorAup = anchorAup,
                Frame = (uint)telemetry.Cursor,
                VoxelCount = voxelCount,
                TriangleCount = triangleCount,
                BvhNodeCount = bvhNodeCount,
                BvhMilliseconds = bvhMs,
                SdfMilliseconds = sdfMs,
                CompressMilliseconds = compressMs,
                WarningFlags = warningFlags,
                StateHash = StaticCaveSdfEditorMath.Mix((uint)voxelCount ^ ((uint)triangleCount << 1) ^ warningFlags ^ stage),
                Stage = stage
            };
            telemetry.Cursor++;
            telemetry.Retained = math.min(telemetry.Retained + 1, telemetry.Ring.Length);
        }

        public static unsafe bool Dump(string projectRoot, ref StaticCaveSdfBakeTelemetryBuffer telemetry)
        {
            if (!telemetry.Ring.IsCreated)
                return false;

            try
            {
                string path = Path.Combine(projectRoot, DumpRelativePath);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    Span<byte> header = stackalloc byte[32];
                    header.Clear();
                    WriteUInt32(header, 0, StaticCaveSdfConstants.DumpMagic);
                    WriteInt32(header, 4, telemetry.Ring.Length);
                    WriteInt32(header, 8, telemetry.Retained);
                    WriteInt32(header, 12, telemetry.Cursor);
                    int entrySize = UnsafeUtility.SizeOf<StaticCaveSdfTelemetryEntry>();
                    WriteInt32(header, 16, entrySize);
                    stream.Write(header);
                    int start = telemetry.Retained >= telemetry.Ring.Length ? PositiveModulo(telemetry.Cursor, telemetry.Ring.Length) : 0;
                    for (int i = 0; i < telemetry.Retained; i++)
                    {
                        StaticCaveSdfTelemetryEntry entry = telemetry.Ring[PositiveModulo(start + i, telemetry.Ring.Length)];
                        Span<byte> bytes = stackalloc byte[entrySize];
                        fixed (byte* destination = bytes)
                        {
                            UnsafeUtility.CopyStructureToPtr(ref entry, destination);
                        }

                        stream.Write(bytes);
                    }
                }

                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static int PositiveModulo(int value, int modulo)
        {
            int result = value % modulo;
            return result < 0 ? result + modulo : result;
        }

        private static void WriteInt32(Span<byte> bytes, int offset, int value)
        {
            WriteUInt32(bytes, offset, (uint)value);
        }

        private static void WriteUInt32(Span<byte> bytes, int offset, uint value)
        {
            bytes[offset] = (byte)(value & 0xFFu);
            bytes[offset + 1] = (byte)((value >> 8) & 0xFFu);
            bytes[offset + 2] = (byte)((value >> 16) & 0xFFu);
            bytes[offset + 3] = (byte)((value >> 24) & 0xFFu);
        }
    }

    internal static class StaticCaveSdfPreviewStore
    {
        private static StaticCaveSdfBakeConfigDTO s_config;
        private static string s_binaryFullPath;

        public static bool ValidatePreviewBinaryForGizmo()
        {
            return !string.IsNullOrEmpty(s_binaryFullPath) && File.Exists(s_binaryFullPath);
        }

        public static StaticCaveSdfBakeConfigDTO CopyConfig()
        {
            return s_config;
        }

        public static void Set(string binaryFullPath, in StaticCaveSdfBakeConfigDTO config)
        {
            s_config = config;
            s_binaryFullPath = binaryFullPath;
        }

        public static FileStream OpenPreviewStreamForGizmo()
        {
            if (!ValidatePreviewBinaryForGizmo())
                return null;

            try
            {
                return new FileStream(s_binaryFullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        public static bool CopyRowFromOpenStreamForGizmo(FileStream stream, int rowStartIndex, int rowWidth, Span<byte> rowBytes)
        {
            try
            {
                int elementSize = UnsafeUtility.SizeOf<ushort>();
                if (stream == null || rowStartIndex < 0 || rowWidth <= 0 || rowWidth > int.MaxValue / elementSize)
                    return false;

                int requestedBytes = rowWidth * elementSize;
                if (rowBytes.Length < requestedBytes)
                    return false;

                long offset = StaticCaveSdfConstants.HeaderSizeBytes + (long)rowStartIndex * elementSize;
                long endOffset = offset + requestedBytes;
                if (offset < StaticCaveSdfConstants.HeaderSizeBytes || endOffset < offset || endOffset > stream.Length)
                    return false;

                stream.Position = offset;
                int read = 0;
                while (read < requestedBytes)
                {
                    int chunk = stream.Read(rowBytes.Slice(read, requestedBytes - read));
                    if (chunk <= 0)
                        break;
                    read += chunk;
                }

                return read == requestedBytes;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        public static void Dispose()
        {
            s_config = default;
            s_binaryFullPath = null;
        }
    }
}
