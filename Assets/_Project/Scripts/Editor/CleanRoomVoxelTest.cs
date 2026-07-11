#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Hecton8.Editor
{
    public static class CleanRoomVoxelTest
    {
        private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static readonly string OutputDir = Path.Combine(ProjectRoot, "Docs", "Reports", "CleanRoom");
        private static readonly string BeautyPath = Path.Combine(OutputDir, "Cave_Beauty.png");
        private static readonly string XRayPath = Path.Combine(OutputDir, "Cave_SDF_Slice_XRay.png");
        private static readonly string TelemetryPath = Path.Combine(OutputDir, "Cave_Voxel_Telemetry.txt");
        private const int MarchingCubesEdgeTableLength = 256;
        private const int MarchingCubesTriTableLength = 4096;
        private const int GridDimension = 64;
        private const float VoxelStep = 3f;
        private const float TerrainHeight = 0f;
        private const uint Seed = 0xA8B155u;
        private const int FaultSlotCount = 8;

        [MenuItem("Hecton8/Tests/Clean Room Voxels")]
        public static void MenuExecute()
        {
            Execute();
        }

        public static void Execute()
        {
            int exitCode = 0;
            GlobalDataVault proofVault = null;
            GameObject engineObject = null;
            GameObject meshObject = null;
            Camera camera = null;
            ComputeBuffer dummyProbeGrid = null;
            ComputeBuffer dummyGiRelayShBuffer = null;
            ComputeBuffer dummyBiomeInfluenceGrid = null;
            try
            {
                Directory.CreateDirectory(OutputDir);
                DeleteStaleArtifact(BeautyPath);
                DeleteStaleArtifact(XRayPath);
                DeleteStaleArtifact(TelemetryPath);
                DeleteStaleArtifact(Path.Combine(OutputDir, "Cave_Render_Analysis.txt"));
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                proofVault = GlobalDataVault.Create();

                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.006f, 0.009f, 0.012f, 1f);

                engineObject = new GameObject("CleanRoom_HectonVoxelEngine") { hideFlags = HideFlags.HideAndDontSave };
                _ = engineObject.AddComponent<global::HectonVoxelEngine>();

                CleanRoomVoxelBuffers buffers = default;
                try
                {
                    BuildVoxelProof(ref buffers, proofVault);
                    meshObject = BuildMeshObject(buffers);
                    camera = BuildCamera(meshObject, buffers);
                    dummyProbeGrid = new ComputeBuffer(1, 16, ComputeBufferType.Structured);
                    dummyProbeGrid.SetData(new[] { new Vector4(0f, 0f, 0f, 0f) });
                    Shader.SetGlobalBuffer("_H8CustomLightProbeGrid", dummyProbeGrid);
                    dummyGiRelayShBuffer = new ComputeBuffer(27, sizeof(float), ComputeBufferType.Structured);
                    dummyGiRelayShBuffer.SetData(new float[27]);
                    Shader.SetGlobalBuffer("_HectonGIRelaySHBuffer", dummyGiRelayShBuffer);
                    dummyBiomeInfluenceGrid = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Structured);
                    dummyBiomeInfluenceGrid.SetData(new uint[] { 0u });
                    Shader.SetGlobalBuffer("_HectonScatterBiomeInfluenceGrid", dummyBiomeInfluenceGrid);
                    Shader.SetGlobalInt("_HectonScatterBiomeInfluenceGridCount", 0);
                    Shader.SetGlobalVector("_HectonScatterBiomeInfluenceGridOrigin", Vector4.zero);
                    Shader.SetGlobalVector("_HectonScatterBiomeInfluenceGridParams", Vector4.zero);
                    Shader.SetGlobalVector("_H8CustomLightProbeGridState", Vector4.zero);
                    Shader.SetGlobalVector("_H8InteriorGIProbeParams", Vector4.zero);
                    Shader.SetGlobalVector("_H8InteriorGIProbeOrigin", Vector4.zero);
                    Shader.SetGlobalVector("_H8InteriorGIProbeRootAup", Vector4.zero);
                    RenderBeauty(camera);
                    ExportXRay(buffers);
                    WriteTelemetry(buffers);
                    Debug.Log(string.Format(CultureInfo.InvariantCulture, "[CleanRoomVoxel] CaveVolumeRatio={0:F6} RawVertices={1} WeldedVertices={2} SpawnCandidates={3}", buffers.CaveVolumeRatio, buffers.RawCount, buffers.WeldedCount, buffers.SpawnCount));
                    Debug.Log("[CleanRoomVoxel] Clean-room voxel proof complete.");
                }
                finally
                {
                    buffers.Dispose();
                }
            }
            catch (Exception ex)
            {
                exitCode = 1;
                Debug.LogError("[CleanRoomVoxel] FAILURE: " + ex);
            }
            finally
            {
                RenderTexture.active = null;
                if (dummyProbeGrid != null)
                {
                    dummyProbeGrid.Dispose();
                    dummyProbeGrid = null;
                }
                if (dummyGiRelayShBuffer != null)
                {
                    dummyGiRelayShBuffer.Dispose();
                    dummyGiRelayShBuffer = null;
                }
                if (dummyBiomeInfluenceGrid != null)
                {
                    dummyBiomeInfluenceGrid.Dispose();
                    dummyBiomeInfluenceGrid = null;
                }
                DestroyCamera(camera);
                DestroyMeshObject(meshObject);
                if (engineObject != null)
                    Object.DestroyImmediate(engineObject);
                MCTables.Shutdown();
                if (proofVault != null)
                    proofVault.Dispose();
                if (Application.isBatchMode)
                    global::System.Environment.Exit(exitCode);
            }
        }

        private static void DeleteStaleArtifact(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        private static void BuildVoxelProof(ref CleanRoomVoxelBuffers buffers, IDataVault proofVault)
        {
            int pts = GridDimension + 1;
            int totalPts = pts * pts * pts;
            int totalCells = GridDimension * GridDimension * GridDimension;
            buffers.Pts = pts;
            buffers.TotalCells = totalCells;
            buffers.WorldCenter = new Vector3(0f, -150f, 0f);
            buffers.VolumeOrigin = (float3)buffers.WorldCenter - new float3(GridDimension, GridDimension, GridDimension) * VoxelStep * 0.5f;
            buffers.VoxelStep = VoxelStep;
            buffers.Density = new NativeArray<float>(totalPts, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            buffers.SmoothDensity = new NativeArray<float>(totalPts, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            buffers.QuantizedDensity = new NativeArray<sbyte>(totalPts, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            buffers.FaultFlags = new NativeArray<int>(FaultSlotCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            buffers.CellVertexCounts = new NativeArray<int>(totalCells, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            buffers.CellVertexOffsets = new NativeArray<int>(totalCells, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            float caveThreshold = 0.65f;
            float carveStrength = 28f;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                RunDensity(buffers, caveThreshold, carveStrength);
                buffers.CaveVolumeRatio = ComputeOpenRatio(buffers.Density);
                Debug.Log(string.Format(CultureInfo.InvariantCulture, "[CleanRoomVoxel] DensityAttempt={0} CaveThreshold={1:F3} CarveStrength={2:F2} CaveVolumeRatio={3:F6}", attempt + 1, caveThreshold, carveStrength, buffers.CaveVolumeRatio));
                if (buffers.CaveVolumeRatio >= 0.15f && buffers.CaveVolumeRatio <= 0.25f)
                    break;

                if (buffers.CaveVolumeRatio <= 0.0001f || buffers.CaveVolumeRatio < 0.15f)
                {
                    caveThreshold = math.max(0.28f, caveThreshold - 0.08f);
                    carveStrength = math.min(40f, carveStrength + 3f);
                }
                else
                {
                    caveThreshold = math.min(1.35f, caveThreshold + 0.08f);
                    carveStrength = math.max(10f, carveStrength - 2f);
                }
            }

            buffers.FinalCaveThreshold = caveThreshold;
            buffers.FinalCarveStrength = carveStrength;
            if (buffers.CaveVolumeRatio <= 0.0001f || buffers.CaveVolumeRatio > 0.40f)
                throw new InvalidOperationException("Cave volume ratio outside hard limits: " + buffers.CaveVolumeRatio.ToString("F6", CultureInfo.InvariantCulture));

            float densityDecodeScale = math.max(VoxelStep * 0.125f, 0.005f);
            new VoxelDensityQuantizeJob
            {
                densityDecodeInvScale = 1f / densityDecodeScale,
                density = buffers.Density,
                quantizedDensity = buffers.QuantizedDensity,
                densityFaultFlags = buffers.FaultFlags
            }.Schedule(totalPts, 64).Complete();

            if (proofVault == null)
                throw new InvalidOperationException("Clean-room DataVault unavailable.");

            MCTables.JobTableLease tables = default;
            NativeArray<int>.ReadOnly edgeTable;
            NativeArray<int>.ReadOnly triTable;
            bool disposeTableLease = false;
            MCTables.Initialize(proofVault);
            if (MCTables.TryAcquireEditorReadOnlyJobTables(proofVault, out tables, out string tableFailureReason))
            {
                edgeTable = tables.EdgeTable;
                triTable = tables.TriTable;
                disposeTableLease = true;
            }
            else
            {
                EnsureCleanRoomMarchingCubesTables(ref buffers);
                edgeTable = buffers.EdgeTable.AsReadOnly();
                triTable = buffers.TriTable.AsReadOnly();
                Debug.Log($"[CleanRoomVoxel] Using local marching-cubes table fallback. DataVaultReady={MCTables.IsReady} Reason={tableFailureReason}");
            }

            try
            {
                new VoxelMCCountJob
                {
                    cellsX = GridDimension,
                    cellsY = GridDimension,
                    cellsZ = GridDimension,
                    ptsX = pts,
                    ptsY = pts,
                    ptsZ = pts,
                    densityDecodeScale = densityDecodeScale,
                    density = buffers.QuantizedDensity,
                    edgeTable = edgeTable,
                    triTable = triTable,
                    cellVertexCounts = buffers.CellVertexCounts,
                    densityFaultFlags = buffers.FaultFlags
                }.Schedule(totalCells, 64).Complete();

                int rawCount = 0;
                for (int i = 0; i < totalCells; i++)
                {
                    buffers.CellVertexOffsets[i] = rawCount;
                    rawCount += math.max(0, buffers.CellVertexCounts[i]);
                }

                if (rawCount < 3)
                    throw new InvalidOperationException("Marching cubes produced no cave surface.");

                buffers.RawCount = rawCount;
                buffers.RawVertices = new NativeArray<MCRawVertex>(rawCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                buffers.WeldedPositions = new NativeArray<float3>(rawCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                buffers.TriangleIndices = new NativeArray<int>(rawCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                buffers.WeldedCounter = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                buffers.EdgeVertexX = new NativeArray<int>(GridDimension * pts * pts, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                buffers.EdgeVertexY = new NativeArray<int>(pts * GridDimension * pts, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                buffers.EdgeVertexZ = new NativeArray<int>(pts * pts * GridDimension, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                JobHandle clearX = new VoxelFillIntArrayJob { Value = -1, Values = buffers.EdgeVertexX }.Schedule(buffers.EdgeVertexX.Length, 64);
                JobHandle clearY = new VoxelFillIntArrayJob { Value = -1, Values = buffers.EdgeVertexY }.Schedule(buffers.EdgeVertexY.Length, 64);
                JobHandle clearZ = new VoxelFillIntArrayJob { Value = -1, Values = buffers.EdgeVertexZ }.Schedule(buffers.EdgeVertexZ.Length, 64);
                JobHandle clearEdges = JobHandle.CombineDependencies(clearX, clearY, clearZ);

                new VoxelMCExtractJob
                {
                    cellsX = GridDimension,
                    cellsY = GridDimension,
                    cellsZ = GridDimension,
                    ptsX = pts,
                    ptsY = pts,
                    ptsZ = pts,
                    volumeOrigin = buffers.VolumeOrigin,
                    voxelStep = VoxelStep,
                    densityDecodeScale = densityDecodeScale,
                    density = buffers.QuantizedDensity,
                    edgeTable = edgeTable,
                    triTable = triTable,
                    cellVertexOffsets = buffers.CellVertexOffsets,
                    cellVertexCounts = buffers.CellVertexCounts,
                    outVertices = buffers.RawVertices,
                    densityFaultFlags = buffers.FaultFlags
                }.Schedule(totalCells, 64, clearEdges).Complete();
            }
            finally
            {
                if (disposeTableLease)
                    tables.Dispose();
            }

            new VoxelWeldJob
            {
                rawCount = buffers.RawCount,
                ptsX = pts,
                ptsY = pts,
                ptsZ = pts,
                rawVertices = buffers.RawVertices,
                edgeVertexX = buffers.EdgeVertexX,
                edgeVertexY = buffers.EdgeVertexY,
                edgeVertexZ = buffers.EdgeVertexZ,
                weldedPositions = buffers.WeldedPositions,
                triangleIndices = buffers.TriangleIndices,
                weldedCounter = buffers.WeldedCounter,
                densityFaultFlags = buffers.FaultFlags
            }.Schedule().Complete();

            buffers.WeldedCount = buffers.WeldedCounter[0];
            if (buffers.WeldedCount < 3)
                throw new InvalidOperationException("Weld produced no valid cave vertices.");

            buffers.Normals = new NativeArray<float3>(buffers.WeldedCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            buffers.Curvature = new NativeArray<float>(buffers.WeldedCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            buffers.AmbientOcclusion = new NativeArray<float>(buffers.WeldedCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            buffers.Colors = new NativeArray<Color32>(buffers.WeldedCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            buffers.SkirtAlpha = new NativeArray<float>(buffers.WeldedCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            buffers.DirtyBlend = new NativeArray<float>(buffers.WeldedCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            buffers.Biome = new NativeArray<float>(buffers.WeldedCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            buffers.SpawnPoints = new NativeArray<CaveSpawnData>(math.max(64, buffers.WeldedCount / 20), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            buffers.SpawnCounter = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            new VoxelNormalJob
            {
                ptsX = pts,
                ptsY = pts,
                ptsZ = pts,
                densityStrideY = pts,
                densityStrideZ = pts * pts,
                volumeOrigin = buffers.VolumeOrigin,
                invVoxelStep = 1f / VoxelStep,
                densityField = buffers.QuantizedDensity,
                positions = buffers.WeldedPositions,
                normals = buffers.Normals,
                curvatureValues = buffers.Curvature,
                ambientOcclusionValues = buffers.AmbientOcclusion,
                densityFaultFlags = buffers.FaultFlags
            }.Schedule(buffers.WeldedCount, 64).Complete();

            NativeArray<float> terrainHeights = new NativeArray<float>(pts * pts, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<float> gridBiome = new NativeArray<float>(pts * pts, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<CaveEntrance> colorCaveEntrances = new NativeArray<CaveEntrance>(0, Allocator.TempJob);
            NativeArray<VoxelModifiedCellEntry> colorModifiedCells = new NativeArray<VoxelModifiedCellEntry>(0, Allocator.TempJob);
            NativeArray<int> colorModifiedCellBucketHeads = new NativeArray<int>(0, Allocator.TempJob);
            NativeArray<int> colorModifiedCellNext = new NativeArray<int>(0, Allocator.TempJob);
            try
            {
                for (int i = 0; i < terrainHeights.Length; i++)
                    terrainHeights[i] = TerrainHeight;

                new VoxelColorJob
                {
                    maxDepth = 500f,
                    caveEdgeWidth = 3f,
                    seamTransitionBand = 3f,
                    volumeCenter = (float3)buffers.WorldCenter,
                    volumeHalfExtent = GridDimension * VoxelStep * 0.5f,
                    ptsX = pts,
                    ptsZ = pts,
                    volumeOrigin = buffers.VolumeOrigin,
                    voxelStep = VoxelStep,
                    lodLevel = 0,
                    lodTransitionBand = 0f,
                    positions = buffers.WeldedPositions,
                    normals = buffers.Normals,
                    terrainHeights = terrainHeights,
                    gridBiome = gridBiome,
                    curvatureValues = buffers.Curvature,
                    ambientOcclusionValues = buffers.AmbientOcclusion,
                    biomeValues = buffers.Biome,
                    caveEntrances = colorCaveEntrances,
                    modifiedCells = colorModifiedCells,
                    modifiedCellBucketHeads = colorModifiedCellBucketHeads,
                    modifiedCellNext = colorModifiedCellNext,
                    absoluteCellOffset = double3.zero,
                    colors = buffers.Colors,
                    skirtAlphaValues = buffers.SkirtAlpha
                }.Schedule(buffers.WeldedCount, 64).Complete();
            }
            finally
            {
                terrainHeights.Dispose();
                gridBiome.Dispose();
                colorCaveEntrances.Dispose();
                colorModifiedCells.Dispose();
                colorModifiedCellBucketHeads.Dispose();
                colorModifiedCellNext.Dispose();
            }

            new VoxelSpawnPointJob
            {
                positions = buffers.WeldedPositions,
                normals = buffers.Normals,
                ambientOcclusionValues = buffers.AmbientOcclusion,
                volumeCenter = (float3)buffers.WorldCenter,
                volumeHalfExtent = GridDimension * VoxelStep * 0.5f,
                floorNormalThreshold = 0.75f,
                minInteriorDepth = 0.05f,
                keepFraction = 0.08f,
                seed = Seed,
                spawnPoints = buffers.SpawnPoints,
                spawnPointCount = buffers.SpawnCounter,
                spawnPointCapacity = buffers.SpawnPoints.Length
            }.Schedule().Complete();
            buffers.SpawnCount = buffers.SpawnCounter[0];
        }

        private static void EnsureCleanRoomMarchingCubesTables(ref CleanRoomVoxelBuffers buffers)
        {
            if (!buffers.EdgeTable.IsCreated)
                buffers.EdgeTable = new NativeArray<int>(MarchingCubesEdgeTableLength, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            if (!buffers.TriTable.IsCreated)
                buffers.TriTable = new NativeArray<int>(MarchingCubesTriTableLength, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            string sourcePath = Path.Combine(Application.dataPath, "_Project", "Scripts", "HectonVoxelEngine.cs");
            string source = File.ReadAllText(sourcePath);
            FillCleanRoomIntTableFromStackalloc(source, "ReadOnlySpan<int> et = stackalloc int[256]", buffers.EdgeTable, MarchingCubesEdgeTableLength, "edge");
            FillCleanRoomIntTableFromStackalloc(source, "ReadOnlySpan<int> tt = stackalloc int[4096]", buffers.TriTable, MarchingCubesTriTableLength, "tri");
        }

        private static void FillCleanRoomIntTableFromStackalloc(string source, string markerText, NativeArray<int> target, int expectedLength, string label)
        {
            int marker = source.IndexOf(markerText, StringComparison.Ordinal);
            if (marker < 0)
                throw new InvalidOperationException($"Clean-room marching-cubes {label} table marker is unavailable.");
            int braceStart = source.IndexOf('{', marker);
            int braceEnd = source.IndexOf("};", braceStart, StringComparison.Ordinal);
            if (braceStart < 0 || braceEnd <= braceStart)
                throw new InvalidOperationException($"Clean-room marching-cubes {label} table body is unavailable.");

            int count = 0;
            int i = braceStart + 1;
            while (i < braceEnd && count < expectedLength)
            {
                while (i < braceEnd && !IsIntegerStart(source[i]))
                    i++;
                if (i >= braceEnd)
                    break;

                int sign = 1;
                if (source[i] == '-')
                {
                    sign = -1;
                    i++;
                }

                int value = 0;
                if (i + 1 < braceEnd && source[i] == '0' && (source[i + 1] == 'x' || source[i + 1] == 'X'))
                {
                    i += 2;
                    while (i < braceEnd && TryHexValue(source[i], out int hex))
                    {
                        value = value * 16 + hex;
                        i++;
                    }
                }
                else
                {
                    while (i < braceEnd && source[i] >= '0' && source[i] <= '9')
                    {
                        value = value * 10 + (source[i] - '0');
                        i++;
                    }
                }

                target[count++] = value * sign;
            }

            if (count != expectedLength)
                throw new InvalidOperationException($"Clean-room marching-cubes {label} table parsed {count} entries, expected {expectedLength}.");
        }

        private static bool IsIntegerStart(char c)
        {
            return c == '-' || (c >= '0' && c <= '9');
        }

        private static bool TryHexValue(char c, out int value)
        {
            if (c >= '0' && c <= '9')
            {
                value = c - '0';
                return true;
            }
            if (c >= 'a' && c <= 'f')
            {
                value = c - 'a' + 10;
                return true;
            }
            if (c >= 'A' && c <= 'F')
            {
                value = c - 'A' + 10;
                return true;
            }

            value = 0;
            return false;
        }

        private static void RunDensity(CleanRoomVoxelBuffers buffers, float caveThreshold, float carveStrength)
        {
            int pts = buffers.Pts;
            int totalPts = pts * pts * pts;
            NativeArray<float> terrainHeights = new NativeArray<float>(pts * pts, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<float> gridBiome = new NativeArray<float>(pts * pts, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<CaveNode> caveNodes = new NativeArray<CaveNode>(0, Allocator.TempJob);
            NativeArray<CaveTunnel> caveTunnels = new NativeArray<CaveTunnel>(0, Allocator.TempJob);
            NativeArray<CaveEntrance> caveEntrances = new NativeArray<CaveEntrance>(0, Allocator.TempJob);
            NativeArray<CaveStructure> caveStructures = new NativeArray<CaveStructure>(0, Allocator.TempJob);
            NativeArray<VoxelCraterStamp> craterStamps = new NativeArray<VoxelCraterStamp>(0, Allocator.TempJob);
            NativeArray<VoxelModifiedCellEntry> modifiedCells = new NativeArray<VoxelModifiedCellEntry>(0, Allocator.TempJob);
            NativeArray<int> modifiedCellBucketHeads = new NativeArray<int>(0, Allocator.TempJob);
            NativeArray<int> modifiedCellNext = new NativeArray<int>(0, Allocator.TempJob);
            NativeArray<int> nodeBucketOffsets = new NativeArray<int>(0, Allocator.TempJob);
            NativeArray<int> nodeBucketIndices = new NativeArray<int>(0, Allocator.TempJob);
            NativeArray<int> tunnelBucketOffsets = new NativeArray<int>(0, Allocator.TempJob);
            NativeArray<int> tunnelBucketIndices = new NativeArray<int>(0, Allocator.TempJob);
            try
            {
                for (int i = 0; i < terrainHeights.Length; i++)
                    terrainHeights[i] = TerrainHeight;

                new VoxelDensityJob
                {
                    ptsX = pts,
                    ptsY = pts,
                    ptsZ = pts,
                    volumeOrigin = buffers.VolumeOrigin,
                    voxelStep = VoxelStep,
                    terrainHeights = terrainHeights,
                    gridBiome = gridBiome,
                    caveNodes = caveNodes,
                    caveTunnels = caveTunnels,
                    caveEntrances = caveEntrances,
                    caveStructures = caveStructures,
                    craterStamps = craterStamps,
                    modifiedCells = modifiedCells,
                    modifiedCellBucketHeads = modifiedCellBucketHeads,
                    modifiedCellNext = modifiedCellNext,
                    nodeBucketOffsets = nodeBucketOffsets,
                    nodeBucketIndices = nodeBucketIndices,
                    tunnelBucketOffsets = tunnelBucketOffsets,
                    tunnelBucketIndices = tunnelBucketIndices,
                    caveParams = default,
                    absoluteNoiseOffset = float3.zero,
                    absoluteCellOffset = double3.zero,
                    partitionDimX = 1,
                    partitionDimY = 1,
                    partitionDimZ = 1,
                    partitionOrigin = buffers.VolumeOrigin,
                    partitionInvCellSize = new float3(1f / math.max(GridDimension * VoxelStep, 0.01f)),
                    sealMargin = 0f,
                    lodLevel = 0,
                    lodTransitionBand = 0f,
                    enableBiomeSdfModifiers = 0,
                    PrimaryFrequency = 0.012f,
                    SecondaryFrequency = 0.017f,
                    CarveStrengthMeters = carveStrength,
                    CaveThreshold = caveThreshold,
                    MaxCrustDepthMeters = 400f,
                    SurfaceProtectionMeters = 30f,
                    StrataLayerThicknessMeters = 24f,
                    StrataShelvingStrength = 0.4f,
                    WorldSeed = Seed,
                    density = buffers.Density,
                    smoothDensity = buffers.SmoothDensity,
                    densityFaultFlags = buffers.FaultFlags
                }.Schedule(totalPts, 64).Complete();
            }
            finally
            {
                terrainHeights.Dispose();
                gridBiome.Dispose();
                caveNodes.Dispose();
                caveTunnels.Dispose();
                caveEntrances.Dispose();
                caveStructures.Dispose();
                craterStamps.Dispose();
                modifiedCells.Dispose();
                modifiedCellBucketHeads.Dispose();
                modifiedCellNext.Dispose();
                nodeBucketOffsets.Dispose();
                nodeBucketIndices.Dispose();
                tunnelBucketOffsets.Dispose();
                tunnelBucketIndices.Dispose();
            }
        }

        private static float ComputeOpenRatio(NativeArray<float> density)
        {
            if (!density.IsCreated || density.Length == 0)
                return 0f;

            int open = 0;
            for (int i = 0; i < density.Length; i++)
            {
                if (density[i] < 0f)
                    open++;
            }

            return open / (float)density.Length;
        }

        private static GameObject BuildMeshObject(CleanRoomVoxelBuffers buffers)
        {
            Vector3[] vertices = new Vector3[buffers.WeldedCount];
            Vector3[] normals = new Vector3[buffers.WeldedCount];
            Color32[] colors = new Color32[buffers.WeldedCount];
            Vector4[] bakedAmbientOcclusion = new Vector4[buffers.WeldedCount];
            Vector4[] dirtyBlendUv2 = new Vector4[buffers.WeldedCount];
            Vector4[] absolutePositionWS = new Vector4[buffers.WeldedCount];
            for (int i = 0; i < buffers.WeldedCount; i++)
            {
                float3 p = buffers.WeldedPositions[i];
                float3 n = buffers.Normals[i];
                vertices[i] = new Vector3(p.x, p.y, p.z);
                normals[i] = new Vector3(n.x, n.y, n.z);
                colors[i] = buffers.Colors[i];
                bakedAmbientOcclusion[i] = new Vector4(0f, 0f, 0f, buffers.AmbientOcclusion[i]);
                dirtyBlendUv2[i] = new Vector4(buffers.DirtyBlend[i], 1f, buffers.Curvature[i], 0f);
                absolutePositionWS[i] = new Vector4(p.x, p.y, p.z, p.y);
            }

            int[] indices = new int[buffers.RawCount];
            for (int i = 0; i < buffers.RawCount; i++)
                indices[i] = math.clamp(buffers.TriangleIndices[i], 0, buffers.WeldedCount - 1);

            Mesh mesh = new Mesh { name = "CleanRoom_AbyssalVoxelMesh", indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.colors32 = colors;
            mesh.SetUVs(1, bakedAmbientOcclusion);
            mesh.SetUVs(2, dirtyBlendUv2);
            mesh.SetUVs(3, absolutePositionWS);
            mesh.SetTriangles(indices, 0, true);
            mesh.RecalculateBounds();

            GameObject go = new GameObject("CleanRoom_AbyssalVoxelRock") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.position = Vector3.zero;
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = BuildVoxelMaterial();
            return go;
        }

        private static void DestroyMeshObject(GameObject meshObject)
        {
            if (meshObject == null)
                return;

            Mesh mesh = null;
            Material material = null;
            if (meshObject.TryGetComponent(out MeshFilter filter))
                mesh = filter.sharedMesh;
            if (meshObject.TryGetComponent(out MeshRenderer renderer))
                material = renderer.sharedMaterial;

            Object.DestroyImmediate(meshObject);
            if (mesh != null)
                Object.DestroyImmediate(mesh);
            if (material != null)
                Object.DestroyImmediate(material);
        }

        private static void DestroyCamera(Camera camera)
        {
            if (camera == null)
                return;

            if (camera.targetTexture != null)
            {
                RenderTexture rt = camera.targetTexture;
                camera.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
            Object.DestroyImmediate(camera.gameObject);
        }

        private static Material BuildVoxelMaterial()
        {
            Shader shader = Shader.Find("Hecton8/Environment/Hecton_AbyssalVoxelRock");
            if (shader == null)
                throw new InvalidOperationException("Hecton_AbyssalVoxelRock shader not found.");

            Material material = new Material(shader) { name = "CleanRoom_AbyssalVoxelRock_Material", hideFlags = HideFlags.HideAndDontSave };
            Texture2DArray albedo = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_AlbedoArray.asset");
            Texture2DArray normal = AssetDatabase.LoadAssetAtPath<Texture2DArray>("Assets/_SourceData/Terrain/TextureArrays/Terrain_NormalArray.asset");
            if (albedo != null)
                material.SetTexture("_AlbedoArray", albedo);
            if (normal != null)
                material.SetTexture("_NormalArray", normal);
            material.SetFloat("_VoxelSandArrayIndex", 0f);
            material.SetFloat("_VoxelRockArrayIndex", 3f);
            material.SetFloat("_VoxelTriplanarScale", 0.08f);
            material.SetFloat("_VoxelTriplanarSharpness", 5f);
            material.SetFloat("_VoxelArrayNormalStrength", 0.85f);
            material.SetFloat("_Tiling", 0.12f);
            material.SetFloat("_OcclusionStrength", 1f);
            material.SetFloat("_Smoothness", 0.18f);
            material.SetFloat("_CavityAoNoiseStrength", 0.2f);
            material.SetFloat("_HectonDamageVolumeActive", 0f);
            material.SetFloat("_SargassumCutMaskActive", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            return material;
        }

        private static Camera BuildCamera(GameObject target, CleanRoomVoxelBuffers buffers)
        {
            Bounds bounds = target.GetComponent<MeshFilter>().sharedMesh.bounds;
            Vector3 focus = FindCameraFocus(bounds, buffers);
            GameObject cameraObject = new GameObject("CleanRoom_CaveCamera") { hideFlags = HideFlags.HideAndDontSave };
            cameraObject.transform.position = focus + new Vector3(0f, 3.2f, -18f);
            cameraObject.transform.LookAt(focus + new Vector3(0f, 0.6f, 0f));
            Camera camera = cameraObject.AddComponent<Camera>();
            UniversalAdditionalCameraData urp = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = false;
            urp.renderShadows = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.001f, 0.0015f, 0.002f, 1f);
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 500f;
            camera.fieldOfView = 64f;

            GameObject lightObject = new GameObject("CleanRoom_CaveCamera_PointLight") { hideFlags = HideFlags.HideAndDontSave };
            lightObject.transform.SetParent(cameraObject.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 1.2f, 0.6f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.intensity = 8f;
            light.range = 40f;
            light.color = new Color(0.82f, 0.92f, 1f, 1f);
            return camera;
        }

        private static Vector3 FindCameraFocus(Bounds bounds, CleanRoomVoxelBuffers buffers)
        {
            for (int i = 0; i < buffers.SpawnCount && i < buffers.SpawnPoints.Length; i++)
            {
                float3 p = buffers.SpawnPoints[i].position;
                if (math.all(math.isfinite(p)))
                    return new Vector3(p.x, p.y + 2.0f, p.z);
            }

            return bounds.center;
        }

        private static void RenderBeauty(Camera camera)
        {
            const int width = 1920;
            const int height = 1080;
            RenderTexture rt = new RenderTexture(width, height, 32, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();
            RTHandle handle = null;
            try
            {
                camera.targetTexture = rt;
                camera.Render();
                camera.targetTexture = null;

                Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    tex.Apply(false, false);
                    File.WriteAllBytes(BeautyPath, tex.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    Object.DestroyImmediate(tex);
                }
            }
            finally
            {
                handle?.Release();
                if (camera != null && camera.targetTexture == rt)
                    camera.targetTexture = null;
                RenderTexture.active = null;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }

        private static void ExportXRay(CleanRoomVoxelBuffers buffers)
        {
            NativeArray<Color32> pixels = new NativeArray<Color32>(buffers.Pts * buffers.Pts, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            try
            {
                new SdfSliceExportJob
                {
                    Density = buffers.Density,
                    Pixels = pixels,
                    PtsX = buffers.Pts,
                    PtsY = buffers.Pts,
                    PtsZ = buffers.Pts,
                    SliceZ = buffers.Pts / 2,
                    VolumeOrigin = buffers.VolumeOrigin,
                    VoxelStep = VoxelStep,
                    TerrainHeight = TerrainHeight
                }.Schedule(pixels.Length, 64).Complete();
                WriteColor32Png(XRayPath, pixels, buffers.Pts, buffers.Pts);
            }
            finally
            {
                pixels.Dispose();
            }
        }

        private static void WriteTelemetry(CleanRoomVoxelBuffers buffers)
        {
            string text =
                "CaveVolumeRatio=" + buffers.CaveVolumeRatio.ToString("F6", CultureInfo.InvariantCulture) + "\n" +
                "RawVertices=" + buffers.RawCount + "\n" +
                "WeldedVertices=" + buffers.WeldedCount + "\n" +
                "SpawnCandidates=" + buffers.SpawnCount + "\n" +
                "FinalCaveThreshold=" + buffers.FinalCaveThreshold.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "FinalCarveStrength=" + buffers.FinalCarveStrength.ToString("F3", CultureInfo.InvariantCulture) + "\n" +
                "GridDimension=" + GridDimension + "\n" +
                "VoxelStep=" + VoxelStep.ToString("F3", CultureInfo.InvariantCulture) + "\n";
            File.WriteAllText(TelemetryPath, text, new System.Text.UTF8Encoding(false));
        }

        private static void WriteColor32Png(string path, NativeArray<Color32> pixels, int width, int height)
        {
            NativeArray<byte> png = default;
            try
            {
                png = ImageConversion.EncodeNativeArrayToPNG(pixels, GraphicsFormat.R8G8B8A8_UNorm, (uint)width, (uint)height, 0u);
                if (!png.IsCreated || png.Length == 0)
                    throw new InvalidOperationException($"Native PNG encode returned no bytes for {path}.");
                WriteNativeBytes(path, png);
            }
            finally
            {
                if (png.IsCreated)
                    png.Dispose();
            }
        }

        private static unsafe void WriteNativeBytes(string path, NativeArray<byte> bytes)
        {
            byte* pointer = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(bytes);
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 65536, FileOptions.SequentialScan);
            stream.Write(new ReadOnlySpan<byte>(pointer, bytes.Length));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct SdfSliceExportJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<float> Density;
            [WriteOnly, NoAlias] public NativeArray<Color32> Pixels;
            public int PtsX;
            public int PtsY;
            public int PtsZ;
            public int SliceZ;
            public float3 VolumeOrigin;
            public float VoxelStep;
            public float TerrainHeight;

            public void Execute(int index)
            {
                int x = index % PtsX;
                int y = index / PtsX;
                if (x < 0 || x >= PtsX || y < 0 || y >= PtsY || SliceZ < 0 || SliceZ >= PtsZ)
                {
                    Pixels[index] = new Color32(255, 0, 255, 255);
                    return;
                }

                float worldY = VolumeOrigin.y + y * VoxelStep;
                if (math.abs(worldY - TerrainHeight) <= VoxelStep * 0.5f)
                {
                    Pixels[index] = new Color32(255, 0, 0, 255);
                    return;
                }

                float d = Density[x + y * PtsX + SliceZ * PtsX * PtsY];
                byte v = d < 0f ? (byte)0 : (byte)255;
                Pixels[index] = new Color32(v, v, v, 255);
            }
        }

        private struct CleanRoomVoxelBuffers : IDisposable
        {
            public int Pts;
            public int TotalCells;
            public int RawCount;
            public int WeldedCount;
            public int SpawnCount;
            public float CaveVolumeRatio;
            public float FinalCaveThreshold;
            public float FinalCarveStrength;
            public float VoxelStep;
            public Vector3 WorldCenter;
            public float3 VolumeOrigin;
            public NativeArray<float> Density;
            public NativeArray<float> SmoothDensity;
            public NativeArray<sbyte> QuantizedDensity;
            public NativeArray<int> FaultFlags;
            public NativeArray<int> CellVertexCounts;
            public NativeArray<int> CellVertexOffsets;
            public NativeArray<MCRawVertex> RawVertices;
            public NativeArray<float3> WeldedPositions;
            public NativeArray<int> TriangleIndices;
            public NativeArray<int> WeldedCounter;
            public NativeArray<int> EdgeVertexX;
            public NativeArray<int> EdgeVertexY;
            public NativeArray<int> EdgeVertexZ;
            public NativeArray<float3> Normals;
            public NativeArray<float> Curvature;
            public NativeArray<float> AmbientOcclusion;
            public NativeArray<Color32> Colors;
            public NativeArray<int> EdgeTable;
            public NativeArray<int> TriTable;
            public NativeArray<float> SkirtAlpha;
            public NativeArray<float> DirtyBlend;
            public NativeArray<float> Biome;
            public NativeArray<CaveSpawnData> SpawnPoints;
            public NativeArray<int> SpawnCounter;

            public void Dispose()
            {
                if (Density.IsCreated) Density.Dispose();
                if (SmoothDensity.IsCreated) SmoothDensity.Dispose();
                if (QuantizedDensity.IsCreated) QuantizedDensity.Dispose();
                if (FaultFlags.IsCreated) FaultFlags.Dispose();
                if (CellVertexCounts.IsCreated) CellVertexCounts.Dispose();
                if (CellVertexOffsets.IsCreated) CellVertexOffsets.Dispose();
                if (RawVertices.IsCreated) RawVertices.Dispose();
                if (WeldedPositions.IsCreated) WeldedPositions.Dispose();
                if (TriangleIndices.IsCreated) TriangleIndices.Dispose();
                if (WeldedCounter.IsCreated) WeldedCounter.Dispose();
                if (EdgeVertexX.IsCreated) EdgeVertexX.Dispose();
                if (EdgeVertexY.IsCreated) EdgeVertexY.Dispose();
                if (EdgeVertexZ.IsCreated) EdgeVertexZ.Dispose();
                if (Normals.IsCreated) Normals.Dispose();
                if (Curvature.IsCreated) Curvature.Dispose();
                if (AmbientOcclusion.IsCreated) AmbientOcclusion.Dispose();
                if (Colors.IsCreated) Colors.Dispose();
                if (EdgeTable.IsCreated) EdgeTable.Dispose();
                if (TriTable.IsCreated) TriTable.Dispose();
                if (SkirtAlpha.IsCreated) SkirtAlpha.Dispose();
                if (DirtyBlend.IsCreated) DirtyBlend.Dispose();
                if (Biome.IsCreated) Biome.Dispose();
                if (SpawnPoints.IsCreated) SpawnPoints.Dispose();
                if (SpawnCounter.IsCreated) SpawnCounter.Dispose();
            }
        }
    }
}
#endif
