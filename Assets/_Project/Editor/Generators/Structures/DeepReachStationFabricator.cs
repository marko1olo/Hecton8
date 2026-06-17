#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using Hecton8.Editor.ColliderOptimization1716;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Editor.Structures
{
    public struct StationFabricationSettings
    {
        public string ModulePrefabFolder;
        public string OutputFolder;
        public string StationName;
        public uint Seed;
        public int3 GridDims;
        public int MaxPlacements;
        public float CellSize;
        public float GlobalQualityWeight;
        public float WeldEpsilon;

        public static StationFabricationSettings Default
        {
            get
            {
                return new StationFabricationSettings
                {
                    ModulePrefabFolder = DeepReachStationModuleLibraryBuilder.DefaultPrefabFolder,
                    OutputFolder = "Assets/_Project/Art/Baked/Structures",
                    StationName = "Station_AbyssHub",
                    Seed = 8421u,
                    GridDims = new int3(9, 3, 13),
                    MaxPlacements = 100,
                    CellSize = 7.5f,
                    GlobalQualityWeight = 0.72f,
                    WeldEpsilon = 0.0015f
                };
            }
        }
    }

    public struct StationFabricationResult
    {
        public bool Success;
        public string PrefabPath;
        public string MeshPath;
        public StationBakeCountersDTO Counters;
        public int SourceTriangleEstimate;
        public int FinalTriangleCount;
        public int FinalVertexCount;
        public string FailureReason;
    }

    public static class DeepReachStationFabricator
    {
        private const int MaxEditorSourceVertexCapacity = 2_000_000;
        private const int MaxEditorSourceIndexCapacity = 6_000_000;

        private static readonly VertexAttributeDescriptor[] s_vertexLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.UInt32, 1)
        };

        [MenuItem("Hecton8/Structures/Fabricate Deep Reach Station Seed 8421")]
        public static void FabricateMenu()
        {
            StationFabricationSettings settings = StationFabricationSettings.Default;
            if (Fabricate(settings, out StationFabricationResult result))
                UnityEngine.Debug.Log($"Deep Reach station baked: {result.PrefabPath} ({result.FinalVertexCount} verts, {result.FinalTriangleCount} tris).");
            else
                UnityEngine.Debug.LogError($"Deep Reach station bake failed: {result.FailureReason}");
        }

        public static bool Fabricate(StationFabricationSettings settings, out StationFabricationResult result)
        {
            result = default;
            settings = Sanitize(settings);
            Stopwatch totalWatch = Stopwatch.StartNew();
            StationModuleLibrary library = null;
            NativeArray<StationWfcCellDTO> grid = default;
            NativeArray<StationPlacementDTO> placements = default;
            NativeArray<StationBakeCountersDTO> counters = default;
            NativeArray<StationMeshVertexDTO> transformedVertices = default;
            NativeArray<int> rawIndices = default;
            NativeArray<ushort> rawTriangleMaterials = default;
            NativeArray<StationMeshVertexDTO> weldedVertices = default;
            NativeArray<int> weldedIndices = default;
            NativeArray<ushort> weldedTriangleMaterials = default;
            NativeArray<int> remap = default;
            NativeArray<StationWeldBucketDTO> buckets = default;

            try
            {
                EnsureAssetFolder(settings.OutputFolder);
                library = DeepReachStationModuleLibraryBuilder.BuildFromConstructionPrefabs(
                    settings.ModulePrefabFolder,
                    Allocator.TempJob);
                grid = new NativeArray<StationWfcCellDTO>(ResolveCellCount(settings.GridDims), Allocator.TempJob, NativeArrayOptions.ClearMemory);
                placements = new NativeArray<StationPlacementDTO>(settings.MaxPlacements, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                counters = new NativeArray<StationBakeCountersDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                Stopwatch watch = Stopwatch.StartNew();
                new StationWfcSolverJob
                {
                    Grid = grid,
                    Rules = library.Rules,
                    Placements = placements,
                    Counters = counters,
                    GridDims = settings.GridDims,
                    Seed = settings.Seed,
                    MaxPlacements = settings.MaxPlacements,
                    CellSize = settings.CellSize,
                    GlobalQualityWeight = settings.GlobalQualityWeight
                }.Run();
                watch.Stop();
                StationBakeCountersDTO counterValue = counters[0];
                counterValue.WfcMilliseconds = (float)watch.Elapsed.TotalMilliseconds;
                counters[0] = counterValue;

                FailClosedIfRequired(counters[0], "WFC");

                if (!HasUsablePlacement(counters[0]))
                    throw new InvalidOperationException("WFC produced zero station placements.");

                ResolveMeshCapacity(library, placements, (int)counters[0].PlacementCount, out int sourceVertexCapacity, out int sourceIndexCapacity);
                int sourceTriangleCapacity = math.max(sourceIndexCapacity / 3, 1);
                transformedVertices = new NativeArray<StationMeshVertexDTO>(sourceVertexCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                rawIndices = new NativeArray<int>(sourceIndexCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                rawTriangleMaterials = new NativeArray<ushort>(sourceTriangleCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weldedVertices = new NativeArray<StationMeshVertexDTO>(sourceVertexCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weldedIndices = new NativeArray<int>(sourceIndexCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weldedTriangleMaterials = new NativeArray<ushort>(sourceTriangleCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                remap = new NativeArray<int>(sourceVertexCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                buckets = new NativeArray<StationWeldBucketDTO>(ResolveBucketCapacity(sourceVertexCapacity), Allocator.TempJob, NativeArrayOptions.ClearMemory);

                watch.Restart();
                new StationMeshFusionJob
                {
                    Placements = placements,
                    MeshSlices = library.MeshSlices,
                    SourceVertices = library.Vertices,
                    SourceTriangles = library.Triangles,
                    TransformedVertices = transformedVertices,
                    RawIndices = rawIndices,
                    RawTriangleMaterials = rawTriangleMaterials,
                    Counters = counters
                }.Run();
                watch.Stop();
                counterValue = counters[0];
                counterValue.FusionMilliseconds = (float)watch.Elapsed.TotalMilliseconds;
                counters[0] = counterValue;
                FailClosedIfRequired(counters[0], "mesh fusion");

                watch.Restart();
                new StationVertexWeldingJob
                {
                    SourceVertices = transformedVertices,
                    SourceIndices = rawIndices,
                    SourceTriangleMaterials = rawTriangleMaterials,
                    WeldedVertices = weldedVertices,
                    WeldedIndices = weldedIndices,
                    WeldedTriangleMaterials = weldedTriangleMaterials,
                    SourceToWeldedRemap = remap,
                    Buckets = buckets,
                    Counters = counters,
                    WeldEpsilon = settings.WeldEpsilon
                }.Run();
                watch.Stop();
                counterValue = counters[0];
                counterValue.WeldMilliseconds = (float)watch.Elapsed.TotalMilliseconds;
                counters[0] = counterValue;
                FailClosedIfRequired(counters[0], "vertex welding");

                watch.Restart();
                new StationProceduralDamageJob
                {
                    Vertices = weldedVertices,
                    Counters = counters,
                    Seed = settings.Seed ^ 0xDADABEEFu,
                    GlobalQualityWeight = settings.GlobalQualityWeight,
                    StationHalfExtents = ResolveStationHalfExtents(placements, (int)counters[0].PlacementCount, settings.CellSize)
                }.Run();
                watch.Stop();
                counterValue = counters[0];
                counterValue.DamageMilliseconds = (float)watch.Elapsed.TotalMilliseconds;
                counters[0] = counterValue;
                FailClosedIfRequired(counters[0], "damage bake");

                Material[] materials = ResolveStationMaterials(library, settings.OutputFolder);
                Mesh mesh = CreateMeshAsset(settings, weldedVertices, weldedIndices, weldedTriangleMaterials, counters[0], materials, out Material[] activeMaterials, out string meshPath);
                string prefabPath = CreatePrefabAsset(settings, mesh, activeMaterials);
                AssetDatabase.SaveAssets();

                totalWatch.Stop();
                result.Success = true;
                result.PrefabPath = prefabPath;
                result.MeshPath = meshPath;
                result.Counters = counters[0];
                result.SourceTriangleEstimate = (int)(counters[0].SourceIndexCount / 3u + counters[0].CulledTriangleCount);
                result.FinalTriangleCount = (int)(counters[0].WeldedIndexCount / 3u);
                result.FinalVertexCount = (int)counters[0].WeldedVertexCount;

                return true;
            }
            catch (Exception ex)
            {
                totalWatch.Stop();
                result.Success = false;
                result.FailureReason = ex.Message;
                return false;
            }
            finally
            {
                if (buckets.IsCreated)
                    buckets.Dispose();
                if (remap.IsCreated)
                    remap.Dispose();
                if (weldedIndices.IsCreated)
                    weldedIndices.Dispose();
                if (weldedTriangleMaterials.IsCreated)
                    weldedTriangleMaterials.Dispose();
                if (weldedVertices.IsCreated)
                    weldedVertices.Dispose();
                if (rawTriangleMaterials.IsCreated)
                    rawTriangleMaterials.Dispose();
                if (rawIndices.IsCreated)
                    rawIndices.Dispose();
                if (transformedVertices.IsCreated)
                    transformedVertices.Dispose();
                if (counters.IsCreated)
                    counters.Dispose();
                if (placements.IsCreated)
                    placements.Dispose();
                if (grid.IsCreated)
                    grid.Dispose();
                library?.Dispose();
            }
        }

        private static StationFabricationSettings Sanitize(StationFabricationSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.ModulePrefabFolder))
                settings.ModulePrefabFolder = DeepReachStationModuleLibraryBuilder.DefaultPrefabFolder;
            settings.ModulePrefabFolder = SanitizeAssetFolder(settings.ModulePrefabFolder, DeepReachStationModuleLibraryBuilder.DefaultPrefabFolder);
            settings.OutputFolder = SanitizeAssetFolder(settings.OutputFolder, "Assets/_Project/Art/Baked/Structures");
            settings.StationName = SanitizeAssetName(settings.StationName, "Station_AbyssHub");

            settings.Seed = settings.Seed == 0u ? 1u : settings.Seed;
            settings.GridDims = new int3(
                math.clamp(settings.GridDims.x, 3, 64),
                math.clamp(settings.GridDims.y, 1, 16),
                math.clamp(settings.GridDims.z, 3, 64));
            settings.MaxPlacements = math.clamp(settings.MaxPlacements <= 0 ? 100 : settings.MaxPlacements, 1, 512);
            if (!math.isfinite(settings.CellSize))
                throw new InvalidOperationException("Station cell size is non-finite.");
            if (!math.isfinite(settings.GlobalQualityWeight))
                throw new InvalidOperationException("Station quality weight is non-finite.");
            if (!math.isfinite(settings.WeldEpsilon))
                throw new InvalidOperationException("Station weld epsilon is non-finite.");

            settings.CellSize = math.clamp(settings.CellSize <= 0f ? 7.5f : settings.CellSize, 1f, 32f);
            settings.GlobalQualityWeight = math.saturate(settings.GlobalQualityWeight);
            settings.WeldEpsilon = math.clamp(settings.WeldEpsilon <= 0f ? 0.0015f : settings.WeldEpsilon, 0.0001f, 0.05f);
            return settings;
        }

        private static string SanitizeAssetFolder(string folder, string fallback)
        {
            string value = string.IsNullOrWhiteSpace(folder) ? fallback : folder.Trim().Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrWhiteSpace(value))
                value = fallback;

            if (!value.Equals("Assets", StringComparison.Ordinal) && !value.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidOperationException($"Station asset folder must be under Assets/: {value}");

            string[] segments = value.Split('/');
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];
                if (segment.Length == 0 || segment == "." || segment == "..")
                    throw new InvalidOperationException($"Station asset folder contains invalid segment: {value}");

                for (int c = 0; c < segment.Length; c++)
                {
                    if (Array.IndexOf(invalid, segment[c]) >= 0)
                        throw new InvalidOperationException($"Station asset folder contains invalid character: {value}");
                }
            }

            return value;
        }

        private static string SanitizeAssetName(string value, string fallback)
        {
            string input = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            char[] buffer = input.ToCharArray();
            for (int i = 0; i < buffer.Length; i++)
            {
                char c = buffer[i];
                if (c == '/' || c == '\\' || c == ':' || Array.IndexOf(invalid, c) >= 0)
                    buffer[i] = '_';
            }

            string result = new string(buffer).Trim('_', ' ');
            return string.IsNullOrWhiteSpace(result) ? fallback : result;
        }

        private static int ResolveCellCount(int3 dims)
        {
            int3 safe = new int3(math.max(dims.x, 1), math.max(dims.y, 1), math.max(dims.z, 1));
            return safe.x * safe.y * safe.z;
        }

        private static bool HasUsablePlacement(StationBakeCountersDTO counters)
        {
            return counters.PlacementCount > 0u && (counters.FaultFlags & DeepReachStationConstants.FaultNoRules) == 0u;
        }

        private static void FailClosedIfRequired(StationBakeCountersDTO counters, string stage)
        {
            uint fatal = DeepReachStationConstants.FaultNoRules |
                         DeepReachStationConstants.FaultContradiction |
                         DeepReachStationConstants.FaultCapacity |
                         DeepReachStationConstants.FaultNonFinite |
                         DeepReachStationConstants.FaultInvalidTopology;
            if ((counters.FaultFlags & fatal) != 0u)
                throw new InvalidOperationException($"Station {stage} produced fatal fault mask 0x{counters.FaultFlags:X8}.");
        }

        private static void ResolveMeshCapacity(
            StationModuleLibrary library,
            NativeArray<StationPlacementDTO> placements,
            int placementCount,
            out int vertexCapacity,
            out int indexCapacity)
        {
            long vertices = 0;
            long indices = 0;
            int count = math.min(placementCount, placements.Length);
            for (int i = 0; i < count; i++)
            {
                StationPlacementDTO placement = placements[i];
                int moduleId = placement.ModuleId;
                if ((uint)moduleId >= (uint)library.MeshSlices.Length)
                    continue;

                StationMeshSliceDTO slice = library.MeshSlices[moduleId];
                vertices += slice.VertexCount;
                indices += slice.TriangleCount * 3L;
            }

            if (vertices > MaxEditorSourceVertexCapacity)
                throw new InvalidOperationException($"Station source vertex budget exceeded: {vertices}/{MaxEditorSourceVertexCapacity}.");
            if (indices > MaxEditorSourceIndexCapacity)
                throw new InvalidOperationException($"Station source index budget exceeded: {indices}/{MaxEditorSourceIndexCapacity}.");

            vertexCapacity = (int)Math.Max(1L, Math.Min(vertices, int.MaxValue / 4L));
            indexCapacity = (int)Math.Max(3L, Math.Min(indices, int.MaxValue / 4L));
        }

        private static int ResolveBucketCapacity(int sourceVertexCapacity)
        {
            int capacity = 1;
            int target = math.max(16, sourceVertexCapacity * 2);
            while (capacity < target && capacity < (1 << 28))
                capacity <<= 1;
            return capacity;
        }

        private static float3 ResolveStationHalfExtents(NativeArray<StationPlacementDTO> placements, int placementCount, float cellSize)
        {
            if (placementCount <= 0)
                return new float3(cellSize);

            bool has = false;
            float3 min = default;
            float3 max = default;
            int count = math.min(placementCount, placements.Length);
            for (int i = 0; i < count; i++)
            {
                float3 p = placements[i].LocalToStation.c3.xyz;
                if (!has)
                {
                    min = p;
                    max = p;
                    has = true;
                }
                else
                {
                    min = math.min(min, p);
                    max = math.max(max, p);
                }
            }

            return math.max((max - min) * 0.5f + cellSize, new float3(1f));
        }

        private static Mesh CreateMeshAsset(
            StationFabricationSettings settings,
            NativeArray<StationMeshVertexDTO> vertices,
            NativeArray<int> indices,
            NativeArray<ushort> triangleMaterials,
            StationBakeCountersDTO counters,
            Material[] materials,
            out Material[] activeMaterials,
            out string meshPath)
        {
            activeMaterials = Array.Empty<Material>();
            int vertexCount = (int)counters.WeldedVertexCount;
            int indexCount = (int)counters.WeldedIndexCount;
            if (vertexCount <= 0 || indexCount < 3)
                throw new InvalidOperationException("Station mesh bake produced empty geometry.");
            if (vertexCount > vertices.Length)
                throw new InvalidOperationException($"Station mesh vertex counter exceeds buffer length: {vertexCount}/{vertices.Length}.");
            if (indexCount > indices.Length)
                throw new InvalidOperationException($"Station mesh index counter exceeds buffer length: {indexCount}/{indices.Length}.");
            if (indexCount % 3 != 0)
                throw new InvalidOperationException($"Station mesh index counter is not triangle aligned: {indexCount}.");
            ValidateIndexBuffer(indices, indexCount, vertexCount);
            int materialCount = ResolveMaterialCount(materials);

            NativeArray<StationRenderVertexDTO> renderVertices = default;
            NativeArray<int> sortedIndices = default;
            NativeArray<int> materialIndexCounts = default;
            NativeArray<int> materialRemap = default;
            NativeArray<int> activeSourceSlots = default;
            NativeArray<int> subMeshStarts = default;
            NativeArray<int> subMeshCounts = default;
            NativeArray<int> subMeshWriteOffsets = default;
            try
            {
                renderVertices = new NativeArray<StationRenderVertexDTO>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                sortedIndices = new NativeArray<int>(indexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                materialIndexCounts = new NativeArray<int>(materialCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                materialRemap = new NativeArray<int>(materialCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                activeSourceSlots = new NativeArray<int>(materialCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                subMeshStarts = new NativeArray<int>(materialCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                subMeshCounts = new NativeArray<int>(materialCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                subMeshWriteOffsets = new NativeArray<int>(materialCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                Bounds bounds = PackRenderVertices(vertices, renderVertices, vertexCount);
                int activeSubMeshCount = BuildMaterialSortedIndexBuffer(
                    indices,
                    triangleMaterials,
                    indexCount,
                    materialCount,
                    sortedIndices,
                    materialIndexCounts,
                    materialRemap,
                    activeSourceSlots,
                    subMeshStarts,
                    subMeshCounts,
                    subMeshWriteOffsets);
                activeMaterials = ResolveActiveMaterials(materials, activeSourceSlots, activeSubMeshCount);
                Mesh mesh = new Mesh
                {
                    name = $"{settings.StationName}_Seed{settings.Seed}_Baked",
                    indexFormat = IndexFormat.UInt32
                };

                const MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers;
                mesh.SetVertexBufferParams(vertexCount, s_vertexLayout);
                mesh.SetIndexBufferParams(indexCount, mesh.indexFormat);
                mesh.SetVertexBufferData(renderVertices, 0, 0, vertexCount, 0, flags);
                mesh.SetIndexBufferData(sortedIndices, 0, 0, indexCount, flags);
                mesh.subMeshCount = activeSubMeshCount;
                for (int sub = 0; sub < activeSubMeshCount; sub++)
                {
                    mesh.SetSubMesh(sub, new SubMeshDescriptor(subMeshStarts[sub], subMeshCounts[sub], MeshTopology.Triangles)
                    {
                        bounds = bounds,
                        vertexCount = vertexCount
                    }, flags);
                }

                mesh.bounds = bounds;
                mesh.OptimizeIndexBuffers();
                mesh.UploadMeshData(true);

                meshPath = $"{settings.OutputFolder}/{mesh.name}.asset";
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existing != null)
                {
                    EditorUtility.CopySerialized(mesh, existing);
                    UnityEngine.Object.DestroyImmediate(mesh);
                    return existing;
                }

                AssetDatabase.CreateAsset(mesh, meshPath);
                return mesh;
            }
            finally
            {
                if (subMeshWriteOffsets.IsCreated)
                    subMeshWriteOffsets.Dispose();
                if (subMeshCounts.IsCreated)
                    subMeshCounts.Dispose();
                if (subMeshStarts.IsCreated)
                    subMeshStarts.Dispose();
                if (activeSourceSlots.IsCreated)
                    activeSourceSlots.Dispose();
                if (materialRemap.IsCreated)
                    materialRemap.Dispose();
                if (materialIndexCounts.IsCreated)
                    materialIndexCounts.Dispose();
                if (sortedIndices.IsCreated)
                    sortedIndices.Dispose();
                if (renderVertices.IsCreated)
                    renderVertices.Dispose();
            }
        }

        private static Bounds PackRenderVertices(
            NativeArray<StationMeshVertexDTO> source,
            NativeArray<StationRenderVertexDTO> destination,
            int vertexCount)
        {
            bool hasBounds = false;
            float3 min = default;
            float3 max = default;
            int count = math.min(vertexCount, math.min(source.Length, destination.Length));
            for (int i = 0; i < count; i++)
            {
                StationMeshVertexDTO vertex = source[i];
                if (!DeepReachStationMath.IsFinite(vertex.Position) ||
                    !DeepReachStationMath.IsFinite(vertex.Normal) ||
                    !DeepReachStationMath.IsFinite(vertex.Uv0))
                    throw new InvalidOperationException($"Station render vertex is non-finite at index {i}.");

                StationRenderVertexDTO packed = default;
                packed.Position = vertex.Position;
                packed.Normal = math.normalizesafe(vertex.Normal, new float3(0f, 1f, 0f));
                packed.Uv0 = vertex.Uv0;
                packed.ColorRgba = vertex.ColorRgba;
                packed.Flags = vertex.Flags;
                destination[i] = packed;

                if (!hasBounds)
                {
                    min = vertex.Position;
                    max = vertex.Position;
                    hasBounds = true;
                }
                else
                {
                    min = math.min(min, vertex.Position);
                    max = math.max(max, vertex.Position);
                }
            }

            if (!hasBounds)
                return new Bounds(Vector3.zero, Vector3.one);

            float3 center = (min + max) * 0.5f;
            float3 size = math.max(max - min, new float3(0.01f));
            return new Bounds(new Vector3(center.x, center.y, center.z), new Vector3(size.x, size.y, size.z));
        }

        private static void ValidateIndexBuffer(NativeArray<int> indices, int indexCount, int vertexCount)
        {
            for (int i = 0; i < indexCount; i++)
            {
                int index = indices[i];
                if ((uint)index >= (uint)vertexCount)
                    throw new InvalidOperationException($"Station mesh index {i} escapes vertex buffer: {index}/{vertexCount}.");
            }
        }

        private static int BuildMaterialSortedIndexBuffer(
            NativeArray<int> indices,
            NativeArray<ushort> triangleMaterials,
            int indexCount,
            int materialCount,
            NativeArray<int> sortedIndices,
            NativeArray<int> materialIndexCounts,
            NativeArray<int> materialRemap,
            NativeArray<int> activeSourceSlots,
            NativeArray<int> subMeshStarts,
            NativeArray<int> subMeshCounts,
            NativeArray<int> subMeshWriteOffsets)
        {
            for (int i = 0; i < materialCount; i++)
            {
                materialIndexCounts[i] = 0;
                materialRemap[i] = -1;
                activeSourceSlots[i] = 0;
            }

            int triangleCount = indexCount / 3;
            for (int triangle = 0; triangle < triangleCount; triangle++)
                materialIndexCounts[ResolveTriangleMaterialSlot(triangleMaterials, triangle, materialCount)] += 3;

            int activeSubMeshCount = 0;
            int cursor = 0;
            for (int materialSlot = 0; materialSlot < materialCount; materialSlot++)
            {
                int count = materialIndexCounts[materialSlot];
                if (count <= 0)
                    continue;

                materialRemap[materialSlot] = activeSubMeshCount;
                activeSourceSlots[activeSubMeshCount] = materialSlot;
                subMeshStarts[activeSubMeshCount] = cursor;
                subMeshCounts[activeSubMeshCount] = count;
                subMeshWriteOffsets[activeSubMeshCount] = cursor;
                cursor += count;
                activeSubMeshCount++;
            }

            if (activeSubMeshCount == 0)
            {
                materialRemap[0] = 0;
                activeSourceSlots[0] = 0;
                activeSubMeshCount = 1;
            }

            for (int triangle = 0; triangle < triangleCount; triangle++)
            {
                int sourceIndex = triangle * 3;
                int materialSlot = ResolveTriangleMaterialSlot(triangleMaterials, triangle, materialCount);
                int compactSlot = materialRemap[materialSlot];
                int destinationIndex = subMeshWriteOffsets[compactSlot];
                sortedIndices[destinationIndex] = indices[sourceIndex];
                sortedIndices[destinationIndex + 1] = indices[sourceIndex + 1];
                sortedIndices[destinationIndex + 2] = indices[sourceIndex + 2];
                subMeshWriteOffsets[compactSlot] = destinationIndex + 3;
            }

            return activeSubMeshCount;
        }

        private static int ResolveTriangleMaterialSlot(NativeArray<ushort> triangleMaterials, int triangleIndex, int materialCount)
        {
            if (!triangleMaterials.IsCreated || (uint)triangleIndex >= (uint)triangleMaterials.Length)
                return 0;

            int slot = triangleMaterials[triangleIndex];
            return (uint)slot < (uint)materialCount ? slot : 0;
        }

        private static int ResolveMaterialCount(Material[] materials)
        {
            if (materials == null || materials.Length == 0)
                throw new InvalidOperationException("Station material list is empty.");

            return Math.Min(materials.Length, DeepReachStationConstants.MaxMaterialSlots);
        }

        private static Material[] ResolveActiveMaterials(Material[] materials, NativeArray<int> activeSourceSlots, int activeSubMeshCount)
        {
            Material[] active = new Material[activeSubMeshCount];
            int materialCount = ResolveMaterialCount(materials);
            for (int i = 0; i < activeSubMeshCount; i++)
            {
                int slot = Mathf.Clamp(activeSourceSlots[i], 0, materialCount - 1);
                active[i] = materials[slot];
            }

            return active;
        }

        private static string CreatePrefabAsset(StationFabricationSettings settings, Mesh mesh, Material[] materials)
        {
            string prefabPath = $"{settings.OutputFolder}/GEN_{settings.StationName}_Seed{settings.Seed}.prefab";
            GameObject root = new GameObject($"GEN_{settings.StationName}_Seed{settings.Seed}");
            try
            {
                MeshFilter filter = root.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                MeshRenderer renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                GameObjectUtility.SetStaticEditorFlags(root,
                    StaticEditorFlags.BatchingStatic |
                    StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic |
                    StaticEditorFlags.ContributeGI);

                if (!ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root, out string colliderFailure))
                    throw new InvalidOperationException("Collider topology rejected before station prefab save. path=" + prefabPath + " reason=" + colliderFailure);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool success);
                if (!success)
                    throw new InvalidOperationException($"PrefabUtility failed to save {prefabPath}.");

                if (!ColliderOptimizerEngine1716.ValidatePrefabAssetTopology(prefabPath, out colliderFailure))
                    throw new InvalidOperationException("Collider topology rejected after station prefab save. path=" + prefabPath + " reason=" + colliderFailure);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            return prefabPath;
        }

        private static Material[] ResolveStationMaterials(StationModuleLibrary library, string outputFolder)
        {
            if (library != null && library.Materials != null && library.Materials.Length > 0)
            {
                int materialCount = Math.Min(library.Materials.Length, DeepReachStationConstants.MaxMaterialSlots);
                Material[] materials = new Material[materialCount];
                Material fallback = null;
                for (int i = 0; i < materialCount; i++)
                {
                    Material material = library.Materials[i];
                    if (material == null)
                    {
                        if (fallback == null)
                            fallback = ResolveFallbackStationMaterial(outputFolder);
                        material = fallback;
                    }

                    materials[i] = material;
                }

                return materials;
            }

            if (library != null && library.PrimaryMaterial != null)
                return new[] { library.PrimaryMaterial };

            return new[] { ResolveFallbackStationMaterial(outputFolder) };
        }

        private const string AuthoredStationMaterialPath = "Assets/_Project/Art/Materials/Construction/Mat_Module_Foundation.mat";

        private static Material ResolveFallbackStationMaterial(string outputFolder)
        {
            string materialPath = $"{outputFolder}/MAT_Station_BakedGrime.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            bool ownsOutputMaterial = material != null;
            if (material == null)
            {
                material = AssetDatabase.LoadAssetAtPath<Material>(AuthoredStationMaterialPath);
                if (material == null)
                    throw new InvalidOperationException("Missing authored station material: " + AuthoredStationMaterialPath);
            }

            if (ownsOutputMaterial)
            {
                TrySetColor(material, "_BaseColor", new Color(0.44f, 0.48f, 0.46f, 1f));
                TrySetFloat(material, "_Smoothness", 0.22f);
                TrySetFloat(material, "_Metallic", 0.58f);
                material.enableInstancing = true;
            }

            return material;
        }

        private static void TrySetColor(Material material, string property, Color color)
        {
            if (material != null && material.HasProperty(property))
                material.SetColor(property, color);
        }

        private static void TrySetFloat(Material material, string property, float value)
        {
            if (material != null && material.HasProperty(property))
                material.SetFloat(property, value);
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
                return;

            string normalized = assetFolder.Replace('\\', '/').Trim('/');
            if (normalized.Equals("Assets", StringComparison.Ordinal))
                return;
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidOperationException($"Station asset folder must be under Assets/: {assetFolder}");

            string[] segments = normalized.Split('/');
            string current = "Assets";
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    string guid = AssetDatabase.CreateFolder(current, segments[i]);
                    if (string.IsNullOrEmpty(guid) && !AssetDatabase.IsValidFolder(next))
                        throw new InvalidOperationException($"Unable to create station asset folder: {next}");
                }

                current = next;
            }
        }

    }
}
#endif
