#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Editor.ColliderOptimization1716;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace Hecton8.EditorTools.Generators.Flora
{
    internal enum FloraTopologyPreset : byte
    {
        KelpForestFrond = 0,
        AbyssalBrainCoral = 1,
        ThermalTubeWorm = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FloraGenomeDTO
    {
        public uint Seed;
        public uint PresetHash;
        public int PresetKind;
        public int RecursionDepth;
        public int MaxNodeCount;
        public int MaxEdgeCount;
        public int RadialSegments;
        public int PathSegments;
        public float BranchAngleRadians;
        public float SegmentLength;
        public float LengthFalloff;
        public float BaseRadius;
        public float RadiusFalloff;
        public float TwistRadians;
        public float TwistProbability;
        public float GlowWeight;
        public float GlobalQualityWeight;
        public int Lod0TriangleBudget;
        public uint Flags;
        public uint _pad0;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FloraNode
    {
        public float3 Position;
        public float3 Direction;
        public float Radius;
        public float RootDistance;
        public uint PhaseHash;
        public int ParentIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FloraEdge
    {
        public int StartNode;
        public int EndNode;
        public int BranchDepth;
        public int Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FloraVertexData
    {
        public float3 Position;
        public float3 Normal;
        public float4 Tangent;
        public float2 UV;
        public float2 UVMask;
        public uint PackedColor;
        public uint Pad0;
    }

    /// <summary>
    /// Interleaved payload for vertex stream 2. Unity supports a maximum of four vertex streams
    /// (0..3), and inside one stream it lays attributes out in <see cref="VertexAttribute"/> enum
    /// order, so Tangent, TexCoord0 and TexCoord1 occupy offsets 0, 16 and 24 of a 32-byte stride.
    /// Streams 0, 1 and 3 stay single-attribute because <c>FloraTopologyStudio1711</c> validates
    /// Position, Normal and Color as dedicated streams with attribute offset 0.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct FloraInterleavedStream2Vertex
    {
        public float4 Tangent;
        public float2 UV0;
        public float2 UVMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FloraBoundsDTO
    {
        public float3 Min;
        public float3 Max;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FloraGenerationCounters
    {
        public int NodeCount;
        public int EdgeCount;
        public int VertexCount;
        public int IndexCount;
        public int Overflow;
        public int Truncated;
        public ulong VertexHash;
    }

    internal struct FloraTopologyMetrics
    {
        public int NodeCount;
        public int EdgeCount;
        public int Overflow;
        public int Truncated;
        public int Lod0Vertices;
        public int Lod0Triangles;
        public int Lod1Vertices;
        public int Lod1Triangles;
        public int Lod2Vertices;
        public int Lod2Triangles;
        public ulong Lod0Hash;
        public ulong Lod1Hash;
        public ulong Lod2Hash;
        public double GrowthMilliseconds;
        public double Lod0Milliseconds;
        public double Lod1Milliseconds;
        public double Lod2Milliseconds;
        public Bounds Lod0Bounds;
        public Bounds Lod1Bounds;
        public Bounds Lod2Bounds;
    }

    internal static class FloraTopologyGenerator1604
    {
        private const string MeshOutputRoot = "Assets/_Project/Art/Generated/Flora/Topology1604";
        private const string PrefabOutputRoot = "Assets/_Project/Prefabs/Nature/Flora/Topology1604";
        private const string KelpMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_tall.mat";
        private const string CoralMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat";
        private const string MassiveCoralMaterialPath = "Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_massive.mat";
        private const string KelpShaderName = "Hecton8/Flora/KelpMaster";
        private const string KelpGpuInstancerShaderName = "GPUInstancer/Hecton8/Flora/KelpMaster";
        private const string CoralShaderName = "Hecton8/Flora/CoralMaster";
        private const string CoralGpuInstancerShaderName = "GPUInstancer/Hecton8/Flora/CoralMaster";
        private const string GeneratedCompoundRootName = "COL_CompoundProxy_1716";
        private const float Lod0Threshold = 0.62f;
        private const float Lod1Threshold = 0.22f;
        private const float Lod2Threshold = 0.06f;

        [MenuItem("Hecton8/Authoring/Flora Topology 1604/Open Studio", priority = 184)]
        public static void OpenStudio()
        {
            FloraTopologyStudioWindow1604.Open();
        }

        [MenuItem("Hecton8/Authoring/Flora Topology 1604/Generate Seed Pack", priority = 185)]
        public static void GenerateSeedPack()
        {
            EnsureFolder(MeshOutputRoot);
            EnsureFolder(PrefabOutputRoot);

            bool generatedAll = true;
            generatedAll &= GenerateAndSave(FloraTopologyPreset.KelpForestFrond, 16040042u, 0.75f, ResolveDefaultTriangleBudget(FloraTopologyPreset.KelpForestFrond));
            generatedAll &= GenerateAndSave(FloraTopologyPreset.AbyssalBrainCoral, 16041042u, 0.82f, ResolveDefaultTriangleBudget(FloraTopologyPreset.AbyssalBrainCoral));
            generatedAll &= GenerateAndSave(FloraTopologyPreset.ThermalTubeWorm, 16042042u, 0.68f, ResolveDefaultTriangleBudget(FloraTopologyPreset.ThermalTubeWorm));

            if (!generatedAll)
            {
                Debug.LogError("[FloraTopology1604] Seed pack generation failed. See previous fail-closed errors; successful entries remain valid generated assets.");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FloraTopology1604] Seed pack generated successfully.");
        }

        [MenuItem("Hecton8/Authoring/Flora Topology 1604/Run Determinism Self Test", priority = 186)]
        public static void RunDeterminismSelfTest()
        {
            bool passed = true;
            passed &= RunPresetDeterminism(FloraTopologyPreset.KelpForestFrond, 42u);
            passed &= RunPresetDeterminism(FloraTopologyPreset.AbyssalBrainCoral, 43u);
            passed &= RunPresetDeterminism(FloraTopologyPreset.ThermalTubeWorm, 44u);

            if (passed)
                Debug.Log("[FloraTopology1604] Determinism self test passed. STATUS=PENDING UNITY IMPORT/PROFILER VERIFICATION.");
        }

        [MenuItem("Hecton8/Authoring/Flora Topology 1604/Run 100K Fuzzer", priority = 187)]
        public static void RunFuzzer()
        {
            FloraGenomeDTO genome = CreateGenome(FloraTopologyPreset.AbyssalBrainCoral, 16049999u, 1f, 100000);
            genome.RecursionDepth = 6;
            genome.MaxNodeCount = 6000;
            genome.MaxEdgeCount = 5999;
            genome.RadialSegments = 6;
            genome.PathSegments = 4;
            genome.Lod0TriangleBudget = 300000;

            if (!TryGenerateMeshes(genome, "GEN_FloraTopology1604_Fuzzer", out Mesh[] meshes, out FloraTopologyMetrics metrics))
            {
                Debug.LogError("[FloraTopology1604] 100K fuzzer failed to generate native mesh buffers.");
                return;
            }

            DestroyMeshes(meshes);

            if (metrics.Lod0Vertices < 100000)
            {
                Debug.LogError("[FloraTopology1604] 100K fuzzer under-produced vertices. VertexCount=" + metrics.Lod0Vertices);
                return;
            }

            if (metrics.Lod0Milliseconds > 100.0)
                Debug.LogWarning("[FloraTopology1604] 100K fuzzer exceeded 100ms static target. ms=" + metrics.Lod0Milliseconds.ToString("F3"));

            Debug.Log("[FloraTopology1604] 100K fuzzer completed. Vertices=" + metrics.Lod0Vertices + ", Tris=" + metrics.Lod0Triangles + ", ms=" + metrics.Lod0Milliseconds.ToString("F3"));
        }

        [MenuItem("Hecton8/Authoring/Flora Topology 1604/Run LOD Silhouette Audit", priority = 188)]
        public static void RunSilhouetteAudit()
        {
            bool passed = true;
            passed &= RunPresetSilhouetteAudit(FloraTopologyPreset.KelpForestFrond, 16040042u, 0.75f, ResolveDefaultTriangleBudget(FloraTopologyPreset.KelpForestFrond));
            passed &= RunPresetSilhouetteAudit(FloraTopologyPreset.AbyssalBrainCoral, 16041042u, 0.82f, ResolveDefaultTriangleBudget(FloraTopologyPreset.AbyssalBrainCoral));
            passed &= RunPresetSilhouetteAudit(FloraTopologyPreset.ThermalTubeWorm, 16042042u, 0.68f, ResolveDefaultTriangleBudget(FloraTopologyPreset.ThermalTubeWorm));

            if (passed)
                Debug.Log("[FloraTopology1604] LOD silhouette audit passed for all presets.");
        }

        public static FloraGenomeDTO CreateGenome(FloraTopologyPreset preset, uint seed, float globalQualityWeight, int lod0TriangleBudget)
        {
            float quality = math.saturate(math.select(1f, globalQualityWeight, math.isfinite(globalQualityWeight)));
            int clampedBudget = math.clamp(lod0TriangleBudget, 192, 80000);

            switch (preset)
            {
                case FloraTopologyPreset.KelpForestFrond:
                {
                    int radialSegments = math.clamp((int)math.round(math.lerp(3f, 7f, quality)), 3, 8);
                    int pathSegments = 9;
                    int maxEdges = ResolveEdgeCapacityForBudget(clampedBudget, radialSegments, pathSegments, true);
                    return new FloraGenomeDTO
                    {
                        Seed = SanitizeSeed(seed),
                        PresetHash = 0x6B656C70u,
                        PresetKind = (int)preset,
                        RecursionDepth = math.clamp((int)math.round(math.lerp(4f, 7f, quality)), 3, 8),
                        MaxNodeCount = math.clamp(maxEdges + 2, 16, 4096),
                        MaxEdgeCount = maxEdges,
                        RadialSegments = radialSegments,
                        PathSegments = pathSegments,
                        BranchAngleRadians = math.radians(math.lerp(10f, 26f, quality)),
                        SegmentLength = math.lerp(0.34f, 0.52f, quality),
                        LengthFalloff = math.lerp(0.76f, 0.86f, quality),
                        BaseRadius = math.lerp(0.035f, 0.075f, quality),
                        RadiusFalloff = math.lerp(0.72f, 0.82f, quality),
                        TwistRadians = math.radians(math.lerp(10f, 38f, quality)),
                        TwistProbability = math.lerp(0.18f, 0.42f, quality),
                        GlowWeight = math.lerp(0.2f, 0.55f, quality),
                        GlobalQualityWeight = quality,
                        Lod0TriangleBudget = clampedBudget,
                        Flags = 0u,
                        _pad0 = 0u
                    };
                }

                case FloraTopologyPreset.AbyssalBrainCoral:
                {
                    int radialSegments = math.clamp((int)math.round(math.lerp(5f, 10f, quality)), 5, 12);
                    int pathSegments = 6;
                    int maxEdges = ResolveEdgeCapacityForBudget(clampedBudget, radialSegments, pathSegments, false);
                    return new FloraGenomeDTO
                    {
                        Seed = SanitizeSeed(seed),
                        PresetHash = 0x434F524Cu,
                        PresetKind = (int)preset,
                        RecursionDepth = math.clamp((int)math.round(math.lerp(4f, 8f, quality)), 4, 9),
                        MaxNodeCount = math.clamp(maxEdges + 4, 32, 8192),
                        MaxEdgeCount = maxEdges,
                        RadialSegments = radialSegments,
                        PathSegments = pathSegments,
                        BranchAngleRadians = math.radians(math.lerp(34f, 58f, quality)),
                        SegmentLength = math.lerp(0.24f, 0.42f, quality),
                        LengthFalloff = math.lerp(0.67f, 0.78f, quality),
                        BaseRadius = math.lerp(0.12f, 0.22f, quality),
                        RadiusFalloff = math.lerp(0.70f, 0.84f, quality),
                        TwistRadians = math.radians(math.lerp(24f, 64f, quality)),
                        TwistProbability = math.lerp(0.55f, 0.86f, quality),
                        GlowWeight = math.lerp(0.08f, 0.32f, quality),
                        GlobalQualityWeight = quality,
                        Lod0TriangleBudget = clampedBudget,
                        Flags = 1u,
                        _pad0 = 0u
                    };
                }

                default:
                {
                    int radialSegments = math.clamp((int)math.round(math.lerp(6f, 12f, quality)), 6, 14);
                    int pathSegments = 7;
                    int maxEdges = ResolveEdgeCapacityForBudget(clampedBudget, radialSegments, pathSegments, false);
                    return new FloraGenomeDTO
                    {
                        Seed = SanitizeSeed(seed),
                        PresetHash = 0x54554245u,
                        PresetKind = (int)FloraTopologyPreset.ThermalTubeWorm,
                        RecursionDepth = math.clamp((int)math.round(math.lerp(3f, 6f, quality)), 3, 7),
                        MaxNodeCount = math.clamp(maxEdges + 8, 32, 4096),
                        MaxEdgeCount = maxEdges,
                        RadialSegments = radialSegments,
                        PathSegments = pathSegments,
                        BranchAngleRadians = math.radians(math.lerp(8f, 24f, quality)),
                        SegmentLength = math.lerp(0.26f, 0.46f, quality),
                        LengthFalloff = math.lerp(0.74f, 0.88f, quality),
                        BaseRadius = math.lerp(0.065f, 0.13f, quality),
                        RadiusFalloff = math.lerp(0.82f, 0.92f, quality),
                        TwistRadians = math.radians(math.lerp(6f, 24f, quality)),
                        TwistProbability = math.lerp(0.12f, 0.30f, quality),
                        GlowWeight = math.lerp(0.34f, 0.78f, quality),
                        GlobalQualityWeight = quality,
                        Lod0TriangleBudget = clampedBudget,
                        Flags = 2u,
                        _pad0 = 0u
                    };
                }
            }
        }

        internal static int ResolveDefaultTriangleBudget(FloraTopologyPreset preset)
        {
            if (preset == FloraTopologyPreset.AbyssalBrainCoral)
                return 48000;

            if (preset == FloraTopologyPreset.ThermalTubeWorm)
                return 22000;

            return 18000;
        }

        public static bool GenerateAndSave(FloraTopologyPreset preset, uint seed, float globalQualityWeight, int lod0TriangleBudget)
        {
            FloraGenomeDTO genome = CreateGenome(preset, seed, globalQualityWeight, lod0TriangleBudget);
            string safePreset = preset.ToString();
            string assetName = "GEN_FloraTopology1604_" + safePreset + "_" + seed.ToString("X8");

            if (!TryResolveMaterial(preset, out Material material))
                return false;

            if (!TryGenerateMeshes(genome, assetName, out Mesh[] meshes, out FloraTopologyMetrics metrics))
                return false;

            try
            {
                string meshFolder = MeshOutputRoot + "/" + safePreset;
                string prefabFolder = PrefabOutputRoot + "/" + safePreset;
                EnsureFolder(meshFolder);
                EnsureFolder(prefabFolder);

                string lod0Path = meshFolder + "/" + assetName + "_LOD0.asset";
                string lod1Path = meshFolder + "/" + assetName + "_LOD1.asset";
                string lod2Path = meshFolder + "/" + assetName + "_LOD2.asset";
                Mesh lod0 = CreateOrUpdateMeshAsset(lod0Path, meshes[0], out bool lod0Created);
                Mesh lod1 = CreateOrUpdateMeshAsset(lod1Path, meshes[1], out bool lod1Created);
                Mesh lod2 = CreateOrUpdateMeshAsset(lod2Path, meshes[2], out bool lod2Created);
                if (lod0 == null || lod1 == null || lod2 == null)
                {
                    DeleteCreatedMeshAsset(lod0Path, lod0Created);
                    DeleteCreatedMeshAsset(lod1Path, lod1Created);
                    DeleteCreatedMeshAsset(lod2Path, lod2Created);
                    Debug.LogError("[FloraTopology1604] Mesh asset persistence failed for " + assetName);
                    return false;
                }

                if (!SavePrefab(prefabFolder + "/" + assetName + ".prefab", assetName, preset, lod0, lod1, lod2, material))
                {
                    DeleteCreatedMeshAsset(lod0Path, lod0Created);
                    DeleteCreatedMeshAsset(lod1Path, lod1Created);
                    DeleteCreatedMeshAsset(lod2Path, lod2Created);
                    return false;
                }

                AssetDatabase.SaveAssets();

                Debug.Log(
                    "[FloraTopology1604] Generated " + assetName
                    + " Nodes=" + metrics.NodeCount
                    + " Edges=" + metrics.EdgeCount
                    + " Truncated=" + metrics.Truncated
                    + " LOD0=" + metrics.Lod0Vertices + "/" + metrics.Lod0Triangles
                    + " LOD1=" + metrics.Lod1Vertices + "/" + metrics.Lod1Triangles
                    + " LOD2=" + metrics.Lod2Vertices + "/" + metrics.Lod2Triangles
                    + " Hash=" + metrics.Lod0Hash.ToString("X16"));
                return true;
            }
            finally
            {
                DestroyMeshes(meshes);
            }
        }

        internal static bool TryGenerateMeshes(FloraGenomeDTO genome, string meshName, out Mesh[] meshes, out FloraTopologyMetrics metrics)
        {
            meshes = null;
            metrics = default;

            if (!ValidateGenome(genome))
                return false;

            if (!ValidateRuntimeLayouts())
                return false;

            NativeArray<FloraNode> nodes = default;
            NativeArray<FloraEdge> edges = default;
            NativeArray<FloraGenerationCounters> counters = default;

            try
            {
                nodes = new NativeArray<FloraNode>(genome.MaxNodeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                edges = new NativeArray<FloraEdge>(genome.MaxEdgeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                counters = new NativeArray<FloraGenerationCounters>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                Stopwatch stopwatch = Stopwatch.StartNew();
                LSystemGrowthJob growthJob = new LSystemGrowthJob
                {
                    Genome = genome,
                    Nodes = nodes,
                    Edges = edges,
                    Counters = counters
                };
                JobHandle growthHandle = growthJob.Schedule();
                growthHandle.Complete();
                stopwatch.Stop();

                FloraGenerationCounters growthCounters = counters[0];
                if (growthCounters.NodeCount < 2 || growthCounters.EdgeCount < 1 || growthCounters.Overflow != 0)
                {
                    Debug.LogError("[FloraTopology1604] Genome Complexity Exceeded Capacity. Nodes=" + growthCounters.NodeCount + ", Edges=" + growthCounters.EdgeCount + ", Overflow=" + growthCounters.Overflow);
                    return false;
                }

                metrics.NodeCount = growthCounters.NodeCount;
                metrics.EdgeCount = growthCounters.EdgeCount;
                metrics.Overflow = growthCounters.Overflow;
                metrics.Truncated = growthCounters.Truncated;
                metrics.GrowthMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

                meshes = new Mesh[3];
                if (!TryBuildLod(genome, meshName, 0, nodes, edges, growthCounters.EdgeCount, ref metrics, out meshes[0]))
                {
                    DestroyMeshes(meshes);
                    meshes = null;
                    return false;
                }

                if (!TryBuildLod(genome, meshName, 1, nodes, edges, growthCounters.EdgeCount, ref metrics, out meshes[1]))
                {
                    DestroyMeshes(meshes);
                    meshes = null;
                    return false;
                }

                if (!TryBuildLod(genome, meshName, 2, nodes, edges, growthCounters.EdgeCount, ref metrics, out meshes[2]))
                {
                    DestroyMeshes(meshes);
                    meshes = null;
                    return false;
                }

                if (!ValidateLodSilhouette(meshName, metrics))
                {
                    DestroyMeshes(meshes);
                    meshes = null;
                    return false;
                }

                return true;
            }
            finally
            {
                if (counters.IsCreated)
                    counters.Dispose();
                if (edges.IsCreated)
                    edges.Dispose();
                if (nodes.IsCreated)
                    nodes.Dispose();
            }
        }

        private static bool TryBuildLod(
            FloraGenomeDTO genome,
            string meshName,
            int lod,
            NativeArray<FloraNode> nodes,
            NativeArray<FloraEdge> edges,
            int edgeCount,
            ref FloraTopologyMetrics metrics,
            out Mesh mesh)
        {
            mesh = null;
            int radialSegments = ResolveRadialSegments(genome.RadialSegments, lod);
            int axialSlices = ResolveAxialSlices(genome.PathSegments, lod);
            int maxIncludedEdges = math.max(1, edgeCount);
            bool ribbonSkin = genome.PresetKind == (int)FloraTopologyPreset.KelpForestFrond;
            int vertexCapacity = ribbonSkin
                ? maxIncludedEdges * 2 * (axialSlices + 1)
                : maxIncludedEdges * (radialSegments + 1) * (axialSlices + 1);
            int indexCapacity = ribbonSkin
                ? maxIncludedEdges * axialSlices * 12
                : maxIncludedEdges * radialSegments * axialSlices * 6;

            NativeArray<FloraVertexData> vertices = default;
            NativeArray<uint> indices = default;
            NativeArray<FloraGenerationCounters> counters = default;
            NativeArray<FloraBoundsDTO> bounds = default;

            try
            {
                vertices = new NativeArray<FloraVertexData>(vertexCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                indices = new NativeArray<uint>(indexCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                counters = new NativeArray<FloraGenerationCounters>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                bounds = new NativeArray<FloraBoundsDTO>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                bounds[0] = new FloraBoundsDTO
                {
                    Min = new float3(float.MaxValue, float.MaxValue, float.MaxValue),
                    Max = new float3(float.MinValue, float.MinValue, float.MinValue)
                };

                Stopwatch stopwatch = Stopwatch.StartNew();
                SkinSkeletonJob skinJob = new SkinSkeletonJob
                {
                    Genome = genome,
                    LodLevel = lod,
                    RadialSegments = radialSegments,
                    AxialSlices = axialSlices,
                    EdgeCount = edgeCount,
                    Nodes = nodes,
                    Edges = edges,
                    Vertices = vertices,
                    Indices = indices,
                    Counters = counters,
                    Bounds = bounds
                };
                JobHandle skinHandle = skinJob.Schedule();
                skinHandle.Complete();
                stopwatch.Stop();

                FloraGenerationCounters skinCounters = counters[0];
                if (skinCounters.VertexCount < 3 || skinCounters.IndexCount < 3 || skinCounters.Overflow != 0)
                {
                    Debug.LogError("[FloraTopology1604] LOD build exceeded native capacity. LOD=" + lod + ", Vertices=" + skinCounters.VertexCount + ", Indices=" + skinCounters.IndexCount + ", Overflow=" + skinCounters.Overflow);
                    return false;
                }

                if (!ValidateIndexRange(indices, skinCounters.IndexCount, skinCounters.VertexCount))
                {
                    Debug.LogError("[FloraTopology1604] LOD build emitted out-of-range indices. LOD=" + lod + ", Vertices=" + skinCounters.VertexCount + ", Indices=" + skinCounters.IndexCount);
                    return false;
                }

                int triangleCount = skinCounters.IndexCount / 3;
                int allowedTriangles = ResolveLodTriangleBudget(genome.Lod0TriangleBudget, lod);
                if (triangleCount > allowedTriangles)
                {
                    Debug.LogError("[FloraTopology1604] LOD triangle budget exceeded. LOD=" + lod + ", Triangles=" + triangleCount + ", Budget=" + allowedTriangles);
                    return false;
                }

                FloraBoundsDTO boundsDto = bounds[0];
                Bounds unityBounds = ToBounds(boundsDto);
                ulong hash = ComputeVertexHash(vertices, skinCounters.VertexCount);
                mesh = CreateMesh(meshName + "_LOD" + lod, vertices, indices, skinCounters.VertexCount, skinCounters.IndexCount, unityBounds);

                if (lod == 0)
                {
                    metrics.Lod0Vertices = skinCounters.VertexCount;
                    metrics.Lod0Triangles = triangleCount;
                    metrics.Lod0Hash = hash;
                    metrics.Lod0Milliseconds = stopwatch.Elapsed.TotalMilliseconds;
                    metrics.Lod0Bounds = unityBounds;
                }
                else if (lod == 1)
                {
                    metrics.Lod1Vertices = skinCounters.VertexCount;
                    metrics.Lod1Triangles = triangleCount;
                    metrics.Lod1Hash = hash;
                    metrics.Lod1Milliseconds = stopwatch.Elapsed.TotalMilliseconds;
                    metrics.Lod1Bounds = unityBounds;
                }
                else
                {
                    metrics.Lod2Vertices = skinCounters.VertexCount;
                    metrics.Lod2Triangles = triangleCount;
                    metrics.Lod2Hash = hash;
                    metrics.Lod2Milliseconds = stopwatch.Elapsed.TotalMilliseconds;
                    metrics.Lod2Bounds = unityBounds;
                }

                return true;
            }
            finally
            {
                if (bounds.IsCreated)
                    bounds.Dispose();
                if (counters.IsCreated)
                    counters.Dispose();
                if (indices.IsCreated)
                    indices.Dispose();
                if (vertices.IsCreated)
                    vertices.Dispose();
            }
        }

        private static Mesh CreateMesh(string meshName, NativeArray<FloraVertexData> vertices, NativeArray<uint> indices, int vertexCount, int indexCount, Bounds bounds)
        {
            Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
            Mesh mesh = null;
            bool meshDataApplied = false;
            try
            {
                Mesh.MeshData meshData = meshDataArray[0];
                // Stream budget: Unity supports exactly four vertex streams (0..3). TexCoord0 used
                // to be declared on stream 4, which is outside that range, so no LOD mesh could
                // ever be built. Tangent/TexCoord0/TexCoord1 now share stream 2 and Position,
                // Normal and Color keep the dedicated streams FloraTopologyStudio1711 validates.
                // TexCoord1 is the shader-side "UVMask" set required by Hecton_KelpMaster.shader
                // (Attributes.uvMask : TEXCOORD1) and by 3dmodel.md section 3.
                meshData.SetVertexBufferParams(
                    vertexCount,
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
                    new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1),
                    new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4, 2),
                    new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 3),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 2),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 2, 2));

                meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);

                NativeArray<float3> positions = meshData.GetVertexData<float3>(0);
                NativeArray<float3> normals = meshData.GetVertexData<float3>(1);
                NativeArray<FloraInterleavedStream2Vertex> stream2 = meshData.GetVertexData<FloraInterleavedStream2Vertex>(2);
                NativeArray<Color32> colors = meshData.GetVertexData<Color32>(3);

                for (int i = 0; i < vertexCount; i++)
                {
                    FloraVertexData vertex = vertices[i];
                    positions[i] = vertex.Position;
                    normals[i] = vertex.Normal;
                    stream2[i] = new FloraInterleavedStream2Vertex
                    {
                        Tangent = vertex.Tangent,
                        UV0 = vertex.UV,
                        UVMask = vertex.UVMask
                    };
                    colors[i] = UnpackColor(vertex.PackedColor);
                }

                NativeArray<uint> indexData = meshData.GetIndexData<uint>();
                for (int i = 0; i < indexCount; i++)
                    indexData[i] = indices[i];

                meshData.subMeshCount = 1;
                meshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles)
                {
                    bounds = bounds,
                    vertexCount = vertexCount
                }, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);

                mesh = new Mesh
                {
                    name = meshName,
                    indexFormat = IndexFormat.UInt32
                };
                Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers);
                meshDataApplied = true;
                mesh.bounds = bounds;
                return mesh;
            }
            catch
            {
                if (!meshDataApplied)
                    meshDataArray.Dispose();
                if (mesh != null)
                    UnityEngine.Object.DestroyImmediate(mesh);
                throw;
            }
        }

        private static Mesh CreateOrUpdateMeshAsset(string meshPath, Mesh sourceMesh, out bool createdNew)
        {
            createdNew = false;
            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existingMesh == null)
            {
                Mesh createdMesh = UnityEngine.Object.Instantiate(sourceMesh);
                createdMesh.name = sourceMesh.name;
                AssetDatabase.CreateAsset(createdMesh, meshPath);
                Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (savedMesh != null)
                {
                    createdNew = true;
                    FinalizeMeshAssetForScatter(savedMesh);
                    EditorUtility.SetDirty(savedMesh);
                    return savedMesh;
                }

                Debug.LogError("[FloraTopology1604] Mesh asset save failed. Path=" + meshPath);
                if (AssetDatabase.LoadMainAssetAtPath(meshPath) != null)
                    AssetDatabase.DeleteAsset(meshPath);
                if (!EditorUtility.IsPersistent(createdMesh))
                    UnityEngine.Object.DestroyImmediate(createdMesh);
                return null;
            }

            EditorUtility.CopySerialized(sourceMesh, existingMesh);
            existingMesh.name = sourceMesh.name;
            FinalizeMeshAssetForScatter(existingMesh);
            EditorUtility.SetDirty(existingMesh);
            return existingMesh;
        }

        private static void FinalizeMeshAssetForScatter(Mesh mesh)
        {
            if (mesh == null)
                return;

            mesh.UploadMeshData(true);
        }

        private static void DeleteCreatedMeshAsset(string meshPath, bool createdNew)
        {
            if (!createdNew)
                return;

            if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) == null)
                return;

            if (!AssetDatabase.DeleteAsset(meshPath))
                Debug.LogError("[FloraTopology1604] Failed to delete incomplete mesh asset. Path=" + meshPath);
        }

        private static bool SavePrefab(string prefabPath, string prefabName, FloraTopologyPreset preset, Mesh lod0, Mesh lod1, Mesh lod2, Material material)
        {
            GameObject root = new GameObject(prefabName);
            try
            {
                ConfigureScatterPrefabObject(root);
                LODGroup lodGroup = root.AddComponent<LODGroup>();
                lodGroup.animateCrossFading = true;
                lodGroup.fadeMode = LODFadeMode.CrossFade;

                Renderer lod0Renderer = AddRenderer(root.transform, "__LOD0", lod0, material);
                Renderer lod1Renderer = AddRenderer(root.transform, "__LOD1", lod1, material);
                Renderer lod2Renderer = AddRenderer(root.transform, "__LOD2", lod2, material);

                lodGroup.SetLODs(new[]
                {
                    new LOD(Lod0Threshold, new[] { lod0Renderer }),
                    new LOD(Lod1Threshold, new[] { lod1Renderer }),
                    new LOD(Lod2Threshold, new[] { lod2Renderer })
                });
                ApplyLodBounds(lodGroup, lod0, lod1, lod2);

                if (preset != FloraTopologyPreset.KelpForestFrond)
                    AddCollisionProxy(root, lod0, preset);

                if (!ColliderOptimizerEngine1716.ValidatePrefabColliderBudget(root, out string colliderFailure))
                {
                    Debug.LogError("[FloraTopology1604] 1716 collider validation failed before save. Path=" + prefabPath + " Failure=" + colliderFailure);
                    return false;
                }

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (savedPrefab != null)
                {
                    if (!ColliderOptimizerEngine1716.ValidatePrefabAssetTopology(prefabPath, out colliderFailure))
                    {
                        Debug.LogError("[FloraTopology1604] 1716 collider validation failed after save. Path=" + prefabPath + " Failure=" + colliderFailure);
                        return false;
                    }

                    return true;
                }

                Debug.LogError("[FloraTopology1604] Prefab save failed. Path=" + prefabPath);
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Renderer AddRenderer(Transform parent, string name, Mesh mesh, Material material)
        {
            GameObject child = new GameObject(name);
            ConfigureScatterPrefabObject(child);
            child.transform.SetParent(parent, false);

            MeshFilter meshFilter = child.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = true;
            return renderer;
        }

        private static void ConfigureScatterPrefabObject(GameObject target)
        {
            target.isStatic = false;
            GameObjectUtility.SetStaticEditorFlags(target, 0);
        }

        private static void AddCollisionProxy(GameObject root, Mesh lod0, FloraTopologyPreset preset)
        {
            Bounds bounds = lod0 != null ? lod0.bounds : new Bounds(Vector3.zero, Vector3.one);
            GameObject proxyRoot = new GameObject(GeneratedCompoundRootName);
            proxyRoot.transform.SetParent(root.transform, false);
            proxyRoot.transform.localPosition = Vector3.zero;
            proxyRoot.transform.localRotation = Quaternion.identity;
            proxyRoot.transform.localScale = Vector3.one;

            GameObject colliderObject = new GameObject("COL_Sphere_" + preset);
            colliderObject.transform.SetParent(proxyRoot.transform, false);
            colliderObject.transform.localPosition = Vector3.zero;
            colliderObject.transform.localRotation = Quaternion.identity;
            colliderObject.transform.localScale = Vector3.one;

            SphereCollider collider = colliderObject.AddComponent<SphereCollider>();
            collider.center = bounds.center;
            collider.radius = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
            collider.isTrigger = preset == FloraTopologyPreset.ThermalTubeWorm;

            int physicsLayer = ResolveFloraCollisionLayer(collider.isTrigger);
            if (physicsLayer >= 0)
            {
                proxyRoot.layer = physicsLayer;
                colliderObject.layer = physicsLayer;
            }
        }

        private static int ResolveFloraCollisionLayer(bool trigger)
        {
            if (trigger)
            {
                int floraNonColliding = LayerMask.NameToLayer("Flora_NonColliding");
                if (floraNonColliding >= 0)
                    return floraNonColliding;
            }

            int worldStatic = LayerMask.NameToLayer("World_Static");
            if (worldStatic >= 0)
                return worldStatic;

            int physicsLayer = LayerMask.NameToLayer("Physics");
            if (physicsLayer >= 0)
                return physicsLayer;

            return 0;
        }

        private static void ApplyLodBounds(LODGroup lodGroup, Mesh lod0, Mesh lod1, Mesh lod2)
        {
            Bounds combined = lod0 != null ? lod0.bounds : new Bounds(Vector3.zero, Vector3.one * 0.1f);
            if (lod1 != null)
                combined.Encapsulate(lod1.bounds);
            if (lod2 != null)
                combined.Encapsulate(lod2.bounds);

            lodGroup.localReferencePoint = combined.center;
            lodGroup.size = Mathf.Max(0.01f, Mathf.Max(combined.size.x, Mathf.Max(combined.size.y, combined.size.z)));
        }

        private static bool TryResolveMaterial(FloraTopologyPreset preset, out Material material)
        {
            string path = preset == FloraTopologyPreset.KelpForestFrond
                ? KelpMaterialPath
                : preset == FloraTopologyPreset.ThermalTubeWorm
                    ? MassiveCoralMaterialPath
                    : CoralMaterialPath;

            material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null && IsExpectedFloraShader(preset, material.shader))
                return true;

            if (material != null)
                Debug.LogError("[FloraTopology1604] Required flora material has wrong shader. Path=" + path + ", Shader=" + (material.shader != null ? material.shader.name : "<null>"));
            else
                Debug.LogError("[FloraTopology1604] Required flora material missing. Path=" + path);

            material = null;
            return false;
        }

        internal static bool IsExpectedFloraShader(FloraTopologyPreset preset, Shader shader)
        {
            if (shader == null)
                return false;

            string shaderName = shader.name;
            if (preset == FloraTopologyPreset.KelpForestFrond)
                return shaderName == KelpShaderName || shaderName == KelpGpuInstancerShaderName;

            return shaderName == CoralShaderName || shaderName == CoralGpuInstancerShaderName;
        }

        private static bool RunPresetDeterminism(FloraTopologyPreset preset, uint seed)
        {
            FloraGenomeDTO genome = CreateGenome(preset, seed, 0.75f, ResolveDefaultTriangleBudget(preset));
            if (!TryGenerateMeshes(genome, "GEN_FloraTopology1604_TestA", out Mesh[] firstMeshes, out FloraTopologyMetrics first))
                return false;

            DestroyMeshes(firstMeshes);

            if (!TryGenerateMeshes(genome, "GEN_FloraTopology1604_TestB", out Mesh[] secondMeshes, out FloraTopologyMetrics second))
                return false;

            DestroyMeshes(secondMeshes);

            bool equal = first.Lod0Hash == second.Lod0Hash
                && first.Lod1Hash == second.Lod1Hash
                && first.Lod2Hash == second.Lod2Hash
                && first.Lod0Vertices == second.Lod0Vertices
                && first.Lod1Vertices == second.Lod1Vertices
                && first.Lod2Vertices == second.Lod2Vertices;

            if (!equal)
                Debug.LogError("[FloraTopology1604] Determinism failure for " + preset + ". First=" + first.Lod0Hash.ToString("X16") + ", Second=" + second.Lod0Hash.ToString("X16"));

            return equal;
        }

        private static bool RunPresetSilhouetteAudit(FloraTopologyPreset preset, uint seed, float quality, int triangleBudget)
        {
            FloraGenomeDTO genome = CreateGenome(preset, seed, quality, triangleBudget);
            if (!TryGenerateMeshes(genome, "GEN_FloraTopology1604_Silhouette_" + preset, out Mesh[] meshes, out FloraTopologyMetrics metrics))
                return false;

            DestroyMeshes(meshes);
            Debug.Log("[FloraTopology1604] Silhouette audit " + preset
                + " VolumeDelta=" + ComputeVolumeDelta(metrics.Lod0Bounds, metrics.Lod2Bounds).ToString("P2")
                + " CenterShift=" + ComputeNormalizedCenterShift(metrics.Lod0Bounds, metrics.Lod2Bounds).ToString("P2"));
            return true;
        }

        private static bool ValidateLodSilhouette(string label, FloraTopologyMetrics metrics)
        {
            float volumeDelta = ComputeVolumeDelta(metrics.Lod0Bounds, metrics.Lod2Bounds);
            float centerShift = ComputeNormalizedCenterShift(metrics.Lod0Bounds, metrics.Lod2Bounds);
            if (volumeDelta > 0.10f || centerShift > 0.10f)
            {
                Debug.LogError("[FloraTopology1604] LOD silhouette audit failed for " + label
                    + ". VolumeDelta=" + volumeDelta.ToString("P2")
                    + ", CenterShift=" + centerShift.ToString("P2"));
                return false;
            }

            return true;
        }

        private static float ComputeVolumeDelta(Bounds lod0, Bounds lod2)
        {
            float lod0Volume = ComputeBoundsVolume(lod0);
            float lod2Volume = ComputeBoundsVolume(lod2);
            return math.abs(lod0Volume - lod2Volume) / math.max(0.0001f, lod0Volume);
        }

        private static float ComputeBoundsVolume(Bounds bounds)
        {
            Vector3 size = bounds.size;
            return Mathf.Max(0.0001f, size.x) * Mathf.Max(0.0001f, size.y) * Mathf.Max(0.0001f, size.z);
        }

        private static float ComputeNormalizedCenterShift(Bounds lod0, Bounds lod2)
        {
            Vector3 centerDelta = lod2.center - lod0.center;
            float referenceExtent = Mathf.Max(0.0001f, Mathf.Max(lod0.extents.x, Mathf.Max(lod0.extents.y, lod0.extents.z)));
            return centerDelta.magnitude / referenceExtent;
        }

        private static void DestroyMeshes(Mesh[] meshes)
        {
            if (meshes == null)
                return;

            for (int i = 0; i < meshes.Length; i++)
            {
                if (meshes[i] != null)
                    UnityEngine.Object.DestroyImmediate(meshes[i]);
            }
        }

        private static bool ValidateGenome(FloraGenomeDTO genome)
        {
            if (genome.MaxNodeCount < 2 || genome.MaxEdgeCount < 1)
                return false;

            if (genome.Lod0TriangleBudget < 192 || genome.Lod0TriangleBudget > 300000)
                return false;

            if (genome.RecursionDepth < 1 || genome.RecursionDepth > 12)
                return false;

            if (genome.RadialSegments < 3 || genome.RadialSegments > 24)
                return false;

            if (genome.PathSegments < 1 || genome.PathSegments > 16)
                return false;

            if (!math.isfinite(genome.SegmentLength) || !math.isfinite(genome.BaseRadius))
                return false;

            return genome.SegmentLength > 0f && genome.BaseRadius > 0f;
        }

        private static bool ValidateRuntimeLayouts()
        {
            bool valid = true;
            valid &= ValidateSize<FloraGenomeDTO>("FloraGenomeDTO", 80);
            valid &= ValidateOffset<FloraGenomeDTO>(nameof(FloraGenomeDTO.Seed), 0);
            valid &= ValidateOffset<FloraGenomeDTO>(nameof(FloraGenomeDTO.PresetHash), 4);
            valid &= ValidateOffset<FloraGenomeDTO>(nameof(FloraGenomeDTO.PresetKind), 8);
            valid &= ValidateOffset<FloraGenomeDTO>(nameof(FloraGenomeDTO.RecursionDepth), 12);
            valid &= ValidateOffset<FloraGenomeDTO>(nameof(FloraGenomeDTO.MaxNodeCount), 16);
            valid &= ValidateOffset<FloraGenomeDTO>(nameof(FloraGenomeDTO.MaxEdgeCount), 20);
            valid &= ValidateOffset<FloraGenomeDTO>(nameof(FloraGenomeDTO.RadialSegments), 24);
            valid &= ValidateOffset<FloraGenomeDTO>(nameof(FloraGenomeDTO.PathSegments), 28);
            valid &= ValidateOffset<FloraGenomeDTO>(nameof(FloraGenomeDTO.BranchAngleRadians), 32);
            valid &= ValidateOffset<FloraGenomeDTO>(nameof(FloraGenomeDTO.SegmentLength), 36);
            valid &= ValidateOffset<FloraGenomeDTO>(nameof(FloraGenomeDTO.LengthFalloff), 40);
            valid &= ValidateOffset<FloraGenomeDTO>(nameof(FloraGenomeDTO.BaseRadius), 44);
            valid &= ValidateOffset<FloraGenomeDTO>(nameof(FloraGenomeDTO.RadiusFalloff), 48);
            valid &= ValidateOffset<FloraGenomeDTO>(nameof(FloraGenomeDTO.TwistRadians), 52);
            valid &= ValidateOffset<FloraGenomeDTO>(nameof(FloraGenomeDTO.TwistProbability), 56);
            valid &= ValidateOffset<FloraGenomeDTO>(nameof(FloraGenomeDTO.GlowWeight), 60);
            valid &= ValidateOffset<FloraGenomeDTO>(nameof(FloraGenomeDTO.GlobalQualityWeight), 64);
            valid &= ValidateOffset<FloraGenomeDTO>(nameof(FloraGenomeDTO.Lod0TriangleBudget), 68);
            valid &= ValidateOffset<FloraGenomeDTO>(nameof(FloraGenomeDTO.Flags), 72);
            valid &= ValidateOffset<FloraGenomeDTO>("_pad0", 76);

            valid &= ValidateSize<FloraNode>("FloraNode", 40);
            valid &= ValidateSize<FloraEdge>("FloraEdge", 16);
            valid &= ValidateSize<FloraVertexData>("FloraVertexData", 64);
            valid &= ValidateOffset<FloraVertexData>(nameof(FloraVertexData.Position), 0);
            valid &= ValidateOffset<FloraVertexData>(nameof(FloraVertexData.Normal), 12);
            valid &= ValidateOffset<FloraVertexData>(nameof(FloraVertexData.Tangent), 24);
            valid &= ValidateOffset<FloraVertexData>(nameof(FloraVertexData.UV), 40);
            valid &= ValidateOffset<FloraVertexData>(nameof(FloraVertexData.UVMask), 48);
            valid &= ValidateOffset<FloraVertexData>(nameof(FloraVertexData.PackedColor), 56);
            valid &= ValidateOffset<FloraVertexData>("Pad0", 60);
            valid &= ValidateSize<FloraBoundsDTO>("FloraBoundsDTO", 24);
            valid &= ValidateSize<FloraGenerationCounters>("FloraGenerationCounters", 32);

            // These three offsets are the contract with Unity's in-stream attribute packing for
            // vertex stream 2. If they drift, TexCoord1 stops landing where the shader's TEXCOORD1
            // binding expects it and every sway term silently collapses to zero.
            valid &= ValidateSize<FloraInterleavedStream2Vertex>("FloraInterleavedStream2Vertex", 32);
            valid &= ValidateOffset<FloraInterleavedStream2Vertex>(nameof(FloraInterleavedStream2Vertex.Tangent), 0);
            valid &= ValidateOffset<FloraInterleavedStream2Vertex>(nameof(FloraInterleavedStream2Vertex.UV0), 16);
            valid &= ValidateOffset<FloraInterleavedStream2Vertex>(nameof(FloraInterleavedStream2Vertex.UVMask), 24);
            return valid;
        }

        private static bool ValidateSize<T>(string label, int expectedBytes) where T : unmanaged
        {
            int actualBytes = UnsafeUtility.SizeOf<T>();
            if (actualBytes == expectedBytes && (actualBytes & 7) == 0)
                return true;

            Debug.LogError("[FloraTopology1604] Layout size drift " + label + ". Expected=" + expectedBytes + ", Actual=" + actualBytes);
            return false;
        }

        private static bool ValidateOffset<T>(string fieldName, int expectedOffset) where T : unmanaged
        {
            int actualOffset = Marshal.OffsetOf<T>(fieldName).ToInt32();
            if (actualOffset == expectedOffset)
                return true;

            Debug.LogError("[FloraTopology1604] Layout offset drift " + typeof(T).Name + "." + fieldName + ". Expected=" + expectedOffset + ", Actual=" + actualOffset);
            return false;
        }

        private static int ResolveRadialSegments(int baseSegments, int lod)
        {
            if (lod == 0)
                return math.max(3, baseSegments);

            if (lod == 1)
                return math.max(3, (int)math.ceil(baseSegments * 0.66f));

            return math.max(3, (int)math.ceil(baseSegments * 0.42f));
        }

        private static int ResolveAxialSlices(int basePathSegments, int lod)
        {
            int safeSegments = math.clamp(basePathSegments, 1, 16);
            if (lod == 0)
                return math.clamp(safeSegments, 2, 4);

            if (lod == 1)
                return math.clamp((int)math.ceil(safeSegments * 0.5f), 1, 2);

            return 1;
        }

        private static int ResolveLodTriangleBudget(int lod0TriangleBudget, int lod)
        {
            int safeBudget = math.clamp(lod0TriangleBudget, 192, 300000);
            if (lod == 0)
                return safeBudget;

            if (lod == 1)
                return math.max(96, (int)math.ceil(safeBudget * 0.60f));

            return math.max(48, (int)math.ceil(safeBudget * 0.35f));
        }

        private static int ResolveEdgeCapacityForBudget(int triangleBudget, int radialSegments, int pathSegments, bool ribbonSkin)
        {
            int axialSlices = ResolveAxialSlices(pathSegments, 0);
            int trianglesPerEdge = ribbonSkin
                ? axialSlices * 4
                : math.max(3, radialSegments) * axialSlices * 2;
            int edgeBudget = triangleBudget / math.max(1, trianglesPerEdge);
            return math.clamp(edgeBudget, 1, 8191);
        }

        private static Bounds ToBounds(FloraBoundsDTO dto)
        {
            float3 min = dto.Min;
            float3 max = dto.Max;
            if (!math.all(math.isfinite(min)) || !math.all(math.isfinite(max)))
                return new Bounds(Vector3.zero, Vector3.one * 0.01f);

            float3 center = (min + max) * 0.5f;
            float3 size = math.max(max - min, new float3(0.01f, 0.01f, 0.01f));
            return new Bounds(ToVector3(center), ToVector3(size));
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static Color32 UnpackColor(uint packed)
        {
            return new Color32(
                (byte)(packed & 0xFFu),
                (byte)((packed >> 8) & 0xFFu),
                (byte)((packed >> 16) & 0xFFu),
                (byte)((packed >> 24) & 0xFFu));
        }

        private static ulong ComputeVertexHash(NativeArray<FloraVertexData> vertices, int count)
        {
            ulong hash = 1469598103934665603ul;
            int safeCount = math.clamp(count, 0, vertices.Length);
            for (int i = 0; i < safeCount; i++)
            {
                FloraVertexData vertex = vertices[i];
                hash = MixHash(hash, math.asuint(vertex.Position.x));
                hash = MixHash(hash, math.asuint(vertex.Position.y));
                hash = MixHash(hash, math.asuint(vertex.Position.z));
                hash = MixHash(hash, math.asuint(vertex.Normal.x));
                hash = MixHash(hash, math.asuint(vertex.Normal.y));
                hash = MixHash(hash, math.asuint(vertex.Normal.z));
                hash = MixHash(hash, vertex.PackedColor);
            }

            return hash;
        }

        private static ulong MixHash(ulong hash, uint value)
        {
            hash ^= value;
            hash *= 1099511628211ul;
            return hash;
        }

        private static bool ValidateIndexRange(NativeArray<uint> indices, int indexCount, int vertexCount)
        {
            if (vertexCount <= 0 || indexCount <= 0 || indexCount > indices.Length)
                return false;

            uint maxVertex = (uint)vertexCount;
            for (int i = 0; i < indexCount; i++)
            {
                if (indices[i] >= maxVertex)
                    return false;
            }

            return true;
        }

        private static uint SanitizeSeed(uint seed)
        {
            return seed == 0u ? 1u : seed;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            int separator = assetPath.LastIndexOf('/');
            if (separator <= 0)
                return;

            string parent = assetPath.Substring(0, separator);
            string folder = assetPath.Substring(separator + 1);
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(assetPath))
                AssetDatabase.CreateFolder(parent, folder);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct LSystemGrowthJob : IJob
    {
        public FloraGenomeDTO Genome;
        [NoAlias] public NativeArray<FloraNode> Nodes;
        [NoAlias] public NativeArray<FloraEdge> Edges;
        [NoAlias] public NativeArray<FloraGenerationCounters> Counters;

        public void Execute()
        {
            FloraGenerationCounters counters = default;
            uint seed = Genome.Seed == 0u ? 1u : Genome.Seed;
            Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex(seed ^ Genome.PresetHash);

            int rootCount = ResolveRootCount(Genome.PresetKind);
            for (int i = 0; i < rootCount; i++)
            {
                float angle = (math.PI * 2f) * (i / (float)math.max(1, rootCount));
                float radius = Genome.PresetKind == (int)FloraTopologyPreset.ThermalTubeWorm ? 0.22f : 0.02f;
                float3 pos = new float3(math.cos(angle) * radius, 0f, math.sin(angle) * radius);
                float3 dir = ResolveInitialDirection(Genome.PresetKind, angle);
                TryAddNode(ref counters, new FloraNode
                {
                    Position = pos,
                    Direction = dir,
                    Radius = Genome.BaseRadius * ResolveRootRadiusScale(Genome.PresetKind, i),
                    RootDistance = 0f,
                    PhaseHash = Hash(seed, (uint)i, 0xA511E9B3u),
                    ParentIndex = -1
                });
            }

            int currentStart = 0;
            int currentCount = counters.NodeCount;
            float segmentLength = Genome.SegmentLength;
            float radiusScale = 1f;

            for (int depth = 0; depth < Genome.RecursionDepth && currentCount > 0; depth++)
            {
                int nextStart = counters.NodeCount;
                int nextCount = 0;
                int childCount = ResolveChildCount(Genome.PresetKind, depth);

                for (int i = 0; i < currentCount; i++)
                {
                    int parentIndex = currentStart + i;
                    if (parentIndex < 0 || parentIndex >= counters.NodeCount)
                        continue;

                    FloraNode parent = Nodes[parentIndex];
                    for (int child = 0; child < childCount; child++)
                    {
                        if (counters.NodeCount >= Nodes.Length || counters.EdgeCount >= Edges.Length)
                        {
                            counters.Truncated = 1;
                            Counters[0] = counters;
                            return;
                        }

                        float variation = 0.86f + random.NextFloat(0f, 0.28f);
                        float3 direction = ResolveChildDirection(parent.Direction, Genome, depth, child, childCount, ref random);
                        float length = segmentLength * variation;
                        float3 position = parent.Position + direction * length;
                        float childRadius = math.max(0.006f, Genome.BaseRadius * radiusScale * Genome.RadiusFalloff);

                        int nodeIndex = counters.NodeCount;
                        TryAddNode(ref counters, new FloraNode
                        {
                            Position = position,
                            Direction = direction,
                            Radius = childRadius,
                            RootDistance = parent.RootDistance + length,
                            PhaseHash = Hash(seed, (uint)nodeIndex, (uint)(depth + 1) * 0x9E3779B9u),
                            ParentIndex = parentIndex
                        });

                        TryAddEdge(ref counters, new FloraEdge
                        {
                            StartNode = parentIndex,
                            EndNode = nodeIndex,
                            BranchDepth = depth,
                            Flags = child == 0 ? 0 : 1
                        });
                        nextCount++;
                    }
                }

                currentStart = nextStart;
                currentCount = nextCount;
                segmentLength *= Genome.LengthFalloff;
                radiusScale *= Genome.RadiusFalloff;
            }

            Counters[0] = counters;
        }

        private void TryAddNode(ref FloraGenerationCounters counters, FloraNode node)
        {
            if (counters.NodeCount >= Nodes.Length)
            {
                counters.Overflow = 1;
                return;
            }

            Nodes[counters.NodeCount] = node;
            counters.NodeCount++;
        }

        private void TryAddEdge(ref FloraGenerationCounters counters, FloraEdge edge)
        {
            if (counters.EdgeCount >= Edges.Length)
            {
                counters.Overflow = 1;
                return;
            }

            Edges[counters.EdgeCount] = edge;
            counters.EdgeCount++;
        }

        private static int ResolveRootCount(int presetKind)
        {
            if (presetKind == (int)FloraTopologyPreset.ThermalTubeWorm)
                return 7;

            if (presetKind == (int)FloraTopologyPreset.AbyssalBrainCoral)
                return 3;

            return 1;
        }

        private static float ResolveRootRadiusScale(int presetKind, int rootIndex)
        {
            if (presetKind == (int)FloraTopologyPreset.ThermalTubeWorm)
                return 0.82f + (rootIndex & 3) * 0.07f;

            return 1f;
        }

        private static float3 ResolveInitialDirection(int presetKind, float angle)
        {
            if (presetKind == (int)FloraTopologyPreset.AbyssalBrainCoral)
                return math.normalize(new float3(math.cos(angle) * 0.18f, 0.92f, math.sin(angle) * 0.18f));

            if (presetKind == (int)FloraTopologyPreset.ThermalTubeWorm)
                return math.normalize(new float3(math.cos(angle) * 0.08f, 0.98f, math.sin(angle) * 0.08f));

            return new float3(0f, 1f, 0f);
        }

        private static int ResolveChildCount(int presetKind, int depth)
        {
            if (presetKind == (int)FloraTopologyPreset.AbyssalBrainCoral)
                return depth == 0 ? 4 : 3;

            if (presetKind == (int)FloraTopologyPreset.ThermalTubeWorm)
                return depth < 2 ? 1 : (depth == 2 ? 2 : 1);

            return depth > 0 && (depth & 1) == 0 ? 2 : 1;
        }

        private static float3 ResolveChildDirection(float3 parentDirection, FloraGenomeDTO genome, int depth, int child, int childCount, ref Unity.Mathematics.Random random)
        {
            float3 forward = math.normalizesafe(parentDirection, new float3(0f, 1f, 0f));
            float3 side = StablePerpendicular(forward);
            float3 binormal = math.normalizesafe(math.cross(forward, side), new float3(0f, 0f, 1f));
            float centered = childCount <= 1 ? 0f : (child / (float)(childCount - 1)) - 0.5f;
            float twistChance = math.saturate(genome.TwistProbability);
            float twist = random.NextFloat(0f, 1f) <= twistChance
                ? random.NextFloat(-genome.TwistRadians, genome.TwistRadians)
                : 0f;
            float yaw = centered * math.PI * 2f + twist;
            float branchAngle = child == 0
                ? genome.BranchAngleRadians * 0.22f
                : genome.BranchAngleRadians * (0.78f + random.NextFloat(0f, 0.22f));

            if (genome.PresetKind == (int)FloraTopologyPreset.KelpForestFrond)
                branchAngle *= child == 0 ? 0.18f : 0.72f;

            if (genome.PresetKind == (int)FloraTopologyPreset.ThermalTubeWorm)
                branchAngle *= 0.38f;

            float3 radial = math.normalizesafe(side * math.cos(yaw) + binormal * math.sin(yaw), side);
            float3 direction = math.normalizesafe(forward * math.cos(branchAngle) + radial * math.sin(branchAngle), forward);

            if (genome.PresetKind == (int)FloraTopologyPreset.AbyssalBrainCoral)
            {
                float sphericalBias = math.saturate(depth * 0.14f);
                direction = math.normalizesafe(math.lerp(direction, radial + new float3(0f, 0.26f, 0f), sphericalBias), direction);
            }

            return direction;
        }

        private static float3 StablePerpendicular(float3 forward)
        {
            float3 axis = math.abs(forward.y) < 0.82f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);
            return math.normalizesafe(math.cross(axis, forward), new float3(1f, 0f, 0f));
        }

        private static uint Hash(uint a, uint b, uint c)
        {
            uint h = 2166136261u;
            h = (h ^ a) * 16777619u;
            h = (h ^ b) * 16777619u;
            h = (h ^ c) * 16777619u;
            return h;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct SkinSkeletonJob : IJob
    {
        public FloraGenomeDTO Genome;
        public int LodLevel;
        public int RadialSegments;
        public int AxialSlices;
        public int EdgeCount;

        [ReadOnly, NoAlias] public NativeArray<FloraNode> Nodes;
        [ReadOnly, NoAlias] public NativeArray<FloraEdge> Edges;
        [NoAlias] public NativeArray<FloraVertexData> Vertices;
        [NoAlias] public NativeArray<uint> Indices;
        [NoAlias] public NativeArray<FloraGenerationCounters> Counters;
        [NoAlias] public NativeArray<FloraBoundsDTO> Bounds;

        public void Execute()
        {
            FloraGenerationCounters counters = default;
            FloraBoundsDTO bounds = Bounds[0];

            for (int edgeIndex = 0; edgeIndex < EdgeCount; edgeIndex++)
            {
                FloraEdge edge = Edges[edgeIndex];
                if (edge.StartNode < 0 || edge.EndNode < 0 || edge.StartNode >= Nodes.Length || edge.EndNode >= Nodes.Length)
                    continue;

                FloraNode start = Nodes[edge.StartNode];
                FloraNode end = Nodes[edge.EndNode];
                if (Genome.PresetKind == (int)FloraTopologyPreset.KelpForestFrond)
                    AddRibbon(ref counters, ref bounds, start, end, edge.BranchDepth);
                else
                    AddTube(ref counters, ref bounds, start, end, edge.BranchDepth);
                if (counters.Overflow != 0)
                    break;
            }

            Counters[0] = counters;
            Bounds[0] = bounds;
        }

        private void AddRibbon(ref FloraGenerationCounters counters, ref FloraBoundsDTO boundsDto, FloraNode start, FloraNode end, int branchDepth)
        {
            float3 delta = end.Position - start.Position;
            float lengthSq = math.lengthsq(delta);
            if (lengthSq < 0.0000001f)
                return;

            int safeAxialSlices = math.max(1, AxialSlices);
            int vertexCount = (safeAxialSlices + 1) * 2;
            int indexCount = safeAxialSlices * 12;
            if (counters.VertexCount + vertexCount > Vertices.Length || counters.IndexCount + indexCount > Indices.Length)
            {
                counters.Overflow = 1;
                return;
            }

            float maxRootDistance = math.max(0.001f, EstimateMaxRootDistance(Genome));
            float3 tangent = delta * math.rsqrt(lengthSq);
            float3 side = StablePerpendicular(tangent);
            float3 normal = math.normalizesafe(math.cross(side, tangent), new float3(0f, 0f, 1f));
            float lodWidthScale = 1f;
            int baseVertex = counters.VertexCount;

            for (int ring = 0; ring <= safeAxialSlices; ring++)
            {
                float t = ring / (float)safeAxialSlices;
                float3 center = math.lerp(start.Position, end.Position, t);
                float rootDistance = math.lerp(start.RootDistance, end.RootDistance, t);
                float widthNoise = 0.84f + math.sin(rootDistance * 8.7f + (end.PhaseHash & 255u) * 0.019f) * 0.16f;
                float halfWidth = math.max(0.012f, math.lerp(start.Radius, end.Radius, t) * 3.75f * lodWidthScale * widthNoise);

                for (int sideIndex = 0; sideIndex < 2; sideIndex++)
                {
                    float edgeSign = sideIndex == 0 ? -1f : 1f;
                    float edgeWave = math.sin(rootDistance * 13.1f + sideIndex * 2.31f + branchDepth * 0.73f) * halfWidth * 0.10f * lodWidthScale;
                    float3 position = center + side * (edgeSign * halfWidth) + normal * edgeWave;
                    float leverage01 = math.saturate(rootDistance / maxRootDistance);
                    byte sway = ResolveSwayByte(Genome.PresetKind, leverage01);
                    // Channel G carries the bioluminescence MASK, which is what ResolveGlow returns.
                    // It used to carry HashToByte(...) as a "phase" instead, and that was wrong on
                    // two counts the contract states outright: 3DMODEL_FLORA_CORAL.md section 2
                    // requires "Non-emissive tissue = 0" and an FNV hash is never 0 for non-emissive
                    // families, and the hash is uncorrelated between adjacent vertices so it is not
                    // a usable phase field in the first place. It also mixed LodLevel into its input,
                    // so the same vertex changed value per LOD, against section 6's requirement that
                    // LOD2 "Keep vertex color R/G/B semantics". The consumers never wanted an
                    // authored phase: Hecton_CoralMaster.shader synthesises phase spatially from
                    // biolumLocalAupCoord, and Hecton_KelpMaster.shader reads COLOR.g as
                    // bakedBiolumMask, which is exactly this quantity.
                    byte biolumMask = (byte)math.clamp((int)math.round(ResolveGlow(position, Genome.GlowWeight) * 255f), 0, 255);

                    Vertices[counters.VertexCount] = new FloraVertexData
                    {
                        Position = position,
                        Normal = normal,
                        Tangent = new float4(side, 1f),
                        UV = new float2(sideIndex, leverage01),
                        // UVMask (TEXCOORD1): U is 0 and 1 at the blade margins, V is the geodesic
                        // root-to-tip distance with the holdfast pinned at 0. This is the mask set
                        // Hecton_KelpMaster.shader binds and multiplies every sway, prop-wash and
                        // player-interaction term by, and the uv.y = [0=root, 1=tip] input that
                        // REND_Instanced_Flora_Physics.txt section III.C requires.
                        UVMask = new float2(sideIndex, leverage01),
                        PackedColor = PackColor(sway, biolumMask, NoBakedOcclusion, FamilySpecificMask),
                        Pad0 = 0u
                    };

                    ExpandBounds(ref boundsDto, position);
                    counters.VertexCount++;
                }
            }

            for (int slice = 0; slice < safeAxialSlices; slice++)
            {
                uint a = (uint)(baseVertex + slice * 2);
                uint b = (uint)(baseVertex + (slice + 1) * 2);
                uint c = b + 1u;
                uint d = a + 1u;

                Indices[counters.IndexCount++] = a;
                Indices[counters.IndexCount++] = b;
                Indices[counters.IndexCount++] = c;
                Indices[counters.IndexCount++] = a;
                Indices[counters.IndexCount++] = c;
                Indices[counters.IndexCount++] = d;
                Indices[counters.IndexCount++] = a;
                Indices[counters.IndexCount++] = c;
                Indices[counters.IndexCount++] = b;
                Indices[counters.IndexCount++] = a;
                Indices[counters.IndexCount++] = d;
                Indices[counters.IndexCount++] = c;
            }
        }

        private void AddTube(ref FloraGenerationCounters counters, ref FloraBoundsDTO boundsDto, FloraNode start, FloraNode end, int branchDepth)
        {
            float3 delta = end.Position - start.Position;
            float lengthSq = math.lengthsq(delta);
            if (lengthSq < 0.0000001f)
                return;

            int safeAxialSlices = math.max(1, AxialSlices);
            int ringVertexCount = (RadialSegments + 1) * (safeAxialSlices + 1);
            int indexCount = RadialSegments * safeAxialSlices * 6;
            if (counters.VertexCount + ringVertexCount > Vertices.Length || counters.IndexCount + indexCount > Indices.Length)
            {
                counters.Overflow = 1;
                return;
            }

            float maxRootDistance = math.max(0.001f, EstimateMaxRootDistance(Genome));
            float3 tangent = delta * math.rsqrt(lengthSq);
            float3 side = StablePerpendicular(tangent);
            float3 binormal = math.normalizesafe(math.cross(tangent, side), new float3(0f, 0f, 1f));
            float lodRadiusScale = 1f;
            int baseVertex = counters.VertexCount;

            for (int ring = 0; ring <= safeAxialSlices; ring++)
            {
                float t = ring / (float)safeAxialSlices;
                float3 center = math.lerp(start.Position, end.Position, t);
                float baseRadius = math.max(0.0025f, math.lerp(start.Radius, end.Radius, t) * lodRadiusScale);
                float rootDistance = math.lerp(start.RootDistance, end.RootDistance, t);

                for (int sideIndex = 0; sideIndex <= RadialSegments; sideIndex++)
                {
                    float u = sideIndex / (float)RadialSegments;
                    float angle = u * math.PI * 2f;
                    float sin = math.sin(angle);
                    float cos = math.cos(angle);
                    float3 normal = math.normalizesafe(side * cos + binormal * sin, side);
                    float radius = math.max(0.0025f, baseRadius * ResolveSurfaceRadiusScale(Genome.PresetKind, center, rootDistance, sideIndex, ring, branchDepth, LodLevel));
                    float3 position = center + normal * radius;
                    float3 tangent4 = math.normalizesafe(-side * sin + binormal * cos, binormal);
                    float leverage01 = math.saturate(rootDistance / maxRootDistance);
                    byte sway = ResolveSwayByte(Genome.PresetKind, leverage01);
                    // Channel G carries the bioluminescence MASK, which is what ResolveGlow returns.
                    // It used to carry HashToByte(...) as a "phase" instead, and that was wrong on
                    // two counts the contract states outright: 3DMODEL_FLORA_CORAL.md section 2
                    // requires "Non-emissive tissue = 0" and an FNV hash is never 0 for non-emissive
                    // families, and the hash is uncorrelated between adjacent vertices so it is not
                    // a usable phase field in the first place. It also mixed LodLevel into its input,
                    // so the same vertex changed value per LOD, against section 6's requirement that
                    // LOD2 "Keep vertex color R/G/B semantics". The consumers never wanted an
                    // authored phase: Hecton_CoralMaster.shader synthesises phase spatially from
                    // biolumLocalAupCoord, and Hecton_KelpMaster.shader reads COLOR.g as
                    // bakedBiolumMask, which is exactly this quantity.
                    byte biolumMask = (byte)math.clamp((int)math.round(ResolveGlow(position, Genome.GlowWeight) * 255f), 0, 255);

                    Vertices[counters.VertexCount] = new FloraVertexData
                    {
                        Position = position,
                        Normal = normal,
                        Tangent = new float4(tangent4, 1f),
                        UV = new float2(u, leverage01),
                        // UVMask (TEXCOORD1): U is the circumferential parameter, V is the geodesic
                        // root-to-tip distance with the anchor ring pinned at 0. Tube families keep
                        // the same V semantics as the ribbon family so one mask contract covers
                        // every preset and no consumer has to branch on preset kind.
                        UVMask = new float2(u, leverage01),
                        PackedColor = PackColor(sway, biolumMask, NoBakedOcclusion, FamilySpecificMask),
                        Pad0 = 0u
                    };

                    ExpandBounds(ref boundsDto, position);
                    counters.VertexCount++;
                }
            }

            int row = RadialSegments + 1;
            for (int slice = 0; slice < safeAxialSlices; slice++)
            {
                int sliceBase = baseVertex + slice * row;
                int nextSliceBase = sliceBase + row;
                for (int i = 0; i < RadialSegments; i++)
                {
                    uint a = (uint)(sliceBase + i);
                    uint b = (uint)(nextSliceBase + i);
                    uint c = (uint)(nextSliceBase + i + 1);
                    uint d = (uint)(sliceBase + i + 1);

                    Indices[counters.IndexCount++] = a;
                    Indices[counters.IndexCount++] = b;
                    Indices[counters.IndexCount++] = c;
                    Indices[counters.IndexCount++] = a;
                    Indices[counters.IndexCount++] = c;
                    Indices[counters.IndexCount++] = d;
                }
            }
        }

        private static float EstimateMaxRootDistance(FloraGenomeDTO genome)
        {
            float length = genome.SegmentLength;
            float sum = 0f;
            for (int i = 0; i < genome.RecursionDepth; i++)
            {
                sum += length;
                length *= genome.LengthFalloff;
            }

            return sum;
        }

        private static float ResolveSurfaceRadiusScale(int presetKind, float3 center, float rootDistance, int sideIndex, int ring, int branchDepth, int lodLevel)
        {
            if (presetKind == (int)FloraTopologyPreset.AbyssalBrainCoral)
            {
                float lodStrength = lodLevel == 0 ? 0.115f : (lodLevel == 1 ? 0.075f : 0.035f);
                float cellular = math.sin(center.x * 8.3f + sideIndex * 1.71f + branchDepth * 0.37f) *
                                 math.cos(center.z * 7.1f + ring * 0.93f);
                return math.max(0.72f, 1f + cellular * lodStrength);
            }

            if (presetKind == (int)FloraTopologyPreset.ThermalTubeWorm)
            {
                float lodStrength = lodLevel == 0 ? 0.075f : (lodLevel == 1 ? 0.045f : 0.018f);
                float corrugation = math.sin(rootDistance * 19.0f + sideIndex * 0.41f + branchDepth * 0.67f);
                return math.max(0.80f, 1f + corrugation * lodStrength);
            }

            return 1f;
        }

        private static float3 StablePerpendicular(float3 forward)
        {
            float3 axis = math.abs(forward.y) < 0.82f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);
            return math.normalizesafe(math.cross(axis, forward), new float3(1f, 0f, 0f));
        }

        private static void ExpandBounds(ref FloraBoundsDTO bounds, float3 point)
        {
            bounds.Min = math.min(bounds.Min, point);
            bounds.Max = math.max(bounds.Max, point);
        }

        private static byte HashToByte(uint a, uint b, uint c)
        {
            uint h = 2166136261u;
            h = (h ^ a) * 16777619u;
            h = (h ^ b) * 16777619u;
            h = (h ^ c) * 16777619u;
            return (byte)(h & 0xFFu);
        }

        private static float ResolveGlow(float3 position, float glowWeight)
        {
            float wave = math.sin(position.x * 3.17f + position.y * 2.11f + position.z * 4.03f);
            return math.saturate((0.5f + wave * 0.5f) * glowWeight);
        }

        /// <summary>
        /// Bakes vertex-colour R, the water-current sway amplitude, per
        /// 3DMODEL_FLORA_CORAL.md section 2: <c>sway = saturate(distanceFromAnchor /
        /// maxFlexibleLength) ^ stiffnessExponent</c>, with "Anchor/root = 0", "Rigid mineralized
        /// coral = 0 to 32" and "Flexible frond tips = 192 to 255". The per-family exponent and
        /// amplitude ceiling are baked here because Hecton_KelpMaster.shader consumes R directly as
        /// an amplitude and states that the generator owns the stiffness curve, so a rigid
        /// mineralized organism and a flexible frond must not leave this writer sharing one ramp.
        /// </summary>
        private static byte ResolveSwayByte(int presetKind, float leverage01)
        {
            float safeLeverage = math.saturate(math.select(0f, leverage01, math.isfinite(leverage01)));

            // AbyssalBrainCoral is mineralized skeleton. Section 2 caps its band at 32/255, so the
            // ceiling is the contract and not a taste choice.
            float stiffnessExponent = 1f;
            float amplitudeCeiling = 1f;
            if (presetKind == (int)FloraTopologyPreset.AbyssalBrainCoral)
            {
                amplitudeCeiling = 32f / 255f;
            }
            else if (presetKind == (int)FloraTopologyPreset.ThermalTubeWorm)
            {
                // Chitinous tube at the base, soft plume at the distal end: the amplitude stays
                // near zero along the tube and only the plume reaches the flexible band.
                stiffnessExponent = 2.2f;
            }

            float amplitude01 = math.saturate(math.pow(safeLeverage, stiffnessExponent) * amplitudeCeiling);
            return (byte)math.clamp((int)math.round(amplitude01 * 255f), 0, 255);
        }

        /// <summary>
        /// Vertex colour channel B is baked ambient occlusion in every family contract
        /// (3DMODEL_FLORA_CORAL.md section 2, 3dmodel.md sections 4 and 5). There is no ambient
        /// occlusion to put in B here, and that is a statement about this generator's inputs rather
        /// than a shortcut: AddRibbon and AddTube emit one ring at a time from a local start/end
        /// node pair and never see the rest of the colony, so nothing at the emission site can
        /// measure how enclosed a vertex is. Real occlusion needs either the whole branch set plus a
        /// spatial query, or a ray-traced bake.
        ///
        /// B previously carried the emissive glow value, which is a different physical quantity and
        /// arrived at the shader as inverted occlusion -- glowing tissue read as unoccluded and dark
        /// tissue as fully occluded.
        ///
        /// 255 is "fully unoccluded" and is the same missing-AO default the compliant Blender lane
        /// uses, for the reason it states in h8forge/vertexcolor.py write_organic_channels: "a
        /// darkening default would bake fake shadow into every asset whose AO bake failed". A
        /// curvature or root-distance proxy is deliberately NOT substituted, because
        /// vertexcolor.py curvature_edge_wear is explicit that a geometric estimate is honest for
        /// wear and is NOT honest for occlusion. The real value has to come from the Cycles bake in
        /// the forge lane (Tools/Blender/h8forge/vertexcolor.py bake_ambient_occlusion) or from an
        /// offline post-pass over the finished mesh.
        /// </summary>
        private const byte NoBakedOcclusion = 255;

        /// <summary>
        /// Vertex colour channel A is the family-specific mask -- "thickness, damage eligibility,
        /// harvest mask, or wetness" -- and 3DMODEL_FLORA_CORAL.md section 2 requires that the chosen
        /// meaning be written into the asset manifest. This generator authors no thickness, damage,
        /// harvest or wetness field, so the channel stays fully open rather than being filled with a
        /// an invented gradient. Named rather than a bare 255 so the channel's role is legible at the
        /// pack site and so a future real mask has one place to land.
        /// </summary>
        private const byte FamilySpecificMask = 255;

        private static uint PackColor(byte r, byte g, byte b, byte a)
        {
            return (uint)r | ((uint)g << 8) | ((uint)b << 16) | ((uint)a << 24);
        }
    }

    internal static class FloraApexIntegratorVerifier1604
    {
        private static readonly string[] ScanTargets =
        {
            "Assets/_Project/Editor/Generators/Flora",
            "Assets/_Project/Scripts/World/FloraAmbientSway",
            "Assets/_Project/Scripts/World/ProceduralCoral",
            "Assets/_Project/Scripts/World/ProceduralFamily_Flora.cs"
        };

        private static readonly string[] HotMethodNames =
        {
            "Tick",
            "FixedTick",
            "LateFrameTick",
            "Update",
            "FixedUpdate",
            "LateUpdate",
            "Execute",
            "AddRibbon",
            "AddTube",
            "TryAddNode",
            "TryAddEdge",
            "ResolveChildDirection",
            "ResolveSurfaceRadiusScale",
            "EstimateMaxRootDistance",
            "StablePerpendicular",
            "ExpandBounds",
            "HashToByte",
            "ResolveGlow",
            "PackColor",
            "PreSimulationTick",
            "PostSimulationTick",
            "VisualSyncTick"
        };

        private static readonly string[] LookupTokens =
        {
            "GlobalRegistry.Get<",
            ".GetComponent<",
            ".GetComponent(",
            "GetComponent<",
            "GetComponent("
        };

        private static readonly string[] PresentationTokens =
        {
            "Shader.SetGlobal",
            "SetGlobalConstantBuffer",
            ".LockBufferForWrite<",
            "Graphics.Draw",
            "UnityEngine.Graphics.Draw"
        };

        private static readonly string[] HotGcTokens =
        {
            "new " + "List" + "<",
            "new " + "Dictionary" + "<",
            "new " + "HashSet" + "<",
            "new " + "Queue" + "<",
            "new " + "Stack" + "<",
            "new " + "Native" + "Array" + "<",
            "new " + "Native" + "List" + "<",
            "new " + "Native" + "HashMap" + "<",
            "new " + "Native" + "ParallelHashMap" + "<",
            ".ToList(",
            ".ToArray(",
            ".Where(",
            ".Select(",
            ".Any(",
            ".FirstOrDefault(",
            "string.Format(",
            ".ToString("
        };

        private static readonly string[] HotBlockingTokens =
        {
            ".Complete(",
            "CompleteAll(",
            "WaitForCompletion(",
            "GetData("
        };

        [MenuItem("Hecton8/Authoring/Flora Topology 1604/Run Apex Integrator Verification", priority = 189)]
        public static void RunApexIntegratorVerification()
        {
            int fileCount = 0;
            int failures = 0;
            for (int i = 0; i < ScanTargets.Length; i++)
                failures += ScanTarget(ScanTargets[i], ref fileCount);

            bool compilerBusy = HasProcess("dotnet") || HasProcess("csc") || HasProcess("MSBuild");
            string throttle = compilerBusy
                ? "compiler contention detected; build intentionally not launched"
                : "no compiler process detected; build still not launched by verifier";

            if (failures == 0)
            {
                Debug.Log("[FloraTopology1604] APEX_VERIFICATION PASS. Files=" + fileCount
                    + ". HotLookup=0. HotGC=0. HotBlocking=0. PhasePresentation=0. NestedWriteLocks=0. BuildThrottle=" + throttle + ".");
                return;
            }

            Debug.LogError("[FloraTopology1604] APEX_VERIFICATION FAIL. Files=" + fileCount
                + ". Violations=" + failures + ". BuildThrottle=" + throttle + ".");
        }

        private static int ScanTarget(string target, ref int fileCount)
        {
            if (File.Exists(target))
                return ScanFile(target, ref fileCount);

            if (!Directory.Exists(target))
                return 0;

            string[] files = Directory.GetFiles(target, "*.cs", SearchOption.AllDirectories);
            int failures = 0;
            for (int i = 0; i < files.Length; i++)
                failures += ScanFile(files[i], ref fileCount);
            return failures;
        }

        private static int ScanFile(string path, ref int fileCount)
        {
            fileCount++;
            string text = File.ReadAllText(path);
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            string[] scanLines = new string[lines.Length];
            bool inBlockComment = false;
            for (int i = 0; i < lines.Length; i++)
                scanLines[i] = StripLineForScanning(lines[i], ref inBlockComment);

            int failures = 0;
            int braceDepth = 0;
            int methodDepth = -1;
            int activeWriteLocks = 0;
            string pendingMethod = null;
            string activeMethod = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string scanLine = scanLines[i];
                string trimmed = scanLine.Trim();
                if (activeMethod == null)
                {
                    string candidate = TryResolveMethodName(trimmed);
                    if (candidate != null)
                        pendingMethod = candidate;
                }

                int opens = CountChar(scanLine, '{');
                int closes = CountChar(scanLine, '}');
                if (activeMethod == null && pendingMethod != null && opens > 0)
                {
                    activeMethod = pendingMethod;
                    methodDepth = braceDepth + opens - closes;
                    activeWriteLocks = 0;
                    pendingMethod = null;
                }

                if (activeMethod != null && IsHotMethod(activeMethod))
                {
                    failures += ScanHotLookup(path, i + 1, activeMethod, scanLine);
                    failures += ScanHotGc(path, i + 1, activeMethod, scanLine);
                    failures += ScanHotBlocking(path, i + 1, activeMethod, scanLine);
                    failures += ScanPresentationPhase(path, i + 1, activeMethod, scanLine);
                }

                if (activeMethod != null)
                    failures += ScanWriteLockFlattening(path, i + 1, scanLines, i, scanLine, ref activeWriteLocks);

                braceDepth += opens - closes;
                if (activeMethod != null && braceDepth < methodDepth)
                {
                    activeMethod = null;
                    methodDepth = -1;
                    activeWriteLocks = 0;
                }
            }

            return failures;
        }

        private static string StripLineForScanning(string line, ref bool inBlockComment)
        {
            if (string.IsNullOrEmpty(line))
                return string.Empty;

            char[] chars = line.ToCharArray();
            bool inString = false;
            bool inChar = false;
            bool verbatimString = false;
            for (int i = 0; i < chars.Length; i++)
            {
                char current = chars[i];
                if (inBlockComment)
                {
                    if (current == '*' && i + 1 < chars.Length && chars[i + 1] == '/')
                    {
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i++;
                        inBlockComment = false;
                        continue;
                    }

                    chars[i] = ' ';
                    continue;
                }

                if (inString)
                {
                    if (verbatimString && current == '"' && i + 1 < chars.Length && chars[i + 1] == '"')
                    {
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                        i++;
                        continue;
                    }

                    bool endsString = current == '"' && (verbatimString || !IsEscaped(chars, i));
                    chars[i] = ' ';
                    if (endsString)
                    {
                        inString = false;
                        verbatimString = false;
                    }

                    continue;
                }

                if (inChar)
                {
                    bool endsChar = current == '\'' && !IsEscaped(chars, i);
                    chars[i] = ' ';
                    if (endsChar)
                        inChar = false;
                    continue;
                }

                if (current == '/' && i + 1 < chars.Length && chars[i + 1] == '/')
                {
                    for (int j = i; j < chars.Length; j++)
                        chars[j] = ' ';
                    break;
                }

                if (current == '/' && i + 1 < chars.Length && chars[i + 1] == '*')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    inBlockComment = true;
                    continue;
                }

                if (current == '$' && i + 2 < chars.Length && chars[i + 1] == '@' && chars[i + 2] == '"')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    chars[i + 2] = ' ';
                    i += 2;
                    inString = true;
                    verbatimString = true;
                    continue;
                }

                if (current == '@' && i + 2 < chars.Length && chars[i + 1] == '$' && chars[i + 2] == '"')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    chars[i + 2] = ' ';
                    i += 2;
                    inString = true;
                    verbatimString = true;
                    continue;
                }

                if (current == '@' && i + 1 < chars.Length && chars[i + 1] == '"')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    inString = true;
                    verbatimString = true;
                    continue;
                }

                if (current == '"')
                {
                    chars[i] = ' ';
                    inString = true;
                    verbatimString = false;
                    continue;
                }

                if (current == '\'')
                {
                    chars[i] = ' ';
                    inChar = true;
                }
            }

            return new string(chars);
        }

        private static bool IsEscaped(char[] chars, int quoteIndex)
        {
            int slashCount = 0;
            for (int i = quoteIndex - 1; i >= 0 && chars[i] == '\\'; i--)
                slashCount++;
            return (slashCount & 1) != 0;
        }

        private static int ScanHotLookup(string path, int lineNumber, string methodName, string line)
        {
            for (int i = 0; i < LookupTokens.Length; i++)
            {
                if (line.Contains(LookupTokens[i]) && !line.Contains("TryRegister"))
                {
                    Debug.LogError("[FloraTopology1604] Hot lookup violation " + path + ":" + lineNumber + " in " + methodName);
                    return 1;
                }
            }

            return 0;
        }

        private static int ScanPresentationPhase(string path, int lineNumber, string methodName, string line)
        {
            for (int i = 0; i < PresentationTokens.Length; i++)
            {
                if (line.Contains(PresentationTokens[i]) &&
                    methodName != "VisualSyncTick" &&
                    methodName != "LateFrameTick")
                {
                    Debug.LogError("[FloraTopology1604] Presentation phase violation " + path + ":" + lineNumber + " in " + methodName);
                    return 1;
                }
            }

            return 0;
        }

        private static int ScanHotGc(string path, int lineNumber, string methodName, string line)
        {
            for (int i = 0; i < HotGcTokens.Length; i++)
            {
                if (line.Contains(HotGcTokens[i]))
                {
                    Debug.LogError("[FloraTopology1604] Hot zero-GC violation " + path + ":" + lineNumber + " in " + methodName);
                    return 1;
                }
            }

            return 0;
        }

        private static int ScanHotBlocking(string path, int lineNumber, string methodName, string line)
        {
            for (int i = 0; i < HotBlockingTokens.Length; i++)
            {
                if (line.Contains(HotBlockingTokens[i]))
                {
                    Debug.LogError("[FloraTopology1604] Hot blocking call violation " + path + ":" + lineNumber + " in " + methodName);
                    return 1;
                }
            }

            return 0;
        }

        private static int ScanWriteLockFlattening(
            string path,
            int lineNumber,
            string[] lines,
            int lineIndex,
            string line,
            ref int activeWriteLocks)
        {
            int failures = 0;
            if (line.Contains("TryAcquireWrite(") || line.Contains("TryAcquireWriteLock"))
            {
                activeWriteLocks++;
                if (activeWriteLocks > 1)
                {
                    Debug.LogError("[FloraTopology1604] Nested DataVault write lock risk " + path + ":" + lineNumber);
                    failures++;
                }

                if (!ContainsWithin(lines, lineIndex, 24, "try") ||
                    !ContainsWithin(lines, lineIndex, 96, "finally") ||
                    !ContainsWithin(lines, lineIndex, 96, "ReleaseWriteLock"))
                {
                    Debug.LogError("[FloraTopology1604] DataVault write lock lacks strict try/finally release " + path + ":" + lineNumber);
                    failures++;
                }
            }

            if (line.Contains("ReleaseWriteLock") && activeWriteLocks > 0)
                activeWriteLocks--;

            return failures;
        }

        private static string TryResolveMethodName(string trimmed)
        {
            int paren = trimmed.IndexOf('(');
            if (paren <= 0 || trimmed.StartsWith("if ", StringComparison.Ordinal) ||
                trimmed.StartsWith("if(", StringComparison.Ordinal) ||
                trimmed.StartsWith("for ", StringComparison.Ordinal) ||
                trimmed.StartsWith("for(", StringComparison.Ordinal) ||
                trimmed.StartsWith("while ", StringComparison.Ordinal) ||
                trimmed.StartsWith("while(", StringComparison.Ordinal) ||
                trimmed.StartsWith("switch ", StringComparison.Ordinal) ||
                trimmed.StartsWith("catch ", StringComparison.Ordinal) ||
                trimmed.StartsWith("using ", StringComparison.Ordinal) ||
                trimmed.StartsWith("return ", StringComparison.Ordinal))
            {
                return null;
            }

            int end = paren - 1;
            while (end >= 0 && char.IsWhiteSpace(trimmed[end]))
                end--;
            if (end < 0)
                return null;

            int start = end;
            while (start >= 0 && (char.IsLetterOrDigit(trimmed[start]) || trimmed[start] == '_'))
                start--;
            start++;
            if (start > end)
                return null;

            string name = trimmed.Substring(start, end - start + 1);
            return string.IsNullOrEmpty(name) ? null : name;
        }

        private static bool IsHotMethod(string methodName)
        {
            for (int i = 0; i < HotMethodNames.Length; i++)
            {
                if (methodName == HotMethodNames[i])
                    return true;
            }

            return false;
        }

        private static bool ContainsWithin(string[] lines, int startIndex, int lineCount, string token)
        {
            int end = Math.Min(lines.Length, startIndex + lineCount);
            for (int i = startIndex; i < end; i++)
            {
                if (lines[i].Contains(token))
                    return true;
            }

            return false;
        }

        private static int CountChar(string value, char target)
        {
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == target)
                    count++;
            }

            return count;
        }

        private static bool HasProcess(string processName)
        {
            Process[] processes = Process.GetProcessesByName(processName);
            try
            {
                return processes.Length > 0;
            }
            finally
            {
                for (int i = 0; i < processes.Length; i++)
                    processes[i].Dispose();
            }
        }
    }

    internal sealed class FloraTopologyStudioWindow1604 : EditorWindow
    {
        private FloraTopologyPreset _preset = FloraTopologyPreset.KelpForestFrond;
        private int _seed = 16040042;
        private float _globalQualityWeight = 0.75f;
        private int _lod0TriangleBudget = 18000;
        private string _lastMetrics = "No generation run in this editor session.";
        private HelpBox _metricsBox;

        public static void Open()
        {
            FloraTopologyStudioWindow1604 window = GetWindow<FloraTopologyStudioWindow1604>("Flora Topology Studio");
            window.minSize = new Vector2(360f, 220f);
            window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 8f;
            rootVisualElement.style.paddingRight = 8f;
            rootVisualElement.style.paddingTop = 8f;

            Label title = new Label("Flora Topology Studio 1604");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            rootVisualElement.Add(title);

            EnumField presetField = new EnumField("Preset", _preset);
            presetField.RegisterValueChangedCallback(evt => _preset = (FloraTopologyPreset)evt.newValue);
            rootVisualElement.Add(presetField);

            IntegerField seedField = new IntegerField("Seed");
            seedField.value = _seed;
            seedField.RegisterValueChangedCallback(evt => _seed = Math.Max(1, evt.newValue));
            rootVisualElement.Add(seedField);

            Slider qualitySlider = new Slider("GlobalQualityWeight", 0f, 1f);
            qualitySlider.value = _globalQualityWeight;
            qualitySlider.RegisterValueChangedCallback(evt => _globalQualityWeight = math.saturate(evt.newValue));
            rootVisualElement.Add(qualitySlider);

            SliderInt budgetSlider = new SliderInt("LOD0 Triangle Budget", 192, 80000);
            budgetSlider.value = _lod0TriangleBudget;
            budgetSlider.RegisterValueChangedCallback(evt => _lod0TriangleBudget = math.clamp(evt.newValue, 192, 80000));
            rootVisualElement.Add(budgetSlider);

            VisualElement buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.marginTop = 6f;
            rootVisualElement.Add(buttonRow);

            Button generateButton = new Button(Generate) { text = "Generate Prefab" };
            generateButton.style.flexGrow = 1f;
            buttonRow.Add(generateButton);

            Button determinismButton = new Button(FloraTopologyGenerator1604.RunDeterminismSelfTest) { text = "Determinism Test" };
            determinismButton.style.flexGrow = 1f;
            buttonRow.Add(determinismButton);

            Button verifierButton = new Button(FloraApexIntegratorVerifier1604.RunApexIntegratorVerification) { text = "Apex Verify" };
            verifierButton.style.flexGrow = 1f;
            buttonRow.Add(verifierButton);

            _metricsBox = new HelpBox(_lastMetrics, HelpBoxMessageType.Info);
            _metricsBox.style.marginTop = 8f;
            rootVisualElement.Add(_metricsBox);
        }

        private void Generate()
        {
            uint seed = (uint)math.max(1, _seed);
            bool generated = FloraTopologyGenerator1604.GenerateAndSave(_preset, seed, _globalQualityWeight, _lod0TriangleBudget);
            _lastMetrics = generated
                ? "Generated " + _preset + " seed " + seed.ToString("X8") + ". Prefab and LOD mesh assets written under Topology1604."
                : "Generation failed. Check Unity Console for fail-closed reason.";
            if (_metricsBox != null)
                _metricsBox.text = _lastMetrics;
        }
    }
}
#endif
