using System.Diagnostics;
using Hecton8.World.OfflineWreckageBaker;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.World.OfflineWreckageBaker.Editor
{
    public static class OfflineWreckageMockBenchmark
    {
        [MenuItem("Hecton8/Wreckage Forge/Run Mock Benchmark")]
        public static void RunMenu()
        {
            bool passed = RunBenchmark();
            UnityEngine.Debug.Log("Offline wreckage mock benchmark passed: " + passed);
        }

        public static bool RunBenchmark()
        {
            int3 resolution = new int3(48, 48, 6);
            int vertexCount = resolution.x * resolution.y * resolution.z;
            int xyQuadCount = (resolution.x - 1) * (resolution.y - 1);
            int xzQuadCount = (resolution.x - 1) * (resolution.z - 1);
            int yzQuadCount = (resolution.y - 1) * (resolution.z - 1);
            int quadCount = 2 * (xyQuadCount + xzQuadCount + yzQuadCount);
            int indexCount = quadCount * 6;
            int vertexCapacity = vertexCount + indexCount + 2048;
            int indexCapacity = indexCount + 4096;

            NativeArray<OfflineWreckageBakeVertexDTO> baseVertices = default;
            NativeArray<int> baseIndices = default;
            NativeArray<OfflineWreckageBakeVertexDTO> workingVertices = default;
            NativeArray<OfflineWreckageBakeVertexDTO> stateVertices = default;
            NativeArray<int> stateIndices = default;
            NativeArray<float> tearWeights = default;
            NativeArray<OfflineWreckageBakeCounters64> counters = default;
            NativeArray<float3> hullPoints = default;
            try
            {
                baseVertices = new NativeArray<OfflineWreckageBakeVertexDTO>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                baseIndices = new NativeArray<int>(indexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                workingVertices = new NativeArray<OfflineWreckageBakeVertexDTO>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                stateVertices = new NativeArray<OfflineWreckageBakeVertexDTO>(vertexCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                stateIndices = new NativeArray<int>(indexCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                tearWeights = new NativeArray<float>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                counters = new NativeArray<OfflineWreckageBakeCounters64>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                hullPoints = new NativeArray<float3>(OfflineWreckageBakeConstants.MaxCollisionHullVertices, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                Stopwatch stopwatch = Stopwatch.StartNew();
                GenerateMockStructuralDeformationJob mockVertices = new GenerateMockStructuralDeformationJob
                {
                    Vertices = baseVertices,
                    Resolution = resolution,
                    CellSize = 0.25f,
                    GlobalQualityWeight = 0.82f,
                    ShearTorsion = 2.35f,
                    BlastRadius = 4.5f,
                    BlastEpicenter = new float3(0.15f, 0.25f, -0.35f)
                };
                GenerateMockGridSurfaceIndicesJob mockIndices = new GenerateMockGridSurfaceIndicesJob
                {
                    Output = baseIndices,
                    Resolution = resolution
                };
                JobHandle vertexHandle = mockVertices.Schedule(vertexCount, 128);
                JobHandle indexHandle = mockIndices.Schedule(quadCount, 128);
                JobHandle handle = JobHandle.CombineDependencies(vertexHandle, indexHandle);

                CopyBaseVerticesJob copy = new CopyBaseVerticesJob
                {
                    Source = baseVertices,
                    Destination = workingVertices
                };
                handle = copy.Schedule(vertexCount, 128, handle);

                ApplyStructuralShearJob shear = new ApplyStructuralShearJob
                {
                    Vertices = workingVertices,
                    ShearAxis = new float3(0.22f, 1f, 0.17f),
                    ShearTorsion = 2.35f,
                    CollapseCompression = 0.42f,
                    GlobalQualityWeight = 0.82f
                };
                handle = shear.Schedule(vertexCount, 128, handle);

                ApplyRadialBlastJob blast = new ApplyRadialBlastJob
                {
                    Vertices = workingVertices,
                    TearWeights = tearWeights,
                    EpicenterLocal = new float3(0.15f, 0.25f, -0.35f),
                    Radius = 4.5f,
                    TearThreshold = 0.34f,
                    DamageScale = 0.92f,
                    GlobalQualityWeight = 0.82f
                };
                handle = blast.Schedule(vertexCount, 128, handle);

                BuildTornTrianglesJob torn = new BuildTornTrianglesJob
                {
                    SourceVertices = workingVertices,
                    SourceIndices = baseIndices,
                    TearWeights = tearWeights,
                    OutputVertices = stateVertices,
                    OutputIndices = stateIndices,
                    Counters = counters,
                    TearThreshold = 0.26f,
                    SplitDistance = 0.16f,
                    GlobalQualityWeight = 0.82f,
                    EpicenterLocal = new float3(0.15f, 0.25f, -0.35f),
                    DamageScale = 0.92f
                };
                handle = torn.Schedule(handle);

                RecalculateDeformedNormalsJob normals = new RecalculateDeformedNormalsJob
                {
                    Vertices = stateVertices,
                    Indices = stateIndices,
                    Counters = counters
                };
                handle = normals.Schedule(handle);

                BakeDamageColorsJob colors = new BakeDamageColorsJob
                {
                    Vertices = stateVertices,
                    Counters = counters,
                    EpicenterLocal = new float3(0.15f, 0.25f, -0.35f),
                    BlastRadius = 4.5f,
                    ScorchIntensity = 1.55f,
                    GlobalQualityWeight = 0.82f
                };
                handle = colors.Schedule(vertexCapacity, 128, handle);

                GenerateConvexHullsJob hull = new GenerateConvexHullsJob
                {
                    Vertices = stateVertices,
                    Counters = counters,
                    HullPoints = hullPoints
                };
                handle = hull.Schedule(handle);
                handle.Complete();
                stopwatch.Stop();

                OfflineWreckageBakeCounters64 result = counters[0];
                return result.ActiveVertexCount > vertexCount &&
                       result.ActiveVertexCount <= vertexCapacity &&
                       result.ActiveIndexCount > 0 &&
                       (result.ActiveIndexCount % 3) == 0 &&
                       result.ActiveIndexCount <= indexCapacity &&
                       result.HullVertexCount == OfflineWreckageBakeConstants.SupportHullPointCount &&
                       stopwatch.Elapsed.TotalMilliseconds >= 0.0;
            }
            finally
            {
                if (hullPoints.IsCreated)
                    hullPoints.Dispose();
                if (counters.IsCreated)
                    counters.Dispose();
                if (tearWeights.IsCreated)
                    tearWeights.Dispose();
                if (stateIndices.IsCreated)
                    stateIndices.Dispose();
                if (stateVertices.IsCreated)
                    stateVertices.Dispose();
                if (workingVertices.IsCreated)
                    workingVertices.Dispose();
                if (baseIndices.IsCreated)
                    baseIndices.Dispose();
                if (baseVertices.IsCreated)
                    baseVertices.Dispose();
            }
        }
    }
}
