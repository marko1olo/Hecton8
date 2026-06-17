#if UNITY_EDITOR
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Hecton8.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Hecton8.Editor.AITextureControlMaps
{
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal unsafe struct GenerateMockComplexMeshJob : IJobParallelFor
    {
        [NoAlias] [NativeDisableUnsafePtrRestriction] public void* VertexPtr;
        public MockComplexMeshConfigDTO Config;
        public int VertexCount;

        public void Execute(int quadIndex)
        {
            if (VertexPtr == null || VertexCount <= 0)
                return;

            MockComplexMeshConfigDTO config = Config;
            int ringSegments = math.max(3, config.RingSegments);
            int tubeSegments = math.max(3, config.TubeSegments);
            int ring = quadIndex / tubeSegments;
            int tube = quadIndex - ring * tubeSegments;
            float u0 = ring * math.rcp(ringSegments);
            float u1 = (ring + 1) * math.rcp(ringSegments);
            float v0 = tube * math.rcp(tubeSegments);
            float v1 = (tube + 1) * math.rcp(tubeSegments);
            int dst = quadIndex * 6;

            Write(dst, SampleKnot(u0, v0, config), new float2(u0, v0));
            Write(dst + 1, SampleKnot(u0, v1, config), new float2(u0, v1));
            Write(dst + 2, SampleKnot(u1, v0, config), new float2(u1, v0));
            Write(dst + 3, SampleKnot(u1, v0, config), new float2(u1, v0));
            Write(dst + 4, SampleKnot(u0, v1, config), new float2(u0, v1));
            Write(dst + 5, SampleKnot(u1, v1, config), new float2(u1, v1));
        }

        private float3 SampleKnot(float u, float v, MockComplexMeshConfigDTO config)
        {
            float tau = 6.28318530718f;
            float t = u * tau;
            float twist = math.max(0.1f, config.Twist);
            float t2 = t * 2.0f;
            float t3 = t * 3.0f;
            float t3Twist = t3 + twist;
            MathLodApproximation.ApproxSinCosBhaskara(t2, out float sinT2, out float cosT2);
            MathLodApproximation.ApproxSinCosBhaskara(t3, out float sinT3, out float cosT3);
            float sinT3Twist = MathLodApproximation.ApproxSinBhaskara(t3Twist);
            float radial = config.MajorRadius + 0.28f * MathLodApproximation.ApproxCosBhaskara(t3Twist);
            float3 center = new float3(
                radial * cosT2,
                0.42f * sinT3,
                radial * sinT2);

            float3 tangent = Normalize(new float3(
                -2.0f * radial * sinT2 - 0.84f * sinT3Twist * cosT2,
                1.26f * cosT3,
                2.0f * radial * cosT2 - 0.84f * sinT3Twist * sinT2));
            float3 radialAxis = Normalize(new float3(cosT2, 0.0f, sinT2));
            float3 binormal = Normalize(math.cross(tangent, radialAxis));
            float3 normalAxis = Normalize(math.cross(binormal, tangent));
            float p = v * tau;
            float ridged = 1.0f - math.abs(HashSignedNoise(u * 17.0f + v * 31.0f, config.Seed));
            float wave = MathLodApproximation.ApproxSinBhaskara(t * 7.0f + p * 5.0f + (config.Seed & 15u));
            float radius = math.max(0.02f, config.TubeRadius * (1.0f + config.Irregularity * (ridged * 0.38f + wave * 0.12f)));
            MathLodApproximation.ApproxSinCosBhaskara(p, out float sinP, out float cosP);
            float3 shell = normalAxis * cosP + binormal * sinP;
            return center + shell * radius;
        }

        private void Write(int index, float3 position, float2 uv)
        {
            if ((uint)index >= (uint)VertexCount)
                return;

            float3 normal = Normalize(position - ApproxCenter(position));
            AITextureBakeVertex vertex;
            vertex.Position = math.all(math.isfinite(position)) ? position : float3.zero;
            vertex.Normal = math.all(math.isfinite(normal)) ? normal : new float3(0.0f, 1.0f, 0.0f);
            vertex.Uv0 = math.all(math.isfinite(uv)) ? uv : float2.zero;
            byte* ptr = (byte*)VertexPtr + index * UnsafeUtility.SizeOf<AITextureBakeVertex>();
            UnsafeUtility.AsRef<AITextureBakeVertex>(ptr) = vertex;
        }

        private static float3 ApproxCenter(float3 position)
        {
            float len = math.length(new float2(position.x, position.z));
            float3 radial = len > 1e-5f ? new float3(position.x / len, 0.0f, position.z / len) : new float3(1.0f, 0.0f, 0.0f);
            return radial * math.max(0.1f, len - 0.15f);
        }

        private static float HashSignedNoise(float value, uint seed)
        {
            uint bits = math.asuint(value) ^ seed * 747796405u;
            bits ^= bits >> 16;
            bits *= 2246822519u;
            bits ^= bits >> 13;
            return ((bits & 0xFFFFu) * (2.0f / 65535.0f)) - 1.0f;
        }

        private static float3 Normalize(float3 value)
        {
            float lenSq = math.lengthsq(value);
            return math.isfinite(lenSq) && lenSq > 1e-12f
                ? value * math.rsqrt(math.max(lenSq, 1e-12f))
                : new float3(0.0f, 1.0f, 0.0f);
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    internal unsafe struct FillUInt32IndexJob : IJobParallelFor
    {
        [NoAlias] [NativeDisableUnsafePtrRestriction] public void* IndexPtr;
        public int IndexCount;

        public void Execute(int index)
        {
            if (IndexPtr == null || (uint)index >= (uint)IndexCount)
                return;

            byte* ptr = (byte*)IndexPtr + index * UnsafeUtility.SizeOf<uint>();
            UnsafeUtility.AsRef<uint>(ptr) = (uint)index;
        }
    }

    internal static unsafe class AITextureMockMeshBenchmark
    {
        private const string NativeMemoryOwner = nameof(AITextureMockMeshBenchmark);

        [MenuItem("Hecton8/AI Texture Control Maps/Generate Mock Complex Mesh Benchmark", false, 2670)]
        internal static void GenerateMockComplexMeshAsset()
        {
            EnsureAssetFolder("Assets/_Project");
            EnsureAssetFolder("Assets/_Project/BakedGeometry");
            EnsureAssetFolder("Assets/_Project/BakedGeometry/AITexturing");
            EnsureAssetFolder(AITextureControlMapConstants.MockMeshFolder);
            EnsureFileFolder(AITextureControlMapConstants.MockBenchmarkReportPath);

            MockComplexMeshConfigDTO config;
            config.RingSegments = AITextureControlMapConstants.MockDefaultRingSegments;
            config.TubeSegments = AITextureControlMapConstants.MockDefaultTubeSegments;
            config.MajorRadius = 1.15f;
            config.TubeRadius = 0.18f;
            config.Irregularity = 0.65f;
            config.Seed = 0x5348494Eu;
            config.Twist = 1.73f;
            config._pad0 = 0u;

            int quadCount = config.RingSegments * config.TubeSegments;
            int vertexCount = quadCount * 6;
            NativeArray<AITextureBakeVertex> vertices = AITextureNativeMemory.AllocateArray<AITextureBakeVertex>(
                vertexCount,
                Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory,
                NativeMemoryOwner,
                nameof(vertices)); // COLD ALLOC: NativeArray<AITextureBakeVertex>[vertexCount] - editor mock complex mesh vertices - owner: AITextureMockMeshBenchmark
            NativeArray<uint> indices = AITextureNativeMemory.AllocateArray<uint>(
                vertexCount,
                Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory,
                NativeMemoryOwner,
                nameof(indices)); // COLD ALLOC: NativeArray<uint>[vertexCount] - editor mock complex mesh indices - owner: AITextureMockMeshBenchmark

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                JobHandle vertexHandle = new GenerateMockComplexMeshJob
                {
                    VertexPtr = NativeArrayUnsafeUtility.GetUnsafePtr(vertices),
                    Config = config,
                    VertexCount = vertexCount
                }.Schedule(quadCount, 64);
                JobHandle indexHandle = new FillUInt32IndexJob
                {
                    IndexPtr = NativeArrayUnsafeUtility.GetUnsafePtr(indices),
                    IndexCount = vertexCount
                }.Schedule(vertexCount, 128);
                JobHandle.CombineDependencies(vertexHandle, indexHandle).Complete();
                stopwatch.Stop();

                Mesh mesh = new Mesh
                {
                    name = "MSH_SHINOBU_269_MockComplexKnot"
                };
                mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertexBufferParams(vertexCount, AITextureControlMapVertexLayout.Layout);
                mesh.SetVertexBufferData(vertices, 0, 0, vertexCount, 0, MeshUpdateFlags.DontRecalculateBounds);
                mesh.SetIndexBufferParams(vertexCount, IndexFormat.UInt32);
                mesh.SetIndexBufferData(indices, 0, 0, vertexCount, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                mesh.subMeshCount = 1;
                mesh.SetSubMesh(0, new SubMeshDescriptor(0, vertexCount, MeshTopology.Triangles), MeshUpdateFlags.DontRecalculateBounds);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(false);

                string assetPath = AssetDatabase.GenerateUniqueAssetPath(AITextureControlMapConstants.MockMeshFolder + "/MSH_SHINOBU_269_MockComplexKnot.asset");
                AssetDatabase.CreateAsset(mesh, assetPath);
                AssetDatabase.SaveAssets();
                File.WriteAllText(AITextureControlMapConstants.MockBenchmarkReportPath, BuildReport(assetPath, vertexCount, quadCount, stopwatch.Elapsed.TotalMilliseconds));
                Hecton8.Core.H8Debug.Log("[AITextureMockMeshBenchmark] Generated mock mesh " + assetPath + " vertices=" + vertexCount.ToString(CultureInfo.InvariantCulture) + ".");
            }
            finally
            {
                AITextureNativeMemory.DisposeArray(ref indices);
                AITextureNativeMemory.DisposeArray(ref vertices);
            }
        }

        private static string BuildReport(string assetPath, int vertexCount, int quadCount, double milliseconds)
        {
            StringBuilder builder = new StringBuilder(768); // COLD ALLOC: StringBuilder[768] - editor mock benchmark report - owner: AITextureMockMeshBenchmark
            builder.Append("{\n");
            AppendJson(builder, "schema", "hecton8.ai_texture_mock_mesh_benchmark.v1", true);
            AppendJson(builder, "assetPath", assetPath, true);
            AppendJson(builder, "vertexCount", vertexCount, true);
            AppendJson(builder, "quadCount", quadCount, true);
            AppendJson(builder, "jobMilliseconds", milliseconds.ToString("0.000", CultureInfo.InvariantCulture), true);
            AppendJson(builder, "geometry", "twisted-irregular-knot-uv-stress", true);
            AppendJson(builder, "status", "PENDING_UNITY_IMPORT_VERIFICATION", false);
            builder.Append("}\n");
            return builder.ToString();
        }

        private static void AppendJson(StringBuilder builder, string key, string value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": \"").Append(Escape(value)).Append('"');
            builder.Append(comma ? ",\n" : "\n");
        }

        private static void AppendJson(StringBuilder builder, string key, int value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            builder.Append(comma ? ",\n" : "\n");
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            int slash = assetPath.LastIndexOf('/');
            if (slash <= 0)
                return;

            string parent = assetPath.Substring(0, slash);
            string folder = assetPath.Substring(slash + 1);
            EnsureAssetFolder(parent);
            if (!AssetDatabase.IsValidFolder(assetPath))
                AssetDatabase.CreateFolder(parent, folder);
        }

        private static void EnsureFileFolder(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }
    }
}
#endif
