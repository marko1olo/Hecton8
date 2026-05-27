using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Hecton8.World.OfflineHadalArchBaker;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World.OfflineHadalArchBaker.Editor
{
    public struct HadalArchBakeResult
    {
        public string Lod0Path;
        public string Lod1Path;
        public string Lod2Path;
        public string PrefabPath;
        public int Lod0Triangles;
        public int Lod1Triangles;
        public int Lod2Triangles;
        public uint WarningFlags;
        public float SdfMilliseconds;
        public float ExtractionMilliseconds;
    }

    public static class HadalArchBakePipeline
    {
        private const string BakedMeshFolder = "Assets/_Project/BakedGeometry/HadalStructures";
        private const string DefaultMaterialPath = "Assets/_Project/Art/Materials/Mat_TriplanarRock.mat";
        private const string ReportPath = "Docs/Reports/HADAL_BAKE_REPORT.json";
        private const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_215.bin";

        public static bool BakeAsync(
            string assetName,
            SdfShapeDTO[] managedShapes,
            HadalArchBakeConfigDTO config,
            Material material,
            bool createPrefab,
            Action<HadalArchBakeResult> onCompleted,
            Action<Exception> onFailed)
        {
            try
            {
                HadalArchBakeResult result = Bake(assetName, managedShapes, config, material, createPrefab);
                onCompleted?.Invoke(result);
                return true;
            }
            catch (Exception ex)
            {
                onFailed?.Invoke(ex);
                return false;
            }
        }

        public static HadalArchBakeResult Bake(
            string assetName,
            SdfShapeDTO[] managedShapes,
            HadalArchBakeConfigDTO config,
            Material material,
            bool createPrefab)
        {
            string safeName = SanitizeAssetName(assetName);
            Directory.CreateDirectory(BakedMeshFolder);
            Directory.CreateDirectory("Docs/Reports");
            Directory.CreateDirectory("Docs/AgentLogs");

            config = SanitizeConfig(config, managedShapes != null ? managedShapes.Length : 0);
            int voxelCount = config.Resolution.x * config.Resolution.y * config.Resolution.z;
            int cellCount = math.max(1, (config.Resolution.x - 1) * (config.Resolution.y - 1) * (config.Resolution.z - 1));
            int vertexCapacity = ResolveVertexCapacity(cellCount, ref config);
            uint warningFlags = vertexCapacity < cellCount * 9 ? HadalArchBakeConstants.WarningCapacityClamp : 0u;
            Stopwatch stopwatch = Stopwatch.StartNew();
            float sdfMs = 0f;
            float extractionMs = 0f;

            NativeArray<float> densities = default;
            NativeArray<byte> cavity = default;
            NativeArray<SdfShapeDTO> shapes = default;
            NativeList<HadalArchVertexDTO> lod0Vertices = default;
            NativeList<int> lod0Indices = default;
            NativeList<HadalArchVertexDTO> lod1Vertices = default;
            NativeList<int> lod1Indices = default;
            NativeList<HadalArchVertexDTO> lod2Vertices = default;
            NativeList<int> lod2Indices = default;
            NativeList<HadalArchVertexDTO> weldedVertices = default;
            NativeList<int> weldedIndices = default;
            NativeParallelHashMap<ulong, int> weldMap = default;
            NativeArray<HadalArchBakeTelemetryEntry> telemetry = default;

            try
            {
                densities = new NativeArray<float>(voxelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                cavity = new NativeArray<byte>(voxelCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                telemetry = new NativeArray<HadalArchBakeTelemetryEntry>(HadalArchBakeConstants.TelemetryFrames, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                shapes = new NativeArray<SdfShapeDTO>(math.max(config.ShapeCount, 1), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < config.ShapeCount; i++)
                    shapes[i] = managedShapes[i];

                lod0Vertices = new NativeList<HadalArchVertexDTO>(vertexCapacity, Allocator.TempJob);
                lod0Indices = new NativeList<int>(vertexCapacity, Allocator.TempJob);
                lod1Vertices = new NativeList<HadalArchVertexDTO>(math.max(3, (int)(vertexCapacity * math.saturate(config.Lod1KeepRatio))), Allocator.TempJob);
                lod1Indices = new NativeList<int>(lod1Vertices.Capacity, Allocator.TempJob);
                lod2Vertices = new NativeList<HadalArchVertexDTO>(math.max(3, (int)(vertexCapacity * math.saturate(config.Lod2KeepRatio))), Allocator.TempJob);
                lod2Indices = new NativeList<int>(lod2Vertices.Capacity, Allocator.TempJob);

                EditorUtility.DisplayProgressBar("Hadal Structure Forge", "Evaluating SDF boolean graph", 0.15f);
                Stopwatch stage = Stopwatch.StartNew();
                JobHandle sdfHandle = config.ShapeCount > 0
                    ? new EvaluateSdfBooleanGraphJob { Densities = densities, Shapes = shapes, Config = config }.Schedule(voxelCount, 64)
                    : new GenerateMockSdfVolumeJob { Densities = densities, Config = config }.Schedule(voxelCount, 64);
                JobHandle noiseHandle = new ApplySdfNoiseDisplacementJob { Densities = densities, Config = config }.Schedule(voxelCount, 64, sdfHandle);
                JobHandle sealHandle = new SealSdfBoundaryShellJob { Densities = densities, Config = config }.Schedule(voxelCount, 64, noiseHandle);
                sealHandle.Complete();
                sdfMs = (float)stage.Elapsed.TotalMilliseconds;
                warningFlags |= HadalArchBakeConstants.WarningBoundaryShellSealed;

                EditorUtility.DisplayProgressBar("Hadal Structure Forge", "Baking Dear-Lie cavity occlusion", 0.38f);
                JobHandle cavityHandle = new BakeCavityOcclusionJob { Densities = densities, CavityVisibility = cavity, Config = config }.Schedule(voxelCount, 64);
                cavityHandle.Complete();

                EditorUtility.DisplayProgressBar("Hadal Structure Forge", "Extracting outer shell", 0.55f);
                stage.Restart();
                new ExtractArchMeshJob
                {
                    Densities = densities,
                    CavityVisibility = cavity,
                    Vertices = lod0Vertices,
                    Indices = lod0Indices,
                    Config = config
                }.Schedule().Complete();
                extractionMs = (float)stage.Elapsed.TotalMilliseconds;

                EditorUtility.DisplayProgressBar("Hadal Structure Forge", "Welding duplicate shell vertices", 0.64f);
                weldedVertices = new NativeList<HadalArchVertexDTO>(math.max(3, lod0Vertices.Length), Allocator.TempJob);
                weldedIndices = new NativeList<int>(math.max(3, lod0Indices.Length), Allocator.TempJob);
                weldMap = new NativeParallelHashMap<ulong, int>(math.max(1, lod0Vertices.Length), Allocator.TempJob);
                new WeldArchMeshJob
                {
                    SourceVertices = lod0Vertices.AsArray(),
                    SourceIndices = lod0Indices.AsArray(),
                    OutputVertices = weldedVertices,
                    OutputIndices = weldedIndices,
                    VertexLookup = weldMap,
                    Config = config
                }.Schedule().Complete();
                lod0Vertices.Dispose();
                lod0Indices.Dispose();
                lod0Vertices = weldedVertices;
                lod0Indices = weldedIndices;
                weldedVertices = default;
                weldedIndices = default;
                weldMap.Dispose();

                EditorUtility.DisplayProgressBar("Hadal Structure Forge", "Decimating deterministic LODs", 0.72f);
                new DeterministicLodDecimationJob
                {
                    SourceVertices = lod0Vertices.AsArray(),
                    SourceIndices = lod0Indices.AsArray(),
                    OutputVertices = lod1Vertices,
                    OutputIndices = lod1Indices,
                    KeepRatio = config.Lod1KeepRatio,
                    CollapseWeight = 0.06f,
                    Seed = config.Seed ^ 0x4C4F4431u
                }.Schedule().Complete();
                new DeterministicLodDecimationJob
                {
                    SourceVertices = lod0Vertices.AsArray(),
                    SourceIndices = lod0Indices.AsArray(),
                    OutputVertices = lod2Vertices,
                    OutputIndices = lod2Indices,
                    KeepRatio = config.Lod2KeepRatio,
                    CollapseWeight = 0.18f,
                    Seed = config.Seed ^ 0x4C4F4432u
                }.Schedule().Complete();

                if (lod0Indices.Length / 3 > HadalArchBakeConstants.CriticalLod0TriangleBudget)
                    warningFlags |= HadalArchBakeConstants.WarningTriangleBudgetExceeded;

                telemetry[0] = BuildTelemetry(config, voxelCount, lod0Vertices.Length, lod0Indices.Length, sdfMs, extractionMs, warningFlags, 4u);

                EditorUtility.DisplayProgressBar("Hadal Structure Forge", "Serializing mesh assets", 0.88f);
                string lod0Path = CreateMeshAsset(safeName + "_LOD0", lod0Vertices.AsArray(), lod0Indices.AsArray());
                string lod1Path = CreateMeshAsset(safeName + "_LOD1", lod1Vertices.AsArray(), lod1Indices.AsArray());
                string lod2Path = CreateMeshAsset(safeName + "_LOD2", lod2Vertices.AsArray(), lod2Indices.AsArray());
                string prefabPath = createPrefab ? CreatePrefab(safeName, lod0Path, lod1Path, lod2Path, material) : string.Empty;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                HadalArchBakeResult result = new HadalArchBakeResult
                {
                    Lod0Path = lod0Path,
                    Lod1Path = lod1Path,
                    Lod2Path = lod2Path,
                    PrefabPath = prefabPath,
                    Lod0Triangles = lod0Indices.Length / 3,
                    Lod1Triangles = lod1Indices.Length / 3,
                    Lod2Triangles = lod2Indices.Length / 3,
                    WarningFlags = warningFlags,
                    SdfMilliseconds = sdfMs,
                    ExtractionMilliseconds = extractionMs
                };
                WriteReport(in result, in config, managedShapes != null ? managedShapes.Length : 0, stopwatch.Elapsed.TotalMilliseconds);
                HadalArchSelfAudit.WriteAudit(in result, in config, managedShapes != null ? managedShapes.Length : 0);
                return result;
            }
            catch
            {
                if (telemetry.IsCreated)
                    DumpBlackBox(telemetry);
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (densities.IsCreated) densities.Dispose();
                if (cavity.IsCreated) cavity.Dispose();
                if (shapes.IsCreated) shapes.Dispose();
                if (lod0Vertices.IsCreated) lod0Vertices.Dispose();
                if (lod0Indices.IsCreated) lod0Indices.Dispose();
                if (lod1Vertices.IsCreated) lod1Vertices.Dispose();
                if (lod1Indices.IsCreated) lod1Indices.Dispose();
                if (lod2Vertices.IsCreated) lod2Vertices.Dispose();
                if (lod2Indices.IsCreated) lod2Indices.Dispose();
                if (weldedVertices.IsCreated) weldedVertices.Dispose();
                if (weldedIndices.IsCreated) weldedIndices.Dispose();
                if (weldMap.IsCreated) weldMap.Dispose();
                if (telemetry.IsCreated) telemetry.Dispose();
            }
        }

        public static Material ResolveDefaultMaterial()
        {
            return AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialPath);
        }

        private static HadalArchBakeConfigDTO SanitizeConfig(HadalArchBakeConfigDTO config, int shapeCount)
        {
            int resolution = math.clamp(config.Resolution.x <= 0 ? HadalArchBakeConstants.DefaultResolution : config.Resolution.x, 16, 128);
            config.Resolution = new int3(resolution);
            config.VoxelSize = math.max(config.VoxelSize, 0.05f);
            config.GlobalQualityWeight = math.saturate(config.GlobalQualityWeight);
            config.NoiseFrequency = math.max(config.NoiseFrequency, 0.001f);
            config.NoiseAmplitude = math.max(config.NoiseAmplitude, 0f);
            config.CavityRayDistance = math.max(config.CavityRayDistance, config.VoxelSize);
            config.CavityRayCount = math.clamp(config.CavityRayCount, 1, 12);
            config.ShapeCount = math.clamp(shapeCount, 0, HadalArchBakeConstants.MaxPreviewShapes);
            config.Lod1KeepRatio = math.clamp(config.Lod1KeepRatio <= 0f ? 0.5f : config.Lod1KeepRatio, 0.05f, 1f);
            config.Lod2KeepRatio = math.clamp(config.Lod2KeepRatio <= 0f ? 0.1f : config.Lod2KeepRatio, 0.02f, config.Lod1KeepRatio);
            config.SurfaceBand = math.max(config.SurfaceBand, config.VoxelSize * 4f);
            config.Seed = config.Seed == 0u ? HadalArchBakeMath.HashFnv1a(config.CenterAup) : config.Seed;
            config.NoiseSeedJitter = HadalArchBakeMath.BuildNoiseSeedJitter(config.Seed);
            return config;
        }

        private static int ResolveVertexCapacity(int cellCount, ref HadalArchBakeConfigDTO config)
        {
            float q = math.saturate(config.GlobalQualityWeight);
            int requested = (int)math.min(2400000L, (long)cellCount * (long)math.round(math.lerp(3f, 9f, q)));
            return math.max(4096, requested);
        }

        private static string CreateMeshAsset(string meshName, NativeArray<HadalArchVertexDTO> vertices, NativeArray<int> indices)
        {
            Mesh mesh = new Mesh
            {
                name = meshName,
                indexFormat = IndexFormat.UInt32
            };

            mesh.SetVertexBufferParams(vertices.Length, VertexLayout());
            if (vertices.Length > 0)
            {
                mesh.SetVertexBufferData(vertices, 0, 0, vertices.Length, 0,
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            }

            mesh.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
            if (indices.Length > 0)
            {
                mesh.SetIndexBufferData(indices, 0, 0, indices.Length,
                    MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            }

            mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length, MeshTopology.Triangles),
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);

            string path = AssetDatabase.GenerateUniqueAssetPath(BakedMeshFolder + "/" + meshName + ".asset");
            AssetDatabase.CreateAsset(mesh, path);
            return path;
        }

        private static VertexAttributeDescriptor[] VertexLayout()
        {
            return new[]
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0),
                new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4, 0),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 0),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, 0),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord3, VertexAttributeFormat.Float32, 3, 0)
            };
        }

        private static string CreatePrefab(string assetName, string lod0Path, string lod1Path, string lod2Path, Material material)
        {
            Mesh lod0 = AssetDatabase.LoadAssetAtPath<Mesh>(lod0Path);
            Mesh lod1 = AssetDatabase.LoadAssetAtPath<Mesh>(lod1Path);
            Mesh lod2 = AssetDatabase.LoadAssetAtPath<Mesh>(lod2Path);
            Material resolvedMaterial = material != null ? material : ResolveDefaultMaterial();
            GameObject root = new GameObject("GEN_" + assetName);
            try
            {
                Renderer r0 = CreateLodChild(root.transform, "LOD0", lod0, resolvedMaterial);
                Renderer r1 = CreateLodChild(root.transform, "LOD1", lod1, resolvedMaterial);
                Renderer r2 = CreateLodChild(root.transform, "LOD2", lod2, resolvedMaterial);
                LODGroup lodGroup = root.AddComponent<LODGroup>();
                lodGroup.fadeMode = LODFadeMode.CrossFade;
                lodGroup.animateCrossFading = false;
                lodGroup.SetLODs(new[]
                {
                    new LOD(0.55f, new[] { r0 }),
                    new LOD(0.18f, new[] { r1 }),
                    new LOD(0.04f, new[] { r2 })
                });
                lodGroup.RecalculateBounds();
                MeshCollider collider = root.AddComponent<MeshCollider>();
                collider.sharedMesh = lod2 != null ? lod2 : lod1 != null ? lod1 : lod0;
                collider.convex = false;
                GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
                string prefabPath = AssetDatabase.GenerateUniqueAssetPath(BakedMeshFolder + "/GEN_" + assetName + ".prefab");
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return prefabPath;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Renderer CreateLodChild(Transform root, string name, Mesh mesh, Material material)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(root, false);
            MeshFilter filter = child.AddComponent<MeshFilter>();
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(child, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
            return renderer;
        }

        private static HadalArchBakeTelemetryEntry BuildTelemetry(
            HadalArchBakeConfigDTO config,
            int voxelCount,
            int vertexCount,
            int indexCount,
            float sdfMs,
            float extractionMs,
            uint warningFlags,
            uint stage)
        {
            return new HadalArchBakeTelemetryEntry
            {
                CenterAup = config.CenterAup,
                Frame = 0u,
                VoxelCount = voxelCount,
                VertexCount = vertexCount,
                IndexCount = indexCount,
                SdfMilliseconds = sdfMs,
                ExtractionMilliseconds = extractionMs,
                WarningFlags = warningFlags,
                StateHash = HadalArchBakeMath.Mix((uint)vertexCount ^ ((uint)indexCount << 1) ^ config.Seed),
                DumpReason = 0u,
                Stage = stage
            };
        }

        private static void WriteReport(in HadalArchBakeResult result, in HadalArchBakeConfigDTO config, int shapeCount, double totalMs)
        {
            StringBuilder builder = new StringBuilder(1024);
            builder.Append("{\n");
            builder.Append("  \"version\": ").Append(HadalArchBakeConstants.ReportVersion).Append(",\n");
            builder.Append("  \"agent\": \"SHINOBU_215\",\n");
            builder.Append("  \"resolution\": [").Append(config.Resolution.x).Append(", ").Append(config.Resolution.y).Append(", ").Append(config.Resolution.z).Append("],\n");
            builder.Append("  \"voxelSize\": ").Append(config.VoxelSize.ToString("0.####", CultureInfo.InvariantCulture)).Append(",\n");
            builder.Append("  \"shapeOperations\": ").Append(shapeCount).Append(",\n");
            builder.Append("  \"lodTriangles\": { \"lod0\": ").Append(result.Lod0Triangles).Append(", \"lod1\": ").Append(result.Lod1Triangles).Append(", \"lod2\": ").Append(result.Lod2Triangles).Append(" },\n");
            builder.Append("  \"timingsMs\": { \"sdf\": ").Append(result.SdfMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)).Append(", \"extract\": ").Append(result.ExtractionMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)).Append(", \"total\": ").Append(totalMs.ToString("0.###", CultureInfo.InvariantCulture)).Append(" },\n");
            builder.Append("  \"warningFlags\": ").Append(result.WarningFlags).Append(",\n");
            builder.Append("  \"boundaryShellSealed\": ").Append((result.WarningFlags & HadalArchBakeConstants.WarningBoundaryShellSealed) != 0u ? "true" : "false").Append(",\n");
            builder.Append("  \"criticalWarning\": ").Append(result.Lod0Triangles > HadalArchBakeConstants.CriticalLod0TriangleBudget ? "true" : "false").Append(",\n");
            builder.Append("  \"rollbackExcluded\": true,\n");
            builder.Append("  \"assets\": { \"lod0\": \"").Append(result.Lod0Path).Append("\", \"lod1\": \"").Append(result.Lod1Path).Append("\", \"lod2\": \"").Append(result.Lod2Path).Append("\", \"prefab\": \"").Append(result.PrefabPath).Append("\" }\n");
            builder.Append("}\n");
            File.WriteAllText(ReportPath, builder.ToString(), new UTF8Encoding(false));
        }

        private static void DumpBlackBox(NativeArray<HadalArchBakeTelemetryEntry> telemetry)
        {
            using (FileStream stream = new FileStream(DumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(HadalArchBakeConstants.DumpMagic);
                writer.Write(telemetry.Length);
                for (int i = 0; i < telemetry.Length; i++)
                {
                    HadalArchBakeTelemetryEntry entry = telemetry[i];
                    writer.Write(entry.CenterAup.x);
                    writer.Write(entry.CenterAup.y);
                    writer.Write(entry.CenterAup.z);
                    writer.Write(entry.Frame);
                    writer.Write(entry.VoxelCount);
                    writer.Write(entry.VertexCount);
                    writer.Write(entry.IndexCount);
                    writer.Write(entry.SdfMilliseconds);
                    writer.Write(entry.ExtractionMilliseconds);
                    writer.Write(entry.WarningFlags);
                    writer.Write(entry.StateHash);
                    writer.Write(entry.DumpReason);
                    writer.Write(entry.Stage);
                }
            }
        }

        private static string SanitizeAssetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Hadal_Arch";

            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                builder.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            }

            return builder.ToString();
        }

    }
}
